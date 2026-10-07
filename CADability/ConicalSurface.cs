using CADability.Curve2D;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// Common interface for <see cref="ConicalSurface"/> and <see cref="ConicalSurfaceNP"/>.
    /// Both surfaces implement only a half cone, which goes from the <see cref="ICone.Apex"/> in the direction of <see cref="ICone.Axis"/>.
    /// </summary>
    public interface ICone
    {
        /// <summary>
        /// The apex of the cone. 
        /// </summary>
        GeoPoint Apex { get; set; }
        /// <summary>
        /// The axis of the cone. Also specifies, which part of the cone is used
        /// </summary>
        GeoVector Axis { get; set; }
        /// <summary>
        /// The full opening angle. Sometimes you need only the half angle
        /// </summary>
        Angle OpeningAngle { get; set; }
    }
    /// <summary>
    /// A conical surface which implements <see cref="ISurface"/>. The surface represents a circular or elliptical
    /// cone. The u parameter always describes a circle or ellipse, the v parameter a Line.
    /// </summary>
    [Serializable()]
    public class ConicalSurface : ISurfaceImpl, ISerializable, IDeserializationCallback, ISurfaceOfRevolution, IExportStep, ICone, IJsonSerialize
    {
        // Der Einheitskegel hat als halben Öffnungswinkel 45°, Der Ursprung ist die Kegelspitze, u geht im Kreis
        // v in die ZRichtung
        private ModOp toCone; // diese ModOp modifiziert den Einheitskegel in den konkreten Kegel
        private ModOp toUnit; // die inverse ModOp zum schnelleren Rechnen
        private double voffset; // OCas arbeitet mit anderem v, ggf. nach dem Einlesen die 2d Kuren verschieben und voffset wieder wegmachen
        public ConicalSurface(GeoPoint apex, GeoVector dirx, GeoVector diry, GeoVector dirz, double semiAngle, double voffset = 0.0)
        {
            double s = Math.Sin(semiAngle);
            double c = Math.Cos(semiAngle);
            this.voffset = voffset / s;
            ModOp m1 = ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis },
                new GeoVector[] { s * dirx, s * diry, c * dirz });
            ModOp m2 = ModOp.Translate(apex - GeoPoint.Origin);
            toCone = m2 * m1;
            toUnit = toCone.GetInverse();
        }
        public ConicalSurface(GeoPoint p1, GeoVector n1, GeoPoint p2, GeoVector n2, GeoPoint p3, GeoVector n3)
        {   // 3 Punkte mit Normalenvektor spezifizieren eine KegelFläche
            Plane pl1 = new Plane(p1, n1);
            Plane pl2 = new Plane(p2, n2);
            Plane pl3 = new Plane(p3, n3);
            GeoPoint loc;
            GeoVector dir12;
            pl1.Intersect(pl2, out loc, out dir12);
            GeoPoint apex = pl3.Intersect(loc, dir12);
            GeoPoint p11 = apex + (p1 - apex).Normalized;
            GeoPoint p22 = apex + (p2 - apex).Normalized;
            GeoPoint p33 = apex + (p3 - apex).Normalized;
            Plane perp = new Plane(p11, p22, p33);
            GeoPoint2D cnt;
            double r;
            Geometry.CircleFitLs(new GeoPoint2D[] { perp.Project(p11), perp.Project(p22), perp.Project(p33) }, out cnt, out r);
            double semiAngle = Math.Atan2(r, 1);
            // noch nicht fertig!!!
        }
        internal ConicalSurface(ModOp toCone, BoundingRect? usedArea = null) : base(usedArea)
        {
            this.toCone = toCone;
            toUnit = toCone.GetInverse();
            voffset = 0.0;
        }
        /// <summary>
        /// The two provided circles must share a common axis and must have different radii. The resulting cone passes through the provided circles.
        /// If the conditions are not met, null will be returned.
        /// </summary>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <returns></returns>
        public static ConicalSurface FromTwoCircles(Ellipse c1, Ellipse c2)
        {
            if (Precision.SameDirection(c1.Normal, c2.Normal, false) && Precision.SameDirection(c1.Normal, c2.Center - c1.Center, false))
            {   // the two circles have a common axis
                // work in the plane where the circles appear as horizontal lines (c1 is the X-Axis) and the normal of the circles is th Y-axis
                if (Math.Abs(c1.Radius - c2.Radius) < Precision.eps) return null; // this would be a cylinder
                if (c1.Radius < c2.Radius) (c1, c2) = (c2, c1);
                Plane pln = new Plane(c1.Center, c1.MajorAxis, c1.Normal);
                GeoPoint2D c2cnt = pln.Project(c2.Center); //c2cnt.x must be 0.0
                double y = c2cnt.y / (c1.Radius - c2.Radius) * c1.Radius;
                GeoPoint apex = pln.ToGlobal(new GeoPoint2D(0, y));
                double semiAngle = Math.Atan2(c1.Radius, y);
                ConicalSurface res = new ConicalSurface(apex, c1.MajorAxis.Normalized, c1.MinorAxis.Normalized, c1.Normal, semiAngle, 0.0);
                return res;
            }
            return null;
        }
        public GeoPoint Location
        {
            get
            {
                return toCone * GeoPoint.Origin;
            }
        }
        public GeoVector Axis
        {
            get
            {
                return toCone * GeoVector.ZAxis;
            }
        }
        public GeoVector XAxis
        {
            get
            {
                return toCone * GeoVector.XAxis;
            }
        }
        public GeoVector YAxis
        {
            get
            {
                return toCone * GeoVector.YAxis;
            }
        }
        public GeoVector ZAxis
        {
            get
            {
                return toCone * GeoVector.ZAxis;
            }
        }
        public Angle OpeningAngle
        {
            get
            {
                return new Angle(toCone * new GeoVector(1, 0, 1), toCone * new GeoVector(-1, 0, 1));
            }
        }
        public Line AxisLine(double vmin, double vmax)
        {
            return Line.TwoPoints(toCone * new GeoPoint(0, 0, vmin), toCone * new GeoPoint(0, 0, vmax));
        }
        #region ISurfaceImpl Overrides
        // im Folgenden noch mehr überschreiben, das hier ist erst der Anfang:
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ISurface GetModified(ModOp m)
        {
            return new ConicalSurface(m * toCone, usedArea);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            uv.y += voffset; // voffset sollte jetzt immer 0 sein. Nein, unmittelbar nach dem Erzeugen aus OCas ist es das nicht und PointAt kommt schon dran
            return toCone * new GeoPoint(uv.y * Math.Cos(uv.x), uv.y * Math.Sin(uv.x), uv.y);
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            if (!Precision.IsNullVector(p - Location) && !Precision.IsPointOnAxis(p, new CADability.Axis(Location, Axis)))
            {
                try
                {
                    Plane pln = new Plane(Location, Axis, p - Location);
                    if (pln.IsValid())
                    {
                        // in this plane the x-axis is the conical axis, the origin is the apex of the cone
                        Angle dira = OpeningAngle / 2.0;
                        // this line through origin with angle dira and -dira are the envelope lines of the cone
                        GeoPoint2D p2d = pln.Project(p);
                        GeoPoint2D fp1 = Geometry.DropPL(p2d, GeoPoint2D.Origin, new GeoVector2D(dira));
                        GeoPoint2D fp2 = Geometry.DropPL(p2d, GeoPoint2D.Origin, new GeoVector2D(-dira));
                        double mindist = double.MaxValue;
                        GeoPoint2D res = GeoPoint2D.Origin;
                        foreach (GeoPoint2D fp in new GeoPoint2D[] { fp1, fp2 })
                        {
                            GeoPoint2D r;
                            GeoPoint pu = toUnit * pln.ToGlobal(fp);
                            if (pu.z < 0.0)
                            {
                                double u = Math.Atan2(-pu.y, -pu.x);
                                if (u < 0) u += Math.PI * 2;
                                r = new GeoPoint2D(u, pu.z - voffset);
                            }
                            else
                            {
                                double u = Math.Atan2(pu.y, pu.x);
                                if (u < 0) u += Math.PI * 2;
                                r = new GeoPoint2D(u, pu.z - voffset);
                            }
                            double d = PointAt(r) | p;
                            if (d < mindist)
                            {
                                mindist = d;
                                res = r;
                            }
                        }
                        return res;
                    }
                }
                catch (PlaneException) { }
            }
            // on the axis (or the apex): any u, v as above
            return new GeoPoint2D(0.0, (toUnit * p).z - voffset);

            // this is the old implementation, which was bad for points outside the surface
            //GeoPoint pu = toUnit * p;
            //if (pu.z < 0.0)
            //{
            //    double u = Math.Atan2(-pu.y, -pu.x);
            //    if (u < 0) u += Math.PI * 2;
            //    return new GeoPoint2D(u, pu.z - voffset);
            //}
            //else
            //{
            //    double u = Math.Atan2(pu.y, pu.x);
            //    if (u < 0) u += Math.PI * 2;
            //    return new GeoPoint2D(u, pu.z - voffset);
            //}
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PerpendicularFoot (GeoPoint)"/>
        /// </summary>
        /// <param name="fromHere"></param>
        /// <returns></returns>
        public override GeoPoint2D[] PerpendicularFoot(GeoPoint fromHere)
        {
            try
            {
                if (!(fromHere - Location).IsNullVector())
                {
                    Plane pln = new Plane(Location, Axis, fromHere - Location);
                    if (pln.IsValid())
                    {
                        // in this plane the x-axis is the conical axis, the origin is the apex of the cone
                        Angle dira = OpeningAngle / 2.0;
                        // this line through origin with angle dira and -dira are the envelope lines of the cone
                        GeoPoint2D fromHere2d = pln.Project(fromHere);
                        GeoPoint2D fp1 = Geometry.DropPL(fromHere2d, GeoPoint2D.Origin, new GeoVector2D(dira));
                        GeoPoint2D fp2 = Geometry.DropPL(fromHere2d, GeoPoint2D.Origin, new GeoVector2D(-dira));
                        return new GeoPoint2D[] { PositionOf(pln.ToGlobal(fp1)), PositionOf(pln.ToGlobal(fp2)) };
                    }
                }
                return new GeoPoint2D[0];
            }
            catch
            {   // fromHere is on the axis
                return new GeoPoint2D[0];
            }
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
            ConicalSurface res = new ConicalSurface(toCone);
            res.usedArea = usedArea;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            boxedSurfaceEx = null;
            toCone = m * toCone;
            toUnit = toCone.GetInverse();
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

            List<ICurve2D> res = new List<ICurve2D>();
            GeoVector dirunit = toUnit * direction; // im Normsystem

            // siehe maximafile cone.max:
            // /* Laden mit: batch("cone.max"); */
            // cone(u,v):= [v * cos(u), v * sin(u), v];
            // conedu(u):= [-sin(u),cos(u),0];
            // conedv(u) := [cos(u)/sqrt(2),sin(u)/sqrt(2),1/sqrt(2)];
            // cross(left,right) := [left[2]*right[3] - left[3]*right[2],left[3]*right[1] - left[1]*right[3],left[1]*right[2] - left[2]*right[1]];
            // dot(a,b) := a[1]*b[1] + a[2]*b[2] + a[3]*b[3];
            // solve(dot(cross(conedu(u),conedv(u)),[dirx,diry,dirz])=0,u);
            // trigreduce(dot(cross(conedu(u),conedv(u)),[dirx,diry,dirz]));
            // /* auf die Idee den Vektor dir in [cos(d), sin(d), dirz] aufzuteilen kommt maxima nicht von alleine */
            // trigreduce(dot(cross(conedu(u), conedv(u)), [cos(d), sin(d), dirz]));		
            // /* folgendes liefert die (Teil-) Loesung, symmetrisch Loesung bezüglich d beachten */				
            // solve(trigreduce(dot(cross(conedu(u), conedv(u)), [cos(d), sin(d), dirz])),u);
            double d = Math.Atan2(dirunit.y, dirunit.x);
            double n = Math.Sqrt(dirunit.x * dirunit.x + dirunit.y * dirunit.y);
            double dirz = dirunit.z / n;
            if (Math.Abs(dirz) < 1.0)
            {   // sonst: Blick von innen in den Kegel, keine Tangentialkontur
                double u1 = Math.Acos(dirz) + d;
                double u2 = d - Math.Acos(dirz);
                if (u1 < umin) u1 += Math.PI * 2.0;
                if (u1 > umax) u1 -= Math.PI * 2.0;
                if (u1 > umin) res.Add(new Line2D(new GeoPoint2D(u1, vmin), new GeoPoint2D(u1, vmax)));
                if (u2 < umin) u2 += Math.PI * 2.0;
                if (u2 > umax) u2 -= Math.PI * 2.0;
                if (u2 > umin) res.Add(new Line2D(new GeoPoint2D(u2, vmin), new GeoPoint2D(u2, vmax)));
            }

            return res.ToArray();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            uv.y += voffset; // voffset is not guaranteed to be 0, step import creates such surfaces
            return toCone * new GeoVector(-uv.y * Math.Sin(uv.x), uv.y * Math.Cos(uv.x), 0.0);
        }
        static double Sqrt2 = Math.Sqrt(2.0);
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            uv.y += voffset; // v-offset is not guaranteed to be 0, step import creates such surfaces
            return toCone * new GeoVector(Math.Cos(uv.x), Math.Sin(uv.x), 1.0);
        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            location = PointAt(uv); // GeoPoint(uv.y * Math.Cos(uv.x), uv.y * Math.Sin(uv.x), uv.y);
            uv.y += voffset; // v-offset is not guaranteed to be 0, step import creates such surfaces
            du = toCone * new GeoVector(-uv.y * Math.Sin(uv.x), uv.y * Math.Cos(uv.x), 0.0);
            dv = toCone * new GeoVector(Math.Cos(uv.x), Math.Sin(uv.x), 1.0);
            duu = toCone * new GeoVector(-uv.y * Math.Cos(uv.x), -uv.y * Math.Sin(uv.x), 0.0);
            dvv = toCone * new GeoVector(0.0, 0.0, 0.0);
            duv = toCone * new GeoVector(-Math.Sin(uv.x), Math.Cos(uv.x), 0.0);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {
            if (uv.y == 0.0) uv.y = 1.0;
            return UDirection(uv) ^ VDirection(uv);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.MakeCanonicalForm ()"/>
        /// </summary>
        /// <returns></returns>
        public override ModOp2D MakeCanonicalForm()
        {
            ModOp2D res = ModOp2D.Translate(0.0, voffset);
            voffset = 0.0;
            return res;
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
            if (Curve3dOfProjected<ConicalSurface>(curve2d) is ICurve onThisSurface) return onThisSurface;
            Line2D l2d = curve2d as Line2D;
            if (l2d != null)
            {
                GeoVector2D v2d = l2d.StartDirection; // v2d ist genormt
                if (Math.Abs(v2d.y) < 1e-8)
                {   // horizontale Linie: Einheitskreis
                    bool direction = v2d.x > 0;
                    Ellipse e = Ellipse.Construct();
                    double y = l2d.StartPoint.y + voffset;
                    // voffset: leider kommt diese Methode schon dran, bevor der Kegel genormt wurde (Ocas Import)
                    // deshalb muss hier noch voffset berücksichtigt werden
                    if (Math.Abs(y) < 1e-6) return null; // a pole
                    Plane pl = new Plane(Plane.StandardPlane.XYPlane, y);
                    e.SetPlaneRadius(pl, y, y);
                    e.StartParameter = new Angle(l2d.StartPoint.x);
                    //e.SetArcPlaneCenterStartEndPoint(Plane.XYPlane, GeoPoint2D.Origin,
                    //    GeoPoint2D.Origin + y * new GeoVector2D(new Angle(l2d.StartPoint.x)),
                    //    GeoPoint2D.Origin + y * new GeoVector2D(new Angle(l2d.EndPoint.x)), pl, direction);
                    double sw = l2d.EndPoint.x - l2d.StartPoint.x;
                    if (sw < -2.0 * Math.PI) sw = -2.0 * Math.PI;
                    if (sw > 2.0 * Math.PI) sw = 2.0 * Math.PI;
                    e.SweepParameter = sw;
                    e.Modify(toCone);
                    // durch die Neuberechnung der Hauptachsen kann der Startparameter sich bei Modify ändern
                    return e;
                    //Polyline pol = Polyline.Construct();
                    //GeoPoint[] pts = new GeoPoint[5];
                    //pts[0] = PointAt(l2d.PointAt(0.0));
                    //pts[1] = PointAt(l2d.PointAt(0.25));
                    //pts[2] = PointAt(l2d.PointAt(0.5));
                    //pts[3] = PointAt(l2d.PointAt(0.75));
                    //pts[4] = PointAt(l2d.PointAt(1.0));
                    //pol.SetPoints(pts,false);
                    //return pol;
                }
                else if (Math.Abs(v2d.x) < 1e-8)
                {   // vertikal, wird eine Linie
                    Line l = Line.Construct();
                    l.SetTwoPoints(PointAt(l2d.StartPoint), PointAt(l2d.EndPoint));
                    return l;
                }
            }
            return base.Make3dCurve(curve2d);
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
                return Math.PI * 2.0;
            }
        }
        public override double VPeriod
        {
            get
            {
                return 0.0;
            }
        }
        public override double[] GetUSingularities()
        {
            return new double[0];
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetVSingularities ()"/>
        /// </summary>
        /// <returns></returns>
        public override double[] GetVSingularities()
        {
            return new double[] { 0.0 };
        }
        public override bool IsRotated(Axis rotationAxis)
        {
            return Precision.SameDirection(Axis, rotationAxis.Direction, false);
        }

        private static double CopySign(double magnitude, double sign)
        {
            return Math.Abs(magnitude) * (sign >= 0 ? 1.0 : -1.0);
        }
        /// <summary>
        /// Finds the intersection points of a line with the cone z^2 = x^2 + y^2.
        /// The line is defined by p and d
        /// Returns 0, 1, or 2 points of intersection.
        /// </summary>
        private static List<GeoPoint> IntersectLineWithUnitCone(GeoPoint p, GeoVector d)
        {
            var result = new List<GeoPoint>();

            // Coefficients of the quadratic equation At² + Bt + C = 0
            double A = d.z * d.z - d.x * d.x - d.y * d.y;
            double B = 2 * (p.z * d.z - p.x * d.x - p.y * d.y);
            double C = p.z * p.z - p.x * p.x - p.y * p.y;

            const double epsilon = 1e-6f;

            if (Math.Abs(A) < epsilon)
            {
                // Degenerate case: linear equation Bt + C = 0
                if (Math.Abs(B) > epsilon)
                {
                    double t = -C / B;
                    result.Add(p + t * d);
                }
                // else: No solution or infinite solutions (line lies on cone surface)
            }
            else
            {
                double discriminant = B * B - 4 * A * C;

                if (discriminant < -epsilon)
                {
                    // No real intersection points
                    return result;
                }
                else if (Math.Abs(discriminant) < epsilon)
                {
                    // One real solution (tangent)
                    double t = -B / (2 * A);
                    result.Add(p + t * d);
                }
                else
                {
                    // Two real solutions, numerically stable version
                    double sqrtD = Math.Sqrt(discriminant);
                    double q = -0.5 * (B + CopySign(sqrtD, B));
                    double t1 = q / A;
                    double t2 = C / q;
                    result.Add(p + t1 * d);
                    result.Add(p + t2 * d);
                }
            }

            return result;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetLineIntersection (GeoPoint, GeoVector)"/>
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {

            // Bringe die Linie in das Einheitssystem und bestimme dann die Punkte, bei denen
            // x^2+y^2 == z^2
            GeoPoint sp = toUnit * startPoint;
            GeoVector dir = toUnit * direction;
            List<GeoPoint> ips = IntersectLineWithUnitCone(sp, dir);
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            for (int i = 0; i < ips.Count; i++) res.Add(PositionOf(toCone * ips[i]));
            return res.ToArray();
            // following code was not numericaly stable:

            //double A = dir.x * dir.x + dir.y * dir.y - dir.z * dir.z;
            //double B = 2 * (sp.x * dir.x + sp.y * dir.y - sp.z * dir.z);
            //double C = sp.x * sp.x + sp.y * sp.y - sp.z * sp.z;
            //if (Math.Abs(B * B - 4 * A * C) < Precision.eps)
            //{   // tangential intersection
            //    return new GeoPoint2D[] { PositionOf(toCone * (sp - B / (2 * A) * dir)) };
            //}
            //// Mit Maxima: 
            //// solve((spz+l*dirz)^2=(spx+l*dirx)^2+(spy+l*diry)^2,l);  string(%);
            //// ergibt sich:
            //// [l = (sqrt((diry^2+dirx^2)*spz^2+(-2*diry*dirz*spy-2*dirx*dirz*spx)*spz+\
            //// (dirz^2-dirx^2)*spy^2+2*dirx*diry*spx*spy+(dirz^2-diry^2)*spx^2)-dirz*spz+diry\
            //// *spy+dirx*spx)/(dirz^2-diry^2-dirx^2),l = -(sqrt((diry^2+dirx^2)*spz^2+(-2*dir\
            //// y*dirz*spy-2*dirx*dirz*spx)*spz+(dirz^2-dirx^2)*spy^2+2*dirx*diry*spx*spy+(dir\
            //// z^2-diry^2)*spx^2)+dirz*spz-diry*spy-dirx*spx)/(dirz^2-diry^2-dirx^2)]
            //double root = (dir.y * dir.y + dir.x * dir.x) * sp.z * sp.z + (-2 * dir.y * dir.z * sp.y - 2 * dir.x * dir.z * sp.x) * sp.z + (dir.z * dir.z - dir.x * dir.x) * sp.y * sp.y + 2 * dir.x * dir.y * sp.x * sp.y + (dir.z * dir.z - dir.y * dir.y) * sp.x * sp.x;
            //double denominator = (dir.z * dir.z - dir.y * dir.y - dir.x * dir.x);
            //if (root >= 0.0 && denominator != 0.0)
            //{
            //    double l1 = (Math.Sqrt(root) - dir.z * sp.z + dir.y * sp.y + dir.x * sp.x) / denominator;
            //    double l2 = -(Math.Sqrt(root) + dir.z * sp.z - dir.y * sp.y - dir.x * sp.x) / denominator;
            //    GeoPoint pl1 = sp + l1 * dir;
            //    GeoPoint pl2 = sp + l2 * dir;
            //    // Mit dem Winkel (u-Parameter) verhält es sich so: im "oberen" Kegel ist es der Winkel
            //    // der x,y Komponente, im "unteren" ist es um PI versetzt. Das Ergebnis soll immer
            //    // im Bereich 0..2*PI sein.
            //    double u1 = Math.Atan2(pl1.y, pl1.x);
            //    if (pl1.z < 0.0) u1 += Math.PI;
            //    if (u1 < 0.0) u1 += Math.PI * 2.0;
            //    double u2 = Math.Atan2(pl2.y, pl2.x);
            //    if (pl2.z < 0.0) u2 += Math.PI;
            //    if (u2 < 0.0) u2 += Math.PI * 2.0;
            //    return new GeoPoint2D[] { new GeoPoint2D(u1, pl1.z), new GeoPoint2D(u2, pl2.z) };
            //}
            //return new GeoPoint2D[0];

            //    // Versuch einer Geometrischen Lösung:
            //    Plane pln = new Plane(sp, dir, dir ^ GeoVector.ZAxis); 
            //    // in dieser Ebene ist unsere gerade die X-Achse und sie schneidet den Kegel
            //    GeoPoint center = pln.Intersect(GeoPoint.Origin, GeoVector.ZAxis);
            //    // in dieser Ebene gibt es eine Ellipse, deren Hauptachse die Projektion der Kegelachse ist
            //    GeoVector2D majoraxis = pln.Project(GeoVector.ZAxis);
            //    GeoVector2D minoraxis = new GeoVector2D(majoraxis.y, -majoraxis.x); // senkrecht dazu
            //    GeoVector majoraxis3d = pln.ToGlobal(majoraxis);
            //    GeoVector minoraxis3d = pln.ToGlobal(minoraxis);
            //    // wir brauchen jetzt die beiden Ebenen die durch die ZAchse und die Ellipsenachsen aufgespannt werden
            //    Plane plnmaj = new Plane(GeoPoint.Origin, majoraxis3d ^ GeoVector.ZAxis, GeoVector.ZAxis);
            //    Plane plnmin = new Plane(GeoPoint.Origin, minoraxis3d ^ GeoVector.ZAxis, GeoVector.ZAxis);
            //    // in diesen beiden Ebenen bestimmen wir die Schnittpunkt der Diagonaln mit den Ellipsenachsen
            //    GeoPoint2D majpnt, minpnt;
            //    bool b1 = Geometry.IntersectLL(GeoPoint2D.Origin, new GeoVector2D(1.0, 1.0), plnmaj.Project(center), plnmaj.Project(majoraxis3d), out majpnt);
            //    bool b2 = Geometry.IntersectLL(GeoPoint2D.Origin, new GeoVector2D(1.0, 1.0), plnmin.Project(center), plnmin.Project(minoraxis3d), out minpnt);
            //    if (b1 && b2)
            //    {
            //        double majrad = Geometry.Dist(plnmaj.Project(center), majpnt);
            //        double minrad = Geometry.Dist(plnmin.Project(center), minpnt);
            //        majoraxis.Norm();
            //        minoraxis.Norm();
            //        Ellipse2D e2d = new Ellipse2D(pln.Project(center), majrad * majoraxis, minrad * minoraxis);
            //        // die Ausgangsgerade ist die X-Achse in pln
            //        GeoPoint2D [] ippln = Geometry.IntersectEL(pln.Project(center), majrad, minrad, majoraxis.Angle, GeoPoint2D.Origin, new GeoPoint2D(1.0, 0.0));
            //        GeoPoint2D[] res = new GeoPoint2D[ippln.Length];
            //        for (int i = 0; i < ippln.Length; ++i)
            //        {
            //            GeoPoint ip3d = pln.ToGlobal(ippln[i]);
            //            res[i] = new GeoPoint2D(Math.Atan2(ip3d.y, ip3d.x), ip3d.z);
            //        }
            //        return res;
            //    }
            //}
            //return new GeoPoint2D[0];
        }
        public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            base.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            umin = 0.0;
            umax = Math.PI * 2.0;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetPlaneIntersection (PlaneSurface, double, double, double, double, double)"/>
        /// The intersection curves are clipped to the provided bounds, which may split a single intersection curve into
        /// several parts (a hyperbola for example may enter and leave the vmin/vmax bounds several times).
        /// </summary>
        /// <param name="pl">the plane to intersect with</param>
        /// <param name="umin">left bound of the relevant part of this surface</param>
        /// <param name="umax">right bound of the relevant part of this surface</param>
        /// <param name="vmin">lower bound of the relevant part of this surface</param>
        /// <param name="vmax">upper bound of the relevant part of this surface</param>
        /// <param name="precision">precision for approximated curves, 0.0: use <see cref="Precision.eps"/></param>
        /// <returns>the intersection curves, may be empty</returns>
        public override IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision)
        {
            // The intersection of a plane and a cone is a conic section (see http://mathworld.wolfram.com/ConicSection.html).
            // Everything is calculated in the unit system of this cone, where the surface is
            //      P(u, z) = (z*cos(u), z*sin(u), z)
            // (z is the surface parameter v shifted by voffset) and the plane is the set of all points p with normal*p == dist.
            // Substituting P(u, z) into the plane equation yields
            //      z * (normal.x*cos(u) + normal.y*sin(u) + normal.z) == dist
            // The bracket only depends on u, we call it g(u) and write it as
            //      g(u) = radius*cos(u-phase) + normal.z,   radius = |(normal.x, normal.y)|, phase = atan2(normal.y, normal.x)
            // so the intersection curve, expressed in the parameter space of this cone, simply is
            //      z(u) = dist / g(u)
            // defined for all u where g(u) != 0. This single formula covers all cases and also yields the classification:
            //      dist == 0:            the plane contains the apex, the intersection are the (at most two) lines at the zeros of g
            //      radius == 0:          g is constant, the intersection is a circle (an ellipse in the real world)
            //      radius <  |normal.z|: g has no zeros, the intersection is a (closed) ellipse
            //      radius == |normal.z|: g has one zero, the intersection is a parabola
            //      radius >  |normal.z|: g has two zeros, the intersection is a hyperbola
            // Ellipses are returned as <see cref="Ellipse"/>, parabolas and hyperbolas as rational quadratic BSplines,
            // which describe these curves exactly.
            Plane pln = new Plane(toUnit * pl.Location, toUnit * pl.DirectionX, toUnit * pl.DirectionY);
            GeoVector normal = pln.Normal; // normalized
            double dist = -pln.Distance(GeoPoint.Origin); // Distance(p) is normal*(p-location), so this is normal*location
            double radius = Math.Sqrt(normal.x * normal.x + normal.y * normal.y);
            double phase = Math.Atan2(normal.y, normal.x);
            // the surface parameter v and the z coordinate of the unit system differ by voffset (which usually is 0.0)
            double zmin = Math.Min(vmin, vmax) + voffset;
            double zmax = Math.Max(vmin, vmax) + voffset;
            // The domain for a ProjectedCurve is the u interval of the curve itself, not the u bounds of the surface:
            // a curve which crosses the periodic seam would otherwise jump by a full period in the middle.
            BoundingRect ArcDomain(double ua, double ub) => new BoundingRect(ua, Math.Min(vmin, vmax), ub, Math.Max(vmin, vmax));
            if (precision <= 0.0) precision = Precision.eps;

            double G(double u) => radius * Math.Cos(u - phase) + normal.z;
            GeoPoint UnitPoint(double u, double z) => new GeoPoint(z * Math.Cos(u), z * Math.Sin(u), z);
            GeoPoint PointAtU(double u) => toCone * UnitPoint(u, dist / G(u));
            GeoVector DirectionAtU(double u)
            {   // the derivative of (z(u)*cos(u), z(u)*sin(u), z(u)) with z(u) == dist/g(u) and g'(u) == -radius*sin(u-phase)
                double g = G(u);
                double z = dist / g;
                double dz = dist * radius * Math.Sin(u - phase) / (g * g);
                return toCone * new GeoVector(dz * Math.Cos(u) - z * Math.Sin(u), dz * Math.Sin(u) + z * Math.Cos(u), dz);
            }
            GeoVector2D OnPlane(GeoVector dir) => (pl.ToXYPlane * dir).To2D(); // a direction in the 2d system of the plane
            // all values congruent to u modulo 2*pi which are inside the u bounds. When the bounds cover exactly one
            // period, only the first value is returned, because umin and umax describe the same surface line.
            IEnumerable<double> InUBounds(double u)
            {
                double res = u + Math.Ceiling((umin - u) / (2 * Math.PI)) * 2 * Math.PI; // the first value >= umin
                if (res > umax) yield break;
                yield return res;
                for (res += 2 * Math.PI; res <= umax && umax - umin > 2 * Math.PI + 1e-10; res += 2 * Math.PI) yield return res;
            }

            List<IDualSurfaceCurve> result = new List<IDualSurfaceCurve>();
            if (Math.Abs(pl.Plane.Distance(Location)) < Precision.eps)
            {   // the plane contains the apex: the intersection consists of the lines at the zeros of g,
                // i.e. cos(u-phase) == -normal.z/radius. If g has no zeros, the apex is the only common point.
                if (radius > 0.0 && Math.Abs(normal.z) <= radius)
                {
                    double delta = Math.Acos(Math.Max(-1.0, Math.Min(1.0, -normal.z / radius)));
                    List<double> us = new List<double>(InUBounds(phase - delta));
                    // if the two solutions describe the same line, the plane touches the cone and there is only one line
                    double vfar = Math.Abs(vmin) > Math.Abs(vmax) ? vmin : vmax;
                    if ((PointAt(new GeoPoint2D(phase - delta, vfar)) | PointAt(new GeoPoint2D(phase + delta, vfar))) > precision) us.AddRange(InUBounds(phase + delta));
                    foreach (double u in us)
                    {
                        GeoPoint sp = PointAt(new GeoPoint2D(u, vmin));
                        GeoPoint ep = PointAt(new GeoPoint2D(u, vmax));
                        if (Precision.IsEqual(sp, ep)) continue;
                        Line line = Line.TwoPoints(sp, ep);
                        Line2D onCone = new Line2D(new GeoPoint2D(u, vmin), new GeoPoint2D(u, vmax));
                        Line2D onPlane = new Line2D(pl.PositionOf(sp), pl.PositionOf(ep));
                        result.Add(new DualSurfaceCurve(line, this, onCone, pl, onPlane));
                    }
                }
                return result.ToArray();
            }

            // Find the u values which separate the parts of the curve inside the bounds from those outside: these are the
            // poles of z(u) (where g(u) == 0) and the parameters where z(u) == zmin or z(u) == zmax. Between two such values
            // the curve is either completely inside or completely outside the bounds.
            List<double> breaks = new List<double> { umin, umax };
            void AddUWhereGIs(double value)
            {   // add all u inside the u bounds with g(u) == value
                if (radius == 0.0) return; // g is constant, there is nothing to solve
                double cosDelta = (value - normal.z) / radius;
                if (cosDelta < -1.0 || cosDelta > 1.0) return; // g never has this value
                double delta = Math.Acos(cosDelta);
                breaks.AddRange(InUBounds(phase - delta));
                breaks.AddRange(InUBounds(phase + delta));
            }
            AddUWhereGIs(0.0); // the poles, where z(u) is infinite
            if (zmin != 0.0) AddUWhereGIs(dist / zmin);
            if (zmax != 0.0) AddUWhereGIs(dist / zmax);
            breaks.Sort();
            List<(double from, double to)> inside = new List<(double, double)>();
            for (int i = 0; i < breaks.Count - 1; i++)
            {
                double ua = breaks[i], ub = breaks[i + 1];
                if (ub - ua < 1e-10) continue; // an empty interval
                double g = G((ua + ub) / 2.0);
                if (g == 0.0) continue;
                double z = dist / g;
                if (z < zmin || z > zmax) continue; // this part of the curve is outside the bounds
                if (inside.Count > 0 && inside[inside.Count - 1].to == ua) inside[inside.Count - 1] = (inside[inside.Count - 1].from, ub);
                else inside.Add((ua, ub)); // the curve only touches the bounds here, no need to split it
            }
            if (inside.Count > 1 && umax - umin >= 2 * Math.PI - 1e-8 && inside[0].from == umin && inside[inside.Count - 1].to == umax)
            {   // the u bounds cover a full period and the curve crosses the periodic seam: join the two parts
                (double from, double to) first = inside[0], last = inside[inside.Count - 1];
                inside.RemoveAt(inside.Count - 1);
                inside.RemoveAt(0);
                inside.Insert(0, (last.from, first.to + 2 * Math.PI));
            }

            // The intersection curve is an ellipse when g has no zeros. When it is extremely elongated (i.e. almost a parabola)
            // an arc of it is better described by a rational BSpline, which is exact in all these cases.
            bool isEllipse = radius < Math.Abs(normal.z);
            bool useEllipse = radius < Math.Abs(normal.z) * (1.0 - 1e-6);
            foreach ((double ua, double ub) in inside)
            {
                bool closed = isEllipse && ub - ua >= 2 * Math.PI - 1e-8;
                if (!closed && (PointAtU(ua) | PointAtU(ub)) < precision && ub - ua < Math.PI) continue; // a negligible sliver
                IDualSurfaceCurve dsc = (closed || useEllipse) ? EllipseIntersection(ua, ub, closed) : ConicIntersection(ua, ub);
                if (dsc != null) result.Add(dsc);
            }
            return result.ToArray();

            // Creates the intersection curve for the case of a circle or an ellipse, clipped to ua...ub.
            IDualSurfaceCurve EllipseIntersection(double ua, double ub, bool closed)
            {
                // The vertices of the major axis are the extreme values of z(u), which are at u == phase and u == phase+pi.
                GeoPoint p1 = UnitPoint(phase, dist / (normal.z + radius));
                GeoPoint p2 = UnitPoint(phase + Math.PI, dist / (normal.z - radius));
                GeoPoint center = new GeoPoint(p1, p2);
                GeoVector majorAxis = p1 - center;
                GeoVector minorAxis;
                if (radius == 0.0) minorAxis = new GeoVector(0.0, center.z, 0.0); // a circle around the axis of the cone
                else
                {   // the endpoints of the minor axis are the two points where z(u) equals the z of the center. There
                    // z(u) == dist/(normal.z*normal.z-radius*radius)*normal.z holds, which yields cos(u-phase) == -radius/normal.z
                    minorAxis = UnitPoint(phase + Math.Acos(-radius / normal.z), center.z) - center;
                }
                Ellipse elli = Ellipse.Construct();
                elli.SetEllipseCenterAxis(center, majorAxis, minorAxis); // in the unit system the axes are perpendicular
                elli.Modify(toCone); // toCone may distort, Modify determines the principal axes of the resulting ellipse
                double startParameter = elli.ParameterOf(PointAtU(ua));
                double sweep = closed ? 2 * Math.PI : PositiveAngle(elli.ParameterOf(PointAtU(ub)) - startParameter);
                // the ellipse must run in the direction of increasing u
                GeoVector startDirection = elli.Plane.ToGlobal(new GeoVector2D(-elli.MajorRadius * Math.Sin(startParameter), elli.MinorRadius * Math.Cos(startParameter)));
                if (startDirection * DirectionAtU(ua) < 0.0) sweep = closed ? -2 * Math.PI : sweep - 2 * Math.PI;
                elli.StartParameter = startParameter;
                elli.SweepParameter = sweep;
                ICurve2D onCone;
                if (radius == 0.0) onCone = new Line2D(new GeoPoint2D(ua, center.z - voffset), new GeoPoint2D(ub, center.z - voffset));
                else if (closed)
                {   // a closed curve cannot be described by a ProjectedCurve, so approximate the (analytically known)
                    // curve in the parameter space. The precision is the same a ProjectedCurve uses internally.
                    double uvPrecision = Math.Max(elli.Length * 1e-5, Precision.eps);
                    onCone = BSpline2D.Approximate(t => new GeoPoint2D(ua + t * (ub - ua), dist / G(ua + t * (ub - ua)) - voffset), uvPrecision);
                }
                else onCone = new ProjectedCurve(elli, this, true, ArcDomain(ua, ub)); // a ProjectedCurve cannot describe a closed curve
                return new DualSurfaceCurve(elli, this, onCone, pl, pl.GetProjectedCurve(elli, 0.0));
            }

            // Creates the intersection curve for the case of a parabola or a hyperbola (or a very elongated ellipse),
            // clipped to ua...ub. The curve is built in the 2d system of the plane as a rational quadratic BSpline, which
            // describes a conic section exactly. Each of its segments spans less than 90 degrees of the tangent direction.
            IDualSurfaceCurve ConicIntersection(double ua, double ub)
            {
                double turn = 0.0; // the total change of the tangent direction, sampled, because it may exceed 180 degrees
                GeoVector2D lastDirection = OnPlane(DirectionAtU(ua));
                for (int i = 1; i <= 8; i++)
                {
                    GeoVector2D dir = OnPlane(DirectionAtU(ua + i * (ub - ua) / 8.0));
                    turn += Math.Abs(new SweepAngle(lastDirection, dir));
                    lastDirection = dir;
                }
                int numSegments = Math.Max(1, (int)Math.Ceiling(turn / (Math.PI / 2.0)));
                GeoPoint2D[] poles = new GeoPoint2D[2 * numSegments + 1];
                double[] weights = new double[2 * numSegments + 1];
                double[] knots = new double[numSegments + 1];
                int[] multiplicities = new int[numSegments + 1];
                poles[0] = pl.PositionOf(PointAtU(ua));
                weights[0] = 1.0;
                knots[0] = 0.0;
                multiplicities[0] = 3;
                for (int i = 0; i < numSegments; i++)
                {
                    double su = ua + i * (ub - ua) / numSegments;
                    double eu = ua + (i + 1) * (ub - ua) / numSegments;
                    GeoPoint2D startPoint = poles[2 * i];
                    GeoPoint2D endPoint = pl.PositionOf(PointAtU(eu));
                    // the middle pole of a conic segment is the intersection of the tangents at its endpoints
                    if (!Geometry.IntersectLL(startPoint, OnPlane(DirectionAtU(su)), endPoint, OnPlane(DirectionAtU(eu)), out GeoPoint2D middlePole)) return ApproximatedIntersection(ua, ub);
                    double weight = ConicWeight(startPoint, middlePole, endPoint, pl.PositionOf(PointAtU((su + eu) / 2.0)));
                    if (!(weight > 0.0)) return ApproximatedIntersection(ua, ub);
                    poles[2 * i + 1] = middlePole;
                    weights[2 * i + 1] = weight;
                    poles[2 * i + 2] = endPoint;
                    weights[2 * i + 2] = 1.0;
                    knots[i + 1] = (i + 1.0) / numSegments;
                    multiplicities[i + 1] = 2;
                }
                multiplicities[numSegments] = 3;
                BSpline2D onPlane = new BSpline2D(poles, weights, knots, multiplicities, 2, false, 0.0, 1.0);
                ICurve curve3d = pl.Make3dCurve(onPlane);
                return new DualSurfaceCurve(curve3d, this, new ProjectedCurve(curve3d, this, true, ArcDomain(ua, ub)), pl, onPlane);
            }

            // Fallback for ConicIntersection: an approximation of the intersection curve in the 2d system of the plane.
            IDualSurfaceCurve ApproximatedIntersection(double ua, double ub)
            {
                BSpline2D onPlane = BSpline2D.Approximate(t => pl.PositionOf(PointAtU(ua + t * (ub - ua))), precision);
                if (onPlane == null) return null;
                ICurve curve3d = pl.Make3dCurve(onPlane);
                return new DualSurfaceCurve(curve3d, this, new ProjectedCurve(curve3d, this, true, ArcDomain(ua, ub)), pl, onPlane);
            }
        }
        /// <summary>
        /// Returns the angle in the range 0...2*pi
        /// </summary>
        private static double PositiveAngle(double a)
        {
            a = a % (2 * Math.PI);
            if (a < 0.0) a += 2 * Math.PI;
            return a;
        }
        /// <summary>
        /// Returns the weight of the middle pole of the rational quadratic Bezier curve which is defined by the three poles
        /// (with the weights 1, w, 1) and passes through <paramref name="onCurve"/>. Returns 0.0 if there is no such curve.
        /// </summary>
        private static double ConicWeight(GeoPoint2D pole0, GeoPoint2D pole1, GeoPoint2D pole2, GeoPoint2D onCurve)
        {
            // The barycentric coordinates of a point of the curve with respect to the triangle of the poles are proportional
            // to ((1-t)^2, 2*w*t*(1-t), t^2). So with the barycentric coordinates (alpha, beta, gamma) of onCurve
            // beta/(2*sqrt(alpha*gamma)) == w holds, independent of the (unknown) parameter t.
            double Cross(GeoPoint2D a, GeoPoint2D b, GeoPoint2D c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            double total = Cross(pole0, pole1, pole2);
            if (total == 0.0) return 0.0; // the poles are collinear
            double alpha = Cross(onCurve, pole1, pole2) / total;
            double beta = Cross(pole0, onCurve, pole2) / total;
            double gamma = Cross(pole0, pole1, onCurve) / total;
            if (alpha <= 0.0 || beta <= 0.0 || gamma <= 0.0) return 0.0; // onCurve is not inside the triangle of the poles
            return beta / (2.0 * Math.Sqrt(alpha * gamma));
        }
        public override void Intersect(ICurve curve, BoundingRect uvExtent, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {
            if (curve is Line line)
            {
                GeoPoint2D[] ip2d = GetLineIntersection(line.StartPoint, line.StartDirection);
                ips = new GeoPoint[ip2d.Length];
                uvOnFaces = new GeoPoint2D[ip2d.Length];
                uOnCurve3Ds = new double[ip2d.Length];
                for (int i = 0; i < ip2d.Length; i++)
                {
                    ips[i] = PointAt(ip2d[i]);
                    uvOnFaces[i] = ip2d[i];
                    uOnCurve3Ds[i] = curve.PositionOf(ips[i]);
                }
                return;
            }
            else if (curve.GetPlanarState() == PlanarState.Planar)
            {   // planar intersections of the cone are simple. If the other curve is also planar, we can do it in 2D
                Plane pl = curve.GetPlane();
                IDualSurfaceCurve[] dsc = GetPlaneIntersection(new PlaneSurface(pl), uvExtent.Left, uvExtent.Right, uvExtent.Bottom, uvExtent.Top, Precision.eps);

                if (dsc != null && dsc.Length > 0) // normally an ellipse or parabola, but also two lines
                {
                    List<GeoPoint> lips = [];
                    List<GeoPoint2D> luvOnFaces = [];
                    List<double> luOnCurve3Ds = [];
                    for (int j = 0; j < dsc.Length; j++)
                    {
                        ICurve2D c2dcone = dsc[j].Curve3D.GetProjectedCurve(pl);
                        ICurve2D c2d = curve.GetProjectedCurve(pl);
                        GeoPoint2DWithParameter[] ips2d = c2d.Intersect(c2dcone);
                        if (ips2d != null)
                        {
                            for (int i = 0; i < ips2d.Length; i++)
                            {
                                lips.Add(pl.ToGlobal(ips2d[i].p));
                                luvOnFaces.Add(this.PositionOf(lips.Last()));
                                luOnCurve3Ds.Add(curve.PositionOf(lips.Last()));
                            }
                        }
                    }
                    ips = lips.ToArray();
                    uvOnFaces = luvOnFaces.ToArray();
                    uOnCurve3Ds = luOnCurve3Ds.ToArray();
                    return;
                }
            }
            base.Intersect(curve, uvExtent, out ips, out uvOnFaces, out uOnCurve3Ds);
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            ConicalSurface cc = CopyFrom as ConicalSurface;
            if (cc != null)
            {
                this.toCone = cc.toCone;
                this.toUnit = cc.toUnit;
                this.voffset = 0.0;
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
            GeoPoint q = toUnit * p;
            return q.x * q.x + q.y * q.y - q.z * q.z;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.HitTest (BoundingCube, out GeoPoint2D)"/>
        /// </summary>
        /// <param name="bc"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override bool HitTest(BoundingCube bc, out GeoPoint2D uv)
        {
            // any vertex of the cube on the cone?
            uv = GeoPoint2D.Origin;
            if (bc.Contains(this.Location))
            {
                // the center of the cube is inside the box. (0.0) is a valid point
                return true;
            }
            GeoPoint[] cube = bc.Points;
            bool[] pos = new bool[8];
            for (int i = 0; i < 8; ++i)
            {
                GeoPoint p = cube[i];
                GeoPoint q = toUnit * p;
                if (Math.Abs(q.x * q.x + q.y * q.y - q.z * q.z) < Precision.eps)
                {
                    uv = PositionOf(p);
                    return true;
                }
                pos[i] = Orientation(p) < 0;
            }

            // any line of the cube interfering the cone?
            int[,] l = bc.LineNumbers;
            for (int k = 0; k < 12; ++k)
            {
                int i = l[k, 0];
                int j = l[k, 1];
                GeoPoint2D[] erg = GetLineIntersection(cube[i], cube[j] - cube[i]);
                for (int m = 0; m < erg.Length; ++m)
                {
                    GeoPoint gp = PointAt(erg[m]);
                    if (bc.Contains(gp) || bc.IsOnBounds(gp, bc.Size * 1e-8))
                    {
                        uv = erg[m];
                        return true;
                    }
                }
                //if (pos[i] != pos[j])
                //    throw new ApplicationException("internal error: ConicalSurface.HitTest");
            }

            // cube´s vertices within the surface?
            if (pos[0] && pos[1] && pos[2] && pos[3] && pos[4] && pos[5] && pos[6] && pos[7])
                return false;   //convexity of the inner points in both single cones
            //if the cube´s vertices within both cones, there would be a lineintersection 

            // complete cone is outside of the cube?
            if (!bc.Interferes(Location, Axis, 0, false))
                return false;   //all vertices of the cube are out of the cone

            // now every mantleline goes through the complete cube
            double d = (toUnit * bc.GetCenter()).z;
            uv = PositionOf(toCone * (new GeoPoint(d, 0, d)));
            return true;
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
            double[,] m = toCone.Matrix;
            double[] res = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            for (int i = 0; i < 2; ++i)
            {
                res[0] += m[i, 0] * m[i, 0]; res[1] += m[i, 1] * m[i, 1]; res[2] += m[i, 2] * m[i, 2];
                res[3] += 2 * m[i, 0] * m[i, 1]; res[4] += 2 * m[i, 1] * m[i, 2]; res[5] += 2 * m[i, 0] * m[i, 2];
                res[6] += 2 * m[i, 0] * m[i, 3]; res[7] += 2 * m[i, 1] * m[i, 3]; res[8] += 2 * m[i, 2] * m[i, 3];
                res[9] += m[i, 3] * m[i, 3];
            }
            res[0] -= m[2, 0] * m[2, 0]; res[1] -= m[2, 1] * m[2, 1]; res[2] -= m[2, 2] * m[2, 2];
            res[3] -= 2 * m[2, 0] * m[2, 1]; res[4] -= 2 * m[2, 1] * m[2, 2]; res[5] -= 2 * m[2, 0] * m[2, 2];
            res[6] -= 2 * m[2, 0] * m[2, 3]; res[7] -= 2 * m[2, 1] * m[2, 3]; res[8] -= 2 * m[2, 2] * m[2, 3];
            res[9] -= m[2, 3] * m[2, 3];
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.ReverseOrientation ()"/>
        /// </summary>
        /// <returns></returns>
        public override ModOp2D ReverseOrientation()
        {
            boxedSurfaceEx = null;
            toCone = toCone * new ModOp(1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1, 0); // umkehrung von x
            toUnit = toCone.GetInverse();
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
            e.SetCirclePlaneCenterRadius(new Plane(Plane.XYPlane, v), new GeoPoint(0, 0, v), v);
            e.StartParameter = umin;
            e.SweepParameter = umax - umin;
            e.Modify(toCone);
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
            if (other is ConicalSurface)
            {
                ConicalSurface cother = other as ConicalSurface;
                // ist die folgende Bedingung nicht zu streng?
                if (Precision.SameDirection(ZAxis, cother.ZAxis, false) &&
                    (Geometry.DistPL(cother.Location, Location, ZAxis.Normalized) < precision) &&
                    Precision.IsEqual(OpeningAngle, cother.OpeningAngle))
                {   // the transformation can only be a translation in u-direction and possibly a mirroring in u-direction

                    GeoPoint2D pu0 = cother.PositionOf(PointAt(new GeoPoint2D(0.0, 1.0)));
                    GeoPoint2D pu1 = cother.PositionOf(PointAt(new GeoPoint2D(Math.PI / 2, 1.0)));
                    if (pu0.x - pu1.x < -Math.PI) pu1.x -= 2 * Math.PI;
                    if (pu0.x - pu1.x > Math.PI) pu0.x -= 2 * Math.PI;
                    // a*0.0 + b = pu0.x
                    // a*(PI/2) + b = pu1.x
                    double b = pu0.x;
                    double a = (pu1.x - pu0.x) / (Math.PI / 2);
                    // pu.y muss 1.0 sein, oder?
                    if (Math.Abs(pu0.y - 1.0) < 1e-7 && Math.Abs(pu0.y - 1.0) < 1e-7 && Math.Abs(Math.Abs(a) - 1) < 1e-7)
                    {   // this should always be the case
                        firstToSecond = new ModOp2D(a, 0.0, b, 0.0, 1.0, 0.0);
                        return true;
                    }
                    else
                    {
                        firstToSecond = ModOp2D.Null;
                        return false;
                    }
                }
                else
                {   // if it failed, check the four cornerpoints on the other surface
                    // (if they are different, the first check will fail in most cases
                    // the following did not reflct inversion of the cone
                    firstToSecond = ModOp2D.Null;
                    GeoPoint pt = PointAt(thisBounds.GetLowerLeft());
                    GeoPoint po = cother.PointAt(cother.PositionOf(pt));
                    if ((pt | po) > precision) return false;
                    pt = PointAt(thisBounds.GetLowerRight());
                    po = cother.PointAt(cother.PositionOf(pt));
                    if ((pt | po) > precision) return false;
                    pt = PointAt(thisBounds.GetUpperLeft());
                    po = cother.PointAt(cother.PositionOf(pt));
                    if ((pt | po) > precision) return false;
                    pt = PointAt(thisBounds.GetUpperRight());
                    po = cother.PointAt(cother.PositionOf(pt));
                    if ((pt | po) > precision) return false;

                    po = cother.PointAt(otherBounds.GetLowerLeft());
                    pt = PointAt(PositionOf(po));
                    if ((pt | po) > precision) return false;
                    po = cother.PointAt(otherBounds.GetLowerRight());
                    pt = PointAt(PositionOf(po));
                    if ((pt | po) > precision) return false;
                    po = cother.PointAt(otherBounds.GetUpperLeft());
                    pt = PointAt(PositionOf(po));
                    if ((pt | po) > precision) return false;
                    po = cother.PointAt(otherBounds.GetUpperRight());
                    pt = PointAt(PositionOf(po));
                    if ((pt | po) > precision) return false;

                    // all points fit on the other surface within precision
                    GeoPoint2D pu0 = cother.PositionOf(PointAt(new GeoPoint2D(0.0, 1.0)));
                    GeoPoint2D pu1 = cother.PositionOf(PointAt(new GeoPoint2D(Math.PI / 2, 1.0)));
                    if (pu0.x - pu1.x < -Math.PI) pu1.x += 2 * Math.PI;
                    if (pu0.x - pu1.x > Math.PI) pu0.x -= 2 * Math.PI;
                    // a*0.0 + b = pu0.x
                    // a*(PI/2) + b = pu1.x
                    double b = pu0.x;
                    double a = (pu1.x - pu0.x) / (Math.PI / 2);
                    // pu.y muss 1.0 sein, oder?
                    if (Math.Abs(pu0.y - 1.0) < 1e-7 && Math.Abs(pu0.y - 1.0) < 1e-7 && Math.Abs(Math.Abs(a) - 1) < 1e-7)
                    {   // this should always be the case
                        firstToSecond = new ModOp2D(a, 0.0, b, 0.0, 1.0, 0.0);
                        return true;
                    }
                    else
                    {
                        firstToSecond = ModOp2D.Null;
                        return false;
                    }
                }
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public override RuledSurfaceMode IsRuled
        {
            get
            {
                return RuledSurfaceMode.ruledInV;
            }
        }
        public override double MaxDist(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp)
        {
            mp = new GeoPoint2D(sp, ep);
            return Geometry.DistPL(PointAt(mp), PointAt(sp), PointAt(ep));
        }

        /// <summary>
        /// Which of the two nappes of the cone this surface is used on. The parameter v is the signed distance
        /// from the apex along a surface line, and it never changes its sign within a face: the used area lies
        /// either completely on one side of the apex or completely on the other. Both nappes are oriented
        /// outward, so the same offset has to move the apex in opposite directions on them.
        /// <see cref="ISurfaceImpl.Domain"/> is the only place that carries this information; when it is empty
        /// - a surface that is not bound to a face - the positive nappe is assumed.
        /// </summary>
        private bool OnPositiveNappe => usedArea.IsEmpty() || usedArea.IsInfinite || usedArea.IsInvalid() || usedArea.GetCenter().y + voffset >= 0.0;
        public override ISurface GetOffsetSurface(double offset)
        {
            return GetOffsetSurface(offset, out ModOp2D dumy);
        }
        public override ISurface GetOffsetSurface(double offset, out ModOp2D mod)
        {
            // The offset surface of a cone is the same cone with its apex moved along the axis: moving the
            // surface outward by "offset" perpendicular to itself grows the radius at a fixed axial position by
            // offset/cos(semiAngle), which is exactly what shifting the apex by offset/sin(semiAngle) does. On
            // the negative nappe "outward" points the other way round the axis, hence the sign.
            double oa2 = OpeningAngle / 2.0;
            double signedOffset = OnPositiveNappe ? offset : -offset;
            // sin(oa2) = signedOffset/l
            double l = signedOffset / Math.Sin(oa2);
            GeoPoint apex = Location - l * ZAxis.Normalized;
            ConicalSurface res = new ConicalSurface(apex, XAxis.Normalized, YAxis.Normalized, ZAxis.Normalized, oa2, 0);
            // The offset surface uses the same u and a shifted v: a point at the distance (v+voffset) from the
            // apex ends up at the distance (v+voffset) + signedOffset/tan(semiAngle) from the new apex, and the
            // result has no voffset of its own, so that shift is part of the mapping as well.
            mod = ModOp2D.Translate(0.0, voffset + signedOffset / Math.Tan(oa2));
            return res;
        }
        public override ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {   // should better use Surfaces.Intersect
            if (other is PlaneSurface ps)
            {
                IDualSurfaceCurve[] dsc = GetPlaneIntersection(ps, thisBounds.Left, thisBounds.Right, thisBounds.Bottom, thisBounds.Top, 0.0);
                ICurve[] res = new ICurve[dsc.Length];
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = dsc[i].Curve3D;
                }
                return res;
            }
            if (other is ISurfaceOfRevolution rev)
            {
                List<ICurve> res = new List<ICurve>();
                if (Precision.SameAxis(rev.Axis, (this as ISurfaceOfRevolution).Axis))
                {   // two surfaces of revolution with the same axis
                    Intersect(rev.Curve, thisBounds, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds);
                    for (int i = 0; i < uvOnFaces.Length; i++)
                    {
                        res.Add(FixedV(uvOnFaces[i].y, thisBounds.Left, thisBounds.Right));
                    }
                }
                if (res.Count > 0) return res.ToArray();
            }
            return base.Intersect(thisBounds, other, otherBounds);
        }
        public override ISurface GetNonPeriodicSurface(ICurve[] orientedCurves)
        {
            ConicalSurfaceNP res = new ConicalSurfaceNP(Location, XAxis, YAxis, ZAxis);
            GeoPoint testPoint = orientedCurves[0].PointAt(0.5); // any point except the apex
            // we need the zAxis to have positive values for all points (a face with a conical surface never contains parts from both sides of the apex)
            double lp = Geometry.LinePar(Location, ZAxis, testPoint);
            if (lp < 0) res = new ConicalSurfaceNP(Location, XAxis, YAxis, -ZAxis);
            //GeoPoint testPoint1 = res.PointAt(res.PositionOf(testPoint));
            //if (PositionOf(testPoint).y<0) res = new ConicalSurfaceNP(Location, XAxis, YAxis, -ZAxis);
            GeoVector normalOriginal = GetNormal(PositionOf(testPoint));
            GeoVector normalNp = res.GetNormal(res.PositionOf(testPoint));
            if (normalOriginal * normalNp < 0) res.ReverseOrientation(); // make the same orientation as this conical surface
            return res;
        }
        public override IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> extremePositions)
        {
            if (other is PlaneSurface)
            {
                return GetPlaneIntersection(other as PlaneSurface, thisBounds.Left, thisBounds.Right, thisBounds.Bottom, thisBounds.Top, Precision.eps);

            }
            return base.GetDualSurfaceCurves(thisBounds, other, otherBounds, seeds, extremePositions);
        }
        public override ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {
            ICurve2D res = null;
            if (curve is Ellipse elli)
            {   // we need the projected curve where the curve itself might have some distance to the cone. The projection must be perpendicular
                if (Geometry.DistPL(elli.Center, Location, Axis) < Precision.eps)
                {   // the center of the ellipse is located on the cones axis
                    if (Precision.SameDirection(elli.Normal, Axis, false))
                    {   // and it is aligned to the axis
                        // we need the perpendicular projection of some ellipse point onto the cone. PositionOf is not perpendicular!
                        GeoPoint2D sp = GeoPoint2D.Origin, ep = GeoPoint2D.Origin, mp = GeoPoint2D.Origin;
                        GeoPoint2D[] pf = PerpendicularFoot(elli.StartPoint); // this always yields 2 points
                        if (pf.Length == 2)
                        {
                            if ((PointAt(pf[0]) | elli.StartPoint) < (PointAt(pf[1]) | elli.StartPoint)) sp = pf[0];
                            else sp = pf[1];
                        }
                        pf = PerpendicularFoot(elli.EndPoint);
                        if (pf.Length == 2)
                        {
                            if ((PointAt(pf[0]) | elli.EndPoint) < (PointAt(pf[1]) | elli.EndPoint)) ep = pf[0];
                            else ep = pf[1];
                        }
                        pf = PerpendicularFoot(elli.PointAt(0.5));
                        if (pf.Length == 2)
                        {
                            if ((PointAt(pf[0]) | elli.PointAt(0.5)) < (PointAt(pf[1]) | elli.PointAt(0.5))) mp = pf[0];
                            else mp = pf[1];
                        }
                        // now we need a (horizontal) line from sp to ep crossing mp
                        if (Math.Abs(mp.x - sp.x) > Math.PI)
                        {
                            if (sp.x < mp.x) sp.x += Math.PI * 2;
                            else sp.x -= Math.PI * 2;
                        }
                        if (Math.Abs(mp.x - ep.x) > Math.PI)
                        {
                            if (ep.x < mp.x) ep.x += Math.PI * 2;
                            else ep.x -= Math.PI * 2;
                        }
                        if (elli.IsClosed)
                        {   // a full circle
                            if (Math.Abs(sp.x - ep.x) < Math.PI) sp.x -= Math.PI * 2;
                            // we need to consider the direction of the line
                            GeoVector2D dir2d = ep - sp;
                            GeoVector dir = dir2d.x * UDirection(sp) + dir2d.y * VDirection(sp);
                            if (elli.StartDirection * dir < 0)
                            {   // reverse the line
                                mp = sp;
                                sp = ep;
                                ep = mp;
                            }
                        }
                        res = new Line2D(sp, ep);
                        return res;
                    }
                }
            }
            ICurve crvunit = curve.CloneModified(toUnit);
            if (crvunit is Line l)
            {
                if (Geometry.DistPL(GeoPoint.Origin, l.StartPoint, l.EndPoint) < Precision.eps)
                {   // not yet tested
                    GeoPoint mp = l.PointAt(0.5);
                    double u = Math.Atan2(mp.y, mp.x);
                    if (l.StartPoint.z + l.EndPoint.z < 0) u += Math.PI; // start- or endpoint could be 0, crossing z=0 is not allowed
                    if (u < 0.0) u += 2.0 * Math.PI;
                    res = new Line2D(new GeoPoint2D(u, l.StartPoint.z - voffset), new GeoPoint2D(u, l.EndPoint.z - voffset));
                    return res;
                }
                else
                {
                    GeoVector v2 = l.StartPoint - GeoPoint.Origin;
                    GeoVector v3 = l.EndPoint - GeoPoint.Origin;
                    if (Math.Abs((v3 ^ v2).z) < Precision.eps)
                    {   // line and axis are in a common plane
                        GeoPoint2D p1 = PositionOf(curve.StartPoint);
                        GeoPoint2D p2 = PositionOf(curve.EndPoint);
                        SurfaceHelper.AdjustPeriodicStartPoint(this, p1, ref p2);
                        res = new Line2D(p1, p2);
                        return res;
                    }
                }
            }
            else if (crvunit is Ellipse)
            {
                Ellipse e = (crvunit as Ellipse);
                if (Precision.SameDirection(GeoVector.ZAxis, e.Plane.Normal, true))
                {   // es wird immer davon ausgegangen, dass curve sehr nahe am Zylinder liegt, also aus einem Schnitt
                    // oder einer anderen Berechnung kommt. Es wird auch erwartet, dass ein Bogen nicht über den Saum geht.
                    bool forward = Math.Sign(e.SweepParameter) == Math.Sign(e.Plane.Normal.z);
                    double ustart = Math.Atan2(e.StartPoint.y, e.StartPoint.x);
                    //if (ustart < 0.0) ustart += 2.0 * Math.PI;
                    double uend = Math.Atan2(e.EndPoint.y, e.EndPoint.x);
                    if (e.Center.z < 0)
                    {
                        ustart += Math.PI;
                        uend += Math.PI;
                    }
                    // if (uend < 0.0) uend += 2.0 * Math.PI;
                    GeoPoint mp = e.PointAt(0.5);
                    // da der Bogen nicht über den Saum gehen darf müsste hier ustart und uend richtig sein
                    // man könnte mit forward checken, was jedoch, wenn nicht richtig?
                    if (!e.IsArc)
                    {
                        // a full circle
                        if (forward) uend = ustart + Math.PI * 2.0;
                        else uend = ustart - Math.PI * 2.0;
                    }
                    else
                    {   // the following is more exact for slightly inclined arcs
                        GeoPoint2D sp = PositionOf(curve.StartPoint);
                        GeoPoint2D ep = PositionOf(curve.EndPoint);
                        if (!forward && (sp.x < ep.x))
                        {
                            // entweder ustart + 2*pi oder uend - 2*pi
                            sp.x += 2 * Math.PI;
                        }
                        if (forward && (sp.x > ep.x))
                        {
                            ep.x += 2 * Math.PI; // noch nicht getestet
                        }
                        res = new Line2D(sp, ep);
                        return res;

                        //Unreachable code
                        /*
                        if (!forward && (ustart < uend))
                        {
                            // entweder ustart + 2*pi oder uend - 2*pi
                            ustart += 2 * Math.PI;
                        }
                        if (forward && (ustart > uend))
                        {
                            uend += 2 * Math.PI; // noch nicht getestet
                        }
                        */
                    }
                    // Grenzfälle: ustart oder uend liegen auf 0.0 oder 2*pi
                    // dann weiß man nicht ob der Punkt zyklisch richtig ist
                    res = new Line2D(new GeoPoint2D(ustart, e.Center.z), new GeoPoint2D(uend, e.Center.z));
                    return res;
                }
                else
                {
                    // copied from CylindricalSurface (see there for comments):
                    GeoPoint2D pse = PositionOf(curve.StartPoint);
                    GeoPoint2D pme = PositionOf(curve.PointAt(0.5));
                    GeoPoint2D pee = PositionOf(curve.EndPoint);
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
                    if (s > 0)
                    {
                        double ac = -d * cosa + c * cosb - c + d;
                        double u1 = -Math.Acos(ac / Math.Sqrt(s));
                        double u3 = -Math.Acos(-ac / Math.Sqrt(s));
                        double minDist = double.MaxValue;
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
                        if (minDist < Precision.eps && res != null)
                        {
                            return res;
                        }
                    }

                }
            }
            return base.GetProjectedCurve(curve, precision);
        }
        public override int GetExtremePositions(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, out List<Tuple<double, double, double, double>> extremePositions)
        {
            switch (other)
            {
                case PlaneSurface _:
                case CylindricalSurface _:
                    {
                        int res = other.GetExtremePositions(otherBounds, this, thisBounds, out extremePositions);
                        if (res > 0)
                        {
                            for (int i = 0; i < extremePositions.Count; i++)
                            {
                                extremePositions[i] = new Tuple<double, double, double, double>(extremePositions[i].Item3, extremePositions[i].Item4, extremePositions[i].Item1, extremePositions[i].Item2);
                            }
                        }
                        return res;
                    }
                case ConicalSurface cs:
                    {
                        // we are looking for a connection line of the two axis where the angle of the connection line to the axis is perpendicular to the semi angle of the cone
                        // i.e. this line is perpendicular to both cone surfaces
                        // maybe this is too time-consuming and we should use a new GaussNewtonMinimizer for finding a perpendicular connection of two surfaces
                        GeoVector nzaxis1 = ZAxis.Normalized;
                        GeoVector nzaxis2 = cs.ZAxis.Normalized;
                        Geometry.DistLL(Location, nzaxis1, cs.Location, nzaxis2, out double s1, out double s2);
                        GeoPoint p1 = Location + s1 * nzaxis1; // good starting positions for "LineConnection", which starts with s1 and s2 == 0
                        GeoPoint p2 = cs.Location + s2 * nzaxis2;
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        for (int i = 0; i < 4; i++)
                        {
                            double a1, a2;
                            switch (i)
                            {
                                case 0:
                                    a1 = Math.Cos(Math.PI / 2.0 + OpeningAngle / 2.0);
                                    a2 = Math.Cos(Math.PI / 2.0 + cs.OpeningAngle / 2.0);
                                    break;
                                case 1:
                                    a1 = Math.Cos(-Math.PI / 2.0 + OpeningAngle / 2.0);
                                    a2 = Math.Cos(Math.PI / 2.0 + cs.OpeningAngle / 2.0);
                                    break;
                                case 2:
                                    a1 = Math.Cos(Math.PI / 2.0 + OpeningAngle / 2.0);
                                    a2 = Math.Cos(-Math.PI / 2.0 + cs.OpeningAngle / 2.0);
                                    break;
                                default:
                                case 3:
                                    a1 = Math.Cos(-Math.PI / 2.0 + OpeningAngle / 2.0);
                                    a2 = Math.Cos(-Math.PI / 2.0 + cs.OpeningAngle / 2.0);
                                    break;
                            }
                            GaussNewtonMinimizer.LineConnection(p1, nzaxis1, p2, nzaxis2, a1, a2, out s1, out s2);
                            Line intsLine = Line.TwoPoints(p1 + s1 * nzaxis1, p2 + s2 * nzaxis2);
                            GeoPoint2D[] ips = this.GetLineIntersection(intsLine.StartPoint, intsLine.StartDirection); // two points, one is perpendicular
                            if (ips.Length == 2)
                            {
                                GeoPoint2D found;
                                if (Math.Abs(VDirection(ips[0]) * intsLine.StartDirection) < Math.Abs(VDirection(ips[1]) * intsLine.StartDirection)) found = ips[0];
                                else found = ips[1];
                                SurfaceHelper.AdjustPeriodic(this, thisBounds, ref found);
                                if (thisBounds.Contains(found)) extremePositions.Add(new Tuple<double, double, double, double>(found.x, found.y, double.NaN, double.NaN));
                            }
                            ips = cs.GetLineIntersection(intsLine.StartPoint, intsLine.StartDirection);
                            if (ips.Length == 2)
                            {
                                GeoPoint2D found;
                                if (Math.Abs(cs.VDirection(ips[0]) * intsLine.StartDirection) < Math.Abs(cs.VDirection(ips[1]) * intsLine.StartDirection)) found = ips[0];
                                else found = ips[1];
                                SurfaceHelper.AdjustPeriodic(other, otherBounds, ref found);
                                if (otherBounds.Contains(found)) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, found.x, found.y));
                            }
                        }
                        return extremePositions.Count;
                    }
                case SphericalSurface ss:
                    {
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        double lp = Geometry.LinePar(this.Location, this.Axis, ss.Location);
                        GeoPoint onAxis = Location + lp * Axis;
                        GeoVector dir = ss.Location - onAxis;
                        GeoPoint2D[] ips = this.GetLineIntersection(onAxis, dir); // two points, one is perpendicular
                        if (ips.Length == 2)
                        {
                            SurfaceHelper.AdjustPeriodic(this, thisBounds, ref ips[0]);
                            if (thisBounds.Contains(ips[0])) extremePositions.Add(new Tuple<double, double, double, double>(ips[0].x, ips[0].y, double.NaN, double.NaN));
                            SurfaceHelper.AdjustPeriodic(this, thisBounds, ref ips[1]);
                            if (thisBounds.Contains(ips[1])) extremePositions.Add(new Tuple<double, double, double, double>(ips[1].x, ips[1].y, double.NaN, double.NaN));
                        }
                        ips = ss.GetLineIntersection(onAxis, dir);
                        if (ips.Length == 2)
                        {
                            SurfaceHelper.AdjustPeriodic(other, otherBounds, ref ips[0]);
                            if (otherBounds.Contains(ips[0])) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, ips[0].x, ips[0].y));
                            SurfaceHelper.AdjustPeriodic(other, otherBounds, ref ips[1]);
                            if (otherBounds.Contains(ips[1])) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, ips[1].x, ips[1].y));
                        }
                        return extremePositions.Count;
                    }
            }
            return base.GetExtremePositions(thisBounds, other, otherBounds, out extremePositions);
        }
        #endregion
        #region ISerializable Members
        protected ConicalSurface(SerializationInfo info, StreamingContext context)
        {
            toCone = (ModOp)info.GetValue("ToCone", typeof(ModOp));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ToCone", toCone, typeof(ModOp));
            if (voffset != 0.0) throw new ApplicationException("erst noch voffset entfernen");
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            toUnit = toCone.GetInverse();
            voffset = 0.0;
        }
        #endregion
        #region IJsonSerialize
        protected ConicalSurface() // for IJsonSerialize
        {
            voffset = 0.0;
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("ToUnit", toUnit);
        }

        public void SetObjectData(IJsonReadData data)
        {
            toUnit = data.GetProperty<ModOp>("ToUnit");
            toCone = toUnit.GetInverse();
        }
        #endregion
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            List<IPropertyEntry> se = new List<IPropertyEntry>();
            GeoPointProperty location = new GeoPointProperty(frame, "ConicalSurface.Location");
            location.ReadOnly = true;
            location.OnGetValue = new EditableProperty<GeoPoint>.GetValueDelegate(delegate () { return toCone * GeoPoint.Origin; });
            se.Add(location);
            GeoVectorProperty dirx = new GeoVectorProperty(frame, "ConicalSurface.DirectionX");
            dirx.ReadOnly = true;
            dirx.IsAngle = false;
            dirx.OnGetValue = new EditableProperty<GeoVector>.GetValueDelegate(delegate () { return toCone * GeoVector.XAxis; });
            se.Add(dirx);
            GeoVectorProperty diry = new GeoVectorProperty(frame, "ConicalSurface.DirectionY");
            diry.ReadOnly = true;
            diry.IsAngle = false;
            diry.OnGetValue = new EditableProperty<GeoVector>.GetValueDelegate(delegate () { return toCone * GeoVector.XAxis; });
            se.Add(diry);
            AngleProperty openingAngle = new AngleProperty(frame, "ConicalSurface.OpeningAngle");
            openingAngle.ReadOnly = true;
            openingAngle.OnGetValue = new EditableProperty<Angle>.GetValueDelegate(delegate () { return OpeningAngle; });
            se.Add(openingAngle);
            return new GroupProperty("ConicalSurface", se.ToArray());
        }
#if DEBUG
        new Face DebugAsFace
        {
            get
            {
                Face res = Face.MakeFace(this, new SimpleShape(Border.MakeRectangle(0.0, 2 * Math.PI, 0.0, 10.0)));
                return res;
            }
        }
#endif
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

        GeoPoint ICone.Apex { get => Location; set => throw new NotImplementedException(); }
        GeoVector ICone.Axis { get => ZAxis; set => throw new NotImplementedException(); }
        Angle ICone.OpeningAngle { get => OpeningAngle; set => throw new NotImplementedException(); }
        #endregion
        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            GeoPoint cnt = Location + 500 * ZAxis.Normalized;
            GeoVector dir = ZAxis;
            double sa = OpeningAngle / 2.0;
            double radius = 500 * Math.Tan(sa);
            int ax = export.WriteAxis2Placement3d(cnt, dir, XAxis);
            return export.WriteDefinition("CONICAL_SURFACE('', #" + ax.ToString() + "," + export.ToString(radius) + "," + export.ToString(sa) + ")");
        }

    }
}
