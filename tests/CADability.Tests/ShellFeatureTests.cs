using CADability.GeoObject;
using System.Collections.Generic;
using System.Linq;

namespace CADability.Tests
{
    /// <summary>
    /// The named features of a shell (<see cref="Shell.AddFeature"/>) are stored as user data of its faces and must survive
    /// cloning and saving.
    /// </summary>
    [TestClass]
    public class ShellFeatureTests
    {
        private static Solid BoxWithFeatures()
        {
            Solid box = Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis);
            Shell shell = box.Shells[0];
            shell.AddFeature("top", new[] { shell.Faces[0] });
            shell.AddFeature("sides", new[] { shell.Faces[1], shell.Faces[2] });
            shell.AddFeature("top and side", new[] { shell.Faces[0], shell.Faces[1] });
            return box;
        }

        /// <summary>
        /// The features as indices of the faces, which can be compared between copies of a shell.
        /// </summary>
        private static Dictionary<string, int[]> FeatureIndices(Shell shell)
        {
            List<Face> faces = shell.Faces.ToList();
            return shell.GetFeatures().ToDictionary(f => f.Key, f => f.Value.Select(face => faces.IndexOf(face)).OrderBy(i => i).ToArray());
        }

        private static void AssertSameFeatures(Shell expected, Shell actual, string what)
        {
            Dictionary<string, int[]> e = FeatureIndices(expected), a = FeatureIndices(actual);
            CollectionAssert.AreEquivalent(e.Keys.ToList(), a.Keys.ToList(), what + ": feature names");
            foreach (string name in e.Keys) CollectionAssert.AreEqual(e[name], a[name], what + ": faces of " + name);
        }

        [TestMethod]
        public void FeaturesSurviveCloning()
        {
            Solid box = BoxWithFeatures();
            Solid clone = box.Clone() as Solid;
            AssertSameFeatures(box.Shells[0], clone.Shells[0], "clone");
            // the clone has its own sets: changing a feature of the clone leaves the original unchanged
            clone.Shells[0].RemoveFeature("top");
            Assert.IsTrue(box.Shells[0].GetFeatures().ContainsKey("top"), "removing a feature of the clone changed the original");
        }

        [TestMethod]
        public void FeaturesSurviveSaving()
        {
            Solid box = BoxWithFeatures();
            Solid read = JsonSerialize.FromString(JsonSerialize.ToString(box)) as Solid;
            Assert.IsNotNull(read);
            AssertSameFeatures(box.Shells[0], read.Shells[0], "json");
        }

        [TestMethod]
        public void FeaturesSurviveSavingAProject()
        {
            Solid box = BoxWithFeatures();
            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(box);
            string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ShellFeatures-" + Guid.NewGuid().ToString("N") + ".cdb.json");
            try
            {
                Assert.IsTrue(project.WriteToFile(file, false), "the project was not written");
                Solid read = Project.ReadFromFile(file).GetActiveModel().AllObjects.OfType<Solid>().Single();
                AssertSameFeatures(box.Shells[0], read.Shells[0], "project file");
            }
            finally
            {
                System.IO.File.Delete(file);
            }
        }
    }
}
