using System.Drawing;
using System.Runtime.CompilerServices;
using ACadSharp;
using ACadSharp.IO;
using ACadSharp.Tables;
using CADability.Attribute;
using CADability.GeoObject;
using AcadColor = ACadSharp.Color;
using AcadLine = ACadSharp.Entities.Line;
using AcadInsert = ACadSharp.Entities.Insert;
using AcadLayer = ACadSharp.Tables.Layer;
using Block = CADability.GeoObject.Block;
using Color = System.Drawing.Color;
using Line = CADability.GeoObject.Line;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #293 "Block color not rendered": the lines of a block that sits on the red layer
    /// "rebars" were drawn black. The reporter's drawing came from a DXF whose block contents are
    /// on layer "0" and ByLayer, which AutoCAD draws with the layer of the INSERT. The import
    /// resolved them against layer "0" instead (black), and ByBlock contents became black as well,
    /// so the colors were wrong in the data and the renderer showed what it was given.
    /// A cloned Block also lost its color for children with <see cref="ColorDef.CDfromParent"/>.
    /// </summary>
    [TestClass]
    public class BlockColorTests
    {
        public TestContext TestContext { get; set; }

        private static readonly Color Red = Color.FromArgb(255, 0, 0);      // ACI 1
        private static readonly Color Yellow = Color.FromArgb(255, 255, 0); // ACI 2
        private static readonly Color Green = Color.FromArgb(0, 255, 0);    // ACI 3
        private static readonly Color Cyan = Color.FromArgb(0, 255, 255);   // ACI 4
        private static readonly Color Blue = Color.FromArgb(0, 0, 255);     // ACI 5

        // y coordinates that identify the lines of the test block
        private const double LayerZeroByLayerY = 0, ByBlockY = 10, OwnLayerY = 20, ExplicitY = 30;

        [TestMethod]
        public void DxfBlockContentsOnLayerZeroAndByBlockTakeTheInsertAttributes()
        {
            CadDocument doc = new CadDocument();
            AcadLayer layerZero = doc.Layers["0"];
            AcadLayer rebars = new AcadLayer("rebars") { Color = new AcadColor(1) };
            AcadLayer walls = new AcadLayer("walls") { Color = new AcadColor(4) };
            AcadLayer green = new AcadLayer("green") { Color = new AcadColor(3) };
            doc.Layers.Add(rebars);
            doc.Layers.Add(walls);
            doc.Layers.Add(green);

            BlockRecord block = new BlockRecord("rebar");
            block.Entities.Add(MakeLine(LayerZeroByLayerY, layerZero, AcadColor.ByLayer));
            block.Entities.Add(MakeLine(ByBlockY, layerZero, AcadColor.ByBlock));
            block.Entities.Add(MakeLine(OwnLayerY, green, AcadColor.ByLayer));
            block.Entities.Add(MakeLine(ExplicitY, layerZero, new AcadColor(5)));
            doc.BlockRecords.Add(block);

            // like the reporter's drawing: an INSERT on the red layer with ByLayer color
            doc.Entities.Add(new AcadInsert(block) { Layer = rebars, Color = AcadColor.ByLayer });
            // an INSERT with an explicit color: layer-0 ByLayer contents still follow the layer
            // of the INSERT, only ByBlock contents take the INSERT's own color
            doc.Entities.Add(new AcadInsert(block) { Layer = walls, Color = new AcadColor(2), InsertPoint = new CSMath.XYZ(100, 0, 0) });

            Model model = Import(doc);
            List<Block> blocks = model.AllObjects.OfType<Block>().ToList();
            Assert.AreEqual(2, blocks.Count);
            Block onRebars = blocks.Single(b => b.Layer.Name == "rebars");
            Block onWalls = blocks.Single(b => b.Layer.Name == "walls");

            Dictionary<double, Color> painted = PaintedColors(onRebars);
            Assert.AreEqual(Red.ToArgb(), painted[LayerZeroByLayerY].ToArgb(), "layer 0, ByLayer: color of the INSERT's layer");
            Assert.AreEqual(Red.ToArgb(), painted[ByBlockY].ToArgb(), "ByBlock: color of the INSERT");
            Assert.AreEqual(Green.ToArgb(), painted[OwnLayerY].ToArgb(), "own layer, ByLayer: color of that layer");
            Assert.AreEqual(Blue.ToArgb(), painted[ExplicitY].ToArgb(), "explicit color stays");
            Assert.AreEqual("rebars", LineAt(onRebars, LayerZeroByLayerY).Layer.Name, "layer 0 contents go to the INSERT's layer");
            Assert.AreEqual("rebars", LineAt(onRebars, ExplicitY).Layer.Name);
            Assert.AreEqual("green", LineAt(onRebars, OwnLayerY).Layer.Name);

            painted = PaintedColors(onWalls);
            Assert.AreEqual(Cyan.ToArgb(), painted[LayerZeroByLayerY].ToArgb(), "layer 0, ByLayer: color of the INSERT's layer, not of the INSERT");
            Assert.AreEqual(Yellow.ToArgb(), painted[ByBlockY].ToArgb(), "ByBlock: color of the INSERT");
            Assert.AreEqual(Green.ToArgb(), painted[OwnLayerY].ToArgb());
            Assert.AreEqual(Blue.ToArgb(), painted[ExplicitY].ToArgb());
            Assert.AreEqual("walls", LineAt(onWalls, LayerZeroByLayerY).Layer.Name);

            // the block's own color must follow later changes, as for any CADability Block
            onRebars.ColorDef = new ColorDef("magenta", Color.Magenta);
            Assert.AreEqual(Color.Magenta.ToArgb(), PaintedColors(onRebars)[ByBlockY].ToArgb());
        }

        [TestMethod]
        public void DxfNestedBlockContentsResolveAgainstTheOutermostInsert()
        {
            CadDocument doc = new CadDocument();
            AcadLayer layerZero = doc.Layers["0"];
            AcadLayer rebars = new AcadLayer("rebars") { Color = new AcadColor(1) };
            AcadLayer green = new AcadLayer("green") { Color = new AcadColor(3) };
            doc.Layers.Add(rebars);
            doc.Layers.Add(green);

            BlockRecord inner = new BlockRecord("inner");
            inner.Entities.Add(MakeLine(LayerZeroByLayerY, layerZero, AcadColor.ByLayer));
            inner.Entities.Add(MakeLine(ByBlockY, layerZero, AcadColor.ByBlock));
            doc.BlockRecords.Add(inner);

            BlockRecord outer = new BlockRecord("outer");
            // nested on layer 0 and ByBlock: everything comes from the outer INSERT
            outer.Entities.Add(new AcadInsert(inner) { Layer = layerZero, Color = AcadColor.ByBlock });
            // nested on its own layer with an explicit color: that wins over the outer INSERT
            outer.Entities.Add(new AcadInsert(inner) { Layer = green, Color = new AcadColor(5), InsertPoint = new CSMath.XYZ(100, 0, 0) });
            doc.BlockRecords.Add(outer);

            doc.Entities.Add(new AcadInsert(outer) { Layer = rebars, Color = AcadColor.ByLayer });

            Model model = Import(doc);
            Block placed = model.AllObjects.OfType<Block>().Single();
            Assert.AreEqual(2, placed.Count);
            Block nestedByBlock = (Block)placed.Item(0);
            Block nestedBlue = (Block)placed.Item(1);

            Dictionary<double, Color> painted = PaintedColors(nestedByBlock);
            Assert.AreEqual(Red.ToArgb(), painted[LayerZeroByLayerY].ToArgb());
            Assert.AreEqual(Red.ToArgb(), painted[ByBlockY].ToArgb());
            Assert.AreEqual("rebars", nestedByBlock.Layer.Name);
            Assert.AreEqual("rebars", LineAt(nestedByBlock, LayerZeroByLayerY).Layer.Name);

            painted = PaintedColors(nestedBlue);
            Assert.AreEqual(Green.ToArgb(), painted[LayerZeroByLayerY].ToArgb(), "layer 0 inside the nested block: layer of the nested INSERT");
            Assert.AreEqual(Blue.ToArgb(), painted[ByBlockY].ToArgb(), "ByBlock inside the nested block: color of the nested INSERT");
            Assert.AreEqual("green", LineAt(nestedBlue, LayerZeroByLayerY).Layer.Name);
        }

        [TestMethod]
        public void BlockColorStillReachesChildrenFromParentAfterSavingAsCdb()
        {
            CadDocument doc = new CadDocument();
            AcadLayer rebars = new AcadLayer("rebars") { Color = new AcadColor(1) };
            doc.Layers.Add(rebars);
            BlockRecord block = new BlockRecord("rebar");
            block.Entities.Add(MakeLine(ByBlockY, doc.Layers["0"], AcadColor.ByBlock));
            doc.BlockRecords.Add(block);
            doc.Entities.Add(new AcadInsert(block) { Layer = rebars, Color = AcadColor.ByLayer });
            string file = TestContext.TestName + ".dxf";
            DxfWriter.Write(file, doc);
            Project imported = Project.ReadFromFile(file, "dxf");

            MemoryStream written = new MemoryStream();
            imported.WriteToJson(written); // closes the stream
            Project project = ReadProject(new MemoryStream(written.ToArray()));
            Block placed = project.GetActiveModel().AllObjects.OfType<Block>().Single();

            Assert.AreEqual(Red.ToArgb(), PaintedColors(placed)[ByBlockY].ToArgb());
            placed.ColorDef = new ColorDef("blue", Blue);
            Assert.AreEqual(Blue.ToArgb(), PaintedColors(placed)[ByBlockY].ToArgb(), "the child must follow the block's color after reading");
            Assert.AreEqual(Blue.ToArgb(), PaintedColors((Block)placed.Clone())[ByBlockY].ToArgb());
        }

        [TestMethod]
        public void ClonedBlockKeepsItsColorForChildrenFromParent()
        {
            Block block = Block.Construct();
            block.Add(MakeFromParentLine(ByBlockY));
            block.ColorDef = new ColorDef("red", Red);
            Assert.AreEqual(Red.ToArgb(), PaintedColors(block)[ByBlockY].ToArgb(), "test setup");

            Block clone = (Block)block.Clone();
            Assert.AreEqual(Red.ToArgb(), PaintedColors(clone)[ByBlockY].ToArgb());

            // and the clone keeps following its color
            clone.ColorDef.Color = Blue;
            Assert.AreEqual(Blue.ToArgb(), PaintedColors(clone)[ByBlockY].ToArgb());
        }

        /// <summary>
        /// The reporter's project (issue #293). Its blocks were imported from DXF by an older
        /// CADability: the block is on layer "rebars" (red), its lines on layer "0" with the color
        /// "0:ByLayer" (black), which is why they are drawn black. The file cannot tell that the
        /// lines were meant to inherit, the fix is in the DXF import. With the lines set to
        /// <see cref="ColorDef.CDfromParent"/>, which is what the import produces now for such
        /// contents, they show the block's red, also on a copy of the block.
        /// </summary>
        [TestMethod]
        public void Issue293ProjectBlockShowsItsColorOnChildrenFromParent()
        {
            string file = CdbFile();
            Assert.IsTrue(File.Exists(file), $"test file not found: {file}");
            Project project = ReadProject(file);
            Block block = project.GetActiveModel().AllObjects.OfType<Block>().First(b => b.Name == "Top rebar elem.10");

            Assert.AreEqual("rebars", block.Layer.Name);
            Assert.AreEqual(Red.ToArgb(), block.ColorDef.Color.ToArgb());
            List<Line> lines = block.Children.OfType<Line>().ToList();
            Assert.AreEqual(block.Count, lines.Count);
            foreach (Line line in lines)
            {
                Assert.AreEqual("0", line.Layer.Name);
                Assert.AreEqual("0:ByLayer", line.ColorDef.Name);
            }
            Assert.IsTrue(RecordPaint(block).All(c => c.color.ToArgb() == Color.Black.ToArgb()),
                "the children are drawn in their own color");

            // what the import produces now: children with CDfromParent, added to the block
            // (adding is what connects them to this block's CDfromParent)
            block.Clear();
            foreach (Line line in lines) line.ColorDef = ColorDef.CDfromParent;
            block.Add(new GeoObjectList(lines.Cast<IGeoObject>()));
            Assert.IsTrue(RecordPaint(block).All(c => c.color.ToArgb() == Red.ToArgb()));
            Block copy = (Block)block.Clone();
            Assert.IsTrue(RecordPaint(copy).All(c => c.color.ToArgb() == Red.ToArgb()),
                "a copy of the block must show its color as well");
        }

        private static AcadLine MakeLine(double y, AcadLayer layer, AcadColor color)
        {
            return new AcadLine(new CSMath.XYZ(0, y, 0), new CSMath.XYZ(10, y, 0)) { Layer = layer, Color = color };
        }

        private static Line MakeFromParentLine(double y)
        {
            Line line = Line.Construct();
            line.SetTwoPoints(new GeoPoint(0, y, 0), new GeoPoint(10, y, 0));
            line.ColorDef = ColorDef.CDfromParent;
            return line;
        }

        private Model Import(CadDocument doc)
        {
            string file = TestContext.TestName + ".dxf";
            DxfWriter.Write(file, doc);
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            return project.GetActiveModel();
        }

        private static Line LineAt(Block block, double y)
        {
            return AllLines(block).Single(l => Math.Abs(l.StartPoint.y - y) < 1e-6);
        }

        private static IEnumerable<Line> AllLines(Block block)
        {
            foreach (IGeoObject child in block.Children)
            {
                if (child is Line line) yield return line;
                else if (child is Block nested) foreach (Line l in AllLines(nested)) yield return l;
            }
        }

        /// <summary>
        /// The color each line of the block is painted with, keyed by the line's y coordinate.
        /// </summary>
        private static Dictionary<double, Color> PaintedColors(Block block)
        {
            Dictionary<double, Color> res = new Dictionary<double, Color>();
            foreach ((Color color, GeoPoint[] points) in RecordPaint(block)) res.Add(Math.Round(points[0].y, 6), color);
            return res;
        }

        private static List<(Color color, GeoPoint[] points)> RecordPaint(IGeoObject go)
        {
            RecordingPaintTo3D paintTo3D = new RecordingPaintTo3D();
            go.PaintTo3D(paintTo3D);
            return paintTo3D.Polylines;
        }

        private static string CdbFile([CallerFilePath] string thisFile = "")
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile), "Files", "CDB", "issue293.cdb.json");

        private static Project ReadProject(string file)
        {
            try
            {
                return Project.ReadFromFile(file, "cdb");
            }
            catch (TypeInitializationException e) when (e.GetBaseException() is PlatformNotSupportedException)
            {
                Assert.Inconclusive("reading a project needs System.Drawing printing, which is only available on Windows");
                return null;
            }
        }

        private static Project ReadProject(Stream stream)
        {
            try
            {
                return Project.ReadFromJson(stream);
            }
            catch (TypeInitializationException e) when (e.GetBaseException() is PlatformNotSupportedException)
            {
                Assert.Inconclusive("reading a project needs System.Drawing printing, which is only available on Windows");
                return null;
            }
        }

        /// <summary>
        /// Records which color is current when a polyline is painted.
        /// </summary>
        private class RecordingPaintTo3D : IPaintTo3D
        {
            private Color current = Color.Empty;
            public List<(Color color, GeoPoint[] points)> Polylines { get; } = new List<(Color, GeoPoint[])>();

            public void SetColor(Color color, int lockColor = 0) { current = color; }
            public void Polyline(GeoPoint[] points) { Polylines.Add((current, points)); }

            #region unused IPaintTo3D members
            public bool PaintSurfaces => true;
            public bool PaintEdges => true;
            public bool PaintSurfaceEdges { get; set; }
            public bool UseLineWidth { get; set; }
            public double Precision { get; set; }
            public double PixelToWorld => 1.0;
            public bool SelectMode { get; set; }
            public Color SelectColor { get; set; }
            public bool DelayText { get; set; }
            public bool DelayAll { get; set; }
            public bool TriangulateText { get; set; }
            public bool DontRecalcTriangulation { get; set; }
            public PaintCapabilities Capabilities => PaintCapabilities.Standard;
            public IDisposable FacesBehindEdgesOffset => null;
            public bool IsBitmap => false;

            public void MakeCurrent() { }
            public void AvoidColor(Color color) { }
            public void SetLineWidth(LineWidth lineWidth) { }
            public void SetLinePattern(LinePattern pattern) { }
            public void FilledPolyline(GeoPoint[] points) { }
            public void Points(GeoPoint[] points, float size, PointSymbol pointSymbol) { }
            public void Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples) { }
            public void PrepareText(string fontName, string textString, FontStyle fontStyle) { }
            public void PreparePointSymbol(PointSymbol pointSymbol) { }
            public void PrepareIcon(Bitmap icon) { }
            public void PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset) { }
            public void PrepareBitmap(Bitmap bitmap) { }
            public void RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight) { }
            public void Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, CADability.GeoObject.Text.AlignMode alignment, CADability.GeoObject.Text.LineAlignMode lineAlignment) { }
            public void List(IPaintTo3DList paintThisList) { }
            public void SelectedList(IPaintTo3DList paintThisList, int wobbleRadius) { }
            public void Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree) { }
            public void Line2D(int sx, int sy, int ex, int ey) { }
            public void Line2D(PointF p1, PointF p2) { }
            public void FillRect2D(PointF p1, PointF p2) { }
            public void Point2D(int x, int y) { }
            public void DisplayIcon(GeoPoint p, Bitmap icon) { }
            public void DisplayBitmap(GeoPoint p, Bitmap bitmap) { }
            public void SetProjection(Projection projection, BoundingCube boundingCube) { }
            public void Clear(Color background) { }
            public void Resize(int width, int height) { }
            public void OpenList(string name = null) { }
            public IPaintTo3DList CloseList() => null;
            public IPaintTo3DList MakeList(List<IPaintTo3DList> sublists) => null;
            public void OpenPath() { }
            public void ClosePath(Color color) { }
            public void CloseFigure() { }
            public void Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter) { }
            public void FreeUnusedLists() { }
            public void UseZBuffer(bool use) { }
            public void Blending(bool on) { }
            public void FinishPaint() { }
            public void PaintFaces(PaintTo3D.PaintMode paintMode) { }
            public void Dispose() { }
            public void PushState() { }
            public void PopState() { }
            public void PushMultModOp(ModOp insertion) { }
            public void PopModOp() { }
            public void SetClip(Rectangle clipRectangle) { }
            #endregion
        }
    }
}
