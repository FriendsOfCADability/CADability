using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;

namespace CADability.Forms
{
    /// <summary>
    /// A <see cref="FontProvider"/> that gets the fonts from GDI and GDI+, as CADability did before issue #412: the glyph
    /// outlines from <see cref="GraphicsPath.AddString(string, FontFamily, int, float, PointF, StringFormat)"/>, the widths and
    /// the kerning from gdi32. It knows every font Windows knows (also OpenType fonts with CFF outlines) and synthesizes bold
    /// and italic faces a family does not have. Set <see cref="FontProvider.Current"/> to an instance of this class to use it;
    /// the default is the <see cref="ManagedFontProvider"/>.
    /// </summary>
    public class GdiFontProvider : FontProvider
    {
        private readonly Dictionary<(string family, bool bold, bool italic), GdiFontFace> faces = new Dictionary<(string, bool, bool), GdiFontFace>();
        private HashSet<string> familyNames; // upper case

        /// <inheritdoc/>
        public override string[] GetFamilyNames()
        {
            return FontFamily.Families.Select(f => f.Name).ToArray();
        }
        /// <inheritdoc/>
        public override FontFace GetFace(string familyName, bool bold, bool italic)
        {
            if (string.IsNullOrEmpty(familyName)) return null;
            lock (faces)
            {
                if (familyNames == null) familyNames = new HashSet<string>(GetFamilyNames().Select(n => n.ToUpperInvariant()));
                if (!familyNames.Contains(familyName.ToUpperInvariant())) return null;
                if (!faces.TryGetValue((familyName.ToUpperInvariant(), bold, italic), out GdiFontFace face))
                {
                    face = new GdiFontFace(new FontFamily(familyName), bold, italic);
                    faces[(familyName.ToUpperInvariant(), bold, italic)] = face;
                }
                return face;
            }
        }
        /// <summary>
        /// The generic sans serif family of GDI+, as before issue #412.
        /// </summary>
        public override FontFace GetDefaultFace(bool bold, bool italic)
        {
            using (FontFamily sansSerif = new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif))
            {
                return GetFace(sansSerif.Name, bold, italic);
            }
        }
    }

    /// <summary>
    /// A face of the <see cref="GdiFontProvider"/>.
    /// </summary>
    internal sealed class GdiFontFace : FontFace
    {
        private readonly FontFamily family;
        private readonly FontStyle style; // bold and italic only
        private readonly int em;
        private readonly Dictionary<char, double> widths = new Dictionary<char, double>();
        private Dictionary<(char, char), double> kerning;

        public GdiFontFace(FontFamily family, bool bold, bool italic)
        {
            this.family = family;
            FontStyle requested = (bold ? FontStyle.Bold : FontStyle.Regular) | (italic ? FontStyle.Italic : FontStyle.Regular);
            style = requested;
            if (!family.IsStyleAvailable(style))
            {
                if (family.IsStyleAvailable(FontStyle.Regular)) style = FontStyle.Regular;
                else if (family.IsStyleAvailable(FontStyle.Bold)) style = FontStyle.Bold;
                else if (family.IsStyleAvailable(FontStyle.Italic)) style = FontStyle.Italic;
            }
            em = family.GetEmHeight(style);
            if (em == 0) em = 1000;
        }
        public override string FamilyName => family.Name;
        public override bool IsBold => (style & FontStyle.Bold) != 0;
        public override bool IsItalic => (style & FontStyle.Italic) != 0;
        public override int EmHeight => em;
        public override int CellAscent => family.GetCellAscent(style);
        public override int CellDescent => family.GetCellDescent(style);
        public override int LineSpacing => family.GetLineSpacing(style);

        public override double GetAdvanceWidth(char c)
        {
            lock (widths)
            {
                if (widths.TryGetValue(c, out double width)) return width;
                WithSelectedFont(hdc =>
                {
                    ABC[] abc = new ABC[1];
                    GetCharABCWidths(hdc, c, c, abc);
                    width = (abc[0].abcA + abc[0].abcB + abc[0].abcC) / (double)em;
                });
                widths[c] = width;
                return width;
            }
        }
        public override double GetKerning(char left, char right)
        {
            lock (widths)
            {
                if (kerning == null)
                {
                    Dictionary<(char, char), double> pairs = new Dictionary<(char, char), double>();
                    WithSelectedFont(hdc =>
                    {
                        int num = GetKerningPairs(hdc, 0, null);
                        if (num > 0)
                        {
                            KERNINGPAIR[] kp = new KERNINGPAIR[num];
                            GetKerningPairs(hdc, num, kp);
                            for (int i = 0; i < kp.Length; ++i) pairs[((char)kp[i].wFirst, (char)kp[i].wSecond)] = kp[i].iKernAmount / (double)em;
                        }
                    });
                    kerning = pairs;
                }
                kerning.TryGetValue((left, right), out double res);
                return res;
            }
        }
        public override void GetGlyphPath(char c, bool underline, bool strikeout, out PointF[] points, out byte[] types)
        {
            FontStyle fs = style | (underline ? FontStyle.Underline : FontStyle.Regular) | (strikeout ? FontStyle.Strikeout : FontStyle.Regular);
            using (GraphicsPath path = new GraphicsPath())
            using (StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                sf.LineAlignment = StringAlignment.Near;
                sf.Alignment = StringAlignment.Near;
                path.AddString(c.ToString(), family, (int)fs, 1.0f, new PointF(0.0f, 0.0f), sf);
                if (path.PointCount == 0)
                {
                    points = new PointF[0];
                    types = new byte[0];
                }
                else
                {
                    points = path.PathPoints;
                    types = path.PathTypes;
                }
            }
        }

        /// <summary>
        /// Selects this face with the em height as pixel size into a device context for the duration of <paramref name="action"/>.
        /// The device context only lives during the call, so nothing is left to a finalizer.
        /// </summary>
        private void WithSelectedFont(Action<IntPtr> action)
        {
            IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
            try
            {
                using (Font font = new Font(family, em, style, GraphicsUnit.Pixel))
                {
                    IntPtr hfont = font.ToHfont();
                    IntPtr oldFont = SelectObject(hdc, hfont);
                    try
                    {
                        action(hdc);
                    }
                    finally
                    {
                        SelectObject(hdc, oldFont);
                        DeleteObject(hfont);
                    }
                }
            }
            finally
            {
                DeleteDC(hdc);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ABC
        {
            public int abcA;
            public uint abcB;
            public int abcC;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct KERNINGPAIR
        {
            public short wFirst;
            public short wSecond;
            public int iKernAmount;
        }
        [DllImport("gdi32.dll", EntryPoint = "GetCharABCWidthsW")]
        private static extern bool GetCharABCWidths(IntPtr hdc, uint uFirstChar, uint uLastChar, [Out] ABC[] lpabc);
        [DllImport("gdi32.dll", EntryPoint = "GetKerningPairsW")]
        private static extern int GetKerningPairs(IntPtr hdc, int cPairs, [Out] KERNINGPAIR[] lpkrnpair);
        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
