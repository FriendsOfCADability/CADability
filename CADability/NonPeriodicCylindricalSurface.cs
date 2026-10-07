using CADability.Curve2D;
using System;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    [Serializable()]
    public class NonPeriodicCylindricalSurface : CylindricalSurface, INonPeriodicSurfaceConversion, ISerializable, IDeserializationCallback
    {
        // Achtung: Helper geht von anderem uv-System aus
        public NonPeriodicCylindricalSurface(NonPeriodicCylindricalSurface toClone)
            : base(toClone)
        {
            this.vmin = toClone.vmin;
        }
        public NonPeriodicCylindricalSurface(CylindricalSurface toSubstitute, double vmin, double vmax)
            : base(toSubstitute)
        {
            // vmin wird so bestimmt, dass es sicher kleiner ist als der zu erwartende v-Bereich
            this.vmin = vmin - (vmax - vmin) / 2.0;
        }
        public NonPeriodicCylindricalSurface(ModOp toCylinder, double vmin, BoundingRect? usedArea = null) 
            : base(toCylinder, usedArea)
        {
            // vmin wird so bestimmt, dass es sicher kleiner ist als der zu erwartende v-Bereich
            this.vmin = vmin;
        }
        private double vmin; // alle Werte müssen über vmin liegen
        #region Umwandlungen Periodisch<->nicht Periodisch
        private GeoPoint2D toPeriodic(GeoPoint2D uv)
        {
            return new GeoPoint2D(Math.Atan2(uv.y, uv.x), vmin + Math.Sqrt(uv.x * uv.x + uv.y * uv.y));
        }
        private GeoPoint2D fromPeriodic(GeoPoint2D uv)
        {
            double r = uv.y - vmin;
            return new GeoPoint2D(r * Math.Cos(uv.x), r * Math.Sin(uv.x));
        }
        private ICurve2D fromPeriodic(ICurve2D curve2d)
        {
            // gegeben: Kurve im periodischen System, gesucht: Kurve im nichtperiodischen System
            if (curve2d is Line2D)
            {
                Line2D l2d = curve2d as Line2D;
                if (Math.Abs(l2d.StartDirection.y) < 1e-6)
                {   // horizontale Linie im periodischen System wird Kreisbogen um den Mittelpunkt im nichtperiodischen System
                    Arc2D a2d = new Arc2D(GeoPoint2D.Origin, l2d.StartPoint.y - vmin, fromPeriodic(l2d.StartPoint), fromPeriodic(l2d.EndPoint), l2d.EndPoint.x > l2d.StartPoint.x);
                    return a2d;
                }
                if (Math.Abs(l2d.StartDirection.x) < 1e-6)
                {   // vertikale Linie um periodischen System sollte Linie durch den Ursprung im nichtperiodischen System werden
                    return new Line2D(fromPeriodic(l2d.StartPoint), fromPeriodic(l2d.EndPoint));
                }
            }
            return new TransformedCurve2D(curve2d, new NonPeriodicTransformation(vmin));
        }
        #endregion
        public override string ToString()
        {
            return "NonPeriodicCylindricalSurface: " + "loc: " + Location.ToString() + "dirx: " + XAxis.ToString() + "diry: " + YAxis.ToString();
        }
        #region ISurface
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            return base.PointAt(toPeriodic(uv));
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.DerivationAt (GeoPoint2D, out GeoPoint, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <param name="location"></param>
        /// <param name="du"></param>
        /// <param name="dv"></param>
        public override void DerivationAt(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv)
        {
            // Maxima code:
            // fx(u,v):= cos(atan2(v,u));
            // fy(u,v):=sin(atan2(v,u));
            // fz(u,v):=sqrt(u*u+v*v);
            // diff(fx(u,v),u,1); u.s.w.
            // diff(fx(u,v),u,2);
            double u = uv.x;
            double v = uv.y;
            double sq = u * u + v * v;
            double rt = Math.Sqrt(sq);
            double rt3 = rt * rt * rt;
            location = toCylinder * new GeoPoint(u / rt, v / rt, rt);
            du = toCylinder * new GeoVector(1 / rt - u * u / rt3, -(u * v) / rt3, u / rt);
            dv = toCylinder * new GeoVector(-(u * v) / rt3, 1 / rt - v * v / rt3, v / rt);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Derivation2At (GeoPoint2D, out GeoPoint, out GeoVector, out GeoVector, out GeoVector, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <param name="location"></param>
        /// <param name="du"></param>
        /// <param name="dv"></param>
        /// <param name="duu"></param>
        /// <param name="dvv"></param>
        /// <param name="duv"></param>
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv,
            out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            double u = uv.x;
            double v = uv.y;
            double sq = u * u + v * v;
            double rt = Math.Sqrt(sq);
            double rt3 = rt * rt * rt;
            double rt5 = rt3 * rt * rt;
            location = toCylinder * new GeoPoint(u / rt, v / rt, rt);
            du = toCylinder * new GeoVector(1 / rt - u * u / rt3, -(u * v) / rt3, u / rt);
            dv = toCylinder * new GeoVector(-(u * v) / rt3, 1 / rt - v * v / rt3, v / rt);
            duu = toCylinder * new GeoVector((3 * u * u * u) / rt5 - (3 * u) / rt3, (3 * u * u * v) / rt5 - v / rt3, 1 / rt - u * u / rt3);
            dvv = toCylinder * new GeoVector((3 * u * v * v) / rt5 - u / rt3, (3 * v * v * v) / rt5 - (3 * v) / rt3, 1 / rt - v * v / rt3);
            duv = toCylinder * new GeoVector((3 * u * u * v) / rt5 - v / rt3, (3 * u * v * v) / rt5 - u / rt3, -(u * v) / rt3);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            double u = uv.x;
            double v = uv.y;
            double sq = u * u + v * v;
            double rt = Math.Sqrt(sq);
            double rt3 = rt * rt * rt;
            return toCylinder * new GeoVector(1 / rt - u * u / rt3, -(u * v) / rt3, u / rt);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            double u = uv.x;
            double v = uv.y;
            double sq = u * u + v * v;
            double rt = Math.Sqrt(sq);
            double rt3 = rt * rt * rt;
            return toCylinder * new GeoVector(-(u * v) / rt3, 1 / rt - v * v / rt3, v / rt);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            double u = uv.x;
            double v = uv.y;
            double sq = u * u + v * v;
            double rt = Math.Sqrt(sq);
            double rt3 = rt * rt * rt;
            GeoVector du = toCylinder * new GeoVector(1 / rt - u * u / rt3, -(u * v) / rt3, u / rt);
            GeoVector dv = toCylinder * new GeoVector(-(u * v) / rt3, 1 / rt - v * v / rt3, v / rt);
            return du ^ dv;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            return fromPeriodic(base.PositionOf(p));
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            return new NonPeriodicCylindricalSurface(this);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            base.CopyData(CopyFrom);
            this.vmin = (CopyFrom as NonPeriodicCylindricalSurface).vmin;
        }
        public override bool IsUPeriodic
        {
            get
            {
                return false;
            }
        }
        public override bool IsVPeriodic
        {
            get
            {
                return false;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            return new NonPeriodicCylindricalSurface(m * toCylinder, vmin, usedArea);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.FixedU (double, double, double)"/>
        /// </summary>
        /// <param name="u"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public override ICurve FixedU(double u, double vmin, double vmax)
        {
            return new FixedParameterCurve<NonPeriodicCylindricalSurface>(this, new GeoPoint2D(u, vmin), new GeoPoint2D(u, vmax));
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.FixedV (double, double, double)"/>
        /// </summary>
        /// <param name="v"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <returns></returns>
        public override ICurve FixedV(double v, double umin, double umax)
        {
            return new FixedParameterCurve<NonPeriodicCylindricalSurface>(this, new GeoPoint2D(umin, v), new GeoPoint2D(umax, v));
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetLineIntersection (GeoPoint, GeoVector)"/>
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            GeoPoint2D[] res = base.GetLineIntersection(startPoint, direction);
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = fromPeriodic(res[i]);
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetProjectedCurve (ICurve, double)"/>
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {
            return new ProjectedCurve(curve, this, true, BoundingRect.EmptyBoundingRect);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetPlaneIntersection (PlaneSurface, double, double, double, double, double)"/>
        /// </summary>
        /// <param name="pl"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision)
        {
            IDualSurfaceCurve[] idsc = base.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
            if (idsc == null || idsc.Length == 0) return idsc;
            IDualSurfaceCurve dsc = idsc[0];
            if (dsc != null)
            {
                dsc = new DualSurfaceCurve(dsc.Curve3D, this, new ProjectedCurve(dsc.Curve3D, this, true, new BoundingRect(umin, vmin, umax, vmax)), dsc.Surface2, dsc.Curve2D2);
            }
            return new IDualSurfaceCurve[] { dsc };
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Make3dCurve (ICurve2D)"/>
        /// </summary>
        /// <param name="curve2d"></param>
        /// <returns></returns>
        public override ICurve Make3dCurve(ICurve2D curve2d)
        {
            if (ProjectedCurve.IsPlain(curve2d) && (curve2d as ProjectedCurve).Surface == this)
            {
                return (curve2d as ProjectedCurve).Curve3D;
            }
            throw new NotImplementedException();
        }
        #endregion
        #region INonPeriodicSurfaceConversion Members
        GeoPoint2D INonPeriodicSurfaceConversion.ToPeriodic(GeoPoint2D uv)
        {
            return toPeriodic(uv);
        }
        GeoPoint2D INonPeriodicSurfaceConversion.FromPeriodic(GeoPoint2D uv)
        {
            return fromPeriodic(uv);
        }
        ICurve2D INonPeriodicSurfaceConversion.ToPeriodic(ICurve2D curve2d)
        {
            throw new NotImplementedException();
        }
        ICurve2D INonPeriodicSurfaceConversion.FromPeriodic(ICurve2D curve2d)
        {
            return fromPeriodic(curve2d);
        }
        #endregion
        internal class NonPeriodicTransformation : ICurveTransformation2D
        {
            double vmin;
            public NonPeriodicTransformation(double vmin)
            {
                this.vmin = vmin;
            }
            #region ICurveTransformation2D Members

            GeoPoint2D ICurveTransformation2D.ReverseTransformPoint(GeoPoint2D uv)
            {
                return new GeoPoint2D(Math.Atan2(uv.y, uv.x), vmin + Math.Sqrt(uv.x * uv.x + uv.y * uv.y));
            }
            GeoPoint2D ICurveTransformation2D.TransformPoint(GeoPoint2D uv)
            {
                double r = uv.y - vmin;
                return new GeoPoint2D(r * Math.Cos(uv.x), r * Math.Sin(uv.x));
            }
            GeoPoint2D ICurveTransformation2D.TransformPoint(ICurve2D curve, double par)
            {   // gesucht: Punkt der Kurve im nichtperiodischen System
                GeoPoint2D uv = curve.PointAt(par);
                double r = uv.y - vmin;
                return new GeoPoint2D(r * Math.Cos(uv.x), r * Math.Sin(uv.x));
            }
            GeoVector2D ICurveTransformation2D.TransformDeriv1(ICurve2D curve, double par)
            {   // gesucht: Richtung im nicht periodischen System
                GeoPoint2D uv = curve.PointAt(par);
                GeoVector2D dir = curve.DirectionAt(par);
                return new GeoVector2D(Math.Cos(uv.x) * (dir.y) - Math.Sin(uv.x) * (dir.x) * (uv.y - vmin),
                    Math.Cos(uv.x) * (dir.x) * (uv.y - vmin) + Math.Sin(uv.x) * (dir.y));
            }
            GeoVector2D ICurveTransformation2D.TransformDeriv2(ICurve2D curve, double par)
            {   // 2. Ableitung z.Z. nicht gefragt
                throw new NotImplementedException();
            }
            #endregion
        }
        #region ISerializable Members
        protected NonPeriodicCylindricalSurface(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            vmin = info.GetDouble("Vmin");
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Vmin", vmin);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            base.OnDeserialization(sender);
        }
        #endregion
    }

    /// <summary>
    /// Allgemeine Implementierung einer 3D Kurve auf einer Fläche mit festem u bzw v Parameter
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class FixedParameterCurve<T> : GeneralCurve where T : ISurface
    {
        T surface;
        GeoPoint2D startPoint;
        GeoPoint2D endPoint;
        public FixedParameterCurve(T surface, GeoPoint2D startPoint, GeoPoint2D endPoint)
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
            this.surface = surface;
        }
        protected override double[] GetBasePoints()
        {
            // die Kurve ist so etwas Parabelähnliches, hat also keine Wendepunkte. Deshalb sollten 3 Punkte genügen.
            // Das stimmt zwar für Zylinder, aber wie ist es bei anderen Flächen? Vielleicht solltem man optional
            // ein array von u bzw. v Punkten vorgeben können, die obligatorisch sind.
            return new double[] { 0, 0.5, 1 };
        }
        public override GeoVector DirectionAt(double Position)
        {
            if (Math.Abs(startPoint.x - endPoint.x) < Math.Abs(startPoint.y - endPoint.y))
            {   // also vertikal, da nur horizontal oder vertikal vorkommt
                GeoVector dir = surface.VDirection(startPoint + Position * (endPoint - startPoint));
                if (startPoint.y < endPoint.y) return dir;
                else return -dir;
            }
            else
            {   // horizontal
                GeoVector dir = surface.UDirection(startPoint + Position * (endPoint - startPoint));
                if (startPoint.x < endPoint.x) return dir;
                else return -dir;
            }
        }
        public override GeoPoint PointAt(double Position)
        {
            return surface.PointAt(startPoint + Position * (endPoint - startPoint));
        }
        public override void Reverse()
        {
            GeoPoint2D p = startPoint;
            startPoint = endPoint;
            endPoint = p;
            base.InvalidateSecondaryData();
        }
        public override ICurve[] Split(double Position)
        {
            if (Position <= 0.0 || Position >= 1.0)
                return new ICurve[1] { this.Clone() as ICurve };
            GeoPoint2D p = startPoint + Position * (endPoint - startPoint);
            return new ICurve[]
                {
                    new FixedParameterCurve<T>(surface, startPoint, p),
                    new FixedParameterCurve<T>(surface, p, endPoint)
                };
        }
        public override void Trim(double StartPos, double EndPos)
        {
            GeoPoint2D sp = startPoint + StartPos * (endPoint - startPoint);
            GeoPoint2D ep = startPoint + EndPos * (endPoint - startPoint);
            this.startPoint = sp;
            this.endPoint = ep;
            InvalidateSecondaryData();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            this.surface = (T)surface.GetModified(m);
            InvalidateSecondaryData();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            return new FixedParameterCurve<T>(surface, startPoint, endPoint);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            if (ToCopyFrom is FixedParameterCurve<T>)
            {
                this.surface = (ToCopyFrom as FixedParameterCurve<T>).surface;
                this.startPoint = (ToCopyFrom as FixedParameterCurve<T>).startPoint;
                this.endPoint = (ToCopyFrom as FixedParameterCurve<T>).endPoint;
                InvalidateSecondaryData();
            }
        }
    }
}
