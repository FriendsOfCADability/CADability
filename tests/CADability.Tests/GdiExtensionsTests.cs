#if WINDOWS
using CADability.Curve2D;
using CADability.Forms;
using CADability.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CADability.Tests
{
    /// <summary>
    /// The GDI+ conversions moved from the geometry types of the kernel into <see cref="GdiExtensions"/> in CADability.Forms
    /// (issue #412, step 5). The obsolete kernel members stay in 1.x; the extensions must produce the same paths.
    /// GDI+ only works on Windows, so these tests only run there.
    /// </summary>
    [TestClass]
    public class GdiExtensionsTests
    {
#pragma warning disable CS0618 // the obsolete kernel members are the reference
        private static GraphicsPath Old(ICurve2D curve, bool forward)
        {
            GraphicsPath res = new GraphicsPath();
            curve.AddToGraphicsPath(res, forward);
            return res;
        }
#pragma warning restore CS0618
        private static GraphicsPath New(ICurve2D curve, bool forward)
        {
            GraphicsPath res = new GraphicsPath();
            res.AddCurve2D(curve, forward);
            return res;
        }
        private static void AssertSamePath(GraphicsPath expected, GraphicsPath actual, string what)
        {
            CollectionAssert.AreEqual(expected.PathTypes, actual.PathTypes, what + ": path types");
            CollectionAssert.AreEqual(expected.PathPoints, actual.PathPoints, what + ": path points");
        }
        private static ICurve2D[] Curves()
        {
            return new ICurve2D[]
            {
                new Line2D(new GeoPoint2D(1, 2), new GeoPoint2D(7, -3)),
                new Arc2D(new GeoPoint2D(2, 1), 3, Angle.Deg(30), SweepAngle.Deg(120)),
                new Arc2D(new GeoPoint2D(2, 1), 3, Angle.Deg(300), SweepAngle.Deg(-200)),
                new Circle2D(new GeoPoint2D(-1, 4), 2.5),
                new Ellipse2D(new GeoPoint2D(0, 0), new GeoVector2D(4, 1), new GeoVector2D(-0.5, 2)),
                new Polyline2D(new[] { new GeoPoint2D(0, 0), new GeoPoint2D(3, 1), new GeoPoint2D(4, 5), new GeoPoint2D(-1, 2) }),
                new Path2D(new ICurve2D[] { new Line2D(new GeoPoint2D(0, 0), new GeoPoint2D(4, 0)), new Arc2D(new GeoPoint2D(4, 2), 2, Angle.Deg(270), SweepAngle.Deg(180)) }),
                // knots from 0 to 1: the only case in which the old implementation sampled the right parameters
                new BSpline2D(new[] { new GeoPoint2D(0, 0), new GeoPoint2D(1, 3), new GeoPoint2D(4, 3), new GeoPoint2D(5, 0) }, null, new double[] { 0, 1 }, new[] { 4, 4 }, 3, false, 0, 1),
            };
        }

        [TestMethod]
        public void CurvesGiveTheSamePathAsTheObsoleteMembers()
        {
            foreach (ICurve2D curve in Curves())
            {
                AssertSamePath(Old(curve, true), New(curve, true), curve.GetType().Name + " forward");
                AssertSamePath(Old(curve, false), New(curve, false), curve.GetType().Name + " reversed");
            }
        }

        [TestMethod]
        public void EllipseArcStartsAndEndsLikeTheObsoleteMember()
        {
            // the number of points comes from the polar sweep here, the obsolete member uses the parametric sweep
            EllipseArc2D arc = EllipseArc2D.Create(new GeoPoint2D(1, 1), new GeoVector2D(5, 0), new GeoVector2D(0, 2), new GeoPoint2D(6, 1), new GeoPoint2D(1, 3), true);
            foreach (bool forward in new[] { true, false })
            {
                PointF[] expected = Old(arc, forward).PathPoints, actual = New(arc, forward).PathPoints;
                Assert.AreEqual(expected[0], actual[0]);
                Assert.AreEqual(expected[expected.Length - 1], actual[actual.Length - 1]);
            }
        }

        [TestMethod]
        public void BSplineWithKnotsBeyondOneIsSampledOnTheCurve()
        {
            BSpline2D bsp = new BSpline2D(new[] { new GeoPoint2D(0, 0), new GeoPoint2D(1, 3), new GeoPoint2D(4, 3), new GeoPoint2D(5, 0) }, null, new double[] { 0, 5 }, new[] { 4, 4 }, 3, false, 0, 5);
            PointF[] points = New(bsp, true).PathPoints;
            Assert.AreEqual(bsp.StartPoint.PointF, points[0]);
            Assert.AreEqual(bsp.EndPoint.PointF, points[points.Length - 1]);
            // MinDistance and not Distance: BSpline2D.Distance returns double.MaxValue for these points
            foreach (PointF p in points) Assert.AreEqual(0.0, bsp.MinDistance(new GeoPoint2D(p.X, p.Y)), 1e-4);
            AssertSamePath(Old(bsp, true), New(bsp, true), "the obsolete member is fixed as well");
        }

        [TestMethod]
        public void ShapesGiveTheSamePathAsTheObsoleteMembers()
        {
            Border outline = new Border(new ICurve2D[] { new Line2D(new GeoPoint2D(0, 0), new GeoPoint2D(10, 0)), new Line2D(new GeoPoint2D(10, 0), new GeoPoint2D(10, 8)),
                new Line2D(new GeoPoint2D(10, 8), new GeoPoint2D(0, 8)), new Line2D(new GeoPoint2D(0, 8), new GeoPoint2D(0, 0)) });
            Border hole = new Border(new ICurve2D[] { new Circle2D(new GeoPoint2D(5, 4), 2) });
            CompoundShape shape = new CompoundShape(new SimpleShape(outline, hole));
#pragma warning disable CS0618
            GraphicsPath oldBorder = new GraphicsPath();
            outline.AddToGraphicsPath(oldBorder, false);
            GraphicsPath oldShape = shape.CreateGraphicsPath();
#pragma warning restore CS0618
            GraphicsPath newBorder = new GraphicsPath();
            newBorder.AddBorder(outline, false);
            AssertSamePath(oldBorder, newBorder, "border");
            AssertSamePath(oldShape, shape.ToGraphicsPath(), "compound shape");
        }

        [TestMethod]
        public void ModOp2DGivesTheSameMatrixAsTheObsoleteMember()
        {
            ModOp2D m = ModOp2D.Rotate(new GeoPoint2D(3, -2), SweepAngle.Deg(37)) * ModOp2D.Scale(2, 0.5);
#pragma warning disable CS0618
            CollectionAssert.AreEqual(m.Matrix2D.Elements, m.ToGdiMatrix().Elements);
#pragma warning restore CS0618
        }
    }
}
#endif
