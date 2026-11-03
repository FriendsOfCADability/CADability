using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #515: boolean operations with an oblique frustum from <see cref="Make3D.MakeRuledSolid"/>, whose side faces are
    /// <see cref="RuledSurface"/>s. <see cref="RuledSurface"/> did not override <see cref="ISurface.GetDualSurfaceCurves"/>, so the
    /// intersection with a plane was the general interpolated curve, which stopped short of the end of one of the half faces. One
    /// intersection edge was missing, and <see cref="Solid.Intersect"/> and <see cref="Solid.Unite"/> gave nothing. The exact
    /// intersections of <see cref="RuledSurface.GetPlaneIntersection"/> are now used.
    /// </summary>
    /// <remarks>
    /// The frustum goes from the circle of radius 10 around the origin in z = 0 to the circle of radius 5 around (dx, 0, 10). The
    /// section at height z is a circle of radius 10 - z / 2, so a part between z0 and z1 has the volume
    /// π (z1 - z0) / 3 (R0² + R0 R1 + R1²), for the oblique frustum as for the straight one.
    /// </remarks>
    [TestClass]
    public class RuledFrustumBooleanTests
    {
        private static Solid Frustum(double dx)
        {
            Ellipse c1 = Ellipse.Construct();
            c1.SetCirclePlaneCenterRadius(Plane.XYPlane, GeoPoint.Origin, 10);
            Ellipse c2 = Ellipse.Construct();
            c2.SetCirclePlaneCenterRadius(new Plane(new GeoPoint(dx, 0, 10), GeoVector.ZAxis), new GeoPoint(dx, 0, 10), 5);
            GeoObject.Path p1 = GeoObject.Path.Construct();
            p1.Set(new ICurve[] { c1 });
            GeoObject.Path p2 = GeoObject.Path.Construct();
            p2.Set(new ICurve[] { c2 });
            Solid frustum = Make3D.MakeRuledSolid(p1, p2, null);
            Assert.IsNotNull(frustum);
            return frustum;
        }

        /// <summary>the volume of the frustum between the heights z0 and z1</summary>
        private static double Volume(double z0, double z1)
        {
            z0 = Math.Max(z0, 0);
            z1 = Math.Min(z1, 10);
            double r0 = 10 - z0 / 2, r1 = 10 - z1 / 2;
            return Math.PI * (z1 - z0) / 3 * (r0 * r0 + r0 * r1 + r1 * r1);
        }

        /// <summary>the box -20 &lt; x, y &lt; 20, z0 &lt; z &lt; z1</summary>
        private static Solid Slab(double z0, double z1) =>
            Make3D.MakeBox(new GeoPoint(-20, -20, z0), 40 * GeoVector.XAxis, 40 * GeoVector.YAxis, (z1 - z0) * GeoVector.ZAxis);

        private static double SlabVolume(double z0, double z1) => 1600 * (z1 - z0);

        private static double Sum(Solid[] solids)
        {
            foreach (Solid s in solids) AssertClosedAndConsistent(s);
            return solids.Sum(s => s.Volume(1e-4));
        }

        private static void AssertClosedAndConsistent(Solid solid)
        {
            foreach (Shell shell in solid.Shells)
            {
                Assert.AreEqual(0, shell.OpenEdgesExceptPoles.Length, "the result has open edges");
                Assert.IsTrue(shell.CheckConsistency(), "the result is not consistent");
            }
        }

        [DataTestMethod]
        [DataRow(2.0, DisplayName = "oblique")]
        [DataRow(0.0, DisplayName = "straight")]
        public void the_frustum_has_the_expected_volume(double dx)
        {
            Solid frustum = Frustum(dx);
            AssertClosedAndConsistent(frustum);
            Assert.AreEqual(Volume(0, 10), frustum.Volume(1e-4), 1e-3);
        }

        [TestMethod]
        public void the_oblique_frustum_has_ruled_side_faces()
        {
            Assert.AreEqual(2, Frustum(2).Shells[0].Faces.Count(f => f.Surface is RuledSurface));
        }

        /// <summary>the slab 4 &lt; z &lt; 6 cuts only the side faces, the slab -1 &lt; z &lt; 4 also contains the bottom face</summary>
        [DataTestMethod]
        [DataRow(2.0, 4.0, 6.0, DisplayName = "oblique, slab through the middle")]
        [DataRow(2.0, -1.0, 4.0, DisplayName = "oblique, slab over the bottom")]
        [DataRow(0.0, 4.0, 6.0, DisplayName = "straight, slab through the middle")]
        [DataRow(0.0, -1.0, 4.0, DisplayName = "straight, slab over the bottom")]
        public void intersect(double dx, double z0, double z1)
        {
            Solid[] common = Solid.Intersect(Frustum(dx), Slab(z0, z1));
            Assert.AreEqual(1, common.Length);
            Assert.AreEqual(Volume(z0, z1), Sum(common), 1e-3);
        }

        [DataTestMethod]
        [DataRow(2.0, 4.0, 6.0, DisplayName = "oblique, slab through the middle")]
        [DataRow(2.0, -1.0, 4.0, DisplayName = "oblique, slab over the bottom")]
        [DataRow(0.0, 4.0, 6.0, DisplayName = "straight, slab through the middle")]
        [DataRow(0.0, -1.0, 4.0, DisplayName = "straight, slab over the bottom")]
        public void unite(double dx, double z0, double z1)
        {
            Solid union = Solid.Unite(Frustum(dx), Slab(z0, z1));
            Assert.IsNotNull(union);
            AssertClosedAndConsistent(union);
            Assert.AreEqual(Volume(0, 10) + SlabVolume(z0, z1) - Volume(z0, z1), union.Volume(1e-4), 1e-2);
        }

        [DataTestMethod]
        [DataRow(2.0, 4.0, 6.0, DisplayName = "oblique, slab through the middle")]
        [DataRow(2.0, -1.0, 4.0, DisplayName = "oblique, slab over the bottom")]
        [DataRow(0.0, 4.0, 6.0, DisplayName = "straight, slab through the middle")]
        [DataRow(0.0, -1.0, 4.0, DisplayName = "straight, slab over the bottom")]
        public void subtract_the_slab(double dx, double z0, double z1)
        {
            Solid[] rest = Solid.Subtract(Frustum(dx), Slab(z0, z1));
            Assert.AreEqual(z0 > 0 ? 2 : 1, rest.Length);
            Assert.AreEqual(Volume(0, 10) - Volume(z0, z1), Sum(rest), 1e-3);
        }

        [DataTestMethod]
        [DataRow(2.0, 4.0, 6.0, DisplayName = "oblique, slab through the middle")]
        [DataRow(2.0, -1.0, 4.0, DisplayName = "oblique, slab over the bottom")]
        [DataRow(0.0, 4.0, 6.0, DisplayName = "straight, slab through the middle")]
        [DataRow(0.0, -1.0, 4.0, DisplayName = "straight, slab over the bottom")]
        public void subtract_the_frustum(double dx, double z0, double z1)
        {
            Solid[] rest = Solid.Subtract(Slab(z0, z1), Frustum(dx));
            Assert.AreEqual(1, rest.Length);
            Assert.AreEqual(SlabVolume(z0, z1) - Volume(z0, z1), Sum(rest), 1e-2);
        }

        private static GeoObject.Path PathOf(ICurve curve)
        {
            GeoObject.Path path = GeoObject.Path.Construct();
            path.Set(new ICurve[] { curve });
            return path;
        }

        /// <summary>
        /// Ruled solids from the circle of radius 10 in z = 0 to other tops, whose intersections with the planes of the slab are no
        /// circles or take other ways in <see cref="RuledSurface"/>: an ellipse (a curve of constant v that is no circle), a tilted
        /// circle (the general intersection) and a circle starting at another angle (the circle of constant v of arcs that start at
        /// different angles).
        /// </summary>
        private static Solid RuledSolid(string top)
        {
            Ellipse bottom = Ellipse.Construct();
            bottom.SetCirclePlaneCenterRadius(Plane.XYPlane, GeoPoint.Origin, 10);
            Ellipse upper = Ellipse.Construct();
            GeoPoint center = new GeoPoint(2, 0, 10);
            switch (top)
            {
                case "ellipse":
                    upper.SetEllipseCenterAxis(center, 6 * GeoVector.XAxis, 4 * GeoVector.YAxis);
                    break;
                case "tilted":
                    upper.SetCirclePlaneCenterRadius(new Plane(center, GeoVector.XAxis, new GeoVector(0, Math.Cos(0.3), Math.Sin(0.3))), center, 5);
                    break;
                case "rotated":
                    upper.SetCirclePlaneCenterRadius(new Plane(center, GeoVector.YAxis, -GeoVector.XAxis), center, 5);
                    break;
            }
            Solid solid = Make3D.MakeRuledSolid(PathOf(bottom), PathOf(upper), null);
            Assert.IsNotNull(solid);
            AssertClosedAndConsistent(solid);
            Assert.AreEqual(2, solid.Shells[0].Faces.Count(f => f.Surface is RuledSurface));
            return solid;
        }

        /// <summary>
        /// Without a closed formula for the volumes, the results must add up: the part inside and the part outside the slab make the
        /// solid, the union is the solid and the slab without their common part, and so on.
        /// </summary>
        [DataTestMethod]
        [DataRow("ellipse")]
        [DataRow("tilted")]
        [DataRow("rotated")]
        public void the_parts_of_other_ruled_solids_add_up(string top)
        {
            Solid solid = RuledSolid(top);
            double total = solid.Volume(1e-4);
            Solid slab = Slab(4, 6);
            double common = Sum(Solid.Intersect(solid, slab));
            Assert.IsTrue(common > 100, "the slab cuts the solid");
            Assert.AreEqual(total, Sum(Solid.Subtract(solid, slab)) + common, 1e-3);
            Assert.AreEqual(SlabVolume(4, 6), Sum(Solid.Subtract(slab, solid)) + common, 1e-3);
            Solid union = Solid.Unite(solid, slab);
            Assert.IsNotNull(union);
            AssertClosedAndConsistent(union);
            Assert.AreEqual(total + SlabVolume(4, 6) - common, union.Volume(1e-4), 1e-3);
        }

        /// <summary>
        /// The curve of constant v between a circle and an ellipse in parallel planes, which <see cref="RuledSurface.GetPlaneIntersection"/>
        /// returns, lies on both surfaces.
        /// </summary>
        [TestMethod]
        public void the_plane_intersection_between_a_circle_and_an_ellipse_lies_on_both_surfaces()
        {
            Ellipse c = Ellipse.Construct();
            c.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, GeoPoint.Origin, 10, 0, Math.PI);
            Ellipse e = Ellipse.Construct();
            e.SetEllipseArcCenterAxis(new GeoPoint(2, 0, 10), 6 * GeoVector.XAxis, 4 * GeoVector.YAxis, 0, Math.PI);
            RuledSurface ruled = new RuledSurface(c, e);
            // a plane with a scaled uv system like the faces of a box
            PlaneSurface pl = new PlaneSurface(new GeoPoint(-20, -20, 4), 40 * GeoVector.YAxis, 40 * GeoVector.XAxis, -GeoVector.ZAxis);
            IDualSurfaceCurve[] dscs = ruled.GetPlaneIntersection(pl, 0, 1, 0, 1, Precision.eps);
            Assert.AreEqual(1, dscs.Length);
            for (int i = 0; i <= 10; i++)
            {
                GeoPoint p = dscs[0].Curve3D.PointAt(i / 10.0);
                Assert.AreEqual(4.0, p.z, 1e-9, "the curve lies in the plane");
                Assert.AreEqual(0.0, dscs[0].Surface1.PointAt(dscs[0].Curve2D1.PointAt(i / 10.0)) | p, 1e-6, "the curve on the ruled surface is wrong");
                Assert.AreEqual(0.0, dscs[0].Surface2.PointAt(dscs[0].Curve2D2.PointAt(i / 10.0)) | p, 1e-6, "the curve on the plane is wrong");
            }
        }
    }
}
