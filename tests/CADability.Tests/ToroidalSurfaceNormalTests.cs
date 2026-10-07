using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="ToroidalSurface.GetNormal(GeoPoint2D)"/>, especially for a spindle torus
    /// (minor radius greater than major radius), where the u-direction degenerates at the poles and
    /// the cross product UDirection ^ VDirection is the nullvector, although a normal vector exists.
    /// </summary>
    [TestClass]
    public class ToroidalSurfaceNormalTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>
        /// A torus which is neither axis aligned nor unscaled, so a wrong handling of toTorus would show up.
        /// </summary>
        private static ToroidalSurface MakeTorus(double majorRadius, double minorRadius)
        {
            ModOp rot = ModOp.Rotate(new GeoVector(1, 2, 3).Normalized, SweepAngle.Deg(37));
            return new ToroidalSurface(new GeoPoint(10, -20, 5), rot * GeoVector.XAxis, rot * GeoVector.YAxis, rot * GeoVector.ZAxis,
                majorRadius, minorRadius);
        }

        /// <summary>
        /// Away from the poles GetNormal must be identical to the normalized cross product of the two
        /// derivatives, no matter whether it is an ordinary or a spindle torus, and no matter whether
        /// the surface is reverse oriented.
        /// </summary>
        [DataTestMethod]
        [DataRow(30.0, 10.0, false)] // ordinary torus
        [DataRow(30.0, 10.0, true)]
        [DataRow(10.0, 20.0, false)] // spindle torus: the poles are at cos(v) == -1/2
        [DataRow(10.0, 20.0, true)]
        public void NormalMatchesCrossProduct(double majorRadius, double minorRadius, bool reversed)
        {
            ToroidalSurface ts = MakeTorus(majorRadius, minorRadius);
            if (reversed) ts.ReverseOrientation();
            double[] singularities = ts.GetVSingularities();
            for (int i = 0; i < 24; i++)
            {
                double u = i * 2 * Math.PI / 24;
                for (int j = 0; j < 24; j++)
                {
                    double v = j * 2 * Math.PI / 24 + 0.031; // avoid hitting a pole exactly
                    bool closeToPole = false;
                    for (int k = 0; k < singularities.Length; k++)
                    {
                        if (Math.Abs(Math.IEEERemainder(v - singularities[k], 2 * Math.PI)) < 1e-3) closeToPole = true;
                    }
                    if (closeToPole) continue; // there the cross product is (almost) the nullvector
                    GeoPoint2D uv = new GeoPoint2D(u, v);
                    GeoVector expected = (ts.UDirection(uv) ^ ts.VDirection(uv)).Normalized;
                    GeoVector normal = ts.GetNormal(uv);
                    Assert.AreEqual(1.0, normal.Length, 1e-10, $"normal not normalized at {uv}");
                    Assert.AreEqual(1.0, expected * normal, 1e-9, $"wrong normal at {uv}");
                }
            }
        }

        /// <summary>
        /// At a pole of the spindle torus the normal must still be a valid (unit) vector, perpendicular to the
        /// v-direction, and it must be the limit of the normals when approaching the pole from the outer part
        /// of the surface.
        /// </summary>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void NormalAtPole(bool reversed)
        {
            const double majorRadius = 10.0, minorRadius = 20.0;
            ToroidalSurface ts = MakeTorus(majorRadius, minorRadius);
            if (reversed) ts.ReverseOrientation();
            double[] poles = ts.GetVSingularities();
            Assert.AreEqual(2, poles.Length);
            foreach (double vPole in poles)
            {
                // the u-direction is degenerated here, all u map onto the same point on the axis
                GeoPoint onAxis = ts.PointAt(new GeoPoint2D(0.0, vPole));
                for (int i = 0; i < 12; i++)
                {
                    double u = i * 2 * Math.PI / 12;
                    GeoPoint2D uv = new GeoPoint2D(u, vPole);
                    Assert.IsTrue(ts.UDirection(uv).Length < 1e-8, "expected a degenerated u-direction at the pole");
                    Assert.IsTrue((ts.PointAt(uv) | onAxis) < 1e-8, "expected a single point at the pole");
                    GeoVector normal = ts.GetNormal(uv);
                    Assert.AreEqual(1.0, normal.Length, 1e-10, $"normal not normalized at the pole {uv}");
                    Assert.AreEqual(0.0, normal * ts.VDirection(uv).Normalized, 1e-10, $"normal not perpendicular to v-direction at {uv}");

                    // approach the pole from the outer part of the surface (1 + minorRadius*cos(v) > 0) and
                    // compare with the cross product there
                    double outer = 1.0 + minorRadius / majorRadius * Math.Cos(vPole - 1e-4) > 0 ? vPole - 1e-4 : vPole + 1e-4;
                    GeoPoint2D uvOuter = new GeoPoint2D(u, outer);
                    GeoVector approached = (ts.UDirection(uvOuter) ^ ts.VDirection(uvOuter)).Normalized;
                    Assert.AreEqual(1.0, approached * normal, 1e-6, $"normal at the pole {uv} is not the limit from the outer side");

                    // and from the inner (spindle) part it must be the exact opposite
                    double inner = outer > vPole ? vPole - 1e-4 : vPole + 1e-4;
                    GeoPoint2D uvInner = new GeoPoint2D(u, inner);
                    GeoVector fromInner = (ts.UDirection(uvInner) ^ ts.VDirection(uvInner)).Normalized;
                    Assert.AreEqual(-1.0, fromInner * normal, 1e-6, $"normal at the pole {uv} does not flip on the inner side");
                }
            }
        }

        /// <summary>
        /// <see cref="ISurface.ReverseOrientation"/> must invert the normal, also at the poles of a spindle torus.
        /// </summary>
        [TestMethod]
        public void ReverseOrientationInvertsNormalAtPole()
        {
            ToroidalSurface ts = MakeTorus(10.0, 20.0);
            ToroidalSurface reversed = (ToroidalSurface)ts.Clone();
            ModOp2D toReversed = reversed.ReverseOrientation();
            double[] poles = ts.GetVSingularities();
            foreach (double vPole in poles)
            {
                for (int i = 0; i < 12; i++)
                {
                    GeoPoint2D uv = new GeoPoint2D(i * 2 * Math.PI / 12, vPole);
                    GeoPoint2D uvr = toReversed * uv;
                    Assert.IsTrue((ts.PointAt(uv) | reversed.PointAt(uvr)) < 1e-8, "reversed surface has a different point");
                    Assert.AreEqual(-1.0, ts.GetNormal(uv) * reversed.GetNormal(uvr), 1e-9, $"normal not inverted at {uv}");
                }
            }
        }
    }
}
