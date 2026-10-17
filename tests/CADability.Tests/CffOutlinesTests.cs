using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// The outlines of OpenType fonts with CFF outlines (issue #412, step 6). InterCffTest.otf (name-keyed) and InterCidTest.otf
    /// (CID-keyed, two font dicts) are made by make_cff_test_fonts.py from a subset of Inter so that they use subroutines,
    /// the specialized curve and line operators, flex and hint masks; InterCffTest.reference.json holds the outlines as fontTools
    /// draws them, the same for both fonts.
    /// </summary>
    [TestClass]
    public class CffOutlinesTests
    {
        private static string FontFile(string name, [CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "Fonts", name);

        private sealed class Recorder : IGlyphOutlineSink
        {
            public readonly List<(string op, double[] args)> Commands = new List<(string, double[])>();
            public void MoveTo(double x, double y) => Commands.Add(("moveTo", new[] { x, y }));
            public void LineTo(double x, double y) => Commands.Add(("lineTo", new[] { x, y }));
            public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3) => Commands.Add(("curveTo", new[] { x1, y1, x2, y2, x3, y3 }));
            public void ClosePath() => Commands.Add(("closePath", new double[0]));
        }

        private static void AssertOutlinesMatchFontTools(string fontFile)
        {
            TrueTypeFont font = TrueTypeFont.Load(FontFile(fontFile)).Single();
            Assert.IsTrue(font.HasCffOutlines);
            Assert.IsFalse(font.HasTrueTypeOutlines);
            Assert.IsTrue(font.HasOutlines);
            JsonElement glyphs = JsonDocument.Parse(File.ReadAllText(FontFile("InterCffTest.reference.json"))).RootElement.GetProperty("glyphs");
            int compared = 0;
            foreach (JsonElement expected in glyphs.EnumerateArray())
            {
                string c = expected.GetProperty("char").GetString();
                int glyph = font.GetGlyphIndex(c[0]);
                Assert.AreEqual(expected.GetProperty("glyph").GetInt32(), glyph, $"glyph index of '{c}'");
                Recorder recorder = new Recorder();
                font.DrawCff(glyph, recorder);
                JsonElement commands = expected.GetProperty("commands");
                Assert.AreEqual(commands.GetArrayLength(), recorder.Commands.Count, $"number of drawing commands of '{c}'");
                int i = 0;
                foreach (JsonElement command in commands.EnumerateArray())
                {
                    (string op, double[] args) = recorder.Commands[i++];
                    Assert.AreEqual(command[0].GetString(), op, $"command {i} of '{c}'");
                    double[] expectedArgs = command[1].EnumerateArray().Select(v => v.GetDouble()).ToArray();
                    Assert.AreEqual(expectedArgs.Length, args.Length, $"arguments of command {i} of '{c}'");
                    for (int k = 0; k < args.Length; ++k) Assert.AreEqual(expectedArgs[k], args[k], 1e-9, $"argument {k} of command {i} of '{c}'");
                }
                ++compared;
            }
            Assert.AreEqual(104, compared);
        }

        [TestMethod]
        public void NameKeyedOutlinesMatchFontTools()
        {
            AssertOutlinesMatchFontTools("InterCffTest.otf");
        }

        [TestMethod]
        public void CidKeyedOutlinesMatchFontTools()
        {
            AssertOutlinesMatchFontTools("InterCidTest.otf");
        }

        [TestMethod]
        public void PathDataOfACffGlyph()
        {
            TrueTypeFont font = TrueTypeFont.Load(FontFile("InterCffTest.otf")).Single();
            Recorder recorder = new Recorder();
            int glyph = font.GetGlyphIndex('B');
            font.DrawCff(glyph, recorder);
            font.GetPathData(glyph, out PointF[] points, out byte[] types);
            Assert.AreEqual(recorder.Commands.Count(c => c.op == "closePath"), types.Count(t => (t & 0x80) != 0), "one closed figure per contour");
            Assert.IsTrue(types.All(t => new[] { 0, 1, 3 }.Contains(t & 0x7F)));
            Assert.AreEqual(recorder.Commands.Count(c => c.op == "curveTo") * 3, types.Count(t => (t & 0x7F) == 3), "the cubic curves go in unchanged");
            Assert.AreEqual(font.CellAscent / (double)font.UnitsPerEm, points.Max(p => p.Y), 1e-6, "the B stands on the baseline");
        }

        [TestMethod]
        public void HeadersOfAnOpenTypeFontAreRead()
        {
            TrueTypeFont header = TrueTypeFont.ReadHeaders(FontFile("InterCidTest.otf")).Single();
            Assert.IsTrue(header.IsHeaderOnly);
            Assert.IsTrue(header.HasCffOutlines);
            Assert.AreEqual("Inter", header.FamilyName);
        }

        [TestMethod]
        [DoNotParallelize]
        public void TextWithAnOpenTypeFont()
        {
            string directory = Path.Combine(Path.GetTempPath(), "CADability-otf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.Copy(FontFile("InterCffTest.otf"), Path.Combine(directory, "InterCffTest.otf"));
                FontProvider.Current = new ManagedFontProvider(new[] { directory }, false);
                FontFace face = FontCache.GlobalFontCache.GetFace("Inter", 0);
                Assert.AreEqual("Inter", face.FamilyName);
                Text text = Text.Construct();
                text.Font = "Inter";
                text.TextString = "CFF";
                text.Location = GeoPoint.Origin;
                text.LineDirection = GeoVector.XAxis;
                text.GlyphDirection = GeoVector.YAxis;
                text.TextSize = 1.0;
                Assert.IsTrue(text.GetOutline().Count > 0);
            }
            finally
            {
                TestFonts.UseTestFonts(null);
                Directory.Delete(directory, true);
            }
        }
    }
}
