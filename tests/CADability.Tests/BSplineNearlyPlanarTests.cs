using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// A spline whose poles lie within <see cref="Precision.eps"/> of a plane, but not in it, counts as planar. It was
    /// evaluated as a 2d spline in that plane, which moves every point by up to that distance, while the start and end
    /// point are the unprojected end poles: the curve jumped at its ends (ported from ShapeIt 7d0e769).
    /// </summary>
    [TestClass]
    public class BSplineNearlyPlanarTests
    {
        private static BSpline NearlyPlanarSpline(double offset)
        {
            // in the plane z == 100, apart from the offsets of the inner poles, all of them below Precision.eps
            GeoPoint[] poles = {
                new GeoPoint(0, 0, 100 + 0.8 * offset), new GeoPoint(10, 5, 100 - offset), new GeoPoint(20, -5, 100 + offset),
                new GeoPoint(30, 2, 100 - 0.5 * offset), new GeoPoint(40, 0, 100 + 0.9 * offset) };
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.SetData(3, poles, null, new double[] { 0, 1, 2 }, new int[] { 4, 1, 4 }, false));
            return bsp;
        }

        [TestMethod]
        public void NearlyPlanarSplineIsContinuousAtItsEnds()
        {
            BSpline bsp = NearlyPlanarSpline(0.4 * Precision.eps);
            ICurve curve = bsp;
            Assert.AreEqual(PlanarState.Planar, curve.GetPlanarState());
            GeoPoint startPole = bsp.Poles[0];
            GeoPoint endPole = bsp.Poles[bsp.Poles.Length - 1];
            Assert.AreEqual(0.0, curve.PointAt(0.0) | startPole, 1e-12, "the curve does not start at its first pole");
            Assert.AreEqual(0.0, curve.PointAt(1.0) | endPole, 1e-12, "the curve does not end at its last pole");
            Assert.AreEqual(0.0, curve.PointAt(1e-12) | startPole, 1e-9, "the curve jumps at its start");
            Assert.AreEqual(0.0, curve.PointAt(1.0 - 1e-12) | endPole, 1e-9, "the curve jumps at its end");
            Assert.AreEqual(0.0, curve.StartPoint | curve.PointAt(0.0), 1e-12);
            Assert.AreEqual(0.0, curve.EndPoint | curve.PointAt(1.0), 1e-12);
        }

        [TestMethod]
        public void PlanarSplineStaysInItsPlane()
        {
            BSpline bsp = NearlyPlanarSpline(0.0);
            ICurve curve = bsp;
            for (int i = 0; i <= 100; i++)
            {
                Assert.AreEqual(100.0, curve.PointAt(i / 100.0).z, 1e-12);
            }
        }
    }
}
