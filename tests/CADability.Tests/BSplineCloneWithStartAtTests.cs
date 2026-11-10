using CADability.GeoObject;
using System;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="BSpline.CloneWithStartAt(double)"/> (formerly the internal SetCyclicalStartPoint, made public for
    /// importers that build CADability models from other kernels, see PR 537). The copy must run through the same
    /// points in the same direction, starting at the given position, for polynomial and rational splines, and leave
    /// the original unchanged.
    /// </summary>
    [TestClass]
    public class BSplineCloneWithStartAtTests
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
        /// The exact rational quadratic circle of radius 50 around (10, 20, 0) with nine poles, starting and ending at
        /// (60, 20, 0).
        /// </summary>
        private static BSpline RationalCircle()
        {
            double w = Math.Sqrt(0.5);
            GeoPoint c = new GeoPoint(10, 20, 0);
            GeoPoint[] poles = {
                c + new GeoVector(50, 0, 0), c + new GeoVector(50, 50, 0), c + new GeoVector(0, 50, 0),
                c + new GeoVector(-50, 50, 0), c + new GeoVector(-50, 0, 0), c + new GeoVector(-50, -50, 0),
                c + new GeoVector(0, -50, 0), c + new GeoVector(50, -50, 0), c + new GeoVector(50, 0, 0) };
            double[] weights = { 1, w, 1, w, 1, w, 1, w, 1 };
            double[] knots = { 0, 1, 2, 3, 4 };
            int[] multiplicities = { 3, 2, 2, 2, 3 };
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.SetData(2, poles, weights, knots, multiplicities, false));
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
        [DataRow(0.5)]
        [DataRow(0.77)]
        public void ClosedCubicStartsAtPosition(double position)
        {
            BSpline original = ClosedCubic();
            BSpline copy = original.CloneWithStartAt(position);
            ICurve o = original, c = copy;

            Assert.IsTrue((c.StartPoint | o.PointAt(position)) < 1e-8);
            Assert.IsTrue((c.EndPoint | c.StartPoint) < 1e-8);
            AssertRotated(o, c, position);
            Assert.AreEqual(o.Length, c.Length, 1e-6);
            // the original is not changed
            Assert.IsTrue((o.StartPoint | new GeoPoint(100, 0, 0)) < 1e-12);
        }

        [DataTestMethod]
        [DataRow(0.125)]
        [DataRow(0.6)]
        public void RationalCircleStaysCircle(double position)
        {
            BSpline original = RationalCircle();
            BSpline copy = original.CloneWithStartAt(position);
            ICurve c = copy;

            AssertRotated(original, c, position);
            GeoPoint center = new GeoPoint(10, 20, 0);
            for (int i = 0; i <= 100; i++)
            {
                Assert.AreEqual(50.0, c.PointAt(i / 100.0) | center, 1e-8);
            }
        }

        [DataTestMethod]
        [DataRow(0.0)]
        [DataRow(1.0)]
        public void StartOrEndReturnsUnchangedCopy(double position)
        {
            BSpline original = ClosedCubic();
            BSpline copy = original.CloneWithStartAt(position);

            Assert.AreNotSame(original, copy);
            AssertRotated(original, copy, 0.0);
        }

        [TestMethod]
        public void OpenSplineIsRejected()
        {
            BSpline open = BSpline.Construct();
            Assert.IsTrue(open.ThroughPoints(new[] { new GeoPoint(0, 0, 0), new GeoPoint(10, 5, 0), new GeoPoint(20, 0, 0), new GeoPoint(30, 8, 0) }, 3, false));

            Assert.ThrowsException<InvalidOperationException>(() => open.CloneWithStartAt(0.5));
        }

        [DataTestMethod]
        [DataRow(-0.1)]
        [DataRow(1.1)]
        [DataRow(double.NaN)]
        public void PositionOutsideZeroToOneIsRejected(double position)
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ClosedCubic().CloneWithStartAt(position));
        }

        /// <summary>
        /// Periodic splines used to come out wrong: start and end point were right, the curve in between was not,
        /// because splitting a periodic spline yields parts that do not fit together. ImportStep makes closed STEP
        /// splines periodic and then moved their start point this way.
        /// </summary>
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

            BSpline copy = periodic.CloneWithStartAt(position);

            AssertRotated(periodic, copy, position);
            Assert.IsFalse(copy.IsClosed);
        }

        /// <summary>The way ImportStep closes a spline: the clamped spline is turned periodic by <see cref="BSpline.IsClosed"/>.</summary>
        [TestMethod]
        public void SplineMadePeriodicByIsClosed()
        {
            BSpline periodic = ClosedCubic();
            periodic.IsClosed = true;
            Assert.IsTrue(periodic.IsClosed);

            AssertRotated(periodic, periodic.CloneWithStartAt(0.35), 0.35);
        }
    }
}
