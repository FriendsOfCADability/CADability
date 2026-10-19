using System;
using System.Collections.Generic;
using System.IO;

namespace CADability
{
    /// <summary>
    /// Receives the outline of a glyph in font units: contours of lines and cubic Bezier segments.
    /// </summary>
    internal interface IGlyphOutlineSink
    {
        void MoveTo(double x, double y);
        void LineTo(double x, double y);
        void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3);
        void ClosePath();
    }

    /// <summary>
    /// Reads the glyph outlines of the "CFF " table of an OpenType font: the Compact Font Format with Type 2 charstrings, as used
    /// by many .otf files. Name-keyed and CID-keyed fonts (FDArray, FDSelect) are supported, with local and global subroutines,
    /// hint masks and the flex operators. Hints are skipped, they do not change the outline. Not supported: CFF2 (variable fonts)
    /// and the deprecated accent composition of endchar (seac), which modern fonts do not use.
    /// </summary>
    internal sealed class CffOutlines
    {
        private readonly byte[] data;
        private readonly List<(int start, int length)> charStrings;
        private readonly List<(int start, int length)> globalSubrs;
        private readonly List<List<(int start, int length)>> localSubrs = new List<List<(int start, int length)>>(); // per font dict
        private readonly byte[] fdSelect; // font dict index per glyph, null for a name-keyed font
        private readonly double scale; // FontMatrix * units per em, 1 in almost all fonts

        /// <summary>
        /// The number of glyphs (charstrings).
        /// </summary>
        public int GlyphCount => charStrings.Count;
        /// <summary>
        /// True for a CID-keyed font.
        /// </summary>
        public bool IsCid => fdSelect != null;

        /// <param name="data">the font file</param>
        /// <param name="offset">the start of the "CFF " table</param>
        /// <param name="length">its length</param>
        /// <param name="unitsPerEm">from the "head" table, to check the FontMatrix</param>
        public CffOutlines(byte[] data, int offset, int length, int unitsPerEm)
        {
            this.data = data;
            int end = offset + length;
            if (length < 4 || data[offset] != 1) throw new InvalidDataException("not a CFF table (CFF2 is not supported)");
            int pos = offset + data[offset + 2]; // header size
            ReadIndex(ref pos, end); // names
            List<(int start, int length)> topDicts = ReadIndex(ref pos, end);
            ReadIndex(ref pos, end); // strings
            globalSubrs = ReadIndex(ref pos, end);
            if (topDicts.Count == 0) throw new InvalidDataException("CFF table without a font");
            Dictionary<int, double[]> top = ReadDict(topDicts[0].start, topDicts[0].length);
            if (!top.TryGetValue(17, out double[] charStringsOffset)) throw new InvalidDataException("CFF font without charstrings");
            if (top.TryGetValue(1206, out double[] charstringType) && charstringType[0] != 2) throw new InvalidDataException("only Type 2 charstrings are supported");
            int csPos = offset + (int)charStringsOffset[0];
            charStrings = ReadIndex(ref csPos, end);
            scale = 1.0;
            if (top.TryGetValue(1207, out double[] fontMatrix) && fontMatrix.Length >= 4 && fontMatrix[0] != 0)
            {
                // the FontMatrix is stored rounded (e.g. 0.00048828 for 1/2048): a factor this close to 1 is 1
                double s = fontMatrix[0] * unitsPerEm;
                if (Math.Abs(s - 1.0) > 1e-3) scale = s;
            }
            if (top.TryGetValue(1236, out double[] fdArrayOffset) && top.TryGetValue(1237, out double[] fdSelectOffset))
            {   // CID-keyed: one private dict (with its own local subroutines) per font dict
                int fdPos = offset + (int)fdArrayOffset[0];
                foreach (var fd in ReadIndex(ref fdPos, end)) localSubrs.Add(ReadLocalSubrs(offset, ReadDict(fd.start, fd.length), end));
                fdSelect = ReadFdSelect(offset + (int)fdSelectOffset[0], charStrings.Count, end);
            }
            else localSubrs.Add(ReadLocalSubrs(offset, top, end));
        }

        /// <summary>
        /// Sends the outline of <paramref name="glyph"/> to <paramref name="sink"/>.
        /// </summary>
        public void Draw(int glyph, IGlyphOutlineSink sink)
        {
            if (glyph < 0 || glyph >= charStrings.Count) return;
            int fd = 0;
            if (fdSelect != null)
            {
                fd = fdSelect[glyph];
                if (fd >= localSubrs.Count) throw new InvalidDataException("invalid FDSelect");
            }
            new Interpreter(this, localSubrs[fd], scale == 1.0 ? sink : new ScalingSink(sink, scale)).Run(charStrings[glyph]);
        }

        private List<(int start, int length)> ReadLocalSubrs(int cffOffset, Dictionary<int, double[]> dict, int end)
        {
            if (!dict.TryGetValue(18, out double[] priv) || priv.Length < 2) return new List<(int, int)>();
            int privSize = (int)priv[0], privOffset = cffOffset + (int)priv[1];
            if (privSize <= 0) return new List<(int, int)>();
            Dictionary<int, double[]> privateDict = ReadDict(privOffset, privSize);
            if (!privateDict.TryGetValue(19, out double[] subrs)) return new List<(int, int)>();
            int pos = privOffset + (int)subrs[0]; // relative to the private dict
            return ReadIndex(ref pos, end);
        }
        private byte[] ReadFdSelect(int pos, int numGlyphs, int end)
        {
            byte[] res = new byte[numGlyphs];
            int format = data[pos];
            if (format == 0)
            {
                if (pos + 1 + numGlyphs > end) throw new InvalidDataException("invalid FDSelect");
                Array.Copy(data, pos + 1, res, 0, numGlyphs);
            }
            else if (format == 3)
            {
                int nRanges = U16(pos + 1);
                for (int i = 0; i < nRanges; ++i)
                {
                    int r = pos + 3 + 3 * i;
                    int first = U16(r), fd = data[r + 2], next = U16(r + 3);
                    for (int g = first; g < next && g < numGlyphs; ++g) res[g] = (byte)fd;
                }
            }
            else throw new InvalidDataException("unsupported FDSelect format " + format);
            return res;
        }
        private List<(int start, int length)> ReadIndex(ref int pos, int end)
        {
            List<(int, int)> res = new List<(int, int)>();
            if (pos + 2 > end) throw new InvalidDataException("CFF INDEX exceeds the table");
            int count = U16(pos);
            if (count == 0)
            {
                pos += 2;
                return res;
            }
            int offSize = data[pos + 2];
            if (offSize < 1 || offSize > 4) throw new InvalidDataException("invalid CFF INDEX");
            int offsets = pos + 3;
            int dataStart = offsets + (count + 1) * offSize - 1; // offsets count from 1
            int Offset(int i)
            {
                int v = 0;
                for (int k = 0; k < offSize; ++k) v = (v << 8) | data[offsets + i * offSize + k];
                return v;
            }
            for (int i = 0; i < count; ++i)
            {
                int s = Offset(i), e = Offset(i + 1);
                if (e < s || dataStart + e > end) throw new InvalidDataException("invalid CFF INDEX");
                res.Add((dataStart + s, e - s));
            }
            pos = dataStart + Offset(count);
            return res;
        }
        /// <summary>
        /// Reads a DICT: operator (two-byte operators as 1200 + second byte) to its operands.
        /// </summary>
        private Dictionary<int, double[]> ReadDict(int pos, int length)
        {
            Dictionary<int, double[]> res = new Dictionary<int, double[]>();
            List<double> operands = new List<double>();
            int end = pos + length;
            if (end > data.Length) throw new InvalidDataException("CFF DICT exceeds the file");
            while (pos < end)
            {
                int b0 = data[pos++];
                if (b0 <= 21)
                {
                    int op = b0;
                    if (b0 == 12) op = 1200 + data[pos++];
                    res[op] = operands.ToArray();
                    operands.Clear();
                }
                else if (b0 == 28) { operands.Add((short)((data[pos] << 8) | data[pos + 1])); pos += 2; }
                else if (b0 == 29) { operands.Add((data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3]); pos += 4; }
                else if (b0 == 30) operands.Add(ReadReal(ref pos));
                else if (b0 >= 32 && b0 <= 246) operands.Add(b0 - 139);
                else if (b0 >= 247 && b0 <= 250) operands.Add((b0 - 247) * 256 + data[pos++] + 108);
                else if (b0 >= 251 && b0 <= 254) operands.Add(-(b0 - 251) * 256 - data[pos++] - 108);
                else throw new InvalidDataException("invalid CFF DICT");
            }
            return res;
        }
        private double ReadReal(ref int pos)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            while (true)
            {
                int b = data[pos++];
                foreach (int nibble in new[] { b >> 4, b & 0xF })
                {
                    if (nibble <= 9) sb.Append((char)('0' + nibble));
                    else if (nibble == 0xA) sb.Append('.');
                    else if (nibble == 0xB) sb.Append('E');
                    else if (nibble == 0xC) sb.Append("E-");
                    else if (nibble == 0xE) sb.Append('-');
                    else if (nibble == 0xF)
                    {
                        double.TryParse(sb.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v);
                        return v;
                    }
                }
            }
        }
        private int U16(int pos) => (data[pos] << 8) | data[pos + 1];
        private static int Bias(int count) => count < 1240 ? 107 : count < 33900 ? 1131 : 32768;

        private sealed class ScalingSink : IGlyphOutlineSink
        {
            private readonly IGlyphOutlineSink sink;
            private readonly double s;
            public ScalingSink(IGlyphOutlineSink sink, double s) { this.sink = sink; this.s = s; }
            public void MoveTo(double x, double y) => sink.MoveTo(x * s, y * s);
            public void LineTo(double x, double y) => sink.LineTo(x * s, y * s);
            public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3) => sink.CurveTo(x1 * s, y1 * s, x2 * s, y2 * s, x3 * s, y3 * s);
            public void ClosePath() => sink.ClosePath();
        }

        /// <summary>
        /// The Type 2 charstring interpreter (Adobe Technical Note #5177).
        /// </summary>
        private sealed class Interpreter
        {
            private readonly CffOutlines font;
            private readonly List<(int start, int length)> local;
            private readonly IGlyphOutlineSink sink;
            private readonly List<double> stack = new List<double>(48);
            private readonly double[] transient = new double[32];
            private double x, y;
            private int stems;
            private bool haveWidth, open, finished;
            private int depth;
            private readonly Random random = new Random(0);

            public Interpreter(CffOutlines font, List<(int start, int length)> local, IGlyphOutlineSink sink)
            {
                this.font = font;
                this.local = local;
                this.sink = sink;
            }
            public void Run((int start, int length) charString)
            {
                Execute(charString.start, charString.start + charString.length);
                if (open) sink.ClosePath();
                open = false;
            }

            private void Execute(int pos, int end)
            {
                if (++depth > 12) throw new InvalidDataException("CFF subroutines nested too deeply");
                byte[] d = font.data;
                while (pos < end && !finished)
                {
                    int b0 = d[pos++];
                    if (b0 >= 32 || b0 == 28)
                    {   // an operand
                        if (b0 == 28) { stack.Add((short)((d[pos] << 8) | d[pos + 1])); pos += 2; }
                        else if (b0 <= 246) stack.Add(b0 - 139);
                        else if (b0 <= 250) stack.Add((b0 - 247) * 256 + d[pos++] + 108);
                        else if (b0 <= 254) stack.Add(-(b0 - 251) * 256 - d[pos++] - 108);
                        else { stack.Add(((d[pos] << 24) | (d[pos + 1] << 16) | (d[pos + 2] << 8) | d[pos + 3]) / 65536.0); pos += 4; }
                        continue;
                    }
                    switch (b0)
                    {
                        case 1: // hstem
                        case 3: // vstem
                        case 18: // hstemhm
                        case 23: // vstemhm
                            Stems();
                            break;
                        case 19: // hintmask
                        case 20: // cntrmask
                            Stems(); // the operands of an implicit vstemhm
                            pos += (stems + 7) / 8;
                            break;
                        case 21: // rmoveto
                            Width(2);
                            MoveTo(x + Arg(0), y + Arg(1));
                            stack.Clear();
                            break;
                        case 22: // hmoveto
                            Width(1);
                            MoveTo(x + Arg(0), y);
                            stack.Clear();
                            break;
                        case 4: // vmoveto
                            Width(1);
                            MoveTo(x, y + Arg(0));
                            stack.Clear();
                            break;
                        case 5: // rlineto
                            for (int i = 0; i + 1 < stack.Count; i += 2) LineTo(x + stack[i], y + stack[i + 1]);
                            stack.Clear();
                            break;
                        case 6: // hlineto
                        case 7: // vlineto
                            {
                                bool horizontal = b0 == 6;
                                for (int i = 0; i < stack.Count; ++i, horizontal = !horizontal)
                                {
                                    if (horizontal) LineTo(x + stack[i], y);
                                    else LineTo(x, y + stack[i]);
                                }
                                stack.Clear();
                                break;
                            }
                        case 8: // rrcurveto
                            for (int i = 0; i + 5 < stack.Count; i += 6) Curve(stack[i], stack[i + 1], stack[i + 2], stack[i + 3], stack[i + 4], stack[i + 5]);
                            stack.Clear();
                            break;
                        case 24: // rcurveline
                            {
                                int i = 0;
                                for (; i + 7 < stack.Count; i += 6) Curve(stack[i], stack[i + 1], stack[i + 2], stack[i + 3], stack[i + 4], stack[i + 5]);
                                if (i + 1 < stack.Count) LineTo(x + stack[i], y + stack[i + 1]);
                                stack.Clear();
                                break;
                            }
                        case 25: // rlinecurve
                            {
                                int i = 0;
                                for (; i + 7 < stack.Count; i += 2) LineTo(x + stack[i], y + stack[i + 1]);
                                if (i + 5 < stack.Count) Curve(stack[i], stack[i + 1], stack[i + 2], stack[i + 3], stack[i + 4], stack[i + 5]);
                                stack.Clear();
                                break;
                            }
                        case 26: // vvcurveto
                            {
                                int i = 0;
                                double dx1 = 0;
                                if (stack.Count % 2 == 1) dx1 = stack[i++];
                                for (; i + 3 < stack.Count; i += 4)
                                {
                                    Curve(dx1, stack[i], stack[i + 1], stack[i + 2], 0, stack[i + 3]);
                                    dx1 = 0;
                                }
                                stack.Clear();
                                break;
                            }
                        case 27: // hhcurveto
                            {
                                int i = 0;
                                double dy1 = 0;
                                if (stack.Count % 2 == 1) dy1 = stack[i++];
                                for (; i + 3 < stack.Count; i += 4)
                                {
                                    Curve(stack[i], dy1, stack[i + 1], stack[i + 2], stack[i + 3], 0);
                                    dy1 = 0;
                                }
                                stack.Clear();
                                break;
                            }
                        case 30: // vhcurveto
                        case 31: // hvcurveto
                            {
                                bool horizontal = b0 == 31;
                                int i = 0;
                                while (i + 3 < stack.Count)
                                {
                                    double extra = stack.Count - i == 5 ? stack[i + 4] : 0; // the last curve may have a fifth operand
                                    if (horizontal) Curve(stack[i], 0, stack[i + 1], stack[i + 2], extra, stack[i + 3]);
                                    else Curve(0, stack[i], stack[i + 1], stack[i + 2], stack[i + 3], extra);
                                    horizontal = !horizontal;
                                    i += 4;
                                }
                                stack.Clear();
                                break;
                            }
                        case 10: // callsubr
                        case 29: // callgsubr
                            {
                                List<(int start, int length)> subrs = b0 == 10 ? local : font.globalSubrs;
                                int index = (int)Pop() + Bias(subrs.Count);
                                if (index < 0 || index >= subrs.Count) throw new InvalidDataException("invalid CFF subroutine index");
                                Execute(subrs[index].start, subrs[index].start + subrs[index].length);
                                break;
                            }
                        case 11: // return
                            --depth;
                            return;
                        case 14: // endchar (the accent composition "seac" with four operands is not supported)
                            if (!haveWidth && (stack.Count == 1 || stack.Count == 5)) stack.RemoveAt(0);
                            haveWidth = true;
                            if (open) sink.ClosePath();
                            open = false;
                            finished = true;
                            stack.Clear();
                            break;
                        case 12:
                            Escape(d[pos++]);
                            break;
                        default: // reserved operators are ignored
                            stack.Clear();
                            break;
                    }
                }
                --depth;
            }

            private void Escape(int op)
            {
                switch (op)
                {
                    case 35: // flex
                        Curve(Arg(0), Arg(1), Arg(2), Arg(3), Arg(4), Arg(5));
                        Curve(Arg(6), Arg(7), Arg(8), Arg(9), Arg(10), Arg(11));
                        break;
                    case 34: // hflex
                        Curve(Arg(0), 0, Arg(1), Arg(2), Arg(3), 0);
                        Curve(Arg(4), 0, Arg(5), -Arg(2), Arg(6), 0);
                        break;
                    case 36: // hflex1
                        {
                            double dy1 = Arg(1), dy2 = Arg(3), dy5 = Arg(7);
                            Curve(Arg(0), dy1, Arg(2), dy2, Arg(4), 0);
                            Curve(Arg(5), 0, Arg(6), dy5, Arg(8), -(dy1 + dy2 + dy5));
                            break;
                        }
                    case 37: // flex1
                        {
                            double dx = 0, dy = 0;
                            for (int i = 0; i < 10; i += 2) { dx += Arg(i); dy += Arg(i + 1); }
                            Curve(Arg(0), Arg(1), Arg(2), Arg(3), Arg(4), Arg(5));
                            if (Math.Abs(dx) > Math.Abs(dy)) Curve(Arg(6), Arg(7), Arg(8), Arg(9), Arg(10), -dy);
                            else Curve(Arg(6), Arg(7), Arg(8), Arg(9), -dx, Arg(10));
                            break;
                        }
                    // arithmetic and storage operators, rarely used
                    case 3: { double b = Pop(), a = Pop(); stack.Add(a != 0 && b != 0 ? 1 : 0); return; } // and
                    case 4: { double b = Pop(), a = Pop(); stack.Add(a != 0 || b != 0 ? 1 : 0); return; } // or
                    case 5: stack.Add(Pop() == 0 ? 1 : 0); return; // not
                    case 9: stack.Add(Math.Abs(Pop())); return; // abs
                    case 10: { double b = Pop(), a = Pop(); stack.Add(a + b); return; } // add
                    case 11: { double b = Pop(), a = Pop(); stack.Add(a - b); return; } // sub
                    case 12: { double b = Pop(), a = Pop(); stack.Add(b == 0 ? 0 : a / b); return; } // div
                    case 14: stack.Add(-Pop()); return; // neg
                    case 15: { double b = Pop(), a = Pop(); stack.Add(a == b ? 1 : 0); return; } // eq
                    case 18: Pop(); return; // drop
                    case 20: { int i = (int)Pop(); double v = Pop(); if (i >= 0 && i < transient.Length) transient[i] = v; return; } // put
                    case 21: { int i = (int)Pop(); stack.Add(i >= 0 && i < transient.Length ? transient[i] : 0); return; } // get
                    case 22: { double v2 = Pop(), v1 = Pop(), s2 = Pop(), s1 = Pop(); stack.Add(v1 <= v2 ? s1 : s2); return; } // ifelse
                    case 23: stack.Add(random.NextDouble() + double.Epsilon); return; // random
                    case 24: { double b = Pop(), a = Pop(); stack.Add(a * b); return; } // mul
                    case 26: stack.Add(Math.Sqrt(Math.Abs(Pop()))); return; // sqrt
                    case 27: { double a = Pop(); stack.Add(a); stack.Add(a); return; } // dup
                    case 28: { double b = Pop(), a = Pop(); stack.Add(b); stack.Add(a); return; } // exch
                    case 29: { int i = (int)Pop(); stack.Add(i >= 0 && i < stack.Count ? stack[stack.Count - 1 - i] : stack.Count > 0 ? stack[stack.Count - 1] : 0); return; } // index
                    case 30: // roll
                        {
                            int j = (int)Pop(), n = (int)Pop();
                            if (n > 0 && n <= stack.Count)
                            {
                                double[] part = stack.GetRange(stack.Count - n, n).ToArray();
                                for (int i = 0; i < n; ++i) stack[stack.Count - n + ((i + j) % n + n) % n] = part[i];
                            }
                            return;
                        }
                }
                stack.Clear();
            }

            private void Stems()
            {
                if (!haveWidth && stack.Count % 2 == 1) stack.RemoveAt(0);
                haveWidth = true;
                stems += stack.Count / 2;
                stack.Clear();
            }
            private void Width(int expected)
            {
                if (!haveWidth && stack.Count > expected) stack.RemoveAt(0);
                haveWidth = true;
            }
            private double Arg(int i) => i < stack.Count ? stack[i] : 0;
            private double Pop()
            {
                if (stack.Count == 0) return 0;
                double v = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                return v;
            }
            private void MoveTo(double nx, double ny)
            {
                if (open) sink.ClosePath();
                x = nx;
                y = ny;
                sink.MoveTo(x, y);
                open = true;
            }
            private void LineTo(double nx, double ny)
            {
                x = nx;
                y = ny;
                sink.LineTo(x, y);
            }
            private void Curve(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
            {
                double x1 = x + dx1, y1 = y + dy1, x2 = x1 + dx2, y2 = y1 + dy2;
                x = x2 + dx3;
                y = y2 + dy3;
                sink.CurveTo(x1, y1, x2, y2, x, y);
            }
        }
    }
}
