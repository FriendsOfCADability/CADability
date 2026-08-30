using System.IO;
using System.Text;

namespace CADability.Tests
{
    /// <summary>
    /// BoundingCube was renamed to BoundingBox. Both persistence formats record type names, so
    /// files written before the rename carry the old one and would otherwise become unreadable.
    /// These tests pin the mapping that keeps them readable - remove it and they fail with
    /// "unable to deserialize: CADability.BoundingCube".
    /// </summary>
    [TestClass]
    public class RenamedTypeCompatibilityTests
    {
        /// <summary>
        /// A Model as an older CADability wrote it: through ISerializable, which the file marks
        /// with "$TypeVersion": -1. On that path every value carries its own type name, so
        /// MinExtend appears as {"$Type": ..., "$Value": [...]} - and in a file written before the
        /// rename that name is CADability.BoundingCube. %TYPE% is filled in per test.
        /// </summary>
        private const string ModelWrittenViaISerializable =
            "{\"CADability\":{\"URL\":\"http://www.cadability.de\",\"Version\":\"1.0.0.0\",\"Assembly\":\"CADability\"},\"Entities\":[" +
            "{\"$Index(Debug)\":0,\"$Type\":\"CADability.Model\",\"$TypeIndex\":1,\"$TypeVersion\":-1," +
            "\"GeoObjectList\":\"#1\",\"Name\":\"Standard model\"," +
            "\"Unit\":{\"$Type\":\"CADability.Model+Units\",\"$Value\":\"millimeter\"}," +
            "\"DefaultScale\":{\"$Type\":\"Double\",\"$Value\":\"1\"}," +
            "\"LineStyleScale\":{\"$Type\":\"Double\",\"$Value\":\"1\"}," +
            "\"MinExtend\":{\"$Type\":\"%TYPE%\",\"$Value\":[1,2,3,4,5,6]}," +
            "\"AllDrives\":null,\"AllSchedules\":null,\"UserData\":null}," +
            "{\"$Index(Debug)\":1,\"$Type\":\"CADability.GeoObject.GeoObjectList\",\"$TypeIndex\":2,\"$TypeVersion\":0,\"List\":[],\"UserData\":\"#2\"}," +
            "{\"$Index(Debug)\":2,\"$Type\":\"CADability.UserData\",\"$TypeIndex\":3,\"$TypeVersion\":0}]}";

        private static Model ReadModel(string typeName)
        {
            string document = ModelWrittenViaISerializable.Replace("%TYPE%", typeName);
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(document)))
            {
                return new JsonSerialize().FromStream(stream) as Model;
            }
        }

        private static void AssertIsTheExpectedBox(Model model)
        {
            Assert.IsNotNull(model, "the document did not deserialize into a Model");
            BoundingBox box = model.MinExtend;
            Assert.AreEqual(1.0, box.Xmin, "Xmin");
            Assert.AreEqual(2.0, box.Xmax, "Xmax");
            Assert.AreEqual(3.0, box.Ymin, "Ymin");
            Assert.AreEqual(4.0, box.Ymax, "Ymax");
            Assert.AreEqual(5.0, box.Zmin, "Zmin");
            Assert.AreEqual(6.0, box.Zmax, "Zmax");
        }

        [TestMethod]
        public void json_written_before_the_rename_still_reads()
        {
            AssertIsTheExpectedBox(ReadModel("CADability.BoundingCube"));
        }

        [TestMethod]
        public void json_written_after_the_rename_reads_as_well()
        {
            AssertIsTheExpectedBox(ReadModel("CADability.BoundingBox"));
        }
    }
}
