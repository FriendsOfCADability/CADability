using CADability.Curve2D;
using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// The managed TrueType reader, which replaces GDI for text on every platform (issue #412, step 6). The reference data
    /// (LiberationSans-Regular.reference.json) was written by fontTools from the same font file, so every value here is
    /// checked against an independent implementation.
    /// </summary>
    [TestClass]
    public class TrueTypeFontTests
    {
        private static string FontFile(string name, [CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "Fonts", name);

        private static TrueTypeFont LoadLiberationSans()
        {
            TrueTypeFont[] fonts = TrueTypeFont.Load(FontFile("LiberationSans-Regular.ttf"));
            Assert.AreEqual(1, fonts.Length);
            return fonts[0];
        }
        private static JsonElement Reference()
        {
            return JsonDocument.Parse(File.ReadAllText(FontFile("LiberationSans-Regular.reference.json"))).RootElement;
        }

        [TestMethod]
        public void NamesAndMetricsMatchFontTools()
        {
            TrueTypeFont font = LoadLiberationSans();
            JsonElement reference = Reference();
            Assert.AreEqual(reference.GetProperty("family").GetString(), font.FamilyName);
            Assert.AreEqual(reference.GetProperty("subfamily").GetString(), font.SubfamilyName);
            Assert.AreEqual(reference.GetProperty("full").GetString(), font.FullName);
            Assert.AreEqual(reference.GetProperty("unitsPerEm").GetInt32(), font.UnitsPerEm);
            Assert.AreEqual(reference.GetProperty("ascender").GetInt32(), font.Ascender);
            Assert.AreEqual(reference.GetProperty("descender").GetInt32(), font.Descender);
            Assert.AreEqual(reference.GetProperty("lineGap").GetInt32(), font.LineGap);
            Assert.AreEqual(reference.GetProperty("winAscent").GetInt32(), font.WinAscent);
            Assert.AreEqual(reference.GetProperty("winDescent").GetInt32(), font.WinDescent);
            Assert.AreEqual(reference.GetProperty("glyphCount").GetInt32(), font.GlyphCount);
            Assert.IsFalse(font.IsBold);
            Assert.IsFalse(font.IsItalic);
            Assert.IsTrue(font.HasTrueTypeOutlines);
        }

        [TestMethod]
        public void GlyphsMatchFontTools()
        {
            TrueTypeFont font = LoadLiberationSans();
            int composites = 0;
            foreach (JsonElement expected in Reference().GetProperty("glyphs").EnumerateArray())
            {
                string c = expected.GetProperty("char").GetString();
                int glyph = font.GetGlyphIndex(c[0]);
                Assert.AreEqual(expected.GetProperty("glyph").GetInt32(), glyph, $"glyph index of '{c}'");
                Assert.AreEqual(expected.GetProperty("advance").GetInt32(), font.GetAdvanceWidth(glyph), $"advance width of '{c}'");
                if (expected.GetProperty("composite").GetBoolean()) ++composites;
                List<GlyphPoint[]> contours = font.GetContours(glyph);
                JsonElement expectedContours = expected.GetProperty("contours");
                Assert.AreEqual(expectedContours.GetArrayLength(), contours.Count, $"number of contours of '{c}'");
                int ci = 0;
                foreach (JsonElement expectedContour in expectedContours.EnumerateArray())
                {
                    GlyphPoint[] contour = contours[ci++];
                    Assert.AreEqual(expectedContour.GetArrayLength(), contour.Length, $"points of contour {ci} of '{c}'");
                    int pi = 0;
                    foreach (JsonElement p in expectedContour.EnumerateArray())
                    {
                        GlyphPoint actual = contour[pi++];
                        // fontTools rounds the points of transformed components to integers
                        Assert.AreEqual(p[0].GetInt32(), actual.X, 0.51, $"x of point {pi} of '{c}'");
                        Assert.AreEqual(p[1].GetInt32(), actual.Y, 0.51, $"y of point {pi} of '{c}'");
                        Assert.AreEqual(p[2].GetInt32() == 1, actual.OnCurve, $"on curve flag of point {pi} of '{c}'");
                    }
                }
            }
            Assert.IsTrue(composites > 0, "the reference contains composite glyphs (e.g. Ä)");
        }

        [TestMethod]
        public void KerningMatchesFontTools()
        {
            TrueTypeFont font = LoadLiberationSans();
            JsonElement reference = Reference();
            Assert.AreEqual(reference.GetProperty("kerningPairCount").GetInt32(), font.GetKerningPairs().Count());
            foreach (JsonElement pair in reference.GetProperty("kerning").EnumerateArray())
            {
                int left = font.GetGlyphIndex(pair.GetProperty("left").GetString()[0]);
                int right = font.GetGlyphIndex(pair.GetProperty("right").GetString()[0]);
                Assert.AreEqual(pair.GetProperty("value").GetInt32(), font.GetKerning(left, right), pair.GetProperty("left").GetString() + pair.GetProperty("right").GetString());
            }
            Assert.AreEqual(0, font.GetKerning(font.GetGlyphIndex('H'), font.GetGlyphIndex('H')), "no kerning between straight letters");
        }

        [TestMethod]
        public void MissingCharacterAndSpace()
        {
            TrueTypeFont font = LoadLiberationSans();
            Assert.AreEqual(0, font.GetGlyphIndex(0x10FFFF), "a character the font does not have gives the missing glyph");
            int space = font.GetGlyphIndex(' ');
            Assert.AreNotEqual(0, space);
            Assert.AreEqual(0, font.GetContours(space).Count);
            font.GetPathData(space, out PointF[] points, out byte[] types);
            Assert.AreEqual(0, points.Length);
            Assert.AreEqual(0, types.Length);
        }

        [TestMethod]
        public void FontCollectionIsRead()
        {
            // a .ttc file with the same font twice: the table offsets of a collection count from the start of the file
            byte[] ttf = File.ReadAllBytes(FontFile("LiberationSans-Regular.ttf"));
            byte[] ttc = MakeCollection(ttf, 2);
            TrueTypeFont[] fonts = TrueTypeFont.Load(ttc);
            Assert.AreEqual(2, fonts.Length);
            TrueTypeFont single = LoadLiberationSans();
            foreach (TrueTypeFont font in fonts)
            {
                Assert.AreEqual(single.FamilyName, font.FamilyName);
                int glyph = font.GetGlyphIndex('g');
                Assert.AreEqual(single.GetGlyphIndex('g'), glyph);
                List<GlyphPoint[]> expected = single.GetContours(glyph), actual = font.GetContours(glyph);
                Assert.AreEqual(expected.Count, actual.Count);
                for (int i = 0; i < expected.Count; ++i) CollectionAssert.AreEqual(expected[i], actual[i]);
            }
        }

        [TestMethod]
        public void PathDataIsInTheGdiPlusLayout()
        {
            TrueTypeFont font = LoadLiberationSans();
            double em = font.UnitsPerEm;
            // the "H" stands on the baseline, which is at CellAscent / em below the top of the cell
            font.GetPathData(font.GetGlyphIndex('H'), out PointF[] points, out byte[] types);
            Assert.AreEqual(0, types[0], "a figure starts with a start point");
            Assert.AreEqual(1, types.Count(t => (t & 0x80) != 0), "the H is a single closed figure");
            Assert.IsTrue(types.All(t => (t & 0x7F) == 0 || (t & 0x7F) == 1), "the H consists of lines only");
            Assert.AreEqual(font.CellAscent / em, points.Max(p => p.Y), 1e-6, "the bottom of the H is the baseline");
            // the "o" has two closed figures made of Bezier segments
            font.GetPathData(font.GetGlyphIndex('o'), out points, out types);
            Assert.AreEqual(2, types.Count(t => (t & 0x80) != 0));
            Assert.IsTrue(types.Count(t => (t & 0x7F) == 3) % 3 == 0, "Bezier points come in groups of three");
        }

        [TestMethod]
        public void OutlinesAreTheExactQuadraticCurves()
        {
            // the cubic segments of the path data are the degree elevation of the quadratic segments of the font: points of the
            // quadratic curves lie on the outline that GlyphShapeBuilder builds with the highest precision (exact splines)
            TrueTypeFont font = LoadLiberationSans();
            double em = font.UnitsPerEm;
            foreach (char c in "S@&8g")
            {
                int glyph = font.GetGlyphIndex(c);
                font.GetPathData(glyph, out PointF[] points, out byte[] types);
                Path2D[] paths = GlyphShapeBuilder.PathDataToPath2D(points, types, 2);
                Assert.AreEqual(font.GetContours(glyph).Count, paths.Length, $"one path per contour of '{c}'");
                Assert.IsTrue(paths.All(p => p.IsClosed), $"all paths of '{c}' are closed");
                foreach (GeoPoint2D p in PointsOnQuadraticSegments(font.GetContours(glyph)))
                {
                    GeoPoint2D mapped = new GeoPoint2D(p.x / em, 1.0 - (font.CellAscent - p.y) / em); // PathDataToPath2D turns y upwards
                    double distance = paths.Min(path => path.MinDistance(mapped));
                    Assert.AreEqual(0.0, distance, 1e-5, $"point {p} of '{c}'");
                }
            }
        }

        /// <summary>
        /// Liberation Sans with one table changed: <paramref name="patch"/> gets the font data and the offset of the table.
        /// </summary>
        private static TrueTypeFont PatchedLiberationSans(string table, Action<byte[], int> patch)
        {
            byte[] data = File.ReadAllBytes(FontFile("LiberationSans-Regular.ttf"));
            int numTables = data[4] << 8 | data[5];
            for (int i = 0; i < numTables; ++i)
            {
                int record = 12 + 16 * i;
                if (System.Text.Encoding.ASCII.GetString(data, record, 4) != table) continue;
                patch(data, data[record + 8] << 24 | data[record + 9] << 16 | data[record + 10] << 8 | data[record + 11]);
                return TrueTypeFont.Load(data).Single();
            }
            throw new AssertFailedException("no table " + table);
        }

        [TestMethod]
        public void TheOutlineStartsAtTheLeftSideBearingOfHmtx()
        {
            // Times New Roman Italic and Symbol have glyphs whose xMin is not the left side bearing in "hmtx". Windows (and
            // FreeType) move such a glyph so that its left edge is at the left side bearing; the test makes 'H' such a glyph.
            TrueTypeFont font = LoadLiberationSans();
            int glyph = font.GetGlyphIndex('H');
            Assert.IsTrue(glyph < font.GlyphCount);
            TrueTypeFont patched = PatchedLiberationSans("hmtx", (data, hmtx) =>
            {
                int lsb = (short)(data[hmtx + 4 * glyph + 2] << 8 | data[hmtx + 4 * glyph + 3]) + 100;
                data[hmtx + 4 * glyph + 2] = (byte)(lsb >> 8);
                data[hmtx + 4 * glyph + 3] = (byte)lsb;
            });
            List<GlyphPoint[]> original = font.GetContours(glyph), moved = patched.GetContours(glyph);
            Assert.AreEqual(original.Count, moved.Count);
            for (int c = 0; c < original.Count; ++c)
                for (int i = 0; i < original[c].Length; ++i)
                {
                    Assert.AreEqual(original[c][i].X + 100, moved[c][i].X);
                    Assert.AreEqual(original[c][i].Y, moved[c][i].Y);
                }
            Assert.AreEqual(font.GetAdvanceWidth(glyph), patched.GetAdvanceWidth(glyph), "the advance width stays");
        }

        [TestMethod]
        public void UseTypoMetricsSwitchesTheCellToTheTypographicMetrics()
        {
            // Bahnschrift, Cascadia, Sitka and Cambria Math set USE_TYPO_METRICS (bit 7 of fsSelection), and GDI+ then takes
            // the cell and the line spacing from the typographic metrics; the test sets the bit in Liberation Sans
            TrueTypeFont font = LoadLiberationSans();
            Assert.IsFalse(font.UseTypoMetrics);
            Assert.AreEqual(font.WinAscent, font.CellAscent);
            Assert.AreEqual(font.WinDescent, font.CellDescent);
            TrueTypeFont patched = PatchedLiberationSans("OS/2", (data, os2) => data[os2 + 63] |= 0x80);
            Assert.IsTrue(patched.UseTypoMetrics);
            Assert.AreEqual(1491, patched.CellAscent);
            Assert.AreEqual(431, patched.CellDescent);
            Assert.AreEqual(1491 + 431 + 307, patched.LineSpacing);
        }

        [TestMethod]
        public void InvalidDataIsRejected()
        {
            Assert.ThrowsException<InvalidDataException>(() => TrueTypeFont.Load(new byte[0]));
            Assert.ThrowsException<InvalidDataException>(() => TrueTypeFont.Load(System.Text.Encoding.ASCII.GetBytes("this is not a font file at all")));
        }

        private static IEnumerable<GeoPoint2D> PointsOnQuadraticSegments(List<GlyphPoint[]> contours)
        {
            foreach (GlyphPoint[] contour in contours)
            {
                int n = contour.Length;
                for (int i = 0; i < n; ++i)
                {
                    GlyphPoint p = contour[i];
                    if (p.OnCurve) continue;
                    GlyphPoint prev = contour[(i + n - 1) % n], next = contour[(i + 1) % n];
                    // implied on-curve points between two off-curve points
                    GeoPoint2D s = prev.OnCurve ? new GeoPoint2D(prev.X, prev.Y) : new GeoPoint2D((prev.X + p.X) / 2, (prev.Y + p.Y) / 2);
                    GeoPoint2D e = next.OnCurve ? new GeoPoint2D(next.X, next.Y) : new GeoPoint2D((next.X + p.X) / 2, (next.Y + p.Y) / 2);
                    GeoPoint2D ctrl = new GeoPoint2D(p.X, p.Y);
                    foreach (double t in new[] { 0.25, 0.5, 0.75 })
                    {
                        double u = 1 - t;
                        yield return new GeoPoint2D(u * u * s.x + 2 * u * t * ctrl.x + t * t * e.x, u * u * s.y + 2 * u * t * ctrl.y + t * t * e.y);
                    }
                }
            }
        }
        private static byte[] MakeCollection(byte[] ttf, int count)
        {
            int header = 12 + 4 * count;
            byte[] res = new byte[header + count * ttf.Length];
            void Put32(int pos, uint v) { res[pos] = (byte)(v >> 24); res[pos + 1] = (byte)(v >> 16); res[pos + 2] = (byte)(v >> 8); res[pos + 3] = (byte)v; }
            res[0] = (byte)'t'; res[1] = (byte)'t'; res[2] = (byte)'c'; res[3] = (byte)'f';
            Put32(4, 0x00010000);
            Put32(8, (uint)count);
            int numTables = (ttf[4] << 8) | ttf[5];
            for (int f = 0; f < count; ++f)
            {
                int start = header + f * ttf.Length;
                Put32(12 + 4 * f, (uint)start);
                Array.Copy(ttf, 0, res, start, ttf.Length);
                for (int i = 0; i < numTables; ++i)
                {   // move the table offsets of the table directory
                    int rec = start + 12 + 16 * i + 8;
                    uint offset = (uint)((res[rec] << 24) | (res[rec + 1] << 16) | (res[rec + 2] << 8) | res[rec + 3]);
                    Put32(rec, offset + (uint)start);
                }
            }
            return res;
        }
    }
}
