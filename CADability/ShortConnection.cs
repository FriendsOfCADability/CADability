using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using Wintellect.PowerCollections;

namespace CADability
{
    /// <summary>
    /// Tries to find a good solution for the (metric) "travelling salesman problem".
    /// This is still prliminary and may change in future.
    /// </summary>

    public class ShortConnection
    {
        class vertex : IQuadTreeInsertable
        {
            public GeoPoint2D position;
            public object tag;
#if DEBUG
            public vertex _next; // mit dem gehts weiter
            public vertex _previous; // der Vorgänger
            public vertex next
            {
                get
                {
                    return _next;
                }
                set
                {
                    _next = value;
                }
            }
            public vertex previous
            {
                get
                {
                    return _previous;
                }
                set
                {
                    _previous = value;
                }
            }
#else
            public vertex next; // mit dem gehts weiter
            public vertex previous; // der Vorgänger
#endif
            public connection toNext;
            public bool isFixStartPoint, isFixEndPoint; // ist starrer Anfangs- bzw. Endpunkt einer festen Verbindung
            public bool keepConnection; // die von hier ausgehende verbindung muss bestehen bleiben
            public int id; // da keine neuen erzeugt werden ist das einfach die fortlaufende Nummer. Gut für Sets etc.
            public vertex(GeoPoint2D position, object tag, int id)
            {
                this.position = position;
                this.tag = tag;
                this.id = id;
            }
            #region IQuadTreeInsertable Members

            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return new BoundingRect(position);
            }

            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                return rect.Contains(position);
            }

            object IQuadTreeInsertable.ReferencedObject
            {
                get { throw new NotImplementedException(); }
            }

            #endregion
            public override int GetHashCode()
            {
                return id;
            }
            public override bool Equals(object obj)
            {
                vertex other = obj as vertex;
                if (other != null) return id.Equals(other.id);
                return false;
            }

            public bool IsFollowedBy(vertex vertex)
            {
                for (vertex v = this; v != null; v = v.next)
                {
                    if (v == vertex) return true;
                }
                return false;
            }
        }
        class connection : IQuadTreeInsertable
        {
            public vertex from, to;
            public int id;
            public bool isFixed; // darf nicht aufgetrennt werden
            public connection(vertex from, vertex to)
            {
                this.from = from;
                this.to = to;
                if (from.id < to.id)
                {
                    id = (from.id << 16) + to.id;
                }
                else
                {
                    id = (to.id << 16) + from.id;
                }
                isFixed = from.keepConnection;
            }

            public double Length
            {
                get
                {
                    return from.position | to.position;
                }
            }
            public GeoPoint2D center
            {
                get
                {
                    return new GeoPoint2D(from.position, to.position);
                }
            }
            #region IQuadTreeInsertable Members

            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return new BoundingRect(from.position, to.position);
            }

            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                ClipRect clr = new ClipRect(rect);
                return clr.LineHitTest(from.position, to.position);
            }

            object IQuadTreeInsertable.ReferencedObject
            {
                get { throw new NotImplementedException(); }
            }

            #endregion
            public override int GetHashCode()
            {   // verschieden orientierte connections sind identisch
                return id;
            }
            public override bool Equals(object obj)
            {
                connection other = obj as connection;
                if (other != null) return id.Equals(other.id);
                return false;
            }
#if DEBUG
            public Line2D Debug
            {
                get
                {
                    return new Line2D(from.position, to.position);
                }
            }
#endif
        }
        class crossing : IComparable
        {
            public connection first;
            public connection second;
            public double saved;
            //public int hashCode;
            public crossing(connection first, connection second)
            {
                this.first = first;
                this.second = second;
                this.saved = (first.from.position | second.from.position) + (first.to.position | second.to.position) - first.Length - second.Length;
            }
            #region IComparable Members
            int IComparable.CompareTo(object obj)
            {
                crossing other = obj as crossing;
                if (other != null)
                {
                    return saved.CompareTo(other.saved);
                }
                return 0;
            }
            #endregion
        }
        QuadTree<vertex> quadTree;
        vertex[] vertices;
        vertex first, last;
        BoundingRect extension;
        double diagonal;
        double minSearchRadius;

        const double betterFactor = 0.95; // Faktor dafür, dass nicht der nächstgelegene Punkt verwendet wird
        // sondern der nächstgelegene Punkt ein neues Verbundstück beginnt. Wichtig bei rasterförmigen Punkten


        public ShortConnection(GeoPoint2D[] points, object[] tags, int startWith, int endWith)
        {

            extension = BoundingRect.EmptyBoundingRect;
            vertices = new vertex[points.Length];
            first = null;
            last = null;
            for (int i = 0; i < points.Length; i++)
            {
                extension.MinMax(points[i]);
                vertices[i] = new vertex(points[i], tags[i], i);
#if DEBUG
                System.Globalization.NumberFormatInfo numberFormatInfo = (System.Globalization.NumberFormatInfo)System.Globalization.CultureInfo.InvariantCulture.NumberFormat.Clone();
                System.Diagnostics.Trace.WriteLine("points[" + i.ToString() + "] = new GeoPoint2D(" + points[i].x.ToString(numberFormatInfo) + ", " + points[i].y.ToString(numberFormatInfo) + ");");
#endif
            }

            extension.Inflate(extension.Size / 10000);// ein bisschen größer und nicht entartet
            quadTree = new QuadTree<vertex>(extension);
            for (int i = 0; i < vertices.Length; i++)
            {
                quadTree.AddObject(vertices[i]);
            }
            if (startWith >= 0) first = vertices[startWith];
            if (endWith >= 0) last = vertices[endWith];
            diagonal = Math.Sqrt(extension.Width * extension.Width + extension.Height * extension.Height);
            minSearchRadius = diagonal / Math.Sqrt(points.Length);
        }
        // Testdaten hier: http://www.tsp.gatech.edu/data/index.html

        // wie es weitergehen soll: Stand 15.12.11.
        // Bislang gibt es:
        // Die Startbedingung: alle kürzestmöglichen Abstände werden verwendet, ich denke, das ist optimal
        // Die Optimierung (1) mit den sich schneidenden Verbindungen: Diese sind einfach zu finden (Listen vom QuadTree)
        // und einfach aufzudröseln.
        // die Optimierung (2) einen Punkt in eine andere Verbindung zu hängen, wenn der neue Umweg mehr Einsparung bringt.
        // die Optimierung mit zwei Punkten in eine Verbindung einzuhängen: die könnte noch verbessert werden
        // indem man nach vorne und hinten weitersucht und eine ganze Kette "umbettet"
        // was noch fehlt: es bleiben manchmal lange Verbindungen übrig, die sicherlich nicht optimal sind. Diese
        // verbinden zwei Teile, die vielleicht nicht anders verbunden werden können. Idee zur Verbesserung: suche 
        // zwei Kanten der beiden verschiedenen Teile, die nahe beieinander liegen. Brich diese auf und verbind damit
        // die beiden Teile. Die beiden noch offenen Enden werden mit den beiden Enden der langen Kante verbunden.
        // Die lange Kante wird entfernt. Das führt sicherlich erstmal zu einem schlechteren Gesamtwert, deshalb
        // muss man die Situation vorher speichern. Aber es ist gut möglich, dass Optimierungen der Art (1) und (2)
        // schnell wieder das Ergebnis verbessern über das bisherige Optimum hinaus. Allerdings darf man nicht zuerst 
        // die Überkreuzungen entfernen, sonst macht man die lange Kante gleich wieder hin.
        // Es gibt auch reichlich Literatur unter Wikipedia
        public void SetFixedConnection(int[] fixedPairs, bool mayReverse)
        {
#if DEBUG
            for (int i = 0; i < fixedPairs.Length; ++i)
            {
                System.Diagnostics.Trace.WriteLine("fx[" + i.ToString() + "] = " + fixedPairs[i].ToString() + ";");
            }
#endif
            for (int i = 0; i < fixedPairs.Length; i += 2)
            {
                Connect(vertices[fixedPairs[i]], vertices[fixedPairs[i + 1]]);
                vertices[fixedPairs[i]].keepConnection = true;
                if (!mayReverse)
                {
                    vertices[fixedPairs[i]].isFixStartPoint = true;
                    vertices[fixedPairs[i + 1]].isFixEndPoint = true;
                }
            }
        }

        public object[] GetRoutet()
        {
            vertex startWith;
            if (first != null) startWith = first;
            else startWith = vertices[0];
            while (startWith != null)
            {
                vertex start, end;
                FindStartEnd(startWith, out start, out end);
                // unser segment geht jetzt von start bis end
                vertex v1 = FindClosest(start);
                vertex v2 = null;
                if (end != start) v2 = FindClosest(end);
                if (v1 == null && v2 == null) break;
                if (v1 != null)
                {
                    double d1 = v1.position | start.position;
                    vertex better;
                    if (v2 != null)
                    {
                        double d2 = v2.position | end.position;
                        if (d2 < d1)
                        {
                            better = FindClosest(v2);
                            if (better != null && better != end && (better.position | v2.position) < d2 * betterFactor)
                            {
                                if (mayConnect(better, v2))
                                {
                                    startWith = v2;
                                    // von dem gefundenen nächsten gibt es eine bessere Verbindung
                                    // Schade, dass wir nicht "better" als bereits gefundenes nächstes für die nächste Runde mitgeben können
                                    continue;
                                }
                            }
                            if (Connect(end, v2))
                            {
                                startWith = v2;
                                continue;
                            }
                        }
                    }
                    better = FindClosest(v1);
                    if (better != null && better != start && (better.position | v1.position) < d1 * betterFactor)
                    {
                        if (mayConnect(better, v1))
                        {
                            startWith = v1;
                            // von dem gefundenen nächsten gibt es eine bessere Verbindung
                            // Schade, dass wir nicht "better" als bereits gefundenes nächstes für die nächste Runde mitgeben können
                            continue;
                        }
                    }
                    if (Connect(start, v1))
                    {
                        startWith = v1;
                        continue;
                    }
                }
                else if (v2 != null)
                {
                    vertex better;
                    double d2 = v2.position | end.position;
                    better = FindClosest(v2);
                    if (better != null && better != end && (better.position | v2.position) < d2 * betterFactor)
                    {
                        if (mayConnect(better, v2))
                        {
                            startWith = v2;
                            // von dem gefundenen nächsten gibt es eine bessere Verbindung
                            // Schade, dass wir nicht "better" als bereits gefundenes nächstes für die nächste Runde mitgeben können
                            continue;
                        }
                    }
                    if (Connect(end, v2))
                    {
                        startWith = v2;
                        continue;
                    }
                }
                else
                {
                    startWith = quadTree.SomeObject; // fertig, wenns nix mehr gibt
                    vertex[] debugall = quadTree.GetAllObjects();
                    if (debugall.Length <= 1) startWith = null;
                }
            }
            // jetzt verbessern:
            // suche zu einem Vertex eine naheliegende Linie, die nicht den Vertex selbst verbindet
            // Wenn die Ersparniss durch Entfernen des Vertexes aus dem Pfad größer ist als die Kosten
            // ihn neu einzufügen, dann tue das.
            if (first != null) startWith = first;
            else startWith = vertices[0];
            while (startWith.previous != null) startWith = startWith.previous; // ganz an den Anfang gehen, offene Liste
            // first = startWith; // warum, das mach first kaputt!
            // ab hier steht startWith ganz am Anfang
            double lastlen = TotalLength * 2; // damit es einmal losgeht
            while (lastlen > TotalLength)
            {
                lastlen = TotalLength;
                Improve();
                ImprovePermutateConnections();
            }

            object[] res = new object[vertices.Length];
            int i = 0;
            for (vertex v = startWith; v != null; v = v.next)
            {
                res[i] = v.tag;
                ++i;
            }
            return res;
        }

        private void ImprovePermutateConnections()
        {   // hier geht es darum, die längsten Verbindungen zu entfernen und die entstehenden Bruchstücke besser zu verbinden
            // Die längsten Verbindungen sind deshalb Kandidaten hier, da bei ihrer erstmaligen Erzeugung keine große Freiheit mehr bestand.
            // Außerdem sind die Gewinnchancen höher.

            // Es werden jeweils 3 aus n (maxConnectionToCheck) Kandidaten entfernt und mit allen möglichen premutationen die Bruchstücke wieder
            // zusammengefügt. Wenn der erste Kandidat feststeht, sind das nur 6 mögliche Permutationen (wobei eine ja verwirklicht ist)

            // Im Prinzip kannd dieses Verfahren auch auf 4 oder mehr Verbindungen ausgedehnt werden, wobei die Anzahl der möglichen Permutationen start zunimmt 
            // (bei 4 (mit festem Start) 24, bei 5 120 u.s.w.) Im Gegenzug müsste man maxConnectionToCheck verringern, damit die Rechenzeit akzeptabel bleibt.

            // Wenn man mit Umdrehen arbeitet, gibt es mehr Möglichkeiten und es macht auch schon mit 2 Verbindungen Sinn.
            // Das ist aber noch nicht implementiert. 
            const int maxConnectionToCheck = 50; // maximale Anzahl der Verbindungen, die hier überprüft werden
            // Bei 10 Verbindungen sind das 10*9*8, also 720 Möglichkeiten, also Aufrufe von Recombine,
            // bei 20 Verbindungen sind es 21*19*18, also 6840, bei 30: 24360
            vertex startWith;
            if (first != null) startWith = first;
            else startWith = vertices[0];
            while (startWith.previous != null) startWith = startWith.previous; // ganz an den Anfang gehen, offene Liste

            bool improved = true;
            while (improved)
            {
                improved = false;
                // 1. bestimme die "Ausreißer", Verbindungen die viel länger als der Durchschnitt sind
                OrderedMultiDictionary<double, int> sortedConnections = new OrderedMultiDictionary<double, int>(true);
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].toNext != null && !vertices[i].keepConnection)
                    {
                        sortedConnections.Add(-vertices[i].toNext.Length, i); // das "-", damit die längste Kante zuerst kommt
                    }
                }
                int[] connectionsToCheck = new int[Math.Min(maxConnectionToCheck, sortedConnections.Count)];
                int index = 0;
                foreach (KeyValuePair<double, ICollection<int>> item in sortedConnections)
                {
                    foreach (int ind in item.Value)
                    {
                        connectionsToCheck[index] = ind;
                        ++index;
                        if (index >= connectionsToCheck.Length) break;
                    }
                    if (index >= connectionsToCheck.Length) break;
                }
                // 2. connectionsToCheck enthält jetzt die Liste der Längsten Verbindungen
                // Wähle 3 davon aus und entferne diese Verbindungen. Es entstehen 4 Reststücke
                // Versuche diese besser zu kombinieren. Der Maximale Schleifendurchlauf ist durch maxConnectionToCheck gegeben
                for (int i = 0; i < connectionsToCheck.Length; i++)
                {
                    for (int j = i + 1; j < connectionsToCheck.Length; j++)
                    {
                        for (int k = j + 1; k < connectionsToCheck.Length; k++)
                        {
                            improved = Recombine(connectionsToCheck[i], connectionsToCheck[j], connectionsToCheck[k], startWith);
                            if (improved) break;
                        }
                        if (improved) break;
                    }
                    if (improved) break;
                }
            }
        }

        private static int[,] permutations = new int[,] { { 0, 1, 2, 3 }, { 0, 1, 3, 2 }, { 0, 2, 1, 3 }, { 0, 2, 3, 1 }, { 0, 3, 1, 2 }, { 0, 3, 2, 1 } };
        private bool Recombine(int i, int j, int k, vertex startWith)
        {
            // es entstehen 4 Stücke wenn die 3 Verbindungen entfernt werden
            // wenn der Anfang fest ist, bleibt eines der Anfang, es gibt dann 6 Möglichkeiten, wenn nicht, gibt es 24 Möglichkeiten
            // 1. Anfangs und Endpunkte der Stücke suchen
            Pair<int, int>[] segments = new Pair<int, int>[4];
            int index = 0;
            int start = startWith.id; // id ist auch der Index im Array
            vertex v;
            for (v = startWith; v.next != null; v = v.next)
            {
                if (v.id == i || v.id == j || v.id == k)
                {
                    segments[index] = new Pair<int, int>(start, v.id);
                    ++index;
                    start = v.next.id; // Start für die nächste Runde
                }
            }
            segments[index] = new Pair<int, int>(start, v.id); // das letzte Stück
            if (index != 3) return false; // kann nicht vorkommen
            // jetzt gibt es 4 Segmente und es können drei neue Verbindungen überprüft werden
            double orgLength = vertices[i].toNext.Length + vertices[j].toNext.Length + vertices[k].toNext.Length;
            double minLength = orgLength - this.diagonal * 1e-6; // bei Gleichheit keine Verbesserung
            int bestPermutation = -1;
            // das erste Segment bleibt immer das erste und es wird nicht umgedreht (diese Einschränkung kann man später aufheben)
            for (int si = 0; si < permutations.GetLength(0); si++)
            {
                double len = 0.0;
                for (int l = 0; l < 3; l++)
                {
                    vertex v1 = vertices[segments[permutations[si, l]].Second];
                    vertex v2 = vertices[segments[permutations[si, l + 1]].First];
                    len += v1.position | v2.position;
                }
                if (len < minLength)
                {
                    minLength = len;
                    bestPermutation = si;
                }
            }
            if (bestPermutation >= 0)
            {   // eine bessere Lösung gefunden
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                CADability.Attribute.ColorDef cnew = new CADability.Attribute.ColorDef("neu", System.Drawing.Color.Green);
                CADability.Attribute.ColorDef cold = new CADability.Attribute.ColorDef("alt", System.Drawing.Color.Gray);
                for (int l = 0; l < 3; l++)
                {
                    vertex v1 = vertices[segments[permutations[bestPermutation, l]].Second];
                    vertex v2 = vertices[segments[permutations[bestPermutation, l + 1]].First];
                    Line line = Line.Construct();
                    line.SetTwoPoints(new GeoPoint(v1.position), new GeoPoint(v2.position));
                    line.ColorDef = cnew;
                    dc.Add(line, l);
                    v1 = vertices[segments[l].Second];
                    v2 = vertices[segments[l + 1].First];
                    line = Line.Construct();
                    line.SetTwoPoints(new GeoPoint(v1.position), new GeoPoint(v2.position));
                    line.ColorDef = cold;
                    dc.Add(line, l);
                }
#endif
                // die alten Verbindungen auflösen
                for (int l = 0; l < 3; l++)
                {
                    vertex v1 = vertices[segments[l].Second];
                    vertex v2 = vertices[segments[l + 1].First];
                    v1.next = null;
                    v2.previous = null;
                    connectionTree.RemoveObject(v1.toNext);
                    v1.toNext = null;
                }
                // die neuen Verbindungen einfügen
                for (int l = 0; l < 3; l++)
                {
                    vertex v1 = vertices[segments[permutations[bestPermutation, l]].Second];
                    vertex v2 = vertices[segments[permutations[bestPermutation, l + 1]].First];
                    v1.next = v2;
                    v2.previous = v1;
                    connection con = new connection(v1, v2);
                    v1.toNext = con;
                    connectionTree.AddObject(con); // brauchen wir den Connection tree überhaupt noch???
                }
#if DEBUG
                if (!DebugOK) throw new ApplicationException("Vorsicht!!!");
                if (connectionTree.GetAllObjects().Length != vertices.Length - 1) throw new ApplicationException("connections falsch!!!");
                ConnectionsOK();
#endif
                return true;
            }
            return false;
        }

        QuadTree<connection> connectionTree;
        class improvement
        {
            public double saved;
            public vertex remove;
            public vertex insertAfter;
            public improvement(double saved, vertex remove, vertex insertAfter)
            {
                this.saved = saved;
                this.remove = remove;
                this.insertAfter = insertAfter;
            }
        }
        private void Improve()
        {
            quadTree.RemoveObjects(new List<vertex>(quadTree.AllObjects()));
            connectionTree = new QuadTree<connection>(extension);
            // die beiden QuadTrees aufladen:
            for (int i = 0; i < vertices.Length; i++)
            {
                quadTree.AddObject(vertices[i]);
                if (vertices[i].next != null)
                {
                    connection con = new connection(vertices[i], vertices[i].next);
                    vertices[i].toNext = con;
                    connectionTree.AddObject(con);
                }
            }
#if DEBUG
            if (!DebugOK) throw new ApplicationException("Vorsicht!!!");
            if (connectionTree.GetAllObjects().Length != vertices.Length - 1) throw new ApplicationException("connections falsch!!!");
            ConnectionsOK();
#endif

            Boolean didChange;
            do
            {
                // 1. Überkreuzungen sind eigentlich immer schlecht. Diese hier beseitigen
                didChange = false;
                // 1.1: alle Überkreuzungen ansammeln durch betrachten der Listen im QuadTree
                Set<crossing> crossings = new Set<crossing>();
                int dbgnum = 0;
                foreach (List<connection> list in connectionTree.AllLists)
                {
                    ++dbgnum;
                    for (int i = 0; i < list.Count - 1; i++)
                    {
                        if (list[i].isFixed) continue;
                        for (int j = i + 1; j < list.Count; j++)
                        {
                            if (list[j].isFixed) continue;
                            if (Geometry.InnerIntersection(list[i].from.position, list[i].to.position, list[j].from.position, list[j].to.position))
                            {
                                if (list[i].from.position != list[i].to.position && list[j].from.position != list[j].to.position)
                                {   // null-Linien nicht berücksichtigen
                                    if (list[i].to.toNext != null && list[j].to.toNext != null)
                                    {   // die Enden gehen nicht
                                        crossings.Add(new crossing(list[i], list[j]));
                                    }
                                }
                            }
                        }
                    }
                }
                // crossings enthält jetzt alle Überkreuzungen, nach eingesparten Längen sortieren
                List<crossing> sortedCrossings = new List<crossing>(crossings);
                sortedCrossings.Sort(); // implementiert IComparable
                // mit den besten beginnend alle entflechten. Aber wenn einer der betreffenden vertices bereits in einer
                // Entflechtung beteiligt war, dann nicht verwenden, die Überkreuzung könnte bereits behoben sein.
                Set<vertex> usedVertices = new Set<vertex>();
                foreach (crossing crossing in sortedCrossings)
                {
                    if (!usedVertices.Add(crossing.first.from) &&
                        !usedVertices.Add(crossing.first.to) &&
                        !usedVertices.Add(crossing.second.from) &&
                        !usedVertices.Add(crossing.second.to))
                    {
                        // kann gefahrlos entkreuzt werden, kein Konflikt mit anderen crossings
                        //usedVertices.Add(crossing.First.from); // add wird im if schon gemacht. Damit wird nur einmal
                        // auf den Set zugegriffen, möglicherweise werden zu viele hinzugefügt, aber es gibt ja noch weitere Durchläufe...
                        //usedVertices.Add(crossing.First.to);
                        //usedVertices.Add(crossing.Second.from);
                        //usedVertices.Add(crossing.Second.to);
                        // entflechten:
                        // in der Reihenfolge entflechten, dass Anfang und Ende der ganzen Kurve unbehelligt bleiben
                        // und das Zwischenstück umgedreht wird (noch berücksichtigen ob umdrehen erlaubt ist)
                        bool followed = crossing.first.to.IsFollowedBy(crossing.second.from);
                        connection con1, con2;
                        if (followed)
                        {
                            con1 = crossing.first;
                            con2 = crossing.second;
                        }
                        else
                        {
                            con1 = crossing.second;
                            con2 = crossing.first;
                        }
                        vertex c11 = con1.from;
                        vertex c12 = con1.to;
                        vertex c21 = con2.from;
                        vertex c22 = con2.to;
                        bool mayReverse = true;
                        for (vertex vv = c22; vv != c21; vv = vv.next)
                        {
                            if (vv == null)
                            {
                                // nicht sicher ob das so OK ist ...
                                // mayReverse = false;
                                break;
                            }
                            else
                            {
                                if (vv.isFixEndPoint || vv.isFixStartPoint)
                                {
                                    mayReverse = false;
                                    break;
                                }
                            }
                        }
                        if (mayReverse)
                        {
                            c11.next = null;
                            c12.previous = null;
                            c21.next = null;
                            c22.previous = null;
                            connectionTree.RemoveObject(con1);
                            connectionTree.RemoveObject(con2);
                            // und alle connections aus dem QuadTree raus
                            for (vertex vv = c12; vv != null; vv = vv.next)
                            {
                                connectionTree.RemoveObject(vv.toNext);
                                usedVertices.Add(vv);
                            }
                            Reverse(c21, false);
                            // c21 ist jetzt der Anfang, der Pfad ist aber immer noch rausgeschnitten
                            for (vertex vv = c21; vv != null; vv = vv.next)
                            {
                                if (vv.next != null)
                                {
                                    vv.toNext = new connection(vv, vv.next);
                                    connectionTree.AddObject(vv.toNext);
                                }
                            }
                            c11.next = c21;
                            c21.previous = c11;
                            c12.next = c22;
                            c22.previous = c12;
                            c11.toNext = new connection(c11, c11.next);
                            c12.toNext = new connection(c12, c12.next);
                            connectionTree.AddObject(c11.toNext);
                            connectionTree.AddObject(c12.toNext);
                            didChange = true;
                        }
#if DEBUG
                        if (!DebugOK) throw new ApplicationException("Vorsicht!!!");
                        if (connectionTree.GetAllObjects().Length != vertices.Length - 1) throw new ApplicationException("connections falsch!!!");
                        ConnectionsOK();
#endif
                    }

                }

                List<improvement> changes = new List<improvement>();
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertex v = vertices[i];
                    if (v.next != null && v.previous != null && !v.toNext.isFixed)
                    {
                        double lengthAdded;
                        connection con = FindBestConnection(v, out lengthAdded);
                        double lengthRemoved = v.toNext.Length + v.previous.toNext.Length - (v.previous.position | v.next.position);
                        if (lengthRemoved - lengthAdded > 0.0)
                        {
                            changes.Add(new improvement(lengthRemoved - lengthAdded, v, con.from));
                        }
                    }
                }
#if DEBUG
                DebuggerContainer dbg = this.Debug;
#endif
                usedVertices.Clear();
                changes.Sort(delegate (improvement first, improvement second) { return second.saved.CompareTo(first.saved); });
#if DEBUG
                DebuggerContainer dc2 = new DebuggerContainer();
                foreach (improvement improvement in changes)
                {
                    dc2.Add(new Line2D(improvement.remove.position, improvement.insertAfter.position), System.Drawing.Color.Blue, (int)improvement.saved);
                }
#endif
                foreach (improvement improvement in changes)
                {
                    vertex toRemove = improvement.remove;
                    vertex insertAfter = improvement.insertAfter;
                    vertex preToRemove = toRemove.previous;
                    vertex afterToRemove = toRemove.next;
                    vertex afterInsertAfter = insertAfter.next;
                    if (!usedVertices.Add(toRemove) &&
                        !usedVertices.Add(insertAfter) &&
                        !usedVertices.Add(preToRemove) &&
                        !usedVertices.Add(afterInsertAfter) &&
                        !usedVertices.Add(afterToRemove) &&
                        !toRemove.toNext.isFixed && // es dürfen keine fixierten Verbindungen entfernt werden
                        !toRemove.previous.toNext.isFixed &&
                        !insertAfter.toNext.isFixed
                        )
                    {
                        // die drei Kanten aus dem QuadTree entfernen
                        connectionTree.RemoveObject(toRemove.toNext);
                        connectionTree.RemoveObject(toRemove.previous.toNext);
                        connectionTree.RemoveObject(insertAfter.toNext);
                        // die drei neuen Kanten aufbauen
                        preToRemove.next = afterToRemove;
                        afterToRemove.previous = preToRemove;
                        insertAfter.next = toRemove;
                        toRemove.previous = insertAfter;
                        toRemove.next = afterInsertAfter;
                        afterInsertAfter.previous = toRemove;
                        // drei neue connections machen
                        insertAfter.toNext = new connection(insertAfter, insertAfter.next);
                        toRemove.toNext = new connection(toRemove, toRemove.next);
                        preToRemove.toNext = new connection(preToRemove, preToRemove.next);
                        // und in den QuadTree einfügen
                        connectionTree.AddObject(insertAfter.toNext);
                        connectionTree.AddObject(toRemove.toNext);
                        connectionTree.AddObject(preToRemove.toNext);
                        didChange = true;
#if DEBUG
                        if (!DebugOK) throw new ApplicationException("Vorsicht!!!");
                        if (connectionTree.GetAllObjects().Length != vertices.Length - 1) throw new ApplicationException("connections falsch!!!");
                        ConnectionsOK();
#endif
                    }
                }
                if (ImproveConnection())
                {
                    didChange = true;
#if DEBUG
                    if (!DebugOK) throw new ApplicationException("Vorsicht!!!");
                    if (connectionTree.GetAllObjects().Length != vertices.Length - 1) throw new ApplicationException("connections falsch!!!");
                    ConnectionsOK();
#endif
                }
            } while (didChange);
        }

        bool ImproveReverseFixedConnections()
        {   // bringt so nix
            connection[] all = connectionTree.GetAllObjects();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].isFixed)
                {
                    if (all[i].to.next != null && all[i].from.previous != null)
                    {
                        if (all[i].to.next.toNext != null && all[i].from.previous.toNext != null)
                        {
                            if (!all[i].to.next.toNext.isFixed && !all[i].from.previous.toNext.isFixed)
                            {
                                double l = all[i].to.next.toNext.Length + all[i].from.previous.toNext.Length;
                                double l1 = (all[i].to.position | all[i].to.next.position) + (all[i].from.position | all[i].from.previous.position);
                                if (l1 < l)
                                {
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        class connectionImprovement : IComparable
        {   // öffne die Verbindung toOpen, und füge das segment zwischen den beiden vertices neu ein, 
            // die Richtung wird mit forward gegeben
            public double saved; // die neue Länge, je kleiner umso besser
            public connection toOpen;
            public vertex startSegment, endSegment;
            public bool forward;
            public connectionImprovement(double saved, connection toOpen, vertex startSegment, vertex endSegment, bool forward)
            {
                this.saved = saved;
                this.toOpen = toOpen;
                this.startSegment = startSegment;
                this.endSegment = endSegment;
                this.forward = forward;
            }

            #region IComparable Members

            int IComparable.CompareTo(object obj)
            {
                connectionImprovement other = obj as connectionImprovement;
                if (other != null)
                {
                    return saved.CompareTo(other.saved);
                }
                return 0;
            }

            #endregion
        }
        bool ImproveConnection()
        {
            List<connectionImprovement> improvementList = new List<connectionImprovement>();
            for (int i = 0; i < vertices.Length; i++)
            {
                connection con = vertices[i].toNext;
                if (con != null && con.from.previous != null && con.to.next != null) // keine connections am Anfang/Ende verwenden
                {
                    double r = con.Length / 2.0;
                    BoundingRect ext = new BoundingRect(con.center, r, r);
                    connection[] close = connectionTree.GetObjectsFromRect(ext);
                    double mindist = double.MaxValue;
                    connection bestNeighbour = null;
                    bool forward = false;
                    for (int j = 0; j < close.Length; j++)
                    {
                        if (close[j] == con) continue;
                        if (close[j].to == con.from || close[j].from == con.to) continue; // keine zusammenhängenden connections überprüfen
                        if (close[j].to.next == con.from || close[j].from == con.to.next) continue;
                        // auch keine connections mit nur einer Lücke dazwischen überprüfen
                        if (close[j].to.next == null || close[j].from.previous == null) continue;
                        // keine connections am Anfang bzw Ende verwednen
                        double d1 = (close[j].from.position | con.from.position) + (close[j].to.position | con.to.position);
                        double d2 = (close[j].from.position | con.to.position) + (close[j].to.position | con.from.position);
                        double d = Math.Min(d1, d2);
                        if (d < mindist)
                        {
                            mindist = d;
                            bestNeighbour = close[j];
                            forward = d1 < d2;
                        }
                    }
                    if (bestNeighbour != null)
                    {
                        // versuche bestNeighbour in con zu integrieren, möglichst auch noch mehr nach vorne und hinten zu verlängern
                        vertex v1 = bestNeighbour.from;
                        vertex v2 = bestNeighbour.to;
                        vertex vstart, vend; // am nächsten zu Anfang und Ende von con
                        if (forward)
                        {
                            vstart = bestNeighbour.from;
                            vend = bestNeighbour.to;
                        }
                        else
                        {
                            vstart = bestNeighbour.to;
                            vend = bestNeighbour.from;
                        }
                        if (NoIntersection(vstart, con.from) && NoIntersection(vend, con.to))
                        {
                            double length = (con.to.position | vend.position) + (con.from.position | vstart.position)
                                + (v1.previous.position | v2.next.position) - con.Length - v1.previous.toNext.Length - v2.toNext.Length;
                            // das ist die neue Länge, wenn positiv, dann wirds länger, wenn negativ, dann kürzer.
                            if (length < 0.0)
                            {
                                improvementList.Add(new connectionImprovement(length, con, v1, v2, forward));
                            }
                        }
                    }
                }
            }
            improvementList.Sort();
            Set<vertex> usedVertices = new Set<vertex>();
            bool didImprove = false;
            for (int i = 0; i < improvementList.Count; i++)
            {
                connectionImprovement imp = improvementList[i];
                if (!usedVertices.Add(imp.toOpen.from) &&
                    !usedVertices.Add(imp.toOpen.to) &&
                    !usedVertices.Add(imp.startSegment) &&
                    !usedVertices.Add(imp.endSegment) &&
                    !imp.startSegment.previous.toNext.isFixed && // es dürfen keine gefixten Verbindungen entfernt werden
                    !imp.endSegment.toNext.isFixed &&
                    !imp.toOpen.isFixed &&
                    (imp.forward || mayReverse(imp.startSegment))
                    )
                {   // kann verwendet werden, da alle vertices frei sind.
                    // 1. startSegment bis ednSegment aushängen
#if DEBUG
                    DebuggerContainer dc3 = new DebuggerContainer();
                    dc3.Add(imp.startSegment.previous.toNext.Debug, System.Drawing.Color.Red, 1);
                    dc3.Add(imp.endSegment.toNext.Debug, System.Drawing.Color.Red, 2);
                    dc3.Add(imp.toOpen.Debug, System.Drawing.Color.Red, 3);
#endif
                    connectionTree.RemoveObject(imp.startSegment.previous.toNext);
                    connectionTree.RemoveObject(imp.endSegment.toNext);
                    connectionTree.RemoveObject(imp.toOpen);
                    imp.startSegment.previous.next = imp.endSegment.next;
                    imp.endSegment.next.previous = imp.startSegment.previous;
                    imp.startSegment.previous.toNext = new connection(imp.startSegment.previous, imp.startSegment.previous.next);
                    connectionTree.AddObject(imp.startSegment.previous.toNext);
#if DEBUG
                    dc3.Add(imp.startSegment.previous.toNext.Debug, System.Drawing.Color.Green, 1);
#endif
                    if (imp.forward)
                    {
                        imp.toOpen.from.next = imp.startSegment;
                        imp.startSegment.previous = imp.toOpen.from;
                        imp.toOpen.from.toNext = new connection(imp.toOpen.from, imp.toOpen.from.next);
                        connectionTree.AddObject(imp.toOpen.from.toNext);

                        imp.endSegment.next = imp.toOpen.to;
                        imp.endSegment.next.previous = imp.endSegment;
                        imp.endSegment.toNext = new connection(imp.endSegment, imp.endSegment.next);
                        connectionTree.AddObject(imp.endSegment.toNext);
#if DEBUG
                        dc3.Add(imp.toOpen.from.toNext.Debug, System.Drawing.Color.Green, 2);
                        dc3.Add(imp.endSegment.toNext.Debug, System.Drawing.Color.Green, 3);
#endif
                    }
                    else
                    {
                        imp.startSegment.previous = null;
                        imp.endSegment.next = null;
                        Reverse(imp.startSegment, true);

                        imp.toOpen.from.next = imp.endSegment;
                        imp.endSegment.previous = imp.toOpen.from;
                        imp.toOpen.from.toNext = new connection(imp.toOpen.from, imp.toOpen.from.next);
                        connectionTree.AddObject(imp.toOpen.from.toNext);

                        imp.startSegment.next = imp.toOpen.to;
                        imp.startSegment.next.previous = imp.startSegment;
                        imp.startSegment.toNext = new connection(imp.startSegment, imp.startSegment.next);
                        connectionTree.AddObject(imp.startSegment.toNext);
#if DEBUG
                        dc3.Add(imp.toOpen.from.toNext.Debug, System.Drawing.Color.Green, 2);
                        dc3.Add(imp.startSegment.toNext.Debug, System.Drawing.Color.Green, 3);
#endif
                    }
                    didImprove = true;
                }
            }
            return didImprove;
        }


        private bool NoIntersection(vertex v1, vertex v2)
        {
            connection[] close = connectionTree.GetObjectsCloseTo(new Line2D(v1.position, v2.position));
            for (int i = 0; i < close.Length; i++)
            {
                // nur solche connections testen, die nicht einen der beiden vertices enthalten
                if (close[i].from != v1 && close[i].from != v2 && close[i].to != v1 && close[i].to != v2)
                {
                    if (Geometry.SegmentIntersection(v1.position, v2.position, close[i].from.position, close[i].to.position))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool FindBestCrossing(vertex v, out connection con1, out connection con2)
        {
            con1 = null;
            con2 = null;
            return false;
        }

        private connection FindBestConnection(vertex v, out double lengthAdded)
        {   // überprüft noch nicht, ob eine der Verbindungen geschützt ist
            double radius = minSearchRadius;
            while (true)
            {   // das ist ein bisschen ungenau: es kann sein, dass eine connection, die weiter wegliegt
                // und vom QuadTree nicht geliefert wird, ein besseres Sparpotential hat. Ist aber relativ
                // unwahrscheinlich
                connection[] inSquare = connectionTree.GetObjectsFromRect(new BoundingRect(v.position, radius, radius));
                connection found = null;
                double minDist = double.MaxValue;
#if DEBUG
                DebuggerContainer dc3 = new DebuggerContainer();
#endif
                for (int i = 0; i < inSquare.Length; i++)
                {
                    if (!inSquare[i].isFixed && inSquare[i] != v.toNext && inSquare[i] != v.previous.toNext)
                    {
                        double d = (inSquare[i].from.position | v.position) + (inSquare[i].to.position | v.position) - (inSquare[i].Length);
#if DEBUG
                        dc3.Add(inSquare[i].Debug, System.Drawing.Color.Chartreuse, inSquare[i].id);
#endif
                        if (d < minDist)
                        {
                            minDist = d;
                            found = inSquare[i];
                        }
                    }
                }
                lengthAdded = minDist;
                if (found != null) return found;
                radius *= 2;
                if (radius > diagonal) return null;
            }

        }

        private bool Connect(vertex v1, vertex v2)
        {   // verbindet v1 mit v2. 
            if (v2 == first || v1 == last)
            {
                vertex tmp = v1;
                v1 = v2;
                v2 = tmp;
            }
            if (v1 == first && v1.next != null) return false; // first kann niccht mehr verbunden werden
            if (v2 == last && v2.previous != null) return false; // last kann nicht mehr verbunden werden

            // also wenn first oder last vorkommen, dann first=v1 oder last==v2
            if (v1.next == null && v2.previous == null)
            {   // einfache Verbindung von v1 nach v2 ohne umdrehen
                v1.next = v2;
                v2.previous = v1;
                CheckUsed(v1);
                CheckUsed(v2);
                return true;
            }
            if (v1 == first)
            {
                if (v2.next == null && mayReverse(v2))
                {
                    Reverse(v2, false); // v2.previous ist jetzt null
                    v1.next = v2;
                    v2.previous = v1;
                    CheckUsed(v1);
                    CheckUsed(v2);
                    return true;
                }
                return false; // v1==first und v2 nicht umdrehbar
            }
            if (v2 == last)
            {
                if (v1.previous == null && mayReverse(v1))
                {
                    Reverse(v1, false); // v1.next ist jetzt null
                    v1.next = v2;
                    v2.previous = v1;
                    CheckUsed(v1);
                    CheckUsed(v2);
                    return true;
                }
                return false; // v2==last und v1 nicht umdrehbar
            }
            if (v1.previous == null && v2.next == null)
            {   // einfache Verbindung von v2 nach v1 ohne umdrehen und nicht first oder last
                v1.previous = v2;
                v2.next = v1;
                CheckUsed(v1);
                CheckUsed(v2);
                return true;
            }
            // jetzt muss eines von beiden umgedreht werden, first und last ist nicht dabei

            if (v1 != first && mayReverse(v1))
            {
                Reverse(v1, false);
                return Connect(v1, v2);
            }
            if (v2 != last && mayReverse(v2))
            {
                Reverse(v2, false);
                return Connect(v1, v2);
            }
            return false;
        }

        private void CheckUsed(vertex v)
        {   // aus dem quadtree entfernen, wenn es nicht mehr angeschlossen werden kann
            if (v.next != null && v.previous != null) quadTree.RemoveObject(v);
            if (v.next != null && v == first) quadTree.RemoveObject(v);
            if (v.previous != null && v == last) quadTree.RemoveObject(v);
        }

        private void Reverse(vertex v2, bool withConnections)
        {   
            // das ginge auch ohne die Liste!!!
            //if (!mayReverse(v2))
            //{
            //    int dbg = 0;
            //}
            List<vertex> path = new List<vertex>();
            for (vertex v = v2.previous; v != null; v = v.previous)
            {
                path.Insert(0, v);
            }
            for (vertex v = v2; v != null; v = v.next)
            {
                path.Add(v);
            }
            for (int i = 0; i < path.Count; i++)
            {
                if (i > 0)
                {
                    path[i].next = path[i - 1];
                }
                else
                {
                    path[i].next = null;
                }
                if (i < path.Count - 1)
                {
                    path[i].previous = path[i + 1];
                }
                else
                {
                    path[i].previous = null;
                }
            }
            for (int i = path.Count - 1; i > 0; --i)
            {
                path[i].keepConnection = path[i - 1].keepConnection;
            }
            path[0].keepConnection = false;
            if (withConnections)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    if (path[i].toNext != null) connectionTree.RemoveObject(path[i].toNext);
                    if (path[i].next != null)
                    {
                        path[i].toNext = new connection(path[i], path[i].next);
                        connectionTree.AddObject(path[i].toNext);
                    }
                }
            }
        }

        private void FindStartEnd(vertex startWith, out vertex start, out vertex end)
        {
            start = startWith;
            while (start.previous != null) start = start.previous;
            end = startWith;
            while (end.next != null) end = end.next;
        }
        vertex FindClosest(vertex toThis)
        {
            double radius = minSearchRadius;
            while (true)
            {
                vertex[] inSquare;
                if (radius > diagonal)
                {
                    inSquare = quadTree.GetAllObjects();
                }
                else
                {
                    inSquare = quadTree.GetObjectsFromRect(new BoundingRect(toThis.position, radius, radius));
                }
                vertex found = null;
                double minDist = double.MaxValue;
                for (int i = 0; i < inSquare.Length; i++)
                {
                    if (inSquare[i] != toThis)
                    {
                        double d = inSquare[i].position | toThis.position;
                        if (d <= radius && d < minDist)
                        {
                            if (mayConnect(toThis, inSquare[i]))
                            {
                                minDist = d;
                                found = inSquare[i];
                            }
                        }
                    }
                }
                if (found != null) return found;
                if (radius > diagonal) return null;
                radius *= 2;
            }
        }

        private bool mayConnect(vertex v1, vertex v2)
        {
            // keine Zyklen erzeugen:
            for (vertex v = v1; v != null; v = v.next)
            {
                if (v == v2) return false;
            }
            for (vertex v = v1.previous; v != null; v = v.previous)
            {
                if (v == v2) return false;
            }
            // Anfangs und Endpunkte können nur in eine Richtung verbinden
            // return false, wenn diese Richtung bereits besetzt
            if ((v1 == first || v1.isFixEndPoint) && v1.next != null) return false;
            if ((v1 == last || v1.isFixStartPoint) && v1.previous != null) return false;
            if ((v2 == first || v2.isFixEndPoint) && v2.next != null) return false;
            if ((v2 == last || v2.isFixStartPoint) && v2.previous != null) return false;
            // keine zwei Endpunkt oder zwei Startpunkte miteinander verbinden
            if (v1.isFixStartPoint && v2.isFixStartPoint) return false;
            if (v1.isFixEndPoint && v2.isFixEndPoint) return false;
            // der einfachste Fall
            if (v1.next == null && v2.previous == null) return true;
            if (!mayReverse(v1) || !mayReverse(v2))
            {   // eines der beiden Segmente kann nicht umgedreht werden
                if (v2.next == null && v1.previous == null && v1 != first && v2 != last) return true;
                return false;
            }
            else
            {
                return (v1.previous == null || v1.next == null) && (v2.previous == null || v2.next == null);
                // das sollte eigentlich immer true sein
            }
        }

        private bool mayReverse(vertex v1)
        {
            if (first != null && first.IsFollowedBy(v1)) return false;
            if (last != null && v1.IsFollowedBy(last)) return false;
            // wenn isFixStartPoint oder isFixEndPoint gesetzt ist, dann gibt es immer schon eine Verbindung
            for (vertex v = v1; v != null; v = v.next)
            {
                if (v.isFixStartPoint || v.isFixEndPoint) return false;
            }
            for (vertex v = v1.previous; v != null; v = v.previous)
            {
                if (v.isFixStartPoint || v.isFixEndPoint) return false;
            }
            return true;
        }
#if DEBUG
        DebuggerContainer Debug
        {
            get
            {   // die Verbindungen anzeigen
                DebuggerContainer res = new DebuggerContainer();
                CADability.Attribute.ColorDef free = new CADability.Attribute.ColorDef("frei", System.Drawing.Color.Blue);
                CADability.Attribute.ColorDef fix = new CADability.Attribute.ColorDef("fixed", System.Drawing.Color.Red);
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].next != null)
                    {
                        Line line = Line.Construct();
                        line.SetTwoPoints(new GeoPoint(vertices[i].position), new GeoPoint(vertices[i].next.position));
                        if (vertices[i].keepConnection) line.ColorDef = fix;
                        else line.ColorDef = free;
                        res.Add(line, i);
                    }
                    Point point = Point.Construct();
                    point.Location = new GeoPoint(vertices[i].position);
                    point.Symbol = PointSymbol.Cross;
                    res.Add(point, i);
                }
                return res;
            }
        }
        bool DebugOK
        {
            get
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].next != null)
                    {
                        if (vertices[i].toNext.to != vertices[i].next) return false;
                        if (vertices[i].toNext.from != vertices[i]) return false;
                        if (vertices[i].toNext.isFixed != vertices[i].keepConnection) return false;
                    }
                }
                int count = 0; // überprüfen ob nur eine Strecke
                vertex v = this.first;
                while (v != null)
                {
                    v = v.next;
                    ++count;
                }
                // zusätzlicher Test: abwechselnd keepConnection, Test gilt nicht allgemein, muss wieder raus!
                //for (v = first; v.next != null; v = v.next)
                //{
                //    if (v.keepConnection == v.next.keepConnection) return false;
                //}
                // bis hier her wieder raus
                return count == vertices.Length; ;
            }
        }
        void ConnectionsOK()
        {   // gibt es im QuadTree einen falschen Eintrag?
            connection[] all = connectionTree.GetAllObjects();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].from.toNext != all[i]) throw new ApplicationException("Stelle 1");
                if (all[i].from.next != all[i].to) throw new ApplicationException("Stelle 2");
            }
        }
#endif
        double TotalLength
        {
            get
            {
                double d = 0.0;
                double d1 = 0.0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].toNext != null)
                    {
                        d += vertices[i].toNext.Length;
                    }
                    if (vertices[i].next != null)
                    {
                        d1 += vertices[i].position | vertices[i].next.position;
                    }
                }
                return d1;
            }
        }
    }

    /// <summary>
    /// Class to find a polyline between two points that does not cross the forbidden area
    /// </summary>

    public class FindPathThroughForbiddenArea
    {
        public class Impossible : ApplicationException
        {
            internal Impossible(string msg)
                : base(msg)
            {
            }
        }

        BoundingRect[] forbiddenArea;
        BoundingRect[] forbiddenAreaShrunk; // etwas kleiner, damit HitTest funktioniert
        Set<GeoPoint2D> forbiddenPoints;
        double forbiddenPointsOffset;

        /// <summary>
        /// Constructor to define the forbidden area
        /// </summary>
        /// <param name="forbiddenArea">Rectangle which may not be crossed</param>
        public FindPathThroughForbiddenArea(BoundingRect[] forbiddenArea)
        {
            this.forbiddenArea = (BoundingRect[])forbiddenArea.Clone();
            // das größte zuerst
            Array.Sort<BoundingRect>(this.forbiddenArea, delegate (BoundingRect cp1, BoundingRect cp2)
            {
                return cp2.Size.CompareTo(cp1.Size);
            });
            forbiddenAreaShrunk = (BoundingRect[])this.forbiddenArea.Clone(); // da BoundingRect ein struct ist gibt es hier Kopien
            if (forbiddenArea.Length > 0)
            {
                double eps = this.forbiddenArea[0].Size * 1e-10;
                for (int i = 0; i < forbiddenAreaShrunk.Length; i++)
                {
                    forbiddenAreaShrunk[i].Inflate(-eps);
                }
            }
            forbiddenPoints = new Set<GeoPoint2D>();
            forbiddenPointsOffset = 0.0;
            for (int i = 0; i < forbiddenArea.Length; i++)
            {
                GeoPoint2D p1 = forbiddenArea[i].GetLowerLeft();
                GeoPoint2D p2 = forbiddenArea[i].GetLowerRight();
                GeoPoint2D p3 = forbiddenArea[i].GetUpperLeft();
                GeoPoint2D p4 = forbiddenArea[i].GetUpperRight();
                for (int j = 0; j < forbiddenArea.Length; j++)
                {
                    if (i != j)
                    {
                        if (forbiddenArea[j].Contains(p1)) forbiddenPoints.Add(p1);
                        if (forbiddenArea[j].Contains(p2)) forbiddenPoints.Add(p2);
                        if (forbiddenArea[j].Contains(p3)) forbiddenPoints.Add(p3);
                        if (forbiddenArea[j].Contains(p4)) forbiddenPoints.Add(p4);
                    }
                }
            }
            // Verbesserung: Überlappende Rechtecke zusammenfassen: ein Dictionary Rechteck->Punktliste
            // die Punktliste enthält nur die äußeren Punkte. Statt der 4 Eckpunkte untersucht man dann die Punktliste
            // und nimmt von den beiden Extremen (rechts und links) das kleinere.
        }

        /// <summary>
        /// Tests whether the provided point <paramref name="toTest"/> is contained in one of the rectangles
        /// </summary>
        /// <param name="toTest">Point to test</param>
        /// <returns>true if contained, false otherwise.</returns>
        public bool ContainsPoint(GeoPoint2D toTest)
        {
            return ContainsPoint(toTest, 0.0);
        }
        public bool ContainsPoint(GeoPoint2D toTest, double xyOffset)
        {
            for (int i = 0; i < forbiddenArea.Length; i++)
            {
                BoundingRect br = forbiddenArea[i];
                br.Inflate(xyOffset);
                if (br.Contains(toTest)) return true;
            }
            return false;
        }

        /// <summary>
        /// Tests whether the provided polyline interferes with one of the rectangles
        /// </summary>
        /// <param name="toTest">The polyline</param>
        /// <returns>true when the polyline and one of the rectangles interfere, fals otherwise</returns>
        public bool PolyLineHitTest(GeoPoint2D[] toTest)
        {
            return PolyLineHitTest(toTest, 0.0);
        }
        public bool PolyLineHitTest(GeoPoint2D[] toTest, double xyOffset)
        {
            if (toTest.Length == 1) return ContainsPoint(toTest[0], xyOffset);
            for (int i = 0; i < toTest.Length - 1; i++)
            {
                for (int j = 0; j < forbiddenArea.Length; j++)
                {
                    BoundingRect br = forbiddenAreaShrunk[j];
                    br.Inflate(xyOffset);
                    if (ClipRect.LineHitTest(toTest[i], toTest[i + 1], ref br))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Finds a polyline which connects <paramref name="startPoint"/> and <paramref name="endPoint"/>
        /// and returns the intermediate points. The resulting polyline does not cross the forbidden area
        /// </summary>
        /// <param name="startPoint">The polyline starts here</param>
        /// <param name="endPoint">the polyline end here</param>
        /// <returns>Intermediate points to go around the forbidden area</returns>
        public List<GeoPoint2D> FindPath(GeoPoint2D startPoint, GeoPoint2D endPoint, double xyOffset)
        {
            if (xyOffset != forbiddenPointsOffset)
            {   // forbiddenPoints are vertex-points of one forbiddenArea which reside in another forbiddenArea
                // should only be calculated once, but xyOffset should be respected
                forbiddenPoints = new Set<GeoPoint2D>();
                BoundingRect[] fbaoff = new BoundingRect[forbiddenArea.Length];
                for (int i = 0; i < forbiddenArea.Length; i++)
                {
                    fbaoff[i] = forbiddenArea[i];
                    fbaoff[i].Inflate(xyOffset);
                }
                for (int i = 0; i < fbaoff.Length; i++)
                {
                    GeoPoint2D p1 = fbaoff[i].GetLowerLeft();
                    GeoPoint2D p2 = fbaoff[i].GetLowerRight();
                    GeoPoint2D p3 = fbaoff[i].GetUpperLeft();
                    GeoPoint2D p4 = fbaoff[i].GetUpperRight();
                    for (int j = 0; j < fbaoff.Length; j++)
                    {
                        if (i != j)
                        {
                            if (fbaoff[j].Contains(p1)) forbiddenPoints.Add(p1);
                            if (fbaoff[j].Contains(p2)) forbiddenPoints.Add(p2);
                            if (fbaoff[j].Contains(p3)) forbiddenPoints.Add(p3);
                            if (fbaoff[j].Contains(p4)) forbiddenPoints.Add(p4);
                        }
                    }
                }
                forbiddenPointsOffset = xyOffset;
            }
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            res.Add(startPoint);
            res.Add(endPoint);
            double len = startPoint | endPoint;
            FindPath(res, ref len, xyOffset);
            res.RemoveAt(0);
            res.RemoveAt(res.Count - 1);
            // maybe better solution with reversed forbiddenAreas
            Array.Reverse(forbiddenArea);
            List<GeoPoint2D> res1 = new List<GeoPoint2D>();
            res1.Add(startPoint);
            res1.Add(endPoint);
            double len1 = startPoint | endPoint;
            FindPath(res1, ref len1, xyOffset);
            res1.RemoveAt(0);
            res1.RemoveAt(res1.Count - 1);
            if (len < len1) return res;
            else return res1;
        }

        private void FindPath(List<GeoPoint2D> points, ref double length, double xyOffset)
        {   // siehe Verbessrung Kommentar im Konstruktor
#if DEBUG
            DebuggerContainer dc = new CADability.DebuggerContainer();
            Polyline2D p2d = new Polyline2D(points.ToArray());
            dc.Add(p2d);
            for (int i = 0; i < forbiddenArea.Length; i++)
            {
                dc.Add(forbiddenArea[i].DebugAsPolyline);
            }
#endif
            bool modified = true;
            while (modified)
            {
                modified = false;
                for (int i = 0; i < forbiddenArea.Length; i++)
                {
                    BoundingRect fbnormal = forbiddenArea[i];
                    fbnormal.Inflate(xyOffset);
                    BoundingRect fbshrunk = fbnormal;
                    fbshrunk.Inflate(-fbnormal.Size * 1e-10);
                    for (int j = 0; j < points.Count - 1; j++)
                    {
                        if (ClipRect.LineHitTest(points[j], points[j + 1], ref fbshrunk))
                        {   // es gibt immer zwei Möglichkeiten, die Sinn machen zu überprüfen
                            // jetzt wird immer die kürzere verwendet. Man könnte auch eine Kopie von points machen
                            // und beide Möglichkeiten überprüfen und dann die kürzere nehmen
                            GeoPoint2D[] pf = new GeoPoint2D[4];
                            pf[0] = fbnormal.GetLowerLeft();
                            pf[1] = fbnormal.GetLowerRight();
                            pf[2] = fbnormal.GetUpperLeft();
                            pf[3] = fbnormal.GetUpperRight();
                            double[] dd = new double[4];
                            for (int k = 0; k < 4; k++)
                            {
                                dd[k] = Geometry.DistPL(pf[k], points[j], points[j + 1]);
                            }
                            double maxPositiv = 0.0;
                            double maxNegativ = 0.0;
                            int maxIndNeg = -1;
                            int maxIndPos = -1;
                            for (int k = 0; k < 4; k++)
                            {
                                if (dd[k] < maxNegativ)
                                {
                                    maxNegativ = dd[k];
                                    maxIndNeg = k;
                                }
                                else if (dd[k] > maxPositiv)
                                {
                                    maxPositiv = dd[k];
                                    maxIndPos = k;
                                }
                            }
                            // hier wird der index mit der wenigsten Abweichung gesucht
                            // man könnte aber auch beide  verwenden. Dazu Kopien der points machen
                            // und bestes Ergebnis verwenden. Ein Fehler (was die minimale Länge angeht) wird dadurch 
                            // entstehen, dass man zuerst das ungünstigere Rechteck betrachtet und deshalb einen
                            // längeren Weg findet
                            int ind = -1;
                            if (maxIndNeg < 0) ind = maxIndPos;
                            else if (maxIndPos < 0) ind = maxIndNeg;
                            else if (forbiddenPoints.Contains(pf[maxIndNeg])) ind = maxIndPos;
                            else if (forbiddenPoints.Contains(pf[maxIndPos])) ind = maxIndNeg;
                            else
                            {
                                if (Math.Abs(dd[maxIndPos]) < Math.Abs(dd[maxIndNeg])) ind = maxIndPos;
                                else ind = maxIndNeg;
                            }
                            if (ind >= 0)
                            {
                                length = length - (points[j] | points[j + 1]) + (points[j] | pf[ind]) + (pf[ind] | points[j + 1]);
                                if (points.Contains(pf[ind])) throw new Impossible("Disconnected Areas");
                                points.Insert(j + 1, pf[ind]);
                                FindPath(points, ref length, xyOffset);
                            }
                            else continue; // dieser HitTest hat zwar zugeschlagen, wird aber trotzdem nicht verwendet
                            // da z.B. beide Punkte nicht in Frage kommen
                            modified = true;
                            break;
                        }
                        if (modified) break;
                    }
                }
            }
        }
    }
}
