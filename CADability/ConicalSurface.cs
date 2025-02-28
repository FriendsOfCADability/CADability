using CADability.Curve2D;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
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
    public class ConicalSurface : ISurfaceImpl, ISerializable, IDeserializationCallback, ISurfaceOfRevolution, IExportStep, ICone
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
            // if (voffset != 0.0) throw new ApplicationException("internal error: voffset must be 0.0");
            GeoPoint pu = toUnit * p;
            if (pu.z < 0.0)
            {
                double u = Math.Atan2(-pu.y, -pu.x);
                if (u < 0) u += Math.PI * 2;
                return new GeoPoint2D(u, pu.z - voffset);
            }
            else
            {
                double u = Math.Atan2(pu.y, pu.x);
                if (u < 0) u += Math.PI * 2;
                return new GeoPoint2D(u, pu.z - voffset);
            }
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
                Plane pln = new Plane(Location, Axis, fromHere - Location);
                // in this plane the x-axis is the conical axis, the origin is the apex of the cone
                Angle dira = OpeningAngle / 2.0;
                // this line through origin with angle dira and -dira are the envelope lines of the cone
                GeoPoint2D fromHere2d = pln.Project(fromHere);
                GeoPoint2D fp1 = Geometry.DropPL(fromHere2d, GeoPoint2D.Origin, new GeoVector2D(dira));
                GeoPoint2D fp2 = Geometry.DropPL(fromHere2d, GeoPoint2D.Origin, new GeoVector2D(-dira));
                return new GeoPoint2D[] { PositionOf(pln.ToGlobal(fp1)), PositionOf(pln.ToGlobal(fp2)) };
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
            if (curve2d is ProjectedCurve pc)
            {
                if (pc.Surface is ConicalSurface)
                {
                    BoundingRect otherBounds = new BoundingRect(PositionOf(pc.Surface.PointAt(pc.StartPoint)), PositionOf(pc.Surface.PointAt(pc.EndPoint)));
                    if (pc.Surface.SameGeometry(pc.GetExtent(), this, otherBounds, Precision.eps, out ModOp2D notneeded))
                    {
                        return pc.Curve3DFromParams; // if trimmed or reversed still returns the correct 3d curve (but trimmed and/or reversed)
                    }
                }
            }
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
            // Mit Maxima: 
            // solve((spz+l*dirz)^2=(spx+l*dirx)^2+(spy+l*diry)^2,l);  string(%);
            // ergibt sich:
            // [l = (sqrt((diry^2+dirx^2)*spz^2+(-2*diry*dirz*spy-2*dirx*dirz*spx)*spz+\
            // (dirz^2-dirx^2)*spy^2+2*dirx*diry*spx*spy+(dirz^2-diry^2)*spx^2)-dirz*spz+diry\
            // *spy+dirx*spx)/(dirz^2-diry^2-dirx^2),l = -(sqrt((diry^2+dirx^2)*spz^2+(-2*dir\
            // y*dirz*spy-2*dirx*dirz*spx)*spz+(dirz^2-dirx^2)*spy^2+2*dirx*diry*spx*spy+(dir\
            // z^2-diry^2)*spx^2)+dirz*spz-diry*spy-dirx*spx)/(dirz^2-diry^2-dirx^2)]
            double root = (dir.y * dir.y + dir.x * dir.x) * sp.z * sp.z + (-2 * dir.y * dir.z * sp.y - 2 * dir.x * dir.z * sp.x) * sp.z + (dir.z * dir.z - dir.x * dir.x) * sp.y * sp.y + 2 * dir.x * dir.y * sp.x * sp.y + (dir.z * dir.z - dir.y * dir.y) * sp.x * sp.x;
            double denominator = (dir.z * dir.z - dir.y * dir.y - dir.x * dir.x);
            if (root >= 0.0 && denominator != 0.0)
            {
                double l1 = (Math.Sqrt(root) - dir.z * sp.z + dir.y * sp.y + dir.x * sp.x) / denominator;
                double l2 = -(Math.Sqrt(root) + dir.z * sp.z - dir.y * sp.y - dir.x * sp.x) / denominator;
                GeoPoint pl1 = sp + l1 * dir;
                GeoPoint pl2 = sp + l2 * dir;
                // Mit dem Winkel (u-Parameter) verhält es sich so: im "oberen" Kegel ist es der Winkel
                // der x,y Komponente, im "unteren" ist es um PI versetzt. Das Ergebnis soll immer
                // im Bereich 0..2*PI sein.
                double u1 = Math.Atan2(pl1.y, pl1.x);
                if (pl1.z < 0.0) u1 += Math.PI;
                if (u1 < 0.0) u1 += Math.PI * 2.0;
                double u2 = Math.Atan2(pl2.y, pl2.x);
                if (pl2.z < 0.0) u2 += Math.PI;
                if (u2 < 0.0) u2 += Math.PI * 2.0;
                return new GeoPoint2D[] { new GeoPoint2D(u1, pl1.z), new GeoPoint2D(u2, pl2.z) };
            }
            return new GeoPoint2D[0];

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
            /*
             * http://mathworld.wolfram.com/ConicSection.html
             * Die möglisch Lösungen sind Kreis oder Ellipse, Parabel oder Hyperbels 
             */
            Plane pln = new Plane(toUnit * pl.Location, toUnit * pl.DirectionX, toUnit * pl.DirectionY);
            Angle a = new Angle(pln.Normal, GeoVector.ZAxis);
            Angle teta = new Angle(-pln.Normal, GeoVector.ZAxis);
            double angToz = Math.Abs(a) > Math.Abs(teta) ? Math.Abs(teta) : Math.Abs(a);
            if (Precision.IsPointOnPlane(GeoPoint.Origin, pln))
            {   // Ebene geht durch den Scheitel, d.h. zwei (Mantel-)Linien (oder nichts) als Ergebnis
                // gesucht: die Schnittlinie mit der X/Y-Ebene durch (0,0,1)
                List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                GeoVector dir = pln.Normal ^ GeoVector.ZAxis;
                if (!Precision.IsNullVector(dir))
                {
                    GeoVector vz = pln.ToGlobal(pln.Project(GeoVector.ZAxis));
                    Plane plz = new Plane(Plane.StandardPlane.XYPlane, 1.0);
                    GeoPoint pint = plz.Intersect(GeoPoint.Origin, vz);
                    GeoPoint2D lorg = plz.Project(pint);
                    GeoVector2D ldir = plz.Project(dir);
                    // Schnitt der Linie mit dem Kreis (0,0), r=1 auf der X/Y-Ebene durch (0,0,1)
                    GeoPoint2D[] ips = Geometry.IntersectLC(lorg, ldir, GeoPoint2D.Origin, 1.0);
                    for (int i = 0; i < ips.Length; ++i)
                    {   // der Schnittpunkt mit dem Kreis bestimmt den u Parameter, v ist durch vmin, vmax gegeben
                        double u = Math.Atan2(ips[i].y, ips[i].x);
                        if (u < 0.0) u += 2.0 * Math.PI;
                        GeoPoint p1 = this.PointAt(new GeoPoint2D(u, vmin));
                        GeoPoint p2 = this.PointAt(new GeoPoint2D(u, vmax));
                        Line l3d = Line.Construct();
                        l3d.SetTwoPoints(p1, p2);
                        Line2D lc = new Line2D(new GeoPoint2D(u, vmin), new GeoPoint2D(u, vmax));
                        Line2D lp = new Line2D(pl.PositionOf(p1), pl.PositionOf(p2));
                        DualSurfaceCurve dsc = new DualSurfaceCurve(l3d, this, lc, pl, lp);
                        res.Add(dsc);
                    }
                    return res.ToArray();
                }
            }

            if (angToz > Math.PI / 4 && angToz <= Math.PI / 2)
            #region Hyperbola
            {   //Hier kommt ein Hyperbel, ein Punkt oder nichts raus
                // System.Diagnostics.Trace.WriteLine("Hyperbel");

                ICurve topCircle;
                if (vmax < 0) topCircle = FixedV(vmin, 0, Math.PI * 2); // vmin and vmax must have the same sign!
                else topCircle = FixedV(vmax, 0, Math.PI * 2);
                // Find the two intersectionspoints of the top circle of the cone (at vmax) with the plane (called endpoints).
                // The intersection curve will be a hyperbola. 
                // In the uv system of the plane find the intersection point of the two tangents at the endpoints.
                // find the point on the hyperbola where it is intersected by the line from the midpoint between the endpoints and the tangent intersection points
                // with these four points we can make a bspline, which is exactely a hyperbola
                double[] pi = topCircle.GetPlaneIntersection(pl.Plane);
                if (pi.Length == 2)
                {
                    GeoPoint p3d1 = topCircle.PointAt(pi[0]);
                    GeoPoint p3d2 = topCircle.PointAt(pi[1]);
                    GeoPoint2D cuv1 = PositionOf(p3d1);
                    GeoPoint2D cuv2 = PositionOf(p3d2);
                    BoundingRect domain = new BoundingRect(umin, vmin, umax, vmax);
                    SurfaceHelper.AdjustPeriodic(this, domain, ref cuv1);
                    SurfaceHelper.AdjustPeriodic(this, domain, ref cuv2);
                    GeoPoint2D puv1 = pl.PositionOf(p3d1);
                    GeoPoint2D puv2 = pl.PositionOf(p3d2);
                    GeoVector dir1 = GetNormal(cuv1) ^ pl.GetNormal(puv1);
                    GeoVector dir2 = GetNormal(cuv2) ^ pl.GetNormal(puv2);
                    GeoVector2D pdir1 = (pl.ToXYPlane * dir1).To2D();
                    GeoVector2D pdir2 = (pl.ToXYPlane * dir2).To2D();
                    GeoPoint2D midPoint = new GeoPoint2D(puv1, puv2);
                    Geometry.IntersectLL(puv1, pdir1, puv2, pdir2, out GeoPoint2D tangentIntersectionPoint);
                    GeoPoint tip3d = pl.FromXYPlane * tangentIntersectionPoint;
                    GeoPoint mp3d = pl.FromXYPlane * midPoint;
                    GeoPoint2D[] hypMidPoint = GetLineIntersection(mp3d, tip3d - mp3d);
                    // there should be one hypMidPoint with v<vmax
                    for (int i = 0; i < hypMidPoint.Length; i++)
                    {
                        bool ok;
                        if (vmax < 0) ok = hypMidPoint[i].y > vmin && hypMidPoint[i].y < 0;
                        else ok = hypMidPoint[i].y < vmax && hypMidPoint[i].y > 0;
                        if (ok)
                        {
                            GeoPoint2D pHypMidPoint = pl.PositionOf(PointAt(hypMidPoint[i]));
                            BSpline2D crvOnPlane = BSpline2D.MakeHyperbola(puv1, puv2, pHypMidPoint, tangentIntersectionPoint);
                            BSpline crv3d = pl.Make3dCurve(crvOnPlane) as BSpline;
                            ICurve2D onCone = new ProjectedCurve(crv3d, this, true, domain);
#if DEBUG
                            // The following yields exactely the same curve as "onCone"
                            // We would need a 2d curve "SecantCurve2D" analoguous to SineCurve2D, which has the "f" as property (besides a ModOp2D to enable modifications)
                            // This would probably be faster than the ProjectedCurve
                            // Maybe we could make some general curve, which takes a formula (u,v)->(x,y) and the derivations, but how could you serialize that?
                            // some direction and periodic adjustement cases would have to be discriminated
                            double u1 = cuv1.x;
                            double u2 = cuv2.x;
                            SurfaceHelper.AdjustPeriodic(this, domain, ref hypMidPoint[i]);
                            double um = hypMidPoint[i].x;
                            double du = Math.PI / 2 - um;
                            double y1 = 1.0 / Math.Sin(u1 + du);
                            double y2 = 1.0 / Math.Sin(u2 + du);
                            double ym = 1.0 / Math.Sin(um + du);
                            double f = (hypMidPoint[i].y - cuv1.y) / (ym - y1);
                            GeoPoint2D[] pnts = new GeoPoint2D[100];
                            for (int j = 0; j < pnts.Length; j++)
                            {
                                double u = u2 + du + j / 99.0 * (u1 - u2);
                                pnts[j] = new GeoPoint2D(u - du, f / Math.Sin(u));
                            }
                            Polyline2D pl2d = new Polyline2D(pnts);
#endif
                            DualSurfaceCurve dsc = new DualSurfaceCurve(crv3d, this, onCone, pl, crvOnPlane);
                            return new IDualSurfaceCurve[] { dsc };
                        }
                    }
                }
                GeoPoint loc;
                GeoVector dir;
                // Eduards Code für Hyperbeln eingeklammert
                // Der Kegel soll kein Doppelkegel sein, also gibt es nur eine Hyperbel.
                // vmin und vmax geben den Ausschlag
                // pln ist die Ebene im Unitsystem
                // finde die beiden Schnittpunkte der Ebene mit dem Krei bei vmin bzw vmax
                Plane vminPln = new Plane(new GeoPoint(0, 0, vmin), GeoVector.XAxis, GeoVector.YAxis);
                List<InterpolatedDualSurfaceCurve.SurfacePoint> sp = new List<InterpolatedDualSurfaceCurve.SurfacePoint>();
                if (vminPln.Intersect(pln, out loc, out dir))
                {
                    GeoPoint2D[] ips = Geometry.IntersectLC(vminPln.Project(loc), vminPln.Project(dir), GeoPoint2D.Origin, Math.Abs(vmin));
                    if (ips.Length == 2)
                    {
                        GeoPoint p3d = toCone * vminPln.ToGlobal(ips[0]);
                        GeoPoint2D uv = PositionOf(p3d);
                        SurfaceHelper.AdjustPeriodic(this, new BoundingRect(umin, vmin, umax, vmax), ref uv);
                        sp.Add(new InterpolatedDualSurfaceCurve.SurfacePoint(p3d, uv, pl.PositionOf(p3d)));
                        p3d = toCone * vminPln.ToGlobal(ips[1]);
                        uv = PositionOf(p3d);
                        SurfaceHelper.AdjustPeriodic(this, new BoundingRect(umin, vmin, umax, vmax), ref uv);
                        sp.Add(new InterpolatedDualSurfaceCurve.SurfacePoint(p3d, uv, pl.PositionOf(p3d)));
                    }
                }
                Plane vmaxPln = new Plane(new GeoPoint(0, 0, vmax), GeoVector.XAxis, GeoVector.YAxis);
                if (vmaxPln.Intersect(pln, out loc, out dir))
                {
                    GeoPoint2D[] ips = Geometry.IntersectLC(vmaxPln.Project(loc), vmaxPln.Project(dir), GeoPoint2D.Origin, Math.Abs(vmax));
                    if (ips.Length == 2)
                    {
                        GeoPoint p3d = toCone * vmaxPln.ToGlobal(ips[0]);
                        GeoPoint2D uv = PositionOf(p3d);
                        SurfaceHelper.AdjustPeriodic(this, new BoundingRect(umin, vmin, umax, vmax), ref uv);
                        sp.Add(new InterpolatedDualSurfaceCurve.SurfacePoint(p3d, uv, pl.PositionOf(p3d)));
                        p3d = toCone * vmaxPln.ToGlobal(ips[1]);
                        uv = PositionOf(p3d);
                        SurfaceHelper.AdjustPeriodic(this, new BoundingRect(umin, vmin, umax, vmax), ref uv);
                        sp.Add(new InterpolatedDualSurfaceCurve.SurfacePoint(p3d, uv, pl.PositionOf(p3d)));
                    }
                }
                if (sp.Count == 2)
                {
                    InterpolatedDualSurfaceCurve dsc = new InterpolatedDualSurfaceCurve(this, pl, sp.ToArray());
                    return new IDualSurfaceCurve[] { dsc.ToDualSurfaceCurve() };
                }
                else if (sp.Count == 4)
                {
                    if ((sp[0].p3d | sp[2].p3d) < (sp[0].p3d | sp[3].p3d))
                    {
                        InterpolatedDualSurfaceCurve dsc1 = new InterpolatedDualSurfaceCurve(this, pl, new InterpolatedDualSurfaceCurve.SurfacePoint[] { sp[0], sp[2] });
                        InterpolatedDualSurfaceCurve dsc2 = new InterpolatedDualSurfaceCurve(this, pl, new InterpolatedDualSurfaceCurve.SurfacePoint[] { sp[1], sp[3] });
                        return new IDualSurfaceCurve[] { dsc1.ToDualSurfaceCurve(), dsc2.ToDualSurfaceCurve() };
                    }
                    else
                    {
                        InterpolatedDualSurfaceCurve dsc1 = new InterpolatedDualSurfaceCurve(this, pl, new InterpolatedDualSurfaceCurve.SurfacePoint[] { sp[0], sp[3] });
                        InterpolatedDualSurfaceCurve dsc2 = new InterpolatedDualSurfaceCurve(this, pl, new InterpolatedDualSurfaceCurve.SurfacePoint[] { sp[1], sp[2] });
                        return new IDualSurfaceCurve[] { dsc1.ToDualSurfaceCurve(), dsc2.ToDualSurfaceCurve() };
                    }
                }
                else
                {
                    return new IDualSurfaceCurve[] { };
                }

                //Angle ha = new Angle(pln.Normal.x, pln.Normal.y);
                //ModOp hm = ModOp.Rotate(GeoVector.ZAxis, -ha);
                //ModOp hm1 = hm.GetInverse();
                //Plane hp = pln;
                //hp.Modify(hm);
                //GeoVector hnormal = hm * pln.Normal;
                //GeoVector2D dirline = new GeoVector2D(hnormal.z, -hnormal.x);
                //GeoPoint onX;
                //onX = hp.Intersect(GeoPoint.Origin, GeoVector.XAxis);
                //GeoPoint2D hcnt1, hcm1;
                //GeoPoint2D hcnt2 = new GeoPoint2D();
                //GeoPoint2D hcm2 = new GeoPoint2D();
                //bool zweiteilig = false;

                //if (vmin * vmax >= 0)
                //{ //Es giebt nur ein Seitetige Hyperbel
                //    if (Math.Abs(vmax) > Math.Abs(vmin))
                //    { //Die Positive Teil
                //        Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, new GeoPoint2D(0,vmax), GeoVector2D.XAxis, out hcm1);
                //        if (onX.x >0)
                //            Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, GeoPoint2D.Origin, new GeoVector2D(1, 1), out hcnt1);
                //        else
                //            Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, GeoPoint2D.Origin, new GeoVector2D(-1, 1), out hcnt1);

                //        if (Math.Abs(hcnt1.x) >= Math.Abs(vmax)) //Kein Hyperbel
                //            return new IDualSurfaceCurve[0];
                //    }
                //    else
                //    {//Die Negative Teil
                //        Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, new GeoPoint2D(0, vmin), GeoVector2D.XAxis, out hcm1);
                //        if (onX.x > 0)
                //            Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, GeoPoint2D.Origin, new GeoVector2D(-1, 1), out hcnt1);
                //        else
                //            Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, GeoPoint2D.Origin, new GeoVector2D(1, 1), out hcnt1);

                //        if (Math.Abs(hcnt1.x) >= Math.Abs(vmin)) //Kein Hyperbel
                //            return new IDualSurfaceCurve[0];
                //    }
                //}
                //else // sollte nicht vorkommen
                //{ // Möglicherweise zweiseitige Heperbel
                //    Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, new GeoPoint2D(0, vmax), GeoVector2D.XAxis, out hcm1);
                //    Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, new GeoPoint2D(0, vmin), GeoVector2D.XAxis, out hcm2);
                //    if (onX.x > 0)
                //    {
                //        Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, GeoPoint2D.Origin, new GeoVector2D(1, 1), out hcnt1);
                //        Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, GeoPoint2D.Origin, new GeoVector2D(-1, 1), out hcnt2);
                //    }
                //    else
                //    {
                //        Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, GeoPoint2D.Origin, new GeoVector2D(-1, 1), out hcnt1);
                //        Geometry.IntersectLL(new GeoPoint2D(onX.x, 0), dirline, GeoPoint2D.Origin, new GeoVector2D(1, 1), out hcnt2);
                //    }
                //    zweiteilig = true;
                //    if (Math.Abs(hcm1.x) >= Math.Abs(vmax))
                //    {//Positive Teil der Hyperbel giebt es nicht
                //        zweiteilig = false;
                //        if (Math.Abs(hcm2.x) >= Math.Abs(vmin))
                //        {//Negative Teil der Hyperbel giebt es nicht
                //            return new IDualSurfaceCurve[0];
                //        }
                //        else
                //        {
                //            hcnt1 = hcnt2;
                //            hcm1 = hcm2;
                //        }
                //    }
                //    else
                //    {
                //        if (Math.Abs(hcm2.x) >= Math.Abs(vmin))
                //        {//Negative Teil der Hyperbel giebt es nicht
                //            zweiteilig = false;
                //        }
                //    }
                //}

                ////Prüfen ob es zwei Geraden sind
                //if (Math.Abs(hcm1.x) <= Precision.eps)
                //{// Zwei Geraden
                //    GeoPoint hgp1 = new GeoPoint(0, vmin, vmin);
                //    GeoPoint hgp2 = new GeoPoint(0, vmax, vmax);
                //    //Gerade im Weltsystem
                //    Line hglw = Line.Construct();
                //    hglw.StartPoint = toCone * (hm1 * hgp1);
                //    hglw.EndPoint = toCone * (hm1 * hgp2);
                //    //Gerade in der pl Ebene
                //    Line2D hglpl = new Line2D(pl.PositionOf(hm1 * hgp1), pl.PositionOf(hm1 * hgp2));
                //    //Gerade im (u,v) System
                //    Angle hbeta = new Angle(pln.Normal.x, pln.Normal.y);
                //    Line2D hgluv = new Line2D(new GeoPoint2D(hbeta.Radian + Math.PI / 2, vmin),
                //        new GeoPoint2D(hbeta.Radian + Math.PI / 2, vmax));
                //    DualSurfaceCurve hgdsc1 = new DualSurfaceCurve(hglw, this, hgluv, pl, hglpl);
                //    //Gerade im Weltsystem
                //    Line hglw2 = Line.Construct();
                //    hgp1 = new GeoPoint(0, -vmin, vmin);
                //    hgp2 = new GeoPoint(0, -vmax, vmax);
                //    hglw2.StartPoint = toCone * (hm1 * hgp1);
                //    hglw2.EndPoint = toCone * (hm1 * hgp2);
                //    //Gerade in der pl Ebene
                //    hglpl = new Line2D(pl.PositionOf(hm1 * hgp1), pl.PositionOf(hm1 * hgp2));
                //    //Gerade im (u,v) System
                //    hgluv = new Line2D(new GeoPoint2D(hbeta.Radian + 3 * Math.PI / 2, vmin),
                //        new GeoPoint2D(hbeta.Radian + 3 * Math.PI / 2, vmax));

                //    DualSurfaceCurve hgdsc2 = new DualSurfaceCurve(hglw2, this, hgluv, pl, hglpl);
                //    return new IDualSurfaceCurve[] { hgdsc1, hgdsc2 };
                //}

                //int npnts = 5; //(2 * npnts +1) Punkten werden benutzt um die Hyperbel
                ////als Bspline zu bezeichnet
                //GeoPoint2D[] ht = new GeoPoint2D[npnts];
                //ht[0] = hcm1;
                //double na, nb, nd;
                //nd = Geometry.Dist(hcm1, hcnt1);
                //na = 0;
                //nb = nd / 2;
                //dirline = new GeoVector2D(hcnt1.x - hcm1.x, hcnt1.y - hcm1.y);
                //dirline.Norm();
                //for (int i = 1; i < ht.Length; i++)
                //{
                //    nb = (nd - na) / 2;
                //    na = na + nb;
                //    ht[i] = hcm1 + na * dirline;
                //    //ht[i] = new GeoPoint2D(ht[i - 1], hcnt); //Die Abstände zwischen den Punkten    
                ////werden immer kleiner
                //}
                //GeoPoint[] hw = new GeoPoint[npnts];
                //for (int i = 0; i < ht.Length; i++)
                //    hw[i] = toCone * (hm1 * new GeoPoint(ht[i].x, 0, ht[i].y));

                //GeoPoint hc = toCone * (hm1 * new GeoPoint(hcnt1.x, 0, hcnt1.y));
                //GeoVector hwdir = toCone * ((hm1 * new GeoVector(dirline.x, 0, dirline.y)) ^ pln.Normal);

                //GeoPoint2D[][] hwi = new GeoPoint2D[npnts][];
                //for (int i = 0; i < hw.Length; i++)
                //{
                //    hwi[i] = GetLineIntersection(hw[i], hwdir);
                //}

                //GeoPoint hcu = toUnit * hc;
                //GeoPoint[] harrp = new GeoPoint[2 * npnts + 1];
                //GeoPoint2D[] harrp2D = new GeoPoint2D[2 * npnts + 1];

                //for (int i = 0; i < hwi.Length; i++)
                //{
                //    GeoPoint xx = toUnit * PointAt(hwi[i][0]);
                //    if (xx.y > hcu.y)
                //    {
                //        harrp[i] = PointAt(hwi[i][0]);
                //        harrp2D[i] = pl.PositionOf(harrp[i]);
                //        harrp[2 * npnts - i] = PointAt(hwi[i][1]);
                //        harrp2D[2 * npnts - i] = pl.PositionOf(harrp[2 * npnts - i]);
                //    }
                //    else
                //    {
                //        harrp[i] = PointAt(hwi[i][1]);
                //        harrp2D[i] = pl.PositionOf(harrp[i]);
                //        harrp[2 * npnts - i] = PointAt(hwi[i][0]);
                //        harrp2D[2 * npnts - i] = pl.PositionOf(harrp[2 * npnts - i]);
                //    }
                //}
                //harrp[npnts] = hc;
                //harrp2D[npnts] = pl.PositionOf(hc);

                ////Herstellung des Hyperbel 
                //// im Weltsystem
                //BSpline hbsp = Refine(harrp, 3, false, pl, precision);
                //// im der Ebene
                //BSpline2D hbsp2D = new BSpline2D(harrp2D, 3, false);
                ////in der (u,v) System
                //GeoPoint2D[] hpnts = new GeoPoint2D[hbsp.ThroughPointCount];
                //for (int i = 0; i < hpnts.Length; i++)
                //{
                //    GeoPoint hbp = (hbsp as ICurve).PointAt(i * 1.0 / (hpnts.Length - 1));
                //    GeoPoint2D hzp = this.PositionOf(hbp);
                //    //Der Winkel zurückgeliefert bei PointAt() liegt zwischen -Pi und Pi
                //    //aber im (u,v) System variert u zwischen null und 2Pi
                //    hpnts[i].x = Math.PI + hzp.x;
                //    hpnts[i].y = hzp.y;
                //}
                //BSpline2D.AdjustPeriodic(hpnts, 2 * Math.PI, 0);
                //BSpline2D hc2d = new BSpline2D(hpnts, 3, false);
                //DualSurfaceCurve hdsc = new DualSurfaceCurve(hbsp, this, hc2d, pl, hbsp2D);
                //if (!zweiteilig)
                //{
                //    return new IDualSurfaceCurve[] { hdsc };
                //}

                //ht[0] = hcm2;
                //nd = Geometry.Dist(hcm2, hcnt2);
                //na = 0;
                //nb = nd / 2;
                //dirline = new GeoVector2D(hcnt2.x - hcm2.x, hcnt2.y - hcm2.y);
                //dirline.Norm();
                //for (int i = 1; i < ht.Length; i++)
                //{
                //    nb = (nd - na) / 2;
                //    na = na + nb;
                //    ht[i] = hcm2 + na * dirline;
                //}

                //for (int i = 0; i < ht.Length; i++)
                //    hw[i] = toCone * (hm1 * new GeoPoint(ht[i].x, 0, ht[i].y));

                //hc = toCone * (hm1 * new GeoPoint(hcnt2.x, 0, hcnt2.y));
                //for (int i = 0; i < hw.Length; i++)
                //    hwi[i] = GetLineIntersection(hw[i], hwdir);

                //hcu = toUnit * hc;
                //for (int i = 0; i < hwi.Length; i++)
                //{
                //    GeoPoint xx = toUnit * PointAt(hwi[i][0]);
                //    if (xx.y > hcu.y)
                //    {
                //        harrp[i] = PointAt(hwi[i][0]);
                //        harrp2D[i] = pl.PositionOf(harrp[i]);
                //        harrp[2 * npnts - i] = PointAt(hwi[i][1]);
                //        harrp2D[2 * npnts - i] = pl.PositionOf(harrp[2 * npnts - i]);
                //    }
                //    else
                //    {
                //        harrp[i] = PointAt(hwi[i][1]);
                //        harrp2D[i] = pl.PositionOf(harrp[i]);
                //        harrp[2 * npnts - i] = PointAt(hwi[i][0]);
                //        harrp2D[2 * npnts - i] = pl.PositionOf(harrp[2 * npnts - i]);
                //    }
                //}
                //harrp[npnts] = hc;
                //harrp2D[npnts] = pl.PositionOf(hc);

                ////Herstellung des Hyperbel 
                //// im Weltsystem
                //BSpline hbspo = Refine(harrp, 2, false, pl, precision);
                //// im der Ebene
                //BSpline2D hbsp2Do = new BSpline2D(harrp2D, 3, false);
                ////in der (u,v) System
                //hpnts = new GeoPoint2D[hbspo.ThroughPointCount];
                //for (int i = 0; i < hpnts.Length; i++)
                //{
                //    GeoPoint hbp = (hbspo as ICurve).PointAt(i * 1.0 / (hpnts.Length - 1));
                //    GeoPoint2D hzp = this.PositionOf(hbp);
                //    hpnts[i].x = Math.PI + hzp.x;
                //    hpnts[i].y = hzp.y;
                //}
                //BSpline2D.AdjustPeriodic(hpnts, 2 * Math.PI, 0);
                //BSpline2D hc2do = new BSpline2D(hpnts, 3, false);
                //DualSurfaceCurve hdsco = new DualSurfaceCurve(hbspo, this, hc2do, pl, hbsp2Do);
                //return new IDualSurfaceCurve[] { hdsc, hdsco };
                // return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
            }
            #endregion
            if (Math.Abs(angToz - Math.PI / 4) <= Precision.epsa)
            #region Parabola
            {
                //Hier können eine Gerade, eine Prabola, ein Punkt oder nichts raus kommen
                // System.Diagnostics.Trace.WriteLine("Hier können eine Gerade, eine Prabola, ein Punkt oder nichts raus kommen");
                bool ok1 = false;
                bool ok2 = false;

                Angle b = new Angle(pln.Normal.x, pln.Normal.y);
                ModOp m = ModOp.Rotate(GeoVector.ZAxis, -b);
                ModOp m1 = m.GetInverse();
                GeoVector normal = m * pln.Normal;
                GeoVector2D dirline = new GeoVector2D(normal.z, -normal.x);

                GeoPoint l = pln.Intersect(GeoPoint.Origin, GeoVector.ZAxis);
                if (vmax > Precision.eps && Math.Abs(l.z - 2 * vmax) <= Precision.eps)
                {   //Hier kommt nur ein Punkt raus                    
                    return new IDualSurfaceCurve[0];
                }
                if (vmin < -Precision.eps && Math.Abs(l.z - 2 * vmin) <= Precision.eps)
                {   //Hier kommt nur ein Punkt raus                    
                    return new IDualSurfaceCurve[0];
                }
                if (Precision.IsEqual(l, GeoPoint.Origin))
                {   //Hier kommt nur eine Gerade raus                    
                    GeoPoint gp1 = new GeoPoint();
                    GeoPoint gp2 = new GeoPoint();
                    Angle beta = new Angle(dirline.x, dirline.y);
                    if (Math.Abs(beta.Radian - Math.PI / 4) <= Precision.epsa || Math.Abs(beta.Radian - 5 * Math.PI / 4) <= Precision.epsa)
                    {
                        gp1 = new GeoPoint(vmin, 0, vmin);
                        gp2 = new GeoPoint(vmax, 0, vmax);
                    }
                    if (Math.Abs(beta.Radian - 3 * Math.PI / 4) <= Precision.epsa || Math.Abs(beta.Radian - 7 * Math.PI / 4) <= Precision.epsa)
                    {
                        gp1 = new GeoPoint(-vmin, 0, vmin);
                        gp2 = new GeoPoint(-vmax, 0, vmax);
                    }
                    //Gerade im Weltsystem
                    Line glw = Line.Construct();
                    glw.StartPoint = toCone * (m1 * gp1);
                    glw.EndPoint = toCone * (m1 * gp2);
                    //Gerade in der pl Ebene
                    Line2D glpl = new Line2D(pl.PositionOf(m1 * gp1), pl.PositionOf(m1 * gp2));
                    //Gerade im (u,v) System
                    beta = new Angle(pln.Normal.x, pln.Normal.y);
                    Line2D gluv = new Line2D(new GeoPoint2D(beta.Radian + Math.PI / 2, vmin),
                        new GeoPoint2D(beta.Radian + Math.PI / 2, vmax));
                    DualSurfaceCurve gdsc = new DualSurfaceCurve(glw, this, gluv, pl, glpl);

                    return new IDualSurfaceCurve[] { gdsc };
                }
                GeoPoint2D l2D = new GeoPoint2D(0, l.z);
                GeoPoint2D pm2D;
                if (vmax > Precision.eps && l.z > Precision.eps)
                    pm2D = new GeoPoint2D(0, vmax);
                else if (vmin < -Precision.eps && l.z < -Precision.eps)
                    pm2D = new GeoPoint2D(0, vmin);
                else
                    return new IDualSurfaceCurve[0];

                GeoPoint2D p12D;
                ok1 = Geometry.IntersectLL(l2D, dirline, pm2D, GeoVector2D.XAxis, out p12D);
                GeoPoint2D cn2D;
                ok2 = Geometry.IntersectLL(l2D, dirline, GeoPoint2D.Origin, new GeoVector2D(normal.x, normal.z), out cn2D);
                GeoPoint cn = m1 * new GeoPoint(cn2D.x, 0, cn2D.y);
                GeoPoint ptmp = m1 * new GeoPoint(p12D.x, 0, p12D.y);
                GeoPoint p1 = toCone * cn;
                GeoPoint pptmp = toCone * ptmp;
                GeoVector dir = new GeoVector(dirline.x, 0, dirline.y);
                GeoVector wn = normal ^ dir;
                GeoVector won = toCone * (m1 * wn);
                GeoPoint2D[] pp;
                pp = GetLineIntersection(pptmp, won);
                if (pp.Length < 2)
                {
                    //Die Ebene trifft nicht den Kegel im Weltsystem
                    // oder nur in einen Punkt
                    return new IDualSurfaceCurve[0];
                }

                GeoPoint p0 = PointAt(pp[0]);
                GeoPoint p2 = PointAt(pp[1]);
                //Herstellung des Parable 
                // im Weltsystem
                GeoPoint[] arrp = new GeoPoint[3];
                arrp[0] = p0;
                arrp[1] = p1;
                arrp[2] = p2;
                BSpline bsp = BSpline.Construct();
                bsp.ThroughPoints(arrp, 2, false);
                // im der Ebene
                GeoPoint2D[] arrp2D = new GeoPoint2D[3];
                arrp2D[0] = pl.PositionOf(p0);
                arrp2D[1] = pl.PositionOf(p1);
                arrp2D[2] = pl.PositionOf(p2);
                BSpline2D bsp2D = new BSpline2D(arrp2D, 2, false);
                //in der (u,v) System
                GeoPoint2D[] pnts = new GeoPoint2D[50];
                for (int i = 0; i < pnts.Length; i++)
                {
                    GeoPoint bp = (bsp as ICurve).PointAt(i * 1.0 / (pnts.Length - 1));
                    GeoPoint2D zp = this.PositionOf(bp);
                    //Der Winkel zurückgeliefert bei PointAt() liegt zwischen -Pi und Pi
                    //aber im (u,v) System variert u zwischen null und 2Pi
                    pnts[i].x = Math.PI + zp.x;
                    pnts[i].y = zp.y;
                    //pnts[i] = zp;
                }
                BSpline2D.AdjustPeriodic(pnts, 2 * Math.PI, 0);
                BSpline2D c2d = new BSpline2D(pnts, 2, false);
                DualSurfaceCurve dsc = new DualSurfaceCurve(bsp, this, c2d, pl, bsp2D);
                return new IDualSurfaceCurve[] { dsc };
                // return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
            }
            #endregion
            if (Math.Abs(a.Radian - Math.PI / 4) > Precision.epsa || Math.Abs(a.Radian - 3 * Math.PI / 4) > Precision.epsa)
            #region Kreis, Ellipse oder zwei Linien
            {
                double d = pln.Location.z;
                GeoVector majax, minax;
                GeoPoint cnt = pln.Intersect(GeoPoint.Origin, GeoVector.ZAxis);

                if (Precision.IsEqual(cnt, GeoPoint.Origin))
                #region Zwei Linien
                {
                    Angle ang = new Angle(pln.Normal.x, pln.Normal.y);
                    ModOp mo = ModOp.Rotate(GeoVector.ZAxis, -ang);
                    ModOp mo1 = mo.GetInverse();
                    GeoVector n2g = mo * pln.Normal;
                    GeoVector2D ldir2D = new GeoVector2D(n2g.z, -n2g.x);
                    GeoPoint2D pm1, pm2;
                    Geometry.IntersectLL(new GeoPoint2D(0, vmin), GeoVector2D.XAxis, GeoPoint2D.Origin, ldir2D, out pm1);
                    Geometry.IntersectLL(new GeoPoint2D(0, vmax), GeoVector2D.XAxis, GeoPoint2D.Origin, ldir2D, out pm2);
                    GeoPoint pm13D = toCone * (mo1 * new GeoPoint(pm1.x, 0, pm1.y));
                    GeoPoint pm23D = toCone * (mo1 * new GeoPoint(pm2.x, 0, pm2.y));
                    GeoVector wdir = toCone * (mo1 * (n2g ^ new GeoVector(ldir2D.x, 0, ldir2D.y)));
                    GeoPoint2D[] p2D1 = new GeoPoint2D[2];
                    GeoPoint2D[] p2D2 = new GeoPoint2D[2];
                    p2D1 = GetLineIntersection(pm13D, wdir);
                    p2D2 = GetLineIntersection(pm23D, wdir);
                    GeoPoint start1, start2, ende1, ende2;
                    int i = 0;
                    if (Geometry.Dist(p2D1[0], p2D2[0]) < Geometry.Dist(p2D1[0], p2D2[1]))
                    {
                        i = 1;
                    }
                    start1 = PointAt(p2D1[0]);
                    ende1 = PointAt(p2D2[1 - i]);
                    start2 = PointAt(p2D1[1]);
                    ende2 = PointAt(p2D2[i]);
                    //Linien im Weltsystem
                    Line lw1 = Line.Construct();
                    lw1.StartPoint = start1;
                    lw1.EndPoint = ende1;
                    Line lw2 = Line.Construct();
                    lw2.EndPoint = ende2;
                    lw2.StartPoint = start2;
                    //Linen auf der Ebene
                    Line2D lpl1 = new Line2D(pl.PositionOf(start1), pl.PositionOf(ende1));
                    Line2D lpl2 = new Line2D(pl.PositionOf(start2), pl.PositionOf(ende2));
                    //Linien im (u,v) System
                    Line2D luv1 = new Line2D(p2D1[0], p2D2[1 - i]);
                    Line2D luv2 = new Line2D(p2D1[1], p2D2[i]);
                    DualSurfaceCurve dsc1 = new DualSurfaceCurve(lw1, this, luv1, pl, lpl1);
                    DualSurfaceCurve dsc2 = new DualSurfaceCurve(lw2, this, luv2, pl, lpl2);
                    return new IDualSurfaceCurve[] { dsc1, dsc2 };
                }
                #endregion
                if (Math.Abs(a.Radian) <= Precision.epsa || Math.Abs(a.Radian - 2 * Math.PI) <= Precision.epsa)
                #region Kreis
                {
                    // es kommt ein Kreis oder nichts
                    // System.Diagnostics.Trace.WriteLine("es kommt ein Kreis oder nichts");
                    majax = GeoVector.XAxis;
                    minax = GeoVector.YAxis;
                    majax = d * majax;
                    minax = d * minax;
                }
                #endregion
                else
                #region Eclipse
                {
                    Angle alph = new Angle(pln.Normal.x, pln.Normal.y);
                    ModOp m = ModOp.Rotate(GeoVector.ZAxis, -alph);
                    GeoVector normal = m * pln.Normal;
                    ModOp m1 = m.GetInverse();

                    GeoPoint l = pln.Intersect(GeoPoint.Origin, GeoVector.ZAxis);
                    GeoPoint2D l2d = new GeoPoint2D(l.x, l.z);
                    GeoVector2D dirline = new GeoVector2D(normal.z, -normal.x);
                    GeoPoint2D ip1;
                    Geometry.IntersectLL(l2d, dirline, GeoPoint2D.Origin, new GeoVector2D(1, 1), out ip1);
                    GeoPoint2D ip2;
                    Geometry.IntersectLL(l2d, dirline, GeoPoint2D.Origin, new GeoVector2D(-1, 1), out ip2);
                    // es kommt ein Ellipse oder nichts
                    // System.Diagnostics.Trace.WriteLine("es kommt ein Ellipse oder nichts");

                    GeoPoint p1 = m1 * new GeoPoint(ip1.x, 0, ip1.y);
                    GeoPoint p2 = m1 * new GeoPoint(ip2.x, 0, ip2.y);
                    cnt.x = (p1.x + p2.x) / 2;
                    cnt.y = (p1.y + p2.y) / 2;
                    cnt.z = (p1.z + p2.z) / 2;

                    majax = new GeoVector(cnt, p1);
                    GeoVector dirOrto = toCone * (majax ^ pln.Normal);
                    GeoPoint mp = toCone * cnt;
                    GeoPoint2D[] tp;
                    tp = GetLineIntersection(mp, dirOrto);
                    /////
                    if (tp.Length < 2)
                    {
                        //Die Ebene trifft nicht den Kegel im Weltsystem
                        // oder nur in einen Punkt
                        return new IDualSurfaceCurve[0];
                    }
                    GeoPoint ep1 = PointAt(tp[0]);
                    GeoPoint ep2 = PointAt(tp[1]);
                    minax = new GeoVector(cnt, toUnit * ep1);
                }
                #endregion
                GeoPoint center = toCone * cnt;
                GeoVector majaxis = toCone * majax;
                GeoVector minaxis = toCone * minax;
                Ellipse elli = Ellipse.Construct();
                elli.SweepParameter = Math.PI * 2.0; // das folgende setzt sweepparameter nicht, deshalb hier
                elli.SetEllipseCenterAxis(center, majaxis, minaxis);
                // split the ellipse and use only the part between umin and umax
                GeoPoint pp0 = PointAt(new GeoPoint2D(umin, vmin));
                GeoPoint pp1 = PointAt(new GeoPoint2D(umin, vmax));
                GeoPoint e0 = pl.Plane.Intersect(pp0, pp1 - pp0);
                pp0 = PointAt(new GeoPoint2D(umax, vmin));
                pp1 = PointAt(new GeoPoint2D(umax, vmax));
                GeoPoint e1 = pl.Plane.Intersect(pp0, pp1 - pp0);
                double par0 = elli.PositionOf(e0);
                double par1 = elli.PositionOf(e1);
                // par0 and par1 are the two Parameters where the full Ellipse intersects with the u-bounds of this surface
                Ellipse elli1 = elli.Clone() as Ellipse;
                elli1.Trim(par0, par1);
                GeoPoint2D po = this.PositionOf(elli1.PointAt(0.5));
                BoundingRect ubounds = new BoundingRect(umin, vmin, umax, vmax);
                SurfaceHelper.AdjustPeriodic(this, ubounds, ref po);
                if (po.x < umin || po.x > umax)
                {
                    elli.Trim(par1, par0);
                }
                else
                {
                    elli = elli1;
                }

                //GeoPoint2D centerOnPl = pl.PositionOf(center);
                //GeoPoint2D p1OnPl = pl.PositionOf(center + majaxis);
                //GeoPoint2D p2OnPl = pl.PositionOf(center - minaxis);
                //Ellipse2D elli2d = Geometry.Ellipse2P2T(p1OnPl, p2OnPl, p2OnPl - centerOnPl, p1OnPl - centerOnPl);
                //ICurve2D c2dpl = elli2d.Trim(0.0, 1.0);
                ICurve2D c2dpl = pl.GetProjectedCurve(elli, 0.0);
                GeoPoint2D[] pnts = new GeoPoint2D[50];
                for (int i = 0; i < pnts.Length; i++)
                {
                    GeoPoint b = elli.PointAt(i * 1.0 / (pnts.Length - 1));
                    GeoPoint2D z = this.PositionOf(b);
                    SurfaceHelper.AdjustPeriodic(this, ubounds, ref z);
                    pnts[i] = z;
                }
                BSpline2D c2d = new BSpline2D(pnts, 2, false); // actually this is a Sin-curve, we would only need the amplitude and offset (phase)
                DualSurfaceCurve dsc = new DualSurfaceCurve(elli, this, c2d, pl, c2dpl);
                return new IDualSurfaceCurve[] { dsc };
                //return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
            }
            #endregion
            //Schwerig zu sagen
            // System.Diagnostics.Trace.WriteLine("Schwerig zu sagen");
            return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
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
                {   // es kann sich hier nur um eine Verschiebung von u handeln, in v kann nicht verschoben werden
                    GeoPoint2D pu = cother.PositionOf(PointAt(new GeoPoint2D(0.0, 1.0)));
                    // pu.y muss 1.0 sein, oder?
                    if (Math.Abs(pu.y - 1.0) < 1e-7)
                    {
                        firstToSecond = ModOp2D.Translate(pu.x, 0.0);
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
                    GeoPoint2D pu = cother.PositionOf(PointAt(new GeoPoint2D(0.0, 1.0)));
                    firstToSecond = ModOp2D.Translate(pu.x, 0.0);
                    return true;
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
            // ACHTUNG: zyklisch wird hier nicht berücksichtigt, wird aber vom aufrufenden Kontext (Triangulierung) berücksichtigt
            // ansonsten wäre ja auch nicht klar, welche 2d-Linie gemeint ist
            return Geometry.DistPL(PointAt(mp), PointAt(sp), PointAt(ep));
        }

        public override ISurface GetOffsetSurface(double offset)
        {
            return GetOffsetSurface(offset, out ModOp2D dumy);
        }
        public override ISurface GetOffsetSurface(double offset, out ModOp2D mod)
        {
            // nur zum Überprüfen:
            //GeoPoint2D uv00 = GeoPoint2D.Origin; 
            //GeoPoint2D uv01 = new GeoPoint2D(Math.PI, 0);
            //GeoPoint2D uv10 = new GeoPoint2D(0, 1);
            //GeoPoint2D uv11 = new GeoPoint2D(Math.PI, 1);
            //GeoPoint p00 = PointAt(uv00) + offset * GetNormal(uv00).Normalized;
            //GeoPoint p01 = PointAt(uv01) + offset * GetNormal(uv01).Normalized;
            //GeoPoint p10 = PointAt(uv10) + offset * GetNormal(uv10).Normalized;
            //GeoPoint p11 = PointAt(uv11) + offset * GetNormal(uv11).Normalized;
            //double par1, par2;
            //double dd = Geometry.DistLL(p00, p10 - p00, p01, p11 - p01, out par1, out par2);
            //GeoPoint apex = p00 + par1 * (p10 - p00);
            double oa2 = OpeningAngle / 2.0;
            // sin(oa2) = offset/l
            double l = offset / Math.Sin(oa2);
            GeoPoint apex = Location - l * ZAxis.Normalized;
            ConicalSurface res = new ConicalSurface(apex, XAxis.Normalized, YAxis.Normalized, ZAxis.Normalized, oa2, 0);
            GeoPoint p0 = PointAt(GeoPoint2D.Origin) + offset * GetNormal(GeoPoint2D.Origin).Normalized;
            GeoPoint2D uv0 = res.PositionOf(p0);
            GeoPoint p1 = PointAt(new GeoPoint2D(0, 1)) + offset * GetNormal(new GeoPoint2D(0, 1)).Normalized;
            GeoPoint2D uv1 = res.PositionOf(p1);
            mod = ModOp2D.Translate(0, uv0.y) * ModOp2D.Scale(1.0, (uv1.y - uv0.y));
#if DEBUG
            SimpleShape ss = new SimpleShape(Border.MakeRectangle(0, Math.PI, 0, 100));
            Face dbg1 = Face.MakeFace(this, ss);
            Face dbg2 = Face.MakeFace(res, ss.GetModified(mod));
            GeoObjectList dbgl = new GeoObject.GeoObjectList(dbg1, dbg2);
#endif
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
                        return new Line2D(sp, ep);
                    }
                }
            }
            ICurve crvunit = curve.CloneModified(toUnit);
            if (crvunit is Line)
            {
                Line l = crvunit as Line;
                if (Geometry.DistPL(GeoPoint.Origin, l.StartPoint, l.EndPoint) < Precision.eps)
                {   // not yet tested
                    GeoPoint mp = l.PointAt(0.5);
                    double u = Math.Atan2(mp.y, mp.x);
                    if (l.StartPoint.z + l.EndPoint.z < 0) u += Math.PI; // start- or endpoint could be 0, crossing z=0 is not allowed
                    if (u < 0.0) u += 2.0 * Math.PI;
                    return new Line2D(new GeoPoint2D(u, l.StartPoint.z - voffset), new GeoPoint2D(u, l.EndPoint.z - voffset));
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
                        return new Line2D(sp, ep);
                        
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
                    return new Line2D(new GeoPoint2D(ustart, e.Center.z), new GeoPoint2D(uend, e.Center.z));
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
                        if (minDist < Precision.eps) return res;
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
                        GeoPoint2D[] fp = PerpendicularFoot(ss.Location);
                        extremePositions = new List<Tuple<double, double, double, double>>();
                        for (int i = 0; i < fp.Length; i++)
                        {
                            SurfaceHelper.AdjustPeriodic(this, thisBounds, ref fp[i]);
                            if (otherBounds.Contains(fp[i])) extremePositions.Add(new Tuple<double, double, double, double>(fp[i].x, fp[i].y, double.NaN, double.NaN));
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
