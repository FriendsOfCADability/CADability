using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #515: the general intersection of two surfaces (<c>BoxedSurfaceEx.Intersect</c>) links the intersection points on the
    /// u/v grids of both surfaces to curves. At a point on the grids of both surfaces the walk advanced only on the surface whose
    /// successor it followed. On the other one it stayed behind, and the curve ended early, after the next point on the grid of only
    /// that surface found no successor. Whether such a point occurs depends on the grid, which earlier intersections refine, so the
    /// same intersection was complete on a new surface and cut short on one that had been intersected before.
    /// </summary>
    [TestClass]
    public class BoxedSurfaceIntersectionTests
    {
        /// <summary>the half of an oblique frustum between the half circles of radius 10 in z = 0 and of radius 5 around (2, 0, 10)</summary>
        private static RuledSurface HalfFrustum()
        {
            Ellipse bottom = Ellipse.Construct();
            bottom.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, GeoPoint.Origin, 10, Math.PI, Math.PI);
            Ellipse top = Ellipse.Construct();
            top.SetArcPlaneCenterRadiusAngles(new Plane(new GeoPoint(2, 0, 10), GeoVector.ZAxis), new GeoPoint(2, 0, 10), 5, Math.PI, Math.PI);
            return new RuledSurface(bottom, top);
        }

        /// <summary>the plane z = <paramref name="z"/> with a scaled uv system, like the faces of a box</summary>
        private static PlaneSurface PlaneAt(double z) =>
            new PlaneSurface(new GeoPoint(-20, -20, z), 40 * GeoVector.YAxis, 40 * GeoVector.XAxis, -GeoVector.ZAxis);

        [TestMethod]
        public void the_intersection_curve_is_complete_after_earlier_intersections_of_the_surface()
        {
            RuledSurface surface = HalfFrustum();
            // the sequence of a boolean operation with the slab 4 < z < 6: the intersections refine the grid of the surface, and the
            // third one, again with z = 4, used to end at (6.73, -5.37, 4) instead of (8.8, 0, 4)
            foreach (double z in new[] { 4.0, 6.0, 4.0, 6.0 })
            {
                double v = z / 10;
                GeoPoint start = surface.PointAt(new GeoPoint2D(0, v));
                GeoPoint end = surface.PointAt(new GeoPoint2D(1, v));
                ICurve[] curves = surface.BoxedSurfaceEx.Intersect(new BoundingRect(0, 0, 1, 1), PlaneAt(z), new BoundingRect(0, 0, 1, 1), new List<GeoPoint> { start, end });
                Assert.AreEqual(1, curves.Length, $"z = {z}");
                GeoPoint[] ends = { curves[0].StartPoint, curves[0].EndPoint };
                Assert.IsTrue(ends.Any(p => (p | start) < 1e-6) && ends.Any(p => (p | end) < 1e-6),
                    $"z = {z}: the curve from {curves[0].StartPoint} to {curves[0].EndPoint} does not connect {start} and {end}");
                for (int i = 0; i <= 10; i++) Assert.AreEqual(z, curves[0].PointAt(i / 10.0).z, 1e-6, "the curve lies in the plane");
            }
        }
    }
}
