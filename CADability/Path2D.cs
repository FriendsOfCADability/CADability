using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;

namespace CADability.Curve2D
{
    /// <summary>
    /// Composition of one or more ICurve2D objects. The contained ICurve2D objects are connected
    /// and stored in the right order, i.e. SubCurve[i].EndPoint is identical or close to 
    /// SubCurve[i+1].StartPoint. Path2D may be open or closed. It also may be self-intersecting.
    /// </summary>
    [Serializable()]
    public class Path2D : GeneralCurve2D, ISerializable
    {
        private ICurve2D[] subCurves;
        // private double[] length; // die Längen der einzelnen Teilstücke wird nirgends verwendet, kostet Zeit
        private enum State { yes, no, unknown }
        private bool displayClosed;
        /// <summary>
        /// Constructs a path from curves that are already connected in the right order: the end point of each curve must coincide
        /// with the start point of the next (according to <see cref="Precision.IsEqual(GeoPoint2D, GeoPoint2D)"/>). The array is
        /// copied, the curves themselves are shared with the caller.
        /// </summary>
        /// <param name="subCurves">the connected curves in their order</param>
        /// <exception cref="ApplicationException">when two consecutive curves are not connected</exception>
        public Path2D(ICurve2D[] subCurves)
        {
            this.subCurves = (ICurve2D[])subCurves.Clone();
            // length = new double[subCurves.Length];
            // for (int i = 0; i < subCurves.Length; ++i) length[i] = subCurves[i].Length;
            for (int i = 1; i < subCurves.Length; ++i)
            {
                if (!Precision.IsEqual(subCurves[i].StartPoint, subCurves[i - 1].EndPoint))
                {
#if DEBUG
                    DebuggerContainer dc1 = new DebuggerContainer();
                    dc1.Add(subCurves);
#endif
                    throw new ApplicationException("Path2D segments not connected");
                }
                // Das Problem tritt auf, wenn eine Ellipse/Kreis in der Projektion zur Linie wird: dann
                // stimmen Anfangs und Enpunkt nicht
            }
            displayClosed = false;
        }
        /// <summary>
        /// Constructs a path from curves in the right order and closes small gaps between them by moving the end point of each
        /// curve onto the start point of the next, which modifies the given curves. The value of <paramref name="forceConnection"/>
        /// is not examined; this overload always connects.
        /// </summary>
        /// <param name="subCurves">the curves in their order</param>
        /// <param name="forceConnection">not examined, the overload itself selects the behaviour</param>
        public Path2D(ICurve2D[] subCurves, bool forceConnection)
        {
            this.subCurves = (ICurve2D[])subCurves.Clone();
            for (int i = 1; i < subCurves.Length; ++i)
            {
                if (!Precision.IsEqual(this.subCurves[i].StartPoint, this.subCurves[i - 1].EndPoint))
                {
                    this.subCurves[i - 1].EndPoint = this.subCurves[i].StartPoint;
                }
            }
            // length = new double[subCurves.Length];
            // for (int i = 0; i < subCurves.Length; ++i) length[i] = subCurves[i].Length;
            displayClosed = false;
        }
        /// <summary>
        /// The curves the path consists of, in their order, as a copy of the internal array. The curves themselves are not copied,
        /// so changing one of them changes the path.
        /// </summary>
        public ICurve2D[] SubCurves
        {
            get { return (ICurve2D[])subCurves.Clone(); }
        }
        /// <summary>
        /// The number of curves the path consists of.
        /// </summary>
        public int SubCurvesCount
        {
            get
            {
                return subCurves.Length;
            }
        }
        /// <inheritdoc/>
        public override bool IsClosed
        {
            get
            {
                if (subCurves.Length == 1) return subCurves[0].IsClosed;
                return Precision.IsEqual(StartPoint, EndPoint);
            }
        }
        /// <summary>
        /// Creates a <see cref="Border"/> from the curves of this path. A closed border is oriented counterclockwise, so the
        /// segments may be reversed; use <see cref="MakeBorder(out bool)"/> to learn whether that happened.
        /// </summary>
        /// <returns>the border</returns>
        public Border MakeBorder()
        {
            return new Border(subCurves);
        }
        /// <summary>
        /// Creates a <see cref="Border"/> from the curves of this path and tells whether its orientation was reversed to make a
        /// closed border counterclockwise.
        /// </summary>
        /// <param name="reversed">true if the border runs in the opposite direction of this path</param>
        /// <returns>the border</returns>
        public Border MakeBorder(out bool reversed)
        {
            return new Border(out reversed, subCurves);
        }
        private void AddFlattend(ArrayList addTo, ICurve2D toAdd)
        {
            if (toAdd is Path2D)
            {
                AddFlattend(addTo, (toAdd as Path2D).subCurves);
            }
            else if (toAdd is Polyline2D)
            {
                Polyline2D p2d = (toAdd as Polyline2D);
                for (int i = 0; i < p2d.Vertex.Length - 1; ++i)
                {
                    Line2D l2d = new Line2D(p2d.Vertex[i], p2d.Vertex[i + 1]);
                    addTo.Add(l2d);
                }
            }
            else
            {
                if (toAdd != null && toAdd.Length > Precision.eps) addTo.Add(toAdd);
            }
        }
        private void AddFlattend(ArrayList addTo, ICurve2D[] toAdd)
        {
            for (int i = 0; i < toAdd.Length; ++i)
            {
                AddFlattend(addTo, toAdd[i]);
            }
        }
        /// <summary>
        /// Replaces nested <see cref="Path2D"/> segments by their own sub curves and <see cref="Polyline2D"/> segments by lines,
        /// so that the path consists of simple curves only. Null segments and segments shorter than <see cref="Precision.eps"/> are
        /// dropped.
        /// </summary>
        public void Flatten()
        {
            ArrayList newSubCurves = new ArrayList();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                AddFlattend(newSubCurves, subCurves[i]);
            }
            subCurves = (ICurve2D[])newSubCurves.ToArray(typeof(ICurve2D));
        }
        internal void ForceClosed()
        {
            if (!Precision.IsEqual(this.subCurves[subCurves.Length - 1].EndPoint, this.subCurves[0].StartPoint))
            {
                this.subCurves[subCurves.Length - 1].EndPoint = this.subCurves[0].StartPoint;
            }
        }
        internal void ForceConnected()
        {
            for (int i = 1; i < subCurves.Length; ++i)
            {
                GeoPoint2D c = new GeoPoint2D(subCurves[i - 1].EndPoint, subCurves[i].StartPoint);
                subCurves[i - 1].EndPoint = c;
                subCurves[i].StartPoint = c;
            }
        }
        /// <summary>
        /// Intended to mark the path as closed for display purposes. The value is stored but not used by the library at present.
        /// </summary>
        public bool DisplayClosed
        {
            set { displayClosed = value; }
        }
        /// <summary>
        /// Appends a curve at the end of the path. The start point of <paramref name="toAppend"/> is set to the end point of the
        /// path, which modifies that curve, so it should already start there or close to it.
        /// </summary>
        /// <param name="toAppend">the curve to append</param>
        public void Append(ICurve2D toAppend)
        {
            toAppend.StartPoint = this.EndPoint;
            List<ICurve2D> sub = new List<ICurve2D>(SubCurves);
            sub.Add(toAppend);
            subCurves = sub.ToArray();
        }
        /// <summary>
        /// Removes the first curve of the path; the remaining curves stay as they are.
        /// </summary>
        public void RemoveFirstSegment()
        {
            List<ICurve2D> sub = new List<ICurve2D>(SubCurves);
            sub.RemoveAt(0);
            subCurves = sub.ToArray();
        }
        /// <summary>
        /// Rough test whether two paths describe the same curve: both must have the same start and end points, and each inner
        /// vertex (junction of two segments) of either path must be closer than <paramref name="precision"/> to the other path. For a
        /// path with a single segment its middle point is tested as well. Deviations between the vertices are not detected.
        /// </summary>
        /// <param name="precision">the allowed distance</param>
        /// <param name="other">the path to compare with</param>
        /// <returns>true if the paths are considered equal</returns>
        public bool GeometricalEqual(double precision, Path2D other)
        {   // nur grober Test, mehr brauch ich z.Z. nicht bei BorderOperation
            if (this.SubCurvesCount == 0 && other.SubCurvesCount == 0) return true;
            if (this.SubCurvesCount == 0) return false;
            if (other.SubCurvesCount == 0) return false;
            if ((this.StartPoint | other.StartPoint) > precision) return false;
            if ((this.EndPoint | other.EndPoint) > precision) return false;
            for (int i = 0; i < subCurves.Length - 1; i++)
            {
                if (other.MinDistance(subCurves[i].EndPoint) > precision) return false;
            }
            for (int i = 0; i < other.subCurves.Length - 1; i++)
            {
                if (this.MinDistance(other.subCurves[i].EndPoint) > precision) return false;
            }
            if (subCurves.Length == 1)
            {
                if (other.MinDistance(subCurves[0].PointAt(0.5)) > precision) return false;
            }
            return true;
        }
        /// <summary>
        /// Adds a curve at the end or at the beginning of the path if one of its end points is closer than
        /// <paramref name="precision"/> to the end point or the start point of the path. The curve is reversed when necessary, and
        /// small gaps are closed by moving end points, so <paramref name="toConnect"/> may be modified.
        /// </summary>
        /// <param name="toConnect">the curve to add</param>
        /// <param name="precision">the maximal distance between the end points to connect</param>
        /// <returns>true if the curve was added, false if it does not connect to either end of the path</returns>
        public bool ConnectWith(ICurve2D toConnect, double precision)
        {
            if ((this.EndPoint | toConnect.StartPoint) < precision)
            {
                List<ICurve2D> list = new List<ICurve2D>(subCurves);
                list.Add(toConnect);
                subCurves = list.ToArray();
            }
            else if ((this.EndPoint | toConnect.EndPoint) < precision)
            {
                List<ICurve2D> list = new List<ICurve2D>(subCurves);
                toConnect.Reverse();
                list.Add(toConnect);
                subCurves = list.ToArray();
            }
            else if ((this.StartPoint | toConnect.EndPoint) < precision)
            {
                List<ICurve2D> list = new List<ICurve2D>(subCurves);
                list.Insert(0, toConnect);
                subCurves = list.ToArray();
            }
            else if ((this.StartPoint | toConnect.StartPoint) < precision)
            {
                List<ICurve2D> list = new List<ICurve2D>(subCurves);
                toConnect.Reverse();
                list.Insert(0, toConnect);
                subCurves = list.ToArray();
            }
            else return false;
            for (int i = 1; i < subCurves.Length; ++i)
            {
                if (!Precision.IsEqual(this.subCurves[i].StartPoint, this.subCurves[i - 1].EndPoint))
                {
                    this.subCurves[i - 1].EndPoint = this.subCurves[i].StartPoint;
                }
            }
            // length = new double[subCurves.Length];
            // for (int i = 0; i < subCurves.Length; ++i) length[i] = subCurves[i].Length;
            return true;
        }
        /// <summary>
        /// The curve with the given index, in the order of the path. It is the curve itself, not a copy.
        /// </summary>
        /// <param name="Index">index from 0 to <see cref="SubCurvesCount"/>-1</param>
        /// <returns>the curve</returns>
        public ICurve2D this[int Index]
        {
            get { return subCurves[Index]; }
        }
        /// <summary>
        /// Replaces the curve with the given index by another curve with the same start and end points, e.g. by a simpler or a
        /// more precise representation of the same segment.
        /// </summary>
        /// <param name="index">index of the curve to replace</param>
        /// <param name="curve">the new curve</param>
        /// <returns>true if replaced, false if the start or end point of <paramref name="curve"/> differs from those of the segment</returns>
        public bool ReplaceSubcurve(int index, ICurve2D curve)
        {
            if (!Precision.IsEqual(subCurves[index].StartPoint, curve.StartPoint)) return false;
            if (!Precision.IsEqual(subCurves[index].EndPoint, curve.EndPoint)) return false;
            subCurves[index] = curve;
            return true;
        }
        #region ICurve2D Members
        /// <inheritdoc/>
        public override GeoPoint2D StartPoint
        {
            get
            {
                return subCurves[0].StartPoint;
            }
            set
            {
                subCurves[0].StartPoint = value;
                // TODO: Achtung, das macht length kaputt, fehlt entsprechender Mechanismus
            }
        }
        /// <inheritdoc/>
        public override GeoPoint2D EndPoint
        {
            get
            {
                return subCurves[subCurves.Length - 1].EndPoint;
            }
            set
            {
                subCurves[subCurves.Length - 1].EndPoint = value;
            }
        }
        /// <inheritdoc/>
        public override GeoVector2D StartDirection
        {
            get
            {
                return subCurves[0].StartDirection;
            }
        }
        /// <inheritdoc/>
        public override GeoVector2D EndDirection
        {
            get
            {
                return subCurves[subCurves.Length - 1].EndDirection;
            }
        }
        /// <inheritdoc/>
        public override GeoVector2D MiddleDirection
        {
            get
            {
                throw new NotSupportedException("Get MiddleDirection of Path2D");
            }
        }
        /// <inheritdoc/>
        public override GeoVector2D DirectionAt(double Position)
        {
            double l = Position * subCurves.Length;
            int i = (int)l; // (int) schneidet ab, also ist i der Index der Kurve
            if (i < 0) i = 0;
            if (i >= subCurves.Length) i = subCurves.Length - 1;
            double p = l - i;
            return subCurves[i].DirectionAt(p);
        }
        /// <inheritdoc/>
        public override GeoPoint2D PointAt(double Position)
        {	// Position ist zwischen 0.0 und 1.0
            // wird zuerst hochgerechnet auf Gesamtlänge und dann in Index und Offset aufgeteilt
            double l = Position * subCurves.Length;
            int i = (int)l; // (int) schneidet ab, also ist i der Index der Kurve
            if (i < 0) i = 0;
            if (i >= subCurves.Length) i = subCurves.Length - 1;
            double p = l - i;
            return subCurves[i].PointAt(p);
        }
        /// <inheritdoc/>
        public override double PositionOf(GeoPoint2D p)
        {
            int foundSegment = -1;
            double foundPosition = -1.0;
            double minDist = double.MaxValue;
            // solange der Punkt auf einem Segment liegt, gibt es kein Problem. Ist der
            // Punkt allerdings außerhalb, so kann er u.U. sowohl vor dem Anfang als auch nach
            // dem Ende des Pfades liegen (z.B. beim Halbkreis). Wir lassen nur die Seite zu,
            // die dem gesuchten Punkt näher liegt, sonst gibt es bei solchen Halbkreisförmigen
            // Pfaden Probleme.
            bool closerToStartPoint = Geometry.Dist(StartPoint, p) < Geometry.Dist(EndPoint, p);
            for (int i = 0; i < subCurves.Length; ++i)
            {
                double pos = subCurves[i].PositionOf(p);
                double d = Math.Abs(subCurves[i].Distance(p));
                if (d < minDist)
                {
                    if ((pos >= 0.0 && pos <= 1.0) ||
                        (closerToStartPoint && i == 0 && pos < 0.5) ||
                        (!closerToStartPoint && i == subCurves.Length - 1 && pos > 0.5))
                    {
                        minDist = d;
                        foundSegment = i;
                        foundPosition = pos;
                    }
                }
            }
            if (foundSegment >= 0) return (foundSegment + foundPosition) / subCurves.Length;
            else return -1.0; // d.h. außerhalb
        }
        internal double InsidePositionOf(GeoPoint2D p)
        {   // wie PositionOf, liefert jedoch nur innere Punkte
            int foundSegment = -1;
            double foundPosition = -1.0;
            double minDist = double.MaxValue;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                double pos = subCurves[i].PositionOf(p);
                double d = Math.Abs(subCurves[i].Distance(p));
                if (d < minDist)
                {
                    if ((pos >= 0.0 && pos <= 1.0))
                    {
                        minDist = d;
                        foundSegment = i;
                        foundPosition = pos;
                    }
                }
            }
            // noch die Eckpunkte in den Test mit einbeziehen
            for (int i = 0; i < subCurves.Length; i++)
            {
                double d = p | subCurves[i].StartPoint;
                if (d < minDist)
                {
                    minDist = d;
                    foundSegment = i;
                    foundPosition = 0.0;
                }
            }
            {
                double d = p | subCurves[subCurves.Length - 1].EndPoint;
                if (d < minDist)
                {
                    minDist = d;
                    foundSegment = subCurves.Length - 1;
                    foundPosition = 1.0;
                }
            }
            if (foundSegment >= 0) return (foundSegment + foundPosition) / subCurves.Length;
            else return -1.0; // d.h. außerhalb
        }
        /// <inheritdoc/>
        public override double PositionAtLength(double position)
        {
            double length = Length;
            int maxind = subCurves.Length;
            for (int i = 0; i < maxind; ++i)
            {
                double l = subCurves[i].Length;
                if (l < position)
                {
                    position -= l;
                }
                else
                {
                    return (i + subCurves[i].PositionAtLength(position)) / maxind;
                }
            }
            return 1.0;
        }
        /// <inheritdoc/>
        public override double Length
        {
            get
            {
                double l = 0.0;
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    l += subCurves[i].Length;
                }
                return l;
            }
        }
        /// <inheritdoc/>
        public override double Sweep
        {
            get
            {
                double sweep = 0.0;
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    if (i > 0)
                    {
                        SweepAngle vertexdiff = new SweepAngle((subCurves[i - 1] as ICurve2D).EndDirection, (subCurves[i] as ICurve2D).StartDirection);
                        sweep += vertexdiff;
                    }
                    sweep += subCurves[i].Sweep;
                }
                return sweep;
            }
        }
        /// <inheritdoc/>
        public override double GetAreaFromPoint(GeoPoint2D p)
        {
            double res = 0.0;
            for (int i = 0; i < subCurves.Length; i++)
            {
                res += subCurves[i].GetAreaFromPoint(p);
            }
            return res;
        }
        /// <inheritdoc/>
        public override ICurve2D[] Split(double Position)
        {
            if (Position <= 0.0 || Position >= 1.0)
            {
                return new ICurve2D[] { Clone() };
            }
            else
            {
                return new ICurve2D[] { Trim(0.0, Position), Trim(Position, 1.0) };
            }
        }
        /// <summary>
        /// Splits the path at several normalized parameters at once. The path itself remains unchanged.
        /// </summary>
        /// <param name="positions">the parameters where to split; the array is sorted in place</param>
        /// <returns>the pieces between consecutive parameters, from 0.0 to 1.0, in their order; empty intervals (duplicate
        /// parameters, parameters at 0.0 or 1.0) and degenerate pieces are left out</returns>
        public ICurve2D[] Split(double[] positions)
        {
            Array.Sort(positions);
            // Duplicate positions, and positions at 0.0 or 1.0, describe empty intervals. Trim returns null
            // or a degenerate curve for those, and a fixed size array would hand them to the caller.
            List<ICurve2D> res = new List<ICurve2D>(positions.Length + 1);
            for (int i = 0; i <= positions.Length; i++)
            {
                double spar, epar;
                if (i == 0) spar = 0.0;
                else spar = positions[i - 1];
                if (i == positions.Length) epar = 1.0;
                else epar = positions[i];
                if (spar >= epar - 1e-6) continue;
                ICurve2D trimmed = Trim(spar, epar);
                if (trimmed != null && trimmed.Length > Precision.eps) res.Add(trimmed);
            }
            return res.ToArray();
        }
        /// <inheritdoc/>
        public override double Distance(GeoPoint2D p)
        {
            double mindist = double.MaxValue; // Absolut, immer positiv
            double res = double.MaxValue; // echter Abstand, Vorzeichenbehaftet
            for (int i = 0; i < subCurves.Length; ++i)
            {
                double d = subCurves[i].Distance(p);
                double dd = Math.Abs(d);
                if (dd < mindist)
                {
                    mindist = dd;
                    res = d;
                }
            }
            return res;
        }
        /// <inheritdoc/>
        public override double MinDistance(ICurve2D Other)
        {
            double minDist = double.MaxValue;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                minDist = Math.Min(minDist, subCurves[i].MinDistance(Other));
            }
            return minDist;
        }
        /// <inheritdoc/>
        public override double MinDistance(GeoPoint2D p)
        {
            double minDist = double.MaxValue;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                minDist = Math.Min(minDist, subCurves[i].MinDistance(p));
            }
            return minDist;
        }
        /// <inheritdoc/>
        public override ICurve2D Trim(double StartPos, double EndPos)
        {
            Path2D res = this.Clone() as Path2D;
            res.TrimOff(StartPos, EndPos);
            return res;
        }
        private void TrimOff(double StartPos, double EndPos)
        {
            double sp = StartPos * subCurves.Length;
            double ep = EndPos * subCurves.Length;
            int sind = (int)Math.Ceiling(sp);
            int eind = (int)Math.Floor(ep);
            ArrayList res = new ArrayList();
            if (eind < sind)
            {
                // nur ein Stückchen von sind
                double par1 = sp - sind + 1;
                double par2 = ep - sind + 1;
                ICurve2D curve = subCurves[sind - 1].Clone();
                curve = curve.Trim(par1, par2);
                if (curve != null) res.Add(curve);
            }
            else
            {
                if (sind > 0)
                {
                    double par = sp - sind + 1;
                    if (par < 1.0 - 1e-6)
                    {
                        ICurve2D curve = subCurves[sind - 1].Clone();
                        curve = curve.Trim(par, 1.0);
                        if (curve != null) res.Add(curve);
                    }
                }
                for (int i = sind; i < eind; ++i)
                {
                    res.Add(subCurves[i]);
                }
                if (eind < subCurves.Length)
                {
                    double par = ep - eind;
                    if (par > 1e-6)
                    {
                        ICurve2D curve = subCurves[eind].Clone();
                        curve = curve.Trim(0.0, par);
                        if (curve != null) res.Add(curve);
                    }
                }
            }
            subCurves = (ICurve2D[])res.ToArray(typeof(ICurve2D));

        }
        /// <inheritdoc/>
        public override ICurve2D Parallel(double Dist, bool approxSpline, double precision, double roundAngle)
        {
            ICurve2D[] par = new ICurve2D[subCurves.Length];
            for (int i = 0; i < subCurves.Length; ++i)
            {
                par[i] = subCurves[i].Parallel(Dist, approxSpline, precision, roundAngle);
            }
            bool removeRecurringLines = false;
            if (roundAngle < 0)
            {
                roundAngle = -roundAngle;
                removeRecurringLines = true;
            }
            List<ICurve2D> res = new List<ICurve2D>();
            //res.Add(par[0]);
            List<int> splitPositions = new List<int>(); // there is no connection between res[i].EndPoint and res[i+1].StartPoint
            // A closed path has one more corner than it has gaps between consecutive segments: the one
            // between the last and the first segment. Stopping at par.Length - 1 leaves that corner
            // untreated and the parallel curve of a closed path comes back open.
            for (int i = 0; i < par.Length; ++i)
            {
                int nexti = (i + 1) % par.Length;
                res.Add(par[i]); // maybe this curve will be shortened
                if (nexti == 0 && !IsClosed) continue; // the last segment of an open path has no successor
                if (!Precision.IsEqual(par[i].EndPoint, par[nexti].StartPoint))
                {	// not tangential
                    GeoVector2D endtan = subCurves[i].EndDirection;
                    GeoVector2D starttan = subCurves[nexti].StartDirection;
                    bool connected = false;
                    double b = GeoVector2D.Orientation(starttan, endtan);
                    if ((b > 0) != (Dist > 0))
                    {   // it is an outside bend, so we have to introduce an arc
                        // or two lines
                        double a = Math.PI - new Angle(endtan, starttan);
                        bool round = a < roundAngle; // wir suchen den Innenwinkel
                        if (a >= roundAngle)
                        {   // make two lines, because the angle is obtuse
                            GeoPoint2D ip;
                            if (Geometry.IntersectLL(par[i].EndPoint, endtan, par[nexti].StartPoint, starttan, out ip))
                            {
                                double pos1 = Geometry.LinePar(par[i].EndPoint, endtan, ip);
                                double pos2 = Geometry.LinePar(par[nexti].StartPoint, starttan, ip);
                                if (pos1 > 0 || pos2 < 0)
                                {
                                    res.Add(new Line2D(par[i].EndPoint, ip));
                                    res.Add(new Line2D(ip, par[nexti].StartPoint));
                                    connected = true;
                                }
                            }
                        }
                        if (!connected)
                        {   // runde Ecken
                            Arc2D a2d = new Arc2D(subCurves[i].EndPoint, Math.Abs(Dist), par[i].EndPoint, par[nexti].StartPoint, Dist > 0.0);
                            res.Add(a2d);
                        }
                    }
                    else
                    {   // it bends to the inside: either we can shorten the two segments i,i+1 or we have to leave the parallel path open at these ends and fix it later
                        GeoPoint2DWithParameter[] ips = par[i].Intersect(par[nexti]);
                        if (ips.Length > 0)
                        {
                            int ind = 0;
                            if (ips.Length > 1)
                            {   // multiple intersections (very rare), we need the closest to the end of par[i]
                                double maxp1 = 0.0;
                                for (int j = 0; j < ips.Length; j++)
                                {
                                    if (ips[j].par1>0 && ips[j].par1<=1.0&& ips[j].par2 > 0 && ips[j].par2 <= 1.0 && ips[j].par1 > maxp1)
                                    {
                                        ind = j;
                                        maxp1 = ips[j].par1;
                                    }
                                }
                            }
                            par[i].EndPoint = ips[ind].p;
                            par[nexti].StartPoint = ips[ind].p;
                        }
                        else
                        {
                            splitPositions.Add(res.Count - 1);
                        }
                    }
                }
                else
                {   // i,i+1 is a tangetnial connection, so the parallel curves are also connected
                }
            }
            if (splitPositions.Count > 0)
            {
                // res contains several non connected paths
                Path2D[] nonconnected = new Path2D[splitPositions.Count + 1];
                for (int i = 0; i <= splitPositions.Count; i++)
                {
                    int si, ei;
                    if (i == 0) si = 0;
                    else si = splitPositions[i - 1] + 1;
                    if (i == splitPositions.Count) ei = res.Count;
                    else ei = splitPositions[i] + 1;
                    ICurve2D[] subList = new ICurve2D[ei - si];
                    res.CopyTo(si, subList, 0, ei - si);
                    nonconnected[i] = new Path2D(subList);
                }
                for (int i = 0; i < nonconnected.Length; i++)
                {
                    int j = (i + 1) % nonconnected.Length;
                    if (j != 0 || IsClosed) // only a closed path has to be joined up again at its end
                    {
                        GeoPoint2DWithParameter[] ips = nonconnected[i].Intersect(nonconnected[j]);
                        if (ips.Length > 0)
                        {
                            int ind = -1;
                            double maxp1 = 0.0;
                            for (int k = 0; k < ips.Length; k++)
                            {
                                if (ips[k].par1 > -1e-6 && ips[k].par1 < 1.0 + 1e-6 && ips[k].par2 > -1e-6 && ips[k].par2 < 1.0 + 1e-6)
                                {
                                    if (ips[k].par1 > maxp1)
                                    {
                                        ind = k;
                                        maxp1 = ips[k].par1;
                                    }
                                }
                            }
                            // not sure, whether the following is the general solution:
                            if (ind >= 0)
                            {
                                nonconnected[i].TrimOff(0.0, ips[ind].par1);
                                nonconnected[j].TrimOff(ips[ind].par2, 1.0);
                            }
                        }
                    }
                }
                res.Clear();
                for (int i = 0; i < nonconnected.Length; i++)
                {
                    res.AddRange(nonconnected[i].subCurves);
                    if (i < nonconnected.Length - 1)
                    {
                        if (!Precision.IsEqual(nonconnected[i].EndPoint, nonconnected[i + 1].StartPoint))
                        {
                            if (Geometry.IntersectLL(nonconnected[i].EndPoint, nonconnected[i].EndDirection.ToLeft(), nonconnected[i + 1].StartPoint,
                                nonconnected[i + 1].StartDirection.ToLeft(), out GeoPoint2D center))
                            {
                                Arc2D a2d = new Arc2D(center, Math.Abs(Dist), nonconnected[i].EndPoint, nonconnected[i + 1].StartPoint, Dist > 0.0);
                                SweepAngle sw = new SweepAngle(a2d.EndDirection, nonconnected[i + 1].StartDirection);
                                if (Math.Abs(sw) > Math.PI / 2.0) a2d = new Arc2D(center, Math.Abs(Dist), nonconnected[i].EndPoint, nonconnected[i + 1].StartPoint, Dist < 0.0);
                                res.Add(a2d);
                            }
                        }
                    }
                }
            }
            if (removeRecurringLines)
            {
                bool removed = false;
                do
                {
                    removed = false;
                    for (int i = 0; i < res.Count - 1; i++)
                    {
                        if (Precision.OppositeDirection(res[i].EndDirection, res[i + 1].StartDirection))
                        {
                            double pos = res[i].PositionOf(res[i + 1].EndPoint);
                            if (pos > 0 && pos < 1)
                            {
                                // res[i].EndPoint = res[i + 1].EndPoint;
                                res[i].EndPoint = res[i].PointAt(pos);
                                res.RemoveAt(i + 1);
                                --i;
                                removed = true;
                            }
                            else
                            {
                                pos = res[i + 1].PositionOf(res[i].StartPoint);
                                if (pos > 0 && pos < 1)
                                {
                                    // res[i + 1].StartPoint = res[i].StartPoint;
                                    res[i + 1].StartPoint = res[i + 1].PointAt(pos);
                                    res.RemoveAt(i);
                                    --i;
                                    removed = true;
                                }
                            }
                        }
                    }
                } while (removed);
                for (int i = 0; i < res.Count - 1; i++)
                {
                    if (res[i + 1] is Arc2D) res[i].EndPoint = res[i + 1].StartPoint;
                    else if (res[i] is Arc2D) res[i + 1].StartPoint = res[i].EndPoint;
                }
            }
            Path2D pres = new Path2D(res.ToArray(), true);
            return pres;
        }
        /// <inheritdoc/>
        public override GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {
            ArrayList res = new ArrayList();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                GeoPoint2DWithParameter[] tmp = subCurves[i].Intersect(IntersectWith);
                for (int j = 0; j < tmp.Length; ++j)
                {
                    // nur innere Schnittpunkte gelten, es sei denn für die 1. oder letzte Kurve
                    if (subCurves[i].IsParameterOnCurve(tmp[j].par1) ||
                        ((i == 0) && (tmp[j].par1 < 0.5)) || // 0.5 um Genauigkeitsprobleme bei 0.0 bzw. 1.0 zu vermeiden
                        ((i == subCurves.Length - 1) && (tmp[j].par1 > 0.5)))
                    {
                        tmp[j].par1 = (tmp[j].par1 + i) / subCurves.Length;
                        res.Add(tmp[j]);
                    }
                }
            }
            return (GeoPoint2DWithParameter[])res.ToArray(typeof(GeoPoint2DWithParameter));
        }
        /// <inheritdoc/>
        public override GeoPoint2DWithParameter[] Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            Line2D l2d = new Line2D(StartPoint, EndPoint);
            return Intersect(l2d);
        }
        /// <inheritdoc/>
        public override GeoPoint2D[] PerpendicularFoot(GeoPoint2D FromHere)
        {
            ArrayList res = new ArrayList();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                GeoPoint2D[] tmp = subCurves[i].PerpendicularFoot(FromHere);
                for (int j = 0; j < tmp.Length; ++j)
                {	// Fußpunkte müssen auf den einzelnen Kurven liegen oder 
                    // in der vorderen bzw. hinteren Verlängerung
                    double pos = subCurves[i].PositionOf(tmp[j]);
                    bool add = subCurves[i].IsParameterOnCurve(pos);
                    if (i == 0) add |= pos < 0.5;
                    if (i == subCurves.Length - 1) add |= pos > 0.5;
                    if (add) res.Add(tmp[j]);
                }
            }
            return (GeoPoint2D[])res.ToArray(typeof(GeoPoint2D));
        }
        /// <inheritdoc/>
        public override GeoPoint2D[] TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo)
        {
            ArrayList res = new ArrayList();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                res.AddRange(subCurves[i].TangentPoints(FromHere, CloseTo));
            }
            return (GeoPoint2D[])res.ToArray(typeof(GeoPoint2D));
        }
        /// <inheritdoc/>
        public override GeoPoint2D[] TangentPointsToAngle(Angle ang, GeoPoint2D CloseTo)
        {
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                res.AddRange(subCurves[i].TangentPointsToAngle(ang, CloseTo));
            }
            return res.ToArray();
        }
        /// <inheritdoc/>
        public override double[] TangentPointsToAngle(GeoVector2D direction)
        {
            List<double> res = new List<double>();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                double[] tmp = subCurves[i].TangentPointsToAngle(direction);
                for (int j = 0; j < tmp.Length; j++)
                {
                    tmp[j] = (tmp[j] + i) / subCurves.Length;
                }
                res.AddRange(tmp);
            }
            return res.ToArray();
        }
        /// <inheritdoc/>
        public override double[] GetInflectionPoints()
        {
            return base.GetInflectionPoints();
        }
        /// <inheritdoc/>
        public override void Reverse()
        {
            Array.Reverse(subCurves);
            for (int i = 0; i < subCurves.Length; ++i)
            {
                subCurves[i].Reverse();
            }
        }
        /// <inheritdoc/>
        public override void Move(double dx, double dy)
        {
            for (int i = 0; i < subCurves.Length; ++i)
            {
                subCurves[i].Move(dx, dy);
            }
        }
        /// <inheritdoc/>
        public override ICurve2D Clone()
        {
            ICurve2D[] subCurvesClone = new ICurve2D[subCurves.Length];
            for (int i = 0; i < subCurves.Length; ++i)
            {
                subCurvesClone[i] = subCurves[i].Clone();
            }
            Path2D res = new Path2D(subCurvesClone);
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <inheritdoc/>
        public override ICurve2D CloneReverse(bool reverse)
        {
            ICurve2D res = Clone();
            if (reverse) res.Reverse();
            return res;
        }
        /// <inheritdoc/>
        public override void Copy(ICurve2D toCopyFrom)
        {   // müssen die gleiche Struktur haben
            Path2D c = toCopyFrom as Path2D;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                subCurves[i].Copy(c.subCurves[i]);
            }
            UserData.CloneFrom(c.UserData);
        }
        /// <inheritdoc/>
        public override IGeoObject MakeGeoObject(Plane p)
        {
            ICurve[] curves3D = new ICurve[subCurves.Length];
            for (int i = 0; i < subCurves.Length; ++i)
            {
                curves3D[i] = subCurves[i].MakeGeoObject(p) as ICurve;
            }
            // if (curves3D.Length == 1) return curves3D[0] as IGeoObject;
            Path res = Path.Construct();
            res.Set(curves3D);
            return res;
        }
        /// <inheritdoc/>
        public override ICurve2D Project(Plane fromPlane, Plane toPlane)
        {
            ArrayList curves = new ArrayList(subCurves.Length);
            for (int i = 0; i < subCurves.Length; ++i)
            {
                ICurve2D pr = subCurves[i].Project(fromPlane, toPlane);
                if (pr != null) curves.Add(pr);
            }
            return new Path2D((ICurve2D[])(curves.ToArray(typeof(ICurve2D))));
        }
        /// <inheritdoc/>
        public override void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {
            if (forward)
            {
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    subCurves[i].AddToGraphicsPath(path, forward);
                }
            }
            else
            {
                for (int i = subCurves.Length - 1; i >= 0; --i)
                {
                    subCurves[i].AddToGraphicsPath(path, forward);
                }
            }
        }
        /// <inheritdoc/>
        public override bool IsParameterOnCurve(double par)
        {
            double l = par * subCurves.Length;
            int i = (int)l; // (int) schneidet ab, also ist i der Index der Kurve
            if (i < 0) i = 0;
            if (i >= subCurves.Length) i = subCurves.Length - 1;
            double p = l - i;
            return subCurves[i].IsParameterOnCurve(p);
        }
        /// <inheritdoc/>
        public override IQuadTreeInsertable GetExtendedHitTest()
        {
            return new InfinitePath2D(this);
        }
        /// <inheritdoc/>
        public override double[] GetSelfIntersections()
        {
            ArrayList res = new ArrayList();
            for (int i = 0; i < subCurves.Length - 1; ++i)
            {
                for (int j = i + 1; j < subCurves.Length; ++j)
                {
                    GeoPoint2DWithParameter[] isp = subCurves[i].Intersect(subCurves[j]);
                    for (int k = 0; k < isp.Length; ++k)
                    {
                        if (subCurves[i].IsParameterOnCurve(isp[k].par1) &&
                            subCurves[j].IsParameterOnCurve(isp[k].par2))
                        {
                            // Schnittpunkte der unmittelbar aufeinanderfolgenden Stücke
                            // (also die eigentlichen Zusammenhänge) nicht berücksichtigen
                            bool skip = false;
                            if (j == i + 1 && (isp[k].par1 > 1.0 - 1e-5 || isp[k].par2 < 1e-5))
                                skip = true; // Schnitt zweier aufeinanderfolgender Kurven am Zusammenhangspunkt
                            if (i == 0 && j == subCurves.Length - 1 && (isp[k].par1 < 1e-5 || isp[k].par2 > 1.0 - 1e-5))
                                skip = true; // Schnitt des Endpunkts mit dem Anfangspunkt
                            if (!skip)
                            {
                                double par1 = (i + isp[k].par1) / subCurves.Length;
                                double par2 = (j + isp[k].par2) / subCurves.Length;
                                res.Add(par1);
                                res.Add(par2);
                            }
                        }
                    }
                }
            }
            return (double[])res.ToArray(typeof(double));
        }
        internal Path2D SelfIntersectionsRemoved()
        {   // Achtung: geschlossen wird hier noch nicht überprüft!!!
            double[] si = GetSelfIntersections();
            if (si.Length == 0) return null; // keine Selbstüberschneidung
#if DEBUG

            DebuggerContainer dbg = new DebuggerContainer();
            for (int i = 0; i < si.Length; ++i)
            {
                dbg.Add(this.PointAt(si[i]), Color.Red, i);
            }
            dbg.Add(this, Color.Blue, 0);
#endif
            double start = 0.0;
            List<ICurve2D> snippets = new List<ICurve2D>();
            for (int i = 0; i < si.Length; i += 2)
            {
                if (si[i] - start > 0.5)
                {
                    ICurve2D tr = this.Trim(start, si[i]);
                    snippets.Add(tr);
                    start = si[i + 1];
                }
            }
            {
                ICurve2D tr = this.Trim(start, 1.0);
                snippets.Add(tr);
            }
            try
            {
                return new Path2D(snippets.ToArray());
            }
            catch (System.ApplicationException)
            {
                return null;
            }
        }
        /// <inheritdoc/>
        public override bool ReinterpretParameter(ref double p)
        {
            if (subCurves.Length == 1)
            {
                return subCurves[0].ReinterpretParameter(ref p);
            }
            return false;
        }
        /// <inheritdoc/>
        public override ICurve2D Approximate(bool linesOnly, double maxError)
        {
            // wenn alles Linien oder Kreisbögen sind, dann ist man ja schon fertig
            bool ok = true;
            bool mixed = false;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                if (subCurves[i] is Line2D)
                {
                    mixed = true;
                    continue;
                }
                if (subCurves[i] is BSpline2D)
                {
                    mixed = true;
                    continue;
                }
                if (!linesOnly && (subCurves[i] is Arc2D || subCurves[i] is Circle2D))
                {
                    mixed = true;
                    continue;
                }
                ok = false;
                break;
            }
            if (ok && !mixed) return this.Clone();
            if (mixed)
            {   // enthält sowohl echte Linien (oder Bögen, wenn Bögen erlaubt) als auch andere Dinge
                // dann ist es besser die einzelnen untersegmente zu approximieren
                List<ICurve2D> list = new List<ICurve2D>();
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    list.Add(subCurves[i].Approximate(linesOnly, maxError));
                }
                Path2D res = new Path2D(list.ToArray(), true);
                res.Flatten();
                return res;
            }
            else
            {
                // nur nicht erlaubte segmente, insbesondere viele kurze tangentiale Splines.
                // mit möglichst wenigen Stützpunkten annähern
                List<ICurve2D> list;
                if (IsClosed)
                {   // Start- und Enpunkt identisch. Man braucht mindestens einen Zwischenpunkt
                    List<ICurve2D> l1 = Approximate(0.0, 0.5, linesOnly, maxError);
                    List<ICurve2D> l2 = Approximate(0.5, 1.0, linesOnly, maxError);
                    list = new List<ICurve2D>();
                    list.AddRange(l1);
                    list.AddRange(l2);
                }
                else
                {
                    list = Approximate(0.0, 1.0, linesOnly, maxError);
                }
                Path2D res = new Path2D(list.ToArray(), true);
                res.Flatten();
                return res;
            }
        }

        private List<ICurve2D> Approximate(double par1, double par2, bool linesOnly, double maxError)
        {
            double l = par1 * subCurves.Length;
            int i1 = (int)l + 1;
            if (i1 < 0) i1 = 0;
            // if (i1 >= subCurves.Length) i1 = subCurves.Length - 1;
            // nicht i1 runterrechnen, wird ja nie als index verwendet, wenn es größer als i2 ist
            l = par2 * subCurves.Length;
            int i2 = (int)l;
            if (i2 < 0) i2 = 0;
            if (i2 >= subCurves.Length) i2 = subCurves.Length - 1;
            if (linesOnly)
            {
                Line2D l2d = new Line2D(PointAt(par1), PointAt(par2));
                bool ok = true;
                for (int i = i1; i <= i2; ++i)
                {
                    GeoPoint2D p = subCurves[i].StartPoint;
                    if (Math.Abs(Geometry.DistPL(p, l2d.StartPoint, l2d.EndPoint)) > maxError)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok && MinDistance(l2d.PointAt(0.5)) > maxError && l2d.Length > maxError) ok = false;
                // l2d.Length > maxError: wenn die beiden Punkte so nahe beieinander liegen könnte es sich natürlich um eine Überschneidung handeln
                // aber das ist extrem unwahrscheinlich. Man könnte hier auch einen Abbruch nehmen, wenn die beiden Parameter sich zu nahe kommen
                // aber irgend ein Abbruchkriterium braucht es, MinDistance ist zu unsicher
                if (ok)
                {
                    List<ICurve2D> res = new List<ICurve2D>(1);
                    res.Add(l2d);
                    return res;
                }
                else
                {
                    List<ICurve2D> res = new List<ICurve2D>();
                    double pmm = (par1 + par2) / 2.0;
                    res.AddRange(Approximate(par1, pmm, linesOnly, maxError));
                    res.AddRange(Approximate(pmm, par2, linesOnly, maxError));
                    return res;
                }
            }
            else
            {
                ICurve2D[] arcs = Curves2D.ConnectByTwoArcs(PointAt(par1), PointAt(par2), DirectionAt(par1), DirectionAt(par2));
                bool ok = true;
                for (int i = i1; i <= i2; ++i)
                {
                    GeoPoint2D p = subCurves[i].StartPoint;
                    bool close = false;
                    for (int j = 0; j < arcs.Length; ++j)
                    {
                        if (arcs[j].MinDistance(p) < maxError)
                        {
                            close = true;
                        }
                    }
                    if (!close)
                    {
                        ok = false;
                        break;
                    }
                    if (!close)
                    {
                        ok = false;
                        break;
                    }
                }
                // jetzt umgekehrt den Abstand der Mittelpunkte der Bögen (oder Linien) vom Originalpfad testen
                for (int j = 0; j < arcs.Length; ++j)
                {
                    if (this.MinDistance(arcs[j].PointAt(0.5)) > maxError)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                {
                    return new List<ICurve2D>(arcs);
                }
                else
                {
                    if (Math.Abs(arcs[0].Sweep) < 0.1 || Math.Abs(arcs[1].Sweep) < 0.1)
                    {   // Bögen zu klein, dann besser Linien, sonst gibts Stack Überlauf
                        return Approximate(par1, par2, true, maxError);
                    }
                    else
                    {
                        List<ICurve2D> res = new List<ICurve2D>();
                        double pmm = (par1 + par2) / 2.0;
                        res.AddRange(Approximate(par1, pmm, linesOnly, maxError));
                        res.AddRange(Approximate(pmm, par2, linesOnly, maxError));
                        return res;
                    }
                }
            }

        }
        /// <inheritdoc/>
        public override double GetArea()
        {
            double res = 0.0;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                res += subCurves[i].GetArea();
            }
            return res;
        }
        /// <inheritdoc/>
        public override ICurve2D GetModified(ModOp2D m)
        {
            ICurve2D[] mod = new ICurve2D[subCurves.Length];
            for (int i = 0; i < subCurves.Length; i++)
            {
                mod[i] = subCurves[i].GetModified(m);
            }
            return new Path2D(mod);
        }
        /// <inheritdoc/>
        public override bool IsValidParameter(double par)
        {
            double l = par * subCurves.Length;
            int i = (int)l; // (int) schneidet ab, also ist i der Index der Kurve
            if (i < 0)
            {
                i = 0;
                double p = l - i;
                return subCurves[0].IsValidParameter(p);
            }
            if (i >= subCurves.Length)
            {
                i = subCurves.Length - 1;
                double p = l - i;
                return subCurves[subCurves.Length - 1].IsValidParameter(p);
            }
            return true; // dazwischen ist natürlich alles erlaubt
        }
        /// <inheritdoc/>
        public override ICurve2D GetFused(ICurve2D toFuseWith, double precision)
        {
            return null;
        }
        /// <inheritdoc/>
        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {
            point = GeoPoint2D.Origin;
            deriv = deriv2 = GeoVector2D.NullVector;
            return false;
        }
        #endregion
        #region IQuadTreeInsertable Members

        /// <inheritdoc/>
        public override BoundingRect GetExtent()
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < subCurves.Length; ++i) res.MinMax(subCurves[i].GetExtent());
            return res;
        }

        /// <inheritdoc/>
        public override bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {
            for (int i = 0; i < subCurves.Length; ++i)
            {
                if (subCurves[i].HitTest(ref Rect, IncludeControlPoints)) return true;
            }
            return false;
        }

        #endregion
#if DEBUG
        internal DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    res.Add(subCurves[i], Color.Red, i);
                }
                return res;
            }
        }
#endif
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Path2D(SerializationInfo info, StreamingContext context)
        {
            subCurves = info.GetValue("SubCurves", typeof(ICurve2D[])) as ICurve2D[];
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("SubCurves", subCurves);
        }
        #endregion

        internal bool Reduce(double precision)
        {
            Reduce2D r2d = new Reduce2D();
            r2d.Precision = precision;
            r2d.OutputMode = Reduce2D.Mode.Paths;
            r2d.Add(subCurves);
            ICurve2D[] red = r2d.Reduced;
            if (red.Length == 1)
            {
                if (red[0] is Path2D)
                {
                    subCurves = (red[0] as Path2D).subCurves.Clone() as ICurve2D[];
                    return true;
                }
                else
                {   // eine einzige Kurve
                    subCurves = new ICurve2D[] { red[0] };
                    return true;
                }
            }
            return false;
        }
        internal Path2D[] SplitAtKink(double kinktol)
        {   // teilt an Knicken, geht davon aus, dass BSplines keine Knicke haben
            List<int> kinkpos = new List<int>();
            List<Path2D> res = new List<Path2D>();
            Path2D clone = this.Clone() as Path2D;
            clone.Flatten(); // hat nur BSplines und Linien (und Bögen)
            int startSegment = 0;
            for (int i = 1; i < clone.subCurves.Length; i++)
            {
                SweepAngle sw = new SweepAngle(clone.subCurves[i - 1].EndDirection, clone.subCurves[i].StartDirection);
                if (Math.Abs(sw.Radian) > kinktol)
                {
                    res.Add(clone.SubPath(startSegment, i));
                    startSegment = i;
                }
            }
            res.Add(clone.SubPath(startSegment, clone.SubCurvesCount));
            return res.ToArray();
        }

        /// <summary>
        /// Returns a copy of this path with all its vertices replaced by arcs of the given radius. A
        /// vertex where the two segments meet tangentially, or where the arc would not fit, is left as
        /// it is. For a closed path the vertex between the last and the first segment is rounded as well.
        /// </summary>
        /// <param name="radius">radius of the rounding arcs</param>
        public Path2D RoundVertices(double radius)
        {
            List<ICurve2D> resCurves = new List<ICurve2D>();
            Flatten();
            for (int i = 0; i < subCurves.Length; i++) resCurves.Add(subCurves[i].Clone());
            for (int i = 0; i < resCurves.Count; i++)
            {
                int next = i + 1;
                if (next >= resCurves.Count)
                {
                    if (IsClosed) next = 0;
                    else break;
                }
                GeoVector2D diri = resCurves[i].EndDirection;
                GeoVector2D dirn = resCurves[next].StartDirection;
                double dd = radius;
                // the normalized cross product tells which way the path bends, and how sharply
                double dir = (diri.x * dirn.y - diri.y * dirn.x) / (resCurves[i].Length * resCurves[next].Length);
                if (Math.Abs(dir) < 1e-3) continue; // tangential, there is no vertex to round
                if (dir > 0.0) dd = -dd;
                // the arc touches both segments, so its center sits where the two parallels meet
                ICurve2D p1 = resCurves[i].Parallel(dd, false, Precision.eps, 0.0);
                ICurve2D p2 = resCurves[next].Parallel(dd, false, Precision.eps, 0.0);
                if (p1 == null || p2 == null) continue;
                GeoPoint2DWithParameter[] ips = p1.Intersect(p2);
                double minDist = double.MaxValue;
                int found = -1;
                for (int j = 0; j < ips.Length; ++j)
                {
                    if (ips[j].par1 >= 0.0 && ips[j].par1 <= 1.0 && ips[j].par2 >= 0.0 && ips[j].par2 <= 1.0)
                    {
                        double d = ips[j].p | resCurves[i].EndPoint;
                        if (d < minDist)
                        {
                            minDist = d;
                            found = j;
                        }
                    }
                }
                if (found >= 0)
                {
                    ICurve2D c1 = resCurves[i].Trim(0.0, ips[found].par1);
                    ICurve2D c2 = resCurves[next].Trim(ips[found].par2, 1.0);
                    if (c1 != null && c2 != null)
                    {
                        Arc2D a = new Arc2D(ips[found].p, radius, c1.EndPoint, c2.StartPoint, dd < 0);
                        resCurves[i] = c1;
                        resCurves[next] = c2;
                        resCurves.Insert(next, a);
                        ++i; // skip the arc that was just inserted
                    }
                }
            }
            return new Path2D(resCurves.ToArray());
        }

        /// <summary>
        /// Creates a closed rectangle with rounded corners around <paramref name="center"/>, rotated by
        /// <paramref name="rotation"/> about that center.
        /// </summary>
        public static Path2D CreateRoundedRectangle(GeoPoint2D center, double width, double height, double cornerRadius, SweepAngle rotation)
        {
            if (cornerRadius > width / 2 || cornerRadius > height / 2) return null; // the corners would overlap
            GeoPoint2D[] vtx = new GeoPoint2D[4];
            GeoVector2D dx = cornerRadius * GeoVector2D.XAxis;
            GeoVector2D dy = cornerRadius * GeoVector2D.YAxis;
            // the four rectangle vertices, counterclockwise from the bottom left
            vtx[0] = new GeoPoint2D(center.x - width / 2, center.y - height / 2);
            vtx[1] = new GeoPoint2D(center.x + width / 2, center.y - height / 2);
            vtx[2] = new GeoPoint2D(center.x + width / 2, center.y + height / 2);
            vtx[3] = new GeoPoint2D(center.x - width / 2, center.y + height / 2);
            List<ICurve2D> res = new List<ICurve2D>();
            res.Add(new Line2D(vtx[0] + dx, vtx[1] - dx));
            res.Add(new Arc2D(vtx[1] - dx + dy, cornerRadius, vtx[1] - dx, vtx[1] + dy, true));
            res.Add(new Line2D(vtx[1] + dy, vtx[2] - dy));
            res.Add(new Arc2D(vtx[2] - dy - dx, cornerRadius, vtx[2] - dy, vtx[2] - dx, true));
            res.Add(new Line2D(vtx[2] - dx, vtx[3] + dx));
            res.Add(new Arc2D(vtx[3] + dx - dy, cornerRadius, vtx[3] + dx, vtx[3] - dy, true));
            res.Add(new Line2D(vtx[3] - dy, vtx[0] + dy));
            res.Add(new Arc2D(vtx[0] + dy + dx, cornerRadius, vtx[0] + dy, vtx[0] + dx, true));
            Path2D resPath = new Path2D(res.ToArray(), true);
            if (rotation != 0.0) resPath = resPath.GetModified(ModOp2D.Rotate(center, rotation)) as Path2D;
            return resPath;
        }

        private Path2D SubPath(int startSegment, int lastSegment)
        {
            ICurve2D[] sub = new ICurve2D[lastSegment - startSegment];
            for (int j = 0; j < sub.Length; j++)
            {
                sub[j] = subCurves[startSegment + j].Clone();
            }
            return new Path2D(sub);
        }

    }

    /// <summary>
    /// Internal helper for the hit test of extended paths or polylines. It is used only for searching the quad tree and
    /// therefore does not have to be exact: it consists of the curve itself plus the extensions of its first and its last
    /// segment. Both extensions are infinite in both directions, which is more than necessary (only the extension beyond
    /// the end would be needed), but harmless for this purpose. Used only for open polylines and paths.
    /// </summary>
    internal class InfinitePath2D : IQuadTreeInsertable
    {
        private IQuadTreeInsertable[] subCurves; // Unterkurven, werden nie verändert
        public InfinitePath2D(Polyline2D polyline)
        {	// drei Unterkurven, die PolyLine selbst, die verlängerte erste und letzte Linie
            subCurves = new IQuadTreeInsertable[3];
            subCurves[0] = polyline;
            Line2D l1 = new Line2D(polyline.GetVertex(0), polyline.GetVertex(1));
            Line2D l2 = new Line2D(polyline.GetVertex(polyline.VertexCount - 2), polyline.GetVertex(polyline.VertexCount - 1));
            subCurves[1] = l1.GetExtendedHitTest();
            subCurves[2] = l2.GetExtendedHitTest();
        }
        public InfinitePath2D(Path2D path)
        {
            ICurve2D[] pathSubCurves = path.SubCurves;
            subCurves = new IQuadTreeInsertable[pathSubCurves.Length];
            for (int i = 0; i < pathSubCurves.Length; ++i)
            {
                subCurves[i] = pathSubCurves[i] as IQuadTreeInsertable;
            }
            // komischerweise führt das unten stehende zu Problemen, deshalb die obige Schleife
            // subCurves = (IQuadTreeInsertable [])path.SubCurves; // ist schon ein Clone
            // wenn die Anzahl genau 1 ist, dann wird das GetExtendedHitTest() für das gleiche Objekt
            // zweimal aufgerufen und das geht in die Hose. Deshalb hier die Abfragen
            if (subCurves[0] is ICurve2D)
            {
                subCurves[0] = (subCurves[0] as ICurve2D).GetExtendedHitTest();
            }
            if (subCurves[subCurves.Length - 1] is ICurve2D)
            {
                subCurves[subCurves.Length - 1] = (subCurves[subCurves.Length - 1] as ICurve2D).GetExtendedHitTest() as IQuadTreeInsertable;
            }
        }
        #region IQuadTreeInsertable Members

        /// <inheritdoc/>
        public BoundingRect GetExtent()
        {
            // braucht nicht implementiert zu werden
            return new BoundingRect();
        }

        /// <inheritdoc/>
        public bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {
            for (int i = 0; i < subCurves.Length; ++i)
            {
                if (subCurves[i].HitTest(ref Rect, IncludeControlPoints)) return true;
            }
            return false;
        }
        public object ReferencedObject
        {
            get
            {
                return this;
            }
        }

        #endregion
    }
}

