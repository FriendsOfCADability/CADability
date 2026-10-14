using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Linq;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="Make3D.MakeRevolution"/>, which the "rotate objects" action (Constr3DPathRotate) uses, threw a
    /// NotImplementedException for every path and curve, and in a release build also for a face. The areas are compared
    /// with Pappus's theorem: a curve rotated by the angle a sweeps the area a * integral of r ds, r being the distance
    /// from the axis.
    /// </summary>
    [TestClass]
    public class MakeRevolutionTests
    {
        private static readonly GeoPoint axisLocation = GeoPoint.Origin;
        private static readonly GeoVector axisDirection = GeoVector.ZAxis;

        private static CADability.GeoObject.Path MakePath(params ICurve[] curves)
        {
            CADability.GeoObject.Path path = CADability.GeoObject.Path.Construct();
            Assert.IsTrue(path.Set(curves));
            return path;
        }

        /// <summary>The area the curve sweeps when rotated by <paramref name="sweep"/> around the z-axis.</summary>
        private static double PappusArea(ICurve curve, double sweep)
        {
            const int n = 20000;
            double moment = 0.0;
            GeoPoint last = curve.PointAt(0.0);
            for (int i = 1; i <= n; i++)
            {
                GeoPoint p = curve.PointAt(i / (double)n);
                GeoPoint m = new GeoPoint(last, p);
                moment += Math.Sqrt(m.x * m.x + m.y * m.y) * (p | last);
                last = p;
            }
            return sweep * moment;
        }

        private static double TriangulatedArea(IGeoObject shellOrFace)
        {
            Face[] faces = shellOrFace is Shell shell ? shell.Faces : new[] { (Face)shellOrFace };
            double area = 0.0;
            foreach (Face face in faces)
            {
                face.GetTriangulation(0.001, out GeoPoint[] points, out _, out int[] triangles, out _);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    area += ((points[triangles[i + 1]] - points[triangles[i]]) ^ (points[triangles[i + 2]] - points[triangles[i]])).Length / 2.0;
                }
            }
            return area;
        }

        private static IGeoObject Revolve(IGeoObject toRotate, double sweep)
        {
            return Make3D.MakeRevolution(toRotate, axisLocation, axisDirection, sweep, null);
        }

        /// <summary>
        /// Every point of the triangulated result lies on the curve rotated by an angle between 0 and
        /// <paramref name="sweep"/>, and the angles reach both ends. The curves are in the xz-plane with x &gt;= 0, so a point
        /// rotated back to the xz-plane must be on the curve.
        /// </summary>
        private static void AssertOnRotatedCurve(ICurve curve, IGeoObject res, double sweep)
        {
            Face[] faces = res is Shell shell ? shell.Faces : new[] { (Face)res };
            double minAngle = double.MaxValue, maxAngle = double.MinValue, maxDistance = 0.0;
            foreach (Face face in faces)
            {
                face.GetTriangulation(0.01, out GeoPoint[] points, out _, out _, out _);
                foreach (GeoPoint p in points)
                {
                    double r = Math.Sqrt(p.x * p.x + p.y * p.y);
                    maxDistance = Math.Max(maxDistance, curve.DistanceTo(new GeoPoint(r, 0, p.z)));
                    if (r < 1e-6) continue; // on the axis, no angle
                    double angle = Math.Atan2(p.y, p.x);
                    if (angle < -1e-6) angle += 2 * Math.PI;
                    minAngle = Math.Min(minAngle, angle);
                    maxAngle = Math.Max(maxAngle, angle);
                }
            }
            Assert.IsTrue(maxDistance < 1e-6, $"a point of the result is {maxDistance} away from the rotated curve");
            Assert.AreEqual(0.0, minAngle, 1e-6, "start of the rotation");
            if (sweep < 2 * Math.PI - 1e-6) Assert.AreEqual(sweep, maxAngle, 1e-6, "end of the rotation");
            else Assert.IsTrue(maxAngle > 2 * Math.PI - 0.1, "a full rotation goes all the way round");
        }

        private static void AssertSweptArea(ICurve curve, double sweep)
        {
            IGeoObject res = Revolve(curve.Clone(), sweep);
            Assert.IsNotNull(res, "no result");
            Assert.IsTrue(res is Shell || res is Face, $"result is {res.GetType().Name}");
            AssertOnRotatedCurve(curve, res, sweep);
            double expected = PappusArea(curve, sweep);
            Assert.AreEqual(expected, TriangulatedArea(res), 2e-3 * expected, "area");
        }

        [TestMethod]
        [DataRow(2 * Math.PI)]
        [DataRow(Math.PI / 2)]
        public void LineParallelToTheAxis(double sweep)
        {
            AssertSweptArea(Line.TwoPoints(new GeoPoint(10, 0, 0), new GeoPoint(10, 0, 20)), sweep);
        }

        [TestMethod]
        [DataRow(2 * Math.PI)]
        [DataRow(Math.PI / 2)]
        public void OpenPathStartingOnTheAxis(double sweep)
        {
            // a disc around the pole and a cylinder
            CADability.GeoObject.Path path = MakePath(Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0)), Line.TwoPoints(new GeoPoint(10, 0, 0), new GeoPoint(10, 0, 20)));
            AssertSweptArea(path, sweep);
        }

        [TestMethod]
        [DataRow(2 * Math.PI)]
        [DataRow(Math.PI / 2)]
        public void ArcEndingOnTheAxis(double sweep)
        {
            // a quarter circle around the origin from the xy-plane to the z-axis: part of a sphere
            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterStartEndPoint(Plane.XZPlane, GeoPoint2D.Origin, new GeoPoint2D(10, 0), new GeoPoint2D(0, 10), Plane.XZPlane, true);
            Assert.IsTrue(((arc as ICurve).EndPoint | new GeoPoint(0, 0, 10)) < 1e-9, "the arc is not the expected one");
            AssertSweptArea(arc, sweep);
        }

        [TestMethod]
        [DataRow(2 * Math.PI)]
        [DataRow(Math.PI / 2)]
        public void Spline(double sweep)
        {
            BSpline bsp = BSpline.Construct();
            bsp.ThroughPoints(new[] { new GeoPoint(5, 0, 0), new GeoPoint(12, 0, 5), new GeoPoint(8, 0, 12), new GeoPoint(15, 0, 20) }, 3, false);
            AssertSweptArea(bsp, sweep);
        }

        [TestMethod]
        public void ClosedPathGivesAClosedShell()
        {
            // a rectangle away from the axis: the shell of a ring, closed, but a shell and not a solid
            GeoPoint[] c = { new GeoPoint(10, 0, 0), new GeoPoint(20, 0, 0), new GeoPoint(20, 0, 10), new GeoPoint(10, 0, 10) };
            CADability.GeoObject.Path path = MakePath(Line.TwoPoints(c[0], c[1]), Line.TwoPoints(c[1], c[2]), Line.TwoPoints(c[2], c[3]), Line.TwoPoints(c[3], c[0]));
            IGeoObject res = Revolve(path.Clone(), 2 * Math.PI);
            Assert.IsInstanceOfType(res, typeof(Shell));
            Assert.AreEqual(0, ((Shell)res).OpenEdges.Length, "open edges");
            double expected = PappusArea(path, 2 * Math.PI);
            Assert.AreEqual(expected, TriangulatedArea(res), 2e-3 * expected, "area");
        }

        [TestMethod]
        public void FaceGivesASolid()
        {
            // the same rectangle as a face: a ring with volume 2*pi * 15 * 100
            Face face = Face.MakeFace(new GeoObjectList(Line.TwoPoints(new GeoPoint(10, 0, 0), new GeoPoint(20, 0, 0)), Line.TwoPoints(new GeoPoint(20, 0, 0), new GeoPoint(20, 0, 10)),
                Line.TwoPoints(new GeoPoint(20, 0, 10), new GeoPoint(10, 0, 10)), Line.TwoPoints(new GeoPoint(10, 0, 10), new GeoPoint(10, 0, 0))));
            Assert.IsNotNull(face);
            Solid solid = Revolve(face, 2 * Math.PI) as Solid;
            Assert.IsNotNull(solid, "rotating a face gives a solid");
            Assert.AreEqual(2 * Math.PI * 15 * 100, solid.Volume(0.001), 1e-3 * 2 * Math.PI * 15 * 100, "volume");
        }

        [TestMethod]
        public void OtherObjectsGiveNull()
        {
            Assert.IsNull(Revolve(CADability.GeoObject.Point.Construct(), 2 * Math.PI));
        }

        /// <summary>
        /// A face with an edge that is a spline with corners inside, as STEP files have them. The side faces of the
        /// result are copies of the face, so the path made from its edges must not be split at the corners: its curves
        /// and the edges of the copies got out of step, an IndexOutOfRangeException for every such face.
        /// </summary>
        [TestMethod]
        public void FaceWithACornerSplineEdgeGivesASolid()
        {
            // two quadratic Bézier segments in the xz-plane from (10,0,0) over (20,0,5) to (10,0,10), bulging outwards:
            // at the shared pole the tangents (1,0,4) and (-1,0,4) make a real corner, and the line back to the
            // start closes the outline
            GeoPoint[] poles = { new GeoPoint(10, 0, 0), new GeoPoint(19, 0, 1), new GeoPoint(20, 0, 5), new GeoPoint(19, 0, 9), new GeoPoint(10, 0, 10) };
            BSpline spline = BSpline.Construct();
            Assert.IsTrue(spline.SetData(2, poles, null, new double[] { 0, 1, 2 }, new int[] { 3, 2, 3 }, false));
            Plane xz = new Plane(GeoPoint.Origin, GeoVector.XAxis, GeoVector.ZAxis);
            ICurve2D outline = (spline as ICurve).GetProjectedCurve(xz);
            ICurve2D closing = new Line2D(outline.EndPoint, outline.StartPoint);
            Face face = Face.MakeFace(new PlaneSurface(xz), new System.Collections.Generic.List<System.Collections.Generic.List<ICurve2D>>
                { new System.Collections.Generic.List<ICurve2D> { outline, closing } });
            Assert.AreEqual(2, face.AllEdges.Length, "the spline stays one edge of the face");
            Assert.AreEqual(2, spline.SplitAtCorners().Length, "the spline has a corner inside");

            Solid solid = Revolve(face, 2 * Math.PI) as Solid;
            Assert.IsNotNull(solid, "rotating the face gives a solid");
            Assert.AreEqual(0, solid.Shells[0].OpenEdgesExceptPoles.Length, "open edges");
            // Pappus: the volume is 2*pi times the first moment of the area about the axis, from a polygon of the outline
            double area = 0.0, moment = 0.0;
            GeoPoint2D[] polygon = new GeoPoint2D[2001];
            for (int i = 0; i <= 2000; i++) polygon[i] = xz.Project((spline as ICurve).PointAt(i / 2000.0));
            for (int i = 0; i < polygon.Length; i++)
            {
                GeoPoint2D p = polygon[i], q = polygon[(i + 1) % polygon.Length];
                double cross = p.x * q.y - q.x * p.y;
                area += cross / 2.0;
                moment += cross * (p.x + q.x) / 6.0;
            }
            Assert.AreEqual(2 * Math.PI * Math.Abs(moment), solid.Volume(0.01), 1e-3 * 2 * Math.PI * Math.Abs(moment), "volume");
        }
    }
}
