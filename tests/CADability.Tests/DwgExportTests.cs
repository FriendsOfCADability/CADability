using System.Runtime.CompilerServices;
using ACadSharp;
using ACadSharp.IO;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// The export dialog offered DWG, but Project.Export wrote nothing for it. It now writes the same
    /// document as the DXF export with the DwgWriter of ACadSharp.
    /// </summary>
    [TestClass]
    public class DwgExportTests
    {
        public TestContext TestContext { get; set; }

        private static string TestFile(string folder, string name, [CallerFilePath] string thisFile = "")
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile), "Files", folder, name);

        private string TempFile(string extension)
            => System.IO.Path.Combine(TestContext.TestRunResultsDirectory ?? System.IO.Path.GetTempPath(), $"dwgexport-{Guid.NewGuid():N}.{extension}");

        private static string TypeCounts(Model model)
            => string.Join(", ", model.AllObjects.GroupBy(o => o.GetType().Name).OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count()));

        /// <summary>
        /// A dimension, a polyline, a circle and a box, which the export writes as a polyface mesh.
        /// </summary>
        private static Project SyntheticProject()
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            Dimension dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimPoints;
            dim.DimensionStyle = project.DimensionStyleList.Current;
            dim.Normal = GeoVector.ZAxis;
            dim.DimLineRef = new GeoPoint(0, 20, 0);
            dim.DimLineDirection = GeoVector.XAxis;
            dim.AddPoint(new GeoPoint(0, 0, 0));
            dim.AddPoint(new GeoPoint(100, 0, 0));
            model.Add(dim);
            Polyline polyline = Polyline.Construct();
            polyline.SetPoints(new[] { new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0), new GeoPoint(10, 10, 0) }, false);
            model.Add(polyline);
            Ellipse circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(Plane.XYPlane, new GeoPoint(30, 30, 0), 5);
            model.Add(circle);
            model.Add(Make3D.MakeBox(new GeoPoint(50, 50, 0), 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis));
            return project;
        }

        private static T WithChoice<T>(string name, int selection, Func<T> action)
        {
            bool existed = Settings.GlobalSettings.ContainsSetting(name);
            int old = Settings.GlobalSettings.GetIntValue(name, 0);
            Settings.GlobalSettings.SetValue(name, selection);
            try { return action(); }
            finally
            {
                if (existed) Settings.GlobalSettings.SetValue(name, old);
                else Settings.GlobalSettings.RemoveSetting(name);
            }
        }

        private Project ExportAndRead(Project project, string format)
        {
            string file = TempFile(format);
            try
            {
                Assert.IsTrue(project.Export(file, format), $"{format} was not written");
                return Project.ReadFromFile(file, format);
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static void AssertSameContents(Model expected, Model actual, string what)
        {
            Assert.AreEqual(TypeCounts(expected), TypeCounts(actual), what);
            BoundingCube e = expected.Extent, a = actual.Extent;
            double tolerance = 1e-6 * Math.Max(1.0, e.Size);
            Assert.IsTrue(Math.Abs(e.Xmin - a.Xmin) < tolerance && Math.Abs(e.Xmax - a.Xmax) < tolerance
                && Math.Abs(e.Ymin - a.Ymin) < tolerance && Math.Abs(e.Ymax - a.Ymax) < tolerance
                && Math.Abs(e.Zmin - a.Zmin) < tolerance && Math.Abs(e.Zmax - a.Zmax) < tolerance,
                $"{what}: extent {a} instead of {e}");
        }

        /// <summary>
        /// A DWG written by Project.Export reads back as the same objects as the DXF of the same project.
        /// With the default version (AutoCAD 2000) this needs the DWG to be written as AutoCAD 2004, the
        /// DwgWriter of ACadSharp drops the polyface meshes, and so the box, in AutoCAD 2000 DWG.
        /// </summary>
        [DataTestMethod]
        [DataRow("Dxf", "0583667_008.dxf")]
        [DataRow("Dxf", "BlockXTestFile.dxf")]
        [DataRow("Dxf", "BlockRefXTestFile.dxf")]
        [DataRow("Dxf", "issue143.dxf")]
        [DataRow("Dxf", "issue194.dxf")]
        [DataRow("Dxf", "issue204.dxf")]
        [DataRow("Dxf", "Z273.dxf")]
        [DataRow("Step", "issue101.stp")]
        [DataRow(null, null)]
        public void dwg_export_reads_back_like_the_dxf_export(string folder, string fileName)
        {
            Project project;
            if (fileName == null) project = SyntheticProject();
            else
            {
                string file = TestFile(folder, fileName);
                Assert.IsTrue(File.Exists(file), $"test file not found: {file}");
                project = Project.ReadFromFile(file, System.IO.Path.GetExtension(file).TrimStart('.'));
            }
            Model fromDxf = ExportAndRead(project, "dxf").GetActiveModel();
            Model fromDwg = ExportAndRead(project, "dwg").GetActiveModel();
            Assert.IsTrue(fromDwg.Count > 0, "the DWG is empty");
            AssertSameContents(fromDxf, fromDwg, fileName ?? "synthetic project");
            if (fileName == null) Assert.AreEqual(1, fromDwg.AllObjects.Count(o => o is Shell || o is Solid), "the box is missing");
        }

        /// <summary>
        /// The setting "DxfDwg.Version" also chooses the DWG version, but versions ACadSharp cannot write
        /// as DWG, or not without losing the polyface meshes, are raised: up to AutoCAD 2000 to 2004, 2007
        /// to 2010.
        /// </summary>
        [DataTestMethod]
        [DoNotParallelize]
        [DataRow(5, ACadVersion.AC1018)]  // 13
        [DataRow(6, ACadVersion.AC1018)]  // 14
        [DataRow(7, ACadVersion.AC1018)]  // 2000, the default
        [DataRow(8, ACadVersion.AC1018)]  // 2004
        [DataRow(9, ACadVersion.AC1024)]  // 2007
        [DataRow(10, ACadVersion.AC1024)] // 2010
        [DataRow(11, ACadVersion.AC1027)] // 2013
        [DataRow(12, ACadVersion.AC1032)] // 2018
        [DataRow(0, ACadVersion.AC1018)]  // 2.5, not writable as DXF either
        public void dwg_version_follows_the_setting_where_acadsharp_can_write_it(int selection, ACadVersion expected)
        {
            Project project = SyntheticProject();
            string file = TempFile("dwg");
            try
            {
                Assert.IsTrue(WithChoice("DxfDwg.Version", selection, () => project.Export(file, "dwg")));
                CadDocument doc;
                using (DwgReader reader = new DwgReader(file, null)) doc = reader.Read();
                Assert.AreEqual(expected, doc.Header.Version);
                Model back = Project.ReadFromFile(file, "dwg").GetActiveModel();
                Assert.AreEqual(1, back.AllObjects.Count(o => o is Shell || o is Solid), "the box is missing");
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Reading a DWG left the file open, because the DwgReader was never disposed: the file could not be
        /// written or deleted until the garbage collector finalized the reader.
        /// </summary>
        [TestMethod]
        public void reading_a_dwg_closes_the_file()
        {
            string file = TempFile("dwg");
            try
            {
                Assert.IsTrue(SyntheticProject().Export(file, "dwg"));
                Assert.IsTrue(Project.ReadFromFile(file, "dwg").GetActiveModel().Count > 0);
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// "DxfDwg.Format" (DWG/DXF/DXB) and "DxfDwg.Text" (Fit/Original) were shown in the control center,
        /// but read nowhere.
        /// </summary>
        [TestMethod]
        public void unused_dxf_dwg_settings_are_gone()
        {
            Assert.IsTrue(Settings.GlobalSettings.ContainsSetting("DxfDwg.Version"));
            Assert.IsFalse(Settings.GlobalSettings.ContainsSetting("DxfDwg.Format"));
            Assert.IsFalse(Settings.GlobalSettings.ContainsSetting("DxfDwg.Text"));
        }
    }
}
