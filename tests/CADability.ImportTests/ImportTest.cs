using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    [TestClass]
    public class ImportTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void import_dxf_malformed_objects_section_falls_back_to_geometry()
        {
            // Regression: DxfException "Invalid dxf code with value 0" in the OBJECTS section
            // crashed the import even though all geometry (in BLOCKS/ENTITIES) was already
            // parsed. The import now retries without the OBJECTS section so geometry survives.
            // The OBJECTS section is intentionally truncated / broken here.
            const string dxf = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1015
  0
ENDSEC
  0
SECTION
  2
TABLES
  0
TABLE
  2
LAYER
 70
1
  0
LAYER
  2
0
 70
0
 62
7
  6
Continuous
  0
ENDTAB
  0
ENDSEC
  0
SECTION
  2
BLOCKS
  0
BLOCK
  8
0
  2
*Model_Space
 10
0.0
 20
0.0
 30
0.0
  3
*Model_Space
  4

  0
LINE
  8
0
 10
10.0
 20
20.0
 30
0.0
 11
30.0
 21
40.0
 31
0.0
  0
ENDBLK
  8
0
  0
ENDSEC
  0
SECTION
  2
ENTITIES
  0
ENDSEC
  0
SECTION
  2
OBJECTS
  0
DICTIONARY
  5
C
330
0
281
1
  3
ACAD_GROUP
350
D
  0
"; // intentionally truncated (no ENDSEC/EOF) — simulates a malformed OBJECTS section

            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project, "Import must fall back gracefully when OBJECTS section is malformed");
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.IsTrue(model.AllObjects.Count > 0,
                "Geometry from BLOCKS section must survive a malformed OBJECTS section");
        }

        [TestMethod]
        public void import_dxf_empty_block_insert_not_placed_at_origin()
        {
            // Regression: empty blocks (e.g. dimension arrowheads with no geometry) were
            // inserted as GeoObject.Block at (0,0,0), appearing far outside the main drawing.
            // CreateInsert must return null when the referenced block has no children.
            const string dxf = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1015
  0
ENDSEC
  0
SECTION
  2
BLOCKS
  0
BLOCK
  8
0
  2
EmptyArrow
 10
0.0
 20
0.0
 30
0.0
  3
EmptyArrow
  4

  0
ENDBLK
  8
0
  0
BLOCK
  8
0
  2
Geometry
 10
0.0
 20
0.0
 30
0.0
  3
Geometry
  4

  0
LINE
  8
0
 10
100.0
 20
100.0
 30
0.0
 11
200.0
 21
200.0
 31
0.0
  0
ENDBLK
  8
0
  0
ENDSEC
  0
SECTION
  2
ENTITIES
  0
INSERT
  8
0
  2
EmptyArrow
 10
0.0
 20
0.0
 30
0.0
  0
INSERT
  8
0
  2
Geometry
 10
0.0
 20
0.0
 30
0.0
  0
ENDSEC
  0
EOF
";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project, "Import must not crash");
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.IsTrue(model.AllObjects.Count == 1,
                $"Empty block insert must be skipped; expected 1 object, got {model.AllObjects.Count}");
        }

        [TestMethod]
        public void import_dxf_arc_with_reversed_normal_uses_wcs_center()
        {
            // Regression: ARC/CIRCLE entities with Normal=(0,0,-1) store their center in OCS.
            // OCS→WCS via Arbitrary Axis Algorithm flips the X component for Normal=(0,0,-1).
            // OCS (-1000,-500,0) with Normal=(0,0,-1) → WCS (1000,-500,0).
            // Without the fix the center was imported at the raw OCS coordinate (-1000,-500).
            const string dxf = "  0\r\nSECTION\r\n  2\r\nHEADER\r\n  9\r\n$ACADVER\r\n  1\r\nAC1015\r\n  0\r\nENDSEC\r\n  0\r\nSECTION\r\n  2\r\nBLOCKS\r\n  0\r\nBLOCK\r\n  8\r\n0\r\n  2\r\n*Model_Space\r\n 10\r\n0.0\r\n 20\r\n0.0\r\n 30\r\n0.0\r\n  3\r\n*Model_Space\r\n  4\r\n\r\n  0\r\nARC\r\n  8\r\n0\r\n 10\r\n-1000.0\r\n 20\r\n-500.0\r\n 30\r\n0.0\r\n 40\r\n100.0\r\n 50\r\n0.0\r\n 51\r\n90.0\r\n210\r\n0.0\r\n220\r\n0.0\r\n230\r\n-1.0\r\n  0\r\nENDBLK\r\n  8\r\n0\r\n  0\r\nENDSEC\r\n  0\r\nSECTION\r\n  2\r\nENTITIES\r\n  0\r\nENDSEC\r\n  0\r\nEOF\r\n";
            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.IsTrue(model.AllObjects.Count > 0, "ARC entity must be imported");
            var arc = model.AllObjects[0] as Ellipse;
            Assert.IsNotNull(arc, "Imported object must be an Ellipse/Arc");
            // WCS center must be (1000,-500,0), not the raw OCS value (-1000,-500,0)
            Assert.IsTrue(System.Math.Abs(arc.Center.x - 1000.0) < 1e-6,
                $"Arc WCS X must be 1000, got {arc.Center.x}");
            Assert.IsTrue(System.Math.Abs(arc.Center.y - (-500.0)) < 1e-6,
                $"Arc WCS Y must be -500, got {arc.Center.y}");
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/BlockXTestFile.dxf", nameof(import_dxf_block_with_insert_succeeds))]
        public void import_dxf_block_with_insert_succeeds()
        {
            // BlockXTestFile.dxf contains a LINE entity plus an INSERT of block "AnonymousBlock1"
            // which itself contains 2 LINE entities forming an X shape.
            // After import the model must have 2 top-level objects: 1 Line and 1 Block (with 2 child Lines).
            var file = System.IO.Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "BlockXTestFile.dxf");
            Assert.IsTrue(System.IO.File.Exists(file));
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.AllObjects.Count,
                $"Expected 1 Line + 1 Block (from INSERT), got {model.AllObjects.Count} objects");
            var block = model.AllObjects.OfType<Block>().FirstOrDefault();
            Assert.IsNotNull(block, "INSERT must be imported as a GeoObject.Block");
            Assert.AreEqual(2, block.Count,
                $"Block from INSERT must contain 2 child Lines; got {block.Count}");
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/BlockRefXTestFile.dxf", nameof(import_dxf_simple_line_file_succeeds))]
        public void import_dxf_simple_line_file_succeeds()
        {
            // BlockRefXTestFile.dxf contains a single LINE entity and a paper-space VIEWPORT.
            // The VIEWPORT is not a geometry entity and must be skipped; only 1 Line must be imported.
            var file = System.IO.Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "BlockRefXTestFile.dxf");
            Assert.IsTrue(System.IO.File.Exists(file));
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.AreEqual(1, model.AllObjects.Count,
                $"Expected 1 Line; got {model.AllObjects.Count} objects");
            Assert.IsInstanceOfType(model.AllObjects[0], typeof(Line),
                "The single imported object must be a Line");
        }

        [TestMethod]
        public void import_dxf_r12_orphaned_table_succeeds()
        {
            // Regression guard for a client-side workaround of an ACadSharp bug.
            //
            // BUG IN ACADSHARP (≤ 3.6.35):
            //   DxfReader requires TABLE records to be wrapped in a SECTION / TABLES / ENDSEC
            //   envelope.  Some R12 (AC1003) files produced by third-party CAD tools place the
            //   LAYER (and other) TABLE records directly after the HEADER ENDSEC with no outer
            //   SECTION wrapper.  ACadSharp silently returns a CadDocument whose *Model_Space
            //   block record contains zero entities — no exception is thrown — even though the
            //   ENTITIES section that follows the orphaned TABLE block is perfectly valid.
            //
            // FIX APPLIED IN ImportDxf.cs (NormalizeOrphanedTables):
            //   Before passing bytes to DxfReader the raw file is scanned for the pattern
            //   "ENDSEC immediately followed by TABLE".  When detected, the missing
            //   SECTION / TABLES / ENDSEC envelope is injected synthetically and the
            //   corrected bytes are handed to ACadSharp.
            //
            // TODO: report upstream to ACadSharp so DxfReader handles this natively.
            //   https://github.com/DomCR/ACadSharp/issues
            const string dxf = "  0\r\nSECTION\r\n  2\r\nHEADER\r\n  9\r\n$ACADVER\r\n  1\r\nAC1003\r\n  0\r\nENDSEC\r\n" +
                               // ↓ bare TABLE — no surrounding SECTION/TABLES/ENDSEC (the bug trigger)
                               "  0\r\nTABLE\r\n  2\r\nLAYER\r\n 70\r\n1\r\n  0\r\nLAYER\r\n  2\r\n0\r\n 70\r\n0\r\n 62\r\n7\r\n  6\r\nContinuous\r\n  0\r\nENDTAB\r\n" +
                               "  0\r\nSECTION\r\n  2\r\nENTITIES\r\n" +
                               "  0\r\nLINE\r\n  8\r\n0\r\n 10\r\n0.0\r\n 20\r\n0.0\r\n 30\r\n0.0\r\n 11\r\n10.0\r\n 21\r\n10.0\r\n 31\r\n0.0\r\n" +
                               "  0\r\nENDSEC\r\n  0\r\nEOF\r\n";

            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.IsTrue(model.AllObjects.Count > 0,
                "LINE entity from R12 ENTITIES section must be imported when TABLE is orphaned");
        }

        [TestMethod]
        public void import_dxf_r12_tab_prefixed_values_and_named_layer_succeeds()
        {
            // Regression guard — extends import_dxf_r12_orphaned_table_succeeds with two
            // additional quirks observed in real-world R12 files from certain CAD tools:
            //
            //  1. TAB-prefixed integer group values (e.g. " 70\r\n\t2\r\n").
            //     The DXF spec requires right-justified decimal integers with no leading TAB.
            //     ACadSharp silently ignores the malformed token; as a result the LAYER TABLE
            //     capacity is not read, so named layers may not be registered in doc.Layers.
            //     Entities that reference such layers fall back to the default "0" layer.
            //
            //  2. Entities on a named layer ("1") rather than the default ("0").
            //     Because layer "1" is not in doc.Layers (due to quirk 1), SetAttributes
            //     cannot set the layer attribute — but the entity must still be created and
            //     added to the model regardless.
            //
            // The orphaned-TABLE normalizer (NormalizeOrphanedTables in ImportDxf.cs) wraps
            // the file before ACadSharp sees it, so both LINE and ARC are imported despite
            // the malformed layer count and the missing SECTION/TABLES envelope.
            const string dxf = "  0\r\nSECTION\r\n  2\r\nHEADER\r\n  9\r\n$ACADVER\r\n  1\r\nAC1003\r\n  0\r\nENDSEC\r\n" +
                               "  0\r\nTABLE\r\n  2\r\nLAYER\r\n 70\r\n\t2\r\n" + // ← TAB before "2"
                               "  0\r\nLAYER\r\n  2\r\n0\r\n 70\r\n0\r\n 62\r\n7\r\n  6\r\nContinuous\r\n" +
                               "  0\r\nLAYER\r\n  2\r\n1\r\n 70\r\n0\r\n 62\r\n4\r\n  6\r\nContinuous\r\n" +
                               "  0\r\nENDTAB\r\n" +
                               "  0\r\nSECTION\r\n  2\r\nENTITIES\r\n" +
                               "  0\r\nLINE\r\n  8\r\n1\r\n 10\r\n0.0\r\n 20\r\n0.0\r\n 30\r\n0.0\r\n 11\r\n10.0\r\n 21\r\n10.0\r\n 31\r\n0.0\r\n" + // layer 1
                               "  0\r\nARC\r\n  8\r\n1\r\n 10\r\n5.0\r\n 20\r\n5.0\r\n 30\r\n0.0\r\n 40\r\n3.0\r\n 50\r\n0.0\r\n 51\r\n180.0\r\n" + // layer 1
                               "  0\r\nENDSEC\r\n  0\r\nEOF\r\n";

            var file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            var project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            var model = project.GetActiveModel();
            Assert.IsNotNull(model);
            Assert.AreEqual(2, model.AllObjects.Count,
                $"LINE and ARC on layer '1' must both be imported; got {model.AllObjects.Count}");
        }
    }
}
