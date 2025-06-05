using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// A planar infinite surface. Implements ISurface.
    /// The plane is defined by two vectors which are not necessary perpendicular or normalized.
    /// </summary>
    [Serializable()]
    public class PlaneSurface : ISurfaceImpl, ISerializable, IDeserializationCallback, IExportStep
    {
        private ModOp fromUnitPlane; // projects the XY plane into this surface
        private ModOp toUnitPlane; // inverted fromUnitPlane

        internal PlaneSurface(ModOp m, BoundingRect? usedArea = null) : base(usedArea)
        {
            fromUnitPlane = m;
            toUnitPlane = fromUnitPlane.GetInverse();
        }
        public PlaneSurface(Plane plane)
        {
            fromUnitPlane = new ModOp(plane.DirectionX, plane.DirectionY, plane.Normal, plane.Location);
            toUnitPlane = fromUnitPlane.GetInverse();
        }
        public PlaneSurface(GeoPoint location, GeoVector uDirection, GeoVector vDirection, GeoVector zDirection)
        {
            fromUnitPlane = new ModOp(uDirection, vDirection, zDirection, location);
            toUnitPlane = fromUnitPlane.GetInverse();
        }
        public PlaneSurface(GeoPoint location, GeoVector uDirection, GeoVector vDirection)
        {
            GeoVector zDirection = uDirection ^ vDirection;
            fromUnitPlane = new ModOp(uDirection, vDirection, zDirection, location);
            toUnitPlane = fromUnitPlane.GetInverse();
        }
        internal PlaneSurface(GeoPoint p0, GeoPoint px, GeoPoint py)
        {   // px und py geben ein Rechtssystem für die Ebene, wird bei MakeBox so verwendet
            GeoVector dirx = px - p0;
            GeoVector diry = py - p0;
            fromUnitPlane = new ModOp(dirx, diry, dirx ^ diry, p0);
            toUnitPlane = fromUnitPlane.GetInverse();
        }
        public override string ToString()
        {
            return "PlaneSurface: " + "loc: " + Location.ToString() + "dirx: " + DirectionX.ToString() + "diry: " + DirectionY.ToString();
        }
        public GeoPoint Location
        {
            get
            {
                return fromUnitPlane * GeoPoint.Origin;
            }
        }
        public GeoVector DirectionX
        {
            get
            {
                return fromUnitPlane * GeoVector.XAxis;
            }
        }
        public GeoVector DirectionY
        {
            get
            {
                return fromUnitPlane * GeoVector.YAxis;
            }
        }
        public GeoVector Normal
        {
            get
            {
                return fromUnitPlane * GeoVector.ZAxis;
            }
        }
        public ModOp ToXYPlane
        {
            get
            {
                return toUnitPlane;
            }
        }
        public ModOp FromXYPlane
        {
            get
            {
                return fromUnitPlane;
            }
        }
        public Plane Plane
        {
            get
            {
                return new Plane(Location, DirectionX, DirectionY);
            }
        }
        #region ISurfaceImpl Overrides
        private bool IsRectangular
        {
            get
            {
                GeoVector dirx = DirectionX;
                if (!Precision.IsNormedVector(dirx)) return false;
                GeoVector diry = DirectionX;
                if (!Precision.IsNormedVector(diry)) return false;
                if (!Precision.IsPerpendicular(dirx, diry, true)) return false;
                return true;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            return new PlaneSurface(m * fromUnitPlane, usedArea);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Make3dCurve (ICurve2D)"/>
        /// </summary>
        /// <param name="curve2d"></param>
        /// <returns></returns>
        public override ICurve Make3dCurve(CADability.Curve2D.ICurve2D curve2d)
        {
            if (curve2d is Curve2DAspect)
            {
                ICurve res = (curve2d as Curve2DAspect).Get3DCurve(this);
                if (res != null) return res;
            }
            if (curve2d is ProjectedCurve pc)
            {
                if (pc.Surface is PlaneSurface)
                {
                    BoundingRect otherBounds = new BoundingRect(PositionOf(pc.Surface.PointAt(pc.StartPoint)), PositionOf(pc.Surface.PointAt(pc.EndPoint)));
                    if (pc.Surface.SameGeometry(pc.GetExtent(), this, otherBounds, Precision.eps, out ModOp2D notneeded))
                    {
                        return pc.Curve3DFromParams; // if trimmed or reversed still returns the correct 3d curve (but trimmed and/or reversed)
                    }
                }
            }
            IGeoObject go = curve2d.MakeGeoObject(Plane.XYPlane);
            go.Modify(fromUnitPlane);
            return go as ICurve;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            // return (fromUnitPlane * GeoVector.ZAxis); // this looks like a sever bug! Fixed on 22.02.18
            return ((fromUnitPlane * GeoVector.XAxis) ^ (fromUnitPlane * GeoVector.YAxis)).Normalized;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            return (fromUnitPlane * GeoVector.XAxis);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            return (fromUnitPlane * GeoVector.YAxis);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            return fromUnitPlane * new GeoPoint(uv);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            if (toUnitPlane.IsNull) toUnitPlane = fromUnitPlane.GetInverse();
            return (toUnitPlane * p).To2D();
        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            location = PointAt(uv);
            du = UDirection(uv);
            dv = VDirection(uv);
            duu = GeoVector.NullVector;
            duv = GeoVector.NullVector;
            dvv = GeoVector.NullVector;
        }
        public override bool IsUPeriodic { get { return false; } }
        public override bool IsVPeriodic { get { return false; } }
        public override double UPeriod { get { return 0.0; } }
        public override double VPeriod { get { return 0.0; } }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.IsVanishingProjection (Projection, double, double, double, double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public override bool IsVanishingProjection(Projection p, double umin, double umax, double vmin, double vmax)
        {
            return Precision.IsPerpendicular(p.Direction, fromUnitPlane * GeoVector.ZAxis, false);
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
            // zuerst die Eckpunkte einschließen
            CheckZMinMax(p, umin, vmin, ref zMin, ref zMax);
            CheckZMinMax(p, umin, vmax, ref zMin, ref zMax);
            CheckZMinMax(p, umax, vmin, ref zMin, ref zMax);
            CheckZMinMax(p, umax, vmax, ref zMin, ref zMax);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            return new PlaneSurface(fromUnitPlane);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            boxedSurfaceEx = null;
            fromUnitPlane = m * fromUnitPlane;
            toUnitPlane = fromUnitPlane.GetInverse();
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
            PlaneSurface plm = pl.GetModified(toUnitPlane) as PlaneSurface;
            GeoVector dir = GeoVector.ZAxis ^ plm.Normal.Normalized; // Richtung der Linie
            dir = toUnitPlane * (Normal.Normalized ^ pl.Normal.Normalized);
            GeoVector2D dir2d = new GeoVector2D(dir.x, dir.y);
            if (dir.Length > 0.0)
            {
                GeoPoint2D[] ip = plm.GetLineIntersection(GeoPoint.Origin, new GeoVector(dir.y, -dir.x, dir.z)); // dir.z müsste ohnehin 0 sein
                if (ip.Length > 0)
                {
                    GeoPoint org = plm.PointAt(ip[0]);
                    GeoPoint2D org2d = new GeoPoint2D(org.x, org.y);
                    double lmin, lmax;
                    if (Math.Abs(dir.x) > Math.Abs(dir.y))
                    {   // eher waagrecht
                        lmin = (umin - org.x) / dir.x;
                        lmax = (umax - org.x) / dir.x;
                    }
                    else
                    {
                        lmin = (vmin - org.y) / dir.y;
                        lmax = (vmax - org.y) / dir.y;
                    }
                    ICurve2D curve2d1 = new Line2D(org2d + lmin * dir2d, org2d + lmax * dir2d);
                    Line line = Line.Construct();
                    line.SetTwoPoints(PointAt(curve2d1.StartPoint), PointAt(curve2d1.EndPoint));
                    ICurve2D curve2d2 = new Line2D(pl.PositionOf(line.StartPoint), pl.PositionOf(line.EndPoint));
                    return new DualSurfaceCurve[] { new DualSurfaceCurve(line, this, curve2d1, pl, curve2d2) };
                }
            }
            return new DualSurfaceCurve[] { };
            //return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetLineIntersection (GeoPoint, GeoVector)"/>
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            GeoPoint sp = toUnitPlane * startPoint;
            GeoVector dir = toUnitPlane * direction;
            if (dir.z != 0.0)
            {
                double l = -sp.z / dir.z;
                GeoPoint2D ip = new GeoPoint2D(sp.x + l * dir.x, sp.y + l * dir.y);
                return new GeoPoint2D[] { ip };
            }
            return new GeoPoint2D[] { };
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            PlaneSurface cc = CopyFrom as PlaneSurface;
            if (cc != null)
            {
                this.fromUnitPlane = cc.fromUnitPlane;
                this.toUnitPlane = cc.toUnitPlane;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetProjectedCurve (ICurve, double)"/>
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {
            // Hier muss beachtet werden, dass dieses PlaneSurface Objekt ein anderes uv System haben kann als
            // Plane, also begeben wir uns in das Unit System
            if (curve is InterpolatedDualSurfaceCurve)
            {
                return base.GetProjectedCurve(curve, precision);
            }
            ICurve crvunit = curve.CloneModified(toUnitPlane);
            return crvunit.GetProjectedCurve(Plane.XYPlane);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.ReverseOrientation ()"/>
        /// </summary>
        /// <returns></returns>
        public override ModOp2D ReverseOrientation()
        {   // x- und y-Achse vertauschen
            boxedSurfaceEx = null;
            fromUnitPlane = fromUnitPlane * new ModOp(0, 1, 0, 0, 1, 0, 0, 0, 0, 0, -1, 0);
            toUnitPlane = fromUnitPlane.GetInverse();
            return new ModOp2D(0, 1, 0, 1, 0, 0);
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
            firstToSecond = ModOp2D.Null;
            if (other is PlaneSurface)
            {
                PlaneSurface second = other as PlaneSurface;
                GeoPoint p = PointAt(thisBounds.GetCenter());
                if (Math.Abs(second.Plane.Distance(p)) > precision) return false;
                p = second.PointAt(otherBounds.GetCenter());
                if (Math.Abs(Plane.Distance(p)) > precision) return false;
                if (Precision.SameDirection(Normal, second.Normal, false))
                {
                    GeoPoint2D[] src = new GeoPoint2D[] { GeoPoint2D.Origin, new GeoPoint2D(1, 0), new GeoPoint2D(0, 1) };
                    GeoPoint2D[] dst = new GeoPoint2D[3];
                    for (int i = 0; i < 3; ++i)
                    {
                        dst[i] = second.PositionOf(PointAt(src[i]));
                    }
                    firstToSecond = ModOp2D.Fit(src, dst, true);
                    return true;
                }
                else
                    return false;
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public override double IsParallel(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            if (other is PlaneSurface ps)
            {
                if (Precision.SameDirection(this.Normal, ps.Normal, false))
                {
                    return this.GetDistance(ps.Location);
                }
            }
            return double.MaxValue;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetOffsetSurface (double)"/>
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public override ISurface GetOffsetSurface(double offset)
        {
            return new PlaneSurface(Location + offset * GetNormal(GeoPoint2D.Origin).Normalized, this.DirectionX, this.DirectionY);
            // the old code was wrong for fromUnitPlane with negative determinant:
            //GeoVector n = fromUnitPlane * GeoVector.ZAxis;
            //n.Norm();
            //ModOp newToUnit = toUnitPlane * ModOp.Translate(-offset * n);
            //return new PlaneSurface(newToUnit.GetInverse());
        }
        public override bool Oriented
        {
            get
            {
                return true;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Orientation (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double Orientation(GeoPoint p)
        {
            GeoPoint q = this.PointAt((GetLineIntersection(p, Normal))[0]);
            double d = q | p;
            if (Precision.IsEqual(q + d / Normal.Length * Normal, p)) return d;
            else return -d;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.HitTest (BoundingCube, out GeoPoint2D)"/>
        /// </summary>
        /// <param name="bc"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override bool HitTest(BoundingCube bc, out GeoPoint2D uv)
        {
            GeoPoint[] cube = bc.Points;
            bool[] pos = new bool[8];
            // any vertex of the cube on the plane?
            for (int i = 0; i < 8; ++i)
            {
                GeoPoint p = cube[i];
                if (Math.Abs((toUnitPlane * p).z) < Precision.eps)
                {
                    uv = PositionOf(p);
                    return true;
                }
                pos[i] = Orientation(p) < 0;
            }
            // any line of the cube interfering the plane?
            int[,] l = bc.LineNumbers;
            for (int k = 0; k < 12; ++k)
            {
                int i = l[k, 0];
                int j = l[k, 1];
                if (pos[i] != pos[j])
                {
                    GeoPoint2D[] erg = GetLineIntersection(cube[i], cube[j] - cube[i]);
                    uv = erg[0];
                    return true;
                }

            }
            // now the cube´s vertices are on one side only
            uv = GeoPoint2D.Origin;
            return false;  //convexity of the inner and outer points
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetExtrema ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoPoint2D[] GetExtrema()
        {
            return new GeoPoint2D[0];
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetPolynomialParameters ()"/>
        /// </summary>
        /// <returns></returns>
        public override double[] GetPolynomialParameters()
        {
            double[,] m = fromUnitPlane.Matrix;
            double[] res = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            for (int i = 0; i < 3; ++i)
            {
                res[6] += m[i, 0]; res[7] += m[i, 1]; res[8] += m[i, 2];
                res[9] += m[i, 3];
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Intersect (ICurve, BoundingRect, out GeoPoint[], out GeoPoint2D[], out double[])"/>
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="uvExtent"></param>
        /// <param name="ips"></param>
        /// <param name="uvOnFaces"></param>
        /// <param name="uOnCurve3Ds"></param>
        public override void Intersect(ICurve curve, BoundingRect uvExtent, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {
            if (curve is Line)
            {
                GeoPoint ip;
                if (Plane.Intersect(curve.StartPoint, curve.StartDirection, out ip))
                {
                    ips = new GeoPoint[] { ip };
                    uvOnFaces = new GeoPoint2D[] { PositionOf(ip) };
                    uOnCurve3Ds = new double[] { curve.PositionOf(ip) };
                }
                else
                {
                    ips = new GeoPoint[0];
                    uvOnFaces = new GeoPoint2D[0];
                    uOnCurve3Ds = new double[0];
                }
#if DEBUG
                //GeoPoint[] ipsdbg;
                //GeoPoint2D[] uvOnFacesdbg;
                //double[] uOnCurve3Dsdbg;
                //base.Intersect(curve, out ipsdbg, out uvOnFacesdbg, out uOnCurve3Dsdbg);
#endif
                return;
            }
            //else if (curve is IExplicitPCurve3D)
            //{
            //    ExplicitPCurve3D epc3d = (curve as IExplicitPCurve3D).GetExplicitPCurve3D();
            //    double [] res = epc3d.GetPlaneIntersection(Location, DirectionX, DirectionY);
            //    for (int i = 0; i < res.Length; i++)
            //    {
            //        double d = Plane.Distance(epc3d.PointAt(res[i]));
            //        if (i>0) d = Plane.Distance(epc3d.PointAt((res[i]+res[i-1])/2.0));
            //    }
            //    double dd = Plane.Distance(epc3d.PointAt(epc3d.knots[epc3d.knots.Length - 1]));
            //    for (int i = 0; i < res.Length; i++)
            //    {
            //        res[i] = (res[i] - epc3d.knots[0]) / (epc3d.knots[epc3d.knots.Length - 1] - epc3d.knots[0]);
            //    }
            //}
            else
            {
                if (curve.GetPlanarState() == PlanarState.Planar)
                {
                    GeoPoint loc;
                    GeoVector dir;
                    Plane pln = curve.GetPlane();
                    if (Plane.Intersect(curve.GetPlane(), out loc, out dir))
                    {
                        ICurve2D c2d = curve.GetProjectedCurve(pln);
                        BoundingRect ext = c2d.GetExtent();
                        ext.Inflate(ext.Height + ext.Width); // sonst entstehen null-Linien
                        ICurve2D ll = new Line2D(pln.Project(loc), pln.Project(dir), ext);
#if DEBUG
                        DebuggerContainer dc = new DebuggerContainer();
                        dc.Add(c2d);
                        dc.Add(ll);
#endif
                        GeoPoint2DWithParameter[] gpp = ll.Intersect(c2d);
                        ips = new GeoPoint[gpp.Length];
                        uvOnFaces = new GeoPoint2D[gpp.Length];
                        uOnCurve3Ds = new double[gpp.Length];
                        for (int i = 0; i < gpp.Length; ++i)
                        {
                            ips[i] = pln.ToGlobal(gpp[i].p);
                            uvOnFaces[i] = PositionOf(ips[i]);
                            uOnCurve3Ds[i] = curve.PositionOf(ips[i]);
                        }
                    }
                    else
                    {
                        ips = new GeoPoint[0];
                        uvOnFaces = new GeoPoint2D[0];
                        uOnCurve3Ds = new double[0];
                    }
#if DEBUG
                    //GeoPoint[] ipsdbg;
                    //GeoPoint2D[] uvOnFacesdbg;
                    //double[] uOnCurve3Dsdbg;
                    //base.Intersect(curve, out ipsdbg, out uvOnFacesdbg, out uOnCurve3Dsdbg);
#endif
                    return;
                }
            }
            TetraederHull th = new TetraederHull(curve);
            // alle Kurven müssten eine gemeinsame basis habe, auf die man sich hier beziehen kann
            // damit die TetraederHull nicht mehrfach berechnet werden muss
            th.PlaneIntersection(this, out ips, out uvOnFaces, out uOnCurve3Ds);
            // base.Intersect(curve, out ips, out uvOnFaces, out uOnCurve3Ds);
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
            return Line.TwoPoints(fromUnitPlane * new GeoPoint(u, vmin, 0), fromUnitPlane * new GeoPoint(u, vmax, 0));
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
            return Line.TwoPoints(fromUnitPlane * new GeoPoint(umin, v, 0), fromUnitPlane * new GeoPoint(umax, v, 0));
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PerpendicularFoot (GeoPoint)"/>
        /// </summary>
        /// <param name="fromHere"></param>
        /// <returns></returns>
        public override GeoPoint2D[] PerpendicularFoot(GeoPoint fromHere)
        {   // den einzigen Fußpunkt gibts immer, auch wenn der Punkt genau auf der Fläche liegt
            return new GeoPoint2D[] { PositionOf(fromHere) };
        }
        public override double MaxDist(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp)
        {
            mp = new GeoPoint2D(sp, ep);
            return 0.0;
        }
        public override ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            IDualSurfaceCurve[] dsc = other.GetPlaneIntersection(this, otherBounds.Left, otherBounds.Right, otherBounds.Bottom, otherBounds.Top, Precision.eps);
            // at least when other is also a plane, we must provide otherBounds
            ICurve[] res = new ICurve[dsc.Length];
            for (int i = 0; i < dsc.Length; i++)
            {
                res[i] = dsc[i].Curve3D;
            }
            return res; // es wird nicht auf otherBounds geclippt
        }
        public override IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> extremePositions)
        {
            // this is always implemented at the more complex surface:
            if (other is PlaneSurface)
            {
                return GetPlaneIntersection(other as PlaneSurface, thisBounds.Left, thisBounds.Right, thisBounds.Bottom, thisBounds.Top, Precision.eps);
            }
            else
            {
                IDualSurfaceCurve[] res = other.GetDualSurfaceCurves(otherBounds, this, thisBounds, seeds);
                for (int i = 0; i < res.Length; i++)
                {
                    res[i].SwapSurfaces();
                }
                return res;
            }
        }
        public override int GetExtremePositions(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, out List<Tuple<double, double, double, double>> extremePositions)
        {
            switch (other)
            {
                case SphericalSurface ss:
                    {
                        GeoPoint2D fp = PositionOf(ss.Location);
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        extremePositions.Add(new Tuple<double, double, double, double>(fp.x, fp.y, double.NaN, double.NaN));
                        return 1;
                    }
                case ToroidalSurface ts:
                    {
                        GeoVector dir = (ts.ZAxis ^ Normal) ^ ts.ZAxis; // this is the direction of a line from the center of the torus where the u positions of the extereme position of the torus are
                        if (dir.IsNullVector()) break;
                        GeoPoint2D[] ip = ts.GetLineIntersection(ts.Location, dir); // the result should be 4 points, but we are interested in u parameters only and this must be u and u+pi
                        if (ip != null && ip.Length > 0)
                        {
                            extremePositions = new List<Tuple<double, double, double, double>>();
                            double u = ip[0].x;
                            SurfaceHelper.AdjustUPeriodic(ts, otherBounds, ref u);
                            if (u >= otherBounds.Left && u <= otherBounds.Right) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, u, double.NaN));
                            u += Math.PI;
                            SurfaceHelper.AdjustUPeriodic(ts, otherBounds, ref u);
                            if (u >= otherBounds.Left && u <= otherBounds.Right) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, u, double.NaN));
                            return extremePositions.Count;
                        }
                    }
                    break;
                case PlaneSurface ps:
                case CylindricalSurface cys:
                case ConicalSurface cos:
                    extremePositions = null;
                    return 0;
                case ISurfaceImpl ns:
                    {
                        GeoPoint2D[] normals = ns.BoxedSurfaceEx.PositionOfNormal(Normal);
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        for (int i = 0; i < normals.Length; i++)
                        {
                            if (otherBounds.Contains(normals[i])) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, normals[i].x, normals[i].y));
                        }
                    }
                    break;
            }
            extremePositions = null;
            return -1; // means: no implementation for this combination
        }
        public override bool IsExtruded(GeoVector direction)
        {
            return Precision.IsPerpendicular(Normal, direction, false);
        }
        #endregion
        #region ISerializable Members
        protected PlaneSurface(SerializationInfo info, StreamingContext context)
        {
            fromUnitPlane = (ModOp)info.GetValue("FromUnitPlane", typeof(ModOp));
            // vorher war:
            //location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            //uDirection = (GeoVector)info.GetValue("UDirection", typeof(GeoVector));
            //vDirection = (GeoVector)info.GetValue("VDirection", typeof(GeoVector));
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("FromUnitPlane", fromUnitPlane, typeof(ModOp));
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            toUnitPlane = fromUnitPlane.GetInverse();
        }
        #endregion
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            List<IPropertyEntry> se = new List<IPropertyEntry>();
            GeoPointProperty location = new GeoPointProperty(frame, "PlanarSurface.Location");
            location.ReadOnly = true;
            location.OnGetValue = new EditableProperty<GeoPoint>.GetValueDelegate(delegate () { return fromUnitPlane * GeoPoint.Origin; });
            se.Add(location);
            GeoVectorProperty dirx = new GeoVectorProperty(frame, "PlanarSurface.DirectionX");
            dirx.ReadOnly = true;
            dirx.IsAngle = false;
            dirx.OnGetValue = new EditableProperty<GeoVector>.GetValueDelegate(delegate () { return fromUnitPlane * GeoVector.XAxis; });
            se.Add(dirx);
            GeoVectorProperty diry = new GeoVectorProperty(frame, "PlanarSurface.DirectionY");
            diry.ReadOnly = true;
            diry.IsAngle = false;
            diry.OnGetValue = new EditableProperty<GeoVector>.GetValueDelegate(delegate () { return fromUnitPlane * GeoVector.YAxis; });
            se.Add(diry);
            return new GroupProperty("PlanarSurface", se.ToArray());
        }
        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            // #171=AXIS2_PLACEMENT_3D('Plane Axis2P3D',#168,#169,#170) ;
            int ax = export.WriteAxis2Placement3d(Location, Normal, DirectionX);
            // #172=PLANE('',#171) ;
            return export.WriteDefinition("PLANE('',#" + ax.ToString() + ")");
        }
    }
}
