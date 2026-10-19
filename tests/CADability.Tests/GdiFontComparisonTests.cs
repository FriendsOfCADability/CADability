#if WINDOWS
using CADability.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace CADability.Tests
{
    /// <summary>
    /// Compares the managed font reader (<see cref="ManagedFontProvider"/>, the default since issue #412) with GDI and GDI+
    /// (<see cref="GdiFontProvider"/>, how CADability got its fonts before) on the fonts installed on Windows: the families,
    /// the metrics, the advance widths, the underline and strikeout lines and the glyph outlines must be the same, so that
    /// texts in existing drawings keep their layout. The outlines are compared with the unhinted outlines of GDI.
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
        [DataRow("Segoe UI Variable Text Light")] // named from "STAT": "Light Text" in "fvar"
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
                    // only real faces: a missing bold or italic face is synthesized by both, see SyntheticStylesMatchGdiPlus
                    if (g.IsBold != bold || g.IsItalic != italic || !(m is ManagedFontFace real) || real.IsBold != bold || real.IsItalic != italic) continue;
                    Compare(g, real, Characters, true, report);
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
            FontStyle style = (g.IsBold ? FontStyle.Bold : FontStyle.Regular) | (g.IsItalic ? FontStyle.Italic : FontStyle.Regular);
            using (GdiGlyphOutlines outlines = new GdiGlyphOutlines(g.FamilyName, style, m.EmHeight))
            {
                foreach (char c in outlineCharacters.Where(c => m.Font.GetGlyphIndex(c) != 0))
                {
                    m.GetGlyphPath(c, false, false, out PointF[] mp, out byte[] mt);
                    CompareOutlines($"outline of '{c}'", outlines.Get(c, m.CellAscent), Flatten(mp, mt, em), Fail);
                }
            }
            if (decorations)
            {
                CompareBar(g, m, true, Fail);
                CompareBar(g, m, false, Fail);
            }
        }

        /// <summary>
        /// The underline or the strikeout line, which is the last figure of the path data of GDI+ and of the managed reader.
        /// </summary>
        private static void CompareBar(FontFace g, FontFace m, bool underline, Action<string> fail)
        {
            g.GetGlyphPath('H', underline, !underline, out PointF[] gp, out byte[] gt);
            m.GetGlyphPath('H', underline, !underline, out PointF[] mp, out byte[] mt);
            List<List<(double x, double y)>> a = Flatten(gp, gt, m.EmHeight), b = Flatten(mp, mt, m.EmHeight);
            if (a.Count == 0 || b.Count == 0) fail((underline ? "underline" : "strikeout") + " is missing");
            else CompareOutlines(underline ? "underline" : "strikeout", a.GetRange(a.Count - 1, 1), b.GetRange(b.Count - 1, 1), fail);
        }

        private static void CompareOutlines(string what, List<List<(double x, double y)>> gdi, List<List<(double x, double y)>> managed, Action<string> fail)
        {
            if (gdi.Count != managed.Count)
            {
                fail($"{what}: GDI {gdi.Count} contours, managed {managed.Count}");
                return;
            }
            // the contours may start at different points, so the outlines are compared as point sets (Hausdorff distance)
            double distance = Math.Max(Directed(gdi, managed), Directed(managed, gdi));
            if (distance > OutlineTolerance) fail($"{what}: distance {distance:0.###} font units");
        }

        /// <summary>
        /// The unhinted glyph outlines of GDI (GetGlyphOutline with GGO_NATIVE | GGO_UNHINTED) at the em height as pixel size,
        /// so in font units. GDI+ (<see cref="GdiFontProvider"/>) is not the reference for the outlines: AddString gives the
        /// hinted outlines, which differ from the design by a few font units in strongly hinted fonts (e.g. 17 units for '2'
        /// of Calibri) and are wrong for Segoe Script.
        /// </summary>
        private sealed class GdiGlyphOutlines : IDisposable
        {
            [StructLayout(LayoutKind.Sequential)] private struct GLYPHMETRICS { public uint bx, by; public int ox, oy; public short cx, cy; }
            [StructLayout(LayoutKind.Sequential)] private struct MAT2 { public int m11, m12, m21, m22; }
            [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
            [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
            [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
            [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string faceName);
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern uint GetGlyphOutlineW(IntPtr hdc, uint c, uint format, out GLYPHMETRICS metrics, uint size, byte[] buffer, ref MAT2 matrix);
            private const uint GGO_NATIVE = 2, GGO_UNHINTED = 0x100;

            private readonly IntPtr hdc, hfont, oldFont;
            public GdiGlyphOutlines(string family, FontStyle style, int em)
            {
                // the weight of a regular face is FW_DONTCARE: with FW_NORMAL GDI would embolden a lighter face, e.g. the
                // instance "Cascadia Code ExtraLight"
                int weight = (style & FontStyle.Bold) != 0 ? 700 : 0;
                hfont = CreateFontW(-em, 0, 0, 0, weight, (style & FontStyle.Italic) != 0 ? 1u : 0u, 0, 0, 1 /* DEFAULT_CHARSET */, 0, 0, 0, 0, family);
                hdc = CreateCompatibleDC(IntPtr.Zero);
                oldFont = SelectObject(hdc, hfont);
            }
            public void Dispose()
            {
                SelectObject(hdc, oldFont);
                DeleteObject(hfont);
                DeleteDC(hdc);
            }
            private static double Fixed(byte[] b, int offset) => BitConverter.ToInt16(b, offset + 2) + BitConverter.ToUInt16(b, offset) / 65536.0;
            /// <summary>
            /// The contours of a character as polylines in the layout of <see cref="Flatten"/>: font units, y downwards, the
            /// top of the cell at 0.
            /// </summary>
            public List<List<(double x, double y)>> Get(char c, int cellAscent)
            {
                const int n = 16;
                MAT2 identity = new MAT2 { m11 = 0x10000, m22 = 0x10000 };
                List<List<(double x, double y)>> res = new List<List<(double, double)>>();
                uint size = GetGlyphOutlineW(hdc, c, GGO_NATIVE | GGO_UNHINTED, out GLYPHMETRICS metrics, 0, null, ref identity);
                if (size == 0 || size == uint.MaxValue) return res;
                byte[] b = new byte[size];
                GetGlyphOutlineW(hdc, c, GGO_NATIVE | GGO_UNHINTED, out metrics, size, b, ref identity);
                (double x, double y) Point(int offset) => (Fixed(b, offset), cellAscent - Fixed(b, offset + 4));
                int pos = 0;
                while (pos < size)
                {   // TTPOLYGONHEADER: cb, dwType, pfxStart, then TTPOLYCURVEs: wType, cpfx, apfx[cpfx]
                    int cb = BitConverter.ToInt32(b, pos);
                    List<(double x, double y)> contour = new List<(double, double)> { Point(pos + 8) };
                    for (int p = pos + 16; p < pos + cb;)
                    {
                        int type = BitConverter.ToUInt16(b, p), count = BitConverter.ToUInt16(b, p + 2);
                        if (type == 1) for (int k = 0; k < count; ++k) contour.Add(Point(p + 4 + 8 * k)); // lines
                        else if (type == 2)
                        {   // quadratic B-spline: the on-curve points between the control points are implied
                            for (int k = 0; k < count - 1; ++k)
                            {
                                (double x, double y) p0 = contour[contour.Count - 1], c1 = Point(p + 4 + 8 * k), next = Point(p + 12 + 8 * k);
                                (double x, double y) e = k == count - 2 ? next : ((c1.x + next.x) / 2, (c1.y + next.y) / 2);
                                for (int s = 1; s <= n; ++s)
                                {
                                    double t = (double)s / n, u = 1 - t;
                                    contour.Add((u * u * p0.x + 2 * u * t * c1.x + t * t * e.x, u * u * p0.y + 2 * u * t * c1.y + t * t * e.y));
                                }
                            }
                        }
                        else
                        {   // cubic Bezier segments (fonts with CFF outlines)
                            for (int k = 0; k + 2 < count; k += 3)
                            {
                                (double x, double y) p0 = contour[contour.Count - 1], p1 = Point(p + 4 + 8 * k), p2 = Point(p + 12 + 8 * k), p3 = Point(p + 20 + 8 * k);
                                for (int s = 1; s <= n; ++s)
                                {
                                    double t = (double)s / n, u = 1 - t;
                                    contour.Add((u * u * u * p0.x + 3 * u * u * t * p1.x + 3 * u * t * t * p2.x + t * t * t * p3.x,
                                                 u * u * u * p0.y + 3 * u * u * t * p1.y + 3 * u * t * t * p2.y + t * t * t * p3.y));
                                }
                            }
                        }
                        p += 4 + 8 * count;
                    }
                    contour.Add(contour[0]);
                    res.Add(contour);
                    pos += cb;
                }
                return res;
            }
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
