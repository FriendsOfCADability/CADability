using CADability.Curve2D;
using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra.Double;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MathNet.Numerics.LinearAlgebra.Factorization;

namespace CADability
{
    /// <summary>
    /// Dient zur Manipulation von Polynomkoeffizienten, bevor aus Polynomen die Wurzel gezogen wird
    /// </summary>
    internal class PolynomSingleVariable
    {
        // z.B. a*x³ + b*x² + c*x +d
        // Der Exponent ist der Index in coeff (das geht also von hinten nach vorne, d ist coeff[0])
        double[] c;
        public PolynomSingleVariable()
        {
            this.c = new double[0];

        }
        public PolynomSingleVariable(params double[] coeff)
        {
            this.c = coeff;

        }
        public int Length
        {
            get
            {
                return c.Length;
            }
        }
        public double this[int i]
        {
            get
            {
                return c[i];
            }
        }
        public static implicit operator double[](PolynomSingleVariable p)
        {
            return p.c;
        }
        public static PolynomSingleVariable operator *(PolynomSingleVariable a, PolynomSingleVariable b)
        {
            double[] res = new double[a.Length + b.Length - 1];
            Array.Clear(res, 0, res.Length);
            for (int i = 0; i < a.Length; i++)
            {
                for (int j = 0; j < b.Length; j++)
                {
                    res[i + j] += a[i] * b[j];
                }
            }
            return new PolynomSingleVariable(res);
        }
        public static PolynomSingleVariable operator +(PolynomSingleVariable a, PolynomSingleVariable b)
        {   // geht auch, wenn eines von beiden die Länge 0 hat
            double[] res = new double[Math.Max(a.Length, b.Length)];
            Array.Clear(res, 0, res.Length);
            for (int i = 0; i < res.Length; i++)
            {
                if (i < a.Length) res[i] += a[i];
                if (i < b.Length) res[i] += b[i];
            }
            return new PolynomSingleVariable(res);
        }
        public static PolynomSingleVariable operator ^(PolynomSingleVariable a, int n)
        {
            if (n == 0) return new PolynomSingleVariable(1.0); //??
            double[] res = new double[(a.Length - 1) * n + 1];
            double[] b = new double[(a.Length - 1) * n + 1];
            Array.Clear(res, 0, res.Length);
            Array.Copy(a, res, a.Length); // das ist hoch 1;
            for (int k = 1; k < n; k++)
            {
                Array.Copy(res, b, k * (a.Length - 1) + 1); // b ist das Zwischenergbenis
                for (int i = 0; i < a.Length; i++)
                {
                    for (int j = 0; j < k * (a.Length - 1) + 1; j++)
                    {
                        res[i + j] += a[i] * b[j];
                    }
                }
            }
            return new PolynomSingleVariable(res);
        }
    }

    /* Buchbergers Algorithmus:
    * https://pdfs.semanticscholar.org/fd92/3828a3bc450b4b8d003f48e63e6c570951b0.pdf
    */


    /// <summary>
    /// A single term of the polynom
    /// </summary>
    public class monom
    {   // ein Summand des Polynoms
        /// <summary>
        /// the exponent for each variable, e.g. {0,2,1} means "y²*z"
        /// </summary>
        public int[] exp; // die Exponenten der Variablen
        /// <summary>
        /// index into the coefficient array c of the polynom
        /// </summary>
        public int index; // der Index in das Koeffizientenarray c
        private Polynom owner;
        public double Coefficient
        {
            get
            {
                return owner.Coeff(index);
            }
        }
        internal monom(int dim, Polynom owner)
        {
            exp = new int[dim];
            this.owner = owner;
        }
    }
    /// <summary>
    /// A Polynom in multiple variables, often named x,y,z, but may be any dimension.
    /// e.g. a*x³ + b*x²*y + c*x*y² +d*y³ + e*x² + f*x*y + g*y² + h*x + i*y + j (deg(ree)==3, dim(ension)==2: (x,y))
    /// </summary>
    public class Polynom : IEnumerable<monom>
    {
        int deg; // höchste Potenz, Summe aller Potenzen eines Mnoms (Summanden)
        int dim; // Anzahl der Variablen
        double[] c; // die Koeffizienten, bei eindimensionalen ist c[n] der Koeffizient für x^n 

        /// <summary>
        /// Creates an empty polynom with all coefficients set to 0, prepared to have a maximum degree of deg
        /// and a dimension of dim (number of variables)
        /// </summary>
        /// <param name="deg">the maximum degree</param>
        /// <param name="dim">number of variables</param>
        public Polynom(int deg, int dim)
        {
            this.deg = deg;
            this.dim = dim;
            int num = deg + 1;
            for (int i = 1; i < dim; i++) num *= deg + 1;
            c = new double[num]; // das sind mehr Koeffizienten, als man braucht, da auch solche dabei sind, bei denen die Summe der Potenzen größer als deg ist, aber so ist es am einfachsten zu indizieren
        }
        /// <summary>
        /// Creates a constant Polynom (often 1 or 0) with the provided dimension
        /// </summary>
        /// <param name="cv"></param>
        /// <param name="dim"></param>
        public Polynom(double cv, int dim)
        {
            this.dim = dim;
            this.deg = 0;
            c = new double[1];
            c[0] = cv;

        }
        static public Polynom SingleDimLinear(double c0, double c1)
        {
            Polynom res = new Polynom(1, 1);
            res.c[0] = c0;
            res.c[1] = c1;
            return res;
        }
        static public Polynom SingleDim(params double[] c)
        {   // to be implemented
            throw new NotImplementedException();
        }
        /// <summary>
        /// Creates a polynom as specified by the parameters.
        /// Expects input like
        /// (3, "x2", 4, "x", 5, "", 6, "y2", 7, "z", 8, "x2y")
        /// a alternating sequence of double values and strings. The charcters in the strings are interpreted as follows:
        /// A letter, which is the name of the variable, followed by an optional digit, which is the exponent
        /// of the variable (when missing "1" is assumed).
        /// An empty string is for the constant. The number of different variable names defines the dimension.
        /// The highest degree is derived from the exponents. The indices of the variables are in alphabetical order.
        /// The variable names have no further meaning and are not conserved.
        /// </summary>
        /// <param name="def">the definition of the polynom</param>
        public Polynom(params object[] def)
        {
            if ((def.Length & 0x01) != 0) throw new ApplicationException("MultiPolynom: odd number of parameters not allowed");
            SortedSet<char> variableNames = new SortedSet<char>();
            for (int i = 1; i < def.Length; i += 2)
            {
                string sexp = def[i] as string;
                if (sexp == null) throw new ApplicationException("MultiPolynom: every second parameter must be a string");
                for (int j = 0; j < sexp.Length; j++)
                {
                    if (char.IsLetter(sexp[j])) variableNames.Add(sexp[j]);
                }
            }
            SortedDictionary<char, int> variableDict = new SortedDictionary<char, int>();
            int ind = 0;
            foreach (char item in variableNames)
            {
                variableDict[item] = ind++;
            }
            dim = variableNames.Count;
            Dictionary<int[], double> monoms = new Dictionary<int[], double>();
            int maxdeg = 0;
            for (int i = 0; i < def.Length; i += 2)
            {
                string sexp = def[i + 1] as string;
                int[] exp = new int[dim];
                char act = '.';
                int sumdeg = 0;
                for (int j = 0; j < sexp.Length; j++)
                {
                    if (char.IsLetter(sexp[j]))
                    {   // z.B. "xy"
                        if (act != '.')
                        {
                            exp[variableDict[act]] = 1;
                            sumdeg += 1;
                        }
                        act = sexp[j];
                    }
                    else if (char.IsDigit(sexp[j]))
                    {
                        int e = (int)sexp[j] - (int)'0';
                        if (act != '.')
                        {
                            exp[variableDict[act]] = e;
                            sumdeg += e;
                        }
                        act = '.';
                    }
                }
                if (act != '.')
                {
                    exp[variableDict[act]] = 1;
                    sumdeg += 1;
                }
                maxdeg = Math.Max(maxdeg, sumdeg);
                if (def[i] is int)
                    monoms[exp] = (int)(def[i]);
                else
                    monoms[exp] = (double)(def[i]);
            }
            deg = maxdeg;
            int num = deg + 1;
            for (int i = 1; i < dim; i++) num *= deg + 1;
            c = new double[num];
            foreach (KeyValuePair<int[], double> m in monoms)
            {
                Set(m.Value, m.Key);
            }
        }
        /// <summary>
        /// Creates a polynom with a single monom where m are the exponents and v is the facctor
        /// </summary>
        /// <param name="m"></param>
        /// <param name="v"></param>
        public Polynom(int[] m, double v)
        {
            deg = 0;
            for (int i = 0; i < m.Length; i++)
            {
                deg += m[i];
            }
            dim = m.Length;
            int num = deg + 1;
            for (int i = 1; i < dim; i++) num *= deg + 1;
            c = new double[num]; // das sind mehr Koeffizienten, als man braucht, da auch solche dabei sind, bei denen die Summe der Potenzen   
            c[index(m)] = v;
        }
        //public Polynom(params double[] coeff)
        //{
        //    dim = 1;
        //    deg = coeff.Length - 1;
        //    int num = coeff.Length;
        //    c = new double[num];
        //    for (int i = 0; i < coeff.Length; i++)
        //    {
        //        c[i] = coeff[i];
        //    }
        //}
        public int Degree
        {
            get
            {
                return deg;
            }
        }

        public int Dimension { get { return dim; } }

        public int[] LeadingTerm
        {
            get
            {
                int[] res = new int[dim]; // preset to (0,...,0)
                int sum = 0;
                foreach (monom m in this)
                {
                    if (m.Coefficient != 0.0)
                    {
                        int ms = 0;
                        for (int i = 0; i < dim; i++) ms += m.exp[i];
                        if (ms > sum)
                        {
                            sum = ms;
                            res = m.exp.Clone() as int[];
                        }
                        else if (ms == sum)
                        {
                            bool isHigher = false;
                            for (int i = 0; i < dim; i++)
                            {
                                if (m.exp[i] > res[i])
                                {
                                    isHigher = true;
                                    break;
                                }
                                else if (m.exp[i] < res[i]) break;
                            }
                            if (isHigher)
                            {
                                res = m.exp.Clone() as int[];
                            }
                        }
                    }
                }
                return res;
            }
        }

        private int index(int[] exp)
        {
            // liefert den index in c für die Kombination von exponenten
            // exp muss Länge dim haben
            int res = 0;
            int f = 1;
            for (int i = 0; i < dim; i++)
            {
                res += f * exp[i];
                f *= (deg + 1);
            }
            return res;
        }
        private int[] reverseindex(int ind)
        {
            int[] res = new int[dim];
            for (int i = 0; i < dim; i++)
            {
                ind = Math.DivRem(ind, deg + 1, out res[i]);
            }
            return res;
        }
        //private IEnumerable<monom> submonomials(monom current, int dimindex, int sum, int index)
        //{
        //    int f = 1;
        //    for (int i = 0; i < dimindex; i++) f *= (deg + 1);
        //    if (dimindex == dim - 1)
        //    {   // letzte Variable, hier wird geliefert
        //        for (int i = 0; i <= deg - sum; i++)
        //        {
        //            current.exp[dimindex] = i;
        //            current.index = index;
        //            index += f;
        //            yield return current;
        //        }
        //    }
        //    else
        //    {
        //        for (int i = 0; i <= deg - sum; i++)
        //        {
        //            current.exp[dimindex] = i;
        //            foreach (monom m in submonomials(current, dimindex + 1, sum + i, index))
        //            {
        //                yield return m;
        //            }
        //            index += f;
        //        }
        //    }
        //}
        //internal IEnumerable<monom> monomialsrec()
        //{
        //    monom res = new monom(dim, this); // nur einmal allokiert, wird immer geliefert
        //    foreach (monom m in submonomials(res, 0, 0, 0)) yield return m;
        //}
        private bool[] UsedDimensions()
        {
            bool[] res = new bool[dim];
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] != 0.0)
                {
                    int rem = i;
                    for (int j = 0; j < dim; j++)
                    {
                        int k;
                        rem = Math.DivRem(rem, deg + 1, out k);
                        res[j] |= k > 0;
                    }
                }
            }
            return res;
        }
        /// <summary>
        /// Multiplies two Polynoms, which must have the same dimension
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Polynom operator *(Polynom a, Polynom b)
        {
            System.Diagnostics.Debug.Assert(a.dim == b.dim);
            Polynom res = new Polynom(a.deg + b.deg, a.dim);
            foreach (monom ma in a)
            {
                foreach (monom mb in b)
                {
                    int[] exp = new int[a.dim];
                    for (int i = 0; i < exp.Length; i++)
                    {
                        exp[i] = ma.exp[i] + mb.exp[i];
                    }
                    res.AddCoeff(exp, a.c[ma.index] * b.c[mb.index]);
                }
            }
            res.reduce();
            return res;
        }
        public static Polynom operator *(double a, Polynom b)
        {
            Polynom res = new Polynom(b.deg, b.dim);
            foreach (monom mb in b)
            {
                res.Set(a * b.c[mb.index], mb.exp);
            }
            res.reduce();
            return res;
        }
        public static Polynom operator /(Polynom b, double a) => (1 / a) * b;
        public static Polynom operator *(Polynom b, double a) => a * b;
        public static Polynom operator ^(Polynom b, int n)
        {
            if (n == 2) return b * b;
            else
            {
                Polynom res = b;
                for (int i = 0; i < n; i++)
                {
                    res = res * b;
                }
                return res;
            }
        }
        /// <summary>
        /// Adds two Polynoms, which must have the same dimension
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Polynom operator +(Polynom a, Polynom b)
        {
            System.Diagnostics.Debug.Assert(a.dim == b.dim);
            Polynom res = new Polynom(Math.Max(a.deg, b.deg), a.dim);
            foreach (monom ma in a)
            {
                res.AddCoeff(ma.exp, a.c[ma.index]);
            }
            foreach (monom mb in b)
            {
                res.AddCoeff(mb.exp, b.c[mb.index]);
            }
            return res;
        }
        public static Polynom operator -(Polynom a, Polynom b)
        {
            System.Diagnostics.Debug.Assert(a.dim == b.dim);
            Polynom res = new Polynom(Math.Max(a.deg, b.deg), a.dim);
            foreach (monom ma in a)
            {
                res.AddCoeff(ma.exp, a.c[ma.index]);
            }
            foreach (monom mb in b)
            {
                res.AddCoeff(mb.exp, -b.c[mb.index]);
            }
            return res;
        }
        public static Polynom operator -(Polynom a)
        {
            Polynom res = a.Clone();
            for (int i = 0; i < res.c.Length; i++)
            {
                if (res.c[i] != 0)
                    res.c[i] = -res.c[i];
            }
            return res;
        }
        public static Polynom operator -(Polynom a, double b)
        {
            return a - new Polynom(b, a.dim);
        }
        public static Polynom operator +(Polynom a, double b)
        {
            return a + new Polynom(b, a.dim);
        }
        public static Polynom operator -(double b, Polynom a) => -(a - b);
        public static Polynom operator +(double b, Polynom a) => a + b;

        // Subtracts polynom b from this and returns the difference. When two coefficients are close to each other, the coefficient is set to 0
        public Polynom Subtract(Polynom b)
        {
            System.Diagnostics.Debug.Assert(dim == b.dim);
            Polynom res = new Polynom(Math.Max(deg, b.deg), dim);
            foreach (monom ma in this)
            {
                res.AddCoeff(ma.exp, c[ma.index]);
            }
            foreach (monom mb in b)
            {   // the coefficients are close if only the last 4 bits of the 48 bit mantissa are different
                // not sure, whether this is a good value, but it is independant of the magnitude of the coefficient
                if (Math.Abs((BitConverter.DoubleToInt64Bits(c[index(mb.exp)]) >> 1) - (BitConverter.DoubleToInt64Bits(b.c[mb.index]) >> 1)) > 16)
                    res.AddCoeff(mb.exp, -b.c[mb.index]);
                else
                    res.Set(0.0, mb.exp);
            }
            return res;
        }
        /// <summary>
        /// Adds the provided Polynom to this Polynom. The degree of the provided Polynom may not exceed the degree of this Polynom.
        /// </summary>
        /// <param name="b"></param>
        public void Add(Polynom b)
        {
            System.Diagnostics.Debug.Assert(dim == b.dim);
            foreach (monom mb in b)
            {
                AddCoeff(mb.exp, b.c[mb.index]);
            }
        }
        /// <summary>
        /// Reduces the degree of the Polynom, when the highest degree of all terms is less than the degree of this Polynom.
        /// Saves space.
        /// </summary>
        public void reduce()
        {
            // falls deg zu hoch ist, umwandeln in ein MultiPolynom mit maximalem deg
            int maxdeg = 0;
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] != 0.0)
                {
                    int[] exp = reverseindex(i);
                    int sum = 0;
                    for (int j = 0; j < dim; j++) sum += exp[j];
                    if (sum > maxdeg) maxdeg = sum;
                }
            }
            if (maxdeg < deg)
            {
                int num = maxdeg + 1;
                for (int i = 1; i < dim; i++) num *= maxdeg + 1;
                double[] cc = new double[num];
                Array.Clear(cc, 0, cc.Length);
                for (int i = 0; i < c.Length; i++)
                {
                    if (c[i] != 0.0)
                    {
                        int[] exp = reverseindex(i); // reverseindex nach altem degree, ccind nach neuem degree
                        int ccind = 0;
                        int f = 1;
                        for (int j = 0; j < dim; j++)
                        {
                            ccind += f * exp[j];
                            f *= (maxdeg + 1);
                        }
                        cc[ccind] = c[i];
                    }
                }
                deg = maxdeg;
                c = cc;
            }
        }
        public void NormalizeLeadingTerm()
        {
            int[] lt = LeadingTerm;
            double f = Get(lt);
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] != 0.0)
                {
                    c[i] /= f;
                }
            }
            Set(1.0, lt); // not necessary
        }
        /// <summary>
        /// Replaces the variables by new polynomes and calculates the resulting Polynom. The number of provided polynoms must match the number of variables of this Polynom, which is the degree.
        /// </summary>
        /// <param name="substWith"></param>
        /// <returns></returns>
        public Polynom Substitute(params Polynom[] substWith)
        {
            int md = 0;
            for (int i = 0; i < substWith.Length; i++) md = Math.Max(md, substWith[i].deg);
            int resdim = substWith[0].dim;
            // substwith müssen alle die geiche dim haben
            for (int i = 0; i < substWith.Length; i++)
            {
                System.Diagnostics.Debug.Assert(substWith[i].dim == resdim);
            }
            Polynom res = new Polynom(deg * md, resdim);
            foreach (monom m0 in this)
            {
                Polynom tmp = new Polynom(c[m0.index], resdim); // die Konstante
                for (int i = 0; i < m0.exp.Length; i++)
                {
                    for (int j = 0; j < m0.exp[i]; j++)
                    {
                        tmp = tmp * substWith[i];
                    }
                }
                res.Add(tmp);
            }
            return res;
        }
        public Polynom SubstituteRational(Polynom px, Polynom py, Polynom pz, Polynom pw)
        {
            if (px.dim != 1) throw new ApplicationException("SubstituteRational works only with one dimensional polynoms");
            if (py.dim != 1) throw new ApplicationException("SubstituteRational works only with one dimensional polynoms");
            if (pz.dim != 1) throw new ApplicationException("SubstituteRational works only with one dimensional polynoms");
            if (this.dim != 3) throw new ApplicationException("SubstituteRational works only on three dimensional polynoms");
            int md = 0;
            md = Math.Max(md, px.deg);
            md = Math.Max(md, py.deg);
            md = Math.Max(md, pz.deg);
            md = Math.Max(md, pw.deg);
            int resdim = px.dim;
            Polynom resw = new Polynom(deg * md, 2);
            // die Polynome werden erweitert um eine 2. Variable, die für 1/pw steht
            // diese wird in jedes Term mit Faktor 1 hineinmultipliziert
            Polynom pxw = new Polynom(px.deg, 2);
            Polynom pyw = new Polynom(py.deg, 2);
            Polynom pzw = new Polynom(pz.deg, 2);
            foreach (monom m0 in px)
            {
                pxw.Set(px.c[m0.index], m0.exp[0], 1);
            }
            foreach (monom m0 in py)
            {
                pyw.Set(py.c[m0.index], m0.exp[0], 1);
            }
            foreach (monom m0 in pz)
            {
                pzw.Set(pz.c[m0.index], m0.exp[0], 1);
            }
            foreach (monom m0 in this)
            {
                Polynom tmp = new Polynom(c[m0.index], 2); // die Konstante
                for (int j = 0; j < m0.exp[0]; j++)
                {
                    tmp = tmp * pxw;
                }
                for (int j = 0; j < m0.exp[1]; j++)
                {
                    tmp = tmp * pyw;
                }
                for (int j = 0; j < m0.exp[2]; j++)
                {
                    tmp = tmp * pzw;
                }

                resw.Add(tmp);
            }
            // res ist jetzt ein Polynom, das eine Variable enthält, die für 1/pw steht
            // jetzt wird mit dem höchsten Exponenten dieser Variablen durchmultipliziert (keine Auswirkung auf Nullstelle)
            int emax = 0;
            foreach (monom m in resw)
            {
                emax = Math.Max(emax, m.exp[1]);
            }

            Polynom res = new Polynom(resw.deg * emax * pw.deg, 1);
            // pw wird jetzt auf jeden term emax mal aufmultipliziert, es sei denn es steht im Nenner,
            // dann wird quasi gekürzt
            foreach (monom m in resw)
            {
                Polynom tmp = new Polynom(res.deg, 1);
                tmp.Set(resw.c[m.index], m.exp[0]); // der Term ohne das 1/pw
                for (int i = m.exp[1]; i < emax; i++)
                {
                    tmp = tmp * pw;
                }
                res.Add(tmp);
            }

            return res;
        }
        public override string ToString()
        {
            StringBuilder res = new StringBuilder();
            string varnames = "xyzabcdefgh";
            foreach (monom m in this)
            {
                if (c[m.index] == 0.0) continue;
                if (res.Length > 0) res.Append(" + ");
                res.Append(c[m.index].ToString("G4"));
                for (int i = 0; i < m.exp.Length; i++)
                {
                    if (m.exp[i] > 0)
                    {
                        if (m.exp[i] == 1) res.Append("*" + varnames[i]);
                        else if (m.exp[i] == 2) res.Append("*" + varnames[i] + "²");
                        else if (m.exp[i] == 3) res.Append("*" + varnames[i] + "³");
                        else res.Append("*" + varnames[i] + "^" + m.exp[i].ToString());
                    }
                }

            }
            return res.ToString();
        }
        /// <summary>
        /// Sets the factor for the term specified by the parameter
        /// </summary>
        /// <param name="v"></param>
        /// <param name="exp"></param>
        public void Set(double v, params int[] exp)
        {
            c[index(exp)] = v;
        }
        public double Get(params int[] exp)
        {
            return c[index(exp)];
        }
        private void AddCoeff(int[] exp, double v)
        {
            c[index(exp)] += v;
        }
        /// <summary>
        /// Calculates the (real) roots of this Polynom. The Polynom must be 1-dimensional
        /// </summary>
        /// <returns></returns>
        public double[] Roots()
        {
            System.Diagnostics.Debug.Assert(dim == 1);
            reduce(); // höchster degree != 0
            double[] frparams = new double[c.Length];
            for (int i = 0; i <= deg; ++i)
            {
                frparams[deg - i] = c[index(new int[] { i })];
            }
            try
            {
                List<double> res = RealPolynomialRootFinder.FindRoots(frparams);
                return res.ToArray();
            }
            catch (ApplicationException)
            {
                return new double[0];
            }
        }
        /// <summary>
        /// Modifies the Polynom (which must be 3-dimensional) by the provided Modop
        /// </summary>
        /// <param name="m"></param>
        public Polynom Modified(ModOp m)
        {
            System.Diagnostics.Debug.Assert(dim == 3);
            Polynom[] subst = new Polynom[3];
            for (int i = 0; i < 3; i++)
            {
                subst[i] = new Polynom(1, 3);
                for (int j = 0; j < 3; j++)
                {
                    subst[i].Set(m[i, j], Convert.ToInt32(j == 0), Convert.ToInt32(j == 1), Convert.ToInt32(j == 2));
                }
                subst[i].Set(m[i, 3], 0, 0, 0);
            }
            // analog zu:
            //subst[0] = new Polynom(m.Item(0, 0), "x", m.Item(0, 1), "y", m.Item(0, 2), "z", m.Item(0, 3), "");
            //subst[1] = new Polynom(m.Item(1, 0), "x", m.Item(1, 1), "y", m.Item(1, 2), "z", m.Item(1, 3), "");
            //subst[2] = new Polynom(m.Item(2, 0), "x", m.Item(2, 1), "y", m.Item(2, 2), "z", m.Item(2, 3), "");
            return Substitute(subst);
        }
        /// <summary>
        /// Modifies the Polynom (which must be 2-dimensional) by the provided Modop
        /// </summary>
        /// <param name="m"></param>
        public Polynom Modified(ModOp2D m)
        {
            System.Diagnostics.Debug.Assert(dim == 2);
            Polynom[] subst = new Polynom[2];
            double[,] mm = m.Matrix;
            for (int i = 0; i < 2; i++)
            {
                subst[i] = new Polynom(1, 2);
                for (int j = 0; j < 2; j++)
                {
                    subst[i].Set(mm[i, j], Convert.ToInt32(j == 0), Convert.ToInt32(j == 1));
                }
                subst[i].Set(mm[i, 2], 0, 0);
            }
            return Substitute(subst);
        }
        /// <summary>
        /// Inserts the values for the variables and calculates the result of the Polynom
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public double Eval(params double[] values)
        {
            if (values.Length != dim) throw new ApplicationException("Eval: there must be " + dim.ToString() + " values in the parameter");
            double res = 0.0;
            foreach (monom m in this)
            {
                double f = c[m.index];
                for (int i = 0; i < dim; i++)
                {
                    for (int j = 0; j < m.exp[i]; j++)
                    {
                        f *= values[i];
                    }
                }
                res += f;
            }
            return res;
        }
        /// <summary>
        /// reverses all variables, replaces x by -x, y by -y,  etc.
        /// </summary>
        /// <returns></returns>
        public Polynom Reversed()
        {
            Polynom[] subst = new Polynom[dim];
            for (int i = 0; i < dim; i++) subst[i] = new Polynom(-1, "x");
            return Substitute(subst);
        }
        public Polynom Offset(params double[] off)
        {
            Polynom[] subst = new Polynom[dim];
            for (int i = 0; i < dim; i++)
            {
                if (i < off.Length) subst[i] = new Polynom(1, "x", -off[i], "");
                else subst[i] = new Polynom(0, 1);
            }
            return Substitute(subst);
        }
        public Polynom Clone()
        {
            Polynom res = new Polynom();
            res.dim = dim;
            res.deg = deg;
            res.c = (double[])c.Clone();
            return res;
        }
        public Polynom Derivate(params int[] diff)
        {
            int ddeg = deg;
            for (int i = 0; i < diff.Length; i++)
            {
                ddeg -= diff[i];
            }
            if (ddeg < 0) ddeg = 0;
            Polynom res = new Polynom(ddeg, dim);
            foreach (monom m in this)
            {
                double f = m.Coefficient;
                int[] exp = (int[])m.exp.Clone();
                for (int i = 0; i < diff.Length; i++)
                {
                    for (int j = 0; j < diff[i]; j++)
                    {
                        f *= exp[i];
                        exp[i] = Math.Max(0, exp[i] - 1);
                    }
                }
                if (f != 0.0) res.Set(f, exp);
            }
            return res;
        }
        /// <summary>
        /// Returns the integrated polynom 
        /// </summary>
        /// <param name="intg">variables to integrate</param>
        /// <returns></returns>
        public Polynom Integrate(params int[] intg)
        {
            int ddeg = deg;
            for (int i = 0; i < intg.Length; i++)
            {
                ddeg += intg[i];
            }
            Polynom res = new Polynom(ddeg, dim);
            foreach (monom m in this)
            {
                double f = m.Coefficient;
                int[] exp = (int[])m.exp.Clone();
                for (int i = 0; i < intg.Length; i++)
                {
                    for (int j = 0; j < intg[i]; j++)
                    {
                        ++exp[i];
                        f /= exp[i];
                    }
                }
                res.Set(f, exp);
            }
            return res;
        }

        public IEnumerator<monom> GetEnumerator()
        {
            monom m = new monom(dim, this);
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] != 0.0)
                {
                    m.index = i;
                    int rem = i;
                    for (int j = 0; j < dim; j++)
                    {
                        int k;
                        rem = Math.DivRem(rem, deg + 1, out k);
                        m.exp[j] = k;
                    }
                    yield return m;
                }
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            monom m = new monom(dim, this);
            for (int i = 0; i < c.Length; i++)
            {
                if (c[i] != 0.0)
                {
                    m.index = i;
                    int rem = i;
                    for (int j = 0; j < dim; j++)
                    {
                        int k;
                        rem = Math.DivRem(rem, deg + 1, out k);
                        m.exp[j] = k;
                    }
                    yield return m;
                }
            }
        }
        internal double Coeff(int index)
        {
            return c[index];
        }
        public Polynom Divide(Polynom devideBy, out Polynom remainder)
        {
            if (dim != 1 || devideBy.dim != 1) throw new ApplicationException("Polynom Divide: polynoms must be onedimensional");
            Polynom res = new Polynom(0, 1);
            remainder = Clone();
            while (remainder.Degree >= Degree)
            {
                double f = remainder.c[remainder.deg] / devideBy.c[devideBy.deg];
                // res.Add(new Polynom())
            }
            return res;
        }
        public static ModOp2D LagrangeReduction2(Polynom polynom)
        {
            if (polynom.Degree != 2) return ModOp2D.Null;

            Matrix A = new DenseMatrix(2, 2);
            A[0, 0] = polynom.Get(2, 0);
            A[1, 1] = polynom.Get(0, 2);
            A[0, 1] = A[1, 0] = polynom.Get(1, 1) / 2.0;
            Evd<double> AEigen = A.Evd();
            Matrix ev = (Matrix)AEigen.EigenVectors;
            ModOp2D m1 = new ModOp2D(ev.ToArray());
            Polynom mod1 = polynom.Modified(m1); // the resulting polynom should not have a mixed term x*y 
            mod1.Set(0.0, 1, 1);// these value should be almost zero anyhow.
            double tx = -mod1.Get(1, 0) / (2 * mod1.Get(2, 0));
            double ty = -mod1.Get(0, 1) / (2 * mod1.Get(0, 2));
            ModOp2D m2 = ModOp2D.Translate(tx, ty);
            Polynom mod2 = mod1.Modified(m2); // // the resulting polynom should have no linear terms
            mod2.Set(0.0, 1, 0); // these values should be almost zero anyhow.
            mod2.Set(0.0, 0, 1);
            // mod2 only has x² and y² (and a constant), all other coefficients are 0
            // x² should have the greatest coefficient
            ModOp2D m3;
            double cx = Math.Abs(mod2.Get(2, 0));
            double cy = Math.Abs(mod2.Get(0, 2));
            double scale;
            if (cx > cy)
            {
                scale = mod2.Get(2, 0);
                m3 = new ModOp2D(GeoVector2D.XAxis, GeoVector2D.YAxis, GeoPoint2D.Origin);
            }
            else
            {
                scale = mod2.Get(0, 2);
                m3 = new ModOp2D(GeoVector2D.YAxis, GeoVector2D.XAxis, GeoPoint2D.Origin);
            }
            polynom.Set(mod2.Modified(m3));
            return m1 * m2 * m3;
        }

        /// <summary>
        /// Sloves two polynoms (==0) of degree 2 and dimension 2. the optinal minmax parameter gives bounds for the first (index 0,1) and the second (indes 2,3) variable
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="minmax"></param>
        /// <returns></returns>
        public static List<double[]> SolveTwoSquared(Polynom p1, Polynom p2, double[] minmax = null)
        {
            // GroebnerBasis[{a*x^2+b*x +c*y^2+d*y+e*x*y+f,g*x^2+h*x +i*y^2+j*y +k},{x,y}]
            if (p1.Dimension != 2 || p2.Dimension != 2) throw new ApplicationException("SolveTwoSquared: polynoms must have dimension 2");
            if (p1.Degree != 2 || p2.Degree != 2) throw new ApplicationException("SolveTwoSquared: polynoms must have degree 2");
#if DEBUG
            Polynom p1org = p1.Clone();
            Polynom p2org = p2.Clone();
#endif
            ModOp2D modified = ModOp2D.Identity;
            if (p1.Get(1, 1) != 0.0) // is the mixed term (x*y) set?
            {
                if (p2.Get(1, 1) != 0.0)
                {
                    // both Polynoms have the mixed trem x*y. Eliminate in p2 via Lagrange
                    p2 = p2.Clone(); // don't change the original
                    modified = LagrangeReduction2(p2); // now p2 doesn't have a mixed term
                    p1 = p1.Modified(modified);
                }
            }
            else if (p2.Get(1, 1) != 0.0)
            {
                Polynom tmp = p2;
                p2 = p1;
                p1 = tmp;
            }
            // here p2 does not have a mixed term
            double a = p1.Get(2, 0);
            double b = p1.Get(1, 0);
            double c = p1.Get(0, 2);
            double d = p1.Get(0, 1);
            double e = p1.Get(1, 1);
            double f = p1.Get(0, 0);

            double g = p2.Get(2, 0);
            double h = p2.Get(1, 0);
            double i = p2.Get(0, 2);
            double j = p2.Get(0, 1);
            double k = p2.Get(0, 0);

            List<double[]> res = new List<double[]>();
            Polynom y = new Polynom(
                c * c * g * g + a * a * i * i + e * e * g * i - 2 * a * c * g * i, "y4",
                2 * c * d * g * g - c * e * g * h - 2 * a * d * g * i + 2 * b * e * g * i - a * e * h * i + e * e * g * j - 2 * a * c * g * j + 2 * a * a * i * j, "y3",
                d * d * g * g + 2 * c * f * g * g + a * c * h * h + a * a * j * j - b * c * g * h - d * e * g * h + b * b * g * i - 2 * a * f * g * i - a * b * h * i - 2 * a * d * g * j + 2 * b * e * g * j - a * e * h * j + e * e * g * k - 2 * a * c * g * k + 2 * a * a * i * k, "y2",
                2 * d * f * g * g + a * d * h * h - b * d * g * h - e * f * g * h + b * b * g * j - 2 * a * f * g * j - a * b * h * j - 2 * a * d * g * k + 2 * b * e * g * k - a * e * h * k + 2 * a * a * j * k, "y",
                f * f * g * g + a * f * h * h + a * a * k * k - b * f * g * h + b * b * g * k - 2 * a * f * g * k - a * b * h * k, "");
            double[] rts = y.Roots();
            for (int ii = 0; ii < rts.Length; ii++)
            {
                if (minmax == null || (minmax[2] <= rts[ii] && minmax[3] >= rts[ii]))
                {
                    double x = (-c * g * rts[ii] * rts[ii] + a * i * rts[ii] * rts[ii] - d * g * rts[ii] + a * j * rts[ii] - f * g + a * k) / (+e * g * rts[ii] + b * g - a * h);
                    if (minmax == null || (minmax[0] <= x && minmax[1] >= x))
                    {
                        GeoPoint2D p = modified * new GeoPoint2D(x, rts[ii]);
                        res.Add(new double[] { p.x, p.y });
#if DEBUG
                        double e1 = p1org.Eval(p.x, p.y);
                        double e2 = p2org.Eval(p.x, p.y);
#endif
                    }
                }
            }

            return res;

        }
        private static List<double[]> SolveTwoSquaredx(Polynom p1, Polynom p2, double[] minmax)
        {
            List<double[]> res = new List<double[]>();

            // GroebnerBasis[{d*x^2+a*x +b*y^2+c*y+e,i*x^2+f*x +g*y^2+h*y +j},{x,y}]
            if (p1.Dimension != 2 || p2.Dimension != 2) throw new ApplicationException("SolveTwoSquared: polynoms must have dimension 2");
            if (p1.Degree != 2 || p2.Degree != 2) throw new ApplicationException("SolveTwoSquared: polynoms must have degree 2");
            double d = p1.Get(2, 0);
            double a = p1.Get(1, 0);
            double b = p1.Get(0, 2);
            double c = p1.Get(0, 1);
            double e = p1.Get(0, 0);
            double i = p2.Get(2, 0);
            double f = p2.Get(1, 0);
            double g = p2.Get(0, 2);
            double h = p2.Get(0, 1);
            double j = p2.Get(0, 0);

            Polynom y = new Polynom(+b * b * i * i - 2 * b * d * g * i + d * d * g * g, "y4",
                2 * b * c * i * i - 2 * b * d * h * i - 2 * c * d * g * i + 2 * d * d * g * h, "y3",
                +a * a * g * i + b * d * f * f - 2 * b * d * i * j + 2 * b * e * i * i + c * c * i * i - 2 * c * d * h * i + 2 * d * d * g * j + d * d * h * h - 2 * d * e * g * i - a * b * f * i - a * d * f * g, "y2",
                +a * a * h * i - a * c * f * i - a * d * f * h + c * d * f * f - 2 * c * d * i * j + 2 * c * e * i * i + 2 * d * d * h * j - 2 * d * e * h * i, "y",
                -a * d * f * j - a * e * f * i + d * e * f * f + d * d * j * j - 2 * d * e * i * j + a * a * i * j + e * e * i * i, "");
            double[] rts = y.Roots();
            for (int ii = 0; ii < rts.Length; ii++)
            {
                if (minmax == null || (minmax[2] <= rts[ii] && minmax[3] >= rts[ii]))
                {
                    double x = ((-b * i + d * g) * rts[ii] * rts[ii] + (d * h - c * i) * rts[ii] + d * j - e * i) / (a * i - d * f);
                    double e1 = p1.Eval(x, rts[ii]);
                    double e2 = p2.Eval(x, rts[ii]);
                    if (minmax == null || (minmax[0] <= x && minmax[1] >= x))
                    {
                        res.Add(new double[] { x, rts[ii] });
                    }
                }
            }

            return res;

        }
        private static List<double[]> Solve(Polynom[] equations, (double min, double max)[] bounds)
        {
            List<double[]> res = new List<double[]>();
            int dim = equations[0].Dimension;
            for (int i = 1; i < equations.Length; i++)
            {
                if (equations[i].Dimension != dim) throw new ApplicationException("Solve polynomial equations: polynoms must have same dimension");
            }
            if (equations.Length < dim) throw new ApplicationException("Solve polynomial equations: too few equations");
            Polynom singleVariableEquation = null;
            List<Polynom> groebnerBasis = new List<Polynom>();
            for (int i = 0; i < equations.Length; i++)
            {
                Polynom p = equations[i].Clone();
                p.NormalizeLeadingTerm();
                groebnerBasis.Add(p);
                bool[] dd = p.UsedDimensions();
                int cnt = 0;
                for (int j = 0; j < dd.Length; j++) if (dd[j]) ++cnt;
                if (cnt <= 1) singleVariableEquation = equations[i];
            }
            HashSet<Tuple<Polynom, Polynom>> alreadyTested = new HashSet<Tuple<Polynom, Polynom>>();
            while (true) // !singleVariableEquation)
            {
                List<Polynom> toAdd = new List<Polynom>();
                for (int i = 0; i < groebnerBasis.Count - 1; i++)
                {
                    int[] lti = groebnerBasis[i].LeadingTerm;
                    for (int j = i + 1; j < groebnerBasis.Count; j++)
                    {
                        if (alreadyTested.Contains(new Tuple<Polynom, Polynom>(groebnerBasis[i], groebnerBasis[j]))) continue;
                        else alreadyTested.Add(new Tuple<Polynom, Polynom>(groebnerBasis[i], groebnerBasis[j]));
                        int[] ltj = groebnerBasis[j].LeadingTerm;
                        // e.g. http://demonstrations.wolfram.com/TheBuchbergerGroebnerBasisAlgorithm/
                        // f*LCM(LT(f),LT(g))/LT(f) - g*LCM(LT(f),LT(g))/LT(g)
                        // or http://hilbert.math.uni-mannheim.de/~seiler/CA14/kap2.pdf "§5: Gröbner-Basen und der Buchberger-Algorithmus"
                        bool iIs1 = true, jIs1 = true; // determin whther factor is 1
                        int[] ltf = new int[dim];
                        int[] ltg = new int[dim];
                        for (int k = 0; k < dim; k++)
                        {
                            int diff = ltj[k] - lti[k];
                            ltf[k] = Math.Max(0, diff);
                            ltg[k] = Math.Max(0, -diff);
                            // lti is factor for f (lastBasis[i]) ltj for g
                            if (diff > 0) iIs1 = false;
                            if (diff < 0) jIs1 = false;
                        }
                        Polynom f, g;
                        if (iIs1) f = groebnerBasis[i];
                        else f = groebnerBasis[i] * new Polynom(ltf, 1.0);
                        if (jIs1) g = groebnerBasis[j];
                        else g = groebnerBasis[j] * new Polynom(ltg, 1.0);
                        Polynom p = f.Subtract(g);
                        p.reduce();
                        if (p.Degree > 0)
                        {
                            // p.NormalizeLeadingTerm();
                            //List<Polynom> sortedGB = new List<Polynom>(groebnerBasis);
                            //sortedGB.Sort(delegate (Polynom p1, Polynom p2)
                            //{
                            //    int[] lt1 = p1.LeadingTerm;
                            //    int[] lt2 = p2.LeadingTerm;
                            //    return CompareMonom(lt1, lt2);
                            //});
                            bool reduced = false;
                            //Polynom pc;
                            do
                            {
                                //pc = p.Clone();
                                reduced = false;
                                for (int k = 0; k < groebnerBasis.Count; k++)
                                {
                                    reduced |= p.ReduceBy(groebnerBasis[k]); // should be sorted by leading term, biggest first
                                }
                            } while (reduced && p.Degree > 0); // not sure, whether this terminates && pc != p);
                            do
                            {
                                reduced = false;
                                for (int k = 0; k < toAdd.Count; k++)
                                {
                                    reduced |= p.ReduceBy(toAdd[k]);
                                }
                            } while (reduced && p.Degree > 0);
                            p.NormalizeLeadingTerm();
                            if (p.Degree > 0)
                            {
                                bool[] dd = p.UsedDimensions();
                                int cnt = 0;
                                for (int k = 0; k < dd.Length; k++) if (dd[k]) ++cnt;
                                if (cnt <= 1) singleVariableEquation = p;
                                toAdd.Add(p);
                            }
                        }
                    }
                }
                if (toAdd.Count == 0) break;
                groebnerBasis.AddRange(toAdd);
            }
            // the groebner basis seems to be correct 
            // tested with https://www.wolframalpha.com and inputs like "GroebnerBasis[{x^2-2y^2+z^2,xy-3-z, xz-4},{x,y,z}]"
            // but it takes very long for higher degrees and the results are useless with real examples
            if (singleVariableEquation != null)
            {
                //bool[] dd = singleVariableEquation.UsedDimensions();
                //Polynom[] pps = new Polynom[dim];
                //int curind = -1;
                //for (int i = 0; i < dim; i++)
                //{
                //    if (dd[i])
                //    {
                //        pps[i] = new Polynom(1, "x");
                //        curind = i;
                //    }
                //    else pps[i] = new Polynom(0.0, 1);
                //}
                //Polynom toSolve = singleVariableEquation.Substitute(pps);
                //    double[] rts = toSolve.Roots();
                //for (int r = 0; r < rts.Length; r++)
                //{
                //    pps[curind] = new Polynom(rts[r], 1);
                //    pps[1] = new Polynom(1, "x");
                //    toSolve = groebnerBasis[44].Substitute(pps);
                //    double[] rts1 = toSolve.Roots();
                //    for (int rr = 0; rr < rts1.Length; rr++)
                //    {
                //        pps[1] = new Polynom(rts1[rr], 1);
                //        pps[0] = new Polynom(1, "x");
                //        toSolve = groebnerBasis[43].Substitute(pps);
                //        double[] rts2 = toSolve.Roots();
                //        double err = equations[0].Eval(rts2[0], rts1[0], rts[0]);
                //        double err1 = equations[1].Eval(rts2[0], rts1[0], rts[0]);
                //        double err2 = equations[2].Eval(rts2[0], rts1[0], rts[0]);
                //        err = groebnerBasis[45].Eval(rts2[0], rts1[0], rts[0]);
                //        err1 = groebnerBasis[44].Eval(rts2[0], rts1[0], rts[0]);
                //        err2 = groebnerBasis[43].Eval(rts2[0], rts1[0], rts[0]);
                //        err2 = groebnerBasis[42].Eval(rts2[0], rts1[0], rts[0]);
                //    }
                //}
            }
            return res;
        }

        private bool ReduceBy(Polynom r)
        {
            int[] ltr = r.LeadingTerm;
            int[] found = null;
            foreach (monom m in this)
            {
                bool contained = true;
                for (int i = 0; i < m.exp.Length; i++)
                {
                    if (ltr[i] > m.exp[i])
                    {
                        contained = false;
                        break;
                    }
                }
                if (contained)
                {
                    if (found == null || MonomLE(found, m.exp)) found = m.exp.Clone() as int[];
                }
            }
            if (found != null)
            {
                int[] md = new int[dim];
                bool is1 = true;
                for (int i = 0; i < dim; i++)
                {
                    md[i] = found[i] - ltr[i];
                    if (md[i] > 0) is1 = false;
                }
                // the leading term of r divides a monom of this
                Polynom red;
                if (is1) red = this.Subtract((Get(found) / r.Get(ltr)) * r);
                else red = this.Subtract(r * new Polynom(md, Get(found) / r.Get(ltr)));
                red.reduce();
                Set(red);
                return true;
            }
            return false;
        }

        private void Set(Polynom red)
        {
            dim = red.dim;
            deg = red.deg;
            c = (double[])red.c.Clone();
        }

        /// <summary>
        /// Implements less or equal for the monom order
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        private static bool MonomLE(int[] left, int[] right)
        {
            int cleft = 0, cright = 0;
            for (int i = 0; i < left.Length; i++) cleft += left[i];
            for (int i = 0; i < right.Length; i++) cright += right[i];
            switch (cleft.CompareTo(cright))
            {
                case -1: return true;
                case 1: return false;
                default:
                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] > right[i]) return false;
                    }
                    return true;
            }
        }

        private static int CompareMonom(int[] left, int[] right)
        {
            int cleft = 0, cright = 0;
            for (int i = 0; i < left.Length; i++) cleft += left[i];
            for (int i = 0; i < right.Length; i++) cright += right[i];
            switch (cleft.CompareTo(cright))
            {
                case -1: return -1;
                case 1: return +1;
                default:
                    for (int i = 0; i < left.Length; i++)
                    {
                        if (left[i] > right[i]) return +1;
                        if (left[i] < right[i]) return -1;
                    }
                    return 0;
            }
        }

        private static bool IsEqual(Polynom f, Polynom g)
        {
            foreach (monom m in f)
            {
                double cg = g.Get(m.exp);
                double cf = m.Coefficient;
                double d = Math.Abs(cf - cg);
                if (d > 0.0)
                {
                    double e = Math.Abs(cf + cg);
                    if (e / d < 1e+14) return false;
                }
            }
            return true;
        }

        internal static Polynom[] Line3d(GeoPoint startPoint, GeoVector direction)
        {
            return new Polynom[] { new Polynom(direction.x, "u", startPoint.x, ""), new Polynom(direction.y, "u", startPoint.y, ""), new Polynom(direction.z, "u", startPoint.z, "") };
        }
    }

    internal class PolynomVector
    {
        Polynom x;
        Polynom y;
        Polynom z;

        public PolynomVector(double x, double y, double z)
        {
            this.x = new Polynom(0, "x", 0, "y", 0, "z", x, "");
            this.y = new Polynom(0, "x", 0, "y", 0, "z", y, "");
            this.z = new Polynom(0, "x", 0, "y", 0, "z", z, "");
        }
        public PolynomVector(GeoVector c)
        {
            this.x = new Polynom(0, "x", 0, "y", 0, "z", c.x, "");
            this.y = new Polynom(0, "x", 0, "y", 0, "z", c.y, "");
            this.z = new Polynom(0, "x", 0, "y", 0, "z", c.z, "");
        }
        public static PolynomVector xyz => new PolynomVector(new Polynom(1, "x", 0, "y", 0, "z"), new Polynom(0, "x", 1, "y", 0, "z"), new Polynom(0, "x", 0, "y", 1, "z"));
        public PolynomVector()
        {
            this.x = new Polynom(1, "x", 0, "y", 0, "z");
            this.y = new Polynom(0, "x", 1, "y", 0, "z");
            this.z = new Polynom(0, "x", 0, "y", 1, "z");
        }
        public PolynomVector(Polynom x, Polynom y, Polynom z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public GeoVector Eval(double[] pos)
        {
            return new GeoVector(x.Eval(pos), y.Eval(pos), z.Eval(pos));
        }
        public static PolynomVector operator ^(PolynomVector left, PolynomVector right)
        {
            return new PolynomVector(left.y * right.z - left.z * right.y,
                                    left.z * right.x - left.x * right.z,
                                    left.x * right.y - left.y * right.x);
        }
        public static PolynomVector operator ^(PolynomVector left, GeoVector right)
        {
            return new PolynomVector(left.y * right.z - left.z * right.y,
                                    left.z * right.x - left.x * right.z,
                                    left.x * right.y - left.y * right.x);
        }
        public static Polynom operator *(PolynomVector left, PolynomVector right)
        {
            return left.x * right.x + left.y * right.y + left.z * right.z;
        }
        public static Polynom operator *(GeoVector left, PolynomVector right)
        {
            return left.x * right.x + left.y * right.y + left.z * right.z;
        }
        public static Polynom operator *(PolynomVector left, GeoVector right)
        {
            return left.x * right.x + left.y * right.y + left.z * right.z;
        }
        public static PolynomVector operator ^(GeoVector left, PolynomVector right)
        {
            return new PolynomVector(left.y * right.z - left.z * right.y,
                                    left.z * right.x - left.x * right.z,
                                    left.x * right.y - left.y * right.x);
        }
        public static PolynomVector operator *(Polynom left, PolynomVector right)
        {
            return new PolynomVector(left * right.x, left * right.y, left * right.z);
        }
        public static PolynomVector operator +(PolynomVector left, PolynomVector right)
        {
            return new PolynomVector(left.x + right.x, left.y + right.y, left.z + right.z);
        }
        public static PolynomVector operator -(PolynomVector left, PolynomVector right)
        {
            return new PolynomVector(left.x - right.x, left.y - right.y, left.z - right.z);
        }
        public static PolynomVector operator -(PolynomVector left, GeoVector right)
        {
            return new PolynomVector(left.x - right.x, left.y - right.y, left.z - right.z);
        }
        public static PolynomVector operator -(GeoVector left, PolynomVector right)
        {
            return new PolynomVector(left.x - right.x, left.y - right.y, left.z - right.z);
        }
    }
    internal class RationalPolynom
    {
        Polynom nominator, denominator; // Zähler, Nenner
        public RationalPolynom(Polynom nominator, Polynom denominator)
        {
            this.nominator = nominator;
            this.denominator = denominator;
        }
        public RationalPolynom(Polynom nominator)
        {
            this.nominator = nominator;
            this.denominator = new Polynom(1.0, nominator.Dimension);
        }
        public RationalPolynom(double val, int dim)
        {
            this.nominator = new Polynom(val, dim);
            this.denominator = new Polynom(1.0, dim);
        }
        public static RationalPolynom operator *(double f, RationalPolynom a)
        {
            return new RationalPolynom(f * a.nominator, a.denominator);
        }
        public static RationalPolynom operator *(RationalPolynom a, RationalPolynom b)
        {
            return new RationalPolynom(a.nominator * b.nominator, a.denominator * b.denominator);
        }
        public static RationalPolynom operator /(RationalPolynom a, RationalPolynom b)
        {
            return a * new RationalPolynom(b.denominator, b.nominator);
        }
        public static RationalPolynom operator +(RationalPolynom a, RationalPolynom b)
        {
            return new RationalPolynom(a.nominator * b.denominator + b.nominator * a.denominator, a.denominator * b.denominator);
        }
        public static RationalPolynom operator -(RationalPolynom a, RationalPolynom b)
        {
            return new RationalPolynom(a.nominator * b.denominator - b.nominator * a.denominator, a.denominator * b.denominator);
        }

        public double Eval(params double[] val)
        {
            return nominator.Eval(val) / denominator.Eval(val);
        }
        public RationalPolynom Derivate()
        {   // erstmal nur eine Ableitung und eindimensional
            Polynom diffnom = nominator.Derivate(1);
            Polynom diffdenom = denominator.Derivate(1);
            return new RationalPolynom((diffnom * denominator - diffdenom * nominator), (denominator * denominator));
        }

        public double[] Roots()
        {
            return nominator.Roots();
        }
    }

    /// <summary>
    /// Curve defined by 3 polynomials in one veriable ("u"), or by 4 polynoms with an additional "w" component (homogenuous coordinates).
    /// the curve may be defined piecewise: the knots array defines the intervalls for the u parameter while the polynom arrays define the curves for each intervall
    /// </summary>
    public class ExplicitPCurve3D
    {
        public Polynom[] px, py, pz, pw;
        public Polynom[] px1, py1, pz1, pw1; // erste Ableitung, nur einmal berechnen
        public Polynom[] px2, py2, pz2, pw2; // 2. Ableitung, ebenso
        public Polynom[] pxi, pyi, pzi, pwi; // und auch die Integrale
        public double[] knots; // eins mehr als px.Length, die Abschnitte, für die die Polynome gelten
        private static ExplicitPCurve3D unitCircle;
        public static ExplicitPCurve3D UnitCircle
        {
            get
            {
                if (unitCircle == null)
                {
                    Polynom px = new Polynom(2.34314575050762, "", -1.37258300203048, "x", -0.970562748477142, "x2");
                    Polynom py = new Polynom(3.31370849898476, "x", -0.970562748477142, "x2");
                    Polynom pw = new Polynom(2.34314575050762, "", -1.37258300203048, "x", 1.37258300203048, "x2");

                    Polynom[] ppx = new Polynom[4];
                    Polynom[] ppy = new Polynom[4];
                    Polynom[] ppz = new Polynom[4];
                    Polynom[] ppw = new Polynom[4];
                    ppx[0] = px;
                    ppy[0] = py;
                    ppz[0] = new Polynom(0, 1);
                    ppw[0] = pw;
                    ppx[1] = -px.Reversed().Offset(2);
                    ppy[1] = py.Reversed().Offset(2);
                    ppz[1] = new Polynom(0, 1);
                    ppw[1] = pw.Reversed().Offset(2);
                    ppx[2] = -px.Offset(2);
                    ppy[2] = -py.Offset(2);
                    ppz[2] = new Polynom(0, 1);
                    ppw[2] = pw.Offset(2);
                    ppx[3] = px.Reversed().Offset(4);
                    ppy[3] = -py.Reversed().Offset(4);
                    ppz[3] = new Polynom(0, 1);
                    ppw[3] = pw.Reversed().Offset(4);
                    unitCircle = new ExplicitPCurve3D(ppx, ppy, ppz, ppw, new double[] { 0, 1, 2, 3, 4 });
                }
                return unitCircle;
            }
        }
        public static ExplicitPCurve3D MakeCircle(ModOp toCircle)
        {
            return UnitCircle.GetModified(toCircle);
            //Polynom px = new Polynom(2.34314575050762, "", -1.37258300203048, "x", -0.970562748477142, "x2");
            //Polynom py = new Polynom(3.31370849898476, "x", -0.970562748477142, "x2");
            //Polynom pw = new Polynom(2.34314575050762, "", -1.37258300203048, "x", 1.37258300203048, "x2");

            //Polynom[] ppx = new Polynom[4];
            //Polynom[] ppy = new Polynom[4];
            //Polynom[] ppz = new Polynom[4];
            //Polynom[] ppw = new Polynom[4];
            //ppx[0] = toCircle.Item(0, 0) * px + toCircle.Item(0, 1) * py + toCircle.Item(0, 3) * pw;
            //ppy[0] = toCircle.Item(1, 0) * px + toCircle.Item(1, 1) * py + toCircle.Item(1, 3) * pw;
            //ppz[0] = toCircle.Item(2, 0) * px + toCircle.Item(2, 1) * py + toCircle.Item(2, 3) * pw;
            //ppw[0] = pw;
            //Polynom pxr = -px.Reversed().Offset(2);
            //Polynom pyr = py.Reversed().Offset(2);
            //Polynom pwr = pw.Reversed().Offset(2);
            //ppx[1] = toCircle.Item(0, 0) * pxr + toCircle.Item(0, 1) * pyr + toCircle.Item(0, 3) * pwr;
            //ppy[1] = toCircle.Item(1, 0) * pxr + toCircle.Item(1, 1) * pyr + toCircle.Item(1, 3) * pwr;
            //ppz[1] = toCircle.Item(2, 0) * pxr + toCircle.Item(2, 1) * pyr + toCircle.Item(2, 3) * pwr;
            //ppw[1] = pwr;
            //pxr = -px.Offset(2);
            //pyr = -py.Offset(2);
            //pwr = pw.Offset(2);
            //ppx[2] = toCircle.Item(0, 0) * pxr + toCircle.Item(0, 1) * pyr + toCircle.Item(0, 3) * pwr;
            //ppy[2] = toCircle.Item(1, 0) * pxr + toCircle.Item(1, 1) * pyr + toCircle.Item(1, 3) * pwr;
            //ppz[2] = toCircle.Item(2, 0) * pxr + toCircle.Item(2, 1) * pyr + toCircle.Item(2, 3) * pwr;
            //ppw[2] = pwr;
            //pxr = px.Reversed().Offset(4);
            //pyr = -py.Reversed().Offset(4);
            //pwr = pw.Reversed().Offset(4);
            //ppx[3] = toCircle.Item(0, 0) * pxr + toCircle.Item(0, 1) * pyr + toCircle.Item(0, 3) * pwr;
            //ppy[3] = toCircle.Item(1, 0) * pxr + toCircle.Item(1, 1) * pyr + toCircle.Item(1, 3) * pwr;
            //ppz[3] = toCircle.Item(2, 0) * pxr + toCircle.Item(2, 1) * pyr + toCircle.Item(2, 3) * pwr;
            //ppw[3] = pwr;
            //return new ExplicitCurve3D(ppx, ppy, ppz, ppw, new double[] { 0, 1, 2, 3, 4 });
        }
        public ExplicitPCurve3D GetModified(ModOp m)
        {
            Polynom[] ppx = new Polynom[px.Length];
            Polynom[] ppy = new Polynom[py.Length];
            Polynom[] ppz = new Polynom[pz.Length];
            Polynom[] ppw;
            if (pw == null) ppw = null;
            else ppw = new Polynom[pw.Length];
            for (int i = 0; i < px.Length; i++)
            {
                if (ppw != null)
                {
                    ppx[i] = m[0, 0] * px[i] + m[0, 1] * py[i] + m[0, 2] * pz[i] + m[0, 3] * pw[i];
                    ppy[i] = m[1, 0] * px[i] + m[1, 1] * py[i] + m[1, 2] * pz[i] + m[1, 3] * pw[i];
                    ppz[i] = m[2, 0] * px[i] + m[2, 1] * py[i] + m[2, 2] * pz[i] + m[2, 3] * pw[i];
                    ppw[i] = pw[i];
                }
                else
                {
                    ppx[i] = m[0, 0] * px[i] + m[0, 1] * py[i] + m[0, 2] * pz[i] + new Polynom(m[0, 3], 1);
                    ppy[i] = m[1, 0] * px[i] + m[1, 1] * py[i] + m[1, 2] * pz[i] + new Polynom(m[1, 3], 1);
                    ppz[i] = m[2, 0] * px[i] + m[2, 1] * py[i] + m[2, 2] * pz[i] + new Polynom(m[2, 3], 1);
                }
            }
            double[] pknots = null;
            if (knots != null) pknots = (double[])knots.Clone();
            return new ExplicitPCurve3D(ppx, ppy, ppz, ppw, pknots);
        }
        public static ExplicitPCurve3D MakeLine(GeoPoint sp, GeoVector dir)
        {
            //return new ExplicitPCurve3D(new Polynom(sp.x, "", dir.x, "u"), new Polynom(sp.y, "", dir.y, "u"), new Polynom(sp.z, "", dir.z, "u"));
            return new ExplicitPCurve3D(Polynom.SingleDimLinear(sp.x, dir.x), Polynom.SingleDimLinear(sp.y, dir.y), Polynom.SingleDimLinear(sp.z, dir.z));

        }
        public static ExplicitPCurve3D FromCurve(ICurve cv, double[] knots, int degree, bool homogenuous)
        {
            Polynom[] px = new Polynom[knots.Length - 1];
            Polynom[] py = new Polynom[knots.Length - 1];
            Polynom[] pz = new Polynom[knots.Length - 1];
            int ok = 0;
            if (!homogenuous)
            {
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    // a*u³+b*u²+c*u+d=cvx(u) im nicht homogenen Fall
                    Matrix m = new DenseMatrix(degree + 1, degree + 1);
                    Matrix b = new DenseMatrix(degree + 1, 3);
                    double du = (knots[i + 1] - knots[i]) / degree;
                    for (int j = 0; j < degree + 1; j++)
                    {
                        double u = knots[i] + j * du;
                        double c = 1.0;
                        for (int k = 0; k < degree + 1; k++)
                        {
                            m[j, k] = c;
                            c *= u;
                        }
                        GeoPoint p = cv.PointAt(u);
                        b[j, 0] = p.x;
                        b[j, 1] = p.y;
                        b[j, 2] = p.z;
                    }
                    Matrix x = (Matrix)m.Solve(b);
                    if (x.IsValid())
                    {
                        // Matrix dbg = m.QRD().Solve(b);

                        ++ok;
                        px[i] = new Polynom(degree, 1);
                        py[i] = new Polynom(degree, 1);
                        pz[i] = new Polynom(degree, 1);
                        int[] exp = new int[1]; // eindimensional (Polynom in u)
                        for (int j = 0; j < degree + 1; j++)
                        {
                            exp[0] = j;
                            px[i].Set(x[j, 0], exp);
                            py[i].Set(x[j, 1], exp);
                            pz[i].Set(x[j, 2], exp);
                        }
#if DEBUG
                        double err = 0.0;
                        for (int j = 0; j < degree; j++)
                        {
                            double u = knots[i] + (j + 0.5) * du;
                            GeoPoint p = cv.PointAt(u);
                            GeoPoint q = new CADability.GeoPoint(px[i].Eval(u), py[i].Eval(u), pz[i].Eval(u));
                            err += p | q;
                        }
#endif
                    }
                }
                if (ok == knots.Length - 1)
                {
                    return new ExplicitPCurve3D(px, py, pz, null, knots);
                }
            }
            else
            {
                // homogener Fall
                // ax*u³+bx*u²+cx*u+dx -e*cvx(u)*u³-f*cvx(u)*u²-g*cvx(u)*u-h*cvx(u) = 0
                // ay*u³+by*u²+cy*u+dy -e*cvy(u)*u³-f*cvy(u)*u²-g*cvy(u)*u-h*cvy(u) = 0
                // az*u³+bz*u²+cz*u+dz -e*cvz(u)*u³-f*cvz(u)*u²-g*cvz(u)*u-h*cvz(u) = 0
                // e+f+g+h = 1
                // damit sind es 3*(deg+1) + deg+1 unbekannte, wobei die letzte Gleichung ohne einen Punkt auskommt
                // wir brauchen also (4*(deg+1)-1)/3 Punkte, was so natürlich nicht aufgeht
                Polynom[] pw = new Polynom[knots.Length - 1];

                for (int i = 0; i < knots.Length - 1; i++)
                {
                    Matrix m = new DenseMatrix(4 * (degree + 1), 4 * (degree + 1)); // ist mit 0 vorbesetzt
                    Vector b = new DenseVector(4 * (degree + 1));
                    int n = (4 * (degree + 1) - 1 + 2) / 3; // +2, damit aufgerundet wird bei /3
                    double du = (knots[i + 1] - knots[i]) / (n - 1); // (n-1) damit 1. und letzter Knoten-Punkt verwendet wird
                    for (int j = 0; j < n; j++)
                    {
                        double u = knots[i] + j * du;
                        GeoPoint p = cv.PointAt(u);
                        double c = 1.0;

                        for (int k = 0; k < degree + 1; k++)
                        {
                            m[j * 3, k] = c;
                            m[j * 3, k + 3 * (degree + 1)] = -c * p.x;
                            if (j * 3 + 1 < 4 * (degree + 1))
                            {
                                m[j * 3 + 1, k + degree + 1] = c;
                                m[j * 3 + 1, k + 3 * (degree + 1)] = -c * p.y;
                            }
                            if (j * 3 + 2 < 4 * (degree + 1))
                            {
                                m[j * 3 + 2, k + 2 * (degree + 1)] = c;
                                m[j * 3 + 2, k + 3 * (degree + 1)] = -c * p.z;
                            }
                            c *= u;
                        }
                    }
                    for (int k = 0; k < degree + 1; k++)
                    {
                        m[m.RowCount - 1, k + 3 * (degree + 1)] = 1.0; // letzte Zeile hat für die Koeffizienten von pw nur 1.0, sonst 0.0
                    }

                    b[b.Count - 1] = 1.0; // das ganze b ist 0.0 bis auf die letzte Zeile
                    Vector x = (Vector)m.Solve(b);
                    if (x != null)
                    {
                        ++ok;
                        px[i] = new Polynom(degree, 1);
                        py[i] = new Polynom(degree, 1);
                        pz[i] = new Polynom(degree, 1);
                        pw[i] = new Polynom(degree, 1);
                        int[] exp = new int[1]; // eindimensional (Polynom in u)
                        for (int j = 0; j < degree + 1; j++)
                        {
                            exp[0] = j;
                            px[i].Set(x[j], exp);
                            py[i].Set(x[j + degree + 1], exp);
                            pz[i].Set(x[j + 2 * (degree + 1)], exp);
                            pw[i].Set(x[j + 3 * (degree + 1)], exp);
                        }
#if DEBUG
                        double err = 0.0;
                        for (int j = 0; j < n; j++)
                        {
                            double u = knots[i] + (j + 0.5) * du;
                            GeoPoint p = cv.PointAt(u);
                            double w = pw[i].Eval(u);
                            GeoPoint q = new CADability.GeoPoint(px[i].Eval(u) / w, py[i].Eval(u) / w, pz[i].Eval(u) / w);
                            err += p | q;
                        }
#endif
                    }
                }
                if (ok == knots.Length - 1)
                {
                    return new ExplicitPCurve3D(px, py, pz, pw, knots);
                }

            }
            return null;
        }
        internal static ExplicitPCurve3D FromPointsDirections(GeoPoint[] pnt, GeoVector[] dir, double[] knots = null)
        {
            if (knots == null)
            {
                knots = new double[pnt.Length];
                for (int i = 0; i < knots.Length; i++)
                {
                    knots[i] = (double)i / (double)(knots.Length - 1);
                }
            }
            Polynom[] px = new Polynom[knots.Length - 1];
            Polynom[] py = new Polynom[knots.Length - 1];
            Polynom[] pz = new Polynom[knots.Length - 1];
            int ok = 0;
            // a*u³+b*u²+c*u+d=pnt[i].x | u=knots[i]
            // a*u³+b*u²+c*u+d=pnt[i+1].x | u=knots[i+1]
            // 3*a*u²+2*u*b+c=dir[i].x | u=knots[i]
            // 3*a*u²+2*u*b+c=dir[i+1].x | u=knots[i+1]
            for (int i = 0; i < px.Length; i++)
            {
                double len = (pnt[i] | pnt[i + 1]) / (knots[i + 1] - knots[i]);
                GeoVector dir1 = dir[i];
                GeoVector dir2 = dir[i + 1];
                dir1.Length = len;
                dir2.Length = len;
                Matrix m = new DenseMatrix(4, 4); // ist mit 0 vorbesetzt
                Matrix b = new DenseMatrix(4, 3);
                m[0, 0] = knots[i] * knots[i] * knots[i];
                m[0, 1] = knots[i] * knots[i];
                m[0, 2] = knots[i];
                m[0, 3] = 1.0;
                b[0, 0] = pnt[i].x;
                b[0, 1] = pnt[i].y;
                b[0, 2] = pnt[i].z;
                m[1, 0] = knots[i + 1] * knots[i + 1] * knots[i + 1];
                m[1, 1] = knots[i + 1] * knots[i + 1];
                m[1, 2] = knots[i + 1];
                m[1, 3] = 1.0;
                b[1, 0] = pnt[i + 1].x;
                b[1, 1] = pnt[i + 1].y;
                b[1, 2] = pnt[i + 1].z;
                m[2, 0] = 3 * knots[i] * knots[i];
                m[2, 1] = 2 * knots[i];
                m[2, 2] = 1.0;
                m[2, 3] = 0.0;
                b[2, 0] = dir1.x;
                b[2, 1] = dir1.y;
                b[2, 2] = dir1.z;
                m[3, 0] = 3 * knots[i + 1] * knots[i + 1];
                m[3, 1] = 2 * knots[i + 1];
                m[3, 2] = 1.0;
                m[3, 3] = 0.0;
                b[3, 0] = dir2.x;
                b[3, 1] = dir2.y;
                b[3, 2] = dir2.z;
                Matrix x = (Matrix)m.Solve(b);
                if (x.IsValid())
                {
                    // Matrix dbg = m.QRD().Solve(b);

                    ++ok;
                    px[i] = new Polynom(3, 1);
                    py[i] = new Polynom(3, 1);
                    pz[i] = new Polynom(3, 1);
                    int[] exp = new int[1]; // eindimensional (Polynom in u)
                    for (int j = 0; j < 3 + 1; j++)
                    {
                        exp[0] = 3 - j;
                        px[i].Set(x[j, 0], exp);
                        py[i].Set(x[j, 1], exp);
                        pz[i].Set(x[j, 2], exp);
#if DEBUG
                        if (double.IsNaN(x[j, 0]) || double.IsNaN(x[j, 1]) || double.IsNaN(x[j, 2]))
                        {

                        }
#endif
                    }
                }
            }
            if (ok == knots.Length - 1)
            {
                return new ExplicitPCurve3D(px, py, pz, null, knots);
            }
            else return null;
        }
        public ExplicitPCurve3D(Polynom px, Polynom py, Polynom pz, Polynom pw = null)
        {
            this.px = new Polynom[] { px };
            this.py = new Polynom[] { py };
            this.pz = new Polynom[] { pz };
            if (pw != null) this.pw = new Polynom[] { pw };
            else this.pw = null;
            knots = null;
        }
        public ExplicitPCurve3D(Polynom[] px, Polynom[] py, Polynom[] pz, Polynom[] pw = null, double[] knots = null)
        {
            this.px = px;
            this.py = py;
            this.pz = pz;
            this.pw = pw;
            this.knots = knots;
        }
        public GeoPoint PointAt(double u)
        {
            int ind = 0;
            if (knots != null) // nur ein Abschnitt, unendlich
            {
                ind = -1;
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    if (knots[i] <= u && knots[i + 1] > u)
                    {
                        ind = i;
                        break;
                    }
                }
                if (ind == -1)
                {
                    if (Math.Abs(u - knots[0]) < Math.Abs(u - knots[knots.Length - 1])) ind = 0;
                    else ind = knots.Length - 2;
                }
            }
            if (pw != null && pw[ind] != null)
            {
                double w = pw[ind].Eval(u);
                return new GeoPoint(px[ind].Eval(u) / w, py[ind].Eval(u) / w, pz[ind].Eval(u) / w);
            }
            else
            {
                return new GeoPoint(px[ind].Eval(u), py[ind].Eval(u), pz[ind].Eval(u));
            }

        }
        public GeoVector DirectionAt(double u)
        {
            int ind = 0;
            if (knots != null) // nur ein Abschnitt, unendlich
            {
                ind = -1;
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    if (knots[i] <= u && knots[i + 1] > u)
                    {
                        ind = i;
                        break;
                    }
                }
                if (ind == -1)
                {
                    if (Math.Abs(u - knots[0]) < Math.Abs(u - knots[knots.Length - 1])) ind = 0;
                    else ind = knots.Length - 2;
                }
            }
            CalcDerivatives(true, false, false);
            if (pw != null && pw[ind] != null)
            {
                double w = pw[ind].Eval(u);
                double w1 = pw1[ind].Eval(u);
                return new GeoVector((px1[ind].Eval(u) * w - px[ind].Eval(u) * w1) / (w * w), (py1[ind].Eval(u) * w - py[ind].Eval(u) * w1) / (w * w), (pz1[ind].Eval(u) * w - pz[ind].Eval(u) * w1) / (w * w));
            }
            else
            {
                return new GeoVector(px1[ind].Eval(u), py1[ind].Eval(u), pz1[ind].Eval(u));
            }

        }
        public GeoVector Direction2At(double u)
        {
            int ind = 0;
            if (knots != null) // nur ein Abschnitt, unendlich
            {
                ind = -1;
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    if (knots[i] <= u && knots[i + 1] > u)
                    {
                        ind = i;
                        break;
                    }
                }
                if (ind == -1)
                {
                    if (Math.Abs(u - knots[0]) < Math.Abs(u - knots[knots.Length - 1])) ind = 0;
                    else ind = knots.Length - 2;
                }
            }
            CalcDerivatives(true, true, false);
            if (pw != null && pw[ind] != null)
            {
                double w = pw[ind].Eval(u);
                double w1 = pw1[ind].Eval(u);
                double w2 = pw2[ind].Eval(u); // not sure whether the following is correct:
                return new GeoVector((px2[ind].Eval(u) * w - px[ind].Eval(u) * w2) / (w * w), (py2[ind].Eval(u) * w - py[ind].Eval(u) * w2) / (w * w), (pz2[ind].Eval(u) * w - pz[ind].Eval(u) * w2) / (w * w));
            }
            else
            {
                return new GeoVector(px2[ind].Eval(u), py2[ind].Eval(u), pz2[ind].Eval(u));
            }

        }
        public double RadiusAt(double u)
        {
            int ind = 0;
            if (knots != null) // nur ein Abschnitt, unendlich
            {
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    if (knots[i] <= u && knots[i + 1] > u)
                    {
                        ind = i;
                        break;
                    }
                }
            }
            if (px1 == null) CalcDeriv1();
            if (px2 == null) CalcDeriv2();
            if (pw != null && pw[ind] != null)
            {
                RationalPolynom rx1 = new RationalPolynom(px[ind], pw[ind]).Derivate();
                RationalPolynom ry1 = new RationalPolynom(py[ind], pw[ind]).Derivate();
                RationalPolynom rz1 = new RationalPolynom(pz[ind], pw[ind]).Derivate();
                RationalPolynom rx2 = rx1.Derivate();
                RationalPolynom ry2 = ry1.Derivate();
                RationalPolynom rz2 = rz1.Derivate();
                GeoVector d1 = new GeoVector(rx1.Eval(u), ry1.Eval(u), rz1.Eval(u));
                GeoVector d2 = new GeoVector(rx2.Eval(u), ry2.Eval(u), rz2.Eval(u));
                return Math.Pow(d1.Length, 3) / (d1 ^ d2).Length;
            }
            else
            {
                GeoVector d1 = new GeoVector(px1[ind].Eval(u), py1[ind].Eval(u), pz1[ind].Eval(u));
                GeoVector d2 = new GeoVector(px2[ind].Eval(u), py2[ind].Eval(u), pz2[ind].Eval(u));
                return Math.Pow(d1.Length, 3) / (d1 ^ d2).Length;
            }
        }
        public double RadiusAt(double u, out Plane curvaturePlane)
        {
            int ind = 0;
            if (knots != null) // nur ein Abschnitt, unendlich
            {
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    if (knots[i] <= u && knots[i + 1] > u)
                    {
                        ind = i;
                        break;
                    }
                }
            }
            if (px1 == null) CalcDeriv1();
            if (px2 == null) CalcDeriv2();
            double radius;
            GeoPoint pos;
            GeoVector d1, d2;
            if (pw != null && pw[ind] != null)
            {
                RationalPolynom rx1 = new RationalPolynom(px[ind], pw[ind]).Derivate();
                RationalPolynom ry1 = new RationalPolynom(py[ind], pw[ind]).Derivate();
                RationalPolynom rz1 = new RationalPolynom(pz[ind], pw[ind]).Derivate();
                RationalPolynom rx2 = rx1.Derivate();
                RationalPolynom ry2 = ry1.Derivate();
                RationalPolynom rz2 = rz1.Derivate();
                d1 = new GeoVector(rx1.Eval(u), ry1.Eval(u), rz1.Eval(u));
                d2 = new GeoVector(rx2.Eval(u), ry2.Eval(u), rz2.Eval(u));
                double w = pw[ind].Eval(u);
                pos = new GeoPoint(px[ind].Eval(u) / w, py[ind].Eval(u) / w, pz[ind].Eval(u) / w);
                radius = Math.Pow(d1.Length, 3) / (d1 ^ d2).Length;
            }
            else
            {
                d1 = new GeoVector(px1[ind].Eval(u), py1[ind].Eval(u), pz1[ind].Eval(u));
                d2 = new GeoVector(px2[ind].Eval(u), py2[ind].Eval(u), pz2[ind].Eval(u));
                pos = new GeoPoint(px[ind].Eval(u), py[ind].Eval(u), pz[ind].Eval(u));
                radius = Math.Pow(d1.Length, 3) / (d1 ^ d2).Length;
            }
            GeoPoint cnt = pos + radius * d2.Normalized;
            curvaturePlane = new Plane(cnt, d1, d2);
            return radius;
        }
        public double Length(double sp, double ep)
        {
            if (pxi == null) CalcIntegral();
            // length is integral(|c'|) = integral(sqrt((x/du)²+(y/du)²+(z/du)²)*du
            // Polynom l = (px1[i] * px1[i] + py1[i] * py1[i] + pz1[i] * pz1[i]);
            // but this doesn't help since I see no way to integrate it. Bernoulli allows only root of a polynom integration up to degree 2

            return 0.0;
        }
        internal static double sqr(double d)
        {
            return d * d;
        }
        private int IndexOf(double u)
        {
            int ind = 0;
            if (knots != null) // nur ein Abschnitt, unendlich
            {
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    if (knots[i] <= u && knots[i + 1] > u)
                    {
                        ind = i;
                        break;
                    }
                }
            }
            return ind;
        }
        /// <summary>
        /// Returns the best parameter for which the value of this curve comes closest to p. If the curve is composed of multiple segments (knots!=null) then only the segment
        /// which contains startHere is checked. If it does not contain a minimum, double.MaxValue is returned. If you want to check all segments of a segmented
        /// curve, use PositionOf(GeoPoint p, out double dist)
        /// </summary>
        /// <param name="p"></param>
        /// <param name="startHere">initial guess for the parameter</param>
        /// <returns>the parameter for the point p or double.MaxValue, if not found</returns>
        public double PositionOf(GeoPoint p, double startHere, out double dist)
        {
            int ind = IndexOf(startHere);
            if (px1 == null) CalcDeriv1();
            double bestDist = double.MaxValue;
            double res = double.MaxValue;
            Polynom diff = (px[ind] - p.x) * px1[ind] + (py[ind] - p.y) * py1[ind] + (pz[ind] - p.z) * pz1[ind];
            double[] roots = diff.Roots();
            for (int i = 0; i < roots.Length; i++)
            {
                if (knots == null || (roots[i] >= knots[ind] && roots[i] <= knots[ind + 1]))
                {
                    GeoPoint ev = new GeoPoint(px[ind].Eval(roots[i]), py[ind].Eval(roots[i]), pz[ind].Eval(roots[i]));
                    double d = ev | p;
                    if (d < bestDist)
                    {
                        bestDist = d;
                        res = roots[i];
                    }
                }
            }
            dist = bestDist;
            return res;
        }
        public double PositionOf(GeoPoint p, out double dist)
        {
            if (px1 == null) CalcDeriv1();
            int upper = 1;
            if (knots != null) upper = knots.Length - 1;
            double bestDist = double.MaxValue;
            double res = double.MaxValue;
            double dkn = 0.0;
            if (knots != null) dkn = (knots[knots.Length - 1] - knots[0]) * 1e-6;
            for (int ind = 0; ind < upper; ind++)
            {

                Polynom diff = (px[ind] - p.x) * px1[ind] + (py[ind] - p.y) * py1[ind] + (pz[ind] - p.z) * pz1[ind];
                double[] roots = diff.Roots();

                for (int i = 0; i < roots.Length; i++)
                {
                    if (knots == null || (roots[i] >= knots[ind] - dkn && roots[i] <= knots[ind + 1] + dkn))
                    {
                        GeoPoint ev = new GeoPoint(px[ind].Eval(roots[i]), py[ind].Eval(roots[i]), pz[ind].Eval(roots[i]));
                        double d = ev | p;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            res = roots[i];
                        }
                    }
                }
            }
            dist = bestDist;
            return res;
        }
#if DEBUG
        internal double[] GetCurvatureExtrema()
        {
            // create a rational polynom, which yields the curvature radius from the parameter. Find maxima and minima
            // works in principle, but results are bad: degree 15 for roots when degree of curve is 3
            List<double> res = new List<double>();
            if (px1 == null) CalcDeriv1();
            if (px2 == null) CalcDeriv2();
            int num = 1;
            if (knots != null) num = knots.Length - 1;
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < num; i++)
            {
                if (pw == null)
                {
                    double m = (knots[i] + knots[i + 1]) / 2.0;
                    Polynom l1sqr = px1[i] * px1[i] + py1[i] * py1[i] + pz1[i] * pz1[i];
                    Polynom cx = py1[i] * pz2[i] - pz1[i] * py2[i];
                    Polynom cy = px1[i] * pz2[i] - pz1[i] * px2[i];
                    Polynom cz = px1[i] * py2[i] - py1[i] * px2[i];
                    Polynom denom = (cx * cx + cy * cy + cz * cz);
                    Polynom numerator = l1sqr * l1sqr * l1sqr;
                    double rr = Math.Sqrt(numerator.Eval(m) / denom.Eval(m)); // das ist der Radius
                    Polynom toRoot = denom.Derivate(1) * numerator - denom * numerator.Derivate(1); // das ist der Zähler der Ableitung, die 0 werden muss
                    double[] rts = toRoot.Roots();
                    for (int j = 0; j < rts.Length; j++)
                    {
                        if (knots[i] <= rts[j] && rts[j] <= knots[i + 1])
                        {
                            m = rts[j];
                            GeoVector dbg = new GeoVector(cx.Eval(m), cy.Eval(m), cz.Eval(m));
                            GeoVector cnt = new GeoVector(px2[i].Eval(m), py2[i].Eval(m), pz2[i].Eval(m));
                            GeoVector dir = DirectionAt(m);
                            double rad = RadiusAt(m);
                            cnt = (dbg ^ dir).Normalized;
                            GeoPoint pos = PointAt(m);
                            Line l = Line.TwoPoints(pos, pos + rad * cnt.Normalized);
                            Ellipse elli = Ellipse.Construct();
                            Plane pln = new Plane(pos, dir, cnt);
                            elli.SetCirclePlaneCenterRadius(pln, pos + rad * cnt.Normalized, rad);
                            dc.Add(elli);
                            dc.Add(l);
                        }
                    }
                    // double[] rts = curv.Roots();
                    double len = l1sqr.Eval(m);

                }
            }
            return res.ToArray();
        }
        internal Polyline Debug100
        {
            get
            {
                GeoPoint[] pnts = new GeoPoint[100];
                for (int i = 0; i < pnts.Length; i++)
                {
                    double u;
                    if (knots == null) u = (double)i / (double)(pnts.Length - 1);
                    else
                        u = knots[0] + (knots[knots.Length - 1] - knots[0]) / (pnts.Length - 1);
                    pnts[i] = PointAt(i * u);
                }
                return Polyline.FromPoints(pnts);
            }
        }
        internal Polyline Debug1000
        {
            get
            {
                GeoPoint[] pnts = new GeoPoint[1000];
                for (int i = 0; i < pnts.Length; i++)
                {
                    double u;
                    if (knots == null) u = (double)i / (double)(pnts.Length - 1);
                    else
                        u = knots[0] + (knots[knots.Length - 1] - knots[0]) / (pnts.Length - 1);
                    pnts[i] = PointAt(i * u);
                }
                return Polyline.FromPoints(pnts);
            }
        }
#endif
        internal double[] GetCurvaturePositions(double radius)
        {
            List<double> res = new List<double>();
            if (px1 == null) CalcDeriv1();
            if (px2 == null) CalcDeriv2();
            int num = 1;
            if (knots != null) num = knots.Length - 1;
            for (int i = 0; i < num; i++)
            {
                if (pw == null)
                {
                    // siehe https://de.wikipedia.org/wiki/Kr%C3%BCmmung (Raumkurven) Krümmung ist invers zu Radius
                    // den Bruch invertieren und quadrieren, damit steht da r² = (1. Ableitung)³ / (Kreuzprodukt)² oder 
                    // r²*(Kreuzprodukt)² - (1. Ableitung)³ == 0
                    // im Versuch mit einem handgemalten BSpline, also Grad 3, entstand hier ein Polynom (curv) vom Grad 12
                    // was aber Lösungen auf 10 Stellen genau lieferte. Ob das Sinn macht, ist noch nicht klar
                    Polynom l1sqr = px1[i] * px1[i] + py1[i] * py1[i] + pz1[i] * pz1[i];
                    Polynom cx = py1[i] * pz2[i] - pz1[i] * py2[i];
                    Polynom cy = px1[i] * pz2[i] - pz1[i] * px2[i];
                    Polynom cz = px1[i] * py2[i] - py1[i] * px2[i];
                    Polynom curv = radius * radius * (cx * cx + cy * cy + cz * cz) - l1sqr * l1sqr * l1sqr;
                    double[] rts = curv.Roots();
                    for (int j = 0; j < rts.Length; j++)
                    {
                        if (knots == null)
                        {
                            // geht das dann von 0 bis 1?
                            if (rts[j] >= 0.0 && rts[j] <= 1.0) res.Add(rts[j]);
                        }
                        else
                        {
                            if (rts[j] >= knots[i] && rts[j] <= knots[i + 1]) res.Add(rts[j]);
                        }
                    }
#if DEBUG
                    if (knots != null)
                    {
                        double dbgpos = (knots[i + 1] + knots[i]) / 2.0;
                        double rad = ((l1sqr * l1sqr * l1sqr).Eval(dbgpos)) / ((cx * cx + cy * cy + cz * cz).Eval(dbgpos));
                        System.Diagnostics.Trace.WriteLine("Krümmung bei " + dbgpos.ToString() + ": " + rad.ToString() + ", " + l1sqr.Eval(dbgpos).ToString() + ", " + (cx * cx + cy * cy + cz * cz).Eval(dbgpos).ToString());
                        System.Diagnostics.Trace.WriteLine("Vergleich: " + ((l1sqr * l1sqr * l1sqr).Eval(dbgpos)).ToString() + ", " + (l1sqr.Eval(dbgpos) * l1sqr.Eval(dbgpos) * l1sqr.Eval(dbgpos)).ToString());
                    }
#endif
                }
                else
                {
                    // Im homogenen Fall betrachten wir px[i]/pw[i] als rationales Polynom
                    // aber es geht noch einfacher: den Nenner einfach im geiste mitführen, schließlich ist der Nenner in "curv" bei beiden Summanden gleich
                    // Quotientenregel fürs Ableiten!
                    Polynom rx1n = px1[i] * pw[i] - px[i] * pw1[i]; // durch pw[i]²
                    Polynom ry1n = py1[i] * pw[i] - py[i] * pw1[i]; // durch pw[i]²
                    Polynom rz1n = pz1[i] * pw[i] - pz[i] * pw1[i]; // durch pw[i]²
                    Polynom pw2 = pw[i] * pw[i];
                    Polynom pw2d = pw2.Derivate(1);
                    Polynom rx2n = rx1n.Derivate(1) * pw2 - rx1n * pw2d; // durch pw[i]^4
                    Polynom ry2n = ry1n.Derivate(1) * pw2 - ry1n * pw2d; // durch pw[i]^4
                    Polynom rz2n = rz1n.Derivate(1) * pw2 - rz1n * pw2d; // durch pw[i]^4
                    Polynom l1sqrn = rx1n * rx1n + ry1n * ry1n + rz1n * rz1n; // durch pw[i]^4
                    Polynom cxn = ry1n * rz2n - rz1n * ry2n; // durch pw[i]^6
                    Polynom cyn = rx1n * rz2n - rz1n * rx2n; // durch pw[i]^6
                    Polynom czn = rx1n * ry2n - ry1n * rx2n; // durch pw[i]^6
                    Polynom curvn = radius * radius * (cxn * cxn + cyn * cyn + czn * czn) - l1sqrn * l1sqrn * l1sqrn; // beide Summanden haben pw[i]^12 im Nenner, für Nullstellen also irrelevant
                    double[] rts = curvn.Roots();
                    // Im Versuch mit einer Ellipse gibt es gute Ergebnisse: curvn hat Grad 12, die Nullstellen sind aber gut

                    // RationalPolynom funktioniert auch, der Grad wird aber exorbitant (120) und die Nullstellen nicht so gut
                    //RationalPolynom rx1 = new RationalPolynom(px[i], pw[i]).Derivate();
                    //RationalPolynom ry1 = new RationalPolynom(py[i], pw[i]).Derivate();
                    //RationalPolynom rz1 = new RationalPolynom(pz[i], pw[i]).Derivate();
                    //RationalPolynom rx2 = rx1.Derivate();
                    //RationalPolynom ry2 = ry1.Derivate();
                    //RationalPolynom rz2 = rz1.Derivate();
                    //RationalPolynom l1sqr = rx1 * rx1 + ry1 * ry1 + rz1 * rz1;
                    //RationalPolynom cx = ry1 * rz2 - rz1 * ry2;
                    //RationalPolynom cy = rx1 * rz2 - rz1 * rx2;
                    //RationalPolynom cz = rx1 * ry2 - ry1 * rx2;
                    //RationalPolynom curv = radius * radius * (cx * cx + cy * cy + cz * cz) - l1sqr * l1sqr * l1sqr;
                    //double[] rts = curv.Roots();
                    for (int j = 0; j < rts.Length; j++)
                    {
                        if (knots == null)
                        {
                            // geht das dann von 0 bis 1?
                            if (rts[j] >= 0.0 && rts[j] <= 1.0) res.Add(rts[j]);
                        }
                        else
                        {
                            if (rts[j] >= knots[i] && rts[j] <= knots[i + 1]) res.Add(rts[j]);
                        }
                    }
                }
            }
            return res.ToArray();
        }
        internal double[] GetPlaneIntersection(GeoPoint loc, GeoVector dirx, GeoVector diry, double spar = double.MinValue, double epar = double.MaxValue)
        {   // n*(x-a)==0
            List<double> res = new List<double>();
            GeoVector normal = dirx ^ diry;
            if (knots != null)
            {
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    if (knots[i] <= epar || knots[i + 1] >= spar)
                    {
                        Polynom p;
                        if (pw == null)
                        {
                            p = normal.x * (px[i] - loc.x) + normal.y * (py[i] - loc.y) + normal.z * (pz[i] - loc.z);
                        }
                        else
                        {
                            p = normal.x * (px[i] - loc.x * pw[i]) + normal.y * (py[i] - loc.y * pw[i]) + normal.z * (pz[i] - loc.z * pw[i]);
                            // das ist 
                            // normal.x * (px[i]/pw[i] - loc.x) + normal.y * (py[i]/pw[i] - loc.y) + normal.z * (pz[i]/pw[i] - loc.z)
                            // mit pw multipliziert. Für die Nullstellen sollte das keinen Unterschied machen. Liefert zusätzlich die Nullstellen von pw
                            // die es aber nicht geben darf
                        }
                        double[] rts = p.Roots();
                        for (int j = 0; j < rts.Length; j++)
                        {
                            if (rts[j] >= knots[i] && rts[j] <= knots[i + 1] && rts[j] >= spar && rts[j] <= epar) res.Add(rts[j]);
                        }
                    }
                }
            }
            else
            {
                Polynom p;
                if (pw == null)
                {
                    p = normal.x * (px[0] - loc.x) + normal.y * (py[0] - loc.y) + normal.z * (pz[0] - loc.z);
                }
                else
                {
                    p = normal.x * (px[0] - loc.x * pw[0]) + normal.y * (py[0] - loc.y * pw[0]) + normal.z * (pz[0] - loc.z * pw[0]);
                    // das ist 
                    // normal.x * (px[i]/pw[i] - loc.x) + normal.y * (py[i]/pw[i] - loc.y) + normal.z * (pz[i]/pw[i] - loc.z)
                    // mit pw multipliziert. Für die Nullstellen sollte das keinen Unterschied machen. Liefert zusätzlich die Nullstellen von pw
                    // die es aber nicht geben darf
                }
                double[] rts = p.Roots();
                for (int j = 0; j < rts.Length; j++)
                {
                    if (rts[j] >= spar && rts[j] <= epar) res.Add(rts[j]);
                }
            }
            return res.ToArray();
        }
        internal double[] GetPerpendicularToDirection(GeoVector dir)
        {
            List<double> res = new List<double>();
            for (int i = 0; i < px.Length; i++)
            {
                CalcDerivatives(true, false, false);
                Polynom toSolve;
                if (pw != null)
                {
                    toSolve = dir.x * (px1[i] * pw[i] - px[i] * pw1[i]) + dir.y * (py1[i] * pw[i] - py[i] * pw1[i]) + dir.z * (pz1[i] * pw[i] - pz[i] * pw1[i]);
                }
                else
                {
                    toSolve = dir.x * px1[i] + dir.y * py1[i] + dir.z * pz1[i];
                }
                double[] rts = toSolve.Roots();
                for (int j = 0; j < rts.Length; j++)
                {
                    if (knots == null)
                    {
                        // geht das dann von 0 bis 1?
                        // if (rts[j] >= 0.0 && rts[j] <= 1.0)
                        res.Add(rts[j]);
                    }
                    else
                    {
                        if (rts[j] >= knots[i] && rts[j] <= knots[i + 1]) res.Add(rts[j]);
                    }
                }
            }
            return res.ToArray();

        }
        internal double[] GetPerpendicularFootPoints(GeoPoint fromHere)
        {
            List<double> res = new List<double>();
            for (int i = 0; i < px.Length; i++)
            {
                Polynom dirfx, dirfy, dirfz;
                if (pw != null)
                {
                    dirfx = px[i] - new Polynom(fromHere.x, 1) * pw[i];
                    dirfy = py[i] - new Polynom(fromHere.y, 1) * pw[i];
                    dirfz = pz[i] - new Polynom(fromHere.z, 1) * pw[i];
                }
                else
                {
                    dirfx = px[i] - new Polynom(fromHere.x, 1);
                    dirfy = py[i] - new Polynom(fromHere.y, 1);
                    dirfz = pz[i] - new Polynom(fromHere.z, 1);
                }
                CalcDerivatives(true, false, false);
                Polynom toSolve;
                if (pw != null)
                {
                    toSolve = dirfx * (px1[i] * pw[i] - px[i] * pw1[i]) + dirfy * (py1[i] * pw[i] - py[i] * pw1[i]) + dirfz * (pz1[i] * pw[i] - pz[i] * pw1[i]);
                }
                else
                {
                    toSolve = dirfx * px1[i] + dirfy * py1[i] + dirfz * pz1[i];
                }
                double[] rts = toSolve.Roots();
                for (int j = 0; j < rts.Length; j++)
                {
                    if (knots == null)
                    {
                        // geht das dann von 0 bis 1?
                        // if (rts[j] >= 0.0 && rts[j] <= 1.0)
                        res.Add(rts[j]);
                    }
                    else
                    {
                        if (rts[j] >= knots[i] && rts[j] <= knots[i + 1]) res.Add(rts[j]);
                    }
                }
            }
            return res.ToArray();
        }
        private void CalcDeriv2()
        {
            px2 = new CADability.Polynom[px.Length];
            py2 = new CADability.Polynom[px.Length];
            pz2 = new CADability.Polynom[px.Length];
            if (pw != null) pw2 = new CADability.Polynom[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                px2[i] = px[i].Derivate(new int[] { 2 });
                py2[i] = py[i].Derivate(new int[] { 2 });
                pz2[i] = pz[i].Derivate(new int[] { 2 });
                if (pw != null) pw2[i] = pw[i].Derivate(new int[] { 2 });
            }
        }
        private void CalcDeriv1()
        {
            px1 = new CADability.Polynom[px.Length];
            py1 = new CADability.Polynom[px.Length];
            pz1 = new CADability.Polynom[px.Length];
            if (pw != null) pw1 = new CADability.Polynom[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                px1[i] = px[i].Derivate(new int[] { 1 });
                py1[i] = py[i].Derivate(new int[] { 1 });
                pz1[i] = pz[i].Derivate(new int[] { 1 });
                if (pw != null && pw[i] != null) pw1[i] = pw[i].Derivate(new int[] { 1 });
            }
        }
        private void CalcIntegral()
        {
            pxi = new CADability.Polynom[px.Length];
            pyi = new CADability.Polynom[px.Length];
            pzi = new CADability.Polynom[px.Length];
            if (pw != null) pwi = new CADability.Polynom[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                pxi[i] = px[i].Integrate(new int[] { 1 });
                pyi[i] = py[i].Integrate(new int[] { 1 });
                pzi[i] = pz[i].Integrate(new int[] { 1 });
                if (pw != null) pwi[i] = pw[i].Integrate(1);
            }
        }
        public void CalcDerivatives(bool first, bool second, bool integral)
        {
            if (first && px1 == null) CalcDeriv1();
            if (second && px2 == null) CalcDeriv2();
            if (integral && pxi == null) CalcIntegral();
        }
    }
    /// <summary>
    /// Curve defined by 3 polynomials in one veriable ("u"), or by 4 polynoms with an additional "w" component (homogenuous coordinates).
    /// the curve may be defined piecewise: the knots array defines the intervalls for the u parameter while the polynom arrays define the curves for each intervall
    /// </summary>
    public class ExplicitPCurve2D
    {
        public Polynom[] px, py, pw;
        public Polynom[] px1, py1, pw1; // erste Ableitung, nur einmal berechnen
        public Polynom[] px2, py2, pw2; // 2. Ableitung, ebenso
        public Polynom[] pxi, pyi, pwi; // und auch die Integrale
        public double[] knots; // eins mehr als px.Length, die Abschnitte, für die die Polynome gelten
        private static ExplicitPCurve2D unitCircle;
        public static ExplicitPCurve2D UnitCircle
        {
            get
            {
                if (unitCircle == null)
                {
                    Polynom px = new Polynom(2.34314575050762, "", -1.37258300203048, "x", -0.970562748477142, "x2");
                    Polynom py = new Polynom(3.31370849898476, "x", -0.970562748477142, "x2");
                    Polynom pw = new Polynom(2.34314575050762, "", -1.37258300203048, "x", 1.37258300203048, "x2");

                    Polynom[] ppx = new Polynom[4];
                    Polynom[] ppy = new Polynom[4];
                    Polynom[] ppw = new Polynom[4];
                    ppx[0] = px;
                    ppy[0] = py;
                    ppw[0] = pw;
                    ppx[1] = -px.Reversed().Offset(2);
                    ppy[1] = py.Reversed().Offset(2);
                    ppw[1] = pw.Reversed().Offset(2);
                    ppx[2] = -px.Offset(2);
                    ppy[2] = -py.Offset(2);
                    ppw[2] = pw.Offset(2);
                    ppx[3] = px.Reversed().Offset(4);
                    ppy[3] = -py.Reversed().Offset(4);
                    ppw[3] = pw.Reversed().Offset(4);
                    unitCircle = new ExplicitPCurve2D(ppx, ppy, ppw, new double[] { 0, 1, 2, 3, 4 });
                }
                return unitCircle;
            }
        }

        public bool IsRational
        {
            get
            {
                return pw != null;
            }
        }

        public static ExplicitPCurve2D MakeCircle(ModOp2D toCircle)
        {
            return UnitCircle.GetModified(toCircle);
        }
        public ExplicitPCurve2D GetModified(ModOp2D m)
        {
            Polynom[] ppx = new Polynom[px.Length];
            Polynom[] ppy = new Polynom[py.Length];
            Polynom[] ppw;
            if (pw == null) ppw = null;
            else ppw = new Polynom[pw.Length];
            for (int i = 0; i < px.Length; i++)
            {
                if (ppw != null)
                {
                    ppx[i] = m.At(0, 0) * px[i] + m.At(0, 1) * py[i] + m.At(0, 2) * pw[i];
                    ppy[i] = m.At(1, 0) * px[i] + m.At(1, 1) * py[i] + m.At(1, 2) * pw[i];
                    ppw[i] = pw[i];
                }
                else
                {
                    ppx[i] = m.At(0, 0) * px[i] + m.At(0, 1) * py[i] + new Polynom(m.At(0, 2), 1);
                    ppy[i] = m.At(1, 0) * px[i] + m.At(1, 1) * py[i] + new Polynom(m.At(1, 2), 1);
                }
            }
            double[] pknots = null;
            if (knots != null) pknots = (double[])knots.Clone();
            return new ExplicitPCurve2D(ppx, ppy, ppw, pknots);
        }
        public static ExplicitPCurve2D MakeLine(GeoPoint2D sp, GeoVector2D dir)
        {
            return new ExplicitPCurve2D(new Polynom(sp.x, "", dir.x, "u"), new Polynom(sp.y, "", dir.y, "u"));
        }
        public static ExplicitPCurve2D FromCurve(ICurve2D cv, double[] knots, int degree, bool homogenuous)
        {
            Polynom[] px = new Polynom[knots.Length - 1];
            Polynom[] py = new Polynom[knots.Length - 1];
            int ok = 0;
            if (!homogenuous)
            {
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    // a*u³+b*u²+c*u+d=cvx(u) im nicht homogenen Fall
                    Matrix m = new DenseMatrix(degree + 1, degree + 1);
                    Matrix b = new DenseMatrix(degree + 1, 2);
                    double du = (knots[i + 1] - knots[i]) / degree;
                    for (int j = 0; j < degree + 1; j++)
                    {
                        double u = knots[i] + j * du;
                        double c = 1.0;
                        for (int k = 0; k < degree + 1; k++)
                        {
                            m[j, k] = c;
                            c *= u;
                        }
                        GeoPoint2D p = cv.PointAt(u);
                        b[j, 0] = p.x;
                        b[j, 1] = p.y;
                    }
                    Matrix x = (Matrix)m.Solve(b);
                    if (x.IsValid())
                    {
                        // Matrix dbg = m.QRD().Solve(b);

                        ++ok;
                        px[i] = new Polynom(degree, 1);
                        py[i] = new Polynom(degree, 1);
                        int[] exp = new int[1]; // eindimensional (Polynom in u)
                        for (int j = 0; j < degree + 1; j++)
                        {
                            exp[0] = j;
                            px[i].Set(x[j, 0], exp);
                            py[i].Set(x[j, 1], exp);
                        }
#if DEBUG
                        double err = 0.0;
                        for (int j = 0; j < degree; j++)
                        {
                            double u = knots[i] + (j + 0.5) * du;
                            GeoPoint2D p = cv.PointAt(u);
                            GeoPoint2D q = new GeoPoint2D(px[i].Eval(u), py[i].Eval(u));
                            err += p | q;
                        }
#endif
                    }
                }
                if (ok == knots.Length - 1)
                {
                    return new ExplicitPCurve2D(px, py, null, knots);
                }
            }
            else
            {
                // homogener Fall
                // ax*u³+bx*u²+cx*u+dx -e*cvx(u)*u³-f*cvx(u)*u²-g*cvx(u)*u-h*cvx(u) = 0
                // ay*u³+by*u²+cy*u+dy -e*cvy(u)*u³-f*cvy(u)*u²-g*cvy(u)*u-h*cvy(u) = 0
                // az*u³+bz*u²+cz*u+dz -e*cvz(u)*u³-f*cvz(u)*u²-g*cvz(u)*u-h*cvz(u) = 0
                // e+f+g+h = 1
                // damit sind es 3*(deg+1) + deg+1 unbekannte, wobei die letzte Gleichung ohne einen Punkt auskommt
                // wir brauchen also (4*(deg+1)-1)/3 Punkte, was so natürlich nicht aufgeht
                Polynom[] pw = new Polynom[knots.Length - 1];

                for (int i = 0; i < knots.Length - 1; i++)
                {
                    Matrix m = new DenseMatrix(3 * (degree + 1), 3 * (degree + 1)); // ist mit 0 vorbesetzt
                    Vector b = new DenseVector(3 * (degree + 1));
                    int n = (3 * (degree + 1) - 1 + 2) / 3; // +2, damit aufgerundet wird bei /3
                    double du = (knots[i + 1] - knots[i]) / (n - 1); // (n-1) damit 1. und letzter Knoten-Punkt verwendet wird
                    for (int j = 0; j < n; j++)
                    {
                        double u = knots[i] + j * du;
                        GeoPoint2D p = cv.PointAt(u);
                        double c = 1.0;

                        for (int k = 0; k < degree + 1; k++)
                        {
                            m[j * 3, k] = c;
                            m[j * 3, k + 3 * (degree + 1)] = -c * p.x;
                            if (j * 3 + 1 < 4 * (degree + 1))
                            {
                                m[j * 3 + 1, k + degree + 1] = c;
                                m[j * 3 + 1, k + 3 * (degree + 1)] = -c * p.y;
                            }
                            c *= u;
                        }
                    }
                    for (int k = 0; k < degree + 1; k++)
                    {
                        m[m.RowCount - 1, k + 3 * (degree + 1)] = 1.0; // letzte Zeile hat für die Koeffizienten von pw nur 1.0, sonst 0.0
                    }

                    b[b.Count - 1] = 1.0; // das ganze b ist 0.0 bis auf die letzte Zeile
                    Vector x = (Vector)m.Solve(b);
                    if (x != null)
                    {
                        ++ok;
                        px[i] = new Polynom(degree, 1);
                        py[i] = new Polynom(degree, 1);
                        pw[i] = new Polynom(degree, 1);
                        int[] exp = new int[1]; // eindimensional (Polynom in u)
                        for (int j = 0; j < degree + 1; j++)
                        {
                            exp[0] = j;
                            px[i].Set(x[j], exp);
                            py[i].Set(x[j + degree + 1], exp);
                            pw[i].Set(x[j + 2 * (degree + 1)], exp);
                        }
#if DEBUG
                        double err = 0.0;
                        for (int j = 0; j < n; j++)
                        {
                            double u = knots[i] + (j + 0.5) * du;
                            GeoPoint2D p = cv.PointAt(u);
                            double w = pw[i].Eval(u);
                            GeoPoint2D q = new GeoPoint2D(px[i].Eval(u) / w, py[i].Eval(u) / w);
                            err += p | q;
                        }
#endif
                    }
                }
                if (ok == knots.Length - 1)
                {
                    return new ExplicitPCurve2D(px, py, pw, knots);
                }

            }
            return null;
        }
        public ExplicitPCurve2D(Polynom px, Polynom py, Polynom pw = null)
        {
            this.px = new Polynom[] { px };
            this.py = new Polynom[] { py };
            this.pw = new Polynom[] { pw };
            knots = null;
        }
        public ExplicitPCurve2D(Polynom[] px, Polynom[] py, Polynom[] pw = null, double[] knots = null)
        {
            this.px = px;
            this.py = py;
            this.pw = pw;
            this.knots = knots;
        }
        public GeoPoint2D PointAt(double u)
        {
            int ind = 0;
            if (knots != null) // nur ein Abschnitt, unendlich
            {
                ind = -1;
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    if (knots[i] <= u && knots[i + 1] > u)
                    {
                        ind = i;
                        break;
                    }
                }
                if (ind == -1)
                {
                    if (Math.Abs(u - knots[0]) < Math.Abs(u - knots[knots.Length - 1])) ind = 0;
                    else ind = knots.Length - 2;
                }
            }
            if (pw != null && pw[ind] != null)
            {
                double w = pw[ind].Eval(u);
                return new GeoPoint2D(px[ind].Eval(u) / w, py[ind].Eval(u) / w);
            }
            else
            {
                return new GeoPoint2D(px[ind].Eval(u), py[ind].Eval(u));
            }

        }
        public double PositionOf(GeoPoint2D p, out double dist, double lowerParameter = 0.0, double upperParameter = 1.0)
        {
            if (px1 == null) CalcDeriv1();
            int lower = 0;
            int upper = 1;
            if (knots != null)
            {
                double s = knots[0] + lowerParameter * (knots[knots.Length - 1] - knots[0]);
                double e = knots[0] + upperParameter * (knots[knots.Length - 1] - knots[0]);
                for (int i = 0; i < knots.Length; i++)
                {
                    if (knots[i] <= s) lower = i;
                    if (knots[i] >= e)
                    {
                        upper = i;
                        break;
                    }
                }
            }
            double bestDist = double.MaxValue;
            double res = double.MaxValue;
            double dkn = 0.0;
            if (knots != null) dkn = (knots[knots.Length - 1] - knots[0]) * 1e-6;
            for (int ind = lower; ind < upper; ind++)
            {
                Polynom diff;
                if (pw != null && pw[ind] != null)
                {
                    diff = px[ind] * px1[ind] - p.x * px1[ind] * pw[ind] + py[ind] * py1[ind] - p.y * py1[ind] * pw[ind];
                }
                else
                {
                    diff = (px[ind] - p.x) * px1[ind] + (py[ind] - p.y) * py1[ind];
                }


                double[] roots = diff.Roots();

                for (int i = 0; i < roots.Length; i++)
                {
                    if (knots == null || (roots[i] >= knots[ind] - dkn && roots[i] <= knots[ind + 1] + dkn))
                    {
                        GeoPoint2D ev;
                        if (pw != null && pw[ind] != null)
                        {
                            double w = pw[ind].Eval(roots[i]);
                            ev = new GeoPoint2D(px[ind].Eval(roots[i]) / w, py[ind].Eval(roots[i]) / w);

                        }
                        else
                        {
                            ev = new GeoPoint2D(px[ind].Eval(roots[i]), py[ind].Eval(roots[i]));
                        }

                        double d = ev | p;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            res = roots[i];
                        }
                    }
                }
            }
            dist = bestDist;
            return res;
        }
        public double NormaliseParameter(double knotParameter)
        {
            if (knots != null)
            {
                return (knotParameter - knots[0]) / (knots[knots.Length - 1] - knots[0]);
            }
            else
            {
                return knotParameter;
            }
        }
        /// <summary>
        /// Returns the area swept by the vector from (0,0) to the points on the curve 
        /// </summary>
        /// <returns></returns>
        internal double RawArea()
        {
            GeoPoint2D sp = PointAt(knots[0]);
            double a = 0.0;
            for (int i = 0; i < knots.Length; i++)
            {
                GeoPoint2D ep = PointAt(knots[i]);
                a += (sp.x * ep.y - sp.y * ep.x) / 2.0;
                sp = ep;
            }
            return a;
        }
        public double Area()
        {
            return Area(knots[0], knots[knots.Length - 1]);
        }
        /// <summary>
        /// Returns the area swept by the vector from (0,0) to the points on the curve from parameter sp to ep
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="ep"></param>
        /// <returns></returns>
        public double Area(double sp, double ep)
        {   // see: http://www2.hs-esslingen.de/~mohr/mathematik/me1/diff-geo.pdf, Kap. 5
            if (px1 == null) CalcDeriv1();
            double a = 0.0;
            if (pw == null)
            {
                for (int i = 0; i < knots.Length - 1; i++)
                {
                    if (sp < knots[i + 1] && ep > knots[i])
                    {
                        Polynom intg = (px[i] * py1[i] - px1[i] * py[i]).Integrate(new int[] { 1 });
                        a += (intg.Eval(Math.Min(ep, knots[i + 1])) - intg.Eval(Math.Max(sp, knots[i])));
                    }
                }
            }
            else
            {
                // siehe http://www.sosmath.com/calculus/integration/rational/Tablefraction/Tablefraction.html und http://www.sosmath.com/calculus/integration/rational/rational.html
                // Die Idee, wenn pw max. Grad 2 ist (z.B. Kreise, Ellipsen), was eigentlich immer der Fall ist: die Ableitung einer rationalen hat nach der Quotientenregel pw²
                // im Nenner, was für die Zeile "Polynom intg = (px[i] * py1[i] + px1[i] * py[i]).Integrate(new int[] { 1 });" bedeutet, dass jeweils pw³ im Nenner steht
                // Das lässt sich mit der letzten Zeile aus Tablefraction.html lösen. Dazu fassen wir beide Summanden zusammen, dann brauchen wir zunächst die Polynomdivision, damit
                // im Zähler nur (ax + b) steht (das ungebrochene Polynom wird wie oben integriert). Jetzt muss man das x durch k*t substituieren, so dass im Zähler und Nenner das selbe a steht
                // [ (dx + e) / (fx² + gx + h)³ -> mit x = k*t folgt k = d/f, also x = d/f*t, wobei das a zu d*d/f wird (falls f==f gilt die 2. Zeile)
                //  ([d*d/f]*t + e) / ([d*d/f]*t² +g*d/f*t + h)³ ->
                //  ([d*d/f]*t + g*d/f + (e-g*d/f)) / ([d*d/f]*t² +g*d/f*t + h)³ ->
                //  ([d*d/f]*t + g*d/f) / ([d*d/f]*t² +g*d/f*t + h)³  + + (e-g*d/f) / ([d*d/f]*t² +g*d/f*t + h)³ ]
                // der 1. Summand lässt sich nun mit der letzten Zeile aus Tablefraction.html lösen, der 2. Summand mit der 5. Zeile,
                // wobei die Regel zweimal angewendet werden muss. Das ergibt letztlich eine lange Formel mit dem ata aus der 3. Zeile und ist somit nur noch Schreibarbeit
                // siehe auch https://de.wikibooks.org/wiki/Formelsammlung_Mathematik:_Unbestimmte_Integrale_rationaler_Funktionen#Integrale,_die_ax2_+_bx_+_c_enthalten
            }
            return a / 2.0; // warum hier Minus? Wenigstens ein fall gefunden, wo es - sein muss
        }
        //public double RadiusAt(double u)
        //{
        //    int ind = 0;
        //    if (knots != null) // nur ein Abschnitt, unendlich
        //    {
        //        for (int i = 0; i < knots.Length - 1; i++)
        //        {
        //            if (knots[i] <= u && knots[i + 1] > u)
        //            {
        //                ind = i;
        //                break;
        //            }
        //        }
        //    }
        //    if (px1 == null) CalcDeriv1();
        //    if (px2 == null) CalcDeriv2();
        //    if (pw != null && pw[ind] != null)
        //    {
        //        RationalPolynom rx1 = new RationalPolynom(px[ind], pw[ind]).Derivate();
        //        RationalPolynom ry1 = new RationalPolynom(py[ind], pw[ind]).Derivate();
        //        RationalPolynom rx2 = rx1.Derivate();
        //        RationalPolynom ry2 = ry1.Derivate();
        //        GeoVector2D d1 = new GeoVector2D(rx1.Eval(u), ry1.Eval(u));
        //        GeoVector2D d2 = new GeoVector2D(rx2.Eval(u), ry2.Eval(u));
        //        return Math.Pow(d1.Length, 3) / (d1 ^ d2).Length;
        //    }
        //    else
        //    {
        //        GeoVector d1 = new GeoVector(px1[ind].Eval(u), py1[ind].Eval(u), pz1[ind].Eval(u));
        //        GeoVector d2 = new GeoVector(px2[ind].Eval(u), py2[ind].Eval(u), pz2[ind].Eval(u));
        //        return Math.Pow(d1.Length, 3) / (d1 ^ d2).Length;
        //    }
        //}
        internal static double sqr(double d)
        {
            return d * d;
        }
        //        internal double[] GetCurvaturePositions(double radius)
        //        {
        //            List<double> res = new List<double>();
        //            if (px1 == null) CalcDeriv1();
        //            if (px2 == null) CalcDeriv2();
        //            int num = 1;
        //            if (knots != null) num = knots.Length - 1;
        //            for (int i = 0; i < num; i++)
        //            {
        //                if (pw == null)
        //                {
        //                    // siehe https://de.wikipedia.org/wiki/Kr%C3%BCmmung (Raumkurven) Krümmung ist invers zu Radius
        //                    // den Bruch invertieren und quadrieren, damit steht da r² = (1. Ableitung)³ / (Kreuzprodukt)² oder 
        //                    // r²*(Kreuzprodukt)² - (1. Ableitung)³ == 0
        //                    // im Versuch mit einem handgemalten BSpline, also Grad 3, entstand hier ein Polynom (curv) vom Grad 12
        //                    // was aber Lösungen auf 10 Stellen genau lieferte. Ob das Sinn macht, ist noch nicht klar
        //                    Polynom l1sqr = px1[i] * px1[i] + py1[i] * py1[i] + pz1[i] * pz1[i];
        //                    Polynom cx = py1[i] * pz2[i] - pz1[i] * py2[i];
        //                    Polynom cy = px1[i] * pz2[i] - pz1[i] * px2[i];
        //                    Polynom cz = px1[i] * py2[i] - py1[i] * px2[i];
        //                    Polynom curv = radius * radius * (cx * cx + cy * cy + cz * cz) - l1sqr * l1sqr * l1sqr;
        //                    double[] rts = curv.Roots();
        //                    for (int j = 0; j < rts.Length; j++)
        //                    {
        //                        if (knots == null)
        //                        {
        //                            // geht das dann von 0 bis 1?
        //                            if (rts[j] >= 0.0 && rts[j] <= 1.0) res.Add(rts[j]);
        //                        }
        //                        else
        //                        {
        //                            if (rts[j] >= knots[i] && rts[j] <= knots[i + 1]) res.Add(rts[j]);
        //                        }
        //                    }
        //#if DEBUG
        //                    if (knots != null)
        //                    {
        //                        double dbgpos = (knots[i + 1] + knots[i]) / 2.0;
        //                        double rad = ((l1sqr * l1sqr * l1sqr).Eval(dbgpos)) / ((cx * cx + cy * cy + cz * cz).Eval(dbgpos));
        //                        System.Diagnostics.Trace.WriteLine("Krümmung bei " + dbgpos.ToString() + ": " + rad.ToString() + ", " + l1sqr.Eval(dbgpos).ToString() + ", " + (cx * cx + cy * cy + cz * cz).Eval(dbgpos).ToString());
        //                        System.Diagnostics.Trace.WriteLine("Vergleich: " + ((l1sqr * l1sqr * l1sqr).Eval(dbgpos)).ToString() + ", " + (l1sqr.Eval(dbgpos) * l1sqr.Eval(dbgpos) * l1sqr.Eval(dbgpos)).ToString());
        //                    }
        //#endif
        //                }
        //                else
        //                {
        //                    // Im homogenen Fall betrachten wir px[i]/pw[i] als rationales Polynom
        //                    // aber es geht noch einfacher: den Nenner einfach im geiste mitführen, schließlich ist der Nenner in "curv" bei beiden Summanden gleich
        //                    // Quotientenregel fürs Ableiten!
        //                    Polynom rx1n = px1[i] * pw[i] - px[i] * pw1[i]; // durch pw[i]²
        //                    Polynom ry1n = py1[i] * pw[i] - py[i] * pw1[i]; // durch pw[i]²
        //                    Polynom rz1n = pz1[i] * pw[i] - pz[i] * pw1[i]; // durch pw[i]²
        //                    Polynom pw2 = pw[i] * pw[i];
        //                    Polynom pw2d = pw2.Derivate(1);
        //                    Polynom rx2n = rx1n.Derivate(1) * pw2 - rx1n * pw2d; // durch pw[i]^4
        //                    Polynom ry2n = ry1n.Derivate(1) * pw2 - ry1n * pw2d; // durch pw[i]^4
        //                    Polynom rz2n = rz1n.Derivate(1) * pw2 - rz1n * pw2d; // durch pw[i]^4
        //                    Polynom l1sqrn = rx1n * rx1n + ry1n * ry1n + rz1n * rz1n; // durch pw[i]^4
        //                    Polynom cxn = ry1n * rz2n - rz1n * ry2n; // durch pw[i]^6
        //                    Polynom cyn = rx1n * rz2n - rz1n * rx2n; // durch pw[i]^6
        //                    Polynom czn = rx1n * ry2n - ry1n * rx2n; // durch pw[i]^6
        //                    Polynom curvn = radius * radius * (cxn * cxn + cyn * cyn + czn * czn) - l1sqrn * l1sqrn * l1sqrn; // beide Summanden haben pw[i]^12 im Nenner, für Nullstellen also irrelevant
        //                    double[] rts = curvn.Roots();
        //                    // Im Versuch mit einer Ellipse gibt es gute Ergebnisse: curvn hat Grad 12, die Nullstellen sind aber gut

        //                    // RationalPolynom funktioniert auch, der Grad wird aber exorbitant (120) und die Nullstellen nicht so gut
        //                    //RationalPolynom rx1 = new RationalPolynom(px[i], pw[i]).Derivate();
        //                    //RationalPolynom ry1 = new RationalPolynom(py[i], pw[i]).Derivate();
        //                    //RationalPolynom rz1 = new RationalPolynom(pz[i], pw[i]).Derivate();
        //                    //RationalPolynom rx2 = rx1.Derivate();
        //                    //RationalPolynom ry2 = ry1.Derivate();
        //                    //RationalPolynom rz2 = rz1.Derivate();
        //                    //RationalPolynom l1sqr = rx1 * rx1 + ry1 * ry1 + rz1 * rz1;
        //                    //RationalPolynom cx = ry1 * rz2 - rz1 * ry2;
        //                    //RationalPolynom cy = rx1 * rz2 - rz1 * rx2;
        //                    //RationalPolynom cz = rx1 * ry2 - ry1 * rx2;
        //                    //RationalPolynom curv = radius * radius * (cx * cx + cy * cy + cz * cz) - l1sqr * l1sqr * l1sqr;
        //                    //double[] rts = curv.Roots();
        //                    for (int j = 0; j < rts.Length; j++)
        //                    {
        //                        if (knots == null)
        //                        {
        //                            // geht das dann von 0 bis 1?
        //                            if (rts[j] >= 0.0 && rts[j] <= 1.0) res.Add(rts[j]);
        //                        }
        //                        else
        //                        {
        //                            if (rts[j] >= knots[i] && rts[j] <= knots[i + 1]) res.Add(rts[j]);
        //                        }
        //                    }
        //                }
        //            }
        //            return res.ToArray();
        //        }
        //        internal double[] GetPlaneIntersection(GeoPoint loc, GeoVector dirx, GeoVector diry, double spar = double.MinValue, double epar = double.MaxValue)
        //        {   // n*(x-a)==0
        //            List<double> res = new List<double>();
        //            GeoVector normal = dirx ^ diry;
        //            if (knots != null)
        //            {
        //                for (int i = 0; i < knots.Length - 1; i++)
        //                {
        //                    if (knots[i] <= epar || knots[i + 1] >= spar)
        //                    {
        //                        Polynom p;
        //                        if (pw == null)
        //                        {
        //                            p = normal.x * (px[i] - loc.x) + normal.y * (py[i] - loc.y) + normal.z * (pz[i] - loc.z);
        //                        }
        //                        else
        //                        {
        //                            p = normal.x * (px[i] - loc.x * pw[i]) + normal.y * (py[i] - loc.y * pw[i]) + normal.z * (pz[i] - loc.z * pw[i]);
        //                            // das ist 
        //                            // normal.x * (px[i]/pw[i] - loc.x) + normal.y * (py[i]/pw[i] - loc.y) + normal.z * (pz[i]/pw[i] - loc.z)
        //                            // mit pw multipliziert. Für die Nullstellen sollte das keinen Unterschied machen. Liefert zusätzlich die Nullstellen von pw
        //                            // die es aber nicht geben darf
        //                        }
        //                        double[] rts = p.Roots();
        //                        for (int j = 0; j < rts.Length; j++)
        //                        {
        //                            if (rts[j] >= knots[i] && rts[j] <= knots[i + 1] && rts[j] >= spar && rts[j] <= epar) res.Add(rts[j]);
        //                        }
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                Polynom p;
        //                if (pw == null)
        //                {
        //                    p = normal.x * (px[0] - loc.x) + normal.y * (py[0] - loc.y) + normal.z * (pz[0] - loc.z);
        //                }
        //                else
        //                {
        //                    p = normal.x * (px[0] - loc.x * pw[0]) + normal.y * (py[0] - loc.y * pw[0]) + normal.z * (pz[0] - loc.z * pw[0]);
        //                    // das ist 
        //                    // normal.x * (px[i]/pw[i] - loc.x) + normal.y * (py[i]/pw[i] - loc.y) + normal.z * (pz[i]/pw[i] - loc.z)
        //                    // mit pw multipliziert. Für die Nullstellen sollte das keinen Unterschied machen. Liefert zusätzlich die Nullstellen von pw
        //                    // die es aber nicht geben darf
        //                }
        //                double[] rts = p.Roots();
        //                for (int j = 0; j < rts.Length; j++)
        //                {
        //                    if (rts[j] >= spar && rts[j] <= epar) res.Add(rts[j]);
        //                }
        //            }
        //            return res.ToArray();
        //        }
        private void CalcDeriv2()
        {
            px2 = new CADability.Polynom[px.Length];
            py2 = new CADability.Polynom[px.Length];
            if (pw != null) pw2 = new CADability.Polynom[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                px2[i] = px[i].Derivate(new int[] { 2 });
                py2[i] = py[i].Derivate(new int[] { 2 });
                if (pw != null) pw2[i] = pw[i].Derivate(new int[] { 2 });
            }
        }
        private void CalcDeriv1()
        {
            px1 = new CADability.Polynom[px.Length];
            py1 = new CADability.Polynom[px.Length];
            if (pw != null) pw1 = new CADability.Polynom[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                px1[i] = px[i].Derivate(new int[] { 1 });
                py1[i] = py[i].Derivate(new int[] { 1 });
                if (pw != null) pw1[i] = pw[i].Derivate(new int[] { 1 });
            }
        }
        private void CalcIntegral()
        {
            pxi = new CADability.Polynom[px.Length];
            pyi = new CADability.Polynom[px.Length];
            if (pw != null) pwi = new CADability.Polynom[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                pxi[i] = px[i].Integrate(new int[] { 1 });
                pyi[i] = py[i].Integrate(new int[] { 1 });
                if (pw != null) pwi[i] = pw[i].Integrate(new int[] { 1 });
            }
        }
    }
    /// <summary>
    /// A surface, defined by a polynom in 3 variables. The surface is defined as P(x,y,z)==0.
    /// 
    /// </summary>
    public class ImplicitPSurface
    {
        Polynom polynom;
        internal double error; // daran kann man erkennen, ob man es verwenden sollte

        public ImplicitPSurface(Polynom polynom)
        {
            this.polynom = polynom;
        }
        public ImplicitPSurface(Polynom polynom, ModOp m)
        {
            this.polynom = polynom.Modified(m);
        }
        /// <summary>
        /// Creates a quadric through these points (needs at least 10 points)
        /// </summary>
        /// <param name="samples"></param>
        public ImplicitPSurface(GeoPoint[] samples)
        {
            int degree = 2;
            List<int[]> exp = new List<int[]>();
            for (int i = 0; i <= degree; i++)
            {
                for (int j = 0; j <= degree - i; j++)
                {
                    for (int k = 0; k <= degree - i - j; k++)
                    {
                        exp.Add(new int[] { i, j, k });
                    }
                }
            }
            int nUnknown = 10; // exp.Count; // number of unknown coefficients (10 for degree 2, 20 for degree 3)
            int nPoints = samples.Length;
            // the linear equations reflect the point: polynom(point)==0
            // and the derivation: (polynom*d/dx)(point) == normal.x (same with y and z)
            // which leads to 4 equations (rows in the matrix) per point (and normal)

            int rows = Math.Max((int)Math.Sqrt(nPoints), 2);
            {
                Matrix m = new DenseMatrix(nPoints + 1, nUnknown);
                Vector b = new DenseVector(nPoints + 1);
                for (int i = 0; i < nPoints; i++)
                {
                    // the polynom is zero at a point on the surface
                    for (int j = 0; j < nUnknown; j++)
                    {
                        double d = 1;
                        for (int k = 0; k < exp[j][0]; k++) d *= samples[i].x;
                        for (int k = 0; k < exp[j][1]; k++) d *= samples[i].y;
                        for (int k = 0; k < exp[j][2]; k++) d *= samples[i].z;
                        m[i, j] = d;
                    }
                    b[i] = 0.0;
                }
                for (int i = 0; i < nUnknown; i++) m[nPoints, i] = 1.0;
                b[nPoints] = 1.0; // alle anderen sind 0.0
                QR<double> qrd = m.QR();
                // SingularValueDecomposition svd = m.SVD();
                if (qrd.IsFullRank)
                {
                    Vector x = (Vector)qrd.Solve(b);
                    if (x != null)
                    {
                        polynom = new Polynom(degree, 3);
                        for (int i = 0; i < nUnknown; i++)
                        {
                            polynom.Set(x[i], exp[i]);
                        }
                    }
                }
            }
            if (polynom == null) throw new ApplicationException("could not create implicit surface");
        }
        /// <summary>
        /// Approximate the provided surface with a imlicit polynomial surface. Uses points an normals of the surface. Yields good
        /// results for quadrics (degree==2) and for ruled surfaces.
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="uvbounds"></param>
        /// <param name="degree"></param>
        /// <param name="useNormal"></param>
        /// <param name="nPoints"></param>
        public ImplicitPSurface(ISurface surface, BoundingRect uvbounds, int degree, bool useNormal, int nPoints = 0)
        {
            List<int[]> exp = new List<int[]>();
            for (int i = 0; i <= degree; i++)
            {
                for (int j = 0; j <= degree - i; j++)
                {
                    for (int k = 0; k <= degree - i - j; k++)
                    {
                        exp.Add(new int[] { i, j, k });
                    }
                }
            }
            int nUnknown = exp.Count; // number of unknown coefficients (10 for degree 2, 20 for degree 3)
            nPoints = Math.Max(nUnknown, nPoints); // these are 4 times as many points than needed, but the result is better with more points
            // the linear equations reflect the point: polynom(point)==0
            // and the derivation: (polynom*d/dx)(point) == normal.x (same with y and z)
            // which leads to 4 equations (rows in the matrix) per point (and normal)

            int rows = Math.Max((int)Math.Sqrt(nPoints), 2);
            double dv = uvbounds.Height / rows;
            double du = rows * uvbounds.Width / nPoints;
            double u = uvbounds.Left + du / 2.0;
            double v = uvbounds.Bottom + dv / 2.0;
            if (useNormal)
            {
                Matrix m = new DenseMatrix(nPoints * 4, nUnknown);
                Vector b = new DenseVector(nPoints * 4);
                for (int i = 0; i < nPoints; i++)
                {
                    surface.DerivationAt(new GeoPoint2D(u, v), out GeoPoint p, out GeoVector diru, out GeoVector dirv);
                    GeoVector normal = (diru ^ dirv).Normalized; // we use normalized, because the sphere in u/v returns shorter diru vectors closer to the pole
                    u += du;
                    if (u > uvbounds.Width)
                    {   // next row
                        u -= uvbounds.Width;
                        v += dv;
                    }
                    // the derivations of the polynom make the normal
                    for (int j = 0; j < nUnknown; j++)
                    {
                        double d = exp[j][0];
                        for (int k = 0; k < exp[j][0] - 1; k++) d *= p.x;
                        for (int k = 0; k < exp[j][1]; k++) d *= p.y;
                        for (int k = 0; k < exp[j][2]; k++) d *= p.z;
                        m[4 * i + 0, j] = d;
                    }
                    b[4 * i + 0] = normal.x;
                    for (int j = 0; j < nUnknown; j++)
                    {
                        double d = exp[j][1];
                        for (int k = 0; k < exp[j][0]; k++) d *= p.x;
                        for (int k = 0; k < exp[j][1] - 1; k++) d *= p.y;
                        for (int k = 0; k < exp[j][2]; k++) d *= p.z;
                        m[4 * i + 1, j] = d;
                    }
                    b[4 * i + 1] = normal.y;
                    for (int j = 0; j < nUnknown; j++)
                    {
                        double d = exp[j][2];
                        for (int k = 0; k < exp[j][0]; k++) d *= p.x;
                        for (int k = 0; k < exp[j][1]; k++) d *= p.y;
                        for (int k = 0; k < exp[j][2] - 1; k++) d *= p.z;
                        m[4 * i + 2, j] = d;
                    }
                    b[4 * i + 2] = normal.z;
                    // the polynom is zero at a point on the surface
                    for (int j = 0; j < nUnknown; j++)
                    {
                        double d = 1;
                        for (int k = 0; k < exp[j][0]; k++) d *= p.x;
                        for (int k = 0; k < exp[j][1]; k++) d *= p.y;
                        for (int k = 0; k < exp[j][2]; k++) d *= p.z;
                        m[4 * i + 3, j] = d;
                    }
                    b[4 * i + 3] = 0.0;
                }
                QR<double> qrd = m.QR();
                // SingularValueDecomposition svd = m.SVD();
                if (qrd.IsFullRank)
                {
                    Vector x = (Vector)qrd.Solve(b);
                    if (x.IsValid())
                    {
                        polynom = new Polynom(degree, 3);
                        for (int i = 0; i < nUnknown; i++)
                        {
                            polynom.Set(x[i], exp[i]);
                        }
#if DEBUG
                        DebuggerContainer dc = new DebuggerContainer();
                        // nPoints += nPoints / 2; // to get different points
                        rows = (int)Math.Sqrt(nPoints);
                        dv = uvbounds.Height / rows;
                        du = rows * uvbounds.Width / nPoints;
                        u = uvbounds.Left + du / 2.0;
                        v = uvbounds.Bottom + dv / 2.0;
                        double err = 0.0;
                        for (int i = 0; i < nPoints; i++)
                        {
                            GeoPoint p = surface.PointAt(new GeoPoint2D(u, v));
                            u += du;
                            if (u > uvbounds.Width)
                            {   // next row
                                u -= uvbounds.Width;
                                v += dv;
                            }
                            GeoPoint[] fpts = this.FootPoint(p);
                            if (fpts.Length > 0)
                            {
                                GeoPoint p0 = fpts[0];
                                for (int j = 1; j < fpts.Length; j++)
                                {
                                    if ((fpts[j] | p) < (p0 | p)) p0 = fpts[j];
                                }
                                GeoVector normal = this.Normal(p0);
                                dc.Add(Line.MakeLine(p0, p0 + 10 * normal.Normalized));
                                err += (p | p0);
                            }
                        }
                        err /= nPoints; // average error
#endif
                    }
                }
            }
            else
            {
                Matrix m = new DenseMatrix(nPoints + 1, nUnknown);
                Vector b = new DenseVector(nPoints + 1);
                for (int i = 0; i < nPoints; i++)
                {
                    GeoPoint p = surface.PointAt(new GeoPoint2D(u, v));
                    u += du;
                    if (u > uvbounds.Width)
                    {   // next row
                        u -= uvbounds.Width;
                        v += dv;
                    }
                    // the polynom is zero at a point on the surface
                    for (int j = 0; j < nUnknown; j++)
                    {
                        double d = 1;
                        for (int k = 0; k < exp[j][0]; k++) d *= p.x;
                        for (int k = 0; k < exp[j][1]; k++) d *= p.y;
                        for (int k = 0; k < exp[j][2]; k++) d *= p.z;
                        m[i, j] = d;
                    }
                    b[i] = 0.0;
                }
                for (int i = 0; i < nUnknown; i++) m[nPoints, i] = 1.0;
                b[nPoints] = 1.0; // alle anderen sind 0.0
                QR<double> qrd = m.QR();
                // SingularValueDecomposition svd = m.SVD();
                if (qrd.IsFullRank)
                {
                    Vector x = (Vector)qrd.Solve(b);
                    if (x.IsValid())
                    {
                        polynom = new Polynom(degree, 3);
                        for (int i = 0; i < nUnknown; i++)
                        {
                            polynom.Set(x[i], exp[i]);
                        }
#if DEBUG
                        DebuggerContainer dc = new DebuggerContainer();
                        nPoints += nPoints / 2; // to get different points
                        rows = (int)Math.Sqrt(nPoints);
                        dv = uvbounds.Height / rows;
                        du = rows * uvbounds.Width / nPoints;
                        u = uvbounds.Left + du / 2.0;
                        v = uvbounds.Bottom + dv / 2.0;
                        double err = 0.0;
                        for (int i = 0; i < nPoints; i++)
                        {
                            GeoPoint p = surface.PointAt(new GeoPoint2D(u, v));
                            u += du;
                            if (u > uvbounds.Width)
                            {   // next row
                                u -= uvbounds.Width;
                                v += dv;
                            }
                            GeoPoint[] fpts = this.FootPoint(p);
                            if (fpts.Length > 0)
                            {
                                GeoPoint p0 = fpts[0];
                                for (int j = 1; j < fpts.Length; j++)
                                {
                                    if ((fpts[j] | p) < (p0 | p)) p0 = fpts[j];
                                }
                                GeoVector normal = this.Normal(p0);
                                dc.Add(Line.MakeLine(p0, p0 + 10 * normal.Normalized));
                                err += (p | p0);
                            }
                        }
                        err /= nPoints; // average error
#endif
                    }
                }
            }
            if (polynom == null) throw new ApplicationException("could not create implicit surface");
        }
        public ImplicitPSurface(ISurface surface, BoundingRect uvbounds, int maxdegree)
        {
            List<int[]> exp = new List<int[]>();
            for (int i = 0; i <= maxdegree; i++)
            {
                for (int j = 0; j <= maxdegree - i; j++)
                {
                    for (int k = 0; k <= maxdegree - i - j; k++)
                    {
                        exp.Add(new int[] { i, j, k });
                    }
                }
            }
            // es gibt also exp.Count unbekannte, exp sind die Exponenten für x, y und z
            // Es gilt jetzt die Kofiizienten für die Polynome P(x,y,z)==0 zu finden.
            // Da alle Koeffizienten==0 eine Lösung ist, muss noch eine zusätzliche Gleichung her,
            // die letztlich der (unbedeutenden) Skalierung dient. Ich nehme mal Summe aller Koefffizenten==1
            int n = exp.Count;
            Matrix m = new DenseMatrix(n, n);
            // suche ein Punktraster von n-1 Punkten möglichst gleichmäßig in uvbounds verteilt und möglichst nicht auf den Rändern
            int rows = (int)Math.Sqrt(n);
            double dv = uvbounds.Height / rows;
            double du = rows * uvbounds.Width / n;
            double u = uvbounds.Left + du / 2.0;
            double v = uvbounds.Bottom + dv / 2.0;
            for (int i = 0; i < n - 1; i++)
            {
                GeoPoint p = surface.PointAt(new GeoPoint2D(u, v));
                u += du;
                if (u > uvbounds.Width)
                {   // nächste Zeile
                    u -= uvbounds.Width;
                    v += dv;
                }
                for (int j = 0; j < n; j++)
                {
                    double d = 1;
                    for (int k = 0; k < exp[j][0]; k++) d *= p.x;
                    for (int k = 0; k < exp[j][1]; k++) d *= p.y;
                    for (int k = 0; k < exp[j][2]; k++) d *= p.z;
                    m[i, j] = d;
                }
            }
            // letzte Zeile: Summe aller Koeffizienten = 1
            for (int i = 0; i < n; i++) m[n - 1, i] = 1.0;
            // m[n - 1, 0] = 1.0; // Konstante==1
            Vector b = new DenseVector(n);
            b[n - 1] = 1.0; // alle anderen sind 0.0
            Vector x = (Vector)m.Solve(b);
            if (x.IsValid())
            {
                polynom = new Polynom(maxdegree, 3);
                for (int i = 0; i < n; i++)
                {
                    polynom.Set(x[i], exp[i]);
                }
#if DEBUG
                double err = 0.0;
                rows += 1; // damit testet man an völlig anderen Stellen
                dv = uvbounds.Height / rows;
                du = rows * uvbounds.Width / n;
                u = uvbounds.Left + du / 2.0;
                v = uvbounds.Bottom + dv / 2.0;
                for (int i = 0; i < n - 1; i++)
                {
                    GeoPoint p = surface.PointAt(new GeoPoint2D(u, v));
                    u += du;
                    if (u > uvbounds.Width)
                    {   // nächste Zeile
                        u -= uvbounds.Width;
                        v += dv;
                    }
                    double d = polynom.Eval(p.x, p.y, p.z);
                    err += Math.Abs(d);
                }
                error = err;
                System.Diagnostics.Trace.WriteLine("Error: " + err.ToString());
#endif
            }
        }
        public ImplicitPSurface(ISurface surface, BoundingRect uvbounds, int maxdegree, double precision)
        {
            // ziemlich uneffizient
            // besser: die Punkte nur einmal ausrechnen
            for (int md = 2; md <= maxdegree; md++)
            {
                List<int[]> exp = new List<int[]>();
                for (int i = 0; i <= md; i++)
                {
                    for (int j = 0; j <= md - i; j++)
                    {
                        for (int k = 0; k <= md - i - j; k++)
                        {
                            exp.Add(new int[] { i, j, k });
                        }
                    }
                }
                // es gibt also exp.Count unbekannte, exp sind die Exponenten für x, y und z
                // Es gilt jetzt die Kofiizienten für die Polynome P(x,y,z)==0 zu finden.
                // Da alle Koeffizienten==0 eine Lösung ist, muss noch eine zusätzliche Gleichung her,
                // die letztlich der (unbedeutenden) Skalierung dient. Ich nehme mal Summe aller Koefffizenten==1
                int n = exp.Count;
                Matrix m = new DenseMatrix(n, n);
                // suche ein Punktraster von n-1 Punkten möglichst gleichmäßig in uvbounds verteilt und möglichst nicht auf den Rändern
                int rows = (int)Math.Sqrt(n);
                double dv = uvbounds.Height / rows;
                double du = rows * uvbounds.Width / n;
                double u = uvbounds.Left + du / 2.0;
                double v = uvbounds.Bottom + dv / 2.0;
                for (int i = 0; i < n - 1; i++)
                {
                    GeoPoint p = surface.PointAt(new GeoPoint2D(u, v));
                    u += du;
                    if (u > uvbounds.Width)
                    {   // nächste Zeile
                        u -= uvbounds.Width;
                        v += dv;
                    }
                    for (int j = 0; j < n; j++)
                    {
                        double d = 1;
                        for (int k = 0; k < exp[j][0]; k++) d *= p.x;
                        for (int k = 0; k < exp[j][1]; k++) d *= p.y;
                        for (int k = 0; k < exp[j][2]; k++) d *= p.z;
                        m[i, j] = d;
                    }
                }
                // letzte Zeile: Summe aller Koeffizienten = 1
                for (int i = 0; i < n; i++) m[n - 1, i] = 1.0;
                // m[n - 1, 0] = 1.0; // Konstante==1
                Vector b = new DenseVector(n);
                b[n - 1] = 1.0; // alle anderen sind 0.0
                Vector x = (Vector)m.Solve(b);
                if (x.IsValid())
                {
                    polynom = new Polynom(md, 3);
                    for (int i = 0; i < n; i++)
                    {
                        polynom.Set(x[i], exp[i]);
                    }
                    double err = 0.0;
                    rows += 1; // damit testet man an völlig anderen Stellen
                    dv = uvbounds.Height / rows;
                    du = rows * uvbounds.Width / n;
                    u = uvbounds.Left + du / 2.0;
                    v = uvbounds.Bottom + dv / 2.0;
                    for (int i = 0; i < n - 1; i++)
                    {
                        GeoPoint p = surface.PointAt(new GeoPoint2D(u, v));
                        u += du;
                        if (u > uvbounds.Width)
                        {   // nächste Zeile
                            u -= uvbounds.Width;
                            v += dv;
                        }
                        double d = polynom.Eval(p.x, p.y, p.z);
                        err += Math.Abs(d);
                    }
                    System.Diagnostics.Trace.WriteLine("Error: " + err.ToString());
                    error = err;
                    if (err < precision) break;
                }
            }
        }
        /// <summary>
        /// Modifies the surface into it's "canonical" position. A sphere will be around the origin, a cylinder will be around the z-axis,
        /// a cone will be around the z-axis with its apex at the origin. The returned ModOp modifies the canonical form into the original form.
        /// Makes only sense for quadrics
        /// </summary>
        /// <returns>the modification that transforms the canonical form into the initial form</returns>
        public ModOp LagrangeReduction3()
        {
            if (polynom.Degree != 2) return ModOp.Null;

            Matrix A = new DenseMatrix(3, 3);
            A[0, 0] = polynom.Get(2, 0, 0);
            A[1, 1] = polynom.Get(0, 2, 0);
            A[2, 2] = polynom.Get(0, 0, 2);
            A[0, 1] = A[1, 0] = polynom.Get(1, 1, 0) / 2.0;
            A[0, 2] = A[2, 0] = polynom.Get(1, 0, 1) / 2.0;
            A[1, 2] = A[2, 1] = polynom.Get(0, 1, 1) / 2.0;
            Evd<double> AEigen = A.Evd();
            Matrix ev = (Matrix)AEigen.EigenVectors;
            ModOp m1 = new ModOp(ev.ToArray());
            Polynom mod1 = polynom.Modified(m1); // the resulting polynom should not have mixed terms (like x*y or y*z)
            mod1.Set(0.0, 1, 1, 0); // these values should be almost zero anyhow.
            mod1.Set(0.0, 1, 0, 1);
            mod1.Set(0.0, 0, 1, 1);
            double tx = -mod1.Get(1, 0, 0) / (2 * mod1.Get(2, 0, 0));
            double ty = -mod1.Get(0, 1, 0) / (2 * mod1.Get(0, 2, 0));
            double tz = -mod1.Get(0, 0, 1) / (2 * mod1.Get(0, 0, 2));
            ModOp m2 = ModOp.Translate(tx, ty, tz);
            Polynom mod2 = mod1.Modified(m2); // // the resulting polynom should have no linear terms
            mod2.Set(0.0, 1, 0, 0); // these values should be almost zero anyhow.
            mod2.Set(0.0, 0, 1, 0);
            mod2.Set(0.0, 0, 0, 1);
            // mod2 only has x², y² and z² (and a constant), all other coefficients are 0
            // x² should have the greatest coefficient
            ModOp m3;
            double cx = Math.Abs(mod2.Get(2, 0, 0));
            double cy = Math.Abs(mod2.Get(0, 2, 0));
            double cz = Math.Abs(mod2.Get(0, 0, 2));
            double scale;
            if (cx > cy && cx > cz)
            {
                scale = mod2.Get(2, 0, 0);
                if (cy > cz)
                {
                    m3 = new ModOp(GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, GeoPoint.Origin);
                }
                else
                {
                    m3 = new ModOp(GeoVector.XAxis, GeoVector.ZAxis, GeoVector.YAxis, GeoPoint.Origin);
                }
            }
            else if (cy > cz)
            {
                scale = mod2.Get(0, 2, 0);
                if (cx > cz)
                {
                    m3 = new ModOp(GeoVector.YAxis, GeoVector.XAxis, GeoVector.ZAxis, GeoPoint.Origin);
                }
                else
                {
                    m3 = new ModOp(GeoVector.YAxis, GeoVector.ZAxis, GeoVector.XAxis, GeoPoint.Origin);
                }
            }
            else
            {
                scale = mod2.Get(0, 0, 2);
                if (cx > cy)
                {
                    m3 = new ModOp(GeoVector.ZAxis, GeoVector.XAxis, GeoVector.YAxis, GeoPoint.Origin);
                }
                else
                {
                    m3 = new ModOp(GeoVector.ZAxis, GeoVector.YAxis, GeoVector.XAxis, GeoPoint.Origin);
                }
            }
            // m3 = m3 * ModOp.Scale(1.0 / (2 * scale)); // don't understand why 2*scale, but seems correct (no!)
            // don't scale, leave the surface unscaled
            polynom = mod2.Modified(m3);
            polynom = (1.0 / polynom.Get(2, 0, 0)) * polynom; // make coefficient of x² = 1.0, doesn't change the surface
            return m1 * m2 * m3;
        }
        /// <summary>
        /// Returns the implicit polynom of this surface (i.e. polynom(x,y,z)==0 for all points on the surface)
        /// </summary>
        public Polynom Polynom
        {
            get
            {
                return polynom;
            }
        }
        /// <summary>
        /// Returns the intersection points of the provided curve with this surface. 
        /// </summary>
        /// <param name="c3d">curve to intersect with</param>
        /// <param name="u">returns the parameter values for the curve of the intersection points</param>
        /// <returns>the intersection points</returns>
        public GeoPoint[] Intersect(ExplicitPCurve3D c3d, out double[] u)
        {
            List<double> ures = new List<double>();
            List<GeoPoint> pres = new List<GeoPoint>();
            for (int i = 0; i < c3d.px.Length; i++)
            {
                Polynom rp;
                if (c3d.pw == null || c3d.pw[i] == null)
                {
                    rp = polynom.Substitute(c3d.px[i], c3d.py[i], c3d.pz[i]);
                }
                else
                {
                    rp = polynom.SubstituteRational(c3d.px[i], c3d.py[i], c3d.pz[i], c3d.pw[i]);
                }
                double[] rts = rp.Roots();
                for (int j = 0; j < rts.Length; j++)
                {
                    if (c3d.knots == null || (rts[j] >= c3d.knots[i] && rts[j] <= c3d.knots[i + 1]))
                    {
                        pres.Add(c3d.PointAt(rts[j]));
                        ures.Add(rts[j]);
                    }
                }
            }
            u = ures.ToArray();
            return pres.ToArray();
        }
        public GeoPoint[] Intersect(GeoPoint sp, GeoVector dir, out double[] u)
        {
            Polynom px = new Polynom(sp.x, "", dir.x, "u");
            Polynom py = new Polynom(sp.y, "", dir.y, "u");
            Polynom pz = new Polynom(sp.z, "", dir.z, "u");
            List<double> ures = new List<double>();
            List<GeoPoint> pres = new List<GeoPoint>();
            Polynom rp = polynom.Substitute(px, py, pz);
            double[] rts = rp.Roots();
            for (int j = 0; j < rts.Length; j++)
            {
                pres.Add(sp + rts[j] * dir);
                ures.Add(rts[j]);
            }
            u = ures.ToArray();
            return pres.ToArray();
        }
        /// <summary>
        /// Returns a list of all points of the surface, where there is a minimum or maximum distance to the plane, defined by
        /// loc, dirx, diry. Returns only those points, which have a (perpendicular) footpoint in the parallelogram, defined by
        /// the parameters.
        /// </summary>
        /// <param name="loc"></param>
        /// <param name="dirx"></param>
        /// <param name="diry"></param>
        /// <returns></returns>
        public GeoPoint[] MinMaxToPatch(GeoPoint loc, GeoVector dirx, GeoVector diry)
        {   // das war ein Denkfehler, das geht so nicht....
            // aber: wie in https://de.wikipedia.org/wiki/Implizite_Fl%C3%A4che unter Normalkrümmung gezeigt,
            // kann man die Krümmung in einem Punkt berechnen (grad F ist der Gradient, also der Normalenvektor in diesem Punkt,
            // der ja für einen Punkt konstant ist). Somit müsste man Minimum und Maximum bestimmen können. un in die Richtung
            // mit maximaler Krümmumg müsste man eine Linie machen auf der man das Maximum sucht...
            List<GeoPoint> res = new List<GeoPoint>();

            GeoVector mdirx = new GeoVector(dirx.x, dirx.y, 0);
            GeoVector mdiry = new GeoVector(diry.x, diry.y, 0);
            GeoVector n = dirx ^ diry;
            n.z = 0.0;
            ModOp to2d = new ModOp(mdirx, n, mdiry, loc);
            // bildet ab, auf eine Ebene, bei der das Parallelogramm als waagrechter Strich erscheint (X-Achse) und diry verschwindet.
            Polynom inPlane = polynom.Modified(to2d);
            // das Polynom pm ist zwar noch 3dimensional, allerdings kommt das z darin nicht mehr vor.
            Polynom inPlane2d = new Polynom(inPlane.Degree, 2);
            int[] exp = new int[2];
            foreach (monom monom in inPlane)
            {
                exp[0] = monom.exp[0];
                exp[1] = monom.exp[1];
                inPlane2d.Set(monom.Coefficient, exp);
            }
            // inPlane2d ist ein Polynom in x und y
            return res.ToArray();
        }
        public GeoPoint[] MinMaxToCurve(ExplicitPCurve3D c3d, out double[] u)
        {
            List<double> ures = new List<double>();
            List<GeoPoint> pres = new List<GeoPoint>();
            for (int i = 0; i < c3d.px.Length; i++)
            {
                Polynom rp;
                if (c3d.pw == null || c3d.pw[i] == null)
                {
                    rp = polynom.Substitute(c3d.px[i], c3d.py[i], c3d.pz[i]);
                }
                else
                {
                    rp = polynom.SubstituteRational(c3d.px[i], c3d.py[i], c3d.pz[i], c3d.pw[i]);
                }
                rp = rp.Derivate(1); // 1. Ableitung: die Nullstellen sind die Orte der minimalen und maximalen Abstände zur Kurve
                double[] rts = rp.Roots();
                for (int j = 0; j < rts.Length; j++)
                {
                    if (c3d.knots == null || (rts[j] >= c3d.knots[i] && rts[j] <= c3d.knots[i + 1]))
                    {
                        pres.Add(c3d.PointAt(rts[j]));
                        ures.Add(rts[j]);
                    }
                }
            }
            u = ures.ToArray();
            return pres.ToArray();
        }
        public GeoPoint[] FootPoint(GeoPoint fromHere)
        {
            GeoVector normal = new GeoVector(
            polynom.Derivate(1, 0, 0).Eval(fromHere.x, fromHere.y, fromHere.z),
            polynom.Derivate(0, 1, 0).Eval(fromHere.x, fromHere.y, fromHere.z),
            polynom.Derivate(0, 0, 1).Eval(fromHere.x, fromHere.y, fromHere.z));
            double[] u;
            GeoPoint[] res = Intersect(ExplicitPCurve3D.MakeLine(fromHere, normal), out u);
            return res;
        }
        public GeoVector Normal(GeoPoint fromHere)
        {
            GeoVector normal = new GeoVector(
            polynom.Derivate(1, 0, 0).Eval(fromHere.x, fromHere.y, fromHere.z),
            polynom.Derivate(0, 1, 0).Eval(fromHere.x, fromHere.y, fromHere.z),
            polynom.Derivate(0, 0, 1).Eval(fromHere.x, fromHere.y, fromHere.z));
            return normal;
        }
        public double PolynomialDistance(GeoPoint fromHere)
        {
            return polynom.Eval(fromHere.x, fromHere.y, fromHere.z);
        }
        public double[] PerpendicularToGradient(GeoPoint lineStart, GeoVector lineDirection)
        {
            Polynom linex = new Polynom(lineStart.x, "", lineDirection.x, "x");
            Polynom liney = new Polynom(lineStart.y, "", lineDirection.y, "x");
            Polynom linez = new Polynom(lineStart.z, "", lineDirection.z, "x");
            Polynom dx = polynom.Derivate(1, 0, 0).Substitute(linex, liney, linez);
            Polynom dy = polynom.Derivate(0, 1, 0).Substitute(linex, liney, linez);
            Polynom dz = polynom.Derivate(0, 0, 1).Substitute(linex, liney, linez);
            Polynom toSolve = lineDirection.x * dx + lineDirection.y * dy + lineDirection.z * dz;
            return toSolve.Roots();
        }
        public double[] PerpendicularToGradient(ExplicitPCurve3D curve)
        {
            if (curve.pw != null)
            {
                List<double> res = new List<double>();
                for (int i = 0; i < curve.px.Length - 1; i++)
                {

                    Polynom dx = polynom.Derivate(1, 0, 0).SubstituteRational(curve.px[i], curve.py[i], curve.pz[i], curve.pw[i]);
                    Polynom dy = polynom.Derivate(0, 1, 0).SubstituteRational(curve.px[i], curve.py[i], curve.pz[i], curve.pw[i]);
                    Polynom dz = polynom.Derivate(0, 0, 1).SubstituteRational(curve.px[i], curve.py[i], curve.pz[i], curve.pw[i]);
                    curve.CalcDerivatives(true, false, false);
                    // according to Quotient rule:
                    // (px1[i]*pw[i] - px[i]*pw1[i])/pw[i]², ignore nominator, because it is the same for all and doesn't influence the roots
                    Polynom toSolve = (curve.px1[i] * curve.pw[i] - curve.px[i] * curve.pw1[i]) * dx + (curve.py1[i] * curve.pw[i] - curve.py[i] * curve.pw1[i]) * dy + (curve.pz1[i] * curve.pw[i] - curve.pz[i] * curve.pw1[i]) * dz;
                    double[] roots = toSolve.Roots();
                    for (int j = 0; j < roots.Length; j++)
                    {
                        if (curve.knots[i] <= roots[j] && curve.knots[i + 1] >= roots[j]) res.Add(roots[j]);
                    }
                }
                return res.ToArray();
            }
            else
            {
                List<double> res = new List<double>();
                for (int i = 0; i < curve.px.Length - 1; i++)
                {

                    Polynom dx = polynom.Derivate(1, 0, 0).Substitute(curve.px[i], curve.py[i], curve.pz[i]);
                    Polynom dy = polynom.Derivate(0, 1, 0).Substitute(curve.px[i], curve.py[i], curve.pz[i]);
                    Polynom dz = polynom.Derivate(0, 0, 1).Substitute(curve.px[i], curve.py[i], curve.pz[i]);
                    curve.CalcDerivatives(true, false, false);
                    Polynom toSolve = curve.px1[i] * dx + curve.py1[i] * dy + curve.pz1[i] * dz;
                    double[] roots = toSolve.Roots();
                    for (int j = 0; j < roots.Length; j++)
                    {
                        if (curve.knots[i] <= roots[j] && curve.knots[i + 1] >= roots[j]) res.Add(roots[j]);
                    }
                }
                return res.ToArray();
            }
        }
    }
    internal class ImplicitPCurve2D
    {
        private Polynom polynom;
        public static int PointsNeeded(int degree, bool onlyEvenDegree)
        {
            int res = 0;
            for (int i = 0; i <= degree; i++)
            {
                for (int j = 0; j <= degree - i; j++)
                {
                    if (!onlyEvenDegree || ((i + j) & 0x01) == 0) ++res;
                }
            }
            return res;
        }
        public ImplicitPCurve2D(GeoPoint2D[] pnts, int maxdegree, bool onlyEvenDegree)
        {
            List<int[]> exp = new List<int[]>();
            for (int i = 0; i <= maxdegree; i++)
            {
                for (int j = 0; j <= maxdegree - i; j++)
                {
                    if (!onlyEvenDegree || ((i + j) & 0x01) == 0)
                        exp.Add(new int[] { i, j });
                }
            }
            // es gibt also exp.Count unbekannte, exp sind die Exponenten für x, y und z
            // Es gilt jetzt die Kofiizienten für die Polynome P(x,y,z)==0 zu finden.
            // Da alle Koeffizienten==0 eine Lösung ist, muss noch eine zusätzliche Gleichung her,
            // die letztlich der (unbedeutenden) Skalierung dient. Ich nehme mal Summe aller Koefffizenten==1
            int n = exp.Count;
            Matrix m = new DenseMatrix(pnts.Length + 1, n);
            for (int i = 0; i < pnts.Length; i++)
            {
                GeoPoint2D p = pnts[i];
                for (int j = 0; j < n; j++)
                {
                    double d = 1;
                    for (int k = 0; k < exp[j][0]; k++) d *= p.x;
                    for (int k = 0; k < exp[j][1]; k++) d *= p.y;
                    m[i, j] = d;
                }
            }
            // letzte Zeile: Summe aller Koeffizienten = 1
            for (int i = 0; i < n; i++) m[pnts.Length, i] = 1.0;
            // m[n - 1, 0] = 1.0; // Konstante==1
            Vector b = new DenseVector(pnts.Length + 1);
            b[pnts.Length] = 1.0; // alle anderen sind 0.0
            m.QR().Solve(b);
            Vector x = (Vector)m.QR().Solve(b);
            polynom = new Polynom(maxdegree, 2);
            for (int i = 0; i < n; i++)
            {
                polynom.Set(x[i], exp[i]);
            }
        }

        public Polynom Polynom
        {
            get
            {
                return polynom;
            }
        }
    }
    internal class ExplicitPSurface
    {
        public Polynom px, py, pz, pw; // zunächst nicht gestückelt, diimension==2, d.h. (u,v)
        public Polynom pxu, pyu, pzu, pwu, pxv, pyv, pzv, pwv; // die Ableitungen nach u bzw. v
        public ExplicitPSurface(ISurface surface, BoundingRect uvbounds, int uDegree, int vDegree, bool homogenous)
        {
            // es gibt die gemischten bis zum vollen Grad für u und v, d.h. bei degree==3 gibts auch u³*v³
            // wenn homogen, dann (x,y,z) = P(u,v)/Pw(u,v);
            // Pw(u,v) ist scalar, also x*Pw(u,v) = P(u,v) oder Px(u,v)-x*Pw(u,v) == 0
            // da hierbei die Skalierung beliebig ist, kann man noch die Summe der Pw-Koeffizienten==1 setzen
            List<int[]> exp = new List<int[]>();
            for (int i = 0; i <= uDegree; i++)
            {
                for (int j = 0; j <= vDegree; j++)
                {
                    exp.Add(new int[] { i, j });
                }
            }
            // es gibt also exp.Count unbekannte, exp sind die Exponenten für u und v
            // Es gilt jetzt die Kofiizienten für die Polynome P(u,v)==PointAt(u,v) zu finden.
            int n = exp.Count;
            Matrix m = new DenseMatrix(n, n);
            // px(u,v) == x, analog y und z
            // suche ein Punktraster von n-1 Punkten möglichst gleichmäßig in uvbounds verteilt und möglichst nicht auf den Rändern
            int rows = (int)Math.Sqrt(n);
            double dv = uvbounds.Height / rows;
            double du = rows * uvbounds.Width / n;
            double u = uvbounds.Left + du / 2.0;
            double v = uvbounds.Bottom + dv / 2.0;
            Matrix b = new DenseMatrix(n, 3);
            for (int i = 0; i < n; i++)
            {
                GeoPoint p = surface.PointAt(new GeoPoint2D(u, v));
                for (int j = 0; j < n; j++)
                {
                    double d = 1;
                    for (int k = 0; k < exp[j][0]; k++) d *= u;
                    for (int k = 0; k < exp[j][1]; k++) d *= v;
                    m[i, j] = d;
                }
                u += du;
                if (u > uvbounds.Width)
                {   // nächste Zeile
                    u -= uvbounds.Width;
                    v += dv;
                }
                b[i, 0] = p.x;
                b[i, 1] = p.y;
                b[i, 2] = p.z;
            }
            Matrix x = (Matrix)m.Solve(b);
            if (x.IsValid())
            {
                px = new Polynom(uDegree + vDegree, 2);
                py = new Polynom(uDegree + vDegree, 2);
                pz = new Polynom(uDegree + vDegree, 2);
                for (int i = 0; i < n; i++)
                {
                    px.Set(x[i, 0], exp[i]);
                    py.Set(x[i, 1], exp[i]);
                    pz.Set(x[i, 2], exp[i]);
                }
#if DEBUG
                double err = 0.0;
                rows += 1; // damit testet man an völlig anderen Stellen
                dv = uvbounds.Height / rows;
                du = rows * uvbounds.Width / n;
                u = uvbounds.Left + du / 2.0;
                v = uvbounds.Bottom + dv / 2.0;
                for (int i = 0; i < n - 1; i++)
                {
                    GeoPoint p = surface.PointAt(new GeoPoint2D(u, v));
                    double xx = px.Eval(u, v);
                    double yy = py.Eval(u, v);
                    double zz = pz.Eval(u, v);
                    err += new GeoPoint(xx, yy, zz) | p;
                    u += du;
                    if (u > uvbounds.Width)
                    {   // nächste Zeile
                        u -= uvbounds.Width;
                        v += dv;
                    }
                }
                System.Diagnostics.Trace.WriteLine("Error: " + err.ToString());
                // versuche die Geschichte umzudrehen für "PositionOf"
                // d.h. ein Polynom, bei dem gilt P(x,y,z)==PositionOf(x,y,z) (==(u,v))
#endif
            }
        }
        public ExplicitPSurface(Polynom[] p)
        {
            if (p.Length == 3)
            {
                px = p[0];
                py = p[1];
                pz = p[2];
                if (px.Dimension != 2) throw new ApplicationException("invalid parameter");
                if (py.Dimension != 2) throw new ApplicationException("invalid parameter");
                if (pz.Dimension != 2) throw new ApplicationException("invalid parameter");
            }
            else if (p.Length == 4)
            {
                px = p[0];
                py = p[1];
                pz = p[2];
                pw = p[3];
                if (px.Dimension != 2) throw new ApplicationException("invalid parameter");
                if (py.Dimension != 2) throw new ApplicationException("invalid parameter");
                if (pz.Dimension != 2) throw new ApplicationException("invalid parameter");
                if (pw.Dimension != 2) throw new ApplicationException("invalid parameter");
            }
            else throw new ApplicationException("invalid parameter");
        }
        public GeoPoint PointAt(GeoPoint2D uv)
        {
            double x = px.Eval(uv.x, uv.y);
            double y = py.Eval(uv.x, uv.y);
            double z = pz.Eval(uv.x, uv.y);
            if (pw != null)
            {
                double w = pw.Eval(uv.x, uv.y);
                return new GeoPoint(x / w, y / w, z / w);
            }
            else
            {
                return new GeoPoint(x, y, z);
            }
        }
        public GeoVector UDirection(GeoPoint2D uv)
        {
            if (pxu == null) pxu = px.Derivate(1, 0);
            if (pyu == null) pyu = py.Derivate(1, 0);
            if (pzu == null) pzu = pz.Derivate(1, 0);
            if (pw != null && pwu == null) pwu = pw.Derivate(1, 0);
            double xd = pxu.Eval(uv.x, uv.y);
            double yd = pyu.Eval(uv.x, uv.y);
            double zd = pzu.Eval(uv.x, uv.y);
            if (pw != null)
            {
                // Quotientenregel
                double wd = pwu.Eval(uv.x, uv.y);
                double x = px.Eval(uv.x, uv.y);
                double y = py.Eval(uv.x, uv.y);
                double z = pz.Eval(uv.x, uv.y);
                double w = pw.Eval(uv.x, uv.y);
                double w2 = w * w;
                return new GeoVector((xd * w - x * wd) / w2, (yd * w - y * wd) / w2, (zd * w - z * wd) / w2);
            }
            else
            {
                return new GeoVector(xd, yd, zd);
            }
        }
        public GeoVector VDirection(GeoPoint2D uv)
        {
            if (pxv == null) pxv = px.Derivate(0, 1);
            if (pyv == null) pyv = py.Derivate(0, 1);
            if (pzv == null) pzv = pz.Derivate(0, 1);
            if (pw != null && pwv == null) pwv = pw.Derivate(0, 1);
            double xd = pxv.Eval(uv.x, uv.y);
            double yd = pyv.Eval(uv.x, uv.y);
            double zd = pzv.Eval(uv.x, uv.y);
            if (pw != null)
            {
                // Quotientenregel
                double wd = pwv.Eval(uv.x, uv.y);
                double x = px.Eval(uv.x, uv.y);
                double y = py.Eval(uv.x, uv.y);
                double z = pz.Eval(uv.x, uv.y);
                double w = pw.Eval(uv.x, uv.y);
                double w2 = w * w;
                return new GeoVector((xd * w - x * wd) / w2, (yd * w - y * wd) / w2, (zd * w - z * wd) / w2);
            }
            else
            {
                return new GeoVector(xd, yd, zd);
            }
        }
        public GeoPoint2D[] MinMax(BoundingRect uvbounds, GeoVector dir)
        {
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            // bestimmte Linien im u,v System als Polynom in einer Variablen betrachten. 
            // Tangente diese Linie soll senkrecht zu dir stehen. Dann neue Richtung für u,v Linie bestimmen
            calcDerivations();
            GeoPoint2D c = uvbounds.GetCenter();
            Polynom fux = pxu.Substitute(new Polynom(1, "x"), new Polynom(c.y, 1));
            Polynom fuy = pyu.Substitute(new Polynom(1, "x"), new Polynom(c.y, 1));
            Polynom fuz = pzu.Substitute(new Polynom(1, "x"), new Polynom(c.y, 1));
            Polynom scpu = dir.x * fux + dir.y * fuy + dir.z * fuz; // Skalarprodukt der (Ableitung von fixedu) mit (dir) als Polynom
                                                                    // gesucht sind dessen Nullstellen. Dort steht dir senkrecht auf der Kurve. Jetzt weiter in Richtung von v suchen
            double[] maxu = scpu.Roots();
            for (int i = 0; i < maxu.Length; i++)
            {
                if (maxu[i] >= uvbounds.Left && maxu[i] <= uvbounds.Right)
                {
                    c.x = maxu[i];
                    Polynom fvx = pxv.Substitute(new Polynom(c.x, 1), new Polynom(1, "x"));
                    Polynom fvy = pyv.Substitute(new Polynom(c.x, 1), new Polynom(1, "x"));
                    Polynom fvz = pzv.Substitute(new Polynom(c.x, 1), new Polynom(1, "x"));
                    Polynom scpv = dir.x * fvx + dir.y * fvy + dir.z * fvz; // Skalarprodukt der (Ableitung von fixedv) mit (dir) als Polynom
                    double[] maxv = scpv.Roots();
                    for (int j = 0; j < maxv.Length; j++)
                    {
                        if (maxv[j] >= uvbounds.Bottom && maxv[j] <= uvbounds.Top) res.Add(new GeoPoint2D(maxu[i], maxv[j]));
                    }
                }
            }
            return res.ToArray();
        }

        private void calcDerivations()
        {
            if (pxu == null) pxu = px.Derivate(1, 0);
            if (pyu == null) pyu = py.Derivate(1, 0);
            if (pzu == null) pzu = pz.Derivate(1, 0);
            if (pw != null && pwu == null) pwu = pw.Derivate(1, 0);
            if (pxv == null) pxv = px.Derivate(0, 1);
            if (pyv == null) pyv = py.Derivate(0, 1);
            if (pzv == null) pzv = pz.Derivate(0, 1);
            if (pw != null && pwv == null) pwv = pw.Derivate(0, 1);
        }
    }
    /* IDEE: OffsetExplicitPSurface:
    Diese wird dargestellt durch ein Polynom plus die Wurzel aus einem Polynom durch ein Polynom.
    Auch die Ableitungen scheinen diese Form zu haben. Möglicherweise auch:
    (Polynom plus die Wurzel aus einem Polynom) durch (Polynom plus die Wurzel aus einem Polynom)
    Damit hätte man einen exakten Offset für alle Flächen.
    Diese Formel lässt auch Nullstellenbestimmungen zu: 
    P(u)+sqrt(Q(u)) == 0
    P²(u) == Q(u)
    P²(u)-Q(u) == 0 
    */
    //internal class PolyRootSurface : ISurfaceImpl
    //{
    //    double[] uKnots, vKnots;
    //    Polynom[,] px, py, pz, pw, qx, qy, qz;
    //    // pw, qx, qy, qz können null sein
    //    // hat die Form x = (px + sqrt(qx))/pw, y, z analog
    //    int uIndex(double u)
    //    {
    //        int high = uKnots.Length - 1;
    //        int low = 0;
    //        if (u <= uKnots[low]) return low;
    //        int mid = (low + high) / 2;
    //        while (u < uKnots[mid] || u >= uKnots[mid + 1])
    //        {
    //            if (u < uKnots[mid]) high = mid;
    //            else low = mid;
    //            mid = (low + high) / 2;
    //            if (low == high) return low;
    //        }
    //        return mid;
    //    }
    //    int vIndex(double v)
    //    {
    //        int high = vKnots.Length - 1;
    //        int low = 0;
    //        if (v <= vKnots[low]) return low;
    //        int mid = (low + high) / 2;
    //        while (v < vKnots[mid] || v >= vKnots[mid + 1])
    //        {
    //            if (v < vKnots[mid]) high = mid;
    //            else low = mid;
    //            mid = (low + high) / 2;
    //            if (low == high) return low;
    //        }
    //        return mid;
    //    }
    //    public override GeoPoint PointAt(GeoPoint2D uv)
    //    {
    //        int i = uIndex(uv.x);
    //        int j = vIndex(uv.y);
    //        double x = px[i, j].Eval(uv.x, uv.y);
    //        double y = py[i, j].Eval(uv.x, uv.y);
    //        double z = pz[i, j].Eval(uv.x, uv.y);
    //        if (qx != null)
    //        {
    //            x += Math.Sqrt(qx[i, j].Eval(uv.x, uv.y));
    //            y += Math.Sqrt(qy[i, j].Eval(uv.x, uv.y));
    //            z += Math.Sqrt(qz[i, j].Eval(uv.x, uv.y));
    //        }
    //        if (pw != null)
    //        {
    //            double w = pw[i, j].Eval(uv.x, uv.y);
    //            x /= w;
    //            y /= w;
    //            z /= w;
    //        }
    //        return new GeoPoint(x, y, z);
    //    }
    //    public override GeoVector UDirection(GeoPoint2D uv)
    //    {
    //        int i = uIndex(uv.x);
    //        int j = vIndex(uv.y);
    //        Polynom ppx = px[i, j].Derivate(1, 0);
    //        Polynom ppy = py[i, j].Derivate(1, 0);
    //        Polynom ppz = pz[i, j].Derivate(1, 0);
    //        if (qx != null)
    //        {

    //        }
    //        return GeoVector.NullVector;
    //    }
    //    public override GeoVector VDirection(GeoPoint2D uv)
    //    {
    //        int i = uIndex(uv.x);
    //        int j = vIndex(uv.y);
    //        Polynom ppx = px[i, j].Derivate(1, 0);
    //        Polynom ppy = py[i, j].Derivate(1, 0);
    //        Polynom ppz = pz[i, j].Derivate(1, 0);
    //        if (qx != null)
    //        {

    //        }
    //        return GeoVector.NullVector;
    //    }
    //    public override ICurve FixedU(double u, double vmin, double vmax)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    public override ICurve FixedV(double u, double umin, double umax)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    public override ISurface GetModified(ModOp m)
    //    {
    //        throw new NotImplementedException();
    //    }
    //    public override IPropertyEntry GetPropertyEntry(IFrame frame)
    //    {
    //        return null;
    //    }
    //}

    public interface IImplicitPSurface
    {
        ImplicitPSurface GetImplicitPSurface();
    }
    public interface IExplicitPCurve3D
    {
        ExplicitPCurve3D GetExplicitPCurve3D();
    }
    public interface IExplicitPCurve2D
    {
        ExplicitPCurve2D GetExplicitPCurve2D();
    }
#if DEBUG
    public class PolynomTester
    {
        static public void Debug(Face face, ICurve curve)
        {
            if (face.Surface is NurbsSurface)
            {
                NurbsSurface ns = face.Surface as NurbsSurface;
                double umin, umax, vmin, vmax;
                ns.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                double[] intu, intv;
                ns.GetSafeParameterSteps(umin, umax, vmin, vmax, out intu, out intv);
                ImplicitPSurface[,] imps = new ImplicitPSurface[intu.Length - 1, intv.Length - 1];
                for (int i = 0; i < intu.Length - 1; i++)
                {
                    for (int j = 0; j < intv.Length - 1; j++)
                    {
                        imps[i, j] = new ImplicitPSurface(ns, new BoundingRect(intu[i], intv[j], intu[i + 1], intv[j + 1]), 6);
                    }
                }
            }
            if (curve is Ellipse)
            {
                Ellipse elli = curve as Ellipse;
                ModOp m = ModOp.Translate(elli.Center.x, elli.Center.y, elli.Center.z) * ModOp.Fit(new GeoVector[] { GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis }, new GeoVector[] { elli.MajorAxis, elli.MinorAxis, elli.MajorAxis ^ elli.MinorAxis });
                ExplicitPCurve3D e3d = ExplicitPCurve3D.MakeCircle(m);
                GeoPoint[] pnts = new GeoPoint[100];
                for (int i = 0; i < 100; i++)
                {
                    pnts[i] = e3d.PointAt(4.0 * i / 100);
                }
                Polyline dbg = Polyline.Construct();
                dbg.SetPoints(pnts, false);
            }
        }
        static public void Debugx()
        {
            Polynom mp = new Polynom(3, 3);
            mp.Set(2, 3, 0, 0); // 2*x³
            mp.Set(3, 0, 3, 0); // 3*y³
            mp.Set(4, 0, 0, 3); // 4*z³
            mp.Set(5, 2, 1, 0); // 5*x²*y
            Polynom mq = new Polynom(3, 3);
            mq.Set(2, 0, 0, 1); // 2*z
            Polynom mpq = mp * mq;
            System.Diagnostics.Trace.WriteLine("(" + mp.ToString() + ") * (" + mq.ToString() + ") = (" + mpq.ToString() + ")");
            Polynom mx = new Polynom(1, 1);
            mx.Set(17, 0);
            mx.Set(23, 1);
            Polynom my = new Polynom(1, 1);
            my.Set(17, 0);
            my.Set(23, 1);
            Polynom mz = new Polynom(1, 1);
            mz.Set(17, 0);
            mz.Set(23, 1);
            Polynom ms = mp.Substitute(new Polynom[] { mx, my, mz });

            Polynom circle = new Polynom(2, 2);
            circle.Set(-1, 0, 0); // Konstante, -radius
            circle.Set(1, 2, 0); // x²
            circle.Set(1, 0, 2); // y²
                                 // x²+y²-1 (==0), Einheitskreis
                                 // Linie von (0,0.5) mit richtung (1,1)
            Polynom lx = new Polynom(1, 1);
            lx.Set(0, 0);
            lx.Set(1, 1); // x = 0+u*1
            Polynom ly = new Polynom(1, 1);
            ly.Set(0.5, 0);
            ly.Set(1, 1); // y = 0.5+u*1
            Polynom intersect = circle.Substitute(new Polynom[] { lx, ly });
            double[] roots = intersect.Roots();
            for (int i = 0; i < roots.Length; i++)
            {
                double tst = intersect.Eval(roots[i]);
                tst = circle.Eval(lx.Eval(roots[i]), ly.Eval(roots[i]));
            }

        }
        public static GeoPoint[] DebugIntersect(SphericalSurface ss, GeoPoint sp, GeoVector dir)
        {
            // warum invers? hier steht es z.B., dass es so sein muss: http://www.cip.ifi.lmu.de/~langeh/test/Sultanow%20-%20Implizite%20Flaechen.pdf

            //ImplicitPSurface pln = new ImplicitPSurface(new Polynom(-0.5, "", 0, "x", 1, "y", 0, "z"));
            //ExplicitPCurve3D circ = ExplicitPCurve3D.MakeCircle(ModOp.Translate(1, 0, 0) * ModOp.Rotate(GeoVector.XAxis, SweepAngle.Deg(30)) * ModOp.Scale(2));
            //double[] u;
            //GeoPoint[] ips = pln.Intersect(circ, out u);
            //ImplicitPSurface imp = new ImplicitPSurface(ss, new BoundingRect(0, -1, 3, 1), 2);
            //Polynom sphere = (new Polynom(1, "x2", 1, "y2", 1, "z2", -1, "")).Modified(ss.ToSphere.GetInverse());
            //Polynom lx = new Polynom(dir.x, "u", sp.x, "");
            //Polynom ly = new Polynom(dir.y, "u", sp.y, "");
            //Polynom lz = new Polynom(dir.z, "u", sp.z, "");
            //Polynom intersect = sphere.Substitute(lx, ly, lz);
            //double[] roots = intersect.Roots();
            //List<GeoPoint> res = new List<GeoPoint>();
            //for (int i = 0; i < roots.Length; i++)
            //{
            //    res.Add(sp + roots[i] * dir);
            //}
            //return res.ToArray();
            return null;
        }

        public static IGeoObject TestCylCylInt(CylindricalSurface cyl1, CylindricalSurface cyl2)
        {
            double a1, a2;
            Geometry.DistLL(cyl1.Location, cyl1.Axis, cyl2.Location, cyl2.Axis, out a1, out a2);
            GeoPoint pa1 = cyl1.Location + a1 * cyl1.Axis;
            GeoPoint pa2 = cyl2.Location + a2 * cyl2.Axis;
            Plane pln1 = new Plane(pa1, cyl1.Axis, pa2 - pa1);
            Plane pln2 = new Plane(pa2, cyl2.Axis, pa1 - pa2);
            GeoPoint ip = pa1;
            GeoPoint sp = pa1, ep = pa2;
            GeoPoint[] ips;
            GeoPoint2D[] uv1, uv2;
            GeoPoint[] fourPoints = new GeoPoint[4];
            Surfaces.PlaneIntersection(pln1, cyl1, cyl2, out ips, out uv1, out uv2);
            if (ips.Length == 2)
            {
                fourPoints[0] = ips[0];
                fourPoints[2] = ips[1];
            }
            Surfaces.PlaneIntersection(pln2, cyl1, cyl2, out ips, out uv1, out uv2);
            if (ips.Length == 2)
            {
                fourPoints[1] = ips[0];
                fourPoints[3] = ips[1];
            }
            for (int i = 0; i < fourPoints.Length; i++)
            {
                TestCylCyl(fourPoints[i], fourPoints[(i + 1) % 4], cyl1, cyl2);
            }
            Polyline res = Polyline.Construct();
            res.SetPoints(fourPoints, false);
            return res;
        }

        public static void TestNurbs(NurbsSurface ns)
        {
            double umin, umax, vmin, vmax;
            ns.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            double[] intu, intv;
            ns.GetSafeParameterSteps(umin, umax, vmin, vmax, out intu, out intv);
            //for (int i = 0; i < intu.Length - 1; i++)
            //{
            //    for (int j = 0; j < intv.Length - 1; j++)
            //    {
            //        Polynom[] rps = null;
            //        if (ns.GetNubs() != null) rps = ns.GetNubs().SurfacePointPolynom((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
            //        if (ns.GetNurbs() != null) rps = ns.GetNurbs().SurfacePointPolynom((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
            //        double x = rps[0].Eval((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
            //        double y = rps[1].Eval((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
            //        double z = rps[2].Eval((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
            //        GeoPoint p = ns.PointAt(new GeoPoint2D((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2));
            //        BoundingRect patch = new CADability.BoundingRect(intu[i], intv[j], intu[i + 1], intv[j + 1]);
            //        ExplicitPSurface eps = new CADability.ExplicitPSurface(ns, patch, ns.UDegree, ns.VDegree, ns.isRational);
            //    }
            //}
            for (int k = -2; k < 9; k++)
            {
                double err = 0.0;
                for (int i = 0; i < intu.Length - 1; i++)
                {
                    for (int j = 0; j < intv.Length - 1; j++)
                    {
                        BoundingRect patch = new BoundingRect(intu[i], intv[j], intu[i + 1], intv[j + 1]);
                        ImplicitPSurface ips = new ImplicitPSurface(ns, patch, ns.UDegree + ns.VDegree + k);
                        err += ips.error;
                    }
                }
                System.Diagnostics.Trace.WriteLine("NurbsSurface: implicitdegree: " + (ns.UDegree + ns.VDegree + k).ToString() + ", error: " + err.ToString());
            }
        }
        public static void TestBoxedNurbs(NurbsSurface ns)
        {
            double umin, umax, vmin, vmax;
            ns.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            double[] intu, intv;
            ns.GetSafeParameterSteps(umin, umax, vmin, vmax, out intu, out intv);
            for (int i = 0; i < intu.Length - 1; i++)
            {
                for (int j = 0; j < intv.Length - 1; j++)
                {
                    Polynom[] rps = null;
                    if (ns.GetNubs() != null) rps = ns.GetNubs().SurfacePointPolynom((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
                    if (ns.GetNurbs() != null) rps = ns.GetNurbs().SurfacePointPolynom((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
                    double x = rps[0].Eval((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
                    double y = rps[1].Eval((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
                    double z = rps[2].Eval((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2);
                    GeoPoint p = ns.PointAt(new GeoPoint2D((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2));
                    double d = p | new GeoPoint(x, y, z);
                    BoundingRect patch = new BoundingRect(intu[i], intv[j], intu[i + 1], intv[j + 1]);
                    ExplicitPSurface eps = new ExplicitPSurface(rps);
                    GeoPoint pm = eps.PointAt(new GeoPoint2D((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2));
                    GeoVector diru = eps.UDirection(new GeoPoint2D((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2));
                    GeoVector dirv = eps.VDirection(new GeoPoint2D((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2));
                    GeoVector orgu = ns.UDirection(new GeoPoint2D((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2));
                    GeoVector orgv = ns.VDirection(new GeoPoint2D((intu[i] + intu[i + 1]) / 2, (intv[j] + intv[j + 1]) / 2));
                    Face dbgf = Face.MakeFace(ns, new SimpleShape(Border.MakeRectangle(patch)));
                    double maxdist;
                    bool islinear;
                    Plane pln = Plane.FromPoints(new GeoPoint[] { eps.PointAt(new GeoPoint2D(intu[i], intv[j])), eps.PointAt(new GeoPoint2D(intu[i + 1], intv[j])), eps.PointAt(new GeoPoint2D(intu[i], intv[j + 1])), eps.PointAt(new GeoPoint2D(intu[i + 1], intv[j + 1])) }, out maxdist, out islinear);
                    GeoPoint[] corners = new GeoPoint[] { eps.PointAt(new GeoPoint2D(intu[i], intv[j])), eps.PointAt(new GeoPoint2D(intu[i + 1], intv[j])), eps.PointAt(new GeoPoint2D(intu[i], intv[j + 1])), eps.PointAt(new GeoPoint2D(intu[i + 1], intv[j + 1])) };
                    GeoPoint2D[] pnts = eps.MinMax(patch, pln.Normal);
                    pm = eps.PointAt(pnts[0]);
                    PlaneSurface pls = new PlaneSurface(pln);
                    BoundingRect plminmax = BoundingRect.EmptyBoundingRect;
                    for (int k = 0; k < corners.Length; k++)
                    {
                        plminmax.MinMax(pls.PositionOf(corners[k]));
                    }
                    Face dbgp = Face.MakeFace(pls, new SimpleShape(Border.MakeRectangle(plminmax)));
                    Line dbgl = Line.TwoPoints(pm, pln.ToGlobal(pln.Project(pm)));
                    DebuggerContainer dc0 = new DebuggerContainer();
                    dc0.Add(dbgl);
                    dc0.Add(dbgf);
                    dc0.Add(dbgp);
                }
            }
        }
        private static void TestCylCyl2(GeoPoint p1, GeoPoint p2, CylindricalSurface cyl1, CylindricalSurface cyl2)
        {
            double par1, par2;
            Geometry.DistLL(cyl1.Location, cyl1.Axis, cyl2.Location, cyl2.Axis, out par1, out par2);
            GeoPoint c1 = cyl1.Location + par1 * cyl1.Axis;
            GeoPoint c2 = cyl2.Location + par2 * cyl2.Axis;
            Plane bp = new Plane(c1, c2 - c1); // eine Ebene, parallel zu beiden Zylinderachsen

            ModOp fromPlane = new ModOp(cyl1.Axis, cyl2.Axis, c2 - c1, c1);
            ModOp toPlane = fromPlane.GetInverse();

            GeoPoint[] pnts = new GeoPoint[9];
            for (int i = 0; i < 9; i++)
            {
                Plane pln = new Plane(fromPlane * new GeoPoint(5 * i - 20, 0, 0), fromPlane * GeoVector.YAxis, fromPlane * GeoVector.ZAxis);
                //Plane pln = new Plane(c1, fromPlane * new GeoVector(4 * i - 16, 16, 0), fromPlane * GeoVector.ZAxis);
                GeoPoint[] ips;
                GeoPoint2D[] uv1, uv2;
                Surfaces.PlaneIntersection(pln, cyl1, cyl2, out ips, out uv1, out uv2);
                for (int j = 0; j < ips.Length; j++)
                {
                    ips[j] = toPlane * ips[j];
                    if (ips[j].y > 0) pnts[i] = ips[j];
                }
            }
            Polyline dbgip = Polyline.Construct();
            dbgip.SetPoints(pnts, false);
            Matrix m = new DenseMatrix(10, 10);
            double[] u = new double[] { 0, 1 / 8.0, 2 / 8.0, 3 / 8.0, 4 / 8.0, 5 / 8.0, 6 / 8.0, 7 / 8.0, 1 };
            for (int i = 0; i < 9; i++)
            {
                //u[i] = -Math.Atan2(1, 2 * u[i] - 1) / Math.PI*2 + 1.5;
                //u[i] = Math.Asin(u[i] * 2 - 1)/Math.PI+0.5; //  (-Math.PI / 2.0 + u[i] * Math.PI) / 2.0 + 0.5;
            }
            for (int i = 0; i < 9; i++)
            {
                m[i, 0] = u[i] * u[i] * u[i] * u[i];
                m[i, 1] = u[i] * u[i] * u[i];
                m[i, 2] = u[i] * u[i];
                m[i, 3] = u[i];
                m[i, 4] = 1.0;
                m[i, 5] = -u[i] * u[i] * u[i] * u[i] * pnts[i].x;
                m[i, 6] = -u[i] * u[i] * u[i] * pnts[i].x;
                m[i, 7] = -u[i] * u[i] * pnts[i].x;
                m[i, 8] = -u[i] * pnts[i].x;
                m[i, 9] = -pnts[i].x;
            }
            m[9, 5] = 1;
            m[9, 6] = 1;
            Vector b = new DenseVector(10);
            b[9] = 1; // alle anderen 0
            Vector x = (Vector)m.Solve(b);
            for (int i = 0; i < 9; i++)
            {
                m[i, 0] = u[i] * u[i] * u[i] * u[i];
                m[i, 1] = u[i] * u[i] * u[i];
                m[i, 2] = u[i] * u[i];
                m[i, 3] = u[i];
                m[i, 4] = 1.0;
                m[i, 5] = -u[i] * u[i] * u[i] * u[i] * pnts[i].y;
                m[i, 6] = -u[i] * u[i] * u[i] * pnts[i].y;
                m[i, 7] = -u[i] * u[i] * pnts[i].y;
                m[i, 8] = -u[i] * pnts[i].y;
                m[i, 9] = -pnts[i].y;
            }
            m[9, 5] = 1;
            m[9, 6] = 1;
            Vector y = (Vector)m.Solve(b);
            for (int i = 0; i < 9; i++)
            {
                m[i, 0] = u[i] * u[i] * u[i] * u[i];
                m[i, 1] = u[i] * u[i] * u[i];
                m[i, 2] = u[i] * u[i];
                m[i, 3] = u[i];
                m[i, 4] = 1.0;
                m[i, 5] = -u[i] * u[i] * u[i] * u[i] * pnts[i].z;
                m[i, 6] = -u[i] * u[i] * u[i] * pnts[i].z;
                m[i, 7] = -u[i] * u[i] * pnts[i].z;
                m[i, 8] = -u[i] * pnts[i].z;
                m[i, 9] = -pnts[i].z;
            }
            m[9, 5] = 1;
            m[9, 6] = 1;
            Vector z = (Vector)m.Solve(b);
            if (y.IsValid() && z.IsValid()) // x != null && 
            {
                //Polynom px = new Polynom(x[0, 0], "u4", x[1, 0], "u3", x[2, 0], "u2", x[3, 0], "u", x[4, 0], "");
                //Polynom pwx = new Polynom(x[5, 0], "u4", x[6, 0], "u3", x[7, 0], "u2", x[8, 0], "u", x[9, 0], "");
                Polynom py = new Polynom(y[0], "u4", y[1], "u3", y[2], "u2", y[3], "u", y[4], "");
                Polynom pwy = new Polynom(y[5], "u4", y[6], "u3", y[7], "u2", y[8], "u", y[9], "");
                Polynom pz = new Polynom(z[0], "u4", z[1], "u3", z[2], "u2", z[3], "u", z[4], "");
                Polynom pwz = new Polynom(z[5], "u4", z[6], "u3", z[7], "u2", z[8], "u", z[9], "");
                double err = 0.0;
                GeoPoint[] dbgp = new GeoPoint[100];
                for (int i = 0; i < 100; i++)
                {
                    //GeoPoint p = new GeoPoint(px.Eval(i / 100.0) / pwx.Eval(i / 100.0), py.Eval(i / 100.0) / pwy.Eval(i / 100.0), pz.Eval(i / 100.0) / pwz.Eval(i / 100.0));
                    GeoPoint p = new GeoPoint(i / 100.0 * 40 - 20, py.Eval(i / 100.0) / pwy.Eval(i / 100.0), pz.Eval(i / 100.0) / pwz.Eval(i / 100.0));
                    dbgp[i] = fromPlane * p;
                    double d1 = Math.Abs(Geometry.DistPL(dbgp[i], cyl1.Location, cyl1.Axis) - cyl1.RadiusX);
                    double d2 = Math.Abs(Geometry.DistPL(dbgp[i], cyl2.Location, cyl2.Axis) - cyl2.RadiusX);
                    //err += d1 + d2;
                    Plane pln = new Plane(fromPlane * new GeoPoint(p.x, 0, 0), fromPlane * GeoVector.YAxis, fromPlane * GeoVector.ZAxis);
                    //Plane pln = new Plane(c1, fromPlane * new GeoVector(4 * i - 16, 16, 0), fromPlane * GeoVector.ZAxis);
                    GeoPoint[] ips;
                    GeoPoint2D[] uv1, uv2;
                    Surfaces.PlaneIntersection(pln, cyl1, cyl2, out ips, out uv1, out uv2);
                    for (int j = 0; j < ips.Length; j++)
                    {
                        ips[j] = toPlane * ips[j];
                        if (ips[j].y > 0)
                        {
                            err += p.To2D() | ips[j].To2D();
                        }
                    }

                }
                dbgip.SetPoints(dbgp, false);
            }
        }



        private static void TestCylCyl(GeoPoint p1, GeoPoint p2, CylindricalSurface cyl1, CylindricalSurface cyl2)
        {
            double par1, par2;
            Geometry.DistLL(cyl1.Location, cyl1.Axis, cyl2.Location, cyl2.Axis, out par1, out par2);
            GeoPoint c1 = cyl1.Location + par1 * cyl1.Axis;
            GeoPoint c2 = cyl2.Location + par2 * cyl2.Axis;
            Plane bp = new Plane(c1, c2 - c1); // eine Ebene, parallel zu beiden Zylinderachsen

            ModOp fromPlane = new ModOp(cyl1.Axis, cyl2.Axis, c2 - c1, c1);
            ModOp toPlane = fromPlane.GetInverse();
            int degree = 4;
            int num = ImplicitPCurve2D.PointsNeeded(degree, false);
            num *= 2;
            GeoPoint2D[] pnts2d = new GeoPoint2D[num];
            for (int i = 0; i < (num + 1) / 2; i++)
            {
                double c = Math.Cos(i * Math.PI / num / 2);
                double s = Math.Sin(i * Math.PI / num / 2);
                Plane pln = new Plane(c1, fromPlane * new GeoVector(c, -s, 0));
                GeoPoint[] ips;
                GeoPoint2D[] uv1, uv2;
                Surfaces.PlaneIntersection(pln, cyl1, cyl2, out ips, out uv1, out uv2);
                if (ips.Length == 2)
                {
                    GeoPoint2D p2d0 = (toPlane * ips[0]).To2D();
                    GeoPoint2D p2d1 = (toPlane * ips[1]).To2D();
                    //p2d0 = uv2[0];
                    //p2d1 = uv2[1];
                    pnts2d[i] = p2d0;
                    pnts2d[i + num / 2] = p2d1;
                }
            }
            //double uref = pnts2d[0].x;
            //for (int i = 1; i < pnts2d.Length; i++)
            //{
            //    if (pnts2d[i].x - uref > Math.PI) pnts2d[i].x -= Math.PI * 2;
            //    if (pnts2d[i].x - uref < -Math.PI) pnts2d[i].x += Math.PI * 2;
            //}
            Polyline2D pl2d = new Polyline2D(pnts2d);
            ImplicitPCurve2D ipc2d = new ImplicitPCurve2D(pnts2d, degree, false);
            Polynom diag = ipc2d.Polynom.Substitute(new Polynom(1, "x"), new Polynom(1, "x")); // 
            double[] rts1 = diag.Roots();
            Line2D dbgdiag = new Line2D(new GeoPoint2D(rts1[0], rts1[0]), new GeoPoint2D(rts1[1], rts1[1]));
            pnts2d = new GeoPoint2D[200];
            double err = 0.0;
            for (int i = 0; i < 100; i++)
            {
                double c = Math.Cos(i * Math.PI / 100);
                double s = Math.Sin(i * Math.PI / 100);
                Plane pln = new Plane(c1, fromPlane * new GeoVector(c, -s, 0));
                GeoPoint[] ips;
                GeoPoint2D[] uv1, uv2;
                Surfaces.PlaneIntersection(pln, cyl1, cyl2, out ips, out uv1, out uv2);
                if (ips.Length == 2)
                {
                    GeoPoint2D p2d0 = (toPlane * ips[0]).To2D();
                    GeoPoint2D p2d1 = (toPlane * ips[1]).To2D();
                    //p2d0 = cyl2.PositionOf(ips[0]);
                    //p2d1 = cyl2.PositionOf(ips[1]);
                    //if (p2d0.x - uref > Math.PI) p2d0.x -= Math.PI * 2;
                    //if (p2d0.x - uref < -Math.PI) p2d0.x += Math.PI * 2;
                    //if (p2d1.x - uref > Math.PI) p2d1.x -= Math.PI * 2;
                    //if (p2d1.x - uref < -Math.PI) p2d1.x += Math.PI * 2;
                    err += Math.Abs(ipc2d.Polynom.Eval(p2d0.x, p2d0.y));
                    err += Math.Abs(ipc2d.Polynom.Eval(p2d1.x, p2d1.y));
                    if (i == 0 || ((p2d0 | pnts2d[i - 1]) + (p2d1 | pnts2d[i + 100 - 1])) < ((p2d1 | pnts2d[i - 1]) + (p2d0 | pnts2d[i + 100 - 1])))
                    {
                        pnts2d[i] = p2d0;
                        pnts2d[i + 100] = p2d1;
                    }
                    else
                    {
                        pnts2d[i] = p2d1;
                        pnts2d[i + 100] = p2d0;
                    }
                }
            }
            pl2d = new Curve2D.Polyline2D(pnts2d);
        }
        private static void TestCylCylx(GeoPoint p1, GeoPoint p2, CylindricalSurface cyl1, CylindricalSurface cyl2)
        {
            // Px(u)/Pw(u) = x (homogenes Polynom muss durch x gehen
            // das sind 3 Polynomgleichungen, die Punkte auf der Kurve erfüllen müssen
            // wenn wir die quadratisch annehmen: a*u²+b*u+c=d*u²*x + e*u*x + f*x
            // 6 Unbekannte, wir brauchen 5 Punkte und z.B. d+e==1, da sonst 0 für alle a bis f eine Lösung ist
            // umgeformt:
            // a*u² + b*u + c - d*u²*x - e*u*x - f*x = 0 für 5 Paare (x,u)
            //                  d      + e           = 1
            //for (double pu = 0.1; pu < 0.4; pu += 0.02)
            double u1min = 0.05, u1max = 0.09;
            double u2min = 0.12, u2max = 0.20;
            double u3min = 0.22, u3max = 0.38;
            for (int cnt = 0; cnt < 100; cnt++)
            {
                ExplicitPCurve3D circle = ExplicitPCurve3D.MakeCircle(ModOp.Identity);
                Random rnd = new Random();
                double minError = double.MaxValue;
                double par1, par2;
                Geometry.DistLL(cyl1.Location, cyl1.Axis, cyl2.Location, cyl2.Axis, out par1, out par2);
                GeoPoint c1 = cyl1.Location + par1 * cyl1.Axis;
                GeoPoint c2 = cyl2.Location + par2 * cyl2.Axis;
                Plane bp = new Plane(c1, c2 - c1); // eine Ebene, parallel zu beiden Zylinderachsen
                GeoPoint pc1 = bp.ToGlobal(bp.Project(p1));
                GeoPoint pc2 = bp.ToGlobal(bp.Project(p2));

                //double[] u = new double[] { 0, 1.0 / 8, 2.0 / 8, 3.0 / 8, 4.0 / 8, 5.0 / 8, 6.0 / 8, 7.0 / 8, 1 };
                double[,,] errn = new double[4, 4, 4];
                int n1m = -1, n2m = -1, n3m = -1;
                for (int n1 = 0; n1 < 4; n1++)
                    for (int n2 = 0; n2 < 4; n2++)
                        for (int n3 = 0; n3 < 4; n3++)
                        {
                            double[] u = new double[] { 0, 0, 0, 0, 0.5, 1, 1, 1, 1 };
                            //u[1] = 0.07 -0.02  + rnd.NextDouble() * 0.04;
                            //u[2] = 0.16 - 0.04 + rnd.NextDouble() * 0.08;
                            //u[3] = 0.3- 0.08 + rnd.NextDouble() * 0.16;
                            u[1] = u1min + (u1max - u1min) * n1 / 3.0;
                            u[2] = u2min + (u2max - u2min) * n2 / 3.0;
                            u[3] = u3min + (u3max - u3min) * n3 / 3.0;
                            Array.Sort(u);
                            u[5] = 1 - u[3];
                            u[6] = 1 - u[2];
                            u[7] = 1 - u[1];
                            Matrix m = new DenseMatrix(10, 10);
                            GeoPoint[] pnts = new GeoPoint[9];
                            GeoVector dir = p2 - p1;
                            for (int i = 0; i < 9; i++)
                            {
                                // Plane pln = new Plane(p1 + i / 8.0 * dir, dir);
                                double alpha = i / 8.0 * Math.PI / 2.0;
                                double fx = Math.Cos(alpha);
                                double fy = Math.Sin(alpha);
                                Plane pln = new Plane(c1, c2 - c1, fx * (pc1 - c1).Normalized + fy * (pc2 - c1).Normalized); // Ebene durch kürzeste Verbindeung der Zylinderachsen und zwischen p1 und p2 gedreht
                                GeoPoint[] ips;
                                GeoPoint2D[] uv1, uv2;
                                Surfaces.PlaneIntersection(pln, cyl1, cyl2, out ips, out uv1, out uv2);
                                double md = double.MaxValue;
                                for (int j = 0; j < ips.Length; j++)
                                {
                                    double d = Geometry.DistPL(ips[j], p1, p2);
                                    if (d < md)
                                    {
                                        md = d;
                                        pnts[i] = ips[j];
                                    }
                                }
                            }
                            Polyline dbgip = Polyline.Construct();
                            dbgip.SetPoints(pnts, false);
                            for (int i = 0; i < 9; i++)
                            {
                                m[i, 0] = u[i] * u[i] * u[i] * u[i];
                                m[i, 1] = u[i] * u[i] * u[i];
                                m[i, 2] = u[i] * u[i];
                                m[i, 3] = u[i];
                                m[i, 4] = 1.0;
                                m[i, 5] = -u[i] * u[i] * u[i] * u[i] * pnts[i].x;
                                m[i, 6] = -u[i] * u[i] * u[i] * pnts[i].x;
                                m[i, 7] = -u[i] * u[i] * pnts[i].x;
                                m[i, 8] = -u[i] * pnts[i].x;
                                m[i, 9] = -pnts[i].x;
                            }
                            m[9, 5] = 1;
                            m[9, 6] = 1;
                            Vector b = new DenseVector(10);
                            b[9] = 1; // alle anderen 0
                            Vector x = (Vector)m.Solve(b);
                            for (int i = 0; i < 9; i++)
                            {
                                m[i, 0] = u[i] * u[i] * u[i] * u[i];
                                m[i, 1] = u[i] * u[i] * u[i];
                                m[i, 2] = u[i] * u[i];
                                m[i, 3] = u[i];
                                m[i, 4] = 1.0;
                                m[i, 5] = -u[i] * u[i] * u[i] * u[i] * pnts[i].y;
                                m[i, 6] = -u[i] * u[i] * u[i] * pnts[i].y;
                                m[i, 7] = -u[i] * u[i] * pnts[i].y;
                                m[i, 8] = -u[i] * pnts[i].y;
                                m[i, 9] = -pnts[i].y;
                            }
                            m[9, 5] = 1;
                            m[9, 6] = 1;
                            Vector y = (Vector)m.Solve(b);
                            for (int i = 0; i < 9; i++)
                            {
                                m[i, 0] = u[i] * u[i] * u[i] * u[i];
                                m[i, 1] = u[i] * u[i] * u[i];
                                m[i, 2] = u[i] * u[i];
                                m[i, 3] = u[i];
                                m[i, 4] = 1.0;
                                m[i, 5] = -u[i] * u[i] * u[i] * u[i] * pnts[i].z;
                                m[i, 6] = -u[i] * u[i] * u[i] * pnts[i].z;
                                m[i, 7] = -u[i] * u[i] * pnts[i].z;
                                m[i, 8] = -u[i] * pnts[i].z;
                                m[i, 9] = -pnts[i].z;
                            }
                            m[9, 5] = 1;
                            m[9, 6] = 1;
                            Vector z = (Vector)m.Solve(b);
                            if (x.IsValid() && y.IsValid() && z.IsValid())
                            {
                                Polynom px = new Polynom(x[0], "u4", x[1], "u3", x[2], "u2", x[3], "u", x[4], "");
                                Polynom pwx = new Polynom(x[5], "u4", x[6], "u3", x[7], "u2", x[8], "u", x[9], "");
                                Polynom py = new Polynom(y[0], "u4", y[1], "u3", y[2], "u2", y[3], "u", y[4], "");
                                Polynom pwy = new Polynom(y[5], "u4", y[6], "u3", y[7], "u2", y[8], "u", y[9], "");
                                Polynom pz = new Polynom(z[0], "u4", z[1], "u3", z[2], "u2", z[3], "u", z[4], "");
                                Polynom pwz = new Polynom(z[5], "u4", z[6], "u3", z[7], "u2", z[8], "u", z[9], "");
                                double err = 0.0;
                                GeoPoint[] dbgp = new GeoPoint[100];
                                for (int i = 0; i < 100; i++)
                                {
                                    dbgp[i] = new GeoPoint(px.Eval(i / 100.0) / pwx.Eval(i / 100.0), py.Eval(i / 100.0) / pwy.Eval(i / 100.0), pz.Eval(i / 100.0) / pwz.Eval(i / 100.0));
                                    double d1 = Math.Abs(Geometry.DistPL(dbgp[i], cyl1.Location, cyl1.Axis) - cyl1.RadiusX);
                                    double d2 = Math.Abs(Geometry.DistPL(dbgp[i], cyl2.Location, cyl2.Axis) - cyl2.RadiusX);
                                    err += d1 + d2;
                                }
                                errn[n1, n2, n3] = err;
                                Polyline poly = Polyline.Construct();
                                poly.SetPoints(dbgp, false);
                                if (err < minError)
                                {
                                    n1m = n1;
                                    n2m = n2;
                                    n3m = n3;
                                    minError = err;
                                    System.Diagnostics.Trace.WriteLine(string.Format("u1, u2, u3, err: {0}, {1}, {2}, {3}", u[1], u[2], u[3], err));
                                }
                            }
                        }
                if (n1m >= 2) u1min += (u1max - u1min) / 4.0;
                else u1max -= (u1max - u1min) / 4.0;
                if (n2m >= 2) u2min += (u2max - u2min) / 4.0;
                else u2max -= (u2max - u2min) / 4.0;
                if (n3m >= 2) u3min += (u3max - u3min) / 4.0;
                else u3max -= (u3max - u3min) / 4.0;
            }
        }

        public static void TestBSpline(ICurve bsp)
        {
            if (bsp is IExplicitPCurve3D)
            {
                ExplicitPCurve3D ec3d = (bsp as IExplicitPCurve3D).GetExplicitPCurve3D();
                double[] dbg1 = ec3d.GetCurvaturePositions(10);
                double[] dbg2 = ec3d.GetCurvaturePositions(20);
                for (int i = 0; i < dbg1.Length; i++)
                {
                    System.Diagnostics.Trace.WriteLine("radius: " + ec3d.RadiusAt(dbg1[i]));
                }
                for (int i = 0; i < dbg2.Length; i++)
                {
                    System.Diagnostics.Trace.WriteLine("radius: " + ec3d.RadiusAt(dbg2[i]));
                }
            }
        }
    }
#endif
}
