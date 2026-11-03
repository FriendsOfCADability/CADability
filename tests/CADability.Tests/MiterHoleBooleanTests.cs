using CADability.GeoObject;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #512: the corner of a frame of two mitered bars, each with a round hole of the same radius in the same place of the
    /// profile. The hole cylinders cross like a Steinmetz solid, one of their two intersection ellipses is the common edge of both
    /// hole faces on the cut, where the bars touch. <see cref="Solid.Unite"/> returned null and <see cref="Solid.Subtract"/> no
    /// solid at all. The intersection curves of the touching faces were made into new edges, although they lay on existing
    /// edges of both faces: the check for that only worked, when the curve had exactly two intersection vertices, but this one
    /// is split at the singular points, where the ellipses cross. And a singular point was split only into the edge of one of
    /// the bars, so the coinciding edges of the two bars ended in different vertices and could not be connected.
    /// </summary>
    /// <remarks>
    /// Both bars have the profile 40 x 30 with the hole around (20, 15), the vertical one along z, the horizontal one along x.
    /// They meet on the cut x + z = 100. Every fibre through the centroid (20, 15) of the profile has the length 100, so a bar
    /// has the volume 100 times the area of its profile.
    /// </remarks>
    [TestClass]
    public class MiterHoleBooleanTests
    {
        private static SimpleShape Profile(double holeRadius) =>
            new SimpleShape(Border.MakeRectangle(0, 40, 0, 30), Border.MakeCircle(new GeoPoint2D(20, 15), holeRadius));

        /// <summary>the vertical bar, its end cut on x + z = 100 for a profile going on in +x</summary>
        private static Solid Vertical(double holeRadius) =>
            Make3D.MakePrismMiter(Profile(holeRadius), GeoPoint.Origin, GeoVector.YAxis, 100 * GeoVector.ZAxis, -GeoVector.XAxis, GeoVector.XAxis);

        /// <summary>the horizontal bar, its start cut on the same plane for the profile coming from -z</summary>
        private static Solid Horizontal(double holeRadius) =>
            Make3D.MakePrismMiter(Profile(holeRadius), new GeoPoint(0, 0, 100), GeoVector.YAxis, 100 * GeoVector.XAxis, -GeoVector.ZAxis, GeoVector.ZAxis);

        private static double BarVolume(double holeRadius) => 100 * (1200 - Math.PI * holeRadius * holeRadius);

        private static void AssertClosedAndConsistent(Solid solid)
        {
            foreach (Shell shell in solid.Shells)
            {
                Assert.AreEqual(0, shell.OpenEdgesExceptPoles.Length, "the result has open edges");
                Assert.IsTrue(shell.CheckConsistency(), "the result is not consistent");
            }
        }

        [TestMethod]
        public void the_bars_have_the_expected_volume()
        {
            Assert.AreEqual(BarVolume(5), Vertical(5).Volume(1e-3), 0.1);
            Assert.AreEqual(BarVolume(5), Horizontal(5).Volume(1e-3), 0.1);
        }

        [DataTestMethod]
        [DataRow(5.0, DisplayName = "equal holes (#512)")]
        [DataRow(4.0, DisplayName = "different holes")]
        public void the_bars_unite(double horizontalHole)
        {
            Solid corner = Solid.Unite(Vertical(5), Horizontal(horizontalHole));
            Assert.IsNotNull(corner, "the bars touch and must unite");
            Assert.AreEqual(1, corner.Shells.Length);
            AssertClosedAndConsistent(corner);
            Assert.AreEqual(BarVolume(5) + BarVolume(horizontalHole), corner.Volume(1e-3), 0.2);
        }

        [DataTestMethod]
        [DataRow(5.0, DisplayName = "equal holes (#512)")]
        [DataRow(4.0, DisplayName = "different holes")]
        public void subtracting_the_horizontal_bar_leaves_the_vertical_one(double horizontalHole)
        {
            Solid[] rest = Solid.Subtract(Vertical(5), Horizontal(horizontalHole));
            Assert.AreEqual(1, rest.Length);
            AssertClosedAndConsistent(rest[0]);
            Assert.AreEqual(BarVolume(5), rest[0].Volume(1e-3), 0.1);
        }

        [DataTestMethod]
        [DataRow(5.0, DisplayName = "equal holes (#512)")]
        [DataRow(4.0, DisplayName = "different holes")]
        public void subtracting_the_vertical_bar_leaves_the_horizontal_one(double horizontalHole)
        {
            Solid[] rest = Solid.Subtract(Horizontal(horizontalHole), Vertical(5));
            Assert.AreEqual(1, rest.Length);
            AssertClosedAndConsistent(rest[0]);
            Assert.AreEqual(BarVolume(horizontalHole), rest[0].Volume(1e-3), 0.1);
        }

        [DataTestMethod]
        [DataRow(5.0, DisplayName = "equal holes (#512)")]
        [DataRow(4.0, DisplayName = "different holes")]
        public void the_bars_have_nothing_in_common(double horizontalHole)
        {
            Solid[] common = Solid.Intersect(Vertical(5), Horizontal(horizontalHole));
            Assert.AreEqual(0.0, common.Sum(s => s.Volume(1e-3)), 0.1);
        }

        /// <summary>
        /// A box pressed into the corner from outside cuts both bars, also the hole faces near the common ellipse, so the united
        /// corner must stay usable for further operations.
        /// </summary>
        [TestMethod]
        public void the_united_corner_can_be_cut_further()
        {
            Solid corner = Solid.Unite(Vertical(5), Horizontal(5));
            Assert.IsNotNull(corner);
            // the slab 12 < y < 18 through the middle of both holes, which lies inside the profile between y = 0 and y = 30
            Solid slab = Make3D.MakeBox(new GeoPoint(-10, 12, -50), 160 * GeoVector.XAxis, 6 * GeoVector.YAxis, 160 * GeoVector.ZAxis);
            Solid[] rest = Solid.Subtract(corner, slab);
            Assert.AreEqual(2, rest.Length, "the slab cuts the corner into a front and a back part");
            foreach (Solid s in rest) AssertClosedAndConsistent(s);
            // the slab removes the part of both bars with 12 < y < 18: the profile there is the strip 40 x 6 minus the hole
            double holeInStrip = 2 * (3 * Math.Sqrt(25 - 9) + 25 * Math.Asin(3.0 / 5.0)); // area of the circle of radius 5 with |y - 15| < 3
            double removed = 2 * 100 * (240 - holeInStrip);
            Assert.AreEqual(2 * BarVolume(5) - removed, rest.Sum(s => s.Volume(1e-3)), 0.5);
        }
    }
}
