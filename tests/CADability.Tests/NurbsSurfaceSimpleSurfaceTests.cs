using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability.Tests
{
    // Regression tests for issue #347: NurbsSurface.GetSimpleSurface and GetCanonicalForm looped forever for
    // surfaces with a tiny parameter domain and a singularity at the domain boundary, because the sample
    // parameters were searched with a fixed singularity tolerance of 1e-6, which covered the whole domain.
    [TestClass]
    public class NurbsSurfaceSimpleSurfaceTests
    {
        // generous limit: the calls return within milliseconds when they terminate at all
        private const int TimeoutMilliseconds = 20000;

        /// <summary>
        /// Degree 2x2 NURBS patch with the u-domain [0, uSpan]. All poles of the last u-row coincide, so the
        /// surface is singular at umax. With <paramref name="singularAtUMin"/> the first u-row collapses too.
        /// </summary>
        private static NurbsSurface MakeSurface(double uSpan, bool singularAtUMin)
        {
            GeoPoint[,] poles = new GeoPoint[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    poles[i, j] = new GeoPoint(i, j, (i == 1 && j == 1) ? 0.3 : 0.0);
                }
            }
            for (int j = 0; j < 3; j++) poles[2, j] = new GeoPoint(2, 1, 0); // singular at umax
            if (singularAtUMin)
            {
                for (int j = 0; j < 3; j++) poles[0, j] = new GeoPoint(0, 1, 0); // singular at umin
            }
            return new NurbsSurface(poles, null, new double[] { 0, uSpan }, new double[] { 0, 1 },
                new int[] { 3, 3 }, new int[] { 3, 3 }, 2, 2, false, false);
        }

        /// <summary>
        /// Runs <paramref name="action"/> on a background task and fails instead of hanging the test run
        /// when it does not return in time.
        /// </summary>
        private static void AssertTerminates(Action action, string name)
        {
            Task task = Task.Factory.StartNew(action, TaskCreationOptions.LongRunning);
            Assert.IsTrue(task.Wait(TimeoutMilliseconds), name + " did not return (infinite loop, issue #347)");
        }

        [TestMethod]
        public void GetCanonicalForm_TinyDomainSingularAtMax_Terminates()
        {
            NurbsSurface ns = MakeSurface(5e-7, false);
            AssertTerminates(() => ns.GetCanonicalForm(1e-3, null), "GetCanonicalForm");
        }

        [TestMethod]
        public void GetSimpleSurface_TinyDomainSingularAtBothEnds_Terminates()
        {
            NurbsSurface ns = MakeSurface(1.5e-6, true);
            AssertTerminates(() => ns.GetSimpleSurface(0.0, out ISurface _, out ModOp2D _), "GetSimpleSurface");
        }

        /// <summary>
        /// Rational degree 2x2 NURBS of a sphere with radius 10 around the origin: three quarters around the z-axis
        /// (u in [0, 3 * knotSpan]) from the south pole to the equator (v in [0, knotSpan]), moved by <paramref name="m"/>.
        /// The poles are computed from the angles, as an exporter would do, so the poles of the row v = 0 all lie at
        /// the south pole, up to rounding.
        /// </summary>
        private static NurbsSurface MakeSphere(ModOp m, double knotSpan)
        {
            double w = Math.Sqrt(0.5);
            // control points of a quarter arc from angle a: (cos a, sin a), then the corner (cos a - sin a, sin a + cos a) with weight w
            (double c, double s, double w) ArcPole(double start, int k)
            {
                double a = start + (k / 2) * Math.PI / 2;
                return k % 2 == 0 ? (Math.Cos(a), Math.Sin(a), 1.0) : (Math.Cos(a) - Math.Sin(a), Math.Sin(a) + Math.Cos(a), w);
            }
            GeoPoint[,] poles = new GeoPoint[7, 3];
            double[,] weights = new double[7, 3];
            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    var u = ArcPole(0.0, i);
                    var v = ArcPole(-Math.PI / 2, j);
                    poles[i, j] = m * new GeoPoint(10 * u.c * v.c, 10 * u.s * v.c, 10 * v.s);
                    weights[i, j] = u.w * v.w;
                }
            }
            return new NurbsSurface(poles, weights, new double[] { 0, knotSpan, 2 * knotSpan, 3 * knotSpan }, new double[] { 0, knotSpan },
                new int[] { 3, 2, 2, 3 }, new int[] { 3, 3 }, 2, 2, false, false);
        }

        /// <summary>
        /// The sphere found by GetSimpleSurface has the z-axis, so when the NURBS sphere is tilted, its pole is an
        /// ordinary point of the sphere and the corners (umin, vmin) and (umax, vmin) get the same sphere parameters,
        /// up to rounding. The reparametrisation fitted to the corners then had a determinant of about 1e-16 (1e-22 with
        /// knot spans of 1000) instead of exactly 0.0, which skipped the fit to inner points: the returned
        /// reparametrisation was singular, its inverse failed or exploded. The rational parametrisation of the NURBS sphere
        /// does not run linearly with the angles, so only the variant for callers that need the geometry alone
        /// (geometryOnly, used by Shell.MakeRegularSurfaces) returns the sphere with an approximate reparametrisation.
        /// </summary>
        [TestMethod]
        public void GetSimpleSurface_TiltedSphereWithPoleAtTheCorners_ReparametrisationIsRegular()
        {
            List<string> singular = new List<string>();
            int spheres = 0;
            foreach (GeoVector axis in new[] { new GeoVector(1, 1, 1), new GeoVector(3, 1, 2), new GeoVector(1, 0, 0), new GeoVector(1, 1, 0) })
            {
                foreach (double angle in new[] { 0.5, 1.5, 2.5, 3.0 })
                {
                    foreach (double knotSpan in new[] { 1.0, 1000.0 })
                    {
                        ModOp m = ModOp.Translate(10, 20, 30) * ModOp.Rotate(axis.Normalized, new SweepAngle(angle));
                        NurbsSurface ns = MakeSphere(m, knotSpan);
                        if (!ns.GetSimpleSurface(0.0, out ISurface simple, out ModOp2D reparametrisation, true) || !(simple is SphericalSurface)) continue;
                        ++spheres;
                        // the images of the corners must span a triangle: its area relative to the square of its longest side
                        GeoPoint2D p0 = reparametrisation * new GeoPoint2D(0, 0);
                        GeoPoint2D p1 = reparametrisation * new GeoPoint2D(3 * knotSpan, 0);
                        GeoPoint2D p2 = reparametrisation * new GeoPoint2D(0, knotSpan);
                        GeoVector2D d1 = p1 - p0, d2 = p2 - p0;
                        double longest = Math.Max(Math.Max(d1.Length, d2.Length), (p2 - p1).Length);
                        double shape = Math.Abs(d1.x * d2.y - d1.y * d2.x) / (longest * longest);
                        if (!(shape > 1e-6)) singular.Add($"axis {axis}, angle {angle}, knot span {knotSpan}: determinant {reparametrisation.Determinant}, area/side^2 {shape}");
                    }
                }
            }
            Assert.IsTrue(spheres > 0, "no sphere was found at all");
            Assert.AreEqual(0, singular.Count, "singular reparametrisation for " + string.Join("; ", singular));
        }

        private static readonly double halfSqrt2 = Math.Sqrt(0.5);

        /// <summary>
        /// The control net of the unit circle as rational quadratic arcs: a full circle of four arcs or a quarter circle.
        /// </summary>
        private static (double x, double y, double w)[] UnitCircle(bool full)
        {
            if (!full) return new[] { (1.0, 0.0, 1.0), (1.0, 1.0, halfSqrt2), (0.0, 1.0, 1.0) };
            return new[] { (1.0, 0.0, 1.0), (1.0, 1.0, halfSqrt2), (0.0, 1.0, 1.0), (-1.0, 1.0, halfSqrt2), (-1.0, 0.0, 1.0),
                (-1.0, -1.0, halfSqrt2), (0.0, -1.0, 1.0), (1.0, -1.0, halfSqrt2), (1.0, 0.0, 1.0) };
        }

        /// <summary>
        /// The exact rational NURBS of the profile (r, z, weight) rotated around the z-axis by a full or a quarter turn, as
        /// STEP files contain them for cylinders, cones, spheres and tori. u runs around the axis.
        /// </summary>
        private static NurbsSurface Revolved((double r, double z, double w)[] profile, double[] vKnots, int[] vMults, int vDegree, bool full)
        {
            var circle = UnitCircle(full);
            GeoPoint[,] poles = new GeoPoint[circle.Length, profile.Length];
            double[,] weights = new double[circle.Length, profile.Length];
            for (int i = 0; i < circle.Length; i++)
            {
                for (int j = 0; j < profile.Length; j++)
                {
                    poles[i, j] = new GeoPoint(profile[j].r * circle[i].x, profile[j].r * circle[i].y, profile[j].z);
                    weights[i, j] = circle[i].w * profile[j].w;
                }
            }
            double[] uKnots = full ? new[] { 0.0, 0.25, 0.5, 0.75, 1.0 } : new[] { 0.0, 1.0 };
            int[] uMults = full ? new[] { 3, 2, 2, 2, 3 } : new[] { 3, 3 };
            return new NurbsSurface(poles, weights, uKnots, vKnots, uMults, vMults, 2, vDegree, false, false);
        }

        private static IEnumerable<(string name, NurbsSurface surface, Type shape)> RationalSurfaces()
        {
            const double R = 5, r = 2, H = 10;
            var line = new[] { (R, 0.0, 1.0), (R, H, 1.0) };
            yield return ("cylinder", Revolved(line, new[] { 0.0, 1.0 }, new[] { 2, 2 }, 1, false), typeof(CylindricalSurface));
            yield return ("full cylinder", Revolved(line, new[] { 0.0, 1.0 }, new[] { 2, 2 }, 1, true), typeof(CylindricalSurface));
            yield return ("cone", Revolved(new[] { (R, 0.0, 1.0), (R / 2, H, 1.0) }, new[] { 0.0, 1.0 }, new[] { 2, 2 }, 1, false), typeof(ConicalSurface));
            var halfCircle = new[] { (0.0, -R, 1.0), (R, -R, halfSqrt2), (R, 0.0, 1.0), (R, R, halfSqrt2), (0.0, R, 1.0) };
            yield return ("sphere", Revolved(halfCircle, new[] { 0.0, 0.5, 1.0 }, new[] { 3, 2, 3 }, 2, false), typeof(SphericalSurface));
            var quarterCircle = new[] { (R, 0.0, 1.0), (R, R, halfSqrt2), (0.0, R, 1.0) };
            yield return ("octant of a sphere", Revolved(quarterCircle, new[] { 0.0, 1.0 }, new[] { 3, 3 }, 2, false), typeof(SphericalSurface));
            var tube = UnitCircle(false);
            var torusProfile = new (double, double, double)[3];
            for (int i = 0; i < 3; i++) torusProfile[i] = (R + r * tube[i].x, r * tube[i].y, tube[i].w);
            yield return ("torus", Revolved(torusProfile, new[] { 0.0, 1.0 }, new[] { 3, 3 }, 2, false), typeof(ToroidalSurface));
        }

        private static double SizeOf(NurbsSurface ns)
        {
            ns.GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
            BoundingCube ext = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i <= 10; i++)
                for (int j = 0; j <= 10; j++) ext.MinMax(ns.PointAt(new GeoPoint2D(umin + i * (umax - umin) / 10, vmin + j * (vmax - vmin) / 10)));
            return ext.Size;
        }

        /// <summary>
        /// The largest distance between a point of <paramref name="ns"/> and the point of <paramref name="simple"/> at the
        /// reparametrised parameters, on a grid of 21 x 21 points.
        /// </summary>
        private static double ReparametrisationError(NurbsSurface ns, ISurface simple, ModOp2D reparametrisation)
        {
            ns.GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
            double max = 0.0;
            for (int i = 0; i <= 20; i++)
            {
                for (int j = 0; j <= 20; j++)
                {
                    GeoPoint2D uv = new GeoPoint2D(umin + i * (umax - umin) / 20, vmin + j * (vmax - vmin) / 20);
                    max = Math.Max(max, ns.PointAt(uv) | simple.PointAt(reparametrisation * uv));
                }
            }
            return max;
        }

        /// <summary>
        /// GetSimpleSurface found the right cylinder, cone, sphere or torus for an exact rational NURBS, but returned a
        /// reparametrisation fitted to three points. The rational parametrisation does not run linearly with the angle, so
        /// the reparametrised points were up to 0.1 (cylinder, cone) and up to the diameter (sphere, torus) away from the
        /// points of the NURBS. A face simplified with Face.MakeRegularSurface got wrong edges, and NurbsSurface.MaxDist,
        /// which the triangulation uses, measured the distance at the wrong points. Now the reparametrisation is checked,
        /// and such a surface is not simplified.
        /// </summary>
        [TestMethod]
        public void GetSimpleSurface_ReparametrisationMapsToTheSamePoints()
        {
            foreach ((string name, NurbsSurface ns, Type _) in RationalSurfaces())
            {
                double precision = SizeOf(ns) * 1e-6;
                if (ns.GetSimpleSurface(precision, out ISurface simple, out ModOp2D reparametrisation))
                {
                    double error = ReparametrisationError(ns, simple, reparametrisation);
                    Assert.IsTrue(error <= precision, $"{name}: {simple.GetType().Name}, the reparametrised points are {error} away");
                }
            }
        }

        /// <summary>
        /// A closed but not periodic NURBS torus has its first and last test circle at the same place, and the plane through
        /// the three centers threw a PlaneException.
        /// </summary>
        [TestMethod]
        public void GetSimpleSurface_FullTorus_DoesNotThrow()
        {
            var tube = UnitCircle(true);
            var profile = new (double, double, double)[tube.Length];
            for (int i = 0; i < tube.Length; i++) profile[i] = (5 + 2 * tube[i].x, 2 * tube[i].y, tube[i].w);
            NurbsSurface ns = Revolved(profile, new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }, new[] { 3, 2, 2, 2, 3 }, 2, true);
            ns.GetSimpleSurface(SizeOf(ns) * 1e-6, out ISurface _, out ModOp2D _);
            ns.GetSimpleSurface(SizeOf(ns) * 1e-6, out ISurface _, out ModOp2D _, true);
        }

        /// <summary>
        /// Shell.MakeRegularSurfaces recomputes the edges from their 3D curves, so it only needs the shape and still gets the
        /// cylinder, cone, sphere or torus of an exact rational NURBS.
        /// </summary>
        [TestMethod]
        public void GetSimpleSurface_GeometryOnly_FindsTheShape()
        {
            foreach ((string name, NurbsSurface ns, Type shape) in RationalSurfaces())
            {
                double precision = SizeOf(ns) * 1e-6;
                Assert.IsTrue(ns.GetSimpleSurface(precision, out ISurface simple, out ModOp2D _, true), $"{name}: no simple surface");
                Assert.IsInstanceOfType(simple, shape, name);
                ns.GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
                for (int i = 0; i <= 10; i++)
                {
                    for (int j = 0; j <= 10; j++)
                    {
                        GeoPoint p = ns.PointAt(new GeoPoint2D(umin + i * (umax - umin) / 10, vmin + j * (vmax - vmin) / 10));
                        Assert.IsTrue((p | simple.PointAt(simple.PositionOf(p))) <= precision, $"{name}: {p} is not on the {shape.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// NurbsSurface.MaxDist, which decides how fine the triangulation gets, used the torus found by GetSimpleSurface with
        /// the wrong reparametrisation and measured the distance at the wrong points: the triangulation of this patch of a
        /// torus was 29 % too large at a precision of 0.01, and triangles were 12 times the precision away from the surface.
        /// </summary>
        [TestMethod]
        public void TriangulationOfARationalTorusPatch_HasTheAreaOfTheTorus()
        {
            const double R = 5, r = 2;
            var tube = UnitCircle(false);
            var profile = new (double, double, double)[3];
            for (int i = 0; i < 3; i++) profile[i] = (R + r * tube[i].x, r * tube[i].y, tube[i].w);
            NurbsSurface ns = Revolved(profile, new[] { 0.0, 1.0 }, new[] { 3, 3 }, 2, false);
            Face face = Face.MakeFace(ns, new CADability.Shapes.SimpleShape(CADability.Shapes.Border.MakeRectangle(0, 1, 0, 1)));
            face.GetTriangulation(0.01, out GeoPoint[] points, out GeoPoint2D[] _, out int[] triangles, out BoundingCube _);
            double area = 0.0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                area += ((points[triangles[i + 1]] - points[triangles[i]]) ^ (points[triangles[i + 2]] - points[triangles[i]])).Length / 2.0;
            }
            // a quarter turn of the outer upper quarter of the tube: pi/2 * integral of (R + r cos t) r dt for t from 0 to pi/2
            double expected = Math.PI / 2 * r * (R * Math.PI / 2 + r);
            Assert.AreEqual(expected, area, 0.01 * expected);
        }

        /// <summary>
        /// A NURBS whose parametrisation is affine, here a planar patch with evenly spaced poles of degree 2, is still
        /// simplified, with an exact reparametrisation.
        /// </summary>
        [TestMethod]
        public void GetSimpleSurface_AffinePlanarPatch_IsSimplified()
        {
            GeoPoint[,] poles = new GeoPoint[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++) poles[i, j] = new GeoPoint(1 + 2 * i + 0.5 * j, 3 + j, 2 + 0.3 * i - 0.2 * j);
            NurbsSurface ns = new NurbsSurface(poles, null, new double[] { 0, 1 }, new double[] { 0, 1 }, new int[] { 3, 3 }, new int[] { 3, 3 }, 2, 2, false, false);
            double precision = SizeOf(ns) * 1e-6;
            Assert.IsTrue(ns.GetSimpleSurface(precision, out ISurface simple, out ModOp2D reparametrisation));
            Assert.IsInstanceOfType(simple, typeof(PlaneSurface));
            Assert.IsTrue(ReparametrisationError(ns, simple, reparametrisation) <= precision);
        }

        [TestMethod]
        public void GetSimpleSurface_RegularDomainSingularAtMax_Terminates()
        {
            // control case, which already worked before the fix
            NurbsSurface ns = MakeSurface(1e-3, true);
            AssertTerminates(() => ns.GetSimpleSurface(0.0, out ISurface _, out ModOp2D _), "GetSimpleSurface");
            AssertTerminates(() => ns.GetCanonicalForm(1e-3, null), "GetCanonicalForm");
        }
    }
}
