using System;
using CADability.Curve2D;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #429: <see cref="Border.MakePolygon"/> created a zero-length first segment and no segment back to the first
    /// vertex, and <see cref="BorderBuilder.Connect"/> was a stub that dropped a segment which did not connect with its
    /// start point.
    /// </summary>
    [TestClass]
    public class BorderMakePolygonTests
    {
        [TestMethod]
        public void HexagonIsClosedWithSixEqualSides()
        {
            GeoPoint2D center = new GeoPoint2D(1, 2);
            double radius = 3.0;
            Border hexagon = Border.MakePolygon(center, radius, 6);

            Assert.AreEqual(6, hexagon.Count);
            Assert.IsTrue(hexagon.IsClosed);
            for (int i = 0; i < hexagon.Count; i++)
            {
                ICurve2D side = hexagon[i];
                Assert.AreEqual(radius, side.Length, 1e-10, "side " + i + " of a hexagon is as long as the radius");
                Assert.AreEqual(radius, Geometry.Dist(center, side.StartPoint), 1e-10, "vertex " + i + " lies on the circle");
                ICurve2D next = hexagon[(i + 1) % hexagon.Count];
                Assert.AreEqual(0.0, Geometry.Dist(side.EndPoint, next.StartPoint), 1e-10, "side " + i + " connects to the next one");
            }
            Assert.AreEqual(1.5 * Math.Sqrt(3.0) * radius * radius, hexagon.Area, 1e-8);
            Assert.AreEqual(0.0, Geometry.Dist(hexagon[0].StartPoint, center + radius * GeoVector2D.XAxis), 1e-10, "the first vertex lies in x direction");
        }

        [TestMethod]
        public void SimpleShapePolygonHasTheSameArea()
        {
            SimpleShape square = SimpleShape.MakePolygon(GeoPoint2D.Origin, Math.Sqrt(2.0), 4);
            Assert.AreEqual(4.0, square.Area, 1e-8);
        }

        [TestMethod]
        public void BorderBuilderConnectReversesAndBridgesSegments()
        {
            BorderBuilder builder = new BorderBuilder();
            builder.AddSegment(new Line2D(new GeoPoint2D(0, 0), new GeoPoint2D(1, 0)));
            builder.Connect(new Line2D(new GeoPoint2D(1, 1), new GeoPoint2D(1, 0))); // connects with its end point: reversed
            builder.Connect(new Line2D(new GeoPoint2D(0.5, 1), new GeoPoint2D(0, 1))); // gap from (1,1) to (0.5,1): bridged
            builder.Connect(new Line2D(new GeoPoint2D(0, 1), new GeoPoint2D(0, 0))); // connects as it is

            Border border = builder.BuildBorder();
            Assert.IsTrue(border.IsClosed);
            Assert.AreEqual(5, border.Count, "four segments plus the bridging line");
            Assert.AreEqual(1.0, border.Area, 1e-10);
            for (int i = 0; i < border.Count; i++)
            {
                ICurve2D next = border[(i + 1) % border.Count];
                Assert.AreEqual(0.0, Geometry.Dist(border[i].EndPoint, next.StartPoint), 1e-10, "segment " + i + " connects to the next one");
            }
        }
    }
}
