using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Two spheres intersect in a circle. There was no exact solution for that, the general surface intersection
    /// approximated the circle, and building the tetraeder hull of the approximation did not end within minutes, e.g. for
    /// the union of two spheres of radius 5 whose centers are 6 apart. Where the general intersection did return, the
    /// booleans were often empty.
    /// </summary>
    [TestClass]
    public class SphereBooleanTests
    {
        private static Solid Sphere(GeoPoint center, double radius, GeoVector rotationAxis, double degrees)
        {
            Solid sphere = Make3D.MakeSphere(center, radius);
            if (degrees != 0.0) sphere.Modify(ModOp.Rotate(center, rotationAxis, SweepAngle.Deg(degrees)));
            return sphere;
        }

        /// <summary>The volume of the lens two intersecting spheres have in common.</summary>
        private static double Lens(double r1, double r2, double d)
            => Math.PI * Math.Pow(r1 + r2 - d, 2) * (d * d + 2 * d * r2 - 3 * r2 * r2 + 2 * d * r1 + 6 * r2 * r1 - 3 * r1 * r1) / (12 * d);

        private static T WithinAMinute<T>(Func<T> operation, string what)
        {
            Task<T> task = Task.Factory.StartNew(operation, TaskCreationOptions.LongRunning);
            Assert.IsTrue(task.Wait(60000), what + " did not return within a minute");
            return task.Result;
        }

        private static void AssertSolid(Solid solid, double volume, string what)
        {
            Assert.IsNotNull(solid, what);
            Assert.AreEqual(0, solid.Shells[0].OpenEdgesExceptPoles.Length, what + ": open edges");
            Assert.AreEqual(volume, solid.Volume(1e-2), volume * 2e-3, what);
        }

        [DataTestMethod]
        [DataRow(0.0, 0.0, 6.0, 0.0, 0.0, 1.0, 0.0, DisplayName = "coaxial, the seams in the same plane")]
        [DataRow(0.0, 0.0, 6.0, 0.0, 0.0, 1.0, 30.0, DisplayName = "coaxial, the seams turned against each other")]
        [DataRow(2.0, 1.0, 5.0, 1.0, 0.3, 0.2, 70.0, DisplayName = "axes offset")]
        public void two_spheres_can_be_united_intersected_and_subtracted(double x, double y, double z, double ax, double ay, double az, double degrees)
        {
            GeoPoint center = new GeoPoint(x, y, z);
            Solid A() => Sphere(GeoPoint.Origin, 5.0, GeoVector.ZAxis, 0.0);
            Solid B() => Sphere(center, 5.0, new GeoVector(ax, ay, az), degrees);
            double v = 4.0 / 3.0 * Math.PI * 125.0;
            double lens = Lens(5.0, 5.0, center.ToVector().Length);

            AssertSolid(WithinAMinute(() => Solid.Unite(A(), B()), "union"), 2.0 * v - lens, "union");
            AssertSolid(WithinAMinute(() => Solid.Intersect(A(), B()), "intersection").Single(), lens, "intersection");
            AssertSolid(WithinAMinute(() => Solid.Subtract(A(), B()), "difference").Single(), v - lens, "a - b");
            AssertSolid(WithinAMinute(() => Solid.Subtract(B(), A()), "difference").Single(), v - lens, "b - a");
        }

        [TestMethod]
        public void tilted_spheres_can_be_united()
        {
            Solid A() => Sphere(GeoPoint.Origin, 5.0, new GeoVector(1, 1, 1), 40.0);
            Solid B() => Sphere(new GeoPoint(0, 0, 6), 5.0, GeoVector.ZAxis, 0.0);
            double va = 4.0 / 3.0 * Math.PI * 125.0, lens = Lens(5.0, 5.0, 6.0);
            AssertSolid(WithinAMinute(() => Solid.Unite(A(), B()), "union"), 2 * va - lens, "union");
            AssertSolid(WithinAMinute(() => Solid.Intersect(A(), B()), "intersection").Single(), lens, "intersection");
        }

        [TestMethod]
        public void spheres_that_do_not_cross_have_no_intersection_curve()
        {
            SphericalSurface a = (SphericalSurface)Make3D.MakeSphere(GeoPoint.Origin, 5).Shells[0].Faces[0].Surface;
            foreach ((GeoPoint c, double r) in new[] { (new GeoPoint(20, 0, 0), 5.0), (new GeoPoint(10, 0, 0), 5.0), (new GeoPoint(1, 0, 0), 2.0), (GeoPoint.Origin, 3.0) })
            {
                SphericalSurface b = (SphericalSurface)Make3D.MakeSphere(c, r).Shells[0].Faces[0].Surface;
                BoundingRect all = new BoundingRect(0, -Math.PI / 2, 2 * Math.PI, Math.PI / 2);
                Assert.AreEqual(0, a.GetDualSurfaceCurves(all, b, all, null, null).Length, $"apart, touching, inside or concentric: {c} {r}");
            }
        }
    }
}
