using System;
using System.Drawing;

namespace CADability
{
    /// <summary>
    /// A bold or italic face made from another face of the family, for a family that does not have that style, the way GDI+
    /// made it for the texts of CADability before the font service (issue #412): GDI+ reports every style of every family
    /// as available and synthesizes the missing ones.
    /// <list type="bullet">
    /// <item>Italic is a shear of the glyphs by 0x5700/0x10000 (18.77°) around the baseline. The advance widths stay the same.</item>
    /// <item>Bold makes every stroke wider: the outline is moved outwards by half the strength on every side
    /// (FT_Outline_EmboldenXY of FreeType) and then by half the strength to the right and up, so that the left side and the
    /// baseline of a glyph stay where they are and its right side and top grow by the strength. The strength is
    /// ceiling(em / 50) font units horizontally and one unit less vertically, e.g. 41 and 40 for 2048 units per em. A face
    /// with a weight of 600 (semibold) or more is not made wider. The advance widths stay the same, as with the widths of
    /// GDI that CADability used.</item>
    /// </list>
    /// Both were measured against GDI+ on Windows.
    /// </summary>
    internal sealed class SyntheticFontFace : FontFace
    {
        /// <summary>
        /// The shear of the synthetic italic of GDI and GDI+: 0x5700/0x10000, about tan(18.77°).
        /// </summary>
        internal const double ItalicShear = 0x5700 / 65536.0;

        private readonly FontFace face;
        private readonly bool bold, italic;

        public SyntheticFontFace(FontFace face, bool bold, bool italic)
        {
            this.face = face;
            this.bold = bold;
            this.italic = italic;
        }
        /// <summary>
        /// The face the synthetic style is made from.
        /// </summary>
        internal FontFace BaseFace => face;
        public override string FamilyName => face.FamilyName;
        public override bool IsBold => bold || face.IsBold;
        public override bool IsItalic => italic || face.IsItalic;
        public override int EmHeight => face.EmHeight;
        public override int CellAscent => face.CellAscent;
        public override int CellDescent => face.CellDescent;
        public override int LineSpacing => face.LineSpacing;
        public override double GetAdvanceWidth(int codePoint) => face.GetAdvanceWidth(codePoint);
        public override double GetKerning(int left, int right) => face.GetKerning(left, right);

        /// <summary>
        /// The strength of the synthetic bold of <paramref name="face"/> in units of the em height, horizontally and
        /// vertically: how much wider and higher a stroke gets. Zero for a face with a weight of 600 or more.
        /// </summary>
        internal static (double x, double y) BoldStrengthOf(FontFace face)
        {
            int weight = face is ManagedFontFace managed ? managed.Font.WeightClass : face.IsBold ? 700 : 400;
            if (weight >= 600) return (0.0, 0.0); // GDI+ does not make a semibold or heavier face wider
            int em = face.EmHeight;
            int x = (int)Math.Ceiling(em / 50.0);
            return (x / (double)em, (x - 1) / (double)em);
        }

        public override void GetGlyphPath(int codePoint, bool underline, bool strikeout, out PointF[] points, out byte[] types)
        {
            face.GetGlyphPath(codePoint, underline, strikeout, out points, out types);
            if (points.Length == 0) return;
            if (bold)
            {
                (double x, double y) = BoldStrengthOf(face);
                if (x > 0) points = Embolden(points, types, x, y);
            }
            if (italic)
            {
                // the path is in units of the em height with y downwards and the baseline at CellAscent/EmHeight
                double baseline = CellAscent / (double)EmHeight;
                for (int i = 0; i < points.Length; ++i) points[i].X = (float)(points[i].X + ItalicShear * (baseline - points[i].Y));
            }
        }

        /// <summary>
        /// Makes the strokes of a path (in the layout of <see cref="FontFace.GetGlyphPath"/>, y downwards) wider by
        /// <paramref name="strengthX"/> and higher by <paramref name="strengthY"/>: FT_Outline_EmboldenXY of FreeType moves every
        /// point of a contour outwards by half the strength along the bisector of its two edges, then the path is moved by half
        /// the strength to the right and up. For a polygon this moves every edge that faces right by the strength to the right
        /// and every edge that faces up by the strength up, and leaves the others where they are, which is what GDI+ does. The
        /// control points of the Bezier segments are moved the same way.
        /// </summary>
        internal static PointF[] Embolden(PointF[] points, byte[] types, double strengthX, double strengthY)
        {
            PointF[] res = (PointF[])points.Clone();
            double halfX = strengthX / 2.0, halfY = strengthY / 2.0;
            // the orientation of the whole outline, as FreeType determines it: the holes run the other way than the outer
            // contours, so that "outwards" means away from the ink for both, and the holes get smaller
            double area = 0.0;
            ForEachContour(types, (first, last) =>
            {
                for (int i = first; i <= last; ++i)
                {
                    int j = i == last ? first : i + 1;
                    area += points[i].X * -points[j].Y - points[j].X * -points[i].Y; // y upwards
                }
            });
            if (area == 0.0) return res;
            bool clockwise = area < 0; // the TrueType orientation (outer contours clockwise with y upwards)
            ForEachContour(types, (first, last) => EmboldenContour(points, res, first, last, halfX, halfY, clockwise));
            for (int i = 0; i < res.Length; ++i)
            {
                res[i].X = (float)(res[i].X + halfX);
                res[i].Y = (float)(res[i].Y - halfY);
            }
            return res;
        }

        private static void ForEachContour(byte[] types, Action<int, int> action)
        {
            int first = 0;
            while (first < types.Length)
            {
                int last = first;
                while (last + 1 < types.Length && (types[last + 1] & 0x07) != 0) ++last; // a figure ends before the next start point
                action(first, last);
                first = last + 1;
            }
        }

        private static void EmboldenContour(PointF[] points, PointF[] res, int first, int last, double halfX, double halfY, bool clockwise)
        {
            int n = last - first + 1;
            if (n < 3) return;
            // FreeType works with y upwards: flip y
            double X(int i) => points[first + (i % n + n) % n].X;
            double Y(int i) => -points[first + (i % n + n) % n].Y;

            // the incoming edge of the first point: the last edge with a length
            int prev = n - 1;
            double inX = 0, inY = 0, lIn = 0;
            for (int k = 0; k < n; ++k, --prev)
            {
                inX = X(0) - X(prev); inY = Y(0) - Y(prev);
                lIn = Math.Sqrt(inX * inX + inY * inY);
                if (lIn > 0) break;
            }
            if (lIn == 0) return;
            inX /= lIn; inY /= lIn;
            for (int i = 0; i < n; ++i)
            {
                if (i > 0 && X(i) == X(i - 1) && Y(i) == Y(i - 1))
                {   // a point on top of the previous one moves with it
                    res[first + i] = res[first + i - 1];
                    continue;
                }
                int next = i + 1;
                double outX = 0, outY = 0, lOut = 0;
                for (int k = 0; k < n; ++k, ++next)
                {
                    outX = X(next) - X(i); outY = Y(next) - Y(i);
                    lOut = Math.Sqrt(outX * outX + outY * outY);
                    if (lOut > 0) break;
                }
                if (lOut == 0) return;
                outX /= lOut; outY /= lOut;
                double shiftX = 0, shiftY = 0;
                double d = inX * outX + inY * outY; // cosine of the turn
                if (d > -0.9375) // not a sharp turn back, where the shift would be huge
                {
                    d += 1;
                    shiftX = inY + outY;
                    shiftY = inX + outX;
                    if (clockwise) shiftX = -shiftX; else shiftY = -shiftY;
                    double q = outX * inY - outY * inX; // sine of the turn
                    if (clockwise) q = -q;
                    double l = Math.Min(lIn, lOut);
                    // the shift along the bisector, limited by the length of the shorter edge
                    shiftX = halfX * q <= l * d ? shiftX * halfX / d : shiftX * l / q;
                    shiftY = halfY * q <= l * d ? shiftY * halfY / d : shiftY * l / q;
                }
                res[first + i].X = (float)(points[first + i].X + shiftX);
                res[first + i].Y = (float)(points[first + i].Y - shiftY); // back to y downwards
                inX = outX; inY = outY; lIn = lOut;
            }
        }
    }
}
