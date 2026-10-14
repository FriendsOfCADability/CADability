using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="ShellMetrics.IntegratedVolume(Shell)"/> on faces bounded by intersection curves (issue #459). Two cylinders cross at
    /// right angles with axes that meet, A with radius 5 along z, B with radius 5 + d along x, both 20 long. Their boundaries meet in
    /// <see cref="InterpolatedDualSurfaceCurve"/>s, which turn sharply near the points where the cylinders touch, the closer to
    /// equal radii the sharper. The integrated volume was up to 1e-4 relative off, while a fine triangulation matched the exact volume.
    /// </summary>
    [TestClass]
    public class IntersectionCurveVolumeTests
    {
        /// <summary>
        /// The common volume of the two cylinders: the integral of 4 sqrt(r1² - y²) sqrt(r2² - y²) over |y| &lt; min(r1, r2),
        /// with y = m sin t (Simpson).
        /// </summary>
        private static double Common(double r1, double r2)
        {
            double m = Math.Min(r1, r2);
            double f(double t)
            {
                double y = m * Math.Sin(t);
                return 4 * Math.Sqrt(Math.Max(0, r1 * r1 - y * y)) * Math.Sqrt(Math.Max(0, r2 * r2 - y * y)) * m * Math.Cos(t);
            }
            const int n = 20000;
            double a = -Math.PI / 2, h = Math.PI / n, s = f(a) + f(-a);
            for (int i = 1; i < n; i++) s += f(a + i * h) * (i % 2 == 1 ? 4 : 2);
            return s * h / 3;
        }

        /// <summary>
        /// A union reverses both shells and reverses the result back. Reversing a face replaced the ProjectedCurves of its edges by a
        /// coarse approximation with arcs, so the union of two crossing cylinders of equal radius was 0.05 % too large (#458) and
        /// the union of two spheres with offset centres 0.03 % too small (#459), while the differences and the intersection were exact.
        /// </summary>
        [TestMethod]
        public void the_union_of_crossing_cylinders_of_equal_radius_is_exact()
        {
            Solid a = Make3D.MakeCylinder(new GeoPoint(0, 0, -10), 5 * GeoVector.XAxis, 20 * GeoVector.ZAxis);
            Solid b = Make3D.MakeCylinder(new GeoPoint(-10, 0, 0), 5 * GeoVector.YAxis, 20 * GeoVector.XAxis);
            double expected = 1000 * Math.PI - 16.0 * 125 / 3; // two cylinders minus the Steinmetz solid
            Solid union = Solid.Unite(a, b);
            Assert.IsNotNull(union);
            Assert.AreEqual(expected, ShellMetrics.IntegratedVolume(union.Shells[0]), 1e-6 * expected, "integrated");
            Assert.AreEqual(expected, union.Volume(1e-4), 1e-6 * expected, "triangulated");
        }

        [DataTestMethod]
        [DataRow(2.0, 1.0, 5.0, 5.0, 1.0, 0.3, 0.2, 70.0, DisplayName = "axes offset")]
        [DataRow(1.0, 2.0, 6.0, 3.0, 0.0, 0.0, 1.0, 0.0, DisplayName = "different radii")]
        [DataRow(0.0, 0.0, 6.0, 5.0, 1.0, 0.3, 0.2, 30.0, DisplayName = "coaxial, the second sphere tilted")]
        public void the_union_of_two_spheres_is_exact(double x, double y, double z, double radius, double ax, double ay, double az, double degrees)
        {
            GeoPoint center = new GeoPoint(x, y, z);
            Solid a = Make3D.MakeSphere(GeoPoint.Origin, 5.0);
            Solid b = Make3D.MakeSphere(center, radius);
            if (degrees != 0.0) b.Modify(ModOp.Rotate(center, new GeoVector(ax, ay, az), SweepAngle.Deg(degrees)));
            double d = center.ToVector().Length, r1 = 5.0, r2 = radius;
            double lens = Math.PI * Math.Pow(r1 + r2 - d, 2) * (d * d + 2 * d * r2 - 3 * r2 * r2 + 2 * d * r1 + 6 * r2 * r1 - 3 * r1 * r1) / (12 * d);
            double expected = 4.0 / 3.0 * Math.PI * (r1 * r1 * r1 + r2 * r2 * r2) - lens;
            Solid union = Solid.Unite(a, b);
            Assert.IsNotNull(union);
            // was 2e-4 to 4e-4 relative off with both methods
            Assert.AreEqual(expected, ShellMetrics.IntegratedVolume(union.Shells[0]), 1e-6 * expected, "integrated");
            Assert.AreEqual(expected, union.Volume(1e-4), 1e-5 * expected, "triangulated");
        }

        [DataTestMethod]
        [DataRow(2.5e-3, DisplayName = "B slightly wider")]
        [DataRow(-2.5e-3, DisplayName = "B slightly narrower")]
        [DataRow(0.5, DisplayName = "B wider")]
        public void the_volume_of_crossing_cylinders_is_integrated_exactly(double d)
        {
            Solid A() => Make3D.MakeCylinder(new GeoPoint(0, 0, -10), 5 * GeoVector.XAxis, 20 * GeoVector.ZAxis);
            Solid B() => Make3D.MakeCylinder(new GeoPoint(-10, 0, 0), (5 + d) * GeoVector.YAxis, 20 * GeoVector.XAxis);
            double va = 500 * Math.PI, vb = 20 * Math.PI * (5 + d) * (5 + d), common = Common(5, 5 + d);
            foreach ((string what, Solid[] result, double expected) in new[] {
                ("a - b", Solid.Subtract(A(), B()), va - common),
                ("b - a", Solid.Subtract(B(), A()), vb - common),
                ("a * b", Solid.Intersect(A(), B()), common) })
            {
                Assert.IsTrue(result != null && result.Length > 0, what);
                double integrated = result.Sum(s => s.Shells.Sum(sh => ShellMetrics.IntegratedVolume(sh)));
                // was up to 1e-4 relative off, now about 1e-9
                Assert.AreEqual(expected, integrated, 1e-7 * expected, what);
            }
        }
    }
}
