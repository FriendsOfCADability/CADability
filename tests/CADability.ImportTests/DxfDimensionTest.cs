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
    /// survive in UserData. The export writes CADability's <see cref="Dimension"/> as a DXF
    /// DIMENSION.
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

        // --- export: CADability's Dimension as a DXF DIMENSION --------------------------------

        /// <summary>
        /// A dimension drawn in CADability used to be dropped on export, there was no case for
        /// it at all. It has to be written as a DIMENSION with its definition points, its
        /// measured text and the picture CADability drew in the anonymous block.
        /// </summary>
        [TestMethod]
        public void export_dimension_writes_a_dxf_dimension()
        {
            Project project = MakeProjectWithDimension(new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0));
            Dimension dim = (Dimension)project.GetActiveModel()[0];
            ACadSharp.CadDocument doc = ExportAndRead(project);

            ACadSharp.Entities.DimensionLinear linear = doc.Entities.OfType<ACadSharp.Entities.DimensionLinear>().Single();
            Assert.AreEqual(0.0, Distance(linear.FirstPoint, new GeoPoint(0, 0, 0)), 1e-8, "group 13");
            Assert.AreEqual(0.0, Distance(linear.SecondPoint, new GeoPoint(100, 0, 0)), 1e-8, "group 14");
            Assert.AreEqual(0.0, Distance(linear.DefinitionPoint, new GeoPoint(100, 20, 0)), 1e-8,
                "group 10 is where the dimension line meets the second extension line");
            Assert.AreEqual(0.0, linear.Rotation, 1e-12, "the dimension runs along the x axis");
            Assert.AreEqual(100.0, linear.Measurement, 1e-8);
            Assert.AreEqual(dim.GetDimText(0), linear.Text, "the text CADability shows");
            Assert.IsNotNull(linear.Block, "the picture travels in the anonymous block");
            StringAssert.StartsWith(linear.Block.Name, "*D", "the anonymous block of a dimension");
            Assert.IsTrue(linear.Block.IsAnonymous);
            // Without GDI (anywhere but Windows) CADability stops drawing at the text, after
            // the extension lines and the arrows; on Windows the dimension line and the text follow.
            Assert.IsTrue(linear.Block.Entities.OfType<ACadSharp.Entities.Line>().Count(
                    l => Math.Abs(l.StartPoint.X - l.EndPoint.X) < 1e-8) >= 2, "the two extension lines");
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.IsTrue(linear.Block.Entities.OfType<ACadSharp.Entities.Line>().Any(
                    l => Math.Abs(l.StartPoint.Y - 20) < 1e-8 && Math.Abs(l.EndPoint.Y - 20) < 1e-8), "the dimension line");
                Assert.IsTrue(linear.Block.Entities.Any(e => e is ACadSharp.Entities.TextEntity || e is ACadSharp.Entities.MText),
                    "the measurement text");
            }
            Assert.AreEqual(project.DimensionStyleList.Current.Name, linear.Style.Name);
            Assert.AreEqual(project.DimensionStyleList.Current.TextSize, linear.Style.TextHeight, 1e-12);
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
            ACadSharp.CadDocument doc = ExportAndRead(project);

            double[] measured = doc.Entities.OfType<ACadSharp.Entities.DimensionLinear>()
                .Select(d => d.Measurement).OrderBy(d => d).ToArray();
            Assert.AreEqual(2, measured.Length, "three points measure two sections");
            Assert.AreEqual(40.0, measured[0], 1e-8);
            Assert.AreEqual(60.0, measured[1], 1e-8);
        }

        /// <summary>
        /// DXF puts a radial dimension's center in group 10 and the point on the circle in
        /// group 15.
        /// </summary>
        [TestMethod]
        public void export_radial_dimension_puts_the_center_in_group_10()
        {
            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(MakeCircleDimension(project, Dimension.EDimType.DimRadius));
            ACadSharp.CadDocument doc = ExportAndRead(project);

            ACadSharp.Entities.DimensionRadius radial = doc.Entities.OfType<ACadSharp.Entities.DimensionRadius>().Single();
            Assert.AreEqual(0.0, Distance(radial.DefinitionPoint, new GeoPoint(10, 10, 0)), 1e-8, "the center");
            Assert.AreEqual(0.0, Distance(radial.AngleVertex, new GeoPoint(35, 10, 0)), 1e-8,
                "the point on the circle, on the side of the text");
            Assert.AreEqual(25.0, radial.Measurement, 1e-8);
            Assert.AreEqual(10.0, radial.LeaderLength, 1e-8);
        }

        /// <summary>
        /// A diameter dimension names the two ends of the diameter: group 15 is the one where
        /// the leader and the text are, group 10 the opposite one.
        /// </summary>
        [TestMethod]
        public void export_diameter_dimension_puts_the_text_side_in_group_15()
        {
            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(MakeCircleDimension(project, Dimension.EDimType.DimDiameter));
            ACadSharp.CadDocument doc = ExportAndRead(project);

            ACadSharp.Entities.DimensionDiameter diametric = doc.Entities.OfType<ACadSharp.Entities.DimensionDiameter>().Single();
            Assert.AreEqual(0.0, Distance(diametric.AngleVertex, new GeoPoint(35, 10, 0)), 1e-8, "the text side");
            Assert.AreEqual(0.0, Distance(diametric.DefinitionPoint, new GeoPoint(-15, 10, 0)), 1e-8, "the opposite end");
            Assert.AreEqual(50.0, diametric.Measurement, 1e-8);
        }

        /// <summary>
        /// An angular dimension: vertex in group 15, a point on each leg in 13 and 14, and the
        /// point the dimension arc runs through in group 10.
        /// </summary>
        [TestMethod]
        public void export_angular_dimension_writes_three_point_angular_dimension()
        {
            Project project = Project.CreateSimpleProject();
            Dimension dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimAngle;
            dim.DimensionStyle = project.DimensionStyleList.Current;
            dim.Normal = GeoVector.ZAxis;
            dim.AddPoint(new GeoPoint(0, 0, 0));
            dim.AddPoint(new GeoPoint(10, 0, 0));
            dim.AddPoint(new GeoPoint(0, 10, 0));
            dim.DimLineRef = new GeoPoint(14.14, 14.14, 0);
            project.GetActiveModel().Add(dim);
            ACadSharp.CadDocument doc = ExportAndRead(project);

            ACadSharp.Entities.DimensionAngular3Pt angular = doc.Entities.OfType<ACadSharp.Entities.DimensionAngular3Pt>().Single();
            Assert.AreEqual(0.0, Distance(angular.AngleVertex, new GeoPoint(0, 0, 0)), 1e-8);
            Assert.AreEqual(0.0, Distance(angular.FirstPoint, new GeoPoint(10, 0, 0)), 1e-8);
            Assert.AreEqual(0.0, Distance(angular.SecondPoint, new GeoPoint(0, 10, 0)), 1e-8);
            Assert.AreEqual(0.0, Distance(angular.DefinitionPoint, new GeoPoint(14.14, 14.14, 0)), 1e-8);
            Assert.AreEqual(Math.PI / 2, angular.Measurement, 1e-8);
        }

        /// <summary>
        /// A dimension in a tilted plane: the definition points are world points, the text
        /// position (group 11) is a point in the OCS of the normal.
        /// </summary>
        [TestMethod]
        public void export_tilted_dimension_writes_the_text_position_in_the_ocs()
        {
            Project project = MakeProjectWithDimension(new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0));
            Dimension dim = (Dimension)project.GetActiveModel()[0];
            ModOp tilt = ModOp.Rotate(new GeoPoint(5, 5, 5), new GeoVector(1, 1, 0.3), new SweepAngle(0.65));
            dim.Modify(tilt);
            ACadSharp.CadDocument doc = ExportAndRead(project);

            ACadSharp.Entities.DimensionLinear linear = doc.Entities.OfType<ACadSharp.Entities.DimensionLinear>().Single();
            Assert.AreEqual(0.0, Distance(linear.FirstPoint, tilt * new GeoPoint(0, 0, 0)), 1e-8, "group 13 is a world point");
            Assert.AreEqual(0.0, Distance(linear.SecondPoint, tilt * new GeoPoint(100, 0, 0)), 1e-8, "group 14 is a world point");
            Assert.AreEqual(0.0, Distance(linear.DefinitionPoint, tilt * new GeoPoint(100, 20, 0)), 1e-8, "group 10 is a world point");
            GeoVector n = dim.Normal.Normalized;
            Assert.AreEqual(0.0, (new GeoVector(linear.Normal.X, linear.Normal.Y, linear.Normal.Z) - n).Length, 1e-8);
            Assert.IsTrue(Math.Abs(n.z) < 0.99, "test setup: the dimension must be tilted");
            // back from the OCS, by AutoCAD's arbitrary axis algorithm
            GeoVector ax = (Math.Abs(n.x) < 1.0 / 64 && Math.Abs(n.y) < 1.0 / 64) ? GeoVector.YAxis ^ n : GeoVector.ZAxis ^ n;
            ax.Norm();
            GeoVector ay = n ^ ax;
            GeoVector rotated = Math.Cos(linear.Rotation) * ax + Math.Sin(linear.Rotation) * ay;
            Assert.AreEqual(0.0, (rotated - tilt * GeoVector.XAxis).Length, 1e-8,
                "group 50 is the direction of the dimension line, as an angle in the OCS");
            CSMath.XYZ t = linear.TextMiddlePoint;
            GeoPoint text = GeoPoint.Origin + t.X * ax + t.Y * ay + t.Z * n;
            Assert.AreEqual((tilt * GeoPoint.Origin - GeoPoint.Origin) * n, t.Z, 1e-8,
                "the elevation of the text is the one of the dimension's plane");
            Assert.IsTrue((text | (tilt * new GeoPoint(50, 20, 0))) < 10.0,
                "the text sits at the middle of the dimension line, not at " + text);
        }

        /// <summary>
        /// A coordinate dimension has no DXF entity that carries its meaning; it keeps its
        /// picture as a block instead of getting lost.
        /// </summary>
        [TestMethod]
        public void export_coordinate_dimension_keeps_its_picture_as_a_block()
        {
            Project project = Project.CreateSimpleProject();
            Dimension dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimCoord;
            dim.DimensionStyle = project.DimensionStyleList.Current;
            dim.Normal = GeoVector.ZAxis;
            dim.DimLineDirection = GeoVector.XAxis;
            dim.DimLineRef = new GeoPoint(0, -20, 0);
            dim.AddPoint(new GeoPoint(0, 0, 0));
            dim.AddPoint(new GeoPoint(40, 10, 0));
            dim.AddPoint(new GeoPoint(100, 30, 0));
            project.GetActiveModel().Add(dim);
            ACadSharp.CadDocument doc = ExportAndRead(project);

            Assert.AreEqual(0, doc.Entities.OfType<ACadSharp.Entities.Dimension>().Count());
            ACadSharp.Entities.Insert insert = doc.Entities.OfType<ACadSharp.Entities.Insert>().Single();
            Assert.IsTrue(insert.Block.Entities.Count > 0, "the block holds what CADability drew");
            Assert.IsFalse(insert.Block.IsAnonymous, "an INSERT's block keeps its name, it is not a *D block");
        }

        /// <summary>
        /// The notes the import keeps about a DIMENSION are not written back as XData of the
        /// INSERT that the imported block becomes.
        /// </summary>
        [TestMethod]
        public void export_does_not_write_the_dimension_userdata_as_xdata()
        {
            Project project = ImportDxf(DimensionWithBlock);
            Assert.IsNotNull(project.GetActiveModel()[0].UserData["CADability.DxfDimension"]);
            ACadSharp.CadDocument doc = ExportAndRead(project);

            ACadSharp.Entities.Insert insert = doc.Entities.OfType<ACadSharp.Entities.Insert>().Single();
            Assert.AreEqual(0, insert.ExtendedData.Count(), "no XData for the import notes");
        }

        /// <summary>
        /// The full round trip: what is written is read back as a dimension, with its measured
        /// value intact. The picture contains the measurement text, which needs GDI.
        /// </summary>
        [TestMethod]
        public void export_dimension_round_trips_as_a_dxf_dimension()
        {
            RequireGdi();
            Project project = MakeProjectWithDimension(new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0));
            Model reimported = ExportAndImport(project);

            Block block = SingleDimensionBlock(reimported);
            Assert.AreEqual("DimensionLinear", block.UserData["CADability.DxfDimension"],
                "it should be a DIMENSION entity, not a heap of lines");
            Assert.AreEqual(100.0, (double)block.UserData["CADability.DxfDimension.Measurement"], 1e-6,
                "the measured value should survive the round trip");
            Assert.IsTrue(block.Children.Count > 0, "the picture should travel in the anonymous block");
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

        // A radius or diameter dimension of the circle around (10,10) with radius 25, its
        // dimension line ending 10 beyond the circle at (45,10)
        private static Dimension MakeCircleDimension(Project project, Dimension.EDimType type)
        {
            Dimension dim = Dimension.Construct();
            dim.DimType = type;
            dim.DimensionStyle = project.DimensionStyleList.Current;
            dim.Normal = GeoVector.ZAxis;
            dim.AddPoint(new GeoPoint(10, 10, 0));
            dim.Radius = 25.0;
            dim.DimLineRef = new GeoPoint(45, 10, 0);
            return dim;
        }

        private string Export(Project project)
        {
            string file = this.TestContext.TestName + ".dxf";
            Assert.IsTrue(project.Export(file, "dxf"), "export should succeed");
            return file;
        }

        private ACadSharp.CadDocument ExportAndRead(Project project)
        {
            return ACadSharp.IO.DxfReader.Read(Export(project));
        }

        private Model ExportAndImport(Project project)
        {
            Project read = Project.ReadFromFile(Export(project), "dxf");
            Assert.IsNotNull(read, "the exported file should be readable again");
            Model model = read.GetActiveModel();
            Assert.IsNotNull(model);
            return model;
        }

        private static double Distance(CSMath.XYZ p, GeoPoint q) => new GeoPoint(p.X, p.Y, p.Z) | q;

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
