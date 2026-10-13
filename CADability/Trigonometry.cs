#region MathNet Numerics, Copyright ©2004 Christoph Ruegg 

// MathNet Numerics, part of MathNet
//
// Copyright (c) 2004,	Christoph Ruegg, http://www.cdrnet.net
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published 
// by the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public 
// License along with this program; if not, write to the Free Software
// Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.

#endregion

using System;

namespace MathNet.Numerics
{
    /// <summary>
    /// Static DoublePrecision Trigonometry Helper Class
    /// </summary>

    public sealed class Trig
    {
        private Trig() { }

        #region Angle Conversion
        /// <summary>
        /// Converts a degree (360-periodic) angle to a radiant (2*Pi-periodic) angle. 
        /// </summary>
        public static double DegreeToRadiant(double degree)
        {
            return degree / 180 * Math.PI;
        }
        /// <summary>
        /// Converts a radiant (2*Pi-periodic) angle to a degree (360-periodic) angle. 
        /// </summary>
        public static double RadiantToDegree(double radiant)
        {
            return radiant / Math.PI * 180;
        }
        /// <summary>
        /// Converts a newgrad (400-periodic) angle to a radiant (2*Pi-periodic) angle. 
        /// </summary>
        public static double NewgradToRadiant(double newgrad)
        {
            return newgrad / 200 * Math.PI;
        }
        /// <summary>
        /// Converts a radiant (2*Pi-periodic) angle to a newgrad (400-periodic) angle. 
        /// </summary>
        public static double RadiantToNewgrad(double radiant)
        {
            return radiant / Math.PI * 200;
        }
        /// <summary>
        /// Converts a degree (360-periodic) angle to a newgrad (400-periodic) angle. 
        /// </summary>
        public static double DegreeToNewgrad(double degree)
        {
            return degree / 9 * 10;
        }
        /// <summary>
        /// Converts a newgrad (400-periodic) angle to a degree (360-periodic) angle. 
        /// </summary>
        public static double NewgradToDegree(double newgrad)
        {
            return newgrad / 10 * 9;
        }
        #endregion

        #region Trigonometric Functions
        /// <summary>Trigonometric Sine (Sinus) of an angle in radians</summary>
        public static double Sin(double angleRadians)
        {
            return Math.Sin(angleRadians);
        }
        /// <summary>Trigonometric Cosine (Cosinus) of an angle in radians</summary>
        public static double Cos(double angleRadians)
        {
            return Math.Cos(angleRadians);
        }
        /// <summary>Trigonometric Tangent (Tangens) of an angle in radians</summary>
        public static double Tan(double angleRadians)
        {
            return Math.Tan(angleRadians);
        }
        /// <summary>Trigonometric Cotangent (Cotangens) of an angle in radians</summary>
        public static double Cot(double angleRadians)
        {
            return 1 / Math.Tan(angleRadians);
        }
        /// <summary>Trigonometric Secant (Sekans) of an angle in radians</summary>
        public static double Sec(double angleRadians)
        {
            return 1 / Math.Cos(angleRadians);
        }
        /// <summary>Trigonometric Cosecant (Cosekans) of an angle in radians</summary>
        public static double Csc(double angleRadians)
        {
            return 1 / Math.Sin(angleRadians);
        }
        #endregion

        #region Trigonometric Arcus Functions
        /// <summary>Trigonometric Arcus Sine (Arkussinus) in radians</summary>
        public static double Asin(double length)
        {
            return Math.Asin(length);
        }
        /// <summary>Trigonometric Arcus Cosine (Arkuscosinus) in radians</summary>
        public static double Acos(double length)
        {
            return Math.Acos(length);
        }
        /// <summary>Trigonometric Arcus Tangent (Arkustangens) in radians</summary>
        public static double Atan(double length)
        {
            return Math.Atan(length);
        }
        /// <summary>The principal argument (in radians) of the complex number x+I*y</summary>
        /// <param name="nominator">y</param>
        /// <param name="denominator">x</param>
        public static double ArcusTangentFromRational(double nominator, double denominator)
        {
            return Math.Atan2(nominator, denominator);
        }
        /// <summary>Trigonometric Arcus Cotangent (Arkuscotangens) in radians</summary>
        public static double Acot(double length)
        {
            return Math.Atan(1 / length);
        }
        /// <summary>Trigonometric Arcus Secant (Arkussekans) in radians</summary>
        public static double Asec(double length)
        {
            return Math.Acos(1 / length);
        }
        /// <summary>Trigonometric Arcus Cosecant (Arkuscosekans) in radians</summary>
        public static double Acsc(double length)
        {
            return Math.Asin(1 / length);
        }
        #endregion

        #region Hyperbolic Functions
        /// <summary>Trigonometric Hyperbolic Sine (Sinus hyperbolicus)</summary>
        public static double Sinh(double x)
        {
            //NOT SUPPORTED BY COMPACT FRAMEWORK!!???
            //return Math.Sinh(x)
            return (Math.Exp(x) - Math.Exp(-x)) / 2;
        }
        /// <summary>Trigonometric Hyperbolic Cosine (Cosinus hyperbolicus)</summary>
        public static double Cosh(double x)
        {
            //NOT SUPPORTED BY COMPACT FRAMEWORK!!???
            //return Math.Cosh(x);
            return (Math.Exp(x) + Math.Exp(-x)) / 2;
        }
        /// <summary>Trigonometric Hyperbolic Tangent (Tangens hyperbolicus)</summary>
        public static double Tanh(double x)
        {
            //NOT SUPPORTED BY COMPACT FRAMEWORK!!???
            //return Math.Tanh(x);
            double e1 = Math.Exp(x);
            double e2 = Math.Exp(-x);
            return (e1 - e2) / (e1 + e2);
        }
        /// <summary>Trigonometric Hyperbolic Cotangent (Cotangens hyperbolicus)</summary>
        public static double Coth(double x)
        {
            //NOT SUPPORTED BY COMPACT FRAMEWORK!!???
            //return 1/Math.Tanh(x);
            double e1 = Math.Exp(x);
            double e2 = Math.Exp(-x);
            return (e1 + e2) / (e1 - e2);
        }
        /// <summary>Trigonometric Hyperbolic Secant (Sekans hyperbolicus)</summary>
        public static double Sech(double x)
        {
            return 1 / Cosh(x);
        }
        /// <summary>Trigonometric Hyperbolic Cosecant (Cosekans hyperbolicus)</summary>
        public static double Csch(double x)
        {
            return 1 / Sinh(x);
        }
        #endregion

        #region Hyperbolic Area Functions
        /// <summary>Trigonometric Hyperbolic Area Sine (Areasinus hyperbolicus)</summary>
        public static double Asinh(double x)
        {
            return Math.Log(x + Math.Sqrt(x * x + 1), Math.E);
        }
        /// <summary>Trigonometric Hyperbolic Area Cosine (Areacosinus hyperbolicus)</summary>
        public static double Acosh(double x)
        {
            return Math.Log(x + Math.Sqrt(x - 1) * Math.Sqrt(x + 1), Math.E);
        }
        /// <summary>Trigonometric Hyperbolic Area Tangent (Areatangens hyperbolicus)</summary>
        public static double Atanh(double x)
        {
            return Math.Log((1 + x) / (1 - x), Math.E) / 2;
        }
        /// <summary>Trigonometric Hyperbolic Area Cotangent (Areacotangens hyperbolicus)</summary>
        public static double Acoth(double x)
        {
            return Math.Log((x + 1) / (x - 1), Math.E) / 2;
        }
        /// <summary>Trigonometric Hyperbolic Area Secant (Areasekans hyperbolicus)</summary>
        public static double Asech(double x)
        {
            return Acosh(1 / x);
        }
        /// <summary>Trigonometric Hyperbolic Area Cosecant (Areacosekans hyperbolicus)</summary>
        public static double Acsch(double x)
        {
            return Asinh(1 / x);
        }
        #endregion

        #region Special Functions
        /// <summary> Returns <code>sqrt(a<sup>2</sup> + b<sup>2</sup>)</code> 
        /// without underflow/overlow.</summary>
        public static double Hypot(double a, double b)
        {
            double r;

            if (Math.Abs(a) > Math.Abs(b))
            {
                r = b / a;
                r = Math.Abs(a) * Math.Sqrt(1 + r * r);
            }
            else if (b != 0)
            {
                r = a / b;
                r = Math.Abs(b) * Math.Sqrt(1 + r * r);
            }
            else r = 0.0;

            return r;
        }

        /// <summary>
        /// Returns the natural logarithm of Gamma for a real value > 0
        /// </summary>
        /// <param name="xx">A real value for Gamma calculation</param>
        /// <returns>A value ln|Gamma(xx))| for xx > 0</returns>
        public static double GammaLn(double xx)
        {
            double x, y, ser, temp;
            double[] coefficient = new double[]{76.18009172947146,-86.50535032941677,
                                                   24.01409824083091,-1.231739572450155,0.1208650973866179e-2,-0.5395239384953e-5};
            int j;
            y = x = xx;
            temp = x + 5.5;
            temp -= ((x + 0.5) * System.Math.Log(temp));
            ser = 1.000000000190015;
            for (j = 0; j <= 5; j++)
                ser += (coefficient[j] / ++y);
            return -temp + System.Math.Log(2.50662827463100005 * ser / x);
        }
        /// <summary>
        /// Returns a factorial of an integer number (n!)
        /// </summary>
        /// <param name="n">The value to be factorialized</param>
        /// <returns>The double precision result</returns>
        public static double Factorial(int n)
        {
            int ntop = 4;
            double[] a = new double[32];
            a[0] = 1.0; a[1] = 1.0; a[2] = 2.0; a[3] = 6.0; a[4] = 24.0;
            int j;
            if (n < 0)
                throw new ArithmeticException("Factorial: Negative factorial request.");
            if (n > 32)
                return System.Math.Exp(GammaLn(n + 1.0));
            while (ntop < n)
            {
                j = ntop++;
                a[ntop] = a[j] * ntop;
            }
            return a[n];
        }
        /// <summary>
        /// Returns a binomial coefficient of n and k as a double precision number
        /// </summary>
        /// <param name="n"></param>
        /// <param name="k"></param>
        /// <returns></returns>
        public static double BinomialCoefficient(int n, int k)
        {
            return System.Math.Floor(0.5 + System.Math.Exp(FactorialLn(n) - FactorialLn(k) - FactorialLn(n - k)));
        }
        /// <summary>
        /// Returns the natural logarithm of the factorial of <paramref name="n"/>, ln(n!), computed via the logarithm of the
        /// gamma function (<see cref="GammaLn"/>). Throws an <see cref="ArithmeticException"/> for a negative argument.
        /// </summary>
        /// <param name="n">A non-negative integer</param>
        /// <returns>ln(n!)</returns>
        public static double FactorialLn(int n)
        {
            double[] a = new double[101];
            if (n < 0)
                throw new ArithmeticException("Factorial negative argument");
            if (n <= 1)
                return 0.0d;
            if (n <= 100)
            {
                a[n] = GammaLn(n + 1.0d);
                return (a[n] == 0.0d) ? a[n] : (a[n]);
            }
            else
            {
                return GammaLn(n + 1.0d);
            }
        }
        /// <summary>
        /// Returns the beta function B(z, w) = Γ(z)·Γ(w) / Γ(z + w), computed via <see cref="GammaLn"/>.
        /// </summary>
        /// <param name="z">First argument, must be positive</param>
        /// <param name="w">Second argument, must be positive</param>
        /// <returns>The value of the beta function</returns>
        public static double Beta(double z, double w)
        {
            return System.Math.Exp(GammaLn(z) + GammaLn(w) - GammaLn(z + w));
        }
        #endregion
    }
}
