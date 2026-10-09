using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// ICurve.Length and ICurve2D.Length of curves without a closed form for the arc length: elliptical arcs and
    /// intersection curves. These lengths are the cutting lengths of tubes read from STEP files - a miter cut is an
    /// ellipse, a cross bore an intersection curve of two cylinders - so they must be exact, not estimated from a
    /// polygon through a few points of the curve, which is always too short.
    /// </summary>
    [TestClass]
    public class CurveLengthTests
    {
        /// <summary>
        /// The integral of <paramref name="speed"/> over [from, from+sweep] by the midpoint rule with 2^17 and 2^18
        /// steps, extrapolated (the error of the midpoint rule is c2*h^2 + c4*h^4 + ...). Independent of the
        /// adaptive Gauss-Legendre quadrature used in the library.
        /// </summary>
        private static double Integral(Func<double, double> speed, double from, double sweep)
        {
            double Midpoint(int n)
            {
                double h = sweep / n, sum = 0.0;
                for (int i = 0; i < n; ++i) sum += speed(from + (i + 0.5) * h);
                return Math.Abs(sum * h);
            }
            double coarse = Midpoint(1 << 17), fine = Midpoint(1 << 18);
            return fine + (fine - coarse) / 3.0;
        }

        /// <summary>The length of the arc of the ellipse cos(t)*a, sin(t)*b from t0 over sweep.</summary>
        private static double EllipseReference(double a, double b, double t0, double sweep)
        {
            return Integral(t => Math.Sqrt(a * a * Math.Sin(t) * Math.Sin(t) + b * b * Math.Cos(t) * Math.Cos(t)), t0, sweep);
        }

        private static void AssertRelative(double expected, double actual, double relativeTolerance, string message = "")
        {
            Assert.IsTrue(Math.Abs(actual - expected) <= relativeTolerance * Math.Abs(expected),
                $"{message} expected {expected:R}, actual {actual:R}, relative error {(actual - expected) / expected:E2}");
        }

        public static IEnumerable<object[]> EllipseCases
        {
            get
            {
                double[][] axes = { new[] { 30 * Math.Sqrt(2), 30.0 }, new[] { 5.0, 4.99 }, new[] { 10.0, 1.0 }, new[] { 100.0, 3.0 }, new[] { 2.0, 7.0 } };
                double[][] arcs = { new[] { 0.0, 2 * Math.PI }, new[] { 0.3, 1.1 }, new[] { -0.5, 4.0 }, new[] { 2.0, -2.5 }, new[] { 1.0, Math.PI } };
                foreach (double[] ax in axes)
                    foreach (double[] arc in arcs)
                        yield return new object[] { ax[0], ax[1], arc[0], arc[1] };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(EllipseCases))]
        public void the_length_of_an_ellipse_is_the_elliptic_integral(double a, double b, double t0, double sweep)
        {
            double expected = EllipseReference(a, b, t0, sweep);
            // tilted in space, so that nothing depends on the axes being parallel to the coordinate axes
            GeoVector dirA = new GeoVector(1, 2, 3).Normalized;
            GeoVector dirB = (dirA ^ new GeoVector(0, 0, 1)).Normalized;
            GeoPoint center = new GeoPoint(7, -3, 11);
            Ellipse elli = Ellipse.Construct();
            elli.SetEllipseArcCenterAxis(center, a * dirA, b * dirB, t0, sweep);
            ICurve c3d = elli;
            for (int i = 0; i <= 4; ++i)
            {   // make sure the reference measures the same curve
                double t = t0 + i * sweep / 4;
                Assert.IsTrue((c3d.PointAt(i / 4.0) | center + Math.Cos(t) * a * dirA + Math.Sin(t) * b * dirB) < 1e-9);
            }
            AssertRelative(expected, c3d.Length, 1e-9, "3d");

            // the same arc in its plane, with the axes rotated against the coordinate axes of the plane
            GeoVector px = Math.Cos(0.4) * dirA + Math.Sin(0.4) * dirB, py = -Math.Sin(0.4) * dirA + Math.Cos(0.4) * dirB;
            ICurve2D c2d = c3d.GetProjectedCurve(new Plane(center, px, py));
            Assert.IsInstanceOfType(c2d, typeof(Ellipse2D)); // EllipseArc2D for the arcs
            AssertRelative(expected, c2d.Length, 1e-9, "2d");
        }

        [DataTestMethod]
        [DataRow(30.0, 30.0)]
        [DataRow(42.42640687119285, 30.0)]
        [DataRow(10.0, 1.0)]
        [DataRow(1.0, 50.0)]
        public void the_circumference_of_a_2d_ellipse(double a, double b)
        {
            GeoVector2D dir = new GeoVector2D(0.6, 0.8);
            Ellipse2D elli = new Ellipse2D(new GeoPoint2D(3, 4), a * dir, b * dir.ToLeft());
            AssertRelative(EllipseReference(a, b, 0.0, 2 * Math.PI), elli.Length, 1e-9);
        }

        [TestMethod]
        public void a_degenerate_ellipse_is_a_line_traversed_back_and_forth()
        {
            Assert.AreEqual(4 * 5.0, ArcLength.OfEllipse(25.0, 0.0, 0.0, 0.0, 2 * Math.PI), 1e-12);
            Assert.AreEqual(2 * 5.0, ArcLength.OfEllipse(0.0, 25.0, 0.0, -Math.PI / 2, Math.PI), 1e-12); // from (0,-5) to (0,5)
            Assert.AreEqual(3.0 * 1.5, ArcLength.OfEllipse(9.0, 9.0, 0.0, 1.0, -1.5), 1e-12); // a circle
        }

        // the tube of the bug report: outer radius 30, wall 3, length 500 along +z
        private const double outerRadius = 30.0, innerRadius = 27.0;

        private static Solid RoundTube()
        {
            Face outerFace = Face.MakeFace(new PlaneSurface(Plane.XYPlane), new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, outerRadius)));
            Solid outer = Make3D.MakePrism(outerFace, new GeoVector(0, 0, 500), null) as Solid;
            Face innerFace = Face.MakeFace(new PlaneSurface(new Plane(new GeoPoint(0, 0, -10), GeoVector.XAxis, GeoVector.YAxis)), new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, innerRadius)));
            Solid inner = Make3D.MakePrism(innerFace, new GeoVector(0, 0, 520), null) as Solid;
            Solid[] tube = Solid.Subtract(outer, inner);
            Assert.AreEqual(1, tube.Length);
            return tube[0];
        }

        [TestMethod]
        public void the_contours_of_a_miter_cut_have_the_length_of_the_ellipses()
        {
            // a 45 degree miter cut: subtract a box whose face lies in the plane through (0,0,450) with normal (1,0,1)
            Plane pl = new Plane(new GeoPoint(0, 0, 450), new GeoVector(1, 0, 1));
            GeoVector dx = 200 * pl.DirectionX, dy = 200 * pl.DirectionY;
            Solid box = Make3D.MakeBox(pl.Location - dx - dy, 2 * dx, 2 * dy, 200 * pl.Normal);
            Solid[] cut = Solid.Subtract(RoundTube(), box);
            Assert.AreEqual(1, cut.Length);

            double outerSum = 0.0, innerSum = 0.0;
            int outerCount = 0, innerCount = 0;
            foreach (Edge edge in cut[0].Shells[0].Edges)
            {
                if (!(edge.Curve3D is Ellipse elli) || elli.IsCircle) continue;
                GeoPoint m = edge.Curve3D.PointAt(0.5);
                double r = Math.Sqrt(m.x * m.x + m.y * m.y);
                if (Math.Abs(r - outerRadius) < 1e-6) { outerSum += edge.Curve3D.Length; ++outerCount; }
                else if (Math.Abs(r - innerRadius) < 1e-6) { innerSum += edge.Curve3D.Length; ++innerCount; }
                else Assert.Fail($"unexpected elliptical edge at radius {r}");
            }
            Assert.IsTrue(outerCount > 0 && innerCount > 0);
            // the section of a cylinder of radius r with a plane at 45 degrees is an ellipse with the half axes r*sqrt(2) and r
            AssertRelative(EllipseReference(outerRadius * Math.Sqrt(2), outerRadius, 0, 2 * Math.PI), outerSum, 1e-9, "outer contour");
            AssertRelative(EllipseReference(innerRadius * Math.Sqrt(2), innerRadius, 0, 2 * Math.PI), innerSum, 1e-9, "inner contour");
            Assert.AreEqual(229.21187, outerSum, 1e-5);
        }

        /// <summary>
        /// The length of the intersection of the tube wall of radius <paramref name="r"/> (around z) with the bore of
        /// radius 5 (around the x-axis at z == 250): y == 5*cos(phi), z == 250 + 5*sin(phi), x == sqrt(r^2 - y^2).
        /// </summary>
        private static double BoreReference(double r)
        {
            return Integral(phi =>
            {
                double y = 5 * Math.Cos(phi), dy = -5 * Math.Sin(phi), dz = 5 * Math.Cos(phi);
                double dx = -y * dy / Math.Sqrt(r * r - y * y);
                return Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }, 0.0, 2 * Math.PI);
        }

        private static Solid CrossBoredTube()
        {
            Solid drill = Make3D.MakeCylinder(new GeoPoint(-50, 0, 250), 5 * GeoVector.YAxis, 100 * GeoVector.XAxis);
            Solid[] bored = Solid.Subtract(RoundTube(), drill);
            Assert.AreEqual(1, bored.Length);
            return bored[0];
        }

        /// <summary>The edges of the hole in the wall of radius <paramref name="r"/> on the +x side of the tube.</summary>
        private static List<ICurve> BoreContour(Solid bored, double r)
        {
            List<ICurve> res = new List<ICurve>();
            foreach (Edge edge in bored.Shells[0].Edges)
            {
                GeoPoint m = edge.Curve3D.PointAt(0.5);
                if (m.x > 0 && Math.Abs(Math.Sqrt(m.x * m.x + m.y * m.y) - r) < 1e-6 && Math.Abs(m.z - 250) < 6) res.Add(edge.Curve3D);
            }
            return res;
        }

        [TestMethod]
        public void the_contours_of_a_cross_bore_have_the_length_of_the_intersection_curves()
        {
            Solid bored = CrossBoredTube();
            foreach (double r in new[] { outerRadius, innerRadius })
            {
                List<ICurve> contour = BoreContour(bored, r);
                Assert.IsTrue(contour.Count > 0, $"no contour at radius {r}");
                Assert.IsTrue(contour.Exists(c => c is InterpolatedDualSurfaceCurve), "the contour is expected to be an intersection curve");
                double sum = 0.0;
                foreach (ICurve c in contour) sum += c.Length;
                AssertRelative(BoreReference(r), sum, 1e-6, $"contour at radius {r}");
                if (r == outerRadius) Assert.AreEqual(31.47117, sum, 1e-5);
            }
        }

        [TestMethod]
        public void the_length_of_an_intersection_curve_follows_its_modifications()
        {
            InterpolatedDualSurfaceCurve idsc = BoreContour(CrossBoredTube(), outerRadius).Find(c => c is InterpolatedDualSurfaceCurve) as InterpolatedDualSurfaceCurve;
            Assert.IsNotNull(idsc);
            double l = idsc.Length;

            ICurve[] parts = idsc.Split(0.3);
            Assert.AreEqual(2, parts.Length);
            AssertRelative(l, parts[0].Length + parts[1].Length, 1e-9, "the parts of a split curve");

            ICurve reversed = idsc.Clone() as ICurve;
            reversed.Reverse();
            AssertRelative(l, reversed.Length, 1e-9, "reversed");

            ICurve trimmed = idsc.Clone() as ICurve;
            Assert.AreEqual(l, trimmed.Length, 1e-9 * l); // now cached in the clone
            trimmed.Trim(0.0, 0.3);
            AssertRelative(parts[0].Length, trimmed.Length, 1e-9, "trimmed");

            ICurve scaled = idsc.Clone() as ICurve;
            Assert.AreEqual(l, scaled.Length, 1e-9 * l);
            (scaled as IGeoObject).Modify(ModOp.Scale(GeoPoint.Origin, 2.0));
            AssertRelative(2 * l, scaled.Length, 1e-9, "scaled");
        }

        [TestMethod]
        public void the_length_of_a_sine_curve_is_exact()
        {
            // the unrolled miter cut: v == 7*sin(u-0.4)+30 for u from 1.0 to 3.5
            SineCurve2D sine = new SineCurve2D(0.6, 2.5, new ModOp2D(1, 0, 0.4, 0, 7, 30));
            for (int i = 0; i <= 4; ++i)
            {
                GeoPoint2D p = sine.PointAt(i / 4.0);
                Assert.AreEqual(1.0 + i * 2.5 / 4, p.x, 1e-12);
                Assert.AreEqual(7 * Math.Sin(p.x - 0.4) + 30, p.y, 1e-12);
            }
            double expected = Integral(u => Math.Sqrt(1 + 49 * Math.Cos(u) * Math.Cos(u)), 0.6, 2.5);
            AssertRelative(expected, sine.Length, 1e-9);
        }
    }
}
