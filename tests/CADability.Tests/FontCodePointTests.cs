using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="FontFace"/> takes Unicode code points, so that characters outside the Basic Multilingual Plane, e.g. most
    /// emoji, which are a surrogate pair of two chars in a string, reach the font as one character (issue #412).
    /// </summary>
    [TestClass]
    public class FontCodePointTests
    {
        private const int Emoji = 0x1F600; // GRINNING FACE, "😀" in a string

        /// <summary>
        /// A face that records the characters it is asked for. Every character is a square from 0.1 to 0.4 with an advance
        /// width of 0.5 (in units of the em height).
        /// </summary>
        private sealed class RecordingFace : FontFace
        {
            public readonly List<int> Glyphs = new List<int>(), Widths = new List<int>();
            public readonly List<(int, int)> KerningPairs = new List<(int, int)>();
            public override string FamilyName => "Recording";
            public override bool IsBold => false;
            public override bool IsItalic => false;
            public override int EmHeight => 1000;
            public override int CellAscent => 800;
            public override int CellDescent => 200;
            public override int LineSpacing => 1000;
            public override double GetAdvanceWidth(int codePoint)
            {
                lock (Widths) Widths.Add(codePoint);
                return 0.5;
            }
            public override double GetKerning(int left, int right)
            {
                lock (KerningPairs) KerningPairs.Add((left, right));
                return 0.0;
            }
            public override void GetGlyphPath(int codePoint, bool underline, bool strikeout, out PointF[] points, out byte[] types)
            {
                lock (Glyphs) Glyphs.Add(codePoint);
                points = new[] { new PointF(0.1f, 0.2f), new PointF(0.4f, 0.2f), new PointF(0.4f, 0.6f), new PointF(0.1f, 0.6f) };
                types = new byte[] { 0, 1, 1, 0x81 };
            }
        }
        private sealed class RecordingProvider : FontProvider
        {
            public readonly RecordingFace Face = new RecordingFace();
            public override string[] GetFamilyNames() => new[] { "Recording" };
            public override FontFace GetFace(string familyName, bool bold, bool italic) => string.Equals(familyName, "Recording", StringComparison.OrdinalIgnoreCase) ? Face : null;
        }

        private static bool IsSurrogate(int c) => c >= 0xD800 && c <= 0xDFFF;

        [TestMethod]
        public void ASurrogatePairIsOneCodePoint()
        {
            CollectionAssert.AreEqual(new[] { 'a', Emoji, 'b' }, Text.CodePoints("a😀b"));
            // a lone surrogate is no code point, it is kept as the char it is
            CollectionAssert.AreEqual(new[] { 0xD83D, 'x' }, Text.CodePoints("\uD83Dx"));
            CollectionAssert.AreEqual(new[] { 'x', 0xDE00 }, Text.CodePoints("x\uDE00"));
            CollectionAssert.AreEqual(new int[0], Text.CodePoints(""));
        }

        [TestMethod]
        [DoNotParallelize]
        public void AnEmojiReachesTheFontAsOneCharacter()
        {
            RecordingProvider provider = new RecordingProvider();
            try
            {
                FontProvider.Current = provider;
                Text text = Text.Construct();
                text.Font = "Recording";
                text.TextString = "a😀b";
                text.Location = GeoPoint.Origin;
                text.LineDirection = GeoVector.XAxis;
                text.GlyphDirection = GeoVector.YAxis;
                text.TextSize = 1.0;

                // three characters, not four as with the two halves of the surrogate pair as separate characters: the extent
                // is the sum of the advance widths, the outline goes from the first square at 0.1 to the third at 2 * 0.5 + 0.4
                BoundingCube extent = text.GetBoundingCube();
                Assert.AreEqual(1.5, extent.Xmax - extent.Xmin, 1e-6, "the advance widths of three characters");
                BoundingCube outline = text.GetOutline().GetExtent();
                Assert.AreEqual(0.1, outline.Xmin, 1e-6);
                Assert.AreEqual(1.4, outline.Xmax, 1e-6, "the outlines of three characters");

                RecordingFace face = provider.Face;
                CollectionAssert.AreEquivalent(new[] { 'a', Emoji, 'b' }, face.Glyphs.Distinct().ToArray(), "the outlines asked for");
                Assert.IsTrue(face.Widths.Contains(Emoji), "the advance width of the emoji");
                Assert.IsTrue(face.KerningPairs.Contains(('a', Emoji)) && face.KerningPairs.Contains((Emoji, 'b')), "the kerning around the emoji");
                Assert.IsFalse(face.Glyphs.Concat(face.Widths).Any(IsSurrogate), "no half of a surrogate pair reaches the font");
            }
            finally
            {
                TestFonts.UseTestFonts(null);
            }
        }

        [TestMethod]
        public void TheManagedReaderMapsCodePointsAboveTheBmp()
        {
            // the face of the managed reader passes the code point on to the cmap, which supports format 12 (beyond U+FFFF);
            // a font without the character gives the missing glyph, but no exception
            FontFace face = new ManagedFontProvider(new[] { TestFonts.Directory }, false).GetFace("Liberation Sans", false, false);
            Assert.IsNotNull(face);
            Assert.AreEqual(face.GetAdvanceWidth(0xFFFF + 1), face.GetAdvanceWidth(0x10FFFF), 1e-12, "both are the missing glyph");
            face.GetGlyphPath(Emoji, false, false, out PointF[] points, out byte[] types);
            Assert.AreEqual(points.Length, types.Length);
        }
    }
}
