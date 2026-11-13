using CADability.Attribute;
using CADability.GeoObject;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="JsonSerialize"/> reads the <see cref="JsonVersion"/> attribute and the <see cref="SerializableAttribute"/>
    /// of a type once and keeps the answer. The JSON it writes must be exactly what it wrote when it read the attributes by
    /// reflection for every value (<see cref="JsonSerialize.BypassAttributeCache"/>), and reading must give the same
    /// objects.
    /// </summary>
    [TestClass]
    public class JsonSerializeAttributeCacheTests
    {
        private static string File(string folder, string name, [CallerFilePath] string thisFile = "")
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile), "Files", folder, name);

        /// <summary>
        /// A serializable value for UserData, which is written via the [Serializable] path of the serializer.
        /// </summary>
        [Serializable]
        public class SerializableTag
        {
            public string Name { get; set; }
            public int Number { get; set; }
            public SerializableTag() { }
        }

        private static Project SimpleProject(bool serializableUserData)
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            Layer contour = project.LayerList.CreateOrFind("Contour");
            Layer marking = project.LayerList.CreateOrFind("Marking");
            ColorDef red = project.ColorList.CreateOrFind("Red", System.Drawing.Color.Red);
            ColorDef blue = project.ColorList.CreateOrFind("Blue", System.Drawing.Color.Blue);
            GeoPoint[] p = { new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0), new GeoPoint(100, 60, 0), new GeoPoint(20, 60, 0), new GeoPoint(0, 40, 0) };
            for (int i = 0; i < p.Length; i++)
            {
                Line line = Line.Construct();
                line.SetTwoPoints(p[i], p[(i + 1) % p.Length]);
                line.Layer = contour;
                line.ColorDef = red;
                line.UserData.Add("Id", i);
                if (serializableUserData) line.UserData.Add("Tag", new SerializableTag { Name = "edge", Number = i });
                line.UserData.Add("Weights", new double[] { i, 2.5 * i });
                model.Add(line);
            }
            for (int i = 0; i < 3; i++)
            {
                Ellipse arc = Ellipse.Construct();
                arc.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, new GeoPoint(30 + 20 * i, 30, 0), 5, 0, Math.PI);
                arc.Layer = marking;
                arc.ColorDef = blue;
                arc.UserData.Add("Name", "arc " + i);
                model.Add(arc);
            }
            Ellipse circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(Plane.XYPlane, new GeoPoint(70, 30, 0), 8);
            circle.Layer = contour;
            circle.ColorDef = red;
            model.Add(circle);

            Text text = Text.Construct();
            text.Font = "Liberation Sans";
            text.TextString = "Part 4711";
            text.Location = new GeoPoint(10, 20, 0);
            text.LineDirection = GeoVector.XAxis;
            text.GlyphDirection = GeoVector.YAxis;
            text.TextSize = 5.0;
            model.Add(text);

            Block block = Block.Construct();
            block.Name = "Marker";
            Line l1 = Line.Construct();
            l1.SetTwoPoints(new GeoPoint(-1, 0, 0), new GeoPoint(1, 0, 0));
            Line l2 = Line.Construct();
            l2.SetTwoPoints(new GeoPoint(0, -1, 0), new GeoPoint(0, 1, 0));
            block.Add(l1);
            block.Add(l2);
            model.Add(block);
            BlockRef blockRef = BlockRef.Construct(block);
            blockRef.RefPoint = new GeoPoint(50, 50, 0);
            model.Add(blockRef);

            Dimension dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimPoints;
            dim.DimensionStyle = project.DimensionStyleList.Current;
            dim.Normal = GeoVector.ZAxis;
            dim.DimLineRef = new GeoPoint(0, -20, 0);
            dim.DimLineDirection = GeoVector.XAxis;
            dim.AddPoint(new GeoPoint(0, 0, 0));
            dim.AddPoint(new GeoPoint(100, 0, 0));
            model.Add(dim);
            return project;
        }

        private static IEnumerable<(string name, Func<Project> create)> Projects()
        {
            yield return ("simple project", () => SimpleProject(false));
            // [Serializable] objects in UserData are written through the reflection path of the serializer
            yield return (SerializableUserData, () => SimpleProject(true));
            // hatches, dimensions and texts
            yield return ("BVH_Bona.dxf", () => Project.ReadFromFile(File("Dxf", "BVH_Bona.dxf"), "dxf"));
            yield return ("0583667_008.dxf", () => Project.ReadFromFile(File("Dxf", "0583667_008.dxf"), "dxf"));
            // blocks and block references
            yield return ("BlockXTestFile.dxf", () => Project.ReadFromFile(File("Dxf", "BlockXTestFile.dxf"), "dxf"));
            yield return ("BlockRefXTestFile.dxf", () => Project.ReadFromFile(File("Dxf", "BlockRefXTestFile.dxf"), "dxf"));
            // projects saved by CADability, with solids, user data, pictures and page settings
            foreach (string cdb in new[] { "issue293.cdb.json", "Volumes.cdb.json", "pictures-1x.cdb.json", "pagesettings-1x-net48.cdb.json" })
            {
                yield return (cdb, () =>
                {
                    using (FileStream stream = System.IO.File.OpenRead(File("CDB", cdb))) return Project.ReadFromJson(stream);
                });
            }
        }

        private static byte[] Write(Project project, bool cached)
        {
            bool old = JsonSerialize.BypassAttributeCache;
            JsonSerialize.BypassAttributeCache = !cached;
            try
            {
                MemoryStream stream = new MemoryStream();
                project.WriteToJson(stream);
                return stream.ToArray();
            }
            finally
            {
                JsonSerialize.BypassAttributeCache = old;
            }
        }

        private static Project Read(byte[] json, bool cached)
        {
            bool old = JsonSerialize.BypassAttributeCache;
            JsonSerialize.BypassAttributeCache = !cached;
            try
            {
                return Project.ReadFromJson(new MemoryStream(json));
            }
            finally
            {
                JsonSerialize.BypassAttributeCache = old;
            }
        }

        private static void AssertSameBytes(byte[] expected, byte[] actual, string what)
        {
            if (expected.AsSpan().SequenceEqual(actual)) return;
            int i = 0;
            while (i < Math.Min(expected.Length, actual.Length) && expected[i] == actual[i]) i++;
            int from = Math.Max(0, i - 80);
            Assert.Fail(what + ": the JSON differs at byte " + i + " (lengths " + expected.Length + " and " + actual.Length + ")\nexpected: "
                + Encoding.UTF8.GetString(expected, from, Math.Min(160, expected.Length - from))
                + "\nactual:   " + Encoding.UTF8.GetString(actual, from, Math.Min(160, actual.Length - from)));
        }

        [TestMethod]
        public void CachedAttributes_WriteTheSameJson()
        {
            foreach (var (name, create) in Projects())
            {
                Project project = create();
                Assert.IsNotNull(project, name);
                byte[] uncached = Write(project, false);
                byte[] cached = Write(project, true);
                byte[] uncachedAgain = Write(project, false);
                AssertSameBytes(uncached, uncachedAgain, name + ", written twice without the cache");
                AssertSameBytes(uncached, cached, name + ", written with and without the cache");
            }
        }

        /// <summary>
        /// The JSON in a form that does not depend on the order in which the objects are written: a Hashtable or Dictionary
        /// keyed by objects (e.g. layers) is written in the order of the hash codes of its keys, which differ between two
        /// projects read from the same file, and that order also decides the indices of the objects and types. Here each
        /// entity is written with the name of its type instead of the type index, without its own index and with every
        /// reference "#n" replaced by "#"; the entities and the entries of each dictionary are sorted (a Hashtable also
        /// changes the order of string keys when it is rebuilt). The versions of the types are compared separately.
        /// </summary>
        private static (List<string> entities, List<string> versions) OrderIndependent(byte[] json)
        {
            // CADability writes infinite and undefined doubles as bare words, which System.Text.Json does not accept
            string text = Regex.Replace(Encoding.UTF8.GetString(json), "(?<=[:\\[,])(-?Infinity|NaN)(?=[,\\]}])", "\"$1\"");
            JsonObject root = JsonNode.Parse(text).AsObject();
            JsonArray entities = root["Entities"].AsArray();
            Dictionary<int, string> typeNames = new Dictionary<int, string>();
            List<string> versions = new List<string>();
            foreach (JsonNode e in entities)
            {
                JsonObject o = e.AsObject();
                if (o.TryGetPropertyValue("$Type", out JsonNode type))
                {
                    typeNames[(int)o["$TypeIndex"]] = (string)type + "," + (string)o["$Assembly"];
                    versions.Add((string)type + ": " + o["$TypeVersion"]?.ToJsonString());
                }
            }
            List<string> res = new List<string>();
            foreach (JsonNode e in entities)
            {
                JsonObject o = e.AsObject();
                string typeName = typeNames[(int)o["$TypeIndex"]];
                foreach (string key in new[] { "$Type", "$Assembly", "$TypeVersion", "$TypeIndex", "$Index(Debug)", "$Type(Debug)" }) o.Remove(key);
                o["$TypeName"] = typeName;
                if (o["$Entries"] is JsonArray entries)
                {   // the entries of a dictionary in an order of their own (with the value types in the same order)
                    JsonArray valueTypes = o["$ValueTypes"] as JsonArray;
                    if (valueTypes != null && valueTypes.Count != entries.Count) valueTypes = null;
                    int[] order = Enumerable.Range(0, entries.Count)
                        .OrderBy(i => Regex.Replace(entries[i]?.ToJsonString() ?? "null", "\"#[0-9]+\"", "\"#\"") + (valueTypes?[i]?.ToJsonString() ?? ""), StringComparer.Ordinal).ToArray();
                    JsonArray sortedEntries = new JsonArray(order.Select(i => entries[i]?.DeepClone()).ToArray());
                    o["$Entries"] = sortedEntries;
                    if (valueTypes != null) o["$ValueTypes"] = new JsonArray(order.Select(i => valueTypes[i]?.DeepClone()).ToArray());
                }
                res.Add(Regex.Replace(o.ToJsonString(), "\"#[0-9]+\"", "\"#\""));
            }
            res.Sort(StringComparer.Ordinal);
            versions.Sort(StringComparer.Ordinal);
            root.Remove("Entities");
            res.Add(root.ToJsonString());
            return (res, versions);
        }

        private static void AssertSameOrderIndependent(byte[] expected, byte[] actual, string what)
        {
            var (expectedEntities, expectedVersions) = OrderIndependent(expected);
            var (actualEntities, actualVersions) = OrderIndependent(actual);
            CollectionAssert.AreEqual(expectedVersions, actualVersions, what + ": type versions");
            Assert.AreEqual(expectedEntities.Count, actualEntities.Count, what + ": number of entities");
            for (int i = 0; i < expectedEntities.Count; i++) Assert.AreEqual(expectedEntities[i], actualEntities[i], what + ": entity");
        }

        /// <summary>
        /// Projects whose JSON changes in a round trip with and without the cache: a [Serializable] object in UserData is
        /// read back as a dictionary of its properties, and reading the DXF import normalizes a plane
        /// (0.99999999999999989 becomes 1).
        /// </summary>
        private static readonly HashSet<string> RoundTripChanges = new HashSet<string> { SerializableUserData, "0583667_008.dxf" };
        private const string SerializableUserData = "simple project with [Serializable] user data";

        [TestMethod]
        public void CachedAttributes_RoundTrip()
        {
            foreach (var (name, create) in Projects())
            {
                byte[] json = Write(create(), true);

                // the same project read from the JSON is written identically with and without the cache
                Project read = Read(json, true);
                AssertSameBytes(Write(read, false), Write(read, true), name + ", read project written with and without the cache");

                // reading with and without the cache gives the same projects. Two projects read from the same JSON may write
                // their hashtables in a different order (the order depends on hash codes, which for objects differ between
                // the two projects and for strings between processes), so where the bytes differ, the comparison ignores
                // the order of the objects.
                byte[] readUncached = Write(Read(json, false), false);
                byte[] readCached = Write(Read(json, true), true);
                if (!readUncached.AsSpan().SequenceEqual(readCached))
                {
                    AssertSameOrderIndependent(readUncached, readCached, name + ", read and written with and without the cache");
                }

                // the round trip itself, which does not depend on the cache
                if (RoundTripChanges.Contains(name) || json.AsSpan().SequenceEqual(readCached)) continue;
                AssertSameOrderIndependent(json, readCached, name + ", written, read and written again");
            }
        }

        [TestMethod]
        public void CachedAttributes_SameAnswersForAllTypes()
        {
            // the cache only remembers what reflection says about a type, so it must give the same answers for every type
            // that can occur, which are the types of CADability, of the base library and of this test assembly
            IEnumerable<Type> types = typeof(Project).Assembly.GetTypes()
                .Concat(typeof(object).Assembly.GetTypes())
                .Concat(typeof(JsonSerializeAttributeCacheTests).Assembly.GetTypes())
                .Concat(new[] { typeof(List<GeoPoint>), typeof(Dictionary<string, object>), typeof(GeoPoint[]), typeof(double[,]) });
            int count = 0;
            foreach (Type type in types)
            {
                bool expectedStruct, expectedSerializable;
                JsonSerialize.BypassAttributeCache = true;
                try
                {
                    expectedStruct = JsonSerialize.SerializeAsStruct(type);
                    expectedSerializable = JsonSerialize.HasSerializableAttribute(type);
                }
                finally
                {
                    JsonSerialize.BypassAttributeCache = false;
                }
                Assert.AreEqual(expectedStruct, JsonSerialize.SerializeAsStruct(type), type.FullName);
                Assert.AreEqual(expectedSerializable, JsonSerialize.HasSerializableAttribute(type), type.FullName);
                Assert.AreEqual(expectedStruct, JsonSerialize.SerializeAsStruct(type), type.FullName + ", second time");
                count++;
            }
            Assert.IsTrue(count > 1000);
        }

        [TestMethod]
        public void CachedAttributes_ProjectsContainWhatTheyShould()
        {
            // the test inputs really contain hatches, dimensions, texts and blocks
            HashSet<Type> types = new HashSet<Type>();
            foreach (var (name, create) in Projects())
            {
                Project project = create();
                for (int i = 0; i < project.GetModelCount(); i++)
                {
                    foreach (IGeoObject go in project.GetModel(i).AllObjects) Collect(go, types);
                }
            }
            Assert.IsTrue(types.Contains(typeof(Hatch)), "a hatch");
            Assert.IsTrue(types.Any(t => typeof(Dimension).IsAssignableFrom(t)), "a dimension");
            Assert.IsTrue(types.Any(t => typeof(Text).IsAssignableFrom(t)), "a text");
            Assert.IsTrue(types.Contains(typeof(Block)), "a block");
            Assert.IsTrue(types.Contains(typeof(BlockRef)), "a block reference");
        }

        private static void Collect(IGeoObject go, HashSet<Type> types)
        {
            types.Add(go.GetType());
            if (go is BlockRef br && br.ReferencedBlock != null) Collect(br.ReferencedBlock, types);
            if (go.NumChildren > 0)
            {
                for (int i = 0; i < go.NumChildren; i++) Collect(go.Child(i), types);
            }
        }

        [TestMethod]
        public void CachedAttributes_SameJsonFromParallelThreads()
        {
            Project project = SimpleProject(true);
            byte[] expected = Write(project, false);
            byte[][] results = new byte[64][];
            Parallel.For(0, results.Length, i =>
            {
                // every thread its own project, all of them share the cache
                results[i] = Write(SimpleProject(true), true);
                Project read = Read(results[i], true);
                Assert.IsNotNull(read);
            });
            for (int i = 0; i < results.Length; i++) AssertSameBytes(expected, results[i], "thread " + i);
        }

        // types for the attribute lookup: without attribute, with attribute, inheriting it, and with IJsonVersion

        public class WithoutVersion : IJsonSerialize
        {
            public int Value;
            public WithoutVersion() { }
            public void GetObjectData(IJsonWriteData data) => data.AddProperty("Value", Value);
            public void SetObjectData(IJsonReadData data) => Value = data.GetIntProperty("Value");
        }
        [JsonVersion(7)]
        public class WithVersion : WithoutVersion { }
        public class InheritsVersion : WithVersion { }
        [JsonVersion(serializeAsStruct = true, version = 2)]
        public class AsStruct : IJsonSerialize
        {
            public int Value;
            public AsStruct() { }
            protected AsStruct(IJsonReadStruct data) { Value = data.GetValue<int>(); }
            public void GetObjectData(IJsonWriteData data) => data.AddValues(Value);
            public void SetObjectData(IJsonReadData data) { }
        }
        public class ByInterface : WithoutVersion, IJsonVersion
        {
            public string GetAssemblyName() => "Other";
            public int GetTypeVersion(Type type) => 5;
        }
        public class Container : IJsonSerialize
        {
            public object[] Items;
            public Container() { }
            public void GetObjectData(IJsonWriteData data) => data.AddProperty("Items", Items);
            public void SetObjectData(IJsonReadData data) => Items = data.GetProperty<object[]>("Items");
        }

        [TestMethod]
        public void CachedAttributes_TypesWithAndWithoutJsonVersion()
        {
            foreach (Type type in new[] { typeof(WithoutVersion), typeof(WithVersion), typeof(InheritsVersion), typeof(AsStruct), typeof(ByInterface), typeof(int), typeof(GeoPoint), typeof(string) })
            {
                JsonSerialize.BypassAttributeCache = true;
                bool expected;
                try
                {
                    expected = JsonSerialize.SerializeAsStruct(type);
                }
                finally
                {
                    JsonSerialize.BypassAttributeCache = false;
                }
                Assert.AreEqual(expected, JsonSerialize.SerializeAsStruct(type), type.Name);
                Assert.AreEqual(expected, JsonSerialize.SerializeAsStruct(type), type.Name + ", second time");
            }
            Assert.IsTrue(JsonSerialize.SerializeAsStruct(typeof(AsStruct)));
            Assert.IsTrue(JsonSerialize.SerializeAsStruct(typeof(GeoPoint)));
            Assert.IsFalse(JsonSerialize.SerializeAsStruct(typeof(ByInterface)), "IJsonVersion is not evaluated");

            Container container = new Container
            {
                Items = new object[] { new WithoutVersion { Value = 1 }, new WithVersion { Value = 2 }, new InheritsVersion { Value = 3 }, new AsStruct { Value = 4 }, new ByInterface { Value = 5 } }
            };
            JsonSerialize.BypassAttributeCache = true;
            string expectedJson;
            try
            {
                expectedJson = JsonSerialize.ToString(container);
            }
            finally
            {
                JsonSerialize.BypassAttributeCache = false;
            }
            string json = JsonSerialize.ToString(container);
            Assert.AreEqual(expectedJson, json);
            // the versions as the attribute (inherited) says, 0 without attribute, also for IJsonVersion
            StringAssert.Contains(json, "WithVersion\",\"$Assembly\":\"CADability.Tests\",\"$TypeIndex\":3,\"$TypeVersion\":7,");
            StringAssert.Contains(json, "InheritsVersion\",\"$Assembly\":\"CADability.Tests\",\"$TypeIndex\":4,\"$TypeVersion\":7,");
            StringAssert.Contains(json, "ByInterface\",\"$Assembly\":\"CADability.Tests\",\"$TypeIndex\":5,\"$TypeVersion\":0,");
            StringAssert.Contains(json, "WithoutVersion\",\"$Assembly\":\"CADability.Tests\",\"$TypeIndex\":2,\"$TypeVersion\":0,");
            StringAssert.Contains(json, "\"Items\":[\"#1\",\"#2\",\"#3\",[4],\"#4\"]", "AsStruct is written inline");
            Container read = (Container)JsonSerialize.FromString(json);
            Assert.AreEqual(5, read.Items.Length);
            // without a typed context the inline struct is read back as its list of values, as it always was
            Assert.AreEqual(5, ((ByInterface)read.Items[4]).Value);
        }

        private static int CountOf(string s, string part)
        {
            int n = 0;
            for (int i = s.IndexOf(part); i >= 0; i = s.IndexOf(part, i + 1)) n++;
            return n;
        }

        [TestMethod]
        public void CachedAttributes_SerializableAttribute()
        {
            foreach (Type type in new[] { typeof(SerializableTag), typeof(WithoutVersion), typeof(int), typeof(string), typeof(double[]), typeof(DayOfWeek), typeof(Line) })
            {
                bool expected = System.Attribute.GetCustomAttribute(type, typeof(SerializableAttribute)) != null;
                Assert.AreEqual(expected, JsonSerialize.HasSerializableAttribute(type), type.Name);
                Assert.AreEqual(expected, JsonSerialize.HasSerializableAttribute(type), type.Name + ", second time");
            }
        }
    }
}
