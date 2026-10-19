using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// DWG files older than R13: R10 (AC1006) and R11/R12 (AC1009). The drawings come from
    /// https://github.com/michal-josef-spacek/CAD-Format-DWG-AC1006 and
    /// https://github.com/michal-josef-spacek/CAD-Format-DWG-AC1009 (BSD-2-Clause, see REUSE.toml).
    /// </summary>
    [TestClass]
    public class DwgPreR13ImportTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Files/Dwg/r12_insert.dwg", nameof(import_dwg_r12_insert_succeeds))]
        public void import_dwg_r12_insert_succeeds()
        {
            // An INSERT of block BLOCK1 at (6,1,3), scaled by 0.5 and rotated by 30 degrees.
            // The block holds a single LINE from (0,0,-1) to (1,1,0).
            Model model = this.ImportDwg("r12_insert.dwg");

            Assert.AreEqual(1, model.Count, "the INSERT must be the only object of the model");
            Block block = model[0] as Block;
            Assert.IsNotNull(block, "the INSERT must be imported as a Block");
            Assert.AreEqual("BLOCK1", block.Name);
            Assert.AreEqual(1, block.Count);

            Line line = block.Child(0) as Line;
            Assert.IsNotNull(line, "the block must hold the LINE");
            AssertPoint(new GeoPoint(6, 1, 2.5), line.StartPoint);
            AssertPoint(new GeoPoint(6.18301270189222, 1.68301270189222, 3), line.EndPoint);
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dwg/r10_insert_thickness.dwg", nameof(import_dwg_r10_insert_succeeds))]
        public void import_dwg_r10_insert_succeeds()
        {
            // R10 stores the entities slightly differently from R11/R12. The block BLOCK holds a
            // LINE with a thickness, which imports as a face.
            Model model = this.ImportDwg("r10_insert_thickness.dwg");

            Assert.AreEqual(1, model.Count, "the INSERT must be the only object of the model");
            Block block = model[0] as Block;
            Assert.IsNotNull(block, "the INSERT must be imported as a Block");
            Assert.AreEqual("BLOCK", block.Name);
            Assert.AreEqual(1, block.Count);
            Assert.IsInstanceOfType(block.Child(0), typeof(Face), "a LINE with thickness imports as a Face");
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dwg/r12_dimension_aligned.dwg", nameof(import_dwg_r12_dimension_succeeds))]
        public void import_dwg_r12_dimension_succeeds()
        {
            RequireGdi();

            // An aligned DIMENSION whose block holds the dimension lines, the arrows and the text.
            Model model = this.ImportDwg("r12_dimension_aligned.dwg");

            Assert.AreEqual(1, model.Count, "the DIMENSION must be the only object of the model");
            Block block = model[0] as Block;
            Assert.IsNotNull(block, "a DIMENSION imports as the block that holds its picture");

            List<IGeoObject> all = Flatten(block).ToList();
            Assert.IsTrue(all.OfType<Line>().Any(), "the dimension lines must be imported");
            Assert.IsTrue(all.OfType<Text>().Any(t => t.TextString == "1.4142"),
                "the measurement text must be imported");
        }

        // Adding a Text to a model computes its extent through GDI, which only exists on Windows.
        private static void RequireGdi()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                Assert.Inconclusive("text needs GDI, which is only available on Windows");
        }

        private static IEnumerable<IGeoObject> Flatten(Block block)
        {
            for (int i = 0; i < block.Count; i++)
            {
                IGeoObject child = block.Child(i);
                yield return child;
                if (child is Block inner)
                {
                    foreach (IGeoObject go in Flatten(inner)) yield return go;
                }
            }
        }

        private static void AssertPoint(GeoPoint expected, GeoPoint actual)
        {
            Assert.IsTrue((expected | actual) < 1e-9, $"expected {expected}, got {actual}");
        }

        private Model ImportDwg(string name)
        {
            string file = System.IO.Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, name);
            Assert.IsTrue(File.Exists(file));
            Assert.IsTrue(CADability.DXF.Import.CanImportVersion(file), "the version of the drawing must be readable");

            Project project = Project.ReadFromFile(file, "dwg");
            Assert.IsNotNull(project);
            Model model = project.GetActiveModel();
            Assert.IsNotNull(model);
            return model;
        }
    }
}
