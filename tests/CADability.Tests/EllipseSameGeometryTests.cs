using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="ICurve.SameGeometry"/> of two arcs compares the curves by their points, not by their centers
    /// (ported from the ShapeIt fork). The center of a short arc is ill-conditioned: moving one end by less than the
    /// precision moves the center by much more, and the arcs were then not recognized as the same curve.
    /// </summary>
    [TestClass]
    public class EllipseSameGeometryTests
    {
        private static Ellipse Arc(GeoPoint start, GeoPoint middle, GeoPoint end)
        {
            Ellipse arc = Ellipse.Construct();
            arc.SetArc3Points(start, middle, end, Plane.XYPlane);
            return arc;
        }

        private static GeoPoint OnCircle(double radius, double angle) => new GeoPoint(radius * Math.Cos(angle), radius * Math.Sin(angle), 0.0);

        [TestMethod]
        public void ShortArcWithSlightlyMovedEndIsTheSameCurve()
        {
            const double r = 10.0, sweep = 0.01; // an arc of length 0.1
            Ellipse original = Arc(OnCircle(r, 0.0), OnCircle(r, sweep / 2), OnCircle(r, sweep));
            // the end moved by 5e-5 off the circle, as Shell.RecalcEdges does when it moves the end of an edge onto a vertex
            Ellipse moved = Arc(OnCircle(r, 0.0), OnCircle(r, sweep / 2), OnCircle(r + 5e-5, sweep));
            const double precision = 1e-4;
            Assert.IsTrue((original.Center | moved.Center) > precision, "the test needs a center that moves by more than the precision");
            Assert.IsTrue((original as ICurve).SameGeometry(moved, precision));
            Assert.IsTrue((moved as ICurve).SameGeometry(original, precision));
        }

        [TestMethod]
        public void ReversedArcIsTheSameCurve()
        {
            Ellipse arc = Arc(OnCircle(10, 0.0), OnCircle(10, 0.5), OnCircle(10, 1.0));
            Ellipse reversed = Arc(OnCircle(10, 1.0), OnCircle(10, 0.5), OnCircle(10, 0.0));
            Assert.IsTrue((arc as ICurve).SameGeometry(reversed, 1e-6));
        }

        [TestMethod]
        public void ArcsWithCommonEndsButDifferentBulgeDiffer()
        {
            GeoPoint a = new GeoPoint(-10, 0, 0), b = new GeoPoint(10, 0, 0);
            Ellipse upper = Arc(a, new GeoPoint(0, 10, 0), b);
            Ellipse flat = Arc(a, new GeoPoint(0, 5, 0), b);
            Ellipse lower = Arc(a, new GeoPoint(0, -10, 0), b);
            Assert.IsFalse((upper as ICurve).SameGeometry(flat, 1e-6));
            Assert.IsFalse((upper as ICurve).SameGeometry(lower, 1e-6));
        }

        [TestMethod]
        public void ArcsOnTheSameCircleWithDifferentEndsDiffer()
        {
            Ellipse arc = Arc(OnCircle(10, 0.0), OnCircle(10, 0.5), OnCircle(10, 1.0));
            Ellipse longer = Arc(OnCircle(10, 0.0), OnCircle(10, 0.6), OnCircle(10, 1.2));
            Assert.IsFalse((arc as ICurve).SameGeometry(longer, 1e-6));
        }
    }
}
