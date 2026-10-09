using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System.Runtime.CompilerServices;

namespace CADability.Tests
{
    /// <summary>
    /// A border that consists of a single closed curve (e.g. a circle) is split into two halves when a
    /// <see cref="SimpleShape"/> is built, and the original curve is kept in <see cref="Border.UnsplittedOutline"/>.
    /// <see cref="CompoundShape.CreateFromList"/> reduces the shapes before it returns them, which must not lose that curve
    /// (issue #194).
    /// </summary>
    [TestClass]
    public class CompoundShapeUnsplittedOutlineTests
    {
        private static string DxfFile(string name, [CallerFilePath] string thisFile = "")
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile), "Files", "Dxf", name);

        private static Ellipse MakeCircle(double x, double y, double radius)
        {
            Ellipse circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(Plane.XYPlane, new GeoPoint(x, y, 0.0), radius);
            return circle;
        }

        private static void AssertIsCircle(ICurve2D outline, GeoPoint2D center, double radius, string what)
        {
            Assert.IsNotNull(outline, what + ": UnsplittedOutline must not be null");
            Circle2D circle = outline as Circle2D;
            Assert.IsNotNull(circle, what + ": UnsplittedOutline must be a circle, but is " + outline.GetType().Name);
            Assert.IsTrue(circle.IsClosed, what + ": UnsplittedOutline must be a full circle");
            Assert.AreEqual(center.x, circle.Center.x, 1e-6, what + ": center x");
            Assert.AreEqual(center.y, circle.Center.y, 1e-6, what + ": center y");
            Assert.AreEqual(radius, circle.Radius, 1e-6, what + ": radius");
        }

        /// <summary>
        /// Checks that <paramref name="shape"/> has a hole around the projection of <paramref name="center"/> whose
        /// UnsplittedOutline is the circle with <paramref name="radius"/>.
        /// </summary>
        private static void AssertHasCircularHole(SimpleShape shape, Plane plane, GeoPoint center, double radius)
        {
            GeoPoint2D center2d = plane.Project(center);
            Border hole = shape.Holes.OrderBy(h => h.Extent.GetCenter() | center2d).First();
            AssertIsCircle(hole.UnsplittedOutline, center2d, radius, "hole at " + center.ToString());
        }

        [TestMethod]
        public void CreateFromList_KeepsUnsplittedOutlineOfCircularHoles_Issue194Dxf()
        {
            // the file of the reporter: a slot made of two lines and two arcs with two circular holes
            string file = DxfFile("issue194.dxf");
            Assert.IsTrue(System.IO.File.Exists(file));
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            GeoObjectList objects = project.GetActiveModel().AllObjects;

            CompoundShape cs = CompoundShape.CreateFromList(objects, 0.1, out Plane plane, true);
            Assert.IsNotNull(cs);
            SimpleShape ss = Assert.That.Single(cs.SimpleShapes);
            Assert.AreEqual(2, ss.Holes.Length);
            AssertHasCircularHole(ss, plane, new GeoPoint(2.3125, 1.5, 0.0), 0.2655);
            AssertHasCircularHole(ss, plane, new GeoPoint(6.3125, 1.5, 0.0), 0.2655);
            // the slot consists of several curves, it never was a single curve
            Assert.IsNull(ss.Outline.UnsplittedOutline);
        }

        [TestMethod]
        public void CreateFromList_KeepsUnsplittedOutlineOfCircularHoles()
        {
            Polyline rect = Polyline.Construct();
            rect.SetPoints(new[] { new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0), new GeoPoint(100, 50, 0), new GeoPoint(0, 50, 0) }, true);
            GeoObjectList objects = new GeoObjectList(rect, MakeCircle(25, 25, 10), MakeCircle(75, 25, 8));

            CompoundShape cs = CompoundShape.CreateFromList(objects, 0.001, out Plane plane, false);
            Assert.IsNotNull(cs);
            SimpleShape ss = Assert.That.Single(cs.SimpleShapes);
            Assert.AreEqual(2, ss.Holes.Length);
            AssertHasCircularHole(ss, plane, new GeoPoint(25, 25, 0), 10);
            AssertHasCircularHole(ss, plane, new GeoPoint(75, 25, 0), 8);
        }

        [TestMethod]
        public void CreateFromList_KeepsUnsplittedOutlineOfCircularOutlineAndHole()
        {
            GeoObjectList objects = new GeoObjectList(MakeCircle(300, 0, 40), MakeCircle(300, 0, 10));

            CompoundShape cs = CompoundShape.CreateFromList(objects, 0.001, out Plane plane, false);
            Assert.IsNotNull(cs);
            SimpleShape ss = Assert.That.Single(cs.SimpleShapes);
            AssertIsCircle(ss.Outline.UnsplittedOutline, plane.Project(new GeoPoint(300, 0, 0)), 40, "outline");
            Assert.AreEqual(1, ss.Holes.Length);
            AssertHasCircularHole(ss, plane, new GeoPoint(300, 0, 0), 10);
        }
    }
}
