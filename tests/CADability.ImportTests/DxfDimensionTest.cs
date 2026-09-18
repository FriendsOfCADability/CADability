using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// DXF dimensions. They are imported as the anonymous block AutoCAD keeps with every
    /// DIMENSION, because that block holds the picture AutoCAD drew; CADability's own
    /// <see cref="Dimension"/> would redraw it from its own style engine and look different.
    /// These tests cover what that costs and what it must not cost: the block's contents are
    /// mostly ByBlock on layer 0 and have to take the DIMENSION's attributes, a dimension
    /// without a block must not vanish, and the data needed to build a real dimension later
    /// has to survive in UserData. The export side writes a real DIMENSION back.
    /// </summary>
    [TestClass]
    public class DxfDimensionTest
    {
        public TestContext TestContext { get; set; }

        // A linear dimension over (0,0) - (100,0), dimension line at y = 20, drawn into the
        // anonymous block *D1. The block contents sit on layer 0 with color ByBlock (62 = 0),
        // which is what AutoCAD writes; the DIMENSION itself is on the red layer "Bemassung".
        private const string DimensionWithBlock = @"  0
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
  0
LAYER
  2
Bemassung
 70
0
 62
1
  6
CONTINUOUS
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
*D1
 70
1
 10
0.0
 20
0.0
 30
0.0
  3
*D1
  1

  0
LINE
  8
0
 62
0
 10
0.0
 20
20.0
 30
0.0
 11
100.0
 21
20.0
 31
0.0
  0
POINT
  8
DEFPOINTS
 10
0.0
 20
0.0
 30
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
DIMENSION
  8
Bemassung
100
AcDbEntity
100
AcDbDimension
  2
*D1
 70
0
 10
0.0
 20
20.0
 30
0.0
 11
50.0
 21
22.0
 31
0.0
100
AcDbAlignedDimension
 13
0.0
 23
0.0
 33
0.0
 14
100.0
 24
0.0
 34
0.0
  0
ENDSEC
  0
EOF
";

        /// <summary>
        /// The line inside the block is ByBlock on layer 0. DXF resolves that against the
        /// entity that placed the block, so it has to come out red and on layer "Bemassung",
        /// not black on layer 0.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_resolves_byblock_against_the_dimension()
        {
            Model model = ImportDxf(DimensionWithBlock);
            Block block = SingleDimensionBlock(model);

            Line line = block.Children.OfType<Line>().FirstOrDefault();
            Assert.IsNotNull(line, "the dimension line inside the block should be imported");
            Assert.AreEqual("Bemassung", line.Layer?.Name,
                "contents on layer 0 take the layer of the entity that placed the block");
            Assert.IsNotNull(line.ColorDef, "the line should have a color");
            Assert.AreEqual(System.Drawing.Color.Red.ToArgb(), line.ColorDef.Color.ToArgb(),
                "ByBlock resolves against the DIMENSION, whose layer is red");
        }

        /// <summary>
        /// Definition points are construction data that AutoCAD does not plot.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_drops_defpoints()
        {
            Model model = ImportDxf(DimensionWithBlock);
            Block block = SingleDimensionBlock(model);
            Assert.AreEqual(0, block.Children.OfType<Point>().Count(),
                "the POINT on layer DEFPOINTS should not be imported");
        }

        /// <summary>
        /// What makes the block a dimension has to survive, so an application can tell it from
        /// an ordinary block and a later step can build a real dimension without a new import.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_keeps_its_data_in_userdata()
        {
            Model model = ImportDxf(DimensionWithBlock);
            Block block = SingleDimensionBlock(model);

            Assert.AreEqual("DimensionAligned", block.UserData["CADability.DxfDimension"],
                "the marker should name the DXF dimension type");
            Assert.AreEqual(100.0, (double)block.UserData["CADability.DxfDimension.Measurement"], 1e-8,
                "the measured value should survive the import");
        }

        /// <summary>
        /// DXF R12 and several third party writers leave the anonymous block out. Dropping the
        /// dimension without a word was the old behaviour; it is drawn from its definition
        /// points instead.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_without_block_is_drawn_from_its_definition_points()
        {
            string dxf = DimensionWithBlock
                .Replace("  2\n*D1\n 70\n0\n", "  2\n\n 70\n0\n")
                .Replace("  2\r\n*D1\r\n 70\r\n0\r\n", "  2\r\n\r\n 70\r\n0\r\n");
            Assert.AreNotEqual(DimensionWithBlock, dxf, "the DIMENSION's block reference should be cleared");

            RequireSystemDrawing("the regenerated block carries the measurement text");
            Model model = ImportDxf(dxf);
            Assert.AreEqual(1, model.AllObjects.Count, "the dimension should still be imported");
            Block block = model.AllObjects[0] as Block;
            Assert.IsNotNull(block, "the regenerated dimension should be a block");
            Assert.IsTrue(block.Children.Count > 0, "the regenerated block should hold geometry");
            Assert.AreEqual(0, block.Children.OfType<Point>().Count(),
                "the definition points ACadSharp regenerates should not be imported");
        }

        /// <summary>
        /// A dimension drawn in CADability used to be dropped on export, there was no case for
        /// it at all. It has to come back as a dimension, with its measured value intact.
        /// </summary>
        [TestMethod]
        public void export_dimension_round_trips_as_a_dxf_dimension()
        {
            Project project = MakeProjectWithDimension(new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0));
            Model reimported = ExportAndImport(project);

            Assert.AreEqual(1, reimported.AllObjects.Count, "one dimension in, one dimension out");
            Block block = reimported.AllObjects[0] as Block;
            Assert.IsNotNull(block, "the dimension should come back");
            Assert.AreEqual("DimensionLinear", block.UserData["CADability.DxfDimension"],
                "it should be a DIMENSION entity, not a heap of lines");
            Assert.AreEqual(100.0, (double)block.UserData["CADability.DxfDimension.Measurement"], 1e-6,
                "the measured value should survive the round trip");
            Assert.IsTrue(block.Children.Count > 0, "the picture should travel in the anonymous block");
        }

        /// <summary>
        /// DXF has no dimension over more than two points, so a chain becomes one DIMENSION per
        /// measured section.
        /// </summary>
        [TestMethod]
        public void export_dimension_chain_writes_one_dimension_per_section()
        {
            Project project = MakeProjectWithDimension(
                new GeoPoint(0, 0, 0), new GeoPoint(40, 0, 0), new GeoPoint(100, 0, 0));
            Model reimported = ExportAndImport(project);

            Assert.AreEqual(2, reimported.AllObjects.Count, "three points measure two sections");
            double[] measured = reimported.AllObjects
                .OfType<Block>()
                .Select(b => (double)b.UserData["CADability.DxfDimension.Measurement"])
                .OrderBy(d => d)
                .ToArray();
            Assert.AreEqual(40.0, measured[0], 1e-6);
            Assert.AreEqual(60.0, measured[1], 1e-6);
        }

        // --- helpers -------------------------------------------------------------------------

        private static Project MakeProjectWithDimension(params GeoPoint[] points)
        {
            Project project = Project.CreateSimpleProject();
            Dimension dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimPoints;
            dim.DimensionStyle = project.DimensionStyleList.Current;
            dim.Normal = GeoVector.ZAxis;
            dim.DimLineRef = new GeoPoint(0, 20, 0);
            dim.DimLineDirection = GeoVector.XAxis;
            foreach (GeoPoint p in points) dim.AddPoint(p);
            project.GetActiveModel().Add(dim);
            return project;
        }

        private Model ExportAndImport(Project project)
        {
            string file = this.TestContext.TestName + ".dxf";
            Assert.IsTrue(project.Export(file, "dxf"), "export should succeed");
            Project read = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(read, "the exported file should be readable again");
            Model model = read.GetActiveModel();
            Assert.IsNotNull(model);
            return model;
        }

        private static Block SingleDimensionBlock(Model model)
        {
            Assert.AreEqual(1, model.AllObjects.Count, "the DIMENSION should import as one object");
            Block block = model.AllObjects[0] as Block;
            Assert.IsNotNull(block, "a DIMENSION imports as the block that holds its picture");
            return block;
        }

        /// <summary>
        /// System.Drawing is Windows only from .NET 7 on, and a text object asks it for the
        /// installed font families when it computes its extent. Tests whose drawing contains a
        /// measurement text can only run where that works.
        /// </summary>
        private static void RequireSystemDrawing(string why)
        {
#pragma warning disable CA1416 // the call is guarded, that is the point of it
            try { _ = System.Drawing.FontFamily.Families; }
#pragma warning restore CA1416
            catch (Exception)
            {
                Assert.Inconclusive("needs System.Drawing, which is Windows only: " + why);
            }
        }

        private Model ImportDxf(string dxf)
        {
            string file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            Model model = project.GetActiveModel();
            Assert.IsNotNull(model);
            return model;
        }
    }
}
