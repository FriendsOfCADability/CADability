using CADability.GeoObject;
using System.Reflection;

namespace CADability.Tests
{
    // Validation of the exact (quadratic formula based) extrema computation in BSpline.GetIntervalExtent
    // for non rational curves of degree <= 3, which replaces the TetraederHull bisection on that path.
    // GetIntervalExtent is internal (CADability is strong named, no InternalsVisibleTo), so it is invoked
    // via reflection here.
    [TestClass]
    public class BSplineIntervalExtentTests
    {
        private static BoundingBox IntervalExtent(BSpline bsp, double pmin, double pmax)
        {
            MethodInfo mi = typeof(BSpline).GetMethod("GetIntervalExtent", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "BSpline.GetIntervalExtent not found");
            return (BoundingBox)mi.Invoke(bsp, new object[] { pmin, pmax });
        }

        /// <summary>
        /// The interval extent must contain a dense sampling of the curve section (guarantee) and must not
        /// exceed the sample box by more than tightness*diagonal (exactness).
        /// </summary>
        private static void CheckInterval(BSpline bsp, double pmin, double pmax, double tightness, string name)
        {
            BoundingBox box = IntervalExtent(bsp, pmin, pmax);
            BoundingBox sampleBox = BoundingBox.EmptyBoundingBox;
            const int samples = 4000;
            for (int i = 0; i <= samples; ++i)
            {
                sampleBox.MinMax(bsp.PointAtParam(pmin + i * (pmax - pmin) / samples));
            }
            double eps = Math.Max(sampleBox.DiagonalLength, 1.0) * 1e-9;
            BoundingBox boxExp = box;
            boxExp.Expand(eps);
            Assert.IsTrue(boxExp.Contains(sampleBox),
                name + ": interval extent misses curve points: " + box + " vs samples " + sampleBox);
            BoundingBox sampleExp = sampleBox;
            sampleExp.Expand(Math.Max(sampleBox.DiagonalLength, 1.0) * tightness);
            Assert.IsTrue(sampleExp.Contains(box),
                name + ": interval extent too loose: " + box + " vs samples " + sampleBox);
        }

        [TestMethod]
        public void interval_extent_is_exact_for_cubic_spline()
        {
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.ThroughPoints(new GeoPoint[]
            {
                new GeoPoint(0, 0, 0),
                new GeoPoint(1, 2, 1),
                new GeoPoint(2, -1, -2),
                new GeoPoint(3, 3, 1),
                new GeoPoint(4, 0, 0),
                new GeoPoint(5, -2, 2),
            }, 3, false));
            Assert.AreEqual(3, bsp.Degree);
            double[] knots = bsp.Knots;
            double kmin = knots[0], kmax = knots[knots.Length - 1];
            CheckInterval(bsp, kmin, kmax, 1e-5, "cubic full");
            // partial spans crossing inner knots
            CheckInterval(bsp, kmin + 0.15 * (kmax - kmin), kmin + 0.85 * (kmax - kmin), 1e-5, "cubic partial");
            // a section inside a single span
            CheckInterval(bsp, knots[1], 0.5 * (knots[1] + knots[2]), 1e-5, "cubic single span part");
        }

        [TestMethod]
        public void interval_extent_is_exact_for_planar_cubic_spline()
        {
            // all points in the plane z == 0: the BSpline may use its 2d representation internally
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.ThroughPoints(new GeoPoint[]
            {
                new GeoPoint(0, 0, 0),
                new GeoPoint(1, 3, 0),
                new GeoPoint(2, -2, 0),
                new GeoPoint(3, 1, 0),
                new GeoPoint(4, -1, 0),
            }, 3, false));
            double[] knots = bsp.Knots;
            double kmin = knots[0], kmax = knots[knots.Length - 1];
            CheckInterval(bsp, kmin, kmax, 1e-5, "planar full");
            CheckInterval(bsp, kmin + 0.2 * (kmax - kmin), kmin + 0.7 * (kmax - kmin), 1e-5, "planar partial");
        }

        [TestMethod]
        public void interval_extent_is_exact_for_quadratic_spline()
        {
            // degree 2 exercises the linear derivative branch
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.ThroughPoints(new GeoPoint[]
            {
                new GeoPoint(0, 0, 0),
                new GeoPoint(1, 2, -1),
                new GeoPoint(2, -1, 1),
                new GeoPoint(3, 1, 2),
            }, 2, false));
            Assert.AreEqual(2, bsp.Degree);
            double[] knots = bsp.Knots;
            CheckInterval(bsp, knots[0], knots[knots.Length - 1], 1e-5, "quadratic full");
        }

        [TestMethod]
        public void interval_extent_still_works_for_higher_degree()
        {
            // degree 4 takes the old TetraederHull path, which is approximate: only containment and a
            // loose tightness are required here
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.ThroughPoints(new GeoPoint[]
            {
                new GeoPoint(0, 0, 0),
                new GeoPoint(1, 2, 1),
                new GeoPoint(2, -1, -2),
                new GeoPoint(3, 3, 1),
                new GeoPoint(4, 0, 0),
                new GeoPoint(5, -2, 2),
                new GeoPoint(6, 1, -1),
            }, 4, false));
            Assert.AreEqual(4, bsp.Degree);
            double[] knots = bsp.Knots;
            CheckInterval(bsp, knots[0], knots[knots.Length - 1], 1e-3, "quartic full");
        }
    }
}
