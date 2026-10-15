using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CADability
{
    /// <summary>
    /// An axis of a variable font ("fvar"), e.g. the weight "wght" from 200 to 900 with the default 400.
    /// </summary>
    internal struct VariationAxis
    {
        public string Tag;
        public double Min, Default, Max;
    }
    /// <summary>
    /// A named instance of a variable font ("fvar"), e.g. "Light Condensed" with its coordinates in user units.
    /// </summary>
    internal struct NamedInstance
    {
        public int SubfamilyNameId;
        public string SubfamilyName;
        public double[] Coordinates;
    }

    /// <summary>
    /// The variation data of a variable OpenType font: the axes and named instances ("fvar"), the axis mapping ("avar"), the
    /// glyph variations of TrueType outlines ("gvar", with the interpolation of untouched points), the advance width variations
    /// ("HVAR"), the variations of the font wide metrics ("MVAR"), the item variation store that CFF2 outlines use for their
    /// blend operator and the names of the instances from the style attributes ("STAT").
    /// </summary>
    internal sealed class FontVariations
    {
        private readonly byte[] data;
        public VariationAxis[] Axes { get; }
        public NamedInstance[] Instances { get; }
        private readonly List<(double from, double to)>[] avar; // per axis, null without avar
        private readonly int gvarOffset = -1, gvarArray, gvarGlyphCount, sharedTuples, sharedTupleCount;
        private readonly bool gvarLongOffsets;
        private readonly ItemVariationStore hvarStore;
        private readonly int hvarAdvanceMap = -1;
        private readonly ItemVariationStore mvarStore;
        private readonly Dictionary<string, (int outer, int inner)> mvarRecords = new Dictionary<string, (int, int)>();
        private readonly int statOffset = -1;
        private readonly Func<int, string> nameOf;

        /// <param name="data">the font file</param>
        /// <param name="tables">offset and length of the tables of the font</param>
        /// <param name="nameOf">the name for a name ID</param>
        public FontVariations(byte[] data, Dictionary<string, (int offset, int length)> tables, Func<int, string> nameOf)
        {
            this.data = data;
            this.nameOf = nameOf;
            var fvar = tables["fvar"];
            int f = fvar.offset;
            int axesOffset = f + U16(f + 4), axisCount = U16(f + 8), axisSize = U16(f + 10), instanceCount = U16(f + 12), instanceSize = U16(f + 14);
            if (axisCount == 0 || axisSize < 20 || instanceSize < 4 + 4 * axisCount) throw new InvalidDataException("invalid fvar table");
            Axes = new VariationAxis[axisCount];
            for (int i = 0; i < axisCount; ++i)
            {
                int a = axesOffset + i * axisSize;
                Axes[i] = new VariationAxis
                {
                    Tag = System.Text.Encoding.ASCII.GetString(data, a, 4),
                    Min = Fixed(a + 4),
                    Default = Fixed(a + 8),
                    Max = Fixed(a + 12),
                };
            }
            List<NamedInstance> instances = new List<NamedInstance>();
            int instancesOffset = axesOffset + axisCount * axisSize;
            for (int i = 0; i < instanceCount; ++i)
            {
                int p = instancesOffset + i * instanceSize;
                if (p + instanceSize > fvar.offset + fvar.length) break;
                double[] coords = new double[axisCount];
                for (int k = 0; k < axisCount; ++k) coords[k] = Fixed(p + 4 + 4 * k);
                int nameId = U16(p);
                instances.Add(new NamedInstance { SubfamilyNameId = nameId, SubfamilyName = nameOf(nameId) ?? "", Coordinates = coords });
            }
            Instances = instances.ToArray();
            if (tables.TryGetValue("avar", out var av) && U16(av.offset) == 1)
            {
                int count = U16(av.offset + 6);
                if (count == axisCount)
                {
                    avar = new List<(double, double)>[axisCount];
                    int p = av.offset + 8;
                    for (int i = 0; i < axisCount; ++i)
                    {
                        int maps = U16(p);
                        p += 2;
                        avar[i] = new List<(double, double)>();
                        for (int k = 0; k < maps; ++k, p += 4) avar[i].Add((F2Dot14(p), F2Dot14(p + 2)));
                    }
                }
            }
            if (tables.TryGetValue("gvar", out var gv))
            {
                int g = gv.offset;
                if (U16(g + 4) != axisCount) throw new InvalidDataException("gvar and fvar have a different number of axes");
                gvarOffset = g;
                sharedTupleCount = U16(g + 6);
                sharedTuples = g + (int)U32(g + 8);
                gvarGlyphCount = U16(g + 12);
                gvarLongOffsets = (U16(g + 14) & 1) != 0;
                gvarArray = g + (int)U32(g + 16);
            }
            if (tables.TryGetValue("HVAR", out var hv))
            {
                int h = hv.offset;
                hvarStore = new ItemVariationStore(data, h + (int)U32(h + 4), axisCount);
                uint map = U32(h + 8);
                if (map != 0) hvarAdvanceMap = h + (int)map;
            }
            if (tables.TryGetValue("MVAR", out var mv) && mv.length >= 12)
            {
                int m = mv.offset;
                int recordSize = U16(m + 6), recordCount = U16(m + 8), store = U16(m + 10);
                if (store != 0 && recordSize >= 8)
                {
                    mvarStore = new ItemVariationStore(data, m + store, axisCount);
                    for (int i = 0; i < recordCount; ++i)
                    {
                        int r = m + 12 + i * recordSize;
                        mvarRecords[System.Text.Encoding.ASCII.GetString(data, r, 4)] = (U16(r + 4), U16(r + 6));
                    }
                }
            }
            if (tables.TryGetValue("STAT", out var st) && st.length >= 18) statOffset = st.offset;
        }

        /// <summary>
        /// The change of a font wide metric from "MVAR" at the normalized coordinates, e.g. "strs" for the size of the strikeout
        /// line, 0 if the font does not vary it.
        /// </summary>
        public double GetMetricDelta(string tag, double[] coords)
        {
            if (mvarStore == null || coords == null || !mvarRecords.TryGetValue(tag, out var record)) return 0.0;
            return mvarStore.GetDelta(record.outer, record.inner, coords);
        }

        /// <summary>
        /// The name of an instance (coordinates in user units) as the style attributes of "STAT" make it, e.g. "Display Light"
        /// for wght 300 and opsz 36 of Segoe UI Variable: the names of the axis values of the instance in the axis ordering of
        /// "STAT", without the elidable ones like "Regular". This is how Windows names the instances, while the subfamily name
        /// in "fvar" may have another order ("Light Display"). Null if the font has no "STAT" table or an axis has no value
        /// for the instance.
        /// </summary>
        public string StatInstanceName(double[] userCoordinates)
        {
            if (statOffset < 0) return null;
            try
            {
                int t = statOffset;
                int designAxisSize = U16(t + 4), designAxisCount = U16(t + 6), designAxes = t + (int)U32(t + 8);
                int valueCount = U16(t + 12), valueOffsets = t + (int)U32(t + 14);
                // the coordinate of the instance on every design axis (NaN for an axis "fvar" does not have)
                double[] coordinate = new double[designAxisCount];
                int[] ordering = new int[designAxisCount];
                for (int i = 0; i < designAxisCount; ++i)
                {
                    int a = designAxes + i * designAxisSize;
                    string tag = System.Text.Encoding.ASCII.GetString(data, a, 4);
                    ordering[i] = U16(a + 6);
                    coordinate[i] = double.NaN;
                    for (int k = 0; k < Axes.Length; ++k) if (Axes[k].Tag == tag) coordinate[i] = userCoordinates[k];
                }
                bool Matches(int axis, double value) => axis < designAxisCount && Math.Abs(coordinate[axis] - value) < 1e-3;
                string[] names = new string[designAxisCount];
                bool[] resolved = new bool[designAxisCount];
                List<(int axis, int nameId, bool elidable)> single = new List<(int, int, bool)>();
                for (int v = 0; v < valueCount; ++v)
                {
                    int p = valueOffsets + U16(valueOffsets + 2 * v);
                    int format = U16(p);
                    if (format == 4)
                    {   // a combination of values on several axes, e.g. "Condensed Bold", which wins over the single values
                        int count = U16(p + 2), flags = U16(p + 4), nameId = U16(p + 6);
                        List<int> axes = new List<int>();
                        bool all = count > 0;
                        for (int k = 0; k < count; ++k)
                        {
                            int axis = U16(p + 8 + 6 * k);
                            if (!Matches(axis, Fixed(p + 10 + 6 * k))) all = false;
                            axes.Add(axis);
                        }
                        if (!all || axes.Any(axis => resolved[axis])) continue;
                        int first = axes.OrderBy(axis => ordering[axis]).First();
                        foreach (int axis in axes) resolved[axis] = true;
                        names[first] = (flags & 2) != 0 ? "" : nameOf(nameId);
                        continue;
                    }
                    if (format < 1 || format > 3) continue;
                    int ax = U16(p + 2), fl = U16(p + 4), id = U16(p + 6);
                    if (ax >= designAxisCount) continue;
                    bool match;
                    if (double.IsNaN(coordinate[ax])) match = false; // an axis without variation: see below
                    else if (format == 2) match = Fixed(p + 12) <= coordinate[ax] && coordinate[ax] <= Fixed(p + 16);
                    else match = Matches(ax, Fixed(p + 8));
                    if (match && !resolved[ax])
                    {
                        resolved[ax] = true;
                        names[ax] = (fl & 2) != 0 ? "" : nameOf(id);
                    }
                    single.Add((ax, id, (fl & 2) != 0));
                }
                for (int i = 0; i < designAxisCount; ++i)
                {
                    if (resolved[i]) continue;
                    if (!double.IsNaN(coordinate[i])) return null; // an axis of "fvar" without a value for this instance
                    // an axis the font does not vary, e.g. "ital" of an upright font: its only value, if it has exactly one
                    List<(int axis, int nameId, bool elidable)> values = single.Where(x => x.axis == i).ToList();
                    if (values.Count == 1 && !values[0].elidable) names[i] = nameOf(values[0].nameId);
                }
                for (int i = 0; i < designAxisCount; ++i) if (resolved[i] && names[i] == null) return null; // a name is missing
                IEnumerable<string> parts = Enumerable.Range(0, designAxisCount).OrderBy(i => ordering[i]).Select(i => names[i]).Where(n => !string.IsNullOrEmpty(n));
                string res = string.Join(" ", parts);
                return res.Length == 0 ? "Regular" : res;
            }
            catch (IndexOutOfRangeException)
            {
                return null; // a broken "STAT" table: the names of "fvar" are used
            }
        }

        /// <summary>
        /// The normalized coordinates (-1 to 1, with the "avar" mapping applied) for coordinates in user units, e.g. wght 300.
        /// </summary>
        public double[] Normalize(double[] userCoordinates)
        {
            double[] res = new double[Axes.Length];
            for (int i = 0; i < Axes.Length; ++i)
            {
                VariationAxis a = Axes[i];
                double v = userCoordinates != null && i < userCoordinates.Length ? userCoordinates[i] : a.Default;
                v = Math.Max(a.Min, Math.Min(a.Max, v));
                double n = 0.0;
                if (v < a.Default && a.Default > a.Min) n = (v - a.Default) / (a.Default - a.Min);
                else if (v > a.Default && a.Max > a.Default) n = (v - a.Default) / (a.Max - a.Default);
                if (avar != null) n = Map(avar[i], n);
                res[i] = n;
            }
            return res;
        }
        private static double Map(List<(double from, double to)> map, double v)
        {
            if (map.Count == 0) return v;
            if (v <= map[0].from) return v + map[0].to - map[0].from;
            for (int i = 1; i < map.Count; ++i)
            {
                if (v <= map[i].from)
                {
                    var (f0, t0) = map[i - 1];
                    var (f1, t1) = map[i];
                    if (f1 == f0) return t1;
                    return t0 + (v - f0) * (t1 - t0) / (f1 - f0);
                }
            }
            return v + map[map.Count - 1].to - map[map.Count - 1].from;
        }

        /// <summary>
        /// The change of the advance width of a glyph from "HVAR", or NaN if the font has no "HVAR" table.
        /// </summary>
        public double GetAdvanceDelta(int glyph, double[] coords)
        {
            if (hvarStore == null) return double.NaN;
            int outer = 0, inner = glyph;
            if (hvarAdvanceMap >= 0) (outer, inner) = DeltaSetIndex(hvarAdvanceMap, glyph);
            return hvarStore.GetDelta(outer, inner, coords);
        }
        /// <summary>
        /// The item variation store of a CFF2 table, which its blend operator uses.
        /// </summary>
        public ItemVariationStore ReadItemVariationStore(int offset)
        {
            return new ItemVariationStore(data, offset, Axes.Length);
        }

        /// <summary>
        /// Adds the "gvar" deltas of a glyph to <paramref name="dx"/> and <paramref name="dy"/>: one delta per point and four for the
        /// phantom points at the end. For a simple glyph <paramref name="contourEnds"/> are the last point indices of the contours
        /// and the untouched points of a contour are interpolated (IUP); a composite glyph has one point per component and no IUP.
        /// </summary>
        public void AddGlyphDeltas(int glyph, double[] x, double[] y, int[] contourEnds, double[] coords, double[] dx, double[] dy)
        {
            if (gvarOffset < 0 || glyph >= gvarGlyphCount) return;
            int start, end;
            if (gvarLongOffsets)
            {
                start = (int)U32(gvarOffset + 20 + 4 * glyph);
                end = (int)U32(gvarOffset + 20 + 4 * glyph + 4);
            }
            else
            {
                start = 2 * U16(gvarOffset + 20 + 2 * glyph);
                end = 2 * U16(gvarOffset + 20 + 2 * glyph + 2);
            }
            if (end <= start) return;
            int gvd = gvarArray + start;
            int tupleCount = U16(gvd), countFlags = tupleCount;
            tupleCount &= 0x0FFF;
            int pos = gvd + U16(gvd + 2); // the serialized data
            int header = gvd + 4;
            int pointCount = x.Length;
            int[] sharedPoints = null;
            if ((countFlags & 0x8000) != 0) sharedPoints = ReadPoints(ref pos);
            int axisCount = Axes.Length;
            double[] peak = new double[axisCount], startTuple = new double[axisCount], endTuple = new double[axisCount];
            for (int t = 0; t < tupleCount; ++t)
            {
                int size = U16(header), index = U16(header + 2);
                header += 4;
                if ((index & 0x8000) != 0)
                {
                    for (int a = 0; a < axisCount; ++a) peak[a] = F2Dot14(header + 2 * a);
                    header += 2 * axisCount;
                }
                else
                {
                    int shared = index & 0x0FFF;
                    if (shared >= sharedTupleCount) throw new InvalidDataException("invalid shared tuple index");
                    for (int a = 0; a < axisCount; ++a) peak[a] = F2Dot14(sharedTuples + 2 * (shared * axisCount + a));
                }
                bool intermediate = (index & 0x4000) != 0;
                if (intermediate)
                {
                    for (int a = 0; a < axisCount; ++a) startTuple[a] = F2Dot14(header + 2 * a);
                    for (int a = 0; a < axisCount; ++a) endTuple[a] = F2Dot14(header + 2 * (axisCount + a));
                    header += 4 * axisCount;
                }
                int tupleData = pos;
                pos += size;
                double scalar = Scalar(coords, peak, intermediate ? startTuple : null, intermediate ? endTuple : null);
                if (scalar == 0.0) continue;
                int p = tupleData;
                int[] points = (index & 0x2000) != 0 ? ReadPoints(ref p) : sharedPoints;
                int n = points == null ? pointCount : points.Length; // null: all points
                double[] deltaX = ReadDeltas(ref p, n), deltaY = ReadDeltas(ref p, n);
                if (points == null)
                {
                    for (int i = 0; i < pointCount; ++i)
                    {
                        dx[i] += scalar * deltaX[i];
                        dy[i] += scalar * deltaY[i];
                    }
                    continue;
                }
                double[] tx = new double[pointCount], ty = new double[pointCount];
                bool[] touched = new bool[pointCount];
                for (int i = 0; i < points.Length; ++i)
                {
                    int k = points[i];
                    if (k < 0 || k >= pointCount) continue;
                    tx[k] += deltaX[i];
                    ty[k] += deltaY[i];
                    touched[k] = true;
                }
                if (contourEnds != null) InterpolateUntouched(x, y, contourEnds, touched, tx, ty);
                for (int i = 0; i < pointCount; ++i)
                {
                    dx[i] += scalar * tx[i];
                    dy[i] += scalar * ty[i];
                }
            }
        }

        /// <summary>
        /// The interpolation of untouched points (IUP): every point of a contour without a delta of its own gets a delta from the
        /// two nearest touched points of its contour, separately for x and y.
        /// </summary>
        internal static void InterpolateUntouched(double[] x, double[] y, int[] contourEnds, bool[] touched, double[] dx, double[] dy)
        {
            int start = 0;
            foreach (int end in contourEnds)
            {
                List<int> refs = new List<int>();
                for (int i = start; i <= end; ++i) if (touched[i]) refs.Add(i);
                if (refs.Count > 0 && refs.Count <= end - start)
                {
                    for (int r = 0; r < refs.Count; ++r)
                    {
                        int r1 = refs[r], r2 = refs[(r + 1) % refs.Count];
                        // the untouched points after r1 up to r2 (cyclic in the contour)
                        for (int i = Next(r1, start, end); i != r2; i = Next(i, start, end))
                        {
                            dx[i] = Interpolate(x[i], x[r1], x[r2], dx[r1], dx[r2]);
                            dy[i] = Interpolate(y[i], y[r1], y[r2], dy[r1], dy[r2]);
                        }
                    }
                }
                start = end + 1;
            }
        }
        private static int Next(int i, int start, int end) => i == end ? start : i + 1;
        private static double Interpolate(double v, double c1, double c2, double d1, double d2)
        {
            if (c1 == c2) return d1 == d2 ? d1 : 0.0;
            if (c1 > c2)
            {
                (c1, c2) = (c2, c1);
                (d1, d2) = (d2, d1);
            }
            if (v <= c1) return d1;
            if (v >= c2) return d2;
            return d1 + (v - c1) * (d2 - d1) / (c2 - c1);
        }

        /// <summary>
        /// The factor of a variation tuple at the coordinates (all normalized), as fontTools' supportScalar computes it.
        /// </summary>
        internal static double Scalar(double[] coords, double[] peak, double[] start, double[] end)
        {
            double scalar = 1.0;
            for (int i = 0; i < peak.Length; ++i)
            {
                double p = peak[i];
                if (p == 0.0) continue;
                double s = start != null ? start[i] : Math.Min(p, 0.0), e = end != null ? end[i] : Math.Max(p, 0.0);
                if (s > p || p > e) continue;
                if (s < 0.0 && e > 0.0) continue;
                double v = i < coords.Length ? coords[i] : 0.0;
                if (v == p) continue;
                if (v <= s || e <= v) return 0.0;
                if (v < p) scalar *= (v - s) / (p - s);
                else scalar *= (e - v) / (e - p);
            }
            return scalar;
        }

        private int[] ReadPoints(ref int pos)
        {
            int count = data[pos++];
            if (count == 0) return null; // all points
            if ((count & 0x80) != 0) count = ((count & 0x7F) << 8) | data[pos++];
            int[] res = new int[count];
            int n = 0, last = 0;
            while (n < count)
            {
                int control = data[pos++];
                int run = (control & 0x7F) + 1;
                bool words = (control & 0x80) != 0;
                for (int i = 0; i < run && n < count; ++i)
                {
                    int d;
                    if (words) { d = U16(pos); pos += 2; }
                    else d = data[pos++];
                    last += d;
                    res[n++] = last;
                }
            }
            return res;
        }
        private double[] ReadDeltas(ref int pos, int count)
        {
            double[] res = new double[count];
            int n = 0;
            while (n < count)
            {
                int control = data[pos++];
                int run = (control & 0x3F) + 1;
                int kind = control & 0xC0;
                for (int i = 0; i < run && n < count; ++i)
                {
                    if (kind == 0x80) res[n++] = 0;
                    else if (kind == 0x40) { res[n++] = S16(pos); pos += 2; }
                    else if (kind == 0xC0) { res[n++] = (int)U32(pos); pos += 4; }
                    else res[n++] = (sbyte)data[pos++];
                }
            }
            return res;
        }
        private (int outer, int inner) DeltaSetIndex(int map, int index)
        {
            int format = data[map], entryFormat = data[map + 1];
            int count = format == 0 ? U16(map + 2) : (int)U32(map + 2);
            int entries = map + (format == 0 ? 4 : 6);
            if (count == 0) return (0, index);
            if (index >= count) index = count - 1;
            int size = ((entryFormat >> 4) & 3) + 1, innerBits = (entryFormat & 0xF) + 1;
            int entry = 0;
            for (int k = 0; k < size; ++k) entry = (entry << 8) | data[entries + index * size + k];
            return (entry >> innerBits, entry & ((1 << innerBits) - 1));
        }

        private int U16(int pos) => (data[pos] << 8) | data[pos + 1];
        private short S16(int pos) => (short)((data[pos] << 8) | data[pos + 1]);
        private uint U32(int pos) => (uint)((data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3]);
        private double Fixed(int pos) => (int)U32(pos) / 65536.0;
        private double F2Dot14(int pos) => S16(pos) / 16384.0;
    }

    /// <summary>
    /// An item variation store ("HVAR", "MVAR", "CFF2"): rows of deltas for the regions of the variation space.
    /// </summary>
    internal sealed class ItemVariationStore
    {
        private readonly double[][] regionStart, regionPeak, regionEnd; // [region][axis]
        private readonly int[][] regionIndexes; // per item variation data
        private readonly int[][][] deltas; // [data][item][region]

        public ItemVariationStore(byte[] data, int offset, int axisCount)
        {
            int U16(int p) => (data[p] << 8) | data[p + 1];
            int U32(int p) => (data[p] << 24) | (data[p + 1] << 16) | (data[p + 2] << 8) | data[p + 3];
            double F2Dot14(int p) => (short)U16(p) / 16384.0;
            int regionList = offset + U32(offset + 2);
            int dataCount = U16(offset + 6);
            int axes = U16(regionList), regionCount = U16(regionList + 2);
            if (axes != axisCount) throw new InvalidDataException("the item variation store has another number of axes");
            regionStart = new double[regionCount][];
            regionPeak = new double[regionCount][];
            regionEnd = new double[regionCount][];
            for (int r = 0; r < regionCount; ++r)
            {
                regionStart[r] = new double[axes];
                regionPeak[r] = new double[axes];
                regionEnd[r] = new double[axes];
                for (int a = 0; a < axes; ++a)
                {
                    int p = regionList + 4 + 6 * (r * axes + a);
                    regionStart[r][a] = F2Dot14(p);
                    regionPeak[r][a] = F2Dot14(p + 2);
                    regionEnd[r][a] = F2Dot14(p + 4);
                }
            }
            regionIndexes = new int[dataCount][];
            deltas = new int[dataCount][][];
            for (int d = 0; d < dataCount; ++d)
            {
                int ivd = offset + U32(offset + 8 + 4 * d);
                int itemCount = U16(ivd), wordDeltaCount = U16(ivd + 2), regionIndexCount = U16(ivd + 4);
                bool longWords = (wordDeltaCount & 0x8000) != 0;
                int words = wordDeltaCount & 0x7FFF;
                regionIndexes[d] = new int[regionIndexCount];
                for (int k = 0; k < regionIndexCount; ++k)
                {
                    regionIndexes[d][k] = U16(ivd + 6 + 2 * k);
                    if (regionIndexes[d][k] >= regionCount) throw new InvalidDataException("invalid region index");
                }
                int p = ivd + 6 + 2 * regionIndexCount;
                deltas[d] = new int[itemCount][];
                for (int i = 0; i < itemCount; ++i)
                {
                    int[] row = new int[regionIndexCount];
                    for (int k = 0; k < regionIndexCount; ++k)
                    {
                        if (k < words)
                        {
                            if (longWords) { row[k] = U32(p); p += 4; }
                            else { row[k] = (short)U16(p); p += 2; }
                        }
                        else
                        {
                            if (longWords) { row[k] = (short)U16(p); p += 2; }
                            else { row[k] = (sbyte)data[p]; p += 1; }
                        }
                    }
                    deltas[d][i] = row;
                }
            }
        }
        /// <summary>
        /// The number of item variation data subtables.
        /// </summary>
        public int DataCount => deltas.Length;
        /// <summary>
        /// The factors of the regions of the item variation data <paramref name="outer"/> at the coordinates.
        /// </summary>
        public double[] GetScalars(int outer, double[] coords)
        {
            if (outer < 0 || outer >= regionIndexes.Length) throw new InvalidDataException("invalid item variation data index");
            int[] regions = regionIndexes[outer];
            double[] res = new double[regions.Length];
            for (int k = 0; k < regions.Length; ++k)
            {
                int r = regions[k];
                res[k] = FontVariations.Scalar(coords, regionPeak[r], regionStart[r], regionEnd[r]);
            }
            return res;
        }
        /// <summary>
        /// The delta of the item (<paramref name="outer"/>, <paramref name="inner"/>) at the coordinates.
        /// </summary>
        public double GetDelta(int outer, int inner, double[] coords)
        {
            if (outer < 0 || outer >= deltas.Length || inner < 0 || inner >= deltas[outer].Length) return 0.0;
            double[] scalars = GetScalars(outer, coords);
            int[] row = deltas[outer][inner];
            double res = 0.0;
            for (int k = 0; k < row.Length; ++k) res += row[k] * scalars[k];
            return res;
        }
    }
}
