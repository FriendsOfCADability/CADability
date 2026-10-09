using System;
using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// Arc length of a curve by numerical integration of its speed, i.e. the length of the unnormalized
    /// tangent |C'(t)|. The length is the integral of that speed over the parameter interval, which is what
    /// a curve whose parametrization is not proportional to the arc length - a spline, a rational spline
    /// above all - needs: measuring it by the polygon through a handful of interpolation points is short by
    /// whatever the curve bulges out between them. The exact rational quadratic circle is the extreme case,
    /// with one span per quadrant: its chords are 10% shorter than the circle.
    /// </summary>
    internal static class ArcLength
    {
        // 10 point Gauss-Legendre on [-1,1], positive half only - the rule is symmetric, so each node counts
        // twice. Two properties are what make it the right rule here: it is exact up to polynomial degree 19,
        // so a smooth span is finished in one evaluation, and it never samples the ends of the interval,
        // where the speed of a spline with a degenerate first or last span is singular.
        private static readonly double[] nodes = {
            0.1488743389816312, 0.4333953941292472, 0.6794095682990244, 0.8650633666889845, 0.9739065285171717 };
        private static readonly double[] weights = {
            0.2955242247147529, 0.2692667193099963, 0.2190863625159820, 0.1494513491505806, 0.0666713443086881 };

        private static double Gauss(Func<double, double> speed, double from, double to)
        {
            double middle = (from + to) / 2.0;
            double half = (to - from) / 2.0;
            double sum = 0.0;
            for (int i = 0; i < nodes.Length; ++i)
            {
                double d = half * nodes[i];
                sum += weights[i] * (speed(middle - d) + speed(middle + d));
            }
            return sum * half;
        }

        /// <summary>
        /// Bisects until the halved interval no longer changes the result by more than <paramref name="tolerance"/>.
        /// The tolerance is halved along with the interval, so the errors of all parts add up to at most the
        /// tolerance this was started with.
        /// </summary>
        private static double Refine(Func<double, double> speed, double from, double to, double coarse, double tolerance, int depth)
        {
            double middle = (from + to) / 2.0;
            double left = Gauss(speed, from, middle);
            double right = Gauss(speed, middle, to);
            double fine = left + right;
            if (depth <= 0 || Math.Abs(fine - coarse) <= tolerance) return fine;
            return Refine(speed, from, middle, left, tolerance / 2.0, depth - 1)
                 + Refine(speed, middle, to, right, tolerance / 2.0, depth - 1);
        }

        /// <summary>
        /// The arc length over [<paramref name="from"/>, <paramref name="to"/>] of a curve whose speed |C'(t)|
        /// is given by <paramref name="speed"/>. Always positive, also for from &gt; to.
        /// </summary>
        /// <param name="speed">length of the unnormalized tangent at a parameter</param>
        /// <param name="from">start of the parameter interval</param>
        /// <param name="to">end of the parameter interval</param>
        /// <param name="breakPoints">parameters where the speed is not smooth - the knots of a spline. Values
        /// outside the interval are ignored, the order does not matter</param>
        /// <param name="relativeTolerance">accuracy relative to the total length</param>
        /// <param name="maxDepth">gives up bisecting after this many levels, so a curve with a cusp - where
        /// the speed drops to zero and the integrand is no longer smooth - terminates too</param>
        public static double FromSpeed(Func<double, double> speed, double from, double to,
            IEnumerable<double> breakPoints = null, double relativeTolerance = 1e-9, int maxDepth = 12)
        {
            if (from > to)
            {
                double t = from; from = to; to = t;
            }
            if (!(to > from)) return 0.0; // empty interval, or NaN ends

            // the knots cut the curve into its spans: inside a span the speed is smooth and the Gauss rule
            // converges fast, across a knot it is only continuous and a rule spanning the kink would not
            List<double> parts = new List<double>();
            parts.Add(from);
            if (breakPoints != null)
            {
                foreach (double b in breakPoints)
                {
                    if (b > from && b < to) parts.Add(b);
                }
                parts.Sort();
            }
            parts.Add(to);

            double[] coarse = new double[parts.Count - 1];
            double estimate = 0.0;
            for (int i = 0; i < coarse.Length; ++i)
            {
                coarse[i] = Gauss(speed, parts[i], parts[i + 1]);
                estimate += coarse[i];
            }
            if (!(estimate > 0.0)) return estimate; // zero length, or NaN - nothing to refine

            double tolerance = relativeTolerance * estimate / coarse.Length;
            double res = 0.0;
            for (int i = 0; i < coarse.Length; ++i)
            {
                res += Refine(speed, parts[i], parts[i + 1], coarse[i], tolerance, maxDepth);
            }
            return res;
        }

        /// <summary>
        /// The arc length of the ellipse (or elliptical arc) P(t) == C + cos(t)*A + sin(t)*B for t from
        /// <paramref name="start"/> over <paramref name="sweep"/> (may be negative). The axes A and B need not be
        /// perpendicular, only their squared lengths and their scalar product are needed. There is no closed form
        /// for this length (it is an incomplete elliptic integral of the second kind), so it is integrated
        /// numerically, which is exact to the floating point precision for any ratio of the axes.
        /// </summary>
        /// <param name="aa">A*A, the squared length of the first axis</param>
        /// <param name="bb">B*B, the squared length of the second axis</param>
        /// <param name="ab">A*B, zero for perpendicular axes</param>
        /// <param name="start">start parameter (angle)</param>
        /// <param name="sweep">swept angle</param>
        public static double OfEllipse(double aa, double bb, double ab, double start, double sweep)
        {
            if (sweep == 0.0) return 0.0;
            if (ab == 0.0 && aa == bb) return Math.Sqrt(aa) * Math.Abs(sweep); // a circle
            // |P'(t)|^2 == sin^2*A*A - 2*sin*cos*A*B + cos^2*B*B. The speed is smallest (and for a very slender
            // ellipse almost has a kink) at the multiples of pi/2 when the axes are perpendicular, so these are
            // the natural places to cut the interval
            Func<double, double> speed = t =>
            {
                double s = Math.Sin(t), c = Math.Cos(t);
                return Math.Sqrt(Math.Max(0.0, s * s * aa - 2.0 * s * c * ab + c * c * bb));
            };
            double from = Math.Min(start, start + sweep), to = Math.Max(start, start + sweep);
            List<double> quarters = new List<double>();
            for (double q = Math.Ceiling(from / (Math.PI / 2)) * (Math.PI / 2); q < to; q += Math.PI / 2) quarters.Add(q);
            return FromSpeed(speed, from, to, quarters, 1e-13, 20);
        }

        /// <summary>
        /// The arc length of a curve, which is only known by its points, over the parameter interval
        /// [<paramref name="from"/>, <paramref name="to"/>]. Summing up the chords between a few points is always
        /// too short, by how much the curve deviates from its chords. The sum over n equal parameter steps of a
        /// smooth curve is short by c2/n^2 + c4/n^4 + ..., so the sums for n, 2n, 4n, ... are extrapolated to an
        /// infinite number of chords (Romberg). Unlike the speed from finite differences, the chords do not amplify
        /// the noise of points which are computed by an iteration, e.g. onto two intersecting surfaces.
        /// </summary>
        /// <param name="pointAt">the point at a parameter</param>
        /// <param name="from">start of the parameter interval</param>
        /// <param name="to">end of the parameter interval</param>
        /// <param name="breakPoints">parameters where the curve may not be smooth, e.g. where the pieces of an
        /// approximation meet. Each span between them is measured on its own. Values outside the interval are
        /// ignored, the order does not matter</param>
        /// <param name="relativeTolerance">accuracy relative to the length of each span</param>
        /// <param name="maxLevel">the number of chords per span is doubled at most this many times, starting with 4</param>
        public static double FromPoints(Func<double, GeoPoint> pointAt, double from, double to,
            IEnumerable<double> breakPoints = null, double relativeTolerance = 1e-11, int maxLevel = 10)
        {
            if (from > to)
            {
                double t = from; from = to; to = t;
            }
            if (!(to > from)) return 0.0;
            List<double> parts = new List<double>();
            parts.Add(from);
            if (breakPoints != null)
            {
                foreach (double b in breakPoints)
                {
                    if (b > from && b < to) parts.Add(b);
                }
                parts.Sort();
            }
            parts.Add(to);
            double res = 0.0;
            for (int i = 0; i < parts.Count - 1; ++i)
            {
                if (parts[i + 1] > parts[i]) res += Romberg(pointAt, parts[i], parts[i + 1], relativeTolerance, maxLevel);
            }
            return res;
        }

        private static double Romberg(Func<double, GeoPoint> pointAt, double from, double to, double relativeTolerance, int maxLevel)
        {
            int n = 4;
            GeoPoint[] points = new GeoPoint[n + 1];
            for (int i = 0; i <= n; ++i) points[i] = pointAt(from + (to - from) * i / n);
            double[] previous = new double[] { Chords(points) };
            for (int level = 1; level <= maxLevel; ++level)
            {
                // the new points lie between the old ones, which are reused
                GeoPoint[] finer = new GeoPoint[2 * n + 1];
                for (int i = 0; i < n; ++i)
                {
                    finer[2 * i] = points[i];
                    finer[2 * i + 1] = pointAt(from + (to - from) * (2 * i + 1) / (2 * n));
                }
                finer[2 * n] = points[n];
                points = finer;
                n *= 2;
                double[] row = new double[level + 1];
                row[0] = Chords(points);
                double f = 1.0;
                for (int j = 1; j <= level; ++j)
                {
                    f *= 4.0;
                    row[j] = row[j - 1] + (row[j - 1] - previous[j - 1]) / (f - 1.0);
                }
                if (level >= 2 && Math.Abs(row[level] - previous[level - 1]) <= relativeTolerance * Math.Abs(row[level])) return row[level];
                previous = row;
            }
            return previous[previous.Length - 1];
        }

        private static double Chords(GeoPoint[] points)
        {
            double sum = 0.0;
            for (int i = 0; i < points.Length - 1; ++i) sum += points[i] | points[i + 1];
            return sum;
        }
    }
}
