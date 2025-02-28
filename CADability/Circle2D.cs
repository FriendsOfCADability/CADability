using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Optimization;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.Curve2D
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable()]
    public class Circle2D : GeneralCurve2D, ISerializable
    {
        private GeoPoint2D center;
        private double radius;
        internal bool counterClock; // nur bei Vollkreis die Richtung (Bogen hat sweep)
        public Circle2D(GeoPoint2D center, double radius)
        {
            this.center = center;
            this.radius = Math.Abs(radius);
            counterClock = radius > 0.0;
        }
        public GeoPoint2D Center
        {
            get { return center; }
            set { center = value; }
        }
        public double Radius
        {
            get { return radius; }
            set { radius = value; }
        }
        public override string ToString()
        {
            return "Circle2D: (" + center.ToString() + ") " + radius.ToString();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (ICurve2D)"/>
        /// </summary>
        /// <param name="IntersectWith"></param>
        /// <returns></returns>
        public override GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {
            Circle2D c2d = IntersectWith as Circle2D;
            if (c2d != null)
            {
                GeoPoint2D[] isp = Geometry.IntersectCC(center, radius, c2d.center, c2d.radius);
                GeoPoint2DWithParameter[] res = new GeoPoint2DWithParameter[isp.Length];
                for (int i = 0; i < isp.Length; ++i)
                {
                    res[i].p = isp[i];
                    res[i].par1 = this.PositionOf(isp[i]);
                    res[i].par2 = c2d.PositionOf(isp[i]);
                }
                return res;
            }
            Line2D l2d = IntersectWith as Line2D;
            if (l2d != null)
            {
                GeoPoint2D[] isp = Geometry.IntersectLC(l2d.StartPoint, l2d.EndPoint, center, radius);
                GeoPoint2DWithParameter[] res = new GeoPoint2DWithParameter[isp.Length];
                for (int i = 0; i < isp.Length; ++i)
                {
                    res[i].p = isp[i];
                    res[i].par1 = this.PositionOf(isp[i]);
                    res[i].par2 = l2d.PositionOf(isp[i]);
                }
                return res;
            }
            if (IntersectWith is Ellipse2D) // git auch für Arc, ist dort jeweils implementiert
            {
                GeoPoint2DWithParameter[] res = IntersectWith.Intersect(this);
                for (int i = 0; i < res.Length; ++i)
                {   // Parameter im Ergebnis vertauschen
                    double tmp = res[i].par1;
                    res[i].par1 = res[i].par2;
                    res[i].par2 = tmp;
                }
                return res;
            }
            return base.Intersect(IntersectWith); // der allgemeine Fall
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
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PositionOf (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double PositionOf(GeoPoint2D p)
        {
            double res = Math.Atan2(p.y - center.y, p.x - center.x);
            if (res < 0.0) res += 2.0 * Math.PI;
            if (!counterClock) res = 2.0 * Math.PI - res;
            return res / (2.0 * Math.PI);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoPoint2D PointAt(double Position)
        {
            Angle a = Position * 2.0 * Math.PI;
            if (!counterClock) a = 2.0 * Math.PI - a.Radian;
            return new GeoPoint2D(center, radius, a);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoVector2D DirectionAt(double Position)
        {
            GeoVector2D r = (Math.PI * 2.0) * (PointAt(Position) - Center); // introduced factor (31.1.18) because of HelicalSurface, NewtonLineIntersection
            if (counterClock) return r.ToLeft();
            else return r.ToRight();
        }
        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {
            double par = position * 2.0 * Math.PI;
            if (counterClock)
            {
                point = new GeoPoint2D(center.x + radius * Math.Cos(par), center.y + radius * Math.Sin(par));
                deriv = (Math.PI * 2.0) * new GeoVector2D(-radius * Math.Sin(par), radius * Math.Cos(par));
                deriv2 = (Math.PI * 2.0) * (Math.PI * 2.0) * new GeoVector2D(-radius * Math.Cos(par), -radius * Math.Sin(par));
            }
            else
            {
                point = new GeoPoint2D(center.x + radius * Math.Cos(-par), center.y + radius * Math.Sin(-par));
                deriv = (Math.PI * 2.0) * new GeoVector2D(radius * Math.Sin(-par), -radius * Math.Cos(-par));
                deriv2 = (Math.PI * 2.0) * (Math.PI * 2.0) * new GeoVector2D(-radius * Math.Cos(-par), -radius * Math.Sin(-par));
            }
            return true;
        }
        internal override void GetTriangulationPoints(out GeoPoint2D[] interpol, out double[] interparam)
        {
            interpol = new GeoPoint2D[5];
            interparam = new double[] { 0, 0.25, 0.5, 0.75, 1.0 };
            for (int i = 0; i < interparam.Length; i++)
            {
                interpol[i] = PointAt(interparam[i]);
            }
        }
        public override GeoPoint2D StartPoint
        {
            get
            {
                return new GeoPoint2D(center.x + radius, center.y);
            }
            set
            {
                if (!Precision.IsEqual(StartPoint, value))
                {
                    throw new Curve2DException("unable to modify startpoint of a circle", Curve2DException.Curve2DExceptionType.General);
                }
            }
        }
        public override GeoPoint2D EndPoint
        {
            get
            {
                return new GeoPoint2D(center.x + radius, center.y);
            }
            set
            {
                if (!Precision.IsEqual(EndPoint, value))
                {
                    throw new Curve2DException("unable to modify endpoint of a circle", Curve2DException.Curve2DExceptionType.General);
                }
            }
        }
        public override GeoVector2D EndDirection
        {
            get
            {
                return GeoVector2D.YAxis;
            }
        }
        public override GeoVector2D StartDirection
        {
            get
            {
                if (counterClock) return GeoVector2D.YAxis;
                else return -GeoVector2D.YAxis;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Reverse ()"/>
        /// </summary>
        public override void Reverse()
        {
            counterClock = !counterClock;
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
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.TangentPoints (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <param name="CloseTo"></param>
        /// <returns></returns>
        public override GeoPoint2D[] TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo)
        {
            GeoPoint2D center2 = new GeoPoint2D(FromHere, center); // auf der Mitte
            GeoPoint2D[] res = Geometry.IntersectCC(center2, Geometry.Dist(center2, center), center, radius);
            if (res.Length == 2 && (Geometry.Dist(res[0], CloseTo) > Geometry.Dist(res[1], CloseTo)))
            {
                GeoPoint2D tmp = res[0];
                res[0] = res[1];
                res[1] = tmp;
            }
            return res;
        }
		public override GeoPoint2D[] TangentPointsToAngle(Angle ang, GeoPoint2D CloseTo)
		{
			GeoPoint2D[] res = Geometry.IntersectLC(center + radius * new GeoVector2D(ang).ToLeft(), center + radius * new GeoVector2D(ang).ToRight(), center, radius);
			if (res.Length == 2 && (Geometry.Dist(res[0], CloseTo) > Geometry.Dist(res[1], CloseTo)))
			{
				GeoPoint2D tmp = res[0];
				res[0] = res[1];
				res[1] = tmp;
			}
			return res;
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
            // berücksichtigt die Richtung
            Circle2D res = new Circle2D(center, radius + Dist);
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Distance (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double Distance(GeoPoint2D p)
        {
            double d = Geometry.Dist(p, center);
            return Math.Abs(d - radius);
        }
        public override double Length
        {
            get
            {
                return Radius * 2.0 * Math.PI;
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
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtent ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingRect GetExtent()
        {
            return new BoundingRect(center, radius, radius);
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
            if (clr.ArcHitTest(center, radius, 0, new GeoPoint2D(center.x + radius, center.y), new GeoPoint2D(center.x, center.y + radius))) return true;
            if (clr.ArcHitTest(center, radius, 1, new GeoPoint2D(center.x, center.y + radius), new GeoPoint2D(center.x - radius, center.y))) return true;
            if (clr.ArcHitTest(center, radius, 2, new GeoPoint2D(center.x - radius, center.y), new GeoPoint2D(center.x, center.y - radius))) return true;
            if (clr.ArcHitTest(center, radius, 3, new GeoPoint2D(center.x, center.y - radius), new GeoPoint2D(center.x + radius, center.y))) return true;
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ICurve2D Clone()
        {
            Circle2D res = new Circle2D(center, radius);
            res.counterClock = counterClock;
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
            Circle2D res = new Circle2D(center, radius);
            if (reverse) res.counterClock = !counterClock;
            else res.counterClock = counterClock;
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Copy (ICurve2D)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public override void Copy(ICurve2D toCopyFrom)
        {
            Circle2D c = toCopyFrom as Circle2D;
            center = c.center;
            radius = c.radius;
            UserData.CloneFrom(c.UserData);
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
                Arc2D res;
                if (counterClock) res = new Arc2D(center, radius, Angle.A0, SweepAngle.Full);
                else res = new Arc2D(center, radius, Angle.A0, SweepAngle.FullReverse);
                return res.Trim(StartPos, EndPos);
            }
            else
            {
                return new Arc2D(center, radius, PointAt(StartPos), PointAt(EndPos), counterClock);
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
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PerpendicularFoot (GeoPoint2D)"/>
        /// </summary>
        /// <param name="FromHere"></param>
        /// <returns></returns>
        public override GeoPoint2D[] PerpendicularFoot(GeoPoint2D FromHere)
        {
            if (Precision.IsEqual(FromHere, center)) return new GeoPoint2D[0];
            Angle a = new Angle(FromHere, center);
            return new GeoPoint2D[] { new GeoPoint2D(center, radius, a), new GeoPoint2D(center, radius, a + SweepAngle.Opposite) };
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
                return Other.MinDistance(this); // bei der Linie ist es definiert
            }
            else if (Other is Circle2D)
            {
                double minDist = Curves2D.SimpleMinimumDistance(this, Other); // dort werden die Schnitt- Start- Endpunkte miteinander verrechnet
                // jetzt nur noch gucken, ob sich die beiden Kreise oder Bögen annähern
                Circle2D c = Other as Circle2D;
                double cdist = Geometry.Dist(c.center, center);
                if (cdist < Precision.eps) return minDist; // mit gleichen Mittelpunkten funktioniert
                // das folgende nicht, aber der Abstand ist schon von SimpleMinimumDistance bestimmt
                // da dort auch die Fußpunkte verwendung finden
                if (cdist > c.radius + radius)
                {	// Annäherung zweier nicht schneidenden Kreise
                    GeoVector2D dir = c.center - center; // von hier zum anderen
                    dir.Norm();
                    GeoPoint2D p1 = center + radius * dir;
                    GeoPoint2D p2 = c.center + c.radius * dir.Opposite();
                    double pos1 = this.PositionOf(p1);
                    double pos2 = c.PositionOf(p2);
                    if (pos1 >= 0.0 && pos1 <= 1.0 && pos2 >= 0.0 && pos2 <= 1.0)
                    {
                        minDist = Math.Min(minDist, cdist - c.radius - radius);
                    }
                }
                else if (radius - c.radius > cdist)
                {	// Kreis c liegt innerhalb von diesem Kreis
                    GeoVector2D dir = c.center - center; // von hier zum anderen
                    dir.Norm();
                    GeoPoint2D p1 = center + radius * dir;
                    GeoPoint2D p2 = c.center + c.radius * dir;
                    double pos1 = this.PositionOf(p1);
                    double pos2 = c.PositionOf(p2);
                    if (pos1 >= 0.0 && pos1 <= 1.0 && pos2 >= 0.0 && pos2 <= 1.0)
                    {
                        minDist = Math.Min(minDist, Geometry.Dist(p1, p2));
                    }
                }
                else if (c.radius - radius > cdist)
                {	// dieser Kreis liegt innerhalb von c
                    GeoVector2D dir = center - c.center;
                    dir.Norm();
                    GeoPoint2D p1 = c.center + c.radius * dir;
                    GeoPoint2D p2 = center + radius * dir;
                    double pos1 = c.PositionOf(p1);
                    double pos2 = PositionOf(p2);
                    if (pos1 >= 0.0 && pos1 <= 1.0 && pos2 >= 0.0 && pos2 <= 1.0)
                    {
                        minDist = Math.Min(minDist, Geometry.Dist(p1, p2));
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
            Ellipse e = Ellipse.Construct();
            e.SetCirclePlaneCenterRadius(fromPlane, fromPlane.ToGlobal(Center), Radius);
            return e.GetProjectedCurve(toPlane);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.AddToGraphicsPath (System.Drawing.Drawing2D.GraphicsPath, bool)"/>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="forward"></param>
        public override void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {	// forward oder nicht ist doch egal, oder?
            // im Modus "System.Drawing.Drawing2D.FillMode.Alternate" scheint es egal zu sein,
            // im Modus "System.Drawing.Drawing2D.FillMode.Winding" dagegen nicht.
            // zu letzterem müsste man dem Ding eine Richtung geben können
            // deshalb jetzt implementiert mit AddArc statt AddEllipse. Der Modus "Winding"
            // gefällt mir besser und wir haben die Information ja!
            if (forward)
                path.AddArc((float)(Center.x - Radius), (float)(Center.y - Radius), (float)(2 * Radius),
                    (float)(2 * Radius), 0.0f, 360.0f);
            else
                path.AddArc((float)(Center.x - Radius), (float)(Center.y - Radius), (float)(2 * Radius),
                    (float)(2 * Radius), 0.0f, -360.0f);
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
        {
            if (counterClock) return radius * radius * Math.PI;
            else return -(radius * radius * Math.PI);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetAreaFromPoint (GeoPoint2D)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double GetAreaFromPoint(GeoPoint2D p)
        {
            if ((p | center) < radius) return GetArea();
            else return 0.0;
        }
        public override bool IsClosed
        {
            get
            {
                return true;
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
            if (toFuseWith is Circle2D)
            {
                if (toFuseWith is Arc2D)
                {   // Bögen nicht, es sei den volle
                    if (Math.Abs(Math.Abs((toFuseWith as Arc2D).Sweep) - Math.PI * 2.0) > 1e-8) return null;
                }
                Circle2D c2d = (toFuseWith as Circle2D);
                if ((center | c2d.center) + Math.Abs(radius - c2d.radius) < precision)
                {   // beides zusammen kleiner als precision
                    return new Circle2D(center / c2d.center, (radius + c2d.radius) / 2.0);
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
            double[] res = new double[2];
            res[0] = direction.ToRight().Angle.Radian / (Math.PI * 2);
            res[1] = direction.ToLeft().Angle.Radian / (Math.PI * 2);
            return res;
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Circle2D(SerializationInfo info, StreamingContext context)
        {
            center = (GeoPoint2D)info.GetValue("Center", typeof(GeoPoint2D));
            radius = (double)info.GetValue("Radius", typeof(double));
            try
            {
                counterClock = (bool)info.GetValue("CounterClock", typeof(bool));
            }
            catch (SerializationException)
            {
                counterClock = true;
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Center", center);
            info.AddValue("Radius", radius);
            info.AddValue("CounterClock", counterClock, typeof(bool));
        }

        internal static double Fit(IEnumerable<GeoPoint2D> points, ref GeoPoint2D center2d, ref double radius)
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
            Vector<double> observedX = new DenseVector(pnts.Length); // there is no need to set values
            Vector<double> observedY = new DenseVector(pnts.Length); // this is the data we want to achieve, namely 0.0
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-12, maximumIterations: 20);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    // parameters: 0:cx, 1:cy, 3: radius
                    GeoPoint2D cnt = new GeoPoint2D(vd[0], vd[1]);
                    DenseVector res = new DenseVector(pnts.Length);
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        res[i] = (pnts[i] | cnt) - vd[2];
                    }
#if DEBUG
                    double err = 0.0;
                    for (int i = 0; i < pnts.Length; i++) err += res[i] * res[i];
#endif
                    return res;
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {
                    // parameters: 0:cx, 1:cy, 3: radius
                    GeoPoint2D cnt = new GeoPoint2D(vd[0], vd[1]);
                    var prime = new DenseMatrix(pnts.Length, 3);
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        double d = pnts[i] | cnt;
                        prime[i, 0] = -(pnts[i].x - vd[0]) / d;
                        prime[i, 1] = -(pnts[i].y - vd[1]) / d;
                        prime[i, 2] = -1;
                    }
                    return prime;
                }), observedX, observedY);
            NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { center2d.x, center2d.y, radius }));
            if (mres.ReasonForExit == ExitCondition.Converged || mres.ReasonForExit == ExitCondition.RelativeGradient)
            {
                center2d = new GeoPoint2D(mres.MinimizingPoint[0], mres.MinimizingPoint[1]);
                radius = mres.MinimizingPoint[2];
                double err = 0.0;
                for (int i = 0; i < pnts.Length; i++)
                {
                    err += Math.Abs((pnts[i] | center2d) - radius);
                }
                return err;
            }
            else
            {
                return double.MaxValue;
            }
        }
        #endregion
    }
}
