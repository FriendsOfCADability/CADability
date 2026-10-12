using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.DXF;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// XData imported from DXF/DWG has to survive being saved as cdb and read back (#371).
    /// The import keeps ACadSharp's raw values: a control string (group code 1002) is a char
    /// '{' or '}', a 16 bit integer a short. The JSON writer wrote the char without quotes, so
    /// the cdb file was no valid JSON and could not be opened again.
    /// </summary>
    [TestClass]
    public class CdbXDataRoundtripTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void cdb_keeps_xdata_control_strings()
        {
            Line line = MakeLine();
            line.UserData.Add("AcadAnnotative", AnnotativeXData());

            ExtendedEntityData back = XDataOf(JsonRoundtrip(line), "AcadAnnotative");

            AssertSameXData(AnnotativeXData(), back);
        }

        // JSON reads every number as double. The values must come back with the types the
        // import gives them, not only with the same numbers.
        [TestMethod]
        public void cdb_keeps_xdata_value_types()
        {
            ExtendedEntityData xData = new ExtendedEntityData { ApplicationName = "ACAD" };
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.Int32, 70000));
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.Real, 2.5));
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.DatabaseHandle, (ulong)0x2708));
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.LayerName, (ulong)0x10));
            Line line = MakeLine();
            line.UserData.Add("ACAD", xData);

            ExtendedEntityData back = XDataOf(JsonRoundtrip(line), "ACAD");

            AssertSameXData(xData, back);
        }

        // The whole way a drawing takes: imported from DXF, saved as cdb, opened again and
        // exported to DXF. The XData at the end must be the XData from the first import.
        [TestMethod]
        public void dxf_xdata_survives_cdb_and_dxf_export()
        {
            Line line = MakeLine();
            line.UserData.Add("AcadAnnotative", AnnotativeXData());
            IGeoObject imported = ReimportSingleObject(Export(line, "first"));
            ExtendedEntityData fromDxf = XDataOf(imported, "AcadAnnotative");

            IGeoObject fromCdb = JsonRoundtrip(imported);
            ExtendedEntityData again = XDataOf(ReimportSingleObject(Export(fromCdb, "second")), "AcadAnnotative");

            AssertSameXData(fromDxf, again);
        }

        // What DXF.Import.SetUserData makes of AutoCAD's annotative data on a dimension
        private static ExtendedEntityData AnnotativeXData()
        {
            ExtendedEntityData xData = new ExtendedEntityData { ApplicationName = "AcadAnnotative" };
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.String, "AnnotativeData"));
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.ControlString, '{'));
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.Int16, (short)1));
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.Int16, (short)0));
            xData.Data.Add(new KeyValuePair<XDataCode, object>(XDataCode.ControlString, '}'));
            return xData;
        }

        private static void AssertSameXData(ExtendedEntityData expected, ExtendedEntityData actual)
        {
            Assert.AreEqual(expected.Data.Count, actual.Data.Count, "number of XData records");
            for (int i = 0; i < expected.Data.Count; i++)
            {
                Assert.AreEqual(expected.Data[i].Key, actual.Data[i].Key, "group code of record " + i);
                Assert.AreEqual(expected.Data[i].Value, actual.Data[i].Value, "value of record " + i);
            }
        }

        private static Line MakeLine()
        {
            Line line = Line.Construct();
            line.SetTwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0));
            return line;
        }

        private static IGeoObject JsonRoundtrip(IGeoObject geoObject)
        {
            MemoryStream stream = new MemoryStream();
            new JsonSerialize().ToStream(stream, geoObject, false);
            stream.Position = 0;
            IGeoObject back = new JsonSerialize().FromStream(stream) as IGeoObject;
            Assert.IsNotNull(back, "the object must be read back from JSON");
            return back;
        }

        private static ExtendedEntityData XDataOf(IGeoObject geoObject, string application)
        {
            ExtendedEntityData xData = geoObject.UserData.GetData(application) as ExtendedEntityData;
            Assert.IsNotNull(xData, "the object must carry XData of " + application);
            return xData;
        }

        private string Export(IGeoObject geoObject, string suffix)
        {
            Project project = Project.CreateSimpleProject();
            project.GetModel(0).Add(geoObject.Clone());
            string file = this.TestContext.TestName + "_" + suffix + ".dxf";
            Assert.IsTrue(project.Export(file, "dxf"), "export must succeed");
            return file;
        }

        private static IGeoObject ReimportSingleObject(string file)
        {
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            Model model = project.GetActiveModel();
            Assert.AreEqual(1, model.AllObjects.Count, "one entity expected after re-import");
            return model.AllObjects[0];
        }
    }
}
