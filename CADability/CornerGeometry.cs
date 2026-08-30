using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// Pure geometric core of the corner tools (see <see cref="CADability.Actions.CornerCurvesAction"/>), free of any
    /// interaction state so it can be unit tested. Given two curves that form a corner, a pick position and a size it
    /// computes either a tangential fillet arc or a symmetric chamfer line.
    /// </summary>
    public static class CornerGeometry
    {
        /// <summary>What to build in the corner.</summary>
        public enum Operation
        {
            Fillet, // a tangential arc of the given radius
            Chamfer // a straight bevel; the "size" is the length of the chamfer edge
        }

        /// <summary>
        /// Computes the tangential fillet arc of the given <paramref name="radius"/> in the corner where the two curves
        /// meet, near <paramref name="pick"/>. The centre is at distance radius from both curves on the inner side, found
        /// directly as the intersection of the two inner parallels; the tangent points are the perpendicular feet.
        /// </summary>
        public static bool TryComputeFillet(ICurve curve1, ICurve curve2, GeoPoint pick, double radius,
            Plane drawingPlane, out Ellipse arc, out GeoPoint cornerPoint)
        {
            bool ok = TryComputeCornerCurve(curve1, curve2, pick, radius, Operation.Fillet, drawingPlane,
                out ICurve cornerCurve, out cornerPoint);
            arc = cornerCurve as Ellipse;
            return ok;
        }

        /// <summary>
        /// Computes the symmetric chamfer whose edge has the given <paramref name="length"/> in the corner where the two
        /// curves meet, near <paramref name="pick"/>. Symmetric means the chamfer is perpendicular to the angle bisector;
        /// the bisector offset that yields the requested edge length is solved directly (closed form for lines, by
        /// bisection when a curved side is involved).
        /// </summary>
        public static bool TryComputeChamfer(ICurve curve1, ICurve curve2, GeoPoint pick, double length,
            Plane drawingPlane, out Line chamfer, out GeoPoint cornerPoint)
        {
            bool ok = TryComputeCornerCurve(curve1, curve2, pick, length, Operation.Chamfer, drawingPlane,
                out ICurve cornerCurve, out cornerPoint);
            chamfer = cornerCurve as Line;
            return ok;
        }

        /// <summary>
        /// Computes the corner curve (a fillet arc or a chamfer line) that connects the two curves in their corner near
        /// <paramref name="pick"/>. <paramref name="size"/> is the fillet radius or the chamfer edge length. The two
        /// endpoints of the returned curve are the points where the two curves have to be shortened to.
        /// </summary>
        public static bool TryComputeCornerCurve(ICurve curve1, ICurve curve2, GeoPoint pick, double size,
            Operation operation, Plane drawingPlane, out ICurve cornerCurve, out GeoPoint cornerPoint)
        {
            cornerCurve = null;
            cornerPoint = GeoPoint.Origin;
            if (!Setup(curve1, curve2, pick, size, out Plane pl, out ICurve2D c1, out ICurve2D c2,
                    out GeoPoint2D corner, out double cornerPar1, out double cornerPar2, out GeoVector2D bisector))
                return false;
            cornerPoint = pl.ToGlobal(corner);
            if (operation == Operation.Fillet)
                return TryFillet(c1, c2, corner, cornerPar1, cornerPar2, bisector, size, pl, drawingPlane, out cornerCurve);
            return TryChamfer(c1, c2, corner, cornerPar1, cornerPar2, bisector, size, pl, out cornerCurve);
        }

        /// <summary>
        /// Common preparation for both operations: the common plane, the two projected curves, the corner (the crossing
        /// closest to the pick) with its parameters, and the bisector of the inner angle (pointing into the region where
        /// the corner curve has to sit). Returns false if there is no common plane, no crossing, or the curves run
        /// collinearly (no real corner).
        /// </summary>
        private static bool Setup(ICurve curve1, ICurve curve2, GeoPoint pick, double size,
            out Plane pl, out ICurve2D c1, out ICurve2D c2, out GeoPoint2D corner,
            out double cornerPar1, out double cornerPar2, out GeoVector2D bisector)
        {
            pl = Plane.XYPlane; c1 = null; c2 = null; corner = GeoPoint2D.Origin;
            cornerPar1 = 0.0; cornerPar2 = 0.0; bisector = GeoVector2D.NullVector;
            if (curve1 == null || curve2 == null || curve1 == curve2 || size <= 0.0) return false;
            if (!Curves.GetCommonPlane(curve1, curve2, out pl)) return false;

            c1 = curve1.GetProjectedCurve(pl);
            c2 = curve2.GetProjectedCurve(pl);
            if (c1 is Path2D p1) p1.Flatten();
            if (c2 is Path2D p2) p2.Flatten();

            GeoPoint2D pick2D = pl.Project(pick);
            GeoPoint2DWithParameter[] crossings = c1.Intersect(c2);
            if (crossings.Length == 0) return false;
            corner = crossings[0].p;
            for (int i = 1; i < crossings.Length; i++)
                if (Geometry.Dist(crossings[i].p, pick2D) < Geometry.Dist(corner, pick2D)) corner = crossings[i].p;
            cornerPar1 = c1.PositionOf(corner);
            cornerPar2 = c2.PositionOf(corner);

            // the into-curve tangents at the corner span the inner angle; their normalized sum bisects it
            bisector = IntoDirection(c1, cornerPar1).Normalized + IntoDirection(c2, cornerPar2).Normalized;
            return bisector.Length >= 1e-9;
        }

        private static bool TryFillet(ICurve2D c1, ICurve2D c2, GeoPoint2D corner, double cornerPar1, double cornerPar2,
            GeoVector2D bisector, double radius, Plane pl, Plane drawingPlane, out ICurve fillet)
        {
            fillet = null;
            ICurve2D inner1 = InnerParallel(c1, radius, corner, bisector);
            ICurve2D inner2 = InnerParallel(c2, radius, corner, bisector);
            if (inner1 == null || inner2 == null) return false;

            // the centre is where the two inner parallels meet; take the valid candidate nearest the corner
            GeoPoint2D center = GeoPoint2D.Origin, tangent1 = GeoPoint2D.Origin, tangent2 = GeoPoint2D.Origin;
            double bestDist = double.MaxValue;
            foreach (GeoPoint2DWithParameter ip in inner1.Intersect(inner2))
            {
                // the tangent points are the perpendicular feet of the centre on the two curves, within the curves at radius
                if (!TangentPoint(c1, ip.p, cornerPar1, radius, out GeoPoint2D t1)) continue;
                if (!TangentPoint(c2, ip.p, cornerPar2, radius, out GeoPoint2D t2)) continue;
                if (Precision.IsEqual(t1, t2)) continue; // degenerate
                double dist = Geometry.Dist(ip.p, corner);
                if (dist < bestDist) { bestDist = dist; center = ip.p; tangent1 = t1; tangent2 = t2; }
            }
            if (bestDist == double.MaxValue) return false;

            Ellipse arc0 = Ellipse.Construct();
            Ellipse arc1 = Ellipse.Construct();
            arc0.SetArcPlaneCenterStartEndPoint(drawingPlane, center, tangent1, tangent2, pl, false);
            arc1.SetArcPlaneCenterStartEndPoint(drawingPlane, center, tangent1, tangent2, pl, true);
            // it is always the smaller of the two arcs
            fillet = Math.Abs(arc0.SweepParameter) > Math.Abs(arc1.SweepParameter) ? arc1 : arc0;
            return true;
        }

        private static bool TryChamfer(ICurve2D c1, ICurve2D c2, GeoPoint2D corner, double cornerPar1, double cornerPar2,
            GeoVector2D bisector, double length, Plane pl, out ICurve chamfer)
        {
            chamfer = null;
            GeoVector2D bisectorUnit = bisector.Normalized;
            GeoVector2D perpDir = bisectorUnit.ToLeft(); // perpendicular to the bisector

            // the chamfer is perpendicular to the bisector at distance d along it; its endpoints are the intersections
            // with the two curves. The edge length grows monotonically with d, so solve d for the requested length.
            bool ChordAt(double d, out GeoPoint2D p1, out GeoPoint2D p2, out double chord)
            {
                p1 = GeoPoint2D.Origin; p2 = GeoPoint2D.Origin; chord = 0.0;
                GeoPoint2D m = corner + d * bisectorUnit;
                Line2D perp = new Line2D(m - perpDir, m + perpDir);
                if (!NearestWithin(c1, perp, m, cornerPar1, out p1)) return false;
                if (!NearestWithin(c2, perp, m, cornerPar2, out p2)) return false;
                chord = Geometry.Dist(p1, p2);
                return true;
            }

            // closed-form start value from the tangent directions (exact for two straight lines): chord = 2 d tan(halfAngle)
            double cosHalf = Math.Max(-1.0, Math.Min(1.0, IntoDirection(c1, cornerPar1).Normalized * bisectorUnit));
            double tanHalf = Math.Tan(Math.Acos(cosHalf));
            double hi = tanHalf > 1e-9 ? length / (2.0 * tanHalf) : length;
            if (hi <= 0.0) hi = length;
            double lo = 0.0;
            // make hi an upper bound: grow while the chamfer still fits and is shorter than requested
            for (int g = 0; g < 40; g++)
            {
                if (ChordAt(hi, out _, out _, out double ch) && ch < length) { lo = hi; hi *= 2.0; }
                else break;
            }

            GeoPoint2D bestP1 = GeoPoint2D.Origin, bestP2 = GeoPoint2D.Origin;
            bool found = false;
            for (int it = 0; it < 60; it++)
            {
                double mid = 0.5 * (lo + hi);
                if (ChordAt(mid, out GeoPoint2D p1, out GeoPoint2D p2, out double ch))
                {
                    bestP1 = p1; bestP2 = p2; found = true;
                    if (Math.Abs(ch - length) < length * 1e-8) break;
                    if (ch < length) lo = mid; else hi = mid;
                }
                else hi = mid; // the perpendicular no longer meets both curves: too far, shrink
            }
            if (!found || Precision.IsEqual(bestP1, bestP2)) return false;

            Line line = Line.Construct();
            line.SetTwoPoints(pl.ToGlobal(bestP1), pl.ToGlobal(bestP2));
            chamfer = line;
            return true;
        }

        // the intersection of 'curve' with 'line' that is within the curve and closest to 'near'
        private static bool NearestWithin(ICurve2D curve, ICurve2D line, GeoPoint2D near, double cornerPar, out GeoPoint2D point)
        {
            point = GeoPoint2D.Origin;
            double best = double.MaxValue;
            bool found = false;
            foreach (GeoPoint2DWithParameter ip in curve.Intersect(line))
            {
                if (!WithinCurve(ip.par1, cornerPar)) continue; // par1 is the parameter on 'curve'
                double d = Geometry.Dist(ip.p, near);
                if (d < best) { best = d; point = ip.p; found = true; }
            }
            return found;
        }

        /// <summary>
        /// The tangent of <paramref name="curve"/> at the corner, pointing away from the corner into the curve's body.
        /// The corner is (near) an end of the curve, so the direction is flipped when the corner is at the curve's end.
        /// </summary>
        private static GeoVector2D IntoDirection(ICurve2D curve, double cornerPar)
        {
            GeoVector2D tangent = curve.DirectionAt(cornerPar);
            return Math.Abs(cornerPar - 1.0) < Math.Abs(cornerPar - 0.0) ? -tangent : tangent;
        }

        /// <summary>
        /// The parallel of <paramref name="curve"/> at distance <paramref name="radius"/> offset toward the inner side of
        /// the corner (the side the <paramref name="bisector"/> points to).
        /// </summary>
        private static ICurve2D InnerParallel(ICurve2D curve, double radius, GeoPoint2D corner, GeoVector2D bisector)
        {
            ICurve2D positive = curve.Parallel(radius, false, 0.0, 0.0);
            ICurve2D negative = curve.Parallel(-radius, false, 0.0, 0.0);
            if (positive == null) return negative;
            if (negative == null) return positive;
            // the perpendicular foot of the corner on the parallel gives the true offset direction
            GeoPoint2D[] feet = positive.PerpendicularFoot(corner);
            if (feet.Length == 0) return positive;
            GeoVector2D toPositive = feet[0] - corner;
            return toPositive * bisector > 0.0 ? positive : negative;
        }

        /// <summary>
        /// The tangent point where a fillet centred at <paramref name="center"/> touches <paramref name="curve"/>: the
        /// perpendicular foot at distance <paramref name="radius"/> that lies within the curve. Returns false if there is
        /// no such foot (the fillet does not fit).
        /// </summary>
        private static bool TangentPoint(ICurve2D curve, GeoPoint2D center, double cornerPar, double radius, out GeoPoint2D tangent)
        {
            tangent = GeoPoint2D.Origin;
            double bestError = radius * 1e-3; // the foot must sit at the radius; a farther foot is a different branch
            bool found = false;
            foreach (GeoPoint2D foot in curve.PerpendicularFoot(center))
            {
                if (!WithinCurve(curve.PositionOf(foot), cornerPar)) continue;
                double error = Math.Abs(Geometry.Dist(foot, center) - radius);
                if (error < bestError) { bestError = error; tangent = foot; found = true; }
            }
            return found;
        }

        // true if 'par' lies within the curve; the range is extended toward the corner when the corner itself is a
        // virtual intersection beyond the curve's end
        private static bool WithinCurve(double par, double cornerPar)
        {
            return par > Math.Min(0.0, cornerPar) && par < Math.Max(1.0, cornerPar);
        }

        /// <summary>
        /// Applies <paramref name="operation"/> to every corner of the ordered <paramref name="segments"/> (which meet
        /// end-to-start) with the given <paramref name="size"/> (fillet radius or chamfer edge length), and returns the
        /// resulting parts (shortened segments and corner curves) to be joined into a path. For a <paramref name="closed"/>
        /// outline the seam between the last and first segment is treated as well. Each segment is shortened by the corner
        /// curves of its two neighbouring corners; corners where the corner curve does not fit are left unchanged. Returns
        /// null if no corner could be processed.
        /// </summary>
        public static List<ICurve> AllCorners(IReadOnlyList<ICurve> segments, bool closed, double size,
            Operation operation, Plane drawingPlane)
        {
            int n = segments.Count;
            if (n < 2 || size <= 0.0) return null;
            int cornerCount = closed ? n : n - 1;

            // the corner curve at corner i sits between segments[i] and segments[(i+1) % n]; null where it does not fit
            ICurve[] cornerCurves = new ICurve[cornerCount];
            bool any = false;
            for (int i = 0; i < cornerCount; i++)
            {
                ICurve segA = segments[i];
                ICurve segB = segments[(i + 1) % n];
                if (TryComputeCornerCurve(segA, segB, segA.EndPoint, size, operation, drawingPlane, out ICurve cc, out _))
                {
                    cornerCurves[i] = cc;
                    any = true;
                }
            }
            if (!any) return null;

            List<ICurve> result = new List<ICurve>();
            // shorten each segment by the corner curves of the corners before and after it
            for (int i = 0; i < n; i++)
            {
                ICurve prev = (i >= 1) ? cornerCurves[i - 1] : (closed ? cornerCurves[cornerCount - 1] : null);
                ICurve next = (i < cornerCount) ? cornerCurves[i] : null;
                ICurve seg = segments[i].Clone();
                double tStart = prev != null ? seg.PositionOf(TangentOn(prev, seg)) : 0.0;
                double tEnd = next != null ? seg.PositionOf(TangentOn(next, seg)) : 1.0;
                if (tStart < tEnd - 1e-8) // the segment is not fully consumed by its two corner curves
                {
                    seg.Trim(tStart, tEnd);
                    result.Add(seg);
                }
            }
            foreach (ICurve cc in cornerCurves) if (cc != null) result.Add(cc);
            return result;
        }

        // the endpoint of the corner curve that lies on the given segment (the point the segment is shortened to)
        private static GeoPoint TangentOn(ICurve cornerCurve, ICurve segment)
        {
            return DistanceToCurve(cornerCurve.StartPoint, segment) <= DistanceToCurve(cornerCurve.EndPoint, segment)
                ? cornerCurve.StartPoint : cornerCurve.EndPoint;
        }

        private static double DistanceToCurve(GeoPoint p, ICurve curve)
        {
            double pos = curve.PositionOf(p);
            if (pos < 0.0) pos = 0.0;
            if (pos > 1.0) pos = 1.0;
            return p | curve.PointAt(pos);
        }
    }
}
