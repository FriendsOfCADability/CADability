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

        /// <summary>
        /// WriteToFileWithoutUserData copied the project through the binary serialization, which no longer exists on .NET 5
        /// and later, so it always returned false and wrote nothing. It copies through JSON now.
        /// </summary>
        [TestMethod]
        public void a_project_can_be_written_without_its_user_data()
        {
            Project project = Project.CreateSimpleProject();
            Line line = Line.TwoPoints(GeoPoint.Origin, new GeoPoint(10, 0, 0));
            line.UserData.Add("secret", "not in the file");
            project.GetActiveModel().Add(line);
            project.UserData.Add("project secret", "not in the file either");
            string file = System.IO.Path.Combine(TestContext.TestRunResultsDirectory ?? System.IO.Path.GetTempPath(), $"without-user-data-{Guid.NewGuid():N}.cdb.json");
            try
            {
                Assert.IsTrue(project.WriteToFileWithoutUserData(file));
                Project read = Project.ReadFromFile(file, "cdb");
                Line readLine = read.GetActiveModel().AllObjects.OfType<Line>().Single();
                Assert.AreEqual(0.0, readLine.Length - 10.0, 1e-12);
                Assert.IsNull(readLine.UserData.GetData("secret"));
                Assert.IsNull(read.UserData.GetData("project secret"));
                Assert.AreEqual("not in the file", line.UserData.GetData("secret"), "the project itself keeps its user data");
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
