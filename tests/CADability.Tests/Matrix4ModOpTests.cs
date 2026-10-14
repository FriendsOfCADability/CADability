using System;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #434: <see cref="Matrix4(ModOp)"/> built a 3x4 matrix from <see cref="ModOp.Matrix"/>, and the operators of
    /// <see cref="Matrix4"/>, which read the fourth row, threw. <c>PrintToGDI</c> builds its projection this way.
    /// </summary>
    [TestClass]
    public class Matrix4ModOpTests
    {
        [TestMethod]
        public void TranslationMovesPointsAndKeepsVectors()
        {
            Matrix4 matrix = new Matrix4(ModOp.Translate(1.0, 2.0, 3.0));
            GeoPoint point = matrix * new GeoPoint(1.0, 1.0, 1.0);
            Assert.AreEqual(2.0, point.x, 1e-12);
            Assert.AreEqual(3.0, point.y, 1e-12);
            Assert.AreEqual(4.0, point.z, 1e-12);
            GeoVector vector = matrix * new GeoVector(1.0, 1.0, 1.0);
            Assert.AreEqual(1.0, vector.x, 1e-12);
            Assert.AreEqual(1.0, vector.y, 1e-12);
            Assert.AreEqual(1.0, vector.z, 1e-12);
        }

        [TestMethod]
        public void RotationAgreesWithTheModOp()
        {
            ModOp rotation = ModOp.Rotate(GeoVector.ZAxis, Math.PI / 3.0);
            Matrix4 matrix = new Matrix4(rotation);
            GeoPoint p = new GeoPoint(2.0, -1.0, 0.5);
            GeoPoint expected = rotation * p;
            GeoPoint actual = matrix * p;
            Assert.AreEqual(0.0, expected | actual, 1e-12);
            Assert.AreEqual(4, matrix.hm.RowCount);
            Assert.AreEqual(4, matrix.hm.ColumnCount);
        }
    }
}
