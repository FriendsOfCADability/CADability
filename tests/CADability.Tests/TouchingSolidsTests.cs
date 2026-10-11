using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Boolean operations of solids that touch each other in a face, an edge or a point, but have no common volume.
    /// Solid.Intersect threw a NullReferenceException whenever the solids touched in a face: when no face of the result was
    /// left, the shells were tested whether one contains the other, but their faces had been taken apart by then. Where the
    /// solids only touched in an edge or a point, that test took a vertex on the other solid and found it inside: the
    /// intersection was a whole box, and box b minus box a was empty. Two boxes that touch in a whole face gave empty
    /// differences.
    /// </summary>
    [TestClass]
    public class TouchingSolidsTests
    {
        private static Solid Box(double x, double y, double z, double dx, double dy, double dz)
            => Make3D.MakeBox(new GeoPoint(x, y, z), dx * GeoVector.XAxis, dy * GeoVector.YAxis, dz * GeoVector.ZAxis);

        private static double Volume(Solid[] result, string what)
        {
            Assert.IsNotNull(result, what);
            Assert.AreEqual(1, result.Length, what + ": number of solids");
            Assert.AreEqual(0, result[0].Shells[0].OpenEdgesExceptPoles.Length, what + ": open edges");
            return result[0].Volume(1e-3);
        }

        /// <summary>
        /// Checks the operations of <paramref name="a"/> and <paramref name="b"/>, which touch but have no common volume. A union
        /// is only checked where the solids touch in a face: where they touch in an edge or a point, there is no manifold union.
        /// </summary>
        private static void AssertTouching(Func<Solid> a, Func<Solid> b, bool faceContact)
        {
            double va = a().Volume(1e-3), vb = b().Volume(1e-3);
            Solid[] intersection = Solid.Intersect(a(), b());
            Assert.AreEqual(0, intersection.Length, "a * b is empty");
            Assert.AreEqual(va, Volume(Solid.Subtract(a(), b()), "a - b"), va * 1e-6, "a - b");
            Assert.AreEqual(vb, Volume(Solid.Subtract(b(), a()), "b - a"), vb * 1e-6, "b - a");
            if (faceContact) Assert.AreEqual(va + vb, Volume(new[] { Solid.Unite(a(), b()) }, "a + b"), (va + vb) * 1e-6, "a + b");
        }

        [DataTestMethod]
        [DataRow(10.0, 0.0, 0.0, 10.0, 10.0, 10.0, true, DisplayName = "a whole face")]
        [DataRow(0.0, 0.0, 10.0, 10.0, 10.0, 10.0, true, DisplayName = "a whole face, on top")]
        [DataRow(10.0, 2.0, 3.0, 5.0, 5.0, 5.0, true, DisplayName = "a smaller face")]
        [DataRow(10.0, -5.0, -5.0, 10.0, 20.0, 20.0, true, DisplayName = "a larger face")]
        [DataRow(10.0, 5.0, 5.0, 10.0, 10.0, 10.0, true, DisplayName = "faces offset")]
        [DataRow(10.0, 10.0, 0.0, 10.0, 10.0, 10.0, false, DisplayName = "an edge")]
        [DataRow(10.0, 10.0, 3.0, 10.0, 10.0, 4.0, false, DisplayName = "a part of an edge")]
        [DataRow(10.0, 10.0, 10.0, 10.0, 10.0, 10.0, false, DisplayName = "a corner")]
        public void boxes_that_touch(double x, double y, double z, double dx, double dy, double dz, bool faceContact)
        {
            AssertTouching(() => Box(0, 0, 0, 10, 10, 10), () => Box(x, y, z, dx, dy, dz), faceContact);
        }

        [DataTestMethod]
        [DataRow(10.0, 0.0, 10.0, 10.0, 10.0, 10.0, DisplayName = "filling the notch")]
        [DataRow(10.0, 2.0, 10.0, 5.0, 5.0, 5.0, DisplayName = "in the notch")]
        public void a_box_in_the_notch_of_an_l_profile(double x, double y, double z, double dx, double dy, double dz)
        {
            Solid L() => Solid.Unite(Box(0, 0, 0, 20, 10, 10), Box(0, 0, 10, 10, 10, 10));
            AssertTouching(L, () => Box(x, y, z, dx, dy, dz), true);
        }

        [TestMethod]
        public void a_sphere_on_a_box()
        {
            AssertTouching(() => Box(0, 0, 0, 10, 10, 10), () => Make3D.MakeSphere(new GeoPoint(5, 5, 15), 5), false);
        }

        /// <summary>
        /// The same faces, but with a common volume. These threw a NullReferenceException too, when a difference was empty.
        /// </summary>
        [TestMethod]
        public void boxes_with_common_faces()
        {
            Solid A() => Box(0, 0, 0, 10, 10, 10);
            Assert.AreEqual(0, Solid.Subtract(A(), A()).Length, "a - a");
            Assert.AreEqual(1000, Volume(Solid.Intersect(A(), A()), "a * a"), 1e-6, "a * a");
            Solid B() => Box(0, 0, 0, 10, 10, 5);
            Assert.AreEqual(0, Solid.Subtract(B(), A()).Length, "b - a, b in a");
            Assert.AreEqual(500, Volume(Solid.Subtract(A(), B()), "a - b, b in a"), 1e-6, "a - b, b in a");
        }
    }
}
