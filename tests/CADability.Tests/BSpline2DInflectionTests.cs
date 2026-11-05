using System;
using System.Collections.Generic;
using CADability;
using CADability.Curve2D;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="BSpline2D.GetInflectionPoints"/> (from ShapeIt 9d8fcd0): two inflections in one knot span,
    /// an inflection exactly on a knot, a sign change across a C1 knot, rational and quartic spans, and curves without
    /// inflection points.
    /// <para>
    /// The reference is computed independently: the sign of d1 x d2 sampled densely along the curve via
    /// <see cref="ICurve2D.TryPointDeriv2At"/>, every sign change counts as an inflection point.
    /// </para>
    /// </summary>
    [TestClass]
    public class BSpline2DInflectionTests
    {
        private static List<double> SampledInflections(ICurve2D curve, int steps = 20000)
        {
            List<double> res = new List<double>();
            int lastSign = 0;
            for (int i = 0; i <= steps; ++i)
            {
                double pos = (double)i / steps;
                curve.TryPointDeriv2At(pos, out GeoPoint2D p, out GeoVector2D d1, out GeoVector2D d2);
                double cross = d1.x * d2.y - d1.y * d2.x;
                double zero = 1e-9 * d1.Length * d2.Length;
                int sign = cross > zero ? 1 : (cross < -zero ? -1 : 0);
                if (sign != 0)
                {
                    if (lastSign != 0 && sign != lastSign) res.Add(pos - 0.5 / steps);
                    lastSign = sign;
                }
            }
            return res;
        }

        private static void AssertMatchesSampling(BSpline2D bsp, int expectedCount)
        {
            double[] found = bsp.GetInflectionPoints();
            List<double> expected = SampledInflections(bsp);
            Assert.AreEqual(expectedCount, expected.Count, "reference sampling");
            Assert.AreEqual(expected.Count, found.Length, "number of inflection points: " + string.Join(", ", found));
            for (int i = 0; i < found.Length; ++i)
            {
                Assert.AreEqual(expected[i], found[i], 1e-3, "inflection point " + i);
            }
        }

        private static BSpline2D Bezier(double[] weights, params GeoPoint2D[] poles)
        {
            int degree = poles.Length - 1;
            return new BSpline2D(poles, weights, new double[] { 0, 1 }, new int[] { degree + 1, degree + 1 }, degree, false, 0.0, 1.0);
        }

        [TestMethod]
        public void CubicSpanWithTwoInflections()
        {   // both inflections lie in one knot span, the curvature has the same sign at both ends of the span
            BSpline2D bsp = Bezier(null, new GeoPoint2D(0, 0), new GeoPoint2D(0, 1), new GeoPoint2D(1, 1), new GeoPoint2D(-3, -1));
            AssertMatchesSampling(bsp, 2);
        }

        [TestMethod]
        public void CubicSpanWithTwoInflectionsNonRationalNubs()
        {   // a similar curve through the interpolation constructor, which yields the non-rational nubs representation
            BSpline2D bsp = Bezier(null, new GeoPoint2D(0, 0), new GeoPoint2D(0, 1), new GeoPoint2D(1, 1), new GeoPoint2D(-3, -1));
            BSpline2D nubs = new BSpline2D(new GeoPoint2D[] { bsp.PointAt(0.0), bsp.PointAt(0.25), bsp.PointAt(0.5), bsp.PointAt(0.75), bsp.PointAt(1.0) }, 3, false);
            int count = SampledInflections(nubs).Count;
            Assert.IsTrue(count > 0);
            AssertMatchesSampling(nubs, count);
        }

        [TestMethod]
        public void InflectionExactlyOnKnot()
        {   // the poles are point symmetric about the origin, so is the curve: the curvature is odd about the middle knot
            GeoPoint2D[] poles = { new GeoPoint2D(-3, 0), new GeoPoint2D(-2, -1), new GeoPoint2D(0, 0), new GeoPoint2D(2, 1), new GeoPoint2D(3, 0) };
            BSpline2D bsp = new BSpline2D(poles, null, new double[] { 0, 1, 2 }, new int[] { 4, 1, 4 }, 3, false, 0.0, 2.0);
            double[] found = bsp.GetInflectionPoints();
            Assert.AreEqual(1, found.Length, string.Join(", ", found));
            Assert.AreEqual(0.5, found[0], 1e-9);
        }

        [TestMethod]
        public void CurvatureJumpAtC1Knot()
        {   // a double knot makes the cubic only C1, the curvature changes its sign by a jump at the knot
            // the joint lies between the poles (2,0) and (3,0): the polygon turns right before it and left after it
            GeoPoint2D[] poles = { new GeoPoint2D(0, -1), new GeoPoint2D(1, -1), new GeoPoint2D(2, 0), new GeoPoint2D(3, 0), new GeoPoint2D(4, 1), new GeoPoint2D(5, 1) };
            BSpline2D bsp = new BSpline2D(poles, null, new double[] { 0, 1, 2 }, new int[] { 4, 2, 4 }, 3, false, 0.0, 2.0);
            double[] found = bsp.GetInflectionPoints();
            AssertMatchesSampling(bsp, found.Length);
            Assert.IsTrue(Array.Exists(found, f => Math.Abs(f - 0.5) < 1e-9), "inflection at the knot: " + string.Join(", ", found));
        }

        [TestMethod]
        public void RationalCubic()
        {
            BSpline2D bsp = Bezier(new double[] { 1, 3, 0.5, 1 }, new GeoPoint2D(0, 0), new GeoPoint2D(0, 1), new GeoPoint2D(1, 1), new GeoPoint2D(-3, -1));
            AssertMatchesSampling(bsp, SampledInflections(bsp).Count);
            Assert.IsTrue(SampledInflections(bsp).Count > 0);
        }

        [TestMethod]
        public void QuarticSpan()
        {
            BSpline2D bsp = Bezier(null, new GeoPoint2D(0, 0), new GeoPoint2D(1, 2), new GeoPoint2D(2, -2), new GeoPoint2D(3, 2), new GeoPoint2D(4, 0));
            AssertMatchesSampling(bsp, SampledInflections(bsp).Count);
            Assert.IsTrue(SampledInflections(bsp).Count > 0);
        }

        [TestMethod]
        public void RationalQuadraticHasNoInflection()
        {
            BSpline2D bsp = Bezier(new double[] { 1, 0.3, 1 }, new GeoPoint2D(0, 0), new GeoPoint2D(1, 1), new GeoPoint2D(2, 0));
            Assert.AreEqual(0, bsp.GetInflectionPoints().Length);
        }

        [TestMethod]
        public void StraightCubicHasNoInflection()
        {
            BSpline2D bsp = Bezier(null, new GeoPoint2D(0, 0), new GeoPoint2D(1, 1), new GeoPoint2D(3, 3), new GeoPoint2D(4, 4));
            Assert.AreEqual(0, bsp.GetInflectionPoints().Length);
        }

        [TestMethod]
        public void SCurveOverManySpans()
        {   // an interpolated sine: an inflection at each zero crossing, wherever the knots fall
            GeoPoint2D[] through = new GeoPoint2D[41];
            for (int i = 0; i < through.Length; ++i) through[i] = new GeoPoint2D(i * 0.25, Math.Sin(i * 0.25));
            BSpline2D bsp = new BSpline2D(through, 3, false);
            int count = SampledInflections(bsp).Count;
            Assert.IsTrue(count >= 3); // zero crossings of sin at pi, 2pi, 3pi, plus what the end conditions add
            AssertMatchesSampling(bsp, count);
        }
    }
}
