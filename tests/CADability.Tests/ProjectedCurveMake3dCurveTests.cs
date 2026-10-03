using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="ISurface.Make3dCurve"/> takes a shortcut for a <see cref="ProjectedCurve"/> on the same (or a
    /// geometrically equal) surface and returns its 3d curve. A ProjectedCurve keeps the 3d curve it was made from,
    /// which need not lie on the surface, so the shortcut must check that it does. Only the sphere did.
    /// </summary>
    [TestClass]
    public class ProjectedCurveMake3dCurveTests
    {
        private static IEnumerable<(string what, ISurface surface, GeoVector normalAtCurve)> Surfaces()
        {
            yield return ("plane", new PlaneSurface(new Plane(new GeoPoint(1, 2, 3), new GeoVector(1, 0, 0), new GeoVector(0, 1, 0))), GeoVector.ZAxis);
            yield return ("cylinder", new CylindricalSurface(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, GeoVector.ZAxis), GeoVector.XAxis);
            yield return ("sphere", new SphericalSurface(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis), GeoVector.XAxis);
            yield return ("cone", new ConicalSurface(new GeoPoint(0, 0, -20), GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, Math.Atan(0.5)), new GeoVector(2, 0, -1).Normalized);
            yield return ("torus", new ToroidalSurface(GeoPoint.Origin, GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, 20, 5), GeoVector.XAxis);
        }

        /// <summary>
        /// A short line through the surface near a point on it, moved by <paramref name="offset"/> along the normal there.
        /// </summary>
        private static ICurve CurveNear(ISurface surface, GeoVector normal, double offset)
        {
            GeoPoint near = new GeoPoint(10, 0, 0);
            if (surface is PlaneSurface) near = new GeoPoint(1, 2, 3);
            else if (surface is ToroidalSurface) near = new GeoPoint(25, 0, 0);
            GeoPoint2D center = surface.PositionOf(near);
            GeoPoint2D sp = center + new GeoVector2D(-0.05, -0.05), ep = center + new GeoVector2D(0.05, 0.05);
            return Line.TwoPoints(surface.PointAt(sp) + offset * normal, surface.PointAt(ep) + offset * normal);
        }

        [TestMethod]
        public void the_3d_curve_of_a_projected_curve_on_the_surface_is_taken()
        {
            foreach ((string what, ISurface surface, GeoVector normal) in Surfaces())
            {
                ICurve onSurface = surface.Make3dCurve(new Line2D(surface.PositionOf(CurveNear(surface, normal, 0).StartPoint), surface.PositionOf(CurveNear(surface, normal, 0).EndPoint)));
                ProjectedCurve pc = new ProjectedCurve(onSurface, surface, true, BoundingRect.EmptyBoundingRect);
                ICurve res = surface.Make3dCurve(pc);
                Assert.IsNotNull(res, what);
                Assert.IsTrue((res.StartPoint | onSurface.StartPoint) < 1e-8 && (res.EndPoint | onSurface.EndPoint) < 1e-8, $"{what}: not the 3d curve of the projected curve");
            }
        }

        [TestMethod]
        public void a_3d_curve_off_the_surface_is_not_taken()
        {
            foreach ((string what, ISurface surface, GeoVector normal) in Surfaces())
            {
                ICurve off = CurveNear(surface, normal, 0.5);
                ProjectedCurve pc = new ProjectedCurve(off, surface, true, BoundingRect.EmptyBoundingRect);
                ICurve res = surface.Make3dCurve(pc);
                if (res == null) continue; // no 3d curve at all is acceptable, a wrong one is not
                for (int i = 0; i <= 10; i++)
                {
                    GeoPoint p = res.PointAt(i / 10.0);
                    Assert.IsTrue(surface.GetDistance(p) < 1e-6, $"{what}: Make3dCurve returned a curve {surface.GetDistance(p)} away from the surface");
                }
            }
        }
    }
}
