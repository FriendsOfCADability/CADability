using System;
using CADability.Attribute;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #428: <see cref="DimensionStyle.EFontFlag"/> had the consecutive values 0, 1, 2, 3 but was combined like a
    /// flags enum, so bold could never be stored and strike out equalled italic plus underline.
    /// </summary>
    [TestClass]
    public class DimensionStyleFontFlagsTests
    {
        [TestMethod]
        public void MembersAreSingleBits()
        {
            Assert.IsTrue(typeof(DimensionStyle.EFontFlag).IsDefined(typeof(FlagsAttribute), false));
            foreach (DimensionStyle.EFontFlag flag in Enum.GetValues(typeof(DimensionStyle.EFontFlag)))
            {
                int value = (int)flag;
                Assert.IsTrue(value > 0 && (value & (value - 1)) == 0, flag + " is a single bit");
            }
        }

        [TestMethod]
        public void BoldCanBeStored()
        {
            DimensionStyle style = new DimensionStyle();
            style.DimFontBold = true;
            Assert.IsTrue(style.DimFontBold);
            Assert.IsFalse(style.DimFontItalic);
            Assert.IsFalse(style.DimFontUnderline);
            Assert.IsFalse(style.DimFontStrikeOut);
            style.DimFontBold = false;
            Assert.IsFalse(style.DimFontBold);
        }

        [TestMethod]
        public void StrikeOutIsIndependentOfItalicAndUnderline()
        {
            DimensionStyle style = new DimensionStyle();
            style.DimFontItalic = true;
            style.DimFontUnderline = true;
            Assert.IsFalse(style.DimFontStrikeOut, "italic plus underline is not strike out");
            style.DimFontStrikeOut = true;
            style.DimFontItalic = false;
            Assert.IsTrue(style.DimFontStrikeOut);
            Assert.IsTrue(style.DimFontUnderline);
            Assert.IsFalse(style.DimFontItalic);
            Assert.AreEqual(DimensionStyle.EFontFlag.DimFontUnderline | DimensionStyle.EFontFlag.DimFontStrikeOut, style.FontFlags);
        }
    }
}
