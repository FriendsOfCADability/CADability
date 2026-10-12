using CADability.GeoObject;
using CADability;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CADability.GeoObject
{
    /// <summary>
    /// Extensions for multiple surfaces
    /// </summary>
    public partial class Surfaces
    {
        /// <summary>
        /// Find two points on the surfaces, where the connection is perpendicular to both surfaces. Currently used by parametrics to identify faces where we can
        /// change the distance. Implemented for two parallel planes and for a plane and a cylinder whose axis is parallel to the plane.
        /// </summary>
        /// <param name="surface1">the first surface</param>
        /// <param name="domain1">the used part of the first surface</param>
        /// <param name="surface2">the second surface</param>
        /// <param name="domain2">the used part of the second surface</param>
        /// <param name="preferredPoint">when there are several solutions, the one closest to this point is returned (may be invalid)</param>
        /// <param name="uv1">the point on the first surface</param>
        /// <param name="uv2">the point on the second surface</param>
        /// <returns>true, if such a pair of points was found</returns>
        public static bool ParallelDistance(ISurface surface1, BoundingRect domain1, ISurface surface2, BoundingRect domain2, GeoPoint preferredPoint, out GeoPoint2D uv1, out GeoPoint2D uv2)
        {
            if (surface1 is PlaneSurface pls1)
            {
                if (surface2 is PlaneSurface pls2)
                {
                    if (Precision.SameDirection(pls1.Normal, pls2.Normal, false))
                    {
                        uv1 = domain1.GetCenter();
                        uv2 = pls2.PositionOf(pls1.PointAt(uv1));
                        return true;
                    }
                }
                if (surface2 is ICylinder cyl)
                {
                    if (Precision.IsPerpendicular(pls1.Normal, cyl.Axis.Direction, false))
                    {
                        GeoPoint2D axloc = pls1.PositionOf(cyl.Axis.Location);
                        GeoVector2D axdir = pls1.PositionOf(cyl.Axis.Location + cyl.Axis.Direction) - axloc;
                        GeoPoint2D p1 = axloc ;
                        GeoPoint2D p2 = axloc + axdir; // p1->p2 the cylinder axis projected onto the plane
                        ClipRect clr = new ClipRect(domain1);
                        if (clr.ClipLine(ref p1, ref p2))
                        {
                            uv1 = uv2 = GeoPoint2D.Invalid;
                            GeoPoint2D pm = new GeoPoint2D(p1, p2);
                            if (!preferredPoint.IsValid) preferredPoint = new GeoPoint(pls1.PointAt(p1), pls1.PointAt(p2));
                            GeoPoint2D[] ips = surface2.GetLineIntersection(pls1.PointAt(pm), pls1.Normal);
                            double mindist = double.MaxValue;
                            for (int i = 0; i < ips.Length; i++)
                            {
                                SurfaceHelper.AdjustPeriodic(surface2, domain2, ref ips[i]);
                                if (domain2.ContainsEps(ips[i], Precision.eps))
                                {
                                    GeoPoint p = surface2.PointAt(ips[i]);
                                    if ((preferredPoint | p) < mindist)
                                    {
                                        mindist = preferredPoint | p;
                                        uv1 = pm;
                                        uv2 = ips[i];
                                    }
                                }
                            }
                            return uv1.IsValid;
                        }
                    }
                }
            }
            else if (surface2 is PlaneSurface pls2) return ParallelDistance(surface2, domain2, surface1, domain1, preferredPoint, out uv2, out uv1);
            uv1 = uv2 = GeoPoint2D.Invalid;
            return false;
        }
    }
}

