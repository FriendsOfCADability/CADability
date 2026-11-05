using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
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
    }
}
