using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;

namespace CADability.Shapes
{
    /* NEW CONCEPT (not yet implemented, 20.7.15)
     * --- this concerns simple borders, not SimpleShapes, i.e., there are no holes, everything is oriented counterclockwise ---
     *
     * Create a quadtree over both borders.
     * Subdivide the quadtree so far that each list contains only one intersection point. If intersection points are closer than "precision,"
     * then treat them as one intersection point.
     * Each square (i.e., leaf of the quadtree) has, with respect to a border, only 2 intersection points, or it is entirely inside or entirely outside.
     * These intersection points must be determined (parameters on the square's side and parameters on the border).
     * Depending on the boolean operation, it should now be very simple: each square yields one interval (border1/border2, from-to) or 2 intervals if an
     * intersection point is contained. (If no intersection point is contained, then the square should only contain one border.
     * (Problems are self-intersections/contacts: if all 4 corner points are inside, then the interval can be ignored.)
     * Segments that are identical are treated like intersection points, with a special identifier.
     * Finally, the intervals are collected. Connected pieces of a border are simply joinable. Here, multiple outlines and even holes can arise.
     * The directions are also clear: during union operations, the directions remain preserved; during a difference,
     * the direction of the right side is reversed.
     *
     * */

    internal class BorderOperation
    {
        private struct PointPosition : IComparable
        {
            public PointPosition(double par, GeoPoint2D point, double oppositePar, int id, double cross)
            {
                this.id = id;
                this.par = par;
                this.point = point;
                this.index = -1;
                this.oppositePar = oppositePar;
                this.direction = Direction.Unknown;
                this.used = false;
                this.cross = cross;
            }
            public int id; // two with the same ID belong to the same intersection point
            public double par; // Position parameter on the border ranges from 0 to the number of segments, integer at corners
            public GeoPoint2D point; // the point itself
            public double oppositePar; // Position parameter on the other border (only if on the edge)
            public int index; // the index in the other list
            public bool used; // already used, do not use anymore
            public enum Direction { Entering, Leaving, Crossing, Ambigous, Ignore, Unknown } // Crossing is for open borders
            public Direction direction;
            public double cross;
            public PointPosition Decremented()
            {
                PointPosition res = new PointPosition();
                res.id = id;
                res.par = par;
                res.point = point;
                res.oppositePar = oppositePar;
                res.index = index - 1;
                res.used = used;
                res.direction = direction;
                return res;
            }
            #region IComparable Members
            public int CompareTo(object obj)
            {
                PointPosition ct = (PointPosition)obj;
                return par.CompareTo(ct.par);
            }
            #endregion
        }
        private double precision; // small value, points with an even smaller distance are considered identical
        private BoundingRect extent; // common extent of both borders
        private bool intersect;
        private Border border1; // first operand
        private Border border2; // second operand
        private PointPosition[] border1Points; // list of relevant points on B1
        private PointPosition[] border2Points; // list of relevant points on B2
        private void Refine(List<PointPosition> points, Border pointsborder, Border other)
        {
            // remove duplicates. The question of precision arises here
            // initially implemented with a fixed value of 1e-6
            // additional effort if end and beginning are identical
            // omitted, as it is handled before the call
            //for (int i=points.Count-1; i>0; --i)
            //{
            //    if (Math.Abs((points[i - 1]).par - (points[i]).par) < 1e-6 ||
            //        Math.Abs(pointsborder.Count - Math.Abs((points[i - 1]).par - (points[i]).par)) < 1e-6)
            //    {
            //        if (Math.Abs((points[i - 1]).oppositePar - (points[i]).oppositePar) < 1e-6 ||
            //            Math.Abs(other.Count - Math.Abs((points[i - 1]).oppositePar - (points[i]).oppositePar)) < 1e-6)
            //        {
            //            points.RemoveAt(i - 1);
            //        }
            //    }
            //}
            // still check the last and the first
            //int last = points.Count - 1;
            //if (last > 0)
            //{
            //    if (Math.Abs((points[last]).par - (points[0]).par) < 1e-6 ||
            //        Math.Abs(pointsborder.Count - Math.Abs((points[last]).par - (points[0]).par)) < 1e-6)
            //    {
            //        if (Math.Abs((points[last]).oppositePar - (points[0]).oppositePar) < 1e-6 ||
            //            Math.Abs(other.Count - Math.Abs((points[last]).oppositePar - (points[0]).oppositePar)) < 1e-6)
            //        {
            //            points.RemoveAt(last);
            //        }
            //    }
            //}
            PointPosition[] pointsa = (PointPosition[])points.ToArray();
            // the points in pointsa are characterized with respect to 
            // their behavior concerning the other curve: does the 
            // respective border enter the other, or leave it.
            // Problem case: contact over a segment: initially, the entry point 
            // in a contact is set to Ignore. 
            // In the subsequent loop, the ignore spreads backward if there are 
            // the same values before and after.
            // It is likely that there will be a problem if two borders 
            // share a common segment but still have an actual intersection.
            // Such a case needs to be constructed. This is because we always 
            // consider the exit point, and the two point lists are traversed 
            // in different directions, so the points won't match. The midpoint 
            // should be used in such cases.
            // Problem case: contact with only a single corner point is eliminated.
            if (pointsa.Length > 1)
            {
                double par = (pointsa[pointsa.Length - 1].par + pointsborder.Count + pointsa[0].par) / 2.0;
                if (par > pointsborder.Count) par -= pointsborder.Count;
                Border.Position lastpos = other.GetPosition(pointsborder.PointAt(par), precision);
                if (lastpos == Border.Position.OnCurve)
                {
                    double par1 = pointsa[pointsa.Length - 1].par + 0.324625 * (pointsa[0].par + pointsborder.Count - pointsa[pointsa.Length - 1].par);
                    double par2 = pointsa[pointsa.Length - 1].par + 0.689382 * (pointsa[0].par + pointsborder.Count - pointsa[pointsa.Length - 1].par);
                    if (par1 > pointsborder.Count) par1 -= pointsborder.Count;
                    if (par2 > pointsborder.Count) par2 -= pointsborder.Count;
                    Border.Position newpos1 = other.GetPosition(pointsborder.PointAt(par1), precision);
                    Border.Position newpos2 = other.GetPosition(pointsborder.PointAt(par2), precision);
                    if (newpos1 != lastpos) lastpos = newpos1;
                    else if (newpos2 != lastpos) lastpos = newpos2;
                }
                Border.Position firstpos = lastpos;

                for (int i = 0; i < pointsa.Length; ++i)
                {
                    Border.Position newpos;
                    if (i < pointsa.Length - 1)
                    {
                        par = (pointsa[i].par + pointsa[i + 1].par) / 2.0;
                        newpos = other.GetPosition(pointsborder.PointAt(par), precision);
                        if (newpos == Border.Position.OnCurve)
                        {   // for safety, check two additional points. If one is not OnCurve, take that one. 
                            // Exactly the midpoint can produce an artifact
                            double par1 = pointsa[i].par + 0.324625 * (pointsa[i + 1].par - pointsa[i].par);
                            double par2 = pointsa[i].par + 0.689382 * (pointsa[i + 1].par - pointsa[i].par);
                            Border.Position newpos1 = other.GetPosition(pointsborder.PointAt(par1), precision);
                            Border.Position newpos2 = other.GetPosition(pointsborder.PointAt(par2), precision);
                            if (newpos1 != newpos) newpos = newpos1;
                            else if (newpos2 != newpos) newpos = newpos2;
                        }
                    }
                    else
                    {
                        newpos = firstpos;
                    }
                    // newly introduced: position determination based on the direction of the intersection
                    // epsilon is still arbitrary
                    if (pointsa[i].cross > 0.01 && other.IsClosed)
                    {
                        pointsa[i].direction = PointPosition.Direction.Leaving;
                    }
                    else if (pointsa[i].cross < -0.01 && other.IsClosed)
                    {
                        pointsa[i].direction = PointPosition.Direction.Entering;
                    }
                    else if (newpos == Border.Position.OnCurve && lastpos == Border.Position.OnCurve)
                    {
                        pointsa[i].direction = PointPosition.Direction.Ignore;
                    }
                    else if (newpos == Border.Position.OpenBorder)
                    {
                        pointsa[i].direction = PointPosition.Direction.Crossing;
                    }
                    else if (newpos == lastpos)
                    {
                        pointsa[i].direction = PointPosition.Direction.Ignore;
                    }
                    else if (newpos == Border.Position.OnCurve)
                    {
                        // In hatching with islands, perfectly fitting puzzle pieces are often subtracted
                        // that match in individual sections. These must not be
                        // considered as belonging. See, for example, Schraffur2.cdb
                        // pointsa[i].direction = PointPosition.Direction.Ignore;
                        if (lastpos == Border.Position.Outside)
                            pointsa[i].direction = PointPosition.Direction.Entering;
                        else
                            pointsa[i].direction = PointPosition.Direction.Leaving;
                    }
                    else if (newpos == Border.Position.Inside)
                    {
                        pointsa[i].direction = PointPosition.Direction.Entering;
                    }
                    else
                    {
                        pointsa[i].direction = PointPosition.Direction.Leaving;
                    }
                    lastpos = newpos;
                }
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int i = 0; i < pointsa.Length; ++i)
                {
                    dc.Add(pointsa[i].point, System.Drawing.Color.DarkBlue, pointsa[i].direction.ToString());
                }
#endif
                // in the following loop, two identical ones, separated only by ignore, are reduced to one
                if (pointsborder != border1) // for testing purposes, limited to the 2nd pass only
                {
                    bool ignoring;
                    do
                    {
                        ignoring = false;
                        PointPosition.Direction lastdir = PointPosition.Direction.Ignore;
                        // use the last value as the starting value:
                        for (int i = pointsa.Length - 1; i >= 0; --i)
                        {
                            if (pointsa[i].direction != PointPosition.Direction.Ignore)
                            {
                                lastdir = pointsa[i].direction;
                                break;
                            }
                        }
                        for (int i = 0; i < pointsa.Length; ++i)
                        {
                            if (lastdir != PointPosition.Direction.Ignore && pointsa[i].direction == PointPosition.Direction.Ignore)
                            {
                                int next = i + 1;
                                if (next >= pointsa.Length) next = 0;
                                if (pointsa[next].direction != PointPosition.Direction.Ignore)
                                {
                                    if (pointsa[next].direction == lastdir)
                                    {
                                        pointsa[next].direction = PointPosition.Direction.Ignore;
                                        ignoring = true;
                                    }
                                }
                            }
                            if (pointsa[i].direction != PointPosition.Direction.Ignore)
                            {
                                lastdir = pointsa[i].direction;
                            }
                        }
                    } while (ignoring);
                }
#if DEBUG
                DebuggerContainer dc1 = new DebuggerContainer();
                for (int i = 0; i < pointsa.Length; ++i)
                {
                    dc1.Add(pointsa[i].point, System.Drawing.Color.DarkBlue, pointsa[i].direction.ToString());
                }
#endif
                points.Clear();
                for (int i = 0; i < pointsa.Length; ++i)
                {
                    if (pointsa[i].direction == PointPosition.Direction.Leaving ||
                        pointsa[i].direction == PointPosition.Direction.Entering ||
                        pointsa[i].direction == PointPosition.Direction.Crossing)
                    {
                        int iminus1 = i - 1;
                        if (iminus1 < 0) iminus1 = pointsa.Length - 1;
                        // only take those where there is really a change
                        if ((pointsa[iminus1].direction != pointsa[i].direction) ||
                            (pointsa[iminus1].direction == PointPosition.Direction.Crossing && pointsa[i].direction == PointPosition.Direction.Crossing))
                        {
                            points.Add(pointsa[i]);
                        }
                    }
                }
#if DEBUG
                DebuggerContainer dc2 = new DebuggerContainer();
                for (int i = 0; i < points.Count; ++i)
                {
                    dc2.Add(points[i].point, System.Drawing.Color.DarkBlue, points[i].direction.ToString());
                }
#endif
            }
            else
            {
                points.Clear(); // only a single point, which doesn't count anyway, right?
                intersect = false; // in any case, no known instance where something like this occurs
            }
        }
        private void GenerateClusterSet()
        {
            // first, populate the two lists with the corner points. why? abolished!
            List<PointPosition> b1p = new List<PointPosition>();
            List<PointPosition> b2p = new List<PointPosition>();
#if DEBUG
            debugb1p = b1p;
            debugb2p = b2p;
#endif
            // why should we include the corner points???
            //			for (int i=0; i<border1.Count; ++i)
            //			{
            //				GeoPoint2D p = border1[i].StartPoint;
            //				b1p.Add(new PointPosition(i,p,border2.GetPosition(p,precision)));
            //			}
            //			for (int i=0; i<border2.Count; ++i)
            //			{
            //				GeoPoint2D p = border2[i].StartPoint;
            //				b2p.Add(new PointPosition(i,p,border1.GetPosition(p,precision)));
            //			}
            // now add the intersection points:
            // it would make sense to iterate over the border with fewer segments
            intersect = false;
            GeoPoint2DWithParameter[] isp = border1.GetIntersectionPoints(border2, precision);
            for (int j = 0; j < isp.Length; ++j)
            {
                double dirz;
                try
                {
                    GeoVector2D dir1 = border1.DirectionAt(isp[j].par1).Normalized;
                    GeoVector2D dir2 = border2.DirectionAt(isp[j].par2).Normalized;
                    dirz = dir1.x * dir2.y - dir1.y * dir2.x;
                }
                catch (GeoVectorException)
                {
                    dirz = 0.0;
                }
                // At the corner point, dirz is not meaningful, so set it to 0.0, then another mechanism takes over
                if (Math.Abs(Math.Round(isp[j].par2) - isp[j].par2) < 1e-7) dirz = 0.0;
                if (Math.Abs(Math.Round(isp[j].par1) - isp[j].par1) < 1e-7) dirz = 0.0;
                b1p.Add(new PointPosition(isp[j].par1, isp[j].p, isp[j].par2, j, dirz));
                b2p.Add(new PointPosition(isp[j].par2, isp[j].p, isp[j].par1, j, -dirz));
                intersect = true;
            }
            b1p.Sort(); // sorted by ascending parameter
            b2p.Sort();
            // remove points that are too close to each other, and do so equally from both lists
            // it should actually be sufficient to iterate over one list
            for (int i = b1p.Count - 1; i > 0; --i)
            {
                if (Math.Abs((b1p[i - 1]).par - (b1p[i]).par) < 1e-6 ||
                    Math.Abs(border1.Count - Math.Abs((b1p[i - 1]).par - (b1p[i]).par)) < 1e-6)
                {
                    if (Math.Abs((b1p[i - 1]).oppositePar - (b1p[i]).oppositePar) < 1e-6 ||
                        Math.Abs(border2.Count - Math.Abs((b1p[i - 1]).oppositePar - (b1p[i]).oppositePar)) < 1e-6)
                    {
                        int id = b1p[i - 1].id;
                        b1p.RemoveAt(i - 1);
                        for (int j = 0; j < b2p.Count; j++)
                        {
                            if (b2p[j].id == id)
                            {
                                b2p.RemoveAt(j);
                                break;
                            }
                        }
                    }
                }
            }
            // and check again at the end
            int last = b1p.Count - 1;
            if (last > 0)
            {
                if (Math.Abs((b1p[last]).par - (b1p[0]).par) < 1e-6 ||
                    Math.Abs(border1.Count - Math.Abs((b1p[last]).par - (b1p[0]).par)) < 1e-6)
                {
                    if (Math.Abs((b1p[last]).oppositePar - (b1p[0]).oppositePar) < 1e-6 ||
                        Math.Abs(border2.Count - Math.Abs((b1p[last]).oppositePar - (b1p[0]).oppositePar)) < 1e-6)
                    {
                        int id = b1p[last].id;
                        b1p.RemoveAt(last);
                        for (int j = 0; j < b2p.Count; j++)
                        {
                            if (b2p[j].id == id)
                            {
                                b2p.RemoveAt(j);
                                break;
                            }
                        }
                    }
                }
            }

            Refine(b1p, border1, border2);
            // All entries removed from b1 must also be removed from b2.
            // Previously, this was done after Refine(b2p...), but that had the following drawback:
            // For example, two horizontally slightly offset horizontal rectangles have 4 intersection points,
            // where there are two Entering and two Leaving points each.
            // During the first Refine, only the adjacent pair Entering->Leaving is retained.
            // However, in the point list b2p, these do not appear consecutively, so the other pair is retained there,
            // which means that, in total, nothing is left. Therefore, remove them right away.
            for (int i = b2p.Count - 1; i >= 0; --i)
            {
                bool ok = false;
                for (int j = 0; j < b1p.Count; ++j)
                {
                    if (b1p[j].id == b2p[i].id)
                    {
                        ok = true;
                        break;
                    }
                }
                if (!ok) b2p.RemoveAt(i);
            }
            Refine(b2p, border2, border1);
            for (int i = 0; i < b1p.Count; ++i)
            {
                for (int j = 0; j < b2p.Count; ++j)
                {   // Changed the following comparison from == to <1e-10 because very small differences occur.
                    // The differences arise because nearly identical items were removed,
                    // but not the same ones were removed from both lists.
                    // Here, it should now be sufficient to check by id, right?
                    //if ((Math.Abs((b1p[i]).par-(b2p[j]).oppositePar)<1e-10) &&
                    //    (Math.Abs((b1p[i]).oppositePar-(b2p[j]).par)<1e-10))
                    if (b1p[i].id == b2p[j].id)
                    {
                        // since PointPosition is a struct, it's cumbersome here:
                        PointPosition pp1 = b1p[i];
                        PointPosition pp2 = b2p[j];
                        pp1.index = j;
                        pp2.index = i;
                        b1p[i] = pp1;
                        b2p[j] = pp2;
                    }
                }
                if (b1p[i].index == -1)
                {
                    b1p.RemoveAt(i); // no partner found, remove it
                    --i;
                }
            }

            // Consider the following special case:
            // If there are two Entering points and their corresponding two Leaving points in a list,
            // then delete one of the pairs because it represents a shared contour segment.
            bool removed = true;
            while (removed)
            {
                removed = false;
                for (int i = 1; i < b1p.Count; i++)
                {
                    if (b1p[i - 1].direction == b1p[i].direction)
                    {
                        int i1 = b1p[i - 1].index;
                        int i2 = b1p[i].index;
                        if (Math.Abs(i2 - i1) == 1 && b2p[i1].direction == b2p[i2].direction)
                        {
                            // Condition met: remove i from list b1p
                            int r1 = i;
                            int r2 = b1p[i].index;
                            b2p.RemoveAt(r2);
                            b1p.RemoveAt(r1);
                            for (int j = 0; j < b1p.Count; j++)
                            {
                                if (b1p[j].index > r2) b1p[j] = b1p[j].Decremented(); // because of the annoying struct
                            }
                            for (int j = 0; j < b2p.Count; j++)
                            {
                                if (b2p[j].index > r1) b2p[j] = b2p[j].Decremented();
                            }
                            removed = true;
                            break;
                        }
                    }
                }
            }

            // actually, there should no longer be any entries with index == -1
            // and removing them is a bit tricky because the indices have already been assigned
            // but it would be possible. For now, just throw an exception:
            for (int i = 0; i < b1p.Count; ++i)
            {
                if ((b1p[i]).index == -1) throw new BorderException("internal error in GenerateClusterSet", BorderException.BorderExceptionType.InternalError);
            }
            for (int i = 0; i < b2p.Count; ++i)
            {
                if ((b2p[i]).index == -1) throw new BorderException("internal error in GenerateClusterSet", BorderException.BorderExceptionType.InternalError);
            }
            border1Points = b1p.ToArray();
            border2Points = b2p.ToArray();
            borderPosition = BorderPosition.unknown;
        }
        public BorderOperation(Border b1, Border b2)
        {
            border1 = b1;
            border2 = b2;
            extent = border1.Extent;
            extent.MinMax(border2.Extent);
            precision = (extent.Width + extent.Height) * 1e-6; // 1e-8 was too strict!
            try
            {
                GenerateClusterSet();
            }
            catch (BorderException)
            {
                intersect = false;
                border1 = null;
                border2 = null;
            }
        }
        public BorderOperation(Border b1, Border b2, double prec)
        {
            border1 = b1;
            border2 = b2;
            extent = border1.Extent;
            extent.MinMax(border2.Extent);
            precision = prec;
            try
            {
                GenerateClusterSet();
            }
            catch (BorderException)
            {
                intersect = false;
                border1 = null;
                border2 = null;
            }
        }
        private int FindNextPoint(bool onB1, int startHere, PointPosition.Direction searchFor, bool forward)
        {
            PointPosition[] borderPoints;
            if (onB1) borderPoints = border1Points;
            else borderPoints = border2Points;
            if (borderPoints.Length == 0) return -1;
            if (startHere < 0) startHere = 0;
            if (startHere >= borderPoints.Length) startHere = borderPoints.Length - 1;
            int ind = startHere;
            int lind = ind - 1;
            if (!forward) lind = ind + 1;
            if (lind < 0) lind = borderPoints.Length - 1;
            if (lind >= borderPoints.Length) lind = 0;
            while (true)
            {
                if (borderPoints[ind].direction == searchFor && !borderPoints[ind].used)
                {
                    borderPoints[ind].used = true;
                    return ind;
                }
                lind = ind;
                if (forward) ++ind;
                else --ind;
                if (ind >= borderPoints.Length) ind = 0;
                if (ind < 0) ind = borderPoints.Length - 1;
                if (ind == startHere) break;
            }
            return -1;
        }
        public CompoundShape Union()
        {
            switch (this.Position)
            {
                case BorderPosition.b1coversb2:
                    return new CompoundShape(new SimpleShape(border1));
                case BorderPosition.b2coversb1:
                    return new CompoundShape(new SimpleShape(border2));
                case BorderPosition.disjunct:
                    return new CompoundShape(new SimpleShape(border1), new SimpleShape(border2));
                case BorderPosition.intersecting:
                    // The most difficult case, here there can be a shell and multiple holes
                    List<Border> bdrs = new List<Border>();
                    int startb1 = -1;
                    int ind;
                    List<ICurve2D> segments = new List<ICurve2D>();
#if DEBUG
                    //DebuggerContainer dc = new DebuggerContainer();
#endif
                    while ((ind = FindNextPoint(true, startb1, PointPosition.Direction.Leaving, true)) >= 0)
                    {
                        int ind1 = FindNextPoint(true, ind, PointPosition.Direction.Entering, true);
                        if (ind1 >= 0)
                        {
                            segments.AddRange(border1.GetPart(border1Points[ind].par, border1Points[ind1].par, true));
#if DEBUG
                            //dc.toShow.Clear();
                            //dc.Add(segments.ToArray());
#endif
                            int ind2 = FindNextPoint(false, border1Points[ind1].index, PointPosition.Direction.Entering, true);
                            segments.AddRange(border2.GetPart(border2Points[border1Points[ind1].index].par, border2Points[ind2].par, true));
#if DEBUG
                            //dc.toShow.Clear();
                            //dc.Add(segments.ToArray());
#endif
                            startb1 = border2Points[ind2].index; // index to b1
                        }
                        if (segments.Count > 0)
                        {
                            GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                            GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                            if ((sp | ep) < precision)
                            {
                                Border bdr = new Border(segments.ToArray(), true);
                                // Remove identical forward and backward curves
                                if (bdr.ReduceDeadEnd(precision * 10))
                                {
                                    bdr.RemoveSmallSegments(precision);
                                    bdrs.Add(bdr);
                                }
                                segments.Clear();
                                startb1 = -1;
                            }
                        }
                    }
                    if (bdrs.Count > 0)
                    {
                        // Here, a ring may emerge that consists only of a single border
                        // This still needs to be checked additionally

                        bdrs.Sort(new BorderAreaComparer());
                        Border outline = bdrs[bdrs.Count - 1] as Border;
                        double[] si = outline.GetSelfIntersection(precision);
                        if (si.Length >= 3) // Two intersection points, like a "C" that touches at the opening, so it is essentially an "O"
                        {
                            // Cut the outline into pieces and eliminate counter-oriented parts
                            List<double> parms = new List<double>();
                            for (int i = 0; i < si.Length; i += 3)
                            {
                                parms.Add(si[i]);
                                parms.Add(si[i + 1]);
                            }
                            parms.Sort();
                            List<Path2D> parts = new List<Path2D>();
                            for (int i = 0; i < parms.Count; i++)
                            {
                                parts.Add(new Path2D(outline.GetPart(parms[i], parms[(i + 1) % parms.Count], true)));
                            }
#if DEBUG
                            GeoObjectList dbgl1 = new GeoObjectList();
                            for (int i = 0; i < parts.Count; i++)
                            {
                                dbgl1.Add(parts[i].MakeGeoObject(Plane.XYPlane));
                            }
#endif
                            // `parts` now contains the sliced-up outline. Search for and remove identical sections with opposing directions.
                            bool removed = true;
                            while (removed)
                            {
                                removed = false;
                                for (int i = 0; i < parts.Count; i++)
                                {
                                    if (parts[i].SubCurvesCount==0)
                                    {
                                        parts.RemoveAt(i);
                                        removed = true;
                                        break;
                                    }
                                    Path2D part1 = parts[i].CloneReverse(true) as Path2D;
                                    if (part1.IsClosed)
                                    {
                                        // a self-returning piece?
                                        Border testEmpty = new Border(part1);
                                        if (testEmpty.Area < precision)
                                        {
                                            parts.RemoveAt(i);
                                            removed = true;
                                            break;
                                        }
                                    }
                                    for (int j = i + 1; j < parts.Count; j++)
                                    {
                                        if (part1.GeometricalEqual(precision, parts[j]))
                                        {
                                            parts.RemoveAt(j);
                                            parts.RemoveAt(i);
                                            removed = true;
                                            break;
                                        }
                                    }
                                    if (removed) break;
                                }
                            }
#if DEBUG
                            GeoObjectList dbgl2 = new GeoObjectList();
                            for (int i = 0; i < parts.Count; i++)
                            {
                                dbgl2.Add(parts[i].MakeGeoObject(Plane.XYPlane));
                            }
#endif
                            CompoundShape tmp = CompoundShape.CreateFromList(parts.ToArray(), precision);
                            if (tmp != null && tmp.SimpleShapes.Length == 1)
                            {
                                bdrs.RemoveAt(bdrs.Count - 1); // old outline removed
                                for (int i = 0; i < tmp.SimpleShapes[0].NumHoles; i++)
                                {
                                    bdrs.Add(tmp.SimpleShapes[0].Hole(i)); // Add holes
                                }
                                outline = tmp.SimpleShapes[0].Outline;
                                bdrs.Add(tmp.SimpleShapes[0].Outline); // new outline added, will be removed again shortly
                            }
                        }
                        else if (si.Length == 3)
                        {
                            // A single inner intersection point, this could be a "spike" in the outline
                        }
                        bdrs.RemoveAt(bdrs.Count - 1); // remove outline
                        return new CompoundShape(new SimpleShape(outline, bdrs.ToArray()));
                    }
                    break;
            }
            return new CompoundShape(new SimpleShape(border1), new SimpleShape(border2));
            // throw new BorderException("unexpected error in BorderOperation.Union!", BorderException.BorderExceptionType.InternalError);
        }
        public CompoundShape Intersection()
        {
            switch (this.Position)
            {
                case BorderPosition.b1coversb2:
                    return new CompoundShape(new SimpleShape(border2));
                case BorderPosition.b2coversb1:
                    return new CompoundShape(new SimpleShape(border1));
                case BorderPosition.disjunct:
                    return new CompoundShape(); // empty!
                case BorderPosition.intersecting:
                    // The most difficult case, here there can be multiple shells but no holes
                    ArrayList bdrs = new ArrayList();
                    int startb1 = -1;
                    int ind;
                    ArrayList segments = new ArrayList();
                    while ((ind = FindNextPoint(true, startb1, PointPosition.Direction.Entering, true)) >= 0)
                    {
                        int ind1 = FindNextPoint(true, ind, PointPosition.Direction.Leaving, true);
                        if (ind1 >= 0)
                        {
                            segments.AddRange(border1.GetPart(border1Points[ind].par, border1Points[ind1].par, true));
                            int ind2 = FindNextPoint(false, border1Points[ind1].index, PointPosition.Direction.Leaving, true);
                            segments.AddRange(border2.GetPart(border2Points[border1Points[ind1].index].par, border2Points[ind2].par, true));
                            startb1 = border2Points[ind2].index; // index to b1
                        }
                        if (segments.Count > 0)
                        {
                            GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                            GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                            //if (Precision.IsEqual(sp, ep))
                            if (this.precision == 0) { this.precision = Precision.eps; }
                            if ((sp | ep) < this.precision)
                            {
                                bdrs.Add(new Border((ICurve2D[])segments.ToArray(typeof(ICurve2D))));
                                segments.Clear();
                                startb1 = -1;
                            }
                        }
                    }
                    if (bdrs.Count > 0)
                    {
                        SimpleShape[] ss = new SimpleShape[bdrs.Count];
                        for (int i = 0; i < bdrs.Count; ++i)
                        {
                            ss[i] = new SimpleShape(bdrs[i] as Border);
                        }
                        return new CompoundShape(ss);
                    }
                    // They overlap (presumably touch), but the content is empty, so return empty
                    return new CompoundShape();
            }
            throw new BorderException("unexpected error in BorderOperation.Intersection!", BorderException.BorderExceptionType.InternalError);
        }
        public CompoundShape Difference()
        {
            switch (this.Position)
            {
                case BorderPosition.b1coversb2:
                    return new CompoundShape(new SimpleShape(border1, border2));
                case BorderPosition.b2coversb1:
                    // Everything is removed, so it’s empty. Why was "b2-b1" written here before???
                    return new CompoundShape(); // empty!
                // return new CompoundShape(new SimpleShape(border2, border1));
                case BorderPosition.disjunct:
                    // disjoint: then it must be border1, right? previously this was left empty
                    return new CompoundShape(new SimpleShape(border1));
                // return new CompoundShape(); // empty!
                case BorderPosition.intersecting:
                    // the most difficult case, here there can be multiple outlines but no holes
                    ArrayList bdrs = new ArrayList();
                    int startb1 = -1;
                    int ind;
                    bool found = false;
                    ArrayList segments = new ArrayList();
                    while ((ind = FindNextPoint(true, startb1, PointPosition.Direction.Leaving, true)) >= 0)
                    {
                        int ind1 = FindNextPoint(true, ind, PointPosition.Direction.Entering, true);
                        if (ind1 >= 0)
                        {
                            segments.AddRange(border1.GetPart(border1Points[ind].par, border1Points[ind1].par, true));
                            int ind2 = FindNextPoint(false, border1Points[ind1].index, PointPosition.Direction.Entering, false);
                            segments.AddRange(border2.GetPart(border2Points[border1Points[ind1].index].par, border2Points[ind2].par, false));
                            startb1 = border2Points[ind2].index; // index to b1
                        }
                        if (segments.Count > 0)
                        {
                            GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                            GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                            //if (Precision.IsEqual(sp, ep))
                            if ((sp | ep) < this.precision)
                            {
                                Border bdr = new Border((ICurve2D[])segments.ToArray(typeof(ICurve2D)), true);
                                if (bdr.ReduceDeadEnd(precision * 10))
                                {
                                    bdr.RemoveSmallSegments(precision);
                                    bdrs.Add(bdr);
                                }
                                found = true;
                                segments.Clear();
                                startb1 = -1;
                            }
                        }
                    }
                    if (bdrs.Count > 0)
                    {
                        SimpleShape[] ss = new SimpleShape[bdrs.Count];
                        for (int i = 0; i < bdrs.Count; ++i)
                        {
                            ss[i] = new SimpleShape(bdrs[i] as Border);
                        }
                        return new CompoundShape(ss);
                    }
                    // they intersect, but the content is empty, so return border1
                    if (found) 
                        return new CompoundShape();
                    
                    return new CompoundShape(new SimpleShape(border1));

            }
            throw new BorderException("unexpected error in BorderOperation.Intersection!", BorderException.BorderExceptionType.InternalError);
        }
        public CompoundShape Split()
        {
            if (!intersect || border1Points.Length == 0 || border2Points.Length == 0)
            {
                return new CompoundShape(new SimpleShape(border1)); // no intersection
            }
            // The second border is open and splits the first one
            ArrayList bdrs = new ArrayList();
            int startb2 = -1;
            int ind;
            ArrayList segments = new ArrayList();
            // first search in the direction of border2
            // We always move forward on border1
            while ((ind = FindNextPoint(false, startb2, PointPosition.Direction.Entering, true)) >= 0)
            {
                int ind1 = FindNextPoint(false, ind, PointPosition.Direction.Leaving, true);
                if (ind1 >= 0)
                {
                    segments.AddRange(border2.GetPart(border2Points[ind].par, border2Points[ind1].par, true));
                    // On Border 1, all points are actually "Crossing"; what about contacts (touch points)?
                    // But the point at "border2Points[ind1].index" must not be found again, hence the "+1"
                    int nextInd = (border2Points[ind1].index + 1);
                    if (nextInd >= border1Points.Length) nextInd = 0;
                    int ind2 = FindNextPoint(true, nextInd, PointPosition.Direction.Crossing, true);
                    if (ind2 >= 0)
                    {
                        segments.AddRange(border1.GetPart(border1Points[border2Points[ind1].index].par, border1Points[ind2].par, true));
                        startb2 = border1Points[ind2].index;
                    }
                    else
                    {

                    }
                }
                if (segments.Count > 0)
                {
                    GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                    GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                    //if (Precision.IsEqual(sp, ep))
                    if ((sp | ep) < this.precision)
                    {
                        bdrs.Add(new Border((ICurve2D[])segments.ToArray(typeof(ICurve2D)), true));
                        segments.Clear();
                        startb2 = -1;
                    }
                }
            }

            // now search in the opposite direction of border2.
            // but first, release all points again
            for (int i = 0; i < border1Points.Length; i++)
            {
                border1Points[i].used = false;
            }
            for (int i = 0; i < border2Points.Length; i++)
            {
                border2Points[i].used = false;
            }
            while ((ind = FindNextPoint(false, startb2, PointPosition.Direction.Leaving, false)) >= 0)
            {
                int ind1 = FindNextPoint(false, ind, PointPosition.Direction.Entering, false);
                if (ind1 >= 0)
                {
                    segments.AddRange(border2.GetPart(border2Points[ind].par, border2Points[ind1].par, false));
                    // On Border 1, all points are actually "Crossing"; what about touch points?
                    // However, the point at border2Points[ind1].index must not be found again, hence the "+1".
                    int nextInd = (border2Points[ind1].index + 1);
                    if (nextInd >= border1Points.Length) nextInd = 0;
                    int ind2 = FindNextPoint(true, nextInd, PointPosition.Direction.Crossing, true);
                    if (ind2 >= 0)
                    {
                        segments.AddRange(border1.GetPart(border1Points[border2Points[ind1].index].par, border1Points[ind2].par, true));
                        startb2 = border1Points[ind2].index;
                    }
                    else
                    {

                    }
                }
                if (segments.Count > 0)
                {
                    GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                    GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                    //if (Precision.IsEqual(sp, ep))
                    if ((sp | ep) < this.precision)
                    {
                        bdrs.Add(new Border((ICurve2D[])segments.ToArray(typeof(ICurve2D)), true));
                        segments.Clear();
                        startb2 = -1;
                    }
                }
            }
            if (bdrs.Count > 1) // If Split only returns one border, something went wrong (changed on 2.11.15)
            {
                SimpleShape[] ss = new SimpleShape[bdrs.Count];
                for (int i = 0; i < bdrs.Count; i++)
                {
                    ss[i] = new SimpleShape(bdrs[i] as Border);
                }
                return new CompoundShape(ss);
            }
            else
            {
                return new CompoundShape(new SimpleShape(border1)); // no intersection
            }
        }
        public enum BorderPosition { disjunct, intersecting, b1coversb2, b2coversb1, identical, unknown };
        private BorderPosition borderPosition;
        public BorderPosition Position
        {
            get
            {
                if (border1 == null) return BorderPosition.unknown; // The initialization did not succeed
                if (borderPosition == BorderPosition.unknown)
                {
                    if (intersect && this.border1Points.Length > 0)
                    {
                        borderPosition = BorderPosition.intersecting;
                    }
                    else
                    {
                        bool on1 = false;
                        bool on2 = false;
                        Border.Position pos = border2.GetPosition(border1.StartPoint, precision);
                        if (pos == Border.Position.OnCurve)
                        {
                            // silly coincidence: no intersection point and tested with a contact point
                            try
                            {
                                // pos = border2.GetPosition(border1.SomeInnerPoint);
                                // Changed as follows: Consideration - no intersection point and tested with a contact point.
                                // Then test with another point on the border. Two contact points would be a big coincidence.
                                // That's why the strange number is used, to avoid systematic patterns.
                                pos = border2.GetPosition(border1.PointAt(0.636548264536 * border1.Segments.Length), precision);
                            }
                            catch (BorderException) { }
                        }
                        on1 = (pos == Border.Position.OnCurve);
                        if (pos == Border.Position.Inside)
                        {
                            borderPosition = BorderPosition.b2coversb1;
                        }
                        else
                        {
                            pos = border1.GetPosition(border2.StartPoint, precision);
                            if (pos == Border.Position.OnCurve)
                            {
                                try
                                {
                                    //pos = border1.GetPosition(border2.SomeInnerPoint);
                                    pos = border1.GetPosition(border2.PointAt(0.636548264536 * border2.Segments.Length), precision);
                                }
                                catch (BorderException) { }
                            }
                            on2 = (pos == Border.Position.OnCurve);
                            if (pos == Border.Position.Inside)
                            {
                                borderPosition = BorderPosition.b1coversb2;
                            }
                            else
                            {
                                borderPosition = BorderPosition.disjunct;
                            }
                        }
                        if (on1 && on2)
                        {
                            if (CheckIdentical()) borderPosition = BorderPosition.identical;
                        }
                    }
                }
                return borderPosition;
            }
        }

        private bool CheckIdentical()
        {
            for (int i = 0; i < border1.Count; ++i)
            {
                if (border2.GetPosition(border1.Segments[i].PointAt(0.5), precision) != Border.Position.OnCurve) return false;
            }
            for (int i = 0; i < border2.Count; ++i)
            {
                if (border1.GetPosition(border2.Segments[i].PointAt(0.5), precision) != Border.Position.OnCurve) return false;
            }
            return true;
        }
        internal bool IsValid()
        {
            return intersect && border1 != null && border2 != null;
        }
#if DEBUG
        List<PointPosition> debugb1p;
        List<PointPosition> debugb2p;
        public DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                res.Add(border1, System.Drawing.Color.Red, 1);
                res.Add(border2, System.Drawing.Color.Blue, 2);
                if (debugb1p != null)
                {
                    for (int i = 0; i < debugb1p.Count; ++i)
                    {
                        string dbg = i.ToString() + ": " + debugb1p[i].direction.ToString() + ", " + debugb1p[i].index.ToString() + " id: " + debugb1p[i].id.ToString();
                        res.Add(debugb1p[i].point, System.Drawing.Color.DarkRed, dbg);
                    }
                }
                if (debugb2p != null)
                {
                    for (int i = 0; i < debugb2p.Count; ++i)
                    {
                        string dbg = i.ToString() + ": " + debugb2p[i].direction.ToString() + ", " + debugb2p[i].index.ToString() + " id: " + debugb2p[i].id.ToString();
                        res.Add(debugb2p[i].point, System.Drawing.Color.DarkBlue, dbg);
                    }
                }
                return res;
            }
        }
#endif

        internal void MakeUnused()
        {
            for (int i = 0; i < border1Points.Length; i++)
            {
                border1Points[i].used = false;
            }
            for (int i = 0; i < border2Points.Length; i++)
            {
                border2Points[i].used = false;
            }
        }
    }

    // as a key for Dictionary
    internal class BorderPair : IComparable
    {
        int id1, id2;
        public BorderPair(Border bdr1, Border bdr2)
        {
            if (bdr1.Id < bdr2.Id)
            {
                id1 = bdr1.Id;
                id2 = bdr2.Id;
            }
            else
            {
                id1 = bdr2.Id;
                id2 = bdr1.Id;
            }
        }
        public override int GetHashCode()
        {
            return id1.GetHashCode() ^ id2.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            BorderPair other = obj as BorderPair;
            if (other != null)
            {
                return id1 == other.id1 && id2 == other.id2;
            }
            return base.Equals(obj);
        }

        int IComparable.CompareTo(object obj)
        {
            BorderPair other = obj as BorderPair;
            if (other != null)
            {
                if (other.id1 == id1) return id2.CompareTo(other.id2);
                return id1.CompareTo(other.id1);
            }
            return -1;
        }

        internal static BorderOperation GetBorderOperation(Dictionary<BorderPair, BorderOperation> borderOperationCache, Border bdr1, Border bdr2, double precision)
        {
            BorderOperation bo;
            if (borderOperationCache == null)
            {
                if (precision == 0.0) bo = new BorderOperation(bdr1, bdr2);
                else bo = new BorderOperation(bdr1, bdr2, precision);
                return bo;
            }
            BorderPair bp = new BorderPair(bdr1, bdr2);
            if (!borderOperationCache.TryGetValue(bp, out bo))
            {
                if (precision == 0.0) bo = new BorderOperation(bdr1, bdr2);
                else bo = new BorderOperation(bdr1, bdr2, precision);
                borderOperationCache[bp] = bo;
            }
            else
            {
                bo.MakeUnused();
            }
            return bo;
        }
    }
}
