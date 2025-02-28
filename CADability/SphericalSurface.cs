using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// Interface to handle both CylindricalSurface and CylindricalSurfaceNP
    /// </summary>
    public interface ISphere : ISurfaceWithRadius
    {
        GeoPoint Center { get; set; }
        bool OutwardOriented { get; }
    }
    [Serializable()]
    public class SphericalSurface : ISurfaceImpl, ISerializable, IDeserializationCallback, IExportStep, ISphere
    {
        // Die Kugel ist so beschaffen, dass sie lediglich durch eine ModOp definiert ist.
        // Die Eiheitskugel steht im Ursprung mit Radius 1, u beschreibt einen Breitenkreis, v einen Längenkreis
        // u ist im Intervall 0 .. 2*pi, v: -pi/2 .. pi/2
        private ModOp toSphere; // diese ModOp modifiziert die Einheitskugel in die konkrete Kugel
        private ModOp toUnit; // die inverse ModOp zum schnelleren Rechnen
        public SphericalSurface(GeoPoint location, GeoVector directionx, GeoVector directiony, GeoVector directionz)
        {
            ModOp m1 = ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis },
                new GeoVector[] { directionx, directiony, directionz });
            ModOp m2 = ModOp.Translate(location.x, location.y, location.z);
            toSphere = m2 * m1;
            toUnit = toSphere.GetInverse();
        }
        internal SphericalSurface(ModOp toSphere, BoundingRect? usedArea = null) : base(usedArea)
        {
            this.toSphere = toSphere;
            toUnit = toSphere.GetInverse();
        }
        public double RadiusX
        {
            get
            {
                return (toSphere * GeoVector.XAxis).Length;
            }
        }
        public double RadiusY
        {
            get
            {
                return (toSphere * GeoVector.YAxis).Length;
            }
        }
        public double RadiusZ
        {
            get
            {
                return (toSphere * GeoVector.ZAxis).Length;
            }
        }
        public GeoPoint Location
        {
            get
            {
                return toSphere * GeoPoint.Origin;
            }
        }
        public GeoVector Axis
        {
            get
            {
                return toSphere * GeoVector.ZAxis;
            }
        }
        public GeoVector XAxis
        {
            get
            {
                return toSphere * GeoVector.XAxis;
            }
        }
        public GeoVector YAxis
        {
            get
            {
                return toSphere * GeoVector.YAxis;
            }
        }
        public GeoVector ZAxis
        {
            get
            {
                return toSphere * GeoVector.ZAxis;
            }
        }
        #region ISurfaceImpl Overrides
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            return new SphericalSurface(m * toSphere, usedArea);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            //GeoVector res = toSphere * new GeoVector(Math.Cos(uv.x) * Math.Cos(uv.y), Math.Sin(uv.x) * Math.Cos(uv.y), Math.Sin(uv.y));
            //GeoVector res = UDirection(uv) ^ VDirection(uv); // wg. Berücksichtigung der Orientierung
            GeoVector res = (PointAt(uv) - toSphere * GeoPoint.Origin).Normalized;
            if (!toSphere.Oriented) res = -res;
            //res.Norm();
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetTangentCurves (GeoVector, double, double, double, double)"/>
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public override ICurve2D[] GetTangentCurves(GeoVector direction, double umin, double umax, double vmin, double vmax)
        {
            GeoVector dirunit = toUnit * direction;
            // das ist ein GroßKreis auf der Kugel, der in uv auszudrücken ist
            // es gibt keine Standard ICurve2D, die das kann
            // zwei Sonderfälle sind der Blick von oben und der Blick aus der Äquatorebene
            if (Precision.SameDirection(dirunit, GeoVector.ZAxis, false))
            {
                return new ICurve2D[] { new Line2D(new GeoPoint2D(umin, 0.0), new GeoPoint2D(umax, 0.0)) };
            }
            else if (Precision.IsDirectionInPlane(dirunit, Plane.XYPlane))
            {
                double a = Math.Atan2(dirunit.x, -dirunit.y); // senkrecht zur Richtung der Betrachtung
                // jetzt sind die Werte zwischen umin und umax gesucht
                // vorausgesetzt umin<umax, aber das gilt ja wohl, oder?
                while (a >= umin) a -= Math.PI * 2.0;
                List<ICurve2D> res = new List<ICurve2D>(2);
                a += Math.PI;
                while (a <= umax)
                {
                    if (a >= umin)
                    {
                        res.Add(new Line2D(new GeoPoint2D(a, vmin), new GeoPoint2D(a, vmax)));
                    }
                    a += Math.PI; // 180° weiter ist die andere
                }
                return res.ToArray();
            }
            else
            {
                // der allgemeine schräge Fall: hier sollte noch eine bessere Lösung gefunden werden
                // ein ICurve2D, welches ein Großkreis auf einer Kugel beschreibt
                // vorläufig aber
                return base.GetTangentCurves(direction, umin, umax, vmin, vmax);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            return toSphere * new GeoPoint(Math.Cos(uv.x) * Math.Cos(uv.y), Math.Sin(uv.x) * Math.Cos(uv.y), Math.Sin(uv.y));
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            return toSphere * new GeoVector(-Math.Sin(uv.x) * Math.Cos(uv.y), Math.Cos(uv.x) * Math.Cos(uv.y), 0.0);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            return toSphere * new GeoVector(-Math.Cos(uv.x) * Math.Sin(uv.y), -Math.Sin(uv.x) * Math.Sin(uv.y), Math.Cos(uv.y));
        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            location = PointAt(uv);
            du = UDirection(uv);
            dv = VDirection(uv);
            duu = toSphere * new GeoVector(-Math.Cos(uv.x) * Math.Cos(uv.y), -Math.Sin(uv.x) * Math.Cos(uv.y), 0.0);
            duv = toSphere * new GeoVector(Math.Sin(uv.x) * Math.Sin(uv.y), -Math.Cos(uv.x) * Math.Sin(uv.y), 0.0);
            dvv = toSphere * new GeoVector(-Math.Cos(uv.x) * Math.Cos(uv.y), -Math.Sin(uv.x) * Math.Cos(uv.y), -Math.Sin(uv.y));
        }
        public override bool IsUPeriodic
        {
            get
            {
                return true;
            }
        }
        public override bool IsVPeriodic
        {
            get
            {
                return true;
            }
        }
        public override double UPeriod
        {
            get
            {
                return 2.0 * Math.PI;
            }
        }
        public override double VPeriod
        {
            get
            {
                return 2.0 * Math.PI;
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
            // return base.GetLineIntersection(startPoint, direction);
            GeoPoint sp = toUnit * startPoint;
            GeoVector dir = toUnit * direction;
            dir.Norm();
            double d = Geometry.DistPL(GeoPoint.Origin, sp, dir);
            if (d < Precision.eps)
            {
                // die Linie geht durch den Kugelmittelpunkt (Ursprung)
                GeoPoint2D[] ip = new GeoPoint2D[2];
                ip[0] = PositionOfUnit(GeoPoint.Origin + dir);
                ip[1] = PositionOfUnit(GeoPoint.Origin - dir);
                return ip;
            }
            else
            {
                // betrachte jetzt die Ebene, in der die Linie liegt und durch den Nullpunkt
                Plane pl = new Plane(GeoPoint.Origin, dir, sp - GeoPoint.Origin);
                GeoPoint2D spl = pl.Project(sp);
                GeoVector2D dirl = pl.Project(dir); // das muss die X-Achse sein, oder?
                GeoPoint2D[] ip = Geometry.IntersectLC(spl, spl + dirl, GeoPoint2D.Origin, 1.0);
                for (int i = 0; i < ip.Length; i++)
                {
                    ip[i] = PositionOfUnit(pl.ToGlobal(ip[i]));
                }
                return ip;
            }
        }
        private GeoPoint2D PositionOfUnit(GeoPoint geoPoint)
        {
            if (geoPoint.y == 0.0 && geoPoint.x == 0.0)
            {
                if (geoPoint.z > 0.0) return new GeoPoint2D(0.0, Math.PI / 2.0);
                else return new GeoPoint2D(0.0, -Math.PI / 2.0);
            }
            double u = Math.Atan2(geoPoint.y, geoPoint.x);
            if (u < 0.0) u += 2.0 * Math.PI; // Standardbereich für u zwischen 0 und 2*pi
            // it was Asin(z), but this is bad, if z>1, we should not return pi/2 for z>1, the following is better
            return new GeoPoint2D(u, Math.Atan2(geoPoint.z, Math.Sqrt(geoPoint.y * geoPoint.y + geoPoint.x * geoPoint.x)));
        }
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
            return false; // verschwindet nie
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
                if (pc.Surface is SphericalSurface)
                {
                    BoundingRect otherBounds = new BoundingRect(PositionOf(pc.Surface.PointAt(pc.StartPoint)), PositionOf(pc.Surface.PointAt(pc.EndPoint)));
                    if (pc.Surface.SameGeometry(pc.GetExtent(), this, otherBounds, Precision.eps, out ModOp2D notneeded))
                    {
                        if (this.GetDistance(pc.Curve3DFromParams.PointAt(0.5)) < Precision.eps) // the 3d curve must reside on the surface
                            return pc.Curve3DFromParams; // if trimmed or reversed still returns the correct 3d curve (but trimmed and/or reversed)
                    }
                }
            }
            // return base.Make3dCurve(curve2d); warum stand das da?
            if (curve2d is Line2D)
            {
                // horizontale oder vertikale Linien als Besonderheit:
                Line2D l2d = curve2d as Line2D;
                double d = Math.Abs(l2d.StartPoint.y - l2d.EndPoint.y);
                // wir sind im uv sytem der Kugel, also absolutes epsilon
                if (d < 1e-8)
                {   // Parallelkreis zum Äquator
                    Ellipse e = Ellipse.Construct();
                    double z = Math.Sin(l2d.StartPoint.y);
                    double r = Math.Cos(l2d.StartPoint.y);
                    if (r < 1e-13) return null; // singuläre Stelle gibt keine Kurve!
                    e.SetArcPlaneCenterRadiusAngles(new Plane(Plane.StandardPlane.XYPlane, z),
                        new GeoPoint(0.0, 0.0, z), r, l2d.StartPoint.x, l2d.EndPoint.x - l2d.StartPoint.x);
                    e.Modify(toSphere);
                    return e;
                }
                d = Math.Abs(l2d.StartPoint.x - l2d.EndPoint.x);
                if (d < 1e-8)
                {
                    // senkrechte Linie, also Längengrad
                    Plane pl = new Plane(GeoPoint.Origin, new GeoVector(l2d.StartPoint.x, Angle.A0), GeoVector.ZAxis);
                    Ellipse e = Ellipse.Construct();
                    e.SetArcPlaneCenterRadiusAngles(pl, GeoPoint.Origin, 1.0, l2d.StartPoint.y, l2d.EndPoint.y - l2d.StartPoint.y);
                    e.Modify(toSphere);
                    return e;
                }
            }
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
            return GetDualSurfaceCurves(new BoundingRect(umin, vmin, umax, vmax), pl, BoundingRect.EmptyBoundingRect, null, null); // ist dort schon richtig implementiert
            
            //Unreachable code
            /*
            // hier könnte man die oben beschriebene 2D Schnittkure erzeugen
            Plane pln = new Plane(toUnit * pl.Location, toUnit * pl.DirectionX, toUnit * pl.DirectionY);
            bool rotated = false;
            ModOp mm1 = ModOp.Identity;
            if (Precision.IsPerpendicular(GeoVector.ZAxis, pln.Normal, true))
            {
                Angle be = new Angle();
                be.Degree = 90;
                rotated = true;
                if (Precision.IsPerpendicular(GeoVector.YAxis, pln.Normal, true))
                {
                    ModOp mm = ModOp.Rotate(GeoVector.YAxis, be.Radian);
                    mm1 = mm.GetInverse();
                    pln.Modify(mm);
                }
                else
                {
                    ModOp mm = ModOp.Rotate(GeoVector.XAxis, be.Radian);
                    mm1 = mm.GetInverse();
                    pln.Modify(mm);
                }
            }
            GeoPoint l = pln.Intersect(GeoPoint.Origin, GeoVector.ZAxis);
            Angle a = new Angle(pln.Normal.x, pln.Normal.y);
            ModOp m = ModOp.Rotate(GeoVector.ZAxis, -a);
            GeoVector normal = m * pln.Normal;
            ModOp m1 = m.GetInverse();

            GeoVector norm1 = m1 * normal;
            GeoVector2D dirl2D = new GeoVector2D(-normal.z, normal.x);

            GeoPoint2D lz2D = new GeoPoint2D(0, l.z);
            GeoPoint2D cnt2D;
            Geometry.IntersectLL(lz2D, dirl2D, GeoPoint2D.Origin, new GeoVector2D(normal.x, normal.z), out cnt2D);

            double r = 1.0;
            GeoPoint2D[] tmp;
            tmp = Geometry.IntersectLC(lz2D, dirl2D, GeoPoint2D.Origin, r);
            if (tmp.Length <= 1)
                return new IDualSurfaceCurve[0];

            GeoPoint p1 = new GeoPoint(tmp[0].x, 0, tmp[0].y);
            GeoPoint p2 = new GeoPoint(tmp[1].x, 0, tmp[1].y);
            double d = Geometry.Dist(p1, p2);
            GeoPoint p3 = new GeoPoint(cnt2D.x, d / 2, cnt2D.y);
            GeoPoint p4 = new GeoPoint(cnt2D.x, -d / 2, cnt2D.y);

            GeoPoint cnt = m1 * new GeoPoint(cnt2D.x, 0, cnt2D.y);
            GeoVector majax = new GeoVector(cnt, m1 * p1);
            GeoVector minax = new GeoVector(cnt, m1 * p3);

            GeoPoint center = toSphere * cnt;
            GeoVector majaxis = toSphere * majax;
            GeoVector minaxis = toSphere * minax;
            if (rotated)
            {
                center = toSphere * (mm1 * cnt);
                majaxis = toSphere * (mm1 * majax);
                minaxis = toSphere * (mm1 * minax);
            }
            Ellipse elli = Ellipse.Construct();
            elli.SetEllipseCenterAxis(center, majaxis, minaxis);
            elli.StartParameter = 0.0;
            elli.SweepParameter = SweepAngle.Full;
            GeoPoint2D centerOnPl = pl.PositionOf(center);
            GeoPoint2D p1OnPl = pl.PositionOf(center + majaxis);
            GeoPoint2D p2OnPl = pl.PositionOf(center - minaxis);
            Ellipse2D elli2d = Geometry.Ellipse2P2T(p1OnPl, p2OnPl, p2OnPl - centerOnPl, p1OnPl - centerOnPl);
            ICurve2D c2dpl = elli2d.Trim(0.0, 1.0);
            GeoPoint2D[] pnts = new GeoPoint2D[50];
            pnts[0] = this.PositionOf(elli.PointAt(0));
            d = 0;
            bool ok = true;
            for (int i = 1; i < pnts.Length; i++)
            {
                GeoPoint b = elli.PointAt(i * 1.0 / (pnts.Length - 1));
                GeoPoint2D z = this.PositionOf(b);
                if (Math.Abs(z.x - pnts[i - 1].x) >= Math.PI)
                {
                    if (ok)
                    {
                        ok = false;
                        if (z.x > pnts[i - 1].x)
                            d = -2 * Math.PI;
                        else
                            d = +2 * Math.PI;
                    }
                    z.x += d;
                }
                pnts[i] = z;
            }
            BSpline2D.AdjustPeriodic(pnts, 2 * Math.PI, 2 * Math.PI);
            BSpline2D c2d = new BSpline2D(pnts, 2, false);
            DualSurfaceCurve dsc = new DualSurfaceCurve(elli, this, c2d, pl, c2dpl);
            return new IDualSurfaceCurve[] { dsc };
            */
            //            return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
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
            intu = new double[7];
            intu[0] = umin;
            for (int i = 1; i < 6; ++i) intu[i] = umin + i * (umax - umin) / 6.0;
            intu[6] = umax;
            intv = new double[3];
            intv[0] = vmin;
            intv[1] = vmin + (vmax - vmin) / 2.0;
            intv[2] = vmax;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            return PositionOfUnit(toUnit * p);
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
            GeoPoint2D uv = PositionOfUnit(GeoPoint.Origin + p.Direction);
            if (uv.x > umin && uv.x < umax && uv.y > vmin && uv.y < vmax)
            {
                CheckZMinMax(p, uv.x, uv.y, ref zMin, ref zMax);
            }
            uv = PositionOfUnit(GeoPoint.Origin - p.Direction);
            if (uv.x > umin && uv.x < umax && uv.y > vmin && uv.y < vmax)
            {
                CheckZMinMax(p, uv.x, uv.y, ref zMin, ref zMax);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            return new SphericalSurface(toSphere);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            boxedSurfaceEx = null;
            toSphere = m * toSphere;
            toUnit = toSphere.GetInverse();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            SphericalSurface cc = CopyFrom as SphericalSurface;
            if (cc != null)
            {
                this.toSphere = cc.toSphere;
                this.toUnit = cc.toUnit;
            }
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
            GeoVector v = toUnit * p - GeoPoint.Origin;
            return v.Length - 1;
            //GeoVector v = p-Location;
            //if (v.Length < Precision.eps)
            //    return 0;
            //GeoPoint2D[] g = GetLineIntersection(Location, p-Location);
            //double d = Math.Min(Location | PointAt(g[0]), Location | PointAt(g[1]));
            //return ((p|Location) - d);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.HitTest (BoundingCube, out GeoPoint2D)"/>
        /// </summary>
        /// <param name="bc"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override bool HitTest(BoundingCube bc, out GeoPoint2D uv)
        {
            // any vertex of the cube on the sphere?
            uv = GeoPoint2D.Origin;
            GeoPoint[] cube = bc.Points;
            bool[] pos = new bool[8];
            for (int i = 0; i < 8; ++i)
            {
                GeoPoint p = cube[i];
                GeoPoint q = toUnit * p;
                if (Math.Abs(q.x * q.x + q.y * q.y + q.z * q.z - 1) < Precision.eps)
                {
                    uv = PositionOf(p);
                    return true;
                }
                pos[i] = Orientation(p) <= 0;
            }

            // any line of the cube interfering the sphere?
            int[,] l = bc.LineNumbers;
            for (int k = 0; k < 12; ++k)
            {
                int i = l[k, 0];
                int j = l[k, 1];
                GeoPoint2D[] erg = GetLineIntersection(cube[i], cube[j] - cube[i]);
                for (int m = 0; m < erg.Length; ++m)
                {
                    GeoPoint gp = PointAt(erg[m]);
                    if (bc.Contains(gp, bc.Size * 1e-8))
                    {
                        uv = erg[m];
                        return true;
                    }
                }
                if (pos[i] != pos[j])
                    throw new ApplicationException("internal error: SphericalSurface.HitTest");
            }

            // cube´s vertices within the surface?
            if (pos[0] && pos[1] && pos[2] && pos[3] && pos[4] && pos[5] && pos[6] && pos[7])
                return false;   //convexity of the inner points 

            // complete sphere outside of the cube?
            for (int i = 0; i < 8; ++i)
            {
                cube[i] = toUnit * cube[i];
            }
            Plane[] planes = new Plane[6];
            planes[0] = new Plane(cube[0], cube[1], cube[2]);
            planes[1] = new Plane(cube[0], cube[2], cube[4]);
            planes[2] = new Plane(cube[0], cube[4], cube[1]);
            planes[3] = new Plane(cube[7], cube[5], cube[3]);
            planes[4] = new Plane(cube[7], cube[3], cube[6]);
            planes[5] = new Plane(cube[7], cube[6], cube[5]);
            for (int i = 0; i < 6; ++i)
            {
                GeoPoint p = PointAt(planes[i].Project(GeoPoint.Origin));
                GeoPoint gp = planes[i].ToGlobal(p);
                if ((bc.Contains(gp, Precision.eps)) && (gp - Location).Length <= 1)
                {
                    GeoPoint2D[] s = GetLineIntersection(Location, gp - Location);
                    for (int j = 0; j < s.Length; ++j)
                    {
                        GeoPoint g = PointAt(s[j]);
                        if (bc.Contains(g, Precision.eps))
                        {
                            uv = s[j];
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetSaveUSteps ()"/>
        /// </summary>
        /// <returns></returns>
        protected override double[] GetSaveUSteps()
        {
            return new double[] { 0.0, Math.PI / 2.0, Math.PI, 3 * Math.PI / 2, 2 * Math.PI };
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetSaveVSteps ()"/>
        /// </summary>
        /// <returns></returns>
        protected override double[] GetSaveVSteps()
        {
            return new double[] { -Math.PI, 0.0, Math.PI };
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetExtrema ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoPoint2D[] GetExtrema()
        {   // noch nicht getestet
            GeoPoint2D[] res = new GeoPoint2D[6];
            GeoVector axis = toUnit * GeoVector.XAxis;
            double u = Math.Atan2(axis.y, axis.x);
            double v = Math.Atan2(axis.z, Math.Sqrt(axis.x * axis.x + axis.y * axis.y));
            res[0] = new GeoPoint2D(u, v);
            res[1] = new GeoPoint2D(u + Math.PI, -v);
            axis = toUnit * GeoVector.YAxis;
            u = Math.Atan2(axis.y, axis.x);
            v = Math.Atan2(axis.z, Math.Sqrt(axis.x * axis.x + axis.y * axis.y));
            res[2] = new GeoPoint2D(u, v);
            res[3] = new GeoPoint2D(u + Math.PI, -v);
            axis = toUnit * GeoVector.ZAxis;
            u = Math.Atan2(axis.y, axis.x);
            v = Math.Atan2(axis.z, Math.Sqrt(axis.x * axis.x + axis.y * axis.y));
            res[4] = new GeoPoint2D(u, v);
            res[5] = new GeoPoint2D(u + Math.PI, -v);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetPolynomialParameters ()"/>
        /// </summary>
        /// <returns></returns>
        public override double[] GetPolynomialParameters()
        {
            double[,] m = toSphere.Matrix;
            double[] res = { 0, 0, 0, 0, 0, 0, 0, 0, 0, -1 };
            for (int i = 0; i < 3; ++i)
            {
                res[0] += m[i, 0] * m[i, 0]; res[1] += m[i, 1] * m[i, 1]; res[2] += m[i, 2] * m[i, 2];
                res[3] += 2 * m[i, 0] * m[i, 1]; res[4] += 2 * m[i, 1] * m[i, 2]; res[5] += 2 * m[i, 0] * m[i, 2];
                res[6] += 2 * m[i, 0] * m[i, 3]; res[7] += 2 * m[i, 1] * m[i, 3]; res[8] += 2 * m[i, 2] * m[i, 3];
                res[9] += m[i, 3] * m[i, 3];
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.ReverseOrientation ()"/>
        /// </summary>
        /// <returns></returns>
        public override ModOp2D ReverseOrientation()
        {
            boxedSurfaceEx = null;
            toSphere = toSphere * new ModOp(1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1, 0); // umkehrung von y
            toUnit = toSphere.GetInverse();
            return new ModOp2D(-1, 0, 2.0 * Math.PI, 0, 1, 0);
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
            Ellipse e = Ellipse.Construct();
            Plane pln = new Plane(GeoPoint.Origin, new GeoVector(Math.Cos(u), Math.Sin(u), 0.0), GeoVector.ZAxis);
            e.SetArcPlaneCenterStartEndPoint(pln, GeoPoint2D.Origin, new GeoPoint2D(Math.Cos(vmin), Math.Sin(vmin)), new GeoPoint2D(Math.Cos(vmax), Math.Sin(vmax)), pln, true);
            e.Modify(toSphere);
            return e;
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
            Ellipse e = Ellipse.Construct();
            double z = Math.Sin(v);
            e.SetCirclePlaneCenterRadius(new Plane(Plane.XYPlane, z), new GeoPoint(0, 0, z), Math.Cos(v));
            e.StartParameter = umin;
            e.SweepParameter = umax - umin;
            e.Modify(toSphere);
            return e;
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
            if (other is SphericalSurface)
            {
                SphericalSurface othersphere = other as SphericalSurface;
                if ((othersphere.Location | Location) < precision &&
                    Math.Abs(RadiusX - othersphere.RadiusX) < precision &&
                    Math.Abs(RadiusY - othersphere.RadiusY) < precision &&
                    Math.Abs(RadiusZ - othersphere.RadiusZ) < precision)
                {
                    GeoPoint2D[] src = new GeoPoint2D[3];
                    GeoPoint2D[] dst = new GeoPoint2D[3];
                    src[0] = GeoPoint2D.Origin;
                    src[1] = new GeoPoint2D(1.0, 0.0);
                    src[2] = new GeoPoint2D(0.0, 1.0);
                    for (int i = 0; i < 3; ++i)
                    {
                        dst[i] = othersphere.PositionOf(PointAt(src[i]));
                    }
                    firstToSecond = ModOp2D.Fit(src, dst, true);
                    return true;
                }
                return false;
            }
            // sonst die allgemeine Überprüfung
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public override double MaxDist(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp)
        {
            mp = new GeoPoint2D(sp, ep);
            // ACHTUNG: zyklisch wird hier nicht berücksichtigt, wird aber vom aufrufenden Kontext (Triangulierung) berücksichtigt
            // ansonsten wäre ja auch nicht klar, welche 2d-Linie gemeint ist
            return Geometry.DistPL(PointAt(mp), PointAt(sp), PointAt(ep));
        }
        public override double[] GetUSingularities()
        {
            return new double[] { };
        }
        public override double[] GetVSingularities()
        {
            return new double[] { -Math.PI / 2.0, Math.PI / 2.0 };
        }
        public override void Intersect(ICurve curve, BoundingRect uvExtent, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {
            ips = null;
            if (curve is Line)
            {
                Line line = curve as Line;
                ips = Geometry.IntersectSphereLine(toUnit * line.StartPoint, toUnit * line.StartDirection);
            }
            else if (curve is Ellipse)
            {
                Ellipse elli = (curve as Ellipse);
                ips = Geometry.IntersectSphereEllipse(toUnit * elli.Center, toUnit * elli.MajorAxis, toUnit * elli.MinorAxis);
            }
            if (ips != null)
            {
                uvOnFaces = new GeoPoint2D[ips.Length];
                uOnCurve3Ds = new double[ips.Length];
                for (int i = 0; i < ips.Length; i++)
                {
                    ips[i] = toSphere * ips[i];
                    uvOnFaces[i] = PositionOf(ips[i]);
                    SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uvOnFaces[i]);
                    uOnCurve3Ds[i] = curve.PositionOf(ips[i]);
                }
            }
            else base.Intersect(curve, uvExtent, out ips, out uvOnFaces, out uOnCurve3Ds);
        }
        public override ICurve Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, GeoPoint seed)
        {
            ICurve[] cvs = Intersect(thisBounds, other, otherBounds);
            for (int i = 0; i < cvs.Length; i++)
            {
                if (cvs[i].DistanceTo(seed) < Precision.eps) return cvs[i];
            }
            return null;
        }
        public override ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            GetExtremePositions(thisBounds, other, otherBounds, out List<Tuple<double, double, double, double>> extremePositions);
            IDualSurfaceCurve[] dsc = GetDualSurfaceCurves(thisBounds, other, otherBounds, null, extremePositions);
            if (dsc != null && dsc.Length > 0)
            {
                ICurve[] res = new ICurve[dsc.Length];
                for (int i = 0; i < dsc.Length; i++)
                {
                    res[i] = dsc[i].Curve3D;
                }
                return res;
            }
            return BoxedSurfaceEx.Intersect(thisBounds, other, otherBounds, null, extremePositions); // allgemeine Lösung
        }
        public override IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> extremePositions)
        {   // hier sollten die Schnitte mit Ebene, Cylinder, Kegel und Kugel gelöst werden
            // mit höheren Flächen sollte bei diesen (also z.B. Torus) implementiert werden
            if (other is PlaneSurface)
            {   // das Ergebnis sollte aus einer möglichst nicht geschlossenen Kurve bestehen
                // selbst, wenn in den Bounds zwei Stücke entstehen.
                GeoPoint2D center2d;
                double radius;
                Plane unitPlane = toUnit * (other as PlaneSurface).Plane;
                if (Geometry.IntersectSpherePlane(unitPlane, out center2d, out radius) && radius > Precision.eps)
                {
                    Ellipse elli = Ellipse.Construct();
                    elli.SetCirclePlaneCenterRadius(unitPlane, unitPlane.ToGlobal(center2d), radius);
                    elli.Modify(toSphere); // Schnittkreis oder Ellipse (bei elliptischer Kugel)
                    GeoPoint2D spos = PositionOf(elli.StartPoint);
                    // GeoPoint2D mpos = PositionOf(elli.PointAt(0.5));
                    if (thisBounds.ContainsPeriodic(spos, UPeriod, VPeriod))
                    {   // damit die Naht der Ellipse außerhalb des thisBounds liegt
                        elli.Modify(ModOp.Rotate(elli.Center, elli.Plane.Normal, SweepAngle.Opposite));
                    }

                    ICurve2D pc = this.GetProjectedCurve(elli, 0.0); // new ProjectedCurve(elli, this, true, thisBounds);
                    if (thisBounds.ContainsPeriodic(pc.StartPoint, UPeriod, VPeriod))
                    {   // das sollte somit nie drankommen, wile die Naht oben schon abgecheckt ist
                        if (pc.GetExtent() <= thisBounds)
                        {
                            // es ist ganz drinnen
                        }
                        else
                        {
                            Shapes.SimpleShape ss = new Shapes.SimpleShape(thisBounds);
                            double[] parts = ss.Clip(pc, true);
                            if (parts.Length > 2)
                            {
                                // von 0 bis 1. Wert ist innerhalb
                                GeoPoint ep = PointAt(pc.PointAt(parts[1]));
                                GeoPoint sp = PointAt(pc.PointAt(parts[2]));
                                elli.StartPoint = sp;
                                elli.SweepParameter = elli.SweepParameter / 2.0; // nur damit es ein Bogen wird
                                elli.EndPoint = ep;

                            }
                        }
                    }
                    else
                    {
                        Shapes.SimpleShape ss = new Shapes.SimpleShape(thisBounds);
                        double[] parts = ss.Clip(pc, true);
                        if (parts.Length > 1)
                        {
                            // von 0 bis 1. Wert ist innerhalb
                            GeoPoint ep = PointAt(pc.PointAt(parts[parts.Length - 1]));
                            GeoPoint sp = PointAt(pc.PointAt(parts[0]));
                            elli.StartPoint = sp;
                            elli.SweepParameter = elli.SweepParameter / 2.0; // nur damit es ein Bogen wird
                            elli.EndPoint = ep;

                        }
                    }
                    pc = this.GetProjectedCurve(elli, 0.0); // new ProjectedCurve(elli, this, true, thisBounds);
                    ICurve2D opc = other.GetProjectedCurve(elli, 0.0);
                    return new IDualSurfaceCurve[] { new DualSurfaceCurve(elli, this, pc, other, opc) };
                }
                return new IDualSurfaceCurve[0];
            }
            else if (other is ICylinder cyl) 
            { 
                if (cyl.Axis.Distance(this.Location)<Precision.eps && Math.Abs(cyl.Radius-this.RadiusX)<Precision.eps) 
                {   // this sphere is touching the cylinder:
                    Plane pln = new Plane(Location, cyl.Axis.Direction); // the plane through center of this sphere, perpendicular to the cylinder axis
                    IDualSurfaceCurve[] cylcrv = other.GetPlaneIntersection(new PlaneSurface(pln), otherBounds.Left, otherBounds.Right, otherBounds.Bottom, otherBounds.Top, 0.0);
                    if (cylcrv!=null && cylcrv.Length==1) 
                    { 
                        if (cylcrv[0].Curve3D is Ellipse elli)
                        {
                            ICurve2D pc = this.GetProjectedCurve(elli, 0.0);
                            return new IDualSurfaceCurve[] { new DualSurfaceCurve(elli, this, pc, other, cylcrv[0].Curve2D1) };
                        }
                    }
                }
            }
            return base.GetDualSurfaceCurves(thisBounds, other, otherBounds, seeds, extremePositions);
        }
        public override GeoPoint2D[] PerpendicularFoot(GeoPoint fromHere)
        {
            GeoVector dir = toUnit * (Location - fromHere);
            dir.Norm();
            GeoPoint2D[] ip = new GeoPoint2D[2];
            ip[0] = PositionOfUnit(GeoPoint.Origin + dir);
            ip[1] = PositionOfUnit(GeoPoint.Origin - dir);
            return ip;
        }
        public override ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {
            if (curve is Ellipse elli)
            {
                // two special kinds of ellipses: longitudinal and latitudinal  circles
                GeoPoint2D sp = PositionOf(elli.StartPoint);
                GeoPoint2D ep = PositionOf(elli.EndPoint);
                if (Math.Abs(Math.Abs(sp.y) - Math.PI / 2) < 1e-5)
                {   // when a pole point is projected, the x value is arbitrary. We set it to the x-value of the other point to make a straight line
                    sp.x = ep.x;
                }
                if (Math.Abs(Math.Abs(ep.y) - Math.PI / 2) < 1e-5)
                {   // when a pole point is projected, the x value is arbitrary. We set it to the x-value of the other point to make a straight line
                    ep.x = sp.x;
                }
                if (Math.Abs(sp.x - ep.x) > Math.PI * 2 - 1e-6)
                {   // an arc on the seam
                    sp.x = ep.x;
                }
                if (Precision.SameDirection(elli.Normal, this.ZAxis, false))
                {
                    // a longitudinal circle, i.e. a horizontal line in uv
                    if (!elli.IsArc || (sp | ep) < 1e-6)
                    {
                        ep.x = sp.x + Math.PI * 2;
                        if (UDirection(sp) * elli.StartDirection < 0)
                        {
                            double tmp = ep.x;
                            ep.x = sp.x;
                            sp.x = tmp;
                        }
                    }
                    else
                    {
                        if (ep.x > sp.x)
                        {
                            if (UDirection(sp) * elli.StartDirection < 0)
                            {
                                ep.x -= Math.PI * 2;
                            }
                        }
                        else
                        {
                            if (UDirection(sp) * elli.StartDirection > 0)
                            {
                                ep.x += Math.PI * 2;
                            }
                        }
                    }
                    return new Line2D(sp, ep);
                }
                else if (Precision.IsPerpendicular(elli.Normal, ZAxis, false) && Precision.IsEqual(elli.Center, Location))
                {
                    // a latitudinal circle
                    // the u-parameter (x) of sp and ep may be unprecise, when the point is close to the pole
                    // the u-parameters of sp and ep must be identical
                    // since v (y) is in the range -pi/2 .. pi/2 we use the point which is closer to the "equator"
                    if (Math.Abs(sp.y) < Math.Abs(ep.y)) ep.x = sp.x;
                    else sp.x = ep.x;
                    if (!elli.IsArc || (sp | ep) < 1e-6)
                    {
                        ep.y = sp.y + Math.PI * 2;
                    }
                    if (ep.y > sp.y)
                    {
                        if (VDirection(sp) * elli.StartDirection < 0)
                        {
                            ep.y += Math.PI * 2;
                        }
                    }
                    else
                    {
                        if (VDirection(sp) * elli.StartDirection > 0)
                        {
                            ep.y += Math.PI * 2;
                        }
                    }
                    return new Line2D(sp, ep);
                }
                if (elli.IsCircle && this.IsRealSphere) // it is not a longitudinal or latitudial circle
                {
                    if (Geometry.DistPL(elli.Center, this.Location, elli.Plane.Normal) < Precision.eps)
                    {   // this is a circle on the sphere - or above or below the sphere, but parallel
                        if (Math.Abs(GetDistance(elli.StartPoint)) > Precision.eps)
                        {   // the circle hovers above or below the sphere, but can be projected onto the sphere
                            GeoVector ctoc = elli.StartPoint - Location;
                            ctoc.Length = RadiusX;
                            GeoPoint c = Geometry.DropPL(Location + ctoc, Location, elli.Normal);
                            Ellipse clone = elli.Clone() as Ellipse;
                            clone.Center = c;
                            clone.Radius = (Location + ctoc) | c;
                            curve = clone;
                        }
                        return new ProjectedCurve(curve, this, true, this.usedArea);
                    }
                }
            }
            return base.GetProjectedCurve(curve, precision);
        }
        public override ISurface GetOffsetSurface(double offset)
        {
            if (Math.Abs(RadiusX - RadiusY) < Precision.eps && Math.Abs(RadiusY - RadiusZ) < Precision.eps)
            {
                if (toSphere.Determinant > 0)
                {   // outward oriented surface
                    if (offset < 0 && -offset > RadiusX)
                    {   // reversing the orientation
                        GeoVector xx = (offset + RadiusX) * XAxis.Normalized;
                        GeoVector yy = (offset + RadiusY) * YAxis.Normalized;
                        GeoVector zz = (offset + RadiusZ) * ZAxis.Normalized;
                        return new SphericalSurface(this.Location, xx, yy, zz);
                    }
                    else
                    {
                        GeoVector xx = (offset + RadiusX) * XAxis.Normalized;
                        GeoVector yy = (offset + RadiusY) * YAxis.Normalized;
                        GeoVector zz = (offset + RadiusZ) * ZAxis.Normalized;
                        return new SphericalSurface(this.Location, xx, yy, zz);
                    }
                }
                else
                {
                    GeoVector xx = (RadiusX - offset) * XAxis.Normalized;
                    GeoVector yy = (RadiusY - offset) * YAxis.Normalized;
                    GeoVector zz = (RadiusZ - offset) * ZAxis.Normalized;
                    return new SphericalSurface(this.Location, xx, yy, zz);
                }
            }
            else return base.GetOffsetSurface(offset);

        }
        public override int GetExtremePositions(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, out List<Tuple<double, double, double, double>> extremePositions)
        {
            switch (other)
            {
                case PlaneSurface _:
                case CylindricalSurface _:
                case ConicalSurface _:
                    {
                        int res = other.GetExtremePositions(otherBounds, this, thisBounds, out extremePositions);
                        for (int i = 0; i < extremePositions.Count; i++)
                        {
                            extremePositions[i] = new Tuple<double, double, double, double>(extremePositions[i].Item3, extremePositions[i].Item4, extremePositions[i].Item1, extremePositions[i].Item2);
                        }
                        return res;
                    }
                case SphericalSurface ss:
                    {
                        GeoVector connect = ss.Location - Location;
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        if (!connect.IsNullVector())
                        {
                            GeoPoint2D[] ips = GetLineIntersection(Location, connect);
                            for (int i = 0; i < ips.Length; i++)
                            {
                                SurfaceHelper.AdjustPeriodic(this, thisBounds, ref ips[i]);
                                if (thisBounds.Contains(ips[i])) extremePositions.Add(new Tuple<double, double, double, double>(ips[i].x, ips[i].y, double.NaN, double.NaN));
                            }
                            ips = ss.GetLineIntersection(Location, connect);
                            for (int i = 0; i < ips.Length; i++)
                            {
                                SurfaceHelper.AdjustPeriodic(other, otherBounds, ref ips[i]);
                                if (otherBounds.Contains(ips[i])) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, ips[i].x, ips[i].y));
                            }
                        }
                        return extremePositions.Count;
                    }
            }
            extremePositions = null;
            return -1; // means: no implementation for this combination
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNonPeriodicSurface (Border)"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface GetNonPeriodicSurface(ICurve[] orientedCurves)
        {
            SphericalSurfaceNP res = new SphericalSurfaceNP(Location, RadiusX, toUnit.Determinant > 0, orientedCurves);
            return res;
        }
        #endregion
        #region ISerializable Members
        protected SphericalSurface(SerializationInfo info, StreamingContext context)
        {
            toSphere = (ModOp)info.GetValue("ToSphere", typeof(ModOp));
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ToSphere", toSphere, typeof(ModOp));
        }

        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            toUnit = toSphere.GetInverse();
        }
        #endregion
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            List<IPropertyEntry> se = new List<IPropertyEntry>();
            GeoPointProperty center = new GeoPointProperty("SphericalSurface.Center", frame, false);
            center.ReadOnly = true;
            center.OnGetValue = new EditableProperty<GeoPoint>.GetValueDelegate(delegate () { return toSphere * GeoPoint.Origin; });
            se.Add(center);
            DoubleProperty radius = new DoubleProperty("SphericalSurface.Radius", frame);
            radius.ReadOnly = true;
            radius.OnGetValue = new EditableProperty<double>.GetValueDelegate(delegate () { return (toSphere * GeoVector.XAxis).Length; });
            radius.Refresh();
            se.Add(radius);
            return new GroupProperty("SphericalSurface", se.ToArray());
        }
        public bool IsRealSphere
        {
            get
            {
                if (Math.Abs((toSphere * GeoVector.XAxis).Length - (toSphere * GeoVector.YAxis).Length) > Precision.eps) return false;
                if (Math.Abs((toSphere * GeoVector.XAxis).Length - (toSphere * GeoVector.ZAxis).Length) > Precision.eps) return false;
                if (Math.Abs((toSphere * GeoVector.ZAxis).Length - (toSphere * GeoVector.YAxis).Length) > Precision.eps) return false;
                return true;

            }
        }
        public bool OutwardOriented => toSphere.Determinant > 0;

        GeoPoint ISphere.Center { get => Location; set => this.Modify(ModOp.Translate(value - Location)); }

        bool ISurfaceWithRadius.IsModifiable => IsRealSphere; // can set the radius of a real sphere only

        double ISurfaceWithRadius.Radius { get => (toSphere * GeoVector.XAxis).Length; set => Modify(ModOp.Scale(Location, value / RadiusX)); }

        double GetRadius(DoubleProperty sender)
        {
            return (toSphere * GeoVector.XAxis).Length;
        }
        GeoPoint GetCenter(GeoPointProperty sender)
        {
            return toSphere * GeoPoint.Origin;
        }
        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            int ax = export.WriteAxis2Placement3d(Location, Axis, XAxis);
            int res = export.WriteDefinition("SPHERICAL_SURFACE('',#" + ax.ToString() + "," + export.ToString(RadiusX) + ")");
            if (toSphere.Determinant < 0) return -res; // the normal vector points to the inside, STEP only knows sperical surfaces with outside pointing normals, 
            // the sign of the result holds the orientation information
            else return res;

        }

    }
}
