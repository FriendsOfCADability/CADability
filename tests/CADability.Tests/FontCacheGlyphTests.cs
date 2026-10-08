using CADability.Curve2D;
using CADability.GeoObject;
using System.Drawing;
using System.Runtime.InteropServices;

namespace CADability.Tests
{
    /// <summary>
    /// Sends all printable ASCII characters of installed fonts through <see cref="FontCache.Get(string, int, char, out double, IPaintTo3D)"/>
    /// and checks that every glyph with a closed outline is painted with triangles. Needs Windows (GDI) and the font.
    /// </summary>
    [TestClass]
    public class FontCacheGlyphTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// A paint interface that only records what has been painted into the current display list.
        /// </summary>
        private class RecordingPaintTo3D : IPaintTo3D
        {
            private class RecordedList : IPaintTo3DList
            {
                public string Name { get; set; }
                public List<IPaintTo3DList> containedSubLists { set { } }
                public void Dispose() { }
                public int Triangles;
                public int Curves;
            }
            private RecordedList current;
            public int TrianglesInLastList;
            public int CurvesInLastList;

            public bool PaintSurfaces => true;
            public bool PaintEdges => true;
            public bool PaintSurfaceEdges { get; set; } = true;
            public bool UseLineWidth { get; set; }
            public double Precision { get; set; } = 0.05;
            public double PixelToWorld => 1.0;
            public bool SelectMode { get; set; }
            public Color SelectColor { get; set; }
            public bool DelayText { get; set; }
            public bool DelayAll { get; set; }
            public bool TriangulateText { get; set; } = true;
            public bool DontRecalcTriangulation { get; set; }
            public PaintCapabilities Capabilities => PaintCapabilities.Standard;
            public IDisposable FacesBehindEdgesOffset => null;
            public bool IsBitmap => false;

            public void MakeCurrent() { }
            public void SetColor(Color color, int lockColor = 0) { }
            public void AvoidColor(Color color) { }
            public void SetLineWidth(Attribute.LineWidth lineWidth) { }
            public void SetLinePattern(Attribute.LinePattern pattern) { }
            public void Polyline(GeoPoint[] points) { if (current != null) ++current.Curves; }
            public void FilledPolyline(GeoPoint[] points) { if (current != null) ++current.Curves; }
            public void Points(GeoPoint[] points, float size, PointSymbol pointSymbol) { }
            public void Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples) { if (current != null) current.Triangles += indextriples.Length / 3; }
            public void PrepareText(string fontName, string textString, FontStyle fontStyle) { }
            public void PreparePointSymbol(PointSymbol pointSymbol) { }
            public void PrepareIcon(Bitmap icon) { }
            public void PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset) { }
            public void PrepareBitmap(Bitmap bitmap) { }
            public void RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight) { }
            public void Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, GeoObject.Text.AlignMode alignment, GeoObject.Text.LineAlignMode lineAlignment) { }
            public void List(IPaintTo3DList paintThisList) { }
            public void SelectedList(IPaintTo3DList paintThisList, int wobbleRadius) { }
            public void Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree) { if (current != null) ++current.Curves; }
            public void Line2D(int sx, int sy, int ex, int ey) { }
            public void Line2D(PointF p1, PointF p2) { }
            public void FillRect2D(PointF p1, PointF p2) { }
            public void Point2D(int x, int y) { }
            public void DisplayIcon(GeoPoint p, Bitmap icon) { }
            public void DisplayBitmap(GeoPoint p, Bitmap bitmap) { }
            public void SetProjection(Projection projection, BoundingCube boundingCube) { }
            public void Clear(Color background) { }
            public void Resize(int width, int height) { }
            public void OpenList(string name = null) { current = new RecordedList() { Name = name }; }
            public IPaintTo3DList CloseList()
            {
                RecordedList res = current;
                current = null;
                TrianglesInLastList = res.Triangles;
                CurvesInLastList = res.Curves;
                if (res.Triangles == 0 && res.Curves == 0) return null; // like the OpenGL implementation: an empty list is null
                return res;
            }
            public IPaintTo3DList MakeList(List<IPaintTo3DList> sublists) { return new RecordedList(); }
            public void OpenPath() { }
            public void ClosePath(Color color) { }
            public void CloseFigure() { }
            public void Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter) { if (current != null) ++current.Curves; }
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
        }

        private static string CheckFontAvailable(string fontName)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "glyph outlines need GDI, which is only available on Windows";
            if (!GeoObject.Text.FontFamilyNames.Contains(fontName.ToUpper()))
            {
                // name the installed families that start with the same word, which shows a misspelled or a wrong instance name
                string firstWord = fontName.Split(' ')[0].ToUpper();
                string[] similar = GeoObject.Text.FontFamilyNames.Where(n => n.StartsWith(firstWord)).OrderBy(n => n).ToArray();
                return "font '" + fontName + "' is not installed" + (similar.Length > 0 ? ", installed are: " + string.Join(", ", similar) : "");
            }
            return null;
        }

        /// <summary>
        /// Checks all characters from 0x20 to 0x7E of the font. Returns the characters whose outline is closed but which have not
        /// been painted with triangles (the outline fallback was used) and the characters that produced no display list although
        /// they have an outline.
        /// </summary>
        private static (List<char> withoutTriangles, List<char> withoutList) CheckFont(string fontName, int fontStyle, TestContext testContext)
        {
            FontCache fontCache = new FontCache();
            RecordingPaintTo3D paintTo3D = new RecordingPaintTo3D();
            List<char> withoutTriangles = new List<char>();
            List<char> withoutList = new List<char>();
            for (char c = (char)0x20; c <= (char)0x7E; ++c)
            {
                Path2D[] outline = fontCache.GetOutline2D(fontName, fontStyle, c, out double w);
                bool hasClosedOutline = outline.Any(p => p.IsClosed);
                IPaintTo3DList list = fontCache.Get(fontName, fontStyle, c, out double width, paintTo3D);
                if (!hasClosedOutline) continue;
                if (list == null) withoutList.Add(c);
                else if (paintTo3D.TrianglesInLastList == 0) withoutTriangles.Add(c);
                // the result must be cached now, even when it failed: the second call must not paint again
                paintTo3D.TrianglesInLastList = -1;
                fontCache.Get(fontName, fontStyle, c, out width, paintTo3D);
                Assert.AreEqual(-1, paintTo3D.TrianglesInLastList, "glyph '" + c + "' of " + fontName + " was not cached");
            }
            testContext?.WriteLine("{0}: {1} glyphs without triangles [{2}], {3} without display list [{4}]", fontName, withoutTriangles.Count, new string(withoutTriangles.ToArray()), withoutList.Count, new string(withoutList.ToArray()));
            return (withoutTriangles, withoutList);
        }

        [DataTestMethod]
        [DataRow("Arial")]
        [DataRow("Calibri")]
        [DataRow("Bahnschrift")]
        [DataRow("Bahnschrift Light")]
        // GDI knows the variable font of Windows 11 only by the names of its instances, there is no family "Segoe UI Variable"
        [DataRow("Segoe UI Variable Text")]
        [DataRow("Segoe UI Variable Display")]
        [DataRow("Segoe UI")]
        public void AllGlyphsHaveTriangles(string fontName)
        {
            string notAvailable = CheckFontAvailable(fontName);
            if (notAvailable != null) Assert.Inconclusive(notAvailable);
            var (withoutTriangles, withoutList) = CheckFont(fontName, (int)FontStyle.Regular, TestContext);
            Assert.AreEqual(0, withoutList.Count, "glyphs without display list in " + fontName + ": " + new string(withoutList.ToArray()));
            Assert.AreEqual(0, withoutTriangles.Count, "glyphs without triangles in " + fontName + ": " + new string(withoutTriangles.ToArray()));
        }

        /// <summary>
        /// Survey over all installed fonts: writes the glyphs without triangles to the test output, does not fail.
        /// Takes a few minutes, therefore it only runs when it is started under the debugger (Debug Test in Visual Studio)
        /// or with the environment variable CADABILITY_FONT_SURVEY set, e.g.
        /// <c>dotnet test --filter Name=ReportGlyphsWithoutTrianglesForAllInstalledFonts -e CADABILITY_FONT_SURVEY=1</c>.
        /// An [Ignore] attribute cannot be overridden in the test explorer, so the test could not be run at all.
        /// </summary>
        [TestMethod]
        public void ReportGlyphsWithoutTrianglesForAllInstalledFonts()
        {
            if (!System.Diagnostics.Debugger.IsAttached && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CADABILITY_FONT_SURVEY")))
                Assert.Inconclusive("survey over all installed fonts, takes a few minutes: start it with Debug Test or set CADABILITY_FONT_SURVEY=1");
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) Assert.Inconclusive("glyph outlines need GDI, which is only available on Windows");
            int fontsWithProblems = 0;
            foreach (FontFamily ff in FontFamily.Families)
            {
                try
                {
                    var (withoutTriangles, withoutList) = CheckFont(ff.Name, (int)FontStyle.Regular, TestContext);
                    if (withoutTriangles.Count + withoutList.Count > 0) ++fontsWithProblems;
                }
                catch (Exception ex)
                {
                    TestContext.WriteLine("{0}: exception {1}", ff.Name, ex.Message);
                    ++fontsWithProblems;
                }
            }
            TestContext.WriteLine("{0} fonts with glyphs without triangles", fontsWithProblems);
        }
    }
}
