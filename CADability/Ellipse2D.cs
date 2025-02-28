using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Optimization;
using MathNet.Numerics.Optimization.TrustRegion;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability.Curve2D
{
    /// <summary>
    /// Describes an ellipse in 2D. Implements the ICurve2D interface.
    /// </summary>
    [Serializable()]
    public class Ellipse2D : GeneralCurve2D, ISerializable
    {
        // Hauptdaten
        internal GeoPoint2D center;
        internal GeoVector2D majorAxis;
        internal GeoVector2D minorAxis;
        internal bool counterClock; // nur bei Vollellipse die Richtung
        // interne sekundäre Daten:
        internal ModOp2D toUnitCircle; // diese Abbildung bildet die Ellipse auf den Einheitskreis ab
        internal ModOp2D fromUnitCircle; // diese Abbildung bildet den Einheitskreis auf die Ellipse ab
        internal GeoPoint2D left, right, bottom, top; // die Extrempunkte
        internal double majrad, minrad, majaxsin, majaxcos;
        
        internal void RecalcUnitCircle()
        {
            // berechnen der beiden ModOps fromUnitCircle und toUnitCircle 
            // Achtung: das berücksichtigt nicht ein Linkssystem. Das ist somit eigentlich falsch
            // im Linkssystem müsste das Vorzeichen von MinorRadius negativ sein, damit die Matrix stimmt!
            double MajorRadius = majorAxis.Length;
            double MinorRadius = minorAxis.Length;
            if (GeoVector2D.Orientation(majorAxis, minorAxis) < 0)
            {   // eingeführt wg. dem Umdrehen eines Faces mit elliptischen Begrenzungen (11.3.15), Datei 97871_086103_032.cdb, nach Shell.AssertOutwardOrientation stimmt das Face Nr. 8 nicht mehr
                MinorRadius = -MinorRadius;
            }
            if (MajorRadius == 0.0 || MinorRadius == 0.0)
            {
                fromUnitCircle = toUnitCircle = ModOp2D.Identity;
            }
            else
            {
                Angle MajorAngle = majorAxis.Angle;
                double s = Math.Sin(MajorAngle);
                double c = Math.Cos(MajorAngle);
                toUnitCircle = new ModOp2D(c / MajorRadius, s / MajorRadius, (-c * center.x - s * center.y) / MajorRadius,
                    -s / MinorRadius, c / MinorRadius, (s * center.x - c * center.y) / MinorRadius);
                fromUnitCircle = new ModOp2D(MajorRadius * c, -MinorRadius * s, center.x,
                    MajorRadius * s, MinorRadius * c, center.y);
            }
        }
        internal void GetAxisAlignedEllipse(out double MajorRadius, out double MinorRadius, out ModOp2D toAxis, out ModOp2D fromAxis)
        {
            MajorRadius = majorAxis.Length;
            MinorRadius = minorAxis.Length;
            if (MajorRadius == 0.0 || MinorRadius == 0.0)
            {
                fromAxis = toAxis = ModOp2D.Identity;
            }
            else
            {
                // noch überprüfen dass MajorAxis>MinorAxis ist
                Angle MajorAngle = majorAxis.Angle;
                double s = Math.Sin(MajorAngle);
                double c = Math.Cos(MajorAngle);
                toAxis = new ModOp2D(c, s, (-c * center.x - s * center.y), -s, c, (s * center.x - c * center.y));
                fromAxis = new ModOp2D(c, -s, center.x, s, c, center.y);
            }
        }
        internal void RecalcAxis()
        {
            Geometry.PrincipalAxis(center, majorAxis, minorAxis, out majorAxis, out minorAxis, out left, out right, out bottom, out top, false);
        }
        internal void Recalc()
        {
            RecalcAxis();
            RecalcUnitCircle();
        }
        public Ellipse2D()
        {
        }
        /// <summary>
        /// Constructs an ellipse in 2D. The two axis may not be orthogonal but must not be colinear.
        /// </summary>
        /// <param name="center">Center of the ellipse</param>
        /// <param name="axis1">First axis of the ellipse</param>
        /// <param name="axis2">Second axis of the ellipse</param>
        public Ellipse2D(GeoPoint2D center, GeoVector2D axis1, GeoVector2D axis2)
        {
            this.center = center;
            this.majorAxis = axis1;
            this.minorAxis = axis2;
            RecalcAxis(); // left u.s.w.
            RecalcUnitCircle(); // Reihenfoöge geändert, erst axis, dann unitCircle scheint mir logischer

            // folgende Daten werden nur zur Darstellung gebraucht
            majrad = majorAxis.Length;
            minrad = minorAxis.Length;
            majaxsin = Math.Sin(majorAxis.Angle);
            majaxcos = Math.Cos(majorAxis.Angle);
            counterClock = true;
        }
        /// <summary>
        /// Constructs an ellipse in 2D. The major and minor axis must be orthogonal.
        /// </summary>
        /// <param name="center">Center of the ellipse</param>
        /// <param name="majorAxis">Major axis of the ellipse</param>
        /// <param name="minorAxis">Minor axis of the ellipse</param>
        /// <param name="left">left extremum of the ellipse</param>
        /// <param name="right">right extremum of the ellipse</param>
        /// <param name="bottom">bottom extremum of the ellipse</param>
        /// <param name="top">top extremum of the ellipse</param>
        public Ellipse2D(GeoPoint2D center, GeoVector2D majorAxis, GeoVector2D minorAxis, bool counterClock, GeoPoint2D left, GeoPoint2D right, GeoPoint2D bottom, GeoPoint2D top)
        {
            this.center = center;
            this.majorAxis = majorAxis;
            this.minorAxis = minorAxis;
            RecalcUnitCircle();
            this.counterClock = counterClock;

            this.left = left;
            this.right = right;
            this.bottom = bottom;
            this.top = top;

            // folgende Daten werden nur zur Darstellung gebraucht
            majrad = majorAxis.Length;
            minrad = minorAxis.Length;
            majaxsin = Math.Sin(majorAxis.Angle);
            majaxcos = Math.Cos(majorAxis.Angle);
        }
        public static Ellipse2D FromFivePoints(GeoPoint2D[] p, bool isFull)
        {
            Ellipse2D res = FromFivePoints(p);
            if (res != null)
            {
                double mindist = double.MaxValue;
                double p0 = res.PositionOf(p[0]);
                for (int i = 1; i < 5; i++)
                {
                    double p1 = res.PositionOf(p[i]);
                    if (Math.Abs(p1 - p0) < Math.Abs(mindist))
                    {
                        mindist = p1 - p0;
                    }
                    p0 = p1;
                }
                if (mindist < 0) res.Reverse();
                if (!isFull)
                {
                    double sp = res.PositionOf(p[0]);
                    double ep = res.PositionOf(p[4]);
                    double mp = res.PositionOf(p[2]);
                    if (sp < ep)
                    {
                        if (sp < mp && mp < ep) res = res.Trim(sp, ep) as Ellipse2D;
                        else res = res.Trim(ep, sp) as Ellipse2D;
                    }
                    else
                    {   // to be tested!
                        if (sp < mp && mp < ep) res = res.Trim(ep, sp) as Ellipse2D;
                        else res = res.Trim(sp, ep) as Ellipse2D;
                    }
                }
            }
            return res;
        }
        public static Ellipse2D FromFivePoints(GeoPoint2D[] p)
        {
            Matrix m = new DenseMatrix(5, 5);
            Vector b = new DenseVector(5);
            for (int i = 0; i < 5; ++i)
            {
                m[i, 0] = 1;
                m[i, 1] = 2 * p[i].x;
                m[i, 2] = 2 * p[i].y;
                m[i, 3] = 2 * p[i].x * p[i].y;
                m[i, 4] = p[i].y * p[i].y;
                b[i] = -p[i].x * p[i].x;
            }
            Vector x = (Vector)m.Solve(b);
            if (!x.IsValid()) return null;
            double l1, l2;
            if (Geometry.quadgl(1, -(1 + x[4]), x[4] - x[3] * x[3], out l1, out l2) == 0)
            {
                l1 = l2 = (1 + x[4]) / 2.0;
            }
            if (l1 == 0.0 || l2 == 0.0) return null;

            Angle MajorAngle;
            if (Math.Abs(l1 - 1) > Math.Abs(l2 - 1)) MajorAngle = Math.Atan2(l1 - 1, x[3]);
            else MajorAngle = Math.Atan2(l2 - 1, x[3]) + Math.PI / 2.0;
            double b1 = x[1] * Math.Cos(MajorAngle) + x[2] * Math.Sin(MajorAngle);
            double b2 = -x[1] * Math.Sin(MajorAngle) + x[2] * Math.Cos(MajorAngle);
            double MajorRadius = Math.Sqrt(Math.Abs((b1 * b1 / l1 + b2 * b2 / l2 - x[0]) / l1));
            double MinorRadius = Math.Sqrt(Math.Abs((b1 * b1 / l1 + b2 * b2 / l2 - x[0]) / l2));
            Matrix a = new DenseMatrix(2, 2);
            Vector c = new DenseVector(2);
            a[0, 0] = 1;
            a[1, 0] = a[0, 1] = x[3];
            a[1, 1] = x[4];
            c[0] = -x[1];
            c[1] = -x[2];
            Vector mp = (Vector)a.Solve(c);
            if (!mp.IsValid()) return null;


            //{	// ein Test, ob die Lösung auch stimmt. Es gibt Lösungen
            //    // die wohl eher Hyperbeln oder so was liefern und trotzdem alle
            //    // durch die Gleichungen gegebenen Bedingungen erfüllen
            //    if (MajorRadius < 1e-13) return null;
            //    if (MinorRadius < 1e-13) return null;
            //    double s = Math.Sin(MajorAngle);
            //    double cos = Math.Cos(MajorAngle);
            //    ModOp2D tuc = new ModOp2D(cos / MajorRadius, s / MajorRadius, (-cos * mp[0, 0] - s * mp[1, 0]) / MajorRadius,
            //                 -s / MinorRadius, cos / MinorRadius, (s * mp[0, 0] - cos * mp[1, 0]) / MinorRadius);
            //    // bildet auf den Einheitskreis ab
            //    for (int i = 0; i < 5; ++i)
            //    {
            //        GeoPoint2D pp = tuc * p[i];
            //        double e = Math.Abs(pp.x * pp.x + pp.y * pp.y - 1.0);
            //        if (e > 1e-5) return null;
            //    }
            //}

            // hier ist nun alles bestimmt:
            Angle MinorAngle = MajorAngle + SweepAngle.ToLeft;
            Ellipse2D res = new Ellipse2D(new GeoPoint2D(mp[0], mp[1]), MajorRadius * new GeoVector2D(MajorAngle), MinorRadius * new GeoVector2D(MinorAngle));
            return res;
        }
        public static Ellipse2D FromPoints(IEnumerable<GeoPoint2D> points)
        {
            GeoPoint2D[] pnts = null;
            if (points is GeoPoint2D[] a) pnts = a;
            else if (points is List<GeoPoint2D> l) pnts = l.ToArray();
            else
            {
                List<GeoPoint2D> lp = new List<GeoPoint2D>();
                foreach (GeoPoint2D point2D in points)
                {
                    lp.Add(point2D);
                }
                pnts = lp.ToArray();
            }
            Vector<double> observedX = new DenseVector(pnts.Length + 1); // there is no need to set values
            Vector<double> observedY = new DenseVector(pnts.Length + 1); // this is the data we want to achieve, namely 1.0, points on the unit circle distance to origin
            for (int i = 0; i < observedY.Count; i++)
            {
                observedY[i] = 1;
            }
            observedY[pnts.Length] = 0; // the scalar product of minor and major axis
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-12, maximumIterations: 20);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    // parameters: the homogeneous matrix that projects the ellipse to the unit circle
                    ModOp2D m = new ModOp2D(vd[0], vd[1], vd[2], vd[3], vd[4], vd[5]);
                    DenseVector res = new DenseVector(pnts.Length + 1);
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        GeoPoint2D pp = m * pnts[i];
                        res[i] = pp.x * pp.x + pp.y * pp.y;
                    }
                    res[pnts.Length] = m[0, 0] * m[0, 1] + m[1, 0] * m[1, 1]; // the axis should be perpendicular
#if DEBUG
                    double err = 0.0;
                    for (int i = 0; i < pnts.Length; i++) err += (res[i] - 1) * (res[i] - 1);
                    err += res[pnts.Length] * res[pnts.Length];
#endif
                    return res;
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {
                    // parameters: the homogeneous matrix that projects the ellipse to the unit circle
                    ModOp2D m = new ModOp2D(vd[0], vd[1], vd[2], vd[3], vd[4], vd[5]);
                    var prime = new DenseMatrix(pnts.Length + 1, 6);
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        GeoPoint2D pp = m * pnts[i];
                        prime[i, 0] = 2 * pnts[i].x * pp.x;
                        prime[i, 1] = 2 * pnts[i].y * pp.x;
                        prime[i, 2] = 2 * pp.x;
                        prime[i, 3] = 2 * pnts[i].x * pp.y;
                        prime[i, 4] = 2 * pnts[i].y * pp.y;
                        prime[i, 5] = 2 * pp.y;
                    }
                    prime[pnts.Length, 0] = m[0, 1];
                    prime[pnts.Length, 1] = m[0, 0];
                    prime[pnts.Length, 2] = 0;
                    prime[pnts.Length, 3] = m[1, 1];
                    prime[pnts.Length, 4] = m[1, 0];
                    prime[pnts.Length, 5] = 0;
                    return prime;
                }), observedX, observedY);

            BoundingRect ext = new BoundingRect(pnts);
            NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { 2.0 / ext.Width, 0, -ext.GetCenter().x, 0, 2.0 / ext.Height, -ext.GetCenter().y }));
            if (mres.ReasonForExit == ExitCondition.Converged || mres.ReasonForExit == ExitCondition.RelativeGradient)
            {
                Vector<double> vd = mres.MinimizingPoint;
                ModOp2D m = new ModOp2D(vd[0], vd[1], vd[2], vd[3], vd[4], vd[5]);
                m = m.GetInverse();
                return new Ellipse2D(m * GeoPoint2D.Origin, m * GeoVector2D.XAxis, m * GeoVector2D.YAxis);
            }
            else
            {
                return null;
            }
        }

        #region ICurve2D Members
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.TangentPoints (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
        public override GeoPoint2D[] TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo)
        {
            GeoPoint2D StartU = toUnitCircle * FromHere;
            GeoPoint2D center = new GeoPoint2D(0.0, 0.0); // der Kreis im Mittelpunkt mit Radius 1.0
            GeoPoint2D[] res = Geometry.IntersectCC(new GeoPoint2D(StartU, center), Geometry.Dist(StartU, center) / 2.0, center, 1.0);
            for (int i = 0; i < res.Length; ++i) res[i] = fromUnitCircle * res[i];
            if (res.Length == 2 && (Geometry.Dist(res[0], CloseTo) > Geometry.Dist(res[1], CloseTo)))
            {
                GeoPoint2D tmp = res[0];
                res[0] = res[1];
                res[1] = tmp;
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MakeGeoObject (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override IGeoObject MakeGeoObject(Plane p)
        {
            Ellipse e = Ellipse.Construct();
            e.SetEllipseCenterAxis(p.ToGlobal(center), p.ToGlobal(majorAxis), p.ToGlobal(minorAxis));
            e.SweepParameter = Math.PI * 2.0;
            if (!counterClock) (e as ICurve).Reverse();
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
            // TODO: was ist mit der Richtung
            Ellipse e = Ellipse.Construct();
            e.SetEllipseCenterAxis(fromPlane.ToGlobal(center), fromPlane.ToGlobal(majorAxis), fromPlane.ToGlobal(minorAxis));
            return e.GetProjectedCurve(toPlane);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtent ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingRect GetExtent()
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            res.MinMax(left);
            res.MinMax(top);
            res.MinMax(bottom);
            res.MinMax(right);
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
            Angle majorang = majorAxis.Angle;
            double radiusx = majorAxis.Length;
            double radiusy = minorAxis.Length;
            ClipRect clr = new ClipRect(Rect);
            if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, 0, right, top)) return true;
            if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, 1, top, left)) return true;
            if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, 2, left, bottom)) return true;
            if (clr.EllipseArcHitTest(center, radiusx, radiusy, majorang, 3, bottom, right)) return true;
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (ICurve2D)"/>
        /// </summary>
        /// <param name="IntersectWith"></param>
        /// <returns></returns>
        public override GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {	// gesucht sind alle Schnittpunkte, also auch in der Verlängerung!
            Line2D l2d = IntersectWith as Line2D;
            if (l2d != null)
            {
                GeoPoint2DWithParameter[] res = l2d.Intersect(this); // die Linie kanns
                // aber noch die Rollen vertauschen
                for (int i = 0; i < res.Length; ++i)
                {	// par1 und par2 vertauschen
                    double tmp = res[i].par1;
                    res[i].par1 = res[i].par2;
                    res[i].par2 = tmp;
                }
                return res;
            }
            else if (IntersectWith is Circle2D) // geht auch für Arc2D
            {
                GeoPoint2D[] ip = Geometry.IntersectEC(center, majrad, minrad, majorAxis.Angle, (IntersectWith as Circle2D).Center, (IntersectWith as Circle2D).Radius);
                GeoPoint2DWithParameter[] res = new GeoPoint2DWithParameter[ip.Length];
                double error = 0.0;
                for (int i = 0; i < ip.Length; ++i)
                {
                    GeoPoint2D p0 = toUnitCircle * ip[i];
                    error += Math.Abs(1.0 - p0.ToVector().Length);
                    error += Math.Abs(1.0 - (ip[i] | (IntersectWith as Circle2D).Center) / (IntersectWith as Circle2D).Radius);
                    res[i].p = ip[i];
                    res[i].par1 = PositionOf(ip[i]);
                    res[i].par2 = IntersectWith.PositionOf(ip[i]); // liefert auch für Arc2D den richtigen Wert
                }
                if (error < 1e-7) // auf Radius 1 normierter fehler
                {   // wenn nicht, dann unten mit dreiecksschnitten lösen
                    return res;
                }
            }
            else if (IntersectWith is Ellipse2D) // geht auch für EllipseArc2D
            {
                Ellipse2D elli = (IntersectWith as Ellipse2D);
                Ellipse2D ellinorm = this.GetModified(elli.toUnitCircle) as Ellipse2D;
                GeoPoint2D[] ip = Geometry.IntersectEC(ellinorm.center, ellinorm.majrad, ellinorm.minrad, ellinorm.majorAxis.Angle, elli.toUnitCircle * elli.center, 1.0);
                GeoPoint2DWithParameter[] res = new GeoPoint2DWithParameter[ip.Length];
                for (int i = 0; i < ip.Length; ++i)
                {
                    res[i].p = elli.fromUnitCircle * ip[i];
                    res[i].par1 = PositionOf(res[i].p);
                    res[i].par2 = IntersectWith.PositionOf(res[i].p);
                }
                for (int i = 0; i < res.Length; i++)
                {
                    RefineIntersectionPoint(this, elli, ref res[i]);
                }
                if (((ellinorm.Center | GeoPoint2D.Origin) < Precision.eps) && res.Length == 0)
                {   // gleicher Mittelpunkt und keine Lösung, könnte zusammenhängender Bogen zweier identischen Ellipsen sein
                    bool connected = false;
                    GeoPoint2D p = GeoPoint2D.Origin;
                    double par0 = 0.0;
                    double par1 = 0.0;
                    if ((this.EndPoint | elli.StartPoint) < Precision.eps)
                    {
                        p = this.EndPoint;
                        par0 = 1.0;
                        par1 = 0.0;
                        connected = true;
                    }
                    if ((this.EndPoint | elli.EndPoint) < Precision.eps)
                    {
                        p = this.EndPoint;
                        par0 = 1.0;
                        par1 = 1.0;
                        connected = true;
                    }
                    if ((this.StartPoint | elli.StartPoint) < Precision.eps)
                    {
                        p = this.StartPoint;
                        par0 = 0.0;
                        par1 = 0.0;
                        connected = true;
                    }
                    if ((this.StartPoint | elli.EndPoint) < Precision.eps)
                    {
                        p = this.StartPoint;
                        par0 = 0.0;
                        par1 = 1.0;
                        connected = true;
                    }
                    if (connected)
                    {
                        res = new GeoPoint2DWithParameter[1];
                        res[0] = new GeoPoint2DWithParameter(p, par0, par1);
                    }
                }
                return res;
            }
            // auch wenn die Ellipsenschnittpunkte schlecht waren
            return base.Intersect(IntersectWith);
        }
        private static void RefineIntersectionPoint(Ellipse2D e1, Ellipse2D e2, ref GeoPoint2DWithParameter ip)
        {   // hier mir Param arbeiten, da es sich auch um einen Ellipsenbogen handeln kann
            double par1 = e1.ParamOf(ip.p);
            double par2 = e2.ParamOf(ip.p);
            GeoPoint2D p1 = e1.PointAtParam(par1);
            GeoPoint2D p2 = e2.PointAtParam(par2);
            int counter = 0;
            while (!Precision.IsEqual(p1, p2))
            {
                GeoVector2D d1 = e1.DirectionAtParam(par1);
                GeoVector2D d2 = e2.DirectionAtParam(par2);
                GeoPoint2D p;
                if (Geometry.IntersectLL(p1, d1, p2, d2, out p))
                {
                    par1 = e1.ParamOf(p);
                    par2 = e2.ParamOf(p);
                    p1 = e1.PointAtParam(par1);
                    p2 = e2.PointAtParam(par2);
                    ip.p = p;
                }
                else break;
                ++counter;
                if (counter > 100) break;
            }
            ip.par1 = e1.PositionOf(ip.p); // richtige Werte auch für Arc
            ip.par2 = e2.PositionOf(ip.p);
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
            return new Ellipse2D(m * center, m * majorAxis, m * minorAxis); // Hauptachsentransformation im Konstruktor
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
            if (a < 0.0) a += 2.0 * Math.PI;
            if (!counterClock) a = 2.0 * Math.PI - a;
            return a / (2.0 * Math.PI);
        }
        internal double ParamOf(GeoPoint2D p)
        {
            GeoPoint2D p0 = toUnitCircle * p;
            double a = Math.Atan2(p0.y, p0.x);
            if (a < 0.0) a += 2.0 * Math.PI;
            if (!counterClock) a = 2.0 * Math.PI - a;
            return a;
        }
        internal GeoPoint2D PointAtParam(double Position)
        {
            double a = Position;
            if (!counterClock) a = 2.0 * Math.PI - a;
            GeoVector2D dbgx = fromUnitCircle * GeoVector2D.XAxis;
            GeoVector2D dbgy = fromUnitCircle * GeoVector2D.YAxis;
            return fromUnitCircle * new GeoPoint2D(Math.Cos(a), Math.Sin(a));
        }
        internal GeoVector2D DirectionAtParam(double param)
        {
            double a = param;
            if (!counterClock) a = 2.0 * Math.PI - a;
            ModOp2D to, from;
            double maj, min;
            GetAxisAlignedEllipse(out maj, out min, out to, out from);
            return from * new GeoVector2D(-maj * Math.Sin(a), min * Math.Cos(a));
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PerpendicularFoot (GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <returns></returns>
        public override GeoPoint2D[] PerpendicularFoot(GeoPoint2D fromHere)
        {
            return base.PerpendicularFoot(fromHere);
        }
        protected GeoPoint2D ClosestPerpendicularFoot(GeoPoint2D fromHere)
        {
            //GeoPoint2D p0 = toUnitCircle * fromHere;
            //double a = Math.Atan2(p0.y, p0.x);
            //if (a < 0.0) a += 2.0 * Math.PI;
            //if (!counterClock) a = 2.0 * Math.PI - a;
            //double u = a / (2 * Math.PI);
            //GeoPoint2D[] res = new GeoPoint2D[2];
            //return PointAt(NewtonPerpendicular(fromHere, u));
            GeoPoint2D[] pf = PerpendicularFoot(fromHere);
            double mindist = double.MaxValue;
            int ind = -1;
            for (int i = 0; i < pf.Length; i++)
            {
                double dist = pf[i] | fromHere;
                if (dist < mindist)
                {
                    mindist = dist;
                    ind = i;
                }
            }
            if (ind >= 0) return pf[ind];
            // the following will probably never be called:
            // see: http://www.geometrictools.com/Documentation/DistancePointToEllipse2.pdf
            // they seem to prefer Newton too, otherwise solve an equation of degree 4.
            double a, b;
            ModOp2D to, from;
            GetAxisAlignedEllipse(out a, out b, out to, out from);
            GeoPoint2D p = to * fromHere;
            double x = 0.0, y = 0.0;
            int iter = 0;
            double d = Geometry.DistancePointEllipse(p.x, p.y, a, b, Precision.eps, 16, ref iter, ref x, ref y);
            p = from * new GeoPoint2D(x, y);
            double par = ParamOf(p);
            return PointAtParam(par);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Distance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double Distance(GeoPoint2D p)
        {
            double res = double.MaxValue;
            double d = ClosestPerpendicularFoot(p) | p;
            if (d < res) res = d;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MinDistance (ICurve2D)"/>
        /// </summary>
        /// <param name="Other"></param>
        /// <returns></returns>
        public override double MinDistance(ICurve2D Other)
        {
            return base.MinDistance(Other);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MinDistance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double MinDistance(GeoPoint2D p)
        {
            return Distance(p); // geschlossen ist hier das selbe
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoPoint2D PointAt(double Position)
        {
            double a = Position * 2.0 * Math.PI;
            if (counterClock == fromUnitCircle.Determinant < 0) a = 2.0 * Math.PI - a; // a reflecting matrix also reverses the ellipse, we only need to reverse it once
            GeoVector2D dbgx = fromUnitCircle * GeoVector2D.XAxis;
            GeoVector2D dbgy = fromUnitCircle * GeoVector2D.YAxis;
            return fromUnitCircle * new GeoPoint2D(Math.Cos(a), Math.Sin(a));
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoVector2D DirectionAt(double Position)
        {
            double sweepParameter;
            if (counterClock) sweepParameter = 2.0 * Math.PI;
            else sweepParameter = -2.0 * Math.PI;
            Angle a = Position * sweepParameter;
            ModOp2D to, from;
            double maj, min;
            GetAxisAlignedEllipse(out maj, out min, out to, out from);
            if (counterClock) return (2.0 * Math.PI) * (from * new GeoVector2D(-maj * Math.Sin(a), min * Math.Cos(a)));
            else return -(2.0 * Math.PI) * (from * new GeoVector2D(-maj * Math.Sin(a), min * Math.Cos(a)));

            //double a = Position * 2.0 * Math.PI;
            //if (!counterClock) a = 2.0 * Math.PI - a;
            //GeoVector2D res;
            //if (counterClock)
            //{
            //    res = fromUnitCircle * new GeoVector2D(-Math.Sin(a), Math.Cos(a));
            //}
            //else
            //{
            //    res = fromUnitCircle * new GeoVector2D(Math.Sin(a), -Math.Cos(a));
            //}
            //// res.Norm(); // sollte nicht genormt werden, oder?
            //return res;
        }
        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2)
        {
            double sweepParameter;
            if (counterClock) sweepParameter = 2.0 * Math.PI;
            else sweepParameter = -2.0 * Math.PI;
            double u = position * sweepParameter;
            double su = Math.Sin(u);
            double cu = Math.Cos(u);
            point = fromUnitCircle * new GeoPoint2D(cu, su);
            double pp = 2 * Math.PI;
            double ppp = 4 * Math.PI * Math.PI;
            if (!counterClock) pp = -pp;
            deriv1 = pp * new GeoVector2D(fromUnitCircle[0, 1] * cu - fromUnitCircle[0, 0] * su, fromUnitCircle[1, 1] * cu - fromUnitCircle[1, 0] * su);
            deriv2 = ppp * new GeoVector2D(-fromUnitCircle[0, 1] * su - fromUnitCircle[0, 0] * cu, -fromUnitCircle[1, 1] * su - fromUnitCircle[1, 0] * cu);
            return true;
        }
        public override bool TryPointDeriv3At(double position, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2, out GeoVector2D deriv3)
        {
            double sweepParameter;
            if (counterClock) sweepParameter = 2.0 * Math.PI;
            else sweepParameter = -2.0 * Math.PI;
            double u = position * sweepParameter;
            double su = Math.Sin(u);
            double cu = Math.Cos(u);
            point = fromUnitCircle * new GeoPoint2D(cu, su);
            double pp = 2 * Math.PI;
            double ppp = 4 * Math.PI * Math.PI;
            double pppp = 8 * Math.PI * Math.PI * Math.PI;
            if (!counterClock)
            {
                pp = -pp;
                pppp = -pppp;
            }
            deriv1 = pp * new GeoVector2D(fromUnitCircle[0, 1] * cu - fromUnitCircle[0, 0] * su, fromUnitCircle[1, 1] * cu - fromUnitCircle[1, 0] * su);
            deriv2 = ppp * new GeoVector2D(-fromUnitCircle[0, 1] * su - fromUnitCircle[0, 0] * cu, -fromUnitCircle[1, 1] * su - fromUnitCircle[1, 0] * cu);
            deriv3 = pppp * new GeoVector2D(fromUnitCircle[0, 0] * su - fromUnitCircle[0, 1] * cu, fromUnitCircle[1, 0] * su - fromUnitCircle[1, 1] * cu);
            return true;
        }
        public double NewtonPerpendicular(GeoPoint2D fromHere, double position)
        {
            NewtonMinimizer nm = new NewtonMinimizer(1e-6, 10);
            IObjectiveFunction iof = ObjectiveFunction.GradientHessian(
                new Func<Vector<double>, (double, Vector<double>, Matrix<double>)>(delegate (Vector<double> vd)
                {
                    // vd[0] is the 0..1 parameter of the curve
                    TryPointDeriv3At(vd[0], out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2, out GeoVector2D deriv3);
                    GeoVector2D toPoint = fromHere - point;
                    double s1 = toPoint * deriv1;
                    double s2 = (toPoint * deriv2 - deriv1 * deriv1);
                    double val = sqr(s1);
                    Vector<double> gradient = new DenseVector(new double[] { 2 * s1 * s2 });
                    Matrix<double> hessian = new DenseMatrix(1, 1);
                    hessian[0, 0] = 2 * s1 * (toPoint * deriv3 - 3 * deriv1 * deriv2) + 2 * sqr(s2);
                    return (val, gradient, hessian);
                }));
            try
            {
                MinimizationResult mres = nm.FindMinimum(iof, new DenseVector(new double[] { position }));
                return mres.MinimizingPoint[0];
            }
            catch (Exception)
            {
                return iof.Point[0];
            }
        }
        public override double Length
        {
            get
            {
                // Umfang der Ellipse gemäß http://mathworld.wolfram.com/Ellipse.html
                double a = majorAxis.Length;
                double b = minorAxis.Length;
                double h = (a - b) / (a + b);
                h = h * h;
                return Math.PI * (a + b) * (1 + (3 * h) / (10 + Math.Sqrt(4 - 3 * h)));
            }
        }
        public override double Sweep
        {
            get
            {
                if (counterClock) return SweepAngle.Full;
                else return SweepAngle.FullReverse;
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
            if (StartPos < EndPos)
            {
                EllipseArc2D ea = new EllipseArc2D(center, majorAxis, minorAxis, Angle.A0, Sweep, left, right, bottom, top);
                return ea.Trim(StartPos, EndPos);
            }
            else
            {
                return EllipseArc2D.Create(center, majorAxis, minorAxis, PointAt(StartPos), PointAt(EndPos), counterClock);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Split (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override ICurve2D[] Split(double Position)
        {
            if (Position > 0.0 && Position < 1.0)
            {
                return new ICurve2D[] { Trim(0.0, Position), Trim(Position, 1.0) };
            }
            return new ICurve2D[] { Clone() };
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Reverse ()"/>
        /// </summary>
        public override void Reverse()
        {
            counterClock = !counterClock;
            // Recalc(); der Aufruf von Recalc dreht u.U. eine Achse um. Das ist explizit nicht gewünscht
            // vor allem, wenn ein Ellipsenbogen umgedreht wird, denn dann wird es dort sondt falsch
            // Recalc bezieht sich nicht auf counterClock. Deshalb sollte es auch nicht nötig sein
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Move (double, double)"/>
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        public override void Move(double dx, double dy)
        {
            center.x += dx;
            center.y += dy;
            Recalc();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.AddToGraphicsPath (System.Drawing.Drawing2D.GraphicsPath, bool)"/>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="forward"></param>
        public override void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {
            // aus dem Bogen übernommen, evtl besser in Kreisbögen zerlegen...
            int n = 36;
            PointF[] pnts = new PointF[n + 1]; // der erste und letzte Punkt ist identisch
            double step = 1.0 / n;
            for (int i = 0; i < n; ++i)
            {
                GeoPoint2D p = this.PointAt(i * step);
                pnts[i] = new PointF((float)p.x, (float)p.y);
            }
            GeoPoint2D endPoint = this.PointAt(1.0);
            pnts[n] = new PointF((float)endPoint.x, (float)endPoint.y);
            if (!forward) Array.Reverse(pnts);
            path.AddCurve(pnts);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ICurve2D Clone()
        {
            Ellipse2D res = new Ellipse2D(center, majorAxis, minorAxis);
            // die folgenden Daten einfach übernehmen
            res.counterClock = counterClock;
            res.toUnitCircle = toUnitCircle;
            res.fromUnitCircle = fromUnitCircle;
            res.left = left;
            res.right = right;
            res.bottom = bottom;
            res.top = top;
            res.majrad = majrad;
            res.minrad = minrad;
            res.majaxsin = majaxsin;
            res.majaxcos = majaxcos;
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
            Ellipse2D res = Clone() as Ellipse2D;
            if (reverse)
            {
                res.Reverse();
            }
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Copy (ICurve2D)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public override void Copy(ICurve2D toCopyFrom)
        {
            Ellipse2D c = toCopyFrom as Ellipse2D;
            counterClock = c.counterClock;
            toUnitCircle = c.toUnitCircle;
            fromUnitCircle = c.fromUnitCircle;
            left = c.left;
            right = c.right;
            bottom = c.bottom;
            top = c.top;
            majrad = c.majrad;
            minrad = c.minrad;
            majaxsin = c.majaxsin;
            majaxcos = c.majaxcos;
            UserData.CloneFrom(c.UserData);

        }
        public override bool IsClosed
        {
            get
            {
                return true;
            }
        }
        public override GeoVector2D StartDirection
        {
            get
            {
                GeoVector2D res;
                if (counterClock)
                {
                    res = fromUnitCircle * new GeoVector2D(0.0, 1.0);
                }
                else
                {
                    res = fromUnitCircle * new GeoVector2D(0.0, -1.0);
                }
                res.Norm();
                return res;
            }
        }
        public override GeoVector2D EndDirection
        {
            get
            {
                return StartDirection;
            }
        }
        public override GeoPoint2D StartPoint
        {
            get
            {
                return center + majorAxis;
            }
            set
            {
                if (!Precision.IsEqual(StartPoint, value))
                    throw new ApplicationException("cannot set StartPoint of Ellipse2D");
            }
        }
        public override GeoPoint2D EndPoint
        {
            get
            {
                return center + majorAxis;
            }
            set
            {
                double pos = PositionOf(value);
                GeoPoint2D point2D = PointAt(pos);
                if (!Precision.IsEqual(EndPoint, value))
                    throw new ApplicationException("cannot set EndPoint of Ellipse2D");
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetArea ()"/>
        /// </summary>
        /// <returns></returns>
        public override double GetArea()
        {   // es geht um die Fläche vom Nullpunkt aus gesehen, aber hier egal, da geschlossen
            if (counterClock) return majrad * minrad * Math.PI;
            else return -(majrad * minrad * Math.PI);
        }

        #endregion
        public GeoPoint2D Center
        {
            get { return center; }
            set
            {
                center = value;
                Recalc();
            }
        }
        public GeoVector2D MajorAxis
        {
            get { return majorAxis; }
            set
            {
                // das macht keinen Sinn: beide Achsen müssen gleichzeitig gesetzt werden,
                // denn sie müssen immer senkrecht stehen. Hier nützt auch RecalcAxis nichts,
                // denn das verändert diese Achse gleich wieder.
                throw new NotImplementedException("Ellipse2D: Set MajorAxis");
            }
        }
        public GeoVector2D MinorAxis
        {
            get { return minorAxis; }
            set
            {
                // s.o.
                throw new NotImplementedException("Ellipse2D: Set MinorAxis");
            }
        }

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Ellipse2D(SerializationInfo info, StreamingContext context)
        {
            center = (GeoPoint2D)info.GetValue("Center", typeof(GeoPoint2D));
            majorAxis = (GeoVector2D)info.GetValue("MajorAxis", typeof(GeoVector2D));
            minorAxis = (GeoVector2D)info.GetValue("MinorAxis", typeof(GeoVector2D));
            try
            {
                counterClock = (bool)info.GetValue("CounterClock", typeof(bool));
            }
            catch (SerializationException)
            {
                counterClock = true;
            }

            Recalc();
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Center", center, typeof(GeoPoint2D));
            info.AddValue("MajorAxis", majorAxis, typeof(GeoVector2D));
            info.AddValue("MinorAxis", minorAxis, typeof(GeoVector2D));
            info.AddValue("CounterClock", counterClock, typeof(bool));
        }

        #endregion

    }
}
