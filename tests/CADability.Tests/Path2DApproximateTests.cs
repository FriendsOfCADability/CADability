using System;
using System.Collections.Generic;
using CADability;
using CADability.Curve2D;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="Path2D.Approximate(bool, double)"/> (issue #554): a path that starts with a segment
    /// which is not allowed as is (an arc with linesOnly) must not lose whole segments, whichever segment it starts with,
    /// and a closed chain of short tangential curves must still be approximated with few segments.
    /// </summary>
    [TestClass]
    public class Path2DApproximateTests
    {
        private const double outerRadius = 100.0;
        private const double innerRadius = 70.0;

        /// <summary>The quarter ring from issue #554, starting with the outer arc.</summary>
        private static ICurve2D[] QuarterRing()
        {
            return new ICurve2D[]
            {
                new Arc2D(new GeoPoint2D(0, 0), outerRadius, new GeoPoint2D(outerRadius, 0), new GeoPoint2D(0, outerRadius), true),
                new Line2D(new GeoPoint2D(0, outerRadius), new GeoPoint2D(0, innerRadius)),
                new Arc2D(new GeoPoint2D(0, 0), innerRadius, new GeoPoint2D(0, innerRadius), new GeoPoint2D(innerRadius, 0), false),
                new Line2D(new GeoPoint2D(innerRadius, 0), new GeoPoint2D(outerRadius, 0)),
            };
        }

        private static ICurve2D[] Rotate(ICurve2D[] segments, int start)
        {
            ICurve2D[] res = new ICurve2D[segments.Length];
            for (int i = 0; i < segments.Length; i++) res[i] = segments[(i + start) % segments.Length].Clone();
            return res;
        }

        private static IEnumerable<GeoPoint2D> Samples(ICurve2D curve, int perSegment)
        {
            ICurve2D[] parts = curve is Path2D p ? p.SubCurves : new ICurve2D[] { curve };
            foreach (ICurve2D part in parts)
            {
                for (int i = 0; i <= perSegment; i++) yield return part.PointAt(i / (double)perSegment);
            }
        }

        /// <summary>
        /// Both directions of the Hausdorff distance, sampled: every point of the approximation lies near the original
        /// and every point of the original lies near the approximation.
        /// </summary>
        private static void AssertClose(ICurve2D original, ICurve2D approx, double maxError, string context)
        {
            double tolerance = maxError * 1.01 + 1e-9;
            foreach (GeoPoint2D p in Samples(approx, 50))
            {
                double d = original.MinDistance(p);
                Assert.IsTrue(d <= tolerance, $"{context}: point {p} of the approximation is {d} away from the original");
            }
            foreach (GeoPoint2D p in Samples(original, 200))
            {
                double d = approx.MinDistance(p);
                Assert.IsTrue(d <= tolerance, $"{context}: point {p} of the original is {d} away from the approximation");
            }
        }

        [TestMethod]
        public void QuarterRing_AnyStartSegment_KeepsBothArcs()
        {
            double exactArea = Math.PI / 4.0 * (outerRadius * outerRadius - innerRadius * innerRadius);
            double maxError = 1.0;
            foreach (bool linesOnly in new[] { true, false })
            {
                for (int start = 0; start < 4; start++)
                {
                    string context = $"start segment {start}, linesOnly {linesOnly}";
                    Path2D path = new Path2D(Rotate(QuarterRing(), start), true);
                    Path2D approx = path.Approximate(linesOnly, maxError) as Path2D;
                    Assert.IsNotNull(approx, context);
                    Assert.IsTrue(approx.IsClosed, context);

                    double area = Math.Abs(approx.GetArea());
                    // an inscribed polygon loses at most maxError times the length of the arcs
                    double arcLength = Math.PI / 2.0 * (outerRadius + innerRadius);
                    Assert.AreEqual(exactArea, area, arcLength * maxError, $"{context}: area {area}");

                    foreach (ICurve2D seg in approx.SubCurves)
                    {
                        if (linesOnly) Assert.IsInstanceOfType(seg, typeof(Line2D), context);
                        double r = seg.StartPoint.ToVector().Length;
                        Assert.IsTrue(r >= innerRadius - maxError && r <= outerRadius + 1e-6, $"{context}: vertex {seg.StartPoint} at radius {r}");
                    }
                    AssertClose(path, approx, maxError, context);
                }
            }
        }

        [TestMethod]
        public void QuarterRing_BorderAsPath_LinesOnly()
        {
            // the exact call from issue #554
            Border border = new Border(QuarterRing());
            Path2D approx = (Path2D)border.AsPath().Approximate(true, 1.0);
            double area = Math.Abs(approx.GetArea());
            Assert.IsTrue(area > 3900 && area < 4010, $"area {area}");
            AssertClose(border.AsPath(), approx, 1.0, "Border.AsPath");
        }

        /// <summary>A closed chain of short tangential elliptical arcs, the case the subdivision branch is meant for.</summary>
        private static Path2D EllipseChain(int pieces)
        {
            GeoPoint2D c = new GeoPoint2D(10, 20);
            GeoVector2D major = new GeoVector2D(80, 0);
            GeoVector2D minor = new GeoVector2D(0, 30);
            ICurve2D[] segs = new ICurve2D[pieces];
            for (int i = 0; i < pieces; i++)
            {
                double a1 = 2 * Math.PI * i / pieces;
                double a2 = 2 * Math.PI * (i + 1) / pieces;
                GeoPoint2D p1 = c + Math.Cos(a1) * major + Math.Sin(a1) * minor;
                GeoPoint2D p2 = c + Math.Cos(a2) * major + Math.Sin(a2) * minor;
                segs[i] = EllipseArc2D.Create(c, major, minor, p1, p2, true);
            }
            return new Path2D(segs, true);
        }

        [TestMethod]
        public void ClosedTangentialChain_IsApproximatedWithFewSegments()
        {
            const int pieces = 360;
            Path2D path = EllipseChain(pieces);
            Assert.IsTrue(path.IsClosed);
            double maxError = 0.1;
            foreach (bool linesOnly in new[] { true, false })
            {
                string context = $"linesOnly {linesOnly}";
                Path2D approx = path.Approximate(linesOnly, maxError) as Path2D;
                Assert.IsNotNull(approx, context);
                AssertClose(path, approx, maxError, context);
                Assert.AreEqual(Math.PI * 80 * 30, Math.Abs(approx.GetArea()), 2 * Math.PI * 80 * maxError, context);
                // segment by segment would yield at least one segment per piece
                Assert.IsTrue(approx.SubCurves.Length < pieces / 3, $"{context}: {approx.SubCurves.Length} segments");
            }
        }

        [TestMethod]
        public void ClosedChainOfArcs_LinesOnly_IsApproximatedWithFewSegments()
        {
            // a circle made of 72 tangential arcs: no segment is allowed as is with linesOnly
            const int pieces = 72;
            const double r = 50;
            ICurve2D[] segs = new ICurve2D[pieces];
            for (int i = 0; i < pieces; i++)
            {
                segs[i] = new Arc2D(GeoPoint2D.Origin, r, new Angle(2 * Math.PI * i / pieces), new SweepAngle(2 * Math.PI / pieces));
            }
            Path2D path = new Path2D(segs, true);
            double maxError = 0.1;
            Path2D approx = path.Approximate(true, maxError) as Path2D;
            Assert.IsNotNull(approx);
            AssertClose(path, approx, maxError, "circle of arcs");
            Assert.IsTrue(approx.SubCurves.Length < pieces, $"{approx.SubCurves.Length} segments");
        }
    }
}
