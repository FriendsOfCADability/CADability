using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    [Serializable()]
    public class OffsetSurface : ISurfaceImpl, ISerializable, IExportStep
    {
        private ISurface baseSurface;
        private double offset;
        double umin, umax, vmin, vmax; // nur zur Kommunikation mit OCas nötig
        public OffsetSurface(ISurface baseSurface, double offset, BoundingRect? usedArea = null) : base(usedArea)
        {
            this.baseSurface = baseSurface;
            this.offset = offset;
            baseSurface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
        }
        public ISurface BaseSurface
        {
            get
            {
                return baseSurface;
            }
        }
        public double Offset
        {
            get
            {
                return offset;
            }
        }
        [Serializable]
        class FixedCurve : GeneralCurve, ISerializable
        {   // serialisierbar machen!
            ICurve fixedCurve;
            bool fixedU;
            OffsetSurface offsetSurface;
            double param, pmin, pmax;
            public FixedCurve(OffsetSurface offsetSurface, double param, double pmin, double pmax, bool fixedU)
            {
                this.fixedU = fixedU;
                this.offsetSurface = offsetSurface;
                this.param = param;
                this.pmin = pmin;
                this.pmax = pmax;
                if (fixedU)
                {
                    fixedCurve = offsetSurface.baseSurface.FixedU(param, pmin, pmax);
                }
                else
                {
                    fixedCurve = offsetSurface.baseSurface.FixedV(param, pmin, pmax);
                }
                if (fixedCurve is BSpline)
                {
                    GeoPoint dbg = (fixedCurve as BSpline).GetPole(0);
                    if (double.IsNaN(dbg.x))
                    {
                    }
                }
            }
            protected override double[] GetBasePoints()
            {
                double[] res = fixedCurve.GetSavePositions();
                return res;
            }

            public override GeoVector DirectionAt(double Position)
            {
                return fixedCurve.DirectionAt(Position);
                // Das stimt nicht, mit Maxima berechnen, braucht sicherlich 2. Ableitung der Fläche
            }

            public override GeoPoint PointAt(double Position)
            {
                GeoPoint2D uv;
                if (fixedU)
                {
                    uv = new GeoPoint2D(param, pmin + Position * (pmax - pmin));
                }
                else
                {
                    uv = new GeoPoint2D(pmin + Position * (pmax - pmin), param);
                }
                return offsetSurface.PointAt(uv);
            }

            public override void Reverse()
            {
                double tmp = pmin;
                pmin = pmax;
                pmax = tmp;
            }

            public override ICurve[] Split(double Position)
            {
                return fixedCurve.Split(Position);
            }

            public override void Trim(double StartPos, double EndPos)
            {
                fixedCurve.Trim(StartPos, EndPos);
            }

            /// <summary>
            /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
            /// </summary>
            /// <param name="m"></param>
            public override void Modify(ModOp m)
            {   // this curve cannot be modified, because it is defined by the underlying offset surface.
                // this is only called when a Face or Surface has been modified.
                InvalidateSecondaryData();
            }

            /// <summary>
            /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
            /// </summary>
            /// <returns></returns>
            public override IGeoObject Clone()
            {
                return new FixedCurve(offsetSurface, param, pmin, pmax, fixedU);
            }

            /// <summary>
            /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
            /// </summary>
            /// <param name="ToCopyFrom"></param>
            public override void CopyGeometry(IGeoObject ToCopyFrom)
            {
                throw new NotImplementedException();
            }

            public override GeoPoint EndPoint
            {
                get
                {
                    return base.EndPoint;
                }

                set
                {
                    GeoPoint2D p = offsetSurface.PositionOf(value);
                    if (fixedU) pmax = p.y;
                    else pmax = p.x;
                }
            }
            public override GeoPoint StartPoint
            {
                get
                {
                    return base.StartPoint;
                }

                set
                {
                    GeoPoint2D p = offsetSurface.PositionOf(value);
                    if (fixedU) pmin = p.y;
                    else pmin = p.x;
                }
            }

            #region ISerializable Members
            protected FixedCurve(SerializationInfo info, StreamingContext context)
            {
                fixedCurve = info.GetValue("FixedCurve", typeof(ICurve)) as ICurve;
                fixedU = info.GetBoolean("FixedU");
                offsetSurface = info.GetValue("OffsetSurface", typeof(OffsetSurface)) as OffsetSurface;
                param = info.GetDouble("Param");
                pmin = info.GetDouble("Pmin");
                pmax = info.GetDouble("Pmax");
            }
            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("FixedCurve", fixedCurve);
                info.AddValue("FixedU", fixedU);
                info.AddValue("OffsetSurface", offsetSurface);
                info.AddValue("Param", param);
                info.AddValue("Pmin", pmin);
                info.AddValue("Pmax", pmax);
            }
            #endregion
        }
        #region ISurfaceImpl overrides
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNaturalBounds (out double, out double, out double, out double)"/>
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            base.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            if (umin == double.MinValue || vmin == double.MinValue)
            {
                umin = this.umin; // diese Werte existieren immer (siehe Konstruktoren) Das Problem ist die BoxedSurface, die usedArea braucht
                umax = this.umax;
                vmin = this.vmin;
                vmax = this.vmax;
                // baseSurface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            }
        }
        public override bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
        {
            if (other is OffsetSurface)
            {
                OffsetSurface oos = (other as OffsetSurface);
                return baseSurface.SameGeometry(thisBounds, oos.baseSurface, otherBounds, precision, out firstToSecond) && oos.offset == offset;
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            return baseSurface.PointAt(uv) + offset * baseSurface.GetNormal(uv).Normalized;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            /* Formula from maxima, syntax:
             * load(vect);
             * f(x,y) := depends([f1,f2,f3],[x,y]);
             * g(x,y) := f(x,y)+c*(diff(f(x,y),x)~diff(f(x,y),y))/((diff(f(x,y),x)~diff(f(x,y),y)).(diff(f(x,y),x)~diff(f(x,y),y)))^0.5;
             * Note: "." means scalarproduct, "~" is crossprodukt, note that x~y~z is x~(y~z) not (x~y)~z
             * diff(g(x,y),x);
             * stringout("file",%);
             * With appropriate substitution you get the formula below.
             */

            GeoVector du, dv, duu, dvv, duv;
            GeoPoint loc;
            baseSurface.Derivation2At(uv, out loc, out du, out dv, out duu, out dvv, out duv);
            GeoVector crop;
            //CndHlp3D.GeoVector3D dbg = Helper.UDirection(uv.ToCndHlp());
            crop = (dv ^ (du ^ dv));
            //GeoVector dbg1 = -0.5 * offset * (duu * crop + (du * (dv ^ ((duu ^ dv) + (du ^ duv)))) + (du * (duv ^ (du ^ dv)))) / System.Math.Pow((du * crop), 1.5) * (du ^ dv);
            //GeoVector dgb2 = (offset / System.Math.Pow((du * crop), 0.5)) * ((duu ^ dv) + (du ^ duv));
            //GeoVector dgb4 = -0.5 * offset * (duu * crop + (du * (dv ^ ((duu ^ dv) + (du ^ duv)))) + (du * (duv ^ (du ^ dv)))) / System.Math.Pow((du * crop), 1.5) * (du ^ dv) + offset * ((duu ^ dv) + (du ^ duv)) / System.Math.Pow((du * crop), 0.5) + du;
            return -0.5 * offset * (duu * crop + (du * (dv ^ ((duu ^ dv) + (du ^ duv)))) + (du * (duv ^ (du ^ dv)))) / System.Math.Pow((du * crop), 1.5) * (du ^ dv) + offset * ((duu ^ dv) + (du ^ duv)) / System.Math.Pow((du * crop), 0.5) + du;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            /* Formula from maxima, syntax:
             * load(vect);
             * f(x,y) := depends([f1,f2,f3],[x,y]);
             * g(x,y) := f(x,y)+c*(diff(f(x,y),x)~diff(f(x,y),y))/((diff(f(x,y),x)~diff(f(x,y),y)).(diff(f(x,y),x)~diff(f(x,y),y)))^0.5;
             * Note: "." means scalarproduct, "~" is crossprodukt, note that x~y~z is x~(y~z) not (x~y)~z
             * diff(g(x,y),y);
             * With appropriate substitution you get the formula below.
             */
            GeoVector du, dv, duu, dvv, duv;
            GeoPoint loc;
            baseSurface.Derivation2At(uv, out loc, out du, out dv, out duu, out dvv, out duv);
            //CndHlp3D.GeoVector3D dbg = Helper.VDirection(uv.ToCndHlp());
            GeoVector crop = dv ^ (du ^ dv);
            //GeoVector dbg1 = (-0.5 * offset * ((duv * crop) + (du * (dvv ^ (du ^ dv))) + (du * (dv ^ ((duv ^ dv) + (du ^ dvv))))) / System.Math.Pow(du * crop, 1.5) * (du ^ dv)) + (offset / System.Math.Pow(du * crop, 0.5) * ((duv ^ dv) + (du ^ dvv))) + dv;
            return (-0.5 * offset * ((duv * crop) + (du * (dvv ^ (du ^ dv))) + (du * (dv ^ ((duv ^ dv) + (du ^ dvv))))) / System.Math.Pow(du * crop, 1.5) * (du ^ dv)) + (offset / System.Math.Pow(du * crop, 0.5) * ((duv ^ dv) + (du ^ dvv))) + dv;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            return baseSurface.GetNormal(uv); // the normal should be the same, and we would have to handle singularities here
            // return this.UDirection(uv) ^ this.VDirection(uv);
        }
        public override double[] GetUSingularities()
        {
            return baseSurface.GetUSingularities();
        }
        public override double[] GetVSingularities()
        {
            return baseSurface.GetVSingularities();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetZMinMax (Projection, double, double, double, double, ref double, ref double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="zMin"></param>
        /// <param name="zMax"></param>
        public override void GetZMinMax(Projection p, double umin, double umax, double vmin, double vmax, ref double zMin, ref double zMax)
        {
            throw new NotImplementedException("GetZMinMax not implemented");
        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            BaseSurface.Derivation2At(uv, out location, out du, out dv, out duu, out dvv, out duv);
            //base.Derivation2At(uv, out location, out du, out dv, out duu, out dvv, out duv);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Make3dCurve (ICurve2D)"/>
        /// </summary>
        /// <param name="curve2d"></param>
        /// <returns></returns>
        public override ICurve Make3dCurve(ICurve2D curve2d)
        {
            if (curve2d is Curve2DAspect)
            {
                ICurve res = (curve2d as Curve2DAspect).Get3DCurve(this);
                if (res != null) return res;
            }
            if (Curve3dOfProjected<OffsetSurface>(curve2d) is ICurve onThisSurface) return onThisSurface;
            return base.Make3dCurve(curve2d);
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
            return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            OffsetSurface res = new OffsetSurface(baseSurface.Clone(), offset);
            res.umin = umin;
            res.umax = umax;
            res.vmin = vmin;
            res.vmax = vmax;
            res.usedArea = usedArea;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {   // genaugenommen geht eine Modifikation hier nicht, wird aber z.Z. nur zur Translation verwendet
            boxedSurfaceEx = null;
            if (m.IsOrthogonal)
            {
                baseSurface = baseSurface.GetModified(m);
                offset = m * offset;
            }
            else throw new NotImplementedException("Modify OffsetSurface with non ortogonal Matrix");
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            if (m.IsIsogonal)
            {
                OffsetSurface res = new OffsetSurface(baseSurface.GetModified(m), offset * m.Factor, usedArea);
                return res;
            }
            else throw new NotImplementedException("Modify OffsetSurface with non ortogonal Matrix");
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            OffsetSurface cc = CopyFrom as OffsetSurface;
            if (cc != null)
            {
                this.baseSurface = cc.baseSurface.Clone();
                this.offset = cc.offset;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetLineIntersection (GeoPoint, GeoVector)"/>
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            return base.GetLineIntersection(startPoint, direction);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetOffsetSurface (double)"/>
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public override ISurface GetOffsetSurface(double offset)
        {
            return new OffsetSurface(baseSurface.Clone(), this.offset + offset);
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
            if (vmin > vmax)
            {
                FixedCurve fc = new FixedCurve(this, u, vmax, vmin, true);
                fc.Reverse();
                return fc;
            }
            else
            {
                return new FixedCurve(this, u, vmin, vmax, true);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.FixedV (double, double, double)"/>
        /// </summary>
        /// <param name="u"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <returns></returns>
        public override ICurve FixedV(double u, double umin, double umax)
        {
            if (umin > umax)
            {
                FixedCurve fc = new FixedCurve(this, u, umax, umin, false);
                fc.Reverse();
                return fc;
            }
            else
            {
                return new FixedCurve(this, u, umin, umax, false);
            }
        }
        public override bool IsUPeriodic
        {
            get
            {
                return baseSurface.IsUPeriodic;
            }
        }
        public override bool IsVPeriodic
        {
            get
            {
                return baseSurface.IsVPeriodic;
            }
        }
        public override double UPeriod => baseSurface.UPeriod;
        public override double VPeriod => baseSurface.VPeriod;
        public override RuledSurfaceMode IsRuled
        {
            get
            {
                return baseSurface.IsRuled;
            }
        }
        public override ModOp2D ReverseOrientation()
        {
            boxedSurfaceEx = null;
            offset = -offset;
            return baseSurface.ReverseOrientation();
        }
        #endregion
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            // to implement:
            return new GroupProperty("OffsetSurface", new IPropertyEntry[0]);
        }
        #region ISerializable Members
        protected OffsetSurface(SerializationInfo info, StreamingContext context)
        {
            baseSurface = info.GetValue("BaseSurface", typeof(ISurface)) as ISurface;
            offset = info.GetDouble("Offset");
            umin = info.GetDouble("Umin");
            umax = info.GetDouble("Umax");
            vmin = info.GetDouble("Vmin");
            vmax = info.GetDouble("Vmax");
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("BaseSurface", baseSurface);
            info.AddValue("Offset", offset);
            info.AddValue("Umin", umin);
            info.AddValue("Umax", umax);
            info.AddValue("Vmin", vmin);
            info.AddValue("Vmax", vmax);
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            int n = (baseSurface as IExportStep).Export(export, false);
            return export.WriteDefinition("OFFSET_SURFACE('',#" + n.ToString() + "," + export.ToString(offset) + ",.F.)");
        }
        #endregion
    }
}
