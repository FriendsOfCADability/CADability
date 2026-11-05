using CADability.GeoObject;
using System.Collections;
using System.Collections.Generic;

namespace CADability.Tests
{
    /// <summary>
    /// Round trips through <see cref="JsonSerialize"/> for collections whose element type must survive the json file,
    /// which only knows untyped arrays. On reading, the type comes from the context: the property type requested by
    /// <see cref="IJsonReadData.GetProperty{T}(string)"/> or the value types recorded for a dictionary.
    /// </summary>
    [TestClass]
    public class JsonSerializeRoundTripTests
    {
        private static Solid Box(double x = 0.0)
        {
            return Make3D.MakeBox(new GeoPoint(x, 0, 0), GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis);
        }

        private static T RoundTrip<T>(T obj)
        {
            return (T)JsonSerialize.FromString(JsonSerialize.ToString(obj));
        }

        /// <summary>
        /// An IJsonSerialize object with collection properties of different nesting depth.
        /// </summary>
        public class Holder : IJsonSerialize
        {
            public List<Solid> Solids;
            public List<List<Solid>> NestedSolids;
            public Solid[][] JaggedSolids;
            public List<GeoPoint> Points;
            public List<int> Ints;
            public IList<Solid> SolidInterface;
            public Dictionary<string, object> Named;

            public Holder() { }
            public void GetObjectData(IJsonWriteData data)
            {
                data.AddProperty("Solids", Solids);
                data.AddProperty("NestedSolids", NestedSolids);
                data.AddProperty("JaggedSolids", JaggedSolids);
                data.AddProperty("Points", Points);
                data.AddProperty("Ints", Ints);
                data.AddProperty("SolidInterface", SolidInterface);
                data.AddProperty("Named", Named);
            }
            public void SetObjectData(IJsonReadData data)
            {
                Solids = data.GetProperty<List<Solid>>("Solids");
                NestedSolids = data.GetProperty<List<List<Solid>>>("NestedSolids");
                JaggedSolids = data.GetProperty<Solid[][]>("JaggedSolids");
                Points = data.GetProperty<List<GeoPoint>>("Points");
                Ints = data.GetProperty<List<int>>("Ints");
                SolidInterface = data.GetProperty<IList<Solid>>("SolidInterface");
                Named = data.GetProperty<Dictionary<string, object>>("Named");
            }
        }

        [TestMethod]
        public void DictionaryWithTypedListValues()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>
            {
                ["solids"] = new List<Solid> { Box(0), Box(2) },
                ["points"] = new List<GeoPoint> { new GeoPoint(1, 2, 3), new GeoPoint(4, 5, 6) },
                ["array"] = new Solid[] { Box(4) },
                ["single"] = Box(6),
                ["number"] = 1.5,
                ["vector"] = new GeoVector(1, 0, 0),
            };
            Dictionary<string, object> read = RoundTrip(dict);

            List<Solid> solids = read["solids"] as List<Solid>;
            Assert.IsNotNull(solids, "List<Solid> must be restored as List<Solid>");
            Assert.AreEqual(2, solids.Count);
            Assert.IsTrue(solids.TrueForAll(s => s != null));
            Assert.AreEqual(2.0, solids[1].GetExtent(0.0).Xmin, 1e-12);

            List<GeoPoint> points = read["points"] as List<GeoPoint>;
            Assert.IsNotNull(points, "List<GeoPoint> must be restored as List<GeoPoint>");
            Assert.AreEqual(new GeoPoint(4, 5, 6), points[1]);

            Solid[] array = read["array"] as Solid[];
            Assert.IsNotNull(array, "Solid[] must be restored as Solid[]");
            Assert.AreEqual(1, array.Length);
            Assert.IsNotNull(array[0]);

            Assert.IsInstanceOfType(read["single"], typeof(Solid));
            Assert.AreEqual(1.5, (double)read["number"]);
            Assert.AreEqual(new GeoVector(1, 0, 0), (GeoVector)read["vector"]);
        }

        [TestMethod]
        public void DictionaryWithTypedListValuesInsideObject()
        {
            // the dictionary is a property of another object, as in a project's UserData
            Holder holder = new Holder
            {
                Named = new Dictionary<string, object>
                {
                    ["beams"] = new List<Solid> { Box(0), Box(2), Box(4) },
                    ["rod"] = new List<Solid> { Box(8) },
                    ["angle"] = 75.0,
                }
            };
            Holder read = RoundTrip(holder);

            List<Solid> beams = read.Named["beams"] as List<Solid>;
            Assert.IsNotNull(beams);
            Assert.AreEqual(3, beams.Count);
            Assert.IsTrue(beams.TrueForAll(s => s != null));
            Assert.IsInstanceOfType(read.Named["rod"], typeof(List<Solid>));
            Assert.AreEqual(75.0, (double)read.Named["angle"]);
        }

        [TestMethod]
        public void TypedCollectionProperties()
        {
            Holder holder = new Holder
            {
                Solids = new List<Solid> { Box(0), Box(2) },
                NestedSolids = new List<List<Solid>> { new List<Solid> { Box(0) }, new List<Solid> { Box(2), Box(4) } },
                JaggedSolids = new Solid[][] { new Solid[] { Box(0), Box(2) }, new Solid[] { Box(4) } },
                Points = new List<GeoPoint> { new GeoPoint(1, 2, 3) },
                Ints = new List<int> { 1, 2, 3 },
                SolidInterface = new List<Solid> { Box(6) },
            };
            Holder read = RoundTrip(holder);

            Assert.AreEqual(2, read.Solids.Count);
            Assert.IsTrue(read.Solids.TrueForAll(s => s != null));

            // used to come back empty: the inner lists could not be added to List<List<Solid>>
            Assert.AreEqual(2, read.NestedSolids.Count);
            Assert.AreEqual(1, read.NestedSolids[0].Count);
            Assert.AreEqual(2, read.NestedSolids[1].Count);
            Assert.IsTrue(read.NestedSolids[1].TrueForAll(s => s != null));

            Assert.AreEqual(2, read.JaggedSolids.Length);
            Assert.AreEqual(2, read.JaggedSolids[0].Length);
            Assert.IsNotNull(read.JaggedSolids[1][0]);

            Assert.AreEqual(new GeoPoint(1, 2, 3), read.Points[0]);
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, read.Ints);

            Assert.IsInstanceOfType(read.SolidInterface, typeof(List<Solid>));
            Assert.AreEqual(1, read.SolidInterface.Count);
        }

        [TestMethod]
        public void DictionaryWithUniformListValues()
        {
            // all values have the same type: written as a single "$ValueType"
            Dictionary<string, object> dict = new Dictionary<string, object>
            {
                ["a"] = new List<Solid> { Box(0) },
                ["b"] = new List<Solid> { Box(2), Box(4) },
            };
            Dictionary<string, object> read = RoundTrip(dict);
            Assert.IsInstanceOfType(read["a"], typeof(List<Solid>));
            Assert.AreEqual(2, ((List<Solid>)read["b"]).Count);
        }

        [TestMethod]
        public void SharedSolidKeepsIdentity()
        {
            // the same solid in a list and as a single value is written once and read back as one object
            Solid box = Box();
            Dictionary<string, object> dict = new Dictionary<string, object>
            {
                ["list"] = new List<Solid> { box },
                ["single"] = box,
            };
            Dictionary<string, object> read = RoundTrip(dict);
            Assert.AreSame(read["single"], ((List<Solid>)read["list"])[0]);
        }

        [TestMethod]
        public void TypeNameWithOtherAssemblyVersion()
        {
            // files written by another CADability version carry that version in the generic argument
            Dictionary<string, object> dict = new Dictionary<string, object>
            {
                ["solids"] = new List<Solid> { Box() },
                ["number"] = 1.0,
            };
            string json = JsonSerialize.ToString(dict);
            string version = "Version=" + typeof(Solid).Assembly.GetName().Version;
            StringAssert.Contains(json, version);
            json = json.Replace(version, "Version=99.0.0.0");

            Dictionary<string, object> read = (Dictionary<string, object>)JsonSerialize.FromString(json);
            Assert.IsInstanceOfType(read["solids"], typeof(List<Solid>));
        }

        [TestMethod]
        public void UnknownValueTypeKeepsValue()
        {
            // a value type that cannot be resolved (e.g. from an application assembly which is not loaded)
            // must not make the whole file unreadable
            Dictionary<string, object> dict = new Dictionary<string, object>
            {
                ["solid"] = Box(),
                ["number"] = 2.5,
            };
            string json = JsonSerialize.ToString(dict);
            StringAssert.Contains(json, "\"System.Double\"");
            json = json.Replace("\"System.Double\"", "\"Unknown.Application.Type\"");

            Dictionary<string, object> read = (Dictionary<string, object>)JsonSerialize.FromString(json);
            Assert.IsInstanceOfType(read["solid"], typeof(Solid));
            Assert.AreEqual(2.5, (double)read["number"]);
        }

        [TestMethod]
        public void HashtableWithMixedValues()
        {
            Hashtable table = new Hashtable
            {
                ["solids"] = new List<Solid> { Box() },
                ["name"] = "box",
            };
            Hashtable read = RoundTrip(table);
            Assert.IsInstanceOfType(read["solids"], typeof(List<Solid>));
            Assert.AreEqual("box", read["name"]);
        }
    }
}
