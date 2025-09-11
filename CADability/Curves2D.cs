using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace CADability.Curve2D
{
    /// <summary>
    /// This class provides some static Methods concerning the interaction of two ICurve2D
    /// objects.
    /// </summary>

    public class Curves2D
    {
        /// <summary>
        /// Calculates lines that are tangential to both the first and the second curve. Returns
        /// an array of 2D points, where each pair of points represent a line. The number of
        /// points is always even. Implemented for circles or arcs only.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static GeoPoint2D[] TangentLines(ICurve2D first, ICurve2D second)
        {
            if (first is Circle2D c1 && second is Circle2D c2)
            {   // Circle2D also includes Arc2D
                return Geometry.TangentCC(c1.Center, c1.Radius, c2.Center, c2.Radius);
            }
            if (first is GeneralCurve2D g1 && second is GeneralCurve2D g2)
            {   // all ICurve2D curves in CADability are derived from GeneralCurve2D. We use the triangulation.
                // basisPoints are the points on the curve. vertices are outside the curve and vertices[i] is the apex of the triangle with the basis basisPoints[i], basisPoint[i+1].
                // When the connection of the two triangle apexes doesn't cross the base line of the triangle, then there could be a tangent to both curves close to that line
                g1.GetTriangulationPoints(out GeoPoint2D[] basisPoints1, out GeoPoint2D[] vertices1, out GeoVector2D[] directions1, out double[] parameters1);
                g2.GetTriangulationPoints(out GeoPoint2D[] basisPoints2, out GeoPoint2D[] vertices2, out GeoVector2D[] directions2, out double[] parameters2);
                for (int i = 0; i < vertices1.Length; i++)
                {
                    for (int j = 0; j < vertices2.Length; j++)
                    {
                        if (Geometry.OnSameSide(basisPoints1[i], basisPoints1[i + 1], vertices1[i], vertices2[j]) &&
                            Geometry.OnSameSide(basisPoints2[j], basisPoints2[j + 1], vertices1[i], vertices2[j]))
                        {
                            bool outerTangent = Geometry.OnSameSide(basisPoints1[i], basisPoints2[j], vertices1[i], vertices2[j]);
                            // with two circles the outer tangent lines don't cross, the inner tangent lines do cross
                            double par1, par2; // we need good start positions for the parameters.
                            double fi = directions1[i] * (vertices1[i] - vertices2[j]).ToLeft();
                            double fi1 = directions1[i + 1] * (vertices1[i] - vertices2[j]).ToLeft();
                            double fj = directions2[j] * (vertices1[i] - vertices2[j]).ToLeft();
                            double fj1 = directions2[j + 1] * (vertices1[i] - vertices2[j]).ToLeft();
                            double par1m = (1 - fi / (fi - fi1)) * parameters1[i] + (1 - fi1 / (fi1 - fi)) * parameters1[i + 1];
                            double par2m = (1 - fj / (fj - fj1)) * parameters2[j] + (1 - fj1 / (fj1 - fj)) * parameters2[j + 1];
                            for (int k = 0; k < 4; k++)
                            {
                                switch (k) // it is difficult to find good start values for the parameters, here we try different guesses
                                {
                                    default:
                                    case 0: par1 = par1m; par2 = par2m; break;
                                    case 1: par1 = parameters1[i]; par2 = parameters2[j]; break;
                                    case 2: par1 = parameters1[i + 1]; par2 = parameters2[j]; break;
                                    case 3: par1 = parameters1[i]; par2 = parameters2[j + 1]; break;
                                    case 4: par1 = parameters1[i + 1]; par2 = parameters2[j + 1]; break;
                                }
                                double mindist = Math.Abs(Geometry.DistPL(first.PointAt(par1), vertices1[i], vertices2[j])) + Math.Abs(Geometry.DistPL(second.PointAt(par2), vertices1[i], vertices2[j]));
                                while (mindist > Precision.eps)
                                {
                                    if (first.TryPointDeriv2At(par1, out GeoPoint2D loc1, out GeoVector2D deriv11, out GeoVector2D deriv12) &&
                                        second.TryPointDeriv2At(par2, out GeoPoint2D loc2, out GeoVector2D deriv21, out GeoVector2D deriv22))
                                    {
                                        double d = (deriv11.x * deriv12.y - deriv12.x * deriv11.y);
                                        double s = (deriv11.x * deriv11.x + deriv11.y * deriv11.y);
                                        GeoPoint2D cnt1 = new GeoPoint2D(loc1.x - deriv11.y * s / d, loc1.y + deriv11.x * s / d);
                                        double r1 = cnt1 | loc1;
                                        d = (deriv21.x * deriv22.y - deriv22.x * deriv21.y);
                                        s = (deriv21.x * deriv21.x + deriv21.y * deriv21.y);
                                        GeoPoint2D cnt2 = new GeoPoint2D(loc2.x - deriv21.y * s / d, loc2.y + deriv21.x * s / d);
                                        double r2 = cnt2 | loc2;
                                        GeoPoint2D[] t = Geometry.TangentCC(cnt1, r1, cnt2, r2);
                                        // the first 4 points are the two outer tangent lines, which are maybe followed by another 4 points of the crossing inner tangent lines
                                        // each pair of points is in the order of the circles
                                        if (outerTangent && t.Length >= 4)
                                        {
                                            if ((t[0] | loc1) + (t[1] | loc2) < (t[2] | loc1) + (t[3] | loc2))
                                            {
                                                par1 = first.PositionOf(t[0]);
                                                par2 = second.PositionOf(t[1]);
                                                double dd = (first.PointAt(par1) | t[0]) + (second.PointAt(par2) | t[1]);
                                                if (dd < mindist) mindist = dd;
                                                else break;
                                            }
                                            else
                                            {
                                                par1 = first.PositionOf(t[2]);
                                                par2 = second.PositionOf(t[3]);
                                                double dd = (first.PointAt(par1) | t[2]) + (second.PointAt(par2) | t[3]);
                                                if (dd < mindist) mindist = dd;
                                                else break;
                                            }
                                        }
                                        else if (!outerTangent && t.Length == 8)
                                        {
	                                        // Use the inner (crossing) tangent lines from the circle solution.
	                                        // t[4]-t[7] are the tangency points for the two crossing tangents.
	                                        // Determine which pair is closer to the current curve points.
	                                        double error1 = (t[4] | loc1) + (t[5] | loc2);
	                                        double error2 = (t[6] | loc1) + (t[7] | loc2);
	                                        if (error1 < error2)
	                                        {
		                                        // First inner tangent is closer
		                                        par1 = first.PositionOf(t[4]);
		                                        par2 = second.PositionOf(t[5]);
	                                        }
	                                        else
	                                        {
		                                        // Second inner tangent is closer
		                                        par1 = first.PositionOf(t[6]);
		                                        par2 = second.PositionOf(t[7]);
	                                        }
	                                        // Compute distance error for this choice
	                                        double dd = (first.PointAt(par1) | t[(error1 < error2 ? 4 : 6)])
	                                                    + (second.PointAt(par2) | t[(error1 < error2 ? 5 : 7)]);
	                                        if (dd < mindist)
	                                        {
		                                        mindist = dd;
		                                        // Continue refining in the while loop (next iteration will recompute new osculating circles)
	                                        }
	                                        else
	                                        {
		                                        break; // if no improvement, break out of refinement loop
	                                        }
                                        }
										else break; // no solution
                                    }
                                    else break;
                                }
                                if (mindist <= Precision.eps)
                                {
                                    return new GeoPoint2D[] { first.PointAt(par1), second.PointAt(par2) };
                                }
                            }
                        }
                    }
                }
            }
            return Array.Empty<GeoPoint2D>();
        }
        /// <summary>
        /// Calculates the start- and endpoints of lines that are tangential to the given
        /// curves. There may be any number of solutions, including
        /// no solution. Each solution consists of two points: 1.: the startpoint, 2.: the endpoint
        /// of the Line. the startpoint lies on the first curve, the endpoint on the second. The length of
        /// the returned array is a multiple of 2 (or 0).
        /// If both curves are circles or arcs, all possible solutions are returned. If a curve is
        /// mor complex than a circle (e.g. bspline, ellipse) only the solution closest to the
        /// points p1 and p2 is returned.
        /// </summary>
        /// <param name="first">first curve</param>
        /// <param name="second">second curve</param>
        /// <param name="p1">point near first curve</param>
        /// <param name="p2">point near second curve</param>
        /// <returns></returns>
        public static GeoPoint2D[] TangentLines(ICurve2D first, ICurve2D second, GeoPoint2D p1, GeoPoint2D p2)
        {
            if (first is Circle2D && second is Circle2D)
            {
                return TangentLines(first, second);
            }
            throw new NotImplementedException();
        }
        /// <summary>
        /// Calculates the center points and the tangential points of the circles that are tangential to the given
        /// curves and have the given radius. There may be any number of solutions, including
        /// no solution. Each solution consists of three points: 1.: the center, 2.: the tangential
        /// point to the first curve, 3.: the tangential point to the second curve. The length of
        /// the returned array is a multiple of 3 (or 0)
        /// </summary>
        /// <param name="first">first curve</param>
        /// <param name="second">second curve</param>
        /// <param name="radius">radius of the requsted circle</param>
        /// <returns>the requested center points</returns>
        public static GeoPoint2D[] TangentCircle(ICurve2D first, ICurve2D second, double radius)
        {
            ArrayList res = new ArrayList();
            for (int side = 0; side < 4; ++side)
            {	// welche Seite für die Parallele, 4 Kombinationsmöglichkeiten
                double r1, r2;
                if ((side & 0x1) == 0) r1 = radius;
                else r1 = -radius;
                if ((side & 0x2) == 0) r2 = radius;
                else r2 = -radius;
                ICurve2D par1 = first.Parallel(r1, false, 0.0, 0.0);
                ICurve2D par2 = second.Parallel(r2, false, 0.0, 0.0);
                if (par1 != null && par2 != null)
                {
                    GeoPoint2DWithParameter[] ppar = par1.Intersect(par2);
                    for (int i = 0; i < ppar.Length; ++i)
                    {
                        GeoPoint2D center = ppar[i].p;
                        bool tp1ok = false;
                        bool tp2ok = false;
                        GeoPoint2D tp1 = new GeoPoint2D(0.0, 0.0);
                        GeoPoint2D tp2 = new GeoPoint2D(0.0, 0.0);
                        GeoPoint2D[] t1 = first.PerpendicularFoot(center);
                        for (int j = 0; j < t1.Length; ++j)
                        {
                            if (Math.Abs(Geometry.Dist(t1[j], center) - radius) < Precision.eps)
                            {
                                tp1 = t1[j];
                                tp1ok = true;
                                break;
                            }
                        }
                        GeoPoint2D[] t2 = second.PerpendicularFoot(center);
                        for (int j = 0; j < t2.Length; ++j)
                        {
                            if (Math.Abs(Geometry.Dist(t2[j], center) - radius) < Precision.eps)
                            {
                                tp2 = t2[j];
                                tp2ok = true;
                                break;
                            }
                        }
                        if (tp1ok && tp2ok)
                        {
                            res.Add(center);
                            res.Add(tp1);
                            res.Add(tp2);
                        }
                    }
                }
            }
            return (GeoPoint2D[])res.ToArray(typeof(GeoPoint2D));
        }

        /// <summary>
        /// Calculates the center points and the tangential point of the circles that are tangential to a given
        /// curve with a given circlepoint and have the given radius. There may be any number of solutions, including
        /// no solution. Each solution consists of two points: 1.: the center, 2.: the tangential
        /// point to the  curve. The length of
        /// the returned array is a multiple of 2 (or 0)
        /// </summary>
        /// <param name="curve">curve</param>
        /// <param name="point">point on the circle</param>
        /// <param name="radius">radius of the requsted circle</param>
        /// <returns>the requested center points</returns>
        public static GeoPoint2D[] TangentCircle(ICurve2D curve, GeoPoint2D point, double radius)
        {
            ArrayList res = new ArrayList();
            for (int side = 0; side < 2; ++side)
            {	// welche Seite für die Parallele, 4 Kombinationsmöglichkeiten
                double r1;
                if ((side & 0x1) == 0) r1 = radius;
                else r1 = -radius;
                ICurve2D par1 = curve.Parallel(r1, false, 0.0, 0.0);
                ICurve2D par2 = new Circle2D(point, radius);
                if (par1 != null && par2 != null)
                {
                    GeoPoint2DWithParameter[] ppar = par1.Intersect(par2);
                    for (int i = 0; i < ppar.Length; ++i)
                    {
                        GeoPoint2D center = ppar[i].p;
                        bool tp1ok = false;
                        GeoPoint2D tp1 = new GeoPoint2D(0.0, 0.0);
                        GeoPoint2D tp2 = new GeoPoint2D(0.0, 0.0);
                        GeoPoint2D[] t1 = curve.PerpendicularFoot(center);
                        for (int j = 0; j < t1.Length; ++j)
                        {
                            if (Math.Abs(Geometry.Dist(t1[j], center) - radius) < Precision.eps)
                            {
                                tp1 = t1[j];
                                tp1ok = true;
                                break;
                            }
                        }
                        if (tp1ok)
                        {
                            res.Add(center);
                            res.Add(tp1);
                        }
                    }
                }
            }
            return (GeoPoint2D[])res.ToArray(typeof(GeoPoint2D));
        }
        /// <summary>
        /// Calculates the center points and the tangential points of the circles that are tangential to the given
        /// curves. If the curves are lines or circles all possible solutions are revealed. If the curves
        /// are more complex, only the solution that is closest to the given points is revealed.
        /// Each solution consists of 4 points: 1.: the center, 2.: the tangential point to c1, 
        /// 3.: the tangential point to c2, 4.: the tangential point to c3. The radius mus be calculated 
        /// by the distance of the center to one of the tangential points. The length of
        /// the returned array is a multiple of 4 (or 0)
        /// </summary>
        /// <param name="c1">first tangential curve</param>
        /// <param name="c2">second tangential curve</param>
        /// <param name="c3">third tangential curve</param>
        /// <param name="p1">startpoint on c1</param>
        /// <param name="p2">startpoint on c2</param>
        /// <param name="p3">startpoint on c3</param>
        /// <returns>Quadruples of GeoPoint2D defining 0 or more circles</returns>
        public static GeoPoint2D[] TangentCircle(ICurve2D c1, ICurve2D c2, ICurve2D c3, GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3)
        {

            //List<ICurve2D> sortedCurves = new List<ICurve2D>();
            //if (c1 is Line2D) sortedCurves.Insert(0, c1);
            //else if (c1 is Circle2D) sortedCurves.Add(c1); // Arc2D ist abgeleitet von Circle2D
            //if (c2 is Line2D) sortedCurves.Insert(0, c2);
            //else if (c2 is Circle2D) sortedCurves.Add(c2); // Arc2D ist abgeleitet von Circle2D
            //if (c3 is Line2D) sortedCurves.Insert(0, c3);
            //else if (c3 is Circle2D) sortedCurves.Add(c3); // Arc2D ist abgeleitet von Circle2D
            //if (sortedCurves.Count == 3)
            //{
            //    if (sortedCurves[2] is Line2D) return TangentCircleLLL(sortedCurves[0] as Line2D, sortedCurves[1] as Line2D, sortedCurves[2] as Line2D);
            //    else if (sortedCurves[1] is Line2D) return TangentCircleLLC(sortedCurves[0] as Line2D, sortedCurves[1] as Line2D, sortedCurves[2] as Circle2D);
            //    else if (sortedCurves[0] is Line2D) return TangentCircleLCC(sortedCurves[0] as Line2D, sortedCurves[1] as Circle2D, sortedCurves[2] as Circle2D);
            //    else return TangentCircleCCC(sortedCurves[0] as Circle2D, sortedCurves[1] as Circle2D, sortedCurves[2] as Circle2D);
            //}

            int so = 0;
            if (c1 is Circle2D) so = so + 1;
            else if (!(c1 is Line2D)) return null;
            if (c2 is Circle2D) so = so + 2;
            else if (!(c2 is Line2D)) return null;
            if (c3 is Circle2D) so = so + 4;
            else if (!(c3 is Line2D)) return null;
            GeoPoint2D[] points;
            switch (so)
            {
                case 0:
                    return TangentCircleLLL(c1 as Line2D, c2 as Line2D, c3 as Line2D);
                case 1:
                    points = TangentCircleLLC(c2 as Line2D, c3 as Line2D, c1 as Circle2D);
                    Exchange(points, 2, 0);
                    Exchange(points, 2, 1);
                    return points;
                case 2:
                    points = TangentCircleLLC(c1 as Line2D, c3 as Line2D, c2 as Circle2D);
                    Exchange(points, 2, 1);
                    return points;
                case 3:
                    points = TangentCircleLCC(c3 as Line2D, c1 as Circle2D, c2 as Circle2D);
                    Exchange(points, 0, 2);
                    Exchange(points, 0, 1);
                    return points;
                case 4:
                    return TangentCircleLLC(c1 as Line2D, c2 as Line2D, c3 as Circle2D);
                case 5:
                    points = TangentCircleLCC(c2 as Line2D, c1 as Circle2D, c3 as Circle2D);
                    Exchange(points, 1, 0);
                    return points;
                case 6:
                    return TangentCircleLCC(c1 as Line2D, c2 as Circle2D, c3 as Circle2D);
                case 7:
                    return TangentCircleCCC(c1 as Circle2D, c2 as Circle2D, c3 as Circle2D);
            }


            ArrayList res = new ArrayList();
            return (GeoPoint2D[])res.ToArray(typeof(GeoPoint2D));
        }

        private static void Exchange(GeoPoint2D[] points, int before, int after)
        {
            for (int i = 0; i < points.Length; i = i + 4)
            {
                GeoPoint2D tmp = points[before + 1 + i];
                points[before + 1 + i] = points[after + 1 + i];
                points[after + 1 + i] = tmp;
            }

        }


        private static GeoPoint2D[] TangentCircleCCC(Circle2D c1, Circle2D c2, Circle2D c3)
        {
            GeoPoint2D[] centers;
            double[] radii;
            Circle3C(c1.Center, c1.Radius, c2.Center, c2.Radius, c3.Center, c3.Radius, out centers, out radii);
            GeoPoint2D[] res = new GeoPoint2D[radii.Length * 4];
            for (int i = 0; i < radii.Length; ++i)
            {
                res[4 * i] = centers[i];
                GeoPoint2D[] pf = c1.PerpendicularFoot(centers[i]);
                if (pf.Length == 2)
                {
                    if (Math.Abs((pf[0] | centers[i]) - radii[i]) < Math.Abs((pf[1] | centers[i]) - radii[i]))
                    {
                        res[4 * i + 1] = pf[0];
                    }
                    else
                    {
                        res[4 * i + 1] = pf[1];
                    }
                }
                else
                    if (pf.Length == 1)
                    res[4 * i + 1] = pf[0];
                else res[4 * i + 1] = new GeoPoint2D(c1.Center, c1.Radius, (Angle)0);
                pf = c2.PerpendicularFoot(centers[i]);
                if (pf.Length == 2)
                {
                    if (Math.Abs((pf[0] | centers[i]) - radii[i]) < Math.Abs((pf[1] | centers[i]) - radii[i]))
                    {
                        res[4 * i + 2] = pf[0];
                    }
                    else
                    {
                        res[4 * i + 2] = pf[1];
                    }
                }
                else
                    if (pf.Length == 1)
                    res[4 * i + 2] = pf[0];
                else res[4 * i + 2] = new GeoPoint2D(c2.Center, c2.Radius, (Angle)0);
                pf = c3.PerpendicularFoot(centers[i]);
                if (pf.Length == 2)
                {
                    if (Math.Abs((pf[0] | centers[i]) - radii[i]) < Math.Abs((pf[1] | centers[i]) - radii[i]))
                    {
                        res[4 * i + 3] = pf[0];
                    }
                    else
                    {
                        res[4 * i + 3] = pf[1];
                    }
                }
                else
                    if (pf.Length == 1)
                    res[4 * i + 3] = pf[0];
                else res[4 * i + 3] = new GeoPoint2D(c3.Center, c3.Radius, (Angle)0);
            }
            return res;
        }
        private static GeoPoint2D[] TangentCircleLCC(Line2D l1, Circle2D c2, Circle2D c3)
        {
            GeoPoint2D[] centers;
            double[] radii;
            CircleL2C(l1.StartPoint, l1.EndPoint, c2.Center, c2.Radius, c3.Center, c3.Radius, out centers, out radii);
            GeoPoint2D[] res = new GeoPoint2D[radii.Length * 4];
            for (int i = 0; i < radii.Length; ++i)
            {
                res[4 * i] = centers[i];
                res[4 * i + 1] = Geometry.DropPL(centers[i], l1.StartPoint, l1.EndPoint);
                GeoPoint2D[] pf = c2.PerpendicularFoot(centers[i]);
                if (pf.Length == 2)
                {
                    if (Math.Abs((pf[0] | centers[i]) - radii[i]) < Math.Abs((pf[1] | centers[i]) - radii[i]))
                    {
                        res[4 * i + 2] = pf[0];
                    }
                    else
                    {
                        res[4 * i + 2] = pf[1];
                    }
                }
                else
                    if (pf.Length == 1)
                    res[4 * i + 2] = pf[0];
                else res[4 * i + 2] = new GeoPoint2D(c2.Center, c2.Radius, (Angle)0);
                pf = c3.PerpendicularFoot(centers[i]);
                if (pf.Length == 2)
                {
                    if (Math.Abs((pf[0] | centers[i]) - radii[i]) < Math.Abs((pf[1] | centers[i]) - radii[i]))
                    {
                        res[4 * i + 3] = pf[0];
                    }
                    else
                    {
                        res[4 * i + 3] = pf[1];
                    }
                }
                else
                    if (pf.Length == 1)
                    res[4 * i + 3] = pf[0];
                else res[4 * i + 3] = new GeoPoint2D(c3.Center, c3.Radius, (Angle)0);
            }
            return res;
        }
        private static GeoPoint2D[] TangentCircleLLC(Line2D l1, Line2D l2, Circle2D c3)
        {
            GeoPoint2D[] centers;
            double[] radii;
            Circle2LC(l1.StartPoint, l1.EndPoint, l2.StartPoint, l2.EndPoint, c3.Center, c3.Radius, out centers, out radii);
            GeoPoint2D[] res = new GeoPoint2D[radii.Length * 4];
            for (int i = 0; i < radii.Length; ++i)
            {
                res[4 * i] = centers[i];
                res[4 * i + 1] = Geometry.DropPL(centers[i], l1.StartPoint, l1.EndPoint);
                res[4 * i + 2] = Geometry.DropPL(centers[i], l2.StartPoint, l2.EndPoint);
                GeoPoint2D[] pf = c3.PerpendicularFoot(centers[i]);
                if (pf.Length == 2)
                {
                    if (Math.Abs((pf[0] | centers[i]) - radii[i]) < Math.Abs((pf[1] | centers[i]) - radii[i]))
                    {
                        res[4 * i + 3] = pf[0];
                    }
                    else
                    {
                        res[4 * i + 3] = pf[1];
                    }
                }
                else
                    if (pf.Length == 1)
                    res[4 * i + 3] = pf[0];
                else res[4 * i + 3] = new GeoPoint2D(c3.Center, c3.Radius, (Angle)0);
            }
            return res;
        }
        private static GeoPoint2D[] TangentCircleLLL(Line2D l1, Line2D l2, Line2D l3)
        {
            GeoPoint2D[] centers;
            double[] radii;
            Circle3L(l1.StartPoint, l1.EndPoint, l2.StartPoint, l2.EndPoint, l3.StartPoint, l3.EndPoint, out centers, out radii);
            GeoPoint2D[] res = new GeoPoint2D[radii.Length * 4];
            for (int i = 0; i < radii.Length; ++i)
            {
                res[4 * i] = centers[i];
                res[4 * i + 1] = Geometry.DropPL(centers[i], l1.StartPoint, l1.EndPoint);
                res[4 * i + 2] = Geometry.DropPL(centers[i], l2.StartPoint, l2.EndPoint);
                res[4 * i + 3] = Geometry.DropPL(centers[i], l3.StartPoint, l3.EndPoint);
            }
            return res;
        }
        /// <summary>
        /// Connect two curves: the endpoint of the first curve with the startpoint of the second curve.
        /// </summary>
        /// <param name="first">First curve</param>
        /// <param name="second">Second curve</param>
        /// <returns>true if succeede, false otherwise</returns>
        public static bool Connect(ICurve2D first, ICurve2D second)
        {
            // hier soll der Endpunkt der 1. Kurve mit dem Startpunkt der 2. Kurve
            // zur Deckung gebracht werden. Manche Kurven haben damit Probleme.
            // Problemfälle sind z.B. Linien, die sich ein kleines Stück überlappen,
            // ganz kleine Überschneidungen oder kleine Lücken. Schnittpunkte sind
            // eher ungeeignet, da es sich oft um tangentiale Fälle handelt.
            GeoPoint2D p1 = first.EndPoint;
            GeoPoint2D p2 = second.StartPoint;
            double d = Geometry.Dist(p1, p2);
            if (d == 0.0) return true; // beim Kreis z.B.
            GeoPoint2D c = new GeoPoint2D(p1, p2); // Zwischenpunkt
            double dist;
            do
            {
                dist = d;
                double par1 = first.PositionOf(c);
                double par2 = second.PositionOf(c);
                if (par2 > 1.0) return false;
                if (par1 < 0.0) return false;
                p1 = first.PointAt(par1);
                p2 = second.PointAt(par2);
                c = new GeoPoint2D(p1, p2); // neuer Zwischenpunkt
                d = Geometry.Dist(p1, p2);
            } while (d < dist * 0.7); // solange es einigermaßen konvergiert
            first.EndPoint = c;
            second.StartPoint = c;

            return true;
        }

        public static double SimpleMinimumDistance(ICurve2D first, ICurve2D second)
        {
            GeoPoint2D p1, p2;
            return SimpleMinimumDistance(first, second, out p1, out p2);
        }

        public static bool NearestPoint(GeoPoint2DWithParameter[] points, GeoPoint2D selectionPoint, out GeoPoint2DWithParameter nearestPoint)
        {
            double dist = double.MaxValue; // Entfernung vom selectionPoint für Schnittpunkte
            nearestPoint.p = new GeoPoint2D(0.0, 0.0);
            nearestPoint.par1 = 0.0;
            nearestPoint.par2 = 0.0;
            for (int i = 0; i < points.Length; ++i) // Schleife über alle Schnittpunkte
            {
                //  Den nächsten nehmen!
                double distLoc = Geometry.Dist(points[i].p, selectionPoint);
                if (distLoc < dist)
                {
                    dist = distLoc;
                    // jetzt merken
                    nearestPoint = points[i];
                }
            }
            if (dist < double.MaxValue) return true;
            return false;
        }

        /// <summary>
        /// Returns the minimum distance of the two given curves. 
        /// </summary>
        /// <param name="first">first curve</param>
        /// <param name="second">second curve</param>
        /// <param name="p1">returns the point on the first curve</param>
        /// <param name="p2">returns the point on the second curve</param>
        /// <returns>The value of the minimum distance</returns>
        public static double SimpleMinimumDistance(ICurve2D first, ICurve2D second, out GeoPoint2D p1, out GeoPoint2D p2)
        {
            return SimpleMinimumDistance(first, second, false, GeoPoint2D.Origin, out p1, out p2);
        }
        /// <summary>
        /// Returns the minimum distance of the two given curves. If the curves are parallel,
        /// the connection of p1 and p2 will go through preferredPosition.
        /// </summary>
        /// <param name="first">first curve</param>
        /// <param name="second">second curve</param>
        /// <param name="preferredPosition">Position for parallel curves</param>
        /// <param name="p1">returns the point on the first curve</param>
        /// <param name="p2">returns the point on the second curve</param>
        /// <returns>The value of the minimum distance</returns>
        public static double SimpleMinimumDistance(ICurve2D first, ICurve2D second, GeoPoint2D preferredPosition, out GeoPoint2D p1, out GeoPoint2D p2)
        {
            return SimpleMinimumDistance(first, second, true, preferredPosition, out p1, out p2);
        }
        /// <summary>
        /// Constructs two arcs that connect <paramref name="p1"/> and <paramref name="p2"/> and are tangential to
        /// <paramref name="dir1"/> and <paramref name="dir2"/> respectively.
        /// </summary>
        /// <param name="p1">Startpoint</param>
        /// <param name="p2">Endpoint</param>
        /// <param name="dir1">Starting direction</param>
        /// <param name="dir2">ending direction</param>
        /// <returns></returns>
        public static ICurve2D[] ConnectByTwoArcs(GeoPoint2D p1, GeoPoint2D p2, GeoVector2D dir1, GeoVector2D dir2)
        {	// gesucht sind zwei Kreisbögen, die an par1 bzw. par2 tangential zu dieser Kurve sind
            // und an einem inneren Punkt tangential ineinander übergehen. 
            // Es wird zuerst eine (willkürliche) innere Tangente bestimmt.
            // der innere Punkt innerPoint wird dann so bestimmt, dass p1, innerPoint und die beiden
            // Richtungen dir1 und innerTangent ein gleichschenkliges Dreieck bilden und ebenso
            // p2, dir2 und innerPoint, innerTangent. Mit dem gleichschenkligen Dreieck ist
            // gewährleistet, dass es jeweils einen Bogen gibt, der durch beide Eckpunkte geht
            // und dort tangential ist. 
            // Die bestimmung der inneren Tangente kann evtl. noch verbessert werden
            GeoPoint2D ps;
            if (Geometry.IntersectLL(p1, dir1, p2, dir2, out ps))
            {
                GeoVector2D innerTangent = (ps | p1) * dir1.Normalized + (ps | p2) * dir2.Normalized; // die Richtung der Tangente im inneren Schnittpunkt
                if (!Precision.IsNullVector(innerTangent))
                {
                    innerTangent.Norm(); // di1, dir2 und innerTangent sind normiert
                    GeoVector2D sec1 = dir1.Normalized + innerTangent;
                    GeoVector2D sec2 = dir2.Normalized + innerTangent;
                    GeoPoint2D innerPoint;
                    if (Geometry.IntersectLL(p1, sec1, p2, sec2, out innerPoint))
                    {
                        GeoPoint2D c1, c2;
                        if (Geometry.IntersectLL(p1, dir1.ToLeft(), innerPoint, innerTangent.ToLeft(), out c1))
                        {
                            if (Geometry.IntersectLL(p2, dir2.ToLeft(), innerPoint, innerTangent.ToLeft(), out c2))
                            {
                                Arc2D a1 = new Arc2D(c1, Geometry.Dist(c1, innerPoint), p1, innerPoint, !Geometry.OnLeftSide(p1, c1, innerPoint - c1));
                                Arc2D a2 = new Arc2D(c2, Geometry.Dist(c2, innerPoint), innerPoint, p2, Geometry.OnLeftSide(p2, c2, innerPoint - c2));
                                if (Precision.SameNotOppositeDirection(a1.EndDirection, a2.StartDirection, false) && Math.Abs(a1.Sweep) < Math.PI / 4 && Math.Abs(a2.Sweep) < Math.PI / 4
                                    && Math.Abs(a1.Sweep) > 1e-3 && Math.Abs(a2.Sweep) > 1e-3)
                                {
                                    return new ICurve2D[] { a1, a2 };
                                }
                            }
                        }
                    }
                }
            }
            return new ICurve2D[] { new Line2D(p1, p2) };
        }
        /// <summary>
        /// Calculates the distance of the two curves in the provided direction. In other words this means how far con you
        /// move the <paramref name="first"/> curve in the direction <paramref name="dir"/> so that it touches the <paramref name="second"/> curve?
        /// The result may be double.MaxValue if the curves will never touch eac other, or it might be negative if you would have to
        /// move first in the opposite direction of dir to touch second.
        /// <note>Currently only implemented for <see cref="Line2D"/> and <see cref="Arc2D"/></note>.
        /// </summary>
        /// <param name="first">Fisrt curve</param>
        /// <param name="second">Second curve</param>
        /// <param name="dir">Direction of movement or distance</param>
        /// <returns>Distance</returns>
        public static double DistanceAtDirection(ICurve2D first, ICurve2D second, GeoVector2D dir)
        {
            dir.Norm();
            BoundingRect ext = first.GetExtent() + second.GetExtent();
            double length = ext.Width + ext.Height;
            double res = double.MaxValue;
            if (first is Line2D && second is Line2D)
            {
                Line2D l1 = first as Line2D;
                Line2D l2 = second as Line2D;
                double pos1, pos2;
                Geometry.IntersectLLpar(l1.StartPoint, dir, l2.StartPoint, l2.EndPoint - l2.StartPoint, out pos1, out pos2);
                if (pos2 >= 0.0 && pos2 <= 1.0)
                {
                    res = Math.Min(res, pos1);
                }
                Geometry.IntersectLLpar(l1.EndPoint, dir, l2.StartPoint, l2.EndPoint - l2.StartPoint, out pos1, out pos2);
                if (pos2 >= 0.0 && pos2 <= 1.0)
                {
                    res = Math.Min(res, pos1);
                }
                Geometry.IntersectLLpar(l2.StartPoint, -dir, l1.StartPoint, l1.EndPoint - l1.StartPoint, out pos1, out pos2);
                if (pos2 >= 0.0 && pos2 <= 1.0)
                {
                    res = Math.Min(res, pos1);
                }
                Geometry.IntersectLLpar(l2.EndPoint, -dir, l1.StartPoint, l1.EndPoint - l1.StartPoint, out pos1, out pos2);
                if (pos2 >= 0.0 && pos2 <= 1.0)
                {
                    res = Math.Min(res, pos1);
                }
                return res;
            }
            if (first is Arc2D && second is Arc2D)
            {
                Arc2D a1 = first as Arc2D;
                Arc2D a2 = second as Arc2D;
                // zuerst: gibt es eine Berührung ohne Beteiligung der Endpunkte
                // Außenberührung
                GeoPoint2D[] ips = Geometry.IntersectLC(a1.Center, dir, a2.Center, a1.Radius + a2.Radius);
                for (int i = 0; i < ips.Length; ++i)
                {

                    GeoPoint2D[] common = Geometry.IntersectCC(ips[i], a1.Radius, a2.Center, a2.Radius);
                    if (common.Length >= 1)
                    {
                        GeoVector2D trans = (ips[i] - a1.Center);
                        double pos1 = a1.PositionOf(common[0] - trans);
                        double pos2 = a2.PositionOf(common[0]);
                        if (a1.IsParameterOnCurve(pos1) && a2.IsParameterOnCurve(pos2))
                        {
                            double l;
                            if (Math.Abs(dir.x) > Math.Abs(dir.y)) l = trans.x / dir.x;
                            else l = trans.y / dir.y;
                            res = Math.Min(res, l);
                        }
                    }
                }
                // Innenberührung
                ips = Geometry.IntersectLC(a1.Center, dir, a2.Center, Math.Abs(a1.Radius - a2.Radius));
                for (int i = 0; i < ips.Length; ++i)
                {

                    GeoPoint2D[] common = Geometry.IntersectCC(ips[i], a1.Radius, a2.Center, a2.Radius);
                    if (common.Length >= 1) // oft zwei fast identische Punkte
                    {
                        GeoVector2D trans = (ips[i] - a1.Center);
                        double pos1 = a1.PositionOf(common[0] - trans);
                        double pos2 = a2.PositionOf(common[0]);
                        if (a1.IsParameterOnCurve(pos1) && a2.IsParameterOnCurve(pos2))
                        {
                            double l;
                            if (Math.Abs(dir.x) > Math.Abs(dir.y)) l = trans.x / dir.x;
                            else l = trans.y / dir.y;
                            res = Math.Min(res, l);
                        }
                    }
                }
                // Anstoßen an den Endpunkten
                Line2D l2d = new Line2D(a1.StartPoint, a1.StartPoint + length * dir);
                GeoPoint2DWithParameter[] ip = a2.Intersect(l2d);
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (a2.IsParameterOnCurve(ip[i].par1))
                    {
                        res = Math.Min(res, ip[i].par2 * length);
                    }
                }
                l2d = new Line2D(a1.EndPoint, a1.EndPoint + length * dir);
                ip = a2.Intersect(l2d);
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (a2.IsParameterOnCurve(ip[i].par1))
                    {
                        res = Math.Min(res, ip[i].par2 * length);
                    }
                }
                l2d = new Line2D(a2.StartPoint, a2.StartPoint - length * dir);
                ip = a1.Intersect(l2d);
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (a1.IsParameterOnCurve(ip[i].par1))
                    {
                        res = Math.Min(res, ip[i].par2 * length);
                    }
                }
                l2d = new Line2D(a2.EndPoint, a2.EndPoint - length * dir);
                ip = a1.Intersect(l2d);
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (a1.IsParameterOnCurve(ip[i].par1))
                    {
                        res = Math.Min(res, ip[i].par2 * length);
                    }
                }
                return res;
            }
            Line2D line = null;
            Arc2D arc = null;
            if (first is Line2D && second is Arc2D)
            {
                line = first as Line2D;
                arc = second as Arc2D;
            }
            else if (first is Arc2D && second is Line2D)
            {
                line = second as Line2D;
                arc = first as Arc2D;
                dir = -dir; // Richtung umdrehen
            }
            else
            {   // erstmal nur für linien und Bögen
                // später in Linien und Bögen annähern und dann berechnen
                return double.MaxValue;
            }
            // jetzt also line und bogen
            {
                GeoVector2D perpline = line.StartDirection.ToLeft();
                perpline.Norm();
                GeoPoint2D tp1 = arc.Center + arc.Radius * perpline;
                GeoPoint2D tp2 = arc.Center - arc.Radius * perpline;
                Line2D l2d;
                GeoPoint2DWithParameter[] ip;
                if (arc.IsParameterOnCurve(arc.PositionOf(tp1)))
                {
                    l2d = new Line2D(tp1, tp1 - length * dir);
                    ip = line.Intersect(l2d);
                    for (int i = 0; i < ip.Length; ++i)
                    {
                        if (line.IsParameterOnCurve(ip[i].par1))
                        {
                            res = Math.Min(res, ip[i].par2 * length);
                        }
                    }
                }
                if (arc.IsParameterOnCurve(arc.PositionOf(tp2)))
                {
                    l2d = new Line2D(tp2, tp2 - length * dir);
                    ip = line.Intersect(l2d);
                    for (int i = 0; i < ip.Length; ++i)
                    {
                        if (line.IsParameterOnCurve(ip[i].par1))
                        {
                            res = Math.Min(res, ip[i].par2 * length);
                        }
                    }
                }
                l2d = new Line2D(line.StartPoint, line.StartPoint + length * dir);
                ip = arc.Intersect(l2d);
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (arc.IsParameterOnCurve(ip[i].par1))
                    {
                        res = Math.Min(res, ip[i].par2 * length);
                    }
                }
                l2d = new Line2D(line.EndPoint, line.EndPoint + length * dir);
                ip = arc.Intersect(l2d);
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (arc.IsParameterOnCurve(ip[i].par1))
                    {
                        res = Math.Min(res, ip[i].par2 * length);
                    }
                }
                l2d = new Line2D(arc.StartPoint, arc.StartPoint - length * dir);
                ip = line.Intersect(l2d);
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (line.IsParameterOnCurve(ip[i].par1))
                    {
                        res = Math.Min(res, ip[i].par2 * length);
                    }
                }
                l2d = new Line2D(arc.EndPoint, arc.EndPoint - length * dir);
                ip = line.Intersect(l2d);
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (line.IsParameterOnCurve(ip[i].par1))
                    {
                        res = Math.Min(res, ip[i].par2 * length);
                    }
                }
                return res;
            }
        }
        #region private helper
        private static double SimpleMinimumDistance(ICurve2D first, ICurve2D second, bool usePreferred, GeoPoint2D preferredPosition, out GeoPoint2D p1, out GeoPoint2D p2)
        {
            // 1.: bei Schnitten: 0.0
            p1 = new GeoPoint2D(0.0, 0.0);
            p2 = new GeoPoint2D(0.0, 0.0);
            GeoPoint2DWithParameter[] pp = first.Intersect(second);
            for (int i = 0; i < pp.Length; ++i)
            {
                if (pp[i].par1 >= 0.0 && pp[i].par1 <= 1.0 && pp[i].par2 >= 0.0 && pp[i].par2 <= 1.0)
                {	// Die Epsilontik ist hier nicht wichtig, denn was hier durch die Lappen
                    // geht wird weiter unten noch gefunden.
                    p1 = p2 = pp[i].p;
                    return 0.0;
                }
            }
            double minDist = double.MaxValue;
            double minDistLoc;

            bool doIterate = false; // nach den Fußpunkten iterieren, z.B. für zwei Ellipsen

            if (first is Path2D)
            {
                double mindist = double.MaxValue;
                Path2D fp = first.Clone() as Path2D;
                fp.Flatten();
                for (int i = 0; i < fp.SubCurves.Length; i++)
                {
                    GeoPoint2D pp1, pp2;
                    double dist = SimpleMinimumDistance(fp.SubCurves[i], second, usePreferred, preferredPosition, out pp1, out pp2);
                    if (dist < mindist)
                    {
                        mindist = dist;
                        p1 = pp1;
                        p2 = pp2;
                    }
                }
                return mindist;
            }
            else if (second is Path2D)
            {   // umgedreht aufrufen, first ist ja kein Path2D
                return SimpleMinimumDistance(second, first, usePreferred, preferredPosition, out p2, out p1);
            }
            else if (first is Line2D && second is Circle2D)
            {	// Spezialfall Kreis(Bogen) Linie ist so gelöst
                return SimpleMinimumDistance(second, first, usePreferred, preferredPosition, out p2, out p1);
            }
            else if (first is Circle2D && second is Line2D)
            {
                // hier schon klar, dass kein Schnitt
                Circle2D circ = first as Circle2D;
                Line2D lin = second as Line2D;
                GeoPoint2D[] ppp = lin.PerpendicularFoot(circ.Center); // immer genau eine Lösung
                GeoPoint2D perpLine = ppp[0];
                if (lin.IsParameterOnCurve(lin.PositionOf(perpLine)))
                {
                    p2 = perpLine;
                    ppp = circ.PerpendicularFoot(perpLine); // immer genau zwei Lösungen
                    if (ppp.Length == 2)
                    {
                        double d0 = Geometry.Dist(ppp[0], perpLine);
                        double d1 = Geometry.Dist(ppp[1], perpLine);
                        if (d0 < d1)
                        {
                            if (circ.IsParameterOnCurve(circ.PositionOf(ppp[0])))
                            {
                                p1 = ppp[0];
                                minDist = Geometry.Dist(p1, p2);
                            }
                        }
                        else
                        {
                            if (circ.IsParameterOnCurve(circ.PositionOf(ppp[0])))
                            {
                                p1 = ppp[0];
                                minDist = Geometry.Dist(p1, p2);
                            }
                        }
                    }
                }
            }
            else if (first is Circle2D && second is Circle2D)
            {	// sie schneiden sich nicht!
                Circle2D circ1 = first as Circle2D;
                Circle2D circ2 = second as Circle2D;
                if (!Precision.IsEqual(circ1.Center, circ2.Center))
                {
                    Line2D l = new Line2D(circ1.Center, circ2.Center);
                    GeoPoint2DWithParameter[] pwp1 = l.Intersect(circ1);
                    GeoPoint2DWithParameter[] pwp2 = l.Intersect(circ2);
                    for (int i = 0; i < pwp1.Length; ++i)
                    {
                        for (int j = 0; j < pwp2.Length; ++j)
                        {
                            if (pwp1[i].par2 >= 0.0 && pwp1[i].par2 <= 1.0 &&
                                pwp2[j].par2 >= 0.0 && pwp2[j].par2 <= 1.0)
                            {
                                double d = Geometry.Dist(pwp1[i].p, pwp2[j].p);
                                if (d < minDist)
                                {
                                    minDist = d;
                                    p1 = pwp1[i].p;
                                    p2 = pwp2[j].p;
                                }
                            }
                        }
                    }
                }
            }
            else if (first is Line2D && second is Line2D)
            {	// nix machen
            }
            else if (usePreferred)
            {
                doIterate = true;
            }

            if (usePreferred)
            {
                // 2a.: die Fußpunkte der preferredPosition auf beide Kurven betrachten
                do
                {	// ggf. wird hier iteriert: die beiden Fußpunkte werden verbunden und die 
                    // Mitte wird als neuer Startpunkt genommen
                    GeoPoint2D[] pf1 = first.PerpendicularFoot(preferredPosition);
                    GeoPoint2D[] pf2 = second.PerpendicularFoot(preferredPosition);
                    bool goOnIterate = false;
                    for (int i = 0; i < pf1.Length; ++i)
                    {
                        for (int j = 0; j < pf2.Length; ++j)
                        {
                            double d1 = first.PositionOf(pf1[i]);
                            double d2 = second.PositionOf(pf2[j]);
                            if (d1 >= 0.0 && d1 <= 1.0 && d2 >= 0.0 && d2 <= 1.0)
                            {
                                minDistLoc = Geometry.Dist(pf1[i], pf2[j]);
                                if (minDistLoc < minDist)
                                {
                                    minDist = minDistLoc;
                                    p1 = pf1[i];
                                    p2 = pf2[j];
                                    if (doIterate)
                                    {
                                        goOnIterate = true;
                                        preferredPosition = new GeoPoint2D(p1, p2); // Mittelpunkt
                                    }
                                }
                            }
                        }
                    }
                    doIterate = goOnIterate;
                } while (doIterate);
            }
            double minDistPreferred = minDist;	// wenn weiter unten ein fast identischer Abstand
            GeoPoint2D p1Preferred = p1;			// gefunden wird, dann soll dieser (weil bevorzugt) gelten
            GeoPoint2D p2Preferred = p2;


            // 2b.: die Fußpunkte der Start- und Endpunkte auf der anderen Kurve betrachten
            GeoPoint2D[] p = first.PerpendicularFoot(second.StartPoint);
            for (int i = 0; i < p.Length; ++i)
            {
                double d = first.PositionOf(p[i]);
                //				if (d>=0.0 && d<=1.0) minDist = Math.Min(minDist,Geometry.dist(p[i],second.StartPoint));
                if (d >= 0.0 && d <= 1.0)
                {
                    minDistLoc = Geometry.Dist(p[i], second.StartPoint);
                    if (minDistLoc < minDist)
                    {
                        minDist = minDistLoc;
                        p1 = p[i];
                        p2 = second.StartPoint;
                    }
                }
            }
            p = first.PerpendicularFoot(second.EndPoint);
            for (int i = 0; i < p.Length; ++i)
            {
                double d = first.PositionOf(p[i]);
                //				if (d>=0.0 && d<=1.0) minDist = Math.Min(minDist,Geometry.dist(p[i],second.EndPoint));
                if (d >= 0.0 && d <= 1.0)
                {
                    minDistLoc = Geometry.Dist(p[i], second.EndPoint);
                    if (minDistLoc < minDist)
                    {
                        minDist = minDistLoc;
                        p1 = p[i];
                        p2 = second.EndPoint;
                    }
                }
            }
            p = second.PerpendicularFoot(first.StartPoint);
            for (int i = 0; i < p.Length; ++i)
            {
                double d = second.PositionOf(p[i]);
                //				if (d>=0.0 && d<=1.0) minDist = Math.Min(minDist,Geometry.dist(p[i],first.StartPoint));
                if (d >= 0.0 && d <= 1.0)
                {
                    minDistLoc = Geometry.Dist(p[i], first.StartPoint);
                    if (minDistLoc < minDist)
                    {
                        minDist = minDistLoc;
                        p2 = p[i];
                        p1 = first.StartPoint;
                    }
                }
            }
            p = second.PerpendicularFoot(first.EndPoint);
            for (int i = 0; i < p.Length; ++i)
            {
                double d = second.PositionOf(p[i]);
                //				if (d>=0.0 && d<=1.0) minDist = Math.Min(minDist,Geometry.dist(p[i],first.EndPoint));
                if (d >= 0.0 && d <= 1.0)
                {
                    minDistLoc = Geometry.Dist(p[i], first.EndPoint);
                    if (minDistLoc < minDist)
                    {
                        minDist = minDistLoc;
                        p2 = p[i];
                        p1 = first.EndPoint;
                    }
                }
            }

            // 3.: die 4 Enpunktabstände gehören auf jeden Fall dazu
            //			minDist = Math.Min(minDist,Geometry.dist(second.StartPoint,first.EndPoint));
            minDistLoc = Geometry.Dist(second.StartPoint, first.EndPoint);
            if (minDistLoc < minDist)
            {
                minDist = minDistLoc;
                p2 = second.StartPoint;
                p1 = first.EndPoint;
            }
            //			minDist = Math.Min(minDist,Geometry.dist(second.EndPoint,first.EndPoint));
            minDistLoc = Geometry.Dist(second.EndPoint, first.EndPoint);
            if (minDistLoc < minDist)
            {
                minDist = minDistLoc;
                p2 = second.EndPoint;
                p1 = first.EndPoint;
            }
            //			minDist = Math.Min(minDist,Geometry.dist(second.StartPoint,first.StartPoint));
            minDistLoc = Geometry.Dist(second.StartPoint, first.StartPoint);
            if (minDistLoc < minDist)
            {
                minDist = minDistLoc;
                p2 = second.StartPoint;
                p1 = first.StartPoint;
            }
            //			minDist = Math.Min(minDist,Geometry.dist(second.EndPoint,first.StartPoint));
            minDistLoc = Geometry.Dist(second.EndPoint, first.StartPoint);
            if (minDistLoc < minDist)
            {
                minDist = minDistLoc;
                p2 = second.EndPoint;
                p1 = first.StartPoint;
            }
            if (minDist < minDistPreferred && minDistPreferred - minDist < Precision.eps)
            {	// minDistPreferred wurde nur geringfügig unterboten. Dann sollen die preferred 
                // Werte zurückgeliefert werden
                p1 = p1Preferred;
                p2 = p2Preferred;
                minDist = minDistPreferred;
            }
            return minDist;
        }
        #endregion
        #region Helper from http://www.arcenciel.co.uk/geometry/ angepasst auf double und GeoPoint2D

        /// <summary>
        /// Get the normalised equation of a line between two points in the form: Ax + By + C = 0
        /// </summary>
        /// <param name="pt1">First point</param>
        /// <param name="pt2">Second point</param>
        /// <param name="A">X factor</param>
        /// <param name="B">Y factor</param>
        /// <param name="C">Constant</param>
        /// <returns>true on success</returns>
        private static bool LineCoefficients(GeoPoint2D pt1, GeoPoint2D pt2, out double A, out double B, out double C)
        //****************************************************************************************
        // Derive the equation of the line between 2 points in the form Ax + By + C = 0
        //  If the points are coincident, return false
        // In this overload, coordinates are integral values
        {
            // Validation
            if ((pt1 == pt2))
            {
                A = B = C = 0.0;
                return false;
            }

            // Get constants
            double xDiff = pt2.x - pt1.x;
            double yDiff = pt2.y - pt1.y;
            double rSquare = (xDiff * xDiff) + (yDiff * yDiff);
            double rInv = 1.0 / rSquare;

            // Derive parameters
            A = -yDiff * rInv;
            B = xDiff * rInv;
            C = (pt1.x * pt2.y - pt2.x * pt1.y) * rInv;

            // Normalize the equation for convenience
            double sMult = 1.0 / Math.Sqrt(A * A + B * B);
            A *= sMult;
            B *= sMult;
            C *= sMult;

            // Return success
            return true;
        }


        /// <summary>
        /// Find circles tangent to three lines
        /// </summary>
        /// <param name="pt11">First point on first line</param>
        /// <param name="pt12">Second point on first line</param>
        /// <param name="pt21">First point on second line</param>
        /// <param name="pt22">Second point on second line</param>
        /// <param name="pt31">First point on third line</param>
        /// <param name="pt32">Second point on third line</param>
        /// <param name="Centres">Array to receive list of centre points</param>
        /// <param name="Radii">Array to receive list of radii</param>
        private static void Circle3L(GeoPoint2D pt11, GeoPoint2D pt12, GeoPoint2D pt21, GeoPoint2D pt22,
              GeoPoint2D pt31, GeoPoint2D pt32, out GeoPoint2D[] Centres, out double[] Radii)
        // Find circles tangent to 3 lines
        {
            double a1, b1, c1, a2, b2, c2, a3, b3, c3;
            double t1, t2, t3, u, v, Div, fRadius, xc, yc;
            double nRadius;
            GeoPoint2D ptCentre;

            // Initialise
            List<GeoPoint2D> CentersList = new List<GeoPoint2D>();
            List<double> RadiiList = new List<double>();

            // Ensure second line is not vertical
            if (pt21.x == pt22.x)
            {
                if (pt11.x == pt12.x)
                {
                    GeoPoint2D ptTemp = pt21;
                    pt21 = pt31;
                    pt31 = ptTemp;
                    ptTemp = pt22;
                    pt22 = pt32;
                    pt32 = ptTemp;
                }
                else
                {
                    GeoPoint2D ptTemp = pt11;
                    pt11 = pt21;
                    pt21 = ptTemp;
                    ptTemp = pt12;
                    pt12 = pt22;
                    pt22 = ptTemp;
                }
            }

            // Get normalised line equations
            if (!LineCoefficients(pt11, pt12, out a1, out b1, out c1) ||
            !LineCoefficients(pt21, pt22, out a2, out b2, out c2) ||
            !LineCoefficients(pt31, pt32, out a3, out b3, out c3))
            {
                Centres = new GeoPoint2D[0];
                Radii = new double[0];
                return;
            }

            // Derive constants from equations
            u = (a2 * b1) - (a1 * b2);
            v = (a3 * b2) - (a2 * b3);

            // Take all combinations of t(n) to get circles on other side of lines
            for (int iCase = 0; iCase < 8; ++iCase)
            {
                t1 = ((iCase & 1) == 0) ? 1 : -1;
                t2 = ((iCase & 2) == 0) ? 1 : -1;
                t3 = ((iCase & 4) == 0) ? 1 : -1;

                // Calculate radius
                Div = v * (b1 * t2 - b2 * t1) - u * (b2 * t3 - b3 * t2);
                if (Div == 0.0) continue;
                fRadius = (u * (b3 * c2 - b2 * c3) - v * (b2 * c1 - b1 * c2)) / Div;
                if (fRadius <= 0.0) continue;
                nRadius = fRadius;

                // Derive centre
                if (u != 0.0) xc = (b2 * c1 - b2 * fRadius * t1 - b1 * c2 + b1 * fRadius * t2) / u;
                else if (v != 0.0) xc = (b3 * c2 + b3 * fRadius * t2 - b2 * c3 + b2 * fRadius * t3) / v;
                else continue;
                // if (Math.Abs(xc) > (double)Int32.MaxValue) continue;
                if (b1 != 0.0) yc = (-a1 * xc - c1 + fRadius * t1) / b1;
                else if (b2 != 0.0) yc = (-a2 * xc - c2 + fRadius * t2) / b2;
                else yc = (-a3 * xc - c3 + fRadius * t3) / b3;
                // if (Math.Abs(yc) > (double)Int32.MaxValue) continue;
                ptCentre = new GeoPoint2D(xc, yc);

                // Add to solutions list
                CentersList.Add(ptCentre);
                RadiiList.Add(nRadius);
            }

            // Return the number of solutions found
            Centres = CentersList.ToArray();
            Radii = RadiiList.ToArray();
        }
        /// <summary>
        /// Find circles tangent to two lines and a circle
        /// </summary>
        /// <param name="pt11">First point on first line</param>
        /// <param name="pt12">Second point on first line</param>
        /// <param name="pt21">First point on second line</param>
        /// <param name="pt22">Second point on second line</param>
        /// <param name="ptCentre">Centre of circle</param>
        /// <param name="nRadius">Radius of circle</param>
        /// <param name="Centres">Array to receive list of centre points</param>
        /// <param name="Radii">Array to receive list of radii</param>
        /// <returns>Number of solutions</returns>
        private static void Circle2LC(GeoPoint2D pt11, GeoPoint2D pt12, GeoPoint2D pt21, GeoPoint2D pt22,
              GeoPoint2D ptCentre, double nRadius, out GeoPoint2D[] Centres, out double[] Radii)
        //*************************************************************************
        // Find circles tangent to 2 lines and a circle
        {


            // Translate so first circle is on origin
            GeoPoint2D pt11N = new GeoPoint2D(pt11.x - ptCentre.x, pt11.y - ptCentre.y);
            GeoPoint2D pt12N = new GeoPoint2D(pt12.x - ptCentre.x, pt12.y - ptCentre.y);
            GeoPoint2D pt21N = new GeoPoint2D(pt21.x - ptCentre.x, pt21.y - ptCentre.y);
            GeoPoint2D pt22N = new GeoPoint2D(pt22.x - ptCentre.x, pt22.y - ptCentre.y);

            // If first line not vertical
            if (pt11.x != pt12.x)
            {
                NormCircle2LC(pt11N, pt12N, pt21N, pt22N, nRadius, out Centres, out Radii);
            }

            // If second line not vertical
            else if (pt21.x != pt22.x)
            {
                NormCircle2LC(pt21N, pt22N, pt11N, pt12N, nRadius, out Centres, out Radii);
            }

            // If both lines vertical, special case
            else
            {
                // Initialise
                List<GeoPoint2D> CentersList = new List<GeoPoint2D>();
                List<double> RadiiList = new List<double>();

                // Centres are midway between the lines, and the radius is half the separation
                GeoPoint2D ptTanCirc = new GeoPoint2D();
                ptTanCirc.x = (pt11N.x + pt21N.x) / 2.0;
                double radTanCirc = Math.Abs(pt11N.x - pt21N.x) / 2.0;

                // Calculate +/- y-coordinate of centres
                double ycSquare = ((radTanCirc + nRadius) * (radTanCirc + nRadius)) -
                      (ptTanCirc.x * ptTanCirc.x);
                double yc = Math.Sqrt(ycSquare);
                ptTanCirc.y = ptCentre.y + yc;

                // Add solutions
                CentersList.Add(ptTanCirc);
                RadiiList.Add(radTanCirc);
                if (yc != 0)
                {
                    ptTanCirc.y = ptCentre.y - yc;
                    CentersList.Add(ptTanCirc);
                    RadiiList.Add(radTanCirc);
                }

                Centres = CentersList.ToArray();
                Radii = RadiiList.ToArray();
            }

            // Transform results back to original coordinate system
            for (int iSolution = 0; iSolution < Centres.Length; ++iSolution)
            {
                Centres[iSolution].x += ptCentre.x;
                Centres[iSolution].y += ptCentre.y;
            }

        }

        private static void NormCircle2LC(GeoPoint2D pt11, GeoPoint2D pt12, GeoPoint2D pt21, GeoPoint2D pt22, double nRadius,
                                                        out GeoPoint2D[] Centres, out double[] Radii)
        //*******************************************************************************************
        // Find circles tangent to 2 lines and a circle centred on (0, 0)
        //     pt11.x <> pt12.x. (If line is vertical, swap parameters in call)
        {
            double a1, b1, c1, a2, b2, c2, b24ac, b24acRoot;
            double t1, t2, r3, u, w, s, A, B, C, fRadius;

            // Initialise
            List<GeoPoint2D> CentersList = new List<GeoPoint2D>();
            List<double> RadiiList = new List<double>();

            // Confirm first line not vertical (so b1 <> 0)
            if (pt11.x == pt12.x)
            {
                Centres = new GeoPoint2D[0];
                Radii = new double[0];
                return;
            }

            // Get normalised line equations
            if (!LineCoefficients(pt11, pt12, out a1, out b1, out c1) ||
            !LineCoefficients(pt21, pt22, out a2, out b2, out c2))
            {
                Centres = new GeoPoint2D[0];
                Radii = new double[0];
                return;
            }

            // Take all combinations of t(n) to get circles on other side of lines
            // and nRadius to get circles on other side of given circle
            for (int iCase = 0; iCase < 8; ++iCase)
            {
                t1 = ((iCase & 1) == 0) ? 1 : -1;
                t2 = ((iCase & 2) == 0) ? 1 : -1;
                r3 = ((iCase & 4) == 0) ? nRadius : -nRadius;

                // Derive constants
                u = (t1 * b2) - (t2 * b1);
                w = (b1 * c2) - (b2 * c1);
                s = (a1 * b2) - (a2 * b1);
                A = (u * u) - (2 * a1 * s * u * t1) + (t1 * t1 * s * s) - (b1 * b1 * s * s);
                B = 2.0 * ((u * w) + (c1 * a1 * s * u) - (a1 * s * t1 * w) - (c1 * t1 * s * s) - (r3 * b1 * b1 * s * s));
                C = (w * w) + (2 * a1 * s * c1 * w) + (c1 * c1 * s * s) - (b1 * b1 * r3 * r3 * s * s);

                // Calculate radii as roots of a quadratic
                b24ac = (B * B) - (4 * A * C);
                if ((b24ac < 0.0) || Math.Abs(A) < 1e-10) continue;
                b24acRoot = Math.Sqrt(b24ac);
                for (int iRoot = 0; iRoot < 2; ++iRoot)
                {
                    if ((b24ac == 0.0) && (iRoot > 0)) continue;
                    if (iRoot == 0) fRadius = (-B + b24acRoot) / (A + A);
                    else fRadius = (-B - b24acRoot) / (A + A);
                    if ((fRadius <= 0.0)) continue;
                    double radTanCirc = fRadius;

                    // Derive x-coordinate of centre 
                    GeoPoint2D ptCentre = new GeoPoint2D();
                    double xc, yc;
                    if (Math.Abs(s) > 1e-10)
                    {
                        // Calculate centre where lines not parallel
                        xc = (fRadius * u + w) / s;
                        //if (Math.Abs(xc) > (double)Int32.MaxValue) continue;
                        ptCentre.x = xc;
                        yc = ((-a1 * xc) - c1 + (fRadius * t1)) / b1;
                        //if (Math.Abs(yc) > (double)Int32.MaxValue) continue;
                        ptCentre.y = yc;

                        // Add solution
                        CentersList.Add(ptCentre);
                        RadiiList.Add(radTanCirc);
                    }
                    else  // If lines are parallel, there are 2 solutions
                    {
                        double Ac = t1 * t1;
                        double Bc = 2 * a1 * (c1 - (fRadius * t1));
                        double Cc = ((fRadius * t1) - c1) * ((fRadius * t1) - c1) - (b1 * b2 * (r3 + fRadius) * (r3 + fRadius));
                        b24ac = (Bc * Bc) - (4 * Ac * Cc);
                        if ((b24ac < 0.0) || (Ac == 0.0)) continue;
                        b24acRoot = Math.Sqrt(b24ac);

                        for (int xCase = 0; xCase < 2; ++xCase)
                        {
                            // Calculate x-coordinate
                            if (xCase == 0) xc = (-Bc + b24acRoot) / (Ac + Ac);
                            else xc = (-Bc - b24acRoot) / (Ac + Ac);
                            //if (Math.Abs(xc) > (double)Int32.MaxValue) continue;
                            ptCentre.x = xc;

                            // Calculate y-coordinate
                            yc = (-a1 * xc - c1 + fRadius * t1) / b1;
                            //if (Math.Abs(yc) > (double)Int32.MaxValue) continue;
                            ptCentre.y = yc;

                            // Add solution
                            CentersList.Add(ptCentre);
                            RadiiList.Add(radTanCirc);
                        }
                    }
                }
            }

            Centres = CentersList.ToArray();
            Radii = RadiiList.ToArray();
        }
        /// <summary>
        /// Find circles tangent to a line and two circles
        /// </summary>
        /// <param name="pt1">First point on line</param>
        /// <param name="pt2">Second point on line</param>
        /// <param name="ptCentre1">Centre of first circle</param>
        /// <param name="nRad2">Radius of first circle</param>
        /// <param name="ptCentre2">Centre of second circle</param>
        /// <param name="nRad3">Radius of second circle</param>
        /// <param name="Centres">Array to receive list of centre points</param>
        /// <param name="Radii">Array to receive list of radii</param>
        /// <returns>Number of solutions</returns>
        private static void CircleL2C(GeoPoint2D pt1, GeoPoint2D pt2, GeoPoint2D ptCentre1, double nRad2,
                                GeoPoint2D ptCentre2, double nRad3, out GeoPoint2D[] Centres, out double[] Radii)
        //***************************************************************************
        // Find tangents to a line and two circles
        {

            // Initialise
            List<GeoPoint2D> CentersList = new List<GeoPoint2D>();
            List<double> RadiiList = new List<double>();

            // Translate so first circle is on origin
            GeoPoint2D pt1N = new GeoPoint2D(pt1.x - ptCentre1.x, pt1.y - ptCentre1.y);
            GeoPoint2D pt2N = new GeoPoint2D(pt2.x - ptCentre1.x, pt2.y - ptCentre1.y);
            GeoPoint2D ptCentre2N = new GeoPoint2D(ptCentre2.x - ptCentre1.x, ptCentre2.y - ptCentre1.y);

            // Find angle to rotate second circle to x-axis
            double ang = Math.Atan2(-ptCentre2N.y, ptCentre2N.x);
            double angCos = Math.Cos(ang);
            double angSin = Math.Sin(ang);

            // Rotate circle centre
            double x3 = (ptCentre2N.x * angCos) - (ptCentre2N.y * angSin);

            // Rotate first point on line
            double iTemp = (pt1N.x * angCos) - (pt1N.y * angSin);
            pt1N.y = ((pt1N.x * angSin) + (pt1N.y * angCos));
            pt1N.x = iTemp;

            // Rotate second point on line
            iTemp = (pt2N.x * angCos) - (pt2N.y * angSin);
            pt2N.y = ((pt2N.x * angSin) + (pt2N.y * angCos));
            pt2N.x = iTemp;

            // Derive solutions
            GeoPoint2D[] fCentres;
            double[] fRadii;
            NormCircleL2C(pt1N, pt2N, nRad2, x3, nRad3, out fCentres, out fRadii);

            // Transform solutions back to original coordinate system
            double xc, yc;
            GeoPoint2D ptCentre = new GeoPoint2D();
            double angNCos = Math.Cos(-ang);
            double angNSin = Math.Sin(-ang);
            for (int iSolution = 0; iSolution < fRadii.Length; ++iSolution)
            {
                xc = fCentres[iSolution].x;
                yc = fCentres[iSolution].y;
                ptCentre.x = ((xc * angNCos - yc * angNSin) + ptCentre1.x);
                ptCentre.y = ((xc * angNSin + yc * angNCos) + ptCentre1.y);
                CentersList.Add(ptCentre);
                RadiiList.Add(fRadii[iSolution]);
            }

            Centres = CentersList.ToArray();
            Radii = RadiiList.ToArray();

        }

        private static void NormCircleL2C(GeoPoint2D pt1, GeoPoint2D pt2, double nRad2, double x3, double nRad3,
                                                        out GeoPoint2D[] fCentres, out double[] fRadii)
        //************************************************************************
        // Find circles tangent to a line and 2 circles. The first circle is centred on the origin,
        // the second at (x3, 0)
        {
            double a1, b1, c1, t, r2, r3, a, b, c, u, s;
            double A, B, C, b24ac, b24acRoot, fRadius, xc, yc, Ac, Bc, Cc;

            // Initialise
            List<GeoPoint2D> CentersList = new List<GeoPoint2D>();
            List<double> RadiiList = new List<double>();

            // Get line equation
            a1 = 0.0; b1 = 0.0; c1 = 0.0;
            if (!LineCoefficients(pt1, pt2, out a1, out b1, out c1))
            {
                fCentres = new GeoPoint2D[0];
                fRadii = new double[0];
                return;
            }

            // Take all combinations of t to get circles on other side of line,
            // and radii to get circles on other side of given circles
            for (int iCase = 0; iCase < 8; ++iCase)
            {
                t = ((iCase & 1) == 0) ? 1 : -1;
                r2 = ((iCase & 2) == 0) ? nRad2 : -nRad2;
                r3 = ((iCase & 4) == 0) ? nRad3 : -nRad3;

                // Get constants
                a = 2 * (a1 * (r2 - r3) - x3 * t);
                b = 2 * b1 * (r2 - r3);
                c = 2 * c1 * (r2 - r3) + t * (r2 * r2 - r3 * r3 + x3 * x3);
                if (b != 0.0)
                {
                    u = b1 * c - b * c1;
                    s = a1 * b - a * b1;
                    A = t * t * b * b * (a * a + b * b) - b * b * s * s;
                    B = 2 * (u * t * b * (a * a + b * b) + a * c * s * t * b - b * b * s * s * r2);
                    C = u * u * (a * a + b * b) + 2 * a * c * s * u + c * c * s * s - b * b * s * s * r2 * r2;
                }
                else
                {
                    u = a1 * c - a * c1;
                    s = a * b1;
                    A = a * a * (t * t * a * a - s * s);
                    B = 2 * a * a * (u * t * a - s * s * r2);
                    C = u * u * a * a + c * c * s * s - a * a * s * s * r2 * r2;
                }

                // Calculate radius
                b24ac = (B * B) - (4 * A * C);
                if ((b24ac < 0.0) || (A == 0.0)) continue;
                b24acRoot = Math.Sqrt(b24ac);
                for (int iRoot = 0; iRoot < 2; ++iRoot)
                {
                    if ((b24ac == 0.0) && (iRoot > 0)) continue;
                    if (iRoot == 0) fRadius = (-B + b24acRoot) / (A + A);
                    else fRadius = (-B - b24acRoot) / (A + A);
                    if ((fRadius <= 0.0)) continue;

                    // Derive x-coordinate of centre
                    double[] xSols = new double[2];
                    int nxSols = 0;
                    if (x3 != 0.0)
                    {
                        xc = ((r2 + fRadius) * (r2 + fRadius) - (r3 + fRadius) * (r3 + fRadius) + x3 * x3) / (2 * x3);
                        //if (Math.Abs(xc) > (double)Int32.MaxValue) continue;
                        xSols[nxSols++] = xc;
                    }
                    else // If circles are concentric there are 2 solutions for x
                    {
                        Ac = (a1 * a1 + b1 * b1);
                        Bc = -2 * a1 * (fRadius * t - c1);
                        Cc = (fRadius * t - c1) * (fRadius * t - c1) - b1 * b1 * (r2 + fRadius) * (r2 + fRadius);
                        b24ac = Bc * Bc - 4 * Ac * Cc;
                        if ((b24ac < 0.0) || (Ac == 0.0)) continue;
                        b24acRoot = Math.Sqrt(b24ac);
                        for (int xCase = 0; xCase < 2; ++xCase)
                        {
                            // Calculate x-coordinate
                            if (xCase == 0) xc = (-Bc + b24acRoot) / (Ac + Ac);
                            else xc = (-Bc - b24acRoot) / (Ac + Ac);
                            //if (Math.Abs(xc) > (double)Int32.MaxValue) continue;
                            xSols[nxSols++] = xc;
                        }
                    }

                    // Now derive y-coordinate(s)
                    for (int ixSol = 0; ixSol < nxSols; ++ixSol)
                    {
                        xc = xSols[ixSol];
                        if (b1 != 0.0)
                        {
                            yc = (-a1 * xc - c1 + fRadius * t) / b1;
                        }
                        else
                        {
                            double ycSquare = (r2 + fRadius) * (r2 + fRadius) - (xc * xc);
                            if (ycSquare < 0.0) continue;
                            yc = Math.Sqrt(ycSquare);
                        }
                        //if (Math.Abs(yc) > (double)Int32.MaxValue) continue;

                        // Add solution
                        GeoPoint2D ptCentre = new GeoPoint2D(xc, yc);
                        CentersList.Add(ptCentre);
                        RadiiList.Add(fRadius);
                        if (b1 == 0.0)
                        {
                            ptCentre = new GeoPoint2D(xc, -yc);
                            CentersList.Add(ptCentre);
                            RadiiList.Add(fRadius);
                        }
                    }
                }
            }

            // Return number of solutions found
            fCentres = CentersList.ToArray();
            fRadii = RadiiList.ToArray();
        }
        /// <summary>
        /// Find circles tangent to three other circles
        /// </summary>
        /// <param name="center1">Centre of first circle</param>
        /// <param name="radius1">Radius of first circle</param>
        /// <param name="center2">Centre of second circle</param>
        /// <param name="radius2">Radius of second circle</param>
        /// <param name="center3">Centre of third circle</param>
        /// <param name="radius3">Radius of third circle</param>
        /// <param name="centers">Array to receive list of centre points</param>
        /// <param name="radii">Array to receive list of radii</param>
        /// <returns>Number of solutions</returns>
        private static void Circle3C(GeoPoint2D center1, double radius1, GeoPoint2D center2, double radius2,
                                GeoPoint2D center3, double radius3, out GeoPoint2D[] centers, out double[] radii)
        //*************************************************************************************
        // Find tangents to three circles
        {
            // Initialise
            List<GeoPoint2D> CentersList = new List<GeoPoint2D>();
            List<double> RadiiList = new List<double>();

            // If all circles concentric, there are no solutions
            if (Precision.IsEqual(center1, center2) || Precision.IsEqual(center1, center3) || Precision.IsEqual(center2, center3))
            {
                centers = new GeoPoint2D[0];
                radii = new double[0];
                return;
            }

            // Ensure first 2 circles are not concentric
            GeoPoint2D ptTemp = center2;
            center2 = center3;
            center3 = ptTemp;
            double nTemp = radius2;
            radius2 = radius3;
            radius3 = nTemp;

            // Translate so first circle is on origin
            GeoPoint2D center2n = new GeoPoint2D();
            center2n.x = center2.x - center1.x;
            center2n.y = center2.y - center1.y;
            GeoPoint2D center3n = new GeoPoint2D();
            center3n.x = center3.x - center1.x;
            center3n.y = center3.y - center1.y;

            // Find angle to rotate second circle to x-axis
            double ang = Math.Atan2(-center2n.y, center2n.x);
            double angCos = Math.Cos(ang);
            double angSin = Math.Sin(ang);

            // Rotate second and third circles
            double x2 = center2n.x * angCos - center2n.y * angSin;
            double x3 = center3n.x * angCos - center3n.y * angSin;
            double y3 = center3n.x * angSin + center3n.y * angCos;

            // Derive solutions
            GeoPoint2D[] fCenters;
            double[] fRadii;
            NormCircle3C(radius1, x2, radius2, x3, y3, radius3, out fCenters, out fRadii);

            // Transform solutions back to original coordinate system
            double xc, yc;
            GeoPoint2D ptCentre = new GeoPoint2D();
            double angNCos = Math.Cos(-ang);
            double angNSin = Math.Sin(-ang);
            for (int iSolution = 0; iSolution < fRadii.Length; ++iSolution)
            {
                xc = fCenters[iSolution].x;
                yc = fCenters[iSolution].y;
                ptCentre.x = (xc * angNCos - yc * angNSin) + center1.x;
                ptCentre.y = (xc * angNSin + yc * angNCos) + center1.y;
                CentersList.Add(ptCentre);
                RadiiList.Add(fRadii[iSolution]);
            }

            centers = CentersList.ToArray();
            radii = RadiiList.ToArray();
        }

        private static void NormCircle3C(double radius1, double x2, double radius2, double x3, double y3,
                                            double radius3, out GeoPoint2D[] centers, out double[] radii)
        //********************************************************************************
        // Find circles tangent to 3 circles. The first circle is centered at the origin, the
        // second at (x2, 0) and the third at (x3, y3)
        // NB x2 must not be zero. y3 must not be zero.
        {
            double r1, r2, r3, a, b, c, t, A, B, C, b24ac, b24acRoot;
            double fRadius, xc, yc;

            // Initialise
            List<GeoPoint2D> CentersList = new List<GeoPoint2D>();
            List<double> RadiiList = new List<double>();

            // Negate the radii to get all combinations
            for (int iCase = 0; iCase < 8; ++iCase)
            {
                r1 = ((iCase & 1) == 0) ? radius1 : -radius1;
                r2 = ((iCase & 2) == 0) ? radius2 : -radius2;
                r3 = ((iCase & 4) == 0) ? radius3 : -radius3;

                // Special case where radii of first 2 circles are equal
                if (r1 == r2)
                {
                    CoRadialSol(r1, x2, r2, x3, y3, r3, out centers, out radii);
                    CentersList.AddRange(centers);
                    RadiiList.AddRange(radii);
                    continue;
                }

                // Get constants
                a = 2 * (x2 * (r3 - r1) - x3 * (r2 - r1));
                b = 2 * y3 * (r1 - r2);
                c = (r2 - r1) * (x3 * x3 + y3 * y3 -
                      (r3 - r1) * (r3 - r1)) - (r3 - r1) * (x2 * x2 - (r2 - r1) * (r2 - r1));
                t = (x2 * x2 + r1 * r1 - r2 * r2) / 2.0;
                A = (r1 - r2) * (r1 - r2) * (a * a + b * b) - (x2 * x2 * b * b);
                B = 2 * (t * (r1 - r2) * (a * a + b * b) + a * c * x2 * (r1 - r2) - (r1 * x2 * x2 * b * b));
                C = t * t * (a * a + b * b) + (2 * a * c * x2 * t) + (c * c * x2 * x2) - (r1 * r1 * x2 * x2 * b * b);

                // Calculate radius
                b24ac = B * B - 4 * A * C;
                if ((b24ac > 0.0) && (A != 0.0))
                {
                    b24acRoot = Math.Sqrt(b24ac);
                    for (int iRoot = 0; iRoot < 2; ++iRoot)
                    {
                        if ((b24ac == 0.0) && (iRoot > 0)) continue;
                        if (iRoot == 0) fRadius = (-B + b24acRoot) / (A + A);
                        else fRadius = (-B - b24acRoot) / (A + A);
                        if ((fRadius <= 0.0)) continue;

                        // Derive x-coordinate of centre (x2 may not be zero)
                        xc = (fRadius * (r1 - r2) + t) / x2;
                        //if (Math.Abs(xc) > (double)Int32.MaxValue) continue;

                        // Derive y-coordinate of centre. b should never be 0, as  
                        // r1=r2 is special case and y3 may not be zero
                        yc = (-a * xc - c) / b;
                        //if (Math.Abs(yc) > (double)Int32.MaxValue) continue;

                        // Load results
                        GeoPoint2D ptCentre = new GeoPoint2D(xc, yc);
                        CentersList.Add(ptCentre);
                        RadiiList.Add(fRadius);
                    }
                }
            }
            centers = CentersList.ToArray();
            radii = RadiiList.ToArray();
        }

        private static void CoRadialSol(double r1, double x2, double r2, double x3, double y3,
              double r3, out GeoPoint2D[] fCentres, out double[] fRadii)
        //*******************************************************************************
        // Find circle tangent to 3 circles, where the first 2 circles have the same radius
        // The first circle is at (0,0), radius r1, the second at (x2, 0) 
        // and the third at (x3,y3), radius r3
        {
            double b24ac, b24acRoot, yc, fRadius, A, B, C;
            GeoPoint2D ptCentre;

            // Initialise
            List<GeoPoint2D> CentersList = new List<GeoPoint2D>();
            List<double> RadiiList = new List<double>();

            // Calculate x-cordinate of centre
            double xc = x2 / 2.0;

            // If all radii are equal, there will be only one solution
            if (r1 == r3)
            {
                if (y3 == 0.0)
                {
                    fCentres = new GeoPoint2D[0];
                    fRadii = new double[0];
                    return;
                }

                // Calculate y-coordinate of centre
                yc = (x3 * x3 - 2 * xc * x3 + y3 * y3) / (y3 + y3);
                //if (Math.Abs(yc) > (double)Int32.MaxValue) return;

                // Derive radius
                A = 1;
                B = 2 * r1;
                C = r1 * r1 - xc * xc - yc * yc;
                b24ac = B * B - 4 * A * C;
                if (b24ac < 0.0)
                {
                    fCentres = new GeoPoint2D[0];
                    fRadii = new double[0];
                    return;
                }
                b24acRoot = Math.Sqrt(b24ac);
                fRadius = (-B + b24acRoot) / (A + A);
                if ((fRadius <= 0.0))
                {
                    fRadius = (-B - b24acRoot) / (A + A);
                    if ((fRadius <= 0.0))
                    {
                        fCentres = new GeoPoint2D[0];
                        fRadii = new double[0];
                        return;
                    }
                }

                // Add solution
                ptCentre = new GeoPoint2D(xc, yc);
                CentersList.Add(ptCentre);
                RadiiList.Add(fRadius);
            }
            else
            {
                // Evaluate constants
                double k = r1 * r1 - r3 * r3 + x3 * x3 + y3 * y3 - 2 * xc * x3;
                A = 4 * ((r1 - r3) * (r1 - r3) - y3 * y3);
                B = 4 * (k * (r1 - r3) - 2 * y3 * y3 * r1);
                C = 4 * xc * xc * y3 * y3 + k * k - 4 * y3 * y3 * r1 * r1;
                b24ac = B * B - 4 * A * C;
                if ((b24ac >= 0.0) && (A != 0.0))
                {
                    // Calculate radii
                    b24acRoot = Math.Sqrt(b24ac);
                    for (int iRoot = 0; iRoot < 2; ++iRoot)
                    {
                        if ((b24ac == 0.0) && (iRoot > 0)) continue;
                        if (iRoot == 0) fRadius = (-B + b24acRoot) / (A + A);
                        else fRadius = (-B - b24acRoot) / (A + A);
                        if ((fRadius <= 0.0)) continue;

                        // Evaluate y-coordinate
                        yc = (2 * fRadius * (r1 - r3) + k) / (2 * y3);

                        // Add solution 
                        ptCentre = new GeoPoint2D(xc, yc);
                        CentersList.Add(ptCentre);
                        RadiiList.Add(fRadius);
                    }
                }
            }
            fCentres = CentersList.ToArray();
            fRadii = RadiiList.ToArray();
        }
        #endregion
    }



}
