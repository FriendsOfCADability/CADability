using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CADability.Tests
{
    /// <summary>
    /// Splines with coinciding poles (issue 173). A doubled pole of a degree 2 spline, or a tripled pole of a degree 3
    /// spline, makes the curve pass through that pole with a vanishing derivative - the usual way to model a corner, which
    /// is how such splines come out of DXF files. <see cref="ICurve.GetProjectedCurve"/> used to replace them by a spline
    /// through some points of the curve, which rounded the corners off and overshot next to them by several units, and
    /// everything derived from the projection was off as well: the display, <see cref="ICurve.Approximate"/>,
    /// Reduce2D. The projection is now exact, and <see cref="BSpline2D"/> copes with the vanishing derivative.
    /// </summary>
    [TestClass]
    public class BSplineCoincidingPolesTests
    {
        public TestContext TestContext { get; set; }

        private static readonly GeoPoint[] pentagon = {
            new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0), new GeoPoint(100, 50, 0), new GeoPoint(50, 80, 0), new GeoPoint(0, 50, 0) };
        // the corners of this one are at the positions 0.25, 0.5 and 0.75, where algorithms that halve the curve
        // run into them
        private static readonly GeoPoint[] rectangle = {
            new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 0), new GeoPoint(100, 50, 0), new GeoPoint(0, 50, 0) };

        /// <summary>
        /// A closed, clamped (not periodic) spline around a pentagon or a rectangle, both have their vertical sides at
        /// x = 0 and x = 100. Every corner is repeated <paramref name="polesPerCorner"/> times, with a single pole in
        /// the middle of each side. With <paramref name="polesPerCorner"/> equal to <paramref name="degree"/> the curve
        /// has real corners there.
        /// </summary>
        private static BSpline CornerSpline(int degree, int polesPerCorner, bool useRectangle)
        {
            return CornerSpline(useRectangle ? rectangle : pentagon, degree, polesPerCorner);
        }

        private static BSpline CornerSpline(GeoPoint[] corners, int degree, int polesPerCorner)
        {
            List<GeoPoint> poles = new List<GeoPoint>();
            for (int i = 0; i < corners.Length; i++)
            {
                for (int k = 0; k < polesPerCorner; k++) poles.Add(corners[i]);
                poles.Add(new GeoPoint(corners[i], corners[(i + 1) % corners.Length]));
            }
            for (int k = 0; k < polesPerCorner; k++) poles.Add(corners[0]);
            int knotCount = poles.Count - degree + 1;
            double[] knots = new double[knotCount];
            int[] multiplicities = new int[knotCount];
            for (int i = 0; i < knotCount; i++)
            {
                knots[i] = i;
                multiplicities[i] = 1;
            }
            multiplicities[0] = multiplicities[knotCount - 1] = degree + 1;
            double[] weights = new double[poles.Count];
            for (int i = 0; i < weights.Length; i++) weights[i] = 1.0;
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.SetData(degree, poles.ToArray(), weights, knots, multiplicities, false));
            return bsp;
        }

        /// <summary>A dense polyline on the exact curve, the reference for the measurements.</summary>
        private static GeoPoint2D[] Sample(ICurve curve, Plane plane, int count = 20000)
        {
            GeoPoint2D[] res = new GeoPoint2D[count + 1];
            for (int i = 0; i <= count; i++) res[i] = plane.Project(curve.PointAt(i / (double)count));
            return res;
        }

        private static double DistanceToPolyline(GeoPoint2D[] polyline, GeoPoint2D p)
        {
            double res = double.MaxValue;
            for (int i = 1; i < polyline.Length; i++)
            {
                GeoVector2D seg = polyline[i] - polyline[i - 1];
                double len2 = seg * seg;
                double t = len2 > 0 ? Math.Max(0.0, Math.Min(1.0, ((p - polyline[i - 1]) * seg) / len2)) : 0.0;
                res = Math.Min(res, (polyline[i - 1] + t * seg) | p);
            }
            return res;
        }

        // degree 2 and 3 with real corners, and degree 3 with doubled poles, which has no corners but has been
        // replaced the same way
        [TestMethod]
        [DataRow(2, 2, false)]
        [DataRow(3, 3, false)]
        [DataRow(3, 2, false)]
        [DataRow(2, 2, true)]
        [DataRow(3, 3, true)]
        [DataRow(3, 2, true)]
        public void ProjectionIsExact(int degree, int polesPerCorner, bool useRectangle)
        {
            ICurve curve = CornerSpline(degree, polesPerCorner, useRectangle);
            ICurve2D c2d = curve.GetProjectedCurve(Plane.XYPlane);
            Assert.IsInstanceOfType(c2d, typeof(BSpline2D));
            double maxDeviation = 0.0;
            for (int i = 0; i <= 1000; i++)
            {
                double pos = i / 1000.0;
                maxDeviation = Math.Max(maxDeviation, c2d.PointAt(pos) | Plane.XYPlane.Project(curve.PointAt(pos)));
            }
            TestContext.WriteLine($"degree {degree}, {polesPerCorner} poles per corner{(useRectangle ? ", rectangle" : "")}: deviation {maxDeviation}");
            Assert.IsTrue(maxDeviation < 1e-9, $"the projected curve deviates by {maxDeviation}");
        }

        [TestMethod]
        [DataRow(2, 2, false)]
        [DataRow(3, 3, false)]
        [DataRow(3, 2, false)]
        [DataRow(2, 2, true)]
        [DataRow(3, 3, true)]
        [DataRow(3, 2, true)]
        public void ApproximationFollowsTheCurve(int degree, int polesPerCorner, bool useRectangle)
        {
            // this is what the display of the spline uses (with linesOnly true)
            ICurve curve = CornerSpline(degree, polesPerCorner, useRectangle);
            GeoPoint2D[] exact = Sample(curve, Plane.XYPlane, 4000);
            foreach (bool linesOnly in new[] { true, false })
            {
                const double precision = 0.01;
                ICurve approx = curve.Approximate(linesOnly, precision);
                double maxDeviation = 0.0;
                for (int i = 0; i <= 2000; i++) maxDeviation = Math.Max(maxDeviation, approx.DistanceTo(curve.PointAt(i / 2000.0)));
                // and the other way round, in case the approximation strays from the curve between its own points
                GeoPoint2D[] approxPoints = Sample(approx, Plane.XYPlane, 1000);
                for (int i = 0; i < approxPoints.Length; i++) maxDeviation = Math.Max(maxDeviation, DistanceToPolyline(exact, approxPoints[i]));
                TestContext.WriteLine($"degree {degree}, {polesPerCorner} poles per corner{(useRectangle ? ", rectangle" : "")}, linesOnly {linesOnly}: deviation {maxDeviation}");
                Assert.IsTrue(maxDeviation < 1.5 * precision, $"the approximation (linesOnly {linesOnly}) deviates by {maxDeviation}");
            }
        }

        [TestMethod]
        [DataRow(2, 2, false)]
        [DataRow(3, 3, false)]
        [DataRow(3, 2, false)]
        [DataRow(2, 2, true)]
        [DataRow(3, 3, true)]
        [DataRow(3, 2, true)]
        public void ProjectedCurveMeasuresCorrectly(int degree, int polesPerCorner, bool useRectangle)
        {
            ICurve curve = CornerSpline(degree, polesPerCorner, useRectangle);
            ICurve2D c2d = curve.GetProjectedCurve(Plane.XYPlane);
            GeoPoint2D[] exact = Sample(curve, Plane.XYPlane);

            // in both directions: the sides of a corner swap, and the derivative at the knot is the one of the other side
            foreach (ICurve2D c in new[] { c2d, c2d.CloneReverse(true) })
            {
                // points on the curve, many of them close to the corners, and the corners themselves
                double maxDistance = 0.0;
                for (int i = 0; i <= 500; i++)
                {
                    GeoPoint2D p = Plane.XYPlane.Project(curve.PointAt(i / 500.0));
                    maxDistance = Math.Max(maxDistance, c.MinDistance(p));
                }
                // with fewer coinciding poles than the degree the curve does not go through the corners
                if (polesPerCorner == degree) foreach (GeoPoint corner in useRectangle ? rectangle : pentagon) maxDistance = Math.Max(maxDistance, c.MinDistance(Plane.XYPlane.Project(corner)));
                Assert.IsTrue(maxDistance < 1e-6, $"MinDistance of points on the curve is up to {maxDistance}");

                // points off the curve, compared with the dense polyline
                Random rnd = new Random(173);
                for (int i = 0; i < 200; i++)
                {
                    GeoPoint2D p = new GeoPoint2D(-20 + 140 * rnd.NextDouble(), -20 + 120 * rnd.NextDouble());
                    double expected = DistanceToPolyline(exact, p);
                    double found = c.MinDistance(p);
                    Assert.AreEqual(expected, found, 1e-3, $"MinDistance from {p}");
                }
            }

            double polylineLength = 0.0;
            for (int i = 1; i < exact.Length; i++) polylineLength += exact[i] | exact[i - 1];
            Assert.AreEqual(polylineLength, c2d.Length, 1e-4 * polylineLength, "Length");

            double polylineArea = 0.0;
            for (int i = 1; i < exact.Length; i++) polylineArea += (exact[i - 1].x * exact[i].y - exact[i].x * exact[i - 1].y) / 2.0;
            Assert.AreEqual(polylineArea, c2d.GetArea(), 1e-4 * Math.Abs(polylineArea), "Area");

            // a line across the curve, through both straight vertical sides
            GeoPoint2DWithParameter[] ips = c2d.Intersect(new GeoPoint2D(-10, 20), new GeoPoint2D(110, 20));
            Assert.AreEqual(2, ips.Length, "number of intersection points");
            foreach (GeoPoint2DWithParameter ip in ips)
            {
                Assert.AreEqual(20.0, ip.p.y, 1e-6);
                Assert.IsTrue(DistanceToPolyline(exact, ip.p) < 1e-3, $"intersection point {ip.p} is not on the curve");
                Assert.IsTrue((c2d.PointAt(ip.par1) | ip.p) < 1e-6, $"intersection parameter does not match {ip.p}");
            }
        }

        [TestMethod]
        public void TangentAtCoincidingEndPoles()
        {
            // the curve starts and ends in a corner, its derivative vanishes there
            BSpline2D c2d = (BSpline2D)(CornerSpline(2, 2, false) as ICurve).GetProjectedCurve(Plane.XYPlane);
            Assert.IsTrue(Precision.SameDirection(c2d.StartDirection, GeoVector2D.XAxis, false), $"start direction {c2d.StartDirection}");
            Assert.IsTrue(Precision.SameDirection(c2d.EndDirection, -GeoVector2D.YAxis, false), $"end direction {c2d.EndDirection}");
        }

        [TestMethod]
        public void PolesCoincidingOnlyInTheProjection()
        {
            // a non planar spline: poles 1 and 2 only differ in z, so they coincide when projected to the xy-plane
            GeoPoint[] poles = {
                new GeoPoint(0, 0, 0), new GeoPoint(40, 0, 0), new GeoPoint(40, 0, 30), new GeoPoint(40, 40, 30), new GeoPoint(80, 40, 0), new GeoPoint(80, 80, 10) };
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.SetData(2, poles, null, new double[] { 0, 1, 2, 3, 4 }, new int[] { 3, 1, 1, 1, 3 }, false));
            ICurve curve = bsp;
            ICurve2D c2d = curve.GetProjectedCurve(Plane.XYPlane);
            double maxDeviation = 0.0;
            double maxDistance = 0.0;
            for (int i = 0; i <= 1000; i++)
            {
                GeoPoint2D p = Plane.XYPlane.Project(curve.PointAt(i / 1000.0));
                maxDeviation = Math.Max(maxDeviation, c2d.PointAt(i / 1000.0) | p);
                if (i % 5 == 0) maxDistance = Math.Max(maxDistance, c2d.MinDistance(p));
            }
            Assert.IsTrue(maxDeviation < 1e-9, $"the projected curve deviates by {maxDeviation}");
            Assert.IsTrue(maxDistance < 1e-6, $"MinDistance of points on the curve is up to {maxDistance}");
        }

        [TestMethod]
        [DataRow(2, 2, false)]
        [DataRow(3, 3, false)]
        public void DxfImportKeepsTheSpline(int degree, int polesPerCorner, bool useRectangle)
        {
            BSpline bsp = CornerSpline(degree, polesPerCorner, useRectangle);
            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(bsp);
            string fileName = System.IO.Path.Combine(TestContext.TestRunDirectory ?? System.IO.Path.GetTempPath(), $"coinciding_poles_{degree}.dxf");
            new CADability.DXF.Export().WriteToFile(project, fileName);
            GeoObjectList imported = new CADability.DXF.Import(fileName).Project.GetActiveModel().AllObjects;
            Assert.AreEqual(1, imported.Count);
            Assert.IsInstanceOfType(imported[0], typeof(BSpline), "the spline must not be replaced by a polyline");
            ICurve curve = (ICurve)imported[0];
            double maxDeviation = 0.0;
            for (int i = 0; i <= 1000; i++) maxDeviation = Math.Max(maxDeviation, curve.PointAt(i / 1000.0) | (bsp as ICurve).PointAt(i / 1000.0));
            Assert.IsTrue(maxDeviation < 1e-6, $"the imported spline deviates by {maxDeviation}");
        }

        [TestMethod]
        [DataRow(2, 2, false)]
        [DataRow(3, 3, false)]
        [DataRow(3, 2, false)]
        [DataRow(2, 2, true)]
        public void NoSelfIntersectionsAtTheCorners(int degree, int polesPerCorner, bool useRectangle)
        {
            // the two base points the triangulation puts next to a corner must not look like a self intersection
            ICurve2D c2d = (CornerSpline(degree, polesPerCorner, useRectangle) as ICurve).GetProjectedCurve(Plane.XYPlane);
            double[] selfIntersections = c2d.GetSelfIntersections();
            for (int i = 0; i < selfIntersections.Length - 1; i += 2)
            {
                // a closed curve may report its start and end point
                bool startAndEnd = Math.Min(selfIntersections[i], selfIntersections[i + 1]) < 1e-6 && Math.Max(selfIntersections[i], selfIntersections[i + 1]) > 1 - 1e-6;
                Assert.IsTrue(startAndEnd, $"self intersection reported at {selfIntersections[i]}, {selfIntersections[i + 1]}");
            }
        }

        [TestMethod]
        [DataRow(2, 2, false)]
        [DataRow(3, 3, false)]
        [DataRow(2, 2, true)]
        public void ShapesFromTheCurve(int degree, int polesPerCorner, bool useRectangle)
        {
            // the same operations on a dense polygon of the curve are the reference
            ICurve curve = CornerSpline(degree, polesPerCorner, useRectangle);
            ICurve2D c2d = curve.GetProjectedCurve(Plane.XYPlane);
            GeoPoint2D[] exact = Sample(curve, Plane.XYPlane, 8000);
            Border polygon = new Border(exact.Take(exact.Length - 1).ToArray());
            Border border = new Border(c2d);
            SimpleShape shape = new SimpleShape(border);
            SimpleShape reference = new SimpleShape(polygon);
            Assert.AreEqual(reference.Area, shape.Area, 1e-3, "Area");

            Random rnd = new Random(173);
            for (int i = 0; i < 300; i++)
            {
                GeoPoint2D p = new GeoPoint2D(-10 + 120 * rnd.NextDouble(), -10 + 100 * rnd.NextDouble());
                if (DistanceToPolyline(exact, p) < 0.05) continue; // too close to decide against a polygon
                Assert.AreEqual(polygon.GetPosition(p), border.GetPosition(p), $"position of {p}");
            }

            // a circle around a corner, one on the inside of the curve and one crossing a side
            foreach (GeoPoint2D center in new[] { new GeoPoint2D(100, 0), new GeoPoint2D(0, 50), new GeoPoint2D(60, 0) })
            {
                SimpleShape circle = new SimpleShape(Border.MakeCircle(center, 10));
                Assert.AreEqual(SimpleShape.Unite(reference, circle).Area, SimpleShape.Unite(shape, circle).Area, 1e-2, $"Unite at {center}");
                Assert.AreEqual(SimpleShape.Subtract(reference, circle).Area, SimpleShape.Subtract(shape, circle).Area, 1e-2, $"Subtract at {center}");
                Assert.AreEqual(SimpleShape.Intersect(reference, circle).Area, SimpleShape.Intersect(shape, circle).Area, 1e-2, $"Intersect at {center}");
            }
        }

        [TestMethod]
        [DataRow(2, 2)]
        [DataRow(3, 3)]
        [DataRow(3, 2)]
        public void NonPlanarSplineWithCorners(int degree, int polesPerCorner)
        {
            // not planar, so this neither goes through GetProjectedCurve nor ArcLineFitting2D
            GeoPoint[] corners = {
                new GeoPoint(0, 0, 0), new GeoPoint(100, 0, 20), new GeoPoint(100, 50, 0), new GeoPoint(50, 80, 30), new GeoPoint(0, 50, -10) };
            ICurve curve = CornerSpline(corners, degree, polesPerCorner);
            Assert.AreEqual(PlanarState.NonPlanar, curve.GetPlanarState());
            foreach (bool linesOnly in new[] { true, false })
            {
                const double precision = 0.01;
                ICurve approx = curve.Approximate(linesOnly, precision);
                double maxDeviation = 0.0;
                for (int i = 0; i <= 2000; i++) maxDeviation = Math.Max(maxDeviation, approx.DistanceTo(curve.PointAt(i / 2000.0)));
                TestContext.WriteLine($"degree {degree}, {polesPerCorner} poles per corner, linesOnly {linesOnly}: deviation {maxDeviation}");
                Assert.IsTrue(maxDeviation < 1.5 * precision, $"the approximation (linesOnly {linesOnly}) deviates by {maxDeviation}");
            }
        }

        [TestMethod]
        [DataRow(2, 2)]
        [DataRow(3, 3)]
        public void LineThroughACorner(int degree, int polesPerCorner)
        {
            // the line runs into the curve exactly at the corner (100, 0) and ends inside of it
            ICurve2D c2d = (CornerSpline(degree, polesPerCorner, false) as ICurve).GetProjectedCurve(Plane.XYPlane);
            GeoPoint2DWithParameter[] ips = c2d.Intersect(new GeoPoint2D(110, -10), new GeoPoint2D(40, 60));
            Assert.AreEqual(1, ips.Length, "the corner must be reported once");
            Assert.IsTrue((ips[0].p | new GeoPoint2D(100, 0)) < 1e-6, $"intersection point {ips[0].p}");
        }

        /// <summary>
        /// The points of the spline in issue173.dxf (the file attached to the issue) at its knots, from an
        /// evaluation of the DXF data independent of CADability. The spline consists of 27 cubic Bézier segments,
        /// each knot has the multiplicity 3, so the curve has a corner at every knot. The straight segments start and
        /// end with coinciding poles, so at several knots the derivative vanishes on one side only.
        /// </summary>
        private static readonly GeoPoint2D[] issue173KnotPoints = {
            new GeoPoint2D(101.07046962338, -194.8771162786902), new GeoPoint2D(19.57863072231197, -194.8771162786902),
            new GeoPoint2D(19.57863072231197, -178.4729496161246), new GeoPoint2D(69.3204696313175, -129.0842329943569),
            new GeoPoint2D(79.02185851778103, -116.9133996640663), new GeoPoint2D(83.07880296121124, -102.6258996676381),
            new GeoPoint2D(81.31491407276332, -93.71826078097617), new GeoPoint2D(76.55241407395394, -86.92728856045169),
            new GeoPoint2D(69.49685852016226, -82.60576078375429), new GeoPoint2D(60.85380296676749, -81.10645522857357),
            new GeoPoint2D(45.15501960480676, -87.19187189371888), new GeoPoint2D(37.39390849563593, -102.8022885564829),
            new GeoPoint2D(20.63696405538072, -99.9800663349663), new GeoPoint2D(25.13488072092291, -86.83909411602932),
            new GeoPoint2D(33.8661307187401, -76.25576078534179), new GeoPoint2D(46.12533074822738, -69.28839967597251),
            new GeoPoint2D(61.03019185561228, -66.81895523214544), new GeoPoint2D(75.93505296299718, -69.11201078712773),
            new GeoPoint2D(88.54685851539978, -75.81478856322981), new GeoPoint2D(97.27810851321696, -86.92728856045169),
            new GeoPoint2D(100.5413029568456, -102.2731218899485), new GeoPoint2D(98.95380295724249, -113.3856218871704),
            new GeoPoint2D(94.6322751805451, -123.087010773634), new GeoPoint2D(88.1940807377102, -131.8182607714512),
            new GeoPoint2D(80.43296962853935, -140.020344102734), new GeoPoint2D(39.86335293946302, -179.5312829491934),
            new GeoPoint2D(101.07046962338, -179.5312829491934) };

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/issue173.dxf", nameof(Issue173File))]
        public void Issue173File()
        {
            string file = System.IO.Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName, "issue173.dxf");
            Assert.IsTrue(System.IO.File.Exists(file));
            GeoObjectList imported = new CADability.DXF.Import(file).Project.GetActiveModel().AllObjects;
            Assert.AreEqual(1, imported.Count);
            Assert.IsInstanceOfType(imported[0], typeof(BSpline), "the spline must not be replaced by a polyline");
            ICurve curve = (ICurve)imported[0];
            ICurve2D c2d = curve.GetProjectedCurve(Plane.XYPlane);
            ICurve2D reversed = c2d.CloneReverse(true);
            foreach (GeoPoint2D p in issue173KnotPoints)
            {
                Assert.IsTrue(curve.DistanceTo(new GeoPoint(p.x, p.y, 0.0)) < 1e-6, $"the spline misses {p}");
                Assert.IsTrue(c2d.MinDistance(p) < 1e-6, $"the projected spline misses {p}");
                Assert.IsTrue(reversed.MinDistance(p) < 1e-6, $"the reversed projected spline misses {p}");
            }
            // from the independent evaluation, a polygon of 10800 points
            Assert.AreEqual(4326.3974, new SimpleShape(new Border(c2d)).Area, 1e-3, "Area");
            Assert.AreEqual(579.2681, curve.Length, 1e-3, "Length");

            // the display of the spline
            GeoPoint2D[] exact = Sample(curve, Plane.XYPlane, 27 * 200);
            const double precision = 0.01;
            ICurve approx = curve.Approximate(true, precision);
            double maxDeviation = 0.0;
            for (int i = 0; i < exact.Length; i++) maxDeviation = Math.Max(maxDeviation, approx.DistanceTo(new GeoPoint(exact[i].x, exact[i].y, 0.0)));
            foreach (GeoPoint2D p in Sample(approx, Plane.XYPlane, 4000)) maxDeviation = Math.Max(maxDeviation, c2d.MinDistance(p));
            Assert.IsTrue(maxDeviation < 1.5 * precision, $"the displayed approximation deviates by {maxDeviation}");

            double[] selfIntersections = c2d.GetSelfIntersections();
            for (int i = 0; i < selfIntersections.Length - 1; i += 2)
            {
                bool startAndEnd = Math.Min(selfIntersections[i], selfIntersections[i + 1]) < 1e-6 && Math.Max(selfIntersections[i], selfIntersections[i + 1]) > 1 - 1e-6;
                Assert.IsTrue(startAndEnd, $"self intersection reported at {selfIntersections[i]}, {selfIntersections[i + 1]}");
            }
        }
    }
}
