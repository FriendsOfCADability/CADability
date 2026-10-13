using System;
using System.Linq;
using System.Reflection;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #431: the constructor of <see cref="ParametricRotationProperty"/> did not store the axis, and the angle
    /// computed at the axis edge was overwritten by the fallback values.
    /// </summary>
    [TestClass]
    public class ParametricRotationPropertyTests
    {
        private static (Face top, Face front, Edge common) BoxFaces()
        {
            Solid box = Make3D.MakeBox(GeoPoint.Origin, 10.0 * GeoVector.XAxis, 10.0 * GeoVector.YAxis, 10.0 * GeoVector.ZAxis);
            Shell shell = box.Shells[0];
            Face top = shell.Faces.Single(f => f.GetExtent(0.0).Zmin > 9.9);
            Face front = shell.Faces.Single(f => f.GetExtent(0.0).Ymax < 0.1);
            Edge common = top.AllEdges.Intersect(front.AllEdges).Single();
            return (top, front, common);
        }

        [TestMethod]
        public void AxisIsStoredAndTheAngleIsMeasuredAtIt()
        {
            (Face top, Face front, Edge common) = BoxFaces();
            ParametricRotationProperty property = new ParametricRotationProperty("rotation", new[] { front }, new[] { top }, new object[0], ParametricRotationProperty.Mode.symmetric, common);

            FieldInfo axisField = typeof(ParametricRotationProperty).GetField("axis", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(axisField);
            Assert.AreSame(common, axisField.GetValue(property), "the constructor stores the axis");
            Assert.AreEqual(Math.PI / 2.0, property.Value, 1e-8, "the faces of a box meet at a right angle");
        }

        [TestMethod]
        public void AngleDoesNotDependOnWhichFaceIsPrimary()
        {
            (Face top, Face front, Edge common) = BoxFaces();
            ParametricRotationProperty property = new ParametricRotationProperty("rotation", new[] { top }, new[] { front }, new object[0], ParametricRotationProperty.Mode.symmetric, common);
            Assert.AreEqual(Math.PI / 2.0, property.Value, 1e-8);
        }
    }
}
