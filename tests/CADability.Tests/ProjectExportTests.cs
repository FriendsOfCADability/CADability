using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Project.Export returned false for STEP and STL, which it wrote, and true for dwg, dxb and html, which it did not write. DWG is written now.
    /// </summary>
    [TestClass]
    public class ProjectExportTests
    {
        public TestContext TestContext { get; set; }

        [DataTestMethod]
        [DataRow("dxf", true)]
        [DataRow("step", true)]
        [DataRow("stp", true)]
        [DataRow("stl", true)]
        [DataRow("dwg", true)]
        [DataRow("dxb", false)]
        [DataRow("html", false)]
        [DataRow("iges", false)]
        public void export_returns_whether_the_file_was_written(string format, bool written)
        {
            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis));
            string file = System.IO.Path.Combine(TestContext.TestRunResultsDirectory ?? System.IO.Path.GetTempPath(), $"export-{Guid.NewGuid():N}.{format}");
            try
            {
                Assert.AreEqual(written, project.Export(file, format));
                Assert.AreEqual(written, File.Exists(file) && new FileInfo(file).Length > 0);
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
