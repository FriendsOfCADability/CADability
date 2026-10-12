using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Boolean operations of solids whose faces lie very close to each other, reduced from issue #168
    /// ("Solid.Substract returns null on large objects"). The data of the issue is tested in
    /// <see cref="IssueFilesRegressionTests"/>, these are the two effects it consists of, built from scratch.
    /// </summary>
    [TestClass]
    public class BooleanNearlyCoincidentTests
    {
        private static Solid Box(double x, double y, double z, double dx, double dy, double dz)
            => Make3D.MakeBox(new GeoPoint(x, y, z), dx * GeoVector.XAxis, dy * GeoVector.YAxis, dz * GeoVector.ZAxis);

        /// <summary>
        /// A bar of 40 x 20 along y from 0 to <paramref name="length"/>, its edge along y at x == 0, z == 0 is rounded with radius 3.
        /// </summary>
        private static Solid FilletBar(double length)
        {
            const double r = 3.0;
            Plane xz = new Plane(GeoPoint.Origin, GeoVector.XAxis, GeoVector.ZAxis);
            Ellipse arc = Ellipse.Construct();
            arc.SetArc3Points(new GeoPoint(0, 0, r), new GeoPoint(r - r * Math.Sqrt(0.5), 0, r - r * Math.Sqrt(0.5)), new GeoPoint(r, 0, 0), xz);
            GeoObject.Path outline = GeoObject.Path.Construct();
            outline.Set(new ICurve[] {
                Line.TwoPoints(new GeoPoint(r, 0, 0), new GeoPoint(40, 0, 0)),
                Line.TwoPoints(new GeoPoint(40, 0, 0), new GeoPoint(40, 0, 20)),
                Line.TwoPoints(new GeoPoint(40, 0, 20), new GeoPoint(0, 0, 20)),
                Line.TwoPoints(new GeoPoint(0, 0, 20), new GeoPoint(0, 0, r)),
                arc });
            Face profile = Face.MakeFace(new GeoObjectList(outline));
            return Make3D.MakePrism(profile, length * GeoVector.YAxis, null) as Solid;
        }

        private static Solid Single(Solid[] result, string what)
        {
            Assert.IsNotNull(result, what);
            Assert.AreEqual(1, result.Length, what + ": number of solids");
            Shell shell = result[0].Shells[0];
            Assert.AreEqual(0, shell.OpenEdges.Length, what + ": open edges");
            foreach (Face face in shell.Faces) Assert.IsTrue(face.CheckConsistency(), what + ": inconsistent face");
            return result[0];
        }

        /// <summary>
        /// Checks a - b, b - a and a * b against the volume of the common part.
        /// </summary>
        private static void AssertBooleans(Solid a, Solid b, double common, double tolerance)
        {
            double va = a.Volume(1e-3), vb = b.Volume(1e-3);
            Assert.AreEqual(common, Single(Solid.Intersect(a, b), "a * b").Volume(1e-3), tolerance, "a * b");
            Assert.AreEqual(va - common, Single(Solid.Subtract(a, b), "a - b").Volume(1e-3), tolerance, "a - b");
            Assert.AreEqual(vb - common, Single(Solid.Subtract(b, a), "b - a").Volume(1e-3), tolerance, "b - a");
        }

        /// <summary>
        /// The precision of the operation used to be 1e-6 of the size of both solids together. With a bar of 3000 it was
        /// 3e-3, so a face 2.5e-3 away from the bar's face vanished, and the results were empty. A bar of 100 always worked.
        /// </summary>
        [DataTestMethod]
        [DataRow(2.5e-3)]
        [DataRow(1e-3)]
        public void a_face_close_to_a_face_of_a_long_bar_is_kept(double gap)
        {
            Solid bar = Box(0, 0, 0, 40, 3000, 80);
            Solid box = Box(-gap, -50, 10, 20 + gap, 80, 40); // its face at x == -gap is parallel to the bar's face at x == 0
            AssertBooleans(box, bar, 20 * 30 * 40, 0.01);
        }

        /// <summary>
        /// The bottom of the box lies (almost) in the bottom plane of the bar, which is tangent to the rounded edge. 1e-7 above
        /// that plane, the bottom edge of the box enters the cylinder and leaves it again sqrt(2 * 3 * 1e-7) == 7.7e-4 further on,
        /// i.e. the intersection point of the box edge and the rounded face moved 7.7e-4 away from the tangent edge, while the
        /// intersection of the two surfaces, which are tangential within the precision, was the tangent line. The two did not
        /// match, an intersection edge was missing, and box - bar was empty.
        /// </summary>
        [DataTestMethod]
        [DataRow(0.0, 1e-7)]
        [DataRow(0.0, 3e-7)]
        [DataRow(0.0, 1e-6)]
        [DataRow(3e-8, 0.0)]
        [DataRow(3e-8, 3e-7)]
        [DataRow(3e-8, -3e-7)]
        [DataRow(3e-8, 1e-6)]
        public void a_box_nearly_on_the_tangent_plane_of_a_rounded_edge(double tilt, double dz)
        {
            Solid bar = FilletBar(100);
            Solid box = Box(-1, -50, dz, 21, 80, 10);
            if (tilt != 0.0) box.Modify(ModOp.Rotate(new GeoPoint(0, -10, dz), GeoVector.XAxis, new SweepAngle(tilt)));
            // the common part is 20 x 10 of the profile, without the corner outside the rounding, and 30 long
            double common = 30 * (200 - (9 - 9 * Math.PI / 4));
            AssertBooleans(box, bar, common, 1e-3);
        }
    }
}
