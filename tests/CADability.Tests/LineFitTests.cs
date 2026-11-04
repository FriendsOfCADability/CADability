using System;
using CADability;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="Geometry.LineFit(GeoPoint2D[], out GeoPoint2D, out GeoVector2D)"/> and its 3d counterpart:
    /// orthogonal least squares, i.e. the line through the centroid that minimizes the sum of the squared perpendicular
    /// distances. The lines deliberately lie far from the origin, where a fit of the uncentered moments goes wrong.
    /// </summary>
    [TestClass]
    public class LineFitTests
    {
        private static double SumOfSquares2D(GeoPoint2D[] points, GeoPoint2D loc, GeoVector2D dir)
        {
            GeoVector2D n = dir.Normalized;
            double sum = 0.0;
            foreach (GeoPoint2D p in points)
            {
                double d = (p.x - loc.x) * n.y - (p.y - loc.y) * n.x;
                sum += d * d;
            }
            return sum;
        }

        [TestMethod]
        public void PointsOnHorizontalLineFarFromOrigin()
        {
            GeoPoint2D[] points = new GeoPoint2D[11];
            for (int i = 0; i < points.Length; i++) points[i] = new GeoPoint2D(i / 10.0, 10.0);
            double dev = Geometry.LineFit(points, out GeoPoint2D loc, out GeoVector2D dir);
            Assert.AreEqual(0.0, dev, 1e-12);
            Assert.AreEqual(1.0, Math.Abs(dir.x), 1e-12);
            Assert.AreEqual(0.0, dir.y, 1e-12);
            Assert.AreEqual(10.0, loc.y, 1e-12);
        }

        [TestMethod]
        public void PointsOnSlantedLineFarFromOrigin()
        {
            GeoVector2D expected = new GeoVector2D(3, -1).Normalized;
            GeoPoint2D[] points = new GeoPoint2D[7];
            for (int i = 0; i < points.Length; i++) points[i] = new GeoPoint2D(1000, -500) + (i * 0.37) * expected;
            double dev = Geometry.LineFit(points, out GeoPoint2D loc, out GeoVector2D dir);
            Assert.AreEqual(0.0, dev, 1e-9);
            Assert.AreEqual(1.0, Math.Abs(dir * expected), 1e-12);
            Assert.AreEqual(1.0, dir.Length, 1e-12);
        }

        [TestMethod]
        public void TwoPointsDefineTheLine()
        {
            GeoPoint2D[] points = { new GeoPoint2D(5, 5), new GeoPoint2D(6, 7) };
            double dev = Geometry.LineFit(points, out GeoPoint2D loc, out GeoVector2D dir);
            Assert.AreEqual(0.0, dev, 1e-12);
            Assert.AreEqual(1.0, Math.Abs(dir * new GeoVector2D(1, 2).Normalized), 1e-12);
        }

        [TestMethod]
        public void NoisyPointsMinimizeTheSumOfSquares()
        {   // the reference is a brute force search over the direction through the centroid, which is part of the optimum
            Random rnd = new Random(17);
            GeoPoint2D[] points = new GeoPoint2D[50];
            for (int i = 0; i < points.Length; i++)
            {
                double t = rnd.NextDouble() * 10.0;
                points[i] = new GeoPoint2D(200 + t, 300 + 0.4 * t + (rnd.NextDouble() - 0.5));
            }
            Geometry.LineFit(points, out GeoPoint2D loc, out GeoVector2D dir);
            double fitted = SumOfSquares2D(points, loc, dir);
            GeoPoint2D centroid = new GeoPoint2D(points);
            for (int k = 0; k < 3600; k++)
            {
                double a = Math.PI * k / 3600;
                double other = SumOfSquares2D(points, centroid, new GeoVector2D(Math.Cos(a), Math.Sin(a)));
                Assert.IsTrue(fitted <= other * (1 + 1e-12), "direction at angle " + a + " is better");
            }
            // and the line must not be improved by a parallel shift either
            GeoVector2D n = dir.ToLeft().Normalized;
            Assert.IsTrue(fitted <= SumOfSquares2D(points, loc + 0.01 * n, dir));
            Assert.IsTrue(fitted <= SumOfSquares2D(points, loc - 0.01 * n, dir));
        }

        [TestMethod]
        public void PointsOn3DLineFarFromOrigin()
        {
            GeoVector expected = new GeoVector(1, 2, -0.5).Normalized;
            GeoPoint[] points = new GeoPoint[9];
            for (int i = 0; i < points.Length; i++) points[i] = new GeoPoint(100, -40, 250) + (i * 0.2) * expected;
            double dev = Geometry.LineFit(points, out GeoPoint loc, out GeoVector dir);
            Assert.AreEqual(0.0, dev, 1e-9);
            Assert.AreEqual(1.0, Math.Abs(dir * expected), 1e-12);
            Assert.AreEqual(1.0, dir.Length, 1e-12);
        }

        [TestMethod]
        public void Noisy3DPointsMatch2DFitInTheirPlane()
        {   // points in the plane z = 7: the 3d fit must agree with the 2d fit of the same points
            Random rnd = new Random(3);
            GeoPoint[] points = new GeoPoint[40];
            GeoPoint2D[] points2d = new GeoPoint2D[40];
            for (int i = 0; i < points.Length; i++)
            {
                double t = rnd.NextDouble() * 5.0;
                points2d[i] = new GeoPoint2D(-80 + t, 60 - 1.5 * t + 0.3 * (rnd.NextDouble() - 0.5));
                points[i] = new GeoPoint(points2d[i].x, points2d[i].y, 7.0);
            }
            double dev3 = Geometry.LineFit(points, out GeoPoint loc3, out GeoVector dir3);
            double dev2 = Geometry.LineFit(points2d, out GeoPoint2D loc2, out GeoVector2D dir2);
            Assert.AreEqual(dev2, dev3, 1e-9);
            Assert.AreEqual(1.0, Math.Abs(dir3.x * dir2.x + dir3.y * dir2.y), 1e-12);
            Assert.AreEqual(0.0, dir3.z, 1e-12);
        }
    }
}
