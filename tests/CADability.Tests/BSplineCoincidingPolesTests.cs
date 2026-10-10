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
            ICurve curve = CornerSpline(2, 2, false);
            Assert.IsTrue(Precision.SameDirection(curve.StartDirection, GeoVector.XAxis, false), $"3d start direction {curve.StartDirection}");
            Assert.IsTrue(Precision.SameDirection(curve.EndDirection, -GeoVector.YAxis, false), $"3d end direction {curve.EndDirection}");
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
        [DataRow(2, 2)]
        [DataRow(3, 3)]
        [DataRow(3, 2)]
        public void DxfRoundTripKeepsTheSpline(int degree, int polesPerCorner)
        {
            // the spline comes back as the same spline: same degree, poles and knots
            BSpline bsp = CornerSpline(degree, polesPerCorner, false);
            string fileName = ExportToFile(new GeoObjectList(bsp), $"coinciding_poles_{degree}_{polesPerCorner}.dxf");
            GeoObjectList imported = new CADability.DXF.Import(fileName).Project.GetActiveModel().AllObjects;
            Assert.AreEqual(1, imported.Count);
            Assert.IsInstanceOfType(imported[0], typeof(BSpline), "the spline must not be replaced");
            AssertSameSpline(bsp, (BSpline)imported[0], GeoVector.NullVector);
            // and written again it is the same
            string again = ExportToFile(imported, $"coinciding_poles_{degree}_{polesPerCorner}_again.dxf");
            AssertSameSpline(bsp, ReadSpline(again), GeoVector.NullVector);
            Assert.AreEqual(1 | 8, SplineFlags(again), "closed and planar");
        }

        [TestMethod]
        public void ExtrudeSplineWithCoincidingEndPoles()
        {
            // the derivative vanishes at both ends, the projection of the curve onto the extruded surface used to fail
            BSpline bsp = CornerSpline(3, 2, false);
            IGeoObject extruded = Make3D.Extrude(bsp, new GeoVector(0, 0, 10), null);
            Assert.IsNotNull(extruded);
            Face face = extruded as Face ?? ((Shell)extruded).Faces[0];
            // one of its edges is the spline
            bool found = face.AllEdges.Any(e => Enumerable.Range(0, 101).All(i => e.Curve3D.DistanceTo((bsp as ICurve).PointAt(i / 100.0)) < 1e-6));
            Assert.IsTrue(found, "the spline is no edge of the extruded face");
        }

        [TestMethod]
        public void FaceFromAClosedSplineKeepsTheSpline()
        {
            // Border.Reduce used to approximate the spline with the maximum gap, Precision.eps for Face.MakeFace, into
            // thousands of arcs
            GeoPoint[] points = Enumerable.Range(0, 12).Select(i => new GeoPoint(50 * Math.Cos(i * Math.PI / 6), 30 * Math.Sin(i * Math.PI / 6), 0)).ToArray();
            BSpline bsp = BSpline.Construct();
            bsp.ThroughPoints(points.Concat(new[] { points[0] }).ToArray(), 3, false);
            Face face = Face.MakeFace(new GeoObjectList(bsp));
            Assert.IsNotNull(face);
            Assert.IsTrue(face.AllEdges.Length <= 2, $"{face.AllEdges.Length} edges");
            GeoPoint2D[] exact = Sample(bsp, Plane.XYPlane, 8000);
            double polygonArea = 0.0;
            for (int i = 1; i < exact.Length; i++) polygonArea += (exact[i - 1].x * exact[i].y - exact[i].x * exact[i - 1].y) / 2.0;
            Assert.AreEqual(Math.Abs(polygonArea), face.Area.Area, 1e-3, "Area");
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
        public void FaceFromEllipsesKeepsThem()
        {
            // a full ellipse, and half an ellipse closed by a line
            Ellipse full = Ellipse.Construct();
            full.SetEllipseCenterAxis(new GeoPoint(10, 20, 0), new GeoVector(40, 0, 0), new GeoVector(0, 25, 0));
            Face face = Face.MakeFace(new GeoObjectList(full));
            Assert.IsNotNull(face);
            Assert.IsTrue(face.AllEdges.Length <= 2, $"{face.AllEdges.Length} edges");
            Assert.AreEqual(Math.PI * 40 * 25, face.Area.Area, 1e-6 * Math.PI * 40 * 25, "full ellipse");

            Ellipse half = Ellipse.Construct();
            half.SetEllipseArcCenterAxis(new GeoPoint(10, 20, 0), new GeoVector(40, 0, 0), new GeoVector(0, 25, 0), 0.0, Math.PI);
            Line diameter = Line.TwoPoints(half.EndPoint, half.StartPoint);
            face = Face.MakeFace(new GeoObjectList(half, diameter));
            Assert.IsNotNull(face);
            Assert.IsTrue(face.AllEdges.Length <= 3, $"{face.AllEdges.Length} edges");
            Assert.AreEqual(Math.PI * 40 * 25 / 2, face.Area.Area, 1e-6 * Math.PI * 40 * 25, "half ellipse");
        }

        private string ExportToFile(GeoObjectList objects, string name)
        {
            Project project = Project.CreateSimpleProject();
            foreach (IGeoObject go in objects) project.GetActiveModel().Add(go.Clone());
            string fileName = System.IO.Path.Combine(TestContext.TestRunDirectory ?? System.IO.Path.GetTempPath(), name);
            new CADability.DXF.Export().WriteToFile(project, fileName);
            return fileName;
        }

        /// <summary>The number of entities of the given type in the ENTITIES section of a DXF file.</summary>
        private static int CountEntities(string file, string type)
        {
            string[] lines = System.IO.File.ReadAllLines(file);
            int i = 0;
            while (i < lines.Length - 1 && !(lines[i].Trim() == "2" && lines[i + 1].Trim() == "ENTITIES")) i++;
            int count = 0;
            for (; i < lines.Length - 1 && lines[i].Trim() != "ENDSEC"; i++)
            {
                if (lines[i].Trim() == "0" && lines[i + 1].Trim() == type) count++;
            }
            return count;
        }

        /// <summary>Group code 70 of the first SPLINE of a DXF file.</summary>
        private static int SplineFlags(string file)
        {
            string[] lines = System.IO.File.ReadAllLines(file);
            // the line after the subclass marker is a group code, from there every second line is one
            for (int i = Array.FindIndex(lines, l => l.Trim() == "AcDbSpline") + 1; i < lines.Length - 1; i += 2)
            {
                if (lines[i].Trim() == "70") return int.Parse(lines[i + 1].Trim());
            }
            return -1;
        }

        private static void AssertSameSpline(BSpline expected, BSpline actual, GeoVector offset)
        {
            Assert.AreEqual(expected.Degree, actual.Degree, "degree");
            Assert.AreEqual(expected.Poles.Length, actual.Poles.Length, "number of poles");
            for (int i = 0; i < expected.Poles.Length; i++) Assert.IsTrue((expected.Poles[i] + offset | actual.Poles[i]) < 1e-9, $"pole {i}");
            CollectionAssert.AreEqual(expected.Knots, actual.Knots, "knots");
            CollectionAssert.AreEqual(expected.Multiplicities, actual.Multiplicities, "multiplicities");
        }

        /// <summary>The area of a face from its triangulation, which also shows a face that is broken inside.</summary>
        private static double TriangulatedArea(Face face)
        {
            face.GetTriangulation(0.01, out GeoPoint[] points, out _, out int[] triangles, out _);
            double area = 0.0;
            for (int i = 0; i < triangles.Length; i += 3) area += ((points[triangles[i + 1]] - points[triangles[i]]) ^ (points[triangles[i + 2]] - points[triangles[i]])).Length / 2.0;
            return area;
        }

        /// <summary>
        /// Reads the first SPLINE of a DXF file directly from its group codes, without the import, which splits the
        /// spline at its corners.
        /// </summary>
        private static BSpline ReadSpline(string file)
        {
            string[] lines = System.IO.File.ReadAllLines(file);
            int degree = 0;
            List<double> knots = new List<double>();
            List<GeoPoint> poles = new List<GeoPoint>();
            double x = 0.0, y = 0.0;
            int i = 0;
            while (!(lines[i].Trim() == "0" && lines[i + 1].Trim() == "SPLINE")) i += 2;
            for (i += 2; i < lines.Length - 1 && lines[i].Trim() != "0"; i += 2)
            {
                double value() => double.Parse(lines[i + 1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                switch (lines[i].Trim())
                {
                    case "71": degree = (int)value(); break;
                    case "40": knots.Add(value()); break;
                    case "10": x = value(); break;
                    case "20": y = value(); break;
                    case "30": poles.Add(new GeoPoint(x, y, value())); break;
                }
            }
            BSpline bsp = BSpline.Construct();
            Assert.IsTrue(bsp.SetData(degree, poles.ToArray(), null, knots.ToArray(), null, false));
            return bsp;
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/issue173.dxf", nameof(Issue173Spline))]
        public void Issue173Spline()
        {
            // the spline itself, as one BSpline, with its corners where the derivative vanishes on one side only
            string file = System.IO.Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName, "issue173.dxf");
            ICurve curve = ReadSpline(file);
            ICurve2D c2d = curve.GetProjectedCurve(Plane.XYPlane);
            ICurve2D reversed = c2d.CloneReverse(true);
            foreach (GeoPoint2D p in issue173KnotPoints)
            {
                Assert.IsTrue(curve.DistanceTo(new GeoPoint(p.x, p.y, 0.0)) < 1e-6, $"the spline misses {p}");
                Assert.IsTrue(c2d.MinDistance(p) < 1e-6, $"the projected spline misses {p}");
                Assert.IsTrue(reversed.MinDistance(p) < 1e-6, $"the reversed projected spline misses {p}");
            }
            // points on the curve, also right behind the corners, where the derivative vanishes on one side only
            double maxDistance = 0.0;
            List<double> positions = new List<double>();
            for (int i = 0; i <= 27 * 40; i++) positions.Add(i / (27.0 * 40));
            for (int k = 0; k <= 27; k++) foreach (double offset in new[] { -0.1, -0.01, -0.001, 0.001, 0.01, 0.1 }) positions.Add((k + offset) / 27.0);
            foreach (double position in positions.Where(t => t >= 0.0 && t <= 1.0)) maxDistance = Math.Max(maxDistance, curve.DistanceTo(curve.PointAt(position)));
            Assert.IsTrue(maxDistance < 1e-6, $"DistanceTo of points on the spline is up to {maxDistance}");
            // from the independent evaluation, a polygon of 10800 points
            Assert.AreEqual(4326.3974, new SimpleShape(new Border(c2d.Clone())).Area, 1e-3, "Area");
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

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/issue173.dxf", nameof(Issue173Import))]
        public void Issue173Import()
        {
            // the import keeps the spline, the export writes it back as it was
            string file = System.IO.Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName, "issue173.dxf");
            Project project = new CADability.DXF.Import(file).Project;
            GeoObjectList imported = project.GetActiveModel().AllObjects;
            Assert.AreEqual(1, imported.Count);
            BSpline spline = (BSpline)imported[0];
            BSpline original = ReadSpline(file);
            AssertSameSpline(original, spline, GeoVector.NullVector);
            string exported = ExportToFile(imported, "issue173_exported.dxf");
            Assert.AreEqual(1, CountEntities(exported, "SPLINE"));
            Assert.AreEqual(0, CountEntities(exported, "LINE"));
            AssertSameSpline(original, ReadSpline(exported), GeoVector.NullVector);
            // closed and planar as in the file; periodic (2) was only declared, the knots are clamped
            Assert.AreEqual(1 | 8, SplineFlags(exported), "flags");

            // what is done with such a contour: the operations split the spline at its 26 inner corners, the straight
            // Bézier segments become planar faces
            Shell side = (Shell)Make3D.Extrude(spline.Clone(), new GeoVector(0, 0, 10), project);
            Assert.AreEqual(27, side.Faces.Length, "extruded faces");
            Assert.AreEqual(579.2681 * 10, side.Faces.Sum(TriangulatedArea), 1.0, "extruded area");
            Solid rotated = Make3D.Rotate(spline.Clone(), new Axis(GeoPoint.Origin, GeoVector.YAxis), SweepAngle.Full, 0, project) as Solid;
            Assert.IsNotNull(rotated, "rotating the closed contour gives a solid");
            // reference: 2*pi times the first moment of the area about the y-axis, from a polygon of 10800 points
            Assert.AreEqual(1701045.81, rotated.Volume(0.01), 1e-4 * 1701045.81, "rotated volume");
            Face face = Face.MakeFace(new GeoObjectList(spline.Clone()));
            Assert.IsNotNull(face);
            Assert.AreEqual(27, face.AllEdges.Length, "the edges of the face are the smooth pieces");
            Assert.AreEqual(4326.3974, face.Area.Area, 1e-3, "face area");
            Solid solid = (Solid)Make3D.Extrude(face, new GeoVector(0, 0, 10), project);
            Assert.AreEqual(29, solid.Shells[0].Faces.Length, "faces of the solid");
            Assert.AreEqual(0, solid.Shells[0].OpenEdges.Length, "open edges");
            Assert.AreEqual(43263.974, solid.Volume(0.001), 1e-2, "volume");
            // the spline in the model is not changed by these operations
            AssertSameSpline(original, spline, GeoVector.NullVector);
        }

        [TestMethod]
        [DeploymentItem(@"Files/Dxf/issue173.dxf", nameof(Issue173SplineNotPlanar))]
        public void Issue173SplineNotPlanar()
        {
            // The spline of the issue bent out of its plane, with the same corners. A planar spline gets its positions
            // from the projection to its plane, this one from the tetrahedron hull, which returns positions far off
            // right behind some of the corners (41.7 at 3 of these points). BSpline.PositionOf does not use such a
            // result when a point of the curve contradicts it.
            string file = System.IO.Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName, "issue173.dxf");
            BSpline flat = ReadSpline(file);
            GeoPoint[] poles = flat.Poles.Select(p => new GeoPoint(p.x, p.y, 0.002 * (p.x - 60) * (p.x - 60) + 0.001 * p.y * p.y)).ToArray();
            BSpline bent = BSpline.Construct();
            Assert.IsTrue(bent.SetData(flat.Degree, poles, null, flat.Knots, flat.Multiplicities, false));
            ICurve curve = bent;
            Assert.AreEqual(PlanarState.NonPlanar, curve.GetPlanarState());

            List<double> positions = new List<double>();
            for (int i = 0; i <= 27 * 40; i++) positions.Add(i / (27.0 * 40));
            for (int k = 0; k <= 27; k++) foreach (double offset in new[] { -0.1, -0.01, -0.001, 0.001, 0.01, 0.1 }) positions.Add((k + offset) / 27.0);
            double maxDistance = 0.0;
            foreach (double position in positions.Where(t => t >= 0.0 && t <= 1.0)) maxDistance = Math.Max(maxDistance, curve.DistanceTo(curve.PointAt(position)));
            Assert.IsTrue(maxDistance < 1e-6, $"DistanceTo of points on the spline is up to {maxDistance}");

            Plane plane = new Plane(new GeoPoint(0, -100, 0), GeoVector.YAxis);
            double[] intersections = curve.GetPlaneIntersection(plane);
            Assert.AreEqual(4, intersections.Length, "intersections with the plane y = -100");
            foreach (double position in intersections) Assert.AreEqual(-100.0, curve.PointAt(position).y, 1e-6);
        }
    }
}
