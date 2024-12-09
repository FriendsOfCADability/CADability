using System;
using System.Collections;
using System.Collections.Generic;
using CADability.GeoObject;
using MathNet.Numerics.Optimization;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using MathNet.Numerics.LinearAlgebra.Double;
using CADability.Curve2D;
using MathNet.Numerics.Optimization.TrustRegion;
using MathNet.Numerics;
using Microsoft.Win32.SafeHandles;

namespace CADability
{
    static public partial class BoxedSurfaceExtension
    {
        static private double quad(double x) { return x * x * x * x; }
        static private double cube(double x) { return x * x * x; }
        static double sqr(double d) { return d * d; }
        static double exp32(double d) { return Math.Sqrt(d * d * d); }
        /// <summary>
        /// Find the (u,v)-position for the provided point on a surface. Using the NewtonMinimizer from MathNet. Faster than LevenbergMarquardtMinimizer. Surfaces need second derivatives.
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="p3d"></param>
        /// <param name="res"></param>
        /// <param name="mindist"></param>
        /// <returns></returns>
        public static bool PositionOfMN(ISurface surface, GeoPoint p3d, ref GeoPoint2D res, out double mindist)
        {
            NewtonMinimizer nm = new NewtonMinimizer(1e-12, 30);
            IObjectiveFunction iof = ObjectiveFunction.GradientHessian(
                new Func<Vector<double>, (double, Vector<double>, Matrix<double>)>(delegate (Vector<double> vd)
                {
                    GeoPoint2D uv = new GeoPoint2D(vd[0], vd[1]);
                    surface.Derivation2At(uv, out GeoPoint loc, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv);
                    double val = (p3d.x - loc.x) * (p3d.x - loc.x) + (p3d.y - loc.y) * (p3d.y - loc.y) + (p3d.z - loc.z) * (p3d.z - loc.z);
                    double u = -2 * du.x * (p3d.x - loc.x) - 2 * du.y * (p3d.y - loc.y) - 2 * du.z * (p3d.z - loc.z);
                    double v = -2 * dv.x * (p3d.x - loc.x) - 2 * dv.y * (p3d.y - loc.y) - 2 * dv.z * (p3d.z - loc.z);
                    Vector<double> gradient = new DenseVector(new double[] { u, v });
                    Matrix<double> hessian = new DenseMatrix(2, 2);
                    hessian[0, 0] = -2 * duu.z * (p3d.z - loc.z) - 2 * duu.y * (p3d.y - loc.y) - 2 * duu.x * (p3d.x - loc.x) + 2 * du.z * du.z + 2 * du.y * du.y + 2 * du.x * du.x;
                    hessian[1, 1] = -2 * dvv.z * (p3d.z - loc.z) - 2 * dvv.y * (p3d.y - loc.y) - 2 * dvv.x * (p3d.x - loc.x) + 2 * dv.z * dv.z + 2 * dv.y * dv.y + 2 * dv.x * dv.x;
                    hessian[0, 1] = hessian[1, 0] = -2 * duv.z * (p3d.z - loc.z) - 2 * duv.y * (p3d.y - loc.y) - 2 * duv.x * (p3d.x - loc.x) + 2 * du.z * dv.z + 2 * du.y * dv.y + 2 * du.x * dv.x;
                    return (val, gradient, hessian);
                }));
            try
            {
                MinimizationResult mres = nm.FindMinimum(iof, new DenseVector(new double[] { res.x, res.y }));
                res = new GeoPoint2D(mres.MinimizingPoint[0], mres.MinimizingPoint[1]);
                mindist = mres.FunctionInfoAtMinimum.Value;
                return true;
            }
            catch
            {
                res = GeoPoint2D.Origin;
                mindist = double.MaxValue;
                return false;
            }
        }
        /// <summary>
        /// Find the (u,v)-position for the provided point on a surface. Using the LevenbergMarquardtMinimizer from MathNet. Slower than the NewtonMinimizer but working with first tests.
        /// It is said that it is more robust.
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="p3d"></param>
        /// <param name="res"></param>
        /// <param name="mindist"></param>
        /// <returns></returns>
        public static bool PositionOfLM(ISurface surface, GeoPoint p3d, ref GeoPoint2D res, out double mindist)
        {
            Vector<double> observedX = new DenseVector(3); // there is no need to set values, index 0 is for x, 1 for y and 2 for z
            Vector<double> observedY = new DenseVector(new double[] { p3d.x, p3d.y, p3d.z }); // this is the data we want to achieve
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer();
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    GeoPoint p = surface.PointAt(new GeoPoint2D(vd[0], vd[1]));
                    return new DenseVector(new double[] { p.x, p.y, p.z });
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {   // these are the derivations for PointAt(uv)-p3d in x, y and z
                    GeoPoint2D uv = new GeoPoint2D(vd[0], vd[1]);
                    surface.DerivationAt(uv, out GeoPoint loc, out GeoVector du, out GeoVector dv);
                    var prime = new DenseMatrix(3, 2);
                    prime[0, 0] = du.x;
                    prime[0, 1] = dv.x;
                    prime[1, 0] = du.y;
                    prime[1, 1] = dv.y;
                    prime[2, 0] = du.z;
                    prime[2, 1] = dv.z;
                    return prime;
                }), observedX, observedY);
            try
            {
                NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { res.x, res.y }));
                res = new GeoPoint2D(mres.MinimizingPoint[0], mres.MinimizingPoint[1]);
                mindist = (p3d.x - mres.MinimizedValues[0]) * (p3d.x - mres.MinimizedValues[0]) + (p3d.y - mres.MinimizedValues[1]) * (p3d.y - mres.MinimizedValues[1]) + (p3d.z - mres.MinimizedValues[2]) * (p3d.z - mres.MinimizedValues[2]);
                return true;
            }
            catch
            {
                res = GeoPoint2D.Origin;
                mindist = double.MaxValue;
                return false;
            }
        }
        public static void Test()
        {
        }
        public static bool CurveIntersection2D(ICurve2D curve1, double spar1, double epar1, ICurve2D curve2, double spar2, double epar2, ref double par1, ref double par2, ref GeoPoint2D ip)
        {
            NewtonMinimizer nm = new NewtonMinimizer(1e-30, 30);
            IObjectiveFunction iof = ObjectiveFunction.GradientHessian(
                new Func<Vector<double>, (double, Vector<double>, Matrix<double>)>(delegate (Vector<double> vd)
                {
                    double u = vd[0]; // parameter on first curve
                    double v = vd[1]; // parameter on second curve
                    curve1.TryPointDeriv2At(u, out GeoPoint2D f, out GeoVector2D fu, out GeoVector2D fuu);
                    curve2.TryPointDeriv2At(v, out GeoPoint2D g, out GeoVector2D gv, out GeoVector2D gvv);
                    double val = f & g; // the squared distance between the two points, which newton has to minimize
                    double du = 2 * fu.x * (f.x - g.x) + 2 * fu.y * (f.y - g.y);
                    double dv = 2 * gv.x * (f.x - g.x) + 2 * gv.y * (f.y - g.y);
                    Vector<double> gradient = new DenseVector(new double[] { du, dv });
                    Matrix<double> hessian = new DenseMatrix(2, 2);
                    hessian[0, 0] = 2 * fuu.x * (f.x - g.x) + 2 * fuu.y * (f.y - g.y) + 2 * fu.x * fu.x + 2 * fu.y * fu.y;
                    hessian[1, 1] = 2 * gvv.x * (f.x - g.x) + 2 * gvv.y * (f.y - g.y) + 2 * gv.x * gv.x + 2 * gv.y * gv.y;
                    hessian[0, 1] = hessian[1, 0] = -2 * fu.x * gv.x - 2 * fu.y * gv.y;
                    return (val, gradient, hessian);
                }));
            try
            {
                MinimizationResult mres = nm.FindMinimum(iof, new DenseVector(new double[] { (spar1 + epar1) / 2, (spar2 + epar2) / 2 }));
                par1 = mres.MinimizingPoint[0];
                par2 = mres.MinimizingPoint[1];
                ip = new GeoPoint2D(curve1.PointAt(par1), curve2.PointAt(par2));
                // mres.FunctionInfoAtMinimum.Value;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static bool CurveSurfaceIntersection(ISurface surface, ICurve curve, ref GeoPoint2D uvOnSurface, ref double uOnCurve, out GeoPoint ip)
        {
            NewtonMinimizer nm = new NewtonMinimizer(1e-12, 30, true);
            GeoPoint lastIp = GeoPoint.Origin;
            IObjectiveFunction iof = ObjectiveFunction.GradientHessian(
                new Func<Vector<double>, (double, Vector<double>, Matrix<double>)>(delegate (Vector<double> vd)
                {
                    GeoPoint2D uv = new GeoPoint2D(vd[0], vd[1]); // parameter on surface
                    surface.Derivation2At(uv, out GeoPoint s, out GeoVector su, out GeoVector sv, out GeoVector suu, out GeoVector svv, out GeoVector suv);
                    double t = vd[2]; // parameter on curve
                    curve.TryPointDeriv2At(t, out GeoPoint c, out GeoVector ct, out GeoVector ctt);
                    double val = c & s; // the squared distance between the two points, which newton has to minimize
                    lastIp = new GeoPoint(c, s); // remember last intersection point
                    Vector<double> gradient = new DenseVector(new double[] {
                        2 * (s.x - c.x) * su.x + 2 * (s.y - c.y) * su.y + 2 * (s.z - c.z) * su.z,
                        2 * (s.x - c.x) * sv.x + 2 * (s.y - c.y) * sv.y + 2 * (s.z - c.z) * sv.z,
                        -2 * (s.x - c.x) * ct.x - 2 * (s.y - c.y) * ct.y - 2 * (s.z - c.z) * ct.z
                    });
                    Matrix<double> hessian = new DenseMatrix(3, 3);
                    hessian[0, 0] = 2 * (s.x - c.x) * suu.x + 2 * su.x * su.x + 2 * (s.y - c.y) * suu.y + 2 * su.y * su.y + 2 * (s.z - c.z) * suu.z + 2 * su.z * su.z;
                    hessian[1, 1] = 2 * (s.x - c.x) * svv.x + 2 * sv.x * sv.x + 2 * (s.y - c.y) * svv.y + 2 * sv.y * sv.y + 2 * (s.z - c.z) * svv.z + 2 * sv.z * sv.z;
                    hessian[2, 2] = 2 * ct.x * ct.x - 2 * ctt.x * (s.x - c.x) + 2 * ct.y * ct.y - 2 * ctt.y * (s.y - c.y) + 2 * ct.z * ct.z - 2 * ctt.z * (s.z - c.z);
                    hessian[0, 1] = hessian[1, 0] = 2 * su.x * sv.x + 2 * (s.x - c.x) * suv.x + 2 * su.y * sv.y + 2 * (s.y - c.y) * suv.y + 2 * su.z * sv.z + 2 * (s.z - c.z) * suv.z;
                    hessian[0, 2] = hessian[2, 0] = -2 * ct.x * su.x - 2 * ct.y * su.y - 2 * ct.z * su.z;
                    hessian[1, 2] = hessian[2, 1] = -2 * ct.x * sv.x - 2 * ct.y * sv.y - 2 * ct.z * sv.z;
                    return (val, gradient, hessian);
                }));
            try
            {
                MinimizationResult mres = nm.FindMinimum(iof, new DenseVector(new double[] { uvOnSurface.x, uvOnSurface.y, uOnCurve }));
                uvOnSurface = new GeoPoint2D(mres.MinimizingPoint[0], mres.MinimizingPoint[1]);
                uOnCurve = mres.MinimizingPoint[2];
                ip = lastIp;
                return true;
            }
            catch (Exception)
            {
                ip = GeoPoint.Origin;
                return false;
            }
        }

        public static bool CurveSurfaceIntersection(ISurface surface, ICurve curve, BoundingRect surfaceBounds, double uOnCurveMin, double uOnCurveMax, ref GeoPoint2D uvOnSurface, ref double uOnCurve, out GeoPoint ip)
        {
            Vector<double> observedX = new DenseVector(3); // there is no need to set values, index 0 is for x, 1 for y and 2 for z
            Vector<double> observedY = new DenseVector(new double[] { 0, 0, 0 }); // this is the data we want to achieve
            GeoPoint lastip = GeoPoint.Origin;
            TrustRegionNewtonCGMinimizer trm = new TrustRegionNewtonCGMinimizer();
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    GeoPoint2D uv = new GeoPoint2D(vd[0], vd[1]); // parameter on surface
                    double t = vd[2]; // parameter on curve
                    GeoPoint c = curve.PointAt(t);
                    GeoPoint p = surface.PointAt(new GeoPoint2D(vd[0], vd[1]));
                    lastip = new GeoPoint(c, p);
                    return new DenseVector(new double[] { p.x - c.x, p.y - c.y, p.z - c.z });
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {   // these are the derivations for PointAt(uv)-p3d in x, y and z
                    GeoPoint2D uv = new GeoPoint2D(vd[0], vd[1]);
                    surface.DerivationAt(uv, out GeoPoint loc, out GeoVector du, out GeoVector dv);
                    double t = vd[2]; // parameter on curve
                    GeoVector dt = curve.DirectionAt(t);
                    var prime = new DenseMatrix(3, 3);
                    prime[0, 0] = du.x;
                    prime[0, 1] = dv.x;
                    prime[0, 2] = -dt.x;
                    prime[1, 0] = du.y;
                    prime[1, 1] = dv.y;
                    prime[1, 2] = -dt.y;
                    prime[2, 0] = du.z;
                    prime[2, 1] = dv.z;
                    prime[2, 2] = -dt.z;
                    return prime;
                }), observedX, observedY);
            try
            {
                NonlinearMinimizationResult mres = trm.FindMinimum(iom, new double[] { uvOnSurface.x, uvOnSurface.y, uOnCurve }, new double[] { surfaceBounds.Left, surfaceBounds.Bottom, uOnCurveMin }, new double[] { surfaceBounds.Right, surfaceBounds.Top, uOnCurveMax });
                if (mres.ReasonForExit == ExitCondition.Converged)
                {
                    uvOnSurface = new GeoPoint2D(mres.MinimizingPoint[0], mres.MinimizingPoint[1]);
                    uOnCurve = mres.MinimizingPoint[2];
                    ip = lastip;
                    return true;
                }
                else
                {
                    ip = lastip;
                    return false;
                }
            }
            catch
            {
                ip = lastip;
                return false;
            }
        }
        public static bool CurveSurfaceIntersectionDL(ISurface surface, ICurve curve, BoundingRect surfaceBounds, double uOnCurveMin, double uOnCurveMax, ref GeoPoint2D uvOnSurface, ref double uOnCurve, out GeoPoint ip)
        {
            Vector<double> observedX = new DenseVector(3); // there is no need to set values, index 0 is for x, 1 for y and 2 for z
            Vector<double> observedY = new DenseVector(new double[] { 0, 0, 0 }); // this is the data we want to achieve
            GeoPoint lastip = GeoPoint.Origin;
            TrustRegionDogLegMinimizer trm = new TrustRegionDogLegMinimizer();
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    GeoPoint2D uv = new GeoPoint2D(vd[0], vd[1]); // parameter on surface
                    double t = vd[2]; // parameter on curve
                    GeoPoint c = curve.PointAt(t);
                    GeoPoint p = surface.PointAt(new GeoPoint2D(vd[0], vd[1]));
                    lastip = new GeoPoint(c, p);
                    return new DenseVector(new double[] { p.x - c.x, p.y - c.y, p.z - c.z });
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {   // these are the derivations for PointAt(uv)-p3d in x, y and z
                    GeoPoint2D uv = new GeoPoint2D(vd[0], vd[1]);
                    surface.DerivationAt(uv, out GeoPoint loc, out GeoVector du, out GeoVector dv);
                    double t = vd[2]; // parameter on curve
                    GeoVector dt = curve.DirectionAt(t);
                    var prime = new DenseMatrix(3, 3);
                    prime[0, 0] = du.x;
                    prime[0, 1] = dv.x;
                    prime[0, 2] = -dt.x;
                    prime[1, 0] = du.y;
                    prime[1, 1] = dv.y;
                    prime[1, 2] = -dt.y;
                    prime[2, 0] = du.z;
                    prime[2, 1] = dv.z;
                    prime[2, 2] = -dt.z;
                    return prime;
                }), observedX, observedY);
            try
            {
                NonlinearMinimizationResult mres = trm.FindMinimum(iom, new double[] { uvOnSurface.x, uvOnSurface.y, uOnCurve }, new double[] { surfaceBounds.Left, surfaceBounds.Bottom, uOnCurveMin }, new double[] { surfaceBounds.Right, surfaceBounds.Top, uOnCurveMax });
                if (mres.ReasonForExit == ExitCondition.Converged)
                {
                    uvOnSurface = new GeoPoint2D(mres.MinimizingPoint[0], mres.MinimizingPoint[1]);
                    uOnCurve = mres.MinimizingPoint[2];
                    ip = lastip;
                    return true;
                }
                else
                {
                    ip = lastip;
                    return false;
                }
            }
            catch
            {
                ip = lastip;
                return false;
            }
        }

        public static bool CurveSurfaceIntersectionLM(ISurface surface, ICurve curve, ref GeoPoint2D uvOnSurface, ref double uOnCurve, out GeoPoint ip)
        {
            Vector<double> observedX = new DenseVector(3); // there is no need to set values, index 0 is for x, 1 for y and 2 for z
            Vector<double> observedY = new DenseVector(new double[] { 0, 0, 0 }); // this is the data we want to achieve
            GeoPoint lastip = GeoPoint.Origin;
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(maximumIterations: 10);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    GeoPoint2D uv = new GeoPoint2D(vd[0], vd[1]); // parameter on surface
                    double t = vd[2]; // parameter on curve
                    GeoPoint c = curve.PointAt(t);
                    GeoPoint p = surface.PointAt(new GeoPoint2D(vd[0], vd[1]));
                    lastip = new GeoPoint(c, p);
                    return new DenseVector(new double[] { p.x - c.x, p.y - c.y, p.z - c.z });
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {   // these are the derivations for PointAt(uv)-p3d in x, y and z
                    GeoPoint2D uv = new GeoPoint2D(vd[0], vd[1]);
                    surface.DerivationAt(uv, out GeoPoint loc, out GeoVector du, out GeoVector dv);
                    double t = vd[2]; // parameter on curve
                    GeoVector dt = curve.DirectionAt(t);
                    var prime = new DenseMatrix(3, 3);
                    prime[0, 0] = du.x;
                    prime[0, 1] = dv.x;
                    prime[0, 2] = -dt.x;
                    prime[1, 0] = du.y;
                    prime[1, 1] = dv.y;
                    prime[1, 2] = -dt.y;
                    prime[2, 0] = du.z;
                    prime[2, 1] = dv.z;
                    prime[2, 2] = -dt.z;
                    return prime;
                }), observedX, observedY);
            try
            {
                NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { uvOnSurface.x, uvOnSurface.y, uOnCurve }));
                uvOnSurface = new GeoPoint2D(mres.MinimizingPoint[0], mres.MinimizingPoint[1]);
                uOnCurve = mres.MinimizingPoint[2];
                ip = lastip;
                return true;
            }
            catch
            {
                ip = lastip;
                return false;
            }
        }

        public static bool SurfacesIntersectionLM(ISurface surface1, ISurface surface2, ISurface surface3, ref GeoPoint2D uv1, ref GeoPoint2D uv2, ref GeoPoint2D uv3, ref GeoPoint ip)
        {
            //Polynom implicitSurface1 = null, implicitSurface2 = null, implicitSurface3 = null;
            //if (surface1 is ISurfaceImpl si1) implicitSurface1 = si1.GetImplicitPolynomial();
            //if (surface2 is ISurfaceImpl si2) implicitSurface2 = si2.GetImplicitPolynomial();
            //if (surface3 is ISurfaceImpl si3) implicitSurface3 = si3.GetImplicitPolynomial();
            //List<ISurface> notimpl = new List<ISurface>();
            //notimpl.Add(surface1); notimpl.Add(surface2); notimpl.Add(surface3);
            //List<Polynom> polynoms = new List<Polynom>();
            //List<GeoPoint2D> luv = new List<GeoPoint2D>(new GeoPoint2D[] { uv1, uv2, uv3 });
            //if (implicitSurface3 != null)
            //{
            //    notimpl.RemoveAt(2);
            //    luv.RemoveAt(2);
            //    polynoms.Add(implicitSurface3);
            //}
            //if (implicitSurface2 != null)
            //{
            //    notimpl.RemoveAt(1);
            //    luv.RemoveAt(1);
            //    polynoms.Add(implicitSurface2);
            //}
            //if (implicitSurface1 != null)
            //{
            //    notimpl.RemoveAt(0);
            //    luv.RemoveAt(0);
            //    polynoms.Add(implicitSurface1);
            //}
            //if (polynoms.Count == 3)
            //{
            //    if (SurfacesIntersectionLM(polynoms[0], polynoms[1], polynoms[2], ref ip))
            //    {
            //        uv1 = surface1.PositionOf(ip);
            //        uv2 = surface2.PositionOf(ip);
            //        uv3 = surface3.PositionOf(ip);
            //        return true;
            //    }
            //}
            Vector<double> observedX = new DenseVector(9); // there is no need to set values
            Vector<double> observedY = new DenseVector(new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 }); // this is the data we want to achieve
            GeoPoint lastip = GeoPoint.Origin;
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(maximumIterations: 10);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    GeoPoint p1 = surface1.PointAt(new GeoPoint2D(vd[0], vd[1]));
                    GeoPoint p2 = surface2.PointAt(new GeoPoint2D(vd[2], vd[3]));
                    GeoPoint p3 = surface3.PointAt(new GeoPoint2D(vd[4], vd[5]));
                    return new DenseVector(new double[] { p1.x - p2.x, p1.y - p2.y, p1.z - p2.z, p2.x - p3.x, p2.y - p3.y, p2.z - p3.z, p3.x - p1.x, p3.y - p1.y, p3.z - p1.z });
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {   // these are the derivations for p1-p2, p2-3 and p3-p1 in x, y and z
                    surface1.DerivationAt(new GeoPoint2D(vd[0], vd[1]), out GeoPoint loc1, out GeoVector du1, out GeoVector dv1);
                    surface2.DerivationAt(new GeoPoint2D(vd[2], vd[3]), out GeoPoint loc2, out GeoVector du2, out GeoVector dv2);
                    surface3.DerivationAt(new GeoPoint2D(vd[4], vd[5]), out GeoPoint loc3, out GeoVector du3, out GeoVector dv3);
                    var prime = new DenseMatrix(9, 6);
                    prime[0, 0] = du1.x;
                    prime[0, 1] = dv1.x;
                    prime[0, 2] = -du2.x;
                    prime[0, 3] = -dv2.x;
                    prime[0, 4] = 0;
                    prime[0, 5] = 0;
                    prime[1, 0] = du1.y;
                    prime[1, 1] = dv1.y;
                    prime[1, 2] = -du2.y;
                    prime[1, 3] = -dv2.y;
                    prime[1, 4] = 0;
                    prime[1, 5] = 0;
                    prime[2, 0] = du1.z;
                    prime[2, 1] = dv1.z;
                    prime[2, 2] = -du2.z;
                    prime[2, 3] = -dv2.z;
                    prime[2, 4] = 0;
                    prime[2, 5] = 0;
                    prime[3, 0] = 0;
                    prime[3, 1] = 0;
                    prime[3, 2] = du2.x;
                    prime[3, 3] = dv2.x;
                    prime[3, 4] = -du3.x;
                    prime[3, 5] = -dv3.x;
                    prime[4, 0] = 0;
                    prime[4, 1] = 0;
                    prime[4, 2] = du2.y;
                    prime[4, 3] = dv2.y;
                    prime[4, 4] = -du3.y;
                    prime[4, 5] = -dv3.y;
                    prime[5, 0] = 0;
                    prime[5, 1] = 0;
                    prime[5, 2] = du2.z;
                    prime[5, 3] = dv2.z;
                    prime[5, 4] = -du3.z;
                    prime[5, 5] = -dv3.z;
                    prime[6, 0] = -du1.x;
                    prime[6, 1] = -dv1.x;
                    prime[6, 2] = 0;
                    prime[6, 3] = 0;
                    prime[6, 4] = du3.x;
                    prime[6, 5] = dv3.x;
                    prime[7, 0] = -du1.y;
                    prime[7, 1] = -dv1.y;
                    prime[7, 2] = 0;
                    prime[7, 3] = 0;
                    prime[7, 4] = du3.y;
                    prime[7, 5] = dv3.y;
                    prime[8, 0] = -du1.z;
                    prime[8, 1] = -dv1.z;
                    prime[8, 2] = 0;
                    prime[8, 3] = 0;
                    prime[8, 4] = du3.z;
                    prime[8, 5] = dv3.z;
                    return prime;
                }), observedX, observedY);
            try
            {
                NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { uv1.x, uv1.y, uv2.x, uv2.y, uv3.x, uv3.y }));
                uv1 = new GeoPoint2D(mres.MinimizingPoint[0], mres.MinimizingPoint[1]);
                uv2 = new GeoPoint2D(mres.MinimizingPoint[2], mres.MinimizingPoint[3]);
                uv3 = new GeoPoint2D(mres.MinimizingPoint[4], mres.MinimizingPoint[5]);
                ip = new GeoPoint(surface1.PointAt(uv1), surface2.PointAt(uv2), surface3.PointAt(uv3));
                return true;
            }
            catch
            {
                ip = GeoPoint.Origin;
                return false;
            }
        }
        public static bool SurfacesIntersectionLM(Polynom surface1, Polynom surface2, Polynom surface3, ref GeoPoint ip)
        {
            Vector<double> observedX = new DenseVector(3); // there is no need to set values
            Vector<double> observedY = new DenseVector(new double[] { 0, 0, 0 }); // this is the data we want to achieve
            Polynom[,] derivatives = new Polynom[3, 3];
            derivatives[0, 0] = surface1.Derivate(1, 0, 0);
            derivatives[0, 1] = surface1.Derivate(0, 1, 0);
            derivatives[0, 2] = surface1.Derivate(0, 0, 1);
            derivatives[1, 0] = surface2.Derivate(1, 0, 0);
            derivatives[1, 1] = surface2.Derivate(0, 1, 0);
            derivatives[1, 2] = surface2.Derivate(0, 0, 1);
            derivatives[2, 0] = surface3.Derivate(1, 0, 0);
            derivatives[2, 1] = surface3.Derivate(0, 1, 0);
            derivatives[2, 2] = surface3.Derivate(0, 0, 1);
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(maximumIterations: 10);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    double d1 = surface1.Eval(vd[0], vd[1], vd[2]);
                    double d2 = surface2.Eval(vd[0], vd[1], vd[2]);
                    double d3 = surface3.Eval(vd[0], vd[1], vd[2]);
                    return new DenseVector(new double[] { d1, d2, d3 });
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {
                    var prime = new DenseMatrix(3, 3);
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            prime[i, j] = derivatives[i, j].Eval(vd[0], vd[1], vd[2]);
                        }
                    }
                    return prime;
                }), observedX, observedY);
            try
            {
                NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { ip.x, ip.y, ip.z }));
                ip = new GeoPoint(mres.MinimizingPoint[0], mres.MinimizingPoint[1], mres.MinimizingPoint[2]);
                return true;
            }
            catch
            {
                ip = GeoPoint.Origin;
                return false;
            }
        }
        /// <summary>
        /// Fit a plane through a number of points. This is working, but has a bad performance. Better use Plane.FromPoints
        /// </summary>
        /// <param name="points"></param>
        /// <param name="location"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public static bool PlaneFit(IEnumerable<GeoPoint> points, out GeoPoint location, out GeoVector normal)
        {
            GeoPoint[] pnts = null;
            if (points is GeoPoint[]) pnts = (GeoPoint[])(points as GeoPoint[]).Clone();
            else
            {
                List<GeoPoint> lpnts = null;
                if (points is List<GeoPoint>) lpnts = points as List<GeoPoint>;
                else
                {
                    lpnts = new List<GeoPoint>();
                    foreach (GeoPoint point in points)
                    {
                        lpnts.Add(point);
                    }
                }
                pnts = lpnts.ToArray(); // GeoPoint is a struct, this hopefully clones the GeoPoints
            }
            Vector<double> observedX = new DenseVector(pnts.Length); // there is no need to set values
            Vector<double> observedY = new DenseVector(pnts.Length); // this is the data we want to achieve, namely 0.0
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-8, maximumIterations: 20);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    DenseVector res = new DenseVector(pnts.Length);
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        res[i] = (vd[0] * pnts[i].x + vd[1] * pnts[i].y + vd[2] * pnts[i].z) / Math.Sqrt(vd[0] * vd[0] + vd[1] * vd[1] + vd[2] * vd[2]) - Math.Sqrt(vd[0] * vd[0] + vd[1] * vd[1] + vd[2] * vd[2]);
                    }
#if DEBUG
                    double err = 0.0;
                    for (int i = 0; i < pnts.Length; i++) err += res[i] * res[i];
#endif
                    return res;
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {
                    var prime = new DenseMatrix(pnts.Length, 3);
                    double l = vd[0] * vd[0] + vd[1] * vd[1] + vd[2] * vd[2];
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        prime[i, 0] = -vd[0] * (vd[0] * pnts[i].x + vd[1] * pnts[i].y + vd[2] * pnts[i].z) / Math.Sqrt(l * l * l) + (pnts[i].x - vd[0]) / Math.Sqrt(l);
                        prime[i, 1] = -vd[1] * (vd[0] * pnts[i].x + vd[1] * pnts[i].y + vd[2] * pnts[i].z) / Math.Sqrt(l * l * l) + (pnts[i].y - vd[1]) / Math.Sqrt(l);
                        prime[i, 2] = -vd[2] * (vd[0] * pnts[i].x + vd[1] * pnts[i].y + vd[2] * pnts[i].z) / Math.Sqrt(l * l * l) + (pnts[i].z - vd[2]) / Math.Sqrt(l);
                    }
                    return prime;
                }), observedX, observedY);

            try
            {
                DenseVector sparams = new DenseVector(3);
                GeoPoint org = GeoPoint.Origin;
                GeoVector norm = GeoVector.NullVector;
                bool found = false;
                for (int i = 0; i < pnts.Length; i++)
                {
                    for (int j = i + 1; j < pnts.Length; j++)
                    {
                        for (int k = j + 1; k < pnts.Length; k++)
                        {
                            GeoVector dirx = pnts[j] - pnts[i];
                            GeoVector diry = pnts[k] - pnts[i];
                            GeoVector n = dirx ^ diry;
                            if (!n.IsNullVector())
                            {
                                Plane pln = new Plane(pnts[i], dirx, diry);
                                double d = pln.Distance(GeoPoint.Origin);
                                norm = -d * n.Normalized;
                                sparams[0] = norm.x;
                                sparams[1] = norm.y;
                                sparams[2] = norm.z;
                                org = pnts[i];
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }
                    if (found) break;
                }
                GeoVector offset = new GeoVector(norm.x - org.x, norm.y - org.y, norm.z - org.z);
                for (int i = 0; i < pnts.Length; i++) pnts[i] += offset;
                NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { norm.x, norm.y, norm.z }));
                if (mres.ReasonForExit == ExitCondition.Converged || mres.ReasonForExit == ExitCondition.RelativeGradient)
                {
                    normal = new GeoVector(mres.MinimizingPoint[0], mres.MinimizingPoint[1], mres.MinimizingPoint[2]);
                    location = GeoPoint.Origin + normal - offset;
                    Plane dbg = new Plane(location, normal);
                    double error = 0.0;
                    foreach (GeoPoint point in points) error += dbg.Distance(point);

                    return true;
                }
            }
            catch (Exception)
            {
            }
            normal = GeoVector.NullVector;
            location = GeoPoint.Origin;
            return false;
        }
        public static double LineFit(IEnumerable<GeoPoint> points, double precision, out GeoPoint location, out GeoVector direction)
        {
            GeoPoint[] pnts = points.ToArray();
            Vector<double> observedX = new DenseVector(pnts.Length); // there is no need to set values
            Vector<double> observedY = new DenseVector(pnts.Length); // this is the data we want to achieve, namely 0.0
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-15, maximumIterations: 20);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    //// v = d ^ (l - p);
                    //// v.Length/d.Length is the distance of point p to the line (l,d)
                    //// sqrt((dy*(lz-pz)-dz*(ly-py))² + (...)² + (...)²) / sqrt(dx*dx+dy*dy+dz*dz) the distance
                    //// error: dy*(lz-pz)-dz*(ly-py))² + (...)² + (...)² / (dx*dx+dy*dy+dz*dz) the sqaure of the distance

                    DenseVector res = new DenseVector(pnts.Length);
                    double lx = vd[0];
                    double ly = vd[1];
                    double lz = vd[2];
                    double dx = vd[3];
                    double dy = vd[4];
                    double dz = vd[5];
                    double dl = dx * dx + dy * dy + dz * dz;
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        double px = pnts[i].x;
                        double py = pnts[i].y;
                        double pz = pnts[i].z;
                        res[i] = (sqr(dy * (lz - pz) - dz * (ly - py)) + sqr(dx * (lz - pz) - dz * (lx - px)) + sqr(dx * (ly - py) - dy * (lx - px))) / (dl);
                    }
#if DEBUG
                    double err = 0.0;
                    for (int i = 0; i < pnts.Length; i++) err += res[i] * res[i];
#endif
                    return res;
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {
                    //// derivations of the (square of the) distance by lx,ly,lz,dx,dy,dz
                    ////((-2 * dz * (dx * (lz - pz) - dz * (lx - px))) - 2 * dy * (dx * (ly - py) - dy * (lx - px))) / (dz ^ 2 + dy ^ 2 + dx ^ 2);
                    ////(2 * dx * (dx * (ly - py) - dy * (lx - px)) - 2 * dz * (dy * (lz - pz) - dz * (ly - py))) / (dz ^ 2 + dy ^ 2 + dx ^ 2);
                    ////(2 * dy * (dy * (lz - pz) - dz * (ly - py)) + 2 * dx * (dx * (lz - pz) - dz * (lx - px))) / (dz ^ 2 + dy ^ 2 + dx ^ 2);
                    ////(2 * (dx * (lz - pz) - dz * (lx - px)) * (lz - pz) + 2 * (dx * (ly - py) - dy * (lx - px)) * (ly - py)) / (dz ^ 2 + dy ^ 2 + dx ^ 2) - (2 * dx * ((dy * (lz - pz) - dz * (ly - py)) ^ 2 + (dx * (lz - pz) - dz * (lx - px)) ^ 2 + (dx * (ly - py) - dy * (lx - px)) ^ 2)) / (dz ^ 2 + dy ^ 2 + dx ^ 2) ^ 2;
                    ////(2 * (dy * (lz - pz) - dz * (ly - py)) * (lz - pz) + 2 * (px - lx) * (dx * (ly - py) - dy * (lx - px))) / (dz ^ 2 + dy ^ 2 + dx ^ 2) - (2 * dy * ((dy * (lz - pz) - dz * (ly - py)) ^ 2 + (dx * (lz - pz) - dz * (lx - px)) ^ 2 + (dx * (ly - py) - dy * (lx - px)) ^ 2)) / (dz ^ 2 + dy ^ 2 + dx ^ 2) ^ 2;
                    ////(2 * (py - ly) * (dy * (lz - pz) - dz * (ly - py)) + 2 * (px - lx) * (dx * (lz - pz) - dz * (lx - px))) / (dz ^ 2 + dy ^ 2 + dx ^ 2) - (2 * dz * ((dy * (lz - pz) - dz * (ly - py)) ^ 2 + (dx * (lz - pz) - dz * (lx - px)) ^ 2 + (dx * (ly - py) - dy * (lx - px)) ^ 2)) / (dz ^ 2 + dy ^ 2 + dx ^ 2) ^ 2;

                    var prime = new DenseMatrix(pnts.Length, 6);
                    double lx = vd[0];
                    double ly = vd[1];
                    double lz = vd[2];
                    double dx = vd[3];
                    double dy = vd[4];
                    double dz = vd[5];
                    double dl = dx * dx + dy * dy + dz * dz;

                    for (int i = 0; i < pnts.Length; i++)
                    {
                        double px = pnts[i].x;
                        double py = pnts[i].y;
                        double pz = pnts[i].z;
                        prime[i, 0] = ((-2 * dz * (dx * (lz - pz) - dz * (lx - px))) - 2 * dy * (dx * (ly - py) - dy * (lx - px))) / dl;
                        prime[i, 1] = (2 * dx * (dx * (ly - py) - dy * (lx - px)) - 2 * dz * (dy * (lz - pz) - dz * (ly - py))) / dl;
                        prime[i, 2] = (2 * dy * (dy * (lz - pz) - dz * (ly - py)) + 2 * dx * (dx * (lz - pz) - dz * (lx - px))) / dl;
                        prime[i, 3] = (2 * (dx * (lz - pz) - dz * (lx - px)) * (lz - pz) + 2 * (dx * (ly - py) - dy * (lx - px)) * (ly - py)) / (dl) - (2 * dx * (sqr(dy * (lz - pz) - dz * (ly - py)) + sqr(dx * (lz - pz) - dz * (lx - px)) + sqr(dx * (ly - py) - dy * (lx - px)))) / sqr(dl);
                        prime[i, 4] = (2 * (dy * (lz - pz) - dz * (ly - py)) * (lz - pz) + 2 * (px - lx) * (dx * (ly - py) - dy * (lx - px))) / (dl) - (2 * dy * (sqr(dy * (lz - pz) - dz * (ly - py)) + sqr(dx * (lz - pz) - dz * (lx - px)) + sqr(dx * (ly - py) - dy * (lx - px)))) / sqr(dl);
                        prime[i, 5] = (2 * (py - ly) * (dy * (lz - pz) - dz * (ly - py)) + 2 * (px - lx) * (dx * (lz - pz) - dz * (lx - px))) / (dl) - (2 * dz * (sqr(dy * (lz - pz) - dz * (ly - py)) + sqr(dx * (lz - pz) - dz * (lx - px)) + sqr(dx * (ly - py) - dy * (lx - px)))) / sqr(dl);
                    }
                    return prime;
                }), observedX, observedY);
            GeoVector d = pnts[1] - pnts[0];
            double tl = d.TaxicabLength;
            for (int i = 2; i < pnts.Length; i++)
            {
                GeoVector di = pnts[i] - pnts[0];
                double li = di.TaxicabLength;
                if (li > tl)
                {
                    tl = li;
                    d = di;
                }
            }
            NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { pnts[0].x, pnts[0].y, pnts[0].z, d.x, d.y, d.z }));
            if (mres.ReasonForExit == ExitCondition.Converged || mres.ReasonForExit == ExitCondition.RelativeGradient)
            {
                location = new GeoPoint(mres.MinimizingPoint[0], mres.MinimizingPoint[1], mres.MinimizingPoint[2]);
                direction = new GeoVector(mres.MinimizingPoint[3], mres.MinimizingPoint[4], mres.MinimizingPoint[5]);
                double error = 0.0;
                foreach (GeoPoint point in points) error += Geometry.DistPL(point, location, direction);

                return error;
            }
            location = GeoPoint.Origin;
            direction = GeoVector.NullVector;
            return double.MaxValue;

        }
        public static double ConeFit(IArray<GeoPoint> points, GeoPoint apex, GeoVector axis, double theta, double precision, out ConicalSurface cs)
        {
            // according to wikipedia (a cone with apex=(0,0,0), d: axis and theta: opening angle
            //F(u) = (u * d)^2 - (d*d)*(u*u)*cos(theta)
            // with apex=c:
            // F(p) = ((p-c)*d)^2 - (d*d)*((p-c)*(p-c))*(cos(theta))^2
            //ax: px - cx;
            //ay: py - cy;
            //az: pz - cz;
            // dx: cos(b)*cos(a);
            // dy: cos(b)*sin(a);
            // dz: sin(b);
            //f: (ax * dx + ay * dy + az * dz) ^ 2 - (dx * dx + dy * dy + dz * dz) * (ax * ax + ay * ay + az * az) * (cos(t)) ^ 2;
            //dfdcx: diff(f, cx, 1);
            //dfdcy: diff(f, cy, 1);
            //dfdcz: diff(f, cz, 1);
            //dfdnx: diff(f, a, 1);
            //dfdny: diff(f, b, 1);
            //dfdt: diff(f, t, 1);
            //stringout("C:/Temp/coneapprox.txt", values);
            // yields:
            //f: (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx)) ^ 2 - (dz ^ 2 + dy ^ 2 + dx ^ 2) * ((pz - cz) ^ 2 + (py - cy) ^ 2 + (px - cx) ^ 2) * cos(t) ^ 2;
            //dfdcx: 2 * (dz ^ 2 + dy ^ 2 + dx ^ 2) * (px - cx) * cos(t) ^ 2 - 2 * dx * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx));
            //dfdcy: 2 * (dz ^ 2 + dy ^ 2 + dx ^ 2) * (py - cy) * cos(t) ^ 2 - 2 * dy * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx));
            //dfdcz: 2 * (dz ^ 2 + dy ^ 2 + dx ^ 2) * (pz - cz) * cos(t) ^ 2 - 2 * dz * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx));
            //dfdnx: 2 * (px - cx) * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx)) - 2 * dx * ((pz - cz) ^ 2 + (py - cy) ^ 2 + (px - cx) ^ 2) * cos(t) ^ 2;
            //dfdny: 2 * (py - cy) * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx)) - 2 * dy * ((pz - cz) ^ 2 + (py - cy) ^ 2 + (px - cx) ^ 2) * cos(t) ^ 2;
            //dfdnz: 2 * (pz - cz) * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx)) - 2 * dz * ((pz - cz) ^ 2 + (py - cy) ^ 2 + (px - cx) ^ 2) * cos(t) ^ 2;
            //dfdt: 2 * (dz ^ 2 + dy ^ 2 + dx ^ 2) * ((pz - cz) ^ 2 + (py - cy) ^ 2 + (px - cx) ^ 2) * cos(t) * sin(t);
            //ax: px - cx;
            //ay: py - cy;
            //az: pz - cz;

            // now in polar coordinates to avoid the axis direction becoming zero
            //ax:px-cx;
            //ay:py-cy;
            //az:pz-cz;
            //dx:cos(a)*cos(b);
            //dy:sin(a)*cos(b);
            //dz:sin(b);
            //f:(sin(b)*(pz-cz)+sin(a)*cos(b)*(py-cy)+cos(a)*cos(b)*(px-cx))^2-(sin(b)^2+sin(a)^2*cos(b)^2+cos(a)^2*cos(b)^2)*((pz-cz)^2+(py-cy)^2+(px-cx)^2)*cos(t)^2;
            //dfdcx:2*(sin(b)^2+sin(a)^2*cos(b)^2+cos(a)^2*cos(b)^2)*(px-cx)*cos(t)^2-2*cos(a)*cos(b)*(sin(b)*(pz-cz)+sin(a)*cos(b)*(py-cy)+cos(a)*cos(b)*(px-cx));
            //dfdcy:2*(sin(b)^2+sin(a)^2*cos(b)^2+cos(a)^2*cos(b)^2)*(py-cy)*cos(t)^2-2*sin(a)*cos(b)*(sin(b)*(pz-cz)+sin(a)*cos(b)*(py-cy)+cos(a)*cos(b)*(px-cx));
            //dfdcz:2*(sin(b)^2+sin(a)^2*cos(b)^2+cos(a)^2*cos(b)^2)*(pz-cz)*cos(t)^2-2*sin(b)*(sin(b)*(pz-cz)+sin(a)*cos(b)*(py-cy)+cos(a)*cos(b)*(px-cx));
            //dfda:2*(cos(a)*cos(b)*(py-cy)-sin(a)*cos(b)*(px-cx))*(sin(b)*(pz-cz)+sin(a)*cos(b)*(py-cy)+cos(a)*cos(b)*(px-cx));
            //dfdb:2*(cos(b)*(pz-cz)-sin(a)*sin(b)*(py-cy)-cos(a)*sin(b)*(px-cx))*(sin(b)*(pz-cz)+sin(a)*cos(b)*(py-cy)+cos(a)*cos(b)*(px-cx))-((-2*sin(a)^2*cos(b)*sin(b))-2*cos(a)^2*cos(b)*sin(b)+2*cos(b)*sin(b))*((pz-cz)^2+(py-cy)^2+(px-cx)^2)*cos(t)^2;
            //dfdt:2*(sin(b)^2+sin(a)^2*cos(b)^2+cos(a)^2*cos(b)^2)*((pz-cz)^2+(py-cy)^2+(px-cx)^2)*cos(t)*sin(t);

            // parameters: { cx,cy,cz,a,b,t } wher xc,cy,cz is the apex, a,b polarcoordinates for the axis direction, t half of opening angle
            Vector<double> observedX = new DenseVector(points.Length); // there is no need to set values
            Vector<double> observedY = new DenseVector(points.Length); // this is the data we want to achieve, namely 0.0
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-15, maximumIterations: 20);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    DenseVector values = new DenseVector(points.Length);
                    GeoPoint c = new GeoPoint(vd[0], vd[1], vd[2]);
                    double sina = Math.Sin(vd[3]);
                    double cosa = Math.Cos(vd[3]);
                    double sinb = Math.Sin(vd[4]);
                    double cosb = Math.Cos(vd[4]);
                    double t = vd[5];
                    for (int i = 0; i < points.Length; i++)
                    {
                        GeoPoint pi = points[i];
                        values[i] = sqr(sinb * (pi.z - c.z) + sina * cosb * (pi.y - c.y) + cosa * cosb * (pi.x - c.x)) -
                            (sqr(sinb) + sqr(sina * cosb) + sqr(cosa * cosb)) * (sqr(pi.z - c.z) + sqr(pi.y - c.y) + sqr(pi.x - c.x)) * sqr(Math.Cos(t));
                    }
                    return values;
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {
                    DenseMatrix derivs = DenseMatrix.Create(points.Length, 6, 0); // Jacobi Matrix 
                    double cx = vd[0];
                    double cy = vd[1];
                    double cz = vd[2];
                    double sina = Math.Sin(vd[3]);
                    double cosa = Math.Cos(vd[3]);
                    double sinb = Math.Sin(vd[4]);
                    double cosb = Math.Cos(vd[4]);
                    double t = vd[5];
                    double cost2 = sqr(Math.Cos(t));
                    double t1 = sinb * sinb + sqr(sina * cosb) + sqr(cosa * cosb);
                    for (int i = 0; i < points.Length; i++)
                    {
                        double px = points[i].x;
                        double py = points[i].y;
                        double pz = points[i].z;

                        derivs[i, 0] = 2 * (t1) * (px - cx) * cost2 - 2 * cosa * cosb * (sinb * (pz - cz) + sina * cosb * (py - cy) + cosa * cosb * (px - cx));
                        derivs[i, 1] = 2 * (t1) * (py - cy) * cost2 - 2 * sina * cosb * (sinb * (pz - cz) + sina * cosb * (py - cy) + cosa * cosb * (px - cx));
                        derivs[i, 2] = 2 * (t1) * (pz - cz) * cost2 - 2 * sinb * (sinb * (pz - cz) + sina * cosb * (py - cy) + cosa * cosb * (px - cx));
                        derivs[i, 3] = 2 * (cosa * cosb * (py - cy) - sina * cosb * (px - cx)) * (sinb * (pz - cz) + sina * cosb * (py - cy) + cosa * cosb * (px - cx));
                        derivs[i, 4] = 2 * (cosb * (pz - cz) - sina * sinb * (py - cy) - cosa * sinb * (px - cx)) * (sinb * (pz - cz) + sina * cosb * (py - cy) + cosa * cosb * (px - cx)) - ((-2 * sqr(sina) * cosb * sinb) - 2 * sqr(cosa) * cosb * sinb + 2 * cosb * sinb) * (sqr(pz - cz) + sqr(py - cy) + sqr(px - cx)) * cost2;
                        derivs[i, 5] = 2 * (t1) * (sqr(pz - cz) + sqr(py - cy) + sqr(px - cx)) * Math.Cos(t) * Math.Sin(t);
                    }
                    return derivs;
                }), observedX, observedY);
            double a = Math.Atan2(axis.y, axis.x);
            double b = Math.Atan2(axis.z, Math.Sqrt(axis.x * axis.x + axis.y * axis.y));
            NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { apex.x, apex.y, apex.z, a, b, theta }));
            if (mres.ReasonForExit == ExitCondition.Converged || mres.ReasonForExit == ExitCondition.RelativeGradient)
            {
                GeoVector dir = new GeoVector(Math.Cos(mres.MinimizingPoint[4]) * Math.Cos(mres.MinimizingPoint[3]), Math.Cos(mres.MinimizingPoint[4]) * Math.Sin(mres.MinimizingPoint[3]), Math.Sin(mres.MinimizingPoint[4]));
                dir.ArbitraryNormals(out GeoVector dirx, out GeoVector diry);
                cs = new ConicalSurface(new GeoPoint(mres.MinimizingPoint[0], mres.MinimizingPoint[1], mres.MinimizingPoint[2]), dirx, diry, dir, mres.MinimizingPoint[5]);
                return sqr(mres.StandardErrors[0]) + sqr(mres.StandardErrors[1]) + sqr(mres.StandardErrors[2]) + sqr(mres.StandardErrors[3]) + sqr(mres.StandardErrors[4]) + sqr(mres.StandardErrors[5]);
            }
            else
            {
                cs = null;
                return double.MaxValue;
            }
        }

        public static double TorusFit(IArray<GeoPoint> points, GeoPoint center, GeoVector axis, double minrad, double precision, out ToroidalSurface ts)
        {
            // see https://www.geometrictools.com/Documentation/DistanceToCircle3.pdf
            // using this Maxima input:
            //R2: nx* nx +ny * ny + nz * nz; /* n is both the normal (axis direction) of the torus and the major radius (lenth of n) */
            //dx: px - cx;
            //dy: py - cy;
            //dz: pz - cz;
            //ndx: ny* dz-nz * dy;
            //ndy: nx* dz-nz * dx;
            //ndz: nx* dy-ny * dx;
            //a: (nx * dx + ny * dy + nz * dz) ^ 2 / R2; /* sub expression */
            //f: r* r -(a + (sqrt(dx * dx + dy * dy + dz * dz - a) - sqrt(R2)) ^ 2); /* r*r - "distance point to main torus circle", r is the minor radius, positive inside, negative outside of the torus */
            //dfdcx: diff(f, cx, 1); /* derivations needed for Jacobi Matrix */
            //dfdcy: diff(f, cy, 1);
            //dfdcz: diff(f, cz, 1);
            //dfdnx: diff(f, nx, 1);
            //dfdny: diff(f, ny, 1);
            //dfdnz: diff(f, nz, 1);
            //dfdr: diff(f, r, 1);
            //stringout("C:/Temp/torusapprox.txt", values);

            // parameters: { cx,cy,cz,nx,ny,nz,r} wher xc,cy,cz is the center of the torus, nx,ny,nz the normal, length of (nx,ny,nz) the major radius, r the minor radius
            Vector<double> observedX = new DenseVector(points.Length); // there is no need to set values
            Vector<double> observedY = new DenseVector(points.Length); // this is the data we want to achieve, namely 0.0
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-15, maximumIterations: 20);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    DenseVector values = new DenseVector(points.Length);
                    double cx = vd[0];
                    double cy = vd[1];
                    double cz = vd[2];
                    double nx = vd[3];
                    double ny = vd[4];
                    double nz = vd[5];
                    double r = vd[6];
                    double R2 = nx * nx + ny * ny + nz * nz;
                    double R = Math.Sqrt(R2);
                    for (int i = 0; i < points.Length; i++)
                    {
                        GeoVector d = points[i] - new GeoPoint(cx, cy, cz);
                        values[i] = r * r - (sqr(nx * d.x + ny * d.y + nz * d.z) / R2 + sqr(Math.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z - sqr(nx * d.x + ny * d.y + nz * d.z) / R2) - R));
                    }
                    return values;
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {
                    DenseMatrix derivs = DenseMatrix.Create(points.Length, 7, 0); // Jacobi Matrix 
                    double cx = vd[0];
                    double cy = vd[1];
                    double cz = vd[2];
                    double nx = vd[3];
                    double ny = vd[4];
                    double nz = vd[5];
                    double r = vd[6];
                    double nn = nz * nz + ny * ny + nx * nx;
                    double rn = Math.Sqrt(nn);

                    for (int i = 0; i < points.Length; i++)
                    {
                        double px = points[i].x;
                        double py = points[i].y;
                        double pz = points[i].z;
                        double dx = px - cx;
                        double dy = py - cy;
                        double dz = pz - cz;
                        // manually extracted common subexpressions
                        double nd = nz * dz + ny * dy + nx * dx;
                        double dd = dz * dz + dy * dy + dx * dx;
                        double b = Math.Sqrt((-sqr(nd)) / nn + dd);
                        double a = b - Math.Sqrt(nn);
                        double ndnn = nd / nn;
                        double sndnn = sqr(nd) / sqr(nn);
                        derivs[i, 0] = 2.0 * nx * ndnn - (2.0 * nx * ndnn - 2.0 * dx) * a / b;
                        derivs[i, 1] = 2.0 * ny * ndnn - (2.0 * ny * ndnn - 2.0 * dy) * a / b;
                        derivs[i, 2] = 2.0 * nz * ndnn - (2.0 * nz * ndnn - 2.0 * dz) * a / b;
                        derivs[i, 3] = (-2.0) * ((nx * sndnn - dx * ndnn) / b - nx / rn) * a + 2.0 * nx * sndnn - 2.0 * dx * ndnn;
                        derivs[i, 4] = (-2.0) * ((ny * sndnn - dy * ndnn) / b - ny / rn) * a + 2.0 * ny * sndnn - 2.0 * dy * ndnn;
                        derivs[i, 5] = (-2.0) * ((nz * sndnn - dz * ndnn) / b - nz / rn) * a + 2.0 * nz * sndnn - 2.0 * dz * ndnn;
                        derivs[i, 6] = 2 * r;
                    }
                    return derivs;
                }), observedX, observedY);
            NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { center.x, center.y, center.z, axis.x, axis.y, axis.z, minrad }));
            if (mres.ReasonForExit == ExitCondition.Converged || mres.ReasonForExit == ExitCondition.RelativeGradient)
            {
                GeoPoint loc = new GeoPoint(mres.MinimizingPoint[0], mres.MinimizingPoint[1], mres.MinimizingPoint[2]);
                GeoVector normal = new GeoVector(mres.MinimizingPoint[3], mres.MinimizingPoint[4], mres.MinimizingPoint[5]);
                double majrad = normal.Length;
                Plane pln = new Plane(loc, normal);
                ts = new ToroidalSurface(pln.Location, pln.DirectionX, pln.DirectionY, pln.Normal, majrad, Math.Abs(mres.MinimizingPoint[6]));
                double err = 0.0;
                for (int i = 0; i < mres.StandardErrors.Count; i++) err += sqr(mres.StandardErrors[i]);
                return err;
            }
            else
            {
                ts = null;
                return double.MaxValue;
            }
        }
        public static double CylinderFit(IArray<GeoPoint> points, GeoPoint center, GeoVector axis, double radius, double precision, out CylindricalSurface cs)
        {
            // a,b are the polar angles of the axis, s,t are the parameters for the two vectors spanning the plane perpendicular to (dx,dy,dz), r is radius, p is the point to test
            // (-dy,dx,0) and (dx*dz,dy*dz,-dy*dy-dx*dx) are two vectors (v1 and v2), which span a plane perpendicular to (dx,dy,dz)
            // s*v1+t*v2 is the center of the cone, so we have to unknowns for the center (s and t) and two for the axis (a and b)
            // d is the axis of the cylinder, c the location, p the point to test, f the function to minimize 
            //dx:cos(a)*cos(b);
            //dy:sin(a)*cos(b);
            //dz:sin(b);
            //cx: -s*dy + t*dz*dz;
            //cy: s*dx + t*dy+dz;
            //cz:t*(-dy*dy-dx*dx);
            //ax:px-cx;
            //ay:py-cy;
            //az:pz-cz;
            //f: r^2+(ax*dx)^2+(ay*dy)^2+(az*dz)^2-ax^2-ay^2-az^2;
            //dfds: diff(f, s, 1);
            //dfdt: diff(f, t, 1);
            //dfda: diff(f, a, 1);
            //dfdb: diff(f, b, 1);
            //dfdr: diff(f, r, 1);
            //stringout("C:/Temp/cylapprox.txt", values);
            /*
                f:r^2+sin(b)^2*(pz-cz)^2-(pz-cz)^2+sin(a)^2*cos(b)^2*(py-cy)^2-(py-cy)^2+cos(a)^2*cos(b)^2*(px-cx)^2-(px-cx)^2;
                dfdcx:2*(px-cx)-2*cos(a)^2*cos(b)^2*(px-cx);
                dfdcy:2*(py-cy)-2*sin(a)^2*cos(b)^2*(py-cy);
                dfdcz:2*(pz-cz)-2*sin(b)^2*(pz-cz);
                dfda:2*cos(a)*sin(a)*cos(b)^2*(py-cy)^2-2*cos(a)*sin(a)*cos(b)^2*(px-cx)^2;
                dfdb:2*cos(b)*sin(b)*(pz-cz)^2-2*sin(a)^2*cos(b)*sin(b)*(py-cy)^2-2*cos(a)^2*cos(b)*sin(b)*(px-cx)^2;
                dfdr:2*r;
            */

            // parameters: 0: cx, 1: cy, 2: dx, 3: dy, 4: dz, 5: r (c: location, d: direction, r: radius)
            Vector<double> observedX = new DenseVector(points.Length); // there is no need to set values
            Vector<double> observedY = new DenseVector(points.Length); // this is the data we want to achieve, namely 0.0
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-15, maximumIterations: 20);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    DenseVector values = new DenseVector(points.Length);
                    double cx = vd[0];
                    double cy = vd[1];
                    double cz = vd[2];
                    double sina = Math.Sin(vd[3]);
                    double cosa = Math.Cos(vd[3]);
                    double sinb = Math.Sin(vd[4]);
                    double cosb = Math.Cos(vd[4]);
                    double r = vd[5];
                    for (int i = 0; i < points.Length; i++)
                    {
                        double px = points[i].x;
                        double py = points[i].y;
                        double pz = points[i].z;
                        values[i] = r * r + sinb * sinb * sqr(pz - cz) - sqr(pz - cz) + sina * sina * cosb * cosb * sqr(py - cy) - sqr(py - cy) + cosa + cosa * cosb * cosb * sqr(px - cx) - sqr(px - cx);
                    }
                    return values;
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {
                    DenseMatrix derivs = DenseMatrix.Create(points.Length, 6, 0); // Jacobi Matrix 
                    double cx = vd[0];
                    double cy = vd[1];
                    double cz = vd[2];
                    double sina = Math.Sin(vd[3]);
                    double cosa = Math.Cos(vd[3]);
                    double sinb = Math.Sin(vd[4]);
                    double cosb = Math.Cos(vd[4]);
                    double r = vd[5];

                    for (int i = 0; i < points.Length; i++)
                    {
                        double px = points[i].x;
                        double py = points[i].y;
                        double pz = points[i].z;

                        derivs[i, 0] = 2 * (px - cx) - 2 * cosa * cosa * cosb * cosb * (px - cx);
                        derivs[i, 1] = 2 * (py - cy) - 2 * sina * sina * cosb * cosb * (py - cy);
                        derivs[i, 2] = 2 * (pz - cz) - 2 * sinb * sinb * (pz - cz);
                        derivs[i, 3] = 2 * cosa * sina * cosb * cosb * sqr(py - cy) - 2 * cosa * sina * cosb * cosb * sqr(px - cx);
                        derivs[i, 4] = 2 * cosb * sinb * sqr(pz - cz) - 2 * sina * sina * cosb * sinb * sqr(py - cy) - 2 * cosa * cosa * cosb * sinb * sqr(px - cx);
                        derivs[i, 5] = 2 * r;
                    }
                    return derivs;
                }), observedX, observedY);
            double a = Math.Atan2(axis.y, axis.x);
            double b = Math.Atan2(axis.z, Math.Sqrt(axis.x * axis.x + axis.y * axis.y));
            NonlinearMinimizationResult mres = lm.FindMinimum(iom, new DenseVector(new double[] { center.x, center.y, center.z, a, b, radius }));
            if (mres.ReasonForExit == ExitCondition.Converged || mres.ReasonForExit == ExitCondition.RelativeGradient)
            {
                GeoVector dir = new GeoVector(Math.Cos(mres.MinimizingPoint[4]) * Math.Cos(mres.MinimizingPoint[3]), Math.Cos(mres.MinimizingPoint[4]) * Math.Sin(mres.MinimizingPoint[3]), Math.Sin(mres.MinimizingPoint[4]));
                dir.ArbitraryNormals(out GeoVector dirx, out GeoVector diry);
                cs = new CylindricalSurface(center, radius * dirx, radius * diry, axis);
                double err = 0.0;
                for (int i = 0; i < mres.StandardErrors.Count; i++) err += sqr(mres.StandardErrors[i]);
#if DEBUG
                double dd = 0.0;
                for (int i = 0; i < points.Length; i++)
                {
                    dd += cs.GetDistance(points[i]);
                }
#endif
                return err;
            }
            else
            {
                cs = null;
                return double.MaxValue;
            }
        }

        private static GeoPoint2D[] Solve2D(double a, double b, double c, double d, double e, double f)
        {
            // the implicit quadric a*x² + b*y² + c*x*y + d*x + e*y == f
            // may be a ellipse, parabola or hyperbola or a line (a==0 and b==0)
            // we are looking for minima and maxima in x and y
            // Maxima input:
            // q: a*x^2 + b*y^2 + c*x*y + d*x + e*y -f;
            // solve ([q,diff(q,x)],[x,y]);
            // solve ([q,diff(q,y)],[x,y]);
            // stringout("C:/Temp/quadric2d.txt", values);
            // dx: [[x = -(c * sqrt((4 * a ^ 2 * b - a * c ^ 2) * f + a ^ 2 * e ^ 2 - a * c * d * e + a * b * d ^ 2) + a * c * e - 2 * a * b * d) / (a * c ^ 2 - 4 * a ^ 2 * b),y = (2 * sqrt((4 * a ^ 2 * b - a * c ^ 2) * f + a ^ 2 * e ^ 2 - a * c * d * e + a * b * d ^ 2) + 2 * a * e - c * d) / (c ^ 2 - 4 * a * b)],[x = (c * sqrt((4 * a ^ 2 * b - a * c ^ 2) * f + a ^ 2 * e ^ 2 - a * c * d * e + a * b * d ^ 2) - a * c * e + 2 * a * b * d) / (a * c ^ 2 - 4 * a ^ 2 * b),y = -(2 * sqrt((4 * a ^ 2 * b - a * c ^ 2) * f + a ^ 2 * e ^ 2 - a * c * d * e + a * b * d ^ 2) - 2 * a * e + c * d) / (c ^ 2 - 4 * a * b)]];
            // dy: [[x = -(2 * sqrt((4 * a * b ^ 2 - b * c ^ 2) * f + a * b * e ^ 2 - b * c * d * e + b ^ 2 * d ^ 2) + c * e - 2 * b * d) / (c ^ 2 - 4 * a * b),y = (c * sqrt((4 * a * b ^ 2 - b * c ^ 2) * f + a * b * e ^ 2 - b * c * d * e + b ^ 2 * d ^ 2) + 2 * a * b * e - b * c * d) / (b * c ^ 2 - 4 * a * b ^ 2)],[x = (2 * sqrt((4 * a * b ^ 2 - b * c ^ 2) * f + a * b * e ^ 2 - b * c * d * e + b ^ 2 * d ^ 2) - c * e + 2 * b * d) / (c ^ 2 - 4 * a * b),y = -(c * sqrt((4 * a * b ^ 2 - b * c ^ 2) * f + a * b * e ^ 2 - b * c * d * e + b ^ 2 * d ^ 2) - 2 * a * b * e + b * c * d) / (b * c ^ 2 - 4 * a * b ^ 2)]];

            // dx: [[x = -(c * sqrt((4 * aa * b - a * cc) * f + aa * ee - a * c * d * e + a * b * dd) + a * c * e - 2 * a * b * d) / (a * cc - 4 * aa * b),y = (2 * sqrt((4 * aa * b - a * cc) * f + aa * ee - a * c * d * e + a * b * dd) + 2 * a * e - c * d) / (cc - 4 * a * b)],[x = (c * sqrt((4 * aa * b - a * cc) * f + aa * ee - a * c * d * e + a * b * dd) - a * c * e + 2 * a * b * d) / (a * cc - 4 * aa * b),y = -(2 * sqrt((4 * aa * b - a * cc) * f + aa * ee - a * c * d * e + a * b * dd) - 2 * a * e + c * d) / (cc - 4 * a * b)]];
            // dy: [[x = -(2 * sqrt((4 * a * bb - b * cc) * f + a * b * ee - b * c * d * e + bb * dd) + c * e - 2 * b * d) / (cc - 4 * a * b),y = (c * sqrt((4 * a * bb - b * cc) * f + a * b * ee - b * c * d * e + bb * dd) + 2 * a * b * e - b * c * d) / (b * cc - 4 * a * bb)],[x = (2 * sqrt((4 * a * bb - b * cc) * f + a * b * ee - b * c * d * e + bb * dd) - c * e + 2 * b * d) / (cc - 4 * a * b),y = -(c * sqrt((4 * a * bb - b * cc) * f + a * b * ee - b * c * d * e + bb * dd) - 2 * a * b * e + b * c * d) / (b * cc - 4 * a * bb)]];

            GeoPoint2D[] res = new GeoPoint2D[8]; // bottom, top, left, right, or invald, if not exists and 4 more points
            double aa = a * a;
            double bb = b * b;
            double cc = c * c;
            double dd = d * d;
            double ee = e * e;
            double r = (4 * aa * b - a * cc) * f + aa * ee - a * c * d * e + a * b * dd;
            double d1 = a * cc - 4 * aa * b;
            double d2 = cc - 4 * a * b;
            double x = double.NaN, y = double.NaN;
            if (r >= 0 && d1 != 0.0 && d2 != 0.0)
            {
                res[0] = new GeoPoint2D(-(c * Math.Sqrt(r) + a * c * e - 2 * a * b * d) / d1, (2 * Math.Sqrt(r) + 2 * a * e - c * d) / d2);
                res[1] = new GeoPoint2D((c * Math.Sqrt(r) - a * c * e + 2 * a * b * d) / d1, -(2 * Math.Sqrt(r) - 2 * a * e + c * d) / d2);
                x = (res[0].x + res[1].x) / 2.0;
                y = (res[0].y + res[1].y) / 2.0;
            }
            else
            {
                res[0] = GeoPoint2D.Invalid;
                res[1] = GeoPoint2D.Invalid;
            }
            r = (4 * a * bb - b * cc) * f + a * b * ee - b * c * d * e + bb * dd;
            d1 = b * cc - 4 * a * bb;
            if (r >= 0 && d1 != 0.0 && d2 != 0.0)
            {
                res[2] = new GeoPoint2D(-(2 * Math.Sqrt(r) + c * e - 2 * b * d) / d2, (c * Math.Sqrt(r) + 2 * a * b * e - b * c * d) / d1);
                res[3] = new GeoPoint2D((2 * Math.Sqrt(r) - c * e + 2 * b * d) / d2, -(c * Math.Sqrt(r) - 2 * a * b * e + b * c * d) / d1);
                x = (res[2].x + res[3].x) / 2.0;
                y = (res[2].y + res[3].y) / 2.0;
            }
            else
            {
                res[2] = GeoPoint2D.Invalid;
                res[3] = GeoPoint2D.Invalid;
            }
            if (double.IsNaN(x) || double.IsNaN(y))
            {
                res[4] = GeoPoint2D.Invalid;
                res[5] = GeoPoint2D.Invalid;
                res[6] = GeoPoint2D.Invalid;
                res[7] = GeoPoint2D.Invalid;
            }
            else
            {
                // 	[x=-(sqrt((c^2-4*a*b)*y^2+(2*c*d-4*a*e)*y+4*a*f+d^2)+c*y+d)/(2*a),x=(sqrt((c^2-4*a*b)*y^2+(2*c*d-4*a*e)*y+4*a*f+d^2)-c*y-d)/(2*a)]
                // 	[y=-(sqrt((c^2-4*a*b)*x^2+(2*c*e-4*b*d)*x+4*b*f+e^2)+c*x+e)/(2*b),y=(sqrt((c^2-4*a*b)*x^2+(2*c*e-4*b*d)*x+4*b*f+e^2)-c*x-e)/(2*b)]
                res[4] = new GeoPoint2D(-(Math.Sqrt((cc - 4 * a * b) * y * y + (2 * c * d - 4 * a * e) * y + 4 * a * f + dd) + c * y + d) / (2 * a), y);
                res[5] = new GeoPoint2D((Math.Sqrt((cc - 4 * a * b) * y * y + (2 * c * d - 4 * a * e) * y + 4 * a * f + dd) - c * y - d) / (2 * a), y);
                res[6] = new GeoPoint2D(x, -(Math.Sqrt((cc - 4 * a * b) * x * x + (2 * c * e - 4 * b * d) * x + 4 * b * f + ee) + c * x + e) / (2 * b));
                res[7] = new GeoPoint2D(x, (Math.Sqrt((cc - 4 * a * b) * x * x + (2 * c * e - 4 * b * d) * x + 4 * b * f + ee) - c * x - e) / (2 * b));
            }
            return res;
        }

        public static double TestPlaneFit(IArray<GeoPoint> points, out Plane plane)
        {
            plane = Plane.XYPlane; // in case of failure
            if (points.Length < 3)
            {
                return double.MaxValue;
            }

            Matrix A = new DenseMatrix(points.Length, 3);
            Vector B = new DenseVector(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                A[i, 0] = points[i].x; // move the points to (x,y,z)>(1,1,1)
                A[i, 1] = points[i].y;
                A[i, 2] = points[i].z;
                B[i] = -1;
            }
            GeoVector translation = GeoVector.NullVector;
            Matrix AAT = (Matrix)(A.Transpose().Multiply(A));
            if (AAT.Rank() < 3)
            {   // this might be the case when the optimal plane geos through the origin
                // then we need to move the point cloud
                // a goo direction seems to be the axis direction where the extent is smallest
                BoundingCube ext = new BoundingCube(points.ToArray());
                int ind;
                if (ext.XDiff < ext.YDiff && ext.XDiff < ext.ZDiff) ind = 0;
                else if (ext.YDiff < ext.XDiff && ext.YDiff < ext.ZDiff) ind = 1;
                else ind = 2;
                double sz = ext.Size;
                translation[ind] += sz;
                for (int i = 0; i < points.Length; i++)
                {
                    A[i, ind] += sz;
                }
                AAT = (Matrix)(A.Transpose().Multiply(A));
                if (AAT.Rank() < 3) return double.MaxValue;
            }
            Vector res = (Vector)AAT.Solve(A.Transpose().Multiply(B));
            if (res.IsValid())
            {
                double err = 0.0;
                if (translation.IsNullVector())
                {
                    for (int i = 0; i < points.Length; i++)
                    {
                        err += sqr(res[0] * points[i].x + res[1] * points[i].y + res[2] * points[i].z + 1);
                    }
                }
                else
                {
                    for (int i = 0; i < points.Length; i++)
                    {
                        err += sqr(res[0] * (points[i].x + translation.x) + res[1] * (points[i].y + translation.y) + res[2] * (points[i].z + translation.z) + 1);
                    }
                }
                double l = -1.0 / (res[0] * res[0] + res[1] * res[1] + res[2] * res[2]);

                GeoPoint axisLocation = new GeoPoint(l * res[0], l * res[1], l * res[2]);
                plane = new Plane(axisLocation - translation, (new GeoVector(res[0], res[1], res[2])).Normalized);
                return err;
            }
            plane = new Plane();
            return double.MaxValue;
        }
        public static double QuadricFit(IArray<GeoPoint> points)
        {
            GeoPoint cnt = new GeoPoint(points.ToArray()); // center of all points
            // the quadric has the form a*x² + 2*b*x*y + 2*c*x*z + 2*d*x + e*y^2 + 2*f*y*z + 2*g*y + h*z^2 + 2*i*z == -1
            Matrix A = new DenseMatrix(points.Length, 9);
            Vector B = new DenseVector(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                points[i] -= cnt.ToVector(); // move to origin
                A[i, 0] = points[i].x * points[i].x;
                A[i, 1] = 2 * points[i].x * points[i].y;
                A[i, 2] = 2 * points[i].x * points[i].z;
                A[i, 3] = 2 * points[i].x;
                A[i, 4] = points[i].y * points[i].y;
                A[i, 5] = 2 * points[i].y * points[i].z;
                A[i, 6] = 2 * points[i].y;
                A[i, 7] = points[i].z * points[i].z;
                A[i, 8] = 2 * points[i].z;
                B[i] = -1;
            }
            double size = new BoundingCube(points.ToArray()).Size;
            Vector res = (Vector)A.Transpose().Multiply(A).Solve(A.Transpose().Multiply(B));
            if (res.IsValid())
            {
                double err = 0.0;
                for (int i = 0; i < points.Length; i++)
                {
                    err += sqr(res[0] * points[i].x * points[i].x
                        + res[1] * 2 * points[i].x * points[i].y
                        + res[2] * 2 * points[i].x * points[i].z
                        + res[3] * 2 * points[i].x
                        + res[4] * points[i].y * points[i].y
                        + res[5] * 2 * points[i].y * points[i].z
                        + res[6] * 2 * points[i].y
                        + res[7] * points[i].z * points[i].z
                        + res[8] * 2 * points[i].z + 1);
                }
                // matrix AA according to https://de.wikipedia.org/wiki/Quadrik#Eigenschaften is [ [ a, b, c], [b, e, f], [c, f, h] ], matrix b is [d, g, i]
                // the quadric has the form a*x² + 2*b*x*y + 2*c*x*z + 2*d*x + e*y^2 + 2*f*y*z + 2*g*y + h*z² + 2*i*z + 1 == 0
                Matrix AA = new DenseMatrix(3, 3, new double[] { res[0], res[1], res[2], res[1], res[4], res[5], res[2], res[5], res[7] });
                Evd<double> AAEvd = AA.Evd(Symmetricity.Symmetric);
                Matrix<double> S = AAEvd.EigenVectors;
                Matrix<double> dbg = S.Transpose().Multiply(AA.Multiply(S));
                GeoPoint[] dbgp = new GeoPoint[points.Length];


                // +a*x^2
                // +2*b*x*y
                // +2*c*x*z
                // +(2*c*dz+2*b*dy+2*a*dx+2*d)*x
                // +e*y^2
                // +2*f*y*z
                // +(2*g+2*dz*f+2*dy*e+2*b*dx)*y
                // 	h*z^2
                // +(2*i+2*dz*h+2*dy*f+2*c*dx)*z
                // +2*dz*i+dz^2*h+2*dy*g+2*dy*dz*f+dy^2*e+2*c*dx*dz+2*b*dx*dy+a*dx^2+2*d*dx+1
                // a:0, b:1, c:2, d:3, e:4, f:5, g:6, h:7, i:8
                // (2*c*dz+2*b*dy+2*a*dx+2*d) == 0
                // (2*g+2*dz*f+2*dy*e+2*b*dx) == 0
                // (2*i+2*dz*h+2*dy*f+2*c*dx) == 0
                Matrix toOrigin = new DenseMatrix(3, 3, new double
                    [] { 2 * res[0], 2 * res[1], 2 * res[2], 2 * res[1], 2 * res[4], 2 * res[5], 2 * res[2], 2 * res[5], 2 * res[7] });
                Vector toOriginB = new DenseVector(new double[] { -2 * res[3], -2 * res[6], -2 * res[8] });
                Vector translate = (Vector)toOrigin.Solve(toOriginB);
                Matrix Sinv = (Matrix)S.Inverse();
                for (int i = 0; i < points.Length; i++)
                {
                    dbgp[i] = Sinv * (points[i] - new GeoVector(translate[0], translate[1], translate[2]));
                }
                ModOp mtoOrigin = new ModOp(Sinv.ToArray(), new GeoVector(0, 0, 0)) * ModOp.Translate(-new GeoVector(translate[0], translate[1], translate[2]));
                for (int i = 0; i < points.Length; i++)
                {
                    dbgp[i] = mtoOrigin * points[i];
                }
            }
            return double.MaxValue;
        }
        public static double QuadricFitOld(IArray<GeoPoint> points)
        {
            Vector<double> observedX = new DenseVector(points.Length); // there is no need to set values
            Vector<double> observedY = new DenseVector(points.Length); // this is the data we want to achieve, namely 0.0

            // translate the points to a cloud around the origin
            GeoPoint center = new GeoPoint(points.ToArray());
            GeoVector translation = center.ToVector();
            for (int i = 0; i < points.Length; i++)
            {
                points[i] -= translation;
            }
            DenseMatrix derivs = DenseMatrix.Create(points.Length, 9, 0); // Jacobi Matrix 
            for (int i = 0; i < points.Length; i++)
            {
                double x = points[i].x;
                double y = points[i].y;
                double z = points[i].z;

                derivs[i, 0] = x * x;
                derivs[i, 1] = 2 * x * y;
                derivs[i, 2] = 2 * x * z;
                derivs[i, 3] = 2 * x;
                derivs[i, 4] = y * y;
                derivs[i, 5] = 2 * y * z;
                derivs[i, 6] = 2 * y;
                derivs[i, 7] = z * z;
                derivs[i, 8] = 2 * z;
            }
            // the quadric has the form a*x² + 2*b*x*y + 2*c*x*z + 2*d*x + e*y^2 + 2*f*y*z + 2*g*y + h*z^2 + 2*i*z == 1
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-15, maximumIterations: 40);
            IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                {
                    DenseVector values = new double[points.Length];
                    double A = vd[0];
                    double B = vd[1];
                    double C = vd[2];
                    double D = vd[3];
                    double E = vd[4];
                    double F = vd[5];
                    double G = vd[6];
                    double H = vd[7];
                    double I = vd[8];
                    double J = -1.0;
                    for (int i = 0; i < points.Length; i++)
                    {
                        double x = points[i].x;
                        double y = points[i].y;
                        double z = points[i].z;
                        values[i] = H * z * z + 2 * F * y * z + 2 * C * x * z + 2 * I * z + E * y * y + 2 * B * x * y + 2 * G * y + A * x * x + 2 * D * x + J;
                    }
                    return values;
                }),
                new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                {
                    return derivs; // this is independant of parameters
                }), observedX, observedY);
            NonlinearMinimizationResult mres;
            System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            mres = lm.FindMinimum(iom, new DenseVector(new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
            stopWatch.Stop();
            if (mres.ReasonForExit == ExitCondition.Converged || mres.ReasonForExit == ExitCondition.RelativeGradient)
            {
                double A = mres.MinimizingPoint[0];
                double B = mres.MinimizingPoint[1];
                double C = mres.MinimizingPoint[2];
                double D = mres.MinimizingPoint[3];
                double E = mres.MinimizingPoint[4];
                double F = mres.MinimizingPoint[5];
                double G = mres.MinimizingPoint[6];
                double H = mres.MinimizingPoint[7];
                double I = mres.MinimizingPoint[8];
                // matrix A according to https://de.wikipedia.org/wiki/Quadrik#Eigenschaften is [ [ a, b, c], [b, e, f], [c, f, h] ], matrix b is [d, g, i]
                // the quadric has the form a*x² + 2*b*x*y + 2*c*x*z + 2*d*x + e*y^2 + 2*f*y*z + 2*g*y + h*z² + 2*i*z == 1
                // intersection with xy-plane:
                // E*y² + 2*B*x*y + 2*G*y + A*x² + 2*D*x == 1
                Matrix AA = new DenseMatrix(3, 3, new double[] { A, B, C, B, E, F, C, F, H });
                Evd<double> AAEvd = AA.Evd(Symmetricity.Symmetric);
                Matrix<double> S = AAEvd.EigenVectors;
                Matrix<double> dbg = S.Transpose().Multiply(AA.Multiply(S));
                GeoPoint[] dbgp = new GeoPoint[points.Length];
                Matrix Sinv = (Matrix)S.Inverse();
                for (int i = 0; i < points.Length; i++)
                {
                    dbgp[i] = Sinv * points[i];
                }
                // dbgp ist Zylinder in Richtung x-Achse

                GeoPoint2D[] xy = Solve2D(A, E, 2 * B, 2 * D, 2 * G, 1);
                double d1 = xy[0] | xy[1];
                double d2 = xy[2] | xy[3];
                GeoPoint2D c1 = new GeoPoint2D(xy[0], xy[1]);
                GeoPoint2D c2 = new GeoPoint2D(xy[2], xy[3]);
                Geometry.PrincipalAxis(xy[0] - c1, xy[2] - c1, out GeoVector2D majorAxis, out GeoVector2D minorAxis);
                Ellipse2D exy = new Ellipse2D(c1, majorAxis, minorAxis);
                IGeoObject go = exy.MakeGeoObject(Plane.XYPlane);
                go.Modify(ModOp.Translate(translation));
                Line l1 = Line.TwoPoints(Plane.XYPlane.ToGlobal(xy[0]), Plane.XYPlane.ToGlobal(xy[1]));
                l1.Modify(ModOp.Translate(translation));
                Line l2 = Line.TwoPoints(Plane.XYPlane.ToGlobal(xy[2]), Plane.XYPlane.ToGlobal(xy[3]));
                l2.Modify(ModOp.Translate(translation));
                Line l3 = Line.TwoPoints(Plane.XYPlane.ToGlobal(xy[4]), Plane.XYPlane.ToGlobal(xy[5]));
                l3.Modify(ModOp.Translate(translation));
                Line l4 = Line.TwoPoints(Plane.XYPlane.ToGlobal(xy[6]), Plane.XYPlane.ToGlobal(xy[7]));
                l4.Modify(ModOp.Translate(translation));
                DebuggerContainer dc = new DebuggerContainer();
                dc.Add(go);
                dc.Add(l1);
                dc.Add(l2);
                dc.Add(l3);
                dc.Add(l4);
            }
            else
            {
            }

            return double.MaxValue;
        }

        /*
        class ScalarObjectiveFunction : IScalarObjectiveFunction
        {
            class ScalarObjectiveFunctionEvaluation: IScalarObjectiveFunctionEvaluation
            {
                double point;
                ScalarObjectiveFunction scalarObjectiveFunction;
                public ScalarObjectiveFunctionEvaluation(ScalarObjectiveFunction scalarObjectiveFunction, double point)
                {
                    this.point = point;
                    this.scalarObjectiveFunction = scalarObjectiveFunction;
                }

                public double Point => point;

                public double Value => scalarObjectiveFunction.Objective(point);

                public double Derivative => scalarObjectiveFunction.Derivative(point);

                public double SecondDerivative => scalarObjectiveFunction.SecondDerivative(point);
            }
            public Func<double, double> Objective { get; private set; }
            public Func<double, double> Derivative { get; private set; }
            public Func<double, double> SecondDerivative { get; private set; }

            public ScalarObjectiveFunction(Func<double, double> objective)
            {
                Objective = objective;
                Derivative = null;
                SecondDerivative = null;
            }

            public ScalarObjectiveFunction(Func<double, double> objective, Func<double, double> derivative)
            {
                Objective = objective;
                Derivative = derivative;
                SecondDerivative = null;
            }

            public ScalarObjectiveFunction(Func<double, double> objective, Func<double, double> derivative, Func<double, double> secondDerivative)
            {
                Objective = objective;
                Derivative = derivative;
                SecondDerivative = secondDerivative;
            }

            public bool IsDerivativeSupported
            {
                get { return Derivative != null; }
            }

            public bool IsSecondDerivativeSupported
            {
                get { return SecondDerivative != null; }
            }

            public IScalarObjectiveFunctionEvaluation Evaluate(double point)
            {
                return new ScalarObjectiveFunctionEvaluation(this, point);
            }
        }

        public void TryPerpFoot(GeoPoint2D fromHere)
        {
            GeoPoint2D p0 = toUnitCircle * fromHere;
            double a = Math.Atan2(p0.y, p0.x);
            if (a < 0.0) a += 2.0 * Math.PI;
            if (!counterClock) a = 2.0 * Math.PI - a;
            double u = a / (2 * Math.PI);
            GoldenSectionMinimizer nm = new GoldenSectionMinimizer();
            IScalarObjectiveFunction isof = new ScalarObjectiveFunction(
                new Func<double, double>(delegate (double u0)
                {
                    TryPointDeriv3At(u0, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2, out GeoVector2D deriv3);
                    GeoVector2D toPoint = fromHere - point;
                    double s1 = toPoint * deriv1;
                    double s2 = (toPoint * deriv2 - deriv1 * deriv1);
                    double val = sqr(s1);
                    return val;
                }),
                new Func<double, double>(delegate (double u0)
                {
                    TryPointDeriv3At(u0, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2, out GeoVector2D deriv3);
                    GeoVector2D toPoint = fromHere - point;
                    double s1 = toPoint * deriv1;
                    double s2 = (toPoint * deriv2 - deriv1 * deriv1);
                    double val = sqr(s1);
                    return 2 * s1 * s2;
                }),
                new Func<double, double>(delegate (double u0)
                {
                    TryPointDeriv3At(u0, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2, out GeoVector2D deriv3);
                    GeoVector2D toPoint = fromHere - point;
                    double s1 = toPoint * deriv1;
                    double s2 = (toPoint * deriv2 - deriv1 * deriv1);
                    double val = sqr(s1);
                    return 2 * s1 * (toPoint * deriv3 - 3 * deriv1 * deriv2) + 2 * sqr(s2);
                })
                );
            try
            {
                ScalarMinimizationResult mres = nm.FindMinimum(isof, u - 0.5, u + 0.5);
                double u0 = mres.MinimizingPoint;
            }
            catch
            {
            }
        }
         */
    }
}