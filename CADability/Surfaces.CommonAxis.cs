using CADability.Curve2D;
using System;
using System.Collections.Generic;

namespace CADability.GeoObject
{
    public partial class Surfaces
    {
        /// <summary>
        /// Intersects two surfaces which are rotationally symmetric about the same axis. Their intersection can
        /// only consist of circles around that axis, and those circles are found in a single plane: the section
        /// through the axis gives one or two meridian curves per surface, and every intersection point of these
        /// 2d curves marks one circle.
        /// <para>
        /// All surfaces implementing <see cref="ISurfaceOfRevolution"/> qualify, and so does a sphere whose center
        /// lies on the axis of the other one - a sphere is a surface of revolution about every axis through its
        /// center, it just cannot name one. Two spheres are not handled here, their intersection is a circle
        /// around the line through the two centers and <see cref="SphericalSurface"/> solves it directly.
        /// </para>
        /// <para>
        /// The result is not trimmed to <paramref name="bounds1"/> and <paramref name="bounds2"/>, it only covers
        /// the common part; the caller trims to the face boundaries anyway. Returns null when the two surfaces do
        /// not share an axis or when no meridian section could be made - then the general intersection has to do
        /// the work. An empty array means: they share an axis but do not meet.
        /// </para>
        /// </summary>
        public static IDualSurfaceCurve[] IntersectOnCommonAxis(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2)
        {
            if (!GetCommonAxis(surface1, surface2, out Axis axis)) return null;
            GeoVector dir = axis.Direction.Normalized;
            // The meridian plane goes through the axis and through a point of the used area of surface1, so that the
            // section really meets the part of the surface that is in use. In this plane x is the axial and y the
            // radial direction, which makes an intersection point (x,y) the circle of radius |y| at the axial
            // position x.
            GeoVector radial = surface1.PointAt(bounds1.GetCenter()) - axis.Location;
            radial = radial - (radial * dir) * dir;
            if (radial.IsNullVector()) dir.ArbitraryNormals(out radial, out GeoVector _); // that point is on the axis
            Plane meridian = new Plane(axis.Location, dir, radial);

            List<ICurve2D> section1 = MeridianSection(surface1, bounds1, meridian);
            List<ICurve2D> section2 = MeridianSection(surface2, bounds2, meridian);
            if (section1.Count == 0 || section2.Count == 0) return null; // no usable section, leave it to the caller

            List<GeoPoint2D> found = new List<GeoPoint2D>();
            for (int i = 0; i < section1.Count; i++)
            {
                for (int j = 0; j < section2.Count; j++)
                {
                    GeoPoint2DWithParameter[] ips = section1[i].Intersect(section2[j]);
                    if (ips == null) continue;
                    for (int k = 0; k < ips.Length; k++) AddCirclePosition(found, ips[k].p);
                }
            }

            List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
            foreach (GeoPoint2D ip in found)
            {
                GeoPoint center = meridian.ToGlobal(new GeoPoint2D(ip.x, 0.0));
                Ellipse circle = Ellipse.Construct();
                circle.SetCirclePlaneCenterRadius(new Plane(center, dir), center, Math.Abs(ip.y));
                AddCircle(res, circle, surface1, surface2);
            }
            return res.ToArray();
        }

        /// <summary>
        /// The axis both surfaces are rotationally symmetric about, if there is one: two surfaces of revolution (cylinder,
        /// cone, torus, surface of revolution) on the same axis, or one of them and a sphere centered on its axis. This is
        /// e.g. how the inner and the outer wall of a tube or a bend are recognized as belonging together.
        /// </summary>
        /// <param name="surface1">first surface</param>
        /// <param name="surface2">second surface</param>
        /// <param name="axis">the common axis, if there is one</param>
        /// <returns>true, if both surfaces are rotationally symmetric about the same axis</returns>
        public static bool GetCommonAxis(ISurface surface1, ISurface surface2, out Axis axis)
        {
            axis = new Axis(GeoPoint.Origin, GeoVector.ZAxis);
            // an ellipsoid is not a sphere: it has no rotational symmetry about an arbitrary axis
            if (surface1 is SphericalSurface s1 && !s1.IsRealSphere) return false;
            if (surface2 is SphericalSurface s2 && !s2.IsRealSphere) return false;
            ISurfaceOfRevolution rev1 = surface1 as ISurfaceOfRevolution;
            ISurfaceOfRevolution rev2 = surface2 as ISurfaceOfRevolution;
            if (rev1 != null && rev2 != null)
            {
                if (!Precision.SameAxis(rev1.Axis, rev2.Axis)) return false;
                axis = rev1.Axis;
                return true;
            }
            // a sphere has no axis of its own, it uses the one of the other surface - provided its center is on it
            if (rev1 != null && surface2 is SphericalSurface sphere2)
            {
                if (rev1.Axis.Distance(sphere2.Location) > Precision.eps) return false;
                axis = rev1.Axis;
                return true;
            }
            if (rev2 != null && surface1 is SphericalSurface sphere1)
            {
                if (rev2.Axis.Distance(sphere1.Location) > Precision.eps) return false;
                axis = rev2.Axis;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The meridian curves of a surface in the section plane, as 2d curves of that plane. The full u range is
        /// used, not the one of <paramref name="bounds"/>: the meridian of the other surface may well sit outside
        /// the u range of this one and still intersect it. The v range is kept, it is what limits the extent of
        /// the surface, a truncated cone for instance.
        /// </summary>
        private static List<ICurve2D> MeridianSection(ISurface surface, BoundingRect bounds, Plane meridian)
        {
            List<ICurve2D> res = new List<ICurve2D>();
            surface.GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
            if (double.IsInfinity(umin) || double.IsInfinity(umax) || umin >= umax) { umin = 0.0; umax = 2.0 * Math.PI; }
            IDualSurfaceCurve[] dsc = surface.GetPlaneIntersection(new PlaneSurface(meridian), umin, umax, bounds.Bottom, bounds.Top, 0.0);
            if (dsc == null) return res;
            for (int i = 0; i < dsc.Length; i++)
            {
                if (dsc[i].Curve3D == null) continue;
                ICurve2D c2d = dsc[i].Curve3D.GetProjectedCurve(meridian);
                if (c2d != null) res.Add(c2d);
            }
            return res;
        }

        /// <summary>
        /// Collects the position of a circle, given by an intersection point in the meridian plane. The two
        /// meridians of a surface are mirrored at the axis, so (x,y) and (x,-y) describe the same circle and must
        /// not be used twice; a tangential contact yields two almost identical points for the same reason.
        /// </summary>
        private static void AddCirclePosition(List<GeoPoint2D> found, GeoPoint2D ip)
        {
            GeoPoint2D position = new GeoPoint2D(ip.x, Math.Abs(ip.y));
            if (position.y < Precision.eps) return; // the circle degenerates to a point on the axis
            for (int i = 0; i < found.Count; i++)
            {
                if ((found[i] | position) < Precision.eps) return;
            }
            found.Add(position);
        }

        /// <summary>
        /// Adds a circle around the common axis as a dual surface curve. When one of the two 2d curves would come
        /// out closed - which happens on a sphere whose own axis is tilted against the circle, where the 2d curve
        /// is a ProjectedCurve, and such a curve cannot be closed - the circle is split into two halves instead.
        /// </summary>
        private static void AddCircle(List<IDualSurfaceCurve> res, Ellipse circle, ISurface surface1, ISurface surface2)
        {
            ICurve2D c2d1 = surface1.GetProjectedCurve(circle, 0.0);
            ICurve2D c2d2 = surface2.GetProjectedCurve(circle, 0.0);
            if (c2d1 != null && c2d2 != null && !IsClosed(c2d1) && !IsClosed(c2d2))
            {
                res.Add(new DualSurfaceCurve(circle, surface1, c2d1, surface2, c2d2));
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                Ellipse half = circle.Clone() as Ellipse;
                half.Trim(i * 0.5, (i + 1) * 0.5);
                ICurve2D h1 = surface1.GetProjectedCurve(half, 0.0);
                ICurve2D h2 = surface2.GetProjectedCurve(half, 0.0);
                if (h1 == null || h2 == null) return;
                res.Add(new DualSurfaceCurve(half, surface1, h1, surface2, h2));
            }
        }

        private static bool IsClosed(ICurve2D curve) => (curve.StartPoint | curve.EndPoint) < Precision.eps;
    }
}
