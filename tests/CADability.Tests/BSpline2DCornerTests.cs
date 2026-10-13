using CADability.Curve2D;

namespace CADability.Tests
{
    /// <summary>
    /// #416, finding 6: a 2d spline whose tangent jumps at a knot of full multiplicity, without a vanishing derivative, was
    /// no corner for <c>BSpline2D.CornerKnots</c>, while <c>BSpline.CornerParameters</c> took it for one. So the triangulation
    /// had a single base point there, with the tangent of one side, and <see cref="BSpline2D.MinDistance"/> missed the corner
    /// as the closest point for the points in front of it, by about 4e-6 here.
    /// </summary>
    [TestClass]
    public class BSpline2DCornerTests
    {
        // degree 2, the middle knot has multiplicity 2: the curve passes through (10,10) with a kink, the tangent turns
        // from (5,2) to (5,-2), the derivative does not vanish on either side
        private static BSpline2D Kinked() => new BSpline2D(
            new[] { new GeoPoint2D(0, 0), new GeoPoint2D(5, 8), new GeoPoint2D(10, 10), new GeoPoint2D(15, 8), new GeoPoint2D(20, 0) },
            null, new double[] { 0, 1, 2 }, new int[] { 3, 2, 3 }, 2, false, 0, 2);

        [TestMethod]
        public void the_corner_is_the_closest_point_for_the_points_in_front_of_it()
        {
            BSpline2D spline = Kinked();
            foreach (GeoPoint2D p in new[] { new GeoPoint2D(10, 12), new GeoPoint2D(10, 11), new GeoPoint2D(10.3, 13) })
            {
                double sampled = double.MaxValue;
                for (int i = 0; i <= 200000; i++) sampled = Math.Min(sampled, spline.PointAt(i / 200000.0) | p);
                Assert.AreEqual(sampled, spline.MinDistance(p), 1e-9, $"MinDistance of {p}");
            }
        }

        [TestMethod]
        public void the_corner_is_no_self_intersection()
        {
            Assert.AreEqual(0, Kinked().GetSelfIntersections().Length);
        }
    }
}
