using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// DXF dimensions (issue #167). They are imported as the anonymous block AutoCAD keeps with
    /// every DIMENSION, because that block holds the picture AutoCAD drew. The block's contents
    /// are mostly ByBlock on layer 0 and have to take the DIMENSION's attributes, a dimension
    /// without a block must not vanish, and the data that makes the block a dimension has to
    /// survive in UserData.
    /// </summary>
    [TestClass]
    public class DxfDimensionTest
    {
        public TestContext TestContext { get; set; }

        private const string Header = @"  0
SECTION
  2
HEADER
  9
$ACADVER
  1
AC1015
  0
ENDSEC
";

        // A red layer "Dimensions" and the linetype DASHED
        private const string Tables = @"  0
SECTION
  2
TABLES
  0
TABLE
  2
LTYPE
  0
LTYPE
  2
ByBlock
 70
0
  3

 72
65
 73
0
 40
0.0
  0
LTYPE
  2
ByLayer
 70
0
  3

 72
65
 73
0
 40
0.0
  0
LTYPE
  2
CONTINUOUS
 70
0
  3
Solid line
 72
65
 73
0
 40
0.0
  0
LTYPE
  2
DASHED
 70
0
  3
Dashed
 72
65
 73
2
 40
3.0
 49
2.0
 74
0
 49
-1.0
 74
0
  0
ENDTAB
  0
TABLE
  2
LAYER
  0
LAYER
  2
Dimensions
 70
0
 62
1
  6
CONTINUOUS
  0
ENDTAB
  0
TABLE
  2
DIMSTYLE
  0
DIMSTYLE
100
AcDbSymbolTableRecord
100
AcDbDimStyleTableRecord
  2
NOTEXT
 70
0
140
1e-9
  0
ENDTAB
  0
ENDSEC
";

        // The anonymous block *D1: the dimension line at y = 20 on layer 0, with color,
        // linetype and lineweight ByBlock, which is what AutoCAD writes, and a definition point
        // on layer DEFPOINTS.
        private const string Blocks = @"  0
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
  6
BYBLOCK
370
-2
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
";

        /// <summary>
        /// An aligned DIMENSION from (<paramref name="x1"/>, <paramref name="y"/>) to
        /// (<paramref name="x2"/>, <paramref name="y"/>) with its dimension line at
        /// <paramref name="lineY"/>, on the red layer "Dimensions", dashed and 0.50 mm wide.
        /// <paramref name="block"/> null leaves the block reference (group 2) out,
        /// <paramref name="style"/> null leaves the dimension style at "Standard".
        /// </summary>
        private static string AlignedDimension(string block, string style = null,
            double x1 = 0.0, double x2 = 100.0, double y = 0.0, double lineY = 20.0)
        {
            string F(double d) => d.ToString("0.0###", CultureInfo.InvariantCulture);
            return "  0\nDIMENSION\n  8\nDimensions\n  6\nDASHED\n370\n50\n100\nAcDbEntity\n100\nAcDbDimension\n"
                + (block == null ? "" : "  2\n" + block + "\n")
                + (style == null ? "" : "  3\n" + style + "\n")
                + " 70\n33\n"
                // group 10 is where the dimension line meets the second extension line
                + " 10\n" + F(x2) + "\n 20\n" + F(lineY) + "\n 30\n0.0\n"
                + " 11\n" + F((x1 + x2) / 2) + "\n 21\n" + F(lineY + 2) + "\n 31\n0.0\n"
                + "100\nAcDbAlignedDimension\n"
                + " 13\n" + F(x1) + "\n 23\n" + F(y) + "\n 33\n0.0\n"
                + " 14\n" + F(x2) + "\n 24\n" + F(y) + "\n 34\n0.0\n";
        }

        private const string EntitiesStart = @"  0
SECTION
  2
ENTITIES
";

        private const string Epilogue = @"  0
ENDSEC
  0
EOF
";

        private static readonly string DimensionWithBlock =
            Header + Tables + Blocks + EntitiesStart + AlignedDimension("*D1") + Epilogue;

        /// <summary>
        /// The line inside the block is ByBlock on layer 0. DXF resolves that against the
        /// entity that placed the block, so it has to come out red, dashed, 0.5 mm wide and on
        /// layer "Dimensions", not black on layer 0.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_resolves_byblock_against_the_dimension()
        {
            Project project = ImportDxf(DimensionWithBlock);
            Block block = SingleDimensionBlock(project.GetActiveModel());

            Assert.AreEqual("Dimensions", block.Layer?.Name, "the block is on the layer of the DIMENSION");
            Line line = block.Children.OfType<Line>().FirstOrDefault();
            Assert.IsNotNull(line, "the dimension line inside the block should be imported");
            Assert.AreEqual("Dimensions", line.Layer?.Name,
                "contents on layer 0 take the layer of the entity that placed the block");
            Assert.IsNotNull(line.ColorDef, "the line should have a color");
            Assert.AreEqual(System.Drawing.Color.Red.ToArgb(), line.ColorDef.Color.ToArgb(),
                "ByBlock resolves against the DIMENSION, whose layer is red");
            Assert.AreEqual("DASHED", line.LinePattern?.Name, "linetype ByBlock is the DIMENSION's linetype");
            Assert.AreEqual(0.5, line.LineWidth.Width, 1e-9, "lineweight ByBlock is the DIMENSION's lineweight");
            Assert.IsTrue(project.LinePatternList.FindIndex(line.LinePattern) >= 0,
                "no stand-in for ByBlock may be left over");
        }

        /// <summary>
        /// Definition points are construction data that AutoCAD does not plot.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_drops_defpoints()
        {
            Block block = SingleDimensionBlock(ImportDxf(DimensionWithBlock).GetActiveModel());
            Assert.AreEqual(0, block.Children.OfType<Point>().Count(),
                "the POINT on layer DEFPOINTS should not be imported");
            Assert.AreEqual(1, block.Children.OfType<Line>().Count(), "the dimension line stays");
        }

        /// <summary>
        /// Only the picture of a dimension loses its definition points: an INSERT of the same
        /// block definition keeps everything that is in it.
        /// </summary>
        [TestMethod]
        public void import_dxf_insert_keeps_defpoints()
        {
            const string insert = @"  0
INSERT
  8
Dimensions
  2
*D1
 10
0.0
 20
0.0
 30
0.0
";
            Model model = ImportDxf(Header + Tables + Blocks + EntitiesStart
                + AlignedDimension("*D1") + insert + Epilogue).GetActiveModel();
            Assert.AreEqual(2, model.Count);
            Block dimension = model.AllObjects.OfType<Block>().Single(b => b.UserData.ContainsData("CADability.DxfDimension"));
            Block inserted = model.AllObjects.OfType<Block>().Single(b => !b.UserData.ContainsData("CADability.DxfDimension"));
            Assert.AreEqual(0, dimension.Children.OfType<Point>().Count());
            Assert.AreEqual(1, inserted.Children.OfType<Point>().Count());
        }

        /// <summary>
        /// What makes the block a dimension has to survive, so an application can tell it from
        /// an ordinary block.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_keeps_its_data_in_userdata()
        {
            Block block = SingleDimensionBlock(ImportDxf(DimensionWithBlock).GetActiveModel());

            Assert.AreEqual("DimensionAligned", block.UserData["CADability.DxfDimension"],
                "the marker should name the DXF dimension type");
            Assert.AreEqual(100.0, (double)block.UserData["CADability.DxfDimension.Measurement"], 1e-8,
                "the measured value should survive the import");
            Assert.AreEqual("Standard", block.UserData["CADability.DxfDimension.Style"]);
        }

        /// <summary>
        /// Issue #167: DXF R12 and several third party writers leave the anonymous block out.
        /// Such a dimension was dropped without a word; it is drawn from its definition points
        /// instead. Here the DIMENSION names no block at all. Its style "NOTEXT" has a text
        /// height too small to draw (DIMTXT has to be positive), so the picture has no measurement text,
        /// which needs GDI and therefore Windows; <see cref="import_dxf_dimension_without_block_shows_its_measurement"/> covers that.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_without_block_is_drawn_from_its_definition_points()
        {
            string dxf = Header + Tables + EntitiesStart + AlignedDimension(null, "NOTEXT") + Epilogue;
            Block block = AssertRegeneratedDimension(ImportDxf(dxf).GetActiveModel());
            Assert.AreEqual(0, block.Children.OfType<Text>().Count(), "the style has no text");
        }

        /// <summary>
        /// The same for a DIMENSION that names a block the file does not contain.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_with_missing_block_is_drawn_from_its_definition_points()
        {
            string dxf = Header + Tables + EntitiesStart + AlignedDimension("*D7", "NOTEXT") + Epilogue;
            AssertRegeneratedDimension(ImportDxf(dxf).GetActiveModel());
        }

        /// <summary>
        /// With the standard style the regenerated picture carries the measurement text.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_without_block_shows_its_measurement()
        {
            RequireGdi();
            string dxf = Header + Tables + EntitiesStart + AlignedDimension(null) + Epilogue;
            Block block = AssertRegeneratedDimension(ImportDxf(dxf).GetActiveModel());
            Text text = block.Children.OfType<Text>().SingleOrDefault();
            Assert.IsNotNull(text, "the measurement text should be part of the picture");
            StringAssert.StartsWith(text.TextString, "100");
        }

        /// <summary>
        /// Two dimensions without a block must not share one regenerated picture.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimensions_without_block_get_a_picture_each()
        {
            string dxf = Header + Tables + EntitiesStart
                + AlignedDimension(null, "NOTEXT")
                + AlignedDimension(null, "NOTEXT", x1: 0.0, x2: 200.0, y: 50.0, lineY: 80.0)
                + Epilogue;
            Model model = ImportDxf(dxf).GetActiveModel();

            Assert.AreEqual(2, model.Count, "both dimensions should be imported");
            Block[] blocks = model.AllObjects.OfType<Block>()
                .OrderBy(b => (double)b.UserData["CADability.DxfDimension.Measurement"]).ToArray();
            Assert.AreEqual(100.0, (double)blocks[0].UserData["CADability.DxfDimension.Measurement"], 1e-8);
            Assert.AreEqual(200.0, (double)blocks[1].UserData["CADability.DxfDimension.Measurement"], 1e-8);
            Assert.IsTrue(HasDimensionLine(blocks[0], 20.0, 100.0), "each dimension shows its own picture");
            Assert.IsTrue(HasDimensionLine(blocks[1], 80.0, 200.0), "each dimension shows its own picture");
            Assert.IsFalse(HasDimensionLine(blocks[0], 80.0, 200.0), "each dimension shows its own picture");
        }

        // A horizontal line of the given length at the given height
        private static bool HasDimensionLine(Block block, double y, double length)
        {
            return block.Children.OfType<Line>().Any(l => Math.Abs(l.StartPoint.y - y) < 1e-6
                && Math.Abs(l.EndPoint.y - y) < 1e-6 && Math.Abs(Math.Abs(l.EndPoint.x - l.StartPoint.x) - length) < 1e-6);
        }

        /// <summary>
        /// The picture ACadSharp draws for the aligned dimension of the fixture: a dimension line
        /// from (0,20) to (100,20), extension lines, no definition points, and everything in the
        /// DIMENSION's attributes.
        /// </summary>
        private static Block AssertRegeneratedDimension(Model model)
        {
            Block block = SingleDimensionBlock(model);
            Assert.AreEqual("DimensionAligned", block.UserData["CADability.DxfDimension"]);
            Assert.AreEqual(100.0, (double)block.UserData["CADability.DxfDimension.Measurement"], 1e-8);
            Assert.AreEqual("Dimensions", block.Layer?.Name, "the block is on the layer of the DIMENSION");
            Assert.AreEqual(0, block.Children.OfType<Point>().Count(),
                "the definition points ACadSharp regenerates should not be imported");
            Line[] lines = block.Children.OfType<Line>().ToArray();
            Assert.IsTrue(HasDimensionLine(block, 20.0, 100.0), "the dimension line runs from (0,20) to (100,20)");
            Assert.AreEqual(2, lines.Count(l => Math.Abs(l.StartPoint.x - l.EndPoint.x) < 1e-6),
                "both extension lines are drawn");
            foreach (Line line in lines)
            {
                Assert.AreEqual("Dimensions", line.Layer?.Name, "contents take the layer of the DIMENSION");
                Assert.AreEqual(System.Drawing.Color.Red.ToArgb(), line.ColorDef.Color.ToArgb(),
                    "contents take the color of the DIMENSION");
            }
            return block;
        }

        // --- helpers -------------------------------------------------------------------------

        private static Block SingleDimensionBlock(Model model)
        {
            Assert.AreEqual(1, model.Count, "the DIMENSION should import as one object");
            Block block = model[0] as Block;
            Assert.IsNotNull(block, "a DIMENSION imports as the block that holds its picture");
            return block;
        }

        // Adding a Text to a model computes its extent through GDI, which only exists on Windows.
        private static void RequireGdi()
        {
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                Assert.Inconclusive("text needs GDI, which is only available on Windows");
        }

        private Project ImportDxf(string dxf)
        {
            string file = this.TestContext.TestName + ".dxf";
            File.WriteAllText(file, dxf);
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            Assert.IsNotNull(project.GetActiveModel());
            return project;
        }
    }
}
