using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="BSpline.SetCyclicalStartPoint(double)"/> moves the start of a closed spline, ImportStep uses it for edges
    /// that start somewhere along a closed curve. For periodic splines the result used to have the right start and end
    /// point but a wrong curve in between, because splitting a periodic spline yields parts that do not fit together.
    /// ImportStep makes closed STEP splines periodic (<see cref="BSpline.IsClosed"/>) before it moves their start.
    /// </summary>
    [TestClass]
    public class BSplineCyclicalStartPointTests
    {
        /// <summary>
        /// A closed, clamped cubic spline around a square of side 200 centred at the origin, starting and ending at
        /// (100, 0, 0).
        /// </summary>
        private static BSpline ClosedCubic()
        {
            GeoPoint[] poles = {
                new GeoPoint(100, 0, 0), new GeoPoint(100, 100, 0), new GeoPoint(0, 100, 0), new GeoPoint(-100, 100, 0),
                new GeoPoint(-100, 0, 0), new GeoPoint(-100, -100, 0), new GeoPoint(0, -100, 0), new GeoPoint(100, -100, 0),
                new GeoPoint(100, 0, 0) };
            double[] knots = { 0, 1, 2, 3, 4, 5, 6 };
            int[] multiplicities = { 4, 1, 1, 1, 1, 1, 4 };
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.SetData(3, poles, null, knots, multiplicities, false));
            return bsp;
        }

        /// <summary>
        /// Position t of the copy must be position (start + t) mod 1 of the original: the same points in the same
        /// direction, only shifted.
        /// </summary>
        private static void AssertRotated(ICurve original, ICurve copy, double start)
        {
            for (int i = 0; i <= 200; i++)
            {
                double t = i / 200.0;
                double onOriginal = start + t;
                if (onOriginal > 1.0) onOriginal -= 1.0;
                GeoPoint expected = original.PointAt(onOriginal);
                GeoPoint actual = copy.PointAt(t);
                Assert.IsTrue((expected | actual) < 1e-8, $"position {t}: expected {expected}, got {actual}");
            }
        }

        [DataTestMethod]
        [DataRow(0.3)]
        [DataRow(0.77)]
        public void ClampedSpline(double position)
        {
            BSpline original = ClosedCubic();

            AssertRotated(original, original.SetCyclicalStartPoint(position), position);
        }

        [DataTestMethod]
        [DataRow(4, 0.4)]
        [DataRow(4, 0.9)]
        [DataRow(6, 0.4)]
        public void PeriodicSplineThroughPoints(int pointCount, double position)
        {
            GeoPoint[] points = pointCount == 4
                ? new[] { new GeoPoint(0, 0, 0), new GeoPoint(10, 5, 0), new GeoPoint(20, 0, 0), new GeoPoint(10, -8, 0) }
                : new[] { new GeoPoint(0, 0, 0), new GeoPoint(10, 5, 0), new GeoPoint(20, 0, 0), new GeoPoint(25, -6, 3),
                          new GeoPoint(10, -8, 0), new GeoPoint(2, -4, -1) };
            BSpline periodic = BSpline.Construct();
            Assert.IsTrue(periodic.ThroughPoints(points, 3, true));
            Assert.IsTrue(periodic.IsClosed);

            AssertRotated(periodic, periodic.SetCyclicalStartPoint(position), position);
        }

        /// <summary>The way ImportStep closes a spline: the clamped spline is turned periodic by <see cref="BSpline.IsClosed"/>.</summary>
        [TestMethod]
        public void SplineMadePeriodicByIsClosed()
        {
            BSpline periodic = ClosedCubic();
            periodic.IsClosed = true;
            Assert.IsTrue(periodic.IsClosed);

            AssertRotated(periodic, periodic.SetCyclicalStartPoint(0.35), 0.35);
        }
    }
}
