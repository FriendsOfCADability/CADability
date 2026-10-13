using CADability.Curve2D;

namespace CADability.Tests
{
    /// <summary>
    /// Circles, arcs, ellipses and elliptical arcs under a singular <see cref="ModOp2D"/> (#416, finding 3). The image lies on a
    /// line, and dividing by the vanishing minor axis gave an <see cref="EllipseArc2D"/> with NaN coordinates. It is a
    /// <see cref="Line2D"/> over the range the image covers now.
    /// </summary>
    [TestClass]
    public class SingularModificationTests
    {
        private static ICurve2D[] Curves() => new ICurve2D[]
        {
            new Circle2D(new GeoPoint2D(1, 2), 3),
            new Arc2D(new GeoPoint2D(1, 2), 3, new Angle(0.3), new SweepAngle(2.0)),
            new Arc2D(new GeoPoint2D(1, 2), 3, new Angle(2.0), new SweepAngle(-2.5)),
            new Ellipse2D(new GeoPoint2D(1, 2), new GeoVector2D(4, 1), new GeoVector2D(-0.5, 2)),
            EllipseArc2D.Create(new GeoPoint2D(1, 2), new GeoVector2D(4, 0), new GeoVector2D(0, 2), new GeoPoint2D(5, 2), new GeoPoint2D(1, 4), true),
        };

        private static ModOp2D[] Singular() => new ModOp2D[]
        {
            ModOp2D.Scale(1, 0),
            ModOp2D.Scale(0, 1),
            ModOp2D.Rotate(new SweepAngle(0.7)) * ModOp2D.Scale(2, 0),
            ModOp2D.Scale(0),
        };

        [TestMethod]
        public void TheImageIsTheSegmentTheCurveIsMappedTo()
        {
            foreach (ICurve2D curve in Curves())
                foreach ((ModOp2D m, int j) in Singular().Select((m, j) => (m, j)))
                {
                    string what = $"{curve.GetType().Name} under singular modification {j}";
                    ICurve2D image = curve.GetModified(m);
                    Assert.IsInstanceOfType(image, typeof(Line2D), what);
                    GeoPoint2D sp = image.StartPoint, ep = image.EndPoint;
                    Assert.IsFalse(double.IsNaN(sp.x) || double.IsNaN(sp.y) || double.IsNaN(ep.x) || double.IsNaN(ep.y), what);
                    GeoPoint2D[] mapped = Enumerable.Range(0, 1001).Select(i => m * curve.PointAt(i / 1000.0)).ToArray();
                    foreach (GeoPoint2D p in mapped)
                        Assert.IsTrue(image.Distance(p) < 1e-9, $"{what}: {p} is not on {sp} - {ep}");
                    // the segment is not longer than the image: both of its ends are reached
                    Assert.IsTrue(mapped.Min(p => p | sp) < 1e-4 && mapped.Min(p => p | ep) < 1e-4, $"{what}: {sp} - {ep} is too long");
                    // it points the way the image leaves the start point
                    if (image.Length > 0)
                    {
                        GeoVector2D leaving = mapped.First(p => (p | mapped[0]) > 1e-6 * image.Length) - mapped[0];
                        Assert.IsTrue(leaving * (ep - sp) > 0, $"{what}: direction");
                    }
                }
        }

        [TestMethod]
        public void AnImageThatDoesNotTurnBackStartsAndEndsWhereTheCurveDoes()
        {
            ICurve2D arc = new Arc2D(new GeoPoint2D(1, 2), 3, new Angle(0.3), new SweepAngle(2.0)); // x decreases all the way
            ModOp2D m = ModOp2D.Scale(1, 0);
            ICurve2D image = arc.GetModified(m);
            Assert.IsTrue((image.StartPoint | m * arc.StartPoint) < 1e-12);
            Assert.IsTrue((image.EndPoint | m * arc.EndPoint) < 1e-12);
        }
    }
}
