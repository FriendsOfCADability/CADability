using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Plane.FromPoints used to fit a*x + b*y + c*z + 1 = 0, which cannot describe a plane through the origin. For such
    /// points it moved them along the axis with the smallest extent, and if that axis lies in the plane, the plane stayed
    /// where it was. The points were then reported as linear, and a planar BSpline projected its poles onto an arbitrary
    /// plane through its chord - it lost one coordinate everywhere between its end points. The fit is now done relative
    /// to the centroid of the points, which also fixes issues #147 (points far from the origin) and #204 (orientation).
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

        /// <summary>Random points in the XY plane, some sets around the origin, some far away from it.</summary>
        private static IEnumerable<GeoPoint[]> PointSetsInTheXYPlane()
        {
            Random rnd = new Random(204);
            foreach (double offset in new[] { 0.0, 10.0, -500.0, 366081.0 })
            {
                for (int k = 0; k < 25; k++)
                {
                    GeoPoint[] points = new GeoPoint[3 + rnd.Next(20)];
                    for (int i = 0; i < points.Length; i++) points[i] = new GeoPoint(offset + 100 * rnd.NextDouble(), offset + 100 * rnd.NextDouble(), 0.0);
                    yield return points;
                }
            }
        }

        /// <summary>
        /// Issue #204: the normal of points with z = 0 always came out as -Z, because the fit moved them up along z first. The
        /// orientation is now fixed by a rule: the z component of the normal is positive (y, then x, when it is 0).
        /// </summary>
        [TestMethod]
        public void points_in_the_xy_plane_give_the_positive_z_axis()
        {
            foreach (GeoPoint[] points in PointSetsInTheXYPlane())
            {
                Plane plane = Plane.FromPoints(points, out double maxDistance, out bool isLinear);
                Assert.IsFalse(isLinear);
                Assert.AreEqual(0.0, maxDistance, 1e-9);
                Assert.AreEqual(1.0, plane.Normal.z, 1e-12, "normal " + plane.Normal.ToString());
                Assert.AreEqual(1.0, plane.DirectionX.x, 1e-9, "x-axis " + plane.DirectionX.ToString());
            }
        }

        /// <summary>
        /// Issue #204: a normal that is +Z or -Z up to rounding errors went through the general branch of the constructor, which
        /// gives an x-axis of -Y or Y instead of X or Y as for the exact normal.
        /// </summary>
        [TestMethod]
        public void an_almost_vertical_normal_gives_the_same_axes_as_the_exact_one()
        {
            Plane up = new Plane(GeoPoint.Origin, new GeoVector(1.1526487981188832E-15, 1.9964611032487207E-17, 1));
            Assert.AreEqual(1.0, up.DirectionX.x, 1e-12, "x-axis " + up.DirectionX.ToString());
            Assert.AreEqual(1.0, up.DirectionY.y, 1e-12, "y-axis " + up.DirectionY.ToString());
            Assert.AreEqual(1.1526487981188832E-15, up.Normal.x, 1e-20, "the normal is kept as it is");
            Plane down = new Plane(GeoPoint.Origin, new GeoVector(1.1526487981188832E-15, 1.9964611032487207E-17, -1));
            Plane exactDown = new Plane(GeoPoint.Origin, -GeoVector.ZAxis);
            Assert.AreEqual(1.0, down.DirectionX * exactDown.DirectionX, 1e-12, "x-axis " + down.DirectionX.ToString());
            Assert.AreEqual(1.0, down.DirectionY * exactDown.DirectionY, 1e-12, "y-axis " + down.DirectionY.ToString());
        }

        /// <summary>
        /// The result does not depend on where the points are or in which order they are given: tilted points far away from the
        /// origin give the same plane as near it, and points in vertical planes get the orientation rule too.
        /// </summary>
        [TestMethod]
        public void the_plane_does_not_depend_on_position_or_order()
        {
            GeoVector u = new GeoVector(1, 2, 0.5), v = new GeoVector(-0.3, 0.2, 1);
            GeoVector expected = (u ^ v).Normalized;
            if (expected.z < 0) expected = -expected;
            foreach (double offset in new[] { 0.0, 1e3, 1e5, 366081.0, -1e6 })
            {
                GeoPoint[] points = Arc(u, v, 0, 300, 15);
                for (int i = 0; i < points.Length; i++) points[i] = points[i] + new GeoVector(offset, -offset, offset / 2);
                Plane plane = Plane.FromPoints(points, out double maxDistance, out bool isLinear);
                Assert.IsFalse(isLinear, "offset " + offset);
                Assert.AreEqual(0.0, maxDistance, 1e-9, "offset " + offset);
                Assert.AreEqual(1.0, plane.Normal * expected, 1e-12, "offset " + offset + ", normal " + plane.Normal.ToString());
                Array.Reverse(points);
                Plane reversed = Plane.FromPoints(points, out _, out _);
                Assert.AreEqual(1.0, reversed.Normal * expected, 1e-12, "reversed, offset " + offset);
            }
            Plane vertical = Plane.FromPoints(Arc(new GeoVector(-1, 1, 0), GeoVector.ZAxis, 0, 300, 15), out _, out _);
            Assert.AreEqual(1.0, vertical.Normal * new GeoVector(1, 1, 0).Normalized, 1e-12, "z is 0, so y is positive");
            Plane yz = Plane.FromPoints(Arc(GeoVector.YAxis, GeoVector.ZAxis, 0, 300, 15), out _, out _);
            Assert.AreEqual(1.0, yz.Normal.x, 1e-12, "y and z are 0, so x is positive");
        }

        /// <summary>A clamped cubic BSpline through 13 poles on an ellipse with half axes 1.5 and 3 around (x0, 5000, 0), like the "0" glyph of issue #147.</summary>
        private static BSpline ZeroGlyph(double x0)
        {
            int n = 12;
            GeoPoint[] poles = new GeoPoint[n + 1];
            double[] weights = new double[n + 1];
            for (int i = 0; i <= n; i++)
            {
                double a = 2 * Math.PI * i / n;
                poles[i] = new GeoPoint(x0 + 1.5 * Math.Cos(a), 5000 + 3 * Math.Sin(a), 0);
                weights[i] = 1.0;
            }
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.SetData(3, poles, weights, new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, new int[] { 4, 1, 1, 1, 1, 1, 1, 1, 1, 1, 4 }, false));
            return bsp;
        }

        /// <summary>
        /// Issue #147: far from the origin the fit of the uncentred coordinates looked rank deficient, the poles were reported as
        /// linear, and the spline was flattened onto a line through its end points.
        /// </summary>
        [TestMethod]
        public void a_closed_planar_bspline_far_from_the_origin_stays_planar()
        {
            foreach (double x0 in new[] { 366081.0, 100000.0, 0.0 })
            {
                ICurve curve = ZeroGlyph(x0);
                Assert.AreEqual(PlanarState.Planar, curve.GetPlanarState(), "x0 = " + x0);
                Assert.AreEqual(1.0, curve.GetPlane().Normal.z, 1e-12, "x0 = " + x0);
                BoundingCube ext = BoundingCube.EmptyBoundingCube;
                for (int i = 0; i <= 400; i++) ext.MinMax(curve.PointAt(i / 400.0));
                Assert.AreEqual(2.933, ext.Xmax - ext.Xmin, 1e-3, "width at x0 = " + x0);
                Assert.AreEqual(5.732, ext.Ymax - ext.Ymin, 1e-3, "height at x0 = " + x0);
                Assert.AreEqual(0.0, ext.Zmax - ext.Zmin, 1e-9, "flat at x0 = " + x0);
            }
        }

        /// <summary>
        /// Issue #204: the plane of a shape made from a closed planar spline in the XY plane had the normal -Z, so all arcs made
        /// from it were exported with the extrusion direction (0, 0, -1).
        /// </summary>
        [TestMethod]
        public void a_shape_from_a_planar_spline_lies_in_a_plane_with_positive_z_normal()
        {
            List<GeoPoint> through = new List<GeoPoint>();
            for (int i = 0; i < 12; i++)
            {
                double a = 2 * Math.PI * i / 12, r = 60 + 10 * Math.Sin(3 * a);
                through.Add(new GeoPoint(200 + r * Math.Cos(a), 100 + r * Math.Sin(a), 0));
            }
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.ThroughPoints(through.ToArray(), 3, true));
            Assert.AreEqual(1.0, (bsp as ICurve).GetPlane().Normal.z, 1e-12, "plane of the spline");
            CADability.Shapes.CompoundShape cs = CADability.Shapes.CompoundShape.CreateFromList(new GeoObjectList(bsp), 0.001, out Plane plane);
            Assert.IsNotNull(cs);
            Assert.AreEqual(1.0, plane.Normal.z, 1e-12, "normal " + plane.Normal.ToString());
            Assert.AreEqual(1.0, plane.DirectionX.x, 1e-9, "x-axis " + plane.DirectionX.ToString());
        }

        /// <summary>
        /// A straight spline whose inner poles deviate from the line by less than Precision.eps does not define a plane: it
        /// must stay UnderDetermined, so that it still shares a plane with a neighbouring arc. A fit that only looked at the
        /// rounding of the coordinates reported such splines as planar, with a normal that only reflected the deviations.
        /// </summary>
        [TestMethod]
        public void an_almost_straight_spline_still_shares_a_plane_with_an_arc()
        {
            foreach (double offset in new[] { 0.0, 1000.0, 100000.0 })
            {
                foreach (double deviation in new[] { 0.0, 1e-10, 1e-7, 5e-7 })
                {
                    GeoPoint[] poles = { new GeoPoint(offset, 0, 0), new GeoPoint(offset + 33, 0.7 * deviation, 0.3 * deviation),
                        new GeoPoint(offset + 66, -0.4 * deviation, 0.9 * deviation), new GeoPoint(offset + 100, 0, 0) };
                    BSpline bsp = BSpline.Construct();
                    Assert.IsTrue(bsp.SetData(3, poles, null, new double[] { 0, 1 }, new int[] { 4, 4 }, false));
                    Assert.AreEqual(PlanarState.UnderDetermined, (bsp as ICurve).GetPlanarState(), "offset " + offset + ", deviation " + deviation);
                    Ellipse arc = Ellipse.Construct();
                    arc.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, new GeoPoint(offset + 100, 50, 0), 50, -Math.PI / 2, Math.PI / 2);
                    Assert.IsTrue(Curves.GetCommonPlane(bsp, arc, out Plane common), "offset " + offset + ", deviation " + deviation);
                    Assert.AreEqual(1.0, Math.Abs(common.Normal.z), 1e-6, "offset " + offset + ", deviation " + deviation);
                }
            }
            // a small curve that is clearly bent stays planar, even if it is bent by less than Precision.eps
            GeoPoint[] small = { new GeoPoint(0, 0, 0), new GeoPoint(1e-4, 5e-7, 0), new GeoPoint(2e-4, 0, 0) };
            Plane.FromPoints(small, out _, out bool smallIsLinear);
            Assert.IsFalse(smallIsLinear, "a small bent curve is not linear");
        }
    }
}
