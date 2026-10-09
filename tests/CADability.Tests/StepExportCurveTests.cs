using CADability.GeoObject;
using System.Text.RegularExpressions;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// Regression tests for issue #66, "Export step problem":
    /// <list type="bullet">
    /// <item>An edge whose 3d curve is a <see cref="GeoObject.Path"/> (e.g. after ruling a surface) made the STEP export throw
    /// a NullReferenceException, because Path has no STEP representation. Such a curve is now written as a BSpline.</item>
    /// <item>A face bounded by a single closed edge (fma_02 from the issue's comments) was exported with an empty EDGE_LOOP
    /// and without the curve of the edge.</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class StepExportCurveTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// A planar face in the xy-plane, bounded by a <see cref="GeoObject.Path"/> (a line, a semicircle with radius 30 and
        /// another line) and a closing line. Its area is 100*60 + pi*30*30/2.
        /// </summary>
        private static Face FaceWithPathEdge(out GeoObject.Path path)
        {
            path = GeoObject.Path.Construct();
            path.Add(Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0)));
            Ellipse arc = Ellipse.Construct();
            arc.SetArc3Points(new GeoPoint(100, 0, 0), new GeoPoint(130, 30, 0), new GeoPoint(100, 60, 0), Plane.XYPlane);
            path.Add(arc);
            path.Add(Line.TwoPoints(new GeoPoint(100, 60, 0), new GeoPoint(0, 60, 0)));
            Line closing = Line.TwoPoints(new GeoPoint(0, 60, 0), new GeoPoint(0, 0, 0));
            PlaneSurface surface = new PlaneSurface(Plane.XYPlane);
            Face face = Face.Construct();
            face.Surface = surface;
            Edge pathEdge = new Edge(face, path, face, surface.GetProjectedCurve(path, 0.0), true);
            Edge lineEdge = new Edge(face, closing, face, surface.GetProjectedCurve(closing, 0.0), true);
            face.Set(surface, new Edge[][] { new Edge[] { pathEdge, lineEdge } });
            return face;
        }

        private static string TempFile(string name)
        {
            string dir = Path.Combine(Path.GetTempPath(), "CADabilityTests");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, name + ".stp");
            if (File.Exists(file)) File.Delete(file);
            return file;
        }

        /// <summary>
        /// Checks the structure of an exported STEP file: every referenced entity is defined, and every EDGE_LOOP and
        /// ORIENTED_EDGE refers to something.
        /// </summary>
        private static void AssertStepFileIsComplete(string file)
        {
            string text = File.ReadAllText(file);
            HashSet<string> defined = new HashSet<string>(Regex.Matches(text, @"^#(\d+)\s*=", RegexOptions.Multiline).Select(m => m.Groups[1].Value));
            Assert.IsTrue(defined.Count > 0, "the STEP file contains no entities");
            string data = text.Substring(text.IndexOf("DATA;"));
            HashSet<string> referenced = new HashSet<string>(Regex.Matches(data, @"#(\d+)").Select(m => m.Groups[1].Value));
            referenced.ExceptWith(defined);
            Assert.AreEqual(0, referenced.Count, "undefined entities: #" + string.Join(", #", referenced));
            Assert.IsFalse(Regex.IsMatch(text, @"EDGE_LOOP\s*\(\s*'[^']*'\s*,\s*\(\s*\)\s*\)"), "the STEP file contains an empty EDGE_LOOP");
            Assert.IsFalse(Regex.IsMatch(text, @"ORIENTED_EDGE\s*\([^;]*#-1"), "an ORIENTED_EDGE refers to no edge");
        }

        private static IEnumerable<Face> Faces(IGeoObject go)
        {
            switch (go)
            {
                case Solid solid: return solid.Shells.SelectMany(shell => shell.Faces);
                case Shell shell: return shell.Faces;
                case Face face: return new Face[] { face };
                case Block block: return block.Children.SelectMany(Faces);
                default: return Enumerable.Empty<Face>();
            }
        }

        private static double Area(IEnumerable<Face> faces) => faces.Sum(face => ShellMetrics.SurfaceArea(Shell.FromFaces((Face)face.Clone())));

        /// <summary>The maximum distance of points of <paramref name="curve"/> from <paramref name="other"/>.</summary>
        private static double MaxDistance(ICurve curve, ICurve other)
        {
            double max = 0.0;
            for (int i = 0; i <= 1000; i++)
            {
                GeoPoint p = curve.PointAt(i / 1000.0);
                max = Math.Max(max, p | other.PointAt(other.PositionOf(p)));
            }
            return max;
        }

        [TestMethod]
        public void a_face_with_a_path_edge_survives_a_step_round_trip()
        {
            Face face = FaceWithPathEdge(out GeoObject.Path path);
            double area = 100 * 60 + Math.PI * 30 * 30 / 2;
            Assert.AreEqual(area, Area(new Face[] { face }), 1e-6, "area of the original face");

            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(face);
            string file = TempFile("issue66_path_edge");
            new ExportStep().WriteToFile(file, project);
            AssertStepFileIsComplete(file);

            Project read = Project.ReadFromFile(file, "stp");
            List<Face> faces = read.GetActiveModel().AllObjects.SelectMany(Faces).ToList();
            Assert.AreEqual(1, faces.Count);
            Assert.AreEqual(area, Area(faces), area * 1e-6, "area after the round trip");
            Assert.IsTrue(faces[0].CheckConsistency());
            // the path has been written as a single curve, which follows it closely, also around the corners
            ICurve written = faces[0].AllEdges.Select(edge => edge.Curve3D).OrderBy(curve => MaxDistance(path, curve)).First();
            Assert.AreEqual(0.0, MaxDistance(path, written), 1e-5, "deviation of the exported path");
            Assert.AreEqual(path.Length, written.Length, 1e-4, "length of the exported path");
        }

        /// <summary>
        /// The vertices of the path are poles of the BSpline with <paramref name="joints"/> knots of full multiplicity, so the
        /// corners are kept, and the first segment, a line on the x-axis, is reproduced exactly.
        /// </summary>
        private static void AssertCornersAreKept(BSpline bsp, int joints)
        {
            foreach (GeoPoint corner in new GeoPoint[] { new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0), new GeoPoint(100, 60, 0), new GeoPoint(0, 60, 0) })
            {
                Assert.IsTrue(bsp.Poles.Any(pole => (pole | corner) < 1e-10), $"no pole at the corner {corner}");
            }
            Assert.AreEqual(joints, bsp.Multiplicities.Skip(1).Take(bsp.Multiplicities.Length - 2).Count(m => m == bsp.Degree), "number of joints");
            // a line is reproduced exactly: the poles of the first segment lie on the x-axis
            foreach (GeoPoint pole in bsp.Poles.TakeWhile(pole => (pole | new GeoPoint(100, 0, 0)) > 1e-10)) Assert.AreEqual(0.0, pole.y, 1e-12);
        }

        [TestMethod]
        public void a_path_of_lines_and_arcs_is_converted_to_an_exact_bspline_with_corners()
        {
            FaceWithPathEdge(out GeoObject.Path path);
            BSpline bsp = ExportStep.ToBSpline(path, 1e-6);
            Assert.AreEqual(2, bsp.Degree);
            Assert.IsTrue(bsp.HasWeights, "the semicircle needs a rational BSpline");
            Assert.AreEqual(0.0, MaxDistance(bsp, path), 1e-9);
            Assert.AreEqual(0.0, MaxDistance(path, bsp), 2e-6); // limited by the precision of BSpline.PositionOf
            // the line, two quarter circles and the line: three joints and no other inner knot
            Assert.AreEqual(5, bsp.Multiplicities.Length);
            AssertCornersAreKept(bsp, 3);
        }

        [TestMethod]
        public void a_path_with_a_spline_is_approximated_by_a_bspline_with_corners()
        {
            FaceWithPathEdge(out GeoObject.Path path);
            // the semicircle replaced by a spline through points of it: there is no exact representation, so the path is approximated
            ICurve[] curves = path.Curves;
            BSpline spline = BSpline.Construct();
            spline.ThroughPoints(new GeoPoint[] { new GeoPoint(100, 0, 0), new GeoPoint(130, 30, 0), new GeoPoint(100, 60, 0) }, 3, false);
            path = GeoObject.Path.Construct();
            Assert.IsTrue(path.Set(new ICurve[] { curves[0].Clone(), spline, curves[2].Clone() }));
            BSpline bsp = ExportStep.ToBSpline(path, 1e-6);
            Assert.AreEqual(3, bsp.Degree);
            Assert.IsFalse(bsp.HasWeights);
            Assert.AreEqual(0.0, MaxDistance(path, bsp), 2e-6);
            Assert.AreEqual(0.0, MaxDistance(bsp, path), 2e-6);
            AssertCornersAreKept(bsp, 2);
        }

        [TestMethod]
        public void a_path_of_lines_and_arcs_survives_a_step_round_trip_exactly()
        {
            Face face = FaceWithPathEdge(out GeoObject.Path path);
            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(face);
            string file = TempFile("issue66_exact_path_edge");
            new ExportStep().WriteToFile(file, project);
            AssertStepFileIsComplete(file);
            StringAssert.Contains(File.ReadAllText(file), "RATIONAL_B_SPLINE_CURVE");

            Project read = Project.ReadFromFile(file, "stp");
            List<Face> faces = read.GetActiveModel().AllObjects.SelectMany(Faces).ToList();
            Assert.AreEqual(1, faces.Count);
            ICurve written = faces[0].AllEdges.Select(edge => edge.Curve3D).OrderBy(curve => MaxDistance(curve, path)).First();
            // the curve read back lies on the path and has the same ends
            Assert.AreEqual(0.0, MaxDistance(written, path), 1e-9, "deviation of the exported path");
            Assert.AreEqual(0.0, Math.Min(written.StartPoint | path.StartPoint, written.StartPoint | path.EndPoint), 1e-9);
            Assert.AreEqual(0.0, Math.Min(written.EndPoint | path.StartPoint, written.EndPoint | path.EndPoint), 1e-9);
            Assert.AreEqual(path.Length, written.Length, 1e-6, "length of the exported path");
        }

        [TestMethod]
        [DeploymentItem(@"Files/CDB/issue66.cdb.json", nameof(issue66_ruled_solid_with_path_edges_survives_a_step_round_trip))]
        public void issue66_ruled_solid_with_path_edges_survives_a_step_round_trip()
        {
            string cdb = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue66.cdb.json");
            Assert.IsTrue(File.Exists(cdb));
            Project project;
            try
            {
                project = Project.ReadFromFile(cdb, "cdb");
            }
            catch (TypeInitializationException e) when (e.GetBaseException() is PlatformNotSupportedException)
            {
                Assert.Inconclusive("reading a project needs System.Drawing printing, which is only available on Windows");
                return;
            }
            Solid original = project.GetActiveModel().AllObjects.OfType<Solid>().Single();
            Assert.IsTrue(original.Shells[0].Edges.Any(edge => edge.Curve3D is GeoObject.Path), "the test file is meant to contain edges with a Path");
            double volume = original.Volume(1e-4);

            string file = TempFile("issue66_ruled");
            new ExportStep().WriteToFile(file, project);
            AssertStepFileIsComplete(file);

            Project read = Project.ReadFromFile(file, "stp");
            List<Solid> solids = read.GetActiveModel().AllObjects.SelectMany(go => go is Block block ? block.Children.ToList() : new List<IGeoObject> { go }).OfType<Solid>().ToList();
            Assert.AreEqual(1, solids.Count);
            Assert.AreEqual(original.Shells[0].Faces.Length, solids[0].Shells[0].Faces.Length);
            Assert.AreEqual(volume, solids[0].Volume(1e-4), volume * 1e-5);
            Assert.AreEqual(0, solids[0].Shells[0].OpenEdgesExceptPoles.Length);
        }

        [TestMethod]
        [DeploymentItem(@"Files/Step/issue66_fma_02.stp", nameof(issue66_face_with_a_single_closed_edge_survives_a_step_round_trip))]
        public void issue66_face_with_a_single_closed_edge_survives_a_step_round_trip()
        {
            string stp = Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue66_fma_02.stp");
            Assert.IsTrue(File.Exists(stp));
            Project project = Project.ReadFromFile(stp, "stp");
            List<Face> faces = project.GetActiveModel().AllObjects.SelectMany(Faces).ToList();
            Assert.IsTrue(faces.Count > 0);
            Assert.IsTrue(faces.All(face => face.AllEdges.Length == 1 && face.AllEdges[0].Curve3D is BSpline), "a disc bounded by a single closed BSpline");
            double area = Area(faces);

            string file = TempFile("issue66_fma_02");
            new ExportStep().WriteToFile(file, project);
            AssertStepFileIsComplete(file);
            StringAssert.Contains(File.ReadAllText(file), "B_SPLINE_CURVE_WITH_KNOTS");

            Project read = Project.ReadFromFile(file, "stp");
            List<Face> readFaces = read.GetActiveModel().AllObjects.SelectMany(Faces).ToList();
            Assert.AreEqual(faces.Count, readFaces.Count);
            Assert.AreEqual(area, Area(readFaces), area * 1e-6);
        }
    }
}
