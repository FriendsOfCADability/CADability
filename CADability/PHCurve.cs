using MathNet.Numerics;
using System.Numerics;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Wintellect.PowerCollections;



namespace CADability.GeoObject
{
    [Serializable]
    internal class PHCurve : GeneralCurve, ISerializable
    //Die Algorithmen richten sich größtenteils nach dem Buch
    // "Pythagorean-Hodograph Curves" von Rida T. Farouki, erschienen 2008
    // bei Springer in der Reihe "Geometry and Computing". Im Code sind Verweise
    // auf die entsprechenden Literaturstellen zu finden.
    {
        public GeoPoint[] points; //Durchgangspunkte
        public GeoVector[] derivs; //Ableitungen auf den Segmenten in 0
        public GeoVector[] derivs2; //Ableitungen auf den Segmenten in 1
        public GeoPoint[,] cpoints; //Kontrollpunkte; 6 pro Segment, erster und letzter entsprechen den zugehörigen Durchgangspunkten
        public Complex[] a, b, k, c; //definierende Parameter der einzelnen Kurvensegmente
        public int precis = 0; //Konstante für die gewünschte Iterationsstufe bei der Bogenlängenparametriesierung
        public double[] t; //Segmentlängen im Bezug auf den Parameterbereich
        public double[] par; //Parameterwerte in den Kontrollpunkten

        public PHCurve()
        {

        }
        public void Set(BSpline bsp)
        {

        }
        public void Set(GeoPoint[] pts)
        {
            points = pts;
            derivs = new GeoVector[pts.Length];
            a = new Complex[pts.Length - 1];
            b = new Complex[pts.Length - 1];
            k = new Complex[pts.Length - 1];
            c = new Complex[pts.Length - 1];
            double[] th = new double[pts.Length];
            calculateSpacings(pts);
            par = knotParams();
            calculateDerivs(pts);
            for (int i = 0; i <= pts.Length - 2; ++i)
                init(i);
            for (int j = 0; j < precis; ++j)
            {
                for (int i = 1; i < pts.Length; ++i)
                    th[i] = (arcLength(par[i]) - arcLength(par[i - 1])) / arcLength(1);
                for (int i = 1; i < pts.Length; ++i)
                    t[i] = th[i];
                par = knotParams();
                calculateDerivs(pts);
                for (int i = 0; i <= pts.Length - 2; ++i)
                    init(i);
            }
            calculateCPoints();
        }
        private void Set(GeoPoint[] pts, GeoVector[] devs1, GeoVector[] devs2)
        {
            points = pts;
            derivs = devs1;
            derivs2 = devs2;
            a = new Complex[pts.Length - 1];
            b = new Complex[pts.Length - 1];
            k = new Complex[pts.Length - 1];
            c = new Complex[pts.Length - 1];
            double[] th = new double[pts.Length];
            calculateSpacings(pts);
            par = knotParams();
            for (int i = 0; i < pts.Length - 1; ++i)
                init(i);
            for (int j = 0; j < precis; ++j)
            {
                for (int i = 1; i < pts.Length; ++i)
                    th[i] = (arcLength(par[i]) - arcLength(par[i - 1])) / arcLength(1);
                for (int i = 1; i < pts.Length; ++i)
                    t[i] = th[i];
                par = knotParams();
                for (int i = 0; i <= pts.Length - 2; ++i)
                    init(i);
            }
            calculateCPoints();
        }

        #region Überschriebene Methoden
        protected override double[] GetBasePoints()
        {
            return par;
        }
        public override GeoVector DirectionAt(double position)
        {
            int i = 0;
            while (position > par[i + 1])
                i += 1;
            /* für uniforme Knoten:
            int l = points.Length - 1;
            if (position == 1.0)
                i = l-1;
            else
                i = (int)Math.Floor(l * position);
            Complex apos = l * position - i - a[i], bpos = l * position - i - b[i];
            */
            Complex apos = (position - par[i]) / t[i + 1] - a[i], bpos = (position - par[i]) / t[i + 1] - b[i];
            //Farouki, S. 527 ff.
            GeoVector v = vFromComplex(fromStdDeriv(k[i] * (apos * bpos).Square(), i));
            return v;
        }
        public override GeoPoint PointAt(double position)
        {
            int i = 0;
            while (position > par[i + 1])
                i += 1;
            /* für uniforme Knoten:
            int l = points.Length - 1;
            if (position == 1.0)
                i = l-1;
            else
                i = (int)Math.Floor(l * position);
            Complex apos = l * position - i - a[i], bpos = l * position - i - b[i];
            */
            Complex apos = (position - par[i]) / t[i + 1] - a[i], bpos = (position - par[i]) / t[i + 1] - b[i];
            //Farouki, S. 528 f.
            GeoPoint p = pFromComplex(fromStdPoint(k[i] / 30 * (apos.Power(5.0) - 5 * apos.Power(4.0) * bpos + 10 * apos.Square() * apos * bpos.Square()) + c[i], i));
            return p;
        }
        public GeoVector Direction2At(double position)
        {
            int i = 0;
            while (position > par[i + 1])
                i += 1;
            Complex apos = (position - par[i]) / t[i + 1] - a[i], bpos = (position - par[i]) / t[i + 1] - b[i];
            GeoVector v = vFromComplex(fromStdDeriv(k[i] * 2 * apos * bpos * (apos + bpos), i));
            return v;
        }
        public override void Reverse()
        {
            Array.Reverse(points);
            Set(points);
        }
        public override ICurve[] Split(double Position)
        {
            if (Position == 0.0 || Position == 1.0)
                return new ICurve[1] { (PHCurve)this.Clone() };
            ICurve[] segs = new ICurve[2];
            GeoPoint p = PointAt(Position);
            GeoVector v = DirectionAt(Position);
            int i = 0;
            while (Position >= par[i + 1])
                i += 1;
            GeoPoint[] pts2 = new GeoPoint[points.Length - i];
            GeoVector[] devs2 = new GeoVector[points.Length - i];
            GeoVector[] devs22 = new GeoVector[points.Length - i];
            GeoPoint[] pts1;
            GeoVector[] devs1;
            GeoVector[] devs21;
            if (Position == par[i])
            {
                pts1 = new GeoPoint[i + 1];
                devs1 = new GeoVector[i + 1];
                devs21 = new GeoVector[i + 1];
            }
            else
            {
                pts1 = new GeoPoint[i + 2];
                devs1 = new GeoVector[i + 2];
                devs21 = new GeoVector[i + 2];
            }

            for (int j = 0; j <= i; ++j)
            {
                pts1[j] = points[j];
                devs1[j] = derivs[j];
            }

            for (int j = 0; j < i; ++j)
            {
                devs21[j] = derivs2[j];
            }

            if (Position != par[i])
            {
                pts1[i + 1] = p;
                devs1[i] = vFromComplex(new Complex(derivs[i].x, derivs[i].y) * ((Position - par[i])) / (par[i + 1] - par[i]));

            }

            pts2[0] = p;

            if (Position != par[i])
            {
                devs2[0] = vFromComplex(new Complex(v.x, v.y) * ((par[i + 1] - Position) / (par[i + 1] - par[i])));
                devs21[i] = vFromComplex(new Complex(v.x, v.y) * ((Position - par[i]) / (par[i + 1] - par[i])));
                devs22[0] = vFromComplex(new Complex(derivs2[i].x, derivs2[i].y) * ((par[i + 1] - Position) / (par[i + 1] - par[i])));
            }
            else
            {
                devs2[0] = derivs[i];
                devs21[i] = devs2[0];
                devs22[0] = derivs[i + 1];
            }
            for (int j = i + 1; j <= points.Length - 1; ++j)
            {
                pts2[j - i] = points[j];
                devs2[j - i] = derivs[j];
            }
            for (int j = i + 1; j <= points.Length - 2; ++j)
            {
                devs22[j - i] = derivs2[j];
            }
            PHCurve seg0 = new PHCurve();
            PHCurve seg1 = new PHCurve();
            seg0.Set(pts1, devs1, devs21);
            seg1.Set(pts2, devs2, devs22);
            segs[0] = seg0;
            segs[1] = seg1;
            return segs;
        }
        public override ICurve[] Split(double Position1, double Position2)
        {
            ICurve[] segs = new ICurve[2];
            if (Position1 > Position2)
            {
                double tmp = Position1;
                Position1 = Position2;
                Position2 = tmp;
            }
            GeoPoint p1 = PointAt(Position1);
            GeoPoint p2 = PointAt(Position2);
            GeoVector v1 = DirectionAt(Position1);
            GeoVector v2 = DirectionAt(Position2);
            int i1 = points.Length - 1;
            int i2 = 0;
            if (Position1 == 0.0)
                i1 = 0;
            else
                while (Position1 <= par[i1])
                    i1 -= 1;
            if (Position2 == 1.0)
                i2 = points.Length - 1;
            else
                while (Position2 >= par[i2 + 1])
                    i2 += 1;
            GeoPoint[] pts2 = new GeoPoint[i1 - i2 + points.Length + 1];
            GeoVector[] devs2 = new GeoVector[i1 - i2 + points.Length + 1];
            GeoVector[] devs22 = new GeoVector[i1 - i2 + points.Length + 1];
            GeoPoint[] pts1;
            GeoVector[] devs1;
            GeoVector[] devs21;
            if (Position1 == par[i1 + 1] && Position2 == par[i2])
            {
                pts1 = new GeoPoint[i2 - i1];
                devs1 = new GeoVector[i2 - i1];
                devs21 = new GeoVector[i2 - i1];
            }
            else
            {
                if (Position1 == par[i1 + 1] || Position2 == par[i2])
                {
                    pts1 = new GeoPoint[i2 - i1 + 1];
                    devs1 = new GeoVector[i2 - i1 + 1];
                    devs21 = new GeoVector[i2 - i1 + 1];
                }
                else
                {
                    pts1 = new GeoPoint[i2 - i1 + 2];
                    devs1 = new GeoVector[i2 - i1 + 2];
                    devs21 = new GeoVector[i2 - i1 + 2];
                }
            }

            if (i1 == i2 && par[i1 + 1] != Position1 && par[i2] != Position2)
            {
                pts1[0] = p1;
                pts1[1] = p2;
                devs1[0] = vFromComplex(new Complex(v1.x, v1.y) * (Position2 - Position1) / (par[i1 + 1] - par[i1]));
                devs21[0] = vFromComplex(new Complex(v2.x, v2.y) * (Position2 - Position1) / (par[i1 + 1] - par[i1]));
            }
            else
            {
                pts1[0] = p1;
                devs1[0] = vFromComplex(new Complex(v1.x, v1.y) * (par[i1 + 1] - Position1) / (par[i1 + 1] - par[i1]));
                if (Position1 != par[i1 + 1])
                {
                    for (int j = 1; j < i2 - i1; ++j)
                    {
                        pts1[j] = points[j + i1];
                        devs1[j] = derivs[j + i1];
                    }
                    pts1[i2 - i1] = points[i2];

                    if (Position2 != par[i2])
                    {
                        pts1[i2 - i1 + 1] = p2;
                        devs1[i2 - i1] = vFromComplex(new Complex(derivs[i2].x, derivs[i2].y) * (Position2 - par[i2]) / (par[i2 + 1] - par[i2]));
                        devs21[i2 - i1] = vFromComplex(new Complex(v2.x, v2.y) * ((Position2 - par[i2]) / (par[i2 + 1] - par[i2])));
                    }
                    devs21[0] = vFromComplex(new Complex(derivs2[i1].x, derivs2[i1].y) * (par[i1 + 1] - Position1) / (par[i1 + 1] - par[i1]));
                    for (int j = 1; j < i2 - i1; ++j)
                    {
                        devs21[j] = derivs2[j + i1];
                    }
                }
                else
                {
                    for (int j = 0; j < i2 - i1; ++j)
                    {
                        pts1[j] = points[j + i1 + 1];
                        devs1[j] = derivs[j + i1 + 1];
                    }

                    if (Position2 != par[i2])
                    {
                        pts1[i2 - i1] = p2;
                        devs1[i2 - i1 - 1] = vFromComplex(new Complex(derivs[i2].x, derivs[i2].y) * (Position2 - par[i2]) / (par[i2 + 1] - par[i2]));
                        devs21[i2 - i1 - 1] = vFromComplex(new Complex(v2.x, v2.y) * (Position2 - par[i2]) / (par[i2 + 1] - par[i2]));
                    }

                    for (int j = 0; j < i2 - i1 - 1; ++j)
                    {
                        devs21[j] = derivs2[j + i1 + 1];
                    }
                }
            }
            pts2[0] = p2;

            if (Position2 != par[i2])
            {
                devs2[0] = vFromComplex(new Complex(v2.x, v2.y) * (par[i2 + 1] - Position2) / (par[i2 + 1] - par[i2]));
                devs22[0] = vFromComplex(new Complex(derivs2[i2].x, derivs2[i2].y) * (par[i2 + 1] - Position2) / (par[i2 + 1] - par[i2]));
            }
            else
            {
                devs2[0] = derivs[i2];
                devs22[0] = derivs2[i2];
            }

            if (Position1 != par[i1 + 1])
            {
                devs2[points.Length - i2 + i1 - 1] = vFromComplex(new Complex(derivs[i1].x, derivs[i1].y) * (Position1 - par[i1]) / (par[i1 + 1] - par[i1]));
                devs22[points.Length - i2 + i1 - 1] = vFromComplex(new Complex(v1.x, v1.y) * (Position1 - par[i1]) / (par[i1 + 1] - par[i1]));
            }
            else
            {
                devs2[points.Length - i2 + i1 - 1] = derivs[i1];
                devs22[points.Length - i2 + i1 - 1] = derivs2[i1];
            }

            for (int j = 1; j < points.Length - i2 - 1; ++j)
            {
                pts2[j] = points[j + i2];
                devs2[j] = derivs[j + i2];
            }
            for (int j = points.Length - i2 - 1; j < points.Length - i2 + i1 - 1; ++j)
            {
                pts2[j] = points[j - points.Length + i2 + 1];
                devs2[j] = derivs[j - points.Length + i2 + 1];
            }
            pts2[points.Length - i2 + i1 - 1] = points[i1];
            pts2[points.Length - i2 + i1] = p1;
            for (int j = 1; j < points.Length - i2; ++j)
            {
                devs22[j] = derivs2[j + i2];
            }
            for (int j = points.Length - i2; j < points.Length - i2 + i1 - 1; ++j)
            {
                devs22[j] = derivs2[j - points.Length + i2 + 1];
            }

            PHCurve seg0 = new PHCurve();
            PHCurve seg1 = new PHCurve();
            seg0.Set(pts1, devs1, devs21);
            seg1.Set(pts2, devs2, devs22);
            segs[0] = seg0;
            segs[1] = seg1;
            return segs;
        }
        public override bool IsClosed
        {
            get
            {
                return points[0] == points[points.Length - 1];
            }
        }
        public override void Trim(double StartPos, double EndPos)
        {
            ICurve[] s;
            if (StartPos == 0.0)
            {
                s = Split(EndPos);
                this.points = (GeoPoint[])((PHCurve)s[0]).points.Clone();
                this.derivs = (GeoVector[])((PHCurve)s[0]).derivs.Clone();
                this.derivs2 = (GeoVector[])((PHCurve)s[0]).derivs2.Clone();
                this.cpoints = (GeoPoint[,])((PHCurve)s[0]).cpoints.Clone();
                this.a = (Complex[])((PHCurve)s[0]).a.Clone();
                this.b = (Complex[])((PHCurve)s[0]).b.Clone();
                this.k = (Complex[])((PHCurve)s[0]).k.Clone();
                this.c = (Complex[])((PHCurve)s[0]).c.Clone();
                this.t = (double[])((PHCurve)s[0]).t.Clone();
                this.par = (double[])((PHCurve)s[0]).par.Clone();
            }
            if (EndPos == 1.0)
            {
                s = Split(StartPos);
                this.points = (GeoPoint[])((PHCurve)s[1]).points.Clone();
                this.derivs = (GeoVector[])((PHCurve)s[1]).derivs.Clone();
                this.derivs2 = (GeoVector[])((PHCurve)s[1]).derivs2.Clone();
                this.cpoints = (GeoPoint[,])((PHCurve)s[1]).cpoints.Clone();
                this.a = (Complex[])((PHCurve)s[1]).a.Clone();
                this.b = (Complex[])((PHCurve)s[1]).b.Clone();
                this.k = (Complex[])((PHCurve)s[1]).k.Clone();
                this.c = (Complex[])((PHCurve)s[1]).c.Clone();
                this.t = (double[])((PHCurve)s[1]).t.Clone();
                this.par = (double[])((PHCurve)s[1]).par.Clone();
            }
            else
            {
                s = Split(StartPos, EndPos);
                this.points = (GeoPoint[])((PHCurve)s[0]).points.Clone();
                this.derivs = (GeoVector[])((PHCurve)s[0]).derivs.Clone();
                this.derivs2 = (GeoVector[])((PHCurve)s[0]).derivs2.Clone();
                this.cpoints = (GeoPoint[,])((PHCurve)s[0]).cpoints.Clone();
                this.a = (Complex[])((PHCurve)s[0]).a.Clone();
                this.b = (Complex[])((PHCurve)s[0]).b.Clone();
                this.k = (Complex[])((PHCurve)s[0]).k.Clone();
                this.c = (Complex[])((PHCurve)s[0]).c.Clone();
                this.t = (double[])((PHCurve)s[0]).t.Clone();
                this.par = (double[])((PHCurve)s[0]).par.Clone();
            }
        }
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                for (int i = 0; i < points.Length; ++i)
                {
                    points[i] = m * points[i];
                    derivs[i] = m * derivs[i];
                    derivs2[i] = m * derivs2[i];
                }
                base.InvalidateSecondaryData();
            }
        }
        public override IGeoObject Clone()
        {
            PHCurve res = new PHCurve();
            res.points = (GeoPoint[])points.Clone();
            res.derivs = (GeoVector[])derivs.Clone();
            res.derivs2 = (GeoVector[])derivs2.Clone();
            res.cpoints = (GeoPoint[,])cpoints.Clone();
            res.a = (Complex[])a.Clone();
            res.b = (Complex[])b.Clone();
            res.k = (Complex[])k.Clone();
            res.c = (Complex[])c.Clone();
            res.t = (double[])t.Clone();
            res.par = (double[])par.Clone();
            res.precis = precis;
            return res;
        }
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            PHCurve other = ToCopyFrom as PHCurve;
            points = (GeoPoint[])other.points.Clone();
            derivs = (GeoVector[])other.derivs.Clone();
            derivs2 = (GeoVector[])other.derivs2.Clone();
            cpoints = (GeoPoint[,])cpoints.Clone();
            a = (Complex[])other.a.Clone();
            b = (Complex[])other.b.Clone();
            k = (Complex[])other.k.Clone();
            c = (Complex[])other.c.Clone();
            t = (double[])other.t.Clone();
            par = (double[])other.par.Clone();
            precis = other.precis;
        }
        #endregion
        #region helper
        private double[] knotParams()
        {
            par = new double[t.Length];
            par[0] = 0;
            par[t.Length - 1] = 1;
            for (int k = 1; k < t.Length - 1; ++k)
                for (int j = 1; j <= k; ++j)
                    par[k] += t[j];
            return par;
        }
        private void calculateSpacings(GeoPoint[] pts)
        {
            t = new double[pts.Length];
            t[0] = 0;
            double s = 0;
            for (int i = 1; i < pts.Length; ++i)
            {
                t[i] = (new Complex(pts[i].x, pts[i].y) - new Complex(pts[i - 1].x, pts[i - 1].y)).Magnitude;
                s += t[i];
            }
            for (int i = 1; i < pts.Length; ++i)
            {
                t[i] /= s;
            }
        }
        private void calculateDerivs(GeoPoint[] pts)
        {
            int n = pts.Length - 1;
            //für den iterativen Ansatz: Complex[] z = new Complex[n+1];
            Complex[] a = new Complex[n + 1];
            Complex[] b = new Complex[n + 1];
            Complex[] c = new Complex[n + 1];
            Complex[] r = new Complex[n + 1];
            Complex[] beta = new Complex[n + 1];
            Complex[] rho = new Complex[n + 1];
            Complex[] eta = new Complex[n + 1];
            Complex[] d = new Complex[n + 1];
            Complex[] q = new Complex[n + 1];
            Complex m, theta;
            derivs2 = new GeoVector[n + 1];
            // Ermittlung der Ableitungen für einen kubischen C2-"Standard"-Spline
            // mit nicht-uniformen Knoten nach Farouki 328 ff.
            // Die gelieferten Ableitungen sind nur sinnvoll, wenn sich in dem zu den Durchgangspunkten
            // gehörigen Polygonzug keine zu spitzen Dreiecke ergeben!
            if (!IsClosed)
            {
                //Lösungsverfahren für offene Splines mit quadratischen Endsegmenten, Farouki 330 ff.
                a[0] = 0;
                b[0] = 1;
                c[0] = 1;
                for (int j = 1; j < n; ++j)
                {
                    a[j] = t[j + 1];
                    b[j] = 2 * (t[j] + t[j + 1]);
                    c[j] = t[j];
                }
                a[n] = 1;
                b[n] = 1;
                c[n] = 0;
                r[0] = 2 * new Complex((pts[1] - pts[0]).x, (pts[1] - pts[0]).y) / t[1];
                r[n] = 2 * new Complex((pts[n] - pts[n - 1]).x, (pts[n] - pts[n - 1]).y) / t[n];
                for (int j = 1; j <= n - 1; ++j)
                {
                    r[j] = 3 * ((t[j] / t[j + 1]) * new Complex((pts[j + 1] - pts[j]).x, (pts[j + 1] - pts[j]).y) + (t[j + 1] / t[j]) * new Complex((pts[j] - pts[j - 1]).x, (pts[j] - pts[j - 1]).y));
                }
                beta[0] = b[0];
                rho[0] = r[0];
                for (int k = 0; k <= n - 1; ++k)
                {
                    m = a[k + 1] / beta[k];
                    beta[k + 1] = b[k + 1] - m * c[k];
                    rho[k + 1] = r[k + 1] - m * rho[k];
                }
                d[n] = (rho[n] / beta[n]);
                for (int k = n - 1; k >= 1; --k)
                    d[k] = ((rho[k] - c[k] * d[k + 1]) / beta[k]);
                d[0] = ((rho[0] - c[0] * d[1]) / beta[0]) * t[1];//Multiplikation mit Segmentlänge!
                derivs2[0] = vFromComplex(d[1] * t[1]);
                for (int k = 1; k <= n - 1; ++k)
                {
                    derivs2[k] = vFromComplex(d[k + 1] * t[k + 1]);
                    d[k] *= t[k + 1];
                }
                d[n] *= t[n];



            }
            else
            {
                //Lösungsverfahren für geschlossene Splines mit periodischen Endbedingungen, Farouki 330 ff.
                a[0] = t[1];
                b[0] = 2 * (t[n] + t[1]);
                c[0] = t[n];
                a[n] = a[0];
                b[n] = b[0];
                c[n] = c[0];
                for (int j = 1; j <= n - 1; ++j)
                {
                    a[j] = t[j + 1];
                    b[j] = 2 * (t[j] + t[j + 1]);
                    c[j] = t[j];
                }
                r[0] = 3 * ((t[n] / t[1]) * new Complex((pts[1] - pts[0]).x, (pts[1] - pts[0]).y) + (t[1] / t[n]) * new Complex((pts[0] - pts[n - 1]).x, (pts[0] - pts[n - 1]).y));
                r[n] = r[0];
                for (int j = 1; j <= n - 1; ++j)
                {
                    r[j] = 3 * ((t[j] / t[j + 1]) * new Complex((pts[j + 1] - pts[j]).x, (pts[j + 1] - pts[j]).y) + (t[j + 1] / t[j]) * new Complex((pts[j] - pts[j - 1]).x, (pts[j] - pts[j - 1]).y));
                }
                beta[0] = b[0];
                rho[0] = r[0];
                eta[0] = a[0];
                for (int k = 0; k <= n - 3; ++k)
                {
                    m = a[k + 1] / beta[k];
                    beta[k + 1] = b[k + 1] - m * c[k];
                    rho[k + 1] = r[k + 1] - m * rho[k];
                    eta[k + 1] = -m * eta[k];
                }
                eta[n - 2] += c[n - 2];
                m = a[n - 1] / beta[n - 2];
                beta[n - 1] = b[n - 1] - m * eta[n - 2];
                rho[n - 1] = r[n - 1] - m * rho[n - 2];
                theta = c[n - 1];
                for (int k = 0; k <= n - 2; ++k)
                {
                    m = theta / beta[k];
                    beta[n - 1] -= m * eta[k];
                    rho[n - 1] -= m * rho[k];
                    theta = -m * c[k];
                }
                d[n - 1] = (rho[n - 1] / beta[n - 1]);
                d[n - 2] = ((rho[n - 2] - eta[n - 2] * d[n - 1]) / beta[n - 2]);
                for (int k = n - 3; k >= 1; --k)
                    d[k] = ((rho[k] - c[k] * d[k + 1] - eta[k] * d[n - 1]) / beta[k]);
                d[0] = ((rho[0] - c[0] * d[1] - eta[0] * d[n - 1]) / beta[0]);
                derivs2[0] = vFromComplex(d[1] * t[1]);//Multiplikation mit Segmentlänge!
                derivs2[n] = derivs2[0];
                d[n] = d[0];
                for (int k = 1; k <= n - 2; ++k)
                {
                    derivs2[k] = vFromComplex(d[k + 1] * t[k + 1]);
                    d[k] *= t[k + 1];
                }
                derivs2[n - 1] = vFromComplex(d[n] * t[n]);
                d[n - 1] *= t[n];
                d[0] *= t[1];
                d[n] *= t[n];
            }


            for (int k = 0; k <= n; ++k)
            {
                derivs[k] = vFromComplex(d[k]);
            }


            // für den iterativen Ansatz:
            //Ermittlung der Startapproximation z
            /*
            q[1] = 6 * new Complex((pts[1] - pts[0]).x, (pts[1] - pts[0]).y) - (d[0]+d[1]).SquareRoot();
            for (int i = 2; i <= n; ++i)
            {
                q[i] = (6 * new Complex((pts[i] - pts[i-1]).x, (pts[i] - pts[i-1]).y) - (d[i-1]+d[i])).SquareRoot();
                if (q[i].Real * q[i - 1].Real + q[i].Imaginary* q[i - 1].Imaginary< 0)
                    q[i] *= -1;
            }

            a[1] = 1;
            b[1] = 0;
            c[1] = 0;
            for (int j = 2; j < n; ++j)
            {
                a[j] = 1;
                b[j] = 6;
                c[j] = 1;
            }
            a[n] = 0;
            b[n] = 0;
            c[n] = 1;
            r[1] = 0.5 * q[1].SquareRoot();
            r[n] = 0.5 * q[n].SquareRoot();
            r[2] = 4 * q[2].SquareRoot() - r[1];
            r[n - 1] = 4 * q[n - 1].SquareRoot() - r[n];
            for (int j = 3; j < n - 1; ++j)
            {
                r[j] = 4 * q[j].SquareRoot();
            }
            z[1] = r[1];
            z[n] = r[n];
            
            beta[2] = b[2];
            rho[2] = r[2];
            for (int k = 2; k < n - 1; ++k)
            {
                m = a[k + 1] / beta[k];
                beta[k + 1] = b[k + 1] - m * c[k];
                rho[k + 1] = r[k + 1] - m * rho[k];
            }
            z[n - 1] = rho[n - 1] / beta[n - 1];
            for (int k = n - 2; k >= 2; --k)
                z[k] = (rho[k] - c[k] * z[k + 1]) / beta[k];
            


                // Newton-Raphson-Verfahren für festgelegte Startapproximation z (s.o.)
                for (int i = 0; i < p; ++i)
                {
                    //Inkrementvektor ermitteln
                    a[0] = 0;
                    b[0] = 26 * z[1] - 2 * z[2];
                    c[0] = 2 * z[2] - 2 * z[1];
                    for (int j = 1; j < n - 1; ++j)
                    {
                        a[j] = 6 * z[j - 1] + 13 * z[j] + z[j + 1];
                        b[j] = 13 * z[j - 1] + 54 * z[j] + 13 * z[j + 1];
                        c[j] = z[j - 1] + 13 * z[j] + 6 * z[j + 1];
                    }
                    a[n - 1] = 2 * z[n - 1] - 2 * z[n];
                    b[n - 1] = 26 * z[n] - 2 * z[n - 1];
                    c[n - 1] = 0;
                    r[0] = -(13 * z[1].Square() + z[2].Square() - 2 * z[1] * z[2] - 12 * new Complex((pts[1] - pts[0]).x, (pts[1] - pts[0]).y));
                    r[n - 1] = -(13 * z[n].Square() + z[n - 1].Square() - 2 * z[n] * z[n - 1] - 12 * new Complex((pts[n] - pts[n - 1]).x, (pts[n] - pts[n - 1]).y));
                    for (int j = 1; j < n - 1; ++j)
                    {
                        r[j] = 3 * z[j - 1].Square() + 27 * z[j].Square() + 3 * z[j + 1].Square() + z[j - 1] * z[j + 1] + 13 * z[j - 1] * z[j] + 13 * z[j] * z[j + 1] - 60 * new Complex((pts[j] - pts[j - 1]).x, (pts[j] - pts[j - 1]).y);
                    }
                    beta[0] = b[0];
                    rho[0] = r[0];
                    for (int k = 0; k < n - 1; ++k)
                    {
                        m = a[k + 1] / beta[k];
                        beta[k + 1] = b[k + 1] - m * c[k];
                        rho[k + 1] = r[k + 1] - m * rho[k];
                    }
                    d[n - 1] = rho[n - 1] / beta[n - 1];
                    for (int k = n - 2; k >= 0; --k)
                        d[k] = (rho[k] - c[k] * d[k + 1]) / beta[k];
                    // N-R-Iterationsschritt
                    for (int j = 1; j <= n; ++j)
                        z[j] += d[j-1];
                }
            
            //Setzen der Ableitungsvektoren
            derivs[0] = vFromComplex(0.25 * ((3 * z[1] - z[2]).Square())*t[0]);
            for (int i = 0; i < n - 1; ++i)
                derivs[i + 1] = vFromComplex(0.25 * ((z[i] + z[i+1]).Square())*t[i+1]);
            derivs[n] = vFromComplex(0.25 * ((3 * z[n] - z[n-1]).Square())*t[n]);
        */
        }
        private void init(int i)
        {
            // Farouki, S. 528 ff.
            Complex d0, d1, rho1, rho2, rhosqr, alpha11, alpha12, alpha21, alpha22;
            Complex mu111, mu112, mu121, mu122, mu211, mu212, mu221, mu222;
            Complex a11, a12, a21, a22, b11, b12, b21, b22;
            SortedList<double, Pair<Complex, Complex>> ab = new SortedList<double, Pair<Complex, Complex>>(4);
            d0 = stdDeriv(derivs[i], i);
            d1 = stdDeriv(derivs2[i], i);
            rho1 = (d0 / d1).SquareRoot();
            rho2 = -rho1;
            rhosqr = d0 / d1;
            alpha11 = quadSol1(-3 * (1 + rho1), 6 * rhosqr + 2 * rho1 + 6 - 30 / d1);
            alpha12 = quadSol2(-3 * (1 + rho1), 6 * rhosqr + 2 * rho1 + 6 - 30 / d1);
            alpha21 = quadSol1(-3 * (1 + rho2), 6 * rhosqr + 2 * rho2 + 6 - 30 / d1);
            alpha22 = quadSol2(-3 * (1 + rho2), 6 * rhosqr + 2 * rho2 + 6 - 30 / d1);
            mu111 = quadSol1(-alpha11, rho1);
            mu112 = quadSol2(-alpha11, rho1);
            mu121 = quadSol1(-alpha12, rho1);
            mu122 = quadSol2(-alpha12, rho1);
            mu211 = quadSol1(-alpha21, rho2);
            mu212 = quadSol2(-alpha21, rho2);
            mu221 = quadSol1(-alpha22, rho2);
            mu222 = quadSol2(-alpha22, rho2);
            a11 = mu111 / (mu111 + 1);
            b11 = mu112 / (mu112 + 1);
            a12 = mu121 / (mu121 + 1);
            b12 = mu122 / (mu122 + 1);
            a21 = mu211 / (mu211 + 1);
            b21 = mu212 / (mu212 + 1);
            a22 = mu221 / (mu221 + 1);
            b22 = mu222 / (mu222 + 1);
            ab[absRotInd(a11, b11)] = new Pair<Complex, Complex>(a11, b11);
            ab[absRotInd(a12, b12)] = new Pair<Complex, Complex>(a12, b12);
            ab[absRotInd(a21, b21)] = new Pair<Complex, Complex>(a21, b21);
            ab[absRotInd(a22, b22)] = new Pair<Complex, Complex>(a22, b22);
            a[i] = ab.Values[0].First;
            b[i] = ab.Values[0].Second;
            k[i] = d0 / (a[i].Square() * b[i].Square());
            c[i] = k[i] / 30 * (a[i].Power(5.0) - 5 * a[i].Power(4.0) * b[i] + 10 * a[i].Square() * a[i] * b[i].Square());
        }
        private void calculateCPoints()
        {
            cpoints = new GeoPoint[points.Length - 1, 6];
            Complex[] p = new Complex[6];
            //DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < points.Length - 1; ++i)
            {
                p[0] = 0;
                p[1] = 1 / 5.0 * k[i] * a[i].Square() * b[i].Square();
                p[2] = p[1] - 1 / 10.0 * k[i] * a[i] * b[i] * (a[i] + b[i] - 2 * a[i] * b[i]);
                p[3] = p[2] + 1 / 30.0 * k[i] * ((a[i] + b[i] - 2 * a[i] * b[i]).Square() + 2 * a[i] * (1 - a[i]) * b[i] * (1 - b[i]));
                p[4] = p[3] - 1 / 10.0 * k[i] * (1 - a[i]) * (1 - b[i]) * (a[i] + b[i] - 2 * a[i] * b[i]);
                p[5] = 1;
                for (int j = 0; j <= 5; ++j)
                {
                    cpoints[i, j] = pFromComplex(fromStdPoint(p[j], i));
                    // dc.Add(cpoints[i, j], System.Drawing.Color.Blue, i * 10 + j);
                }
            }
        }
        public double arcLength(double param)
        {
            //Farouki S.386 f. und S. 528
            int i = 0;
            double r = 0;
            while (param > par[i + 1])
                i += 1;
            Complex[,] w = new Complex[3, i + 1];
            double[,] sigma = new double[5, i + 1];
            double[,] s = new double[6, i + 1];
            double pos = (param - par[i]) / t[i + 1];
            for (int j = 0; j <= i; ++j)
            {
                w[0, j] = k[j].SquareRoot() * a[j] * b[j];
                w[1, j] = -k[j].SquareRoot() * 0.5 * (a[j] * (1 - b[j]) + (1 - a[j]) * b[j]);
                w[2, j] = k[j].SquareRoot() * (1 - a[j]) * (1 - b[j]);
            }
            for (int j = 0; j <= i; ++j)
            {
                sigma[0, j] = w[0, j].Real * w[0, j].Real + w[0, j].Imaginary* w[0, j].Imaginary;
                sigma[1, j] = w[0, j].Real * w[1, j].Real + w[0, j].Imaginary* w[1, j].Imaginary;
                sigma[2, j] = 2 / 3.0 * (w[1, j].Real * w[1, j].Real + w[1, j].Imaginary* w[1, j].Imaginary) + 1 / 3.0 * (w[0, j].Real * w[2, j].Real + w[0, j].Imaginary* w[2, j].Imaginary);
                sigma[3, j] = w[1, j].Real * w[2, j].Real + w[1, j].Imaginary* w[2, j].Imaginary;
                sigma[4, j] = w[2, j].Real * w[2, j].Real + w[2, j].Imaginary* w[2, j].Imaginary;
            }
            for (int j = 0; j <= i; ++j)
            {
                for (int k = 1; k <= 5; ++k)
                    for (int l = 0; l < k; ++l)
                        s[k, j] += sigma[l, j] / 5.0;
            }
            for (int j = 0; j < i; ++j)
                r += (fromStdDeriv(s[5, j], j)).Magnitude;
            for (int k = 0; k <= 5; ++k)
                r += (fromStdDeriv(s[k, i] * (faculty(5) / (faculty(k) * faculty(5 - k))) * Math.Pow(1 - pos, 5 - k) * Math.Pow(pos, k), i)).Magnitude;
            return r;
        }
        public double paramSpeed(double param)
        {
            //Farouki S.386 f. und S. 528
            int i = 0;
            double r = 0;
            while (param > par[i + 1])
                i += 1;
            Complex[,] w = new Complex[3, i + 1];
            double[,] sigma = new double[5, i + 1];
            double[,] s = new double[6, i + 1];
            double pos = (param - par[i]) / t[i + 1];
            for (int j = 0; j <= i; ++j)
            {
                w[0, j] = k[j].SquareRoot() * a[j] * b[j];
                w[1, j] = -k[j].SquareRoot() * 0.5 * (a[j] * (1 - b[j]) + (1 - a[j]) * b[j]);
                w[2, j] = k[j].SquareRoot() * (1 - a[j]) * (1 - b[j]);
            }
            for (int j = i; j == i; ++j)
            {
                sigma[0, j] = w[0, j].Real * w[0, j].Real + w[0, j].Imaginary* w[0, j].Imaginary;
                sigma[1, j] = w[0, j].Real * w[1, j].Real + w[0, j].Imaginary* w[1, j].Imaginary;
                sigma[2, j] = 2 / 3.0 * (w[1, j].Real * w[1, j].Real + w[1, j].Imaginary* w[1, j].Imaginary) + 1 / 3.0 * (w[0, j].Real * w[2, j].Real + w[0, j].Imaginary* w[2, j].Imaginary);
                sigma[3, j] = w[1, j].Real * w[2, j].Real + w[1, j].Imaginary* w[2, j].Imaginary;
                sigma[4, j] = w[2, j].Real * w[2, j].Real + w[2, j].Imaginary* w[2, j].Imaginary;
            }
            for (int k = 0; k <= 4; ++k)
                r += (fromStdDeriv(sigma[k, i] * (faculty(4) / (faculty(k) * faculty(4 - k))) * Math.Pow(1 - pos, 4 - k) * Math.Pow(pos, k), i)).Magnitude;
            return r;
        }
        private double faculty(int n)
        {
            if (n == 0)
                return 1;
            int r = 1;
            for (int i = 1; i <= n; ++i)
                r *= i;
            return r;
        }
        public Complex stdPoint(GeoPoint p, int i)
        {
            //Normierung auf Einheitsintervall
            Complex z = new Complex(p.x - this.points[i].x, p.y - this.points[i].y);
            return z / new Complex(this.points[i + 1].x - this.points[i].x, this.points[i + 1].y - this.points[i].y);
        }
        public Complex fromStdPoint(Complex z, int i)
        {
            Complex sp = new Complex(this.points[i].x, this.points[i].y);
            Complex ep = new Complex(this.points[i + 1].x, this.points[i + 1].y);
            return z * (ep - sp) + sp;
        }
        public Complex stdDeriv(GeoVector v, int i)
        {
            //Normierung auf Einheitsintervall
            Complex z = new Complex(v.x, v.y);
            return z / new Complex(this.points[i + 1].x - this.points[i].x, this.points[i + 1].y - this.points[i].y);
        }
        public Complex fromStdDeriv(Complex z, int i)
        {
            Complex sp = new Complex(this.points[i].x, this.points[i].y);
            Complex ep = new Complex(this.points[i + 1].x, this.points[i + 1].y);
            return z * (ep - sp);
        }
        public GeoPoint pFromComplex(Complex z)
        {
            return new GeoPoint(z.Real, z.Imaginary);
        }
        public GeoVector vFromComplex(Complex z)
        {
            return new GeoVector(z.Real, z.Imaginary, 0);
        }
        private Complex quadSol1(Complex p, Complex q)
        {
            return -p / 2 + (p.Square() / 4 - q).SquareRoot();
        }
        private Complex quadSol2(Complex p, Complex q)
        {
            return -p / 2 - (p.Square() / 4 - q).SquareRoot();
        }
        //Absoluter Rotationsindex nach Farouki, S. 531 ff.
        private double absRotInd(Complex a, Complex b)
        {
            if (a.Imaginary>= 0 && b.Imaginary>= 0 || a.Imaginary<= 0 && b.Imaginary<= 0)
                return (Math.Abs((a - 1).Atan().Real - a.Atan().Real) + Math.Abs((b - 1).Atan().Real - b.Atan().Real)) / Math.PI;
            else
            {
                Complex t1c = quadSol1(-2 * (a * b).Imaginary/ (a + b).Imaginary, (a.MagnitudeSquared() * b + b.MagnitudeSquared() * a).Imaginary/ (a + b).Imaginary);
                Complex t2c = quadSol2(-2 * (a * b).Imaginary/ (a + b).Imaginary, (a.MagnitudeSquared() * b + b.MagnitudeSquared() * a).Imaginary/ (a + b).Imaginary);
                double t1 = t1c.Real;
                double t2 = t2c.Real;
                double result = 0;
                List<double> roots = new List<double>();
                roots.Add(0);
                if (t1c.Imaginary== 0 && t1 > 0 && t1 < 1)
                    roots.Add(t1);
                if (t2c.Imaginary== 0 && t2 > 0 && t2 < 1)
                    roots.Add(t2);
                roots.Add(1);
                roots.Sort();
                for (int i = 0; i < roots.Count - 2; ++i)
                    result += Math.Abs(Math.Abs((a - roots[i + 1]).Atan().Real - (a - roots[i]).Atan().Real) - Math.Abs((b - roots[i + 1]).Atan().Real + (b - roots[i]).Atan().Real));
                return result / Math.PI;
            }
        }
        #endregion

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected PHCurve(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                switch (e.Name)
                {
                    default:
                        base.SetSerializationValue(e.Name, e.Value);
                        break;
                    case "Points":
                        points = (GeoPoint[])e.Value;
                        break;
                    case "Derivs":
                        derivs = (GeoVector[])e.Value;
                        break;
                    case "Derivs2":
                        derivs2 = (GeoVector[])e.Value;
                        break;
                    case "CPoints":
                        cpoints = (GeoPoint[,])e.Value;
                        break;
                    case "A":
                        a = (Complex[])e.Value;
                        break;
                    case "B":
                        b = (Complex[])e.Value;
                        break;
                    case "K":
                        k = (Complex[])e.Value;
                        break;
                    case "C":
                        c = (Complex[])e.Value;
                        break;
                    case "Precis":
                        precis = (int)e.Value;
                        break;
                    case "T":
                        t = (double[])e.Value;
                        break;
                    case "Par":
                        par = (double[])e.Value;
                        break;
                }
            }
        }
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            // zuerst die Hauptwerte
            info.AddValue("Points", points);
            info.AddValue("Derivs", derivs);
            info.AddValue("Derivs2", derivs2);
            info.AddValue("CPoints", cpoints);
            info.AddValue("A", a);
            info.AddValue("B", b);
            info.AddValue("K", k);
            info.AddValue("C", c);
            info.AddValue("Precis", precis);
            info.AddValue("T", t);
            info.AddValue("Par", par);
        }

        #endregion
    }



    [Serializable]
    internal class PHOffsetCurve : GeneralCurve, ISerializable
    {
        PHCurve theCurve; //Originalkurve
        double offset; //Abstand zur Originalkurve


        public PHOffsetCurve()
        {

        }
        public void Set(PHCurve curve, double off)
        {
            theCurve = (PHCurve)curve.Clone();
            offset = off;
            //DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i <= 300; ++i)
            {
                Line l = Line.Construct();
                l.SetTwoPoints(PointAt(i / 300.0), PointAt(i / 300.0) + DirectionAt(i / 300.0));
                //dc.Add(l,i);
            }
        }

        #region Überschriebene Methoden
        protected override double[] GetBasePoints()
        {
            return new double[] { 0.0, 0.5, 1.0 };
        }
        public override GeoVector DirectionAt(double position)
        {
            /*int i = 0;
            while (position > theCurve.par[i + 1])
                i += 1;
            double p = paramSpeed(position);
            double p1 = paramSpeedDeriv(position);
            Complex f = fromStdDeriv(1, i);
            Complex fm = fromStdDeriv(1, i).Magnitude;
            Complex apos = (position - theCurve.par[i]) / theCurve.t[i + 1] - theCurve.a[i];
            Complex bpos = (position - theCurve.par[i]) / theCurve.t[i + 1] - theCurve.b[i];
            */
            GeoVector vec = (paramSpeed(position) * theCurve.Direction2At(position) - paramSpeedDeriv(position) * theCurve.DirectionAt(position)) / (paramSpeed(position) * paramSpeed(position));
            //GeoVector v = vFromComplex(f * theCurve.k[i] * (apos * bpos).Square() + offset * ((f * 2 * theCurve.k[i] * apos * bpos * (apos + bpos) * p - f * theCurve.k[i] * (apos * bpos).Square() * p1) / (fm * p * p)));
            //GeoVector v = vFromComplex((fromStdDeriv(theCurve.k[i] * (apos * bpos).Square(), i) + offset * (fromStdDeriv(-theCurve.k[i] * (apos * bpos).Square() * paramSpeedDeriv(position), i) + fromStdDeriv(paramSpeed(position) * 2 * theCurve.k[i] * apos * bpos * (bpos + apos), i) / fromStdDeriv((Complex)(paramSpeed(position) * paramSpeed(position)),i))));
            GeoVector v = theCurve.DirectionAt(position) - offset * vFromComplex(Complex.ImaginaryOne * new Complex(vec.x, vec.y));
            return v;
        }
        public override GeoPoint PointAt(double position)
        {
            int i = 0;
            while (position > theCurve.par[i + 1])
                i += 1;
            Complex apos = (position - theCurve.par[i]) / theCurve.t[i + 1] - theCurve.a[i], bpos = (position - theCurve.par[i]) / theCurve.t[i + 1] - theCurve.b[i];
            GeoPoint p = pFromComplex(fromStdPoint(theCurve.k[i] / 30 * (apos.Power(5.0) - 5 * apos.Power(4.0) * bpos + 10 * apos.Square() * apos * bpos.Square()) + theCurve.c[i], i) + offset / paramSpeed(position) * fromStdDeriv(-theCurve.k[i] * (apos * bpos).Square() * Complex.ImaginaryOne, i));
            return p;
        }
        public override void Reverse()
        {
            theCurve.Reverse();
        }
        public override ICurve[] Split(double Position)
        {
            ICurve[] segs = new ICurve[2];
            ICurve[] tmp = theCurve.Split(Position);
            PHOffsetCurve seg0 = new PHOffsetCurve();
            PHOffsetCurve seg1 = new PHOffsetCurve();
            seg0.Set((PHCurve)tmp[0], offset);
            seg1.Set((PHCurve)tmp[1], offset);
            segs[0] = seg0;
            segs[1] = seg1;
            return segs;
        }
        public override ICurve[] Split(double Position1, double Position2)
        {
            ICurve[] segs = new ICurve[2];
            ICurve[] tmp = theCurve.Split(Position1, Position2);
            PHOffsetCurve seg0 = new PHOffsetCurve();
            PHOffsetCurve seg1 = new PHOffsetCurve();
            seg0.Set((PHCurve)tmp[0], offset);
            seg1.Set((PHCurve)tmp[1], offset);
            segs[0] = seg0;
            segs[1] = seg1;
            return segs;
        }
        public override bool IsClosed
        {
            get
            {
                return theCurve.IsClosed;
            }
        }
        public override void Trim(double StartPos, double EndPos)
        {
            ICurve[] s;
            if (IsClosed)
            {
                s = ((PHCurve)theCurve.Clone()).Split(StartPos, EndPos);
                this.theCurve = (PHCurve)s[0];
            }
            else
            {
                if (StartPos == 0.0)
                {
                    s = ((PHCurve)theCurve.Clone()).Split(EndPos);
                    this.theCurve = (PHCurve)s[0];
                }
                if (EndPos == 1.0)
                {
                    s = ((PHCurve)theCurve.Clone()).Split(StartPos);
                    this.theCurve = (PHCurve)s[1];
                }
                else
                {
                    s = ((PHCurve)((PHCurve)theCurve.Clone()).Split(EndPos)[0]).Split(StartPos / EndPos);
                    this.theCurve = (PHCurve)s[0];
                }
            }
        }
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                ((PHCurve)theCurve.Clone()).Modify(m);
            }
        }
        public override IGeoObject Clone()
        {
            PHOffsetCurve res = new PHOffsetCurve();
            res.theCurve = (PHCurve)theCurve.Clone();
            res.offset = offset;
            return res;
        }
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            PHOffsetCurve other = ToCopyFrom as PHOffsetCurve;
            theCurve = (PHCurve)other.theCurve.Clone();
            offset = other.offset;
        }
        #endregion
        #region helper

        public double arcLength(double param)
        {
            int i = 0;
            double r = 0;
            while (param > theCurve.par[i + 1])
                i += 1;
            Complex[,] w = new Complex[3, i + 1];
            double[,] sigma = new double[5, i + 1];
            double[,] s = new double[6, i + 1];
            double pos = (param - theCurve.par[i]) / theCurve.t[i + 1];
            for (int j = 0; j <= i; ++j)
            {
                w[0, j] = theCurve.k[j].SquareRoot() * theCurve.a[j] * theCurve.b[j];
                w[1, j] = -theCurve.k[j].SquareRoot() * 0.5 * (theCurve.a[j] * (1 - theCurve.b[j]) + (1 - theCurve.a[j]) * theCurve.b[j]);
                w[2, j] = theCurve.k[j].SquareRoot() * (1 - theCurve.a[j]) * (1 - theCurve.b[j]);
            }
            for (int j = 0; j <= i; ++j)
            {
                sigma[0, j] = w[0, j].Real * w[0, j].Real + w[0, j].Imaginary* w[0, j].Imaginary;
                sigma[1, j] = w[0, j].Real * w[1, j].Real + w[0, j].Imaginary* w[1, j].Imaginary;
                sigma[2, j] = 2 / 3.0 * (w[1, j].Real * w[1, j].Real + w[1, j].Imaginary* w[1, j].Imaginary) + 1 / 3.0 * (w[0, j].Real * w[2, j].Real + w[0, j].Imaginary* w[2, j].Imaginary);
                sigma[3, j] = w[1, j].Real * w[2, j].Real + w[1, j].Imaginary* w[2, j].Imaginary;
                sigma[4, j] = w[2, j].Real * w[2, j].Real + w[2, j].Imaginary* w[2, j].Imaginary;
            }
            for (int j = 0; j <= i; ++j)
            {
                for (int k = 1; k <= 5; ++k)
                    for (int l = 0; l < k; ++l)
                        s[k, j] += sigma[l, j] / 5.0;
            }
            for (int j = 0; j < i; ++j)
                r += (fromStdDeriv(s[5, j], j)).Magnitude;
            for (int k = 0; k <= 5; ++k)
                r += (fromStdDeriv(s[k, i] * (faculty(5) / (faculty(k) * faculty(5 - k))) * Math.Pow(1 - pos, 5 - k) * Math.Pow(pos, k), i)).Magnitude;
            return r;
        }
        public double paramSpeed(double param)
        {
            GeoVector d = theCurve.DirectionAt(param);
            return Math.Sqrt(d.x * d.x + d.y * d.y);
        }
        public double paramSpeedDeriv(double param)
        {
            GeoVector d = theCurve.DirectionAt(param);
            GeoVector dd = theCurve.Direction2At(param);
            return 1 / (2 * Math.Sqrt(d.x * d.x + d.y * d.y)) * (2 * d.x * dd.x + 2 * d.y * dd.y);
        }
        /*public double paramSpeed(double param)
        {
            int i = 0;
            double r = 0;
            while (param > theCurve.par[i + 1])
                i += 1;
            Complex[,] w = new Complex[3, i + 1];
            double[,] sigma = new double[5, i + 1];
            double[,] s = new double[6, i + 1];
            double pos = (param - theCurve.par[i]) / theCurve.t[i + 1];
            for (int j = 0; j <= i; ++j)
            {
                w[0, j] = theCurve.k[j].SquareRoot() * theCurve.a[j] * theCurve.b[j];
                w[1, j] = -theCurve.k[j].SquareRoot() * 0.5 * (theCurve.a[j] * (1 - theCurve.b[j]) + (1 - theCurve.a[j]) * theCurve.b[j]);
                w[2, j] = theCurve.k[j].SquareRoot() * (1 - theCurve.a[j]) * (1 - theCurve.b[j]);
            }
            for (int j = i; j == i; ++j)
            {
                sigma[0, j] = w[0, j].Real * w[0, j].Real + w[0, j].Imaginary* w[0, j].Imaginary;
                sigma[1, j] = w[0, j].Real * w[1, j].Real + w[0, j].Imaginary* w[1, j].Imaginary;
                sigma[2, j] = 2 / 3.0 * (w[1, j].Real * w[1, j].Real + w[1, j].Imaginary* w[1, j].Imaginary) + 1 / 3.0 * (w[0, j].Real * w[2, j].Real + w[0, j].Imaginary* w[2, j].Imaginary);
                sigma[3, j] = w[1, j].Real * w[2, j].Real + w[1, j].Imaginary* w[2, j].Imaginary;
                sigma[4, j] = w[2, j].Real * w[2, j].Real + w[2, j].Imaginary* w[2, j].Imaginary;
            }
            for (int k = 0; k <= 4; ++k)
                r += (fromStdDeriv(sigma[k, i] * (faculty(4) / (faculty(k) * faculty(4 - k))) * Math.Pow(1 - pos, 4 - k) * Math.Pow(pos, k), i)).Magnitude;
            return r;
        }
        public double paramSpeedDeriv(double param)
        {
            int i = 0;
            double r = 0;
            while (param > theCurve.par[i + 1])
                i += 1;
            Complex[,] w = new Complex[3, i + 1];
            double[,] sigma = new double[5, i + 1];
            double[,] s = new double[4, i + 1];
            double pos = (param - theCurve.par[i]) / theCurve.t[i + 1];
            for (int j = 0; j <= i; ++j)
            {
                w[0, j] = theCurve.k[j].SquareRoot() * theCurve.a[j] * theCurve.b[j];
                w[1, j] = -theCurve.k[j].SquareRoot() * 0.5 * (theCurve.a[j] * (1 - theCurve.b[j]) + (1 - theCurve.a[j]) * theCurve.b[j]);
                w[2, j] = theCurve.k[j].SquareRoot() * (1 - theCurve.a[j]) * (1 - theCurve.b[j]);
            }
            for (int j = i; j == i; ++j)
            {
                sigma[0, j] = w[0, j].Real * w[0, j].Real + w[0, j].Imaginary* w[0, j].Imaginary;
                sigma[1, j] = w[0, j].Real * w[1, j].Real + w[0, j].Imaginary* w[1, j].Imaginary;
                sigma[2, j] = 2 / 3.0 * (w[1, j].Real * w[1, j].Real + w[1, j].Imaginary* w[1, j].Imaginary) + 1 / 3.0 * (w[0, j].Real * w[2, j].Real + w[0, j].Imaginary* w[2, j].Imaginary);
                sigma[3, j] = w[1, j].Real * w[2, j].Real + w[1, j].Imaginary* w[2, j].Imaginary;
                sigma[4, j] = w[2, j].Real * w[2, j].Real + w[2, j].Imaginary* w[2, j].Imaginary;
            }
            for (int j = 0; j <= 3; ++j)
            {
                s[j, i] = sigma[j + 1, i] - sigma[j,i];
            }
            for (int k = 0; k <= 3; ++k)
                r += (fromStdDeriv(s[k, i] * (faculty(3) / (faculty(k) * faculty(3 - k))) * Math.Pow(1 - pos, 3 - k) * Math.Pow(pos, k), i)).Magnitude;
            r *= 4;
            return r;
        }*/
        public double curvature(double param)
        {
            int i = 0;
            while (param > theCurve.par[i + 1])
                i += 1;
            double pos = (param - theCurve.par[i]) / theCurve.t[i + 1];
            Complex apos = (param - theCurve.par[i]) / theCurve.t[i + 1] - theCurve.a[i], bpos = (param - theCurve.par[i]) / theCurve.t[i + 1] - theCurve.b[i];
            Complex deriv1 = theCurve.k[i] * (apos * bpos).Square();
            Complex deriv2 = 2 * theCurve.k[i] * apos * bpos * (apos + bpos);
            /*double d1arg = deriv1.Atan().Real;
            double d2arg = deriv2.Atan().Real;
            if (d1arg < 0)
                d1arg += 2 * Math.PI;
            if (d2arg < 0)
                d2arg += 2 * Math.PI;
            double sin = Math.Sin(d2arg - d1arg);
            */
            return (deriv1.Real * deriv2.Imaginary- deriv1.Imaginary* deriv2.Real) / (paramSpeed(param) * paramSpeed(param) * paramSpeed(param));
        }
        private double faculty(int n)
        {
            if (n == 0)
                return 1;
            int r = 1;
            for (int i = 1; i <= n; ++i)
                r *= i;
            return r;
        }
        private Complex stdPoint(GeoPoint p, int i)
        {
            Complex z = new Complex(p.x - theCurve.points[i].x, p.y - theCurve.points[i].y);
            return z / new Complex(theCurve.points[i + 1].x - theCurve.points[i].x, theCurve.points[i + 1].y - theCurve.points[i].y);
        }
        private Complex fromStdPoint(Complex z, int i)
        {
            Complex sp = new Complex(theCurve.points[i].x, theCurve.points[i].y);
            Complex ep = new Complex(theCurve.points[i + 1].x, theCurve.points[i + 1].y);
            return z * (ep - sp) + sp;
        }
        private Complex stdDeriv(GeoVector v, int i)
        {
            Complex z = new Complex(v.x, v.y);
            return z / new Complex(theCurve.points[i + 1].x - theCurve.points[i].x, theCurve.points[i + 1].y - theCurve.points[i].y);
        }
        private Complex fromStdDeriv(Complex z, int i)
        {
            Complex sp = new Complex(theCurve.points[i].x, theCurve.points[i].y);
            Complex ep = new Complex(theCurve.points[i + 1].x, theCurve.points[i + 1].y);
            return z * (ep - sp);
        }
        private GeoPoint pFromComplex(Complex z)
        {
            return new GeoPoint(z.Real, z.Imaginary);
        }
        private GeoVector vFromComplex(Complex z)
        {
            return new GeoVector(z.Real, z.Imaginary, 0);
        }

        #endregion

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected PHOffsetCurve(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                switch (e.Name)
                {
                    default:
                        base.SetSerializationValue(e.Name, e.Value);
                        break;
                    case "TheCurve":
                        theCurve = (PHCurve)e.Value;
                        break;
                    case "Offset":
                        offset = (double)e.Value;
                        break;
                }
            }
        }
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            // zuerst die Hauptwerte
            info.AddValue("TheCurve", theCurve);
            info.AddValue("Offset", offset);
        }

        #endregion
    }

}
