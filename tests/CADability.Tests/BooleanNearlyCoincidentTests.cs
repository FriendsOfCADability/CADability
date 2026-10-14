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
        // below the tangent plane the box does not cut the rounding, and 1e-5 above it, the cut is too far from the tangent edge to
        // be moved onto it: the bottom of the box was not split along the tangent edge, box - bar was empty (issue #400)
        [DataRow(0.0, -1e-7)]
        [DataRow(0.0, -3e-7)]
        [DataRow(0.0, 1e-5)]
        [DataRow(3e-8, 1e-5)]
        public void a_box_nearly_on_the_tangent_plane_of_a_rounded_edge(double tilt, double dz)
        {
            Solid bar = FilletBar(100);
            Solid box = Box(-1, -50, dz, 21, 80, 10);
            if (tilt != 0.0) box.Modify(ModOp.Rotate(new GeoPoint(0, -10, dz), GeoVector.XAxis, new SweepAngle(tilt)));
            // the common part is 20 x 10 of the profile, without the corner outside the rounding, and 30 long
            double common = 30 * (200 - (9 - 9 * Math.PI / 4));
            // within the precision of the operation (1.1e-4), the box may also be taken as lying on the plane: a slab of the
            // contact area (20 x 30) and dz thick
            AssertBooleans(box, bar, common, Math.Max(1e-3, 600 * Math.Abs(dz)));
        }

        private static double VolumeOf(Solid[] result, string what)
        {
            Assert.IsNotNull(result, what);
            foreach (Solid solid in result) Assert.AreEqual(0, solid.Shells[0].OpenEdges.Length, what + ": open edges");
            return result.Sum(s => s.Volume(1e-4));
        }

        /// <summary>
        /// A box reaching into the top face of a cube by less than the precision of the operation (about 1.7e-5 here) touches it.
        /// No face of either solid was left over, and the test, whether one solid contains the other, took a vertex of the box
        /// 1e-7 below the top face for a point not on the cube, because the extent of the planar face had no thickness: the
        /// intersection was the whole box (issue #400, found by tests/CADability.BooleanSweep).
        /// </summary>
        [DataTestMethod]
        [DataRow(1e-7)]
        [DataRow(1e-6)]
        [DataRow(1e-5)]
        public void a_box_reaching_into_a_face_by_less_than_the_precision_touches_it(double depth)
        {
            Solid cube = Box(0, 0, 0, 10, 10, 10);
            Solid box = Box(2, 2, 10 - depth, 6, 6, 5);
            Assert.AreEqual(0.0, VolumeOf(Solid.Intersect(cube, box), "cube * box"), 36 * depth + 1e-6, "cube * box");
            Assert.AreEqual(1000.0, Single(Solid.Subtract(cube, box), "cube - box").Volume(1e-4), 36 * depth + 1e-6, "cube - box");
            Assert.AreEqual(180.0, Single(Solid.Subtract(box, cube), "box - cube").Volume(1e-4), 36 * depth + 1e-6, "box - cube");
        }

        /// <summary>
        /// A box 1e-5 above the top face of a cube, within the precision of the operation (1.7e-5). The faces were taken as
        /// overlapping, but the vertical edges of the box, which end 1e-5 above the face, did not intersect it: the tolerance of
        /// their parameter was fixed at 1e-6, i.e. 5e-6 for an edge of 5. Without the intersection points the top face of the
        /// cube was not split, cube - box gave the reversed box, a negative volume, and the union was null (issue #400).
        /// </summary>
        [TestMethod]
        public void a_box_at_the_precision_above_a_face_touches_it()
        {
            Solid cube = Box(0, 0, 0, 10, 10, 10);
            Solid box = Box(2, 2, 10 + 1e-5, 6, 6, 5);
            Assert.AreEqual(1000.0, Single(Solid.Subtract(cube, box), "cube - box").Volume(1e-4), 1e-3, "cube - box");
            Assert.AreEqual(180.0, Single(Solid.Subtract(box, cube), "box - cube").Volume(1e-4), 1e-3, "box - cube");
            Solid union = Solid.Unite(cube, box);
            Assert.IsNotNull(union, "cube + box");
            Assert.AreEqual(1180.0, Single(new[] { union }, "cube + box").Volume(1e-4), 1e-3, "cube + box");
        }

        /// <summary>
        /// A blind hole whose top is 1e-5 below the top face of the box, within the precision of the operation (1.4e-5): the
        /// difference was the unchanged box and the union the cylinder alone, for the same reason as in
        /// <see cref="a_box_at_the_precision_above_a_face_touches_it"/>.
        /// </summary>
        [TestMethod]
        public void a_blind_hole_ending_at_the_precision_below_a_face()
        {
            Solid cube = Box(0, 0, 0, 10, 10, 10);
            Solid cylinder = Make3D.MakeCylinder(new GeoPoint(5, 5, 4), 2 * GeoVector.XAxis, (6 - 1e-5) * GeoVector.ZAxis);
            double hole = 4 * Math.PI * 6;
            Assert.AreEqual(1000.0 - hole, VolumeOf(Solid.Subtract(cube, cylinder), "cube - cylinder"), 1e-3, "cube - cylinder");
            Solid union = Solid.Unite(cube, cylinder);
            Assert.IsNotNull(union, "cube + cylinder");
            Assert.AreEqual(1000.0, Single(new[] { union }, "cube + cylinder").Volume(1e-4), 1e-3, "cube + cylinder");
        }

        /// <summary>
        /// Two cylinders of radius 5 and 5 + <paramref name="dr"/> on the same axis, the longer one around the shorter one (or inside
        /// it): within the precision of the operation (3e-5 here) the mantles coincide. CylindricalSurface.SameGeometry compared the
        /// radii with Precision.eps instead of the precision passed in, so for 1e-6 and 1e-5 the mantles were neither overlapping nor
        /// intersecting, and the intersection was empty (issue #400).
        /// </summary>
        [DataTestMethod]
        [DataRow(1e-6)]
        [DataRow(-1e-6)]
        [DataRow(1e-5)]
        [DataRow(-1e-5)]
        public void coaxial_cylinders_whose_radii_differ_by_less_than_the_precision(double dr)
        {
            Solid shorter = Make3D.MakeCylinder(GeoPoint.Origin, 5 * GeoVector.XAxis, 10 * GeoVector.ZAxis);
            Solid longer = Make3D.MakeCylinder(new GeoPoint(0, 0, -5), (5 + dr) * GeoVector.XAxis, 20 * GeoVector.ZAxis);
            double common = 10 * Math.PI * Math.Pow(Math.Min(5, 5 + dr), 2);
            double tolerance = 1e-3 + 2 * Math.PI * 5 * 20 * Math.Abs(dr); // the mantle dr thick
            Assert.AreEqual(common, Single(Solid.Intersect(shorter, longer), "shorter * longer").Volume(1e-4), tolerance, "shorter * longer");
            Assert.AreEqual(longer.Volume(1e-4) - common, VolumeOf(Solid.Subtract(longer, shorter), "longer - shorter"), tolerance, "longer - shorter");
        }

        [TestMethod]
        public void cylinders_are_the_same_geometry_within_the_precision()
        {
            CylindricalSurface a = new CylindricalSurface(GeoPoint.Origin, 5 * GeoVector.XAxis, 5 * GeoVector.YAxis, GeoVector.ZAxis);
            CylindricalSurface b = new CylindricalSurface(new GeoPoint(1e-6, 0, 0), (5 + 1e-6) * GeoVector.XAxis, (5 + 1e-6) * GeoVector.YAxis, GeoVector.ZAxis);
            BoundingRect bounds = new BoundingRect(0, 0, Math.PI, 10);
            Assert.IsTrue(a.SameGeometry(bounds, b, bounds, 1e-5, out _), "within 1e-5");
            Assert.IsFalse(a.SameGeometry(bounds, b, bounds, 1e-7, out _), "not within 1e-7");
        }
    }
}
