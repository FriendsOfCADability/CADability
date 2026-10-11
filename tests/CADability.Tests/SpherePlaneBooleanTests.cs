using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// A sphere and a plane intersect in a circle. SphericalSurface.GetDualSurfaceCurves returned the whole circle for each
    /// face of a sphere, with a 2d curve that ran over the full period, so booleans of spheres and planar faces were wrong:
    /// a box minus a sphere centred on its top face returned the box unchanged, other positions gave negative volumes or
    /// a stack overflow.
    /// </summary>
    [TestClass]
    public class SpherePlaneBooleanTests
    {
        private static Solid Box() => Make3D.MakeBox(new GeoPoint(-10, -10, 0), 20 * GeoVector.XAxis, 20 * GeoVector.YAxis, 10 * GeoVector.ZAxis);

        /// <summary>The volume of the part of a sphere of radius r below a plane at the height h above its bottom.</summary>
        private static double Cap(double r, double h) => Math.PI * h * h * (3 * r - h) / 3;

        private static T WithinAMinute<T>(Func<T> operation, string what)
        {
            Task<T> task = Task.Factory.StartNew(operation, TaskCreationOptions.LongRunning);
            Assert.IsTrue(task.Wait(60000), what + " did not return within a minute");
            return task.Result;
        }

        private static void AssertVolume(Solid[] solids, double volume, string what)
        {
            Assert.IsNotNull(solids, what);
            Assert.AreEqual(1, solids.Length, what + ": number of solids");
            Assert.AreEqual(0, solids[0].Shells[0].OpenEdgesExceptPoles.Length, what + ": open edges");
            Assert.AreEqual(volume, solids[0].Volume(1e-2), Math.Max(volume, 100.0) * 2e-3, what);
        }

        [DataTestMethod]
        [DataRow(10.0, 0.0, DisplayName = "centred on the top face")]
        [DataRow(10.5, 0.0, DisplayName = "above the top face")]
        [DataRow(9.5, 0.0, DisplayName = "below the top face")]
        [DataRow(10.0, 30.0, DisplayName = "centred on the top face, turned")]
        public void a_sphere_can_be_subtracted_from_a_box(double z, double degrees)
        {
            GeoPoint center = new GeoPoint(0, 0, z);
            Solid Sphere()
            {
                Solid sphere = Make3D.MakeSphere(center, 4);
                if (degrees != 0.0) sphere.Modify(ModOp.Rotate(center, new GeoVector(1, 0.3, 0.2), SweepAngle.Deg(degrees)));
                return sphere;
            }
            double inside = Cap(4, 4 + 10 - z), sphere = 4.0 / 3.0 * Math.PI * 64;

            AssertVolume(WithinAMinute(() => Solid.Subtract(Box(), Sphere()), "box - sphere"), 4000 - inside, "box - sphere");
            AssertVolume(WithinAMinute(() => Solid.Intersect(Box(), Sphere()), "box * sphere"), inside, "box * sphere");
            AssertVolume(WithinAMinute(() => new[] { Solid.Unite(Box(), Sphere()) }, "box + sphere"), 4000 + sphere - inside, "box + sphere");
            AssertVolume(WithinAMinute(() => Solid.Subtract(Sphere(), Box()), "sphere - box"), sphere - inside, "sphere - box");
        }

        [TestMethod]
        public void a_sphere_on_an_edge_of_a_box_and_a_sphere_in_a_slab()
        {
            double sphere4 = 4.0 / 3.0 * Math.PI * 64;
            // centred on an edge of the box: a quarter of the sphere is inside
            Solid OnEdge() => Make3D.MakeSphere(new GeoPoint(10, 0, 10), 4);
            AssertVolume(WithinAMinute(() => Solid.Intersect(OnEdge(), Box()), "on edge"), sphere4 / 4, "sphere * box, centred on an edge");
            AssertVolume(WithinAMinute(() => Solid.Subtract(OnEdge(), Box()), "on edge"), sphere4 * 3 / 4, "sphere - box, centred on an edge");

            // a sphere of radius 5 and a slab from z = -1 to 1: the zone between the two planes
            Solid Slab() => Make3D.MakeBox(new GeoPoint(-10, -10, -1), 20 * GeoVector.XAxis, 20 * GeoVector.YAxis, 2 * GeoVector.ZAxis);
            double zone = Math.PI * (25 * 2 - 2.0 / 3.0), sphere5 = 4.0 / 3.0 * Math.PI * 125;
            AssertVolume(WithinAMinute(() => Solid.Intersect(Make3D.MakeSphere(GeoPoint.Origin, 5), Slab()), "slab"), zone, "sphere * slab");
            Solid[] apart = WithinAMinute(() => Solid.Subtract(Make3D.MakeSphere(GeoPoint.Origin, 5), Slab()), "slab");
            Assert.AreEqual(2, apart.Length, "the slab cuts the sphere in two");
            Assert.AreEqual(sphere5 - zone, apart.Sum(s => s.Volume(1e-2)), (sphere5 - zone) * 2e-3, "sphere - slab");
        }
    }
}
