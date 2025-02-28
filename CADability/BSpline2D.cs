using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;

/*  ITERATIONSVERFAHREN
 * Bekannt sind oft zwei Positionen für den Parameter: einer links und einer rechts von der Lösung.
 * Am einfachsten und sichersten ist halt die Bisection, die immer in der Mitte eines Intervalls teilt.
 * Die braucht aber etwa 45 Schritte um die Lösung auf die gewünschte genauigkeit zu finden.
 * Oft is ein Proportionalitätsfaktor bekannt, der eine gute Schätzung für die Lösung liefert.
 * Wenn man das Intervall an dieser Stelle aufteilt, so liegt die Lösung im neuen Intervall oft hart an der
 * Grenze, z.B. im linken Intervall ganz rechts. Bei der Aufteilung dieses Intervalls wäre es jetzt ideal
 * die Aufteilung würde im Beispiel knapp links von der Lösung stattfinden, denn dann wäre das resultierende
 * Intervall sehr klein. Findet sie aber nicht auf der guten Seite statt, so ist in diesem Schritt fast 
 * nichts gewonnen. In dem adaptiven Verfahren wird dann nicht aufgeteilt, sondern der Proportionalitätsfaktor 
 * geändert, so dass in der nächsten Runde hoffentlich ein bessere Aufteilung stattfindet. Dieses Verfahren
 * sollte in allen Iterationen verwendet werden. Es konvergiert fast immer sehr gut und es ist im Extremfall 
 * sicher, d.h. es erhöht den Proportionalitätsfaktor bis er über 0.25 ist und somit fast wie die Bisektion
 * arbeitet. Siehe "poscorrfactor". 
 * Der Effekt ist oft ein Wechsel zwischen 
 *      1. Aufteilung in etwa in der Mitte nahe der Löung (wie Bisektion)
 *      2. Aufteilung in ein neues (sehr kleines) Intervall, bei dem die Lösung in etwa in der Mitte liegt
 * Im Durchschnitt sind es weniger als 10 Schritte im Vergleich zu 45 Schritten bei der einfachen Bisektion.
 */

namespace CADability.Curve2D
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable()]
    public class BSpline2D : GeneralCurve2D, ISerializable, IDeserializationCallback, IDebuggerVisualizer, IExplicitPCurve2D
    {
        private GeoPoint2D[] poles; // die Kontrollpunkte, deren Anzahl bestimmt die Größe der anderen Arrays
        private double[] weights; // kann leer sein oder genausogroß wie poles (letzerer Fall ist rational)
        private double[] knots; // die Knotenwerte (für non uniform)
        private int[] multiplicities; // wie oft wird jeder Knotenwert verwendet
        // die Summe der multiplicities ist (Anzahl poles)-degree-1 (offen) bzw.
        // die Summe der multiplicities ohne die letzte ist (Anzahl poles) (geschlossen)
        private int degree; // Grad
        private bool periodic; // geschlossen. das ist nur eine Information wie der Spline entstanden ist
        // der zugrundeliegende NURBS ist immer "Clamped"
        private double startParam; // geht hier los
        private double endParam; // und endet da
        // die folgenden werden nicht gespeichert, sind nur für die HiddenLine Darstellung von Bedeutung:
        private BSpline original;
        private Plane projectionPlane;
        private double zMin, zMax;
        // die folgenden Daten sind für die schnelle Berechnung und können null sein
        private Nurbs<GeoPoint2D, GeoPoint2DPole> nubs; // nicht rational, nur einer der beiden wird gesetzt
        private Nurbs<GeoPoint2DH, GeoPoint2DHPole> nurbs; // rational
        //private GeoPoint2D[] interpol; // gewisse Stützpunkte für jeden Knoten und ggf Zwischenpunkte (Wendepunkte, zu große Dreiecke)
        //private GeoVector2D[] interdir; // Richtungen an den Stützpunkten
        //private double[] interparam; // Parameterwerte an den Stützpunkten
        //private GeoPoint2D[] tringulation; // Dreiecks-Zwischenpunkte (einer weniger als interpol)
        private double parameterEpsilon; // ein epsilon, welches sich auf den Parameter bezieht. Abbruch für Iterationen
        private double distanceEpsilon; // ein epsilon, welches sich auf die Ausdehnung bezieht. Abbruch für Iterationen
        WeakReference<ExplicitPCurve2D> explicitPCurve2D;

        private void InvalidateCache()
        {
            base.ClearTriangulation();
            //interpol = null;
            //interdir = null;
            //interparam = null;
            //tringulation = null;
            nubs = null;
            nurbs = null;
            explicitPCurve2D = null;
            Init();
        }
        private void MakeFlat()
        {
            List<double> knotslist = new List<double>();
            int dbgnumkn = 0;
            for (int i = 0; i < knots.Length; ++i)
            {
                dbgnumkn += multiplicities[i];
                for (int j = 0; j < multiplicities[i]; ++j)
                {
                    knotslist.Add(knots[i]);
                }
            }

            //Unreachable code
            /*
            if (false) // periodic ist nur eine Info, der Spline selbst ist doch immer geclampet, oder?
            // if (periodic)
            {
                double dknot = knots[knots.Length - 1] - knots[0];
                // neue Idee: 
                // 1. es werden immer "degree" poles hinten angehängt
                // 2. vor dem 2. Knoten müssen immer degree+1 Knoten sein ( siehe STEP/piece0: dort sind alle Knoten 4-fach
                //    bei degree=5 und FindSpan muss immer eine Stelle finden, an der es gerade wechselt)
                // 3. es werden soviele Knoten hinten angehängt, dass "knotslist.Length-degree-1 ==  poles.length" gilt
                int secondknotindex = multiplicities[0]; // dort ist der erste von 0 verschiedene Knoten
                for (int i = 0; i <= degree - multiplicities[0]; ++i)
                {
                    knotslist.Insert(0, knotslist[knotslist.Count - degree - i] - dknot);
                    ++secondknotindex;
                }
                while (knotslist.Count - degree - 1 < poles.Length + degree)
                {
                    knotslist.Add(knotslist[secondknotindex] + dknot);
                    ++secondknotindex;
                }
            }
            */

            if (weights == null)
            {
                GeoPoint2D[] npoles;
                
                //Unreachable code
                /* if (false)
                //if (periodic)
                {
                    npoles = new GeoPoint2D[poles.Length + degree];
                    for (int i = 0; i < poles.Length; ++i)
                    {
                        npoles[i] = poles[i];
                    }
                    for (int i = 0; i < degree; ++i)
                    {
                        npoles[poles.Length + i] = poles[i];
                    }
                    // nach neuer idee, war vorher so:
                    //for (int i = 0; i < 2 * degree - 2; ++i)
                    //{
                    //    npoles[poles.Length + i] = poles[i];
                    //}
                    //if (knotslist.Count - degree - 1 < npoles.Length)
                    //{   // das kommt bei STEP/piece0 z.B. vor, kommt ja nur über OpenCascade
                    //    knotslist.Insert(0, knotslist[0]);
                    //}
                }*/
                //else
                //{

                    npoles = new GeoPoint2D[poles.Length];
                    for (int i = 0; i < poles.Length; ++i)
                    {
                        npoles[i] = poles[i];
                    }
                //}
                this.nubs = new Nurbs<GeoPoint2D, GeoPoint2DPole>(degree, npoles, knotslist.ToArray());
                nubs.InitDeriv1();
            }
            else
            {
                GeoPoint2DH[] npoles;
                
                //Unreachable code
                /*if (false)
                // if (periodic)
                {
                    npoles = new GeoPoint2DH[poles.Length + degree];
                    for (int i = 0; i < poles.Length; ++i)
                    {
                        npoles[i] = new GeoPoint2DH(poles[i], weights[i]);
                    }
                    for (int i = 0; i < degree; ++i)
                    {
                        npoles[poles.Length + i] = new GeoPoint2DH(poles[i], weights[i]);
                    }
                    // s.o.
                }
                else
                {*/
                    npoles = new GeoPoint2DH[poles.Length];
                    for (int i = 0; i < poles.Length; ++i)
                    {
                        npoles[i] = new GeoPoint2DH(poles[i], weights[i]);
                    }
                //}
                this.nurbs = new Nurbs<GeoPoint2DH, GeoPoint2DHPole>(degree, npoles, knotslist.ToArray());
                nurbs.InitDeriv1();
            }

            parameterEpsilon = Math.Max(Math.Abs(startParam), Math.Abs(endParam)) * 1e-14;
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < poles.Length; ++i)
            {
                ext.MinMax(poles[i]);
            }
            distanceEpsilon = (ext.Width + ext.Height) * 1e-14;
        }
        //        private void MakeTriangulation()
        //        {
        //            List<GeoPoint2D> tristart = new List<GeoPoint2D>();
        //            List<GeoPoint2D> triinter = new List<GeoPoint2D>();
        //            List<GeoVector2D> tridir = new List<GeoVector2D>();
        //            List<double> tripar = new List<double>();
        //            GeoPoint2D p1;
        //            GeoVector2D v1, v12;
        //            double[] tknots = knots;
        //            if (knots.Length == 2)
        //            {   // es kommen Splines vor, die nur zwei Knoten haben und geschlossene Kreise sind
        //                // (degree z.B. 14). Dann wird einfach ein Knoten dazwischengehängt
        //                tknots = new double[3];
        //                tknots[0] = knots[0];
        //                tknots[1] = (knots[0] + knots[1]) / 2.0;
        //                tknots[2] = knots[1];
        //            }
        //            PointDirAt2(tknots[0], out p1, out v1, out v12);
        //            for (int i = 0; i < tknots.Length - 1; ++i)
        //            {   // iterieren über die echten Knoten, die doppelten interessieren nicht.
        //                // Wenn zwischen zwei Knoten ein Wendepunkt ist, wird der als Stützpunkt mitgenommen
        //                // Wir gehen hier davon aus dass der ganze Bereich des knotenvektors verwendet wird
        //                // startParam und endParam werden nicht berücksichtigt. (Überhaupt: ist startParam nicht immer
        //                // knots[0] und endParam knots[letzer]?)
        //                // was noch fehlt: Kurven, die einen Knick haben sollten am Knick ein Nulldreieck einfügen
        //                // dazu müsste allerdings die Nurbs Methode in der Lage sein die Ableitungen unmittelbar VOR einem Knoten
        //                // zu berechnen. Wenn ein Knoten als Parameter eingegeben wird, dann kommt immer die Ableitung nach dem
        //                // Knoten heraus
        //                double sp = tknots[i];
        //                double ep = tknots[i + 1];
        //                GeoPoint2D p2;
        //                GeoVector2D v2, v22;
        //                PointDirAt2(ep, out p2, out v2, out v22);
        //                double ip;
        //                if (FindInflectionPoint(sp, p1, v1, v12, ep, p2, v2, v22, out ip))
        //                {
        //                    PointDirAt(sp, out p1, out v1);
        //                    PointDirAt(ip, out p2, out v2);
        //                    // Nulldreiecke machen Probleme
        //                    // und Splines mit hoherm Grad, die aber Linien sind, produzieren viele "InflectionPoints"
        //                    if (ip - sp > this.parameterEpsilon)
        //                    {
        //                        MakeTriangle(sp, ip, p1, v1, p2, v2, tristart, triinter, tridir, tripar, 0);
        //                        p1 = p2;
        //                        v1 = v2;
        //                    }
        //                    if (ep - ip > this.parameterEpsilon)
        //                    {
        //                        PointDirAt(ep, out p2, out v2);
        //                        MakeTriangle(ip, ep, p1, v1, p2, v2, tristart, triinter, tridir, tripar, 0);
        //                    }
        //                }
        //                else
        //                {
        //                    MakeTriangle(sp, ep, p1, v1, p2, v2, tristart, triinter, tridir, tripar, 0);
        //                }
        //                // Daten für die nächste Runde übernehmen
        //                p1 = p2;
        //                v1 = v2;
        //                v12 = v22;
        //            }
        //            // den letzten Punkt noch zufügen
        //            GeoPoint2D pe;
        //            GeoVector2D ve;
        //            PointDirAt(knots[knots.Length - 1], out pe, out ve);
        //            tristart.Add(pe);
        //            tridir.Add(ve);
        //            tripar.Add(knots[knots.Length - 1]);

        //            interpol = tristart.ToArray();
        //            interdir = tridir.ToArray();
        //            interparam = tripar.ToArray();
        //            tringulation = triinter.ToArray();
        //#if DEBUG
        //            for (int i = 0; i < interpol.Length; i++)
        //            {
        //                if (interpol[i].IsNan)
        //                {

        //                }
        //            }
        //            if (interpol.Length > maxTriangleCount)
        //            {
        //                maxTriangleCount = interpol.Length;
        //                //System.Diagnostics.Trace.WriteLine("MaxTraingleCount: " + maxTriangleCount.ToString());
        //            }
        //#endif
        //        }

        private bool FindInflectionPoint(double su, GeoPoint2D pl, GeoVector2D dir1l, GeoVector2D dir2l, double eu, GeoPoint2D pr, GeoVector2D dir1r, GeoVector2D dir2r, out double par)
        {
            GeoPoint2D pm;
            GeoVector2D dir1m, dir2m;

            double left = dir1l.x * dir2l.y - dir1l.y * dir2l.x;
            double right = dir1r.x * dir2r.y - dir1r.y * dir2r.x;
            bool bleft = left < 0;
            bool bright = right < 0;

            // Vorzeichenwechsel (der z-Komponente) beim Kreuzprodukt von 1. und 2. Ableitung ist Wendepunkt
            if (bleft != bright)
            {
                double mu = su;
                double poscorrfactor = 2.0;
                int cnt = 0;
                do
                {
                    double pos;
                    pos = left / (left - right);
                    if (pos < 0.25) pos *= poscorrfactor;
                    if (pos > 0.75) pos = 1 - (1 - pos) * poscorrfactor;
                    double m = su + pos * (eu - su);
                    if (m == mu) break; // es geht nicht besser
                    mu = m;
                    PointDirAt2(mu, out pm, out dir1m, out dir2m);
                    double middle = dir1m.x * dir2m.y - dir1m.y * dir2m.x;
                    bool bmiddle = middle < 0.0;
                    if (bmiddle != bright)
                    {   // linke Seite ersetzen
                        if (pos < 0.25)
                        {   // der Teilfaktor pos ist klein (oft sehr klein) und trotzdem soll links abgeschnitten werden
                            // da wäre es besser einen größeren Teilfaktor zu nehmen und rechts abzuschneiden
                            poscorrfactor *= 2;
                        }
                        else
                        {
                            su = mu;
                            left = middle;
                            poscorrfactor = 2.0;
                        }
                    }
                    else
                    {
                        if (pos > 0.75)
                        {
                            poscorrfactor *= 2;
                        }
                        else
                        {
                            eu = mu;
                            right = middle;
                            poscorrfactor = 2.0;
                        }
                    }
                    ++cnt;
                } while ((Math.Abs(eu - su) > parameterEpsilon) && (cnt < 5));
                par = mu;
                return true;
            }
            par = 0.0;
            return false;
        }
        private void MakeTriangle(double startPar, double endPar, GeoPoint2D p1, GeoVector2D v1, GeoPoint2D p2, GeoVector2D v2, List<GeoPoint2D> tristart, List<GeoPoint2D> triinter, List<GeoVector2D> tridir, List<double> tripar, int deepth)
        {
            if (deepth > 3)
            {   // da ist was schief, das sollte nicht vorkommen und muss genauer untersucht werden
                if (tristart.Count == 0 || p1 != tristart[tristart.Count - 1])
                {
                    tristart.Add(p1);
                    tridir.Add(v1);
                    tripar.Add(startPar);
                    triinter.Add(new GeoPoint2D(p1, p2));
                }
                else
                {   // nur für Breakpoint
                }
                return;
            }
            GeoPoint2D ints;
            if (Geometry.IntersectLL(p1, v1, p2, v2, out ints))
            {
                double pos1, pos2;
                if (Math.Abs(v1.x) > Math.Abs(v1.y)) pos1 = (ints.x - p1.x) / v1.x;
                else pos1 = (ints.y - p1.y) / v1.y;
                if (Math.Abs(v2.x) > Math.Abs(v2.y)) pos2 = (ints.x - p2.x) / v2.x;
                else pos2 = (ints.y - p2.y) / v2.y;
                if (pos1 > 0.0 && pos2 < 0.0)
                {   // gutes Dreieck, nur ggf. zu spitz
                    double c = v1 * v2; // cos des Winkels
                    if (c <= 0)
                    {   // über 90°, also noch einen Zwischenpunkt einfügen, damit flachere Dreiecke entstehen
                        // man kann den Winkel auch noch enger setzen, damit flachere Dreiecke entstehen
                        double middlePar = (startPar + endPar) / 2.0;
                        GeoPoint2D pm;
                        GeoVector2D vm;
                        PointDirAt(middlePar, out pm, out vm);
                        MakeTriangle(startPar, middlePar, p1, v1, pm, vm, tristart, triinter, tridir, tripar, deepth + 1);
                        MakeTriangle(middlePar, endPar, pm, vm, p2, v2, tristart, triinter, tridir, tripar, deepth + 1);
                        return;
                    }
                    else
                    {   // gutes flaches Dreieck
                        if (tristart.Count == 0 || p1 != tristart[tristart.Count - 1])
                        {
                            tristart.Add(p1);
                            tridir.Add(v1);
                            tripar.Add(startPar);
                            triinter.Add(ints);
                        }
                        else
                        {   // nur für Breakpoint
                        }
                        return;
                    }
                }
                else
                {
                    // es hat nicht geklappt: guten Zwischenpunkt suchen
                    // das sollte seit der Zufügung von Wendepunkten nicht mehr vorkommen
                    // führt zu StackOverflow
                    //double middlePar;
                    //GeoPoint2D pm;
                    //GeoVector2D vm;
                    //FindGoodTrianglePoint(startPar, endPar, p1, v1, p2, v2, out middlePar, out pm, out vm);
                    //MakeTriangle(startPar, middlePar, p1, v1, pm, vm, tristart, triinter, tridir, tripar, deepth + 1);
                    //MakeTriangle(middlePar, endPar, pm, vm, p2, v2, tristart, triinter, tridir, tripar, deepth + 1);
                    //return;
                }

            }
            // es hat nicht geklappt: degeneriert?
            if (Precision.SameDirection(v1, v2, false) && Precision.SameDirection(p2 - p1, v1, false))
            {   // zur Linie degeneriertes Dreieck einfach zufügen
                if (tristart.Count == 0 || p1 != tristart[tristart.Count - 1])
                {
                    tristart.Add(p1);
                    tridir.Add(v1);
                    tripar.Add(startPar);
                    triinter.Add(new GeoPoint2D(p1, p2));
                }
                else
                {   // nur für Breakpoint
                }
                return;
            }
            else
            {   // kein Schnitt, kommt vor bei Halbkreisen, die mit nur zwei knoten, aber hohem degree gemacht sind
                double middlePar = (startPar + endPar) / 2.0;
                GeoPoint2D pm;
                GeoVector2D vm;
                PointDirAt(middlePar, out pm, out vm);
                MakeTriangle(startPar, middlePar, p1, v1, pm, vm, tristart, triinter, tridir, tripar, deepth + 1);
                MakeTriangle(middlePar, endPar, pm, vm, p2, v2, tristart, triinter, tridir, tripar, deepth + 1);
            }
        }
        private void FindGoodTrianglePoint(double startPar, double endPar, GeoPoint2D p1, GeoVector2D v1, GeoPoint2D p2, GeoVector2D v2, out double middlePar, out GeoPoint2D pm, out GeoVector2D vm)
        {
            middlePar = (startPar + endPar) / 2.0;
            PointDirAt(middlePar, out pm, out vm);
            // Wendepunkte werden so nicht gefunden!!!            
            GeoPoint2D ints;
            bool firstok = false;
            if (Geometry.IntersectLL(p1, v1, pm, vm, out ints))
            {
                double pos1, pos2;
                if (Math.Abs(v1.x) > Math.Abs(v1.y)) pos1 = (ints.x - p1.x) / v1.x;
                else pos1 = (ints.y - p1.y) / v1.y;
                if (Math.Abs(v2.x) > Math.Abs(v2.y)) pos2 = (ints.x - p2.x) / v2.x;
                else pos2 = (ints.y - p2.y) / v2.y;
                if (pos1 > 0.0 && pos2 < 0.0)
                {   // erstes Dreieck gut, zweites testen:
                    firstok = true;
                    if (Geometry.IntersectLL(pm, vm, p2, v2, out ints))
                    {
                        if (Math.Abs(v1.x) > Math.Abs(v1.y)) pos1 = (ints.x - p1.x) / v1.x;
                        else pos1 = (ints.y - p1.y) / v1.y;
                        if (Math.Abs(v2.x) > Math.Abs(v2.y)) pos2 = (ints.x - p2.x) / v2.x;
                        else pos2 = (ints.y - p2.y) / v2.y;
                        if (pos1 > 0.0 && pos2 < 0.0)
                        {   // zweites Dreieck gut, fertig
                            return; // die Werte sind ja gesetzt
                        }
                    }
                }
            }
            if (Geometry.Dist(p1, p2) < Precision.eps * 1000 || (endPar - startPar < parameterEpsilon))
            {   // auch fertig, da zu klein
                return;
            }
            // das schlechte Dreieck wird also genauer untersucht, das andere wird ignoriert
            // die out Parameter sind ja value types, werden also beim Aufruf kopiert.
            if (firstok)
                FindGoodTrianglePoint(middlePar, endPar, pm, vm, p2, v2, out middlePar, out pm, out vm);
            else
                FindGoodTrianglePoint(startPar, middlePar, p1, v1, pm, vm, out middlePar, out pm, out vm);
        }
        private void Init()
        {
            try
            {
                RepairHighMultiplicities();
                MakeFlat(); // es gibt immer auch die flachen
                if (nubs != null) nubs.InitDeriv2();
                else nurbs.InitDeriv2();
                // MakeTriangulation();
                if (nubs != null) nubs.ClearDeriv2();
                else nurbs.ClearDeriv2();
            }
            catch (System.ArithmeticException)
            {
                // MakeSimplePolyLine();
                // Notlösung 
                BSpline2D b2d = new BSpline2D(this.poles, 1, false); // Polyline durch die Pole
                this.Copy(b2d);
            }
        }

        private void RepairHighMultiplicities()
        {
            if (multiplicities[0] > degree + 1)
            {
                int toRemove = multiplicities[0] - degree - 1;
                GeoPoint2D[] newpoles = new GeoPoint2D[poles.Length - toRemove];
                multiplicities[0] = degree + 1;
                Array.Copy(poles, toRemove, newpoles, 0, newpoles.Length);
                poles = newpoles;
            }
            if (multiplicities[multiplicities.Length - 1] > degree + 1)
            {
                int toRemove = multiplicities[multiplicities.Length - 1] - degree - 1;
                GeoPoint2D[] newpoles = new GeoPoint2D[poles.Length - toRemove];
                multiplicities[multiplicities.Length - 1] = degree + 1;
                Array.Copy(poles, newpoles, newpoles.Length);
                poles = newpoles;
            }
        }

        /// <summary>
        /// Constructs a BSpline2D (NURBS) from its main data
        /// </summary>
        /// <param name="poles">the poles</param>
        /// <param name="weights">the weigts or null if not rational</param>
        /// <param name="knots">the knot vector</param>
        /// <param name="multiplicities">the multiplicities vector for the knot vector (same size)</param>
        /// <param name="degree">the degree</param>
        /// <param name="periodic">true for periodic (closed) false otherwise</param>
        /// <param name="startParam">startparameter</param>
        /// <param name="endParam">endparameter</param>
        public BSpline2D(GeoPoint2D[] poles, double[] weights, double[] knots, int[] multiplicities, int degree, bool periodic, double startParam, double endParam)
        {
            this.poles = (GeoPoint2D[])poles.Clone();
            if (weights == null)
            {
                this.weights = new double[poles.Length];
                for (int i = 0; i < this.weights.Length; ++i)
                {
                    this.weights[i] = 1.0;
                }
            }
            else
            {
                this.weights = (double[])weights.Clone();
            }
            this.knots = (double[])knots.Clone();
            for (int i = 1; i < knots.Length; ++i)
            {
                if (knots[i] < knots[i - 1]) throw new ApplicationException("wrong order of knots");
            }
            this.multiplicities = (int[])multiplicities.Clone();
            // es kommen manchmal Splines vor, die fast identische knoten haben. OpenCascade kann damit nicht
            // deshalb hier der test:
            double eps = (this.knots[this.knots.Length - 1] - this.knots[0]) * 1e-10;
            if (this.knots[1] - this.knots[0] < eps)
            {
                this.knots = new double[knots.Length - 1];
                Array.Copy(knots, 1, this.knots, 0, knots.Length - 1);
                this.knots[0] = knots[0];
                this.multiplicities = new int[multiplicities.Length - 1];
                Array.Copy(multiplicities, 1, this.multiplicities, 0, multiplicities.Length - 1);
                this.multiplicities[0] = multiplicities[0] + multiplicities[1];
            }
            if (this.knots[this.knots.Length - 1] - this.knots[this.knots.Length - 2] < eps && (this.poles[this.poles.Length - 1] | this.poles[this.poles.Length - 2]) < Precision.eps)
            {
                double[] newknots = new double[knots.Length - 1];
                Array.Copy(knots, 0, newknots, 0, knots.Length - 1);
                newknots[newknots.Length - 1] = knots[knots.Length - 1];
                int[] newmultiplicities = new int[multiplicities.Length - 1];
                Array.Copy(multiplicities, 0, newmultiplicities, 0, multiplicities.Length - 1);
                GeoPoint2D[] newPoles = new GeoPoint2D[this.poles.Length - 1];
                double[] newWeights = new double[this.weights.Length - 1];
                Array.Copy(this.poles, 0, newPoles, 0, this.poles.Length - 1);
                Array.Copy(this.weights, 0, newWeights, 0, this.weights.Length - 1);
                newPoles[newPoles.Length - 1] = this.poles[this.poles.Length - 1];
                newWeights[newWeights.Length - 1] = this.weights[this.weights.Length - 1];
                newmultiplicities[newmultiplicities.Length - 1] = multiplicities[multiplicities.Length - 1] + multiplicities[multiplicities.Length - 2] - 1;
                this.multiplicities = newmultiplicities;
                this.knots = newknots;
                this.poles = newPoles;
                this.weights = newWeights;
            }

            this.degree = degree;
            this.periodic = periodic;
            double sp, ep;
            if (startParam < endParam)
            {
                sp = startParam;
                ep = endParam;
            }
            else
            {
                sp = endParam;
                ep = startParam;
            }
            // den Knotenraum normieren, so dass er bei 0.0 anfängt
            // da ohnehin nur die differenzen zählen ist das ok und für opencascade besser
            for (int i = 0; i < this.knots.Length; ++i)
            {
                this.knots[i] -= sp;
            }
            // zunächst die natürlichen Grenzen nehemen
            this.startParam = this.knots[0];
            this.endParam = this.knots[this.knots.Length - 1];
            ep -= sp;
            sp = 0.0;
            if (periodic)
            {
                InitPeriodic();
                periodic = false;
            }
            try
            {
                Init();
            }
            catch (NurbsException)
            {
            }
            // sollte durch Start/Endparameter nur ein Teilbereich des Splines gelten, dann
            // wird er abgeschnitten, da das sonst an manchen Stellen zu problemen führen kann
            if (periodic)
            {
                if (ClampPeriodic(sp, ep))
                {
                    Init();
                }
            }
            else if (sp - this.startParam > parameterEpsilon || this.endParam - ep > parameterEpsilon)
            {
                if (Clamp(sp, ep))
                {
                    Init();
                }
            }
            else
            {
                this.startParam = sp;
                this.endParam = ep;
            }
        }

        private void InitPeriodic()
        {
            // kopiere einmal Poles, knots und weights vornedran und einmal hintendran
            // erweitere startParam und endParam, anschließend wird ja getrimmt
            GeoPoint2D[] ppoles = new GeoPoint2D[poles.Length * 3];
            poles.CopyTo(ppoles, 0);
            poles.CopyTo(ppoles, poles.Length);
            poles.CopyTo(ppoles, poles.Length * 2);
            double[] pweights = new double[weights.Length * 3];
            weights.CopyTo(pweights, 0);
            weights.CopyTo(pweights, weights.Length);
            weights.CopyTo(pweights, weights.Length * 2);
            List<double> pknots = new List<double>(knots.Length * 3);
            pknots.AddRange(knots); // damit es anfänglich gefüllt ist
            pknots.AddRange(knots);
            pknots.AddRange(knots);
            double period = knots[knots.Length - 1] - knots[0];
            for (int i = 0; i < knots.Length; i++)
            {
                pknots[i] -= period;
                pknots[i + knots.Length * 2] += period;
            }
            List<int> pmultiplicities = new List<int>(multiplicities.Length * 3);
            pmultiplicities.AddRange(multiplicities);
            pmultiplicities.AddRange(multiplicities);
            pmultiplicities.AddRange(multiplicities);
            pmultiplicities[0] -= (degree - 1);// das kann naoch auf multiplicities.Length-1 verteilt werden
            pmultiplicities[multiplicities.Length] -= (degree - 1);
            pmultiplicities[multiplicities.Length * 2] -= (degree - 1);
            if (pknots[knots.Length * 2 - 1] == pknots[knots.Length * 2])
            {
                pknots.RemoveAt(knots.Length * 2);
                pmultiplicities[knots.Length * 2 - 1] += pmultiplicities[knots.Length * 2];
                pmultiplicities.RemoveAt(knots.Length * 2);
            }
            if (pknots[knots.Length - 1] == pknots[knots.Length])
            {
                pknots.RemoveAt(knots.Length);
                pmultiplicities[knots.Length - 1] += pmultiplicities[knots.Length];
                pmultiplicities.RemoveAt(knots.Length);
            }
            int numflat = 0;
            for (int i = 0; i < pmultiplicities.Count; i++)
            {
                numflat += pmultiplicities[i];
            }
            numflat -= ppoles.Length;
            numflat -= degree + 1;
            while (numflat > 0)
            {
                --numflat;
                if (pmultiplicities[pmultiplicities.Count - 1] > pmultiplicities[0]) --pmultiplicities[pmultiplicities.Count - 1];
                else --pmultiplicities[0];
            }
            while (numflat < 0)
            {
                ++numflat;
                if (pmultiplicities[pmultiplicities.Count - 1] < pmultiplicities[0]) ++pmultiplicities[pmultiplicities.Count - 1];
                else ++pmultiplicities[0];
            }
            this.knots = pknots.ToArray();
            this.weights = pweights;
            this.poles = ppoles;
            this.multiplicities = pmultiplicities.ToArray();
            this.startParam = this.knots[0];
            this.endParam = this.knots[this.knots.Length - 1];
        }
        public static BSpline2D MakeCircle(GeoPoint2D center, double radius)
        {
            // mit dem exakten Kreis gibts Knicke, weil der nur quadratisch ist
            GeoPoint2D[] poles = new GeoPoint2D[9];
            poles[0] = center + new GeoVector2D(radius, 0.0);
            poles[1] = center + new GeoVector2D(radius, radius);
            poles[2] = center + new GeoVector2D(0.0, radius);
            poles[3] = center + new GeoVector2D(-radius, radius);
            poles[4] = center + new GeoVector2D(-radius, 0.0);
            poles[5] = center + new GeoVector2D(-radius, -radius);
            poles[6] = center + new GeoVector2D(0.0, -radius);
            poles[7] = center + new GeoVector2D(radius, -radius);
            poles[8] = center + new GeoVector2D(radius, 0.0);
            double[] weights = new double[9];
            double s2 = Math.Sqrt(2.0) / 2.0;
            weights[0] = 1.0;
            weights[1] = s2;
            weights[2] = 1.0;
            weights[3] = s2;
            weights[4] = 1.0;
            weights[5] = s2;
            weights[6] = 1.0;
            weights[7] = s2;
            weights[8] = 1.0;
            double[] knots = new double[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
            knots = new double[] { 0.0, 1.0, 2.0, 3.0, 4.0 };
            int[] mults = new int[] { 3, 2, 2, 2, 3 };
            return new BSpline2D(poles, weights, knots, mults, 2, false, 0.0, 4.0);
        }
        public static BSpline2D MakeSpiral(GeoPoint2D center, double offset, int turns)
        {
            // mit dem exakten Kreis gibts Knicke, weil der nur quadratisch ist
            //GeoPoint2D[] poles = new GeoPoint2D[9];
            //poles[0] = center + new GeoVector2D(0.0 * offset / 8, 0.0);
            //poles[1] = center + new GeoVector2D(1 * offset / 8, 1 * offset / 8);
            //poles[2] = center + new GeoVector2D(0.0, 2 * offset / 8);
            //poles[3] = center + new GeoVector2D(-3 * offset / 8, 3 * offset / 8);
            //poles[4] = center + new GeoVector2D(-4 * offset / 8, 0.0);
            //poles[5] = center + new GeoVector2D(-5 * offset / 8, -5 * offset / 8);
            //poles[6] = center + new GeoVector2D(0.0, -6 * offset / 8);
            //poles[7] = center + new GeoVector2D(7 * offset / 8, -7 * offset / 8);
            //poles[8] = center + new GeoVector2D(8 * offset / 8, 0.0);
            //double[] weights = new double[9];
            //double s2 = Math.Sqrt(2.0) / 2.0;
            //weights[0] = 1.0;
            //weights[1] = s2;
            //weights[2] = 1.0;
            //weights[3] = s2;
            //weights[4] = 1.0;
            //weights[5] = s2;
            //weights[6] = 1.0;
            //weights[7] = s2;
            //weights[8] = 1.0;
            //double[] knots = new double[] { 0.0, 0.25, 0.5, 0.75, 1.0 };
            //int[] mults = new int[] { 3, 2, 2, 2, 3 };
            //return new BSpline2D(poles, weights, knots, mults, 2, false, 0.0, 1.0);
            GeoPoint2D[] throughpoints = new GeoPoint2D[8 * turns];
            double step = Math.PI / 4.0;
            offset = offset / 8.0;
            for (int i = 0; i < throughpoints.Length; ++i)
            {
                throughpoints[i] = new GeoPoint2D(center.x + i * offset * Math.Cos(i * step), center.y + i * offset * Math.Sin(i * step));
            }
            return new BSpline2D(throughpoints, 3, false);
        }
        internal static BSpline2D MakeSpiral(GeoPoint2D center, double offset, int turns, int firstturn)
        {
            GeoPoint2D[] throughpoints = new GeoPoint2D[8 * (turns - firstturn)];
            double step = Math.PI / 4.0;
            offset = offset / 8.0;
            for (int i = 0; i < throughpoints.Length; ++i)
            {
                throughpoints[i] = new GeoPoint2D(center.x + (i + firstturn * 8) * offset * Math.Cos(i * step), center.y + (i + firstturn * 8) * offset * Math.Sin(i * step));
            }
            return new BSpline2D(throughpoints, 3, false);
        }
        /// <summary>
        /// Creates a (segment of a) hyperbola defined by its endpoints (startPoint, endPoint), the intersection-point of the tangents at the endpoints and a point
        /// located on the hyperbola where the hyperbola intersects with the line [midpoint(startPoint, endPoint), tangentIntersectionPoint]
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="endPoint"></param>
        /// <param name="midPoint"></param>
        /// <param name="tangentIntersectionPoint"></param>
        /// <returns></returns>
        public static BSpline2D MakeHyperbola(GeoPoint2D startPoint, GeoPoint2D endPoint, GeoPoint2D midPoint, GeoPoint2D tangentIntersectionPoint)
        {   // see: The NURBS Book, p294
            GeoPoint2D[] poles = new GeoPoint2D[] { startPoint, tangentIntersectionPoint, endPoint };
            GeoPoint2D M = new GeoPoint2D(startPoint, endPoint);
            double d = (midPoint | M) / (tangentIntersectionPoint | M);
            if (d < 1)
            {
                double[] weights = new double[] { 1, d / (1 - d), 1 };
                return new BSpline2D(poles, weights, new double[] { 0, 1 }, new int[] { 3, 3 }, 2, false, 0, 1);
            }
            else return null; // this are no hyperbola points
        }
        private bool ClampPeriodic(double startPar, double endPar)
        {
            if (nubs != null)
            {
                Nurbs<GeoPoint2D, GeoPoint2DPole> newnubs = nubs.Trim(startPar, endPar);
                List<double> newknots = new List<double>();
                List<int> newmults = new List<int>();
                newknots.Add(newnubs.UKnots[0]);
                newmults.Add(1);
                for (int i = 1; i < newnubs.UKnots.Length; ++i)
                {
                    if (newnubs.UKnots[i] == newknots[newknots.Count - 1])
                    {
                        ++(newmults[newmults.Count - 1]);
                    }
                    else
                    {
                        newknots.Add(newnubs.UKnots[i]);
                        newmults.Add(1);
                    }
                }
                double[] newweights = new double[newnubs.Poles.Length];
                for (int i = 0; i < newweights.Length; ++i)
                {
                    newweights[i] = 1.0;
                }
                this.poles = (GeoPoint2D[])newnubs.Poles.Clone();
                this.knots = newknots.ToArray();
                this.multiplicities = newmults.ToArray();
                this.weights = newweights;
                this.startParam = startPar;
                this.endParam = endPar;
                this.periodic = false;
                InvalidateCache();
                return true;
            }
            else
            {
                Nurbs<GeoPoint2DH, GeoPoint2DHPole> newnurbs = nurbs.Trim(startPar, endPar);
                List<double> newknots = new List<double>();
                List<int> newmults = new List<int>();
                newknots.Add(newnurbs.UKnots[0]);
                newmults.Add(1);
                for (int i = 1; i < newnurbs.UKnots.Length; ++i)
                {
                    if (newnurbs.UKnots[i] == newknots[newknots.Count - 1])
                    {
                        ++(newmults[newmults.Count - 1]);
                    }
                    else
                    {
                        newknots.Add(newnurbs.UKnots[i]);
                        newmults.Add(1);
                    }
                }
                double[] newweights = new double[newnurbs.Poles.Length];
                GeoPoint2D[] newpoles = new GeoPoint2D[newnurbs.Poles.Length];
                for (int i = 0; i < newweights.Length; ++i)
                {
                    newweights[i] = newnurbs.Poles[i].w;
                    newpoles[i] = (GeoPoint2D)newnurbs.Poles[i];
                }
                this.poles = newpoles;
                this.knots = newknots.ToArray();
                this.multiplicities = newmults.ToArray();
                this.weights = newweights;
                this.startParam = startPar;
                this.endParam = endPar;
                this.periodic = false;
                InvalidateCache();
                return true;
            }
        }
        public static void AdjustPeriodic(GeoPoint2D[] points, double xPeriod, double yPeriod)
        {
            if (xPeriod > 0.0)
            {
                for (int i = 1; i < points.Length; ++i)
                {
                    if (i > 0 && Math.Abs(points[i].x - points[i - 1].x) > xPeriod / 2.0)
                    {
                        if (points[i].x - points[i - 1].x > 0)
                        {
                            points[i].x -= xPeriod;
                        }
                        else
                        {
                            points[i].x += xPeriod;
                        }
                    }
                }
            }
            if (yPeriod > 0.0)
            {
                for (int i = 1; i < points.Length; ++i)
                {
                    if (i > 0 && Math.Abs(points[i].y - points[i - 1].y) > yPeriod / 2.0)
                    {
                        if (points[i].y - points[i - 1].y > 0)
                        {
                            points[i].y -= yPeriod;
                        }
                        else
                        {
                            points[i].y += yPeriod;
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Constructs a BSpline2D (NURBS) by a set of points, that will be interpolated.
        /// </summary>
        /// <param name="throughpoints">the points to be interpolated</param>
        /// <param name="degree">the degree of the BSpline2D</param>
        /// <param name="periodic">true for periodic (closed) false otherwise</param>
        public BSpline2D(GeoPoint2D[] throughpoints, int degree, bool periodic)
        {
            degree = Math.Min(degree, throughpoints.Length - 1); // bei 2 Punkten nur 1. Grad, also Linie, u.s.w
            double[] throughpointsparam;
            nubs = new Nurbs<GeoPoint2D, GeoPoint2DPole>(degree, throughpoints, periodic, out throughpointsparam);
            poles = nubs.Poles;
            double[] flatknots = nubs.UKnots;
            List<double> hknots = new List<double>();
            List<int> hmult = new List<int>();
            for (int i = 0; i < flatknots.Length; ++i)
            {
                if (i == 0)
                {
                    hknots.Add(flatknots[i]);
                    hmult.Add(1);
                }
                else
                {
                    if (flatknots[i] == hknots[hknots.Count - 1])
                    {
                        ++hmult[hmult.Count - 1];
                    }
                    else
                    {
                        hknots.Add(flatknots[i]);
                        hmult.Add(1);
                    }
                }
            }
            // weights wieder rausschmeißen, aber z.Z. gibt es noch Probleme, wenn null
            weights = new double[poles.Length];
            for (int i = 0; i < weights.Length; ++i)
            {
                weights[i] = 1.0;
            }
            knots = hknots.ToArray();
            multiplicities = hmult.ToArray();
            this.degree = degree;
            this.periodic = periodic;
            this.startParam = flatknots[degree];
            this.endParam = flatknots[flatknots.Length - degree - 1];

            // statt Init(); 
            nubs.InitDeriv1();

            nubs.InitDeriv2();
            //MakeTriangulation();
            nubs.ClearDeriv2();

            parameterEpsilon = Math.Max(Math.Abs(startParam), Math.Abs(endParam)) * 1e-14;
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < poles.Length; ++i)
            {
                ext.MinMax(poles[i]);
            }
            distanceEpsilon = (ext.Width + ext.Height) * 1e-14;
        }
        public BSpline2D(GeoPoint2D[] throughpoints, GeoVector2D[] throughdirections, int degree, bool periodic)
        {
            GeoPoint2D[] dirs = new GeoPoint2D[throughdirections.Length];
            for (int i = 0; i < throughdirections.Length; ++i)
            {
                dirs[i] = new GeoPoint2D(throughdirections[i].x, throughdirections[i].y);
            }
            degree = Math.Min(degree, throughpoints.Length - 1); // bei 2 Punkten nur 1. Grad, also Linie, u.s.w
            nubs = new Nurbs<GeoPoint2D, GeoPoint2DPole>(degree, throughpoints, dirs, periodic);
            poles = nubs.Poles;
            double[] flatknots = nubs.UKnots;
            List<double> hknots = new List<double>();
            List<int> hmult = new List<int>();
            for (int i = 0; i < flatknots.Length; ++i)
            {
                if (i == 0)
                {
                    hknots.Add(flatknots[i]);
                    hmult.Add(1);
                }
                else
                {
                    if (flatknots[i] == hknots[hknots.Count - 1])
                    {
                        ++hmult[hmult.Count - 1];
                    }
                    else
                    {
                        hknots.Add(flatknots[i]);
                        hmult.Add(1);
                    }
                }
            }
            // weights wieder rausschmeißen, aber z.Z. gibt es noch Probleme, wenn null
            weights = new double[poles.Length];
            for (int i = 0; i < weights.Length; ++i)
            {
                weights[i] = 1.0;
            }
            knots = hknots.ToArray();
            multiplicities = hmult.ToArray();
            this.degree = degree;
            this.periodic = periodic;
            this.startParam = knots[0];
            this.endParam = knots[knots.Length - 1];

            // statt Init();
            nubs.InitDeriv1();

            nubs.InitDeriv2();
            //MakeTriangulation();
            nubs.ClearDeriv2();

            parameterEpsilon = Math.Max(Math.Abs(startParam), Math.Abs(endParam)) * 1e-14;
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < poles.Length; ++i)
            {
                ext.MinMax(poles[i]);
            }
            distanceEpsilon = (ext.Width + ext.Height) * 1e-14;

        }
#if DEBUG
#endif
        //internal override int GetMinParts()
        //{
        //    return knots.Length;
        //}
        /// <summary>
        /// Gets a copy of the knots defining the BSpline (NURBS)
        /// </summary>
        public double[] Knots
        {
            get
            {
                return (double[])knots.Clone();
            }
        }
        /// <summary>
        /// Gets a copy of the weights defining the BSpline (NURBS)
        /// </summary>
        public double[] Weights
        {
            get
            {
                return (double[])weights.Clone();
            }
        }
        /// <summary>
        /// Gets a copy of the multiplicities defining the BSpline (NURBS)
        /// </summary>
        public int[] Multiplicities
        {
            get
            {
                return (int[])multiplicities.Clone();
            }
        }
        /// <summary>
        /// Gets the degree of the BSpline (NURBS)
        /// </summary>
        public int Degree
        {
            get
            {
                return degree;
            }
        }
        /// <summary>
        /// Gets a copy of the poles defining the BSpline (NURBS)
        /// </summary>
        public GeoPoint2D[] Poles
        {
            get
            {
                return (GeoPoint2D[])poles.Clone();
            }
        }
        public double StartParam
        {
            get
            {
                return startParam;
            }
        }
        public double EndParam
        {
            get
            {
                return endParam;
            }
        }
        internal void SetZValues(BSpline original, Plane projectionPlane)
        {
            this.original = original;
            this.projectionPlane = projectionPlane;
            zMin = double.MaxValue;
            zMax = double.MinValue;
            // double du = 1.0/(poles.Length-1);
            // Poles bilden eine konvexe Hülle, damit so auf der sicheren Seite:
            for (int i = 0; i < original.PoleCount; i++)
            {
                GeoPoint p = projectionPlane.ToLocal(original.GetPole(i));
                if (zMin > p.z) zMin = p.z;
                if (zMax < p.z) zMax = p.z;
            }
        }
        protected override void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] parameters)
        {
            double[] tknots = knots;
            if (knots.Length == 2)
            {   // there are splines with only two knots and degree 14 which represent a full circle. We invent an additional knot
                tknots = new double[3];
                tknots[0] = knots[0];
                tknots[1] = (knots[0] + knots[1]) / 2.0;
                tknots[2] = knots[1];
            }
            points = new GeoPoint2D[tknots.Length];
            directions = new GeoVector2D[tknots.Length];
            parameters = new double[tknots.Length];
            for (int i = 0; i < tknots.Length; i++)
            {
                PointDerAt(tknots[i], out points[i], out directions[i]);
                parameters[i] = (tknots[i] - startParam) / (endParam - startParam);
            }
        }
        internal override void GetTriangulationPoints(out GeoPoint2D[] points, out double[] parameters)
        {
            double[] tknots = knots;
            if (knots.Length == 2)
            {   // there are splines with only two knots and degree 14 which represent a full circle. We invent an additional knot
                tknots = new double[3];
                tknots[0] = knots[0];
                tknots[1] = (knots[0] + knots[1]) / 2.0;
                tknots[2] = knots[1];
            }
            points = new GeoPoint2D[tknots.Length];
            parameters = new double[tknots.Length];
            for (int i = 0; i < tknots.Length; i++)
            {
                points[i] = PointAtParam(tknots[i]);
                parameters[i] = (tknots[i] - startParam) / (endParam - startParam);
            }
        }
        #region ICurve2D Members
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override ICurve2D Clone()
        {
            BSpline2D res = new BSpline2D(poles, weights, knots, multiplicities, degree, periodic, startParam, endParam);
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Copy (ICurve2D)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public override void Copy(ICurve2D toCopyFrom)
        {
            BSpline2D c = toCopyFrom as BSpline2D;
            poles = (GeoPoint2D[])c.poles.Clone();
            if (c.weights != null) weights = (double[])c.weights.Clone();
            else weights = null;
            knots = (double[])c.knots.Clone();
            multiplicities = (int[])c.multiplicities.Clone();
            degree = c.degree;
            periodic = c.periodic;
            startParam = c.startParam;
            endParam = c.endParam;
            UserData.CloneFrom(c.UserData);
            this.InvalidateCache();
            this.Init();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Reverse ()"/>
        /// </summary>
        public override void Reverse()
        {
            Array.Reverse(poles);
            if (weights != null) Array.Reverse(weights);
            if (knots != null)
            {
                double startOffset = startParam - knots[0];
                double endOffset = knots[knots.Length - 1] - endParam;
                double[] newknots = new double[knots.Length];
                newknots[0] = 0.0; // Anfang ist egal, es geht nur um die Differenzen
                for (int i = 1; i < knots.Length; ++i)
                {
                    newknots[i] = newknots[i - 1] + (knots[knots.Length - i] - knots[knots.Length - i - 1]);
                }
                knots = newknots;
                startParam = endOffset;
                endParam = knots[knots.Length - 1] - startOffset;
            }
            if (multiplicities != null) Array.Reverse(multiplicities);

            // das geht sicher effizienter, einziges problem in obigem auskommentierten Text ist
            // das Umdrehen von knots und start- und endparam.
            // hier erstmal die Lösung aus OCAS
            //CndHlp2D.BSpline2D b2d = Entity2D as CndHlp2D.BSpline2D;
            //CndHlp2D.BSpline2D rev = b2d.GetReversed();
            //BSpline2D b2drev = FromCndHlp2D(rev);
            //this.poles = b2drev.poles;
            //this.weights = b2drev.weights;
            //this.knots = b2drev.knots;
            //this.multiplicities = b2drev.multiplicities;
            //this.degree = b2drev.degree;
            //this.periodic = b2drev.periodic;
            //this.startParam = b2drev.startParam;
            //this.endParam = b2drev.endParam;

            InvalidateCache();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Move (double, double)"/>
        /// </summary>
        /// <param name="dx"></param>
        /// <param name="dy"></param>
        public override void Move(double dx, double dy)
        {
            GeoVector2D offset = new GeoVector2D(dx, dy);
            for (int i = 0; i < poles.Length; i++)
            {
                poles[i] = poles[i] + offset;
            }
            InvalidateCache();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.CloneReverse (bool)"/>
        /// </summary>
        /// <param name="reverse"></param>
        /// <returns></returns>
        public override ICurve2D CloneReverse(bool reverse)
        {
            BSpline2D res = (BSpline2D)Clone();
            if (reverse) res.Reverse();
            res.UserData.CloneFrom(this.UserData);
            return res;
        }
        public override double Sweep
        {
            get
            {	// hier wird angenommen, dass zwischen zwei Poles der Tangentenwinkel
                // sich um weniger als 180° ändert. Stimmt das?
                double sweep = 0.0;
                double d = (endParam - startParam) / poles.Length;
                GeoVector2D lastDir = DirectionAt(startParam);
                for (int i = 1; i <= poles.Length; ++i)
                {
                    GeoVector2D nextDir = this.DirectionAtParam(startParam + i * d);
                    if (!nextDir.IsNullVector())
                    {
                        sweep += new SweepAngle(lastDir, nextDir);
                        lastDir = nextDir;
                    }
                }
                return sweep;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.AddToGraphicsPath (System.Drawing.Drawing2D.GraphicsPath, bool)"/>
        /// </summary>
        /// <param name="path"></param>
        /// <param name="forward"></param>
        public override void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path, bool forward)
        {
            // Notfalls Darstellung als Polyline
            int l = poles.Length * 4 + 2;
            PointF[] ppf = new PointF[l];
            double du = (endParam - startParam) / (l - 1);
            for (int i = 0; i < l; ++i)
            {
                ppf[i] = PointAt(startParam + i * du).PointF;
            }
            if (!forward) Array.Reverse(ppf);
            path.AddLines(ppf);
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Project (Plane, Plane)"/>
        /// </summary>
        /// <param name="fromPlane"></param>
        /// <param name="toPlane"></param>
        /// <returns></returns>
        public override ICurve2D Project(Plane fromPlane, Plane toPlane)
        {
            BSpline2D res = (BSpline2D)this.Clone();
            for (int i = 0; i < res.poles.Length; ++i)
            {
                res.poles[i] = toPlane.Project(fromPlane.ToGlobal(res.poles[i]));
            }
            res.InvalidateCache();
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetModified (ModOp2D)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public override ICurve2D GetModified(ModOp2D m)
        {
            BSpline2D res = (BSpline2D)this.Clone();
            for (int i = 0; i < res.poles.Length; ++i)
            {
                res.poles[i] = m * res.poles[i];
            }
            res.InvalidateCache();
            return res;
        }
        private bool Clamp(double stp, double edp)
        {
            bool ok = TrimParam(stp, edp);
            return ok;
            //BSpline2D tmp = null;
            //if (Math.Abs(stp -knots[0])<(knots[knots.Length-1]-knots[0])*1e-8) tmp = this;
            //else
            //{
            //    ICurve2D[] cvs = SplitParam(stp);
            //    if (cvs.Length > 1)
            //    {
            //        tmp = cvs[1] as BSpline2D;
            //        edp -= stp;
            //        stp = 0.0;
            //    }
            //}
            //if (tmp != null && Math.Abs(edp - knots[knots.Length - 1]) < (knots[knots.Length - 1] - knots[0]) * 1e-8)
            //{

            //    ICurve2D[] cvs = tmp.SplitParam(edp);
            //    if (cvs.Length > 1) tmp = cvs[0] as BSpline2D;
            //}
            //if (tmp != null && tmp != this)
            //{
            //    this.poles = tmp.poles;
            //    this.knots = tmp.knots;
            //    this.multiplicities = tmp.multiplicities;
            //    this.weights = tmp.weights;
            //    this.startParam = stp;
            //    this.endParam = edp;
            //    InvalidateCache();
            //    return true;
            //}
            //return false;
            //double[] newknots;
            //double[] newweights;
            //GeoPoint2D[] newpoles;
            //int startind,endind;
            //if (nubs != null)
            //{
            //    startind = nubs.CurveKnotIns(stp, degree, out newknots, out newpoles);
            //    Nurbs<GeoPoint2D, GeoPoint2DPole> nubs1 = new Nurbs<GeoPoint2D, GeoPoint2DPole>(degree, newpoles, newknots);
            //    endind = nubs1.CurveKnotIns(edp, degree, out newknots, out newpoles);
            //    newweights = new double[newpoles.Length];
            //    for (int i = 0; i < newpoles.Length; ++i)
            //    {
            //        newweights[i] = 1.0;
            //    }
            //}
            //else
            //{
            //    GeoPoint2DH[] newpolestmp;
            //    startind = nurbs.CurveKnotIns(stp, degree, out newknots, out newpolestmp);
            //    Nurbs<GeoPoint2DH, GeoPoint2DHPole> nurbs1 = new Nurbs<GeoPoint2DH, GeoPoint2DHPole>(degree, newpolestmp, newknots);
            //    endind = nurbs1.CurveKnotIns(edp, degree, out newknots, out newpolestmp);
            //    newpoles = new GeoPoint2D[newpolestmp.Length];
            //    newweights = new double[newpolestmp.Length];
            //    for (int i = 0; i < newpoles.Length; ++i)
            //    {
            //        newpoles[i] = newpolestmp[i];
            //        newweights[i] = newpolestmp[i].w;
            //    }
            //}
            //List<double> knots1 = new List<double>();
            //List<int> mults1 = new List<int>();
            //List<GeoPoint2D> poles1 = new List<GeoPoint2D>();
            //List<double> weights1 = new List<double>();
            //knots1.Add(newknots[startind+1]);
            //mults1.Add(1);
            //for (int i = startind+2; i < endind+degree; ++i)
            //{
            //    if (newknots[i] >=stp && newknots[i] <=edp)
            //    {
            //        if (knots1[knots1.Count - 1] == newknots[i]) ++mults1[mults1.Count - 1];
            //        else
            //        {
            //            knots1.Add(newknots[i]);
            //            mults1.Add(1);
            //        }
            //    }
            //}
            //++mults1[mults1.Count - 1]; // der fehlt halt noch;
            //for (int i = 0; i < newpoles.Length; ++i)
            //{
            //    if (i >startind && i<endind)
            //    {
            //        poles1.Add(newpoles[i]);
            //        weights1.Add(newweights[i]);
            //    }
            //}
            //this.poles = poles1.ToArray();
            //this.knots = knots1.ToArray();
            //this.multiplicities = mults1.ToArray();
            //this.weights = weights1.ToArray();
            //this.startParam = stp;
            //this.endParam = edp;
            //InvalidateCache();
            //Init();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Trim (double, double)"/>
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="EndPos"></param>
        /// <returns></returns>
        public override ICurve2D Trim(double StartPos, double EndPos)
        {
            if (StartPos == EndPos) return null;
            if (EndPos < StartPos && IsClosed)
            {
                GeoPoint2D[] newPoles = new GeoPoint2D[poles.Length * 2 - 1];
                double[] newWeights = new double[weights.Length * 2 - 1];
                double[] newKnots = new double[knots.Length * 2 - 1];
                int[] newMultiplicities = new int[multiplicities.Length * 2 - 1];
                for (int i = 0; i < poles.Length; i++)
                {
                    newPoles[i] = poles[i];
                    newPoles[i + poles.Length - 1] = poles[i];
                }
                for (int i = 0; i < weights.Length; i++)
                {
                    newWeights[i] = weights[i];
                    newWeights[i + weights.Length - 1] = weights[i];
                }
                for (int i = 0; i < multiplicities.Length; i++)
                {
                    newMultiplicities[i] = multiplicities[i];
                    newMultiplicities[i + multiplicities.Length - 1] = multiplicities[i];
                }
                double dk = knots[knots.Length - 1] - knots[0];
                for (int i = 0; i < knots.Length; i++)
                {
                    newKnots[i] = knots[i];
                    newKnots[i + knots.Length - 1] = knots[i] + dk;
                }
                BSpline2D tmp = new BSpline2D(newPoles, newWeights, newKnots, newMultiplicities, degree, true, newKnots[0], newKnots[newKnots.Length - 1]);
                return tmp.Trim(StartPos / 2.0, EndPos / 2.0 + 0.5);
            }
            try
            {
                double p1 = startParam + StartPos * (endParam - startParam);
                double p2 = startParam + EndPos * (endParam - startParam);
                double dk = (knots[knots.Length - 1] - knots[0]) * 1e-8;
                for (int i = 0; i < knots.Length; i++) // auf Knoten einrasten, wenn sehr nahe dran. Zu enge Knoten machen Probleme
                {
                    if (Math.Abs(knots[i] - p1) < dk) p1 = knots[i];
                    if (Math.Abs(knots[i] - p2) < dk) p2 = knots[i];
                }
                if (p1 == p2) return null;
                if (nubs != null)
                {
                    Nurbs<GeoPoint2D, GeoPoint2DPole> newnubs = nubs.Trim(p1, p2);
                    List<double> newknots = new List<double>();
                    List<int> newmults = new List<int>();
                    newknots.Add(newnubs.UKnots[0]);
                    newmults.Add(1);
                    for (int i = 1; i < newnubs.UKnots.Length; ++i)
                    {
                        if (newnubs.UKnots[i] == newknots[newknots.Count - 1])
                        {
                            ++(newmults[newmults.Count - 1]);
                        }
                        else
                        {
                            newknots.Add(newnubs.UKnots[i]);
                            newmults.Add(1);
                        }
                    }
                    double[] newweights = new double[newnubs.Poles.Length];
                    for (int i = 0; i < newweights.Length; ++i)
                    {
                        newweights[i] = 1.0;
                    }
                    return new BSpline2D(newnubs.Poles, newweights, newknots.ToArray(), newmults.ToArray(), degree, false, p1, p2);
                }
                else
                {
                    Nurbs<GeoPoint2DH, GeoPoint2DHPole> newnurbs = nurbs.Trim(p1, p2);
                    List<double> newknots = new List<double>();
                    List<int> newmults = new List<int>();
                    newknots.Add(newnurbs.UKnots[0]);
                    newmults.Add(1);
                    for (int i = 1; i < newnurbs.UKnots.Length; ++i)
                    {
                        if (newnurbs.UKnots[i] == newknots[newknots.Count - 1])
                        {
                            ++(newmults[newmults.Count - 1]);
                        }
                        else
                        {
                            newknots.Add(newnurbs.UKnots[i]);
                            newmults.Add(1);
                        }
                    }
                    double[] newweights = new double[newnurbs.Poles.Length];
                    GeoPoint2D[] newpoles = new GeoPoint2D[newnurbs.Poles.Length];
                    for (int i = 0; i < newweights.Length; ++i)
                    {
                        newweights[i] = newnurbs.Poles[i].w;
                        newpoles[i] = (GeoPoint2D)newnurbs.Poles[i];
                    }
                    return new BSpline2D(newpoles, newweights, newknots.ToArray(), newmults.ToArray(), degree, false, p1, p2);
                }
            }
            catch
            {
                return null; // ein winzigstes Stück, das kann Problemne machen
            }

            //CndHlp2D.BSpline2D hlp = MakeEntity2D() as CndHlp2D.BSpline2D;
            //CndHlp2D.BSpline2D trimmed = hlp.Trim(startParam + (endParam - startParam) * StartPos, startParam + (endParam - startParam) * EndPos);
            //GeoPoint2D[] poles;
            //double[] weights;
            //double[] knots;
            //int[] multiplicities;
            //int degree;
            //bool periodic;
            //double lstartParam;
            //double lendParam;
            //CndHlp2D.GeoPoint2D[] pls;
            //trimmed.GetData(out pls, out weights, out knots, out multiplicities, out degree, out periodic, out lstartParam, out lendParam);
            //poles = new GeoPoint2D[pls.Length];
            //for (int i = 0; i < pls.Length; ++i)
            //{
            //    poles[i] = new GeoPoint2D(pls[i]);
            //}
            //return new BSpline2D(poles, weights, knots, multiplicities, degree, periodic, lstartParam, lendParam);
        }
        public bool TrimParam(double startPar, double endPar)
        {
            if (startPar < knots[0] + parameterEpsilon && endPar > knots[knots.Length - 1] - parameterEpsilon)
            {
                return false;   // es muss nichts gemacht werden
            }
            bool adjustStartPole = false;
            bool adjustEndPole = false;
            GeoPoint2D startPole = GeoPoint2D.Origin;
            GeoPoint2D endPole = GeoPoint2D.Origin;
            for (int i = 0; i < knots.Length; ++i)
            {
                if (Math.Abs(startPar - knots[i]) < parameterEpsilon)
                {
                    adjustStartPole = startPar != knots[i];
                    if (adjustStartPole) startPole = PointAtParam(startPar);
                    startPar = knots[i];
                }
                if (Math.Abs(endPar - knots[i]) < parameterEpsilon)
                {
                    adjustEndPole = endPar != knots[i];
                    if (adjustEndPole) endPole = PointAtParam(endPar);
                    endPar = knots[i];
                }
                // ggf. diesen Fall merken und einen StartPol bzw. EndPol korrigieren
            }
            if (nubs != null)
            {
                Nurbs<GeoPoint2D, GeoPoint2DPole> newnubs = nubs.Trim(startPar, endPar);
                List<double> newknots = new List<double>();
                List<int> newmults = new List<int>();
                newknots.Add(newnubs.UKnots[0]);
                newmults.Add(1);
                for (int i = 1; i < newnubs.UKnots.Length; ++i)
                {
                    if (newnubs.UKnots[i] == newknots[newknots.Count - 1])
                    {
                        ++(newmults[newmults.Count - 1]);
                    }
                    else
                    {
                        newknots.Add(newnubs.UKnots[i]);
                        newmults.Add(1);
                    }
                }
                double[] newweights = new double[newnubs.Poles.Length];
                for (int i = 0; i < newweights.Length; ++i)
                {
                    newweights[i] = 1.0;
                }
                this.poles = (GeoPoint2D[])newnubs.Poles.Clone();
                this.knots = newknots.ToArray();
                this.multiplicities = newmults.ToArray();
                this.weights = newweights;
                this.startParam = startPar;
                this.endParam = endPar;
                this.periodic = false;
                InvalidateCache();
                return true;
            }
            else
            {
                Nurbs<GeoPoint2DH, GeoPoint2DHPole> newnurbs = nurbs.Trim(startPar, endPar);
                List<double> newknots = new List<double>();
                List<int> newmults = new List<int>();
                newknots.Add(newnurbs.UKnots[0]);
                newmults.Add(1);
                for (int i = 1; i < newnurbs.UKnots.Length; ++i)
                {
                    if (newnurbs.UKnots[i] == newknots[newknots.Count - 1])
                    {
                        ++(newmults[newmults.Count - 1]);
                    }
                    else
                    {
                        newknots.Add(newnurbs.UKnots[i]);
                        newmults.Add(1);
                    }
                }
                double[] newweights = new double[newnurbs.Poles.Length];
                GeoPoint2D[] newpoles = new GeoPoint2D[newnurbs.Poles.Length];
                for (int i = 0; i < newweights.Length; ++i)
                {
                    newweights[i] = newnurbs.Poles[i].w;
                    newpoles[i] = (GeoPoint2D)newnurbs.Poles[i];
                }
                this.poles = newpoles;
                if (adjustStartPole) poles[0] = startPole;
                if (adjustEndPole) poles[poles.Length - 1] = endPole;
                this.knots = newknots.ToArray();
                this.multiplicities = newmults.ToArray();
                this.weights = newweights;
                this.startParam = startPar;
                this.endParam = endPar;
                this.periodic = false;
                InvalidateCache();
                return true;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Split (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override ICurve2D[] Split(double Position)
        {
            double u = startParam + (endParam - startParam) * Position;
            return SplitParam(u);
        }
        private ICurve2D[] SplitParam(double u)
        {
            if (u <= knots[0] || u >= knots[knots.Length - 1])
            {
                return new ICurve2D[] { Clone() };
            }
            else
            {
                double[] newknots;
                double[] newweights;
                GeoPoint2D[] newpoles;
                int indtosplit;
                if (nubs != null)
                {
                    indtosplit = nubs.CurveKnotIns(u, degree, out newknots, out newpoles);
                    newweights = new double[newpoles.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newweights[i] = 1.0;
                    }
                }
                else
                {
                    GeoPoint2DH[] newpolestmp;
                    indtosplit = nurbs.CurveKnotIns(u, degree, out newknots, out newpolestmp);
                    newpoles = new GeoPoint2D[newpolestmp.Length];
                    newweights = new double[newpolestmp.Length];
                    for (int i = 0; i < newpoles.Length; ++i)
                    {
                        newpoles[i] = newpolestmp[i];
                        newweights[i] = newpolestmp[i].w;
                    }
                }
                List<double> knots1 = new List<double>();
                List<int> mults1 = new List<int>();
                List<double> knots2 = new List<double>();
                List<int> mults2 = new List<int>();
                List<GeoPoint2D> poles1 = new List<GeoPoint2D>();
                List<GeoPoint2D> poles2 = new List<GeoPoint2D>();
                List<double> weights1 = new List<double>();
                List<double> weights2 = new List<double>();
                knots1.Add(newknots[0]);
                mults1.Add(1);
                knots2.Add(u);
                mults2.Add(1);
                int kn1 = 0;
                for (int i = 1; i < newknots.Length; ++i)
                {
                    if (newknots[i] <= u)
                    {
                        if (knots1[knots1.Count - 1] == newknots[i]) ++mults1[mults1.Count - 1];
                        else
                        {
                            knots1.Add(newknots[i]);
                            mults1.Add(1);
                        }
                        ++kn1;
                    }
                    if (newknots[i] >= u)
                    {
                        if (knots2[knots2.Count - 1] == newknots[i]) ++mults2[mults2.Count - 1];
                        else
                        {
                            knots2.Add(newknots[i]);
                            mults2.Add(1);
                        }
                    }
                }
                ++mults1[mults1.Count - 1]; // der fehlt halt noch;
                indtosplit = kn1 - degree; // kleine Nachbesserung, manchmal Probleme, wenn genau auf einem Knoten geschnitten wird
                for (int i = 0; i < newpoles.Length; ++i)
                {
                    if (i <= indtosplit)
                    {
                        poles1.Add(newpoles[i]);
                        weights1.Add(newweights[i]);
                    }
                    if (i >= indtosplit)
                    {
                        poles2.Add(newpoles[i]);
                        weights2.Add(newweights[i]);
                    }
                }
                return new ICurve2D[]{
                    new BSpline2D(poles1.ToArray(),weights1.ToArray(),knots1.ToArray(),mults1.ToArray(),degree,false,startParam,u),
                    new BSpline2D(poles2.ToArray(),weights2.ToArray(),knots2.ToArray(),mults2.ToArray(),degree,false,u,endParam)};
            }
        }
        public override double Length
        {
            get
            {
                //				try
                //				{
                //					CndHlp2D.Entity2D hlp = MakeEntity2D();
                //					return hlp.Length;
                //				}
                //				catch
                //				{
                try
                {
                    ICurve2D cv = this.Approximate(true, -poles.Length);
                    return cv.Length;
                }
                catch (Exception e)
                {
                    if (e is ThreadAbortException) throw (e);
                    return base.Length;
                }
                //				}
            }
        }
        public override GeoPoint2D StartPoint
        {
            get
            {
                GeoPoint2D res = PointAt(0.0);
                return res;
            }
            set
            {
                if (periodic)
                {
                    poles[0] = value;
                    InvalidateCache();
                }
                else
                {
                    if (startParam > knots[0]) Clamp(startParam, endParam);
                    poles[0] = value;
                    InvalidateCache();
                }
            }
        }
        public override GeoPoint2D EndPoint
        {
            get
            {
                GeoPoint2D res = PointAt(1.0);
                if (res.IsNan) res = poles[poles.Length - 1];
                return res;
            }
            set
            {
                if (periodic)
                {
                    poles[poles.Length - 1] = value;
                    InvalidateCache();
                }
                else
                {
                    if (endParam != knots[knots.Length - 1]) Clamp(startParam, endParam);
                    poles[poles.Length - 1] = value;
                    InvalidateCache();
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoPoint2D PointAt(double Position)
        {
            if (nubs == null && nurbs == null) Init(); // nach dem Einlesen ist dies leider manchmal der Fall (Reihenfolge von IDeserializationCallback)
            GeoPoint2D res;
            double u = startParam + Position * (endParam - startParam);
            if (nubs != null) res = nubs.CurvePoint(u);
            else res = nurbs.CurvePoint(u);
#if DEBUG
            //string[] formula = nurbs.CurvePointFormula(u);
            //for (int i = 0; i < formula.Length; i++)
            //{
            //    System.Diagnostics.Trace.WriteLine(formula[i]);
            //}
            //Polynom[] plns = nurbs.CurvePointPolynom(u);
            //double x = plns[0].Eval(u) / plns[2].Eval(u);
            //double y = plns[1].Eval(u) / plns[2].Eval(u);
#endif
            if (res.IsNan)
            {
                if (Position == 0.0) res = poles[0];
                else if (Position == 1.0) res = poles[poles.Length - 1];
            }
            return res;
        }
        internal GeoPoint2D PointAtParam(double param)
        {
            GeoPoint2D res;
            if (param < startParam) param = startParam;
            if (param > endParam) param = endParam;
            if (nubs != null) res = nubs.CurvePoint(param);
            else res = nurbs.CurvePoint(param);
            return res;
        }
        internal GeoVector2D DirectionAtParam(double u)
        {
            GeoVector2D res;
            if (nubs != null)
            {
                GeoPoint2D pnt, dir;
                nubs.CurveDeriv1(u, out pnt, out dir);
                res = dir.ToVector();
            }
            else
            {
                GeoPoint2DH pnth, dirh;
                nurbs.CurveDeriv1(u, out pnth, out dirh);
                res = dirh;
            }
            if (res.Length > 0.0) res.Norm();
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public override GeoVector2D DirectionAt(double Position)
        {
            double u = startParam + Position * (endParam - startParam);
            GeoVector2D res;
            if (nubs != null)
            {
                GeoPoint2D pnt, dir;
                nubs.CurveDeriv1(u, out pnt, out dir);
                res = dir.ToVector();
            }
            else
            {
                GeoPoint2DH pnth, dirh;
                nurbs.CurveDeriv1(u, out pnth, out dirh);
                res = dirh;
            }
            // if (res.Length > 0.0) res.Norm();
            // nicht normiert! wenn es wo normiert gebraucht wird, dann dort extra normieren!
            return (endParam - startParam) * res;
        }
        public override GeoVector2D StartDirection
        {
            get
            {
                return DirectionAt(0.0);
            }
        }
        public override GeoVector2D EndDirection
        {
            get
            {
                return DirectionAt(1.0);
            }
        }
        public override GeoVector2D MiddleDirection
        {
            get
            {
                return DirectionAt(0.5);
            }
        }
        public override double[] GetSelfIntersections()
        {
            return base.GetSelfIntersections();
        }
        private void AddApproximateArc(double spar, double epar, double precision, List<ICurve2D> parts)
        {
            PointDirAt(spar, out GeoPoint2D sp, out GeoVector2D sd);
            PointDirAt(epar, out GeoPoint2D ep, out GeoVector2D ed);
            Arc2D a2d = Arc2D.From2PointsAndTangents(sp, sd, ep, ed);
            if (a2d == null) parts.Add(new Line2D(sp, ep));
            else
            {
                double mpar = (spar + epar) / 2.0;
                if (Math.Abs(a2d.Distance(PointAtParam(mpar))) > precision && (epar - spar) > (endParam - startParam) * 1e-6)
                {
                    AddApproximateArc(spar, mpar, precision, parts);
                    AddApproximateArc(mpar, epar, precision, parts);
                }
                else
                {
                    parts.Add(a2d);
                }
            }
        }

        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Parallel (double, bool, double, double)"/>
        /// </summary>
        /// <param name="Dist"></param>
        /// <param name="approxSpline"></param>
        /// <param name="precision"></param>
        /// <param name="roundAngle"></param>
        /// <returns></returns>
        public override ICurve2D Parallel(double Dist, bool approxSpline, double precision, double roundAngle)
        {
            // Man bräuchte einen Konstruktor, der auch Knoten (und ggf. Gewichte) nimmt
            // dann könnte man versuchen den parallelen spline zu bilden
            //for (int n = 1; n < 65; n = n * 2)
            //{
            //    int l = n * (degree * knots.Length);
            //    GeoPoint2D[] throughpoints = new GeoPoint2D[l];
            //    for (int i = 0; i < l; ++i)
            //    {
            //        double par = (double)i / (double)(l - 1);
            //        GeoPoint2D p = PointAt(par);
            //        GeoVector2D dir = DirectionAt(par);
            //        throughpoints[i] = p + Dist * dir.Normalized.ToRight();
            //    }
            //    BSpline2D res = new BSpline2D(throughpoints, degree, periodic);
            //    if (this.MinDistance(res) > Math.Abs(Dist) - precision) return res;
            //}
            return base.Parallel(Dist, approxSpline, precision, roundAngle);
            // folgendes war der Versuch, eine Parallele polylinie zu den polen zu verwenden, das geht nicht
            //GeoPoint2D[] parallelPoles = new GeoPoint2D[poles.Length];
            //if (IsClosed)
            //{
            //    GeoVector2D dir0 = (poles[1] - poles[0]).Normalized.ToRight();
            //    GeoVector2D dirn = (poles[poles.Length - 1] - poles[poles.Length - 2]).Normalized.ToRight();
            //    GeoPoint2D p0 = poles[0] + Dist * dir0;
            //    GeoPoint2D p1 = poles[1] + Dist * dir0;
            //    GeoPoint2D pn0 = poles[poles.Length - 2] + Dist * dirn;
            //    GeoPoint2D pn1 = poles[poles.Length - 1] + Dist * dirn;
            //    GeoPoint2D ip;
            //    if (Geometry.IntersectLL(p0, p1, pn0, pn1, out ip))
            //    {
            //        parallelPoles[0] = ip;
            //        parallelPoles[poles.Length - 1] = ip;
            //    }
            //    else
            //    {
            //        parallelPoles[0] = p0;
            //        parallelPoles[poles.Length - 1] = pn1;
            //    }
            //}
            //else
            //{
            //    GeoVector2D dir0 = (poles[1] - poles[0]).Normalized.ToRight();
            //    GeoVector2D dirn = (poles[poles.Length - 1] - poles[poles.Length - 2]).Normalized.ToRight();
            //    parallelPoles[0] = poles[0] + Dist * dir0;
            //    parallelPoles[poles.Length - 1] = poles[poles.Length - 1] + Dist * dirn;
            //}
            //for (int i = 1; i < poles.Length - 1; ++i)
            //{
            //    GeoVector2D dir0 = (poles[i] - poles[i - 1]).Normalized.ToRight();
            //    GeoVector2D dir1 = (poles[i + 1] - poles[i]).Normalized.ToRight();
            //    GeoPoint2D p0 = poles[i - 1] + Dist * dir0;
            //    GeoPoint2D p1 = poles[i] + Dist * dir0;
            //    GeoPoint2D p2 = poles[i] + Dist * dir1;
            //    GeoPoint2D p3 = poles[i + 1] + Dist * dir1;
            //    GeoPoint2D ip;
            //    if (Geometry.IntersectLL(p0, p1, p2, p3, out ip))
            //    {
            //        parallelPoles[i] = ip;
            //    }
            //    else
            //    {
            //        parallelPoles[i] = new GeoPoint2D(p1, p2); // Mitte zwischen den beiden versetzen
            //    }
            //}
            //// der Konstruktor cloned die arrays
            //BSpline2D res = new BSpline2D(parallelPoles, weights, knots, multiplicities, degree, periodic, startParam, endParam);
            //double dbg = this.MinDistance(res);
            //return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetFused (ICurve2D, double)"/>
        /// </summary>
        /// <param name="toFuseWith"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override ICurve2D GetFused(ICurve2D toFuseWith, double precision)
        {
            if (toFuseWith is BSpline2D)
            {
                BSpline2D other = toFuseWith as BSpline2D;
                bool identical = true;
                if (other.degree == degree && other.knots.Length == knots.Length && other.poles.Length == poles.Length)
                {
                    for (int i = 0; i < poles.Length; ++i)
                    {
                        if ((poles[i] | other.poles[i]) > precision)
                        {
                            identical = false;
                            break;
                        }
                    }
                    if (identical)
                    {
                        for (int i = 0; i < knots.Length; ++i)
                        {
                            if (Math.Abs(knots[i] - other.knots[i]) > 1e-6)
                            {
                                identical = false;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    return null;
                }
                if (identical) return this.Clone();
                //if ((StartPoint | EndPoint) > precision && (StartPoint | other.EndPoint) < precision && (EndPoint | other.StartPoint) < precision)
                //{
                //    other = other.CloneReverse(true);
                //}
                // umdrehen is nicht notwendig
                // jetzt einfach testen ob die Knoten und Punkte zwischen den Knoten identisch sind
                for (int i = 0; i < knots.Length; ++i)
                {
                    if (other.Distance(PointAtParam(knots[i])) > precision) return null;
                    if (i > 0 && other.Distance(PointAtParam((knots[i - 1] + knots[i]) / 2.0)) > precision) return null;
                }
                return this.Clone();
                // Brachial wäre: einen QuadTree machen bis runter zu precision, der überprüft, ob es ein Quadrat mit nur einem Spline gibt
            }
            return null;
        }
        /// <summary>
        /// wie DirectionAt, jedoch Ergebnis nicht normiert
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        internal GeoVector2D DerivationAt(double Position)
        {
            double u = startParam + Position * (endParam - startParam);
            GeoPoint2D pnt, dir;
            if (nubs != null) nubs.CurveDeriv1(u, out pnt, out dir);
            else nubs.CurveDeriv1(u, out pnt, out dir);
            GeoVector2D res = dir.ToVector();
            return res;
        }

        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (ICurve2D)"/>
        /// </summary>
        /// <param name="IntersectWith"></param>
        /// <returns></returns>
        public override GeoPoint2DWithParameter[] Intersect(ICurve2D IntersectWith)
        {
            return base.Intersect(IntersectWith);
        }
        private void Intersect(BSpline2D b2d, GeoPoint2D sp1, double spar1, GeoVector2D sdir1, GeoPoint2D ep1, double epar1, GeoVector2D edir1, GeoPoint2D tri1, GeoPoint2D sp2, double spar2, GeoVector2D sdir2, GeoPoint2D ep2, double epar2, GeoVector2D edir2, GeoPoint2D tri2, SortedDictionary<double, GeoPoint2DWithParameter> list)
        {   // zwei Abschnitte von zwei BSplines schneiden, es ist schon getestet dass die Dreiecke sich schneiden
            // 1. Versuch: Basislinienschnitt
            bool done = false;
            if (Geometry.SegmentIntersection(sp1, ep1, sp2, ep2))
            {
                done = Intersect(b2d, sp1, spar1, ep1, epar1, sp2, spar2, ep2, epar2, list);
            }
            // 2. Versuch: Dreieckshalbierung
            if (!done)
            {   // Dreieck halbieren und weiter probieren
                // hängt bisweilen, muss sicherer werden!
                if (TringleIntersect(sp1, tri1, ep1, sp2, tri2, ep2))
                {
                    double mpar1 = (spar1 + epar1) / 2.0;
                    double mpar2 = (spar2 + epar2) / 2.0;
                    GeoPoint2D mp1, mp2;
                    GeoVector2D mdir1, mdir2;
                    PointDirAt(mpar1, out mp1, out mdir1);
                    b2d.PointDirAt(mpar2, out mp2, out mdir2);
                    GeoPoint2D tri11, tri12, tri21, tri22;
                    if (Geometry.IntersectLL(sp1, sdir1, mp1, mdir1, out tri11) &&
                        Geometry.IntersectLL(mp1, mdir1, ep1, edir1, out tri12) &&
                        Geometry.IntersectLL(sp2, sdir2, mp2, mdir2, out tri21) &&
                        Geometry.IntersectLL(mp2, mdir2, ep2, edir2, out tri22))
                    {
                        Intersect(b2d, sp1, spar1, sdir1, mp1, mpar1, mdir1, tri11, sp2, spar2, sdir2, mp2, mpar2, mdir2, tri21, list);
                        Intersect(b2d, sp1, spar1, sdir1, mp1, mpar1, mdir1, tri11, mp2, mpar2, mdir2, ep2, epar2, edir2, tri21, list);
                        Intersect(b2d, mp1, mpar1, mdir1, ep1, epar1, edir1, tri12, sp2, spar2, sdir2, mp2, mpar2, mdir2, tri22, list);
                        Intersect(b2d, mp1, mpar1, mdir1, ep1, epar1, edir1, tri12, mp2, mpar2, mdir2, ep2, epar2, edir2, tri22, list);
                    }
                }
            }
        }
        private bool Intersect(BSpline2D b2d, GeoPoint2D sp1, double spar1, GeoPoint2D ep1, double epar1, GeoPoint2D sp2, double spar2, GeoPoint2D ep2, double epar2, SortedDictionary<double, GeoPoint2DWithParameter> list)
        {
            // Basislinienschnitt per doppelter proportionaler Bisektion:
            // die beiden basislinien (Sehnen) der beiden NURBS Abschnitte schneiden sich. Die Iteration läuft jetzt so,
            // dass beide NURBS im Schnittverhältnis aufgeteilt werden und von den beiden Teilstücken auf jedem Abschnitt
            // jeweils das bessere genommen wird. Die Auswahl des besseren ist nicht trivial, aber 
            // das iteriert auch, wenn pos1 oder pos2 außerhalb des [0..1] Intervalls liegen
            // das kommt schon mal vor
            // ---------------------
            // Schlechtes Verfahren: vielleicht so probieren: Der proportionale Sehnenschnitt liefert auf jeder Kurve
            // einen Punkt. Wir suchen auf der einen Kurve einen Punkt, der im inneren des anderen Dreiecks liegt (das 
            // könnte der Sehnenschnittpunkt sein, oder in dessen Nähe, aber es muss einen geben, da sich die 
            // Ausgangsdreicke schneiden. Dann suchen wir noch rechts und links davon einen Punkt, der außerhalb
            // des Dreiecks liegt. So haben wir ein nuese Dreieck. Jetzt gehts andersrum weiter.
            // ODER: Tangentenverfahren: Im Sehnenschnittpunkt nehmen wir die Tangenten mit ungenormtem Vektor. 
            // Die Vektorlänge geht doch irgendwie mit dem Parameter einher. Dort wo sich die beiden Tangenten schneiden
            // ist unser neuer Ausgangspunkt. Das sollte konvergieren, wir überprüfen lediglich, ob die Parameterintervalle
            // verlassen werden. Da keine Wendepunkte, sollte das ganze konvergieren.
            GeoPoint2D mp1, mp2;
            double pos1 = Geometry.IntersectLLpar(sp1, ep1, sp2, ep2);
            double pos2 = Geometry.IntersectLLpar(sp2, ep2, sp1, ep1);
            double mpar1, mpar2;
            GeoVector2D dir1, dir2;
            mpar1 = spar1 + pos1 * (epar1 - spar1);
            mpar2 = spar2 + pos2 * (epar2 - spar2);
            int dbgc = 0;
            while (mpar1 >= spar1 && mpar1 <= epar1 && mpar2 >= spar2 && mpar2 <= epar2)
            {
                PointDerAt(mpar1, out mp1, out dir1);
                b2d.PointDerAt(mpar2, out mp2, out dir2);
                Geometry.IntersectLLpar(mp1, dir1, mp2, dir2, out pos1, out pos2);
                if ((Math.Abs(pos1) < parameterEpsilon && Math.Abs(pos2) < b2d.parameterEpsilon) || Geometry.Dist(mp1, mp2) < (distanceEpsilon + b2d.distanceEpsilon))
                {
                    GeoPoint2DWithParameter gpp = new GeoPoint2DWithParameter();
                    gpp.p = new GeoPoint2D(mp1, mp2);
                    gpp.par1 = (mpar1 - startParam) / (endParam - startParam);
                    gpp.par2 = (mpar2 - b2d.startParam) / (b2d.endParam - b2d.startParam);
                    list[mpar1] = gpp;
                    return true;
                }
                mpar1 += pos1;
                mpar2 += pos2;
                ++dbgc;
            }
            //    do
            //    {
            //        if (pos1 < 0.01) pos1 *= 2;
            //        if (pos1 > 0.99) pos1 = 1 - (1 - pos1) * 2;
            //        if (pos2 < 0.01) pos2 *= 2;
            //        if (pos2 > 0.99) pos2 = 1 - (1 - pos2) * 2;
            //        if (pos1 < -0.5 || pos1 > 1.5 || pos2 < -0.5 || pos2 > 1.5)
            //        {
            //            return false; // so weit außerhalb soll es nicht liegen
            //        }
            //        mpar1 = spar1 + pos1 * (epar1 - spar1);
            //        mpar2 = spar2 + pos2 * (epar2 - spar2);
            //        mp1 = PointAtParam(mpar1);
            //        mp2 = b2d.PointAtParam(mpar2);
            //        double pos11 = Geometry.IntersectLLpar(sp1, mp1, sp2, mp2);
            //        double pos12 = Geometry.IntersectLLpar(mp1, ep1, sp2, mp2);
            //        double pos13 = Geometry.IntersectLLpar(sp1, mp1, mp2, ep2);
            //        double pos14 = Geometry.IntersectLLpar(mp1, ep1, mp2, ep2);
            //        double pos21 = Geometry.IntersectLLpar(sp2, mp2, sp1, mp1);
            //        double pos22 = Geometry.IntersectLLpar(sp2, mp2, mp1, ep1);
            //        double pos23 = Geometry.IntersectLLpar(mp2, ep2, sp1, mp1);
            //        double pos24 = Geometry.IntersectLLpar(mp2, ep2, mp1, ep1);
            //        // pos1j gehört zu pos2j. Gesucht: das paar, das bei beiden am nächsten an 0.5 liegt
            //        SortedList<double, int> caselist = new SortedList<double, int>();
            //        caselist[Math.Abs(pos11 - 0.5) + Math.Abs(pos21 - 0.5)] = 1;
            //        caselist[Math.Abs(pos12 - 0.5) + Math.Abs(pos22 - 0.5)] = 2;
            //        caselist[Math.Abs(pos13 - 0.5) + Math.Abs(pos23 - 0.5)] = 3;
            //        caselist[Math.Abs(pos14 - 0.5) + Math.Abs(pos24 - 0.5)] = 4;
            //        switch (caselist.Values[0])
            //        {
            //            case 1:
            //                pos1 = pos11;
            //                pos2 = pos21;
            //                epar1 = mpar1;
            //                epar2 = mpar2;
            //                ep1 = mp1;
            //                ep2 = mp2;
            //                break;
            //            case 2:
            //                pos1 = pos12;
            //                pos2 = pos22;
            //                spar1 = mpar1;
            //                epar2 = mpar2;
            //                sp1 = mp1;
            //                ep2 = mp2;
            //                break;
            //            case 3:
            //                pos1 = pos13;
            //                pos2 = pos23;
            //                epar1 = mpar1;
            //                spar2 = mpar2;
            //                ep1 = mp1;
            //                sp2 = mp2;
            //                break;
            //            case 4:
            //                pos1 = pos14;
            //                pos2 = pos24;
            //                spar1 = mpar1;
            //                spar2 = mpar2;
            //                sp1 = mp1;
            //                sp2 = mp2;
            //                break;
            //        }
            //        ++dbgc;
            //    }
            //    while ((Math.Abs(epar1 - spar1) > eps) && (Math.Abs(epar2 - spar2) > b2d.eps));
            //    // ein Abbruch genügt, sonst wird es keine Linie mehr
            //    GeoPoint2DWithParameter gpp = new GeoPoint2DWithParameter();
            //    gpp.p = new GeoPoint2D(mp1, mp2);
            //    gpp.par1 = (mpar1 - startParam) / (endParam - startParam);
            //    gpp.par2 = (mpar2 - b2d.startParam) / (b2d.endParam - b2d.startParam);
            //    list[mpar1] = gpp;
            //    return true;
            //}
            //catch (ArithmeticException)
            //{
            //    // IntersectLLpar kann das werfen, wenn der Nenner 0 wird, also parallel
            //}
            return false;
        }
        private bool TringleIntersect(GeoPoint2D t1, GeoPoint2D t2, GeoPoint2D t3, GeoPoint2D u1, GeoPoint2D u2, GeoPoint2D u3)
        {
            // überprüfen ob die Dreiecksspitze und das andere Dreieck auf verschiedenen Seiten der Basis liegen
            // das ist vermutlich ein häufiger Fall
            GeoVector2D dir1 = t3 - t1;
            bool l1 = Geometry.OnLeftSide(u1, t1, dir1);
            bool l2 = Geometry.OnLeftSide(u2, t1, dir1);
            if (l1 == l2)
            {
                bool l3 = Geometry.OnLeftSide(u3, t1, dir1);
                if (l3 == l1)
                {
                    bool l4 = Geometry.OnLeftSide(t2, t1, dir1);
                    if (l4 != l3) return false;
                }
            }
            GeoVector2D dir2 = u3 - u1;
            l1 = Geometry.OnLeftSide(t1, u1, dir2);
            l2 = Geometry.OnLeftSide(t2, u1, dir2);
            if (l1 == l2)
            {
                bool l3 = Geometry.OnLeftSide(t3, u1, dir2);
                if (l3 == l1)
                {
                    bool l4 = Geometry.OnLeftSide(u2, u1, dir2);
                    if (l4 != l3) return false;
                }
            }
            // jetzt testen ob irgend ein Schnitt vorliegt
            if (Geometry.SegmentIntersection(t1, t2, u1, u2)) return true;
            if (Geometry.SegmentIntersection(t1, t2, u1, u3)) return true;
            if (Geometry.SegmentIntersection(t1, t2, u2, u3)) return true;
            if (Geometry.SegmentIntersection(t1, t3, u1, u2)) return true;
            if (Geometry.SegmentIntersection(t1, t3, u1, u3)) return true;
            if (Geometry.SegmentIntersection(t1, t3, u2, u3)) return true;
            if (Geometry.SegmentIntersection(t2, t3, u1, u2)) return true;
            if (Geometry.SegmentIntersection(t2, t3, u1, u3)) return true;
            if (Geometry.SegmentIntersection(t2, t3, u2, u3)) return true;
            return false;
        }
        private void IntersectTriangle(GeoPoint2D startpoint, GeoVector2D startdir, double startpar, GeoPoint2D endpoint, GeoVector2D enddir, double endpar, Line2D l2d, SortedDictionary<double, GeoPoint2D> list)
        {
            GeoPoint2D trianglepoint;
            if (Geometry.IntersectLL(startpoint, startdir, endpoint, enddir, out trianglepoint))
            {
                // die Bedingung dass hier überhaupt was geht ist: die Linie schneidet eine der 
                // Dreiecksseiten oder sie liegt ganz im Dreieck
                // wenn die Basislinie geschnitten wird, dann ist das der einfache Fall mit nur einem Schnittpunkt
                // Es gibt aber ein Problem, wenn die basislinie in genau einem Endpunkt geschnitten wird und die gegenüberliegende Dreiecksseite:
                // dann kann es zwei Schnittpunkte geben
                bool baselineintersect = Geometry.SegmentIntersection(startpoint, endpoint, l2d.StartPoint, l2d.EndPoint);
                if (baselineintersect)
                {
                    if (Geometry.SegmentIntersection(startpoint, trianglepoint, l2d.StartPoint, l2d.EndPoint) &&
                        Geometry.SegmentIntersection(trianglepoint, endpoint, l2d.StartPoint, l2d.EndPoint))
                    {
                        baselineintersect = false;
                        // es werden nämlich beide Seiten gescnitten, d.h. der Schnitt geht durch einen Eckpunkt
                        // aber auch noch durch einen anderen Punkt
                    }
                }
                if (baselineintersect)
                {   // schneidet die Basislinie: hier können wir besser iterieren, die andere Iteration
                    // stößt auf Probleme den Dreieckspunkt zu finden, wenn die Parameterdifferenz sehr klein wird
                    // Außerdem ist die andere Iteration viel langsamer und rekursiv
                    try
                    {
                        GeoPoint2D mp = startpoint;
                        double pos = Geometry.IntersectLLpar(startpoint, endpoint, l2d.StartPoint, l2d.EndPoint);
                        double middlepar = startpar;
                        int dbgc = 0;
                        double poscorrfactor = 2.0;
                        do
                        {
                            if (pos < 0.25) pos *= poscorrfactor;
                            if (pos > 0.75) pos = 1 - (1 - pos) * poscorrfactor;
                            double m = startpar + pos * (endpar - startpar);
                            if (m == middlepar) break;
                            middlepar = m;
                            mp = PointAtParam(middlepar);
                            double pos1 = Geometry.IntersectLLpar(startpoint, mp, l2d.StartPoint, l2d.EndPoint);
                            double pos2 = Geometry.IntersectLLpar(mp, endpoint, l2d.StartPoint, l2d.EndPoint);
                            // auf welchem Abschnitt liegt der Punkt?
                            double pos1abs = Math.Abs(pos1 - 0.5);
                            double pos2abs = Math.Abs(pos2 - 0.5);
                            if (pos1abs < pos2abs)
                            {
                                if (pos > 0.75)
                                {
                                    poscorrfactor *= 2;
                                }
                                else
                                {
                                    endpoint = mp;
                                    endpar = middlepar;
                                    pos = pos1;
                                    poscorrfactor = 2.0;
                                }
                            }
                            else
                            {
                                if (pos < 0.25)
                                {
                                    poscorrfactor *= 2;
                                }
                                else
                                {
                                    startpoint = mp;
                                    startpar = middlepar;
                                    pos = pos2;
                                    poscorrfactor = 2.0;
                                }
                            }
                            ++dbgc;
                            if (dbgc > 30) { }
                        }
                        // while (Math.Abs(Geometry.DistPL(mp, l2d.StartPoint, l2d.EndPoint)) > Precision.eps);
                        while (Math.Abs(endpar - startpar) > parameterEpsilon);
                        list[middlepar] = mp;
                    }
                    catch (ArithmeticException)
                    {
                        // IntersectLLpar kann das werfen, wenn der Nenner 0 wird, also parallel
                    }
                }
                else if (Geometry.SegmentIntersection(startpoint, trianglepoint, l2d.StartPoint, l2d.EndPoint) ||
                    Geometry.SegmentIntersection(trianglepoint, endpoint, l2d.StartPoint, l2d.EndPoint) ||
                    PointInsideTriangle(l2d.StartPoint, startpoint, trianglepoint, endpoint))
                {
                    double middlepar = (startpar + endpar) / 2.0;
                    GeoPoint2D middlepoint;
                    GeoVector2D middledir;
                    PointDirAt(middlepar, out middlepoint, out middledir);
                    if ((endpar - startpar) < parameterEpsilon &&
                        Geometry.DistPL(middlepoint, l2d.StartPoint, l2d.EndPoint) < Precision.eps)
                    {
                        // gefunden!
                        list[middlepar] = middlepoint;
                    }
                    else
                    {
                        // unterteilen
                        IntersectTriangle(startpoint, startdir, startpar, middlepoint, middledir, middlepar, l2d, list);
                        IntersectTriangle(middlepoint, middledir, middlepar, endpoint, enddir, endpar, l2d, list);
                    }
                }
            }
            else
            {
                // der Dreickspunkt ist nicht mehr auffindbar, vielleicht ist es echt tangential und wir haben
                // den Berührpunkt schon
                double middlepar = (startpar + endpar) / 2.0;
                GeoPoint2D middlepoint;
                GeoVector2D middledir;
                PointDirAt(middlepar, out middlepoint, out middledir);
                if (Geometry.DistPL(middlepoint, l2d.StartPoint, l2d.EndPoint) < Precision.eps)
                {
                    // gefunden!
                    list[middlepar] = middlepoint;
                    // das liefert hunderte von Lösungen, alle fast mit dem gleichen punkt...
                    // da stimmt was nicht
                }
            }
        }
        private bool PointInsideTriangle(GeoPoint2D testpoint, GeoPoint2D startpoint, GeoPoint2D trianglepoint, GeoPoint2D endpoint)
        {
            bool l1 = Geometry.OnLeftSide(testpoint, startpoint, trianglepoint - startpoint);
            bool l2 = Geometry.OnLeftSide(testpoint, trianglepoint, endpoint - trianglepoint);
            bool l3 = Geometry.OnLeftSide(testpoint, startpoint, endpoint - startpoint);
            return (l1 == l2 && l1 != l3);
        }
        /// <summary>
        /// Calculate point and direction at a given parameter <paramref name="param"/>, <paramref name="dir"/> will be normalized upon return
        /// </summary>
        /// <param name="param"></param>
        /// <param name="point"></param>
        /// <param name="dir"></param>
        internal void PointDirAt(double param, out GeoPoint2D point, out GeoVector2D dir)
        {
            if (nubs != null)
            {
                GeoPoint2D dirp;
                nubs.CurveDeriv1(param, out point, out dirp);
                dir = dirp.ToVector();
            }
            else
            {
                GeoPoint2DH pointh, dirph;
                nurbs.CurveDeriv1(param, out pointh, out dirph);
                dir = dirph;
                point = pointh;
            }
            if (dir.Length > 0.0) dir.Norm();
        }
        /// <summary>
        /// Same as <see cref="BSpline2D.PointDirAt(double, out GeoPoint2D, out GeoVector2D)"/> but without normalizing
        /// </summary>
        /// <param name="param"></param>
        /// <param name="point"></param>
        /// <param name="dir"></param>
        internal void PointDerAt(double param, out GeoPoint2D point, out GeoVector2D dir)
        {
            if (nubs != null)
            {
                GeoPoint2D dirp;
                nubs.CurveDeriv1(param, out point, out dirp);
                dir = dirp.ToVector();
            }
            else
            {
                GeoPoint2DH pointh, dirph;
                nurbs.CurveDeriv1(param, out pointh, out dirph);
                dir = dirph;
                point = pointh;
            }
        }
        private class CompareCloseTo : IComparer<GeoPoint2D>
        {   // sortiert die Punkte im Abstand zu "ToHere"
            private GeoPoint2D ToHere;
            public CompareCloseTo(GeoPoint2D ToHere)
            {
                this.ToHere = ToHere;
            }
            #region IComparer<GeoPoint2D> Members

            int IComparer<GeoPoint2D>.Compare(GeoPoint2D x, GeoPoint2D y)
            {
                double d1 = Geometry.Dist(x, ToHere);
                double d2 = Geometry.Dist(y, ToHere);
                return d1.CompareTo(d2);
            }

            #endregion
        }

        // now using base implementation
        //public override GeoPoint2D[] TangentPoints(GeoPoint2D FromHere, GeoPoint2D CloseTo)
        //{   // wenn eine Linie vom Ausgangspunkt durch eine Dreiecksspitze nicht durch die Basislinie geht,
        //    // dann ist es eine Tangente.
        //    if (tringulation == null) MakeTriangulation();
        //    List<GeoPoint2D> res = new List<GeoPoint2D>();
        //    for (int i = 0; i < tringulation.Length; ++i)
        //    {
        //        if (Geometry.OnSameSide(interpol[i], interpol[i + 1], FromHere, tringulation[i]))
        //        {   // notwendige und hinreichende Bedingung für eine tangente. Für die Eckpunkte ggf kritisch
        //            // erstmal als Bisektion implementiert. Man könnte auch einen Ellipsenbogen durch die beiden
        //            // Punkte und Richtungen legen und die Position der Tangente nehmen...
        //            double sparam = interparam[i];
        //            double eparam = interparam[i + 1];
        //            GeoVector2D sdir = interdir[i];
        //            GeoVector2D edir = interdir[i + 1];
        //            GeoPoint2D spoint = interpol[i];
        //            GeoPoint2D epoint = interpol[i + 1];
        //            GeoVector2D tdir = tringulation[i] - FromHere;
        //            double sz = sdir.x * tdir.y - sdir.y * tdir.x;
        //            double ez = edir.x * tdir.y - edir.y * tdir.x;
        //            // sz und ez sind die ZKomponente des Vektorproduktes, d.h. der sin des Winkels
        //            // zwischen der angenäherten tangente und der NURBS Kurve. Uns interessiert nur das Vorzeichen
        //            GeoPoint2D mpoint;
        //            do
        //            {
        //                double mparam = (sparam + eparam) / 2.0;
        //                GeoVector2D mdir;
        //                PointDirAt(mparam, out mpoint, out mdir);
        //                tdir = mpoint - FromHere;
        //                double mz = mdir.x * tdir.y - mdir.y * tdir.x;
        //                if ((mz < 0 && sz < 0) || (mz >= 0 && sz >= 0))
        //                {
        //                    sz = mz;
        //                    sparam = mparam;
        //                }
        //                else
        //                {
        //                    ez = mz;
        //                    eparam = mparam;
        //                }
        //            }
        //            while (Math.Abs(eparam - sparam) > parameterEpsilon);
        //            res.Add(mpoint);
        //        }
        //    }
        //    res.Sort(new CompareCloseTo(CloseTo));
        //    return res.ToArray();
        //}

        // now using base implementation
        //public override GeoPoint2D[] TangentPointsToAngle(Angle ang, GeoPoint2D CloseTo)
        //{
        //    GeoVector2D direction = new GeoVector2D(ang);
        //    List<GeoPoint2D> res = new List<GeoPoint2D>();
        //    for (int i = 0; i < tringulation.Length; ++i)
        //    {
        //        if (Geometry.OnSameSide(interpol[i], interpol[i + 1], tringulation[i], direction))
        //        {   // notwendige und hinreichende Bedingung für eine tangente. Für die Eckpunkte ggf kritisch
        //            // erstmal als Bisektion implementiert. Man könnte auch einen Ellipsenbogen durch die beiden
        //            // Punkte und Richtungen legen und die Position der Tangente nehmen...
        //            double sparam = interparam[i];
        //            double eparam = interparam[i + 1];
        //            GeoVector2D sdir = interdir[i];
        //            GeoVector2D edir = interdir[i + 1];
        //            GeoPoint2D spoint = interpol[i];
        //            GeoPoint2D epoint = interpol[i + 1];
        //            GeoVector2D tdir = direction;
        //            double sz = sdir.x * tdir.y - sdir.y * tdir.x;
        //            double ez = edir.x * tdir.y - edir.y * tdir.x;
        //            // sz und ez sind die ZKomponente des Vektorproduktes, d.h. der sin des Winkels
        //            // zwischen der angenäherten tangente und der NURBS Kurve. Uns interessiert nur das Vorzeichen
        //            GeoPoint2D mpoint;
        //            do
        //            {
        //                double mparam = (sparam + eparam) / 2.0;
        //                GeoVector2D mdir;
        //                PointDirAt(mparam, out mpoint, out mdir);
        //                double mz = mdir.x * tdir.y - mdir.y * tdir.x;
        //                if ((mz < 0 && sz < 0) || (mz >= 0 && sz >= 0))
        //                {
        //                    sz = mz;
        //                    sparam = mparam;
        //                }
        //                else
        //                {
        //                    ez = mz;
        //                    eparam = mparam;
        //                }
        //            }
        //            while (Math.Abs(eparam - sparam) > parameterEpsilon);
        //            res.Add(mpoint);
        //        }
        //    }
        //    res.Sort(new CompareCloseTo(CloseTo));
        //    return res.ToArray();
        //}

        // now using base implementation
        //public override double[] TangentPointsToAngle(GeoVector2D direction)
        //{   // Ähnlich wie TangentPoints: Die Linie hat keinen Ausgangspunkt sondern einen festen Winkel
        //    List<double> res = new List<double>();
        //    for (int i = 0; i < tringulation.Length; ++i)
        //    {
        //        if (Geometry.OnSameSide(interpol[i], interpol[i + 1], tringulation[i], direction))
        //        {   // notwendige und hinreichende Bedingung für eine tangente. Für die Eckpunkte ggf kritisch
        //            // erstmal als Bisektion implementiert. Man könnte auch einen Ellipsenbogen durch die beiden
        //            // Punkte und Richtungen legen und die Position der Tangente nehmen...
        //            double sparam = interparam[i];
        //            double eparam = interparam[i + 1];
        //            GeoVector2D sdir = interdir[i];
        //            GeoVector2D edir = interdir[i + 1];
        //            GeoPoint2D spoint = interpol[i];
        //            GeoPoint2D epoint = interpol[i + 1];
        //            GeoVector2D tdir = direction;
        //            double sz = sdir.x * tdir.y - sdir.y * tdir.x;
        //            double ez = edir.x * tdir.y - edir.y * tdir.x;
        //            // sz und ez sind die ZKomponente des Vektorproduktes, d.h. der sin des Winkels
        //            // zwischen der angenäherten tangente und der NURBS Kurve. Uns interessiert nur das Vorzeichen
        //            GeoPoint2D mpoint;
        //            double mparam;
        //            do
        //            {
        //                mparam = (sparam + eparam) / 2.0;
        //                GeoVector2D mdir;
        //                PointDirAt(mparam, out mpoint, out mdir);
        //                double mz = mdir.x * tdir.y - mdir.y * tdir.x;
        //                if ((mz < 0 && sz < 0) || (mz >= 0 && sz >= 0))
        //                {
        //                    sz = mz;
        //                    sparam = mparam;
        //                }
        //                else
        //                {
        //                    ez = mz;
        //                    eparam = mparam;
        //                }
        //            }
        //            while (Math.Abs(eparam - sparam) > parameterEpsilon);
        //            res.Add((mparam - startParam) / (endParam - startParam));
        //            // war vorher einfach "mparam", es sollte aber der normierte Parameter geliefert werden
        //        }
        //    }
        //    return res.ToArray();
        //}

        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.Intersect (GeoPoint2D, GeoPoint2D)"/>
        /// </summary>
        /// <param name="StartPoint"></param>
        /// <param name="EndPoint"></param>
        /// <returns></returns>
        public override GeoPoint2DWithParameter[] Intersect(GeoPoint2D StartPoint, GeoPoint2D EndPoint)
        {
            return Intersect(new Line2D(StartPoint, EndPoint));
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.IsValidParameter (double)"/>
        /// </summary>
        /// <param name="par"></param>
        /// <returns></returns>
        public override bool IsValidParameter(double par)
        {
            double u = startParam + par * (endParam - startParam);
            return u >= knots[0] && u <= knots[knots.Length - 1];
        }
        /// <summary>
        /// Punkt und 1. und 2. Ableitung. ACHTUNG: param ist echter Parameter nicht 0..1!
        /// dir1 und dir2 sind nicht normiert (dir2 kann 0 sein)
        /// </summary>
        /// <param name="param"></param>
        /// <param name="point"></param>
        /// <param name="dir1"></param>
        /// <param name="dir2"></param>
        private void PointDirAt2(double param, out GeoPoint2D point, out GeoVector2D dir1, out GeoVector2D dir2)
        {
            if (nubs != null)
            {
                GeoPoint2D ndir1, ndir2;
                nubs.CurveDeriv2(param, out point, out ndir1, out ndir2);
                dir1 = ndir1.ToVector();
                dir2 = ndir2.ToVector();
            }
            else
            {
                GeoPoint2DH npoint, ndir1, ndir2;
                nurbs.CurveDeriv2(param, out npoint, out ndir1, out ndir2);
                point = npoint;
                dir1 = (GeoVector2D)ndir1;
                dir2 = (GeoVector2D)ndir2;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.MakeGeoObject (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public override IGeoObject MakeGeoObject(Plane p)
        {
            BSpline bsp = BSpline.Construct();
            GeoPoint[] p3d = new GeoPoint[poles.Length];
            for (int i = 0; i < poles.Length; ++i)
            {
                p3d[i] = p.ToGlobal(poles[i]);
            }
            bsp.SetData(this.degree, p3d, weights, knots, multiplicities, periodic, startParam, endParam);
            return bsp;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.TryPointDeriv2At (double, out GeoPoint2D, out GeoVector2D, out GeoVector2D)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <param name="point"></param>
        /// <param name="deriv1"></param>
        /// <param name="deriv2"></param>
        /// <returns></returns>
        public override bool TryPointDeriv2At(double position, out GeoPoint2D point, out GeoVector2D deriv1, out GeoVector2D deriv2)
        {
            double param = startParam + position * (endParam - startParam);
            if (nubs != null)
            {
                GeoPoint2D ndir1, ndir2;
                nubs.CurveDeriv2(param, out point, out ndir1, out ndir2);
                deriv1 = ndir1.ToVector();
                deriv2 = ndir2.ToVector();
            }
            else
            {
                GeoPoint2DH npoint, ndir1, ndir2;
                nurbs.CurveDeriv2(param, out npoint, out ndir1, out ndir2);
                point = npoint;
                deriv1 = (GeoVector2D)ndir1;
                deriv2 = (GeoVector2D)ndir2;
            }
            return true;
        }
        public override double GetArea()
        {
            // ExplicitPCurve2D uses alot of memory, it is a WeakReference now
            ExplicitPCurve2D epc2d = (this as IExplicitPCurve2D).GetExplicitPCurve2D();
            if (epc2d==null || epc2d.IsRational) return base.GetArea();
            else
            {
                double epca = epc2d.Area();
                double repca = epc2d.RawArea();
                if (Math.Sign(epca) != Math.Sign(repca) || Math.Abs(epca - repca) > 0.1 * Math.Max(Math.Abs(epca), Math.Abs(repca))) return base.GetArea();
                return epca;
            }
        }
        #endregion
        #region IQuadTreeInsertable Members

        /// <summary>
        /// Overrides <see cref="CADability.Curve2D.GeneralCurve2D.GetExtent ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingRect GetExtent()
        {
            if (nubs == null && nurbs == null) Init(); // this is sometimes the case when a file is read
            return base.GetExtent();
        }

        private double FindVMax(double par1, double par2, double max)
        {   // horizontale Tangente suchen
            GeoPoint2D pleft, pright;
            GeoVector2D vleft, vright;
            PointDirAt(par1, out pleft, out vleft);
            PointDirAt(par2, out pright, out vright);
            if (vleft.y < 0) return max;
            if (vright.y > 0) return max; // sollte es nicht geben
            int counter = 0;
            do
            {
                ++counter;
                GeoPoint2D pmiddle;
                GeoVector2D vmiddle;
                double parm = (par1 + par2) / 2.0;
                PointDirAt(parm, out pmiddle, out vmiddle);
                if (vmiddle.y > 0.0) par1 = parm;
                else par2 = parm;
                if (Math.Abs(par2 - par1) < parameterEpsilon || counter > 30) return pmiddle.y;
            }
            while (true);
        }
        private double FindVMin(double par1, double par2, double min)
        {   // horizontale Tangente suchen
            GeoPoint2D pleft, pright;
            GeoVector2D vleft, vright;
            PointDirAt(par1, out pleft, out vleft);
            PointDirAt(par2, out pright, out vright);
            if (vleft.y > 0) return min;
            if (vright.y < 0) return min; // sollte es nicht geben
            int counter = 0;
            do
            {
                ++counter;
                GeoPoint2D pmiddle;
                GeoVector2D vmiddle;
                double parm = (par1 + par2) / 2.0;
                PointDirAt(parm, out pmiddle, out vmiddle);
                if (vmiddle.y < 0.0) par1 = parm;
                else par2 = parm;
                if (Math.Abs(par2 - par1) < parameterEpsilon || counter > 30) return pmiddle.y;
            }
            while (true);
        }
        private double FindHMax(double par1, double par2, double max)
        {   // vertikale Tangente suchen
            GeoPoint2D pleft, pright;
            GeoVector2D vleft, vright;
            PointDirAt(par1, out pleft, out vleft);
            PointDirAt(par2, out pright, out vright);
            if (vleft.x < 0) return max;
            if (vright.x > 0) return max; // sollte es nicht geben
            int counter = 0;
            do
            {
                ++counter;
                GeoPoint2D pmiddle;
                GeoVector2D vmiddle;
                double parm = (par1 + par2) / 2.0;
                PointDirAt(parm, out pmiddle, out vmiddle);
                if (vmiddle.x > 0.0) par1 = parm;
                else par2 = parm;
                if (Math.Abs(par2 - par1) < parameterEpsilon || counter > 30) return pmiddle.x;
            }
            while (true);
        }
        private double FindHMin(double par1, double par2, double min)
        {   // vertikale Tangente suchen
            GeoPoint2D pleft, pright;
            GeoVector2D vleft, vright;
            PointDirAt(par1, out pleft, out vleft);
            PointDirAt(par2, out pright, out vright);
            if (vleft.x > 0) return min;
            if (vright.x < 0) return min; // sollte es nicht geben
            int counter = 0;
            do
            {
                ++counter;
                GeoPoint2D pmiddle;
                GeoVector2D vmiddle;
                double parm = (par1 + par2) / 2.0;
                PointDirAt(parm, out pmiddle, out vmiddle);
                if (vmiddle.x < 0.0) par1 = parm;
                else par2 = parm;
                if (Math.Abs(par2 - par1) < parameterEpsilon || counter > 30) return pmiddle.x;
            }
            while (true);
        }

        private bool TringleHitTest(ref ClipRect clr, GeoPoint2D p1, GeoPoint2D pm, GeoPoint2D p2, GeoVector2D v1, GeoVector2D v2, double sp, double ep)
        {   // überprüft ob clr die Kurve in dem gegebenen Dreieck berührt
            if (Precision.IsEqual(p1, p2)) return false; // eigentlich sollten keine doppelten punkte vorkommen, das muss noch überprüft werden
            bool hitBasis = clr.LineHitTest(p1, p2);
            bool hitSide1 = clr.LineHitTest(p1, pm);
            bool hitSide2 = clr.LineHitTest(pm, p2);
            // wenn die Basis und eine Seite berührt wurde, dann wird auch die Kurve berührt
            if (hitBasis && (hitSide1 || hitSide2)) return true;
            if (!hitBasis && !hitSide1 && !hitSide2)
            {
                // wenn keine Seite berührt wurde, dann ist die Frage Rechteck drin oder nicht?
                // wenn man auf den Seiten um das Dreieck geht und der Mittelpunkt des rechtecks ist immer links oder immer
                // rechts, dann liegt das Rechteck im Dreieck
                // wenn einer der beiden Vektoren der Nullvektor ist, dann nähern wir uns hier einer singulären Stelle
                // und die gilt (vorläufig) als nicht getroffen, wenn nicht die Basislinie und eine der Dreiecksseiten
                // getroffen wurde
                if (Precision.IsNullVector(v1) || Precision.IsNullVector(v2)) return false;
                GeoPoint2D center = clr.Center;
                bool l1 = Geometry.OnLeftSide(center, p1, v1);
                bool l2 = Geometry.OnLeftSide(center, pm, v2);
                if (l1 != l2) return false; // garantiert draußen
                bool l3 = Geometry.OnLeftSide(center, p2, p1 - p2);
                if (l3 != l1) return false; // garantiert draußen
                // der Punkt liegt bezüglich der Dreieckseiten immer auf der selben Seite: also ist er drinnen
                // dass das Rechteck ganz im Dreieck liegt bedeutet aber nicht, dass es auch die Kurve berührt
                // es gibt insbesondere diese klitzekleinen Rechtecke, die bei der Border dazu verwendet werden
                // zu testen ob ein Punkt auf der Kurve liegt, und die sind oft im Dreieck, aber nicht auf der Kurve.
                // also nicht: return true;
            }
            // Hier ist noch offen ob berührt oder nicht, denn die Basislinie wurde nicht getroffen, wohl aber eine Seite,
            // oder das Rechteck liegt ganz im Dreieck.
            // Also Dreieck unterteilen:
            double mp = (sp + ep) / 2.0;
            GeoPoint2D pm1, pm2, pmm;
            GeoVector2D vm;
            try
            {
                SubTriangle(p1, p2, v1, v2, mp, out pm1, out pm2, out pmm, out vm);
                if (TringleHitTest(ref clr, p1, pm1, pmm, v1, vm, sp, mp)) return true;
                if (TringleHitTest(ref clr, pmm, pm2, p2, vm, v2, mp, ep)) return true;
            }
            catch (BSplineException) { }
            return false;
        }

        private void SubTriangle(GeoPoint2D p1, GeoPoint2D p2, GeoVector2D v1, GeoVector2D v2, double mp, out GeoPoint2D pm1, out GeoPoint2D pm2, out GeoPoint2D pmm, out GeoVector2D vm)
        {
            PointDirAt(mp, out pmm, out vm);
            if (!Geometry.IntersectLL(p1, v1, pmm, vm, out pm1)) throw new BSplineException("internal");
            if (!Geometry.IntersectLL(pmm, vm, p2, v2, out pm2)) throw new BSplineException("internal");
        }

        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected BSpline2D(SerializationInfo info, StreamingContext context)
        {
            poles = (GeoPoint2D[])info.GetValue("Poles", typeof(GeoPoint2D[]));
            weights = (double[])info.GetValue("Weights", typeof(double[]));
            knots = (double[])info.GetValue("Knots", typeof(double[]));
            multiplicities = (int[])info.GetValue("Multiplicities", typeof(int[]));
            degree = (int)info.GetValue("Degree", typeof(int));
            periodic = (bool)info.GetValue("Periodic", typeof(bool));
            startParam = (double)info.GetValue("StartParam", typeof(double));
            endParam = (double)info.GetValue("EndParam", typeof(double));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Poles", poles, typeof(GeoPoint2D[]));
            info.AddValue("Weights", weights, typeof(double[]));
            info.AddValue("Knots", knots, typeof(double[]));
            info.AddValue("Multiplicities", multiplicities, typeof(int[]));
            info.AddValue("Degree", degree);
            info.AddValue("Periodic", periodic);
            info.AddValue("StartParam", startParam);
            info.AddValue("EndParam", endParam);
        }

        #endregion
        public bool GetSimpleCurve(double precison, out ICurve2D simpleCurve)
        {
            if (degree == 1)
            {
                simpleCurve = new Line2D(this.StartPoint, this.EndPoint);
                return true;
            }
            // alle Pole auf einer Linie
            if (precison == 0.0) precison = Precision.eps;
            GeoPoint2D sp = this.StartPoint;
            GeoPoint2D ep = this.EndPoint;
            if ((sp | ep) > precison)
            {
                bool linear = true;
                // alle inneren Pole testen, der erste und letzte sind ja start- und endpunkt
                for (int i = 1; i < poles.Length - 1; i++)
                {
                    if (Math.Abs(Geometry.DistPL(poles[i], sp, ep)) > precison)
                    {
                        linear = false;
                        break;
                    }
                }
                if (linear)
                {
                    simpleCurve = new Line2D(this.StartPoint, this.EndPoint);
                    return true;
                }
            }
            GeoPoint2D middlePoint = PointAt(0.5);
            double d1 = StartPoint | middlePoint;
            double d2 = EndPoint | middlePoint;
            if (Math.Min(d1, d2) / Math.Max(d1, d2) > 0.5)
            {   // Problemfall war ein Spline, der alle Pole an einem Ende sehr nahe beieinander hat und den letzten Pol weit weg.
                // daraus wurde ein Kreisbogen. Deshalb testen, ob der Mittelpunkt auch ungefähr in der Mitte liegt
                Arc2D a2d = Arc2D.From3Points(StartPoint, middlePoint, EndPoint);
                if (a2d != null)
                {
                    bool ok = true;
                    if (a2d.Length > 2 * ((StartPoint | middlePoint) + (middlePoint | EndPoint))) ok = false; // der Kreisbogen kann nicht länger sein als 2* die Länge der Segmente (gilt auch bei Vollkreis)
                    if (Math.Abs(a2d.Sweep) < Math.PI / 10) ok = false; // ein Kreisbogen sollte größer als 10 Grad sein, sonst sind die ungenauigkeiten zu groß
                    if (ok) for (int i = 0; i < 10; i++)
                        {
                            if (a2d.Distance(PointAt(i / 10.0)) > precison) ok = false;
                        }
                    if (ok)
                    {
                        simpleCurve = a2d;
                        return true;
                    }
                }

                if (Precision.Equals(StartPoint, EndPoint))
                {
                    a2d = Arc2D.From3Points(StartPoint, PointAt(1.0 / 3.0), PointAt(2.0 / 3.0));
                    if (a2d != null)
                    {
                        bool ok = true;
                        Circle2D c2d = new Circle2D(a2d.Center, a2d.Radius);
                        for (int i = 0; i < 10; i++)
                        {
                            if (c2d.Distance(PointAt(i / 10.0)) > precison) ok = false;
                        }
                        if (ok)
                        {
                            simpleCurve = c2d;
                            return true;
                        }
                    }
                }
            }

            simpleCurve = null;
            return false;
        }
        //public ICurve2D IsPartOf(double precision, ICurve2D simpleCurve)
        //{
        //    if (simpleCurve.MinDistance(this.StartPoint) > precision) return null;
        //    if (simpleCurve.MinDistance(this.EndPoint) > precision) return null;
        //    if (tringulation == null) MakeTriangulation();
        //    for (int i = 0; i < interpol.Length; i++)
        //    {
        //        if (simpleCurve.MinDistance(interpol[i]) > precision) return null;
        //    }
        //    double ps = simpleCurve.PositionOf(this.StartPoint);
        //    double pe = simpleCurve.PositionOf(this.EndPoint);
        //    if (ps < pe)
        //    {
        //        return simpleCurve.Trim(ps, pe);
        //    }
        //    else
        //    {
        //        ICurve2D res = simpleCurve.Trim(pe, ps);
        //        res.Reverse();
        //        return res;
        //    }
        //}
        public override string ToString()
        {
            return "BSpline2D: (" + StartPoint.DebugString + ") (" + EndPoint.DebugString + ")";
        }
        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            Init();
        }

        #endregion
        #region IDebuggerVisualizer Members

        GeoObjectList IDebuggerVisualizer.GetList()
        {
            GeoObjectList res = new GeoObjectList();
            res.Add(MakeGeoObject(Plane.XYPlane));
            //if (interpol != null)
            //{
            //    ColorDef red = new ColorDef("red", Color.Red);
            //    ColorDef blue = new ColorDef("blue", Color.Blue);
            //    for (int i = 0; i < interpol.Length - 1; ++i)
            //    {
            //        Line l1 = Line.Construct();
            //        l1.StartPoint = new GeoPoint(interpol[i]);
            //        l1.EndPoint = new GeoPoint(interpol[i + 1]);
            //        l1.ColorDef = red;
            //        res.Add(l1);
            //        Line l2 = Line.Construct();
            //        l2.StartPoint = new GeoPoint(interpol[i]);
            //        l2.EndPoint = new GeoPoint(tringulation[i]);
            //        l2.ColorDef = blue;
            //        res.Add(l2);
            //        Line l3 = Line.Construct();
            //        l3.StartPoint = new GeoPoint(interpol[i + 1]);
            //        l3.EndPoint = new GeoPoint(tringulation[i]);
            //        l3.ColorDef = blue;
            //        res.Add(l3);
            //    }
            //}
            return res;
        }
#if DEBUG
        public GeoObjectList DEbugList
        {
            get
            {
                GeoObjectList res = new GeoObjectList(this.MakeGeoObject(Plane.XYPlane));
                return res;
            }
        }
#endif
        #endregion
        internal void reparametrize(double s, double e)
        {
            double f = (e - s) / (knots[knots.Length - 1] - knots[0]);
            double k0 = knots[0];
            for (int i = 0; i < knots.Length; i++)
            {
                knots[i] = s + (knots[i] - k0) * f;
            }
            Init();
        }
        internal bool findTangentNewton(double startPos, GeoVector2D normal, out double result)
        {   // suche Punkt auf der Kurve, wo die Kurve senkrecht zu normal ist
            // konvertiert gut, wenn in der Nähe vom Ergbenis, sonst kanns umherfliegen
            result = startPos;
            //reparametrize(-10, 1);
            //result = -5;
            GeoPoint2DH pos, pd1, pd2;
            nurbs.CurveDeriv2(result, out pos, out pd1, out pd2);
            GeoVector2D d1 = new GeoVector2D(pd1.x, pd1.y);
            GeoVector2D d2 = new GeoVector2D(pd2.x, pd2.y);
            double err = d1 * normal;
            for (int k = 0; k < 20; k++)
            {
                if (nurbs != null)
                {
                    // f(u) = u²*d22+u*d1+pos ist das angenäherte quadratische polynom (d22==1/2*d2)
                    // f'(u) = u*d2+d1
                    // f'(u)*normal==0
                    // u*d2*normal = -d1*normal
                    double sp = d1 * normal; // ist 0, wenn senkrecht
                    double u = -(d1 * normal) / (d2 * normal);
                    double te;
                    do
                    {   // falls das Ergebnis schlecht ist, dann den Schritt solange halbieren, bis das Ergebnis besser wird
                        nurbs.CurveDeriv2(result + u, out pos, out pd1, out pd2);
                        d1 = new GeoVector2D(pd1.x, pd1.y);
                        d2 = new GeoVector2D(pd2.x, pd2.y);
                        te = d1 * normal;
                        if (Math.Abs(te) < Math.Abs(err)) break;
                        u /= 2.0;
                    } while (Math.Abs(u) > 1e-10);
                    err = te;
                    result += u;
                }
            }
            return true;
        }
        ExplicitPCurve2D IExplicitPCurve2D.GetExplicitPCurve2D()
        {
            if (explicitPCurve2D == null || !explicitPCurve2D.TryGetTarget(out ExplicitPCurve2D res))
            {
                if (nurbs != null)
                {
                    Polynom[] px = new Polynom[knots.Length - 1];
                    Polynom[] py = new Polynom[knots.Length - 1];
                    Polynom[] pz = new Polynom[knots.Length - 1];
                    Polynom[] pw = new Polynom[knots.Length - 1];
                    for (int i = 0; i < knots.Length - 1; i++)
                    {
                        Polynom[] pn = nurbs.CurvePointPolynom((knots[i] + knots[i + 1]) / 2.0);
                        // das sind 4 Polynome, px,py,pz,pw
                        px[i] = pn[0];
                        py[i] = pn[1];
                        pw[i] = pn[2];
                    }
                    bool isRational = false;
                    for (int i = 0; i < weights.Length; i++)
                    {
                        if (weights[i] != 1.0)
                        {
                            isRational = true;
                            break;
                        }
                    }
                    if (isRational) res = new ExplicitPCurve2D(px, py, pw, knots);
                    else res = new ExplicitPCurve2D(px, py, null, knots);
                    explicitPCurve2D = new WeakReference<ExplicitPCurve2D>(res);
                    return res;
                }
                if (nubs != null)
                {
                    Polynom[] px = new Polynom[knots.Length - 1];
                    Polynom[] py = new Polynom[knots.Length - 1];
                    for (int i = 0; i < knots.Length - 1; i++)
                    {
                        Polynom[] pn = nubs.CurvePointPolynom((knots[i] + knots[i + 1]) / 2.0);
                        // das sind 4 Polynome, px,py,pz,pw
                        px[i] = pn[0];
                        py[i] = pn[1];
                    }
                    res = new ExplicitPCurve2D(px, py, null, knots);
                    explicitPCurve2D = new WeakReference<ExplicitPCurve2D>(res);
                    return res;
                }
            }
            return null;
        }

        //protected override void GetTriangulationBasis(out GeoPoint2D[] points, out GeoVector2D[] directions, out double[] parameters)
        //{
        //    // u/ (endParam - startParam)- startParam = Position ;
        //    parameters = new double[knots.Length];
        //    points = new GeoPoint2D[knots.Length];
        //    directions = new GeoVector2D[knots.Length];
        //    for (int i = 0; i < knots.Length; i++)
        //    {
        //        parameters[i] = knots[i] / (endParam - startParam) - startParam;
        //        directions[i] = DirectionAtParam(knots[i]);
        //        points[i] = PointAtParam(knots[i]);
        //    }
        //}

        internal bool WeirdPoles
        {
            get
            {
                for (int i = 1; i < Poles.Length - 1; i++)
                {
                    Angle a = new Angle(poles[i] - poles[i - 1], poles[i + 1] - poles[i]);
                    if (Math.Abs(a.Radian - Math.PI) < 0.2) return true; // almost opposite direction
                }
                return false;
            }
        }
    }
}
