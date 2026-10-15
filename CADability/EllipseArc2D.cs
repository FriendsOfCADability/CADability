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
                if ((sweepPar > 0.0) == (GeoVector2D.Orientation(majorAxis, minorAxis) > 0)) return p.IsLeftOf(endPoint, startPoint);
                else return p.IsLeftOf(startPoint, endPoint);
            }
        }
        /// <summary>
        /// Constructs an elliptical arc from its parametric data. The point at the parametric angle a is
        /// <c>center + cos(a)*majorAxis + sin(a)*minorAxis</c>; the arc starts at <paramref name="startPar"/> and sweeps by
        /// <paramref name="sweepPar"/>. The extreme points must match the ellipse (as computed by the principal axis transformation
        /// in <see cref="Geometry"/>); <see cref="Create"/> does all that and is the usual way to make an elliptical arc.
        /// </summary>
        /// <param name="center">center of the ellipse</param>
        /// <param name="majorAxis">major axis, from the center to the end of the axis</param>
        /// <param name="minorAxis">minor axis, from the center to the end of the axis</param>
        /// <param name="startPar">parametric angle of the start point, in radians</param>
        /// <param name="sweepPar">signed parametric sweep angle in radians, positive from the major axis towards the minor axis</param>
        /// <param name="left">leftmost point of the full ellipse</param>
        /// <param name="right">rightmost point of the full ellipse</param>
        /// <param name="bottom">lowest point of the full ellipse</param>
        /// <param name="top">highest point of the full ellipse</param>
        public EllipseArc2D(GeoPoint2D center, GeoVector2D majorAxis, GeoVector2D minorAxis, double startPar, double sweepPar, GeoPoint2D left, GeoPoint2D right, GeoPoint2D bottom, GeoPoint2D top)
            : base(center, majorAxis, minorAxis, sweepPar > 0.0, left, right, bottom, top)
        {
            this.startPar = startPar;
            this.sweepPar = sweepPar;
            startPoint = center + Math.Cos(startPar) * majorAxis + Math.Sin(startPar) * minorAxis;
            endPoint = center + Math.Cos(startPar + sweepPar) * majorAxis + Math.Sin(startPar + sweepPar) * minorAxis;
            RecalcQuadrant();
        }
        /// <summary>
        /// Creates the elliptical arc on the ellipse with the given center and axes from <paramref name="startPoint"/> to
        /// <paramref name="endPoint"/>. The axes need not be the principal axes nor perpendicular, they are converted first. The
        /// points are assumed to lie on the ellipse (only their parametric angles are used); coincident points give a full ellipse.
        /// </summary>
        /// <param name="center">center of the ellipse</param>
        /// <param name="majorAxis">first axis of the ellipse</param>
        /// <param name="minorAxis">second axis of the ellipse</param>
        /// <param name="startPoint">start point on the ellipse</param>
        /// <param name="endPoint">end point on the ellipse</param>
        /// <param name="counterClock">true for a counterclockwise arc, false for a clockwise arc</param>
        /// <returns>the elliptical arc</returns>
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
        /// <summary>
        /// The polar angle of the start point in radians, measured about the center from the major axis. Unlike the parametric
        /// start angle of the constructor this is the geometric angle of the ray from the center to the start point, as GDI+
        /// expects it for drawing an arc of an ellipse.
        /// </summary>
        public double axisStart
        {
            get
            {
                return start;
            }
        }
        /// <summary>
        /// The signed polar sweep angle from the start point to the end point in radians (see <see cref="axisStart"/>), positive
        /// counterclockwise; +2*pi or -2*pi for a full ellipse. Used for drawing with GDI+.
        /// </summary>
        public double axisSweep
        {
            get
            {
                return sweep;
            }
        }
        /// <summary>
        /// Makes the axes a right-handed system (the minor axis to the left of the major axis). If they are left-handed, the minor
        /// axis is flipped and the parametric start and sweep angles are adapted so that the arc itself, including its direction,
        /// stays the same. Needed before drawing with GDI+, which assumes right-handed axes.
        /// </summary>
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
        /// <inheritdoc/>
        public override IGeoObject MakeGeoObject(Plane p)
        {
            Ellipse e = Ellipse.Construct();
            e.SetEllipseCenterAxis(p.ToGlobal(Center), p.ToGlobal(MajorAxis), p.ToGlobal(MinorAxis));
            e.StartParameter = startPar;
            e.SweepParameter = sweepPar; // war vorher sweep, scheint mir aber falsch gewesen zu sein (Test: LeverArm.brep)
            return e;
        }
        /// <inheritdoc/>
        public override ICurve2D Project(Plane fromPlane, Plane toPlane)
        {
            Ellipse e = Ellipse.Construct();
            e.SetEllipseCenterAxis(fromPlane.ToGlobal(Center), fromPlane.ToGlobal(MajorAxis), fromPlane.ToGlobal(MinorAxis));
            e.StartParameter = startPar;
            e.SweepParameter = sweepPar;
            return e.GetProjectedCurve(toPlane);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override ICurve2D Clone()
        {
            EllipseArc2D res = new EllipseArc2D(Center, majorAxis, minorAxis, startPar, sweepPar, left, right, bottom, top);
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <inheritdoc/>
        public override ICurve2D CloneReverse(bool reverse)
        {
            ICurve2D res;
            if (reverse)
                res = new EllipseArc2D(Center, majorAxis, minorAxis, startPar + sweepPar, -sweepPar, left, right, bottom, top);
            else res = Clone();
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override ICurve2D Trim(double StartPos, double EndPos)
        {
            Angle st = startPar + StartPos * sweepPar;
            Angle en = startPar + EndPos * sweepPar;
            SweepAngle sw = new SweepAngle(st, en, sweepPar > 0.0);
            if (StartPos == 0.0 && EndPos == 1.0) sw = sweepPar;
            return new EllipseArc2D(center, majorAxis, minorAxis, st, sw, left, right, bottom, top);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override bool IsParameterOnCurve(double par)
        {	// d stimmt nicht besonders gut. Macht das Probleme?
            double d = Precision.epsa;
            return -d <= par && par <= 1.0 + d;
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override GeoPoint2D PointAt(double Position)
        {
            Angle a;
            a = startPar + Position * sweepPar;
            //double c = Math.Cos(a);
            //double s = Math.Sin(a);
            //return center + c * majorAxis + s * minorAxis;
            return fromUnitCircle * new GeoPoint2D(Math.Cos(a), Math.Sin(a));
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override double Length
        {
            get
            {
                // the exact elliptic integral: the polygon through points 5 degrees apart, which was used here,
                // is about 0.07% too short for an ellipse with axes 1:1.4 (a miter cut at 45 degrees)
                // fromUnitCircle maps the unit circle to perpendicular axes with the lengths of majorAxis and
                // minorAxis (or to the unit circle itself, when one of them is degenerate)
                return ArcLength.OfEllipse(majorAxis * majorAxis, minorAxis * minorAxis, 0.0, startPar, sweepPar);
            }
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        [Obsolete("Use the GraphicsPath extension AddCurve2D of CADability.Forms.GdiExtensions instead. GDI+ leaves the kernel in CADability 2.0, see issue #412.")]
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
        /// <inheritdoc/>
        public override IQuadTreeInsertable GetExtendedHitTest()
        {
            return new Ellipse2D(center, majorAxis, minorAxis);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {
            GeoPoint2DWithParameter[] res = base.Intersect(IntersectWith);
            for (int i = 0; i < res.Length; ++i)
            {
                res[i].par1 = PositionOf(res[i].p);
            }
            return res;
        }
        /// <inheritdoc/>
        public override GeoPoint2DWithParameter[] Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            // Schneller machen mit toUnitCircle
            return Intersect(new Line2D(StartPoint, EndPoint));
        }
        /// <inheritdoc/>
        public override ICurve2D GetModified(ModOp2D m)
        {
            // a degenerate ellipse has no fromUnitCircle (it is the identity)
            if (majorAxis.IsNullVector() || minorAxis.IsNullVector()) return EllipseArc2D.Create(m * center, m * majorAxis, m * minorAxis, m * StartPoint, m * EndPoint, counterClock);
            // the arc is the image of an arc of the unit circle under fromUnitCircle
            return Circle2D.ModifiedArc(m * fromUnitCircle, GeoPoint2D.Origin, 1.0, startPar, sweepPar, false);
        }
        /// <inheritdoc/>
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
                    // a full arc contains every arc of its ellipse, its start and end point say nothing about the other one
                    if (a1.IsClosed) return a1.Clone();
                    if (a1.sweepPar * a2.sweepPar < 0.0) a2 = (EllipseArc2D)a2.CloneReverse(true); // must not change one of the arcs to be fused
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
        /// <inheritdoc/>
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
        /// <summary>
        /// Returns the complementary arc on the same ellipse: it starts at the end point of this arc, runs in the same direction
        /// and ends at the start point of this arc, so that both arcs together form the full ellipse.
        /// </summary>
        /// <returns>the complementary arc</returns>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
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
