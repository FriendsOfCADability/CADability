using CADability.Curve2D;
using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// Texts get their fonts from <see cref="FontProvider.Current"/>, a static service of the kernel, and no longer from GDI or
    /// the frame (issue #412, step 6): they work without a frame and on every platform. The tests that replace the provider
    /// do not run in parallel with other tests and restore the provider of <see cref="TestFonts"/>.
    /// </summary>
    [TestClass]
    public class FontProviderTests
    {
        private static string FontDirectory([CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "Fonts");

        private static Text MakeText(string font, string textString, double size)
        {
            Text text = Text.Construct();
            text.Font = font;
            text.TextString = textString;
            text.Location = GeoPoint.Origin;
            text.LineDirection = GeoVector.XAxis;
            text.GlyphDirection = GeoVector.YAxis;
            text.TextSize = size;
            return text;
        }
        private static void WithProvider(FontProvider provider, Action action)
        {
            try
            {
                FontProvider.Current = provider;
                action();
            }
            finally
            {
                TestFonts.UseTestFonts(null);
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void TextWorksWithoutFrameAndWithoutGdi()
        {
            Assert.IsNull(FrameImpl.MainFrame, "the tests run without a frame");
            WithProvider(new ManagedFontProvider(new[] { FontDirectory() }, false), () =>
            {
                Text text = MakeText("Liberation Sans", "Hello", 10.0);
                GeoObjectList outline = text.GetOutline();
                Assert.IsTrue(outline.Count > 0, "the text has an outline");
                BoundingCube ext = text.GetBoundingCube();
                Assert.IsFalse(ext.IsEmpty);

                // the width of the text is the sum of the advance widths and the kerning of the font
                TrueTypeFont font = TrueTypeFont.Load(Path.Combine(FontDirectory(), "LiberationSans-Regular.ttf"))[0];
                double advance = 0.0;
                string s = "Hello";
                for (int i = 0; i < s.Length; ++i)
                {
                    advance += font.GetAdvanceWidth(font.GetGlyphIndex(s[i]));
                    if (i > 0) advance += font.GetKerning(font.GetGlyphIndex(s[i - 1]), font.GetGlyphIndex(s[i]));
                }
                double width = 10.0 * advance / font.UnitsPerEm;
                Assert.AreEqual(width, text.GetBoundingCube().Xmax - text.GetBoundingCube().Xmin, 0.15 * 10.0, "the extent is about the advance width (the glyphs do not fill their whole advance)");
            });
        }

        [TestMethod]
        [DoNotParallelize]
        public void UnknownFontUsesTheDefaultFace()
        {
            WithProvider(new ManagedFontProvider(new[] { FontDirectory() }, false), () =>
            {
                FontFace face = FontCache.GlobalFontCache.GetFace("A Font That Does Not Exist", 0);
                Assert.AreEqual("Liberation Sans", face.FamilyName);
                Text known = MakeText("Liberation Sans", "Ag", 1.0), unknown = MakeText("A Font That Does Not Exist", "Ag", 1.0);
                Assert.AreEqual(known.GetOutline().GetExtent().Size, unknown.GetOutline().GetExtent().Size, 1e-9);
            });
        }

        [TestMethod]
        [DoNotParallelize]
        public void WithoutAnyFontTheExceptionSaysWhatToDo()
        {
            string empty = Path.Combine(Path.GetTempPath(), "CADability-no-fonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(empty);
            try
            {
                WithProvider(new ManagedFontProvider(new[] { empty }, false), () =>
                {
                    InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() => FontCache.GlobalFontCache.GetFace("Arial", 0));
                    StringAssert.Contains(ex.Message, "FontProvider.Current");
                    // the extent and the glyphs for painting are empty instead of failing, an explicit request for the outline throws
                    FontCache.GlobalFontCache.Get("Arial", 0, 'A', out double width, null);
                    Text text = MakeText("Arial", "no font", 1.0);
                    text.GetBoundingCube();
                    Assert.ThrowsException<InvalidOperationException>(() => text.GetOutline());
                });
            }
            finally
            {
                Directory.Delete(empty, true);
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void UnderlineAndStrikeoutAddBars()
        {
            WithProvider(new ManagedFontProvider(new[] { FontDirectory() }, false), () =>
            {
                Path2D[] plain = FontCache.GlobalFontCache.GetOutline2D("Liberation Sans", 0, 'o', out double w1);
                Path2D[] underlined = FontCache.GlobalFontCache.GetOutline2D("Liberation Sans", 4, 'o', out double w2);
                Path2D[] both = FontCache.GlobalFontCache.GetOutline2D("Liberation Sans", 4 | 8, 'o', out double w3);
                Assert.AreEqual(plain.Length + 1, underlined.Length);
                Assert.AreEqual(plain.Length + 2, both.Length);
                Assert.AreEqual(w1, w2);
                // the underline is below the baseline (y = 1 - CellAscent/em after PathDataToPath2D)
                FontFace face = FontCache.GlobalFontCache.GetFace("Liberation Sans", 0);
                double baseline = 1.0 - face.CellAscent / (double)face.EmHeight;
                Assert.IsTrue(underlined.Last().GetExtent().Top < baseline);
            });
        }

        [TestMethod]
        [DoNotParallelize]
        public void MissingStyleFallsBackToAnExistingFace()
        {
            ManagedFontProvider provider = new ManagedFontProvider(new[] { FontDirectory() }, false);
            CollectionAssert.AreEqual(new[] { "Liberation Sans" }, provider.GetFamilyNames());
            FontFace bold = provider.GetFace("liberation sans", true, false);
            Assert.IsNotNull(bold, "the family name is not case sensitive");
            Assert.IsFalse(bold.IsBold, "only the regular face is there");
            Assert.IsNull(provider.GetFace("Arial", false, false));
            Assert.AreSame(bold, provider.GetFace("Liberation Sans", false, false), "faces are loaded once");
        }

        [TestMethod]
        public void HeadersAreReadWithoutLoadingTheGlyphs()
        {
            string file = Path.Combine(FontDirectory(), "LiberationSans-Regular.ttf");
            TrueTypeFont header = TrueTypeFont.ReadHeaders(file).Single(), full = TrueTypeFont.Load(file).Single();
            Assert.IsTrue(header.IsHeaderOnly);
            Assert.IsFalse(full.IsHeaderOnly);
            Assert.AreEqual(full.FamilyName, header.FamilyName);
            Assert.AreEqual(full.SubfamilyName, header.SubfamilyName);
            Assert.AreEqual(full.IsBold, header.IsBold);
            Assert.AreEqual(full.IsItalic, header.IsItalic);
            Assert.AreEqual(full.UnitsPerEm, header.UnitsPerEm);
            Assert.AreEqual(full.CellAscent, header.CellAscent);
            Assert.AreEqual(full.UnderlinePosition, header.UnderlinePosition);
            Assert.IsTrue(header.HasTrueTypeOutlines);
            Assert.ThrowsException<InvalidOperationException>(() => header.GetContours(5));
        }

        [TestMethod]
        public void FontCacheHasNoFinalizerThatCanCrashTheProcess()
        {
            // the FontCache used to create a GDI device context in its constructor and delete it in its finalizer; without GDI
            // the finalizer threw and ended the process (issue #412)
            for (int i = 0; i < 10; ++i) new FontCache();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
