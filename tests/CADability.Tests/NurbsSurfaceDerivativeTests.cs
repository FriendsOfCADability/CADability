using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability.Tests
{
    /// <summary>
    /// The derivatives of <see cref="NurbsSurface.Derivation2At"/> must be the derivatives of <see cref="NurbsSurface.PointAt"/>.
    /// They are evaluated from the derivative control points of the knot span the point lies in (Nurbs.LocalSurfaceDerivCpts),
    /// indexed relative to that span: these tests cover rational and non rational surfaces of several degrees with uneven
    /// knots, at points in every span.
    /// </summary>
    [TestClass]
    public class NurbsSurfaceDerivativeTests
    {
        private static IEnumerable<(string what, NurbsSurface surface)> Surfaces()
        {
            Random rnd = new Random(23);
            foreach ((int du, int dv, bool rational) in new[] { (3, 3, false), (3, 3, true), (2, 3, true), (3, 2, false), (1, 3, true), (4, 2, true) })
            {
                int nu = du + 4, nv = dv + 3;
                GeoPoint[,] poles = new GeoPoint[nu, nv];
                double[,] weights = rational ? new double[nu, nv] : null;
                for (int i = 0; i < nu; i++)
                {
                    for (int j = 0; j < nv; j++)
                    {
                        poles[i, j] = new GeoPoint(10 * i + 2 * rnd.NextDouble(), 10 * j + 2 * rnd.NextDouble(), 6 * rnd.NextDouble());
                        if (rational) weights[i, j] = 0.6 + 0.8 * rnd.NextDouble();
                    }
                }
                double[] uKnots = new double[nu - du + 1];
                int[] uMults = new int[uKnots.Length];
                for (int i = 0; i < uKnots.Length; i++)
                {
                    uKnots[i] = i == 0 ? 0.0 : uKnots[i - 1] + 0.5 + rnd.NextDouble(); // uneven spans
                    uMults[i] = (i == 0 || i == uKnots.Length - 1) ? du + 1 : 1;
                }
                double[] vKnots = new double[nv - dv + 1];
                int[] vMults = new int[vKnots.Length];
                for (int i = 0; i < vKnots.Length; i++)
                {
                    vKnots[i] = 2.0 + 0.7 * i * i; // uneven, not starting at 0
                    vMults[i] = (i == 0 || i == vKnots.Length - 1) ? dv + 1 : 1;
                }
                NurbsSurface surface = new NurbsSurface(poles, weights, uKnots, vKnots, uMults, vMults, du, dv, false, false);
                yield return ($"degree {du}x{dv}{(rational ? ", rational" : "")}", surface);
            }
        }

        private static void AssertClose(GeoVector expected, GeoVector actual, double relTol, string message)
        {
            double err = (expected - actual).Length / Math.Max(expected.Length, 1.0);
            Assert.IsTrue(err < relTol, $"{message}: expected {expected}, got {actual}, relative error {err:E2}");
        }

        [TestMethod]
        public void Derivation2AtMatchesCentralDifferences()
        {
            foreach ((string what, NurbsSurface surface) in Surfaces())
            {
                surface.GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
                for (int iu = 1; iu < 12; iu++)
                {
                    for (int iv = 1; iv < 10; iv++)
                    {
                        // positions that avoid the knots, where the derivatives of a low degree need not be continuous
                        GeoPoint2D uv = new GeoPoint2D(umin + (umax - umin) * (iu + 0.37) / 12.6, vmin + (vmax - vmin) * (iv + 0.29) / 10.6);
                        surface.Derivation2At(uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv);
                        string at = $"{what} at {uv}";
                        Assert.AreEqual(0.0, location | surface.PointAt(uv), 1e-9, at + ": location");

                        double h = 1e-5;
                        GeoPoint2D up = new GeoPoint2D(uv.x + h, uv.y), um = new GeoPoint2D(uv.x - h, uv.y);
                        GeoPoint2D vp = new GeoPoint2D(uv.x, uv.y + h), vm = new GeoPoint2D(uv.x, uv.y - h);
                        AssertClose((1.0 / (2 * h)) * (surface.PointAt(up) - surface.PointAt(um)), du, 1e-6, at + ": du");
                        AssertClose((1.0 / (2 * h)) * (surface.PointAt(vp) - surface.PointAt(vm)), dv, 1e-6, at + ": dv");
                        AssertClose((1.0 / (2 * h)) * (surface.UDirection(up) - surface.UDirection(um)), duu, 1e-5, at + ": duu");
                        AssertClose((1.0 / (2 * h)) * (surface.VDirection(vp) - surface.VDirection(vm)), dvv, 1e-5, at + ": dvv");
                        AssertClose((1.0 / (2 * h)) * (surface.UDirection(vp) - surface.UDirection(vm)), duv, 1e-5, at + ": duv");
                        AssertClose(surface.UDirection(uv), du, 1e-10, at + ": du against UDirection");
                        AssertClose(surface.VDirection(uv), dv, 1e-10, at + ": dv against VDirection");
                    }
                }
            }
        }
    }
}
