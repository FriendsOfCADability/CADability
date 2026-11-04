using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// ACadSharp up to 3.8.35 threw on an INSERT with a scale factor of 0 and dropped it while
    /// reading. Since 3.8.36 it keeps such inserts, and the singular transformation must not reach
    /// the model: the import skips them, as before.
    /// </summary>
    [TestClass]
    public class DxfInsertScaleTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void import_dxf_skips_insert_with_zero_scale()
        {
            ACadSharp.CadDocument doc = new ACadSharp.CadDocument();
            ACadSharp.Tables.BlockRecord record = new ACadSharp.Tables.BlockRecord("block1");
            record.Entities.Add(new ACadSharp.Entities.Line(CSMath.XYZ.Zero, new CSMath.XYZ(10, 10, 0)));
            doc.BlockRecords.Add(record);

            doc.Entities.Add(new ACadSharp.Entities.Insert(record) { InsertPoint = new CSMath.XYZ(100, 0, 0) });
            doc.Entities.Add(new ACadSharp.Entities.Insert(record) { XScale = 0.0 });
            doc.Entities.Add(new ACadSharp.Entities.Insert(record) { ZScale = 0.0 });

            string file = this.TestContext.TestName + ".dxf";
            using (ACadSharp.IO.DxfWriter writer = new ACadSharp.IO.DxfWriter(file, doc, false)) writer.Write();

            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            Block[] blocks = project.GetActiveModel().AllObjects.OfType<Block>().ToArray();
            Assert.AreEqual(1, blocks.Length, "only the insert with scale 1 is imported");
            Line line = blocks[0].Children.OfType<Line>().Single();
            Assert.AreEqual(100.0, line.StartPoint.x, 1e-9, "the imported insert is the one at x = 100");
        }
    }
}
