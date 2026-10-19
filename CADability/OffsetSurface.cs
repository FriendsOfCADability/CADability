using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// The surface at a constant distance from another surface: the point at (u, v) is the point of <see cref="BaseSurface"/>
    /// at (u, v) moved by <see cref="Offset"/> along the normal of the base surface, so both surfaces share the same parameter
    /// space. <see cref="ISurface.GetOffsetSurface(double)"/> returns it for surfaces that have no exact offset of their own
    /// kind, and the STEP import creates it for OFFSET_SURFACE entities. Derivatives are computed from the derivatives of the
    /// base surface; where the base surface is curved more strongly than the offset (radius of curvature below the offset)
    /// the offset surface is singular.
    /// </summary>
    [Serializable()]
    public class OffsetSurface : ISurfaceImpl, ISerializable, IExportStep
    {
        private ISurface baseSurface;
        private double offset;
        double umin, umax, vmin, vmax; // nur zur Kommunikation mit OCas nötig
        /// <summary>
        /// Creates the offset of <paramref name="baseSurface"/> at the distance <paramref name="offset"/>.
        /// </summary>
        /// <param name="baseSurface">the surface to offset; it is used as it is, not copied</param>
        /// <param name="offset">the distance along the normal of the base surface; negative values offset against the normal</param>
        /// <param name="usedArea">the rectangle in parameter space that is actually used, if known; it helps methods that need to sample the surface</param>
        public OffsetSurface(ISurface baseSurface, double offset, BoundingRect? usedArea = null) : base(usedArea)
        {
            this.baseSurface = baseSurface;
            this.offset = offset;
            baseSurface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
        }
        /// <summary>
        /// The surface this surface is the offset of.
        /// </summary>
        public ISurface BaseSurface
        {
            get
            {
                return baseSurface;
            }
        }
        /// <summary>
        /// The distance between the base surface and this surface, measured along the normal of the base surface; negative when
        /// this surface lies on the back side of the base surface.
        /// </summary>
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
            // the offset surface, or the surface it became by a modification (see Modify)
            ISurface offsetSurface;
            double param, pmin, pmax;
            public FixedCurve(OffsetSurface offsetSurface, double param, double pmin, double pmax, bool fixedU)
                : this(offsetSurface, fixedU ? offsetSurface.baseSurface.FixedU(param, pmin, pmax) : offsetSurface.baseSurface.FixedV(param, pmin, pmax), param, pmin, pmax, fixedU)
            {
            }
            private FixedCurve(ISurface offsetSurface, ICurve fixedCurve, double param, double pmin, double pmax, bool fixedU)
            {
                this.fixedU = fixedU;
                this.offsetSurface = offsetSurface;
                this.fixedCurve = fixedCurve;
                this.param = param;
                this.pmin = pmin;
                this.pmax = pmax;
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

            /// <inheritdoc/>
            public override void Modify(ModOp m)
            {   // The curve is defined by the surface. A face modifies its surface with GetModified, which returns a new surface,
                // so the curve must modify its own reference to the surface in the same way, or it stays where it was.
                offsetSurface = offsetSurface.GetModified(m);
                fixedCurve.Modify(m); // only used for directions and positions
                InvalidateSecondaryData();
            }

            /// <inheritdoc/>
            public override IGeoObject Clone()
            {
                return new FixedCurve(offsetSurface, fixedCurve.Clone() as ICurve, param, pmin, pmax, fixedU);
            }

            /// <inheritdoc/>
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
                offsetSurface = info.GetValue("OffsetSurface", typeof(ISurface)) as ISurface;
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
        {
            if (other is OffsetSurface)
            {
                OffsetSurface oos = (other as OffsetSurface);
                return baseSurface.SameGeometry(thisBounds, oos.baseSurface, otherBounds, precision, out firstToSecond) && oos.offset == offset;
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }

        /// <inheritdoc/>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            return baseSurface.PointAt(uv) + offset * baseSurface.GetNormal(uv).Normalized;
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            return baseSurface.GetNormal(uv); // the normal should be the same, and we would have to handle singularities here
            // return this.UDirection(uv) ^ this.VDirection(uv);
        }
        /// <inheritdoc/>
        public override double[] GetUSingularities()
        {
            return baseSurface.GetUSingularities();
        }
        /// <inheritdoc/>
        public override double[] GetVSingularities()
        {
            return baseSurface.GetVSingularities();
        }
        /// <inheritdoc/>
        public override void GetZMinMax(Projection p, double umin, double umax, double vmin, double vmax, ref double zMin, ref double zMax)
        {
            throw new NotImplementedException("GetZMinMax not implemented");
        }
        /// <inheritdoc/>
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            BaseSurface.Derivation2At(uv, out location, out du, out dv, out duu, out dvv, out duv);
            //base.Derivation2At(uv, out location, out du, out dv, out duu, out dvv, out duv);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision)
        {
            return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// Returns the modified surface. Under a rotation, reflection or translation, optionally with a uniform scaling, this is
        /// again an offset surface. Under any other modification, e.g. a scaling with different factors along the axes, the image
        /// of an offset surface is no offset surface: its distance to the modified base surface depends on the direction of the
        /// normal. Then the part of the parameter space in which the surface is used (the domain of its face) is approximated by a
        /// <see cref="NurbsSurface"/> in the same (u, v) parametrisation (see <see cref="Approximate"/>), and the modified
        /// approximation is returned, so the 2d curves of the face stay valid.
        /// </summary>
        /// <param name="m">the modification</param>
        /// <returns>the modified surface</returns>
        public override ISurface GetModified(ModOp m)
        {
            if (m.IsIsogonal) return new OffsetSurface(baseSurface.GetModified(m), offset * m.Factor, usedArea);
            if (UsedBounds(out BoundingRect bounds))
            {   // the approximation is modified, so its deviation is scaled by up to the largest stretch of m
                double[,] a = m.Matrix;
                double stretch = 0.0;
                for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) stretch += a[i, j] * a[i, j];
                stretch = Math.Sqrt(stretch); // the Frobenius norm is at least the largest stretch
                NurbsSurface approximation = Approximate(bounds.Left, bounds.Right, bounds.Bottom, bounds.Top, Precision.eps / Math.Max(stretch, 1.0));
                return approximation.GetModified(m);
            }
            // without a finite part of the parameter space there is nothing to approximate: keep the result of the versions
            // before #415 for a scaling along perpendicular axes, which has the right shape only for a uniform scaling
            if (HasPerpendicularAxes(m)) return new OffsetSurface(baseSurface.GetModified(m), offset * m.Factor, usedArea);
            throw new NotImplementedException("Modify OffsetSurface without bounds with a non orthogonal matrix");
        }
        /// <summary>
        /// The part of the parameter space in which this surface is used: <see cref="ISurfaceImpl.usedArea"/> or the natural
        /// bounds, if they are finite.
        /// </summary>
        private bool UsedBounds(out BoundingRect bounds)
        {
            bounds = usedArea;
            if (IsFinite(bounds)) return true;
            GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
            bounds = new BoundingRect(umin, vmin, umax, vmax);
            return IsFinite(bounds);
        }
        private static bool IsFinite(BoundingRect r)
        {
            return !r.IsEmpty() && !r.IsInfinite && r.Width > 0.0 && r.Height > 0.0 &&
                r.Left > double.MinValue && r.Right < double.MaxValue && r.Bottom > double.MinValue && r.Top < double.MaxValue;
        }
        /// <summary>
        /// Approximates this surface in the given part of its parameter space by a non rational <see cref="NurbsSurface"/> with
        /// the same parametrisation: the point at (u, v) of the result is (within <paramref name="precision"/>) the point at (u, v)
        /// of this surface. The surface is interpolated by a cubic spline surface through a grid of points, which is refined in
        /// each direction until the points between the grid points are within <paramref name="precision"/>, or up to 256 segments.
        /// </summary>
        /// <param name="umin">the lower bound of u</param>
        /// <param name="umax">the upper bound of u</param>
        /// <param name="vmin">the lower bound of v</param>
        /// <param name="vmax">the upper bound of v</param>
        /// <param name="precision">the maximum distance of the approximation from this surface</param>
        /// <returns>the approximation</returns>
        public override NurbsSurface Approximate(double umin, double umax, double vmin, double vmax, double precision)
        {
            const int maxSegments = 256;
            int nu = 8, nv = 8;
            while (true)
            {
                NurbsSurface res = Interpolate(umin, umax, vmin, vmax, nu, nv);
                // the deviation half way between the grid points: along u, along v and in the middle of the cells
                double du = 0.0, dv = 0.0, dc = 0.0;
                for (int i = 0; i <= 2 * nu; i++)
                {
                    double u = umin + i * (umax - umin) / (2 * nu);
                    for (int j = 0; j <= 2 * nv; j++)
                    {
                        if (i % 2 == 0 && j % 2 == 0) continue; // a grid point, interpolated exactly
                        GeoPoint2D uv = new GeoPoint2D(u, vmin + j * (vmax - vmin) / (2 * nv));
                        double d = res.PointAt(uv) | PointAt(uv);
                        if (j % 2 == 0) du = Math.Max(du, d);
                        else if (i % 2 == 0) dv = Math.Max(dv, d);
                        else dc = Math.Max(dc, d);
                    }
                }
                bool refineU = du > precision || (dc > precision && dv <= precision);
                bool refineV = dv > precision || (dc > precision && du <= precision);
                if (refineU && nu < maxSegments) nu *= 2;
                else refineU = false;
                if (refineV && nv < maxSegments) nv *= 2;
                else refineV = false;
                if (!refineU && !refineV) return res;
            }
        }
        /// <summary>
        /// The cubic spline surface through the points of a grid of <paramref name="nu"/> x <paramref name="nv"/> segments, with
        /// the parameters of the grid points as its parameters (NURBS book A9.4: the rows are interpolated in u, then the poles of
        /// the rows in v).
        /// </summary>
        private NurbsSurface Interpolate(double umin, double umax, double vmin, double vmax, int nu, int nv)
        {
            double[] up = new double[nu + 1];
            for (int i = 0; i <= nu; i++) up[i] = umin + i * (umax - umin) / nu;
            double[] vp = new double[nv + 1];
            for (int j = 0; j <= nv; j++) vp[j] = vmin + j * (vmax - vmin) / nv;
            int udegree = Math.Min(3, nu), vdegree = Math.Min(3, nv);
            GeoPoint[,] rowPoles = null;
            double[] uKnots = null;
            for (int j = 0; j <= nv; j++)
            {
                GeoPoint[] row = new GeoPoint[nu + 1];
                for (int i = 0; i <= nu; i++) row[i] = PointAt(new GeoPoint2D(up[i], vp[j]));
                Nurbs<GeoPoint, GeoPointPole> rowCurve = new Nurbs<GeoPoint, GeoPointPole>(row, up, udegree);
                if (rowPoles == null) rowPoles = new GeoPoint[rowCurve.Poles.Length, nv + 1];
                for (int i = 0; i < rowCurve.Poles.Length; i++) rowPoles[i, j] = rowCurve.Poles[i];
                uKnots = rowCurve.UKnots;
            }
            GeoPoint[,] poles = null;
            double[] vKnots = null;
            for (int i = 0; i < rowPoles.GetLength(0); i++)
            {
                GeoPoint[] column = new GeoPoint[nv + 1];
                for (int j = 0; j <= nv; j++) column[j] = rowPoles[i, j];
                Nurbs<GeoPoint, GeoPointPole> columnCurve = new Nurbs<GeoPoint, GeoPointPole>(column, vp, vdegree);
                if (poles == null) poles = new GeoPoint[rowPoles.GetLength(0), columnCurve.Poles.Length];
                for (int j = 0; j < columnCurve.Poles.Length; j++) poles[i, j] = columnCurve.Poles[j];
                vKnots = columnCurve.UKnots;
            }
            return new NurbsSurface(poles, null, uKnots, vKnots, udegree, vdegree, false, false);
        }
        private static bool HasPerpendicularAxes(ModOp m)
        {
            double[,] a = m.Matrix;
            GeoVector x = new GeoVector(a[0, 0], a[1, 0], a[2, 0]);
            GeoVector y = new GeoVector(a[0, 1], a[1, 1], a[2, 1]);
            GeoVector z = new GeoVector(a[0, 2], a[1, 2], a[2, 2]);
            return Precision.IsPerpendicular(x, y, false) && Precision.IsPerpendicular(x, z, false) && Precision.IsPerpendicular(y, z, false);
        }
        /// <inheritdoc/>
        public override void CopyData(ISurface CopyFrom)
        {
            OffsetSurface cc = CopyFrom as OffsetSurface;
            if (cc != null)
            {
                this.baseSurface = cc.baseSurface.Clone();
                this.offset = cc.offset;
            }
        }
        /// <inheritdoc/>
        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            return base.GetLineIntersection(startPoint, direction);
        }
        /// <inheritdoc/>
        public override ISurface GetOffsetSurface(double offset)
        {
            return new OffsetSurface(baseSurface.Clone(), this.offset + offset);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override bool IsUPeriodic
        {
            get
            {
                return baseSurface.IsUPeriodic;
            }
        }
        /// <inheritdoc/>
        public override bool IsVPeriodic
        {
            get
            {
                return baseSurface.IsVPeriodic;
            }
        }
        /// <inheritdoc/>
        public override double UPeriod => baseSurface.UPeriod;
        /// <inheritdoc/>
        public override double VPeriod => baseSurface.VPeriod;
        /// <inheritdoc/>
        public override RuledSurfaceMode IsRuled
        {
            get
            {
                return baseSurface.IsRuled;
            }
        }
        /// <inheritdoc/>
        public override ModOp2D ReverseOrientation()
        {
            boxedSurfaceEx = null;
            offset = -offset;
            return baseSurface.ReverseOrientation();
        }
        #endregion
        /// <inheritdoc/>
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            // to implement:
            return new GroupProperty("OffsetSurface", new IPropertyEntry[0]);
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor for the legacy binary deserialization (<see cref="System.Runtime.Serialization.ISerializable"/>). Files are
        /// written in the JSON format now; the binary format is still read.
        /// </summary>
        /// <param name="info">the data of the object</param>
        /// <param name="context">the source of the deserialization</param>
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
