using System;
using CADability.Curve2D;
using CADability.Shapes;

namespace CADability.Tests
{
    [TestClass]
    public class ConvexHullTests
    {
        /// <summary>
        /// Arc which reports (numerically caused) inflection points, e.g. at the very start of the curve.
        /// Trimming at such a position yields null, which used to crash Border.ConvexHull.
        /// </summary>
        private class ArcWithInflectionPoints : Arc2D
        {
            private readonly double[] inflectionPoints;
            public ArcWithInflectionPoints(GeoPoint2D center, double radius, Angle start, SweepAngle sweep, double[] inflectionPoints)
                : base(center, radius, start, sweep)
            {
                this.inflectionPoints = inflectionPoints;
            }
            public override double[] GetInflectionPoints() => inflectionPoints;
            public override ICurve2D Clone() => new ArcWithInflectionPoints(Center, Radius, new Angle(StartPoint, Center), SweepAngle, inflectionPoints);
        }

        [DataTestMethod]
        [DataRow(new double[] { 0.0 })]
        [DataRow(new double[] { 1.0 })]
        [DataRow(new double[] { 0.0, 0.5 })]
        [DataRow(new double[] { 0.3, 0.3 })]
        public void ConvexHull_InflectionPointsAtSegmentEnds_DoesNotThrow(double[] inflectionPoints)
        {
            Border halfDisc = new Border(new ICurve2D[]
            {
                new ArcWithInflectionPoints(GeoPoint2D.Origin, 10, new Angle(0.0), new SweepAngle(Math.PI), inflectionPoints),
                new Line2D(new GeoPoint2D(-10, 0), new GeoPoint2D(10, 0))
            });

            Border hull = halfDisc.ConvexHull();
            SmallestEnclosingRect rect = halfDisc.GetSmallestEnclosingRectangle();

            Assert.AreEqual(Math.PI * 50, hull.Area, 1e-6);
            Assert.AreEqual(20, Math.Max(rect.Width, rect.Height), 1e-6);
            Assert.AreEqual(10, Math.Min(rect.Width, rect.Height), 1e-6);
        }

        [TestMethod]
        public void GetSmallestEnclosingRectangle_Square()
        {
            SmallestEnclosingRect rect = Border.MakeRectangle(0, 10, 0, 10).GetSmallestEnclosingRectangle();
            Assert.AreEqual(10, rect.Width, 1e-6);
            Assert.AreEqual(10, rect.Height, 1e-6);
        }
    }
}
