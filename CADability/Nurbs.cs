using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Collections.Generic;
using System.Text;

namespace CADability
{

    #region Ursprüngliche Implementierung ohne generics
    internal abstract class Pole
    {
        abstract public void Add(double factor, Pole toAdd);
        abstract public Pole Create(double factor, Pole plus, Pole minus);
        abstract public void Clear();
        abstract public Pole Clone();
        abstract public void Norm();
        abstract public void Set(Pole toCopyFrom);
        abstract public double Weight { get; }
        virtual public GeoPoint GeoPoint { get { return GeoPoint.Origin; } }
        virtual public GeoPoint2D GeoPoint2D { get { return GeoPoint2D.Origin; } }
        virtual public GeoVector GeoVector { get { return new GeoVector(0, 0, 0); } }
        virtual public GeoVector2D GeoVector2D { get { return new GeoVector2D(0, 0); } }
    }

    internal class Pole3DW : Pole
    {
        public double x, y, z, w;
        public Pole3DW(double x, double y, double z, double w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
        public override Pole Clone()
        {
            return new Pole3DW(x, y, z, w);
        }
        public override void Norm()
        {
            x /= w;
            y /= w;
            z /= w;
            w = 1.0;
        }
        public override double Weight
        {
            get { return w; }
        }
        public override void Add(double factor, Pole toAdd)
        {
            Pole3DW p3d = toAdd as Pole3DW;
            x += factor * p3d.x;
            y += factor * p3d.y;
            z += factor * p3d.z;
            w += factor * p3d.w;
        }
        public override void Clear()
        {
            x = y = z = w = 0.0;
        }
        public override Pole Create(double factor, Pole plus, Pole minus)
        {
            Pole3DW plus3d = plus as Pole3DW;
            Pole3DW minus3d = minus as Pole3DW;
            return new Pole3DW(factor * (plus3d.x - minus3d.x), factor * (plus3d.y - minus3d.y), factor * (plus3d.z - minus3d.z), factor * (plus3d.w - minus3d.w));
        }
        public override void Set(Pole toCopyFrom)
        {
            Pole3DW p3d = toCopyFrom as Pole3DW;
            x = p3d.x;
            y = p3d.y;
            z = p3d.z;
            w = p3d.w;
        }
        public override GeoPoint GeoPoint
        {
            get
            {
                return new GeoPoint(x / w, y / w, z / w);
            }
        }
        public override GeoVector GeoVector
        {
            get
            {
                return new GeoVector(x, y, z);
            }
        }
    }

    internal class Pole3D : Pole
    {
        public double x, y, z;
        public Pole3D(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public override Pole Clone()
        {
            return new Pole3D(x, y, z);
        }
        public override void Norm()
        {
        }
        public override double Weight
        {
            get { return 1.0; }
        }
        public override void Add(double factor, Pole toAdd)
        {
            Pole3D p3d = toAdd as Pole3D;
            x += factor * p3d.x;
            y += factor * p3d.y;
            z += factor * p3d.z;
        }
        public override void Clear()
        {
            x = y = z = 0.0;
        }
        public override Pole Create(double factor, Pole plus, Pole minus)
        {
            Pole3D plus3d = plus as Pole3D;
            Pole3D minus3d = minus as Pole3D;
            return new Pole3D(factor * (plus3d.x - minus3d.x), factor * (plus3d.y - minus3d.y), factor * (plus3d.z - minus3d.z));
        }
        public override void Set(Pole toCopyFrom)
        {
            Pole3D p3d = toCopyFrom as Pole3D;
            x = p3d.x;
            y = p3d.y;
            z = p3d.z;
        }
        public override GeoPoint GeoPoint
        {
            get
            {
                return new GeoPoint(x, y, z);
            }
        }
        public override GeoVector GeoVector
        {
            get
            {
                return new GeoVector(x, y, z);
            }
        }
    }

    internal class Pole2DW : Pole
    {
        public double x, y, w;
        public Pole2DW(double x, double y, double w)
        {
            this.x = x;
            this.y = y;
            this.w = w;
        }
        public override Pole Clone()
        {
            return new Pole2DW(x, y, w);
        }
        public override void Norm()
        {
            x /= w;
            y /= w;
            w = 1.0;
        }
        public override double Weight
        {
            get { return w; }
        }
        public override void Add(double factor, Pole toAdd)
        {
            Pole2DW p2d = toAdd as Pole2DW;
            x += factor * p2d.x;
            y += factor * p2d.y;
            w += factor * p2d.w;
        }
        public override void Clear()
        {
            x = y = w = 0.0;
        }
        public override Pole Create(double factor, Pole plus, Pole minus)
        {
            Pole2DW plus2d = plus as Pole2DW;
            Pole2DW minus2d = minus as Pole2DW;
            return new Pole2DW(factor * (plus2d.x - minus2d.x), factor * (plus2d.y - minus2d.y), factor * (plus2d.w - minus2d.w));
        }
        public override void Set(Pole toCopyFrom)
        {
            Pole2DW p2d = toCopyFrom as Pole2DW;
            x = p2d.x;
            y = p2d.y;
            w = p2d.w;
        }
        public override GeoPoint2D GeoPoint2D
        {
            get
            {
                return new GeoPoint2D(x / w, y / w);
            }
        }
        public override GeoVector2D GeoVector2D
        {
            get
            {
                return new GeoVector2D(x, y); // Vectoren sind nicht homogen, weight kann auch 0 sein!
            }
        }
    }

    internal class Pole2D : Pole
    {
        public double x, y;
        public Pole2D(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
        public override Pole Clone()
        {
            return new Pole2D(x, y);
        }
        public override void Norm()
        {
        }
        public override double Weight
        {
            get { return 1.0; }
        }
        public override void Add(double factor, Pole toAdd)
        {
            Pole2D p2d = toAdd as Pole2D;
            x += factor * p2d.x;
            y += factor * p2d.y;
        }
        public override void Clear()
        {
            x = y = 0.0;
        }
        public override Pole Create(double factor, Pole plus, Pole minus)
        {
            Pole2D plus2d = plus as Pole2D;
            Pole2D minus2d = minus as Pole2D;
            return new Pole2D(factor * (plus2d.x - minus2d.x), factor * (plus2d.y - minus2d.y));
        }
        public override void Set(Pole toCopyFrom)
        {
            Pole2D p2d = toCopyFrom as Pole2D;
            x = p2d.x;
            y = p2d.y;
        }
        public override GeoPoint2D GeoPoint2D
        {
            get
            {
                return new GeoPoint2D(x, y);
            }
        }
        public override GeoVector2D GeoVector2D
        {
            get
            {
                return new GeoVector2D(x, y);
            }
        }
    }

    /// <summary>
    /// Diese Klasse bildet die NURBS Funktionalität aus OpenCascade nach.
    /// Vielleicht wird mal mehr draus...
    /// </summary>
    internal class Nurbs
    {
        /// <summary>
        /// NURBS Buch S. 68,
        /// knot (U) ist der flache Knotenvektor (mit Wiederholungen)
        /// n ist noch nicht ganz klar, aber hat mit der Länge des Knotenvectors zu tun
        /// </summary>
        /// <param name="n"></param>
        /// <param name="degree"></param>
        /// <param name="u"></param>
        /// <param name="knot"></param>
        /// <returns></returns>
        static int FindSpan(int high, int low, double u, double[] knot)
        {
            if (u >= knot[high]) return high - 1; // Sonderfall
            int mid = (low + high) / 2;
            while (u < knot[mid] || u >= knot[mid + 1])
            {
                if (u < knot[mid]) high = mid;
                else low = mid;
                mid = (low + high) / 2;
            }
            return mid;
        }
        /// <summary>
        /// NURBS Buch S. 70.
        /// Hier ein Versuch mit unsafe, wg fixed bzw. stackalloc
        /// Das muss noch genauer ausgemessen werden, was es an Verbesserung bringt...
        /// </summary>
        static unsafe void BasisFuns(int span, double u, int degree, double[] knot, out double[] N)
        {
            N = new double[degree + 1];
            fixed (double* pN = N)
            {
                double* left = stackalloc double[degree + 1]; // left und right sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
                double* right = stackalloc double[degree + 1];
                pN[0] = 1.0;
                for (int j = 1; j <= degree; ++j)
                {
                    left[j] = u - knot[span + 1 - j];
                    right[j] = knot[span + j] - u;
                    double saved = 0.0;
                    for (int r = 0; r < j; ++r)
                    {
                        double temp = pN[r] / (right[r + 1] + left[j - r]);
                        pN[r] = saved + right[r + 1] * temp;
                        saved = left[j - r] * temp;
                    }
                    pN[j] = saved;
                }
            }
        }
        static void AllBasisFuns(int span, double u, int degree, double[] knot, out double[][] AN)
        {
            AN = new double[degree + 1][];
            for (int i = 0; i <= degree; ++i)
            {
                BasisFuns(span, u, i, knot, out AN[i]);
            }
        }
        /// <summary>
        /// NURBS Buch S. 124
        /// </summary>
        static public void CurvePoint(int degree, double[] knots, Pole[] Poles, double u, Pole res)
        {
            int span;
            int n = knots.Length - degree - 1;
            span = FindSpan(n, degree, u, knots);
            double[] N; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFuns(span, u, degree, knots, out N);
            res.Clear();
            for (int j = 0; j <= degree; ++j)
            {
                res.Add(N[j], Poles[span - degree + j]);
            }
        }
        // Folgendes geht leider nicht, da der Generic Parameter nicht mit + oder * verwendet werden kann
        // das würde nur über Interface oder virtuelle methode einer Basisklasse gehen
        // DOCH! http://www.codeproject.com/csharp/genericnumerics.asp
        static public T CurvePoint<T, C>(int degree, double[] knots, T[] Poles, double u)
            where T : new()
            where C : IPoleCalculator<T>, new()
        {
            C calc = new C(); // das kostet angeblich nix!
            int span;
            int n = knots.Length - degree - 1;
            span = FindSpan(n, degree, u, knots);
            double[] N; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFuns(span, u, degree, knots, out N);
            T res = new T();
            for (int j = 0; j <= degree; ++j)
            {
                res = calc.Add(res, calc.Mul(N[j], Poles[span - degree + j]));
                // res = res + N[j] * Poles[span - degree + j];
            }
            return res;
        }

        static public void CurveDerivCpts1(int degree, double[] knots, Pole[] Poles, out Pole[] Ctpts)
        {
            // Buch S. 98
            // nur die Kontrollpunkte der 1. Ableitung berechnen, im Buch:
            // p = degree, r1==0, r2==n==knots.Length-degree-1, 
            int n = Poles.Length - 1;
            Ctpts = new Pole[n];
            int tmp = degree;
            for (int i = 0; i <= n - 1; ++i)
            {
                // Ctpts[i] = degree * (Poles[i + 1] - Poles[i]) / (knots[i + degree + 1] - knots[i + 1]);
                // damit ein Pole vom richtigen Typ erzeugt wird hier Aufruf der Create Methode
                // hier wird knots um eines mehr gebraucht als bei den anderen Mathoden. Bei geschlossenen
                // macht das Probleme.
                int maxind = Math.Min(i + degree + 1, knots.Length - 1);
                Ctpts[i] = Poles[i].Create(degree / (knots[maxind] - knots[i + 1]), Poles[i + 1], Poles[i]);
            }
        }
        static public void CurveDerivCpts(int degree, int d, double[] knots, Pole[] Poles, out Pole[][] Ctpts)
        {
            // Buch S. 98
            Ctpts = new Pole[d + 1][];
            Ctpts[0] = new Pole[Poles.Length];
            for (int i = 0; i < Poles.Length; ++i)
            {
                Ctpts[0][i] = Poles[i].Clone();
            }
            for (int k = 1; k <= d; ++k)
            {
                int n = Poles.Length - k;
                Ctpts[k] = new Pole[n];
                int tmp = degree - k + 1;
                for (int i = 0; i <= n - 1; ++i)
                {
                    Ctpts[k][i] = Poles[i].Create(tmp / (knots[i + degree + 1] - knots[i + k]), Ctpts[k - 1][i + 1], Ctpts[k - 1][i]);
                }
            }
        }
        static public void CurveDerivsAlg1(int degree, double[] knots, Pole[] Poles, Pole[] Deriv1, double u, Pole deriv)
        {
            // implementiert im Buch S. 99 für d==1, also nur 1. Ableitung
            // die unveränderlichen PK1 müssen natürlich zum BSpline objekt, damit sie nicht immer neu berechnet werden müssen
            // für NURBS mit Weight, (also rationale) muss noch mit RatCurveDerivs1 nachgebessert werden
            int n = knots.Length - degree - 1;
            int span = FindSpan(n, degree, u, knots);
            double[] N;
            // AllBasisFuns(span, u, degree, knots, out N); // wir brauchen ja nur die mit "degree-1"
            BasisFuns(span, u, degree - 1, knots, out N);
            // CurveDerivCpts1(degree, knots, Poles, out PK1); wird jetzt übergeben
            deriv.Clear();
            for (int j = 0; j <= degree - 1; ++j)
            {
                deriv.Add(N[j], Deriv1[j + span - degree]);
            }
        }
        static public void CurveDerivsAlg(int degree, int d, double[] knots, Pole[] Poles, Pole[][] PK, double u, out Pole[] deriv)
        {
            // implementiert im Buch S. 99 für d==1, also nur 1. Ableitung
            // die unveränderlichen PK1 müssen natürlich zum BSpline objekt, damit sie nicht immer neu berechnet werden müssen
            // für NURBS mit Weight, (also rationale) muss noch mit RatCurveDerivs1 nachgebessert werden
            // Des Ergebnis von CurveDerivCpts wird als Parameter reingegeben, da es von u unabhängig ist
            int n = knots.Length - degree - 1;
            int span = FindSpan(n, degree, u, knots);
            double[][] N;
            AllBasisFuns(span, u, degree, knots, out N);
            deriv = new Pole[d + 1];
            for (int k = degree + 1; k <= d; ++k)
            {
                deriv[k] = Poles[0].Clone(); // gleicher Typ;
                deriv[k].Clear(); // null setzen
            }
            int du = Math.Min(d, degree);
            for (int k = 0; k <= du; ++k)
            {
                deriv[k] = Poles[0].Clone(); // gleicher Typ;
                deriv[k].Clear(); // null setzen
                for (int j = 0; j <= degree - k; ++j)
                {
                    deriv[k].Add(N[degree - k][j], PK[k][j + span - degree]);
                }
            }
        }
        static public void RatCurveDerivs1(Pole derivAtU, Pole pointAtU, Pole CK)
        {
            // im Buch S. 127. Aders und wders sind die echten Komponenten bzw. das Gewicht
            // Es wird kein Array von Poles übergeben, sondern nur die 0. und 1. Ableitung
            // Es wird auch nicht in Koordinaten und Gewicht geteilt, das wird hier direkt gemacht
            // Für k==0 wird pointAt durch sein gewicht geteilt, für k==1 die gesuchte 1. Ableitung bestimmt
            Pole pointAtUNorm = pointAtU.Clone();
            pointAtUNorm.Norm(); // für den k=0 Fall wird nur durch Gewicht geteilt
            Pole v = derivAtU.Clone();
            v.Add(-derivAtU.Weight, pointAtUNorm); // Bin11 ist hoffentlich 1
            CK.Set(v);
        }
        static private int[][] Bino(int max)
        {
            int[][] res = new int[max + 1][];
            for (int i = 0; i <= max; ++i)
            {
                res[i] = new int[i + 1];
                for (int j = 0; j <= i; ++j)
                {   // i über j
                    if (i == 0) res[i][j] = 1;
                    else if (i == j || j == 0)
                    {
                        res[i][j] = 1;
                    }
                    else
                    {
                        res[i][j] = res[i - 1][j] + res[i - 1][j - 1];
                    }
                }
            }
            return res;
        }
        static public void RatCurveDerivs(Pole[] derivAtU, int d, out Pole[] CK)
        {
            // im Buch S. 127. Aders und wders sind die echten Komponenten bzw. das Gewicht
            // diese beiden werden hier in einem Parameter übergeben
            CK = new Pole[d + 1];
            Pole nullPole = derivAtU[0].Clone();
            nullPole.Clear();
            double w = derivAtU[0].Weight;
            int[][] B = Bino(d);
            for (int k = 0; k <= d; ++k)
            {
                Pole v = derivAtU[k].Clone();
                for (int i = 1; i <= k; ++i)
                {
                    v.Add(-B[k][i] * derivAtU[i].Weight, CK[k - i]);
                }
                CK[k] = v.Create(1.0 / w, v, nullPole);
            }
        }
        static public int CurveKnotIns(int degree, double[] knots, Pole[] poles, double u, int r, out double[] newknots, out Pole[] newpoles)
        {
            // Buch S. 151
            // p = degree
            // s ist die Anzahl wie oft der Knoten schon drin ist (links von k), kann man berechnen
            // r ist die Anzahl wie oft er noch rein soll (als Parameter: wie oft er drin sein soll, wird ggf.
            // runtergerechnet, wenn er schon drin ist)
            // das Ergebnis ist der Index, an dem u eingefügt wurde und wo somit die knots und poles aufzuteilen
            // sind, wenn es denn zum splitten verwendet wird.
            int np = poles.Length - 1; // könnte auch "knots.Length - degree - 1" sein, oder?
            int k = FindSpan(knots.Length - degree - 1, degree, u, knots);
            int s = 0;
            while (knots[k - s] == u)
            {
                ++s;
                --r;
            }
            int mp = np + degree + 1;
            int nq = np + r;
            newknots = new double[mp + r + 1];
            newpoles = new Pole[poles.Length + r]; // ist vielleicht falsch (sieht aber gut aus)
            Pole[] RW = new Pole[degree + 1];
            for (int i = 0; i <= k; ++i) newknots[i] = knots[i];
            for (int i = 1; i <= r; ++i) newknots[k + i] = u;
            for (int i = k + 1; i <= mp; ++i) newknots[i + r] = knots[i];

            for (int i = 0; i <= k - degree; ++i) newpoles[i] = poles[i].Clone();
            for (int i = k - s; i <= np; ++i) newpoles[i + r] = poles[i].Clone();
            for (int i = 0; i <= degree - s; ++i) RW[i] = poles[k - degree + i].Clone();
            int L = 0;
            for (int j = 1; j <= r; ++j)
            {
                L = k - degree + j;
                for (int i = 0; i <= degree - j - s; ++i)
                {
                    double alpha = (u - knots[L + i]) / (knots[i + k + 1] - knots[L + i]);
                    Pole tmp = poles[0].Clone(); // um einen vom gleichen typ zu erzeugen
                    tmp.Clear();
                    tmp.Add(alpha, RW[i + 1]);
                    tmp.Add(1.0 - alpha, RW[i]); // auch w stimmt so!
                    RW[i] = tmp;
                    // RW[i] = alpha * RW[i + 1] + (1.0 - alpha) * RW[i];
                }
                newpoles[L] = RW[0].Clone();
                newpoles[k + r - j - s] = RW[degree - j - s].Clone();
            }
            for (int i = L + 1; i < k - s; ++i)
            {
                newpoles[i] = RW[i - L].Clone();
            }
            return k;
        }
    }
    #endregion

    internal interface IPoleCalculator<T>
    {
        T Add(T a, T b);
        T Sub(T a, T b);
        T Mul(double f, T a);
        T Norm(T a);    // Normierung auf Länge 1
        bool IsZero(T a);
        bool IsRational { get; }
        T NormH(T a);
        double Weight(T a);
        double EuclNorm(T a);   // Euklidische Norm
        double Dist(T a, T b);
        double[] GetComponents(T a);
        void SetComponents(ref T a, double[] c);
        void ClearWeight(ref T a);
    }

    struct GeoPoint2DPole : IPoleCalculator<GeoPoint2D>
    {
        #region IPoleCalculator<GeoPoint2D> Members
        public GeoPoint2D Add(GeoPoint2D a, GeoPoint2D b)
        {
            return new GeoPoint2D(a.x + b.x, a.y + b.y);
        }
        public GeoPoint2D Sub(GeoPoint2D a, GeoPoint2D b)
        {
            return new GeoPoint2D(a.x - b.x, a.y - b.y);
        }
        public GeoPoint2D Mul(double f, GeoPoint2D a)
        {
            return new GeoPoint2D(f * a.x, f * a.y);
        }
        public GeoPoint2D Norm(GeoPoint2D a)
        {
            if (a == GeoPoint2D.Origin)
            {
                throw new NurbsException("Norm with zero vector");
            }
            double factor = 1 / Geometry.Dist(a, GeoPoint2D.Origin);
            return Mul(factor, a);
        }
        public bool IsZero(GeoPoint2D a)
        {
            return (a == GeoPoint2D.Origin);
        }
        public bool IsRational
        {
            get
            {
                return false;
            }
        }
        public GeoPoint2D NormH(GeoPoint2D a)
        {
            return a; // ist ja nicht homogen, kommt auch nie dran
        }
        public double Weight(GeoPoint2D a)
        {
            return 1.0; // kommt auch nie dran
        }
        public double EuclNorm(GeoPoint2D a)
        {
            return Math.Sqrt(a.x * a.x + a.y * a.y);
        }
        public double Dist(GeoPoint2D a, GeoPoint2D b)
        {
            return Geometry.Dist(a, b);
        }
        public double[] GetComponents(GeoPoint2D a)
        {
            return new double[] { a.x, a.y };
        }
        public void SetComponents(ref GeoPoint2D a, double[] c)
        {
            a.x = c[0];
            a.y = c[1];
        }
        public void ClearWeight(ref GeoPoint2D a) { }

        #endregion
    }

    struct GeoPointPole : IPoleCalculator<GeoPoint>
    {
        #region IPoleCalculator<GeoPoint> Members
        public GeoPoint Add(GeoPoint a, GeoPoint b)
        {
            return new GeoPoint(a.x + b.x, a.y + b.y, a.z + b.z);
        }
        public GeoPoint Sub(GeoPoint a, GeoPoint b)
        {
            return new GeoPoint(a.x - b.x, a.y - b.y, a.z - b.z);
        }
        public GeoPoint Mul(double f, GeoPoint a)
        {
            return new GeoPoint(f * a.x, f * a.y, f * a.z);
        }
        public GeoPoint Norm(GeoPoint a)
        {
            if (a == GeoPoint.Origin)
            {
                throw new NurbsException("Norm with zero vector");
            }
            double factor = 1 / Geometry.Dist(a, GeoPoint.Origin);
            return Mul(factor, a);
        }
        public bool IsZero(GeoPoint a)
        {
            return (a == GeoPoint.Origin);
        }
        public bool IsRational
        {
            get
            {
                return false;
            }
        }
        public GeoPoint NormH(GeoPoint a)
        {
            return a; // ist ja nicht homogen, kommt auch nie dran
        }
        public double Weight(GeoPoint a)
        {
            return 1.0; // kommt auch nie dran
        }
        public double EuclNorm(GeoPoint a)
        {
            return Math.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z);
        }
        public double Dist(GeoPoint a, GeoPoint b)
        {
            return Geometry.Dist(a, b);
        }
        public double[] GetComponents(GeoPoint a)
        {
            return new double[] { a.x, a.y, a.z };
        }
        public void SetComponents(ref GeoPoint a, double[] c)
        {
            a.x = c[0];
            a.y = c[1];
            a.z = c[2];
        }
        public void ClearWeight(ref GeoPoint a) { }
        #endregion
    }

    /// <summary>
    /// Interner homogener 2D Punkt für die Verwendung in Nurbs&lt;T, C&gt;
    /// </summary>
    internal struct GeoPoint2DH
    {
        public double x, y, w;
        public GeoPoint2DH(GeoPoint2D p, double w)
        {
            x = p.x * w;
            y = p.y * w;
            this.w = w;
        }
        public GeoPoint2DH(double x, double y, double w)
        {
            this.x = x;
            this.y = y;
            this.w = w;
        }
        static public implicit operator GeoPoint2D(GeoPoint2DH h)
        {
            if (h.w != 0.0) return new GeoPoint2D(h.x / h.w, h.y / h.w);
            else return new GeoPoint2D(h.x, h.y);
        }
        static public implicit operator GeoVector2D(GeoPoint2DH h)
        {
            return new GeoVector2D(h.x, h.y); // Vector nicht durch w teilen
        }
    }
    /// <summary>
    /// Interner homogener Punkt für die Verwendung in Nurbs&lt;T, C&gt;
    /// </summary>
    internal struct GeoPointH
    {
        public double x, y, z, w;
        public GeoPointH(GeoPoint p, double w)
        {
            x = p.x * w;
            y = p.y * w;
            z = p.z * w;
            this.w = w;
        }
        public GeoPointH(double x, double y, double z, double w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
        static public implicit operator GeoPoint(GeoPointH h)
        {
            return new GeoPoint(h.x / h.w, h.y / h.w, h.z / h.w);
        }
        static public implicit operator GeoVector(GeoPointH h)
        {
            return new GeoVector(h.x, h.y, h.z); // Vector nicht durch w teilen
        }
        public override string ToString()
        {
            return "(" + x.ToString("f3") + " " + y.ToString("f3") + " " + z.ToString("f3") + " " + w.ToString("f3") + ")";
        }
    }

    struct GeoPoint2DHPole : IPoleCalculator<GeoPoint2DH>
    {
        #region IPoleCalculator<GeoPoint2DH> Members
        public GeoPoint2DH Add(GeoPoint2DH a, GeoPoint2DH b)
        {
            return new GeoPoint2DH(a.x + b.x, a.y + b.y, a.w + b.w);
        }
        public GeoPoint2DH Sub(GeoPoint2DH a, GeoPoint2DH b)
        {
            return new GeoPoint2DH(a.x - b.x, a.y - b.y, a.w - b.w);
        }
        public GeoPoint2DH Mul(double f, GeoPoint2DH a)
        {
            return new GeoPoint2DH(f * a.x, f * a.y, f * a.w);
        }
        public GeoPoint2DH Norm(GeoPoint2DH a)
        {
            return a;
        }
        public bool IsZero(GeoPoint2DH a)
        {
            return (a.x == 0.0 && a.y == 0.0);
        }
        public bool IsRational
        {
            get
            {
                return true;
            }
        }
        public GeoPoint2DH NormH(GeoPoint2DH a)
        {
            return new GeoPoint2DH(a.x / a.w, a.y / a.w, 1.0);
        }
        public double Weight(GeoPoint2DH a)
        {
            return a.w;
        }
        public double EuclNorm(GeoPoint2DH a)
        {
            return Math.Sqrt(a.x * a.x + a.y * a.y + a.w * a.w);
        }
        public double Dist(GeoPoint2DH a, GeoPoint2DH b)
        {
            return Geometry.Dist((GeoPoint2D)a, (GeoPoint2D)b);
        }
        public double[] GetComponents(GeoPoint2DH a)
        {
            return new double[] { a.x, a.y, a.w };
        }
        public void SetComponents(ref GeoPoint2DH a, double[] c)
        {
            a.x = c[0];
            a.y = c[1];
            a.w = c[2];
        }
        public void ClearWeight(ref GeoPoint2DH a)
        {
            a.w = 1.0;
        }
        #endregion
    }

    struct GeoPointHPole : IPoleCalculator<GeoPointH>
    {
        #region IPoleCalculator<GeoPointH> Members
        public GeoPointH Add(GeoPointH a, GeoPointH b)
        {
            return new GeoPointH(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        }
        public GeoPointH Sub(GeoPointH a, GeoPointH b)
        {
            return new GeoPointH(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
        }
        public GeoPointH Mul(double f, GeoPointH a)
        {
            return new GeoPointH(f * a.x, f * a.y, f * a.z, f * a.w);
        }
        public GeoPointH Norm(GeoPointH a)
        {
            return a;
        }
        public bool IsZero(GeoPointH a)
        {
            return (a.x == 0.0 && a.y == 0.0 && a.z == 0.0);
        }
        public bool IsRational
        {
            get
            {
                return true;
            }
        }
        public GeoPointH NormH(GeoPointH a)
        {
            return new GeoPointH(a.x / a.w, a.y / a.w, a.z / a.w, 1.0);
        }
        public double Weight(GeoPointH a)
        {
            return a.w;
        }
        public double EuclNorm(GeoPointH a)
        {
            return Math.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z + a.w * a.w);
        }
        public double Dist(GeoPointH a, GeoPointH b)
        {
            return Geometry.Dist((GeoPoint)a, (GeoPoint)b);
        }
        public double[] GetComponents(GeoPointH a)
        {
            return new double[] { a.x, a.y, a.z, a.w };
        }
        public void SetComponents(ref GeoPointH a, double[] c)
        {
            a.x = c[0];
            a.y = c[1];
            a.z = c[2];
            a.w = c[3];
        }
        public void ClearWeight(ref GeoPointH a)
        {
            a.w = 1.0;
        }
        #endregion
    }

    internal class NurbsException : ApplicationException
    {
        public NurbsException(string msg)
            : base(msg)
        {
        }
    }

    interface Indexer<T>
    {
        T this[int i] { get; set; }
        int Length { get; }
    }
    /// <summary>
    /// Implementiert nach "The NURBS Book" (Piegl, Tiller) und nach Ideen aus
    /// http://www.codeproject.com/csharp/genericnumerics.asp
    /// Helferklasse für 2 und 3 dimensionale NURBS Kurven und für Nurbsflächen rational oder nicht rational.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="C"></typeparam>
    internal class Nurbs<T, C>
        where T : new()
        where C : IPoleCalculator<T>, new()
    {
        internal T[] poles; // die Pole des NURBS. Sie können gewichtet sein oder auch nicht, 2 oder 3 dimensional
        // bei Flächen ist poles eigentlich zweidumensional und die Indizierung läuft wie unten beschrieben
        int udegree; // der Grad (bei Flächen in U-Richtung
        int vdegree; // der Grad (bei Flächen in U-Richtung
        internal double[] uknots; // flacher Knotenvektor, also ggf. mit Wiederholungen und periodic Ergänzung
        internal double[] vknots; // v-Vektor, falls es eine Fläche ist
        T[] deriv1; // Pole der 1. Ableitung, werden immer erzeugt
        T[] deriv2; // Pole der 2. Ableitung werden nur bei Bedarf erzeugt
        internal int numUPoles; // poles[u,v] = poles[u + numUPoles*v], deriv1[u,v] = deriv1[u+(numUPoles-1)*v]
        internal int numVPoles;
        private class DerivsKL
        {
            class Fixed : Indexer<T>
            {
                DerivsKL derivsKL; // Rückverweis
                int k, l, j;
                bool fixedj;
                public Fixed(DerivsKL derivsKL, int k, int l, int j, bool fixedj)
                {
                    this.derivsKL = derivsKL;
                    this.k = k;
                    this.l = l;
                    this.j = j;
                    this.fixedj = fixedj;
                }
                #region Indexer<T> Members

                T Indexer<T>.this[int i]
                {
                    get
                    {
                        if (fixedj)
                        {
                            return derivsKL[k, l, i, j];
                        }
                        else
                        {
                            return derivsKL[k, l, j, i];
                        }
                    }
                    set
                    {
                        derivsKL[k, l, i, j] = value;
                    }
                }

                int Indexer<T>.Length
                {
                    get
                    {
                        return 0; // sollte nicht drankommen
                    }
                }

                #endregion
            }
            Nurbs<T, C> nurbs;
            public DerivsKL(Nurbs<T, C> nurbs, int maxDeriv)
            {
                this.nurbs = nurbs;
                PKL = new T[maxDeriv + 1, maxDeriv + 1, nurbs.numUPoles, nurbs.numVPoles];
            }
            T[,,,] PKL;
            public T this[int k, int l, int i, int j]
            {
                get
                {
                    return PKL[k, l, i, j];
                }
                set
                {
                    PKL[k, l, i, j] = value;
                }
            }
            public Indexer<T> FixedJ(int k, int l, int j)
            {
                return new Fixed(this, k, l, j, true) as Indexer<T>;
            }
            public Indexer<T> FixedI(int k, int l, int i)
            {
                return new Fixed(this, k, l, i, false) as Indexer<T>;
            }

        }
        // Hilfsfunktionen zum Indizieren bei Flächen
        int ind(int u, int v) { return u + numUPoles * v; }
        int ind1(int u, int v) { return u + (numUPoles - 1) * v; }
        int ind2(int u, int v) { return u + (numUPoles - 2) * v; }
        int inds(bool isU, int u, int v)
        {
            if (isU) return (u + numUPoles * v) * 2;
            else return (u + numUPoles * v) * 2 + 1;
        }
        class PoleRow : Indexer<T>
        {
            Nurbs<T, C> nurbs;
            int fix;
            bool fixu;
            public PoleRow(Nurbs<T, C> nurbs, int fix, bool fixu)
            {
                this.nurbs = nurbs;
                this.fix = fix;
                this.fixu = fixu;
            }

            #region Indexer<T> Members

            public T this[int i]
            {
                get
                {
                    if (fixu)
                    {
                        return nurbs.poles[fix + nurbs.numUPoles * i];
                    }
                    else
                    {
                        return nurbs.poles[i + nurbs.numUPoles * fix];
                    }
                }
                set
                {
                    if (fixu)
                    {
                        nurbs.poles[fix + nurbs.numUPoles * i] = value;
                    }
                    else
                    {
                        nurbs.poles[i + nurbs.numUPoles * fix] = value;
                    }
                }
            }

            public int Length
            {
                get { throw new NotImplementedException(); }
            }

            #endregion
        }
        C calc; // die abstrakte Rechenmaschine

        public Nurbs(int degree, T[] poles, double[] knots)
        {
            calc = new C(); // das kostet angeblich nix!
            this.udegree = degree;
            this.poles = poles;
            this.uknots = knots;
            if (knots.Length - degree - 1 != poles.Length) throw new NurbsException("lenth of knots and poles not compatible with degree");
            if (poles.Length <= udegree) throw new NurbsException("lenth of knots and poles not compatible with degree");
            // Dieser einzige Konstruktor testet die Konsistenz bezüglich der Längen der arrays und degree
            // damit kann weiter nichts beim indizieren schief gehen
        }



        // Konstruktor macht aus unclamped-Nurbs-Daten einen clamped Nurbs (Buch S.576)
        public Nurbs(bool periodic, int degree, T[] poles, double[] knots)
        {
            calc = new C(); // das kostet angeblich nix!
            this.udegree = degree;
            this.poles = poles;
            this.uknots = knots;
            // if (knots.Length - degree - 1 != poles.Length) throw new NurbsException("lenth of knots and poles not compatible with degree");
            // Dieser einzige Konstruktor testet die Konsistenz bezüglich der Längen der arrays und degree
            // damit kann weiter nichts beim indizieren schief gehen
            Nurbs<T, C> res = Trim(knots[degree], knots[knots.Length - 1 - degree]);
            this.poles = res.poles;
            this.uknots = res.uknots;
        }




        public Nurbs(int degree, T[] throughpoints, T[] throughdirections, bool periodic)
        {   // im Buch Seite 375
            // die Vektoren müssen wohl Einheitsvektoren sein, zumindest beim test mit dem Kreis war es so
            // funktioniert bei 2. und 3. Grad ganz ordentlich, bei hohem Grad sieht es am Anfang und am Ende schlecht aus
            // es liefert aber z.B. beim Kreis immer noch einen etwas größeren Fehler, wenn man mit Tangenten arbeitet als ohne
            // bei gleicher Punktzahl. Komisch, oder?
            // also erstmal nicht verwenden...
            if (throughpoints.Length != throughdirections.Length) throw new NurbsException("points and direction arrays must be same size");
            calc = new C(); // das kostet angeblich nix!
            this.udegree = degree;

            // 1. Abstände für den Knotenvektor
            double[] k = new double[throughpoints.Length - 1];
            double lastk = 0.0;
            for (int i = 0; i < k.Length; ++i)
            {
                k[i] = lastk + calc.Dist(throughpoints[i + 1], throughpoints[i]);
                lastk = k[i];
            }
            // 2. daraus den Knotenvektor
            uknots = new double[2 * throughpoints.Length + degree + 1];
            for (int i = 0; i < degree + 1; ++i)
            {
                uknots[i] = 0.0;
            }

            // der Vektor k ist also die Basis. uknots soll erzeugt werden. Die ersten degree+1 und die letzten degree+1 stehen schon fest.
            // die mittleren ergeben sich durch strecken der k-Skala auf die uknots skala.
            // 2*throughpoints.Length + degree + 1 - 2*(degree+1) müssen noch verteilt werden
            // 2*throughpoints.Length - degree -1 auf throughpoints.Length - 1
            double f = (double)(throughpoints.Length - 1) / (double)(2 * throughpoints.Length - degree);
            for (int i = degree + 1; i < 2 * throughpoints.Length; ++i)
            {
                double d = (i - degree) * f;
                int ind = (int)Math.Floor(d);
                double s;
                if (ind > 0)
                {
                    s = k[ind - 1] + (d - ind) * (k[ind] - k[ind - 1]);
                }
                else
                {
                    s = 0.0 + (d - ind) * (k[ind] - 0.0);
                }
                uknots[i] = s;
            }
            for (int i = 2 * throughpoints.Length; i < 2 * throughpoints.Length + degree + 1; ++i)
            {
                uknots[i] = k[throughpoints.Length - 2];
            }
            // 3. zunächste leere Poles erzeugen
            poles = new T[2 * throughpoints.Length];
            // 4. Matrix für das Gleichungssystem erzeugen
            double[,] matrix = new double[poles.Length, poles.Length]; // sind alle 0
            for (int i = 0; i < throughpoints.Length; ++i)
            {
                double[,] bf;
                double u;
                if (i < 1) u = 0.0;
                else u = k[i - 1];
                int span = FindSpanU(uknots.Length - degree - 1, degree, u);
                DersBasisFuns(span, u, udegree, 1, out bf);
                // die inneren Zeilen der Matrix sind um 1 nach rechts verschoben, hier mit ++span implementiert
                //if (i > 0 && i < throughpoints.Length-1) ++span;
                for (int j = 0; j <= udegree; ++j)
                {
                    matrix[2 * i, span - degree + j] = bf[0, j];
                }
                // diese Zeilen gemäß dem Buch liefern dasselbe wie der allgemeine Fall
                //if (i == 0)
                //{
                //    double uu = uknots[degree + 1] / degree;
                //    matrix[2 * i + 1, 0] = -1/uu;
                //    matrix[2 * i + 1, 1] = 1 / uu;
                //}
                //else if (i == throughpoints.Length - 1)
                //{
                //    double uu = (uknots[poles.Length] - uknots[poles.Length - 1]) / degree;
                //    matrix[2 * i + 1, poles.Length - 2] = -1 / uu;
                //    matrix[2 * i + 1, poles.Length - 1] = 1 / uu;
                //}
                //else
                for (int j = 0; j <= udegree; ++j)
                {
                    matrix[2 * i + 1, span - degree + j] = bf[1, j];
                }
            }
            // q ist der Lösungsvektor, also die Durchgangspunkte und die Richtungen
            int dim = calc.GetComponents(throughpoints[0]).Length;
            double[,] q = new double[2 * throughpoints.Length, dim];
            for (int i = 0; i < throughpoints.Length; ++i)
            {
                double[] c = calc.GetComponents(throughpoints[i]);
                for (int j = 0; j < c.Length; ++j)
                {
                    q[2 * i, j] = c[j];
                }
                c = calc.GetComponents(throughdirections[i]);
                for (int j = 0; j < c.Length; ++j)
                {
                    q[2 * i + 1, j] = c[j];
                }
            }
            Matrix lam = DenseMatrix.OfArray(matrix);
            Matrix laq = DenseMatrix.OfArray(q);
            LU<double> lud = lam.LU();
            Matrix lapoles = (Matrix)lud.Solve(laq);
            for (int i = 0; i < poles.Length; ++i)
            {
                double[] lap = new double[dim];
                for (int j = 0; j < dim; ++j)
                {
                    lap[j] = lapoles[i, j];
                }
                calc.SetComponents(ref poles[i], lap);
            }
        }


        public Nurbs(int degree, T[] throughpoints, bool periodic, out double[] throughpointsparam)
        {   // im Buch Seite 369
            calc = new C(); // das kostet angeblich nix!
            degree = Math.Min(degree, throughpoints.Length - 1); // bei 2 Punkten nur 1. Grad, also Linie, u.s.w
            this.udegree = degree;
            if (periodic)
            {   // die Durchgangspunkte nach vorne und hinten verlängern, so dass der gesuchte Spline nur ein innerer Abschnitt
                // des erzeugten ist. 
                // Wenn der letzte und der erste Punkt nicht identisch sind, dann wird der erste zusätzlich hinten angehängt
                // es entsteht also genaugenommen ein offener Spline, der geclampt ist, aber in der Geometrie 
                // exakt periodisch ist
                T[] closedthroughpoints = throughpoints;
                if (calc.Dist(throughpoints[0], throughpoints[throughpoints.Length - 1]) > Precision.eps)
                {
                    closedthroughpoints = new T[throughpoints.Length + 1];
                    Array.Copy(throughpoints, closedthroughpoints, throughpoints.Length);
                    closedthroughpoints[closedthroughpoints.Length - 1] = throughpoints[0];
                }
                T[] cpoints = new T[closedthroughpoints.Length + 2 * degree];
                Array.Copy(closedthroughpoints, 0, cpoints, degree, closedthroughpoints.Length);
                for (int i = 0; i < degree; ++i)
                {
                    cpoints[i] = closedthroughpoints[closedthroughpoints.Length - 1 - degree + i];
                    cpoints[closedthroughpoints.Length + degree + i] = closedthroughpoints[i + 1];
                }
                double[] tmpparam;
                Nurbs<T, C> tmp = new Nurbs<T, C>(degree, cpoints, false, out tmpparam); // einen offenen verlängerten machen
                double u0, u1;  // an diesen Stellen muss tmp jetzt getrimmt werden
                u0 = tmpparam[degree];
                u1 = tmpparam[degree + closedthroughpoints.Length - 1];
                tmp = tmp.Trim(u0, u1);
                this.poles = tmp.poles;
                this.uknots = tmp.uknots;
                throughpointsparam = new double[closedthroughpoints.Length];
                for (int i = 0; i < throughpointsparam.Length; ++i)
                {
                    throughpointsparam[i] = tmpparam[degree + i];
                }
                return;
            }

            // 1. Abstände für den Knotenvektor
            double[] k = new double[throughpoints.Length - 1];
            double lastk = 0.0;
            bool kIsOK = true;
            for (int i = 0; i < k.Length; ++i)
            {
                double d = calc.Dist(throughpoints[i + 1], throughpoints[i]);
                if (d == 0.0)
                {
                    kIsOK = false;
                    break;
                }
                k[i] = lastk + d;
                lastk = k[i];
            }
            if (!kIsOK)
            {   // wenn mehrere gleiche Punkte vorkommen, dann wird ein einfacher um 1 fortschreitender
                // Knotenvektor gemacht. Das ist glaube ich "uniform"
                for (int i = 0; i < k.Length; ++i)
                {
                    k[i] = i + 1;
                }
            }
            // die Parameter die zu den Throughpoints gehören lassen sich aus den Knoten nicht mehr (oder nur mit Aufwand)
            // rekonstruieren. Deshalb werden sie hier zurückgeliefert
            throughpointsparam = new double[k.Length + 1];
            for (int i = 0; i < k.Length; ++i)
            {
                throughpointsparam[i + 1] = k[i];
            }
            throughpointsparam[0] = 0.0;
            // 2. daraus den Knotenvektor
            uknots = new double[throughpoints.Length + degree + 1];
            for (int i = degree + 1; i < throughpoints.Length; ++i)
            {
                double s = 0.0;
                for (int j = 0; j < degree; ++j)
                {
                    s += k[i - degree - 1 + j];
                }
                uknots[i] = s / degree;
            }
            for (int i = throughpoints.Length; i < throughpoints.Length + degree + 1; ++i)
            {
                uknots[i] = k[throughpoints.Length - 2];
            }
            if (periodic)
            {   // kommt nicht mehr dran
                for (int i = degree; i >= 0; --i)
                {
                    uknots[i] = uknots[i + 1] - (uknots[throughpoints.Length - degree + i] - uknots[throughpoints.Length - degree + i - 1]);
                }
                for (int i = throughpoints.Length; i < throughpoints.Length + degree + 1; ++i)
                {
                    uknots[i] = uknots[i - 1] + uknots[1 + i - throughpoints.Length] - uknots[i - throughpoints.Length];
                }
            }
            else
            {   // ist doch schon oben gesetzt
                for (int i = 0; i < degree + 1; ++i)
                {
                    uknots[i] = 0.0;
                }
                for (int i = throughpoints.Length; i < throughpoints.Length + degree + 1; ++i)
                {
                    uknots[i] = k[throughpoints.Length - 2];
                }
            }
            // 3. zunächste leere Poles erzeugen
            poles = new T[throughpoints.Length];
            // 4. Matrix für das Gleichungssystem erzeugen
            // double[,] matrix = new double[poles.Length, poles.Length]; // sind alle 0
            Matrix bmatrix = new SparseMatrix(poles.Length, poles.Length);
            // LinearAlgebra.BandedMatrix bmatrix = new CADability.LinearAlgebra.BandedMatrix(poles.Length, degree - 1, degree - 1);
            for (int i = 0; i < poles.Length; ++i)
            {
                double[] bf;
                double u;
                if (i == 0) u = 0.0;
                else u = k[i - 1];
                int span = FindSpanU(uknots.Length - degree - 1, degree, u);
                BasisFunsU(span, u, degree, out bf);
                for (int j = 0; j < bf.Length; ++j)
                {
                    // matrix[i, span - degree + j] = bf[j];
                    bmatrix[i, span - degree + j] = bf[j];
                }
            }
            // q ist der Lösungsvektor, also die Durchgangspunkte
            int dim = calc.GetComponents(throughpoints[0]).Length;
            double[,] q = new double[throughpoints.Length, dim];
            for (int i = 0; i < throughpoints.Length; ++i)
            {
                double[] c = calc.GetComponents(throughpoints[i]);
                for (int j = 0; j < c.Length; ++j)
                {
                    q[i, j] = c[j];
                }
            }
            //LinearAlgebra.Matrix lam = new CADability.LinearAlgebra.Matrix(matrix);
            Matrix laq = DenseMatrix.OfArray(q);
            //LinearAlgebra.LUDecomposition lud = lam.LUD();
            //LinearAlgebra.Matrix lapoles = lud.Solve(laq);
            Matrix bpoles;
            bpoles = (Matrix)bmatrix.Solve(laq);
            if (bpoles != null)
            {
                for (int i = 0; i < poles.Length; ++i)
                {
                    double[] lap = new double[dim];
                    for (int j = 0; j < dim; ++j)
                    {
                        // lap[j] = lapoles[i, j];
                        lap[j] = bpoles[i, j];
                    }
                    calc.SetComponents(ref poles[i], lap);
                }
            }
            else
            {
                throw new NurbsException("unable to construct NURBS with throughpoints");
            }
        }



        public Nurbs(int degree, T[] throughpoints, double[] k, bool periodic)
        {   // im Buch Seite 369
            calc = new C(); // das kostet angeblich nix!
            degree = Math.Min(degree, throughpoints.Length - 1); // bei 2 Punkten nur 1. Grad, also Linie, u.s.w
            this.udegree = degree;
            if (periodic)
            {   // die Durchgangspunkte nach vorne und hinten verlängern, so dass der gesuchte Spline nur ein innerer Abschnitt
                // des erzeugten ist. 
                // Wenn der letzte und der erste Punkt nicht identisch sind, dann wird der erste zusätzlich hinten angehängt
                // es entsteht also genaugenommen ein offener Spline, der geclampt ist, aber in der Geometrie 
                // exakt periodisch ist
                T[] closedthroughpoints = throughpoints;
                if (calc.Dist(throughpoints[0], throughpoints[throughpoints.Length - 1]) > Precision.eps)
                {
                    closedthroughpoints = new T[throughpoints.Length + 1];
                    Array.Copy(throughpoints, closedthroughpoints, throughpoints.Length);
                    closedthroughpoints[closedthroughpoints.Length - 1] = throughpoints[0];
                }
                T[] cpoints = new T[closedthroughpoints.Length + 2 * degree];
                Array.Copy(closedthroughpoints, 0, cpoints, degree, closedthroughpoints.Length);
                for (int i = 0; i < degree; ++i)
                {
                    cpoints[i] = closedthroughpoints[closedthroughpoints.Length - 1 - degree + i];
                    cpoints[closedthroughpoints.Length + degree + i] = closedthroughpoints[i + 1];
                }
                double[] tmpparam;
                Nurbs<T, C> tmp = new Nurbs<T, C>(degree, cpoints, false, out tmpparam); // einen offenen verlängerten machen
                double u0, u1;  // an diesen Stellen muss tmp jetzt getrimmt werden
                u0 = tmpparam[degree];
                u1 = tmpparam[degree + closedthroughpoints.Length - 1];
                double diff = (tmp.uknots[tmp.uknots.Length - 1] - tmp.uknots[0]) * 1e-8;
                for (int i = 0; i < tmp.uknots.Length; i++)
                {
                    if (Math.Abs(u0 - tmp.uknots[i]) < diff) u0 = tmp.uknots[i];
                    if (Math.Abs(u1 - tmp.uknots[i]) < diff) u1 = tmp.uknots[i];
                }
                tmp = tmp.Trim(u0, u1);
                //tmp = tmp.TrimAtKnot(degree + 2, tmp.uknots.Length - 3 - degree);
                this.poles = tmp.poles;
                this.uknots = tmp.uknots;
                double kmin = tmp.uknots[0];
                for (int i = 0; i < uknots.Length; i++)
                {
                    uknots[i] -= kmin;
                }
                return;
            }
            // Zusammenhang zwischen Knoten und k:
            // uknots[i] = (k[i-deg-1]+...+k[i-2])/deg
            // damit liese sich mit einem linearen System aus uknots wieder k berechnen
            // uknots = M*k, wobei m eine Bandmatrix ist, die lauter 1/deg Werte hat, wobei k etwas erweitert
            // am Anfang und Ende 0 bzw letzte k-Werte mehrfach hat
            uknots = new double[throughpoints.Length + degree + 1];
            for (int i = degree + 1; i < throughpoints.Length; ++i)
            {
                double s = 0.0;
                for (int j = 0; j < degree; ++j)
                {
                    s += k[i - degree - 1 + j];
                }
                uknots[i] = s / degree;
            }
            for (int i = throughpoints.Length; i < throughpoints.Length + degree + 1; ++i)
            {
                uknots[i] = k[throughpoints.Length - 2];
            }
            if (periodic)
            {   // kommt nicht mehr dran
                for (int i = degree; i >= 0; --i)
                {
                    uknots[i] = uknots[i + 1] - (uknots[throughpoints.Length - degree + i] - uknots[throughpoints.Length - degree + i - 1]);
                }
                for (int i = throughpoints.Length; i < throughpoints.Length + degree + 1; ++i)
                {
                    uknots[i] = uknots[i - 1] + uknots[1 + i - throughpoints.Length] - uknots[i - throughpoints.Length];
                }
            }
            else
            {   // ist doch schon oben gesetzt
                for (int i = 0; i < degree + 1; ++i)
                {
                    uknots[i] = 0.0;
                }
                for (int i = throughpoints.Length; i < throughpoints.Length + degree + 1; ++i)
                {
                    uknots[i] = k[throughpoints.Length - 2];
                }
            }
            // 3. zunächste leere Poles erzeugen
            poles = new T[throughpoints.Length];
            // 4. Matrix für das Gleichungssystem erzeugen
            // double[,] matrix = new double[poles.Length, poles.Length]; // sind alle 0
            Matrix bmatrix = new SparseMatrix(poles.Length, poles.Length);
            // BandedMatrix bmatrix = new BandedMatrix(poles.Length, degree - 1, degree - 1);
            for (int i = 0; i < poles.Length; ++i)
            {
                double[] bf;
                double u;
                if (i == 0) u = 0.0;
                else u = k[i - 1];
                int span = FindSpanU(uknots.Length - degree - 1, degree, u);
                BasisFunsU(span, u, degree, out bf);
                for (int j = 0; j < bf.Length; ++j)
                {
                    // matrix[i, span - degree + j] = bf[j];
                    bmatrix[i, span - degree + j] = bf[j];
                }
            }
            // q ist der Lösungsvektor, also die Durchgangspunkte
            int dim = calc.GetComponents(throughpoints[0]).Length;
            double[,] q = new double[throughpoints.Length, dim];
            for (int i = 0; i < throughpoints.Length; ++i)
            {
                double[] c = calc.GetComponents(throughpoints[i]);
                for (int j = 0; j < c.Length; ++j)
                {
                    q[i, j] = c[j];
                }
            }
            //LinearAlgebra.Matrix lam = new CADability.LinearAlgebra.Matrix(matrix);
            Matrix laq = DenseMatrix.OfArray(q);
            //LinearAlgebra.LUDecomposition lud = lam.LUD();
            //LinearAlgebra.Matrix lapoles = lud.Solve(laq);
            Matrix bpoles;
            bpoles = (Matrix)bmatrix.Solve(laq);
            if (bpoles != null)
            {
                for (int i = 0; i < poles.Length; ++i)
                {
                    double[] lap = new double[dim];
                    for (int j = 0; j < dim; ++j)
                    {
                        // lap[j] = lapoles[i, j];
                        lap[j] = bpoles[i, j];
                    }
                    calc.SetComponents(ref poles[i], lap);
                }
            }
            else
            {
                throw new NurbsException("unable to construct NURBS with throughpoints");
            }
        }
        public Nurbs(int udegree, int vdegree, T[] poles, int numUPoles, int numVPoles, double[] uknots, double[] vknots)
        {
            calc = new C(); // das kostet angeblich nix!
            this.udegree = udegree;
            this.vdegree = vdegree;
            this.poles = poles;
            this.uknots = uknots;
            this.vknots = vknots;
            this.numUPoles = numUPoles;
            this.numVPoles = numVPoles;
            if (uknots.Length - udegree - 1 != numUPoles) throw new NurbsException("lenth of knots and poles not compatible with degree");
            if (vknots.Length - vdegree - 1 != numVPoles) throw new NurbsException("lenth of knots and poles not compatible with degree");
            if (numUPoles * numVPoles != poles.Length) throw new NurbsException("lenth of knots and poles not compatible with degree");
            if (numUPoles <= udegree || numVPoles <= vdegree) throw new NurbsException("lenth of knots and poles not compatible with degree");
            // Dieser Konstruktor testet die Konsistenz bezüglich der Längen der arrays und degree
            // damit kann weiter nichts beim indizieren schief gehen
#if DEBUG
            // DerivsKL derivsKL = SurfaceDeriveCpts(3);
#endif
        }

        int FindSpanU(int high, int low, double u)
        {
            //if ((u >= uknots[high] && high < uknots.Length - 1) || (u <= uknots[low] && low > 0)) return FindSpanU(uknots.Length - 1, 0, u); // ggf. bei v nachziehen!
            if (u >= uknots[high])
            {
                int res = high - 1; // this was -1, but in one case we need res = high. any cases?
                // im folgenden eine Notbremse, die nur bei periodischen Splines benötigt wird:
                // vermutlich wid das mit einer ordentlichen Implementierung von unclamped unnötig
                while (res > low && uknots[res] == uknots[res + 1]) --res;
                if (res < low) res = low;
                return res;
            }
            // versuchsweise auch für Werte außerhalb arbeiten
            // if (u >= uknots[high]) return high-1;
            if (u <= uknots[low]) return low;
            int mid = (low + high) / 2;
            while (u < uknots[mid] || u >= uknots[mid + 1])
            {
                if (u < uknots[mid]) high = mid;
                else low = mid;
                mid = (low + high) / 2;
                if (low == high) return low;
            }
            return mid;
        }
        int FindSpanV(int high, int low, double v)
        {
            if (v >= vknots[high])
            {
                int res = high - 1; // Sonderfall
                // im folgenden eine Notbremse, die nur bei periodischen Splines benötigt wird:
                // vermutlich wid das mit einer ordentlichen Implementierung von unclamped unnötig
                while (res > 0 && vknots[res] == vknots[res + 1]) --res;
                return res;
            }
            // versuchsweise auch für Werte außerhalb arbeiten
            // if (v >= vknots[high]) return high-1;
            if (v <= vknots[low]) return low;
            int mid = (low + high) / 2;
            while (v < vknots[mid] || v >= vknots[mid + 1])
            {
                if (v < vknots[mid]) high = mid;
                else low = mid;
                mid = (low + high) / 2;
                if (low == high) return low;
            }
            return mid;
        }
        unsafe void BasisFunsU(int span, double u, int deg, out double[] N)
        {
            N = new double[deg + 1];
            fixed (double* pN = N)
            {
                double* left = stackalloc double[deg + 1];
                double* right = stackalloc double[deg + 1];
                pN[0] = 1.0;
                for (int j = 1; j <= deg; ++j)
                {
                    left[j] = u - uknots[span + 1 - j];
                    right[j] = uknots[span + j] - u;
                    double saved = 0.0;
                    for (int r = 0; r < j; ++r)
                    {
                        double temp = pN[r] / (right[r + 1] + left[j - r]);
                        if (!double.IsNaN(temp) && !double.IsInfinity(temp))
                        {
                            pN[r] = saved + right[r + 1] * temp;
                            saved = left[j - r] * temp;
                        }
                        else
                        {
                            pN[r] = saved;
                        }
                    }
                    pN[j] = saved;
                }
            }
        }
        void BasisFunsU(int span, int deg, out Polynom[] N, bool dim2)
        {
            N = new Polynom[deg + 1];
            Polynom[] left = new Polynom[deg + 1];
            Polynom[] right = new Polynom[deg + 1];
            if (dim2) N[0] = new Polynom(1.0, 2);
            else N[0] = new Polynom(1.0, 1);
            for (int j = 1; j <= deg; ++j)
            {
                if (dim2)
                {
                    left[j] = new Polynom(1, "u", -uknots[span + 1 - j], "", 0, "v");
                    right[j] = new Polynom(-1, "u", uknots[span + j], "", 0, "v");
                }
                else
                {
                    left[j] = new Polynom(1, "u", -uknots[span + 1 - j], "");
                    right[j] = new Polynom(-1, "u", uknots[span + j], "");
                }
                Polynom saved;
                if (dim2) saved = new Polynom(0.0, 2);
                else saved = new Polynom(0.0, 1);
                for (int r = 0; r < j; ++r)
                {
                    Polynom temp = (1.0 / (right[r + 1] + left[j - r]).Coeff(0)) * N[r];
                    N[r] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }
                N[j] = saved;
            }
        }
        void BasisFunsV(int span, int deg, out Polynom[] N)
        {
            N = new Polynom[deg + 1];
            Polynom[] left = new Polynom[deg + 1];
            Polynom[] right = new Polynom[deg + 1];
            N[0] = new Polynom(1.0, 2);
            for (int j = 1; j <= deg; ++j)
            {
                left[j] = new Polynom(0, "u", 1, "v", -vknots[span + 1 - j], "");
                right[j] = new Polynom(0, "u", -1, "v", vknots[span + j], "");
                Polynom saved = new Polynom(0.0, 2);
                for (int r = 0; r < j; ++r)
                {
                    Polynom temp = (1.0 / (right[r + 1] + left[j - r]).Coeff(0)) * N[r];
                    N[r] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }
                N[j] = saved;
            }
        }
        void BasisFunsString(int span, double u, int deg, out string[] N)
        {   // liefert die basisfunktionen als string, somit als Input für Maxima
            // damit könnte man die liniearen Schnitte bis 4. Grades direkt lösbar machen
            // und noch einiges andere mehr...
            // indizes sollten sich auf span beziehen
            // ACHTUNG: auf genügende Klammerung achten!
            N = new string[deg + 1];
            string[] left = new string[deg + 1];
            string[] right = new string[deg + 1];
            N[0] = "1 ";
            for (int j = 1; j <= deg; ++j)
            {
                left[j] = "(u - uknots[span +(" + (1 - j).ToString() + ")])";
                right[j] = "(uknots[span +(" + (j).ToString() + ")] - u)";
                string saved = "0 ";
                for (int r = 0; r < j; ++r)
                {
                    string temp = "((" + N[r] + ")/(" + right[r + 1] + "+" + left[j - r] + "))";
                    N[r] = "(" + saved + "+ ((" + right[r + 1] + ")*(" + temp + ")))";
                    saved = "((" + left[j - r] + ")*(" + temp + "))";
                }
                N[j] = saved;
            }
        }
        unsafe void BasisFunsV(int span, double v, int deg, out double[] N)
        {
            N = new double[deg + 1];
            fixed (double* pN = N)
            {
                double* left = stackalloc double[deg + 1];
                double* right = stackalloc double[deg + 1];
                pN[0] = 1.0;
                for (int j = 1; j <= deg; ++j)
                {
                    left[j] = v - vknots[span + 1 - j];
                    right[j] = vknots[span + j] - v;
                    double saved = 0.0;
                    for (int r = 0; r < j; ++r)
                    {
                        double temp = pN[r] / (right[r + 1] + left[j - r]);
                        if (!double.IsNaN(temp) && !double.IsInfinity(temp))
                        {
                            pN[r] = saved + right[r + 1] * temp;
                            saved = left[j - r] * temp;
                        }
                        else
                        {
                            pN[r] = saved;
                        }
                    }
                    pN[j] = saved;
                }
            }
        }
        void AllBasisFunsU(int span, double u, out double[][] AN)
        {
            AN = new double[udegree + 1][];
            for (int i = 0; i <= udegree; ++i)
            {
                BasisFunsU(span, u, i, out AN[i]);
            }
        }
        void AllBasisFunsV(int span, double v, out double[][] AN)
        {
            AN = new double[vdegree + 1][];
            for (int i = 0; i <= vdegree; ++i)
            {
                BasisFunsV(span, v, i, out AN[i]);
            }
        }
        void NBasisFuns(int span, int max, double u, out double[][] AN)
        {   // Berechnet nur einen Teil von AllBasisFuns
            AN = new double[udegree + 1][];
            int firsti = Math.Max(0, udegree - max + 1);
            for (int i = firsti; i <= udegree; ++i)
            {
                BasisFunsU(span, u, i, out AN[i]);
            }
        }
        void DersBasisFuns(int span, double u, int deg, int maxder, out double[,] N)
        {   // Buch Seite 72
            // i = span, p = deg, n = maxder, ders = N
            N = new double[maxder + 1, deg + 1];
            double[,] ndu = new double[deg + 1, deg + 1];
            ndu[0, 0] = 1.0;
            double[] left = new double[deg + 1];
            double[] right = new double[deg + 1];
            for (int j = 1; j <= deg; ++j)
            {
                left[j] = u - uknots[span + 1 - j];
                right[j] = uknots[span + j] - u;
                double saved = 0.0;
                for (int r = 0; r < j; ++r)
                {
                    ndu[j, r] = right[r + 1] + left[j - r];
                    double temp = ndu[r, j - 1] / ndu[j, r];
                    ndu[r, j] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }
                ndu[j, j] = saved;
            }
            for (int j = 0; j <= deg; ++j)
            {
                N[0, j] = ndu[j, deg];
            }
            double[,] a = new double[deg + 1, deg + 1];
            for (int r = 0; r <= deg; ++r)
            {
                int s1 = 0;
                int s2 = 1;
                a[0, 0] = 1.0;
                for (int k = 1; k <= maxder; ++k)
                {
                    double d = 0.0;
                    int rk = r - k;
                    int pk = deg - k;
                    if (r >= k)
                    {
                        a[s2, 0] = a[s1, 0] / ndu[pk + 1, rk];
                        d = a[s2, 0] * ndu[rk, pk];
                    }
                    int j1, j2;
                    if (rk >= -1) j1 = 1;
                    else j1 = -rk;
                    if (r - 1 <= pk) j2 = k - 1;
                    else j2 = pk - r;
                    for (int j = j1; j <= j2; ++j)
                    {
                        a[s2, j] = (a[s1, j] - a[s1, j - 1]) / ndu[pk + 1, rk + j];
                        d += a[s2, j] * ndu[rk + j, pk];
                    }
                    if (r <= pk)
                    {
                        a[s2, k] = -a[s1, k - 1] / ndu[pk + 1, r];
                        d += a[s2, k] * ndu[r, pk];
                    }
                    N[k, r] = d;
                    int tmp = s1;
                    s1 = s2;
                    s2 = tmp;
                }
            }
            int rr = deg;
            for (int k = 1; k <= maxder; ++k)
            {
                for (int j = 0; j <= deg; ++j)
                {
                    N[k, j] *= rr;
                }
                rr *= (deg - k);
            }
        }
        T RatCurveDerivs1(T derivAtU, T pointAtU)
        {
            // im Buch S. 127. Aders und wders sind die echten Komponenten bzw. das Gewicht
            // Es wird kein Array von Poles übergeben, sondern nur die 0. und 1. Ableitung
            // Es wird auch nicht in Koordinaten und Gewicht geteilt, das wird hier direkt gemacht
            // Für k==0 wird pointAt durch sein gewicht geteilt, für k==1 die gesuchte 1. Ableitung bestimmt
            T pointAtUNorm = calc.NormH(pointAtU);
            T v = derivAtU;
            v = calc.Add(v, calc.Mul(-calc.Weight(derivAtU), pointAtUNorm));
            return calc.Mul(1.0 / calc.Weight(pointAtU), v);
        }
        void RatCurveDerivs2(T pointAtU, ref T deriv1AtU, ref T deriv2AtU)
        {
            // im Buch S. 127. Aders und wders sind die echten Komponenten bzw. das Gewicht
            // diese beiden werden hier in einem Parameter übergeben
            double w = calc.Weight(pointAtU);
            // int[][] B = Bino(d);
            //for (int k = 0; k <= d; ++k)
            //{
            //    Pole v = derivAtU[k].Clone();
            //    for (int i = 1; i <= k; ++i)
            //    {
            //        v.Add(-B[k][i] * derivAtU[i].Weight, CK[k - i]);
            //    }
            //    CK[k] = v.Create(1.0 / w, v, nullPole);
            //}
            // k=0: nichts zu tun (pointAtU bleibt unverändert)
            // k=1:
            if (w != 0.0) deriv1AtU = calc.Mul(1.0 / w, calc.Add(deriv1AtU, calc.Mul(-calc.Weight(deriv1AtU), pointAtU)));
            else deriv1AtU = calc.Mul(1.0 / 1.0, calc.Add(deriv1AtU, calc.Mul(-calc.Weight(deriv1AtU), pointAtU)));
            // k=2:
            T v = deriv2AtU; // Binomialkoeffizienten sind alle 1
            v = calc.Add(v, calc.Mul(-calc.Weight(deriv1AtU), deriv1AtU));
            v = calc.Add(v, calc.Mul(-calc.Weight(deriv2AtU), pointAtU));
            if (w != 0.0) deriv2AtU = calc.Mul(1.0 / w, v);
            else deriv2AtU = calc.Mul(1.0 / 1.0, v);
        }

        public void InitDeriv1()
        {
            // Buch S. 98
            // nur die Kontrollpunkte der 1. Ableitung berechnen, im Buch:
            // p = degree, r1==0, r2==n==knots.Length-degree-1, 
            if (poles.Length == 0) return;
            int n = poles.Length - 1;
            deriv1 = new T[n];
            int tmp = udegree;
            for (int i = 0; i <= n - 1; ++i)
            {
                int maxind = Math.Min(i + udegree + 1, uknots.Length - 1);
                if (uknots[maxind] == uknots[i + 1]) deriv1[i] = new T(); // 0,0,0
                else deriv1[i] = calc.Mul(udegree / (uknots[maxind] - uknots[i + 1]), calc.Sub(poles[i + 1], poles[i]));
            }
        }
        /// <summary>
        /// Calculates the control points of the derived curves up to maxderiv
        /// </summary>
        /// <param name="pls"></param>
        /// <param name="knots"></param>
        /// <param name="degree"></param>
        /// <param name="maxderiv"></param>
        /// <returns></returns>
        private T[,] CurveDerivCpts(Indexer<T> pls, double[] knots, int degree, int maxderiv)
        {   // Buch S. 98: P=pls (poles), p=degree, U=knots, d=maxderiv
            int numPoles = knots.Length - degree - 1;
            int r = numPoles - 1;
            int r1 = 0;
            int r2 = r;
            T[,] PK = new T[maxderiv + 1, numPoles];
            for (int i = 0; i <= r; i++)
            {
                PK[0, i] = pls[i];
            }
            for (int k = 1; k <= maxderiv; k++)
            {
                int tmp = Math.Max(0, degree - k + 1);
                for (int i = 0; i <= r - k; i++)
                {
                    PK[k, i] = calc.Mul(tmp / (knots[r1 + i + degree + 1] - knots[r1 + i + k]), calc.Sub(PK[k - 1, i + 1], PK[k - 1, i]));
                }
            }
            return PK;
        }
        private DerivsKL SurfaceDeriveCpts(int maxderiv)
        {   // Buch S. 114: n,m: Anzahl der Poles, p,q: u/vdegree, U,V: knots, P: poles, d: maxderiv, r1,s1: 0, r2,s2: n,m
            DerivsKL PKL = new Nurbs<T, C>.DerivsKL(this, maxderiv);
            int du = Math.Min(maxderiv, udegree);
            int dv = Math.Min(maxderiv, vdegree);
            int r = numUPoles - 1;
            int s = numVPoles - 1; // r1,s1==0, r2,s2==r,s
            for (int j = 0; j <= s; j++)
            {
                T[,] temp = CurveDerivCpts(new PoleRow(this, j, false), uknots, udegree, du);
                for (int k = 0; k <= du; k++)
                {
                    for (int i = 0; i <= r - k; i++)
                    {
                        PKL[k, 0, i, j] = temp[k, i];
                    }
                }
            }
            for (int k = 0; k <= du; k++)
            {
                for (int i = 0; i <= r - k; i++)
                {
                    int dd = Math.Min(maxderiv - k, dv);
                    T[,] temp = CurveDerivCpts(PKL.FixedI(k, 0, i), vknots, vdegree, dv);
                    for (int l = 0; l <= dv; l++)
                    {
                        for (int j = 0; j <= s - l; j++)
                        {
                            PKL[k, l, i, j] = temp[l, j];
                        }
                    }
                }
            }
            return PKL;
        }
        private T[] CurveDerivCptsU(int fixv)
        {
            int n = numUPoles - 1;
            T[] res = new T[n];
            int tmp = udegree;
            for (int i = 0; i <= n - 1; ++i)
            {
                int maxind = Math.Min(i + udegree + 1, uknots.Length - 1);
                res[i] = calc.Mul(udegree / (uknots[maxind] - uknots[i + 1]), calc.Sub(poles[ind(i + 1, fixv)], poles[ind(i, fixv)]));
            }
            return res;
        }
        private T[] CurveDerivCptsV(int fixu)
        {
            int n = numVPoles - 1;
            T[] res = new T[n];
            int tmp = vdegree;
            for (int i = 0; i <= n - 1; ++i)
            {
                int maxind = Math.Min(i + vdegree + 1, vknots.Length - 1);
                res[i] = calc.Mul(vdegree / (vknots[maxind] - vknots[i + 1]), calc.Sub(poles[ind(fixu, i + 1)], poles[ind(fixu, i)]));
            }
            return res;
        }
        private T[] derivs;
        public void InitDerivS()
        {
            // Buch S. 114
            // nur die Kontrollpunkte der 1. Ableitung berechnen, im Buch:
            // p = udegree, q = vdegree, r1==0, r2==n==numUPoles-1, s1,s2 entsprechend v, d==1
            // wir brauchen nur die Kontrollpunkte der 1. Ableitungen in u und in v
            // PKL[1][0][i][j] und PKL[0][1][i][j] sollen erzeugt werden
            // i: 0..numUpoles-1-1, j: 0..numVPoles-1 (im 1. Fall)
            int r = numUPoles - 1;
            int s = numVPoles - 1;
            // derivs = new Dictionary<tuple, T>();
            derivs = new T[numUPoles * numVPoles * 2]; // nicht alle besetzt, aber einfacher zu indizieren
            for (int j = 0; j <= s; ++j)
            {
                T[] tmp = CurveDerivCptsU(j);
                for (int i = 0; i <= r - 1; ++i)
                {
                    derivs[inds(true, i, j)] = tmp[i];
                }
            }
            for (int i = 0; i <= r; ++i)
            {
                T[] tmp = CurveDerivCptsV(i);
                for (int j = 0; j <= s - 1; ++j)
                {
                    derivs[inds(false, i, j)] = tmp[j];
                }
            }
        }
        public void InitDeriv2()
        {
            // Buch S. 98
            int d = 2;
            T[][] Ctpts = new T[d + 1][];
            Ctpts[0] = new T[poles.Length];
            for (int i = 0; i < poles.Length; ++i)
            {
                Ctpts[0][i] = poles[i];
            }
            for (int k = 1; k <= d; ++k)
            {
                int n = poles.Length - k;
                Ctpts[k] = new T[n];
                int tmp = udegree - k + 1;
                for (int i = 0; i <= n - 1; ++i)
                {
                    if (uknots[i + udegree + 1] == uknots[i + k]) Ctpts[k][i] = new T();
                    else Ctpts[k][i] = calc.Mul(tmp / (uknots[i + udegree + 1] - uknots[i + k]), calc.Sub(Ctpts[k - 1][i + 1], Ctpts[k - 1][i]));
                }
            }
            deriv2 = Ctpts[2]; // clonen?
        }
        public void ClearDeriv2()
        {
            deriv2 = null;
        }

        public T CurvePoint(double u)
        {
            int span;
            int n = uknots.Length - udegree - 1;
            span = FindSpanU(n, udegree, u);
            double[] N; // N sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsU(span, u, udegree, out N);
            T res = new T();
            for (int j = 0; j <= udegree; ++j)
            {
                res = calc.Add(res, calc.Mul(N[j], poles[span - udegree + j]));
                // res = res + N[j] * Poles[span - degree + j];
            }
            return res;
        }
        /// <summary>
        /// Writes a NURBS of a certain degree to a string that you can use as input for maxima. The poles are p0x, p0y,p0z,p0w,...,...p4w
        /// </summary>
        /// <param name="u"></param>
        /// <returns></returns>
        internal string CurvePointForMaxima(double u)
        {
            StringBuilder res = new StringBuilder();
            int span;
            int n = uknots.Length - udegree - 1;
            span = FindSpanU(n, udegree, u);
            string[] Nstr;
            BasisFunsString(span, u, udegree, out Nstr);
            string uknotstring = "[";
            for (int i = 0; i < uknots.Length; i++) uknotstring = uknotstring + uknots[i].ToString() + ",";
            uknotstring = uknotstring.Substring(0, uknotstring.Length - 1) + "]";
            for (int i = 0; i < Nstr.Length; i++)
            {
                res.AppendLine("N" + i.ToString() + "(u,span,uknots) := " + Nstr[i] + ";");
                // res.AppendLine("N" + i.ToString() + "(u," + (span + 1).ToString() + "," + uknotstring + ");");
            }
            string resstring = "0";
            for (int j = 0; j <= udegree; ++j)
            {
                resstring = "(" + resstring + ") + N" + j.ToString() + "(u," + (span + 1).ToString() + "," + uknotstring + ")*p" + (span - udegree + j).ToString() + "w";
            }
            // to get the rational bSpline, remove the "w(u):="  and "/w(u)"
            res.AppendLine("w(u):=" + resstring + ";");
            res.AppendLine("x(u):= (" + resstring.Replace('w', 'x') + ")/w(u);");
            res.AppendLine("y(u):= (" + resstring.Replace('w', 'y') + ")/w(u);");
            res.AppendLine("z(u):= (" + resstring.Replace('w', 'z') + ")/w(u);");
            res.AppendLine("w(u);");
            res.AppendLine("x(u);");
            res.AppendLine("y(u);");
            res.AppendLine("z(u);");

            return res.ToString();
        }

        public string[] CurvePointFormula(double u)
        {
            string[] Nstr;
            int n = uknots.Length - udegree - 1;
            int span = FindSpanU(n, udegree, u);
            BasisFunsString(span, u, udegree, out Nstr);
            string[] res = new string[Nstr.Length];
            for (int i = 0; i < res.Length; i++) res[i] = "0";
            for (int j = 0; j <= udegree; ++j)
            {
                // res = res + N[j] * Poles[span - degree + j];
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = "(" + res[i] + ")" + " + (" + Nstr[j] + ") * " + "p[span-degree+" + j.ToString() + ", " + i.ToString() + "]";
                }
            }
            return res;
        }
        public Polynom[] CurvePointPolynom(double u)
        {
            int span;
            int n = uknots.Length - udegree - 1;
            span = FindSpanU(n, udegree, u);
            Polynom[] N; // N sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsU(span, udegree, out N, false);
            Polynom[] res = new Polynom[calc.GetComponents(poles[0]).Length];
            for (int i = 0; i < res.Length; i++) res[i] = new Polynom(0.0, 1);
            for (int j = 0; j <= udegree; ++j)
            {
                // res = calc.Add(res, calc.Mul(N[j], poles[span - udegree + j]));
                // res = res + N[j] * Poles[span - degree + j];
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = res[i] + calc.GetComponents(poles[span - udegree + j])[i] * N[j];
                }
            }
            return res;
        }
        public T SurfacePoint(double u, double v)
        {
            int n = uknots.Length - udegree - 1;
            int uspan = FindSpanU(n, udegree, u);
            int m = vknots.Length - vdegree - 1;
            int vspan = FindSpanV(m, vdegree, v);
            double[] Nu; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsU(uspan, u, udegree, out Nu);
            double[] Nv; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsV(vspan, v, vdegree, out Nv);
            int uind = uspan - udegree;
            T res = new T();
            for (int l = 0; l <= vdegree; ++l)
            {
                T tmp = new T();
                int vind = vspan - vdegree + l;
                for (int k = 0; k <= udegree; ++k)
                {
                    tmp = calc.Add(tmp, calc.Mul(Nu[k], poles[ind(uind + k, vind)]));
                }
                res = calc.Add(res, calc.Mul(Nv[l], tmp));
            }
            return res;
        }
        internal Polynom[] SurfacePointPolynom(double u, double v)
        {   // wir brauchen keine rationalen Polynome, einfache Polynome reichen. Die Division in BasisFunsU/V sind immer konstante
            int n = uknots.Length - udegree - 1;
            int uspan = FindSpanU(n, udegree, u);
            int m = vknots.Length - vdegree - 1;
            int vspan = FindSpanV(m, vdegree, v);
            Polynom[] Nu; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsU(uspan, udegree, out Nu, true);
            Polynom[] Nv; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsV(vspan, vdegree, out Nv);
            int uind = uspan - udegree;
            Polynom[] res = new Polynom[calc.GetComponents(poles[0]).Length];
            for (int i = 0; i < res.Length; i++) res[i] = new Polynom(0.0, 2);
            for (int l = 0; l <= vdegree; ++l)
            {
                Polynom[] tmp = new Polynom[calc.GetComponents(poles[0]).Length];
                for (int i = 0; i < tmp.Length; i++) tmp[i] = new Polynom(0.0, 2);
                int vind = vspan - vdegree + l;
                for (int k = 0; k <= udegree; ++k)
                {
                    for (int i = 0; i < tmp.Length; i++)
                    {
                        tmp[i] = tmp[i] + calc.GetComponents(poles[ind(uind + k, vind)])[i] * Nu[k];
                    }
                }
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = res[i] + tmp[i] * Nv[l];
                }
            }
            return res;
        }
        /// <summary>
        /// Returns a nurbs curve of a surface with fixed u parameter. The type of the poles of the returned curve is the same 
        /// as the type of poles of the surface (rational/nonrational)
        /// </summary>
        /// <param name="u">Fixed u parameter</param>
        /// <returns>the nurbs curve</returns>
        public Nurbs<T, C> FixedU(double u)
        {
            T[] vpoles = new T[numVPoles];
            int n = uknots.Length - udegree - 1;
            int uspan = FindSpanU(n, udegree, u);
            double[] Nu; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsU(uspan, u, udegree, out Nu);
            int uind = uspan - udegree;
            for (int vind = 0; vind < vpoles.Length; ++vind)
            {
                T tmp = new T();
                for (int k = 0; k <= udegree; ++k)
                {
                    tmp = calc.Add(tmp, calc.Mul(Nu[k], poles[ind(uind + k, vind)]));
                }
                vpoles[vind] = tmp;
            }
            return new Nurbs<T, C>(vdegree, vpoles, (double[])vknots.Clone());
        }
        /// <summary>
        /// Returns a nurbs curve of a surface with fixed v parameter. The type of the poles of the returned curve is the same 
        /// as the type of poles of the surface (rational/nonrational)
        /// </summary>
        /// <param name="u">Fixed v parameter</param>
        /// <returns>the nurbs curve</returns>
        public Nurbs<T, C> FixedV(double v)
        {
            T[] upoles = new T[numUPoles];
            int n = vknots.Length - vdegree - 1;
            int vspan = FindSpanV(n, vdegree, v);
            double[] Nv; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsV(vspan, v, vdegree, out Nv);
            int vind = vspan - vdegree;
            for (int uind = 0; uind < upoles.Length; ++uind)
            {
                T tmp = new T();
                for (int k = 0; k <= vdegree; ++k)
                {
                    tmp = calc.Add(tmp, calc.Mul(Nv[k], poles[ind(uind, vind + k)]));
                }
                upoles[uind] = tmp;
            }
            return new Nurbs<T, C>(udegree, upoles, (double[])uknots.Clone());
        }
        public T[,] SurfaceDeriv(double u, double v, int maxderiv)
        {   // Buch Seite 115 kombiniert mit Seite 137
            // n,m: Anzahl der Poles, p,q: u/vdegree, U,V: knots, P: poles, d: maxderiv

            // Scheint nun zu funktionieren, die Sache mit maxderiv ist, dass ja eine quadratische Matrix
            // zurückgeliefert wird, die bei [0,0], den eigentlichen Punkt hat und and den andere Stellen [k,l]
            // die k-te Ableitung nach u und die l-te Ableitung nach v, in der Diagonalen ist das natürlich mehr
            // als maxderiv. Andernfalls müsste man ein Dreieck zurückliefern und die Laufindizes anders einschränken.
            T[,] SKL = new T[maxderiv + 1, maxderiv + 1];

            int du = Math.Min(maxderiv, udegree);
            int dv = Math.Min(maxderiv, vdegree);
            // die Ergebnis Initialisierung mit 0 kann man sich sparen, da alle T schon 0 sind
            //for (int k = udegree+1; k <= maxderiv; k++)
            //{
            //    for (int l = 0; l <= maxderiv-k; l++)
            //    {
            //        SKL[k, l] = new T();
            //    }    
            //}

            int n = uknots.Length - udegree - 1;
            int uspan = FindSpanU(n, udegree, u);
            int m = vknots.Length - vdegree - 1;
            int vspan = FindSpanV(m, vdegree, v);
            double[][] Nu, Nv;
            AllBasisFunsU(uspan, u, out Nu);
            AllBasisFunsV(vspan, v, out Nv);
            DerivsKL PKL = SurfaceDeriveCpts(maxderiv); // muss natürlich in einen cache

            int uind = uspan - udegree;
            int vind = vspan - vdegree;

            for (int k = 0; k <= du; k++)
            {
                int dd = Math.Min(maxderiv - k, dv);
                for (int l = 0; l <= dv; l++)
                {
                    for (int i = 0; i <= vdegree - l; i++)
                    {
                        T tmp = new T();
                        for (int j = 0; j <= udegree - k; j++)
                        {
                            tmp = calc.Add(tmp, calc.Mul(Nu[udegree - k][j], PKL[k, l, uind + j, vind + i]));
                        }
                        SKL[k, l] = calc.Add(SKL[k, l], calc.Mul(Nv[vdegree - l][i], tmp));
                    }
                }
            }

            // jetzt im Falle von Rational noch Normieren: S. 137, Aders, wders ist SKL
            if (calc.IsRational)
            {
                int[][] B = Bino(maxderiv);
                T[,] SKLR = new T[maxderiv + 1, maxderiv + 1]; // sicherheitshalber auf einem 2. Array arbeiten, notwendig?

                for (int k = 0; k <= maxderiv; k++)
                {
                    for (int l = 0; l <= maxderiv; l++)
                    {
                        T v1 = SKL[k, l]; // Original: v
                        calc.ClearWeight(ref v1);
                        for (int j = 1; j <= l; j++)
                        {
                            v1 = calc.Sub(v1, calc.Mul(B[l][j] * calc.Weight(SKL[0, j]), SKLR[k, l - j]));
                        }
                        for (int i = 1; i <= k; i++)
                        {
                            v1 = calc.Sub(v1, calc.Mul(B[k][i] * calc.Weight(SKL[i, 0]), SKLR[k - i, l]));
                            T v2 = new T();
                            for (int j = 1; j <= l; j++)
                            {
                                v2 = calc.Add(v2, calc.Mul(B[l][j] * calc.Weight(SKL[i, j]), SKLR[k - i, l - j]));
                            }
                            v1 = calc.Sub(v1, calc.Mul(B[k][i], v2));
                        }
                        SKLR[k, l] = calc.Mul(1.0 / calc.Weight(SKL[0, 0]), v1);
                    }
                }
                SKLR[0, 0] = SKL[0, 0];
                return SKLR;
            }

            return SKL; // nicht rational
        }
        static private int[][] Bino(int max)
        {
            int[][] res = new int[max + 1][];
            for (int i = 0; i <= max; ++i)
            {
                res[i] = new int[i + 1];
                for (int j = 0; j <= i; ++j)
                {   // i über j
                    if (i == 0) res[i][j] = 1;
                    else if (i == j || j == 0)
                    {
                        res[i][j] = 1;
                    }
                    else
                    {
                        res[i][j] = res[i - 1][j] + res[i - 1][j - 1];
                    }
                }
            }
            return res;
        }

        public void SurfaceDeriv1(double u, double v, out T pointAtUV, out T derivU, out T derivV)
        {   // Seite 137 bin sind alle 1
            int n = uknots.Length - udegree - 1;
            int uspan = FindSpanU(n, udegree, u);
            int m = vknots.Length - vdegree - 1;
            int vspan = FindSpanV(m, vdegree, v);
            double[] Nu; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsU(uspan, u, udegree, out Nu);
            double[] Nv; // n sollte mit stackalloc alokiert werden, da es nur lokal gebraucht wird
            BasisFunsV(vspan, v, vdegree, out Nv);
            double[] Nuu;
            BasisFunsU(uspan, u, udegree - 1, out Nuu);
            double[] Nvv;
            BasisFunsV(vspan, v, vdegree - 1, out Nvv);
            derivU = new T();
            derivV = new T();
            pointAtUV = new T();
            int uind = uspan - udegree;
            int vind = vspan - vdegree;
            // 0: k=0, l=0, der Punkt
            for (int i = 0; i <= vdegree - 0; ++i)
            {
                T tmp = new T();
                for (int j = 0; j <= udegree - 0; ++j)
                {
                    tmp = calc.Add(tmp, calc.Mul(Nu[j], poles[ind(uind + j, vind + i)]));
                }
                pointAtUV = calc.Add(pointAtUV, calc.Mul(Nv[i], tmp));
            }
            // 1: k=0, l=1, die Ableitung Richtung V

            for (int i = 0; i <= vdegree - 1; ++i)
            {
                T tmp = new T();
                for (int j = 0; j <= udegree - 0; ++j)
                {
                    tmp = calc.Add(tmp, calc.Mul(Nu[j], derivs[inds(false, uind + j, vind + i)]));
                }
                derivV = calc.Add(derivV, calc.Mul(Nvv[i], tmp));
            }
            // 2: k=1, l=0, die Ableitung Richtung U
            for (int i = 0; i <= vdegree - 0; ++i)
            {
                T tmp = new T();
                for (int j = 0; j <= udegree - 1; ++j)
                {
                    tmp = calc.Add(tmp, calc.Mul(Nuu[j], derivs[inds(true, uind + j, vind + i)]));
                }
                derivU = calc.Add(derivU, calc.Mul(Nv[i], tmp));
            }
            if (calc.IsRational)
            {
                derivU = RatCurveDerivs1(derivU, pointAtUV);
                derivV = RatCurveDerivs1(derivV, pointAtUV);
            }
        }
        public void CurveDeriv1(double u, out T pointAtU, out T derivAtU)
        {
            // implementiert im Buch S. 99 für d==1, also nur 1. Ableitung
            // die unveränderlichen PK1 müssen natürlich zum BSpline objekt, damit sie nicht immer neu berechnet werden müssen
            // für NURBS mit Weight, (also rationale) muss noch mit RatCurveDerivs1 nachgebessert werden
            if (deriv1 == null) InitDeriv1();
            int n = uknots.Length - udegree - 1;
            int span = FindSpanU(n, udegree, u);
            double[] N;
            BasisFunsU(span, u, udegree, out N);
            pointAtU = new T();
            for (int j = 0; j <= udegree; ++j)
            {
                pointAtU = calc.Add(pointAtU, calc.Mul(N[j], poles[span - udegree + j]));
            }

            BasisFunsU(span, u, udegree - 1, out N);
            derivAtU = new T();
            for (int j = 0; j <= udegree - 1; ++j)
            {
                derivAtU = calc.Add(derivAtU, calc.Mul(N[j], deriv1[j + span - udegree]));
            }
            if (calc.IsRational)
            {
                derivAtU = RatCurveDerivs1(derivAtU, pointAtU);
            }
        }
        public void CurveDeriv2(double u, out T pointAtU, out T deriv1AtU, out T deriv2AtU)
        {
            bool clear = false;
            if (deriv2 == null)
            {
                InitDeriv2();
                clear = true;
            }
            int d = 2;
            int n = uknots.Length - udegree - 1;
            int span = FindSpanU(n, udegree, u);
            double[][] N;
            NBasisFuns(span, 3, u, out N);
            int du = Math.Min(d, udegree);
            //for (int k = 0; k <= du; ++k)
            //{
            //    deriv[k] = new T(); // (0,0,0)
            //    for (int j = 0; j <= degree - k; ++j)
            //    {
            //        deriv[k].Add(N[degree - k][j], PK[k][j + span - degree]);
            //    }
            //}
            // k==0:
            pointAtU = new T(); // (0,0,0)
            for (int j = 0; j <= udegree - 0; ++j)
            {
                pointAtU = calc.Add(pointAtU, calc.Mul(N[udegree - 0][j], poles[j + span - udegree]));
            }
            // k==1:
            deriv1AtU = new T(); // (0,0,0)
            for (int j = 0; j <= udegree - 1; ++j)
            {
                deriv1AtU = calc.Add(deriv1AtU, calc.Mul(N[udegree - 1][j], deriv1[j + span - udegree]));
            }
            // k==2:
            deriv2AtU = new T(); // (0,0,0)
            for (int j = 0; j <= udegree - 2; ++j)
            {
                deriv2AtU = calc.Add(deriv2AtU, calc.Mul(N[udegree - 2][j], deriv2[j + span - udegree]));
            }
            if (calc.IsRational)
            {
                RatCurveDerivs2(pointAtU, ref deriv1AtU, ref deriv2AtU);
            }
            if (clear) ClearDeriv2();
        }
        public int FindIndex(double u)
        {   // liefert den Index für den Parameter
            int k = FindSpanU(uknots.Length - udegree - 1, udegree, u);
            return k; // evtl noch Verbesserung wie in CurveKnotIns
        }
        public int CurveKnotIns(double u, int r, out double[] newknots, out T[] newpoles)
        {
            // Buch S. 151
            // p = degree
            // s ist die Anzahl wie oft der Knoten schon drin ist (links von k), kann man berechnen
            // r ist die Anzahl wie oft er noch rein soll (als Parameter: wie oft er drin sein soll, wird ggf.
            // runtergerechnet, wenn er schon drin ist)
            // das Ergebnis ist der Index, an dem u eingefügt wurde und wo somit die knots und poles aufzuteilen
            // sind, wenn es denn zum splitten verwendet wird.
            int np = poles.Length - 1; // könnte auch "knots.Length - degree - 1" sein, oder?
            int k = FindSpanU(uknots.Length - udegree, udegree, u);
            if (u != uknots[k] && u - uknots[k] < (uknots[uknots.Length - 1] - uknots[0]) * 1e-8)
            {   // hier wird geschummelt: wenn fast exakt auf einem Knoten eingefügt werden soll, so wird
                // der knoten manipuliert und ein bisschen zurechtgerückt
                // wozu wird das gebraucht? wegen "prova filo.igs" ausgeschaltet
                // wieder eingeschaltet wg. trimmen bei PN7448_S1188.1_Einsatz DS.stp
                int n = 0;
                while (k - n >= 0 && uknots[k] == uknots[k - n]) ++n;
                for (int i = 0; i < n; i++) uknots[k - i] = u;
            }
            int s = 0;
            while (uknots[k - s] == u && s < k)
            {
                ++s;
                --r;
            }
            if (r <= 0)
            {
                newpoles = (T[])poles.Clone();
                newknots = (double[])uknots.Clone();
                return k - s + r; // +r eingefügt wg. periodischen (9.2.11)
            }
            // k = k - s; // diese zeile eingefügt, das gab sonst undefinierte double
            // vorige Zeile wieder ausgehängt: gibt bei Trim wenn u ein bestehender Knoten ist sonst effektiv einen Fehler
            // welches war der Fall wo das gebraucht wurde?
            // vermutlich könnte man sich die ganze Routine sparen, wenn r==0
            int mp = np + udegree + 1;
            int nq = np + r;
            newknots = new double[mp + r + 1];
            newpoles = new T[poles.Length + r];
            T[] RW = new T[udegree + 1];
            for (int i = 0; i <= k; ++i) newknots[i] = uknots[i];
            for (int i = 1; i <= r; ++i) newknots[k + i] = u;
            for (int i = k + 1; i <= mp; ++i) newknots[i + r] = uknots[i];

            for (int i = 0; i <= k - udegree; ++i) newpoles[i] = poles[i];
            for (int i = k - s; i <= np; ++i) newpoles[i + r] = poles[i];
            // for (int i = 0; i <= udegree - s; ++i) RW[i] = poles[k - udegree + i];
            for (int i = 0; i <= udegree; ++i) RW[i] = poles[Math.Min(k - udegree + i, poles.Length - 1)]; // introduced Math.Min for nurbs where the last multiplicity is less than or equal degree
            int L = 0;
            for (int j = 1; j <= r; ++j)
            {
                L = k - udegree + j;
                for (int i = 0; i <= udegree - j - s; ++i)
                {
                    double alpha = (u - uknots[L + i]) / (uknots[i + k + 1] - uknots[L + i]);
                    T tmp = calc.Mul(alpha, RW[i + 1]);
                    tmp = calc.Add(tmp, calc.Mul(1.0 - alpha, RW[i])); // auch w stimmt so!
                    RW[i] = tmp;
                    // RW[i] = alpha * RW[i + 1] + (1.0 - alpha) * RW[i];
                }
                newpoles[L] = RW[0];
                newpoles[k + r - j - s] = RW[udegree - j - s];
            }
            for (int i = L + 1; i < k - s; ++i)
            {
                newpoles[i] = RW[i - L];
            }
            return k - s; // bei Trim wenn auf einen bestehenden U-Wert getrimmt wird eingeführt vorher war nur "k"
        }
        public int CurveKnotInsAt(int ki, int r, out double[] newknots, out T[] newpoles)
        {
            // Buch S. 151
            // p = degree
            // s ist die Anzahl wie oft der Knoten schon drin ist (links von k), kann man berechnen
            // r ist die Anzahl wie oft er noch rein soll (als Parameter: wie oft er drin sein soll, wird ggf.
            // runtergerechnet, wenn er schon drin ist)
            // das Ergebnis ist der Index, an dem u eingefügt wurde und wo somit die knots und poles aufzuteilen
            // sind, wenn es denn zum splitten verwendet wird.
            int np = poles.Length - 1; // könnte auch "knots.Length - degree - 1" sein, oder?
            int k = ki;
            int s = 0;
            while (uknots[k - s] == uknots[ki] && s < k)
            {
                ++s;
                --r;
            }
            if (r <= 0)
            {
                newpoles = (T[])poles.Clone();
                newknots = (double[])uknots.Clone();
                return k - s + r; // +r eingefügt wg. periodischen (9.2.11)
            }
            // k = k - s; // diese zeile eingefügt, das gab sonst undefinierte double
            // vorige Zeile wieder ausgehängt: gibt bei Trim wenn u ein bestehender Knoten ist sonst effektiv einen Fehler
            // welches war der Fall wo das gebraucht wurde?
            // vermutlich könnte man sich die ganze Routine sparen, wenn r==0
            int mp = np + udegree + 1;
            int nq = np + r;
            newknots = new double[mp + r + 1];
            newpoles = new T[poles.Length + r];
            T[] RW = new T[udegree + 1];
            for (int i = 0; i <= k; ++i) newknots[i] = uknots[i];
            for (int i = 1; i <= r; ++i) newknots[k + i] = uknots[ki];
            for (int i = k + 1; i <= mp; ++i) newknots[i + r] = uknots[i];

            for (int i = 0; i <= k - udegree; ++i) newpoles[i] = poles[i];
            for (int i = k - s; i <= np; ++i) newpoles[i + r] = poles[i];
            // for (int i = 0; i <= udegree - s; ++i) RW[i] = poles[k - udegree + i];
            for (int i = 0; i <= udegree; ++i) RW[i] = poles[k - udegree + i];
            int L = 0;
            for (int j = 1; j <= r; ++j)
            {
                L = k - udegree + j;
                for (int i = 0; i <= udegree - j - s; ++i)
                {
                    double alpha = (uknots[ki] - uknots[L + i]) / (uknots[i + k + 1] - uknots[L + i]);
                    T tmp = calc.Mul(alpha, RW[i + 1]);
                    tmp = calc.Add(tmp, calc.Mul(1.0 - alpha, RW[i])); // auch w stimmt so!
                    RW[i] = tmp;
                    // RW[i] = alpha * RW[i + 1] + (1.0 - alpha) * RW[i];
                }
                newpoles[L] = RW[0];
                newpoles[k + r - j - s] = RW[udegree - j - s];
            }
            for (int i = L + 1; i < k - s; ++i)
            {
                newpoles[i] = RW[i - L];
            }
            return k - s; // bei Trim wenn auf einen bestehenden U-Wert getrimmt wird eingeführt vorher war nur "k"
        }
        public T[] Poles
        {
            get
            {
                return (T[])poles.Clone();
            }
        }
        public double[] UKnots
        {
            get
            {
                return (double[])uknots.Clone();
            }
        }
        public double[] VKnots
        {
            get
            {
                return (double[])vknots.Clone();
            }
        }
        public int UKnotNum
        {
            get
            {
                return uknots.Length;
            }
        }
        public int UDegree
        {
            get
            {
                return udegree;
            }
        }
        public int VDegree
        {
            get
            {
                return vdegree;
            }
        }
        /// <summary>
        /// Return a clone with modified poles. degree and knot remain unmodified.
        /// </summary>
        /// <param name="poles">a set of poles, must be the same size as this.poles</param>
        /// <returns>the clone</returns>
        public Nurbs<T, C> Clone(T[] poles)
        {
            return new Nurbs<T, C>(udegree, poles, uknots);
        }
        public double[] FindXNullDeg3(int span, double[] x)
        {   // geht natürlich nur für nicht rationale, oder?
            // x muss bei span-3 beginnen und 4 Werte haben x[0] = pole[span-3].x u.s.w.
            // span zwischen udegree und uknots.Length - udegree - 1;
            double uum3 = uknots[span - 3] * uknots[span - 3];
            double uum2 = uknots[span - 2] * uknots[span - 2];
            double uum1 = uknots[span - 1] * uknots[span - 1];
            double uu0 = uknots[span] * uknots[span];
            double uu1 = uknots[span + 1] * uknots[span + 1];
            double uu2 = uknots[span + 2] * uknots[span + 2];
            double uu3 = uknots[span + 3] * uknots[span + 3];
            double um3 = uknots[span - 3] * uknots[span - 2];
            double um2 = uknots[span - 2] * uknots[span - 1];
            double um1 = uknots[span - 1] * uknots[span];
            double u0 = uknots[span] * uknots[span + 1];
            double u1 = uknots[span + 1] * uknots[span + 2];
            double u2 = uknots[span + 2] * uknots[span + 3];
            double a = (x[3] * uknots[span + 1] * uum1 - x[2] * uknots[span + 1] * uum1 - x[3] * uknots[span - 2] * uum1 + x[2] * uknots[span - 2] * uum1 + x[1] * uknots[span + 3] * uu2 - x[0] * uknots[span + 3] * uu2 - x[1] * uknots[span] * uu2 + x[0] * uknots[span] * uu2 - x[2] * uknots[span + 3] * uu1 + x[1] * uknots[span + 3] * uu1 + x[3] * uknots[span + 2] * uu1 - x[2] * uknots[span + 2] * uu1 + x[2] * uknots[span] * uu1 - x[1] * uknots[span] * uu1 - x[3] * uknots[span - 1] * uu1 + x[2] * uknots[span - 1] * uu1 + x[1] * uknots[span + 2] * uu0 - x[0] * uknots[span + 2] * uu0 - x[2] * uknots[span + 1] * uu0 + x[1] * uknots[span + 1] * uu0 - x[1] * uknots[span - 1] * uu0 + x[0] * uknots[span - 1] * uu0 + x[2] * uknots[span - 2] * uu0 - x[1] * uknots[span - 2] * uu0 - x[2] * uknots[span + 3] * um2 + x[1] * uknots[span + 3] * um2 + x[3] * uknots[span + 2] * um2 - x[2] * uknots[span + 2] * um2 + x[3] * uknots[span + 1] * um2 - x[2] * uknots[span + 1] * um2 + x[2] * uknots[span] * um2 - x[1] * uknots[span] * um2 + x[1] * uknots[span + 3] * um1 - x[0] * uknots[span + 3] * um1 + x[1] * uknots[span + 2] * um1 - x[0] * uknots[span + 2] * um1 - x[2] * uknots[span + 1] * um1 + x[1] * uknots[span + 1] * um1 - x[1] * uknots[span] * u2 + x[0] * uknots[span] * u2 - x[1] * uknots[span - 1] * u2 + x[0] * uknots[span - 1] * u2 + x[2] * uknots[span - 2] * u2 - x[1] * uknots[span - 2] * u2 - x[2] * uknots[span + 3] * u1 + x[1] * uknots[span + 3] * u1 - x[3] * uknots[span - 1] * u1 + x[2] * uknots[span - 1] * u1 - x[3] * uknots[span - 2] * u1 + x[2] * uknots[span - 2] * u1 + x[2] * uknots[span + 3] * u0 - x[1] * uknots[span + 3] * u0 + x[2] * uknots[span + 2] * u0 - x[1] * uknots[span + 2] * u0 - x[2] * uknots[span - 2] * u0 + x[1] * uknots[span - 2] * u0 + x[2] * uknots[span - 1] * uknots[span + 1] * uknots[span + 3] - x[1] * uknots[span - 1] * uknots[span + 1] * uknots[span + 3] + x[2] * uknots[span - 2] * uknots[span + 1] * uknots[span + 3] - x[1] * uknots[span - 2] * uknots[span + 1] * uknots[span + 3] - x[2] * uknots[span - 2] * uknots[span] * uknots[span + 3] + x[1] * uknots[span - 2] * uknots[span] * uknots[span + 3] - x[2] * uknots[span - 2] * uknots[span] * uknots[span + 2] + x[1] * uknots[span - 2] * uknots[span] * uknots[span + 2]) / (uu0 * uu1 * uum1 + u2 * uu1 * uum1 - uknots[span] * uknots[span + 3] * uu1 * uum1 - uknots[span] * uknots[span + 2] * uu1 * uum1 + u1 * uu0 * uum1 - u0 * uu0 * uum1 + uknots[span + 1] * uknots[span + 3] * uu0 * uum1 - uknots[span - 2] * uknots[span + 3] * uu0 * uum1 - uknots[span - 2] * uknots[span + 2] * uu0 * uum1 - uknots[span - 2] * uknots[span + 1] * uu0 * uum1 + uknots[span - 2] * uknots[span] * uu0 * uum1 - u0 * u2 * uum1 + uknots[span - 2] * uknots[span] * u2 * uum1 - uknots[span - 2] * uknots[span + 3] * u1 * uum1 + uknots[span - 2] * uknots[span + 3] * u0 * uum1 + uknots[span - 2] * uknots[span + 2] * u0 * uum1 + uu0 * uu1 * uu2 + um1 * uu1 * uu2 - u0 * uu1 * uu2 + uknots[span + 1] * uknots[span + 3] * uu1 * uu2 - uknots[span] * uknots[span + 3] * uu1 * uu2 - uknots[span - 1] * uknots[span + 3] * uu1 * uu2 - uknots[span - 2] * uknots[span + 3] * uu1 * uu2 + uknots[span - 2] * uknots[span] * uu1 * uu2 + um2 * uu0 * uu2 - uknots[span - 1] * uknots[span + 1] * uu0 * uu2 - uknots[span - 2] * uknots[span + 1] * uu0 * uu2 - u0 * um2 * uu2 + uknots[span + 1] * uknots[span + 3] * um2 * uu2 - uknots[span] * uknots[span + 3] * um2 * uu2 + uknots[span + 1] * uknots[span + 3] * um1 * uu2 + uknots[span - 2] * uknots[span + 3] * u0 * uu2 + um2 * uu0 * uu1 + um1 * uu0 * uu1 + u2 * uu0 * uu1 + u1 * uu0 * uu1 - uknots[span - 1] * uknots[span + 3] * uu0 * uu1 - uknots[span] * uknots[span + 2] * uu0 * uu1 - 2 * uknots[span - 1] * uknots[span + 2] * uu0 * uu1 - uknots[span - 2] * uknots[span + 2] * uu0 * uu1 - uknots[span - 1] * uknots[span + 1] * uu0 * uu1 + u2 * um2 * uu1 - uknots[span] * uknots[span + 3] * um2 * uu1 - uknots[span] * uknots[span + 2] * um2 * uu1 + 2 * u2 * um1 * uu1 + u1 * um1 * uu1 + uknots[span + 1] * uknots[span + 3] * um1 * uu1 - u0 * u2 * uu1 + uknots[span - 2] * uknots[span] * u2 * uu1 - uknots[span - 1] * uknots[span + 3] * u1 * uu1 + u2 * um2 * uu0 + 2 * u1 * um2 * uu0 - u0 * um2 * uu0 + uknots[span + 1] * uknots[span + 3] * um2 * uu0 - uknots[span] * uknots[span + 2] * um2 * uu0 + u1 * um1 * uu0 - uknots[span - 1] * uknots[span + 3] * u1 * uu0 - uknots[span - 2] * uknots[span + 3] * u1 * uu0 + uknots[span - 2] * uknots[span + 2] * u0 * uu0 - 2 * u0 * u2 * um2);
            double b = -3 * (x[3] * u0 * uum1 - x[2] * u0 * uum1 - x[3] * uknots[span - 2] * uknots[span] * uum1 + x[2] * uknots[span - 2] * uknots[span] * uum1 - x[1] * u0 * uu2 + x[0] * u0 * uu2 + x[1] * uknots[span + 1] * uknots[span + 3] * uu2 - x[0] * uknots[span + 1] * uknots[span + 3] * uu2 - x[3] * um1 * uu1 + x[2] * um1 * uu1 - x[2] * u2 * uu1 + x[1] * u2 * uu1 + x[3] * uknots[span] * uknots[span + 2] * uu1 - x[1] * uknots[span] * uknots[span + 2] * uu1 + x[2] * um2 * uu0 - x[1] * um2 * uu0 + x[1] * u1 * uu0 - x[0] * u1 * uu0 - x[2] * uknots[span - 1] * uknots[span + 1] * uu0 + x[0] * uknots[span - 1] * uknots[span + 1] * uu0 + x[3] * u0 * um2 - x[2] * u0 * um2 - x[2] * uknots[span] * uknots[span + 3] * um2 + x[1] * uknots[span] * uknots[span + 3] * um2 + x[3] * uknots[span] * uknots[span + 2] * um2 - x[2] * uknots[span] * uknots[span + 2] * um2 - x[3] * u1 * um1 + x[2] * u1 * um1 + x[1] * u1 * um1 - x[0] * u1 * um1 + x[2] * uknots[span + 1] * uknots[span + 3] * um1 - x[0] * uknots[span + 1] * uknots[span + 3] * um1 - x[1] * u0 * u2 + x[0] * u0 * u2 - x[1] * uknots[span - 1] * uknots[span + 3] * u1 + x[0] * uknots[span - 1] * uknots[span + 3] * u1 + x[2] * uknots[span - 2] * uknots[span + 3] * u1 - x[1] * uknots[span - 2] * uknots[span + 3] * u1 - x[3] * uknots[span - 2] * uknots[span + 2] * u0 + x[1] * uknots[span - 2] * uknots[span + 2] * u0) / (uu0 * uu1 * uum1 + u2 * uu1 * uum1 - uknots[span] * uknots[span + 3] * uu1 * uum1 - uknots[span] * uknots[span + 2] * uu1 * uum1 + u1 * uu0 * uum1 - u0 * uu0 * uum1 + uknots[span + 1] * uknots[span + 3] * uu0 * uum1 - uknots[span - 2] * uknots[span + 3] * uu0 * uum1 - uknots[span - 2] * uknots[span + 2] * uu0 * uum1 - uknots[span - 2] * uknots[span + 1] * uu0 * uum1 + uknots[span - 2] * uknots[span] * uu0 * uum1 - u0 * u2 * uum1 + uknots[span - 2] * uknots[span] * u2 * uum1 - uknots[span - 2] * uknots[span + 3] * u1 * uum1 + uknots[span - 2] * uknots[span + 3] * u0 * uum1 + uknots[span - 2] * uknots[span + 2] * u0 * uum1 + uu0 * uu1 * uu2 + um1 * uu1 * uu2 - u0 * uu1 * uu2 + uknots[span + 1] * uknots[span + 3] * uu1 * uu2 - uknots[span] * uknots[span + 3] * uu1 * uu2 - uknots[span - 1] * uknots[span + 3] * uu1 * uu2 - uknots[span - 2] * uknots[span + 3] * uu1 * uu2 + uknots[span - 2] * uknots[span] * uu1 * uu2 + um2 * uu0 * uu2 - uknots[span - 1] * uknots[span + 1] * uu0 * uu2 - uknots[span - 2] * uknots[span + 1] * uu0 * uu2 - u0 * um2 * uu2 + uknots[span + 1] * uknots[span + 3] * um2 * uu2 - uknots[span] * uknots[span + 3] * um2 * uu2 + uknots[span + 1] * uknots[span + 3] * um1 * uu2 + uknots[span - 2] * uknots[span + 3] * u0 * uu2 + um2 * uu0 * uu1 + um1 * uu0 * uu1 + u2 * uu0 * uu1 + u1 * uu0 * uu1 - uknots[span - 1] * uknots[span + 3] * uu0 * uu1 - uknots[span] * uknots[span + 2] * uu0 * uu1 - 2 * uknots[span - 1] * uknots[span + 2] * uu0 * uu1 - uknots[span - 2] * uknots[span + 2] * uu0 * uu1 - uknots[span - 1] * uknots[span + 1] * uu0 * uu1 + u2 * um2 * uu1 - uknots[span] * uknots[span + 3] * um2 * uu1 - uknots[span] * uknots[span + 2] * um2 * uu1 + 2 * u2 * um1 * uu1 + u1 * um1 * uu1 + uknots[span + 1] * uknots[span + 3] * um1 * uu1 - u0 * u2 * uu1 + uknots[span - 2] * uknots[span] * u2 * uu1 - uknots[span - 1] * uknots[span + 3] * u1 * uu1 + u2 * um2 * uu0 + 2 * u1 * um2 * uu0 - u0 * um2 * uu0 + uknots[span + 1] * uknots[span + 3] * um2 * uu0 - uknots[span] * uknots[span + 2] * um2 * uu0 + u1 * um1 * uu0 - uknots[span - 1] * uknots[span + 3] * u1 * uu0 - uknots[span - 2] * uknots[span + 3] * u1 * uu0 + uknots[span - 2] * uknots[span + 2] * u0 * uu0 - 2 * u0 * u2 * um2);
            double c = 3 * (x[3] * uknots[span + 1] * uu0 * uum1 - x[2] * uknots[span + 1] * uu0 * uum1 - x[3] * uknots[span - 2] * uu0 * uum1 + x[2] * uknots[span - 2] * uu0 * uum1 + x[1] * uknots[span + 3] * uu1 * uu2 - x[0] * uknots[span + 3] * uu1 * uu2 - x[1] * uknots[span] * uu1 * uu2 + x[0] * uknots[span] * uu1 * uu2 + x[3] * uknots[span + 2] * uu0 * uu1 - x[0] * uknots[span + 2] * uu0 * uu1 - x[3] * uknots[span - 1] * uu0 * uu1 + x[0] * uknots[span - 1] * uu0 * uu1 + x[2] * uknots[span + 3] * um1 * uu1 - x[0] * uknots[span + 3] * um1 * uu1 + x[2] * uknots[span + 2] * um1 * uu1 - x[0] * uknots[span + 2] * um1 * uu1 - x[2] * uknots[span] * u2 * uu1 + x[0] * uknots[span] * u2 * uu1 - x[2] * uknots[span - 1] * u2 * uu1 + x[0] * uknots[span - 1] * u2 * uu1 + x[3] * uknots[span + 2] * um2 * uu0 - x[1] * uknots[span + 2] * um2 * uu0 + x[3] * uknots[span + 1] * um2 * uu0 - x[1] * uknots[span + 1] * um2 * uu0 - x[3] * uknots[span - 1] * u1 * uu0 + x[1] * uknots[span - 1] * u1 * uu0 - x[3] * uknots[span - 2] * u1 * uu0 + x[1] * uknots[span - 2] * u1 * uu0 - x[2] * uknots[span] * u2 * um2 + x[1] * uknots[span] * u2 * um2 + x[2] * uknots[span + 3] * u1 * um2 - x[1] * uknots[span + 3] * u1 * um2 - x[2] * uknots[span + 3] * u0 * um2 + x[1] * uknots[span + 3] * u0 * um2 - x[2] * uknots[span + 2] * u0 * um2 + x[1] * uknots[span + 2] * u0 * um2 + x[2] * uknots[span + 3] * u1 * um1 - x[1] * uknots[span + 3] * u1 * um1 + x[2] * uknots[span - 2] * u0 * u2 - x[1] * uknots[span - 2] * u0 * u2) / (uu0 * uu1 * uum1 + u2 * uu1 * uum1 - uknots[span] * uknots[span + 3] * uu1 * uum1 - uknots[span] * uknots[span + 2] * uu1 * uum1 + u1 * uu0 * uum1 - u0 * uu0 * uum1 + uknots[span + 1] * uknots[span + 3] * uu0 * uum1 - uknots[span - 2] * uknots[span + 3] * uu0 * uum1 - uknots[span - 2] * uknots[span + 2] * uu0 * uum1 - uknots[span - 2] * uknots[span + 1] * uu0 * uum1 + uknots[span - 2] * uknots[span] * uu0 * uum1 - u0 * u2 * uum1 + uknots[span - 2] * uknots[span] * u2 * uum1 - uknots[span - 2] * uknots[span + 3] * u1 * uum1 + uknots[span - 2] * uknots[span + 3] * u0 * uum1 + uknots[span - 2] * uknots[span + 2] * u0 * uum1 + uu0 * uu1 * uu2 + um1 * uu1 * uu2 - u0 * uu1 * uu2 + uknots[span + 1] * uknots[span + 3] * uu1 * uu2 - uknots[span] * uknots[span + 3] * uu1 * uu2 - uknots[span - 1] * uknots[span + 3] * uu1 * uu2 - uknots[span - 2] * uknots[span + 3] * uu1 * uu2 + uknots[span - 2] * uknots[span] * uu1 * uu2 + um2 * uu0 * uu2 - uknots[span - 1] * uknots[span + 1] * uu0 * uu2 - uknots[span - 2] * uknots[span + 1] * uu0 * uu2 - u0 * um2 * uu2 + uknots[span + 1] * uknots[span + 3] * um2 * uu2 - uknots[span] * uknots[span + 3] * um2 * uu2 + uknots[span + 1] * uknots[span + 3] * um1 * uu2 + uknots[span - 2] * uknots[span + 3] * u0 * uu2 + um2 * uu0 * uu1 + um1 * uu0 * uu1 + u2 * uu0 * uu1 + u1 * uu0 * uu1 - uknots[span - 1] * uknots[span + 3] * uu0 * uu1 - uknots[span] * uknots[span + 2] * uu0 * uu1 - 2 * uknots[span - 1] * uknots[span + 2] * uu0 * uu1 - uknots[span - 2] * uknots[span + 2] * uu0 * uu1 - uknots[span - 1] * uknots[span + 1] * uu0 * uu1 + u2 * um2 * uu1 - uknots[span] * uknots[span + 3] * um2 * uu1 - uknots[span] * uknots[span + 2] * um2 * uu1 + 2 * u2 * um1 * uu1 + u1 * um1 * uu1 + uknots[span + 1] * uknots[span + 3] * um1 * uu1 - u0 * u2 * uu1 + uknots[span - 2] * uknots[span] * u2 * uu1 - uknots[span - 1] * uknots[span + 3] * u1 * uu1 + u2 * um2 * uu0 + 2 * u1 * um2 * uu0 - u0 * um2 * uu0 + uknots[span + 1] * uknots[span + 3] * um2 * uu0 - uknots[span] * uknots[span + 2] * um2 * uu0 + u1 * um1 * uu0 - uknots[span - 1] * uknots[span + 3] * u1 * uu0 - uknots[span - 2] * uknots[span + 3] * u1 * uu0 + uknots[span - 2] * uknots[span + 2] * u0 * uu0 - 2 * u0 * u2 * um2);
            double d = (x[2] * uu0 * uu1 * uum1 + x[2] * u2 * uu1 * uum1 - x[2] * uknots[span] * uknots[span + 3] * uu1 * uum1 - x[2] * uknots[span] * uknots[span + 2] * uu1 * uum1 + x[2] * u1 * uu0 * uum1 - x[3] * u0 * uu0 * uum1 + x[2] * uknots[span + 1] * uknots[span + 3] * uu0 * uum1 - x[2] * uknots[span - 2] * uknots[span + 3] * uu0 * uum1 - x[2] * uknots[span - 2] * uknots[span + 2] * uu0 * uum1 - x[2] * uknots[span - 2] * uknots[span + 1] * uu0 * uum1 + x[3] * uknots[span - 2] * uknots[span] * uu0 * uum1 - x[2] * u0 * u2 * uum1 + x[2] * uknots[span - 2] * uknots[span] * u2 * uum1 - x[2] * uknots[span - 2] * uknots[span + 3] * u1 * uum1 + x[2] * uknots[span - 2] * uknots[span + 3] * u0 * uum1 + x[2] * uknots[span - 2] * uknots[span + 2] * u0 * uum1 + x[1] * uu0 * uu1 * uu2 + x[1] * um1 * uu1 * uu2 - x[0] * u0 * uu1 * uu2 + x[0] * uknots[span + 1] * uknots[span + 3] * uu1 * uu2 - x[1] * uknots[span] * uknots[span + 3] * uu1 * uu2 - x[1] * uknots[span - 1] * uknots[span + 3] * uu1 * uu2 - x[1] * uknots[span - 2] * uknots[span + 3] * uu1 * uu2 + x[1] * uknots[span - 2] * uknots[span] * uu1 * uu2 + x[1] * um2 * uu0 * uu2 - x[1] * uknots[span - 1] * uknots[span + 1] * uu0 * uu2 - x[1] * uknots[span - 2] * uknots[span + 1] * uu0 * uu2 - x[1] * u0 * um2 * uu2 + x[1] * uknots[span + 1] * uknots[span + 3] * um2 * uu2 - x[1] * uknots[span] * uknots[span + 3] * um2 * uu2 + x[1] * uknots[span + 1] * uknots[span + 3] * um1 * uu2 + x[1] * uknots[span - 2] * uknots[span + 3] * u0 * uu2 + x[1] * um2 * uu0 * uu1 + x[3] * um1 * uu0 * uu1 + x[2] * u2 * uu0 * uu1 + x[0] * u1 * uu0 * uu1 - x[2] * uknots[span - 1] * uknots[span + 3] * uu0 * uu1 - x[3] * uknots[span] * uknots[span + 2] * uu0 * uu1 - x[2] * uknots[span - 1] * uknots[span + 2] * uu0 * uu1 - x[1] * uknots[span - 1] * uknots[span + 2] * uu0 * uu1 - x[1] * uknots[span - 2] * uknots[span + 2] * uu0 * uu1 - x[0] * uknots[span - 1] * uknots[span + 1] * uu0 * uu1 + x[1] * u2 * um2 * uu1 - x[1] * uknots[span] * uknots[span + 3] * um2 * uu1 - x[1] * uknots[span] * uknots[span + 2] * um2 * uu1 + x[2] * u2 * um1 * uu1 + x[1] * u2 * um1 * uu1 + x[0] * u1 * um1 * uu1 + x[0] * uknots[span + 1] * uknots[span + 3] * um1 * uu1 - x[0] * u0 * u2 * uu1 + x[1] * uknots[span - 2] * uknots[span] * u2 * uu1 - x[0] * uknots[span - 1] * uknots[span + 3] * u1 * uu1 + x[2] * u2 * um2 * uu0 + x[2] * u1 * um2 * uu0 + x[1] * u1 * um2 * uu0 - x[3] * u0 * um2 * uu0 + x[2] * uknots[span + 1] * uknots[span + 3] * um2 * uu0 - x[3] * uknots[span] * uknots[span + 2] * um2 * uu0 + x[3] * u1 * um1 * uu0 - x[2] * uknots[span - 1] * uknots[span + 3] * u1 * uu0 - x[2] * uknots[span - 2] * uknots[span + 3] * u1 * uu0 + x[3] * uknots[span - 2] * uknots[span + 2] * u0 * uu0 - x[2] * u0 * u2 * um2 - x[1] * u0 * u2 * um2) / (uu0 * uu1 * uum1 + u2 * uu1 * uum1 - uknots[span] * uknots[span + 3] * uu1 * uum1 - uknots[span] * uknots[span + 2] * uu1 * uum1 + u1 * uu0 * uum1 - u0 * uu0 * uum1 + uknots[span + 1] * uknots[span + 3] * uu0 * uum1 - uknots[span - 2] * uknots[span + 3] * uu0 * uum1 - uknots[span - 2] * uknots[span + 2] * uu0 * uum1 - uknots[span - 2] * uknots[span + 1] * uu0 * uum1 + uknots[span - 2] * uknots[span] * uu0 * uum1 - u0 * u2 * uum1 + uknots[span - 2] * uknots[span] * u2 * uum1 - uknots[span - 2] * uknots[span + 3] * u1 * uum1 + uknots[span - 2] * uknots[span + 3] * u0 * uum1 + uknots[span - 2] * uknots[span + 2] * u0 * uum1 + uu0 * uu1 * uu2 + um1 * uu1 * uu2 - u0 * uu1 * uu2 + uknots[span + 1] * uknots[span + 3] * uu1 * uu2 - uknots[span] * uknots[span + 3] * uu1 * uu2 - uknots[span - 1] * uknots[span + 3] * uu1 * uu2 - uknots[span - 2] * uknots[span + 3] * uu1 * uu2 + uknots[span - 2] * uknots[span] * uu1 * uu2 + um2 * uu0 * uu2 - uknots[span - 1] * uknots[span + 1] * uu0 * uu2 - uknots[span - 2] * uknots[span + 1] * uu0 * uu2 - u0 * um2 * uu2 + uknots[span + 1] * uknots[span + 3] * um2 * uu2 - uknots[span] * uknots[span + 3] * um2 * uu2 + uknots[span + 1] * uknots[span + 3] * um1 * uu2 + uknots[span - 2] * uknots[span + 3] * u0 * uu2 + um2 * uu0 * uu1 + um1 * uu0 * uu1 + u2 * uu0 * uu1 + u1 * uu0 * uu1 - uknots[span - 1] * uknots[span + 3] * uu0 * uu1 - uknots[span] * uknots[span + 2] * uu0 * uu1 - 2 * uknots[span - 1] * uknots[span + 2] * uu0 * uu1 - uknots[span - 2] * uknots[span + 2] * uu0 * uu1 - uknots[span - 1] * uknots[span + 1] * uu0 * uu1 + u2 * um2 * uu1 - uknots[span] * uknots[span + 3] * um2 * uu1 - uknots[span] * uknots[span + 2] * um2 * uu1 + 2 * u2 * um1 * uu1 + u1 * um1 * uu1 + uknots[span + 1] * uknots[span + 3] * um1 * uu1 - u0 * u2 * uu1 + uknots[span - 2] * uknots[span] * u2 * uu1 - uknots[span - 1] * uknots[span + 3] * u1 * uu1 + u2 * um2 * uu0 + 2 * u1 * um2 * uu0 - u0 * um2 * uu0 + uknots[span + 1] * uknots[span + 3] * um2 * uu0 - uknots[span] * uknots[span + 2] * um2 * uu0 + u1 * um1 * uu0 - uknots[span - 1] * uknots[span + 3] * u1 * uu0 - uknots[span - 2] * uknots[span + 3] * u1 * uu0 + uknots[span - 2] * uknots[span + 2] * u0 * uu0 - 2 * u0 * u2 * um2);
            double[] u = new double[3];
            int n = Geometry.ragle3(a, b, c, d, u);
            List<double> res = new List<double>();
            for (int i = 0; i < n; ++i)
            {
                if (u[i] >= uknots[span] && u[i] <= uknots[span + 1]) res.Add(u[i]);
            }
            return res.ToArray();
        }
        public Nurbs<T, C> Trim(double u0, double u1)
        {
            if (u0 == uknots[0] && u1 == uknots[uknots.Length - 1])
            {
                return Clone(poles.Clone() as T[]);
            }
            Nurbs<T, C> tmp;
            int i0;
            if (u0 <= uknots[0])
            {
                i0 = 1;
                tmp = this;
            }
            else
            {
                T[] newpoles;
                double[] newknots;
                i0 = CurveKnotIns(u0, udegree, out newknots, out newpoles);
                tmp = new Nurbs<T, C>(udegree, newpoles, newknots);
                i0 += 1;
            }
            T[] newpoles1;
            double[] newknots1;
            int i1;
            if (u1 >= uknots[uknots.Length - 1])
            {
                i1 = tmp.uknots.Length - 1;
                newknots1 = tmp.uknots;
                newpoles1 = tmp.poles;
            }
            else
            {
                i1 = tmp.CurveKnotIns(u1, udegree, out newknots1, out newpoles1);
                i1 += udegree + 1;
            }
            // die folgenden Zeilen wurden notwendig wg. tecnosta.cdb
            while (newknots1[i0] < u0) ++i0;
            while (newknots1[i0] == newknots1[i0 + udegree]) ++i0;
            while (newknots1[i1 - udegree] < u1 && i1 < newknots1.Length - 1) ++i1;
            double[] resknots = new double[i1 - i0 + 2];
            T[] respoles = new T[resknots.Length - udegree - 1];
            resknots[0] = u0;
            resknots[resknots.Length - 1] = u1;
            for (int i = i0; i < i1; ++i)
            {
                resknots[i - i0 + 1] = newknots1[i];
            }
            for (int i = 0; i < respoles.Length; ++i)
            {
                respoles[i] = newpoles1[i0 - 1 + i];
            }
            // manchmal gibt es am Anfang bzw Ende minimale Unterschiede. 
            // wenn fast exakt auf einem knoten geschnitten wurde, dann erhöhen wir die multiplicities vom ersten oder letzten knoten
            // indem wir diesen gleich setzen
            // folgende Zeilen wieder entfernt, stattdessen bei curveknotins auf fast gleichen parameter getestet
            //double dkn = resknots[resknots.Length - 1] - resknots[0];
            //for (int i = 1; i < resknots.Length; i++)
            //{
            //    if (resknots[i] - resknots[0] < dkn * 1e-6) resknots[i] = resknots[0];
            //    else break;
            //}
            //for (int i = resknots.Length - 2; i >= 0; --i)
            //{
            //    if (resknots[resknots.Length - 1] - resknots[i] < dkn * 1e-6) resknots[i] = resknots[resknots.Length - 1];
            //    else break;
            //}
            resknots[0] = resknots[1];
            resknots[resknots.Length - 1] = resknots[resknots.Length - 2];
            return new Nurbs<T, C>(udegree, respoles, resknots);
        }
        public Nurbs<T, C> TrimAtKnot(int ks, int ke)
        {
            Nurbs<T, C> tmp;
            int i0;
            T[] newpoles;
            double[] newknots;
            i0 = CurveKnotInsAt(ks, udegree, out newknots, out newpoles);
            tmp = new Nurbs<T, C>(udegree, newpoles, newknots);
            i0 += 1;
            T[] newpoles1;
            double[] newknots1;
            int i1;
            i1 = tmp.CurveKnotInsAt(ke + udegree - 1, udegree, out newknots1, out newpoles1); // + udegree - 1 bislang nur an degree=3 getestet
            i1 += udegree + 1;
            // die folgenden Zeilen wurden notwendig wg. tecnosta.cdb
            while (newknots1[i0] == newknots1[i0 + udegree]) ++i0;
            double[] resknots = new double[i1 - i0 + 2];
            T[] respoles = new T[resknots.Length - udegree - 1];
            resknots[0] = uknots[ks];
            resknots[resknots.Length - 1] = uknots[ke];
            for (int i = i0; i < i1; ++i)
            {
                resknots[i - i0 + 1] = newknots1[i];
            }
            for (int i = 0; i < respoles.Length; ++i)
            {
                respoles[i] = newpoles1[i0 - 1 + i];
            }
            // manchmal gibt es am Anfang bzw Ende minimale Unterschiede. 
            // Die ersten und letzten beiden Knoten müssen aber eigentlich immer identisch sein, selbst bei degree==1, oder?
            resknots[0] = resknots[1];
            resknots[resknots.Length - 1] = resknots[resknots.Length - 2];
            return new Nurbs<T, C>(udegree, respoles, resknots);
        }
        public Nurbs<T, C>[] Split(double u)
        {
            if (u <= uknots[0] || u >= uknots[uknots.Length - 1])
            {
                return new Nurbs<T, C>[] { Clone(poles.Clone() as T[]) };
            }
            else
            {
                double[] newknots;
                T[] newpoles;
                int indtosplit;
                indtosplit = CurveKnotIns(u, udegree, out newknots, out newpoles);
                // folgendes ginge besser ohne Liste, Längen sind bekannt
                List<double> knots1 = new List<double>();
                List<double> knots2 = new List<double>();
                List<T> poles1 = new List<T>();
                List<T> poles2 = new List<T>();
                knots1.Add(newknots[0]);
                knots2.Add(u);
                for (int i = 1; i < newknots.Length; ++i)
                {
                    if (newknots[i] <= u)
                    {
                        knots1.Add(newknots[i]);
                    }
                    if (newknots[i] >= u)
                    {
                        knots2.Add(newknots[i]);
                    }
                }
                // knots1.Add(newknots[newknots.Length-1]);
                knots1.Add(u);
                //indtosplit = kn1 - udegree; // kleine Nachbesserung, manchmal Probleme, wenn genau auf einem Knoten geschnitten wird
                for (int i = 0; i < newpoles.Length; ++i)
                {
                    if (i <= indtosplit)
                    {
                        poles1.Add(newpoles[i]);
                    }
                    if (i >= indtosplit)
                    {
                        poles2.Add(newpoles[i]);
                    }
                }
                return new Nurbs<T, C>[]{
                    new Nurbs<T, C>(udegree,poles1.ToArray(),knots1.ToArray()),
                    new Nurbs<T, C>(udegree,poles2.ToArray(),knots2.ToArray())};

            }
        }
        internal void NormKnots(double firstknot, double lastknot)
        {   // Skalierung des Parameterbereichs auf ein bestimmtes Intervall
            double f = (lastknot - firstknot) / (uknots[uknots.Length - 1] - uknots[0]);
            double o = firstknot - uknots[0] * f;
            for (int i = 0; i < uknots.Length; ++i)
            {
                uknots[i] = uknots[i] * f + o;
            }
        }
        public Nurbs<T, C> TrimU(double u0, double u1)
        {
            if (u0 == uknots[0] && u1 == uknots[uknots.Length - 1])
            {
                return new Nurbs<T, C>(udegree, vdegree, poles, numUPoles, numVPoles, uknots, vknots);
            }
            Nurbs<T, C>[] tmp = new Nurbs<T, C>[numVPoles];
            for (int i = 0; i < numVPoles; i++)
            {
                T[] pls = new T[numUPoles];
                for (int j = 0; j < numUPoles; j++)
                {
                    pls[j] = poles[ind(j, i)];
                }
                tmp[i] = new Nurbs<T, C>(udegree, pls, uknots);
            }
            int i0 = 0;
            if (u0 <= uknots[0])
            {
                i0 = 1;
            }
            else
            {
                for (int i = 0; i < numVPoles; i++)
                {
                    T[] newpoles;
                    double[] newknots;
                    i0 = tmp[i].CurveKnotIns(u0, udegree, out newknots, out newpoles);
                    tmp[i] = new Nurbs<T, C>(udegree, newpoles, newknots);
                    i0 += 1;
                }
            }
            T[] newpoles1;
            double[] newknots1;
            int i1 = 0;
            if (u1 >= uknots[uknots.Length - 1])
            {
                i1 = tmp[0].uknots.Length - 1;
                //newknots1 = tmp.uknots;
                //newpoles1 = tmp.poles;
            }
            else
            {
                for (int i = 0; i < numVPoles; i++)
                {
                    i1 = tmp[i].CurveKnotIns(u1, udegree, out newknots1, out newpoles1);
                    tmp[i] = new Nurbs<T, C>(udegree, newpoles1, newknots1);
                    i1 += udegree + 1;
                }
            }
            // die folgenden Zeilen wurden notwendig wg. tecnosta.cdb
            //while (newknots1[i0] < u0) ++i0;
            //while (newknots1[i0] == newknots1[i0 + udegree]) ++i0;
            //while (newknots1[i1 - udegree] < u1 && i1 < newknots1.Length - 1) ++i1;

            double[] resknots = new double[i1 - i0 + 2];
            int newNumUPoles = resknots.Length - udegree - 1;
            T[] respoles = new T[newNumUPoles * numVPoles];
            resknots[0] = u0;
            resknots[resknots.Length - 1] = u1;
            newknots1 = tmp[0].UKnots;
            for (int i = i0; i < i1; ++i)
            {
                resknots[i - i0 + 1] = newknots1[i];
            }
            for (int j = 0; j < numVPoles; j++)
            {
                newpoles1 = tmp[j].poles;
                for (int i = 0; i < newNumUPoles; ++i)
                {
                    int ii = i + newNumUPoles * j;
                    respoles[ii] = newpoles1[i0 - 1 + i]; // was respoles[ind(i, j)], which is obviously wrong
                }

            }
            resknots[0] = resknots[1];
            resknots[resknots.Length - 1] = resknots[resknots.Length - 2];
            return new Nurbs<T, C>(udegree, vdegree, respoles, newNumUPoles, numVPoles, resknots, vknots);
        }



        /*+++++++++++++++++++++ Neue bzw. überarbeitete Methoden von Henning +++++++++++++++++++++++*/


        // Neuer Konstruktor zur Interpolation mit B-Spline-Kurven:
        // Der nicht-periodische Fall ist im Wesentlichen unverändert, lediglich der Parameterbereich
        // wurde auf das Intervall [0,1] normiert.
        // Beim periodischen Fall wird nun ein geschlossener B-Spline erzeugt, der die gegebenen Throughpoints
        // interpoliert und überall, insbesondere auch an der "Nahtstelle", (degree-1)-mal stetig differenzierbar ist
        // (der Spline wird auch dann geschlossen, wenn der erste throughpoint nicht mit dem letzten übereinstimmt).
        // Diese Interpolation mit einem geschlossenen B-Spline ist für hohen Grad (i.d.R. schon ab Grad 4) nicht unbedingt
        // zu empfehlen, weil dann die Forderung nach der hohen Differenzierbarkeit an der Nahtstelle zu unnatürlichen
        // Ergebnissen führen kann. Falls der Grad gerade ist, wird im periodischen Fall zur Wahl der Knoten die "shifting" Methode
        // angewendet, die den Vorteil hat, dass das entstehende Gleichungssystem gut konditioniert ist, aber den Nachteil,
        // dass der Startpunkt der Kurve (Parameterwert 0) nicht mit dem ersten Durchgangspunkt übereinstimmt
        // (vgl. Artikel "Choosing nodes and knots in closed B-spline curve interpolation to point data" von H. Park, Computer-Aided Design 33 (2001) ).
        public Nurbs(int degree, T[] throughpoints, bool periodic, out double[] throughpointsparam, bool test, double[] initialKnots = null)
        {// "test" ist nur ein Dummy-Parameter, um den neuen Konstruktor vom alten zu unterscheiden
            calc = new C();
            degree = Math.Min(degree, throughpoints.Length - 1); // bei 2 Punkten nur 1. Grad, also Linie, u.s.w
            this.udegree = degree;
            if (periodic)
            {   // In diesem Fall wird ein geschlossener B-Spline erzeugt, der überall (degree-1)-mal stetig differenzierbar ist
                int n = (calc.Dist(throughpoints[0], throughpoints[throughpoints.Length - 1]) > Precision.eps) ? throughpoints.Length : throughpoints.Length - 1;
                // Abstände für die Parameterwerte der Durchgangspunkte:
                if (initialKnots == null)
                {
                    double[] k = new double[n - 1];
                    double lastk = 0.0;
                    bool kIsOK = true;
                    for (int i = 0; i < k.Length; ++i)
                    {
                        double d = calc.Dist(throughpoints[i + 1], throughpoints[i]);
                        if (d == 0.0)
                        {
                            kIsOK = false;
                            break;
                        }
                        k[i] = lastk + d;
                        lastk = k[i];
                    }
                    double dlast = calc.Dist(throughpoints[0], throughpoints[n - 1]);
                    if (dlast == 0.0) kIsOK = false;
                    throughpointsparam = new double[n + 1];
                    if (!kIsOK)
                    {   // wenn zwei aufeinanderfolgende Punkte identisch sind, werden die Parameterwerte gleichmäßig zwischen 0 und 1 verteilt (Notlösung)
                        for (int i = 0; i < throughpointsparam.Length; ++i)
                        {
                            throughpointsparam[i] = (double)i / (double)n;
                        }
                    }
                    else
                    {   // sonst werden die Parameterwerte nach der "chordlength-Methode" verteilt
                        double chordlength = k[n - 2] + dlast; // Länge des geschlossenen Polygonzugs
                        for (int i = 1; i < n; ++i)
                        {
                            throughpointsparam[i] = k[i - 1] / chordlength;
                        }
                        throughpointsparam[n] = 1.0;
                    }
                }
                else
                {
                    if (initialKnots.Length != n + 1) throw new NurbsException("unable to construct NURBS with provided knots");
                    throughpointsparam = initialKnots;
                }
                uknots = new double[n + 2 * degree + 1];
                for (int i = uknots.Length - degree - 1; i < uknots.Length; ++i)
                {
                    uknots[i] = 1.0;
                }
                if (degree % 2 == 1)
                {   // wenn der Grad ungerade ist, werden die inneren Knoten so gewählt,
                    // dass sie mit den Parameterwerten der Durchgangspunkte übereinstimmen (liefert die besten Ergebnisse)
                    for (int i = 1; i < n; ++i)
                    {
                        uknots[degree + i] = throughpointsparam[i];
                    }
                }
                else
                {   // wenn der Grad gerade ist, wird die "shifting method" angewendet
                    uknots[degree + 1] = (1 - throughpointsparam[n - 1] + throughpointsparam[1]) / 2;
                    for (int i = 1; i < n - 1; ++i)
                    {
                        uknots[degree + 1 + i] = uknots[degree + i] + (throughpointsparam[i + 1] - throughpointsparam[i - 1]) / 2;
                    }
                    double shift = (1 - throughpointsparam[n - 1]) / 2;
                    for (int i = 0; i < n; ++i)
                    {
                        throughpointsparam[i] += shift;
                    }
                }
                // zunächst leere Poles erzeugen
                poles = new T[n + degree];
                // Matrix für das Gleichungssystem erzeugen
                double[,] matrix = new double[poles.Length, poles.Length]; // sind alle 0
                // LinearAlgebra.BandedMatrix bmatrix = new CADability.LinearAlgebra.BandedMatrix(poles.Length, degree - 1, degree - 1);
                for (int i = 0; i < n; ++i)
                {
                    double[] bf;
                    double u = throughpointsparam[i];
                    int span = (degree % 2 == 1) ? (degree + i) : FindSpanU(uknots.Length - degree - 1, degree, u);
                    BasisFunsU(span, u, degree, out bf);
                    for (int j = 0; j < bf.Length; ++j)
                    {
                        matrix[i, span - degree + j] = bf[j];
                        // bmatrix[i, span - degree + j] = bf[j];
                    }
                }
                // Forderung Startpunkt = Endpunkt:
                matrix[n, 0] = 1.0;
                matrix[n, poles.Length - 1] = -1.0;
                // Forderung nach der Gleichheit der Ableitungen in Start- und Endpunkt bis zur Ordnung degree-1:
                double[,] dersBfStart, dersBfEnd;
                DersBasisFuns(degree, 0.0, degree, degree - 1, out dersBfStart);
                DersBasisFuns(uknots.Length - degree - 2, 1.0, degree, degree - 1, out dersBfEnd);
                for (int i = 1; i < degree; ++i)
                {
                    for (int j = 0; j <= degree; ++j)
                    {
                        matrix[n + i, j] += dersBfStart[i, j];
                        matrix[n + i, n - 1 + j] = -dersBfEnd[i, j];
                    }
                }
                // q ist die rechte Seite des Gleichungssystems
                int dim = calc.GetComponents(throughpoints[0]).Length;
                double[,] q = new double[n + degree, dim];
                for (int i = 0; i < n; ++i)
                {
                    double[] c = calc.GetComponents(throughpoints[i]);
                    for (int j = 0; j < c.Length; ++j)
                    {
                        q[i, j] = c[j];
                    }
                }
                Matrix lam = DenseMatrix.OfArray(matrix);
                Matrix laq = DenseMatrix.OfArray(q);
                LU<double> lud = lam.LU();
                Matrix lapoles = (Matrix)lud.Solve(laq);
                // LinearAlgebra.Matrix bpoles;
                // bpoles = bmatrix.Solve(laq);
                if (lapoles != null)
                {
                    for (int i = 0; i < poles.Length; ++i)
                    {
                        double[] lap = new double[dim];
                        for (int j = 0; j < dim; ++j)
                        {
                            lap[j] = lapoles[i, j];
                            // lap[j] = bpoles[i, j];
                        }
                        calc.SetComponents(ref poles[i], lap);
                    }
                }
                else
                {
                    throw new NurbsException("unable to construct NURBS with throughpoints");
                }
                return;
            }
            {
                // Nicht-periodischer Fall:
                int n = throughpoints.Length - 1; // Durchgangspunkte sind indiziert von 0 bis n
                // 1. Abstände für die Parameterwerte der Durchgangspunkte
                if (initialKnots == null)
                {
                    double[] k = new double[n];
                    double lastk = 0.0;
                    bool kIsOK = true;
                    for (int i = 0; i < n; ++i)
                    {
                        double d = calc.Dist(throughpoints[i + 1], throughpoints[i]);
                        if (d == 0.0)
                        {
                            kIsOK = false;
                            break;
                        }
                        k[i] = lastk + d;
                        lastk = k[i];
                    }
                    throughpointsparam = new double[n + 1];
                    if (!kIsOK)
                    {   // wenn zwei aufeinanderfolgende Punkte identisch sind, werden die Parameterwerte gleichmäßig zwischen 0 und 1 verteilt (Notlösung)
                        for (int i = 0; i <= n; ++i)
                        {
                            throughpointsparam[i] = (double)i / (double)n;
                        }
                    }
                    else
                    {   // sonst werden die Parameterwerte nach der "chordlength" Methode verteilt
                        double chordlength = k[n - 1]; // Länge des Polygonzugs der Durchgangspunkte
                        for (int i = 1; i <= n; ++i)
                        {
                            throughpointsparam[i] = k[i - 1] / chordlength;
                        }
                    }
                }
                else
                {
                    if (initialKnots.Length != n + 1) throw new NurbsException("unable to construct NURBS with provided knots");
                    throughpointsparam = initialKnots;
                }
                // 2. daraus den Knotenvektor nach der "averaging" Methode
                uknots = new double[n + degree + 2];
                for (int i = degree + 1; i <= n; ++i)
                {
                    double s = 0.0;
                    for (int j = 0; j < degree; ++j)
                    {
                        s += throughpointsparam[i - degree + j];
                    }
                    uknots[i] = s / degree;
                }
                for (int i = n + 1; i < uknots.Length; ++i)
                {
                    uknots[i] = 1.0;
                }
                // 3. zunächste leere Poles erzeugen
                poles = new T[n + 1];
                // 4. Matrix für das Gleichungssystem erzeugen
                // double[,] matrix = new double[poles.Length, poles.Length]; // sind alle 0
                Matrix bmatrix = new SparseMatrix(poles.Length, poles.Length);
                //LinearAlgebra.BandedMatrix bmatrix = new CADability.LinearAlgebra.BandedMatrix(poles.Length, degree - 1, degree - 1);
                for (int i = 0; i < poles.Length; ++i)
                {
                    double[] bf;
                    double u = throughpointsparam[i];
                    int span = FindSpanU(uknots.Length - degree - 1, degree, u);
                    BasisFunsU(span, u, degree, out bf);
                    for (int j = 0; j < bf.Length; ++j)
                    {
                        // matrix[i, span - degree + j] = bf[j];
                        bmatrix[i, span - degree + j] = bf[j];
                    }
                }
                // q ist der Lösungsvektor, also die Durchgangspunkte
                int dim = calc.GetComponents(throughpoints[0]).Length;
                double[,] q = new double[n + 1, dim];
                for (int i = 0; i <= n; ++i)
                {
                    double[] c = calc.GetComponents(throughpoints[i]);
                    for (int j = 0; j < c.Length; ++j)
                    {
                        q[i, j] = c[j];
                    }
                }
                //LinearAlgebra.Matrix lam = new CADability.LinearAlgebra.Matrix(matrix);
                Matrix laq = DenseMatrix.OfArray(q);
                //LinearAlgebra.LUDecomposition lud = lam.LUD();
                //LinearAlgebra.Matrix lapoles = lud.Solve(laq);
                Matrix bpoles;
                bpoles = (Matrix)bmatrix.Solve(laq);
                if (bpoles.IsValid())
                {
                    for (int i = 0; i < poles.Length; ++i)
                    {
                        double[] lap = new double[dim];
                        for (int j = 0; j < dim; ++j)
                        {
                            // lap[j] = lapoles[i, j];
                            lap[j] = bpoles[i, j];
                        }
                        calc.SetComponents(ref poles[i], lap);
                    }
                }
                else
                {
                    throw new NurbsException("unable to construct NURBS with throughpoints");
                }
            }
        }


        // Konstruktor zur B-Spline-Interpolation mit vorgegebener erster Ableitung im Startpunkt (sDirection) und
        // im Endpunkt (eDirection):
        // Das Ergebnis hängt auch von der Länge von sDirection und eDirection ab! Ein Richtwert für die Länge
        // wäre ein Wert im Bereich der chordlength (Länge des Polygonzugs der Durchgangspunkte).
        public Nurbs(int degree, T[] throughpoints, T sDirection, T eDirection, out double[] throughpointsparam)
        {
            calc = new C();
            degree = Math.Min(degree, throughpoints.Length - 1); // bei 2 Punkten nur 1. Grad, also Linie, u.s.w
            this.udegree = degree;
            int n = throughpoints.Length - 1; // Durchgangspunkte sind indiziert von 0 bis n
            // 1. Parameterwerte der Durchgangspunkte
            throughpointsparam = new double[n + 1];
            double lastd = 0.0;
            bool dIsOK = true;
            for (int i = 1; i <= n; ++i)
            {
                double d = calc.Dist(throughpoints[i], throughpoints[i - 1]);
                if (d == 0.0)
                {
                    dIsOK = false;
                }
                throughpointsparam[i] = lastd + d;
                lastd = throughpointsparam[i];
            }
            double chordlength = throughpointsparam[n]; // Länge des Polygonzugs der Durchgangspunkte
            if (!dIsOK)
            {   // wenn zwei aufeinanderfolgende Punkte identisch sind, werden die Parameterwerte gleichmäßig zwischen 0 und 1 verteilt (Notlösung)
                for (int i = 0; i <= n; ++i)
                {
                    throughpointsparam[i] = (double)i / (double)n;
                }
            }
            else
            {   // sonst werden die Parameterwerte nach der "chordlength" Methode verteilt
                for (int i = 1; i <= n; ++i)
                {
                    throughpointsparam[i] /= chordlength;
                }
            }
            // 2. daraus den Knotenvektor
            uknots = new double[n + degree + 4];
            for (int i = n + 3; i < uknots.Length; ++i)
            {
                uknots[i] = 1.0;
            }
            // Spezialfall: kubische Spline-Interpolation (innere Knoten = Parameterwerte der Durchgangspunkte)
            if (degree == 3)
            {
                for (int i = 1; i < n; ++i)
                {
                    uknots[3 + i] = throughpointsparam[i];
                }
            }
            else
            {   //sonst werden die Knoten nach der "averaging" Methode verteilt
                for (int i = degree + 1; i <= n + 2; ++i)
                {
                    double s = 0.0;
                    for (int j = 0; j < degree; ++j)
                    {
                        s += throughpointsparam[i - degree - 1 + j];
                    }
                    uknots[i] = s / degree;
                }
            }
            // 3. zunächst leere Poles erzeugen
            poles = new T[n + 3];
            // 4. Matrix für das Gleichungssystem erzeugen
            // double[,] matrix = new double[poles.Length, poles.Length]; // sind alle 0
            Matrix bmatrix = new SparseMatrix(poles.Length, poles.Length);
            //LinearAlgebra.BandedMatrix bmatrix = new CADability.LinearAlgebra.BandedMatrix(poles.Length, degree - 1, degree - 1);
            bmatrix[0, 0] = 1.0;
            bmatrix[1, 0] = -1.0;
            bmatrix[1, 1] = 1.0;
            bmatrix[n + 1, n + 1] = -1.0;
            bmatrix[n + 1, n + 2] = 1.0;
            bmatrix[n + 2, n + 2] = 1.0;
            for (int i = 2; i <= n; ++i)
            {
                double[] bf;
                double u = throughpointsparam[i - 1];
                int span = FindSpanU(uknots.Length - degree - 1, degree, u);
                BasisFunsU(span, u, degree, out bf);
                for (int j = 0; j < bf.Length; ++j)
                {
                    // matrix[i, span - degree + j] = bf[j];
                    bmatrix[i, span - degree + j] = bf[j];
                }
            }
            // 5. Ableitungen in Start- und Endpunkt für das Gleichungssystem anpassen
            // (vgl. NURBS Book, S. 371, Gleichung 9.11 und 9.12)
            sDirection = calc.Mul(uknots[degree + 1] / degree, sDirection);
            eDirection = calc.Mul((1 - uknots[n + 2]) / degree, eDirection);

            // 6. rechte Seite des Gleichungssystems (q) erzeugen
            int dim = calc.GetComponents(throughpoints[0]).Length;
            double[,] q = new double[n + 3, dim];

            double[] q0 = calc.GetComponents(throughpoints[0]);
            for (int j = 0; j < dim; ++j)
            {
                q[0, j] = q0[j];
            }
            double[] qs = calc.GetComponents(sDirection);
            for (int j = 0; j < dim; ++j)
            {
                q[1, j] = qs[j];
            }
            double[] qe = calc.GetComponents(eDirection);
            for (int j = 0; j < dim; ++j)
            {
                q[n + 1, j] = qe[j];
            }
            double[] qn = calc.GetComponents(throughpoints[n]);
            for (int j = 0; j < dim; ++j)
            {
                q[n + 2, j] = qn[j];
            }
            for (int i = 2; i <= n; ++i)
            {
                double[] c = calc.GetComponents(throughpoints[i - 1]);
                for (int j = 0; j < dim; ++j)
                {
                    q[i, j] = c[j];
                }
            }
            //LinearAlgebra.Matrix lam = new CADability.LinearAlgebra.Matrix(matrix);
            Matrix laq = DenseMatrix.OfArray(q);
            //LinearAlgebra.LUDecomposition lud = lam.LUD();
            //LinearAlgebra.Matrix lapoles = lud.Solve(laq);
            Matrix bpoles;
            bpoles = (Matrix)bmatrix.Solve(laq);
            if (bpoles != null)
            {
                for (int i = 0; i < poles.Length; ++i)
                {
                    double[] lap = new double[dim];
                    for (int j = 0; j < dim; ++j)
                    {
                        // lap[j] = lapoles[i, j];
                        lap[j] = bpoles[i, j];
                    }
                    calc.SetComponents(ref poles[i], lap);
                }
            }
            else
            {
                throw new NurbsException("unable to construct NURBS with throughpoints and end derivatives");
            }
        }


        // Neuer Konstruktor zur B-Spline-Interpolation mit vorgegebenen ersten Ableitungen in den Durchgangspunkten:
        // Auch hier ist das Ergebnis von der Länge der "throughdirections" abhängig (und als Richtwert kann die chordlength genommen werden).
        // Die größte Änderung gegenüber dem alten Konstruktor ist, dass nun auch der periodic-Fall behandelt wird,
        // bei dem ein geschlossener B-Spline erzeugt wird, dessen erste Ableitung in Anfangs- und Endpunkt übereinstimmt und den durch
        // throughdirections[0] gegebenen Wert hat (insbesondere ist er an der Nahtstelle mind. einmal stetig differenzierbar).
        public Nurbs(int degree, T[] throughpoints, T[] throughdirections, bool periodic, out double[] throughpointsparam)
        {   // im Buch Seite 375
            if (throughpoints.Length != throughdirections.Length) throw new NurbsException("points and direction arrays must be same size");
            calc = new C(); // das kostet angeblich nix!
            this.udegree = degree;

            int n = throughpoints.Length - 1;
            if (periodic && calc.Dist(throughpoints[0], throughpoints[throughpoints.Length - 1]) > Precision.eps) n++;

            // 1. Parameterwerte der Durchgangspunkte nach der "chordlength" Methode bestimmen
            throughpointsparam = new double[n + 1];
            double lastd = 0.0;
            bool dIsOK = true;
            for (int i = 1; i < n; ++i)
            {
                double d = calc.Dist(throughpoints[i], throughpoints[i - 1]);
                if (d == 0.0)
                {
                    dIsOK = false;
                }
                throughpointsparam[i] = lastd + d;
                lastd = throughpointsparam[i];
            }
            double lastDist = periodic ? calc.Dist(throughpoints[n - 1], throughpoints[0]) : calc.Dist(throughpoints[n - 1], throughpoints[n]);
            if (lastDist == 0.0) dIsOK = false;
            double chordlength = throughpointsparam[n] = lastd + lastDist; // Länge des Polygonzugs der Durchgangspunkte
            if (!dIsOK)
            {   // wenn zwei aufeinanderfolgende Punkte identisch sind, werden die Parameterwerte gleichmäßig zwischen 0 und 1 verteilt (Notlösung)
                for (int i = 0; i <= n; ++i)
                {
                    throughpointsparam[i] = (double)i / (double)n;
                }
            }
            else
            {
                for (int i = 1; i <= n; ++i)
                {
                    throughpointsparam[i] /= chordlength;
                }
            }
            // 2. daraus den Knotenvektor
            uknots = new double[2 * n + degree + 3];
            // Die inneren Knoten ergeben sich durch "Strecken" der Parameterskala auf die uknots Skala.
            // 2*n - degree + 1 innere Knoten kommen auf n Parameterwerte
            double f = (double)n / (double)(2 * n - degree + 2);
            for (int i = degree + 1; i <= 2 * n + 1; ++i)
            {
                double d = (i - degree) * f;
                int ind = (int)Math.Floor(d);
                uknots[i] = throughpointsparam[ind] + (d - ind) * (throughpointsparam[ind + 1] - throughpointsparam[ind]);
            }
            for (int i = 2 * (n + 1); i < 2 * n + degree + 3; ++i)
            {
                uknots[i] = 1.0;
            }
            // 3. zunächste leere Poles erzeugen
            poles = new T[2 * (n + 1)];
            // 4. Matrix für das Gleichungssystem erzeugen
            double[,] matrix = new double[poles.Length, poles.Length]; // sind alle 0
            for (int i = 0; i <= n; ++i)
            {
                double[,] bf;
                double u = throughpointsparam[i];
                int span = FindSpanU(uknots.Length - degree - 1, degree, u);
                DersBasisFuns(span, u, udegree, 1, out bf);
                for (int j = 0; j <= degree; ++j)
                {
                    matrix[2 * i, span - degree + j] = bf[0, j];
                }
                for (int j = 0; j <= degree; ++j)
                {
                    matrix[2 * i + 1, span - degree + j] = bf[1, j];
                }
            }
            // q ist der Lösungsvektor, also die Durchgangspunkte und die Richtungen
            int dim = calc.GetComponents(throughpoints[0]).Length;
            double[,] q = new double[2 * (n + 1), dim];
            double[] c;
            for (int i = 0; i < n; ++i)
            {
                c = calc.GetComponents(throughpoints[i]);
                for (int j = 0; j < dim; ++j)
                {
                    q[2 * i, j] = c[j];
                }
                c = calc.GetComponents(throughdirections[i]);
                for (int j = 0; j < dim; ++j)
                {
                    q[2 * i + 1, j] = c[j];
                }
            }
            c = calc.GetComponents(throughpoints[periodic ? 0 : n]);
            for (int j = 0; j < dim; ++j)
            {
                q[2 * n, j] = c[j];
            }
            c = calc.GetComponents(throughdirections[periodic ? 0 : n]);
            for (int j = 0; j < dim; ++j)
            {
                q[2 * n + 1, j] = c[j];
            }
            Matrix lam = DenseMatrix.OfArray(matrix);
            Matrix laq = DenseMatrix.OfArray(q);
            LU<double> lud = lam.LU();
            Matrix lapoles = (Matrix)lud.Solve(laq);
            if (lapoles != null)
            {
                for (int i = 0; i < poles.Length; ++i)
                {
                    double[] lap = new double[dim];
                    for (int j = 0; j < dim; ++j)
                    {
                        lap[j] = lapoles[i, j];
                    }
                    calc.SetComponents(ref poles[i], lap);
                }
            }
            else
            {
                throw new NurbsException("unable to construct NURBS with throughpoints and directions");
            }
        }


        // Methoden zur Berechnung von oberen Schranken für die zweiten Ableitungen von nicht-rationalen B-Spline-Kurven bzw. -Flächen:
        // Diese Methoden können verwendet werden, um den Approximationsfehler bei einer linearen Approximation der Kurve bzw. Fläche
        // abzuschätzen (vgl. Artikel "Surface algorithms using bounds on derivatives" von Filip, Magedson und Markot, Computer Aided Geometric Design 3 (1986) ).
        public double[] BoundsCurveDeriv2()
        {   // Liefert ein Array bounds[] mit oberen Schranken für den Betrag der zweiten Ableitung einer nicht-rationalen B-Spline-Kurve.
            // bounds[i] ist eine obere Schranke (i.A. nicht das Supremum) für den Betrag (euklidische Norm) der zweiten Ableitung des 
            // Kurvensegments auf dem Intervall von hknots[i] bis hknots[i+1] (wobei hknots den "steilen" Knotenvektor bezeichnet).
            if (deriv2 == null) InitDeriv2();
            // Erstelle Liste mit allen Indizes i, für die gilt: uknots[i] < uknots[i+1]
            List<int> hknotsInd = new List<int>();
            for (int i = udegree; i < poles.Length; ++i)
            {
                if (uknots[i] != uknots[i + 1]) hknotsInd.Add(i);
            }
            int[] knotsInd = hknotsInd.ToArray();
            double[] bounds = new double[knotsInd.Length];
            for (int i = 0; i < bounds.Length; ++i)
            {
                double max = 0.0;
                for (int j = knotsInd[i] - udegree; j <= knotsInd[i] - 2; ++j)
                {
                    double temp = calc.EuclNorm(deriv2[j]);
                    if (temp > max) max = temp;
                }
                bounds[i] = max;
            }
            return bounds;
        }

        public double[,,] BoundsSurfaceDeriv2()
        {   // Liefert ein Array bounds[ , , ] mit oberen Schranken für die Beträge der zweiten Ableitungen einer nicht-rationalen B-Spline-Fläche.
            // bounds[l, i, j] ist eine obere Schranke (i.A. nicht das Supremum) für den Betrag (euklidische Norm) der (2-l)-fach in u-Richtung und
            // l-fach in v-Richtung abgeleiteten Flächenfunktion und zwar auf dem Parameterbereich von huknots[i] bis huknots[i+1] (bzgl. u) und 
            // von hvknots[j] bis hvknots[j+1] (bzgl. v), wobei huknots den "steilen" u-Knotenvektor und hvknots den "steilen" v-Knotenvektor bezeichnet.
            DerivsKL PKL = SurfaceDeriveCpts(2);
            // Erstelle Liste mit allen Indizes i, für die gilt: uknots[i] < uknots[i+1]
            List<int> huknotsInd = new List<int>();
            for (int i = udegree; i < numUPoles; ++i)
            {
                if (uknots[i] != uknots[i + 1]) huknotsInd.Add(i);
            }
            int[] uknotsInd = huknotsInd.ToArray();
            // Erstelle Liste mit allen Indizes i, für die gilt: vknots[i] < vknots[i+1]
            List<int> hvknotsInd = new List<int>();
            for (int i = vdegree; i < numVPoles; ++i)
            {
                if (vknots[i] != vknots[i + 1]) hvknotsInd.Add(i);
            }
            int[] vknotsInd = hvknotsInd.ToArray();
            double[,,] bounds = new double[3, uknotsInd.Length, vknotsInd.Length];
            for (int l = 0; l <= 2; ++l)
            {
                for (int i_0 = 0; i_0 < uknotsInd.Length; ++i_0)
                {
                    for (int j_0 = 0; j_0 < vknotsInd.Length; ++j_0)
                    {
                        double max = 0.0;
                        for (int i = uknotsInd[i_0] - udegree; i <= uknotsInd[i_0] - (2 - l); ++i)
                        {
                            for (int j = vknotsInd[j_0] - vdegree; j <= vknotsInd[j_0] - l; ++j)
                            {
                                double temp = calc.EuclNorm(PKL[2 - l, l, i, j]);
                                if (temp > max) max = temp;
                            }
                        }
                        bounds[l, i_0, j_0] = max;
                    }
                }
            }
            return bounds;
        }


    }
}
