using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// The hit tests of a <see cref="BSpline"/> use its tetrahedron hull: the curve is cut into pieces, and each piece is
    /// enclosed by its end points and the intersection of its end tangents. That only encloses a piece without an
    /// inflection. A piece shaped like an S got a hull beside the curve, and the curve could not be picked there: in
    /// the spline of issue #173, a control point dragged inward made a dent that could only be picked at its two ends.
    /// </summary>
    [TestClass]
    public class CurveHullTests
    {
        /// <summary>
        /// A quarter circle of radius 50 as three cubic Bezier pieces (knots of multiplicity 3, like the spline of issue
        /// #173), with the second inner control point of the middle piece moved by <paramref name="dent"/>.
        /// </summary>
        private static BSpline BezierArc(GeoVector dent)
        {
            const int pieces = 3;
            const double r = 50.0;
            double k = 4.0 / 3.0 * Math.Tan(Math.PI / 2.0 / pieces / 4.0); // handle length of a cubic circle segment
            List<GeoPoint> poles = new List<GeoPoint>();
            for (int i = 0; i < pieces; i++)
            {
                double a0 = i * Math.PI / 2.0 / pieces, a1 = (i + 1) * Math.PI / 2.0 / pieces;
                GeoPoint s = new GeoPoint(r * Math.Cos(a0), r * Math.Sin(a0), 0.0);
                GeoPoint e = new GeoPoint(r * Math.Cos(a1), r * Math.Sin(a1), 0.0);
                if (i == 0) poles.Add(s);
                poles.Add(s + k * r * new GeoVector(-Math.Sin(a0), Math.Cos(a0), 0.0));
                poles.Add(e - k * r * new GeoVector(-Math.Sin(a1), Math.Cos(a1), 0.0));
                poles.Add(e);
            }
            poles[5] = poles[5] + dent;
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.SetData(3, poles.ToArray(), null, new double[] { 0, 1, 2, 3 }, new int[] { 4, 3, 3, 4 }, false));
            return bsp;
        }

        private static IEnumerable<double> Positions(int n) => Enumerable.Range(0, n + 1).Select(i => i / (double)n);

        /// <summary>
        /// Every point of the curve lies in a small cube around it and in a small pick area around it, so the hit tests
        /// must say so, for the plain arc, for dents that make the middle piece an S, and for a dent that also leaves
        /// the plane, which gives tetrahedra instead of triangles. A pick area only reaches a limited depth, so it is
        /// only checked where it contains the point.
        /// </summary>
        [DataTestMethod]
        [DataRow(0.0, 0.0, 0.0)]
        [DataRow(-25.0, -25.0, 0.0)]
        [DataRow(-40.0, -10.0, 0.0)]
        [DataRow(-25.0, -25.0, 15.0)]
        public void every_point_of_a_spline_hits(double dx, double dy, double dz)
        {
            BSpline bsp = BezierArc(new GeoVector(dx, dy, dz));
            ICurve curve = bsp;
            Projection projection = new Projection(Projection.StandardProjection.FromTop);
            List<string> missed = new List<string>();
            int areasChecked = 0;
            foreach (double u in Positions(300))
            {
                GeoPoint p = curve.PointAt(u);
                BoundingCube cube = new BoundingCube(p, 0.05);
                if (!bsp.HitTest(ref cube, 0.0)) missed.Add($"cube at {u:F4}");
                GeoPoint2D p2 = projection.ProjectUnscaled(p);
                Projection.PickArea area = projection.GetPickSpace(new BoundingRect(p2.x - 0.05, p2.y - 0.05, p2.x + 0.05, p2.y + 0.05));
                if (!BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * p)) continue;
                areasChecked++;
                if (!bsp.HitTest(area, false)) missed.Add($"pick area at {u:F4}");
            }
            Assert.AreEqual(0, missed.Count, "points of the curve that do not hit: " + string.Join(", ", missed.Take(10)));
            Assert.IsTrue(areasChecked > 200, $"only {areasChecked} pick areas contain their point");
        }

        /// <summary>
        /// The same through the model, which is how a click in a view selects: every point of the dent selects the
        /// spline.
        /// </summary>
        [TestMethod]
        public void a_dent_in_a_spline_can_be_picked_in_the_model()
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            BSpline bsp = BezierArc(new GeoVector(-25.0, -25.0, 0.0));
            model.Add(bsp);
            Projection projection = new Projection(Projection.StandardProjection.FromTop);
            int missed = 0;
            foreach (double u in Positions(100))
            {
                GeoPoint2D p2 = projection.ProjectUnscaled((bsp as ICurve).PointAt(u));
                Projection.PickArea area = projection.GetPickSpace(new BoundingRect(p2.x - 0.1, p2.y - 0.1, p2.x + 0.1, p2.y + 0.1));
                if (!model.GetObjectsFromRect(area, null, PickMode.normal, null).Contains(bsp)) missed++;
            }
            Assert.AreEqual(0, missed, "positions on the spline where a click does not select it");
        }
    }
}
