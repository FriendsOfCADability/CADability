using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.DXF;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// Every INSERT gets its own copy of its block, so nested blocks multiply (issue #488): a DXF of a few KB with a line in
    /// block L9 and 10 INSERTs of the next level in each of L1 to L8 expands to 10^8 lines, and the import used to run until
    /// the memory was exhausted. The file of 3.3 KB is now refused after about 2.5 s and 0.8 GB. It now stops with an exception when the copies exceed the setting "DxfDwg.MaxImportObjects".
    /// </summary>
    [TestClass]
    public class DxfNestedInsertLimitTest
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// Writes the DXF by hand: ACadSharp's DxfWriter itself needs gigabytes for such a file, which is only a few KB.
        /// </summary>
        private string WriteNestedBlocks(int levels, int insertsPerLevel)
        {
            StringBuilder sb = new StringBuilder();
            void Group(int code, object value) => sb.Append(code).Append('\n').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\n');
            void Block(string name)
            {
                Group(0, "BLOCK"); Group(8, "0"); Group(2, name); Group(70, 0); Group(10, 0); Group(20, 0); Group(30, 0); Group(3, name);
            }
            void Insert(string name, double y)
            {
                Group(0, "INSERT"); Group(8, "0"); Group(2, name); Group(10, 0); Group(20, y); Group(30, 0);
            }
            Group(0, "SECTION"); Group(2, "BLOCKS");
            Block("L" + levels);
            Group(0, "LINE"); Group(8, "0"); Group(10, 0); Group(20, 0); Group(30, 0); Group(11, 1); Group(21, 0); Group(31, 0);
            Group(0, "ENDBLK"); Group(8, "0");
            for (int level = levels - 1; level >= 1; level--)
            {
                Block("L" + level);
                for (int i = 0; i < insertsPerLevel; i++) Insert("L" + (level + 1), 2 * i);
                Group(0, "ENDBLK"); Group(8, "0");
            }
            Group(0, "ENDSEC");
            Group(0, "SECTION"); Group(2, "ENTITIES");
            Insert("L1", 0);
            Group(0, "ENDSEC");
            Group(0, "EOF");
            string file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, sb.ToString());
            return file;
        }

        private static int LineCount(IGeoObject go)
        {
            if (go is Block blk) return blk.Children.Sum(LineCount);
            return go is Line ? 1 : 0;
        }

        [TestMethod]
        public void nested_blocks_beyond_the_limit_stop_the_import()
        {
            string file = WriteNestedBlocks(9, 10);
            Stopwatch sw = Stopwatch.StartNew();
            long before = GC.GetTotalMemory(true);
            Exception ex = null;
            try
            {
                Project.ReadFromFile(file, "dxf");
            }
            catch (Exception e)
            {
                ex = e;
            }
            Assert.IsNotNull(ex, "the import of 10^8 lines must be refused");
            StringAssert.Contains(ex.Message, "DxfDwg.MaxImportObjects");
            Assert.IsTrue(sw.Elapsed.TotalSeconds < 60, "the import took " + sw.Elapsed);
            GC.Collect();
            Assert.IsTrue(GC.GetTotalMemory(true) - before < 500_000_000, "the memory of the refused import was not released");
        }

        [TestMethod]
        public void nested_blocks_below_the_limit_are_imported_completely()
        {
            string file = WriteNestedBlocks(5, 10); // 10^4 lines
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.AreEqual(10000, project.GetActiveModel().AllObjects.Sum(LineCount));
        }

        [TestMethod]
        [DoNotParallelize] // changes a global setting
        public void the_limit_is_a_setting()
        {
            string file = WriteNestedBlocks(5, 10);
            const string setting = "DxfDwg.MaxImportObjects";
            bool existed = Settings.GlobalSettings.ContainsSetting(setting);
            object old = existed ? Settings.GlobalSettings.GetValue(setting) : null;
            Settings.GlobalSettings.SetValue(setting, 1000);
            try
            {
                Exception ex = null;
                try { Project.ReadFromFile(file, "dxf"); }
                catch (Exception e) { ex = e; }
                Assert.IsNotNull(ex, "10^4 lines exceed a limit of 1000");
                StringAssert.Contains(ex.Message, "1000 objects");
            }
            finally
            {
                if (existed) Settings.GlobalSettings.SetValue(setting, old);
                else Settings.GlobalSettings.RemoveSetting(setting);
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> with the setting "DxfDwg.MaxImportObjects" set to <paramref name="limit"/>.
        /// </summary>
        private static void WithLimit(int limit, Action action)
        {
            const string setting = "DxfDwg.MaxImportObjects";
            bool existed = Settings.GlobalSettings.ContainsSetting(setting);
            object old = existed ? Settings.GlobalSettings.GetValue(setting) : null;
            Settings.GlobalSettings.SetValue(setting, limit);
            try { action(); }
            finally
            {
                if (existed) Settings.GlobalSettings.SetValue(setting, old);
                else Settings.GlobalSettings.RemoveSetting(setting);
            }
        }

        /// <summary>
        /// The exception a caller gets, with its exact type checked: it must be the public type and not wrapped.
        /// </summary>
        private static DxfImportLimitException ExpectLimit(Action import, long limit)
        {
            DxfImportLimitException ex = Assert.ThrowsException<DxfImportLimitException>(import);
            Assert.AreEqual(limit, ex.Limit);
            Assert.IsNull(ex.InnerException);
            StringAssert.Contains(ex.Message, "DxfDwg.MaxImportObjects");
            return ex;
        }

        /// <summary>
        /// A caller written for 1.8.0-preview.3, which only knew an <see cref="ApplicationException"/>, still catches it.
        /// </summary>
        private static void ExpectApplicationException(Action import)
        {
            try
            {
                import();
                Assert.Fail("the import must be refused");
            }
            catch (ApplicationException ex)
            {
                Assert.IsInstanceOfType(ex, typeof(DxfImportLimitException));
            }
        }

        [TestMethod]
        [DoNotParallelize] // changes a global setting
        public void a_dxf_file_beyond_the_limit_throws_the_public_type()
        {
            string file = WriteNestedBlocks(5, 10); // 10^4 lines
            WithLimit(1000, () =>
            {
                ExpectLimit(() => Project.ReadFromFile(file, "dxf"), 1000);
                ExpectLimit(() => { _ = new Import(file).Project; }, 1000);
                ExpectApplicationException(() => Project.ReadFromFile(file, "dxf"));
            });
        }

        /// <summary>
        /// The DWG reader of ACadSharp for R13 and later, with the same nested blocks as the DXF tests.
        /// </summary>
        [TestMethod]
        [DoNotParallelize] // changes a global setting
        public void a_dwg_file_beyond_the_limit_throws_the_public_type()
        {
            // the nested blocks of WriteNestedBlocks(5, 10), built with ACadSharp, which writes the DWG
            ACadSharp.CadDocument doc = new ACadSharp.CadDocument(ACadSharp.ACadVersion.AC1018);
            ACadSharp.Tables.BlockRecord inner = new ACadSharp.Tables.BlockRecord("L5");
            inner.Entities.Add(new ACadSharp.Entities.Line(new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(1, 0, 0)));
            doc.BlockRecords.Add(inner);
            for (int level = 4; level >= 1; level--)
            {
                ACadSharp.Tables.BlockRecord outer = new ACadSharp.Tables.BlockRecord("L" + level);
                for (int i = 0; i < 10; i++) outer.Entities.Add(new ACadSharp.Entities.Insert(inner) { InsertPoint = new CSMath.XYZ(0, 2 * i, 0) });
                doc.BlockRecords.Add(outer);
                inner = outer;
            }
            doc.Entities.Add(new ACadSharp.Entities.Insert(inner));
            string dwg = this.TestContext.TestName + ".dwg";
            using (ACadSharp.IO.DwgWriter writer = new ACadSharp.IO.DwgWriter(dwg, doc)) writer.Write();

            Project imported = Project.ReadFromFile(dwg, "dwg"); // the DWG holds the whole drawing
            Assert.AreEqual(10000, imported.GetActiveModel().AllObjects.Sum(LineCount));
            WithLimit(1000, () =>
            {
                ExpectLimit(() => Project.ReadFromFile(dwg, "dwg"), 1000);
                ExpectLimit(() => { _ = new Import(dwg).Project; }, 1000);
                ExpectApplicationException(() => Project.ReadFromFile(dwg, "dwg"));
            });
        }

        /// <summary>
        /// The R10 to R12 DWG reader of the ACadSharp fork, with a real drawing: one INSERT of a block with one line,
        /// which is 2 objects.
        /// </summary>
        [TestMethod]
        [DoNotParallelize] // changes a global setting
        [DeploymentItem(@"Files/Dwg/r12_insert.dwg", nameof(an_r12_dwg_file_beyond_the_limit_throws_the_public_type))]
        public void an_r12_dwg_file_beyond_the_limit_throws_the_public_type()
        {
            string dwg = System.IO.Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "r12_insert.dwg");
            Assert.IsTrue(File.Exists(dwg));
            WithLimit(1, () =>
            {
                ExpectLimit(() => Project.ReadFromFile(dwg, "dwg"), 1);
                ExpectApplicationException(() => Project.ReadFromFile(dwg, "dwg"));
            });
        }

        /// <summary>
        /// Other errors of the DWG import are still wrapped in an <see cref="InvalidOperationException"/>.
        /// </summary>
        [TestMethod]
        public void other_dwg_errors_are_still_wrapped()
        {
            string dwg = this.TestContext.TestName + ".dwg";
            File.WriteAllBytes(dwg, Encoding.ASCII.GetBytes("AC1018" + new string('\0', 200)));
            Assert.ThrowsException<InvalidOperationException>(() => Project.ReadFromFile(dwg, "dwg"));
        }
    }
}
