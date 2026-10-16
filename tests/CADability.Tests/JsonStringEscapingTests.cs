using CADability.Attribute;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="JsonSerialize"/> wrote strings without escaping backslashes, but read "\r" and "\n" as control characters,
    /// so a Windows path like C:\Users\runneradmin\new\image.png did not survive saving and reading. Files now escape
    /// backslashes and say so in their header ("StringEscaping"); files without it are read as before.
    /// </summary>
    [TestClass]
    public class JsonStringEscapingTests
    {
        private static readonly string[] Strings =
        {
            @"C:\Users\runneradmin\new\image.png",
            @"\\server\share\file.cdb",
            @"ends with a backslash\",
            @"ends with two backslashes\\",
            "a \"quoted\" word",
            @"a backslash before a quote \""",
            "two\nlines and a\rcarriage return",
            @"literal \n and \r and \t",
            "#starts with a hash",
            "$starts with a dollar",
        };

        private static string WriteLayers(IEnumerable<string> names)
        {
            Project project = Project.CreateSimpleProject();
            foreach (string name in names) project.LayerList.Add(new Layer(name));
            MemoryStream written = new MemoryStream();
            project.WriteToJson(written);
            return System.Text.Encoding.UTF8.GetString(written.ToArray());
        }
        private static string[] ReadLayers(string json)
        {
            Project read = Project.ReadFromJson(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));
            return read.LayerList.ToArray().Select(l => l.Name).ToArray();
        }

        [TestMethod]
        public void StringsSurviveARoundTrip()
        {
            string json = WriteLayers(Strings);
            StringAssert.Contains(json, "\"StringEscaping\":1");
            string[] names = ReadLayers(json);
            foreach (string s in Strings) CollectionAssert.Contains(names, s, s);
        }

        [TestMethod]
        public void FilesWithoutTheMarkerAreReadAsBefore()
        {
            // a file of an older version: backslashes are not escaped, only \", \n and \r are resolved
            string json = WriteLayers(new[] { "placeholder" }).Replace(",\"StringEscaping\":1", "");
            Assert.IsFalse(json.Contains("StringEscaping"));
            json = json.Replace("\"placeholder\"", "\"\\\\server\\share\\new \\\"x\\\"\"");
            CollectionAssert.Contains(ReadLayers(json), "\\\\server\\share\new \"x\"");
        }

        [TestMethod]
        public void UnknownEscapesAreKept()
        {
            Assert.AreEqual(@"C:\temp\data", JsonSerialize.Unescape(@"C:\temp\data"));
            Assert.AreEqual("a\\b\"c\nd\re", JsonSerialize.Unescape(@"a\\b\""c\nd\re"));
            Assert.AreEqual(@"end\", JsonSerialize.Unescape(@"end\"));
        }
    }
}
