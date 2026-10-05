using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Unrolling contours on a cylinder (the wall of a round tube): a planar cut through the tube - e.g. a miter cut at its
    /// end - is an ellipse in 3d and a sine curve with the period 2*pi in the (u,v) system of the cylinder. Ported from
    /// ShapeIt: the sine fit (SineCurve2D.Create), the exact way back from the sine curve to the ellipse
    /// (CylindricalSurface.Make3dCurve) and the helix for a slanted line (HelicalCurve).
    /// </summary>
    [TestClass]
    public class CylinderUnrollingTests
    {
        private const double r = 20.0;

        private static CylindricalSurface Cylinder(bool reversed = false)
        {
            CylindricalSurface cyl = new CylindricalSurface(new GeoPoint(10, -5, 3), r * GeoVector.XAxis, r * GeoVector.YAxis, GeoVector.ZAxis);
            if (reversed) cyl.ReverseOrientation();
            return cyl;
        }

        /// <summary>The section of the cylinder with a plane tilted by <paramref name="degrees"/> against the cross section.</summary>
        private static Ellipse MiterCut(CylindricalSurface cyl, double degrees)
        {
            double a = degrees * Math.PI / 180.0;
            Plane cut = new Plane(cyl.Location + 50 * GeoVector.ZAxis, new GeoVector(Math.Cos(a), 0, Math.Sin(a)), GeoVector.YAxis);
            PlaneSurface ps = new PlaneSurface(cut);
            IDualSurfaceCurve[] dsc = cyl.GetPlaneIntersection(ps, 0, 2 * Math.PI, -100, 200, 0.0);
            Assert.AreEqual(1, dsc.Length);
            Assert.IsInstanceOfType(dsc[0].Curve3D, typeof(Ellipse));
            return (Ellipse)dsc[0].Curve3D;
        }

        private static void AssertSameCurve(ICurve expected, ICurve actual, double tol = 1e-8)
        {
            for (int i = 0; i <= 12; i++)
                Assert.IsTrue((expected.PointAt(i / 12.0) | actual.PointAt(i / 12.0)) < tol, $"at {i / 12.0}: {expected.PointAt(i / 12.0)} and {actual.PointAt(i / 12.0)}");
        }

        [DataTestMethod]
        [DataRow(30.0, false)]
        [DataRow(45.0, false)]
        [DataRow(45.0, true)]
        [DataRow(-20.0, false)]
        public void a_full_miter_cut_unrolls_to_one_period_of_a_sine_curve(double degrees, bool reversed)
        {
            CylindricalSurface cyl = Cylinder(reversed);
            Ellipse cut = MiterCut(cyl, degrees);
            Assert.IsTrue(cut.IsClosed);
            ICurve2D c2d = cyl.GetProjectedCurve(cut, 0.0);
            Assert.IsInstanceOfType(c2d, typeof(SineCurve2D), "the closed ellipse must not end up as an approximated ProjectedCurve");
            Assert.AreEqual(2 * Math.PI, Math.Abs(c2d.EndPoint.x - c2d.StartPoint.x), 1e-8, "one full turn");
            for (int i = 0; i <= 12; i++)
                Assert.IsTrue((cyl.PointAt(c2d.PointAt(i / 12.0)) | (cut as ICurve).PointAt(i / 12.0)) < 1e-6, $"at {i / 12.0}");
            // and back: the sine curve is an exact ellipse again
            ICurve back = cyl.Make3dCurve(c2d);
            Assert.IsInstanceOfType(back, typeof(Ellipse));
            AssertSameCurve(cut, back, 1e-6);
            // the amplitude of the sine curve is r*tan(angle), the height of the cut along the tube
            BoundingRect ext = c2d.GetExtent();
            Assert.AreEqual(2 * r * Math.Abs(Math.Tan(degrees * Math.PI / 180.0)), ext.Height, 1e-6);
        }

        [TestMethod]
        public void an_arc_of_a_miter_cut_across_the_seam()
        {
            CylindricalSurface cyl = Cylinder();
            Ellipse cut = MiterCut(cyl, 30.0);
            // the part from 300 to 420 degrees around the axis, i.e. across the seam at u == 0
            GeoPoint2D uvStart = cyl.PositionOf(cyl.PointAt(new GeoPoint2D(300 * Math.PI / 180, 0)));
            Ellipse arc = cut.Clone() as Ellipse;
            double p1 = (cut as ICurve).PositionOf(cut.Center + 100 * new GeoVector(Math.Cos(300 * Math.PI / 180), Math.Sin(300 * Math.PI / 180), 0));
            double p2 = (cut as ICurve).PositionOf(cut.Center + 100 * new GeoVector(Math.Cos(60 * Math.PI / 180), Math.Sin(60 * Math.PI / 180), 0));
            // the curve of the cut runs from p1 over the seam to p2, maybe in the other direction
            if (p1 < p2) arc.Trim(p1, p2); else { (arc as ICurve).Reverse(); arc.Trim(1 - p1, 1 - p2); }
            ICurve2D c2d = cyl.GetProjectedCurve(arc, 0.0);
            Assert.IsInstanceOfType(c2d, typeof(SineCurve2D));
            for (int i = 0; i <= 12; i++)
                Assert.IsTrue((cyl.PointAt(c2d.PointAt(i / 12.0)) | (arc as ICurve).PointAt(i / 12.0)) < 1e-6, $"at {i / 12.0}");
            AssertSameCurve(arc, cyl.Make3dCurve(c2d), 1e-6);
        }

        [TestMethod]
        public void a_sine_curve_on_the_cylinder_is_an_exact_ellipse()
        {
            CylindricalSurface cyl = Cylinder();
            // v == 7*sin(u-0.4)+30, from u == 1 over 2.5 radians
            SineCurve2D sine = new SineCurve2D(1.0 - 0.4, 2.5, new ModOp2D(1, 0, 0.4, 0, 7, 30));
            ICurve c3d = cyl.Make3dCurve(sine);
            Assert.IsInstanceOfType(c3d, typeof(Ellipse));
            for (int i = 0; i <= 12; i++)
                Assert.IsTrue((cyl.PointAt(sine.PointAt(i / 12.0)) | c3d.PointAt(i / 12.0)) < 1e-9);
            Assert.AreEqual(PlanarState.Planar, c3d.GetPlanarState());
        }

        [TestMethod]
        public void a_slanted_line_on_the_cylinder_is_a_helix()
        {
            CylindricalSurface cyl = Cylinder();
            Line2D l2d = new Line2D(new GeoPoint2D(0.5, 10), new GeoPoint2D(0.5 + 3 * Math.PI, 40));
            ICurve c3d = cyl.Make3dCurve(l2d);
            Assert.IsInstanceOfType(c3d, typeof(HelicalCurve));
            for (int i = 0; i <= 12; i++)
                Assert.IsTrue((cyl.PointAt(l2d.PointAt(i / 12.0)) | c3d.PointAt(i / 12.0)) < 1e-9);
            // the length of a helix is the length of the unrolled line: the hypotenuse of the arc length and the height
            Assert.AreEqual(Math.Sqrt(Math.Pow(3 * Math.PI * r, 2) + 30 * 30), c3d.Length, 1e-6);
        }

        [TestMethod]
        public void sine_curve_through_four_points()
        {
            Func<double, GeoPoint2D> f = x => new GeoPoint2D(x, 3.0 * Math.Sin(x - 0.7) + 2.0);
            SineCurve2D sc = SineCurve2D.Create(f(1.0), f(2.0), f(3.5), f(5.0));
            Assert.IsNotNull(sc);
            Assert.IsTrue((sc.StartPoint | f(1.0)) < 1e-9 && (sc.EndPoint | f(5.0)) < 1e-9);
            for (int i = 0; i <= 10; i++)
            {
                GeoPoint2D p = sc.PointAt(i / 10.0);
                Assert.AreEqual(f(p.x).y, p.y, 1e-9);
            }
            Assert.IsNull(SineCurve2D.Create(f(1.0), f(2.0), f(3.0), f(1.0)), "start and end at the same x");
        }
    }
}
