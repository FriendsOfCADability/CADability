using CADability.GeoObject;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CADability.Tests
{
    /// <summary>
    /// A file with objects of a type that is unknown when it is read (e.g. user data from an assembly that is not loaded) is read
    /// with <c>JsonProxyType</c> objects in their place. Saving such a project used to throw an InvalidCastException, because the
    /// type version is read as double; and the type properties were written twice, the type index replaced by the type version.
    /// </summary>
    [TestClass]
    public class JsonUnknownTypeTests
    {
        public class FirstPayload : IJsonSerialize
        {
            public double Value;
            public FirstPayload() { }
            public void GetObjectData(IJsonWriteData data) => data.AddProperty("Value", Value);
            public void SetObjectData(IJsonReadData data) => Value = data.GetProperty<double>("Value");
        }

        public class SecondPayload : IJsonSerialize
        {
            public string Text;
            public SecondPayload() { }
            public void GetObjectData(IJsonWriteData data) => data.AddProperty("Text", Text);
            public void SetObjectData(IJsonReadData data) => Text = data.GetProperty<string>("Text");
        }

        private static readonly string firstName = typeof(FirstPayload).FullName!;
        private static readonly string secondName = typeof(SecondPayload).FullName!;

        private static string Write(Project project)
        {
            using MemoryStream stream = new MemoryStream();
            project.WriteToJson(stream);
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static Project Read(string json) => Project.ReadFromJson(new MemoryStream(Encoding.UTF8.GetBytes(json)));

        /// <summary>
        /// The user data of all objects of the model as "value/text", or the type name where a payload is not of its own type.
        /// </summary>
        private static string Describe(Project project)
        {
            return string.Join(" ", project.GetActiveModel().Cast<IGeoObject>().Select(g =>
            {
                object first = g.UserData.GetData("first"), second = g.UserData.GetData("second");
                string a = first is FirstPayload f ? f.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : first?.GetType().Name ?? "null";
                string b = second is SecondPayload s ? s.Text : second?.GetType().Name ?? "null";
                return a + "/" + b;
            }));
        }

        /// <summary>
        /// A project with three lines, each with user data of two types, saved with those types renamed to types that do not exist.
        /// </summary>
        private static string FileWithUnknownTypes()
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            for (int i = 0; i < 3; i++)
            {
                Line line = Line.TwoPoints(new GeoPoint(i, 0, 0), new GeoPoint(i, 1, 0));
                line.UserData.Add("first", new FirstPayload { Value = i + 0.5 });
                line.UserData.Add("second", new SecondPayload { Text = "t" + i });
                model.Add(line);
            }
            string json = Write(project);
            Assert.IsTrue(json.Contains(firstName) && json.Contains(secondName));
            return json.Replace(firstName, "Missing.FirstPayload").Replace(secondName, "Missing.SecondPayload");
        }

        [TestMethod]
        public void ProjectWithUnknownTypesCanBeSavedAndReadAgain()
        {
            Project read = Read(FileWithUnknownTypes());
            Assert.AreEqual("JsonProxyType/JsonProxyType JsonProxyType/JsonProxyType JsonProxyType/JsonProxyType", Describe(read));

            string saved = Write(read); // threw an InvalidCastException
            // each unknown type is defined once, with its own type index, and no object has a property twice
            Assert.AreEqual(1, Regex.Matches(saved, "\"\\$Type\":\"Missing.FirstPayload\"").Count);
            Assert.AreEqual(1, Regex.Matches(saved, "\"\\$Type\":\"Missing.SecondPayload\"").Count);
            Assert.IsFalse(Regex.IsMatch(saved, "\"\\$TypeIndex\":[0-9]+,[^{}]*\"\\$TypeIndex\""), "an object has its type index twice");

            Project readAgain = Read(saved);
            Assert.AreEqual(Describe(read), Describe(readAgain));
            Assert.AreEqual(saved, Write(readAgain), "saving again gives the same file");
        }

        [TestMethod]
        public void UnknownTypesSurviveSavingWhenTheyAreKnownAgain()
        {
            string saved = Write(Read(FileWithUnknownTypes()));
            string known = saved.Replace("Missing.FirstPayload", firstName).Replace("Missing.SecondPayload", secondName);
            Assert.AreEqual("0.5/t0 1.5/t1 2.5/t2", Describe(Read(known)));
        }

        [TestMethod]
        public void StringSetWithoutStringsIsReadEmpty()
        {
            string json = JsonSerialize.ToString(new SerializableHashSetString(new[] { "a", "b" }));
            string withoutStrings = Regex.Replace(json, ",?\"Strings\":\\[[^\\]]*\\]", "");
            Assert.AreNotEqual(json, withoutStrings, "the test needs to remove the strings");
            SerializableHashSetString read = (SerializableHashSetString)JsonSerialize.FromString(withoutStrings);
            Assert.AreEqual(0, read.Count);
        }
    }
}
