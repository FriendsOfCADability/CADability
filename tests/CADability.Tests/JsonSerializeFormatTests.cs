using CADability.Attribute;
using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="SphericalSurface"/>, <see cref="LayerList"/>, the named values of a <see cref="Project"/> and
    /// <see cref="BoundingRect"/> are written in the <see cref="IJsonSerialize"/> form, the same form ShapeIt writes
    /// (issue #401). Volumes.cdb.json was written by ShapeIt; issue66.cdb.json is an older file of this repository
    /// with these types in the <see cref="System.Runtime.Serialization.ISerializable"/> form, which must still be read.
    /// </summary>
    [TestClass]
    public class JsonSerializeFormatTests
    {
        private static string CdbFile(string name, [CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "CDB", name);

        private static Project RoundTrip(Project project)
        {
            MemoryStream written = new MemoryStream();
            project.WriteToJson(written); // closes the stream
            return Project.ReadFromJson(new MemoryStream(written.ToArray()));
        }

        private static void AssertLayersAreConsistent(Project project)
        {
            LayerList layers = project.LayerList;
            Assert.IsNotNull(layers);
            Assert.IsNotNull(layers.Current, "the current layer");
            Assert.AreSame(layers.Current, layers.Find(layers.Current.Name), "the current layer is an entry of the list");
            foreach (Layer layer in layers.ToArray())
                Assert.AreSame(layer, layers.Find(layer.Name), $"layer {layer.Name} is found by its name");
            foreach (IGeoObject go in project.GetActiveModel().AllObjects)
                if (go.Layer != null) Assert.AreSame(layers.Find(go.Layer.Name), go.Layer, $"the layer of {go.GetType().Name} is an entry of the list");
        }

        [TestMethod]
        public void ShapeItFileIsReadAndSurvivesARoundTrip()
        {
            Project read = Project.ReadFromFile(CdbFile("Volumes.cdb.json"), "cdb");
            Assert.IsNotNull(read, "could not read the ShapeIt file");
            string[] layerNames = read.LayerList.ToArray().Select(l => l.Name).ToArray();
            Assert.AreEqual(6, layerNames.Length, "the file has six layers");
            Assert.AreEqual("2D", read.LayerList.Current.Name, "the current layer of the file");
            AssertLayersAreConsistent(read);

            Solid[] solids = read.GetActiveModel().AllObjects.OfType<Solid>().ToArray();
            Assert.AreEqual(3, solids.Length);
            Assert.IsTrue(solids.Any(s => s.Shells[0].Faces.Any(f => f.Surface is SphericalSurface)), "the hemisphere keeps its spherical surface");

            Project again = RoundTrip(read);
            CollectionAssert.AreEqual(layerNames, again.LayerList.ToArray().Select(l => l.Name).ToArray());
            Assert.AreEqual("2D", again.LayerList.Current.Name);
            AssertLayersAreConsistent(again);
            Solid[] solidsAgain = again.GetActiveModel().AllObjects.OfType<Solid>().ToArray();
            Assert.AreEqual(3, solidsAgain.Length);
            double[] volumes = solids.Select(s => ShellMetrics.IntegratedVolume(s.Shells[0], 0.01)).OrderBy(v => v).ToArray();
            double[] volumesAgain = solidsAgain.Select(s => ShellMetrics.IntegratedVolume(s.Shells[0], 0.01)).OrderBy(v => v).ToArray();
            for (int i = 0; i < volumes.Length; i++)
                Assert.AreEqual(volumes[i], volumesAgain[i], volumes[i] * 1e-9, "the solids are the same after the round trip");
        }

        [TestMethod]
        public void OlderFileWithTheSerializableFormIsStillRead()
        {
            Project read = Project.ReadFromFile(CdbFile("issue66.cdb.json"), "cdb");
            Assert.IsNotNull(read, "could not read the older file");
            AssertLayersAreConsistent(read);
            Project again = RoundTrip(read);
            AssertLayersAreConsistent(again);
            CollectionAssert.AreEquivalent(read.LayerList.ToArray().Select(l => l.Name).ToArray(),
                again.LayerList.ToArray().Select(l => l.Name).ToArray());
            Assert.AreEqual(read.GetActiveModel().AllObjects.Count, again.GetActiveModel().AllObjects.Count);
        }

        /// <summary>
        /// The colours and layers read from JSON belong to their list again: renaming one to the name of another is
        /// refused by the list, as for a list that was built in code. The ColorList used to miss its callback after
        /// reading, so its colours had no list and accepted a duplicate name.
        /// </summary>
        [TestMethod]
        public void ColorsAndLayersBelongToTheirListAfterReading()
        {
            Project project = Project.CreateSimpleProject();
            Assert.IsTrue(project.ColorList.Count >= 2 && project.LayerList.Count >= 2, "the default lists have at least two entries");
            Project again = RoundTrip(project);

            ColorList colors = again.ColorList;
            Assert.ThrowsException<NameAlreadyExistsException>(() => colors[0].Name = colors[1].Name, "a colour must not take the name of another one");
            LayerList layers = again.LayerList;
            Assert.ThrowsException<NameAlreadyExistsException>(() => layers[0].Name = layers[1].Name, "a layer must not take the name of another one");
        }

        [TestMethod]
        public void NamedValuesSurviveARoundTrip()
        {
            Project project = Project.CreateSimpleProject();
            project.SetNamedValue("d", 2.5);
            project.SetNamedValue("p", new GeoPoint(1, 2, 3));
            project.SetNamedValue("v", new GeoVector(4, 5, 6));
            Project again = RoundTrip(project);
            Assert.AreEqual(2.5, (double)again.GetNamedValue("d"));
            Assert.AreEqual(new GeoPoint(1, 2, 3), (GeoPoint)again.GetNamedValue("p"));
            Assert.AreEqual(new GeoVector(4, 5, 6), (GeoVector)again.GetNamedValue("v"));
        }

        [TestMethod]
        public void SphericalSurfaceSurvivesARoundTrip()
        {
            SphericalSurface sphere = new SphericalSurface(new GeoPoint(1, 2, 3), 5 * GeoVector.YAxis, 5 * GeoVector.ZAxis, 5 * GeoVector.XAxis);
            string json = JsonSerialize.ToString(sphere);
            SphericalSurface read = JsonSerialize.FromString(json) as SphericalSurface;
            Assert.IsNotNull(read);
            foreach (GeoPoint2D uv in new GeoPoint2D[] { new GeoPoint2D(0.3, 0.4), new GeoPoint2D(2.0, -0.9), new GeoPoint2D(4.0, 1.1) })
                Assert.IsTrue((sphere.PointAt(uv) | read.PointAt(uv)) < 1e-10);
        }

        [TestMethod]
        public void BoundingRectIsWrittenAsAStruct()
        {
            // a property of an IJsonSerialize object
            CylindricalSurface cylinder = new CylindricalSurface(GeoPoint.Origin, 5 * GeoVector.XAxis, 5 * GeoVector.YAxis, GeoVector.ZAxis);
            NonPeriodicSurface surface = new NonPeriodicSurface(cylinder, new BoundingRect(0.0, -1.5, 2.0 * Math.PI, 7.25));
            string json = JsonSerialize.ToString(surface);
            StringAssert.Contains(json, "\"PeriodicBounds\":[0,-1.5,", "left, bottom, right, top, as ShapeIt writes it");
            Assert.IsFalse(json.Contains("\"$Type\":\"CADability.BoundingRect\""), "no entity of its own");
            NonPeriodicSurface read = JsonSerialize.FromString(json) as NonPeriodicSurface;
            Assert.IsNotNull(read);
            foreach (GeoPoint2D uv in new GeoPoint2D[] { new GeoPoint2D(0.3, 0.4), new GeoPoint2D(-2.0, 0.9), new GeoPoint2D(1.0, -3.1) })
                Assert.IsTrue((surface.PointAt(uv) | read.PointAt(uv)) < 1e-10);

            // a value of an ISerializable object (ProjectedCurve.PeriodicDomain in the ShapeIt file)
            MemoryStream written = new MemoryStream();
            Project.ReadFromFile(CdbFile("Volumes.cdb.json"), "cdb").WriteToJson(written);
            string project = System.Text.Encoding.UTF8.GetString(written.ToArray());
            StringAssert.Contains(project, "\"PeriodicDomain\":{\"$Type\":\"CADability.BoundingRect\",\"$Value\":[");
        }
    }
}
