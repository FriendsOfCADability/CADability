using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// A full <see cref="Arc2D"/> or <see cref="EllipseArc2D"/>, as <c>GetModified</c> returns for a circle or an ellipse whose
    /// start point moves, is closed although it is not a <see cref="Circle2D"/> or <see cref="Ellipse2D"/> (#416, finding 4).
    /// Code that told closed from open curves by their type took it for an open arc, whose start and end point coincide.
    /// </summary>
    [TestClass]
    public class FullArcTests
    {
        private static readonly Arc2D FullArc = new Arc2D(GeoPoint2D.Origin, 3.75, new Angle(0.7), new SweepAngle(2.0 * Math.PI));

        [TestMethod]
        public void FusingAnArcWithAFullArcGivesTheFullArc()
        {
            // inside the full arc, reversed, and across its start point at 0.7
            foreach (Arc2D part in new[] { new Arc2D(GeoPoint2D.Origin, 3.75, new Angle(1.0), new SweepAngle(1.0)), new Arc2D(GeoPoint2D.Origin, 3.75, new Angle(5.0), new SweepAngle(-2.0)),
                new Arc2D(GeoPoint2D.Origin, 3.75, new Angle(0.2), new SweepAngle(1.0)), new Arc2D(GeoPoint2D.Origin, 3.75, new Angle(1.2), new SweepAngle(-1.0)) })
            {
                foreach (ICurve2D fused in new[] { FullArc.GetFused(part, 1e-6), part.GetFused(FullArc, 1e-6) })
                {
                    Assert.IsNotNull(fused);
                    Assert.IsTrue(fused.IsClosed, "the full arc contains the other one");
                    Assert.AreEqual(2.0 * Math.PI * 3.75, fused.Length, 1e-9);
                }
            }
        }

        [TestMethod]
        public void FusingAnEllipticArcWithAFullOneGivesTheFullOne()
        {
            ModOp2D m = ModOp2D.Scale(1, 2); // moves the start point of the circle away from the major axis
            EllipseArc2D full = new Circle2D(GeoPoint2D.Origin, 2.0).GetModified(m) as EllipseArc2D;
            Assert.IsNotNull(full, "a full elliptic arc");
            Assert.IsTrue(full.IsClosed);
            // reversed, and across the start point of the full arc at angle 0 of the circle
            foreach (Arc2D arc in new[] { new Arc2D(GeoPoint2D.Origin, 2.0, new Angle(2.0), new SweepAngle(-1.5)), new Arc2D(GeoPoint2D.Origin, 2.0, new Angle(5.5), new SweepAngle(1.2)) })
            {
                EllipseArc2D part = (EllipseArc2D)arc.GetModified(m);
                GeoPoint2D partStart = part.StartPoint;
                foreach (ICurve2D fused in new[] { full.GetFused(part, 1e-6), part.GetFused(full, 1e-6) })
                {
                    Assert.IsNotNull(fused);
                    Assert.IsTrue(fused.IsClosed, "the full arc contains the other one");
                    Assert.AreEqual(full.Length, fused.Length, 1e-9);
                }
                Assert.IsTrue((part.StartPoint | partStart) < 1e-12, "fusing must not reverse one of the arcs");
            }
        }

        [TestMethod]
        public void AFullArcInTheUvSystemOfACylinderOrConeIsAClosedCurve()
        {
            ISurface[] surfaces =
            {
                new CylindricalSurfaceNP(new GeoPoint(1, 2, 3), 5 * GeoVector.XAxis, 5 * GeoVector.YAxis, 10 * GeoVector.ZAxis),
                new ConicalSurfaceNP(new GeoPoint(1, 2, 3), GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, 0.4),
            };
            foreach (ISurface surface in surfaces)
            {
                ICurve curve = surface.Make3dCurve(FullArc);
                Assert.IsInstanceOfType(curve, typeof(Ellipse), surface.GetType().Name);
                Assert.IsTrue(curve.IsClosed, $"{surface.GetType().Name}: a full circle around the axis");
                for (int i = 0; i < 16; i++)
                {
                    GeoPoint p = surface.PointAt(FullArc.PointAt(i / 16.0));
                    Assert.IsTrue(curve.DistanceTo(p) < 1e-8, $"{surface.GetType().Name}: point {i} on the curve");
                }
            }
        }
    }
}
