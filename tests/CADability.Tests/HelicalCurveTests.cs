using CADability.GeoObject;
using System;

namespace CADability.Tests
{
    [TestClass]
    public class HelicalCurveTests
    {
        /// <summary>
        /// A helix around the z-axis: radius 10, pitch 5, two turns, starting at (10,0,0).
        /// </summary>
        private static HelicalCurve MakeTestHelix()
        {
            return HelicalCurve.FromAxisStartPoint(GeoPoint.Origin, GeoVector.ZAxis, new GeoPoint(10, 0, 0), 5.0, 2.0);
        }

        [TestMethod]
        public void PointAt_MatchesAnalyticFormula()
        {
            HelicalCurve h = MakeTestHelix();
            for (int i = 0; i <= 20; ++i)
            {
                double t = i / 20.0;
                double a = t * 4.0 * Math.PI; // two turns
                GeoPoint expected = new GeoPoint(10 * Math.Cos(a), 10 * Math.Sin(a), 5.0 * a / (2 * Math.PI));
                Assert.IsTrue((h.PointAt(t) | expected) < 1e-9, "point at " + t);
            }
            Assert.IsTrue((h.StartPoint | new GeoPoint(10, 0, 0)) < 1e-9);
            Assert.IsTrue((h.EndPoint | new GeoPoint(10, 0, 10)) < 1e-9);
        }

        [TestMethod]
        public void DirectionAt_MatchesNumericalDerivative()
        {
            HelicalCurve h = MakeTestHelix();
            const double eps = 1e-6;
            for (int i = 1; i < 20; ++i)
            {
                double t = i / 20.0;
                GeoVector numeric = (1.0 / (2 * eps)) * (h.PointAt(t + eps) - h.PointAt(t - eps));
                Assert.IsTrue((h.DirectionAt(t) - numeric).Length < 1e-4, "direction at " + t);
            }
        }

        [TestMethod]
        public void TryPointDeriv2At_MatchesNumericalSecondDerivative()
        {
            HelicalCurve h = MakeTestHelix();
            const double eps = 1e-5;
            for (int i = 1; i < 10; ++i)
            {
                double t = i / 10.0;
                Assert.IsTrue(h.TryPointDeriv2At(t, out GeoPoint p, out GeoVector d1, out GeoVector d2));
                Assert.IsTrue((p | h.PointAt(t)) < 1e-9);
                GeoVector numeric = (1.0 / (2 * eps)) * (h.DirectionAt(t + eps) - h.DirectionAt(t - eps));
                Assert.IsTrue((d2 - numeric).Length < 1e-3, "2nd derivative at " + t);
            }
        }

        [TestMethod]
        public void Length_IsExact()
        {
            HelicalCurve h = MakeTestHelix();
            // arc length of two turns: sqrt((2*pi*r)^2 + pitch^2) per turn
            double expected = 2.0 * Math.Sqrt(Math.Pow(2 * Math.PI * 10, 2) + 25.0);
            Assert.AreEqual(expected, h.Length, 1e-8);
        }

        [TestMethod]
        public void PositionOf_ReturnsPositionOfPointsOnTheCurve()
        {
            HelicalCurve h = MakeTestHelix();
            for (int i = 0; i <= 20; ++i)
            {
                double t = i / 20.0;
                double found = h.PositionOf(h.PointAt(t));
                Assert.AreEqual(t, found, 1e-8, "position of point at " + t);
            }
        }

        [TestMethod]
        public void PositionOf_PointOffTheCurveIsTheFootPoint()
        {
            HelicalCurve h = MakeTestHelix();
            double t = 0.375;
            GeoPoint onCurve = h.PointAt(t);
            // move away perpendicular to the curve, the foot point must stay the same
            GeoVector offset = h.DirectionAt(t) ^ GeoVector.ZAxis;
            offset.Norm();
            double found = h.PositionOf(onCurve + 0.5 * offset);
            Assert.AreEqual(t, found, 1e-6);
        }

        [TestMethod]
        public void DistanceTo_PointOnAxisIsRadius()
        {
            HelicalCurve h = MakeTestHelix();
            GeoPoint onCurve = h.PointAt(0.5);
            Assert.AreEqual(0.0, h.DistanceTo(onCurve), 1e-8);
        }

        [TestMethod]
        public void Reverse_KeepsGeometry()
        {
            HelicalCurve h = MakeTestHelix();
            GeoPoint sp = h.StartPoint, ep = h.EndPoint;
            GeoPoint mid = h.PointAt(0.25);
            h.Reverse();
            Assert.IsTrue((h.StartPoint | ep) < 1e-9);
            Assert.IsTrue((h.EndPoint | sp) < 1e-9);
            Assert.IsTrue((h.PointAt(0.75) | mid) < 1e-9);
        }

        [TestMethod]
        public void Trim_KeepsGeometry()
        {
            HelicalCurve h = MakeTestHelix();
            GeoPoint p25 = h.PointAt(0.25), p50 = h.PointAt(0.5), p75 = h.PointAt(0.75);
            h.Trim(0.25, 0.75);
            Assert.IsTrue((h.StartPoint | p25) < 1e-9);
            Assert.IsTrue((h.EndPoint | p75) < 1e-9);
            Assert.IsTrue((h.PointAt(0.5) | p50) < 1e-9);
        }

        [TestMethod]
        public void Split_YieldsTheTwoParts()
        {
            HelicalCurve h = MakeTestHelix();
            GeoPoint pm = h.PointAt(0.3);
            ICurve[] parts = h.Split(0.3);
            Assert.AreEqual(2, parts.Length);
            Assert.IsTrue((parts[0].StartPoint | h.StartPoint) < 1e-9);
            Assert.IsTrue((parts[0].EndPoint | pm) < 1e-9);
            Assert.IsTrue((parts[1].StartPoint | pm) < 1e-9);
            Assert.IsTrue((parts[1].EndPoint | h.EndPoint) < 1e-9);
            Assert.AreEqual(h.Length, parts[0].Length + parts[1].Length, 1e-8);
        }

        [TestMethod]
        public void Modify_RigidMotionKeepsRadiusAndPitch()
        {
            HelicalCurve h = MakeTestHelix();
            ModOp m = ModOp.Rotate(new GeoPoint(1, 2, 3), GeoVector.XAxis, new SweepAngle(0.7)) * ModOp.Translate(3, -4, 5);
            GeoPoint p = h.PointAt(0.4);
            h.Modify(m);
            Assert.AreEqual(10.0, h.Radius, 1e-9);
            Assert.AreEqual(5.0, h.Pitch, 1e-9);
            Assert.IsTrue((h.PointAt(0.4) | (m * p)) < 1e-8);
        }

        [TestMethod]
        public void Modify_UniformScaleScalesRadiusAndPitch()
        {
            HelicalCurve h = MakeTestHelix();
            GeoPoint p = h.PointAt(0.4);
            ModOp m = ModOp.Scale(3.0);
            h.Modify(m);
            Assert.AreEqual(30.0, h.Radius, 1e-9);
            Assert.AreEqual(15.0, h.Pitch, 1e-9);
            Assert.IsTrue((h.PointAt(0.4) | (m * p)) < 1e-8);
        }

        [TestMethod]
        public void Modify_MirroringInvertsTheHandedness()
        {
            HelicalCurve h = MakeTestHelix();
            Assert.IsFalse(h.IsLeftHanded);
            ModOp m = ModOp.ReflectPlane(Plane.XYPlane);
            GeoPoint p = h.PointAt(0.4);
            h.Modify(m);
            Assert.IsTrue(h.IsLeftHanded);
            Assert.IsTrue((h.PointAt(0.4) | (m * p)) < 1e-8);
        }

        [TestMethod]
        public void GetBoundingCube_ContainsTheCurve()
        {
            HelicalCurve h = MakeTestHelix();
            BoundingBox bb = h.GetBoundingCube();
            for (int i = 0; i <= 100; ++i)
            {
                Assert.IsTrue(bb.Contains(h.PointAt(i / 100.0), 1e-6), "point at " + i);
            }
            // the helix touches the cylinder of radius 10
            Assert.AreEqual(-10.0, bb.Xmin, 1e-3);
            Assert.AreEqual(10.0, bb.Xmax, 1e-3);
        }

        [TestMethod]
        public void PlanarState_ZeroPitchIsPlanar()
        {
            HelicalCurve h = HelicalCurve.FromAxisStartPoint(GeoPoint.Origin, GeoVector.ZAxis, new GeoPoint(10, 0, 0), 0.0, 1.0);
            Assert.AreEqual(PlanarState.Planar, h.GetPlanarState());
            Assert.IsTrue(h.IsInPlane(Plane.XYPlane));
            Assert.IsTrue(h.IsClosed);
            Assert.AreEqual(2 * Math.PI * 10, h.Length, 1e-8);
        }

        [TestMethod]
        public void PlanarState_HelixIsNotPlanar()
        {
            Assert.AreEqual(PlanarState.NonPlanar, MakeTestHelix().GetPlanarState());
            Assert.IsFalse(MakeTestHelix().IsClosed);
        }

        [TestMethod]
        public void TangentPosition_FindsTheTangentsPerTurn()
        {
            HelicalCurve h = MakeTestHelix();
            double t = 0.3;
            double[] positions = h.TangentPosition(h.DirectionAt(t));
            Assert.IsNotNull(positions);
            bool found = false;
            foreach (double p in positions)
            {
                Assert.IsTrue(Precision.SameDirection(h.DirectionAt(p), h.DirectionAt(t), false), "direction at " + p);
                if (Math.Abs(p - t) < 1e-8) found = true;
            }
            Assert.IsTrue(found, "the queried position itself must be among the results");
            Assert.AreEqual(2, positions.Length); // two turns: the direction repeats once per turn
        }

        [TestMethod]
        public void PointAndDerivativesAt_MatchesTryPointDeriv2At()
        {
            HelicalCurve h = MakeTestHelix();
            var ders = h.PointAndDerivativesAt(0.42, 3);
            Assert.IsTrue(h.TryPointDeriv2At(0.42, out GeoPoint p, out GeoVector d1, out GeoVector d2));
            Assert.IsTrue((ders[0] - p.ToVector()).Length < 1e-9);
            Assert.IsTrue((ders[1] - d1).Length < 1e-9);
            Assert.IsTrue((ders[2] - d2).Length < 1e-9);
            // third derivative, numerically from the second one
            const double eps = 1e-5;
            h.TryPointDeriv2At(0.42 + eps, out _, out _, out GeoVector d2p);
            h.TryPointDeriv2At(0.42 - eps, out _, out _, out GeoVector d2m);
            GeoVector numeric = (1.0 / (2 * eps)) * (d2p - d2m);
            Assert.IsTrue((ders[3] - numeric).Length < 1e-2);
        }

        [TestMethod]
        public void ParameterToPosition_IsInverseOfPositionToParameter()
        {
            ICurve h = MakeTestHelix();
            for (int i = 0; i <= 10; ++i)
            {
                double t = i / 10.0;
                Assert.AreEqual(t, h.ParameterToPosition(h.PositionToParameter(t)), 1e-12);
            }
        }

        [TestMethod]
        public void JsonSerialization_RoundTrip()
        {
            HelicalCurve h = MakeTestHelix();
            h.ColorDef = new CADability.Attribute.ColorDef("red", System.Drawing.Color.Red);
            // the curve itself is serialized: reading a whole project needs System.Drawing printing, i.e. Windows
            HelicalCurve restored = JsonSerialize.FromString(JsonSerialize.ToString(h)) as HelicalCurve;
            Assert.IsNotNull(restored, "the helical curve must survive the round trip");
            Assert.AreEqual(h.Radius, restored.Radius, 1e-12);
            Assert.AreEqual(h.Pitch, restored.Pitch, 1e-12);
            Assert.AreEqual(h.StartParameter, restored.StartParameter, 1e-12);
            Assert.AreEqual(h.SweepParameter, restored.SweepParameter, 1e-12);
            Assert.IsTrue((h.PointAt(0.33) | restored.PointAt(0.33)) < 1e-9);
            Assert.IsNotNull(restored.ColorDef);
        }
    }
}
