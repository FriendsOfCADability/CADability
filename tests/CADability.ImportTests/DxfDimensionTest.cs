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

        // --- Stufe 3: DIMENSION as CADability's own Dimension --------------------------------

        // Header, a red layer "Bemassung" and a dimension style "LOGICAL" with values that are
        // nothing like CADability's defaults, so a translation is visible in the assertions.
        private const string StyledPrologue = @"  0
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
LOGICAL
 70
0
 40
2.0
 41
1.5
 42
0.5
 44
1.25
140
3.5
271
3
  0
ENDTAB
  0
TABLE
  2
APPID
  0
APPID
  2
ACAD
 70
0
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
ENDBLK
  8
0
  0
ENDSEC
  0
SECTION
  2
ENTITIES
";

        private const string Epilogue = @"  0
ENDSEC
  0
EOF
";

        /// <summary>
        /// With the option on, a linear dimension is no longer a picture but a dimension:
        /// measured points, dimension line and a text CADability computes itself.
        /// </summary>
        [TestMethod]
        public void import_dxf_linear_dimension_as_a_dimension_object()
        {
            Dimension dim = SingleDimensionObject(ImportAsDimensions(StyledPrologue + @"  0
DIMENSION
  8
Bemassung
100
AcDbEntity
100
AcDbDimension
  2
*D1
  3
LOGICAL
 70
32
 10
0.0
 20
20.0
 30
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
100
AcDbRotatedDimension
 50
0.0
" + Epilogue));

            Assert.AreEqual(Dimension.EDimType.DimPoints, dim.DimType);
            Assert.AreEqual(2, dim.PointCount, "the two measured points");
            Assert.AreEqual(0.0, dim.GetPoint(0) | new GeoPoint(0, 0, 0), 1e-8);
            Assert.AreEqual(0.0, dim.GetPoint(1) | new GeoPoint(100, 0, 0), 1e-8);
            Assert.AreEqual(0.0, dim.DimLineRef | new GeoPoint(0, 20, 0), 1e-8, "the dimension line");
            Assert.AreEqual(0.0, new SweepAngle(dim.DimLineDirection, GeoVector.XAxis).Radian, 1e-8,
                "rotation 0 means the dimension runs along the x axis");
            Assert.AreEqual("100", dim.GetDimText(0), "the text is measured, not copied");
        }

        /// <summary>
        /// The DIMSTYLE variables have to arrive, otherwise the rebuilt dimension is drawn with
        /// CADability's defaults and looks nothing like the original. DIMSCALE (40) is applied
        /// to the sizes here rather than kept, because CADability draws its sizes as they are.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_translates_the_dimension_style()
        {
            Dimension dim = SingleDimensionObject(ImportAsDimensions(StyledPrologue + LinearDimension + Epilogue));
            CADability.Attribute.DimensionStyle style = dim.DimensionStyle;

            Assert.IsNotNull(style, "a rebuilt dimension needs a style to draw with");
            Assert.AreEqual("LOGICAL", style.Name, "the style should keep the name from the file");
            Assert.AreEqual(7.0, style.TextSize, 1e-8, "DIMTXT 3.5 times DIMSCALE 2");
            Assert.AreEqual(3.0, style.SymbolSize, 1e-8, "DIMASZ 1.5 times DIMSCALE 2");
            Assert.AreEqual(1.0, style.ExtLineOffset, 1e-8, "DIMEXO 0.5 times DIMSCALE 2");
            Assert.AreEqual(2.5, style.ExtLineExtension, 1e-8, "DIMEXE 1.25 times DIMSCALE 2");
            Assert.AreEqual(0.001, style.Round, 1e-12, "DIMDEC 3 rounds to a thousandth");
        }

        /// <summary>
        /// Almost every dimension in a real drawing overrides something of the style it names.
        /// An override has to reach the dimension it belongs to without changing its neighbours,
        /// so it gets a style of its own.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_style_override_does_not_leak()
        {
            // The second dimension overrides DIMTXT (group code 140) to 9.0 through XData.
            string overridden = LinearDimension + @"1001
ACAD
1000
DSTYLE
1002
{
1070
140
1040
9.0
1002
}
";
            Model model = ImportAsDimensions(StyledPrologue + LinearDimension + overridden + Epilogue);
            Dimension[] dims = model.AllObjects.OfType<Dimension>().ToArray();
            Assert.AreEqual(2, dims.Length, "both dimensions should be rebuilt");

            double[] sizes = dims.Select(d => d.DimensionStyle.TextSize).OrderBy(t => t).ToArray();
            Assert.AreEqual(7.0, sizes[0], 1e-8, "the plain dimension keeps DIMTXT 3.5 times DIMSCALE 2");
            Assert.AreEqual(18.0, sizes[1], 1e-8, "the overridden one gets 9.0 times DIMSCALE 2");
            Assert.AreNotSame(dims[0].DimensionStyle, dims[1].DimensionStyle,
                "an override must not be written into the style its neighbours use");
        }

        /// <summary>
        /// DXF puts a radial dimension's center in group 10 and the point on the circle in
        /// group 15 - the other way round than for a diameter, which is easy to get backwards.
        /// </summary>
        [TestMethod]
        public void import_dxf_radial_dimension_as_a_dimension_object()
        {
            Dimension dim = SingleDimensionObject(ImportAsDimensions(StyledPrologue + @"  0
DIMENSION
  8
Bemassung
100
AcDbEntity
100
AcDbDimension
  2
*D1
  3
LOGICAL
 70
36
 10
10.0
 20
10.0
 30
0.0
100
AcDbRadialDimension
 15
35.0
 25
10.0
 35
0.0
 40
0.0
" + Epilogue));

            Assert.AreEqual(Dimension.EDimType.DimRadius, dim.DimType);
            Assert.AreEqual(0.0, dim.GetPoint(0) | new GeoPoint(10, 10, 0), 1e-8, "group 10 is the center");
            Assert.AreEqual(25.0, dim.Radius, 1e-8, "the distance to the point on the circle");
            Assert.AreEqual("25", dim.GetDimText(0));
        }

        /// <summary>
        /// A diameter dimension names the two ends of the diameter; the center is their middle.
        /// </summary>
        [TestMethod]
        public void import_dxf_diameter_dimension_as_a_dimension_object()
        {
            Dimension dim = SingleDimensionObject(ImportAsDimensions(StyledPrologue + @"  0
DIMENSION
  8
Bemassung
100
AcDbEntity
100
AcDbDimension
  2
*D1
  3
LOGICAL
 70
35
 10
30.0
 20
10.0
 30
0.0
100
AcDbDiametricDimension
 15
-10.0
 25
10.0
 35
0.0
 40
0.0
" + Epilogue));

            Assert.AreEqual(Dimension.EDimType.DimDiameter, dim.DimType);
            Assert.AreEqual(0.0, dim.GetPoint(0) | new GeoPoint(10, 10, 0), 1e-8, "the middle of the two ends");
            Assert.AreEqual(20.0, dim.Radius, 1e-8);
            Assert.AreEqual("40", dim.GetDimText(0), "a diameter dimension shows twice the radius");
        }

        /// <summary>
        /// An angular dimension names a vertex and two rays, and says which of the four sectors
        /// it means by the point the dimension arc runs through. Here that point is in the lower
        /// right quadrant, so the measured angle is the 270° one, not the 90° one.
        /// </summary>
        [TestMethod]
        public void import_dxf_angular_dimension_takes_the_sector_the_arc_point_names()
        {
            Dimension dim = SingleDimensionObject(ImportAsDimensions(StyledPrologue + @"  0
DIMENSION
  8
Bemassung
100
AcDbEntity
100
AcDbDimension
  2
*D1
  3
LOGICAL
 70
37
 10
7.07
 20
-7.07
 30
0.0
100
AcDb3PointAngularDimension
 13
10.0
 23
0.0
 33
0.0
 14
0.0
 24
10.0
 34
0.0
 15
0.0
 25
0.0
 35
0.0
" + Epilogue));

            Assert.AreEqual(Dimension.EDimType.DimAngle, dim.DimType);
            Assert.AreEqual(3, dim.PointCount, "center and one point on each leg");
            Assert.AreEqual(0.0, dim.GetPoint(0) | new GeoPoint(0, 0, 0), 1e-8, "group 15 is the vertex");
            // Counterclockwise from the y leg to the x leg is 270°, and the arc point at -45°
            // lies on that side; the other way round it would read 90°.
            Assert.AreEqual("270", dim.GetDimText(0), "the sector the dimension arc runs through");
        }

        /// <summary>
        /// CADability has no arc length dimension, so that one keeps the picture AutoCAD drew
        /// instead of turning into something it is not.
        /// </summary>
        [TestMethod]
        public void import_dxf_arc_length_dimension_keeps_its_block()
        {
            Model model = ImportAsDimensions(DimensionWithBlock);
            Assert.AreEqual(1, model.AllObjects.Count);
            Assert.IsInstanceOfType(model.AllObjects[0], typeof(Dimension),
                "an aligned dimension has a counterpart and should be rebuilt");

            // The same file, but as the one type CADability cannot express
            string arcLength = DimensionWithBlock.Replace("AcDbAlignedDimension", "AcDbArcDimension");
            Model fallback = ImportAsDimensions(arcLength);
            Assert.AreEqual(1, fallback.AllObjects.Count, "it must not get lost either");
            Assert.IsInstanceOfType(fallback.AllObjects[0], typeof(Block),
                "without a counterpart the picture is kept");
        }

        /// <summary>
        /// Group 1 overrides the measured text. "&lt;&gt;" inside it stands for the measurement,
        /// which CADability has no placeholder for - the text around it becomes prefix and
        /// postfix so the number keeps being measured.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_text_override_keeps_the_measurement()
        {
            string withText = LinearDimension.Replace("100\nAcDbAlignedDimension",
                "  1\nca. <> mm\n100\nAcDbAlignedDimension");
            Assert.AreNotEqual(LinearDimension, withText, "the text override should be inserted");
            Dimension dim = SingleDimensionObject(ImportAsDimensions(StyledPrologue + withText + Epilogue));

            Assert.AreEqual("ca. ", dim.GetPrefix(0));
            Assert.AreEqual("100", dim.GetDimText(0), "the value keeps being measured");
            Assert.AreEqual(" mm", dim.GetPostfix(0));
        }

        /// <summary>
        /// Without the option nothing changes: the dimension stays the picture AutoCAD drew.
        /// </summary>
        [TestMethod]
        public void import_dxf_dimension_stays_a_block_unless_asked()
        {
            Model model = ImportDxf(StyledPrologue + LinearDimension + Epilogue);
            Assert.AreEqual(1, model.AllObjects.Count);
            Assert.IsInstanceOfType(model.AllObjects[0], typeof(Block),
                "the option is off by default");
        }

        /// <summary>
        /// Import and export have to agree on which definition point means what. The radial
        /// dimension is where that is easiest to get backwards - DXF puts the center in group
        /// 10 and the point on the circle in group 15, the other way round than for a diameter.
        /// The hand written file above pins the convention; this ties the export to it.
        /// </summary>
        [TestMethod]
        public void dimension_survives_import_export_import()
        {
            Model imported = ImportAsDimensions(StyledPrologue + LinearDimension + Epilogue);
            Dimension before = SingleDimensionObject(imported);

            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            foreach (IGeoObject go in imported.AllObjects) model.Add(go.Clone());

            Settings.GlobalSettings.SetValue("DxfImport.DimensionsAsDimension", true);
            Dimension after;
            try { after = SingleDimensionObject(ExportAndImport(project)); }
            finally { Settings.GlobalSettings.SetValue("DxfImport.DimensionsAsDimension", false); }

            Assert.AreEqual(before.DimType, after.DimType);
            Assert.AreEqual(0.0, before.GetPoint(0) | after.GetPoint(0), 1e-6, "first measured point");
            Assert.AreEqual(0.0, before.GetPoint(1) | after.GetPoint(1), 1e-6, "second measured point");
            // AutoCAD puts the definition point where the dimension line meets the second
            // extension line, the source file had it at the first one - the same line either
            // way, so what has to match is the line, not the point naming it.
            GeoVector offset = after.DimLineRef - before.DimLineRef;
            GeoVector along = before.DimLineDirection;
            along.Norm();
            Assert.AreEqual(0.0, (offset - (offset * along) * along).Length, 1e-6,
                "the dimension line should be the same line");
            Assert.AreEqual(0.0, new SweepAngle(before.DimLineDirection, after.DimLineDirection).Radian, 1e-6,
                "and run in the same direction");
            Assert.AreEqual(before.GetDimText(0), after.GetDimText(0), "the measured text");
        }

        /// <summary>
        /// The same for a radius, where the two definition points are not interchangeable.
        /// </summary>
        [TestMethod]
        public void radial_dimension_survives_import_export_import()
        {
            Project project = Project.CreateSimpleProject();
            Dimension dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimRadius;
            dim.DimensionStyle = project.DimensionStyleList.Current;
            dim.Normal = GeoVector.ZAxis;
            dim.AddPoint(new GeoPoint(10, 10, 0));
            dim.Radius = 25.0;
            dim.DimLineRef = new GeoPoint(45, 10, 0); // 10 beyond the circle
            project.GetActiveModel().Add(dim);

            Settings.GlobalSettings.SetValue("DxfImport.DimensionsAsDimension", true);
            Dimension after;
            try { after = SingleDimensionObject(ExportAndImport(project)); }
            finally { Settings.GlobalSettings.SetValue("DxfImport.DimensionsAsDimension", false); }

            Assert.AreEqual(Dimension.EDimType.DimRadius, after.DimType);
            Assert.AreEqual(0.0, after.GetPoint(0) | new GeoPoint(10, 10, 0), 1e-6,
                "the center, not the point on the circle");
            Assert.AreEqual(25.0, after.Radius, 1e-6);
        }

        private const string LinearDimension = @"  0
DIMENSION
  8
Bemassung
100
AcDbEntity
100
AcDbDimension
  2
*D1
  3
LOGICAL
 70
32
 10
0.0
 20
20.0
 30
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
100
AcDbRotatedDimension
 50
0.0
";

        private Model ImportAsDimensions(string dxf)
        {
            Settings.GlobalSettings.SetValue("DxfImport.DimensionsAsDimension", true);
            try { return ImportDxf(dxf); }
            finally { Settings.GlobalSettings.SetValue("DxfImport.DimensionsAsDimension", false); }
        }

        private static Dimension SingleDimensionObject(Model model)
        {
            Assert.AreEqual(1, model.AllObjects.Count, "the DIMENSION should import as one object");
            Dimension dim = model.AllObjects[0] as Dimension;
            Assert.IsNotNull(dim, "with the option on it should be a Dimension, not a "
                + model.AllObjects[0].GetType().Name);
            return dim;
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
