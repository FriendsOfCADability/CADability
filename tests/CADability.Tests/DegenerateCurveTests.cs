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
