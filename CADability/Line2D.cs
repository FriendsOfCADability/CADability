using CADability.GeoObject;
using System;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability.Curve2D
{
    /// <summary>
    /// Implements a line in 2D space. By implementing the ICurve2D interface this line
    /// can be handled as any 2D curve.
    /// </summary>
    [Serializable()]
    public class Line2D : GeneralCurve2D, ISerializable, IJsonSerialize
    {
        private GeoPoint2D startPoint;
        private GeoPoint2D endPoint;
        public Line2D(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            startPoint = StartPoint;
            endPoint = EndPoint;
        }
        /// <summary>
        /// Constructs a line passing through <paramref name="location"/> with the provided <paramref name="direction"/> clipped by the 
        /// rectangle <paramref name="clippedBy"/>
        /// </summary>
        /// <param name="location">Point on the (extension of the) line </param>
        /// <param name="direction">Direction of the line</param>
        /// <param name="clippedBy">Clipping rectangle</param>
        public Line2D(GeoPoint2D location, GeoVector2D direction, BoundingRect clippedBy)
        {
            direction.Norm(); // ist ja ne Kopie
            // das geht aber eleganter, oder?
            GeoPoint2D c = Geometry.DropPL(clippedBy.GetCenter(), location, direction);
            double l = clippedBy.Width + clippedBy.Height;
            startPoint = c + l * direction;
            endPoint = c - l * direction;
            ClipRect clr = new ClipRect(clippedBy);
            clr.ClipLine(ref startPoint, ref endPoint);
        }
        public override string ToString()
        {
            return "Line2D: (" + startPoint.DebugString + ") (" + endPoint.DebugString + ")";
        }
        #region ICurve2D Members
        internal override void GetTriangulationPoints(out GeoPoint2D[] interpol, out double[] interparam)
        {   // for GeneralCurve2D
            interpol = new GeoPoint2D[] { startPoint, endPoint };
            interparam = new double[] { 0.0, 1.0 };
        }
        public override GeoPoint2D StartPoint
        {
            get
            {
                return startPoint;
            }
            set
            {
                startPoint = value;
            }
        }
        public override GeoPoint2D EndPoint
        {
            get
            {
                return endPoint;
            }
            set
            {
                endPoint = value;
            }
        }
        public override GeoVector2D StartDirection
        {
            get
            {
                GeoVector2D v = endPoint - startPoint;
                return v;
            }
        }
        public override GeoVector2D EndDirection
        {
            get
            {
                return StartDirection;
            }
        }
        public override GeoVector2D MiddleDirection
        {
            get
            {
                return StartDirection;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
		public override GeoVector2D DirectionAt(double Position)
        {
            return StartDirection;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
		public override GeoPoint2D PointAt(double Position)
        {
            double dx = endPoint.x - startPoint.x;
            double dy = endPoint.y - startPoint.y;
            return new GeoPoint2D(startPoint.x + Position * dx, startPoint.y + Position * dy);
        }
        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {
            point = PointAt(position);
            deriv = endPoint - startPoint;
            deriv2 = GeoVector2D.NullVector;
            return true;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PositionOf (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
		public override double PositionOf(GeoPoint2D p)
        {
            double dx = endPoint.x - startPoint.x;
            double dy = endPoint.y - startPoint.y;
            if (Math.Abs(dx) > Math.Abs(dy)) return (p.x - startPoint.x) / dx;
            else return (p.y - startPoint.y) / dy;
        }
        public override double Length
        {
            get
            {
                return Geometry.Dist(startPoint, endPoint);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetAreaFromPoint (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
		public override double GetAreaFromPoint(GeoPoint2D p)
        {   // gesucht ist die vom Punkt p durch die Linie aufgespannte Fläche
            // mit Vorzeichen (gegen Uhrzeigersinn ist positiv)
            return ((startPoint.x - p.x) * (endPoint.y - p.y) - (startPoint.y - p.y) * (endPoint.x - p.x)) / 2.0;
        }
        public override double Sweep { get { return 0.0; } }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Split (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
		public override ICurve2D[] Split(double Position)
        {
            if (Position <= 0.0 || Position >= 1.0)
            {
                ICurve2D[] res = new ICurve2D[1];
                res[0] = Clone();
                return res;
            }
            else
            {
                ICurve2D[] res = new ICurve2D[2];
                res[0] = Clone().Trim(0.0, Position);
                res[1] = Clone().Trim(Position, 1.0);
                return res;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtent ()"/>
        /// </summary>
        /// <returns></returns>
		public override BoundingRect GetExtent()
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            res.MinMax(startPoint);
            res.MinMax(endPoint);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.HitTest (ref BoundingRect, bool)"/>
        /// </summary>
        /// <param name="Rect"></param>
        /// <param name="IncludeControlPoints"></param>
        /// <returns></returns>
		public override bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {
            ClipRect clr = new ClipRect(Rect);
            return clr.LineHitTest(startPoint, endPoint);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Distance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
		public override double Distance(GeoPoint2D p)
        {   // welcher Abstand ist hier gefragt? Die Parallelenkonstruktion erwartet den
            // Abstand von der unendlichen Linie. Braucht jemand was anderes?
            // Nachtrag: jedenfalls braucht die Tangentenkonstruktion auch diesen idealen Abstand
            //			double par = PositionOf(p);
            //			if (par<=0.0) return Geometry.dist(p,startPoint);
            //			else if (par>=1.0) return Geometry.dist(p,endPoint);
            //			else
            //			{
            double dx = endPoint.x - startPoint.x;
            double dy = endPoint.y - startPoint.y;
            double d = Math.Sqrt(dx * dx + dy * dy);
            if (d > 0.0) return -((dy * p.x - dx * p.y + startPoint.y * endPoint.x - startPoint.x * endPoint.y) / d);
            else return Geometry.Dist(p, startPoint);
            //			}
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MinDistance (ICurve2D)"/>
        /// </summary>
        /// <param name="Other"></param>
        /// <returns></returns>
		public override double MinDistance(ICurve2D Other)
        {
            if (Other is Line2D)
            {
                Line2D l = Other as Line2D;
                // schneller Vorabtest auf Schnitt: (noch nicht getestet)
                double f1 = (startPoint.x - endPoint.x) * (l.endPoint.y - endPoint.y) - (startPoint.y - endPoint.y) * (l.endPoint.x - endPoint.x);
                double f2 = (startPoint.x - endPoint.x) * (l.startPoint.y - endPoint.y) - (startPoint.y - endPoint.y) * (l.startPoint.x - endPoint.x);
                if (f1 * f2 < 0.0)
                {   // verschiedenes Vorzeichen
                    double f3 = (l.startPoint.x - l.endPoint.x) * (endPoint.y - l.endPoint.y) - (l.startPoint.y - l.endPoint.y) * (endPoint.x - l.endPoint.x);
                    double f4 = (l.startPoint.x - l.endPoint.x) * (startPoint.y - l.endPoint.y) - (l.startPoint.y - l.endPoint.y) * (startPoint.x - l.endPoint.x);
                    if (f3 * f4 < 0.0) return 0.0; // echter Schnittpunkt
                }
                double minDist = double.MaxValue;
                double dx1 = endPoint.x - startPoint.x;
                double dy1 = endPoint.y - startPoint.y;
                bool DoDx1 = Math.Abs(dx1) > Math.Abs(dy1);
                double dx2 = l.endPoint.x - l.startPoint.x;
                double dy2 = l.endPoint.y - l.startPoint.y;
                bool DoDx2 = Math.Abs(dx2) > Math.Abs(dy2);
                double d;
                // Berechnung der Fußpunktabstände, wenn sie auf die andere Linie treffen
                GeoPoint2D p = Geometry.DropPL(l.startPoint, startPoint, endPoint);
                if (DoDx1) d = (p.x - startPoint.x) / dx1;
                else d = (p.y - startPoint.y) / dy1;
                if (d >= 0.0 && d <= 1.0) minDist = Math.Min(minDist, Geometry.Dist(p, l.startPoint));
                p = Geometry.DropPL(l.endPoint, startPoint, endPoint);
                if (DoDx1) d = (p.x - startPoint.x) / dx1;
                else d = (p.y - startPoint.y) / dy1;
                if (d >= 0.0 && d <= 1.0) minDist = Math.Min(minDist, Geometry.Dist(p, l.endPoint));
                p = Geometry.DropPL(startPoint, l.startPoint, l.endPoint);
                if (DoDx2) d = (p.x - l.startPoint.x) / dx2;
                else d = (p.y - l.startPoint.y) / dy2;
                if (d >= 0.0 && d <= 1.0) minDist = Math.Min(minDist, Geometry.Dist(p, startPoint));
                p = Geometry.DropPL(endPoint, l.startPoint, l.endPoint);
                if (DoDx2) d = (p.x - l.startPoint.x) / dx2;
                else d = (p.y - l.startPoint.y) / dy2;
                if (d >= 0.0 && d <= 1.0) minDist = Math.Min(minDist, Geometry.Dist(p, endPoint));
                if (minDist == double.MaxValue)
                {   // kein Fußpunkt auf der anderen Linie: die gegenseitigen Start/Endpunkt
                    // Abstände verwenden
                    minDist = Math.Min(minDist, Geometry.Dist(l.startPoint, endPoint));
                    minDist = Math.Min(minDist, Geometry.Dist(l.endPoint, endPoint));
                    minDist = Math.Min(minDist, Geometry.Dist(l.startPoint, startPoint));
                    minDist = Math.Min(minDist, Geometry.Dist(l.endPoint, startPoint));
                }
                return minDist;
            }
            else if (Other is Arc2D)
            {
                double minDist = Curves2D.SimpleMinimumDistance(this, Other);
                Arc2D a = Other as Arc2D;
                GeoPoint2D[] fp = PerpendicularFoot(a.Center);
                for (int i = 0; i < fp.Length; ++i)
                {
                    GeoPoint2D p = fp[i];
                    double pos = PositionOf(p);
                    if (pos >= 0.0 && pos <= 1.0)
                    {
                        pos = a.PositionOf(p);
                        if (pos >= 0.0 && pos <= 1.0)
                        {
                            double d = Geometry.Dist(p, a.Center);
                            if (d > a.Radius) minDist = Math.Min(minDist, d - a.Radius);
                        }
                    }
                }
                return minDist;
            }
            else if (Other is Circle2D)
            {
                double minDist = Curves2D.SimpleMinimumDistance(this, Other);
                Circle2D c = Other as Circle2D;
                GeoPoint2D[] fp = PerpendicularFoot(c.Center);
                for (int i = 0; i < fp.Length; ++i)
                {
                    GeoPoint2D p = fp[i];
                    double pos = PositionOf(p);
                    if (pos >= 0.0 && pos <= 1.0)
                    {
                        double d = Geometry.Dist(p, c.Center);
                        if (d > c.Radius) minDist = Math.Min(minDist, d - c.Radius);
                    }
                }
                return minDist;
            }
            else
            {
                return base.MinDistance(Other);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Trim (double, double)"/>
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="EndPos"></param>
        /// <returns></returns>
		public override ICurve2D Trim(double StartPos, double EndPos)
        {
            GeoPoint2D newStartPoint = PointAt(StartPos);
            GeoPoint2D newEndPoint = PointAt(EndPos);
            return new Line2D(newStartPoint, newEndPoint);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Parallel (double, bool, double, double)"/>
        /// </summary>
        /// <param name="Dist"></param>
        /// <param name="approxSpline"></param>
        /// <param name="precision"></param>
        /// <param name="roundAngle"></param>
        /// <returns></returns>
		public override ICurve2D Parallel(double Dist, bool approxSpline, double precision, double roundAngle)
        {
            ICurve2D res = Clone();

            double dx = endPoint.x - startPoint.x;
            double dy = endPoint.y - startPoint.y;
            double ab = Math.Sqrt(dx * dx + dy * dy);
            if (ab > 0.0)
            {
                // Dist>0: rechte Seite
                double rx = Dist * dy / ab;
                double ry = Dist * dx / ab;
                res.StartPoint = new GeoPoint2D(startPoint, rx, -ry);
                res.EndPoint = new GeoPoint2D(endPoint, rx, -ry);
            }
            else
            {
                throw new Curve2DException("Parallel Null-Line", Curve2DException.Curve2DExceptionType.LineIsNull);
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (ICurve2D)"/>
        /// </summary>
        /// <param name="IntersectWith"></param>
        /// <returns></returns>
		public override GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {   // gesucht sind alle Schnittpunkte, also auch in der Verlängerung!
            Line2D l2d = IntersectWith as Line2D;
            if (l2d != null)
            {
                GeoPoint2D ip;
                if (Geometry.IntersectLL(startPoint, endPoint, l2d.StartPoint, l2d.EndPoint, out ip))
                {
                    double pos1 = this.PositionOf(ip);
                    double pos2 = l2d.PositionOf(ip);
                    GeoPoint2DWithParameter pwp = new GeoPoint2DWithParameter();
                    pwp.p = ip;
                    pwp.par1 = pos1;
                    pwp.par2 = pos2;
                    return new GeoPoint2DWithParameter[] { pwp };
                }
                else
                    return new GeoPoint2DWithParameter[0];
            }
            Circle2D c2d = IntersectWith as Circle2D;
            if (c2d != null)
            {
                GeoPoint2D[] isp = Geometry.IntersectLC(startPoint, endPoint, c2d.Center, c2d.Radius);
                GeoPoint2DWithParameter[] res = new GeoPoint2DWithParameter[isp.Length];
                for (int i = 0; i < isp.Length; ++i)
                {
                    res[i].p = isp[i];
                    res[i].par1 = this.PositionOf(isp[i]);
                    res[i].par2 = c2d.PositionOf(isp[i]);
                }
                return res;
            }
            Ellipse2D e2d = IntersectWith as Ellipse2D;
            if (e2d != null)
            {
                GeoPoint2D[] isp = Geometry.IntersectEL(e2d.center, e2d.majorAxis.Length, e2d.minorAxis.Length, e2d.majorAxis.Angle, startPoint, endPoint);
                GeoPoint2DWithParameter[] res = new GeoPoint2DWithParameter[isp.Length];
                for (int i = 0; i < isp.Length; ++i)
                {
                    res[i].p = isp[i];
                    res[i].par1 = this.PositionOf(isp[i]);
                    res[i].par2 = e2d.PositionOf(isp[i]);
                }
                return res;
            }
            Polyline2D p2d = IntersectWith as Polyline2D;
            if (p2d != null)
            {
                GeoPoint2DWithParameter[] res = p2d.Intersect(this); // sorum geht es
                for (int i = 0; i < res.Length; ++i)
                {
                    double t = res[i].par1;
                    res[i].par1 = res[i].par2;
                    res[i].par2 = t;
                }
                return res;
            }
            Path2D pa2d = IntersectWith as Path2D;
            if (pa2d != null)
            {
                GeoPoint2DWithParameter[] res = pa2d.Intersect(this); // sorum geht es
                for (int i = 0; i < res.Length; ++i)
                {
                    double t = res[i].par1;
                    res[i].par1 = res[i].par2;
                    res[i].par2 = t;
                }
                return res;
            }
            BSpline2D b2d = IntersectWith as BSpline2D;
            if (b2d != null)
            {
                GeoPoint2DWithParameter[] res = b2d.Intersect(this); // sorum geht es
                for (int i = 0; i < res.Length; ++i)
                {
                    double t = res[i].par1;
                    res[i].par1 = res[i].par2;
                    res[i].par2 = t;
                }
                return res;
            }
            return base.Intersect(IntersectWith);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PerpendicularFoot (GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <returns></returns>
		public override GeoPoint2D[] PerpendicularFoot(GeoPoint2D FromHere)
        {
            GeoPoint2D f = Geometry.DropPL(FromHere, startPoint, endPoint);
            return new GeoPoint2D[] { f };
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.TangentPoints (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
		public override GeoPoint2D[] TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo)
        {
            return new GeoPoint2D[0];
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.TangentPointsToAngle (Angle, GeoPoint2D)"/>
        /// </summary>
        /// <param name="ang"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
		public override GeoPoint2D[] TangentPointsToAngle(Angle ang, GeoPoint2D CloseTo)
        {
            return new GeoPoint2D[0];
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="StartPoint"></param>
        /// <param name="EndPoint"></param>
        /// <returns></returns>
		public override GeoPoint2DWithParameter[] Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            Line2D lp = new Line2D(StartPoint, EndPoint);
            return Intersect(lp);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Reverse ()"/>
        /// </summary>
		public override void Reverse()
        {
            GeoPoint2D p = startPoint;
            startPoint = endPoint;
            endPoint = p;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Move (double, double)"/>
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        public override void Move(double dx, double dy)
        {
            startPoint.x += dx;
            startPoint.y += dy;
            endPoint.x += dx;
            endPoint.y += dy;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetModified (ModOp2D)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ICurve2D GetModified(ModOp2D m)
        {   // allgemeine Lösung, wird für Kreis und Ellipse übernommen
            Line2D res = Clone() as Line2D;
            res.startPoint = m * startPoint;
            res.endPoint = m * endPoint;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ICurve2D Clone()
        {
            Line2D res = new Line2D(StartPoint, EndPoint);
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.CloneReverse (bool)"/>
        /// </summary>
        /// <param name="reverse"></param>
        /// <returns></returns>
		public override ICurve2D CloneReverse(bool reverse)
        {
            ICurve2D res = this.Clone();
            if (reverse) res.Reverse();
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Copy (ICurve2D)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public override void Copy(ICurve2D toCopyFrom)
        {
            Line2D c = toCopyFrom as Line2D;
            startPoint = c.startPoint;
            endPoint = c.endPoint;
            UserData.CloneFrom(c.UserData);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MakeGeoObject (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
		public override IGeoObject MakeGeoObject(Plane p)
        {
            Line l = Line.Construct();
            l.StartPoint = p.ToGlobal(startPoint);
            l.EndPoint = p.ToGlobal(endPoint);
            return l;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Project (Plane, Plane)"/>
        /// </summary>
        /// <param name="fromPlane"></param>
        /// <param name="toPlane"></param>
        /// <returns></returns>
		public override ICurve2D Project(Plane fromPlane, Plane toPlane)
        {
            Line2D l = new Line2D(toPlane.Project(fromPlane.ToGlobal(startPoint)), toPlane.Project(fromPlane.ToGlobal(endPoint)));
            if (l.Length > Precision.eps) return l;
            else return null;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.AddToGraphicsPath (System.Drawing.Drawing2D.GraphicsPath, bool)"/>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="forward"></param>
		public override void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {
            if (forward)
            {
                path.AddLine((float)startPoint.x, (float)startPoint.y, (float)endPoint.x, (float)endPoint.y);
            }
            else
            {
                path.AddLine((float)endPoint.x, (float)endPoint.y, (float)startPoint.x, (float)startPoint.y);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtendedHitTest ()"/>
        /// </summary>
        /// <returns></returns>
		public override IQuadTreeInsertable GetExtendedHitTest()
        {
            return new InfiniteLine2D(startPoint, endPoint);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Approximate (bool, double)"/>
        /// </summary>
        /// <param name="linesOnly"></param>
        /// <param name="maxError"></param>
        /// <returns></returns>
		public override ICurve2D Approximate(bool linesOnly, double maxError)
        {
            return Clone();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetArea ()"/>
        /// </summary>
        /// <returns></returns>
        public override double GetArea()
        {
            return (startPoint.x * endPoint.y - startPoint.y * endPoint.x) / 2.0;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetFused (ICurve2D, double)"/>
        /// </summary>
        /// <param name="toFuseWith"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override ICurve2D GetFused(ICurve2D toFuseWith, double precision)
        {
            if (toFuseWith is Line2D)
            {
                Line2D l1, l2;
                if (this.Length > toFuseWith.Length)
                {
                    l1 = this;
                    l2 = toFuseWith as Line2D;
                }
                else
                {
                    l1 = toFuseWith as Line2D;
                    l2 = this;
                }
                // Sonderfall: beides Null-Linien (entstehen oft bei der Projektion
                if (l1.Length < Precision.eps)
                {
                    if (Precision.IsEqual(l1.startPoint, l2.startPoint)) return l1.Clone();
                    else return null;
                }
                if (Math.Abs(Geometry.DistPL(l2.startPoint, l1.startPoint, l1.endPoint)) < precision &&
                    Math.Abs(Geometry.DistPL(l2.endPoint, l1.startPoint, l1.endPoint)) < precision)
                {
                    double pos1 = l1.PositionOf(l2.startPoint);
                    double pos2 = l1.PositionOf(l2.endPoint);
                    double eps = precision / l1.Length;
                    if ((pos1 > -eps && pos1 < 1 + eps) || (pos2 > -eps && pos2 < 1 + eps))
                    {
                        GeoPoint2D sp = l1.PointAt(Math.Min(Math.Min(pos1, pos2), 0.0));
                        GeoPoint2D ep = l1.PointAt(Math.Max(Math.Max(pos1, pos2), 1.0));
                        // sp und ep wurden jetzt bezüglich der längeren Linie (l1) berechnet, aber wenn es nur ein kleiner Knick ist, dann
                        // und l2 teilweise außerhalb von l1 liegt. dann l2s Start- bzw. Endpunkt verwenden
                        if (pos1 < 0 && pos1 < pos2)
                        {
                            sp = l2.startPoint;
                        }
                        if (pos2 < 0 && pos2 < pos1)
                        {
                            sp = l2.endPoint;
                        }
                        if (pos1 > 1.0 && pos1 > pos2)
                        {
                            ep = l2.startPoint;
                        }
                        if (pos2 > 1.0 && pos2 > pos1)
                        {
                            ep = l2.endPoint;
                        }

                        Line2D res = new Line2D(sp, ep);
                        return res;
                    }
                }
            }
            return null;
        }
        #endregion

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Line2D(SerializationInfo info, StreamingContext context)
        {
            startPoint = (GeoPoint2D)info.GetValue("StartPoint", typeof(GeoPoint2D));
            endPoint = (GeoPoint2D)info.GetValue("EndPoint", typeof(GeoPoint2D));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("StartPoint", startPoint);
            info.AddValue("EndPoint", endPoint);
        }
        protected Line2D() { } // needed for IJsonSerialize
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("StartPoint", startPoint);
            data.AddProperty("EndPoint", endPoint);
        }

        public void SetObjectData(IJsonReadData data)
        {
            startPoint = data.GetProperty<GeoPoint2D>("StartPoint");
            endPoint = data.GetProperty<GeoPoint2D>("EndPoint");
        }

        #endregion

    }
    internal class InfiniteLine2D : IQuadTreeInsertable, I2DIntersectable
    {
        private ModOp2D toXAxis; // dreht und verschiebt diese Linie so, dass sie zur x-Achse wird
        private GeoPoint2D startPoint;
        private GeoVector2D direction;
        public InfiniteLine2D(GeoPoint2D startPoint, GeoPoint2D endPoint)
        {
            this.startPoint = startPoint;
            direction = endPoint - startPoint;
            Angle a = direction.Angle;
            if (Precision.IsEqual(a, Angle.A0) || Precision.IsEqual(a, Angle.A180))
            {
                toXAxis = ModOp2D.Translate(0.0, -startPoint.y);
            }
            else
            {
                double l = -startPoint.y / direction.y; // != 0.0, da nicht horizontal
                double x = startPoint.x + l * direction.x;
                // double y = startPoint.y+l*direction.y;
                toXAxis = ModOp2D.Rotate(new GeoPoint2D(x, 0.0), new SweepAngle(-a));
            }
        }
        #region IQuadTreeInsertable Members

        /// <summary>
        /// Implements <see cref="CADability.IQuadTreeInsertable.GetExtent ()"/>
        /// </summary>
        /// <returns></returns>
        public BoundingRect GetExtent()
        {
            return BoundingRect.EmptyBoundingRect; // sollte nie aufgerufen werden
        }

        /// <summary>
        /// Implements <see cref="CADability.IQuadTreeInsertable.HitTest (ref BoundingRect, bool)"/>
        /// </summary>
        /// <param name="Rect"></param>
        /// <param name="IncludeControlPoints"></param>
        /// <returns></returns>
		public bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {   // die 4 Eckpunkte des Rechtecks werden in das System gebracht, in dem die Linie
            // die X-Achse ist. Wenn alle positiv oder alle negativ sind, dann nicht getroffen.
            GeoPoint2D ll = toXAxis * new GeoPoint2D(Rect.Left, Rect.Bottom);
            if (ll.y > 0.0)
            {
                GeoPoint2D lr = toXAxis * new GeoPoint2D(Rect.Right, Rect.Bottom);
                if (lr.y <= 0.0) return true;
                GeoPoint2D ur = toXAxis * new GeoPoint2D(Rect.Right, Rect.Top);
                if (ur.y <= 0.0) return true;
                GeoPoint2D ul = toXAxis * new GeoPoint2D(Rect.Left, Rect.Top);
                if (ul.y <= 0.0) return true;
                return false;
            }
            else
            {
                GeoPoint2D lr = toXAxis * new GeoPoint2D(Rect.Right, Rect.Bottom);
                if (lr.y >= 0.0) return true;
                GeoPoint2D ur = toXAxis * new GeoPoint2D(Rect.Right, Rect.Top);
                if (ur.y >= 0.0) return true;
                GeoPoint2D ul = toXAxis * new GeoPoint2D(Rect.Left, Rect.Top);
                if (ul.y >= 0.0) return true;
                return false;
            }
        }

        GeoPoint2D[] I2DIntersectable.IntersectWith(I2DIntersectable other)
        {
            if (other is InfiniteLine2D)
            {
                GeoPoint2D res;
                if (Geometry.IntersectLL(startPoint, direction, (other as InfiniteLine2D).startPoint, (other as InfiniteLine2D).direction, out res)) return new GeoPoint2D[] { res };
                return new GeoPoint2D[0];
            }
            if (other is ICurve2D)
            {
                BoundingRect ext = (other as ICurve2D).GetExtent();

            }
            throw new NotImplementedException("I2DIntersectable.IntersectWith " + other.GetType().Name);
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
    internal class SemiInfiniteAxis2D : IQuadTreeInsertable
    {
        public enum Direction { toLeft, toRight, toBottom, toTop }
        GeoPoint2D startPoint;
        Direction direction;
        public SemiInfiniteAxis2D(GeoPoint2D startPoint, Direction direction)
        {
            this.startPoint = startPoint;
            this.direction = direction;
        }
        #region IQuadTreeInsertable Members

        /// <summary>
        /// Implements <see cref="CADability.IQuadTreeInsertable.GetExtent ()"/>
        /// </summary>
        /// <returns></returns>
        public BoundingRect GetExtent()
        {
            return BoundingRect.EmptyBoundingRect; // sollte nie aufgerufen werden
        }

        /// <summary>
        /// Implements <see cref="CADability.IQuadTreeInsertable.HitTest (ref BoundingRect, bool)"/>
        /// </summary>
        /// <param name="Rect"></param>
        /// <param name="IncludeControlPoints"></param>
        /// <returns></returns>
        public bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {
            switch (direction)
            {
                case Direction.toLeft:
                    if (Rect.Left > startPoint.x) return false;
                    return Rect.Bottom <= startPoint.y && Rect.Top >= startPoint.y;
                case Direction.toRight:
                    if (Rect.Right < startPoint.x) return false;
                    return Rect.Bottom <= startPoint.y && Rect.Top >= startPoint.y;
                case Direction.toBottom:
                    if (Rect.Bottom > startPoint.y) return false;
                    return Rect.Left <= startPoint.x && Rect.Right >= startPoint.x;
                case Direction.toTop:
                    if (Rect.Top < startPoint.y) return false;
                    return Rect.Left <= startPoint.x && Rect.Right >= startPoint.x;
                default:
                    return false;
            }
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
