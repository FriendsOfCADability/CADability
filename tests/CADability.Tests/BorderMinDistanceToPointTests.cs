using CADability.Curve2D;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="Border.GetMinDistance(GeoPoint2D)"/>, made public for importers that build CADability models from
    /// other kernels (PR 537): the distance from a point to the outline, the same inside and outside.
    /// </summary>
    [TestClass]
    public class BorderMinDistanceToPointTests
    {
        [DataTestMethod]
        [DataRow(5.0, 5.0, 5.0)]    // centre, inside
        [DataRow(2.0, 5.0, 2.0)]    // inside, closest to the left side
        [DataRow(15.0, 5.0, 5.0)]   // outside, right of the square
        [DataRow(13.0, 14.0, 5.0)]  // outside, closest to the corner (10, 10)
        [DataRow(5.0, 0.0, 0.0)]    // on the border
        public void DistanceToSquare(double x, double y, double expected)
        {
            Border square = Border.MakeRectangle(0, 10, 0, 10);

            Assert.AreEqual(expected, square.GetMinDistance(new GeoPoint2D(x, y)), 1e-10);
        }

        [TestMethod]
        public void DistanceToCircle()
        {
            Border circle = Border.MakeCircle(new GeoPoint2D(1, 2), 3);

            Assert.AreEqual(3.0, circle.GetMinDistance(new GeoPoint2D(1, 2)), 1e-10);
            Assert.AreEqual(4.0, circle.GetMinDistance(new GeoPoint2D(1, 9)), 1e-10);
        }
    }
}
