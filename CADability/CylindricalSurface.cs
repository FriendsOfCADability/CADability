using CADability.Curve2D;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Wintellect.PowerCollections;

namespace CADability.GeoObject
{
    public interface ISurfaceWithRadius
    {
        bool IsModifiable { get; }
        double Radius { get; set; }

    }
    /// <summary>
    /// Interface to handle both CylindricalSurface and CylindricalSurfaceNP
    /// </summary>
    public interface ICylinder : ISurfaceWithRadius
    {
        Axis Axis { get; set; }
        bool OutwardOriented { get; }
    }
    /// <summary>
    /// A cylindrical surface which implements <see cref="ISurface"/>. The surface represents a circular or elliptical
    /// cylinder. The u parameter always describes a circle or ellipse, the v parameter a Line.
    /// </summary>
    [Serializable()]
    public class CylindricalSurface : ISurfaceImpl, ISurfaceOfRevolution, ISerializable, IDeserializationCallback, ISurfacePlaneIntersection,
        IExportStep, ISurfaceOfArcExtrusion, ICylinder, IJsonSerialize
    {
        // Der Zylinder ist so beschaffen, dass er lediglich durch eine ModOp definiert ist.
        // Der Einheitszylinder steht im Ursprung mit Radius 1, u beschreibt einen Kreis, v eine Mantellinie
        protected ModOp toCylinder; // diese ModOp modifiziert den Einheitszylinder in den konkreten Zylinder
        protected ModOp toUnit; // die inverse ModOp zum schnelleren Rechnen
        Polynom implicitPolynomial;
        /// <summary>
        /// Creates a cylindrical surface. The length of <paramref name="directionX"/> and <paramref name="directionY"/> specify the radius.
        /// The axis is perpendicular to <paramref name="directionX"/> and <paramref name="directionY"/> (right hand). The u parameter starts at
        /// <paramref name="location"/>+<paramref name="directionX"/>, the v parameter increments along the axis and is 0 at location.
        /// </summary>
        /// <param name="location"></param>
        /// <param name="directionX"></param>
        /// <param name="directionY"></param>
        public CylindricalSurface(GeoPoint location, GeoVector directionX, GeoVector directionY, GeoVector directionZ) : base()
        {
            // this may also be a left handed system
            ModOp m1 = ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis },
                new GeoVector[] { directionX, directionY, directionZ });
            ModOp m2 = ModOp.Translate(location.x, location.y, location.z);
            toCylinder = m2 * m1;
            toUnit = toCylinder.GetInverse();
        }
        internal CylindricalSurface(CylindricalSurface toClone) : base()
        {
            this.toCylinder = toClone.toCylinder;
            toUnit = toCylinder.GetInverse();
        }
        internal CylindricalSurface(ModOp toCylinder, BoundingRect? usedArea = null) : base(usedArea)
        {
            this.toCylinder = toCylinder;
            toUnit = toCylinder.GetInverse();
        }
        public override string ToString()
        {
            return "CylindricalSurface: " + "loc: " + Location.ToString() + "dirx: " + XAxis.ToString() + "diry: " + YAxis.ToString();
        }
        public double RadiusX
        {
            get
            {
                return (toCylinder * GeoVector.XAxis).Length;
            }
        }
        public double RadiusY
        {
            get
            {
                return (toCylinder * GeoVector.YAxis).Length;
            }
        }
        public GeoPoint Location
        {
            get
            {
                return toCylinder * GeoPoint.Origin;
            }
        }
        public GeoVector Axis
        {
            get
            {
                return toCylinder * GeoVector.ZAxis;
            }
        }
        public GeoVector XAxis
        {
            get
            {
                return toCylinder * GeoVector.XAxis;
            }
        }
        public GeoVector YAxis
        {
            get
            {
                return toCylinder * GeoVector.YAxis;
            }
        }
        public GeoVector ZAxis
        {
            get
            {
                return toCylinder * GeoVector.ZAxis;
            }
        }
        public Line AxisLine(double vmin, double vmax)
        {
            return Line.TwoPoints(toCylinder * new GeoPoint(0, 0, vmin), toCylinder * new GeoPoint(0, 0, vmax));
        }
        #region ISurfaceImpl Overrides
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            return new CylindricalSurface(m * toCylinder, usedArea);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            // GeoVector res = toCylinder * new GeoVector(Math.Cos(uv.x), Math.Sin(uv.x), 0.0);
            // obiges berücksichtigt wohl nicht die Orientierung, deshalb:
            GeoVector res = (toCylinder * new GeoVector(-Math.Sin(uv.x), Math.Cos(uv.x), 0.0)) ^ (toCylinder * GeoVector.ZAxis);
            res.Norm();
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
            if (Precision.SameDirection(dirunit, GeoVector.ZAxis, false))
            {
                return new ICurve2D[] { };
            }
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
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            return toCylinder * new GeoPoint(Math.Cos(uv.x), Math.Sin(uv.x), uv.y);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            return toCylinder * new GeoVector(-Math.Sin(uv.x), Math.Cos(uv.x), 0.0);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            return toCylinder * GeoVector.ZAxis;
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
            location = toCylinder * new GeoPoint(Math.Cos(uv.x), Math.Sin(uv.x), uv.y);
            du = toCylinder * new GeoVector(-Math.Sin(uv.x), Math.Cos(uv.x), 0.0);
            dv = toCylinder * GeoVector.ZAxis;
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
            location = toCylinder * new GeoPoint(Math.Cos(uv.x), Math.Sin(uv.x), uv.y);
            du = toCylinder * new GeoVector(-Math.Sin(uv.x), Math.Cos(uv.x), 0.0);
            dv = toCylinder * GeoVector.ZAxis;
            duu = toCylinder * new GeoVector(-Math.Cos(uv.x), -Math.Sin(uv.x), 0.0);
            dvv = GeoVector.NullVector;
            duv = GeoVector.NullVector;
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
                return false;
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
                return 0.0;
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
            if (Precision.IsNullVector(dir.To2D())) return new GeoPoint2D[0];
            GeoPoint2D[] ip = Geometry.IntersectLC(sp.To2D(), sp.To2D() + dir.To2D(), GeoPoint2D.Origin, 1.0);
            GeoPoint2D[] res = new GeoPoint2D[ip.Length];
            // gesucht sind uv Punkte
            for (int i = 0; i < res.Length; i++)
            {
                double l = Geometry.LinePar(sp.To2D(), dir.To2D(), ip[i]);
                double v = sp.z + l * dir.z;
                double u = Math.Atan2(ip[i].y, ip[i].x);
                if (u < 0.0) u += 2.0 * Math.PI;
                res[i] = new GeoPoint2D(u, v);
            }
            return res;
        }
        public override ICurve Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, GeoPoint seed)
        {   // this is a general implementation, which is good when Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds) is implemented. Should be used in base class
            ICurve[] res = Intersect(thisBounds, other, otherBounds);
            if (res != null)
            {
                double mindist = double.MaxValue;
                int found = -1;
                for (int i = 0; i < res.Length; i++)
                {
                    double dist = res[i].DistanceTo(seed);
                    if (dist < mindist)
                    {
                        mindist = dist;
                        found = i;
                    }
                }
                if (found >= 0) return res[found];
            }
            return base.Intersect(thisBounds, other, otherBounds, seed);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Intersect (BoundingRect, ISurface, BoundingRect)"/>
        /// </summary>
        /// <param name="thisBounds"></param>
        /// <param name="other"></param>
        /// <param name="otherBounds"></param>
        /// <returns></returns>
        public override ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            if (other is PlaneSurface)
            {
                return other.Intersect(otherBounds, this, thisBounds);
            }
            if (other is CylindricalSurface)
            {
                CylindricalSurface cyl2 = other as CylindricalSurface;
                if (Precision.SameDirection(Axis, cyl2.Axis, false))
                {
                    GeoPoint2D c2 = (toUnit * cyl2.Location).To2D();
                    GeoVector2D dx2 = (toUnit * cyl2.XAxis).To2D();
                    GeoVector2D dy2 = (toUnit * cyl2.YAxis).To2D();
                    GeoPoint2D[] pnts = Geometry.IntersectEC(c2, dx2.Length, dy2.Length, dx2.Angle, GeoPoint2D.Origin, 1.0);
                    List<ICurve> res = new List<ICurve>();
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        // SurfaceHelper.AdjustPeriodic(this, thisBounds, ref pnts[i]);
                        GeoPoint sp = toCylinder * new GeoPoint(pnts[i].x, pnts[i].y, thisBounds.Bottom);
                        GeoPoint ep = toCylinder * new GeoPoint(pnts[i].x, pnts[i].y, thisBounds.Top);
                        Line line = Line.Construct();
                        line.SetTwoPoints(sp, ep);
                        res.Add(line);
                    }
                    return res.ToArray();
                }
                else
                {   // ein paar spezielle Lösungen (mit Ellipsen als Ergebnis) abfangen. BoxedSurfaceEx.Intersect ist aber auch gut!
                    
                    double dpar1, dpar2;
                    double adist = Geometry.DistLL(this.Location, this.Axis, cyl2.Location, cyl2.Axis, out dpar1, out dpar2);
                    if (adist < Precision.eps)
                    {

                    }
                    GetExtremePositions(thisBounds, other, otherBounds, out List<Tuple<double, double, double, double>> extremePositions);
                    if (usedArea.IsInfinite || double.IsInfinity(usedArea.Size)) { usedArea = thisBounds; }
                    ICurve[] res = BoxedSurfaceEx.Intersect(thisBounds, other, otherBounds, null, extremePositions);
                    return res;
                    
                    //Unreachable code
                    /*
                    InterpolatedDualSurfaceCurve.SurfacePoint[] basePoints = new InterpolatedDualSurfaceCurve.SurfacePoint[5];
                    // wir brauchen 4 Punkte (der 5. ist der 1.)
                    // zwei Ebenen sind gegeben durch die Achse eines Zylinder und der Senkrechten auf beide Achsen, dgl. mit dem anderen Zylinder
                    // in jeder Ebene gibt es 2 Schnittpunkte (es kann auch 4 geben - dünner mit dickem Zylinder - noch implementieren)
                    double par1, par2;
                    double dist = Geometry.DistLL(this.Location, this.Axis, cyl2.Location, cyl2.Axis, out par1, out par2);
                    if (dist > Precision.eps)
                    {
                        GeoPoint p1 = this.Location + par1 * this.Axis;
                        GeoPoint p2 = cyl2.Location + par2 * cyl2.Axis;
                        GeoVector nrm = this.Axis ^ cyl2.Axis;
                        Plane pln = new Plane(p1, p2 - p1, this.Axis);
                        GeoPoint[] ip;
                        GeoPoint2D[] uv1;
                        GeoPoint2D[] uv2;
                        Surfaces.PlaneIntersection(pln, this, cyl2, out ip, out uv1, out uv2);
                        if (ip.Length == 2)
                        {
                            basePoints[0] = new InterpolatedDualSurfaceCurve.SurfacePoint(ip[0], uv1[0], uv2[0]);
                            basePoints[2] = new InterpolatedDualSurfaceCurve.SurfacePoint(ip[1], uv1[1], uv2[1]);
                            basePoints[4] = basePoints[0]; // Kurve ist geschlossen
                            pln = new Plane(p1, p2 - p1, cyl2.Axis);
                            Surfaces.PlaneIntersection(pln, this, cyl2, out ip, out uv1, out uv2);
                            if (ip.Length == 2)
                            {
                                basePoints[1] = new InterpolatedDualSurfaceCurve.SurfacePoint(ip[0], uv1[0], uv2[0]);
                                basePoints[3] = new InterpolatedDualSurfaceCurve.SurfacePoint(ip[1], uv1[1], uv2[1]);
                                InterpolatedDualSurfaceCurve idsc = new InterpolatedDualSurfaceCurve(this, other, basePoints);
                                return new ICurve[] { idsc };
                            }
                        }
                    }
                    */
                }
            }
            if (other is SphericalSurface)
            {
                SphericalSurface sph = other as SphericalSurface;
                if (sph.IsRealSphere && this.IsRealCylinder)
                {
                    GeoPoint cnt = toUnit * sph.Location;
                    if (Math.Abs(cnt.x) < RadiusX * Precision.eps && Math.Abs(cnt.y) < RadiusX * Precision.eps)
                    {
                        // konzentrische Kugel
                        if (Math.Abs(sph.RadiusX - RadiusX) < Precision.eps)
                        {
                            // konzentrisch mit gleichem Radius: Berührung
                            Ellipse res = Ellipse.Construct();
                            res.SetArcPlaneCenterRadiusAngles(new Plane(Plane.XYPlane, cnt.z), cnt, 1, thisBounds.Left, thisBounds.Right - thisBounds.Left);
                            res.Modify(toCylinder);
                            return new ICurve[] { res };
                        }
                    }
                }
            }
            if (other is ToroidalSurface ts)
            {
                if (this.IsRealCylinder)
                {
                    GeoPoint cnt = toUnit * ts.Location;
                    if (Math.Abs(cnt.x) < RadiusX * Precision.eps && Math.Abs(cnt.y) < RadiusX * Precision.eps)
                    {
                        // concentric
                        if (Precision.SameDirection(this.ZAxis, ts.ZAxis, false))
                        {   // same or opposite orientation should result in one or two circles
                            if ((Math.Abs(ts.XAxis.Length + ts.MinorRadius - RadiusX) < Precision.eps) || // concentric, torus fits exactly inside cylinder
                                (Math.Abs(ts.XAxis.Length - ts.MinorRadius - RadiusX) < Precision.eps)) // concentric, cylinder fits exactly inside torus 
                            {
                                GeoPoint c = toUnit * ts.Location;
                                return new ICurve[] { this.FixedV(c.z, thisBounds.Left, thisBounds.Right) };
                            }
                            else if ((ts.XAxis.Length + ts.MinorRadius > RadiusX) && (ts.XAxis.Length - ts.MinorRadius < RadiusX))
                            {   // two circles
                                GeoPoint2D[] ips = ts.GetLineIntersection(this.PointAt(thisBounds.GetLowerMiddle()), this.ZAxis);
                                if (ips.Length == 2)
                                {
                                    GeoPoint c1 = toUnit * ts.PointAt(ips[0]);
                                    GeoPoint c2 = toUnit * ts.PointAt(ips[1]);
                                    return new ICurve[] { this.FixedV(c1.z, thisBounds.Left, thisBounds.Right), this.FixedV(c2.z, thisBounds.Left, thisBounds.Right) };
                                }
                            }
                        }
                    }
                    else if (Precision.IsPerpendicular(ts.ZAxis, ZAxis, false) && Geometry.CommonPlane(Location, ZAxis, ts.Location, ts.ZAxis ^ ZAxis, out Plane pln))
                    {   // the axis of the cylinder lies in the plane of the torus (axis-circle plane)
                        Ellipse circle = (ts as ISurfaceOfExtrusion).Axis(otherBounds) as Ellipse;
                        if (circle != null) // must be the case
                        {
                            GeoPoint pOnAxis = Geometry.DropPL(ts.Location, Location, ZAxis);
                            if (Math.Abs(RadiusX - ts.MinorRadius) < Precision.eps && Math.Abs((pOnAxis | circle.Center) - circle.Radius) < Precision.eps)
                            {
                                // the cylinder is tangential to the torus
                                GeoPoint c = toUnit * pOnAxis;
                                double v = c.z;
                                return new ICurve[] { FixedV(v, thisBounds.Left, thisBounds.Right) };
                            }

                        }
                    }
                }

            }
            {
                GetExtremePositions(thisBounds, other, otherBounds, out List<Tuple<double, double, double, double>> extremePositions);
                return BoxedSurfaceEx.Intersect(thisBounds, other, otherBounds, null, extremePositions); // allgemeine Lösung
            }
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
                List<GeoPoint2D> luvOnSurface = new List<GeoPoint2D>();
                List<double> luOnCurve = new List<double>();
                GeoPoint2D[] ip = GetLineIntersection(curve.StartPoint, curve.StartDirection);
                for (int i = 0; i < ip.Length; ++i)
                {
                    double par = curve.PositionOf(PointAt(ip[i]));
                    if (par >= -1e-6 && par <= 1.0 + 1e-6)
                    {
                        SurfaceHelper.AdjustPeriodic(this, uvExtent, ref ip[i]);
                        luOnCurve.Add(par);
                        luvOnSurface.Add(ip[i]);
                    }
                }
                uvOnFaces = luvOnSurface.ToArray();
                uOnCurve3Ds = luOnCurve.ToArray();
                ips = new GeoPoint[uvOnFaces.Length];
                for (int i = 0; i < ips.Length; ++i)
                {
                    ips[i] = PointAt(uvOnFaces[i]);
                }
                return;
            }
            if (curve.GetPlanarState() == PlanarState.Planar)
            {
                Plane pln = curve.GetPlane();
                BoundingCube ext = curve.GetExtent();
                ext.Modify(toUnit); // macht ihn ggf. zu groß, curve.CloneModified(toUnit).GetExtent() geht aber manchmal nicht und dauert zu lange
                IDualSurfaceCurve[] dsc = GetPlaneIntersection(new PlaneSurface(pln), 0.0, Math.PI * 2.0, ext.Zmin, ext.Zmax, 0.0);
                // liefert eine Ellipse oder zwei Linien oder nichts
                List<GeoPoint2D> luvOnSurface = new List<GeoPoint2D>();
                List<double> luOnCurve = new List<double>();
                for (int i = 0; i < dsc.Length; ++i)
                {
                    double[] ip = Curves.Intersect(curve, dsc[i].Curve3D, true);
                    for (int j = 0; j < ip.Length; ++j)
                    {
                        luOnCurve.Add(ip[j]);
                        luvOnSurface.Add(PositionOf(curve.PointAt(ip[j])));
                    }
                }
                uvOnFaces = luvOnSurface.ToArray();
                uOnCurve3Ds = luOnCurve.ToArray();
                ips = new GeoPoint[uvOnFaces.Length];
                for (int i = 0; i < ips.Length; ++i)
                {
                    ips[i] = PointAt(uvOnFaces[i]);
                    SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uvOnFaces[i]);
                }
                return;
            }
            if (curve is BSpline)
            {   // ein projizierter BSpline hat das selbe Parameterverhalten wie der original BSpline
                BSpline2D bsp2d = curve.CloneModified(toUnit).GetProjectedCurve(Plane.XYPlane) as BSpline2D;
                if (bsp2d != null)
                {
                    GeoPoint2DWithParameter[] ips2d = bsp2d.Intersect(new Circle2D(GeoPoint2D.Origin, 1.0));
                    ips = new GeoPoint[ips2d.Length];
                    uvOnFaces = new GeoPoint2D[ips2d.Length];
                    uOnCurve3Ds = new double[ips2d.Length];
                    for (int i = 0; i < ips2d.Length; i++)
                    {
                        uOnCurve3Ds[i] = ips2d[i].par1;
                        ips[i] = curve.PointAt(ips2d[i].par1);
                        uvOnFaces[i] = PositionOf(ips[i]);
                        SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uvOnFaces[i]);
                    }
                    return;
                }
            }
            base.Intersect(curve, uvExtent, out ips, out uvOnFaces, out uOnCurve3Ds);
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
            return Precision.SameDirection(toUnit * p.Direction, GeoVector.ZAxis, false);
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
                if (pc.Surface is CylindricalSurface)
                {
                    BoundingRect otherBounds = new BoundingRect(PositionOf(pc.Surface.PointAt(pc.StartPoint)), PositionOf(pc.Surface.PointAt(pc.EndPoint)));
                    if (pc.Surface.SameGeometry(pc.GetExtent(), this, otherBounds, Precision.eps, out ModOp2D notneeded))
                    {
                        return pc.Curve3DFromParams; // if trimmed or reversed still returns the correct 3d curve (but trimmed and/or reversed)
                    }
                }
            }
            // wenn es eine Linie ist, dann kommt entweder eine Linie (v-Richtung) oder eine Ellipse (u-Richtung)
            // oder eine Schraubenlinie raus
            // das besondere wäre noch ein Sinus, der macht nämlich eine Ellipse, aber das geht besser so:
            // Es gibt eine besondere 2D Kurve, die ist Parameterkurve auf einer Fläche geschnitten mit 
            // einer anderen Fläche. Und wenn die andere Fläche eine Ebene ist, dann gibts eine Ellipse
            // 
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
                    // Kreis(bogen) bzw. Ellipsenbogen
                    Ellipse res = Ellipse.Construct();
                    res.SetArcPlaneCenterRadius(new Plane(Plane.StandardPlane.XYPlane, l2d.StartPoint.y), new GeoPoint(0.0, 0.0, l2d.StartPoint.y), 1.0);
                    res.StartPoint = new GeoPoint(Math.Cos(l2d.StartPoint.x), Math.Sin(l2d.StartPoint.x), l2d.StartPoint.y);
                    // res.StartPoint = new GeoPoint(Math.Cos(l2d.StartPoint.x), Math.Sin(l2d.StartPoint.x), l2d.StartPoint.y);
                    res.StartParameter = l2d.StartPoint.x;
                    res.SweepParameter = l2d.EndPoint.x - l2d.StartPoint.x;
                    res.Modify(toCylinder);
                    return res;
                    //// DEBUG
                    //if (toCylinder.Determinant > 0.0) return res;
                    //else
                    //{
                    //    Line l = Line.Construct();
                    //    l.StartPoint = PointAt(l2d.StartPoint);
                    //    l.EndPoint = PointAt(l2d.EndPoint);
                    //    return l;
                    //}
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
            // hier könnte man die oben beschriebene 2D Schnittkure erzeugen
            /*
             * Schnitt Ebene Zyliner ist auf dem Zylinsermantel eine Sinuskurve
             * Die Kurve könnte allerdings auch als NURBS darstellbar sein, nur wie?
             * Als Sinuskurve sieht sie so aus: u = t, v = v0 + v1*sin(t+v2), also 3 Unbekannte mit sin(a+b)==sina*cosb+cosa*sinb
             * ergibt sich v = v0 + v1*(sin(t)*cos(v2)+cos(t)*sin(v2))
             * v2 sollte leicht zu bestimmen sein: pi/2 ist die Stelle, wo die Kurve am höchsten ist.
             * Eine Möglichkeit für die Schnittbestimmung wäre die methode mit den Dreieckshüllen vom NURBS
             * zu verallgemeinern. Die Wendepunkte sind hier ja bekannt.
             * Beim Schnitt Ebene/Kegel ist es wohl kein Sinus mehr, die Wendepunkte könnten aber trotzdem bestimmbar sein.
             * Und beim Torus???
             * 
             * Zunächst mal NURBS 2d (und 3d) aus Punkten (und Tangenten) bestimmbar machen. Damit kann man grob rechnen...
             */
            // umin, umax, vmin, vmax sind die Grenzen des Cylinders, nicht der Ebene!
            Plane pln = new Plane(toUnit * pl.Location, toUnit * pl.DirectionX, toUnit * pl.DirectionY);
            if (Precision.IsPerpendicular(pln.Normal, GeoVector.ZAxis, true))
            {
                // parallel zur ZAchse
                // return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
                // die Ebene pl erscheint in der XY-Ebene als Linie
                GeoVector dir = pln.Normal ^ GeoVector.ZAxis; // Richtung der Linie in der XY Ebene
                GeoVector2D ldir = new GeoVector2D(dir.x, dir.y);
                GeoPoint2D lstart = new GeoPoint2D(pln.Location.x, pln.Location.y); // Startpunkt der Linie in der XY Ebene
                GeoPoint2D[] ip = Geometry.IntersectLC(lstart, ldir, GeoPoint2D.Origin, 1.0);
                List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                for (int i = 0; i < ip.Length; ++i)
                {
                    Line l3d = Line.Construct();
                    l3d.StartPoint = toCylinder * (new GeoPoint(ip[i].x, ip[i].y, 0.0) + vmin * GeoVector.ZAxis);
                    l3d.EndPoint = toCylinder * (new GeoPoint(ip[i].x, ip[i].y, 0.0) + vmax * GeoVector.ZAxis);
                    double u = Math.Atan2(ip[i].y, ip[i].x);
                    Line2D cl2d = new Line2D(new GeoPoint2D(u, vmin), new GeoPoint2D(u, vmax));
                    SurfaceHelper.AdjustPeriodic(this, new BoundingRect(umin, vmin, umax, vmax), cl2d);
                    Line2D pl2d = new Line2D(pl.PositionOf(l3d.StartPoint), pl.PositionOf(l3d.EndPoint));
                    DualSurfaceCurve dsc = new DualSurfaceCurve(l3d, this, cl2d, pl, pl2d);
                    res.Add(dsc);
                }
                return res.ToArray();
            }
            else
            {
                // pln ist ja die (schräge) Ebene im Einheitssystem des Zylinders
                GeoPoint cnt = pln.Intersect(GeoPoint.Origin, GeoVector.ZAxis);
                // die Hauptachse der entstehenden Ellipse ist die Projektion der ZAchse auf die Ebene
                Ellipse elli = Ellipse.Construct();
                if (Precision.SameDirection(pln.Normal, GeoVector.ZAxis, true))
                {   // waagrechte Ebene gibt ein Kreis
                    elli.SetEllipseArcCenterAxis(toCylinder * cnt, XAxis, YAxis, umin, umax - umin);
                }
                else
                {
                    GeoPoint2D center;
                    GeoVector2D majdir, mindir;
                    Geometry.IntersectCylinderPlane(pln, out center, out majdir, out mindir);
                    //GeoPoint majaxpnt = pln.Intersect(GeoPoint.Origin + GeoVector.XAxis, GeoVector.ZAxis);
                    //GeoPoint minaxpnt = pln.Intersect(GeoPoint.Origin + GeoVector.YAxis, GeoVector.ZAxis);
                    GeoPoint startPoint = pln.Intersect(new GeoPoint(Math.Cos(umin), Math.Sin(umin)), GeoVector.ZAxis);
                    GeoPoint endPoint = pln.Intersect(new GeoPoint(Math.Cos(umax), Math.Sin(umax)), GeoVector.ZAxis);
                    cnt = toCylinder * pln.ToGlobal(center);
                    GeoVector majax = toCylinder * pln.ToGlobal(majdir);
                    GeoVector minax = toCylinder * pln.ToGlobal(mindir);
                    elli.SetEllipseArcCenterAxis(cnt, majax, minax, 0, 2 * Math.PI); // damit es ein Bogen ist
                    if (umin != 0.0 || umax != Math.PI * 2)
                    {   // ansonsten Vollkreis/-Ellipse
                        elli.StartPoint = toCylinder * startPoint;
                        double a = elli.ParameterOf(toCylinder * endPoint);
                        SweepAngle sw = new SweepAngle(elli.StartParameter, a, elli.SweepParameter > 0);
                        elli.SweepParameter = sw;
                        // statt: elli.EndPoint = toCylinder * endPoint;, das geht nicht bei Vollellipsen
                    }
                }

                //elli.SweepParameter = Math.PI * 2.0; // das folgende setzt sweepparameter nicht, deshalb hier
                //elli.SetEllipseCenterAxis(toCylinder * cnt, toCylinder * majax, toCylinder * minax);
                // Ellipse2D elli2d = Geometry.Ellipse2P2T(p1OnPl, p2OnPl, p2OnPl - centerOnPl, p1OnPl - centerOnPl);
                ICurve2D elli2d = pl.GetProjectedCurve(elli, 0.0); // das kann ein EllipsenBogen oder ein Kreisbogen sein
                // ICurve2D c2dpl = elli2d.Trim(0.0, 1.0); // damit wird sie zum Bogen
                ICurve2D c2d; // the 2d curve on the cylinder
                if (Precision.SameDirection(pln.Normal, GeoVector.ZAxis, true))
                {
                    GeoPoint2D p = PositionOf(elli.Center + elli.MajorAxis);
                    c2d = new Line2D(new GeoPoint2D(umin, p.y), new GeoPoint2D(umax, p.y));
                }
                else
                {
                    int n = Math.Max(3, (int)((umax - umin) / (Math.PI * 2) * 500));
                    double u1 = PositionOf(elli.Center + elli.MajorAxis).x;
                    double u2 = PositionOf(elli.Center - elli.MajorAxis).x;
                    double u3 = PositionOf(elli.Center + elli.MinorAxis).x;
                    double u4 = PositionOf(elli.Center - elli.MinorAxis).x;
                    while (u1 < umin) u1 += Math.PI;
                    while (u1 > umax) u1 -= Math.PI;
                    while (u2 < umin) u2 += Math.PI;
                    while (u2 > umax) u2 -= Math.PI;
                    while (u3 < umin) u3 += Math.PI;
                    while (u3 > umax) u3 -= Math.PI;
                    while (u4 < umin) u4 += Math.PI;
                    while (u4 > umax) u4 -= Math.PI;
                    List<double> uvals = new List<double>(n + 4);
                    double du = (umax - umin) / (n - 1);
                    for (int i = 0; i < n; i++)
                    {
                        uvals.Add(umin + i * du);
                    }
                    if (u1 > umin && u1 < umax) uvals.Add(u1);
                    if (u2 > umin && u2 < umax) uvals.Add(u2);
                    if (u3 > umin && u3 < umax) uvals.Add(u3);
                    if (u4 > umin && u4 < umax) uvals.Add(u4);
                    uvals.Sort();
                    for (int i = 1; i < uvals.Count; i++)
                    {
                        if (uvals[i] - uvals[i - 1] < 1e-3)
                        {
                            uvals.RemoveAt(i);
                            --i;
                        }
                    }
                    if (uvals.Count > 2)
                    {
                        GeoPoint2D[] pnts = new GeoPoint2D[uvals.Count];
                        for (int i = 0; i < pnts.Length; ++i)
                        {
                            pnts[i].x = uvals[i];
                            GeoPoint b = new GeoPoint(Math.Cos(pnts[i].x), Math.Sin(pnts[i].x), 0.0);
                            GeoPoint z = pln.Intersect(b, GeoVector.ZAxis);
                            pnts[i].y = z.z;
                        }
                        c2d = new BSpline2D(pnts, 3, false);

                        GeoPoint2D ps0 = PositionOf(elli.Center + elli.MajorAxis);
                        GeoPoint2D ps1 = PositionOf(elli.Center - elli.MajorAxis);
                        ps1.x = ps0.x + Math.PI;
                        GeoPoint2D ps2 = new GeoPoint2D(ps1.x + Math.PI / 2, (ps0.y + ps1.y) / 2);
                        GeoPoint2D pu0 = new GeoPoint2D(Math.PI / 2, 1);
                        GeoPoint2D pu1 = new GeoPoint2D(3 * Math.PI / 2, -1);
                        GeoPoint2D pu2 = new GeoPoint2D(2 * Math.PI, 0);
                        ModOp2D fromUnit = ModOp2D.Fit(new GeoPoint2D[] { pu0, pu1, pu2 }, new GeoPoint2D[] { ps0, ps1, ps2 }, true);
                        ModOp2D toUnit = fromUnit.GetInverse();
                        double ustart = (toUnit * new GeoPoint2D(umin, 0)).x;
                        double uend = (toUnit * new GeoPoint2D(umax, 0)).x;
                        if (uend < ustart) uend += Math.PI * 2;
                        // ustart + 0 * udiff == umin -> ustart = umin
                        // umin + 1 * udiff = umax -> 
                        SineCurve2D sc2d = new SineCurve2D(ustart, uend - ustart, fromUnit);
                        c2d = sc2d;
                    }
                    else c2d = null;
                }
                if (c2d != null)
                {
                    SurfaceHelper.AdjustPeriodic(this, new BoundingRect(umin, vmin, umax, vmax), c2d);
                    DualSurfaceCurve dsc = new DualSurfaceCurve(elli, this, c2d, pl, elli2d);
                    return new IDualSurfaceCurve[] { dsc };
                }
                else return new IDualSurfaceCurve[0];
            }
            // return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
        }
        ICurve2D[] ISurfacePlaneIntersection.GetPlaneIntersection(Plane plane, double umin, double umax, double vmin, double vmax)
        {
            Plane pln = new Plane(toUnit * plane.Location, toUnit * plane.DirectionX, toUnit * plane.DirectionY);
            if (Precision.IsPerpendicular(pln.Normal, GeoVector.ZAxis, true))
            {
                // parallel zur ZAchse
                // return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
                // die Ebene pl erscheint in der XY-Ebene als Linie
                GeoVector dir = pln.Normal ^ GeoVector.ZAxis; // Richtung der Linie in der XY Ebene
                GeoVector2D ldir = new GeoVector2D(dir.x, dir.y);
                GeoPoint2D lstart = new GeoPoint2D(pln.Location.x, pln.Location.y); // Startpunkt der Linie in der XY Ebene
                GeoPoint2D[] ip = Geometry.IntersectLC(lstart, ldir, GeoPoint2D.Origin, 1.0);
                List<ICurve2D> res = new List<ICurve2D>();
                for (int i = 0; i < ip.Length; ++i)
                {
                    GeoPoint sp = toCylinder * (new GeoPoint(ip[i].x, ip[i].y, 0.0) + vmin * GeoVector.ZAxis);
                    GeoPoint ep = toCylinder * (new GeoPoint(ip[i].x, ip[i].y, 0.0) + vmax * GeoVector.ZAxis);
                    Line2D pl2d = new Line2D(plane.Project(sp), plane.Project(ep));
                    res.Add(pl2d);
                }
                return res.ToArray();
            }
            else
            {
                GeoPoint cnt = pln.Intersect(GeoPoint.Origin, GeoVector.ZAxis);
                // die Hauptachse der entstehenden Ellipse ist die Projektion der ZAchse auf die Ebene
                GeoVector majax, minax;
                if (Precision.SameDirection(pln.Normal, GeoVector.ZAxis, true))
                {   // waagrechte Ebene gibt ein Kreis
                    majax = GeoVector.XAxis;
                    minax = GeoVector.YAxis;
                }
                else
                {
                    majax = pln.ToGlobal(pln.Project(GeoVector.ZAxis));
                    minax = majax ^ GeoVector.ZAxis;
                    double f = Math.Sqrt(majax.x * majax.x + majax.y * majax.y);
                    // die Länge der Projektion muss 1 sein
                    majax = (1.0 / f) * majax;
                    minax.Norm();
                }

                GeoPoint center = toCylinder * cnt;
                GeoVector majoraxis = toCylinder * majax;
                GeoVector minoraxis = toCylinder * minax;
                GeoPoint2D centerOnPl = plane.Project(center);
                GeoPoint2D p1OnPl = plane.Project(center + majoraxis);
                GeoPoint2D p2OnPl = plane.Project(center + minoraxis);
                Ellipse2D elli2d = Geometry.Ellipse2P2T(p1OnPl, p2OnPl, p2OnPl - centerOnPl, p1OnPl - centerOnPl);
                ICurve2D c2dpl = elli2d.Trim(0.0, 1.0); // damit wird sie zum Bogen
                return new ICurve2D[] { c2dpl };
            }
        }
        public override IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> extremePositions)
        {   // hier sollten die Schnitte mit Ebene und Cylinder gelöst werden
            // mit höheren Flächen sollte bei diesen (also z.B. Kugel) implementiert werden
            if (other is PlaneSurface)
            {
                return GetPlaneIntersection(other as PlaneSurface, thisBounds.Left, thisBounds.Right, thisBounds.Bottom, thisBounds.Top, Precision.eps);
            }
            else if (other is CylindricalSurface)
            {
                // Sonderfall: parallele Zylinder: zwei Linien
                CylindricalSurface cyl2 = other as CylindricalSurface;
                if (Precision.SameDirection(Axis, cyl2.Axis, false))
                {
                    GeoPoint cnt2 = toUnit * cyl2.Location;
                    GeoVector dirx2 = toUnit * cyl2.XAxis;
                    GeoVector diry2 = toUnit * cyl2.YAxis;
                    GeoPoint2D[] ips = Geometry.IntersectCC(GeoPoint2D.Origin, 1.0, cnt2.To2D(), dirx2.Length);
                    List<IDualSurfaceCurve> res = new List<CADability.IDualSurfaceCurve>();
                    if (ips.Length == 2)
                    {
                        int n = ips.Length;
                        if (ips[0] == ips[1]) n = 1;
                        for (int i = 0; i < n; i++)
                        {
                            double u = new Angle(ips[i].x, ips[i].y).Radian;
                            SurfaceHelper.AdjustUPeriodic(this, thisBounds, ref u);
                            if (u >= thisBounds.Left - 1e-10 && u <= thisBounds.Right + 1e-10)
                            {
                                Line2D l1 = new Line2D(new GeoPoint2D(u, thisBounds.Bottom), new GeoPoint2D(u, thisBounds.Top));
                                Line l = Line.TwoPoints(PointAt(l1.StartPoint), PointAt(l1.EndPoint));
                                Line2D l2 = new Curve2D.Line2D(cyl2.PositionOf(l.StartPoint), cyl2.PositionOf(l.EndPoint));
                                res.Add(new DualSurfaceCurve(l, this, l1, other, l2));
                            }
                        }
                    }
                    return res.ToArray();
                }
                else
                {
                    double par1, par2;
                    if (Geometry.DistLL(Location, ZAxis, cyl2.Location, cyl2.ZAxis, out par1, out par2) < Precision.eps && cyl2.IsRealCylinder && this.IsRealCylinder && Math.Abs(XAxis.Length - cyl2.XAxis.Length) < Precision.eps)
                    {
                        // Sonderfall: zwei Zylinder mit gleichem Durchmesser schneiden sich mit ihren Achsen, dann ist das Ergebnis eine Ellipse
                        GeoPoint ip = Location + par1 * ZAxis;
                        ip = cyl2.Location + par2 * cyl2.ZAxis;
                        GeoVector perp = RadiusX * (ZAxis ^ cyl2.ZAxis).Normalized;
                        GeoPoint top = ip + perp;
                        GeoPoint bottom = ip - perp;
                        Plane pln = new Plane(ip, ZAxis, cyl2.ZAxis); // die Ebene der beiden Achsen
                        GeoVector2D perp1 = pln.Project(RadiusX * (ZAxis ^ perp).Normalized); // senkrecht zur 1. Achse in der gemeinsamen Ebene
                        GeoVector2D perp2 = pln.Project(cyl2.RadiusX * (cyl2.ZAxis ^ perp).Normalized); // senkrecht zur 2. Achse in der gemeinsamen Ebene
                        GeoVector2D ax1 = pln.Project(ZAxis);
                        GeoVector2D ax2 = pln.Project(cyl2.ZAxis); // die Zylinderachsen in der gemeinsamen Ebene
                        // der Nullpunkt der Ebene ist ja der Schnittpunkt der Zylinderachsen
                        GeoPoint2D[] ip2d = new CADability.GeoPoint2D[4]; // die 4 Schnittpunkte
                        bool ok = Geometry.IntersectLL(GeoPoint2D.Origin + perp1, ax1, GeoPoint2D.Origin + perp2, ax2, out ip2d[0]) &&
                            Geometry.IntersectLL(GeoPoint2D.Origin - perp1, ax1, GeoPoint2D.Origin + perp2, ax2, out ip2d[1]) &&
                            Geometry.IntersectLL(GeoPoint2D.Origin + perp1, ax1, GeoPoint2D.Origin - perp2, ax2, out ip2d[2]) &&
                            Geometry.IntersectLL(GeoPoint2D.Origin - perp1, ax1, GeoPoint2D.Origin - perp2, ax2, out ip2d[3]);
                        if (ok) // das muss immer der Fall sein
                        {   // es gibt 4 halb-Ellipsen (oder 2 ganze Ellipsen, die sich aber schneiden, und das ist hier nicht erwünscht)
                            // die gehen alle von "oben", wo sich die beiden Zylinder tangential schneiden, nach "unten", dem gegenüberliegenden Berührpunkt
                            // über den Außenpunkt in der gemeinsamen Ebene
                            List<IDualSurfaceCurve> lres = new List<IDualSurfaceCurve>();
                            for (int i = 0; i < 4; i++)
                            {
                                GeoPoint maxpnt = pln.ToGlobal(ip2d[i]); // Schnittpunkt der beiden Zylinder mit der Ebene durch beide Achsen
                                Ellipse elli = Ellipse.Construct();
                                elli.SetEllipseArcCenterAxis(ip, maxpnt - ip, top - ip, Math.PI * 2.0 - Math.PI / 2.0, Math.PI);
                                if (i == 0)
                                {   // these two points are the points where the two ellipses intersect. We return 4 ellipse arcs
                                    // BRepIntersection needs this two points as additional vertices
                                    if (seeds == null) seeds = new List<GeoPoint>();
                                    seeds.Add(elli.StartPoint);
                                    seeds.Add(elli.EndPoint);
                                }
                                //ICurve dbg3 = (elli as ICurve).Approximate(true, 0.1);
                                //DebuggerContainer dbg4 = new CADability.DebuggerContainer();
                                //GeoVector dbg5 = elli.StartDirection;
                                //GeoVector dbg6 = elli.EndDirection;
                                //BoundingCube dbg7 = elli.GetExtent(0.0);
                                //dbg4.Add(elli);
                                ProjectedCurve pc1 = new CADability.ProjectedCurve(elli, this, true, thisBounds);
                                BoundingRect common = BoundingRect.Common(pc1.GetExtent(), thisBounds);
                                if (common.Width * common.Height > Precision.eps)
                                {
                                    ProjectedCurve pc2 = new CADability.ProjectedCurve(elli, cyl2, true, otherBounds);
                                    common = BoundingRect.Common(pc2.GetExtent(), otherBounds);
                                    if (common.Width * common.Height > Precision.eps)
                                    {
                                        lres.Add(new DualSurfaceCurve(elli, this, pc1, cyl2, pc2));
                                    }
                                }
                            }
                            return lres.ToArray();
                        }
                    }
                }
                // two cylinders intersect: 
                // There are two interseciont curves when the distance between the axis is less than the difference of the radii
                // There is one intersection curve when the distance between the axis is greater than the difference of the radii and smaller than the sum of the radii
                // There is a special case when the distance between the axis is equal to the difference of the radii, there is a singular point where two intersection curves meet
                double par11, par22;
                if (cyl2.IsRealCylinder && this.IsRealCylinder && Math.Abs(Geometry.DistLL(Location, ZAxis, cyl2.Location, cyl2.ZAxis, out par11, out par22) - Math.Abs(this.RadiusX - cyl2.RadiusX)) < Precision.eps)
                {   // the two cylinders have a touching point
                    GeoPoint p1 = Location + par11 * ZAxis;
                    GeoPoint p2 = cyl2.Location + par22 * cyl2.ZAxis;
                    GeoPoint2D[] uv1 = this.GetLineIntersection(p1, p2 - p1);
                    GeoPoint2D[] uv2 = cyl2.GetLineIntersection(p1, p2 - p1);
                    GeoPoint touchingpoint = GeoPoint.Invalid;
                    for (int i = 0; i < uv1.Length; i++)
                    {
                        SurfaceHelper.AdjustPeriodic(this, thisBounds, ref uv1[i]);
                        if (thisBounds.ContainsEps(uv1[i], Precision.eps))
                        {
                            for (int j = 0; j < uv2.Length; j++)
                            {
                                SurfaceHelper.AdjustPeriodic(cyl2, otherBounds, ref uv2[j]);
                                if (otherBounds.ContainsEps(uv2[j], Precision.eps))
                                {
                                    p1 = PointAt(uv1[i]);
                                    p2 = cyl2.PointAt(uv2[j]);
                                    if (Precision.IsEqual(p1, p2))
                                    {
                                        // this should be a seed point
                                        touchingpoint = new GeoPoint(p1, p2);
                                    }
                                }
                            }
                        }
                    }
                    if (touchingpoint.IsValid)
                    {
                        // make many curves, from each seed to the touching point
                        List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                        for (int i = 0; i < seeds.Count; i++)
                        {   // look for an appropriate middle point between this seed and touching point
                            Plane pln = new Plane(new GeoPoint(seeds[i], touchingpoint), seeds[i] - touchingpoint);
                            PlaneSurface pls = new PlaneSurface(pln);
                            IDualSurfaceCurve[] tisc = this.GetPlaneIntersection(pls, thisBounds.Left, thisBounds.Right, thisBounds.Bottom, thisBounds.Top, 0.0);
                            IDualSurfaceCurve[] oisc = other.GetPlaneIntersection(pls, otherBounds.Left, otherBounds.Right, otherBounds.Bottom, otherBounds.Top, 0.0);
                            GeoPoint innerPoint = GeoPoint.Invalid;
                            double mindist = double.MaxValue;
                            for (int j = 0; j < tisc.Length; j++)
                            {
                                for (int k = 0; k < oisc.Length; k++)
                                {
                                    ICurve2D ct;
                                    if (tisc[j].Surface1 is PlaneSurface) ct = tisc[j].Curve2D1;
                                    else ct = tisc[j].Curve2D2;
                                    ICurve2D co;
                                    if (oisc[k].Surface1 is PlaneSurface) co = oisc[k].Curve2D1;
                                    else co = oisc[k].Curve2D2;
                                    GeoPoint2DWithParameter[] ip2d = co.Intersect(ct);
                                    for (int l = 0; l < ip2d.Length; l++)
                                    {
                                        GeoPoint ip = pls.PointAt(ip2d[l].p);
                                        if (thisBounds.ContainsPeriodic(this.PositionOf(ip), Math.PI * 2.0, 0.0) && otherBounds.ContainsPeriodic(other.PositionOf(ip), Math.PI * 2.0, 0.0))
                                        {
                                            double d = Geometry.DistPL(ip, seeds[i], touchingpoint);
                                            if (d < mindist)
                                            {
                                                mindist = d;
                                                innerPoint = ip;
                                            }
                                        }
                                    }
                                }
                            }
                            if (innerPoint.IsValid && !Precision.IsEqual(seeds[i], innerPoint) && !Precision.IsEqual(innerPoint, touchingpoint))
                            {
                                res.Add(new InterpolatedDualSurfaceCurve(this, thisBounds, other, otherBounds, new GeoPoint[] { seeds[i], innerPoint, touchingpoint }));
                            }
                        }
                        seeds.Add(touchingpoint);
                        return res.ToArray();
                    }
                    else return base.GetDualSurfaceCurves(thisBounds, other, otherBounds, seeds, extremePositions);
                }
                else
                if (cyl2.IsRealCylinder && this.IsRealCylinder && Geometry.DistLL(Location, ZAxis, cyl2.Location, cyl2.ZAxis, out par11, out par22) < Math.Abs(this.RadiusX - cyl2.RadiusX) + Precision.eps)
                {   // the smaller of this two cylinders completely penetrates the wider cylinder
                    // so we have two intersection curves (entering and leaving)
                    CylindricalSurface cyl1 = this;
                    BoundingRect bounds1;
                    if (cyl2.RadiusX < cyl1.RadiusX)
                    {
                        cyl1 = cyl2;
                        cyl2 = this;
                        bounds1 = otherBounds;
                    }
                    else
                    {
                        bounds1 = thisBounds;
                    }
                    // cyl1 is the smaller one
                    GeoVector nrm = (cyl1.Axis ^ cyl2.Axis).Normalized;
                    GeoPoint2D upos1 = cyl1.PositionOf(cyl1.Location + cyl1.XAxis.Length * nrm);
                    GeoPoint2D upos2 = cyl2.PositionOf(cyl2.Location + cyl2.XAxis.Length * nrm);
                    int n = Math.Max(2, (int)((bounds1.Right - bounds1.Left) / Math.PI * 4.0));
                    double step = (bounds1.Right - bounds1.Left) / n;
                    double uAtExtreme = upos1.x;
                    while (uAtExtreme < bounds1.Left) uAtExtreme += Math.PI; // yes PI, both sides are extreme values
                    while (uAtExtreme > bounds1.Right) uAtExtreme -= Math.PI;
                    List<GeoPoint> pnts1 = new List<GeoPoint>();
                    List<GeoPoint> pnts2 = new List<GeoPoint>();
                    List<double> usteps = new List<double>(n + 2);
                    for (int i = 0; i <= n; i++) usteps.Add(bounds1.Left + i * step);
                    for (int i = 1; i < usteps.Count; i++)
                    {
                        if (uAtExtreme > usteps[i - 1] + 0.01 && uAtExtreme < usteps[i] - 0.01)
                        {
                            usteps.Insert(i, uAtExtreme);
                            break;
                        }
                    }
                    for (int i = 0; i < usteps.Count; i++)
                    {
                        double u = usteps[i];
                        GeoPoint loc = cyl1.PointAt(new GeoPoint2D(u, bounds1.Bottom));
                        GeoPoint2D[] ips = cyl2.GetLineIntersection(loc, cyl1.Axis);
                        if (ips.Length == 2)
                        {
                            if (ips[0].y < ips[1].y)
                            {
                                pnts1.Add(cyl2.PointAt(ips[0]));
                                pnts2.Add(cyl2.PointAt(ips[1]));
                            }
                            else
                            {
                                pnts1.Add(cyl2.PointAt(ips[1]));
                                pnts2.Add(cyl2.PointAt(ips[0]));
                            }
                        }
                        else if (ips.Length == 1)
                        {
                            pnts1.Add(cyl2.PointAt(ips[0]));
                            pnts2.Add(cyl2.PointAt(ips[0]));
                        }
                    }
                    // the result is not good, we need more points or extra points at the extreme position
                    return new IDualSurfaceCurve[] {
                        new InterpolatedDualSurfaceCurve(this, thisBounds, other, otherBounds, pnts2.ToArray()),
                        new InterpolatedDualSurfaceCurve(this, thisBounds, other, otherBounds, pnts1.ToArray()) };
                }
                else if (cyl2.IsRealCylinder && this.IsRealCylinder && Geometry.DistLL(Location, ZAxis, cyl2.Location, cyl2.ZAxis, out par11, out par22) < Math.Abs(this.RadiusX + cyl2.RadiusX))
                {
                    // the two cylinders have a single closed intersection curve
                    // the following computes too few points, when the cylinder axis are not perpendicular
                    // it would be easy to find more points, but difficult to bring them into the right order.
                    // the base implementation does a good job, so no need to do something here.

                    //GeoPoint m = new GeoPoint(Location + par11 * ZAxis, cyl2.Location + par22 * cyl2.ZAxis); // middle point between the two cylinders
                    //GeoPoint c = toUnit * (Location + par11 * ZAxis);
                    //double v1 = c.z;
                    //c = cyl2.toUnit * (cyl2.Location + par22 * cyl2.ZAxis);
                    //double v2 = c.z;
                    //ICurve e1 = FixedV(v1, 0.0, Math.PI * 2.0);
                    //ICurve e2 = cyl2.FixedV(v2, 0.0, Math.PI * 2.0);
                    //this.Intersect(e2, BoundingRect.InfinitBoundingRect, out GeoPoint[] ips1, out GeoPoint2D[] uv1, out double[] u1);
                    //cyl2.Intersect(e1, BoundingRect.InfinitBoundingRect, out GeoPoint[] ips2, out GeoPoint2D[] uv2, out double[] u2);
                    //if (ips1.Length == 2 && ips2.Length == 2)
                    //{
                    //    GeoPoint[] pnts = new GeoPoint[5];
                    //    pnts[0] = pnts[4] = ips1[0];
                    //    pnts[1] = ips2[0];
                    //    pnts[2] = ips1[1];
                    //    pnts[3] = ips2[1];
                    //    // doesn't work with Difference1.cdb
                    //    // return new IDualSurfaceCurve[] { new InterpolatedDualSurfaceCurve(this, thisBounds, other, otherBounds, pnts) };
                    //}
                }

            }
            else if (other is SphericalSurface)
            {
                if (Geometry.DistPL((other as SphericalSurface).Location, Location, ZAxis) < Precision.eps)
                {   // cylinder/sphere intersection, where the cylinder axis passes through the sphere's center
                    // unfortunately (design error) the matrix toUnit is not orthogonal.
                    // So we have to calculate in 3d:
                    GeoPoint2D[] ips = other.GetLineIntersection(PointAt(GeoPoint2D.Origin), ZAxis);
                    IDualSurfaceCurve[] res = new IDualSurfaceCurve[ips.Length];
                    for (int i = 0; i < ips.Length; i++)
                    {
                        GeoPoint2D pos = PositionOf(other.PointAt(ips[i]));
                        ICurve crv = FixedV(pos.y, thisBounds.Left, thisBounds.Right);
                        res[i] = new DualSurfaceCurve(crv, this, new Line2D(new GeoPoint2D(thisBounds.Left, pos.y), new GeoPoint2D(thisBounds.Right, pos.y)), other, other.GetProjectedCurve(crv, Precision.eps));
                    }
                    return res;
                }
            }
            else if (other is ToroidalSurface torus)
            {
                if (Math.Abs(torus.MinorRadius - XAxis.Length) < Precision.eps)
                {
                    if (Geometry.DistPL(torus.Location, Location, ZAxis) < Precision.eps && Precision.IsPerpendicular(torus.ZAxis, ZAxis, false))
                    {   // special case: tangential intersection

                    }
                }

                ImplicitPSurface ips = (torus as IImplicitPSurface).GetImplicitPSurface();
                double[] tangentPositions = ips.PerpendicularToGradient(Location, Axis);
                // tangentPositions are v-values for the cylinder, where it might be tangential to the torus.
                Ellipse e1 = torus.GetAxisEllipse();
                ExplicitPCurve3D epcra = (e1 as IExplicitPCurve3D).GetExplicitPCurve3D();
                double[] ppa = epcra.GetPerpendicularToDirection(Axis);
                for (int i = 0; i < ppa.Length; i++)
                {
                    GeoPoint ppra = epcra.PointAt(ppa[i]);
                    GeoPoint ppca = Geometry.DropPL(ppra, Location, Axis);
                    Line ldbg = Line.MakeLine(ppra, ppca);
                    GeoVector epcdir = epcra.DirectionAt(ppa[i]);
                    double perp0 = epcdir * (ppca - ppra);
                    double perp1 = e1.DirectionAt(e1.PositionOf(ppra)) * (ppca - ppra);
                    double perp2 = Axis * (ppca - ppra);
                }
                List<GeoPoint> tp = new List<GeoPoint>(); // potential tangent points for the cylinder
                GeoPoint[] clpd = Geometry.CircleLinePerpDist(e1, Location, Axis);
                double radiusTorus = torus.MinorRadius;
                double radiusCylinder = XAxis.Length;
                List<GeoPoint> touchingPoints = new List<GeoPoint>(2); // two points maximum
                List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                for (int i = 0; i < clpd.Length; i += 2)
                {
                    double d = clpd[i] | clpd[i + 1];
                    if (d > radiusTorus + radiusCylinder + Precision.eps)
                    {
                        // no intersection here
                    }
                    else if (d > radiusTorus + radiusCylinder - Precision.eps)
                    {
                        // touching point here
                        GeoPoint tpOnTor = clpd[i] + radiusTorus * (clpd[i + 1] - clpd[i]).Normalized;
                        GeoPoint tpOnCyl = clpd[i + 1] - radiusCylinder * (clpd[i + 1] - clpd[i]).Normalized;
                        touchingPoints.Add(new GeoPoint(tpOnCyl, tpOnTor));
                    }
                    else if (d > Math.Abs(radiusTorus - radiusCylinder) && d > Precision.eps)
                    {
                        // !!! Wir brauchen mehr Punkte: Die Ellipsen zur winkelhalbierenden Ebene auch noch verwenden !!!
                        // Wir müssen die Kurve dynamisch aufteilen, etwa so: Eine Ebenenschaar durch die Verbindungs-Achse (clpd[i + 1] - clpd[i])
                        // Die 4 Auptpunkte sind die Ausgangsmenge. Wie stark weicht die Tangente (Kreuzprodukt Normalenvektoren) in einem schon gefundenen Punkt von der Sekante des Polygons ab.
                        // Je stärker, umso eher muss ein Zwischenpunkt her.
                        // Also: betrachte ein Segment und die Abweichung der Tangenten von der Segmentrichtung in der Ebene, die die Verbindungsachse als Normalenvektor hat.
                        // 
                        // half penetration, one intersection curve
                        GeoPoint cpOnCyl = clpd[i + 1] - radiusCylinder * (clpd[i + 1] - clpd[i]).Normalized;
                        GeoPoint[] tcips = new GeoPoint[9];
                        GeoPoint2D onCyl = PositionOf(clpd[i + 1] - radiusCylinder * (clpd[i + 1] - clpd[i]).Normalized);
                        GeoVector dir45p = ModOp.Rotate(clpd[i + 1], (clpd[i + 1] - clpd[i]), SweepAngle.Deg(45)) * Axis;
                        GeoVector dir45m = ModOp.Rotate(clpd[i + 1], (clpd[i + 1] - clpd[i]), SweepAngle.Deg(135)) * Axis;
                        Plane pln = new Plane(toUnit * clpd[i + 1], toUnit * (clpd[i + 1] - clpd[i]), toUnit * dir45p);
                        GeoPoint2D elliCenter;
                        GeoVector2D majdir, mindir;
                        Geometry.IntersectCylinderPlane(pln, out elliCenter, out majdir, out mindir);
                        Ellipse elli45p = Ellipse.Construct();
                        elli45p.SetEllipseArcCenterAxis(pln.ToGlobal(elliCenter), pln.ToGlobal(majdir), pln.ToGlobal(mindir), 0, Math.PI * 2);
                        elli45p.Modify(toCylinder);

                        pln = new Plane(toUnit * clpd[i + 1], toUnit * (clpd[i + 1] - clpd[i]), toUnit * dir45m);
                        Geometry.IntersectCylinderPlane(pln, out elliCenter, out majdir, out mindir);
                        Ellipse elli45m = Ellipse.Construct();
                        elli45m.SetEllipseArcCenterAxis(pln.ToGlobal(elliCenter), pln.ToGlobal(majdir), pln.ToGlobal(mindir), 0, Math.PI * 2);
                        elli45m.Modify(toCylinder);

                        IDualSurfaceCurve[] plints = this.GetPlaneIntersection(new PlaneSurface(pln), 0, Math.PI * 2.0, double.MinValue, double.MaxValue, 0.0);
                        ICurve line = FixedU(onCyl.x, 0, 1); // this makes a line with the same u-parameter as the cylinders v-parameter
                        GeoPoint[] ips3d;
                        GeoPoint2D[] uvOnTorus;
                        double[] uOnLine, uOnCircle;
                        torus.Intersect(line, new BoundingRect(0, double.MinValue, Math.PI * 2.0, double.MaxValue), out ips3d, out uvOnTorus, out uOnLine);
                        (int below, int above) = FindClosestIndex(uOnLine, onCyl.y, false);
                        if (ips3d.Length >= 2 && below >= 0 && above >= 0)
                        {
                            tcips[0] = tcips[8] = ips3d[below];
                            tcips[4] = ips3d[above];
                            ICurve cylCirc = FixedV(onCyl.y, 0.0, Math.PI * 2.0);
                            torus.Intersect(cylCirc, new BoundingRect(0, 0, Math.PI * 2, Math.PI * 2), out ips3d, out uvOnTorus, out uOnCircle);
                            (below, above) = FindClosestIndex(uOnCircle, onCyl.x / (Math.PI * 2), true);
                            if (ips3d.Length >= 2 && below >= 0 && above >= 0)
                            {
                                tcips[2] = ips3d[below];
                                tcips[6] = ips3d[above];
                                torus.Intersect(elli45p, new BoundingRect(0, 0, Math.PI * 2, Math.PI * 2), out ips3d, out uvOnTorus, out uOnCircle);
                                (below, above) = FindClosestIndex(uOnCircle, elli45p.PositionOf(cpOnCyl), true);
                                if (ips3d.Length >= 2 && below >= 0 && above >= 0)
                                {
                                    tcips[1] = ips3d[below];
                                    tcips[5] = ips3d[above];
                                    torus.Intersect(elli45m, new BoundingRect(0, 0, Math.PI * 2, Math.PI * 2), out ips3d, out uvOnTorus, out uOnCircle);
                                    (below, above) = FindClosestIndex(uOnCircle, elli45m.PositionOf(cpOnCyl), true);
                                    if (ips3d.Length >= 2 && below >= 0 && above >= 0)
                                    {
                                        tcips[3] = ips3d[below];
                                        tcips[7] = ips3d[above];
#if DEBUG
                                        // verschaffe ein Bild der Situation
                                        // hier sieht man gut: der Schnitt der Tangenten ist ein guter Ausgangspunkt für die nächste Ebene im Ebenenbündel
                                        DebuggerContainer dcn = new DebuggerContainer();
                                        GeoPoint2D[] dbgpln = new GeoPoint2D[8];
                                        Plane plnConnNorm = new Plane(cpOnCyl, (clpd[i + 1] - clpd[i])); // Ebene senkrecht zur Verbindung
                                        for (int k = 0; k < 8; k++)
                                        {
                                            GeoPoint2D uvcyl0 = PositionOf(tcips[k]);
                                            GeoPoint2D uvtor0 = torus.PositionOf(tcips[k]);
                                            GeoVector2D dir0 = 10 * plnConnNorm.Project(GetNormal(uvcyl0) ^ torus.GetNormal(uvtor0)).Normalized;
                                            dcn.Add(new Line2D(plnConnNorm.Project(tcips[k]), plnConnNorm.Project(tcips[k]) + dir0), System.Drawing.Color.Red, 0);
                                            dbgpln[k] = plnConnNorm.Project(tcips[k]);
                                        }
                                        dcn.Add(new Polyline2D(dbgpln), System.Drawing.Color.Red, 0);
#endif
                                        InterpolatedDualSurfaceCurve dsc = new InterpolatedDualSurfaceCurve(this, thisBounds, torus, otherBounds, tcips);
                                        GeoPoint dbgdsc = dsc.PointAt(0.02);
                                        res.Add(dsc);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // full penetration, two intersection curves
                        if (radiusTorus < radiusCylinder)
                        {
                            // the torus goes through the cylinder intersection with two curves here 
                            // (and maybe one or two other curves, which are computed with another pair of clpd points)
                            double tu = torus.PositionOf(clpd[i]).x; // u-value for the tourus: one curve has u-values less than tu, the other greater than cu
                            GeoPoint[] crvbelow = new GeoPoint[8];
                            GeoPoint[] crvabove = new GeoPoint[8];
                            double dv = (otherBounds.Top - otherBounds.Bottom) / 7.0; // use 8 big circles intersection with cylinder
                            for (int j = 0; j < 8; j++)
                            {
                                ICurve bigCircle = torus.FixedV(otherBounds.Bottom + j * dv, 0.0, Math.PI * 2.0);
                                GeoPoint[] ips3d;
                                GeoPoint2D[] uvOnTCylinder;
                                double[] uOnBigCircle;
                                this.Intersect(bigCircle, new BoundingRect(0, double.MinValue, Math.PI * 2.0, double.MaxValue), out ips3d, out uvOnTCylinder, out uOnBigCircle);
                                double valBelow = double.MinValue;
                                double valAbove = double.MaxValue;
                                GeoPoint pntBelow = GeoPoint.Origin;
                                GeoPoint pntAbove = GeoPoint.Origin;
                                for (int k = 0; k < uOnBigCircle.Length; k++)
                                {
                                    double testu = uOnBigCircle[k] * Math.PI * 2 - tu;
                                    if (testu > Math.PI) testu -= Math.PI * 2;
                                    if (testu < -Math.PI) testu += Math.PI * 2;
                                    if (testu >= 0.0)
                                    {
                                        if (testu < valAbove)
                                        {
                                            valAbove = testu;
                                            pntAbove = ips3d[k];
                                        }
                                    }
                                    if (testu <= 0.0)
                                    {
                                        if (testu > valBelow)
                                        {
                                            valBelow = testu;
                                            pntBelow = ips3d[k];
                                        }
                                    }
                                }
                                if (valBelow > double.MinValue && valAbove < double.MaxValue)
                                {   // this must always be the case
                                    crvbelow[j] = pntBelow;
                                    crvabove[j] = pntAbove;
                                }
                            }
                            InterpolatedDualSurfaceCurve dsca = new InterpolatedDualSurfaceCurve(this, thisBounds, torus, otherBounds, crvabove);
                            InterpolatedDualSurfaceCurve dscb = new InterpolatedDualSurfaceCurve(this, thisBounds, torus, otherBounds, crvbelow);
                            res.Add(dsca);
                            res.Add(dscb);
                        }
                        else
                        {   // the cylinder goes through the torus
                            double tv = PositionOf(clpd[i]).y; // v-value for the cylinder: one curve has v-values less than tv, the other greater than tv
                            GeoPoint[] crvbelow = new GeoPoint[8];
                            GeoPoint[] crvabove = new GeoPoint[8];
                            double du = (thisBounds.Right - thisBounds.Left) / 7.0;
                            for (int j = 0; j < 8; j++)
                            {
                                ICurve line = FixedU(thisBounds.Left + j * du, thisBounds.Bottom, thisBounds.Top); // torus.Intersect doesn't use the line length
                                GeoPoint[] ips3d;
                                GeoPoint2D[] uvOnTorus;
                                double[] uOnLine;
                                torus.Intersect(line, new BoundingRect(0, double.MinValue, Math.PI * 2.0, double.MaxValue), out ips3d, out uvOnTorus, out uOnLine);
                                double valBelow = double.MinValue;
                                double valAbove = double.MaxValue;
                                GeoPoint pntBelow = GeoPoint.Origin;
                                GeoPoint pntAbove = GeoPoint.Origin;
                                for (int k = 0; k < uOnLine.Length; k++)
                                {
                                    double testv = PositionOf(ips3d[k]).y - tv; // don't use uOnLine, it is maybe not in the correct system
                                    if (testv >= 0.0)
                                    {
                                        if (testv < valAbove)
                                        {
                                            valAbove = testv;
                                            pntAbove = ips3d[k];
                                        }
                                    }
                                    if (testv <= 0.0)
                                    {
                                        if (testv > valBelow)
                                        {
                                            valBelow = testv;
                                            pntBelow = ips3d[k];
                                        }
                                    }
                                }
                                if (valBelow > double.MinValue && valAbove < double.MaxValue)
                                {   // this must always be the case
                                    crvbelow[j] = pntBelow;
                                    crvabove[j] = pntAbove;
                                }
                            }
                            InterpolatedDualSurfaceCurve dsca = new InterpolatedDualSurfaceCurve(this, thisBounds, torus, otherBounds, crvabove);
                            InterpolatedDualSurfaceCurve dscb = new InterpolatedDualSurfaceCurve(this, thisBounds, torus, otherBounds, crvbelow);
                            res.Add(dsca);
                            res.Add(dscb);

                        }
                    }
                }
#if DEBUG
                GeoObjectList dbgl = new GeoObjectList();
                for (int i = 0; i < clpd.Length; i += 2)
                {
                    Line ldbg = Line.MakeLine(clpd[i], clpd[i + 1]);
                    dbgl.Add(ldbg);
                }
                dbgl.Add(e1);
#endif
                //for (int i = 0; i < tangentPositions.Length; i++)
                //{
                //    Ellipse ei = FixedV(tangentPositions[i], 0.0, Math.PI * 2) as Ellipse;
                //    ExplicitPCurve3D epc3d = (ei as IExplicitPCurve3D).GetExplicitPCurve3D();
                //    double[] uei = ips.PerpendicularToGradient(epc3d);
                //    for (int j = 0; j < uei.Length; j++)
                //    {
                //        GeoPoint c = toUnit * epc3d.PointAt(uei[j]);
                //        double u = Math.Atan2(c.y, c.x);
                //        tp.Add(PointAt(new GeoPoint2D(u, tangentPositions[i])));
                //    }
                //}
                List<GeoPoint2D> tpOnCylinder = new List<GeoPoint2D>();
                List<GeoPoint2D> tpOnTorus = new List<GeoPoint2D>();
                for (int i = 0; i < tangentPositions.Length; i++)
                {
                    GeoPoint cax = toCylinder * new GeoPoint(0, 0, tangentPositions[i]); // Point on cylinder axis
                    GeoPoint pe = e1.PointAt(e1.PositionOf(cax)); // closest point on the ring axis of the torus
#if DEBUG
                    Line ldbg = Line.MakeLine(cax, pe);
                    double perp1 = e1.DirectionAt(e1.PositionOf(cax)) * (cax - pe);
                    double perp2 = Axis * (cax - pe);
#endif
                    double dist = cax | pe;
                    double d = Math.Abs(torus.MinorRadius + this.RadiusX - dist); // d==0: touching from outside
                    double d1 = Math.Abs(Math.Abs(torus.MinorRadius - this.RadiusX) - dist); // d1==0: touching from inside
                    if (d < Precision.eps || d1 < Precision.eps)
                    {
                        // this is a tangential point between cylinder and torus
                        GeoPoint ce = toUnit * pe;
                        double u = Math.Atan2(pe.y, pe.x);
                        GeoPoint2D tpc = new GeoPoint2D(u, tangentPositions[i]);
                        touchingPoints.Add(PointAt(tpc));
                        SurfaceHelper.AdjustPeriodic(this, thisBounds, ref tpc);
                        tpOnCylinder.Add(tpc);
                        GeoPoint2D tpt = torus.PositionOf(touchingPoints[touchingPoints.Count - 1]);
                        SurfaceHelper.AdjustPeriodic(torus, otherBounds, ref tpt);
                        tpOnTorus.Add(tpt);
                    }
                }
                // the following can be generalized for all surfaces:
                // if we have touching points (points where the cylinder and torus surfaces are tangential)
                // we have to split the bounds at these points, add the touching points to the seeds and compute the intersection curves
                // ending in the touching points
                for (int i = touchingPoints.Count - 1; i >= 0; --i)
                {   // use only those touching points which are inside the bounds on both surfaces
                    if (!thisBounds.Contains(tpOnCylinder[i]) || !otherBounds.Contains(tpOnTorus[i]))
                    {
                        tpOnCylinder.RemoveAt(i);
                        tpOnTorus.RemoveAt(i);
                    }
                }
                if (tpOnTorus.Count > 0)
                {
                    // we have to split the bounds at the found positions and create new seedpoints
                    double[] cu = new double[tpOnTorus.Count];
                    double[] cv = new double[tpOnTorus.Count];
                    double[] tu = new double[tpOnTorus.Count];
                    double[] tv = new double[tpOnTorus.Count];
                    for (int i = 0; i < tpOnTorus.Count; i++)
                    {
                        cu[i] = tpOnCylinder[i].x;
                        cv[i] = tpOnCylinder[i].y;
                        tu[i] = tpOnTorus[i].x;
                        tv[i] = tpOnTorus[i].y;
                    }
                    Array.Sort(cu);
                    Array.Sort(cv);
                    Array.Sort(tu);
                    Array.Sort(tv);
                    List<BoundingRect> thisParts = new List<BoundingRect>();
                    List<BoundingRect> otherParts = new List<BoundingRect>();
                    for (int i = 0; i < cu.Length; i++)
                    {
                        double left, right;
                        if (i == 0) left = thisBounds.Left;
                        else left = cu[i];
                        if (i < cu.Length - 1) right = cu[i + 1];
                        else right = thisBounds.Right;
                        for (int j = 0; j < cv.Length; j++)
                        {
                            double bottom, top;
                            if (j == 0) bottom = thisBounds.Bottom;
                            else bottom = cv[j];
                            if (j < cv.Length - 1) top = cv[j + 1];
                            else top = thisBounds.Top;
                            thisParts.Add(new BoundingRect(left, bottom, right, top));
                        }
                    }
                    for (int i = 0; i < tu.Length; i++)
                    {
                        double left, right;
                        if (i == 0) left = otherBounds.Left;
                        else left = tu[i];
                        if (i < tu.Length - 1) right = tu[i + 1];
                        else right = otherBounds.Right;
                        for (int j = 0; j < tv.Length; j++)
                        {
                            double bottom, top;
                            if (j == 0) bottom = otherBounds.Bottom;
                            else bottom = tv[j];
                            if (j < tv.Length - 1) top = tv[j + 1];
                            else top = otherBounds.Top;
                            otherParts.Add(new BoundingRect(left, bottom, right, top));
                        }
                    }
                    List<GeoPoint2D> seedsUvThis = new List<GeoPoint2D>();
                    List<GeoPoint2D> seedsUvOther = new List<GeoPoint2D>();
                    seeds.AddRange(touchingPoints);
                    for (int i = 0; i < seeds.Count; i++)
                    {
                        seedsUvThis.Add(PositionOf(touchingPoints[i], thisBounds));
                        seedsUvOther.Add(torus.PositionOf(touchingPoints[i], otherBounds));
                    }
                    Dictionary<int, Set<int>> thisSeeds = new Dictionary<int, Set<int>>();
                    double eps = thisBounds.Size * 1e-6;
                    for (int i = 0; i < thisParts.Count; i++)
                    {
                        thisSeeds[i] = new Set<int>();
                        for (int j = 0; j < seedsUvThis.Count; j++)
                        {
                            if (thisParts[i].ContainsEps(seedsUvThis[j], eps)) thisSeeds[i].Add(j);
                        }
                    }
                    Dictionary<int, Set<int>> otherSeeds = new Dictionary<int, Set<int>>();
                    eps = otherBounds.Size * 1e-6;
                    for (int i = 0; i < otherParts.Count; i++)
                    {
                        otherSeeds[i] = new Set<int>();
                        for (int j = 0; j < seedsUvOther.Count; j++)
                        {
                            if (otherParts[i].ContainsEps(seedsUvOther[j], eps)) otherSeeds[i].Add(j);
                        }
                    }
                    // List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                    for (int i = 0; i < thisParts.Count; i++)
                    {
                        for (int j = 0; j < otherParts.Count; j++)
                        {
                            Set<int> commonPoints = thisSeeds[i].Intersection(otherSeeds[j]);
                            if (commonPoints.Count > 1)
                            {
                                List<GeoPoint> seedsij = new List<GeoPoint>();
                                foreach (int n in commonPoints)
                                {
                                    seedsij.Add(seeds[n]);
                                }
                                res.AddRange(base.GetDualSurfaceCurves(thisParts[i], other, otherParts[j], seedsij, null));
                            }
                        }
                    }
                    return res.ToArray();
                }
            }
            return base.GetDualSurfaceCurves(thisBounds, other, otherBounds, seeds, extremePositions);
        }

        private static (int below, int above) FindClosestIndex(double[] pars, double closeTo, bool isPeriodic)
        {
            int below = -1;
            int above = -1;
            double maxBelow = double.MinValue;
            double minAbove = double.MaxValue;
            for (int i = 0; i < pars.Length; i++)
            {
                double u = pars[i] - closeTo;
                if (u >= 0.0)
                {
                    if (u < minAbove)
                    {
                        minAbove = u;
                        above = i;
                    }
                }
                else if (u + 1 >= 0.0)
                {
                    if (u + 1 < minAbove)
                    {
                        minAbove = u + 1;
                        above = i;
                    }
                }
                if (u <= 0.0)
                {
                    if (u > maxBelow)
                    {
                        maxBelow = u;
                        below = i;
                    }
                }
                if (u - 1 <= 0.0)
                {
                    if (u - 1 > maxBelow)
                    {
                        maxBelow = u - 1;
                        below = i;
                    }
                }
            }
            return (below, above);
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
            if (umax - umin > Math.PI)
            {
                intu = new double[3];
                intu[0] = umin;
                intu[1] = (umin + umax) / 2.0;
                intu[2] = umax;
            }
            else
            {
                intu = new double[2];
                intu[0] = umin;
                intu[1] = umax;
            }
            intv = new double[2];
            intv[0] = vmin;
            intv[1] = vmax;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            GeoPoint c = toUnit * p;
            double u = Math.Atan2(c.y, c.x);
            if (u < 0.0) u += 2.0 * Math.PI;
            double v = c.z;
            return new GeoPoint2D(u, v);
        }
        private GeoPoint2D PositionOfUnit(GeoPoint p)
        {
            double u = Math.Atan2(p.y, p.x);
            if (u < 0.0) u += 2.0 * Math.PI;
            double v = p.z;
            return new GeoPoint2D(u, v);
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
            // Maxima und Minima liegen in der Richtung dir
            GeoVector dir = toUnit * p.Direction;
            for (double a = Math.Atan2(dir.y, dir.x); a < umax; a += Math.PI)
            {
                if (a > umin)
                {
                    CheckZMinMax(p, a, vmin, ref zMin, ref zMax);
                    CheckZMinMax(p, a, vmax, ref zMin, ref zMax);
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            return new CylindricalSurface(toCylinder);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            boxedSurfaceEx = null;
            toCylinder = m * toCylinder;
            toUnit = toCylinder.GetInverse();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            CylindricalSurface cc = CopyFrom as CylindricalSurface;
            if (cc != null)
            {
                this.toCylinder = cc.toCylinder;
                this.toUnit = cc.toUnit;
            }
        }
        public override ISurface GetOffsetSurface(double offset)
        {
            if (toUnit.Determinant < 0) offset = -offset;
            GeoVector xaxis = XAxis + offset * XAxis.Normalized;
            GeoVector yaxis = YAxis + offset * YAxis.Normalized;
            if (Precision.IsNullVector(xaxis) || Precision.IsNullVector(yaxis)) return null;
            return new CylindricalSurface(Location, xaxis, yaxis, ZAxis);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetProjectedCurve (ICurve, double)"/>
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override GeoPoint[] GetTouchingPoints(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {   // still most of it to implement!!!
            if (other is PlaneSurface)
            {
            }
            else if (other is CylindricalSurface)
            {
            }
            else if (other is ToroidalSurface)
            {
                ToroidalSurface torus = other as ToroidalSurface;
                ImplicitPSurface ips = (torus as IImplicitPSurface).GetImplicitPSurface();
                double[] tangentPositions = ips.PerpendicularToGradient(Location, Axis);
                // tangentPositions are v-values for the cylinder, where it might be tangential to the torus.
                Ellipse e1 = torus.GetAxisEllipse();
                List<GeoPoint> tp = new List<GeoPoint>(); // potential tangent points for the cylinder
                List<GeoPoint> touchingPoints = new List<GeoPoint>();
                //List<GeoPoint2D> tpOnCylinder = new List<GeoPoint2D>();
                //List<GeoPoint2D> tpOnTorus = new List<GeoPoint2D>();
                for (int i = 0; i < tangentPositions.Length; i++)
                {
                    GeoPoint cax = toCylinder * new GeoPoint(0, 0, tangentPositions[i]); // Point on cylinder axis
                    GeoPoint pe = e1.PointAt(e1.PositionOf(cax)); // closest point on the ring axis of the torus
                    double dist = cax | pe;
                    double d = Math.Abs(torus.MinorRadius + this.RadiusX - dist);
                    if (d < Precision.eps)
                    {
                        // this is a tangential point between cylinder and torus
                        GeoPoint ce = toUnit * pe;
                        double u = Math.Atan2(pe.y, pe.x);
                        GeoPoint2D tpc = new GeoPoint2D(u, tangentPositions[i]);
                        touchingPoints.Add(PointAt(tpc));
                        //SurfaceHelper.AdjustPeriodic(this, thisBounds, ref tpc);
                        //tpOnCylinder.Add(tpc);
                        //GeoPoint2D tpt = torus.PositionOf(touchingPoints[touchingPoints.Count - 1]);
                        //SurfaceHelper.AdjustPeriodic(torus, otherBounds, ref tpt);
                        //tpOnTorus.Add(tpt);
                    }
                }
                return touchingPoints.ToArray();
            }
            return new GeoPoint[0];
        }
        public override ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {
            ICurve crvunit = curve.CloneModified(toUnit);
            if (crvunit is Line)
            {
                Line l = crvunit as Line;
                if (Precision.SameDirection(GeoVector.ZAxis, l.StartDirection, false))
                {
                    double u = Math.Atan2(l.StartPoint.y, l.StartPoint.x);
                    if (u < 0.0) u += 2.0 * Math.PI;
                    return new Line2D(new GeoPoint2D(u, l.StartPoint.z), new GeoPoint2D(u, l.EndPoint.z));
                }
            }
            else if (crvunit is Ellipse)
            {
                Ellipse e = (crvunit as Ellipse);
                if (Precision.SameDirection(GeoVector.ZAxis, e.Plane.Normal, true))
                {   // es wird immer davon ausgegangen, dass curve sehr nahe am Zylinder liegt, also aus einem Schnitt
                    // oder einer anderen Berechnung kommt. Es wird auch erwartet, dass ein Bogen nicht über den Saum geht.
                    bool forward = Math.Sign(e.SweepParameter) == Math.Sign(e.Plane.Normal.z);
                    if (e.IsArc)
                    {
                        double ustart = Math.Atan2(e.StartPoint.y, e.StartPoint.x);
                        if (ustart < 0.0) ustart += 2.0 * Math.PI;
                        double uend = Math.Atan2(e.EndPoint.y, e.EndPoint.x);
                        if (uend < 0.0) uend += 2.0 * Math.PI;
                        // da der Bogen nicht über den Saum gehen darf müsste hier ustart und uend richtig sein
                        // man könnte mit forward checken, was jedoch, wenn nicht richtig?
                        if (!forward && ustart < uend)
                        {
                            // entweder ustart + 2*pi oder uend - 2*pi
                            ustart += 2 * Math.PI;
                        }
                        if (forward && ustart > uend)
                        {
                            uend += 2 * Math.PI; // noch nicht getestet
                        }
                        // Grenzfälle: ustart oder uend liegen auf 0.0 oder 2*pi
                        // dann weiß man nicht ob der Punkt zyklisch richtig ist
                        return new Line2D(new GeoPoint2D(ustart, e.Center.z), new GeoPoint2D(uend, e.Center.z));
                    }
                    else
                    {
                        if (forward)
                            return new Line2D(new GeoPoint2D(0.0, e.Center.z), new GeoPoint2D(2.0 * Math.PI, e.Center.z));
                        else
                            return new Line2D(new GeoPoint2D(2.0 * Math.PI, e.Center.z), new GeoPoint2D(0.0, e.Center.z));
                    }
                }
                else
                {
                    // an ellipse on a cylinder is a sine curve in 2d
                    //bool forward = Math.Sign(e.SweepParameter) == Math.Sign(e.Plane.Normal.z);
                    //GeoPoint2D ps0 = PositionOfUnit(e.Center + e.MajorAxis);
                    //GeoPoint2D ps1 = PositionOfUnit(e.Center - e.MajorAxis);
                    //ps1.x = ps0.x + Math.PI;
                    //GeoPoint2D ps2 = new GeoPoint2D(ps1.x + Math.PI / 2, (ps0.y + ps1.y) / 2);
                    //GeoPoint2D pu0 = new GeoPoint2D(Math.PI / 2, 1);
                    //GeoPoint2D pu1 = new GeoPoint2D(3 * Math.PI / 2, -1);
                    //GeoPoint2D pu2 = new GeoPoint2D(2 * Math.PI, 0);
                    //ModOp2D fromUnit = ModOp2D.Fit(new GeoPoint2D[] { pu0, pu1, pu2 }, new GeoPoint2D[] { ps0, ps1, ps2 }, true);
                    //ModOp2D toUnit = fromUnit.GetInverse();
                    //double ustart = (toUnit * PositionOf(curve.StartPoint)).x;
                    //double uend = (toUnit * PositionOf(curve.EndPoint)).x;
                    //if (!forward)
                    //{
                    //    double tmp = ustart;
                    //    ustart = uend;
                    //    uend = tmp;
                    //}
                    //if (uend < ustart) uend += Math.PI * 2;
                    //// ustart + 0 * udiff == umin -> ustart = umin
                    //// umin + 1 * udiff = umax -> 
                    //SineCurve2D sc2d = new SineCurve2D(ustart, uend - ustart, fromUnit);
                    // if (!forward) sc2d.Reverse();


                    // The above code assumes the correct orientation of the ellipse axis. This may not be the case in some situations. (STEP import!, e.g. 3590_E10_NN_FUNZIONA.stp)
                    // So we try to find a sine curve, which is scaled in y and moved in x and y by providing 3 points and two deltas for the sine parameter u.
                    // For 3 points on the curve (start, middle, end) we have two parameter deltas: "a" and "b"
                    // Wolfram Alpha: solve [(Sin(x+a)-Sin(x))*d = (Sin(x+b)-Sin(x))*c, {x}] yields: (substitute u for x, x is the unknown in Wolfram Alpha)
                    // x = -cos^(-1)(-(-d cos(a) + c cos(b) - c + d)/sqrt(-2 c d sin(a) sin(b) - 2 c d cos(a) cos(b) + 2 c d cos(a) + d^2 sin^2(a) + d^2 cos^2(a) - 2 d^2 cos(a) + c^2 sin^2(b) + c^2 cos^2(b) - 2 c^2 cos(b) + 2 c d cos(b) + c^2 - 2 c d + d^2))
                    // x = cos^(-1)(-(-d cos(a) + c cos(b) - c + d)/sqrt(-2 c d sin(a) sin(b) - 2 c d cos(a) cos(b) + 2 c d cos(a) + d^2 sin^2(a) + d^2 cos^2(a) - 2 d^2 cos(a) + c^2 sin^2(b) + c^2 cos^2(b) - 2 c^2 cos(b) + 2 c d cos(b) + c^2 - 2 c d + d^2))
                    // x = -cos^(-1)((-d cos(a) + c cos(b) - c + d)/sqrt(-2 c d sin(a) sin(b) - 2 c d cos(a) cos(b) + 2 c d cos(a) + d^2 sin^2(a) + d^2 cos^2(a) - 2 d^2 cos(a) + c^2 sin^2(b) + c^2 cos^2(b) - 2 c^2 cos(b) + 2 c d cos(b) + c^2 - 2 c d + d^2))
                    // x = cos^(-1)((-d cos(a) + c cos(b) - c + d)/sqrt(-2 c d sin(a) sin(b) - 2 c d cos(a) cos(b) + 2 c d cos(a) + d^2 sin^2(a) + d^2 cos^2(a) - 2 d^2 cos(a) + c^2 sin^2(b) + c^2 cos^2(b) - 2 c^2 cos(b) + 2 c d cos(b) + c^2 - 2 c d + d^2))
                    // (all roots are the same)
                    // this method can be used when 3 points are known and the deltas of their u-parameters are also known

                    GeoPoint2D pse = PositionOfUnit(e.StartPoint);
                    GeoPoint2D pme = PositionOfUnit(e.PointAt(0.5));
                    GeoPoint2D pee = PositionOfUnit(e.EndPoint);
                    if (Math.Abs(pse.x - pme.x) > Math.PI)
                    {   // the middle point must be less than 180° from the starting point
                        if (pme.x < pse.x) pme.x += 2 * Math.PI;
                        else pme.x -= 2 * Math.PI;
                    }
                    if (Math.Abs(pee.x - pme.x) > Math.PI)
                    {   // the ending point must be less than 180° from the middle point
                        if (pee.x < pme.x) pee.x += 2 * Math.PI;
                        else pee.x -= 2 * Math.PI;
                    }
                    // Polyline2D dbgpl = new Polyline2D(new GeoPoint2D[] { pse, pme, pee });
                    double a = pme.x - pse.x;
                    double b = pee.x - pse.x;
                    double cosa = Math.Cos(a);
                    double sina = Math.Sin(a);
                    double cosb = Math.Cos(b);
                    double sinb = Math.Sin(b);
                    double c = (pme.y - pse.y);
                    double d = (pee.y - pse.y);
                    double c2 = c * c;
                    double d2 = d * d;
                    double cd2 = 2 * c * d;
                    double cos2a = cosa * cosa;
                    double cos2b = cosb * cosb;
                    double sin2a = sina * sina;
                    double sin2b = sinb * sinb;
                    double s = -cd2 * sina * sinb - cd2 * cosa * cosb + cd2 * cosa + d2 * sin2a + d2 * cos2a - 2 * d2 * cosa + c2 * sin2b + c2 * cos2b - 2 * c2 * cosb + cd2 * cosb + c2 - cd2 + d2;
                    // can s ever be negative?
                    if (s >= 0)
                    {
                        double ac = -d * cosa + c * cosb - c + d;
                        double cosarg = Math.Max(-1.0, Math.Min(1.0, ac / Math.Sqrt(s)));
                        double u1 = -Math.Acos(cosarg);
                        double u3 = -Math.Acos(-cosarg);
                        double minDist = double.MaxValue;
                        SineCurve2D res = null;
                        foreach (double u in new double[] { u1, -u1, u3, -u3 })
                        {   // find the correct solution of the 4 possible solutions
                            double fy = (pee.y - pse.y) / (Math.Sin(u + b) - Math.Sin(u));
                            double tx = pse.x - u;
                            double ty = pse.y - fy * Math.Sin(u);
                            SineCurve2D s2cx = new SineCurve2D(u, b, ModOp2D.Translate(tx, ty) * ModOp2D.Scale(1, fy));
                            GeoPoint testPoint = PointAt(s2cx.PointAt(0.5));
                            double dist = curve.PointAt(curve.PositionOf(testPoint)) | testPoint;
                            if (dist < minDist)
                            {
                                minDist = dist;
                                res = s2cx;
                            }
                        }
                        if (minDist < (RadiusX + RadiusY) * 1e-5) return res;
                    }
                }
            }
            return base.GetProjectedCurve(curve, precision);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.ReverseOrientation ()"/>
        /// </summary>
        /// <returns></returns>
        public override ModOp2D ReverseOrientation()
        {
            boxedSurfaceEx = null;
            toCylinder = toCylinder * new ModOp(-1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0); // umkehrung von x
            toUnit = toCylinder.GetInverse();
            return new ModOp2D(-1, 0, Math.PI, 0, 1, 0);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.HitTest (BoundingCube, double, double, double, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public override bool HitTest(BoundingCube cube, double umin, double umax, double vmin, double vmax)
        {
            throw new NotImplementedException("HitTest must be implemented");
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.HitTest (BoundingCube, out GeoPoint2D)"/>
        /// </summary>
        /// <param name="bc"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override bool HitTest(BoundingCube bc, out GeoPoint2D uv)
        {
            // any vertex of the cube on the cylinder?
            uv = GeoPoint2D.Origin;
            GeoPoint[] cube = bc.Points;
            bool[] pos = new bool[8];
            for (int i = 0; i < 8; ++i)
            {
                GeoPoint p = cube[i];
                GeoPoint q = toUnit * p;
                if (Math.Abs(q.x * q.x + q.y * q.y - 1) < Precision.eps)
                {
                    uv = PositionOf(p);
                    return true;
                }
                pos[i] = Orientation(p) < 0;
            }

            // any line of the cube interfering the cylinder?
            for (int i = 0; i < 7; ++i)
            {
                for (int j = i + 1; j < 8; ++j)
                {
                    if (pos[i] != pos[j])
                    {
                        GeoPoint2D[] erg = GetLineIntersection(cube[i], cube[j] - cube[i]);
                        for (int k = 0; k < erg.Length; ++k)
                        {
                            GeoPoint gp = PointAt(erg[k]);
                            if (bc.Contains(gp) || bc.IsOnBounds(gp, bc.Size * 1e-8))
                            {
                                uv = erg[k];
                                return true;
                            }
                        }
                        throw new ApplicationException("internal error: CylindricalSurface.HitTest");
                    }
                }
            }

            // cube´s vertices within the surface?
            if (pos[0] && pos[1] && pos[2] && pos[3] && pos[4] && pos[5] && pos[6] && pos[7])
                return false;  //convexity of the inner points

            // complete cylinder is outside of the cube?
            if (!bc.Interferes(Location, Axis, 0, false))
                return false;  //all vertices of the cube are out of the cylinder

            // now every mantleline goes through the complete cube
            uv = PositionOf(toCylinder * (new GeoPoint(1, 0, (toUnit * bc.GetCenter()).z)));
            return true;
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
            GeoPoint q = toUnit * p;
            return (q.x * q.x + q.y * q.y - 1);
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
            if (other is CylindricalSurface)
            {
                CylindricalSurface othercylinder = other as CylindricalSurface;
                if (Math.Abs(XAxis.Length - othercylinder.XAxis.Length) < Precision.eps &&
                Math.Abs(YAxis.Length - othercylinder.YAxis.Length) < Precision.eps &&
                Precision.SameDirection(ZAxis, othercylinder.ZAxis, false) &&
                Geometry.DistPL(othercylinder.Location, Location, ZAxis) < Precision.eps)
                {
                    GeoPoint2D[] src = new GeoPoint2D[3];
                    GeoPoint2D[] dst = new GeoPoint2D[3];
                    src[0] = GeoPoint2D.Origin;
                    src[1] = new GeoPoint2D(1.0, 0.0);
                    src[2] = new GeoPoint2D(0.0, 1.0);
                    for (int i = 0; i < 3; ++i)
                    {
                        dst[i] = othercylinder.PositionOf(PointAt(src[i]));
                    }
                    for (int i = 0; i < 3; ++i)
                    {
                        SurfaceHelper.AdjustPeriodic(othercylinder, otherBounds, ref dst[i]);
                    }
                    if (dst[1].x - dst[0].x > Math.PI) dst[1].x -= Math.PI * 2.0;
                    if (dst[1].x - dst[0].x < -Math.PI) dst[1].x += Math.PI * 2.0;

                    // firstToSecond = ModOp2D.Translate(dx, dst[0].y) * ModOp2D.Scale(1.0, src[2].y - src[0].y);
                    firstToSecond = ModOp2D.Fit(src, dst, true); // für den Fall, dass die Z-Achse gegenläufig ist, braucht man dieses und nicht die Zeile vorher

                    //GeoPoint2D cnt = other.PositionOf(PointAt(thisBounds.GetCenter()));
                    //GeoPoint2D cnt2 = firstToSecond * thisBounds.GetCenter();
                    //double obx = (otherBounds.Left + otherBounds.Right) / 2.0;
                    //if (Math.Abs(obx - cnt2.x) > Math.Abs(obx - (cnt2.x + Math.PI * 2))) firstToSecond = ModOp2D.Translate(Math.PI * 2.0, 0) * firstToSecond;
                    //if (Math.Abs(obx - cnt2.x) > Math.Abs(obx - (cnt2.x - Math.PI * 2))) firstToSecond = ModOp2D.Translate(-Math.PI * 2.0, 0) * firstToSecond;
                    if (firstToSecond[0, 2] > Math.PI) firstToSecond[0, 2] = firstToSecond[0, 2] - 2.0 * Math.PI;
                    if (firstToSecond[0, 2] < -Math.PI) firstToSecond[0, 2] = firstToSecond[0, 2] + 2.0 * Math.PI;
                    return true;
                }
                return false;
            }
            // sonst die allgemeine Überprüfung
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public override double IsParallel(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            if (other is ICylinder c)
            {
                if (c.Axis.SameGeometry((this as ICylinder).Axis))
                {
                    return Math.Abs(Math.Abs((this as ICylinder).Radius) - Math.Abs(c.Radius));
                }
            }
            return double.MaxValue;
        }

        public override Polynom GetImplicitPolynomial()
        {
            if (implicitPolynomial == null)
            {
                GeoVector zNormed = ZAxis.Normalized;
                PolynomVector x = zNormed ^ (new GeoVector(Location.x, Location.y, Location.z) - PolynomVector.xyz);
                implicitPolynomial = (x * x) - XAxis * XAxis;
                // we need to scale the implicit polynomial so that it yields the true distance to the surface
                GeoPoint p = Location + XAxis + XAxis.Normalized; // a point outside the cylinder with distance 1
                double d = implicitPolynomial.Eval(p); // this should be 1 when the polynomial is normalized
                if ((XAxis ^ YAxis) * ZAxis < 0) d = -d; // inverse oriented cylinder
                implicitPolynomial = (1 / d) * implicitPolynomial; // normalize the polynomial
            }
            return implicitPolynomial;
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
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.FixedU (double, double, double)"/>
        /// </summary>
        /// <param name="u"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public override ICurve FixedU(double u, double vmin, double vmax)
        {
            Line l = Line.Construct();
            l.SetTwoPoints(PointAt(new GeoPoint2D(u, vmin)), PointAt(new GeoPoint2D(u, vmax)));
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
            Ellipse e = Ellipse.Construct();
            e.SetCirclePlaneCenterRadius(new Plane(Plane.XYPlane, v), new GeoPoint(0, 0, v), 1.0);
            e.StartParameter = umin;
            e.SweepParameter = umax - umin;
            e.Modify(toCylinder);
            return e;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNonPeriodicSurface (Border)"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface GetNonPeriodicSurface(ICurve[] orientedCurves)
        {
            CylindricalSurfaceNP res = new CylindricalSurfaceNP(Location, RadiusX, ZAxis, toUnit.Determinant > 0, orientedCurves);
            return res;
        }
        public override CADability.GeoObject.RuledSurfaceMode IsRuled
        {
            get
            {
                return RuledSurfaceMode.ruledInV;
            }
        }
        public override double MaxDist(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp)
        {
            mp = new GeoPoint2D(sp, ep);
            // ACHTUNG: zyklisch wird hier nicht berücksichtigt, wird aber vom aufrufenden Kontext (Triangulierung) berücksichtigt
            // ansonsten wäre ja auch nicht klar, welche 2d-Linie gemeint ist
            return Geometry.DistPL(PointAt(mp), PointAt(sp), PointAt(ep));
        }
        public override int GetExtremePositions(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, out List<Tuple<double, double, double, double>> extremePositions)
        {
            switch (other)
            {
                case PlaneSurface ps:
                    {
                        other.GetExtremePositions(otherBounds, this, thisBounds, out extremePositions);
                        if (extremePositions != null)
                        {
                            for (int i = 0; i < extremePositions.Count; i++)
                            {
                                extremePositions[i] = new Tuple<double, double, double, double>(extremePositions[i].Item3, extremePositions[i].Item4, extremePositions[i].Item1, extremePositions[i].Item2);
                            }
                            return extremePositions.Count;
                        }
                        return 0;
                    }
                case CylindricalSurface cs:
                    {
                        Geometry.DistLL(Location, ZAxis, cs.Location, cs.ZAxis, out double par1, out double par2);
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        if (par1 >= thisBounds.Bottom && par1 <= thisBounds.Top) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, par1, double.NaN, double.NaN));
                        if (par2 >= otherBounds.Bottom && par2 <= otherBounds.Top) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, double.NaN, par2));
                        // these are fixed u curves (circles) on both cylinders
                        return extremePositions.Count;
                    }
                case ConicalSurface cos:
                    {
                        Geometry.DistLL(Location, ZAxis, cos.Location, cos.ZAxis, out double par1, out double par2);
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        GeoPoint cp = Location + par1 * ZAxis; // point on the cylinder axis closest to cone axis
                        GeoPoint2D[] fpOnCone = cos.PerpendicularFoot(cp); // perpendicular from this point onto the cone
                        for (int i = 0; i < fpOnCone.Length; i++)
                        {
                            SurfaceHelper.AdjustPeriodic(cos, otherBounds, ref fpOnCone[i]);
                            if (otherBounds.Contains(fpOnCone[i])) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, fpOnCone[i].x, fpOnCone[i].y));
                        }
                        if (par1 >= thisBounds.Bottom && par1 <= thisBounds.Top) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, par1, double.NaN, double.NaN));
                        return extremePositions.Count;
                    }
                case SphericalSurface ss:
                    {
                        GeoPoint fp = Geometry.DropPL(ss.Location, this.Location, this.ZAxis);
                        GeoVector ldir = fp - ss.Location;
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        if (Precision.IsNullVector(ldir))
                        {
                            return 0; // sphere and cylinder have the same axis
                        }
                        GeoPoint2D[] ip = ss.GetLineIntersection(ss.Location, fp - ss.Location);
                        for (int i = 0; i < ip.Length; i++)
                        {
                            SurfaceHelper.AdjustPeriodic(ss, otherBounds, ref ip[i]);
                            if (otherBounds.Contains(ip[i])) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, ip[i].x, ip[i].y));
                        }
                        return extremePositions.Count;
                    }
                case ToroidalSurface ts:
                    {
                        GeoPoint[] fp = Geometry.CircleLinePerpDist(ts.GetAxisEllipse(), Location, ZAxis); // fp are points on the axis-circle of the torus
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        for (int i = 0; i < fp.Length; i++)
                        {
                            GeoPoint2D uv = ts.PositionOf(fp[i]);
                            SurfaceHelper.AdjustPeriodic(ts, otherBounds, ref uv); // use small circles of the torus
                            if (otherBounds.Left <= uv.x && otherBounds.Right >= uv.x) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, uv.x, double.NaN));
                            uv = PositionOf(fp[i]); // on this cylinder
                            if (thisBounds.Contains(uv)) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, uv.y, double.NaN, double.NaN));
                        }
                        return extremePositions.Count;
                    }
            }
            // otherwise: boxedsurface
            extremePositions = null;
            return -1; // means: no implementation for this combination
        }
        public override bool IsExtruded(GeoVector direction)
        {
            return Precision.SameDirection(Axis, direction, false);
        }

        #endregion
        #region ISerializable Members
        protected CylindricalSurface(SerializationInfo info, StreamingContext context) : base()
        {
            toCylinder = (ModOp)info.GetValue("ToCylinder", typeof(ModOp));
            toUnit = toCylinder.GetInverse(); // das ist ein struct, also schon eingelesen
                                              // OnDeserialization kommt manchmal zu spät
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ToCylinder", toCylinder, typeof(ModOp));
        }
        #endregion
        #region IDeserializationCallback Members
        public void OnDeserialization(object sender)
        {
            toUnit = toCylinder.GetInverse();
        }
        #endregion
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            List<IPropertyEntry> se = new List<IPropertyEntry>();
            GeoPointProperty location = new GeoPointProperty("CylindricalSurface.Location", frame, false);
            location.ReadOnly = true;
            location.GetGeoPointEvent += delegate (GeoPointProperty sender) { return toCylinder * GeoPoint.Origin; };
            se.Add(location);
            GeoVectorProperty dirx = new GeoVectorProperty("CylindricalSurface.DirectionX", frame, false);
            dirx.ReadOnly = true;
            dirx.IsAngle = false;
            dirx.GetGeoVectorEvent += delegate (GeoVectorProperty sender) { return toCylinder * GeoVector.XAxis; };
            se.Add(dirx);
            GeoVectorProperty diry = new GeoVectorProperty("CylindricalSurface.DirectionY", frame, false);
            diry.ReadOnly = true;
            diry.IsAngle = false;
            diry.GetGeoVectorEvent += delegate (GeoVectorProperty sender) { return toCylinder * GeoVector.YAxis; };
            se.Add(diry);
            if (Precision.IsEqual(RadiusX, RadiusY))
            {
                DoubleProperty radius = new DoubleProperty("CylindricalSurface.Radius", frame);
                radius.ReadOnly = true;
                radius.DoubleValue = RadiusX;
                radius.GetDoubleEvent += delegate (DoubleProperty sender) { return RadiusX; };
                se.Add(radius);
            }
            return new GroupProperty("CylindricalSurface", se.ToArray());
        }

        /// <summary>
        /// This surface is a round cylinder, not an elliptical cylinder
        /// </summary>
        public bool IsRealCylinder
        {
            get
            {
                return Math.Abs(XAxis.Length - YAxis.Length) < Precision.eps;
            }
        }
        #region ISurfaceOfRevolution Members
        Axis ISurfaceOfRevolution.Axis
        {
            get
            {
                return new Axis(Location, Axis);
            }
        }
        ICurve ISurfaceOfRevolution.Curve
        {
            get
            {
                return Line.MakeLine(PointAt(new GeoPoint2D(0.0, 0.0)), PointAt(new GeoPoint2D(0.0, 1.0)));
            }
        }
        #endregion
        #region ISurfaceOf(Arc)Extrusion
        ICurve ISurfaceOfExtrusion.Axis(BoundingRect domain)
        {
            return Line.TwoPoints(Location + domain.Bottom * ZAxis, Location + domain.Top * ZAxis);
        }
        bool ISurfaceOfExtrusion.ModifyAxis(GeoPoint throughPoint)
        {
            GeoPoint onAxis = Geometry.DropPL(throughPoint, Location, ZAxis);
            this.Modify(ModOp.Translate(throughPoint - onAxis));
            return true;
        }
        IOrientation ISurfaceOfExtrusion.Orientation => throw new NotImplementedException();
        ICurve ISurfaceOfExtrusion.ExtrudedCurve => usedArea.IsEmpty() || usedArea.IsInfinite || usedArea.IsInvalid() ?
            FixedV(0.0, 0.0, Math.PI) : FixedV(0.0, usedArea.Left, usedArea.Right);

        double ISurfaceOfArcExtrusion.Radius
        {
            get
            {
                return XAxis.Length;
            }
            set
            {
                GeoVector directionx = value * XAxis.Normalized;
                GeoVector directiony = value * YAxis.Normalized;
                ModOp m1 = ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis },
                    new GeoVector[] { directionx, directiony, ZAxis });
                ModOp m2 = ModOp.Translate(Location.x, Location.y, Location.z);
                toCylinder = m2 * m1;
                toUnit = toCylinder.GetInverse();
            }
        }

        bool ISurfaceOfExtrusion.ExtrusionDirectionIsV => true;
        #endregion
        public bool OutwardOriented => toCylinder.Determinant > 0;

        Axis ICylinder.Axis
        {
            get => new Axis(Location, ZAxis);
            set
            {
                throw new NotImplementedException();
            }
        }

        double ISurfaceWithRadius.Radius
        {
            get => RadiusX;
            set
            {
                double f = value / RadiusX;
                ModOp m = ModOp.Scale(f);
                toCylinder = toCylinder * m;
                toUnit = toCylinder.GetInverse();
            }
        }
        bool ISurfaceWithRadius.IsModifiable => true;

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            //CYLINDRICAL_SURFACE
            int ax = export.WriteAxis2Placement3d(Location, Axis, XAxis);
            int res = export.WriteDefinition("CYLINDRICAL_SURFACE('',#" + ax.ToString() + "," + export.ToString(RadiusX) + ")");
            if (toCylinder.Determinant < 0) return -res; // the normal vector points to the inside, STEP only knows cylindrical surfaces with outside pointing normals, 
            // the sign of the result holds the orientation information
            else return res;
        }

        protected CylindricalSurface() : base(null)
        {
            // empty constructor for Json
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("ToCylinder", toCylinder);
        }

        public void SetObjectData(IJsonReadData data)
        {
            toCylinder = data.GetProperty<ModOp>("ToCylinder");
            toUnit = toCylinder.GetInverse();
        }
    }
}
