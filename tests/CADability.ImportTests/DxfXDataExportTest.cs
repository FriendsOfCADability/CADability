using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.DXF;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// The DXF export threw "An item with the same key has already been added" when two
    /// UserData entries of one object carried XData of the same application. UserData is keyed
    /// by free names, so this happens e.g. with an "ACAD" entry from an ACadSharp import next to
    /// an "ACAD:ACAD" entry from an older netDxf import, or with plain UserData values (which go
    /// to the CADABILITY application) next to an ExtendedEntityData named "CADABILITY".
    /// netDxf merged the records of such entries, so the export must do the same.
    /// </summary>
    [TestClass]
    public class DxfXDataExportTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void export_dxf_merges_xdata_entries_of_the_same_application()
        {
            Line line = MakeLine();
            line.UserData.Add("ACAD", MakeXData("ACAD", "first"));
            line.UserData.Add("ACAD:ACAD", MakeXData("ACAD", "second"));

            ExtendedEntityData back = ReimportXData(Export(line), "ACAD");
            CollectionAssert.AreEqual(new object[] { "first", "second" }, Values(back));
        }

        [TestMethod]
        public void export_dxf_merges_plain_userdata_with_cadability_xdata()
        {
            Line line = MakeLine();
            // the plain value comes first, so the CADABILITY group already exists when the
            // ExtendedEntityData of the same application is written
            line.UserData.Add("OriginalFileName", "part.geo");
            line.UserData.Add("CADABILITY", MakeXData("CADABILITY", "imported"));

            ExtendedEntityData back = ReimportXData(Export(line), "CADABILITY");
            CollectionAssert.AreEqual(new object[] { "part.geo", "imported" }, Values(back));
        }

        // AppId names are case-insensitive in DXF, so "acad" and "ACAD" are one application.
        [TestMethod]
        public void export_dxf_treats_application_names_case_insensitively()
        {
            Line line = MakeLine();
            line.UserData.Add("ACAD", MakeXData("ACAD", "upper"));
            line.UserData.Add("acad", MakeXData("acad", "lower"));

            ExtendedEntityData back = ReimportXData(Export(line), "ACAD");
            CollectionAssert.AreEqual(new object[] { "upper", "lower" }, Values(back));
        }

        private static Line MakeLine()
        {
            Line line = Line.Construct();
            line.SetTwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0));
            return line;
        }

        private static ExtendedEntityData MakeXData(string application, string value)
        {
            ExtendedEntityData xData = new ExtendedEntityData { ApplicationName = application };
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.String, value));
            return xData;
        }

        private static object[] Values(ExtendedEntityData xData)
        {
            List<object> values = new List<object>();
            foreach (KeyValuePair<XDataCode, object> item in xData.Data)
                if (item.Key == XDataCode.String) values.Add(item.Value);
            return values.ToArray();
        }

        private string Export(IGeoObject geoObject)
        {
            Project project = Project.CreateSimpleProject();
            project.GetModel(0).Add(geoObject.Clone());
            string file = this.TestContext.TestName + ".dxf";
            Assert.IsTrue(project.Export(file, "dxf"), "export must succeed");
            return file;
        }

        private static ExtendedEntityData ReimportXData(string file, string application)
        {
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            Model model = project.GetActiveModel();
            Assert.AreEqual(1, model.AllObjects.Count, "one entity expected after re-import");
            ExtendedEntityData xData = model.AllObjects[0].UserData.GetData(application) as ExtendedEntityData;
            Assert.IsNotNull(xData, "the re-imported entity must carry XData of " + application);
            return xData;
        }
    }
}
