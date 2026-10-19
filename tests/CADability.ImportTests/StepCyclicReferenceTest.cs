using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// Regression for issue #487: ImportStep.CreateEntity resolves references recursively. A reference cycle, here a
    /// MAPPED_ITEM whose REPRESENTATION_MAP maps the representation that contains the mapped item, made it create the same
    /// entities again and again until the stack overflowed, which terminates the process. With the parallel import two
    /// threads that enter a cycle from different ends would instead wait for each other forever. The cyclic reference is
    /// now dropped and the rest of the file is imported.
    /// </summary>
    [TestClass]
    public class StepCyclicReferenceTest
    {
        public TestContext TestContext { get; set; }

        private const string SourceFile = "issue101.stp";
        // issue101.stp contains a single solid with 25 faces, #12 is the representation that holds it
        private const string BrepRepresentation = "#12=ADVANCED_BREP_SHAPE_REPRESENTATION('',(#14),#871);";

        /// <summary>the representation of the solid also contains a mapped item that maps this representation</summary>
        private const string SelfMappingRepresentation =
            "#12=ADVANCED_BREP_SHAPE_REPRESENTATION('',(#14,#9001),#871);\n" +
            "#9001=MAPPED_ITEM('',#9002,#523);\n" +
            "#9002=REPRESENTATION_MAP(#523,#12);";

        /// <summary>
        /// two shape definition representations, whose representations map each other: the import of their roots enters
        /// the cycle from both ends
        /// </summary>
        private const string MutuallyMappingRepresentations =
            BrepRepresentation + "\n" +
            "#9010=SHAPE_DEFINITION_REPRESENTATION(#878,#9011);\n" +
            "#9011=SHAPE_REPRESENTATION('a',(#9012),#871);\n" +
            "#9012=MAPPED_ITEM('',#9013,#523);\n" +
            "#9013=REPRESENTATION_MAP(#523,#9021);\n" +
            "#9020=SHAPE_DEFINITION_REPRESENTATION(#878,#9021);\n" +
            "#9021=SHAPE_REPRESENTATION('b',(#9022),#871);\n" +
            "#9022=MAPPED_ITEM('',#9023,#523);\n" +
            "#9023=REPRESENTATION_MAP(#523,#9011);";

        private string WriteVariant(string replacement)
        {
            string source = System.IO.Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName, SourceFile);
            Assert.IsTrue(File.Exists(source));
            string text = File.ReadAllText(source).Replace("\r\n", "\n");
            Assert.IsTrue(text.Contains(BrepRepresentation), "the fixture should contain the representation of the solid");
            string file = System.IO.Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName, "cyclic.stp");
            File.WriteAllText(file, text.Replace(BrepRepresentation, replacement));
            return file;
        }

        private static int ImportFaceCount(string file, bool parallel)
        {
            object old = Settings.GlobalSettings.GetValue("StepImport.Parallel");
            Settings.GlobalSettings.SetValue("StepImport.Parallel", parallel);
            try
            {
                // a cycle must neither overflow the stack nor make the threads of the parallel import wait for each other
                Task<int> import = Task.Factory.StartNew(() =>
                {
                    Project project = Project.ReadFromFile(file, "stp");
                    return project.GetActiveModel().AllObjects.Sum(CountFaces);
                }, TaskCreationOptions.LongRunning);
                Assert.IsTrue(import.Wait(TimeSpan.FromMinutes(2)), "the import did not finish");
                return import.Result;
            }
            finally
            {
                if (old != null) Settings.GlobalSettings.SetValue("StepImport.Parallel", old);
            }
        }

        [TestMethod]
        [DeploymentItem(@"Files/Step/" + SourceFile, nameof(import_step_with_a_representation_that_maps_itself))]
        public void import_step_with_a_representation_that_maps_itself()
        {
            string file = WriteVariant(SelfMappingRepresentation);
            // the solid, and once more as the mapped item, where the cycle is cut
            Assert.AreEqual(50, ImportFaceCount(file, false), "sequential import");
            Assert.AreEqual(50, ImportFaceCount(file, true), "parallel import");
        }

        [TestMethod]
        [DeploymentItem(@"Files/Step/" + SourceFile, nameof(import_step_with_representations_that_map_each_other))]
        public void import_step_with_representations_that_map_each_other()
        {
            string file = WriteVariant(MutuallyMappingRepresentations);
            // the solid, once for each of the three shape definition representations of its product
            Assert.AreEqual(75, ImportFaceCount(file, false), "sequential import");
            // whether the threads really enter the cycle at the same time depends on the timing, so try several times
            for (int i = 0; i < 20; i++) Assert.AreEqual(75, ImportFaceCount(file, true), "parallel import");
        }

        private static int CountFaces(IGeoObject go)
        {
            switch (go)
            {
                case Solid solid: return solid.Shells.Sum(shell => shell.Faces.Length);
                case Shell shell: return shell.Faces.Length;
                case Face _: return 1;
                case Block block: return Enumerable.Range(0, block.NumChildren).Sum(i => CountFaces(block.Child(i)));
                default: return 0;
            }
        }
    }
}
