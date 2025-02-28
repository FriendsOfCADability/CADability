
using System;
using System.Collections.Generic;
using System.Numerics;

static class RealPolynomialRootFinder
{
    //Global variables that assist the computation, taken from the Visual Studio C++ compiler class float
    // smallest such that 1.0+DBL_EPSILON != 1.0 
    static double DBL_EPSILON = 2.22044604925031E-16;
    // max value 
    static double DBL_MAX = 1.79769313486232E+307;
    // min positive value 
    static double DBL_MIN = 2.2250738585072E-308;

    //If needed, set the maximum allowed degree for the polynomial here:

    // static int Max_Degree_Polynomial = 100;
    // static int Max_Degree_Polynomial = 10;
    //It is done to allocate memory for the computation arrays, so be careful to not set i too high, though in practice it should not be a problem as it is now.

    /// <summary>
    /// The Jenkins–Traub algorithm for polynomial zeros translated into pure VB.NET. It is a translation of the C++ algorithm, which in turn is a translation of the FORTRAN code by Jenkins. See Wikipedia for referances: http://en.wikipedia.org/wiki/Jenkins%E2%80%93Traub_algorithm 
    /// </summary>
    /// <param name="Input">The coefficients for the polynomial starting with the highest degree and ends on the constant, missing degree must be implemented as a 0.</param>
    /// <returns>All the real and complex roots that are found is returned in a list of complex numbers.</returns>
    /// <remarks>The maximum alloed degree polynomial for this implementation is set to 100. It can only take real coefficients.</remarks>
    public static List<double> FindRoots(params double[] Input)
    {
        List<double> result = new List<double>(Input.Length - 1);

        int j = 0;
        int l = 0;
        int N = 0;
        int NM1 = 0;
        int NN = 0;
        int NZ = 0;
        int zerok = 0;

        //Actual degree calculated from the imtems in the Input ParamArray
        int Degree = Input.Length - 1;
        //Helper variable that indicates the maximum length of the polynomial array
        int Degree_Helper = Degree + 1;


        double[] op = new double[Degree_Helper + 1];
        double[] K = new double[Degree_Helper + 1];
        double[] p = new double[Degree_Helper + 1];
        double[] pt = new double[Degree_Helper + 1];
        double[] qp = new double[Degree_Helper + 1];
        double[] temp = new double[Degree_Helper + 1];
        double[] zeror = new double[Degree_Helper + 1];
        double[] zeroi = new double[Degree + 1];
        double bnd = 0;
        double df = 0;
        double dx = 0;
        double ff = 0;
        double moduli_max = 0;
        double moduli_min = 0;
        double x = 0;
        double xm = 0;
        double aa = 0;
        double bb = 0;
        double cc = 0;
        double lzi = 0;
        double lzr = 0;
        double sr = 0;
        double szi = 0;
        double szr = 0;
        double t = 0;
        double u = 0;
        double xx = 0;
        double xxx = 0;
        double yy = 0;

        // These are used to scale the polynomial for more accuracy
        double factor = 0;
        double sc = 0;

        double RADFAC = 3.14159265358979 / 180;
        // Degrees-to-radians conversion factor = pi/180
        double lb2 = Math.Log(2.0);
        // Dummy variable to avoid re-calculating this value in loop below
        double lo = DBL_MIN / DBL_EPSILON;
        //Double.MinValue / Double.Epsilon
        double cosr = Math.Cos(94.0 * RADFAC);
        // = -0.069756474
        double sinr = Math.Sin(94.0 * RADFAC);
        // = 0.99756405

        //Are the polynomial larger that the maximum allowed?
        //TODO: Reenable this check if MAXDEGREE was defined.
        //if (Degree > Degree)
        //{
        //    throw new ApplicationException("The entered Degree is greater than MAXDEGREE. Exiting root finding algorithm. No further action taken.");
        //}

        //Check if the leading coefficient is zero

        if (Input[0] != 0)
        {
            for (int i = 0; i <= Input.Length - 1; i++)
            {
                op[i] = Input[i];
            }

            N = Degree;
            xx = Math.Sqrt(0.5);
            //= 0.70710678
            yy = -xx;

            // Remove zeros at the origin, if any
            j = 0;
            while ((op[N] == 0))
            {
                zeror[j] = 0;
                zeroi[j] = 0.0;
                N -= 1;
                j += 1;
            }

            NN = N + 1;

            for (int i = 0; i <= NN - 1; i++)
            {
                p[i] = op[i];
            }

            while (N >= 1)
            {
                //Start the algorithm for one zero
                if (N <= 2)
                {
                    if (N < 2)
                    {
                        //1st degree polynomial
                        zeror[(Degree) - 1] = -(p[1] / p[0]);
                        zeroi[(Degree) - 1] = 0.0;
                    }
                    else
                    {
                        //2nd degree polynomial
                        Quad_ak1(p[0], p[1], p[2], ref zeror[((Degree) - 2)], ref zeroi[((Degree) - 2)], ref zeror[((Degree) - 1)], ref zeroi[(Degree) - 1]);
                    }
                    //Solutions have been calculated, so exit the loop
                    break; // TODO: might not be correct. Was : Exit While
                }

                moduli_max = 0.0;
                moduli_min = DBL_MAX;

                for (int i = 0; i <= NN - 1; i++)
                {
                    x = Math.Abs(p[i]);
                    if ((x > moduli_max))
                        moduli_max = x;
                    if (((x != 0) & (x < moduli_min)))
                        moduli_min = x;
                }

                // Scale if there are large or very small coefficients
                // Computes a scale factor to multiply the coefficients of the polynomial. The scaling
                // is done to avoid overflow and to avoid undetected underflow interfering with the
                // convergence criterion.
                // The factor is a power of the base.

                //  Scaling the polynomial
                sc = lo / moduli_min;

                if ((((sc <= 1.0) & (moduli_max >= 10)) | ((sc > 1.0) & (DBL_MAX / sc >= moduli_max))))
                {
                    if (sc == 0)
                    {
                        sc = DBL_MIN;
                    }

                    l = Convert.ToInt32(Math.Log(sc) / lb2 + 0.5);
                    factor = Math.Pow(2.0, l);
                    if ((factor != 1.0))
                    {
                        for (int i = 0; i <= NN; i++)
                        {
                            p[i] *= factor;
                        }
                    }
                }

                //Compute lower bound on moduli of zeros
                for (int i = 0; i <= NN - 1; i++)
                {
                    pt[i] = Math.Abs(p[i]);
                }
                pt[N] = -(pt[N]);

                NM1 = N - 1;

                // Compute upper estimate of bound
                x = Math.Exp((Math.Log(-pt[N]) - Math.Log(pt[0])) / Convert.ToDouble(N));

                if ((pt[NM1] != 0))
                {
                    // If Newton step at the origin is better, use it
                    xm = -pt[N] / pt[NM1];
                    if (xm < x)
                    {
                        x = xm;
                    }
                }

                // Chop the interval (0, x) until ff <= 0
                xm = x;

                do
                {
                    x = xm;
                    xm = 0.1 * x;
                    ff = pt[0];
                    for (int i = 1; i <= NN - 1; i++)
                    {
                        ff = ff * xm + pt[i];
                    }
                } while ((ff > 0));

                dx = x;

                do
                {
                    df = pt[0];
                    ff = pt[0];
                    for (int i = 1; i <= N - 1; i++)
                    {
                        ff = x * ff + pt[i];
                        df = x * df + ff;
                    }
                    ff = x * ff + pt[N];
                    dx = ff / df;
                    x -= dx;
                } while ((Math.Abs(dx / x) > 0.005));

                bnd = x;

                // Compute the derivative as the initial K polynomial and do 5 steps with no shift
                for (int i = 1; i <= N - 1; i++)
                {
                    K[i] = Convert.ToDouble(N - i) * p[i] / (Convert.ToDouble(N));
                }
                K[0] = p[0];

                aa = p[N];
                bb = p[NM1];
                if ((K[NM1] == 0))
                {
                    zerok = 1;
                }
                else
                {
                    zerok = 0;
                }

                for (int jj = 0; jj <= 4; jj++)
                {
                    cc = K[NM1];
                    if ((zerok == 1))
                    {
                        // Use unscaled form of recurrence
                        for (int i = 0; i <= NM1 - 1; i++)
                        {
                            j = NM1 - i;
                            K[j] = K[j - 1];
                        }
                        K[0] = 0;
                        if ((K[NM1] == 0))
                        {
                            zerok = 1;
                        }
                        else
                        {
                            zerok = 0;
                        }
                    }
                    else
                    {
                        // Used scaled form of recurrence if value of K at 0 is nonzero
                        t = -aa / cc;
                        for (int i = 0; i <= NM1 - 1; i++)
                        {
                            j = NM1 - i;
                            K[j] = t * K[j - 1] + p[j];
                        }
                        K[0] = p[0];
                        if ((Math.Abs(K[NM1]) <= Math.Abs(bb) * DBL_EPSILON * 10.0))
                        {
                            zerok = 1;
                        }
                        else
                        {
                            zerok = 0;
                        }
                    }
                }

                // Save K for restarts with new shifts
                for (int i = 0; i <= N - 1; i++)
                {
                    temp[i] = K[i];
                }


                for (int jj = 1; jj <= 20; jj++)
                {
                    // Quadratic corresponds to a double shift to a non-real point and its
                    // complex conjugate. The point has modulus BND and amplitude rotated
                    // by 94 degrees from the previous shift.

                    xxx = -(sinr * yy) + cosr * xx;
                    yy = sinr * xx + cosr * yy;
                    xx = xxx;
                    sr = bnd * xx;
                    u = -(2.0 * sr);

                    // Second stage calculation, fixed quadratic
                    Fxshfr_ak1(20 * jj, ref NZ, sr, bnd, K, N, p, NN, qp, u,
                    ref lzi, ref lzr, ref szi, ref szr);


                    if ((NZ != 0))
                    {
                        // The second stage jumps directly to one of the third stage iterations and
                        // returns here if successful. Deflate the polynomial, store the zero or
                        // zeros, and return to the main algorithm.

                        j = (Degree) - N;
                        zeror[j] = szr;
                        zeroi[j] = szi;
                        NN = NN - NZ;
                        N = NN - 1;
                        for (int i = 0; i <= NN - 1; i++)
                        {
                            p[i] = qp[i];
                        }
                        if ((NZ != 1))
                        {
                            zeror[j + 1] = lzr;
                            zeroi[j + 1] = lzi;
                        }

                        //Found roots start all calulations again, with a lower order polynomial
                        break; // TODO: might not be correct. Was : Exit For
                    }
                    else
                    {
                        // If the iteration is unsuccessful, another quadratic is chosen after restoring K
                        for (int i = 0; i <= N - 1; i++)
                        {
                            K[i] = temp[i];
                        }
                    }
                    if ((jj >= 20))
                    {
                        throw new ApplicationException("Failure. No convergence after 20 shifts");
                    }
                }
            }

        }
        else
        {
            throw new ApplicationException("The leading coefficient is zero. No further action taken");
        }

        for (int i = 0; i <= Degree - 1; i++)
        {
            if (zeroi[Degree - 1 - i] == 0.0)
                result.Add(zeror[Degree - 1 - i]);
        }

        return result;
    }

    private static void Fxshfr_ak1(int L2, ref int NZ, double sr, double v, double[] K, int N, double[] p, int NN, double[] qp, double u, ref double lzi, ref double lzr, ref double szi, ref double szr)
    {
        // Computes up to L2 fixed shift K-polynomials, testing for convergence in the linear or
        // quadratic case. Initiates one of the variable shift iterations and returns with the
        // number of zeros found.

        // L2 limit of fixed shift steps
        // NZ number of zeros found

        int fflag = 0;
        int i = 0;
        int iFlag = 0;
        int j = 0;
        int spass = 0;
        int stry = 0;
        int tFlag = 0;
        int vpass = 0;
        int vtry = 0;
        iFlag = 1;
        double a = 0;
        double a1 = 0;
        double a3 = 0;
        double a7 = 0;
        double b = 0;
        double betas = 0;
        double betav = 0;
        double c = 0;
        double d = 0;
        double e = 0;
        double f = 0;
        double g = 0;
        double h = 0;
        double oss = 0;
        double ots = 0;
        double otv = 0;
        double ovv = 0;
        double s = 0;
        double ss = 0;
        double ts = 0;
        double tss = 0;
        double tv = 0;
        double tvv = 0;
        double ui = 0;
        double vi = 0;
        double vv = 0;
        double[] qk = new double[K.Length];
        double[] svk = new double[K.Length];

        NZ = 0;
        betav = 0.25;
        betas = 0.25;
        oss = sr;
        ovv = v;

        // Evaluate polynomial by synthetic division
        QuadSD_ak1(NN, u, v, p, qp, ref a, ref b);

        tFlag = calcSC_ak1(N, a, b, ref a1, ref a3, ref a7, ref c, ref d, ref e, ref f,
        ref g, ref h, K, u, v, qk);


        for (j = 0; j <= L2 - 1; j++)
        {
            fflag = 1;
            // Calculate next K polynomial and estimate v
            nextK_ak1(N, tFlag, a, b, a1, ref a3, ref a7, K, qk, qp);
            tFlag = calcSC_ak1(N, a, b, ref a1, ref a3, ref a7, ref c, ref d, ref e, ref f,
            ref g, ref h, K, u, v, qk);
            newest_ak1(tFlag, ref ui, ref vi, a, a1, a3, a7, b, c, d,
            f, g, h, u, v, K, N, p);

            vv = vi;

            // Estimate s
            if (K[N - 1] != 0)
            {
                ss = -(p[N] / K[N - 1]);
            }
            else
            {
                ss = 0;
            }

            ts = 1;
            tv = 1.0;


            if (((j != 0) & (tFlag != 3)))
            {
                // Compute relative measures of convergence of s and v sequences
                if (vv != 0)
                {
                    tv = Math.Abs((vv - ovv) / vv);
                }

                if (ss != 0)
                {
                    ts = Math.Abs((ss - oss) / ss);
                }


                // If decreasing, multiply the two most recent convergence measures

                if (tv < otv)
                {
                    tvv = tv * otv;
                }
                else
                {
                    tvv = 1;
                }


                if (ts < ots)
                {
                    tss = ts * ots;
                }
                else
                {
                    tss = 1;
                }

                // Compare with convergence criteria

                if (tvv < betav)
                {
                    vpass = 1;
                }
                else
                {
                    vpass = 0;
                }

                if (tss < betas)
                {
                    spass = 1;
                }
                else
                {
                    spass = 0;
                }



                if (((spass == 1) | (vpass == 1)))
                {
                    // At least one sequence has passed the convergence test.
                    // Store variables before iterating

                    for (i = 0; i <= N - 1; i++)
                    {
                        svk[i] = K[i];
                    }

                    s = ss;

                    // Choose iteration according to the fastest converging sequence
                    stry = 0;
                    vtry = 0;


                    do
                    {
                        if ((fflag == 1 & ((fflag == 0))) & ((spass == 1) & (!(vpass == 1) | (tss < tvv))))
                        {
                            // Do nothing. Provides a quick "short circuit".
                        }
                        else
                        {
                            QuadIT_ak1(N, ref NZ, ui, vi, ref szr, ref szi, ref lzr, ref lzi, qp, NN,
                            ref a, ref b, p, qk, ref a1, ref a3, ref a7, ref c, ref d, ref e,
                            ref f, ref g, ref h, K);

                            if (((NZ) > 0))
                                return;

                            // Quadratic iteration has failed. Flag that it has been tried and decrease the
                            // convergence criterion

                            iFlag = 1;
                            vtry = 1;
                            betav *= 0.25;

                            // Try linear iteration if it has not been tried and the s sequence is converging
                            if ((stry == 1 | (!(spass == 1))))
                            {
                                iFlag = 0;
                            }
                            else
                            {
                                for (i = 0; i <= N - 1; i++)
                                {
                                    K[i] = svk[i];
                                }
                            }

                        }

                        if ((iFlag != 0))
                        {
                            RealIT_ak1(ref iFlag, ref NZ, ref s, N, p, NN, qp, ref szr, ref szi, K,
                            qk);

                            if (((NZ) > 0))
                                return;

                            // Linear iteration has failed. Flag that it has been tried and decrease the
                            // convergence criterion
                            stry = 1;
                            betas *= 0.25;


                            if ((iFlag != 0))
                            {
                                // If linear iteration signals an almost double real zero, attempt quadratic iteration
                                ui = -(s + s);
                                vi = s * s;
                            }
                        }

                        // Restore variables
                        for (i = 0; i <= N - 1; i++)
                        {
                            K[i] = svk[i];
                        }


                        // Try quadratic iteration if it has not been tried and the v sequence is converging
                        if ((!(vpass == 1) | vtry == 1))
                        {
                            // Break out of infinite for loop
                            break; // TODO: might not be correct. Was : Exit Do
                        }


                    } while (true);

                    // Re-compute qp and scalar values to continue the second stage
                    QuadSD_ak1(NN, u, v, p, qp, ref a, ref b);
                    tFlag = calcSC_ak1(N, a, b, ref a1, ref a3, ref a7, ref c, ref d, ref e, ref f,
                    ref g, ref h, K, u, v, qk);

                }
            }

            ovv = vv;
            oss = ss;
            otv = tv;
            ots = ts;
        }

    }

    private static void QuadSD_ak1(int NN, double u, double v, double[] p, double[] q, ref double a, ref double b)
    {
        // Divides p by the quadratic 1, u, v placing the quotient in q and the remainder in a, b

        int i = 0;

        b = p[0];
        q[0] = p[0];

        a = -((b) * u) + p[1];
        q[1] = -((b) * u) + p[1];

        for (i = 2; i <= NN - 1; i++)
        {
            q[i] = -((a) * u + (b) * v) + p[i];
            b = (a);
            a = q[i];
        }

    }

    private static int calcSC_ak1(int N, double a, double b, ref double a1, ref double a3, ref double a7, ref double c, ref double d, ref double e, ref double f, ref double g, ref double h, double[] K, double u, double v, double[] qk)
    {

        // This routine calculates scalar quantities used to compute the next K polynomial and
        // new estimates of the quadratic coefficients.

        // calcSC - integer variable set here indicating how the calculations are normalized
        // to avoid overflow.

        int dumFlag = 3;
        // TYPE = 3 indicates the quadratic is almost a factor of K

        // Synthetic division of K by the quadratic 1, u, v
        QuadSD_ak1(N, u, v, K, qk, ref c, ref d);

        if ((Math.Abs((c)) <= (100.0 * DBL_EPSILON * Math.Abs(K[N - 1]))))
        {
            if ((Math.Abs((d)) <= (100.0 * DBL_EPSILON * Math.Abs(K[N - 2]))))
            {
                return dumFlag;
            }
        }

        h = v * b;
        if ((Math.Abs((d)) >= Math.Abs((c))))
        {
            dumFlag = 2;
            // TYPE = 2 indicates that all formulas are divided by d
            e = a / (d);
            f = (c) / (d);
            g = u * b;
            a3 = (e) * ((g) + a) + (h) * (b / (d));
            a1 = -a + (f) * b;
            a7 = (h) + ((f) + u) * a;
        }
        else
        {
            dumFlag = 1;
            // TYPE = 1 indicates that all formulas are divided by c
            e = a / (c);
            f = (d) / (c);
            g = (e) * u;
            a3 = (e) * a + ((g) + (h) / (c)) * b;
            a1 = -(a * ((d) / (c))) + b;
            a7 = (g) * (d) + (h) * (f) + a;
        }

        return dumFlag;
    }

    private static void nextK_ak1(int N, int tFlag, double a, double b, double a1, ref double a3, ref double a7, double[] K, double[] qk, double[] qp)
    {
        // Computes the next K polynomials using the scalars computed in calcSC_ak1

        int i = 0;
        double temp = 0;

        // Use unscaled form of the recurrence
        if ((tFlag == 3))
        {
            K[1] = 0;
            K[0] = 0.0;

            for (i = 2; i <= N - 1; i++)
            {
                K[i] = qk[i - 2];
            }

            return;
        }

        if (tFlag == 1)
        {
            temp = b;
        }
        else
        {
            temp = a;
        }


        if ((Math.Abs(a1) > (10.0 * DBL_EPSILON * Math.Abs(temp))))
        {
            // Use scaled form of the recurrence

            a7 = a7 / a1;
            a3 = a3 / a1;
            K[0] = qp[0];
            K[1] = -((a7) * qp[0]) + qp[1];

            for (i = 2; i <= N - 1; i++)
            {
                K[i] = -((a7) * qp[i - 1]) + (a3) * qk[i - 2] + qp[i];
            }
        }
        else
        {
            // If a1 is nearly zero, then use a special form of the recurrence

            K[0] = 0.0;
            K[1] = -(a7) * qp[0];

            for (i = 2; i <= N - 1; i++)
            {
                K[i] = -((a7) * qp[i - 1]) + (a3) * qk[i - 2];
            }
        }
    }

    private static void newest_ak1(int tFlag, ref double uu, ref double vv, double a, double a1, double a3, double a7, double b, double c, double d, double f, double g, double h, double u, double v, double[] K, int N, double[] p)
    {
        // Compute new estimates of the quadratic coefficients using the scalars computed in calcSC_ak1

        double a4 = 0;
        double a5 = 0;
        double b1 = 0;
        double b2 = 0;
        double c1 = 0;
        double c2 = 0;
        double c3 = 0;
        double c4 = 0;
        double temp = 0;

        vv = 0;
        //The quadratic is zeroed
        uu = 0.0;
        //The quadratic is zeroed


        if ((tFlag != 3))
        {
            if ((tFlag != 2))
            {
                a4 = a + u * b + h * f;
                a5 = c + (u + v * f) * d;

            }
            else
            {
                a4 = (a + g) * f + h;
                a5 = (f + u) * c + v * d;
            }

            // Evaluate new quadratic coefficients
            b1 = -K[N - 1] / p[N];
            b2 = -(K[N - 2] + b1 * p[N - 1]) / p[N];
            c1 = v * b2 * a1;
            c2 = b1 * a7;
            c3 = b1 * b1 * a3;
            c4 = -(c2 + c3) + c1;
            temp = -c4 + a5 + b1 * a4;
            if ((temp != 0.0))
            {
                uu = -((u * (c3 + c2) + v * (b1 * a1 + b2 * a7)) / temp) + u;
                vv = v * (1.0 + c4 / temp);
            }

        }
    }

    private static void QuadIT_ak1(int N, ref int NZ, double uu, double vv, ref double szr, ref double szi, ref double lzr, ref double lzi, double[] qp, int NN, ref double a, ref double b, double[] p, double[] qk, ref double a1, ref double a3, ref double a7, ref double c, ref double d, ref double e, ref double f, ref double g, ref double h, double[] K)
    {
        // Variable-shift K-polynomial iteration for a quadratic factor converges only if the
        // zeros are equimodular or nearly so.

        int i = 0;
        int j = 0;
        int tFlag = 0;
        int triedFlag = 0;
        j = 0;
        triedFlag = 0;

        double ee = 0;
        double mp = 0;
        double omp = 0;
        double relstp = 0;
        double t = 0;
        double u = 0;
        double ui = 0;
        double v = 0;
        double vi = 0;
        double zm = 0;

        NZ = 0;
        //Number of zeros found
        u = uu;
        //uu and vv are coefficients of the starting quadratic
        v = vv;

        do
        {
            Quad_ak1(1.0, u, v, ref szr, ref szi, ref lzr, ref lzi);

            // Return if roots of the quadratic are real and not close to multiple or nearly
            // equal and of opposite sign.
            if ((Math.Abs(Math.Abs(szr) - Math.Abs(lzr)) > 0.01 * Math.Abs(lzr)))
            {
                break; // TODO: might not be correct. Was : Exit Do
            }

            // Evaluate polynomial by quadratic synthetic division
            QuadSD_ak1(NN, u, v, p, qp, ref a, ref b);

            mp = Math.Abs(-((szr) * (b)) + (a)) + Math.Abs((szi) * (b));

            // Compute a rigorous bound on the rounding error in evaluating p
            zm = Math.Sqrt(Math.Abs(v));
            ee = 2.0 * Math.Abs(qp[0]);
            t = -((szr) * (b));

            for (i = 1; i <= N - 1; i++)
            {
                ee = ee * zm + Math.Abs(qp[i]);
            }

            ee = ee * zm + Math.Abs((a) + t);
            ee = (9.0 * ee + 2.0 * Math.Abs(t) - 7.0 * (Math.Abs((a) + t) + zm * Math.Abs((b)))) * DBL_EPSILON;

            // Iteration has converged sufficiently if the polynomial value is less than 20 times this bound
            if ((mp <= 20.0 * ee))
            {
                NZ = 2;
                break; // TODO: might not be correct. Was : Exit Do
            }

            j += 1;

            // Stop iteration after 20 steps
            if ((j > 20))
                break; // TODO: might not be correct. Was : Exit Do

            if ((j >= 2))
            {
                if (((relstp <= 0.01) & (mp >= omp) & (!(triedFlag == 1))))
                {
                    // A cluster appears to be stalling the convergence. Five fixed shift
                    // steps are taken with a u, v close to the cluster.
                    if (relstp < DBL_EPSILON)
                    {
                        relstp = Math.Sqrt(DBL_EPSILON);
                    }
                    else
                    {
                        relstp = Math.Sqrt(relstp);
                    }

                    u -= u * relstp;
                    v += v * relstp;

                    QuadSD_ak1(NN, u, v, p, qp, ref a, ref b);

                    for (i = 0; i <= 4; i++)
                    {
                        tFlag = calcSC_ak1(N, a, b, ref a1, ref a3, ref a7, ref c, ref d, ref e, ref f,
                        ref g, ref h, K, u, v, qk);
                        nextK_ak1(N, tFlag, a, b, a1, ref a3, ref a7, K, qk, qp);
                    }

                    triedFlag = 1;
                    j = 0;

                }

            }

            omp = mp;

            // Calculate next K polynomial and new u and v
            tFlag = calcSC_ak1(N, a, b, ref a1, ref a3, ref a7, ref c, ref d, ref e, ref f,
            ref g, ref h, K, u, v, qk);
            nextK_ak1(N, tFlag, a, b, a1, ref a3, ref a7, K, qk, qp);
            tFlag = calcSC_ak1(N, a, b, ref a1, ref a3, ref a7, ref c, ref d, ref e, ref f,
            ref g, ref h, K, u, v, qk);
            newest_ak1(tFlag, ref ui, ref vi, a, a1, a3, a7, b, c, d,
            f, g, h, u, v, K, N, p);

            // If vi is zero, the iteration is not converging
            if ((vi != 0))
            {
                relstp = Math.Abs((-v + vi) / vi);
                u = ui;
                v = vi;
            }
        } while ((vi != 0));
    }

    private static void RealIT_ak1(ref int iFlag, ref int NZ, ref double sss, int N, double[] p, int NN, double[] qp, ref double szr, ref double szi, double[] K, double[] qk)
    {
        // Variable-shift H-polynomial iteration for a real zero

        // sss - starting iterate
        // NZ - number of zeros found
        // iFlag - flag to indicate a pair of zeros near real axis
        int i = 0;
        int j = 0;
        int nm1 = 0;
        j = 0;
        nm1 = N - 1;
        double ee = 0;
        double kv = 0;
        double mp = 0;
        double ms = 0;
        double omp = 0;
        double pv = 0;
        double s = 0;
        double t = 0;

        iFlag = 0;
        NZ = 0;
        s = sss;

        do
        {
            pv = p[0];

            // Evaluate p at s
            qp[0] = pv;
            for (i = 1; i <= NN - 1; i++)
            {
                qp[i] = pv * s + p[i];
                pv = pv * s + p[i];
            }
            mp = Math.Abs(pv);

            // Compute a rigorous bound on the error in evaluating p
            ms = Math.Abs(s);
            ee = 0.5 * Math.Abs(qp[0]);
            for (i = 1; i <= NN - 1; i++)
            {
                ee = ee * ms + Math.Abs(qp[i]);
            }

            // Iteration has converged sufficiently if the polynomial value is less than
            // 20 times this bound
            if ((mp <= 20.0 * DBL_EPSILON * (2.0 * ee - mp)))
            {
                NZ = 1;
                szr = s;
                szi = 0.0;
                break; // TODO: might not be correct. Was : Exit Do
            }

            j += 1;

            // Stop iteration after 10 steps
            if ((j > 10))
                break; // TODO: might not be correct. Was : Exit Do

            if ((j >= 2))
            {
                if (((Math.Abs(t) <= 0.001 * Math.Abs(-t + s)) & (mp > omp)))
                {
                    // A cluster of zeros near the real axis has been encountered                    ' Return with iFlag set to initiate a quadratic iteration

                    iFlag = 1;
                    sss = s;
                    break; // TODO: might not be correct. Was : Exit Do
                }

            }

            // Return if the polynomial value has increased significantly
            omp = mp;

            // Compute t, the next polynomial and the new iterate
            qk[0] = K[0];
            kv = K[0];
            for (i = 1; i <= N - 1; i++)
            {
                kv = kv * s + K[i];
                qk[i] = kv;
            }
            if ((Math.Abs(kv) > Math.Abs(K[nm1]) * 10.0 * DBL_EPSILON))
            {
                // Use the scaled form of the recurrence if the value of K at s is non-zero
                t = -(pv / kv);
                K[0] = qp[0];
                for (i = 1; i <= N - 1; i++)
                {
                    K[i] = t * qk[i - 1] + qp[i];
                }
            }
            else
            {
                // Use unscaled form
                K[0] = 0.0;
                for (i = 1; i <= N - 1; i++)
                {
                    K[i] = qk[i - 1];
                }
            }

            kv = K[0];
            for (i = 1; i <= N - 1; i++)
            {
                kv = kv * s + K[i];
            }

            if ((Math.Abs(kv) > (Math.Abs(K[nm1]) * 10.0 * DBL_EPSILON)))
            {
                t = -(pv / kv);
            }
            else
            {
                t = 0.0;
            }

            s += t;

        } while (true);
    }

    private static void Quad_ak1(double a, double b1, double c, ref double sr, ref double si, ref double lr, ref double li)
    {
        // Calculates the zeros of the quadratic a*Z^2 + b1*Z + c
        // The quadratic formula, modified to avoid overflow, is used to find the larger zero if the
        // zeros are real and both zeros are complex. The smaller real zero is found directly from
        // the product of the zeros c/a.

        double b = 0;
        double d = 0;
        double e = 0;

        sr = 0;
        si = 0;
        lr = 0;
        li = 0.0;

        if (a == 0)
        {
            if (b1 == 0)
            {
                sr = -c / b1;
            }
        }

        if (c == 0)
        {
            lr = -b1 / a;
        }

        //Compute discriminant avoiding overflow
        b = b1 / 2.0;

        if (Math.Abs(b) < Math.Abs(c))
        {
            if (c >= 0)
            {
                e = a;
            }
            else
            {
                e = -a;
            }

            e = -e + b * (b / Math.Abs(c));
            d = Math.Sqrt(Math.Abs(e)) * Math.Sqrt(Math.Abs(c));
        }
        else
        {
            e = -((a / b) * (c / b)) + 1.0;
            d = Math.Sqrt(Math.Abs(e)) * (Math.Abs(b));
        }



        if ((e >= 0))
        {
            // Real zero
            if (b >= 0)
            {
                d *= -1;
            }
            lr = (-b + d) / a;

            if (lr != 0)
            {
                sr = (c / (lr)) / a;
            }
        }
        else
        {
            // Complex conjugate zeros
            lr = -(b / a);
            sr = -(b / a);
            si = Math.Abs(d / a);
            li = -(si);
        }
    }
}

static class ComplexPolynomialRootFinder
{
    static double sr;
    static double si;
    static double tr;
    static double ti;
    static double pvr;
    static double pvi;
    static double are;
    static double mre;
    static double eta;
    static double infin;

    static int nn;
    //Global variables that assist the computation, taken from the Visual Studio C++ compiler class float
    // smallest such that 1.0+DBL_EPSILON != 1.0 
    static double DBL_EPSILON = 2.22044604925031E-16;
    // max value 
    static double DBL_MAX = 1.79769313486232E+307;
    // min positive value 
    static double DBL_MIN = 2.2250738585072E-308;
    // exponent radix 
    static double DBL_RADIX = 2;


    //If needed, set the maximum allowed degree for the polynomial here:

    static int Max_Degree_Polynomial = 100;
    //It is done to allocate memory for the computation arrays, so be careful to not set i too high, though in practice it should not be a problem as it is now.

    //static int Degree;
    // Allocate arrays
    static double[] pr = new double[Max_Degree_Polynomial + 2];
    static double[] pi = new double[Max_Degree_Polynomial + 2];
    static double[] hr = new double[Max_Degree_Polynomial + 2];
    static double[] hi = new double[Max_Degree_Polynomial + 2];
    static double[] qpr = new double[Max_Degree_Polynomial + 2];
    static double[] qpi = new double[Max_Degree_Polynomial + 2];
    static double[] qhr = new double[Max_Degree_Polynomial + 2];
    static double[] qhi = new double[Max_Degree_Polynomial + 2];
    static double[] shr = new double[Max_Degree_Polynomial + 2];

    static double[] shi = new double[Max_Degree_Polynomial + 2];
    public static List<Complex> FindRoots(params Complex[] Input)
    {

        List<Complex> result = new List<Complex>();

        int idnn2 = 0;
        int conv = 0;
        double xx = 0;
        double yy = 0;
        double cosr = 0;
        double sinr = 0;
        double smalno = 0;
        double @base = 0;
        double xxx = 0;
        double zr = 0;
        double zi = 0;
        double bnd = 0;

        //     const double *opr, const double *opi, int degree, double *zeror, double *zeroi
        //Helper variable that indicates the maximum length of the polynomial array
        int Max_Degree_Helper = Max_Degree_Polynomial + 1;

        //Actual degree calculated from the items in the Input ParamArray
        int Degree = Input.Length - 1;

        //Are the polynomial larger that the maximum allowed?
        if (Degree > Max_Degree_Polynomial)
        {
            throw new ApplicationException("The entered Degree is greater than MAXDEGREE. Exiting root finding algorithm. No further action taken.");
        }

        double[] opr = new double[Degree + 2];
        double[] opi = new double[Degree + 2];
        double[] zeror = new double[Degree + 2];
        double[] zeroi = new double[Degree + 2];

        for (int i = 0; i <= Input.Length - 1; i++)
        {
            opr[i] = Input[i].Real;
            opi[i] = Input[i].Imaginary;
        }

        mcon(ref eta, ref infin, ref smalno, ref @base);
        are = eta;
        mre = 2.0 * Math.Sqrt(2.0) * eta;
        xx = 0.70710678;
        yy = -xx;
        cosr = -0.060756474;
        sinr = -0.99756405;
        nn = Degree;

        // Algorithm fails if the leading coefficient is zero
        if ((opr[0] == 0 & opi[0] == 0))
        {
            throw new ApplicationException("The leading coefficient is zero. No further action taken. Program terminated.");
        }


        // Remove the zeros at the origin if any
        while ((opr[nn] == 0 & opi[nn] == 0))
        {
            idnn2 = Degree - nn;
            zeror[idnn2] = 0;
            zeroi[idnn2] = 0;
            nn -= 1;
        }

        // Make a copy of the coefficients
        for (int i = 0; i <= nn; i++)
        {
            pr[i] = opr[i];
            pi[i] = opi[i];
            shr[i] = cmod(pr[i], pi[i]);
        }

        // Scale the polynomial
        bnd = scale(nn, shr, eta, infin, smalno, @base);
        if ((bnd != 1))
        {
            for (int i = 0; i <= nn; i++)
            {
                pr[i] *= bnd;
                pi[i] *= bnd;
            }
        }
        search:

        if ((nn <= 1))
        {
            cdivid(-pr[1], -pi[1], pr[0], pi[0], ref zeror[Degree - 1], ref zeroi[Degree - 1]);

            for (int i = 0; i <= Degree - 1; i++)
            {
                result.Add(new Complex(zeror[i], zeroi[i]));
            }
            return result;

        }

        // Calculate bnd, alower bound on the modulus of the zeros
        for (int i = 0; i <= nn; i++)
        {
            shr[i] = cmod(pr[i], pi[i]);
        }

        cauchy(nn, shr, shi, ref bnd);


        // Outer loop to control 2 Major passes with different sequences of shifts
        for (int cnt1 = 1; cnt1 <= 2; cnt1++)
        {
            // First stage  calculation , no shift
            noshft(5);

            // Inner loop to select a shift
            for (int cnt2 = 1; cnt2 <= 9; cnt2++)
            {
                // Shift is chosen with modulus bnd and amplitude rotated by 94 degree from the previous shif
                xxx = cosr * xx - sinr * yy;
                yy = sinr * xx + cosr * yy;
                xx = xxx;
                sr = bnd * xx;
                si = bnd * yy;

                // Second stage calculation, fixed shift
                fxshft(10 * cnt2, ref zr, ref zi, ref conv);
                if ((conv == 1))
                {
                    // The second stage jumps directly to the third stage ieration
                    // If successful the zero is stored and the polynomial deflated
                    idnn2 = Degree - nn;
                    zeror[idnn2] = zr;
                    zeroi[idnn2] = zi;
                    nn -= 1;
                    for (int i = 0; i <= nn; i++)
                    {
                        pr[i] = qpr[i];
                        pi[i] = qpi[i];
                    }

                    goto search;
                }
                // If the iteration is unsuccessful another shift is chosen
            }
            // if 9 shifts fail, the outer loop is repeated with another sequence of shifts
        }

        // The zerofinder has failed on two major passes
        // return empty handed with the number of roots found (less than the original degree)
        Degree -= nn;


        for (int i = 0; i <= Degree - 1; i++)
        {
            result.Add(new Complex(zeror[i], zeroi[i]));
        }

        return result;
        // throw new ApplicationException("The program could not converge to find all the zeroes, but a prelimenary result with the ones that are found is returned.");

    }


    // COMPUTES  THE DERIVATIVE  POLYNOMIAL AS THE INITIAL H
    // POLYNOMIAL AND COMPUTES L1 NO-SHIFT H POLYNOMIALS.
    //
    private static void noshft(int l1)
    {
        int j = 0;
        int n = 0;
        int nm1 = 0;
        double xni = 0;
        double t1 = 0;
        double t2 = 0;

        n = nn;
        nm1 = n - 1;
        for (int i = 0; i <= n; i++)
        {
            xni = nn - i;
            hr[i] = xni * pr[i] / n;
            hi[i] = xni * pi[i] / n;
        }
        for (int jj = 1; jj <= l1; jj++)
        {
            if ((cmod(hr[n - 1], hi[n - 1]) > eta * 10 * cmod(pr[n - 1], pi[n - 1])))
            {
                cdivid(-pr[nn], -pi[nn], hr[n - 1], hi[n - 1], ref tr, ref ti);
                for (int i = 0; i <= nm1 - 1; i++)
                {
                    j = nn - i - 1;
                    t1 = hr[j - 1];
                    t2 = hi[j - 1];
                    hr[j] = tr * t1 - ti * t2 + pr[j];
                    hi[j] = tr * t2 + ti * t1 + pi[j];
                }
                hr[0] = pr[0];
                hi[0] = pi[0];

            }
            else
            {
                // If the constant term is essentially zero, shift H coefficients
                for (int i = 0; i <= nm1 - 1; i++)
                {
                    j = nn - i - 1;
                    hr[j] = hr[j - 1];
                    hi[j] = hi[j - 1];
                }
                hr[0] = 0;
                hi[0] = 0;
            }
        }
    }

    // COMPUTES L2 FIXED-SHIFT H POLYNOMIALS AND TESTS FOR CONVERGENCE.
    // INITIATES A VARIABLE-SHIFT ITERATION AND RETURNS WITH THE
    // APPROXIMATE ZERO IF SUCCESSFUL.
    // L2 - LIMIT OF FIXED SHIFT STEPS
    // ZR,ZI - APPROXIMATE ZERO IF CONV IS .TRUE.
    // CONV  - LOGICAL INDICATING CONVERGENCE OF STAGE 3 ITERATION
    //
    private static void fxshft(int l2, ref double zr, ref double zi, ref int conv)
    {
        int n = 0;
        int test = 0;
        int pasd = 0;
        int bol = 0;
        double otr = 0;
        double oti = 0;
        double svsr = 0;
        double svsi = 0;

        n = nn;
        polyev(nn, sr, si, pr, pi, qpr, qpi, ref pvr, ref pvi);
        test = 1;
        pasd = 0;

        // Calculate first T = -P(S)/H(S)
        calct(ref bol);

        // Main loop for second stage
        for (int j = 1; j <= l2; j++)
        {
            otr = tr;
            oti = ti;

            // Compute the next H Polynomial and new t
            nexth(bol);
            calct(ref bol);
            zr = sr + tr;
            zi = si + ti;

            // Test for convergence unless stage 3 has failed once or this
            // is the last H Polynomial
            if ((!(bol == 1 | !(test == 1) | j == 12)))
            {
                if ((cmod(tr - otr, ti - oti) < 0.5 * cmod(zr, zi)))
                {

                    if ((pasd == 1))
                    {
                        // The weak convergence test has been passwed twice, start the third stage
                        // Iteration, after saving the current H polynomial and shift
                        for (int i = 0; i <= n - 1; i++)
                        {
                            shr[i] = hr[i];
                            shi[i] = hi[i];
                        }
                        svsr = sr;
                        svsi = si;
                        vrshft(10, ref zr, ref zi, ref conv);
                        if ((conv == 1))
                            return;

                        //The iteration failed to converge. Turn off testing and restore h,s,pv and T
                        test = 0;
                        for (int i = 0; i <= n - 1; i++)
                        {
                            hr[i] = shr[i];
                            hi[i] = shi[i];
                        }
                        sr = svsr;
                        si = svsi;
                        polyev(nn, sr, si, pr, pi, qpr, qpi, ref pvr, ref pvi);
                        calct(ref bol);
                        continue;
                    }
                    pasd = 1;
                }
            }
            else
            {
                pasd = 0;
            }
        }

        // Attempt an iteration with final H polynomial from second stage
        vrshft(10, ref zr, ref zi, ref conv);
    }

    // CARRIES OUT THE THIRD STAGE ITERATION.
    // L3 - LIMIT OF STEPS IN STAGE 3.
    // ZR,ZI   - ON ENTRY CONTAINS THE INITIAL ITERATE, IF THE
    //           ITERATION CONVERGES IT CONTAINS THE FINAL ITERATE ON EXIT.
    // CONV    -  .TRUE. IF ITERATION CONVERGES
    //
    private static void vrshft(int l3, ref double zr, ref double zi, ref int conv)
    {
        int b = 0;
        int bol = 0;
        // Int(i, j)

        double mp = 0;
        double ms = 0;
        double omp = 0;
        double relstp = 0;
        double r1 = 0;
        double r2 = 0;
        double tp = 0;

        conv = 0;
        b = 0;
        sr = zr;
        si = zi;

        // Main loop for stage three

        for (int i = 1; i <= l3; i++)
        {
            // Evaluate P at S and test for convergence
            polyev(nn, sr, si, pr, pi, qpr, qpi, ref pvr, ref pvi);
            mp = cmod(pvr, pvi);
            ms = cmod(sr, si);
            if ((mp <= 20 * errev(nn, qpr, qpi, ms, mp, are, mre)))
            {
                // Polynomial value is smaller in value than a bound onthe error
                // in evaluationg P, terminate the ietartion
                conv = 1;
                zr = sr;
                zi = si;
                return;
            }
            if ((i != 1))
            {

                if ((!(b == 1 | mp < omp | relstp >= 0.05)))
                {
                    // Iteration has stalled. Probably a cluster of zeros. Do 5 fixed 
                    // shift steps into the cluster to force one zero to dominate
                    tp = relstp;
                    b = 1;
                    if ((relstp < eta))
                        tp = eta;
                    r1 = Math.Sqrt(tp);
                    r2 = sr * (1 + r1) - si * r1;
                    si = sr * r1 + si * (1 + r1);
                    sr = r2;
                    polyev(nn, sr, si, pr, pi, qpr, qpi, ref pvr, ref pvi);
                    for (int j = 1; j <= 5; j++)
                    {
                        calct(ref bol);
                        nexth(bol);
                    }

                    omp = infin;
                    goto _20;
                }

                // Exit if polynomial value increase significantly
                if ((mp * 0.1 > omp))
                    return;
            }

            omp = mp;
            _20:

            // Calculate next iterate
            calct(ref bol);
            nexth(bol);
            calct(ref bol);
            if ((!(bol == 1)))
            {
                relstp = cmod(tr, ti) / cmod(sr, si);
                sr += tr;
                si += ti;
            }
        }
    }

    // COMPUTES  T = -P(S)/H(S).
    // BOOL   - LOGICAL, SET TRUE IF H(S) IS ESSENTIALLY ZERO.
    private static void calct(ref int bol)
    {
        // Int(n)
        int n = 0;
        double hvr = 0;
        double hvi = 0;

        n = nn;

        // evaluate h(s)
        polyev(n - 1, sr, si, hr, hi, qhr, qhi, ref hvr, ref hvi);

        if (cmod(hvr, hvi) <= are * 10 * cmod(hr[n - 1], hi[n - 1]))
        {
            bol = 1;
        }
        else
        {
            bol = 0;
        }

        if ((!(bol == 1)))
        {
            cdivid(-pvr, -pvi, hvr, hvi, ref tr, ref ti);
            return;
        }

        tr = 0;
        ti = 0;
    }

    // CALCULATES THE NEXT SHIFTED H POLYNOMIAL.
    // BOOL   -  LOGICAL, IF .TRUE. H(S) IS ESSENTIALLY ZERO
    //

    private static void nexth(int bol)
    {
        int n = 0;
        double t1 = 0;
        double t2 = 0;

        n = nn;
        if ((!(bol == 1)))
        {
            for (int j = 1; j <= n - 1; j++)
            {
                t1 = qhr[j - 1];
                t2 = qhi[j - 1];
                hr[j] = tr * t1 - ti * t2 + qpr[j];
                hi[j] = tr * t2 + ti * t1 + qpi[j];
            }
            hr[0] = qpr[0];
            hi[0] = qpi[0];
            return;
        }

        // If h(s) is zero replace H with qh

        for (int j = 1; j <= n - 1; j++)
        {
            hr[j] = qhr[j - 1];
            hi[j] = qhi[j - 1];
        }
        hr[0] = 0;
        hi[0] = 0;
    }

    // EVALUATES A POLYNOMIAL  P  AT  S  BY THE HORNER RECURRENCE
    // PLACING THE PARTIAL SUMS IN Q AND THE COMPUTED VALUE IN PV.
    //  
    private static void polyev(int nn, double sr, double si, double[] pr, double[] pi, double[] qr, double[] qi, ref double pvr, ref double pvi)
    {
        //{
        //     Int(i)
        double t = 0;

        qr[0] = pr[0];
        qi[0] = pi[0];
        pvr = qr[0];
        pvi = qi[0];

        for (int i = 1; i <= nn; i++)
        {
            t = (pvr) * sr - (pvi) * si + pr[i];
            pvi = (pvr) * si + (pvi) * sr + pi[i];
            pvr = t;
            qr[i] = pvr;
            qi[i] = pvi;
        }
    }

    // BOUNDS THE ERROR IN EVALUATING THE POLYNOMIAL BY THE HORNER RECURRENCE.
    // QR,QI - THE PARTIAL SUMS
    // MS    -MODULUS OF THE POINT
    // MP    -MODULUS OF POLYNOMIAL VALUE
    // ARE, MRE -ERROR BOUNDS ON COMPLEX ADDITION AND MULTIPLICATION
    //
    private static double errev(int nn, double[] qr, double[] qi, double ms, double mp, double are, double mre)
    {
        //{
        //     Int(i)
        double e = 0;

        e = cmod(qr[0], qi[0]) * mre / (are + mre);
        for (int i = 0; i <= nn; i++)
        {
            e = e * ms + cmod(qr[i], qi[i]);
        }

        return e * (are + mre) - mp * mre;
    }

    // CAUCHY COMPUTES A LOWER BOUND ON THE MODULI OF THE ZEROS OF A
    // POLYNOMIAL - PT IS THE MODULUS OF THE COEFFICIENTS.
    //
    private static void cauchy(int nn, double[] pt, double[] q, ref double fn_val)
    {
        int n = 0;
        double x = 0;
        double xm = 0;
        double f = 0;
        double dx = 0;
        double df = 0;

        pt[nn] = -pt[nn];

        // Compute upper estimate bound
        n = nn;
        x = Math.Exp(Math.Log(-pt[nn]) - Math.Log(pt[0])) / n;
        if ((pt[n - 1] != 0))
        {
            //// Newton step at the origin is better, use it
            xm = -pt[nn] / pt[n - 1];
            if ((xm < x))
                x = xm;
        }

        // Chop the interval (0,x) until f < 0

        while ((true))
        {
            xm = x * 0.1;
            f = pt[0];
            for (int i = 1; i <= nn; i++)
            {
                f = f * xm + pt[i];
            }
            if ((f <= 0))
                break; // TODO: might not be correct. Was : Exit While
            x = xm;
        }
        dx = x;

        // Do Newton iteration until x converges to two decimal places
        while ((Math.Abs(dx / x) > 0.005))
        {
            q[0] = pt[0];
            for (int i = 1; i <= nn; i++)
            {
                q[i] = q[i - 1] * x + pt[i];
            }
            f = q[nn];
            df = q[0];
            for (int i = 1; i <= n - 1; i++)
            {
                df = df * x + q[i];
            }
            dx = f / df;
            x -= dx;
        }

        fn_val = x;
    }

    // RETURNS A SCALE FACTOR TO MULTIPLY THE COEFFICIENTS OF THE POLYNOMIAL.
    // THE SCALING IS DONE TO AVOID OVERFLOW AND TO AVOID UNDETECTED UNDERFLOW
    // INTERFERING WITH THE CONVERGENCE CRITERION.  THE FACTOR IS A POWER OF THE
    // BASE.
    // PT - MODULUS OF COEFFICIENTS OF P
    // ETA, INFIN, SMALNO, BASE - CONSTANTS DESCRIBING THE FLOATING POINT ARITHMETIC.
    //
    private static double scale(int nn, double[] pt, double eta, double infin, double smalno, double @base)
    {
        //{
        //     Int(i, l)
        int l = 0;
        double hi = 0;
        double lo = 0;
        double max = 0;
        double min = 0;
        double x = 0;
        double sc = 0;
        double fn_val = 0;

        // Find largest and smallest moduli of coefficients
        hi = Math.Sqrt(infin);
        lo = smalno / eta;
        max = 0;
        min = infin;

        for (int i = 0; i <= nn; i++)
        {
            x = pt[i];
            if ((x > max))
                max = x;
            if ((x != 0 & x < min))
                min = x;
        }

        // Scale only if there are very large or very small components
        fn_val = 1;
        if ((min >= lo & max <= hi))
            return fn_val;
        x = lo / min;
        if ((x <= 1))
        {
            sc = 1 / (Math.Sqrt(max) * Math.Sqrt(min));
        }
        else
        {
            sc = x;
            if ((infin / sc > max))
                sc = 1;
        }
        l = Convert.ToInt32(Math.Log(sc) / Math.Log(@base) + 0.5);
        fn_val = Math.Pow(@base, l);
        return fn_val;
    }

    // COMPLEX DIVISION C = A/B, AVOIDING OVERFLOW.
    //
    private static void cdivid(double ar, double ai, double br, double bi, ref double cr, ref double ci)
    {
        double r = 0;
        double d = 0;
        double t = 0;
        double infin = 0;

        if ((br == 0 & bi == 0))
        {
            // Division by zero, c = infinity
            mcon(ref t, ref infin, ref t, ref t);
            cr = infin;
            ci = infin;
            return;
        }

        if ((Math.Abs(br) < Math.Abs(bi)))
        {
            r = br / bi;
            d = bi + r * br;
            cr = (ar * r + ai) / d;
            ci = (ai * r - ar) / d;
            return;
        }

        r = bi / br;
        d = br + r * bi;
        cr = (ar + ai * r) / d;
        ci = (ai - ar * r) / d;
    }

    // MODULUS OF A COMPLEX NUMBER AVOIDING OVERFLOW.
    //
    private static double cmod(double r, double i)
    {
        double ar = 0;
        double ai = 0;

        ar = Math.Abs(r);
        ai = Math.Abs(i);
        if ((ar < ai))
        {
            return ai * Math.Sqrt(1.0 + Math.Pow((ar / ai), 2.0));

        }
        else if ((ar > ai))
        {
            return ar * Math.Sqrt(1.0 + Math.Pow((ai / ar), 2.0));
        }
        else
        {
            return ar * Math.Sqrt(2.0);
        }
    }
    // MCON PROVIDES MACHINE CONSTANTS USED IN VARIOUS PARTS OF THE PROGRAM.
    // THE USER MAY EITHER SET THEM DIRECTLY OR USE THE STATEMENTS BELOW TO
    // COMPUTE THEM. THE MEANING OF THE FOUR CONSTANTS ARE -
    // ETA       THE MAXIMUM RELATIVE REPRESENTATION ERROR WHICH CAN BE DESCRIBED
    //           AS THE SMALLEST POSITIVE FLOATING-POINT NUMBER SUCH THAT
    //           1.0_dp + ETA > 1.0.
    // INFINY    THE LARGEST FLOATING-POINT NUMBER
    // SMALNO    THE SMALLEST POSITIVE FLOATING-POINT NUMBER
    // BASE      THE BASE OF THE FLOATING-POINT NUMBER SYSTEM USED
    //

    private static void mcon(ref double eta, ref double infiny, ref double smalno, ref double @base)
    {
        @base = DBL_RADIX;
        eta = DBL_EPSILON;
        infiny = DBL_MAX;
        smalno = DBL_MIN;
    }
}
