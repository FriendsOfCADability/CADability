using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// Variable fonts (issue #412, step 6): the named instances become faces of their own, and their outlines and advance widths
    /// are varied with "gvar" (TrueType), "CFF2" and "HVAR". The test fonts are subsets of Source Sans 3 VF made by
    /// make_variable_test_fonts.py, which also wrote the reference data with fontTools.
    /// </summary>
    [TestClass]
    public class VariableFontTests
    {
        private static string FontFile(string name, [CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "Fonts", name);

        private static JsonElement Reference(string file)
        {
            foreach (JsonElement e in JsonDocument.Parse(File.ReadAllText(FontFile("VarTestSans.reference.json"))).RootElement.EnumerateArray())
                if (e.GetProperty("file").GetString() == file) return e;
            throw new AssertFailedException("no reference for " + file);
        }
        private static double[] Numbers(JsonElement array) => array.EnumerateArray().Select(v => v.GetDouble()).ToArray();

        private sealed class Recorder : IGlyphOutlineSink
        {
            public readonly List<(string op, double[] args)> Commands = new List<(string, double[])>();
            public void MoveTo(double x, double y) => Commands.Add(("moveTo", new[] { x, y }));
            public void LineTo(double x, double y) => Commands.Add(("lineTo", new[] { x, y }));
            public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3) => Commands.Add(("curveTo", new[] { x1, y1, x2, y2, x3, y3 }));
            public void ClosePath() => Commands.Add(("closePath", new double[0]));
        }

        [TestMethod]
        public void AxesAndNamedInstances()
        {
            foreach (string file in new[] { "VarTestSans.ttf", "VarTestSans.otf" })
            {
                TrueTypeFont font = TrueTypeFont.Load(FontFile(file)).Single();
                Assert.IsTrue(font.IsVariable, file);
                VariationAxis axis = font.Axes.Single();
                Assert.AreEqual("wght", axis.Tag);
                Assert.AreEqual(200, axis.Min);
                Assert.AreEqual(200, axis.Default);
                Assert.AreEqual(900, axis.Max);
                CollectionAssert.AreEqual(Reference(file).GetProperty("instances").EnumerateArray().Select(n => n.GetString()).ToArray(),
                    font.NamedInstances.Select(i => i.SubfamilyName).ToArray(), file);
                Assert.IsNull(font.WithVariation(new[] { 200.0 }).NormalizedCoordinates, "the default instance is not varied");
            }
            Assert.IsFalse(TrueTypeFont.Load(FontFile("LiberationSans-Regular.ttf")).Single().IsVariable);
        }

        [TestMethod]
        public void AdvanceWidthsMatchFontTools()
        {
            foreach (string file in new[] { "VarTestSans.ttf", "VarTestSans-NoHVAR.ttf", "VarTestSans.otf" })
            {
                JsonElement reference = Reference(file);
                // with HVAR the widths are exact; without it fontTools rounds the widths from the phantom points to whole units
                double tolerance = file.Contains("NoHVAR") ? 0.5 : 1e-6;
                TrueTypeFont font = TrueTypeFont.Load(FontFile(file)).Single();
                int[] glyphs = reference.GetProperty("glyphs").EnumerateArray().Select(g => g.GetInt32()).ToArray();
                string chars = reference.GetProperty("chars").GetString();
                foreach (JsonElement instance in reference.GetProperty("advances").EnumerateArray())
                {
                    double[] coordinates = Numbers(instance.GetProperty("coordinates"));
                    TrueTypeFont varied = font.WithVariation(coordinates);
                    double[] widths = Numbers(instance.GetProperty("widths"));
                    for (int i = 0; i < glyphs.Length; ++i)
                    {
                        Assert.AreEqual(glyphs[i], varied.GetGlyphIndex(chars[i]));
                        Assert.AreEqual(widths[i], varied.GetAdvanceWidth(glyphs[i]), tolerance, $"{file} wght {coordinates[0]}: advance of '{chars[i]}'");
                    }
                }
            }
        }

        [TestMethod]
        public void TheStrikeoutLineVariesWithMvar()
        {
            // "MVAR" of the test fonts varies the strikeout position ("stro"); the reference has the values of fontTools'
            // instancer. Bahnschrift varies the strikeout size ("strs") this way.
            foreach (string file in new[] { "VarTestSans.ttf", "VarTestSans.otf" })
            {
                TrueTypeFont font = TrueTypeFont.Load(FontFile(file)).Single();
                foreach (JsonElement instance in Reference(file).GetProperty("advances").EnumerateArray())
                {
                    double[] coordinates = Numbers(instance.GetProperty("coordinates"));
                    double[] strikeout = Numbers(instance.GetProperty("strikeout"));
                    TrueTypeFont varied = font.WithVariation(coordinates);
                    Assert.AreEqual(strikeout[0], varied.StrikeoutPosition, $"{file} wght {coordinates[0]}");
                    Assert.AreEqual(strikeout[1], varied.StrikeoutSize, $"{file} wght {coordinates[0]}");
                }
            }
        }

        [TestMethod]
        public void InstancesAreNamedInTheAxisOrderingOfStat()
        {
            // Windows names the instances from "STAT": the names of the axis values in the axis ordering, without the elidable
            // ones. Segoe UI Variable has the instance "Light Display" in "fvar", which Windows calls "Display Light". The test
            // font has the axes wght (ordering 0) and ital (ordering 1, not in "fvar", its only value "Regular" is elidable).
            byte[] data = File.ReadAllBytes(FontFile("VarTestSans.ttf"));
            Assert.AreEqual("Light", TrueTypeFont.Load(data).Single().InstanceName(TrueTypeFont.Load(data).Single().NamedInstances[1]));
            int stat = TableOffset(data, "STAT");
            int axisSize = U16(data, stat + 4), axes = stat + U32(data, stat + 8);
            int values = stat + U32(data, stat + 14), valueCount = U16(data, stat + 12);
            for (int v = 0; v < valueCount; ++v)
            {   // make the value of ital not elidable
                int p = values + U16(data, values + 2 * v);
                if (U16(data, p) == 3) data[p + 5] = 0;
            }
            TrueTypeFont font = TrueTypeFont.Load((byte[])data.Clone()).Single();
            Assert.AreEqual("Light Regular", font.InstanceName(font.NamedInstances[1]));
            // swap the ordering of the axes
            data[axes + 7] = 1;
            data[axes + axisSize + 7] = 0;
            font = TrueTypeFont.Load(data).Single();
            Assert.AreEqual("Regular Light", font.InstanceName(font.NamedInstances[1]));
            Assert.AreEqual("Regular", font.InstanceName(font.NamedInstances[2]), "the elidable Regular of wght is left out");
        }
        private static int U16(byte[] data, int p) => data[p] << 8 | data[p + 1];
        private static int U32(byte[] data, int p) => data[p] << 24 | data[p + 1] << 16 | data[p + 2] << 8 | data[p + 3];
        private static int TableOffset(byte[] data, string tag)
        {
            for (int i = 0; i < U16(data, 4); ++i)
                if (System.Text.Encoding.ASCII.GetString(data, 12 + 16 * i, 4) == tag) return U32(data, 12 + 16 * i + 8);
            throw new AssertFailedException("no table " + tag);
        }

        [TestMethod]
        public void Cff2OutlinesMatchFontTools()
        {
            JsonElement reference = Reference("VarTestSans.otf");
            TrueTypeFont font = TrueTypeFont.Load(FontFile("VarTestSans.otf")).Single();
            Assert.IsTrue(font.IsCff2);
            foreach (JsonElement location in reference.GetProperty("locations").EnumerateArray())
            {
                double[] coordinates = Numbers(location.GetProperty("coordinates"));
                TrueTypeFont varied = font.WithVariation(coordinates);
                foreach (JsonElement glyph in location.GetProperty("glyphs").EnumerateArray())
                {
                    string c = glyph.GetProperty("char").GetString();
                    Recorder recorder = new Recorder();
                    varied.DrawCff(varied.GetGlyphIndex(c[0]), recorder);
                    JsonElement commands = glyph.GetProperty("commands");
                    Assert.AreEqual(commands.GetArrayLength(), recorder.Commands.Count, $"wght {coordinates[0]}: commands of '{c}'");
                    int i = 0;
                    foreach (JsonElement command in commands.EnumerateArray())
                    {
                        (string op, double[] args) = recorder.Commands[i++];
                        Assert.AreEqual(command[0].GetString(), op);
                        double[] expected = Numbers(command[1]);
                        for (int k = 0; k < args.Length; ++k) Assert.AreEqual(expected[k], args[k], 1e-5, $"wght {coordinates[0]}: command {i} of '{c}'");
                    }
                }
            }
        }

        [TestMethod]
        public void TrueTypeOutlinesMatchFontTools()
        {
            // the path data and fontTools may start a contour at different points, so the outlines are compared as point sets:
            // every point sampled on one outline must lie on the other (the path data are floats, hence the tolerance)
            foreach (string file in new[] { "VarTestSans.ttf", "VarTestSans-NoHVAR.ttf" })
            {
                JsonElement reference = Reference(file);
                TrueTypeFont font = TrueTypeFont.Load(FontFile(file)).Single();
                foreach (JsonElement location in reference.GetProperty("locations").EnumerateArray())
                {
                    double[] coordinates = Numbers(location.GetProperty("coordinates"));
                    TrueTypeFont varied = font.WithVariation(coordinates);
                    foreach (JsonElement glyph in location.GetProperty("glyphs").EnumerateArray())
                    {
                        string c = glyph.GetProperty("char").GetString();
                        List<(string op, double[] args)> expected = glyph.GetProperty("commands").EnumerateArray()
                            .Select(command => (command[0].GetString(), Numbers(command[1]))).ToList();
                        varied.GetPathData(varied.GetGlyphIndex(c[0]), out PointF[] points, out byte[] types);
                        List<(string op, double[] args)> actual = Commands(points, types, varied.UnitsPerEm, varied.CellAscent);
                        List<(double x, double y)> a = Sample(actual), b = Sample(expected);
                        double distance = Math.Max(Directed(a, b), Directed(b, a));
                        Assert.AreEqual(0.0, distance, 1e-3, $"{file} wght {coordinates[0]}: outline of '{c}'");
                    }
                }
            }
        }

        [TestMethod]
        public void VariedOutlinesDifferFromTheDefault()
        {
            // a guard against comparing the default instance with itself: Black is much bolder than ExtraLight
            TrueTypeFont font = TrueTypeFont.Load(FontFile("VarTestSans.ttf")).Single();
            int glyph = font.GetGlyphIndex('O');
            double light = font.WithVariation(new[] { 200.0 }).GetAdvanceWidth(glyph), black = font.WithVariation(new[] { 900.0 }).GetAdvanceWidth(glyph);
            Assert.IsTrue(black > light + 20, $"{black} > {light}");
        }

        [TestMethod]
        public void NamedInstancesAreFacesNamedLikeGdi()
        {
            ManagedFontProvider.InstanceFamily("Bahnschrift", "Light Condensed", out string family, out bool bold, out bool italic);
            Assert.AreEqual(("Bahnschrift Light Condensed", false, false), (family, bold, italic));
            ManagedFontProvider.InstanceFamily("Bahnschrift", "Bold Condensed", out family, out bold, out italic);
            Assert.AreEqual(("Bahnschrift Condensed", true, false), (family, bold, italic));
            ManagedFontProvider.InstanceFamily("Segoe UI Variable", "Text Semibold Italic", out family, out bold, out italic);
            Assert.AreEqual(("Segoe UI Variable Text Semibold", false, true), (family, bold, italic));
            ManagedFontProvider.InstanceFamily("X", "Regular", out family, out bold, out italic);
            Assert.AreEqual(("X", false, false), (family, bold, italic));

            string directory = Path.Combine(Path.GetTempPath(), "CADability-vf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.Copy(FontFile("VarTestSans.ttf"), Path.Combine(directory, "VarTestSans.ttf"));
                File.Copy(FontFile("VarTestSans.otf"), Path.Combine(directory, "VarTestSans.otf"));
                ManagedFontProvider provider = new ManagedFontProvider(new[] { directory }, false);
                string[] families = provider.GetFamilyNames();
                foreach (string expected in new[] { "VarTestSans ExtraLight", "VarTestSans Light", "VarTestSans", "VarTestSans Medium", "VarTestSans Semibold", "VarTestSans Black",
                    "VarTestSansCFF2 Light", "VarTestSansCFF2" })
                    CollectionAssert.Contains(families, expected);

                TrueTypeFont font = TrueTypeFont.Load(FontFile("VarTestSans.ttf")).Single();
                foreach ((string name, bool isBold, double wght) in new[] { ("VarTestSans", false, 400.0), ("VarTestSans", true, 700.0), ("VarTestSans Light", false, 300.0), ("VarTestSans Black", false, 900.0) })
                {
                    FontFace face = provider.GetFace(name, isBold, false);
                    Assert.AreEqual(name, face.FamilyName);
                    Assert.AreEqual(isBold, face.IsBold);
                    TrueTypeFont instance = font.WithVariation(new[] { wght });
                    Assert.AreEqual(instance.GetAdvanceWidth(instance.GetGlyphIndex('W')) / instance.UnitsPerEm, face.GetAdvanceWidth('W'), 1e-12, $"{name} bold={isBold}");
                }
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [TestMethod]
        public void UntouchedPointsAreInterpolatedAndClampedLikeTheSpecification()
        {
            // The subsets have sparse tuples only in rectilinear glyphs, where no untouched point lies outside its reference
            // points, so the clamping of IUP is checked here with a made-up glyph of three contours: in the first, the
            // points 1 and 3 have deltas; in the second, only point 6; in the third, no point at all.
            double[] x = { -50, 100, 150, 300, 400, 0, 10, 20, 0, 10 };
            double[] y = { 0, 0, 0, 0, 0, 0, 10, 20, 0, 10 };
            bool[] touched = { false, true, false, true, false, false, true, false, false, false };
            double[] dx = { 0, 10, 0, 30, 0, 0, 4, 0, 0, 0 };
            double[] dy = { 0, 5, 0, 5, 0, 0, -2, 0, 0, 0 };
            FontVariations.InterpolateUntouched(x, y, new[] { 4, 7, 9 }, touched, dx, dy);
            // point 2 lies between its references, the points 0 and 4 outside: they get the delta of the nearer one
            CollectionAssert.AreEqual(new double[] { 10, 10, 15, 30, 30, 4, 4, 4, 0, 0 }, dx);
            // all y coordinates of the references are equal and so are their deltas
            CollectionAssert.AreEqual(new double[] { 5, 5, 5, 5, 5, -2, -2, -2, 0, 0 }, dy);
        }

        [TestMethod]
        public void HeadersOfAVariableFontNameTheInstances()
        {
            TrueTypeFont header = TrueTypeFont.ReadHeaders(FontFile("VarTestSans.ttf")).Single();
            Assert.IsTrue(header.IsHeaderOnly);
            Assert.IsTrue(header.IsVariable);
            Assert.AreEqual(7, header.NamedInstances.Length);
            Assert.AreEqual("Black", header.NamedInstances.Last().SubfamilyName);
        }

        private static List<(string op, double[] args)> Commands(PointF[] points, byte[] types, int unitsPerEm, int cellAscent)
        {   // the path data back in font units, as drawing commands
            List<(string, double[])> res = new List<(string, double[])>();
            (double, double) F(PointF p) => (p.X * unitsPerEm, cellAscent - p.Y * unitsPerEm);
            for (int i = 0; i < points.Length; ++i)
            {
                int t = types[i] & 0x7F;
                var (x, y) = F(points[i]);
                if (t == 0) res.Add(("moveTo", new[] { x, y }));
                else if (t == 1) res.Add(("lineTo", new[] { x, y }));
                else
                {
                    var (x2, y2) = F(points[i + 1]);
                    var (x3, y3) = F(points[i + 2]);
                    res.Add(("curveTo", new[] { x, y, x2, y2, x3, y3 }));
                    i += 2;
                }
                if ((types[i] & 0x80) != 0) res.Add(("closePath", new double[0]));
            }
            return res;
        }
        private static List<(double x, double y)> Sample(List<(string op, double[] args)> commands)
        {
            const int n = 8;
            List<(double, double)> res = new List<(double, double)>();
            double cx = 0, cy = 0, sx = 0, sy = 0;
            foreach (var (op, p) in commands)
            {
                if (op == "moveTo")
                {
                    cx = sx = p[0];
                    cy = sy = p[1];
                    res.Add((cx, cy));
                }
                else if (op == "lineTo" || op == "closePath")
                {
                    double ex = op == "lineTo" ? p[0] : sx, ey = op == "lineTo" ? p[1] : sy;
                    for (int i = 1; i <= n; ++i) res.Add((cx + (ex - cx) * i / n, cy + (ey - cy) * i / n));
                    cx = ex;
                    cy = ey;
                }
                else
                {
                    for (int i = 1; i <= n; ++i)
                    {
                        double t = (double)i / n, u = 1 - t;
                        res.Add((u * u * u * cx + 3 * u * u * t * p[0] + 3 * u * t * t * p[2] + t * t * t * p[4],
                                 u * u * u * cy + 3 * u * u * t * p[1] + 3 * u * t * t * p[3] + t * t * t * p[5]));
                    }
                    cx = p[4];
                    cy = p[5];
                }
            }
            return res;
        }
        private static double Directed(List<(double x, double y)> a, List<(double x, double y)> b)
        {
            double max = 0;
            foreach (var p in a)
            {
                double best = double.MaxValue;
                foreach (var q in b) best = Math.Min(best, (p.x - q.x) * (p.x - q.x) + (p.y - q.y) * (p.y - q.y));
                max = Math.Max(max, Math.Sqrt(best));
            }
            return max;
        }
    }
}
