using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #424: <see cref="Make3D.MakeRuledSolid"/> and <see cref="Make3D.MakeRuledShell"/> threw
    /// <c>NotImplementedException</c> when the paths could not be connected, although their documentation promised null
    /// and the construct actions test for null.
    /// </summary>
    [TestClass]
    public class MakeRuledSolidTests
    {
        private static GeoObject.Path Polygon(double z, params GeoPoint2D[] corners)
        {
            ICurve[] curves = new ICurve[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                GeoPoint2D a = corners[i];
                GeoPoint2D b = corners[(i + 1) % corners.Length];
                curves[i] = Line.MakeLine(new GeoPoint(a.x, a.y, z), new GeoPoint(b.x, b.y, z));
            }
            GeoObject.Path path = GeoObject.Path.Construct();
            path.Set(curves);
            return path;
        }

        private static GeoObject.Path OpenPolyline(double z, params GeoPoint2D[] points)
        {
            ICurve[] curves = new ICurve[points.Length - 1];
            for (int i = 0; i < curves.Length; i++)
            {
                curves[i] = Line.MakeLine(new GeoPoint(points[i].x, points[i].y, z), new GeoPoint(points[i + 1].x, points[i + 1].y, z));
            }
            GeoObject.Path path = GeoObject.Path.Construct();
            path.Set(curves);
            return path;
        }

        private static readonly GeoPoint2D[] square = { new GeoPoint2D(0, 0), new GeoPoint2D(10, 0), new GeoPoint2D(10, 10), new GeoPoint2D(0, 10) };
        private static readonly GeoPoint2D[] triangle = { new GeoPoint2D(0, 0), new GeoPoint2D(10, 0), new GeoPoint2D(5, 10) };

        [TestMethod]
        public void TwoSquaresGiveABox()
        {
            Solid solid = Make3D.MakeRuledSolid(Polygon(0.0, square), Polygon(5.0, square), null);
            Assert.IsNotNull(solid);
            Assert.AreEqual(500.0, solid.Volume(1e-6), 1e-6);
        }

        /// <summary>
        /// Issue #411: paths with different numbers of segments (and more than one each) gave null. Both are split where the
        /// other one has a vertex at the same relative length now, so a square and a triangle give a solid with six ruled
        /// faces.
        /// </summary>
        [TestMethod]
        public void DifferentNumbersOfSegmentsArePairedByTheirPosition()
        {
            Solid solid = Make3D.MakeRuledSolid(Polygon(0.0, square), Polygon(5.0, triangle), null);
            Assert.IsNotNull(solid);
            // square: vertices at 0.25, 0.5, 0.75 of its length; triangle: at 0.309 and 0.654
            Assert.AreEqual(6 + 2, solid.Shells[0].Faces.Length, "six ruled faces and the two planar ones");
            Assert.AreEqual(0, solid.Shells[0].OpenEdges.Length, "a closed shell");
            double volume = solid.Volume(1e-3);
            Assert.IsTrue(volume > 5.0 * 50.0 && volume < 5.0 * 100.0, $"between the triangle and the square prism: {volume}");
            IGeoObject shell = Make3D.MakeRuledShell(Polygon(0.0, square), Polygon(5.0, triangle), null);
            Assert.IsInstanceOfType(shell, typeof(Shell));
            Assert.AreEqual(6, ((Shell)shell).Faces.Length);
        }

        private static readonly GeoPoint[] pentagon = {
            new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0), new GeoPoint(120, 60, 0), new GeoPoint(50, 100, 0), new GeoPoint(-20, 60, 0) };

        /// <summary>
        /// A closed quadratic spline around <see cref="pentagon"/> with doubled poles at the corners: straight sides with
        /// real corners, as one curve.
        /// </summary>
        private static BSpline PentagonSpline()
        {
            List<GeoPoint> poles = new List<GeoPoint>();
            for (int i = 0; i < pentagon.Length; i++)
            {
                poles.Add(pentagon[i]);
                poles.Add(pentagon[i]);
                poles.Add(new GeoPoint(pentagon[i], pentagon[(i + 1) % pentagon.Length]));
            }
            poles.Add(pentagon[0]);
            poles.Add(pentagon[0]);
            int knotCount = poles.Count - 1;
            double[] knots = Enumerable.Range(0, knotCount).Select(i => (double)i).ToArray();
            int[] multiplicities = Enumerable.Repeat(1, knotCount).ToArray();
            multiplicities[0] = multiplicities[knotCount - 1] = 3;
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.SetData(2, poles.ToArray(), null, knots, multiplicities, false));
            return bsp;
        }

        /// <summary>
        /// The largest change of direction between neighbouring samples of <paramref name="curve"/>, in degrees. A sample
        /// right at a corner of a spline with coinciding poles has no direction and is skipped.
        /// </summary>
        private static double LargestKink(ICurve curve)
        {
            double largest = 0.0;
            GeoVector? last = null;
            for (int i = 0; i <= 200; i++)
            {
                GeoVector dir = curve.DirectionAt(i / 200.0);
                if (dir.IsNullVector()) continue;
                if (last.HasValue) largest = Math.Max(largest, new Angle(last.Value, dir).Degree);
                last = dir;
            }
            return largest;
        }

        /// <summary>
        /// Issue #411: a spline with corners was one segment, paired with one segment of the other path, so the ruled face
        /// had the corner inside. The spline is split at its corners first now; every edge of the shell is smooth.
        /// </summary>
        [TestMethod]
        public void ASplineWithCornersGivesSmoothFaces()
        {
            GeoObject.Path lower = GeoObject.Path.Construct();
            lower.Set(new ICurve[] { PentagonSpline() });
            GeoObject.Path upper = Polygon(50.0, new GeoPoint2D(0, 0), new GeoPoint2D(100, 0), new GeoPoint2D(50, 90));
            Assert.IsTrue(LargestKink(lower.Curves[0]) > 60.0, "test setup: the spline has corners");
            Solid solid = Make3D.MakeRuledSolid(lower, upper, null);
            Assert.IsNotNull(solid);
            Assert.AreEqual(0, solid.Shells[0].OpenEdges.Length, "a closed shell");
            foreach (Edge edge in solid.Shells[0].Edges)
                Assert.IsTrue(LargestKink(edge.Curve3D) < 5.0, $"an edge with a kink of {LargestKink(edge.Curve3D):F1} degrees");
            // the pentagon has 5 vertices at relative lengths that differ from the 2 inner ones of the triangle
            Assert.AreEqual(7 + 2, solid.Shells[0].Faces.Length, "seven ruled faces and the two planar ones");
        }

        [TestMethod]
        public void OpenPathGivesNoSolidButAShell()
        {
            GeoObject.Path lower = OpenPolyline(0.0, new GeoPoint2D(0, 0), new GeoPoint2D(10, 0), new GeoPoint2D(10, 10));
            GeoObject.Path upper = OpenPolyline(5.0, new GeoPoint2D(0, 0), new GeoPoint2D(10, 0), new GeoPoint2D(10, 10));
            Assert.IsNull(Make3D.MakeRuledSolid(lower, upper, null), "an open path does not bound a solid");
            IGeoObject shell = Make3D.MakeRuledShell(lower, upper, null);
            Assert.IsInstanceOfType(shell, typeof(Shell));
            Assert.AreEqual(2, ((Shell)shell).Faces.Length, "one ruled face per pair of segments");
        }
    }
}
