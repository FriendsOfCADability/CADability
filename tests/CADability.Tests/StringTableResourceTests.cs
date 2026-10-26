using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using CADability.UserInterface;

namespace CADability.Tests
{
    /// <summary>
    /// Consistency of the embedded string tables (StringTableDeutsch.xml and StringTableEnglish.xml): both languages
    /// define the same ids, no id occurs twice (the later entry silently replaced the earlier one), separator lists
    /// and placeholders agree, and the default styles find the colors, line widths, line patterns and layers they
    /// name.
    /// </summary>
    [TestClass]
    public class StringTableResourceTests
    {
        private static readonly string[] Resources = { "CADability.StringTableDeutsch.xml", "CADability.StringTableEnglish.xml" };

        private static XmlDocument Load(string resource)
        {
            using Stream stream = typeof(StringTable).Assembly.GetManifestResourceStream(resource);
            Assert.IsNotNull(stream, resource);
            XmlDocument document = new XmlDocument();
            document.Load(stream);
            return document;
        }

        private static Dictionary<string, XmlNode> Entries(string resource)
        {
            Dictionary<string, XmlNode> result = new Dictionary<string, XmlNode>();
            foreach (XmlNode data in Load(resource).SelectNodes("root/data"))
                result[data.Attributes["name"].Value] = data;
            return result;
        }

        private static string Text(XmlNode data, string category) => data.SelectSingleNode(category)?.InnerText;

        [TestMethod]
        public void NoIdIsDefinedTwice()
        {
            foreach (string resource in Resources)
            {
                List<string> twice = Load(resource).SelectNodes("root/data").Cast<XmlNode>()
                    .GroupBy(d => d.Attributes["name"].Value).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                Assert.AreEqual(0, twice.Count, resource + ": " + string.Join(", ", twice));
            }
        }

        [TestMethod]
        public void BothLanguagesDefineTheSameIds()
        {
            Dictionary<string, XmlNode> german = Entries(Resources[0]);
            Dictionary<string, XmlNode> english = Entries(Resources[1]);
            Assert.AreEqual(0, german.Keys.Except(english.Keys).Count(), "missing in English: " + string.Join(", ", german.Keys.Except(english.Keys)));
            Assert.AreEqual(0, english.Keys.Except(german.Keys).Count(), "missing in German: " + string.Join(", ", english.Keys.Except(german.Keys)));
        }

        [TestMethod]
        public void ListsAndPlaceholdersAgree()
        {
            Dictionary<string, XmlNode> german = Entries(Resources[0]);
            Dictionary<string, XmlNode> english = Entries(Resources[1]);
            Regex placeholder = new Regex(@"\{\d+[^}]*\}");
            List<string> errors = new List<string>();
            foreach (KeyValuePair<string, XmlNode> entry in english)
            {
                if (!german.TryGetValue(entry.Key, out XmlNode de)) continue;
                foreach (string category in new[] { "label", "tip", "info" })
                {
                    string en = Text(entry.Value, category), other = Text(de, category);
                    if (string.IsNullOrEmpty(en) || string.IsNullOrEmpty(other)) continue;
                    if (en[0] == '|' && (other[0] != '|' || en.Split('|').Length != other.Split('|').Length))
                        errors.Add(entry.Key + ": " + en + " / " + other);
                    IEnumerable<string> a = placeholder.Matches(en).Cast<Match>().Select(m => m.Value).OrderBy(s => s);
                    IEnumerable<string> b = placeholder.Matches(other).Cast<Match>().Select(m => m.Value).OrderBy(s => s);
                    if (!a.SequenceEqual(b)) errors.Add(entry.Key + ": " + en + " / " + other);
                }
            }
            Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
        }

        [TestMethod]
        public void DefaultStylesFindTheirAttributes()
        {
            foreach (string resource in Resources)
            {
                Dictionary<string, XmlNode> entries = Entries(resource);
                HashSet<string> Names(string id, char end) => new HashSet<string>(Text(entries[id], "label").Split('|').Skip(1)
                    .Select(item => item.IndexOf(end) < 0 ? item : item.Substring(0, item.IndexOf(end))));
                HashSet<string>[] lists =
                {
                    Names("LayerList.Default", '\0'),
                    Names("ColorList.Default", '('),
                    Names("LineWidthList.Default", ':'),
                    Names("LinePatternList.Default", ':'),
                };
                foreach (string style in Text(entries["StyleList.Default"], "label").Split('|').Skip(1))
                {
                    string[] attributes = style.Split(':')[1].Split(',');
                    for (int i = 0; i < lists.Length; i++)
                    {
                        if (attributes[i] != "-")
                            Assert.IsTrue(lists[i].Contains(attributes[i]), resource + ": style " + style + " names unknown " + attributes[i]);
                    }
                }
            }
        }
    }
}
