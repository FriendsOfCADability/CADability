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
        foreach (string fam in noItalic.Take(8)) Measure(fam, false, true);
        foreach (string fam in noBold.Take(8)) Measure(fam, true, false);
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
