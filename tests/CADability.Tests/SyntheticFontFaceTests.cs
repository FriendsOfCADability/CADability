using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;

namespace CADability.Tests
{
    /// <summary>
    /// A bold or italic style that a family does not have is synthesized like GDI+ did it for the texts of CADability before
    /// the font service (issue #412). The expected outlines are what GDI+ returned on Windows for the glyph 'I' of fonts
    /// without a bold face (AddString with FontStyle.Bold, in font units, y downwards).
    /// </summary>
    [TestClass]
    public class SyntheticFontFaceTests
    {
        private static PointF[] Em(double em, params double[] xy)
        {
            PointF[] res = new PointF[xy.Length / 2];
            for (int i = 0; i < res.Length; ++i) res[i] = new PointF((float)(xy[2 * i] / em), (float)(xy[2 * i + 1] / em));
            return res;
        }
        private static byte[] Polygon(int n)
        {
            byte[] res = new byte[n];
            for (int i = 1; i < n; ++i) res[i] = 1;
            res[n - 1] |= 0x80;
            return res;
        }
        private static void AssertSame(PointF[] expected, PointF[] actual, double em, double tolerance, string what)
        {
            Assert.AreEqual(expected.Length, actual.Length, what);
            for (int i = 0; i < expected.Length; ++i)
            {
                Assert.AreEqual(expected[i].X * em, actual[i].X * em, tolerance, $"{what}: x of point {i}");
                Assert.AreEqual(expected[i].Y * em, actual[i].Y * em, tolerance, $"{what}: y of point {i}");
            }
        }

        [TestMethod]
        public void BoldMovesTheEdgesFacingRightAndUpLikeGdiPlus()
        {
            // Lucida Console 'I' (em 2048): the right edges move 41 to the right, the upper edges 40 up, the others stay
            PointF[] regular = Em(2048, 173, 1616, 173, 1468, 518, 1468, 518, 481, 173, 481, 173, 333, 1060, 333, 1060, 481, 715, 481, 715, 1468, 1060, 1468, 1060, 1616);
            PointF[] gdiPlus = Em(2048, 173, 1616, 173, 1428, 518, 1428, 518, 481, 173, 481, 173, 293, 1101, 293, 1101, 481, 756, 481, 756, 1428, 1101, 1428, 1101, 1616);
            PointF[] bold = SyntheticFontFace.Embolden(regular, Polygon(regular.Length), 41.0 / 2048, 40.0 / 2048);
            AssertSame(gdiPlus, bold, 2048, 0.01, "Lucida Console 'I'");

            // Microsoft Sans Serif 'I', a rectangle with the other orientation
            regular = Em(2048, 383, 1888, 188, 1888, 188, 422, 383, 422);
            gdiPlus = Em(2048, 424, 1888, 188, 1888, 188, 382, 424, 382);
            bold = SyntheticFontFace.Embolden(regular, Polygon(regular.Length), 41.0 / 2048, 40.0 / 2048);
            AssertSame(gdiPlus, bold, 2048, 0.01, "Microsoft Sans Serif 'I'");
        }

        [TestMethod]
        public void BoldMakesTheHoleOfAGlyphSmaller()
        {
            // a square of 100 with a hole of 50 in the middle, the hole running the other way (y downwards)
            PointF[] points = Em(1000, 0, 0, 100, 0, 100, 100, 0, 100, 25, 25, 25, 75, 75, 75, 75, 25);
            byte[] types = { 0, 1, 1, 0x81, 0, 1, 1, 0x81 };
            PointF[] bold = SyntheticFontFace.Embolden(points, types, 10.0 / 1000, 10.0 / 1000);
            // outside: the right side and the top grow by 10; the hole: its left side and its bottom move by 10, so it gets
            // smaller by 10 in both directions
            double Width(int from) => (bold.Skip(from).Take(4).Max(p => p.X) - bold.Skip(from).Take(4).Min(p => p.X)) * 1000;
            double Height(int from) => (bold.Skip(from).Take(4).Max(p => p.Y) - bold.Skip(from).Take(4).Min(p => p.Y)) * 1000;
            Assert.AreEqual(110, Width(0), 1e-3);
            Assert.AreEqual(110, Height(0), 1e-3);
            Assert.AreEqual(40, Width(4), 1e-3, "the hole gets narrower");
            Assert.AreEqual(40, Height(4), 1e-3, "the hole gets lower");
        }

        /// <summary>
        /// A face that only answers its metrics, for <see cref="SyntheticFontFace.BoldStrengthOf"/>.
        /// </summary>
        private sealed class MetricsFace : FontFace
        {
            private readonly int em; private readonly bool bold;
            public MetricsFace(int em, bool bold) { this.em = em; this.bold = bold; }
            public override string FamilyName => "Metrics";
            public override bool IsBold => bold;
            public override bool IsItalic => false;
            public override int EmHeight => em;
            public override int CellAscent => em;
            public override int CellDescent => 0;
            public override int LineSpacing => em;
            public override double GetAdvanceWidth(int codePoint) => 0.5;
            public override double GetKerning(int left, int right) => 0.0;
            public override void GetGlyphPath(int codePoint, bool underline, bool strikeout, out PointF[] points, out byte[] types) { points = new PointF[0]; types = new byte[0]; }
        }

        [TestMethod]
        public void TheBoldStrengthDependsOnTheEmLikeInGdiPlus()
        {
            // measured with GDI+ on Windows, in font units: right, up
            foreach ((int em, int x, int y) in new[] { (2048, 41, 40), (4096, 82, 81), (1024, 21, 20), (1000, 20, 19), (256, 6, 5) })
                Assert.AreEqual(((double)x / em, (double)y / em), SyntheticFontFace.BoldStrengthOf(new MetricsFace(em, false)), "em " + em);
            // GDI+ does not make a bold, semibold or heavier face any wider
            Assert.AreEqual((0.0, 0.0), SyntheticFontFace.BoldStrengthOf(new MetricsFace(2048, true)));
        }

        [TestMethod]
        public void AMissingStyleIsSynthesizedFromTheClosestFace()
        {
            // the test fonts have Liberation Sans in the regular face only
            ManagedFontProvider provider = new ManagedFontProvider(new[] { TestFonts.Directory }, false);
            FontFace regular = provider.GetFace("Liberation Sans", false, false);
            FontFace italic = provider.GetFace("Liberation Sans", false, true);
            FontFace bold = provider.GetFace("Liberation Sans", true, false);
            FontFace boldItalic = provider.GetFace("Liberation Sans", true, true);
            Assert.IsFalse(regular.IsBold || regular.IsItalic);
            Assert.IsTrue(italic.IsItalic && !italic.IsBold);
            Assert.IsTrue(bold.IsBold && !bold.IsItalic);
            Assert.IsTrue(boldItalic.IsBold && boldItalic.IsItalic);
            Assert.AreSame(italic, provider.GetFace("Liberation Sans", false, true), "a synthetic face is made once");
            Assert.AreEqual("Liberation Sans", italic.FamilyName);
            Assert.AreEqual(regular.GetAdvanceWidth('H'), bold.GetAdvanceWidth('H'), "the advance widths stay the same");
            Assert.AreEqual(regular.CellAscent, italic.CellAscent);

            // italic: a shear by 0x5700/0x10000 around the baseline, the outline is otherwise the same
            regular.GetGlyphPath('H', false, false, out PointF[] r, out byte[] rt);
            italic.GetGlyphPath('H', false, false, out PointF[] i, out byte[] it);
            CollectionAssert.AreEqual(rt, it);
            double baseline = regular.CellAscent / (double)regular.EmHeight;
            for (int k = 0; k < r.Length; ++k)
            {
                Assert.AreEqual(r[k].Y, i[k].Y, 1e-6);
                Assert.AreEqual(r[k].X + 0x5700 / 65536.0 * (baseline - r[k].Y), i[k].X, 1e-5);
            }

            // bold: wider to the right and higher, the left side and the baseline stay
            bold.GetGlyphPath('I', false, false, out PointF[] b, out _);
            regular.GetGlyphPath('I', false, false, out PointF[] ri, out _);
            // GDI+: ceiling(em / 50) font units to the right, one less up (Liberation Sans has 2048 units per em: 41 and 40)
            Assert.AreEqual((41.0 / 2048, 40.0 / 2048), SyntheticFontFace.BoldStrengthOf(regular));
            Assert.AreEqual(ri.Min(p => p.X), b.Min(p => p.X), 1e-5, "the left side");
            Assert.AreEqual(ri.Max(p => p.Y), b.Max(p => p.Y), 1e-5, "the baseline");
            Assert.AreEqual(ri.Max(p => p.X) + 41.0 / 2048, b.Max(p => p.X), 1e-5, "the right side");
            Assert.AreEqual(ri.Min(p => p.Y) - 40.0 / 2048, b.Min(p => p.Y), 1e-5, "the top");
        }
    }
}
