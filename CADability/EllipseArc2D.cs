using CADability.GeoObject;
using System;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability.Curve2D
{
    /// <summary>
    /// Describes an arc of an ellipse in 2D. Implements the ICurve2D interface
    /// </summary>
    [Serializable()]
    public class EllipseArc2D : Ellipse2D, ISerializable, IDeserializationCallback
    {
        // Hauptdaten:
        private Angle start; // der echte Startwinkel nicht der Parameter (in bezug auf die Achsen)
        private SweepAngle sweep; // zu start für GDI	
        private double startPar, sweepPar; // die daten für CONDOR
        // sekundäre Daten
        private GeoPoint2D startPoint;
        private GeoPoint2D endPoint;
        private int startQuad; // Quadrant des Startpunktes
        private int endQuad; // Quadrant des Startpunktes
        private bool crossingZero; // geht über die X-Achse

        private void RecalcQuadrant()
        {
            // Berechnung von start und sweep, wird benötigt für die GDI Darstellung
            Angle MajorAngle = majorAxis.Angle;
            double s = Math.Sin(MajorAngle);
            double c = Math.Cos(MajorAngle);
            ModOp2D toCenter = new ModOp2D(c, s, (-c * center.x - s * center.y), -s, c, (s * center.x - c * center.y));
            GeoVector2D startVector = toCenter * startPoint - GeoPoint2D.Origin;
            GeoVector2D endVector = toCenter * endPoint - GeoPoint2D.Origin;
            start = startVector.Angle;
            sweep = new SweepAngle(start, endVector.Angle, sweepPar > 0.0);
            if (sweep == 0.0)
            {
                if (sweepPar > 0.0) sweep = Math.PI * 2.0;
                if (sweepPar < 0.0) sweep = -Math.PI * 2.0;
            }

            // Berechnung der betroffenen Quadranten
            startQuad = endQuad = -1;
            if (startPoint.IsLeftOf(top, right)) startQuad = 0;
            else if (startPoint.IsLeftOf(left, top)) startQuad = 1;
            else if (startPoint.IsLeftOf(bottom, left)) startQuad = 2;
            else if (startPoint.IsLeftOf(right, bottom)) startQuad = 3;
            if (startQuad == -1)
            {	// der Startpunkt muss auf einer Ecke liegen
                double dmin, d;
                dmin = Geometry.Dist(startPoint, right); startQuad = 0;
                d = Geometry.Dist(startPoint, top);
                if (d < dmin) { startQuad = 1; dmin = d; }
                d = Geometry.Dist(startPoint, left);
                if (d < dmin) { startQuad = 2; dmin = d; }
                if (Geometry.Dist(startPoint, bottom) < dmin) { startQuad = 3; }
            }

            if (endPoint.IsLeftOf(top, right)) endQuad = 0;
            else if (endPoint.IsLeftOf(left, top)) endQuad = 1;
            else if (endPoint.IsLeftOf(bottom, left)) endQuad = 2;
            else if (endPoint.IsLeftOf(right, bottom)) endQuad = 3;
            if (endQuad == -1)
            {
                double dmin, d;
                dmin = Geometry.Dist(endPoint, right); endQuad = 0;
                d = Geometry.Dist(endPoint, top);
                if (d < dmin) { endQuad = 1; dmin = d; }
                d = Geometry.Dist(endPoint, left);
                if (d < dmin) { endQuad = 2; dmin = d; }
                if (Geometry.Dist(endPoint, bottom) < dmin) { endQuad = 3; }
            }
            if ((sweepPar > 0.0) == (GeoVector2D.Orientation(majorAxis, minorAxis) > 0))
            {
                //				crossingZero = right.IsLeftOf(endPoint,startPoint);
                //				if (Precision.IsEqual(right,endPoint) && endQuad==0) crossingZero = true;
                //				if (Precision.IsEqual(right,startPoint) && startQuad==3) crossingZero = true;
                crossingZero = (endQuad < startQuad);
                if (endQuad == startQuad) crossingZero = sweepPar > Math.PI;
            }
            else
            {
                //				crossingZero = right.IsLeftOf(startPoint,endPoint);
                crossingZero = (startQuad < endQuad);
                if (endQuad == startQuad) crossingZero = sweepPar < -Math.PI;
            }
        }
        /// <summary>
        /// Is the point p on the arc and not on the gap
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private bool IsPointOnArc(GeoPoint2D p)
        {	// we assume that the point is on the (full) ellipse and test here, whether it is on the actual arc
            if (Math.Abs(sweepPar - SweepAngle.Full) < 1e-10 || Math.Abs(sweepPar - SweepAngle.FullReverse) < 1e-10) return true;
            if (Math.Abs(sweepPar) < 0.1 || Math.Abs(sweepPar) > SweepAngle.Full - 0.1)
            {   // if start and endpoint are very close, then we cannot use "IsLeftOf", because it might be imprecise
                return IsParameterOnCurve(PositionOf(p));
            }
            else
            {
                if (sweepPar > 0.0) return p.IsLeftOf(endPoint, startPoint);
                else return p.IsLeftOf(startPoint, endPoint);
            }
        }
        public EllipseArc2D(GeoPoint2D center, GeoVector2D majorAxis, GeoVector2D minorAxis, double startPar, double sweepPar, GeoPoint2D left, GeoPoint2D right, GeoPoint2D bottom, GeoPoint2D top)
            : base(center, majorAxis, minorAxis, sweepPar > 0.0, left, right, bottom, top)
        {
            this.startPar = startPar;
            this.sweepPar = sweepPar;
            startPoint = center + Math.Cos(startPar) * majorAxis + Math.Sin(startPar) * minorAxis;
            endPoint = center + Math.Cos(startPar + sweepPar) * majorAxis + Math.Sin(startPar + sweepPar) * minorAxis;
            RecalcQuadrant();
        }
        public static EllipseArc2D Create(GeoPoint2D center, GeoVector2D majorAxis, GeoVector2D minorAxis, GeoPoint2D startPoint, GeoPoint2D endPoint, bool counterClock)
        {
            // bestimme die beiden Winkel
            GeoVector2D majAx;
            GeoVector2D minAx;
            GeoPoint2D left, right, bottom, top;
            Geometry.PrincipalAxis(center, majorAxis, minorAxis, out majAx, out minAx, out left, out right, out bottom, out top, false);
            ModOp2D toUnit = ModOp2D.Fit(new GeoPoint2D[] { center, center + majAx, center + minAx }, new GeoPoint2D[] { GeoPoint2D.Origin, GeoPoint2D.Origin + GeoVector2D.XAxis, GeoPoint2D.Origin + GeoVector2D.YAxis }, true);
            GeoPoint2D p = toUnit * startPoint;
            double sa = p.ToVector().Angle.Radian;
            p = toUnit * endPoint;
            double ea = p.ToVector().Angle.Radian;
            double sw;
            if (counterClock)
            {
                sw = ea - sa;
                if (sw <= 0.0) sw += Math.PI * 2.0; // geändert auf <= bzw. >=, da sonst aus Vollkreisen Leerkreise werden
            }
            else
            {
                sw = ea - sa;
                if (sw >= 0.0) sw -= Math.PI * 2.0;
            }
            return new EllipseArc2D(center, majAx, minAx, sa, sw, left, right, bottom, top);
        }
        public double axisStart
        {
            get
            {
                return start;
            }
        }
        public double axisSweep
        {
            get
            {
                return sweep;
            }
        }
        public void MakePositivOriented()
        {   // stellt sicher, dass die majorAxis und minorAxis ein rechtssystem bilden
            if (GeoVector2D.Orientation(majorAxis, minorAxis) < 0)
            {
                minorAxis = -minorAxis;
                startPar = 2.0 * Math.PI - startPar;
                sweepPar = -sweepPar;
                RecalcQuadrant();
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MakeGeoObject (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override IGeoObject MakeGeoObject(Plane p)
        {
            Ellipse e = Ellipse.Construct();
            e.SetEllipseCenterAxis(p.ToGlobal(Center), p.ToGlobal(MajorAxis), p.ToGlobal(MinorAxis));
            e.StartParameter = startPar;
            e.SweepParameter = sweepPar; // war vorher sweep, scheint mir aber falsch gewesen zu sein (Test: LeverArm.brep)
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
            Ellipse e = Ellipse.Construct();
            e.SetEllipseCenterAxis(fromPlane.ToGlobal(Center), fromPlane.ToGlobal(MajorAxis), fromPlane.ToGlobal(MinorAxis));
            e.StartParameter = startPar;
            e.SweepParameter = sweepPar;
            return e.GetProjectedCurve(toPlane);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.HitTest (ref BoundingRect, bool)"/>
        /// </summary>
        /// <param name="Rect"></param>
        /// <param name="IncludeControlPoints"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {
            Angle majorang = majorAxis.Angle;
            double radiusx = majorAxis.Length;
            double radiusy = minorAxis.Length;
            ClipRect clr = new ClipRect(Rect);
            if (clr.Contains(startPoint)) return true;
            if (clr.Contains(EndPoint)) return true;

            for (int q = 0; q < 4; ++q)
            {
                GeoPoint2D ps;
                GeoPoint2D pe;
                switch (q)
                {
                    default: // nur wg. Compiler
                    case 0:
                        ps = right;
                        pe = top;
                        break;
                    case 1:
                        ps = top;
                        pe = left;
                        break;
                    case 2:
                        ps = left;
                        pe = bottom;
                        break;
                    case 3:
                        ps = bottom;
                        pe = right;
                        break;
                }
                if (Sweep > 0.0)
                {
                    if (crossingZero)
                    {
                        if (q < startQuad && q > endQuad) continue;
                    }
                    else
                    {
                        if (q < startQuad || q > endQuad) continue;
                    }
                    if (crossingZero && startQuad == q && endQuad == q)
                    {	// zwei Tests nötig
                        GeoPoint2D p = endPoint;
                        if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, q, ps, p)) return true;
                        p = startPoint;
                        if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, q, p, pe)) return true;
                    }
                    else
                    {
                        if (startQuad == q) ps = startPoint;
                        if (endQuad == q) pe = endPoint;
                        if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, q, ps, pe)) return true;
                    }
                }
                else
                {
                    if (crossingZero)
                    {
                        if (q < endQuad && q > startQuad) continue;
                    }
                    else
                    {
                        if (q < endQuad || q > startQuad) continue;
                    }
                    if (crossingZero && startQuad == q && endQuad == q)
                    {	// zwei Tests nötig
                        GeoPoint2D p = startPoint;
                        if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, q, ps, p)) return true;
                        p = endPoint;
                        if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, q, p, pe)) return true;
                    }
                    else
                    {
                        if (startQuad == q) pe = startPoint;
                        if (endQuad == q) ps = endPoint;
                        if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, q, ps, pe)) return true;
                    }
                }
            }

            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtent ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingRect GetExtent()
        {
            double exc = Math.Min(majrad, minrad) / Math.Max(majrad, minrad); // die sind doch immer positiv, oder?
            if (exc < 1e-4)
            {
                return this.Approximate(true, Precision.eps).GetExtent();
            }
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            res.MinMax(startPoint);
            res.MinMax(endPoint);
            if (IsPointOnArc(left)) res.MinMax(left);
            if (IsPointOnArc(right)) res.MinMax(right);
            if (IsPointOnArc(bottom)) res.MinMax(bottom);
            if (IsPointOnArc(top)) res.MinMax(top);
            return res;
        }
        public override double Sweep
        {
            get
            {   // das ist ganzschön blöd: es kommt im Prinzip auf sweep und auf die Orintierung der Achsen an
                // am 2.2.17 so implementiert:
                if (Math.Abs(sweepPar) < (Math.PI - 0.1)) return new SweepAngle(StartDirection, EndDirection);
                else if (Math.Abs(sweepPar) < (2.0 * Math.PI - 0.2))
                {
                    GeoVector2D md = MiddleDirection;
                    return new SweepAngle(StartDirection, md) + new SweepAngle(md, EndDirection);
                }
                else
                {
                    if (Math.Sign(GeoVector2D.Orientation(majorAxis, minorAxis)) * Math.Sign(sweepPar) > 0) return SweepAngle.Full;
                    else return SweepAngle.FullReverse;
                }
                // vorher war es so und falsch
                // SweepAngle res = new SweepAngle(StartDirection, EndDirection);
                // if ((sweep < 0.0 && res > 0.0) || (sweep > 0.0 && res < 0.0)) res = -res; // 2.2.17 entfernt: 
            }
        }
        internal void MakeFullEllipse()
        {
            if (sweepPar < 0) sweepPar = -Math.PI * 2.0;
            else sweepPar = Math.PI * 2.0;
            endPoint = startPoint;
            RecalcQuadrant();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ICurve2D Clone()
        {
            EllipseArc2D res = new EllipseArc2D(Center, majorAxis, minorAxis, startPar, sweepPar, left, right, bottom, top);
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
            if (reverse)
                res = new EllipseArc2D(Center, majorAxis, minorAxis, startPar + sweepPar, -sweepPar, left, right, bottom, top);
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
            EllipseArc2D c = toCopyFrom as EllipseArc2D;
            start = c.start;
            sweep = c.sweep;
            startPar = c.startPar;
            sweepPar = c.sweepPar;
            startPoint = c.startPoint;
            endPoint = c.endPoint;
            startQuad = c.startQuad;
            endQuad = c.endQuad;
            crossingZero = c.crossingZero;
            base.Copy(toCopyFrom);
        }

        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Trim (double, double)"/>
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="EndPos"></param>
        /// <returns></returns>
        public override ICurve2D Trim(double StartPos, double EndPos)
        {
            Angle st = startPar + StartPos * sweepPar;
            Angle en = startPar + EndPos * sweepPar;
            SweepAngle sw = new SweepAngle(st, en, sweepPar > 0.0);
            if (StartPos == 0.0 && EndPos == 1.0) sw = sweepPar;
            return new EllipseArc2D(center, majorAxis, minorAxis, st, sw, left, right, bottom, top);
        }
        public override GeoPoint2D StartPoint
        {
            get
            {
                double c = Math.Cos(startPar);
                double s = Math.Sin(startPar);
                return center + c * majorAxis + s * minorAxis;
            }
            set
            {
                // nur für Minimale Angleichungen beim Border
                if (!Precision.IsEqual(StartPoint, value))
                {
                    // das ist eigentlich die allgemeine Lösung: eine ModOp2D, die den Endpunkt festhält und den 
                    // Startpunkt durch eine Drehung, Skalierung und Verschiebung (aber nicht Verzerrung)
                    // verändert
                    try
                    {
                        ModOp2D mod = ModOp2D.Fit(new GeoPoint2D[] { StartPoint, EndPoint }, new GeoPoint2D[] { value, EndPoint }, true);
                        base.center = mod * base.center;
                        base.majorAxis = mod * base.majorAxis;
                        base.minorAxis = mod * base.minorAxis;
                        base.Recalc();
                    }
                    catch (ModOpException)
                    {
                        EllipseArc2D ea = Create(center, majorAxis, minorAxis, value, endPoint, counterClock);
                        this.Copy(ea);
                    }
                }
            }
        }
        public override GeoPoint2D EndPoint
        {
            get
            {
                double c = Math.Cos(startPar + sweepPar);
                double s = Math.Sin(startPar + sweepPar);
                return center + c * majorAxis + s * minorAxis;
            }
            set
            {
                if (!Precision.IsEqual(EndPoint, value))
                {
                    try
                    {
                        ModOp2D mod = ModOp2D.Fit(new GeoPoint2D[] { StartPoint, EndPoint }, new GeoPoint2D[] { StartPoint, value }, true);
                        base.center = mod * base.center;
                        base.majorAxis = mod * base.majorAxis;
                        base.minorAxis = mod * base.minorAxis;
                        base.Recalc();
                    }
                    catch (ModOpException)
                    {
                        EllipseArc2D ea = Create(center, majorAxis, minorAxis, startPoint, value, counterClock);
                        this.Copy(ea);
                    }
                }
            }
        }
        public override GeoVector2D StartDirection
        {
            get
            {
                GeoVector2D res;
                if (sweepPar > 0)
                {
                    res = fromUnitCircle * new GeoVector2D(-Math.Sin(startPar), Math.Cos(startPar));
                }
                else
                {
                    res = fromUnitCircle * new GeoVector2D(Math.Sin(startPar), -Math.Cos(startPar));
                }
                res.Norm();
                return res;
            }
        }
        public override GeoVector2D EndDirection
        {
            get
            {
                double a = startPar + sweepPar;
                GeoVector2D res;
                if (sweepPar > 0)
                {
                    res = fromUnitCircle * new GeoVector2D(-Math.Sin(a), Math.Cos(a));
                }
                else
                {
                    res = fromUnitCircle * new GeoVector2D(Math.Sin(a), -Math.Cos(a));
                }
                res.Norm();
                return res;
            }
        }
        public override bool IsClosed
        {
            get
            {
                return sweepPar >= Math.PI * 2.0 || sweepPar <= -Math.PI * 2.0;
            }
        }
        internal void Close()
        {   // maybe this is wrong, see MakeFullEllipse
            if (sweep > 0) sweep = Math.PI * 2.0;
            else sweep = -Math.PI * 2.0;
            RecalcQuadrant();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Reverse ()"/>
        /// </summary>
        public override void Reverse()
        {
            base.Reverse();
            startPar = startPar + sweepPar;
            sweepPar = -sweepPar;
            if (startPar < 0.0) startPar += Math.PI * 2.0;
            if (startPar > Math.PI * 2.0) startPar -= Math.PI * 2.0;
            GeoPoint2D tmp = startPoint;
            startPoint = endPoint;
            endPoint = tmp;
            RecalcQuadrant();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.IsParameterOnCurve (double)"/>
        /// </summary>
        /// <param name="par"></param>
        /// <returns></returns>
        public override bool IsParameterOnCurve(double par)
        {	// d stimmt nicht besonders gut. Macht das Probleme?
            double d = Precision.epsa;
            return -d <= par && par <= 1.0 + d;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PositionOf (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double PositionOf(GeoPoint2D p)
        {
            GeoPoint2D p0 = toUnitCircle * p;
            double a = Math.Atan2(p0.y, p0.x);
            double a1 = (a - startPar) / sweepPar;
            double a2 = (a - startPar + 2.0 * Math.PI) / sweepPar;
            double a3 = (a - startPar - 2.0 * Math.PI) / sweepPar;
            double a4 = (a - startPar + 4.0 * Math.PI) / sweepPar;
            double a5 = (a - startPar - 4.0 * Math.PI) / sweepPar;
            // welcher der 3 Werte ist näher an 0.5 ?
            double ax = a1;
            if (Math.Abs(a2 - 0.5) < Math.Abs(ax - 0.5)) ax = a2;
            if (Math.Abs(a3 - 0.5) < Math.Abs(ax - 0.5)) ax = a3;
            if (Math.Abs(a4 - 0.5) < Math.Abs(ax - 0.5)) ax = a4;
            if (Math.Abs(a5 - 0.5) < Math.Abs(ax - 0.5)) ax = a5;
            return ax;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoPoint2D PointAt(double Position)
        {
            Angle a;
            a = startPar + Position * sweepPar;
            //double c = Math.Cos(a);
            //double s = Math.Sin(a);
            //return center + c * majorAxis + s * minorAxis;
            return fromUnitCircle * new GeoPoint2D(Math.Cos(a), Math.Sin(a));
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoVector2D DirectionAt(double Position)
        {
            Angle a;
            a = startPar + Position * sweepPar;
            GeoVector2D res;
            if (sweepPar > 0)
            {
                res = fromUnitCircle * new GeoVector2D(-Math.Sin(a), Math.Cos(a));
            }
            else
            {
                res = fromUnitCircle * new GeoVector2D(Math.Sin(a), -Math.Cos(a));
            }
            // res.Norm(); sollte nicht genormt werden wg. Newton
            return res;
        }
        public override double Length
        {
            get
            {
                // siehe auch http://en.wikipedia.org/wiki/Ellipse,  A good approximation is Ramanujan's:
                if (Math.Abs(sweepPar) < 1e-2) return Geometry.Dist(StartPoint, EndPoint);
                // diese Länge ist ziemlich genau, meist so 5 Stellen identisch mit der von OCas berechneten
                ICurve2D approx = this.Approximate(false, -Math.Abs(sweepPar) / Math.PI * 36); // in ca. 5° Schritte
                return approx.Length;
                // wir dürfen nicht ocas verwenden (wg. BackgroungThread)
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MinDistance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double MinDistance(GeoPoint2D p)
        {
            double res = Math.Min(p | StartPoint, p | EndPoint);
            GeoPoint2D cpf = ClosestPerpendicularFoot(p);
            double par = PositionOf(cpf);
            if (IsParameterOnCurve(par))
            {
                double d = cpf | p;
                if (d < res) res = d;
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.AddToGraphicsPath (System.Drawing.Drawing2D.GraphicsPath, bool)"/>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="forward"></param>
        public override void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {	// es wird ein Spline erzeugt in 10° Schritten
            // hier sollte man überlegen, ob man nicht mit einer Kreisbogenannäherung
            // arbeiten sollte

            int n = (int)Math.Round(Math.Abs(sweepPar) / Math.PI * 18);
            if (n < 2) n = 2;
            PointF[] pnts = new PointF[n + 1];
            double step = 1.0 / n;
            for (int i = 0; i < n; ++i)
            {
                GeoPoint2D p = this.PointAt(i * step);
                pnts[i] = new PointF((float)p.x, (float)p.y);
            }
            pnts[n] = new PointF((float)endPoint.x, (float)endPoint.y);
            if (!forward) Array.Reverse(pnts);
            path.AddCurve(pnts);
            // path.AddLines(pnts); // DEBUG!!!
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtendedHitTest ()"/>
        /// </summary>
        /// <returns></returns>
        public override IQuadTreeInsertable GetExtendedHitTest()
        {
            return new Ellipse2D(center, majorAxis, minorAxis);
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
                GeoPoint2D p0 = toUnitCircle * pos;
                double a = Math.Atan2(p0.y, p0.x);
                double a1 = (a - startPar) / sweepPar;
                double a2 = (a - startPar + 2.0 * Math.PI) / sweepPar;
                double a3 = (a - startPar - 2.0 * Math.PI) / sweepPar;
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
            // Schneller machen mit toUnitCircle
            return Intersect(new Line2D(StartPoint, EndPoint));
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetModified (ModOp2D)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ICurve2D GetModified(ModOp2D m)
        {
#if DEBUG
            ////DebuggerContainer dc = new DebuggerContainer();
            ////GeoPoint[] tst = new GeoPoint[10];
            ////for (int i = 0; i < 10; i++)
            ////{
            ////    tst[i] = new GeoPoint(PointAt(i / 9.0));
            ////}
            ////Polyline pl = Polyline.Construct();
            ////try
            ////{
            ////    pl.SetPoints(tst, false);
            ////    dc.Add(pl, 0);
            ////}
            ////catch { }
            ////EllipseArc2D dbg = EllipseArc2D.Create(m * center, m * majorAxis, m * minorAxis, m * StartPoint, m * EndPoint, counterClock);
            ////for (int i = 0; i < 10; i++)
            ////{
            ////    tst[i] = new GeoPoint(dbg.PointAt(i / 9.0));
            ////}
            ////pl = Polyline.Construct();
            ////try
            ////{
            ////    pl.SetPoints(tst, false);
            ////    dc.Add(pl, 1);
            ////}
            ////catch { }
#endif
            EllipseArc2D res = EllipseArc2D.Create(m * center, m * majorAxis, m * minorAxis, m * StartPoint, m * EndPoint, counterClock);
            if (Math.Abs(res.sweepPar) < 1e-6 && Math.Abs(this.sweepPar) > Math.PI * 2.0 - 1e-6) res.sweepPar = this.sweepPar;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetFused (ICurve2D, double)"/>
        /// </summary>
        /// <param name="toFuseWith"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override ICurve2D GetFused(ICurve2D toFuseWith, double precision)
        {
            if (toFuseWith is Ellipse2D && !(toFuseWith is EllipseArc2D))
            {
                return toFuseWith.GetFused(this, precision);
            }
            if (toFuseWith is EllipseArc2D)
            {
                EllipseArc2D a2d = (toFuseWith as EllipseArc2D);
                // hier wird vorausgesetzt, dass majrad>minrad immer gilt. Stimmt das?
                if ((Center | a2d.Center) + Math.Abs(majrad - a2d.majrad) + Math.Abs(minrad - a2d.minrad) < precision)
                {   // alle zusammen kleiner als precision, vielleich beim Bogen zu streng
                    // weitere Bedingung: Achsen müssen übereinstimmen.
                    // dazu müsste man bei vertauschten Richtungen noch mehr Grips reinstecken...

                    EllipseArc2D a1, a2;
                    if (Math.Abs(this.sweepPar) > Math.Abs(a2d.sweepPar))
                    {
                        a1 = this;
                        a2 = a2d;
                    }
                    else
                    {
                        a1 = a2d;
                        a2 = this;
                    }
                    if (a1.sweepPar * a2.sweepPar < 0.0) a2.Reverse();
                    // a1 ist länger als a2. Wenn es verschmelzen soll, dann muss der Start- oder der Endpunkt von a2
                    // innerhalb von a1 liegen

                    // Mittelpunkt und radius ist ja schon getestet
                    double pos1 = a1.PositionOf(a2.StartPoint); // vor dem Anfang oder auf dem Bogen
                    double pos2 = a1.PositionOf(a2.EndPoint); // nach dem Ende oder auf dem Bogen
                    // System.Diagnostics.Trace.WriteLine("pos1, pos2: " + pos1.ToString() + ", " + pos2.ToString());
                    bool pos1ok = a1.IsParameterOnCurve(pos1);
                    bool pos2ok = a1.IsParameterOnCurve(pos2);
                    if (pos1ok && pos2ok)
                    {   // a1 contains both parameters, so it contains the whole arc
                        // but if pos1 == 1 and pos2 == 0 then it is the complimentary arc
                        if (pos1<pos2) return a1.Clone();
                    }
                    if (pos1ok)
                    {
                        return Create(a1.center, a1.majorAxis, a1.minorAxis, a1.startPoint, a2.endPoint, a1.sweepPar > 0.0);
                    }
                    else if (pos2ok)
                    {
                        return Create(a1.center, a1.majorAxis, a1.minorAxis, a2.startPoint, a1.endPoint, a1.sweepPar > 0.0);
                    }
                }
            }
            return null;
        }
        private double areaHelper(double a, double b, double phi)
        {
            return phi - Math.Atan2((b - a) * Math.Sin(2 * phi), (a + b) + (b - a) * Math.Cos(2 * phi));
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetArea ()"/>
        /// </summary>
        /// <returns></returns>
        public override double GetArea()
        {   // Area from origin, source: https://www.geometrictools.com/Documentation/AreaIntersectingEllipses.pdf
            double a = majorAxis.Length, b = minorAxis.Length;
            
            //Unreachable Code
            /*
            if (false) // nonsense, works the same for ellipses >180°, was: (sweepPar > Math.PI)
            {
                // make two sections, because the formula works only for angles less than 180°
                GeoPoint2D startPoint = StartPoint;
                GeoPoint2D endPoint = EndPoint;
                GeoPoint2D middlePoint = PointAt(0.5);
                double triangle = startPoint.x * middlePoint.y - startPoint.y * middlePoint.x + middlePoint.x * endPoint.y - middlePoint.y * endPoint.x;
                double phi1 = (startPoint - center).Angle - majorAxis.Angle;
                double phi2 = (middlePoint - center).Angle - majorAxis.Angle;
                double phi3 = (endPoint - center).Angle - majorAxis.Angle;
                if (sweep > 0)
                {
                    if (phi2 < phi1) phi2 += Math.PI * 2;
                    if (phi3 < phi2) phi3 += Math.PI * 2;
                }
                else
                {
                    if (phi2 > phi1) phi2 -= Math.PI * 2;
                    if (phi3 > phi2) phi3 -= Math.PI * 2;
                }
                double segment = a * b * ((areaHelper(a, b, phi2) - areaHelper(a, b, phi1)) + (areaHelper(a, b, phi3) - areaHelper(a, b, phi2)));
                double segtriangle = GeoVector2D.Area(startPoint - center, middlePoint - center) + GeoVector2D.Area(middlePoint - center, endPoint - center);
                return (triangle + segment - segtriangle) / 2.0;
            }
            else
            {
            */
            GeoPoint2D startPoint = StartPoint;
            GeoPoint2D endPoint = EndPoint;
            double triangle = startPoint.x * endPoint.y - startPoint.y * endPoint.x;
            double phi1 = (startPoint - center).Angle - majorAxis.Angle;
            double phi2 = (endPoint - center).Angle - majorAxis.Angle;
            if (Sweep > 0) // chenged to "Sweep" because "sweep" was positiv when it should have been negative
            {
                if (phi2 <= phi1) phi2 += Math.PI * 2;
            }
            else
            {
                if (phi2 >= phi1) phi2 -= Math.PI * 2;
            }
            double segment = a * b * (areaHelper(a, b, phi2) - areaHelper(a, b, phi1)); // this is the double value
            double segtriangle = GeoVector2D.Area(startPoint - center, endPoint - center); // area of the parallelogram
            return (triangle + segment - segtriangle) / 2.0; // all values are double size, hence /2.0
            //}
        }
        public EllipseArc2D GetComplement()
        {
            double sp;
            if (sweepPar > 0) sp = Math.PI * 2.0 - sweepPar;
            else sp = -(Math.PI * 2.0 + sweepPar);
            double st = startPar + sweepPar;
            if (st > Math.PI * 2.0) st -= Math.PI * 2.0;
            if (st < 0.0) st += Math.PI * 2.0;
            return new EllipseArc2D(center, majorAxis, minorAxis, st, sp, left, right, bottom, top);
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected EllipseArc2D(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            try
            {
                startPar = (double)info.GetValue("StartPar", typeof(double));
                sweepPar = (double)info.GetValue("SweepPar", typeof(double));
            }
            catch (SerializationException)
            {	// die alte Art, da wirds halt falsch (nur ungefähr)(geändert am 14.12.05)
                startPar = (double)(Angle)info.GetValue("Start", typeof(Angle));
                sweepPar = (double)(SweepAngle)info.GetValue("Sweep", typeof(SweepAngle));
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("StartPar", startPar, typeof(double));
            info.AddValue("SweepPar", sweepPar, typeof(double));
        }
        #endregion
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            startPoint = center + Math.Cos(startPar) * majorAxis + Math.Sin(startPar) * minorAxis;
            endPoint = center + Math.Cos(startPar + sweepPar) * majorAxis + Math.Sin(startPar + sweepPar) * minorAxis;
            this.RecalcQuadrant();
        }
        //private void MakeRightHanded()
        //{
        //    if (GeoVector2D.Orientation(majorAxis, minorAxis) < 0)
        //    {
        //        Copy(Create(center, majorAxis, -minorAxis, StartPoint, EndPoint, counterClock));
        //    }
        //}
    }
}
