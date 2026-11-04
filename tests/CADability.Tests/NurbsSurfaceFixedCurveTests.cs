using CADability.Curve2D;
using CADability.GeoObject;
using System.Reflection;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="NurbsSurface.FixedU(double, double, double)"/> and <see cref="NurbsSurface.FixedV(double, double, double)"/>
    /// on a surface whose internal NURBS data is not made yet, as it happens while a file is read and other objects use the
    /// surface before its own deserialization callback ran (ported from ShapeIt ac234a4). The curves were null there,
    /// and a cache kept the null result for good.
    /// </summary>
    [TestClass]
    public class NurbsSurfaceFixedCurveTests
    {
        private static NurbsSurface MakeSurface()
        {
            GeoPoint[,] poles = new GeoPoint[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    poles[i, j] = new GeoPoint(10 * i, 10 * j, (i == 1 && j == 1) ? 5.0 : 0.0);
                }
            }
            return new NurbsSurface(poles, null, new double[] { 0, 1 }, new double[] { 0, 1 },
                new int[] { 3, 3 }, new int[] { 3, 3 }, 2, 2, false, false);
        }

        private static NurbsSurface ReadBack(NurbsSurface surface)
        {
            Face face = Face.MakeFace(surface, new BoundingRect(0, 0, 1, 1));
            Face read = JsonSerialize.FromString(JsonSerialize.ToString(face)) as Face;
            Assert.IsNotNull(read);
            NurbsSurface res = read.Surface as NurbsSurface;
            Assert.IsNotNull(res);
            return res;
        }

        /// <summary>
        /// Puts the surface into the state it has during deserialization: the poles and knots are read, the internal
        /// NURBS objects are not made yet.
        /// </summary>
        private static void DropInternalNurbs(NurbsSurface surface)
        {
            foreach (string name in new[] { "nubs", "nurbs" })
            {
                FieldInfo field = typeof(NurbsSurface).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(field, name);
                field.SetValue(surface, null);
            }
        }

        private static void AssertOnSurface(NurbsSurface surface, ICurve curve, GeoPoint2D uvStart, GeoPoint2D uvEnd)
        {
            Assert.IsNotNull(curve);
            Assert.AreEqual(0.0, curve.StartPoint | surface.PointAt(uvStart), 1e-9);
            Assert.AreEqual(0.0, curve.EndPoint | surface.PointAt(uvEnd), 1e-9);
            GeoPoint2D uvMid = new GeoPoint2D((uvStart.x + uvEnd.x) / 2, (uvStart.y + uvEnd.y) / 2);
            Assert.AreEqual(0.0, curve.PointAt(0.5) | surface.PointAt(uvMid), 1e-9);
        }

        [TestMethod]
        public void FixedCurvesOfASurfaceBeingRead()
        {
            NurbsSurface surface = ReadBack(MakeSurface());
            DropInternalNurbs(surface);
            // the first call must not see uninitialized data, neither the second one a cached null
            for (int pass = 0; pass < 2; pass++)
            {
                AssertOnSurface(surface, surface.FixedU(0.3, 0.0, 1.0), new GeoPoint2D(0.3, 0.0), new GeoPoint2D(0.3, 1.0));
                AssertOnSurface(surface, surface.FixedV(0.6, 0.0, 1.0), new GeoPoint2D(0.0, 0.6), new GeoPoint2D(1.0, 0.6));
            }
        }

        [TestMethod]
        public void FixedCurvesOfAModifiedSurface()
        {
            NurbsSurface surface = MakeSurface();
            AssertOnSurface(surface, surface.FixedU(0.3, 0.0, 1.0), new GeoPoint2D(0.3, 0.0), new GeoPoint2D(0.3, 1.0));
            // after a modification the curve must follow the surface, not come from a cache of the old one
            surface.Modify(ModOp.Translate(0, 0, 7));
            AssertOnSurface(surface, surface.FixedU(0.3, 0.0, 1.0), new GeoPoint2D(0.3, 0.0), new GeoPoint2D(0.3, 1.0));
        }
    }
}
