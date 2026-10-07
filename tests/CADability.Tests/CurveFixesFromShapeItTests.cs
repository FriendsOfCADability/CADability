using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Fixes and API for 3d curves, ported from the ShapeIt fork of CADability: GeneralCurve.DistanceTo,
    /// SameGeometry and the numerical second derivative, ICurve.PointAndDerivativesAt, GeneralCurve.PositionOf,
    /// the tetrahedron test for a line segment against a curved segment, and some Ellipse and BSpline fixes.
    /// </summary>
    [TestClass]
    public class CurveFixesFromShapeItTests
    {
        /// <summary>
        /// A half circle of radius 10 in the xy-plane from angle 0 to 180 degrees. Its parameter wraps around:
        /// the position t and t+1 give the same point, so PointAt outside of [0,1] does not extrapolate the
        /// curve but lands at the other end - the way a curve on a periodic parameter domain behaves. PositionOf
        /// is the unclamped angle in (-90, 270] degrees, so points before the start get a negative position and
        /// points behind the end a position greater than 1.
        /// </summary>
        private class WrappingHalfCircle : GeneralCurve
        {
            private const double r = 10.0;
            private static double Wrap(double t) => (t >= 0.0 && t <= 1.0) ? t : t - Math.Floor(t);
            protected override double[] GetBasePoints() => new double[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
            public override GeoPoint PointAt(double position)
            {
                double a = Math.PI * Wrap(position);
                return new GeoPoint(r * Math.Cos(a), r * Math.Sin(a), 0.0);
            }
            public override GeoVector DirectionAt(double position)
            {
                double a = Math.PI * Wrap(position);
                return new GeoVector(-Math.PI * r * Math.Sin(a), Math.PI * r * Math.Cos(a), 0.0);
            }
            public override double PositionOf(GeoPoint p)
            {
                double a = Math.Atan2(p.y, p.x);
                if (a <= -Math.PI / 2) a += 2 * Math.PI;
                return a / Math.PI;
            }
            public override void Reverse() => throw new NotSupportedException();
            public override ICurve[] Split(double position) => throw new NotSupportedException();
            public override void Trim(double startPos, double endPos) => throw new NotSupportedException();
            public override void Modify(ModOp m) => throw new NotSupportedException();
            public override IGeoObject Clone() => new WrappingHalfCircle();
            public override void CopyGeometry(IGeoObject toCopyFrom) { }
        }

        private static Ellipse Arc(double radius, double startDeg, double sweepDeg)
        {
            Ellipse e = Ellipse.Construct();
            e.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, GeoPoint.Origin, radius, startDeg * Math.PI / 180.0, sweepDeg * Math.PI / 180.0);
            return e;
        }

        [TestMethod]
        public void distance_before_the_start_is_measured_to_the_startpoint()
        {
            ICurve crv = new WrappingHalfCircle();
            // slightly before the start (angle -5.7 degrees): the startpoint (10,0,0) is 1 away. PointAt of the
            // negative position wraps to the other end, the endpoint (-10,0,0) is 20 away. The old code compared
            // a point before the start with the endpoint.
            GeoPoint p = new GeoPoint(10.0, -1.0, 0.0);
            Assert.AreEqual(1.0, crv.DistanceTo(p), 1e-9);
            // and symmetrically slightly behind the end
            GeoPoint q = new GeoPoint(-10.0, -1.0, 0.0);
            Assert.AreEqual(1.0, crv.DistanceTo(q), 1e-9);
        }

        [TestMethod]
        public void general_curve_same_geometry_compares_points()
        {
            ICurve crv = new WrappingHalfCircle();
            Assert.IsTrue(crv.SameGeometry(Arc(10.0, 0.0, 180.0), 1e-6));
            Assert.IsTrue(crv.SameGeometry(Arc(10.0, 0.0, 180.0), 0.0)); // precision 0 means Precision.eps
            Assert.IsFalse(crv.SameGeometry(Arc(10.0, 0.0, 90.0), 1e-6));
            Assert.IsFalse(crv.SameGeometry(Arc(10.1, 0.0, 180.0), 1e-6));
        }

        [TestMethod]
        public void general_curve_has_a_numerical_second_derivative()
        {
            ICurve crv = new WrappingHalfCircle();
            foreach (double t in new double[] { 0.0, 0.3, 0.5, 1.0 })
            {
                Assert.IsTrue(crv.TryPointDeriv2At(t, out GeoPoint p, out GeoVector d1, out GeoVector d2));
                Assert.IsTrue((p | crv.PointAt(t)) < 1e-12);
                Assert.IsTrue((d1 - crv.DirectionAt(t)).Length < 1e-12);
                // analytic: the second derivative points to the center, length pi^2*r
                double a = Math.PI * t;
                GeoVector expected = new GeoVector(-Math.PI * Math.PI * 10.0 * Math.Cos(a), -Math.PI * Math.PI * 10.0 * Math.Sin(a), 0.0);
                Assert.IsTrue((d2 - expected).Length < 1e-3 * expected.Length, $"t={t}: {d2} instead of {expected}");
            }
        }

        [TestMethod]
        public void ellipse_point_and_derivatives_are_analytic()
        {
            Ellipse e = Ellipse.Construct();
            e.SetEllipseArcCenterAxis(new GeoPoint(1, 2, 3), new GeoVector(5, 0, 1), new GeoVector(0, 2, 0), 0.3, 2.0);
            ICurve crv = e;
            const double h = 1e-5;
            foreach (double t in new double[] { 0.0, 0.25, 0.7, 1.0 })
            {
                IReadOnlyList<GeoVector> d = crv.PointAndDerivativesAt(t, 4);
                Assert.AreEqual(5, d.Count);
                Assert.IsTrue((new GeoPoint(d[0].x, d[0].y, d[0].z) | crv.PointAt(t)) < 1e-12);
                Assert.IsTrue((d[1] - crv.DirectionAt(t)).Length < 1e-9);
                Assert.IsTrue(crv.TryPointDeriv2At(t, out _, out _, out GeoVector d2));
                Assert.IsTrue((d[2] - d2).Length < 1e-9);
                // the third and fourth derivatives against central differences of the analytic second and third
                GeoVector d3 = (1.0 / (2 * h)) * (crv.PointAndDerivativesAt(t + h, 2)[2] - crv.PointAndDerivativesAt(t - h, 2)[2]);
                Assert.IsTrue((d[3] - d3).Length < 1e-5 * d[3].Length + 1e-9);
                GeoVector d4 = (1.0 / (2 * h)) * (crv.PointAndDerivativesAt(t + h, 3)[3] - crv.PointAndDerivativesAt(t - h, 3)[3]);
                Assert.IsTrue((d[4] - d4).Length < 1e-5 * d[4].Length + 1e-9);
            }
        }

        [TestMethod]
        public void ellipse_public_second_derivative_matches_the_interface()
        {
            Ellipse e = Arc(4.0, 30.0, 100.0);
            Assert.IsTrue(e.TryPointDeriv2At(0.4, out GeoPoint p, out GeoVector d1, out GeoVector d2));
            Assert.IsTrue((e as ICurve).TryPointDeriv2At(0.4, out GeoPoint pi, out GeoVector d1i, out GeoVector d2i));
            Assert.IsTrue((p | pi) < 1e-12 && (d1 - d1i).Length < 1e-12 && (d2 - d2i).Length < 1e-12);
        }

        [TestMethod]
        public void line_derivatives_beyond_the_first_vanish()
        {
            Line l = Line.TwoPoints(new GeoPoint(1, 1, 1), new GeoPoint(4, 5, 1));
            IReadOnlyList<GeoVector> d = l.PointAndDerivativesAt(0.5, 3);
            Assert.AreEqual(4, d.Count);
            Assert.IsTrue((new GeoPoint(d[0].x, d[0].y, d[0].z) | new GeoPoint(2.5, 3, 1)) < 1e-12);
            Assert.IsTrue((d[1] - new GeoVector(3, 4, 0)).Length < 1e-12);
            Assert.IsTrue(d[2].IsNullVector() && d[3].IsNullVector());
        }

        [TestMethod]
        public void bspline_point_and_derivatives_use_the_spline_derivatives()
        {
            BSpline bsp = BSpline.Construct();
            bsp.ThroughPoints(new GeoPoint[] { new GeoPoint(0, 0, 0), new GeoPoint(3, 2, 1), new GeoPoint(6, -1, 2), new GeoPoint(9, 1, 0) }, 3, false);
            ICurve crv = bsp;
            IReadOnlyList<GeoVector> d = crv.PointAndDerivativesAt(0.4, 2);
            Assert.AreEqual(3, d.Count);
            Assert.IsTrue(crv.TryPointDeriv2At(0.4, out GeoPoint p, out GeoVector d1, out GeoVector d2));
            Assert.IsTrue((new GeoPoint(d[0].x, d[0].y, d[0].z) | p) < 1e-12);
            Assert.IsTrue((d[1] - d1).Length < 1e-12 && (d[2] - d2).Length < 1e-12);
        }

        [TestMethod]
        public void position_of_by_levenberg_marquardt()
        {
            Ellipse e = Arc(10.0, 0.0, 90.0);
            GeoPoint onCurve = (e as ICurve).PointAt(0.6);
            GeoPoint p = onCurve + 2.0 * (onCurve - GeoPoint.Origin).Normalized; // 2 outside, foot point is onCurve
            double u = 0.3;
            Assert.IsTrue(GeneralCurve.PositionOf(e, p, ref u));
            Assert.AreEqual(0.6, u, 1e-8);
        }

        [TestMethod]
        public void line_segment_against_curved_tetrahedra_finds_the_intersection()
        {
            // a 3d spline (its tetrahedra are not flat) and a straight line piercing it: the line has linear
            // tetrahedra only, so this goes through the case "exactly one of the two is a line segment".
            BSpline bsp = BSpline.Construct();
            bsp.ThroughPoints(new GeoPoint[] { new GeoPoint(0, 0, 0), new GeoPoint(3, 2, 1), new GeoPoint(6, -1, 3), new GeoPoint(9, 1, 2) }, 3, false);
            GeoPoint target = (bsp as ICurve).PointAt(0.37);
            GeoVector dir = new GeoVector(0.3, -0.4, 1.0);
            Line l = Line.TwoPoints(target - 5.0 * dir, target + 3.0 * dir);
            TetraederHull thLine = new TetraederHull(l);
            TetraederHull thSpline = new TetraederHull(bsp);
            int n = thLine.Intersect(thSpline, out double[] parLine, out double[] parSpline, out GeoPoint[] ips);
            Assert.IsTrue(n >= 1, "no intersection found");
            Assert.IsTrue(ips.Any(ip => (ip | target) < 1e-6));
            n = thSpline.Intersect(thLine, out parSpline, out parLine, out ips);
            Assert.IsTrue(n >= 1, "no intersection found with exchanged roles");
            Assert.IsTrue(ips.Any(ip => (ip | target) < 1e-6));
        }

        [TestMethod]
        public void line_segment_against_flat_tetrahedra_finds_the_intersection()
        {
            // a circular arc in the xy-plane (flat tetrahedra) and a line crossing it in the same plane
            Ellipse arc = Arc(10.0, 10.0, 70.0);
            GeoPoint target = (arc as ICurve).PointAt(0.5);
            Line inPlane = Line.TwoPoints(new GeoPoint(0, 0, 0), GeoPoint.Origin + 2.0 * target.ToVector());
            int n = new TetraederHull(inPlane).Intersect(new TetraederHull(arc), out _, out _, out GeoPoint[] ips);
            Assert.IsTrue(n >= 1 && ips.Any(ip => (ip | target) < 1e-6), "line in the plane of the arc");
            // and a line piercing the plane of the arc at the arc
            Line piercing = Line.TwoPoints(target + new GeoVector(0.2, 0.1, -3), target + new GeoVector(-0.2, -0.1, 3));
            n = new TetraederHull(piercing).Intersect(new TetraederHull(arc), out _, out _, out ips);
            Assert.IsTrue(n >= 1 && ips.Any(ip => (ip | target) < 1e-6), "line piercing the plane of the arc");
        }

        [TestMethod]
        public void ellipse_complement_goes_the_other_way_round()
        {
            Ellipse e = Arc(5.0, 0.0, 90.0);
            GeoPoint sp = e.StartPoint, ep = e.EndPoint;
            e.Complement();
            Assert.AreEqual(-1.5 * Math.PI, e.SweepParameter, 1e-12);
            Assert.IsTrue((e.StartPoint | sp) < 1e-12 && (e.EndPoint | ep) < 1e-12);
            Assert.IsTrue(((e as ICurve).PointAt(0.5) | new GeoPoint(-5.0 / Math.Sqrt(2), -5.0 / Math.Sqrt(2), 0)) < 1e-9);
            // a full circle has no complement
            Ellipse c = Ellipse.Construct();
            c.SetCirclePlaneCenterRadius(Plane.XYPlane, GeoPoint.Origin, 5.0);
            double sweep = c.SweepParameter;
            c.Complement();
            Assert.AreEqual(sweep, c.SweepParameter);
        }

        [TestMethod]
        public void arc_with_identical_start_and_endpoint_is_a_full_circle()
        {
            Ellipse e = Ellipse.Construct();
            e.SetArcPlaneCenterStartEndPoint(Plane.XYPlane, GeoPoint2D.Origin, new GeoPoint2D(3, 0), new GeoPoint2D(3, 0), Plane.XYPlane, true);
            Assert.AreEqual(2 * Math.PI, e.SweepParameter, 1e-12);
            e.SetArcPlaneCenterStartEndPoint(Plane.XYPlane, GeoPoint2D.Origin, new GeoPoint2D(3, 0), new GeoPoint2D(3, 0), Plane.XYPlane, false);
            Assert.AreEqual(-2 * Math.PI, e.SweepParameter, 1e-12);
        }

        [TestMethod]
        public void plane_intersection_at_the_endpoint_of_an_arc_is_found()
        {
            Ellipse e = Arc(5.0, 0.0, 90.0);
            // the plane y==0 passes exactly through the startpoint, x==0 through the endpoint
            Assert.AreEqual(1, e.PlaneIntersection(new Plane(GeoPoint.Origin, GeoVector.XAxis, GeoVector.ZAxis)).Length);
            Assert.AreEqual(1, e.PlaneIntersection(new Plane(GeoPoint.Origin, GeoVector.YAxis, GeoVector.ZAxis)).Length);
        }

        [TestMethod]
        public void ellipse_same_geometry_with_precision_zero()
        {
            ICurve a = Arc(5.0, 0.0, 90.0);
            ICurve b = Arc(5.0, 0.0, 90.0);
            Assert.IsTrue(a.SameGeometry(b, 0.0));
        }

        [TestMethod]
        public void bspline_interval_extent_contains_the_inner_extrema()
        {
            BSpline bsp = BSpline.Construct();
            bsp.ThroughPoints(new GeoPoint[] { new GeoPoint(0, 0, 0), new GeoPoint(2, 3, 1), new GeoPoint(4, -2, -1), new GeoPoint(6, 4, 2), new GeoPoint(8, 0, 0) }, 3, false);
            bsp.GetData(out int degree, out GeoPoint[] _, out double[] _, out double[] knots, out int[] _);
            double pmin = knots[0] + 0.1 * (knots[knots.Length - 1] - knots[0]);
            double pmax = knots[0] + 0.85 * (knots[knots.Length - 1] - knots[0]);
            BoundingBox ext = bsp.GetIntervalExtent(pmin, pmax);
            // dense sampling of the interval: every sample is inside, and the extreme samples touch the box
            BoundingBox sampled = BoundingBox.EmptyBoundingCube;
            for (int i = 0; i <= 20000; i++)
            {
                GeoPoint p = bsp.PointAtParam(pmin + i * (pmax - pmin) / 20000);
                sampled.MinMax(p);
            }
            Assert.AreEqual(sampled.Xmin, ext.Xmin, 1e-6); Assert.AreEqual(sampled.Xmax, ext.Xmax, 1e-6);
            Assert.AreEqual(sampled.Ymin, ext.Ymin, 1e-6); Assert.AreEqual(sampled.Ymax, ext.Ymax, 1e-6);
            Assert.AreEqual(sampled.Zmin, ext.Zmin, 1e-6); Assert.AreEqual(sampled.Zmax, ext.Zmax, 1e-6);
        }

        [TestMethod]
        public void bezier_net_of_a_knot_span_reproduces_the_spline()
        {
            BSpline bsp = BSpline.Construct();
            bsp.ThroughPoints(new GeoPoint[] { new GeoPoint(0, 0, 0), new GeoPoint(2, 3, 1), new GeoPoint(4, -2, -1), new GeoPoint(6, 4, 2), new GeoPoint(8, 0, 0) }, 3, false);
            bsp.GetData(out int degree, out GeoPoint[] poles, out double[] weights, out double[] knots, out int[] mult);
            List<double> flat = new List<double>();
            for (int i = 0; i < knots.Length; i++) for (int j = 0; j < mult[i]; j++) flat.Add(knots[i]);
            Nurbs<GeoPoint, GeoPointPole> nbs = new Nurbs<GeoPoint, GeoPointPole>(degree, poles, flat.ToArray());
            for (int s = 0; s < knots.Length - 1; s++)
            {
                double mid = 0.5 * (knots[s] + knots[s + 1]);
                GeoPoint[] bez = nbs.GetSpanBezier(mid, out double u0, out double u1);
                Assert.AreEqual(knots[s], u0, 1e-12);
                Assert.AreEqual(knots[s + 1], u1, 1e-12);
                Assert.AreEqual(degree + 1, bez.Length);
                for (int k = 0; k <= 10; k++)
                {
                    double t = k / 10.0;
                    GeoPoint b = DeCasteljau(bez, t);
                    GeoPoint c = nbs.CurvePoint(u0 + t * (u1 - u0));
                    Assert.IsTrue((b | c) < 1e-9, $"span {s}, t={t}");
                }
            }
        }

        [TestMethod]
        public void foot_point_on_a_2d_spline()
        {
            BSpline2D bsp = new BSpline2D(new GeoPoint2D[] { new GeoPoint2D(0, 0), new GeoPoint2D(3, 2), new GeoPoint2D(6, -1), new GeoPoint2D(9, 1) }, 3, false);
            double u0 = bsp.StartParam, u1 = bsp.EndParam;
            double uTarget = u0 + 0.4 * (u1 - u0);
            GeoPoint2D onCurve = bsp.PointAtParam(uTarget);
            GeoVector2D normal = bsp.DirectionAt(bsp.PositionOf(onCurve)).ToLeft().Normalized;
            GeoPoint2D p = onCurve + 0.3 * normal; // close enough, so the foot point is unique
            Assert.IsTrue(bsp.TryFindFootPoint(p, u0, u1, out double uFoot));
            Assert.AreEqual(uTarget, uFoot, 1e-8);
        }

        private static GeoPoint DeCasteljau(GeoPoint[] p, double t)
        {
            GeoPoint[] q = (GeoPoint[])p.Clone();
            for (int r = 1; r < q.Length; r++)
                for (int i = 0; i < q.Length - r; i++)
                    q[i] = new GeoPoint((1 - t) * q[i].x + t * q[i + 1].x, (1 - t) * q[i].y + t * q[i + 1].y, (1 - t) * q[i].z + t * q[i + 1].z);
            return q[0];
        }
    }
}
