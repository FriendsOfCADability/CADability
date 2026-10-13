using System;
using CADability.Curve2D;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #421: <c>BSpline2D.DerivationAt</c> dereferenced <c>nubs</c> in the branch for splines that keep their
    /// weights, where only <c>nurbs</c> is set, so it threw a NullReferenceException for every spline built from poles
    /// (the constructor stores the weights even when they are all 1).
    /// </summary>
    [TestClass]
    public class BSpline2DDerivationAtTests
    {
        private static BSpline2D QuarterCircle()
        {   // the rational quadratic Bézier representation of the quarter circle from (1,0) to (0,1)
            GeoPoint2D[] poles = { new GeoPoint2D(1, 0), new GeoPoint2D(1, 1), new GeoPoint2D(0, 1) };
            double[] weights = { 1.0, Math.Sqrt(2.0) / 2.0, 1.0 };
            return new BSpline2D(poles, weights, new double[] { 0.0, 1.0 }, new int[] { 3, 3 }, 2, false, 0.0, 1.0);
        }

        [TestMethod]
        public void RationalSplineGivesTheDerivative()
        {
            BSpline2D bsp = QuarterCircle();
            GeoVector2D derivative = bsp.DerivationAt(0.5);
            Assert.IsTrue(derivative.Length > 0.0);
            // the tangent at the middle of the quarter circle points in the direction (-1, 1)
            Assert.AreEqual(-Math.Sqrt(0.5), derivative.x / derivative.Length, 1e-10);
            Assert.AreEqual(Math.Sqrt(0.5), derivative.y / derivative.Length, 1e-10);
            // DirectionAt returns the same derivative scaled by the parameter range, which is 1 here
            GeoVector2D direction = bsp.DirectionAt(0.5);
            Assert.AreEqual(direction.x, derivative.x, 1e-10);
            Assert.AreEqual(direction.y, derivative.y, 1e-10);
        }

        [TestMethod]
        public void NonRationalSplineGivesTheDerivative()
        {
            GeoPoint2D[] poles = { new GeoPoint2D(0, 0), new GeoPoint2D(1, 2), new GeoPoint2D(3, 2), new GeoPoint2D(4, 0) };
            BSpline2D bsp = new BSpline2D(poles, null, new double[] { 0.0, 1.0 }, new int[] { 4, 4 }, 3, false, 0.0, 1.0);
            GeoVector2D derivative = bsp.DerivationAt(0.5);
            GeoVector2D direction = bsp.DirectionAt(0.5);
            Assert.IsTrue(derivative.Length > 0.0);
            Assert.AreEqual(direction.x, derivative.x, 1e-10);
            Assert.AreEqual(direction.y, derivative.y, 1e-10);
        }
    }
}
