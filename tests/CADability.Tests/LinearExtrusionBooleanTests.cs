using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #513: boolean operations with a prism whose profile contains a BSpline, so that one side face is a
    /// <see cref="SurfaceOfLinearExtrusion"/>, gave wrong volumes, even negative ones, where the other solid cut that face.
    /// <see cref="SurfaceOfLinearExtrusion.GetPlaneIntersection"/> projected the intersection curve into the coordinate
    /// system of the <see cref="Plane"/> of the <see cref="PlaneSurface"/> instead of into the uv system of the surface. The
    /// faces of <see cref="Make3D.MakeBox"/> have a scaled and swapped uv system, so the 2d curves of the new edges on the
    /// plane faces lay somewhere else entirely. <see cref="RuledSurface.GetPlaneIntersection"/> had the same mistake.
    /// </summary>
    [TestClass]
    public class LinearExtrusionBooleanTests
    {
        /// <summary>the wavy top side of the profile, from (30, 30) to (0, 30)</summary>
        private static BSpline2D Wave() => new BSpline2D(new[] { new GeoPoint2D(30, 30), new GeoPoint2D(20, 34), new GeoPoint2D(10, 27), new GeoPoint2D(0, 30) }, 3, false);

        /// <summary>the square 30 x 30 whose top side is <see cref="Wave"/>, extruded by 50 in z</summary>
        private static Solid WavyPrism()
        {
            Border outline = new Border(new ICurve2D[] {
                new Line2D(new GeoPoint2D(0, 0), new GeoPoint2D(30, 0)),
                new Line2D(new GeoPoint2D(30, 0), new GeoPoint2D(30, 30)),
                Wave(),
                new Line2D(new GeoPoint2D(0, 30), new GeoPoint2D(0, 0)) });
            Solid prism = Make3D.MakePrism(outline.MakePath(Plane.XYPlane), 50 * GeoVector.ZAxis, null, false) as Solid;
            Assert.IsNotNull(prism);
            Assert.AreEqual(1, prism.Shells[0].Faces.Count(f => f.Surface is SurfaceOfLinearExtrusion), "the bspline should give a SurfaceOfLinearExtrusion");
            return prism;
        }

        /// <summary>
        /// The area of the profile between <paramref name="xmin"/> and <paramref name="xmax"/>, the area below the wave, computed
        /// independently of CADability's area calculation with the trapezoidal rule on a fine sampling of the bspline.
        /// </summary>
        private static double ProfileArea(double xmin, double xmax)
        {
            BSpline2D wave = Wave();
            const int n = 200000;
            double area = 0.0;
            GeoPoint2D last = wave.PointAt(0.0);
            for (int i = 1; i <= n; i++)
            {
                GeoPoint2D p = wave.PointAt((double)i / n);
                // the wave runs from right to left; clip the segment to [xmin, xmax]
                double x0 = Math.Min(Math.Max(last.x, xmin), xmax);
                double x1 = Math.Min(Math.Max(p.x, xmin), xmax);
                area += (x0 - x1) * (last.y + p.y) / 2.0;
                last = p;
            }
            return area;
        }

        private static double Volume(Solid[] solids) => solids.Sum(s => s.Volume(1e-3));

        private static void AssertConsistent(Solid[] solids)
        {
            foreach (Solid s in solids)
            {
                foreach (Shell sh in s.Shells)
                {
                    Assert.AreEqual(0, sh.OpenEdgesExceptPoles.Length, "the result has open edges");
                    Assert.IsTrue(sh.CheckConsistency(), "the result is not consistent");
                }
            }
        }

        [TestMethod]
        public void the_prism_has_the_volume_of_the_profile()
        {
            Assert.AreEqual(50 * ProfileArea(0, 30), WavyPrism().Volume(1e-3), 0.5);
        }

        [TestMethod]
        public void a_slab_across_the_whole_prism_is_subtracted()
        {
            Solid prism = WavyPrism();
            Solid slab = Make3D.MakeBox(new GeoPoint(-10, -10, 20), 60 * GeoVector.XAxis, 60 * GeoVector.YAxis, 5 * GeoVector.ZAxis);
            Solid[] rest = Solid.Subtract(prism, slab);
            Assert.AreEqual(2, rest.Length);
            AssertConsistent(rest);
            Assert.AreEqual(45 * ProfileArea(0, 30), Volume(rest), 0.5);
        }

        [TestMethod]
        public void a_slab_across_the_whole_prism_is_intersected()
        {
            Solid prism = WavyPrism();
            Solid slab = Make3D.MakeBox(new GeoPoint(-10, -10, 20), 60 * GeoVector.XAxis, 60 * GeoVector.YAxis, 5 * GeoVector.ZAxis);
            Solid[] common = Solid.Intersect(prism, slab);
            Assert.AreEqual(1, common.Length);
            AssertConsistent(common);
            Assert.AreEqual(5 * ProfileArea(0, 30), Volume(common), 0.1);
        }

        /// <summary>the slab ends below the wave and cuts only plane faces; this was already right</summary>
        [TestMethod]
        public void a_slab_below_the_bspline_is_subtracted()
        {
            Solid prism = WavyPrism();
            Solid below = Make3D.MakeBox(new GeoPoint(-10, -10, 20), 60 * GeoVector.XAxis, 35 * GeoVector.YAxis, 5 * GeoVector.ZAxis);
            Solid[] rest = Solid.Subtract(prism, below);
            Assert.AreEqual(1, rest.Length);
            AssertConsistent(rest);
            Assert.AreEqual(50 * ProfileArea(0, 30) - 5 * 30 * 25, Volume(rest), 0.5);
        }

        /// <summary>the box crosses the bspline face for 5 &lt; x &lt; 15, its side planes x = 5 and x = 15 are parallel to the extrusion</summary>
        [TestMethod]
        public void a_box_crossing_the_bspline_face_is_subtracted()
        {
            Solid prism = WavyPrism();
            Solid part = Make3D.MakeBox(new GeoPoint(5, -10, 20), 10 * GeoVector.XAxis, 60 * GeoVector.YAxis, 5 * GeoVector.ZAxis);
            Solid[] rest = Solid.Subtract(prism, part);
            Assert.AreEqual(1, rest.Length);
            AssertConsistent(rest);
            Assert.AreEqual(50 * ProfileArea(0, 30) - 5 * ProfileArea(5, 15), Volume(rest), 0.5);
        }

        [TestMethod]
        public void a_box_crossing_the_bspline_face_is_intersected()
        {
            Solid prism = WavyPrism();
            Solid part = Make3D.MakeBox(new GeoPoint(5, -10, 20), 10 * GeoVector.XAxis, 60 * GeoVector.YAxis, 5 * GeoVector.ZAxis);
            Solid[] common = Solid.Intersect(prism, part);
            Assert.AreEqual(1, common.Length);
            AssertConsistent(common);
            Assert.AreEqual(5 * ProfileArea(5, 15), Volume(common), 0.05);
        }

        /// <summary>
        /// Checks that both 2d curves of every intersection curve describe the 3d curve on their surfaces.
        /// </summary>
        private static void AssertCurvesOnSurfaces(IDualSurfaceCurve[] dscs)
        {
            Assert.IsTrue(dscs.Length > 0, "no intersection curve");
            foreach (IDualSurfaceCurve dsc in dscs)
            {
                for (int i = 0; i <= 10; i++)
                {
                    GeoPoint p = dsc.Curve3D.PointAt(i / 10.0);
                    Assert.AreEqual(0.0, dsc.Surface1.PointAt(dsc.Curve2D1.PointAt(i / 10.0)) | p, 1e-6, "the curve on the first surface is wrong");
                    Assert.AreEqual(0.0, dsc.Surface2.PointAt(dsc.Curve2D2.PointAt(i / 10.0)) | p, 1e-6, "the curve on the plane is wrong");
                }
            }
        }

        /// <summary>a plane surface whose uv system is scaled and swapped against its plane, like the faces of a box</summary>
        private static PlaneSurface ScaledPlane(GeoPoint location, GeoVector u, GeoVector v) => new PlaneSurface(location, 60 * u, 40 * v, u ^ v);

        [TestMethod]
        public void the_plane_intersection_of_a_linear_extrusion_lies_on_a_scaled_plane()
        {
            SurfaceOfLinearExtrusion sle = WavyPrism().Shells[0].Faces.First(f => f.Surface is SurfaceOfLinearExtrusion).Surface as SurfaceOfLinearExtrusion;
            // perpendicular to the extrusion: the moved basis curve
            AssertCurvesOnSurfaces(sle.GetPlaneIntersection(ScaledPlane(new GeoPoint(-10, -10, 20), GeoVector.YAxis, GeoVector.XAxis), 0, 1, 0, 1, Precision.eps));
            // parallel to the extrusion: lines
            AssertCurvesOnSurfaces(sle.GetPlaneIntersection(ScaledPlane(new GeoPoint(10, -10, -10), GeoVector.ZAxis, GeoVector.YAxis), 0, 1, 0, 1, Precision.eps));
        }

        [TestMethod]
        public void the_plane_intersection_of_a_ruled_surface_lies_on_a_scaled_plane()
        {
            // two circles in parallel planes: the intersection with a parallel plane is a circle
            Ellipse c1 = Ellipse.Construct();
            c1.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, GeoPoint.Origin, 10, 0, Math.PI);
            Ellipse c2 = Ellipse.Construct();
            c2.SetArcPlaneCenterRadiusAngles(new Plane(new GeoPoint(0, 0, 10), GeoVector.ZAxis), new GeoPoint(0, 0, 10), 5, 0, Math.PI);
            RuledSurface circles = new RuledSurface(c1, c2);
            AssertCurvesOnSurfaces(circles.GetPlaneIntersection(ScaledPlane(new GeoPoint(-20, -20, 4), GeoVector.YAxis, GeoVector.XAxis), 0, 1, 0, 1, Precision.eps));
            // two bsplines in parallel planes: the intersection with a parallel plane is a curve between them
            BSpline b1 = BSpline.Construct();
            b1.ThroughPoints(new[] { new GeoPoint(0, 0, 0), new GeoPoint(10, 5, 0), new GeoPoint(20, -3, 0), new GeoPoint(30, 0, 0) }, 3, false);
            BSpline b2 = BSpline.Construct();
            b2.ThroughPoints(new[] { new GeoPoint(0, 10, 10), new GeoPoint(10, 12, 10), new GeoPoint(20, 8, 10), new GeoPoint(30, 10, 10) }, 3, false);
            RuledSurface splines = new RuledSurface(b1, b2);
            AssertCurvesOnSurfaces(splines.GetPlaneIntersection(ScaledPlane(new GeoPoint(-20, -20, 4), GeoVector.YAxis, GeoVector.XAxis), 0, 1, 0, 1, Precision.eps));
        }
    }
}
