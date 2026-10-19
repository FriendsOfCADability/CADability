using System;
using System.Collections.Generic;
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
            (double x, double y) = bold ? BoldStrengthOf(face) : (0.0, 0.0);
            if (x > 0 && face is ManagedFontFace managed)
            {   // made wider on the outline of the font, before the quadratic segments of TrueType become cubic ones
                managed.GetGlyphPath(codePoint, underline, strikeout, x, y, out points, out types);
            }
            else
            {
                face.GetGlyphPath(codePoint, underline, strikeout, out points, out types);
                if (x > 0 && points.Length > 0) points = Embolden(points, types, x, y);
            }
            if (points.Length == 0) return;
            if (italic)
            {
                // the path is in units of the em height with y downwards and the baseline at CellAscent/EmHeight
                double baseline = CellAscent / (double)EmHeight;
                for (int i = 0; i < points.Length; ++i) points[i].X = (float)(points[i].X + ItalicShear * (baseline - points[i].Y));
            }
        }

        /// <summary>
        /// Makes the strokes of a path (in the layout of <see cref="FontFace.GetGlyphPath"/>, y downwards) wider by
        /// <paramref name="strengthX"/> and higher by <paramref name="strengthY"/>, see <see cref="EmboldenContours"/>. The
        /// control points of the Bezier segments are moved like the other points. For the glyphs of a TrueType font this is
        /// done on the quadratic outline instead, as GDI+ does it (see <see cref="TrueTypeFont.GetPathData(int, bool, bool, double, double, out PointF[], out byte[])"/>).
        /// </summary>
        internal static PointF[] Embolden(PointF[] points, byte[] types, double strengthX, double strengthY)
        {
            List<(double x, double y)[]> contours = new List<(double x, double y)[]>();
            List<int> starts = new List<int>();
            int first = 0;
            while (first < types.Length)
            {
                int last = first;
                while (last + 1 < types.Length && (types[last + 1] & 0x07) != 0) ++last; // a figure ends before the next start point
                (double x, double y)[] contour = new (double x, double y)[last - first + 1];
                for (int i = first; i <= last; ++i) contour[i - first] = (points[i].X, -points[i].Y); // y upwards
                contours.Add(contour);
                starts.Add(first);
                first = last + 1;
            }
            List<(double x, double y)[]> bold = EmboldenContours(contours, strengthX, strengthY);
            PointF[] res = new PointF[points.Length];
            for (int c = 0; c < bold.Count; ++c)
                for (int i = 0; i < bold[c].Length; ++i) res[starts[c] + i] = new PointF((float)bold[c][i].x, (float)-bold[c][i].y);
            return res;
        }

        /// <summary>
        /// Makes the strokes of an outline (closed contours, y upwards) wider by <paramref name="strengthX"/> and higher by
        /// <paramref name="strengthY"/>: FT_Outline_EmboldenXY of FreeType moves every point of a contour outwards by half the
        /// strength along the bisector of its two edges, then the outline is moved by half the strength to the right and up.
        /// For a polygon this moves every edge that faces right by the strength to the right and every edge that faces up by
        /// the strength up, and leaves the others where they are, which is what GDI+ does. The orientation is that of the
        /// whole outline, as FreeType determines it: the holes run the other way than the outer contours, so that "outwards"
        /// means away from the ink for both, and the holes get smaller.
        /// </summary>
        internal static List<(double x, double y)[]> EmboldenContours(IList<(double x, double y)[]> contours, double strengthX, double strengthY)
        {
            List<(double x, double y)[]> res = new List<(double x, double y)[]>(contours.Count);
            foreach ((double x, double y)[] contour in contours) res.Add(((double x, double y)[])contour.Clone());
            double area = 0.0;
            foreach ((double x, double y)[] contour in contours)
                for (int i = 0; i < contour.Length; ++i)
                {
                    var p = contour[i]; var q = contour[(i + 1) % contour.Length];
                    area += p.x * q.y - q.x * p.y;
                }
            if (area == 0.0) return res;
            bool clockwise = area < 0; // the TrueType orientation (outer contours clockwise with y upwards)
            double halfX = strengthX / 2.0, halfY = strengthY / 2.0;
            for (int c = 0; c < contours.Count; ++c) EmboldenContour(contours[c], res[c], halfX, halfY, clockwise);
            foreach ((double x, double y)[] contour in res)
                for (int i = 0; i < contour.Length; ++i) contour[i] = (contour[i].x + halfX, contour[i].y + halfY);
            return res;
        }

        private static void EmboldenContour((double x, double y)[] points, (double x, double y)[] res, double halfX, double halfY, bool clockwise)
        {
            int n = points.Length;
            if (n < 3) return;
            (double x, double y) P(int i) => points[(i % n + n) % n];
            // the incoming edge of the first point: the last edge with a length
            double inX = 0, inY = 0, lIn = 0;
            for (int k = 1; k <= n; ++k)
            {
                inX = P(0).x - P(-k).x; inY = P(0).y - P(-k).y;
                lIn = Math.Sqrt(inX * inX + inY * inY);
                if (lIn > 0) break;
            }
            if (lIn == 0) return;
            inX /= lIn; inY /= lIn;
            for (int i = 0; i < n; ++i)
            {
                if (i > 0 && P(i) == P(i - 1))
                {   // a point on top of the previous one moves with it
                    res[i] = res[i - 1];
                    continue;
                }
                double outX = 0, outY = 0, lOut = 0;
                for (int k = 1; k <= n; ++k)
                {
                    outX = P(i + k).x - P(i).x; outY = P(i + k).y - P(i).y;
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
                res[i] = (points[i].x + shiftX, points[i].y + shiftY);
                inX = outX; inY = outY; lIn = lOut;
            }
        }
    }
}
