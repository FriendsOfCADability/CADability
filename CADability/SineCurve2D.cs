using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

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

        /// <summary>
        /// Fits a sine curve y = A * sin(B*(x - C)) + D to the given points.
        /// Uses Levenberg–Marquardt (via MathNet.Numerics.Fit.Curve).
        /// </summary>
        /// <param name="points">List of 2D points (x,y).</param>
        /// <param name="fx">Output: x-scale factor B.</param>
        /// <param name="fy">Output: y-scale factor A.</param>
        /// <param name="tx">Output: x-offset C.</param>
        /// <param name="ty">Output: y-offset D.</param>
        /// <returns>True if the algorithm converged; false otherwise.</returns>
        public static SineCurve2D FitSineCurve(IEnumerable<GeoPoint2D> points)
        {
            // Initialize outputs

            // Need at least 4 points to determine A, B, C, D
            if (points == null)
                return null;

            // Extract x- and y-arrays
            double[] xs = points.Select(p => p.x).ToArray();
            double[] ys = points.Select(p => p.y).ToArray();
            if (xs.Length < 4) return null;

            // Initial guess for vertical offset D and amplitude A
            double yMin = ys.Min();
            double yMax = ys.Max();
            double initialOffset = (yMax + yMin) / 2.0;       // D0
            double initialAmplitude = (yMax - yMin) / 2.0;    // A0

            // Initial guess for x-scale B: assume one full period spans the x-range
            double xMin = xs.Min();
            double xMax = xs.Max();
            double approximatePeriod = xMax - xMin;
            if (approximatePeriod <= 0)
                return null;
            double initialScale = 2.0 * Math.PI / approximatePeriod;  // B0

            // Initial guess for phase shift C
            double initialPhase = xMin;  // C0

            try
            {
                // Perform the nonlinear fit using Levenberg–Marquardt
                // Model: y = A * sin(B*(x - C)) + D
                (double fy, double fx, double tx, double ty) = Fit.Curve(
                    xs, ys,
                    (double x, double A, double B, double C, double D)
                        => A * Math.Sin(B * (x - C)) + D,
                    initialAmplitude,
                    initialScale,
                    initialPhase,
                    initialOffset
                );
                ModOp2D fromUnit = new ModOp2D(fx, 0, tx, 0, fy, ty);
                ModOp2D toUnit = fromUnit.GetInverse();
                double ustart = toUnit * points.First().x;
                double uend = toUnit * points.Last().x;
                return new SineCurve2D(0, 2 * Math.PI, fromUnit);
                return new SineCurve2D(ustart, uend - ustart, fromUnit);
            }
            catch
            {
                // In case of non-convergence or other fitting error
                return null;
            }
        }
        public override ICurve2D Clone()
        {
            return new SineCurve2D(ustart, udiff, fromUnit);
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
                while (u > ustart + udiff + 1e-4)
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
                while (u < ustart + udiff - 1e-4)
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
            deriv2 = fromUnit * new GeoVector2D(1, -udiff * udiff * Math.Sin(ustart + position * udiff));
            return true;
        }

        internal void StartAt(GeoPoint2D p2d)
        {
            double d = PositionOf(p2d);
            ustart += d * udiff;
        }

        #endregion
    }
}
