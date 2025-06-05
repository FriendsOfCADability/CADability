using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;

namespace CADability
{
    internal class ScaledSurface : ISurface
    {
        double fu, fv;
        ISurface original;
        ModOp2D scale, unscale;

        public ScaledSurface(ISurface original, double fu, double fv)
        {
            this.fu = fu;
            this.fv = fv;
            this.original = original;
            scale = ModOp2D.Scale(fu, fv);
            unscale = scale.GetInverse();
        }
        public ISurface GetModified(ModOp m)
        {
            return new ScaledSurface(original.GetModified(m), fu, fv);
        }

        public ICurve Make3dCurve(Curve2D.ICurve2D curve2d)
        {
            return original.Make3dCurve(curve2d.GetModified(unscale));
        }

        public GeoVector GetNormal(GeoPoint2D uv)
        {
            return original.GetNormal(unscale * uv);
        }

        public GeoVector UDirection(GeoPoint2D uv)
        {
            return original.UDirection(unscale * uv);
        }

        public GeoVector VDirection(GeoPoint2D uv)
        {
            return original.VDirection(unscale * uv);
        }

        public GeoPoint PointAt(GeoPoint2D uv)
        {
            return original.PointAt(unscale * uv);
        }

        public GeoPoint2D PositionOf(GeoPoint p)
        {
            return scale * original.PositionOf(p);
        }

        public void DerivationAt(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv)
        {
            original.DerivationAt(unscale * uv, out location, out du, out dv); // du und dv noch skalieren?
        }

        public void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            original.Derivation2At(unscale * uv, out location, out du, out dv, out duu, out dvv, out duv); // du und dv noch skalieren?
        }

        public IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision)
        {
            return original.GetPlaneIntersection(pl, fu * umin, fu * umax, fv * vmin, fv * vmax, precision);
        }

        public ICurve2D[] GetTangentCurves(GeoVector direction, double umin, double umax, double vmin, double vmax)
        {
            ICurve2D[] res = original.GetTangentCurves(direction, fu * umin, fu * umax, fv * vmin, fv * vmax);
            if (res != null) for (int i = 0; i < res.Length; i++)
                {
                    res[i] = res[i].GetModified(scale);
                }
            return res;
        }

        public void GetSafeParameterSteps(double umin, double umax, double vmin, double vmax, out double[] intu, out double[] intv)
        {
            original.GetSafeParameterSteps(fu * umin, fu * umax, fv * vmin, fv * vmax, out intu, out intv);
            for (int i = 0; i < intu.Length; i++)
            {
                intu[i] = fu * intu[i];
            }
            for (int i = 0; i < intv.Length; i++)
            {
                intv[i] = fv * intv[i];
            }
        }

        public bool IsVanishingProjection(Projection p, double umin, double umax, double vmin, double vmax)
        {
            return original.IsVanishingProjection(p, fu * umin, fu * umax, fv * vmin, fv * vmax);
        }

        public GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            GeoPoint2D[] res = original.GetLineIntersection(startPoint, direction);
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = scale * res[i];
            }
            return res;
        }

        public bool IsUPeriodic
        {
            get { return original.IsUPeriodic; }
        }

        public bool IsVPeriodic
        {
            get { return original.IsVPeriodic; }
        }

        public double UPeriod
        {
            get { return fu * original.UPeriod; }
        }

        public double VPeriod
        {
            get { return fv * original.VPeriod; }
        }

        public double[] GetUSingularities()
        {
            double[] res = original.GetUSingularities();
            if (res != null) for (int i = 0; i < res.Length; i++)
                {
                    res[i] = fu * res[i];
                }
            return res;
        }

        public double[] GetVSingularities()
        {
            double[] res = original.GetVSingularities();
            if (res != null) for (int i = 0; i < res.Length; i++)
                {
                    res[i] = fv * res[i];
                }
            return res;
        }

        public Face MakeFace(Shapes.SimpleShape simpleShape)
        {
            return original.MakeFace(simpleShape.GetModified(unscale));
        }

        // die folgenden sind noch Schreibarbeit, wir z.Z. nur für das Triangulieren verwendet:
        public void GetZMinMax(Projection p, double umin, double umax, double vmin, double vmax, ref double zMin, ref double zMax)
        {
            throw new NotImplementedException();
        }

        public ModOp2D MakeCanonicalForm()
        {
            throw new NotImplementedException();
        }

        public ISurface Clone()
        {
            throw new NotImplementedException();
        }

        public void Modify(ModOp m)
        {
            throw new NotImplementedException();
        }

        public void CopyData(ISurface CopyFrom)
        {
            throw new NotImplementedException();
        }

        public NurbsSurface Approximate(double umin, double umax, double vmin, double vmax, double precision)
        {
            throw new NotImplementedException();
        }

        public Curve2D.ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {
            throw new NotImplementedException();
        }

        public void Intersect(ICurve curve, BoundingRect uvExtent, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {
            throw new NotImplementedException();
        }

        public ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            throw new NotImplementedException();
        }

        public ICurve Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, GeoPoint seed)
        {
            throw new NotImplementedException();
        }

        public ModOp2D ReverseOrientation()
        {
            throw new NotImplementedException();
        }

        public bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
        {
            throw new NotImplementedException();
        }
        public double IsParallel(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            return double.MaxValue;
        }

        public ISurface GetOffsetSurface(double offset)
        {
            throw new NotImplementedException();
        }

        public void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            throw new NotImplementedException();
        }

        public bool HitTest(BoundingCube cube, double umin, double umax, double vmin, double vmax)
        {
            throw new NotImplementedException();
        }

        public bool HitTest(BoundingCube cube, out GeoPoint2D uv)
        {
            throw new NotImplementedException();
        }

        public bool Oriented
        {
            get { throw new NotImplementedException(); }
        }

        public double Orientation(GeoPoint p)
        {
            throw new NotImplementedException();
        }

        public GeoPoint2D[] GetExtrema()
        {
            throw new NotImplementedException();
        }

        public BoundingCube GetPatchExtent(BoundingRect uvPatch, bool rough)
        {
            throw new NotImplementedException();
        }

        public ICurve FixedU(double u, double vmin, double vmax)
        {
            throw new NotImplementedException();
        }

        public ICurve FixedV(double v, double umin, double umax)
        {
            throw new NotImplementedException();
        }

        public double[] GetPolynomialParameters()
        {
            throw new NotImplementedException();
        }

        public void SetBounds(BoundingRect boundingRect)
        {
            throw new NotImplementedException();
        }

        public GeoPoint2D[] PerpendicularFoot(GeoPoint fromHere)
        {
            throw new NotImplementedException();
        }

        public bool HasDiscontinuousDerivative(out Curve2D.ICurve2D[] discontinuities)
        {
            throw new NotImplementedException();
        }

        public ISurface GetNonPeriodicSurface(ICurve[] orientedCurves)
        {
            throw new NotImplementedException();
        }

        public void GetPatchHull(BoundingRect uvpatch, out GeoPoint loc, out GeoVector dir1, out GeoVector dir2, out GeoVector dir3)
        {
            throw new NotImplementedException();
        }

        public RuledSurfaceMode IsRuled
        {
            get { throw new NotImplementedException(); }
        }

        public bool UvChangesWithModification => throw new NotImplementedException();

        public IPropertyEntry PropertyEntry => throw new NotImplementedException();

        public double MaxDist(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp)
        {
            double res = original.MaxDist(unscale * sp, unscale * ep, out mp);
            mp = scale * mp;
            return res;
        }

        public IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds)
        {
            throw new NotImplementedException();
        }

        public ICurve2D[] GetSelfIntersections(BoundingRect bounds)
        {

            ICurve2D[] res = original.GetSelfIntersections(new BoundingRect(bounds.Left * fu, bounds.Bottom * fv, bounds.Right * fu, bounds.Top * fv));
            if (res != null)
            {
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = res[i].GetModified(scale);
                }
            }
            return res;
        }
        public GeoPoint[] GetTouchingPoints(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            throw new NotImplementedException();
        }
        public ISurface GetCanonicalForm(double precision, BoundingRect? bounds)
        {
            return original.GetCanonicalForm(precision, bounds);
        }

        public int GetExtremePositions(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, out List<Tuple<double, double, double, double>> extremePositions)
        {
            // the 2d points cannot be transformed, because maybe one component is NaN
            return original.GetExtremePositions(thisBounds, other, otherBounds, out extremePositions);
        }

        public IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> extremePositions = null)
        {
            return original.GetDualSurfaceCurves(thisBounds, other, otherBounds, seeds, extremePositions);
        }

        public double GetDistance(GeoPoint p)
        {
            return original.GetDistance(p);
        }
        public bool IsExtruded(GeoVector direction)
        {
            return original.IsExtruded(direction);
        }

        public int GetExtremePositions(BoundingRect domain, ICurve curve3D, out List<Tuple<double, double, double>> positions)
        {
            throw new NotImplementedException();
        }

        public MenuWithHandler[] GetContextMenuForParametrics(IFrame frame, Face face)
        {
            throw new NotImplementedException();
        }
        public IPropertyEntry GetPropertyEntry(IFrame frame)
        {
            return new GroupProperty("ScaledSurface", new IPropertyEntry[0]);
        }

        public double Fit(IEnumerable<GeoPoint> toPoints)
        {
            throw new NotImplementedException();
        }

        public bool IsRotated(Axis rotationAxis)
        {
            return original.IsRotated(rotationAxis);
        }
    }
}
