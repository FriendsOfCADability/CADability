using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability.Tests
{
    /// <summary>
    /// The derivatives of <see cref="SurfaceOfRevolution"/> must be the derivatives of its <see cref="SurfaceOfRevolution.PointAt"/>.
    /// v is the parameter of the rotated curve, whereas the derivatives of the curve are by its position, so
    /// VDirection needs the factor d(position)/d(parameter); it was missing for every curve whose parameter
    /// range is not 0...1. Derivation2At passed the parameter as a position to the curve and measured the
    /// angle from an arbitrary direction instead of from the curve, so even its location was wrong.
    /// </summary>
    [TestClass]
    public class SurfaceOfRevolutionDerivativeTests
    {
        private static readonly GeoPoint AxisLocation = new GeoPoint(3, -2, 7);
        // deliberately not normalized
        private static readonly GeoVector AxisDirection = 4.0 * new GeoVector(0.3, -1.7, 0.5);

        private static IEnumerable<(string what, ICurve curve)> Profiles()
        {
            GeoVector axis = AxisDirection.Normalized;
            GeoVector radial = (axis ^ GeoVector.XAxis).Normalized;
            GeoVector across = (axis ^ radial).Normalized;

            BSpline spline = BSpline.Construct();
            spline.SetData(3, new GeoPoint[]
            {
                AxisLocation + 5 * radial, AxisLocation + 8 * radial + 0.8 * axis,
                AxisLocation + 6 * radial + 2 * axis, AxisLocation + 9 * radial + 2.8 * axis,
                AxisLocation + 7 * radial + 4 * axis
            }, null, new double[] { 2, 3.5, 5 }, new int[] { 4, 1, 4 }, false);
            yield return ("BSpline, knots 2 to 5", spline);

            // not in a plane with the axis
            yield return ("line", Line.TwoPoints(AxisLocation + 5 * radial + across, AxisLocation + 9 * radial + 2 * axis + across));

            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(new Plane(AxisLocation, radial, axis), AxisLocation + 10 * radial, 3.0, 0.4, 2.5);
            yield return ("circular arc, sweep 2.5", arc);

            Ellipse reversedArc = Ellipse.Construct();
            reversedArc.SetArcPlaneCenterRadiusAngles(new Plane(AxisLocation, radial, axis), AxisLocation + 10 * radial, 3.0, 2.9, -2.5);
            yield return ("circular arc, sweep -2.5", reversedArc);
        }

        private static void AssertClose(GeoVector expected, GeoVector actual, double relTol, string message)
        {
            double err = (expected - actual).Length / Math.Max(expected.Length, 1e-12);
            Assert.IsTrue(err < relTol, $"{message}: expected {expected}, got {actual}, relative error {err:E2}");
        }

        /// <summary>
        /// Compares UDirection and VDirection with central differences of PointAt, and the second derivatives of
        /// Derivation2At with central differences of UDirection and VDirection.
        /// </summary>
        private static void CheckDerivatives(string what, ISurface surface, double vStart, double vEnd)
        {
            double hu = 1e-5;
            double hv = 1e-5 * Math.Abs(vEnd - vStart);
            foreach (double u in new[] { -2.0, 0.0, 0.7, 2.1, 4.4 })
                foreach (double f in new[] { 0.13, 0.37, 0.61, 0.88 })
                {
                    GeoPoint2D uv = new GeoPoint2D(u, vStart + f * (vEnd - vStart));
                    string at = $"{what} at {uv}";
                    GeoVector fdu = (1.0 / (2 * hu)) * (surface.PointAt(new GeoPoint2D(uv.x + hu, uv.y)) - surface.PointAt(new GeoPoint2D(uv.x - hu, uv.y)));
                    GeoVector fdv = (1.0 / (2 * hv)) * (surface.PointAt(new GeoPoint2D(uv.x, uv.y + hv)) - surface.PointAt(new GeoPoint2D(uv.x, uv.y - hv)));
                    AssertClose(fdu, surface.UDirection(uv), 1e-7, $"UDirection, {at}");
                    AssertClose(fdv, surface.VDirection(uv), 1e-7, $"VDirection, {at}");

                    surface.Derivation2At(uv, out GeoPoint location, out GeoVector du, out GeoVector dv,
                        out GeoVector duu, out GeoVector dvv, out GeoVector duv);
                    Assert.IsTrue((location | surface.PointAt(uv)) < 1e-9, $"Derivation2At location, {at}");
                    AssertClose(fdu, du, 1e-7, $"Derivation2At du, {at}");
                    AssertClose(fdv, dv, 1e-7, $"Derivation2At dv, {at}");
                    GeoVector fduu = (1.0 / (2 * hu)) * (surface.UDirection(new GeoPoint2D(uv.x + hu, uv.y)) - surface.UDirection(new GeoPoint2D(uv.x - hu, uv.y)));
                    GeoVector fdvv = (1.0 / (2 * hv)) * (surface.VDirection(new GeoPoint2D(uv.x, uv.y + hv)) - surface.VDirection(new GeoPoint2D(uv.x, uv.y - hv)));
                    GeoVector fduv = (1.0 / (2 * hu)) * (surface.VDirection(new GeoPoint2D(uv.x + hu, uv.y)) - surface.VDirection(new GeoPoint2D(uv.x - hu, uv.y)));
                    AssertClose(fduu, duu, 1e-6, $"Derivation2At duu, {at}");
                    if (dvv.Length > 1e-8 || fdvv.Length > 1e-6) // a line has no second v derivative
                        AssertClose(fdvv, dvv, 1e-5, $"Derivation2At dvv, {at}");
                    AssertClose(fduv, duv, 1e-6, $"Derivation2At duv, {at}");
                }
        }

        [TestMethod]
        public void the_derivatives_are_the_derivatives_of_PointAt()
        {
            foreach ((string what, ICurve curve) in Profiles())
            {
                SurfaceOfRevolution surface = new SurfaceOfRevolution(curve, AxisLocation, AxisDirection);
                CheckDerivatives(what, surface, curve.PositionToParameter(0.0), curve.PositionToParameter(1.0));
            }
        }

        [TestMethod]
        public void the_derivatives_survive_Modify_and_Clone()
        {
            ModOp m = ModOp.Translate(1, 2, 3) * ModOp.Rotate(new GeoVector(1, 1, 0).Normalized, SweepAngle.Deg(33)) * ModOp.Scale(1.7);
            foreach ((string what, ICurve curve) in Profiles())
            {
                ISurface surface = new SurfaceOfRevolution(curve, AxisLocation, AxisDirection).GetModified(m).Clone();
                CheckDerivatives(what + ", modified", surface, curve.PositionToParameter(0.0), curve.PositionToParameter(1.0));
            }
        }
    }
}
