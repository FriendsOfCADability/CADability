using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CADability.Tests
{
    /// <summary>
    /// Fixes of surfaces and curves taken over from the ShapeIt fork (develop 570d479): the new implementation of
    /// <see cref="SurfaceOfRevolution"/> (a 3d curve rotated about an axis), the mixed derivative of
    /// <see cref="RuledSurface"/>, the derivatives of <see cref="Path"/>, and the comparison of short arcs.
    /// </summary>
    [TestClass]
    public class ShapeItSurfaceFixes2Tests
    {
        private static ICurve Profile(int kind)
        {
            switch (kind)
            {
                case 0:
                    {   // a path of a line and an arc: its PositionToParameter is linear in the position, ParameterToPosition maps by length
                        Line l1 = Line.TwoPoints(new GeoPoint(10, 0, 0), new GeoPoint(10, 0, 2));
                        Ellipse a = Ellipse.Construct();
                        a.SetArc3Points(new GeoPoint(10, 0, 2), new GeoPoint(11, 0, 3), new GeoPoint(12, 0, 2.5), Plane.XZPlane);
                        GeoObject.Path p = GeoObject.Path.Construct();
                        p.Set(new ICurve[] { l1, a });
                        return p;
                    }
                case 1:
                    {   // an elliptical arc with a negative sweep
                        Ellipse el = Ellipse.Construct();
                        el.SetEllipseCenterAxis(new GeoPoint(15, 0, 3), new GeoVector(3, 0, 0), new GeoVector(0, 0, 1.5));
                        el.StartParameter = 4.0;
                        el.SweepParameter = -2.5;
                        return el;
                    }
                default:
                    {
                        BSpline bs = BSpline.Construct();
                        bs.ThroughPoints(new GeoPoint[] { new GeoPoint(10, 0, 0), new GeoPoint(12, 0, 3), new GeoPoint(11, 0, 6), new GeoPoint(14, 0, 9) }, 3, false);
                        return bs;
                    }
            }
        }

        private static IEnumerable<GeoPoint2D> InnerPoints(ICurve profile)
        {
            double vs = profile.PositionToParameter(0.0), ve = profile.PositionToParameter(1.0);
            for (int i = 1; i < 10; i++) yield return new GeoPoint2D(0.3 + 0.5 * i, vs + (ve - vs) * (i + 0.37) / 10.0);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void RevolutionDerivativesMatchPointAt(int kind)
        {
            ICurve profile = Profile(kind);
            SurfaceOfRevolution s = new SurfaceOfRevolution(profile, GeoPoint.Origin, GeoVector.ZAxis);
            double vs = profile.PositionToParameter(0.0), ve = profile.PositionToParameter(1.0);
            double hv = 1e-6 * Math.Abs(ve - vs), hu = 1e-6;
            foreach (GeoPoint2D uv in InnerPoints(profile))
            {
                GeoVector fdv = (1.0 / (2 * hv)) * (s.PointAt(new GeoPoint2D(uv.x, uv.y + hv)) - s.PointAt(new GeoPoint2D(uv.x, uv.y - hv)));
                GeoVector fdu = (1.0 / (2 * hu)) * (s.PointAt(new GeoPoint2D(uv.x + hu, uv.y)) - s.PointAt(new GeoPoint2D(uv.x - hu, uv.y)));
                Assert.AreEqual(0.0, (s.VDirection(uv) - fdv).Length, 1e-5 * fdv.Length, $"VDirection at {uv}");
                Assert.AreEqual(0.0, (s.UDirection(uv) - fdu).Length, 1e-5 * fdu.Length, $"UDirection at {uv}");
                s.Derivation2At(uv, out GeoPoint loc, out GeoVector du, out GeoVector dv, out GeoVector _, out GeoVector _, out GeoVector duv);
                Assert.AreEqual(0.0, loc | s.PointAt(uv), 1e-12, "location of Derivation2At");
                Assert.AreEqual(0.0, (dv - fdv).Length, 1e-5 * fdv.Length, $"dv of Derivation2At at {uv}");
                GeoVector fduv = (1.0 / (2 * hu)) * (s.VDirection(new GeoPoint2D(uv.x + hu, uv.y)) - s.VDirection(new GeoPoint2D(uv.x - hu, uv.y)));
                Assert.AreEqual(0.0, (duv - fduv).Length, 1e-5 * Math.Max(fduv.Length, 1.0), $"duv of Derivation2At at {uv}");
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void RevolutionPositionOfIsTheInverseOfPointAt(int kind)
        {
            ICurve profile = Profile(kind);
            SurfaceOfRevolution s = new SurfaceOfRevolution(profile, GeoPoint.Origin, GeoVector.ZAxis);
            foreach (GeoPoint2D uv in InnerPoints(profile))
            {
                GeoPoint p = s.PointAt(uv);
                Assert.AreEqual(0.0, s.PointAt(s.PositionOf(p)) | p, 1e-8, $"PositionOf at {uv}");
            }
        }

        [TestMethod]
        public void RevolutionOrientationDoesNotThrow()
        {   // fell through into the old implementation, which needs the 2d curve, and threw a NullReferenceException
            SurfaceOfRevolution s = new SurfaceOfRevolution(Profile(2), GeoPoint.Origin, GeoVector.ZAxis);
            double inside = s.Orientation(new GeoPoint(0, 0, 4));
            double outside = s.Orientation(new GeoPoint(40, 0, 4));
            Assert.IsFalse(double.IsNaN(inside) || double.IsNaN(outside));
        }

        [TestMethod]
        public void RevolutionIntersectWithCurvesInSpecialPlanes()
        {   // a circle in a plane perpendicular to the axis, and a circle in a plane through the axis, which threw a
            // NullReferenceException, because that special case needs the 2d profile of the old implementation
            SurfaceOfRevolution s = new SurfaceOfRevolution(Profile(2), GeoPoint.Origin, GeoVector.ZAxis);
            s.GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
            BoundingRect bounds = new BoundingRect(umin, vmin, umax, vmax);
            Ellipse perpendicular = Ellipse.Construct();
            perpendicular.SetCirclePlaneCenterRadius(new Plane(new GeoPoint(10, 0, 4), GeoVector.ZAxis), new GeoPoint(10, 0, 4), 10);
            Ellipse throughAxis = Ellipse.Construct();
            throughAxis.SetCirclePlaneCenterRadius(new Plane(new GeoPoint(20, 0, 4.5), GeoVector.XAxis, GeoVector.ZAxis), new GeoPoint(20, 0, 4.5), 9);
            foreach (ICurve curve in new ICurve[] { perpendicular, throughAxis })
            {
                s.Intersect(curve, bounds, out GeoPoint[] ips, out GeoPoint2D[] uvs, out double[] _);
                Assert.AreEqual(ips.Length, uvs.Length);
                Assert.IsTrue(ips.Length >= 2, "the circle crosses the surface");
                for (int i = 0; i < ips.Length; i++)
                {
                    Assert.AreEqual(0.0, s.PointAt(uvs[i]) | ips[i], 1e-6, "the point is on the surface");
                    Assert.AreEqual(0.0, curve.DistanceTo(ips[i]), 1e-6, "the point is on the curve");
                }
            }
        }

        private static void AssertSameWithMapping(ISurface first, ISurface second, BoundingRect bounds)
        {
            Assert.IsTrue(first.SameGeometry(bounds, second, bounds, 1e-6, out ModOp2D m), "same geometry");
            for (int i = 0; i <= 4; i++)
            {
                GeoPoint2D uv = new GeoPoint2D(bounds.Left + i * bounds.Width / 4, bounds.Bottom + (4 - i) * bounds.Height / 4);
                Assert.AreEqual(0.0, first.PointAt(uv) | second.PointAt(m * uv), 1e-6, $"mapped point {uv}");
            }
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void RevolutionSameGeometry(int kind)
        {
            ICurve profile = Profile(kind);
            SurfaceOfRevolution s = new SurfaceOfRevolution(profile, GeoPoint.Origin, GeoVector.ZAxis);
            double vs = profile.PositionToParameter(0.0), ve = profile.PositionToParameter(1.0);
            BoundingRect bounds = new BoundingRect(0.2, Math.Min(vs, ve) + 0.1 * Math.Abs(ve - vs), 2.0, Math.Max(vs, ve) - 0.1 * Math.Abs(ve - vs));
            AssertSameWithMapping(s, s.Clone(), bounds);
            // the same surface with the reversed curve and the reversed axis
            ICurve reversed = profile.Clone();
            reversed.Reverse();
            AssertSameWithMapping(s, new SurfaceOfRevolution(reversed, GeoPoint.Origin, -GeoVector.ZAxis), bounds);
            // a copy rotated about the axis: its curve is rotated as well
            AssertSameWithMapping(s, s.GetModified(ModOp.Rotate(GeoPoint.Origin, GeoVector.ZAxis, new SweepAngle(0.7))), bounds);
            // a different surface: the curve moved along the axis
            ICurve moved = profile.CloneModified(ModOp.Translate(0, 0, 0.5));
            Assert.IsFalse(s.SameGeometry(bounds, new SurfaceOfRevolution(moved, GeoPoint.Origin, GeoVector.ZAxis), bounds, 1e-6, out ModOp2D _));
        }

        [TestMethod]
        public void PathDerivativesAreTheDerivativesOfPointAt()
        {
            GeoObject.Path p = (GeoObject.Path)Profile(0);
            ICurve c = p;
            const double h = 1e-6;
            foreach (double pos in new double[] { 0.1, 0.3, 0.6, 0.9 })
            {
                Assert.IsTrue(c.TryPointDeriv2At(pos, out GeoPoint pt, out GeoVector d1, out GeoVector d2));
                Assert.AreEqual(0.0, pt | c.PointAt(pos), 1e-12);
                GeoVector fd1 = (1.0 / (2 * h)) * (c.PointAt(pos + h) - c.PointAt(pos - h));
                Assert.AreEqual(0.0, (d1 - fd1).Length, 1e-5 * fd1.Length, $"first derivative at {pos}");
                const double h2 = 1e-4;
                GeoVector fd2 = (1.0 / (h2 * h2)) * ((c.PointAt(pos + h2) - pt) + (c.PointAt(pos - h2) - pt));
                Assert.AreEqual(0.0, (d2 - fd2).Length, 1e-3 * Math.Max(fd2.Length, 1.0), $"second derivative at {pos}");
            }
        }

        [TestMethod]
        public void RuledSurfaceMixedDerivative()
        {   // two skew lines: a hyperbolic paraboloid, whose mixed derivative is not zero
            RuledSurface rs = new RuledSurface(Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0)),
                Line.TwoPoints(new GeoPoint(0, 10, 0), new GeoPoint(10, 10, 5)));
            const double h = 1e-5;
            foreach (GeoPoint2D uv in new[] { new GeoPoint2D(0.4, 0.3), new GeoPoint2D(0.8, 0.9) })
            {
                rs.Derivation2At(uv, out GeoPoint _, out GeoVector _, out GeoVector _, out GeoVector _, out GeoVector dvv, out GeoVector duv);
                GeoVector fd = (1.0 / (2 * h)) * (rs.VDirection(new GeoPoint2D(uv.x + h, uv.y)) - rs.VDirection(new GeoPoint2D(uv.x - h, uv.y)));
                Assert.AreEqual(0.0, (duv - fd).Length, 1e-6, "duv");
                Assert.AreEqual(0.0, dvv.Length, 1e-12, "the surface is linear in v");
            }
        }

        [TestMethod]
        public void ShortArcsWithASlightlyMovedEndAreTheSame()
        {   // the center of a short arc is ill-conditioned: moving one end by 5e-5 moves the center of a 3 point arc by about 100
            GeoPoint Onto(double a) => new GeoPoint(100 * Math.Cos(a), 100 * Math.Sin(a), 0);
            Ellipse a1 = Ellipse.Construct();
            a1.SetArc3Points(Onto(0), Onto(0.001), Onto(0.002), Plane.XYPlane);
            Ellipse a2 = Ellipse.Construct();
            a2.SetArc3Points(Onto(0), Onto(0.001), Onto(0.002) + new GeoVector(5e-5, 0, 0), Plane.XYPlane);
            Assert.IsTrue((a1.Center | a2.Center) > 1.0, "test setup: the centers are far apart");
            Assert.IsTrue((a1 as ICurve).SameGeometry(a2, 1e-4));
            ICurve reversed = (a2 as ICurve).Clone();
            reversed.Reverse();
            Assert.IsTrue((a1 as ICurve).SameGeometry(reversed, 1e-4), "reversed arc");
            // a quarter circle and the other quarter of the same circle are different
            Ellipse q1 = Ellipse.Construct();
            q1.SetArc3Points(Onto(0), Onto(Math.PI / 4), Onto(Math.PI / 2), Plane.XYPlane);
            Ellipse q2 = Ellipse.Construct();
            q2.SetArc3Points(Onto(Math.PI / 2), Onto(3 * Math.PI / 4), Onto(Math.PI), Plane.XYPlane);
            Assert.IsFalse((q1 as ICurve).SameGeometry(q2, 1e-4));
            // the same end points, but bulging to the other side
            Ellipse other = Ellipse.Construct();
            other.SetArc3Points(Onto(0), new GeoPoint(100 * Math.Cos(Math.PI / 4) - 20, 100 * Math.Sin(Math.PI / 4) - 20, 0), Onto(Math.PI / 2), Plane.XYPlane);
            Assert.IsFalse((q1 as ICurve).SameGeometry(other, 1e-4));
        }
    }
}
