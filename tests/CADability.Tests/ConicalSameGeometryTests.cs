using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="ConicalSurface.SameGeometry"/> must only report cones that are really identical. UniteBug17 has two
    /// 90° cones on a common axis with different apexes, opening towards each other: their surfaces are perpendicular,
    /// so every point of one cone is projected onto the same circle of the other one, which made them look identical.
    /// </summary>
    [TestClass]
    public class ConicalSameGeometryTests
    {
        private static readonly BoundingRect bounds = new BoundingRect(0.0, 1.0, 2 * Math.PI, 10.0);

        private static ConicalSurface Cone(GeoPoint apex, GeoVector axis, double semiAngle)
        {
            GeoVector dirz = axis.Normalized;
            GeoVector dirx = dirz ^ GeoVector.XAxis;
            if (dirx.Length < 0.1) dirx = dirz ^ GeoVector.YAxis;
            dirx.Norm();
            GeoVector diry = dirz ^ dirx;
            return new ConicalSurface(apex, dirx, diry, dirz, semiAngle);
        }

        private static void AssertMappingIsExact(ISurface first, ISurface second, ModOp2D firstToSecond)
        {
            for (int i = 0; i <= 4; i++)
            {
                GeoPoint2D uv = new GeoPoint2D(i * 1.3, 1.0 + i * 2.0);
                Assert.AreEqual(0.0, first.PointAt(uv) | second.PointAt(firstToSecond * uv), 1e-9, "the mapped point is the same 3d point");
            }
        }

        [TestMethod]
        public void perpendicular_cones_on_a_common_axis_are_different()
        {
            ConicalSurface up = Cone(new GeoPoint(20.3, 0, 5), GeoVector.ZAxis, Math.PI / 4);
            ConicalSurface down = Cone(new GeoPoint(20.3, 0, 25), -GeoVector.ZAxis, Math.PI / 4);
            Assert.IsFalse(up.SameGeometry(bounds, down, bounds, Precision.eps, out _));
            Assert.IsFalse(down.SameGeometry(bounds, up, bounds, Precision.eps, out _));
        }

        [TestMethod]
        public void cones_with_different_apexes_on_a_common_axis_are_different()
        {
            ConicalSurface c1 = Cone(new GeoPoint(0, 0, 0), GeoVector.ZAxis, 0.4);
            ConicalSurface c2 = Cone(new GeoPoint(0, 0, 1), GeoVector.ZAxis, 0.4);
            Assert.IsFalse(c1.SameGeometry(bounds, c2, bounds, Precision.eps, out _));
        }

        [TestMethod]
        public void rotated_cone_is_the_same()
        {
            ConicalSurface c1 = Cone(new GeoPoint(1, 2, 3), new GeoVector(1, 1, 1), 0.4);
            ISurface c2 = c1.GetModified(ModOp.Rotate(new GeoPoint(1, 2, 3), new GeoVector(1, 1, 1), new SweepAngle(0.7)));
            Assert.IsTrue(c1.SameGeometry(bounds, c2, bounds, Precision.eps, out ModOp2D m));
            AssertMappingIsExact(c1, c2, m);
        }

        [TestMethod]
        public void cone_with_reversed_axis_is_the_same()
        {   // same apex, opposite axis: the same double cone, v changes its sign (UniteBug10 has such a pair)
            ConicalSurface c1 = Cone(new GeoPoint(15, 0, -1), GeoVector.ZAxis, Math.PI / 4);
            ConicalSurface c2 = Cone(new GeoPoint(15, 0, -1), -GeoVector.ZAxis, Math.PI / 4);
            Assert.IsTrue(c1.SameGeometry(bounds, c2, bounds, Precision.eps, out ModOp2D m));
            AssertMappingIsExact(c1, c2, m);
        }

        [TestMethod]
        public void scaled_cone_is_the_same()
        {   // scaling around the apex keeps the surface but changes the v-parameter
            ConicalSurface c1 = Cone(new GeoPoint(1, 2, 3), new GeoVector(0, 0, 1), Math.PI / 4);
            ISurface c2 = c1.GetModified(ModOp.Scale(new GeoPoint(1, 2, 3), 10.0));
            Assert.IsTrue(c1.SameGeometry(bounds, c2, bounds, Precision.eps, out ModOp2D m));
            AssertMappingIsExact(c1, c2, m);
        }
    }
}
