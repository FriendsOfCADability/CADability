using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Fixes for the analytic surfaces, ported from the ShapeIt fork of CADability: torus, surface of revolution,
    /// cone, cylinder, plane and surface of linear extrusion. These are the surfaces round, rectangular and bent
    /// tubes from STEP files consist of.
    /// </summary>
    [TestClass]
    public class SurfaceFixesFromShapeItTests
    {
        private static Ellipse Circle(Plane plane, GeoPoint center, double radius)
        {
            Ellipse e = Ellipse.Construct();
            e.SetCirclePlaneCenterRadius(plane, center, radius);
            return e;
        }

        private static ToroidalSurface Torus(double majorRadius, double minorRadius) =>
            new ToroidalSurface(GeoPoint.Origin, GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, majorRadius, minorRadius);

        /// <summary>
        /// Every returned curve must lie on both surfaces, and its 2d curves must describe the same points.
        /// </summary>
        private static void AssertValidDualCurves(IDualSurfaceCurve[] dscs, ISurface s1, ISurface s2, double tol = 1e-6)
        {
            foreach (IDualSurfaceCurve dsc in dscs)
            {
                for (int i = 0; i <= 10; i++)
                {
                    double t = i / 10.0;
                    GeoPoint p = dsc.Curve3D.PointAt(t);
                    Assert.IsTrue(s1.GetDistance(p) < tol, $"point {p} is not on the first surface");
                    Assert.IsTrue(s2.GetDistance(p) < tol, $"point {p} is not on the second surface");
                }
                Assert.IsTrue((s1.PointAt(dsc.Curve2D1.StartPoint) | dsc.Curve3D.StartPoint) < tol, "start of the 2d curve on the first surface");
                Assert.IsTrue((s1.PointAt(dsc.Curve2D1.EndPoint) | dsc.Curve3D.EndPoint) < tol, "end of the 2d curve on the first surface");
                Assert.IsTrue((s2.PointAt(dsc.Curve2D2.StartPoint) | dsc.Curve3D.StartPoint) < tol, "start of the 2d curve on the second surface");
                Assert.IsTrue((s2.PointAt(dsc.Curve2D2.EndPoint) | dsc.Curve3D.EndPoint) < tol, "end of the 2d curve on the second surface");
            }
        }

        [TestMethod]
        public void circle_in_a_meridian_plane_meets_both_tube_circles_of_the_torus()
        {
            ToroidalSurface torus = Torus(30.0, 10.0);
            // a circle around the torus center in the xz-plane passes through the centers of both tube cross sections
            Ellipse circle = Circle(new Plane(GeoPoint.Origin, GeoVector.XAxis, GeoVector.ZAxis), GeoPoint.Origin, 30.0);
            torus.Intersect(circle, new BoundingRect(0, 0, 2 * Math.PI, 2 * Math.PI), out GeoPoint[] ips, out GeoPoint2D[] uv, out double[] u);
            Assert.AreEqual(4, ips.Length);
            for (int i = 0; i < ips.Length; i++)
            {
                Assert.IsTrue(torus.GetDistance(ips[i]) < 1e-8);
                Assert.AreEqual(30.0, ips[i] | GeoPoint.Origin, 1e-8);
                Assert.IsTrue((torus.PointAt(uv[i]) | ips[i]) < 1e-8);
                Assert.IsTrue(((circle as ICurve).PointAt(u[i]) | ips[i]) < 1e-8);
            }
            Assert.AreEqual(2, ips.Count(p => p.x > 0));
            // the same with an ellipse through both tube centers: four different points, not one pair twice
            Ellipse ellipse = Ellipse.Construct();
            ellipse.SetEllipseCenterAxis(GeoPoint.Origin, 30.0 * GeoVector.XAxis, 20.0 * GeoVector.ZAxis);
            torus.Intersect(ellipse, new BoundingRect(0, 0, 2 * Math.PI, 2 * Math.PI), out ips, out uv, out u);
            Assert.AreEqual(4, ips.Length);
            Assert.AreEqual(2, ips.Count(p => p.x > 0));
            Assert.AreEqual(2, ips.Count(p => p.x < 0));
            foreach (GeoPoint p in ips) Assert.IsTrue(torus.GetDistance(p) < 1e-8);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void plane_through_the_torus_axis_gives_two_circles_inside_the_bounds(bool reversed)
        {
            ToroidalSurface torus = Torus(30.0, 10.0);
            if (reversed) torus.ReverseOrientation();
            PlaneSurface pl = new PlaneSurface(new Plane(GeoPoint.Origin, GeoVector.YAxis, GeoVector.ZAxis)); // x == 0
            // the circles are at u = pi/2 and 3pi/2, the requested u range is [4, 8], i.e. 3pi/2 and pi/2 + 2pi
            IDualSurfaceCurve[] dscs = torus.GetPlaneIntersection(pl, 4.0, 8.0, 0.0, 2 * Math.PI, 1e-6);
            Assert.AreEqual(2, dscs.Length);
            foreach (IDualSurfaceCurve dsc in dscs)
            {
                BoundingRect ext = dsc.Curve2D1.GetExtent();
                Assert.IsTrue(ext.Left >= 4.0 && ext.Right <= 8.0, $"2d curve on the torus outside of the bounds: {ext}");
                Assert.AreEqual(2 * Math.PI, dsc.Curve2D1.Length, 1e-8, "a full circle, not a point");
            }
            AssertValidDualCurves(dscs, torus, pl);
        }

        [TestMethod]
        public void torus_is_a_surface_of_revolution()
        {
            ToroidalSurface torus = new ToroidalSurface(new GeoPoint(1, 2, 3), GeoVector.YAxis, GeoVector.ZAxis, GeoVector.XAxis, 30.0, 10.0);
            ISurfaceOfRevolution sor = torus;
            Assert.IsTrue((sor.Axis.Location | new GeoPoint(1, 2, 3)) < 1e-12);
            Assert.IsTrue(Precision.SameDirection(sor.Axis.Direction, GeoVector.XAxis, false));
            ICurve meridian = sor.Curve;
            Assert.IsTrue(meridian is Ellipse e && e.IsCircle && Math.Abs(e.Radius - 10.0) < 1e-12);
            for (int i = 0; i <= 8; i++) Assert.IsTrue(torus.GetDistance(meridian.PointAt(i / 8.0)) < 1e-9);
        }

        /// <summary>
        /// A tube bend described as a surface of revolution with a closed profile (a circle), the way a NURBS based
        /// STEP export may describe a torus. The plane perpendicular to the axis meets the profile twice, so
        /// PositionOf has to take the curve parameter from the same intersection as the angle.
        /// </summary>
        [TestMethod]
        public void position_of_on_a_surface_of_revolution_with_a_closed_profile()
        {
            Ellipse profile = Circle(new Plane(new GeoPoint(30, 0, 0), GeoVector.XAxis, GeoVector.ZAxis), new GeoPoint(30, 0, 0), 10.0);
            SurfaceOfRevolution sor = new SurfaceOfRevolution(profile, GeoPoint.Origin, GeoVector.ZAxis);
            for (int i = 0; i < 12; i++)
            {
                for (int j = 0; j < 12; j++)
                {
                    double a = i * 2 * Math.PI / 12 + 0.1;
                    double b = j * 2 * Math.PI / 12 + 0.05;
                    // the same point described directly: angle a around the axis, angle b on the tube circle
                    double r = 30.0 + 10.0 * Math.Cos(b);
                    GeoPoint p = new GeoPoint(r * Math.Cos(a), r * Math.Sin(a), 10.0 * Math.Sin(b));
                    GeoPoint2D uv = sor.PositionOf(p);
                    Assert.IsTrue((sor.PointAt(uv) | p) < 1e-6, $"PositionOf({p}) gives {sor.PointAt(uv)}");
                }
            }
        }

        [TestMethod]
        public void concentric_circles_on_a_surface_of_revolution_are_straight_lines_in_u()
        {
            BSpline profile = BSpline.Construct();
            profile.ThroughPoints(new GeoPoint[] { new GeoPoint(10, 0, 0), new GeoPoint(12, 0, 5), new GeoPoint(11, 0, 10), new GeoPoint(14, 0, 15) }, 3, false);
            SurfaceOfRevolution sor = new SurfaceOfRevolution(profile, GeoPoint.Origin, GeoVector.ZAxis);
            GeoPoint onProfile = (profile as ICurve).PointAt(0.4);
            Plane horizontal = new Plane(new GeoPoint(0, 0, onProfile.z), GeoVector.XAxis, GeoVector.YAxis);
            // a full circle
            Ellipse full = Circle(horizontal, new GeoPoint(0, 0, onProfile.z), onProfile.x);
            ICurve2D c2d = sor.GetProjectedCurve(full, 1e-6);
            Assert.IsInstanceOfType(c2d, typeof(Line2D));
            Assert.AreEqual(2 * Math.PI, c2d.Length, 1e-8, "the full circle must not collapse to a point");
            for (int i = 0; i <= 8; i++)
                Assert.IsTrue((sor.PointAt(c2d.PointAt(i / 8.0)) | (full as ICurve).PointAt(i / 8.0)) < 1e-6);
            // an arc of 20 degrees across the seam, in both directions
            foreach (bool ccw in new bool[] { true, false })
            {
                Ellipse arc = Ellipse.Construct();
                arc.SetArcPlaneCenterRadiusAngles(horizontal, new GeoPoint(0, 0, onProfile.z), onProfile.x,
                    ccw ? -10 * Math.PI / 180 : 10 * Math.PI / 180, ccw ? 20 * Math.PI / 180 : -20 * Math.PI / 180);
                c2d = sor.GetProjectedCurve(arc, 1e-6);
                Assert.AreEqual(20 * Math.PI / 180, c2d.Length, 1e-8);
                for (int i = 0; i <= 8; i++)
                    Assert.IsTrue((sor.PointAt(c2d.PointAt(i / 8.0)) | (arc as ICurve).PointAt(i / 8.0)) < 1e-6);
            }
        }

        [TestMethod]
        public void surface_of_revolution_knows_its_poles()
        {
            // a quarter circle from the axis down to the equator: a hemisphere with a pole at the start of the profile
            Ellipse quarter = Ellipse.Construct();
            quarter.SetArcPlaneCenterRadiusAngles(new Plane(GeoPoint.Origin, GeoVector.XAxis, GeoVector.ZAxis), GeoPoint.Origin, 10.0, Math.PI / 2, -Math.PI / 2);
            SurfaceOfRevolution sor = new SurfaceOfRevolution(quarter, GeoPoint.Origin, GeoVector.ZAxis);
            double[] vs = sor.GetVSingularities();
            Assert.AreEqual(1, vs.Length);
            for (int i = 0; i < 4; i++) Assert.IsTrue((sor.PointAt(new GeoPoint2D(i * 1.3, vs[0])) | new GeoPoint(0, 0, 10)) < 1e-8);
        }

        private static ConicalSurface Cone() => new ConicalSurface(GeoPoint.Origin, GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, 30 * Math.PI / 180);

        [TestMethod]
        public void position_of_a_point_off_the_cone_is_the_perpendicular_foot()
        {
            ConicalSurface cone = Cone();
            foreach (GeoPoint p in new GeoPoint[] { new GeoPoint(10, 0, 10), new GeoPoint(3, 4, 20), new GeoPoint(-2, 1, 3), new GeoPoint(5, 5, -12) })
            {
                GeoPoint2D uv = cone.PositionOf(p);
                GeoPoint foot = cone.PointAt(uv);
                GeoVector n = cone.GetNormal(uv).Normalized;
                GeoVector d = p - foot;
                Assert.IsTrue((d ^ n).Length < 1e-8 * Math.Max(1.0, d.Length), $"{p}: connection to {foot} is not perpendicular to the cone");
                Assert.AreEqual(cone.GetDistance(p), d.Length, 1e-8);
            }
            // a point on the axis
            GeoPoint2D onAxis = cone.PositionOf(new GeoPoint(0, 0, 5));
            Assert.IsTrue(double.IsFinite(onAxis.x) && double.IsFinite(onAxis.y));
        }

        [DataTestMethod]
        [DataRow(1.0)]
        [DataRow(-1.0)]
        public void cone_offset_on_both_nappes(double nappe)
        {
            ConicalSurface cone = Cone();
            // the face uses either the part above or below the apex
            BoundingRect used = nappe > 0 ? new BoundingRect(0, 5, 2 * Math.PI, 20) : new BoundingRect(0, -20, 2 * Math.PI, -5);
            (cone as ISurfaceImpl).usedArea = used;
            const double offset = 2.0;
            ISurface off = cone.GetOffsetSurface(offset, out ModOp2D mod);
            for (int i = 0; i < 6; i++)
            {
                GeoPoint2D uv = new GeoPoint2D(i * 1.0, used.Bottom + (used.Top - used.Bottom) * i / 5.0);
                GeoPoint expected = cone.PointAt(uv) + offset * cone.GetNormal(uv).Normalized;
                Assert.IsTrue(off.GetDistance(expected) < 1e-8, $"offset point not on the offset surface at {uv}");
                Assert.IsTrue((off.PointAt(mod * uv) | expected) < 1e-8, $"wrong parameter mapping at {uv}");
            }
        }

        [TestMethod]
        public void cone_plane_intersections_are_exact()
        {
            ConicalSurface cone = Cone();
            BoundingRect bounds = new BoundingRect(0, 2, 2 * Math.PI, 30);
            Plane[] planes = new Plane[]
            {
                new Plane(new GeoPoint(0, 0, 10), GeoVector.XAxis, GeoVector.YAxis), // circle
                new Plane(new GeoPoint(0, 0, 10), GeoVector.XAxis, new GeoVector(0, 1, 0.3)), // ellipse
                new Plane(new GeoPoint(3, 0, 0), GeoVector.YAxis, GeoVector.ZAxis), // hyperbola, parallel to the axis
                new Plane(new GeoPoint(0, 0, 10), GeoVector.YAxis, new GeoVector(Math.Sin(30 * Math.PI / 180), 0, Math.Cos(30 * Math.PI / 180))), // parabola, parallel to a surface line
                new Plane(GeoPoint.Origin, GeoVector.XAxis, GeoVector.ZAxis), // through the axis: two lines
            };
            foreach (Plane plane in planes)
            {
                PlaneSurface ps = new PlaneSurface(plane);
                IDualSurfaceCurve[] dscs = cone.GetPlaneIntersection(ps, bounds.Left, bounds.Right, bounds.Bottom, bounds.Top, 1e-6);
                Assert.IsTrue(dscs.Length > 0, $"no intersection with {plane.Location}, {plane.Normal}");
                AssertValidDualCurves(dscs, cone, ps);
            }
        }

        [TestMethod]
        public void circle_touching_a_cylinder_gives_the_exact_touching_point()
        {
            CylindricalSurface cyl = new CylindricalSurface(GeoPoint.Origin, 5 * GeoVector.XAxis, 5 * GeoVector.YAxis, GeoVector.ZAxis);
            Ellipse circle = Circle(new Plane(new GeoPoint(8, 0, 2), GeoVector.XAxis, GeoVector.ZAxis), new GeoPoint(8, 0, 2), 3.0);
            cyl.Intersect(circle, new BoundingRect(0, -10, 2 * Math.PI, 10), out GeoPoint[] ips, out GeoPoint2D[] uv, out double[] u);
            Assert.AreEqual(1, ips.Length);
            Assert.IsTrue((ips[0] | new GeoPoint(5, 0, 2)) < 1e-12);
            Assert.IsTrue((cyl.PointAt(uv[0]) | ips[0]) < 1e-12);
            Assert.IsTrue(((circle as ICurve).PointAt(u[0]) | ips[0]) < 1e-12);
        }

        [TestMethod]
        public void circle_touching_a_plane_gives_the_exact_touching_point()
        {
            PlaneSurface pl = new PlaneSurface(Plane.XYPlane);
            Ellipse circle = Circle(new Plane(new GeoPoint(1, 2, 4), GeoVector.XAxis, GeoVector.ZAxis), new GeoPoint(1, 2, 4), 4.0);
            pl.Intersect(circle, new BoundingRect(-100, -100, 100, 100), out GeoPoint[] ips, out GeoPoint2D[] uv, out double[] u);
            Assert.AreEqual(1, ips.Length);
            Assert.IsTrue((ips[0] | new GeoPoint(1, 2, 0)) < 1e-12);
            Assert.IsTrue(((circle as ICurve).PointAt(u[0]) | ips[0]) < 1e-12);
        }

        [TestMethod]
        public void elliptical_cylinder_and_plane_intersect_in_an_exact_ellipse()
        {
            Ellipse basis = Ellipse.Construct();
            basis.SetEllipseCenterAxis(GeoPoint.Origin, 8 * GeoVector.XAxis, 4 * GeoVector.YAxis);
            SurfaceOfLinearExtrusion sle = new SurfaceOfLinearExtrusion(basis, 20 * GeoVector.ZAxis, 0.0, 2 * Math.PI);
            PlaneSurface ps = new PlaneSurface(new Plane(new GeoPoint(0, 0, 10), GeoVector.XAxis, new GeoVector(0, 1, 0.5)));
            IDualSurfaceCurve[] dscs = sle.GetPlaneIntersection(ps, 0.0, 2 * Math.PI, 0.0, 1.0, 1e-6);
            Assert.AreEqual(1, dscs.Length);
            Assert.IsInstanceOfType(dscs[0].Curve3D, typeof(Ellipse));
            for (int i = 0; i <= 16; i++)
            {
                GeoPoint p = dscs[0].Curve3D.PointAt(i / 16.0);
                Assert.IsTrue(sle.GetDistance(p) < 1e-8 && ps.GetDistance(p) < 1e-8);
            }
        }

        [TestMethod]
        public void surfaces_survive_a_json_roundtrip()
        {
            ISurface[] surfaces = new ISurface[]
            {
                new PlaneSurface(new Plane(new GeoPoint(1, 2, 3), new GeoVector(1, 1, 0), new GeoVector(0, 0, 1))),
                new ToroidalSurface(new GeoPoint(1, 2, 3), GeoVector.YAxis, GeoVector.ZAxis, GeoVector.XAxis, 30.0, 10.0),
                Cone(),
                new SurfaceOfLinearExtrusion(Circle(Plane.XYPlane, GeoPoint.Origin, 3.0), new GeoVector(0, 1, 5), 0.0, 2 * Math.PI),
            };
            foreach (ISurface s in surfaces)
            {
                string json = JsonSerialize.ToString(s);
                ISurface read = JsonSerialize.FromString(json) as ISurface;
                Assert.IsNotNull(read, s.GetType().Name);
                Assert.AreEqual(s.GetType(), read.GetType());
                foreach (GeoPoint2D uv in new GeoPoint2D[] { new GeoPoint2D(0.3, 0.4), new GeoPoint2D(2.0, 0.9), new GeoPoint2D(4.0, 0.1) })
                    Assert.IsTrue((s.PointAt(uv) | read.PointAt(uv)) < 1e-10, s.GetType().Name);
            }
        }
    }
}
