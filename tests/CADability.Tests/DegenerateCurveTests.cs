using System.Reflection;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Curves for which HasValidData is false can be in a model (issue #303). The operations a model applies to its objects
    /// must not fail on them: a polyline without vertices could not be cloned, moved, reversed or read back after saving, and
    /// an arc without sweep could not be approximated.
    /// </summary>
    [TestClass]
    public class DegenerateCurveTests
    {
        [TestMethod]
        public void an_empty_polyline_can_be_cloned_moved_reversed_and_saved()
        {
            Polyline empty = Polyline.Construct();
            Assert.IsFalse(empty.HasValidData());
            Assert.AreEqual(0, ((Polyline)empty.Clone()).PointCount);
            empty.Modify(ModOp.Translate(1, 2, 3));
            empty.IsClosed = true;
            empty.Reverse();
            Polyline read = (Polyline)JsonSerialize.FromString(JsonSerialize.ToString(empty));
            Assert.AreEqual(0, read.PointCount);
            Assert.IsTrue(read.IsClosed);
            read.AddPoint(GeoPoint.Origin);
            read.AddPoint(new GeoPoint(10, 0, 0));
            Assert.IsTrue(read.HasValidData(), "points can be added after reading");
        }

        [TestMethod]
        public void a_polyline_saved_without_vertices_can_be_read()
        {
            // older versions kept null instead of an empty array, and wrote "Vertex": null
            Polyline empty = Polyline.Construct();
            typeof(Polyline).GetField("vertex", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(empty, null);
            string json = JsonSerialize.ToString(empty);
            StringAssert.Contains(json, "null");
            Polyline read = (Polyline)JsonSerialize.FromString(json);
            Assert.AreEqual(0, read.PointCount);
            Assert.AreEqual(0, ((Polyline)read.Clone()).PointCount);
        }

        private static Polyline EmptyPolyline(bool closed)
        {
            Polyline empty = Polyline.Construct();
            empty.IsClosed = closed;
            return empty;
        }

        private static Polyline SinglePointPolyline(bool closed)
        {
            Polyline single = Polyline.Construct();
            single.AddPoint(new GeoPoint(3, 4, 0));
            single.IsClosed = closed;
            return single;
        }

        private static void AssertNoPoints(Action action, string what)
        {
            PolylineException e = Assert.ThrowsException<PolylineException>(action, what);
            Assert.AreEqual(PolylineException.PolylineExceptionType.NoPoints, e.ExceptionType, what);
        }

        /// <summary>
        /// The curve operations that have an answer without a point gave IndexOutOfRangeException, NullReferenceException or
        /// OverflowException on a polyline without vertices, a closed one even for its length.
        /// </summary>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void an_empty_polyline_answers_what_needs_no_point(bool closed)
        {
            ICurve curve = EmptyPolyline(closed);
            Assert.AreEqual(0.0, curve.Length);
            Assert.AreEqual(0, curve.SubCurves.Length);
            Assert.AreEqual(0, curve.GetExtrema(GeoVector.XAxis).Length);
            Assert.AreEqual(double.MaxValue, curve.DistanceTo(new GeoPoint(1, 2, 3)));
            Assert.AreEqual(-1.0, curve.PositionOf(new GeoPoint(1, 2, 3)));
            Assert.AreEqual(-1.0, curve.PositionOf(new GeoPoint(1, 2, 3), Plane.XYPlane));
            Assert.AreEqual(0, curve.Split(0.5).Length);
            Assert.AreEqual(0, curve.Split(0.2, 0.7).Length);
            curve.Trim(0.2, 0.7);
            Assert.AreEqual(0, ((Polyline)curve).PointCount);
            Assert.IsFalse(curve.HitTest(new BoundingCube(new GeoPoint(-100, -100, -100), new GeoPoint(100, 100, 100))));
        }

        /// <summary>
        /// A polyline without vertices has no point to return. It throws a PolylineException of type NoPoints, as
        /// StartDirection already did, instead of an IndexOutOfRangeException.
        /// </summary>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void an_empty_polyline_has_no_points(bool closed)
        {
            Polyline empty = EmptyPolyline(closed);
            AssertNoPoints(() => _ = empty.StartPoint, "StartPoint");
            AssertNoPoints(() => _ = empty.EndPoint, "EndPoint");
            AssertNoPoints(() => empty.StartPoint = GeoPoint.Origin, "set StartPoint");
            AssertNoPoints(() => empty.EndPoint = GeoPoint.Origin, "set EndPoint");
            AssertNoPoints(() => empty.PointAt(0.5), "PointAt");
            AssertNoPoints(() => empty.DirectionAt(0.5), "DirectionAt");
            AssertNoPoints(() => empty.PointAndDerivativesAt(0.5, 1), "PointAndDerivativesAt");
        }

        /// <summary>
        /// A polyline with a single vertex has a point but no direction: DirectionAt threw a GeoVectorException from
        /// normalising the null vector, Trim an IndexOutOfRangeException and PositionOf with a plane a NullReferenceException.
        /// </summary>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void a_polyline_with_one_vertex_has_a_point_but_no_direction(bool closed)
        {
            Polyline single = SinglePointPolyline(closed);
            ICurve curve = single;
            Assert.AreEqual(new GeoPoint(3, 4, 0), curve.StartPoint);
            Assert.AreEqual(new GeoPoint(3, 4, 0), curve.EndPoint);
            Assert.AreEqual(new GeoPoint(3, 4, 0), curve.PointAt(0.5));
            Assert.AreEqual(0.0, curve.Length);
            Assert.AreEqual(5.0, curve.DistanceTo(GeoPoint.Origin), 1e-12);
            Assert.AreEqual(-1.0, curve.PositionOf(GeoPoint.Origin, Plane.XYPlane));
            AssertNoPoints(() => curve.DirectionAt(0.5), "DirectionAt");
            Assert.AreEqual(0, curve.Split(0.5).Length);
            curve.Trim(0.2, 0.7);
            Assert.AreEqual(1, single.PointCount);
        }

        /// <summary>
        /// Invalid objects can be in a model (#303). Picking in the model already skipped a polyline without vertices,
        /// because the oct tree returns no object without extent, but the hit tests of the polyline itself failed when called
        /// directly: the one with a pick area read vertex[0], and a closed polyline broke the quad tree item, the cube hit test
        /// and the pick position.
        /// </summary>
        [TestMethod]
        public void a_model_with_empty_polylines_can_be_picked()
        {
            Project project = Project.CreateSimpleProject();
            Model model = project.GetActiveModel();
            Line line = Line.Construct();
            line.SetTwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0));
            model.Add(line);
            model.Add(EmptyPolyline(false));
            model.Add(EmptyPolyline(true));
            model.Add(SinglePointPolyline(true));
            Assert.AreEqual(4, model.Count);

            Projection projection = new Projection(Projection.StandardProjection.FromTop);
            BoundingRect window = new BoundingRect(-100, -100, 100, 100);
            Projection.PickArea area = projection.GetPickSpace(window);
            foreach (IGeoObject go in model.AllObjects)
            {
                if (go is Polyline polyline && polyline.PointCount == 0)
                {
                    Assert.IsFalse(go.HitTest(area, false), "an empty polyline cannot be picked");
                    Assert.IsFalse(go.HitTest(area, true), "an empty polyline is not inside a window");
                    Assert.IsFalse(go.HitTest(projection, window, false));
                    Assert.AreEqual(double.MaxValue, go.Position(area.FrontCenter, area.Direction, 0.0));
                }
                Assert.IsNotNull(go.GetQuadTreeItem(projection, ExtentPrecision.Raw));
            }
            foreach (PickMode pickMode in new[] { PickMode.normal, PickMode.single, PickMode.singleEdge, PickMode.children })
            {
                GeoObjectList picked = model.GetObjectsFromRect(area, null, pickMode, null);
                Assert.IsTrue(picked.Contains(line), pickMode.ToString());
                Assert.IsFalse(picked.Any(go => go is Polyline polyline && polyline.PointCount == 0), pickMode.ToString());
            }
        }

        [TestMethod]
        public void an_arc_without_sweep_can_be_approximated()
        {
            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, new GeoPoint(5, 5, 0), 3, 0, 0);
            Assert.IsFalse(arc.HasValidData());
            foreach (bool linesOnly in new[] { true, false })
            {
                ICurve approximation = (arc as ICurve).Approximate(linesOnly, 0.01);
                Assert.IsNotNull(approximation);
                Assert.AreEqual(0.0, approximation.Length, 1e-12);
                Assert.AreEqual(0.0, approximation.StartPoint | arc.StartPoint, 1e-12);
            }
        }
    }
}
