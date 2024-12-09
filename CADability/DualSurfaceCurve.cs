using CADability.Curve2D;
using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability
{

    public interface IDualSurfaceCurve
    {
        ICurve2D GetCurveOnSurface(ISurface onThisSurface);
        ICurve Curve3D { get; }
        ISurface Surface1 { get; }
        ICurve2D Curve2D1 { get; }
        ISurface Surface2 { get; }
        ICurve2D Curve2D2 { get; }
        void SwapSurfaces();
        IDualSurfaceCurve[] Split(double v);
    }


    public class DualSurfaceCurve : IDualSurfaceCurve
    {
        ICurve curve3D;
        ISurface surface1;
        ICurve2D curve2D1;
        ISurface surface2;
        ICurve2D curve2D2;
        public DualSurfaceCurve(ICurve curve3D, ISurface surface1, ICurve2D curve2D1, ISurface surface2, ICurve2D curve2D2)
        {
            this.curve3D = curve3D;
            this.surface1 = surface1;
            this.curve2D1 = curve2D1;
            this.surface2 = surface2;
            this.curve2D2 = curve2D2;
        }
        ICurve2D IDualSurfaceCurve.GetCurveOnSurface(ISurface onThisSurface)
        {
            if (onThisSurface == surface1) return new Curve2DAspect(this, true);
            if (onThisSurface == surface2) return new Curve2DAspect(this, false);
            return null;
        }

        public void SwapSurfaces()
        {
            ISurface tmp = surface1;
            surface1 = surface2;
            surface2 = tmp;
            ICurve2D t1 = curve2D1;
            ICurve2D t2 = curve2D2;
            if (curve3D is InterpolatedDualSurfaceCurve)
            {
                (curve3D as InterpolatedDualSurfaceCurve).SetSurfaces(surface1, surface2, true);
                curve2D1 = (curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                curve2D2 = (curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
            }
            else
            {
                curve2D1 = t2;
                curve2D2 = t1;
            }
        }

        IDualSurfaceCurve[] IDualSurfaceCurve.Split(double v)
        {
            ICurve[] splitted = curve3D.Split(v);
            if (splitted == null || splitted.Length != 2) return null;
            IDualSurfaceCurve dsc1 = null, dsc2 = null;
            if (curve3D is InterpolatedDualSurfaceCurve)
            {
                dsc1 = new DualSurfaceCurve(splitted[0], surface1, (splitted[0] as InterpolatedDualSurfaceCurve).CurveOnSurface1, surface2, (splitted[0] as InterpolatedDualSurfaceCurve).CurveOnSurface2);
                dsc2 = new DualSurfaceCurve(splitted[1], surface1, (splitted[1] as InterpolatedDualSurfaceCurve).CurveOnSurface1, surface2, (splitted[1] as InterpolatedDualSurfaceCurve).CurveOnSurface2);
            }
            else
            {
                dsc1 = new DualSurfaceCurve(splitted[0], surface1, surface1.GetProjectedCurve(splitted[0], 0.0), surface2, surface2.GetProjectedCurve(splitted[0], 0.0));
                dsc2 = new DualSurfaceCurve(splitted[1], surface1, surface1.GetProjectedCurve(splitted[1], 0.0), surface2, surface2.GetProjectedCurve(splitted[1], 0.0));
            }
            return new IDualSurfaceCurve[] { dsc1, dsc2 };

            //GeoPoint2D uv1 = surface1.PositionOf(splitted[0].EndPoint);
            //GeoPoint2D uv2 = surface2.PositionOf(splitted[0].EndPoint);
            //double u1 = curve2D1.PositionOf(uv1);
            //double u2 = curve2D2.PositionOf(uv2);

            //GeoVector2D dir12d = curve2D1.DirectionAt(u1);
            //GeoVector2D dir22d = curve2D2.DirectionAt(u2);
            //GeoVector dir1 = dir12d.x*surface1.UDirection(uv1)+ dir12d.y * surface1.VDirection(uv1);
            //GeoVector dir2 = dir22d.x*surface2.UDirection(uv2)+ dir22d.y * surface2.VDirection(uv2);
            //GeoVector dir = curve3D.DirectionAt(v);
            //// dir1 should be in direction of curve3d, dir2 in the opposite dierection
            //if (dir1 * dir < 0) curve2D1.Reverse();
            //if (dir2 * dir > 0) curve2D2.Reverse();
            //ICurve2D[] splitted1 = curve2D1.Split(u1);
            //ICurve2D[] splitted2 = curve2D2.Split(u2);
            //if (splitted1 == null || splitted1.Length != 2) return null;
            //if (splitted2 == null || splitted2.Length != 2) return null;
            //IDualSurfaceCurve dsc1 = null, dsc2 = null;
            //dsc1 = new DualSurfaceCurve(splitted[0], surface1, splitted1[0], surface2, splitted2[1]);
            //dsc2 = new DualSurfaceCurve(splitted[1], surface1, splitted1[1], surface2, splitted2[0]);
            //return new IDualSurfaceCurve[] { dsc1, dsc2 };
        }

        ICurve IDualSurfaceCurve.Curve3D
        {
            get
            {
                return curve3D;
            }
        }
        ISurface IDualSurfaceCurve.Surface1
        {
            get
            {
                return surface1;
            }
        }
        ICurve2D IDualSurfaceCurve.Curve2D1
        {
            get
            {
                return curve2D1;
            }
        }
        ISurface IDualSurfaceCurve.Surface2
        {
            get
            {
                return surface2;
            }
        }
        ICurve2D IDualSurfaceCurve.Curve2D2
        {
            get
            {
                return curve2D2;
            }
        }
    }

    internal class Curve2DAspect : ICurve2D
    {
        /*
         * Diese Klasse hat den Zweck dort wo eine ICurve2D benötigt wird mit einer 3D Kurve arbeiten zu können.
         * 
         * 2D Kurven werden vor allem für die 2D Topologie benötigt (CompoundShape, Border), wo es im Innen und Außen
         * Vereinigen und Schneiden geht. Aber diese Kurven sind oft die Ränder eines Faces, wobei die 3D Beschreibung
         * einfacher ist als die 2D Beschreibung (z.B. schräger Schnitt eines Zylinders: in 3D eine Ellipse, in
         * 2D eine Sinuskurve auf der Parameterfläche des Zylinders) Die 2D Kurven werden oft mit NURBS angenähert,
         * während die 3D Kurven exakt sind. Deshalb ist es u.U. besser z.B. beim Schnitt die eine 3D Kurve mit der 
         * der anderen Kurve zugrundeliegenden Fläche zu schneiden und die Schnittpunkte ins 2D System zurückzurechnen.
         * 
         */

        IDualSurfaceCurve dualSurfaceCurve;
        bool onSurface1;
        bool clipped;
        GeoPoint startPoint, endPoint;
        GeoPoint2D startPoint2D, endPoint2D;
        ICurve2D clippedCurve;

        public Curve2DAspect(IDualSurfaceCurve dualSurfaceCurve, bool onSurface1)
        {
            this.dualSurfaceCurve = dualSurfaceCurve;
            this.onSurface1 = onSurface1;
        }

        private Curve2DAspect Clone()
        {
            Curve2DAspect res = new Curve2DAspect(dualSurfaceCurve, onSurface1);
            res.UserData.CloneFrom(this.UserData);
            return res;
        }

        /// <summary>
        /// Liefert die 3D Kurve zu diesem Objekt, wenn die Surface stimmt. Bestimmt bei geklippten Objekten
        /// die Start- und Endpunkte im 3D. Ansonsten ist es identisch mit der 3D Kurve
        /// </summary>
        /// <param name="onThisSurface"></param>
        /// <returns></returns>
        public ICurve Get3DCurve(ISurface onThisSurface)
        {
            if (onThisSurface != theSurface) return null;
            if (!clipped) return dualSurfaceCurve.Curve3D;
            // bei interpolatedDualSurfaceCurve ist schon geklippt, und hier wird nochmal
            // mit position of parameter gesucht und nochmal geklippt, das ist schlecht!!
            double startParam = dualSurfaceCurve.Curve3D.PositionOf(startPoint);
            double endParam = dualSurfaceCurve.Curve3D.PositionOf(endPoint);
            if (endParam < startParam)
            {
                double tmp = endParam;
                endParam = startParam;
                startParam = tmp;
            }
            if (dualSurfaceCurve.Curve3D.IsClosed)
            {
                ICurve[] splitted = dualSurfaceCurve.Curve3D.Split(startParam, endParam);
                if (splitted.Length == 2) // müsste wohl immer so eine
                {
                    double pos1 = splitted[0].PositionOf(theSurface.PointAt(clippedCurve.PointAt(0.5)));
                    double pos2 = splitted[1].PositionOf(theSurface.PointAt(clippedCurve.PointAt(0.5)));
                    if (Math.Abs(pos1 - 0.5) < Math.Abs(pos2 - 0.5))
                    {
                        return splitted[0];
                    }
                    else
                    {
                        return splitted[1];
                    }
                }
                else if (splitted.Length > 0)
                {   // sollte nicht vorkommen
                    return splitted[0];
                }
                else
                {   // sollte nicht vorkommen
                    return null;
                }
            }
            else
            {
                ICurve res = dualSurfaceCurve.Curve3D.Clone();
                res.Trim(startParam, endParam);
                return res;
            }
        }

        #region ICurve2D Members
        private ICurve2D theCurve
        {
            get
            {
                if (clipped) return clippedCurve;
                if (onSurface1) return dualSurfaceCurve.Curve2D1;
                else return dualSurfaceCurve.Curve2D2;
            }
        }
        private ISurface theSurface
        {
            get
            {
                if (onSurface1) return dualSurfaceCurve.Surface1;
                else return dualSurfaceCurve.Surface2;
            }
        }
        GeoPoint2D ICurve2D.StartPoint
        {
            get
            {
                if (clipped) return startPoint2D;
                else return theCurve.StartPoint;
            }
            set
            {
                clipped = true;
                startPoint2D = value;
                startPoint = theSurface.PointAt(value);
                clippedCurve = theCurve.Clone();
                clippedCurve.StartPoint = value;
            }
        }
        GeoPoint2D ICurve2D.EndPoint
        {
            get
            {
                if (clipped) return endPoint2D;
                else return theCurve.EndPoint;
            }
            set
            {
                clipped = true;
                endPoint2D = value;
                endPoint = theSurface.PointAt(value);
                clippedCurve = theCurve.Clone();
                clippedCurve.EndPoint = value;
            }
        }
        GeoVector2D ICurve2D.StartDirection
        {
            get
            {
                return theCurve.StartDirection;
            }
        }
        GeoVector2D ICurve2D.EndDirection
        {
            get
            {
                return theCurve.EndDirection;
            }
        }
        GeoVector2D ICurve2D.MiddleDirection
        {
            get
            {
                return theCurve.MiddleDirection;
            }
        }
        GeoVector2D ICurve2D.DirectionAt(double Position)
        {
            return theCurve.DirectionAt(Position);
        }
        GeoPoint2D ICurve2D.PointAt(double Position)
        {
            return theCurve.PointAt(Position);
        }
        double ICurve2D.PositionOf(GeoPoint2D p)
        {
            return theCurve.PositionOf(p);
        }
        double ICurve2D.PositionAtLength(double position)
        {
            return theCurve.PositionAtLength(position);
        }
        double ICurve2D.Length
        {
            get
            {
                return theCurve.Length;
            }
        }
        double ICurve2D.GetAreaFromPoint(GeoPoint2D p)
        {
            return theCurve.GetAreaFromPoint(p);
        }
        double ICurve2D.GetArea()
        {
            return theCurve.GetArea();
        }
        double ICurve2D.Sweep
        {
            get
            {
                return theCurve.Sweep;
            }
        }
        ICurve2D[] ICurve2D.Split(double Position)
        {
            ICurve2D[] splitted = theCurve.Split(Position);
            // eine oder zwei Kurven
            ICurve2D[] res = new ICurve2D[splitted.Length];
            for (int i = 0; i < splitted.Length; ++i)
            {
                Curve2DAspect c2da = Clone();
                res[i] = c2da;
                c2da.clipped = true;
                c2da.startPoint2D = splitted[i].StartPoint;
                c2da.endPoint2D = splitted[i].EndPoint;
                c2da.startPoint = theSurface.PointAt(c2da.startPoint2D);
                c2da.endPoint = theSurface.PointAt(c2da.endPoint2D);
                c2da.clippedCurve = splitted[i];
            }
            return res;
        }
        double ICurve2D.Distance(GeoPoint2D p)
        {
            return theCurve.Distance(p);
        }
        double ICurve2D.MinDistance(ICurve2D Other)
        {
            return theCurve.MinDistance(Other);
        }
        double ICurve2D.MinDistance(GeoPoint2D p)
        {
            return theCurve.MinDistance(p);
        }
        ICurve2D ICurve2D.Trim(double StartPos, double EndPos)
        {
            ICurve2D trimmed = theCurve.Trim(StartPos, EndPos);
            Curve2DAspect c2da = Clone();
            c2da.clipped = true;
            c2da.startPoint2D = trimmed.StartPoint;
            c2da.endPoint2D = trimmed.EndPoint;
            c2da.startPoint = theSurface.PointAt(c2da.startPoint2D);
            c2da.endPoint = theSurface.PointAt(c2da.endPoint2D);
            c2da.clippedCurve = trimmed;
            return c2da;
        }
        ICurve2D ICurve2D.Parallel(double Dist, bool approxSpline, double precision, double roundAngle)
        {
            ICurve2D parallel = theCurve.Parallel(Dist, approxSpline, precision, roundAngle);
            Curve2DAspect c2da = Clone();
            c2da.clipped = true;
            c2da.startPoint2D = parallel.StartPoint;
            c2da.endPoint2D = parallel.EndPoint;
            c2da.startPoint = theSurface.PointAt(c2da.startPoint2D);
            c2da.endPoint = theSurface.PointAt(c2da.endPoint2D);
            c2da.clippedCurve = parallel;
            return c2da;
        }
        GeoPoint2DWithParameter[] ICurve2D.Intersect(ICurve2D IntersectWith)
        {
            // hier bessere 3D Berechnung machen und in den 2D NURBS Zwischenpunkte einfügen
            // oder Schnittpunkte sammeln
            return theCurve.Intersect(IntersectWith);
        }
        GeoPoint2DWithParameter[] ICurve2D.Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            return theCurve.Intersect(StartPoint, EndPoint);
        }
        GeoPoint2D[] ICurve2D.PerpendicularFoot(GeoPoint2D FromHere)
        {
            return theCurve.PerpendicularFoot(FromHere);
        }
        GeoPoint2D[] ICurve2D.TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo)
        {
            return theCurve.TangentPoints(FromHere, CloseTo);
        }
        GeoPoint2D[] ICurve2D.TangentPointsToAngle(Angle ang, GeoPoint2D CloseTo)
        {
            return theCurve.TangentPointsToAngle(ang, CloseTo);
        }
        double[] ICurve2D.TangentPointsToAngle(GeoVector2D direction)
        {
            return theCurve.TangentPointsToAngle(direction);
        }
        double[] ICurve2D.GetInflectionPoints()
        {
            return theCurve.GetInflectionPoints();
        }
        void ICurve2D.Reverse()
        {
            if (!clipped)
            {
                clipped = true;
                clippedCurve = theCurve.CloneReverse(true);
                startPoint2D = clippedCurve.StartPoint;
                endPoint2D = clippedCurve.EndPoint;
                startPoint = theSurface.PointAt(startPoint2D);
                endPoint = theSurface.PointAt(endPoint2D);
            }
            else
            {
                clippedCurve.Reverse();
                GeoPoint2D tmp2d = startPoint2D;
                startPoint2D = endPoint2D;
                endPoint2D = tmp2d;
                GeoPoint tmp = startPoint;
                startPoint = endPoint;
                endPoint = tmp;
            }
        }
        ICurve2D ICurve2D.Clone()
        {
            Curve2DAspect c2da = Clone();
            c2da.clipped = clipped;
            if (clipped)
            {
                c2da.clippedCurve = clippedCurve;
                c2da.startPoint2D = startPoint2D;
                c2da.endPoint2D = endPoint2D;
                c2da.startPoint = startPoint;
                c2da.endPoint = endPoint;
            }
            c2da.UserData.CloneFrom(this.UserData);
            return c2da;
        }
        ICurve2D ICurve2D.CloneReverse(bool reverse)
        {
            Curve2DAspect c2da = Clone();
            c2da.clipped = clipped;
            if (clipped)
            {
                c2da.clippedCurve = clippedCurve;
                c2da.startPoint2D = startPoint2D;
                c2da.endPoint2D = endPoint2D;
                c2da.startPoint = startPoint;
                c2da.endPoint = endPoint;
            }
            if (reverse) (c2da as ICurve2D).Reverse();
            c2da.UserData.CloneFrom(this.UserData);
            return c2da;
        }
        void ICurve2D.Copy(ICurve2D toCopyfrom)
        {
            Curve2DAspect c = toCopyfrom as Curve2DAspect;
            theCurve.Copy(c.theCurve);
            UserData.CloneFrom(c.UserData);
        }
        IGeoObject ICurve2D.MakeGeoObject(Plane p)
        {
            return theCurve.MakeGeoObject(p);
        }
        ICurve2D ICurve2D.Project(Plane fromPlane, Plane toPlane)
        {
            return theCurve.Project(fromPlane, toPlane);
        }
        void ICurve2D.AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {
            theCurve.AddToGraphicsPath(path, forward);
        }
        bool ICurve2D.IsParameterOnCurve(double par)
        {
            return theCurve.IsParameterOnCurve(par);
        }
        bool ICurve2D.IsValidParameter(double par)
        {
            return theCurve.IsValidParameter(par);
        }
        IQuadTreeInsertable ICurve2D.GetExtendedHitTest()
        {
            return theCurve.GetExtendedHitTest();
        }
        double[] ICurve2D.GetSelfIntersections()
        {
            return theCurve.GetSelfIntersections();
        }
        bool ICurve2D.ReinterpretParameter(ref double p)
        {
            return theCurve.ReinterpretParameter(ref p);
        }
        ICurve2D ICurve2D.Approximate(bool linesOnly, double maxError)
        {
            // hier ggf 3d Kurve berücksichtigen
            return theCurve.Approximate(linesOnly, maxError);
        }
        private void MakeClipped()
        {
            if (!clipped)
            {
                clipped = true;
                clippedCurve = theCurve.Clone();
                startPoint2D = clippedCurve.StartPoint;
                endPoint2D = clippedCurve.EndPoint;
                startPoint = theSurface.PointAt(startPoint2D);
                endPoint = theSurface.PointAt(endPoint2D);
            }
        }
        void ICurve2D.Move(double x, double y)
        {
            MakeClipped();
            clippedCurve.Move(x, y);
            // jetzt stimmt die original 3D Kurve nicht mehr
            // das könnte Probleme machen
        }
        bool ICurve2D.IsClosed
        {
            get
            {
                return theCurve.IsClosed;
            }
        }
        ICurve2D ICurve2D.GetModified(ModOp2D m)
        {
            return theCurve.GetModified(m);
            // war vorher so: nicht sicher ob das jemand so braucht, macht Probleme wenn nicht geklippt
            //Curve2DAspect c2da = Clone();
            //c2da.MakeClipped();
            //c2da.clippedCurve = c2da.clippedCurve.GetModified(m);
            //// wie Move. Lösung: ModOp2D ansammeln
            //return c2da;
        }
        private UserData userData;
        public UserData UserData
        {
            get
            {
                if (userData == null) userData = new UserData();
                return userData;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.Curve2D.ICurve2D.GetFused (ICurve2D, double)"/>
        /// </summary>
        /// <param name="toFuseWith"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public ICurve2D GetFused(ICurve2D toFuseWith, double precision)
        {
            return theCurve.GetFused(toFuseWith, precision);
        }
        bool ICurve2D.TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {
            point = GeoPoint2D.Origin;
            deriv = deriv2 = GeoVector2D.NullVector;
            return false;
        }
        #endregion

        #region IQuadTreeInsertable Members

        BoundingRect IQuadTreeInsertable.GetExtent()
        {
            return theCurve.GetExtent();
        }

        bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            return theCurve.HitTest(ref rect, includeControlPoints);
        }

        public object ReferencedObject
        {
            get
            {
                return this;
            }
        }
        #endregion
    }

    /// <summary>
    /// A 2d curve as a projection of a 3d curve onto a surface. Sometimes it is easier to calculate points in 3d than in 2d. Then we use this as a more
    /// exact for of the curve than we get, when we approximate the curve in 2d.
    /// </summary>
    [Serializable()]
    public class ProjectedCurve : GeneralCurve2D, ISerializable
    {
        private double startParam; // start parameter on the 3d curve, together wit endParam also specifies the orientation
        private double endParam; // on the 3d curve
        ICurve curve3D;
        ISurface surface;
        BoundingRect periodicDomain; // only for periodic domains: to which period the 3d points should be mapped
        GeoPoint2D startPoint2d, endPoint2d;
        bool startPointIsPole, endPointIsPole;
#if DEBUG
        static int debugCounter = 0;
        private int debugCount; // to identify instance when debugging
#endif

        public ProjectedCurve(ICurve curve3D, ISurface surface, bool forward, BoundingRect domain, double precision = 0.0)
        {
#if DEBUG
            debugCount = debugCounter++;
#endif
            this.curve3D = curve3D; // keep in mind, the curve is not cloned, curve3D should not be modified after this
            this.surface = surface;
            List<GeoPoint> lpoles = new List<GeoPoint>();
            List<GeoPoint2D> lpoles2d = new List<GeoPoint2D>();
            GeoPoint2D cnt2d = domain.GetCenter();
            GeoPoint sp = curve3D.StartPoint;
            GeoPoint ep = curve3D.EndPoint;
            double[] us = surface.GetUSingularities();
            double prec = precision;
            if (prec == 0.0) prec = curve3D.Length * 1e-3; // changed to 1e-3, it is used to snap endpoints to poles
            startPoint2d = surface.PositionOf(curve3D.StartPoint);
            endPoint2d = surface.PositionOf(curve3D.EndPoint);
            bool distinctStartEndPoint = false;
            if ((surface.IsUPeriodic && Math.Abs(startPoint2d.x - endPoint2d.x) < surface.UPeriod * 1e-3) ||
                (surface.IsVPeriodic && Math.Abs(startPoint2d.y - endPoint2d.y) < surface.VPeriod * 1e-3))
            {   // adjust start and endpoint according to its neighbors
                GeoPoint2D p2d = surface.PositionOf(curve3D.PointAt(0.1));
                SurfaceHelper.AdjustPeriodic(surface, periodicDomain, ref p2d);
                BoundingRect ext = new BoundingRect(p2d);
                SurfaceHelper.AdjustPeriodic(surface, ext, ref startPoint2d);
                p2d = surface.PositionOf(curve3D.PointAt(0.9));
                SurfaceHelper.AdjustPeriodic(surface, periodicDomain, ref p2d);
                ext.MinMax(p2d);
                SurfaceHelper.AdjustPeriodic(surface, ext, ref endPoint2d);
                distinctStartEndPoint = true;
            }
            periodicDomain = domain;
            if (periodicDomain.IsEmpty() && (surface.IsUPeriodic || surface.IsVPeriodic))
            {
                // make a few points and assure that they don't jump over the periodic seam
                // if the curve3d doesn't jump around wildly, this should work. Maybe use curve3D.GetSavePositions?
                GeoPoint2D[] point2Ds = new GeoPoint2D[11];
#if DEBUG
                GeoPoint2D[] point2Dsdbg = new GeoPoint2D[11];
#endif
                for (int i = 0; i < 11; i++)
                {
                    point2Ds[i] = surface.PositionOf(curve3D.PointAt(i / 10.0));
#if DEBUG
                    //(surface as ISurfaceImpl).BoxedSurfaceEx.PositionOf(curve3D.PointAt(i / 10.0), out point2Dsdbg[i]);
#endif
                }
                for (int i = 0; i < 10; i++)
                {
                    GeoVector2D offset = GeoVector2D.NullVector;
                    if (surface.IsUPeriodic && Math.Abs(point2Ds[i + 1].x - point2Ds[i].x) > surface.UPeriod / 2.0)
                    {
                        if ((point2Ds[i + 1].x - point2Ds[i].x) < 0) offset.x = surface.UPeriod;
                        else offset.x = -surface.UPeriod;
                    }
                    if (surface.IsVPeriodic && Math.Abs(point2Ds[i + 1].y - point2Ds[i].y) > surface.VPeriod / 2.0)
                    {
                        if ((point2Ds[i + 1].y - point2Ds[i].y) < 0) offset.y = surface.VPeriod;
                        else offset.y = -surface.VPeriod;
                    }
                    point2Ds[i + 1] += offset;
                }
                for (int i = 0; i < 11; i++)
                {
                    periodicDomain.MinMax(point2Ds[i]);
                }
                startPoint2d = point2Ds[0];
                endPoint2d = point2Ds[10];
            }
            if (!periodicDomain.IsEmpty() && (!surface.IsUPeriodic || periodicDomain.Width < surface.UPeriod * (1 - 1e-6)) && (!surface.IsVPeriodic || periodicDomain.Height < surface.VPeriod * (1 - 1e-6)))
            {
                SurfaceHelper.AdjustPeriodic(surface, periodicDomain, ref startPoint2d);
                SurfaceHelper.AdjustPeriodic(surface, periodicDomain, ref endPoint2d);
            }
            startPointIsPole = endPointIsPole = false;
            for (int i = 0; i < us.Length; i++)
            {
                GeoPoint pl = surface.PointAt(new GeoPoint2D(us[i], cnt2d.y));
                if ((pl | sp) < prec)
                {
                    GeoPoint2D tmp = surface.PositionOf(curve3D.PointAt(0.1));
                    startPoint2d = new GeoPoint2D(us[i], tmp.y);
                    startPointIsPole = true;
                }
                if ((pl | ep) < prec)
                {
                    GeoPoint2D tmp = surface.PositionOf(curve3D.PointAt(0.9));
                    endPoint2d = new GeoPoint2D(us[i], tmp.y);
                    endPointIsPole = true;
                }
            }
            double[] vs = surface.GetVSingularities();
            for (int i = 0; i < vs.Length; i++)
            {
                GeoPoint pl = surface.PointAt(new GeoPoint2D(cnt2d.x, vs[i]));
                if ((pl | sp) < prec*10)
                {
                    GeoPoint2D tmp = surface.PositionOf(curve3D.PointAt(0.1));
                    startPoint2d = new GeoPoint2D(tmp.x, vs[i]);
                    startPointIsPole = true;
                }
                if ((pl | ep) < prec*10)
                {
                    GeoPoint2D tmp = surface.PositionOf(curve3D.PointAt(0.9));
                    endPoint2d = new GeoPoint2D(tmp.x, vs[i]);
                    endPointIsPole = true;
                }
            }
            if (forward)
            {
                startParam = 0.0;
                endParam = 1.0;
            }
            else
            {
                startParam = 1.0;
                endParam = 0.0;
            }
#if DEBUG
            this.MakeTriangulation();
#endif
        }
        public ProjectedCurve(ICurve curve3D, ISurface surface, double startParam, double endParam, BoundingRect domain)
        {
#if DEBUG
            debugCount = debugCounter++;
#endif
            this.curve3D = curve3D;
            this.surface = surface;
            this.startParam = startParam;
            this.endParam = endParam;
            periodicDomain = domain;
        }
        internal override void GetTriangulationPoints(out GeoPoint2D[] interpol, out double[] interparam)
        {
            GetTriangulationBasis(out interpol, out _, out interparam);
        }
        protected override void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] parameters)
        {
            double[] pars = curve3D.GetSavePositions();
            List<double> positions = new List<double>();
            for (int i = 0; i < pars.Length; i++)
            {
                double d = Get2dParameter(pars[i]);
                if (d > 1e-6 && d < 1.0 - 1e-6) positions.Add(d); // nur innerhalb des Bereichs und 0 und 1 nicht doppelt
            }
            positions.Add(0.0);
            positions.Add(1.0);
            if (positions.Count < 3) positions.Add(0.5);
            positions.Sort();
            List<double> lparameters = new List<double>();
            for (int i = 0; i < positions.Count; i++)
            {
                lparameters.Add(positions[i]);
            }
            List<GeoPoint2D> lpoints = new List<GeoPoint2D>();
            List<GeoVector2D> ldirections = new List<GeoVector2D>();
            for (int i = 0; i < positions.Count; i++)
            {
                GeoPoint2D p;
                GeoVector2D v;
                PointDirAt(positions[i], out p, out v);
                lpoints.Add(p);
                ldirections.Add(v);
            }

            bool check = true;
            // the interpolation should be smooth. Max. bending between interpolation points 45°, which makes sure, the baseApproximation
            // uses arcs, so that the start- and end-direction are correct
            while (check && lpoints.Count < 100)
            {
                check = false;
                for (int i = lpoints.Count - 1; i > 0; --i)
                {
                    if (Math.Abs(new SweepAngle(ldirections[i], ldirections[i - 1])) > Math.PI / 4)
                    {
                        double par = (positions[i] + positions[i - 1]) / 2.0;
                        GeoPoint2D p = PointAt(par);
                        GeoVector2D dir = DirectionAt(par);
                        lpoints.Insert(i, p);
                        ldirections.Insert(i, dir);
                        positions.Insert(i, par);
                        check = true;
                    }
                }
            }
            points = lpoints.ToArray();
            directions = ldirections.ToArray();
            parameters = positions.ToArray();
            if (surface.IsUPeriodic)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    if ((points[i].x - points[i - 1].x) > surface.UPeriod / 2.0) points[i].x -= surface.UPeriod;
                    if ((points[i].x - points[i - 1].x) < -surface.UPeriod / 2.0) points[i].x += surface.UPeriod;
                }
            }
            if (surface.IsVPeriodic)
            {
                for (int i = 1; i < points.Length; i++)
                {
                    if ((points[i].y - points[i - 1].y) > surface.VPeriod / 2.0) points[i].y -= surface.VPeriod;
                    if ((points[i].y - points[i - 1].y) < -surface.VPeriod / 2.0) points[i].y += surface.VPeriod;
                }
            }
            if (!periodicDomain.IsEmpty())
            {
                SurfaceHelper.AdjustPeriodic(surface, periodicDomain, points);
            }
        }
#if DEBUG
        public void DebugTest()
        {
            GeoPoint2D[] points;
            GeoVector2D[] directions;
            double[] parameters;
            GetTriangulationBasis(out points, out directions, out parameters);
            GeoPoint2D sp = this.StartPoint;
            GeoPoint2D ep = this.EndPoint;
        }
#endif
        public ICurve Curve3DFromParams
        {
            get
            {
                ICurve res;
                res = curve3D.Clone();
                if (IsReverse)
                {
                    res.Reverse();
                    if (startParam == 0.0 && endParam == 1.0) return res;
                    else
                    {
                        res.Trim(1 - startParam, 1 - endParam);
                        return res;
                    }
                }
                else
                {
                    if (startParam == 0.0 && endParam == 1.0) return res;
                    else
                    {
                        res.Trim(startParam, endParam);
                        return res;
                    }
                }
            }
        }
        public ICurve Curve3D
        {
            get
            {
                return curve3D;
            }
        }
        public ISurface Surface
        {
            get
            {
                return surface;
            }
        }
        public bool IsReverse
        {
            get => endParam < startParam;
            internal set
            {
                if (value != IsReverse)
                {
                    double tmp = startParam;
                    startParam = endParam;
                    endParam = tmp;
                    base.ClearTriangulation();
                }
            }
        }
        #region ICurve2D Members
        private double Get3dParameter(double par)
        {
            return startParam + par * (endParam - startParam);
        }
        private double Get2dParameter(double pos)
        {
            return (pos - startParam) / (endParam - startParam);
        }
        private void PointDirAt(double pos, out GeoPoint2D uv, out GeoVector2D dir)
        {
            // Projektion der 3d Richtung auf die Tangentialebene aufgespannt durch die beiden Richtungen
            double par3d = Get3dParameter(pos);
            uv = surface.PositionOf(curve3D.PointAt(par3d));
            if (!periodicDomain.IsEmpty()) SurfaceHelper.AdjustPeriodic(surface, periodicDomain, ref uv);
            if (par3d < 1e-6 && startPointIsPole) uv = startPoint2d;
            if (par3d > 1 - 1e-6 && endPointIsPole) uv = endPoint2d;
            if ((surface.IsUPeriodic && periodicDomain.Width > surface.UPeriod * (1 - 1e-6)) || (surface.IsVPeriodic && periodicDomain.Height > surface.VPeriod * (1 - 1e-6)))
            {   // do not adjust when the domain is the full period and we are close to the start or endpoint. These have been adjusted correctly in the constructor
                if (par3d < 1e-6) uv = startPoint2d;
                else if (par3d > 1 - 1e-6) uv = endPoint2d;
            }
            GeoVector dir3d = curve3D.DirectionAt(par3d);
            // Punkt auf der Fläche und Richtung im Raum:
            // wie drückt sich diese Raumrichtung in diru und dirv aus
            GeoPoint loc;
            GeoVector diru, dirv;
            surface.DerivationAt(uv, out loc, out diru, out dirv);
            Matrix m = DenseMatrix.OfColumnArrays(diru, dirv, diru ^ dirv);
            Vector b = new DenseVector(dir3d);
            Vector s = (Vector)m.Solve(b);
            if (s.IsValid())
            {   // what about the length? Added the .Normalized, because in "HyperCube Evolution - Double Z motor 1.stp" the direction length is definitely wrong
                dir = (endParam - startParam) * new GeoVector2D(s[0], s[1]).Normalized;
            }
            else
            {
                dir = GeoVector2D.NullVector;
            }
        }

        internal void ReflectModification(ISurface surface, ICurve curve3d)
        {   // a face has been modified, in 2d there are no changes, but the surface and the 3d curve must be adopted
            this.surface = surface;
            this.curve3D = curve3d;
            // following was moved to Edge.ReflectModification()
            //if (surface.UvChangesWithModification)
            //{
            //    startPoint2d = surface.PositionOf(curve3D.StartPoint);
            //    endPoint2d = surface.PositionOf(curve3D.EndPoint);
            //    ClearTriangulation();
            //    if (surface is IRestrictedDomain rd) surface.GetNaturalBounds(out periodicDomain.Left, out periodicDomain.Right, out periodicDomain.Bottom, out periodicDomain.Top);
            //}
        }

        public override GeoVector2D DirectionAt(double Position)
        {
            GeoPoint2D loc;
            GeoVector2D res;
            PointDirAt(Position, out loc, out res);
            return res;
        }
        public override GeoPoint2D PointAt(double Position)
        {
            double par3d = Get3dParameter(Position);
            GeoPoint2D res = surface.PositionOf(curve3D.PointAt(par3d));
            if (par3d < 1e-6 && startPointIsPole) res = startPoint2d;
            else if (par3d > 1 - 1e-6 && endPointIsPole) res = endPoint2d;
            if (!periodicDomain.IsEmpty()) SurfaceHelper.AdjustPeriodic(surface, periodicDomain, ref res);
            if ((surface.IsUPeriodic && periodicDomain.Width > surface.UPeriod * (1 - 1e-6)) || (surface.IsVPeriodic && periodicDomain.Height > surface.VPeriod * (1 - 1e-6)))
            {   // do not adjust when the domain is the full period and we are close to the start or endpoint. These have been adjusted correctly in the constructor
                if (par3d < 1e-6) res = startPoint2d;
                else if (par3d > 1 - 1e-6) res = endPoint2d;
            }
            return res;
        }
        public override void Reverse()
        {
            double tmp = startParam;
            startParam = endParam;
            endParam = tmp;
            base.ClearTriangulation();
        }
        public override ICurve2D Clone()
        {
            return new ProjectedCurve(curve3D, surface, startParam, endParam, periodicDomain);
        }
        public override void Copy(ICurve2D toCopyFrom)
        {
            ProjectedCurve pc = toCopyFrom as ProjectedCurve;
            if (pc != null)
            {
                startParam = pc.startParam;
                endParam = pc.endParam;
                curve3D = pc.curve3D;
                surface = pc.surface;
            }
        }
        public override ICurve2D Trim(double StartPos, double EndPos)
        {
            if (StartPos < EndPos)
            {
                double sp3d = Get3dParameter(StartPos);
                double ep3d = Get3dParameter(EndPos);
                return new ProjectedCurve(curve3D, surface, sp3d, ep3d, periodicDomain);
            }
            else
            {
                // es geht bei einer geschlossenen Kurve über den Nahtpunkt
                double sp3d = Get3dParameter(StartPos);
                double ep3d = Get3dParameter(EndPos);
                ICurve c3d = curve3D.Clone();
                c3d.Trim(sp3d, ep3d);
                return new ProjectedCurve(c3d, surface, 0, 1, periodicDomain);
            }
        }
        public override ICurve2D[] Split(double Position)
        {
            double sp3d = Get3dParameter(Position);
            if (Math.Abs(sp3d - startParam) < 1e-6 || Math.Abs(sp3d - endParam) < 1e-6) return new ICurve2D[] { Clone() };
            return new ICurve2D[] {
                new ProjectedCurve(curve3D, surface, startParam, sp3d, periodicDomain),
                new ProjectedCurve(curve3D, surface, sp3d, endParam, periodicDomain) };
        }
        public override void Move(double x, double y)
        {
            bool ok = true; // move the domain by the period
            if (surface.IsUPeriodic)
            {
                double dx = x / surface.UPeriod;
                ok &= (Math.Abs(dx - Math.Round(dx)) < 1e-10);
            }
            else
            {
                ok &= x == 0.0;
            }
            if (surface.IsVPeriodic)
            {
                double dy = y / surface.VPeriod;
                ok &= (Math.Abs(dy - Math.Round(dy)) < 1e-10);
            }
            else
            {
                ok &= y == 0.0;
            }
            if (ok)
            {
                GeoVector2D offset = new GeoVector2D(x, y);
                periodicDomain.Move(offset);
                startPoint2d += offset;
                endPoint2d += offset;
                base.ClearTriangulation();
            }
            else throw new ApplicationException("cannot move ProjectedCurve");
        }
        public override ICurve2D GetModified(ModOp2D m)
        {
            return base.GetModified(m);
        }
        #endregion
        #region ISerializable Members
        protected ProjectedCurve(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
#if DEBUG
            debugCount = debugCounter++;
#endif
            curve3D = info.GetValue("Curve3D", typeof(ICurve)) as ICurve;
            surface = info.GetValue("Surface", typeof(ISurface)) as ISurface;
            startParam = info.GetDouble("StartParam");
            endParam = info.GetDouble("EndParam");
            try
            {
                periodicDomain = (BoundingRect)info.GetValue("PeriodicDomain", typeof(BoundingRect));
            }
            catch (SerializationException)
            {
                periodicDomain = BoundingRect.EmptyBoundingRect;
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Curve3D", curve3D);
            info.AddValue("Surface", surface);
            info.AddValue("StartParam", startParam);
            info.AddValue("EndParam", endParam);
            info.AddValue("PeriodicDomain", periodicDomain);
        }

        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {
            point = GeoPoint2D.Origin;
            deriv = deriv2 = GeoVector2D.NullVector;
            return false;
        }

        #endregion
#if DEBUG
        public Polyline DebugPolyLine
        {
            get
            {
                GeoPoint[] pnts = new GeoPoint[100];
                for (int i = 0; i < pnts.Length; i++)
                {
                    pnts[i] = Plane.XYPlane.ToGlobal(PointAt(i / 99.0));

                }
                Polyline res = Polyline.Construct();
                res.SetPoints(pnts, false);
                return res;
            }
        }
#endif
    }
}
