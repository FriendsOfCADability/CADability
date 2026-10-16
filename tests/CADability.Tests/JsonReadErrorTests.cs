using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// An entity in the <see cref="ISerializable"/> form whose constructor throws is read as null, so that the rest of
    /// the file can still be read. Its cause used to be swallowed, so the only symptom was a NullReferenceException in
    /// the object that refers to it (issue #412, step 2). <see cref="JsonSerialize.ReadErrors"/> keeps the cause.
    /// </summary>
    [TestClass]
    public class JsonReadErrorTests
    {
        [Serializable]
        public class ThrowsWhenRead : ISerializable
        {
            public ThrowsWhenRead() { }
            protected ThrowsWhenRead(SerializationInfo info, StreamingContext context)
            {
                throw new InvalidOperationException("broken on purpose");
            }
            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("Value", 1);
            }
        }

        private static string CdbFile(string name, [CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "CDB", name);

        private static JsonSerialize Read(string json, out object read)
        {
            JsonSerialize js = new JsonSerialize();
            using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)))
                read = js.FromStream(stream);
            return js;
        }

        [TestMethod]
        public void ThrowingEntityIsReadAsNullAndReportsItsCause()
        {
            Hashtable written = new Hashtable { ["broken"] = new ThrowsWhenRead(), ["intact"] = "still here" };
            JsonSerialize js = Read(JsonSerialize.ToString(written), out object read);

            Hashtable table = read as Hashtable;
            Assert.IsNotNull(table, "the rest of the file is still read");
            Assert.AreEqual("still here", table["intact"]);
            Assert.IsNull(table["broken"], "the entity that cannot be created is read as null, as before");

            Assert.AreEqual(1, js.ReadErrors.Count);
            SerializationException error = js.ReadErrors[0];
            StringAssert.Contains(error.Message, typeof(ThrowsWhenRead).FullName);
            StringAssert.Contains(error.Message, "broken on purpose");
            Assert.IsInstanceOfType(error.InnerException, typeof(InvalidOperationException), "the TargetInvocationException of the constructor call is unwrapped");
        }

        [TestMethod]
        public void ReadErrorsAreResetForEveryRead()
        {
            JsonSerialize js = new JsonSerialize();
            using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonSerialize.ToString(new ThrowsWhenRead()))))
                js.FromStream(stream);
            Assert.AreEqual(1, js.ReadErrors.Count);
            using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(JsonSerialize.ToString(new Hashtable { ["intact"] = "still here" }))))
                js.FromStream(stream);
            Assert.AreEqual(0, js.ReadErrors.Count);
        }

        [TestMethod]
        public void TestFilesAreReadWithoutErrors()
        {
            // Volumes.cdb.json produced the errors that hid the cause of the failing volume tests before #438
            foreach (string name in new[] { "Volumes.cdb.json", "issue66.cdb.json" })
            {
                JsonSerialize js = new JsonSerialize();
                using (FileStream stream = File.OpenRead(CdbFile(name)))
                    Assert.IsNotNull(js.FromStream(stream), name);
                Assert.AreEqual(0, js.ReadErrors.Count, name + ": " + string.Join("; ", js.ReadErrors.Select(e => e.Message)));
            }
        }
    }
}
