using CADability.GeoObject;

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
        /// reparametrisation was singular, its inverse failed or exploded.
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
                        if (!ns.GetSimpleSurface(0.0, out ISurface simple, out ModOp2D reparametrisation) || !(simple is SphericalSurface)) continue;
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
