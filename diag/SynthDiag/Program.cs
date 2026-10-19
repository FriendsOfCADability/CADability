// Diagnostics for synthetic bold and italic (not meant to be merged): what GDI+ and GDI do for a font family that has no
// bold or italic face, measured on the Windows runner.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using CADability;

static class Program
{
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [StructLayout(LayoutKind.Sequential)] struct GLYPHMETRICS { public uint bx, by; public int ox, oy; public short cx, cy; }
    [StructLayout(LayoutKind.Sequential)] struct MAT2 { public int m11, m12, m21, m22; }
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern uint GetGlyphOutlineW(IntPtr hdc, uint ch, uint format, out GLYPHMETRICS gm, uint size, byte[] buffer, ref MAT2 mat);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string faceName);

    static readonly ManagedFontProvider managed = new ManagedFontProvider();

    static void Main()
    {
        List<string> noBold = new List<string>(), noItalic = new List<string>();
        using (InstalledFontCollection fonts = new InstalledFontCollection())
        {
            foreach (FontFamily ff in fonts.Families)
            {
                FontFace regular = managed.GetFace(ff.Name, false, false);
                if (regular == null) continue;
                FontFace bold = managed.GetFace(ff.Name, true, false), italic = managed.GetFace(ff.Name, false, true);
                bool mb = bold != null && bold.IsBold, mi = italic != null && italic.IsItalic;
                if (mb && mi) continue;
                Console.WriteLine($"{ff.Name}: managed bold {mb} italic {mi}; GDI+ IsStyleAvailable regular {ff.IsStyleAvailable(FontStyle.Regular)} bold {ff.IsStyleAvailable(FontStyle.Bold)} italic {ff.IsStyleAvailable(FontStyle.Italic)} bolditalic {ff.IsStyleAvailable(FontStyle.Bold | FontStyle.Italic)}");
                if (!mb && ff.IsStyleAvailable(FontStyle.Regular) && !regular.IsBold) noBold.Add(ff.Name);
                if (!mi && ff.IsStyleAvailable(FontStyle.Regular) && !regular.IsItalic) noItalic.Add(ff.Name);
            }
        }
        Console.WriteLine();
        string[] plain = { "Microsoft Sans Serif", "Lucida Console", "Gabriola", "Ink Free", "Lucida Sans Unicode", "Impact", "Javanese Text", "Segoe UI Historic" };
        foreach (string fam in plain) { if (noBold.Contains(fam)) Bold(fam); else Console.WriteLine($"{fam}: has bold or is missing"); }
        Measure("Microsoft Sans Serif", false, true);
    }

    // what GDI+ AddString does for synthetic bold, at the em size 1 of the old kernel code and at the em size em
    static void Bold(string family)
    {
        using FontFamily ff = new FontFamily(family);
        int em = ff.GetEmHeight(FontStyle.Regular), asc = ff.GetCellAscent(FontStyle.Regular);
        Console.WriteLine($"=== {family} bold, em {em} ascent {asc}");
        foreach (float size in new[] { 1.0f, (float)em })
        {
            foreach (char c in "Il")
            {
                var r = GdiPlusAt(ff, FontStyle.Regular, c, size).Select(p => (p.x * em / size, p.y * em / size)).ToList();
                var b = GdiPlusAt(ff, FontStyle.Bold, c, size).Select(p => (p.x * em / size, p.y * em / size)).ToList();
                string pts(List<(double x, double y)> l) => string.Join(" ", l.Take(14).Select(p => $"({p.x:F1},{p.y:F1})"));
                Console.WriteLine($"  size {size} '{c}' regular n {r.Count}: {pts(r)}");
                Console.WriteLine($"  size {size} '{c}' bold    n {b.Count}: {pts(b)}");
            }
        }
        // the advance width the old kernel took: GetCharABCWidths with the bold GDI+ font selected (em height as pixel size)
        foreach (char c in "Ilo")
        {
            int wr = GdiAdvance(ff, FontStyle.Regular, em, c), wb = GdiAdvance(ff, FontStyle.Bold, em, c);
            Console.WriteLine($"  advance '{c}' regular {wr} bold {wb}");
        }
        foreach (char c in "Ho")
        {
            var r = GdiOutline(family, em, 400, false, c, out GLYPHMETRICS gr);
            var s = GdiOutline(family, em, 700, false, c, out GLYPHMETRICS gs);
            Console.WriteLine($"  GDI '{c}' adv {gr.cx}->{gs.cx} box x {gr.ox}..{gr.ox + gr.bx} -> {gs.ox}..{gs.ox + gs.bx} y {gr.oy - (int)gr.by}..{gr.oy} -> {gs.oy - (int)gs.by}..{gs.oy} points {r.Count}/{s.Count}");
        }
    }
    static List<(double x, double y)> GdiPlusAt(FontFamily ff, FontStyle style, char c, float size)
    {
        using GraphicsPath path = new GraphicsPath();
        using StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone();
        path.AddString(c.ToString(), ff, (int)style, size, new PointF(0, 0), sf);
        if (path.PointCount == 0) return new List<(double, double)>();
        return path.PathPoints.Select(p => ((double)p.X, (double)p.Y)).ToList();
    }
    [StructLayout(LayoutKind.Sequential)] struct ABC { public int a; public uint b; public int c; }
    [DllImport("gdi32.dll", EntryPoint = "GetCharABCWidthsW")] static extern bool GetCharABCWidths(IntPtr hdc, uint first, uint last, [Out] ABC[] abc);
    static int GdiAdvance(FontFamily ff, FontStyle style, int em, char c)
    {
        IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
        using Font font = new Font(ff, em, style, GraphicsUnit.Pixel);
        IntPtr hf = font.ToHfont(), old = SelectObject(hdc, hf);
        try { ABC[] abc = new ABC[1]; GetCharABCWidths(hdc, c, c, abc); return abc[0].a + (int)abc[0].b + abc[0].c; }
        finally { SelectObject(hdc, old); DeleteObject(hf); DeleteDC(hdc); }
    }

    static void Measure(string family, bool bold, bool italic)
    {
        Console.WriteLine($"=== {family} synthetic {(bold ? "bold" : "")}{(italic ? "italic" : "")}");
        using FontFamily ff = new FontFamily(family);
        int em = ff.GetEmHeight(FontStyle.Regular);
        FontStyle style = (bold ? FontStyle.Bold : 0) | (italic ? FontStyle.Italic : 0);
        foreach (char c in "HIlo")
        {
            // GDI, unhinted outline in font units (y up, origin on the baseline)
            var r = GdiOutline(family, em, 400, false, c, out GLYPHMETRICS gr);
            var s = GdiOutline(family, em, bold ? 700 : 400, italic, c, out GLYPHMETRICS gs);
            string gdi = $"GDI adv {gr.cx}->{gs.cx} box x {gr.ox}..{gr.ox + gr.bx} -> {gs.ox}..{gs.ox + gs.bx} y {gr.oy - (int)gr.by}..{gr.oy} -> {gs.oy - (int)gs.by}..{gs.oy}";
            if (r.Count == s.Count && r.Count > 0) gdi += " " + Fit(r, s);
            else gdi += $" points {r.Count}/{s.Count}";
            // GDI+, AddString in units of the em height (y down, top of the cell at 0)
            var pr = GdiPlus(ff, FontStyle.Regular, c, em);
            var ps = GdiPlus(ff, style, c, em);
            string gdip = pr.Count == ps.Count && pr.Count > 0 ? Fit(pr.Select(p => (p.x, -p.y)).ToList(), ps.Select(p => (p.x, -p.y)).ToList()) : $"points {pr.Count}/{ps.Count}";
            string box(List<(double x, double y)> l) => l.Count == 0 ? "-" : $"x {l.Min(p => p.x):F0}..{l.Max(p => p.x):F0} y {l.Min(p => p.y):F0}..{l.Max(p => p.y):F0}";
            Console.WriteLine($"  '{c}' {gdi}");
            Console.WriteLine($"      GDI+ {box(pr)} -> {box(ps)} {gdip}");
        }
    }

    // least squares fit of the synthetic outline against the regular one: dx = a + k * y (a shear), dy = b
    static string Fit(List<(double x, double y)> r, List<(double x, double y)> s)
    {
        int n = r.Count;
        double sy = 0, syy = 0, sdx = 0, sydx = 0, sdy = 0;
        for (int i = 0; i < n; ++i) { double y = r[i].y, dx = s[i].x - r[i].x; sy += y; syy += y * y; sdx += dx; sydx += y * dx; sdy += s[i].y - r[i].y; }
        double k = (n * sydx - sy * sdx) / (n * syy - sy * sy), a = (sdx - k * sy) / n, b = sdy / n;
        double res = 0, resy = 0;
        for (int i = 0; i < n; ++i) { res = Math.Max(res, Math.Abs(s[i].x - r[i].x - (a + k * r[i].y))); resy = Math.Max(resy, Math.Abs(s[i].y - r[i].y - b)); }
        return $"shear k {k:F4} ({Math.Atan(k) * 180 / Math.PI:F2} deg) dx0 {a:F1} dy {b:F1} max residual x {res:F1} y {resy:F1} (n {n})";
    }

    static List<(double x, double y)> GdiPlus(FontFamily ff, FontStyle style, char c, int em)
    {
        using GraphicsPath path = new GraphicsPath();
        using StringFormat sf = (StringFormat)StringFormat.GenericTypographic.Clone();
        path.AddString(c.ToString(), ff, (int)style, em, new PointF(0, 0), sf);
        if (path.PointCount == 0) return new List<(double, double)>();
        return path.PathPoints.Select(p => ((double)p.X, (double)p.Y)).ToList();
    }

    static double Fixed(byte[] b, int o) => BitConverter.ToInt16(b, o + 2) + BitConverter.ToUInt16(b, o) / 65536.0;
    static List<(double x, double y)> GdiOutline(string family, int em, int weight, bool italic, char c, out GLYPHMETRICS gm)
    {
        IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
        IntPtr hf = CreateFontW(-em, 0, 0, 0, weight, italic ? 1u : 0u, 0, 0, 1, 0, 0, 0, 0, family);
        IntPtr old = SelectObject(hdc, hf);
        try
        {
            MAT2 m = new MAT2 { m11 = 0x10000, m22 = 0x10000 };
            uint fmt = 2 | 0x100u; // GGO_NATIVE | GGO_UNHINTED: the control points
            uint n = GetGlyphOutlineW(hdc, c, fmt, out gm, 0, null, ref m);
            var res = new List<(double, double)>();
            if (n == 0 || n == 0xFFFFFFFF) return res;
            byte[] b = new byte[n];
            GetGlyphOutlineW(hdc, c, fmt, out gm, n, b, ref m);
            int pos = 0;
            while (pos < n)
            {
                int cb = BitConverter.ToInt32(b, pos);
                res.Add((Fixed(b, pos + 8), Fixed(b, pos + 12)));
                int p = pos + 16;
                while (p < pos + cb)
                {
                    int count = BitConverter.ToUInt16(b, p + 2);
                    for (int k = 0; k < count; ++k) res.Add((Fixed(b, p + 4 + 8 * k), Fixed(b, p + 8 + 8 * k)));
                    p += 4 + 8 * count;
                }
                pos += cb;
            }
            return res;
        }
        finally { SelectObject(hdc, old); DeleteObject(hf); DeleteDC(hdc); }
    }
}
