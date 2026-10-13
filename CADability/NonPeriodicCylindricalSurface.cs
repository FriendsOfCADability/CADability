using CADability.Curve2D;
using System;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// A <see cref="CylindricalSurface"/> with a parameter space that has no seam: the (angle, axis position) system of the
    /// cylinder is replaced by polar coordinates in a plane, u = r·cos(angle), v = r·sin(angle) with r = axis position − vmin.
    /// vmin must lie below every axis position that is used, otherwise the mapping folds over. <see cref="Make3D"/> uses it for
    /// extrusions whose end curves cross the seam of the periodic cylinder. <see cref="INonPeriodicSurfaceConversion"/> converts
    /// points and 2d curves between the two parameter systems.
    /// </summary>
    [Serializable()]
    public class NonPeriodicCylindricalSurface : CylindricalSurface, INonPeriodicSurfaceConversion, ISerializable, IDeserializationCallback
    {
        // Achtung: Helper geht von anderem uv-System aus
        /// <summary>
        /// Copy constructor: creates a surface with the same geometry and the same origin of the polar system as
        /// <paramref name="toClone"/>.
        /// </summary>
        /// <param name="toClone">the surface to copy</param>
        public NonPeriodicCylindricalSurface(NonPeriodicCylindricalSurface toClone)
            : base(toClone)
        {
            this.vmin = toClone.vmin;
        }
        /// <summary>
        /// Creates the non-periodic version of <paramref name="toSubstitute"/> for faces whose v values (positions along the axis)
        /// lie between <paramref name="vmin"/> and <paramref name="vmax"/>. The origin of the polar system is placed half the
        /// interval below <paramref name="vmin"/>, so that all used points get a positive radius.
        /// </summary>
        /// <param name="toSubstitute">the periodic cylinder to replace</param>
        /// <param name="vmin">the smallest v value of the periodic cylinder that will be used</param>
        /// <param name="vmax">the largest v value of the periodic cylinder that will be used</param>
        public NonPeriodicCylindricalSurface(CylindricalSurface toSubstitute, double vmin, double vmax)
            : base(toSubstitute)
        {
            // vmin wird so bestimmt, dass es sicher kleiner ist als der zu erwartende v-Bereich
            this.vmin = vmin - (vmax - vmin) / 2.0;
        }
        /// <summary>
        /// Creates the surface from the transformation of the unit cylinder (see <see cref="CylindricalSurface"/>) and the v value
        /// of the periodic cylinder that corresponds to the origin of the polar system; used by <see cref="GetModified(ModOp)"/>.
        /// </summary>
        /// <param name="toCylinder">maps the unit cylinder (radius 1, axis = z axis) to this cylinder</param>
        /// <param name="vmin">the v value (position along the axis) of the periodic cylinder at the origin of the polar system; all used v values must be greater</param>
        /// <param name="usedArea">the part of the non-periodic parameter space that is actually used, if known</param>
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
        /// <inheritdoc/>
        public override string ToString()
        {
            return "NonPeriodicCylindricalSurface: " + "loc: " + Location.ToString() + "dirx: " + XAxis.ToString() + "diry: " + YAxis.ToString();
        }
        #region ISurface
        /// <inheritdoc/>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            return base.PointAt(toPeriodic(uv));
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            double u = uv.x;
            double v = uv.y;
            double sq = u * u + v * v;
            double rt = Math.Sqrt(sq);
            double rt3 = rt * rt * rt;
            return toCylinder * new GeoVector(1 / rt - u * u / rt3, -(u * v) / rt3, u / rt);
        }
        /// <inheritdoc/>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            double u = uv.x;
            double v = uv.y;
            double sq = u * u + v * v;
            double rt = Math.Sqrt(sq);
            double rt3 = rt * rt * rt;
            return toCylinder * new GeoVector(-(u * v) / rt3, 1 / rt - v * v / rt3, v / rt);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            return fromPeriodic(base.PositionOf(p));
        }
        /// <inheritdoc/>
        public override ISurface Clone()
        {
            return new NonPeriodicCylindricalSurface(this);
        }
        /// <inheritdoc/>
        public override void CopyData(ISurface CopyFrom)
        {
            base.CopyData(CopyFrom);
            this.vmin = (CopyFrom as NonPeriodicCylindricalSurface).vmin;
        }
        /// <inheritdoc/>
        public override bool IsUPeriodic
        {
            get
            {
                return false;
            }
        }
        /// <inheritdoc/>
        public override bool IsVPeriodic
        {
            get
            {
                return false;
            }
        }
        /// <inheritdoc/>
        public override ISurface GetModified(ModOp m)
        {
            return new NonPeriodicCylindricalSurface(m * toCylinder, vmin, usedArea);
        }
        /// <inheritdoc/>
        public override ICurve FixedU(double u, double vmin, double vmax)
        {
            return new FixedParameterCurve<NonPeriodicCylindricalSurface>(this, new GeoPoint2D(u, vmin), new GeoPoint2D(u, vmax));
        }
        /// <inheritdoc/>
        public override ICurve FixedV(double v, double umin, double umax)
        {
            return new FixedParameterCurve<NonPeriodicCylindricalSurface>(this, new GeoPoint2D(umin, v), new GeoPoint2D(umax, v));
        }
        /// <inheritdoc/>
        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            GeoPoint2D[] res = base.GetLineIntersection(startPoint, direction);
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = fromPeriodic(res[i]);
            }
            return res;
        }
        /// <inheritdoc/>
        public override ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {
            return new ProjectedCurve(curve, this, true, BoundingRect.EmptyBoundingRect);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override ICurve Make3dCurve(ICurve2D curve2d)
        {
            if (curve2d is ProjectedCurve && (curve2d as ProjectedCurve).Surface == this)
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
        /// <summary>
        /// Constructor for the legacy binary deserialization (<see cref="System.Runtime.Serialization.ISerializable"/>): reads the
        /// cylinder from the base class and the origin of the polar system. Files are written in the JSON format now; the binary
        /// format is still read.
        /// </summary>
        /// <param name="info">the data of the object</param>
        /// <param name="context">the source of the deserialization</param>
        protected NonPeriodicCylindricalSurface(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            vmin = info.GetDouble("Vmin");
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
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
    /// General implementation of a 3d curve on a surface along which one of the surface parameters is fixed, i.e. a
    /// line in the (u, v) parameter space that is either horizontal (fixed v) or vertical (fixed u), given by its start
    /// and end point in parameter space. Points and directions are computed by evaluating the surface.
    /// </summary>
    /// <typeparam name="T">the type of the surface</typeparam>
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
        /// <inheritdoc/>
        public override void Modify(ModOp m)
        {
            this.surface = (T)surface.GetModified(m);
            InvalidateSecondaryData();
        }
        /// <inheritdoc/>
        public override IGeoObject Clone()
        {
            return new FixedParameterCurve<T>(surface, startPoint, endPoint);
        }
        /// <inheritdoc/>
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
