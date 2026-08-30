using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.RootFinding;

namespace CADability
{
    public static class AdaptiveRootFinder
    {
        public static List<double> FindRootsAdaptive(
            Func<double, double> f,
            double a, double b,
            int initialSteps = 10,
            double tol = 1e-10,
            double minInterval = 1e-6,
            int maxDepth = 20)
        {
            var xs = new List<double>();
            // 1. Grobes Gitter
            for (int i = 0; i <= initialSteps; i++)
                xs.Add(a + (b - a) * i / initialSteps);

            var fs = xs.Select(f).ToList();
            var roots = new List<double>();

            // Prüfe alle Brackets und sammle Vorzeichenwechsel
            for (int i = 0; i < xs.Count - 1; i++)
            {
                if (fs[i] * fs[i + 1] <= 0)
                {
                    // Brent liefert bei Vorzeichenwechsel
                    double root = Brent.FindRoot(f, xs[i], xs[i + 1], tol);
                    roots.Add(root);
                }
                else
                {
                    // 2. Triple-Check
                    if (i > 0 && i < xs.Count - 1)
                    {
                        double f0 = fs[i - 1], f1 = fs[i], f2 = fs[i + 1];
                        if (Math.Abs(f1) < Math.Abs(f0) && Math.Abs(f1) < Math.Abs(f2))
                        {
                            // 3. Verfeinere lokal
                            RefineInterval(f, xs[i - 1], xs[i + 1], f0, f2, roots, tol, minInterval, 1, maxDepth);
                        }
                    }
                }
            }

            // Duplikate (bspw. nahe beieinanderliegende Wurzeln) entfernen
            roots.Sort();
            var uniq = new List<double>();
            double last = double.NaN;
            foreach (var r in roots)
            {
                if (double.IsNaN(last) || Math.Abs(r - last) > tol)
                    uniq.Add(r);
                last = r;
            }
            return uniq;
        }

        private static void RefineInterval(
            Func<double, double> f,
            double x0, double x2,
            double f0, double f2,
            List<double> roots,
            double tol, double minInterval,
            int depth, int maxDepth)
        {
            if (depth > maxDepth || x2 - x0 < minInterval)
                return;

            double x1 = 0.5 * (x0 + x2);
            double f1 = f(x1);

            // Sign-Check im Sub-Interval
            if (f0 * f1 <= 0)
                roots.Add(Brent.FindRoot(f, x0, x1, tol));
            else if (f1 * f2 <= 0)
                roots.Add(Brent.FindRoot(f, x1, x2, tol));
            else
            {
                // erneuter Triple-Check
                if (Math.Abs(f1) < Math.Abs(f0) && Math.Abs(f1) < Math.Abs(f2))
                {
                    // links und rechts weiter teilen
                    RefineInterval(f, x0, x1, f0, f1, roots, tol, minInterval, depth + 1, maxDepth);
                    RefineInterval(f, x1, x2, f1, f2, roots, tol, minInterval, depth + 1, maxDepth);
                }
            }
        }
    }
}