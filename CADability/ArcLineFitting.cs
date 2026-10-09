using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability
{
    struct Tripel<P, Q, R>
    {
        private P firstElement;
        private Q secondElement;
        private R thirdElement;

        public Tripel(P first, Q second, R third)
        {
            firstElement = first;
            secondElement = second;
            thirdElement = third;
        }

        public P First
        {
            get { return firstElement; }
            set { firstElement = value; }
        }
        public Q Second
        {
            get { return secondElement; }
            set { secondElement = value; }
        }
        public R Third
        {
            get { return thirdElement; }
            set { thirdElement = value; }
        }
    }

    // Klasse zur Approximation einer 2D-Kurve durch stückweise Biarcs, bzw. Linien oder
    // Kreisbogen/Linien-Kombinationen. Die Biarcs können entweder optimal, möglichst
    // gleichmäßig (schnellerer Algorithmus), oder durch das Verhältnis der Kruemmungsradien zueinander
    // (schnellste Methode) gewählt werden.
    // Hierzu wird die Kurve entweder zunächst durch einen Polygonzug und dieser Polygonzug
    // durch einen Pfad aus stückweise Kreisbögen und Linien approximiert, oder die Kurve
    // wird direkt durch stückweise Kreisbögen und Linien approximiert und die Distanz
    // zwischen Kurve und approximierendem Spline wird über Gitterpunkte ausgerechnet.
    internal class ArcLineFitting2D
    {

        private ICurve2D curve; // Die Kurve, die approximiert werden soll.
        private bool kruemmung; // Ob die BiArcs durch das Verhältnis der Krümmungsradien zueinander erzeugt werden
        // sollen, oder durch Iteration.
        private bool lazy; // Lässt den Algorithmus bei true den optimalen Biarc suchen, bei false wird die
        // Iteration abgebrochen, sobald der Fehler unterschritten wird.
        private bool poly; // Ob die Approximierung mit Polygonzug gemacht werden soll, oder durch
        // Distanzbestimmung über Gitterpunkte.
        private int anzahlGitter; // Anzahl der Gitterpunkte bei Distanzbestimmung.
        private double maxErrorPoly; // Der maximale Fehler des Polygonzugs von der Kurve.
        private double maxErrorArc; // Der maximale Fehler des Pfads vom Polygonzug.
        // maxErrorPoly + maxErrorArc ergibt den maximalen Fehler insgesamt.
        private double maxError; // Der insgesamte Fehler.
        private int d; // Bestimmt wie viele r_i's bei der Iteration erzeugt werden. (2 * d + 1)
        private double rho; // Bestimmt, wie stark d während der Iteration verkleinert wird. Bei jedem Schritt
        // wird d mit (1 - rho) multipliziert.
        private double ru; // Obergrenze für r. r ist der Quotient alpha / beta. alpha und beta werden benutzt,
        // um den jeweiligen Mittelpunkt auszurechnen, der mit Startpunkt, Startrichtung, Endpunkt und Endrichtung
        // einen Biarc definiert.
        private double rc; // Mittlerer Wert für r.
        private double rl; // Untergrenze für r.
        private double[] inflectionPoints; // Die Wendepunkte der Kurve.
        private SortedDictionary<double, GeoPoint2D> curvePoints; // Speichert Kurvenpunkte zur Zeitersparnis.
        private Path2D approx; // Der Pfad aus Kreisbögen und Linien, der die Kurve approximiert.



        // Konstruktor für BiArc Fitting mit Polygonzugerzeugung.
        public ArcLineFitting2D(ICurve2D curve, double maxErrorPoly, double maxErrorArc, bool kruemmung, bool lazy)
        {
            this.curve = curve;
            this.kruemmung = kruemmung;
            this.lazy = lazy;
            poly = true;
            this.maxErrorPoly = maxErrorPoly;
            this.maxErrorArc = maxErrorArc;
            maxError = maxErrorArc + maxErrorPoly; // Eigentlich nicht nötig.
            curvePoints = new SortedDictionary<double, GeoPoint2D>();
            d = 10;
            rho = 0.25;
            ru = 5.0;
            rc = 1.0;
            rl = 0.2;
            inflectionPoints = curve.GetInflectionPoints();
            Array.Sort(inflectionPoints);
            this.ApproxSpline();
        }

        // Konstruktor für BiArc Fitting mit Polygonzugerzeugung.
        public ArcLineFitting2D(ICurve2D curve, double maxErrorPoly, double maxErrorArc, bool kruemmung, bool lazy, int d, double rho, double ru, double rc, double rl)
        {
            this.curve = curve;
            this.kruemmung = kruemmung;
            this.lazy = lazy;
            poly = true;
            this.maxErrorPoly = maxErrorPoly;
            this.maxErrorArc = maxErrorArc;
            maxError = maxErrorArc + maxErrorPoly; // Eigentlich nicht nötig.
            curvePoints = new SortedDictionary<double, GeoPoint2D>();
            this.d = d;
            this.rho = rho;
            if (rl < rc && rc < ru && (ru < 1.0 || rl > 1.0 || (rc < 1.0 + Precision.eps && rc > 1.0 - Precision.eps)))
            {
                this.ru = ru;
                this.rc = rc;
                this.rl = rl;
            }
            else
            {
                this.ru = 5.0;
                this.rc = 1.0;
                this.rl = 0.2;
            }
            inflectionPoints = curve.GetInflectionPoints();
            Array.Sort(inflectionPoints);
            this.ApproxSpline();
        }

        // Konstruktor für BiArc Fitting ohne Polygonzugerzeugung.
        public ArcLineFitting2D(ICurve2D curve, double maxError, bool kruemmung, bool lazy, int anzahlGitter)
        {
            this.curve = curve;
            this.kruemmung = kruemmung;
            this.lazy = lazy;
            poly = false;
            curvePoints = new SortedDictionary<double, GeoPoint2D>();
            this.anzahlGitter = anzahlGitter;
            this.maxError = maxError;
            maxErrorPoly = 0.2 * maxError; // Eigentlich nicht nötig.
            maxErrorArc = 0.8 * maxError; // Eigentlich nicht nötig.
            d = 10;
            rho = 0.25;
            ru = 5.0;
            rc = 1.0;
            rl = 0.2;
            inflectionPoints = curve.GetInflectionPoints();
            Array.Sort(inflectionPoints);
            this.ApproxSpline();
        }

        // Konstruktor für BiArc Fitting ohne Polygonzugerzeugung.
        public ArcLineFitting2D(ICurve2D curve, double maxError, bool kruemmung, bool lazy, int anzahlGitter, int d, double rho, double ru, double rc, double rl)
        {
            this.curve = curve;
            this.kruemmung = kruemmung;
            this.lazy = lazy;
            poly = false;
            curvePoints = new SortedDictionary<double, GeoPoint2D>();
            this.anzahlGitter = anzahlGitter;
            this.maxError = maxError;
            maxErrorPoly = 0.2 * maxError; // Eigentlich nicht nötig.
            maxErrorArc = 0.8 * maxError; // Eigentlich nicht nötig.
            this.d = d;
            this.rho = rho;
            if (rl < rc && rc < ru && (ru < 1.0 || rl > 1.0 || (rc < 1.0 + Precision.eps && rc > 1.0 - Precision.eps)))
            {
                this.ru = ru;
                this.rc = rc;
                this.rl = rl;
            }
            else
            {
                this.ru = 5.0;
                this.rc = 1.0;
                this.rl = 0.2;
            }
            inflectionPoints = curve.GetInflectionPoints();
            Array.Sort(inflectionPoints);
            this.ApproxSpline();
        }



        public Path2D Approx
        {
            get { return approx; }
        }

        public bool Kruemmung
        {
            get { return kruemmung; }
            set { kruemmung = value; }
        }

        public bool Lazy
        {
            get { return lazy; }
            set { lazy = value; }
        }

        public bool PolySwitch
        {
            get { return poly; }
            set { poly = value; }
        }

        public int AnzahlGitterpunkte
        {
            get { return anzahlGitter; }
            set { anzahlGitter = value; }
        }

        public double MaxErrorPoly
        {
            get { return maxErrorPoly; }
            set { maxErrorPoly = value; }
        }

        public double MaxErrorArc
        {
            get { return maxErrorArc; }
            set { maxErrorArc = value; }
        }

        public double MaxError
        {
            get { return maxError; }
            set { maxError = value; }
        }

        public int D
        {
            get { return d; }
            set { d = value; }
        }

        public double Rho
        {
            get { return rho; }
            set { rho = value; }
        }

        public double Ru
        {
            get { return ru; }
            set { ru = value; }
        }

        public double Rc
        {
            get { return rc; }
            set { rc = value; }
        }

        public double Rl
        {
            get { return rl; }
            set { rl = value; }
        }



        // Bekommt als Input eine 2D-Kurve und erstellt den Path2D, der aus aneinandergesetzten Kreisbögen
        // und Linien besteht und der die Kurve mit (maxErrorPoly + maxErrorArc), bzw. maxError Genauigkeit
        // approximiert.
        private void ApproxSpline()
        {
            // Aufruf der rekursiven Methode zur Approximierung.
            GeoVector2D sdir = curve.StartDirection;
            double pos = 0.0;
            while (sdir.Length < 1e-10)
            {
                pos += 1e-2;
                sdir = curve.DirectionAt(pos);
            }
            approx = new Path2D((ApproxSplineRec(0.0, 1.0, curve.StartPoint, sdir).First).ToArray(), true);
        }

        // Rekursive Methode zur Approximierung, die ein Teilstück der Originalkurve -
        // bzw. beim ersten Aufruf die gesamte Kurve - nimmt und versucht, diese durch
        // eine Linie, einen Kreisbogen und eine Linie, oder einen Biarc zu approximieren. Die Rückgabe
        // beinhaltet eine Liste der einzelnen Stücke. Während der Iteration werden unter Umständen Endpunkt
        // und Endrichtung des vorhergehenden Teilstücks verändert, deshalb bekommt die Methode einen neuen
        // Startpunkt und eine neue Startrichtung als Parameter.
        private Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D> ApproxSplineRec(double startPar, double endPar, GeoPoint2D newStartPoint, GeoVector2D newStartDir)
        {
            GeoPoint2D[] vertex = new GeoPoint2D[0];
            if (poly)
            {
                vertex = Poly(startPar, endPar); // Der Polygonzug wird erzeugt.
                vertex[0] = newStartPoint;
            }
            Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D> line, biArc, biArcL, biArcR;
            bool flag;
            GeoVector2D startDir = newStartDir;
            GeoVector2D endDir = curve.DirectionAt(endPar);
            startDir.Norm();
            // The derivative vanishes where a spline passes through coinciding poles. The tangent there is the
            // direction from inside of the segment. Moving endPar instead would leave a piece of the curve out.
            for (double offset = 1e-8; endDir.Length < 1e-10 && offset < endPar - startPar; offset *= 10)
            {
                endDir = curve.DirectionAt(endPar - offset);
            }
            endDir.Norm();

            GeoPoint2D endPoint = GetCurvePoint(endPar);
            if (((newStartPoint | endPoint) < maxError && (newStartPoint | endPoint) > 0.0 && (endPar - startPar) < 0.5) || (endPar - startPar) < 1e-6)
            {   // Notbremse (zugefügt am 16.11.2011): ein kleinstes Stückchen Linie, aber immer noch falsch: 
                // einfach eine Linie von start- nach endpunkt einfügen
                Line2D l = new Line2D(newStartPoint, endPoint);
                List<ICurve2D> retlist = new List<ICurve2D>();
                retlist.Add(l);
                return new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(retlist, endPoint, l.EndDirection.Normalized); // .Normalized zugefügt am 16.11.2011 G.
            }
            // Teste, ob das Teilstück der Kurve durch eine Linie mit genügender Genauigkeit approximiert werden
            // kann. In diesem Fall wird eine Linie einem Kreisbogen vorgezogen.
            if ((endPar - startPar) < 0.5)
            {
                line = LineTest(startPar, endPar, newStartPoint, GetCurvePoint(endPar), startDir, out flag);
                if (flag) return line;
            }

            // Testet, ob das Teilstück der Kurve durch eine Linie und dann einen Kreisbogen oder durch einen
            // Kreisbogen und dann eine Linie angenähert werden kann.
            Tripel<Line2D, Arc2D, double> lineArc = LineArcTest(vertex, startPar, endPar, newStartPoint, GetCurvePoint(endPar), startDir, endDir);
            Tripel<Arc2D, Line2D, double> arcLine = ArcLineTest(vertex, startPar, endPar, newStartPoint, GetCurvePoint(endPar), startDir, endDir);
            List<ICurve2D> tempList = new List<ICurve2D>();
            double maxErrorTemp = maxError;
            if (poly) maxErrorTemp = maxErrorArc;
            if (lineArc.Third < arcLine.Third && lineArc.Third < maxErrorTemp)
            {
                // Linie/Kreisbogen-Kombination approximiert besser als Kreisbogen/Linie und unterschreitet den Fehler.
                tempList.Add(lineArc.First);
                if (Math.Abs(lineArc.Second.Sweep) < 1e-5)
                {   // der Bogen ist minimal, also nicht zufügen
                    // lineArc.First.EndPoint = lineArc.Second.EndPoint; // die Linie ein wenig anpassen
                }
                else
                {
                    tempList.Add(lineArc.Second);
                }
                return new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(tempList, GetCurvePoint(endPar), endDir);
            }
            else if (arcLine.Third < maxErrorTemp)
            {
                // Kreisbogen/Linie-Kombination approximiert besser als Linie/Kreisbogen und unterschreitet den Fehler.
                if (Math.Abs(arcLine.First.Sweep) < 1e-5)
                {   // der Bogen ist minimal, also nicht zufügen
                    // arcLine.Second.StartPoint = arcLine.First.StartPoint; // die Linie ein wenig anpassen
                }
                else
                {
                    tempList.Add(arcLine.First);
                }
                tempList.Add(arcLine.Second);
                return new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(tempList, GetCurvePoint(endPar), endDir);
            }

            // Aufruf der Methode für das Optimal Single Biarc Fitting.
            biArc = OsbFitting(vertex, startPar, endPar, newStartPoint, startDir, endDir, out flag);
            // Wird flag als true zurückgegeben, unterschreitet der erzeugte Biarc die Fehlergrenze,
            // sodass dieser verwendet werden kann.
            if (flag) return biArc;
            else
            {
                // Ist flag false, dann wird die (Teil-)Kurve am mittleren Parameter in 2 weitere Teilsegmente
                // zerlegt und für diese wird die rekursive Methode wieder aufgerufen.
                biArcL = ApproxSplineRec(startPar, (startPar + endPar) / 2, newStartPoint, newStartDir);
                biArcR = ApproxSplineRec((startPar + endPar) / 2, endPar, biArcL.Second, biArcL.Third);
                (biArcL.First).AddRange(biArcR.First);
                biArcL.Second = biArcR.Second;
                biArcL.Third = biArcR.Third;
                return biArcL;
            }
        }

        // Filtert die für die Parameter relevanten Wendepunkte heraus.
        private double[] GetInflectionPointsPar(double startPar, double endPar)
        {
            List<double> list = new List<double>();
            for (int i = 0; i < inflectionPoints.Length; i++)
            {
                if (inflectionPoints[i] > startPar && inflectionPoints[i] < endPar) list.Add(inflectionPoints[i]);
            }
            return list.ToArray();
        }

        // Erzeugt einen Polygonzug, der das durch die Parameter startPar und endPar gegebene Teilstück
        // der Kurve approximiert.
        public GeoPoint2D[] Poly(double startPar, double endPar)
        {
            // Hole die Wendepunkte der Kurve und teile die Kurve in die Segmente zwischen den Wendepunkten auf.
            // Für die Segmente wird die rekursive Methode aufgerufen.
            double[] inflPoints = GetInflectionPointsPar(startPar, endPar);
            if (inflPoints.Length > 0)
            {
                List<GeoPoint2D> vertex = new List<GeoPoint2D>();
                vertex.Add(GetCurvePoint(startPar));
                vertex.AddRange(PolyRec(startPar, inflPoints[0]));
                for (int i = 0; i < inflPoints.Length - 1; i++)
                {
                    vertex.Add(GetCurvePoint(inflPoints[i]));
                    vertex.AddRange(PolyRec(inflPoints[i], inflPoints[i + 1]));
                }
                vertex.Add(GetCurvePoint(inflPoints[inflPoints.Length - 1]));
                vertex.AddRange(PolyRec(inflPoints[inflPoints.Length - 1], endPar));
                vertex.Add(GetCurvePoint(endPar));
                return vertex.ToArray();
            }
            List<GeoPoint2D> list = new List<GeoPoint2D>();
            list.Add(GetCurvePoint(startPar));
            list.AddRange(PolyRec(startPar, endPar));
            list.Add(GetCurvePoint(endPar));
            return list.ToArray();
        }

        // Testet, ob die als Parameter übergebene Linie das Teilstück der Kurve von startPar bis endPar
        // gut genug annähert.
        // (Gibt weiterhin einen sinnvollen Teilpunkt der Kurve durch den out-Parameter zurück.) stimmt nicht mehr.
        private bool TriangleDiff(Line2D line, double startPar, double endPar, bool polygon, out double midPar)
        {
            // Mit welchem Fehler rechnet man?
            double maxErrorNow;
            if (polygon) maxErrorNow = maxErrorPoly;
            else if (poly) maxErrorNow = maxErrorArc;
            else maxErrorNow = maxError;
            midPar = (startPar + endPar) / 2;
            GeoPoint2D intPoint;
            GeoVector2D startDir = curve.DirectionAt(startPar);
            GeoVector2D endDir = curve.DirectionAt(endPar);
            if (Precision.SameDirection(startDir, endDir, false)) return true;
            GeoPoint2D midPoint = GetCurvePoint(midPar);
            GeoVector2D midDir = curve.DirectionAt(midPar);
            GeoPoint2D startPoint = GetCurvePoint(startPar);
            GeoPoint2D endPoint = GetCurvePoint(endPar);
            // Erzeuge ein die Kurve beinhaltendes Dreieck, indem der Schnittpunkt von zwei Linien vom Startpunkt
            // und Endpunkt berechnet wird.
            Line2D line1 = new Line2D(startPoint, startPoint + startDir);
            Line2D line2 = new Line2D(endPoint, endPoint - endDir);
            GeoPoint2DWithParameter[] intPoints = line1.Intersect(line2);
            if (intPoints.Length > 0 && intPoints[0].par1 > 0.0 && intPoints[0].par2 > 0.0)
            {
                intPoint = intPoints[0].p;
                // Abbruchbedingung, in diesem Fall beinhaltet das Dreieck die Kurve nicht.
                if (!Geometry.OnSameSide(intPoint, midPoint, line.StartPoint, line.EndPoint)) return false;
                GeoPoint2D dropPoint = Geometry.DropPL(intPoint, line.StartPoint, line.EndPoint);
                double dropPar = line.PositionOf(dropPoint);
                // Berechne die Höhe des Dreiecks. Ist sie klein genug, beende die Methode erfolgreich.
                if ((intPoint - dropPoint).Length < maxErrorNow) return true;
                else
                {
                    // Ist die Höhe des Dreiecks nicht klein genug, berechne ein neues, kleineres Dreieck, dass
                    // den Teil der Kurve mit der größten Distanz zur Linie enthält.

                    // Der nächste Teil wurde wegen auskommentiert, da es zu langsam war, der neue Parameter, an
                    // dem in zwei Dreiecke geteilt wird, wird nun einfach als Mitte zwischen startPar und
                    // endPar gewählt. Damit wäre eigentlich auch der out-Parameter midPar unnötig.
                    /*GeoPoint2DWithParameter[] midPoints = curve.Intersect(intPoint, dropPoint);
                    int temp = -1;
                    for (int i = midPoints.Length - 1; i >= 0; i--)
                    {
                        if (midPoints[i].par1 <= endPar && midPoints[i].par1 >= startPar) temp = i;
                    }
                    if (temp != -1)
                    {
                        midPar = midPoints[temp].par1;
                        midPoint = midPoints[temp].p;
                        midDir = curve.DirectionAt(midPar);
                    }*/
                    // Abbruchbedingung, in diesem Fall ist die Linie wohl zu weit von der Kurve entfernt.
                    if ((midPoint - dropPoint).Length > maxErrorNow || (intPoint - midPoint).Length < 0.01 * maxErrorNow) return false;
                    // Testet, auf welcher Seite des mittleren Punkts die Kurve weiter von der Linie entfernt ist,
                    // und ruft die Methode für diese Seite auf.
                    GeoPoint2DWithParameter[] helpPoints = line.Intersect(midPoint, midPoint + midDir);
                    double newMidPar;
                    if (helpPoints.Length == 0 || helpPoints[0].par1 > 1.0 || helpPoints[0].par1 > dropPar) return TriangleDiff(line, startPar, midPar, poly, out newMidPar);
                    else return TriangleDiff(line, midPar, endPar, poly, out newMidPar);
                }
            }
            else return false;
        }

        // Rekursive Methode zur Erzeugung des Polygonzugs.
        private List<GeoPoint2D> PolyRec(double startPar, double endPar)
        {
            GeoPoint2D startPoint = GetCurvePoint(startPar);
            GeoPoint2D endPoint = GetCurvePoint(endPar);
            GeoVector2D startDir = curve.DirectionAt(startPar);
            GeoVector2D endDir = curve.DirectionAt(endPar);
            double midPar;
            List<GeoPoint2D> list = new List<GeoPoint2D>();
            // Erzeuge Linie vom Startpunkt zum Endpunkt des Teilstücks der Kurve.
            Line2D line = new Line2D(startPoint, endPoint);
            // Teste mit TriangleDiff, ob die erzeugte Linie nah genug an der Kurve liegt.
            if (Precision.SameDirection(startDir, endDir, false) || TriangleDiff(line, startPar, endPar, true, out midPar)) return list;
            else
            {
                // Wenn nicht, wird die Kurve wieder aufgeteilt und die Methode wird erneut aufgerufen.
                list.AddRange(PolyRec(startPar, midPar));
                list.Add(GetCurvePoint(midPar));
                list.AddRange(PolyRec(midPar, endPar));
                return list;
            }
        }

        // Testet, ob das Kurventeilstück durch eine Linie approximiert werden kann.
        private Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D> LineTest(double startPar, double endPar, GeoPoint2D startPoint, GeoPoint2D endPoint, GeoVector2D dirStart, out bool flag)
        {
            Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D> ret;
            Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D> helpT = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(new List<ICurve2D>(), startPoint, dirStart);
            List<ICurve2D> retList = new List<ICurve2D>();
            // Abbruchbedingung. Schaut, ob der Endpunkt zu weit von der Linie vom Startpunkt mit Startrichtung
            // entfernt ist.
            if (Math.Abs(Geometry.DistPL(endPoint, startPoint, dirStart)) > maxErrorArc)
            {
                flag = false;
                ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(retList, new GeoPoint2D(), new GeoVector2D());
                return ret;
            }
            flag = true;
            double[] inflPoints = GetInflectionPointsPar(startPar, endPar);
            if (inflPoints.Length > 0)
            {
                // Sind Wendepunkte auf dem Teilstück der Kurve, wird das Teilstück der Kurve aufgeteilt
                // und die ursprüngliche Methode zur Approximierung aufgerufen.
                helpT = ApproxSplineRec(startPar, inflPoints[0], helpT.Second, helpT.Third);
                retList.AddRange(helpT.First);
                for (int i = 0; i < inflPoints.Length - 1; i++)
                {
                    helpT = ApproxSplineRec(inflPoints[i], inflPoints[i + 1], helpT.Second, helpT.Third);
                    retList.AddRange(helpT.First);
                }
                helpT = ApproxSplineRec(inflPoints[inflPoints.Length - 1], endPar, helpT.Second, helpT.Third);
                retList.AddRange(helpT.First);
                ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(retList, helpT.Second, helpT.Third);
                return ret;
            }
            GeoPoint2D newEndPoint = Geometry.DropPL(endPoint, startPoint, dirStart);
            Line2D approx = new Line2D(startPoint, newEndPoint);

            // TangentPointsToAngle noch zu ersetzen durch effizientere Methode.
            /*double[] tangentPosTemp = curve.TangentPointsToAngle(approx.EndDirection);
            List<double> tangentList = new List<double>();
            foreach(double d in tangentPosTemp)
            {
                if(d >= startPar && d <= endPar) tangentList.Add(d);
            }
            double[] tangentPos = tangentList.ToArray();
            double[] distances = new double[tangentPos.Length];
            GeoPoint2D[] tangentPoints = new GeoPoint2D[tangentPos.Length];
            GeoPoint2D[] diffPoints = new GeoPoint2D[tangentPos.Length];
            if (tangentPos.Length > 0)
            {
                for (int i = 0; i < tangentPos.Length; i++)
                {
                    tangentPoints[i] = curve.PointAt(tangentPos[i]);
                    diffPoints[i] = Geometry.DropPL(tangentPoints[i], startPoint, newEndPoint);
                    distances[i] = (tangentPoints[i] - diffPoints[i]).Length;
                }
                if (ArrayMax(distances) > maxErrorArc)
                {
                    flag = false;
                    ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(retList, new GeoPoint2D(), new GeoVector2D());
                    return ret;
                }
            }*/

            double midPar;
            if (!TriangleDiff(approx, startPar, endPar, false, out midPar))
            {
                flag = false;
                ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(retList, new GeoPoint2D(), new GeoVector2D());
                return ret;
            }

            retList.Add(approx);
            ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(retList, newEndPoint, approx.EndDirection);
            return ret;
        }

        // Nähert das Kurventeilstück, bzw. den Polygonzug durch die Kombination einer Linie und dann eines
        // Kreisbogens an. Zurückgegeben werden die Linie, der Kreisbogen, sowie der maximale Fehler
        // vom Kurventeilstück, bzw. vom Polygonzug zu der erzeugten Linie/Kreisbogen-Kombination.
        public Tripel<Line2D, Arc2D, double> LineArcTest(GeoPoint2D[] subVertex, double startPar, double endPar, GeoPoint2D startPoint, GeoPoint2D endPoint, GeoVector2D dirStart, GeoVector2D dirEnd)
        {
            double k, k_line;
            GeoPoint2D center;
            // Zunächst werden die Linienlänge k_line, der Kreisradius k, sowie der Kreismittelpunkt center berechnet.
            // Dabei muss zunächst unterschieden werden, ob die Endrichtung und die Startrichtung, die bei der
            // Berechnung des Kreismittelpunktes um 90 Grad gedreht werden, in die selbe Richtung,
            // oder ob sie in entgegengesetzte Richtungen gedreht werden.
            if (Geometry.OnSameSide(endPoint, startPoint + dirEnd, startPoint, dirStart))
            {
                k_line = (((endPoint.y - startPoint.y) * (dirStart.y - dirEnd.y)) + ((startPoint.x - endPoint.x) * (dirEnd.x - dirStart.x))) / ((dirStart.y * (dirStart.y - dirEnd.y)) - (dirStart.x * (dirEnd.x - dirStart.x)));
                k = (startPoint.x - endPoint.x + (k_line * dirStart.x)) / (dirStart.y - dirEnd.y);
                center = new GeoPoint2D(endPoint.x - (k * dirEnd.y), endPoint.y + (k * dirEnd.x));
            }
            else
            {
                k_line = (((endPoint.y - startPoint.y) * (dirStart.y + dirEnd.y)) - ((startPoint.x - endPoint.x) * (dirEnd.x + dirStart.x))) / ((dirStart.y * (dirStart.y + dirEnd.y)) + (dirStart.x * (dirEnd.x + dirStart.x)));
                k = (startPoint.x - endPoint.x + (k_line * dirStart.x)) / (dirStart.y + dirEnd.y);
                center = new GeoPoint2D(endPoint.x + (k * dirEnd.y), endPoint.y - (k * dirEnd.x));
            }
            bool counterClock = k > 0;
            Line2D line = new Line2D(startPoint, startPoint + (k_line * dirStart));
            Arc2D arc = new Arc2D(center, Math.Abs(k), line.EndPoint, endPoint, counterClock);
            Double swdabg = Math.Abs(Math.PI * 2 - Math.Abs(arc.SweepAngle));
            double dbg = line.EndPoint | endPoint;
            if (!Precision.SameNotOppositeDirection(dirStart, line.StartDirection, false) || !Precision.SameNotOppositeDirection(line.EndDirection, arc.StartDirection, false) || !Precision.SameNotOppositeDirection(arc.EndDirection, dirEnd, false) || Math.Abs(arc.SweepAngle) > Math.PI)
            {
                // Es kann passieren, dass der Kreisbogen oder Linie, die erzeugt wurden, am Startpunkt, am Endpunkt
                // oder am Übergang zwischen Kreisbogen und Linie in entgegengesetzter Richtung übergehen.
                // Darauf wird hier getestet. Falls Startrichtungen und Endrichtungen, sowie die beiden Richtungen
                // am Übergang nicht gleich sind wird die zurückgegebene Distanz auf einen Wert gesetzt, der auf
                // jeden Fall den gesetzten maximalen Fehler überschreitet, so dass die erzeugten Kreisbogen und
                // Linie nicht benutzt werden.
                return new Tripel<Line2D, Arc2D, double>(line, arc, maxError + maxErrorArc + maxErrorPoly);
            }
            double distance = 0.0;
            if (poly)
            {
                distance = PolyDistanceArcLine(arc, line, subVertex, false);
            }
            else
            {
                distance = GitterDistanceArcLine(arc, line, startPar, endPar, false);
            }

            return new Tripel<Line2D, Arc2D, double>(line, arc, distance);
        }

        // Nähert das Kurventeilstück, bzw. den Polygonzug durch die Kombination eines Kreisbogens und dann einer
        // Linie an. Zurückgegeben werden die Linie, der Kreisbogen, sowie der maximale Fehler
        // vom Kurventeilstück, bzw. vom Polygonzug zu der erzeugten Kreisbogen/Linie-Kombination.
        public Tripel<Arc2D, Line2D, double> ArcLineTest(GeoPoint2D[] subVertex, double startPar, double endPar, GeoPoint2D startPoint, GeoPoint2D endPoint, GeoVector2D dirStart, GeoVector2D dirEnd)
        {
            double k, k_line;
            GeoPoint2D center;
            // Zunächst werden die Linienlänge k_line, der Kreisradius k, sowie der Kreismittelpunkt center berechnet.
            // Dabei muss zunächst unterschieden werden, ob die Endrichtung und die Startrichtung, die bei der
            // Berechnung des Kreismittelpunktes um 90 Grad gedreht werden, in die selbe Richtung,
            // oder ob sie in entgegengesetzte Richtungen gedreht werden.
            if (!Geometry.OnSameSide(startPoint, endPoint + dirStart, endPoint, dirEnd))
            {
                k_line = (((startPoint.y - endPoint.y) * (dirEnd.y - dirStart.y)) + ((endPoint.x - startPoint.x) * (dirStart.x - dirEnd.x))) / ((dirEnd.y * (dirEnd.y - dirStart.y)) - (dirEnd.x * (dirStart.x - dirEnd.x)));
                k = (endPoint.x - startPoint.x + (k_line * dirEnd.x)) / (dirEnd.y - dirStart.y);
                center = new GeoPoint2D(startPoint.x - (k * dirStart.y), startPoint.y + (k * dirStart.x));
            }
            else
            {
                k_line = (((startPoint.y - endPoint.y) * (dirEnd.y + dirStart.y)) - ((endPoint.x - startPoint.x) * (dirStart.x + dirEnd.x))) / ((dirEnd.y * (dirEnd.y + dirStart.y)) + (dirEnd.x * (dirStart.x + dirEnd.x)));
                k = (endPoint.x - startPoint.x + (k_line * dirEnd.x)) / (dirEnd.y + dirStart.y);
                center = new GeoPoint2D(startPoint.x + (k * dirStart.y), startPoint.y - (k * dirStart.x));
            }
            bool counterClock = k > 0;
            Arc2D arc = new Arc2D(center, Math.Abs(k), startPoint, endPoint + (k_line * dirEnd), counterClock);
            Line2D line = new Line2D(arc.EndPoint, endPoint);
            if (!Precision.SameNotOppositeDirection(dirStart, arc.StartDirection, false) || !Precision.SameNotOppositeDirection(arc.EndDirection, line.StartDirection, false) || !Precision.SameNotOppositeDirection(line.EndDirection, dirEnd, false) || Math.Abs(arc.SweepAngle) > Math.PI)
            {
                // Es kann passieren, dass der Kreisbogen oder Linie, die erzeugt wurden, am Startpunkt, am Endpunkt
                // oder am Übergang zwischen Kreisbogen und Linie in entgegengesetzter Richtung übergehen.
                // Darauf wird hier getestet. Falls Startrichtungen und Endrichtungen, sowie die beiden Richtungen
                // am Übergang nicht gleich sind wird die zurückgegebene Distanz auf einen Wert gesetzt, der auf
                // jeden Fall den gesetzten maximalen Fehler überschreitet, so dass die erzeugten Kreisbogen und
                // Linie nicht benutzt werden.
                return new Tripel<Arc2D, Line2D, double>(arc, line, maxError + maxErrorArc + maxErrorPoly);
            }
            double distance = 0.0;
            if (poly)
            {
                distance = PolyDistanceArcLine(arc, line, subVertex, true);
            }
            else
            {
                distance = GitterDistanceArcLine(arc, line, startPar, endPar, true);
            }

            return new Tripel<Arc2D, Line2D, double>(arc, line, distance);
        }

        // Methode, die versucht, das Kurventeilstück mit einem BiArc zu approximieren. Gegeben sind
        // Start- und Endpunkt, sowie die Richtungen an den beiden Punkten.
        private Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D> OsbFitting(GeoPoint2D[] subVertex, double startPar, double endPar, GeoPoint2D newStartPoint, GeoVector2D dirStart, GeoVector2D dirEnd, out bool flag)
        {
            Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D> ret;
            // Abbruchbedingung, in diesem Fall werden mehr als 2 Kreisbögen benötigt.
            if (dirStart * dirEnd < 1.0 + Precision.eps && dirStart * dirEnd > 1.0 - Precision.eps)
            {
                ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(new List<ICurve2D>(), new GeoPoint2D(), new GeoVector2D());
                flag = false;
                return ret;
            }
            int dTemp = d;
            double ruTemp = ru;
            double rcTemp = rc;
            double rlTemp = rl;
            GeoPoint2D startPoint = newStartPoint;
            GeoPoint2D endPoint = GetCurvePoint(endPar);

            if (kruemmung)
            {
                // Falls die BiArcs durch das Verhältnis der Krümmungsradien von Start- und Endpunkt erzeugt werden sollen.
                bool counterClockLeft = true;
                bool counterClockRight = true;
                GeoPoint2D pointS, pointE;
                GeoVector2D deriv1S, deriv2S, deriv1E, deriv2E;
                // Die Punkte, bzw. erste und zweite Ableitung an Start- und Endparameter werden geholt.
                curve.TryPointDeriv2At(startPar, out pointS, out deriv1S, out deriv2S);
                curve.TryPointDeriv2At(endPar, out pointE, out deriv1E, out deriv2E);
                // Berechnet die Krümmung an Start- und Endpunkt. Ist diese negativ, verläuft die Kurve in dem Punkt
                // im Uhrzeigersinn, ist sie positiv, verläuft die Kurve gegen den Uhrzeigersinn.
                double kruemmungStart = ((deriv1S.x * deriv2S.y) - (deriv2S.x * deriv1S.y)) / Math.Pow((deriv1S.x * deriv1S.x) + (deriv1S.y * deriv1S.y), 1.5);
                double kruemmungEnd = ((deriv1E.x * deriv2E.y) - (deriv2E.x * deriv1E.y)) / Math.Pow((deriv1E.x * deriv1E.x) + (deriv1E.y * deriv1E.y), 1.5);
                if (kruemmungStart < 0.0)
                {
                    counterClockLeft = false;
                    kruemmungStart = Math.Abs(kruemmungStart);
                }
                if (kruemmungEnd < 0.0)
                {
                    counterClockRight = false;
                    kruemmungEnd = Math.Abs(kruemmungEnd);
                }
                // Dieser Quotient soll am Ende der Quotient zwischen den Radien der beiden Kreisbögen sein.
                double quotient = kruemmungEnd / kruemmungStart;
                // Um die BiArcs auszurechnen, werden zunächst die Anfangs- und Endrichtung um 90 Grad gedreht.
                // Danach wird eine quadratische Gleichung aufgestellt, da der Abstand der beiden Kreismittelpunkte
                // je nachdem, ob die Kreisbögen in die selbe Richtung verlaufen, gleich der Differenz, bzw. der Summe
                // der Radien sein muss und diese Radien über den Quotienten voneinander abhängen.
                GeoVector2D dirStartRot = new GeoVector2D(dirStart.y, -dirStart.x);
                GeoVector2D dirEndRot = new GeoVector2D(dirEnd.y, -dirEnd.x);
                double a, b;
                double c = (startPoint - endPoint) * (startPoint - endPoint);
                if ((counterClockLeft && counterClockRight) || (!counterClockLeft && !counterClockRight))
                {
                    a = (2 / quotient) - (1 / (quotient * quotient)) - 1 + ((dirStartRot - ((1 / quotient) * dirEndRot)) * (dirStartRot - ((1 / quotient) * dirEndRot)));
                    b = (2 * (((startPoint - endPoint) * dirStartRot) - ((1 / quotient) * ((startPoint - endPoint) * dirEndRot))));
                }
                else
                {
                    a = ((dirStartRot + ((1 / quotient) * dirEndRot)) * (dirStartRot + ((1 / quotient) * dirEndRot))) - (2 / quotient) - (1 / (quotient * quotient)) - 1;
                    b = (2 * (((startPoint - endPoint) * dirStartRot) + ((1 / quotient) * ((startPoint - endPoint) * dirEndRot))));
                }
                double x, y;
                if (Quadgl(a, b, c, out x, out y) == 0)
                {
                    // Gleichung war nicht lösbar.
                    flag = false;
                    return new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(new List<ICurve2D>(), endPoint, dirEnd);
                }
                double radiusLeft, radiusRight;
                if (counterClockLeft)
                {
                    if (x < 0)
                    {
                        if (y < 0)
                        {
                            // Beide Lösungen der Gleichung machen Sinn, also wird für beide jeweils ein BiArc
                            // erzeugt, und der BiArc mit der geringeren Distanz genommen.
                            double radiusLeft1 = Math.Abs(x);
                            double radiusLeft2 = Math.Abs(y);
                            double radiusRight1 = radiusLeft1 / quotient;
                            double radiusRight2 = radiusLeft2 / quotient;
                            // Wenn ein Kreisbogen gegen den Uhrzeigersinn verläuft, hätte am Anfang in die
                            // entgegengesetzte Richtung gedreht werden müssen.
                            if (counterClockLeft) dirStartRot = -1 * dirStartRot;
                            if (counterClockRight) dirEndRot = -1 * dirEndRot;
                            // Da die Richtungsvektoren normiert sind, lassen sich so die Mittelpunkte ausrechnen.
                            GeoPoint2D centerLeft1 = startPoint + (radiusLeft1 * dirStartRot);
                            GeoPoint2D centerRight1 = endPoint + (radiusRight1 * dirEndRot);
                            GeoPoint2D centerLeft2 = startPoint + (radiusLeft2 * dirStartRot);
                            GeoPoint2D centerRight2 = endPoint + (radiusRight2 * dirEndRot);
                            GeoPoint2D midPoint1, midPoint2;
                            GeoVector2D midVector1, midVector2;
                            // Zur Erzeugung der BiArcs muss noch der Übergangspunkt berechnet werden.
                            if (radiusLeft1 > radiusRight1)
                            {
                                midVector1 = centerRight1 - centerLeft1;
                                midVector1.Norm();
                                midPoint1 = centerLeft1 + (radiusLeft1 * midVector1);
                            }
                            else
                            {
                                midVector1 = centerLeft1 - centerRight1;
                                midVector1.Norm();
                                midPoint1 = centerRight1 + (radiusRight1 * midVector1);
                            }
                            if (radiusLeft2 > radiusRight2)
                            {
                                midVector2 = centerRight2 - centerLeft2;
                                midVector2.Norm();
                                midPoint2 = centerLeft2 + (radiusLeft2 * midVector2);
                            }
                            else
                            {
                                midVector2 = centerLeft2 - centerRight2;
                                midVector2.Norm();
                                midPoint2 = centerRight2 + (radiusRight2 * midVector2);
                            }
                            Arc2D left1 = new Arc2D(centerLeft1, radiusLeft1, startPoint, midPoint1, counterClockLeft);
                            Arc2D right1 = new Arc2D(centerRight1, radiusRight1, midPoint1, endPoint, counterClockRight);
                            Arc2D left2 = new Arc2D(centerLeft2, radiusLeft2, startPoint, midPoint2, counterClockLeft);
                            Arc2D right2 = new Arc2D(centerRight2, radiusRight2, midPoint2, endPoint, counterClockRight);
                            double distance1 = GitterBiArcDistance(left1, right1, startPar, endPar);
                            double distance2 = GitterBiArcDistance(left2, right2, startPar, endPar);
                            List<ICurve2D> retListT = new List<ICurve2D>();
                            if (distance1 < distance2)
                            {
                                retListT.Add(left1);
                                retListT.Add(right1);
                            }
                            else
                            {
                                retListT.Add(left2);
                                retListT.Add(right2);
                            }
                            if (poly) flag = Math.Min(distance1, distance2) < maxErrorArc;
                            else flag = Math.Min(distance1, distance2) < maxError;
                            return new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(retListT, endPoint, dirEnd);
                        }
                        // Nur eine der Lösungen macht Sinn, also wird diese verwendet.
                        else radiusLeft = Math.Abs(x);
                    }
                    // Nur eine der Lösungen macht Sinn, also wird diese verwendet.
                    else if (y < 0) radiusLeft = Math.Abs(y);
                    else
                    {
                        // Es gibt keine Lösung der Gleichung, die Sinn macht.
                        flag = false;
                        return new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(new List<ICurve2D>(), endPoint, dirEnd);
                    }
                }
                else
                {
                    if (x > 0)
                    {
                        if (y > 0)
                        {
                            // Beide Lösungen der Gleichung machen Sinn, also wird für beide jeweils ein BiArc
                            // erzeugt, und der BiArc mit der geringeren Distanz genommen.
                            double radiusLeft1 = x;
                            double radiusLeft2 = y;
                            double radiusRight1 = radiusLeft1 / quotient;
                            double radiusRight2 = radiusLeft2 / quotient;
                            // Wenn ein Kreisbogen gegen den Uhrzeigersinn verläuft, hätte am Anfang in die
                            // entgegengesetzte Richtung gedreht werden müssen.
                            if (counterClockLeft) dirStartRot = -1 * dirStartRot;
                            if (counterClockRight) dirEndRot = -1 * dirEndRot;
                            // Da die Richtungsvektoren normiert sind, lassen sich so die Mittelpunkte ausrechnen.
                            GeoPoint2D centerLeft1 = startPoint + (radiusLeft1 * dirStartRot);
                            GeoPoint2D centerRight1 = endPoint + (radiusRight1 * dirEndRot);
                            GeoPoint2D centerLeft2 = startPoint + (radiusLeft2 * dirStartRot);
                            GeoPoint2D centerRight2 = endPoint + (radiusRight2 * dirEndRot);
                            GeoPoint2D midPoint1, midPoint2;
                            GeoVector2D midVector1, midVector2;
                            // Zur Erzeugung der BiArcs muss noch der Übergangspunkt berechnet werden.
                            if (radiusLeft1 > radiusRight1)
                            {
                                midVector1 = centerRight1 - centerLeft1;
                                midVector1.Norm();
                                midPoint1 = centerLeft1 + (radiusLeft1 * midVector1);
                            }
                            else
                            {
                                midVector1 = centerLeft1 - centerRight1;
                                midVector1.Norm();
                                midPoint1 = centerRight1 + (radiusRight1 * midVector1);
                            }
                            if (radiusLeft2 > radiusRight2)
                            {
                                midVector2 = centerRight2 - centerLeft2;
                                midVector2.Norm();
                                midPoint2 = centerLeft2 + (radiusLeft2 * midVector2);
                            }
                            else
                            {
                                midVector2 = centerLeft2 - centerRight2;
                                midVector2.Norm();
                                midPoint2 = centerRight2 + (radiusRight2 * midVector2);
                            }
                            Arc2D left1 = new Arc2D(centerLeft1, radiusLeft1, startPoint, midPoint1, counterClockLeft);
                            Arc2D right1 = new Arc2D(centerRight1, radiusRight1, midPoint1, endPoint, counterClockRight);
                            Arc2D left2 = new Arc2D(centerLeft2, radiusLeft2, startPoint, midPoint2, counterClockLeft);
                            Arc2D right2 = new Arc2D(centerRight2, radiusRight2, midPoint2, endPoint, counterClockRight);
                            double distance1 = GitterBiArcDistance(left1, right1, startPar, endPar);
                            double distance2 = GitterBiArcDistance(left2, right2, startPar, endPar);
                            List<ICurve2D> retListT = new List<ICurve2D>();
                            if (distance1 < distance2)
                            {
                                retListT.Add(left1);
                                retListT.Add(right1);
                            }
                            else
                            {
                                retListT.Add(left2);
                                retListT.Add(right2);
                            }
                            if (poly) flag = Math.Min(distance1, distance2) < maxErrorArc;
                            else flag = Math.Min(distance1, distance2) < maxError;
                            return new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(retListT, endPoint, dirEnd);
                        }
                        // Nur eine der Lösungen macht Sinn, also wird diese verwendet.
                        else radiusLeft = x;
                    }
                    // Nur eine der Lösungen macht Sinn, also wird diese verwendet.
                    else if (y > 0) radiusLeft = y;
                    else
                    {
                        // Es gibt keine Lösung der Gleichung, die Sinn macht.
                        flag = false;
                        return new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(new List<ICurve2D>(), endPoint, dirEnd);
                    }
                }
                radiusRight = radiusLeft / quotient;
                // Wenn ein Kreisbogen gegen den Uhrzeigersinn verläuft, hätte am Anfang in die
                // entgegengesetzte Richtung gedreht werden müssen.
                if (counterClockLeft) dirStartRot = -1 * dirStartRot;
                if (counterClockRight) dirEndRot = -1 * dirEndRot;
                // Da die Richtungsvektoren normiert sind, lassen sich so die Mittelpunkte ausrechnen.
                GeoPoint2D centerLeft = startPoint + (radiusLeft * dirStartRot);
                GeoPoint2D centerRight = endPoint + (radiusRight * dirEndRot);
                GeoPoint2D midPoint;
                GeoVector2D midVector;
                // Zur Erzeugung der BiArcs muss noch der Übergangspunkt berechnet werden.
                if (radiusLeft > radiusRight)
                {
                    midVector = centerRight - centerLeft;
                    midVector.Norm();
                    midPoint = centerLeft + (radiusLeft * midVector);
                }
                else
                {
                    midVector = centerLeft - centerRight;
                    midVector.Norm();
                    midPoint = centerRight + (radiusRight * midVector);
                }
                Arc2D left = new Arc2D(centerLeft, radiusLeft, startPoint, midPoint, counterClockLeft);
                Arc2D right = new Arc2D(centerRight, radiusRight, midPoint, endPoint, counterClockRight);
                List<ICurve2D> retList = new List<ICurve2D>();
                retList.Add(left);
                retList.Add(right);
                double distance = GitterBiArcDistance(left, right, startPar, endPar);
                if (poly) flag = distance < maxErrorArc;
                else flag = distance < maxError;
                return new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(retList, endPoint, dirEnd);
            }

            // Ziel der Methode ist, einen Quotienten r zu bestimmen, der das 'Größenverhältnis' der beiden
            // Kreisbögen definiert. Durch dieses lässt sich der entsprechende Mittelpunkt definieren, an dem
            // beide Kreisbögen ineinander übergehen. Mit dem Startpunkt, dem Endpunkt, den Richtungen an Start-
            // und Endpunkt, sowie dem Mittelpunkt und der Forderung, dass der Übergang am Mittelpunkt glatt sei,
            // ist der benötigte Biarc eindeutig bestimmt.
            // Es wird zunächst ein Gitter zwischen den Grenzwerten für r erzeugt.
            // Danach wird für jeden Gitterpunkt ein Biarc erzeugt und der Fehler berechnet. Es wird schließlich
            // nach mehreren Iterationen der Gitterpunkt mit dem minimalen Fehler gewählt, oder, je nach Einstellung,
            // wird der erste Biarc akzeptiert, der den Fehler unterschreitet.
            double rangeLeftLimit = GetLeftRange(rl, rc) * 0.0001; // Limit, bei dem die Iteration abgebrochen wird.
            double rangeRightLimit = GetRightRange(rc, ru) * 0.0001; // Limit, bei dem die Iteration abgebrochen wird.
            int index = 0;
            double[] r = new double[2 * d + 1];
            double[] dist;
            List<ICurve2D>[] arcs;
            do
            {
                // Gitter der r's wird erzeugt.
                r = GetR(rlTemp, rcTemp, ruTemp, dTemp);
                dist = new double[r.Length];
                arcs = new List<ICurve2D>[r.Length];

                int i = r.Length / 2;
                int j = i + 1;
                do
                {
                    if (i >= 0)
                    {
                        // Abbruchbedingung, in diesem Fall werden mehr als 2 Kreisbögen benötigt.
                        if (Math.Abs((startPoint - endPoint) * ((r[i] * dirStart) + dirEnd)) < Precision.eps)
                        {
                            ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(new List<ICurve2D>(), new GeoPoint2D(), new GeoVector2D());
                            flag = false;
                            return ret;
                        }
                        // Erzeugung des Biarcs.
                        arcs[i] = CreateBiArc(r[i], startPoint, endPoint, dirStart, dirEnd);
                        // Berechnen des Fehlers zwischen erzeugtem Biarc und Polygonzug.
                        if (poly) dist[i] = PolyBiArcDistance(arcs[i][0] as Arc2D, arcs[i][1] as Arc2D, subVertex);
                        else dist[i] = GitterBiArcDistance(arcs[i][0] as Arc2D, arcs[i][1] as Arc2D, startPar, endPar);
                        if (lazy)
                        {
                            if (poly && dist[i] < maxErrorArc)
                            {
                                ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(arcs[i], endPoint, dirEnd);
                                flag = true;
                                return ret;
                            }
                            else if (!poly && dist[i] < maxError)
                            {
                                ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(arcs[i], endPoint, dirEnd);
                                flag = true;
                                return ret;
                            }
                        }
                    }
                    if (j < r.Length)
                    {
                        // Abbruchbedingung, in diesem Fall werden mehr als 2 Kreisbögen benötigt.
                        if (Math.Abs((startPoint - endPoint) * ((r[j] * dirStart) + dirEnd)) < Precision.eps)
                        {
                            ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(new List<ICurve2D>(), new GeoPoint2D(), new GeoVector2D());
                            flag = false;
                            return ret;
                        }
                        // Erzeugung des Biarcs.
                        arcs[j] = CreateBiArc(r[j], startPoint, endPoint, dirStart, dirEnd);
                        // Berechnen des Fehlers zwischen erzeugtem Biarc und Polygonzug.
                        if (poly) dist[j] = PolyBiArcDistance(arcs[j][0] as Arc2D, arcs[j][1] as Arc2D, subVertex);
                        else dist[j] = GitterBiArcDistance(arcs[j][0] as Arc2D, arcs[j][1] as Arc2D, startPar, endPar);
                        if (lazy)
                        {
                            if (poly && dist[j] < maxErrorArc)
                            {
                                ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(arcs[j], endPoint, dirEnd);
                                flag = true;
                                return ret;
                            }
                            else if (!poly && dist[j] < maxError)
                            {
                                ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(arcs[j], endPoint, dirEnd);
                                flag = true;
                                return ret;
                            }
                        }
                    }
                    i--;
                    j++;
                } while (i >= 0 || j < r.Length);

                /*for (int i = 0; i < r.Length; i++)
                {
                    // Abbruchbedingung, in diesem Fall werden mehr als 2 Kreisbögen benötigt.
                    if (Math.Abs((startPoint - endPoint) * ((r[i] * dirStart) + dirEnd)) < Precision.eps)
                    {
                        ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(new List<ICurve2D>(), new GeoPoint2D(), new GeoVector2D());
                        flag = false;
                        return ret;
                    }
                    // Erzeugung des Biarcs.
                    arcs[i] = CreateBiArc(r[i], startPoint, endPoint, dirStart, dirEnd);
                    // Berechnen des Fehlers zwischen erzeugtem Biarc und Polygonzug.
                    if (poly) dist[i] = PolyBiArcDistance(arcs[i][0] as Arc2D, arcs[i][1] as Arc2D, subVertex);
                    else dist[i] = GitterBiArcDistance(arcs[i][0] as Arc2D, arcs[i][1] as Arc2D, startPar, endPar);
                    if (lazy)
                    {
                        if (poly && dist[i] < maxErrorArc)
                        {
                            ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(arcs[i], endPoint, dirEnd);
                            flag = true;
                            return ret;
                        }
                        else if (!poly && dist[i] < maxError)
                        {
                            ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(arcs[i], endPoint, dirEnd);
                            flag = true;
                            return ret;
                        }
                    }
                }*/
                // Bestimme r_i mit dem geringsten Fehler.
                index = MinIndex(dist);
                if (index == 0)
                {
                    rlTemp = r[0];
                    rcTemp = r[1];
                    ruTemp = r[2];
                }
                else if (index == 2 * dTemp)
                {
                    rlTemp = r[(2 * dTemp) - 2];
                    rcTemp = r[(2 * dTemp) - 1];
                    ruTemp = r[2 * dTemp];
                }
                else
                {
                    rlTemp = r[index - 1];
                    rcTemp = r[index];
                    ruTemp = r[index + 1];
                }
                // Aktualisiere das d, um bei der nächsten Iteration weniger r_i's zu erzeugen.
                dTemp = Math.Max(2, (int)((1 - rho) * dTemp));
                // Iteriere so lange, bis die Breite des Gitters gering genug ist.
            } while (GetLeftRange(rlTemp, rcTemp) > rangeLeftLimit || GetRightRange(rcTemp, ruTemp) > rangeRightLimit);
            ret = new Tripel<List<ICurve2D>, GeoPoint2D, GeoVector2D>(arcs[index], endPoint, dirEnd);
            if (poly) flag = dist[index] < maxErrorArc;
            else flag = dist[index] < maxError;
            return ret;
        }

        // Gibt den Index des kleinsten Arrayelements zurück.
        private static int MinIndex(double[] array)
        {
            int index = 0;
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] < array[index]) index = i;
            }
            return index;
        }

        // Gibt den Index des größten Arrayelements zurück.
        private static int MaxIndex(double[] array)
        {
            int index = 0;
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] > array[index]) index = i;
            }
            return index;
        }

        // Berechnet die Breite der linken Gitterhälfte.
        private static double GetLeftRange(double rl, double rc)
        {
            if (rl >= rc || (rl < (1.0 - Precision.eps) && rc > (1.0 + Precision.eps))) return 0.0;
            else if (rc <= (1.0 + Precision.eps)) return ((1 / rl) - (1 / rc));
            else return (rc - rl);
        }

        // Berechnet die Breite der rechten Gitterhälfte.
        private static double GetRightRange(double rc, double ru)
        {
            if (ru <= rc || (rc < (1.0 - Precision.eps) && ru > (1.0 + Precision.eps))) return 0.0;
            else if (ru <= (1.0 + Precision.eps)) return ((1 / rc) - (1 / ru));
            else return (ru - rc);
        }

        // Gibt ein Array aus, dass das Gitter für r über dem Intervall [rl,ru] beinhaltet.
        // Es müssen entweder rl,rc,ru <= (>=) 1 sein, oder rc muss gleich 1 sein und rl < 1 und ru > 1.
        private static double[] GetR(double rl, double rc, double ru, int d)
        {
            double[] r = new double[2 * d + 1];
            if (rl >= rc || ru <= rc || (rc >= (1.0 + Precision.eps) && rl <= (1.0 - Precision.eps)) || (rc <= (1.0 - Precision.eps) && ru >= (1.0 + Precision.eps))) return r;
            for (int i = 0; i < d + 1; i++)
            {
                if (rc <= (1.0 + Precision.eps) && rc >= (1.0 - Precision.eps))
                {
                    r[i] = 1 / (((double)(d - i) / d) * (1 / rl) + ((double)i / d) * (1 / rc));
                    r[i + d] = ((double)(d - i) / d) * rc + ((double)i / d) * ru;
                }
                else if (ru <= (1.0 - Precision.eps))
                {
                    r[i] = 1 / (((double)(d - i) / d) * (1 / rl) + ((double)i / d) * (1 / rc));
                    r[i + d] = 1 / (((double)(d - i) / d) * (1 / rc) + ((double)i / d) * (1 / ru));
                }
                else
                {
                    r[i] = ((double)(d - i) / d) * rl + ((double)i / d) * rc;
                    r[i + d] = ((double)(d - i) / d) * rc + ((double)i / d) * ru;
                }
            }
            return r;
        }

        // Erzeugt den entsprechenden Bi-Arc zu einem gewissen Quotienten r.
        private List<ICurve2D> CreateBiArc(double r, GeoPoint2D startPoint, GeoPoint2D endPoint, GeoVector2D startDir, GeoVector2D endDir)
        {
            List<ICurve2D> arcs = new List<ICurve2D>();
            double beta = GetBeta(r, startPoint, endPoint, startDir, endDir);
            double alpha = r * beta;
            double x, y;
            x = (startPoint.ToVector() + alpha * startDir).x;
            y = (startPoint.ToVector() + alpha * startDir).y;
            // Hilfspunkt zur Berechnung des Übergangspunktes.
            GeoPoint2D pointl = new GeoPoint2D(x, y);
            x = (endPoint.ToVector() - beta * endDir).x;
            y = (endPoint.ToVector() - beta * endDir).y;
            // Hilfspunkt zur Berechnung des Übergangspunktes.
            GeoPoint2D pointr = new GeoPoint2D(x, y);
            // Übergangspunkt der beiden Kreisbögen. alpha und beta sind durch Wahl von r bestimmt.
            GeoPoint2D pointm = (new GeoPoint2D((beta / (alpha + beta)) * pointl.x, (beta / (alpha + beta)) * pointl.y) + ((alpha / (alpha + beta)) * pointr.ToVector()));
            // In welche Richtung der linke Kreisbogen durchlaufen wird.
            bool counterClockLeft = Geometry.OnLeftSide(pointm, startPoint, startDir);
            // In welche Richtung der rechte Kreisbogen durchlaufen wird.
            bool counterClockRight = Geometry.OnLeftSide(pointm, endPoint, endDir);
            arcs.Add(CreateArc(startPoint, pointm, pointl, true, counterClockLeft));
            arcs.Add(CreateArc(pointm, endPoint, pointr, false, counterClockRight));
            return arcs;
        }

        // Erzeugt einen Kreisbogen. left gibt an, ob es der linke oder der rechte Kreisbogen des Biarcs ist
        // und counterClockwise zeigt an, in welche Richtung der Kreisbogen durchlaufen wird.
        private Arc2D CreateArc(GeoPoint2D startPoint, GeoPoint2D endPoint, GeoPoint2D helpPoint, bool left, bool counterClockwise)
        {
            if (left)
            {
                // Der Vektor vom Startpunkt zum Hilfspunkt wird um 90 Grad gedreht.
                GeoVector2D vector1 = RotateVector(helpPoint - startPoint, new Angle(Math.PI / 2), counterClockwise);
                // Es wird ein gleichschenkliges Dreieck erzeugt, damit der Schnittpunkt der beiden Linien den
                // Kreismittelpunkt des gesuchten Kreisbogens liefert.
                Angle a = new Angle(helpPoint - startPoint, endPoint - startPoint);
                GeoVector2D vector2;
                if (a < Math.PI / 2)
                {
                    a = (Math.PI / 2) - a;
                    vector2 = RotateVector(startPoint - endPoint, a, !counterClockwise);
                }
                else
                {
                    a = a - (new Angle(Math.PI / 2));
                    vector2 = RotateVector(startPoint - endPoint, a, counterClockwise);
                }
                //Angle a = new Angle(vector1, endPoint - startPoint);
                //GeoVector2D vector2 = RotateVector(startPoint - endPoint, a, !counterClockwise);
                GeoPoint2D center;
                Geometry.IntersectLL(startPoint, vector1, endPoint, vector2, out center);
                // Radius wird berechnet.
                double r1 = (startPoint - center).Length;
                double r2 = (endPoint - center).Length;
                // Sollte nicht passieren.
                //if (Math.Abs(r1 - r2) > Precision.eps) throw new Exception();
                return new Arc2D(center, r1, startPoint, endPoint, counterClockwise);
            }
            else
            {
                // Der Vektor vom Startpunkt zum Hilfspunkt wird um 90 Grad gedreht.
                GeoVector2D vector1 = RotateVector(helpPoint - endPoint, new Angle(Math.PI / 2), !counterClockwise);
                // Es wird ein gleichschenkliges Dreieck erzeugt, damit der Schnittpunkt der beiden Linien den
                // Kreismittelpunkt des gesuchten Kreisbogens liefert.
                Angle b = new Angle(helpPoint - endPoint, startPoint - endPoint);
                GeoVector2D vector2;
                if (b < Math.PI / 2)
                {
                    b = (Math.PI / 2) - b;
                    vector2 = RotateVector(endPoint - startPoint, b, counterClockwise);
                }
                else
                {
                    b = b - (new Angle(Math.PI / 2));
                    vector2 = RotateVector(endPoint - startPoint, b, !counterClockwise);
                }
                //Angle b = new Angle(vector1, startPoint - endPoint);
                //GeoVector2D vector2 = RotateVector(endPoint - startPoint, b, counterClockwise);
                GeoPoint2D center;
                Geometry.IntersectLL(endPoint, vector1, startPoint, vector2, out center);
                // Radius wird berechnet.
                double r1 = (startPoint - center).Length;
                double r2 = (endPoint - center).Length;
                // Sollte nicht passieren.
                //if (Math.Abs(r1 - r2) > Precision.eps) throw new Exception();
                return new Arc2D(center, r1, startPoint, endPoint, counterClockwise);
            }

        }

        // Dreht einen Vektor um einen bestimmten Winkel.
        private static GeoVector2D RotateVector(GeoVector2D vector, Angle angle, bool counterClockwise)
        {
            double rad = angle.Radian;
            double x = vector.x;
            double y = vector.y;
            if (counterClockwise)
            {
                return (new GeoVector2D(x * Math.Cos(rad) - y * Math.Sin(rad), y * Math.Cos(rad) + x * Math.Sin(rad)));
            }
            else
            {
                return (new GeoVector2D(x * Math.Cos(rad) + y * Math.Sin(rad), y * Math.Cos(rad) - x * Math.Sin(rad)));
            }
        }

        // Löst eine quadratische Gleichung, um die nötigen Parameter für den zu erzeugenden BiArc
        // zu bekommen.
        private static double GetBeta(double r, GeoPoint2D startPoint, GeoPoint2D endPoint, GeoVector2D startDir, GeoVector2D endDir)
        {
            GeoVector2D v = startPoint - endPoint;
            double c = v * v;
            double b = 2 * (v * ((r * startDir) + endDir));
            double a = 2 * r * ((startDir * endDir) - 1);
            double x, y = 0.0;
            int e = Quadgl(a, b, c, out x, out y);
            if (x < 0.0)
            {
                if (y < 0.0) throw new Exception();
                else return y;
            }
            return x;
        }

        /// <summary>
        /// Summary of quadgl.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        private static int Quadgl(double a, double b, double c, out double x, out double y)
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
                d = (b * b) - 4 * a * c;
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

        // Liefert das größte Element eines double-Arrays.
        private static double ArrayMax(double[] dArray)
        {
            if (dArray.Length == 0) return 0.0;
            double max = dArray[0];
            foreach (double d in dArray) if (d > max) max = d;
            return max;
        }

        // Berechnet die Distanz von einem Polygonzug zu einem BiArc.
        private static double PolyBiArcDistance(Arc2D left, Arc2D right, GeoPoint2D[] vertex)
        {
            if (vertex.Length < 2) throw new Exception();
            if (Math.Abs(left.Sweep) < Math.PI && Math.Abs(right.Sweep) < Math.PI)
            {
                // Falls die Überstreichwinkel der Kreisbögen weniger als 180 Grad betragen, lässt sich einfach
                // überprüfen, ob eine Linie zwischen den Begrenzungen eines Kreisbogens, gegeben durch die
                // Linien zwischen Mittelpunkt und Startpunkt, sowie Mittelpunkt und Endpunkt, liegt.
                double[] distances = new double[vertex.Length - 1];
                Line2D[] lines = new Line2D[vertex.Length - 1];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = new Line2D(vertex[i], vertex[i + 1]);
                    distances[i] = LineBiArcDistance(left, right, lines[i]);
                }
                return ArrayMax(distances);
            }
            else
            {
                double[][] distances = new double[2][];
                // Fehler des linken Kreisbogen.
                distances[0] = new double[vertex.Length - 1];
                // Fehler des rechten Kreisbogen.
                distances[1] = new double[vertex.Length - 1];
                Line2D[] lines = new Line2D[vertex.Length - 1];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = new Line2D(vertex[i], vertex[i + 1]);
                    distances[0][i] = OldLineArcDistance(left, lines[i]);
                    distances[1][i] = OldLineArcDistance(right, lines[i]);
                    if (distances[1][i] < distances[0][i]) distances[0][i] = distances[1][i];
                }
                return ArrayMax(distances[0]);
            }
        }

        // Berechnet die Distanz von einer Linie zu einem BiArc.
        private static double LineBiArcDistance(Arc2D left, Arc2D right, Line2D line)
        {
            GeoPoint2D startPoint = line.StartPoint;
            GeoPoint2D endPoint = line.EndPoint;
            if (Geometry.OnSameSide(startPoint, left.StartPoint, left.EndPoint, left.EndDirection.ToLeft()) && Geometry.OnSameSide(endPoint, left.StartPoint, left.EndPoint, left.EndDirection.ToLeft()))
            {
                // Liegt die Linie zwischen den Begrenzungen des linken Kreisbogen, dann kann diese Distanz
                // berechnet werden.
                return LineArcDistance(left, line);
            }
            else if (Geometry.OnSameSide(startPoint, right.EndPoint, right.StartPoint, right.StartDirection.ToLeft()) && Geometry.OnSameSide(endPoint, right.EndPoint, right.StartPoint, right.StartDirection.ToLeft()))
            {
                // Liegt die Linie zwischen den Begrenzungen des rechten Kreisbogen, dann kann diese Distanz
                // berechnet werden.
                return LineArcDistance(right, line);
            }
            else
            {
                // Die Linie wird in zwei Linien unterteilt, wovon eine zwischen den Begrenzungen des linken
                // Kreisbogen und die andere zwischen den Begrenzungen des rechten Kreisbogen liegt.
                GeoPoint2D intPoint;
                Geometry.IntersectLL(startPoint, line.StartDirection, left.EndPoint, left.EndDirection.ToLeft(), out intPoint);
                return Math.Max(LineArcDistance(left, new Line2D(startPoint, intPoint)), LineArcDistance(right, new Line2D(intPoint, endPoint)));
            }
        }

        // Berechnet die Distanz von einer Linie zu einem Kreisbogen. Um das richtige Ergebnis zu bekommen, muss die
        // Linie zwischen den Begrenzungen des Kreisbogens liegen.
        private static double LineArcDistance(Arc2D arc, Line2D line)
        {
            GeoPoint2D center = arc.Center;
            GeoPoint2D startPoint = line.StartPoint;
            GeoPoint2D endPoint = line.EndPoint;
            GeoPoint2D midPoint = Geometry.DropPL(center, startPoint, endPoint);
            return Math.Max(Math.Abs((midPoint - center).Length - arc.Radius), Math.Max(Math.Abs((startPoint - center).Length - arc.Radius), Math.Abs((endPoint - center).Length - arc.Radius)));
        }

        // Berechnet die Distanz von einer Linie zu einem Kreisbogen. Hierzu muss die Linie nicht zwischen den
        // Begrenzungen des Kreisbogens liegen.
        private static double OldLineArcDistance(Arc2D arc, Line2D line)
        {
            double[] tangents = arc.TangentPointsToAngle(line.StartDirection);
            GeoPoint2D foot, tangentPoint;
            if (tangents.Length == 0)
            {
                double startDist = arc.MinDistance(line.StartPoint);
                double endDist = arc.MinDistance(line.EndPoint);
                return Math.Max(startDist, endDist);
            }
            else
            {
                double[] distances = new double[tangents.Length];
                for (int i = 0; i < distances.Length; i++)
                {
                    tangentPoint = arc.PointAt(tangents[i]);
                    foot = Geometry.DropPL(tangentPoint, line.StartPoint, line.EndPoint);
                    double tangentDist = (tangentPoint - foot).Length;
                    double startDist = arc.MinDistance(line.StartPoint);
                    double endDist = arc.MinDistance(line.EndPoint);
                    distances[i] = Math.Max(tangentDist, Math.Max(startDist, endDist));
                }
                return ArrayMax(distances);
            }
        }

        // Berechnet die Distanz von den Gitterpunkten eines Kurventeilstücks zu einem BiArc.
        private double GitterBiArcDistance(Arc2D left, Arc2D right, double startPar, double endPar)
        {
            double dist = 0.0;
            GeoPoint2D curvePoint;
            if (Math.Abs(left.Sweep) < Math.PI && Math.Abs(right.Sweep) < Math.PI)
            {
                // Falls die Überstreichwinkel der Kreisbögen weniger als 180 Grad betragen, lässt sich einfach
                // überprüfen, ob ein Punkt zwischen den Begrenzungen eines Kreisbogens, gegeben durch die
                // Linien zwischen Mittelpunkt und Startpunkt, sowie Mittelpunkt und Endpunkt, liegt.
                for (int k = 0; k <= anzahlGitter; k++)
                {
                    curvePoint = GetCurvePoint(startPar + k * (1.0 / anzahlGitter) * (endPar - startPar));
                    if (Geometry.OnSameSide(left.StartPoint, curvePoint, left.EndPoint, left.EndDirection.ToLeft()))
                    {
                        dist = Math.Max(dist, Math.Abs((curvePoint - left.Center).Length - left.Radius));
                    }
                    else
                    {
                        dist = Math.Max(dist, Math.Abs((curvePoint - right.Center).Length - right.Radius));
                    }
                }
                return dist;
            }
            else
            {
                double temp, temp2;
                for (int i = 0; i <= anzahlGitter; i++)
                {
                    curvePoint = GetCurvePoint(startPar + i * (1.0 / anzahlGitter) * (endPar - startPar));
                    temp = left.MinDistance(curvePoint);
                    temp2 = right.MinDistance(curvePoint);
                    dist = Math.Max(dist, Math.Min(temp, temp2));
                }
                return dist;
            }
        }

        // Berechnet die Distanz von den Gitterpunkten eines Kurventeilstücks zu einer Linie/Kreisbogen-, bzw.
        // einer Kreisbogen/Linie-Kombination. Der bool-Wert arcLine zeigt an, ob es sich um eine Kreisbogen/Linie-,
        // oder um eine Linie/Kreisbogen-Kombination handelt.
        private double GitterDistanceArcLine(Arc2D arc, Line2D line, double startPar, double endPar, bool arcLine)
        {
            double dist = 0.0;
            double distTemp = 0.0;
            GeoPoint2D curvePoint;
            if (Math.Abs(arc.Sweep) < Math.PI)
            {
                for (int k = 0; k < anzahlGitter; k++)
                {
                    curvePoint = GetCurvePoint(startPar + k * (1.0 / anzahlGitter) * (endPar - startPar));
                    distTemp = line.MinDistance(curvePoint);
                    if (arcLine)
                    {
                        if (Geometry.OnSameSide(arc.StartPoint, curvePoint, arc.EndPoint, arc.EndDirection.ToLeft()))
                        {
                            distTemp = Math.Min(distTemp, Math.Abs((curvePoint - arc.Center).Length - arc.Radius));
                        }
                    }
                    else
                    {
                        if (Geometry.OnSameSide(arc.EndPoint, curvePoint, arc.StartPoint, arc.StartDirection.ToLeft()))
                        {
                            distTemp = Math.Min(distTemp, Math.Abs((curvePoint - arc.Center).Length - arc.Radius));
                        }
                    }
                    dist = Math.Max(dist, distTemp);
                }
                return dist;
            }
            else
            {
                double temp, temp2;
                for (int i = 0; i <= anzahlGitter; i++)
                {
                    curvePoint = GetCurvePoint(startPar + i * (1.0 / anzahlGitter) * (endPar - startPar));
                    temp = arc.MinDistance(curvePoint);
                    temp2 = line.MinDistance(curvePoint);
                    dist = Math.Max(dist, Math.Min(temp, temp2));
                }
                return dist;
            }
        }

        // Berechnet die Distanz von einem Polygonzug zu einer Linie/Kreisbogen-, bzw. einer Kreisbogen/Linie-
        // Kombination. Der bool-Wert arcLine zeigt an, ob es sich um eine Kreisbogen/Linie-, oder um eine
        // Linie/Kreisbogen-Kombination handelt.
        private static double PolyDistanceArcLine(Arc2D arc, Line2D line, GeoPoint2D[] vertex, bool arcLine)
        {
            if (vertex.Length < 2) throw new Exception();
            if (Math.Abs(arc.Sweep) < Math.PI)
            {
                double[] distances = new double[vertex.Length - 1];
                Line2D[] lines = new Line2D[vertex.Length - 1];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = new Line2D(vertex[i], vertex[i + 1]);
                    if (Geometry.OnSameSide(lines[i].StartPoint, arc.StartPoint, arc.EndPoint, arc.EndDirection.ToLeft()) && Geometry.OnSameSide(lines[i].EndPoint, arc.StartPoint, arc.EndPoint, arc.EndDirection.ToLeft()) && Geometry.OnSameSide(lines[i].StartPoint, arc.EndPoint, arc.StartPoint, arc.StartDirection.ToLeft()) && Geometry.OnSameSide(lines[i].EndPoint, arc.EndPoint, arc.StartPoint, arc.StartDirection.ToLeft()))
                    {
                        distances[i] = Math.Min(LineArcDistance(arc, lines[i]), DistanceLineLine(lines[i], line));
                    }
                    else
                    {
                        if (arcLine)
                        {
                            if (Geometry.OnSameSide(lines[i].StartPoint, line.EndPoint, arc.EndPoint, arc.EndDirection.ToLeft()))
                            {
                                distances[i] = DistanceLineLine(lines[i], line);
                            }
                            else
                            {
                                GeoPoint2D intPoint;
                                Geometry.IntersectLL(lines[i].StartPoint, lines[i].StartDirection, arc.EndPoint, arc.EndDirection.ToLeft(), out intPoint);
                                Line2D lineL = new Line2D(lines[i].StartPoint, intPoint);
                                Line2D lineR = new Line2D(intPoint, lines[i].EndPoint);
                                distances[i] = Math.Max(Math.Min(LineArcDistance(arc, lineL), DistanceLineLine(lineL, line)), DistanceLineLine(lineR, line));
                            }
                        }
                        else
                        {
                            if (Geometry.OnSameSide(lines[i].EndPoint, line.StartPoint, arc.StartPoint, arc.StartDirection.ToLeft()))
                            {
                                distances[i] = DistanceLineLine(lines[i], line);
                            }
                            else
                            {
                                GeoPoint2D intPoint;
                                Geometry.IntersectLL(lines[i].StartPoint, lines[i].StartDirection, arc.StartPoint, arc.StartDirection.ToLeft(), out intPoint);
                                Line2D lineL = new Line2D(lines[i].StartPoint, intPoint);
                                Line2D lineR = new Line2D(intPoint, lines[i].EndPoint);
                                distances[i] = Math.Max(Math.Min(LineArcDistance(arc, lineR), DistanceLineLine(lineR, line)), DistanceLineLine(lineL, line));
                            }
                        }
                    }
                }
                return ArrayMax(distances);
            }
            else
            {
                double[][] distances = new double[2][];
                distances[0] = new double[vertex.Length - 1];
                distances[1] = new double[vertex.Length - 1];
                Line2D[] lines = new Line2D[vertex.Length - 1];
                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = new Line2D(vertex[i], vertex[i + 1]);
                    distances[0][i] = OldLineArcDistance(arc, lines[i]);
                    distances[1][i] = DistanceLineLine(lines[i], line);
                    if (distances[1][i] < distances[0][i]) distances[0][i] = distances[1][i];
                }
                return ArrayMax(distances[0]);
            }
        }

        // Berechnet die maximale Distanz zwischen zwei Linien.
        private static double DistanceLineLine(Line2D line1, Line2D line2)
        {
            GeoPoint2D drop1 = Geometry.DropPL(line1.StartPoint, line2.StartPoint, line2.EndPoint);
            GeoPoint2D drop2 = Geometry.DropPL(line1.EndPoint, line2.StartPoint, line2.EndPoint);
            GeoPoint2D drop3 = Geometry.DropPL(line2.StartPoint, line1.StartPoint, line1.EndPoint);
            GeoPoint2D drop4 = Geometry.DropPL(line2.EndPoint, line1.StartPoint, line1.EndPoint);
            double pos1 = line2.PositionOf(drop1);
            double pos2 = line2.PositionOf(drop2);
            double pos3 = line1.PositionOf(drop3);
            double pos4 = line1.PositionOf(drop4);
            double temp1 = Math.Min((line1.StartPoint - line2.StartPoint).Length, (line1.StartPoint - line2.EndPoint).Length);
            double temp2 = Math.Min((line1.EndPoint - line2.StartPoint).Length, (line1.EndPoint - line2.EndPoint).Length);
            double temp3 = Math.Min((line2.StartPoint - line1.StartPoint).Length, (line2.StartPoint - line1.EndPoint).Length);
            double temp4 = Math.Min((line2.EndPoint - line1.StartPoint).Length, (line2.EndPoint - line1.EndPoint).Length);
            if (pos1 > 0.0 && pos1 < 1.0) temp1 = (drop1 - line1.StartPoint).Length;
            if (pos2 > 0.0 && pos2 < 1.0) temp2 = (drop2 - line1.EndPoint).Length;
            if (pos3 > 0.0 && pos3 < 1.0) temp3 = (drop3 - line2.StartPoint).Length;
            if (pos4 > 0.0 && pos4 < 1.0) temp4 = (drop4 - line2.EndPoint).Length;
            return Math.Max(Math.Max(temp1, temp2), Math.Max(temp3, temp4));
        }

        // Testet, ob der gesuchte Kurvenpunkt schon bekannt ist, und liefert den im Dictionary gespeicherten
        // Punkt zurück falls bekannt. Sonst wird der Punkt berechnet und im Dictionary gespeichert.
        private GeoPoint2D GetCurvePoint(double par)
        {
            GeoPoint2D curvePoint;
            if (curvePoints.TryGetValue(par, out curvePoint)) return curvePoint;
            else
            {
                curvePoint = curve.PointAt(par);
                curvePoints.Add(par, curvePoint);
                return curvePoint;
            }
        }
    }

    // Klasse zur Approximation einer 3D-Kurve durch stückweise Biarcs oder Linien.
    // Die Biarcs können entweder optimal oder möglichst gleichmäßig (schnellerer Algorithmus) gewählt werden.
    // Hierzu wird die Kurve direkt durch stückweise Kreisbögen und Linien approximiert und die Distanz
    // zwischen Kurve und approximierendem Spline über Gitterpunkte ausgerechnet.
    internal class ArcLineFitting3D
    {

        private ICurve curve; // Die Kurve, die approximiert werden soll.
        /*private bool kruemmung; // Ob die Kreisbogen über die Krümmung an Start- und Endpunkt bestimmt werden sollen,
        // oder durch Iteration. funktioniert nicht richtig.*/
        private bool lazy; // Lässt den Algorithmus bei true den optimalen Biarc suchen, bei false wird die
        // Iteration abgebrochen, sobald der Fehler unterschritten wird.
        private int anzahlGitter; // Anzahl der Gitterpunkte bei Distanzbestimmung.
        private double maxError; // Der maximal erlaubte Fehler.
        private int d; // Bestimmt wie viele r_i's bei der Iteration erzeugt werden. (2 * d + 1)
        private double rho; // Bestimmt, wie stark d während der Iteration verkleinert wird. Bei jedem Schritt
        // wird d mit (1 - rho) multipliziert.
        private double ru; // Obergrenze für r. r ist der Quotient alpha / beta. alpha und beta werden benutzt,
        // um den jeweiligen Mittelpunkt auszurechnen, der mit Startpunkt, Startrichtung, Endpunkt und Endrichtung
        // einen Biarc definiert.
        private double rc; // Mittlerer Wert für r.
        private double rl; // Untergrenze für r.
        private SortedDictionary<double, GeoPoint> curvePoints; // Speichert Kurvenpunkte zur Zeitersparnis.
        private Path approx; // Der Pfad aus Kreisbögen und Linien, der die Kurve approximiert.


        // Konstruktor für BiArc Fitting.
        public ArcLineFitting3D(ICurve curve, double maxError, bool lazy, int anzahlGitter)
        {
            this.curve = curve;
            //this.kruemmung = kruemmung;
            this.lazy = lazy;
            curvePoints = new SortedDictionary<double, GeoPoint>();
            this.anzahlGitter = anzahlGitter;
            this.maxError = maxError;
            d = 10;
            rho = 0.25;
            ru = 5.0;
            rc = 1.0;
            rl = 0.2;
            this.ApproxSpline();
        }

        // Konstruktor für BiArc Fitting.
        public ArcLineFitting3D(ICurve curve, double maxError, bool lazy, int anzahlGitter, int d, double rho, double ru, double rc, double rl)
        {
            this.curve = curve;
            //this.kruemmung = kruemmung;
            this.lazy = lazy;
            curvePoints = new SortedDictionary<double, GeoPoint>();
            this.anzahlGitter = anzahlGitter;
            this.maxError = maxError;
            this.d = d;
            if (rho > 0.0 && rho < 1.0) this.rho = rho;
            else this.rho = 0.25;
            if (rl < rc && rc < ru && (ru < 1.0 || rl > 1.0 || (rc < 1.0 + Precision.eps && rc > 1.0 - Precision.eps)))
            {
                this.ru = ru;
                this.rc = rc;
                this.rl = rl;
            }
            else
            {
                this.ru = 5.0;
                this.rc = 1.0;
                this.rl = 0.2;
            }
            this.ApproxSpline();
        }



        public Path Approx
        {
            get { return approx; }
        }

        /*public bool Kruemmung
        {
            get { return kruemmung; }
            set { kruemmung = value; }
        }*/

        public bool Lazy
        {
            get { return lazy; }
            set { lazy = value; }
        }

        public int AnzahlGitterpunkte
        {
            get { return anzahlGitter; }
            set { anzahlGitter = value; }
        }

        public double MaxError
        {
            get { return maxError; }
            set { maxError = value; }
        }

        public int D
        {
            get { return d; }
            set { d = value; }
        }

        public double Rho
        {
            get { return rho; }
            set { rho = value; }
        }

        public double Ru
        {
            get { return ru; }
            set { ru = value; }
        }

        public double Rc
        {
            get { return rc; }
            set { rc = value; }
        }

        public double Rl
        {
            get { return rl; }
            set { rl = value; }
        }



        // Bekommt als Input eine 3D-Kurve und erstellt den Path, der aus aneinandergesetzten Kreisbögen
        // und Linien besteht und der die Kurve mit maxError Genauigkeit approximiert.
        private void ApproxSpline()
        {
            Path temp = Path.Construct();
            // Teilt die Kurve zunächst in von der Methode GetSavePositions bereitgestellte Segmente auf,
            // für die dann die rekursive Methode aufgerufen wird.
            double[] positions = curve.GetSavePositions();
            Tripel<List<ICurve>, GeoPoint, GeoVector> tempTripel;
            GeoPoint tempStartPoint = curve.StartPoint;
            GeoVector tempStartDirection = curve.StartDirection;
            double pos = 0.0;
            while (tempStartDirection.Length < 1e-10)
            {
                pos += 1e-2;
                tempStartDirection = curve.DirectionAt(pos);
            }
            for (int i = 0; i < positions.Length - 1; i++)
            {
                tempTripel = ApproxSplineRec(positions[i], positions[i + 1], tempStartPoint, tempStartDirection);
                foreach (ICurve segment in (tempTripel.First.ToArray())) temp.Add(segment);
                tempStartPoint = tempTripel.Second;
                tempStartDirection = tempTripel.Third;
            }
            approx = temp;

            // Aufruf der rekursiven Methode zur Approximierung.
            /*ICurve[] curves = (ApproxSplineRec(0.0, 1.0, curve.StartPoint, curve.StartDirection).First).ToArray();
            foreach (ICurve segment in curves)
            {
                temp.Add(segment);
            }
            approx = temp;*/
        }

        // Rekursive Methode zur Approximierung, die ein Teilstück der Originalkurve -
        // bzw. beim ersten Aufruf die gesamte Kurve - nimmt und versucht, diese durch
        // eine Linie oder einen Biarc zu approximieren. Rückgabe ist eine Liste der einzelnen Stücke.
        // Während der Iteration wird unter Umständen der Endpunkt, bzw. die Endrichtung, des (Teil-)Splines
        // verändert, deshalb bekommt die Methode einen neuen Startpunkt und eine neue Startrichtung als Parameter.
        private Tripel<List<ICurve>, GeoPoint, GeoVector> ApproxSplineRec(double startPar, double endPar, GeoPoint newStartPoint, GeoVector newStartDir)
        {
            Tripel<List<ICurve>, GeoPoint, GeoVector> line, biArc, biArcL, biArcR;
            bool flag;
            GeoVector startDir = newStartDir;
            GeoVector endDir = curve.DirectionAt(endPar);
            startDir.Norm();
            while (endDir.Length < 1e-10)
            {
                endPar -= 1e-2;
                endDir = curve.DirectionAt(endPar);
            }
            endDir.Norm();

            GeoPoint endPoint = GetCurvePoint(endPar);
            if (((newStartPoint | endPoint) < maxError && (endPar - startPar) < 0.5) || (endPar - startPar) < 1e-6)
            {   // Notbremse (zugefügt am 16.11.2011): ein kleinstes Stückchen Linie, aber immer noch falsch: 
                // einfach eine Linie von start- nach endpunkt einfügen
                Line l = Line.Construct();
                l.SetTwoPoints(newStartPoint, endPoint);
                List<ICurve> retlist = new List<ICurve>();
                retlist.Add(l);
                return new Tripel<List<ICurve>, GeoPoint, GeoVector>(retlist, endPoint, l.EndDirection.Normalized); // .Normalized zugefügt am 16.11.2011 G.
            }
            // Teste, ob das Teilstück der Kurve durch eine Linie mit maxErrorArc Genauigkeit approximiert werden
            // kann. In diesem Fall wird eine Linie einem Kreisbogen vorgezogen.
            if ((endPar - startPar) < 0.5)
            {
                line = LineTest(startPar, endPar, newStartPoint, endPoint, startDir, out flag);
                if (flag) return line;
            }

            // Testet, ob das Teilstück der Kurve durch eine Linie und dann einen Kreisbogen oder durch einen
            // Kreisbogen und dann eine Linie angenähert werden kann.
            /*Tripel<Line, Ellipse, double> lineArc = LineArcTest(startPar, endPar, newStartPoint, GetCurvePoint(endPar), startDir, endDir);
            Tripel<Ellipse, Line, double> arcLine = ArcLineTest(startPar, endPar, newStartPoint, GetCurvePoint(endPar), startDir, endDir);
            List<ICurve> tempList = new List<ICurve>();
            if (lineArc.Third < arcLine.Third && lineArc.Third < maxError)
            {
                // Linie/Kreisbogen-Kombination approximiert besser als Kreisbogen/Linie und unterschreitet den Fehler.
                tempList.Add(lineArc.First);
                tempList.Add(lineArc.Second);
                return new Tripel<List<ICurve>, GeoPoint, GeoVector>(tempList, GetCurvePoint(endPar), endDir);
            }
            else if (arcLine.Third < maxError)
            {
                // Kreisbogen/Linie-Kombination approximiert besser als Linie/Kreisbogen und unterschreitet den Fehler.
                tempList.Add(arcLine.First);
                tempList.Add(arcLine.Second);
                return new Tripel<List<ICurve>, GeoPoint, GeoVector>(tempList, GetCurvePoint(endPar), endDir);
            }*/

            // Aufruf der Methode für das Optimal Single Biarc Fitting.
            biArc = OsbFitting(startPar, endPar, newStartPoint, startDir, endDir, out flag);
            // Wird flag als true zurückgegeben, unterschreitet der erzeugte Biarc die Fehlergrenze,
            // sodass dieser verwendet werden kann.
            if (flag)
            {
#if DEBUG
                Angle dbga = new Angle(endDir, biArc.Third);
                double dbgd = newStartPoint | GetCurvePoint(endPar);
#endif
                return biArc;
            }
            else
            {
                // Ist flag false, dann wird die (Teil-)Kurve am mittleren Parameter in 2 weitere Teilsegmente
                // zerlegt und für diese wird die rekursive Methode wieder aufgerufen.
                biArcL = ApproxSplineRec(startPar, (startPar + endPar) / 2, newStartPoint, newStartDir);
                biArcR = ApproxSplineRec((startPar + endPar) / 2, endPar, biArcL.Second, biArcL.Third);
                (biArcL.First).AddRange(biArcR.First);
                biArcL.Second = biArcR.Second;
                biArcL.Third = biArcR.Third;
                return biArcL;
            }
        }

        // Testet, ob das Kurventeilstück durch eine Linie approximiert werden kann.
        private Tripel<List<ICurve>, GeoPoint, GeoVector> LineTest(double startPar, double endPar, GeoPoint startPoint, GeoPoint endPoint, GeoVector dirStart, out bool flag)
        {
            Tripel<List<ICurve>, GeoPoint, GeoVector> ret;
            Tripel<List<ICurve>, GeoPoint, GeoVector> helpT = new Tripel<List<ICurve>, GeoPoint, GeoVector>(new List<ICurve>(), startPoint, dirStart);
            List<ICurve> retList = new List<ICurve>();
            // Abbruchbedingung. Schaut, ob der Endpunkt zu weit von der Linie vom Startpunkt mit Startrichtung
            // entfernt ist.
            if (Math.Abs(Geometry.DistPL(endPoint, startPoint, dirStart)) > maxError)
            {
                flag = false;
                ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(retList, new GeoPoint(), new GeoVector());
                return ret;
            }
            flag = true;
            GeoPoint newEndPoint = Geometry.DropPL(endPoint, startPoint, dirStart);
            Line approx = Line.Construct();
            approx.StartPoint = startPoint;
            approx.EndPoint = newEndPoint;
            double distance = GitterDistanceLine(approx, startPar, endPar);
            if (distance > maxError)
            {
                flag = false;
                ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(retList, new GeoPoint(), new GeoVector());
                return ret;
            }
            retList.Add(approx);
            ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(retList, newEndPoint, approx.EndDirection.Normalized); // .Normalized zugefügt am 16.11.2011 G.
            return ret;
        }

        // Nähert das Kurventeilstück durch die Kombination einer Linie und dann eines
        // Kreisbogens an. Zurückgegeben werden die Linie, der Kreisbogen, sowie der maximale Fehler
        // vom Kurventeilstück zu der erzeugten Linie/Kreisbogen-Kombination.
        public Tripel<Line, Ellipse, double> LineArcTest(double startPar, double endPar, GeoPoint startPoint, GeoPoint endPoint, GeoVector dirStart, GeoVector dirEnd)
        {
            Line line = Line.Construct();
            Ellipse arc = Ellipse.Construct();
            Plane plane = new Plane(startPoint, dirStart, dirEnd);
            if (!plane.Elem(endPoint))
            {
                return new Tripel<Line, Ellipse, double>(line, arc, 2 * maxError);
            }
            GeoPoint2D startPoint2D = plane.Project(startPoint);
            GeoPoint2D endPoint2D = plane.Project(endPoint);
            GeoVector2D dirStart2D = plane.Project(dirStart);
            GeoVector2D dirEnd2D = plane.Project(dirEnd);
            double k, k_line;
            GeoPoint2D center;
            // Zunächst werden die Linienlänge k_line, der Kreisradius k, sowie der Kreismittelpunkt center berechnet.
            // Dabei muss zunächst unterschieden werden, ob die Endrichtung und die Startrichtung, die bei der
            // Berechnung des Kreismittelpunktes um 90 Grad gedreht werden, in die selbe Richtung,
            // oder ob sie in entgegengesetzte Richtungen gedreht werden.
            if (Geometry.OnSameSide(endPoint2D, startPoint2D + dirEnd2D, startPoint2D, dirStart2D))
            {
                k_line = (((endPoint2D.y - startPoint2D.y) * (dirStart2D.y - dirEnd2D.y)) + ((startPoint2D.x - endPoint2D.x) * (dirEnd2D.x - dirStart2D.x))) / ((dirStart2D.y * (dirStart2D.y - dirEnd2D.y)) - (dirStart2D.x * (dirEnd2D.x - dirStart2D.x)));
                k = (startPoint2D.x - endPoint2D.x + (k_line * dirStart2D.x)) / (dirStart2D.y - dirEnd2D.y);
                center = new GeoPoint2D(endPoint2D.x - (k * dirEnd2D.y), endPoint2D.y + (k * dirEnd2D.x));
            }
            else
            {
                k_line = (((endPoint2D.y - startPoint2D.y) * (dirStart2D.y + dirEnd2D.y)) - ((startPoint2D.x - endPoint2D.x) * (dirEnd2D.x + dirStart2D.x))) / ((dirStart2D.y * (dirStart2D.y + dirEnd2D.y)) + (dirStart2D.x * (dirEnd2D.x + dirStart2D.x)));
                k = (startPoint2D.x - endPoint2D.x + (k_line * dirStart2D.x)) / (dirStart2D.y + dirEnd2D.y);
                center = new GeoPoint2D(endPoint2D.x + (k * dirEnd2D.y), endPoint2D.y - (k * dirEnd2D.x));
            }
            bool counterClock = k > 0;
            line.StartPoint = plane.ToGlobal(startPoint2D);
            line.EndPoint = plane.ToGlobal(startPoint2D + (k_line * dirStart2D));
            arc.SetArcPlaneCenterStartEndPoint(plane, center, startPoint2D + (k_line * dirStart2D), endPoint2D, plane, counterClock);
            if (!Precision.SameNotOppositeDirection(dirStart2D, plane.Project(line.StartDirection), false) || !Precision.SameNotOppositeDirection(plane.Project(line.EndDirection), plane.Project(arc.StartDirection), false) || !Precision.SameNotOppositeDirection(plane.Project(arc.EndDirection), dirEnd2D, false))
            {
                // Es kann passieren, dass der Kreisbogen oder Linie, die erzeugt wurden, am Startpunkt, am Endpunkt
                // oder am Übergang zwischen Kreisbogen und Linie in entgegengesetzter Richtung übergehen.
                // Darauf wird hier getestet. Falls Startrichtungen und Endrichtungen, sowie die beiden Richtungen
                // am Übergang nicht gleich sind wird die zurückgegebene Distanz auf einen Wert gesetzt, der auf
                // jeden Fall den gesetzten maximalen Fehler überschreitet, so dass die erzeugten Kreisbogen und
                // Linie nicht benutzt werden.
                return new Tripel<Line, Ellipse, double>(line, arc, 2 * maxError);
            }
            double distance = GitterDistanceArcLine(arc, line, startPar, endPar);
            return new Tripel<Line, Ellipse, double>(line, arc, distance);
        }

        // Nähert das Kurventeilstück durch die Kombination eines Kreisbogens und dann einer
        // Linie an. Zurückgegeben werden die Linie, der Kreisbogen, sowie der maximale Fehler
        // vom Kurventeilstück zu der erzeugten Kreisbogen/Linie-Kombination.
        public Tripel<Ellipse, Line, double> ArcLineTest(double startPar, double endPar, GeoPoint startPoint, GeoPoint endPoint, GeoVector dirStart, GeoVector dirEnd)
        {
            Line line = Line.Construct();
            Ellipse arc = Ellipse.Construct();
            Plane plane = new Plane(startPoint, dirStart, dirEnd);
            if (!plane.Elem(endPoint))
            {
                return new Tripel<Ellipse, Line, double>(arc, line, 2 * maxError);
            }
            GeoPoint2D startPoint2D = plane.Project(startPoint);
            GeoPoint2D endPoint2D = plane.Project(endPoint);
            GeoVector2D dirStart2D = plane.Project(dirStart);
            GeoVector2D dirEnd2D = plane.Project(dirEnd);
            double k, k_line;
            GeoPoint2D center;
            // Zunächst werden die Linienlänge k_line, der Kreisradius k, sowie der Kreismittelpunkt center berechnet.
            // Dabei muss zunächst unterschieden werden, ob die Endrichtung und die Startrichtung, die bei der
            // Berechnung des Kreismittelpunktes um 90 Grad gedreht werden, in die selbe Richtung,
            // oder ob sie in entgegengesetzte Richtungen gedreht werden.
            if (!Geometry.OnSameSide(startPoint2D, endPoint2D + dirStart2D, endPoint2D, dirEnd2D))
            {
                k_line = (((startPoint2D.y - endPoint2D.y) * (dirEnd2D.y - dirStart2D.y)) + ((endPoint2D.x - startPoint2D.x) * (dirStart2D.x - dirEnd2D.x))) / ((dirEnd2D.y * (dirEnd2D.y - dirStart2D.y)) - (dirEnd2D.x * (dirStart2D.x - dirEnd2D.x)));
                k = (endPoint2D.x - startPoint2D.x + (k_line * dirEnd2D.x)) / (dirEnd2D.y - dirStart2D.y);
                center = new GeoPoint2D(startPoint2D.x - (k * dirStart2D.y), startPoint2D.y + (k * dirStart2D.x));
            }
            else
            {
                k_line = (((startPoint2D.y - endPoint2D.y) * (dirEnd2D.y + dirStart2D.y)) - ((endPoint2D.x - startPoint2D.x) * (dirStart2D.x + dirEnd2D.x))) / ((dirEnd2D.y * (dirEnd2D.y + dirStart2D.y)) + (dirEnd2D.x * (dirStart2D.x + dirEnd2D.x)));
                k = (endPoint2D.x - startPoint2D.x + (k_line * dirEnd2D.x)) / (dirEnd2D.y + dirStart2D.y);
                center = new GeoPoint2D(startPoint2D.x + (k * dirStart2D.y), startPoint2D.y - (k * dirStart2D.x));
            }
            bool counterClock = k > 0;
            arc.SetArcPlaneCenterStartEndPoint(plane, center, startPoint2D, endPoint2D + (k_line * dirEnd2D), plane, counterClock);
            line.StartPoint = plane.ToGlobal(endPoint2D + (k_line * dirEnd2D));
            line.EndPoint = plane.ToGlobal(endPoint2D);
            if (!Precision.SameNotOppositeDirection(dirStart2D, plane.Project(arc.StartDirection), false) || !Precision.SameNotOppositeDirection(plane.Project(arc.EndDirection), plane.Project(line.StartDirection), false) || !Precision.SameNotOppositeDirection(plane.Project(line.EndDirection), dirEnd2D, false))
            {
                // Es kann passieren, dass der Kreisbogen oder Linie, die erzeugt wurden, am Startpunkt, am Endpunkt
                // oder am Übergang zwischen Kreisbogen und Linie in entgegengesetzter Richtung übergehen.
                // Darauf wird hier getestet. Falls Startrichtungen und Endrichtungen, sowie die beiden Richtungen
                // am Übergang nicht gleich sind wird die zurückgegebene Distanz auf einen Wert gesetzt, der auf
                // jeden Fall den gesetzten maximalen Fehler überschreitet, so dass die erzeugten Kreisbogen und
                // Linie nicht benutzt werden.
                return new Tripel<Ellipse, Line, double>(arc, line, 2 * maxError);
            }
            double distance = GitterDistanceArcLine(arc, line, startPar, endPar);
            return new Tripel<Ellipse, Line, double>(arc, line, distance);
        }

        // Methode für Optimal Single Biarc Fitting. Gegeben sind Start- und Endparameter,
        // sowie Start- und Endrichtung.
        private Tripel<List<ICurve>, GeoPoint, GeoVector> OsbFitting(double startPar, double endPar, GeoPoint newStartPoint, GeoVector dirStart, GeoVector dirEnd, out bool flag)
        {
            Tripel<List<ICurve>, GeoPoint, GeoVector> ret;
            // Abbruchbedingung, in diesem Fall werden mehr als 2 Kreisbögen benötigt.
            if (dirStart * dirEnd < 1.0 + Precision.eps && dirStart * dirEnd > 1.0 - Precision.eps)
            {
                ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(new List<ICurve>(), new GeoPoint(), new GeoVector());
                flag = false;
                return ret;
            }
            int dTemp = d;
            double ruTemp = ru;
            double rcTemp = rc;
            double rlTemp = rl;
            GeoPoint startPoint = newStartPoint;
            GeoPoint endPoint = GetCurvePoint(endPar);

            /*if (kruemmung)
            {
                GeoPoint pointS, pointE;
                GeoVector dirS, dirE;
                GeoVector deriv2Start;
                GeoVector deriv2End;
                curve.TryPointDeriv2At(startPar, out pointS, out dirS, out deriv2Start);
                curve.TryPointDeriv2At(endPar, out pointE, out dirE, out deriv2End);
                double kruemmungStart = Math.Sqrt((((deriv2Start.z * dirS.y) - (deriv2Start.y * dirS.z)) * ((deriv2Start.z * dirS.y) - (deriv2Start.y * dirS.z))) + (((deriv2Start.x * dirS.z) - (deriv2Start.z * dirS.x)) * ((deriv2Start.x * dirS.z) - (deriv2Start.z * dirS.x))) + (((deriv2Start.y * dirS.x) - (deriv2Start.x * dirS.y)) * ((deriv2Start.y * dirS.x) - (deriv2Start.x * dirS.y)))) / Math.Pow((dirS.x * dirS.x) + (dirS.y * dirS.y) + (dirS.z * dirS.z), 1.5);
                double kruemmungEnd = Math.Sqrt((((deriv2End.z * dirE.y) - (deriv2End.y * dirE.z)) * ((deriv2End.z * dirE.y) - (deriv2End.y * dirE.z))) + (((deriv2End.x * dirE.z) - (deriv2End.z * dirE.x)) * ((deriv2End.x * dirE.z) - (deriv2End.z * dirE.x))) + (((deriv2End.y * dirE.x) - (deriv2End.x * dirE.y)) * ((deriv2End.y * dirE.x) - (deriv2End.x * dirE.y)))) / Math.Pow((dirE.x * dirE.x) + (dirE.y * dirE.y) + (dirE.z * dirE.z), 1.5);
                double quotient = kruemmungStart / kruemmungEnd;
                /*if (quotient > 50 || quotient < 0.02)
                {
                    flag = false;
                    ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(new List<ICurve>(), endPoint, dirEnd);
                    return ret;
                }/
                double beta = NewtonBeta(quotient, startPoint, endPoint, dirStart, dirEnd);
                double alpha = GetAlpha(beta, startPoint, endPoint, dirStart, dirEnd);
                List<ICurve> biarc = CreateBiArc(beta, alpha, startPoint, endPoint, dirStart, dirEnd);
                double distance = GitterBiArcDistance(biarc[0] as Ellipse, biarc[1] as Ellipse, startPar, endPar);
                flag = distance < maxError;
                ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(biarc, endPoint, dirEnd);
                return ret;
            }*/

            // Ziel der Methode ist, einen Quotienten r zu bestimmen, der das 'Größenverhältnis' der beiden
            // Kreisbögen definiert. Durch dieses lässt sich der entsprechende Mittelpunkt definieren, an dem
            // beide Kreisbögen ineinander übergehen. Mit dem Startpunkt, dem Endpunkt, den Richtungen an Start-
            // und Endpunkt, sowie dem Mittelpunkt und der Forderung, dass der Übergang am Mittelpunkt glatt sei,
            // ist der benötigte Biarc eindeutig bestimmt.
            // Es werden zunächst ein Gitter zwischen den Grenzwerten für r erzeugt.
            // Danach wird für jeden Gitterpunkt ein Biarc erzeugt und der Fehler berechnet. Es wird schließlich
            // nach mehreren Iterationen der Gitterpunkt mit dem minimalen Fehler gewählt, oder, je nach Einstellung,
            // der erste Biarc, der den Fehler unterschreitet, akzeptiert.
            double rangeLeftLimit = GetLeftRange(rl, rc) * 0.0001; // Limit, bei dem die Iteration abgebrochen wird.
            double rangeRightLimit = GetRightRange(rc, ru) * 0.0001; // Limit, bei dem die Iteration abgebrochen wird.
            int index = 0;
            double[] r = new double[2 * d + 1];
            double[] dist;
            List<ICurve>[] arcs;
            do
            {
                // Gitter der r's wird erzeugt.
                r = GetR(rlTemp, rcTemp, ruTemp, dTemp);
                dist = new double[r.Length];
                arcs = new List<ICurve>[r.Length];

                int i = r.Length / 2;
                int j = i + 1;
                do
                {
                    if (i >= 0)
                    {
                        // Abbruchbedingung, in diesem Fall werden mehr als 2 Kreisbögen benötigt.
                        if (Math.Abs((startPoint - endPoint) * ((r[i] * dirStart) + dirEnd)) < Precision.eps)
                        {
                            ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(new List<ICurve>(), new GeoPoint(), new GeoVector());
                            flag = false;
                            return ret;
                        }
                        // Erzeugung des Biarcs.
                        arcs[i] = CreateBiArc(r[i], startPoint, endPoint, dirStart, dirEnd);
                        // Berechnen des Fehlers zwischen erzeugtem Biarc und Kurve.
                        dist[i] = GitterBiArcDistance(arcs[i][0] as Ellipse, arcs[i][1] as Ellipse, startPar, endPar);
                        if (lazy)
                        {
                            if (dist[i] < maxError)
                            {
                                ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(arcs[i], endPoint, dirEnd);
                                flag = true;
                                return ret;
                            }
                        }
                    }

                    if (j < r.Length)
                    {
                        // Abbruchbedingung, in diesem Fall werden mehr als 2 Kreisbögen benötigt.
                        if (Math.Abs((startPoint - endPoint) * ((r[j] * dirStart) + dirEnd)) < Precision.eps)
                        {
                            ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(new List<ICurve>(), new GeoPoint(), new GeoVector());
                            flag = false;
                            return ret;
                        }
                        // Erzeugung des Biarcs.
                        arcs[j] = CreateBiArc(r[j], startPoint, endPoint, dirStart, dirEnd);
                        // Berechnen des Fehlers zwischen erzeugtem Biarc und Kurve.
                        dist[j] = GitterBiArcDistance(arcs[j][0] as Ellipse, arcs[j][1] as Ellipse, startPar, endPar);
                        if (lazy)
                        {
                            if (dist[j] < maxError)
                            {
                                ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(arcs[j], endPoint, dirEnd);
                                flag = true;
                                return ret;
                            }
                        }
                    }

                    i--;
                    j++;
                } while (i >= 0 || j < r.Length);
                /*for (int i = 0; i < r.Length; i++)
                {
                    // Abbruchbedingung, in diesem Fall werden mehr als 2 Kreisbögen benötigt.
                    if (Math.Abs((startPoint - endPoint) * ((r[i] * dirStart) + dirEnd)) < Precision.eps)
                    {
                        ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(new List<ICurve>(), new GeoPoint(), new GeoVector());
                        flag = false;
                        return ret;
                    }
                    // Erzeugung des Biarcs.
                    arcs[i] = CreateBiArc(r[i], startPoint, endPoint, dirStart, dirEnd);
                    // Berechnen des Fehlers zwischen erzeugtem Biarc und Kurve.
                    dist[i] = GitterBiArcDistance(arcs[i][0] as Ellipse, arcs[i][1] as Ellipse, startPar, endPar);
                    if (lazy)
                    {
                        if (dist[i] < maxError)
                        {
                            distCumul += dist[i];
                            counter++;

                            ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(arcs[i], endPoint, dirEnd);
                            flag = true;
                            return ret;
                        }
                    }
                }*/
                // Bestimme r_i mit dem geringsten Fehler.
                index = MinIndex(dist);
                if (index == 0)
                {
                    rlTemp = r[0];
                    rcTemp = r[1];
                    ruTemp = r[2];
                }
                else if (index == 2 * dTemp)
                {
                    rlTemp = r[(2 * dTemp) - 2];
                    rcTemp = r[(2 * dTemp) - 1];
                    ruTemp = r[2 * dTemp];
                }
                else
                {
                    rlTemp = r[index - 1];
                    rcTemp = r[index];
                    ruTemp = r[index + 1];
                }
                // Aktualisiere das d, um bei der nächsten Iteration weniger r_i's zu erzeugen.
                dTemp = Math.Max(2, (int)((1 - rho) * dTemp));
                // Iteriere so lange, bis die Breite des Gitters gering genug ist.
            } while (GetLeftRange(rlTemp, rcTemp) > rangeLeftLimit || GetRightRange(rcTemp, ruTemp) > rangeRightLimit);
            ret = new Tripel<List<ICurve>, GeoPoint, GeoVector>(arcs[index], endPoint, dirEnd);
            flag = dist[index] < maxError;
            return ret;
        }

        // Gibt den Index des kleinsten Arrayelements zurück.
        private static int MinIndex(double[] array)
        {
            int index = 0;
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] < array[index]) index = i;
            }
            return index;
        }

        // Gibt den Index des größten Arrayelements zurück.
        private static int MaxIndex(double[] array)
        {
            int index = 0;
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] > array[index]) index = i;
            }
            return index;
        }

        // Berechnet die Breite der linken Gitterhälfte.
        private static double GetLeftRange(double rl, double rc)
        {
            if (rl >= rc || (rl < (1.0 - Precision.eps) && rc > (1.0 + Precision.eps))) return 0.0;
            else if (rc <= (1.0 + Precision.eps)) return ((1 / rl) - (1 / rc));
            else return (rc - rl);
        }

        // Berechnet die Breite der rechten Gitterhälfte.
        private static double GetRightRange(double rc, double ru)
        {
            if (ru <= rc || (rc < (1.0 - Precision.eps) && ru > (1.0 + Precision.eps))) return 0.0;
            else if (ru <= (1.0 + Precision.eps)) return ((1 / rc) - (1 / ru));
            else return (ru - rc);
        }

        // Gibt ein Array aus, dass das Gitter für r über dem Intervall [rl,ru] beinhaltet.
        // Es müssen entweder rl,rc,ru <= (>=) 1 sein, oder rc muss gleich 1 sein und rl < 1 und ru > 1.
        private static double[] GetR(double rl, double rc, double ru, int d)
        {
            double[] r = new double[2 * d + 1];
            if (rl >= rc || ru <= rc || (rc >= (1.0 + Precision.eps) && rl <= (1.0 - Precision.eps)) || (rc <= (1.0 - Precision.eps) && ru >= (1.0 + Precision.eps))) return r;
            for (int i = 0; i < d + 1; i++)
            {
                if (rc <= (1.0 + Precision.eps) && rc >= (1.0 - Precision.eps))
                {
                    r[i] = 1 / (((double)(d - i) / d) * (1 / rl) + ((double)i / d) * (1 / rc));
                    r[i + d] = ((double)(d - i) / d) * rc + ((double)i / d) * ru;
                }
                else if (ru <= (1.0 - Precision.eps))
                {
                    r[i] = 1 / (((double)(d - i) / d) * (1 / rl) + ((double)i / d) * (1 / rc));
                    r[i + d] = 1 / (((double)(d - i) / d) * (1 / rc) + ((double)i / d) * (1 / ru));
                }
                else
                {
                    r[i] = ((double)(d - i) / d) * rl + ((double)i / d) * rc;
                    r[i + d] = ((double)(d - i) / d) * rc + ((double)i / d) * ru;
                }
            }
            return r;
        }

        /*private static double NewtonBeta(double quotient, GeoPoint startPoint, GeoPoint endPoint, GeoVector startDir, GeoVector endDir)
        {
            double x_N = GetStartWert2(0.0, 10.0, true, quotient, startPoint, endPoint, startDir, endDir);
            //double x_N = GetStartWert(quotient, startPoint, endPoint, startDir, endDir);
            double temp, funValue, deriv1;
            int counter = 0;
            do
            {
                if (counter > 1000) throw new Exception();
                Deriv1BetaFun(x_N, quotient, startPoint, endPoint, startDir, endDir, out funValue, out deriv1);
                temp = x_N;
                x_N = temp - (funValue / deriv1);
                counter++;
            } while (Math.Abs(x_N - temp) > Precision.eps);
            return x_N;
        }

        private static double GetStartWert(double quotient, GeoPoint startPoint, GeoPoint endPoint, GeoVector startDir, GeoVector endDir)
        {
            double schrittweite = 0.05;
            double deriv, temp, temp2;
            double fun = -1.0;
            int count = 0;
            do
            {
                temp2 = fun;
                temp = count * schrittweite;
                Deriv1BetaFun(temp, quotient, startPoint, endPoint, startDir, endDir, out fun, out deriv);
                count++;
            } while (Math.Sign(fun) == Math.Sign(temp2));
            if (Math.Abs(deriv) < 0.1)
            {
                schrittweite = schrittweite / 10;
                do
                {
                    temp = temp - schrittweite;
                    temp2 = fun;
                    Deriv1BetaFun(temp, quotient, startPoint, endPoint, startDir, endDir, out fun, out deriv);
                } while (Math.Sign(fun) == Math.Sign(temp2));
            }
            return temp;
            /*double[] gitter = new double[101];
            double temp, temp2;
            int index = 0;
            for (int i = 0; i < gitter.Length; i++)
            {
                temp = i * 0.2;
                Deriv1BetaFun(temp, quotient, startPoint, endPoint, startDir, endDir, out gitter[i], out temp2);
                if (Math.Abs(gitter[i]) < Math.Abs(gitter[index])) index = i;
            }
            return index * 0.2;/
        }

        private static double GetStartWert2(double start, double schrittWeite, bool richtung, double quotient, GeoPoint startPoint, GeoPoint endPoint, GeoVector startDir, GeoVector endDir)
        {
            double fun, deriv, temp2;
            Deriv1BetaFun(start, quotient, startPoint, endPoint, startDir, endDir, out fun, out deriv);
            do
            {
                if (richtung) start = start + schrittWeite;
                else start = start - schrittWeite;
                temp2 = fun;
                Deriv1BetaFun(start, quotient, startPoint, endPoint, startDir, endDir, out fun, out deriv);
            } while (Math.Sign(fun) == Math.Sign(temp2));
            if (Math.Abs(deriv) < 0.1) return GetStartWert2(start, schrittWeite / 2, !richtung, quotient, startPoint, endPoint, startDir, endDir);
            else return start;
        }

        private static double GetAlpha(double beta, GeoPoint startPoint, GeoPoint endPoint, GeoVector startDir, GeoVector endDir)
        {
            GeoVector v = startPoint - endPoint;
            return ((v * v) + ((2 * v) * (beta * endDir))) / ((2 * beta * (1 - (startDir * endDir))) - ((2 * v) * startDir));
        }
        
        private static void Deriv1BetaFun(double beta, double quotient, GeoPoint startPoint, GeoPoint endPoint, GeoVector startDir, GeoVector endDir, out double funValue, out double deriv1)
        {
            GeoVector v = startPoint - endPoint;
            double alpha = GetAlpha(beta, startPoint, endPoint, startDir, endDir);
            double alphaDiff = ((((2 * v) * endDir) * ((2 * beta * (1 - (startDir * endDir))) - ((2 * v) * startDir))) - (((v * v) + ((2 * v) * (beta * endDir))) * (2 * (1 - (startDir * endDir))))) / (((2 * beta * (1 - (startDir * endDir))) - ((2 * v) * startDir)) * ((2 * beta * (1 - (startDir * endDir))) - ((2 * v) * startDir)));
            double inArcCos1 = ((endDir * v) + (alpha * (startDir * endDir)) + beta) / (alpha + beta);
            double inArcCos2 = ((startDir * v) + (beta * (startDir * endDir)) + alpha) / (alpha + beta);
            if ((inArcCos2 < 1.0 + Precision.eps && inArcCos2 > 1.0 - Precision.eps) || (inArcCos2 < -1.0 + Precision.eps && inArcCos2 > -1.0 - Precision.eps) || Math.Abs(inArcCos1) < Precision.eps || Math.Abs(inArcCos2) < Precision.eps)
            { }
            double inArcCos1Diff = ((((alphaDiff * (startDir * endDir)) + 1) * (alpha + beta)) - (((endDir * v) + (alpha * (startDir * endDir)) + beta) * (alphaDiff + 1))) / ((alpha + beta) * (alpha + beta));
            double inArcCos2Diff = ((((startDir * endDir) + alphaDiff) * (alpha + beta)) - (((startDir * v) + (beta * (startDir * endDir)) + alpha) * (alphaDiff + 1))) / ((alpha + beta) * (alpha + beta));
            double inTan1 = 0.5 * Math.Acos(inArcCos1);
            double inTan2 = 0.5 * Math.Acos(inArcCos2);
            double inTan1Diff = 0.5 * (-1.0 / Math.Sqrt(1 - (inArcCos1 * inArcCos1))) * inArcCos1Diff;
            double inTan2Diff = 0.5 * (-1.0 / Math.Sqrt(1 - (inArcCos2 * inArcCos2))) * inArcCos2Diff;
            double oben = beta * Math.Tan(inTan1);
            double unten = alpha * Math.Tan(inTan2);
            double obenDiff = Math.Tan(inTan1) + (beta * (1 / (Math.Cos(inTan1) * Math.Cos(inTan1))) * inTan1Diff);
            double untenDiff = (alphaDiff * Math.Tan(inTan2)) + (alpha * (1 / (Math.Cos(inTan2) * Math.Cos(inTan2))) * inTan2Diff);
            if (Math.Abs(unten) < Precision.eps || Math.Abs(untenDiff) < Precision.eps) { }
            deriv1 = ((obenDiff * unten) - (oben * untenDiff)) / (unten * unten);
            funValue = (oben / unten) - quotient;
            return;
        }*/

        // Erzeugt den entsprechenden Bi-Arc zu einem gewissen Quotienten r.
        private List<ICurve> CreateBiArc(double r, GeoPoint startPoint, GeoPoint endPoint, GeoVector startDir, GeoVector endDir)
        {
            List<ICurve> arcs = new List<ICurve>();
            double beta = GetBeta(r, startPoint, endPoint, startDir, endDir);
            double alpha = r * beta;
            double x, y, z;
            x = (startPoint.ToVector() + alpha * startDir).x;
            y = (startPoint.ToVector() + alpha * startDir).y;
            z = (startPoint.ToVector() + alpha * startDir).z;
            // Hilfspunkt zur Berechnung des Übergangspunktes.
            GeoPoint pointl = new GeoPoint(x, y, z);
            x = (endPoint.ToVector() - beta * endDir).x;
            y = (endPoint.ToVector() - beta * endDir).y;
            z = (endPoint.ToVector() - beta * endDir).z;
            // Hilfspunkt zur Berechnung des Übergangspunktes.
            GeoPoint pointr = new GeoPoint(x, y, z);
            // Übergangspunkt der beiden Kreisbögen. alpha und beta sind durch Wahl von r bestimmt.
            GeoPoint pointm = new GeoPoint(((beta / (alpha + beta)) * pointl.ToVector() + (alpha / (alpha + beta)) * pointr.ToVector()).x, ((beta / (alpha + beta)) * pointl.ToVector() + (alpha / (alpha + beta)) * pointr.ToVector()).y, ((beta / (alpha + beta)) * pointl.ToVector() + (alpha / (alpha + beta)) * pointr.ToVector()).z);
            // Die Ebenen, in denen die beiden Kreisbögen liegen müssen.
            Plane leftPlane = new Plane(startPoint, pointl, pointm);
            Plane rightPlane = new Plane(endPoint, pointr, pointm);
            GeoPoint2D pointm2D = leftPlane.Project(pointm);
            GeoPoint2D startPoint2D = leftPlane.Project(startPoint);
            GeoVector2D startDir2D = leftPlane.Project(startDir);
            // In welche Richtung der linke Kreisbogen durchlaufen wird.
            bool counterClockLeft = Geometry.OnLeftSide(pointm2D, startPoint2D, startDir2D);
            pointm2D = rightPlane.Project(pointm);
            GeoPoint2D endPoint2D = rightPlane.Project(endPoint);
            GeoVector2D endDir2D = rightPlane.Project(endDir);
            // In welche Richtung der rechte Kreisbogen durchlaufen wird.
            bool counterClockRight = !Geometry.OnLeftSide(pointm2D, endPoint2D, endDir2D);
            arcs.Add(CreateArc(startPoint, pointm, pointl, true, counterClockLeft));
            arcs.Add(CreateArc(pointm, endPoint, pointr, false, counterClockRight));
            return arcs;
        }

        /*private List<ICurve> CreateBiArc(double beta, double alpha, GeoPoint startPoint, GeoPoint endPoint, GeoVector startDir, GeoVector endDir)
        {
            List<ICurve> arcs = new List<ICurve>();
            double x, y, z;
            x = (startPoint.ToVector() + alpha * startDir).x;
            y = (startPoint.ToVector() + alpha * startDir).y;
            z = (startPoint.ToVector() + alpha * startDir).z;
            // Hilfspunkt zur Berechnung des Übergangspunktes.
            GeoPoint pointl = new GeoPoint(x, y, z);
            x = (endPoint.ToVector() - beta * endDir).x;
            y = (endPoint.ToVector() - beta * endDir).y;
            z = (endPoint.ToVector() - beta * endDir).z;
            // Hilfspunkt zur Berechnung des Übergangspunktes.
            GeoPoint pointr = new GeoPoint(x, y, z);
            // Übergangspunkt der beiden Kreisbögen. alpha und beta sind durch Wahl von r bestimmt.
            GeoPoint pointm = new GeoPoint(((beta / (alpha + beta)) * pointl.ToVector() + (alpha / (alpha + beta)) * pointr.ToVector()).x, ((beta / (alpha + beta)) * pointl.ToVector() + (alpha / (alpha + beta)) * pointr.ToVector()).y, ((beta / (alpha + beta)) * pointl.ToVector() + (alpha / (alpha + beta)) * pointr.ToVector()).z);
            // Die Ebenen, in denen die beiden Kreisbögen liegen müssen.
            Plane leftPlane = new Plane(startPoint, pointl, pointm);
            Plane rightPlane = new Plane(endPoint, pointr, pointm);
            GeoPoint2D pointm2D = leftPlane.Project(pointm);
            GeoPoint2D startPoint2D = leftPlane.Project(startPoint);
            GeoVector2D startDir2D = leftPlane.Project(startDir);
            // In welche Richtung der linke Kreisbogen durchlaufen wird.
            bool counterClockLeft = Geometry.OnLeftSide(pointm2D, startPoint2D, startDir2D);
            pointm2D = rightPlane.Project(pointm);
            GeoPoint2D endPoint2D = rightPlane.Project(endPoint);
            GeoVector2D endDir2D = rightPlane.Project(endDir);
            // In welche Richtung der rechte Kreisbogen durchlaufen wird.
            bool counterClockRight = !Geometry.OnLeftSide(pointm2D, endPoint2D, endDir2D);
            arcs.Add(CreateArc(startPoint, pointm, pointl, true, counterClockLeft));
            arcs.Add(CreateArc(pointm, endPoint, pointr, false, counterClockRight));
            return arcs;
        }*/

        // Dreht einen Vektor um einen bestimmten Winkel.
        private static GeoVector2D RotateVector(GeoVector2D vector, Angle angle, bool counterClockwise)
        {
            double rad = angle.Radian;
            double x = vector.x;
            double y = vector.y;
            if (counterClockwise)
            {
                return (new GeoVector2D(x * Math.Cos(rad) - y * Math.Sin(rad), y * Math.Cos(rad) + x * Math.Sin(rad)));
            }
            else
            {
                return (new GeoVector2D(x * Math.Cos(rad) + y * Math.Sin(rad), y * Math.Cos(rad) - x * Math.Sin(rad)));
            }
        }

        // Erzeugt einen Kreisbogen. left gibt an, ob es der linke oder der rechte Kreisbogen des Biarcs ist
        // und counterClockwise zeigt an, in welche Richtung der Kreisbogen durchlaufen wird.
        private Ellipse CreateArc(GeoPoint startPoint, GeoPoint endPoint, GeoPoint helpPoint, bool left, bool counterClockwise)
        {
            if (left)
            {
                // Die Ebene, in der der Kreisbogen liegen soll.
                Plane plane = new Plane(startPoint, helpPoint, endPoint);
                // Die Erzeugung des Kreisbogens ist analog zum 2D-Fall, da die gesamten Punkte und Vektoren
                // in der Ebene plane liegen und somit als 2D-Punkte und 2D-Vektoren aufgefasst werden können.
                GeoPoint2D startPoint2Dt = plane.Project(startPoint);
                GeoPoint2D endPoint2Dt = plane.Project(endPoint);
                GeoVector2D startDir2Dt = plane.Project(helpPoint - startPoint);
                GeoVector2D hilfsDir2Dt = plane.Project(startPoint - endPoint);
                GeoVector2D vector12D = RotateVector(startDir2Dt, new Angle(Math.PI / 2), counterClockwise);
                Angle a2D = new Angle(startDir2Dt, plane.Project(endPoint - startPoint));
                GeoVector2D vector22D;
                if (a2D < Math.PI / 2)
                {
                    a2D = (Math.PI / 2) - a2D;
                    vector22D = RotateVector(hilfsDir2Dt, a2D, !counterClockwise);
                }
                else
                {
                    a2D = a2D - (new Angle(Math.PI / 2));
                    vector22D = RotateVector(hilfsDir2Dt, a2D, counterClockwise);
                }
                GeoPoint2D center2D;
                Geometry.IntersectLL(startPoint2Dt, vector12D, endPoint2Dt, vector22D, out center2D);
                Ellipse retT = Ellipse.Construct();
                retT.SetArcPlaneCenterStartEndPoint(plane, center2D, startPoint2Dt, endPoint2Dt, plane, counterClockwise);
                return retT;

                /*// Der Vektor vom Startpunkt zum Hilfspunkt wird um 90 Grad gedreht.
                GeoVector vector1 = RotateVector(plane, helpPoint - startPoint, new Angle(Math.PI / 2), counterClockwise);
                // Es wird ein gleichschenkliges Dreieck erzeugt, damit der Schnittpunkt der beiden Linien den
                // Kreismittelpunkt des gesuchten Kreisbogens liefert.
                Angle a = new Angle(helpPoint - startPoint, endPoint - startPoint);
                GeoVector vector2;
                if (a < Math.PI / 2)
                {
                    a = (Math.PI / 2) - a;
                    vector2 = RotateVector(plane, startPoint - endPoint, a, !counterClockwise);
                }
                else
                {
                    a = a - (new Angle(Math.PI / 2));
                    vector2 = RotateVector(plane, startPoint - endPoint, a, counterClockwise);
                }
                GeoPoint2D center;
                Line line1 = Line.Construct();
                line1.StartPoint = startPoint;
                line1.EndPoint = startPoint + vector1;
                Line line2 = Line.Construct();
                line2.StartPoint = endPoint;
                line2.EndPoint = endPoint + vector2;
                Line2D proj1 = line1.GetProjectedCurve(plane) as Line2D;
                Line2D proj2 = line2.GetProjectedCurve(plane) as Line2D;
                Geometry.IntersectLL(proj1.StartPoint, proj1.StartDirection, proj2.StartPoint, proj2.StartDirection, out center);
                GeoPoint2D startPoint2D = plane.Project(startPoint);
                GeoPoint2D endPoint2D = plane.Project(endPoint);
                Ellipse ret = Ellipse.Construct();
                ret.SetArcPlaneCenterStartEndPoint(plane, center, startPoint2D, endPoint2D, plane, counterClockwise);
                return ret;*/
            }
            else
            {
                // Die Ebene, in der der Kreisbogen liegen soll.
                Plane plane = new Plane(startPoint, helpPoint, endPoint);
                // Die Erzeugung des Kreisbogens ist analog zum 2D-Fall, da die gesamten Punkte und Vektoren
                // in der Ebene plane liegen und somit als 2D-Punkte und 2D-Vektoren aufgefasst werden können.
                GeoPoint2D startPoint2Dt = plane.Project(startPoint);
                GeoPoint2D endPoint2Dt = plane.Project(endPoint);
                GeoVector2D endDir2Dt = plane.Project(helpPoint - endPoint);
                GeoVector2D hilfsDir2Dt = plane.Project(endPoint - startPoint);
                GeoVector2D vector12D = RotateVector(endDir2Dt, new Angle(Math.PI / 2), !counterClockwise);
                Angle b2D = new Angle(endDir2Dt, plane.Project(startPoint - endPoint));
                GeoVector2D vector22D;
                if (b2D < Math.PI / 2)
                {
                    b2D = (Math.PI / 2) - b2D;
                    vector22D = RotateVector(hilfsDir2Dt, b2D, counterClockwise);
                }
                else
                {
                    b2D = b2D - (new Angle(Math.PI / 2));
                    vector22D = RotateVector(hilfsDir2Dt, b2D, !counterClockwise);
                }
                GeoPoint2D center2D;
                bool test = Geometry.IntersectLL(endPoint2Dt, vector12D, startPoint2Dt, vector22D, out center2D);
                Ellipse retT = Ellipse.Construct();
                retT.SetArcPlaneCenterStartEndPoint(plane, center2D, startPoint2Dt, endPoint2Dt, plane, counterClockwise);
                return retT;

                /*// Der Vektor vom Startpunkt zum Hilfspunkt wird um 90 Grad gedreht.
                GeoVector vector1 = RotateVector(plane, helpPoint - startPoint, new Angle(Math.PI / 2), !counterClockwise);
                // Es wird ein gleichschenkliges Dreieck erzeugt, damit der Schnittpunkt der beiden Linien den
                // Kreismittelpunkt des gesuchten Kreisbogens liefert.
                Angle b = new Angle(helpPoint - startPoint, endPoint - startPoint);
                GeoVector vector2;
                if (b < Math.PI / 2)
                {
                    b = (Math.PI / 2) - b;
                    vector2 = RotateVector(plane, endPoint - startPoint, b, counterClockwise);
                }
                else
                {
                    b = b - (new Angle(Math.PI / 2));
                    vector2 = RotateVector(plane, endPoint - startPoint, b, !counterClockwise);
                }
                GeoPoint2D center;
                Line line1 = Line.Construct();
                line1.StartPoint = startPoint;
                line1.EndPoint = startPoint + vector1;
                Line line2 = Line.Construct();
                line2.StartPoint = endPoint;
                line2.EndPoint = endPoint + vector2;
                Line2D proj1 = line1.GetProjectedCurve(plane) as Line2D;
                Line2D proj2 = line2.GetProjectedCurve(plane) as Line2D;
                Geometry.IntersectLL(proj1.StartPoint, proj1.StartDirection, proj2.StartPoint, proj2.StartDirection, out center);
                GeoPoint2D startPoint2D = plane.Project(startPoint);
                GeoPoint2D endPoint2D = plane.Project(endPoint);
                Ellipse ret = Ellipse.Construct();
                ret.SetArcPlaneCenterStartEndPoint(plane, center, startPoint2D, endPoint2D, plane, counterClockwise);
                return ret;*/
            }

        }

        // Dreht einen Vektor in einer Ebene um einen bestimmten Winkel.
        private static GeoVector RotateVector(Plane plane, GeoVector vector, Angle angle, bool counterClockwise)
        {
            GeoVector normal = plane.Normal;
            normal.Norm();
            double rad = angle.Radian;
            double x = vector.x;
            double y = vector.y;
            double z = vector.z;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            double sinM = -sin;
            if (counterClockwise)
            {
                return (new GeoVector((cos + normal.x * normal.x * (1 - cos)) * x + (normal.x * normal.y * (1 - cos) - normal.z * sin) * y + (normal.x * normal.z * (1 - cos) + normal.y * sin) * z, (normal.y * normal.x * (1 - cos) + normal.z * sin) * x + (cos + normal.y * normal.y * (1 - cos)) * y + (normal.y * normal.z * (1 - cos) - normal.x * sin) * z, (normal.z * normal.x * (1 - cos) - normal.y * sin) * x + (normal.z * normal.y * (1 - cos) + normal.x * sin) * y + (cos + normal.z * normal.z * (1 - cos)) * z));
            }
            else
            {
                return (new GeoVector((cos + normal.x * normal.x * (1 - cos)) * x + (normal.x * normal.y * (1 - cos) - normal.z * sinM) * y + (normal.x * normal.z * (1 - cos) + normal.y * sinM) * z, (normal.y * normal.x * (1 - cos) + normal.z * sinM) * x + (cos + normal.y * normal.y * (1 - cos)) * y + (normal.y * normal.z * (1 - cos) - normal.x * sinM) * z, (normal.z * normal.x * (1 - cos) - normal.y * sinM) * x + (normal.z * normal.y * (1 - cos) + normal.x * sinM) * y + (cos + normal.z * normal.z * (1 - cos)) * z));
            }
        }

        // Löst eine quadratische Gleichung, um die nötigen Parameter für den zu erzeugenden BiArc
        // zu bekommen.
        private static double GetBeta(double r, GeoPoint startPoint, GeoPoint endPoint, GeoVector startDir, GeoVector endDir)
        {
            GeoVector v = startPoint - endPoint;
            double c = v * v;
            double b = 2 * (v * ((r * startDir) + endDir));
            double a = 2 * r * ((startDir * endDir) - 1);
            double x, y = 0.0;
            int e = Quadgl(a, b, c, out x, out y);
            if (x < 0.0)
            {
                if (y < 0.0) throw new Exception();
                else return y;
            }
            return x;
        }

        /// <summary>
        /// Summary of quadgl.
        /// </summary>
        /// <param name=a></param>
        /// <param name=b></param>
        /// <param name=c></param>
        /// <param name=x></param>
        /// <param name=y></param>
        private static int Quadgl(double a, double b, double c, out double x, out double y)
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
                d = (b * b) - 4 * a * c;
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

        // Liefert das größte Element eines double-Arrays.
        private static double ArrayMax(double[] dArray)
        {
            if (dArray.Length == 0) return 0.0;
            double max = dArray[0];
            foreach (double d in dArray) if (d > max) max = d;
            return max;
        }

        // Berechnet die Distanz von den Gitterpunkten eines Kurventeilstücks zu einem BiArc.
        private double GitterBiArcDistance(Ellipse left, Ellipse right, double startPar, double endPar)
        {
            double dist = 0.0;
            GeoPoint curvePoint;
            double temp, temp2;
            for (int i = 0; i <= anzahlGitter; i++)
            {
                double test = startPar + i * (1.0 / anzahlGitter) * (endPar - startPar);
                curvePoint = GetCurvePoint(startPar + i * (1.0 / anzahlGitter) * (endPar - startPar));
                temp = (left as ICurve).DistanceTo(curvePoint);
                temp2 = (right as ICurve).DistanceTo(curvePoint);
                dist = Math.Max(dist, Math.Min(temp, temp2));
            }
            return dist;

        }

        // Berechnet die Distanz von den Gitterpunkten eines Kurventeilstücks zu einer Linie/Kreisbogen-, bzw.
        // einer Kreisbogen/Linie-Kombination.
        private double GitterDistanceArcLine(Ellipse arc, Line line, double startPar, double endPar)
        {
            double dist = 0.0;
            GeoPoint curvePoint;
            double temp, temp2;
            for (int i = 0; i <= anzahlGitter; i++)
            {
                curvePoint = GetCurvePoint(startPar + i * (1.0 / anzahlGitter) * (endPar - startPar));
                temp = (arc as ICurve).DistanceTo(curvePoint);
                temp2 = (line as ICurve).DistanceTo(curvePoint);
                dist = Math.Max(dist, Math.Min(temp, temp2));
            }
            return dist;
        }

        // Berechnet die Distanz von den Gitterpunkten eines Kurventeilstücks zu einer Linie.
        private double GitterDistanceLine(Line line, double startPar, double endPar)
        {
            double dist = 0.0;
            GeoPoint curvePoint;
            for (int i = 0; i <= anzahlGitter; i++)
            {
                curvePoint = GetCurvePoint(startPar + i * (1.0 / anzahlGitter) * (endPar - startPar));
                dist = Math.Max(dist, (line as ICurve).DistanceTo(curvePoint));
            }
            return dist;
        }

        // Testet, ob der gesuchte Kurvenpunkt schon bekannt ist, und liefert den im Dictionary gespeicherten
        // Punkt zurück falls bekannt. Sonst wird der Punkt berechnet und im Dictionary gespeichert.
        private GeoPoint GetCurvePoint(double par)
        {
            GeoPoint curvePoint;
            if (curvePoints.TryGetValue(par, out curvePoint)) return curvePoint;
            else
            {
                curvePoint = curve.PointAt(par);
                curvePoints.Add(par, curvePoint);
                return curvePoint;
            }
        }
    }
}
