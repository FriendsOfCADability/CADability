using CADability.Curve2D;
using System;
using System.Collections.Generic;
using Wintellect.PowerCollections;

namespace CADability.Shapes
{
#if DEBUG
    public class BorderQuadTree
#else
    internal class BorderQuadTree
#endif
    {
        /* KONZEPT:
         * Zwei Border Objekte werden in einen (quadratischen) QuadTree eingefügt.
         * Die Schnittpunkte der beiden Borders werden ebenfalls in diesen Quadtree eingefügt.
         * Jedes Quadrat darf nur maximal einen Schnittpunkt und für jede Border einen Kurvenzug, d.h.
         * nur einen Eintrittspunkt und einen Austrittspunkt, enthalten. Ansonsten wird aufgeteilt.
         * Sollte es beim Aufbau eines Borders Probleme geben (z.B. ein Eintrittspunkt aber kein Austrittspunkt) dann wird eine exception geworfen
         * und der Quadtree etwas verruckelt neu gemacht.
         * Jedes Quadrat enthält also maximal zwei Bordersegmente, deren Start- und Endparameter bekannt sind. Und maximal einen Schnittpunkt.
         * Je nach Operation werden die Intervalle der Segmente aus allen Quadraten zusammengesammelt. Dabei muss man wissen, ob der Eintritts- 
         * oder Austrittspunkt des einen Borders innerhalb oder außerhalb des anderen Borders liegt. Dieses wird rein logisch anhand der Eintritts-
         * und Austrittspunkte bestimmt, also ohne das Border selbst zu fragen.
         * 
         * Es wird mit einem Integer Koordinatensystem gearbeitet und die horizontalen und vertikalen Schnittpunkte der Border mit dem Quadtree
         * nur einmal bestimmt. Deshalb kann man immer gut mit Gleichheiten rechnen
         */

        /// <summary>
        /// Für das Integer Koordinatensystem. Das hat den Zweck, Hash-Tabellen für horizontale und vertikale Schnitte zu ermöglichen
        /// und exakt gleiche Ergebnisse für die gemeinsame Seite eines Quadrates zu liefern, egal aus welchem Quadrat man rechnet.
        /// Die Breite der Quadrate ist imme eine Zweierpotenz. x, y und w sind immer positiv
        /// </summary>
        struct quad
        {
            public int x, y, w; // bestimmt einen Quadranten
            public quad(int x, int y, int w)
            {
                this.x = x;
                this.y = y;
                this.w = w;
            }

            internal quad sub(int i)
            {
                switch (i)
                {
                    case 0: return new quad(x + w / 2, y + w / 2, w / 2);
                    case 1: return new quad(x, y + w / 2, w / 2);
                    case 2: return new quad(x, y, w / 2);
                    case 3: return new quad(x + w / 2, y, w / 2);
                }
                throw new ApplicationException(); // kommt nicht vor;
            }
        }

        enum side { left, right, bottom, top }
        struct intersection : IComparable
        {
            public double xy; // bei horizontal y, bei vertikal x;
            public double par; // Parameter auf dem Border
            public double dir; // Richtung in diesem Punkt
            public intersection(double xy, double par, double dir)
            {
                this.xy = xy;
                this.par = par;
                this.dir = dir;
            }

            int IComparable.CompareTo(object obj)
            {
                if (!(obj is intersection)) return 0;
                return xy.CompareTo(((intersection)obj).xy);
            }
        }

        class interval
        {
            public List<double> ranges;
            double period;
            public interval(double period)
            {
                this.period = period;
                ranges = new List<double>();
            }
            public void add(double start, double end)
            {
                if (start > end)
                {
                    add(start, period);
                    add(0.0, end);
                }
                else
                {
                    int sp = 0, ep = 0, spx = -1, epx = -1;
                    for (int i = 0; i < ranges.Count; i++)
                    {
                        if (ranges[i] < start) sp = i + 1;
                        if (ranges[i] < end) ep = i + 1;
                        if (ranges[i] == start) spx = i;
                        if (ranges[i] == end) epx = i;
                    }
                    if (spx >= 0 && epx >= 0)
                    {   // Verbindung zwischen zwei existierenden Intervallstücken
                        ranges.RemoveAt(epx);
                        ranges.RemoveAt(spx);
                    }
                    else if (spx >= 0 && !even(spx))
                    {
                        ranges[spx] = end;
                    }
                    else if (epx >= 0 && even(epx))
                    {
                        ranges[epx] = start;
                    }
                    else if (sp == ep && even(sp))
                    {
                        ranges.Insert(ep, end);
                        ranges.Insert(sp, start);
                    }
                    else
                    {   // in diesen Fällen kommen keine Überlappungen vor
                        throw new NotImplementedException("overlapping interval");
                    }
                }
            }

            private bool even(int n)
            {
                return (n & 0x1) == 0;
            }

            public bool hasInterval
            {
                get
                {
                    return ranges.Count > 0;
                }
            }
            public Pair<double, double> removeInterval(double startHere)
            {
                int ind = -1;
                if (startHere == -1)
                {
                    ind = 0;
                }
                else
                {
                    for (int i = 0; i < ranges.Count; i += 2)
                    {
                        if (ranges[i] == startHere)
                        {
                            ind = i;
                            break;
                        }
                    }
                }
                if (ind >= 0 && ind < ranges.Count)
                {
                    Pair<double, double> res = new Pair<double, double>(ranges[ind], ranges[ind + 1]);
                    ranges.RemoveRange(ind, 2);
                    return res;
                }
                return new Pair<double, double>(-1, -1);
            }

            internal Pair<double, double> removeIntervalEnd(double endHere)
            {
                int ind = -1;
                if (endHere == -1)
                {
                    ind = 0;
                }
                else
                {
                    for (int i = 0; i < ranges.Count; i += 2)
                    {
                        if (ranges[i + 1] == endHere)
                        {
                            ind = i;
                            break;
                        }
                    }
                }
                if (ind >= 0 && ind < ranges.Count)
                {
                    Pair<double, double> res = new Pair<double, double>(ranges[ind], ranges[ind + 1]);
                    ranges.RemoveRange(ind, 2);
                    return res;
                }
                return new Pair<double, double>(-1, -1);
            }
        }
        class CriticalPosition : ApplicationException
        {
            public enum Reason { unknown, close, tangential, intersectionpoint, oddnumber, enterLeaveConfusion };
            public GeoPoint2D pos;
            public bool border1;
            public Reason reason;
            public CriticalPosition(GeoPoint2D pos, bool border1, Reason reason)
            {
                this.pos = pos;
                this.border1 = border1;
                this.reason = reason;
            }
        }
        /// <summary>
        /// Ein Quadrat des Quadtrees, wenn nicht unterteilt, dann enthält es von jedem Border nur ein Segment mit definierten Eintritts und Austrittspunkt.
        /// Tangentiale Schnitte sind nicht erlaubt, dann muss der ganze Quadtree anders positioniert werden.
        /// Enthält maximal einen Schnittpunkt der beiden Border.
        /// 
        /// Integer Koordinatensystem: so kann man einfache Hash-Tabellen machen, in denen für horizontale und vertikale Linien die Schnittpunkte erfasst werden
        /// </summary>
        class Node
        {
            BorderQuadTree borderQuadTree; // Rückverweis
            quad quad; // die Lage des Quadranten im int-System
            // entweder: 
            Node[] subTree; // wenn null, dann Blatt
            // oder:
            // Eigenschaften des Blattes:
            double enter1, leave1, enter2, leave2; // die Eintritts- bzw- Austrittspunkte der beiden Border, -1, wenn nicht betroffen
            // diese Werte sind zwischen 0 und 4, linksrum, 0 ist der linke untere Eckpunkt
            // Innere Punkt des Borders findet man, indem man von "leave" weitergeht (ggf. über 0) bis enter. Der Rest ist außerhalb
            double bdr1e, bdr1l, bdr2e, bdr2l; // Positionen auf dem Border für Ein- und Austritt in dieses Quadrat
            int intersectionPointIndex; // Index auf borderQuadTree.borderIntersection oder -1. Es darf nur einen geben
            bool fullInsideBdr1, fullInsideBdr2; // wenn kein bdr1 Eintritt oder Austritt vorhanden, sind wir ganz innerhalb von bdr1? analog für bdr2

            public Node(quad quad, BorderQuadTree borderQuadTree)
            {
                this.quad = quad;
                this.borderQuadTree = borderQuadTree;
                enter1 = leave1 = enter2 = leave2 = -1;
            }
            /// <summary>
            /// Fügt die beiden Border in diesen Quadranten ein. Wenn es ein Problem gibt (tangentiale Schitte, Schnittpunkt der Border genau auf der Kante) dann wird false geliefet.
            /// </summary>
            /// <returns></returns>
            public void Init()
            {
                intersection[] left1 = borderQuadTree.GetIntersections(quad, side.left, true);
                intersection[] right1 = borderQuadTree.GetIntersections(quad, side.right, true);
                intersection[] bottom1 = borderQuadTree.GetIntersections(quad, side.bottom, true);
                intersection[] top1 = borderQuadTree.GetIntersections(quad, side.top, true);
                intersection[] left2 = borderQuadTree.GetIntersections(quad, side.left, false);
                intersection[] right2 = borderQuadTree.GetIntersections(quad, side.right, false);
                intersection[] bottom2 = borderQuadTree.GetIntersections(quad, side.bottom, false);
                intersection[] top2 = borderQuadTree.GetIntersections(quad, side.top, false);
                BoundingRect ext = borderQuadTree.ext(quad);
                int num1 = left1.Length + right1.Length + bottom1.Length + top1.Length;
                int num2 = left2.Length + right2.Length + bottom2.Length + top2.Length;
                bool subdivide = (num1 > 2 || num2 > 2);
                intersectionPointIndex = -1;
                if (!subdivide)
                {
                    for (int i = 0; i < borderQuadTree.borderIntersection.Count; i++)
                    {
                        if (ext.Contains(borderQuadTree.borderIntersection[i].p))
                        {
                            if (intersectionPointIndex >= 0)
                            {
                                subdivide = true;
                                break;
                            }
                            intersectionPointIndex = i;
                        }
                    }
                }
                // Wenn ein Border ganz innerhalb liegt und sich nicht mit dem anderen schneidet, dann wird es nicht in diesen Node eingetragen. Es tritt auch sonst nicht in Erscheinung.
                if (subdivide)
                {
                    subTree = new Node[4];
                    for (int i = 0; i < 4; i++)
                    {
                        subTree[i] = new Node(quad.sub(i), borderQuadTree);
                        subTree[i].Init();
                    }
                }
                else
                {
                    if (intersectionPointIndex >= 0 && num1 + num2 != 4) throw new CriticalPosition(borderQuadTree.borderIntersection[intersectionPointIndex].p, false, CriticalPosition.Reason.intersectionpoint);
                    if (num1 == 2)
                    {
                        insert(left1, ref bdr1e, ref bdr1l, ref enter1, ref leave1, side.left, ref ext);
                        insert(right1, ref bdr1e, ref bdr1l, ref enter1, ref leave1, side.right, ref ext);
                        insert(bottom1, ref bdr1e, ref bdr1l, ref enter1, ref leave1, side.bottom, ref ext);
                        insert(top1, ref bdr1e, ref bdr1l, ref enter1, ref leave1, side.top, ref ext);
                    }
                    else if (num1 == 1)
                    {
                        throw new CriticalPosition(GeoPoint2D.Origin, true, CriticalPosition.Reason.oddnumber);
                    }
                    if (num2 == 2)
                    {
                        insert(left2, ref bdr2e, ref bdr2l, ref enter2, ref leave2, side.left, ref ext);
                        insert(right2, ref bdr2e, ref bdr2l, ref enter2, ref leave2, side.right, ref ext);
                        insert(bottom2, ref bdr2e, ref bdr2l, ref enter2, ref leave2, side.bottom, ref ext);
                        insert(top2, ref bdr2e, ref bdr2l, ref enter2, ref leave2, side.top, ref ext);
                    }
                    else if (num2 == 1)
                    {
                        throw new CriticalPosition(GeoPoint2D.Origin, false, CriticalPosition.Reason.oddnumber);
                    }
                }
            }
            /// <summary>
            /// Setzt die fullInsideBdr1/2 für Quadrate, die die betreffende Border nicht enthalten
            /// </summary>
            /// <param name="border1"></param>
            /// <param name="v0"></param>
            /// <param name="v1"></param>
            /// <param name="v2"></param>
            /// <param name="v3"></param>
            public void setInside(bool border1, ref int v0, ref int v1, ref int v2, ref int v3)
            {   // v0..v3: (Quadrantennummer) von rechts oben (0) bis rechts unten(3): 0: außerhalb, 1: innerhalb, 2: unbekannt
                // wird nur ausgeführt um fullInsideBdr1/2 zu setzen, welches nur bei fehlen entsprechender enter/leave Päärchen verwendet wird.
                if (subTree != null)
                {
                    int b, t, l, r, m; // unten, oben, mitte u.s.w.
                    b = t = l = r = m = 2; // alle unbekannt (Seitenmitten und Quadratmitte)
                    subTree[0].setInside(border1, ref v0, ref t, ref m, ref r);
                    subTree[1].setInside(border1, ref t, ref v1, ref l, ref m);
                    subTree[2].setInside(border1, ref m, ref l, ref v2, ref b);
                    subTree[3].setInside(border1, ref r, ref m, ref b, ref v3);
                }
                else
                {
                    if (border1)
                    {
                        if (enter1 < 0) // Border 1 geht hier nicht durch
                        {   // eine Ecke muss definiert sein, die gibt die Lage an
                            if (v0 < 2) fullInsideBdr1 = v0 == 1;
                            else if (v1 < 2) fullInsideBdr1 = v1 == 1;
                            else if (v2 < 2) fullInsideBdr1 = v2 == 1;
                            else if (v3 < 2) fullInsideBdr1 = v3 == 1;
                            if (fullInsideBdr1) v0 = v1 = v2 = v3 = 1;
                            else v0 = v1 = v2 = v3 = 0;
                        }
                        else
                        {
                            v0 = inside(2, border1) ? 1 : 0; // die Parameter laufen von links unten (0) einmal links rum bis 4
                            v1 = inside(3, border1) ? 1 : 0;
                            v2 = inside(0, border1) ? 1 : 0;
                            v3 = inside(1, border1) ? 1 : 0;
                        }
                    }
                    else
                    {
                        if (enter2 < 0) // Border 2 geht hier nicht durch
                        {   // eine Ecke muss definiert sein, die gibt die Lage an
                            if (v0 < 2) fullInsideBdr2 = v0 == 1;
                            else if (v1 < 2) fullInsideBdr2 = v1 == 1;
                            else if (v2 < 2) fullInsideBdr2 = v2 == 1;
                            else if (v3 < 2) fullInsideBdr2 = v3 == 1;
                            if (fullInsideBdr2) v0 = v1 = v2 = v3 = 1;
                            else v0 = v1 = v2 = v3 = 0;
                        }
                        else
                        {
                            v0 = inside(2, border1) ? 1 : 0; // die Parameter laufen von links unten (0) einmal links rum bis 4
                            v1 = inside(3, border1) ? 1 : 0;
                            v2 = inside(0, border1) ? 1 : 0;
                            v3 = inside(1, border1) ? 1 : 0;
                        }
                    }

                }
            }
            /// <summary>
            /// Sammelt die für die gegebene Operation relevanten Abschnitte der beiden Borders zusammen
            /// </summary>
            /// <param name="bdr1parts"></param>
            /// <param name="bdr2parts"></param>
            /// <param name="op"></param>
            public void collectIntervals(interval bdr1parts, interval bdr2parts, op op)
            {
                if (subTree != null)
                {
                    subTree[0].collectIntervals(bdr1parts, bdr2parts, op);
                    subTree[1].collectIntervals(bdr1parts, bdr2parts, op);
                    subTree[2].collectIntervals(bdr1parts, bdr2parts, op);
                    subTree[3].collectIntervals(bdr1parts, bdr2parts, op);
                }
                else
                {
                    switch (op)
                    {
                        case op.unite: // Vereinigung: die jeweils äußeren Teile werden geliefert
                            if (intersectionPointIndex >= 0)
                            {   // zwei Teile, also enter bis zum Schnittpunkt und Schnittpunkt bis zu leave
                                if (!inside(enter1, false)) bdr1parts.add(bdr1e, borderQuadTree.borderIntersection[intersectionPointIndex].par1);
                                if (!inside(enter2, true)) bdr2parts.add(bdr2e, borderQuadTree.borderIntersection[intersectionPointIndex].par2);
                                if (!inside(leave1, false)) bdr1parts.add(borderQuadTree.borderIntersection[intersectionPointIndex].par1, bdr1l);
                                if (!inside(leave2, true)) bdr2parts.add(borderQuadTree.borderIntersection[intersectionPointIndex].par2, bdr2l);
                            }
                            else
                            {
                                // Union: verwende die Stücke eines Borders, die außerhalb des anderen Borders sind
                                if (enter1 > 0)
                                {
                                    if (!inside(enter1, false) && !inside(leave1, false)) bdr1parts.add(bdr1e, bdr1l);
                                }
                                if (enter2 > 0)
                                {
                                    if (!inside(enter2, true) && !inside(leave2, true)) bdr2parts.add(bdr2e, bdr2l);
                                }
                            }
                            break;
                        case op.common: // Schnitt: die jeweils inneren Teile werden geliefert
                            if (intersectionPointIndex >= 0)
                            {   // zwei Teile, also enter bis zum Schnittpunkt und Schnittpunkt bis zu leave
                                if (inside(enter1, false)) bdr1parts.add(bdr1e, borderQuadTree.borderIntersection[intersectionPointIndex].par1);
                                if (inside(enter2, true)) bdr2parts.add(bdr2e, borderQuadTree.borderIntersection[intersectionPointIndex].par2);
                                if (inside(leave1, false)) bdr1parts.add(borderQuadTree.borderIntersection[intersectionPointIndex].par1, bdr1l);
                                if (inside(leave2, true)) bdr2parts.add(borderQuadTree.borderIntersection[intersectionPointIndex].par2, bdr2l);
                            }
                            else
                            {
                                if (enter1 > 0)
                                {
                                    if (inside(enter1, false) && inside(leave1, false)) bdr1parts.add(bdr1e, bdr1l);
                                }
                                if (enter2 > 0)
                                {
                                    if (inside(enter2, true) && inside(leave2, true)) bdr2parts.add(bdr2e, bdr2l);
                                }
                            }
                            break;
                        case op.subtract: // Differenz: bdr1-bdr2, von bdr1 die äußeren, von bdr2 die inneren Teile
                            if (intersectionPointIndex >= 0)
                            {   // zwei Teile, also enter bis zum Schnittpunkt und Schnittpunkt bis zu leave
                                if (!inside(enter1, false)) bdr1parts.add(bdr1e, borderQuadTree.borderIntersection[intersectionPointIndex].par1);
                                if (inside(enter2, true)) bdr2parts.add(bdr2e, borderQuadTree.borderIntersection[intersectionPointIndex].par2);
                                if (!inside(leave1, false)) bdr1parts.add(borderQuadTree.borderIntersection[intersectionPointIndex].par1, bdr1l);
                                if (inside(leave2, true)) bdr2parts.add(borderQuadTree.borderIntersection[intersectionPointIndex].par2, bdr2l);
                            }
                            else
                            {
                                if (enter1 > 0)
                                {
                                    if (!inside(enter1, false) && !inside(leave1, false)) bdr1parts.add(bdr1e, bdr1l);
                                }
                                if (enter2 > 0)
                                {
                                    if (inside(enter2, true) && inside(leave2, true)) bdr2parts.add(bdr2e, bdr2l);
                                }
                            }
                            break;
                    }
                }
            }

            private void insert(intersection[] ia, ref double bdre, ref double bdrl, ref double enter, ref double leave, side side, ref BoundingRect ext)
            {
                if (ia.Length == 0) return;
                // Parameter für enter/leave: 0..4, links unten beginnen, linksrum
                bool hor = (side == side.bottom) || (side == side.top);
                bool rev = (side == side.left) || (side == side.top); // Richtung ist rückwärts
                for (int i = 0; i < ia.Length; i++)
                {
                    double dp;
                    if (hor) dp = (ia[i].xy - ext.Left) / ext.Width;
                    else dp = (ia[i].xy - ext.Bottom) / ext.Height;
                    if (rev) dp = 1 - dp;
                    bool entering; // true, wenn die Kurve hineingeht
                    int n = -1; // Seitennummer
                    switch (side)
                    {
                        case side.left:
                            entering = !toLeft(ia[i].dir);
                            n = 3;
                            break;
                        case side.right:
                            entering = toLeft(ia[i].dir);
                            n = 1;
                            break;
                        case side.bottom:
                            entering = toTop(ia[i].dir);
                            n = 0;
                            break;
                        case side.top:
                            entering = !toTop(ia[i].dir);
                            n = 2;
                            break;
                        default: throw new ApplicationException("invalid parameter");
                    }

                    if ((entering && enter >= 0) || (!entering && leave >= 0)) throw new CriticalPosition(pos(ia[i], side), true, CriticalPosition.Reason.enterLeaveConfusion);

                    if (entering)
                    {
                        enter = n + dp;
                        bdre = ia[i].par;
                    }
                    else
                    {
                        leave = n + dp;
                        bdrl = ia[i].par;
                    }
                }
            }


            private GeoPoint2D pos(intersection intersection, side side)
            {
                switch (side)
                {
                    case BorderQuadTree.side.left:
                        return new GeoPoint2D(borderQuadTree.left(quad), intersection.xy);
                    case BorderQuadTree.side.right:
                        return new GeoPoint2D(borderQuadTree.right(quad), intersection.xy);
                    case BorderQuadTree.side.bottom:
                        return new GeoPoint2D(intersection.xy, borderQuadTree.bottom(quad));
                    case BorderQuadTree.side.top:
                        return new GeoPoint2D(intersection.xy, borderQuadTree.top(quad));
                }
                throw new ApplicationException("invalid parameter"); // kommt nicht vor
            }

            private bool toTop(double ang)
            {
                return ang > 0 && ang < Math.PI;
            }
            private bool toLeft(double ang)
            {
                return ang > Math.PI / 2 && ang < 3 * Math.PI / 2;
            }

            private bool inside(double par, bool border1)
            {   // bei Gleichheit von par und enter1/2 bzw. leave1/2 liefert bdr2 true und bdr1 false. 
                // damit trift, wenn par selbst das andere enter/leave ist, jeweils nur einer der beiden Überprüfungen zu
                if (border1)
                {
                    if (enter1 < 0)
                    {   // border geht nicht durch diesen Knoten
                        return fullInsideBdr1;
                    }
                    else
                    {
                        if (enter1 < leave1)
                        {   // es umfasst die 0
                            return par < enter1 || par > leave1;
                        }
                        else
                        {
                            return par < enter1 && par > leave1;
                        }
                    }
                }
                else
                {
                    if (enter2 < 0)
                    {   // border geht nicht durch diesen Knoten
                        return fullInsideBdr2;
                    }
                    else
                    {
                        if (enter2 < leave2)
                        {   // es umfasst die 0
                            return par <= enter2 || par >= leave2;
                        }
                        else
                        {
                            return par <= enter2 && par >= leave2;
                        }
                    }
                }
            }
#if DEBUG
            private GeoPoint2D pointAt(double par)
            {
                BoundingRect ext = borderQuadTree.ext(quad);
                int s = (int)Math.Floor(par);
                double dp = par - s;
                switch (s)
                {
                    case 0:
                        return ext.GetLowerLeft() + dp * (ext.GetLowerRight() - ext.GetLowerLeft());
                    case 1:
                        return ext.GetLowerRight() + dp * (ext.GetUpperRight() - ext.GetLowerRight());
                    case 2:
                        return ext.GetUpperRight() + dp * (ext.GetUpperLeft() - ext.GetUpperRight());
                    case 3:
                        return ext.GetUpperLeft() + dp * (ext.GetLowerLeft() - ext.GetUpperLeft());
                    default: throw new ApplicationException("invalid parameter");
                }
            }
            internal void debug(DebuggerContainer dc)
            {
                if (subTree != null)
                {
                    for (int i = 0; i < subTree.Length; i++)
                    {
                        subTree[i].debug(dc);
                    }
                }
                else
                {
                    BoundingRect ext = borderQuadTree.ext(quad);
                    dc.Add(ext.ToBorder(), System.Drawing.Color.Blue, 0);
                    if (enter1 >= 0 && leave1 >= 0)
                    {
                        GeoPoint2D sp = pointAt(enter1);
                        GeoPoint2D ep = pointAt(leave1);
                        Line2D l2d = new Line2D(sp, ep);
                        dc.Add(l2d, System.Drawing.Color.DarkMagenta, 1);
                    }
                    else
                    {
                        if (fullInsideBdr1)
                        {
                            Arc2D a2d = new Arc2D(ext.GetCenter(), ext.Width / 6.0, Angle.A0, SweepAngle.Deg(180));
                            dc.Add(a2d, System.Drawing.Color.Red, 1); // Anzeige, dass ganz innerhalb von bdr1
                        }
                    }
                    if (enter2 >= 0 && leave2 >= 0)
                    {
                        GeoPoint2D sp = pointAt(enter2);
                        GeoPoint2D ep = pointAt(leave2);
                        Line2D l2d = new Line2D(sp, ep);
                        dc.Add(l2d, System.Drawing.Color.DarkOrange, 2);
                    }
                    else
                    {
                        if (fullInsideBdr1)
                        {
                            Arc2D a2d = new Arc2D(ext.GetCenter(), ext.Width / 6.0, Angle.A180, SweepAngle.Deg(-180));
                            dc.Add(a2d, System.Drawing.Color.Green, 1); // Anzeige, dass ganz innerhalb von bdr2
                        }
                    }
                    if (intersectionPointIndex >= 0)
                    {
                        CADability.GeoObject.Point p = CADability.GeoObject.Point.Construct();
                        p.Location = new GeoPoint(borderQuadTree.borderIntersection[intersectionPointIndex].p);
                        p.Symbol = GeoObject.PointSymbol.Cross;
                        p.ColorDef = new Attribute.ColorDef("Schnittpunkt", System.Drawing.Color.Red);
                        dc.Add(p);
                    }
                }
            }
#endif

        }
        public enum BorderPosition { disjunct, intersecting, b1coversb2, b2coversb1, identical, unknown };

        private Node root;
        double offx, offy, fct; // zur Umrechnung der Integer-Koordinaten in Welt-Koordinaten

        Border bdr1, bdr2;
        private List<GeoPoint2DWithParameter> borderIntersection; // die Schnittpunkte
        private BorderPosition position;

        double precision;
        Dictionary<int, intersection[]> horIntSect1, horIntSect2, verIntSect1, verIntSect2; // die horizontalen und vertikalen Schnittpunkte der beiden Borders. 

        const double radianEps = 1e-6; // leider fällt mir dafür nix gescheites ein
        public BorderQuadTree(Border bdr1, Border bdr2, double precision)
        {
            this.bdr1 = bdr1;
            this.bdr2 = bdr2;

            BoundingRect ext = bdr1.Extent;
            ext.MinMax(bdr2.Extent);
            if (precision < ext.Size * 1e-8) precision = ext.Size * 1e-8;
            this.precision = precision;
            ext.Inflate(2 * precision);
            double klen = Math.Max(ext.Size * 1e-8, precision);
            GeoVector2D korr = new GeoVector2D(klen, klen);

            GeoPoint2D center = ext.GetCenter();
            double width = Math.Max(ext.Width, ext.Height); // immer quadratisch

            borderIntersection = new List<GeoPoint2DWithParameter>();
            borderIntersection.AddRange(bdr1.GetIntersectionPoints(bdr2));
            borderIntersection.Sort(delegate (GeoPoint2DWithParameter p1, GeoPoint2DWithParameter p2) { return p1.par1.CompareTo(p2.par1); });
            for (int i = borderIntersection.Count - 1; i >= 0; --i)
            {
                int n = (i + 1) % borderIntersection.Count;
                if ((borderIntersection[i].p | borderIntersection[n].p) < precision)
                {   // zwei Schnittpunkte fast identisch, auf einen reduzieren. Auch Schnitte an einer Spitze stören dann nicht
                    GeoPoint2DWithParameter tmp = new GeoPoint2DWithParameter();
                    tmp.p = new GeoPoint2D(borderIntersection[n].p, borderIntersection[i].p); // in der Mitte
                    tmp.par1 = (borderIntersection[n].par1 + borderIntersection[i].par1) / 2.0;
                    tmp.par2 = (borderIntersection[n].par2 + borderIntersection[i].par2) / 2.0;
                    borderIntersection[n] = tmp;
                    borderIntersection.RemoveAt(i);
                }
            }
            if (borderIntersection.Count > 1)
            {
                while (true) // wird nach gelungenem Init abgebrochen
                {

                    // root width == 0x40000000 ist 2^30, 2^31 ist kein int mehr
                    // offx + fct * 0 == center.x-width/2
                    // offx + fct * 2^30 == center.x+width/2
                    //  fct * 2^30 == width
                    // fct = width/(2^30)
                    offx = center.x - width / 2;
                    offy = center.y - width / 2;
                    fct = width / 0x40000000;
                    if (this.precision < 2 * fct) this.precision = 2 * fct; // damit wir im QuadTree nicht die Breite 1 erreichen

                    horIntSect1 = new Dictionary<int, intersection[]>();
                    horIntSect2 = new Dictionary<int, intersection[]>();
                    verIntSect1 = new Dictionary<int, intersection[]>();
                    verIntSect2 = new Dictionary<int, intersection[]>();
                    root = new Node(new quad(0, 0, 0x40000000), this);
                    try
                    {
                        root.Init();
                        int v0, v1, v2, v3;
                        v0 = v1 = v2 = v3 = 0; // alle 4 Ecken sind außerhalb
                        root.setInside(true, ref v0, ref v1, ref v2, ref v3); // für border1
                        root.setInside(false, ref v0, ref v1, ref v2, ref v3); // für border2
                        position = BorderPosition.intersecting;
                        break;
                    }
                    catch (CriticalPosition)
                    {   // Korrekturwert für nächste Runde, wenn es einen Schnittpunkt genau auf einer Kante gab oder einen tangentialen Schnitt
                        center = center + korr;
                        width = width + korr.Length;
                        korr = (Math.Sqrt(2) * korr).ToLeft(); // sollte eine Spirale ergebn
                    }
                }
            }
            else
            {
                // hier behandeln, wenn es keinen oder nur einen Schnittpunkt gibt
                if (borderIntersection.Count > 1)
                {   // Berührung, von innen oder außen?
                    double par1 = (borderIntersection[0].par1 + bdr1.Segments.Length / 2.0) % bdr1.Segments.Length; // gegenüberliegende Positionen
                    double par2 = (borderIntersection[0].par2 + bdr2.Segments.Length / 2.0) % bdr2.Segments.Length;
                    if (bdr1.GetPosition(bdr2.PointAt(par2), precision) == Border.Position.Inside) position = BorderPosition.b1coversb2;
                    else if (bdr2.GetPosition(bdr1.PointAt(par1), precision) == Border.Position.Inside) position = BorderPosition.b2coversb1;
                    else if (Math.Abs(bdr1.Length - bdr2.Length) < precision && (bdr1.GetPosition(bdr2.SomeInnerPoint) == Border.Position.Inside) && (bdr2.GetPosition(bdr1.SomeInnerPoint) == Border.Position.Inside))
                        position = BorderPosition.identical;
                    else position = BorderPosition.disjunct;
                }
                else
                {
                    if (bdr1.GetPosition(bdr2.StartPoint, precision) == Border.Position.Inside) position = BorderPosition.b1coversb2;
                    else if (bdr2.GetPosition(bdr1.StartPoint, precision) == Border.Position.Inside) position = BorderPosition.b2coversb1;
                    else position = BorderPosition.disjunct;
                }
            }
        }
        enum op { unite, subtract, common }
        public CompoundShape Union()
        {
            switch (position)
            {
                case BorderPosition.b1coversb2:
                case BorderPosition.identical:
                    return new CompoundShape(new SimpleShape(bdr1));
                case BorderPosition.b2coversb1:
                    return new CompoundShape(new SimpleShape(bdr2));
                case BorderPosition.disjunct:
                    return new CompoundShape(new SimpleShape(bdr1), new SimpleShape(bdr2));
                case BorderPosition.intersecting:
                    // es gibt eine Hülle und ggf. Löcher
                    return processIntervals(op.unite);
            }
            return new CompoundShape();
        }

        public CompoundShape Intersection()
        {
            switch (position)
            {
                case BorderPosition.b1coversb2:
                    return new CompoundShape(new SimpleShape(bdr2));
                case BorderPosition.b2coversb1:
                    return new CompoundShape(new SimpleShape(bdr1));
                case BorderPosition.disjunct:
                    return new CompoundShape(); // leer!
                case BorderPosition.intersecting:
                    // der schwierigste Fall, hier kann es mehrere Hüllen aber keine Löcher geben
                    return processIntervals(op.common);
            }
            return new CompoundShape(); // sollte nicht vorkommen
        }

        public CompoundShape Difference()
        {
            switch (position)
            {
                case BorderPosition.b1coversb2:
                    return new CompoundShape(new SimpleShape(bdr1, bdr2));
                case BorderPosition.b2coversb1:
                    return new CompoundShape(); // leer!
                case BorderPosition.disjunct:
                    return new CompoundShape(new SimpleShape(bdr1));
                case BorderPosition.intersecting:
                    // der schwierigste Fall, hier kann es mehrere Hüllen aber keine Löcher geben
                    return processIntervals(op.subtract);
            }
            return new CompoundShape(); // leeres liefern
        }

        public BorderPosition Position
        {
            get
            {
                return position;
            }
        }

        private CompoundShape processIntervals(op op)
        {
            interval bdr1parts = new interval(bdr1.Segments.Length);
            interval bdr2parts = new interval(bdr2.Segments.Length);
            root.collectIntervals(bdr1parts, bdr2parts, op);
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < bdr1parts.ranges.Count; i += 2)
            {
                ICurve2D[] parts = bdr1.GetPart(bdr1parts.ranges[i], bdr1parts.ranges[i + 1], true);
                for (int j = 0; j < parts.Length; j++)
                {
                    dc.Add(parts[j], System.Drawing.Color.Red, i);
                }
            }
            for (int i = 0; i < bdr2parts.ranges.Count; i += 2)
            {
                ICurve2D[] parts = bdr2.GetPart(bdr2parts.ranges[i], bdr2parts.ranges[i + 1], true);
                for (int j = 0; j < parts.Length; j++)
                {
                    dc.Add(parts[j], System.Drawing.Color.Green, i);
                }
            }
#endif
            // Stücke zusammensammeln:
            bool onBdr1;
            Pair<double, double> act;
            if (bdr1parts.hasInterval)
            {
                onBdr1 = true;
                act = bdr1parts.removeInterval(-1);
            }
            else
            {
                onBdr1 = false;
                act = bdr2parts.removeInterval(-1);
            }
            List<ICurve2D> currentCollection = new List<ICurve2D>();
            List<Border> borders = new List<Border>();
            while (act.First != -1)
            {
                if (onBdr1)
                {
                    ICurve2D[] parts = bdr1.GetPart(act.First, act.Second, true);
                    currentCollection.AddRange(parts);
                }
                else
                {
                    ICurve2D[] parts = bdr2.GetPart(act.First, act.Second, true);
                    if (op == op.subtract)
                    {   // das Ergebnis muss umgedreht werden!
                        Array.Reverse(parts);
                        for (int i = 0; i < parts.Length; i++)
                        {
                            parts[i].Reverse();
                        }
                    }
                    currentCollection.AddRange(parts);
                }
                if ((currentCollection[0].StartPoint | currentCollection[currentCollection.Count - 1].EndPoint) < precision)
                {   // geschlossen, Border machen
                    borders.Add(new Border(currentCollection.ToArray()));
                    // neu anfangen
                    currentCollection.Clear();
                    if (bdr1parts.hasInterval)
                    {
                        onBdr1 = true;
                        act = bdr1parts.removeInterval(-1);
                    }
                    else if (bdr2parts.hasInterval)
                    {
                        onBdr1 = false;
                        act = bdr2parts.removeInterval(-1);
                    }
                    else
                    {
                        break; // es geht nicht mehr weiter, alle Intervalle sind aufgebraucht
                    }
                }
                else
                {
                    // nächstes Teilstück auf dem anderen Border suchen
                    if (onBdr1)
                    {
                        if (act.Second == bdr1.Segments.Length && bdr1parts.hasInterval && bdr1parts.ranges[0] == 0.0)
                        {   // es geht im selben Border über "Los"
                            act = bdr1parts.removeInterval(0.0);
                        }
                        else
                        {
                            for (int i = 0; i < borderIntersection.Count; i++)
                            {
                                if (borderIntersection[i].par1 == act.Second) // überprüfung auf Gleichheit ist ok, da dieser Parameter nur einmal berechnet wird
                                {
                                    if (op == op.subtract)
                                        act = bdr2parts.removeIntervalEnd(borderIntersection[i].par2);
                                    else
                                        act = bdr2parts.removeInterval(borderIntersection[i].par2);
                                    onBdr1 = false;
                                    borderIntersection.RemoveAt(i); // wird nur einmal gebraucht
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (op == op.subtract)
                        {   // das 2. Stück läuft rückwärts
                            if (act.First == 0.0 && bdr2parts.hasInterval && bdr2parts.ranges[bdr2parts.ranges.Count - 1] == bdr2.Segments.Length)
                            {   // es geht im selben Border über "Los"
                                act = bdr2parts.removeInterval(bdr2parts.ranges[bdr2parts.ranges.Count - 2]); // das letzte Stück liefern
                            }
                            else
                            {
                                for (int i = 0; i < borderIntersection.Count; i++)
                                {
                                    if (borderIntersection[i].par2 == act.First)
                                    {
                                        act = bdr1parts.removeInterval(borderIntersection[i].par1);
                                        onBdr1 = true;
                                        borderIntersection.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (act.Second == bdr2.Segments.Length && bdr2parts.hasInterval && bdr2parts.ranges[0] == 0.0)
                            {   // es geht im selben Border über "Los"
                                act = bdr2parts.removeInterval(0.0);
                            }
                            else
                            {
                                for (int i = 0; i < borderIntersection.Count; i++)
                                {
                                    if (borderIntersection[i].par2 == act.Second)
                                    {
                                        act = bdr1parts.removeInterval(borderIntersection[i].par1);
                                        onBdr1 = true;
                                        borderIntersection.RemoveAt(i);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (op == op.unite)
            {   // hier gibt es nur eine Hülle, das andere sind Löcher
                if (borders.Count == 1)
                {
                    return new CompoundShape(new SimpleShape(borders[0]));
                }
                else if (borders.Count > 1)
                {
                    borders.Sort(new BorderAreaComparer());
                    Border outline = borders[borders.Count - 1]; // die größte
                    borders.RemoveAt(borders.Count - 1);
                    return new CompoundShape(new SimpleShape(outline, borders.ToArray()));
                }
            }
            else
            {   // hier gibt es mehrere Hüllen, aber keine Löcher
                if (borders.Count > 0)
                {
                    SimpleShape[] ss = new SimpleShape[borders.Count];
                    for (int i = 0; i < borders.Count; ++i)
                    {
                        ss[i] = new SimpleShape(borders[i] as Border);
                    }
                    return new CompoundShape(ss);
                }
            }
            return new CompoundShape(); // leeres liefern
        }

        BoundingRect ext(quad quad)
        {
            return new BoundingRect(offx + fct * quad.x, offy + fct * quad.y, offx + fct * (quad.x + quad.w), offy + fct * (quad.y + quad.w));
        }
        double left(quad quad)
        {
            return offx + fct * quad.x;
        }
        double right(quad quad)
        {
            return offx + fct * (quad.x + quad.w);
        }
        double bottom(quad quad)
        {
            return offy + fct * quad.y;
        }
        double top(quad quad)
        {
            return offy + fct * (quad.y + quad.w);
        }

        intersection[] GetIntersections(quad quad, side side, bool onBorder1)
        {
            BoundingRect ext = this.ext(quad);
            double dxy;
            int xy;
            bool hor;
            double min, max;
            switch (side)
            {
                case BorderQuadTree.side.left:
                    xy = quad.x;
                    hor = false;
                    dxy = ext.Left;
                    break;
                case BorderQuadTree.side.right:
                    xy = quad.x + quad.w;
                    hor = false;
                    dxy = ext.Right;
                    break;
                case BorderQuadTree.side.bottom:
                    dxy = ext.Bottom;
                    xy = quad.y;
                    hor = true;
                    break;
                case BorderQuadTree.side.top:
                    dxy = ext.Top;
                    xy = quad.y + quad.w;
                    hor = true;
                    break;
                default: throw new ApplicationException("invalide side"); // kommt nicht vor
            }
            Dictionary<int, intersection[]> toWorkWith;
            if (hor)
            {
                if (onBorder1) toWorkWith = horIntSect1;
                else toWorkWith = horIntSect2;
                min = ext.Left;
                max = ext.Right;
            }
            else
            {
                if (onBorder1) toWorkWith = verIntSect1;
                else toWorkWith = verIntSect2;
                min = ext.Bottom;
                max = ext.Top;
            }
            intersection[] ints;
            if (!toWorkWith.TryGetValue(xy, out ints))
            {
                List<intersection> lints = new List<intersection>();
                Border bdr;
                if (onBorder1) bdr = bdr1;
                else bdr = bdr2;
                double[] oi = bdr.GetOrthoIntersection(hor, dxy, precision);
                for (int i = 0; i < oi.Length; i += 3)
                {
                    lints.Add(new intersection(oi[i], oi[i + 1], oi[i + 2]));
                }
                lints.Sort();
                for (int i = lints.Count - 1; i >= 0; --i)
                {
                    int n = (i + 1) % lints.Count;
                    if (Math.Abs(lints[i].xy - lints[n].xy) < precision)
                    {   // zwei Schnittpunkte sind zu dicht beieinander, gewöhnlich eine Spitze getroffen
                        // blöd wäre eine Einschnürung, die über eine lange Strecke geht, dann kommen wir immer hier rein
                        // doppelte Schnittpunkte treten auf, wenn genau durch einen Eckpunkt geschnitten wird. Die werden entfernt
                        if (Math.Abs(lints[i].par - lints[n].par) < 0.1 && Math.Abs(lints[i].dir - lints[n].dir) < 0.1)
                        {   // den gleichen Schnittpunkt entfernen. Wenn diese Entfernung einen logischen Fehler generiert
                            // dann wird der später wieder erkannt
                            lints.RemoveAt(i);
                            continue;
                        }
                        GeoPoint2D pos;
                        if (hor) pos = new GeoPoint2D(dxy, lints[i].xy);
                        else pos = new GeoPoint2D(lints[i].xy, dxy);
                        throw new CriticalPosition(pos, onBorder1, CriticalPosition.Reason.close);
                    }
                    if ((hor && (lints[i].dir > 2 * Math.PI - radianEps || lints[i].dir < radianEps || (Math.Abs(Math.PI - lints[i].dir) < radianEps))) ||
                        (!hor && (Math.Abs(Math.PI / 2.0 - lints[i].dir) < radianEps || Math.Abs(3 * Math.PI / 2.0 - lints[i].dir) < radianEps)))
                    {   // der Schnittpunkt ist tangential
                        GeoPoint2D pos;
                        if (hor) pos = new GeoPoint2D(dxy, lints[i].xy);
                        else pos = new GeoPoint2D(lints[i].xy, dxy);
                        throw new CriticalPosition(pos, onBorder1, CriticalPosition.Reason.tangential);
                    }
                }
                if ((lints.Count & 0x1) == 0x1)
                {   // die Anzahl der Schnittpunkte muss gerade sein
                    throw new CriticalPosition(GeoPoint2D.Origin, onBorder1, CriticalPosition.Reason.oddnumber);
                }
                toWorkWith[xy] = ints = lints.ToArray();
            }
            List<intersection> hits = new List<intersection>();
            for (int i = 0; i < ints.Length; i++)
            {
                if (ints[i].xy >= min && ints[i].xy <= max) hits.Add(ints[i]);
            }
            return hits.ToArray();
        }

#if DEBUG
        DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                root.debug(res);
                res.Add(bdr1, System.Drawing.Color.Red, 1);
                res.Add(bdr2, System.Drawing.Color.Green, 2);
                return res;
            }
        }
#endif
    }
}
