using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System.Runtime.CompilerServices;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// Boolean operations with a small solid at an imprecise edge of an imported part. The part of issue153.stp is 1.2 m large,
    /// its STEP file has an uncertainty of 1e-5 m, and the BSpline edges between its cylinders and its NURBS faces are up to
    /// 7e-3 (mm) away from these surfaces. The precision of an operation with a cube of 1 is a few 1e-6, so where a face of the
    /// cube crossed such an edge, the intersection point on the edge was too far from the intersection curves of the cylinder
    /// and of the NURBS face with the face of the cube. These curves were dropped, the faces could not be closed, and the whole
    /// part disappeared: the results were empty. It did not depend on the size of the cube (1 or 100), only on whether its
    /// faces crossed such an edge.
    /// <para>
    /// Still not handled: a cube of 0.1 centred on the edge with the deviation of 7e-3. At that size the cube's edges meet the
    /// cylinder beyond the imprecise border of its face, which no tolerance of the intersection points can repair.
    /// </para>
    /// </summary>
    [TestClass]
    public class ImpreciseEdgeBooleanTests
    {
        private static string StepFile(string name, [CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "Step", name);

        private static Solid Part(double scale)
        {
            Solid part = Project.ReadFromFile(StepFile("issue153.stp"), "stp").GetActiveModel().AllObjects.OfType<Solid>().Single();
            if (scale != 1.0) part.Modify(ModOp.Scale(scale));
            return part;
        }

        /// <summary>
        /// A cube of size <paramref name="size"/> at the middle of a BSpline edge of length <paramref name="length"/> between a cylinder
        /// and a NURBS face: centred there, or moved along the edge, so that one of its faces crosses the edge in the middle.
        /// </summary>
        private static Solid CubeAtEdge(Solid part, double length, double size, bool crossing)
        {
            Edge edge = part.Shells[0].Edges.Where(e => e.Curve3D is BSpline && e.SecondaryFace != null && Math.Abs(e.Curve3D.Length - length) < 1e-2 * length)
                .OrderBy(e => e.Curve3D.PointAt(0.5).x).ThenBy(e => e.Curve3D.PointAt(0.5).y).ThenBy(e => e.Curve3D.PointAt(0.5).z).First();
            Assert.IsTrue(edge.PrimaryFace.Surface is CylindricalSurface || edge.SecondaryFace.Surface is CylindricalSurface);
            Assert.IsTrue(edge.PrimaryFace.Surface is NurbsSurface || edge.SecondaryFace.Surface is NurbsSurface);
            GeoPoint middle = edge.Curve3D.PointAt(0.5);
            GeoVector t = edge.Curve3D.DirectionAt(0.5);
            GeoVector axis; // the axis closest to the direction of the edge
            if (Math.Abs(t.x) >= Math.Abs(t.y) && Math.Abs(t.x) >= Math.Abs(t.z)) axis = Math.Sign(t.x) * GeoVector.XAxis;
            else if (Math.Abs(t.y) >= Math.Abs(t.z)) axis = Math.Sign(t.y) * GeoVector.YAxis;
            else axis = Math.Sign(t.z) * GeoVector.ZAxis;
            GeoPoint centre = crossing ? middle + (size / 2) * axis : middle;
            return Make3D.MakeBox(centre - new GeoVector(size / 2, size / 2, size / 2), size * GeoVector.XAxis, size * GeoVector.YAxis, size * GeoVector.ZAxis);
        }

        private static double Volume(Solid[] solids)
            => solids.Sum(s => ShellMetrics.IntegratedVolume(s.Shells[0], ShellMetrics.SizeOf(s.Shells[0]) * 1e-4));

        private static void AssertClosed(Solid[] solids, string what)
        {
            Assert.IsNotNull(solids, what);
            Assert.IsTrue(solids.Length > 0, what + ": no result");
            foreach (Solid solid in solids) Assert.AreEqual(0, solid.Shells[0].OpenEdgesExceptPoles.Length, what + ": open edges");
        }

        /// <summary>
        /// part - cube must be one solid; part * cube and cube - part must be the two parts of the cube. Their volumes are compared,
        /// because the volume of the part is too large to see the cube in it.
        /// </summary>
        [DataTestMethod]
        [DataRow(1.0, 5.823, 1.0, false)] // the edge with the largest deviation, 7e-3
        [DataRow(1.0, 5.823, 1.0, true)]
        [DataRow(1.0, 5.823, 10.0, true)]
        [DataRow(1.0, 3.687, 1.0, false)] // deviation 6e-5
        [DataRow(1.0, 3.687, 30.0, true)]
        [DataRow(10.0, 36.87, 1.0, false)] // a part of 12 m and a cube of 1
        public void a_small_cube_at_an_imprecise_edge_of_an_imported_part(double scale, double length, double size, bool crossing)
        {
            Solid part = Part(scale);
            Solid cube = CubeAtEdge(part, length, size, crossing);

            Solid[] difference = Solid.Subtract(part, cube);
            AssertClosed(difference, "part - cube");
            Assert.AreEqual(1, difference.Length, "part - cube: number of solids");

            Solid[] inside = Solid.Intersect(part, cube);
            Solid[] outside = Solid.Subtract(cube, part);
            AssertClosed(inside, "part * cube");
            AssertClosed(outside, "cube - part");
            double vInside = Volume(inside), vOutside = Volume(outside), vCube = size * size * size;
            Assert.IsTrue(vInside > 0.01 * vCube && vOutside > 0.01 * vCube, "the edge runs through the cube");
            Assert.AreEqual(vCube, vInside + vOutside, 1e-3 * vCube, "part * cube and cube - part make up the cube");
        }

        /// <summary>
        /// A loop whose curves do not meet exactly, as on the cylinder of the part above after it was split at the imprecise edge:
        /// the parameter space of the cylinder has a v-range of 1e-3 per mm, so the loop is narrow and far from the origin. Its
        /// area was summed relative to the origin, and the gap in the lower left corner added more than the area of the loop
        /// with the opposite sign: the outline was taken for a hole and the face was lost.
        /// </summary>
        [TestMethod]
        public void a_loop_with_a_small_gap_far_from_the_origin_keeps_its_orientation()
        {
            ICurve2D[] loop = new ICurve2D[]
            {
                new Line2D(new GeoPoint2D(-0.1863, -0.39696), new GeoPoint2D(-0.1863, -0.39724)),
                new Line2D(new GeoPoint2D(-0.1851, -0.39724), new GeoPoint2D(0.1864, -0.39769)), // starts 1.2e-3 to the right
                new Line2D(new GeoPoint2D(0.1864, -0.39769), new GeoPoint2D(0.1864, -0.39696)),
                new Line2D(new GeoPoint2D(0.1864, -0.39696), new GeoPoint2D(-0.1863, -0.39696)),
            };
            double area = Border.SignedArea(loop);
            Assert.IsTrue(area > 0, "counterclockwise");
            // closed by a line over the gap
            double closed = Border.SignedArea(loop.Append(new Line2D(new GeoPoint2D(-0.1863, -0.39724), new GeoPoint2D(-0.1851, -0.39724))).ToArray());
            Assert.AreEqual(closed, area, 1e-2 * closed);
            ICurve2D[] reversed = loop.Reverse().Select(c => { ICurve2D r = c.Clone(); r.Reverse(); return r; }).ToArray();
            Assert.AreEqual(-area, Border.SignedArea(reversed), 1e-2 * area, "clockwise");
        }
    }
}
