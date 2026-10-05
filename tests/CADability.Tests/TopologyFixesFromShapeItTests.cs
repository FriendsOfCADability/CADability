using CADability.GeoObject;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// Fixes of faces, edges, vertices, shells and solids, ported from the ShapeIt fork of CADability, and STEP round
    /// trips of tube bodies, which is what these fixes are needed for: the bodies are exported to STEP, read back, and
    /// must come back as consistent, closed solids with the right volume.
    /// </summary>
    [TestClass]
    public class TopologyFixesFromShapeItTests
    {
        public TestContext TestContext { get; set; }

        private const double outerRadius = 20.0, wall = 2.0, length = 100.0;

        /// <summary>A planar ring in the xy-plane: the cross section of a round tube.</summary>
        private static Face Annulus(double ro, double ri)
        {
            SimpleShape ring = new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, ro), Border.MakeCircle(GeoPoint2D.Origin, ri));
            return Face.MakeFace(new PlaneSurface(Plane.XYPlane), ring);
        }

        /// <summary>A rectangular frame in the xy-plane: the cross section of a rectangular tube.</summary>
        private static Face Frame(double w, double h, double t)
        {
            SimpleShape frame = new SimpleShape(Border.MakeRectangle(-w / 2, w / 2, -h / 2, h / 2), Border.MakeRectangle(-w / 2 + t, w / 2 - t, -h / 2 + t, h / 2 - t));
            return Face.MakeFace(new PlaneSurface(Plane.XYPlane), frame);
        }

        private static Solid RoundTube() => Make3D.MakePrism(Annulus(outerRadius, outerRadius - wall), new GeoVector(0, 0, length), null) as Solid;
        private static Solid RectangularTube() => Make3D.MakePrism(Frame(60, 40, 3), new GeoVector(0, 0, length), null) as Solid;
        /// <summary>
        /// A 90 degree bend with bend radius 50 around the z-axis, made of the faces a STEP file would contain: two toroidal
        /// faces (outer and inner wall) and two planar rings at the ends. (Make3D.Rotate ignores the holes of a face, so it
        /// cannot make this body from a ring.) The outer wall is split into two halves, because the planar ring has its
        /// outer circle split into two arcs at the same points.
        /// </summary>
        private static Solid BentTube()
        {
            double ri = outerRadius - wall;
            ToroidalSurface outer = new ToroidalSurface(GeoPoint.Origin, GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, 50, outerRadius);
            ToroidalSurface inner = new ToroidalSurface(GeoPoint.Origin, GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, 50, ri);
            ModOp2D m = inner.ReverseOrientation(); // the inner wall faces towards the axis of the tube
            BoundingRect innerArea = new BoundingRect(0, 0, Math.PI / 2, 2 * Math.PI);
            innerArea.Modify(m);
            Face outer1 = Face.MakeFace(outer, new SimpleShape(Border.MakeRectangle(0, Math.PI / 2, 0, Math.PI)));
            Face outer2 = Face.MakeFace(outer, new SimpleShape(Border.MakeRectangle(0, Math.PI / 2, Math.PI, 2 * Math.PI)));
            Face innerWall = Face.MakeFace(inner, new SimpleShape(Border.MakeRectangle(innerArea.Left, innerArea.Right, innerArea.Bottom, innerArea.Top)));
            SimpleShape ring = new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, outerRadius), Border.MakeCircle(GeoPoint2D.Origin, ri));
            // the planes are oriented outward (-y at u == 0, -x at u == pi/2), the circles start where the torus has v == 0
            Face end0 = Face.MakeFace(new PlaneSurface(new Plane(new GeoPoint(50, 0, 0), GeoVector.XAxis, GeoVector.ZAxis)), ring);
            Face end1 = Face.MakeFace(new PlaneSurface(new Plane(new GeoPoint(0, 50, 0), GeoVector.YAxis, -GeoVector.ZAxis)), ring);
            Shell[] shells = Make3D.SewFaces(new Face[] { outer1, outer2, innerWall, end0, end1 });
            Assert.AreEqual(1, shells.Length);
            return Solid.MakeSolid(shells[0]);
        }

        private static double RoundTubeVolume => Math.PI * (outerRadius * outerRadius - (outerRadius - wall) * (outerRadius - wall)) * length;
        private static double RectangularTubeVolume => (60 * 40 - 54 * 34) * length;
        private static double BentTubeVolume => Math.PI * (outerRadius * outerRadius - (outerRadius - wall) * (outerRadius - wall)) * 50 * Math.PI / 2; // Pappus

        [TestMethod]
        public void the_test_bodies_are_valid()
        {
            foreach ((Solid sld, double vol) in new[] { (RoundTube(), RoundTubeVolume), (RectangularTube(), RectangularTubeVolume), (BentTube(), BentTubeVolume) })
            {
                Assert.IsNotNull(sld);
                Assert.AreEqual(vol, sld.Volume(0.01), vol * 1e-3);
                Assert.AreEqual(0, sld.Shells[0].OpenEdgesExceptPoles.Length);
                foreach (Face fc in sld.Shells[0].Faces) Assert.IsTrue(fc.CheckConsistency(), "inconsistent face of the constructed body");
            }
        }

        [TestMethod]
        public void a_mirrored_solid_stays_outward_oriented()
        {
            Solid tube = RoundTube();
            tube.Modify(ModOp.ReflectPlane(new Plane(new GeoPoint(5, 0, 0), GeoVector.XAxis)));
            Assert.AreEqual(RoundTubeVolume, tube.Volume(0.01), RoundTubeVolume * 1e-3, "a mirrored STEP placement must not turn the solid inside out");
            foreach (Face fc in tube.Shells[0].Faces) Assert.IsTrue(fc.CheckConsistency());
        }

        [TestMethod]
        public void reversing_a_shell_does_not_reuse_the_old_triangulation()
        {
            Shell shell = RoundTube().Shells[0];
            double v = shell.Volume(0.01); // this caches the triangulations of the faces
            shell.ReverseOrientation();
            Assert.AreEqual(-v, shell.Volume(0.01), Math.Abs(v) * 1e-3);
        }

        [TestMethod]
        public void a_moved_vertex_forgets_its_cached_parameters()
        {
            Solid box = Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis);
            Face fc = box.Shells[0].Faces[0];
            Vertex v = fc.Vertices[0];
            GeoPoint2D uv = v.GetPositionOnFace(fc); // now cached
            GeoPoint moved = v.Position + 0.5 * (fc.Surface.PointAt(new GeoPoint2D(uv.x + 1, uv.y + 1)) - v.Position);
            v.Position = moved;
            Assert.IsTrue((fc.Surface.PointAt(v.GetPositionOnFace(fc)) | moved) < 1e-10);
        }

        [TestMethod]
        public void shell_contains_points_around_a_cylinder()
        {
            Solid cyl = Make3D.MakePrism(Face.MakeFace(new PlaneSurface(Plane.XYPlane), new SimpleShape(Border.MakeCircle(GeoPoint2D.Origin, 10))), new GeoVector(0, 0, 20), null) as Solid;
            Shell shell = cyl.Shells[0];
            for (int i = 0; i < 16; i++)
            {
                double a = i * 2 * Math.PI / 16 + 0.1;
                Assert.IsTrue(shell.Contains(new GeoPoint(5 * Math.Cos(a), 5 * Math.Sin(a), 10)), $"inside at angle {a}");
                Assert.IsFalse(shell.Contains(new GeoPoint(15 * Math.Cos(a), 15 * Math.Sin(a), 10)), $"outside at angle {a}");
            }
        }

        [TestMethod]
        public void check_consistency_finds_a_vertex_away_from_its_edge()
        {
            Solid box = Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis);
            Face fc = box.Shells[0].Faces[0];
            Assert.IsTrue(fc.CheckConsistency());
            fc.Vertices[0].Position = fc.Vertices[0].Position + new GeoVector(0.1, 0.1, 0.1); // the edges still end at the old position
            Assert.IsFalse(fc.CheckConsistency());
        }

        [TestMethod]
        public void oriented_line_intersection_tells_entering_from_leaving()
        {
            Solid box = Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis);
            List<(double par, bool leaving)> all = new List<(double, bool)>();
            foreach (Face fc in box.Shells[0].Faces)
            {
                all.AddRange(fc.GetOrientedLineIntersection(new GeoPoint(-5, 3, 4), GeoVector.XAxis, out bool boundary));
                Assert.IsFalse(boundary);
            }
            all.Sort((a, b) => a.par.CompareTo(b.par));
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual(5.0, all[0].par, 1e-10);
            Assert.IsFalse(all[0].leaving); // enters at x == 0
            Assert.AreEqual(15.0, all[1].par, 1e-10);
            Assert.IsTrue(all[1].leaving); // leaves at x == 10
        }

        private Solid StepRoundTrip(Solid sld, string name)
        {
            Project pr = Project.CreateSimpleProject();
            pr.GetActiveModel().Add(sld);
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CADabilityTests");
            Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, name + ".stp");
            Assert.IsTrue(pr.Export(file, "stp") || File.Exists(file));
            Project read = Project.ReadFromFile(file, "stp");
            List<Solid> solids = new List<Solid>();
            foreach (IGeoObject go in read.GetActiveModel().AllObjects)
            {
                if (go is Solid s) solids.Add(s);
                else if (go is Block blk) foreach (IGeoObject child in blk.Children) if (child is Solid cs) solids.Add(cs);
            }
            Assert.AreEqual(1, solids.Count, $"{name}: expected exactly one solid");
            return solids[0];
        }

        [DataTestMethod]
        [DataRow("round")]
        [DataRow("rectangular")]
        [DataRow("bent")]
        public void tubes_survive_a_step_round_trip(string kind)
        {
            Solid original = kind == "round" ? RoundTube() : kind == "rectangular" ? RectangularTube() : BentTube();
            double vol = kind == "round" ? RoundTubeVolume : kind == "rectangular" ? RectangularTubeVolume : BentTubeVolume;
            Solid read = StepRoundTrip(original, "tube_" + kind);
            Assert.AreEqual(vol, read.Volume(0.01), vol * 1e-3, $"{kind}: volume after the round trip");
            Assert.AreEqual(0, read.Shells[0].OpenEdgesExceptPoles.Length, $"{kind}: open edges after the round trip");
            foreach (Face fc in read.Shells[0].Faces) Assert.IsTrue(fc.CheckConsistency(), $"{kind}: inconsistent face {fc.Surface.GetType().Name}");
        }

        [TestMethod]
        public void a_mirrored_tube_survives_a_step_round_trip()
        {
            Solid tube = BentTube();
            tube.Modify(ModOp.ReflectPlane(new Plane(GeoPoint.Origin, GeoVector.ZAxis)));
            Solid read = StepRoundTrip(tube, "tube_mirrored");
            Assert.AreEqual(BentTubeVolume, read.Volume(0.01), BentTubeVolume * 1e-3);
        }
    }
}
