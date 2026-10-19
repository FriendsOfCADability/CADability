#if WINDOWS
using CADability.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Text;

namespace CADability.Tests
{
    /// <summary>
    /// The synthetic bold and italic of the managed provider against GDI+, which the texts of CADability used before the
    /// font service (issue #412). <see cref="GdiFontProvider"/> asks GDI+ for the outline with the requested style, and
    /// GDI+ synthesizes a style that the family does not have. GDI+ returns hinted outlines, so the change that the
    /// synthetic style makes to the regular outline is compared point by point: the bold or italic outline minus the
    /// regular one, of the managed provider and of GDI+.
    /// </summary>
    [TestClass]
    public class SyntheticStyleGdiTests
    {
        private static readonly GdiFontProvider gdi = new GdiFontProvider();
        private static readonly ManagedFontProvider managed = new ManagedFontProvider();

        /// <summary>
        /// The point of the GDI+ outline for every point of the managed one: GDI+ may start a contour at another point (e.g.
        /// for MS Gothic), so the points are matched by their position. Null, if a point has no counterpart.
        /// </summary>
        private static int[] Match(PointF[] managedPoints, PointF[] gdiPoints, double em)
        {
            int[] res = new int[managedPoints.Length];
            for (int i = 0; i < managedPoints.Length; ++i)
            {
                bool Same(int j) => Math.Abs(managedPoints[i].X - gdiPoints[j].X) * em < 0.5 && Math.Abs(managedPoints[i].Y - gdiPoints[j].Y) * em < 0.5;
                if (Same(i)) { res[i] = i; continue; }
                res[i] = Enumerable.Range(0, gdiPoints.Length).Where(Same).DefaultIfEmpty(-1).First();
                if (res[i] < 0) return null;
            }
            return res;
        }

        [TestMethod]
        [DataRow("Microsoft Sans Serif", 0.0)]
        [DataRow("Lucida Console", 0.0)]
        [DataRow("Lucida Sans Unicode", 0.0)]
        [DataRow("Impact", 0.0)]
        [DataRow("MS Gothic", 0.0)] // 256 units per em
        // GDI+ moves single points of these two differently, without a rule that could be found: e.g. a point in the middle
        // of a straight slanted edge of the '8' of Sylfaen only moves up. The differences are up to 1.6% (Sylfaen) and 0.85%
        // (Gabriola) of the em height, so these families are compared with a tolerance of 2% of the em height.
        [DataRow("Sylfaen", 0.02)]
        [DataRow("Gabriola", 0.02)] // 4096 units per em
        public void SyntheticStylesMatchGdiPlus(string family, double toleranceOfEm)
        {
            FontFace mRegular = managed.GetFace(family, false, false);
            if (mRegular == null || gdi.GetFace(family, false, false) == null) Assert.Inconclusive(family + " is not installed");
            StringBuilder report = new StringBuilder();
            int compared = 0;
            foreach ((bool bold, bool italic) in new[] { (true, false), (false, true), (true, true) })
            {
                FontFace m = managed.GetFace(family, bold, italic);
                if (!(m is SyntheticFontFace)) continue; // the family has this face
                FontFace g = gdi.GetFace(family, bold, italic), gRegular = gdi.GetFace(family, false, false);
                double em = mRegular.EmHeight;
                double tolerance = Math.Max(2.0, toleranceOfEm * em); // in font units
                foreach (char c in "HIlo8")
                {
                    mRegular.GetGlyphPath(c, false, false, out PointF[] mr, out _);
                    m.GetGlyphPath(c, false, false, out PointF[] ms, out _);
                    gRegular.GetGlyphPath(c, false, false, out PointF[] gr, out _);
                    g.GetGlyphPath(c, false, false, out PointF[] gs, out _);
                    string what = $"{family}{(bold ? " bold" : "")}{(italic ? " italic" : "")} '{c}'";
                    double worst = 0.0;
                    int[] match = mr.Length == ms.Length && gr.Length == gs.Length && mr.Length == gr.Length ? Match(mr, gr, em) : null;
                    if (match != null)
                    {
                        for (int i = 0; i < mr.Length; ++i)
                        {
                            // the change of every point in font units
                            int j = match[i];
                            double dx = ((ms[i].X - mr[i].X) - (gs[j].X - gr[j].X)) * em, dy = ((ms[i].Y - mr[i].Y) - (gs[j].Y - gr[j].Y)) * em;
                            worst = Math.Max(worst, Math.Max(Math.Abs(dx), Math.Abs(dy)));
                        }
                    }
                    else
                    {   // GDI+ splits the curves differently or has other points: compare the change of the bounding box
                        double[] Box(PointF[] p) => new double[] { p.Min(q => q.X), p.Min(q => q.Y), p.Max(q => q.X), p.Max(q => q.Y) };
                        double[] a = Box(mr), b = Box(ms), c2 = Box(gr), d = Box(gs);
                        for (int k = 0; k < 4; ++k) worst = Math.Max(worst, Math.Abs((b[k] - a[k]) - (d[k] - c2[k])) * em);
                    }
                    if (worst > tolerance) report.AppendLine($"{what}: the synthetic style differs from GDI+ by {worst:F1} font units");
                    ++compared;
                }
            }
            if (compared == 0) Assert.Inconclusive(family + " has all styles");
            Assert.AreEqual(0, report.Length, report.ToString());
        }
    }
}
#endif
