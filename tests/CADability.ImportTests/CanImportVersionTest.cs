using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CADability.ImportTests
{
    /// <summary>
    /// <see cref="CADability.DXF.Import.CanImportVersion(string, out string)"/> decides up front
    /// whether ACadSharp will be able to read a drawing, so that an unreadable one can be turned
    /// down with a message naming its version instead of an exception from inside the reader.
    /// The versions it accepts are the ones ACadSharp documents as readable:
    /// https://github.com/DomCR/ACadSharp#compatible-dwgdxf-versions
    /// plus the R10 and R11/R12 DWG files, which the FriendsOfCADability fork of ACadSharp reads.
    /// </summary>
    [TestClass]
    public class CanImportVersionTest
    {
        public TestContext TestContext { get; set; }

        // Smallest DXF that still carries a header: HEADER with $ACADVER, then EOF.
        private static string TextDxf(string version)
        {
            return "  0\r\nSECTION\r\n  2\r\nHEADER\r\n  9\r\n$ACADVER\r\n  1\r\n" + version
                + "\r\n  0\r\nENDSEC\r\n  0\r\nEOF\r\n";
        }

        // The same header in the binary DXF encoding: sentinel, then group codes as 16 bit
        // little endian numbers with zero terminated strings.
        private static byte[] BinaryDxf(string version)
        {
            List<byte> bytes = new List<byte>();
            bytes.AddRange(Encoding.ASCII.GetBytes("AutoCAD Binary DXF\r\n"));
            bytes.Add(0x1a);
            bytes.Add(0x00);

            void Add(short code, string value)
            {
                bytes.Add((byte)(code & 0xff));
                bytes.Add((byte)((code >> 8) & 0xff));
                bytes.AddRange(Encoding.ASCII.GetBytes(value));
                bytes.Add(0x00);
            }

            Add(0, "SECTION");
            Add(2, "HEADER");
            Add(9, "$ACADVER");
            Add(1, version);
            Add(0, "ENDSEC");
            Add(0, "EOF");
            return bytes.ToArray();
        }

        // A DWG opens with its six character version tag, everything after it is irrelevant here.
        private static byte[] Dwg(string version)
        {
            byte[] bytes = new byte[128];
            byte[] tag = Encoding.ASCII.GetBytes(version);
            System.Array.Copy(tag, bytes, tag.Length);
            return bytes;
        }

        private string WriteFile(string name, string content)
        {
            string file = this.TestContext.TestName + name;
            File.WriteAllText(file, content);
            return file;
        }

        private string WriteFile(string name, byte[] content)
        {
            string file = this.TestContext.TestName + name;
            File.WriteAllBytes(file, content);
            return file;
        }

        [TestMethod]
        public void can_import_version_accepts_text_dxf_of_a_readable_version()
        {
            foreach (string version in new[] { "AC1009", "AC1012", "AC1014", "AC1015", "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" })
            {
                string file = WriteFile(version + ".dxf", TextDxf(version));
                Assert.IsTrue(CADability.DXF.Import.CanImportVersion(file, out string found),
                    $"DXF {version} must be readable");
                Assert.AreEqual(version, found);
            }
        }

        [TestMethod]
        public void can_import_version_rejects_dxf_older_than_release_11()
        {
            // R10 and older predate what ACadSharp reads; the version has to reach the caller so
            // it can say which one it was.
            string file = WriteFile("r10.dxf", TextDxf("AC1006"));
            Assert.IsFalse(CADability.DXF.Import.CanImportVersion(file, out string found));
            Assert.AreEqual("AC1006", found);
        }

        [TestMethod]
        public void can_import_version_accepts_dxf_without_a_version_header()
        {
            // ACadSharp falls back to a generic reader when $ACADVER is missing, so a file
            // without a header is worth a try rather than being turned down.
            string file = WriteFile("noheader.dxf", "  0\r\nSECTION\r\n  2\r\nENTITIES\r\n  0\r\nENDSEC\r\n  0\r\nEOF\r\n");
            Assert.IsTrue(CADability.DXF.Import.CanImportVersion(file, out string found));
            Assert.AreEqual(string.Empty, found);
        }

        [TestMethod]
        public void can_import_version_reads_the_version_of_a_binary_dxf()
        {
            string file = WriteFile("binary.dxf", BinaryDxf("AC1032"));
            Assert.IsTrue(CADability.DXF.Import.CanImportVersion(file, out string found));
            Assert.AreEqual("AC1032", found);
        }

        [TestMethod]
        public void can_import_version_accepts_dwg_of_a_readable_version()
        {
            foreach (string version in new[] { "AC1006", "AC1009", "AC1012", "AC1014", "AC1015", "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" })
            {
                string file = WriteFile(version + ".dwg", Dwg(version));
                Assert.IsTrue(CADability.DXF.Import.CanImportVersion(file, out string found),
                    $"DWG {version} must be readable");
                Assert.AreEqual(version, found);
            }
        }

        [TestMethod]
        public void can_import_version_rejects_dwg_older_than_release_10()
        {
            // The DWG reader reaches back to R10 - DwgReader throws CadNotSupportedException for
            // R9 (AC1004) and everything older.
            const string version = "AC1004";

            string dwg = WriteFile("old.dwg", Dwg(version));
            Assert.IsFalse(CADability.DXF.Import.CanImportVersion(dwg, out string found),
                "DWG R9 must not be reported as readable");
            Assert.AreEqual(version, found);
        }

        [TestMethod]
        public void can_import_version_tells_dwg_from_dxf_by_content_not_by_extension()
        {
            // The import list hands over whatever name the order data carries, so a DWG saved
            // under a .dxf name must still be judged by the DWG rules. R10 tells the two apart:
            // readable as DWG, not readable as DXF.
            string file = WriteFile("mislabelled.dxf", Dwg("AC1006"));
            Assert.IsTrue(CADability.DXF.Import.CanImportVersion(file, out string found));
            Assert.AreEqual("AC1006", found);
        }
    }
}
