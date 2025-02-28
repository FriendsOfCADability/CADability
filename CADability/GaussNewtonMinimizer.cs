using CADability.Curve2D;
using CADability.GeoObject;
using MathNet.Numerics.Optimization;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Collections.Generic;

namespace CADability
{
    public static class MyExtensions
    {
        public static double[] Add(this double[] a, double[] b)
        {
            double[] res = new double[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                res[i] = a[i] + b[i];
            }
            return res;
        }
        public static IArray<T> Row<T>(this T[,] a, int ind)
        {
            return new ArrayRow<T>(a, ind);
        }
        public static IArray<T> Column<T>(this T[,] a, int ind)
        {
            return new ArrayColumn<T>(a, ind);
        }
        public static IArray<T> Linear<T>(this T[,] a)
        {
            return new ArrayLinear<T>(a);
        }
        public static IArray<T> ToIArray<T>(this T[] a)
        {
            return new ToIArray<T>(a);
        }
    }
    public class GaussNewtonMinimizer
    {
        public delegate void ErrorFunction(double[] parameters, out double[] errorValues);
        public delegate void JacobiFunction(double[] parameters, out Matrix partialDerivations);
        public delegate bool CheckParameterFunction(double[] parameters);
        public delegate void CurtailParameterFunction(double[] parameters);

        private ErrorFunction errorFunction;
        private JacobiFunction jacobiFunction;
        private CheckParameterFunction checkParameter;
        private CurtailParameterFunction curtailParameter;
        private Matrix Jacobi;
        private Matrix JacobiTJacobi;
        private Vector<double> NegJacobiTError;
        private double[] Error;

        public GaussNewtonMinimizer(ErrorFunction eFunction, JacobiFunction jFunction, CheckParameterFunction checkParameter = null, CurtailParameterFunction curtailParameter = null)
        {
            this.errorFunction = eFunction;
            this.jacobiFunction = jFunction;
            this.checkParameter = checkParameter;
            this.curtailParameter = curtailParameter;
        }

        private double dot(double[] a, double[] b)
        {
            if (a.Length != b.Length) throw new ApplicationException("GaussNewtonMinimizer.dot: parameters must have same length");
            double sum = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                sum += a[i] * b[i];
            }
            return sum;
        }
        private void UnaryMinus(Vector<double> d)
        {
            for (int i = 0; i < d.Count; i++)
            {
                d[i] = -d[i];
            }
        }
        private void ComputeLinearSystemInputs(double[] pCurrent)
        {
            jacobiFunction(pCurrent, out Jacobi);
            JacobiTJacobi = (Matrix)Jacobi.TransposeThisAndMultiply(Jacobi); // Matrix.Transpose(Jacobi) * Jacobi;
            NegJacobiTError = (DenseMatrix.OfRowArrays(Error) * Jacobi).Row(0);
            UnaryMinus(NegJacobiTError);

            //double diagsum = 0.0;
            //for (int i = 0; i < mNegJTF.Length; i++)
            //{
            //    diagsum += mJTJ[i, i];
            //}
            //double adj = 0.1 * diagsum / mNegJTF.Length;
            //for (int i = 0; i < mNegJTF.Length; i++)
            //{
            //    mJTJ[i, i]+=adj;
            //}
        }

        /// <summary>
        /// Solves the Gauss Newton approximation with the given <paramref name="startParameters"/> and the error function and Jacobi matrix as specified in the constructor.
        /// Returns the result in <paramref name="parameters"/> and returns true, if the error function of the result is less than <paramref name="errorTolerance"/>. 
        /// The iteration doesn't stop when <paramref name="errorTolerance"/> is reached, but goes on until no more improvement of the result ocurres.
        /// </summary>
        /// <param name="startParameters">start parameters</param>
        /// <param name="maxIterations">maximum number of iterations</param>
        /// <param name="updateLengthTolerance">not used</param>
        /// <param name="errorTolerance">maximum error for positiv result</param>
        /// <param name="minError">effective error</param>
        /// <param name="numIterations">number of iterations used</param>
        /// <param name="parameters">resulting parameters</param>
        /// <returns></returns>
        public bool Solve(double[] startParameters, int maxIterations, double updateLengthTolerance, double errorTolerance, out double minError, out int numIterations, out double[] parameters)
        {
            double[] minLocation = startParameters.Clone() as double[];
            minError = double.MaxValue;
            numIterations = 0;
            errorFunction(startParameters, out Error);
            minError = dot(Error, Error);
            double[] pCurrent = startParameters.Clone() as double[];
            int numOutside = 0;
            for (numIterations = 1; numIterations < maxIterations; numIterations++)
            {
                ComputeLinearSystemInputs(pCurrent);
                double[] pNext;

                try
                {
                    Vector<double> s = JacobiTJacobi.Cholesky().Solve(NegJacobiTError);
                    pNext = pCurrent.Add(s.ToArray());
                }
                catch (System.ArgumentException)
                {
                    break;
                }
                //Matrix solved = JacobiTJacobi.SaveSolve(new Matrix(NegJacobiTError, true));
                //if (solved == null) break; // should not happen
                //pNext = pCurrent.Add(solved.Column(0));
                if (checkParameter != null && !checkParameter(pNext))
                {
                    curtailParameter?.Invoke(pNext);
                    ++numOutside;
                }
                else
                {
                    numOutside = 0;
                }
                errorFunction(pNext, out Error);
                double error = dot(Error, Error);
                if (error < minError)
                {
                    double convergence = error / minError; // some value less than 1, as long as we have strong convergence we continue the process
                    //minErrorDifference = minError - error;
                    //minUpdateLength = Math.Sqrt(dot(NegJacobiTError, NegJacobiTError));
                    minLocation = pNext;
                    minError = error;
                    // don't stop, if the convergence is still strong
                    if (error <= errorTolerance && convergence > 0.25) // || minUpdateLength <= updateLengthTolerance)
                    {
                        parameters = pNext;
                        return true;
                    }
                }
                else if (numIterations > 5 || numOutside > 3) // the first few steps may diverge, up to three continuous locations may be outside the bounds (if given)
                {
                    parameters = pCurrent;
                    return error <= errorTolerance; // maybe the last step couldn't improve the result, so we take the last result (which was better)
                }
                else
                {
                    if (minError < error && minError < errorTolerance && numIterations == 1)
                    {
                        parameters = pCurrent;
                        return true;
                    }
                    minError = error;
                }
                pCurrent = pNext;

            }
            parameters = pCurrent;
            return minError <= errorTolerance;
        }
        static private double quad(double x) { return x * x * x * x; }
        static private double cube(double x) { return x * x * x; }
        static double sqr(double d) { return d * d; }
        static double exp32(double d) { return Math.Sqrt(d * d * d); }
        /// <summary>
        /// Calculates the closest line approximating the provided <paramref name="points"/>.
        /// </summary>
        /// <param name="points">Points to be approximated by the line</param>
        /// <param name="location">Location of the line</param>
        /// <param name="direction">direction of the line</param>
        /// <returns>maximum distance to the line</returns>
        public static double LineFit(IArray<GeoPoint> points, double precision, out GeoPoint location, out GeoVector direction)
        {
            // We use a GaussNewtonMinimizer with the following parameters: l: location, d: direction of the line
            // parameters: 0:lx, 1:ly, 2: lz, 3: dx, 4: dy, 5: dz

            void efunc(double[] parameters, out double[] values)
            {
                // v = d ^ (l - p);
                // v.Length/d.Length is the distance of point p to the line (l,d)
                // sqrt((dy*(lz-pz)-dz*(ly-py))² + (...)² + (...)²) / sqrt(dx*dx+dy*dy+dz*dz) the distance
                // error: dy*(lz-pz)-dz*(ly-py))² + (...)² + (...)² / (dx*dx+dy*dy+dz*dz) the sqaure of the distance

                values = new double[points.Length];
                double lx = parameters[0];
                double ly = parameters[1];
                double lz = parameters[2];
                double dx = parameters[3];
                double dy = parameters[4];
                double dz = parameters[5];
                double dl = dx * dx + dy * dy + dz * dz;
                for (int i = 0; i < points.Length; i++)
                {
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;
                    values[i] = (sqr(dy * (lz - pz) - dz * (ly - py)) + sqr(dx * (lz - pz) - dz * (lx - px)) + sqr(dx * (ly - py) - dy * (lx - px))) / (dl);
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                // derivations of the (square of the) distance by lx,ly,lz,dx,dy,dz
                //((-2 * dz * (dx * (lz - pz) - dz * (lx - px))) - 2 * dy * (dx * (ly - py) - dy * (lx - px))) / (dz ^ 2 + dy ^ 2 + dx ^ 2);
                //(2 * dx * (dx * (ly - py) - dy * (lx - px)) - 2 * dz * (dy * (lz - pz) - dz * (ly - py))) / (dz ^ 2 + dy ^ 2 + dx ^ 2);
                //(2 * dy * (dy * (lz - pz) - dz * (ly - py)) + 2 * dx * (dx * (lz - pz) - dz * (lx - px))) / (dz ^ 2 + dy ^ 2 + dx ^ 2);
                //(2 * (dx * (lz - pz) - dz * (lx - px)) * (lz - pz) + 2 * (dx * (ly - py) - dy * (lx - px)) * (ly - py)) / (dz ^ 2 + dy ^ 2 + dx ^ 2) - (2 * dx * ((dy * (lz - pz) - dz * (ly - py)) ^ 2 + (dx * (lz - pz) - dz * (lx - px)) ^ 2 + (dx * (ly - py) - dy * (lx - px)) ^ 2)) / (dz ^ 2 + dy ^ 2 + dx ^ 2) ^ 2;
                //(2 * (dy * (lz - pz) - dz * (ly - py)) * (lz - pz) + 2 * (px - lx) * (dx * (ly - py) - dy * (lx - px))) / (dz ^ 2 + dy ^ 2 + dx ^ 2) - (2 * dy * ((dy * (lz - pz) - dz * (ly - py)) ^ 2 + (dx * (lz - pz) - dz * (lx - px)) ^ 2 + (dx * (ly - py) - dy * (lx - px)) ^ 2)) / (dz ^ 2 + dy ^ 2 + dx ^ 2) ^ 2;
                //(2 * (py - ly) * (dy * (lz - pz) - dz * (ly - py)) + 2 * (px - lx) * (dx * (lz - pz) - dz * (lx - px))) / (dz ^ 2 + dy ^ 2 + dx ^ 2) - (2 * dz * ((dy * (lz - pz) - dz * (ly - py)) ^ 2 + (dx * (lz - pz) - dz * (lx - px)) ^ 2 + (dx * (ly - py) - dy * (lx - px)) ^ 2)) / (dz ^ 2 + dy ^ 2 + dx ^ 2) ^ 2;

                derivs = DenseMatrix.Create(points.Length, 6, 0.0); // new Matrix(points.Length, 6); // Jacobi Matrix Ableitungen nach cx, cy, cz und r
                                                       // (pnts.x-cx)²+(pnts.y-cy)²+(pnts.z-cz)²-r² == 0
                double lx = parameters[0];
                double ly = parameters[1];
                double lz = parameters[2];
                double dx = parameters[3];
                double dy = parameters[4];
                double dz = parameters[5];
                double dl = dx * dx + dy * dy + dz * dz;

                for (int i = 0; i < points.Length; i++)
                {
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;
                    derivs[i, 0] = ((-2 * dz * (dx * (lz - pz) - dz * (lx - px))) - 2 * dy * (dx * (ly - py) - dy * (lx - px))) / dl;
                    derivs[i, 1] = (2 * dx * (dx * (ly - py) - dy * (lx - px)) - 2 * dz * (dy * (lz - pz) - dz * (ly - py))) / dl;
                    derivs[i, 2] = (2 * dy * (dy * (lz - pz) - dz * (ly - py)) + 2 * dx * (dx * (lz - pz) - dz * (lx - px))) / dl;
                    derivs[i, 3] = (2 * (dx * (lz - pz) - dz * (lx - px)) * (lz - pz) + 2 * (dx * (ly - py) - dy * (lx - px)) * (ly - py)) / (dl) - (2 * dx * (sqr(dy * (lz - pz) - dz * (ly - py)) + sqr(dx * (lz - pz) - dz * (lx - px)) + sqr(dx * (ly - py) - dy * (lx - px)))) / sqr(dl);
                    derivs[i, 4] = (2 * (dy * (lz - pz) - dz * (ly - py)) * (lz - pz) + 2 * (px - lx) * (dx * (ly - py) - dy * (lx - px))) / (dl) - (2 * dy * (sqr(dy * (lz - pz) - dz * (ly - py)) + sqr(dx * (lz - pz) - dz * (lx - px)) + sqr(dx * (ly - py) - dy * (lx - px)))) / sqr(dl);
                    derivs[i, 5] = (2 * (py - ly) * (dy * (lz - pz) - dz * (ly - py)) + 2 * (px - lx) * (dx * (lz - pz) - dz * (lx - px))) / (dl) - (2 * dz * (sqr(dy * (lz - pz) - dz * (ly - py)) + sqr(dx * (lz - pz) - dz * (lx - px)) + sqr(dx * (ly - py) - dy * (lx - px)))) / sqr(dl);
                }
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            GeoVector d = points[1] - points[0];
            double tl = d.TaxicabLength;
            for (int i = 2; i < points.Length; i++)
            {
                GeoVector di = points[i] - points[0];
                double li = di.TaxicabLength;
                if (li > tl)
                {
                    tl = li;
                    d = di;
                }
            }
            bool ok = gnm.Solve(new double[] { points[0].x, points[0].y, points[0].z, d.x, d.y, d.z }, 30, 1e-6, precision * precision, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                location = new GeoPoint(result[0], result[1], result[2]);
                direction = new GeoVector(result[3], result[4], result[5]);
                return Math.Sqrt(minError);
            }
            else
            {
                location = points[0];
                direction = d;
                return double.MaxValue;
            }
        }
        public static double CircleFit(IArray<GeoPoint> points, GeoPoint center, double radius, double precision, out Ellipse elli)
        {
            Plane pln = Plane.FromPoints(points.ToArray(), out double maxerror, out bool islinear);
            //// double maxerror = PlaneFit(points, precision, out Plane pln);
            if (islinear || maxerror > precision)
            {
                elli = null;
                return double.MaxValue;
            }

            GeoPoint2D[] points2d = new GeoPoint2D[points.Length];
            for (int i = 0; i < points2d.Length; i++)
            {
                points2d[i] = pln.Project(points[i]);
            }
            GeoPoint2D center2d;
            if (!center.IsValid)
            {
                BoundingRect ext = new BoundingRect(points2d);
                center2d = ext.GetCenter();
                radius = ext.Size / 4.0;
            }
            else
            {
                center2d = pln.Project(center);
            }
            // parameters: 0:cx, 1:cy, 3: radius

            void efunc(double[] parameters, out double[] values)
            {
                values = new double[points2d.Length];
                double cx = parameters[0];
                double cy = parameters[1];
                double r = parameters[2];
                for (int i = 0; i < points2d.Length; i++)
                {
                    double px = points2d[i].x;
                    double py = points2d[i].y;
                    values[i] = sqr(px - cx) + sqr(py - cy) - r * r;
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(points2d.Length, 3, 0); // Jacobi Matrix for (px-cx)²+(py-cy)²-r² == 0
                double cx = parameters[0];
                double cy = parameters[1];
                double r = parameters[2];

                for (int i = 0; i < points.Length; i++)
                {
                    double px = points2d[i].x;
                    double py = points2d[i].y;
                    derivs[i, 0] = -2 * (px - cx);
                    derivs[i, 1] = -2 * (py - cy);
                    derivs[i, 2] = -2 * r;
                }
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { center2d.x, center2d.y, radius }, 30, precision * precision * 1e-2, precision * precision, out double minError, out int numiter, out double[] result);
            if (ok || minError < 1e-9)
            {
                center = pln.ToGlobal(new GeoPoint2D(result[0], result[1]));
                elli = Ellipse.Construct();
                elli.SetCirclePlaneCenterRadius(pln, center, result[2]);
                return Math.Sqrt(minError);
            }
            else
            {
                elli = null;
                return double.MaxValue;
            }
        }
        public static double Ellipse2DFit(IArray<GeoPoint2D> points2d, GeoPoint2D center2d, double majrad, double minrad, double angle, double precision, out Ellipse2D ellipse)
        {
            // we need a distance function from a point to the ellipse. This was done with maxima, moving the point to a horizontally aligned ellipse around the origin.
            // Then calculating the angle and using the distance between the point of the ellipse at this angle and the test point.
            // maxima text:
            
            // e(u):=[a * cos(u), b * sin(u)]; /* horizontal ellipse around origin*/
            // s(p):=[(p[1] - cx) * cos(-w) - (p[2] - cy) * sin(-w), (p[1] - cx) * sin(-w) + (p[2] - cy) * cos(-w)]; /* from offset (cx,cy) and rotation (w) to origin and horizontal */
            // u(p):= atan2(s(p)[2] / b, s(p)[1] / a); /* parameter for point u */
            // d(p):= (e(u(p))[1] - s(p)[1]) ^ 2 + (e(u(p))[2] - s(p)[2]) ^ 2;
            // fo: openw("C:/Temp/ellipse.txt");
            // printf(fo, "d=~a;", d(p));
            // newline(fo);
            // printf(fo, "dcx=~a;", diff(d(p), cx, 1));
            // newline(fo);
            // printf(fo, "dcy=~a;", diff(d(p), cy, 1));
            // newline(fo);
            // printf(fo, "dw=~a;", diff(d(p), w, 1));
            // newline(fo);
            // printf(fo, "da=~a;", diff(d(p), a, 1));
            // newline(fo);
            // printf(fo, "db=~a;", diff(d(p), b, 1));
            // newline(fo);
            // close(fo);
            
            // The result was passed through CommonSubExpr to make it a little more readable.

            // parameters are cx,cy, a,b, w (center, major radius, minor radius, angle)
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[points2d.Length];
                double cx = parameters[0];
                double cy = parameters[1];
                double a = parameters[2];
                double b = parameters[3];
                double w = parameters[4];
                double sw = Math.Sin(w);
                double cw = Math.Cos(w);
                for (int i = 0; i < points2d.Length; i++)
                {
                    double sc19 = ((points2d[i].y - cy) * cw - (points2d[i].x - cx) * sw);
                    double sc18 = (points2d[i].y - cy) * sw + (points2d[i].x - cx) * cw;
                    double sc17 = sqr(sc19);
                    double sc16 = sqr(sc18);
                    double sc15 = (-(points2d[i].y - cy)) * sw - (points2d[i].x - cx) * cw;
                    double sc14 = sc16 / sqr(a) + sc17 / sqr(b);
                    double sc13 = 2.0 * sc19;
                    double sc12 = Math.Sqrt(sc14);
                    double sc11 = exp32(sc14);
                    double sc10 = 2.0 * sc11;
                    double sc9 = -cw / sc12;
                    double sc8 = cube(b) * sc11;
                    double sc7 = cube(a) * sc11;
                    double sc6 = 2.0 * sw * sc19 / sqr(b) - 2.0 * cw * sc18 / sqr(a);
                    double sc5 = sc19 / sc12;
                    double sc4 = (-(2.0 * sw * sc18)) / sqr(a) - 2.0 * cw * sc19 / sqr(b);
                    double sc3 = sc5 + (points2d[i].x - cx) * sw - (points2d[i].y - cy) * cw;
                    double sc2 = sc18 / sc12 - (points2d[i].y - cy) * sw - (points2d[i].x - cx) * cw;
                    double sc1 = sc13 * sc18 / sqr(a) + sc13 * sc15 / sqr(b);
                    values[i] = sqr(sc2) + sqr(sc3);
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(points2d.Length, 5, 0); // Jacobi Matrix 
                double cx = parameters[0];
                double cy = parameters[1];
                double a = parameters[2];
                double b = parameters[3];
                double w = parameters[4];
                double sw = Math.Sin(w);
                double cw = Math.Cos(w);
                for (int i = 0; i < points2d.Length; i++)
                {
                    double sc19 = (points2d[i].y - cy) * cw - (points2d[i].x - cx) * sw;
                    double sc18 = (points2d[i].y - cy) * sw + (points2d[i].x - cx) * cw;
                    double sc17 = sqr(sc19);
                    double sc16 = sqr(sc18);
                    double sc15 = (-(points2d[i].y - cy)) * sw - (points2d[i].x - cx) * cw;
                    double sc14 = sc16 / sqr(a) + sc17 / sqr(b);
                    double sc13 = 2.0 * sc19;
                    double sc12 = Math.Sqrt(sc14);
                    double sc11 = exp32(sc14);
                    double sc10 = 2.0 * sc11;
                    double sc9 = -cw / sc12;
                    double sc8 = cube(b) * sc11;
                    double sc7 = cube(a) * sc11;
                    double sc6 = 2.0 * sw * sc19 / sqr(b) - 2.0 * cw * sc18 / sqr(a);
                    double sc5 = sc19 / sc12;
                    double sc4 = (-(2.0 * sw * sc18)) / sqr(a) - 2.0 * cw * sc19 / sqr(b);
                    double sc3 = sc5 + (points2d[i].x - cx) * sw - (points2d[i].y - cy) * cw;
                    double sc2 = sc18 / sc12 - (points2d[i].y - cy) * sw - (points2d[i].x - cx) * cw;
                    double sc1 = sc13 * sc18 / sqr(a) + sc13 * sc15 / sqr(b);
                    derivs[i, 0] = 2.0 * (sc9 - sc18 * sc6 / sc10 + cw) * sc2 + 2.0 * (sw / sc12 - sc19 * sc6 / sc10 - sw) * sc3;
                    derivs[i, 1] = 2.0 * (-sw / sc12 - sc18 * sc4 / sc10 + sw) * sc2 + 2.0 * (sc9 - sc19 * sc4 / sc10 + cw) * sc3;
                    derivs[i, 2] = 2.0 * cube(sc18) * sc2 / sc7 + sc13 * sc16 * sc3 / sc7;
                    derivs[i, 3] = 2.0 * sc17 * sc18 * sc2 / sc8 + 2.0 * cube(sc19) * sc3 / sc8;
                    derivs[i, 4] = 2.0 * (sc5 - sc18 * sc1 / sc10 + (points2d[i].x - cx) * sw - (points2d[i].y - cy) * cw) * sc2 + 2.0 * sc3 * (sc15 / sc12 - sc19 * sc1 / sc10 + (points2d[i].y - cy) * sw + (points2d[i].x - cx) * cw);
                }
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            if (minrad == majrad) minrad = majrad * 0.9; // they should not be equal, because the derivation for the angle will be 0
            bool ok = gnm.Solve(new double[] { center2d.x, center2d.y, majrad, minrad, angle }, 30, precision * precision * 1e-2, precision * precision, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                GeoVector2D axdir = new GeoVector2D(new Angle(result[4]));
                ellipse = new Ellipse2D(new GeoPoint2D(result[0], result[1]), result[2] * axdir, result[3] * axdir.ToLeft());
                return Math.Sqrt(minError);
            }
            else
            {
                ellipse = null;
                return double.MaxValue;
            }
        }
        /// <summary>
        /// This is not good implemented. Do not use!
        /// </summary>
        /// <param name="points"></param>
        /// <param name="precision"></param>
        /// <param name="plane"></param>
        /// <returns></returns>
        public static double PlaneFit(IArray<GeoPoint> points, double precision, out Plane plane)
        {
            // We use a GaussNewtonMinimizer with the following parameters: n: normal of the plane (nx,ny,nz)
            // d distance of the plane from the origin, nx*px+ny*py+nz*pz=d
            // this is not good, because nx==ny==nz==d==0 is always a solution, so
            // sqr(c * pz + b * py + a * px - d) + sqr(a * a + b * b + c * c - 1); ( with (a,b,c)=(nx,ny,nz))
            // also forces the normal vector to have a length of 1

            void efunc(double[] parameters, out double[] values)
            {
                // error: a * px + b * py - c * pz - d (this may be positive or negative. It seems to be ok)
                // a * px + b * py - c * pz - d)^2+(a^2+b^2+c^2-1)^2;
                values = new double[points.Length];
                double a = parameters[0];
                double b = parameters[1];
                double c = parameters[2];
                double d = parameters[3];
                for (int i = 0; i < points.Length; i++)
                {
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;
                    values[i] = sqr(c * pz + b * py + a * px - d) + sqr(a * a + b * b + c * c - 1);
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {

                derivs = DenseMatrix.Create(points.Length, 4, 0); // Jacobi Matrix Derivations
                double a = parameters[0];
                double b = parameters[1];
                double c = parameters[2];
                double d = parameters[3];

                for (int i = 0; i < points.Length; i++)
                {
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;

                    derivs[i, 0] = 2 * px * (-c * pz + b * py + a * px - d) + 4 * a * (c * c + b * b + a * a - 1);
                    derivs[i, 1] = 2 * py * (-c * pz + b * py + a * px - d) + 4 * b * (c * c + b * b + a * a - 1);
                    derivs[i, 2] = 2 * pz * (-c * pz + b * py + a * px - d) + 4 * c * (c * c + b * b + a * a - 1);
                    derivs[i, 3] = -2 * (-c * pz + b * py + a * px - d);
                }
            }
            double[] sparams = new double[4];
            for (int i = 0; i < points.Length; i++)
            {
                for (int j = i + 1; j < points.Length; j++)
                {
                    for (int k = j + 1; k < points.Length; k++)
                    {
                        GeoVector dirx = points[j] - points[i];
                        GeoVector diry = points[k] - points[i];
                        GeoVector n = dirx ^ diry;
                        if (!n.IsNullVector())
                        {
                            Plane pln = new Plane(points[i], dirx, diry);
                            n.Norm();
                            double d = pln.Distance(GeoPoint.Origin);
                            sparams[0] = n.x;
                            sparams[1] = n.y;
                            sparams[2] = n.z;
                            sparams[3] = -d;
                            break;
                        }
                    }
                }
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(sparams, 30, 1e-6, precision * precision, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                GeoVector normal = new GeoVector(result[0], result[1], result[2]);
                GeoPoint loc = new GeoPoint(result[3] * result[0], result[3] * result[1], result[3] * result[2]);
                plane = new Plane(loc, normal);
                return Math.Sqrt(minError);
            }
            else
            {
                plane = Plane.XYPlane;
                return double.MaxValue;
            }
        }
        public static double ConeFitNew(IArray<GeoPoint> points, GeoPoint apex, GeoVector axis, double theta, double precision, out ConicalSurface cs)
        {
            // parameters: { lx,ly,lz,dx,dy,dz,t }
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[points.Length];
                GeoPoint l = new GeoPoint(parameters[0], parameters[1], parameters[2]);
                GeoVector d = new GeoVector(parameters[3], parameters[4], parameters[5]);
                double dl = d.Length;
                double t = parameters[6];
                for (int i = 0; i < points.Length; i++)
                {
                    GeoPoint p = points[i];
                    values[i] = sqr((d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x)) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - t);
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(points.Length, 7, 0); // Jacobi Matrix 
                GeoPoint l = new GeoPoint(parameters[0], parameters[1], parameters[2]);
                GeoVector d = new GeoVector(parameters[3], parameters[4], parameters[5]);
                double t = parameters[6];
                for (int i = 0; i < points.Length; i++)
                {
                    GeoPoint p = points[i];
                    double dl = d.Length;
                    double pll2 = (p - l) * (p - l);
                    double pll = (p - l).Length;
                    derivs[i, 0] = 2 * ((p.x - l.x) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - (d.x * (d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x))) / (exp32(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x)))) * ((d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x)) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - t);
                    derivs[i, 1] = 2 * ((p.y - l.y) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - (d.y * (d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x))) / (exp32(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x)))) * ((d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x)) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - t);
                    derivs[i, 2] = 2 * ((p.z - l.z) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - (d.z * (d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x))) / (exp32(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x)))) * ((d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x)) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - t);
                    derivs[i, 3] = 2 * (((p.x - l.x) * (d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x))) / (Math.Sqrt(dl) * exp32(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - d.x / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x)))) * ((d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x)) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - t);
                    derivs[i, 4] = 2 * (((p.y - l.y) * (d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x))) / (Math.Sqrt(dl) * exp32(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - d.y / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x)))) * ((d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x)) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - t);
                    derivs[i, 5] = 2 * (((p.z - l.z) * (d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x))) / (Math.Sqrt(dl) * exp32(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - d.z / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x)))) * ((d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x)) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - t);
                    derivs[i, 6] = -2 * ((d.z * (p.z - l.z) + d.y * (p.y - l.y) + d.x * (p.x - l.x)) / (Math.Sqrt(dl) * Math.Sqrt(sqr(p.z - l.z) + sqr(p.y - l.y) + sqr(p.x - l.x))) - t);
                }
            }

            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { apex.x, apex.y, apex.z, axis.x, axis.y, axis.z, Math.Cos(theta) }, 30, precision * precision * 0.01, precision * precision, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                Plane pln = new Plane(GeoPoint.Origin, new GeoVector(result[3], result[4], result[5])); // arbitrary x and y axis
                cs = new ConicalSurface(new GeoPoint(result[0], result[1], result[2]), pln.DirectionX, pln.DirectionY, pln.Normal, Math.Acos(result[6]));
                return Math.Sqrt(minError);
            }
            else
            {
                cs = null;
                return double.MaxValue;
            }
        }
        public static double ConeFit(IArray<GeoPoint> points, GeoPoint apex, GeoVector axis, double theta, double precision, out ConicalSurface cs)
        {
            /*
             * two rotations and a scaling on the z-axis convert the provided cone data into the unit cone.
             * First move the apex to the origin (px-ax), then rotate (by a) around the z-axis to align the axis with the y-axis, next rotate (by b) around the x-axis to 
             * coincide the cone axis with the z-axis, then scale (by f) the z-axis to make the (full) opening angle 90°
            RZ: matrix([cos(a),-sin(a),0],[sin(a),cos(a),0],[0,0,1]);
            RX: matrix([1,0,0],[0,cos(b),-sin(b)],[0,sin(b),cos(b)]);
            FZ: matrix([1,0,0],[0,1,0],[0,0,f]);
            P: matrix([px-ax],[py-ay],[pz-az]);
            E: FZ.RX.RZ.P;
            Q: E[1]^2+E[2]^2-E[3]^2;
            dfdax: diff(Q, ax, 1);
            dfday: diff(Q, ay, 1);
            dfdaz: diff(Q, az, 1);
            dfda: diff(Q,a,1);
            dfdb: diff(Q,b,1);
            yields:
            Q:[(cos(b)*(cos(a)*(py-ay)+sin(a)*(px-ax))-sin(b)*(pz-az))^2-f^2*(cos(b)*(pz-az)+sin(b)*(cos(a)*(py-ay)+sin(a)*(px-ax)))^2+(cos(a)*(px-ax)-sin(a)*(py-ay))^2];
            dfdax:[(-2*sin(a)*cos(b)*(cos(b)*(cos(a)*(py-ay)+sin(a)*(px-ax))-sin(b)*(pz-az)))+2*sin(a)*sin(b)*f^2*(cos(b)*(pz-az)+sin(b)*(cos(a)*(py-ay)+sin(a)*(px-ax)))-2*cos(a)*(cos(a)*(px-ax)-sin(a)*(py-ay))];
            dfday:[(-2*cos(a)*cos(b)*(cos(b)*(cos(a)*(py-ay)+sin(a)*(px-ax))-sin(b)*(pz-az)))+2*cos(a)*sin(b)*f^2*(cos(b)*(pz-az)+sin(b)*(cos(a)*(py-ay)+sin(a)*(px-ax)))+2*sin(a)*(cos(a)*(px-ax)-sin(a)*(py-ay))];
            dfdaz:[2*sin(b)*(cos(b)*(cos(a)*(py-ay)+sin(a)*(px-ax))-sin(b)*(pz-az))+2*cos(b)*f^2*(cos(b)*(pz-az)+sin(b)*(cos(a)*(py-ay)+sin(a)*(px-ax)))];
            dfda:[2*cos(b)*(cos(a)*(px-ax)-sin(a)*(py-ay))*(cos(b)*(cos(a)*(py-ay)+sin(a)*(px-ax))-sin(b)*(pz-az))-2*sin(b)*f^2*(cos(a)*(px-ax)-sin(a)*(py-ay))*(cos(b)*(pz-az)+sin(b)*(cos(a)*(py-ay)+sin(a)*(px-ax)))+2*((-cos(a)*(py-ay))-sin(a)*(px-ax))*(cos(a)*(px-ax)-sin(a)*(py-ay))];
            dfdb:[2*((-cos(b)*(pz-az))-sin(b)*(cos(a)*(py-ay)+sin(a)*(px-ax)))*(cos(b)*(cos(a)*(py-ay)+sin(a)*(px-ax))-sin(b)*(pz-az))-2*f^2*(cos(b)*(pz-az)+sin(b)*(cos(a)*(py-ay)+sin(a)*(px-ax)))*(cos(b)*(cos(a)*(py-ay)+sin(a)*(px-ax))-sin(b)*(pz-az))];
            */
#if DEBUG
            GeoObjectList dbglst = new GeoObjectList();
#endif
            // parameters: { ax, ay, az, a, b, f }
            void efunc(double[] parameters, out double[] values)
            {
#if DEBUG
                ConicalSurface csdbg = new ConicalSurface(ModOp.Translate(parameters[0], parameters[1], parameters[2]) * ModOp.Rotate(GeoVector.ZAxis, -parameters[3]) * ModOp.Rotate(GeoVector.XAxis, -parameters[4]) * ModOp.Scale(parameters[5], parameters[5], 1));
                Face fcdbg = Face.MakeFace(csdbg, new BoundingRect(Math.PI, -0.005, 2 * Math.PI, -0.01));
                dbglst.Add(fcdbg);
#endif
                values = new double[points.Length];
                double ax = parameters[0];
                double ay = parameters[1];
                double az = parameters[2];
                double a = parameters[3];
                double b = parameters[4];
                double f = parameters[5];
                for (int i = 0; i < points.Length; i++)
                {
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;
                    values[i] = sqr(Math.Cos(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax)) - Math.Sin(b) * (pz - az)) - f * f * sqr(Math.Cos(b) * (pz - az) + Math.Sin(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax))) + sqr(Math.Cos(a) * (px - ax) - Math.Sin(a) * (py - ay));
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(points.Length, 6, 0); // Jacobi Matrix 
                double ax = parameters[0];
                double ay = parameters[1];
                double az = parameters[2];
                double a = parameters[3];
                double b = parameters[4];
                double f = parameters[5];
                double ff = f * f;
                for (int i = 0; i < points.Length; i++)
                {
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;
                    derivs[i, 0] = (-2 * Math.Sin(a) * Math.Cos(b) * (Math.Cos(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax)) - Math.Sin(b) * (pz - az))) + 2 * Math.Sin(a) * Math.Sin(b) * ff * (Math.Cos(b) * (pz - az) + Math.Sin(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax))) - 2 * Math.Cos(a) * (Math.Cos(a) * (px - ax) - Math.Sin(a) * (py - ay));
                    derivs[i, 1] = (-2 * Math.Cos(a) * Math.Cos(b) * (Math.Cos(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax)) - Math.Sin(b) * (pz - az))) + 2 * Math.Cos(a) * Math.Sin(b) * f * (Math.Cos(b) * (pz - az) + Math.Sin(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax))) + 2 * Math.Sin(a) * (Math.Cos(a) * (px - ax) - Math.Sin(a) * (py - ay));
                    derivs[i, 2] = 2 * Math.Sin(b) * (Math.Cos(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax)) - Math.Sin(b) * (pz - az)) + 2 * Math.Cos(b) * f * (Math.Cos(b) * (pz - az) + Math.Sin(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax)));
                    derivs[i, 3] = 2 * Math.Cos(b) * (Math.Cos(a) * (px - ax) - Math.Sin(a) * (py - ay)) * (Math.Cos(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax)) - Math.Sin(b) * (pz - az)) - 2 * Math.Sin(b) * f * (Math.Cos(a) * (px - ax) - Math.Sin(a) * (py - ay)) * (Math.Cos(b) * (pz - az) + Math.Sin(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax))) + 2 * ((-Math.Cos(a) * (py - ay)) - Math.Sin(a) * (px - ax)) * (Math.Cos(a) * (px - ax) - Math.Sin(a) * (py - ay));
                    derivs[i, 4] = 2 * ((-Math.Cos(b) * (pz - az)) - Math.Sin(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax))) * (Math.Cos(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax)) - Math.Sin(b) * (pz - az)) - 2 * f * (Math.Cos(b) * (pz - az) + Math.Sin(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax))) * (Math.Cos(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax)) - Math.Sin(b) * (pz - az));
                    derivs[i, 5] = -2 * f * sqr(Math.Cos(b) * (pz - az) + Math.Sin(b) * (Math.Cos(a) * (py - ay) + Math.Sin(a) * (px - ax)));
                }
            }
            SweepAngle toY = Angle.A90 - new Angle(axis.x, axis.y);
            GeoVector axisY = ModOp.Rotate(GeoVector.ZAxis, toY) * axis;
            SweepAngle toZ = Angle.A90 - new Angle(axisY.y, axisY.z);
            GeoVector axisZ = ModOp.Rotate(GeoVector.XAxis, toZ) * axisY;

            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { apex.x, apex.y, apex.z, toY, toZ, theta }, 30, precision * precision * 0.01, precision * precision, out double minError, out int numiter, out double[] result);
            // parameters: 0: apex.x, 1:apex.y, 2: apex.z, 3: a (rotation around z-axis), 4: b (rotation around z-axis), 5: f (stretching factor in z)
            if (ok)
            {
                cs = new ConicalSurface(ModOp.Translate(result[0], result[1], result[2]) * ModOp.Rotate(GeoVector.ZAxis, -result[3]) * ModOp.Rotate(GeoVector.XAxis, -result[4]) * ModOp.Scale(result[5], result[5], 1));
                return Math.Sqrt(minError);
            }
            else
            {
                cs = null;
                return double.MaxValue;
            }
        }
        public static double ConeFitOld(GeoPoint[] points, GeoPoint apex, GeoVector axis, double theta, out ConicalSurface cs)
        {
            // according to wikipedia (a cone with apex=(0,0,0), d: axis and theta: opening angle
            //F(u) = (u * d)^2 - (d*d)*(u*u)*cos(theta)
            // with apex=c:
            // F(p) = ((p-c)*d)^2 - (d*d)*((p-c)*(p-c))*(cos(theta))^2
            //ax: px - cx;
            //ay: py - cy;
            //az: pz - cz;
            //f: (ax * dx + ay * dy + az * dz) ^ 2 - (dx * dx + dy * dy + dz * dz) * (ax * ax + ay * ay + az * az) * (cos(t)) ^ 2;
            //dfdcx: diff(f, cx, 1);
            //dfdcy: diff(f, cy, 1);
            //dfdcz: diff(f, cz, 1);
            //dfdnx: diff(f, dx, 1);
            //dfdny: diff(f, dy, 1);
            //dfdnz: diff(f, dz, 1);
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

            /*
            RZ: matrix([cos(a),-sin(a),0],[sin(a),cos(a),0],[0,0,1]);
            RX: matrix([1,0,0],[0,cos(b),-sin(b)],[0,sin(b),cos(b)]);
            FZ: matrix([1,0,0],[0,1,0],[0,0,f]);
            P: matrix([px-ax],[py-ay],[pz-az]);
            E: FZ.RX.RZ.P;
            Q: E[1]^2+E[2]^2-E[3]^2;
            dfdax: diff(Q, ax, 1);
            dfday: diff(Q, ay, 1);
            dfdaz: diff(Q, az, 1);
            dfda: diff(Q,a,1);
            dfdb: diff(Q,b,1);
            */

            void efunc(double[] parameters, out double[] values)
            {
                values = new double[points.Length];
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];
                double dx = parameters[3];
                double dy = parameters[4];
                double dz = parameters[5];
                double t = parameters[6];
                for (int i = 0; i < points.Length; i++)
                {
                    GeoVector a = points[i] - new GeoPoint(cx, cy, cz);
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;
                    values[i] = sqr(dz * (pz - cz) + dy * (py - cy) + dx * (px - cx)) - (dz * dz + dy * dy + dx * dx) * (sqr(pz - cz) + sqr(py - cy) + sqr(px - cx)) * sqr(Math.Cos(t));
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(points.Length, 7, 0); // Jacobi Matrix 
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];
                double dx = parameters[3];
                double dy = parameters[4];
                double dz = parameters[5];
                double t = parameters[6];
                double dd = (dz * dz + dy * dy + dx * dx);
                for (int i = 0; i < points.Length; i++)
                {
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;
                    double pc = sqr(pz - cz) + sqr(py - cy) + sqr(px - cx);
                    derivs[i, 0] = 2 * dd * (px - cx) * sqr(Math.Cos(t)) - 2 * dx * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx));
                    derivs[i, 1] = 2 * dd * (py - cy) * sqr(Math.Cos(t)) - 2 * dy * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx));
                    derivs[i, 2] = 2 * dd * (pz - cz) * sqr(Math.Cos(t)) - 2 * dz * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx));
                    derivs[i, 3] = 2 * (px - cx) * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx)) - 2 * dx * (sqr(pz - cz) + sqr(py - cy) + sqr(px - cx)) * sqr(Math.Cos(t));
                    derivs[i, 4] = 2 * (py - cy) * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx)) - 2 * dy * (sqr(pz - cz) + sqr(py - cy) + sqr(px - cx)) * sqr(Math.Cos(t));
                    derivs[i, 5] = 2 * (pz - cz) * (dz * (pz - cz) + dy * (py - cy) + dx * (px - cx)) - 2 * dz * (sqr(pz - cz) + sqr(py - cy) + sqr(px - cx)) * sqr(Math.Cos(t));
                    derivs[i, 6] = 2 * dd * (sqr(pz - cz) + sqr(py - cy) + sqr(px - cx)) * Math.Cos(t) * Math.Sin(t);
                }
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { apex.x, apex.y, apex.z, axis.x, axis.y, axis.z, theta }, 30, 1e-6, 1e-9, out double minError, out int numiter, out double[] result);
            // parameters: 0: apex.x, 1:apex.y, 2: apex.z, 3: axis.x, 4: axis.y, 5: axis.z, 6: theta
            if (ok)
            {
                apex = new GeoPoint(result[0], result[1], result[2]);
                axis = new GeoVector(result[3], result[4], result[5]);
                Plane pln = new Plane(apex, axis); // arbitrary x and y direction
                theta = result[6];
                cs = new ConicalSurface(apex, pln.DirectionX, pln.DirectionY, pln.Normal, theta);
                return minError;
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
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[points.Length];
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];
                double nx = parameters[3];
                double ny = parameters[4];
                double nz = parameters[5];
                double r = parameters[6];
                double R2 = nx * nx + ny * ny + nz * nz;
                double R = Math.Sqrt(R2);
                for (int i = 0; i < points.Length; i++)
                {
                    GeoVector d = points[i] - new GeoPoint(cx, cy, cz);
                    values[i] = r * r - (sqr(nx * d.x + ny * d.y + nz * d.z) / R2 + sqr(Math.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z - sqr(nx * d.x + ny * d.y + nz * d.z) / R2) - R));
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(points.Length, 7, 0); // Jacobi Matrix 
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];
                double nx = parameters[3];
                double ny = parameters[4];
                double nz = parameters[5];
                double r = parameters[6];
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
            }
#if MATHNETx
                MathNet.Numerics.LinearAlgebra.Vector<double> oeFunc(MathNet.Numerics.LinearAlgebra.Vector<double> parameters, MathNet.Numerics.LinearAlgebra.Vector<double> x)
                {
                    efunc(parameters.ToArray(), out double[] errorValues);
                    return MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(errorValues);
                }
                MathNet.Numerics.LinearAlgebra.Matrix<double> ojfunc(MathNet.Numerics.LinearAlgebra.Vector<double> parameters, MathNet.Numerics.LinearAlgebra.Vector<double> x)
                {
                    jfunc(parameters.ToArray(), out Matrix partialDerivations);
                    var derivs = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(partialDerivations.RowCount, partialDerivations.ColumnCount);
                    for (int i = 0; i < partialDerivations.RowCount; i++)
                    {
                        for (int j = 0; j < partialDerivations.ColumnCount; j++)
                        {
                            derivs[i, j] = partialDerivations[i, j];
                        }
                    }
                    return derivs;
                }
                MathNet.Numerics.Optimization.LevenbergMarquardtMinimizer minimizer = new MathNet.Numerics.Optimization.LevenbergMarquardtMinimizer(0.001, 1e-20, 1e-20, 1e-20, 20);

                var obj = MathNet.Numerics.Optimization.ObjectiveFunction.NonlinearModel(oeFunc, ojfunc, MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(points.Length), MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(points.Length));
                var lmresult = minimizer.FindMinimum(obj, MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(new double[] { 0, 0, 0, 0, 0, 1, 1 }));

#endif
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { center.x, center.y, center.z, axis.x, axis.y, axis.z, minrad }, 30, 1e-6, precision * precision, out double minError, out int numiter, out double[] result);
            // parameters: 0:cx, 1:cy, 2: cz, 3: nx, 4: ny, 5: nz, 6: r (majorRadius is sqrt(n*n), r: minorRadius)
            if (ok)
            {
                GeoPoint loc = new GeoPoint(result[0], result[1], result[2]);
                GeoVector normal = new GeoVector(result[3], result[4], result[5]);
                double majrad = normal.Length;
                Plane pln = new Plane(loc, normal);
                ts = new ToroidalSurface(pln.Location, pln.DirectionX, pln.DirectionY, pln.Normal, majrad, Math.Abs(result[6]));
                return Math.Sqrt(minError);
            }
            else
            {
                ts = null;
                return double.MaxValue;
            }
        }
        public static double SphereFit(IArray<GeoPoint> pnts, GeoPoint center, double radius, double precision, out SphericalSurface ss)
        {
            // parameters: 0:cx, 1:cy, 2: cz, 3: r
            void efunc(double[] parameters, out double[] values)
            {
                // (pnts.x-cx)²+(pnts.y-cy)²+(pnts.z-cz)²==r²
                values = new double[pnts.Length];
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];
                double r = parameters[3];
                for (int i = 0; i < pnts.Length; i++)
                {
                    values[i] = sqr(pnts[i].x - cx) + sqr(pnts[i].y - cy) + sqr(pnts[i].z - cz) - sqr(r);
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(pnts.Length, 4, 0); // Jacobi Matrix Ableitungen nach cx, cy, cz und r
                // (pnts.x-cx)²+(pnts.y-cy)²+(pnts.z-cz)²-r² == 0
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];
                double r = parameters[3];

                for (int i = 0; i < pnts.Length; i++)
                {
                    derivs[i, 0] = -2 * (pnts[i].x - cx);
                    derivs[i, 1] = -2 * (pnts[i].y - cy);
                    derivs[i, 2] = -2 * (pnts[i].z - cz);
                    derivs[i, 3] = -2 * r;
                }
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { center.x, center.y, center.z, radius }, 30, 1e-6, precision * precision, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                center = new GeoPoint(result[0], result[1], result[2]);
                radius = Math.Abs(result[3]);
                ss = new SphericalSurface(center, radius * GeoVector.XAxis, radius * GeoVector.YAxis, radius * GeoVector.ZAxis);
                return Math.Sqrt(minError);
            }
            else
            {
                ss = null;
                return double.MaxValue;
            }
        }
        public static double SphereRadiusFit(IArray<GeoPoint> pnts, GeoPoint center, double radius, double precision, out SphericalSurface ss)
        {
            // parameters: 0:cx, 1:cy, 2: cz
            void efunc(double[] parameters, out double[] values)
            {
                // (pnts.x-cx)²+(pnts.y-cy)²+(pnts.z-cz)²==r²
                values = new double[pnts.Length];
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];
                for (int i = 0; i < pnts.Length; i++)
                {
                    values[i] = sqr(pnts[i].x - cx) + sqr(pnts[i].y - cy) + sqr(pnts[i].z - cz) - sqr(radius);
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(pnts.Length, 3, 0); // Jacobi Matrix Ableitungen nach cx, cy, cz und r
                                                     // (pnts.x-cx)²+(pnts.y-cy)²+(pnts.z-cz)²-r² == 0
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];

                for (int i = 0; i < pnts.Length; i++)
                {
                    derivs[i, 0] = -2 * (pnts[i].x - cx);
                    derivs[i, 1] = -2 * (pnts[i].y - cy);
                    derivs[i, 2] = -2 * (pnts[i].z - cz);
                }
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { center.x, center.y, center.z }, 30, 1e-6, precision * precision, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                center = new GeoPoint(result[0], result[1], result[2]);
                ss = new SphericalSurface(center, radius * GeoVector.XAxis, radius * GeoVector.YAxis, radius * GeoVector.ZAxis);
                return Math.Sqrt(minError);
            }
            else
            {
                ss = null;
                return double.MaxValue;
            }

        }
        public static double ThreeCurveIntersection(ICurve crv1, ICurve crv2, ICurve crv3, ref double u1, ref double u2, ref double u3)
        {
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[3];
                GeoPoint c = crv1.PointAt(parameters[0]);
                GeoPoint d = crv2.PointAt(parameters[1]);
                GeoPoint e = crv3.PointAt(parameters[2]);
                values[0] = sqr(c.x - d.x) + sqr(d.x - e.x) + sqr(e.x - c.x);
                values[1] = sqr(c.y - d.y) + sqr(d.y - e.y) + sqr(e.y - c.y);
                values[2] = sqr(c.z - d.z) + sqr(d.z - e.z) + sqr(e.z - c.z);
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(3, 3, 0);
                double uu1 = parameters[0];
                double uu2 = parameters[1];
                double uu3 = parameters[2];
                GeoPoint c = crv1.PointAt(parameters[0]);
                GeoPoint d = crv2.PointAt(parameters[1]);
                GeoPoint e = crv3.PointAt(parameters[2]);
                GeoVector ct = crv1.DirectionAt(uu1);
                GeoVector dt = crv2.DirectionAt(uu2);
                GeoVector et = crv3.DirectionAt(uu3);

                derivs[0, 0] = 2 * ct.x * (c.x - e.x) - 2 * (d.x - c.x) * ct.x;
                derivs[1, 0] = 2 * ct.y * (c.y - e.y) - 2 * (d.y - c.y) * ct.y;
                derivs[2, 0] = 2 * ct.z * (c.z - e.z) - 2 * (d.z - c.z) * ct.z;
                derivs[0, 1] = 2 * dt.x * (d.x - e.x) - 2 * (c.x - d.x) * dt.x;
                derivs[1, 1] = 2 * dt.y * (d.y - e.y) - 2 * (c.y - d.y) * dt.y;
                derivs[2, 1] = 2 * dt.z * (d.z - e.z) - 2 * (c.z - d.z) * dt.z;
                derivs[0, 2] = 2 * et.x * (e.x - c.x) - 2 * (d.x - e.x) * et.x;
                derivs[1, 2] = 2 * et.y * (e.y - c.y) - 2 * (d.y - e.y) * et.y;
                derivs[2, 2] = 2 * et.z * (e.z - c.z) - 2 * (d.z - e.z) * et.z;
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { u1, u2, u3 }, 30, 1e-6, 1e-12, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                u1 = result[0];
                u2 = result[1];
                u3 = result[2];
                return Math.Sqrt(minError);
            }
            else
            {
                return double.MaxValue;
            }
        }
        public static double CylinderFit(IArray<GeoPoint> pnts, GeoPoint center, GeoVector axis, double radius, double precision, out CylindricalSurface cs)
        {
            /*
             * according to (p-a)*(p-a) = r² + ((p-a)*d)² , where a is an axis point and d is the axis direction, we get
             * (-r^2)-(dz*(pz-az)+dy*(py-ay)+dx*(px-ax))^2+(pz-az)^2+(py-ay)^2+(px-ax)^2;
             * 
             * Now we choose az=0 and transform the input data so that center is origin and axis is z-axis.
             * 
             * and the derivatives
                2*dx*(dz*(pz-0)+dy*(py-ay)+dx*(px-ax))-2*(px-ax);
                2*dy*(dz*(pz-0)+dy*(py-ay)+dx*(px-ax))-2*(py-ay);
                2*dz*(dz*(pz-0)+dy*(py-ay)+dx*(px-ax))-2*(pz-0);
                -2*(px-ax)*(dz*(pz-0)+dy*(py-ay)+dx*(px-ax));
                -2*(py-ay)*(dz*(pz-0)+dy*(py-ay)+dx*(px-ax));
                -2*(pz-0)*(dz*(pz-0)+dy*(py-ay)+dx*(px-ax));
                -2*r;

            */
            // parameters: 0: ax, 1: ay, 2: dx, 3: dy, 4: dz, 5: r (a: location, d: direction, r: radius)
            GeoPoint[] points = new GeoPoint[pnts.Length];
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[points.Length];
                double ax = parameters[0];
                double ay = parameters[1];
                double dx = parameters[2];
                double dy = parameters[3];
                double dz = parameters[4];
                double r = parameters[5];
                for (int i = 0; i < points.Length; i++)
                {
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;
                    values[i] = (-r * r) - sqr(dz * (pz) + dy * (py - ay) + dx * (px - ax)) + sqr(pz) + sqr(py - ay) + sqr(px - ax);
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(points.Length, 6, 0); // Jacobi Matrix 
                double ax = parameters[0];
                double ay = parameters[1];
                double dx = parameters[2];
                double dy = parameters[3];
                double dz = parameters[4];
                double r = parameters[5];

                for (int i = 0; i < points.Length; i++)
                {
                    double px = points[i].x;
                    double py = points[i].y;
                    double pz = points[i].z;

                    derivs[i, 0] = 2 * dx * (dz * (pz) + dy * (py - ay) + dx * (px - ax)) - 2 * (px - ax);
                    derivs[i, 1] = 2 * dy * (dz * (pz) + dy * (py - ay) + dx * (px - ax)) - 2 * (py - ay);
                    derivs[i, 2] = -2 * (px - ax) * (dz * (pz) + dy * (py - ay) + dx * (px - ax));
                    derivs[i, 3] = -2 * (py - ay) * (dz * (pz) + dy * (py - ay) + dx * (px - ax));
                    derivs[i, 4] = -2 * (pz) * (dz * (pz) + dy * (py - ay) + dx * (px - ax));
                    derivs[i, 5] = -2 * r;
                }
            }
            ModOp toZAxis = ModOp.Translate(-center.x, -center.y, -center.z) * ModOp.Rotate(center, axis, GeoVector.ZAxis);
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = toZAxis * pnts[i];
            }
            //axis = toZAxis * axis;
            //center = toZAxis * center;
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { 0.0, 0.0, 0.0, 0.0, 1.0, radius }, 30, 1e-6, precision * precision, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                ModOp fromZAxis = toZAxis.GetInverse();
                center = fromZAxis * new GeoPoint(result[0], result[1], 0.0);
                axis = fromZAxis * new GeoVector(result[2], result[3], result[4]).Normalized;
                axis.ArbitraryNormals(out GeoVector dirx, out GeoVector diry);
                radius = Math.Abs(result[5]);
                cs = new CylindricalSurface(center, radius * dirx, radius * diry, axis);
#if DEBUG
                double dd = 0.0;
                for (int i = 0; i < points.Length; i++)
                {
                    dd += cs.GetDistance(points[i]);
                }
#endif
                return Math.Sqrt(minError);
            }
            else
            {
                cs = null;
                return double.MaxValue;
            }
        }
        public static double QuadricFit(IArray<GeoPoint> pnts)
        {
            /*  A Quadric according to https://lcvmwww.epfl.ch/PROJECTS/classification_quad.pdf
             *  A B C D
             *  B E F G
             *  C F H I
             *  D G I J
                H*z^2+2*F*y*z+2*C*x*z+2*I*z+E*y^2+2*B*x*y+2*G*y+A*x^2+2*D*x+J;
                x^2;
                2*x*y;
                2*x*z;
                2*x;
                y^2;
                2*y*z;
                2*y;
                z^2;
                2*z;
                1;
            */
            // parameters: 0: ax, 1: ay, 2: az, 3: dx, 4: dy, 5: dz, 6: r (a: location, d: direction, r: radius)
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[pnts.Length];
                double A = parameters[0];
                double B = parameters[1];
                double C = parameters[2];
                double D = parameters[3];
                double E = parameters[4];
                double F = parameters[5];
                double G = parameters[6];
                double H = parameters[7];
                double I = parameters[8];
                double J = -1.0; // parameters[9];
                for (int i = 0; i < pnts.Length; i++)
                {
                    double x = pnts[i].x;
                    double y = pnts[i].y;
                    double z = pnts[i].z;
                    values[i] = H * z * z + 2 * F * y * z + 2 * C * x * z + 2 * I * z + E * y * y + 2 * B * x * y + 2 * G * y + A * x * x + 2 * D * x + J;
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(pnts.Length, 9, 0); // Jacobi Matrix 

                for (int i = 0; i < pnts.Length; i++)
                {
                    double x = pnts[i].x;
                    double y = pnts[i].y;
                    double z = pnts[i].z;

                    derivs[i, 0] = x * x;
                    derivs[i, 1] = 2 * x * y;
                    derivs[i, 2] = 2 * x * z;
                    derivs[i, 3] = 2 * x;
                    derivs[i, 4] = y * y;
                    derivs[i, 5] = 2 * y * z;
                    derivs[i, 6] = 2 * y;
                    derivs[i, 7] = z * z;
                    derivs[i, 8] = 2 * z;
                    //derivs[i, 9] = 1;
                }
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            GeoPoint cnt = new GeoPoint(pnts.ToArray());
            double j = cnt.z * cnt.z + 2 * cnt.y * cnt.z + 2 * cnt.x * cnt.z + 2 * cnt.z + cnt.y * cnt.y + 2 * cnt.x * cnt.y + 2 * cnt.y + cnt.x * cnt.x + 2 * cnt.x;
            bool ok = gnm.Solve(new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }, 30, 1e-6, 1e-6, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                double A = result[0];
                double B = result[1];
                double C = result[2];
                double D = result[3];
                double E = result[4];
                double F = result[5];
                double G = result[6];
                double H = result[7];
                double I = result[8];
                double J = -1.0; //  result[9];
                double r = Math.Sqrt(1.0 / Math.Abs(A));
                Matrix MS = DenseMatrix.OfArray(new double[,] { { A, B, C, D }, { B, E, F, G }, { C, F, H, I }, { D, G, I, J } });
                var ed = MS.Evd();
                Vector<System.Numerics.Complex> ev = ed.EigenValues;
                int a = 0, b = 0, c = 0;
                for (int i = 0; i < ev.Count; i++)
                {
                    if (ev[i].Imaginary == 0.0)
                    {
                        if (ev[i].Real > 0) ++a;
                        else if (ev[i].Real < 0) ++b;
                        else ++c;
                    }
                }
                Matrix MQ = DenseMatrix.OfArray(new double[,] { { A, B, C }, { B, E, F }, { C, F, H } });
                ed = MQ.Evd();
                ev = ed.EigenValues;
                int aq = 0, bq = 0, cq = 0;
                for (int i = 0; i < ev.Count; i++)
                {
                    if (ev[i].Imaginary == 0.0)
                    {
                        if (ev[i].Real > 0) ++aq;
                        else if (ev[i].Real < 0) ++bq;
                        else ++cq;
                    }
                }
                Matrix trans = (Matrix)(MQ.Inverse() * DenseMatrix.OfRowArrays(new double[] { D, G, I }));
                GeoPoint mp = new GeoPoint(trans[0, 0], trans[1, 0], trans[2, 0]);
                GeoPoint mmp = new GeoPoint(-trans[0, 0], -trans[1, 0], -trans[2, 0]); // der liegt auf der Achse
                return Math.Sqrt(minError);
            }
            else
            {
                return double.MaxValue;
            }
        }
        public static double CylinderFitOld(IArray<GeoPoint> pnts, GeoPoint center, GeoVector axis, double radius, double precision, out CylindricalSurface cs)
        {
            /* maxima: disance point to line
                load("vect"); 
                norm(x) := sqrt(x . x);
                loc: [lx,ly,lz];
                dir: [dx,dy,dz];
                p: [px,py,pz];
                v1: express(dir~(loc-p));
                dist: norm(v1)/norm(dir);
                difflx: diff(dist-r,lx,1);
                diffly: diff(dist-r,ly,1);
                difflz: diff(dist-r,lz,1);
                diffdx: diff(dist-r,dx,1);
                diffdy: diff(dist-r,dy,1);
                diffdz: diff(dist-r,dz,1);
                diffr: diff(dist-r,r,1);
                yields:
                dist:sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2)/sqrt(dz^2+dy^2+dx^2);
                difflx:(2*dz*(dz*(lx-px)-dx*(lz-pz))-2*dy*(dx*(ly-py)-dy*(lx-px)))/(2*sqrt(dz^2+dy^2+dx^2)*sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2));
                diffly:(2*dx*(dx*(ly-py)-dy*(lx-px))-2*dz*(dy*(lz-pz)-dz*(ly-py)))/(2*sqrt(dz^2+dy^2+dx^2)*sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2));
                difflz:(2*dy*(dy*(lz-pz)-dz*(ly-py))-2*dx*(dz*(lx-px)-dx*(lz-pz)))/(2*sqrt(dz^2+dy^2+dx^2)*sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2));
                diffdx:(2*(dz*(lx-px)-dx*(lz-pz))*(pz-lz)+2*(dx*(ly-py)-dy*(lx-px))*(ly-py))/(2*sqrt(dz^2+dy^2+dx^2)*sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2))-(dx*sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2))/(dz^2+dy^2+dx^2)^(3/2);
                diffdy:(2*(dy*(lz-pz)-dz*(ly-py))*(lz-pz)+2*(px-lx)*(dx*(ly-py)-dy*(lx-px)))/(2*sqrt(dz^2+dy^2+dx^2)*sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2))-(dy*sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2))/(dz^2+dy^2+dx^2)^(3/2);
                diffdz:(2*(py-ly)*(dy*(lz-pz)-dz*(ly-py))+2*(lx-px)*(dz*(lx-px)-dx*(lz-pz)))/(2*sqrt(dz^2+dy^2+dx^2)*sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2))-(dz*sqrt((dy*(lz-pz)-dz*(ly-py))^2+(dz*(lx-px)-dx*(lz-pz))^2+(dx*(ly-py)-dy*(lx-px))^2))/(dz^2+dy^2+dx^2)^(3/2);
                diffr:-1;
                simplified:
                dist=sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px)))))/sqrt((sqr(dz)+sqr(dy)+sqr(dx)));;
                difflx=(2.0*dz*(dz*(lx-px)-dx*(lz-pz))-2.0*dy*(dx*(ly-py)-dy*(lx-px)))/((2.0*sqrt((sqr(dz)+sqr(dy)+sqr(dx))))*sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px))))));;
                diffly=(2.0*dx*(dx*(ly-py)-dy*(lx-px))-2.0*dz*(dy*(lz-pz)-dz*(ly-py)))/((2.0*sqrt((sqr(dz)+sqr(dy)+sqr(dx))))*sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px))))));;
                difflz=(2.0*dy*(dy*(lz-pz)-dz*(ly-py))-2.0*dx*(dz*(lx-px)-dx*(lz-pz)))/((2.0*sqrt((sqr(dz)+sqr(dy)+sqr(dx))))*sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px))))));;
                diffdx=((2.0*(dz*(lx-px)-dx*(lz-pz)))*(pz-lz)+(2.0*(dx*(ly-py)-dy*(lx-px)))*(ly-py))/((2.0*sqrt((sqr(dz)+sqr(dy)+sqr(dx))))*sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px))))))-(dx*sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px))))))/exp((sqr(dz)+sqr(dy)+sqr(dx)), (3.0/2.0));;
                diffdy=((2.0*(dy*(lz-pz)-dz*(ly-py)))*(lz-pz)+(2.0*(px-lx))*(dx*(ly-py)-dy*(lx-px)))/((2.0*sqrt((sqr(dz)+sqr(dy)+sqr(dx))))*sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px))))))-(dy*sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px))))))/exp((sqr(dz)+sqr(dy)+sqr(dx)), (3.0/2.0));;
                diffdz=((2.0*(py-ly))*(dy*(lz-pz)-dz*(ly-py))+(2.0*(lx-px))*(dz*(lx-px)-dx*(lz-pz)))/((2.0*sqrt((sqr(dz)+sqr(dy)+sqr(dx))))*sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px))))))-(dz*sqrt((sqr((dy*(lz-pz)-dz*(ly-py)))+sqr((dz*(lx-px)-dx*(lz-pz)))+sqr((dx*(ly-py)-dy*(lx-px))))))/exp((sqr(dz)+sqr(dy)+sqr(dx)), (3.0/2.0));
             */
            // parameters: 0: lx, 1: ly, 2: lz, 3: dx, 4: dy, 5: dz, 6: r (l: location, d: direction, r: radius)
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[pnts.Length];
                double lx = parameters[0];
                double ly = parameters[1];
                double lz = parameters[2];
                double dx = parameters[3];
                double dy = parameters[4];
                double dz = parameters[5];
                double r = parameters[6];
                for (int i = 0; i < pnts.Length; i++)
                {
                    double px = pnts[i].x;
                    double py = pnts[i].y;
                    double pz = pnts[i].z;
                    values[i] = Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px))))) / Math.Sqrt((sqr(dz) + sqr(dy) + sqr(dx))) - r;
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(pnts.Length, 7, 0); // Jacobi Matrix 
                double lx = parameters[0];
                double ly = parameters[1];
                double lz = parameters[2];
                double dx = parameters[3];
                double dy = parameters[4];
                double dz = parameters[5];
                double r = parameters[6];

                for (int i = 0; i < pnts.Length; i++)
                {
                    double px = pnts[i].x;
                    double py = pnts[i].y;
                    double pz = pnts[i].z;

                    derivs[i, 0] = (2.0 * dz * (dz * (lx - px) - dx * (lz - pz)) - 2.0 * dy * (dx * (ly - py) - dy * (lx - px))) / ((2.0 * Math.Sqrt((sqr(dz) + sqr(dy) + sqr(dx)))) * Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px))))));
                    derivs[i, 1] = (2.0 * dx * (dx * (ly - py) - dy * (lx - px)) - 2.0 * dz * (dy * (lz - pz) - dz * (ly - py))) / ((2.0 * Math.Sqrt((sqr(dz) + sqr(dy) + sqr(dx)))) * Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px))))));
                    derivs[i, 2] = (2.0 * dy * (dy * (lz - pz) - dz * (ly - py)) - 2.0 * dx * (dz * (lx - px) - dx * (lz - pz))) / ((2.0 * Math.Sqrt((sqr(dz) + sqr(dy) + sqr(dx)))) * Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px))))));
                    derivs[i, 3] = ((2.0 * (dz * (lx - px) - dx * (lz - pz))) * (pz - lz) + (2.0 * (dx * (ly - py) - dy * (lx - px))) * (ly - py)) / ((2.0 * Math.Sqrt((sqr(dz) + sqr(dy) + sqr(dx)))) * Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px)))))) - (dx * Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px)))))) / exp32((sqr(dz) + sqr(dy) + sqr(dx)));
                    derivs[i, 4] = ((2.0 * (dy * (lz - pz) - dz * (ly - py))) * (lz - pz) + (2.0 * (px - lx)) * (dx * (ly - py) - dy * (lx - px))) / ((2.0 * Math.Sqrt((sqr(dz) + sqr(dy) + sqr(dx)))) * Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px)))))) - (dy * Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px)))))) / exp32((sqr(dz) + sqr(dy) + sqr(dx)));
                    derivs[i, 5] = ((2.0 * (py - ly)) * (dy * (lz - pz) - dz * (ly - py)) + (2.0 * (lx - px)) * (dz * (lx - px) - dx * (lz - pz))) / ((2.0 * Math.Sqrt((sqr(dz) + sqr(dy) + sqr(dx)))) * Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px)))))) - (dz * Math.Sqrt((sqr((dy * (lz - pz) - dz * (ly - py))) + sqr((dz * (lx - px) - dx * (lz - pz))) + sqr((dx * (ly - py) - dy * (lx - px)))))) / exp32((sqr(dz) + sqr(dy) + sqr(dx)));
                    derivs[i, 6] = -1.0;
                }
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            // the algorithm doesn't converge when the axis is almost exactly aligned to the x-axis (or y-axis, z-axis). So we modify the data
            // that the starting axis is (1,1,1) and reverse the result to the original position. Maybe we need to do the same with other Fit methods?
            GeoVector diag = new GeoVector(1, 1, 1).Normalized;
            ModOp toDiag = ModOp.Rotate(center, axis, diag);
            for (int i = 0; i < pnts.Length; i++)
            {
                pnts[i] = toDiag * pnts[i];
            }
            axis = toDiag * axis;
            center = toDiag * center;
            bool ok = gnm.Solve(new double[] { center.x, center.y, center.z, axis.x, axis.y, axis.z, radius }, 30, 1e-6, precision * precision, out double minError, out int numiter, out double[] result);
            if (ok)
            {
                center = new GeoPoint(result[0], result[1], result[2]);
                axis = new GeoVector(result[3], result[4], result[5]).Normalized;
                ModOp fromDiag = toDiag.GetInverse();
                axis = fromDiag * axis;
                center = fromDiag * center;
                axis.ArbitraryNormals(out GeoVector dirx, out GeoVector diry);
                radius = Math.Abs(result[6]);
                cs = new CylindricalSurface(center, radius * dirx, radius * diry, axis);
                return Math.Sqrt(minError);
            }
            else
            {
                cs = null;
                return double.MaxValue;
            }

        }
        /// <summary>
        /// p1,d1 and p2,d2 define two lines. Find a point on each line (s1,s2) so that the connection of these two points has the angle a1 to the first and a2 to the second line. 
        /// a1 and a2 are provided as cosinus of the angles
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="d1"></param>
        /// <param name="p2"></param>
        /// <param name="d2"></param>
        /// <param name="a1"></param>
        /// <param name="a2"></param>
        /// <param name="s1"></param>
        /// <param name="s2"></param>
        /// <returns></returns>
        public static double LineConnection(GeoPoint p1, GeoVector d1, GeoPoint p2, GeoVector d2, double a1, double a2, out double s1, out double s2)
        {
            // a1 is the cosine of the angle
            // function: ((p1+s1*d1)-(p2*s2*d2))*d1/|(p1+s1*d1)-(p2*s2*d2)| == a1, ((p1+s1*d1)-(p2*s2*d2))*d2/|(p1+s1*d1)-(p2*s2*d2)| == a2
            // parameters: s1, s2, values = ((p1+s1*d1)-(p2*s2*d2))*d1/|(p1+s1*d1)-(p2*s2*d2)| - a1 and ((p1+s1*d1)-(p2*s2*d2))*d2/|(p1+s1*d1)-(p2*s2*d2)| - a2
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[2];
                double t1 = parameters[0];
                double t2 = parameters[1];
                GeoVector c = (p1 + t1 * d1) - (p2 + t2 * d2);
                values[0] = sqr(c * d1 / c.Length - a1);
                values[1] = sqr((-c) * d2 / c.Length - a2);
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(2, 2, 0); // Jacobi Matrix 
                double t1 = parameters[0];
                double t2 = parameters[1];
                //derivs[0, 0] = (sqr(d1.z) + sqr(d1.y) + sqr(d1.x)) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - ((d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) * (2.0 * d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + 2.0 * d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + 2.0 * d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x))) / (2.0 * exp32((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))));
                //derivs[1, 0] = ((-d1.z) * d2.z - d1.y * d2.y - d1.x * d2.x) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - ((d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) * (((-2.0) * d2.z) * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) - 2.0 * d2.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) - 2.0 * d2.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x))) / (2.0 * exp32((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))));
                //derivs[0, 1] = ((-d1.z) * d2.z - d1.y * d2.y - d1.x * d2.x) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - ((2.0 * d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + 2.0 * d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + 2.0 * d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) * (d2.z * (d2.z * t2 - d1.z * t1 + p2.z - p1.z) + d2.y * (d2.y * t2 - d1.y * t1 + p2.y - p1.y) + d2.x * (d2.x * t2 - d1.x * t1 + p2.x - p1.x))) / (2.0 * exp32((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))));
                //derivs[1, 1] = (sqr(d2.z) + sqr(d2.y) + sqr(d2.x)) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - ((((-2.0) * d2.z) * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) - 2.0 * d2.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) - 2.0 * d2.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) * (d2.z * (d2.z * t2 - d1.z * t1 + p2.z - p1.z) + d2.y * (d2.y * t2 - d1.y * t1 + p2.y - p1.y) + d2.x * (d2.x * t2 - d1.x * t1 + p2.x - p1.x))) / (2.0 * exp32((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))));
                derivs[0, 0] = (2.0 * ((sqr(d1.z) + sqr(d1.y) + sqr(d1.x)) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - ((d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) * (2.0 * d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + 2.0 * d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + 2.0 * d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x))) / (2.0 * exp32((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x))))))) * ((d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - a1);
                derivs[0, 1] = (2.0 * (((-d1.z) * d2.z - d1.y * d2.y - d1.x * d2.x) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - ((d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) * (((-2.0) * d2.z) * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) - 2.0 * d2.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) - 2.0 * d2.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x))) / (2.0 * exp32((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x))))))) * ((d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - a1);
                derivs[1, 0] = (2.0 * (((-d1.z) * d2.z - d1.y * d2.y - d1.x * d2.x) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - ((2.0 * d1.z * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) + 2.0 * d1.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) + 2.0 * d1.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) * (d2.z * (d2.z * t2 - d1.z * t1 + p2.z - p1.z) + d2.y * (d2.y * t2 - d1.y * t1 + p2.y - p1.y) + d2.x * (d2.x * t2 - d1.x * t1 + p2.x - p1.x))) / (2.0 * exp32((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x))))))) * ((d2.z * (d2.z * t2 - d1.z * t1 + p2.z - p1.z) + d2.y * (d2.y * t2 - d1.y * t1 + p2.y - p1.y) + d2.x * (d2.x * t2 - d1.x * t1 + p2.x - p1.x)) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - a2);
                derivs[1, 1] = (2.0 * ((sqr(d2.z) + sqr(d2.y) + sqr(d2.x)) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - ((((-2.0) * d2.z) * ((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z) - 2.0 * d2.y * ((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y) - 2.0 * d2.x * ((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)) * (d2.z * (d2.z * t2 - d1.z * t1 + p2.z - p1.z) + d2.y * (d2.y * t2 - d1.y * t1 + p2.y - p1.y) + d2.x * (d2.x * t2 - d1.x * t1 + p2.x - p1.x))) / (2.0 * exp32((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x))))))) * ((d2.z * (d2.z * t2 - d1.z * t1 + p2.z - p1.z) + d2.y * (d2.y * t2 - d1.y * t1 + p2.y - p1.y) + d2.x * (d2.x * t2 - d1.x * t1 + p2.x - p1.x)) / Math.Sqrt((sqr(((-d2.z) * t2 + d1.z * t1 - p2.z + p1.z)) + sqr(((-d2.y) * t2 + d1.y * t1 - p2.y + p1.y)) + sqr(((-d2.x) * t2 + d1.x * t1 - p2.x + p1.x)))) - a2);
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { 0.0, 0.0 }, 30, 1e-6, 1e-6, out double minError, out int numiter, out double[] result);
            s1 = result[0];
            s2 = result[1];
            return minError;
        }
        /// <summary>
        /// Calculates a uv points on each provided surface, so that the connection of the two points is perpendicular to the surfaces at these points.
        /// Starts a GaussNewton approximation in the center of the provided parameter bounds
        /// </summary>
        /// <param name="surface1"></param>
        /// <param name="bounds1"></param>
        /// <param name="surface2"></param>
        /// <param name="bounds2"></param>
        /// <param name="uv1"></param>
        /// <param name="uv2"></param>
        /// <returns></returns>
        public static double SurfaceExtrema(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, ref GeoPoint2D uv1, ref GeoPoint2D uv2)
        {
            // see extremaSurfaces.wxmx
            // parameters uv1.x, uv1.y, uv2.x, uv2.y
            bool checkParameter(double[] parameters)
            {
                GeoPoint2D uvs1 = new GeoPoint2D(parameters[0], parameters[1]);
                GeoPoint2D uvs2 = new GeoPoint2D(parameters[2], parameters[3]);
                return bounds1.ContainsEps(uvs1, bounds1.Size * 1e-6) && bounds2.ContainsEps(uvs2, bounds2.Size * 1e-6);
            }
            void curtailParameter(double[] parameters)
            {
                if (parameters[0] < bounds1.Left) parameters[0] = bounds1.Left;
                if (parameters[0] > bounds1.Right) parameters[0] = bounds1.Right;
                if (parameters[1] < bounds1.Bottom) parameters[1] = bounds1.Bottom;
                if (parameters[1] > bounds1.Top) parameters[1] = bounds1.Top;
                if (parameters[2] < bounds2.Left) parameters[2] = bounds2.Left;
                if (parameters[2] > bounds2.Right) parameters[2] = bounds2.Right;
                if (parameters[3] < bounds2.Bottom) parameters[3] = bounds2.Bottom;
                if (parameters[3] > bounds2.Top) parameters[3] = bounds2.Top;
            }
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[4];
                GeoPoint2D uvs1 = new GeoPoint2D(parameters[0], parameters[1]);
                GeoPoint2D uvs2 = new GeoPoint2D(parameters[2], parameters[3]);
                GeoPoint s = surface1.PointAt(uvs1);
                GeoPoint r = surface2.PointAt(uvs2);
                GeoVector sdu = surface1.UDirection(uvs1);
                GeoVector sdv = surface1.VDirection(uvs1);
                GeoVector rds = surface2.UDirection(uvs2);
                GeoVector rdt = surface2.VDirection(uvs2);
                GeoVector d = s - r;
                d.NormIfNotNull();

                // according to extremaSurfaces3.wxmx (doesn't converge close to intersection points)
                //values[0] = d * sdu;
                //values[1] = d * sdv;
                //values[2] = d * rds;
                //values[3] = d * rdt;

                // according to extremaSurfaces2.wxmx
                values[0] = (s.z - r.z) * sdu.z + (s.y - r.y) * sdu.y + (s.x - r.x) * sdu.x;
                values[1] = (s.z - r.z) * sdv.z + (s.y - r.y) * sdv.y + (s.x - r.x) * sdv.x;
                values[2] = rds.z * (s.z - r.z) + rds.y * (s.y - r.y) + rds.x * (s.x - r.x);
                values[3] = rdt.z * (s.z - r.z) + rdt.y * (s.y - r.y) + rdt.x * (s.x - r.x);

                // according to extremaSurfaces1.wxmx
                //values[0] = 2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x;
                //values[1] = 2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x;
                //values[2] = (-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x);
                //values[3] = (-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x);
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(4, 4, 0);
                GeoPoint2D uvs1 = new GeoPoint2D(parameters[0], parameters[1]);
                GeoPoint2D uvs2 = new GeoPoint2D(parameters[2], parameters[3]);
                surface1.Derivation2At(uvs1, out GeoPoint s, out GeoVector sdu, out GeoVector sdv, out GeoVector sduu, out GeoVector sdvv, out GeoVector sdudv);
                surface2.Derivation2At(uvs2, out GeoPoint r, out GeoVector rds, out GeoVector rdt, out GeoVector rdss, out GeoVector rdtt, out GeoVector rdsdt);

                // some regex: 
                // \([su],[tv]\) -> 
                // ,([uvst]),2 -> d$1$1  
                // ,([uvst]),1 -> d$1
                // 'diff\(([sr])([xyz])([dstuv]*)\) -> $1$3.$2  
                // 'diff\(([cde])([xyz]),([stu]),1\) -> $1$3.$2  
                // (\((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!))\))\^\(3/2\) -> exp32$1
                // (\((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!))\))\^2 -> sqr$1  
                // , -> \r\n
                // ([rs])([xyz]) -> $1.$2

                // according to extremaSurfaces3.wxmx
                //derivs[0, 0] = ((s.z - r.z) * sduu.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + sqr(sdu.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.z - r.z) * sdu.z * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.y - r.y) * sdu.y * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.x - r.x) * sdu.x * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.y - r.y) * sduu.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + sqr(sdu.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.x - r.x) * sduu.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + sqr(sdu.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[0, 1] = (-((s.z - r.z) * sdu.z * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) - ((s.y - r.y) * sdu.y * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.x - r.x) * sdu.x * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (sdu.z * sdv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.z - r.z) * sdudv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (sdu.y * sdv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.y - r.y) * sdudv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (sdu.x * sdv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.x - r.x) * sdudv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[0, 2] = (-(rds.z * sdu.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) - ((s.z - r.z) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x)) * sdu.z) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.y * sdu.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.y - r.y) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x)) * sdu.y) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.x * sdu.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.x - r.x) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x)) * sdu.x) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[0, 3] = (-(rdt.z * sdu.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) - ((s.z - r.z) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x)) * sdu.z) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.y * sdu.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.y - r.y) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x)) * sdu.y) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.x * sdu.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.x - r.x) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x)) * sdu.x) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[1, 0] = (-((s.z - r.z) * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x) * sdv.z) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) + (sdu.z * sdv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.z - r.z) * sdudv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.y - r.y) * sdv.y * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.x - r.x) * sdv.x * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (sdu.y * sdv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.y - r.y) * sdudv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (sdu.x * sdv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.x - r.x) * sdudv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[1, 1] = ((s.z - r.z) * sdvv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + sqr(sdv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.z - r.z) * sdv.z * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.y - r.y) * sdv.y * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.x - r.x) * sdv.x * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.y - r.y) * sdvv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + sqr(sdv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + ((s.x - r.x) * sdvv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + sqr(sdv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[1, 2] = (-(rds.z * sdv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) - ((s.z - r.z) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x)) * sdv.z) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.y * sdv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.y - r.y) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x)) * sdv.y) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.x * sdv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.x - r.x) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x)) * sdv.x) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[1, 3] = (-(rdt.z * sdv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) - ((s.z - r.z) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x)) * sdv.z) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.y * sdv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.y - r.y) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x)) * sdv.y) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.x * sdv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - ((s.x - r.x) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x)) * sdv.x) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[2, 0] = (-(rds.z * (s.z - r.z) * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) - (rds.y * (s.y - r.y) * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.x * (s.x - r.x) * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rds.z * sdu.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rds.y * sdu.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rds.x * sdu.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[2, 1] = (-(rds.z * (s.z - r.z) * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) - (rds.y * (s.y - r.y) * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.x * (s.x - r.x) * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rds.z * sdv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rds.y * sdv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rds.x * sdv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[2, 2] = (rdss.z * (s.z - r.z)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdss.y * (s.y - r.y)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdss.x * (s.x - r.x)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - sqr(rds.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - sqr(rds.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - sqr(rds.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.z * (s.z - r.z) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.y * (s.y - r.y) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.x * (s.x - r.x) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[2, 3] = (rdsdt.z * (s.z - r.z)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdsdt.y * (s.y - r.y)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdsdt.x * (s.x - r.x)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.z * rdt.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.y * rdt.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.x * rdt.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.z * (s.z - r.z) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.y * (s.y - r.y) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.x * (s.x - r.x) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[3, 0] = (-(rdt.z * (s.z - r.z) * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) - (rdt.y * (s.y - r.y) * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.x * (s.x - r.x) * (2 * (s.z - r.z) * sdu.z + 2 * (s.y - r.y) * sdu.y + 2 * (s.x - r.x) * sdu.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdt.z * sdu.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdt.y * sdu.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdt.x * sdu.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[3, 1] = (-(rdt.z * (s.z - r.z) * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x))) - (rdt.y * (s.y - r.y) * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.x * (s.x - r.x) * (2 * (s.z - r.z) * sdv.z + 2 * (s.y - r.y) * sdv.y + 2 * (s.x - r.x) * sdv.x)) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdt.z * sdv.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdt.y * sdv.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdt.x * sdv.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[3, 2] = (rdsdt.z * (s.z - r.z)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdsdt.y * (s.y - r.y)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdsdt.x * (s.x - r.x)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.z * rdt.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.y * rdt.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rds.x * rdt.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.z * (s.z - r.z) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.y * (s.y - r.y) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.x * (s.x - r.x) * ((-2 * rds.z * (s.z - r.z)) - 2 * rds.y * (s.y - r.y) - 2 * rds.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));
                //derivs[3, 3] = (rdtt.z * (s.z - r.z)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdtt.y * (s.y - r.y)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) + (rdtt.x * (s.x - r.x)) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - sqr(rdt.z) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - sqr(rdt.y) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - sqr(rdt.x) / (sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.z * (s.z - r.z) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.y * (s.y - r.y) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x)) - (rdt.x * (s.x - r.x) * ((-2 * rdt.z * (s.z - r.z)) - 2 * rdt.y * (s.y - r.y) - 2 * rdt.x * (s.x - r.x))) / sqr(sqr(s.z - r.z) + sqr(s.y - r.y) + sqr(s.x - r.x));

                // according to extremaSurfaces2.wxmx
                derivs[0, 0] = (s.z - r.z) * sduu.z + sqr(sdu.z) + (s.y - r.y) * sduu.y + sqr(sdu.y) + (s.x - r.x) * sduu.x + sqr(sdu.x);
                derivs[0, 1] = sdu.z * sdv.z + (s.z - r.z) * sdudv.z + sdu.y * sdv.y + (s.y - r.y) * sdudv.y + sdu.x * sdv.x + (s.x - r.x) * sdudv.x;
                derivs[0, 2] = (-rds.z * sdu.z) - rds.y * sdu.y - rds.x * sdu.x;
                derivs[0, 3] = (-rdt.z * sdu.z) - rdt.y * sdu.y - rdt.x * sdu.x;
                derivs[1, 0] = sdu.z * sdv.z + (s.z - r.z) * sdudv.z + sdu.y * sdv.y + (s.y - r.y) * sdudv.y + sdu.x * sdv.x + (s.x - r.x) * sdudv.x;
                derivs[1, 1] = (s.z - r.z) * sdvv.z + sqr(sdv.z) + (s.y - r.y) * sdvv.y + sqr(sdv.y) + (s.x - r.x) * sdvv.x + sqr(sdv.x);
                derivs[1, 2] = (-rds.z * sdv.z) - rds.y * sdv.y - rds.x * sdv.x;
                derivs[1, 3] = (-rdt.z * sdv.z) - rdt.y * sdv.y - rdt.x * sdv.x;
                derivs[2, 0] = rds.z * sdu.z + rds.y * sdu.y + rds.x * sdu.x;
                derivs[2, 1] = rds.z * sdv.z + rds.y * sdv.y + rds.x * sdv.x;
                derivs[2, 2] = rdss.z * (s.z - r.z) + rdss.y * (s.y - r.y) + rdss.x * (s.x - r.x) - sqr(rds.z) - sqr(rds.y) - sqr(rds.x);
                derivs[2, 3] = rdsdt.z * (s.z - r.z) + rdsdt.y * (s.y - r.y) + rdsdt.x * (s.x - r.x) - rds.z * rdt.z - rds.y * rdt.y - rds.x * rdt.x;
                derivs[3, 0] = rdt.z * sdu.z + rdt.y * sdu.y + rdt.x * sdu.x;
                derivs[3, 1] = rdt.z * sdv.z + rdt.y * sdv.y + rdt.x * sdv.x;
                derivs[3, 2] = rdsdt.z * (s.z - r.z) + rdsdt.y * (s.y - r.y) + rdsdt.x * (s.x - r.x) - rds.z * rdt.z - rds.y * rdt.y - rds.x * rdt.x;
                derivs[3, 3] = rdtt.z * (s.z - r.z) + rdtt.y * (s.y - r.y) + rdtt.x * (s.x - r.x) - sqr(rdt.z) - sqr(rdt.y) - sqr(rdt.x);

                // according to extremaSurfaces1.wxmx
                //derivs[0, 0] = 2 * (s.z - r.z) * sduu.z + 2 * sqr(sdu.z) + 2 * (s.y - r.y) * sduu.y + 2 * sqr(sdu.y) + 2 * (s.x - r.x) * sduu.x + 2 * sqr(sdu.x);
                //derivs[0, 1] = 2 * sdu.z * sdv.z + 2 * (s.z - r.z) * sdudv.z + 2 * sdu.y * sdv.y + 2 * (s.y - r.y) * sdudv.y + 2 * sdu.x * sdv.x + 2 * (s.x - r.x) * sdudv.x;
                //derivs[0, 2] = (-2 * rds.z * sdu.z) - 2 * rds.y * sdu.y - 2 * rds.x * sdu.x;
                //derivs[0, 3] = (-2 * rdt.z * sdu.z) - 2 * rdt.y * sdu.y - 2 * rdt.x * sdu.x;
                //derivs[1, 0] = 2 * sdu.z * sdv.z + 2 * (s.z - r.z) * sdudv.z + 2 * sdu.y * sdv.y + 2 * (s.y - r.y) * sdudv.y + 2 * sdu.x * sdv.x + 2 * (s.x - r.x) * sdudv.x;
                //derivs[1, 1] = 2 * (s.z - r.z) * sdvv.z + 2 * sqr(sdv.z) + 2 * (s.y - r.y) * sdvv.y + 2 * sqr(sdv.y) + 2 * (s.x - r.x) * sdvv.x + 2 * sqr(sdv.x);
                //derivs[1, 2] = (-2 * rds.z * sdv.z) - 2 * rds.y * sdv.y - 2 * rds.x * sdv.x;
                //derivs[1, 3] = (-2 * rdt.z * sdv.z) - 2 * rdt.y * sdv.y - 2 * rdt.x * sdv.x;
                //derivs[2, 0] = (-2 * rds.z * sdu.z) - 2 * rds.y * sdu.y - 2 * rds.x * sdu.x;
                //derivs[2, 1] = (-2 * rds.z * sdv.z) - 2 * rds.y * sdv.y - 2 * rds.x * sdv.x;
                //derivs[2, 2] = (-2 * rdss.z * (s.z - r.z)) - 2 * rdss.y * (s.y - r.y) - 2 * rdss.x * (s.x - r.x) + 2 * sqr(rds.z) + 2 * sqr(rds.y) + 2 * sqr(rds.x);
                //derivs[2, 3] = (-2 * rdsdt.z * (s.z - r.z)) - 2 * rdsdt.y * (s.y - r.y) - 2 * rdsdt.x * (s.x - r.x) + 2 * rds.z * rdt.z + 2 * rds.y * rdt.y + 2 * rds.x * rdt.x;
                //derivs[3, 0] = (-2 * rdt.z * sdu.z) - 2 * rdt.y * sdu.y - 2 * rdt.x * sdu.x;
                //derivs[3, 1] = (-2 * rdt.z * sdv.z) - 2 * rdt.y * sdv.y - 2 * rdt.x * sdv.x;
                //derivs[3, 2] = (-2 * rdsdt.z * (s.z - r.z)) - 2 * rdsdt.y * (s.y - r.y) - 2 * rdsdt.x * (s.x - r.x) + 2 * rds.z * rdt.z + 2 * rds.y * rdt.y + 2 * rds.x * rdt.x;
                //derivs[3, 3] = (-2 * rdtt.z * (s.z - r.z)) - 2 * rdtt.y * (s.y - r.y) - 2 * rdtt.x * (s.x - r.x) + 2 * sqr(rdt.z) + 2 * sqr(rdt.y) + 2 * sqr(rdt.x);
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc, checkParameter, curtailParameter);
            bool ok = gnm.Solve(new double[] { uv1.x, uv1.y, uv2.x, uv2.y }, 30, 1e-6, 1e-6, out double minError, out int numiter, out double[] result);
            uv1 = new GeoPoint2D(result[0], result[1]);
            uv2 = new GeoPoint2D(result[2], result[3]);
            return minError;
        }
        /// <summary>
        /// Tries to find a maximum or minimum point in the direction <paramref name="dir"/> of <paramref name="surface"/> within the patch <paramref name="bounds"/>.
        /// Requires the surface to implement <see cref="ISurface.Derivation2At(GeoPoint2D, out GeoPoint, out GeoVector, out GeoVector, out GeoVector, out GeoVector, out GeoVector)"/>.
        /// </summary>
        /// <param name="surface">The surface</param>
        /// <param name="bounds">The patch of the surface to be examined</param>
        /// <param name="dir">The direction in which to find a minimum or maximum</param>
        /// <param name="uv">a starting position for the search (usually the center of bounds)</param>
        /// <returns>The error</returns>
        public static double SurfaceExtrema(ISurface surface, BoundingRect bounds, GeoVector dir, ref GeoPoint2D uv)
        {
            // parameters uv1.x, uv1.y
            bool checkParameter(double[] parameters)
            {
                return bounds.ContainsEps(new GeoPoint2D(parameters[0], parameters[1]), bounds.Size * 1e-6) ;
            }
            void curtailParameter(double[] parameters)
            {
                if (parameters[0] < bounds.Left) parameters[0] = bounds.Left;
                if (parameters[0] > bounds.Right) parameters[0] = bounds.Right;
                if (parameters[1] < bounds.Bottom) parameters[1] = bounds.Bottom;
                if (parameters[1] > bounds.Top) parameters[1] = bounds.Top;
            }
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[2];
                GeoPoint2D suv = new GeoPoint2D(parameters[0], parameters[1]);
                GeoVector sdu = surface.UDirection(suv);
                GeoVector sdv = surface.VDirection(suv);

                // the value to reach zero is the scalar product of diru*dir and dirv*dir
                values[0] = dir.z * sdu.z + dir.y * sdu.y + dir.x * sdu.x;
                values[1] = dir.z * sdv.z + dir.y * sdv.y + dir.x * sdv.x;
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(2, 2, 0);
                GeoPoint2D suv = new GeoPoint2D(parameters[0], parameters[1]);
                surface.Derivation2At(suv, out GeoPoint s, out GeoVector sdu, out GeoVector sdv, out GeoVector sduu, out GeoVector sdvv, out GeoVector sdudv);

                derivs[0, 0] = dir.z * sduu.z + dir.y * sduu.y + dir.x * sduu.x;
                derivs[0, 1] = dir.z * sdudv.z + dir.y * sdudv.y + dir.x * sdudv.x;
                derivs[1, 0] = dir.z * sdudv.z + dir.y * sdudv.y + dir.x * sdudv.x;
                derivs[1, 1] = dir.z * sdvv.z + dir.y * sdvv.y + dir.x * sdvv.x;
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc, checkParameter, curtailParameter);
            bool ok = gnm.Solve(new double[] { uv.x, uv.y}, 30, 1e-6, 1e-6, out double minError, out int numiter, out double[] result);
            uv = new GeoPoint2D(result[0], result[1]);
            return minError;
        }
        public static double SurfaceCurveExtrema(ISurface surface1, BoundingRect bounds1, ICurve curve2, double curveUmin, double curveUmax, ref GeoPoint2D uv1, ref double u2)
        {
            // see extremaSurfaces.wxmx
            // parameters uv1.x, uv1.y, uv2.x, uv2.y
            bool checkParameter(double[] parameters)
            {
                GeoPoint2D uvs1 = new GeoPoint2D(parameters[0], parameters[1]);
                double uc2 = parameters[2];
                return bounds1.ContainsEps(uvs1, bounds1.Size * 1e-6) && uc2 > curveUmin - 1e-6 && uc2 < curveUmax + 1e-6;
            }
            void curtailParameter(double[] parameters)
            {
                if (parameters[0] < bounds1.Left) parameters[0] = bounds1.Left;
                if (parameters[0] > bounds1.Right) parameters[0] = bounds1.Right;
                if (parameters[1] < bounds1.Bottom) parameters[1] = bounds1.Bottom;
                if (parameters[1] > bounds1.Top) parameters[1] = bounds1.Top;
                if (parameters[2] < curveUmin) parameters[2] = curveUmin;
                if (parameters[2] > curveUmax) parameters[2] = curveUmax;
            }
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[3];
                GeoPoint2D uvs1 = new GeoPoint2D(parameters[0], parameters[1]);
                double us2 = parameters[2];
                GeoPoint s = surface1.PointAt(uvs1);
                GeoPoint r = curve2.PointAt(us2);
                GeoVector sdu = surface1.UDirection(uvs1);
                GeoVector sdv = surface1.VDirection(uvs1);
                GeoVector rds = curve2.DirectionAt(us2);

                // according to extremaSurfaceCurve.wxmx
                values[0] = (s.z - r.z) * sdu.z + (s.y - r.y) * sdu.y + (s.x - r.x) * sdu.x;
                values[1] = (s.z - r.z) * sdv.z + (s.y - r.y) * sdv.y + (s.x - r.x) * sdv.x;
                values[2] = rds.z * (s.z - r.z) + rds.y * (s.y - r.y) + rds.x * (s.x - r.x);
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {
                derivs = DenseMatrix.Create(3, 3, 0);
                GeoPoint2D uvs1 = new GeoPoint2D(parameters[0], parameters[1]);
                double us2 = parameters[2];
                surface1.Derivation2At(uvs1, out GeoPoint s, out GeoVector sdu, out GeoVector sdv, out GeoVector sduu, out GeoVector sdvv, out GeoVector sdudv);
                if (!curve2.TryPointDeriv2At(us2, out GeoPoint r, out GeoVector rds, out GeoVector rdss))
                {
                    r = curve2.PointAt(us2);
                    rds = curve2.DirectionAt(us2);
                    rdss = GeoVector.NullVector; // ?? shold not be used, ok for lines
                }

                // some regex: 
                // \([su],[tv]\) -> 
                // ,([uvst]),2 -> d$1$1  
                // ,([uvst]),1 -> d$1
                // 'diff\(([sr])([xyz])([dstuv]*)\) -> $1$3.$2  
                // (\((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!))\))\^\(3/2\) -> exp32$1
                // (\((?>\((?<c>)|[^()]+|\)(?<-c>))*(?(c)(?!))\))\^2 -> sqr$1  
                // , -> \r\n
                // ([rs])([xyz]) -> $1.$2

                // according to extremaSurfaces2.wxmx
                derivs[0, 0] = (s.z - r.z) * sduu.z + sqr(sdu.z) + (s.y - r.y) * sduu.y + sqr(sdu.y) + (s.x - r.x) * sduu.x + sqr(sdu.x);
                derivs[0, 1] = sdu.z * sdv.z + (s.z - r.z) * sdudv.z + sdu.y * sdv.y + (s.y - r.y) * sdudv.y + sdu.x * sdv.x + (s.x - r.x) * sdudv.x;
                derivs[0, 2] = (-rds.z * sdu.z) - rds.y * sdu.y - rds.x * sdu.x;
                derivs[1, 0] = sdu.z * sdv.z + (s.z - r.z) * sdudv.z + sdu.y * sdv.y + (s.y - r.y) * sdudv.y + sdu.x * sdv.x + (s.x - r.x) * sdudv.x;
                derivs[1, 1] = (s.z - r.z) * sdvv.z + sqr(sdv.z) + (s.y - r.y) * sdvv.y + sqr(sdv.y) + (s.x - r.x) * sdvv.x + sqr(sdv.x);
                derivs[1, 2] = (-rds.z * sdv.z) - rds.y * sdv.y - rds.x * sdv.x;
                derivs[2, 0] = rds.z * sdu.z + rds.y * sdu.y + rds.x * sdu.x;
                derivs[2, 1] = rds.z * sdv.z + rds.y * sdv.y + rds.x * sdv.x;
                derivs[2, 2] = rdss.z * (s.z - r.z) + rdss.y * (s.y - r.y) + rdss.x * (s.x - r.x) - sqr(rds.z) - sqr(rds.y) - sqr(rds.x);
            }
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc, checkParameter, curtailParameter);
            bool ok = gnm.Solve(new double[] { uv1.x, uv1.y, u2 }, 30, 1e-6, 1e-6, out double minError, out int numiter, out double[] result);
            uv1 = new GeoPoint2D(result[0], result[1]);
            u2 = result[2];
            return minError;
        }
#if DEBUG
        public static void DebugCone()
        {
            Plane pln = new Plane(GeoPoint.Origin, new GeoVector(50, -50, 50));
            ConicalSurface cs = new ConicalSurface(new GeoPoint(100, 80, 60), pln.DirectionX, pln.DirectionY, pln.Normal, 0.5);
            Face fc = Face.MakeFace(cs, new BoundingRect(0, 1, Math.PI, 10));
            GeoPoint p0 = cs.PointAt(new GeoPoint2D(0, 1));
            GeoPoint p1 = cs.PointAt(new GeoPoint2D(0, 0));
            GeoPoint p2 = Geometry.DropPL(p0, p1, pln.Normal);
            double d = p2 | p1;
            GeoPoint[] samples = new GeoPoint[25]; // 25 evenly spread points
            double umin = 0, umax = Math.PI, vmin = 1, vmax = 10;
            double ustep = (umax - umin) / 4;
            double vstep = (vmax - vmin) / 4;
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    samples[5 * i + j] = cs.PointAt(new GeoPoint2D(umin + i * ustep, vmin + j * vstep));
                }
            }
            GeoPoint cnt = new GeoPoint(samples);
            double maxerror = ConeFit(samples.ToIArray(), cnt, new GeoVector(1, -1, 1), 0.45, 1e-6, out ConicalSurface ocs);
            Face ofc = Face.MakeFace(ocs, new BoundingRect(0, 1, Math.PI, 10));

        }
        public static void DebugApproxNurbs()
        {
            /* zum Interpolieren eines NURBS (mit nur einem Knotenabschnitt)
             * Die Parameter für die Fehlerfunktion sind die Pole (mit Gewichten)
             * Der 1. und letzte Pol haben Gewicht 1
             * Die Fehlerfunktion erzeugt den NURBS und berechnet die Abstände zu den samples
             * Es wird keine Ableitungsfunktion zu verfügung gestellt, keine Jacobi Matrix
             */
            BSpline2D bsp = BSpline2D.MakeCircle(new GeoPoint2D(10, 20), 5);
            bsp.PointAt(0.125);
            double[] uknots = new double[3];
            double u = 0.125;
            int degree = 2;
            int span = 2;
            double[,] p = new double[3, 3];
            double x = 0 + (0 + ((uknots[span + 1] - u) * ((0 + ((uknots[span + 1] - u) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))))) / (uknots[span + 1] - u + (u - uknots[span + (-1)]))))) * p[span - degree + 0, 0] + (((u - uknots[span + (-1)]) * ((0 + ((uknots[span + 1] - u) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))))) / (uknots[span + 1] - u + (u - uknots[span + (-1)])))) + ((uknots[span + 2] - u) * ((u - uknots[span + 0]) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))) / (uknots[span + 2] - u + (u - uknots[span + 0]))))) * p[span - degree + 1, 0] + (u - uknots[span + 0]) * ((u - uknots[span + 0]) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))) / (uknots[span + 2] - u + (u - uknots[span + 0]))) * p[span - degree + 2, 0];
            double y = 0 + (0 + ((uknots[span + 1] - u) * ((0 + ((uknots[span + 1] - u) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))))) / (uknots[span + 1] - u + (u - uknots[span + (-1)]))))) * p[span - degree + 0, 1] + (((u - uknots[span + (-1)]) * ((0 + ((uknots[span + 1] - u) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))))) / (uknots[span + 1] - u + (u - uknots[span + (-1)])))) + ((uknots[span + 2] - u) * ((u - uknots[span + 0]) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))) / (uknots[span + 2] - u + (u - uknots[span + 0]))))) * p[span - degree + 1, 1] + (u - uknots[span + 0]) * ((u - uknots[span + 0]) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))) / (uknots[span + 2] - u + (u - uknots[span + 0]))) * p[span - degree + 2, 1];
            double w = 0 + (0 + ((uknots[span + 1] - u) * ((0 + ((uknots[span + 1] - u) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))))) / (uknots[span + 1] - u + (u - uknots[span + (-1)]))))) * p[span - degree + 0, 2] + (((u - uknots[span + (-1)]) * ((0 + ((uknots[span + 1] - u) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))))) / (uknots[span + 1] - u + (u - uknots[span + (-1)])))) + ((uknots[span + 2] - u) * ((u - uknots[span + 0]) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))) / (uknots[span + 2] - u + (u - uknots[span + 0]))))) * p[span - degree + 1, 2] + (u - uknots[span + 0]) * ((u - uknots[span + 0]) * (1 / (uknots[span + 1] - u + (u - uknots[span + 0]))) / (uknots[span + 2] - u + (u - uknots[span + 0]))) * p[span - degree + 2, 2];
        }
        public static void DebugTestTorus()
        {
            // see https://www.geometrictools.com/Documentation/DistanceToCircle3.pdf
            GeoVector dirx = new GeoVector(1, 1, 2);
            GeoVector diry = GeoVector.ZAxis ^ dirx;
            GeoVector dirz = dirx ^ diry;
            GeoVector n = 15 * dirz.Normalized;
            ToroidalSurface ts = new ToroidalSurface(new GeoPoint(10, 20, 30), dirx.Normalized, diry.Normalized, dirz.Normalized, 15, 5);
            GeoPoint[] pnts = new GeoPoint[20];
            Random rnd = new Random(123);
            for (int i = 0; i < pnts.Length; i++)
            {
                GeoPoint2D uv = new GeoPoint2D(rnd.NextDouble() * 2 * Math.PI, rnd.NextDouble() * 2 * Math.PI);
                pnts[i] = ts.PointAt(uv);
            }
            // parameters: 0:cx, 1:cy, 2: cz, 3: nx, 4: ny, 5: nz, 6: r (majorRadius is sqrt(n*n), r: minorRadius)
            void efunc(double[] parameters, out double[] values)
            {
                values = new double[pnts.Length];
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];
                double nx = parameters[3];
                double ny = parameters[4];
                double nz = parameters[5];
                double r = parameters[6];
                double R2 = nx * nx + ny * ny + nz * nz;
                double R = Math.Sqrt(R2);
                for (int i = 0; i < pnts.Length; i++)
                {
                    GeoVector d = pnts[i] - new GeoPoint(cx, cy, cz);
                    values[i] = r * r - (sqr(nx * d.x + ny * d.y + nz * d.z) / R2 + sqr(Math.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z - sqr(nx * d.x + ny * d.y + nz * d.z) / R2) - R));
                }
            }
            void jfunc(double[] parameters, out Matrix derivs)
            {


                derivs = DenseMatrix.Create(pnts.Length, 7, 0); // Jacobi Matrix 
                double cx = parameters[0];
                double cy = parameters[1];
                double cz = parameters[2];
                double nx = parameters[3];
                double ny = parameters[4];
                double nz = parameters[5];
                double r = parameters[6];
                double nn = nz * nz + ny * ny + nx * nx;
                double rn = Math.Sqrt(nn);

                for (int i = 0; i < pnts.Length; i++)
                {
                    double px = pnts[i].x;
                    double py = pnts[i].y;
                    double pz = pnts[i].z;
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
            }
#if MATHNET
            MathNet.Numerics.LinearAlgebra.Vector<double> oeFunc(MathNet.Numerics.LinearAlgebra.Vector<double> parameters, MathNet.Numerics.LinearAlgebra.Vector<double> x)
            {
                efunc(parameters.ToArray(), out double[] errorValues);
                return MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(errorValues);
            }
            MathNet.Numerics.LinearAlgebra.Matrix<double> ojfunc(MathNet.Numerics.LinearAlgebra.Vector<double> parameters, MathNet.Numerics.LinearAlgebra.Vector<double> x)
            {
                jfunc(parameters.ToArray(), out Matrix partialDerivations);
                var derivs = MathNet.Numerics.LinearAlgebra.Matrix<double>.Build.Dense(partialDerivations.RowCount, partialDerivations.ColumnCount);
                for (int i = 0; i < partialDerivations.RowCount; i++)
                {
                    for (int j = 0; j < partialDerivations.ColumnCount; j++)
                    {
                        derivs[i, j] = partialDerivations[i, j];
                    }
                }
                return derivs;
            }
            MathNet.Numerics.Optimization.LevenbergMarquardtMinimizer minimizer = new MathNet.Numerics.Optimization.LevenbergMarquardtMinimizer(0.001, 1e-20, 1e-20, 1e-20, 20);

            var obj = MathNet.Numerics.Optimization.ObjectiveFunction.NonlinearModel(oeFunc, ojfunc, MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(pnts.Length), MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(pnts.Length));
            var lmresult = minimizer.FindMinimum(obj, MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(new double[] { 0, 0, 0, 0, 0, 1, 1 }));

#endif
            GaussNewtonMinimizer gnm = new GaussNewtonMinimizer(efunc, jfunc);
            bool ok = gnm.Solve(new double[] { 11, 22, 33, -9, -9, 9, 6 }, 30, 1e-6, 1e-9, out double minError, out int numiter, out double[] result);
        }
        public static void ApproxDualSurfaceCurve(ICurve dsc)
        {
#if MATHNET
            GeoPoint[] pnts = new GeoPoint[24];
            for (int i = 0; i < pnts.Length; i++)
            {
                pnts[i] = dsc.PointAt((double)(i + 1) / (double)(pnts.Length + 1));
            }
            BSpline bsp = BSpline.Construct();
            GeoPoint[] poles = new GeoPoint[5];
            double[] weights = new double[5];
            poles[0] = dsc.PointAt(0.0);
            poles[4] = dsc.PointAt(1.0);
            weights[0] = weights[4] = 1.0;
            MathNet.Numerics.LinearAlgebra.Vector<double> oeFunc(MathNet.Numerics.LinearAlgebra.Vector<double> parameters, MathNet.Numerics.LinearAlgebra.Vector<double> x)
            {   // parameters pole[1] and pole[2] (including weights) of a NURBS curve
                poles[1] = new GeoPoint(parameters[0], parameters[1], parameters[2]);
                poles[2] = new GeoPoint(parameters[4], parameters[5], parameters[6]);
                poles[3] = new GeoPoint(parameters[8], parameters[9], parameters[10]);
                weights[1] = parameters[3];
                weights[2] = parameters[7];
                weights[3] = parameters[11];
                bsp.SetData(4, poles, weights, new double[] { 0, 1 }, new int[] { 5, 5 }, false);
                double[] errorValues = new double[pnts.Length];
                for (int i = 0; i < pnts.Length; i++)
                {
                    // errorValues[i] = pnts[i] | (bsp as ICurve).PointAt((double)(i + 1) / (double)(pnts.Length + 1));
                    errorValues[i] = (bsp as ICurve).DistanceTo(pnts[i]);
                }
                double sum = 0.0;
                for (int i = 0; i < errorValues.Length; i++)
                {
                    System.Diagnostics.Trace.Write(errorValues[i].ToString() + " ");
                    sum += errorValues[i] * errorValues[i];
                }
                System.Diagnostics.Trace.WriteLine(sum.ToString());

                return MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(errorValues);
            }
            MathNet.Numerics.Optimization.LevenbergMarquardtMinimizer minimizer = new MathNet.Numerics.Optimization.LevenbergMarquardtMinimizer(0.1, 1e-20, 1e-20, 1e-20, 1000);

            var obj = MathNet.Numerics.Optimization.ObjectiveFunction.NonlinearModel(oeFunc, MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(pnts.Length), MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(pnts.Length));
            Geometry.DistLL(poles[0], dsc.StartDirection, poles[3], dsc.EndDirection, out double par1, out double par2);
            bsp.ThroughPoints(new GeoPoint[] { dsc.PointAt(0.0), dsc.PointAt(0.25), dsc.PointAt(0.5), dsc.PointAt(0.75), dsc.PointAt(1.0) }, 4, false);
            GeoPoint pl1 = bsp.Poles[1];
            GeoPoint pl2 = bsp.Poles[2];
            GeoPoint pl3 = bsp.Poles[3];

            var lmresult = minimizer.FindMinimum(obj, MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(new double[] { pl1.x, pl1.y, pl1.z, 1.0, pl2.x, pl2.y, pl2.z, 1.0, pl3.x, pl3.y, pl3.z, 1.0 }));

            poles[1] = new GeoPoint(lmresult.MinimizingPoint[0], lmresult.MinimizingPoint[1], lmresult.MinimizingPoint[2]);
            poles[2] = new GeoPoint(lmresult.MinimizingPoint[4], lmresult.MinimizingPoint[5], lmresult.MinimizingPoint[6]);
            poles[3] = new GeoPoint(lmresult.MinimizingPoint[8], lmresult.MinimizingPoint[9], lmresult.MinimizingPoint[10]);
            weights[1] = lmresult.MinimizingPoint[3];
            weights[2] = lmresult.MinimizingPoint[7];
            weights[3] = lmresult.MinimizingPoint[11];
            bsp.SetData(4, poles, weights, new double[] { 0, 1 }, new int[] { 5, 5 }, false);

#endif
        }
        private static double SignedDistance(ICurve2D crv, GeoPoint2D p)
        {   // signed distance
            double pos = crv.PositionOf(p);
            GeoPoint2D footPoint = crv.PointAt(pos);
            GeoVector2D dir = crv.DirectionAt(pos).Normalized;
            return Geometry.DistPL(p, footPoint, dir);
        }
        public static void ApproxCurve2D(ICurve2D crv)
        {
#if MATHNET
            //if (crv == null)
            //{
            //    crv = BSpline2D.MakeCircle(new GeoPoint2D(0, 0), 1);
            //    Polynom[] nrbspl = (crv as BSpline2D).Nurbs.CurvePointPolynom(0.125);
            //    crv = crv.Trim(0.0, 0.25);
            //}
            // Funktioniert zwar, konvergiert aber oft nicht oder nur schlecht
            int deg = 4;
            int numPnts = deg * 3; // number of parameters
            GeoPoint2D[] pnts = new GeoPoint2D[numPnts];
            for (int i = 0; i < pnts.Length; i++)
            {
                pnts[i] = crv.PointAt((double)(i + 1) / (double)(pnts.Length + 1));
            }
            GeoPoint2D[] poles = new GeoPoint2D[deg + 1];
            double[] weights = new double[deg + 1];
            poles[0] = crv.PointAt(0.0);
            poles[poles.Length - 1] = crv.PointAt(1.0);
            weights[0] = weights[weights.Length - 1] = 1.0;
            MathNet.Numerics.LinearAlgebra.Vector<double> oeFunc(MathNet.Numerics.LinearAlgebra.Vector<double> parameters, MathNet.Numerics.LinearAlgebra.Vector<double> x)
            {   // parameters pole[1] and pole[2] (including weights) of a NURBS curve
                for (int i = 0; i < deg - 1; i++)
                {
                    poles[i + 1] = new GeoPoint2D(parameters[3 * i], parameters[3 * i + 1]);
                    weights[i + 1] = parameters[3 * i + 2];
                }
                BSpline2D bsppar = new BSpline2D(poles, weights, new double[] { 0, 1 }, new int[] { deg + 1, deg + 1 }, deg, false, 0.0, 1.0);
                double[] errorValues = new double[pnts.Length];
                for (int i = 0; i < pnts.Length; i++)
                {
                    // errorValues[i] = pnts[i] | (bsp as ICurve).PointAt((double)(i + 1) / (double)(pnts.Length + 1));
                    errorValues[i] = SignedDistance(bsppar, pnts[i]);
                }
                double sum = 0.0;
                for (int i = 0; i < errorValues.Length; i++)
                {
                    System.Diagnostics.Trace.Write(errorValues[i].ToString() + " ");
                    sum += errorValues[i] * errorValues[i];
                }
                System.Diagnostics.Trace.WriteLine(sum.ToString());

                return MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(errorValues);
            }
            MathNet.Numerics.Optimization.LevenbergMarquardtMinimizer minimizer = new MathNet.Numerics.Optimization.LevenbergMarquardtMinimizer(0.1, 1e-20, 1e-20, 1e-20, 1000);

            var obj = MathNet.Numerics.Optimization.ObjectiveFunction.NonlinearModel(oeFunc, MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(pnts.Length), MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(pnts.Length));
            GeoPoint2D[] throughpoints = new GeoPoint2D[deg + 1];
            for (int i = 0; i < throughpoints.Length; i++)
            {
                throughpoints[i] = crv.PointAt((double)i / (double)(throughpoints.Length - 1));
            }
            BSpline2D bsp = new BSpline2D(throughpoints, deg, false);
            MathNet.Numerics.LinearAlgebra.Vector<double> initialGuess = MathNet.Numerics.LinearAlgebra.CreateVector.Dense<double>(3 * (deg - 1));
            for (int i = 0; i < deg - 1; i++)
            {
                initialGuess[3 * i] = bsp.Poles[i + 1].x;
                initialGuess[3 * i + 1] = bsp.Poles[i + 1].y;
                initialGuess[3 * i + 2] = 1.0; //weight
            }
            var lmresult = minimizer.FindMinimum(obj, initialGuess);

            for (int i = 0; i < deg - 1; i++)
            {
                poles[i + 1] = new GeoPoint2D(lmresult.MinimizingPoint[3 * i], lmresult.MinimizingPoint[3 * i + 1]);
                weights[i + 1] = lmresult.MinimizingPoint[3 * i + 2];
            }
            bsp = new BSpline2D(poles, weights, new double[] { 0, 1 }, new int[] { deg + 1, deg + 1 }, deg, false, 0.0, 1.0);

#endif
        }
        public static void TestCylCylIntersection()
        {
            // using this Maxima input:
            // k1: 2.34314575050762
            // k2: 1.37258300203048
            // k3: 0.970562748477142
            // k4: 3.31370849898476
            // /*unit circle, first quadrant (0: [0,1]:*/
            // k1: sqrt(2)-2;
            // k2: sqrt(2)-1;
            // w: 1 + k1*u -k1*u^2;
            // cy1x: x = (1+k1*u-k2*u^2)/w;
            // cy1y: y = (sqrt(2)*u-k2*u^2) / w;
            // /* this defines the unit cylinder */
            // xo: 
            // fco(xo,yo):= xo^2 + yo^2 - r^2; /* cylinder around z-axis, radius r */
            // fco(x+dx,c*y-s*z); /* moved dx to right and rotated around x-axis */
            // solve([cy1x,cy1y,fco(x+dx,c*y-s*z)],[x,y,z]); /* x,y,z depending on u */
            //stringout("C:/Temp/cylcyl.txt", values);

            // it turns out that the general form of a quadrant of a cylinder/cylinder intersection should be something like
            // x = (sqrt(a*u4 + b*u³ + c*u² +d) + e*u² + f*u +g)/(h*u²+i*u+j);
            // same for y and z which means 3*10 unknowns

            // let's try with r=2, dx=2.5

            double sqrt2 = Math.Sqrt(2.0);
            GeoPoint[] pnts = new GeoPoint[100];
            double dx = 2.5;
            double r = 2;
            double s = Math.Sin(Math.PI / 4.0);
            double c = Math.Cos(Math.PI / 4.0);
            for (int i = 0; i < 100; i++)
            {
                double u = i * 0.01;
                double x = ((sqrt2 - 1) * u * u + (2 - sqrt2) * u - 1) / ((sqrt2 - 2) * u * u + (2 - sqrt2) * u - 1);
                double y = ((sqrt2 - 1) * u * u - sqrt2 * u) / ((sqrt2 - 2) * u * u + (2 - sqrt2) * u - 1);
                double z = (Math.Sqrt(((9232.0 * sqr(r) + Math.Sqrt(2.0) * ((-6528.0) * sqr(r) + 6528.0 * sqr(dx) + 9232.0 * dx + 3264.0) - 9232.0 * sqr(dx) - 13056.0 * dx - 4616.0) * Math.Pow(u, 16.0) + (Math.Sqrt(2.0) * (52224.0 * sqr(r) - 52224.0 * sqr(dx) - 77680.0 * dx - 28816.0) - 73856.0 * sqr(r) + 73856.0 * sqr(dx) + 109856.0 * dx + 40752.0) * Math.Pow(u, 15.0) + (280128.0 * sqr(r) + Math.Sqrt(2.0) * ((-198080.0) * sqr(r) + 198080.0 * sqr(dx) + 308016.0 * dx + 119320.0) - 280128.0 * sqr(dx) - 435600.0 * dx - 168744.0) * Math.Pow(u, 14.0) + (Math.Sqrt(2.0) * (472640.0 * sqr(r) - 472640.0 * sqr(dx) - 764400.0 * dx - 307880.0) - 668416.0 * sqr(r) + 668416.0 * sqr(dx) + 1081024.0 * dx + 435408.0) * Math.Pow(u, 13.0) + (1122688.0 * sqr(r) + Math.Sqrt(2.0) * ((-793856.0) * sqr(r) + 793856.0 * sqr(dx) + 1329384.0 * dx + 554488.0) - 1122688.0 * sqr(dx) - 1880032.0 * dx - 784164.0) * Math.Pow(u, 12.0) + (Math.Sqrt(2.0) * (995008.0 * sqr(r) - 995008.0 * sqr(dx) - 1718472.0 * dx - 739544.0) - 1407168.0 * sqr(r) + 1407168.0 * sqr(dx) + 2430288.0 * dx + 1045872.0) * Math.Pow(u, 11.0) + (1361248.0 * sqr(r) + Math.Sqrt(2.0) * ((-962528.0) * sqr(r) + 962528.0 * sqr(dx) + 1708448.0 * dx + 756000.0) - 1361248.0 * sqr(dx) - 2416120.0 * dx - 1069144.0) * Math.Pow(u, 10.0) + (Math.Sqrt(2.0) * (732960.0 * sqr(r) - 732960.0 * sqr(dx) - 1332736.0 * dx - 604480.0) - 1036608.0 * sqr(r) + 1036608.0 * sqr(dx) + 1884800.0 * dx + 854864.0) * Math.Pow(u, 9.0) + (627976.0 * sqr(r) + Math.Sqrt(2.0) * ((-444000.0) * sqr(r) + 444000.0 * sqr(dx) + 824580.0 * dx + 382200.0) - 627976.0 * sqr(dx) - 1166184.0 * dx - 540518.0) * Math.Pow(u, 8.0) + (Math.Sqrt(2.0) * (214656.0 * sqr(r) - 214656.0 * sqr(dx) - 406044.0 * dx - 191788.0) - 303648.0 * sqr(r) + 303648.0 * sqr(dx) + 574312.0 * dx + 271244.0) * Math.Pow(u, 7.0) + (116816.0 * sqr(r) + Math.Sqrt(2.0) * ((-82544.0) * sqr(r) + 82544.0 * sqr(dx) + 158620.0 * dx + 76142.0) - 116816.0 * sqr(dx) - 224420.0 * dx - 107706.0) * Math.Pow(u, 6.0) + (Math.Sqrt(2.0) * (24976.0 * sqr(r) - 24976.0 * sqr(dx) - 48636.0 * dx - 23666.0) - 35392.0 * sqr(r) + 35392.0 * sqr(dx) + 68880.0 * dx + 33500.0) * Math.Pow(u, 5.0) + (8288.0 * sqr(r) + Math.Sqrt(2.0) * ((-5824.0) * sqr(r) + 5824.0 * sqr(dx) + 11466.0 * dx + 5642.0) - 8288.0 * sqr(dx) - 16296.0 * dx - 8009.0) * Math.Pow(u, 4.0) + (Math.Sqrt(2.0) * (1008.0 * sqr(r) - 1008.0 * sqr(dx) - 2002.0 * dx - 994.0) - 1456.0 * sqr(r) + 1456.0 * sqr(dx) + 2884.0 * dx + 1428.0) * Math.Pow(u, 3.0) + (184.0 * sqr(r) + Math.Sqrt(2.0) * ((-120.0) * sqr(r) + 120.0 * sqr(dx) + 240.0 * dx + 120.0) - 184.0 * sqr(dx) - 366.0 * dx - 182.0) * sqr(u) + (Math.Sqrt(2.0) * (8.0 * sqr(r) - 8.0 * sqr(dx) - 16.0 * dx - 8.0) - 16.0 * sqr(r) + 16.0 * sqr(dx) + 32.0 * dx + 16.0) * u + sqr(r) - sqr(dx) - 2.0 * dx - 1.0)) + (17.0 * Math.Pow(2.0, (3.0 / 2.0)) * c - 48.0 * c) * Math.Pow(u, 8.0) + (172.0 * c - 61.0 * Math.Pow(2.0, (3.0 / 2.0)) * c) * Math.Pow(u, 7.0) + (3.0 * Math.Pow(2.0, (13.0 / 2.0)) * c - 270.0 * c) * Math.Pow(u, 6.0) + (240.0 * c - 43.0 * Math.Pow(2.0, (5.0 / 2.0)) * c) * Math.Pow(u, 5.0) + (95.0 * Math.Sqrt(2.0) * c - 130.0 * c) * Math.Pow(u, 4.0) + (42.0 * c - 33.0 * Math.Sqrt(2.0) * c) * Math.Pow(u, 3.0) + (7.0 * Math.Sqrt(2.0) * c - 7.0 * c) * sqr(u) - Math.Sqrt(2.0) * c * u) / ((3.0 * Math.Pow(2.0, (9.0 / 2.0)) * s - 68.0 * s) * Math.Pow(u, 8.0) + (272.0 * s - 3.0 * Math.Pow(2.0, (13.0 / 2.0)) * s) * Math.Pow(u, 7.0) + (43.0 * Math.Pow(2.0, (7.0 / 2.0)) * s - 488.0 * s) * Math.Pow(u, 6.0) + (512.0 * s - 45.0 * Math.Pow(2.0, (7.0 / 2.0)) * s) * Math.Pow(u, 5.0) + (15.0 * Math.Pow(2.0, (9.0 / 2.0)) * s - 344.0 * s) * Math.Pow(u, 4.0) + (152.0 * s - 13.0 * Math.Pow(2.0, (7.0 / 2.0)) * s) * Math.Pow(u, 3.0) + (7.0 * Math.Pow(2.0, (5.0 / 2.0)) * s - 44.0 * s) * sqr(u) + (8.0 * s - Math.Pow(2.0, (5.0 / 2.0)) * s) * u - s);
                if (double.IsNaN(z)) z = 0.0;
                pnts[i] = new GeoPoint(x, y, z);
            }

            //double k1 = 2.34314575050762;
            //double k2 = 1.37258300203048;
            //double k3 = 0.970562748477142;
            //double k4 = 3.31370849898476;

            //double u0 = (-(Math.Sqrt(((sqr(k2) + 4.0 * k1 * k2) * sqr(r) + (4.0 * k1 * k3 + (2.0 * dx + 2.0) * sqr(k2) + ((8.0 * dx + 4.0) * k1) * k2) * r + ((4.0 * dx + 4.0) * k1) * k3 + (sqr(dx) + 2.0 * dx + 1.0) * sqr(k2) + ((4.0 * sqr(dx) + 4.0 * dx) * k1) * k2)) + k2 * r + (dx + 1.0) * k2)) / (2.0 * k2 * r + 2.0 * k3 + 2.0 * dx * k2);
            //double u1 = (Math.Sqrt(((sqr(k2) + 4.0 * k1 * k2) * sqr(r) + (4.0 * k1 * k3 + (2.0 * dx + 2.0) * sqr(k2) + ((8.0 * dx + 4.0) * k1) * k2) * r + ((4.0 * dx + 4.0) * k1) * k3 + (sqr(dx) + 2.0 * dx + 1.0) * sqr(k2) + ((4.0 * sqr(dx) + 4.0 * dx) * k1) * k2)) - k2 * r + (-dx - 1.0) * k2) / (2.0 * k2 * r + 2.0 * k3 + 2.0 * dx * k2);
            //double u2 = (Math.Sqrt(((sqr(k2) + 4.0 * k1 * k2) * sqr(r) + (((-4.0) * k1) * k3 + ((-2.0) * dx - 2.0) * sqr(k2) + (((-8.0) * dx - 4.0) * k1) * k2) * r + ((4.0 * dx + 4.0) * k1) * k3 + (sqr(dx) + 2.0 * dx + 1.0) * sqr(k2) + ((4.0 * sqr(dx) + 4.0 * dx) * k1) * k2)) - k2 * r + (dx + 1.0) * k2) / (2.0 * k2 * r - 2.0 * k3 - 2.0 * dx * k2);
            //double u3 = (-(Math.Sqrt(((sqr(k2) + 4.0 * k1 * k2) * sqr(r) + (((-4.0) * k1) * k3 + ((-2.0) * dx - 2.0) * sqr(k2) + (((-8.0) * dx - 4.0) * k1) * k2) * r + ((4.0 * dx + 4.0) * k1) * k3 + (sqr(dx) + 2.0 * dx + 1.0) * sqr(k2) + ((4.0 * sqr(dx) + 4.0 * dx) * k1) * k2)) + k2 * r + (-dx - 1.0) * k2)) / (2.0 * k2 * r - 2.0 * k3 - 2.0 * dx * k2);

            //u1 += 1e-6;
            //u3 -= 1e-6;
            //u1 = 0.0;
            //u3 = 0.25;
            //double du = (u3 - u1) / pnts.Length;
            //for (int i = 0; i < pnts.Length; i++)
            //{
            //    double u = u1 + i * du;
            //    double x = (k3 * sqr(u) + k2 * u - k1) / (k2 * sqr(u) + k2 * u - k1);
            //    double y = (k3 * sqr(u) - k4 * u) / (k2 * sqr(u) + k2 * u - k1);
            //    double z = (Math.Sqrt(((sqr(k2) * (sqr(r) - sqr(dx)) - sqr(k3) - 2.0 * dx * k2 * k3) * Math.Pow(u, 4.0) + (sqr(k2) * (2.0 * sqr(r) - 2.0 * sqr(dx) - 2.0 * dx) + (((-2.0) * dx - 2.0) * k2) * k3) * Math.Pow(u, 3.0) + (sqr(k2) * (sqr(r) - sqr(dx) - 2.0 * dx - 1.0) + k1 * k2 * ((-2.0) * sqr(r) + 2.0 * sqr(dx) + 2.0 * dx) + ((2.0 * dx + 2.0) * k1) * k3) * sqr(u) + (k1 * k2 * ((-2.0) * sqr(r) + 2.0 * sqr(dx) + 4.0 * dx + 2.0)) * u + sqr(k1) * (sqr(r) - sqr(dx) - 2.0 * dx - 1.0))) + c * k3 * sqr(u) - c * k4 * u) / (k2 * s * sqr(u) + k2 * s * u - k1 * s);
            //    pnts[i] = new GeoPoint(x, y, z);
            //}
        }
        static void Test()
        {
            double u = 0, p0x = 0, p0y = 0, p0z = 0, p0w = 0, p1x = 0, p1y = 0, p1z = 0, p1w = 0, p2x = 0, p2y = 0, p2z = 0, p2w = 0, p3x = 0, p3y = 0, p3z = 0, p3w = 0, p4x = 0, p4y = 0, p4z = 0, p4w = 0, dx = 0, dy = 0, dz = 0, lx = 0, ly = 0, lz = 0;

            double tmp0 = (2.0 * dx);
            double tmp1 = quad(u);
            double tmp2 = (6.0 * tmp1);
            double tmp3 = cube(u);
            double tmp4 = (12.0 * tmp3);
            double tmp5 = (tmp2 - tmp4);
            double tmp6 = sqr(u);
            double tmp7 = (6.0 * tmp6);
            double tmp8 = (tmp5 + tmp7);
            double tmp9 = (p2x * tmp8);
            double tmp10 = (4.0 * tmp3);
            double tmp11 = (tmp1 - tmp10);
            double tmp12 = (tmp11 + tmp7);
            double tmp13 = (4.0 * u);
            double tmp14 = (tmp12 - tmp13);
            double tmp15 = (tmp14 + 1.0);
            double tmp16 = (p0x * tmp15);
            double tmp17 = (tmp9 + tmp16);
            double tmp18 = (p4x * tmp1);
            double tmp19 = (tmp17 + tmp18);
            double tmp20 = (12.0 * tmp6);
            double tmp21 = (4.0 * tmp1);
            double tmp22 = (tmp10 - tmp21);
            double tmp23 = (p3x * tmp22);
            double tmp24 = (p2w * tmp8);
            double tmp25 = (p0w * tmp15);
            double tmp26 = (tmp24 + tmp25);
            double tmp27 = (p4w * tmp1);
            double tmp28 = (tmp26 + tmp27);
            double tmp29 = (p3w * tmp22);
            double tmp30 = (p2z * tmp8);
            double tmp31 = (p0z * tmp15);
            double tmp32 = (tmp30 + tmp31);
            double tmp33 = (p4z * tmp1);
            double tmp34 = (tmp32 + tmp33);
            double tmp35 = (p3z * tmp22);
            double tmp36 = (2.0 * dy);
            double tmp37 = (p2y * tmp8);
            double tmp38 = (p0y * tmp15);
            double tmp39 = (tmp37 + tmp38);
            double tmp40 = (p4y * tmp1);
            double tmp41 = (tmp39 + tmp40);
            double tmp42 = (p3y * tmp22);
            double tmp43 = (24.0 * tmp3);
            double tmp44 = (36.0 * tmp6);
            double tmp45 = (tmp43 - tmp44);
            double tmp46 = (12.0 * u);
            double tmp47 = (tmp45 + tmp46);
            double tmp48 = (p2z * tmp47);
            double tmp49 = (tmp10 - tmp20);
            double tmp50 = (tmp49 + tmp46);
            double tmp51 = (tmp50 - 4.0);
            double tmp52 = (p0z * tmp51);
            double tmp53 = (tmp48 + tmp52);
            double tmp54 = (4.0 * p4z);
            double tmp55 = (tmp54 * tmp3);
            double tmp56 = (tmp53 + tmp55);
            double tmp57 = (24.0 * u);
            double tmp58 = (16.0 * tmp3);
            double tmp59 = (tmp20 - tmp58);
            double tmp60 = (p3z * tmp59);
            double tmp61 = (p2w * tmp47);
            double tmp62 = (p0w * tmp51);
            double tmp63 = (tmp61 + tmp62);
            double tmp64 = (4.0 * p4w);
            double tmp65 = (tmp64 * tmp3);
            double tmp66 = (tmp63 + tmp65);
            double tmp67 = (p3w * tmp59);
            double tmp68 = (2.0 * dz);
            double tmp69 = (p2y * tmp47);
            double tmp70 = (p0y * tmp51);
            double tmp71 = (tmp69 + tmp70);
            double tmp72 = (4.0 * p4y);
            double tmp73 = (tmp72 * tmp3);
            double tmp74 = (tmp71 + tmp73);
            double tmp75 = (p3y * tmp59);
            double tmp76 = (p2x * tmp47);
            double tmp77 = (p0x * tmp51);
            double tmp78 = (tmp76 + tmp77);
            double tmp79 = (4.0 * p4x);
            double tmp80 = (tmp79 * tmp3);
            double tmp81 = (tmp78 + tmp80);
            double tmp82 = (p3x * tmp59);
            double tmp83 = sqr(dz);
            double tmp84 = (2.0 * tmp83);
            double tmp85 = sqr(dy);
            double tmp86 = (2.0 * tmp85);
            double tmp87 = (tmp0 * dz);
            double tmp88 = (tmp0 * dy);
            double tmp89 = sqr(dx);
            double tmp90 = (2.0 * tmp89);
            double tmp91 = (tmp36 * dz);
            double tmp92 = (tmp84 * tmp8);
            double tmp93 = (tmp86 * tmp8);
            double tmp94 = (tmp87 * tmp8);
            double tmp95 = (tmp88 * tmp8);
            double tmp96 = (tmp90 * tmp8);
            double tmp97 = (tmp91 * tmp8);
            double tmp98 = (dz * tmp8);
            double tmp99 = (dx * tmp8);
            double tmp100 = (dy * tmp8);
            double tmp101 = (tmp84 * tmp22);
            double tmp102 = (tmp86 * tmp22);
            double tmp103 = (tmp87 * tmp22);
            double tmp104 = (tmp88 * tmp22);
            double tmp105 = (tmp90 * tmp22);
            double tmp106 = (tmp91 * tmp22);
            double tmp107 = (dz * tmp22);
            double tmp108 = (dx * tmp22);
            double tmp109 = (dy * tmp22);

            double uu = (tmp0 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp68 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp36 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p1xx = ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) * ((tmp84 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp86 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - ((tmp87 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp88 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp36 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * (((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p1yy = ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) * ((tmp84 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp90 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - ((tmp91 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp88 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp68 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * (((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p1zz = ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) * ((tmp86 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp90 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - ((tmp91 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp87 * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp0 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * (((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p1ww = (tmp0 * (((dz * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - ((dx * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * (((dy * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - ((dz * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp68 * (((dy * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - ((dz * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * (((dx * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - ((dy * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp36 * (((dx * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - ((dy * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * (((dz * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - ((dx * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp0 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((-((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13))) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) + (((2.0 * (tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67)) * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / cube((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp68 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((-((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13))) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) + (((2.0 * (tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67)) * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / cube((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp36 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((-((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13))) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) + (((2.0 * (tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67)) * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13)) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / cube((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p2xx = ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) * (tmp92 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + tmp93 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp94 * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - (tmp95 * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp36 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * (tmp47 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * tmp8) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p2yy = ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) * (tmp92 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + tmp96 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp97 * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - (tmp95 * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp68 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * (tmp47 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * tmp8) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p2zz = ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) * (tmp93 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + tmp96 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp97 * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - (tmp94 * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp0 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * (tmp47 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * tmp8) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p2ww = (tmp0 * ((tmp98 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp99 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * ((tmp100 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp98 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp68 * ((tmp100 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp98 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * ((tmp99 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp100 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp36 * ((tmp99 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp100 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * ((tmp98 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp99 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp0 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((-((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) * tmp8)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp47 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) + (((2.0 * (tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67)) * tmp8) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / cube((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp68 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((-((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) * tmp8)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp47 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) + (((2.0 * (tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67)) * tmp8) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / cube((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp36 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((-((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) * tmp8)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp47 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) + (((2.0 * (tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67)) * tmp8) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / cube((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p3xx = ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) * (tmp101 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + tmp102 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp103 * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - (tmp104 * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp36 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * (tmp59 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * tmp22) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p3yy = ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) * (tmp101 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + tmp105 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp106 * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - (tmp104 * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp68 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * (tmp59 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * tmp22) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p3zz = ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) * (tmp102 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + tmp105 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp106 * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - (tmp103 * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) + (tmp0 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * (tmp59 / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * tmp22) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))); ;
            double p3ww = (tmp0 * ((tmp107 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp108 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * ((tmp109 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp107 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp68 * ((tmp109 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp107 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * ((tmp108 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp109 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp36 * ((tmp108 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp109 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * ((tmp107 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp108 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29) - ((tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp0 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp36 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((-((tmp56 + p1z * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp60) * tmp22)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp59 * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) + (((2.0 * (tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67)) * tmp22) * (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35)) / cube((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp68 * (dy * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dz * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp0 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((-((tmp74 + p1y * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp75) * tmp22)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp59 * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) + (((2.0 * (tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67)) * tmp22) * (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42)) / cube((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) + (tmp36 * (dx * (ly - (tmp41 + p1y * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp42) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dy * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29))) - tmp68 * (dz * (lx - (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - dx * (lz - (tmp34 + p1z * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp35) / (tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)))) * ((-((tmp81 + p1x * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp82) * tmp22)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) - (tmp59 * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / sqr((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)) + (((2.0 * (tmp66 + p1w * ((-16.0) * tmp3 + tmp44 - tmp57 + 4.0) + tmp67)) * tmp22) * (tmp19 + p1x * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp23)) / cube((tmp28 + p1w * ((-4.0) * tmp1 + tmp4 - tmp20 + tmp13) + tmp29)));
        }
#endif
    }
}
