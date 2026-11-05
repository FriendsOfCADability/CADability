using CADability.GeoObject;
using System.Linq;

namespace CADability.Tests
{
    /// <summary>
    /// Boolean operations of a big box with a tiny tool, which sits on the middle of the top face, half inside. The faces of
    /// <see cref="Make3D.MakeBox"/> have u/v systems scaled by the edge lengths. <c>PlaneSurface.GetPlaneIntersection</c>
    /// mapped the other plane into the u/v system of the first one, which needed the inverse of a ModOp whose determinant is
    /// the product of the scales of both planes; for a box of 10000 and a box of 0.0001 it fell below the limit of
    /// <see cref="ModOp.GetInverse"/>, and the operations threw a <see cref="ModOpException"/>. The volume of a tiny result
    /// far from the origin was noise as well, because <see cref="Shell.Volume"/> spanned its tetrahedra from the origin.
    /// </summary>
    [TestClass]
    public class BooleanSizeRatioTests
    {
        [TestMethod]
        [DataRow(10000.0, 0.0001)]
        [DataRow(100000.0, 0.00001)]
        [DataRow(1000.0, 0.01)]
        public void TinyBoxOnABigBox(double size, double toolSize)
        {
            Solid Big() => Make3D.MakeBox(GeoPoint.Origin, size * GeoVector.XAxis, size * GeoVector.YAxis, size * GeoVector.ZAxis);
            Solid Tool() => Make3D.MakeBox(new GeoPoint(size / 2 - toolSize / 2, size / 2 - toolSize / 2, size - toolSize / 2),
                toolSize * GeoVector.XAxis, toolSize * GeoVector.YAxis, toolSize * GeoVector.ZAxis);
            double big = size * size * size, tool = toolSize * toolSize * toolSize, inside = tool / 2;
            double precision = toolSize * 1e-4;
            // the faces of the tool lie at coordinates of about size, which a double resolves to about 1e-16 * size: the volume of
            // the tool sized results cannot be more exact than that shift of its faces
            double toolTolerance = 1e-6 * tool + 6 * toolSize * toolSize * 1e-15 * size;

            Solid[] difference = Solid.Subtract(Big(), Tool());
            Assert.AreEqual(1, difference.Length, "big - tool");
            Assert.AreEqual(big - inside, difference[0].Volume(precision), 1e-7 * big, "volume of big - tool");
            Assert.AreEqual(11, difference[0].Shells[0].Faces.Length, "faces of big - tool"); // the pocket must be there

            Solid[] toolOutside = Solid.Subtract(Tool(), Big());
            Assert.AreEqual(1, toolOutside.Length, "tool - big");
            Assert.AreEqual(tool - inside, toolOutside[0].Volume(precision), toolTolerance, "volume of tool - big");

            Solid[] intersection = Solid.Intersect(Big(), Tool());
            Assert.AreEqual(1, intersection.Length, "big * tool");
            Assert.AreEqual(inside, intersection[0].Volume(precision), toolTolerance, "volume of big * tool");

            Solid union = Solid.Unite(Big(), Tool());
            Assert.IsNotNull(union, "big + tool");
            Assert.AreEqual(big + tool - inside, union.Volume(precision), 1e-7 * big, "volume of big + tool");
            // the tiny part on top must really be there: the union has the faces of the box and of the part of the tool outside
            Assert.AreEqual(11, union.Shells[0].Faces.Length, "faces of big + tool");
        }
    }
}
