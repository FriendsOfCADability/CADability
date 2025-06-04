using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability.Curve2D
{
    /// <summary>
    /// Describes a circular Arc in 2D. Implements the ICurve2D interface.
    /// </summary>
    [Serializable()]
    public class Arc2D : Circle2D, ISerializable
    {
        private Angle start;
        private SweepAngle sweep;
        /// <summary>
        /// Constructs a circular arc in 2D. 
        /// </summary>
        /// <param name="center">Center of the arc</param>
        /// <param name="radius">Radius of the Arc</param>
        /// <param name="start">Startangle of the arc (x-axis is 0)</param>
        /// <param name="sweep">Sweepangle of the arc -2*pi&lt;=s&lt;=2*pi</param>
        public Arc2D(GeoPoint2D center, double radius, Angle start, SweepAngle sweep)
            : base(center, radius)
        {
            this.start = start;
            this.sweep = sweep;
        }
        public Arc2D(GeoPoint2D center, double radius, GeoPoint2D StartPoint, GeoPoint2D EndPoint, bool counterclock)
            : base(center, radius)
        {
            start = new Angle(StartPoint, center);
            sweep = new SweepAngle(StartPoint - center, EndPoint - center);
            // sweep ist zwischen -pi und +pi, also der Bogen ist maximal ein Halbkreis.
            // counterclock gibt aber die Richtung vor, deshalb ggf. umdrehen
            if ((StartPoint | EndPoint) < radius * 1e-10)
            {
                if (counterclock) sweep = SweepAngle.Full;
                else sweep = SweepAngle.FullReverse;
            }
            if (counterclock && sweep <= 0.0) sweep += 2.0 * Math.PI;
            if (!counterclock && sweep >= 0.0) sweep -= 2.0 * Math.PI;
        }
        static public Arc2D From3Points(GeoPoint2D sp, GeoPoint2D mp, GeoPoint2D ep)
        {
            GeoPoint2D c;
            if (Geometry.IntersectLL(new GeoPoint2D(sp, mp), (mp - sp).ToLeft(), new GeoPoint2D(mp, ep), (ep - mp).ToLeft(), out c))
            {
                double r = ((c | sp) + (c | ep)) / 2.0;
                bool cc = Geometry.OnLeftSide(c, sp, mp);
                return new Arc2D(c, r, sp, ep, cc);
            }
            return null;
        }
        static public Arc2D From2PointsAndTangents(GeoPoint2D sp, GeoVector2D sd, GeoPoint2D ep, GeoVector2D ed)
        {
            if (Geometry.IntersectLL(sp, sd.ToLeft(), ep, ed.ToLeft(), out GeoPoint2D mp))
            {
                bool cc = Geometry.OnLeftSide(mp, sp, ep);
                return new Arc2D(mp, sp | mp, sp, ep, cc);
            }
            else return null;
        }

        public double StartParameter
        {
            get
            {
                return start;
            }
        }
        internal Angle StartAngle
        {
            get
            {
                return start;
            }
            set
            {
                start = value;
            }
        }
        public double EndParameter
        {
            get
            {
                return start + sweep;
            }
        }
        public SweepAngle SweepAngle
        {
            get
            {
                return sweep;
            }
        }
        public double SegmentArea
        {
            get
            {
                return Radius * Radius * (Math.Abs(sweep.Radian) - Math.Sin(Math.Abs(sweep.Radian))) / 2.0;
            }
        }
        public override string ToString()
        {
            return "Arc2D: (" + Center.DebugString + ") " + Radius.ToString(DebugFormat.Coordinate) + ", " + this.StartPoint.DebugString + "->" + this.EndPoint.DebugString + " start: " + this.start.ToString() + " sweep: " + this.sweep.ToString();
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
            Arc2D res;
            double newRadius;	// Abstand nach rechts im Sinne der Richtung des Bogens
            if (sweep > 0.0) newRadius = Radius + Dist;
            else newRadius = Radius - Dist;
            if (newRadius > 0.0) res = new Arc2D(Center, newRadius, start, sweep);
            else res = new Arc2D(Center, -newRadius, start + SweepAngle.Opposite, sweep);
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PositionOf (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double PositionOf(GeoPoint2D p)
        {	// noch ausprobieren!
            Angle a = new Angle(p, Center);
            double a1 = (a.Radian - start.Radian) / sweep;
            double a2 = (a.Radian - start.Radian + 2.0 * Math.PI) / sweep;
            double a3 = (a.Radian - start.Radian - 2.0 * Math.PI) / sweep;
            // welcher der 3 Werte ist näher an 0.5 ?
            double ax = a1;
            if (Math.Abs(a2 - 0.5) < Math.Abs(ax - 0.5)) ax = a2;
            if (Math.Abs(a3 - 0.5) < Math.Abs(ax - 0.5)) ax = a3;
            return ax;
        }
        internal double PositionOf(GeoPoint2D p, bool beforeStart)
        {	// noch ausprobieren!
            Angle a = new Angle(p, Center);
            double a1 = (a.Radian - start.Radian) / sweep;
            double a2 = (a.Radian - start.Radian + 2.0 * Math.PI) / sweep;
            double a3 = (a.Radian - start.Radian - 2.0 * Math.PI) / sweep;
            // welcher der 3 Werte ist näher an 0.5 ?
            double eps = 1e-6;
            if (beforeStart)
            {   // Suche Lösung auf dem Boden oder vor dem Anfang (Endpunkt selbst mit eingeschlossen)
                double ax = a1;
                if (ax > 1.0 + eps) ax = double.MinValue;
                if ((1.0 + eps - a2) > 0 && a2 > ax) ax = a2;
                if ((1.0 + eps - a3) > 0 && a3 > ax) ax = a3;
                return ax;
            }
            else
            {   // Suche Lösung auf dem Bogen oder nach dem Ende (Anfangspunkt selbst mit eingeschlossen)
                double ax = a1;
                if (ax < -eps) ax = double.MaxValue;
                if (a2 > -eps && a2 < ax) ax = a2;
                if (a3 > -eps && a3 < ax) ax = a3;
                return ax;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoPoint2D PointAt(double Position)
        {
            Angle a;
            a = start + Position * sweep;
            return new GeoPoint2D(Center, Radius, a);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoVector2D DirectionAt(double Position)
        {
            GeoVector2D r = (Length / Radius) * (PointAt(Position) - Center); // introduced factor (31.1.18) because of HelicalSurface, NewtonLineIntersection
            if (sweep > 0.0) return r.ToLeft();
            else return r.ToRight();
        }
        public override double Length
        {
            get
            {
                return Radius * Math.Abs(sweep);
            }
        }
        public override double Sweep { get { return sweep; } }
        public override bool IsClosed
        {
            get
            {   // es zählt nur der Vollkreis
                return Math.Abs(sweep.Radian) > Math.PI && Precision.IsEqual(StartPoint, EndPoint);
            }
        }
        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {
            double par = start + position * sweep.Radian;
            if (sweep.Radian > 0)
            {
                point = new GeoPoint2D(Center.x + Radius * Math.Cos(par), Center.y + Radius * Math.Sin(par));
                deriv = (sweep.Radian) * new GeoVector2D(-Radius * Math.Sin(par), Radius * Math.Cos(par));
                deriv2 = (sweep.Radian) * (sweep.Radian) * new GeoVector2D(-Radius * Math.Cos(par), -Radius * Math.Sin(par));
            }
            else
            {
                point = new GeoPoint2D(Center.x + Radius * Math.Cos(-par), Center.y + Radius * Math.Sin(-par));
                deriv = Math.Abs(sweep.Radian) * new GeoVector2D(Radius * Math.Sin(-par), -Radius * Math.Cos(-par));
                deriv2 = (sweep.Radian) * (sweep.Radian) * new GeoVector2D(-Radius * Math.Cos(-par), -Radius * Math.Sin(-par));
            }
            return true;
        }
        internal void Close()
        {
            if (sweep > 0) sweep = Math.PI * 2.0;
            else sweep = -Math.PI * 2.0;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtent ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingRect GetExtent()
        {
            int StartQuadrant = start.Quadrant;
            int EndQuadrant = (start + sweep).Quadrant;
            int numQuads;
            if (sweep < 0.0) numQuads = StartQuadrant - EndQuadrant;
            else numQuads = EndQuadrant - StartQuadrant;
            if (numQuads < 0) numQuads += 4;
            // 19.8.15: die folgende Zeile verworfen, dafür die Abfrage "if (EndQuadrant == StartQuadrant)" wieder aktiviert
            // führt definitiv zum falschen Ergebnis, wenn mehr als 270° und eine Achse nicht im Bogen
            // if (Math.Abs(sweep) > 3 * Math.PI / 2) numQuads = 4; // mehr als 270°
            if (Math.Abs(sweep) >= 2.0 * Math.PI - 1e-7)
            {   // bei ganz rum und start auf 360° gabs ein Problem
                numQuads = 4;
            }
            else if (EndQuadrant == StartQuadrant)
            {	// muss entweder kleiner 90° oder größer 270° sein, d.h. wenns mehr als halbrum geht, dann alle Quadranten betroffen
                if (Math.Abs(sweep) > Math.PI) numQuads = 4;
            }
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            if (sweep < 0.0)
            {	// rechtsrum
                int q = StartQuadrant;
                for (int i = 0; i < numQuads; ++i)
                {
                    switch (q) // Achse zur Rechten betrachten
                    {
                        case 0: res.MinMax(new GeoPoint2D(Center.x + Radius, Center.y)); break;
                        case 1: res.MinMax(new GeoPoint2D(Center.x, Center.y + Radius)); break;
                        case 2: res.MinMax(new GeoPoint2D(Center.x - Radius, Center.y)); break;
                        case 3: res.MinMax(new GeoPoint2D(Center.x, Center.y - Radius)); break;
                    }
                    q -= 1;
                    if (q < 0) q += 4;
                }
            }
            else
            {	// linksrum
                int q = StartQuadrant;
                if (q > 3) q -= 4;
                for (int i = 0; i < numQuads; ++i)
                {
                    switch (q) // Achse zur Linken betrachten
                    {
                        case 4:
                        case 0: res.MinMax(new GeoPoint2D(Center.x, Center.y + Radius)); break;
                        case 1: res.MinMax(new GeoPoint2D(Center.x - Radius, Center.y)); break;
                        case 2: res.MinMax(new GeoPoint2D(Center.x, Center.y - Radius)); break;
                        case 3: res.MinMax(new GeoPoint2D(Center.x + Radius, Center.y)); break;
                    }
                    q += 1;
                    if (q > 3) q -= 4;
                }
            }
            res.MinMax(StartPoint);
            res.MinMax(EndPoint);
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
            if (sweep < 0)
            {   // 19.8.15: bin nicht sicher, ob das mit negativem sweep auch geht, deshalb hier umdrehen
                return (new Arc2D(Center, Radius, start + sweep, -sweep)).HitTest(ref Rect, IncludeControlPoints);
            }
            ClipRect clr = new ClipRect(Rect);

            int StartQuadrant = start.Quadrant;
            int EndQuadrant = (start + sweep).Quadrant;
            int numQuads;
            if (sweep < 0.0) numQuads = StartQuadrant - EndQuadrant;
            else numQuads = EndQuadrant - StartQuadrant;
            if (numQuads < 0) numQuads += 4;
            // siehe bei GetExtent, die folgende Abfrage war falsch!
            // if (Math.Abs(sweep) > 2 * Math.PI / 3) numQuads = 4; // mehr als 270°
            if (EndQuadrant == StartQuadrant)
            {	// wenns mehr als halbrum geht, dann ganzrum
                if (Math.Abs(sweep) > Math.PI) numQuads = 4;
            }
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            int q = StartQuadrant;
            // im Folgenden die ganz überschrittenen Quadranten betrachten, also ganze Viertelbögen
            for (int i = 1; i < numQuads; ++i) // [hier war < numQuads, es muss aber <= heißen, sonst funktionierts nicht immer]<- das ist falsch, es muss < sein, 
            {	// ganze Viertelbögen betrachten
                // q startet mit dem StartQuadranten, also zuerst quadranten weiterschalten
                if (sweep < 0.0) q -= 1;
                else q += 1;
                if (q < 0) q += 4;
                if (q > 3) q -= 4;
                switch (q) // Achse zur Rechten betrachten
                {
                    case 0:
                        if (clr.ArcHitTest(Center, Radius, 0, new GeoPoint2D(Center.x + Radius, Center.y), new GeoPoint2D(Center.x, Center.y + Radius))) return true;
                        break;
                    case 1:
                        if (clr.ArcHitTest(Center, Radius, 1, new GeoPoint2D(Center.x, Center.y + Radius), new GeoPoint2D(Center.x - Radius, Center.y))) return true;
                        break;
                    case 2:
                        if (clr.ArcHitTest(Center, Radius, 2, new GeoPoint2D(Center.x - Radius, Center.y), new GeoPoint2D(Center.x, Center.y - Radius))) return true;
                        break;
                    case 3:
                        if (clr.ArcHitTest(Center, Radius, 3, new GeoPoint2D(Center.x, Center.y - Radius), new GeoPoint2D(Center.x + Radius, Center.y))) return true;
                        break;
                }
            }
            if (numQuads == 0)
            {	// Start und Ende im selben Quadranten
                // nur das Bogenstück selbst betrachten
                if (sweep > 0.0)
                {
                    if (clr.ArcHitTest(Center, Radius, StartQuadrant % 4, StartPoint, EndPoint)) return true;
                }
                else
                {
                    if (clr.ArcHitTest(Center, Radius, StartQuadrant % 4, EndPoint, StartPoint)) return true;
                }
            }
            else
            {
                // Start und Ende in verschiedenen Quadranten
                // zwei Abschnitte betrachten
                if (sweep > 0.0)
                {
                    GeoPoint2D p1;
                    switch (StartQuadrant) // Achse zur Rechten betrachten
                    {
                        default:
                        case 0:
                            p1 = new GeoPoint2D(Center.x, Center.y + Radius);
                            break;
                        case 1:
                            p1 = new GeoPoint2D(Center.x - Radius, Center.y);
                            break;
                        case 2:
                            p1 = new GeoPoint2D(Center.x, Center.y - Radius);
                            break;
                        case 3:
                            p1 = new GeoPoint2D(Center.x + Radius, Center.y);
                            break;
                    }
                    if (clr.ArcHitTest(Center, Radius, StartQuadrant % 4, StartPoint, p1)) return true;
                    switch (EndQuadrant) // Achse zur Rechten betrachten
                    {
                        default:
                        case 1:
                            p1 = new GeoPoint2D(Center.x, Center.y + Radius);
                            break;
                        case 2:
                            p1 = new GeoPoint2D(Center.x - Radius, Center.y);
                            break;
                        case 3:
                            p1 = new GeoPoint2D(Center.x, Center.y - Radius);
                            break;
                        case 0:
                            p1 = new GeoPoint2D(Center.x + Radius, Center.y);
                            break;
                    }
                    if (clr.ArcHitTest(Center, Radius, EndQuadrant % 4, p1, EndPoint)) return true;
                }
                else
                {
                    GeoPoint2D p1;
                    switch (StartQuadrant) // Achse zur Rechten betrachten
                    {
                        default:
                        case 1:
                            p1 = new GeoPoint2D(Center.x, Center.y + Radius);
                            break;
                        case 2:
                            p1 = new GeoPoint2D(Center.x - Radius, Center.y);
                            break;
                        case 3:
                            p1 = new GeoPoint2D(Center.x, Center.y - Radius);
                            break;
                        case 0:
                            p1 = new GeoPoint2D(Center.x + Radius, Center.y);
                            break;
                    }
                    if (clr.ArcHitTest(Center, Radius, StartQuadrant % 4, p1, StartPoint)) return true;
                    switch (EndQuadrant) // Achse zur Rechten betrachten
                    {
                        default:
                        case 0:
                            p1 = new GeoPoint2D(Center.x, Center.y + Radius);
                            break;
                        case 1:
                            p1 = new GeoPoint2D(Center.x - Radius, Center.y);
                            break;
                        case 2:
                            p1 = new GeoPoint2D(Center.x, Center.y - Radius);
                            break;
                        case 3:
                            p1 = new GeoPoint2D(Center.x + Radius, Center.y);
                            break;
                    }
                    if (clr.ArcHitTest(Center, Radius, EndQuadrant % 4, EndPoint, p1)) return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ICurve2D Clone()
        {
            Arc2D res = new Arc2D(Center, Radius, start, sweep);
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
            ICurve2D res;
            if (reverse) res = new Arc2D(Center, Radius, start + sweep, -sweep);
            else res = Clone();
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Copy (ICurve2D)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public override void Copy(ICurve2D toCopyFrom)
        {
            Arc2D c = toCopyFrom as Arc2D;
            start = c.start;
            sweep = c.sweep;
            base.Copy(toCopyFrom);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Reverse ()"/>
        /// </summary>
        public override void Reverse()
        {
            start = start + sweep;
            sweep = -sweep;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Trim (double, double)"/>
        /// </summary>
        /// <param name="startPos"></param>
        /// <param name="endPos"></param>
        /// <returns></returns>
        public override ICurve2D Trim(double startPos, double endPos)
        {
	        //If both parameters are 0.0d, this would produce an invalid arc
			if (startPos == 0.0d && endPos == 0.0d)
		        return null;

            Arc2D res = new Arc2D(Center, Radius, start, sweep);
            Angle st = start + startPos * sweep;
            Angle end = start + endPos * sweep;
            res.sweep = new SweepAngle(st, end, (sweep > 0.0) ^ (startPos > endPos));
            res.start = st;

            //Return null if the trimmed Arc2D would be invalid.
            if(Math.Abs(res.Sweep) < Precision.eps)
                return null;

            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MakeGeoObject (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override IGeoObject MakeGeoObject(Plane p)
        {
            Plane ploc = p;
            ploc.Location = p.ToGlobal(Center);
            Ellipse e = Ellipse.Construct();
            e.SetPlaneRadius(ploc, Radius, Radius);
            e.StartParameter = start;
            e.SweepParameter = sweep;
            return e;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Project (Plane, Plane)"/>
        /// </summary>
        /// <param name="fromPlane"></param>
        /// <param name="toPlane"></param>
        /// <returns></returns>
        public override ICurve2D Project(Plane fromPlane, Plane toPlane)
        {
            Plane ploc = fromPlane;
            ploc.Location = fromPlane.ToGlobal(Center);
            Ellipse e = Ellipse.Construct();
            e.SetPlaneRadius(ploc, Radius, Radius);
            e.StartParameter = start;
            e.SweepParameter = sweep;
            return e.GetProjectedCurve(toPlane);
        }
        public override GeoPoint2D StartPoint
        {
            get
            {
                return new GeoPoint2D(Center, Radius, start);
            }
            set
            {	// es geht darum den Startpunkt ein wenig zu verruckeln und den Endpunkt dort zu lassen wo er ist. 
                // Der jetzige Mittelpunkt wird auf die Mittelsenkrechte der Sehne gelotet
                // und gibt den neuen Mittelpunkt.
                // das kommt z.B. bei Border.GetParallel vor.
                if (Precision.IsEqual(StartPoint, value)) return; // macht sonst bei geschlossenem Bogen ein Problem
                GeoPoint2D ep = EndPoint;
                GeoPoint2D sp = value;
                Center = Geometry.DropPL(Center, new GeoPoint2D(sp, ep), ((GeoVector2D)(ep - sp)).ToLeft());
                Radius = Geometry.Dist(sp, Center);
                bool counterclock = sweep > 0.0;
                start = new Angle(sp, Center);
                sweep = new SweepAngle(sp - Center, ep - Center);
                // sweep ist zwischen -pi und +pi, also der Bogen ist maximal ein Halbkreis.
                // counterclock gibt aber die Richtung vor, deshalb ggf. umdrehen
                if (counterclock && sweep < 0.0) sweep += 2.0 * Math.PI;
                if (!counterclock && sweep > 0.0) sweep -= 2.0 * Math.PI;
            }
        }
        public override GeoPoint2D EndPoint
        {
            get
            {
                return new GeoPoint2D(Center, Radius, start + sweep);
            }
            set
            {	// siehe StartPoint
                if (Precision.IsEqual(EndPoint, value)) return; // macht sonst bei geschlossenem Bogen ein Problem
                GeoPoint2D ep = value;
                GeoPoint2D sp = StartPoint;
                if (ep == sp)
                {
                    if (Math.Abs(sweep) > Math.PI)
                    {
                        sweep = Math.Sign(sweep) * 2.0 * Math.PI;
                        return;
                    }
                    else
                    {
                        sweep = 0.0;
                    }
                }
                Center = Geometry.DropPL(Center, new GeoPoint2D(sp, ep), ((GeoVector2D)(ep - sp)).ToLeft());
                Radius = Geometry.Dist(sp, Center);
                bool counterclock = sweep > 0.0;
                start = new Angle(sp, Center);
                sweep = new SweepAngle(sp - Center, ep - Center);
                // sweep ist zwischen -pi und +pi, also der Bogen ist maximal ein Halbkreis.
                // counterclock gibt aber die Richtung vor, deshalb ggf. umdrehen
                if (counterclock && sweep < 0.0) sweep += 2.0 * Math.PI;
                if (!counterclock && sweep > 0.0) sweep -= 2.0 * Math.PI;
            }
        }
        public override GeoVector2D StartDirection
        {
            get
            {
                Angle a;
                if (sweep > 0.0) a = start + SweepAngle.ToLeft;
                else a = start + SweepAngle.ToRight;
                return a.Direction;
            }
        }
        public override GeoVector2D EndDirection
        {
            get
            {
                Angle a;
                if (sweep > 0.0) a = start + sweep + SweepAngle.ToLeft;
                else a = start + sweep + SweepAngle.ToRight;
                return a.Direction;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MinDistance (ICurve2D)"/>
        /// </summary>
        /// <param name="Other"></param>
        /// <returns></returns>
        public override double MinDistance(ICurve2D Other)
        {
            return base.MinDistance(Other); // dort wird gleichermaßen für Bogen und Kreis gerechnet
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
                path.AddArc((float)(Center.x - Radius), (float)(Center.y - Radius), (float)(2 * Radius),
                    (float)(2 * Radius), (float)(start.Degree), (float)(sweep.Degree));
            }
            else
            {
                path.AddArc((float)(Center.x - Radius), (float)(Center.y - Radius), (float)(2 * Radius),
                    (float)(2 * Radius), (float)((start + sweep).Degree), (float)(-sweep.Degree));
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtendedHitTest ()"/>
        /// </summary>
        /// <returns></returns>
        public override IQuadTreeInsertable GetExtendedHitTest()
        {
            return new Circle2D(Center, Radius);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.ReinterpretParameter (ref double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override bool ReinterpretParameter(ref double p)
        {
            if (p < 0.0 || p > 1.0)
            {	// suche eine Lösung auf der anderen Seite
                GeoPoint2D pos = PointAt(p);
                Angle a = new Angle(pos, Center);
                double a1 = (a.Radian - start.Radian) / sweep;
                double a2 = (a.Radian - start.Radian + 2.0 * Math.PI) / sweep;
                double a3 = (a.Radian - start.Radian - 2.0 * Math.PI) / sweep;
                if (p < 0.0)
                {
                    double res = a1;
                    if (a2 > 1.0 && (res < 1.0 || a2 < res)) res = a2;
                    if (a3 > 1.0 && (res < 1.0 || a3 < res)) res = a3;
                    if (res > 1.0)
                    {
                        p = res;
                        return true;
                    }
                }
                else // also p>1.0
                {
                    double res = a1;
                    if (a2 < 0.0 && (res > 0.0 || a2 > res)) res = a2;
                    if (a3 < 0.0 && (res > 0.0 || a3 > res)) res = a3;
                    if (res < 0.0)
                    {
                        p = res;
                        return true;
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Approximate (bool, double)"/>
        /// </summary>
        /// <param name="linesOnly"></param>
        /// <param name="maxError"></param>
        /// <returns></returns>
        public override ICurve2D Approximate(bool linesOnly, double maxError)
        {
            if (linesOnly)
            {
                return base.Approximate(true, maxError);
            }
            else
            {
                return Clone();
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetArea ()"/>
        /// </summary>
        /// <returns></returns>
        public override double GetArea()
        {   // es geht um die Fläche vom NUllpunkt aus gesehen
            GeoPoint2D startPoint = StartPoint;
            GeoPoint2D endPoint = EndPoint;
            double triangle = startPoint.x * endPoint.y - startPoint.y * endPoint.x;
            double seg = Radius * Radius * (sweep - Math.Sin(sweep)); // das gilt nur bis 180° oder?
            return (triangle + seg) / 2.0;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (ICurve2D)"/>
        /// </summary>
        /// <param name="IntersectWith"></param>
        /// <returns></returns>
        public override GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {
            GeoPoint2DWithParameter[] res = base.Intersect(IntersectWith);
            for (int i = 0; i < res.Length; ++i)
            {
                res[i].par1 = PositionOf(res[i].p);
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="StartPoint"></param>
        /// <param name="EndPoint"></param>
        /// <returns></returns>
        public override GeoPoint2DWithParameter[] Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            return Intersect(new Line2D(StartPoint, EndPoint));
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetModified (ModOp2D)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ICurve2D GetModified(ModOp2D m)
        {
            // es ist wichtig hier zu überschreiben, die Basisfunktionalität macht aus einem 360°
            // Kreisbogen einen Kreis und der Anfangspunkt geht verloren. Diese Information ist aber
            // für die Edges wichtig.
            if (m.IsIsogonal)
            {
                bool cc = sweep > 0.0;
                if (m.Determinant < 0.0)
                {
                    cc = !cc;
                }
                Arc2D res = new Arc2D(m * Center, m.Factor * Radius, m * StartPoint, m * EndPoint, cc);
                if (m.Determinant < 0.0)
                {
                    res.sweep = -sweep; // das schaltet die Fälle aus, wo Unsicherheit zwischen 0 und 360° besteht
                }
                else
                {
                    res.sweep = sweep;
                }
                return res;
            }
            else
            {
                //double sw = sweep;
                //if (m.Determinant < 0.0) sw = -sw;
                GeoVector2D majorAxis;
                GeoVector2D minorAxis;
                GeoPoint2D left, right, bottom, top;
                bool cc = sweep > 0.0;
                if (m.Determinant < 0.0)
                {
                    cc = !cc;
                }
                majorAxis = m * (Radius * GeoVector2D.XAxis);
                minorAxis = m * (Radius * GeoVector2D.YAxis);
                if (Math.Abs(majorAxis.Length - minorAxis.Length) < (majorAxis.Length + minorAxis.Length) * 1e-6)
                {
                    return new Arc2D(m * Center, Math.Abs(m.Factor * Radius), m * StartPoint, m * EndPoint, cc);
                }
                Geometry.PrincipalAxis(m * Center, m * (Radius * GeoVector2D.XAxis), m * (Radius * GeoVector2D.YAxis), out majorAxis, out minorAxis, out left, out right, out bottom, out top, false);
                double a1 = GeoVector2D.Area(m * (Radius * GeoVector2D.XAxis), m * (Radius * GeoVector2D.YAxis));
                double a2 = GeoVector2D.Area(majorAxis, minorAxis);
                // geändert wg. Fehler in IsIsogonal Fall, noch nicht getestet
                EllipseArc2D res = EllipseArc2D.Create(m * Center, majorAxis, minorAxis, m * StartPoint, m * EndPoint, cc);
                double rpos = res.PositionOf(m*this.PointAt(0.5));
                if (rpos < 0 || rpos>1) 
                {
                    res = EllipseArc2D.Create(m * Center, majorAxis, minorAxis, m * StartPoint, m * EndPoint, !cc);
                }
                return res;

                //if (m.Determinant < 0.0)
                //{
                //    return new EllipseArc2D(m * Center, m * (Radius * GeoVector2D.XAxis), m * (Radius * GeoVector2D.YAxis), start + sweep, sw, left, right, bottom, top);
                //}
                //else
                //{
                //    return new EllipseArc2D(m * Center, m * (Radius * GeoVector2D.XAxis), m * (Radius * GeoVector2D.YAxis), start, sw, left, right, bottom, top);
                //}
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetFused (ICurve2D, double)"/>
        /// </summary>
        /// <param name="toFuseWith"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override ICurve2D GetFused(ICurve2D toFuseWith, double precision)
        {
            if (toFuseWith is Circle2D && !(toFuseWith is Arc2D))
            {
                return toFuseWith.GetFused(this, precision);
            }
            if (toFuseWith is Arc2D)
            {
                Arc2D a2d = (toFuseWith as Arc2D);
                if ((Center | a2d.Center) + Math.Abs(Radius - a2d.Radius) < precision)
                {   // beides zusammen kleiner als precision, vielleich beim Bogen zu streng
                    Arc2D a1, a2;
                    if (this.Length > toFuseWith.Length)
                    {
                        a1 = this;
                        a2 = toFuseWith as Arc2D;
                    }
                    else
                    {
                        a1 = toFuseWith as Arc2D;
                        a2 = this;
                    }
                    if (a1.sweep * a2.sweep < 0.0)
                    {
                        a2 = (Arc2D)a2.CloneReverse(true); // war a2.Reverse();, man darf aber nicht eines der beteiligten Objekte ändern
                    }
                    // a1 ist länger als a2. Wenn es verschmelzen soll, dann muss der Start- oder der Endpunkt von a2
                    // innerhalb von a1 liegen

                    // Mittelpunkt und radius ist ja schon getestet
                    double pos1 = a1.PositionOf(a2.StartPoint, true); // vor dem Anfang oder auf dem Bogen
                    double pos2 = a1.PositionOf(a2.EndPoint, false); // nach dem Ende oder auf dem Bogen
                    // System.Diagnostics.Trace.WriteLine("pos1, pos2: " + pos1.ToString() + ", " + pos2.ToString());
                    double eps = precision / a1.Length;
                    if (Math.Abs(pos1 - 1.0) < eps && Math.Abs(pos2) < eps)
                    {   // geht von Ende bis Anfang: Sonderfall: ergibt Vollkreis
                        double full;
                        if (a1.sweep > 0.0) full = Math.PI * 2.0;
                        else full = -Math.PI * 2.0;
                        Arc2D res = new Arc2D(Center / a2d.Center, (Radius + a2d.Radius) / 2.0, a1.start, full);
                        return res;
                    }
                    else if (pos1 > -eps && pos1 < 1 + eps)
                    {   // also pos 2 geht evtl über diesen hinaus
                        GeoPoint2D sp = a1.StartPoint;
                        GeoPoint2D ep = a1.PointAt(Math.Max(pos2, 1.0));
                        Arc2D res = new Arc2D(Center / a2d.Center, (Radius + a2d.Radius) / 2.0, sp, ep, a1.sweep > 0.0);
                        return res;
                    }
                    else if (pos2 > -eps && pos2 < 1 + eps)
                    {
                        GeoPoint2D sp = a1.PointAt(Math.Min(0.0, pos1));
                        GeoPoint2D ep = a1.EndPoint;
                        Arc2D res = new Arc2D(Center / a2d.Center, (Radius + a2d.Radius) / 2.0, sp, ep, a1.sweep > 0.0);
                        return res;
                    }
                    else return null;
                }
            }
            return null;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.TangentPointsToAngle (GeoVector2D)"/>
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        public override double[] TangentPointsToAngle(GeoVector2D direction)
        {
            List<double> res = new List<double>();
            Angle a = direction.ToRight().Angle; // zwischen 0 und 2*pi
            if (start.Sweeps(SweepAngle, a))
            {
                res.Add(PositionOf(Center + direction.ToRight())); // radius ist egal für die richtige Lösung
            }
            a = direction.ToLeft().Angle; // zwischen 0 und 2*pi
            if (start.Sweeps(SweepAngle, a))
            {
                res.Add(PositionOf(Center + direction.ToLeft())); // radius ist egal für die richtige Lösung
            }
            return res.ToArray();
        }
        internal override void GetTriangulationPoints(out GeoPoint2D[] interpol, out double[] interparam)
        {
            int n = (int)Math.Floor(Math.Abs(sweep) / (Math.PI / 2.0)) + 1;
            if (n == 1) n = 2;
            interparam = new double[n];
            for (int i = 0; i < n; i++)
            {
                interparam[i] = i / (double)(n - 1);
            }
            interpol = new GeoPoint2D[n];
            for (int i = 0; i < interparam.Length; i++)
            {
                interpol[i] = PointAt(interparam[i]);
            }
        }

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Arc2D(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            start = (Angle)info.GetValue("Start", typeof(Angle));
            sweep = (SweepAngle)info.GetValue("Sweep", typeof(SweepAngle));
        }

        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Start", start, typeof(Angle));
            info.AddValue("Sweep", sweep, typeof(SweepAngle));
        }

        #endregion
#if DEBUG
        public IGeoObject asGeoObject
        {
            get
            {
                return MakeGeoObject(Plane.XYPlane);
            }
        }
#endif
    }
}
