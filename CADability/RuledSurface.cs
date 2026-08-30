using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// Surface definition of a surface defined by two curves. Both curves use the standard parameter interval from 0.0 to 1.0
    /// The u-direction is provided by a combination of the two curves. The v parameter is defined by a line starting on the
    /// first curve and ending on the second curve. It is the surface described by a wire or rubber band synchronously moving along
    /// the two curves. the default parameter space is 0.0 to 1.0 on u and v.
    /// </summary>
    [Serializable()]
    public class RuledSurface : ISurfaceImpl, ISerializable, IExportStep
    {
        /// <summary>
        /// Dient der Beschreibung einer Zwischenkurve bei festem V
        /// </summary>
        [Serializable()]
        private class IntermediateCurve : GeneralCurve, ISerializable
        {
            private ICurve firstCurve;
            private ICurve secondCurve;
            double v; // 0.0: firstCurve, 1.0: secondCurve, sonst dazwischen
            double startParam, endParam; // fürs Trimmen, gibt auch die Richtung an, oder?
            public IntermediateCurve(ICurve firstCurve, ICurve secondCurve, double v, double startParam, double endParam)
            {
                this.firstCurve = firstCurve.Clone(); // die werden bei Modify verändert, also nicht die originale nehmen
                this.secondCurve = secondCurve.Clone();
                this.v = v;
                this.startParam = startParam;
                this.endParam = endParam;
            }
            #region implement GeneralCurve abstract methods
            /// <summary>
            /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
            /// </summary>
            /// <returns></returns>
            public override IGeoObject Clone()
            {
                return new IntermediateCurve(firstCurve, secondCurve, v, startParam, endParam);
            }
            public override GeoPoint PointAt(double Position)
            {
                double p = startParam + (endParam - startParam) * Position;
                GeoPoint p1 = firstCurve.PointAt(p);
                GeoPoint p2 = secondCurve.PointAt(p);
                return p1 + v * (p2 - p1);
            }
            public override GeoVector DirectionAt(double Position)
            {
                double p = startParam + (endParam - startParam) * Position;
                GeoVector d1 = firstCurve.DirectionAt(p);
                GeoVector d2 = secondCurve.DirectionAt(p);
                return (endParam - startParam) * ((1.0 - v) * d1 + v * d2);
            }
            protected override double[] GetBasePoints()
            {
                List<double> res = new List<double>();
                res.AddRange(firstCurve.GetSavePositions());
                res.AddRange(secondCurve.GetSavePositions());
                res.Sort();
                for (int i = res.Count - 1; i > 0; --i)
                {
                    if (res[i] - res[i - 1] < 1e-3)
                    {
                        res[i - 1] = (res[i] + res[i - 1]) / 2.0;
                        res.RemoveAt(i);
                    }
                }
                if (res.Count < 4)
                {   // nur Start- und Endpunkt sind zu wenig
                    res.Add(1.0 / 3.0);
                    res.Add(2.0 / 3.0);
                    res.Sort(); // nochmal das gleiche Spielchen
                    for (int i = res.Count - 1; i > 0; --i)
                    {
                        if (res[i] - res[i - 1] < 1e-3)
                        {
                            res[i - 1] = (res[i] + res[i - 1]) / 2.0;
                            res.RemoveAt(i);
                        }
                    }
                }
                return res.ToArray();
            }
            /// <summary>
            /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
            /// </summary>
            /// <param name="ToCopyFrom"></param>
            public override void CopyGeometry(IGeoObject ToCopyFrom)
            {
                IntermediateCurve other = ToCopyFrom as IntermediateCurve;
                this.firstCurve = other.firstCurve;
                this.secondCurve = other.secondCurve;
                this.v = other.v;
                this.startParam = other.startParam;
                this.endParam = other.endParam;
            }
            public override void Reverse()
            {
                double tmp = startParam;
                startParam = endParam;
                endParam = tmp;
            }
            public override ICurve[] Split(double Position)
            {
                double p = startParam + (endParam - startParam) * Position;
                IntermediateCurve im1 = this.Clone() as IntermediateCurve;
                IntermediateCurve im2 = this.Clone() as IntermediateCurve;
                im1.endParam = p;
                im2.startParam = p;
                return new ICurve[] { im1, im2 };
            }
            public override void Trim(double StartPos, double EndPos)
            {
                double p1 = startParam + (endParam - startParam) * StartPos;
                double p2 = startParam + (endParam - startParam) * EndPos;
                startParam = p1;
                endParam = p2;
            }
            /// <summary>
            /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
            /// </summary>
            /// <param name="m"></param>
            public override void Modify(ModOp m)
            {
                (firstCurve as IGeoObject).Modify(m);
                (secondCurve as IGeoObject).Modify(m);
                base.InvalidateSecondaryData();
            }
            #endregion
            #region ISerializable Members
            protected IntermediateCurve(SerializationInfo info, StreamingContext context)
            {
                firstCurve = info.GetValue("FirstCurve", typeof(ICurve)) as ICurve;
                secondCurve = info.GetValue("SecondCurve", typeof(ICurve)) as ICurve;
                v = info.GetDouble("V");
                startParam = info.GetDouble("StartParam");
                endParam = info.GetDouble("EndParam");
            }
            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("FirstCurve", firstCurve);
                info.AddValue("SecondCurve", secondCurve);
                info.AddValue("V", v);
                info.AddValue("StartParam", startParam);
                info.AddValue("EndParam", endParam);
            }

            #endregion
        }
        private ICurve firstCurve; // die Kurve am Anfangspunkt des "Drahtes"
        private ICurve secondCurve; // die Kurve am Ende des "Drahtes"
        private ModOp toUnit = ModOp.Null;

        public RuledSurface(ICurve firstCurve, ICurve secondCurve)
        {
            this.firstCurve = firstCurve;
            this.secondCurve = secondCurve;
#if DEBUG
#endif
        }
        public ICurve FirstCurve
        {
            get
            {
                return firstCurve;
            }
        }
        public ICurve SecondCurve
        {
            get
            {
                return secondCurve;
            }
        }
        internal NurbsSurface MakeNurbsSurface()
        {
            // zwei Splines mit gleicher Anzahl von Polen machen, die von 0 bis 1 gehen und einen maximalen Fehler haben
            int n = 2;
            Nurbs<GeoPoint, GeoPointPole> nurbs1 = null;
            Nurbs<GeoPoint, GeoPointPole> nurbs2 = null;
            while (n <= 128) // maximal 128 Stützpunkte
            {
                double[] throughpointsparam;
                GeoPoint[] through1 = new GeoPoint[n];
                GeoPoint[] through2 = new GeoPoint[n];

                for (int i = 0; i < n; ++i)
                {
                    through1[i] = firstCurve.PointAt((double)i / (double)(n - 1));
                    through2[i] = secondCurve.PointAt((double)i / (double)(n - 1));
                }
                nurbs1 = new Nurbs<GeoPoint, GeoPointPole>(3, through1, false, out throughpointsparam);
                nurbs2 = new Nurbs<GeoPoint, GeoPointPole>(3, through2, false, out throughpointsparam);
                nurbs1.NormKnots(0.0, 1.0);
                nurbs2.NormKnots(0.0, 1.0);
                double error1 = 0.0;
                double error2 = 0.0;
                for (int i = 0; i < n - 1; ++i)
                {
                    double par = (double)i / (double)(n - 1) + 1.0 / n;
                    GeoPoint p1 = firstCurve.PointAt(par);
                    GeoPoint p2 = secondCurve.PointAt(par);
                    GeoPoint n1 = nurbs1.CurvePoint(par);
                    GeoPoint n2 = nurbs2.CurvePoint(par);
                    error1 += p1 | n1;
                    error2 += p2 | n2;
                }
                error1 /= n;
                error2 /= n;
                if (error1 < Precision.eps && error2 < Precision.eps) break;
                n *= 2;
            }
            GeoPoint[,] poles = new GeoPoint[nurbs1.Poles.Length, 2];
            double[,] weights = new double[nurbs1.Poles.Length, 2];
            for (int i = 0; i < nurbs1.Poles.Length; ++i)
            {
                poles[i, 0] = nurbs1.Poles[i];
                poles[i, 1] = nurbs2.Poles[i];
                weights[i, 0] = 1.0;
                weights[i, 1] = 1.0;
            }
            return new NurbsSurface(poles, weights, nurbs1.UKnots, new double[] { 0.0, 0.0, 1.0, 1.0 }, nurbs1.UDegree, 1, false, false);

        }
        #region ISurfaceImpl Overrides
        /// <summary>
        /// Implements <see cref="ISurface.PointAt"/>.
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            GeoPoint p1 = firstCurve.PointAt(uv.x);
            GeoPoint p2 = secondCurve.PointAt(uv.x);
            return p1 + uv.y * (p2 - p1);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            GeoVector v1 = firstCurve.DirectionAt(uv.x);
            GeoVector v2 = secondCurve.DirectionAt(uv.x);
            return (1.0 - uv.y) * v1 + uv.y * v2;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            GeoPoint p1 = firstCurve.PointAt(uv.x);
            GeoPoint p2 = secondCurve.PointAt(uv.x);
            return p2 - p1; // ist ja auf die Länge bezogen, wie es sein soll
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
            GeoPoint p1 = firstCurve.PointAt(uv.x);
            GeoPoint p2 = secondCurve.PointAt(uv.x);
            location = p1 + uv.y * (p2 - p1);
            GeoVector v1 = firstCurve.DirectionAt(uv.x);
            GeoVector v2 = secondCurve.DirectionAt(uv.x);
            du = ((1.0 - uv.y) * v1 + uv.y * v2); // hier stand 0.5 * davor, was m.E. falsch war
            dv = p2 - p1;
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
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            GeoPoint p1, p2;
            GeoVector dir11, dir12, dir21, dir22;
            bool ok1 = firstCurve.TryPointDeriv2At(uv.x, out p1, out dir11, out dir12);
            bool ok2 = secondCurve.TryPointDeriv2At(uv.x, out p2, out dir21, out dir22);
            if (ok1 && ok2)
            {   // noch nicht ausführlich getestet
                // base.Derivation2At(uv, out location, out du, out dv, out duu, out dvv, out duv); sollte das gleiche Ergebnis liefern
                location = p1 + uv.y * (p2 - p1);
                du = ((1.0 - uv.y) * dir11 + uv.y * dir21);
                duu = ((1.0 - uv.y) * dir12 + uv.y * dir22);
                dv = p2 - p1;
                duv = dvv = GeoVector.NullVector;
            }
            else
            {
                base.Derivation2At(uv, out location, out du, out dv, out duu, out dvv, out duv);
            }
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
            GeoPoint p1 = PointAt(new GeoPoint2D(u, vmin));
            GeoPoint p2 = PointAt(new GeoPoint2D(u, vmax));
            Line l = Line.Construct();
            l.SetTwoPoints(p1, p2);
            return l;
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
            if (firstCurve is Line && secondCurve is Line)
            {
                Line res = Line.Construct();
                res.SetTwoPoints(PointAt(new GeoPoint2D(umin, v)), PointAt(new GeoPoint2D(umax, v)));
                return res;
            }
            else if (firstCurve is Ellipse && secondCurve is Ellipse)
            {
                Ellipse e1 = firstCurve as Ellipse;
                Ellipse e2 = secondCurve as Ellipse;
                if (Precision.SameDirection(e1.Plane.Normal, e2.Plane.Normal, false) &&
                    Precision.SameDirection(e1.Plane.DirectionX, e2.Plane.DirectionX, false) &&
                    Precision.SameDirection(e1.Plane.DirectionY, e2.Plane.DirectionY, false))
                {
                    if (e1.StartParameter == e2.StartParameter && e1.SweepParameter == e2.SweepParameter && e1.IsCircle && e2.IsCircle)
                    {   // also zwei Kreisbögen, die parallel sind und auch gleiche Start und Endwinkel haben
                        Ellipse e3 = e1.Clone() as Ellipse;
                        e3.Center = e1.Center + v * (e2.Center - e1.Center);
                        e3.Radius = e1.Radius + v * (e2.Radius - e1.Radius); // Radius verändert sich linear
                        double sw = e3.SweepParameter;
                        double sp = e3.StartParameter;
                        e3.StartParameter = sp + umin * sw;
                        e3.SweepParameter = (umax - umin) * sw;
                        return e3;
                    }
                }
            }
            // allgeimer Fall: eine vermischte Kurve
            return new IntermediateCurve(firstCurve, secondCurve, v, umin, umax);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            return new RuledSurface(firstCurve.Clone(), secondCurve.Clone()); // need to clone the curves, because the new surface might be modified
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            RuledSurface other = CopyFrom as RuledSurface;
            this.firstCurve = other.firstCurve;
            this.secondCurve = other.secondCurve;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNaturalBounds (out double, out double, out double, out double)"/>
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            umin = 0.0;
            umax = 1.0;
            vmin = 0.0;
            vmax = 1.0;
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
            if (firstCurve is Line && secondCurve is Line)
            {
                if (Precision.IsPerpendicular(firstCurve.StartDirection, pl.Normal, false) &&
                    Precision.IsPerpendicular(secondCurve.StartDirection, pl.Normal, false))
                {   // eine Ebene, die zu beiden Linien parallel ist
                    GeoPoint sp1, ep1, sp2, ep2;
                    sp1 = firstCurve.PointAt(umin);
                    ep1 = secondCurve.PointAt(umin);
                    sp2 = firstCurve.PointAt(umax);
                    ep2 = secondCurve.PointAt(umax);
                    GeoPoint2D[] ip1 = pl.GetLineIntersection(sp1, ep1 - sp1);
                    GeoPoint2D[] ip2 = pl.GetLineIntersection(sp2, ep2 - sp2);
                    if (ip1.Length == 1 && ip2.Length == 1)
                    {
                        GeoPoint sp = pl.PointAt(ip1[0]);
                        GeoPoint ep = pl.PointAt(ip2[0]);
                        double v = Geometry.LinePar(sp1, ep1, sp);
                        Line line = Line.Construct();
                        line.SetTwoPoints(sp, ep);
                        DualSurfaceCurve dsc = new DualSurfaceCurve(line, this, new Line2D(new GeoPoint2D(umin, v), new GeoPoint2D(umax, v)),
                            pl, new Line2D(ip1[0], ip2[0]));
                        return new IDualSurfaceCurve[] { dsc };
                    }
                }
            }
            if (firstCurve is Ellipse && secondCurve is Ellipse)
            {
                Ellipse e1 = firstCurve as Ellipse;
                Ellipse e2 = secondCurve as Ellipse;
                if (Precision.SameDirection(pl.Normal, e1.Plane.Normal, false) && Precision.SameDirection(pl.Normal, e2.Plane.Normal, false))
                {
                    if (e1.IsCircle && e2.IsCircle)
                    {
                        GeoPoint sp1, ep1, sp2, ep2, spm, epm;
                        sp1 = firstCurve.PointAt(umin);
                        ep1 = secondCurve.PointAt(umin);
                        sp2 = firstCurve.PointAt(umax);
                        ep2 = secondCurve.PointAt(umax);
                        spm = firstCurve.PointAt((umin + umax) / 2.0);
                        epm = secondCurve.PointAt((umin + umax) / 2.0);
                        GeoPoint2D[] ip1 = pl.GetLineIntersection(sp1, ep1 - sp1);
                        GeoPoint2D[] ip2 = pl.GetLineIntersection(sp2, ep2 - sp2);
                        GeoPoint2D[] ipm = pl.GetLineIntersection(spm, epm - spm);
                        if (ip1.Length == 1 && ip2.Length == 1 && ipm.Length == 1)
                        {
                            Ellipse e3 = Ellipse.Construct();
                            e3.SetArc3Points(pl.PointAt(ip1[0]), pl.PointAt(ipm[0]), pl.PointAt(ip2[0]), pl.Plane);
                            double v = Geometry.LinePar(sp1, ep1, pl.PointAt(ip1[0]));
                            DualSurfaceCurve dsc = new DualSurfaceCurve(e3, this, new Line2D(new GeoPoint2D(umin, v), new GeoPoint2D(umax, v)),
                                pl, e3.GetProjectedCurve(pl.Plane));
                            return new IDualSurfaceCurve[] { dsc };
                        }
                    }
                }
            }
            PlanarState ps1 = firstCurve.GetPlanarState();
            PlanarState ps2 = secondCurve.GetPlanarState();
            if ((ps1 == PlanarState.UnderDetermined || ps1 == PlanarState.Planar) && (ps2 == PlanarState.UnderDetermined || ps2 == PlanarState.Planar))
            {
                if (Precision.IsPerpendicular(firstCurve.StartDirection, pl.Normal, false) && Precision.IsPerpendicular(secondCurve.StartDirection, pl.Normal, false))
                {   // beide Kurven sind eben und parallel zur Schnittebene, wir haben also ein festes v und somit eine Zwischenkurve
                    GeoPoint ip = pl.Plane.Intersect(firstCurve.StartPoint, secondCurve.StartPoint - firstCurve.StartPoint);
                    double v = Geometry.LinePar(firstCurve.StartPoint, secondCurve.StartPoint, ip);
                    ICurve cv = FixedV(v, umin, umax);
                    DualSurfaceCurve dsc = new DualSurfaceCurve(cv, this, new Line2D(new GeoPoint2D(umin, v), new GeoPoint2D(umax, v)),
                        pl, cv.GetProjectedCurve(pl.Plane));
                    return new IDualSurfaceCurve[] { dsc };
                }
            }
            return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Make3dCurve (ICurve2D)"/>
        /// </summary>
        /// <param name="curve2d"></param>
        /// <returns></returns>
        public override ICurve Make3dCurve(ICurve2D curve2d)
        {
            if (curve2d is ProjectedCurve pc)
            {
                if (pc.Surface is RuledSurface)
                {
                    BoundingRect otherBounds = new BoundingRect(PositionOf(pc.Surface.PointAt(pc.StartPoint)), PositionOf(pc.Surface.PointAt(pc.EndPoint)));
                    if (pc.Surface.SameGeometry(pc.GetExtent(), this, otherBounds, Precision.eps, out ModOp2D notneeded))
                    {
                        return pc.Curve3DFromParams; // if trimmed or reversed still returns the correct 3d curve (but trimmed and/or reversed)
                    }
                }
            }
            if (curve2d is Line2D)
            {
                Line2D l2d = curve2d as Line2D;
                if (Math.Abs(l2d.StartPoint.y - l2d.EndPoint.y) < 1e-10)
                {   // wir sind hier im 0..1 Raum
                    return FixedV(l2d.StartPoint.y, l2d.StartPoint.x, l2d.EndPoint.x);
                }
                else if (Math.Abs(l2d.StartPoint.x - l2d.EndPoint.x) < 1e-10)
                {
                    return FixedU(l2d.StartPoint.x, l2d.StartPoint.y, l2d.EndPoint.y);
                }
            }
            return base.Make3dCurve(curve2d);
        }
        public override bool IsUPeriodic
        {
            get
            {
                return firstCurve.IsClosed && secondCurve.IsClosed;
            }
        }
        public override bool IsVPeriodic
        {
            get
            {
                return false;
            }
        }
        public override double UPeriod
        {
            get
            {
                if (IsUPeriodic) return 1.0;
                else return 0.0;
            }
        }
        public override double VPeriod
        {
            get
            {
                return 0.0;
            }
        }
        public override void GetSafeParameterSteps(double umin, double umax, double vmin, double vmax, out double[] intu, out double[] intv)
        {
            //base.GetSafeParameterSteps(umin, umax, vmin, vmax, out intu, out intv);
            //return;
            intv = new double[] { 0.0, 1.0 };
            double[] u1 = firstCurve.GetSavePositions();
            double[] u2 = secondCurve.GetSavePositions();
            SortedSet<double> usteps = new SortedSet<double>(u1);
            for (int i = 0; i < u2.Length; i++) usteps.Add(u2[i]);
            bool removed;
            do
            {   // remove usteps which are too close to each other
                removed = false;
                double lastu = -1.0;
                foreach (double u in usteps)
                {
                    if (lastu > 0)
                    {
                        if (u - lastu < 1e-5)
                        {
                            if (lastu == 0) usteps.Remove(u);
                            else usteps.Remove(lastu);
                            removed = true;
                            break;
                        }
                    }
                    lastu = u;
                }
            } while (removed);
            intu = new double[usteps.Count];
            if (usteps.Count > 2)
            {

            }
            usteps.CopyTo(intu);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            RuledSurface res = this.Clone() as RuledSurface;
            res.Modify(m);
            res.usedArea = usedArea;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            firstCurve = firstCurve.CloneModified(m);
            secondCurve = secondCurve.CloneModified(m);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetExtrema ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoPoint2D[] GetExtrema()
        {   // in einer Richtung ist es immer liniear, es gibt also keine Beulen
            return new GeoPoint2D[0];
        }
        public override ModOp2D ReverseOrientation()
        {
            boxedSurfaceEx = null;
            firstCurve.Reverse();
            secondCurve.Reverse();
            return new ModOp2D(-1.0, 0.0, 1.0, 0.0, 1.0, 0.0); // x wird invertiert, x ist der Parameter auf den Kurven
        }
#if DEBUG
        public static int hitcount = 0;
#endif
        public override void Intersect(ICurve curve, BoundingRect uvExtent, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {
            if (curve is IExplicitPCurve3D && firstCurve is Line && secondCurve is Line)
            {
                if (Precision.SameDirection(firstCurve.StartDirection, secondCurve.StartDirection, false))
                { // a simple plane
                    PlaneSurface pls = new PlaneSurface(firstCurve.StartPoint, firstCurve.EndPoint, secondCurve.StartPoint); // has the same uv system
                    pls.Intersect(curve, uvExtent, out ips, out uvOnFaces, out uOnCurve3Ds);
                    return;
                }
                else
                {
                    if (toUnit.IsNull)
                    {
                        lock (this)
                        {
                            if (toUnit.IsNull)
                            {
                                toUnit = ModOp.Fit(new GeoPoint[] { firstCurve.StartPoint, firstCurve.EndPoint, secondCurve.StartPoint, secondCurve.EndPoint },
                                new GeoPoint[] { new GeoPoint(0, 0, 0), new GeoPoint(1, 0, 0), new GeoPoint(0, 1, 0), new GeoPoint(1, 1, 1) }, true);
                            }
                        }
                    }
                    ModOp fromUnit = toUnit.GetInverse();
                    ICurve unitCurve = curve.CloneModified(toUnit);
                    ExplicitPCurve3D explicitCurve = (unitCurve as IExplicitPCurve3D).GetExplicitPCurve3D();
                    GeoPoint[] hypLineIsp = implicitUnitHyperbolic.Intersect(explicitCurve, out double[] uc);
                    List<GeoPoint2D> luv = new List<GeoPoint2D>();
                    List<double> lu = new List<double>();
                    List<GeoPoint> lips = new List<GeoPoint>();

                    for (int i = 0; i < hypLineIsp.Length; i++)
                    {
                        double u = unitCurve.PositionOf(hypLineIsp[i]); // explicitCurve doesn't necessary have the same u system as curve
                        if (u >= -1e-6 && u <= 1 + 1e-6)
                        {
                            GeoPoint2D uv = new GeoPoint2D(hypLineIsp[i].x, hypLineIsp[i].y);
                            if (BoundingRect.UnitBoundingRect.ContainsEps(uv, -0.001))
                            {
                                lu.Add(u);
                                luv.Add(uv);
                                lips.Add(fromUnit * hypLineIsp[i]);
                            }
                        }
                    }

                    uvOnFaces = luv.ToArray();
                    uOnCurve3Ds = lu.ToArray();
                    ips = lips.ToArray();
#if DEBUG
                    ++hitcount;
#endif
                    return;
                }
            }
            base.Intersect(curve, uvExtent, out ips, out uvOnFaces, out uOnCurve3Ds);
        }

        // BoxedSurfaceEx is alot faster with the HitTest. Don't override it
        //public override bool HitTest(BoundingBox cube, out GeoPoint2D uv)
        //{
        //    foreach (GeoPoint2D pnt in new GeoPoint2D[] { GeoPoint2D.Origin, new GeoPoint2D(0.0, 1.0), new GeoPoint2D(1.0, 0.0), new GeoPoint2D(1.0, 1.0) })
        //    {
        //        if (cube.Contains(PointAt(pnt)))
        //        {
        //            uv = pnt;
        //            return true;
        //        }
        //    }
        //    {
        //        GeoPoint sp = firstCurve.StartPoint;
        //        GeoPoint ep = secondCurve.StartPoint;
        //        if (cube.ClipLine(ref sp, ref ep))
        //        {

        //            double v = Geometry.LinePar(firstCurve.StartPoint, secondCurve.StartPoint, new GeoPoint(sp, ep));
        //            uv = new GeoPoint2D(0.0, v);
        //            return true;
        //        }
        //        sp = firstCurve.EndPoint;
        //        ep = secondCurve.EndPoint;
        //        if (cube.ClipLine(ref sp, ref ep))
        //        {

        //            double v = Geometry.LinePar(firstCurve.EndPoint, secondCurve.EndPoint, new GeoPoint(sp, ep));
        //            uv = new GeoPoint2D(1.0, v);
        //            return true;
        //        }
        //    }
        //    if (firstCurve is Line && secondCurve is Line)
        //    {
        //        if (cube.OnSameSide(firstCurve.StartPoint, firstCurve.EndPoint, secondCurve.StartPoint, secondCurve.EndPoint))
        //        {
        //            uv = GeoPoint2D.Invalid; // must be set
        //            return false; // all points on the same side of the cube
        //        }
        //    }

        //    if (firstCurve.HitTest(cube))
        //    {
        //        if (firstCurve is Line)
        //        {
        //            GeoPoint sp = firstCurve.StartPoint;
        //            GeoPoint ep = firstCurve.EndPoint;
        //            if (cube.Interferes(ref sp, ref ep))
        //            {
        //                double u = firstCurve.PositionOf(new GeoPoint(sp, ep));
        //                uv = new GeoPoint2D(u, 0.0);
        //                return true;
        //            }
        //        }
        //        else
        //        {
        //            // we need a inner uv point, which already has been calculated by the HitTest but needs to be recalculated here
        //            PlaneSurface[] planes = cube.GetPlanes();
        //            BoundingRect uvExtend = new BoundingRect(0, 0, 1, 1);
        //            List<double> crvisp = new List<double>();
        //            for (int i = 0; i < planes.Length; i++)
        //            {
        //                planes[i].Intersect(firstCurve, uvExtend, out GeoPoint[] ips, out GeoPoint2D[] uvOnSurface, out double[] uOnCurve);
        //                for (int j = 0; j < ips.Length; j++)
        //                {
        //                    if (uvExtend.Contains(uvOnSurface[j]) && uOnCurve[j] >= 0.0 && uOnCurve[j] <= 1.0)
        //                    {
        //                        crvisp.Add(uOnCurve[j]);
        //                    }
        //                }
        //            }
        //            if (crvisp.Count > 1)
        //            {   // since the start- and enpoint is not inside the cube (because of the test above) the first segment must be inside
        //                double u = firstCurve.PositionOf(new GeoPoint(crvisp[0], crvisp[1]));
        //                uv = new GeoPoint2D(u, 0.0);
        //                return true;
        //            }
        //        }
        //    }
        //    if (secondCurve.HitTest(cube))
        //    {
        //        if (secondCurve is Line)
        //        {
        //            GeoPoint sp = secondCurve.StartPoint;
        //            GeoPoint ep = secondCurve.EndPoint;
        //            if (cube.Interferes(ref sp, ref ep))
        //            {
        //                double u = secondCurve.PositionOf(new GeoPoint(sp, ep));
        //                uv = new GeoPoint2D(u, 1.0);
        //                return true;
        //            }
        //        }
        //        else
        //        {
        //            // we need a inner uv point, which already has been calculated by the HitTest but needs to be recalculated here
        //            PlaneSurface[] planes = cube.GetPlanes();
        //            BoundingRect uvExtend = new BoundingRect(0, 0, 1, 1);
        //            List<double> crvisp = new List<double>();
        //            for (int i = 0; i < planes.Length; i++)
        //            {
        //                planes[i].Intersect(secondCurve, uvExtend, out GeoPoint[] ips, out GeoPoint2D[] uvOnSurface, out double[] uOnCurve);
        //                for (int j = 0; j < ips.Length; j++)
        //                {
        //                    if (uvExtend.Contains(uvOnSurface[j]) && uOnCurve[j] >= 0.0 && uOnCurve[j] <= 1.0)
        //                    {
        //                        crvisp.Add(uOnCurve[j]);
        //                    }
        //                }
        //            }
        //            if (crvisp.Count > 1)
        //            {   // since the start- and enpoint is not inside the cube (because of the test above) the first segment must be inside
        //                double u = secondCurve.PositionOf(new GeoPoint(crvisp[0], crvisp[1]));
        //                uv = new GeoPoint2D(u, 1.0);
        //                return true;
        //            }
        //        }
        //    }
        //    {
        //        GeoPoint sp = PointAt(new GeoPoint2D(0.0, 0.0));  // "left" edge
        //        GeoPoint ep = PointAt(new GeoPoint2D(0.0, 1.0));
        //        GeoPoint sp0 = sp;
        //        GeoPoint ep0 = ep;
        //        if (cube.Interferes(ref sp, ref ep))
        //        {
        //            double v = Geometry.LinePar(sp0, ep0, new GeoPoint(sp, ep));
        //            uv = new GeoPoint2D(0.0, v);
        //            return true;
        //        }
        //        sp = PointAt(new GeoPoint2D(1.0, 0.0));  // "right" edge
        //        ep = PointAt(new GeoPoint2D(1.0, 1.0));
        //        sp0 = sp;
        //        ep0 = ep;
        //        if (cube.Interferes(ref sp, ref ep))
        //        {
        //            double v = Geometry.LinePar(sp0, ep0, new GeoPoint(sp, ep));
        //            uv = new GeoPoint2D(1.0, v);
        //            return true;
        //        }
        //        // the four enclosing curves do not interfere with the cube and none of the four vertices is inside the cube. 
        //        // Test whether one of the cubes edges intersects this surface
        //        GeoPoint[,] lines = cube.Lines;
        //        for (int i = 0; i < 12; i++)
        //        {   // if this is a plane or a hyperbolic paraboloid then GetLineIntersection is directly implemented (no use of BoxedSurface)
        //            GeoPoint2D[] lips = GetLineIntersection(lines[i, 0], lines[i, 1] - lines[i, 0]);
        //            if (lips != null && lips.Length > 0)
        //            {
        //                uv = lips[0];
        //                return true;
        //            }
        //        }
        //        uv = GeoPoint2D.Origin; // must be set
        //    }
        //    return false;
        //}
        private static ImplicitPSurface implicitUnitHyperbolic = new ImplicitPSurface(new Polynom(1, "xy", -1, "z"));

        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            if (firstCurve is Line && secondCurve is Line)
            {
                if (Precision.SameDirection(firstCurve.StartDirection, secondCurve.StartDirection, false))
                { // a simple plane
                    PlaneSurface pls = new PlaneSurface(firstCurve.StartPoint, firstCurve.EndPoint, secondCurve.StartPoint);
                    return pls.GetLineIntersection(startPoint, direction);
                }
                else
                {
                    // a standard hyperbolic paraboloid with the form z = x*y as the affine transformation of this paraboloid
                    if (toUnit.IsNull)
                    {
                        lock (this)
                        {
                            if (toUnit.IsNull)
                            {
                                toUnit = ModOp.Fit(new GeoPoint[] { firstCurve.StartPoint, firstCurve.EndPoint, secondCurve.StartPoint, secondCurve.EndPoint },
                                new GeoPoint[] { new GeoPoint(0, 0, 0), new GeoPoint(1, 0, 0), new GeoPoint(0, 1, 0), new GeoPoint(1, 1, 1) }, true);
                            }
                        }
                    }
                    // ModOp fromUnit = toUnit.GetInverse();
                    // Polynom hyperbolic = new Polynom(1, "xy", -1, "z");
                    //Polynom hyperbolic = new Polynom(2, 3);
                    //hyperbolic.Set(1.0, new int[] { 1, 1, 0 });
                    //hyperbolic.Set(-1.0, new int[] { 0, 0, 1 }); // this is x*y-z==0
                    //ImplicitPSurface implicitHyperbolic = new ImplicitPSurface(hyperbolic);
                    ExplicitPCurve3D explicitCurve = ExplicitPCurve3D.MakeLine(toUnit * startPoint, toUnit * direction);
                    GeoPoint[] hypLineIsp = implicitUnitHyperbolic.Intersect(explicitCurve, out double[] uc);

                    List<GeoPoint2D> luv = new List<GeoPoint2D>();
                    for (int i = 0; i < hypLineIsp.Length; i++)
                    {
                        double u = uc[i]; //= explicitCurve.PositionOf(hypLineIsp[i], out double dist);
                        if (u >= -1e-6 && u <= 1 + 1e-6)
                        {
                            GeoPoint2D uv = new GeoPoint2D(hypLineIsp[i].x, hypLineIsp[i].y);
                            if (BoundingRect.UnitBoundingRect.ContainsEps(uv, -0.001))
                            {
                                luv.Add(uv);
                            }
                        }
                    }
#if DEBUG
                    ++hitcount;
                    if (luv.Count == 0)
                    { }
#endif

                    return luv.ToArray();
                }
            }
            return base.GetLineIntersection(startPoint, direction);
        }
        public override bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
        {
            if (other is RuledSurface)
            {
                RuledSurface rsother = other as RuledSurface;
                if ((firstCurve.SameGeometry(rsother.firstCurve, precision) && secondCurve.SameGeometry(rsother.secondCurve, precision)) ||
                    (firstCurve.SameGeometry(rsother.secondCurve, precision) && secondCurve.SameGeometry(rsother.firstCurve, precision)))
                {
                    GeoPoint2D[] srcPoints = new GeoPoint2D[] { GeoPoint2D.Origin, new GeoPoint2D(1, 0), new GeoPoint2D(0, 1) };
                    GeoPoint2D[] dstPoints = new GeoPoint2D[3];
                    for (int i = 0; i < dstPoints.Length; i++)
                    {
                        dstPoints[i] = rsother.PositionOf(this.PointAt(srcPoints[i]));
                    }
                    firstToSecond = ModOp2D.Fit(srcPoints, dstPoints, true);
                    return true;
                }
                firstToSecond = ModOp2D.Null;
                return false;
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            List<IPropertyEntry> se = new List<IPropertyEntry>();
                if (firstCurve is IGeoObject first)
                {
                    se.Add(first.GetShowProperties(frame) as IPropertyEntry);
                }
                if (secondCurve is IGeoObject second)
                {
                    se.Add(second.GetShowProperties(frame) as IPropertyEntry);
                }
            return new GroupProperty("RuledSurface", se.ToArray());
        }
        #endregion
        #region ISerializable Members
        protected RuledSurface(SerializationInfo info, StreamingContext context)
        {
            firstCurve = info.GetValue("FirstCurve", typeof(ICurve)) as ICurve;
            secondCurve = info.GetValue("SecondCurve", typeof(ICurve)) as ICurve;
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("FirstCurve", firstCurve);
            info.AddValue("SecondCurve", secondCurve);
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            NurbsSurface ns = MakeNurbsSurface();
            return (ns as IExportStep).Export(export, topLevel);
        }
        #endregion
#if DEBUG
        public int GetNumParEpis()
        {
            return BoxedSurfaceEx.DebugCount;
        }

        public int Export(ExportStep export, bool topLevel)
        {
            return (MakeNurbsSurface() as IExportStep).Export(export, topLevel);
        }
#endif
    }
}
