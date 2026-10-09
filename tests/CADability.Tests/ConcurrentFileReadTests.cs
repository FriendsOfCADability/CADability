using System.Runtime.CompilerServices;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Reading a file must not lock other readers out. The tests run in parallel, and VolumeTests failed now and then
    /// with "The process cannot access the file ... because it is being used by another process", because
    /// Project.ReadFromFile opened the file with FileShare.None. Each test keeps a read handle of its own open while
    /// several tasks read the file, so the outcome does not depend on how the reads happen to overlap.
    /// </summary>
    [TestClass]
    public class ConcurrentFileReadTests
    {
        private const int Readers = 4;

        private static string CdbFile(string name, [CallerFilePath] string thisFile = "")
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile), "Files", "CDB", name);

        private static FileStream OpenForReading(string file)
            => new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);

        [TestMethod]
        public void a_project_file_can_be_read_by_several_readers_at_once()
        {
            string file = CdbFile("issue347.cdb.json");
            Assert.IsTrue(File.Exists(file), $"test file not found: {file}");
            Project[] projects;
            using (OpenForReading(file))
            {
                Task<Project>[] reads = Enumerable.Range(0, Readers).Select(_ => Task.Run(() => Project.ReadFromFile(file, "cdb"))).ToArray();
                try
                {
                    projects = Task.WhenAll(reads).GetAwaiter().GetResult();
                }
                catch (TypeInitializationException e) when (e.GetBaseException() is PlatformNotSupportedException)
                {
                    Assert.Inconclusive("reading a project needs System.Drawing printing, which is only available on Windows");
                    return;
                }
            }
            foreach (Project project in projects)
            {
                Assert.IsNotNull(project);
                Assert.AreEqual(1, project.GetActiveModel().AllObjects.Count);
            }
        }

        [TestMethod]
        public void a_binary_stl_file_can_be_read_by_several_readers_at_once()
        {
            string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cadability-{Guid.NewGuid():N}.stl");
            try
            {
                WriteTetrahedron(file);
                Shell[][] results;
                using (OpenForReading(file))
                {
                    Task<Shell[]>[] reads = Enumerable.Range(0, Readers).Select(_ => Task.Run(() => new ImportSTL().Read(file))).ToArray();
                    results = Task.WhenAll(reads).GetAwaiter().GetResult();
                }
                foreach (Shell[] shells in results)
                {
                    Assert.AreEqual(1, shells.Length);
                    Assert.AreEqual(4, shells[0].Faces.Length);
                }
            }
            finally
            {
                GC.Collect(); // ImportSTL.Read does not close its reader
                GC.WaitForPendingFinalizers();
                try { File.Delete(file); } catch (IOException) { }
            }
        }

        /// <summary>
        /// A closed tetrahedron as binary STL: 80 byte header, triangle count, and per triangle the normal, three
        /// vertices and a 16 bit attribute.
        /// </summary>
        private static void WriteTetrahedron(string file)
        {
            GeoPoint o = new GeoPoint(0, 0, 0), x = new GeoPoint(10, 0, 0), y = new GeoPoint(0, 10, 0), z = new GeoPoint(0, 0, 10);
            GeoPoint[][] triangles = { new[] { o, y, x }, new[] { o, x, z }, new[] { o, z, y }, new[] { x, y, z } };
            using (BinaryWriter writer = new BinaryWriter(File.Create(file)))
            {
                writer.Write(new byte[80]);
                writer.Write((uint)triangles.Length);
                foreach (GeoPoint[] t in triangles)
                {
                    GeoVector normal = ((t[1] - t[0]) ^ (t[2] - t[0])).Normalized;
                    writer.Write((float)normal.x); writer.Write((float)normal.y); writer.Write((float)normal.z);
                    foreach (GeoPoint p in t)
                    {
                        writer.Write((float)p.x); writer.Write((float)p.y); writer.Write((float)p.z);
                    }
                    writer.Write((ushort)0);
                }
            }
        }
    }
}
