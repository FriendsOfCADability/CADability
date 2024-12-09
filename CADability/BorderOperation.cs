using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;

namespace CADability.Shapes
{
    /* NEUES KONZEPT (noch nicht implementiert, 20.7.15)
     * --- es geht um einfache Borders, nicht um SimpleShapes, d.h. es gibt keine Löcher, alles ist linksrum orientiert ---
     * 
     * Mache einen Quadtree über beide Borders.
     * Teile den Quadtree soweit auf, dass in jeder Liste nur ein Schnittpunkt liegt. Wenn Schnittpunkt näher als "recision" zusammenfallen,
     * dann als nur einen Schnittpunkt betrachten.
     * Jedes Quadrat (also Blatt des Quadtrees) hat bezüglich einer Border nur 2 Schnittpunkte, bzw. es ist ganz innerhalb oder ganz außerhalb.
     * Diese Schnittpunkte muss man bestimmen (Parameter auf der Quadratseite und Parameter auf dem Border)
     * Je nach boolscher Operation sollte es jetzt ganz einfach sein: jedes Quadrat liefert ein Interval (border1/2, von bis) oder 2 Intervalle,
     * wenn ein Schnittpunkt enthalten ist. (Wenn kein Schnittpunkt enthalten ist, dann soll das Quadrat auch nur ein Border enthalten.
     * (Probleme sind Selbstüberschneidungen/Berührungen: wenn alle 4 Eckpunkte innerhalb sind, dann kann man das Intervall vergessen).
     * Segmente, die identisch sind, werden wie Schnittpunkte behandelt, mit einer besonderen Kennung.
     * Zum Schluss werden die Intervalle aufgesammelt, zusammenhängende Stücke einer Border sind einfach zusammenfügbar. Hier können mehrere Umrandungen
     * und auch Löcher entstehen. Auch die Richtungen sind klar: bei Vereinigung bleiben die Richtungen erhalten, bei Differenz dreht sich die Richtung 
     * der rechten Seite um.
     * 
     * */

    internal class BorderOperation
    {
        private struct PointPosition : IComparable
        {
            public PointPosition(double par, GeoPoint2D point, double oppositePar, int id, double cross)
            {
                this.id = id;
                this.par = par;
                this.point = point;
                this.index = -1;
                this.oppositePar = oppositePar;
                this.direction = Direction.Unknown;
                this.used = false;
                this.cross = cross;
            }
            public int id; // zwei mit der gleichen id gehören zum selben Schnittpunkt
            public double par; // Positionsparameter auf dem Border von 0 bis Segmentanzahl, ganzzahlig bei Ecken
            public GeoPoint2D point; // der Punkt selbst
            public double oppositePar; // Positionsparameter auf der anderen Border (nur wenn auf dem Rand)
            public int index; // der Index in der anderen Liste
            public bool used; // schon verbraucht, nicht mehr verwenden
            public enum Direction { Entering, Leaving, Crossing, Ambigous, Ignore, Unknown } // Crossing ist für offene Border
            public Direction direction;
            public double cross;
            public PointPosition Decremented()
            {
                PointPosition res = new PointPosition();
                res.id = id;
                res.par = par;
                res.point = point;
                res.oppositePar = oppositePar;
                res.index = index - 1;
                res.used = used;
                res.direction = direction;
                return res;
            }
            #region IComparable Members
            public int CompareTo(object obj)
            {
                PointPosition ct = (PointPosition)obj;
                return par.CompareTo(ct.par);
            }
            #endregion
        }
        private double precision; // kleiner Wert, Punkte mit noch kleinerem Abstand werden als identisch betrachtet
        private BoundingRect extent; // gemeinsame Ausdehnung beider Borders
        private bool intersect;
        private Border border1; // erster Operand
        private Border border2; // zweiter Operand
        private PointPosition[] border1Points; // Liste von relevanten Punkten auf B1
        private PointPosition[] border2Points; // Liste von relevanten Punkten auf B2
        private void Refine(List<PointPosition> points, Border pointsborder, Border other)
        {
            // doppelte entfernen. Die Frage nach der Genauigkeit erhebt sich hier
            // zunächstmal fix mit 1e-6 implementiert
            // zusätzlicher Aufwand, wenn Ende und Anfang übereinstimmen
            // weggelassen, da vor dem Aufruf geregelt
            //for (int i=points.Count-1; i>0; --i)
            //{
            //    if (Math.Abs((points[i - 1]).par - (points[i]).par) < 1e-6 ||
            //        Math.Abs(pointsborder.Count - Math.Abs((points[i - 1]).par - (points[i]).par)) < 1e-6)
            //    {
            //        if (Math.Abs((points[i - 1]).oppositePar - (points[i]).oppositePar) < 1e-6 ||
            //            Math.Abs(other.Count - Math.Abs((points[i - 1]).oppositePar - (points[i]).oppositePar)) < 1e-6)
            //        {
            //            points.RemoveAt(i - 1);
            //        }
            //    }
            //}
            // noch den letzten und den ersten checken
            //int last = points.Count - 1;
            //if (last > 0)
            //{
            //    if (Math.Abs((points[last]).par - (points[0]).par) < 1e-6 ||
            //        Math.Abs(pointsborder.Count - Math.Abs((points[last]).par - (points[0]).par)) < 1e-6)
            //    {
            //        if (Math.Abs((points[last]).oppositePar - (points[0]).oppositePar) < 1e-6 ||
            //            Math.Abs(other.Count - Math.Abs((points[last]).oppositePar - (points[0]).oppositePar)) < 1e-6)
            //        {
            //            points.RemoveAt(last);
            //        }
            //    }
            //}
            PointPosition[] pointsa = (PointPosition[])points.ToArray();
            // die Punkte auf pointsa werden hinsichtlich 
            // ihres Verhaltens bezüglich der anderen Kurve charakterisiert: tritt
            // die jeweilige Border in die andere ein, oder verlässt sie diese.
            // Problemfall: Berührung über eine Strecke: zunächst wird der Eintrittspunkt in eine Berührung auf Ignore gesetzt
            // in der darauffolgenden Schleife breitet sich das ignore nach hinten aus, wenn davor und danach gleiche Wete sind.
            // Vermutlich wird es ein Problem geben, wenn zwei Border ein gemeinsames Segment haben, aber dennoch eine echte Überschneidung
            // stattfindet. So einen Fall muss man mal konstruieren. Denn wir lassen jeweils den Austrittspunkt gelten und die beiden
            // Punktlisten werden in verschiedener Richtung durchlaufen, also die Punkte passen dann  nicht zusammen. Man sollte dann den
            // Mittelpunkt nehmen.
            // Problemfall: Berührung mit nur einem Eckpunkt wird eliminiert
            if (pointsa.Length > 1)
            {
                double par = (pointsa[pointsa.Length - 1].par + pointsborder.Count + pointsa[0].par) / 2.0;
                if (par > pointsborder.Count) par -= pointsborder.Count;
                Border.Position lastpos = other.GetPosition(pointsborder.PointAt(par), precision);
                if (lastpos == Border.Position.OnCurve)
                {
                    double par1 = pointsa[pointsa.Length - 1].par + 0.324625 * (pointsa[0].par + pointsborder.Count - pointsa[pointsa.Length - 1].par);
                    double par2 = pointsa[pointsa.Length - 1].par + 0.689382 * (pointsa[0].par + pointsborder.Count - pointsa[pointsa.Length - 1].par);
                    if (par1 > pointsborder.Count) par1 -= pointsborder.Count;
                    if (par2 > pointsborder.Count) par2 -= pointsborder.Count;
                    Border.Position newpos1 = other.GetPosition(pointsborder.PointAt(par1), precision);
                    Border.Position newpos2 = other.GetPosition(pointsborder.PointAt(par2), precision);
                    if (newpos1 != lastpos) lastpos = newpos1;
                    else if (newpos2 != lastpos) lastpos = newpos2;
                }
                Border.Position firstpos = lastpos;

                for (int i = 0; i < pointsa.Length; ++i)
                {
                    Border.Position newpos;
                    if (i < pointsa.Length - 1)
                    {
                        par = (pointsa[i].par + pointsa[i + 1].par) / 2.0;
                        newpos = other.GetPosition(pointsborder.PointAt(par), precision);
                        if (newpos == Border.Position.OnCurve)
                        {   // zur Sicherheit zwei weiterePunkte profen. Wenn einer nicht auf OnCurve ist, dann das nehmen. Genau die Mitte kann ein Artefakt liefern
                            double par1 = pointsa[i].par + 0.324625 * (pointsa[i + 1].par - pointsa[i].par);
                            double par2 = pointsa[i].par + 0.689382 * (pointsa[i + 1].par - pointsa[i].par);
                            Border.Position newpos1 = other.GetPosition(pointsborder.PointAt(par1), precision);
                            Border.Position newpos2 = other.GetPosition(pointsborder.PointAt(par2), precision);
                            if (newpos1 != newpos) newpos = newpos1;
                            else if (newpos2 != newpos) newpos = newpos2;
                        }
                    }
                    else
                    {
                        newpos = firstpos;
                    }
                    // neu eingeführt: Positionsbestimmung anhand der Richtung des Schnittes
                    // epsilon noch willkürlich
                    if (pointsa[i].cross > 0.01 && other.IsClosed)
                    {
                        pointsa[i].direction = PointPosition.Direction.Leaving;
                    }
                    else if (pointsa[i].cross < -0.01 && other.IsClosed)
                    {
                        pointsa[i].direction = PointPosition.Direction.Entering;
                    }
                    else if (newpos == Border.Position.OnCurve && lastpos == Border.Position.OnCurve)
                    {
                        pointsa[i].direction = PointPosition.Direction.Ignore;
                    }
                    else if (newpos == Border.Position.OpenBorder)
                    {
                        pointsa[i].direction = PointPosition.Direction.Crossing;
                    }
                    else if (newpos == lastpos)
                    {
                        pointsa[i].direction = PointPosition.Direction.Ignore;
                    }
                    else if (newpos == Border.Position.OnCurve)
                    {
                        // Bei Schraffur mit Inseln werden oft genau passende puzzleteile abgezogen
                        // die in einzelnen Abschnitten übereinstimmen. Diese dürfen nicht 
                        // als zugehörig betrachtete werden. Siehe z.B. Schraffur2.cdb
                        // pointsa[i].direction = PointPosition.Direction.Ignore;
                        if (lastpos == Border.Position.Outside)
                            pointsa[i].direction = PointPosition.Direction.Entering;
                        else
                            pointsa[i].direction = PointPosition.Direction.Leaving;
                    }
                    else if (newpos == Border.Position.Inside)
                    {
                        pointsa[i].direction = PointPosition.Direction.Entering;
                    }
                    else
                    {
                        pointsa[i].direction = PointPosition.Direction.Leaving;
                    }
                    lastpos = newpos;
                }
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int i = 0; i < pointsa.Length; ++i)
                {
                    dc.Add(pointsa[i].point, System.Drawing.Color.DarkBlue, pointsa[i].direction.ToString());
                }
#endif
                // in folgender Schleife werden zwei gleiche, die nur von ignore getrennt sind auf einen reduziert
                if (pointsborder != border1) // testweise mal nur auf den 2. Durchlauf beschränkt
                {
                    bool ignoring;
                    do
                    {
                        ignoring = false;
                        PointPosition.Direction lastdir = PointPosition.Direction.Ignore;
                        // den letzten Wert als Startwert verwenden:
                        for (int i = pointsa.Length - 1; i >= 0; --i)
                        {
                            if (pointsa[i].direction != PointPosition.Direction.Ignore)
                            {
                                lastdir = pointsa[i].direction;
                                break;
                            }
                        }
                        for (int i = 0; i < pointsa.Length; ++i)
                        {
                            if (lastdir != PointPosition.Direction.Ignore && pointsa[i].direction == PointPosition.Direction.Ignore)
                            {
                                int next = i + 1;
                                if (next >= pointsa.Length) next = 0;
                                if (pointsa[next].direction != PointPosition.Direction.Ignore)
                                {
                                    if (pointsa[next].direction == lastdir)
                                    {
                                        pointsa[next].direction = PointPosition.Direction.Ignore;
                                        ignoring = true;
                                    }
                                }
                            }
                            if (pointsa[i].direction != PointPosition.Direction.Ignore)
                            {
                                lastdir = pointsa[i].direction;
                            }
                        }
                    } while (ignoring);
                }
#if DEBUG
                DebuggerContainer dc1 = new DebuggerContainer();
                for (int i = 0; i < pointsa.Length; ++i)
                {
                    dc1.Add(pointsa[i].point, System.Drawing.Color.DarkBlue, pointsa[i].direction.ToString());
                }
#endif
                points.Clear();
                for (int i = 0; i < pointsa.Length; ++i)
                {
                    if (pointsa[i].direction == PointPosition.Direction.Leaving ||
                        pointsa[i].direction == PointPosition.Direction.Entering ||
                        pointsa[i].direction == PointPosition.Direction.Crossing)
                    {
                        int iminus1 = i - 1;
                        if (iminus1 < 0) iminus1 = pointsa.Length - 1;
                        // nur solche nehmen, wo wirklich ein Wechsel ist
                        if ((pointsa[iminus1].direction != pointsa[i].direction) ||
                            (pointsa[iminus1].direction == PointPosition.Direction.Crossing && pointsa[i].direction == PointPosition.Direction.Crossing))
                        {
                            points.Add(pointsa[i]);
                        }
                    }
                }
#if DEBUG
                DebuggerContainer dc2 = new DebuggerContainer();
                for (int i = 0; i < points.Count; ++i)
                {
                    dc2.Add(points[i].point, System.Drawing.Color.DarkBlue, points[i].direction.ToString());
                }
#endif
            }
            else
            {
                points.Clear(); // nur ein einziger Punkt, der gilt sowieso nicht, oder
                intersect = false; // jedenfalls kein Fall bekannt, wo sowas auftritt
            }
        }
        private void GenerateClusterSet()
        {
            // zuerst die beiden Listen mit den Eckpunkten füttern. wozu? abgeschafft!
            List<PointPosition> b1p = new List<PointPosition>();
            List<PointPosition> b2p = new List<PointPosition>();
#if DEBUG
            debugb1p = b1p;
            debugb2p = b2p;
#endif
            // warum sollten wir die Eckpunkte dazunehmen???
            //			for (int i=0; i<border1.Count; ++i)
            //			{
            //				GeoPoint2D p = border1[i].StartPoint;
            //				b1p.Add(new PointPosition(i,p,border2.GetPosition(p,precision)));
            //			}
            //			for (int i=0; i<border2.Count; ++i)
            //			{
            //				GeoPoint2D p = border2[i].StartPoint;
            //				b2p.Add(new PointPosition(i,p,border1.GetPosition(p,precision)));
            //			}
            // jetzt noch die Schnittpunkt hinzunehmen:
            // es würde Sinn machen über die Border mit den wenigeren Segmenten zu iterieren
            intersect = false;
            GeoPoint2DWithParameter[] isp = border1.GetIntersectionPoints(border2, precision);
            for (int j = 0; j < isp.Length; ++j)
            {
                double dirz;
                try
                {
                    GeoVector2D dir1 = border1.DirectionAt(isp[j].par1).Normalized;
                    GeoVector2D dir2 = border2.DirectionAt(isp[j].par2).Normalized;
                    dirz = dir1.x * dir2.y - dir1.y * dir2.x;
                }
                catch (GeoVectorException)
                {
                    dirz = 0.0;
                }
                // Am Eckpunkt ist das dirz nicht aussagekräftig, also 0.0 setzen, dann kommt anderer Mechanismus dran
                if (Math.Abs(Math.Round(isp[j].par2) - isp[j].par2) < 1e-7) dirz = 0.0;
                if (Math.Abs(Math.Round(isp[j].par1) - isp[j].par1) < 1e-7) dirz = 0.0;
                b1p.Add(new PointPosition(isp[j].par1, isp[j].p, isp[j].par2, j, dirz));
                b2p.Add(new PointPosition(isp[j].par2, isp[j].p, isp[j].par1, j, -dirz));
                intersect = true;
            }
            b1p.Sort(); // sortiert nach aufsteigendem Parameter
            b2p.Sort();
            // zu eng liegende entfernen, und zwar aus beiden Listen gleichermaßen
            // es dürfte eigentlich genügen über eine Liste zu iterieren
            for (int i = b1p.Count - 1; i > 0; --i)
            {
                if (Math.Abs((b1p[i - 1]).par - (b1p[i]).par) < 1e-6 ||
                    Math.Abs(border1.Count - Math.Abs((b1p[i - 1]).par - (b1p[i]).par)) < 1e-6)
                {
                    if (Math.Abs((b1p[i - 1]).oppositePar - (b1p[i]).oppositePar) < 1e-6 ||
                        Math.Abs(border2.Count - Math.Abs((b1p[i - 1]).oppositePar - (b1p[i]).oppositePar)) < 1e-6)
                    {
                        int id = b1p[i - 1].id;
                        b1p.RemoveAt(i - 1);
                        for (int j = 0; j < b2p.Count; j++)
                        {
                            if (b2p[j].id == id)
                            {
                                b2p.RemoveAt(j);
                                break;
                            }
                        }
                    }
                }
            }
            // und noch am Ende checken
            int last = b1p.Count - 1;
            if (last > 0)
            {
                if (Math.Abs((b1p[last]).par - (b1p[0]).par) < 1e-6 ||
                    Math.Abs(border1.Count - Math.Abs((b1p[last]).par - (b1p[0]).par)) < 1e-6)
                {
                    if (Math.Abs((b1p[last]).oppositePar - (b1p[0]).oppositePar) < 1e-6 ||
                        Math.Abs(border2.Count - Math.Abs((b1p[last]).oppositePar - (b1p[0]).oppositePar)) < 1e-6)
                    {
                        int id = b1p[last].id;
                        b1p.RemoveAt(last);
                        for (int j = 0; j < b2p.Count; j++)
                        {
                            if (b2p[j].id == id)
                            {
                                b2p.RemoveAt(j);
                                break;
                            }
                        }
                    }
                }
            }

            Refine(b1p, border1, border2);
            // Alle, die aus b1 entfernt wurden, müssen auch aus b2 entfernt werden
            // das stand früher nach Refine(b2p... aber das hatte folgenden Nachteil:
            // z.B. zwei horizontal leicht versetzte horizontale Rechtecke haben 4 Schnittpunkte, wobei jeweils zweimal
            // Entering und zweimal Leaving kommt. Beim ersten Refine wird nur das nebeneinanderliegende Paar Entering->Leaving belassen
            // das liegt aber in der Punktliste b2p nicht aufeinanderfolgend und dann wird dort das andere Paar beibehalten, so dass
            // insgesamt nicht übrig bleibt. Deshalb also gleich entfernen.
            for (int i = b2p.Count - 1; i >= 0; --i)
            {
                bool ok = false;
                for (int j = 0; j < b1p.Count; ++j)
                {
                    if (b1p[j].id == b2p[i].id)
                    {
                        ok = true;
                        break;
                    }
                }
                if (!ok) b2p.RemoveAt(i);
            }
            Refine(b2p, border2, border1);
            for (int i = 0; i < b1p.Count; ++i)
            {
                for (int j = 0; j < b2p.Count; ++j)
                {   // den folgenden Vergleich von == auf <1e-10 geändert, da offensichtlich kleinste Unterschiede auftreten
                    // die Unterschiede kommen, weil fast identische entfernt wurden, 
                    // aber es wurden nicht in beiden Listen die gleichen entfernt
                    // hier müsste es aber jetzt genügen nach der id zu gucken, oder?
                    //if ((Math.Abs((b1p[i]).par-(b2p[j]).oppositePar)<1e-10) &&
                    //    (Math.Abs((b1p[i]).oppositePar-(b2p[j]).par)<1e-10))
                    if (b1p[i].id == b2p[j].id)
                    {
                        // da PointPosition ein struct, hier umständlich:
                        PointPosition pp1 = b1p[i];
                        PointPosition pp2 = b2p[j];
                        pp1.index = j;
                        pp2.index = i;
                        b1p[i] = pp1;
                        b2p[j] = pp2;
                    }
                }
                if (b1p[i].index == -1)
                {
                    b1p.RemoveAt(i); // keinen Partner gefunden, weg damit
                    --i;
                }
            }
            // noch folgenden Sonderfall berücksichtigen:
            // wenn in einer Liste zweimal Entering und die dazugehörigen zweimal Leaving sind
            // dann einen der beiden löschen, denn es handelt sich um ein gemeinsames Konturstück
            bool removed = true;
            while (removed)
            {
                removed = false;
                for (int i = 1; i < b1p.Count; i++)
                {
                    if (b1p[i - 1].direction == b1p[i].direction)
                    {
                        int i1 = b1p[i - 1].index;
                        int i2 = b1p[i].index;
                        if (Math.Abs(i2 - i1) == 1 && b2p[i1].direction == b2p[i2].direction)
                        {
                            // Bedingung erfüllt: i in List b1p löschen
                            int r1 = i;
                            int r2 = b1p[i].index;
                            b2p.RemoveAt(r2);
                            b1p.RemoveAt(r1);
                            for (int j = 0; j < b1p.Count; j++)
                            {
                                if (b1p[j].index > r2) b1p[j] = b1p[j].Decremented(); // wg. blöden struct
                            }
                            for (int j = 0; j < b2p.Count; j++)
                            {
                                if (b2p[j].index > r1) b2p[j] = b2p[j].Decremented();
                            }
                            removed = true;
                            break;
                        }
                    }
                }
            }
            // eigentlich sollte jetzt kein Eintrag mit index == -1 mehr vorkommen
            // und das rauslöschen ist ein bisschen blöd, weil die Indizes schon vergeben sind
            // es würde aber gehen. Hier erstmal eine exception werfen:
            for (int i = 0; i < b1p.Count; ++i)
            {
                if ((b1p[i]).index == -1) throw new BorderException("internal error in GenerateClusterSet", BorderException.BorderExceptionType.InternalError);
            }
            for (int i = 0; i < b2p.Count; ++i)
            {
                if ((b2p[i]).index == -1) throw new BorderException("internal error in GenerateClusterSet", BorderException.BorderExceptionType.InternalError);
            }
            border1Points = b1p.ToArray();
            border2Points = b2p.ToArray();
            borderPosition = BorderPosition.unknown;
        }
        public BorderOperation(Border b1, Border b2)
        {
            border1 = b1;
            border2 = b2;
            extent = border1.Extent;
            extent.MinMax(border2.Extent);
            precision = (extent.Width + extent.Height) * 1e-6; // 1e-8 war zu scharf!
            try
            {
                GenerateClusterSet();
            }
            catch (BorderException)
            {
                intersect = false;
                border1 = null;
                border2 = null;
            }
        }
        public BorderOperation(Border b1, Border b2, double prec)
        {
            border1 = b1;
            border2 = b2;
            extent = border1.Extent;
            extent.MinMax(border2.Extent);
            precision = prec;
            try
            {
                GenerateClusterSet();
            }
            catch (BorderException)
            {
                intersect = false;
                border1 = null;
                border2 = null;
            }
        }
        private int FindNextPoint(bool onB1, int startHere, PointPosition.Direction searchFor, bool forward)
        {
            PointPosition[] borderPoints;
            if (onB1) borderPoints = border1Points;
            else borderPoints = border2Points;
            if (borderPoints.Length == 0) return -1;
            if (startHere < 0) startHere = 0;
            if (startHere >= borderPoints.Length) startHere = borderPoints.Length - 1;
            int ind = startHere;
            int lind = ind - 1;
            if (!forward) lind = ind + 1;
            if (lind < 0) lind = borderPoints.Length - 1;
            if (lind >= borderPoints.Length) lind = 0;
            while (true)
            {
                if (borderPoints[ind].direction == searchFor && !borderPoints[ind].used)
                {
                    borderPoints[ind].used = true;
                    return ind;
                }
                lind = ind;
                if (forward) ++ind;
                else --ind;
                if (ind >= borderPoints.Length) ind = 0;
                if (ind < 0) ind = borderPoints.Length - 1;
                if (ind == startHere) break;
            }
            return -1;
        }
        public CompoundShape Union()
        {
            switch (this.Position)
            {
                case BorderPosition.b1coversb2:
                    return new CompoundShape(new SimpleShape(border1));
                case BorderPosition.b2coversb1:
                    return new CompoundShape(new SimpleShape(border2));
                case BorderPosition.disjunct:
                    return new CompoundShape(new SimpleShape(border1), new SimpleShape(border2));
                case BorderPosition.intersecting:
                    // der schwierigste Fall, hier kann es eine Hülle und mehrere Löcher geben
                    List<Border> bdrs = new List<Border>();
                    int startb1 = -1;
                    int ind;
                    List<ICurve2D> segments = new List<ICurve2D>();
#if DEBUG
                    //DebuggerContainer dc = new DebuggerContainer();
#endif
                    while ((ind = FindNextPoint(true, startb1, PointPosition.Direction.Leaving, true)) >= 0)
                    {
                        int ind1 = FindNextPoint(true, ind, PointPosition.Direction.Entering, true);
                        if (ind1 >= 0)
                        {
                            segments.AddRange(border1.GetPart(border1Points[ind].par, border1Points[ind1].par, true));
#if DEBUG
                            //dc.toShow.Clear();
                            //dc.Add(segments.ToArray());
#endif
                            int ind2 = FindNextPoint(false, border1Points[ind1].index, PointPosition.Direction.Entering, true);
                            segments.AddRange(border2.GetPart(border2Points[border1Points[ind1].index].par, border2Points[ind2].par, true));
#if DEBUG
                            //dc.toShow.Clear();
                            //dc.Add(segments.ToArray());
#endif
                            startb1 = border2Points[ind2].index; // index auf b1
                        }
                        if (segments.Count > 0)
                        {
                            GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                            GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                            if ((sp | ep) < precision)
                            {
                                Border bdr = new Border(segments.ToArray(), true);
                                // identische hin und zurücklaufende Kurven entfernen
                                if (bdr.ReduceDeadEnd(precision * 10))
                                {
                                    bdr.RemoveSmallSegments(precision);
                                    bdrs.Add(bdr);
                                }
                                segments.Clear();
                                startb1 = -1;
                            }
                        }
                    }
                    if (bdrs.Count > 0)
                    {
                        // hier kann ein Ring entstehen, der nur aus einem Border besteht
                        // das müsste noch extra überprüft werden

                        bdrs.Sort(new BorderAreaComparer());
                        Border outline = bdrs[bdrs.Count - 1] as Border;
                        double[] si = outline.GetSelfIntersection(precision);
                        if (si.Length >= 3) // zwei Schnittpunkt, wie ein "C", das sich an der Öffnung berührt, also eigentlich ein "O" ist
                        {
                            // zerschneide die outline in Stücke und eliminiere gegeläufige Teile
                            List<double> parms = new List<double>();
                            for (int i = 0; i < si.Length; i += 3)
                            {
                                parms.Add(si[i]);
                                parms.Add(si[i + 1]);
                            }
                            parms.Sort();
                            List<Path2D> parts = new List<Path2D>();
                            for (int i = 0; i < parms.Count; i++)
                            {
                                parts.Add(new Path2D(outline.GetPart(parms[i], parms[(i + 1) % parms.Count], true)));
                            }
#if DEBUG
                            GeoObjectList dbgl1 = new GeoObjectList();
                            for (int i = 0; i < parts.Count; i++)
                            {
                                dbgl1.Add(parts[i].MakeGeoObject(Plane.XYPlane));
                            }
#endif
                            // parts enthält jetzt die aufgeschnipselte outline. Suche und entferne gegenläufige identische Teilabschnitte
                            bool removed = true;
                            while (removed)
                            {
                                removed = false;
                                for (int i = 0; i < parts.Count; i++)
                                {
                                    if (parts[i].SubCurvesCount==0)
                                    {
                                        parts.RemoveAt(i);
                                        removed = true;
                                        break;
                                    }
                                    Path2D part1 = parts[i].CloneReverse(true) as Path2D;
                                    if (part1.IsClosed)
                                    {   // ein in sich zurückkehrendes Stückchen?
                                        Border testEmpty = new Border(part1);
                                        if (testEmpty.Area < precision)
                                        {
                                            parts.RemoveAt(i);
                                            removed = true;
                                            break;
                                        }
                                    }
                                    for (int j = i + 1; j < parts.Count; j++)
                                    {
                                        if (part1.GeometricalEqual(precision, parts[j]))
                                        {
                                            parts.RemoveAt(j);
                                            parts.RemoveAt(i);
                                            removed = true;
                                            break;
                                        }
                                    }
                                    if (removed) break;
                                }
                            }
#if DEBUG
                            GeoObjectList dbgl2 = new GeoObjectList();
                            for (int i = 0; i < parts.Count; i++)
                            {
                                dbgl2.Add(parts[i].MakeGeoObject(Plane.XYPlane));
                            }
#endif
                            CompoundShape tmp = CompoundShape.CreateFromList(parts.ToArray(), precision);
                            if (tmp != null && tmp.SimpleShapes.Length == 1)
                            {
                                bdrs.RemoveAt(bdrs.Count - 1); // alte outline weg
                                for (int i = 0; i < tmp.SimpleShapes[0].NumHoles; i++)
                                {
                                    bdrs.Add(tmp.SimpleShapes[0].Hole(i)); // Löcher dazu
                                }
                                outline = tmp.SimpleShapes[0].Outline;
                                bdrs.Add(tmp.SimpleShapes[0].Outline); // neue outline dazu, kommt gleich wieder weg
                            }
                        }
                        else if (si.Length == 3)
                        {   // ein einziger innerer Schnittpunkt, das kann ein "Spike" in  der outline sein
                        }
                        bdrs.RemoveAt(bdrs.Count - 1); // outline weg
                        return new CompoundShape(new SimpleShape(outline, bdrs.ToArray()));
                    }
                    break;
            }
            return new CompoundShape(new SimpleShape(border1), new SimpleShape(border2));
            // throw new BorderException("unexpected error in BorderOperation.Union!", BorderException.BorderExceptionType.InternalError);
        }
        public CompoundShape Intersection()
        {
            switch (this.Position)
            {
                case BorderPosition.b1coversb2:
                    return new CompoundShape(new SimpleShape(border2));
                case BorderPosition.b2coversb1:
                    return new CompoundShape(new SimpleShape(border1));
                case BorderPosition.disjunct:
                    return new CompoundShape(); // leer!
                case BorderPosition.intersecting:
                    // der schwierigste Fall, hier kann es mehrere Hüllen aber keine Löcher geben
                    ArrayList bdrs = new ArrayList();
                    int startb1 = -1;
                    int ind;
                    ArrayList segments = new ArrayList();
                    while ((ind = FindNextPoint(true, startb1, PointPosition.Direction.Entering, true)) >= 0)
                    {
                        int ind1 = FindNextPoint(true, ind, PointPosition.Direction.Leaving, true);
                        if (ind1 >= 0)
                        {
                            segments.AddRange(border1.GetPart(border1Points[ind].par, border1Points[ind1].par, true));
                            int ind2 = FindNextPoint(false, border1Points[ind1].index, PointPosition.Direction.Leaving, true);
                            segments.AddRange(border2.GetPart(border2Points[border1Points[ind1].index].par, border2Points[ind2].par, true));
                            startb1 = border2Points[ind2].index; // index auf b1
                        }
                        if (segments.Count > 0)
                        {
                            GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                            GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                            //if (Precision.IsEqual(sp, ep))
                            if (this.precision == 0) { this.precision = Precision.eps; }
                            if ((sp | ep) < this.precision)
                            {
                                bdrs.Add(new Border((ICurve2D[])segments.ToArray(typeof(ICurve2D))));
                                segments.Clear();
                                startb1 = -1;
                            }
                        }
                    }
                    if (bdrs.Count > 0)
                    {
                        SimpleShape[] ss = new SimpleShape[bdrs.Count];
                        for (int i = 0; i < bdrs.Count; ++i)
                        {
                            ss[i] = new SimpleShape(bdrs[i] as Border);
                        }
                        return new CompoundShape(ss);
                    }
                    // sie überschneiden sich (vermutlich Berührung), aber der Inhalt ist leer, also leer liefern
                    return new CompoundShape();
            }
            throw new BorderException("unexpected error in BorderOperation.Intersection!", BorderException.BorderExceptionType.InternalError);
        }
        public CompoundShape Difference()
        {
            switch (this.Position)
            {
                case BorderPosition.b1coversb2:
                    return new CompoundShape(new SimpleShape(border1, border2));
                case BorderPosition.b2coversb1:
                    // es wird alles weggenommen, also leer, warum stand hier vorher b2-b1???
                    return new CompoundShape(); // leer!
                // return new CompoundShape(new SimpleShape(border2, border1));
                case BorderPosition.disjunct:
                    // disjunkt: das muss dann doch border1 sein, oder? vorher stand hier leer
                    return new CompoundShape(new SimpleShape(border1));
                // return new CompoundShape(); // leer!
                case BorderPosition.intersecting:
                    // der schwierigste Fall, hier kann es mehrere Hüllen aber keine Löcher geben
                    ArrayList bdrs = new ArrayList();
                    int startb1 = -1;
                    int ind;
                    bool found = false;
                    ArrayList segments = new ArrayList();
                    while ((ind = FindNextPoint(true, startb1, PointPosition.Direction.Leaving, true)) >= 0)
                    {
                        int ind1 = FindNextPoint(true, ind, PointPosition.Direction.Entering, true);
                        if (ind1 >= 0)
                        {
                            segments.AddRange(border1.GetPart(border1Points[ind].par, border1Points[ind1].par, true));
                            int ind2 = FindNextPoint(false, border1Points[ind1].index, PointPosition.Direction.Entering, false);
                            segments.AddRange(border2.GetPart(border2Points[border1Points[ind1].index].par, border2Points[ind2].par, false));
                            startb1 = border2Points[ind2].index; // index auf b1
                        }
                        if (segments.Count > 0)
                        {
                            GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                            GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                            //if (Precision.IsEqual(sp, ep))
                            if ((sp | ep) < this.precision)
                            {
                                Border bdr = new Border((ICurve2D[])segments.ToArray(typeof(ICurve2D)), true);
                                if (bdr.ReduceDeadEnd(precision * 10))
                                {
                                    bdr.RemoveSmallSegments(precision);
                                    bdrs.Add(bdr);
                                }
                                found = true;
                                segments.Clear();
                                startb1 = -1;
                            }
                        }
                    }
                    if (bdrs.Count > 0)
                    {
                        SimpleShape[] ss = new SimpleShape[bdrs.Count];
                        for (int i = 0; i < bdrs.Count; ++i)
                        {
                            ss[i] = new SimpleShape(bdrs[i] as Border);
                        }
                        return new CompoundShape(ss);
                    }
                    // sie überschneiden sich, aber der Inhalt ist leer, also border1 liefern
                    if (found) return new CompoundShape();
                    else return new CompoundShape(new SimpleShape(border1));
                    break;
            }
            throw new BorderException("unexpected error in BorderOperation.Intersection!", BorderException.BorderExceptionType.InternalError);
        }
        public CompoundShape Split()
        {
            if (!intersect || border1Points.Length == 0 || border2Points.Length == 0)
            {
                return new CompoundShape(new SimpleShape(border1)); // kein Schnitt
            }
            // die 2. Border ist offen und teilt die 1. auf
            ArrayList bdrs = new ArrayList();
            int startb2 = -1;
            int ind;
            ArrayList segments = new ArrayList();
            // zunächst in Richtung von border2 suchen
            // auf border1 gehen wir immer nur vorwärts
            while ((ind = FindNextPoint(false, startb2, PointPosition.Direction.Entering, true)) >= 0)
            {
                int ind1 = FindNextPoint(false, ind, PointPosition.Direction.Leaving, true);
                if (ind1 >= 0)
                {
                    segments.AddRange(border2.GetPart(border2Points[ind].par, border2Points[ind1].par, true));
                    // auf Border 1 sind eigentlich alle Punkte "Crossing", was ist mit Berührungen?
                    // aber der, auf den border2Points[ind1].index darf ja nicht nochmal gefunden werden, deshalb "+1"
                    int nextInd = (border2Points[ind1].index + 1);
                    if (nextInd >= border1Points.Length) nextInd = 0;
                    int ind2 = FindNextPoint(true, nextInd, PointPosition.Direction.Crossing, true);
                    if (ind2 >= 0)
                    {
                        segments.AddRange(border1.GetPart(border1Points[border2Points[ind1].index].par, border1Points[ind2].par, true));
                        startb2 = border1Points[ind2].index;
                    }
                    else
                    {

                    }
                }
                if (segments.Count > 0)
                {
                    GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                    GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                    //if (Precision.IsEqual(sp, ep))
                    if ((sp | ep) < this.precision)
                    {
                        bdrs.Add(new Border((ICurve2D[])segments.ToArray(typeof(ICurve2D)), true));
                        segments.Clear();
                        startb2 = -1;
                    }
                }
            }
            // jetzt entgegen der Richtung von border2 suchen.
            // zuvor aber alle Punkte wieder freigeben
            for (int i = 0; i < border1Points.Length; i++)
            {
                border1Points[i].used = false;
            }
            for (int i = 0; i < border2Points.Length; i++)
            {
                border2Points[i].used = false;
            }
            while ((ind = FindNextPoint(false, startb2, PointPosition.Direction.Leaving, false)) >= 0)
            {
                int ind1 = FindNextPoint(false, ind, PointPosition.Direction.Entering, false);
                if (ind1 >= 0)
                {
                    segments.AddRange(border2.GetPart(border2Points[ind].par, border2Points[ind1].par, false));
                    // auf Border 1 sind eigentlich alle Punkte "Crossing", was ist mit Berührungen?
                    // aber der, auf den border2Points[ind1].index darf ja nicht nochmal gefunden werden, deshalb "+1"
                    int nextInd = (border2Points[ind1].index + 1);
                    if (nextInd >= border1Points.Length) nextInd = 0;
                    int ind2 = FindNextPoint(true, nextInd, PointPosition.Direction.Crossing, true);
                    if (ind2 >= 0)
                    {
                        segments.AddRange(border1.GetPart(border1Points[border2Points[ind1].index].par, border1Points[ind2].par, true));
                        startb2 = border1Points[ind2].index;
                    }
                    else
                    {

                    }
                }
                if (segments.Count > 0)
                {
                    GeoPoint2D sp = ((ICurve2D)segments[0]).StartPoint;
                    GeoPoint2D ep = ((ICurve2D)segments[segments.Count - 1]).EndPoint;
                    //if (Precision.IsEqual(sp, ep))
                    if ((sp | ep) < this.precision)
                    {
                        bdrs.Add(new Border((ICurve2D[])segments.ToArray(typeof(ICurve2D)), true));
                        segments.Clear();
                        startb2 = -1;
                    }
                }
            }
            if (bdrs.Count > 1) // wenn Split nur ein Border liefert, dann war was falsch (geändert am 2.11.15)
            {
                SimpleShape[] ss = new SimpleShape[bdrs.Count];
                for (int i = 0; i < bdrs.Count; i++)
                {
                    ss[i] = new SimpleShape(bdrs[i] as Border);
                }
                return new CompoundShape(ss);
            }
            else
            {
                return new CompoundShape(new SimpleShape(border1)); // kein Schnitt
            }
        }
        public enum BorderPosition { disjunct, intersecting, b1coversb2, b2coversb1, identical, unknown };
        private BorderPosition borderPosition;
        public BorderPosition Position
        {
            get
            {
                if (border1 == null) return BorderPosition.unknown; // die Initialisierung hat nicht geklappt
                if (borderPosition == BorderPosition.unknown)
                {
                    if (intersect && this.border1Points.Length > 0)
                    {
                        borderPosition = BorderPosition.intersecting;
                    }
                    else
                    {
                        bool on1 = false;
                        bool on2 = false;
                        Border.Position pos = border2.GetPosition(border1.StartPoint, precision);
                        if (pos == Border.Position.OnCurve)
                        {   // dummer Zufall: kein Schnittpunkt und mit Berührpunkt getestet
                            try
                            {
                                // pos = border2.GetPosition(border1.SomeInnerPoint);
                                // geändert wie folgt. Überlegung: kein Schnittpunkt und mit Berührpunkt getestet
                                // dann mit einem anderen Punkt auf dem Border testen. Zwei Berührpunkte
                                // wäre großer Zufall. deshalb die komische zahl, um Systematiken auszuweichen
                                pos = border2.GetPosition(border1.PointAt(0.636548264536 * border1.Segments.Length), precision);
                            }
                            catch (BorderException) { }
                        }
                        on1 = (pos == Border.Position.OnCurve);
                        if (pos == Border.Position.Inside)
                        {
                            borderPosition = BorderPosition.b2coversb1;
                        }
                        else
                        {
                            pos = border1.GetPosition(border2.StartPoint, precision);
                            if (pos == Border.Position.OnCurve)
                            {
                                try
                                {
                                    //pos = border1.GetPosition(border2.SomeInnerPoint);
                                    pos = border1.GetPosition(border2.PointAt(0.636548264536 * border2.Segments.Length), precision);
                                }
                                catch (BorderException) { }
                            }
                            on2 = (pos == Border.Position.OnCurve);
                            if (pos == Border.Position.Inside)
                            {
                                borderPosition = BorderPosition.b1coversb2;
                            }
                            else
                            {
                                borderPosition = BorderPosition.disjunct;
                            }
                        }
                        if (on1 && on2)
                        {
                            if (CheckIdentical()) borderPosition = BorderPosition.identical;
                        }
                    }
                }
                return borderPosition;
            }
        }

        private bool CheckIdentical()
        {
            for (int i = 0; i < border1.Count; ++i)
            {
                if (border2.GetPosition(border1.Segments[i].PointAt(0.5), precision) != Border.Position.OnCurve) return false;
            }
            for (int i = 0; i < border2.Count; ++i)
            {
                if (border1.GetPosition(border2.Segments[i].PointAt(0.5), precision) != Border.Position.OnCurve) return false;
            }
            return true;
        }
        internal bool IsValid()
        {
            return intersect && border1 != null && border2 != null;
        }
#if DEBUG
        List<PointPosition> debugb1p;
        List<PointPosition> debugb2p;
        public DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                res.Add(border1, System.Drawing.Color.Red, 1);
                res.Add(border2, System.Drawing.Color.Blue, 2);
                if (debugb1p != null)
                {
                    for (int i = 0; i < debugb1p.Count; ++i)
                    {
                        string dbg = i.ToString() + ": " + debugb1p[i].direction.ToString() + ", " + debugb1p[i].index.ToString() + " id: " + debugb1p[i].id.ToString();
                        res.Add(debugb1p[i].point, System.Drawing.Color.DarkRed, dbg);
                    }
                }
                if (debugb2p != null)
                {
                    for (int i = 0; i < debugb2p.Count; ++i)
                    {
                        string dbg = i.ToString() + ": " + debugb2p[i].direction.ToString() + ", " + debugb2p[i].index.ToString() + " id: " + debugb2p[i].id.ToString();
                        res.Add(debugb2p[i].point, System.Drawing.Color.DarkBlue, dbg);
                    }
                }
                return res;
            }
        }
#endif

        internal void MakeUnused()
        {
            for (int i = 0; i < border1Points.Length; i++)
            {
                border1Points[i].used = false;
            }
            for (int i = 0; i < border2Points.Length; i++)
            {
                border2Points[i].used = false;
            }
        }
    }

    // als key für Dictionary
    internal class BorderPair : IComparable
    {
        int id1, id2;
        public BorderPair(Border bdr1, Border bdr2)
        {
            if (bdr1.Id < bdr2.Id)
            {
                id1 = bdr1.Id;
                id2 = bdr2.Id;
            }
            else
            {
                id1 = bdr2.Id;
                id2 = bdr1.Id;
            }
        }
        public override int GetHashCode()
        {
            return id1.GetHashCode() ^ id2.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            BorderPair other = obj as BorderPair;
            if (other != null)
            {
                return id1 == other.id1 && id2 == other.id2;
            }
            return base.Equals(obj);
        }

        int IComparable.CompareTo(object obj)
        {
            BorderPair other = obj as BorderPair;
            if (other != null)
            {
                if (other.id1 == id1) return id2.CompareTo(other.id2);
                return id1.CompareTo(other.id1);
            }
            return -1;
        }

        internal static BorderOperation GetBorderOperation(Dictionary<BorderPair, BorderOperation> borderOperationCache, Border bdr1, Border bdr2, double precision)
        {
            BorderOperation bo;
            if (borderOperationCache == null)
            {
                if (precision == 0.0) bo = new BorderOperation(bdr1, bdr2);
                else bo = new BorderOperation(bdr1, bdr2, precision);
                return bo;
            }
            BorderPair bp = new BorderPair(bdr1, bdr2);
            if (!borderOperationCache.TryGetValue(bp, out bo))
            {
                if (precision == 0.0) bo = new BorderOperation(bdr1, bdr2);
                else bo = new BorderOperation(bdr1, bdr2, precision);
                borderOperationCache[bp] = bo;
            }
            else
            {
                bo.MakeUnused();
            }
            return bo;
        }
    }
}
