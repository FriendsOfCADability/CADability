using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="GeneralCurve.DistanceTo(GeoPoint)"/> with points beyond the ends of the curve: it returned the
    /// distance to the extended curve, so a point on the extension counted as a point on the curve (ported from ShapeIt
    /// aa4464c). A <see cref="Line"/> returns the distance to the closest end there, the general curve does the same now.
    /// </summary>
    [TestClass]
    public class GeneralCurveDistanceTests
    {
        private static HelicalCurve MakeTestHelix()
        {
            // radius 10, pitch 5, one turn, from (10,0,0) to (10,0,5)
            return HelicalCurve.FromAxisStartPoint(GeoPoint.Origin, GeoVector.ZAxis, new GeoPoint(10, 0, 0), 5.0, 1.0);
        }

        [TestMethod]
        public void PointOnTheExtensionBeforeTheStart()
        {
            HelicalCurve helix = MakeTestHelix();
            GeoPoint p = helix.PointAt(-0.2); // a fifth of a turn before the start, on the extended helix
            Assert.IsTrue((helix as ICurve).PositionOf(p) < 0.0, "the foot point should lie before the start");
            Assert.AreEqual(p | helix.StartPoint, helix.DistanceTo(p), 1e-9);
        }

        [TestMethod]
        public void PointOnTheExtensionBeyondTheEnd()
        {
            HelicalCurve helix = MakeTestHelix();
            GeoPoint p = helix.PointAt(1.15);
            Assert.IsTrue((helix as ICurve).PositionOf(p) > 1.0, "the foot point should lie beyond the end");
            Assert.AreEqual(p | helix.EndPoint, helix.DistanceTo(p), 1e-9);
        }

        [TestMethod]
        public void PointsOnAndNearTheCurve()
        {
            HelicalCurve helix = MakeTestHelix();
            Assert.AreEqual(0.0, helix.DistanceTo(helix.PointAt(0.37)), 1e-9);
            Assert.AreEqual(0.0, helix.DistanceTo(helix.StartPoint), 1e-9);
            Assert.AreEqual(0.0, helix.DistanceTo(helix.EndPoint), 1e-9);
            // a point 1 outside the cylinder of the helix, radially above a point of the curve
            GeoPoint onCurve = helix.PointAt(0.6);
            GeoPoint outside = onCurve + new GeoVector(onCurve.x, onCurve.y, 0).Normalized;
            Assert.AreEqual(1.0, helix.DistanceTo(outside), 1e-6);
        }
    }
}
