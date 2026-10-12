using CADability.GeoObject;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// The analysis functions ported from ShapeIt, applied to what they are needed for here: recognizing tubes read
    /// from STEP files. Round, rectangular and bent tubes, also with holes in the wall, are built and checked for
    /// axis, wall thickness, genus (number of through holes), edge classification, principal axis and the exact
    /// volume.
    /// </summary>
    [TestClass]
    public class TubeAnalysisTests
    {
        private const double ro = 20.0, ri = 18.0, length = 100.0;

        private static Solid RoundTube()
        {
            SimpleShape ring = new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, ro), Border.MakeCircle(GeoPoint2D.Origin, ri));
            return Make3D.MakePrism(Face.MakeFace(new PlaneSurface(Plane.XYPlane), ring), new GeoVector(0, 0, length), null) as Solid;
        }

        private static Solid RectangularTube(double w, double h, double t)
        {
            SimpleShape frame = new SimpleShape(Border.MakeRectangle(-w / 2, w / 2, -h / 2, h / 2), Border.MakeRectangle(-w / 2 + t, w / 2 - t, -h / 2 + t, h / 2 - t));
            return Make3D.MakePrism(Face.MakeFace(new PlaneSurface(Plane.XYPlane), frame), new GeoVector(0, 0, length), null) as Solid;
        }

        /// <summary>The round tube rotated and moved far away from the origin, as it would sit in an assembly.</summary>
        private static Solid PlacedRoundTube(out ModOp placement)
        {
            Solid tube = RoundTube();
            placement = ModOp.Translate(5000, -3000, 1200) * ModOp.Rotate(new GeoVector(1, 2, 3).Normalized, SweepAngle.Deg(40));
            tube.Modify(placement);
            return tube;
        }

        [TestMethod]
        public void inner_and_outer_wall_of_a_round_tube_share_the_axis()
        {
            Solid tube = PlacedRoundTube(out ModOp placement);
            List<CylindricalSurface> walls = tube.Shells[0].Faces.Select(f => f.Surface).OfType<CylindricalSurface>().ToList();
            Assert.IsTrue(walls.Count >= 2);
            double rmin = walls.Min(c => c.RadiusX), rmax = walls.Max(c => c.RadiusX);
            Assert.AreEqual(ri, rmin, 1e-9);
            Assert.AreEqual(ro, rmax, 1e-9);
            CylindricalSurface outer = walls.First(c => Math.Abs(c.RadiusX - ro) < 1e-9);
            CylindricalSurface inner = walls.First(c => Math.Abs(c.RadiusX - ri) < 1e-9);
            Assert.IsTrue(Surfaces.GetCommonAxis(outer, inner, out Axis axis));
            Assert.IsTrue(Precision.SameDirection(axis.Direction, placement * GeoVector.ZAxis, false));
            Assert.IsTrue(axis.Distance(placement * GeoPoint.Origin) < 1e-8);
            Assert.AreEqual(ro - ri, Math.Abs(outer.RadiusX - inner.RadiusX), 1e-9, "the wall thickness");
            // a cylinder on another axis does not share it
            CylindricalSurface other = new CylindricalSurface(placement * new GeoPoint(1, 0, 0), ro * (placement * GeoVector.XAxis), ro * (placement * GeoVector.YAxis), placement * GeoVector.ZAxis);
            Assert.IsFalse(Surfaces.GetCommonAxis(outer, other, out _));
        }

        [TestMethod]
        public void torus_and_cylinder_on_the_same_axis()
        {
            ToroidalSurface torus = new ToroidalSurface(new GeoPoint(1, 2, 3), GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, 50, 10);
            CylindricalSurface cyl = new CylindricalSurface(new GeoPoint(1, 2, 30), 40 * GeoVector.XAxis, 40 * GeoVector.YAxis, GeoVector.ZAxis);
            Assert.IsTrue(Surfaces.GetCommonAxis(torus, cyl, out Axis axis));
            // they meet in the circles where the cylinder of radius 40 touches the tube of the torus: z == 3 (tangential)
            IDualSurfaceCurve[] circles = Surfaces.IntersectOnCommonAxis(torus, new BoundingRect(0, 0, 2 * Math.PI, 2 * Math.PI), cyl, new BoundingRect(0, -50, 2 * Math.PI, 50));
            Assert.IsNotNull(circles);
            Assert.AreEqual(1, circles.Length);
            Assert.IsTrue(circles[0].Curve3D is Ellipse e && Math.Abs(e.Radius - 40) < 1e-8 && Math.Abs(e.Center.z - 3) < 1e-8);
        }

        [TestMethod]
        public void wall_thickness_of_a_rectangular_tube()
        {
            Solid tube = RectangularTube(60, 40, 3);
            Shell shell = tube.Shells[0];
            Face outerSide = shell.Faces.First(f => f.Surface is PlaneSurface ps && Math.Abs(ps.Location.x - 30) < 1e-9 && Precision.SameDirection(ps.Normal, GeoVector.XAxis, false));
            int n = shell.GetFaceDistances(outerSide, GeoPoint.Invalid, out List<Face> faces, out List<double> distances, out _, out _);
            Assert.IsTrue(n >= 2);
            Assert.IsTrue(distances.Any(d => Math.Abs(d - 3.0) < 1e-9), "the inner wall at a distance of the wall thickness");
            Assert.IsTrue(distances.Any(d => Math.Abs(d - 60.0) < 1e-9), "the opposite outer wall at the width of the tube");
        }

        [TestMethod]
        public void edges_of_a_tube_are_convex_outside_and_concave_inside()
        {
            // all edges at the ends and the outer longitudinal corners have the material inside a 90 degree angle (convex),
            // the four longitudinal corners of the inner hollow have it outside (concave)
            Solid tube = RectangularTube(60, 40, 3);
            int concave = 0;
            foreach (Edge edge in tube.Shells[0].Edges)
            {
                ShellExtensions.AdjacencyType adj = edge.Adjacency();
                GeoPoint m = edge.Curve3D.PointAt(0.5);
                bool innerCorner = Math.Abs(Math.Abs(m.x) - 27) < 1e-9 && Math.Abs(Math.Abs(m.y) - 17) < 1e-9;
                Assert.AreEqual(innerCorner ? ShellExtensions.AdjacencyType.Concave : ShellExtensions.AdjacencyType.Convex, adj, $"edge at {m}");
                if (innerCorner) ++concave;
            }
            Assert.AreEqual(4, concave);
            Assert.IsTrue(tube.Shells[0].Faces.Any(f => f.AllEdgesAreConvex(false)), "the end faces and outer walls");
        }

        [TestMethod]
        public void genus_counts_the_through_holes()
        {
            Assert.AreEqual(0, Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis).Shells[0].Genus);
            Assert.AreEqual(1, RoundTube().Shells[0].Genus, "a tube");
            Assert.AreEqual(1, RectangularTube(60, 40, 3).Shells[0].Genus, "a rectangular tube");
            // a rectangular tube with two round holes through one wall
            Solid tube = RectangularTube(60, 40, 3);
            foreach (double z in new double[] { 30, 70 })
            {
                Solid drill = Make3D.MakeCylinder(new GeoPoint(0, 10, z), 5 * GeoVector.XAxis, 20 * GeoVector.YAxis);
                Solid[] res = Solid.Subtract(tube, drill);
                Assert.AreEqual(1, res.Length);
                tube = res[0];
            }
            Assert.AreEqual(3, tube.Shells[0].Genus, "a tube with two holes in the wall");
        }

        [TestMethod]
        public void hull_and_holes_of_a_closed_body_with_a_cavity()
        {
            Shell shell = Make3D.MakeBox(GeoPoint.Origin, 40 * GeoVector.XAxis, 40 * GeoVector.YAxis, 40 * GeoVector.ZAxis).Shells[0];
            Shell cavity = Make3D.MakeBox(new GeoPoint(10, 10, 10), 5 * GeoVector.XAxis, 5 * GeoVector.YAxis, 5 * GeoVector.ZAxis).Shells[0];
            cavity.ReverseOrientation();
            shell.AddInnerHole(cavity.Faces);
            (HashSet<Face> hull, HashSet<Face>[] holes) = shell.GetHullAndHoles();
            Assert.AreEqual(6, hull.Count);
            Assert.AreEqual(1, holes.Length);
            Assert.AreEqual(-125.0, Shell.SignedVolume(holes[0], 0.01), 1e-9);
            Assert.AreEqual(64000.0 - 125.0, shell.Volume(0.01), 1e-6);
        }

        [TestMethod]
        public void the_principal_axis_of_inertia_is_the_tube_axis()
        {
            foreach (Solid tube in new Solid[] { PlacedRoundTube(out ModOp placement), RectangularTube(60, 40, 3) })
            {
                Shell shell = tube.Shells[0];
                shell.GetMassProperties(0.01, out double volume, out GeoPoint cog, out double[,] tensor);
                Assert.IsTrue(volume > 0);
                // the tube axis is the eigenvector of the smallest moment of inertia (a long thin body)
                var m = MathNet.Numerics.LinearAlgebra.Double.DenseMatrix.OfArray(tensor);
                var evd = m.Evd(MathNet.Numerics.LinearAlgebra.Symmetricity.Symmetric);
                int imin = 0;
                for (int i = 1; i < 3; i++) if (evd.EigenValues[i].Real < evd.EigenValues[imin].Real) imin = i;
                GeoVector axis = new GeoVector(evd.EigenVectors[0, imin], evd.EigenVectors[1, imin], evd.EigenVectors[2, imin]);
                GeoVector expected = tube.Shells[0].Faces.Any(f => f.Surface is CylindricalSurface) ? placement * GeoVector.ZAxis : GeoVector.ZAxis;
                Assert.IsTrue(new Angle(axis, expected).Radian < 1e-5 || new Angle(-axis, expected).Radian < 1e-5, $"axis {axis} instead of {expected}");
                GeoPoint expectedCog = tube.Shells[0].Faces.Any(f => f.Surface is CylindricalSurface) ? placement * new GeoPoint(0, 0, length / 2) : new GeoPoint(0, 0, length / 2);
                Assert.IsTrue((cog | expectedCog) < 1e-3, $"center of gravity {cog} instead of {expectedCog}");
                Assert.IsTrue((shell.Centroid(0.01) | cog) < 1e-9);
            }
        }

        [TestMethod]
        public void the_volume_of_a_tube_far_from_the_origin_is_exact()
        {
            Solid tube = PlacedRoundTube(out _);
            double expected = Math.PI * (ro * ro - ri * ri) * length;
            Assert.AreEqual(expected, ShellMetrics.IntegratedVolume(tube.Shells[0]), expected * 1e-9);
            // the area: outer and inner wall, two rings
            double area = 2 * Math.PI * (ro + ri) * length + 2 * Math.PI * (ro * ro - ri * ri);
            Assert.AreEqual(area, ShellMetrics.SurfaceArea(tube.Shells[0]), area * 1e-9);
            // the triangulated volume, from the center of the tube instead of the origin
            GeoPoint center = tube.Shells[0].GetExtent(0.0).GetCenter();
            Assert.AreEqual(expected, Shell.SignedVolume(tube.Shells[0].Faces, 0.001, center), expected * 1e-4);
        }

        [TestMethod]
        public void parallel_distance_between_a_plane_and_a_cylinder()
        {
            PlaneSurface pl = new PlaneSurface(Plane.XYPlane);
            CylindricalSurface cyl = new CylindricalSurface(new GeoPoint(0, 0, 10), 4 * GeoVector.XAxis, 4 * GeoVector.ZAxis, GeoVector.YAxis); // axis along y, 10 above the plane
            Assert.IsTrue(Surfaces.ParallelDistance(pl, new BoundingRect(-20, -20, 20, 20), cyl, new BoundingRect(0, -20, 2 * Math.PI, 20), GeoPoint.Invalid, out GeoPoint2D uv1, out GeoPoint2D uv2));
            Assert.AreEqual(6.0, pl.PointAt(uv1) | cyl.PointAt(uv2), 1e-9, "from the plane to the near side of the cylinder");
        }
    }
}
