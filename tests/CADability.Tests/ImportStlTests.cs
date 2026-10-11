using System.Globalization;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// ImportSTL.Read opened the file a second time to read the triangles and never closed that reader, so the file stayed
    /// open until the garbage collector finalized it: it could not be deleted or written on Windows meanwhile.
    /// </summary>
    [TestClass]
    public class ImportStlTests
    {
        private static readonly GeoPoint o = new GeoPoint(0, 0, 0), x = new GeoPoint(10, 0, 0), y = new GeoPoint(0, 10, 0), z = new GeoPoint(0, 0, 10);
        private static readonly GeoPoint[][] tetrahedron = { new[] { o, y, x }, new[] { o, x, z }, new[] { o, z, y }, new[] { x, y, z } };

        private static GeoVector Normal(GeoPoint[] t) => ((t[1] - t[0]) ^ (t[2] - t[0])).Normalized;

        private static void WriteBinary(string file)
        {
            using (BinaryWriter writer = new BinaryWriter(File.Create(file)))
            {
                writer.Write(new byte[80]);
                writer.Write((uint)tetrahedron.Length);
                foreach (GeoPoint[] t in tetrahedron)
                {
                    GeoVector n = Normal(t);
                    writer.Write((float)n.x); writer.Write((float)n.y); writer.Write((float)n.z);
                    foreach (GeoPoint p in t)
                    {
                        writer.Write((float)p.x); writer.Write((float)p.y); writer.Write((float)p.z);
                    }
                    writer.Write((ushort)0);
                }
            }
        }

        private static void WriteAscii(string file)
        {
            string F(double d) => d.ToString("R", CultureInfo.InvariantCulture);
            using (StreamWriter writer = File.CreateText(file))
            {
                writer.WriteLine("solid tetrahedron");
                foreach (GeoPoint[] t in tetrahedron)
                {
                    GeoVector n = Normal(t);
                    writer.WriteLine($"  facet normal {F(n.x)} {F(n.y)} {F(n.z)}");
                    writer.WriteLine("    outer loop");
                    foreach (GeoPoint p in t) writer.WriteLine($"      vertex {F(p.x)} {F(p.y)} {F(p.z)}");
                    writer.WriteLine("    endloop");
                    writer.WriteLine("  endfacet");
                }
                writer.WriteLine("endsolid tetrahedron");
            }
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void the_file_is_closed_after_reading(bool ascii)
        {
            string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cadability-{Guid.NewGuid():N}.stl");
            try
            {
                if (ascii) WriteAscii(file);
                else WriteBinary(file);
                Shell[] shells = new ImportSTL().Read(file);
                Assert.AreEqual(1, shells.Length);
                Assert.AreEqual(4, shells[0].Faces.Length);
                // fails while a reader of the import is still open
                using (new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }

                Shell[] fromBytes = new ImportSTL().Read(File.ReadAllBytes(file));
                Assert.AreEqual(1, fromBytes.Length);
                Assert.AreEqual(4, fromBytes[0].Faces.Length);
            }
            finally
            {
                try { File.Delete(file); } catch (IOException) { }
            }
        }
    }
}
