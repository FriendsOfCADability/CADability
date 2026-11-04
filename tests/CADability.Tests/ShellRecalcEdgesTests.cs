using CADability.GeoObject;
using System.Linq;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="Shell.RecalcEdges"/> moves the ends of the edge curves onto their vertices. A curve that is a
    /// <see cref="GeneralCurve"/>, like the interpolated curve of a <see cref="RuledSurface"/>, cannot move its ends and
    /// threw, which failed the whole operation, e.g. an offset of the shell (ported from ShapeIt 9173439).
    /// </summary>
    [TestClass]
    public class ShellRecalcEdgesTests
    {
        [TestMethod]
        public void EdgeCurveWithFixedEndsIsKept()
        {
            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, GeoPoint.Origin, 10.0, 0.0, Math.PI / 2.0);
            ICurve line = Line.TwoPoints(new GeoPoint(30, 0, 10), new GeoPoint(0, 5, 10));
            Face face = Face.MakeFace(new RuledSurface(arc, line), new BoundingRect(0, 0, 1, 1));
            Shell shell = Shell.MakeShell(new Face[] { face });

            Edge edge = shell.Edges.FirstOrDefault(e => e.Curve3D is GeneralCurve);
            Assert.IsNotNull(edge, "the ruled surface should have an interpolated edge curve");
            ICurve curve = edge.Curve3D;
            GeoPoint start = curve.StartPoint;
            edge.Vertex1.Position = start + new GeoVector(2e-6, 0, 0); // a vertex slightly off the curve, as after an intersection

            shell.RecalcEdges();

            Assert.AreSame(curve, edge.Curve3D);
            Assert.AreEqual(0.0, edge.Curve3D.StartPoint | start, 1e-12);
        }
    }
}
