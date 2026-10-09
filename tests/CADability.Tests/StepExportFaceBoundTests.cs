using CADability.GeoObject;
using CADability.Shapes;
using System.Text.RegularExpressions;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// The STEP export of faces with holes. ISO 10303-42 allows only the outer loop of a face to be a FACE_OUTER_BOUND,
    /// the inner loops (holes) must be written as FACE_BOUND. The export used to write every loop as FACE_OUTER_BOUND.
    /// </summary>
    [TestClass]
    public class StepExportFaceBoundTests
    {
        private const double outer = 40.0, inner = 20.0, length = 100.0;

        /// <summary>
        /// A square tube: a square profile with a square hole (as in issue #265), swept along a straight line with
        /// <see cref="Make3D.MakePipe"/>. The two end faces have a hole each.
        /// </summary>
        private static Solid SquareTube()
        {
            SimpleShape profile = new SimpleShape(Border.MakeRectangle(-outer / 2, outer / 2, -outer / 2, outer / 2), Border.MakeRectangle(-inner / 2, inner / 2, -inner / 2, inner / 2));
            Face face = Face.MakeFace(new PlaneSurface(Plane.XYPlane), profile);
            GeoObject.Path along = GeoObject.Path.Construct();
            along.Add(Line.TwoPoints(GeoPoint.Origin, new GeoPoint(0, 0, length)));
            return Make3D.MakePipe(face, along, null) as Solid;
        }

        private static IEnumerable<Solid> Solids(IEnumerable<IGeoObject> objects)
        {
            foreach (IGeoObject go in objects)
            {
                if (go is Solid solid) yield return solid;
                else if (go is Block block) foreach (Solid child in Solids(block.Children)) yield return child;
            }
        }

        private static double Area(Solid solid) => solid.Shells[0].Faces.Sum(face => ShellMetrics.SurfaceArea(Shell.FromFaces((Face)face.Clone())));

        [TestMethod]
        public void holes_are_exported_as_face_bound_and_survive_a_step_round_trip()
        {
            Solid tube = SquareTube();
            Assert.IsNotNull(tube);
            double volume = (outer * outer - inner * inner) * length;
            double area = 2 * (outer * outer - inner * inner) + 4 * (outer + inner) * length;
            Assert.AreEqual(volume, tube.Volume(1e-4), volume * 1e-9, "volume of the original tube");
            Assert.AreEqual(area, Area(tube), area * 1e-9, "area of the original tube");
            int faceCount = tube.Shells[0].Faces.Length;
            int holeCount = tube.Shells[0].Faces.Sum(face => face.HoleCount);
            Assert.AreEqual(2, holeCount, "the end faces of the tube have a hole each");

            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(tube);
            string dir = Path.Combine(Path.GetTempPath(), "CADabilityTests");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "square_tube_face_bounds.stp");
            if (File.Exists(file)) File.Delete(file);
            new ExportStep().WriteToFile(file, project);

            // the entities of the file by their number
            string text = File.ReadAllText(file);
            Dictionary<string, string> entities = Regex.Matches(text, @"^#(\d+)\s*=\s*([^;]*);", RegexOptions.Multiline)
                .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            List<string> faces = entities.Values.Where(e => e.StartsWith("ADVANCED_FACE")).ToList();
            Assert.AreEqual(faceCount, faces.Count, "number of ADVANCED_FACEs");
            Assert.AreEqual(faceCount, entities.Values.Count(e => e.StartsWith("FACE_OUTER_BOUND")), "number of FACE_OUTER_BOUNDs");
            Assert.AreEqual(holeCount, entities.Values.Count(e => e.StartsWith("FACE_BOUND")), "number of FACE_BOUNDs");
            foreach (string face in faces)
            {   // ADVANCED_FACE('name',(#bound,#bound,...),#surface,.T.): exactly one of the bounds is the outer bound
                string[] bounds = Regex.Match(face, @"^ADVANCED_FACE\s*\(\s*'[^']*'\s*,\s*\(([^)]*)\)").Groups[1].Value
                    .Split(',').Select(b => b.Trim().TrimStart('#')).ToArray();
                Assert.AreEqual(1, bounds.Count(b => entities[b].StartsWith("FACE_OUTER_BOUND")), "one FACE_OUTER_BOUND in " + face);
                Assert.AreEqual(bounds.Length - 1, bounds.Count(b => entities[b].StartsWith("FACE_BOUND")), "the other bounds are FACE_BOUNDs in " + face);
            }

            Project read = Project.ReadFromFile(file, "stp");
            List<Solid> solids = Solids(read.GetActiveModel().AllObjects).ToList();
            Assert.AreEqual(1, solids.Count);
            Assert.AreEqual(faceCount, solids[0].Shells[0].Faces.Length, "number of faces after the round trip");
            Assert.AreEqual(holeCount, solids[0].Shells[0].Faces.Sum(face => face.HoleCount), "number of holes after the round trip");
            Assert.AreEqual(0, solids[0].Shells[0].OpenEdgesExceptPoles.Length);
            Assert.AreEqual(volume, solids[0].Volume(1e-4), volume * 1e-9, "volume after the round trip");
            Assert.AreEqual(area, Area(solids[0]), area * 1e-9, "area after the round trip");
            foreach (Face face in solids[0].Shells[0].Faces) Assert.IsTrue(face.CheckConsistency(), "inconsistent face after the round trip");
        }
    }
}
