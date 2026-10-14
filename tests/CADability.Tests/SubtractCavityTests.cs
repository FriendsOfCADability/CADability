using CADability.GeoObject;
using System.Text.RegularExpressions;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// #416, finding 16: subtracting a solid that lies inside another one without touching it returned the first solid
    /// unchanged, the hole was lost. The result is the first solid with the second one as a cavity now, the second entry
    /// of <see cref="Solid.Shells"/>, whose faces point into the cavity. STEP writes and reads it as BREP_WITH_VOIDS; the
    /// import dropped the voids before.
    /// </summary>
    [TestClass]
    public class SubtractCavityTests
    {
        private static Solid Box(double x, double y, double z, double d)
            => Make3D.MakeBox(new GeoPoint(x, y, z), d * GeoVector.XAxis, d * GeoVector.YAxis, d * GeoVector.ZAxis);

        private static void AssertCavity(Solid solid, double volume, string what)
        {
            Assert.AreEqual(2, solid.Shells.Length, $"{what}: the outer shell and the cavity");
            Assert.AreEqual(1000.0, solid.Shells[0].Volume(0.01), 1e-6, $"{what}: the outer shell");
            Assert.IsTrue(solid.Shells[1].Volume(0.01) < 0.0, $"{what}: the faces of the cavity point into it");
            Assert.AreEqual(volume, solid.Volume(0.01), volume * 1e-3, $"{what}: volume of the triangulation");
            Assert.AreEqual(volume, solid.Shells.Sum(s => ShellMetrics.IntegratedVolume(s, 0.01)), volume * 1e-9, $"{what}: integrated volume");
        }

        [TestMethod]
        public void a_solid_inside_another_one_becomes_a_cavity()
        {
            Solid outer = Box(0, 0, 0, 10);
            foreach ((Solid inner, double volume) in new[] { (Box(2, 2, 2, 6), 1000.0 - 216.0), (Make3D.MakeSphere(new GeoPoint(5, 5, 5), 3), 1000.0 - 4.0 / 3.0 * Math.PI * 27.0) })
            {
                Solid[] res = Solid.Subtract(outer, inner);
                Assert.AreEqual(1, res.Length);
                AssertCavity(res[0], volume, inner.Shells[0].Faces[0].Surface.GetType().Name);
                Assert.IsTrue(inner.Shells[0].Volume(0.01) > 0.0, "the subtracted solid is not changed");
                Assert.AreEqual(1, outer.Shells.Length, "the first solid is not changed");
            }
        }

        [TestMethod]
        public void a_cavity_survives_a_step_round_trip()
        {
            Solid solid = Solid.Subtract(Box(0, 0, 0, 10), Box(2, 2, 2, 6)).Single();
            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(solid);
            string dir = Path.Combine(Path.GetTempPath(), "CADabilityTests");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "box_with_cavity.stp");
            if (File.Exists(file)) File.Delete(file);
            new ExportStep().WriteToFile(file, project);

            string text = File.ReadAllText(file);
            Assert.AreEqual(1, Regex.Matches(text, @"=\s*BREP_WITH_VOIDS\s*\(").Count, "a BREP_WITH_VOIDS");
            Assert.AreEqual(0, Regex.Matches(text, @"=\s*MANIFOLD_SOLID_BREP\s*\(").Count, "no MANIFOLD_SOLID_BREP");
            Assert.AreEqual(1, Regex.Matches(text, @"=\s*ORIENTED_CLOSED_SHELL\s*\([^;]*\.F\.\s*\)").Count, "the void, oriented .F.");

            Project read = Project.ReadFromFile(file, "stp");
            Solid[] solids = read.GetActiveModel().AllObjects.OfType<Solid>().ToArray();
            Assert.AreEqual(1, solids.Length);
            AssertCavity(solids[0], 784.0, "after the round trip");
        }
    }
}
