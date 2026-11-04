using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for the 2d projection a <see cref="GeneralCurve"/> returns from <see cref="GeneralCurve.GetProjectedCurve(Plane)"/>:
    /// it must not share its 3d curve with the curve it was made from, otherwise reversing the projection reverses the
    /// original curve as well (ported from ShapeIt 73d64a9).
    /// </summary>
    [TestClass]
    public class GeneralCurveProjectionTests
    {
        private static HelicalCurve MakeTestHelix()
        {
            // a helix around the z-axis: radius 10, pitch 5, one and a quarter turns, starting at (10,0,0)
            return HelicalCurve.FromAxisStartPoint(GeoPoint.Origin, GeoVector.ZAxis, new GeoPoint(10, 0, 0), 5.0, 1.25);
        }

        [TestMethod]
        public void ReversingTheProjectionKeepsTheOriginalCurve()
        {
            HelicalCurve helix = MakeTestHelix();
            GeoPoint start = helix.StartPoint;
            GeoPoint end = helix.EndPoint;
            ICurve2D projected = helix.GetProjectedCurve(Plane.XYPlane);

            projected.Reverse();

            Assert.AreEqual(0.0, helix.StartPoint | start, 1e-12, "the 3d curve was reversed with its projection");
            Assert.AreEqual(0.0, helix.EndPoint | end, 1e-12, "the 3d curve was reversed with its projection");
            Assert.AreEqual(0.0, projected.StartPoint | Plane.XYPlane.Project(end), 1e-9);
            Assert.AreEqual(0.0, projected.EndPoint | Plane.XYPlane.Project(start), 1e-9);
        }

        [TestMethod]
        public void ReversingTheOriginalCurveKeepsTheProjection()
        {
            HelicalCurve helix = MakeTestHelix();
            ICurve2D projected = helix.GetProjectedCurve(Plane.XYPlane);
            GeoPoint2D start2d = projected.StartPoint;

            helix.Reverse();

            Assert.AreEqual(0.0, projected.StartPoint | start2d, 1e-12, "the projection followed a later change of the 3d curve");
        }

        [TestMethod]
        public void ReversingACloneKeepsTheProjection()
        {
            HelicalCurve helix = MakeTestHelix();
            ICurve2D projected = helix.GetProjectedCurve(Plane.XYPlane);
            GeoPoint2D start2d = projected.StartPoint;

            ICurve2D clone = projected.Clone();
            clone.Reverse();
            ICurve2D copy = projected.Clone();
            copy.Copy(projected);
            copy.Reverse();

            Assert.AreEqual(0.0, projected.StartPoint | start2d, 1e-12, "a clone shares the 3d curve with the projection");
            Assert.AreEqual(0.0, clone.EndPoint | start2d, 1e-9);
            Assert.AreEqual(0.0, copy.EndPoint | start2d, 1e-9);
        }
    }
}
