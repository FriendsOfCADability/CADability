using CADability.Curve2D;
using CADability.GeoObject;
using System;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="BSpline2D.Approximate(Func{double, GeoPoint2D}, double, double, double, int)"/> and
    /// <see cref="BSpline.Approximate(Func{double, GeoPoint}, double, double, double, int)"/>. Both methods used to
    /// pass a curve parameter where a normalized position (0...1) is expected, so with a parameter range other than
    /// 0...1 the measured deviation was meaningless and the approximation never converged: it subdivided until the
    /// maximum number of points was reached (1000 poles and seconds of calculation time instead of a few dozen poles).
    /// The parameter range is only a reparametrization, so it must not influence the result at all.
    /// </summary>
    [TestClass]
    public class SplineApproximationTests
    {
        public TestContext TestContext { get; set; }

        // a curve which is not a spline: the shape of a plane/cone intersection in the parameter space of a cone
        private static GeoPoint2D Curve2d(double u) => new GeoPoint2D(u, 50.0 / (0.8 * Math.Cos(u - 0.3) + 1.0));
        private static GeoPoint Curve3d(double u) => new GeoPoint(20.0 * Math.Cos(u), 20.0 * Math.Sin(u), 50.0 / (0.8 * Math.Cos(u - 0.3) + 1.0));
        private const double MinPar = 1.0, MaxPar = 5.0; // the parameter range of the curves above

        [TestMethod]
        public void Approximate2dDoesNotDependOnTheParameterRange()
        {
            const double precision = 1e-4;
            // the same curve, once parameterized from 0 to 1 and once with its own parameter range
            BSpline2D unitRange = BSpline2D.Approximate(t => Curve2d(MinPar + t * (MaxPar - MinPar)), precision);
            BSpline2D ownRange = BSpline2D.Approximate(Curve2d, precision, MinPar, MaxPar);
            Assert.IsNotNull(unitRange);
            Assert.IsNotNull(ownRange);
            double deviation1 = MaxDeviation(t => unitRange.PointAt(t));
            double deviation2 = MaxDeviation(t => ownRange.PointAt(t));
            TestContext?.WriteLine($"poles: {unitRange.Poles.Length} (0...1), {ownRange.Poles.Length} ({MinPar}...{MaxPar}), deviation: {deviation1}, {deviation2}");
            Assert.AreEqual(unitRange.Poles.Length, ownRange.Poles.Length, "the parameter range must not influence the number of poles");
            Assert.AreEqual(deviation1, deviation2, precision * 1e-3, "the parameter range must not influence the resulting curve");
            Assert.IsTrue(deviation1 < 0.01, "the approximation deviates far too much from the curve");
        }

        [TestMethod]
        public void Approximate3dDoesNotDependOnTheParameterRange()
        {
            const double precision = 1e-4;
            BSpline unitRange = BSpline.Approximate(t => Curve3d(MinPar + t * (MaxPar - MinPar)), precision);
            BSpline ownRange = BSpline.Approximate(Curve3d, precision, MinPar, MaxPar);
            Assert.IsNotNull(unitRange);
            Assert.IsNotNull(ownRange);
            double deviation1 = MaxDeviation(t => (unitRange as ICurve).PointAt(t).To2D());
            double deviation2 = MaxDeviation(t => (ownRange as ICurve).PointAt(t).To2D());
            TestContext?.WriteLine($"poles: {unitRange.PoleCount} (0...1), {ownRange.PoleCount} ({MinPar}...{MaxPar})");
            Assert.AreEqual(unitRange.PoleCount, ownRange.PoleCount, "the parameter range must not influence the number of poles");
            for (int i = 0; i <= 50; i++)
            {
                double t = i / 50.0;
                Assert.AreEqual(0.0, (unitRange as ICurve).PointAt(t) | (ownRange as ICurve).PointAt(t), precision * 1e-3, "the parameter range must not influence the resulting curve");
                Assert.IsTrue(((unitRange as ICurve).PointAt(t) | Curve3d(MinPar + t * (MaxPar - MinPar))) < 0.01, "the approximation deviates far too much from the curve");
            }
        }

        [TestMethod]
        public void ApproximateRejectsEmptyParameterRange()
        {
            // an empty or reversed range must not loop forever
            Assert.IsNull(BSpline2D.Approximate(Curve2d, 1e-4, 1.0, 1.0));
            Assert.IsNull(BSpline2D.Approximate(Curve2d, 1e-4, 5.0, 1.0));
            Assert.IsNull(BSpline.Approximate(Curve3d, 1e-4, 1.0, 1.0));
            Assert.IsNull(BSpline.Approximate(Curve3d, 1e-4, 5.0, 1.0));
        }

        [TestMethod]
        public void Approximate3dMeetsThePrecision()
        {
            foreach (double precision in new[] { 1e-2, 1e-4, 1e-6, 1e-8 })
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                int evaluations = 0;
                BSpline bsp = BSpline.Approximate(u => { evaluations++; return Curve3d(u); }, precision, MinPar, MaxPar);
                sw.Stop();
                // the distance of densely sampled curve points from the spline: the distance to the spline point at the
                // same parameter is an exact upper bound, DistanceTo is the geometric distance, but itself only approximated
                double maxDist = 0.0;
                for (int i = 0; i <= 2000; i++)
                {
                    double t = i / 2000.0;
                    GeoPoint p = Curve3d(MinPar + t * (MaxPar - MinPar));
                    maxDist = Math.Max(maxDist, FootPointDistance(bsp, p, t));
                }
                TestContext?.WriteLine($"precision {precision}: poles {bsp.PoleCount}, curve evaluations {evaluations}, max distance {maxDist}, {sw.ElapsedMilliseconds} ms");
                Assert.IsTrue(maxDist <= precision, $"deviation {maxDist} exceeds the precision {precision}");
                Assert.IsTrue(bsp.PoleCount < 1000, "the maximum number of points must not be reached");
            }
        }

        [TestMethod]
        public void Approximate2dMeetsThePrecision()
        {
            foreach (double precision in new[] { 1e-2, 1e-4, 1e-6, 1e-8 })
            {
                int evaluations = 0;
                BSpline2D bsp = BSpline2D.Approximate(u => { evaluations++; return Curve2d(u); }, precision, MinPar, MaxPar);
                double maxDist = 0.0, maxAt = 0.0;
                for (int i = 0; i <= 2000; i++)
                {
                    double t = i / 2000.0, t0 = t;
                    GeoPoint2D p = Curve2d(MinPar + t * (MaxPar - MinPar));
                    // foot point by Newton iteration, starting at the same normalized position
                    double d = bsp.PointAt(t) | p;
                    for (int j = 0; j < 20; j++)
                    {
                        GeoVector2D dir = bsp.DirectionAt(t);
                        double delta = -(dir * (bsp.PointAt(t) - p)) / (dir * dir);
                        t = Math.Max(0.0, Math.Min(1.0, t + delta));
                        d = Math.Min(d, bsp.PointAt(t) | p);
                        if (Math.Abs(delta) < 1e-15) break;
                    }
                    if (d > maxDist) { maxDist = d; maxAt = t0; }
                }
                TestContext?.WriteLine($"precision {precision}: poles {bsp.Poles.Length}, curve evaluations {evaluations}, max distance {maxDist} at {maxAt}");
                Assert.IsTrue(maxDist <= precision, $"deviation {maxDist} exceeds the precision {precision}");
                Assert.IsTrue(bsp.Poles.Length < 1000, "the maximum number of points must not be reached");
            }
        }

        [TestMethod]
        public void Approximate3dWithKinkTerminates()
        {
            // a curve with a tangent discontinuity at u=0.3: cannot be met by a smooth spline, must stop at maxCount at the latest
            BSpline bsp = BSpline.Approximate(u => new GeoPoint(u, Math.Abs(u - 0.3), 0), 1e-6, 0, 1, 200);
            Assert.IsNotNull(bsp);
            Assert.IsTrue(bsp.PoleCount <= 200);
            TestContext?.WriteLine($"poles at kink: {bsp.PoleCount}");
        }

        [TestMethod]
        public void Approximate2dWithKinkTerminates()
        {
            BSpline2D bsp = BSpline2D.Approximate(u => new GeoPoint2D(u, Math.Abs(u - 0.3)), 1e-6, 0, 1, 200);
            Assert.IsNotNull(bsp);
            Assert.IsTrue(bsp.Poles.Length <= 200);
        }

        [TestMethod]
        public void ApproximateOfAnUndefinedCurveIsNull()
        {
            // the interpolation of NaN points fails, which must end in null and not in an exception
            Assert.IsNull(BSpline2D.Approximate(u => new GeoPoint2D(double.NaN, u), 1e-4));
            Assert.IsNull(BSpline.Approximate(u => new GeoPoint(double.NaN, u, 0), 1e-4));
        }

        /// <summary>
        /// The geometric distance of <paramref name="p"/> from <paramref name="curve"/>, found by a Newton iteration for
        /// the foot point starting at the normalized position <paramref name="t"/>. DistanceTo is not precise enough
        /// to verify small precisions.
        /// </summary>
        private static double FootPointDistance(ICurve curve, GeoPoint p, double t)
        {
            double d = curve.PointAt(t) | p;
            for (int i = 0; i < 20; i++)
            {
                GeoVector dir = curve.DirectionAt(t);
                double delta = -(dir * (curve.PointAt(t) - p)) / (dir * dir);
                t = Math.Max(0.0, Math.Min(1.0, t + delta));
                d = Math.Min(d, curve.PointAt(t) | p);
                if (Math.Abs(delta) < 1e-15) break;
            }
            return d;
        }

        /// <summary>
        /// The maximum distance between the approximation (at the normalized position 0...1) and the exact 2d curve.
        /// </summary>
        private static double MaxDeviation(Func<double, GeoPoint2D> approximation)
        {
            double res = 0.0;
            for (int i = 0; i <= 200; i++)
            {
                double t = i / 200.0;
                res = Math.Max(res, approximation(t) | Curve2d(MinPar + t * (MaxPar - MinPar)));
            }
            return res;
        }
    }
}
