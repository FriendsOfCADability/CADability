using CADability.Curve2D;

namespace CADability.Tests
{
    /// <summary>
    /// Intersections of 2d curves which touch each other instead of crossing. The triangle intersection of
    /// <see cref="GeneralCurve2D"/> only finds crossings, the contacts are found by the closest approach search.
    /// </summary>
    [TestClass]
    public class Curve2DTangentialIntersectionTests
    {
        /// <summary>The quadratic Bezier with poles (-1,1), (0,-1), (1,1), which is exactly y = x² for x in [-1,1].</summary>
        private static BSpline2D Parabola()
        {
            return new BSpline2D(new[] { new GeoPoint2D(-1, 1), new GeoPoint2D(0, -1), new GeoPoint2D(1, 1) },
                null, new double[] { 0.0, 1.0 }, new int[] { 3, 3 }, 2, false, 0.0, 1.0);
        }

        [TestMethod]
        public void ParabolaTouchingLineGivesOnePoint()
        {
            BSpline2D parabola = Parabola();
            // the ends are chosen so that no point of a triangulation falls onto the contact point
            Line2D line = new Line2D(new GeoPoint2D(-1.7, 0), new GeoPoint2D(2.9, 0));
            GeoPoint2DWithParameter[] ips = parabola.Intersect(line);
            Assert.AreEqual(1, ips.Length, "the contact point at the apex");
            // a tangential contact is only determined to about the square root of the precision along the curve
            Assert.AreEqual(0.0, ips[0].p.x, 1e-6);
            Assert.AreEqual(0.0, ips[0].p.y, 1e-9);
            Assert.AreEqual(0.5, ips[0].par1, 1e-6);
            Assert.AreEqual(1.7 / 4.6, ips[0].par2, 1e-6);
        }

        [TestMethod]
        public void ParabolaCrossingLineGivesTwoPoints()
        {
            BSpline2D parabola = Parabola();
            Line2D line = new Line2D(new GeoPoint2D(-2, 0.25), new GeoPoint2D(2, 0.25));
            GeoPoint2DWithParameter[] ips = parabola.Intersect(line).OrderBy(ip => ip.p.x).ToArray();
            Assert.AreEqual(2, ips.Length, "a crossing must not be reported again as a contact");
            Assert.AreEqual(-0.5, ips[0].p.x, 1e-6);
            Assert.AreEqual(0.5, ips[1].p.x, 1e-6);
        }

        /// <summary>
        /// The curves do not touch, but they come closer than <see cref="Precision.eps"/>. The chords never
        /// intersect and the hull triangles separate before they get small, so only the search for the closest
        /// approach finds this contact.
        /// </summary>
        [TestMethod]
        public void ParabolaAlmostTouchingLineGivesOnePoint()
        {
            BSpline2D parabola = Parabola();
            Line2D line = new Line2D(new GeoPoint2D(-1.7, -0.5 * Precision.eps), new GeoPoint2D(2.9, -0.5 * Precision.eps));
            GeoPoint2DWithParameter[] ips = parabola.Intersect(line);
            Assert.AreEqual(1, ips.Length);
            Assert.AreEqual(0.0, ips[0].p.x, 1e-6);
            Assert.AreEqual(0.5, ips[0].par1, 1e-6);
        }

        [TestMethod]
        public void ParabolaMissingLineGivesNoPoint()
        {
            BSpline2D parabola = Parabola();
            Line2D line = new Line2D(new GeoPoint2D(-2, -1e-4), new GeoPoint2D(2, -1e-4));
            Assert.AreEqual(0, parabola.Intersect(line).Length, "the gap is much larger than the precision");
        }

        /// <summary>
        /// A short curve which starts tangentially on a big arc. It lies completely inside one hull triangle of the
        /// arc, so the chords never intersect, however far the triangles are subdivided.
        /// </summary>
        [TestMethod]
        public void ShortCurveEndingTangentiallyOnArc()
        {
            Arc2D arc = new Arc2D(new GeoPoint2D(0, -20), 20, Angle.Deg(40), SweepAngle.Deg(90));
            // starts at the top of the circle with a horizontal tangent and bends away from it
            BSpline2D shortCurve = new BSpline2D(new[] { new GeoPoint2D(0, 0), new GeoPoint2D(0.5, 0), new GeoPoint2D(1, 0.2) },
                null, new double[] { 0.0, 1.0 }, new int[] { 3, 3 }, 2, false, 0.0, 1.0);

            GeoPoint2DWithParameter[] ips = shortCurve.Intersect(arc);
            Assert.AreEqual(1, ips.Length);
            Assert.AreEqual(0.0, ips[0].par1, 1e-6, "the start point of the short curve");
            Assert.AreEqual(50.0 / 90.0, ips[0].par2, 1e-6, "the top of the circle");
            Assert.AreEqual(0.0, ips[0].p | GeoPoint2D.Origin, 1e-6);

            // and the other way round
            ips = arc.Intersect(shortCurve);
            Assert.AreEqual(1, ips.Length);
            Assert.AreEqual(50.0 / 90.0, ips[0].par1, 1e-6);
            Assert.AreEqual(0.0, ips[0].par2, 1e-6);
        }
    }
}
