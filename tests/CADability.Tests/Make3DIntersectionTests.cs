using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #424: <see cref="Make3D.Intersection(Solid, Solid)"/> threw <c>NotImplementedException</c> although
    /// <see cref="Solid.Intersect"/> works and <see cref="Make3D.Union(Solid, Solid)"/> and
    /// <see cref="Make3D.Difference(Solid, Solid)"/> delegate to <see cref="Solid"/>.
    /// </summary>
    [TestClass]
    public class Make3DIntersectionTests
    {
        [TestMethod]
        public void IntersectionOfOverlappingBoxes()
        {
            Solid a = Make3D.MakeBox(GeoPoint.Origin, 10.0 * GeoVector.XAxis, 10.0 * GeoVector.YAxis, 10.0 * GeoVector.ZAxis);
            Solid b = Make3D.MakeBox(new GeoPoint(5, 5, 5), 10.0 * GeoVector.XAxis, 10.0 * GeoVector.YAxis, 10.0 * GeoVector.ZAxis);

            Solid[] common = Make3D.Intersection(a, b);
            Assert.AreEqual(1, common.Length);
            Assert.AreEqual(125.0, common[0].Volume(1e-6), 1e-6, "the common part is the 5x5x5 cube");
        }

        [TestMethod]
        public void IntersectionOfDisjunctBoxesIsEmpty()
        {
            Solid a = Make3D.MakeBox(GeoPoint.Origin, 10.0 * GeoVector.XAxis, 10.0 * GeoVector.YAxis, 10.0 * GeoVector.ZAxis);
            Solid b = Make3D.MakeBox(new GeoPoint(20, 0, 0), 10.0 * GeoVector.XAxis, 10.0 * GeoVector.YAxis, 10.0 * GeoVector.ZAxis);

            Solid[] common = Make3D.Intersection(a, b);
            Assert.IsNotNull(common);
            Assert.AreEqual(0, common.Length);
        }
    }
}
