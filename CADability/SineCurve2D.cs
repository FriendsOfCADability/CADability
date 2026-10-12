using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.Curve2D
{
    [Serializable()]
    public class SineCurve2D : GeneralCurve2D, ISerializable
    {
        double ustart, udiff;
        ModOp2D fromUnit;

        public SineCurve2D(double ustart, double udiff, ModOp2D fromUnit)
        {
            this.ustart = ustart;
            this.udiff = udiff;
            this.fromUnit = fromUnit;
        }
        public override ICurve2D Clone()
        {
            return new SineCurve2D(ustart, udiff, fromUnit);
        }
        /// <summary>
        /// The transformation which maps the unit sine curve (u, sin(u)) onto this curve.
        /// </summary>
        public ModOp2D FromUnit => fromUnit;
        /// <summary>
        /// The parameter u of the unit sine curve (u, sin(u)) at the start point of this curve.
        /// </summary>
        public double UStart => ustart;
        /// <summary>
        /// The difference of the parameter u of the unit sine curve (u, sin(u)) between end and start
        /// point of this curve. Negative when the curve is reversed.
        /// </summary>
        public double UDiff => udiff;
        /// <summary>
        /// Creates the sine curve with the period 2*pi, which starts at <paramref name="p1"/>, ends at <paramref name="p4"/>
        /// and passes (in the least squares sense) through <paramref name="p2"/> and <paramref name="p3"/>:
        /// y == a*sin(x-x0)+y0. This is how a planar section of a cylinder (an ellipse) looks like in the parameter
        /// system of the cylinder. Returns null, if the points cannot be fitted.
        /// </summary>
        public static SineCurve2D Create(GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3, GeoPoint2D p4)
        {
            if (Math.Abs(p4.x - p1.x) < 1e-14) return null; // x would be constant
            (double a, double b, double c, double x0, double y0, NonlinearMinimizationResult result) = Fit4Points(p1, p2, p3, p4);
            if (result.ReasonForExit != ExitCondition.Converged && result.ReasonForExit != ExitCondition.RelativePoints
                && result.ReasonForExit != ExitCondition.RelativeGradient) return null;
            if (double.IsNaN(a) || double.IsNaN(x0) || double.IsNaN(y0)) return null;
            return new SineCurve2D(c, b, new ModOp2D(1, 0, x0, 0, a, y0));
        }
        /// <summary>
        /// Fits f(u)=(b*u+c+x0, a*sin(b*u+c)+y0) with u in [0,1], u=0 hits p1, u=1 hits p4.
        /// Assumes the 4 points lie on such a curve.
        /// </summary>
        private static (double a, double b, double c, double x0, double y0, NonlinearMinimizationResult result)
            Fit4Points(GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3, GeoPoint2D p4)
        {
            var xs = Vector<double>.Build.Dense(new[] { p1.x, p2.x, p3.x, p4.x });
            var ys = Vector<double>.Build.Dense(new[] { p1.y, p2.y, p3.y, p4.y });

            // Endpoint constraint u=0 -> p1, u=1 -> p4 implies:
            double b = p4.x - p1.x;

            // Parameter vector p = [a, x0, y0]
            Func<Vector<double>, Vector<double>, Vector<double>> values =
                (p, x) =>
                {
                    double a = p[0], x0 = p[1], y0 = p[2];
                    var yhat = Vector<double>.Build.Dense(x.Count);
                    for (int i = 0; i < x.Count; i++)
                        yhat[i] = a * Math.Sin(x[i] - x0) + y0;
                    return yhat;
                };

            // Jacobian: rows = data points (4), cols = parameters (3)
            // dy/da  = sin(x-x0)
            // dy/dx0 = -a*cos(x-x0)
            // dy/dy0 = 1
            Func<Vector<double>, Vector<double>, Matrix<double>> jacobian =
                (p, x) =>
                {
                    double a = p[0], x0 = p[1];
                    var J = Matrix<double>.Build.Dense(x.Count, 3);
                    for (int i = 0; i < x.Count; i++)
                    {
                        double t = x[i] - x0;
                        J[i, 0] = Math.Sin(t);
                        J[i, 1] = -a * Math.Cos(t);
                        J[i, 2] = 1.0;
                    }
                    return J;
                };

            var objective = ObjectiveFunction.NonlinearModel(values, jacobian, xs, ys);

            // initial guess
            double y0Guess = (p1.y + p2.y + p3.y + p4.y) / 4.0;
            double ymin = Math.Min(Math.Min(p1.y, p2.y), Math.Min(p3.y, p4.y));
            double ymax = Math.Max(Math.Max(p1.y, p2.y), Math.Max(p3.y, p4.y));
            double aGuess = 0.5 * (ymax - ymin);
            if (Math.Abs(aGuess) < 1e-12) aGuess = 1.0;
            double x0Guess = p1.x; // often fine; LM will refine

            var initialGuess = Vector<double>.Build.Dense(new[] { aGuess, x0Guess, y0Guess });

            var lm = new LevenbergMarquardtMinimizer(
                initialMu: 1e-3,
                gradientTolerance: 1e-15,
                stepTolerance: 1e-15,
                functionTolerance: 1e-15,
                maximumIterations: 50);

            var res = lm.FindMinimum(objective, initialGuess);

            double ra = res.MinimizingPoint[0];
            double rx0 = res.MinimizingPoint[1];
            double ry0 = res.MinimizingPoint[2];
            double c = p1.x - rx0; // because x(0)=c+x0 = X1
            return (ra, b, c, rx0, ry0, res);
        }
        /// <summary>
        /// Shifts this curve along its sine wave so that it starts at <paramref name="p2d"/>, which is
        /// expected to lie on the curve. NOTE: the parameter span is kept, so the END POINT MOVES BY THE
        /// SAME AMOUNT - this corrects a start point that is slightly off, it does not trim the curve.
        /// </summary>
        internal void StartAt(GeoPoint2D p2d)
        {
            double d = PositionOf(p2d);
            ustart += d * udiff;
        }

        public override void Copy(ICurve2D toCopyFrom)
        {
            SineCurve2D other = toCopyFrom as SineCurve2D;
            if (other is SineCurve2D)
            {
                ustart = other.ustart;
                udiff = other.udiff;
                fromUnit = other.fromUnit;
            }
        }

        public override GeoVector2D DirectionAt(double pos)
        {
            double u = PosToPar(pos);
            double du = Math.Cos(u);
            GeoVector2D dir = new GeoVector2D(1, du);
            // enter Integrate[sqrt(1+cos(x)^2),x] at https://www.wolframalpha.com/input/?i=Integrate%5Bsqrt(1%2Bcos(x)%5E2),x%5D
            // and the result for the full period is 7.64039557805542, but does this help for the length of the direction???
            return fromUnit * (udiff * dir);
        }
        public override double PositionOf(GeoPoint2D p)
        {
            ModOp2D toUnit = fromUnit.GetInverse();
            double u = (toUnit * p).x;
#if DEBUG
            GeoPoint2D tp = PointAt(ParToPos(u));
            double d = p | tp;
#endif
            return ParToPos(u);
        }
        private double ParToPos(double par)
        {
            return (par - ustart) / udiff;
        }
        private double PosToPar(double pos)
        {
            return ustart + pos * udiff;
        }

        public override double[] GetInflectionPoints()
        {
            List<double> infl = new List<double>();
            if (udiff < 0)
            {
                double u = 0.0;
                while (u < ustart) u += Math.PI;
                while (u > ustart) u -= Math.PI;
                while (u > ustart + udiff)
                {
                    infl.Add(ParToPos(u));
                    u -= Math.PI;
                }
            }
            else
            {
                double u = 0.0;
                while (u > ustart) u -= Math.PI;
                while (u < ustart) u += Math.PI;
                while (u < ustart + udiff)
                {
                    infl.Add(ParToPos(u));
                    u += Math.PI;
                }
            }
            return infl.ToArray();
        }

        public override GeoPoint2D PointAt(double pos)
        {
            double u = PosToPar(pos);
            GeoPoint2D p = new GeoPoint2D(u, Math.Sin(u));
            return fromUnit * p;
        }

        public override void Reverse()
        {
            ClearTriangulation();
            ustart += udiff;
            udiff = -udiff;
        }

        public override ICurve2D GetModified(ModOp2D m)
        {
            return new SineCurve2D(ustart, udiff, m * fromUnit);
        }
        public override void Move(double x, double y)
        {
            ClearTriangulation();
            fromUnit = ModOp2D.Translate(x, y) * fromUnit;
        }
        public override double GetArea()
        {
            GeoPoint2D startPoint = StartPoint;
            GeoPoint2D endPoint = EndPoint;
            double triangle = (startPoint.x * endPoint.y - startPoint.y * endPoint.x) / 2.0;
            double a0 = -Math.Cos(ustart) + Math.Cos(ustart + udiff);
            double a1 = udiff * (Math.Sin(ustart) + Math.Sin(ustart + udiff)) / 2.0;
            double r1 = triangle + fromUnit.Determinant * (a0 + a1);
            return r1;

            //Unreachable code
            /*
            // following the debug code to find the correct signs:
            // I don't understand the signs, they should be wrong in the code above, but turn out to be correct
#if DEBUG
            double aa = Approximate(true, 1e-8).GetArea(); // correct result approximated to at least 7 valid digits
            double r2 = triangle - fromUnit.Determinant * (a0 + a1);
            double r3 = triangle + fromUnit.Determinant * (a0 - a1);
            double r4 = triangle - fromUnit.Determinant * (a0 - a1);
            System.Diagnostics.Trace.WriteLine("SinArea. " + (udiff > 0).ToString() + ", " + (fromUnit.Determinant > 0).ToString() + ", " + aa.ToString() + ", " + r1.ToString() + ", " + r2.ToString() + ", " + r3.ToString() + ", " + r4.ToString());
#endif
            */
        }
        internal override void GetTriangulationPoints(out GeoPoint2D[] interpol, out double[] interparam)
        {
            GetTriangulationBasis(out interpol, out _, out interparam);
        }

        protected override void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] positions)
        {
            List<double> uvalues = new List<double>();
            uvalues.Add(ustart);
            if (udiff < 0)
            {
                double u = 0.0;
                while (u < ustart) u += Math.PI / 2;
                while (u > ustart) u -= Math.PI / 2;
                while (u > ustart + udiff+1e-4)
                {
                    if (u < uvalues[uvalues.Count - 1] - 1e-4) uvalues.Add(u);
                    u -= Math.PI / 2;
                }
            }
            else
            {
                double u = 0.0;
                while (u > ustart) u -= Math.PI / 2;
                while (u < ustart) u += Math.PI / 2;
                while (u < ustart + udiff -1e-4)
                {
                    if (u > uvalues[uvalues.Count - 1] + 1e-4) uvalues.Add(u);
                    u += Math.PI / 2;
                }
            }
            uvalues.Add(ustart + udiff);
            positions = new double[uvalues.Count];
            points = new GeoPoint2D[uvalues.Count];
            directions = new GeoVector2D[uvalues.Count];
            for (int i = 0; i < uvalues.Count; i++)
            {
                positions[i] = ParToPos(uvalues[i]);
                points[i] = PointAt(positions[i]);
                directions[i] = DirectionAt(positions[i]);
            }
        }
        public override ICurve2D Trim(double StartPos, double EndPos)
        {
            double us = PosToPar(StartPos);
            double ue = PosToPar(EndPos);
            SineCurve2D res = Clone() as SineCurve2D;
            res.ustart = us;
            res.udiff = ue - us;
            return res;
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected SineCurve2D(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            ustart = info.GetDouble("Ustart");
            udiff = info.GetDouble("Udiff");
            fromUnit = (ModOp2D)info.GetValue("FromUnit", typeof(ModOp2D));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Ustart", ustart);
            info.AddValue("Udiff", udiff);
            info.AddValue("FromUnit", fromUnit, typeof(ModOp2D));
        }

        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv, out GeoVector2D deriv2)
        {   // not yet tested
            point = fromUnit * new GeoPoint2D(position, Math.Sin(ustart + position * udiff));
            deriv = fromUnit * new GeoVector2D(1, udiff * Math.Cos(ustart + position * udiff));
            deriv2 = fromUnit * new GeoVector2D(1, -udiff *udiff * Math.Sin(ustart + position * udiff));
            return true;
        }

        #endregion
    }
}
