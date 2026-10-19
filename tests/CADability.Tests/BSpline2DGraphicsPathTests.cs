#if WINDOWS
using CADability.Curve2D;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="BSpline2D.AddToGraphicsPath"/> passed knot parameters to <see cref="BSpline2D.PointAt"/>, which expects
    /// 0..1, so a spline whose knots do not run from 0 to 1 was drawn through wrong points. GDI+ only works on Windows, so
    /// this test only runs there.
    /// </summary>
    [TestClass]
    public class BSpline2DGraphicsPathTests
    {
        [TestMethod]
        public void ASplineWithKnotsBeyondOneIsSampledOnTheCurve()
        {
            BSpline2D bsp = new BSpline2D(new[] { new GeoPoint2D(0, 0), new GeoPoint2D(1, 3), new GeoPoint2D(4, 3), new GeoPoint2D(5, 0) }, null, new double[] { 0, 5 }, new[] { 4, 4 }, 3, false, 0, 5);
            GraphicsPath path = new GraphicsPath();
            bsp.AddToGraphicsPath(path, true);
            PointF[] points = path.PathPoints;
            Assert.AreEqual(bsp.StartPoint.PointF, points[0]);
            Assert.AreEqual(bsp.EndPoint.PointF, points[points.Length - 1]);
            // MinDistance and not Distance: BSpline2D.Distance returns double.MaxValue for these points
            foreach (PointF p in points) Assert.AreEqual(0.0, bsp.MinDistance(new GeoPoint2D(p.X, p.Y)), 1e-4);
        }
    }
}
#endif
