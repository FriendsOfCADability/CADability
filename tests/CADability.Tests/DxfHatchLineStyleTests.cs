using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Tables;
using CADability.Attribute;
using CADability.GeoObject;
using AcadHatch = ACadSharp.Entities.Hatch;
using AcadInsert = ACadSharp.Entities.Insert;
using AcadLayer = ACadSharp.Tables.Layer;
using AcadLine = ACadSharp.Entities.Line;
using Block = CADability.GeoObject.Block;
using Hatch = CADability.GeoObject.Hatch;
using Line = CADability.GeoObject.Line;

namespace CADability.Tests
{
    /// <summary>
    /// The line styles of pattern hatches and linetypes read from DXF. A line hatch style was found again by angle, distance
    /// and line width only, so pattern lines with other dashes took the dashes of the first one, and the dashes kept the
    /// negative gaps of DXF, which CADability does not draw. The lines of a pattern hatch in a block always had the lineweight
    /// of the HATCH, never that of the INSERT for ByBlock. And an entity with the linetype ByBlock outside of blocks got a
    /// pattern "ByBlock" that was always continuous, instead of "Continuous".
    /// </summary>
    [TestClass]
    public class DxfHatchLineStyleTests
    {
        public TestContext TestContext { get; set; }

        private static AcadHatch PatternHatch(double y, double angle, CSMath.XY offset, params double[] dashes)
        {
            AcadHatch hatch = new AcadHatch { IsSolid = false, Pattern = new ACadSharp.Entities.HatchPattern("TEST") };
            hatch.Paths.Add(new AcadHatch.BoundaryPath(new AcadHatch.BoundaryPath.Edge[]
            {
                new AcadHatch.BoundaryPath.Polyline(new[] { new CSMath.XYZ(0, y, 0), new CSMath.XYZ(10, y, 0), new CSMath.XYZ(10, y + 10, 0), new CSMath.XYZ(0, y + 10, 0) }, true)
            }));
            hatch.Pattern.Lines.Add(new ACadSharp.Entities.HatchPattern.Line { Angle = angle, Offset = offset, DashLengths = dashes.ToList() });
            return hatch;
        }

        private Project ImportProject(CadDocument doc)
        {
            string file = TestContext.TestName + ".dxf";
            DxfWriter.Write(file, doc);
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            return project;
        }

        private static HatchStyleLines StyleAt(IEnumerable<IGeoObject> objects, double y)
            => (HatchStyleLines)objects.OfType<Hatch>().Single(h => Math.Abs(h.GetExtent(0.0).Ymin - y) < 1e-6).HatchStyle;

        private static IEnumerable<IGeoObject> AllObjects(IEnumerable<IGeoObject> objects)
        {
            foreach (IGeoObject go in objects)
            {
                yield return go;
                if (go is Block block && !(go is Hatch)) foreach (IGeoObject child in AllObjects(block.Children)) yield return child;
            }
        }

        [DataTestMethod]
        [DataRow(new double[] { 0.5, -0.25 }, new double[] { 0.5, 0.25 }, DisplayName = "dash and gap")]
        [DataRow(new double[] { 0.5, -0.25, 0.0, -0.25 }, new double[] { 0.5, 0.25, 0.0, 0.25 }, DisplayName = "dash and dot")]
        [DataRow(new double[] { -0.25, 0.5 }, new double[] { 0.5, 0.25 }, DisplayName = "starting with a gap")]
        [DataRow(new double[] { 0.5, -0.25, 0.5 }, new double[] { 1.0, 0.25 }, DisplayName = "ending with a dash")]
        [DataRow(new double[] { 0.5, -0.1, -0.15, 0.25, 0.25, -0.25 }, new double[] { 0.5, 0.25, 0.5, 0.25 }, DisplayName = "repeated dashes and gaps")]
        [DataRow(new double[] { 0.5 }, new double[0], DisplayName = "continuous")]
        [DataRow(new double[0], new double[0], DisplayName = "empty")]
        public void dxf_dashes_become_a_line_pattern(double[] dxf, double[] expected)
        {
            CollectionAssert.AreEqual(expected, DXF.Import.PatternFromDxfDashes(dxf));
        }

        [TestMethod]
        public void pattern_hatches_are_told_apart_by_their_dashes()
        {
            CadDocument doc = new CadDocument();
            CSMath.XY offset = new CSMath.XY(0.3, 1.0); // the lines are 1 apart, 0.3 is the shift of the dashes along them
            doc.Entities.Add(PatternHatch(0, 0.0, offset, 0.5, -0.25));
            doc.Entities.Add(PatternHatch(20, 0.0, offset, 0.25, -0.25));
            doc.Entities.Add(PatternHatch(40, 0.0, offset, 0.5, -0.25));
            doc.Entities.Add(PatternHatch(60, 0.0, offset));

            Project project = ImportProject(doc);
            IGeoObject[] objects = project.GetActiveModel().AllObjects.ToArray();
            HatchStyleLines first = StyleAt(objects, 0), second = StyleAt(objects, 20), third = StyleAt(objects, 40), continuous = StyleAt(objects, 60);

            Assert.AreSame(first, third, "the same pattern line, the same style");
            Assert.AreNotSame(first, second, "other dashes, another style");
            Assert.AreNotSame(first, continuous);
            CollectionAssert.AreEqual(new[] { 0.5, 0.25 }, first.LinePattern.Pattern);
            CollectionAssert.AreEqual(new[] { 0.25, 0.25 }, second.LinePattern.Pattern);
            Assert.AreEqual(0, continuous.LinePattern.Pattern.Length);
            Assert.AreEqual(LinePattern.Scaling.World, first.LinePattern.Scale, "the dashes are in drawing units");
            foreach (HatchStyleLines style in new[] { first, second, continuous })
            {
                Assert.AreEqual(1.0, style.LineDistance, 1e-9, "the distance of the lines is perpendicular to them");
                Assert.AreSame(style, project.HatchStyleList.Find(style.Name));
                Assert.IsTrue(project.LinePatternList.FindIndex(style.LinePattern) >= 0);
            }
            foreach (Line line in objects.OfType<Hatch>().SelectMany(h => h.Children.OfType<Line>()))
            {
                Assert.AreSame(((HatchStyleLines)((Hatch)line.Owner).HatchStyle).LinePattern, line.LinePattern);
            }
        }

        [TestMethod]
        public void pattern_hatch_lines_in_a_block_take_the_lineweight_of_the_insert()
        {
            CadDocument doc = new CadDocument();
            AcadLayer layerZero = doc.Layers["0"];
            AcadLayer walls = new AcadLayer("walls") { LineWeight = LineWeightType.W50 };
            doc.Layers.Add(walls);

            BlockRecord block = new BlockRecord("hatched");
            AcadHatch byBlock = PatternHatch(0, Math.PI / 4, new CSMath.XY(-1, 1), 0.5, -0.25);
            byBlock.Layer = layerZero;
            byBlock.LineWeight = LineWeightType.ByBlock;
            block.Entities.Add(byBlock);
            AcadHatch byLayer = PatternHatch(20, Math.PI / 4, new CSMath.XY(-1, 1), 0.5, -0.25);
            byLayer.Layer = layerZero;
            byLayer.LineWeight = LineWeightType.ByLayer;
            block.Entities.Add(byLayer);
            AcadHatch own = PatternHatch(40, Math.PI / 4, new CSMath.XY(-1, 1), 0.5, -0.25);
            own.Layer = layerZero;
            own.LineWeight = LineWeightType.W100;
            block.Entities.Add(own);
            doc.BlockRecords.Add(block);

            doc.Entities.Add(new AcadInsert(block) { Layer = walls, LineWeight = LineWeightType.W35 });
            doc.Entities.Add(new AcadInsert(block) { Layer = layerZero, LineWeight = LineWeightType.W70, InsertPoint = new CSMath.XYZ(100, 0, 0) });

            Project project = ImportProject(doc);
            List<Block> blocks = project.GetActiveModel().AllObjects.OfType<Block>().ToList();
            Block onWalls = blocks.Single(b => b.Layer.Name == "walls"), onZero = blocks.Single(b => b.Layer.Name == "0");

            Assert.AreEqual(0.35, StyleAt(onWalls.Children, 0).LineWidth.Width, 1e-9, "ByBlock: the INSERT");
            Assert.AreEqual(0.5, StyleAt(onWalls.Children, 20).LineWidth.Width, 1e-9, "layer 0, ByLayer: the INSERT's layer");
            Assert.AreEqual(1.0, StyleAt(onWalls.Children, 40).LineWidth.Width, 1e-9, "explicit lineweight");
            Assert.AreEqual(0.7, StyleAt(onZero.Children, 0).LineWidth.Width, 1e-9, "ByBlock: the other INSERT");
            Assert.AreEqual(1.0, StyleAt(onZero.Children, 40).LineWidth.Width, 1e-9);

            foreach (Hatch hatch in blocks.SelectMany(b => b.Children.OfType<Hatch>()))
            {
                HatchStyleLines style = (HatchStyleLines)hatch.HatchStyle;
                Assert.AreSame(style, project.HatchStyleList.Find(style.Name), "hatch style not in the project: " + style.Name);
                Assert.IsTrue(project.LineWidthList.FindIndex(style.LineWidth) >= 0, "line width not in the project: " + style.LineWidth.Name);
                foreach (Line line in hatch.Children.OfType<Line>()) Assert.AreSame(style.LineWidth, line.LineWidth);
            }
        }

        [TestMethod]
        public void linetype_by_block_outside_of_blocks_is_continuous()
        {
            CadDocument doc = new CadDocument();
            LineType dashed = new LineType("DASHED");
            dashed.AddSegment(new LineType.Segment { Length = 0.5 });
            dashed.AddSegment(new LineType.Segment { Length = -0.25 });
            doc.LineTypes.Add(dashed);
            AcadLayer layer = new AcadLayer("dashed") { LineType = dashed };
            doc.Layers.Add(layer);
            doc.Entities.Add(new AcadLine(new CSMath.XYZ(0, 0, 0), new CSMath.XYZ(10, 0, 0)) { LineType = doc.LineTypes[LineType.ByBlockName], Layer = layer });
            doc.Entities.Add(new AcadLine(new CSMath.XYZ(0, 10, 0), new CSMath.XYZ(10, 10, 0)) { LineType = doc.LineTypes[LineType.ByLayerName], Layer = layer });
            BlockRecord block = new BlockRecord("byblock");
            block.Entities.Add(new AcadLine(new CSMath.XYZ(0, 20, 0), new CSMath.XYZ(10, 20, 0)) { LineType = doc.LineTypes[LineType.ByBlockName] });
            doc.BlockRecords.Add(block);
            doc.Entities.Add(new AcadInsert(block) { LineType = doc.LineTypes[LineType.ByBlockName] });

            Project project = ImportProject(doc);
            Line[] lines = AllObjects(project.GetActiveModel().AllObjects).OfType<Line>().OrderBy(l => l.StartPoint.y).ToArray();
            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual("Continuous", lines[0].LinePattern.Name, "ByBlock outside of blocks");
            Assert.AreEqual("DASHED", lines[1].LinePattern.Name, "ByLayer: the layer");
            Assert.AreEqual("Continuous", lines[2].LinePattern.Name, "ByBlock in a block placed with ByBlock");
            CollectionAssert.AreEqual(new[] { 0.5, 0.25 }, lines[1].LinePattern.Pattern);
            Assert.IsNull(project.LinePatternList.Find(LineType.ByBlockName), "no pattern for ByBlock");
            Assert.IsNull(project.LinePatternList.Find(LineType.ByLayerName), "no pattern for ByLayer");
        }
    }
}
