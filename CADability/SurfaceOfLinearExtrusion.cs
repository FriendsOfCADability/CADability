using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// Surface defined by a 3d curve and a direction. A point at (u,v) of the surface is defined
    /// by the point of the curve at parameter u plus v*direction. It is the surface that is generated
    /// by the curve moved along the direction. The curve may not have the direction as a tangential
    /// vector. Also the curve is defined in the interval [0,1], the parameterspace of this curve may also
    /// be defined by a startParameter and an endParameter. 
    /// </summary>
    [Serializable()]
    public class SurfaceOfLinearExtrusion : ISurfaceImpl, ISerializable, IExportStep
    {
        private ICurve basisCurve;
        private GeoVector direction;
        private double curveStartParameter, curveEndParameter;
        public SurfaceOfLinearExtrusion(ICurve basisCurve, GeoVector direction, double curveStartParameter, double curveEndParameter)
        {
            this.basisCurve = basisCurve;
            this.direction = direction;
            this.curveEndParameter = curveEndParameter;
            this.curveStartParameter = curveStartParameter;
            // curveStartParameter und curveEndParameter haben folgenden Sinn: 
            // der Aufrufer werwartet ein bestimmtes u/v System. In v ist es einfach: v*direction,
            // in u wird die Kurve ja von 0 bis 1 parametriert, der Aufrufer erwartet jedoch von 
            // curveStartParameter bis curveEndParameter. Deshalb brauchen wir die beiden Werte
            // der Surface ist die Ausdehnung kein Thema, sie ist immer prinzipiell unendlich
            // Leztlich ist es nur eine Forderung von OpenCascade, bei CADability könnten start- und
            // endparameter immer 0.0 und 1.0 sein.
        }
        public ICurve BasisCurve
        {
            get
            {
                return basisCurve;
            }
        }
        public GeoVector Direction
        {
            get
            {
                return direction;
            }
        }
        public void Simplify()
        {
            if (basisCurve is BSpline bsp)
            {
                if (bsp.HasWeights)
                {
                    bool almostNoWeights = true;
                    for (int i = 0; i < bsp.Weights.Length; i++)
                    {
                        if (Math.Abs(1.0 - bsp.Weights[i]) > 1e-3)
                        {
                            almostNoWeights = false;
                            break;
                        }
                    }
                    // there are some very special cases, where the weights are 0.99999999 instead of 1 and make calculation unnecessary difficult
                    if (almostNoWeights) bsp.SetData(bsp.degree, bsp.Poles, null, bsp.Knots, bsp.Multiplicities, bsp.IsClosed);
                }
            }
            if (basisCurve.GetPlanarState() != PlanarState.Planar)
            {   // this changes the 2d system, the caller must be aware of this fact
                Plane pl = new Plane(basisCurve.StartPoint, direction);
                ICurve2D projected = basisCurve.GetProjectedCurve(pl);
                basisCurve = projected.MakeGeoObject(pl) as ICurve;
            }
        }
        public override ISurface GetOffsetSurface(double offset)
        {
            if (basisCurve.GetPlanarState() != PlanarState.NonPlanar && !(basisCurve is BSpline))
            {   // the offset curves of BSplines are more expensive than the OffsetSurface,
                // this is why we exclude BSplines here
                Plane pln = Plane.XYPlane;
                if (basisCurve.GetPlanarState() == PlanarState.Planar)
                {
                    pln = basisCurve.GetPlane();
                }
                else if (basisCurve.GetPlanarState() == PlanarState.UnderDetermined)
                {
                    pln = new Plane(basisCurve.StartPoint, basisCurve.StartDirection, basisCurve.StartDirection ^ direction);
                }
                if (Precision.SameDirection(direction, pln.Normal, false))
                {   // this aproach is only valid, when the extrusion direction is normal to the plane
                    if (direction * pln.Normal < 0) pln.Reverse();
                    ICurve2D c2d = basisCurve.GetProjectedCurve(pln);
                    try
                    {
                        ICurve2D c2dp = c2d.Parallel(offset, false, Precision.eps, Math.PI);
                        if (c2dp != null) return new SurfaceOfLinearExtrusion(c2dp.MakeGeoObject(pln) as ICurve, direction, curveStartParameter, curveEndParameter);
                    }
                    catch (Exception ex) { } // some curves don't implement Parallel (e.g. EllipseArc2D)
                }
            }
            return new OffsetSurface(this, offset);
        }
        #region ISurfaceImpl overrides
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            SurfaceOfLinearExtrusion res = new SurfaceOfLinearExtrusion(basisCurve.Clone(), direction, curveStartParameter, curveEndParameter);
            res.usedArea = usedArea;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            if (IsUPeriodic)
            {
                while (uv.x > curveEndParameter) uv.x -= UPeriod;
                while (uv.x < curveStartParameter) uv.x += UPeriod;
            }
            double pos = (uv.x - curveStartParameter) / (curveEndParameter - curveStartParameter);
            GeoPoint res = basisCurve.PointAt(pos);
            return res + (uv.y * direction);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            if (basisCurve.GetPlanarState() == PlanarState.Planar)
            {
                Plane cp = basisCurve.GetPlane();
                // ICurve2D c2d = basisCurve.GetProjectedCurve(cp); wird nicht benötigt
                if (Precision.IsPerpendicular(cp.Normal, direction, false))
                {

                }
                else
                {
                    GeoPoint pp = cp.Intersect(p, direction); // direction darf natürlich nicht in der Ebene der Kurve liegen
                    double u = basisCurve.PositionOf(pp);
                    double v = Geometry.LinePar(basisCurve.PointAt(u), direction, p);
#if DEBUG
                    //GeoPoint2D dbg1 = base.PositionOf(p);
                    //GeoPoint2D dbg2 = new GeoPoint2D(curveStartParameter + u * (curveEndParameter - curveStartParameter), v);
                    //if ((dbg1 | dbg2) > 1e-6)
                    //{
                    //    throw new ApplicationException("error in SurfaceOfLinearExtrusion.PositionOf");
                    //}
#endif
                    return new GeoPoint2D(curveStartParameter + u * (curveEndParameter - curveStartParameter), v);
                }
            }
            Plane pl = new Plane(basisCurve.StartPoint, direction);
            ICurve2D projected = basisCurve.GetProjectedCurve(pl);
            double uu = projected.PositionOf(pl.Project(p)); // assuming the projected curve and the basisCurve have the same parameter space (which is true for NURBS)
            GeoPoint start = basisCurve.PointAt(uu);
            double vv = Geometry.LinePar(start, direction, p);
            return new GeoPoint2D(uu, vv);
            // GeoPoint2D res = base.PositionOf(p);
            // return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            double pos = (uv.x - curveStartParameter) / (curveEndParameter - curveStartParameter);
            // ausgehend davon, dass DirectionAt den Vektor gemäß änderung um 1 liefert
            // das ist oft nicht der Fall. Man muss also PointAtParam und DirectionAtParam implementieren
            return (curveEndParameter - curveStartParameter) * basisCurve.DirectionAt(pos);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            return direction;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            return UDirection(uv) ^ VDirection(uv);
        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            if (IsUPeriodic)
            {
                while (uv.x > curveEndParameter) uv.x -= UPeriod;
                while (uv.x < curveStartParameter) uv.x += UPeriod;
            }
            double pos = (uv.x - curveStartParameter) / (curveEndParameter - curveStartParameter);
            basisCurve.TryPointDeriv2At(pos, out GeoPoint upoint, out du, out duu);
            location = upoint + (uv.y * direction);
            dv = direction;
            du = (curveEndParameter - curveStartParameter) * du;
            duu = (curveEndParameter - curveStartParameter) * duu;
            dvv = GeoVector.NullVector;
            duv = du;
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
            GeoVector offset1 = vmin * direction;
            GeoVector offset2 = vmax * direction;
            for (int i = 0; i < 10; ++i) // hier mal grob über 10 Punkte der Kurve, das muss noch optimiert werden
            {
                GeoPoint bp = p.UnscaledProjection * (basisCurve.PointAt(i / 10.0) + offset1);
                zMin = Math.Min(zMin, bp.z);
                zMax = Math.Max(zMax, bp.z);
                bp = p.UnscaledProjection * (basisCurve.PointAt(i / 10.0) + offset2);
                zMin = Math.Min(zMin, bp.z);
                zMax = Math.Max(zMax, bp.z);
            }
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
            if (curve2d is ProjectedCurve pc)
            {
                if (pc.Surface is SurfaceOfLinearExtrusion)
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
                GeoVector2D dir = l2d.EndPoint - l2d.StartPoint;
                if (Math.Abs(dir.x) < Precision.eps)
                {   // das Ergebnis ist eine Mantel-Linie
                    Line res = Line.Construct();
                    res.StartPoint = PointAt(l2d.StartPoint);
                    res.EndPoint = PointAt(l2d.EndPoint);
                    return res;
                }
                else if (Math.Abs(dir.y) < Precision.eps)
                {
                    // Basiskurve bzw. Abschnitt derselben
                    ModOp move = ModOp.Translate(l2d.StartPoint.y * direction);
                    ICurve res = basisCurve.CloneModified(move);
                    double sp = (l2d.StartPoint.x - curveStartParameter) / (curveEndParameter - curveStartParameter);
                    double ep = (l2d.EndPoint.x - curveStartParameter) / (curveEndParameter - curveStartParameter);
                    //double sp = l2d.StartPoint.x;
                    //double ep = l2d.EndPoint.x;
                    if (!(basisCurve is BSpline))
                    {   // hier geht auch Verlängern
                        if (sp > ep)
                        {
                            res.Reverse();
                            if (ep != 0.0 || sp != 1.0) res.Trim(1.0 - sp, 1.0 - ep);
                        }
                        else
                        {
                            if (sp != 0.0 || ep != 1.0) res.Trim(sp, ep);
                        }
                    }
                    else
                    {
                        if (sp > 1.0 && ep > 1.0) return null;
                        if (sp < 0.0 && ep < 0.0) return null;
                        if (sp > ep)
                        {
                            res.Reverse();
                            if (ep != 0.0 || sp != 1.0) res.Trim(1.0 - sp, 1.0 - ep);
                        }
                        else
                        {
                            if (sp != 0.0 || ep != 1.0) res.Trim(sp, ep);
                        }
                    }
                    return res;
                }
            }
            if (curve2d is BSpline2D)
            {
                BSpline2D b2d = (curve2d as BSpline2D);
                int numpts = b2d.Poles.Length * b2d.Degree;
                GeoPoint[] pts = new GeoPoint[numpts + 1];
                for (int i = 0; i <= numpts; ++i)
                {
                    pts[i] = PointAt(b2d.PointAt((double)i / (double)numpts));
                }
                BSpline b3d = BSpline.Construct();
                b3d.ThroughPoints(pts, b2d.Degree, false);
                return b3d;
            }
            return base.Make3dCurve(curve2d);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            SurfaceOfLinearExtrusion cc = CopyFrom as SurfaceOfLinearExtrusion;
            if (cc != null)
            {
                this.basisCurve = cc.basisCurve.Clone();
                this.direction = cc.direction;
                this.curveStartParameter = cc.curveStartParameter;
                this.curveEndParameter = cc.curveEndParameter;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            boxedSurfaceEx = null;
            basisCurve = basisCurve.CloneModified(m);
            direction = m * direction;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            ISurface res = Clone();
            res.Modify(m);
            (res as ISurfaceImpl).usedArea = usedArea;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.HitTest (BoundingCube, out GeoPoint2D)"/>
        /// </summary>
        /// <param name="bc"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override bool HitTest(BoundingCube bc, out GeoPoint2D uv)
        {
            Plane p = new Plane(GeoPoint.Origin, direction);
            PlaneSurface ps = new PlaneSurface(p);
            ICurve2D c = basisCurve.GetProjectedCurve(p);
            GeoPoint[] points = bc.Points;
            GeoPoint2D[] points2D = new GeoPoint2D[8];
            for (int i = 0; i < 8; ++i)
            {
                points2D[i] = ps.PositionOf(points[i]);
            }
            // does c hit the polygon?
            int[,] l = bc.LineNumbers;
            for (int k = 0; k < 12; ++k)
            {
                int i = l[k, 0];
                int j = l[k, 1];
                ICurve2D c2 = new Line2D(points2D[i], points2D[j]);
                if (c2.Length > 0)
                {
                    GeoPoint2DWithParameter[] list = c.Intersect(c2);
                    for (int m = 0; m < list.Length; ++m)
                    {
                        GeoPoint2D d0 = list[m].p;
                        double d1 = (points2D[i] - d0).Length;
                        double d2 = (points2D[j] - d0).Length;
                        double d3 = (points2D[i] - points2D[j]).Length;
                        if (Math.Abs(d1 + d2 - d3) < Precision.eps)
                        {
                            if (d3 < Precision.eps)
                                throw new Exception();
                            GeoPoint gp = points[i] + (d1 / d3) * (points[j] - points[i]);
                            uv = PositionOf(gp);
                            return true;
                        }
                    }
                }
            }
            // is c in the polygon?
            GeoPoint e = ps.PointAt(c.EndPoint);
            bool res = (bc.Interferes(e, direction, bc.Size * 1e-8, false));
            if (res)
            {   // nur berechnen, wenn auch gültig
                uv = PositionOf(e);
            }
            else
            {
                uv = GeoPoint2D.Origin;
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.ReverseOrientation ()"/>
        /// </summary>
        /// <returns></returns>
        public override ModOp2D ReverseOrientation()
        {   // wir drehen einfach die Y-Richtung um
            boxedSurfaceEx = null;
            direction = -direction;
            return new ModOp2D(1.0, 0.0, 0.0, 0.0, -1.0, 0.0);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetLineIntersection (GeoPoint, GeoVector)"/>
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            if (Precision.SameDirection(this.direction, direction, false))
            {   // Linie parallel zur Auszugsrichtung
                return new GeoPoint2D[] { };
            }
            else
            {
                // Die Ebene durch Auszugsrichtung und Schnittgerade und Schnittlinien-Startpunkt, schneide
                // die Basiskurve mit der Ebene. Die Schnittpunkt liefern die richtigen u-Werte
                // die v-Werte finden wir vom u-Wert auf der basisCurve ausgehen in Auszugsrichtung und mit der Schnittlinie schneiden
                Plane pln = new Plane(startPoint, direction, this.direction);
                double[] u = basisCurve.GetPlaneIntersection(pln);
                GeoPoint2D[] res = new GeoPoint2D[u.Length];
                pln = new Plane(startPoint, direction, direction ^ this.direction); // Ebene senkrech zur Auszugsrichtung durch den Linienpunkt
                for (int i = 0; i < u.Length; ++i)
                {
                    GeoPoint ip = pln.Intersect(basisCurve.PointAt(u[i]), this.direction);
                    GeoPoint vbasis = basisCurve.PointAt(u[i]);
                    //res[i] = new GeoPoint2D((u[i] - curveStartParameter) / (curveEndParameter - curveStartParameter), Geometry.LinePar(startPoint, this.direction, ip));
                    res[i] = new GeoPoint2D((u[i] * (curveEndParameter - curveStartParameter) + curveStartParameter), Geometry.LinePar(vbasis, this.direction, ip));
                }
                return res;
            }
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
            if (Precision.IsPerpendicular(pl.Plane.Normal, direction, false))
            {   // Ebene, die parallel zur Auszugsrichtung liegt. Es entstehen Linien
                List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                double[] cpar = basisCurve.GetPlaneIntersection(pl.Plane);
                for (int i = 0; i < cpar.Length; i++)
                {
                    // von 0..1 auf startParameter..endParameter umrechnen
                    double pos = curveStartParameter + cpar[i] * (curveEndParameter - curveStartParameter);
                    if (pos >= umin && pos <= umax)
                    {
                        ICurve c3d = FixedU(pos, vmin, vmax);
                        Line2D l2d = new Line2D(new GeoPoint2D(pos, vmin), new GeoPoint2D(pos, vmax));
                        DualSurfaceCurve dsc = new DualSurfaceCurve(c3d, this, l2d, pl, c3d.GetProjectedCurve(pl.Plane));
                        res.Add(dsc);
                    }
                }
                return res.ToArray();
                //return new IDualSurfaceCurve[] { };
                //return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision); // stürzt ab mit 50050020_012_1_FMZ.stp
            }
            else if (Precision.SameDirection(direction, pl.Plane.Normal, false))
            {
                if (basisCurve.GetPlanarState() == PlanarState.Planar && Precision.SameDirection(direction, basisCurve.GetPlane().Normal, false))
                {
                    Plane cp = basisCurve.GetPlane();
                    if (!Precision.SameNotOppositeDirection(cp.Normal, direction))
                    {
                        cp = new Plane(cp.Location, cp.DirectionY, cp.DirectionX);
                    }
                    double v = cp.Distance(pl.Plane.Location) / direction.Length;
                    Line2D l2d = new Line2D(new GeoPoint2D(curveStartParameter, v), new GeoPoint2D(curveEndParameter, v));
                    DualSurfaceCurve dsc = new DualSurfaceCurve(basisCurve.CloneModified(ModOp.Translate(v * direction)), this, l2d, pl, basisCurve.GetProjectedCurve(pl.Plane));
                    return new IDualSurfaceCurve[] { dsc };
                }
            }
            // keine Lösung gefunden
            double[] upos = basisCurve.GetSavePositions();
            InterpolatedDualSurfaceCurve.SurfacePoint[] sp = new InterpolatedDualSurfaceCurve.SurfacePoint[upos.Length];
            for (int i = 0; i < upos.Length; ++i)
            {
                GeoPoint p0 = basisCurve.PointAt(upos[i]);
                double pos = curveStartParameter + upos[i] * (curveEndParameter - curveStartParameter);
                GeoPoint2D[] ips = pl.GetLineIntersection(p0, direction);
                if (ips.Length == 1)
                {
                    GeoPoint p3d = pl.PointAt(ips[0]);
                    double v = Geometry.LinePar(p0, direction, p3d);
                    sp[i] = new InterpolatedDualSurfaceCurve.SurfacePoint(p3d, new GeoPoint2D(pos, v), ips[0]);
                }
            }
            InterpolatedDualSurfaceCurve idsc = new InterpolatedDualSurfaceCurve(this, pl, sp, true);
            return new IDualSurfaceCurve[] { idsc.ToDualSurfaceCurve() };
            //return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
        }

        public override IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> extremePositions)
        {
            if (other is PlaneSurface ps)
            {
                IDualSurfaceCurve[] res = GetPlaneIntersection(ps, thisBounds.Left, thisBounds.Right, thisBounds.Bottom, thisBounds.Top, Precision.eps);
                // don't swao, order is correct: for (int i = 0; i < res.Length; i++) res[i].SwapSurfaces();
                return res;
                
            }
            return base.GetDualSurfaceCurves(thisBounds, other, otherBounds, seeds, extremePositions);
        }
        public override bool IsUPeriodic
        {
            get
            {
                return basisCurve.IsClosed || Precision.IsEqual(basisCurve.StartPoint, basisCurve.EndPoint);
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
                if (basisCurve.IsClosed || Precision.IsEqual(basisCurve.StartPoint, basisCurve.EndPoint)) return curveEndParameter - curveStartParameter;
                return 0.0;
            }
        }
        public override double VPeriod
        {
            get
            {
                return 0.0;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetSafeParameterSteps (double, double, double, double, out double[], out double[])"/>
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="intu"></param>
        /// <param name="intv"></param>
        public override void GetSafeParameterSteps(double umin, double umax, double vmin, double vmax, out double[] intu, out double[] intv)
        {
            double[] sp = basisCurve.GetSavePositions();
            List<double> lintu = new List<double>();
            if (umin >= sp[0] * (curveEndParameter - curveStartParameter) + curveStartParameter) lintu.Add(umin);
            for (int i = 0; i < sp.Length; ++i)
            {
                double u = sp[i] * (curveEndParameter - curveStartParameter) + curveStartParameter;
                if (u > umin && u < umax) lintu.Add(u);
            }
            if (umax <= sp[sp.Length - 1] * (curveEndParameter - curveStartParameter) + curveStartParameter) lintu.Add(umax);
            intu = lintu.ToArray();
            intv = new double[] { vmin, vmax };
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
            Line res = Line.Construct();
            res.SetTwoPoints(PointAt(new GeoPoint2D(u, vmin)), PointAt(new GeoPoint2D(u, vmax)));
            return res;
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
            double pos1 = (umin - curveStartParameter) / (curveEndParameter - curveStartParameter);
            double pos2 = (umax - curveStartParameter) / (curveEndParameter - curveStartParameter);
            ICurve res = basisCurve.CloneModified(ModOp.Translate(v * direction));
            res.Trim(pos1, pos2);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.SameGeometry (BoundingRect, ISurface, BoundingRect, double, out ModOp2D)"/>
        /// </summary>
        /// <param name="thisBounds"></param>
        /// <param name="other"></param>
        /// <param name="otherBounds"></param>
        /// <param name="precision"></param>
        /// <param name="firstToSecond"></param>
        /// <returns></returns>
        public override bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
        {
            if (other is SurfaceOfLinearExtrusion)
            {
                firstToSecond = ModOp2D.Null;
                SurfaceOfLinearExtrusion sleother = other as SurfaceOfLinearExtrusion;
                // es würde genügen, wenn die beiden Kurven sich überlappen. Dann auf das Überlappungsintervall testen
                bool reverse;
                if (!Curves.SameGeometry(BasisCurve, sleother.BasisCurve, precision, out reverse)) return false;
                // zwei Extrempunkte bestimmen, damit sollte es OK sein
                GeoPoint2D uv1 = new GeoPoint2D(curveStartParameter, 0.0);
                GeoPoint p1 = PointAt(uv1);
                GeoPoint2D uv2 = sleother.PositionOf(p1);
                GeoPoint p2 = sleother.PointAt(uv2);
                if ((p1 | p2) > precision) return false;
                uv1 = new GeoPoint2D(curveEndParameter, 1.0);
                p1 = PointAt(uv1);
                uv2 = sleother.PositionOf(p1);
                p2 = sleother.PointAt(uv2);
                if ((p1 | p2) > precision) return false;
                firstToSecond = ModOp2D.Translate(uv2 - uv1);
                return true;
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public override CADability.GeoObject.RuledSurfaceMode IsRuled
        {
            get
            {
                return RuledSurfaceMode.ruledInV;
            }
        }
        public override bool IsVanishingProjection(Projection p, double umin, double umax, double vmin, double vmax)
        {
            return Precision.SameDirection(p.Direction, direction, false);
        }
        public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {   // there is no limit in v, and there is no good handling of semi-infinite bounds
            // base.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            vmin = 0.0;
            vmax = 1.0;
            umin = curveStartParameter;
            umax = curveEndParameter;
        }
        public override bool IsExtruded(GeoVector direction)
        {
            return Precision.SameDirection(this.direction, direction, false);
        }
        public override ISurface GetCanonicalForm(double precision, BoundingRect? bounds)
        {
            ICurve testWith = basisCurve;
            if (basisCurve is BSpline bsp)
            {
                if (bsp.GetSimpleCurve(precision, out ICurve simpleCurve))
                {
                    testWith = simpleCurve;
                }
            }
            if (testWith is Ellipse elli && elli.IsCircle)
            {
                double r = (elli).Radius;
                if (Precision.SameDirection(elli.Plane.Normal, this.direction, false))
                {
                    CylindricalSurface cs = new CylindricalSurface(elli.Center, elli.MajorAxis, elli.MinorAxis, elli.Plane.Normal);
                    GeoVector n1 = this.GetNormal(this.PositionOf(elli.StartPoint));
                    GeoVector n2 = cs.GetNormal(cs.PositionOf(elli.StartPoint));
                    if (n1 * n2 < 0) cs.ReverseOrientation();
                    return cs;
                }
            }
            else if (testWith is Line line && !Precision.SameDirection(line.EndDirection, direction, false))
            {   // a plane
                PlaneSurface ps = new PlaneSurface(line.StartPoint, line.StartDirection.Normalized, direction.Normalized);
                // test the orientation
                GeoVector n1 = this.GetNormal(this.PositionOf(line.StartPoint));
                GeoVector n2 = ps.GetNormal(ps.PositionOf(line.StartPoint));
                if (n1 * n2 < 0) ps.ReverseOrientation();
                return ps;
            }
            return null;
        }
        public override bool CanStretch => true;
#if DEBUG
        public override GeoObjectList DebugGrid
        {
            get
            {
                GeoObjectList res = new GeoObjectList();

                int n = 50;
                for (int i = 0; i <= n; i++)
                {
                    ModOp m = ModOp.Translate((double)i / n * this.direction);
                    ICurve crv = basisCurve.CloneModified(m);
                    crv.Trim(curveStartParameter, curveEndParameter);
                    res.Add(crv as IGeoObject);
                }
                return res;

            }
        }
#endif
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            List<IPropertyEntry> se = new List<IPropertyEntry>();
            if (basisCurve is IGeoObject go)
            {
                se.Add(go.GetShowProperties(frame) as IPropertyEntry);
            }
            GeoVectorProperty dir = new GeoVectorProperty(frame, "SurfaceOfLinearExtrusion.Direction");
            dir.ReadOnly = true;
            dir.OnGetValue = new EditableProperty<GeoVector>.GetValueDelegate(delegate () { return Direction; });
            se.Add(dir);
            return new GroupProperty("SurfaceOfLinearExtrusion", se.ToArray());
        }
        #endregion
        #region ISerializable Members
        protected SurfaceOfLinearExtrusion(SerializationInfo info, StreamingContext context)
        {
            basisCurve = info.GetValue("BasisCurve", typeof(ICurve)) as ICurve;
            direction = (GeoVector)info.GetValue("Direction", typeof(GeoVector));
            curveStartParameter = (double)info.GetValue("CurveStartParameter", typeof(double));
            curveEndParameter = (double)info.GetValue("CurveEndParameter", typeof(double));
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("BasisCurve", basisCurve, typeof(ICurve));
            info.AddValue("Direction", direction, typeof(GeoVector));
            info.AddValue("CurveStartParameter", curveStartParameter, typeof(double));
            info.AddValue("CurveEndParameter", curveEndParameter, typeof(double));
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            int ns = (basisCurve as IExportStep).Export(export, false);
            int nd = (direction.Normalized as IExportStep).Export(export, false);
            int nv = export.WriteDefinition("VECTOR( '', #" + nd.ToString() + ", " + export.ToString(direction.Length) + ")");
            return export.WriteDefinition("SURFACE_OF_LINEAR_EXTRUSION('',#" + ns.ToString() + ",#" + nv.ToString() + ")");
        }
        #endregion
    }
}
