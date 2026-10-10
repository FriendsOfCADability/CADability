using CADability.Curve2D;
using System;
using System.Collections.Generic;

namespace CADability.Tests
{
    /// <summary>
    /// ModOp2D.IsIsogonal (rotation or reflection times a uniform scaling, plus a translation) compared rows where it
    /// should compare columns, with a wrong sign, so it was false even for the identity: Circle2D and Arc2D became
    /// Ellipse2D and EllipseArc2D under every modification except a few rotations by odd multiples of 45 degrees.
    /// ModOp.IsIsogonal (3d) did not compare the lengths of the columns, so a non-uniform scaling counted as isogonal.
    /// Circle2D.GetModified lost the start point and the direction of the circle, and Arc2D.GetModified the direction of a
    /// full arc under a reflection that is not isogonal.
    /// </summary>
    [TestClass]
    public class IsogonalModOpTests
    {
        private static readonly double Rad30 = Math.PI / 6;

        private static IEnumerable<(string name, ModOp2D m, bool isogonal)> Modifications()
        {
            yield return ("identity", ModOp2D.Identity, true);
            yield return ("translation", ModOp2D.Translate(3, -4), true);
            yield return ("rotation 30", ModOp2D.Rotate(new GeoPoint2D(1, 2), new SweepAngle(Rad30)), true);
            yield return ("rotation 45", ModOp2D.Rotate(new SweepAngle(Math.PI / 4)), true);
            yield return ("rotation 180", ModOp2D.Rotate(new SweepAngle(Math.PI)), true);
            yield return ("scaling 2", ModOp2D.Scale(new GeoPoint2D(1, 1), 2), true);
            yield return ("scaling and rotation", ModOp2D.Translate(5, 1) * ModOp2D.Rotate(new SweepAngle(Rad30)) * ModOp2D.Scale(0.5), true);
            yield return ("reflection x-axis", ModOp2D.Reflect(GeoPoint2D.Origin, GeoVector2D.XAxis), true);
            yield return ("reflection 30", ModOp2D.Reflect(new GeoPoint2D(2, 0), new GeoVector2D(new Angle(Rad30))), true);
            yield return ("reflection and scaling", ModOp2D.Scale(3) * ModOp2D.Reflect(GeoPoint2D.Origin, GeoVector2D.YAxis), true);
            yield return ("non-uniform scaling", ModOp2D.Scale(2, 1), false);
            yield return ("non-uniform reflection", ModOp2D.Scale(-2, 1), false);
            yield return ("shear", new ModOp2D(1, 0.5, 0, 0, 1, 0), false);
            yield return ("rotation of a non-uniform scaling", ModOp2D.Rotate(new SweepAngle(Rad30)) * ModOp2D.Scale(1, 3), false);
        }

        private static IEnumerable<(string name, ICurve2D curve)> Curves()
        {
            yield return ("circle", new Circle2D(new GeoPoint2D(1, 2), 3));
            Circle2D clockwise = new Circle2D(new GeoPoint2D(1, 2), 3);
            clockwise.Reverse();
            yield return ("clockwise circle", clockwise);
            yield return ("arc", new Arc2D(new GeoPoint2D(1, 2), 3, new Angle(0.3), new SweepAngle(2.0)));
            yield return ("clockwise arc", new Arc2D(new GeoPoint2D(1, 2), 3, new Angle(0.3), new SweepAngle(-2.0)));
            yield return ("arc over 0", new Arc2D(new GeoPoint2D(-1, 0), 2, new Angle(5.5), new SweepAngle(1.5)));
            yield return ("full arc", new Arc2D(new GeoPoint2D(0, 0), 2, new Angle(1.0), SweepAngle.Full));
        }

        [TestMethod]
        public void IsIsogonal2D()
        {
            foreach ((string name, ModOp2D m, bool isogonal) in Modifications())
            {
                Assert.AreEqual(isogonal, m.IsIsogonal, name);
            }
        }

        [TestMethod]
        public void IsIsogonal3D()
        {
            Assert.IsTrue(ModOp.Identity.IsIsogonal, "identity");
            Assert.IsTrue(ModOp.Rotate(new GeoVector(1, 2, 3), new SweepAngle(0.7)).IsIsogonal, "rotation");
            Assert.IsTrue((ModOp.Scale(2.5) * ModOp.Rotate(GeoVector.XAxis, new SweepAngle(0.3))).IsIsogonal, "rotation and uniform scaling");
            Assert.IsTrue(ModOp.Scale(-1, 1, 1).IsIsogonal, "reflection");
            Assert.IsFalse(ModOp.Scale(2, 1, 1).IsIsogonal, "non-uniform scaling");
            Assert.IsFalse(ModOp.Scale(new GeoVector(1, 1, 0), 2).IsIsogonal, "scaling in one direction");
        }

        [TestMethod]
        public void ModifiedCirclesAndArcs()
        {
            // the modified curve is the modification of the curve, point by point and in the same direction, and an
            // isogonal modification keeps a circle a circle and an arc an arc
            foreach ((string mName, ModOp2D m, bool isogonal) in Modifications())
            {
                foreach ((string cName, ICurve2D curve) in Curves())
                {
                    ICurve2D modified = curve.GetModified(m);
                    for (int i = 0; i <= 20; i++)
                    {
                        double t = i / 20.0;
                        GeoPoint2D expected = m * curve.PointAt(t);
                        Assert.IsTrue((modified.PointAt(t) | expected) < 1e-9, $"{cName}, {mName}: point at {t} is {modified.PointAt(t)} instead of {expected}");
                        if (i > 0 && i < 20) Assert.AreEqual(t, modified.PositionOf(expected), 1e-9, $"{cName}, {mName}: position of the point at {t}");
                    }
                    if (isogonal) Assert.IsTrue(modified is Circle2D || modified is Arc2D, $"{cName}, {mName}: {modified.GetType().Name}");
                    else Assert.IsTrue(modified is Ellipse2D, $"{cName}, {mName}: {modified.GetType().Name}");
                }
            }
        }
    }
}
