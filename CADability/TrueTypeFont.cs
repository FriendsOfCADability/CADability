using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace CADability
{
    /// <summary>
    /// A point of a TrueType glyph contour in font units. Off-curve points are the control points of quadratic Bezier
    /// segments; between two consecutive off-curve points there is an implied on-curve point in the middle.
    /// </summary>
    internal struct GlyphPoint
    {
        public double X, Y;
        public bool OnCurve;
        public GlyphPoint(double x, double y, bool onCurve)
        {
            X = x;
            Y = y;
            OnCurve = onCurve;
        }
    }

    /// <summary>
    /// Reads a TrueType font (.ttf, or one font of a .ttc collection) without any help of the operating system, so that text
    /// works on every platform (issue #412). It reads the glyph outlines (exact quadratic curves from the "glyf" table,
    /// composite glyphs included), the advance widths, the metrics, the kerning pairs of the "kern" table and the names.
    /// OpenType fonts with CFF outlines (most .otf files) are read with <see cref="CffOutlines"/>: their cubic curves go into
    /// the path data unchanged.
    /// </summary>
    internal sealed class TrueTypeFont
    {
        private readonly byte[] data;
        private readonly Dictionary<string, (int offset, int length)> tables = new Dictionary<string, (int offset, int length)>();
        private readonly int numGlyphs;
        private readonly int numberOfHMetrics;
        private readonly bool longLoca;
        private int cmapOffset = -1, cmapFormat, symbolOffset;
        private Dictionary<int, int> kerningPairs; // (left glyph << 16 | right glyph) -> value in font units, created on demand
        private readonly int fontOffset; // of the table directory, a font of a collection does not start at 0
        private Dictionary<int, string> names; // name ID -> name, the preferred record per ID
        private readonly FontVariations variations; // null for a font that is not variable
        private double[] coords; // normalized variation coordinates, null for the default instance

        /// <summary>
        /// The family name, e.g. "Liberation Sans", as GDI knows it (name ID 1, Windows platform preferred).
        /// </summary>
        public string FamilyName { get; }
        /// <summary>
        /// The typographic family name (name ID 16) when the font has one, otherwise <see cref="FamilyName"/>.
        /// </summary>
        public string TypographicFamilyName { get; }
        /// <summary>
        /// The subfamily (style) name, e.g. "Regular" or "Bold Italic" (name ID 2).
        /// </summary>
        public string SubfamilyName { get; }
        /// <summary>
        /// The full name, e.g. "Liberation Sans Bold" (name ID 4).
        /// </summary>
        public string FullName { get; }
        public bool IsBold { get; }
        public bool IsItalic { get; }
        public int UnitsPerEm { get; }
        public int Ascender { get; }
        public int Descender { get; }
        public int LineGap { get; }
        public int WinAscent { get; }
        public int WinDescent { get; }
        /// <summary>
        /// The top of the underline above the baseline in font units (negative: below), from the "post" table.
        /// </summary>
        public int UnderlinePosition { get; }
        public int UnderlineThickness { get; }
        /// <summary>
        /// The top of the strikeout line above the baseline in font units, from the "OS/2" table.
        /// </summary>
        public int StrikeoutPosition { get; }
        public int StrikeoutSize { get; }
        /// <summary>
        /// True for a font read by <see cref="ReadHeaders"/>: it knows its names, style and metrics, but has no glyphs.
        /// </summary>
        public bool IsHeaderOnly { get; private set; }
        /// <summary>
        /// False for an OpenType font with CFF outlines and for a bitmap font, whose glyphs cannot be read.
        /// </summary>
        public bool HasTrueTypeOutlines { get; }
        /// <summary>
        /// True for an OpenType font with outlines in the "CFF " table (Compact Font Format, cubic curves).
        /// </summary>
        public bool HasCffOutlines { get; }
        /// <summary>
        /// True if the CFF outlines are in the "CFF2" table, the format of variable OpenType fonts.
        /// </summary>
        public bool IsCff2 { get; }
        /// <summary>
        /// True if the glyph outlines can be read: TrueType or CFF outlines. False for bitmap fonts and for CFF2 (variable
        /// OpenType fonts).
        /// </summary>
        public bool HasOutlines => HasTrueTypeOutlines || HasCffOutlines;
        private CffOutlines cff; // created on demand
        public int GlyphCount => numGlyphs;

        /// <summary>
        /// The ascent of the character cell in font units, as GDI and GDI+ use it for TrueType fonts (usWinAscent).
        /// </summary>
        public int CellAscent => WinAscent;
        /// <summary>
        /// The descent of the character cell in font units, positive downwards (usWinDescent).
        /// </summary>
        public int CellDescent => WinDescent;
        /// <summary>
        /// The distance between two lines in font units, computed like GDI does: the cell height plus the external leading,
        /// which is the part of the line gap of the "hhea" table that is not already part of the cell.
        /// </summary>
        public int LineSpacing
        {
            get
            {
                int cell = WinAscent + WinDescent;
                int external = Math.Max(0, LineGap - (cell - (Ascender - Descender)));
                return cell + external;
            }
        }

        private TrueTypeFont(byte[] data, int offset)
        {
            this.data = data;
            fontOffset = offset;
            uint version = U32(offset);
            bool trueType = version == 0x00010000 || version == 0x74727565; // 'true' (old Apple fonts)
            if (!trueType && version != 0x4F54544F) throw new InvalidDataException("not a TrueType or OpenType font"); // 'OTTO'
            int numTables = U16(offset + 4);
            for (int i = 0; i < numTables; ++i)
            {
                int rec = offset + 12 + 16 * i;
                string tag = Encoding.ASCII.GetString(data, rec, 4);
                int tableOffset = (int)U32(rec + 8);
                int length = (int)U32(rec + 12);
                if (tableOffset < 0 || length < 0 || tableOffset + (long)length > data.Length) throw new InvalidDataException("table " + tag + " exceeds the file");
                tables[tag] = (tableOffset, length);
            }
            // a TrueType font without outlines is a bitmap font (e.g. color emoji)
            HasTrueTypeOutlines = trueType && tables.ContainsKey("glyf") && tables.ContainsKey("loca");
            HasCffOutlines = !trueType && (tables.ContainsKey("CFF ") || tables.ContainsKey("CFF2"));
            IsCff2 = !trueType && !tables.ContainsKey("CFF ") && tables.ContainsKey("CFF2");
            int head = Table("head");
            UnitsPerEm = U16(head + 18);
            if (UnitsPerEm == 0) UnitsPerEm = 1000;
            int macStyle = U16(head + 44);
            longLoca = S16(head + 50) != 0;
            numGlyphs = U16(Table("maxp") + 4);
            int hhea = Table("hhea");
            Ascender = S16(hhea + 4);
            Descender = S16(hhea + 6);
            LineGap = S16(hhea + 8);
            numberOfHMetrics = U16(hhea + 34);
            bool bold = (macStyle & 1) != 0, italic = (macStyle & 2) != 0;
            WinAscent = Ascender;
            WinDescent = -Descender;
            // defaults like the ones GDI uses when the tables are missing
            UnderlineThickness = Math.Max(1, UnitsPerEm / 14);
            UnderlinePosition = -UnitsPerEm / 10;
            StrikeoutSize = UnderlineThickness;
            StrikeoutPosition = UnitsPerEm / 4;
            if (tables.TryGetValue("OS/2", out var os2) && os2.length >= 78)
            {
                StrikeoutSize = S16(os2.offset + 26);
                StrikeoutPosition = S16(os2.offset + 28);
                int fsSelection = U16(os2.offset + 62);
                italic = (fsSelection & 1) != 0;
                bold = (fsSelection & 0x20) != 0;
                WinAscent = U16(os2.offset + 74);
                WinDescent = U16(os2.offset + 76);
            }
            if (tables.TryGetValue("post", out var post) && post.length >= 12)
            {
                UnderlinePosition = S16(post.offset + 8);
                UnderlineThickness = S16(post.offset + 10);
            }
            IsBold = bold;
            IsItalic = italic;
            ReadNames();
            FamilyName = NameOf(1) ?? "";
            SubfamilyName = NameOf(2) ?? "";
            FullName = NameOf(4) ?? FamilyName;
            TypographicFamilyName = NameOf(16) ?? FamilyName;
            SelectCmap();
            if (tables.ContainsKey("fvar") && (HasTrueTypeOutlines || HasCffOutlines)) variations = new FontVariations(data, tables, NameOf);
        }

        /// <summary>
        /// The name with the name ID <paramref name="nameId"/> (Windows US English preferred), null if the font has none.
        /// </summary>
        public string NameOf(int nameId)
        {
            return names != null && names.TryGetValue(nameId, out string res) ? res : null;
        }
        /// <summary>
        /// True for a variable font ("fvar"). <see cref="NamedInstances"/> are its predefined instances, <see cref="WithVariation"/>
        /// makes an instance at any coordinates.
        /// </summary>
        public bool IsVariable => variations != null;
        /// <summary>
        /// The axes of a variable font, empty for other fonts.
        /// </summary>
        public VariationAxis[] Axes => variations?.Axes ?? new VariationAxis[0];
        /// <summary>
        /// The named instances of a variable font, e.g. "Light Condensed", empty for other fonts.
        /// </summary>
        public NamedInstance[] NamedInstances => variations?.Instances ?? new NamedInstance[0];
        /// <summary>
        /// The normalized coordinates of this instance, null for the default instance.
        /// </summary>
        public double[] NormalizedCoordinates => coords == null ? null : (double[])coords.Clone();
        /// <summary>
        /// Returns this variable font at the coordinates <paramref name="userCoordinates"/> (in the units of the axes, e.g. wght
        /// 300, one value per axis of <see cref="Axes"/>): its glyph outlines and advance widths are varied. The metrics of the
        /// whole font ("MVAR") stay those of the default instance.
        /// </summary>
        public TrueTypeFont WithVariation(double[] userCoordinates)
        {
            if (variations == null) throw new InvalidOperationException("The font \"" + FullName + "\" is not a variable font");
            TrueTypeFont res = new TrueTypeFont(data, fontOffset);
            res.IsHeaderOnly = IsHeaderOnly;
            double[] normalized = res.variations.Normalize(userCoordinates);
            res.coords = normalized.Any(c => c != 0.0) ? normalized : null;
            return res;
        }

        /// <summary>
        /// Reads all fonts of a font file: one for a .ttf or .otf file, all fonts of a .ttc or .otc collection.
        /// </summary>
        /// <param name="data">The content of the file</param>
        /// <returns>The fonts</returns>
        public static TrueTypeFont[] Load(byte[] data)
        {
            if (data == null || data.Length < 12) throw new InvalidDataException("not a font file");
            if (data[0] == 't' && data[1] == 't' && data[2] == 'c' && data[3] == 'f')
            {
                int count = (int)ReadU32(data, 8);
                if (count <= 0 || 12 + 4L * count > data.Length) throw new InvalidDataException("invalid font collection");
                TrueTypeFont[] res = new TrueTypeFont[count];
                for (int i = 0; i < count; ++i) res[i] = new TrueTypeFont(data, (int)ReadU32(data, 12 + 4 * i));
                return res;
            }
            return new TrueTypeFont[] { new TrueTypeFont(data, 0) };
        }
        /// <summary>
        /// Reads the names, styles and metrics of all fonts of a font file without reading the whole file: only the tables
        /// "head", "hhea", "maxp", "OS/2", "post" and "name" are read. This is how a directory with hundreds of fonts is indexed
        /// quickly. The fonts returned have <see cref="IsHeaderOnly"/> set; load the file with <see cref="Load(string)"/> to
        /// get the glyphs.
        /// </summary>
        public static TrueTypeFont[] ReadHeaders(string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] head = ReadBytes(stream, 0, 12);
                if (head[0] == 't' && head[1] == 't' && head[2] == 'c' && head[3] == 'f')
                {
                    int count = (int)ReadU32(head, 8);
                    if (count <= 0 || count > 10000) throw new InvalidDataException("invalid font collection");
                    byte[] offsets = ReadBytes(stream, 12, 4 * count);
                    TrueTypeFont[] res = new TrueTypeFont[count];
                    for (int i = 0; i < count; ++i) res[i] = ReadHeader(stream, ReadU32(offsets, 4 * i));
                    return res;
                }
                return new TrueTypeFont[] { ReadHeader(stream, 0) };
            }
        }
        private static TrueTypeFont ReadHeader(FileStream stream, uint offset)
        {
            // a small font made of the needed tables only, with the same table directory layout, so that the constructor reads it
            byte[] directory = ReadBytes(stream, offset, 12);
            int numTables = (directory[4] << 8) | directory[5];
            if (numTables > 1000) throw new InvalidDataException("invalid table directory");
            byte[] records = ReadBytes(stream, offset + 12, 16 * numTables);
            string[] needed = { "head", "hhea", "maxp", "OS/2", "post", "name", "fvar" };
            List<(byte[] record, byte[] content)> copied = new List<(byte[], byte[])>();
            for (int i = 0; i < numTables; ++i)
            {
                string tag = Encoding.ASCII.GetString(records, 16 * i, 4);
                byte[] record = new byte[16];
                Array.Copy(records, 16 * i, record, 0, 16);
                if (Array.IndexOf(needed, tag) >= 0)
                {
                    uint length = ReadU32(records, 16 * i + 12);
                    if (length > stream.Length) throw new InvalidDataException("table " + tag + " exceeds the file");
                    copied.Add((record, ReadBytes(stream, ReadU32(records, 16 * i + 8), (int)length)));
                }
                else if (tag == "glyf" || tag == "loca" || tag == "CFF " || tag == "CFF2")
                {   // only their presence matters (HasTrueTypeOutlines, HasCffOutlines)
                    for (int k = 8; k < 16; ++k) record[k] = 0;
                    copied.Add((record, new byte[0]));
                }
            }
            int size = 12 + 16 * copied.Count;
            foreach (var (_, content) in copied) size += content.Length;
            byte[] data = new byte[size];
            Array.Copy(directory, data, 12);
            data[4] = (byte)(copied.Count >> 8);
            data[5] = (byte)copied.Count;
            int pos = 12 + 16 * copied.Count;
            for (int i = 0; i < copied.Count; ++i)
            {
                byte[] record = copied[i].record, content = copied[i].content;
                if (content.Length > 0)
                {
                    record[8] = (byte)(pos >> 24); record[9] = (byte)(pos >> 16); record[10] = (byte)(pos >> 8); record[11] = (byte)pos;
                }
                Array.Copy(record, 0, data, 12 + 16 * i, 16);
                Array.Copy(content, 0, data, pos, content.Length);
                pos += content.Length;
            }
            TrueTypeFont res = new TrueTypeFont(data, 0);
            res.IsHeaderOnly = true;
            return res;
        }
        private static byte[] ReadBytes(FileStream stream, long offset, int count)
        {
            if (count < 0 || offset + count > stream.Length) throw new InvalidDataException("unexpected end of the font file");
            byte[] res = new byte[count];
            stream.Seek(offset, SeekOrigin.Begin);
            int read = 0;
            while (read < count)
            {
                int n = stream.Read(res, read, count - read);
                if (n <= 0) throw new InvalidDataException("unexpected end of the font file");
                read += n;
            }
            return res;
        }
        /// <summary>
        /// Reads all fonts of the font file <paramref name="fileName"/>, see <see cref="Load(byte[])"/>.
        /// </summary>
        public static TrueTypeFont[] Load(string fileName)
        {
            return Load(File.ReadAllBytes(fileName));
        }

        /// <summary>
        /// Returns the glyph index of the character <paramref name="codePoint"/> (a Unicode code point), 0 (the "missing
        /// glyph") if the font has no glyph for it.
        /// </summary>
        public int GetGlyphIndex(int codePoint)
        {
            if (cmapOffset < 0) return 0;
            int glyph = Lookup(codePoint);
            if (glyph == 0 && symbolOffset != 0 && codePoint < 0x100) glyph = Lookup(codePoint + symbolOffset);
            return glyph < numGlyphs ? glyph : 0;
        }
        /// <summary>
        /// Returns the advance width of a glyph in font units.
        /// </summary>
        public double GetAdvanceWidth(int glyph)
        {
            int hmtx = Table("hmtx");
            if (numberOfHMetrics == 0) return 0;
            int i = Math.Min(glyph, numberOfHMetrics - 1);
            double advance = U16(hmtx + 4 * i);
            if (coords != null)
            {
                double delta = variations.GetAdvanceDelta(glyph, coords);
                if (double.IsNaN(delta)) delta = PhantomAdvanceDelta(glyph); // no HVAR: the phantom points of gvar
                advance += delta; // fractional: the exact value of the instance
            }
            return advance;
        }
        private int LeftSideBearing(int glyph)
        {
            int hmtx = Table("hmtx");
            if (glyph < numberOfHMetrics) return S16(hmtx + 4 * glyph + 2);
            return S16(hmtx + 4 * numberOfHMetrics + 2 * (glyph - numberOfHMetrics));
        }
        /// <summary>
        /// The change of the advance width from the deltas of the two horizontal phantom points in "gvar".
        /// </summary>
        private double PhantomAdvanceDelta(int glyph)
        {
            if (!HasTrueTypeOutlines) return 0.0;
            int g = GlyphOffset(glyph, out int length);
            int pointCount = 0;
            int xMin = 0;
            if (length > 0)
            {
                int numberOfContours = S16(g);
                xMin = S16(g + 2);
                if (numberOfContours >= 0) pointCount = numberOfContours == 0 ? 0 : U16(g + 10 + 2 * (numberOfContours - 1)) + 1;
                else pointCount = CompositeComponents(g).Count;
            }
            double[] x = new double[pointCount + 4], y = new double[pointCount + 4], dx = new double[pointCount + 4], dy = new double[pointCount + 4];
            x[pointCount] = xMin - LeftSideBearing(glyph);
            variations.AddGlyphDeltas(glyph, x, y, null, coords, dx, dy);
            return dx[pointCount + 1] - dx[pointCount];
        }
        /// <summary>
        /// Returns the kerning between two glyphs in font units from the "kern" table, 0 if there is none. Kerning that only
        /// exists in the "GPOS" table is not used, which is what GDI does as well.
        /// </summary>
        public int GetKerning(int leftGlyph, int rightGlyph)
        {
            if (kerningPairs == null) kerningPairs = ReadKerning();
            kerningPairs.TryGetValue((leftGlyph << 16) | rightGlyph, out int res);
            return res;
        }
        /// <summary>
        /// All kerning pairs of the "kern" table: (left glyph, right glyph) to the value in font units.
        /// </summary>
        public IEnumerable<KeyValuePair<(int left, int right), int>> GetKerningPairs()
        {
            if (kerningPairs == null) kerningPairs = ReadKerning();
            foreach (KeyValuePair<int, int> kv in kerningPairs) yield return new KeyValuePair<(int, int), int>((kv.Key >> 16, kv.Key & 0xFFFF), kv.Value);
        }

        /// <summary>
        /// Returns the contours of a glyph in font units (y upwards), with the components of a composite glyph resolved.
        /// </summary>
        public List<GlyphPoint[]> GetContours(int glyph)
        {
            if (!HasTrueTypeOutlines) throw new NotSupportedException("The outlines of the font \"" + FullName + "\" cannot be read: they are in CFF format or the font has bitmaps only");
            if (IsHeaderOnly) throw new InvalidOperationException("The font \"" + FullName + "\" was read by ReadHeaders and has no glyphs, load it with Load");
            List<GlyphPoint[]> res = new List<GlyphPoint[]>();
            AddContours(glyph, res, 0);
            return res;
        }

        /// <summary>
        /// Returns the outline of a glyph as path data in the form a GDI+ GraphicsPath has after <c>AddString</c> with an em size
        /// of 1 and <c>StringFormat.GenericTypographic</c>: in units of the em, y downwards, the top of the character cell at 0
        /// (the baseline at <see cref="CellAscent"/>/<see cref="UnitsPerEm"/>). The quadratic segments are converted to cubic Bezier
        /// segments without any loss (degree elevation), as GDI+ does. The point types are those of GDI+ PathPointType:
        /// 0 start, 1 line, 3 Bezier, 0x80 closes the figure. This is the input of GlyphShapeBuilder.PathDataToPath2D.
        /// </summary>
        public void GetPathData(int glyph, out PointF[] points, out byte[] types)
        {
            GetPathData(glyph, false, false, out points, out types);
        }
        /// <summary>
        /// Like <see cref="GetPathData(int, out PointF[], out byte[])"/>, and adds the underline and the strikeout line as
        /// rectangles over the advance width of the glyph, as GDI+ does for an underlined or struck out font.
        /// </summary>
        public void GetPathData(int glyph, bool underline, bool strikeout, out PointF[] points, out byte[] types)
        {
            List<PointF> pts = new List<PointF>();
            List<byte> tps = new List<byte>();
            double scale = 1.0 / UnitsPerEm;
            PointF Map(double x, double y) => new PointF((float)(x * scale), (float)((CellAscent - y) * scale));
            if (HasCffOutlines) DrawCff(glyph, new PathDataSink(pts, tps, Map));
            else foreach (GlyphPoint[] contour in GetContours(glyph))
            {
                if (contour.Length < 2) continue;
                int n = contour.Length;
                // the start point: the first on-curve point, or the middle between the last and the first point
                int first = -1;
                for (int i = 0; i < n; ++i) if (contour[i].OnCurve) { first = i; break; }
                double sx, sy;
                if (first >= 0)
                {
                    sx = contour[first].X;
                    sy = contour[first].Y;
                }
                else
                {
                    first = n - 1; // all points are off-curve: start in the middle of the last and the first point
                    sx = (contour[n - 1].X + contour[0].X) / 2;
                    sy = (contour[n - 1].Y + contour[0].Y) / 2;
                }
                int startIndex = pts.Count;
                pts.Add(Map(sx, sy));
                tps.Add(0);
                double cx = sx, cy = sy; // current point
                for (int k = 1; k <= n; ++k)
                {
                    GlyphPoint p = contour[(first + k) % n];
                    if (p.OnCurve)
                    {
                        if (k == n) break; // the closing line is implied by the closing flag
                        if (p.X == cx && p.Y == cy) continue;
                        pts.Add(Map(p.X, p.Y));
                        tps.Add(1);
                        cx = p.X;
                        cy = p.Y;
                    }
                    else
                    {
                        GlyphPoint next = contour[(first + k + 1) % n];
                        double ex, ey;
                        if (k == n)
                        {   // the start point itself was implied (all points off-curve), the last curve ends there
                            ex = sx;
                            ey = sy;
                        }
                        else if (next.OnCurve)
                        {
                            ex = next.X;
                            ey = next.Y;
                            ++k;
                        }
                        else
                        {
                            ex = (p.X + next.X) / 2;
                            ey = (p.Y + next.Y) / 2;
                        }
                        if (!(p.X == cx && p.Y == cy && ex == cx && ey == cy))
                        {
                            // degree elevation of the quadratic segment (c, p, e) to a cubic one
                            pts.Add(Map(cx + 2.0 / 3.0 * (p.X - cx), cy + 2.0 / 3.0 * (p.Y - cy)));
                            pts.Add(Map(ex + 2.0 / 3.0 * (p.X - ex), ey + 2.0 / 3.0 * (p.Y - ey)));
                            pts.Add(Map(ex, ey));
                            tps.Add(3);
                            tps.Add(3);
                            tps.Add(3);
                        }
                        cx = ex;
                        cy = ey;
                    }
                }
                if (pts.Count - startIndex < 2)
                {   // nothing but the start point
                    pts.RemoveAt(startIndex);
                    tps.RemoveAt(startIndex);
                    continue;
                }
                tps[tps.Count - 1] |= 0x80;
            }
            void AddBar(int top, int thickness)
            {
                if (thickness <= 0) return;
                double width = GetAdvanceWidth(glyph);
                pts.Add(Map(0, top)); tps.Add(0);
                pts.Add(Map(width, top)); tps.Add(1);
                pts.Add(Map(width, top - thickness)); tps.Add(1);
                pts.Add(Map(0, top - thickness)); tps.Add(0x81);
            }
            if (underline) AddBar(UnderlinePosition, UnderlineThickness);
            if (strikeout) AddBar(StrikeoutPosition, StrikeoutSize);
            points = pts.ToArray();
            types = tps.ToArray();
        }

        /// <summary>
        /// Sends the outline of a glyph of a font with CFF outlines to <paramref name="sink"/>, in font units.
        /// </summary>
        internal void DrawCff(int glyph, IGlyphOutlineSink sink)
        {
            if (IsHeaderOnly) throw new InvalidOperationException("The font \"" + FullName + "\" was read by ReadHeaders and has no glyphs, load it with Load");
            if (!HasCffOutlines) throw new NotSupportedException("The font \"" + FullName + "\" has no CFF outlines");
            if (cff == null)
            {
                if (IsCff2)
                {
                    var t = tables["CFF2"];
                    cff = new CffOutlines(data, t.offset, t.length, UnitsPerEm, variations);
                }
                else
                {
                    var t = tables["CFF "];
                    cff = new CffOutlines(data, t.offset, t.length, UnitsPerEm);
                }
            }
            cff.Draw(glyph, sink, coords);
        }
        /// <summary>
        /// Collects an outline as GDI+ path data (see <see cref="GetPathData(int, out PointF[], out byte[])"/>).
        /// </summary>
        private sealed class PathDataSink : IGlyphOutlineSink
        {
            private readonly List<PointF> pts;
            private readonly List<byte> tps;
            private readonly Func<double, double, PointF> map;
            private int start = -1; // index of the start point of the open figure
            private double cx, cy;
            public PathDataSink(List<PointF> pts, List<byte> tps, Func<double, double, PointF> map)
            {
                this.pts = pts;
                this.tps = tps;
                this.map = map;
            }
            public void MoveTo(double x, double y)
            {
                ClosePath();
                start = pts.Count;
                pts.Add(map(x, y));
                tps.Add(0);
                cx = x;
                cy = y;
            }
            public void LineTo(double x, double y)
            {
                if (start < 0 || (x == cx && y == cy)) return;
                pts.Add(map(x, y));
                tps.Add(1);
                cx = x;
                cy = y;
            }
            public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
            {
                if (start < 0) return;
                if (x1 == cx && y1 == cy && x2 == cx && y2 == cy && x3 == cx && y3 == cy) return; // no length
                pts.Add(map(x1, y1));
                pts.Add(map(x2, y2));
                pts.Add(map(x3, y3));
                tps.Add(3);
                tps.Add(3);
                tps.Add(3);
                cx = x3;
                cy = y3;
            }
            public void ClosePath()
            {
                if (start < 0) return;
                if (pts.Count - start < 2)
                {   // nothing but the start point
                    pts.RemoveRange(start, pts.Count - start);
                    tps.RemoveRange(start, tps.Count - start);
                }
                else tps[tps.Count - 1] |= 0x80;
                start = -1;
            }
        }
        /// <summary>
        /// The position of a glyph in the "glyf" table and its length, 0 for an empty glyph.
        /// </summary>
        private int GlyphOffset(int glyph, out int length)
        {
            int loca = Table("loca");
            int start, end;
            if (longLoca)
            {
                start = (int)U32(loca + 4 * glyph);
                end = (int)U32(loca + 4 * glyph + 4);
            }
            else
            {
                start = 2 * U16(loca + 2 * glyph);
                end = 2 * U16(loca + 2 * glyph + 2);
            }
            var glyf = tables["glyf"];
            if (end > glyf.length) throw new InvalidDataException("glyph " + glyph + " exceeds the glyf table");
            length = Math.Max(0, end - start);
            return glyf.offset + start;
        }
        private void AddContours(int glyph, List<GlyphPoint[]> res, int depth)
        {
            if (glyph < 0 || glyph >= numGlyphs || depth > 8) return; // a deeper nesting is a broken (or malicious) font
            int g = GlyphOffset(glyph, out int length);
            if (length == 0) return; // an empty glyph, e.g. the space
            int numberOfContours = S16(g);
            if (numberOfContours >= 0) ReadSimpleGlyph(glyph, g, numberOfContours, res);
            else ReadCompositeGlyph(glyph, g, res, depth);
        }
        private void ReadSimpleGlyph(int glyph, int g, int numberOfContours, List<GlyphPoint[]> res)
        {
            int pos = g + 10;
            int[] endPts = new int[numberOfContours];
            for (int i = 0; i < numberOfContours; ++i) endPts[i] = U16(pos + 2 * i);
            pos += 2 * numberOfContours;
            int numPoints = numberOfContours == 0 ? 0 : endPts[numberOfContours - 1] + 1;
            int instructionLength = U16(pos);
            pos += 2 + instructionLength;
            byte[] flags = new byte[numPoints];
            for (int i = 0; i < numPoints;)
            {
                byte f = data[pos++];
                flags[i++] = f;
                if ((f & 8) != 0)
                {
                    int repeat = data[pos++];
                    for (int r = 0; r < repeat && i < numPoints; ++r) flags[i++] = f;
                }
            }
            double[] xs = new double[numPoints], ys = new double[numPoints];
            int v = 0;
            for (int i = 0; i < numPoints; ++i)
            {
                byte f = flags[i];
                if ((f & 2) != 0) v += (f & 0x10) != 0 ? data[pos++] : -data[pos++];
                else if ((f & 0x10) == 0) { v += S16(pos); pos += 2; }
                xs[i] = v;
            }
            v = 0;
            for (int i = 0; i < numPoints; ++i)
            {
                byte f = flags[i];
                if ((f & 4) != 0) v += (f & 0x20) != 0 ? data[pos++] : -data[pos++];
                else if ((f & 0x20) == 0) { v += S16(pos); pos += 2; }
                ys[i] = v;
            }
            if (coords != null)
            {   // a variable font: add the deltas of "gvar", with four phantom points after the points of the glyph
                double[] x = new double[numPoints + 4], y = new double[numPoints + 4], dx = new double[numPoints + 4], dy = new double[numPoints + 4];
                Array.Copy(xs, x, numPoints);
                Array.Copy(ys, y, numPoints);
                x[numPoints] = S16(g + 2) - LeftSideBearing(glyph);
                x[numPoints + 1] = x[numPoints] + U16(Table("hmtx") + 4 * Math.Min(glyph, numberOfHMetrics - 1));
                variations.AddGlyphDeltas(glyph, x, y, endPts, coords, dx, dy);
                for (int i = 0; i < numPoints; ++i)
                {
                    xs[i] += dx[i];
                    ys[i] += dy[i];
                }
            }
            int startPoint = 0;
            for (int c = 0; c < numberOfContours; ++c)
            {
                int count = endPts[c] - startPoint + 1;
                if (count <= 0 || endPts[c] >= numPoints) throw new InvalidDataException("invalid contour");
                GlyphPoint[] contour = new GlyphPoint[count];
                for (int i = 0; i < count; ++i) contour[i] = new GlyphPoint(xs[startPoint + i], ys[startPoint + i], (flags[startPoint + i] & 1) != 0);
                res.Add(contour);
                startPoint = endPts[c] + 1;
            }
        }
        private struct Component
        {
            public int Flags, Glyph, Arg1, Arg2;
            public double A, B, C, D;
        }
        private List<Component> CompositeComponents(int g)
        {
            List<Component> res = new List<Component>();
            int pos = g + 10;
            int flags;
            do
            {
                Component comp = new Component { A = 1, D = 1 };
                flags = comp.Flags = U16(pos);
                comp.Glyph = U16(pos + 2);
                pos += 4;
                if ((flags & 1) != 0) // ARG_1_AND_2_ARE_WORDS
                {
                    comp.Arg1 = S16(pos);
                    comp.Arg2 = S16(pos + 2);
                    pos += 4;
                }
                else if ((flags & 2) != 0)
                {   // offsets are signed
                    comp.Arg1 = (sbyte)data[pos];
                    comp.Arg2 = (sbyte)data[pos + 1];
                    pos += 2;
                }
                else
                {   // point numbers are unsigned
                    comp.Arg1 = data[pos];
                    comp.Arg2 = data[pos + 1];
                    pos += 2;
                }
                if ((flags & 0x8) != 0) // WE_HAVE_A_SCALE
                {
                    comp.A = comp.D = F2Dot14(pos);
                    pos += 2;
                }
                else if ((flags & 0x40) != 0) // WE_HAVE_AN_X_AND_Y_SCALE
                {
                    comp.A = F2Dot14(pos);
                    comp.D = F2Dot14(pos + 2);
                    pos += 4;
                }
                else if ((flags & 0x80) != 0) // WE_HAVE_A_TWO_BY_TWO
                {
                    comp.A = F2Dot14(pos);
                    comp.B = F2Dot14(pos + 2);
                    comp.C = F2Dot14(pos + 4);
                    comp.D = F2Dot14(pos + 6);
                    pos += 8;
                }
                res.Add(comp);
            } while ((flags & 0x20) != 0); // MORE_COMPONENTS
            return res;
        }
        private void ReadCompositeGlyph(int glyph, int g, List<GlyphPoint[]> res, int depth)
        {
            List<Component> components = CompositeComponents(g);
            double[] offsetX = new double[components.Count], offsetY = new double[components.Count];
            for (int i = 0; i < components.Count; ++i)
            {
                offsetX[i] = components[i].Arg1;
                offsetY[i] = components[i].Arg2;
            }
            if (coords != null)
            {   // a variable font: "gvar" has one delta per component (for its offset) and four for the phantom points
                int n = components.Count;
                double[] x = new double[n + 4], y = new double[n + 4], dx = new double[n + 4], dy = new double[n + 4];
                Array.Copy(offsetX, x, n);
                Array.Copy(offsetY, y, n);
                x[n] = S16(g + 2) - LeftSideBearing(glyph);
                variations.AddGlyphDeltas(glyph, x, y, null, coords, dx, dy);
                for (int i = 0; i < n; ++i)
                {
                    if ((components[i].Flags & 2) == 0) continue; // point matching has no offset to vary
                    offsetX[i] += dx[i];
                    offsetY[i] += dy[i];
                }
            }
            for (int ci = 0; ci < components.Count; ++ci)
            {
                Component comp = components[ci];
                double a = comp.A, b = comp.B, c = comp.C, d = comp.D;
                List<GlyphPoint[]> parts = new List<GlyphPoint[]>();
                AddContours(comp.Glyph, parts, depth + 1);
                double dx = 0, dy = 0;
                if ((comp.Flags & 2) != 0) // ARGS_ARE_XY_VALUES
                {
                    dx = offsetX[ci];
                    dy = offsetY[ci];
                    if ((comp.Flags & 0x800) != 0) // SCALED_COMPONENT_OFFSET: the offset is transformed as well
                    {
                        double tx = a * dx + c * dy, ty = b * dx + d * dy;
                        dx = tx;
                        dy = ty;
                    }
                }
                else
                {   // the component is placed by matching a point of the glyph so far (arg1) with a point of the component (arg2)
                    GlyphPoint? p1 = PointByIndex(res, comp.Arg1), p2 = PointByIndex(parts, comp.Arg2);
                    if (p1.HasValue && p2.HasValue)
                    {
                        dx = p1.Value.X - (a * p2.Value.X + c * p2.Value.Y);
                        dy = p1.Value.Y - (b * p2.Value.X + d * p2.Value.Y);
                    }
                }
                foreach (GlyphPoint[] part in parts)
                {
                    GlyphPoint[] moved = new GlyphPoint[part.Length];
                    for (int i = 0; i < part.Length; ++i)
                    {
                        double x = part[i].X, y = part[i].Y;
                        moved[i] = new GlyphPoint(a * x + c * y + dx, b * x + d * y + dy, part[i].OnCurve);
                    }
                    res.Add(moved);
                }
            }
        }
        private static GlyphPoint? PointByIndex(List<GlyphPoint[]> contours, int index)
        {
            foreach (GlyphPoint[] contour in contours)
            {
                if (index < contour.Length) return contour[index];
                index -= contour.Length;
            }
            return null;
        }

        private void SelectCmap()
        {
            if (!tables.TryGetValue("cmap", out var cmap)) return;
            int numTables = U16(cmap.offset + 2);
            int best = -1, bestRank = int.MaxValue;
            for (int i = 0; i < numTables; ++i)
            {
                int rec = cmap.offset + 4 + 8 * i;
                int platform = U16(rec), encoding = U16(rec + 2);
                int sub = cmap.offset + (int)U32(rec + 4);
                if (sub + 2 > cmap.offset + cmap.length) continue;
                int format = U16(sub);
                if (format != 4 && format != 12 && format != 6 && format != 0) continue;
                int rank;
                if (platform == 3 && encoding == 10 && format == 12) rank = 0;
                else if (platform == 0 && format == 12) rank = 1;
                else if (platform == 3 && encoding == 1) rank = 2;
                else if (platform == 0) rank = 3;
                else if (platform == 3 && encoding == 0) rank = 4; // symbol font
                else if (platform == 1 && encoding == 0) rank = 5; // Macintosh Roman, only right for ASCII
                else continue;
                if (rank < bestRank)
                {
                    bestRank = rank;
                    best = sub;
                }
            }
            if (best < 0) return;
            cmapOffset = best;
            cmapFormat = U16(best);
            if (bestRank == 4) symbolOffset = 0xF000; // symbol fonts map their characters to U+F000 to U+F0FF
        }
        private int Lookup(int c)
        {
            int t = cmapOffset;
            switch (cmapFormat)
            {
                case 0:
                    return c < 256 ? data[t + 6 + c] : 0;
                case 6:
                    {
                        int firstCode = U16(t + 6), entryCount = U16(t + 8);
                        int i = c - firstCode;
                        return i >= 0 && i < entryCount ? U16(t + 10 + 2 * i) : 0;
                    }
                case 4:
                    {
                        if (c > 0xFFFF) return 0;
                        int segCountX2 = U16(t + 6);
                        int endCodes = t + 14, startCodes = endCodes + segCountX2 + 2, idDeltas = startCodes + segCountX2, idRangeOffsets = idDeltas + segCountX2;
                        int lo = 0, hi = segCountX2 / 2 - 1;
                        while (lo <= hi)
                        {   // the segments are sorted by their end code
                            int mid = (lo + hi) / 2;
                            if (U16(endCodes + 2 * mid) < c) lo = mid + 1;
                            else hi = mid - 1;
                        }
                        if (lo >= segCountX2 / 2) return 0;
                        int start = U16(startCodes + 2 * lo);
                        if (c < start) return 0;
                        int delta = S16(idDeltas + 2 * lo);
                        int rangeOffsetPos = idRangeOffsets + 2 * lo;
                        int rangeOffset = U16(rangeOffsetPos);
                        if (rangeOffset == 0) return (c + delta) & 0xFFFF;
                        int glyphPos = rangeOffsetPos + rangeOffset + 2 * (c - start);
                        int glyph = U16(glyphPos);
                        return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
                    }
                case 12:
                    {
                        int numGroups = (int)U32(t + 12);
                        int lo = 0, hi = numGroups - 1;
                        while (lo <= hi)
                        {
                            int mid = (lo + hi) / 2;
                            int grp = t + 16 + 12 * mid;
                            uint startChar = U32(grp), endChar = U32(grp + 4);
                            if (c < startChar) hi = mid - 1;
                            else if (c > endChar) lo = mid + 1;
                            else return (int)(U32(grp + 8) + (c - startChar));
                        }
                        return 0;
                    }
            }
            return 0;
        }

        private Dictionary<int, int> ReadKerning()
        {
            Dictionary<int, int> res = new Dictionary<int, int>();
            if (!tables.TryGetValue("kern", out var kern) || kern.length < 4) return res;
            if (U16(kern.offset) != 0) return res; // only the version 0 (Windows) table, as GDI reads it
            int nTables = U16(kern.offset + 2);
            int pos = kern.offset + 4;
            for (int i = 0; i < nTables && pos + 6 <= kern.offset + kern.length; ++i)
            {
                int length = U16(pos + 2);
                int coverage = U16(pos + 4);
                int format = coverage >> 8;
                bool horizontal = (coverage & 1) != 0, minimum = (coverage & 2) != 0, crossStream = (coverage & 4) != 0;
                if (format == 0 && horizontal && !minimum && !crossStream)
                {
                    int nPairs = U16(pos + 6);
                    int p = pos + 14;
                    for (int k = 0; k < nPairs && p + 6 <= kern.offset + kern.length; ++k, p += 6)
                    {
                        int key = (U16(p) << 16) | U16(p + 2);
                        res.TryGetValue(key, out int value);
                        res[key] = value + S16(p + 4);
                    }
                }
                if (length == 0) break; // some fonts have a wrong length in a single subtable
                pos += length;
            }
            return res;
        }

        private void ReadNames()
        {
            names = new Dictionary<int, string>();
            if (!tables.TryGetValue("name", out var name)) return;
            int count = U16(name.offset + 2);
            int strings = name.offset + U16(name.offset + 4);
            // per name ID: the rank of the record found so far (Windows US English is best)
            Dictionary<int, int> rankOf = new Dictionary<int, int>();
            for (int i = 0; i < count; ++i)
            {
                int rec = name.offset + 6 + 12 * i;
                int platform = U16(rec), encoding = U16(rec + 2), language = U16(rec + 4), nameId = U16(rec + 6);
                if (nameId == 0 || (nameId > 25 && nameId < 256)) continue; // only the names CADability uses (families, styles, instances)
                int length = U16(rec + 8), offset = strings + U16(rec + 10);
                if (offset + length > data.Length) continue;
                int rank;
                if (platform == 3 && (encoding == 1 || encoding == 0 || encoding == 10)) rank = language == 0x409 ? 0 : 1;
                else if (platform == 0) rank = 2;
                else if (platform == 1 && encoding == 0) rank = 3;
                else continue;
                if (rankOf.TryGetValue(nameId, out int old) && old <= rank) continue;
                rankOf[nameId] = rank;
                names[nameId] = rank == 3 ? Encoding.ASCII.GetString(data, offset, length) : Encoding.BigEndianUnicode.GetString(data, offset, length);
            }
        }

        private int Table(string tag)
        {
            if (!tables.TryGetValue(tag, out var t)) throw new InvalidDataException("the font has no " + tag + " table");
            return t.offset;
        }
        private int U16(int pos) => (data[pos] << 8) | data[pos + 1];
        private short S16(int pos) => (short)((data[pos] << 8) | data[pos + 1]);
        private uint U32(int pos) => ReadU32(data, pos);
        private double F2Dot14(int pos) => S16(pos) / 16384.0;
        private static uint ReadU32(byte[] d, int pos) => (uint)((d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3]);
    }
}
