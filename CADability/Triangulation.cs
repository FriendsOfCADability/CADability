using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using Wintellect.PowerCollections;

namespace CADability
{
    internal class TriangulationException : ApplicationException
    {   // wird geworfen, wenn Kanten sich berühren oder überschneiden
        public TriangulationException(string msg)
            : base(msg)
        {
        }
    }
    internal class Triangulation
    {
        /*  KONZEPT
         * Im ersten Schritt erfolgt die Dreiecksaufteilung gemäß "ear clipping". Anschließend tauschen benachbarte
         * Dreiecke ggf. ihre gemeinsame Kante gegen die andere Diagonale. Im zweiten Schritt
         * werden die Dreiecke verfeinert gemäß der geforderten Genauigkeit. Nach jedem Schritt werden wieder 
         * falls notwendig Diagonalen getauscht. Die ursprüngliche Umrandung
         * wird bereits in der erforderlichen Genauigkeit erwartet, denn gemeinsame Kanten zwischen verschiedenen
         * Faces sollen auch die gleiche Approximation haben, damit keine Lücken bzw. Überschneidungen entstehen.
         * Die Ausgangspolylinie ist nicht selbstüberschneidend, maximal berührt sie sich selbst bei Einschnitten,
         * die Löcher verbinden.
         */
        class VertexInQuadTree : IQuadTreeInsertable
        {
            public int index; // index in der VertexListe
            List<Vertex> vertex; // Verweis auf die Vertexliste
            public VertexInQuadTree(int index, List<Vertex> vertex)
            {
                this.index = index;
                this.vertex = vertex;
            }
            #region IQuadTreeInsertable Members
            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return new BoundingRect(vertex[index].p2d);
            }
            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                return vertex[index].p2d <= rect;
            }
            object IQuadTreeInsertable.ReferencedObject
            {
                get { return null; }
            }
            #endregion
        }
        class PolygonPointInQuadTree : IQuadTreeInsertable
        {
            public int polygon; // Index des Polygons
            public int index; // Index innerhalb des Polygons 
            public GeoPoint2D point;
            public PolygonPointInQuadTree(GeoPoint2D point, int polygon, int index)
            {
                this.point = point;
                this.polygon = polygon;
                this.index = index;
            }
            #region IQuadTreeInsertable Members
            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return new BoundingRect(point);
            }
            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                return point <= rect;
            }
            object IQuadTreeInsertable.ReferencedObject
            {
                get { return null; }
            }
            #endregion
        }
        class TraingleInQuadTree : IQuadTreeInsertable
        {
            public GeoPoint2D p1, p2, p3;
            Triangle tr;
            public TraingleInQuadTree(int i1, int i2, int i3, List<Vertex> vertex)
            {
                p1 = vertex[i1].p2d;
                p2 = vertex[i2].p2d;
                p3 = vertex[i3].p2d;
            }
            public TraingleInQuadTree(Triangle tr, List<Vertex> vertex)
            {
                this.tr = tr;
                p1 = vertex[tr.v1].p2d;
                p2 = vertex[tr.v2].p2d;
                p3 = vertex[tr.v3].p2d;
            }
            #region IQuadTreeInsertable Members
            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return new BoundingRect(p1, p2, p3);
            }
            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                //using (new PerformanceTick("Triangle HitTest"))
                {
                    int c1 = ClipRect.ClipCode(ref p1, ref rect);
                    int c2 = ClipRect.ClipCode(ref p2, ref rect);
                    int c3 = ClipRect.ClipCode(ref p3, ref rect);
                    if ((c1 & c2 & c3) != 0) return false; // alle Punkte auf einer Seite des Cliprechtecks
                    if ((c1 == 0) || (c2 == 0) || (c3 == 0)) return true; // ein Punkt innerhalb des Cliprechteck

                    // eine der 3 Seiten schneidet das Cliprechteck
                    if (ClipRect.LineHitTest(p1, p2, ref rect)) return true;
                    if (ClipRect.LineHitTest(p2, p3, ref rect)) return true;
                    if (ClipRect.LineHitTest(p3, p1, ref rect)) return true;

                    // jetzt könnte nur noch das Cliprechteck komplett im Dreieck liegen
                    GeoPoint2D center = new GeoPoint2D((rect.Left + rect.Right) / 2.0, (rect.Bottom + rect.Top) / 2.0);
                    // ersatzweise wird hier der Mittelpunkt getestet
                    // der Mittelpunkt des Rechtecks muss auf der selben Seite von allen Dreiecksseiten liegen
                    // also immer rechts, oder immer links wenn es drinnen sein soll.
                    // das liese sich auch mit dem Überstreichwinkel testen (wie Border)
                    bool OnLeftSidep2p3 = Geometry.OnLeftSide(center, p2, p3 - p2);
                    if (Geometry.OnLeftSide(center, p1, p2 - p1) != OnLeftSidep2p3) return false;
                    if (OnLeftSidep2p3 != Geometry.OnLeftSide(center, p3, p1 - p3)) return false;
                    return true; // auf der gleichen Seite für alle drei Dreiecks-Seiten
                }
            }
            object IQuadTreeInsertable.ReferencedObject
            {
                get { return tr; }
            }
            #endregion
        }
        class Triangle
        {   // nur 3 Indizes in die Liste Vertex, aber Klasse, damit referenzierbar
            // immer linksrum orientiert
            public int v1, v2, v3;
            public Edge e1, e2, e3; // e1 verbindet v1 mit v2, e3: v3 und v1
#if DEBUG
            public int id;
#endif
            public Triangle()
            {
#if DEBUG
                id = trcount++;
#endif
            }
            public double Area(List<Vertex> vertex)
            {
                double x1 = vertex[v1].p2d.x;
                double y1 = vertex[v1].p2d.y;
                double x2 = vertex[v2].p2d.x;
                double y2 = vertex[v2].p2d.y;
                double x3 = vertex[v3].p2d.x;
                double y3 = vertex[v3].p2d.y;
                return (x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2)) / 2.0;
            }
            public GeoVector normal(List<Vertex> vertex)
            {
                GeoVector n = (((vertex[v1].p3d - vertex[v2].p3d) ^ (vertex[v3].p3d - vertex[v2].p3d)));
                if (n.IsNullVector()) return n; // um exceptions zu vermeiden
                else return n.Normalized;
            }
#if DEBUG
            public Face Debug(List<Vertex> vertex)
            {
                Face fc = Face.MakeFace(vertex[v1].p3d, vertex[v2].p3d, vertex[v3].p3d);
                IntegerProperty ip = new IntegerProperty(id, "Debug.Id");
                fc.UserData.Add("Debug", ip);
                return fc;
            }
#endif
        }
        class Edge : IComparable<Edge>
        {
            public Triangle t1, t2; // in t1 ist die Kante linksrum orientiert in t2 rechtsrum
            public int v1, v2; // index der Ecken
            public double length; // zum Sortieren, 3D Länge, als key für SortedList
            public double maxDist; // zum sortieren, maximaler Abstand von der Fläche
            public double posMaxDist; // wo ist der maximale Abstand
            public double bend; // der Winkel zwischen den Normalenvektoren der angrenzenden Dreiecke
            public Edge previous; // nur für EarClipping
            public Edge next; // nur für EarClipping
            public double angle; // nur für EarClipping, als key für SortedList
            public bool isOptimal; // ist als Kante von zwei Dreiecken besser als die andere Diagonale
            public int polygon; // bei den ursprünglichen Edges, zu welchem Polygon gehört es
            public void Add(Triangle t)
            {   // hier wird die freie Kante überschrieben
#if DEBUG
                if (t1 != null && t2 != null) throw new ApplicationException("Add Triangle");
#endif
                if (t1 == null) t1 = t;
                else t2 = t;
            }
            public void AddForward(Triangle t)
            {   // hier wird eine vorwärtslaufende kante zugefügt
#if DEBUG
                if (t1 != null) throw new ApplicationException("AddForward Triangle");
#endif
                t1 = t;
            }
            public void AddReverse(Triangle t)
            {   // hier wird eine rückwärtslaufende kante eingefügt
#if DEBUG
                if (t2 != null) throw new ApplicationException("AddReverse Triangle");
#endif
                t2 = t;
            }
            public void Remove(Triangle t)
            {
                if (t == t1) t1 = null;
                else t2 = null;
            }
            public double Debug(List<Vertex> vertex)
            {
                return Geometry.Dist(vertex[v1].p3d, vertex[v2].p3d);
            }
            public GeoObjectList DebugTriangles(List<Vertex> vertex)
            {
                GeoObjectList res = new GeoObjectList();
                if (t1 != null)
                {
                    Face fc = Face.MakeFace(vertex[t1.v1].p3d, vertex[t1.v2].p3d, vertex[t1.v3].p3d);
                    res.Add(fc);
                }
                if (t2 != null)
                {
                    Face fc = Face.MakeFace(vertex[t2.v1].p3d, vertex[t2.v2].p3d, vertex[t2.v3].p3d);
                    res.Add(fc);
                }
                Line ln = Line.Construct();
                ln.SetTwoPoints(vertex[v1].p3d, vertex[v2].p3d);
                res.Add(ln);
                return res;
            }
            #region IComparable<Edge> Members
            // IComparable wird gebraucht, weil Edge in einem OrderedMultiDictionary verwendet wird, und dort
            // die Edges auch irgendwie sortiert sein müssen. Hier ist eigentlich egal wie
            int IComparable<Edge>.CompareTo(Edge other)
            {   // darf nur bei gleichen Edges 0 liefern
                if (v1 < other.v1) return -1;
                if (v1 > other.v1) return +1;
                if (v2 < other.v2) return -1;
                if (v2 > other.v2) return +1;
                return 0;   // beide gleich, selbe Kante!
            }
            #endregion
        }
        class EdgeAngleComparer : IComparer<Edge>
        {
            #region IComparer<Edge> Members

            int IComparer<Edge>.Compare(Edge x, Edge y)
            {
                if (x.angle < y.angle) return -1;
                if (x.angle > y.angle) return 1;
                return (x as IComparable<Edge>).CompareTo(y);
            }

            #endregion
        }
        class EdgeInQuadTree : IQuadTreeInsertable
        {
            List<Vertex> vertex; // Verweis auf die Vertexliste
            public Edge edge; // Verweis auf die Edge
            public EdgeInQuadTree(List<Vertex> vertex, Edge edge)
            {
                this.edge = edge;
                this.vertex = vertex;
            }

            #region IQuadTreeInsertable Members

            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return new BoundingRect(vertex[edge.v1].p2d, vertex[edge.v2].p2d);
            }

            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                ClipRect clr = new ClipRect(rect);
                return clr.LineHitTest(vertex[edge.v1].p2d, vertex[edge.v2].p2d);
            }

            object IQuadTreeInsertable.ReferencedObject
            {
                get { return edge; }
            }

            #endregion
        }
        class Vertex : IQuadTreeInsertable // von struct auf class geändert bringt etwas Geschwindigkeit, hoffentlich sonst keine Probleme
        {
            public GeoPoint2D p2d;
            public GeoPoint p3d;
            public Vertex(GeoPoint2D p2d, GeoPoint p3d)
            {
                this.p2d = p2d;
                this.p3d = p3d;
            }

            #region IQuadTreeInsertable Members
            // das interface wird nicht verwendet, da der Index in vertex[] hier nicht bekannt ist
            BoundingRect IQuadTreeInsertable.GetExtent()
            {
                return new BoundingRect(p2d);
            }

            bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                return p2d <= rect;
            }

            object IQuadTreeInsertable.ReferencedObject
            {
                get { return null; }
            }

            #endregion
        }
        ISurface surface;
        double maxDeflection;
        Angle maxBending;
        List<Triangle> triangle;
        List<Vertex> vertex;
        List<Edge> edges; // anfängliche Liste der Kanten
        List<Pair<Edge, Edge>> edgepairs;
        QuadTree<VertexInQuadTree> vertexQuadTree;
        QuadTree<EdgeInQuadTree> edgequadtree;
        GeoPoint2D[] polygonWithHoles;
        double eps;
        BoundingRect extent;
        bool preferHorizontal, preferVertical;
        public bool innerIntersection; // true, when the provided polyline intersect themselves (which they should not)
#if DEBUG
        static public int trcount;
#endif

#if DEBUG
        internal DebuggerContainer DebugEdges
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                HashSet<Edge> allEdges = new HashSet<Edge>();
                for (int i = 0; i < triangle.Count; ++i)
                {
                    allEdges.Add(triangle[i].e1);
                    allEdges.Add(triangle[i].e2);
                    allEdges.Add(triangle[i].e3);
                }
                foreach (Edge edge in allEdges)
                {
                    Line ln = Line.Construct();
                    ln.StartPoint = vertex[edge.v1].p3d;
                    ln.EndPoint = vertex[edge.v2].p3d;
                    res.Add(ln, edge.v1 * 10000 + edge.v2);
                }
                return res;
            }
        }
        internal DebuggerContainer DebugTriangles
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < triangle.Count; ++i)
                {
                    res.Add(triangle[i].Debug(vertex), i);
                }
                return res;
            }
        }
#endif
        /// <summary>
        /// Triangulates a face defined by the <paramref name="surface"/> and the 2d outline (<paramref name="points"/>)
        /// </summary>
        /// <param name="points">first item is an array of points defining the counterclockwise outline, followed by any number (including no) clockwise holes</param>
        /// <param name="surface">surface of the face</param>
        /// <param name="maxDeflection">maximum allowed distance of the (planar) triangles from the surface</param>
        /// <param name="maxBending">maximum angle between adjacent triangles</param>
        public Triangulation(GeoPoint2D[][] points, ISurface surface, double maxDeflection, Angle maxBending)
        {   
            innerIntersection = false;
            preferHorizontal = surface.IsRuled == RuledSurfaceMode.ruledInU;
            preferVertical = surface.IsRuled == RuledSurfaceMode.ruledInV;
            this.surface = surface;
            this.maxDeflection = maxDeflection;
            this.maxBending = maxBending;
            triangle = new List<Triangle>();
            edgepairs = new List<Pair<Edge, Edge>>();
            List<GeoPoint2D> lPolygonWithHoles = new List<GeoPoint2D>();
            for (int i = 0; i < points.Length; ++i)
            {
                lPolygonWithHoles.AddRange(points[i]);
            }
            polygonWithHoles = lPolygonWithHoles.ToArray();

#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < points.Length; ++i)
            {
                for (int j = 0; j < points[i].Length - 1; ++j)
                {
                    Line2D l2d = new Line2D(points[i][j], points[i][j + 1]);
                    dc.Add(l2d, System.Drawing.Color.Red, j);
                }
            }
#endif
            // Von den inneren Polygonen bestimme die Extrema (als Punkte). Suche alle Punkte vom Äüßeren Polygon, die z.B. 
            // links vom x-Minimum liegen (andere richtungen analog). Aus dieser Menge den nächstgelegenen nehmen. 
            // Die Verbingung des inneren Extrempunktes mit dem nächstgelegenen hat sicher keinen Schnitt mit irgend einer 
            // Linie. Von den 4 Lösungen nimm die kürzeste. Das gefundene Polygon in den QuadTree und weiter mit den
            // verbleibenden inneren.

            // die offset um von den jagged array zum vertex oder edge array zu kommen
            int[] offset = new int[points.Length];
            int totlen = points[0].Length;
            offset[0] = 0;
            for (int i = 1; i < points.Length; ++i)
            {
                offset[i] = totlen;
                totlen += points[i].Length;
            }
            // edges und vertex erzeugen, edges hat hintendran die zunächst noch fehlenden Verbindungen
            // edges besteht zunächst aus geschlossenen zyklen
            edges = new List<Edge>(totlen + 2 * (points.Length - 1));
            vertex = new List<Vertex>(totlen);
            for (int i = 0; i < points.Length; ++i)
            {
                if (points[i].Length > 1)
                {
                    for (int j = 0; j < points[i].Length; ++j)
                    {
                        vertex.Add(new Vertex(points[i][j], surface.PointAt(points[i][j]))); // [offset[i] + j]
                        Edge e = new Edge();
                        e.v1 = offset[i] + j;
                        e.v2 = offset[i] + j + 1;
                        if (e.v2 == offset[i] + points[i].Length)
                        {
                            e.v2 = offset[i];
                        }
                        if (j > 0)
                        {
                            e.previous = edges[offset[i] + j - 1];
                            e.previous.next = e;
                        }
                        if (j == points[i].Length - 1)
                        {   // den Kreis schließen;
                            edges[offset[i]].previous = e;
                            e.next = edges[offset[i]];
                        }
                        e.polygon = i;
                        edges.Add(e); // [offset[i] + j];
                    }
                }
            }
            // Test auf sich überschneidende Kanten. Mit solchen Kanten ist der Algorithmus nicht stabil
            // die Polygone müssen neu berechnet werden. Es gibt im allgemeinen wohl mehrere einzelne Polygone
            extent = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < edges.Count; ++i)
            {
                extent.MinMax(vertex[edges[i].v2].p2d);
            }
            edgequadtree = new QuadTree<EdgeInQuadTree>(extent * 1.1);
            edgequadtree.MaxDeepth = -1; // nutzt auch nicht viel
            for (int i = 0; i < edges.Count; ++i)
            {
                Edge e = edges[i];
                EdgeInQuadTree eq = new EdgeInQuadTree(vertex, e);
                ICollection co = edgequadtree.GetObjectsCloseTo(eq);
                foreach (EdgeInQuadTree eqc in co)
                {
                    if (eqc.edge != e.next && eqc.edge != e.previous)
                    {
                        if (Geometry.SegmentInnerIntersection(vertex[e.v1].p2d, vertex[e.v2].p2d, vertex[eqc.edge.v1].p2d, vertex[eqc.edge.v2].p2d))
                        {
#if DEBUG
                            DebuggerContainer dce = new DebuggerContainer();
                            for (int j = 0; j < edges.Count; ++j)
                            {
                                Line2D l2d = new Line2D(vertex[edges[j].v1].p2d, vertex[edges[j].v2].p2d);
                                if (edges[j] == eqc.edge) dce.Add(l2d, System.Drawing.Color.Red, j);
                                else if (i == j) dce.Add(l2d, System.Drawing.Color.Green, j);
                                else dce.Add(l2d, System.Drawing.Color.Blue, j);
                            }
#endif
                            innerIntersection = true;
                            return; // cannot be used for triangulation
                        }
                    }
                }
                edgequadtree.AddObject(eq);
            }
            eps = (extent.Width + extent.Height) * 1e-8;
            ConnectHoles(points.Length);
        }

        class Connection
        {
            public Edge e1, e2; // gemeint sind die Endpunkte der Kanten
            public double distance; // der Abstand zwischen den Endpunkten
            public Connection(Edge e1, Edge e2, double dist)
            {
                this.e1 = e1;
                this.e2 = e2;
                distance = dist;
            }
        }
        class PolygonPair
        {
            public int i1, i2; // die Nummer des ursprünglichen Polygons
            public PolygonPair(int i1, int i2)
            {   // die beiden sind immer verschieden, also i1 immer kleiner als i2, das macht Equals schneller
                if (i1 < i2)
                {
                    this.i1 = i1;
                    this.i2 = i2;
                }
                else
                {
                    this.i1 = i2;
                    this.i2 = i1;
                }
            }
            public override int GetHashCode()
            {
                return (i1 + i2).GetHashCode();
            }
            public override bool Equals(object obj)
            {
                PolygonPair second = obj as PolygonPair;
                if (second == null) return false;
                return (i1 == second.i1 && i2 == second.i2);
            }
        }
        class ConnectionTree
        {
            public int polygonIndex;
            public List<ConnectionTree> connected;
            public ConnectionTree(int index)
            {
                polygonIndex = index;
                connected = new List<ConnectionTree>();
            }
            /// <summary>
            /// Liefert alle Items, also auch die der untergeordneten Äste
            /// </summary>
            public IEnumerable<ConnectionTree> Items
            {
                get
                {
                    yield return this;
                    for (int i = 0; i < connected.Count; ++i)
                    {
                        foreach (ConnectionTree j in connected[i].Items)
                        {
                            yield return j;
                        }
                    }
                }
            }
        }
        private void ConnectHoles(int polygonCount)
        {   // Konzept: alle Kanten befinden sich im edgequadtree. Die einzelnen Listen des QuadTrees enthalten
            // wertvolle Informationen, wenn sie Kanten aus verschiedenen Löchern und/oder dem umgebenden Polygon enthalten.
            // Dann weiß man, dass diese nahe aneinander sind.
            // Zuerst wird ein Dictionary aufgebaut, welches für Polygonpaare die kürzeste Verbindung enthält.
            // Gleichzeitig wird ein Dictionary aufgebaut, welches zu jedem Polygon die damit verbundenen (gemäß den ersten Dictionary)
            // enthält.
            // Ausgehend vom Polygon 0 (der Umrandung) wird nun ein Baum aufgebaut, indem man von 0 ausgehend die damit verbundenen Löcher
            // identifiziert. Das jedes Polygon (Loch) nur einmal drankommt gibt es auch bei den Verbindungen keine Zyklen.
            // Wenn es mit den ursprünglich gefundenen Zusammenhängen nicht mehr weitergeht, muss mit FindConnection eine Verbindung
            // von bereits im Baum vertretenen und noch unbenutzen Polygonen gefunden werden.
            // Die Verbindungen dürfen sich nicht kreuzen. Das wird jeweils explizit getestet.
            // Von einem Polygon darf es nicht von einem Eckpunkt zu zwei anderen eine Verbindung geben. Das wäre zwar nicht grundsätzlich
            // verboten, aber die richtige Reihenfolge wäre wichtig. Diese Fälle werden deshalb ebenfalls ausgeschlossen.
            int tc0 = System.Environment.TickCount;
            Dictionary<PolygonPair, Connection> connections = new Dictionary<PolygonPair, Connection>();
            Dictionary<int, Set<int>> sortedConnections = new Dictionary<int, Set<int>>();
            foreach (List<EdgeInQuadTree> list in edgequadtree.AllLists)
            {   // Betrachte alle Listen im QuadTree, d.h. Linien, die nahe beieinander liegen
                for (int i = 0; i < list.Count - 1; ++i)
                {
                    for (int j = i + 1; j < list.Count; ++j)
                    {
                        if (list[i].edge.polygon != list[j].edge.polygon)
                        {   // nut Linien, die aus unterschiedlichen Polygonen kommen
                            PolygonPair ind = new PolygonPair(list[i].edge.polygon, list[j].edge.polygon);
                            double dist = vertex[list[i].edge.v2].p2d | vertex[list[j].edge.v2].p2d;
                            Connection cn;
                            bool checkIt = false;
                            if (connections.TryGetValue(ind, out cn))
                            {   // nur überprüfen, wenn die bestehende Verbindung größeren Abstand hat als die neue
                                checkIt = dist < cn.distance;
                            }
                            else
                            {
                                checkIt = true;
                            }
                            if (checkIt)
                            {
                                foreach (EdgeInQuadTree eqi in edgequadtree.ObjectsCloseTo(new Line2D(vertex[list[i].edge.v2].p2d, vertex[list[j].edge.v2].p2d), false))
                                {
                                    if (eqi.edge.v1 == list[i].edge.v2 || eqi.edge.v1 == list[j].edge.v2 || eqi.edge.v2 == list[i].edge.v2 || eqi.edge.v2 == list[j].edge.v2)
                                    {
                                        // die iterierte Edge fällt mit einem der beiden vertices der neuen edge zusammen, das ist nicht verboten
                                        continue;
                                    }
                                    if (Geometry.SegmentIntersection(vertex[eqi.edge.v1].p2d, vertex[eqi.edge.v2].p2d, vertex[list[i].edge.v2].p2d, vertex[list[j].edge.v2].p2d))
                                    {
                                        // die neue Edge würde eine bestehende schneiden, das ist nicht erlaubt
                                        checkIt = false;
                                        break;
                                    }
                                }
                                if (checkIt)
                                {
                                    Set<int> s1, s2;
                                    if (!sortedConnections.TryGetValue(ind.i1, out s1))
                                    {
                                        s1 = new Set<int>();
                                        sortedConnections[ind.i1] = s1;
                                    }
                                    if (!sortedConnections.TryGetValue(ind.i2, out s2))
                                    {
                                        s2 = new Set<int>();
                                        sortedConnections[ind.i2] = s2;
                                    }
                                    s1.Add(ind.i2);
                                    s2.Add(ind.i1);
                                    connections[ind] = new Connection(list[i].edge, list[j].edge, dist);
                                }
                            }
                        }
                    }
                }
            }
            // Hier gilt es jetzt einen Baum aufzubauen, beginnend bei 0, der jedes Polygon genau einmal enthält
            // fehlende Verbindungen zwischen Löchern müssen noch erzeugt werden
            QuadTree<Line2D> insertedConnections = new QuadTree<Line2D>(extent); // sammelt die Verbindungen, weil die sich nicht kreuzen dürfen
            insertedConnections.MaxDeepth = -1;
            Set<int> usedPolygons = new Set<int>();
            usedPolygons.Add(0);
            Set<int> usedVertices = new Set<int>();
            ConnectionTree start = new ConnectionTree(0);
            while (usedPolygons.Count < polygonCount)
            {
                int goOnWith = -1;
                ConnectionTree next = null;
                foreach (ConnectionTree ct in start.Items)
                {   // hier fangen wir immer wieder in der selben Reihenfolge an, das ist nicht sehr effektiv
                    // besser wäre es das zuletzt eingefügte zu bevorzugen, ist aber sschwierig
                    Set<int> test;
                    if (sortedConnections.TryGetValue(ct.polygonIndex, out test))
                    {
                        foreach (int j in test)
                        {
                            if (!usedPolygons.Contains(j))
                            {
                                Connection con = connections[new PolygonPair(ct.polygonIndex, j)]; // die muss existieren
                                if (!usedVertices.Contains(con.e1.v2) && !usedVertices.Contains(con.e2.v2))
                                {   // es dürfen keine neuen Kanten mit schon benutzten Eckpunkten gamacht werden
                                    // jede Verbindung zwischen Löchern muss neue Eckpunkte verwenden
                                    bool intersecting = false;
                                    foreach (Line2D l2d in insertedConnections.ObjectsCloseTo(new Line2D(vertex[con.e1.v2].p2d, vertex[con.e2.v2].p2d)))
                                    {
                                        if (Geometry.SegmentIntersection(l2d.StartPoint, l2d.EndPoint, vertex[con.e1.v2].p2d, vertex[con.e2.v2].p2d))
                                        {
                                            intersecting = true;
                                            break;
                                        }
                                    }
                                    if (intersecting) continue;
                                    next = ct;
                                    goOnWith = j;
                                    break;
                                }
                            }
                        }
                    }
                    if (next != null) break;
                }
                if (next == null)
                {
                    // stelle eine Verbindung von einem unbenutzten mit einem benutzten Polygon her
                    // sollte recht selten vorkommen, die meisten Verbindungen sollten direkt aus dem Quadtree kommen
                    Edge used, unused;
                    FindConnection(usedPolygons, usedVertices, insertedConnections, out used, out unused);
                    // nicht sicher ob das immer klappt, wenn nicht exception und was anderes probieren, aber was???
                    if (used == null || unused == null) return; // das sollte nicht vorkommen, aber es ist ein Fall bekannt, wo es vorkommt
                    // nämlich "3EER400010-5393 --A REV.stp". Dort ist in der Tat eine Insel zum Schluss so ungünstig gelegen, dass alle in Frage 
                    // kommenden Verbindungspunkte schon besetzt sind mit anderen Verbindungen und deshalb nicht mehr genutzt werden können
                    // man könnte natürlich einen Verbindungspunkt mehrfach wählen, aber denn kommt es auf die Reihenfolge an
                    // oder man könnte Seitenmittelpunkte als zusätzliche Punkte einführen und so das Problem beheben.
                    // mit dem "return" wird diese Insel ignoriert.
                    PolygonPair ind = new PolygonPair(used.polygon, unused.polygon);
                    Set<int> s1, s2;
                    if (!sortedConnections.TryGetValue(ind.i1, out s1))
                    {
                        s1 = new Set<int>();
                        sortedConnections[ind.i1] = s1;
                    }
                    if (!sortedConnections.TryGetValue(ind.i2, out s2))
                    {
                        s2 = new Set<int>();
                        sortedConnections[ind.i2] = s2;
                    }
                    s1.Add(ind.i2);
                    s2.Add(ind.i1);
                    // connections[ind] müsste ja undefiniert sein, sonst müsste man ja keines suchen
                    connections[ind] = new Connection(used, unused, vertex[used.v2].p2d | vertex[unused.v2].p2d);
                    // leider wird in der nächsten Runde wieder vorne im Baum angefangen, es wäre gut, man hätte 
                    // gleich das Polygon "used" an der Angel, weil es mit dem weitergehen wird.
                }
                else
                {
                    next.connected.Add(new ConnectionTree(goOnWith));
                    usedPolygons.Add(goOnWith);
                    {   // neue Kanten einfügen
                        Connection con = connections[new PolygonPair(next.polygonIndex, goOnWith)]; // die muss existieren
                        Edge bestOutlineEdge = con.e1;
                        Edge bestHoleEdge = con.e2;
                        usedVertices.Add(bestOutlineEdge.v2); // diese beiden Vertices dürfen in keiner neuen Verbindung verwendet werden
                        usedVertices.Add(bestHoleEdge.v2);
                        Edge e1 = new Edge(); // vom Rand zum Loch
                        Edge e2 = new Edge(); // und zurück
                        e1.v1 = bestOutlineEdge.v2;
                        e1.v2 = bestHoleEdge.v2;
                        e2.v1 = e1.v2;
                        e2.v2 = e1.v1;
                        e1.previous = bestOutlineEdge;
                        e1.next = bestHoleEdge.next;
                        e2.previous = bestHoleEdge;
                        e2.next = bestOutlineEdge.next;
                        e1.polygon = -1;
                        e2.polygon = -1;

                        e1.previous.next = e1;
                        e1.next.previous = e1;
                        e2.previous.next = e2;
                        e2.next.previous = e2;
                        edges.Add(e1);
                        edges.Add(e2);
                        edgepairs.Add(new Pair<Edge, Edge>(e1, e2));
                        edgequadtree.AddObject(new EdgeInQuadTree(vertex, e1));
                        edgequadtree.AddObject(new EdgeInQuadTree(vertex, e2));
                        insertedConnections.AddObject(new Line2D(vertex[e1.v1].p2d, vertex[e1.v2].p2d)); // die darf nicht geschnitten werden
                    }

                }
            }

            int tc1 = System.Environment.TickCount - tc0;
        }
        private void FindConnection(Set<int> usedPolygons, Set<int> usedVertices, QuadTree<Line2D> insertedConnections, out Edge used, out Edge unused)
        {   // stelle eine gültige Verbindung her zwischen einem usedPolygon und einem welches nicht in usedPolygons ist
            // wird aufgerufen, wenn alleine aus dem Quadtree keine Verbindung herstellbar ist
            used = null; unused = null;
            double minDist = double.MaxValue;
            for (int r = 32; r >= 1; r = r / 2) // immer größer werdende Rechteck des Suchrechtecks im Quadtree
            {
                double width = extent.Width / r;
                double height = extent.Height / r;
                // mit einem Rechteck dieser Größe über die ganze Fläche wandern und 
                // nichtzusammenhängende polygone suchen
                // ein unregelmäßiger Gang wäre besser, so fängt man immer wieder links unten an
                // und geht nach rechts oben, wobei mit der zeit links unten schon recht abgegrast ist
                // Wir gehen versetzt zu den Rechtecken im QuadTree, denn im ersten versuch werden ja diese Rechtecke
                // bzw. die Listen des QuadTrees untersucht
                GeoPoint2D cnt = extent.GetCenter();
                for (int i = -r / 2; i <= r / 2; ++i)
                {
                    for (int j = -r / 2; j <= r / 2; ++j)
                    {
                        GeoPoint2D center = cnt + new GeoVector2D(2 * i * width, 2 * j * height);
                        BoundingRect search = new BoundingRect(center, width, height);
                        ICollection co = edgequadtree.GetObjectsFromRect(search);
                        // teile in zwei Listen auf
                        List<Edge> inside = new List<Edge>(co.Count);
                        List<Edge> outside = new List<Edge>(co.Count);
                        foreach (EdgeInQuadTree eq in co)
                        {
                            if (usedPolygons.Contains(eq.edge.polygon)) inside.Add(eq.edge);
                            else outside.Add(eq.edge);
                        }
                        if (inside.Count == 0 || outside.Count == 0) continue; // nix gefunden oder alles in einer Kategorie
                        foreach (Edge e1 in inside)
                        {
                            foreach (Edge e2 in outside)
                            {
                                GeoPoint2D p1 = vertex[e1.v2].p2d; // es werden immer nur die Endpunkte betrachtet
                                GeoPoint2D p2 = vertex[e2.v2].p2d;
                                double d = p1 | p2;
                                bool ok = !usedVertices.Contains(e1.v2) && !usedVertices.Contains(e2.v2);
                                if (ok)
                                {
                                    foreach (EdgeInQuadTree eqi in edgequadtree.ObjectsCloseTo(new Line2D(p1, p2), false))
                                    {
                                        if (eqi.edge.polygon != -1)
                                        {   // andere Verbindungskanten sind schon per usedVertices ausgeschlossen
                                            // Früher war InnerIntersection. Das ist schlecht, denn die Linie kann durch einen anderen Eckpunkt
                                            // gehen und ein Schnitt wird nicht erkannt.
                                            // Also zuerst gemeinsamen Eckpunkt ausschließen, dann SegmentIntersection testen
                                            if (e1.v2 != eqi.edge.v1 && e1.v2 != eqi.edge.v2 && e2.v2 != eqi.edge.v1 && e2.v2 != eqi.edge.v2)
                                            {
                                                if (Geometry.SegmentIntersection(vertex[eqi.edge.v1].p2d, vertex[eqi.edge.v2].p2d, p1, p2))
                                                {
                                                    ok = false;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                if (ok)
                                {   // schneidet es auch nicht eine schon verwendete Verbindungskante
                                    foreach (Line2D l2d in insertedConnections.ObjectsCloseTo(new Line2D(p1, p2)))
                                    {
                                        if (Geometry.SegmentIntersection(l2d.StartPoint, l2d.EndPoint, p1, p2))
                                        {
                                            ok = false;
                                            break;
                                        }
                                    }
                                }
                                if (ok && d < minDist) // kein Schnitt mit irgendwas anderem
                                {
                                    unused = e2;
                                    used = e1;
                                    minDist = d;
                                }
                            }
                        }
                        if (used != null) return; // die beste Lösung aus der erstbesten Liste
                        // alle Listen zu durchsuchen wäre nicht sehr effizient. Andererseits gibt es hier manchmal
                        // subooptimale Lösungen
                    }
                }
            }
        }
        bool TriangleContainsPoints(int v1, int v2, int v3, bool strong)
        {
            return vertexQuadTree.Check(new TraingleInQuadTree(v1, v2, v3, vertex),
                delegate (VertexInQuadTree v)
                {
                    //using (new PerformanceTick("TriangleContainsPoints"))
                    {
                        if (v.index != v1 && v.index != v2 && v.index != v3)
                        {   // die Dreiecke gehn immer links rum
                            if (strong)
                            {
                                // sie dürfen keine Punkte beinhalten und auch keine berühren
                                if (Geometry.DistPL(ref vertex[v.index].p2d, ref vertex[v1].p2d, ref vertex[v2].p2d) < -eps) return false;
                                if (Geometry.DistPL(ref vertex[v.index].p2d, ref vertex[v2].p2d, ref vertex[v3].p2d) < -eps) return false;
                                if (Geometry.DistPL(ref vertex[v.index].p2d, ref vertex[v3].p2d, ref vertex[v1].p2d) < -eps) return false;
                            }
                            else
                            {
                                // hier dürfen Punkte berührt werden
                                if (!Geometry.OnLeftSide(vertex[v.index].p2d, vertex[v1].p2d, vertex[v2].p2d)) return false;
                                if (!Geometry.OnLeftSide(vertex[v.index].p2d, vertex[v2].p2d, vertex[v3].p2d)) return false;
                                if (!Geometry.OnLeftSide(vertex[v.index].p2d, vertex[v3].p2d, vertex[v1].p2d)) return false;
                            }
                            return true;
                        }
                        return false; // ein Eckpunkt, der zählt nicht
                    }
                }
                );

            //BoundingRect tst = new BoundingRect(vertex[v1].p2d, vertex[v2].p2d, vertex[v3].p2d);
            //tst = tst * 1.001;
            //            ICollection c = quadTree.GetObjectsFromRect(tst);
            //            foreach (VertexInQuadTree v in c)
            //            {
            //                if (v.index != v1 && v.index != v2 && v.index != v3)
            //                {   // die Dreiecke gehn immer links rum
            //                    if (strong)
            //                    {
            //                        // sie dürfen keine Punkte beinhalten und auch keine berühren
            //                        if (Geometry.DistPL(vertex[v.index].p2d, vertex[v1].p2d, vertex[v2].p2d) < -eps) continue;
            //                        if (Geometry.DistPL(vertex[v.index].p2d, vertex[v2].p2d, vertex[v3].p2d) < -eps) continue;
            //                        if (Geometry.DistPL(vertex[v.index].p2d, vertex[v3].p2d, vertex[v1].p2d) < -eps) continue;
            //                    }
            //                    else
            //                    {
            //                        // hier dürfen Punkte berührt werden
            //                        if (!Geometry.OnLeftSide(vertex[v.index].p2d, vertex[v1].p2d, vertex[v2].p2d)) continue;
            //                        if (!Geometry.OnLeftSide(vertex[v.index].p2d, vertex[v2].p2d, vertex[v3].p2d)) continue;
            //                        if (!Geometry.OnLeftSide(vertex[v.index].p2d, vertex[v3].p2d, vertex[v1].p2d)) continue;
            //                    }
            //#if DEBUG
            //                    //DebuggerContainer dc = new DebuggerContainer();
            //                    //Line2D l2d = new Line2D(vertex[v1].p2d, vertex[v2].p2d);
            //                    //dc.Add(l2d);
            //                    //l2d = new Line2D(vertex[v2].p2d, vertex[v3].p2d);
            //                    //dc.Add(l2d);
            //                    //l2d = new Line2D(vertex[v3].p2d, vertex[v1].p2d);
            //                    //dc.Add(l2d);
            //                    //Point ppp = Point.Construct();
            //                    //ppp.Location = new GeoPoint(vertex[v.index].p2d);
            //                    //dc.Add(ppp);
            //#endif
            //                    return true; // immer auf der linken Seite
            //                }
            //            }
            //            return false; // keinen drinnen gefunden
        }
        void InsertEdge(SortedList<Edge, object> slAngle, Edge edge)
        {   // es geht um den Winkel am Endpunkt von edge
            GeoVector2D v1 = vertex[edge.v2].p2d - vertex[edge.v1].p2d;
            GeoVector2D v2 = vertex[edge.next.v2].p2d - vertex[edge.next.v1].p2d;
            SweepAngle sw;
            if (Precision.IsNullVector(v1) || Precision.IsNullVector(v2))
            {
                sw = 0.0;
            }
            else
            {
                // bei zylindrischen oder hochgezogenen NURBS Flächen soll eine Richtung
                // für die Dreiecke bevorzugt werden. Dies geschieht hier, indem die Richtungen
                // gedehnt werden und somit die richtigen spitzen Winkel bevorzugt werden
                //if (preferHorizontal)
                //{
                //    v1.y *= 10;
                //    v2.y *= 10;
                //}
                //if (preferVertical)
                //{
                //    v1.x *= 10;
                //    v2.x *= 10;
                //}
                sw = new SweepAngle(v1, v2);
            }
            double d = -sw.Radian;
            if (d < 0 && d > -0.001) d = 0; // fast die gleiche Richtung, die wollen wir nicht als spitze Winkel betrachten
            // d ist jetzt der Winkel. Für das EarClipping ist aber interessant, wie gut die Sehne ist, die die beiden Kanten verbindet
            // Die Überlegung, dass man bei Parallelität der beiden Normalenvektoren den Abstand auf 0 setzen kann ist falsch.
            // Gegenbeispiel: Ebene Nurbsflächen, bei der aber die u oder v-Richtung gebogen sind. Die Gerade im uv-System
            // ist ein Bogen in 3d mit relevantem Abstand von der direkten Verbindung des Start- und Endpunktes
            //GeoVector n1 = surface.GetNormal(vertex[edge.v1].p2d);
            //GeoVector n2 = surface.GetNormal(vertex[edge.next.v2].p2d);
            //double a = n1 * n2 / (n1.Length * n2.Length); // cos des Winkels
            //double md;
            //if (a > 0.99)
            //{   // Winkel kleiner als ca. 8°
            //    md = 0.0;
            //}
            //else
            //{
            GeoPoint2D mp;
            double md = surface.MaxDist(vertex[edge.v1].p2d, vertex[edge.next.v2].p2d, out mp);
            // if (d<0 &&Geometry.PointInsidePolygon(this.polygonWithHoles, mp)<1) md = double.MaxValue/2.0;
            //}
            edge.angle = Math.Sign(d) / (md + maxDeflection); // das ist eine Kombination aus Spitzheit des Winkel und Abstand von der Fläche.
            slAngle.Add(edge, null);
        }


        void InsertEdge(OrderedMultiDictionary<double, Edge> byAngle, Edge edge)
        {   // wird nicht verwendet
            //double f = 1.0 - 1e-13; // liefert den nächsten double wert
            GeoVector2D v1 = vertex[edge.v2].p2d - vertex[edge.v1].p2d;
            GeoVector2D v2 = vertex[edge.next.v2].p2d - vertex[edge.next.v1].p2d;
            SweepAngle sw;
            if (Precision.IsNullVector(v1) || Precision.IsNullVector(v2))
            {
                // throw new ApplicationException("Fatal error in triangulation");
                // das problem hier ist folgendes: das 2D Polygon ist an einer Stelle sehr schmal, aber halt nicht 
                // 0 (von der Fläche her). Hier wird eine Dreiecksseite eingefügt, die fast null ist und das macht beim Winkel
                // Probleme. Ob es sonst noch Probleme macht muss sich erweisen
                sw = 0.0;
            }
            else
            {
                sw = new SweepAngle(v1, v2);
            }
            //// jetzt mit Dreiecksfläche, scheint mir besser geeignet
            //double x1 = vertex[edge.v1].p2d.x;
            //double y1 = vertex[edge.v1].p2d.y;
            //double x2 = vertex[edge.v2].p2d.x;
            //double y2 = vertex[edge.v2].p2d.y;
            //double x3 = vertex[edge.next.v2].p2d.x;
            //double y3 = vertex[edge.next.v2].p2d.y;
            //double a = x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2);
            // count gibt an, in wievielen dreiecken das Eck schon verwendet wurde
            // das hat den Effekt, dass beim regelmäßigen Vieleck die Aufteilung gleichmäßiger ist
            double d = -sw.Radian;
            //if (d == 0.0) d = -1e-13;
            //while (byAngle.ContainsKey(d)) d *= f; // ist bei regelmäßigem n-Eck ineffizient
            //edge.angle = -a;
            //byAngle.Add(-a, edge);
            edge.angle = d; // doch lieber spitze Winkel?
            byAngle.Add(d, edge);
        }
        void InsertLengthEdge(SortedList<double, Edge> sortededges, params Edge[] edges)
        {
            for (int i = 0; i < edges.Length; ++i)
            {
                Edge edge = edges[i];
                double d = -(vertex[edge.v1].p3d | vertex[edge.v2].p3d); // negative Länge wg. sortieren
                while (sortededges.ContainsKey(d)) d += this.maxDeflection / 1000; // d *= f; sortedlist-> multilist
                edge.length = d;
                sortededges[d] = edge;
            }
        }
        static int exchangetotal = 0;
        static int exchangesuccess = 0;
        void ExchangeDiagonalRec(Edge edge, int recDeepth)
        {
            if (recDeepth < 10 && exchangetotal < 10000)
            {   // es sieht so aus, als ob trotz richtigem funktionieren ein StackOverflow passieren kann, 
                if (ExchangeDiagonal(edge))
                {
                }
                // unabhängig davon ob ExchangeDiagonal geklappt hat oder nicht können die Dreiecksseiten
                // suboptimal sein, da bei SplitEdge neue Konstellationen auftreten
                // deshalb hier Grenzen verwenden
                // 18.3.09: mir scheint es wird hier eine fast endlose Lawine ausgelöst
                // das sollte mal überprüft werden
                if (edge.t1 != null)
                {
                    if (!edge.t1.e1.isOptimal) ExchangeDiagonalRec(edge.t1.e1, recDeepth + 1);
                    if (!edge.t1.e2.isOptimal) ExchangeDiagonalRec(edge.t1.e2, recDeepth + 1);
                    if (!edge.t1.e3.isOptimal) ExchangeDiagonalRec(edge.t1.e3, recDeepth + 1);
                }
                if (edge.t2 != null)
                {
                    if (!edge.t2.e1.isOptimal) ExchangeDiagonalRec(edge.t2.e1, recDeepth + 1);
                    if (!edge.t2.e2.isOptimal) ExchangeDiagonalRec(edge.t2.e2, recDeepth + 1);
                    if (!edge.t2.e3.isOptimal) ExchangeDiagonalRec(edge.t2.e3, recDeepth + 1);
                }
            }
            else
            {
            }
        }
        bool ExchangeDiagonal(Edge edge)
        {
            ++exchangetotal;
            if (exchangetotal > 10000) return false; // es gibt Hänger, die untersucht werden müssen
            // z.B. in "Test_CMA201F21700-55.stp"
            edge.isOptimal = true; // wenn wir hier mit false rausgehen, dann ist edge optimal
            // edge ist die Diagonale des Vierecks, gebildet aus den beiden Dreiecken, zu denen edge gehört.
            // <|>. Möglicherweise ist die andere Diagonale besser, da in 3D kürzer. Das wird hier getestet
            // und ggf. werden die beiden dreiecke umgebaut
            int t1vert = 0; // Dreieck t1: welche Ecke liegt nicht an edge
            int t2vert = 0; // Dreieck t2: welche Ecke liegt nicht an edge
            Triangle t1 = edge.t1;
            Triangle t2 = edge.t2;
            if (t1 == null || t2 == null) return false;
            if (t1.e1 == edge) t1vert = 3;
            else if (t1.e2 == edge) t1vert = 1;
            else t1vert = 2;
            if (t2.e1 == edge) t2vert = 3;
            else if (t2.e2 == edge) t2vert = 1;
            else t2vert = 2;
            // 1. Test: ist das Viereck überhaupt konvex, d.h. schneiden sich die beiden Diagonalen in 2D
            int t1ind, t2ind;
            switch (t1vert)
            {
                default: // damit der Compiler Ruhe gibt
                case 1: t1ind = t1.v1; break;
                case 2: t1ind = t1.v2; break;
                case 3: t1ind = t1.v3; break;
            }
            switch (t2vert)
            {
                default: // damit der Compiler Ruhe gibt
                case 1: t2ind = t2.v1; break;
                case 2: t2ind = t2.v2; break;
                case 3: t2ind = t2.v3; break;
            }
            Vertex freet1 = vertex[t1ind];
            Vertex freet2 = vertex[t2ind];
            // ... Abfrage ist schlecht: Nur tauschen, wenn echte Überschneidung der beiden Diagonalen
            if (!Geometry.InnerIntersection(freet1.p2d, freet2.p2d, vertex[edge.v1].p2d, vertex[edge.v2].p2d)) return false;
            // 2. Test: ist die Diagonale in 3D kürzer als edge?
            // Das ist nicht unbedingt ein Qualitätsmerkmal, es hängt von surface ab. D.h. man müsste eigentlich surface
            // entscheiden lassen, welche Diagonale besser ist.
            // Jetzt: Test nach besserer 3D Lösung
            GeoPoint frm = surface.PointAt(new GeoPoint2D(freet1.p2d, freet2.p2d));
            GeoPoint vm = surface.PointAt(new GeoPoint2D(vertex[edge.v1].p2d, vertex[edge.v2].p2d));
            if (Geometry.DistPL(frm, freet1.p3d, freet2.p3d) >= Geometry.DistPL(vm, vertex[edge.v1].p3d, vertex[edge.v2].p3d)) return false;
            //System.Diagnostics.Trace.WriteLine("Austausch: " + t1ind.ToString() + "--" + t2ind.ToString() + " gegen " + edge.v1.ToString() + "--" + edge.v2.ToString() + " (" + Geometry.DistPL(frm, freet1.p3d, freet2.p3d).ToString() + " < " + Geometry.DistPL(vm, vertex[edge.v1].p3d, vertex[edge.v2].p3d).ToString() + " )");
            // if ((freet1.p3d | freet2.p3d) >= (vertex[edge.v1].p3d | vertex[edge.v2].p3d)) return false;
            ++exchangesuccess;
            // 3. jetzt gemäß <|> die "senkrechte" edge um 90° nach links drehen "<->" und die Dreiecke updaten
            // Kante umbiegen
            // die Kanten der beiden Dreiecke können anschließend suboptimal sein
            t1.e1.isOptimal = false;
            t1.e2.isOptimal = false;
            t1.e3.isOptimal = false;
            t2.e1.isOptimal = false;
            t2.e2.isOptimal = false;
            t2.e3.isOptimal = false;
            edge.v1 = t2ind;
            edge.v2 = t1ind;
#if DEBUG
            double dbgd = vertex[edge.v1].p2d | vertex[edge.v2].p2d;
#endif
            edge.isOptimal = true;
            // nicht benötigte kante freigeben
            Edge e1 = null;
            Edge e2 = null;
            switch (t1vert)
            {
                case 1:
                    t1.e3.Remove(t1); // diese kante wird nicht mehr gebraucht
                    e1 = t1.e3;
                    break;
                case 2:
                    t1.e1.Remove(t1); // diese kante wird nicht mehr gebraucht
                    e1 = t1.e1;
                    break;
                case 3:
                    t1.e2.Remove(t1); // diese kante wird nicht mehr gebraucht
                    e1 = t1.e2;
                    break;
            }
            switch (t2vert)
            {
                case 1:
                    t2.e3.Remove(t2);
                    e2 = t2.e3;
                    break;
                case 2:
                    t2.e1.Remove(t2);
                    e2 = t2.e1;
                    break;
                case 3:
                    t2.e2.Remove(t2);
                    e2 = t2.e2;
                    break;
            }
            // die Kanten bei den Dreiecken verändern
            e2.Add(t1); // genau die selbe Richtung, die es vorher in t2 hatte, wenn einzige Kante dann linksrum
            e1.Add(t2); // die Kanten tauschen die Dreiecke, behalten ihre Richtung jeweils bei
            switch (t1vert)
            {
                case 1:
                    t1.e2 = e2;
                    t1.e3 = edge;
                    t1.v3 = t2ind;
                    break;
                case 2:
                    t1.e3 = e2;
                    t1.e1 = edge;
                    t1.v1 = t2ind;
                    break;
                case 3:
                    t1.e1 = e2;
                    t1.e2 = edge;
                    t1.v2 = t2ind;
                    break;
            }
            switch (t2vert)
            {
                case 1:
                    t2.e2 = e1;
                    t2.e3 = edge;
                    t2.v3 = t1ind;
                    break;
                case 2:
                    t2.e3 = e1;
                    t2.e1 = edge;
                    t2.v1 = t1ind;
                    break;
                case 3:
                    t2.e1 = e1;
                    t2.e2 = edge;
                    t2.v2 = t1ind;
                    break;
            }
            return true;
        }
        bool ExchangeDiagonalDist(Edge edge)
        {
            ++exchangetotal;
            if (exchangetotal > 10000) return false; // es gibt Hänger, die untersucht werden müssen
            // z.B. in "Test_CMA201F21700-55.stp"
            edge.isOptimal = true; // wenn wir hier mit false rausgehen, dann ist edge optimal
            // edge ist die Diagonale des Vierecks, gebildet aus den beiden Dreiecken, zu denen edge gehört.
            // <|>. Möglicherweise ist die andere Diagonale besser, da in 3D kürzer. Das wird hier getestet
            // und ggf. werden die beiden dreiecke umgebaut
            int t1vert = 0; // Dreieck t1: welche Ecke liegt nicht an edge
            int t2vert = 0; // Dreieck t2: welche Ecke liegt nicht an edge
            Triangle t1 = edge.t1;
            Triangle t2 = edge.t2;
            if (t1 == null || t2 == null) return false;
            if (t1.e1 == edge) t1vert = 3;
            else if (t1.e2 == edge) t1vert = 1;
            else t1vert = 2;
            if (t2.e1 == edge) t2vert = 3;
            else if (t2.e2 == edge) t2vert = 1;
            else t2vert = 2;
            // 1. Test: ist das Viereck überhaupt konvex, d.h. schneiden sich die beiden Diagonalen in 2D
            int t1ind, t2ind;
            switch (t1vert)
            {
                default: // damit der Compiler Ruhe gibt
                case 1: t1ind = t1.v1; break;
                case 2: t1ind = t1.v2; break;
                case 3: t1ind = t1.v3; break;
            }
            switch (t2vert)
            {
                default: // damit der Compiler Ruhe gibt
                case 1: t2ind = t2.v1; break;
                case 2: t2ind = t2.v2; break;
                case 3: t2ind = t2.v3; break;
            }
            Vertex freet1 = vertex[t1ind];
            Vertex freet2 = vertex[t2ind];
            // ... Abfrage ist schlecht: Nur tauschen, wenn echte Überschneidung der beiden Diagonalen
            if (!Geometry.InnerIntersection(freet1.p2d, freet2.p2d, vertex[edge.v1].p2d, vertex[edge.v2].p2d)) return false;
            // 2. Test: ist die Diagonale in 3D kürzer als edge?
            // Das ist nicht unbedingt ein Qualitätsmerkmal, es hängt von surface ab. D.h. man müsste eigentlich surface
            // entscheiden lassen, welche Diagonale besser ist.
            // Jetzt: Test nach besserer 3D Lösung
            GeoPoint2D mp;
            double ndist = surface.MaxDist(freet1.p2d, freet2.p2d, out mp);
            if (ndist > edge.maxDist) return false; // die ist schlechter
            //System.Diagnostics.Trace.WriteLine("Austausch: " + t1ind.ToString() + "--" + t2ind.ToString() + " gegen " + edge.v1.ToString() + "--" + edge.v2.ToString() + " (" + Geometry.DistPL(frm, freet1.p3d, freet2.p3d).ToString() + " < " + Geometry.DistPL(vm, vertex[edge.v1].p3d, vertex[edge.v2].p3d).ToString() + " )");
            // if ((freet1.p3d | freet2.p3d) >= (vertex[edge.v1].p3d | vertex[edge.v2].p3d)) return false;
            ++exchangesuccess;
            // 3. jetzt gemäß <|> die "senkrechte" edge um 90° nach links drehen "<->" und die Dreiecke updaten
            // Kante umbiegen
            // die Kanten der beiden Dreiecke können anschließend suboptimal sein
            t1.e1.isOptimal = false;
            t1.e2.isOptimal = false;
            t1.e3.isOptimal = false;
            t2.e1.isOptimal = false;
            t2.e2.isOptimal = false;
            t2.e3.isOptimal = false;
            edge.v1 = t2ind;
            edge.v2 = t1ind;
#if DEBUG
            double dbgd = vertex[edge.v1].p2d | vertex[edge.v2].p2d;
#endif
            edge.isOptimal = true;
            edge.maxDist = ndist;
            edge.posMaxDist = -1; // edge.posMaxDist wird nicht berechnet, da nicht mehr benötigt, oder?
            // nicht benötigte kante freigeben
            Edge e1 = null;
            Edge e2 = null;
            switch (t1vert)
            {
                case 1:
                    t1.e3.Remove(t1); // diese kante wird nicht mehr gebraucht
                    e1 = t1.e3;
                    break;
                case 2:
                    t1.e1.Remove(t1); // diese kante wird nicht mehr gebraucht
                    e1 = t1.e1;
                    break;
                case 3:
                    t1.e2.Remove(t1); // diese kante wird nicht mehr gebraucht
                    e1 = t1.e2;
                    break;
            }
            switch (t2vert)
            {
                case 1:
                    t2.e3.Remove(t2);
                    e2 = t2.e3;
                    break;
                case 2:
                    t2.e1.Remove(t2);
                    e2 = t2.e1;
                    break;
                case 3:
                    t2.e2.Remove(t2);
                    e2 = t2.e2;
                    break;
            }
            // die Kanten bei den Dreiecken verändern
            e2.Add(t1); // genau die selbe Richtung, die es vorher in t2 hatte, wenn einzige Kante dann linksrum
            e1.Add(t2); // die Kanten tauschen die Dreiecke, behalten ihre Richtung jeweils bei
            switch (t1vert)
            {
                case 1:
                    t1.e2 = e2;
                    t1.e3 = edge;
                    t1.v3 = t2ind;
                    break;
                case 2:
                    t1.e3 = e2;
                    t1.e1 = edge;
                    t1.v1 = t2ind;
                    break;
                case 3:
                    t1.e1 = e2;
                    t1.e2 = edge;
                    t1.v2 = t2ind;
                    break;
            }
            switch (t2vert)
            {
                case 1:
                    t2.e2 = e1;
                    t2.e3 = edge;
                    t2.v3 = t1ind;
                    break;
                case 2:
                    t2.e3 = e1;
                    t2.e1 = edge;
                    t2.v1 = t1ind;
                    break;
                case 3:
                    t2.e1 = e1;
                    t2.e2 = edge;
                    t2.v2 = t1ind;
                    break;
            }
            return true;
        }
        private void EarClipping()
        {
            // OrderedMultiDictionary<double, Edge> byAngle = new OrderedMultiDictionary<double, Edge>(true);
            vertexQuadTree = new QuadTree<VertexInQuadTree>(extent * 1.1);
            SortedList<Edge, object> slAngle = new SortedList<Edge, object>(edges.Count, new EdgeAngleComparer());
            foreach (Edge edge in edges)
            {
                // InsertEdge(byAngle, edge);
                InsertEdge(slAngle, edge);
                if (edge.angle >= 0.0)
                {   // nur Punkte an nichtkonvexen Ecken in den QuadTree, die anderen spielen keine Rolle
                    vertexQuadTree.AddObject(new VertexInQuadTree(edge.v2, vertex));
                }
            }
            // EarClipping: spitze Ecken mit Außenknick werden abgeschnitten und bilden Dreiecke. Das ist ansich recht
            // unkritisch. Die so entstehnden Dreiecke dürfen keine anderen Eckpunkte enthalten und die neue Kante
            // darf keine bestehende Kante schneiden. Letztere Abfrage ist nur wg. Rundungsfehlern notwendig.
            // Wenn man die Abfrage zu streng fasst, dann kann es sein, dass es nicht mehr weitergeht, fasst man sie
            // zu locker, dann werden falsche Ecken abgetrennt und das System funktioniert nicht mehr.
            // Gesteuert wird das mit Hilfe von "strong"
            bool strong = true;
            int lastTotalCount = slAngle.Count + 1;
            // while (byAngle.TotalCount > 3)
            while (slAngle.Count > 3)
            {
                lastTotalCount = slAngle.Count;
                // foreach (KeyValuePair<double, Edge> kv in byAngle.AllItems)
                bool isConvex = slAngle.Keys[slAngle.Keys.Count - 1].angle < 0.0; // alle Winkel spitzt, also konvex
                Edge ea = slAngle.Keys[0]; // der spitzeste Winkel
                {
                    // bool done = false; // weil in der inneren Schleife abgebrochen wird
                    // Außenwinkel dürfen hier nicht vorkommen
                    // in einem bekannten Fall bedeutet das, dass das Loch die Hülle überragt
                    // dann sollte man das Polygon neu bestimmen
                    // if (kv.Key > 0.0)
                    if (ea.angle > 0.0)
                    {   // es muss immer einen spitzen Winkel an einer Ecke geben, dessen aufgespanntes Dreieck keine Punkte enthält
                        // if (!strong) throw new ApplicationException("Fatal error in triangulation");
                        if (!strong) break; // mal versuchsweise statt Exception
                        // in der Datei 3td08688p15.stp tritt diese Situation auf, wenn man tief hineinzoomt. Das break verwirft dabe vermutlich nur wenige
                        // entartete dreiecke, was nicht weiter auffällt
                        else strong = false; // umschalten auf weniger verschärfte Abfrage, nicht umschalten, sonst ist im nächsten Durchlauf schon false
                        // umgeschaltet wird oben
                        // done = true;
                        // kann sein, dass durch die Umstellung hier von neuem begonnen werden muss, mal sehen
                        while (slAngle.Keys[slAngle.Keys.Count - 1].angle == double.MaxValue)
                        {
                            Edge er = slAngle.Keys[slAngle.Keys.Count - 1];
                            slAngle.RemoveAt(slAngle.Keys.Count - 1);
                            InsertEdge(slAngle, er); // Neuberechnung des Winkels
                        }
                        continue;
                    }
                    // if (!TriangleContainsPoints(kv.Value.v1, kv.Value.v2, kv.Value.next.v2, strong)) // && !EdgeIntersects(kv.Value.v1, kv.Value.next.v2, strong))
                    bool doIt = true;
                    if (!isConvex) // wenn die ganze Kontur konvex ist, dann braucht man nicht zu testen, ob Punkte überdeckt werden (es gibt ja auch keine)
                    {
                        if (TriangleContainsPoints(ea.v1, ea.v2, ea.next.v2, strong))
                        {
                            doIt = false;
                            slAngle.RemoveAt(0); // kommt gleich wieder rein, aber mit neuem Winkel
                            ea.angle = double.MaxValue;
                            slAngle.Add(ea, null); // hintendran, denn wer einmal durch den Test fällt, fällt immer durch
                            // die Ecke kommt wieder dazu, wenn sie durch eine neue Kante aufgeteilt wird
                        }
                    }
                    if (doIt)
                    {
                        Edge e = new Edge(); // die neue Kante
                        // e.previous = kv.Value.previous;
                        // e.next = kv.Value.next.next;
                        e.previous = ea.previous;
                        e.next = ea.next.next;
                        e.previous.next = e;
                        e.next.previous = e;
                        //e.v1 = kv.Value.v1;
                        //e.v2 = kv.Value.next.v2;
                        e.v1 = ea.v1;
                        e.v2 = ea.next.v2;
                        if (e.v1 != e.v2)
                        {
                            Triangle t = new Triangle(); // das neue Dreieck
                            //t.e1 = kv.Value;
                            //t.e2 = kv.Value.next;
                            t.e1 = ea;
                            t.e2 = ea.next;
                            t.e3 = e;
                            t.v1 = t.e1.v1;
                            t.v2 = t.e1.v2;
                            t.v3 = t.e2.v2;
                            t.e1.AddForward(t);
                            t.e2.AddForward(t);
                            t.e3.AddReverse(t);
                            triangle.Add(t);
                            double dbg = t.Area(vertex);
                            // zwei alte kanten weg, eine neue hin
                            //byAngle.Remove(kv.Value.previous.angle, kv.Value.previous); // kommt gleich wieder rein, aber mit neuem Winkel
                            //byAngle.Remove(kv.Key, kv.Value);
                            //byAngle.Remove(kv.Value.next.angle, kv.Value.next);
                            //InsertEdge(byAngle, e);
                            //InsertEdge(byAngle, kv.Value.previous); // war vorher auch drin, hat jetzt einen neuen Winkel
                            slAngle.Remove(ea.previous); // kommt gleich wieder rein, aber mit neuem Winkel
                            slAngle.Remove(ea);
                            slAngle.Remove(ea.next);
                            InsertEdge(slAngle, e);
                            InsertEdge(slAngle, ea.previous); // war vorher auch drin, hat jetzt einen neuen Winkel
                            edges.Add(e); // die Sammlung an sich
                            EdgeInQuadTree eq = new EdgeInQuadTree(vertex, e);
                            edgequadtree.AddObject(eq); // und in den QuadTree
                            strong = true; // wieder streng abfragen, da sich ja was geändert hat
                        }
                        else
                        {   // ein leeres Dreieck, beide kanten weg (kommt vermutlich nicht vor)
                            //byAngle.Remove(kv.Value.angle, kv.Value);
                            //byAngle.Remove(kv.Value.next.angle, kv.Value.next);
                            slAngle.Remove(ea);
                            slAngle.Remove(ea.next);
                        }
                        // es muss einmal klappen solange kv.key noch >0
                        // done = true;
                        // break;
                    }
                    // if (done) break;
                }
            }
            // das letzte Dreieck fehlt noch
            //IEnumerator<KeyValuePair<double, ICollection<Edge>>> en = byAngle.GetEnumerator();
            //en.MoveNext();
            //if (en.Current.Value != null)
            //{
            //    IEnumerator<Edge> een = en.Current.Value.GetEnumerator();
            //    een.MoveNext();
            //    Edge e0 = een.Current;
            //    Triangle t0 = new Triangle(); // das neue Dreieck
            //    t0.e1 = e0;
            //    t0.e2 = e0.next;
            //    t0.e3 = e0.next.next;
            //    t0.v1 = t0.e1.v1;
            //    t0.v2 = t0.e1.v2;
            //    t0.v3 = t0.e2.v2;
            //    t0.e1.AddForward(t0);
            //    t0.e2.AddForward(t0);
            //    t0.e3.AddForward(t0);
            //    triangle.Add(t0);
            //    t0.Area(vertex);
            //}
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            ModOp2D makeSquare = ModOp2D.Scale(1.0 / extent.Width, 1.0 / extent.Height);
            foreach (Edge e in slAngle.Keys)
            {
                Line2D l2d = new Line2D(makeSquare * vertex[e.v1].p2d, makeSquare * vertex[e.v2].p2d);
                dc.Add(l2d);
            }
#endif
            if (slAngle.Count < 2) throw new TriangulationException("no edges");
            Edge e0 = slAngle.Keys[0]; // der erste
            Triangle t0 = new Triangle(); // das neue Dreieck
            t0.e1 = e0;
            t0.e2 = e0.next;
            t0.e3 = e0.next.next;
            t0.v1 = t0.e1.v1;
            t0.v2 = t0.e1.v2;
            t0.v3 = t0.e2.v2;
            t0.e1.AddForward(t0);
            t0.e2.AddForward(t0);
            t0.e3.AddForward(t0);
            triangle.Add(t0);
            t0.Area(vertex);
        }
        private bool EdgeIntersects(int v1, int v2, bool strong)
        {
            // ICollection co = edgequadtree.GetObjectsCloseTo(new Line2D(vertex[v1].p2d, vertex[v2].p2d));
            foreach (EdgeInQuadTree eqc in edgequadtree.ObjectsCloseTo(new Line2D(vertex[v1].p2d, vertex[v2].p2d), false))
            {
                if (eqc.edge.v1 != v1 && eqc.edge.v2 != v1 && eqc.edge.v1 != v2 && eqc.edge.v2 != v2)
                {
                    if (strong)
                    {
                        if (Geometry.SegmentIntersection(vertex[v1].p2d, vertex[v2].p2d, vertex[eqc.edge.v1].p2d, vertex[eqc.edge.v2].p2d))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (Geometry.InnerIntersection(vertex[v1].p2d, vertex[v2].p2d, vertex[eqc.edge.v1].p2d, vertex[eqc.edge.v2].p2d))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        Edge[] SplitEdge(Edge edge)
        {
            return SplitEdge(edge, 0.5);
        }
        Edge[] SplitEdge(Edge edge, double param)
        {
            if (edge.t1 == null || edge.t2 == null)
            {
                return new Edge[0]; // an den Außenkanten ist das noch nicht definiert
            }
            if (param < 1e-6 || param > 1 - 1e-6)
            {
                return new Edge[0]; // an den Außenkanten ist das noch nicht definiert
            }
            Edge[] res = new Edge[4];
            // Die Kante Edge soll aufgebrochen werden. Vorläufig mal in der Mitte
            // von folgendem Bild ausgehend <|> die senkrechte aufbrechen, angenommen edge geht von oben nach unten,
            // dann ist t1 das rechte Dreieck, t2 das linke.
            // den neuen Punkt bestimmen
            GeoPoint2D m = Geometry.LinePos(vertex[edge.v1].p2d, vertex[edge.v2].p2d, param);
            Vertex v = new Vertex(m, surface.PointAt(m));
            vertex.Add(v);
            int vind = vertex.Count - 1;
            // edge aufteilen, Orientierung beibehalten
            Edge e1 = new Edge();
            e1.v1 = edge.v1;
            e1.v2 = vind;
            Edge e2 = new Edge();
            e2.v1 = vind;
            e2.v2 = edge.v2;
            // wir verändern t1 so, dass es die rechte obere Hälfte ist.
            Edge e3 = new Edge(); // die Kante von der Mitte nach rechts
            e3.v1 = vind;
            Edge e4 = new Edge(); // die Kante von der Mitte nach links
            e4.v1 = vind;
            res[0] = e1;
            res[1] = e2;
            res[2] = e3;
            res[3] = e4;
            // die Dreiecke sind rechts oben t1, links oben t2, links unten t3 und rechts unten t4
            // t1 und t3 werden verändert, t2 und t4 neu dazugefügt
            int freevertex = 0; // Dreieck t1: welche Ecke liegt nicht an edge
            Triangle t1 = edge.t1;
            Triangle t2 = new Triangle();
            Triangle t3 = edge.t2;
            Triangle t4 = new Triangle();
            // t1 und t3 sind nöglicherweise nicht mehr optimal. Hier wird zwar mehr gesetzt als notwendig, ist aber 
            // weniger aufwendig als die Abfrage
            t1.e1.isOptimal = false;
            t1.e2.isOptimal = false;
            t1.e3.isOptimal = false;
            t3.e1.isOptimal = false;
            t3.e2.isOptimal = false;
            t3.e3.isOptimal = false;
            if (t1.e1 == edge)
            {
                freevertex = 3;
            }
            else if (t1.e2 == edge)
            {
                freevertex = 1;
            }
            else
            {
                freevertex = 2;
            }
            // setze t1 und t4
            e1.AddForward(t1); // alle 3 Fälle
            e3.AddForward(t1);
            e3.AddReverse(t4);
            e2.AddForward(t4);
            switch (freevertex)
            {
                case 1:
                    e3.v2 = t1.v1;
                    t1.e3.Remove(t1);
                    t4.e3 = t1.e3; // kante geht an t4 vor dem Überschreiben
                    t4.e3.Add(t4);
                    t1.e3 = e3;
                    t1.e2 = e1;
                    t4.v3 = t1.v3; // vorher festhalten
                    t1.v3 = vind; // die anderen beiden bleiben
                    t4.e1 = e3;
                    t4.e2 = e2;
                    t4.v1 = t1.v1;
                    t4.v2 = vind;
                    break;
                case 2:
                    e3.v2 = t1.v2;
                    t1.e1.Remove(t1);
                    t4.e3 = t1.e1; // kante geht an t4 vor dem Überschreiben
                    t4.e3.Add(t4);
                    t1.e1 = e3;
                    t1.e3 = e1;
                    t4.v3 = t1.v1; // vorher festhalten
                    t1.v1 = vind; // die anderen beiden bleiben
                    t4.e1 = e3;
                    t4.e2 = e2;
                    t4.v1 = t1.v2;
                    t4.v2 = vind;
                    break;
                case 3:
                    e3.v2 = t1.v3;
                    t1.e2.Remove(t1);
                    t4.e3 = t1.e2; // kante geht an t4 vor dem Überschreiben
                    t4.e3.Add(t4); // wenn es eine freie kante war, dann war sie linksrum (alle freien sind linksrum)
                    t1.e2 = e3;
                    t1.e1 = e1;
                    t4.v3 = t1.v2; // vorher festhalten
                    t1.v2 = vind; // die anderen beiden bleiben
                    t4.e1 = e3;
                    t4.e2 = e2;
                    t4.v1 = t1.v3;
                    t4.v2 = vind;
                    break;
            }
            // ---------- dasgleiche für t3 und t2
            if (t3.e1 == edge)
            {
                freevertex = 3;
            }
            else if (t3.e2 == edge)
            {
                freevertex = 1;
            }
            else
            {
                freevertex = 2;
            }
            // setze t2 und t3
            e2.AddReverse(t3); // rechtsrum, e2 schon einmal vergeben
            e4.AddForward(t3); // 1. Vergabe, linksrum
            e4.AddReverse(t2);
            e1.AddReverse(t2); // linksrum!
            switch (freevertex)
            {
                case 1:
                    e4.v2 = t3.v1;
                    t3.e3.Remove(t3);
                    t2.e3 = t3.e3; // kante geht an t2 vor dem Überschreiben
                    t2.e3.Add(t2);
                    t3.e3 = e4;
                    t3.e2 = e2; // da schon einmal vergeben jetzt rechtsrum
                    t2.v3 = t3.v3; // vorher festhalten
                    t3.v3 = vind; // die anderen beiden bleiben
                    t2.e1 = e4; // aber rechtsrum
                    t2.e2 = e1;
                    t2.v1 = t3.v1;
                    t2.v2 = vind;
                    break;
                case 2:
                    e4.v2 = t3.v2;
                    t3.e1.Remove(t3);
                    t2.e3 = t3.e1; // kante geht an t2 vor dem Überschreiben
                    t2.e3.Add(t2);
                    t3.e1 = e4;
                    t3.e3 = e2; // da schon einmal vergeben jetzt rechtsrum
                    t2.v3 = t3.v1; // vorher festhalten
                    t3.v1 = vind; // die anderen beiden bleiben
                    t2.e1 = e4; // aber rechtsrum
                    t2.e2 = e1;
                    t2.v1 = t3.v2;
                    t2.v2 = vind;
                    break;
                case 3:
                    e4.v2 = t3.v3;
                    t3.e2.Remove(t3);
                    t2.e3 = t3.e2; // kante geht an t2 vor dem Überschreiben
                    t2.e3.Add(t2);
                    t3.e2 = e4;
                    t3.e1 = e2; // da schon einmal vergeben jetzt rechtsrum
                    t2.v3 = t3.v2; // vorher festhalten
                    t3.v2 = vind; // die anderen beiden bleiben
                    t2.e1 = e4; // aber rechtsrum
                    t2.e2 = e1;
                    t2.v1 = t3.v3;
                    t2.v2 = vind;
                    break;
            }
            e3.isOptimal = true; // die beiden können nicht besser werden, da e1 und e2 kolinear
            e4.isOptimal = true;
            // zwei Dreiecke zufügen, die beiden anderen wurden verändert
            triangle.Add(t2);
            triangle.Add(t4);
            return res;
        }
        Triangle[] SplitTriangle(Triangle t, GeoPoint2D innerPoint)
        {
            // ein Dreieck in drei Dreiecke aufspalten
            // innerPoint ist ein Vertex, der innerhalb liegt
            Vertex v = new Vertex(innerPoint, surface.PointAt(innerPoint));
            vertex.Add(v);
            int vinnerPoint = vertex.Count - 1;
            // drei neue Dreiecke
            Triangle tr1 = new Triangle();
            Triangle tr2 = new Triangle();
            Triangle tr3 = new Triangle();
            // drei neue kanten
            Edge ne1 = new Edge();
            ne1.v1 = t.v1;
            ne1.v2 = vinnerPoint;
            Edge ne2 = new Edge();
            ne1.v1 = t.v2;
            ne1.v2 = vinnerPoint;
            Edge ne3 = new Edge();
            ne1.v1 = t.v3;
            ne1.v2 = vinnerPoint;
            // Dreiecke mit Kanten versehen
            tr1.v1 = t.v1;
            tr1.v2 = t.v2;
            tr1.v3 = vinnerPoint;
            tr1.e1 = t.e1;
            if (t.e1.t1 == t) t.e1.t1 = tr1;
            else t.e1.t2 = tr1;
            tr1.e2 = ne2;
            ne2.AddForward(tr1);
            tr1.e3 = ne1;
            ne1.AddReverse(tr1);

            tr2.v1 = t.v2;
            tr2.v2 = t.v3;
            tr2.v3 = vinnerPoint;
            tr2.e1 = t.e2;
            if (t.e2.t1 == t) t.e2.t1 = tr2;
            else t.e2.t2 = tr2;
            tr2.e2 = ne3;
            ne3.AddForward(tr2);
            tr2.e3 = ne2;
            ne2.AddReverse(tr2);

            tr3.v1 = t.v3;
            tr3.v2 = t.v1;
            tr3.v3 = vinnerPoint;
            tr3.e1 = t.e3;
            if (t.e3.t1 == t) t.e3.t1 = tr3;
            else t.e3.t2 = tr3;
            tr3.e2 = ne1;
            ne1.AddForward(tr3);
            tr3.e3 = ne3;
            ne3.AddReverse(tr3);

            triangle.Remove(t);
            triangle.Add(tr1);
            triangle.Add(tr2);
            triangle.Add(tr3);
            edges.Add(ne1); // die Kanten werden auch zugefügt, da anschließend die Kanten vertauscht werden
            edges.Add(ne2);
            edges.Add(ne3);
            return new Triangle[] { tr1, tr2, tr3 };
        }
        void ApproximateAlt(bool noFlip)
        {
            // Liste von der Größe nach sortierten Kanten erstellen
            SortedList<double, Edge> sortededges = new SortedList<double, Edge>(edges.Count);
            foreach (Edge edge in edges)
            {
                if (edge.t2 != null && edge.t1 != null)
                {   // nur die inneren Kanten betrachten, die äußeren werden nicht weiter aufgeteilt.
                    InsertLengthEdge(sortededges, edge);
                }
            }
            // wenn eine Fläche periodisch ist, dann darf eine Kante höchstens ein Drittel
            // der Periode ausmachen. Damit wird sichergestellt, dass beim Aufteilen der Kanten
            // die beiden Teilstücke jeweils kürzer als das Ausgangsstück sind
            // erstmal einfach nur halbieren, das muss noch ausgefeilter werden
            if (surface.IsUPeriodic)
            {
                List<Edge> allEdges = new List<Edge>(sortededges.Values); // eine Kopie um bis zum Ende
                // iterieren zu können
                foreach (Edge edge in allEdges)
                {
                    if (Math.Abs((vertex[edge.v2].p2d.x - vertex[edge.v1].p2d.x)) > surface.UPeriod / 3.0)
                    {
                        InsertLengthEdge(sortededges, SplitEdge(edge));
                        sortededges.Remove(edge.length);
                    }
                }
            }
            if (surface.IsVPeriodic)
            {
                List<Edge> allEdges = new List<Edge>(sortededges.Values); // eine Kopie um bis zum Ende
                // iterieren zu können
                foreach (Edge edge in allEdges)
                {
                    if (Math.Abs(vertex[edge.v2].p2d.y - vertex[edge.v1].p2d.y) > surface.VPeriod / 3.0)
                    {
                        InsertLengthEdge(sortededges, SplitEdge(edge));
                        sortededges.Remove(edge.length);
                    }
                }
            }

            while (sortededges.Count > 0 && triangle.Count < 10000)
            {   // der Größe nach aufteilen
                List<Edge> toAdd = new List<Edge>();
                foreach (Edge edge in sortededges.Values)
                {   // die Ecken der Größe nach
                    //GeoPoint p1 = new GeoPoint(vertex[edge.v1].p3d, vertex[edge.v2].p3d); // Mittelpunkt 3D
                    GeoPoint2D pm = new GeoPoint2D(vertex[edge.v1].p2d, vertex[edge.v2].p2d); // Mittelpunkt 2D
                    // pm = findMaxDist(vertex[edge.v1].p2d, vertex[edge.v2].p2d);
                    surface.MaxDist(vertex[edge.v1].p2d, vertex[edge.v2].p2d, out pm);
                    GeoPoint p2 = surface.PointAt(pm);
                    // if ((vertex[edge.v1].p3d | vertex[edge.v2].p3d) > 18) // zum Debuggen
                    // komisch, manchmal entstehn klitzekleine (dünne lange) Dreiecke, das müsste man mal überprüfen
                    //if ((p1 | p2) > this.maxDeflection)
                    //{
                    //    if (edge.t1.Area(vertex) < eps || edge.t2.Area(vertex) < eps)
                    //    {
                    //        double dbg = edge.Debug(vertex);
                    //        double dbgt1 = edge.t1.Area(vertex);
                    //        double t1e1 = edge.t1.e1.Debug(vertex);
                    //        double t1e2 = edge.t1.e2.Debug(vertex);
                    //        double t1e3 = edge.t1.e3.Debug(vertex);
                    //        double dbgt2 = edge.t2.Area(vertex);
                    //        double t2e1 = edge.t2.e1.Debug(vertex);
                    //        double t2e2 = edge.t2.e2.Debug(vertex);
                    //        double t2e3 = edge.t2.e3.Debug(vertex);
                    //    }
                    //}
                    // Aufteilen, wenn der Abstand zu groß, aber nicht, wenn die beiden anliegenden dreiecke zu klein
                    if (Geometry.DistPL(p2, vertex[edge.v1].p3d, vertex[edge.v2].p3d) > this.maxDeflection && edge.t1.Area(vertex) > eps && edge.t2.Area(vertex) > eps)
                    { // Abweichung nicht akzeptabel, und die anliegenden Dreiecke groß genug
                        toAdd.AddRange(SplitEdge(edge, Geometry.LinePar(vertex[edge.v1].p2d, vertex[edge.v2].p2d, pm)));
                    }
                }
                if (triangle.Count > 9000)
                {
                }
                // die neuen Kanten können suboptimal sein und nun eine Welle von Veränderungen auslösen.
                // jedoch werden Kanten, die niemals von guten zu schlechten Kanten verändert
                if (!noFlip)
                {
                    for (int i = 0; i < toAdd.Count; ++i)
                    {
                        if (!toAdd[i].isOptimal) ExchangeDiagonalRec(toAdd[i], 0);
                    }
                }
                sortededges.Clear();
                InsertLengthEdge(sortededges, toAdd.ToArray());
            }
        }
        void Approximate(bool noFlip)
        {
            // wenn eine Fläche periodisch ist, dann darf eine Kante höchstens ein Drittel
            // der Periode ausmachen. Damit wird sichergestellt, dass beim Aufteilen der Kanten
            // die beiden Teilstücke jeweils kürzer als das Ausgangsstück sind
            // erstmal einfach nur halbieren, das muss noch ausgefeilter werden
            // ACHTUNG: bei periodischen Flächen darf die Kantenlänge maximal 1/3 der Periode sein. Das müsste man hier noch gewährleisten!

            // Liste von dem Abstand von der Fläche nach sortierten Kanten erstellen

            List<Edge> sortededges = new List<Edge>(edges.Count); // Kopie
            foreach (Edge edge in edges)
            {
                if (edge.t2 != null && edge.t1 != null)
                {   // nur die inneren Kanten betrachten, die äußeren werden nicht weiter aufgeteilt.
                    sortededges.Add(edge);
                }
            }

            while (sortededges.Count > 0 && triangle.Count < 100000)
            {
                foreach (Edge edge in sortededges)
                {
                    if (edge.t2 != null && edge.t1 != null)
                    {   // nur die inneren Kanten betrachten, die äußeren werden nicht weiter aufgeteilt.
                        GeoPoint2D pm;
                        edge.maxDist = surface.MaxDist(vertex[edge.v1].p2d, vertex[edge.v2].p2d, out pm);
                        edge.posMaxDist = Geometry.LinePar(vertex[edge.v1].p2d, vertex[edge.v2].p2d, pm);
                        try
                        {
                            edge.bend = (new SweepAngle(edge.t1.normal(vertex), edge.t2.normal(vertex))).Radian;
                            edge.bend = 0;
                        }
                        catch (GeoVectorException)
                        {
                            edge.bend = 0;
                        }
#if DEBUG
                        // GeoObjectList dbg = edge.DebugTriangles(vertex);
#endif
                    }
                }
                sortededges.Sort(delegate (Edge e1, Edge e2)
                {
                    if (e1.bend > Math.PI / 2) // wenn der Knick zwischen den angrenzenden Dreiecken größer 90° ist, dann auf jeden Fall das mit dem scharfen Knick zuerst
                    {   // nur wenn beide (oder natürlich keines) den scharfen Knick haben, dann wie gewohnt nach maxDist sortieren
                        if (e2.bend < Math.PI / 2) return -1;
                    }
                    else if (e2.bend > Math.PI / 2) return +1;
                    return -e1.maxDist.CompareTo(e2.maxDist);
                });

                List<Edge> toAdd = new List<Edge>();
                for (int i = 0; i < sortededges.Count; i++)
                {   // die mit größtem Ansatnd zuerst
                    Edge edge = sortededges[i];
                    if (edge.maxDist > this.maxDeflection || edge.bend > Math.PI / 2)
                    {
                        if (edge.t1.Area(vertex) > eps && edge.t2.Area(vertex) > eps)
                        {
                            toAdd.AddRange(SplitEdge(edge, edge.posMaxDist));
                        }
                    }
                    else
                    {
                        break; // jetzt kommen nur noch kleinere
                    }
                }
                // nur noch die neu entstandenen überprüfen, die anderen sind schon überprüft
                sortededges.Clear();
                sortededges.AddRange(toAdd);
            }
            if (!noFlip)
            {
                for (int j = 0; j < 10; j++)
                {
                    bool noExchange = true;

                    HashSet<Edge> allEdges = new HashSet<Edge>();
                    for (int i = 0; i < triangle.Count; ++i)
                    {
                        if (!triangle[i].e1.isOptimal) allEdges.Add(triangle[i].e1);
                        if (!triangle[i].e2.isOptimal) allEdges.Add(triangle[i].e2);
                        if (!triangle[i].e3.isOptimal) allEdges.Add(triangle[i].e3);
                    }
                    foreach (Edge e in allEdges)
                    {
                        if (ExchangeDiagonalDist(e)) noExchange = false;
                    }
                    if (noExchange) break;
                }
            }
        }
        public void GetTriangles(GeoPoint2D[] innerPoints, out GeoPoint2D[] p2d, out GeoPoint[] p3d, out int[] triangles)
        {
            int tc0 = System.Environment.TickCount;
            EarClipping(); // grundsätzliche Aufteilung in Dreiecke
            int tc1 = System.Environment.TickCount - tc0;
            if (edgepairs != null)
            {
                for (int i = 0; i < edgepairs.Count; ++i)
                {
                    Edge e1 = edgepairs[i].First;
                    Edge e2 = edgepairs[i].Second;
                    // e1 und e2 sind identisch bis auf die Richtung, sie werden jetzt zusammengefasst
                    Triangle t = e2.t1;
                    if (t == null)
                    {
                        edges.Remove(e1);
                        edges.Remove(e2);
                    }
                    else
                    {
                        if (t.e1 == e2) t.e1 = e1;
                        else if (t.e2 == e2) t.e2 = e1;
                        else t.e3 = e1;
                        e2.Remove(t);
                        e1.AddReverse(t);
                        edges.Remove(e2);
                    }
                }
            }
            bool exchanged;
            int dbgcount = 0;
#if DEBUG
            DebuggerContainer dc = Debug;
#endif
            do
            {
                exchanged = false;
                ++dbgcount;
                foreach (Edge edge in edges)
                {
                    if (edge.t2 != null)
                    {   // nur die inneren Kanten betrachten, die äußeren werden nicht weiter aufgeteilt.
                        // am Anfang sind alle Kanten nicht optimal, bei ExchangeDiagonal werden aber die
                        // Diagonalen optimal gemacht, so dass sie beim 2. Durchlauf nicht mehr überprüft
                        // werden müssen
                        if (!edge.isOptimal && ExchangeDiagonal(edge))
                            exchanged = true;
                        // ExchangeDiagonalRec(edge);
                    }
                }
            } while (exchanged);
            // das zufügen der innerpoints erst hier ausführen, denn vorher gibt es Probleme
            // bei exchangeDiagonal
            if (innerPoints != null && innerPoints.Length > 0) AddInnerPoints(innerPoints);
            exchangetotal = 0;
            exchangesuccess = 0;
            Approximate(false); // Verfeinerung der Aufteilung gemäß Oberfläche
            p2d = new GeoPoint2D[vertex.Count];
            p3d = new GeoPoint[vertex.Count];
            for (int i = 0; i < vertex.Count; ++i)
            {
                p2d[i] = vertex[i].p2d;
                p3d[i] = vertex[i].p3d;
            }
            triangles = new int[triangle.Count * 3];
            int ind = 0;
            for (int i = 0; i < triangle.Count; ++i)
            {
                triangles[ind++] = triangle[i].v1;
                triangles[ind++] = triangle[i].v2;
                triangles[ind++] = triangle[i].v3;
            }
        }

        public void GetTrianglesOnlyEarClipping(GeoPoint2D[] innerPoints, out GeoPoint2D[] p2d, out GeoPoint[] p3d, out int[] triangles)
        {
            int tc0 = System.Environment.TickCount;
            EarClipping(); // grundsätzliche Aufteilung in Dreiecke
            int tc1 = System.Environment.TickCount - tc0;
            if (edgepairs != null)
            {
                for (int i = 0; i < edgepairs.Count; ++i)
                {
                    Edge e1 = edgepairs[i].First;
                    Edge e2 = edgepairs[i].Second;
                    // e1 und e2 sind identisch bis auf die Richtung, sie werden jetzt zusammengefasst
                    Triangle t = e2.t1;
                    if (t == null)
                    {
                        edges.Remove(e1);
                        edges.Remove(e2);
                    }
                    else
                    {
                        if (t.e1 == e2) t.e1 = e1;
                        else if (t.e2 == e2) t.e2 = e1;
                        else t.e3 = e1;
                        e2.Remove(t);
                        e1.AddReverse(t);
                        edges.Remove(e2);
                    }
                }
            }

#if DEBUG
            DebuggerContainer dc = Debug;
#endif
            // folgendes für Montanari auskommentiert
            //do
            //{
            //    exchanged = false;
            //    ++dbgcount;
            //    foreach (Edge edge in edges)
            //    {
            //        if (edge.t2 != null)
            //        {   // nur die inneren Kanten betrachten, die äußeren werden nicht weiter aufgeteilt.
            //            // am Anfang sind alle Kanten nicht optimal, bei ExchangeDiagonal werden aber die
            //            // Diagonalen optimal gemacht, so dass sie beim 2. Durchlauf nicht mehr überprüft
            //            // werden müssen
            //            if (!edge.isOptimal && ExchangeDiagonal(edge))
            //                exchanged = true;
            //            // ExchangeDiagonalRec(edge);
            //        }
            //    }
            //} while (exchanged);
            // das zufügen der innerpoints erst hier ausführen, denn vorher gibt es Probleme
            // bei exchangeDiagonal
            // innerpoints ausgeklammert, wir wollen nur den Rand
            if (innerPoints != null && innerPoints.Length > 0) AddInnerPoints(innerPoints);
            exchangetotal = 0;
            exchangesuccess = 0;
            // Approximate(); // Verfeinerung der Aufteilung gemäß Oberfläche
            p2d = new GeoPoint2D[vertex.Count];
            p3d = new GeoPoint[vertex.Count];
            for (int i = 0; i < vertex.Count; ++i)
            {
                p2d[i] = vertex[i].p2d;
                p3d[i] = vertex[i].p3d;
            }
            triangles = new int[triangle.Count * 3];
            int ind = 0;
            for (int i = 0; i < triangle.Count; ++i)
            {
                triangles[ind++] = triangle[i].v1;
                triangles[ind++] = triangle[i].v2;
                triangles[ind++] = triangle[i].v3;
            }
        }
        public void GetSimpleTriangles(out GeoPoint2D[] p2d, out GeoPoint[] p3d, out int[] triangles, bool splitInaccurateEdges)
        {
#if DEBUG
            trcount = 0;
#endif
            int tc0 = System.Environment.TickCount;
            EarClipping(); // grundsätzliche Aufteilung in Dreiecke
            int tc1 = System.Environment.TickCount - tc0;
            if (edgepairs != null)
            {
                for (int i = 0; i < edgepairs.Count; ++i)
                {
                    Edge e1 = edgepairs[i].First;
                    Edge e2 = edgepairs[i].Second;
                    // e1 und e2 sind identisch bis auf die Richtung, sie werden jetzt zusammengefasst
                    Triangle t = e2.t1;
                    if (t == null)
                    {
                        edges.Remove(e1);
                        edges.Remove(e2);
                    }
                    else
                    {
                        if (t.e1 == e2) t.e1 = e1;
                        else if (t.e2 == e2) t.e2 = e1;
                        else t.e3 = e1;
                        e2.Remove(t);
                        e1.AddReverse(t);
                        edges.Remove(e2);
                    }
                }
            }
            if (splitInaccurateEdges)
            {
                Approximate(false); // nur aufteilen, wenn Abstand zu groß
            }
#if DEBUG
            //DebuggerContainer dc = Debug;
#endif

            p2d = new GeoPoint2D[vertex.Count];
            p3d = new GeoPoint[vertex.Count];
            for (int i = 0; i < vertex.Count; ++i)
            {
                p2d[i] = vertex[i].p2d;
                p3d[i] = vertex[i].p3d;
            }
            triangles = new int[triangle.Count * 3];
            int ind = 0;
            for (int i = 0; i < triangle.Count; ++i)
            {
                triangles[ind++] = triangle[i].v1;
                triangles[ind++] = triangle[i].v2;
                triangles[ind++] = triangle[i].v3;
            }
        }

        private GeoPoint2D findMaxDist(GeoPoint2D p1, GeoPoint2D p2)
        {
            GeoPoint2D p0 = p1;
            GeoPoint2D p3 = p2;
            p1 = new GeoPoint2D((2 * p0.x + p3.x) / 3.0, (2 * p0.y + p3.y) / 3.0); // 1. Drittel
            p2 = new GeoPoint2D((p0.x + 2 * p3.x) / 3.0, (p0.y + 2 * p3.y) / 3.0); // 2. Drittel
            GeoPoint s0 = surface.PointAt(p0);
            GeoPoint s1 = surface.PointAt(p1);
            GeoPoint s2 = surface.PointAt(p2);
            GeoPoint s3 = surface.PointAt(p3);
            GeoPoint sp = s0;
            GeoPoint ep = s3;
            double d1 = Geometry.DistPL(s1, sp, ep);
            double d2 = Geometry.DistPL(s2, sp, ep);
            double d0 = 0.0;
            double d3 = 0.0;
            for (int i = 0; i < 10; i++) // 10 Iterationen
            {
                if (d0 + d1 < d2 + d3)
                {   // bei d0 entfernen
                    p0 = p1; // p3 bleibt
                    s0 = s1;
                    d0 = d1;
                }
                else
                {
                    p3 = p2;
                    s3 = s2;
                    d3 = d2;
                }
                p1 = new GeoPoint2D((2 * p0.x + p3.x) / 3.0, (2 * p0.y + p3.y) / 3.0); // 1. Drittel
                p2 = new GeoPoint2D((p0.x + 2 * p3.x) / 3.0, (p0.y + 2 * p3.y) / 3.0); // 2. Drittel
                s1 = surface.PointAt(p1);
                s2 = surface.PointAt(p2);
                d1 = Geometry.DistPL(s1, sp, ep);
                d2 = Geometry.DistPL(s2, sp, ep);
            }
            return new GeoPoint2D(p1, p2); // die Mitte
        }

        private void AddInnerPoints(GeoPoint2D[] innerPoints)
        {
            QuadTree<TraingleInQuadTree> qt = new QuadTree<TraingleInQuadTree>(extent * 1.1);
            for (int i = 0; i < triangle.Count; i++)
            {
                TraingleInQuadTree tiq = new TraingleInQuadTree(triangle[i], vertex);
                qt.AddObject(tiq);
            }
            for (int i = 0; i < innerPoints.Length; i++)
            {
                bool found = false;
                // zuerst Eckpunkte ignorieren
                BoundingRect br = new BoundingRect(innerPoints[i]);
                br.Inflate(1.0);
                VertexInQuadTree[] viqs = vertexQuadTree.GetObjectsFromRect(br);
                for (int j = 0; j < viqs.Length; j++)
                {
                    if (Precision.IsEqual(vertex[viqs[j].index].p2d, innerPoints[i]))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    // zuerst schauen, ob der Punkt genau auf einer Kante liegt
                    EdgeInQuadTree[] qedges = edgequadtree.GetObjectsFromRect(new BoundingRect(innerPoints[i]));
                    for (int j = 0; j < qedges.Length; j++)
                    {
                        GeoPoint2D ep1 = vertex[qedges[j].edge.v1].p2d;
                        GeoPoint2D ep2 = vertex[qedges[j].edge.v2].p2d;
                        if (Precision.IsPointOnLine(innerPoints[i], ep1, ep2))
                        {
                            Edge[] splitted = SplitEdge(qedges[j].edge, Geometry.LinePar(ep1, ep2, innerPoints[i]));
                            if (splitted.Length == 4)
                            {
                                edgequadtree.RemoveObject(qedges[j]);
                                edgequadtree.AddObject(new EdgeInQuadTree(vertex, splitted[0]));
                                edgequadtree.AddObject(new EdgeInQuadTree(vertex, splitted[1]));
                                edgequadtree.AddObject(new EdgeInQuadTree(vertex, splitted[2]));
                                edgequadtree.AddObject(new EdgeInQuadTree(vertex, splitted[3]));
                                edges.Remove(qedges[j].edge);
                                edges.AddRange(splitted);
                                found = true;
                                break;
                            }
                        }
                    }
                }
                if (!found)
                {   // jetzt überprüfen, ob er in einem Dreieck liegt
                    TraingleInQuadTree[] close = qt.GetObjectsFromRect(new BoundingRect(innerPoints[i]));
                    for (int j = 0; j < close.Length; j++)
                    {
                        if (Geometry.OnLeftSide(close[j].p1, close[j].p2, innerPoints[i]) &&
                        Geometry.OnLeftSide(close[j].p2, close[j].p3, innerPoints[i]) &&
                        Geometry.OnLeftSide(close[j].p3, close[j].p1, innerPoints[i]))
                        {
                            Triangle[] tr = SplitTriangle((close[j] as IQuadTreeInsertable).ReferencedObject as Triangle, innerPoints[i]);
                            // SplitTriangle fügt die Kanten schon zu edges zu, entfernt werden müssen keine Kanten
                            if (tr.Length == 3)
                            {
                                qt.RemoveObject(close[j]);
                                qt.AddObject(new TraingleInQuadTree(tr[0], vertex));
                                qt.AddObject(new TraingleInQuadTree(tr[1], vertex));
                                qt.AddObject(new TraingleInQuadTree(tr[2], vertex));
                                break;
                            }
                        }
                    }
                }
            }
        }
#if DEBUG
        public DebuggerContainer Debug
        {
            get
            {
                ModOp2D makeSquare = ModOp2D.Scale(1.0 / extent.Width, 1.0 / extent.Height);
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < edges.Count; ++i)
                {
                    Line2D l2d = new Line2D(vertex[edges[i].v1].p2d, vertex[edges[i].v2].p2d);
                    res.Add(l2d.GetModified(makeSquare), System.Drawing.Color.Red, edges[i].v1.ToString() + "->" + edges[i].v2.ToString() + " p: " + edges[i].polygon.ToString());
                }
                CADability.Attribute.ColorDef cd = new CADability.Attribute.ColorDef("DEBUG", System.Drawing.Color.Green);
                for (int i = 0; i < triangle.Count; ++i)
                {
                    try
                    {
                        Polyline2D p2d = new Polyline2D(new GeoPoint2D[] { vertex[triangle[i].v1].p2d, vertex[triangle[i].v2].p2d, vertex[triangle[i].v3].p2d, vertex[triangle[i].v1].p2d });
                        // res.Add(p2d.GetModified(makeSquare), System.Drawing.Color.Green, triangle[i].v1.ToString() + "->" + triangle[i].v2.ToString() + "->" + triangle[i].v3.ToString());
                        GeoPoint p1 = new GeoPoint(makeSquare * vertex[triangle[i].v1].p2d);
                        GeoPoint p2 = new GeoPoint(makeSquare * vertex[triangle[i].v2].p2d);
                        GeoPoint p3 = new GeoPoint(makeSquare * vertex[triangle[i].v3].p2d);
                        Face fc = Face.MakeFace(p1, p2, p3);
                        fc.ColorDef = cd;
                        res.Add(fc);
                    }
                    catch (ApplicationException)
                    {
                        Line2D l2d = new Line2D(vertex[triangle[i].v1].p2d, vertex[triangle[i].v2].p2d);
                        res.Add(l2d.GetModified(makeSquare), System.Drawing.Color.Blue, i.ToString());
                        l2d = new Line2D(vertex[triangle[i].v2].p2d, vertex[triangle[i].v3].p2d);
                        res.Add(l2d.GetModified(makeSquare), System.Drawing.Color.Blue, i.ToString());
                        l2d = new Line2D(vertex[triangle[i].v3].p2d, vertex[triangle[i].v1].p2d);
                        res.Add(l2d.GetModified(makeSquare), System.Drawing.Color.Blue, i.ToString());
                    }
                }
                return res;
            }
        }
#endif
    }
}
