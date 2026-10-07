using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Plane.FromPoints fits a*x + b*y + c*z + 1 = 0, which cannot describe a plane through the origin. For such points
    /// it moves them along the axis with the smallest extent, and if that axis lies in the plane, the plane stays where
    /// it is. The points were then reported as linear, and a planar BSpline projected its poles onto an arbitrary plane
    /// through its chord - it lost one coordinate everywhere between its end points.
    /// </summary>
    [TestClass]
    public class PlaneFromPointsTests
    {
        /// <summary>Points of a circle with radius 10 around the origin in the plane spanned by u and v, from angle a to b in degrees.</summary>
        private static GeoPoint[] Arc(GeoVector u, GeoVector v, double a, double b, int count)
        {
            GeoPoint[] res = new GeoPoint[count];
            for (int i = 0; i < count; i++)
            {
                double s = (a + (b - a) * i / (count - 1)) * Math.PI / 180.0;
                res[i] = GeoPoint.Origin + 10.0 * Math.Cos(s) * u.Normalized + 10.0 * Math.Sin(s) * v.Normalized;
            }
            return res;
        }

        /// <summary>
        /// Two arcs in the plane x = -y. The first is symmetric, so x and y have equal extents and the tie went to z. In
        /// the second, z has the smallest extent. Both times the axis lies in the plane.
        /// </summary>
        private static IEnumerable<GeoPoint[]> ArcsInPlanesThroughTheOrigin()
        {
            GeoVector u = new GeoVector(-1, 1, 0), v = GeoVector.ZAxis;
            yield return Arc(u, v, -80, 80, 20);
            yield return Arc(u, v, 80, 100, 20);
        }

        [TestMethod]
        public void a_plane_through_the_origin_is_found_when_an_axis_lies_in_it()
        {
            foreach (GeoPoint[] points in ArcsInPlanesThroughTheOrigin())
            {
                Plane plane = Plane.FromPoints(points, out double maxDistance, out bool isLinear);
                Assert.IsFalse(isLinear, "the points are not on a line");
                Assert.AreEqual(0.0, maxDistance, 1e-9, "the points lie in the plane");
                Assert.AreEqual(1.0, Math.Abs(plane.Normal * new GeoVector(1, 1, 0).Normalized), 1e-9, "the plane is x = -y");
            }
        }

        [TestMethod]
        public void points_on_a_line_through_the_origin_are_still_linear()
        {
            GeoPoint[] points = new GeoPoint[5];
            for (int i = 0; i < points.Length; i++) points[i] = new GeoPoint(i - 2, 2 * (i - 2), 0);
            Plane.FromPoints(points, out double _, out bool isLinear);
            Assert.IsTrue(isLinear);
        }

        /// <summary>
        /// The consequence that showed up: the guide spline of an InterpolatedDualSurfaceCurve on two touching cylinders
        /// lay in x = 0 instead of x = -y, a case found in ShapeIt.
        /// </summary>
        [TestMethod]
        public void a_planar_bspline_passes_through_its_points()
        {
            foreach (GeoPoint[] points in ArcsInPlanesThroughTheOrigin())
            {
                BSpline bsp = BSpline.Construct();
                Assert.IsTrue(bsp.ThroughPoints(points, 3, false));
                foreach (GeoPoint p in points) Assert.AreEqual(0.0, (bsp as ICurve).DistanceTo(p), 1e-9, "the spline passes through " + p.ToString());
                for (int i = 0; i <= 10; i++)
                {
                    GeoPoint p = (bsp as ICurve).PointAt(i / 10.0);
                    Assert.AreEqual(0.0, p.x + p.y, 1e-9, "the spline stays in the plane at " + (i / 10.0) + ": " + p.ToString());
                }
            }
        }
    }
}
