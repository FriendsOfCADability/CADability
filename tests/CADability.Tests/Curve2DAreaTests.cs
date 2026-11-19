using System;
using System.Collections.Generic;
using System.Linq;
using CADability;
using CADability.Curve2D;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for the swept area of a 2d curve, the quantity <see cref="Border.Area"/> sums over its
    /// segments: the integral of (x*y' - y*x')/2 over the parameter interval, which is what
    /// <see cref="Line2D.GetArea"/> and <see cref="Arc2D.GetArea"/> return in closed form.
    /// <para>
    /// Every case here has an answer that can be written down. The curves are exact circles in the nine
    /// pole rational quadratic form, so their area is pi*r*r and nothing about that is approximate.
    /// </para>
    /// </summary>
    [TestClass]
    public class Curve2DAreaTests
    {
        /// <summary>The classical nine pole rational quadratic circle - exact, not an approximation.</summary>
        private static BSpline2D NurbsCircle(double cx, double cy, double r)
        {
            double w = Math.Sqrt(2.0) / 2.0;
            double[,] q = { { 1, 0 }, { 1, 1 }, { 0, 1 }, { -1, 1 }, { -1, 0 }, { -1, -1 }, { 0, -1 }, { 1, -1 }, { 1, 0 } };
            GeoPoint2D[] poles = new GeoPoint2D[9];
            double[] weights = new double[9];
            for (int i = 0; i < 9; i++)
            {
                poles[i] = new GeoPoint2D(cx + r * q[i, 0], cy + r * q[i, 1]);
                weights[i] = (i % 2 == 0) ? 1.0 : w;
            }
            return new BSpline2D(poles, weights, new double[] { 0, 1, 2, 3, 4 }, new int[] { 3, 2, 2, 2, 3 }, 2, false, 0.0, 4.0);
        }

        /// <summary>
        /// The same integral, evaluated here and not by the code under test: composite Simpson over the
        /// parameter interval, fine enough to be exact to the last digit that matters.
        /// </summary>
        private static double SweptArea(ICurve2D curve, GeoPoint2D from, int steps = 20000)
        {
            double Integrand(double t)
            {
                GeoPoint2D p = curve.PointAt(t);
                GeoVector2D d = curve.DirectionAt(t);
                return 0.5 * ((p.x - from.x) * d.y - (p.y - from.y) * d.x);
            }
            double h = 1.0 / steps, sum = Integrand(0.0) + Integrand(1.0);
            for (int i = 1; i < steps; i++) sum += (i % 2 == 1 ? 4.0 : 2.0) * Integrand(i * h);
            return sum * h / 3.0;
        }

        /// <summary>
        /// The defect this replaced: the area used to be taken from a biarc approximation built with
        /// precision 0.0, which degenerates into straight lines for a rational curve, so a circle measured
        /// the area of its chord polygon, 2*r*r instead of pi*r*r. Which radii it hit was erratic - exact
        /// for 10, 12 and 19, short by the factor 2/pi for 4, 8 and 25 - which is why this test walks a
        /// range rather than a single radius.
        /// </summary>
        [TestMethod]
        public void rational_nurbs_circle_measures_its_exact_area()
        {
            foreach (double r in new double[] { 2.5, 4, 8, 9, 10, 12, 13, 19, 25, 30 })
            {
                double exact = Math.PI * r * r;
                double measured = NurbsCircle(0, 0, r).GetArea();
                Assert.AreEqual(exact, measured, 1e-9 * exact, $"the area of an exact circle of radius {r}");
                Assert.AreNotEqual(2.0 * r * r, measured, 1e-6, $"radius {r} fell back on the chord polygon");
            }
        }

        /// <summary>
        /// A closed curve encloses the same area whatever point it is seen from - the shift term
        /// telescopes to the end points, and they coincide. This is what makes GetAreaFromPoint a closed
        /// form correction of GetArea rather than a second integration.
        /// </summary>
        [TestMethod]
        public void area_from_point_does_not_depend_on_the_point_for_a_closed_curve()
        {
            BSpline2D circle = NurbsCircle(30, -7, 8);
            double expected = Math.PI * 64;
            Assert.AreEqual(expected, circle.GetArea(), 1e-9 * expected);
            foreach (GeoPoint2D p in new[] { GeoPoint2D.Origin, new GeoPoint2D(30, -7), new GeoPoint2D(-100, 250) })
                Assert.AreEqual(expected, circle.GetAreaFromPoint(p), 1e-9 * expected, $"seen from {p}");
        }

        /// <summary>
        /// On an OPEN curve the reference point does matter, and the closed form shift has to agree with
        /// the integral it stands for - checked against an independent Simpson quadrature.
        /// </summary>
        [TestMethod]
        public void area_from_point_matches_the_integral_on_an_open_curve()
        {
            ICurve2D half = NurbsCircle(0, 0, 11).Trim(0.0, 0.5);
            foreach (GeoPoint2D p in new[] { GeoPoint2D.Origin, new GeoPoint2D(4, -3), new GeoPoint2D(-60, 20) })
            {
                double expected = SweptArea(half, p);
                Assert.AreEqual(expected, half.GetAreaFromPoint(p), 1e-7 * Math.Max(1.0, Math.Abs(expected)),
                    $"the swept area of a half circle seen from {p}");
            }
        }

        /// <summary>
        /// The point of the whole thing: a Border built from a rational circle, which is what a planar
        /// face of a NURBS solid has as its outline, and whose area ShellMetrics multiplies by the flux
        /// density to get the volume contribution of that face.
        /// </summary>
        [TestMethod]
        public void border_of_a_rational_circle_has_the_area_of_a_circle()
        {
            foreach (double r in new double[] { 4, 8, 10, 25 })
            {
                double exact = Math.PI * r * r;
                Assert.AreEqual(exact, new Border(NurbsCircle(0, 0, r)).Area, 1e-9 * exact, $"radius {r}");
                Assert.AreEqual(exact, new SimpleShape(new Border(NurbsCircle(17, 4, r))).Area, 1e-9 * exact,
                    $"radius {r}, off centre and through SimpleShape");
            }
        }

        /// <summary>
        /// A clamped quintic spline through twelve poles, with the knot <paramref name="u"/> inserted four
        /// times (Boehm). Insertion does not change the curve, only its knot spans: with u close to 1 the
        /// last span becomes as short as 1 - u, with u close to 0 the first one.
        /// </summary>
        private static BSpline2D QuinticWithShortSpan(double u)
        {
            const int degree = 5;
            List<GeoPoint2D> poles = new List<GeoPoint2D>();
            for (int i = 0; i < 12; i++)
            {
                double t = i / 11.0;
                poles.Add(new GeoPoint2D(520 - 430 * t, 310 + 3 * Math.Sin(5 * t)));
            }
            List<double> knots = new List<double>();
            for (int i = 0; i <= degree; i++) knots.Add(0.0);
            for (int i = 1; i <= 6; i++) knots.Add(i / 7.0);
            for (int i = 0; i <= degree; i++) knots.Add(1.0);
            for (int insertion = 0; insertion < 4; insertion++)
            {
                int k = knots.FindLastIndex(x => x <= u);
                List<GeoPoint2D> inserted = new List<GeoPoint2D>();
                for (int i = 0; i <= poles.Count; i++)
                {
                    if (i <= k - degree) inserted.Add(poles[i]);
                    else if (i > k) inserted.Add(poles[i - 1]);
                    else
                    {
                        double a = (u - knots[i]) / (knots[i + degree] - knots[i]);
                        inserted.Add(new GeoPoint2D((1 - a) * poles[i - 1].x + a * poles[i].x, (1 - a) * poles[i - 1].y + a * poles[i].y));
                    }
                }
                poles = inserted;
                knots.Insert(k + 1, u);
            }
            double[] distinct = knots.Distinct().ToArray();
            int[] multiplicities = distinct.Select(d => knots.Count(x => x == d)).ToArray();
            double[] weights = Enumerable.Repeat(1.0, poles.Count).ToArray();
            return new BSpline2D(poles.ToArray(), weights, distinct, multiplicities, degree, false, 0.0, 1.0);
        }

        /// <summary>
        /// A short knot span next to u = 1 used to blow the area up: the exact area came from polynomials
        /// written in the global parameter, whose coefficients on such a span grow with the inverse fifth
        /// power of its width and cancel only in exact arithmetic. The plausibility check compared that area
        /// with a chord polygon evaluated through the same polynomials, so both were wrong by the same
        /// amount and the check passed. A CAD import met it in a DXF part whose spline had a span of width
        /// 5e-7 at its end: a contour of 475 x 86 mm measured 4.8e33 mm^2, and the weight computed from that
        /// overflowed a decimal.
        /// </summary>
        [TestMethod]
        public void short_last_knot_span_does_not_blow_up_the_area()
        {
            foreach (double width in new double[] { 1e-2, 1e-3, 1e-4, 1e-5, 1e-6, 5e-7, 1e-7 })
            {
                BSpline2D spline = QuinticWithShortSpan(1.0 - width);
                double expected = SweptArea(spline, GeoPoint2D.Origin);
                Assert.AreEqual(expected, spline.GetArea(), 1e-7 * Math.Abs(expected), $"last span of width {width}");
            }
        }

        /// <summary>
        /// The way the short span reached the end in the CAD import: the spline in the file had it at its
        /// start, where the polynomials are harmless, and assembling the contour reversed it. Reversing a
        /// curve only changes the sign of the area it sweeps out.
        /// </summary>
        [TestMethod]
        public void reversing_a_spline_with_a_short_first_span_negates_its_area()
        {
            BSpline2D spline = QuinticWithShortSpan(5e-7);
            double forward = spline.GetArea();
            Assert.AreEqual(SweptArea(spline, GeoPoint2D.Origin), forward, 1e-7 * Math.Abs(forward), "before reversing");
            BSpline2D reversed = (BSpline2D)spline.Clone();
            reversed.Reverse();
            Assert.AreEqual(-forward, reversed.GetArea(), 1e-7 * Math.Abs(forward), "after reversing");
        }
    }
}
