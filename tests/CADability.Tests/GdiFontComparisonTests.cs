#if WINDOWS
using CADability.Forms;
using System.Drawing;
using System.Text;

namespace CADability.Tests
{
    /// <summary>
    /// Compares the managed font reader (<see cref="ManagedFontProvider"/>, the default since issue #412) with GDI and GDI+
    /// (<see cref="GdiFontProvider"/>, how CADability got its fonts before) on the fonts installed on Windows: the families,
    /// the metrics, the advance widths and the glyph outlines must be the same. Texts in existing drawings must look as
    /// they did before.
    /// </summary>
    [TestClass]
    public class GdiFontComparisonTests
    {
        public TestContext TestContext { get; set; }

        // GDI gives the advance widths as whole pixels at the em height as pixel size, which is one font unit. The outlines
        // are floats, and GDI rounds the varied coordinates of a variable font to whole font units.
        private const double WidthTolerance = 1.0; // in font units
        private const double OutlineTolerance = 2.0; // in font units

        private static readonly string Characters = new string(Enumerable.Range(0x20, 0x7F - 0x20).Select(c => (char)c).ToArray()) + "ÄÖÜäöüßéÅ€";

        private static readonly ManagedFontProvider managed = new ManagedFontProvider();
        private static readonly GdiFontProvider gdi = new GdiFontProvider();

        [TestMethod]
        public void TheManagedProviderKnowsAllFamiliesOfGdi()
        {
            // GDI cuts the names to 31 characters, the managed provider also finds the family by such a name
            string[] missing = gdi.GetFamilyNames().Where(name => managed.GetFace(name, false, false) == null).OrderBy(name => name).ToArray();
            Assert.AreEqual(0, missing.Length, "families of GDI the managed provider does not know: " + string.Join(", ", missing));
        }

        [TestMethod]
        [DataRow("Arial")]
        [DataRow("Times New Roman")]
        [DataRow("Courier New")]
        [DataRow("Segoe UI")]
        [DataRow("Calibri")]
        [DataRow("Cambria")] // in a font collection (cambria.ttc)
        [DataRow("Consolas")]
        [DataRow("Georgia")]
        [DataRow("Bahnschrift")] // variable: the default instance
        [DataRow("Bahnschrift Light Condensed")] // variable: a named instance
        [DataRow("Segoe UI Variable Display")] // variable, Windows 11 only
        public void FacesMatchGdi(string family)
        {
            if (gdi.GetFace(family, false, false) == null) Assert.Inconclusive(family + " is not installed");
            StringBuilder report = new StringBuilder();
            int compared = 0;
            foreach (bool bold in new[] { false, true })
            {
                foreach (bool italic in new[] { false, true })
                {
                    FontFace g = gdi.GetFace(family, bold, italic), m = managed.GetFace(family, bold, italic);
                    Assert.IsNotNull(m, family + " is not known to the managed provider");
                    // only real faces: GDI synthesizes a missing bold or italic face, the managed provider takes the closest
                    if (g.IsBold != bold || g.IsItalic != italic || m.IsBold != bold || m.IsItalic != italic) continue;
                    Compare(g, (ManagedFontFace)m, Characters, true, report);
                    ++compared;
                }
            }
            Assert.IsTrue(compared > 0, "no face of " + family + " was compared");
            Assert.AreEqual(0, report.Length, report.ToString());
        }

        /// <summary>
        /// All fonts GDI knows, with fewer outlines: the metrics, the advance widths of all characters of the test and
        /// the outlines of a few characters with curves, corners, holes and composite glyphs.
        /// </summary>
        [TestMethod]
        public void AllInstalledFontsMatchGdi()
        {
            StringBuilder report = new StringBuilder();
            int compared = 0;
            foreach (string family in gdi.GetFamilyNames().OrderBy(name => name))
            {
                FontFace g = gdi.GetFace(family, false, false), m = managed.GetFace(family, false, false);
                if (g == null || m == null) continue; // a missing family is reported by TheManagedProviderKnowsAllFamiliesOfGdi
                if (g.IsBold != m.IsBold || g.IsItalic != m.IsItalic) continue; // a family without a regular face
                Compare(g, (ManagedFontFace)m, "AOgs8&@Äé", false, report);
                ++compared;
            }
            TestContext.WriteLine($"{compared} families compared");
            Assert.AreEqual(0, report.Length, report.ToString());
        }

        private static void Compare(FontFace g, ManagedFontFace m, string outlineCharacters, bool decorations, StringBuilder report)
        {
            string face = $"{g.FamilyName}{(g.IsBold ? " bold" : "")}{(g.IsItalic ? " italic" : "")}";
            void Fail(string message) => report.AppendLine(face + ": " + message);
            if (g.EmHeight != m.EmHeight || g.CellAscent != m.CellAscent || g.CellDescent != m.CellDescent || g.LineSpacing != m.LineSpacing)
            {
                Fail($"metrics (em, ascent, descent, line spacing) GDI {g.EmHeight} {g.CellAscent} {g.CellDescent} {g.LineSpacing}, managed {m.EmHeight} {m.CellAscent} {m.CellDescent} {m.LineSpacing}");
                return; // all the rest would differ as well
            }
            double em = m.EmHeight;
            // characters the font does not have are left out: GDI takes them from another font (font linking)
            foreach (char c in Characters.Where(c => m.Font.GetGlyphIndex(c) != 0))
            {
                double gw = g.GetAdvanceWidth(c) * em, mw = m.GetAdvanceWidth(c) * em;
                if (Math.Abs(gw - mw) > WidthTolerance) Fail($"advance width of '{c}' GDI {gw}, managed {mw}");
            }
            foreach (char c in outlineCharacters.Where(c => m.Font.GetGlyphIndex(c) != 0))
            {
                CompareOutline(g, m, c, false, false, Fail);
            }
            if (decorations)
            {
                CompareOutline(g, m, 'H', true, false, Fail);
                CompareOutline(g, m, 'H', false, true, Fail);
            }
        }

        private static void CompareOutline(FontFace g, FontFace m, char c, bool underline, bool strikeout, Action<string> fail)
        {
            string what = $"outline of '{c}'{(underline ? " underlined" : "")}{(strikeout ? " struck out" : "")}";
            g.GetGlyphPath(c, underline, strikeout, out PointF[] gp, out byte[] gt);
            m.GetGlyphPath(c, underline, strikeout, out PointF[] mp, out byte[] mt);
            List<List<(double x, double y)>> a = Flatten(gp, gt, m.EmHeight), b = Flatten(mp, mt, m.EmHeight);
            if (a.Count != b.Count)
            {
                fail($"{what}: GDI {a.Count} contours, managed {b.Count}");
                return;
            }
            // the contours may start at different points, so the outlines are compared as point sets (Hausdorff distance)
            double distance = Math.Max(Directed(a, b), Directed(b, a));
            if (distance > OutlineTolerance) fail($"{what}: distance {distance:0.###} font units");
        }

        /// <summary>
        /// The contours of GDI+ path data as polylines in font units, with the Bezier segments finely subdivided.
        /// </summary>
        private static List<List<(double x, double y)>> Flatten(PointF[] points, byte[] types, double em)
        {
            const int n = 16;
            List<List<(double x, double y)>> res = new List<List<(double, double)>>();
            List<(double x, double y)> contour = null;
            for (int i = 0; i < points.Length; ++i)
            {
                (double x, double y) p = (points[i].X * em, points[i].Y * em);
                switch (types[i] & 0x07)
                {
                    case 0:
                        contour = new List<(double, double)> { p };
                        res.Add(contour);
                        break;
                    case 1:
                        contour.Add(p);
                        break;
                    case 3:
                        (double x, double y) p0 = contour[contour.Count - 1], p1 = p, p2 = (points[i + 1].X * em, points[i + 1].Y * em), p3 = (points[i + 2].X * em, points[i + 2].Y * em);
                        for (int k = 1; k <= n; ++k)
                        {
                            double t = (double)k / n, u = 1 - t;
                            contour.Add((u * u * u * p0.x + 3 * u * u * t * p1.x + 3 * u * t * t * p2.x + t * t * t * p3.x,
                                         u * u * u * p0.y + 3 * u * u * t * p1.y + 3 * u * t * t * p2.y + t * t * t * p3.y));
                        }
                        i += 2;
                        break;
                }
                if ((types[i] & 0x80) != 0) contour.Add(contour[0]);
            }
            return res;
        }

        /// <summary>
        /// The largest distance of a vertex of <paramref name="a"/> from the polylines of <paramref name="b"/>.
        /// </summary>
        private static double Directed(List<List<(double x, double y)>> a, List<List<(double x, double y)>> b)
        {
            double max = 0.0;
            foreach (var p in a.SelectMany(contour => contour))
            {
                double best = double.MaxValue;
                foreach (var contour in b)
                {
                    for (int i = 1; i < contour.Count; ++i) best = Math.Min(best, SegmentDistance(p, contour[i - 1], contour[i]));
                    if (contour.Count == 1) best = Math.Min(best, SegmentDistance(p, contour[0], contour[0]));
                }
                max = Math.Max(max, best);
            }
            return max;
        }
        private static double SegmentDistance((double x, double y) p, (double x, double y) s, (double x, double y) e)
        {
            double dx = e.x - s.x, dy = e.y - s.y, l = dx * dx + dy * dy;
            double t = l == 0.0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((p.x - s.x) * dx + (p.y - s.y) * dy) / l));
            double x = s.x + t * dx - p.x, y = s.y + t * dy - p.y;
            return Math.Sqrt(x * x + y * y);
        }
    }
}
#endif
