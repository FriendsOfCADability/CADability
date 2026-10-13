using System;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #430: <see cref="SurfaceHelper.MinMaxCurvature"/> wrote the mixed second derivative into the first
    /// fundamental form (<c>I[1, 0]</c> instead of <c>II[1, 0]</c>) and never set <c>maxCurvature</c>.
    /// </summary>
    [TestClass]
    public class SurfaceCurvatureTests
    {
        [TestMethod]
        public void CylinderHasALineAndTheCrossSectionCircle()
        {
            double radius = 2.0;
            CylindricalSurface cylinder = new CylindricalSurface(GeoPoint.Origin, radius * GeoVector.XAxis, radius * GeoVector.YAxis, GeoVector.ZAxis);
            SurfaceHelper.MinMaxCurvature(cylinder, new GeoPoint2D(0.3, 1.0), out ICurve minCurvature, out ICurve maxCurvature);

            Assert.IsInstanceOfType(minCurvature, typeof(Line), "along the axis the curvature is 0");
            Assert.IsInstanceOfType(maxCurvature, typeof(Ellipse), "across the axis the curvature circle is the cross section");
            Ellipse circle = (Ellipse)maxCurvature;
            Assert.IsTrue(circle.IsCircle);
            Assert.AreEqual(radius, circle.Radius, 1e-8);
            Assert.AreEqual(0.0, circle.Center | new GeoPoint(0, 0, 1), 1e-8, "the center of the curvature circle lies on the axis");
            Line line = (Line)minCurvature;
            Assert.AreEqual(1.0, Math.Abs(line.StartDirection.Normalized * GeoVector.ZAxis), 1e-8, "the line runs along the axis");
        }

        [TestMethod]
        public void SaddleWithMixedDerivativeHasTwoPrincipalCurvatures()
        {   // the bilinear surface (u, v, u*v): its mixed derivative is (0, 0, 1), so the off-diagonal entry of the
            // second fundamental form is not 0 and the typo in the old code made the result wrong
            GeoPoint[,] poles = new GeoPoint[2, 2];
            poles[0, 0] = new GeoPoint(0, 0, 0);
            poles[1, 0] = new GeoPoint(1, 0, 0);
            poles[0, 1] = new GeoPoint(0, 1, 0);
            poles[1, 1] = new GeoPoint(1, 1, 1);
            double[,] weights = { { 1.0, 1.0 }, { 1.0, 1.0 } };
            NurbsSurface saddle = new NurbsSurface(poles, weights, new double[] { 0.0, 1.0 }, new double[] { 0.0, 1.0 }, new int[] { 2, 2 }, new int[] { 2, 2 }, 1, 1, false, false);

            SurfaceHelper.MinMaxCurvature(saddle, new GeoPoint2D(0.5, 0.5), out ICurve minCurvature, out ICurve maxCurvature);

            // at (0.5, 0.5): E = G = 1.25, F = 0.25, L = N = 0, M = 1/sqrt(1.5); the principal curvatures are
            // H ± sqrt(H² - K) with K = -M²/(EG - F²) and H = -F M/(EG - F²)
            double eg = 1.25 * 1.25 - 0.25 * 0.25;
            double m = 1.0 / Math.Sqrt(1.5);
            double gauss = -m * m / eg;
            double mean = -0.25 * m / eg;
            double k1 = mean + Math.Sqrt(mean * mean - gauss); // the smaller one by absolute value
            double k2 = mean - Math.Sqrt(mean * mean - gauss);
            Assert.IsTrue(Math.Abs(k1) < Math.Abs(k2));

            Assert.IsInstanceOfType(minCurvature, typeof(Ellipse));
            Assert.IsInstanceOfType(maxCurvature, typeof(Ellipse));
            Assert.AreEqual(1.0 / Math.Abs(k1), ((Ellipse)minCurvature).Radius, 1e-6);
            Assert.AreEqual(1.0 / Math.Abs(k2), ((Ellipse)maxCurvature).Radius, 1e-6);
        }
    }
}
