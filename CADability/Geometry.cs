using CADability.Curve2D;
using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Collections.Generic;
using Wintellect.PowerCollections;

namespace CADability
{
    /// <summary>
    /// The class Geometry only contains static methods to solve some basic
    /// geometric tasks.
    /// </summary>

    public class Geometry
    {
        /// <summary>
        /// ApplicationException derived exception class for methods in
        /// class <see cref="Geometry"/>.
        /// </summary>
        public class GeometryException : ApplicationException
        {
            /// <summary>
            /// Constrcts a GeometryException (see ApplicationException)
            /// </summary>
            /// <param name="Message">Error message</param>
            public GeometryException(string Message)
                : base(Message)
            {
            }
        }

        public Geometry()
        {
        }


        /// <summary>
        /// Liefert das Quadrat.
        /// </summary>
        /// <param name=a>Zu quadrierender Wert</param>
        internal static double sqr(double a) { return a * a; }

        /// <summary>
        /// "sign liefert den Betrag von a mit dem Vorzeichen von b"
        /// </summary>
        /// <param name=a>nur der Betrag wird</param>
        /// <param name=b>nur das Vorzeichen wird ausgewertet</param>
        internal static double sign(double a, double b)
        {
            if (b < 0)
                return -Math.Abs(a);
            else return Math.Abs(a);
        }

        /// <summary>
        /// Summary of quadgl.
        /// </summary>
        /// <param name=a></param>
        /// <param name=b></param>
        /// <param name=c></param>
        /// <param name=x></param>
        /// <param name=y></param>
        internal static int quadgl(double a, double b, double c, out double x, out double y)
        {
            double d;
            if (Math.Abs(a) < 1.0e-10)
            {
                if (Math.Abs(b) > 1.0e-10)
                {
                    y = x = (-c) / b;
                    return 1;
                }
                else
                {
                    y = x = 0.0; // muss zugewiesen werden
                    return 0;
                }
            }
            else
            {
                d = sqr(b) - 4 * a * c;
                if (d < 0)
                {
                    y = x = 0.0; // muss zugewiesen werden
                    return 0;
                }
                else
                {
                    if (c == 0)
                    {
                        x = (-b + Math.Abs(b)) / (2 * a);
                        y = (-b - Math.Abs(b)) / (2 * a);
                    }
                    else
                    {
                        x = (-b + Math.Sqrt(d)) / (2 * a);
                        y = (-b - Math.Sqrt(d)) / (2 * a);
                    };
                    if (x == y) return 1;
                    return 2;
                }
            }
        }

        /// <summary>
        /// Summary of ragbq.
        /// </summary>
        /// <param name=a></param>
        /// <param name=b></param>
        /// <param name=c></param>
        /// <param name=x></param>
        internal static int ragbq(double a, double b, double c, double[] x)
        // reelle loesungen der biquadratischen gleichung  a*x**4 + b*x**2 + c = 0. 
        {
            if (x == null) x = new double[4];
            double[] xq = new double[2];
            int num, num1;

            num1 = 0;
            num = quadgl(a, b, c, out xq[0], out xq[1]);

            if (num > 0)
            {
                for (int i = 0; i < num; i++)
                {
                    if ((Math.Abs(xq[i])) < 1e-15) xq[i] = 0;
                    if (xq[i] >= 0)
                    {
                        x[num1] = Math.Sqrt(xq[i]);
                        if (x[num1] != 0)
                        {
                            num1++; x[num1] = -x[num1 - 1];
                        }
                        num1++;
                    }
                }
            }
            return num1;

        }

        /// <summary>
        /// ragle3 berechnet die REELLEN Loesungen einer Gleichung 3. Grades
        /// </summary>
        /// <param name=a></param>
        /// <param name=b></param>
        /// <param name=c></param>
        /// <param name=d></param>
        /// <param name=x></param>
        internal static int ragle3(double a, double b, double c, double d, double[] x)
        {
            if (x == null) x = new double[4];
            double ba, ca, da, p, q, dis3, b3a, wud, u, v, r, phi, cp3, sp3, wu3, xk, qq;
            int nl, i, k;

            if (a == 0) nl = quadgl(b, c, d, out x[0], out x[1]);
            else  // A<>0 
            {
                ba = b / a;
                ca = c / a;
                da = d / a;
                q = ba * ba * ba / 27 - ba * ca / 6 + da / 2;
                p = (3 * ca - ba * ba) / 9;
                dis3 = q * q + p * p * p;
                // hier stand mal ein Test ob DIS3 fast 0 ist 
                // wenn der folgende Test zutrifft und dis3 auf 0.0 gesetzt wird, wird das Ergebnis verfälscht!
                // zu überprüfen mit zwei sehr dünnen Ellipsen, die sich gerade so eben schneiden, QuarticRoots braucht die exakte Lösung
                // if (Math.Abs(dis3) < 1E-15) dis3 = 0;
                b3a = ba / 3;
                if (dis3 >= 0)
                {
                    wud = 0;
                    if (dis3 > 0)
                    {
                        nl = 1;
                        wud = Math.Sqrt(dis3);
                        u = sign(Math.Exp(Math.Log(Math.Abs(-q + wud)) / 3), -q + wud);
                        v = sign(Math.Exp(Math.Log(Math.Abs(-q - wud)) / 3), -q - wud);
                        x[0] = u + v - b3a;
                    }
                    else  // DIS3=0 
                    {
                        qq = q * q;
                        if (qq == 0)
                        {
                            nl = 1; x[0] = -b3a;
                        }
                        else
                        {
                            nl = 2;
                            u = sign(Math.Exp(Math.Log(Math.Abs(-q)) / 3), -q);
                            x[0] = 2 * u - b3a; x[1] = -u - b3a;
                        }
                    }

                }
                else  // DIS3<0  -> P<0 
                {
                    nl = 3;
                    wud = Math.Sqrt(-dis3); r = Math.Sqrt(-p);
                    phi = Math.Atan2(wud, -q);
                    cp3 = Math.Cos(phi / 3); sp3 = Math.Sin(phi / 3);
                    wu3 = Math.Sqrt(3.0);
                    x[0] = 2 * r * cp3 - b3a;
                    x[1] = -r * (cp3 + wu3 * sp3) - b3a;
                    x[2] = -r * (cp3 - wu3 * sp3) - b3a;
                }
                // umordnen der x[i] nach groesse
                if (nl > 1)
                {

                    for (i = 0; i < (nl - 1); i++)
                    {
                        for (k = i + 1; k < nl; k++)
                        {
                            if (x[k] < x[i])
                            {
                                xk = x[i]; x[i] = x[k]; x[k] = xk;
                            }
                        }
                    }
                }
            }  // a <> 0 
            CubicNewtonApproximation(a, b, c, d, x, nl);
            //double error = 0.0;
            //for (int j = 0; j < nl; j++)
            //{
            //    double xx = x[j];
            //    error += Math.Abs(a * xx * xx * xx + b * xx * xx + c * xx + d);
            //}
            return nl;
        }  // ragle3 

        internal static int ragle3fast(double a, double b, double c, double d, double[] x)
        {   // ohne Newton Nachbrenner
            if (x == null) x = new double[4];
            double ba, ca, da, p, q, dis3, b3a, wud, u, v, r, phi, cp3, sp3, wu3, xk, qq;
            int nl, i, k;

            if (a == 0) nl = quadgl(b, c, d, out x[0], out x[1]);
            else  // A<>0 
            {
                ba = b / a;
                ca = c / a;
                da = d / a;
                q = ba * ba * ba / 27 - ba * ca / 6 + da / 2;
                p = (3 * ca - ba * ba) / 9;
                dis3 = q * q + p * p * p;
                // hier stand mal ein Test ob DIS3 fast 0 ist 
                // wenn der folgende Test zutrifft und dis3 auf 0.0 gesetzt wird, wird das Ergebnis verfälscht!
                // zu überprüfen mit zwei sehr dünnen Ellipsen, die sich gerade so eben schneiden, QuarticRoots braucht die exakte Lösung
                // if (Math.Abs(dis3) < 1E-15) dis3 = 0;
                b3a = ba / 3;
                if (dis3 >= 0)
                {
                    wud = 0;
                    if (dis3 > 0)
                    {
                        nl = 1;
                        wud = Math.Sqrt(dis3);
                        u = sign(Math.Exp(Math.Log(Math.Abs(-q + wud)) / 3), -q + wud);
                        v = sign(Math.Exp(Math.Log(Math.Abs(-q - wud)) / 3), -q - wud);
                        x[0] = u + v - b3a;
                    }
                    else  // DIS3=0 
                    {
                        qq = q * q;
                        if (qq == 0)
                        {
                            nl = 1; x[0] = -b3a;
                        }
                        else
                        {
                            nl = 2;
                            u = sign(Math.Exp(Math.Log(Math.Abs(-q)) / 3), -q);
                            x[0] = 2 * u - b3a; x[1] = -u - b3a;
                        }
                    }

                }
                else  // DIS3<0  -> P<0 
                {
                    nl = 3;
                    wud = Math.Sqrt(-dis3); r = Math.Sqrt(-p);
                    phi = Math.Atan2(wud, -q);
                    cp3 = Math.Cos(phi / 3); sp3 = Math.Sin(phi / 3);
                    wu3 = Math.Sqrt(3.0);
                    x[0] = 2 * r * cp3 - b3a;
                    x[1] = -r * (cp3 + wu3 * sp3) - b3a;
                    x[2] = -r * (cp3 - wu3 * sp3) - b3a;
                }
                // umordnen der x[i] nach groesse
                if (nl > 1)
                {

                    for (i = 0; i < (nl - 1); i++)
                    {
                        for (k = i + 1; k < nl; k++)
                        {
                            if (x[k] < x[i])
                            {
                                xk = x[i]; x[i] = x[k]; x[k] = xk;
                            }
                        }
                    }
                }
            }  // a <> 0 
            return nl;
        }  // ragle3 

        /// <summary>
        /// ragle4 berechnet die reellen loesungen einer gleichung 4. Grades, 
        /// maximal 4 Lösungen in x.
        /// </summary>
        /// <param name=a></param>
        /// <param name=b></param>
        /// <param name=c></param>
        /// <param name=d></param>
        /// <param name=e></param>
        /// <param name=x></param>
        internal static int ragle4(double a, double b, double c, double d, double e, double[] x, bool newton = false)
        {
            if (x == null) x = new double[4]; // stimmt das mit 4 Lösungen ?
            double ba, ca, da, ea, p, q, r, a3, b3, c3, d3, gp, gr2, gr, gq, a2, b2, c2, b22, c22, xk;
            int k, n2, n22, n3, nl;
            double[] pp = new double[4];

            nl = 0;
            // die Abschätzung für a viel kleiner als die anderen Koeffizienten erfolgt
            // wg. großer Ungenauigkeiten bei der Berechung einer Linie als Tangente
            // an zwei Kreise, die sich berühren aber verschiedenen Radius haben
            if (Math.Abs(a) < (Math.Abs(b) + Math.Abs(c) + Math.Abs(d) + Math.Abs(e)) * 1e-11)
                nl = ragle3(b, c, d, e, x);
            else // a<>0 
            {
                ba = b / a; ca = c / a; da = d / a; ea = e / a;
                p = ca - 3 * ba * ba / 8;
                q = da - ba * ca / 2 + ba * ba * ba / 8;
                r = ea - ba * da / 4 + ba * ba * ca / 16 - 3 * ba * ba * ba * ba / 256;
                if (q != 0)
                {
                    a3 = 8; b3 = -4 * p; c3 = -8 * r; d3 = 4 * p * r - q * q;
                    n3 = ragle3(a3, b3, c3, d3, pp);
                    gp = pp[0];
                    if (n3 == 0) nl = 0;
                    else   // n3>0 
                    {
                        gr2 = gp * gp - r;
                        if (Math.Abs(gr2) < 1e-13) nl = ragbq(1, p, r, x); // -> q=0,biquadr.
                        if (gr2 < 0) nl = 0;
                        if (gr2 > 0)
                        {
                            gr = Math.Sqrt(gr2);
                            gq = -q / (2 * gr);
                            a2 = 1; b2 = -gq; c2 = gp - gr;
                            n2 = quadgl(a2, b2, c2, out x[0], out x[1]);
                            b22 = gq; c22 = gp + gr;
                            n22 = quadgl(a2, b22, c22, out x[n2], out x[n2 + 1]);
                            nl = n2 + n22;
                        } // gr2>0
                    } // n3>0 
                }
                else  // q=0 
                    nl = ragbq(1, p, r, x);
                // umordnen der x[i] nach groesse
                if (newton)
                {
                    // Bestimmung der Minima und Maxima, dazwischen muss jeweils ein Schnittpunkz liegen
                    double[] minmax = new double[3];
                    int nmm = ragle3(4 * a, 3 * b, 2 * c, d, minmax); // die Minima/Maxima, dazwischen kann jeweils nur eine Lösung sein
                    Array.Sort(minmax, 0, nmm); // aufsteigend sortieren
                    List<double> segments = new List<double>(3);
                    double lasty0 = 0.0;
                    for (int i = 0; i < nmm; i++)
                    {
                        double y0 = ((((a * minmax[i] + b) * minmax[i]) + c) * minmax[i] + d) * minmax[i] + e;
                        double y1 = ((4 * a * minmax[i] + 3 * b) * minmax[i] + 2 * c) * minmax[i] + d;
                        double y2 = (4 * 3 * a * minmax[i] + 3 * 2 * b) * minmax[i] + 2 * c; // 2. Ableitung: Minimum oder Maximum
                        if (i == 0 || i == nmm - 1)
                        {   // die äußeren Minima und maxima gelten so:
                            // wenn y2>0, also Minimum, y0<0 und umgekehrt
                            if (y0 * y2 <= 0) segments.Add(minmax[i]);
                        }
                        else
                        {
                            if (lasty0 * y0 <= 0) segments.Add(minmax[i]);
                        }
                        lasty0 = y0;
                    }
                    QuarticNewtonApproximation(a, b, c, d, e, x, nl);
                    Array.Sort(x, 0, nl);
                    bool ok = segments.Count == nl - 1;
                    for (int i = 0; i < nl; i++)
                    {
                        if (!ok) break;
                        if (i == 0)
                        {
                            if (x[i] > segments[i]) ok = false;
                        }
                        else if (i == nl - 1)
                        {
                            if (x[i] < segments[i - 1]) ok = false;
                        }
                        else if (x[i] < segments[i - 1] || x[i] > segments[i]) ok = false;
                    }
                    if (!ok && segments.Count > 0)
                    {   // wir haben Grenzen, zwischen denen müssen Nullstellen liegen
                        // die werden jetzt mit Bisection oder Newton gefunden
                        // zuerst am Anfang und am Ende noch eine Segmentgrenze einführen, wo ein Vorzeichenwechsle stattfindet
                        double dx = 1;
                        if (segments.Count > 1) dx = segments[segments.Count - 1] - segments[0];
                        double xl = segments[0] - dx;
                        double y0 = ((((a * segments[0] + b) * segments[0]) + c) * segments[0] + d) * segments[0] + e;
                        double yl = ((((a * xl + b) * xl) + c) * xl + d) * xl + e;
                        while (y0 * yl > 0) // soweit nach links, bis Vorzeichenwechsel
                        {
                            dx *= 2;
                            xl -= dx;
                            yl = ((((a * xl + b) * xl) + c) * xl + d) * xl + e;
                            if (double.IsInfinity(yl)) break;
                        }
                        if (!double.IsInfinity(yl)) segments.Insert(0, xl);
                        dx = 1;
                        if (segments.Count > 1) dx = segments[segments.Count - 1] - segments[0];
                        double xr = segments[segments.Count - 1] + dx;
                        y0 = ((((a * segments[segments.Count - 1] + b) * segments[segments.Count - 1]) + c) * segments[segments.Count - 1] + d) * segments[segments.Count - 1] + e;
                        double yr = ((((a * xr + b) * xr) + c) * xr + d) * xr + e;
                        while (y0 * yr > 0) // soweit nach links, bis Vorzeichenwechsel
                        {
                            dx *= 2;
                            xr += dx;
                            yr = ((((a * xr + b) * xr) + c) * xr + d) * xr + e;
                            if (double.IsInfinity(yr)) break;
                        }
                        if (!double.IsInfinity(yr)) segments.Add(xr);
                        FindSegmentsSolutionQuad(a, b, c, d, e, segments, x);
                        nl = segments.Count - 1;
                    }
                }
                if (nl > 0)
                {
                    for (int i = 0; i < nl; i++) x[i] = x[i] - ba / 4; // for i=1 to nl do  x[i]= x[i] - ba/4;
                    if (nl > 1)
                    {
                        for (int i = 0; i < (nl - 1); i++)  //for i=1 to nl-1  do
                        {
                            for (k = i + 1; k < nl; k++)  //for k=i+1 to nl do
                            {
                                if (x[k] < x[i])
                                {
                                    xk = x[i]; x[i] = x[k]; x[k] = xk;
                                }
                            }
                        }
                    }
                }
            } // a<>0 
            if (!newton)
            {
                int n = 0;
                for (int i = nl - 1; i >= 0; --i)
                {
                    double x1 = x[i];
                    double x2 = x[i] * x1;
                    double x3 = x[i] * x2;
                    double x4 = x[i] * x3;
                    if (Math.Abs(a * x4 + b * x3 + c * x2 + d * x1 + e) > Precision.eps)
                    {   // Aussortieren der schlechten Lösungen
                        x[i] = x[3 - n];
                        x[3 - n] = x1;
                        n++;
                    }
                }
                nl -= n;
            }
            return nl;
        }  // ragle4 
        /// <summary>
        /// Lösung für eine Gleichung 4. Grades, wobei jeweils zwischen gegebenen Segmentgrenzen eine Lösung sein muss
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <param name="e"></param>
        /// <param name="segments"></param>
        /// <param name="x"></param>
        private static void FindSegmentsSolutionQuad(double a, double b, double c, double d, double e, List<double> segments, double[] xx)
        {
            for (int i = 0; i < segments.Count - 1; i++)
            {
                double xmin = segments[i];
                double xmax = segments[i + 1];

                do
                {
                    double x = (xmin + xmax) / 2.0;
                    double y = x * (x * (x * (a * x + b) + c) + d) + e;
                    if (y == 0)
                    {
                        xx[i] = x;
                        break; // besser gets nicht
                    }
                    double t = x * (x * (4 * a * x + 3 * b) + 2 * c) + d;
                    double ymax = Math.Abs(y);
                    while ((Math.Abs(t) > 0)) // konvergiert sicher, da ymax jedesmal kleiner wird
                    {
                        // y-y0 = (x-x0)*t | y==0
                        double xn = -y / t + x;
                        if (xn == x) break; // die Größenordnung für die Änderung (-y / t) ist so klein, dass sich nichts mehr ändert
                        x = xn;
                        if (x < xmin || x > xmax) break;
                        y = x * (x * (x * (a * x + b) + c) + d) + e; // für den nächsten Durchlauf
                        if (y == 0 || Math.Abs(y) >= ymax) break;
                        ymax = Math.Abs(y);
                        t = x * (x * (4 * a * x + 3 * b) + 2 * c) + d; // für den nächsten Durchlauf
                    }
                    if (x < xmin || x > xmax)
                    {
                        // Newton fehlgeschlagen, Intervall halbieren
                        double yl = (((a * xmin + b) * xmin + c) * xmin + d) * xmin + e;
                        double yr = (((a * xmax + b) * xmax + c) * xmax + d) * xmax + e;
                        x = (xmin + xmax) / 2.0;
                        double ym = (((a * x + b) * x + c) * x + d) * x + e;
                        if (ym == 0.0)
                        {
                            xx[i] = x;
                            break;
                        }
                        else
                        {
                            if (yl * ym < 0) xmax = x;
                            else xmin = x;
                        }
                    }
                    else
                    {
                        xx[i] = x;
                        break;
                    }
                } while ((xmax - xmin) / Math.Max(Math.Abs(xmin), Math.Abs(xmax)) > 1e-10);
            }
        }

        internal static double SimpleNewtonApproximation(FunctionAndDerivation f, ref int maxIterations, double lowerBound, double upperBound, double functionTolerance, double stepTolerance)
        {
            double l = lowerBound;
            double u = upperBound;
            double x = (l + u) / 2.0;
            f(l, out double fl, out double dfl);
            f(u, out double fu, out double dfu);
            if (Math.Sign(fl) == Math.Sign(fu)) return double.MaxValue; // no result, the sign of the function on the lower bound and upper bound must be different
            double diff = double.MaxValue;
            int cnt = 0;
            int bisectCount = 0;
            double fx = 0.0;
            while (diff > stepTolerance && cnt < maxIterations)
            {
                f(x, out fx, out double dfx);
                double xn = (dfx == 0.0) ? x : x - fx / dfx;
                double ndiff = Math.Abs(x - xn);
                x = xn;
                if (ndiff >= diff || x < l || x > u || dfx == 0.0) // outside the bounds or no convergence
                {   // fall-back to bisection algorithm
                    // bisect the interval and use the part where the function value has different signs
                    ++bisectCount;
                    double m = (l + u) / 2.0;
                    f(m, out double fm, out double dfm);
                    if (fm == 0.0)
                    {
                        x = m; // this is the solution!
                        fx = fm;
                        break;
                    }
                    if ((u - l) / 2.0 < stepTolerance)
                    {   // result found by bisection
                        x = (u - l) / 2.0;
                        f(x, out fx, out dfx);
                        diff = (u - l) / 2.0; // which is less than stepTolerance
                        break; // done
                    }
                    else if (Math.Sign(fu) != Math.Sign(fm))
                    {
                        l = m;
                        fl = fm;
                    }
                    else // if (Math.Sign(fl) != Math.Sign(fm)) this is always the case
                    {
                        u = m;
                        fu = fm;
                    }
                    x = m;
                    fx = fm;
                    cnt = 0; // start new counting of iteration steps
                    diff = double.MaxValue;
                    continue;
                }
                diff = ndiff;
                if (Math.Abs(fx) < functionTolerance) break;
                ++cnt;
            }


            if (diff <= stepTolerance || Math.Abs(fx) < functionTolerance)
            {
                maxIterations = cnt + bisectCount;
                return x;
            }
            else
            {
                maxIterations = cnt + bisectCount;
                return double.MaxValue;
            }
        }
        internal static bool SimpleNewtonApproximation(FunctionAndDerivation f, ref double parameter, ref int maxIterations, double lowerBound, double upperBound, double functionTolerance, double stepTolerance)
        {
            double x = parameter;
            double diff = double.MaxValue;
            int cnt = 0;
            double fx = 0.0;
            while (diff > stepTolerance && cnt < maxIterations)
            {
                f(x, out fx, out double dfx);
                if (dfx != 0.0)
                {
                    double xn = x - fx / dfx;
                    double ndiff = Math.Abs(x - xn);
                    if (ndiff >= diff) break; // no convergence
                    diff = ndiff;
                    x = xn;
                    if (x < lowerBound || x > upperBound || Math.Abs(fx) < functionTolerance) break;
                }
                else break;
                ++cnt;
            }
            if (diff <= stepTolerance || Math.Abs(fx) < functionTolerance)
            {
                parameter = x;
                maxIterations = cnt;
                return true;
            }
            else
            {
                parameter = x;
                maxIterations = cnt;
                return false;
            }
        }
        internal static double SimpleNewtonApproximation(SimpleFunction f, SimpleFunction df, double start)
        {
            double x = start;
            double diff = double.MaxValue;
            double prec = Math.Max(Math.Abs(start) * 1e-6, 1e-6);
            int cnt = 0;
            while (diff > prec && cnt < 4)
            {
                double dfx = df(x);
                if (dfx != 0.0)
                {
                    double xn = x - f(x) / df(x);
                    double ndiff = Math.Abs(x - xn);
                    if (ndiff > diff) break;
                    diff = ndiff;
                    x = xn;
                }
                else break;
                ++cnt;
            }
            if (diff <= prec) return x;
            else return double.MaxValue;
        }
        internal static void CubicNewtonApproximation(double a, double b, double c, double d, double[] xx, int length)
        {
#if DEBUG
            double xmin = xx[0], xmax = xx[0];
            for (int i = 1; i < length; ++i)
            {
                xmin = Math.Min(xmin, xx[i]);
                xmax = Math.Max(xmax, xx[i]);
            }
            double dx = xmax - xmin + 1;
            xmin -= dx;
            xmax += dx;
            dx = (xmax - xmin) / 100;
            GeoPoint2D[] pnts = new GeoPoint2D[100];
            for (int i = 0; i < 100; i++)
            {
                double x0 = xmin + i * dx;
                pnts[i] = new GeoPoint2D(x0, x0 * (x0 * (a * x0 + b) + c) + d);
            }
            Polyline2D pl2d = new Polyline2D(pnts);
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(pl2d);
#endif
            for (int i = 0; i < length; ++i)
            {
                double x = xx[i];
                double y = x * (x * (a * x + b) + c) + d;
                if (y == 0) continue; // besser gets nicht
                double t = x * (3 * a * x + 2 * b) + c;
                double ymax = Math.Abs(y);
                while ((Math.Abs(t) > 0)) // konvergiert sicher, da ymax jedesmal kleiner wird
                {
                    // y-y0 = (x-x0)*t | y==0
                    double xn = -y / t + x;
                    if (xn == x) break; // die Größenordnung für die Änderung (-y / t) ist so klein, dass sich nichts mehr ändert
                    y = xn * (xn * (a * xn + b) + c) + d; // für den nächsten Durchlauf
                    if (Math.Abs(y) >= ymax) break; // neue Lösung schlechter
                    x = xn;
                    if (y == 0) break;
                    ymax = Math.Abs(y);
                    t = x * (3 * a * x + 2 * b) + c; // für den nächsten Durchlauf
                }
                xx[i] = x;
            }
        }
        internal static void QuarticNewtonApproximation(double a, double b, double c, double d, double e, double[] xx, int length)
        {
#if DEBUG
            double xmin = xx[0], xmax = xx[0];
            for (int i = 1; i < length; ++i)
            {
                xmin = Math.Min(xmin, xx[i]);
                xmax = Math.Max(xmax, xx[i]);
            }
            double dx = xmax - xmin + 1;
            xmin -= dx;
            xmax += dx;
            dx = (xmax - xmin) / 100;
            GeoPoint2D[] pnts = new GeoPoint2D[100];
            for (int i = 0; i < 100; i++)
            {
                double x0 = xmin + i * dx;
                pnts[i] = new GeoPoint2D(x0, x0 * (x0 * (x0 * (a * x0 + b) + c) + d) + e);
            }
            Polyline2D pl2d = new Polyline2D(pnts);
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(pl2d);
#endif
            for (int i = 0; i < length; ++i)
            {
                double x = xx[i];
                double y = x * (x * (x * (a * x + b) + c) + d) + e;
                if (y == 0) continue; // besser gets nicht
                double t = x * (x * (4 * a * x + 3 * b) + 2 * c) + d;
                double ymax = Math.Abs(y);
                while ((Math.Abs(t) > 0)) // konvergiert sicher, da ymax jedesmal kleiner wird
                {
                    // y-y0 = (x-x0)*t | y==0
                    double xn = -y / t + x;
                    if (xn == x) break; // die Größenordnung für die Änderung (-y / t) ist so klein, dass sich nichts mehr ändert
                    y = xn * (xn * (xn * (a * xn + b) + c) + d) + e; // für den nächsten Durchlauf
                    if (Math.Abs(y) >= ymax) break; // neue Lösung schlechter
                    x = xn;
                    if (y == 0) break;
                    ymax = Math.Abs(y);
                    t = x * (x * (4 * a * x + 3 * b) + 2 * c) + d; // für den nächsten Durchlauf
                }
                xx[i] = x;
            }
            // war vorher:
            //for (int i = 0; i < length; ++i)
            //{
            //    double x = xx[i];
            //    double ymax = double.MaxValue;
            //    bool better = true;
            //    while (better)
            //    {
            //        double t = x * (x * (4 * a * x + 3 * b) + 2 * c) + d;
            //        if (Math.Abs(t) > 1e-13)
            //        {
            //            double y = x * (x * (x * (a * x + b) + c) + d) + e;
            //            if (y == 0.0) better = false; // exakte Lösung gefunden
            //            else if (Math.Abs(y) < ymax)
            //            {
            //                ymax = Math.Abs(y);
            //                // y-y0 = (x-x0)*t | y==0
            //                x = -y / t + x;
            //            }
            //            else
            //            {
            //                better = false;
            //            }
            //        }
            //        else better = false;
            //    }
            //    xx[i] = x;
            //}
        }
        internal static double[] QuarticRoots(double a, double b, double c, double d, double e)
        {   // nach geometric tools http://www.geometrictools.com/Documentation/LowDegreePolynomialRoots.pdf

            // hier könnte man noch einiges optimieren: Es müsste die genauset Nullstelle von ragle3 verwendet werden, nicht alle
            // zwei double arrays mit 4 Einträgen und Lösung und Fehler, Array.Copy um die richtige Anzahl der Lösungen
            // für das Ergebnis zu erzeugen

            // Maxima: wenn eine gute Nullstelle gefunden ist (x0) dann kann man durch (x-x0) teilen und die Nullstellen vom Rest ausrechnen
            // in Maxima eingegeben: divide(a*x^4+b*x^3+c*x^2+d*x+e, x-x0, x); stringout("test.tst",%);
            // das Ergebnis ist dann
            // (x-x0)*(a*x*x*x+(a*x0+b)*x*x + (a*x0*x0+b*x0+c)*x + (a*x0*x0*x0+b*x0*x0+c*x0+d))
            if (Math.Abs(a) > 1e-13)
            {
                b /= a;
                c /= a;
                d /= a;
                e /= a;
                double[] r = new double[3];
                int n = ragle3(1, -c, b * d - 4 * e, -b * b * e + 4 * c * e - d * d, r);
                double error = double.MaxValue;
                int numsolerror = 0; // Anzahl der Lösungen für Fehlerkorrektur
                double[] res = null;

                OrderedMultiDictionary<double, double> best = null;
                for (int i = 0; i < n; i++)
                {
                    double y = r[i];
                    double R = b * b / 4 - c + y;
                    if (R > 0.0)
                    {
                        R = Math.Sqrt(R);
                        double D = 3 * b * b / 4 - R * R - 2 * c + (4 * b * c - 8 * d - b * b * b) / (4 * R);
                        double E = 3 * b * b / 4 - R * R - 2 * c - (4 * b * c - 8 * d - b * b * b) / (4 * R);
                        if (D >= 0 && E >= 0)
                        {
                            D = Math.Sqrt(D);
                            E = Math.Sqrt(E);
                            if (D == E)
                            {
                                if (D > 0)
                                {
                                    res = new double[] { -b / 4 + R / 2 + D / 2, -b / 4 + R / 2 - D / 2 };
                                }
                                else // D==0
                                {
                                    res = new double[] { -b / 4 + R / 2 };
                                }
                            }
                            else
                            {
                                res = new double[] { -b / 4 + R / 2 + D / 2,
                                                        -b / 4 + R / 2 - D / 2,
                                                        -b / 4 + R / 2 + E / 2,
                                                        -b / 4 + R / 2 - E / 2 };
                            }
                        }
                        else if (D >= 0.0)
                        {
                            D = Math.Sqrt(D);
                            res = new double[] { -b / 4 + R / 2 + D / 2, -b / 4 + R / 2 - D / 2 };
                        }
                        else if (E >= 0.0)
                        {
                            E = Math.Sqrt(E);
                            res = new double[] { -b / 4 + R / 2 + E / 2, -b / 4 + R / 2 - E / 2 };
                        }
                    }
                    else if (Math.Abs(R) < 1e-13)
                    {
                        double s = y * y - 4 * e;
                        if (s >= 0.0)
                        {
                            double D = 3 * b * b / 4 - 2 * c + 2 * Math.Sqrt(s);
                            double E = 3 * b * b / 4 - 2 * c - 2 * Math.Sqrt(s);
                            if (D >= 0.0 && E >= 0.0)
                            {
                                D = Math.Sqrt(D);
                                E = Math.Sqrt(E);
                                if (D == E)
                                {
                                    if (D > 0)
                                    {
                                        res = new double[] { -b / 4 + D / 2, -b / 4 - D / 2 };
                                    }
                                    else
                                    {
                                        res = new double[] { -b / 4 };
                                    }
                                }
                                else
                                {
                                    res = new double[] { -b / 4 + D / 2,
                                                            -b / 4 - D / 2,
                                                            -b / 4 + E / 2,
                                                            -b / 4 - E / 2 };
                                }
                            }
                            else if (D >= 0.0)
                            {
                                D = Math.Sqrt(D);
                                res = new double[] { -b / 4 + D / 2, -b / 4 - D / 2 };
                            }
                            else if (E >= 0.0)
                            {
                                E = Math.Sqrt(E);
                                res = new double[] { -b / 4 + E / 2, -b / 4 - E / 2 };
                            }
                        }
                    }
                    if (res != null)
                    {   // welches ist die beste Lösung für die gefundenen Lösungen 3. Grades
                        QuarticNewtonApproximation(1, b, c, d, e, res, res.Length);
                        // das Problem mit der NewtonApproximation ist:
                        // sie kann Lösungen zusammenschrumpfen lassen, d.h. 4 schlechte auf 2 gute, aber wir brauchen 
                        // die 4 guten Lösungen
                        int numsol = 0;
                        for (int ii = 0; ii < res.Length; ii++)
                        {
                            for (int jj = ii + 1; jj < res.Length; jj++)
                            {
                                double sa = Math.Abs(res[ii]) + Math.Abs(res[jj]);
                                if (sa > 0)
                                {
                                    double ea = Math.Abs(res[ii] - res[jj]) / sa;
                                    if (ea < 1e-9) ++numsol; // zählt die gleichen Lösungen
                                }
                            }
                        }
                        numsol = res.Length - numsol;
                        OrderedMultiDictionary<double, double> tmp = new OrderedMultiDictionary<double, double>(true);
                        double er = 0;
                        for (int j = 0; j < res.Length; j++)
                        {
                            double x = res[j];
                            double ee = Math.Abs(x * x * x * x + b * x * x * x + c * x * x + d * x + e);
                            er += ee;
                            tmp.Add(ee, x);
                        }
                        if (er < error && numsol >= numsolerror)
                        {
                            error = er;
                            best = tmp;
                            numsolerror = numsol;
                        }
                    }
                }
                if (best != null)
                {
                    if (best.LastItem.Key > 1e-8) // Fehler nach Größe der Ableitung testen!
                    {   // Polynom durch die beste Nullstelle teilen, dann 3. Grad bestimmen
                        double x0 = best.FirstItem.Value;
                        // a*x*x*x+(a*x0+b)*x*x + (a*x0*x0+b*x0+c)*x + (a*x0*x0*x0+b*x0*x0+c*x0+d)
                        n = ragle3(1, x0 + b, x0 * x0 + b * x0 + c, x0 * x0 * x0 + b * x0 * x0 + c * x0 + d, r);
                        List<double> rr = new List<double>();
                        rr.Add(x0);
                        for (int i = 0; i < n; ++i)
                        {
                            rr.Add(r[i]);
                        }
                        return rr.ToArray();
                    }
                    return best.SortedValues.ToArray();
                }
                else return new double[] { };
            }
            else
            {
                double[] r = new double[3];
                int n = ragle3(b, c, d, e, r);
                double[] res = new double[n];
                for (int i = 0; i < n; i++)
                {
                    res[i] = r[i];
                }
                return res;
            }
        }
        internal static bool FastSolve22(double a00, double a01, double a10, double a11, double b0, double b1, out double x, out double y)
        {
            double det = a00 * a11 - a10 * a01;
            if (Math.Abs(det) > 1e-20)
            {
                x = (a11 * b0 - a01 * b1) / det;
                y = (a00 * b1 - a10 * b0) / det;
                return true;
            }
            x = y = 0.0;
            return false;
            // folgendes ist etwas schneller, dafür aber unsymmetrisch
            //if (Math.Abs(a10) > Math.Abs(a00))
            //{
            //    double d = a00 / a10;
            //    double nom = (a01 - d * a11);
            //    // if (Math.Abs(nom) < 1e-20) return false;
            //    y = (b0 - d * b1) / nom;
            //    x = (b1 - a11 * y) / a10;
            //}
            //else
            //{
            //    double d = a10 / a00;
            //    double nom = (a11 - d * a01);
            //    // if (Math.Abs(nom) < 1e-20) return false;
            //    y = (b1 - d * b0) / nom;
            //    x = (b1 - a01 * y) / a00;
            //}
            //return !double.IsInfinity(x) && !double.IsInfinity(y);
        }
        /// <summary>
        /// Liefert den Parameter des gegebenen Punktes auf der gegebenen Linie
        /// </summary>
        /// <param name="sp">Startpunkt der Linie</param>
        /// <param name="dir">Richtung der Linie</param>
        /// <param name="p">zu testender Punkt</param>
        /// <returns>der Parameter, z.B. 0.0==StartPunkt</returns>
        public static double LinePar(GeoPoint2D sp, GeoVector2D dir, GeoPoint2D p)
        {
            if (Math.Abs(dir.x) > Math.Abs(dir.y)) return (p.x - sp.x) / dir.x;
            else return (p.y - sp.y) / dir.y;
        }
        public static double LinePar(GeoPoint2D sp, GeoPoint2D ep, GeoPoint2D p)
        {
            double dx = ep.x - sp.x;
            double dy = ep.y - sp.y;
            if (dx == 0.0 && dy == 0.0) return 0.5;
            if (Math.Abs(dx) > Math.Abs(dy)) return (p.x - sp.x) / dx;
            else return (p.y - sp.y) / dy;
        }
        public static double LinePar(GeoPoint sp, GeoPoint ep, GeoPoint p)
        {
            double dx = ep.x - sp.x;
            double dy = ep.y - sp.y;
            double dz = ep.z - sp.z;
            return ((dx * p.x + dy * p.y + dz * p.z) - (dx * sp.x + dy * sp.y + dz * sp.z)) / (dx * dx + dy * dy + dz * dz);

        }
        public static double LinePar(GeoPoint sp, GeoVector dir, GeoPoint p)
        {
            return ((dir.x * p.x + dir.y * p.y + dir.z * p.z) - (dir.x * sp.x + dir.y * sp.y + dir.z * sp.z)) / (dir.x * dir.x + dir.y * dir.y + dir.z * dir.z);
        }
        public static GeoPoint LinePos(GeoPoint sp, GeoPoint ep, double pos)
        {
            return sp + pos * (ep - sp);
        }
        public static GeoPoint LinePos(GeoPoint sp, GeoVector dir, double pos)
        {
            return sp + pos * dir;
        }
        public static GeoPoint2D LinePos(GeoPoint2D sp, GeoPoint2D ep, double pos)
        {
            return sp + pos * (ep - sp);
        }
        public static GeoPoint LinePos(GeoPoint sp, GeoVector dir, GeoPoint toProject)
        {
            double par = LinePar(sp, dir, toProject);
            return sp + par * dir;
        }
        /// <summary>
        /// Find the shortest connection of the two lines
        /// </summary>
        /// <param name="p1">Startpoint of first line</param>
        /// <param name="d1">Direction of first line</param>
        /// <param name="p2">Startpoint of second line</param>
        /// <param name="d2">Direction of second line</param>
        /// <param name="pos1">Resulting position on fist line (p1+pos1*d1)</param>
        /// <param name="pos2">Resulting position on second line (p2+pos2*d2)</param>
        public static void ConnectLines(GeoPoint p1, GeoVector d1, GeoPoint p2, GeoVector d2, out double pos1, out double pos2)
        { // not tested yet
            Matrix m = DenseMatrix.OfArray(new double[,] { { d1 * d1, -d2 * d1 }, { d1 * d2, -d2 * d2 } });
            Vector b = new DenseVector(new double[] { (p2 - p1) * d1, (p2 - p1) * d2 });
            Vector x = (Vector)m.Solve(b);
            pos1 = x[0];
            pos2 = x[1];
        }
        /// <summary>
        /// Calculates the intersectionpoint of two lines in 2d. The intersection point may be in any
        /// extension of the two lines.
        /// </summary>
        /// <param name="StartPoint1">Startpoint of the first line</param>
        /// <param name="EndPoint1">Endpoint of the first line</param>
        /// <param name="StartPoint2">Startpoint of the second line</param>
        /// <param name="EndPoint2">Endpoint of the second line</param>
        /// <param name="IntersectionPoint">the intersection point</param>
        /// <returns>true, if there is an intersection point, otherwise false </returns>
        public static bool IntersectLL(GeoPoint2D StartPoint1, GeoPoint2D EndPoint1, GeoPoint2D StartPoint2, GeoPoint2D EndPoint2, out GeoPoint2D IntersectionPoint)
        {
            // return IntersectLL(StartPoint1, EndPoint1-StartPoint1,StartPoint2, EndPoint2-StartPoint2,out IntersectionPoint);
            // nach http://mathworld.wolfram.com/Line-LineIntersection.html
            /* also sieht es zunächst so aus:
            double dx1 = EndPoint1.x-StartPoint1.x; // dort x1-x2
            double dy1 = EndPoint1.y-StartPoint1.y; // dort y1-y2
            double dx2 = EndPoint2.x-StartPoint2.x; // dort x3-x4
            double dy2 = EndPoint2.y-StartPoint2.y; // dort y3-y4
            double d = dx1*dy2 - dx2*dy1; // dort der Nenner unter beiden Brüchen
            double z1 = EndPoint1.x*StartPoint1.y - EndPoint1.y*StartPoint1.x; // dir Matrix über dem Bruch linkes oberes Element
            double z2 = EndPoint2.x*StartPoint2.y - EndPoint2.y*StartPoint2.x; // dir Matrix über dem Bruch linkes unteres Element
            double x = z1*dx2 - z2*dx1;
            double y = z1*dy2 - z2*dy1;
            double l = Math.Abs(dx1)+Math.Abs(dy1)+Math.Abs(dx2)+Math.Abs(dy2); // Maß für die Länge
            if (Math.Abs(d) < l*1e-10) 
            {
                IntersectionPoint = new GeoPoint2D(0,0);
                return false;
            }
            try
            {
                IntersectionPoint = new GeoPoint2D(x/d,y/d);
                return true;
            }
            catch (ArithmeticException)
            {
                IntersectionPoint = new GeoPoint2D(0,0);
                return false;
            }
            */
            // die folgenden Zeilen sind eine leichte Verbesserung des obigen:
            // beziehen wir das Ganze auf StartPoint1 (was letztlich willkürlich ist, aber bei
            // sehr großen Koordinaten und sehr kleinen Längen (GIS) sonst Probleme macht)
            // dann sieht der selbe Text so aus:
            // er ist leider nicht mehr symmetrisch, dafür 4 Multiplikationen weniger,
            // 1 Subtraktion mehr, 2 Additionen mehr
            double dx1 = EndPoint1.x - StartPoint1.x; // dort x1-x2
            double dy1 = EndPoint1.y - StartPoint1.y; // dort y1-y2
            double dx2 = EndPoint2.x - StartPoint2.x; // dort x3-x4
            double dy2 = EndPoint2.y - StartPoint2.y; // dort y3-y4
            double d = dx1 * dy2 - dx2 * dy1; // dort der Nenner unter beiden Brüchen
            // z1 wird 0.0, double z1 = EndPoint1.x*StartPoint1.y - EndPoint1.y*StartPoint1.x; // dir Matrix über dem Bruch linkes oberes Element
            double z2 = (EndPoint2.y - StartPoint1.y) * (StartPoint2.x - StartPoint1.x) - (EndPoint2.x - StartPoint1.x) * (StartPoint2.y - StartPoint1.y); // dir Matrix über dem Bruch linkes unteres Element
            // z2 im Vorzeichen gegenüber oben geändert
            double x = z2 * dx1;
            double y = z2 * dy1;
            double l = Math.Abs(x) + Math.Abs(y); // Maß für die Länge
            if (Math.Abs(d) < l * 1e-10 || Math.Abs(d) < 1e-10)
            {	// ist dieses Maß gut?
                IntersectionPoint = new GeoPoint2D(0, 0);
                return false;
            }
            try
            {
                IntersectionPoint = new GeoPoint2D(x / d + StartPoint1.x, y / d + StartPoint1.y);
                return true;
            }
            catch (ArithmeticException)
            {
                IntersectionPoint = new GeoPoint2D(0, 0);
                return false;
            }
        }

        internal static double IntersectLLpar(GeoPoint2D StartPoint1, GeoPoint2D EndPoint1, GeoPoint2D StartPoint2, GeoPoint2D EndPoint2)
        {
            double dx1 = EndPoint1.x - StartPoint1.x; // dort x1-x2
            double dy1 = EndPoint1.y - StartPoint1.y; // dort y1-y2
            double dx2 = EndPoint2.x - StartPoint2.x; // dort x3-x4
            double dy2 = EndPoint2.y - StartPoint2.y; // dort y3-y4
            return ((EndPoint2.y - StartPoint1.y) * (StartPoint2.x - StartPoint1.x) - (EndPoint2.x - StartPoint1.x) * (StartPoint2.y - StartPoint1.y)) / (dx1 * dy2 - dx2 * dy1);
        }
        internal static void IntersectLLpar(GeoPoint2D StartPoint1, GeoVector2D Direction1, GeoPoint2D StartPoint2, GeoVector2D Direction2, out double pos1, out double pos2)
        {
            pos1 = ((StartPoint2.y + Direction2.y - StartPoint1.y) * (StartPoint2.x - StartPoint1.x) - (StartPoint2.x + Direction2.x - StartPoint1.x) * (StartPoint2.y - StartPoint1.y)) / (Direction1.x * Direction2.y - Direction2.x * Direction1.y);
            pos2 = ((StartPoint1.y + Direction1.y - StartPoint2.y) * (StartPoint1.x - StartPoint2.x) - (StartPoint1.x + Direction1.x - StartPoint2.x) * (StartPoint1.y - StartPoint2.y)) / (Direction2.x * Direction1.y - Direction1.x * Direction2.y);
        }
        internal static bool IntersectLLparInside(GeoPoint2D StartPoint1, GeoPoint2D EndPoint1, GeoPoint2D StartPoint2, GeoPoint2D EndPoint2, out double pos1, out double pos2)
        {
            return IntersectLLparInside(StartPoint1, EndPoint1 - StartPoint1, StartPoint2, EndPoint2 - StartPoint2, out pos1, out pos2);
        }
        internal static bool IntersectLLparInside(GeoPoint2D StartPoint1, GeoVector2D Direction1, GeoPoint2D StartPoint2, GeoVector2D Direction2, out double pos1, out double pos2)
        {
            double d1 = Direction1.x * Direction2.y;
            double d2 = Direction2.x * Direction1.y;
            double dd = d1 - d2;
            if (Math.Abs(dd) > 1e-13)
            {
                pos1 = ((StartPoint2.y + Direction2.y - StartPoint1.y) * (StartPoint2.x - StartPoint1.x) - (StartPoint2.x + Direction2.x - StartPoint1.x) * (StartPoint2.y - StartPoint1.y)) / (dd);
                pos2 = ((StartPoint1.y + Direction1.y - StartPoint2.y) * (StartPoint1.x - StartPoint2.x) - (StartPoint1.x + Direction1.x - StartPoint2.x) * (StartPoint1.y - StartPoint2.y)) / (-dd);
                return pos1 >= 0.0 && pos1 <= 1.0 && pos2 >= 0.0 && pos2 <= 1.0;
            }
            {
                pos1 = -1.0;
                pos2 = -1.0;
                return false;
            }
        }

        /// <summary>
        /// Calculates the intersectionpoint of two lines in 2d.
        /// </summary>
        /// <param name="StartPoint1">Startpoint of the first line</param>
        /// <param name="dir1">Direction  of the first line</param>
        /// <param name="StartPoint2">Startpoint of the second line</param>
        /// <param name="dir2">Direction of the second line</param>
        /// <param name="IntersectionPoint">the intersection point</param>
        /// <returns>true, if there is an intersection point, otherwise false </returns>
        public static bool IntersectLL(GeoPoint2D StartPoint1, GeoVector2D dir1, GeoPoint2D StartPoint2, GeoVector2D dir2, out GeoPoint2D IntersectionPoint)
        {
            // aus obigen Algorithmus abgeleitet
            double dx1 = dir1.x; // dort x1-x2
            double dy1 = dir1.y; // dort y1-y2
            double dx2 = dir2.x; // dort x3-x4
            double dy2 = dir2.y; // dort y3-y4
            double d = dx1 * dy2 - dx2 * dy1; // dort der Nenner unter beiden Brüchen
            GeoPoint2D EndPoint2 = StartPoint2 + dir2;
            double z2 = (EndPoint2.y - StartPoint1.y) * (StartPoint2.x - StartPoint1.x) - (EndPoint2.x - StartPoint1.x) * (StartPoint2.y - StartPoint1.y); // dir Matrix über dem Bruch linkes unteres Element
            double x = z2 * dx1;
            double y = z2 * dy1;
            double l = Math.Abs(x) + Math.Abs(y); // Maß für die Länge
            if (Math.Abs(d) < l * 1e-10 || Math.Abs(d) < 1e-10)
            {	// ist dieses Maß gut? NEIN!, wenn l==0.0
                IntersectionPoint = new GeoPoint2D(0, 0);
                return false;
            }
            try
            {
                IntersectionPoint = new GeoPoint2D(x / d + StartPoint1.x, y / d + StartPoint1.y);
                return true;
            }
            catch (ArithmeticException)
            {
                IntersectionPoint = new GeoPoint2D(0, 0);
                return false;
            }
        }

        /// <summary>
        /// Returns the distance of the two 2d points.
        /// </summary>
        /// <param name="FirstPoint">1. point</param>
        /// <param name="SecondPoint">2. point</param>
        /// <returns>distance</returns>
        public static double Dist(GeoPoint2D FirstPoint, GeoPoint2D SecondPoint)
        {
            return Math.Sqrt(sqr(FirstPoint.x - SecondPoint.x) + sqr(FirstPoint.y - SecondPoint.y));
        }
        /// <summary>
        /// Returns the distance of the two 3d points.
        /// </summary>
        /// <param name="FirstPoint">1. point</param>
        /// <param name="SecondPoint">2. point</param>
        /// <returns>distance</returns>
        public static double Dist(GeoPoint FirstPoint, GeoPoint SecondPoint)
        {
            return Math.Sqrt(sqr(FirstPoint.x - SecondPoint.x) + sqr(FirstPoint.y - SecondPoint.y) + sqr(FirstPoint.z - SecondPoint.z));
        }
        /// <summary>
        /// Calculates the minimal distance between two lines and the parameter on the lines where the minimal
        /// distance occures. If the lines are parallel, the parameters will be double.MaxValue.
        /// The point <paramref name="l1Start"/> + <paramref name="par1"/>*<paramref name="l1Dir"/> is closest
        /// to the second line, and the point <paramref name="l2Start"/> + <paramref name="par2"/>*<paramref name="l2Dir"/> is closest
        /// to the first line.
        /// </summary>
        /// <param name="l1Start">Startpoint of the first line</param>
        /// <param name="l1Dir">Direction of the first line</param>
        /// <param name="l2Start">Startpoint of the second line</param>
        /// <param name="l2Dir">Direction of the second line</param>
        /// <param name="par1">Parameter on the first line</param>
        /// <param name="par2">Parameter on the second line</param>
        /// <returns>Minimal distance</returns>
        public static double DistLL(GeoPoint l1Start, GeoVector l1Dir, GeoPoint l2Start, GeoVector l2Dir, out double par1, out double par2)
        {
            // l1s + p1*l1dir == l2s + p2*l2dir + p3*xdir; // xdir ist die senkrechte zu beiden
            // p1*l1dir -p2*l2dir - p3*xdir == l2s - l1s
            // ACHTUNG: par2 jetzt mit richtigem Vorzeichen
            try
            {
                GeoVector xdir = l1Dir ^ l2Dir;
                xdir.Norm();
                Matrix m = DenseMatrix.OfRowArrays(l1Dir, l2Dir, xdir);
                Vector b = new DenseVector(l2Start - l1Start);
                Vector x = (Vector)m.Transpose().Solve(b);
                if (x.IsValid())
                {
                    par1 = x[0];
                    par2 = -x[1];
                    return Math.Abs(x[2]);
                }
                else
                {
                    par1 = double.MaxValue;
                    par2 = double.MaxValue;
                    return Geometry.DistPL(l2Start, l1Start, l1Dir);
                }
            }
            catch (GeoVectorException)
            {
                par1 = double.MaxValue;
                par2 = double.MaxValue;
                return Geometry.DistPL(l2Start, l1Start, l1Dir);
            }
        }
        /// <summary>
        /// Intersection point of two lines in 3D. This should only be called when the two lines share a common plane. If not, the result will be in the middle
        /// of the closest connection of the two points. If the lines are parallel a GeometryException will be thrown.
        /// </summary>
        /// <param name="l1Start">Startpoint of first line</param>
        /// <param name="l1Dir">Direction of first line</param>
        /// <param name="l2Start">Startpoint of second line</param>
        /// <param name="l2Dir">Direction of second line</param>
        /// <returns></returns>
        public static GeoPoint IntersectLL(GeoPoint l1Start, GeoVector l1Dir, GeoPoint l2Start, GeoVector l2Dir)
        {
            try
            {
                GeoVector xdir = l1Dir ^ l2Dir;
                xdir.Norm();
                Matrix m = DenseMatrix.OfRowArrays(l1Dir, l2Dir, xdir);
                Vector b = new DenseVector(l2Start - l1Start);
                Vector x = (Vector)m.Transpose().Solve(b);
                if (x.IsValid())
                {
                    return new GeoPoint(l1Start + x[0] * l1Dir, l2Start - x[1] * l2Dir);
                }
                else
                {
                    throw new GeometryException("trying to intersect parallel lines");
                }
            }
            catch (GeoVectorException)
            {
                throw new GeometryException("trying to intersect parallel lines");
            }
        }
        internal static double DistLLWrongPar2(GeoPoint l1Start, GeoVector l1Dir, GeoPoint l2Start, GeoVector l2Dir, out double par1, out double par2)
        {
            // l1s + p1*l1dir == l2s + p2*l2dir + p3*xdir; // xdir ist die senkrechte zu beiden
            // p1*l1dir -p2*l2dir - p3*xdir == l2s - l1s
            // ACHTUNG: par2 hat einen Vorzeichenfehler. Diese Methode nicht mehr verwenden. Überprüfen, ob es OK ist,
            // wo sie verwendet wurde und durch DistLL ersetzen
            try
            {
                GeoVector xdir = l1Dir ^ l2Dir;
                xdir.Norm();
                Matrix m = DenseMatrix.OfRowArrays(l1Dir, l2Dir, xdir);
                Vector b = new DenseVector(l2Start - l1Start);
                Vector x = (Vector)m.Transpose().Solve(b);
                if (x != null)
                {
                    par1 = x[0];
                    par2 = x[1];
                    return Math.Abs(x[2]);
                }
                else
                {
                    par1 = double.MaxValue;
                    par2 = double.MaxValue;
                    return Geometry.DistPL(l2Start, l1Start, l1Dir);
                }
            }
            catch (GeoVectorException)
            {
                par1 = double.MaxValue;
                par2 = double.MaxValue;
                return Geometry.DistPL(l2Start, l1Start, l1Dir);
            }
        }
        /// <summary>
        /// Checks whether the point P is on the left side of the line given by lstart and ldir.
        /// If the point lies exactly on the line, the result is false. There is no precision or epsilon test.
        /// <seealso cref="Precision"/>
        /// </summary>
        /// <param name="P">The point to test</param>
        /// <param name="lstart">Startpoint of the line</param>
        /// <param name="ldir">Direction of the line</param>
        /// <returns>true, if P is on the left side, false otherwise</returns>
        public static bool OnLeftSide(GeoPoint2D P, GeoPoint2D lstart, GeoVector2D ldir)
        {	// spart zu distPL die Division durch die Länge. Noch testen, ob <0.0 oder >0.0
            return (ldir.y * P.x - ldir.x * P.y + lstart.y * ldir.x - lstart.x * ldir.y) < 0.0;
        }
        public static bool OnLeftSide(GeoPoint2D P, GeoPoint2D lstart, GeoPoint2D lend)
        {	// spart zu distPL die Division durch die Länge. Noch testen, ob <0.0 oder >0.0
            return ((lend.y - lstart.y) * P.x - (lend.x - lstart.x) * P.y + lstart.y * lend.x - lstart.x * lend.y) < 0.0;
        }
        internal static bool OnLeftSideZ(GeoPoint2D P, GeoPoint2D lstart, GeoPoint2D lend)
        {	// spart zu distPL die Division durch die Länge. Noch testen, ob <0.0 oder >0.0
            return ((lend.y - lstart.y) * P.x - (lend.x - lstart.x) * P.y + lstart.y * lend.x - lstart.x * lend.y) <= 0.0;
        }
        /// <summary>
        /// Returns true, if p1 an p2 are on the same side of the line given by p3 and p4. Also returns
        /// true if one of the two points p1 and p2 coincide with the line (p3,p4)
        /// </summary>
        /// <param name="p1">first testpoint</param>
        /// <param name="p2">second testpoint</param>
        /// <param name="p3">first linepoint</param>
        /// <param name="p4">second linepoint</param>
        /// <returns>true if on the same side</returns>
        public static bool OnSameSide(GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3, GeoPoint2D p4)
        {	// liegen p0 und p1 auf der selben Seite der Linie inclusive epsilon
            double dx = p4.x - p3.x;
            double dy = p4.y - p3.y;
            double eps = Math.Max(Math.Abs(dx), Math.Abs(dy)) * 1e-10;

            double d0 = (dy * p1.x - dx * p1.y + p3.y * p4.x - p3.x * p4.y);
            double d1 = (dy * p2.x - dx * p2.y + p3.y * p4.x - p3.x * p4.y);
            if (Math.Abs(d0) > Math.Abs(d1))
            {
                if (d0 > 0) return d1 > -eps;
                else return d1 < eps;
            }
            else
            {
                if (d1 > 0) return d0 > -eps;
                else return d0 < eps;
            }
        }
        internal static bool OnSameSideFast(GeoPoint2D p0, GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3)
        {	// liegen p0 und p1 auf der selben Seite der Linie 
            // verhalten bei Linientreffer ist zufällig
            double dx = p3.x - p2.x;
            double dy = p3.y - p2.y;
            double c = p2.y * p3.x - p2.x * p3.y;
            double d0 = (dy * p0.x - dx * p0.y + c);
            double d1 = (dy * p1.x - dx * p1.y + c);
            return Math.Sign(d0) == Math.Sign(d1);
        }
        public static bool OnSameSide(GeoPoint2D p0, GeoPoint2D p1, GeoPoint2D p2, GeoVector2D v2)
        {	// liegen p0 und p1 auf der selben Seite der Linie inclusive epsilon
            double dx = v2.x;
            double dy = v2.y;
            double eps = Math.Max(Math.Abs(dx), Math.Abs(dy)) * 1e-10;

            double d0 = (dy * p0.x - dx * p0.y + p2.y * v2.x - p2.x * v2.y);
            double d1 = (dy * p1.x - dx * p1.y + p2.y * v2.x - p2.x * v2.y);
            if (Math.Abs(d0) > Math.Abs(d1))
            {
                if (d0 > 0) return d1 > -eps;
                else return d1 < eps;
            }
            else
            {
                if (d1 > 0) return d0 > -eps;
                else return d0 < eps;
            }
        }
        public static bool OnSameSideStrict(GeoPoint2D p0, GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3)
        {	// wie OnSameSide, aber false, wenn ein Punkt draufliegt
            double dx = p3.x - p2.x;
            double dy = p3.y - p2.y;
            double eps = Math.Max(Math.Abs(dx), Math.Abs(dy)) * 1e-6;

            double d0 = (dy * p0.x - dx * p0.y + p2.y * p3.x - p2.x * p3.y);
            double d1 = (dy * p1.x - dx * p1.y + p2.y * p3.x - p2.x * p3.y);
            if (Math.Abs(d0) > Math.Abs(d1))
            {
                if (d0 > 0) return d1 > eps;
                else return d1 < -eps;
            }
            else
            {
                if (d1 > 0) return d0 > eps;
                else return d0 < -eps;
            }
        }
        /// <summary>
        /// Returns true, if the two line segments given by (p0,p1) and (p2,p3) have a common intersection. The intersection
        /// point is noct calculated. This Method is intended as a fast check for intersection or not. Returns true, if 
        /// one segment starts or ends on the other segment. 
        /// </summary>
        /// <param name="p0">first line startpoint</param>
        /// <param name="p1">first line endpoint</param>
        /// <param name="p2">second line startpoint</param>
        /// <param name="p3">second line endpoint</param>
        /// <returns>true if segment intersection</returns>
        public static bool SegmentIntersection(GeoPoint2D p0, GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3)
        {	// SChneiden sich die beiden Linien?
            // alter text war:
            // if (OnSameSideStrict(p0, p1, p2, p3)) return false;
            // if (OnSameSideStrict(p2, p3, p0, p1)) return false;
            // schneller geht es so:
            // zuerst Minmax Test, man könnte mit weniger Vergleichen auskommen!
            if (Math.Max(p0.x, p1.x) < Math.Min(p2.x, p3.x)) return false;
            if (Math.Min(p0.x, p1.x) > Math.Max(p2.x, p3.x)) return false;
            if (Math.Max(p0.y, p1.y) < Math.Min(p2.y, p3.y)) return false;
            if (Math.Min(p0.y, p1.y) > Math.Max(p2.y, p3.y)) return false;
            // die vorzeichenbehafteten Flächen der 4 Dreiecke berechnen
            double a1 = p0.x * (p1.y - p2.y) + p1.x * (p2.y - p0.y) + p2.x * (p0.y - p1.y); // Dreieck p0,p1,p2 (Fläche*2)
            double a2 = p0.x * (p1.y - p3.y) + p1.x * (p3.y - p0.y) + p3.x * (p0.y - p1.y); // Dreieck p0,p1,p3
            double a3 = p2.x * (p3.y - p0.y) + p3.x * (p0.y - p2.y) + p0.x * (p2.y - p3.y); // Dreieck p2,p3,p0
            double a4 = p2.x * (p3.y - p1.y) + p3.x * (p1.y - p2.y) + p1.x * (p2.y - p3.y); // Dreieck p2,p3,p1
            double eps = ((p1 - p0).TaxicabLength + (p3 - p2).TaxicabLength) * 1e-8;
            // haben zwei Dreiecke das gleiche Vorzeichen, dann liegen sie auf der selben Seite und es gibt keinen Schnitt
            if (a1 > eps && a2 > eps) return false;
            if (a1 < -eps && a2 < -eps) return false;
            if (a3 > eps && a4 > eps) return false;
            if (a3 < -eps && a4 < -eps) return false;
            // Möglicherweise sind die beiden Linien kolinear, dann sind alle Flächen 0 (selten)
            if (Math.Abs(a1) < eps && Math.Abs(a2) < eps && Math.Abs(a3) < eps && Math.Abs(a4) < eps)
            {
                double lp1 = LinePar(p0, p1, p2);
                double lp2 = LinePar(p0, p1, p3);
                if (lp1 > 1.0 && lp2 > 1.0) return false;
                if (lp1 < 0.0 && lp2 < 0.0) return false;
                // hier könnten sie kolinear sein
            }
            // alle anderen Fälle bedeuten Schnitt
            return true;
        }
        /// <summary>
        /// Same as <see cref="SegmentIntersection"/>, but returns true only if the intersection happens in the inside of
        /// the two segments, not at the endpoints
        /// </summary>
        /// <param name="p0">first line startpoint</param>
        /// <param name="p1">first line endpoint</param>
        /// <param name="p2">second line startpoint</param>
        /// <param name="p3">second line endpoint</param>
        /// <returns>true if segment intersection</returns>
        public static bool SegmentInnerIntersection(GeoPoint2D p0, GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3)
        {	// SChneiden sich die beiden Linien?
            // alter text war:
            // if (OnSameSideStrict(p0, p1, p2, p3)) return false;
            // if (OnSameSideStrict(p2, p3, p0, p1)) return false;
            // schneller geht es so:
            // zuerst Minmax Test, man könnte mit weniger Vergleichen auskommen!
            if (Math.Max(p0.x, p1.x) < Math.Min(p2.x, p3.x)) return false;
            if (Math.Min(p0.x, p1.x) > Math.Max(p2.x, p3.x)) return false;
            if (Math.Max(p0.y, p1.y) < Math.Min(p2.y, p3.y)) return false;
            if (Math.Min(p0.y, p1.y) > Math.Max(p2.y, p3.y)) return false;
            // die vorzeichenbehafteten Flächen der 4 Dreiecke berechnen
            double a1 = p0.x * (p1.y - p2.y) + p1.x * (p2.y - p0.y) + p2.x * (p0.y - p1.y); // Dreieck p0,p1,p2 (Fläche*2)
            double a2 = p0.x * (p1.y - p3.y) + p1.x * (p3.y - p0.y) + p3.x * (p0.y - p1.y); // Dreieck p0,p1,p3
            double a3 = p2.x * (p3.y - p0.y) + p3.x * (p0.y - p2.y) + p0.x * (p2.y - p3.y); // Dreieck p2,p3,p0
            double a4 = p2.x * (p3.y - p1.y) + p3.x * (p1.y - p2.y) + p1.x * (p2.y - p3.y); // Dreieck p2,p3,p1
            double eps = ((p1 - p0).TaxicabLength + (p3 - p2).TaxicabLength) * 1e-8;
            // haben zwei Dreiecke das gleiche Vorzeichen, dann liegen sie auf der selben Seite und es gibt keinen Schnitt
            if (a1 >= 0 && a2 >= 0) return false;
            if (a1 <= 0 && a2 <= 0) return false;
            if (a3 >= 0 && a4 >= 0) return false;
            if (a3 <= 0 && a4 <= 0) return false;
            // Möglicherweise sind die beiden Linien kolinear, dann sind alle Flächen 0 (selten)
            if (Math.Abs(a1) < eps && Math.Abs(a2) < eps && Math.Abs(a3) < eps && Math.Abs(a4) < eps)
            {
                double lp1 = LinePar(p0, p1, p2);
                double lp2 = LinePar(p0, p1, p3);
                if (lp1 > 1.0 && lp2 > 1.0) return false;
                if (lp1 < 0.0 && lp2 < 0.0) return false;
                // hier könnten sie kolinear sein
            }
            // alle anderen Fälle bedeuten Schnitt
            return true;
        }
        /// <summary>
        /// Returns the distance of a point from a line (2d). This might also be negative
        /// </summary>
        /// <param name="P">the point</param>
        /// <param name="lstart">startpoint of the line</param>
        /// <param name="ldir">direction of the line</param>
        /// <returns>distance</returns>
        public static double DistPL(GeoPoint2D P, GeoPoint2D lstart, GeoVector2D ldir)
        {
            return -((ldir.y * P.x - ldir.x * P.y + lstart.y * (lstart.x + ldir.x) - lstart.x * (lstart.y + ldir.y)) / Math.Sqrt(sqr(ldir.x) + sqr(ldir.y)));
        }
        public static double DistPL(GeoPoint P, Axis ax)
        {
            return DistPL(P, ax.Location, ax.Direction);
        }
        /// <summary>
        /// Returns the distance of a point from a line (2d), the distance is positive when the point is on the left side of the
        /// line, negative on the right side.
        /// </summary>
        /// <param name="P">the point</param>
        /// <param name="lstart">startpoint of the line</param>
        /// <param name="lend">endpoint of the line</param>
        /// <returns>distance</returns>
        public static double DistPL(GeoPoint2D P, GeoPoint2D lstart, GeoPoint2D lend)
        {
            double dx, dy;
            if (lstart == lend) return Dist(P, lstart);
            dx = lend.x - lstart.x;
            dy = lend.y - lstart.y;

            return ((lstart.x - P.x) * dy - dx * (lstart.y - P.y)) / Math.Sqrt(dx * dx + dy * dy); // besser und schneller

            // return -((dy * P.x - dx * P.y + lstart.y * lend.x - lstart.x * lend.y) / Math.Sqrt(dx * dx + dy * dy));
        }
        internal static double DistPL(ref GeoPoint2D P, ref GeoPoint2D lstart, ref GeoPoint2D lend)
        {
            double dx, dy;
            if (lstart == lend) return Dist(P, lstart);
            dx = lend.x - lstart.x;
            dy = lend.y - lstart.y;
            return -((dy * P.x - dx * P.y + lstart.y * lend.x - lstart.x * lend.y) / Math.Sqrt(dx * dx + dy * dy));
        }

        /// <summary>
        /// Returns the distance of a point from a line (3d)
        /// </summary>
        /// <param name="P">the point</param>
        /// <param name="lstart">startpoint of the line</param>
        /// <param name="lend">endpoint of the line</param>
        /// <returns>distance</returns>
        public static double DistPL(GeoPoint P, GeoPoint lstart, GeoPoint lend)
        {	// http://mathworld.wolfram.com/Point-LineDistance3-Dimensional.html
            GeoVector v2 = (lend - lstart);
            if (v2.Length < Precision.eps) return Dist(P, lstart);
            GeoVector v1 = v2 ^ (lstart - P);
            return v1.Length / v2.Length;
        }

        /// <summary>
        /// Returns the distance of a point from a line (3d)
        /// </summary>
        /// <param name="P">the point</param>
        /// <param name="lstart">startpoint of the line</param>
        /// <param name="ldir">direction of the line</param>
        /// <returns>distance</returns>
        public static double DistPL(GeoPoint P, GeoPoint lstart, GeoVector ldir)
        {	// http://mathworld.wolfram.com/Point-LineDistance3-Dimensional.html
            GeoVector v2 = ldir;
            if (v2.Length < Precision.eps) return Dist(P, lstart);
            GeoVector v1 = v2 ^ (lstart - P);
            return v1.Length / v2.Length;
        }
        /// <summary>
        /// Returns the perpendicular foot point of point p on the line (lstart,lend)
        /// </summary>
        /// <param name="P">the source point</param>
        /// <param name="lstart">startpoint of the line</param>
        /// <param name="lend">endpoint of the line</param>
        /// <returns>footpoint</returns>
        public static GeoPoint2D DropPL(GeoPoint2D P, GeoPoint2D lstart, GeoPoint2D lend)
        {	// das Lot von P auf die Linie
            double dx = lend.x - lstart.x;
            double dy = lend.y - lstart.y;
            double d = (dx * dx + dy * dy);
            if (d == 0.0) return lstart; // beide Punkte gleich

            //double dist = (dy * P.x - dx * P.y + lstart.y * lend.x - lstart.x * lend.y) / d;
            double dist = -((lstart.x - P.x) * dy - dx * (lstart.y - P.y)) / d;
            return new GeoPoint2D(P.x - dy * dist, P.y + dx * dist);
        }

        /// <summary>
        /// Returns the perpendicular foot point of point p on the line (s,e)
        /// </summary>
        /// <param name="P">The source point</param>
        /// <param name="s">Startpoint of the line</param>
        /// <param name="e">Endpoint of the line</param>
        /// <returns>Footpoint</returns>
        public static GeoPoint DropPL(GeoPoint p, GeoPoint s, GeoPoint e)
        {	// das Lot von P auf die Linie
            // (s+l*v)-p senkrecht auf v, d.h. ((s+l*v)-p)*v == 0
            GeoVector v = e - s;
            double d = (v.x * v.x + v.y * v.y + v.z * v.z);
            if (d == 0.0) return s; // beide Punkte gleich
            double lambda = -((s.x - p.x) * v.x + (s.y - p.y) * v.y + (s.z - p.z) * v.z) / d;
            return s + lambda * v;
        }

        /// <summary>
        /// Returns the perpendicular foot point of point p on the line through s
        /// with direction v
        /// </summary>
        /// <param name="P">The source point</param>
        /// <param name="s">Startpoint of the line</param>
        /// <param name="v">Direction of the line</param>
        /// <returns>Footpoint</returns>
        public static GeoPoint DropPL(GeoPoint p, GeoPoint s, GeoVector v)
        {	// das Lot von P auf die Linie
            // (s+l*v)-p senkrecht auf v, d.h. ((s+l*v)-p)*v == 0
            double d = (v.x * v.x + v.y * v.y + v.z * v.z);
            if (d == 0.0) return s; // beide Punkte gleich
            double lambda = -((s.x - p.x) * v.x + (s.y - p.y) * v.y + (s.z - p.z) * v.z) / d;
            return s + lambda * v;
        }

        /// <summary>
        /// Returns the perpendicular foot point of point p on the line through s
        /// with direction v
        /// </summary>
        /// <param name="P">The source point</param>
        /// <param name="s">Startpoint of the line</param>
        /// <param name="v">Direction of the line</param>
        /// <returns>Footpoint</returns>
        public static GeoPoint2D DropPL(GeoPoint2D p, GeoPoint2D s, GeoVector2D v)
        {	// das Lot von P auf die Linie
            // (s+l*v)-p senkrecht auf v, d.h. ((s+l*v)-p)*v == 0
            double d = (v.x * v.x + v.y * v.y);
            if (d == 0.0) return s; // beide Punkte gleich
            double lambda = -((s.x - p.x) * v.x + (s.y - p.y) * v.y) / d;
            return s + lambda * v;
        }
        public static GeoPoint2D[] TangentPointCircle(GeoPoint2D p, GeoPoint2D c, double radius)
        {
            // from https://stackoverflow.com/questions/1351746/find-a-tangent-point-on-circle
            var dx = c.x - p.x;
            var dy = c.y - p.y;
            if (dx == 0 && dy == 0) return new GeoPoint2D[0]; // no solution

            // PC is distance between P and C, pc2 is PC^2
            var pc2 = dx * dx + dy * dy;
            var pc = Math.Sqrt(pc2);

            if (pc < radius - Precision.eps) 
                return new GeoPoint2D[0]; // no solution

            // R is radius of  circle centered in P, r2 is R^2
            var r2 = pc2 - radius * radius;
            // d is the P => X0 distance (demonstration is here https://mathworld.wolfram.com/Circle-CircleIntersection.html where PC is named 'd' in there)
            var d = r2 / pc;
            // h is the X0 => X1 (and X0 => X2) distance
            var h = Math.Sqrt(r2 - d * d);
            return new GeoPoint2D[] { new GeoPoint2D(p.x + (dx * d - dy * h) / pc, p.y + (dy * d + dx * h) / pc),
                                      new GeoPoint2D(p.x + (dx * d + dy * h) / pc, p.y + (dy * d - dx * h) / pc) };

        }
        static GeoPoint2D[] TangentPointCircleOld(GeoPoint2D fromHere, GeoPoint2D center, double radius)
        {
            //GeoVector2D d = center - fromHere;
            //double dd = d.Length;
            //if (radius > dd) return new GeoPoint2D[0];
            //double a = Math.Asin(radius / dd);
            //double b = Math.Atan2(d.y, d.x);
            //GeoPoint2D[] res = new GeoPoint2D[2];
            //double t = b - a;
            //res[0] = new GeoPoint2D(radius * Math.Sin(t), radius * -Math.Cos(t));
            //t = b + a;
            //res[1] = new GeoPoint2D(radius * -Math.Sin(t), radius * Math.Cos(t));
            //return res;
            GeoPoint2D center2 = new GeoPoint2D(fromHere, center); // center between fromHere and center
            GeoPoint2D[] res = Geometry.IntersectCC(center2, Geometry.Dist(center2, center), center, radius);
            return res;
        }

        /// <summary>
        /// Tangential lines to two circles, returned as pairs of points
        /// </summary>
        /// <param name="center1"></param>
        /// <param name="radius1"></param>
        /// <param name="center2"></param>
        /// <param name="radius2"></param>
        /// <returns></returns>
        public static GeoPoint2D[] TangentCC(GeoPoint2D center1, double radius1, GeoPoint2D center2, double radius2)
        {
            double d = center1 | center2;
            if (d < radius1 || d < radius2) return new GeoPoint2D[0];
            GeoPoint2D[] res;
            if (d > radius1 + radius2) res = new GeoPoint2D[8];
            else res = new GeoPoint2D[4];
            bool exchange;
            if (radius1 > radius2)
            {
                double tmp = radius1;
                radius1 = radius2;
                radius2 = tmp;
                GeoPoint2D tmpc = center1;
                center1 = center2;
                center2 = tmpc;
                exchange = true;
            }
            else exchange = false;
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(new Circle2D(center1, radius1));
            dc.Add(new Circle2D(center2, radius2));
#endif
            GeoPoint2D[] tan = TangentPointCircle(center1, center2, radius2 - radius1); // must be two points
            if (tan == null || tan.Length != 2)
                throw new InvalidOperationException("TangentPointCircle did not return the expected tangent points.");

            // must be 2 points
            GeoVector2D v = tan[0] - center2;
            v.Length = radius1;
            res[0] = center1 + v;
            v.Length = radius2;
            res[1] = center2 + v;
            v = tan[1] - center2;
            v.Length = radius1;
            res[2] = center1 + v;
            v.Length = radius2;
            res[3] = center2 + v;
            if (d > radius1 + radius2)
            {
                // two more tangents
                v = center2 - center1;
                v.Length = radius1 * d / (radius1 + radius2);
                GeoPoint2D c = center1 + v; // homothetic center 
                GeoPoint2D[] tan1 = TangentPointCircle(c, center1, radius1);
                if (tan1 == null || tan1.Length != 2)
                    throw new InvalidOperationException("TangentPointCircle did not return the expected tangent points.");
                
                GeoPoint2D[] tan2 = TangentPointCircle(c, center2, radius2);
                if (tan2 == null || tan2.Length != 2)
                    throw new InvalidOperationException("TangentPointCircle did not return the expected tangent points.");

                // must be two points for each
                if (Geometry.OnSameSide(tan1[0], tan2[0], center1, center2))
                {
                    res[4] = tan1[0];
                    res[5] = tan2[1];
                    res[6] = tan1[1];
                    res[7] = tan2[0];
                }
                else
                {
                    res[4] = tan1[0];
                    res[5] = tan2[0];
                    res[6] = tan1[1];
                    res[7] = tan2[1];
                }
            }
            if (exchange)
            {
                for (int i = 0; i < res.Length; i += 2)
                {
                    GeoPoint2D tmp = res[i];
                    res[i] = res[i + 1];
                    res[i + 1] = tmp;
                }
            }
#if DEBUG
            for (int i = 0; i < res.Length; i += 2)
            {
                dc.Add(new Line2D(res[i], res[i + 1]));
            }
#endif
            return res;
        }
        /// <summary>
        /// Returns the intersection points of two circles in 2d.
        /// The result may contain 0 to 2 points.
        /// </summary>
        /// <param name="center1">center of the first cricle</param>
        /// <param name="radius1">radius of the first circle</param>
        /// <param name="center2">center of the second cricle</param>
        /// <param name="radius2">radius of the second circle</param>
        /// <returns>intersctione points</returns>
        public static GeoPoint2D[] IntersectCC(GeoPoint2D center1, double radius1, GeoPoint2D center2, double radius2)
        {
            double dy, dx, d1, d2, du;
            double aq, bq, cq;
            int m;
            du = Dist(center1, center2);
            if (du < Precision.eps) return new GeoPoint2D[0];
            if (Math.Abs(radius1 + radius2 - du) < Precision.eps || Math.Abs(Math.Abs(radius1 - radius2) - du) < Precision.eps)
            {	// die beiden Kreise berühren sich von innen oder außen
                GeoPoint2D[] s = IntersectLC(center1, center2, center2, radius2);
                if (s.Length == 0) return s;
                if (s.Length == 2)
                {	// TODO: hier noch checken!
                    if (Math.Abs(Dist(s[1], center1) - radius1) < Math.Abs(Dist(s[0], center1) - radius1)) s[0] = s[1];
                    else s[1] = s[0];
                }
                return s;
            }
            else
            {
                dx = center2.x - center1.x;
                dy = center2.y - center1.y;
                d1 = sqr(radius1) - sqr(center1.x) - sqr(center1.y);
                d2 = sqr(radius2) - sqr(center2.x) - sqr(center2.y);
                du = (d1 - d2) / 2;
                if ((Math.Abs(dx) == 0) && (Math.Abs(dy) == 0)) return new GeoPoint2D[0];
                else if (Math.Abs(dy) > Math.Abs(dx))
                {
                    aq = 1 + sqr(dx / dy);
                    bq = 2 * ((center1.y * dx / dy) - (du * dx) / sqr(dy) - center1.x);
                    cq = sqr(du) / sqr(dy) - 2 * center1.y * du / dy - d1;
                    double s0x, s1x;
                    m = quadgl(aq, bq, cq, out s0x, out s1x);
                    switch (m)
                    {
                        case 1:
                            GeoPoint2D[] s = new GeoPoint2D[1];
                            s[0].x = s0x;
                            s[0].y = (du - s[0].x * dx) / dy;
                            return s;
                        case 2:
                            GeoPoint2D[] s1 = new GeoPoint2D[2];
                            s1[0].x = s0x;
                            s1[1].x = s1x;
                            s1[0].y = (du - s1[0].x * dx) / dy;
                            s1[1].y = (du - s1[1].x * dx) / dy;
                            return s1;
                        default: return new GeoPoint2D[0];
                    }
                }
                else
                {
                    aq = 1 + sqr(dy / dx);
                    bq = 2 * ((center1.x * dy / dx) - (du * dy) / sqr(dx) - center1.y);
                    cq = sqr(du) / sqr(dx) - 2 * center1.x * du / dx - d1;
                    double s0y, s1y;
                    m = quadgl(aq, bq, cq, out s0y, out s1y);
                    switch (m)
                    {
                        case 1:
                            GeoPoint2D[] s = new GeoPoint2D[1];
                            s[0].y = s0y;
                            s[0].x = (du - s[0].y * dy) / dx;
                            return s;
                        case 2:
                            GeoPoint2D[] s1 = new GeoPoint2D[2];
                            s1[0].y = s0y;
                            s1[1].y = s1y;
                            s1[0].x = (du - s1[0].y * dy) / dx;
                            s1[1].x = (du - s1[1].y * dy) / dx;
                            return s1;
                        default: return new GeoPoint2D[0];
                    }
                }
                //				if ((center1.x * center2.y + center2.x * s[0].y + center1.y * s[0].x -
                //					center2.y * s[0].x - center1.y * center2.x - center1.x * s[0].y) < 0)
                //				{
                //   				GeoPoint2D h = s[0];
                //					s[0] = s[1];
                //					s[1] = h;
                //				}
            }
            // return new GeoPoint2D[0];
        }

        /// <summary>
        /// Returns the intersection points of a circle and a line in 2d.
        /// The result may contain 0 to 2 points.
        /// </summary>
        /// <param name="lstart">startpoint of the line</param>
        /// <param name="lend">endpoint of the line</param>
        /// <param name="center">center of the cricle</param>
        /// <param name="radius">radius of the circle</param>
        /// <returns>intersctione points</returns>

        public static GeoPoint2D[] IntersectLC(GeoPoint2D lstart, GeoPoint2D lend, GeoPoint2D center, double radius)
        {
            if (lstart == lend || radius == 0) return new GeoPoint2D[0];

            SweepAngle a = new SweepAngle(lend, lstart);
            ModOp2D m = ModOp2D.Rotate(-a) * ModOp2D.Translate(-center.x, -center.y);

            double lf; // y = lf, die Geradegleichung
            double cl; // 1*x*x = cl
            double dx, r2;

            dx = Dist(lstart, lend);
            // ld = 0!
            //lf = (lstart.y*lend.x - lstart.x*lend.y)/dx;
            GeoPoint2D ls = m * lstart;
            GeoPoint2D le = m * lend;
            lf = (ls.y + le.y) / 2.0; // wg. Genauigkeit, die beiden y Werte müssten eigentlich identisch sein
            // al = 1
            // bl = 0
            r2 = sqr(radius);
            cl = r2 - sqr(lf);
            if (Math.Abs(cl) < radius * 1e-10)
            {
                ModOp2D rev = ModOp2D.Translate(center.x, center.y) * ModOp2D.Rotate(a);
                GeoPoint2D[] IntersectionPoints = new GeoPoint2D[1];
                IntersectionPoints[0] = rev * new GeoPoint2D(0.0, lf);
                return IntersectionPoints;
            }
            else if (cl > 0.0)
            {
                ModOp2D rev = ModOp2D.Translate(center.x, center.y) * ModOp2D.Rotate(a);
                GeoPoint2D[] IntersectionPoints = new GeoPoint2D[2];
                IntersectionPoints[0].x = Math.Sqrt(cl);
                IntersectionPoints[0].y = lf;
                IntersectionPoints[1].x = -IntersectionPoints[0].x;
                IntersectionPoints[1].y = lf;
                IntersectionPoints[0] = rev * IntersectionPoints[0];
                IntersectionPoints[1] = rev * IntersectionPoints[1];
                return IntersectionPoints;
            }
            else return new GeoPoint2D[0];
        }

        /// <summary>
        /// Returns the intersection points of a circle and a line in 2d.
        /// The result may contain 0 to 2 points.
        /// </summary>
        /// <param name="lstart">point of the line</param>
        /// <param name="lend">direction of the line</param>
        /// <param name="center">center of the cricle</param>
        /// <param name="radius">radius of the circle</param>
        /// <returns>intersctione points</returns>
        public static GeoPoint2D[] IntersectLC(GeoPoint2D pointofline, GeoVector2D direction, GeoPoint2D center, double radius)
        {
            // von Maxima: solve((x+l*dx-cx)^2+(y+l*dy-cy)^2=r^2,l);
            //pointofline(x,y),direction(dx,dy),center(cx,cy),radius=r
            //l=-(sqrt(-dx^2*y^2+(2*dx*dy*x-2*cx*dx*dy+2*cy*dx^2)*y-dy^2*x^2+(2*cx*dy^2-2*cy*dx*dy)*x+(dy^2+dx^2)*r^2-cx^2*dy^2+2*cx*cy*dx*dy-cy^2*dx^2)+dy*y+dx*x-cy*dy-cx*dx)/(dy^2+dx^2)
            //l=(sqrt(-dx^2*y^2+(2*dx*dy*x-2*cx*dx*dy+2*cy*dx^2)*y-dy^2*x^2+(2*cx*dy^2-2*cy*dx*dy)*x+(dy^2+dx^2)*r^2-cx^2*dy^2+2*cx*cy*dx*dy-cy^2*dx^2)-dy*y-dx*x+cy*dy+cx*dx)/(dy^2+dx^2)]

            //if (Precision.IsEqual(direction, new GeoVector2D(0, 0)) || radius <= Precision.eps)
            if (direction.IsNullVector() || radius <= Precision.eps) return new GeoPoint2D[0];
            double x = pointofline.x;
            double y = pointofline.y;
            double dx = direction.x;
            double dy = direction.y;
            double cx = center.x;
            double cy = center.y;

            double d = Math.Abs(Geometry.DistPL(center, pointofline, direction));
            if (d / radius > 1 + Precision.eps)
                return new GeoPoint2D[0];

            double root = -dx * dx * y * y + (2 * dx * dy * x - 2 * cx * dx * dy + 2 * cy * dx * dx) * y - dy * dy * x * x + (2 * cx * dy * dy - 2 * cy * dx * dy) * x + (dy * dy + dx * dx) * radius * radius - cx * cx * dy * dy + 2 * cx * cy * dx * dy - cy * cy * dx * dx;
            if (root < 0.0)
                root = 0.0;

            double deno = dy * dy + dx * dx;
            double l1 = -(Math.Sqrt(root) + dy * y + dx * x - cy * dy - cx * dx) / deno;
            GeoPoint2D p1 = new GeoPoint2D(x + l1 * dx, y + l1 * dy);
            if (d / radius >= 1 - Precision.eps)
                return new GeoPoint2D[] { p1 };
            double l2 = (Math.Sqrt(root) - dy * y - dx * x + cy * dy + cx * dx) / deno;
            GeoPoint2D p2 = new GeoPoint2D(x + l2 * dx, y + l2 * dy);
            return new GeoPoint2D[] { p1, p2 };
        }

        internal static void GetEllipseModOps(GeoPoint2D Center, double MajorRadius, double MinorRadius, Angle MajorAngle,
            out ModOp2D ToUnitCircle, out ModOp2D FromUnitCircle)
        {
            double s = Math.Sin(MajorAngle);
            double c = Math.Cos(MajorAngle);
            ToUnitCircle = new ModOp2D(c / MajorRadius, s / MajorRadius,
                (-c * Center.x - s * Center.y) / MajorRadius, -s / MinorRadius,
                c / MinorRadius, (s * Center.x - c * Center.y) / MinorRadius);
            FromUnitCircle = new ModOp2D(MajorRadius * c, -MinorRadius * s, Center.x,
                MajorRadius * s, MinorRadius * c, Center.y);
        }

        internal static GeoPoint2D[] IntersectEL(GeoPoint2D Center, double MajorRadius, double MinorRadius, Angle MajorAngle,
            GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            ModOp2D ToUnitCircle, FromUnitCircle;
            GetEllipseModOps(Center, MajorRadius, MinorRadius, MajorAngle, out ToUnitCircle, out FromUnitCircle);
            GeoPoint2D s = ToUnitCircle * StartPoint;
            GeoPoint2D e = ToUnitCircle * EndPoint;
            GeoPoint2D[] ip = new GeoPoint2D[2];
            GeoPoint2D[] res = IntersectLC(s, e, new GeoPoint2D(0.0, 0.0), 1.0);
            for (int i = 0; i < res.Length; ++i) res[i] = FromUnitCircle * res[i];
            return res;
        }

        internal static GeoPoint2D[] IntersectEC(GeoPoint2D CenterE, double MajorRadius, double MinorRadius, Angle MajorAngle,
                     GeoPoint2D CenterC, double Radius)
        {
            // zu begin muesste noch der tangentialfall getestet werden, weil in diesem
            // fall das ergebnis ziemlich ungenau ist. es ist aber recht schwierig
            // festzustellen ob kreis und ellipse tangential zueinander sind
            if (Math.Abs(MajorRadius) < 1e-10) return new GeoPoint2D[0];
            if (Math.Abs(MinorRadius) < 1e-10) return new GeoPoint2D[0];
            if (Math.Abs(1.0 - MajorRadius / MinorRadius) < 1e-8)
            { // es ist der schnittpunkt zweier kreise
                // der schnittpunkt mit einer ellipse, die fast ein kreis ist, fuehrt
                // zu sehr kleinen kooeffizienten bei x**4 in ragle4. das liefert evtl.
                // loesungen, wenn es in wirklichkeit keine gibt. deshalb hier
                // extrabehandlung
                return IntersectCC(CenterE, (MajorRadius + MinorRadius) / 2.0, CenterC, Radius);
            }
            else
            {
                double cw = Math.Cos(MajorAngle);
                double sw = Math.Sin(MajorAngle);
                double a = MajorRadius;
                double b = MinorRadius;
                // hm1 dreht und verschiebt so, dass die ellipse im ursprung liegt und die
                // hauptachse gleich der x-achse ist
                double x0 = cw * CenterC.x + sw * CenterC.y - cw * CenterE.x - sw * CenterE.y;
                double y0 = -sw * CenterC.x + cw * CenterC.y + sw * CenterE.x - cw * CenterE.y;
                double rr = sqr(Radius);

                double ccdist = Math.Sqrt(x0 * x0 + y0 * y0);
                if (ccdist < Radius * 1e-8)
                {
                    // konzentirsch
                    if (Math.Abs(MinorRadius - Radius) < Radius * 1e-8)
                    {
                        List<GeoPoint2D> res = new List<GeoPoint2D>();
                        // Berührung am minorRadius
                        double xx = 0;
                        double yy = Radius;
                        res.Add(new GeoPoint2D(xx * cw - yy * sw + CenterE.x, xx * sw + yy * cw + CenterE.y));
                        yy = -Radius;
                        res.Add(new GeoPoint2D(xx * cw - yy * sw + CenterE.x, xx * sw + yy * cw + CenterE.y));
                        return res.ToArray();
                    }
                    else if (Math.Abs(MajorRadius - Radius) < Radius * 1e-8)
                    {
                        List<GeoPoint2D> res = new List<GeoPoint2D>();
                        // Berührung am minorRadius
                        double xx = Radius;
                        double yy = 0;
                        res.Add(new GeoPoint2D(xx * cw - yy * sw + CenterE.x, xx * sw + yy * cw + CenterE.y));
                        xx = -Radius;
                        res.Add(new GeoPoint2D(xx * cw - yy * sw + CenterE.x, xx * sw + yy * cw + CenterE.y));
                        return res.ToArray();
                    }
                }
                // entspricht gleichungen (1) und (2) seite 157 c.d.g.
                bool exchange = Math.Abs(y0) < Math.Abs(x0);
                if (exchange)
                {
                    double h = a; a = b; b = h;
                    h = x0; x0 = y0; y0 = h;
                }
                double c = (1 - sqr(b / a)) / (2 * y0);
                double d = -x0 / y0;
                double e = (sqr(b) - rr + sqr(x0) + sqr(y0)) / (2 * y0);
                //double[] x = new double[4];
                //int m = ragle4(sqr(c), 2 * c * d, 2 * c * e + sqr(d) + sqr(b / a), 2 * d * e, sqr(e) - sqr(b), x, true);
                //double[] x = Geometry.QuarticRoots(sqr(c), 2 * c * d, 2 * c * e + sqr(d) + sqr(b / a), 2 * d * e, sqr(e) - sqr(b));
                // Geometry.QuarticNewtonApproximation(sqr(c), 2 * c * d, 2 * c * e + sqr(d) + sqr(b / a), 2 * d * e, sqr(e) - sqr(b), x);
                try
                {
                    List<double> roots = RealPolynomialRootFinder.FindRoots(sqr(c), 2 * c * d, 2 * c * e + sqr(d) + sqr(b / a), 2 * d * e, sqr(e) - sqr(b));
#if DEBUG
                    DebuggerContainer dc = new DebuggerContainer();
                    Circle2D c2d = new Circle2D(new GeoPoint2D(x0, y0), Radius);
                    dc.Add(c2d, System.Drawing.Color.Red, 0);
                    Ellipse2D e2d = new Ellipse2D(GeoPoint2D.Origin, a * GeoVector2D.XAxis, b * GeoVector2D.YAxis);
                    dc.Add(e2d, System.Drawing.Color.Blue, 1);
#endif
                    List<GeoPoint2D> res = new List<GeoPoint2D>();
                    for (int i = 0; i < roots.Count; ++i)
                    {
                        double w = roots[i];
                        if (exchange)
                        {
                            double xx = c * sqr(w) + d * w + e;

                            double w1 = Math.Atan2(w - x0, xx - y0);
                            double w2 = Math.Atan2(w / a, xx / b);

                            double a1 = Radius * Math.Cos(w1); // seite 46 analytische geometrie
                            double b1 = Radius * Math.Sin(w1); // tangente an den kreis im schnittpunkt
                            double c1 = rr + y0 * a1 + x0 * b1;

                            double a2 = Math.Cos(w2) / b; // seite 65 analytische geometrie, tangente an die ellipse
                            double b2 = Math.Sin(w2) / a; // c2 = 1

                            double det = a1 * b2 - a2 * b1;
                            xx = (c1 * b2 - b1) / det; // schnittpunkt der beiden tangenten
                            w = (a1 - a2 * c1) / det;

                            res.Add(new GeoPoint2D(xx * cw - w * sw + CenterE.x, xx * sw + w * cw + CenterE.y));
                        }
                        else
                        {
                            double y = c * sqr(w) + d * w + e; // y aus x bestimmen

                            double w1 = Math.Atan2(y - y0, w - x0);
                            double w2 = Math.Atan2(y / b, w / a);

                            double a1 = Radius * Math.Cos(w1); // seite 46 analytische geometrie
                            double b1 = Radius * Math.Sin(w1);
                            double c1 = rr + x0 * a1 + y0 * b1;

                            double a2 = Math.Cos(w2) / a; // seite 65 analytische geometrie
                            double b2 = Math.Sin(w2) / b; // c2 = 1

                            double det = a1 * b2 - a2 * b1;
                            w = (c1 * b2 - b1) / det; // schnittpunkt der beiden tangenten
                            y = (a1 - a2 * c1) / det;

                            res.Add(new GeoPoint2D(w * cw - y * sw + CenterE.x, w * sw + y * cw + CenterE.y));
                        }
                    }
                    if (res.Count == 0)
                    {   // maybe a tangential intersection
                        double u0 = Math.Atan2(y0, x0);
                        double f(double u)
                        {
                            return 2 * a * (x0 - a * Math.Cos(u)) * Math.Sin(u) - 2 * b * Math.Cos(u) * (y0 - b * Math.Sin(u));
                        }
                        double df(double u)
                        {
                            return 2 * a * a * sqr(Math.Sin(u)) + 2 * b * Math.Sin(u) * (y0 - b * Math.Sin(u)) + 2 * b * b * sqr(Math.Cos(u)) + 2 * a * Math.Cos(u) * (x0 - a * Math.Cos(u));
                        }
                        u0 = SimpleNewtonApproximation(f, df, u0);
                        if (u0 != double.MaxValue)
                        {
                            double xx = a * Math.Cos(u0);
                            double yy = b * Math.Sin(u0);
                            if (Math.Abs(sqr(xx - x0) + sqr(yy - y0) - sqr(Radius)) < 1e-4) res.Add(new GeoPoint2D(xx * cw - yy * sw + CenterE.x, xx * sw + yy * cw + CenterE.y));
                            xx = a * Math.Cos(u0 + Math.PI);
                            yy = b * Math.Sin(u0 + Math.PI);
                            if (Math.Abs(sqr(xx - x0) + sqr(yy - y0) - sqr(Radius)) < 1e-4) res.Add(new GeoPoint2D(xx * cw - yy * sw + CenterE.x, xx * sw + yy * cw + CenterE.y));
                        }
                    }
                    return res.ToArray();
                }
                catch (ApplicationException)
                {

                }
            }
            return new GeoPoint2D[0];
        }

        internal static bool IntersectSpherePlane(Plane pln, out GeoPoint2D center, out double radius)
        {
            // Kugel ist um den Ursprung mit Radius 1
            center = pln.Project(GeoPoint.Origin);
            GeoPoint center3d = pln.ToGlobal(center);
            double d = center3d | GeoPoint.Origin;
            if (d < 1.0)
            {
                radius = Math.Sqrt(1.0 - d * d);
                return true;
            }
            else
            {
                radius = 0.0;
                return false;
            }
        }
        internal static GeoPoint[] IntersectSphereLine(GeoPoint lstart, GeoVector ldir)
        {
            // Kugel ist um den Ursprung mit Radius 1
            // siehe Wikipedia: https://en.wikipedia.org/wiki/Line%E2%80%93sphere_intersection
            GeoVector l = ldir.Normalized;
            GeoVector o = lstart - GeoPoint.Origin;
            double a = l * l;
            double b = 2 * (l * o);
            double c = o * o - 1;
            double d1, d2;
            int n = quadgl(a, b, c, out d1, out d2);
            if (n == 0) return new GeoPoint[0];
            if (n == 1) return new GeoPoint[] { lstart + d1 * l };
            else return new GeoPoint[] { lstart + d1 * l, lstart + d2 * l };
        }
        internal static GeoPoint[] IntersectSphereEllipse(GeoPoint c, GeoVector xdir, GeoVector ydir)
        {
            // Einheitskugel mit der gegebenen Ellipse schneiden
            Plane pln = new Plane(c, xdir, ydir);
            GeoPoint fp = pln.ToGlobal(pln.Project(GeoPoint.Origin)); // Fußpunkt des Ursprungs
            double d = fp | GeoPoint.Origin;
            if (d > 1) return new GeoPoint[0];
            double r = Math.Sqrt(1 - d * d); // Radius des Schnittkreises der Ebene mit der Kugel
            GeoPoint2D[] ip2d = IntersectEC(GeoPoint2D.Origin, pln.Project(xdir).Length, pln.Project(ydir).Length, 0.0, pln.Project(fp), r);
            GeoPoint[] res = new GeoPoint[ip2d.Length];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = pln.ToGlobal(ip2d[i]);
            }
            return res;
        }

        internal static GeoPoint[] IntersectTorusLine(double ri, double ro, GeoPoint lstart, GeoVector ldir)
        {
            // ri ist der "kleine" radius, ro ist der große Radius
            // Torus um die Z-Achse
            double fRo2 = ro * ro;
            double fRi2 = ri * ri;
            double fDD = ldir * ldir;
            double fDE = lstart.ToVector() * ldir;
            double fVal = lstart.ToVector() * lstart.ToVector() - (fRo2 + fRi2);

            double e = fVal * fVal - 4.0 * fRo2 * (fRi2 - lstart.z * lstart.z);
            double d = 4.0 * fDE * fVal + 8.0 * fRo2 * ldir.z * lstart.z;
            double c = 2.0 * fDD * fVal + 4.0 * fDE * fDE + 4.0 * fRo2 * ldir.z * ldir.z;
            double b = 4.0 * fDD * fDE;
            double a = fDD * fDD;
            double[] x = new double[4];
            int n = ragle4(a, b, c, d, e, x);

            GeoPoint[] res = new GeoPoint[n];
            for (int i = 0; i < n; i++)
            {
                res[i] = lstart + x[i] * ldir;
            }
            return res;
        }
        internal static bool IntersectCylinderPlane(Plane pln, out GeoPoint2D center, out GeoVector2D majoraxis, out GeoVector2D minoraxis)
        {
            // Zylinder ist um den Ursprung mit Radius 1
            center = pln.Intersect(new Axis(GeoPoint.Origin, GeoVector.ZAxis));
            if (Precision.SameDirection(pln.Normal, GeoVector.ZAxis, false))
            {   // einfacher horizontaler Schnitt
                majoraxis = pln.Intersect(new Axis(new GeoPoint(1, 0, 0), GeoVector.ZAxis)) - center;
                minoraxis = pln.Intersect(new Axis(new GeoPoint(0, 1, 0), GeoVector.ZAxis)) - center;
                return true;
            }
            else
            {
                GeoVector2D mindir = (GeoVector.ZAxis ^ pln.Normal).To2D().Normalized;
                GeoVector2D majdir = mindir.ToRight(); // die beiden Achsen in der xy-Ebene mit Einheitslänge
                if (!majdir.IsNullVector())
                {
                    majoraxis = pln.Intersect(new Axis(new GeoPoint(majdir.x, majdir.y, 0), GeoVector.ZAxis)) - center;
                    minoraxis = pln.Intersect(new Axis(new GeoPoint(mindir.x, mindir.y, 0), GeoVector.ZAxis)) - center;
                    return true;
                }
            }
            // Ebene parallel zur Z-Achse
            majoraxis = GeoVector2D.NullVector;
            minoraxis = GeoVector2D.NullVector;
            return false;
        }

        internal static CADability.Curve2D.Ellipse2D Ellipse2P2T(GeoPoint2D p1, GeoPoint2D p2, GeoVector2D t1, GeoVector2D t2)
        {
            // gesucht ist die affine Abbildung des Einheitskreises, die (1,0)->p1, (0,1)->p2, (0,?)->t1 und (?,0)->t2 abbildet
            // ergibt 6 Gleichungen mit 6 unbekannten (für die Inverse Matrix)
            // das Ergebnis sind nicht die Hauptachsen
            // m00*p1x + m01*p1y + m02                           = 1.0
            //                           m10*p1x + m11*p1y + m12 = 0.0
            // m00*p2x + m01*p2y + m02                           = 0.0
            //                           m10*p2x + m11*p2y + m12 = 1.0
            // m00*t1x + m01*t1y                                 = 0.0
            //                           m10*t2x + m11*t2y       = 0.0
            Matrix a = DenseMatrix.OfArray(new double[,]{ { p1.x, p1.y,  1.0,  0.0,  0.0,  0.0 },
                            {  0.0,  0.0,  0.0, p1.x, p1.y,  1.0 },
                            { p2.x, p2.y,  1.0,  0.0,  0.0,  0.0 },
                            {  0.0,  0.0,  0.0, p2.x, p2.y,  1.0 },
                            { t1.x, t1.y,  0.0,  0.0,  0.0,  0.0 },
                            {  0.0,  0.0,  0.0, t2.x, t2.y,  0.0 } });
            Vector b = new DenseVector(new double[] { 1.0, 0.0, 0.0, 1.0, 0.0, 0.0 });
            Vector x = (Vector)a.Solve(b);
            if (x.IsValid())
            {
                ModOp2D m = new ModOp2D(x[0], x[1], x[2], x[3], x[4], x[5]);
                try
                {
                    m = m.GetInverse();
                }
                catch (ModOpException)
                {
                    return null;
                }
                return new CADability.Curve2D.Ellipse2D(m * GeoPoint2D.Origin, m * new GeoVector2D(1.0, 0.0), m * new GeoVector2D(0.0, 1.0));
            }
            else return null;
        }
        /// <summary>
        /// Calculates the principal axis (major and minor axis) of an ellipse
        /// given by its center and two arbitrary axis. Also calculates the 4 extrema 
        /// of the ellipse.
        /// </summary>
        /// <param name="center">Center of the Ellipse</param>
        /// <param name="axis1">First arbitrary axis</param>
        /// <param name="axis2">Second arbitrary axis</param>
        /// <param name="majorAxis">resulting major axis</param>
        /// <param name="minorAxis">resulting minor axis</param>
        /// <param name="left">resulting left extremum</param>
        /// <param name="right">resulting right extremum</param>
        /// <param name="bottom">resulting bottom extremum</param>
        /// <param name="top">resulting top extremum</param>
        internal static void PrincipalAxis(GeoPoint2D center, GeoVector2D axis1, GeoVector2D axis2, out GeoVector2D majorAxis, out GeoVector2D minorAxis, out GeoPoint2D left, out GeoPoint2D right, out GeoPoint2D bottom, out GeoPoint2D top, bool forceRightHanded)
        {
            // sog. Hauptachsentransformation: gesucht sind die beiden Hauptachsen, die 
            // senkrecht aufeinander stehen und die größte und kleinste Länge haben
            // die Hauptachsenvektoren
            double a, b, g1, g2;
            a = axis1.x * axis2.x + axis1.y * axis2.y;
            b = Geometry.sqr(axis2.x) + Geometry.sqr(axis2.y) - Geometry.sqr(axis1.x) - Geometry.sqr(axis1.y);
            g1 = 2 * a;
            g2 = b + Math.Sqrt(Geometry.sqr(b) + 4 * Geometry.sqr(a));
            if (Math.Abs(g1) + Math.Abs(g2) > 1e-12) // 1e-13 war in einem Fall zu klein
            {
                if (Math.Abs(g2) > Math.Abs(g1))
                {
                    a = Math.Sqrt(1 / (1 + Geometry.sqr(g1 / g2)));
                    b = -g1 * a / g2;
                }
                else
                {
                    b = Math.Sqrt(1 / (1 + Geometry.sqr(g2 / g1)));
                    a = -g2 * b / g1;
                }
                majorAxis = new GeoVector2D(a * axis1.x + b * axis2.x, a * axis1.y + b * axis2.y);
                minorAxis = new GeoVector2D(a * axis2.x - b * axis1.x, a * axis2.y - b * axis1.y);
            }
            else
            {
                // ansonsten sind die Achsen schon senkrecht
                majorAxis = axis1;
                minorAxis = axis2;
            }
            // es ist aber nicht garantiert, dass mainmajax größer ist als mainminax
            double majrad = majorAxis.Length;
            double minrad = minorAxis.Length;
            if (majrad < minrad)
            {	// vertauschen
                double tmp = minrad;
                minrad = majrad;
                majrad = tmp;
                GeoVector2D v = majorAxis;
                majorAxis = minorAxis;
                minorAxis = v;
            }
            // majorAxis, minorAxis sollen ein Rechtssystem bilden
            // Das mit dem rechtssystem scheint mir nicht begründet: es gibt 2D Ellipsen, die ein Linkssystem haben
            // und sie müssen es auch behalten, denn sonst stimmen 2D Curve und 3D Edge nicht überein.
            // Deshlab folgende Zeilen auskommentiert. (Ansonsten müsste man dafür sorgen, dass nie 
            // 2D Ellipsen  mit Linkssystem entstehen)
            // so geändert: es soll Rechtssystem sein, wenn axis1, axis2 Rectssystem ist und analog Linkssystem
            if ((Math.Sign(majorAxis.x * minorAxis.y - majorAxis.y * minorAxis.x) != Math.Sign(axis1.x * axis2.y - axis1.y * axis2.x)))
            {	// eine der beiden Achsen umdrehen, welche ist egal
                majorAxis.x = -majorAxis.x;
                majorAxis.y = -majorAxis.y;
            }
            if (forceRightHanded && (majorAxis.x * minorAxis.y - majorAxis.y * minorAxis.x) < 0.0)
            {   // Nachbrenner: unbedingt ein rechtssystem gewollt
                majorAxis.x = -majorAxis.x;
                majorAxis.y = -majorAxis.y;
            }
            Angle majdir = majorAxis.Angle;
            double majaxsin = Math.Sin(majdir);
            double majaxcos = Math.Cos(majdir);


            if (Math.Abs(majaxsin) < 1e-13)
            {	// die Ellipse befindet sich in horizontaler Lage
                right = new GeoPoint2D(center.x + majrad, center.y);
                left = new GeoPoint2D(center.x - majrad, center.y);
                top = new GeoPoint2D(center.x, center.y + minrad);
                bottom = new GeoPoint2D(center.x, center.y - minrad);
            }
            else if (Math.Abs(majaxcos) < 1e-13)
            {	// die Ellipse befindet sich in vertikaler Lage
                right = new GeoPoint2D(center.x + minrad, center.y);
                left = new GeoPoint2D(center.x - minrad, center.y);
                top = new GeoPoint2D(center.x, center.y + majrad);
                bottom = new GeoPoint2D(center.x, center.y - majrad);
            }
            else
            {
                GeoPoint2D[] p = new GeoPoint2D[4];
                double m1 = majaxcos / majaxsin;
                double m2 = -1 / m1;
                double a1 = Math.Sqrt(Geometry.sqr(minrad) + Geometry.sqr(majrad) * Geometry.sqr(m1));
                double a2 = Math.Sqrt(Geometry.sqr(minrad) + Geometry.sqr(majrad) * Geometry.sqr(m2));
                p[0].x = m1 * Geometry.sqr(majrad) / a1;
                p[0].y = m1 * p[0].x - (a1);
                p[1].x = -m1 * Geometry.sqr(majrad) / a1;
                p[1].y = m1 * p[1].x + (a1);
                p[2].x = m2 * Geometry.sqr(majrad) / a2;
                p[2].y = m2 * p[2].x - (a2);
                p[3].x = -m2 * Geometry.sqr(majrad) / a2;
                p[3].y = m2 * p[3].x + (a2);

                double xmin = double.MaxValue;
                double xmax = double.MinValue;
                double ymin = double.MaxValue;
                double ymax = double.MinValue;
                int ileft, iright, itop, ibottom;
                ileft = iright = itop = ibottom = 0;
                for (int i = 0; i < 4; ++i)
                {
                    p[i] = new GeoPoint2D(center.x + majaxcos * p[i].x - majaxsin * p[i].y, center.y + majaxsin * p[i].x + majaxcos * p[i].y);
                    if (p[i].x < xmin)
                    {
                        xmin = p[i].x;
                        ileft = i;
                    }
                    if (p[i].x > xmax)
                    {
                        xmax = p[i].x;
                        iright = i;
                    }
                    if (p[i].y < ymin)
                    {
                        ymin = p[i].y;
                        ibottom = i;
                    }
                    if (p[i].y > ymax)
                    {
                        ymax = p[i].y;
                        itop = i;
                    }
                }
                right = p[iright];
                left = p[ileft];
                top = p[itop];
                bottom = p[ibottom];
            }
        }
        internal static void PrincipalAxis(GeoVector2D axis1, GeoVector2D axis2, out GeoVector2D majorAxis, out GeoVector2D minorAxis)
        {
            // Hauptachsentransformation ohne extent
            double a, b, g1, g2;
            a = axis1.x * axis2.x + axis1.y * axis2.y;
            b = Geometry.sqr(axis2.x) + Geometry.sqr(axis2.y) - Geometry.sqr(axis1.x) - Geometry.sqr(axis1.y);
            g1 = 2 * a;
            g2 = b + Math.Sqrt(Geometry.sqr(b) + 4 * Geometry.sqr(a));
            if (Math.Abs(g1) + Math.Abs(g2) > 1e-13)
            {
                if (Math.Abs(g2) > Math.Abs(g1))
                {
                    a = Math.Sqrt(1 / (1 + Geometry.sqr(g1 / g2)));
                    b = -g1 * a / g2;
                }
                else
                {
                    b = Math.Sqrt(1 / (1 + Geometry.sqr(g2 / g1)));
                    a = -g2 * b / g1;
                }
                majorAxis = new GeoVector2D(a * axis1.x + b * axis2.x, a * axis1.y + b * axis2.y);
                minorAxis = new GeoVector2D(a * axis2.x - b * axis1.x, a * axis2.y - b * axis1.y);
            }
            else
            {
                // ansonsten sind die Achsen schon senkrecht
                majorAxis = axis1;
                minorAxis = axis2;
            }
        }
        internal static double[,] Minor(double[,] matrix, int ii, int jj)
        {
            int n = matrix.GetLength(0);
            if (matrix.GetLength(1) != n) throw new GeometryException("Minor: matrix must be square");
            double[,] res = new double[n - 1, n - 1];
            int iii = 0;
            for (int i = 0; i < n; ++i)
            {
                if (i != ii)
                {
                    int jjj = 0;
                    for (int j = 0; j < n; ++j)
                    {
                        if (j != jj)
                        {
                            res[iii, jjj] = matrix[i, j];
                            ++jjj;
                        }
                    }
                    ++iii;
                }
            }
            return res;
        }
        internal static double Determinant(double[,] matrix)
        {
            int n = matrix.GetLength(0);
            if (matrix.GetLength(1) != n) throw new GeometryException("Minor: matrix must be square");
            if (n == 2) return matrix[0, 0] * matrix[1, 1] - matrix[1, 0] * matrix[0, 1];
            // noch hinschreiben damit schneller: if (n==3) return matrix[0,0]*Matrix[1,1] - matirx[1,0]*matrix[0,1];
            double res = 0.0;
            for (int i = 0; i < n; ++i)
            {
                res += matrix[i, 0] * Determinant(Minor(matrix, i, 0));
            }
            return res;
        }

        internal static double[,] LUMatrix(double[,] a)
        {
            // eine Klasse LUMatrix machen, die auch noch pivot[], errval und sign enthält
            if (a.GetLength(0) != a.GetLength(1))
            {
                throw new GeometryException("Can only decompose square matrix");
            }
            int n = a.GetLength(0);
            double[,] res = (double[,])a.Clone();
            int nm1 = n - 1;

            int errval = 0;
            double[] pivot = new double[n];
            for (int k = 0; k < n; k++) pivot[k] = k;
            int sign = 1;
            if (nm1 >= 1)	// non-trivial problem
            {
                for (int k = 0; k < nm1; k++)
                {
                    int kp1 = k + 1;
                    double ten = Math.Abs(a[k, k]);
                    int l = k;
                    for (int i = kp1; i < n; i++)
                    {
                        double den = Math.Abs(a[i, k]);
                        if (den > ten)
                        {
                            ten = den;
                            l = i;
                        }
                    }
                    pivot[k] = l;
                    if (res[l, k] != 0.0)
                    {			// nonsingular pivot found 
                        if (l != k)
                        {	// interchange needed 
                            for (int i = k; i < n; i++)
                            {
                                double t = res[l, i];
                                res[l, i] = res[k, i];
                                res[k, i] = t;
                            }
                            sign = -sign;
                        }
                        double q = res[k, k];	// scale row
                        for (int i = kp1; i < n; i++)
                        {
                            double t = -res[i, k] / q;
                            res[i, k] = t;
                            for (int j = kp1; j < n; j++)
                                res[i, j] += t * res[k, j];
                        }
                    }
                    else		/* pivot singular */
                        errval = k;
                }
            }
            pivot[nm1] = nm1;
            if (res[nm1, nm1] == 0.0) errval = nm1;
            return res;
        }

        public static bool PointInsideTriangle(GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3, GeoPoint2D p)
        {
            bool OnLeftSidep2p3 = Geometry.OnLeftSide(p, p2, p3 - p2);
            if (Geometry.OnLeftSide(p, p1, p2 - p1) != OnLeftSidep2p3) return false;
            if (OnLeftSidep2p3 != Geometry.OnLeftSide(p, p3, p1 - p3)) return false;
            return true; // auf der gleichen Seite für alle drei Dreiecks-Seiten

        }

        public static bool PointInsideConvexPolygon(GeoPoint2D[] cp, GeoPoint2D p)
        {
#if DEBUG
            GeoObjectList dbg = new GeoObjectList();
            for (int i = 0; i < cp.Length; i++)
            {
                Line l = Line.Construct();
                l.SetTwoPoints(new GeoPoint(cp[i]), new GeoPoint(cp[(i + 1) % cp.Length]));
                dbg.Add(l);
            }
#endif
            bool OnLeftSidep2p3 = Geometry.OnLeftSide(p, cp[cp.Length - 1], cp[0] - cp[cp.Length - 1]);
            for (int i = 0; i < cp.Length - 1; i++)
            {
                if (Geometry.OnLeftSide(p, cp[i], cp[i + 1] - cp[i]) != OnLeftSidep2p3) return false;
            }
            return true;
        }

        public static int PointInsidePolygon(GeoPoint2D[] cp, GeoPoint2D p)
        {   // von http://geomalgorithms.com/a03-_inclusion.html
            int wn = 0;    // the  winding number counter

            // loop through all edges of the polygon
            for (int i = 0; i < cp.Length; i++)
            {   // edge from V[i] to  V[i+1]
                int i1 = i + 1;
                if (i1 == cp.Length) i1 = 0;
                if (cp[i].y <= p.y)
                {          // start y <= P.y
                    if (cp[i1].y > p.y)      // an upward crossing
                        if (OnLeftSide(p, cp[i], cp[i1]))  // P left of  edge
                            ++wn;            // have  a valid up intersect
                }
                else
                {                        // start y > P.y (no test needed)
                    if (cp[i1].y <= p.y)     // a downward crossing
                        if (!OnLeftSide(p, cp[i], cp[i1]))  // P right of  edge
                            --wn;            // have  a valid down intersect
                }
            }
            return wn;
        }

        /// <summary>
        /// Determins wheather an axis, defined by location and direction, goes through a triangle.
        /// The axis is infinite in both directions.
        /// </summary>
        /// <param name="axisLoc">Point on the axis</param>
        /// <param name="axisDir">Direction of the axis</param>
        /// <param name="t1">1. point of the triangle</param>
        /// <param name="t2">2. point of the triangle</param>
        /// <param name="t3">3. point of the triangle</param>
        /// <returns>true, if the axis passes through the triangle, false otherwise</returns>
        public static bool AxisThroughTriangle(GeoPoint axisLoc, GeoVector axisDir, GeoPoint t1, GeoPoint t2, GeoPoint t3)
        {
            // bestimmt den Schnittpunkt von Achse und Dreiecksebene als Skalarfaktoren zu t2 - t1, t3 - t1 und axisDir
            Matrix m = DenseMatrix.OfRowArrays(t2 - t1, t3 - t1, axisDir);
            Vector s = (Vector)m.Transpose().Solve(new DenseVector(axisLoc - t1));
            if (s.IsValid())
            {
                if (s[0] >= 0.0 && s[1] >= 0.0 && s[0] + s[1] <= 1) return true;
            }
            return false;
        }
        internal static bool AxisThroughTriangle(GeoPoint axisLoc, GeoVector axisDir, GeoPoint t1, GeoPoint t2, GeoPoint t3, out double a)
        {
            // bestimmt den Schnittpunkt von Achse und Dreiecksebene als Skalarfaktoren zu t2 - t1, t3 - t1 und axisDir
            Matrix m = DenseMatrix.OfRowArrays(t2 - t1, t3 - t1, axisDir);
            Vector s = (Vector)m.Transpose().Solve(new DenseVector(axisLoc - t1));
            if (s.IsValid())
            {
                a = s[2];
                if (s[0] >= 0.0 && s[1] >= 0.0 && s[0] + s[1] <= 1) return true;
            }
            a = 0.0;
            return false;
        }
        /// <summary>
        /// Determins wheather a beam, defined by location and direction, goes through a triangle.
        /// The beam starts at <paramref name="axisLoc"/> and extends in direction <paramref name="axisDir"/>. 
        /// Only the forward direction is checked.
        /// </summary>
        /// <param name="axisLoc">Startpoint of the beam</param>
        /// <param name="axisDir">Direction of the beam</param>
        /// <param name="t1">1. point of the triangle</param>
        /// <param name="t2">2. point of the triangle</param>
        /// <param name="t3">3. point of the triangle</param>
        /// <returns>true, if the beam passes through the triangle, false otherwise</returns>
        public static bool BeamThroughTriangle(GeoPoint axisLoc, GeoVector axisDir, GeoPoint t1, GeoPoint t2, GeoPoint t3)
        {
            Matrix m = DenseMatrix.OfRowArrays(t2 - t1, t3 - t1, axisDir);
            Vector s = (Vector)m.Transpose().Solve(new DenseVector(axisLoc - t1));
            if (s != null)
            {
                if (s[0] >= 0.0 && s[1] >= 0.0 && s[0] + s[1] <= 1 && s[2] >= 0.0) return true;
            }
            return false;
        }
        /// <summary>
        /// Returns true if the line segments p1->p2 and p3->p4 have an inner intersection, i.e. an intersection point inside
        /// the bounds of the line segment. Returns false otherwise, returns false if the intersection point coincides with
        /// one of the two lines
        /// </summary>
        /// <param name="p1">startpoint of the first line segment</param>
        /// <param name="p2">endpoint of the first line segment</param>
        /// <param name="p3">startpoint of the second line segment</param>
        /// <param name="p4">endpoint of the second line segment</param>
        /// <returns></returns>
        public static bool InnerIntersection(GeoPoint2D p1, GeoPoint2D p2, GeoPoint2D p3, GeoPoint2D p4)
        {
            if (OnSameSide(p1, p2, p3, p4)) return false;
            if (OnSameSide(p3, p4, p1, p2)) return false;
            return true;
        }

        private static double DistancePointEllipseSpecial(double dU, double dV, double dA,
            double dB, double dEpsilon, int iMax, ref int riIFinal,
            ref double rdX, ref double rdY)
        {
            // initial guess
            double dT = dB * (dV - dB);
            // Newton’s method
            int i;
            for (i = 0; i < iMax; i++)
            {
                double dTpASqr = dT + dA * dA;
                double dTpBSqr = dT + dB * dB;
                double dInvTpASqr = 1.0 / dTpASqr;
                double dInvTpBSqr = 1.0 / dTpBSqr;
                double dXDivA = dA * dU * dInvTpASqr;
                double dYDivB = dB * dV * dInvTpBSqr;
                double dXDivASqr = dXDivA * dXDivA;
                double dYDivBSqr = dYDivB * dYDivB;
                double dF = dXDivASqr + dYDivBSqr - 1.0;
                if (dF < dEpsilon)
                {
                    // F(t0) is close enough to zero, terminate the iteration
                    rdX = dXDivA * dA;
                    rdY = dYDivB * dB;
                    riIFinal = i;
                    break;
                }
                double dFDer = 2.0 * (dXDivASqr * dInvTpASqr + dYDivBSqr * dInvTpBSqr);
                double dRatio = dF / dFDer;
                if (dRatio < dEpsilon)
                {
                    // t1-t0 is close enough to zero, terminate the iteration
                    rdX = dXDivA * dA;
                    rdY = dYDivB * dB;
                    riIFinal = i;
                    break;
                }
                dT += dRatio;
            }
            if (i == iMax)
            {
                // method failed to converge, let caller know
                riIFinal = -1;
                return double.MinValue;
            }
            double dDelta0 = rdX - dU, dDelta1 = rdY - dV;
            return Math.Sqrt(dDelta0 * dDelta0 + dDelta1 * dDelta1);
        }
        /// <summary>
        /// Abstand Punkt Ellipse für ungederhte Ellipse mit x-Achse größer y-Achse aus
        /// http://www.geometrictools.com/Documentation/DistancePointToEllipse2.pdf
        /// kommt i.A. mit wenigen Iterationsschritten aus (maximal 7 bis jetzt gesehen
        /// </summary>
        /// <param name="dU"></param>
        /// <param name="dV"></param>
        /// <param name="dA"></param>
        /// <param name="dB"></param>
        /// <param name="dEpsilon"></param>
        /// <param name="iMax"></param>
        /// <param name="riIFinal"></param>
        /// <param name="rdX"></param>
        /// <param name="rdY"></param>
        /// <returns></returns>
        internal static double DistancePointEllipse(
            double dU, double dV, // test point (u,v)
            double dA, double dB, // ellipse is (x/a)^2 + (y/b)^2 = 1
            double dEpsilon, // zero tolerance for Newton’s method
            int iMax, // maximum iterations in Newton’s method
            ref int riIFinal, // number of iterations used
            ref double rdX, ref double rdY) // a closest point (x,y)
        {
            // special case of circle
            if (Math.Abs(dA - dB) < dEpsilon)
            {
                double dLength = Math.Sqrt(dU * dU + dV * dV);
                return Math.Abs(dLength - dA);
            }
            // reflect U = -U if necessary, clamp to zero if necessary
            bool bXReflect;
            if (dU > dEpsilon)
            {
                bXReflect = false;
            }
            else if (dU < -dEpsilon)
            {
                bXReflect = true;
                dU = -dU;
            }
            else
            {
                bXReflect = false;
                dU = 0.0;
            }
            // reflect V = -V if necessary, clamp to zero if necessary
            bool bYReflect;
            if (dV > dEpsilon)
            {
                bYReflect = false;
            }
            else if (dV < -dEpsilon)
            {
                bYReflect = true;
                dV = -dV;
            }
            else
            {
                bYReflect = false;
                dV = 0.0;
            }
            // transpose if necessary
            double dSave;
            bool bTranspose;
            if (dA >= dB)
            {
                bTranspose = false;
            }
            else
            {
                bTranspose = true;
                dSave = dA;
                dA = dB;
                dB = dSave;
                dSave = dU;
                dU = dV;
                dV = dSave;
            }
            double dDistance;
            if (dU != 0.0)
            {
                if (dV != 0.0)
                {
                    dDistance = DistancePointEllipseSpecial(dU, dV, dA, dB, dEpsilon, iMax, ref riIFinal, ref rdX, ref rdY);
                }
                else
                {
                    double dBSqr = dB * dB;
                    if (dU < dA - dBSqr / dA)
                    {
                        double dASqr = dA * dA;
                        rdX = dASqr * dU / (dASqr - dBSqr);
                        double dXDivA = rdX / dA;
                        rdY = dB * Math.Sqrt(Math.Abs(1.0 - dXDivA * dXDivA));
                        double dXDelta = rdX - dU;
                        dDistance = Math.Sqrt(dXDelta * dXDelta + rdY * rdY);
                        riIFinal = 0;
                    }
                    else
                    {
                        dDistance = Math.Abs(dU - dA);
                        rdX = dA;
                        rdY = 0.0;
                        riIFinal = 0;
                    }
                }
            }
            else
            {
                dDistance = Math.Abs(dV - dB);
                rdX = 0.0;
                rdY = dB;
                riIFinal = 0;
            }
            if (bTranspose)
            {
                dSave = rdX;
                rdX = rdY;
                rdY = dSave;
            }
            if (bYReflect)
            {
                rdY = -rdY;
            }
            if (bXReflect)
            {
                rdX = -rdX;
            }
            return dDistance;
        }
        //funktioniert noch nicht, auch wenn es kompiliert
        //
        //// needs two arrays of coefficients of the following form:
        ////  a20 x^2 + a02 y^2 + a11 xy + a10 x + a01 y + a00
        ////  b10 x + b01 y + b00
        //public static List<double[]> EquationsVar2Deg21(List<double[]> d)
        //{
        //    double a20 = d[0][0]; double a11 = d[0][2]; double a10 = d[0][3];
        //    double a02 = d[0][1]; double a01 = d[0][4]; double a00 = d[0][5];

        //    double b10 = d[1][0]; double b01 = d[1][2]; double b00 = d[1][3];

        //    double h0 = a02 * b00 * b00 - a01 * b00 * b01 + a00 * b01 * b01;
        //    double h1 = a10 * b01 * b01 + 2 * a02 * b00 * b10 - a11 * b00 * b01 - a01 * b10 * b01;
        //    double h2 = a20 * b01 * b01 - a11 * b10 * b01 + a02 * b10 * b10;

        //    double x1, x2;
        //    int n = quadgl(h2, h1, h0, out x1, out x2);
        //    double[] xVal = new double[n];
        //    if (n != 0) xVal[0] = x1;
        //    if (n == 2) xVal[1] = x2;
        //    List<double[]> erg = new List<double[]>(n);
        //    for (int i = 0; i < n; ++i)
        //    {
        //        double x = xVal[i];
        //        double[] h = {x, -(b00 + b10 * x) / b01};
        //        erg.Add(h);
        //    }
        //    return erg;
        //}
        //// needs two arrays of coefficients of the following form:
        ////  a20 x^2 + a02 y^2 + a11 xy + a10 x + a01 y + a00
        ////  b20 x^2 + b02 y^2 + b11 xy + b10 x + b01 y + b00
        //public static List<double[]> EquationsVar2Deg22(List<double[]> d)
        //{
        //    double a20 = d[0][0]; double a11 = d[0][2]; double a10 = d[0][3];
        //    double a02 = d[0][1]; double a01 = d[0][4]; double a00 = d[0][5];

        //    double b20 = d[1][0]; double b11 = d[1][2]; double b10 = d[1][3];
        //    double b02 = d[1][1]; double b01 = d[1][4]; double b00 = d[1][5];

        //    double d00 = a20 * b10 - b20 * a10;
        //    double d01 = a20 * b11 - b20 * a11;
        //    double d10 = a10 * b00 - b10 * a00;
        //    double d11 = a11 * b00 + a10 * b01 - b11 * a00 - b10 * a01;
        //    double d12 = a11 * b01 + a10 * b02 - b11 * a01 - b10 * a02;
        //    double d13 = a11 * b02 - b11 * a02;
        //    double d20 = a20 * b00 - b20 * a00;
        //    double d21 = a20 * b01 - b20 * a01;
        //    double d22 = a20 * b02 - b20 * a02;

        //    double h0 = d00 * d10 - d20 * d20;
        //    double h1 = d01 * d10 + d00 * d11 - 2 * d20 * d21;
        //    double h2 = d01 * d11 + d00 * d12 - d21 * d21 - 2 * d20 * d22;
        //    double h3 = d01 * d12 + d00 * d13 - 2 * d21 * d22;
        //    double h4 = d01 * d13 - d22 * d22;

        //    double[] s = new double[4];
        //    int n = Geometry.ragle4(h4, h3, h2, h1, h0, s);
        //    double[] yVal = new double[n];
        //    for (int i = 0; i < n; i++)
        //    {
        //        yVal[i] = s[i];
        //    }
        //    List<double[]> erg = new List<double[]>(2 * n);
        //    for (int i = 0; i < n; ++i)
        //    {
        //        double y = yVal[i];
        //        double x1, x2;
        //        int m = quadgl(a20, a10 + a11 * y, a00 + a01 * y + a02 * y * y, out x1, out x2);
        //        double[] xArr = { x1, x2 };
        //        for (int j = 0; j < m; ++j)
        //        {
        //            double x = xArr[j];
        //            if (Math.Abs(b20 * x * x + b10 * x + b11 * x * y + b00 + b01 * y + b02 * y * y) < Precision.eps)
        //            {
        //                double[] e = { x, y };
        //                erg.Add(e);
        //            }
        //        }
        //    }
        //    return erg;
        //}
        //// needs three arrays of coefficients of the following form:
        ////  a200 x^2 + a020 y^2 + a002 z^2 + a110 xy + a011 yz + a101 xz + a100 x + a010 y + a001 z + a000
        ////  b100 x + b010 y + b001 z + b000
        ////  c100 x + c010 y + c001 z + c000
        //public static List<double[]> EquationsVar3Deg211(List<double[]> d)
        //{
        //    double a200 = d[0][0]; double a020 = d[0][1]; double a002 = d[0][2];
        //    double a110 = d[0][3]; double a011 = d[0][4]; double a101 = d[0][5];
        //    double a100 = d[0][6]; double a010 = d[0][7]; double a001 = d[0][8];
        //    double a000 = d[0][9];

        //    double b100 = d[1][0]; double b010 = d[1][1]; double b001 = d[1][2];
        //    double b000 = d[1][3];

        //    double c100 = d[2][0]; double c010 = d[2][1]; double c001 = d[2][2];
        //    double c000 = d[2][3];

        //    double d20 = a200 * b010 * b010 - b100 * (a110 * b010 + b100 * a020);
        //    double d11 = 2 * a200 * b010 * b001 - b100 * (a110 * b001 + a101 * b010 + b100 * a011);
        //    double d02 = a200 * b001 * b001 - b100 * (a101 * b001 + b100 * a002);
        //    double d10 = 2 * a200 * b010 * b000 - b100 * (a110 * b000 + a100 * b010 + b100 * a010);
        //    double d01 = 2 * a200 * b001 * b000 - b100 * (a101 * b000 + a100 * b001 + b100 * a001);
        //    double d00 = a200 * b000 * b000 - b100 * (a100 * b000 + b100 * a000);

        //    double e10 = b010 * c100 - c010 * b100;
        //    double e01 = b001 * c100 - c001 * b100;
        //    double e00 = b000 * c100 - c000 * b100;

        //    double[] d1 = {d20,d02,d11,d10,d01,d00};
        //    double[] d2 = {e10,e01,e00};

        //    List<double[]> help = new List<double[]>(2);
        //    help.Add(d1); help.Add(d2);
        //    List<double[]> h = EquationsVar2Deg21(help);
        //    List<double[]> erg = new List<double[]>(2 * h.Count);
        //    for (int i = 0; i < h.Count; ++i) 
        //    {
        //        double y = h[i][0];
        //        double z = h[i][1];
        //        double h0 = b100;
        //        double h1 = b000 + b010 * y + b001 * z;
        //        double x = -h1/h0;
        //        double[] g = { x, y, z };
        //    }
        //    return erg;
        //}
        //// needs three arrays of coefficients of the following form:
        ////  a200 x^2 + a020 y^2 + a002 z^2 + a110 xy + a011 yz + a101 xz + a100 x + a010 y + a001 z + a000
        ////  b200 x^2 + b020 y^2 + b002 z^2 + b110 xy + b011 yz + b101 xz + b100 x + b010 y + b001 z + b000
        ////  c100 x + c010 y + c001 z + c000
        //public static List<double[]> EquationsVar3Deg221(List<double[]> d)
        //{
        //    double a200 = d[0][0]; double a020 = d[0][1]; double a002 = d[0][2];
        //    double a110 = d[0][3]; double a011 = d[0][4]; double a101 = d[0][5];
        //    double a100 = d[0][6]; double a010 = d[0][7]; double a001 = d[0][8];
        //    double a000 = d[0][9];

        //    double b200 = d[1][0]; double b020 = d[1][1]; double b002 = d[1][2];
        //    double b110 = d[1][3]; double b011 = d[1][4]; double b101 = d[1][5];
        //    double b100 = d[1][6]; double b010 = d[1][7]; double b001 = d[1][8];
        //    double b000 = d[1][9];

        //    double c100 = d[2][0]; double c010 = d[2][1]; double c001 = d[2][2];
        //    double c000 = d[2][3];

        //    double d20 = a200 * c010 * c010 - c100 * (a110 * c010 + c100 * a020);
        //    double d11 = 2 * a200 * c010 * c001 - c100 * (a110 * c001 + a101 * c010 + c100 * a011);
        //    double d02 = a200 * c001 * c001 - c100 * (a101 * c001 + c100 * a002);
        //    double d10 = 2 * a200 * c010 * c000 - c100 * (a110 * c000 + a100 * c010 + c100 * a010);
        //    double d01 = 2 * a200 * c001 * c000 - c100 * (a101 * c000 + a100 * c001 + c100 * a001);
        //    double d00 = a200 * c000 * c000 - c100 * (a100 * c000 + c100 * a000);

        //    double e20 = b200 * c010 * c010 - c100 * (b110 * c010 + c100 * b020);
        //    double e11 = 2 * b200 * c010 * c001 - c100 * (b110 * c001 + b101 * c010 + c100 * b011);
        //    double e02 = b200 * c001 * c001 - c100 * (b101 * c001 + c100 * b002);
        //    double e10 = 2 * b200 * c010 * c000 - c100 * (b110 * c000 + b100 * c010 + c100 * b010);
        //    double e01 = 2 * b200 * c001 * c000 - c100 * (b101 * c000 + b100 * c001 + c100 * b001);
        //    double e00 = b200 * c000 * c000 - c100 * (b100 * c000 + c100 * b000);

        //    double[] d1 = { d20, d02, d11, d10, d01, d00 };
        //    double[] d2 = { e20, e02, e11, e10, e01, e00 };

        //    List<double[]> help = new List<double[]>(2);
        //    help.Add(d1); help.Add(d2);
        //    List<double[]> h = EquationsVar2Deg22(help);
        //    List<double[]> erg = new List<double[]>(2 * h.Count);
        //    for (int i = 0; i < h.Count; ++i)
        //    {
        //        double y = h[i][0];
        //        double z = h[i][1];
        //        double x1, x2;
        //        double h00 = a200;
        //        double h01 = a100 + a110 * y + a101 * z;
        //        double h02 = a000 + a010 * y + a001 * z + a011 * y * z + a020 * y * y + a002 * z * z;
        //        double h10 = b200;
        //        double h11 = b100 + b110 * y + b101 * z;
        //        double h12 = b000 + b010 * y + b001 * z + b011 * y * z + b020 * y * y + b002 * z * z;
        //        double h20 = c100;
        //        double h21 = c000 + c010 * y + c001 * z;
        //        int n = quadgl(h00, h01, h02, out x1, out x2);
        //        double[] xVal = new double[n];
        //        if (n != 0) xVal[0] = x1;
        //        if (n == 2) xVal[1] = x2;
        //        for (int j = 0; j < n; ++j)
        //        {
        //            double x = xVal[j];
        //            double[] h1 = { x, y, z };
        //            if (Math.Abs(h10 * x * x + h11 * x + h12) < Precision.eps
        //             && Math.Abs(h20 * x + h21) < Precision.eps)
        //                erg.Add(h1);
        //        }
        //    }
        //    return erg;
        //}
        /// <summary>
        /// tries to find a center and radius for a circle which best fits to the provided points
        /// </summary>
        /// <param name="points">points to fit</param>
        /// <param name="center">center of the circle</param>
        /// <param name="radius">radius of the circle</param>
        /// <returns>maximum error</returns>
        public static double CircleFit(GeoPoint2D[] points, out GeoPoint2D center, out double radius)
        {   
            // aus GeometricTools, siehe dort auch "Wm4ApprQuadraticFit2.h" für besseren Startwert

            // initial guess
            // Startwert hier als Schnittpunkt der Mittelsenkrechten
            GeoPoint2D cnt = new GeoPoint2D(points[0], points[1]);
            GeoVector2D dir = (points[1] - points[0]).ToLeft();
            double x = 0.0;
            double y = 0.0;
            int n = 0;
            for (int i = 1; i < points.Length - 1; i++)
            {
                GeoPoint2D cnt1 = new GeoPoint2D(points[i], points[i + 1]);
                GeoVector2D dir1 = (points[i + 1] - points[i]).ToLeft();
                GeoPoint2D ip;
                if (IntersectLL(cnt, dir, cnt1, dir1, out ip))
                {
                    x += ip.x;
                    y += ip.y;
                    ++n;
                }
                cnt = cnt1;
                dir = dir1;
            }
            if (n > 0)
            {   // Durchschnitt der Mittelsenkrechten Schnitte
                center = new GeoPoint2D(x / n, y / n);
            }
            else
            {   // oder als Schwerpunkt aller Punkte 
                center = new GeoPoint2D(points[0]);
            }
            GeoPoint2D average = center;

            radius = 0.0; // nur für Compiler
            double rad = 0.0;
            for (int i = 0; i < points.Length; i++)
            {
                rad += points[i] | center;
            }
            radius = rad / points.Length;
            double invQuantity = 1.0 / points.Length;

            //for (int i1 = 0; i1 < maxIterations; i1++)
            //{
            //    // update the iterates
            //    GeoPoint2D current = center;
            //    //double rad = 0.0;
            //    //for (int i = 0; i < points.Length; i++)
            //    //{
            //    //    rad += points[i] | center;
            //    //}
            //    //rad /= points.Length;
            //    //GeoVector2D corr = GeoVector2D.NullVector;
            //    //for (int i = 0; i < points.Length; i++)
            //    //{
            //    //    GeoVector2D dd = points[i] - center;
            //    //    double dl = dd.Length - rad;
            //    //    corr += dl * dd.Normalized;
            //    //}
            //    //center = center + corr;
            //    //radius = rad;

            //    //// compute average L, dL/da, dL/db
            //    double lAverage = 0.0;
            //    GeoVector2D derLAverage = GeoVector2D.NullVector;
            //    GeoVector2D diff;
            //    for (int i0 = 0; i0 < points.Length; i0++)
            //    {
            //        diff = points[i0] - center;
            //        double length = diff.Length;
            //        if (length > Precision.eps)
            //        {
            //            lAverage += length;
            //            double invLength = (1.0) / length;
            //            derLAverage -= invLength * diff;
            //        }
            //    }
            //    lAverage *= invQuantity;
            //    derLAverage = invQuantity * derLAverage;

            //    center = average + lAverage * derLAverage;
            //    radius = lAverage;

            //    if (Precision.IsEqual(center, current)) break;
            //}

            double res = 0.0;
            for (int i = 0; i < points.Length; i++)
            {
                double d = Math.Abs((center | points[i]) - radius);
                if (d > res) res = d;
            }
            return res;
        }
        public static double CircleFitLs(GeoPoint2D[] p, out GeoPoint2D c, out double r)
        {   // siehe: http://www.had2know.com/academics/best-fit-circle-least-squares.html
            // kommt ohne Iterationen aus, liefert direkt das Ergebnis mit least squre
#if DEBUG
            //Polyline2D p2d = new Polyline2D(p);
#endif
            Matrix m = new DenseMatrix(3, 3);
            Vector b = new DenseVector(3);
            for (int i = 0; i < p.Length; i++)
            {
                double s = p[i].x * p[i].x + p[i].y * p[i].y;
                b[0] += p[i].x * s;
                b[1] += p[i].y * s;
                b[2] += s;
                s = p[i].x * p[i].y;
                m[0, 0] += p[i].x * p[i].x;
                m[0, 1] += s;
                m[0, 2] += p[i].x;
                m[1, 0] += s;
                m[1, 1] += p[i].y * p[i].y;
                m[1, 2] += p[i].y;
                m[2, 0] += p[i].x;
                m[2, 1] += p[i].y;
            }
            m[2, 2] = p.Length;
            Vector slv = (Vector)m.Transpose().Solve(b);
            if (slv.IsValid())
            {
                c.x = slv[0] / 2.0;
                c.y = slv[1] / 2.0;
                double rt = 4 * slv[2] + slv[0] * slv[0] + slv[1] * slv[1];
                if (rt >= 0)
                {
                    r = Math.Sqrt(rt) / 2.0;
                    double res = 0.0;
                    for (int i = 0; i < p.Length; i++)
                    {
                        res += Math.Abs((p[i].x - c.x) * (p[i].x - c.x) + (p[i].y - c.y) * (p[i].y - c.y) - r * r);
                    }
                    return res;
                }
            }
            c = GeoPoint2D.Origin;
            r = 0;
            return double.MaxValue;
        }

        public static double SphereFit(GeoPoint[] points, out GeoPoint center, out double radius)
        {   // see GeometricTools: GTE\Mathematics\ApprCylinder3.h
            GeoPoint s = new GeoPoint(points);

            double M00 = 0.0, M01 = 0.0, M02 = 0.0, M11 = 0.0, M12 = 0.0, M22 = 0.0;
            GeoVector R = GeoVector.NullVector;
            for (int i = 0; i < points.Length; ++i)
            {
                GeoVector Y = points[i] - s;
                double Y0Y0 = Y[0] * Y[0];
                double Y0Y1 = Y[0] * Y[1];
                double Y0Y2 = Y[0] * Y[2];
                double Y1Y1 = Y[1] * Y[1];
                double Y1Y2 = Y[1] * Y[2];
                double Y2Y2 = Y[2] * Y[2];
                M00 += Y0Y0;
                M01 += Y0Y1;
                M02 += Y0Y2;
                M11 += Y1Y1;
                M12 += Y1Y2;
                M22 += Y2Y2;
                R += (Y0Y0 + Y1Y1 + Y2Y2) * Y;
            }
            R = 0.5 * R;

            double cof00 = M11 * M22 - M12 * M12;
            double cof01 = M02 * M12 - M01 * M22;
            double cof02 = M01 * M12 - M02 * M11;
            double det = M00 * cof00 + M01 * cof01 + M02 * cof02;
            if (det != 0.0)
            {
                double cof11 = M00 * M22 - M02 * M02;
                double cof12 = M01 * M02 - M00 * M12;
                double cof22 = M00 * M11 - M01 * M01;
                center = new GeoPoint();
                center[0] = s[0] + (cof00 * R[0] + cof01 * R[1] + cof02 * R[2]) / det;
                center[1] = s[1] + (cof01 * R[0] + cof11 * R[1] + cof12 * R[2]) / det;
                center[2] = s[2] + (cof02 * R[0] + cof12 * R[1] + cof22 * R[2]) / det;
                double rsqr = 0.0;
                for (int i = 0; i < points.Length; ++i)
                {
                    GeoVector delta = points[i] - center;
                    rsqr += delta * delta;
                }
                rsqr /= points.Length;
                radius = Math.Sqrt(rsqr);
                double error = 0.0;
                for (int i = 0; i < points.Length; i++)
                {
                    error += Math.Abs((points[i] | center) - radius);
                }
                return error;
            }
            else
            {
                radius = double.MaxValue;
                center = GeoPoint.Invalid;
                return double.MaxValue;
            }
        }
        public static double LineFit(GeoPoint2D[] points, out GeoPoint2D location, out GeoVector2D direction)
        {   // nach http://stackoverflow.com/questions/2352256/fit-a-3d-line-to-3d-point-data-in-java
            if (points.Length < 3) throw new ApplicationException("Invalid parameter at LineFit: points must contain more than two points");
            Matrix P = new DenseMatrix(points.Length, 2);
            for (int i = 0; i < points.Length; i++)
            {
                P[i, 0] = points[i].x;
                P[i, 1] = points[i].y;
            }
            Svd<double> svd = (P.Transpose() * P).Svd();
            int maxind = 0;
            for (int i = 1; i < 2; i++)
            {
                if (svd.S[i] > svd.S[maxind]) maxind = i;
            }
            direction = new GeoVector2D(svd.VT[0, maxind], svd.VT[1, maxind]);
            location = new GeoPoint2D(points);
            double d = 0.0;
            double dl = direction.Length;
            if (dl > 0.0)
            {
                for (int i = 0; i < points.Length; i++)
                {
                    d = Math.Max(((new GeoVector(points[i] - location)) ^ new GeoVector(direction)).Length / dl, d);
                }
                return d;
            }
            return double.MaxValue;
        }
        public static double LineFit(GeoPoint[] points, out GeoPoint location, out GeoVector direction)
        {   // nach http://stackoverflow.com/questions/2352256/fit-a-3d-line-to-3d-point-data-in-java
            // better use GaussNewtonMinimizer.LineFit
            if (points.Length < 3) throw new ApplicationException("Invalid parameter at LineFit: points must contain more than two points");
            Matrix P = new DenseMatrix(points.Length, 3);
            for (int i = 0; i < points.Length; i++)
            {
                P[i, 0] = points[i].x;
                P[i, 1] = points[i].y;
                P[i, 2] = points[i].z;
            }
            Svd<double> svd = (P.Transpose() * P).Svd();
            int maxind = 0;
            for (int i = 1; i < 3; i++)
            {
                if (svd.S[i] > svd.S[maxind]) maxind = i;
            }
            direction = new GeoVector(svd.VT[0, maxind], svd.VT[1, maxind], svd.VT[2, maxind]);
            location = new GeoPoint(points);
            double d = 0.0;
            double dl = direction.Length;
            if (dl > 0.0)
            {
                for (int i = 0; i < points.Length; i++)
                {
                    d = Math.Max(((points[i] - location) ^ direction).Length / dl, d);
                    double dd = Geometry.DistPL(points[i], location, direction);
                }
                return d;
            }
            return double.MaxValue;
        }
        public static GeoPoint2D GetPosition(GeoPoint2D p, GeoPoint2D sysLoc, GeoVector2D sysDirx, GeoVector2D sysDiry)
        {
            Matrix m = DenseMatrix.OfRowArrays(sysDirx, sysDiry);
            Vector s = (Vector)m.Transpose().Solve(new DenseVector(p - sysLoc));
            if (s.IsValid())
            {
                return new GeoPoint2D(s[0], s[1]);
            }
            else throw new GeometryException("no solution: linear dependant coordinate system");
        }
        /// <summary>
        /// Computes the vector <paramref name="toRebase"/> in the base given by <paramref name="sysDirx"/>, <paramref name="sysDiry"/> and <paramref name="sysDirz"/>.
        /// </summary>
        /// <param name="toRebase"></param>
        /// <param name="sysDirx"></param>
        /// <param name="sysDiry"></param>
        /// <param name="sysDirz"></param>
        /// <returns></returns>
        public static GeoVector ReBase(GeoVector toRebase, GeoVector sysDirx, GeoVector sysDiry, GeoVector sysDirz)
        {
            return new GeoVector(toRebase * sysDirx / sysDirx.LengthSqared, toRebase * sysDiry / sysDiry.LengthSqared, toRebase * sysDirz / sysDirz.LengthSqared);
            //Matrix m = new Matrix(sysDirx, sysDiry, sysDirz);
            //Matrix s = m.SaveSolveTranspose(new Matrix(toRebase));
            //if (s != null)
            //{
            //    return new GeoVector(s[0, 0], s[1, 0], s[2, 0]);
            //}
            //else throw new GeometryException("no solution: linear dependent coordinate system");
        }
        public static GeoVector ReBaseX(GeoVector toRebase, GeoVector sysDirx, GeoVector sysDiry, GeoVector sysDirz)
        {
            Matrix m = DenseMatrix.OfColumnArrays(sysDirx, sysDiry, sysDirz);
            Vector<double> s = m.Solve(new DenseVector(toRebase));
            if (s != null)
            {
                return new GeoVector(s[0], s[1], s[2]);
            }
            else throw new GeometryException("no solution: linear dependent coordinate system");
        }
        public static GeoVector2D Dir2D(GeoVector dirx, GeoVector diry, GeoVector toConvert)
        {
            ModOp fromUnitPlane = new ModOp(dirx, diry, dirx ^ diry, GeoPoint.Origin);
            ModOp toUnitPlane = fromUnitPlane.GetInverse();
            return new GeoVector2D(toUnitPlane * toConvert);
        }
        internal static void LineExtrema(GeoPoint2D[] points, GeoPoint2D loc, GeoVector2D dir, out GeoPoint2D sp, out GeoPoint2D ep)
        {
            double pmin = double.MaxValue;
            double pmax = double.MinValue;
            sp = GeoPoint2D.Origin;
            ep = GeoPoint2D.Origin;
            for (int i = 0; i < points.Length; i++)
            {
                double p = LinePar(loc, dir, points[i]);
                if (p < pmin)
                {
                    pmin = p;
                    sp = DropPL(points[i], loc, dir);
                }
                if (p > pmax)
                {
                    pmax = p;
                    ep = DropPL(points[i], loc, dir);
                }
            }
        }
        internal delegate double SimpleFunction(double parameter);
        internal delegate void FunctionAndDerivation(double parameter, out double val, out double deriv);
        internal static double GetMinimum(double parstart, double parend, int startProbes, SimpleFunction func)
        {   // eine einfache Minimum Funktion, die das Minimum einer unbekannten Funktion von R->R bestimmt.
            // Ausgehend von einem Parameterintervall werden anfänglich "startProbes" Stellen der Funktion berechnet.
            // zu jedem lokalen Minimum der so entstandenen "Polygonfunktion" werden die beiden angrenzenden Intervalle
            // betrachtet. In den Intervallen werden die Mittelpunkte berechnet und nun vom bisherigen Minimum und den beiden
            // neuen Wertender kleinste gesucht. Der bestimmt nun die beiden Intervalle zum Weitersuchen.
            // Von der Funktion "func" sind keine Ableitungen notwendig. Minima am Rand (parstart, parend) oder welche die mit
            // startProbes nicht erkannt werden, werden nicht gefunden
            if (startProbes < 3) startProbes = 3;
            double[] par = new double[startProbes];
            double[] val = new double[startProbes];
            for (int i = 0; i < startProbes; i++)
            {
                double p = parstart + i * (parend - parstart) / (startProbes - 1);
                par[i] = p;
                val[i] = func(p);
            }
            // für jedes lokale Minimum konvergieren
            double res = double.MaxValue;
            double minval = double.MaxValue;
            for (int i = 0; i < startProbes - 2; i++)
            {
                if (val[i + 1] < val[i] && val[i + 1] < val[i + 2])
                {
                    // par[i+1] ist ein lokales Minimum
                    double p0 = par[i];
                    double p1 = par[i + 2];
                    double pm = par[i + 1];
                    double v0 = val[i];
                    double v1 = val[i + 2];
                    double vm = val[i + 1];
                    while ((p1 - p0) > (parend - parstart) * 1e-8)
                    {   // einfacher Abbruch, Intervall wird in jedem Schritt halbiert
                        double p0m = (p0 + pm) / 2;
                        double p1m = (p1 + pm) / 2;
                        double v0m = func(p0m);
                        double v1m = func(p1m);
                        // die zwei schlechtesten rauswerfen
                        if (v1m < vm)
                        {   // die drei rechten, v1 bleibt
                            v0 = vm;
                            p0 = pm;
                            vm = v1m;
                            pm = p1m;
                        }
                        else if (v0m < vm)
                        {   // die drei linken, v0 bleibt
                            v1 = vm;
                            p1 = pm;
                            vm = v0m;
                            pm = p0m;
                        }
                        else
                        {   // die drei mittleren
                            v1 = v1m;
                            p1 = p1m;
                            v0 = v0m;
                            p0 = p0m;
                        }
                    }
                    if (vm < minval)
                    {
                        minval = vm;
                        res = pm;
                    }
                }
            }
            return res;
        }
#if DEBUG
        public static CADability.GeoObject.Path ApproxArc(CADability.GeoObject.Polyline polyline)
        {
            Plane pln = polyline.GetPlane();
            Polyline2D pl2d = polyline.GetProjectedCurve(pln) as Polyline2D;
            GeoPoint2D[] vertex = pl2d.Vertex;
            int m = vertex.Length / 2;
            GeoVector2D startdir = vertex[m + 1] - vertex[m - 1];
            Angle a = startdir.Angle;
            double min = GetMinimum(-Math.PI / 2.0, Math.PI / 2.0, 10,
                delegate (double p)
                {
                    GeoVector2D dir = new GeoVector2D(a + p);
                    Path2D p2d = MakePathWithArcs(vertex, m, dir);
                    //return p2d.Length;
                    // Summe der Fehler, besser wäre noch die Segmentfläche!
                    double d = 0.0;
                    //for (int i = 0; i < p2d.SubCurvesCount; i++)
                    //{
                    //    d += p2d.SubCurves[i].PointAt(0.5) | new GeoPoint2D(vertex[i], vertex[i + 1]);
                    //}
                    for (int i = 0; i < p2d.SubCurvesCount; i++)
                    {
                        if (p2d.SubCurves[i] is Arc2D)
                        {   // Linien sind exakt
                            d += (p2d.SubCurves[i] as Arc2D).SegmentArea;
                        }
                    }
                    return d;
                }
            );
            if (min < double.MaxValue)
            {
                Path2D res2d = MakePathWithArcs(vertex, m, new GeoVector2D(a + min));
                return res2d.MakeGeoObject(pln) as CADability.GeoObject.Path;
            }
            return null;
        }

        private static ICurve2D MakeArc(GeoPoint2D startPoint, GeoVector2D startDirection, GeoPoint2D endPoint)
        {
            GeoPoint2D center;
            GeoPoint2D middle = new GeoPoint2D(startPoint, endPoint);
            GeoVector2D middledir = (endPoint - startPoint).ToLeft();
            if (IntersectLL(startPoint, startDirection.ToLeft(), middle, middledir, out center))
            {
                bool counterClock = OnLeftSide(endPoint, startPoint, startDirection);
                Arc2D arc = new Arc2D(center, startPoint | center, startPoint, endPoint, counterClock);
                return arc;
            }
            else
            {
                Line2D l2d = new Line2D(startPoint, endPoint);
                return l2d;
            }
        }

        private static Path2D MakePathWithArcs(GeoPoint2D[] vertex, int m, GeoVector2D startdir)
        {
            List<ICurve2D> curves = new List<ICurve2D>();
            GeoVector2D dir = -startdir;
            for (int i = m; i > 0; i--)
            {
                ICurve2D c2d = MakeArc(vertex[i], dir, vertex[i - 1]);
                dir = c2d.EndDirection;
                c2d.Reverse();
                curves.Add(c2d);
            }
            curves.Reverse();
            dir = startdir;
            for (int i = m; i < vertex.Length - 1; i++)
            {
                ICurve2D c2d = MakeArc(vertex[i], dir, vertex[i + 1]);
                dir = c2d.EndDirection;
                curves.Add(c2d);
            }
            Path2D res = new Path2D(curves.ToArray(), true);
            return res;
        }
#endif
        /// <summary>
        /// Finds all real roots of a polynomial of arbitrary degree. The first coefficient is the one with the highest degree.
        /// Uses the Jenkins–Traub algorithm.
        /// </summary>
        /// <param name="coeff">coefficients of the polynom</param>
        /// <returns>the roots</returns>
        static public double[] PolynomialRoots(params double[] coeff)
        {
            // Performance Test
            //if (coeff.Length == 5)
            //{
            //    double[] res = new double[4];
            //    int tc0 = Environment.TickCount;
            //    for (int i = 0; i < 100000; i++)
            //    {
            //        int n = ragle4(coeff[0], coeff[1], coeff[2], coeff[3], coeff[4], res, true);
            //    }
            //    int tc1 = Environment.TickCount;
            //    for (int i = 0; i < 100000; i++)
            //    {
            //        res = QuarticRoots(coeff[0], coeff[1], coeff[2], coeff[3], coeff[4]);
            //    }
            //    int tc2 = Environment.TickCount;
            //    for (int i = 0; i < 100000; i++)
            //    {
            //        List<double> ld = RealPolynomialRootFinder.FindRoots(coeff);
            //    }
            //    int tc3 = Environment.TickCount;
            //    System.Diagnostics.Trace.WriteLine("ragle4: " + (tc1 - tc0).ToString() + " QuarticRoots: " + (tc2 - tc1).ToString() + " RealPolynomialRootFinder: " + (tc3 - tc2).ToString());
            //}
            List<double> ld = RealPolynomialRootFinder.FindRoots(coeff);
            return ld.ToArray();
        }

        static List<Tripel<int, int, int>>[] polycoeff = GetAllCoeff(6);

        private static List<Tripel<int, int, int>>[] GetAllCoeff(int maxDegree)
        {
            List<Tripel<int, int, int>>[] res = new List<Tripel<int, int, int>>[maxDegree];
            for (int i = 0; i < maxDegree; i++)
            {
                res[i] = GetCoeff(i + 1);
            }
            return res;
        }

        private static List<Pair<int, int>> GetPairList(int n)
        {
            List<Pair<int, int>> res = new List<Pair<int, int>>();
            for (int i = 0; i <= n; i++)
            {
                Pair<int, int> p = new Pair<int, int>(i, n - i);
                res.Add(p);
            }
            return res;
        }

        private static List<Tripel<int, int, int>> GetTripleList(int n)
        {
            List<Tripel<int, int, int>> res = new List<CADability.Tripel<int, int, int>>();
            for (int i = 0; i <= n; i++)
            {
                List<Pair<int, int>> pairs = GetPairList(n - i);
                for (int j = 0; j < pairs.Count; j++)
                {
                    Tripel<int, int, int> t = new Tripel<int, int, int>(i, pairs[j].First, pairs[j].Second);
                    res.Add(t);
                }
            }
            res.Add(new Tripel<int, int, int>(0, 0, 0)); // das ist quasi die Konstante
            return res;
        }
        private static double GetPolyFormValue(GeoPoint p, double[] coeff, int degree)
        {
            double res = 0.0;
            for (int i = 0; i < coeff.Length; i++)
            {
                double val = coeff[i];
                for (int j = 0; j < polycoeff[degree][i].First; j++) val *= p.x;
                for (int j = 0; j < polycoeff[degree][i].Second; j++) val *= p.y;
                for (int j = 0; j < polycoeff[degree][i].Third; j++) val *= p.z;
                res += val;
            }
            return res;
        }
        private static double[] GetPolyFormCoeff(GeoPoint p, int degree)
        {
            double[] res = new double[polycoeff[degree].Count];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = 1.0;
                for (int j = 0; j < polycoeff[degree][i].First; j++) res[i] *= p.x;
                for (int j = 0; j < polycoeff[degree][i].Second; j++) res[i] *= p.y;
                for (int j = 0; j < polycoeff[degree][i].Third; j++) res[i] *= p.z;
            }
            return res;
        }
        private static List<Tripel<int, int, int>> GetCoeff(int n)
        {
            List<Tripel<int, int, int>> res = new List<CADability.Tripel<int, int, int>>();
            for (int i = 0; i < n; i++)
            {
                res.AddRange(GetTripleList(i + 1));
            }
            return res;
        }

        internal static bool CommonPlane(GeoPoint p1, GeoVector v1, GeoPoint p2, GeoVector v2, out Plane pln)
        {
            if (Precision.SameDirection(v1, v2, false))
            {   // d.h. parallel
                GeoVector v3 = p2 - p1;
                if (!Precision.SameDirection(v1, v3, false))
                {
                    try
                    {
                        pln = new Plane(p1, v1, v3);
                        return true;
                    }
                    catch (PlaneException)
                    {
                        pln = new Plane(Plane.StandardPlane.XYPlane, 0.0); // braucht leider ein Ergebnis
                        return false;
                    }
                } // else: colinear, geht also nicht
            }
            else
            {
                try
                {
                    Plane p = new Plane(p1, v1, v2);

                    if (Math.Abs(p.Distance(p2)) < Precision.eps)
                    {
                        pln = p;
                        return true;
                    }
                }
                catch (PlaneException)
                {
                    pln = new Plane(Plane.StandardPlane.XYPlane, 0.0); // braucht leider ein Ergebnis
                    return false;
                }
            }
            pln = new Plane(Plane.StandardPlane.XYPlane, 0.0); // braucht leider ein Ergebnis
            return false;
        }

        private static double[] LinearCoeff(double[] coeff, double a, double b, double c, int degree)
        {
            // x = a*u, y = b*u, z= c* u
            double[] res = new double[degree + 1];
            for (int i = 0; i < res.Length; i++) res[i] = 0.0;
            for (int i = 0; i < coeff.Length; i++)
            {
                int pow = polycoeff[degree][i].First + polycoeff[degree][i].Second + polycoeff[degree][i].Third;
                double val = coeff[i];
                for (int j = 0; j < polycoeff[degree][i].First; j++) val *= a;
                for (int j = 0; j < polycoeff[degree][i].Second; j++) val *= b;
                for (int j = 0; j < polycoeff[degree][i].Third; j++) val *= c;
                res[pow] += val;
            }
            return res;
        }

        public static void FindImplicitPolynomialForm(BoundingRect uv, ISurface surface, int maxDegree)
        {
            // maxDegree==6: 89 Koeffizienten, 2: 11 Koeffizienten
            List<Tripel<int, int, int>> coeff = GetCoeff(maxDegree);

            Matrix m = new DenseMatrix(coeff.Count, coeff.Count);
            // suche coeff.Count viele gleichverteilte Punkte auf der surface
            // für jeden Punkt gibt es eine zeile in der matrix, die mit GetPolyFormCoeff bestimmt wird.
            // der vektor B ist 0
            // die Lösung sind die Koeffizienten des Polynoms in x, y, und z


            // Wenn wir später den Schnitt einer solchen Form mit einer Linie suchen, dann  muss die Linie in die Form
            // x = a*u, y = b*u, z = c*u, Dann können wir aus den Koeffizienten mit LinearCoeff u^n, u^(n-1) .. u, const herausholen und das Polynom in u^n lösen

        }

        public static double NextDouble(double value)
        {   // from https://stackoverflow.com/questions/1245870/next-higher-lower-ieee-double-precision-number?rq=1

            // Get the long representation of value:
            var longRep = BitConverter.DoubleToInt64Bits(value);

            long nextLong;
            if (longRep >= 0) // number is positive, so increment to go "up"
                nextLong = longRep + 1L;
            else if (longRep == long.MinValue) // number is -0
                nextLong = 1L;
            else  // number is negative, so decrement to go "up"
                nextLong = longRep - 1L;

            return BitConverter.Int64BitsToDouble(nextLong);
        }
        /// <summary>
        /// Find one ore more axis (point and direction) passing through the 4 provided lines given by p and v.
        /// the provided lines should be pairwise non planar
        /// </summary>
        /// <param name="p"></param>
        /// <param name="v"></param>
        /// <param name="axisPnt"></param>
        /// <param name="axisDir"></param>
        /// <returns>true if a solution could be found</returns>
        public static bool AxisThrouoghFourLines(GeoPoint[] p, GeoVector[] v, out GeoPoint[] axisPnt, out GeoVector[] axisDir)
        {
            axisPnt = null; // preset for return false
            axisDir = null;
            // Lines L0 .. L3: Li = p[i]+s*v[i]
            // point S0 defined as: p[0] + s*v[0], plane P0 defined by p[0] + s*v[0] and L1, depends on s
            // Normal N0 of plane P0 is : (p[1]-S0) ^ v[1]
            // the plane P0 rotates around the line L1 as S0 moves on the line L0
            // this plane intersects with L2 in Q2, which defines a point for a potential axis [S0,Q2]
            // Get the value s2 for s where there is no intersection with L2
            // plane P3 defined by p[3], v[3], v[1] 
            // Get the value s3 for s where there is no intersection with P3
            // with maxima (LineThroughFourLines.wxmx) we get
            double s2 = (((p[0].y - p[1].y) * v[1].x + (p[1].x - p[0].x) * v[1].y) * v[3].z + ((p[1].z - p[0].z) * v[1].x + (p[0].x - p[1].x) * v[1].z) * v[3].y + ((p[0].z - p[1].z) * v[1].y + (p[1].y - p[0].y) * v[1].z) * v[3].x) / ((v[0].y * v[1].z - v[0].z * v[1].y) * v[3].x + (v[0].z * v[1].x - v[0].x * v[1].z) * v[3].y + (v[0].x * v[1].y - v[0].y * v[1].x) * v[3].z);
            double s3 = (((p[0].y - p[2].y) * v[1].x + (p[2].x - p[0].x) * v[1].y) * v[2].z + ((p[2].z - p[0].z) * v[1].x + (p[0].x - p[2].x) * v[1].z) * v[2].y + ((p[0].z - p[2].z) * v[1].y + (p[2].y - p[0].y) * v[1].z) * v[2].x) / ((v[0].y * v[1].z - v[0].z * v[1].y) * v[2].x + (v[0].z * v[1].x - v[0].x * v[1].z) * v[2].y + (v[0].x * v[1].y - v[0].y * v[1].x) * v[2].z);

            if (double.IsNaN(s2) || double.IsNaN(s3)) return false; // probably coplanar lines
            // in the plane P3 we find the intersection points with line [S0,Q2] to make a (2d) curve in the form x = (a*s^2 + b*s + c)/((s - s2)*(s - s3)) 
            // and y = (d*s^2 + e*s + f)/((s - s2)*(s - s3))
            // now we can compute the values for (a, b, c and) d, e, f with 3 samples for s
            // The intersection of this 2d curve with L3 is the solution. Since L3 is the x-axis of P3, we only need to find where the curve intersects th x-axis, 
            // i.e. (d*s^2 + e*s + f)==0 (we don't need a, b and c)
            double[] t = new double[] { (s2 + s3) / 2.0, s3 + (s3 - s2), s2 - (s3 - s2) }; // three sample values for s
            double[] cy = new double[3]; // y values of the curve for this sample values
            PlaneSurface ps3 = new PlaneSurface(p[3], v[3], v[1]); // plane through P3, directions L3 and L1
            for (int i = 0; i < 3; i++)
            {
                GeoPoint q0 = p[0] + t[i] * v[0];
                PlaneSurface ps1 = new PlaneSurface(q0, p[1] - q0, v[1]);
                GeoPoint2D[] ips = ps1.GetLineIntersection(p[2], v[2]);
                if (ips.Length != 1) return false;
                GeoPoint q2 = ps1.PointAt(ips[0]);
                ips = ps3.GetLineIntersection(q0, q2 - q0);
                if (ips.Length != 1) return false;
                cy[i] = ips[0].y;
            }
            // y = (d*s^2 + e*s + f)/((s-s2)*(s-s3))
            // (d*t[i]^2 + e*t[i] + f) == cy[i]*((t[i]-s2)*(t[i]-s3))
            Matrix m = new DenseMatrix(3, 3);
            Vector b = new DenseVector(3);
            for (int i = 0; i < 3; i++)
            {
                m[i, 0] = t[i] * t[i];
                m[i, 1] = t[i];
                m[i, 2] = 1;
                b[i] = cy[i] * ((t[i] - s2) * (t[i] - s3));
            }
            Vector x = (Vector)m.Solve(b);
            if (x != null)
            {
                double d = x[0];
                double e = x[1];
                double f = x[2];
                double r0, r1;
                int n = quadgl(d, e, f, out r0, out r1);
                if (n == 0) return false;
                if (n == 1)
                {
                    axisPnt = new GeoPoint[1];
                    axisDir = new GeoVector[1];
                    axisPnt[0] = p[0] + r0 * v[0];
                    PlaneSurface ps1 = new PlaneSurface(axisPnt[0], p[1] - axisPnt[0], v[1]);
                    GeoPoint2D[] ips = ps1.GetLineIntersection(p[2], v[2]);
                    if (ips.Length != 1) return false;
                    axisDir[0] = ps1.PointAt(ips[0]) - axisPnt[0];
                    return true;
                }
                else
                {
                    axisPnt = new GeoPoint[2];
                    axisDir = new GeoVector[2];
                    axisPnt[0] = p[0] + r0 * v[0];
                    PlaneSurface ps1 = new PlaneSurface(axisPnt[0], p[1] - axisPnt[0], v[1]);
                    GeoPoint2D[] ips = ps1.GetLineIntersection(p[2], v[2]);
                    if (ips.Length != 1) return false;
                    axisDir[0] = ps1.PointAt(ips[0]) - axisPnt[0];
                    axisPnt[1] = p[0] + r1 * v[0];
                    ps1 = new PlaneSurface(axisPnt[1], p[1] - axisPnt[1], v[1]);
                    ips = ps1.GetLineIntersection(p[2], v[2]);
                    if (ips.Length != 1) return false;
                    axisDir[1] = ps1.PointAt(ips[0]) - axisPnt[1];
                    return true;
                }
            }
            return false;
        }
        public static GeoPoint[] CircleLinePerpDist(Ellipse circle, GeoPoint lineLocation, GeoVector lineDirection)
        {   // according to https://www.geometrictools.com/Documentation/DistanceToCircle3.pdf
            GeoVector n = (circle.MajorAxis ^ circle.MinorAxis).Normalized;
            GeoVector m = lineDirection.Normalized;
            GeoVector d = lineLocation - circle.Center;
            double r = circle.MajorRadius;
            GeoVector nm = n ^ m;
            GeoVector nd = n ^ d;
            Polynom tmd = new Polynom(1, "t", m * d, "");
            Polynom nmd = new Polynom(nm * nm, "t", nm * nd, "");
            Polynom toSolve = new Polynom(nm * nm, "t2", 2 * nm * nd, "t", nd * nd, "") * tmd * tmd - r * r * nmd * nmd;
            double[] roots = toSolve.Roots();
            List<GeoPoint> res = new List<GeoPoint>();
            for (int i = 0; i < roots.Length; i++)
            {
                GeoPoint p = lineLocation + roots[i] * m;
                Plane pln = new Plane(p, m); // perpendicular to line at the point found
                GeoPoint[] ints = circle.PlaneIntersection(pln);
                if (ints.Length == 2)
                {
                    double perp1 = (ints[0] - p) * circle.DirectionAt(circle.PositionOf(ints[0]));
                    double perp2 = (ints[1] - p) * circle.DirectionAt(circle.PositionOf(ints[1]));
                    if (Math.Abs(perp1) < Math.Abs(perp2)) res.Add(ints[0]);
                    else res.Add(ints[1]);
                    res.Add(p);
                }
                else if (ints.Length == 1)
                {
                    res.Add(ints[0]);
                }
            }
#if DEBUG
            //Plane cpln = new Plane(circle.Center, circle.MajorAxis, circle.MinorAxis);
            //Circle2D c2d = new Circle2D(GeoPoint2D.Origin, circle.MajorRadius);
            //GeoObjectList dbg = new GeoObjectList();
            //for (int i = 0; i < roots.Length; i++)
            //{
            //    GeoPoint p = lineLocation + roots[i] * m;
            //    GeoPoint2D p2d = cpln.Project(p);
            //    GeoPoint2D p2dc = GeoPoint2D.Origin + circle.MajorRadius * (p2d - GeoPoint2D.Origin).Normalized;
            //    GeoVector2D cdir2d = (p2dc - GeoPoint2D.Origin).ToLeft();
            //    GeoPoint circplePoint = cpln.ToGlobal(GeoPoint2D.Origin + circle.MajorRadius * (p2d - GeoPoint2D.Origin).Normalized);
            //    GeoVector cdir = cpln.ToGlobal(cdir2d);
            //    dbg.Add(Line.MakeLine(p, circplePoint));
            //    double perp1 = cdir * (p - circplePoint);
            //    double perp2 = lineDirection * (p - circplePoint);
            //    Plane plnl = new Plane(p, m);

            //    GeoPoint[] pli = circle.PlaneIntersection(plnl);
            //    if (pli.Length == 2)
            //    {
            //        double perp3 = cdir * (pli[0] - p);
            //        double perp4 = cdir * (pli[1] - p);
            //    }
            //}
#endif
            return res.ToArray();
        }
    }
}
