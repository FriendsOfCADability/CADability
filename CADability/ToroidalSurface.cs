using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// A toroidal surface which implements <see cref="ISurface"/>. The surface represents a torus in space.
    /// It is defined by its position in space (three directions) and two radii. The u parameter describes
    /// the "big" circles around the main axis, the v parameter describes the "small" circles.
    /// </summary>
    [Serializable()]
    public class ToroidalSurface : ISurfaceImpl, ISerializable, IDeserializationCallback, IImplicitPSurface, IExportStep, ISurfaceOfArcExtrusion
    {
        private ModOp toTorus; // diese ModOp modifiziert den Einheitstorus in den konkreten Torus
        private ModOp toUnit; // die inverse ModOp zum schnelleren Rechnen
        private double minorRadius; // im Unit-System. majorRadius ist dort immer 1
        Polynom imlicitPolynomial; // cached but not serialized implicit polynomial (degree 4)
        public ToroidalSurface(GeoPoint loc, GeoVector dirx, GeoVector diry, GeoVector dirz, double majorRadius, double minorRadius)
        {
            ModOp m1 = ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis },
                new GeoVector[] { majorRadius * dirx, majorRadius * diry, majorRadius * dirz });
            ModOp m2 = ModOp.Translate(loc.x, loc.y, loc.z);
            toTorus = m2 * m1;
            toUnit = toTorus.GetInverse();
            this.minorRadius = toUnit.Factor * minorRadius;
            // majorRadius is 1
        }
        internal ToroidalSurface(ModOp toTorus, double minorRadius)
        {
            this.toTorus = toTorus;
            this.minorRadius = minorRadius;
            toUnit = toTorus.GetInverse();
        }
        public GeoPoint Location
        {
            get
            {
                return toTorus * GeoPoint.Origin;
            }
        }
        public GeoVector Axis
        {
            get
            {
                return toTorus * GeoVector.ZAxis;
            }
        }
        public GeoVector XAxis
        {
            get
            {
                return toTorus * GeoVector.XAxis;
            }
        }
        public GeoVector YAxis
        {
            get
            {
                return toTorus * GeoVector.YAxis;
            }
        }
        public GeoVector ZAxis
        {
            get
            {
                return toTorus * GeoVector.ZAxis;
            }
        }
        public double MinorRadius
        {
            get
            {
                GeoVector r = toTorus * (minorRadius * GeoVector.ZAxis);
                return r.Length;
            }
        }
        /// <summary>
        /// The ring radius of the torus
        /// </summary>
        public double MajorRadius
        {
            get
            {
                return Math.Abs(toTorus.Factor);
            }
        }
        internal ModOp ToUnit
        {
            get
            {
                return toUnit;
            }
        }
        #region ISurfaceImpl Overrides
        // im Folgenden noch mehr überschreiben, das hier ist erst der Anfang:
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PointAt (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoPoint PointAt(GeoPoint2D uv)
        {
            GeoPoint punit = GeoPoint.Origin + (1 + minorRadius * Math.Cos(uv.y)) * (Math.Cos(uv.x) * GeoVector.XAxis + Math.Sin(uv.x) * GeoVector.YAxis) + minorRadius * Math.Sin(uv.y) * GeoVector.ZAxis;
            return toTorus * punit;
            // x = (1 + minorRadius * Math.Cos(uv.y))*Math.Cos(uv.x)
            // y = (1 + minorRadius * Math.Cos(uv.y))*Math.Sin(uv.x)
            // z = minorRadius * Math.Sin(uv.y)
            // Torus(u,v) := [(1 + minorRadius * cos(v))*cos(u),(1 + minorRadius * cos(v))*sin(u),minorRadius * sin(v)];
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override GeoPoint2D PositionOf(GeoPoint p)
        {
            GeoPoint pu = toUnit * p;
            return PositionOfInUnit(pu);
        }
        private GeoPoint2D PositionOfInUnit(GeoPoint pu)
        {
            double a = Math.Sqrt(pu.x * pu.x + pu.y * pu.y) - 1.0; // Abstand des Fußpunkts vom Einheitskreis
            double u = Math.Atan2(pu.y, pu.x);
            double v = Math.Atan2(pu.z, a);
            if (u < 0.0) u += 2 * Math.PI;
            if (v < 0.0) v += 2 * Math.PI; // lets always result in [0..2*pi,0..2*pi], we need it for GetProjectedCurve, comparing with Vvsingularity
            if (minorRadius > 1.0)
            {   // in this case the torus intersects itself and the result is ambiguous
                // it could be (u,v)
                GeoPoint p1 = GeoPoint.Origin + (1 + minorRadius * Math.Cos(v)) * (Math.Cos(u) * GeoVector.XAxis + Math.Sin(u) * GeoVector.YAxis) + minorRadius * Math.Sin(v) * GeoVector.ZAxis;
                double v2 = Math.Atan2(pu.z, -(a + 2));
                if (v2 < 0) v2 += 2 * Math.PI;
                GeoPoint p2 = GeoPoint.Origin + (1 + minorRadius * Math.Cos(v2)) * (Math.Cos(u + Math.PI) * GeoVector.XAxis + Math.Sin(u + Math.PI) * GeoVector.YAxis) + minorRadius * Math.Sin(v2) * GeoVector.ZAxis;
                double dd1 = pu | p1;
                double dd2 = pu | p2;
#if DEBUG
                if (dd1 > 1e-5 && dd2 > 1e-5)
                {

                }
#endif
                if (dd1 < dd2) return new GeoPoint2D(u, v);
                else return new GeoPoint2D(u + Math.PI, v2);
            }
            else
            {
                if (minorRadius < 0) v += Math.PI;
                return new GeoPoint2D(u, v);
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
            // wird das noch gebraucht?
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ISurface Clone()
        {
            return new ToroidalSurface(toTorus, minorRadius);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            boxedSurfaceEx = null;
            toTorus = m * toTorus;
            toUnit = toTorus.GetInverse();
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
            GeoVector unitDir = ToUnit * direction;
            if (Precision.SameDirection(unitDir, GeoVector.ZAxis, false))
            {
                // from top: 
                List<ICurve2D> res = new List<ICurve2D>();
                if (vmin <= 0 && 0 <= vmax) res.Add(new Line2D(new GeoPoint2D(umin, 0), new GeoPoint2D(umax, 0)));
                if (vmin <= Math.PI && Math.PI <= vmax) res.Add(new Line2D(new GeoPoint2D(umin, 0), new GeoPoint2D(umax, 0)));
                return res.ToArray();
            }
            else if (Precision.IsPerpendicular(unitDir, GeoVector.ZAxis, false))
            {
                double u = unitDir.To2D().Angle;
                double u0 = u - Math.PI / 2.0;
                double u1 = u + Math.PI / 2.0;
                while (u0 < umin) u0 += Math.PI * 2;
                while (u0 > umax) u0 -= Math.PI * 2;
                while (u1 < umin) u1 += Math.PI * 2;
                while (u1 > umax) u1 -= Math.PI * 2;
                List<ICurve2D> res = new List<ICurve2D>();
                if (umin <= u0 && u0 <= umax) res.Add(new Line2D(new GeoPoint2D(u0, vmin), new GeoPoint2D(u0, vmax)));
                if (umin <= u1 && u1 <= umax) res.Add(new Line2D(new GeoPoint2D(u1, vmin), new GeoPoint2D(u1, vmax)));
                return res.ToArray();
            }
            return base.GetTangentCurves(direction, umin, umax, vmin, vmax);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.UDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector UDirection(GeoPoint2D uv)
        {
            return toTorus * new GeoVector(
                -(1 + minorRadius * Math.Cos(uv.y)) * Math.Sin(uv.x),
                (1 + minorRadius * Math.Cos(uv.y)) * Math.Cos(uv.x),
                0);
        }
        private GeoVector UDirAxis(GeoPoint2D uv)
        {
            return toTorus * new GeoVector(
                -Math.Sin(uv.x),
                Math.Cos(uv.x),
                0);
        }
        static double Sqrt2 = Math.Sqrt(2.0);
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.VDirection (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector VDirection(GeoPoint2D uv)
        {
            return toTorus * new GeoVector(
                -minorRadius * Math.Sin(uv.y) * Math.Cos(uv.x),
                -minorRadius * Math.Sin(uv.y) * Math.Sin(uv.x),
                minorRadius * Math.Cos(uv.y));

        }
        public override void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            location = PointAt(uv);
            du = UDirection(uv);
            dv = VDirection(uv);
            duu = toTorus * new GeoVector(
                Math.Cos(uv.x) * (-minorRadius * Math.Cos(uv.y) - 1),
                -Math.Sin(uv.x) * (minorRadius * Math.Cos(uv.y) + 1), 0);
            duv = toTorus * new GeoVector(
                minorRadius * Math.Sin(uv.x) * Math.Sin(uv.y),
                -minorRadius * Math.Cos(uv.x) * Math.Sin(uv.y), 0);
            dvv = toTorus * new GeoVector(
                -minorRadius * Math.Cos(uv.x) * Math.Cos(uv.y),
                -minorRadius * Math.Sin(uv.x) * Math.Cos(uv.y),
                -minorRadius * Math.Sin(uv.y)
                );
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override GeoVector GetNormal(GeoPoint2D uv)
        {   // at a pole, the udirection may be 0, which results in a nullvector here, but there is actually a normal vector
            // We calculate the normal as the point on the surface - point on the (circular) axis for the provided value of u.
            // I think the normal should always be normalized per definition
            GeoPoint paxis = toTorus * new GeoPoint(Math.Cos(uv.x), Math.Sin(uv.x), 0.0);
            GeoPoint psurface = PointAt(uv);
            if (toTorus.Determinant < 0) return (paxis - psurface).Normalized; // reverse oriented
            else return (psurface - paxis).Normalized; // normal orientation
            // return UDirection(uv) ^ VDirection(uv);
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
                if (pc.Surface is ToroidalSurface)
                {
                    BoundingRect otherBounds = new BoundingRect(PositionOf(pc.Surface.PointAt(pc.StartPoint)), PositionOf(pc.Surface.PointAt(pc.EndPoint)));
                    if (pc.Surface.SameGeometry(pc.GetExtent(), this, otherBounds, Precision.eps, out ModOp2D notneeded))
                    {
                        return pc.Curve3DFromParams; // if trimmed or reversed still returns the correct 3d curve (but trimmed and/or reversed)
                    }
                }
            }
            if (curve2d is Line2D) // dieser Text könnte eigentlich in der Basismethode stehen
            {
                if (Math.Abs(curve2d.StartDirection.x) < Precision.eps)
                {
                    return FixedU(curve2d.StartPoint.x, curve2d.StartPoint.y, curve2d.EndPoint.y);
                }
                else if (Math.Abs(curve2d.StartDirection.y) < Precision.eps)
                {
                    return FixedV(curve2d.StartPoint.y, curve2d.StartPoint.x, curve2d.EndPoint.x);
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
                return true;
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
                return Math.PI * 2.0;
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
            Ellipse e = Ellipse.Construct();
            // the following sometimes returned arcs with 180° offset. 
            //GeoPoint p = new GeoPoint(Math.Cos(u), Math.Sin(u), 0.0);
            //Plane pln = new Plane(p, p - GeoPoint.Origin, GeoVector.ZAxis);
            //e.SetCirclePlaneCenterRadius(pln, p, minorRadius);
            //e.StartParameter = vmin;
            //e.SweepParameter = vmax - vmin;
            //e.Modify(toTorus);

            // this is not very elegant, could be improved, but returns the correct arc
            GeoPoint pstart = PointAt(new GeoPoint2D(u, vmin));
            GeoPoint pEnd = PointAt(new GeoPoint2D(u, vmax));
            GeoPoint pcenter = toTorus * (GeoPoint.Origin + (Math.Cos(u) * GeoVector.XAxis + Math.Sin(u) * GeoVector.YAxis));
            Plane plane = new Plane(pcenter, PointAt(new GeoPoint2D(u, 0)) - pcenter, PointAt(new GeoPoint2D(u, Math.PI / 2.0)) - pcenter);
            e.SetArcPlaneCenterStartEndPoint(plane, GeoPoint2D.Origin, plane.Project(pstart), plane.Project(pEnd), plane, vmin < vmax);
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
            double h = minorRadius * Math.Sin(v);
            Plane pln = new Plane(Plane.XYPlane, h);
            e.SetCirclePlaneCenterRadius(pln, new GeoPoint(0, 0, h), 1.0 + minorRadius * Math.Cos(v));
            e.StartParameter = umin;
            e.SweepParameter = umax - umin;
            e.Modify(toTorus);
            return e;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetLineIntersection (GeoPoint, GeoVector)"/>
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public override GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            GeoPoint[] ga = GetLineIntersection3D(startPoint, direction);
            GeoPoint2D[] erg = new GeoPoint2D[ga.Length];
            for (int i = 0; i < ga.Length; ++i)
            {
                erg[i] = PositionOfInUnit(ga[i]);
            }
            return erg;
        }
        private GeoPoint[] GetLineIntersection3D(GeoPoint startPoint, GeoVector direction)
        {   // get 3D-Points in the unit-system
            GeoPoint sp = toUnit * startPoint;
            GeoVector dirline = toUnit * direction;
            dirline.Norm();
            double dl = Geometry.DistPL(GeoPoint.Origin, sp, dirline);
            if (dl > 1 + minorRadius) return new GeoPoint[0]; // certainly no intersection
            List<GeoPoint> sol = new List<GeoPoint>();
            // test for tangential (because polynomial is bad in this case)
            GeoPoint closest = Geometry.DropPL(GeoPoint.Origin, sp, dirline);
            GeoPoint2D closest2D = PositionOfInUnit(closest);
            GeoPoint punit = GeoPoint.Origin + (1 + minorRadius * Math.Cos(closest2D.y)) * (Math.Cos(closest2D.x) * GeoVector.XAxis + Math.Sin(closest2D.x) * GeoVector.YAxis) + minorRadius * Math.Sin(closest2D.y) * GeoVector.ZAxis;
            if ((punit | closest) < 1e-6) sol.Add(punit); // in the unit system!
            double a, b, c, d, e;
            double x1, x2, x3;
            x1 = dirline.x * dirline.x + dirline.y * dirline.y + dirline.z * dirline.z;
            x2 = 2 * (dirline.x * sp.x + dirline.y * sp.y + dirline.z * sp.z);
            x3 = sp.x * sp.x + sp.y * sp.y + sp.z * sp.z + 1 - minorRadius * minorRadius;
            a = x1 * x1;
            b = 2 * x1 * x2;
            c = 2 * x1 * x3 + x2 * x2 - 4 * (dirline.x * dirline.x + dirline.y * dirline.y);
            d = 2 * x2 * x3 - 8 * (dirline.x * sp.x + dirline.y * sp.y);
            e = x3 * x3 - 4 * (sp.x * sp.x + sp.y * sp.y);
            //double[] s1 = new double[4];
            //int nl = Geometry.ragle4(a, b, c, d, e, s1);
            try
            {
                List<double> s = RealPolynomialRootFinder.FindRoots(a, b, c, d, e);
#if MATHNET
#if DEBUG
                System.Numerics.Complex[] roots = MathNet.Numerics.FindRoots.Polynomial(new double[] { a, b, c, d, e });
#endif
#endif
                for (int i = 0; i < s.Count; i++)
                {
                    double test1 = s[i];
                    double test2 = test1 * test1;
                    double test3 = test1 * test2;
                    double test4 = test1 * test3;
                    double test = a * test4 + b * test3 + c * test2 + d * test1 + e;
                    // sometimes the roots are strange and probably wrong, in theory test must be 0.0
                    if (Math.Abs(test) < 1e-6)
                    {
                        x1 = sp.x + s[i] * dirline.x;
                        x2 = sp.y + s[i] * dirline.y;
                        x3 = sp.z + s[i] * dirline.z;
                        sol.Add(new GeoPoint(x1, x2, x3));
                    }
                    else
                    {
                        x1 = sp.x + s[i] * dirline.x;
                        x2 = sp.y + s[i] * dirline.y;
                        x3 = sp.z + s[i] * dirline.z;
                        GeoPoint pt = new GeoPoint(x1, x2, x3);
                        double dd = PointAt(PositionOf(pt)) | pt;
                    }
                }
            }
            catch (ApplicationException) { }
            return sol.ToArray();
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
            Plane pln = new Plane(toUnit * pl.Location, toUnit * pl.DirectionX, toUnit * pl.DirectionY);
            int degree = 3;
            if (Precision.IsPerpendicular(GeoVector.ZAxis, pln.Normal, false))
            {
                double d = Math.Abs(pln.Distance(GeoPoint.Origin));
                if (d - (1 + minorRadius) >= -Precision.eps)
                {
                    // System.Diagnostics.Trace.WriteLine("Das Ebene trifft nicht den Torus oder nur im einem Punkt");
                    return Array.Empty<IDualSurfaceCurve>();
                }
                if (d <= Precision.eps)
                #region Zwei Kreis
                {
                    // System.Diagnostics.Trace.WriteLine("Zwei Kreis oder keine");

                    GeoVector v = pln.Normal ^ GeoVector.ZAxis;
                    v.Norm();
                    GeoPoint center1 = toTorus * (new GeoPoint(v.x, v.y, 0));
                    GeoPoint center2 = toTorus * (new GeoPoint(-v.x, -v.y, 0));
                    GeoVector majaxis = toTorus * (minorRadius * v);
                    GeoVector minaxis = toTorus * (minorRadius * GeoVector.ZAxis);
                    //Der Erste Kreis
                    Ellipse elli1 = Ellipse.Construct();
                    elli1.SetEllipseCenterAxis(new GeoPoint(v.x, v.y, 0), minorRadius * v, minorRadius * GeoVector.ZAxis);
                    elli1.SweepParameter = vmax - vmin;
                    elli1.StartParameter = vmin;
                    elli1.Modify(toTorus);
                    GeoPoint2D tst1 = PositionOf(elli1.PointAt(0.5));
                    ICurve2D c2dpl1 = pl.GetProjectedCurve(elli1, 0.0); // must be a circle (Ellipse)
                    ICurve2D c2dtr1 = new Line2D(PositionOf(elli1.StartPoint), PositionOf(elli1.EndPoint));
                    DualSurfaceCurve dsc1 = new DualSurfaceCurve(elli1, this, c2dtr1, pl, c2dpl1);
                    //Der Zweite Kreis
                    Ellipse elli2 = Ellipse.Construct();
                    elli2.SetEllipseCenterAxis(new GeoPoint(-v.x, -v.y, 0), -minorRadius * v, minorRadius * GeoVector.ZAxis);
                    elli2.StartParameter = vmin;
                    elli2.SweepParameter = vmax - vmin;
                    elli2.Modify(toTorus);
                    GeoPoint2D tst2 = PositionOf(elli1.PointAt(0.5));
                    ICurve2D c2dpl2 = pl.GetProjectedCurve(elli2, 0.0); // must be a circle (Ellipse)
                    ICurve2D c2dtr2 = new Line2D(PositionOf(elli2.StartPoint), PositionOf(elli2.EndPoint));
                    DualSurfaceCurve dsc2 = new DualSurfaceCurve(elli2, this, c2dtr2, pl, c2dpl2);
                    return new IDualSurfaceCurve[] { dsc1, dsc2 };
                }
                #endregion
                if (d - (1 - minorRadius) <= Precision.eps)
                #region Zwei Kurven
                {
                    // System.Diagnostics.Trace.WriteLine("Zwei Kurven");
                    GeoVector v = GeoVector.ZAxis ^ pln.Normal;
                    v.Norm();
                    GeoVector2D dirline = new GeoVector2D(v.x, v.y);
                    double a = Math.Atan2(pln.Normal.y, pln.Normal.x);
                    GeoPoint2D cnt = new GeoPoint2D(d * Math.Cos(a), d * Math.Sin(a));
                    if (!Precision.IsPointOnPlane(new GeoPoint(cnt.x, cnt.y, 0), pln))
                    {
                        cnt.x = -cnt.x;
                        cnt.y = -cnt.y;
                    }
                    int npts = 20;
                    List<GeoPoint> l1p = new List<GeoPoint>(2 * npts);
                    List<GeoPoint> l1n = new List<GeoPoint>(2 * npts);
                    List<GeoPoint> l2p = new List<GeoPoint>(2 * npts);
                    List<GeoPoint> l2n = new List<GeoPoint>(2 * npts);

                    for (int i = 0; i < 2 * npts + 1; i++)
                    {
                        double cz = minorRadius * Math.Sin(i * 2 * Math.PI / (2 * npts));
                        double r = 1 + minorRadius * Math.Cos(i * 2 * Math.PI / (2 * npts));
                        GeoPoint2D[] tmp = Geometry.IntersectLC(cnt, dirline, GeoPoint2D.Origin, r);
                        if (tmp.Length == 2)
                        {
                            if (tmp[0].x > cnt.x)
                            {
                                if (i <= npts)
                                {
                                    l1p.Add(new GeoPoint(tmp[0].x, tmp[0].y, cz));
                                    l2p.Add(new GeoPoint(tmp[1].x, tmp[1].y, cz));
                                }
                                if (i >= npts)
                                {
                                    l1n.Add(new GeoPoint(tmp[0].x, tmp[0].y, cz));
                                    l2n.Add(new GeoPoint(tmp[1].x, tmp[1].y, cz));
                                }
                            }
                            else
                            {
                                if (i <= npts)
                                {
                                    l1p.Add(new GeoPoint(tmp[1].x, tmp[1].y, cz));
                                    l2p.Add(new GeoPoint(tmp[0].x, tmp[0].y, cz));
                                }
                                if (i >= npts)
                                {
                                    l1n.Add(new GeoPoint(tmp[1].x, tmp[1].y, cz));
                                    l2n.Add(new GeoPoint(tmp[0].x, tmp[0].y, cz));
                                }
                            }
                        }
                        else
                        {
                            if (i <= npts)
                            {
                                l1p.Add(new GeoPoint(tmp[0].x, tmp[0].y, cz));
                                l2p.Add(new GeoPoint(tmp[0].x, tmp[0].y, cz));
                            }
                            if (i >= npts)
                            {
                                l1n.Add(new GeoPoint(tmp[0].x, tmp[0].y, cz));
                                l2n.Add(new GeoPoint(tmp[0].x, tmp[0].y, cz));
                            }
                        }
                    }

                    degree = 3;
                    //Kurve 1p im Welt System
                    GeoPoint[] pnts1 = l1p.ToArray();
                    GeoPoint[] ptmp = new GeoPoint[pnts1.Length];
                    for (int i = 0; i < pnts1.Length; i++)
                    {
                        ptmp[i] = toTorus * pnts1[i];
                    }
                    BSpline c3d1p = this.Refine(ptmp, degree, false, pl, precision);
                    ////Kurve 1p im (u,v) System
                    pnts1 = new GeoPoint[c3d1p.ThroughPointCount];
                    for (int i = 0; i < c3d1p.ThroughPointCount; i++)
                        pnts1[i] = toUnit * c3d1p.GetThroughPoint(i);
                    GeoPoint2D[] pnts = new GeoPoint2D[c3d1p.ThroughPointCount];
                    for (int i = 0; i < pnts1.Length; i++)
                    {
                        double zc = Math.Sqrt(pnts1[i].x * pnts1[i].x + pnts1[i].y * pnts1[i].y) - 1.0;
                        pnts[i].x = Math.Atan2(pnts1[i].y, pnts1[i].x);
                        pnts[i].y = Math.Atan2(pnts1[i].z, zc);
                    }
                    BSpline2D.AdjustPeriodic(pnts, 2 * Math.PI, 2 * Math.PI);
                    BSpline2D c2d1p = new BSpline2D(pnts, degree, false);
                    //Kurve 1p auf der Ebene pl
                    ICurve2D c2dpl1p = (c3d1p as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

                    //Kurve 1n im Welt System
                    pnts1 = l1n.ToArray();
                    ptmp = new GeoPoint[pnts1.Length];
                    for (int i = 0; i < pnts1.Length; i++)
                    {
                        ptmp[i] = toTorus * pnts1[i];
                    }
                    BSpline c3d1n = this.Refine(ptmp, degree, false, pl, precision);
                    ////Kurve 1n im (u,v) System
                    pnts1 = new GeoPoint[c3d1n.ThroughPointCount];
                    for (int i = 0; i < c3d1n.ThroughPointCount; i++)
                        pnts1[i] = toUnit * c3d1n.GetThroughPoint(i);
                    pnts = new GeoPoint2D[c3d1n.ThroughPointCount];
                    for (int i = 0; i < pnts1.Length; i++)
                    {
                        double zc = Math.Sqrt(pnts1[i].x * pnts1[i].x + pnts1[i].y * pnts1[i].y) - 1.0;
                        pnts[i].x = Math.Atan2(pnts1[i].y, pnts1[i].x);
                        pnts[i].y = Math.Atan2(pnts1[i].z, zc);
                    }
                    BSpline2D.AdjustPeriodic(pnts, 2 * Math.PI, 2 * Math.PI);
                    BSpline2D c2d1n = new BSpline2D(pnts, degree, false);
                    //Kurve 1n auf der Ebene pl
                    ICurve2D c2dpl1n = (c3d1n as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

                    //Kurve 2p im Welt System
                    pnts1 = l2p.ToArray();
                    ptmp = new GeoPoint[pnts1.Length];
                    for (int i = 0; i < pnts1.Length; i++)
                    {
                        ptmp[i] = toTorus * pnts1[i];
                    }
                    BSpline c3d2p = this.Refine(ptmp, degree, false, pl, precision);
                    ////Kurve 2p im (u,v) System
                    pnts1 = new GeoPoint[c3d2p.ThroughPointCount];
                    for (int i = 0; i < c3d2p.ThroughPointCount; i++)
                        pnts1[i] = toUnit * c3d2p.GetThroughPoint(i);

                    pnts = new GeoPoint2D[c3d2p.ThroughPointCount];
                    for (int i = 0; i < pnts1.Length; i++)
                    {
                        double zc = Math.Sqrt(pnts1[i].x * pnts1[i].x + pnts1[i].y * pnts1[i].y) - 1.0;
                        pnts[i].x = Math.Atan2(pnts1[i].y, pnts1[i].x);
                        pnts[i].y = Math.Atan2(pnts1[i].z, zc);
                    }
                    BSpline2D.AdjustPeriodic(pnts, 2 * Math.PI, 2 * Math.PI);
                    BSpline2D c2d2p = new BSpline2D(pnts, degree, false);
                    //Kurve 2p auf der Ebene pl
                    ICurve2D c2dpl2p = (c3d2p as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

                    //Kurve 2n im Welt System
                    pnts1 = l2n.ToArray();
                    ptmp = new GeoPoint[pnts1.Length];
                    for (int i = 0; i < pnts1.Length; i++)
                    {
                        ptmp[i] = toTorus * pnts1[i];
                    }
                    BSpline c3d2n = this.Refine(ptmp, degree, false, pl, precision);
                    ////Kurve 2n im (u,v) System
                    pnts1 = new GeoPoint[c3d2n.ThroughPointCount];
                    for (int i = 0; i < c3d2n.ThroughPointCount; i++)
                        pnts1[i] = toUnit * c3d2n.GetThroughPoint(i);

                    pnts = new GeoPoint2D[c3d2n.ThroughPointCount];
                    for (int i = 0; i < pnts1.Length; i++)
                    {
                        double zc = Math.Sqrt(pnts1[i].x * pnts1[i].x + pnts1[i].y * pnts1[i].y) - 1.0;
                        pnts[i].x = Math.Atan2(pnts1[i].y, pnts1[i].x);
                        pnts[i].y = Math.Atan2(pnts1[i].z, zc);
                    }
                    BSpline2D.AdjustPeriodic(pnts, 2 * Math.PI, 2 * Math.PI);
                    BSpline2D c2d2n = new BSpline2D(pnts, degree, false);
                    //Kurve 2n auf der Ebene pl
                    ICurve2D c2dpl2n = (c3d2n as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

                    DualSurfaceCurve dsc1p = new DualSurfaceCurve(c3d1p, this, c2d1p, pl, c2dpl1p);
                    DualSurfaceCurve dsc1n = new DualSurfaceCurve(c3d1n, this, c2d1n, pl, c2dpl1n);
                    DualSurfaceCurve dsc2p = new DualSurfaceCurve(c3d2p, this, c2d2p, pl, c2dpl2p);
                    DualSurfaceCurve dsc2n = new DualSurfaceCurve(c3d2n, this, c2d2n, pl, c2dpl2n);
                    return new IDualSurfaceCurve[] { dsc1p, dsc1n, dsc2p, dsc2n };
                    // return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
                }
                #endregion

                #region Eine Kurve
                // System.Diagnostics.Trace.WriteLine("Eine Kurve");
                //////
                GeoVector kv = GeoVector.ZAxis ^ pln.Normal;
                kv.Norm();
                GeoVector2D kdirline = new GeoVector2D(kv.x, kv.y);

                double ka = Math.Atan2(pln.Normal.y, pln.Normal.x);
                GeoPoint2D kcnt = new GeoPoint2D(d * Math.Cos(ka), d * Math.Sin(ka));
                if (!Precision.IsPointOnPlane(new GeoPoint(kcnt.x, kcnt.y, 0), pln))
                {
                    kcnt.x = -kcnt.x;
                    kcnt.y = -kcnt.y;
                }
                int np = 5;
                GeoPoint[] kpnts1 = new GeoPoint[4 * np + 1];
                GeoPoint2D[] kpnts = new GeoPoint2D[4 * np + 1];
                GeoPoint2D[] ktmp;
                double kr;

                double b = Math.Acos((d - 1) / minorRadius);
                double beta;
                double f;
                for (int i = 0; i < np; i++)
                {
                    beta = i * b / np;
                    kpnts1[i].z = minorRadius * Math.Sin(beta);
                    kpnts1[2 * np - i].z = kpnts1[i].z;
                    kpnts1[2 * np + i].z = -kpnts1[i].z;
                    kpnts1[4 * np - i].z = -kpnts1[i].z;

                    kr = 1 + minorRadius * Math.Cos(beta);
                    ktmp = Geometry.IntersectLC(kcnt, kdirline, GeoPoint2D.Origin, kr);
                    if (ktmp[0].x > kcnt.x)
                    {
                        kpnts1[i].x = ktmp[0].x;
                        kpnts1[i].y = ktmp[0].y;
                        kpnts1[2 * np - i].x = ktmp[1].x;
                        kpnts1[2 * np - i].y = ktmp[1].y;
                        kpnts1[4 * np - i].x = ktmp[0].x;
                        kpnts1[4 * np - i].y = ktmp[0].y;
                        kpnts1[2 * np + i].x = ktmp[1].x;
                        kpnts1[2 * np + i].y = ktmp[1].y;
                    }
                    else
                    {
                        kpnts1[i].x = ktmp[1].x;
                        kpnts1[i].y = ktmp[1].y;
                        kpnts1[2 * np - i].x = ktmp[0].x;
                        kpnts1[2 * np - i].y = ktmp[0].y;
                        kpnts1[4 * np - i].x = ktmp[1].x;
                        kpnts1[4 * np - i].y = ktmp[1].y;
                        kpnts1[2 * np + i].x = ktmp[0].x;
                        kpnts1[2 * np + i].y = ktmp[0].y;
                    }
                }
                kpnts1[np].x = kcnt.x;
                kpnts1[np].y = kcnt.y;
                kpnts1[np].z = minorRadius * Math.Sin(b);
                kpnts1[3 * np].x = kcnt.x;
                kpnts1[3 * np].y = kcnt.y;
                kpnts1[3 * np].z = -kpnts1[np].z;

                ////Kurve im Welt System
                GeoPoint[] kptmp = new GeoPoint[kpnts1.Length];
                for (int i = 0; i < kptmp.Length; i++)
                {
                    kptmp[i] = toTorus * kpnts1[i];
                }
                BSpline kc3d = this.Refine(kptmp, 3, true, pl, precision);

                ////Kurve im (u,v) System
                kpnts1 = new GeoPoint[kc3d.ThroughPointCount];
                for (int i = 0; i < kc3d.ThroughPointCount; i++)
                    kpnts1[i] = toUnit * kc3d.GetThroughPoint(i);

                kpnts = new GeoPoint2D[kc3d.ThroughPointCount];
                for (int i = 0; i < kpnts1.Length; i++)
                {
                    f = Math.Sqrt(kpnts1[i].x * kpnts1[i].x + kpnts1[i].y * kpnts1[i].y) - 1.0;
                    kpnts[i].x = Math.Atan2(kpnts1[i].y, kpnts1[i].x);
                    kpnts[i].y = Math.Atan2(kpnts1[i].z, f);
                }
                BSpline2D.AdjustPeriodic(kpnts, 2 * Math.PI, 2 * Math.PI);
                BSpline2D kc2d = new BSpline2D(kpnts, 3, true);

                ////Kurve auf der Ebene pl
                ICurve2D kc2dpl = (kc3d as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

                DualSurfaceCurve kdsc = new DualSurfaceCurve(kc3d, this, kc2d, pl, kc2dpl);
                return new IDualSurfaceCurve[] { kdsc };
                //return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
                #endregion
            }
            if (Precision.SameDirection(GeoVector.ZAxis, pln.Normal, false))
            #region Zwei Kreis _|_ ZAxis
            {
                BoundingRect bounds = new BoundingRect(umin, vmin, umax, vmax);
                GeoPoint tst = this.PointAt(new GeoPoint2D((umin + umax) / 2.0, (vmin + vmax) / 2.0)); // some point on the torus
                tst = pl.PointAt(pl.PositionOf(tst)); // perpendicular projection of some torus point onto the plane
                GeoPoint tcnt = pl.PointAt(pl.PositionOf(Location));// perpendicular projection of torus center point onto the plane
                GeoPoint2D[] isps = this.GetLineIntersection(tcnt, tst - tcnt);
                if (isps.Length > 0)
                {
                    List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                    List<GeoPoint2D> usedIps = new List<GeoPoint2D>();
                    for (int i = 0; i < isps.Length; i++)
                    {   // it is important to not have duplicate results.
                        SurfaceHelper.AdjustPeriodic(this, bounds, ref isps[i]);
                        // if (!bounds.Contains(isps[i])) continue;
                        bool alreadyused = false;
                        for (int j = 0; j < usedIps.Count; j++)
                        {
                            if (Math.Abs(usedIps[j].y - isps[i].y) < Precision.eps)
                            {
                                alreadyused = true;
                                break;
                            }
                        }
                        if (alreadyused) continue;
                        usedIps.Add(isps[i]);
                        Line2D l2d = new Line2D(new GeoPoint2D(umin, isps[i].y), new GeoPoint2D(umax, isps[i].y));
                        ICurve c3d = Make3dCurve(l2d); // this is a circular arc
                        ICurve2D a2d = pl.GetProjectedCurve(c3d, 0.0); // this is a 2d arc
                        res.Add(new DualSurfaceCurve(c3d, this, l2d, pl, a2d));
                    }
                    return res.ToArray();
                }
                else
                {
                    return Array.Empty<IDualSurfaceCurve>();
                }

                //Unreachable code
                /*
                                GeoPoint onz = pln.Intersect(GeoPoint.Origin, GeoVector.ZAxis);
                                double d = onz.z;
                                if (Math.Abs(d) - Math.Abs(minorRadius) > Precision.eps)
                                #region nichts
                                {
                                    return new IDualSurfaceCurve[0];
                                }
                                #endregion
                                if (Math.Abs(d) - Math.Abs(minorRadius) > -Precision.eps)
                                #region ein Kreis
                                {
                                    // System.Diagnostics.Trace.WriteLine("ein Kreis ");
                                    GeoVector majax = new GeoVector(1, 0, 0);
                                    GeoVector minax = new GeoVector(0, 1, 0);
                                    GeoPoint cnt = new GeoPoint(0, 0, d);
                                    GeoPoint center = toTorus * cnt;
                                    GeoVector majaxis = toTorus * majax;
                                    GeoVector minaxis = toTorus * minax;
                                    //im Weltsystem
                                    Ellipse elli = Ellipse.Construct();
                                    elli.SetEllipseCenterAxis(center, majaxis, minaxis);
                                    GeoPoint2D centerOnPl = pl.PositionOf(center);
                                    //Auf der Ebene
                                    GeoPoint2D p1OnPl = pl.PositionOf(center + majaxis);
                                    GeoPoint2D p2OnPl = pl.PositionOf(center - minaxis);
                                    Ellipse2D elli2d = Geometry.Ellipse2P2T(p1OnPl, p2OnPl, p2OnPl - centerOnPl, p1OnPl - centerOnPl);
                                    ICurve2D c2dpl = elli2d.Trim(0.0, 1.0);
                                    //Im (u,v) System
                                    GeoPoint2D[] pnts = new GeoPoint2D[50];
                                    double f = Math.PI / 2;
                                    if (d < 0)
                                        f = 3 * Math.PI / 2;
                                    for (int i = 0; i < pnts.Length; i++)
                                    {
                                        pnts[i].x = i * 2 * Math.PI / (pnts.Length - 1);
                                        pnts[i].y = f;
                                    }
                                    BSpline2D c2d = new BSpline2D(pnts, 2, false);
                                    DualSurfaceCurve dsc = new DualSurfaceCurve(elli, this, c2d, pl, c2dpl);
                                    return new IDualSurfaceCurve[] { dsc };
                                }
                                #endregion
                                else
                                #region zwei Kreis
                                {
                                    // System.Diagnostics.Trace.WriteLine("zwei Kreis ");
                                    double f = Math.Sqrt(minorRadius * minorRadius - d * d);
                                    double rmax = 1 + f;
                                    double rmin = 1 - f;
                                    double amax = Math.Atan2(Math.Abs(d), f);
                                    //Erste Kreis
                                    GeoVector majax = new GeoVector(rmax, 0, 0);
                                    GeoVector minax = new GeoVector(0, rmax, 0);
                                    GeoPoint cnt = new GeoPoint(0, 0, d);
                                    GeoPoint center = toTorus * cnt;
                                    GeoVector majaxis = toTorus * majax;
                                    GeoVector minaxis = toTorus * minax;
                                    //im Weltsystem
                                    Ellipse elli1 = Ellipse.Construct();
                                    elli1.SetEllipseCenterAxis(center, majaxis, minaxis);
                                    elli1.StartParameter = 0.0; // wird mit obigem nicht gesetzt
                                    elli1.SweepParameter = 2.0 * Math.PI;
                                    GeoPoint2D centerOnPl = pl.PositionOf(center);
                                    //Auf der Ebene
                                    GeoPoint2D p1OnPl = pl.PositionOf(center + majaxis);
                                    GeoPoint2D p2OnPl = pl.PositionOf(center - minaxis);
                                    Ellipse2D elli2d = Geometry.Ellipse2P2T(p1OnPl, p2OnPl, p2OnPl - centerOnPl, p1OnPl - centerOnPl);
                                    ICurve2D c2dpl1 = elli2d.Trim(0.0, 1.0);
                                    //Im (u,v) System
                                    GeoPoint2D[] pnts = new GeoPoint2D[50];
                                    f = amax;
                                    if (d < 0)
                                        f = 2 * Math.PI - amax;
                                    for (int i = 0; i < pnts.Length; i++)
                                    {
                                        pnts[i].x = i * 2 * Math.PI / (pnts.Length - 1);
                                        pnts[i].y = f;
                                    }
                                    BSpline2D c2d1 = new BSpline2D(pnts, 2, false);
                                    DualSurfaceCurve dsc1 = new DualSurfaceCurve(elli1, this, c2d1, pl, c2dpl1);
                                    //zeite Kreis
                                    majax = new GeoVector(rmin, 0, 0);
                                    minax = new GeoVector(0, rmin, 0);
                                    majaxis = toTorus * majax;
                                    minaxis = toTorus * minax;
                                    //im Weltsystem
                                    Ellipse elli2 = Ellipse.Construct();
                                    elli2.SetEllipseCenterAxis(center, majaxis, minaxis);
                                    elli2.StartParameter = 0.0; // wird mit obigem nicht gesetzt
                                    elli2.SweepParameter = Math.PI * 2.0;
                                    //GeoPoint2D centerOnPl = pl.PositionOf(center);
                                    //Auf der Ebene
                                    p1OnPl = pl.PositionOf(center + majaxis);
                                    p2OnPl = pl.PositionOf(center - minaxis);
                                    elli2d = Geometry.Ellipse2P2T(p1OnPl, p2OnPl, p2OnPl - centerOnPl, p1OnPl - centerOnPl);
                                    ICurve2D c2dpl2 = elli2d.Trim(0.0, 1.0);
                                    //Im (u,v) System
                                    f = Math.PI - amax;
                                    if (d < 0)
                                        f = Math.PI + amax;
                                    for (int i = 0; i < pnts.Length; i++)
                                    {
                                        pnts[i].x = i * 2 * Math.PI / (pnts.Length - 1);
                                        pnts[i].y = f;
                                    }
                                    BSpline2D c2d2 = new BSpline2D(pnts, 2, false);
                                    DualSurfaceCurve dsc2 = new DualSurfaceCurve(elli2, this, c2d2, pl, c2dpl2);
                                    return new IDualSurfaceCurve[] { dsc1, dsc2 };
                                    // return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
                                }
                                #endregion
                */
            }
            #endregion


            // System.Diagnostics.Trace.WriteLine("Kurve ...");
            Angle eta = new Angle(pln.Normal.x, pln.Normal.y);
            ModOp m = ModOp.Rotate(GeoVector.ZAxis, -eta);
            GeoVector normal = m * pln.Normal;
            ModOp m1 = m.GetInverse();
            GeoPoint p2z = pln.Intersect(GeoPoint.Origin, GeoVector.ZAxis);
            GeoVector2D dirp = new GeoVector2D(normal.z, -normal.x);
            GeoPoint2D[] tp1, tp2;
            GeoPoint2D cent1 = new GeoPoint2D(-1, 0);
            GeoPoint2D cent2 = new GeoPoint2D(1, 0);

            tp1 = Geometry.IntersectLC(new GeoPoint2D(0, p2z.z), dirp, cent1, minorRadius);
            tp2 = Geometry.IntersectLC(new GeoPoint2D(0, p2z.z), dirp, cent2, minorRadius);
            GeoPoint2D np1, np2, np3, np4;

            if (tp1.Length + tp2.Length <= 1)
            #region Ebene Tagente zu einen kleinen Kreis des Torus
            {
                Geometry.IntersectLL(new GeoPoint2D(0, p2z.z), dirp, new GeoPoint2D(0, minorRadius), GeoVector2D.XAxis, out np1);
                Geometry.IntersectLL(new GeoPoint2D(0, p2z.z), dirp, new GeoPoint2D(0, -minorRadius), GeoVector2D.XAxis, out np2);
                if (Math.Abs(np1.x) > 1.0 || Math.Abs(np2.x) > 1.0)
                {
                    //Die Ebene ist von rausen Tangent zu einen kleinen Kreis des Torus 
                    return new IDualSurfaceCurve[0];
                }
                //Die Ebene ist von innen Tangent nur zu einen kleinen Kreis des Torus
                bool close = false;
                GeoPoint2D p0 = new GeoPoint2D();
                if (tp1.Length == 0 && tp2.Length == 0)
                {
                    //Geometry.IntersectLL(new GeoPoint2D(0, p2z.z), dirp, GeoPoint2D.Origin, GeoVector2D.XAxis, out p0);
                    p0 = np2;
                    close = true;
                }
                else if (tp1.Length == 0)
                {
                    p0 = tp2[0];
                    cent1 = cent2;
                }
                else
                {
                    p0 = tp1[0];
                }

                if (p0.y > 0 && close == false)
                {
                    GeoPoint2D temp = np1;
                    np1 = np2;
                    np2 = temp;
                }

                int ntt = 10;
                List<GeoPoint> mlip = new List<GeoPoint>(ntt);
                List<GeoPoint> mlin = new List<GeoPoint>(ntt);
                List<GeoPoint> mlep = new List<GeoPoint>(ntt);
                List<GeoPoint> mlen = new List<GeoPoint>(ntt);
                for (int i = 0; i < ntt; i++)
                {
                    double re, ri;
                    GeoPoint2D tmp = new GeoPoint2D(p0.x + i * (np1.x - p0.x) / (ntt - 1), p0.y + i * (np1.y - p0.y) / (ntt - 1));
                    tp1 = Geometry.IntersectLC(tmp, GeoVector2D.XAxis, cent1, minorRadius);
                    if (tp1.Length != 0)
                    {
                        if (tp1.Length == 1)
                        {
                            ri = Math.Abs(tp1[0].x);
                            re = ri;
                        }
                        else if (Math.Abs(tp1[0].x) > Math.Abs(tp1[1].x))
                        {
                            re = Math.Abs(tp1[0].x);
                            ri = Math.Abs(tp1[1].x);
                        }
                        else
                        {
                            re = Math.Abs(tp1[1].x);
                            ri = Math.Abs(tp1[0].x);
                        }
                        if (i != ntt - 1)
                        {
                            tp2 = Geometry.IntersectLC(new GeoPoint2D(tmp.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, re);
                            if (tp2[0].y < 0)
                            {
                                mlep.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                                mlen.Add(m1 * new GeoPoint(tmp.x, tp2[1].y, tmp.y));
                            }
                            else
                            {
                                mlep.Add(m1 * new GeoPoint(tmp.x, tp2[1].y, tmp.y));
                                mlen.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                            }
                        }
                        tp2 = Geometry.IntersectLC(new GeoPoint2D(tmp.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, ri);
                        if (tp2.Length == 1)
                        {
                            mlip.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                            mlin.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                        }
                        else if (tp2[0].y < 0)
                        {
                            mlip.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                            mlin.Add(m1 * new GeoPoint(tmp.x, tp2[1].y, tmp.y));
                        }
                        else
                        {
                            mlip.Add(m1 * new GeoPoint(tmp.x, tp2[1].y, tmp.y));
                            mlin.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                        }
                    }
                }
                mlep.Reverse();
                mlen.Reverse();
                mlip.AddRange(mlep);
                mlin.AddRange(mlen);
                mlep.Clear();
                mlen.Clear();

                ////
                if (!close)
                {
                    double dmax = Geometry.Dist(p0, np2);
                    mlep.Add(m1 * new GeoPoint(p0.x, 0, p0.y));
                    mlen.Add(m1 * new GeoPoint(p0.x, 0, p0.y));

                    int im = 1;
                    while (true)
                    {
                        bool ok = false;
                        double re, ri;
                        GeoPoint2D tmp = new GeoPoint2D(p0.x + im * (np2.x - p0.x) / (ntt - 1), p0.y + im * (np2.y - p0.y) / (ntt - 1));
                        double dmin = Geometry.Dist(p0, tmp);
                        if (dmin >= dmax)
                        {
                            tmp = np2;
                            ok = true;
                        }
                        tp1 = Geometry.IntersectLC(tmp, GeoVector2D.XAxis, cent1, minorRadius);
                        if (tp1.Length != 0)
                        {
                            if (tp1.Length == 1)
                            {
                                ri = Math.Abs(tp1[0].x);
                                re = ri;
                            }
                            else if (Math.Abs(tp1[0].x) > Math.Abs(tp1[1].x))
                            {
                                re = Math.Abs(tp1[0].x);
                                ri = Math.Abs(tp1[1].x);
                            }
                            else
                            {
                                re = Math.Abs(tp1[1].x);
                                ri = Math.Abs(tp1[0].x);
                            }
                            tp2 = Geometry.IntersectLC(new GeoPoint2D(tmp.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, re);
                            if (tp2[0].y < 0)
                            {
                                mlip.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                                mlin.Add(m1 * new GeoPoint(tmp.x, tp2[1].y, tmp.y));
                            }
                            else
                            {
                                mlip.Add(m1 * new GeoPoint(tmp.x, tp2[1].y, tmp.y));
                                mlin.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                            }
                            if (ok != true)
                            {
                                tp2 = Geometry.IntersectLC(new GeoPoint2D(tmp.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, ri);
                                if (tp2.Length == 1)
                                {
                                    mlep.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                                    mlen.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                                }
                                else if (tp2[0].y < 0)
                                {
                                    mlep.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                                    mlen.Add(m1 * new GeoPoint(tmp.x, tp2[1].y, tmp.y));
                                }
                                else
                                {
                                    mlep.Add(m1 * new GeoPoint(tmp.x, tp2[1].y, tmp.y));
                                    mlen.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                                }
                            }
                        }
                        if (ok)
                            break;
                        im++;
                    }
                    mlep.Reverse();
                    mlen.Reverse();
                    mlip.AddRange(mlep);
                    mlin.AddRange(mlen);
                }

                //////
                degree = 3;
                //Kurve 1 im Welt System
                GeoPoint[] pnts1 = mlip.ToArray();
                GeoPoint[] psw1 = new GeoPoint[pnts1.Length];
                for (int i = 0; i < pnts1.Length; i++)
                {
                    psw1[i] = toTorus * pnts1[i];
                }
                BSpline f3c3d1 = this.Refine(psw1, degree, close, pl, precision);
                ////Kurve 1 im (u,v) System
                pnts1 = new GeoPoint[f3c3d1.ThroughPointCount];
                for (int i = 0; i < f3c3d1.ThroughPointCount; i++)
                    pnts1[i] = toUnit * f3c3d1.GetThroughPoint(i);

                GeoPoint2D[] psuv1 = new GeoPoint2D[pnts1.Length];
                for (int i = 0; i < pnts1.Length; i++)
                {
                    double f = Math.Sqrt(pnts1[i].x * pnts1[i].x + pnts1[i].y * pnts1[i].y) - 1.0;
                    psuv1[i].x = Math.Atan2(pnts1[i].y, pnts1[i].x);
                    psuv1[i].y = Math.Atan2(pnts1[i].z, f);
                }
                BSpline2D.AdjustPeriodic(psuv1, 2 * Math.PI, 2 * Math.PI);
                BSpline2D f3c2d1 = new BSpline2D(psuv1, degree, close);
                //Kurve 1 auf der Ebene pl
                ICurve2D f3c2dpl1 = (f3c3d1 as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));
                //Kurve 2 im Welt System
                GeoPoint[] pnts2 = mlin.ToArray();
                psw1 = new GeoPoint[pnts2.Length];
                for (int i = 0; i < pnts2.Length; i++)
                {
                    psw1[i] = toTorus * pnts2[i];
                }
                BSpline f3c3d2 = this.Refine(psw1, degree, close, pl, precision);
                ////Kurve 2 im (u,v) System
                pnts1 = new GeoPoint[f3c3d2.ThroughPointCount];
                for (int i = 0; i < f3c3d2.ThroughPointCount; i++)
                    pnts1[i] = toUnit * f3c3d2.GetThroughPoint(i);

                psuv1 = new GeoPoint2D[pnts1.Length];
                for (int i = 0; i < pnts1.Length; i++)
                {
                    double f = Math.Sqrt(pnts1[i].x * pnts1[i].x + pnts1[i].y * pnts1[i].y) - 1.0;
                    psuv1[i].x = Math.Atan2(pnts1[i].y, pnts1[i].x);
                    psuv1[i].y = Math.Atan2(pnts1[i].z, f);
                }
                BSpline2D.AdjustPeriodic(psuv1, 2 * Math.PI, 2 * Math.PI);
                BSpline2D f3c2d2 = new BSpline2D(psuv1, degree, close);
                //Kurve 2 auf der Ebene pl
                ICurve2D f3c2dpl2 = (f3c3d2 as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

                DualSurfaceCurve f3dsc1 = new DualSurfaceCurve(f3c3d1, this, f3c2d1, pl, f3c2dpl1);
                DualSurfaceCurve f3dsc2 = new DualSurfaceCurve(f3c3d2, this, f3c2d2, pl, f3c2dpl2);

                return new IDualSurfaceCurve[] { f3dsc1, f3dsc2 };
            }
            #endregion
            if (tp1.Length == 2 && tp2.Length == 0 || tp1.Length == 0 && tp2.Length == 2)
            #region Eine Kurve
            {
                if (tp1.Length == 0)
                {
                    cent1 = cent2;
                    if (tp2[0].x < tp2[1].x)
                    {
                        np1 = tp2[0];
                        np2 = tp2[1];
                    }
                    else
                    {
                        np1 = tp2[1];
                        np2 = tp2[0];
                    }
                }
                else
                {
                    if (tp1[0].x > tp1[1].x)
                    {
                        np1 = tp1[0];
                        np2 = tp1[1];
                    }
                    else
                    {
                        np1 = tp1[1];
                        np2 = tp1[0];
                    }
                }
                dirp.x = np2.x - np1.x;
                dirp.y = np2.y - np1.y;
                dirp.Norm();

                GeoPoint2D G1, G2;
                if (np1.y > 0)
                {
                    Geometry.IntersectLL(new GeoPoint2D(cent1.x, minorRadius), GeoVector2D.XAxis, new GeoPoint2D(0, p2z.z), dirp, out G1);
                    Geometry.IntersectLL(new GeoPoint2D(cent1.x, -minorRadius), GeoVector2D.XAxis, new GeoPoint2D(0, p2z.z), dirp, out G2);
                    if (cent1.x > 0)
                    {
                        if (np1.x <= cent1.x && np2.x <= cent1.x && np2.y > 0)
                        {
                            GeoPoint2D xtmp = G1;
                            G1 = G2;
                            G2 = xtmp;
                        }
                    }
                    else
                    {
                        if (np1.x >= cent1.x && np2.x >= cent1.x && np2.y > 0)
                        {
                            GeoPoint2D xtmp = G1;
                            G1 = G2;
                            G2 = xtmp;
                        }
                    }
                }
                else
                {
                    Geometry.IntersectLL(new GeoPoint2D(cent1.x, -minorRadius), GeoVector2D.XAxis, new GeoPoint2D(0, p2z.z), dirp, out G1);
                    Geometry.IntersectLL(new GeoPoint2D(cent1.x, minorRadius), GeoVector2D.XAxis, new GeoPoint2D(0, p2z.z), dirp, out G2);
                    if (cent1.x > 0)
                    {
                        if (np1.x <= cent1.x && np2.x <= cent1.x && np2.y < 0)
                        {
                            GeoPoint2D xtmp = G1;
                            G1 = G2;
                            G2 = xtmp;
                        }
                    }
                    else
                    {
                        if (np1.x >= cent1.x && np2.x >= cent1.x && np2.y < 0)
                        {
                            GeoPoint2D xtmp = G1;
                            G1 = G2;
                            G2 = xtmp;
                        }
                    }
                }
                int ni = 20;
                double df = Geometry.Dist(np1, np2) / (ni - 1);
                int ne1 = 0;
                int ne2 = 0;
                int nt = 2 * ni + 3;
                if (cent1.x > 0)
                {
                    if (np1.x < cent1.x)
                    {
                        ne1 = (int)(Geometry.Dist(np1, G1) / df) + 1;
                        nt = 4 * (ne1 - 1) + 2 + 2 * (ni - 1) + 2;
                    }
                    if (np2.x < cent1.x)
                    {
                        ne2 = (int)(Geometry.Dist(np2, G2) / df) + 1;
                        nt = 4 * (ne2 - 1) + nt;
                    }
                }
                else
                {
                    if (np1.x > cent1.x)
                    {
                        ne1 = (int)(Geometry.Dist(np1, G1) / df) + 1;
                        nt = 4 * (ne1 - 1) + 2 + 2 * (ni - 1) + 2;
                    }
                    if (np2.x > cent1.x)
                    {
                        ne2 = (int)(Geometry.Dist(np2, G2) / df) + 1;
                        nt = 4 * (ne2 - 1) + nt;
                    }
                }
                List<GeoPoint> alp1 = new List<GeoPoint>(nt);
                List<GeoPoint> alp2 = new List<GeoPoint>(nt);
                List<GeoPoint> aln1 = new List<GeoPoint>(nt);
                List<GeoPoint> aln2 = new List<GeoPoint>(nt);
                alp1.Add(m1 * new GeoPoint(np1.x, 0, np1.y));
                aln1.Add(m1 * new GeoPoint(np1.x, 0, np1.y));
                if (ne1 != 0)
                {
                    GeoPoint2D[] tp;
                    for (int i = 1; i < ne1; i++)
                    {
                        GeoPoint2D pd = np1 - (i * df) * dirp;
                        double r1, r2;
                        tp = Geometry.IntersectLC(pd, GeoVector2D.XAxis, cent1, minorRadius);
                        if (tp.Length > 1)
                        {
                            if (Math.Abs(tp[0].x) < Math.Abs(tp[1].x))
                            {
                                r1 = Math.Abs(tp[0].x);
                                r2 = Math.Abs(tp[1].x);
                            }
                            else
                            {
                                r1 = Math.Abs(tp[1].x);
                                r2 = Math.Abs(tp[0].x);
                            }
                            tp = Geometry.IntersectLC(new GeoPoint2D(pd.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, r1);
                            alp1.Add(m1 * new GeoPoint(pd.x, Math.Abs(tp[0].y), pd.y));
                            aln1.Add(m1 * new GeoPoint(pd.x, -Math.Abs(tp[0].y), pd.y));
                            tp = Geometry.IntersectLC(new GeoPoint2D(pd.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, r2);
                            alp2.Add(m1 * new GeoPoint(pd.x, Math.Abs(tp[0].y), pd.y));
                            aln2.Add(m1 * new GeoPoint(pd.x, -Math.Abs(tp[0].y), pd.y));
                        }
                    }
                    tp = Geometry.IntersectLC(new GeoPoint2D(G1.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, 1);
                    if (tp.Length > 0)
                    {
                        alp1.Add(m1 * new GeoPoint(G1.x, Math.Abs(tp[0].y), G1.y));
                        aln1.Add(m1 * new GeoPoint(G1.x, -Math.Abs(tp[0].y), G1.y));
                    }
                    alp2.Reverse();
                    aln2.Reverse();
                    alp1.AddRange(alp2);
                    aln1.AddRange(aln2);
                    alp2.Clear();
                    aln2.Clear();
                }
                for (int i = 0; i < ni - 1; i++)
                {
                    GeoPoint2D[] tp;
                    GeoPoint2D pd = np1 + (i * df) * dirp;
                    double r1;
                    tp = Geometry.IntersectLC(pd, GeoVector2D.XAxis, cent1, minorRadius);
                    if (tp.Length == 1)
                        r1 = Math.Abs(tp[0].x);
                    else if (Math.Abs(tp[0].x) > Math.Abs(cent1.x))
                    {
                        r1 = Math.Abs(tp[0].x);
                    }
                    else
                    {
                        r1 = Math.Abs(tp[1].x);
                    }
                    tp = Geometry.IntersectLC(new GeoPoint2D(pd.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, r1);
                    alp1.Add(m1 * new GeoPoint(pd.x, Math.Abs(tp[0].y), pd.y));
                    aln1.Add(m1 * new GeoPoint(pd.x, -Math.Abs(tp[0].y), pd.y));
                }
                bool lestepunkt = true;
                do
                {
                    double r2 = 0;
                    GeoPoint2D pd = np2;
                    GeoPoint2D[] tp = Geometry.IntersectLC(np2, GeoVector2D.XAxis, cent1, minorRadius);
                    if (Math.Abs(tp[0].x) < Math.Abs(tp[1].x))
                    {
                        r2 = Math.Abs(tp[1].x);
                    }
                    else
                    {
                        r2 = Math.Abs(tp[0].x);
                    }
                    alp2.Add(m1 * new GeoPoint(pd.x, 0, pd.y));
                    aln2.Add(m1 * new GeoPoint(pd.x, 0, pd.y));

                    tp = Geometry.IntersectLC(new GeoPoint2D(pd.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, r2);
                    alp1.Add(m1 * new GeoPoint(pd.x, Math.Abs(tp[0].y), pd.y));
                    aln1.Add(m1 * new GeoPoint(pd.x, -Math.Abs(tp[0].y), pd.y));
                    lestepunkt = false;
                } while (lestepunkt);

                if (ne2 != 0)
                {
                    GeoPoint2D[] tp;
                    for (int i = 1; i < ne2; i++)
                    {
                        GeoPoint2D pd = np2 + (i * df) * dirp;
                        double r1, r2;
                        tp = Geometry.IntersectLC(pd, GeoVector2D.XAxis, cent1, minorRadius);
                        if (Math.Abs(tp[0].x) < Math.Abs(tp[1].x))
                        {
                            r1 = Math.Abs(tp[0].x);
                            r2 = Math.Abs(tp[1].x);
                        }
                        else
                        {
                            r1 = Math.Abs(tp[1].x);
                            r2 = Math.Abs(tp[0].x);
                        }
                        tp = Geometry.IntersectLC(new GeoPoint2D(pd.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, r1);
                        //if (tp.Length != 0)
                        //{
                        alp2.Add(m1 * new GeoPoint(pd.x, Math.Abs(tp[0].y), pd.y));
                        aln2.Add(m1 * new GeoPoint(pd.x, -Math.Abs(tp[0].y), pd.y));
                        //}
                        tp = Geometry.IntersectLC(new GeoPoint2D(pd.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, r2);
                        //if (tp.Length != 0)
                        //{
                        alp1.Add(m1 * new GeoPoint(pd.x, Math.Abs(tp[0].y), pd.y));
                        aln1.Add(m1 * new GeoPoint(pd.x, -Math.Abs(tp[0].y), pd.y));
                        //}
                    }
                    tp = Geometry.IntersectLC(new GeoPoint2D(G2.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, 1);
                    alp1.Add(m1 * new GeoPoint(G2.x, Math.Abs(tp[0].y), G2.y));
                    aln1.Add(m1 * new GeoPoint(G2.x, -Math.Abs(tp[0].y), G2.y));
                    alp2.Reverse();
                    aln2.Reverse();
                    alp1.AddRange(alp2);
                    aln1.AddRange(aln2);
                    alp2.Clear();
                    aln2.Clear();
                }
                ////
                degree = 3;
                ////Kurve 1 im Welt System
                GeoPoint[] ps = alp1.ToArray();
                GeoPoint[] psw = new GeoPoint[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    psw[i] = toTorus * ps[i];
                }
                BSpline kwlt1 = this.Refine(psw, degree, false, pl, precision);
                ////Kurve 1 im (u,v) System
                ps = new GeoPoint[kwlt1.ThroughPointCount];
                for (int i = 0; i < kwlt1.ThroughPointCount; i++)
                    ps[i] = toUnit * kwlt1.GetThroughPoint(i);

                GeoPoint2D[] psuv = new GeoPoint2D[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    double f = Math.Sqrt(ps[i].x * ps[i].x + ps[i].y * ps[i].y) - 1.0;
                    psuv[i].x = Math.Atan2(ps[i].y, ps[i].x);
                    psuv[i].y = Math.Atan2(ps[i].z, f);
                }
                BSpline2D.AdjustPeriodic(psuv, 2 * Math.PI, 2 * Math.PI);
                BSpline2D kuv1 = new BSpline2D(psuv, degree, false);
                ////Kurve 1 auf der Ebene pl
                ICurve2D kpl1 = (kwlt1 as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

                ////Kurve 2 im Welt System
                ps = aln1.ToArray();
                psw = new GeoPoint[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    psw[i] = toTorus * ps[i];
                }
                BSpline kwlt2 = this.Refine(psw, degree, false, pl, precision);
                ////Kurve 2 im (u,v) System
                ps = new GeoPoint[kwlt2.ThroughPointCount];
                for (int i = 0; i < kwlt2.ThroughPointCount; i++)
                    ps[i] = toUnit * kwlt2.GetThroughPoint(i);

                psuv = new GeoPoint2D[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    double f = Math.Sqrt(ps[i].x * ps[i].x + ps[i].y * ps[i].y) - 1.0;
                    psuv[i].x = Math.Atan2(ps[i].y, ps[i].x);
                    psuv[i].y = Math.Atan2(ps[i].z, f);
                }
                BSpline2D.AdjustPeriodic(psuv, 2 * Math.PI, 2 * Math.PI);
                BSpline2D kuv2 = new BSpline2D(psuv, degree, false);
                ////Kurve 2 auf der Ebene pl
                ICurve2D kpl2 = (kwlt2 as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

                DualSurfaceCurve kdsc1 = new DualSurfaceCurve(kwlt1, this, kuv1, pl, kpl1);
                DualSurfaceCurve kdsc2 = new DualSurfaceCurve(kwlt2, this, kuv2, pl, kpl2);

                return new IDualSurfaceCurve[] { kdsc1, kdsc2 };
            }
            #endregion
            /////
            #region Der Rest
            if (tp1.Length + tp2.Length > 2)
            {
                if (tp1.Length == 1)
                {
                    np1 = np3 = tp1[0];
                    if (Math.Abs(tp2[0].x) > Math.Abs(tp2[1].x))
                    {
                        np2 = tp2[1];
                        np4 = tp2[0];
                    }
                    else
                    {
                        np2 = tp2[0];
                        np4 = tp2[1];
                    }
                }
                else
                {
                    if (tp2.Length == 1)
                    {
                        np1 = np3 = tp2[0];
                        if (Math.Abs(tp1[0].x) > Math.Abs(tp1[1].x))
                        {
                            np2 = tp1[1];
                            np4 = tp1[0];
                        }
                        else
                        {
                            np2 = tp1[0];
                            np4 = tp1[1];
                        }
                    }
                    else
                    {
                        if (Math.Abs(tp1[0].x) > Math.Abs(tp1[1].x))
                        {
                            np1 = tp1[1];
                            np3 = tp1[0];
                        }
                        else
                        {
                            np1 = tp1[0];
                            np3 = tp1[1];
                        }
                        if (Math.Abs(tp2[0].x) > Math.Abs(tp2[1].x))
                        {
                            np2 = tp2[1];
                            np4 = tp2[0];
                        }
                        else
                        {
                            np2 = tp2[0];
                            np4 = tp2[1];
                        }

                    }
                }
            }
            else
            {
                np1 = np3 = tp1[0];
                np2 = np4 = tp2[0];
            }
            dirp.x = np2.x - np1.x;
            dirp.y = np2.y - np1.y;
            int ntp = 30;

            List<GeoPoint> lin = new List<GeoPoint>(ntp);
            List<GeoPoint> lip = new List<GeoPoint>(ntp);
            List<GeoPoint> len = new List<GeoPoint>(ntp);
            List<GeoPoint> lep = new List<GeoPoint>(ntp);
            lin.Add(m1 * new GeoPoint(np1.x, 0, np1.y));
            lip.Add(m1 * new GeoPoint(np1.x, 0, np1.y));
            for (int i = 1; i < ntp - 1; i++)
            {
                GeoPoint2D tmp = new GeoPoint2D(np1.x + i * (np2.x - np1.x) / (ntp - 1), np1.y + i * (np2.y - np1.y) / (ntp - 1));
                double ri;
                tp1 = Geometry.IntersectLC(tmp, GeoVector2D.XAxis, cent1, minorRadius);
                if (tp1.Length == 1)
                    ri = Math.Abs(tmp.x);
                else if (Math.Abs(tp1[0].x) < Math.Abs(tp1[1].x))
                    ri = Math.Abs(tp1[0].x);
                else
                    ri = Math.Abs(tp1[1].x);

                tp2 = Geometry.IntersectLC(new GeoPoint2D(tmp.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, ri);
                if (tp2[0].y < 0)
                {
                    lin.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                    lip.Add(m1 * new GeoPoint(tmp.x, -tp2[0].y, tmp.y));
                }
                else
                {
                    lin.Add(m1 * new GeoPoint(tmp.x, -tp2[0].y, tmp.y));
                    lip.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                }
            }
            lin.Add(m1 * new GeoPoint(np2.x, 0, np2.y));
            lip.Add(m1 * new GeoPoint(np2.x, 0, np2.y));

            dirp.x = np4.x - np3.x;
            dirp.y = np4.y - np3.y;
            len.Add(m1 * new GeoPoint(np3.x, 0, np3.y));
            lep.Add(m1 * new GeoPoint(np3.x, 0, np3.y));
            for (int i = 1; i < ntp - 1; i++)
            {
                GeoPoint2D tmp = new GeoPoint2D(np3.x + i * (np4.x - np3.x) / (ntp - 1), np3.y + i * (np4.y - np3.y) / (ntp - 1));
                double re;
                tp1 = Geometry.IntersectLC(tmp, GeoVector2D.XAxis, cent1, minorRadius);
                if (tp1.Length == 1)
                    re = Math.Abs(tmp.x);
                else if (Math.Abs(tp1[0].x) > Math.Abs(tp1[1].x))
                    re = Math.Abs(tp1[0].x);
                else
                    re = Math.Abs(tp1[1].x);

                tp2 = Geometry.IntersectLC(new GeoPoint2D(tmp.x, 0), GeoVector2D.YAxis, GeoPoint2D.Origin, re);
                if (tp2[0].y < 0)
                {
                    len.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                    lep.Add(m1 * new GeoPoint(tmp.x, -tp2[0].y, tmp.y));
                }
                else
                {
                    len.Add(m1 * new GeoPoint(tmp.x, -tp2[0].y, tmp.y));
                    lep.Add(m1 * new GeoPoint(tmp.x, tp2[0].y, tmp.y));
                }
            }
            len.Add(m1 * new GeoPoint(np4.x, 0, np4.y));
            lep.Add(m1 * new GeoPoint(np4.x, 0, np4.y));

            ////////
            degree = 3;
            ////Kurve 1 im Welt System
            GeoPoint[] pin = lin.ToArray();
            GeoPoint[] psw2 = new GeoPoint[pin.Length];
            for (int i = 0; i < pin.Length; i++)
            {
                psw2[i] = toTorus * pin[i];
            }
            BSpline fwlt1 = this.Refine(psw2, degree, false, pl, precision);
            ////Kurve 1 im (u,v) System
            pin = new GeoPoint[fwlt1.ThroughPointCount];
            for (int i = 0; i < fwlt1.ThroughPointCount; i++)
                pin[i] = toUnit * fwlt1.GetThroughPoint(i);

            GeoPoint2D[] puv1 = new GeoPoint2D[pin.Length];
            for (int i = 0; i < pin.Length; i++)
            {
                double f = Math.Sqrt(pin[i].x * pin[i].x + pin[i].y * pin[i].y) - 1.0;
                puv1[i].x = Math.Atan2(pin[i].y, pin[i].x);
                puv1[i].y = Math.Atan2(pin[i].z, f);
            }
            BSpline2D.AdjustPeriodic(puv1, 2 * Math.PI, 2 * Math.PI);
            BSpline2D fuv1 = new BSpline2D(puv1, degree, false);
            ////Kurve 1 auf der Ebene pl
            ICurve2D fpl1 = (fwlt1 as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

            ////Kurve 2 im Welt System
            pin = lip.ToArray();
            psw2 = new GeoPoint[pin.Length];
            for (int i = 0; i < pin.Length; i++)
            {
                psw2[i] = toTorus * pin[i];
            }
            BSpline fwlt2 = this.Refine(psw2, degree, false, pl, precision);
            ////Kurve 2 im (u,v) System
            pin = new GeoPoint[fwlt2.ThroughPointCount];
            for (int i = 0; i < fwlt2.ThroughPointCount; i++)
                pin[i] = toUnit * fwlt2.GetThroughPoint(i);

            puv1 = new GeoPoint2D[pin.Length];
            for (int i = 0; i < pin.Length; i++)
            {
                double f = Math.Sqrt(pin[i].x * pin[i].x + pin[i].y * pin[i].y) - 1.0;
                puv1[i].x = Math.Atan2(pin[i].y, pin[i].x);
                puv1[i].y = Math.Atan2(pin[i].z, f);
            }
            BSpline2D.AdjustPeriodic(puv1, 2 * Math.PI, 2 * Math.PI);
            BSpline2D fuv2 = new BSpline2D(puv1, degree, false);
            ////Kurve 2 auf der Ebene pl
            ICurve2D fpl2 = (fwlt2 as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

            ////Kurve 3 im Welt System
            pin = len.ToArray();
            psw2 = new GeoPoint[pin.Length];
            for (int i = 0; i < pin.Length; i++)
            {
                psw2[i] = toTorus * pin[i];
            }
            BSpline fwlt3 = this.Refine(psw2, degree, false, pl, precision);
            ////Kurve 3 im (u,v) System
            pin = new GeoPoint[fwlt3.ThroughPointCount];
            for (int i = 0; i < fwlt3.ThroughPointCount; i++)
                pin[i] = toUnit * fwlt3.GetThroughPoint(i);

            puv1 = new GeoPoint2D[pin.Length];
            for (int i = 0; i < pin.Length; i++)
            {
                double f = Math.Sqrt(pin[i].x * pin[i].x + pin[i].y * pin[i].y) - 1.0;
                puv1[i].x = Math.Atan2(pin[i].y, pin[i].x);
                puv1[i].y = Math.Atan2(pin[i].z, f);
            }
            BSpline2D.AdjustPeriodic(puv1, 2 * Math.PI, 2 * Math.PI);
            BSpline2D fuv3 = new BSpline2D(puv1, degree, false);
            ////Kurve 3 auf der Ebene pl
            ICurve2D fpl3 = (fwlt3 as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));

            ////Kurve 4 im Welt System
            pin = lep.ToArray();
            psw2 = new GeoPoint[pin.Length];
            for (int i = 0; i < pin.Length; i++)
            {
                psw2[i] = toTorus * pin[i];
            }
            BSpline fwlt4 = this.Refine(psw2, degree, false, pl, precision);
            ////Kurve 4 im (u,v) System
            pin = new GeoPoint[fwlt4.ThroughPointCount];
            for (int i = 0; i < fwlt4.ThroughPointCount; i++)
                pin[i] = toUnit * fwlt4.GetThroughPoint(i);

            puv1 = new GeoPoint2D[pin.Length];
            for (int i = 0; i < pin.Length; i++)
            {
                double f = Math.Sqrt(pin[i].x * pin[i].x + pin[i].y * pin[i].y) - 1.0;
                puv1[i].x = Math.Atan2(pin[i].y, pin[i].x);
                puv1[i].y = Math.Atan2(pin[i].z, f);
            }
            BSpline2D.AdjustPeriodic(puv1, 2 * Math.PI, 2 * Math.PI);
            BSpline2D fuv4 = new BSpline2D(puv1, degree, false);
            ////Kurve 4 auf der Ebene pl
            ICurve2D fpl4 = (fwlt4 as ICurve).GetProjectedCurve(new Plane(pl.Location, pl.DirectionX, pl.DirectionY));
            ////////
            DualSurfaceCurve fdsc1 = new DualSurfaceCurve(fwlt1, this, fuv1, pl, fpl1);
            DualSurfaceCurve fdsc2 = new DualSurfaceCurve(fwlt2, this, fuv2, pl, fpl2);
            DualSurfaceCurve fdsc3 = new DualSurfaceCurve(fwlt3, this, fuv3, pl, fpl3);
            DualSurfaceCurve fdsc4 = new DualSurfaceCurve(fwlt4, this, fuv4, pl, fpl4);

            return new IDualSurfaceCurve[] { fdsc1, fdsc2, fdsc3, fdsc4 };
            //return base.GetPlaneIntersection(pl, umin, umax, vmin, vmax);
            #endregion
        }

        public override Polynom GetImplicitPolynomial()
        {
            if (imlicitPolynomial == null)
            {
                // ∆: point - location, r: major radius, s: minor radius, from: https://www.geometrictools.com/Documentation/DistanceToCircle3.pdf
                // s² = (N · ∆)² + (sqrt(|∆|² − (N · ∆)²) − r)² // line 3 of part (3), where |P - K|² == s²
                // s² = (N · ∆)² + (|∆|² − (N · ∆)²) + r² -2*r*sqrt(|∆|² − (N · ∆)²)
                // (s² - (N · ∆)² - (|∆|² − (N · ∆)²) - r²)²/(-2*r)² = |∆|² − (N · ∆)²
                // (s² - (N · ∆)² - (|∆|² − (N · ∆)²) - r²)²/(-2*r)² - |∆|² + (N · ∆)² = 0
                // (s²  - |∆|²  - r²)²/(-2*r)² - |∆|² + (N · ∆)² = 0

                PolynomVector d = PolynomVector.xyz - new PolynomVector(Location - GeoPoint.Origin); // ∆ = point-location, point is the variable for the polynom in x, y and z
                PolynomVector n = new PolynomVector(ZAxis.Normalized);
                double r = XAxis.Length;
                double s = MinorRadius;
                imlicitPolynomial = ((s * s - d * d - r * r) ^ 2) / (4 * r * r) - d * d + (n * d) * (n * d); // this is a polynomial in x, y and z of degree 4
                GeoPoint dist1 = PointAt(GeoPoint2D.Origin) + GetNormal(GeoPoint2D.Origin).Normalized; // point outside the torus with distance 1
                double d1 = imlicitPolynomial.Eval(dist1); // this should yield 1, if the scaling of the polynomial would be correct
                imlicitPolynomial = imlicitPolynomial / d1; // correct the scaling, unfortunately the evaluation of the polynomial doesn't yield the correct distance
                // but it seems with this scaling it yields the correct distance or less
                //dist1 = PointAt(new GeoPoint2D(0,Math.PI/2)) + GetNormal(new GeoPoint2D(0, Math.PI / 2)).Normalized; // point outside the torus with distance 1
                //d1 = imlicitPolynomial.Eval(dist1); // this should yield 1, if the scaling of the polynomial would be correct
            }
            return imlicitPolynomial;
        }
        public Ellipse GetAxisEllipse()
        {
            Ellipse e = Ellipse.Construct();
            Plane pln = Plane.XYPlane;
            e.SetCirclePlaneCenterRadius(pln, GeoPoint.Origin, 1.0);
            e.StartParameter = 0.0;
            e.SweepParameter = 2.0 * Math.PI;
            e.Modify(toTorus);
            return e;
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
            GeoPoint tangentialFound = GeoPoint.Invalid;
            if (curve is Line line)
            {
                GeoPoint[] tangentialIntersections = Geometry.CircleLineDist(GetAxisEllipse(), line.StartPoint, line.StartDirection);
                for (int i = 0; i < tangentialIntersections.Length; i += 2)
                {
                    double d = tangentialIntersections[i] | tangentialIntersections[i + 1];
                    if (Math.Abs(d - this.MinorRadius) < Precision.eps)
                    {   // there is a tangential intersection between this line and the torus
                        // the pair of point is alway first point on the circle, second on the line
                        double pos = line.PositionOf(tangentialIntersections[i + 1]);
                        if (pos > -Precision.eps && pos < 1 + Precision.eps)
                        {
                            tangentialFound = tangentialIntersections[i + 1];
                        }
                    }
                }
            }
            if (!(curve is Ellipse) && curve is IExplicitPCurve3D iexpc)
            {

                ImplicitPSurface psurf = (this as IImplicitPSurface).GetImplicitPSurface();
                GeoPoint[] apnts = psurf.Intersect(iexpc.GetExplicitPCurve3D(), out double[] u);
                List<double> luOnCurve3Ds = new List<double>();
                List<GeoPoint2D> luvOnFaces = new List<GeoPoint2D>();
                List<GeoPoint> lips = new List<GeoPoint>();
                for (int i = 0; i < apnts.Length; ++i)
                {
                    if (Surfaces.NewtonIntersect(this, uvExtent, curve, ref apnts[i])) // polynom root don't yield exactt results. We make it better here
                    {
                        double uu = curve.PositionOf(apnts[i]);
                        GeoPoint2D uv = this.PositionOf(apnts[i]);
                        SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uv);
                        double dist = apnts[i] | PointAt(uv);
                        if (uu > -Precision.eps && uu < 1 + Precision.eps && uvExtent.ContainsEps(uv, -0.01) && dist < 10 * Precision.eps)
                        {
                            lips.Add(apnts[i]);
                            luOnCurve3Ds.Add(uu);
                            luvOnFaces.Add(uv);
                        }
                    }
                }
                if (tangentialFound.IsValid)
                {
                    bool alreadyFound = false;
                    // we could have a tangential intersection and two more normal intersections, but the distance would be big
                    for (int i = 0; i < lips.Count; i++)
                    {
                        if ((lips[i] | tangentialFound) < (MajorRadius + MinorRadius) / 100)
                        {
                            lips[i] = tangentialFound;
                            luOnCurve3Ds[i] = curve.PositionOf(tangentialFound);
                            GeoPoint2D uv = this.PositionOf(tangentialFound);
                            SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uv);
                            luvOnFaces[i] = uv;
                            alreadyFound = true;
                        }
                    }
                    if (!alreadyFound)
                    {
                        double uu = curve.PositionOf(tangentialFound);
                        GeoPoint2D uv = this.PositionOf(tangentialFound);
                        SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uv);
                        if (uu > -Precision.eps && uu < 1 + Precision.eps && uvExtent.ContainsEps(uv, -0.01))
                        {
                            lips.Add(tangentialFound);
                            luOnCurve3Ds.Add(uu);
                            luvOnFaces.Add(uv);
                        }
                    }
                }
                for (int i = lips.Count - 1; i > 0; --i)
                {
                    if (Precision.IsEqual(lips[i], lips[i - 1]))
                    {
                        lips.RemoveAt(i);
                        luvOnFaces.RemoveAt(i);
                        luOnCurve3Ds.RemoveAt(i);
                    }
                }
                ips = lips.ToArray();
                uvOnFaces = luvOnFaces.ToArray();
                uOnCurve3Ds = luOnCurve3Ds.ToArray();
                return;
            }
            if (curve is Line)
            {
                ips = GetLineIntersection3D((curve as Line).StartPoint, (curve as Line).StartDirection);
                if (ips.Length == 2 && (ips[0] | ips[1]) < Precision.eps)
                {   // probably tangential intersection
                    GeoPoint ip = new GeoPoint(toTorus * ips[0], toTorus * ips[1]);
                    GeoPoint2D uv = PositionOf(ip);
                    double s = curve.PositionOf(ip);
                    double error = GaussNewtonMinimizer.SurfaceCurveExtrema(this, uvExtent, curve, 0.0, 1.0, ref uv, ref s);
                    if (error < Precision.eps)
                    {
                        GeoPoint ipt = PointAt(uv);
                        GeoPoint ipc = curve.PointAt(s);
                        if (Precision.IsEqual(ipc, ipt))
                        {
                            ips = new GeoPoint[] { new GeoPoint(ipt, ipc) };
                            uvOnFaces = new GeoPoint2D[] { uv };
                            uOnCurve3Ds = new double[] { s };
                            return;
                        }
                    }
                }

                // ips ist im Unit System
                uvOnFaces = new GeoPoint2D[ips.Length];
                uOnCurve3Ds = new double[ips.Length];
                for (int i = 0; i < ips.Length; i++)
                {
                    ips[i] = toTorus * ips[i];
                    uvOnFaces[i] = PositionOf(ips[i]);
                    SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uvOnFaces[i]);
                    uOnCurve3Ds[i] = curve.PositionOf(ips[i]);
                }
                return;
            }
            else if (curve is Ellipse)
            {
                // zwei Fälle: Ebene der Ellipse geht durch die Z-Achse oder ist parallel zu XY-Ebene
                Ellipse uelli = (curve.Clone() as Ellipse);
                uelli.Modify(toUnit);
                Plane pln = uelli.GetPlane();
                if (Precision.IsPointOnPlane(GeoPoint.Origin, pln) && Precision.IsPerpendicular(pln.Normal, GeoVector.ZAxis, false))
                {
                    GeoPoint cnt = uelli.Center;
                    Plane work;
                    if (Math.Abs(cnt.x) < Precision.eps && Math.Abs(cnt.y) < Precision.eps) work = new Plane(GeoPoint.Origin, pln.Normal ^ GeoVector.ZAxis, GeoVector.ZAxis);
                    else work = new Plane(GeoPoint.Origin, new GeoVector(cnt.x, cnt.y, 0), GeoVector.ZAxis); // in dieser Ebene die Schnittpunkte suchen
                    ICurve2D prelli = uelli.GetProjectedCurve(work);
                    if (prelli is Circle2D) // umfasst auch Arc2D
                    {
                        Circle2D c2d = (prelli as Circle2D);
                        GeoPoint2D[] ips2d = Geometry.IntersectCC(new GeoPoint2D(1, 0), minorRadius, c2d.Center, c2d.Radius);
                        uvOnFaces = new GeoPoint2D[ips2d.Length];
                        uOnCurve3Ds = new double[ips2d.Length];
                        ips = new GeoPoint[ips2d.Length];
                        for (int i = 0; i < ips2d.Length; i++)
                        {
                            ips[i] = toTorus * work.ToGlobal(ips2d[i]);
                            uvOnFaces[i] = PositionOf(ips[i]);
                            SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uvOnFaces[i]);
                            uOnCurve3Ds[i] = curve.PositionOf(ips[i]);
                        }
                        return;
                    }
                    if (prelli is Ellipse2D) // umfasst auch Arc2D
                    {
                        Ellipse2D c2d = (prelli as Ellipse2D);
                        GeoPoint2DWithParameter[] ips2d = c2d.Intersect(new Circle2D(new GeoPoint2D(1, 0), minorRadius));
                        uvOnFaces = new GeoPoint2D[ips2d.Length];
                        uOnCurve3Ds = new double[ips2d.Length];
                        ips = new GeoPoint[ips2d.Length];
                        for (int i = 0; i < ips2d.Length; i++)
                        {
                            ips[i] = toTorus * work.ToGlobal(ips2d[i].p);
                            uvOnFaces[i] = PositionOf(ips[i]);
                            SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uvOnFaces[i]);
                            uOnCurve3Ds[i] = curve.PositionOf(ips[i]);
                        }
                        return;
                    }
                }
                else if (Precision.SameDirection(pln.Normal, GeoVector.ZAxis, false))
                {   // the ellipse is horizontal in respect to the system of the torus
                    // we need to intersect with the two horizontal circles of the torus
                    List<GeoPoint> lips = new List<GeoPoint>();
                    List<GeoPoint2D> luvOnFaces = new List<GeoPoint2D>();
                    List<double> luOnCurve3Ds = new List<double>();
                    Plane epln = new Plane(Plane.StandardPlane.XYPlane, uelli.Center.z);
                    double s = pln.Location.z / minorRadius;
                    if (s >= -1.0 && s <= 1.0)
                    {
                        double v = Math.Asin(s);
                        ImplicitPSurface dbg = (this as IImplicitPSurface).GetImplicitPSurface();

                        //ICurve bigCircle1 = FixedV(v, 0, Math.PI * 2);
                        //ICurve bigCircle2 = FixedV(Math.PI - v, 0, Math.PI * 2);
                        // two big circles on the torus are in the plane of the ellipse uelli
                        ICurve2D prelli = uelli.GetProjectedCurve(epln);
                        double r1 = (1 + minorRadius * Math.Cos(v));
                        double r2 = (1 + minorRadius * Math.Cos(Math.PI - v));
                        foreach (Circle2D c2d in new Circle2D[] { new Circle2D(GeoPoint2D.Origin, r1), new Circle2D(GeoPoint2D.Origin, r2) })
                        {
                            GeoPoint2DWithParameter[] ips2d = prelli.Intersect(c2d);
                            for (int i = 0; i < ips2d.Length; i++)
                            {
                                GeoPoint ip = epln.ToGlobal(ips2d[i].p);
                                ip = toTorus * ip;
                                lips.Add(ip);
                                GeoPoint2D uv = PositionOf(ip);
                                SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uv);
                                luvOnFaces.Add(uv);
                                luOnCurve3Ds.Add(curve.PositionOf(ip));
                            }
                        }
                    } // otherwise the ellipse is above or below the torus

                    ips = lips.ToArray();
                    uvOnFaces = luvOnFaces.ToArray();
                    uOnCurve3Ds = luOnCurve3Ds.ToArray();
                    return;
                }
            }
            // ImplicitPSurface().Intersect ist nicht zuverlässig, bei Ellipsen wird der Grad x^8 und die Lösungen ungenau, so dass manche Lösungen verloren gehen
            //else if (curve is IExplicitPCurve3D)
            //{
            //    ExplicitPCurve3D epc = (curve as IExplicitPCurve3D).GetExplicitPCurve3D();
            //    double[] u;
            //    ips = (this as IImplicitPSurface).GetImplicitPSurface().Intersect(epc, out u);
            //    double prec = (MinorRadius + XAxis.Length) * 1e-8;
            //    List<GeoPoint> lips = new List<GeoPoint>();
            //    List<GeoPoint2D> luvOnFaces = new List<GeoPoint2D>();
            //    List<double> luOnCurve3Ds = new List<double>();
            //    for (int i = 0; i < ips.Length; i++)
            //    {   // Der Polynomschnitt ist oft nicht sehr genau, deshalb hier mit Newton nachbessern
            //        double uc;
            //        GeoPoint2D uv;
            //        if (BoxedSurfaceEx.NewtonCurveIntersection(curve, this, uvExtent, ref ips[i], out uv, out uc))
            //        {
            //            double d = PointAt(PositionOf(ips[i])) | ips[i];
            //            if (d < prec)
            //            {
            //                bool found = false;
            //                for (int j = 0; j < lips.Count; j++)
            //                {   // doppelte Lösungen aussparen
            //                    if ((lips[j] | ips[i]) < prec)
            //                    {
            //                        found = true;
            //                        break;
            //                    }
            //                }
            //                if (!found)
            //                {
            //                    lips.Add(ips[i]);
            //                    GeoPoint2D uvi = PositionOf(ips[i]);
            //                    SurfaceHelper.AdjustPeriodic(this, uvExtent, ref uvi);
            //                    luvOnFaces.Add(uvi);
            //                    luOnCurve3Ds.Add(curve.PositionOf(ips[i]));
            //                }
            //            }
            //        }
            //    }
            //    uvOnFaces = luvOnFaces.ToArray();
            //    uOnCurve3Ds = luOnCurve3Ds.ToArray();
            //    ips = lips.ToArray();
            //    return;
            //}
            base.Intersect(curve, uvExtent, out ips, out uvOnFaces, out uOnCurve3Ds);
        }
        public override ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {   // fill in more special cases
            if (other is PlaneSurface ps)
            {
                IDualSurfaceCurve[] pli = GetPlaneIntersection(ps, thisBounds.Left, thisBounds.Right, thisBounds.Bottom, thisBounds.Top, 0.0);
                ICurve[] res = new ICurve[pli.Length];
                for (int i = 0; i < pli.Length; i++)
                {
                    res[i] = pli[i].Curve3D;
                }
                return res;
            }
            else if (other is CylindricalSurface) return other.Intersect(otherBounds, this, thisBounds);
            return base.Intersect(thisBounds, other, otherBounds);
        }
        public override ICurve Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, GeoPoint seed)
        {
            ICurve[] crvs = Intersect(thisBounds, other, otherBounds);
            for (int i = 0; i < crvs.Length; i++)
            {
                if (crvs[i].DistanceTo(seed) < Precision.eps) return crvs[i];
            }
            return base.Intersect(thisBounds, other, otherBounds, seed);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public override void CopyData(ISurface CopyFrom)
        {
            ToroidalSurface cc = CopyFrom as ToroidalSurface;
            if (cc != null)
            {
                this.toTorus = cc.toTorus;
                this.toUnit = cc.toUnit;
                this.minorRadius = cc.minorRadius;
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
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetExtrema ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoPoint2D[] GetExtrema()
        {   // noch nicht getestet
            // Extrema in den Richtungen der Hauptachsen
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            GeoVector axis = toUnit * GeoVector.XAxis;
            if (!Precision.SameDirection(axis, GeoVector.ZAxis, false))
            {
                double u = Math.Atan2(axis.y, axis.x);
                double v = Math.Atan2(axis.z, Math.Sqrt(axis.x * axis.x + axis.y * axis.y));
                if (u < 0.0) u += Math.PI;
                if (v < 0.0) v += Math.PI;  // 4 Lösungen, u,v in 0..2pi
                res.Add(new GeoPoint2D(u, v));
                res.Add(new GeoPoint2D(u + Math.PI, v));
                res.Add(new GeoPoint2D(u, v + Math.PI));
                res.Add(new GeoPoint2D(u + Math.PI, v + Math.PI));
            }
            axis = toUnit * GeoVector.YAxis;
            if (!Precision.SameDirection(axis, GeoVector.ZAxis, false))
            {
                double u = Math.Atan2(axis.y, axis.x);
                double v = Math.Atan2(axis.z, Math.Sqrt(axis.x * axis.x + axis.y * axis.y));
                if (u < 0.0) u += Math.PI;
                if (v < 0.0) v += Math.PI;  // 4 Lösungen, u,v in 0..2pi
                res.Add(new GeoPoint2D(u, v));
                res.Add(new GeoPoint2D(u + Math.PI, v));
                res.Add(new GeoPoint2D(u, v + Math.PI));
                res.Add(new GeoPoint2D(u + Math.PI, v + Math.PI));
            }
            axis = toUnit * GeoVector.ZAxis;
            if (!Precision.SameDirection(axis, GeoVector.ZAxis, false))
            {
                double u = Math.Atan2(axis.y, axis.x);
                double v = Math.Atan2(axis.z, Math.Sqrt(axis.x * axis.x + axis.y * axis.y));
                if (u < 0.0) u += Math.PI;
                if (v < 0.0) v += Math.PI;  // 4 Lösungen, u,v in 0..2pi
                res.Add(new GeoPoint2D(u, v));
                res.Add(new GeoPoint2D(u + Math.PI, v));
                res.Add(new GeoPoint2D(u, v + Math.PI));
                res.Add(new GeoPoint2D(u + Math.PI, v + Math.PI));
            }
            return res.ToArray();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.GetProjectedCurve (ICurve, double)"/>
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {
            if (curve is Ellipse)
            {
#if DEBUG
                GeoObjectList dbgg = this.DebugGrid;
#endif
                Ellipse e = curve as Ellipse;
                if (precision == 0.0) precision = (MinorRadius + XAxis.Length) * 1e-6;
                // in welche Richtung geht der Kreisbogen und in welcher Richtung geht der Torus an dieser Stelle
                // in u bzw. v? Gleiche Richtung: Absolutbetrag von sweepparameter dazuaddieren
                // verschiedene Richtung: abziehen. Der Absolutbetrag sieht auf den ersten Moment komisch aus
                // muss aber sein: betrachtet man den gleichen Kreisbogen auf einer Ebenen und auf der gleichen Ebene
                // mit umgedrehtem Normalenvektor, so haben die beiden die gleiche Startrichtung, aber umgekehrtes
                // Vorzeichen beim sweepparameter
                if (Precision.SameDirection(e.Plane.Normal, this.ZAxis, false))
                {
                    if (Geometry.DistPL(e.Center, this.Location, this.ZAxis) <= precision)
                    { // ein Großkreis, auch wenn er nicht genau auf dem Torus liegt, denn dann geht es ja um die Projektion
                        GeoPoint2D uv = PositionOf(e.StartPoint);
                        GeoPoint dbguv3d = PointAt(uv);
                        GeoVector tordir = UDirection(uv);
                        GeoVector arcdir = e.StartDirection;
                        double d = tordir * arcdir;
                        if (e.IsArc)
                        {
                            if (d > 0)
                            {
                                // beides gleiche Richtung
                                Line2D res = new Line2D(new GeoPoint2D(uv.x, uv.y), new GeoPoint2D(uv.x + Math.Abs(e.SweepParameter), uv.y));
#if DEBUG
                                ICurve dbg = Make3dCurve(res);
#endif
                                return res;
                            }
                            else
                            {
                                Line2D res = new Line2D(new GeoPoint2D(uv.x, uv.y), new GeoPoint2D(uv.x - Math.Abs(e.SweepParameter), uv.y));
                                return res;
                            }
                        }
                        else
                        {
                            Line2D res;
                            if (d > 0)
                            {
                                res = new Line2D(new GeoPoint2D(uv.x, uv.y), new GeoPoint2D(uv.x + Math.PI * 2.0, uv.y));
                            }
                            else
                            {
                                res = new Line2D(new GeoPoint2D(uv.x + Math.PI * 2.0, uv.y), new GeoPoint2D(uv.x, uv.y));
                            }
                            return res;
                        }
                    }
                }
                else
                {
                    // Torus Mittelpunkt in Kreisebene und Kreisebene enthält ZAchse, dann Kleinkreis
                    if (Precision.IsPointOnPlane(this.Location, e.Plane) && Precision.IsPerpendicular(this.ZAxis, e.Plane.Normal, false))
                    {
                        GeoPoint2D uv = PositionOf(e.StartPoint);
                        double[] vs = GetVSingularities();
                        if (vs.Length > 0)
                        {
                            for (int i = 0; i < vs.Length; i++)
                            {
                                if (Math.Abs(uv.y - vs[i]) < 1e-4) uv.x = PositionOf(e.PointAt(0.5372516273)).x; // we did hit a pole with the startpoint, lets take some other point (but not the endpoint, if e is full circle)
                                // 1e-4 is not critical, because "PositionOf(e.PointAt(..." should always return the same result, the odd value is to assure we don't hit the other pole
                            }
                        }

                        GeoVector tordir = VDirection(uv);
                        GeoVector arcdir = e.StartDirection;
                        double d = tordir * arcdir;
                        if (e.IsArc)
                        {
                            if (d > 0)
                            {
                                // beides gleiche Richtung
                                Line2D res = new Line2D(new GeoPoint2D(uv.x, uv.y), new GeoPoint2D(uv.x, uv.y + Math.Abs(e.SweepParameter)));
                                return res;
                            }
                            else
                            {
                                Line2D res = new Line2D(new GeoPoint2D(uv.x, uv.y), new GeoPoint2D(uv.x, uv.y - Math.Abs(e.SweepParameter)));
                                return res;
                            }
                        }
                        else
                        {
                            Line2D res;
                            if (d > 0)
                            {
                                res = new Line2D(new GeoPoint2D(uv.x, uv.y), new GeoPoint2D(uv.x, uv.y + Math.PI * 2.0));
                            }
                            else
                            {
                                res = new Line2D(new GeoPoint2D(uv.x, uv.y + Math.PI * 2.0), new GeoPoint2D(uv.x, uv.y));
                            }
                            return res;
                        }
                    }
                }
            }

            return base.GetProjectedCurve(curve, precision);
        }
        //public override double Orientation(GeoPoint p)
        //{
        //    GeoPoint q = toUnit * p;
        //    if ((q - GeoPoint.Origin).Length < Precision.eps)
        //        return Math.Abs(1-minorRadius);
        //    double l = (q-GeoPoint.Origin).Length;
        //    if (Math.Abs(minorRadius - 1) < Precision.eps)
        //    {
        //        if (q.x * q.x + q.y * q.y < Precision.eps)
        //            return l;
        //        GeoPoint2D[] gp = GetLineIntersection(Location, p - Location);
        //        for (int i = 0; i < gp.Length; ++i)
        //        {
        //            if ((toUnit * (PointAt(gp[i]) - Location)).Length > l)
        //                return -l;
        //        }
        //        return l;
        //    }
        //    else 
        //    {
        //        if ((PointAt(PositionOf(p))-p).Length<Precision.eps)
        //            return -l;
        //        GeoPoint2D[] gp = GetLineIntersection(Location, p - Location);
        //        if (gp.Length < 4)
        //            return l;
        //        int n = 0;
        //        for (int i = 0; i < 4; ++i) {
        //            if((toUnit * (PointAt(gp[i]) - Location)).Length > l)
        //                n++;
        //        }
        //        if (n == 2)
        //            return -l;
        //        if (n == 4 || n == 0)
        //            return l;
        //        else throw new Exception();
        //    }
        //}

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.Orientation (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override double Orientation(GeoPoint p)
        {
            GeoPoint q = toUnit * p;
            double d = (p - Location).Length;
            double u = Math.Sqrt(q.x * q.x + q.y * q.y);
            double v = Math.Abs(q.z);
            double d1 = (u - 1) * (u - 1) + v * v;
            double d2 = (u + 1) * (u + 1) + v * v;
            if ((d1 < minorRadius * minorRadius) != (d2 < minorRadius * minorRadius))
                return -d;
            return d;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.ReverseOrientation ()"/>
        /// </summary>
        /// <returns></returns>
        public override ModOp2D ReverseOrientation()
        {
            boxedSurfaceEx = null;
            toTorus = toTorus * new ModOp(1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1, 0); // umkehrung von y
            toUnit = toTorus.GetInverse();
            return new ModOp2D(-1, 0, 2.0 * Math.PI, 0, 1, 0);
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.ISurfaceImpl.HitTest (BoundingCube, out GeoPoint2D)"/>
        /// </summary>
        /// <param name="bc"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public override bool HitTest(BoundingCube bc, out GeoPoint2D uv)
        {
            // any vertex of the cube on the torus?
            uv = GeoPoint2D.Origin;
            GeoPoint[] cube = bc.Points;
            GeoPoint[] points = new GeoPoint[8];
            for (int i = 0; i < 8; ++i)
            {
                points[i] = toUnit * cube[i];
            }
            bool[] pos = new bool[8];
            for (int i = 0; i < 8; ++i)
            {
                GeoPoint p = cube[i];
                if ((PointAt(PositionOf(p)) - p).Length < Precision.eps)
                {
                    uv = PositionOf(p);
                    return true;
                }
                pos[i] = Orientation(p) < 0;
            }

            // any line of the cube interfering the torus?
            int[,] l = bc.LineNumbers;
            for (int k = 0; k < 12; ++k)
            {
                int i = l[k, 0];
                int j = l[k, 1];
                GeoPoint[] erg = GetLineIntersection3D(cube[i], cube[j] - cube[i]);
                for (int m = 0; m < erg.Length; ++m)
                {
                    GeoPoint gp = toTorus * erg[m];
                    if (bc.Contains(gp, bc.Size * 1e-8))
                    {
                        uv = PositionOfInUnit(erg[m]);
                        return true;
                    }
                }
                if (pos[i] != pos[j])
                {
                    GeoPoint2D[] isp2d = base.GetLineIntersection(cube[i], cube[j] - cube[i]);
                    if (isp2d.Length > 0)
                    {
                        uv = isp2d[0];
                        return true;
                    }
                }
                //throw new ApplicationException("internal error: ToroidalSurface.HitTest");
            }

            //  any of the cube's faces hitting the torus?
            GeoVector[] nv = new GeoVector[3];
            nv[0] = (points[0] - points[1]) ^ (points[0] - points[2]);
            nv[1] = (points[0] - points[1]) ^ (points[0] - points[4]);
            nv[2] = (points[0] - points[2]) ^ (points[0] - points[4]);
            for (int i = 0; i < 3; ++i)
            {
                //GeoPoint pu = toUnit * p;
                ////double u = Math.Atan2(pu.y, pu.x);
                //double d = Math.Sqrt(pu.x * pu.x + pu.y * pu.y);
                ////double v = Math.Atan2(pu.z, d);
                //return new GeoPoint2D(Math.Atan2(pu.y, pu.x), Math.Atan2(pu.z, d));

                double d = Math.Sqrt(nv[i].x * nv[i].x + nv[i].y * nv[i].y);
                double u = Math.Atan2(nv[i].y, nv[i].x);
                double v = Math.Atan2(nv[i].z, d);
                GeoPoint2D[] gp = new GeoPoint2D[4];
                gp[0] = new GeoPoint2D(u, v);
                gp[1] = new GeoPoint2D(u, Math.PI + v);
                gp[2] = new GeoPoint2D(Math.PI - u, v);
                gp[3] = new GeoPoint2D(Math.PI - u, Math.PI + v);
                for (int j = 0; j < gp.Length; ++j)
                {
                    GeoPoint x = PointAt(gp[j]);
                    if (bc.Contains(x, bc.Size * 1e-8))
                    {
                        uv = new GeoPoint2D(u, v);
                        return true;
                    }
                }
            }

            //  is the complete torus in the cube?
            GeoPoint g = toTorus * (new GeoPoint(1 - minorRadius, 0, 0));
            if (bc.Contains(g))
            {
                uv = PositionOf(g);
                return true;
            }

            //  now the cube is completely inside or outside of the torus
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
            return new double[] { 0.0, Math.PI / 2.0, Math.PI, 3 * Math.PI / 2, 2 * Math.PI };
        }
        public override void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            umin = vmin = 0.0;
            umax = vmax = Math.PI * 2.0;
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
            if (other is ToroidalSurface)
            {
                firstToSecond = ModOp2D.Null;
                ToroidalSurface tsother = other as ToroidalSurface;
                if ((tsother.Location | Location) > precision) return false;
                if (Math.Abs(XAxis.Length - tsother.XAxis.Length) > precision) return false;
                if (Math.Abs(YAxis.Length - tsother.YAxis.Length) > precision) return false;
                // if (Math.Abs(ZAxis.Length - tsother.ZAxis.Length) > precision) return false; // die Länge der Z-Achse
                if (Math.Abs(MinorRadius - tsother.MinorRadius) > precision) return false;
                // fehlt noch die Richtung der Z-Achse, ist aber mit Genauigkeit schwierig
                GeoPoint2D[] uvthis = new GeoPoint2D[3];
                GeoPoint2D[] uvother = new GeoPoint2D[3];
                uvthis[0] = new GeoPoint2D(0.0, 0.0);
                uvthis[1] = new GeoPoint2D(0.0, 1.0);
                uvthis[2] = new GeoPoint2D(1.0, 0.0);
                GeoPoint p1 = PointAt(uvthis[0]);
                uvother[0] = tsother.PositionOf(p1);
                GeoPoint p2 = tsother.PointAt(uvother[0]);
                if ((p1 | p2) > precision) return false;
                p1 = PointAt(new GeoPoint2D(Math.PI / 2.0, 0.0)); // bei 90°
                p2 = tsother.PointAt(tsother.PositionOf(p1));
                if ((p1 | p2) > precision) return false;
                BoundingRect br = new BoundingRect(uvother[0], Math.PI, Math.PI); // we use three points close to each other
                uvother[1] = tsother.PositionOf(this.PointAt(uvthis[1]));
                uvother[2] = tsother.PositionOf(this.PointAt(uvthis[2]));
                SurfaceHelper.AdjustPeriodic(tsother, br, ref uvother[1]);
                SurfaceHelper.AdjustPeriodic(tsother, br, ref uvother[2]);
                firstToSecond = ModOp2D.Fit(uvthis, uvother, true);
                return true;
            }
            return base.SameGeometry(thisBounds, other, otherBounds, precision, out firstToSecond);
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
            double det = toUnit.Determinant;
            double f = Math.Sign(det) * Math.Pow(Math.Abs(det), 1.0 / 3.0);
            double unitoffset = f * offset; // sowohl offset als auch f können negativ sein
            return new ToroidalSurface(toTorus, minorRadius + unitoffset);
        }
        public override ICurve2D[] GetSelfIntersections(BoundingRect bounds)
        {
            List<ICurve2D> res = new List<ICurve2D>();
            double[] vs = GetVSingularities();
            for (int i = 0; i < vs.Length; i++)
            {
                double vm = (bounds.Bottom + bounds.Top) / 2;
                while (Math.Abs(vs[i] - vm) > Math.Abs(vs[i] - Math.PI * 2.0 - vm)) vs[i] -= Math.PI * 2.0;
                while (Math.Abs(vs[i] - vm) > Math.Abs(vs[i] + Math.PI * 2.0 - vm)) vs[i] += Math.PI * 2.0;
                if (vs[i] >= bounds.Bottom && vs[i] <= bounds.Top)
                {
                    Line2D l2d = new Line2D(new GeoPoint2D(bounds.Left, vs[i]), new GeoPoint2D(bounds.Right, vs[i]));
                    res.Add(l2d);
                }
            }
            return res.ToArray();
        }
        public override double[] GetVSingularities()
        {
            if (Math.Abs(minorRadius) > 1.0)
            {
                double v = Math.Acos(1.0 / minorRadius);
                return new double[] { Math.PI - v, Math.PI + v };
            }
            else if (Math.Abs(minorRadius) > 1.0 - 1e-8)
            {
                return new double[] { Math.PI }; // a touching point in the middle
            }
            return new double[0];
        }
        public override IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> extremePositions)
        {
            if (other is ToroidalSurface)
            {   // two toroidal surfaces with the same axis may return two circles
                ToroidalSurface ot = (other as ToroidalSurface);
                if (Precision.SameDirection(this.ZAxis, ot.ZAxis, false))
                {
                    GeoPoint cnt = toUnit * ot.Location;
                    if (Math.Sqrt(cnt.x * cnt.x + cnt.y * cnt.y) < 1e-6)
                    {
                        GeoPoint[] ips;
                        GeoPoint2D[] uv;
                        double[] u;
                        Intersect(ot.FixedU(0, 0, Math.PI * 2), thisBounds, out ips, out uv, out u);
                        List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                        if (uv.Length > 0)
                        {
                            for (int i = 0; i < uv.Length; i++)
                            {
                                ICurve crv = FixedV(uv[i].y, thisBounds.Left, thisBounds.Right);
                                DualSurfaceCurve dsc = new DualSurfaceCurve(crv, this, GetProjectedCurve(crv, 0.0), other, other.GetProjectedCurve(crv, 0.0));
                                SurfaceHelper.AdjustPeriodic(this, thisBounds, (dsc as IDualSurfaceCurve).Curve2D1);
                                SurfaceHelper.AdjustPeriodic(other, otherBounds, (dsc as IDualSurfaceCurve).Curve2D2);
                                // das Problem waren zwei Torushälften, die sich in exakt zwei Eckpunkten trafen, jedoch keine gemeinsame Schnittkurve haben
                                if (!thisBounds.Contains((dsc as IDualSurfaceCurve).Curve2D1.PointAt(0.5)) || !otherBounds.Contains((dsc as IDualSurfaceCurve).Curve2D2.PointAt(0.5))) continue;
                                res.Add(dsc);
                            }
                        }
                        return res.ToArray();
                    }
                }
            }
            if (other is PlaneSurface)
            {   // if the axis is perpendicular to the plane, we may get two circles
                if (Precision.SameDirection(this.Axis, (other as PlaneSurface).Normal, false))
                {
                    Plane unitPlane = toUnit * (other as PlaneSurface).Plane;
                    if (Math.Abs(unitPlane.Location.z) <= Math.Abs(minorRadius) + Precision.eps)
                    {
                        List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                        double v1 = Math.Asin(Math.Max(Math.Min((unitPlane.Location.z / minorRadius), 1), -1)); // -pi/2 ... +pi/2
                        double v2 = Math.PI - v1;
                        if (v1 < 0) v2 = -Math.PI - v1;
                        if (Math.Abs(v1 - v2) < 1e-5)
                        {   // single solution
                            double v = Math.PI / 2.0;
                            if (v1 < 0) v = -v;
                            ICurve crv = FixedV(v, thisBounds.Left, thisBounds.Right);
                            Line2D l2d = new Line2D(new GeoPoint2D(thisBounds.Left, v), new GeoPoint2D(thisBounds.Right, v));
                            SurfaceHelper.AdjustPeriodic(this, thisBounds, l2d);
                            DualSurfaceCurve dsc = new DualSurfaceCurve(crv, this, l2d, other, other.GetProjectedCurve(crv, 0.0));
                            res.Add(dsc);
                        }
                        else
                        {
                            ICurve crv = FixedV(v1, thisBounds.Left, thisBounds.Right);
                            Line2D l2d = new Line2D(new GeoPoint2D(thisBounds.Left, v1), new GeoPoint2D(thisBounds.Right, v1));
                            SurfaceHelper.AdjustPeriodic(this, thisBounds, l2d);
                            DualSurfaceCurve dsc = new DualSurfaceCurve(crv, this, l2d, other, other.GetProjectedCurve(crv, 0.0));
                            res.Add(dsc);
                            crv = FixedV(v2, thisBounds.Left, thisBounds.Right);
                            l2d = new Line2D(new GeoPoint2D(thisBounds.Left, v2), new GeoPoint2D(thisBounds.Right, v2));
                            SurfaceHelper.AdjustPeriodic(this, thisBounds, l2d);
                            dsc = new DualSurfaceCurve(crv, this, new Line2D(new GeoPoint2D(thisBounds.Left, v2), new GeoPoint2D(thisBounds.Right, v2)), other, other.GetProjectedCurve(crv, 0.0));
                            res.Add(dsc);
                        }
                        return res.ToArray();
                    }
                }
                else if (Precision.IsPerpendicular(this.Axis, (other as PlaneSurface).Normal, false))
                {   // the torus axis is parallel to the plane
                    GeoPoint p = other.PointAt(other.PositionOf(this.Location));
                    if (Precision.IsEqual(p, this.Location))
                    { // the plane passes through the center of the torus
                      // resulting in two circles as a result
                        Plane unitPlane = toUnit * (other as PlaneSurface).Plane;
                        double u1 = Math.Atan2(unitPlane.Normal.y, unitPlane.Normal.x);
                        List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
                        foreach (double uu in new double[] { u1 - Math.PI / 2.0, u1 + Math.PI / 2.0 })
                        {
                            double u = uu; // to be able to change it
                            SurfaceHelper.AdjustUPeriodic(this, thisBounds.Left, thisBounds.Right, ref u);
                            if (u >= thisBounds.Left - 1e-6 && u <= thisBounds.Right + 1e-6)
                            {
                                // not yet tested!!!
                                ICurve crv = FixedU(u, thisBounds.Bottom, thisBounds.Top);
                                Line2D l2d = new Line2D(new GeoPoint2D(u, thisBounds.Bottom), new GeoPoint2D(u, thisBounds.Top));
                                DualSurfaceCurve dsc = new DualSurfaceCurve(crv, this, l2d, other, other.GetProjectedCurve(crv, 0.0));
                                res.Add(dsc);
                            }
                        }
                        return res.ToArray();
                    }
                }
            }
            if (other is CylindricalSurface cylindricalSurface)
            {   // we need tangential points to split the result there
                if (extremePositions != null)
                {
                    List<Tuple<double, double, double, double>> swapped = new List<Tuple<double, double, double, double>>();
                    for (int i = 0; i < extremePositions.Count; i++)
                    {
                        swapped.Add(new Tuple<double, double, double, double>(extremePositions[0].Item3, extremePositions[0].Item4, extremePositions[0].Item1, extremePositions[0].Item2));
                    }
                    extremePositions = swapped;
                }
                IDualSurfaceCurve[] res = cylindricalSurface.GetDualSurfaceCurves(otherBounds, this, thisBounds, seeds, extremePositions);
                if (res != null)
                {
                    for (int i = 0; i < res.Length; i++)
                    {
                        res[i].SwapSurfaces();
                    }
                }
                return res;
            }
            return base.GetDualSurfaceCurves(thisBounds, other, otherBounds, seeds, extremePositions);
        }

        public override ISurface GetNonPeriodicSurface(ICurve[] orientedCurves)
        {
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            GeoPoint2D startPoint = GeoPoint2D.Origin;
            for (int i = 0; i < orientedCurves.Length; i++)
            {
                ICurve2D pc = GetProjectedCurve(orientedCurves[i], 0.0);
                if (!ext.IsEmpty())
                {
                    SurfaceHelper.AdjustPeriodicStartPoint(this, startPoint, pc);
                }
                ext.MinMax(pc.GetExtent());
                startPoint = pc.EndPoint;
            }
            if (ext.Width < Math.PI * 2.0 - 0.1 || ext.Height < Math.PI * 2.0 - 0.1) return new NonPeriodicSurface(this, ext);
            return null;
        }
        #endregion
        #region ISerializable Members
        protected ToroidalSurface(SerializationInfo info, StreamingContext context)
        {
            toTorus = (ModOp)info.GetValue("ToTorus", typeof(ModOp));
            minorRadius = (double)info.GetValue("MinorRadius", typeof(double));
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ToTorus", toTorus, typeof(ModOp));
            info.AddValue("MinorRadius", minorRadius, typeof(double));
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            toUnit = toTorus.GetInverse();
        }
        #endregion
        public override IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            List<IPropertyEntry> se = new List<IPropertyEntry>();
            GeoPointProperty location = new GeoPointProperty(frame, "ToroidalSurface.Location");
            location.ReadOnly = true;
            location.OnGetValue = new EditableProperty<GeoPoint>.GetValueDelegate(delegate () { return toTorus * GeoPoint.Origin; });
            se.Add(location);
            GeoVectorProperty dirx = new GeoVectorProperty(frame, "ConicaToroidalSurfacelSurface.DirectionX");
            dirx.ReadOnly = true;
            dirx.IsAngle = false;
            dirx.OnGetValue = new EditableProperty<GeoVector>.GetValueDelegate(delegate () { return toTorus * GeoVector.XAxis; });
            se.Add(dirx);
            GeoVectorProperty diry = new GeoVectorProperty(frame, "ToroidalSurface.DirectionY");
            diry.ReadOnly = true;
            diry.IsAngle = false;
            diry.OnGetValue = new EditableProperty<GeoVector>.GetValueDelegate(delegate () { return toTorus * GeoVector.XAxis; });
            se.Add(diry);
            DoubleProperty majrad = new DoubleProperty(frame, "ToroidalSurface.MajorRadius");
            majrad.ReadOnly = true;
            majrad.OnGetValue = new EditableProperty<double>.GetValueDelegate(delegate () { return toTorus.Factor; });
            se.Add(majrad);
            DoubleProperty minrad = new DoubleProperty(frame, "ToroidalSurface.MinorRadius");
            minrad.ReadOnly = true;
            minrad.OnGetValue = new EditableProperty<double>.GetValueDelegate(delegate () { return toTorus.Factor * minorRadius; });
            se.Add(minrad);
            return new GroupProperty("ToroidalSurface", se.ToArray());
        }
        ImplicitPSurface IImplicitPSurface.GetImplicitPSurface()
        {
            // implicit form according to https://en.wikipedia.org/wiki/Implicit_surface
            // where R==1 and a==minorRadius
            Polynom p = new Polynom(1, "x2", 1, "y2", 1, "z2", 1 - minorRadius * minorRadius, "");
            Polynom pp = p * p + new Polynom(-4, "x2", -4, "y2", 0, "z"); // 0 z to make a 3 dimensional polynom
            ImplicitPSurface res = new ImplicitPSurface(pp, toUnit); // yes, toUnit, not toTorus!

            return res;
        }
        #region ISurfaceOf(Arc)Extrusion
        ICurve ISurfaceOfExtrusion.Axis(BoundingRect domain)
        {   // the axis of the extrusion is an arc
            Ellipse elli = Ellipse.Construct();
            elli.SetArcPlaneCenterRadius(new Plane(Location, XAxis, YAxis), Location, XAxis.Length);
            elli.StartParameter = domain.Left;
            elli.SweepParameter = domain.Width;
            return elli;
        }
        bool ISurfaceOfExtrusion.ModifyAxis(GeoPoint throughPoint)
        {   // a movement along the axis and a change of the major radius. The axis to modify is the circular axis of the torus
            GeoPoint onAxis = Geometry.DropPL(throughPoint, Location, ZAxis);
            double oldMinorRadius = MinorRadius;
            double majorRadius = Geometry.DistPL(throughPoint, Location, ZAxis);
            ModOp m1 = ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis },
                new GeoVector[] { majorRadius * XAxis.Normalized, majorRadius * YAxis.Normalized, majorRadius * ZAxis.Normalized });
            ModOp m2 = ModOp.Translate(onAxis.x, onAxis.y, onAxis.z);
            toTorus = m2 * m1;
            toUnit = toTorus.GetInverse();
            this.minorRadius = toUnit.Factor * oldMinorRadius;
            return true;
        }
        IOrientation ISurfaceOfExtrusion.Orientation => throw new NotImplementedException();
        ICurve ISurfaceOfExtrusion.ExtrudedCurve => usedArea.IsEmpty() || usedArea.IsInfinite || usedArea.IsInvalid() ?
            FixedU(0.0, 0.0, Math.PI) : FixedU(0.0, usedArea.Bottom, usedArea.Right);
        /// <summary>
        /// Setting the radius of a ISurfaceOfArcExtrusion means setting the radius of the extruded arc, which is the minor radius in this case
        /// </summary>
        double ISurfaceOfArcExtrusion.Radius
        {
            get
            {
                return MinorRadius;
            }
            set
            {
                minorRadius = toUnit.Factor * value;
            }
        }

        bool ISurfaceOfExtrusion.ExtrusionDirectionIsV => false; // it is the extrusion of the "small" circle around the axis
        #endregion

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            int ax = export.WriteAxis2Placement3d(Location, Axis, XAxis);
            int res = export.WriteDefinition("TOROIDAL_SURFACE('',#" + ax.ToString() + "," + export.ToString(XAxis.Length) + "," + export.ToString(MinorRadius) + ")");
            if (toTorus.Determinant < 0) return -res; // the normal vector points to the inside, STEP only knows toroidal surfaces with outside pointing normals, 
            // the sign of the result holds the orientation information
            else return res;
        }
#if DEBUG
        override public GeoObjectList DebugGrid
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                double umin = 0;
                double umax = 6.0; // leave a little gap, so you can see where u starts
                double vmin = 0;
                double vmax = 6.0;  // leave a little gap, so you can see where u starts
                int n = 25;
                for (int i = 0; i <= n; i++)
                {   // über die Diagonale
                    GeoPoint[] pu = new GeoPoint[n + 1];
                    GeoPoint[] pv = new GeoPoint[n + 1];
                    for (int j = 0; j <= n; j++)
                    {
                        pu[j] = PointAt(new GeoPoint2D(umin + j * (umax - umin) / n, vmin + i * (vmax - vmin) / n));
                        pv[j] = PointAt(new GeoPoint2D(umin + i * (umax - umin) / n, vmin + j * (vmax - vmin) / n));
                    }
                    try
                    {
                        Polyline plu = Polyline.Construct();
                        plu.SetPoints(pu, false);
                        res.Add(plu);
                    }
                    catch (PolylineException)
                    {
                        Point pntu = Point.Construct();
                        pntu.Location = pu[0];
                        pntu.Symbol = PointSymbol.Cross;
                        res.Add(pntu);
                    }
                    try
                    {
                        Polyline plv = Polyline.Construct();
                        plv.SetPoints(pv, false);
                        res.Add(plv);
                    }
                    catch (PolylineException)
                    {
                        Point pntv = Point.Construct();
                        pntv.Location = pv[0];
                        pntv.Symbol = PointSymbol.Cross;
                        res.Add(pntv);
                    }
                }
                return res;
            }
        }
        override public Face DebugAsFace
        {
            get
            {
                BoundingRect ext = new BoundingRect(0.0, 0.0, 6.0, 6.0);
                return Face.MakeFace(this, new CADability.Shapes.SimpleShape(ext));
            }
        }
#endif
    }
}
