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
        void Trim(GeoPoint startPoint, GeoPoint endPoint);
        void Reverse();
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
        void IDualSurfaceCurve.Trim(GeoPoint startPoint, GeoPoint endPoint)
        {
            if (Precision.IsEqual(curve3D.StartPoint, startPoint) && Precision.IsEqual(curve3D.EndPoint, endPoint)) return;
            if (Precision.IsEqual(curve3D.StartPoint, endPoint) && Precision.IsEqual(curve3D.EndPoint, startPoint))
            {
                curve3D.Reverse();
                curve2D1.Reverse();
                curve2D2.Reverse();
            }
            else
            {
                double startPar = curve3D.PositionOf(startPoint);
                double endPar = curve3D.PositionOf(endPoint);
                bool reverse = (endPar < startPar);
                if (reverse)
                {
                    double t = startPar;
                    startPar = endPar;
                    endPar = t;
                    GeoPoint tmp = startPoint;
                    startPoint = endPoint;
                    endPoint = tmp;
                }
                curve3D.Trim(startPar, endPar);
                GeoPoint2D sp2d = surface1.PositionOf(startPoint);
                GeoPoint2D ep2d = surface1.PositionOf(endPoint);
                curve2D1 = curve2D1.Trim(curve2D1.PositionOf(sp2d), curve2D1.PositionOf(ep2d));
                sp2d = surface2.PositionOf(startPoint);
                ep2d = surface2.PositionOf(endPoint);
                curve2D2 = curve2D2.Trim(curve2D2.PositionOf(sp2d), curve2D2.PositionOf(ep2d));
                if (reverse)
                {
                    curve3D.Reverse();
                    curve2D1.Reverse();
                    curve2D2.Reverse();
                }
            }
        }

        public void Reverse()
        {
            curve3D.Reverse();
            curve2D1.Reverse();
            curve2D2.Reverse();
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
}
