using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability.Tests
{
    /// <summary>
    /// The derivatives of <see cref="HelicalSurface"/> must be the derivatives of its <see cref="HelicalSurface.PointAt"/>.
    /// The v parameter runs from curveStartParameter to curveEndParameter, while the 2d profile is evaluated at the
    /// normalized position, so VDirection needs the factor 1 / (curveEndParameter - curveStartParameter). It was
    /// commented out in 2017, which went unnoticed because all helical surfaces made by CADability itself use the
    /// v range 0 to 1. Derivation2At returned a duu that was neither scaled with the radius nor placed by
    /// toSurface, and duv was always the null vector.
    /// </summary>
    [TestClass]
    public class HelicalSurfaceDerivativeTests
    {
        private static readonly GeoPoint AxisLocation = new GeoPoint(3, -2, 7);
        // deliberately not normalized
        private static readonly GeoVector AxisDirection = 4.0 * new GeoVector(0.3, -1.7, 0.5);
        private const double Pitch = 2.7;

        private static GeoVector Radial
        {
            get
            {
                Plane meridian = new Plane(AxisLocation, AxisDirection.Normalized ^ GeoVector.XAxis); // contains the axis
                return (meridian.Normal ^ AxisDirection.Normalized).Normalized;
            }
        }

        /// <summary>
        /// Profile curves in a plane with the axis; they project to a Line2D, an Arc2D and a BSpline2D.
        /// </summary>
        private static IEnumerable<(string what, ICurve curve)> Profiles()
        {
            GeoVector radial = Radial;
            GeoVector axis = AxisDirection.Normalized;

            Line line = Line.TwoPoints(AxisLocation + 5 * radial, AxisLocation + 9 * radial + 2 * axis);
            yield return ("line", line);

            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(new Plane(AxisLocation, radial, axis), AxisLocation + 10 * radial, 3.0, 0.4, 2.5);
            yield return ("circular arc, sweep 2.5", arc);

            Ellipse reversedArc = Ellipse.Construct();
            reversedArc.SetArcPlaneCenterRadiusAngles(new Plane(AxisLocation, radial, axis), AxisLocation + 10 * radial, 3.0, 2.9, -2.5);
            yield return ("circular arc, sweep -2.5", reversedArc);

            BSpline spline = BSpline.Construct();
            spline.SetData(3, new GeoPoint[]
            {
                AxisLocation + 5 * radial, AxisLocation + 8 * radial + 0.8 * axis,
                AxisLocation + 6 * radial + 2 * axis, AxisLocation + 9 * radial + 2.8 * axis,
                AxisLocation + 7 * radial + 4 * axis
            }, null, new double[] { 2, 3.5, 5 }, new int[] { 4, 1, 4 }, false);
            yield return ("BSpline", spline);
        }

        private static readonly (double start, double end)[] VRanges = { (0.0, 1.0), (2.0, 5.0), (-0.3, 0.2), (4.0, 1.0) };

        /// <summary>
        /// Sample points strictly inside the v range and away from the knot of the BSpline.
        /// </summary>
        private static IEnumerable<GeoPoint2D> Samples(double vStart, double vEnd)
        {
            double[] us = { -2.0, 0.0, 0.7, 2.1, 4.4, 9.0 };
            double[] positions = { 0.13, 0.37, 0.61, 0.88 };
            foreach (double u in us)
                foreach (double pos in positions)
                    yield return new GeoPoint2D(u, vStart + pos * (vEnd - vStart));
        }

        private static (GeoVector du, GeoVector dv) Differenced(ISurface surface, GeoPoint2D uv, double hu, double hv)
        {
            GeoVector du = (1.0 / (2 * hu)) * (surface.PointAt(new GeoPoint2D(uv.x + hu, uv.y)) - surface.PointAt(new GeoPoint2D(uv.x - hu, uv.y)));
            GeoVector dv = (1.0 / (2 * hv)) * (surface.PointAt(new GeoPoint2D(uv.x, uv.y + hv)) - surface.PointAt(new GeoPoint2D(uv.x, uv.y - hv)));
            return (du, dv);
        }

        private static void AssertClose(GeoVector expected, GeoVector actual, double relTol, string message)
        {
            double err = (expected - actual).Length / Math.Max(expected.Length, 1e-12);
            Assert.IsTrue(err < relTol, $"{message}: expected {expected}, got {actual}, relative error {err:E2}");
        }

        /// <summary>
        /// Compares UDirection, VDirection and DerivativeAt with central differences of PointAt, and Derivation2At
        /// with central differences of DerivativeAt.
        /// </summary>
        private static void CheckDerivatives(string what, ISurface surface, double vStart, double vEnd)
        {
            double hu = 1e-5;
            double hv = 1e-5 * Math.Abs(vEnd - vStart);
            foreach (GeoPoint2D uv in Samples(vStart, vEnd))
            {
                (GeoVector fdu, GeoVector fdv) = Differenced(surface, uv, hu, hv);
                string at = $"{what} at {uv}";
                AssertClose(fdu, surface.UDirection(uv), 1e-7, $"UDirection, {at}");
                AssertClose(fdv, surface.VDirection(uv), 1e-7, $"VDirection, {at}");
                surface.DerivationAt(uv, out GeoPoint location, out GeoVector du, out GeoVector dv);
                Assert.IsTrue((location | surface.PointAt(uv)) < 1e-10, $"DerivativeAt location, {at}");
                AssertClose(fdu, du, 1e-7, $"DerivativeAt du, {at}");
                AssertClose(fdv, dv, 1e-7, $"DerivativeAt dv, {at}");

                surface.Derivation2At(uv, out GeoPoint location2, out GeoVector du2, out GeoVector dv2,
                    out GeoVector duu, out GeoVector dvv, out GeoVector duv);
                Assert.IsTrue((location2 | surface.PointAt(uv)) < 1e-9, $"Derivation2At location, {at}");
                AssertClose(fdu, du2, 1e-7, $"Derivation2At du, {at}");
                AssertClose(fdv, dv2, 1e-7, $"Derivation2At dv, {at}");
                surface.DerivationAt(new GeoPoint2D(uv.x + hu, uv.y), out _, out GeoVector duP, out GeoVector dvuP);
                surface.DerivationAt(new GeoPoint2D(uv.x - hu, uv.y), out _, out GeoVector duM, out GeoVector dvuM);
                surface.DerivationAt(new GeoPoint2D(uv.x, uv.y + hv), out _, out _, out GeoVector dvP);
                surface.DerivationAt(new GeoPoint2D(uv.x, uv.y - hv), out _, out _, out GeoVector dvM);
                AssertClose((1.0 / (2 * hu)) * (duP - duM), duu, 1e-6, $"Derivation2At duu, {at}");
                if (dvv.Length > 1e-8 || ((dvP - dvM).Length > 1e-8 * hv)) // a line has no second v derivative
                    AssertClose((1.0 / (2 * hv)) * (dvP - dvM), dvv, 1e-5, $"Derivation2At dvv, {at}");
                AssertClose((1.0 / (2 * hu)) * (dvuP - dvuM), duv, 1e-6, $"Derivation2At duv, {at}");
            }
        }

        [TestMethod]
        public void the_derivatives_are_the_derivatives_of_PointAt()
        {
            foreach ((string what, ICurve curve) in Profiles())
            {
                foreach ((double vStart, double vEnd) in VRanges)
                {
                    HelicalSurface surface = new HelicalSurface(curve, AxisLocation, AxisDirection, Pitch, vStart, vEnd);
                    CheckDerivatives($"{what}, v from {vStart} to {vEnd}", surface, vStart, vEnd);
                }
            }
        }

        [TestMethod]
        public void the_derivatives_survive_Modify_Clone_and_ReverseOrientation()
        {
            ModOp m = ModOp.Translate(1, 2, 3) * ModOp.Rotate(new GeoVector(1, 1, 0).Normalized, SweepAngle.Deg(33)) * ModOp.Scale(1.7);
            foreach ((string what, ICurve curve) in Profiles())
            {
                ISurface surface = new HelicalSurface(curve, AxisLocation, AxisDirection, Pitch, 2.0, 5.0).GetModified(m).Clone();
                CheckDerivatives(what + ", modified", surface, 2.0, 5.0);
                surface.ReverseOrientation();
                CheckDerivatives(what + ", modified and reversed", surface, 2.0, 5.0);
            }
        }

        /// <summary>
        /// An arc that pretends to have no analytic second derivative, like any curve that keeps the default
        /// <see cref="GeneralCurve2D.TryPointDeriv2At"/>.
        /// </summary>
        private class ArcWithoutSecondDerivative : Arc2D
        {
            public ArcWithoutSecondDerivative(GeoPoint2D center, double radius, Angle start, SweepAngle sweep) : base(center, radius, start, sweep) { }
            public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
            {
                point = PointAt(position);
                deriv = DirectionAt(position);
                deriv2 = GeoVector2D.NullVector;
                return false;
            }
        }

        /// <summary>
        /// When the profile curve cannot provide its second derivative, Derivation2At differences the first derivative
        /// of the profile and must still agree with the analytic result.
        /// </summary>
        [TestMethod]
        public void the_second_derivatives_without_an_analytic_profile_derivative()
        {
            ModOp toSurface = ModOp.Translate(1, 2, 3) * ModOp.Rotate(new GeoVector(1, 1, 0).Normalized, SweepAngle.Deg(33));
            Arc2D analytic = new Arc2D(new GeoPoint2D(10, 1), 3.0, new Angle(0.4), new SweepAngle(2.5));
            Arc2D differenced = new ArcWithoutSecondDerivative(new GeoPoint2D(10, 1), 3.0, new Angle(0.4), new SweepAngle(2.5));
            HelicalSurface expected = new HelicalSurface(analytic, Pitch, toSurface, 2.0, 5.0, 0.0);
            HelicalSurface actual = new HelicalSurface(differenced, Pitch, toSurface, 2.0, 5.0, 0.0);
            foreach (GeoPoint2D uv in Samples(2.0, 5.0))
            {
                expected.Derivation2At(uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv);
                actual.Derivation2At(uv, out GeoPoint location2, out GeoVector du2, out GeoVector dv2, out GeoVector duu2, out GeoVector dvv2, out GeoVector duv2);
                Assert.IsTrue((location | location2) < 1e-10, $"location at {uv}");
                AssertClose(du, du2, 1e-12, $"du at {uv}");
                AssertClose(dv, dv2, 1e-12, $"dv at {uv}");
                AssertClose(duu, duu2, 1e-12, $"duu at {uv}");
                AssertClose(dvv, dvv2, 1e-6, $"dvv at {uv}");
                AssertClose(duv, duv2, 1e-12, $"duv at {uv}");
            }
        }

        /// <summary>
        /// Where the profile touches the axis, the rotation contributes nothing to the u derivative, but the pitch
        /// still does: the surface is no pole there.
        /// </summary>
        [TestMethod]
        public void the_u_derivative_on_the_axis_is_the_pitch()
        {
            Line line = Line.TwoPoints(AxisLocation, AxisLocation + 6 * Radial + 1.5 * AxisDirection.Normalized);
            HelicalSurface surface = new HelicalSurface(line, AxisLocation, AxisDirection, Pitch, 2.0, 5.0);
            foreach (double u in new double[] { 0.0, 1.3, -4.0 })
            {
                GeoPoint2D uv = new GeoPoint2D(u, 2.0);
                GeoVector expected = (Pitch / (2.0 * Math.PI)) * AxisDirection.Normalized;
                AssertClose(expected, surface.UDirection(uv), 1e-12, $"UDirection on the axis at u = {u}");
                surface.Derivation2At(uv, out _, out GeoVector du, out _, out GeoVector duu, out _, out _);
                AssertClose(expected, du, 1e-12, $"Derivation2At du on the axis at u = {u}");
                Assert.IsTrue(duu.Length < 1e-12, $"Derivation2At duu on the axis at u = {u}: {duu}");
            }
        }
    }
}
