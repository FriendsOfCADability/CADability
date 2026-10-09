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

        /// <summary>
        /// A border made of the circle, split into two halves, with the circle in UnsplittedOutline.
        /// </summary>
        private static Border SplitCircle(GeoPoint2D center, double radius)
        {
            Border border = new Border(new Circle2D(center, radius));
            border.SplitSingleCurve();
            Assert.AreEqual(2, border.Segments.Length);
            AssertIsCircle(border.UnsplittedOutline, center, radius, "split circle");
            return border;
        }

        /// <summary>
        /// Like <see cref="AssertIsCircle"/>, but the curve may also be an <see cref="Ellipse2D"/> with equal axes, which
        /// is what <see cref="ICurve2D.GetModified"/> makes of a circle for most modifications.
        /// </summary>
        private static void AssertIsCircular(ICurve2D outline, GeoPoint2D center, double radius, string what)
        {
            Assert.IsNotNull(outline, what + ": UnsplittedOutline must not be null");
            Assert.IsTrue(outline.IsClosed, what + ": UnsplittedOutline must be closed");
            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(radius, outline.PointAt(i / 16.0) | center, 1e-6, what + ": distance from the center");
            }
            Assert.AreEqual(Math.PI * radius * radius, Math.Abs(outline.GetArea()), 1e-6, what + ": area");
        }

        /// <summary>
        /// Signed area of the polygon through points sampled along the curves, positive for counterclockwise.
        /// </summary>
        private static double SampledArea(IEnumerable<ICurve2D> curves)
        {
            List<GeoPoint2D> points = new List<GeoPoint2D>();
            foreach (ICurve2D curve in curves)
            {
                for (int i = 0; i < 32; i++) points.Add(curve.PointAt(i / 32.0));
            }
            double area = 0.0;
            for (int i = 0; i < points.Count; i++)
            {
                GeoPoint2D p = points[i], q = points[(i + 1) % points.Count];
                area += p.x * q.y - q.x * p.y;
            }
            return area / 2.0;
        }

        private static void AssertOrientedLikeTheBorder(Border border, string what)
        {
            Assert.AreEqual(Math.Sign(SampledArea(border.Segments)), Math.Sign(SampledArea(new[] { border.UnsplittedOutline })),
                what + ": UnsplittedOutline must run like the segments");
        }

        [TestMethod]
        public void ChangeCyclicalStart_KeepsUnsplittedOutline()
        {
            GeoPoint2D center = new GeoPoint2D(5, 7);
            Border border = SplitCircle(center, 3);
            GeoPoint2D secondStart = border.Segments[1].StartPoint;
            border.ChangeCyclicalStart(1);
            Assert.AreEqual(0.0, border.Segments[0].StartPoint | secondStart, 1e-12, "the border starts with the second half");
            AssertIsCircle(border.UnsplittedOutline, center, 3, "after ChangeCyclicalStart");
            AssertOrientedLikeTheBorder(border, "after ChangeCyclicalStart");
        }

        [TestMethod]
        public void Clone_KeepsUnsplittedOutline()
        {
            GeoPoint2D center = new GeoPoint2D(5, 7);
            Border border = SplitCircle(center, 3);
            Border clone = border.Clone();
            AssertIsCircle(clone.UnsplittedOutline, center, 3, "clone");
            AssertOrientedLikeTheBorder(clone, "clone");
            Assert.AreNotSame(border.UnsplittedOutline, clone.UnsplittedOutline, "the clone must not share the curve");
            clone.Move(1, 1);
            AssertIsCircle(border.UnsplittedOutline, center, 3, "original after moving the clone");
        }

        [TestMethod]
        public void Move_MovesUnsplittedOutline()
        {
            Border border = SplitCircle(new GeoPoint2D(5, 7), 3);
            border.Move(10, -2);
            AssertIsCircle(border.UnsplittedOutline, new GeoPoint2D(15, 5), 3, "after Move");
        }

        [TestMethod]
        public void GetModified_ModifiesUnsplittedOutline()
        {
            Border border = SplitCircle(new GeoPoint2D(5, 7), 3);

            Border scaled = border.GetModified(ModOp2D.Translate(10, -4) * ModOp2D.Scale(2));
            AssertIsCircular(scaled.UnsplittedOutline, new GeoPoint2D(20, 10), 6, "scaled");
            AssertOrientedLikeTheBorder(scaled, "scaled");

            Border mirrored = border.GetModified(ModOp2D.Scale(-1, 1));
            AssertIsCircular(mirrored.UnsplittedOutline, new GeoPoint2D(-5, 7), 3, "mirrored");
            AssertOrientedLikeTheBorder(mirrored, "mirrored");

            // stretched in one direction the circle becomes an ellipse, as the segments do
            Border stretched = border.GetModified(ModOp2D.Scale(2, 1));
            Ellipse2D ellipse = stretched.UnsplittedOutline as Ellipse2D;
            Assert.IsNotNull(ellipse, "stretched: UnsplittedOutline must be an ellipse");
            Assert.IsTrue(ellipse.IsClosed, "stretched: UnsplittedOutline must be closed");
            Assert.AreEqual(Math.PI * 6 * 3, Math.Abs(ellipse.GetArea()), 1e-9, "stretched: area");
            AssertOrientedLikeTheBorder(stretched, "stretched");

            AssertIsCircle(border.UnsplittedOutline, new GeoPoint2D(5, 7), 3, "original");
        }

        [TestMethod]
        public void GetModified_ReflectedClosedSplineRunsLikeTheBorder()
        {
            // a spline keeps its direction when it is reflected, while the border reverses its segments
            BSpline2D spline = new BSpline2D(new[] { new GeoPoint2D(0, 0), new GeoPoint2D(10, 1), new GeoPoint2D(12, 8), new GeoPoint2D(3, 9), new GeoPoint2D(-2, 5) }, 3, true);
            Border border = new Border(spline.Clone());
            border.SplitSingleCurve();
            Assert.IsNotNull(border.UnsplittedOutline);
            AssertOrientedLikeTheBorder(border, "split spline");

            ModOp2D m = ModOp2D.Rotate(SweepAngle.Deg(30)) * ModOp2D.Scale(-2, 1);
            Border mirrored = border.GetModified(m);
            ICurve2D outline = mirrored.UnsplittedOutline;
            Assert.IsNotNull(outline, "UnsplittedOutline must not be null");
            Assert.IsTrue(outline.IsClosed, "UnsplittedOutline must be closed");
            ModOp2D inverse = m.GetInverse();
            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(0.0, spline.MinDistance(inverse * outline.PointAt(i / 16.0)), 1e-6, "UnsplittedOutline must be the modified spline");
            }
            AssertOrientedLikeTheBorder(mirrored, "mirrored spline");
        }

        [TestMethod]
        public void CloneAndGetModifiedOfTheShape_KeepUnsplittedOutlineOfCircularHole()
        {
            Polyline rect = Polyline.Construct();
            rect.SetPoints(new[] { new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0), new GeoPoint(100, 50, 0), new GeoPoint(0, 50, 0) }, true);
            GeoObjectList objects = new GeoObjectList(rect, MakeCircle(25, 25, 10));
            CompoundShape cs = CompoundShape.CreateFromList(objects, 0.001, out Plane plane, false);
            Assert.IsNotNull(cs);
            GeoPoint2D center = plane.Project(new GeoPoint(25, 25, 0));

            SimpleShape clone = Assert.That.Single(cs.Clone().SimpleShapes);
            AssertIsCircle(Assert.That.Single(clone.Holes).UnsplittedOutline, center, 10, "hole of the cloned shape");

            SimpleShape moved = Assert.That.Single(cs.GetModified(ModOp2D.Translate(1, 2)).SimpleShapes);
            AssertIsCircular(Assert.That.Single(moved.Holes).UnsplittedOutline, center + new GeoVector2D(1, 2), 10, "hole of the modified shape");
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
