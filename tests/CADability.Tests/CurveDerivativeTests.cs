using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// <c>TryPointDeriv2At</c> has to return the derivatives of <c>PointAt</c> by the same parameter
    /// <c>PointAt</c> takes, the normalized position from 0 to 1. Three independent errors kept it from
    /// doing that, and each one needed a special curve to show at all:
    /// <list type="bullet">
    /// <item>Arc2D evaluated a negative sweep at the angle mirrored at the x axis, so it reported a point
    /// that is not the point of that position - a positive sweep was fine.</item>
    /// <item>BSpline2D and BSpline returned the derivatives by the KNOT parameter. The chain rule factor is
    /// the length of the knot range, and its square for the second derivative, so a spline with a knot
    /// range of 0 to 1 - which is what this library builds itself - showed nothing.</item>
    /// <item>Nurbs.RatCurveDerivs2 got the rational correction wrong, which needs a curve with weights that
    /// are not all equal. The first derivative came out up to 1.3 degrees off on the exact NURBS circle,
    /// while DirectionAt, which goes through RatCurveDerivs1, was exact on the very same curve.</item>
    /// </list>
    /// The tests compare against central differences: the first derivative against those of PointAt, the
    /// second against those of DirectionAt, which is an independent implementation of the first.
    /// </summary>
    [TestClass]
    public class CurveDerivativeTests
    {
        private const double h = 1e-6;
        private const double tolerance = 1e-5;

        /// <summary>
        /// The exact rational quadratic circle: nine poles, weights alternating 1 and sqrt(2)/2, four spans
        /// of 90 degrees. Rational, so it exercises the weight correction.
        /// </summary>
        private static GeoPoint2D[] CirclePoles(double r) => new GeoPoint2D[]
        {
            new GeoPoint2D(r, 0), new GeoPoint2D(r, r), new GeoPoint2D(0, r), new GeoPoint2D(-r, r),
            new GeoPoint2D(-r, 0), new GeoPoint2D(-r, -r), new GeoPoint2D(0, -r), new GeoPoint2D(r, -r),
            new GeoPoint2D(r, 0)
        };

        private static double[] CircleWeights()
        {
            double w = Math.Sqrt(2.0) / 2.0;
            return new double[] { 1, w, 1, w, 1, w, 1, w, 1 };
        }

        private static BSpline2D RationalCircle2D(double r, double from, double to)
        {
            double s = (to - from) / 4.0;
            double[] knots = { from, from + s, from + 2 * s, from + 3 * s, to };
            return new BSpline2D(CirclePoles(r), CircleWeights(), knots, new int[] { 3, 2, 2, 2, 3 }, 2, false, from, to);
        }

        /// <summary>All weights equal, so this one goes through the non rational branch.</summary>
        private static BSpline2D PolynomialSpline2D(double from, double to)
        {
            GeoPoint2D[] poles =
            {
                new GeoPoint2D(0, 0), new GeoPoint2D(10, 25), new GeoPoint2D(30, -15),
                new GeoPoint2D(50, 20), new GeoPoint2D(70, 0)
            };
            double s = (to - from) / 2.0;
            return new BSpline2D(poles, new double[] { 1, 1, 1, 1, 1 }, new double[] { from, from + s, to },
                new int[] { 3, 2, 3 }, 2, false, from, to);
        }

        private static BSpline RationalCircle3D(double r)
        {
            GeoPoint2D[] poles2d = CirclePoles(r);
            GeoPoint[] poles = new GeoPoint[poles2d.Length];
            for (int i = 0; i < poles.Length; i++) poles[i] = Plane.XYPlane.ToGlobal(poles2d[i]);
            BSpline spline = BSpline.Construct();
            spline.SetData(2, poles, CircleWeights(), new double[] { 0, 1, 2, 3, 4 },
                new int[] { 3, 2, 2, 2, 3 }, false);
            return spline;
        }

        private static BSpline Helix()
        {
            const int n = 61;
            GeoPoint[] points = new GeoPoint[n];
            for (int i = 0; i < n; ++i)
            {
                double a = i * 4 * Math.PI / (n - 1);
                points[i] = new GeoPoint(10 * Math.Cos(a), 10 * Math.Sin(a), 20 * a / (2 * Math.PI));
            }
            BSpline spline = BSpline.Construct();
            Assert.IsTrue(spline.ThroughPoints(points, 3, false));
            return spline;
        }

        private static void AssertDerivatives(string what, ICurve2D curve)
        {
            // Positions away from the knots on purpose: the nine pole circle has its interior knots at
            // multiplicity 2, which for degree 2 leaves the curve C1 but not C2. A central difference across
            // such a knot would compare against the average of two different one sided values.
            for (int i = 1; i < 10; i++)
            {
                double u = (i + 0.37) / 10.0;
                Assert.IsTrue(curve.TryPointDeriv2At(u, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2),
                    $"{what} at {u}: no second derivative");
                Assert.IsTrue((point | curve.PointAt(u)) < 1e-9, $"{what} at {u}: the point is not the point of that position");

                GeoVector2D numeric1 = (1.0 / (2.0 * h)) * (curve.PointAt(u + h) - curve.PointAt(u - h));
                GeoVector2D numeric2 = (1.0 / (2.0 * h)) * (curve.DirectionAt(u + h) - curve.DirectionAt(u - h));
                Close($"{what} at {u}: first derivative", numeric1, deriv1);
                Close($"{what} at {u}: DirectionAt", numeric1, curve.DirectionAt(u));
                Close($"{what} at {u}: second derivative", numeric2, deriv2);
            }
        }

        private static void AssertDerivatives(string what, ICurve curve)
        {
            for (int i = 1; i < 10; i++)
            {
                double u = (i + 0.37) / 10.0;
                Assert.IsTrue(curve.TryPointDeriv2At(u, out GeoPoint point, out GeoVector deriv1, out GeoVector deriv2),
                    $"{what} at {u}: no second derivative");
                Assert.IsTrue((point | curve.PointAt(u)) < 1e-9, $"{what} at {u}: the point is not the point of that position");

                GeoVector numeric1 = (1.0 / (2.0 * h)) * (curve.PointAt(u + h) - curve.PointAt(u - h));
                GeoVector numeric2 = (1.0 / (2.0 * h)) * (curve.DirectionAt(u + h) - curve.DirectionAt(u - h));
                Close($"{what} at {u}: first derivative", numeric1, deriv1);
                Close($"{what} at {u}: second derivative", numeric2, deriv2);
            }
        }

        private static void Close(string what, GeoVector2D expected, GeoVector2D actual)
        {
            double error = (expected - actual).Length / Math.Max(1.0, expected.Length);
            Assert.IsTrue(error < tolerance,
                $"{what}: expected about ({expected.x}, {expected.y}), got ({actual.x}, {actual.y}), relative {error:E3}");
        }

        private static void Close(string what, GeoVector expected, GeoVector actual)
        {
            double error = (expected - actual).Length / Math.Max(1.0, expected.Length);
            Assert.IsTrue(error < tolerance, $"{what}: expected about {expected}, got {actual}, relative {error:E3}");
        }

        [TestMethod]
        public void arc2d_derivatives_follow_the_sweep_in_both_directions()
        {
            AssertDerivatives("arc with a positive sweep",
                new Arc2D(new GeoPoint2D(0, 0), 10, new Angle(0.3), new SweepAngle(Math.PI / 2)));
            // This one used to report the point mirrored at the x axis, 5.55 away from PointAt(u).
            AssertDerivatives("arc with a negative sweep",
                new Arc2D(new GeoPoint2D(0, 0), 10, new Angle(0.3), new SweepAngle(-Math.PI / 2)));
        }

        [TestMethod]
        public void bspline2d_derivatives_are_by_the_position_not_by_the_knot()
        {
            AssertDerivatives("non rational 2d spline, knots 0 to 1", PolynomialSpline2D(0, 1));
            // Knot range 3, so the first derivative used to be 3 times and the second 9 times too small.
            AssertDerivatives("non rational 2d spline, knots 2 to 5", PolynomialSpline2D(2, 5));
        }

        [TestMethod]
        public void rational_bspline2d_derivatives_carry_the_weight_correction()
        {
            // Knot range 1, so the chain rule factor is 1 and what is left is the rational correction alone.
            AssertDerivatives("rational 2d circle, knots 0 to 1", RationalCircle2D(30, 0, 1));
            AssertDerivatives("rational 2d circle, knots 0 to 4", RationalCircle2D(30, 0, 4));
        }

        [TestMethod]
        public void bspline3d_derivatives_are_by_the_position_too()
        {
            AssertDerivatives("rational 3d circle in the xy plane, knots 0 to 4", (ICurve)RationalCircle3D(30));
            AssertDerivatives("non planar helix", (ICurve)Helix());
        }

        [TestMethod]
        public void ellipse_derivatives_carry_the_sweep_twice()
        {
            // The second derivative used to be scaled with the sweep only once, not squared.
            Plane plane = new Plane(new GeoPoint(3, -2, 7), new GeoVector(0.3, -1.7, 0.5), new GeoVector(1, 0.2, -0.4));
            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(plane, new GeoPoint(3, -2, 7), 3.0, 0.4, 2.5);
            AssertDerivatives("circular arc, sweep 2.5", (ICurve)arc);
            Ellipse reversed = Ellipse.Construct();
            reversed.SetArcPlaneCenterRadiusAngles(plane, new GeoPoint(3, -2, 7), 3.0, 2.9, -2.5);
            AssertDerivatives("circular arc, sweep -2.5", (ICurve)reversed);
            Ellipse ellipse = Ellipse.Construct();
            ellipse.SetEllipseCenterAxis(new GeoPoint(3, -2, 7), 7.0 * plane.DirectionX, 2.0 * plane.DirectionY);
            AssertDerivatives("full ellipse", (ICurve)ellipse);
        }

        [TestMethod]
        public void line_derivatives()
        {
            AssertDerivatives("line", (ICurve)Line.TwoPoints(new GeoPoint(3, -2, 7), new GeoPoint(-4, 5, 1)));
        }

        [TestMethod]
        public void the_second_derivative_works_on_a_spline_nobody_has_touched_yet()
        {
            // No warm up call on purpose. The nurbs helper is built lazily and this method used to read the
            // fields straight, so a fresh spline threw a NullReferenceException while the same spline worked
            // as soon as anything had called PointAt on it.
            ICurve fresh = (ICurve)RationalCircle3D(30);
            Assert.IsTrue(fresh.TryPointDeriv2At(0.3, out GeoPoint point, out GeoVector _, out GeoVector _2),
                "a rational planar spline has to deliver its second derivative");
            Assert.IsTrue((point | fresh.PointAt(0.3)) < 1e-9, "and at the right place");
        }

        [TestMethod]
        public void the_result_does_not_depend_on_what_was_called_before()
        {
            ICurve fresh = (ICurve)RationalCircle3D(30);
            ICurve warm = (ICurve)RationalCircle3D(30);
            GeoPoint _ = warm.PointAt(0.5);

            fresh.TryPointDeriv2At(0.3, out GeoPoint p1, out GeoVector d1, out GeoVector dd1);
            warm.TryPointDeriv2At(0.3, out GeoPoint p2, out GeoVector d2, out GeoVector dd2);
            Assert.IsTrue((p1 | p2) < 1e-12, "point");
            Assert.IsTrue((d1 - d2).Length < 1e-12, "first derivative");
            Assert.IsTrue((dd1 - dd2).Length < 1e-12, "second derivative");
        }
    }
}
