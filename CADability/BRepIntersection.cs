﻿using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.LinearAlgebra;
using CADability.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;
using Wintellect.PowerCollections;
#if DEBUG
#endif

/* SO GEHTS WEITER:
 * Die Triangulierung der beteiligten Faces muss vermieden werden, dauert zu lange.
 * Schnitte Surface/Surface und Edge/Surface implementieren
 * Face.SplitSeam muss auch bei Torus arbeiten
 * Konvertierung nach OCas muss mit "inversen" Flächen umgehen können
 * Face sollte so modifiziert werden, dass "orientedOutward" immer true ist
 * und die Löcher immer rechtsrum gehen.
 * 
 * Berührende faces müssen sonderbehandlet werden: overlappingFaces:
 * im Falle von *union* werden die intersectionEdges, von beiden Faces gesammelt
 * und wenn es im umgekehrten Sinn einen geschlossesnen Ragnd gibt, dann kommt diese Fläche
 * mit dazu. 
 * 
 */

/* TOPOLOGISCHES 9.08
 * Im Octtree müssen folgende Bedingungen eingehalten werden:
 * folgende Sonderfälle müssen berücksichtigt sein (im Bezug von fremden Objekten zueinander: 
 * Ecke auf Ecke, Ecke auf Kante, Ecke auf Fläche
 * Kante auf Kante, Kante auf Fläche, Fläche auf Fläche.
 * Ansonsten muss für zwei fremde Flächen in einem Blatt auch ein Schnittpunkt zwischen den beiden dort sein,
 * damit ein eindeutiger Verbindungszug zwischen zwei Durchstoßpunkten zu finden ist. Also ein Schnittpunkt
 * Kante mit fremder Fläche darf nur einen angrenzenden Würfel haben, in dem auch beide Flächen sind. i.A.
 * ein Würfel mit zwei fremden Flächen muss auch einen Schnittpunkt der beiden enthalten. Wenn man einen Weg zwischen
 * zwei Durchstoßpunkten sucht und keinen eindeutigen findet, dann betrachtet man die gemeinsame Seite zweier aneinaderliegender
 * Würfel und sucht darauf den Schnittpunkt. Wenn es keinen gibt, dann fällt der Würfel raus. Amn kommt durch beliebiges
 * Aufteilen leider nicht sicher dorthin, dass es nur eindeutige Wege gibt. Also brauchen wir einen 3-Flächen Schnitt
 * wobei eine Fläche eine begrenzte Ebene ist.
 */

namespace CADability
{
    internal class ThickTriangulatedFace : IOctTreeInsertable
    {
        public Face Face;
        private GeoPoint[] trianglePoint;
        private GeoPoint2D[] triangleUVPoint;
        private int[] triangleIndex;
        private BoundingCube triangleExtent;
        public ThickTriangulatedFace(Face face, double precision)
        {
            this.Face = face;
            Face.GetTriangulation(precision, out trianglePoint, out triangleUVPoint, out triangleIndex, out triangleExtent);
        }
        #region IOctTreeInsertable Members

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
    internal class BRepIntersection
    {
        private Shell s1;
        private Shell s2;
        struct IntersectionPoint
        {
            public List<Edge> onEdges;
            public List<Face> onFaces;
            public GeoPoint position;
        }
        Dictionary<Edge, Dictionary<double, GeoPoint>> edgesIntersectionPoints;

        public BRepIntersection(Shell s1, Shell s2)
        {
            this.s1 = s1;
            this.s2 = s2;
            CalcIntersectionEdges();
        }
        private void CalcIntersectionEdges()
        {
            BoundingCube exts1 = s1.GetExtent(0.0);
            BoundingCube exts2 = s2.GetExtent(0.0);
            BoundingCube ext = exts1; // struct, also Kopie!
            ext.MinMax(exts2);
            // TODO: hier noch sicherstellen, dass GetExtent mit und ohne vorhandene triangulierung funktioniert
            double precision = ext.Size * 1e-3;
            // Die Triangulierung mit einer Genauigkeit stellt sicher, dass die Fläche bis zur Genauigkeit
            // angenähert wird. Für den OctTree betrachtet man nicht nur die dreecke selbst sondern auch die beiden
            // "parallelen" Dreiecksnetze, die durch den Offset mit den Normalenvektoren in den Eckpunkten
            // mit der Länge der Genauigkeit entstehen. Eigentlich müsste man zusätzlich einen inneren Punkt in jedem
            // Dreieck berücksichtigen, der durch den Schnitt der drei Tangentialebenen in den Offsets der
            // Eckpunkte entsteht. Selbst offene Shells sollten so keine Probleme machen. 
            // Viele Flächen lassen sich natürlich direkt auf Schnitte überprüfen, so dass es nicht sicher ist
            // ober der hohe Aufwand des Quadtrees gerechtfertigt ist. Zudem stellt sich die Frage, ob der Quadtree
            // nur fragliche Face-Paare liefern soll oder auch noch Anfangspunkte für die Schnittsuche

            // hier also erstmal mit "3-schichtigen" Dreiecksnetzen arbeiten
            OctTree<ThickTriangulatedFace> octs1 = new OctTree<ThickTriangulatedFace>(exts1, precision);
            foreach (Face fc in s1.Faces)
            {
                octs1.AddObject(new ThickTriangulatedFace(fc, precision));
            }
            foreach (Edge edg in s2.Edges)
            {
                if (edg.Curve3D == null)
                {
                    ThickTriangulatedFace[] close = octs1.GetObjectsCloseTo(edg.Curve3D as IOctTreeInsertable);
                    foreach (ThickTriangulatedFace tf in close)
                    {
                        CheckIntersection(tf.Face, edg);
                    }
                }
            }
            OctTree<ThickTriangulatedFace> octs2 = new OctTree<ThickTriangulatedFace>(exts2, precision);
            foreach (Face fc in s2.Faces)
            {
                octs2.AddObject(new ThickTriangulatedFace(fc, precision));
            }
            foreach (Edge edg in s1.Edges)
            {
                if (edg.Curve3D == null)
                {
                    ThickTriangulatedFace[] close = octs2.GetObjectsCloseTo(edg.Curve3D as IOctTreeInsertable);
                    foreach (ThickTriangulatedFace tf in close)
                    {
                        CheckIntersection(tf.Face, edg);
                    }
                }
            }
        }

        private void CheckIntersection(Face fc, Edge edge)
        {   // Edge und Face sind nahe beieinander. Suche zugehörige Schnittpunkte
            GeoPoint[] ip;
            GeoPoint2D[] uvOnFace;
            double[] uOnCurve3D;
            fc.Intersect(edge, out ip, out uvOnFace, out uOnCurve3D);
        }
        /* So solls weitergehen:
         * ThickTriangulatedFace muss implementiert werden (erstmal einfach)
         * Alle Edge-Schnittpunkte aufsammeln. Jeder kennt Face und Edge. Möglicherweise fallen
         * zwei zusammen, nämlich wenn sich zwei Edges schneiden. Die müssen zusammengefasst werden,
         * haben dann kein Face sondern nur Edges.
         * Die Edges und die Faces sollen ihre Intersectionpoints kennen, jeweils mit u bzs. u/v Wert.
         * Jetzt gilt es aus den Punkten Kurven zu machen: Wenn ein Face zwei hat, dann ist es klar, sind 
         * es jedoch mehrere, dann ist die Frage nach dem Zusammenhang zu klären. Wenn es nur zwei mit
         * dem selben anderen Face gibt, ist es auch klar. Nur wenn es mehr als zwei mit demselben
         * anderen Face gibt, gibt es Probleme, die später behandelt werden müssen.
         * Säume müssen extra behandelt werden.
         * Zwei Punkte, zwei beteiligte Faces: gibt es einfache geometrie (Linie, Ellipse) oder nicht.
         * Wenn nicht. dann muss versucht werden Zwischenpunkte zu finden. InterpolatedDualSurfaceCurve.
         * Im u/v sytsem der einen Fläche Kurve mit fixem u oder v nehmen und mit der anderen Fläche schneiden.
         * Sollte passenden Zwischenpunkt liefern. u.s.w.
         * 
         * Fange mit irgendeinem Punkt an, bestimme welche beiden Faces daran beteiligt sind 
         * (aus jeder Shell jeweils eines), versuche einen passenden zweiten punkt zu finden: 
         * entweder es gibt nur zwei, oder dort wo der Punkt auf einer Kante liegt, gehe die Kanten entlang
         * und suche den nächsten (das macht Probleme bei Löchern)
         * 
         * Eine Kurve, die alle Punkte ansammelt, die zwei faces miteinander verbinden
         */
    }
    class CollectIntersectionCurve
    {
        Face f1;
        Face f2;

        public CollectIntersectionCurve(Face f1, Face f2, GeoPoint p1, GeoPoint p2)
        {
        }
    }

    /* Neues Konzept:
     * Grundidee: OctTree so aufteilen, dass in jedem Würfel die topologischen Zusammenhänge eindeutig sind.
     * Face.HitTest muss sehr exakt sein, bloße Traingulierung reicht nicht aus.
     * Einen OctTree bauen, dessen Würfelaufteilung überschrieben ist:
     * Ein Würfel dar nur maximal einen Vertex beinhalten.Vertex sind sowohl die existierenden
     * als auch Schnittpunkte der Kanten mit den Flächen, also die neuen. Ein Vertex darf nur 
     * in einem Node sein. Wenn ein Würfel keinen Vertex enthält, dann darf er von einer Shell
     * nur ein Face, von einer anderen nur zwei Faces enthalten.
     * Wenn ein Würfel keinen Vertex enthält, dann darf er nur auf zwei Nachbarseiten die
     * gleiche Face-Kombination haben (erkennen von Berührpunkten oder mehreren Schnittkurven
     * zweier Flächen), wenn er einen Vertex enthält, so darf jede Face/Face Kombination nur mit
     * einem Nachbarn übereinstimmen.
     * Mit diesen Bedingungen wird der OctTree topologisch eindeutig. Nur an Berührstellen
     * wird er sehr tief. Die muss man dann gesondert abfangen.
     * Shell muss Vertex-Liste erzeugen können, Vertex verweist auf Edges, Edge auf Vertex.
     * So entstehen neue Kanten: Starte mit einem neuen Vertex. Er ist der Schnitt einer Edge(sx)
     * mit einem Face(sy) (sx und sy entsprechen s1 und s2, sorum oder andersrum). 
     * Numm den Node für diesen Vertex. Beteiligt sind 3 Faces: fsxa, fsxb und fsy. Es gibt i.A. 
     * zwei neue Kanten: eine auf fsxa und fsy und eine auf fsxb und fsy. Suche Nachbarwürfel zu diesem
     * Würfel, die fsxa und fsy enthalten. Es darf nur einen geben. Entweder dieser enthält einen neuen
     * Vertex, dann ist man fertig, oder man muss weitere nachbarn suchen, die auch fsxa unf fsy enthalten
     * bis man zu einem entsprechenden neuen Vertex kommt. Weitere Überlegungen für Vertices, die der Schnitt
     * von mehreren Kanten sind, müssen noch angestellt werden.
     * Edges der einen shell, die flächig in Faces der anderen Shell liegen, müssen als gesonderte Objekte 
     * behandel werden.
     * Neue Edges, die durch Durchdringung ohne Kantenbeteiligung entstehen, müssen als gesonderte Objekte 
     * behandel werden.
     * Die neu entstanden Kanten müssen orientiert werden, dann kann man je nach Wunsch die boolsche
     * Operation ausführen.
     * Identische Faces von verschiedenen Shells sollten berücksichtigt werden, so kann man recht leicht 
     * den Offset machen durch Vereinigen der aufgeklebten Offsetstücke und runden Verbindungsstücke.
     * Evtl. kann man so auch den Offset zunächst auf OCas zurückführen.
     */

    internal class VertexIsOnCubeBoundsException : ApplicationException
    {
        public VertexIsOnCubeBoundsException() : base() { }
    }
#if DOCUMENTATIONONLY
    internal class BRepOperation
    {
        internal class DoubleVertexKey : IComparable<DoubleVertexKey>
        {
            public DoubleVertexKey(Vertex f1, Vertex f2)
            {
            }
            public override int GetHashCode()
            {
                return 0;
            }
            public override bool Equals(object obj)
            {
                return false;
            }
    #region IComparable<DoubleVertexKey> Members
            int IComparable<DoubleVertexKey>.CompareTo(DoubleVertexKey other)
            {
                return 0;
            }
    #endregion
        }
        public enum Operation { union, intersection, difference, testonly, commonface }
        public BRepOperation(Shell s1, Shell s2, Operation operation)
        {
        }
        public Shell[] Result()
        {
            return null;
        }
        public bool Intersect(out GeoPoint anyIntersectionPoint)
        {
            anyIntersectionPoint = GeoPoint.Origin;
            return false;
        }
        public int GetOverlappingFaces(out Face[] onShell1, out Face[] onShell2, out ModOp2D[] firstToSecond)
        {
            onShell1 = null;
            onShell2 = null;
            firstToSecond = null;
            return 0;
        }
    }
#else
#if DEBUG
    public class BRepItem : IOctTreeInsertable, IDebuggerVisualizer
#else
    public class BRepItem : IOctTreeInsertable
#endif
    {
        public enum ItemType { Vertex, Edge, Face };
        public ItemType Type;
        static int hashCodeCounter = 0;
        int hashCode;
        // nur eines der drei folgenden ist gesetzt
        public Edge edge;
        public Face face;
        public Vertex vertex;
        public bool isIntersection; // ein Vertex ist durch Schnitt entstanden, edge und face sind auch gesetzt
        public bool isSeam; // w. Aufteilung von geschlossenen Flächen nicht mehr von Bedeutung

        OctTree<BRepItem> root; // Rückverweis auf den OctTree

        public BRepItem(OctTree<BRepItem> root, Edge edge)
        {
            this.root = root;
            this.Type = ItemType.Edge;
            this.edge = edge;
            hashCode = ++hashCodeCounter;
        }
        public BRepItem(OctTree<BRepItem> root, Face face)
        {
            this.root = root;
            this.Type = ItemType.Face;
            this.face = face;
            hashCode = ++hashCodeCounter;
        }
        public BRepItem(OctTree<BRepItem> root, Vertex vertex)
        {
            this.Type = ItemType.Vertex;
            this.root = root;
            this.vertex = vertex;
            hashCode = ++hashCodeCounter;
        }
        public BRepItem(OctTree<BRepItem> root, Vertex vertex, Edge edge, Face face)
        {
            this.Type = ItemType.Vertex;
            this.root = root;
            this.vertex = vertex;
            isIntersection = true;
            this.edge = edge;
            this.face = face;
            hashCode = ++hashCodeCounter;
        }
        bool FaceHitTest(ref BoundingCube cube, Face face, double precision)
        {
            // edges are inserted into the octtree before the faces. So we can check, whether the edges are already in the octtree
            // this is faster than face.HitTest()
            OctTree<BRepItem>.Node<BRepItem> node = root.FindExactNode(cube);
            if (node != null && node.list != null)
            {
                // if node.list==null, we are not at a leaf yet. we could dive down in the octtre to find an edge
                // not sure what is faster
                foreach (BRepItem bi in node.list)
                {
                    if (bi.Type == BRepItem.ItemType.Edge)
                    {
                        if (bi.edge.PrimaryFace == face) return true;
                        if (bi.edge.SecondaryFace == face) return true;
                    }
                }
                // we only need to test the interior, the edges have already been tested
                return face.HitTestWithoutEdges(ref cube, precision);
            }
            return face.HitTest(ref cube, precision);
        }
        #region IOctTreeInsertable Members
        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {
            switch (Type)
            {
                case ItemType.Edge:
                    if (edge.Curve3D != null)
                        return (edge.Curve3D as IOctTreeInsertable).GetExtent(precision);
                    else
                        return new BoundingCube();
                case ItemType.Vertex:
                    return new BoundingCube(vertex.Position);
                case ItemType.Face:
                    return face.GetExtent(precision);
            }
            return BoundingCube.EmptyBoundingCube;
        }
        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            switch (Type)
            {
                case ItemType.Edge:
                    if (edge.Curve3D != null)
                        return (edge.Curve3D as IOctTreeInsertable).HitTest(ref cube, precision);
                    else
                        return false;
                case ItemType.Vertex:
                    // a vertex may not reside on the bounds of the cube.
                    // if this is the case, we need a different center for the octtree
                    // that is what the exception is for
                    if (cube.IsOnBounds(vertex.Position, root.precision)) throw new VertexIsOnCubeBoundsException();
                    return cube.Contains(vertex.Position);
                case ItemType.Face:
                    return FaceHitTest(ref cube, face, precision);
            }
            return false;
        }
        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            throw new ApplicationException("should not be called");
        }
        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            throw new ApplicationException("should not be called");
        }
        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        #endregion

        #region IDebuggerVisualizer Members
#if DEBUG
        public int innerHashCode
        {
            get
            {
                if (edge != null) return edge.GetHashCode();
                if (face != null) return face.GetHashCode();
                if (vertex != null) return vertex.GetHashCode();
                return -1;
            }
        }
        GeoObjectList IDebuggerVisualizer.GetList()
        {
            GeoObjectList res = new GeoObjectList();
            if (edge != null)
            {
                if (edge.Curve3D != null) res.Add(edge.Curve3D as IGeoObject);
            }
            if (face != null) res.Add(face);
            if (vertex != null)
            {
                Point pnt = Point.Construct();
                pnt.Symbol = PointSymbol.Circle;
                pnt.Location = vertex.Position;
                if (isIntersection) pnt.ColorDef = new ColorDef("DebugI", System.Drawing.Color.Red);
                else pnt.ColorDef = new ColorDef("DebugN", System.Drawing.Color.Blue);
            }
            return res;
        }
#endif
        #endregion
    }
#if DEBUG
    public class BRepOperationOld : OctTree<BRepItem>
#else
    internal class BRepOperationOld : OctTree<BRepItem>
#endif

    {
        private Set<Face> facesOnShell1;
        private Set<Face> facesOnShell2;
        private Shell shell1;
        private Shell shell2;
#if DEBUG
        DebuggerContainer debuggerContainer;
#endif
        class DoubleFaceKey : IComparable<DoubleFaceKey>
        {   // dient als key in einem Dictionary von nodes, in dem zwei Faces von verschiedenen Shells enthalten sind
            public Face face1, face2;
            public DoubleFaceKey(Face f1, Face f2)
            {   // es ist wichtig, dass f1 und f2 nicht vertauscht werden (das waren sie früher)
                // wegen Make3D.SplitCommonFace
                //if (f1.GetHashCode() < f2.GetHashCode())
                //{
                face1 = f1;
                face2 = f2;
                //}
                //else
                //{
                //    face1 = f2;
                //    face2 = f1;
                //}
            }
            public override int GetHashCode()
            {
                return face1.GetHashCode() + face2.GetHashCode();
            }
            public override bool Equals(object obj)
            {
                DoubleFaceKey other = obj as DoubleFaceKey;
                if (other == null) return false;
                // der Quatsch mit Min und Max ist vermutlich nicht nötig, da face1 immer aus shell1 und face2 aus shell2
                int hc11 = Math.Min(this.face1.GetHashCode(), this.face2.GetHashCode());
                int hc12 = Math.Max(this.face1.GetHashCode(), this.face2.GetHashCode());
                int hc21 = Math.Min(other.face1.GetHashCode(), other.face2.GetHashCode());
                int hc22 = Math.Max(other.face1.GetHashCode(), other.face2.GetHashCode());
                return hc11 == hc21 && hc12 == hc22;
            }
            #region IComparable<DoubleFaceKey> Members

            int IComparable<DoubleFaceKey>.CompareTo(DoubleFaceKey other)
            {
                // der Quatsch mit Min und Max ist vermutlich nicht nötig, da face1 immer aus shell1 und face2 aus shell2
                // letztlich ist ja nur irgendeine Ordnung gefragt
                int hc11 = Math.Min(this.face1.GetHashCode(), this.face2.GetHashCode());
                int hc12 = Math.Max(this.face1.GetHashCode(), this.face2.GetHashCode());
                int hc21 = Math.Min(other.face1.GetHashCode(), other.face2.GetHashCode());
                int hc22 = Math.Max(other.face1.GetHashCode(), other.face2.GetHashCode());
                int res = hc11.CompareTo(hc21);
                if (res == 0) res = hc12.CompareTo(hc22);
                return res;
            }

            #endregion
        }
        class EdgeFaceKey : IComparable<EdgeFaceKey>
        {
            public Edge edge;
            public Face face;
            public EdgeFaceKey(Edge edge, Face face)
            {
                this.face = face;
                this.edge = edge;
            }
            public override bool Equals(object obj)
            {
                EdgeFaceKey other = obj as EdgeFaceKey;
                if (other == null) return false;
                return other.edge.GetHashCode() == this.edge.GetHashCode() && other.face.GetHashCode() == this.face.GetHashCode();
            }
            public override int GetHashCode()
            {
                return edge.GetHashCode() + face.GetHashCode();
            }
            #region IComparable<EdgeFaceKey> Members
            int IComparable<EdgeFaceKey>.CompareTo(EdgeFaceKey other)
            {
                int res = edge.GetHashCode().CompareTo(other.edge.GetHashCode());
                if (res == 0) res = face.GetHashCode().CompareTo(other.face.GetHashCode());
                return res;
            }
            #endregion
        }
        internal class DoubleVertexKey : IComparable<DoubleVertexKey>
        {
            public Vertex vertex1, vertex2;
            public DoubleVertexKey(Vertex f1, Vertex f2)
            {
                if (f1.GetHashCode() < f2.GetHashCode())
                {
                    vertex1 = f1;
                    vertex2 = f2;
                }
                else
                {
                    vertex1 = f2;
                    vertex2 = f1;
                }
            }
            public override int GetHashCode()
            {
                return vertex1.GetHashCode() + vertex2.GetHashCode();
            }
            public override bool Equals(object obj)
            {
                DoubleVertexKey other = obj as DoubleVertexKey;
                if (other == null) return false;
                return this.vertex1.GetHashCode() == other.vertex1.GetHashCode() && this.vertex2.GetHashCode() == other.vertex2.GetHashCode();
            }
            #region IComparable<DoubleVertexKey> Members
            int IComparable<DoubleVertexKey>.CompareTo(DoubleVertexKey other)
            {
                int res = vertex1.GetHashCode().CompareTo(other.vertex1.GetHashCode());
                if (res == 0) res = vertex2.GetHashCode().CompareTo(other.vertex2.GetHashCode());
                return res;
            }
            #endregion
        }
        OrderedMultiDictionary<DoubleFaceKey, Node<BRepItem>> intersectionEdgesDictionary;
        Dictionary<DoubleFaceKey, ModOp2D> overlappingFaces; // Faces, die auf der gleichen Surface beruhen und sich überlappen
        Dictionary<Face, Set<Edge>> faceToMixedEdges;
        Dictionary<Edge, List<Vertex>> edgesToSplit;
        Dictionary<Face, Set<Edge>> facesToSplit; // Faces, dies gesplitted werden sollen und deren originale oder gesplittete
        Dictionary<EdgeFaceKey, bool> forwardIntersection; // ursprüngliche Orientierung der mixed edge
        // Outline und holes, nicht jedoch die mixed edges.
        Set<Face> originalFaces;
        Set<Face> splittedFaces;
        private void SortInNodes(Node<BRepItem> node)
        {
            if (node.list != null)
            {
                List<Face> facesS1 = new List<Face>();
                List<Face> facesS2 = new List<Face>();
                foreach (BRepItem bi in node.list)
                {
                    if (bi.Type == BRepItem.ItemType.Face)
                    {
                        if (bi.face.Owner == shell1) facesS1.Add(bi.face);
                        else facesS2.Add(bi.face);
                    }
                }
                foreach (Face f1 in facesS1)
                {
                    foreach (Face f2 in facesS2)
                    {
                        intersectionEdgesDictionary.Add(new DoubleFaceKey(f1, f2), node);
                    }
                }
            }
            else
            {
                SortInNodes(node.ppp);
                SortInNodes(node.mpp);
                SortInNodes(node.pmp);
                SortInNodes(node.mmp);
                SortInNodes(node.ppm);
                SortInNodes(node.mpm);
                SortInNodes(node.pmm);
                SortInNodes(node.mmm);
            }
        }
        private BRepItem ContainsVertex(Node<BRepItem> node, DoubleFaceKey faces)
        {
            foreach (BRepItem bri in node.list)
            {
                if (bri.Type == BRepItem.ItemType.Vertex && bri.isIntersection)
                {
                    DoubleFaceKey k1 = new DoubleFaceKey(bri.edge.PrimaryFace, bri.face);
                    if (k1.Equals(faces)) return bri; // Equals, da "==" nicht Equals aufruft, den operator müsste man extra machen
                    if (bri.edge.SecondaryFace != null)
                    {
                        k1 = new DoubleFaceKey(bri.edge.SecondaryFace, bri.face);
                        if (k1.Equals(faces)) return bri;
                    }
                    // da es immer nur einen Vertex geben sollte, könnte man hier mit null abbrechen
                }
            }
            return null;
        }
        private bool ContainsFaces(Node<BRepItem> node, DoubleFaceKey faces)
        {
            bool face1found = false;
            bool face2found = false;
            foreach (BRepItem bri in node.list)
            {
                if (bri.Type == BRepItem.ItemType.Face)
                {
                    if (bri.face == faces.face1) face1found = true;
                    if (bri.face == faces.face2) face2found = true;
                }
            }
            return face1found && face2found;
        }
        private void RefineNodes()
        {
            // Sämtliche Würfel, die ein Face-Paar enthalten müssen eindeutig sortierbar sein,
            // dürfen also jeweils nur zwei Nachbarn mit dem selben Paar enthalten.
            // Wenn zusätzlich noch ein Vertex für dieses FacePaar enthalten ist, dann darf es 
            // nur einen Nachbarn geben
            // LINKLIST:
            // Es ist nicht erfüllbar, dass eine "linkslist" entsteht, die eindeutig von einem vertex zu einem
            // anderen geht. Es soll aber sichergestellt sein, dass wenn es zwei vertices gibt, die eine
            // Schnittkante begrenzen, diese über die linkslist erreichbar sind und dass wenn es mehr
            // als zwei Schnittpunkte sind -es muss immer eine gerade Zahl sein- nur jeweils ein Paar
            // in einer linkslist zusammenfallen.
            List<DoubleFaceKey> FacePairs = new List<DoubleFaceKey>(intersectionEdgesDictionary.Keys); // zum iterieren
            foreach (DoubleFaceKey key in FacePairs) // betrachte jeweils ein paar von Faces von verschiedenen Körpern
            {
                bool toCheck = true;
                bool overlappingChecked = false; // Gleichheit der Faces noch nicht überprüft
                while (toCheck) // toCeck soll dazu dienen, wenn die Verbindung zwischen den Vertices nicht eindeutig hergestellt werden kann
                {
                    List<Node<BRepItem>> items = new List<Node<BRepItem>>(intersectionEdgesDictionary[key]); // damit man indizieren kann
                    List<Node<BRepItem>> vertexitems = new List<Node<BRepItem>>(); // das sind die Schnittpunkte der Kanten
                    for (int i = 0; i < items.Count; ++i)
                    {
                        if (items[i].list != null)
                        {   // gibt es einen Vertex, der u.a. auf diesen beiden Faces liegt
                            BRepItem itemVertex = ContainsVertex(items[i], key);
                            if (itemVertex != null)
                            {
                                vertexitems.Add(items[i]);
                            }
                        }
                    }
                    toCheck = false;
                    bool overlapping = false;
                    if (vertexitems.Count == 0)
                    {   // vielleicht überlappen sich die beiden Faces
                        ModOp2D m2d;
                        if (key.face1.Surface.SameGeometry(key.face1.GetUVBounds(), key.face2.Surface, key.face2.GetUVBounds(), precision, out m2d))
                        {
                            if (!overlappingFaces.ContainsKey(key))
                            {
                                overlappingFaces.Add(key, m2d);
                                overlapping = true;
                                intersectionEdgesDictionary.Remove(key); // ist das nötig?
                                break;
                            }
                        }
                    }
                    // Eine Kette bilden von einem VertexItem zu einem anderen: Ausgehend von irgendeinem vertexitem
                    List<List<GeoPoint>> chains = new List<List<GeoPoint>>(); // alle Ketten von Start- zu Endpunkten
                    List<Pair<Vertex, Vertex>> startEndVertices = new List<Pair<Vertex, Vertex>>(); // Start und Endvertex für jede Kette
                    while (vertexitems.Count > 0)
                    {   // suche alle Nachbarn. Untersuche die Berührflächen zu den Nachbarn auf Schnittpunkte.
                        // es sollte jeweils nur einen einzigen Nachbarn mit Schnittpunkt geben. Gibt es mehrere, dann aufteilen.
                        List<GeoPoint> chain = new List<GeoPoint>(); // Kette für diesen Durchlauf
                        chains.Add(chain); // schon mal zufügen
                        Node<BRepItem> node = vertexitems[0]; // mit irgend einem anfangen
                        vertexitems.RemoveAt(0);
                        BRepItem itemVertex = ContainsVertex(node, key);
                        Vertex startVertex = itemVertex.vertex;
                        Vertex endVertex = null; // wird gesetzt wenn Ende gefunden wird
                        chain.Add(itemVertex.vertex.Position); // Start- und Endpunkt mit in die Kette
                        List<Node<BRepItem>> usedNodes = new List<Node<BRepItem>>(); // benutze gelten nicht mehr
                        do
                        {
                            usedNodes.Add(node);
                            BoundingCube bc = node.cube;
                            Node<BRepItem>[][] neighbours = GetNeighbourNodes(node, delegate (Node<BRepItem> nd) { return ContainsFaces(nd, key) && !usedNodes.Contains(nd); });
                            // das sind alle Nachbarwürfel, sortiert nach den 6 Seiten
                            List<GeoPoint> ips = new List<GeoPoint>(); // hier werden die Schnittpunkte gesammelt, vorzugsweise nur ein einziger
                            GeoPoint2D uv1, uv2, uv3;
                            List<Node<BRepItem>> goOnWith = new List<Node<BRepItem>>();
                            // die Nachbarn durchsuchen. Feststellen, ob es auf einer Würfelfläche zwischen zwei Würfeln (Node<BRepItem>)
                            // einen Schnittpunkt gibt. Wenn ja, ansammeln. 
                            for (int i = 0; i < neighbours.Length; ++i)
                            {
                                Plane pln; // die Schnittebene
                                BoundingRect br1, br2; // die beiden Flächen, das gemeinsame wird verwendet
                                for (int j = 0; j < neighbours[i].Length; ++j)
                                {
                                    BoundingCube bc1 = neighbours[i][j].cube;
                                    switch ((Side)i)
                                    {
                                        default: // damit nicht unitialized
                                        case Side.left:
                                            {   // die 1. zwei Zeilen könnten außerhalb der j-Schleife stehen
                                                pln = new Plane(Plane.XZPlane, bc.Xmin);
                                                br1 = new BoundingRect(bc.Ymin, bc.Zmin, bc.Ymax, bc.Zmax);
                                                br2 = new BoundingRect(bc1.Ymin, bc1.Zmin, bc1.Ymax, bc1.Zmax);
                                            }
                                            break;
                                        case Side.right:
                                            {
                                                pln = new Plane(Plane.YZPlane, bc.Xmax);
                                                br1 = new BoundingRect(bc.Ymin, bc.Zmin, bc.Ymax, bc.Zmax);
                                                br2 = new BoundingRect(bc1.Ymin, bc1.Zmin, bc1.Ymax, bc1.Zmax);
                                            }
                                            break;
                                        case Side.bottom:
                                            {
                                                pln = new Plane(Plane.XYPlane, bc.Zmin);
                                                br1 = new BoundingRect(bc.Xmin, bc.Ymin, bc.Xmax, bc.Ymax);
                                                br2 = new BoundingRect(bc1.Xmin, bc1.Ymin, bc1.Xmax, bc1.Ymax);
                                            }
                                            break;
                                        case Side.top:
                                            {
                                                pln = new Plane(Plane.XYPlane, bc.Zmax);
                                                br1 = new BoundingRect(bc.Xmin, bc.Ymin, bc.Xmax, bc.Ymax);
                                                br2 = new BoundingRect(bc1.Xmin, bc1.Ymin, bc1.Xmax, bc1.Ymax);
                                            }
                                            break;
                                        case Side.front:
                                            {
                                                pln = new Plane(Plane.XZPlane, -bc.Ymin); // in der XZ Ebene ist die Y-Achse negativ, der Offset hier also mit umgedrehtem Vorzeichen!
                                                br1 = new BoundingRect(bc.Xmin, bc.Zmin, bc.Xmax, bc.Zmax);
                                                br2 = new BoundingRect(bc1.Xmin, bc1.Zmin, bc1.Xmax, bc1.Zmax);
                                            }
                                            break;
                                        case Side.back:
                                            {
                                                pln = new Plane(Plane.XZPlane, -bc.Ymax); // in der XZ Ebene ist die Y-Achse negativ, der Offset hier also mit umgedrehtem Vorzeichen!
                                                br1 = new BoundingRect(bc.Xmin, bc.Zmin, bc.Xmax, bc.Zmax);
                                                br2 = new BoundingRect(bc1.Xmin, bc1.Zmin, bc1.Xmax, bc1.Zmax);
                                            }
                                            break;
                                    }
                                    BoundingRect br = BoundingRect.Common(br1, br2); // gemeinsame 2-d Berührfläche
                                    PlaneSurface pls = new PlaneSurface(pln);
                                    GeoPoint ip = pln.ToGlobal(br.GetCenter()); // beginne mit dem Mittelpunkt der Berührfläche
                                    if (Surfaces.NewtonIntersect(pls, br, key.face1.Surface, key.face1.GetUVBounds(), key.face2.Surface, key.face2.GetUVBounds(), ref ip, out uv1, out uv2, out uv3))
                                    {   // ein Schnittpunkt auf der Berührfläche gefunden
                                        if (br.Contains(uv1) && key.face1.Contains(ref uv2, true) && key.face2.Contains(ref uv3, true))
                                        {   // nur Schnittpunkte die auf der Würfelseite liegen und auch in den beiden faces
                                            // Es muss hier in der Tat geprüft werden, ob die Punkte innerhalb der beiden Faces (mit ihren echten Rändern) liegen
                                            // z.B. eine Ringfläche in der Ebene schneidet eine andere Ebene. Es gibt 4 Schnittpunkte, die beiden inneren
                                            // dürfen nicht verbunden werden, da sie nicht auf dem Ring liegen
                                            goOnWith.Add(neighbours[i][j]);
                                            ips.Add(ip);
                                        }
                                    }
                                }
                            }
                            if (goOnWith.Count != 1)
                            {   // es geht nicht eindeutig weiter, also entweder kein Schnittpunkt gefunden oder mehrere
                                // da muss aufgeteilt werden, in etwa so, aber weiß noch nicht genau...
                                //            toCheck = true;
                                //            items[i].Split();
                                //            // intersectionEdgesDictionary.Remove(key, items[i]);
                                //            // das folgende RemoveAll kann langsam sein, dazu Konzept vielleicht überdenken
                                //            // erstmal ganz entfernt, denn items, die keine list sind werden nicht beachtet
                                //            //intersectionEdgesDictionary.RemoveAll(delegate(KeyValuePair<DoubleFaceKey, ICollection<OctTree<BRepItem>.Node<BRepItem>>> kv)
                                //            //{
                                //            //    return kv.Value.Contains(items[i]);
                                //            //});
                                //            SortInNodes(items[i]); // wieder in das intersectionEdgesDictionary einsortieren
#if DEBUG
                                DebuggerContainer dc1 = new DebuggerContainer();
                                Solid s = node.cube.AsBox;
                                s.ColorDef = new ColorDef("node", System.Drawing.Color.Red);
                                dc1.Add(s);
                                for (int i = 0; i < neighbours.Length; ++i)
                                {
                                    for (int j = 0; j < neighbours[i].Length; ++j)
                                    {
                                        s = neighbours[i][j].cube.AsBox;
                                        s.ColorDef = new ColorDef("neighbour", System.Drawing.Color.Blue);
                                        dc1.Add(s, i * 100 + j);
                                    }
                                }
#endif
                                //throw new NotImplementedException("Split node ambiguous connection");
                                break;
                            }
                            else
                            {   // es geht eindeutig weiter, diesen Zwischenpunkt mit in die Liste nehmen
                                chain.Add(ips[0]);
                                node = goOnWith[0];
                                if (vertexitems.Contains(node))
                                {   // es ist ein vertexknoten, also ist hier Schluss
                                    itemVertex = ContainsVertex(node, key); // extrahiere den Vertex aus dem Node
                                    chain.Add(itemVertex.vertex.Position); // verwende als letzten Punkt dessen Position
                                    endVertex = itemVertex.vertex; // die beiden braucht man wg. der Topologie
                                    startEndVertices.Add(new Pair<Vertex, Vertex>(startVertex, endVertex));
                                    vertexitems.Remove(node);
                                    usedNodes.Add(node); // del letzten nur zum Debuggen
                                    break;
                                }
                            }
                        } while (node != null);
#if DEBUG
                        DebuggerContainer dc = new DebuggerContainer();
                        for (int i = 0; i < usedNodes.Count; ++i)
                        {
                            Solid s = usedNodes[i].cube.AsBox;
                            dc.Add(s);
                        }
                        for (int i = 0; i < chain.Count - 1; ++i)
                        {
                            Line line = Line.Construct();
                            line.SetTwoPoints(chain[i], chain[i + 1]);
                            dc.Add(line);
                        }
                        dc.Add(key.face1);
                        dc.Add(key.face2);
#endif
                    }
                    //for (int i = 0; i < items.Count; ++i) if (items[i].list != null)
                    //    {
                    //        // maximal zwei links pro Knoten (vor und zurück), ist der passende Vertex enthalten, dann nur einer.
                    //        BRepItem itemVertex = ContainsVertex(items[i], key);
                    //        bool vertexInvalid = false;
                    //        if (itemVertex != null)
                    //        {
                    //            if (itemVertex.isSeam) vertexInvalid = linklist[items[i]].Count != 2; // es muss genau 2 geben
                    //            else vertexInvalid = linklist[items[i]].Count > 1;
                    //        }
                    //        if (linklist[items[i]].Count > 2 || vertexInvalid)
                    //        {
                    //            if (items[i].deepth > 4 && !overlappingChecked) // hier noch ausprobieren wg. Geschwindigkeit
                    //            {
                    //                overlappingChecked = true;
                    //                ModOp2D m2d;
                    //                if (key.face1.Surface.SameGeometry(key.face1.GetUVBounds(), key.face2.Surface, key.face2.GetUVBounds(), precision, out m2d))
                    //                {
                    //                    overlappingFaces.Add(key, m2d);
                    //                    overlapping = true;
                    //                    break;
                    //                }
                    //            }
                    //            toCheck = true;
                    //            items[i].Split();
                    //            // intersectionEdgesDictionary.Remove(key, items[i]);
                    //            // das folgende RemoveAll kann langsam sein, dazu Konzept vielleicht überdenken
                    //            // erstmal ganz entfernt, denn items, die keine list sind werden nicht beachtet
                    //            //intersectionEdgesDictionary.RemoveAll(delegate(KeyValuePair<DoubleFaceKey, ICollection<OctTree<BRepItem>.Node<BRepItem>>> kv)
                    //            //{
                    //            //    return kv.Value.Contains(items[i]);
                    //            //});
                    //            SortInNodes(items[i]); // wieder in das intersectionEdgesDictionary einsortieren
                    //        }
                    //    }
                    //if (!toCheck)
                    //{   // die Kanten können erzeugt werden, die linklist liefert die Grundlage dazu
                    //    // anfangen mit einem linklist item mit der Länge 1, dann unter Abbau der linklis
                    //    // fortschreiten bis nicht mehr geht. Solang wiederholen bis nichts mehr da ist.
                    //    for (int i = items.Count - 1; i >= 0; --i)
                    //    {   // alle items, die keine Liste sind entfernen, das ist nur alter Ballast
                    //        if (items[i].list == null) items.RemoveAt(i);
                    //    }
                    //    while (items.Count > 0)
                    //    {
                    //        Node<BRepItem> startWith = null;
                    //        BRepItem startVertex = null;
                    //        for (int i = 0; i < items.Count; ++i)
                    //        {
                    //            BRepItem tst = ContainsVertex(items[i], key);
                    //            if (tst != null)
                    //            {
                    //                startWith = items[i];
                    //                startVertex = tst;
                    //                items.Remove(startWith); // vorher Set daraus machen geht evtl. schneller
                    //                break;
                    //            }
                    //        }
                    //        if (startWith == null) break; // TODO: evtl. innere Schleife hier erzeugen
                    //        Node<BRepItem> previous = null;
                    //        List<GeoPoint> intermediatePoints = new List<GeoPoint>();
                    //        bool goon = true;
                    //        while (goon)
                    //        {
                    //            goon = false;
                    //            foreach (Node<BRepItem> bri in linklist[startWith])
                    //            {
                    //                if (bri != previous)
                    //                {
                    //                    previous = startWith;
                    //                    startWith = bri;
                    //                    items.Remove(bri);
                    //                    if (ContainsVertex(bri, key) != null) break; // zyklisch, z.B Zylinder
                    //                    intermediatePoints.Add(bri.center);
                    //                    goon = true;
                    //                    break; // evtl. Zwischenpunkte merken
                    //                }
                    //            }
                    //        }
                    //        BRepItem endVertex = ContainsVertex(startWith, key);
                    //        CreateMixedEdge(key.face1, key.face2, startVertex, endVertex);
                    //    }
                    //}
                    if (!toCheck)
                    {   // hier erstmal für nur zwei Punkte, wir brauchen dazu noch eine andere struktur, die
                        // zwei punkte und alle items der Zwischenpunkte enthält. Obwohl die Zwischenpunkte
                        // nicht ausgewertet werden
                        for (int i = 0; i < chains.Count; ++i)
                        {
                            Vertex startVertex = startEndVertices[i].First;
                            Vertex endVertex = startEndVertices[i].Second;
                            CreateMixedEdge(key.face1, key.face2, startVertex, endVertex, chains[i]);
                        }
                        //if (vertexitems.Count == 2)
                        //{

                        //    Vertex startVertex = ContainsVertex(vertexitems[0], key).vertex;
                        //    Vertex endVertex = ContainsVertex(vertexitems[1], key).vertex;
                        //    CreateMixedEdge(key.face1, key.face2, startVertex, endVertex);
                        //}
                    }
                }
            }
        }
        private void CreateMixedEdge(Face face1, Face face2, Vertex startVertex, Vertex endVertex, List<GeoPoint> points)
        {
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < points.Count - 1; ++i)
            {
                Line line = Line.Construct();
                line.SetTwoPoints(points[i], points[i + 1]);
                dc.Add(line);
            }
#endif
            if (startVertex == endVertex) return; // geschlossene (periodic) Faces gibt es ja nicht
            ICurve curve = Surfaces.Intersect(face1.Surface, face1.GetUVBounds(), face2.Surface, face2.GetUVBounds(), points);
            GeoVector dircurve = curve.StartDirection;
            GeoPoint2D uv1 = face1.Surface.PositionOf(curve.StartPoint);
            GeoVector dirf1 = face1.Surface.UDirection(uv1) ^ face1.Surface.VDirection(uv1);
            GeoPoint2D uv2 = face2.Surface.PositionOf(curve.StartPoint);
            GeoVector dirf2 = face2.Surface.UDirection(uv2) ^ face2.Surface.VDirection(uv2);
            // die orientierung der Faces wird als bereits richtig angenommen
            // auch für die Differenz, da ist die 2. Shell "links gemacht"
            Matrix m = new Matrix(dircurve, dirf1, dirf2);
            double det = m.Determinant(); // ??? Richtung bestimmen ???
            Edge edge = new Edge(null, curve);
            bool forward = (operation == Operation.union) != (det > 0);
            edge.SetFace(face1, forward);
            edge.SetFace(face2, !forward);
            edge.MakeVertices(startVertex, endVertex); // damit die beiden Vertices auf die edge zeigen
            if (!faceToMixedEdges.ContainsKey(face1)) faceToMixedEdges.Add(face1, new Set<Edge>());
            faceToMixedEdges[face1].Add(edge);
            if (!faceToMixedEdges.ContainsKey(face2)) faceToMixedEdges.Add(face2, new Set<Edge>());
            faceToMixedEdges[face2].Add(edge);
            // die mixed edges werden später bei SplitFace u.U. anderen Faces zugeordnet. Wir
            // müssen aber wissen wie die ursprüngliche orientierung war und das wird hier gemerkt
            // Dieses Dictionary ist etwas überkandidelt, aber edge hat kein UserData
            forwardIntersection[new EdgeFaceKey(edge, face1)] = forward;
            forwardIntersection[new EdgeFaceKey(edge, face2)] = !forward;
        }
        private void GenerateEdgeFaceIntersections()
        {
            edgesToSplit = new Dictionary<Edge, List<Vertex>>();
            OrderedMultiDictionary<EdgeFaceKey, Node<BRepItem>> edgesToFaces = new OrderedMultiDictionary<EdgeFaceKey, Node<BRepItem>>(true);
            List<Node<BRepItem>> leaves = new List<Node<BRepItem>>(Leaves);
            foreach (Node<BRepItem> node in leaves)
            {
                foreach (BRepItem brep in node.list)
                {
                    if (brep.Type == BRepItem.ItemType.Edge)
                    {
                        Edge edge = brep.edge;
                        IGeoObjectOwner shell = edge.PrimaryFace.Owner;
                        foreach (BRepItem second in node.list)
                        {
                            if (second.Type == BRepItem.ItemType.Face)
                            {
                                if (second.face.Owner != shell)
                                {   // keine Schnitte von Kanten, die ganz im Face liegen
                                    //if (overlappingFaces.ContainsKey(new DoubleFaceKey(edge.PrimaryFace, second.face)) ||
                                    //    overlappingFaces.ContainsKey(new DoubleFaceKey(edge.SecondaryFace, second.face)) ||
                                    //    overlappingFaces.ContainsKey(new DoubleFaceKey(second.face, edge.PrimaryFace)) ||
                                    //    overlappingFaces.ContainsKey(new DoubleFaceKey(second.face, edge.SecondaryFace))) continue;
                                    edgesToFaces.Add(new EdgeFaceKey(edge, second.face), node);
                                }
                            }
                        }

                    }
                }
            }
            // edgesToFaces enthält jetzt alle schnittverdächtigen Paare
            // und dazu noch den node, wenn man Anfangswerte suchen würde...
            foreach (EdgeFaceKey ef in edgesToFaces.Keys)
            {
                GeoPoint[] ip;
                GeoPoint2D[] uvOnFace;
                double[] uOnCurve3D;
                // System.Diagnostics.Trace.WriteLine(" Edge Face intersection: " + ef.edge.GetHashCode().ToString() + ", " + ef.face.GetHashCode().ToString());
                ef.face.Intersect(ef.edge, out ip, out uvOnFace, out uOnCurve3D);
                for (int i = 0; i < ip.Length; ++i)
                {
                    BRepItem[] closeNodes = GetObjectsFromBox(new BoundingCube(ip[i])); // GeoObjectsFromPoint wäre schneller, wenns das gäbe
                    Vertex v = null;
                    for (int j = 0; j < closeNodes.Length; ++j)
                    {
                        if (closeNodes[j].Type == BRepItem.ItemType.Vertex && (closeNodes[j].vertex.Position | ip[i]) < precision)
                        {
                            v = closeNodes[j].vertex;
                            break;
                        }
                    }
                    if (v == null)
                    {
                        if (uOnCurve3D[i] < 0.0 || uOnCurve3D[i] > 1.0)
                        {   // es ist ja bereits überprüft ob der Schnitt innerhalb des faces liegt,
                            // aber nicht ob auch auf der edge. Wenns knapp ist mit der edge und kein vertex gefunden
                            // wurde, dann wird der Punkt aussortiert
                            continue;
                        }
                        v = new Vertex(ip[i]);
                    }
                    v.AddEdge(ef.edge); // wird später wieder entfernt, aber wichtig beim Aufteilen der OctTree Knoten
                    BRepItem toAdd = new BRepItem(this, v, ef.edge, ef.face);
                    if (ef.edge.IsPeriodicEdge) toAdd.isSeam = true; // sollte nicht mehr vorkommen
                    AddObject(toAdd); // in den OctTree
                    if (!edgesToSplit.ContainsKey(ef.edge)) edgesToSplit[ef.edge] = new List<Vertex>();
                    edgesToSplit[ef.edge].Add(v);
                    if (operation == Operation.testonly) return; // ein Schnittpunkt reicht hier
                }
            }
        }
        public enum Operation { union, intersection, difference, testonly, commonface }
        Operation operation;
        public BRepOperationOld(Shell s1, Shell s2, Operation operation)
        {
            if (operation != Operation.testonly)
            {
                s1.AssertOutwardOrientation();
                s2.AssertOutwardOrientation();
            }
            this.operation = operation;
#if DEBUG
            debuggerContainer = new DebuggerContainer();
#endif
            if (operation == Operation.commonface)
            {   // hier nicht klonen, da die original faces gesucht werden
                shell1 = s1;
                shell2 = s2;
            }
            else
            {
                shell1 = s1.Clone() as Shell;   // hier wird gekloned, weil die Faces im Verlauf geändert werden und das Original
                shell2 = s2.Clone() as Shell;   // unverändert bleiben soll. ZUm Debuggen kann man das Klonen weglassen
            }
            Vertex[] dumy = shell1.Vertices; // nur damits berechnet wird
            dumy = shell2.Vertices;
            facesOnShell1 = new Set<Face>();
            facesOnShell2 = new Set<Face>();
            foreach (Face f in shell1.Faces)
            {   // ggf. später weglassen, wenn die Faces immer so orientiert sind
                f.MakeTopologicalOrientation();
                List<Face> splitted = f.SplitSeam();
                if (splitted == null || splitted.Count == 0) facesOnShell1.Add(f);
                else
                {
                    facesOnShell1.AddMany(splitted);
                }
            }
            if (operation == Operation.difference)
            {
                foreach (Face f in shell2.Faces)
                {
                    f.MakeInverseOrientation();
                    List<Face> splitted = f.SplitSeam();
                    if (splitted == null || splitted.Count == 0) facesOnShell2.Add(f);
                    else
                    {
                        facesOnShell2.AddMany(splitted);
                    }
                }
            }
            else
            {
                foreach (Face f in shell2.Faces)
                {
                    f.MakeTopologicalOrientation();
                    List<Face> splitted = f.SplitSeam();
                    if (splitted == null || splitted.Count == 0) facesOnShell2.Add(f);
                    else
                    {
                        facesOnShell2.AddMany(splitted);
                    }
                }
            }
            shell1.SetFaces(facesOnShell1.ToArray()); // wir brauchen alle edges, vertices, faces
            shell2.SetFaces(facesOnShell2.ToArray()); // das ändert setzt die Owner auf shell1 bzw shell2
#if DEBUG
            DebuggerContainer dc1 = new DebuggerContainer();
            foreach (Edge edg in shell1.Edges)
            {
                if (edg.Curve3D != null) dc1.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
            }
            DebuggerContainer dc2 = new DebuggerContainer();
            foreach (Edge edg in shell2.Edges)
            {
                if (edg.Curve3D != null) dc2.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
            }
#endif
            BoundingCube ext1 = shell1.GetExtent(0.0);
            BoundingCube ext2 = shell2.GetExtent(0.0);
            BoundingCube ext = ext1;
            ext.MinMax(ext2);
            Random rnd = new Random(1234567890); // zum Debuggen besser ein fester wert
            bool success = false;
            while (!success)
            {
                try
                {
                    BoundingCube extrnd = ext;
                    double s = ext.Size;
                    extrnd.Xmax += s * 1e-4 * rnd.NextDouble(); // zufällige Werte um bei symmetrischen Objekten die Wahrscheinlichkeit
                    extrnd.Ymax += s * 1e-4 * rnd.NextDouble(); // zu veringern, dass Ecken (Vertices) in zwei cubes fallen
                    extrnd.Zmax += s * 1e-4 * rnd.NextDouble(); // zuerst mal statisch zum besseren Debuggen
                    base.Initialize(extrnd, extrnd.Size * 1e-6);
                    // Zuerst alle vertices, edges und faces in den OctTree einfügen und zwar in dieser Reihenfolge
                    // da es so schneller ist
                    foreach (Vertex vtx in shell1.Vertices)
                    {
                        base.AddObject(new BRepItem(this, vtx));
                    }
                    foreach (Vertex vtx in shell2.Vertices)
                    {
                        BRepItem bri = new BRepItem(this, vtx);
                        BRepItem[] close = base.GetObjectsCloseTo(bri);
                        bool replaced = false;
                        for (int i = 0; i < close.Length; ++i)
                        {
                            if (close[i].Type == BRepItem.ItemType.Vertex)
                            {
                                if ((close[i].vertex.Position | vtx.Position) < precision)
                                {   // Vertex von der 2. Shell stimmt mit Vertex von der 1. Shell überein: 
                                    // zu einem Vertex machen
                                    replaced = true;
                                    foreach (Edge e in vtx.Edges)
                                    {
                                        try
                                        {
                                            e.ReplaceVertex(vtx, close[i].vertex);
                                        }
                                        catch (ApplicationException) { }
                                    }
                                }
                            }
                        }
                        if (!replaced) base.AddObject(new BRepItem(this, vtx));
                    }
                    foreach (Edge edg in shell1.Edges)
                    {
                        base.AddObject(new BRepItem(this, edg));
                    }
                    foreach (Edge edg in shell2.Edges)
                    {
                        base.AddObject(new BRepItem(this, edg));
                    }
                    foreach (Face fc in shell1.Faces)
                    {
                        base.AddObject(new BRepItem(this, fc));
                    }
                    foreach (Face fc in shell2.Faces)
                    {
                        base.AddObject(new BRepItem(this, fc));
                    }
                    // durch besseres Aufsplitten des OctTrees evtl. hier Geschwindigkeitsgewinn 
                    // RI: im OctTree sind also alle faces, edges und vertices, wobei nicht unterschieden wird, von welcher shell sie kommen.
                    // Identische Vertices aus verschiedenen Shells sind nur einmal drin
                    overlappingFaces = new Dictionary<DoubleFaceKey, ModOp2D>();
                    CheckFacesOnIdenticalSurfaces(); // tut z.Z. nix
                    GenerateEdgeFaceIntersections(); // Schnittpunkte von Edges mit Faces von verschiedenen shells
                    // sollte eine exception werfen, wenn ein Eckpunkt auf die Grenze (precision) zwischen zwei cubes fällt
                    // dann mit etwas geänderten Grenzen neu beginnen
                    if (operation != Operation.testonly)
                    {   // Für testonly genügen die Kantenschnitte (fast)
                        SplitEdges(); // mit den gefundenen Schnittpunkten werden die Edges jetzt gesplittet
                        intersectionEdgesDictionary = new OrderedMultiDictionary<DoubleFaceKey, Node<BRepItem>>(true);
                        faceToMixedEdges = new Dictionary<Face, Set<Edge>>();
                        forwardIntersection = new Dictionary<EdgeFaceKey, bool>();
                        SortInNodes(node); // füllt intersectionEdgesDictionary
                        if (operation != Operation.commonface)
                        {
                            RefineNodes(); // verfeinert intersectionEdgesDictionary, so dass es nur ein oder zwei Nachbarn gibt
                        }
                        // und erzeugt die "mixedEdges", Kanten zwischen zwei Faces von verschiedenen Shells
                        // debuggerContainer.Add(Debug);
                    }
                    success = true;
                }
                catch (VertexIsOnCubeBoundsException)
                {   // success bleibt false
                    success = (operation == Operation.testonly) && edgesToSplit != null && edgesToSplit.Count > 0; // also schon was gefunden
                    if (success) break;
                }
            }
        }

        private void CheckFacesOnIdenticalSurfaces()
        {
            // Make a list of overlapping faces to avoid intersecion of edges and faces which are on the same surface
            Set<DoubleFaceKey> candidates = new Set<DoubleFaceKey>(); // Kandidaten für paralle faces
            List<Node<BRepItem>> leaves = new List<Node<BRepItem>>(Leaves);
            foreach (Node<BRepItem> node in leaves)
            {
                foreach (BRepItem brep in node.list)
                {
                    if (brep.Type == BRepItem.ItemType.Face)
                    {
                        Face f1 = brep.face;
                        IGeoObjectOwner shell = f1.Owner;
                        foreach (BRepItem second in node.list)
                        {
                            if (second.Type == BRepItem.ItemType.Face)
                            {
                                if (second.face.Owner != shell)
                                {   // DoubleFaceKey must be in correct order
                                    if (shell == shell1)
                                    {
                                        candidates.Add(new DoubleFaceKey(f1, second.face));
                                    }
                                    else
                                    {
                                        candidates.Add(new DoubleFaceKey(second.face, f1));
                                    }
                                }
                            }
                        }

                    }
                }
            }
            foreach (DoubleFaceKey df in candidates)
            {
                ModOp2D firstToSecond;
                BoundingRect ext1, ext2;
                df.face1.Surface.GetNaturalBounds(out ext1.Left, out ext1.Right, out ext1.Bottom, out ext1.Top);
                df.face2.Surface.GetNaturalBounds(out ext2.Left, out ext2.Right, out ext2.Bottom, out ext2.Top);
                if (df.face1.Surface.SameGeometry(ext1, df.face2.Surface, ext2, this.precision, out firstToSecond))
                {
                    overlappingFaces.Add(df, firstToSecond);
                }
            }
        }
        /// <summary>
        /// Liefert die Vereinigung der beiden Shells. Das können mehrere Shells sein, denn es kann eine innere Höhlung entstehen.
        /// </summary>
        /// <returns></returns>
        public Shell[] Result()
        {
            List<Shell> res = new List<Shell>();
            // hier evtl Richtungen korrigieren
            originalFaces = new Set<Face>();
            splittedFaces = new Set<Face>();
            originalFaces.AddMany(facesOnShell1);
            originalFaces.AddMany(facesOnShell2);
            SplitAllFaces();
            // originalFaces enthält nur noch die unberührten
            // SplittedFaces die gesplitteten
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            DebuggerContainer dcsplf = new DebuggerContainer();
            Set<Edge> dbgSplEdg = new Set<Edge>();
            foreach (Face fc in splittedFaces)
            {
                dbgSplEdg.AddMany(fc.AllEdges);
                dcsplf.Add(fc);
            }
            foreach (Edge e in dbgSplEdg)
            {
                dc.Add(e.Curve3D as IGeoObject, e.GetHashCode());
            }
            Set<Edge> dbgOrgEdg = new Set<Edge>();
            foreach (Face fc in originalFaces)
            {
                dbgOrgEdg.AddMany(fc.AllEdges);
            }
            foreach (Edge e in dbgOrgEdg)
            {
                dc.Add(e.Curve3D as IGeoObject, e.GetHashCode());
            }
#endif
            bool contact = false; // einfacher ganzflächiger Kontakt zweier shells (zusammenkleben)
            if (operation == Operation.union)
            {   // nur bei Vereinigung müssen die überlappenden Faces noch berücksichtigt werden und zwar
                // die Teile, die nur von Schnittkanten eingeschlossen sind
                foreach (KeyValuePair<DoubleFaceKey, ModOp2D> kv in overlappingFaces)
                {
                    if (kv.Value.Determinant > 0) // die selbe Orientierung
                    {
                        Dictionary<Vertex, Pair<Edge, bool>> intersectionEdges = new Dictionary<Vertex, Pair<Edge, bool>>();
                        if (faceToMixedEdges.ContainsKey(kv.Key.face1))
                        {
                            foreach (Edge e in faceToMixedEdges[kv.Key.face1])
                            {
                                bool f1 = !splittedFaces.Contains(e.PrimaryFace);
                                bool f2 = !splittedFaces.Contains(e.SecondaryFace);
                                if (f1 || f2)
                                {
                                    if (f2) e.RemoveSecondaryFace();
                                    if (f1) e.RemovePrimaryFace();
                                    bool forward = forwardIntersection[new EdgeFaceKey(e, kv.Key.face1)];
                                    Vertex v;
                                    if (forward) v = e.Vertex1;
                                    else v = e.Vertex2;
                                    intersectionEdges.Add(v, new Pair<Edge, bool>(e, forward));
                                }
                            }
                        }
                        if (faceToMixedEdges.ContainsKey(kv.Key.face2))
                        {
                            foreach (Edge e in faceToMixedEdges[kv.Key.face2])
                            {
                                bool f1 = !splittedFaces.Contains(e.PrimaryFace);
                                bool f2 = !splittedFaces.Contains(e.SecondaryFace);
                                if (f1 || f2)
                                {
                                    if (f2) e.RemoveSecondaryFace();
                                    if (f1) e.RemovePrimaryFace();
                                    bool forward = forwardIntersection[new EdgeFaceKey(e, kv.Key.face2)];
                                    Vertex v;
                                    if (forward) v = e.Vertex1;
                                    else v = e.Vertex2;
                                    if (!intersectionEdges.ContainsKey(v))
                                    {
                                        intersectionEdges.Add(v, new Pair<Edge, bool>(e, forward));
                                    }
                                }
                            }
                        }
                        // alle mixedEdges, die eines der beiden overlappingFaces betreffen sind jetzt gesammelt
                        // DebuggerContainer dc1 = new DebuggerContainer();
                        // dc1.Add(kv.Key.face1);
                        // dc1.Add(kv.Key.face2);
                        foreach (Pair<Edge, bool> ef in intersectionEdges.Values)
                        {
                            ICurve c = ef.First.Curve3D.Clone();
                            if (!ef.Second) c.Reverse();
                            // dc1.Add(c as IGeoObject);
                        }
                        while (intersectionEdges.Count > 0)
                        {
                            Face fc = Face.Construct();
                            fc.Surface = kv.Key.face1.Surface; // willkürlich
                            Dictionary<Vertex, Pair<Edge, bool>>.Enumerator sten = intersectionEdges.GetEnumerator();
                            sten.MoveNext();
                            Edge goOnWith = sten.Current.Value.First;
                            intersectionEdges.Remove(sten.Current.Key);
                            goOnWith.SetFace(fc, sten.Current.Value.Second);
                            Vertex stopAtVertex = sten.Current.Key;
                            Vertex currentVertex = goOnWith.EndVertex(fc);
                            List<Edge> outline = new List<Edge>();
                            outline.Add(goOnWith);
                            while (currentVertex != stopAtVertex)
                            {
                                Pair<Edge, bool> nextItem;
                                if (intersectionEdges.TryGetValue(currentVertex, out nextItem))
                                {
                                    intersectionEdges.Remove(currentVertex);
                                    goOnWith = nextItem.First;
                                    goOnWith.SetFace(fc, nextItem.Second);
                                    currentVertex = goOnWith.EndVertex(fc);
                                    outline.Add(goOnWith);
                                }
                                else
                                {
                                    break;
                                }
                            }
                            if (currentVertex == stopAtVertex)
                            {
                                fc.Set(kv.Key.face1.Surface, outline.ToArray(), null);
                                splittedFaces.Add(fc);
                            }
                        }
                    }
                    else
                    {   // zwei berührende gegensätzlich orientierte Faces
                        if (!faceToMixedEdges.ContainsKey(kv.Key.face1) && !faceToMixedEdges.ContainsKey(kv.Key.face2))
                        {   // sie enthalten keine mixed edges, der Verdacht dass es aneinanderklebende Seiten
                            // sind besteht hier. Noch genauer prüfen
                            // Kommt hier nie rein, gibt es ein Beispiel?
                            if (splittedFaces.Count == 0)
                            {
                                originalFaces.Remove(kv.Key.face1);
                                originalFaces.Remove(kv.Key.face2);
                                contact = true;
                            }
                        }
                    }
                }
            }
            if (contact)
            {   // damit die folgende Schleife anfängt
                // kommt nie dran...
                Face toMove = originalFaces.GetAny();
                if (toMove != null)
                {
                    originalFaces.Remove(toMove);
                    splittedFaces.Add(toMove);
                }
            }
#if DEBUG
            DebuggerContainer dc2 = new DebuggerContainer();
            // dc1.Add(kv.Key.face1);
            // dc1.Add(kv.Key.face2);
            foreach (Face fc in splittedFaces)
            {
                dc2.Add(fc as IGeoObject);
            }
#endif
            while (splittedFaces.Count > 0)
            {
                Face startWith = splittedFaces.GetAny();
                splittedFaces.Remove(startWith);
                Set<Face> shell = new Set<Face>();
                shell.Add(startWith);
                Set<Edge> checkTheseEdges = new Set<Edge>(startWith.AllEdges);
                Predicate<Face> check = delegate (Face f)
                    {
                        return !checkTheseEdges.IsDisjointFrom(new Set<Edge>(f.AllEdges));
                    };
                while (true)
                {   // ausgehend vom einem neu erzeugten, da aufgeteilten Face werden alle damit zusammenhängenden
                    // aus den beiden Listen entfernt, bis keine neuen mehr gefunden werden
                    ICollection<Face> fromSplitted = splittedFaces.RemoveAll(check);
                    ICollection<Face> fromOriginal = originalFaces.RemoveAll(check);
                    if (fromSplitted.Count == 0 && fromOriginal.Count == 0) break;
                    shell.AddMany(fromSplitted);
                    shell.AddMany(fromOriginal);
                    checkTheseEdges.Clear();
                    foreach (Face f in fromSplitted)
                    {
                        checkTheseEdges.AddMany(f.AllEdges);
                    }
                    foreach (Face f in fromOriginal)
                    {
                        checkTheseEdges.AddMany(f.AllEdges);
                    }
                }
                Shell found = Shell.Construct();
                found.SetFaces(shell.ToArray());
                // es treten Fälle auf, bei denen nichtgeschlossene Shells erzeugt werden
                // das sind die Häute bei Differenzen mit von innen berührenden Körpern
                Edge[] open = found.OpenEdges;
                if (open.Length > 0) ConnectOpenEdges(open);
                if (found.IsClosed)
                {
                    found.ReduceFaces(precision);
                    res.Add(found);
                }
            }
            return res.ToArray();
        }
        public int GetOverlappingFaces(out Face[] onShell1, out Face[] onShell2, out ModOp2D[] firstToSecond)
        {
            onShell1 = new Face[overlappingFaces.Count];
            onShell2 = new Face[overlappingFaces.Count];
            firstToSecond = new ModOp2D[overlappingFaces.Count];
            int ind = 0;
            foreach (KeyValuePair<DoubleFaceKey, ModOp2D> kv in overlappingFaces)
            {
                onShell1[ind] = kv.Key.face1;
                onShell2[ind] = kv.Key.face2;
                firstToSecond[ind] = kv.Value;
                ++ind;
            }
            return overlappingFaces.Count;
        }
        internal void ConnectOpenEdges(Edge[] openEdges)
        {
            OrderedMultiDictionary<DoubleVertexKey, Edge> dict = new OrderedMultiDictionary<DoubleVertexKey, Edge>(true);
            for (int i = 0; i < openEdges.Length; ++i)
            {
                dict.Add(new DoubleVertexKey(openEdges[i].Vertex1, openEdges[i].Vertex2), openEdges[i]);
            }
            foreach (KeyValuePair<DoubleVertexKey, ICollection<Edge>> kv in dict)
            {
                if (kv.Value.Count == 2)
                {
                    Edge e1 = null;
                    Edge e2 = null;
                    foreach (Edge e in kv.Value)
                    {
                        if (e1 == null) e1 = e;
                        else e2 = e;
                    }
                    if (e1.Curve3D.SameGeometry(e2.Curve3D, precision))
                    {
                        e1.SetSecondary(e2.PrimaryFace, e2.Curve2D(e2.PrimaryFace), e2.Forward(e2.PrimaryFace));
                        e2.PrimaryFace.ReplaceEdge(e2, new Edge[] { e1 });
                    }
                }
            }
        }
        private void SplitEdges()
        {
            facesToSplit = new Dictionary<Face, Set<Edge>>();
            // 1. Alle Kanten an den Schnittpunkten aufbrechen und für die betroffenen
            // Faces die Liste der möglichen neuen Kanten erstellen
            foreach (KeyValuePair<Edge, List<Vertex>> kv in edgesToSplit)
            {
                Edge edge = kv.Key;
                Set<Vertex> vertexSet = new Set<Vertex>(kv.Value); // einzelne vertices können doppelt vorkommen
                SortedList<double, Vertex> sortedVertices = new SortedList<double, Vertex>();
                foreach (Vertex v in vertexSet)
                {
                    double pos = edge.Curve3D.PositionOf(v.Position);
                    sortedVertices.Add(pos, v);
                    if (v != edge.Vertex1 && v != edge.Vertex2) v.RemoveEdge(edge);
                }
                if (!facesToSplit.ContainsKey(edge.PrimaryFace))
                {
                    facesToSplit[edge.PrimaryFace] = new Set<Edge>(edge.PrimaryFace.AllEdges);
                }
                if (!facesToSplit.ContainsKey(edge.SecondaryFace))
                {
                    facesToSplit[edge.SecondaryFace] = new Set<Edge>(edge.SecondaryFace.AllEdges);
                }
                // die eine Kante raus, die neuen rein in die Liste
                facesToSplit[edge.PrimaryFace].Remove(edge);
                facesToSplit[edge.SecondaryFace].Remove(edge);
                Edge[] splitted = edge.Split(sortedVertices, precision);
                // im OctTree anpassen wäre schwierig (löschen BRepItem nicht bekannt
                // und ist nicht notwendig: beim Erzeugen der MixedEdges kann man über die Vertices feststellen
                // ob es schon ein solches edge gibt
                // nicht mehr notwendig:
                //for (int i = 0; i < splitted.Length; ++i)
                //{
                //    splitted[i].IsPartOf = edge; // damit man weiß ob outline oder hole 
                //}
                facesToSplit[edge.PrimaryFace].AddMany(splitted);
                facesToSplit[edge.SecondaryFace].AddMany(splitted);
                // Die Vertices sollen nicht mehr auf die Edge zeigen, da später beim Finden der neuen
                // Umrandungen sonst mehrdeutigkeiten entstehen. Das Face ist damit aber inkonsistent
                edge.Vertex1.RemoveEdge(edge);
                edge.Vertex2.RemoveEdge(edge);
            }
        }
        private void SplitAllFaces()
        {
            // Alle Faces, die mixed edges enthalten müssen gesplittet werden
            // es kann sein, dass noch welche fehlen, nämlich die, bei denen keine Kante aufgebrochen wurde
            foreach (Face fc in faceToMixedEdges.Keys)
            {
                if (!facesToSplit.ContainsKey(fc))
                {
                    facesToSplit[fc] = new Set<Edge>(fc.AllEdges);
                }
            }
            // 2. die so veränderten Faces ersetzen durch die nach der Aufteilung noch gültigen Faces
            foreach (Face fc in facesToSplit.Keys)
            {
                Set<Edge> split = facesToSplit[fc];
                Set<Edge> mixed;
                if (!faceToMixedEdges.TryGetValue(fc, out mixed)) continue;
                if (SplitFace(fc, split, mixed))
                {
                    originalFaces.Remove(fc); // die kommen im Endergebnis nicht vor
                }
            }
        }
        private bool SplitFace(Face toSplit, Set<Edge> originalEdges, Set<Edge> intersectionEdges)
        {   // das gegebene face hat noch seine ursprünglichen Kanten. In originalEdges sind die aufgeteilten
            // und die originalKanten, d.h. auch ein konsistenter Zustand. Die intersectionEdges müssen jetzt
            // zugefügt werden und dafür einige der originalEdges rausgeworfen werden. das ist ein rein
            // kombinatorisches Problem, nicht geometrisch!
            // Alle intersectionEdges sind von Bedeutung, d.h. es wird keine intersectionEdge geben,
            // die nicht im Ergebnis vorkommt (wie könnte das sein?). Durch die Rechtsorientierung der Inseln ist es 
            // hier viel einfacher da man den Kanten nicht ohne weiteres ansieht ob sie zur Insel oder zum Rand gehören.
            // die Kanten werden jetzt gemäß ihrem startvertex sortiert. Es können mehrere Kanten von einem vertex ausgehen
            // TODO: 
            OrderedMultiDictionary<Vertex, Edge> originalSorted = new OrderedMultiDictionary<Vertex, Edge>(true);
            OrderedMultiDictionary<Vertex, Edge> intersectionSorted = new OrderedMultiDictionary<Vertex, Edge>(true);
            foreach (Edge e in originalEdges)
            {
                originalSorted.Add(e.StartVertex(toSplit), e);
            }
            // zuerst gegenläufige mixed edges entfernen, gleichläufige nur einmal rein
            foreach (Edge e in intersectionEdges)
            {
                // DEBUG:
                e.Curve3D.PointAt(0.111);
                // END DEBUG
                Vertex v = e.EndVertex(toSplit);
                Edge found = null;
                foreach (Edge o in intersectionSorted[v])
                {
                    if (o.EndVertex(toSplit) == e.StartVertex(toSplit))
                    {
                        if (Edge.IsGeometricallyEqual(o, e, true, false, precision))
                        {
                            found = o;
                            break; // es kann ja nicht mit einer zweiten outline auch noch identisch sein
                        }
                    }
                }
                if (found != null)
                {   // eine exakt gegenläufige Schnittkante gefunden (z.B. BRepTest6c.cdb), beide müssen raus
                    intersectionSorted.Remove(found.StartVertex(toSplit), found);
                    continue; // die gibts identisch mehrfach, wird nur einmal benötigt. Die erste läuft ja durch
                }
                v = e.StartVertex(toSplit);
                foreach (Edge o in intersectionSorted[v])
                {
                    if (o.EndVertex(toSplit) == e.EndVertex(toSplit))
                    {
                        if (Edge.IsGeometricallyEqual(o, e, true, true, precision))
                        {
                            found = o;
                            break; // es kann ja nicht mit einer zweiten outline auch noch identisch sein
                        }
                    }
                }
                if (found != null) continue; // exakt gleichläufige darf nur einmal rein
                intersectionSorted.Add(v, e);
            }
            // Wenn eine original und intersection Kante identisch sind, dann gilt folgende Regel:
            // gegenläufig: beide weg, gleiche Richtung, outline muss weg, da die intersection verbindend wirken kann
            List<Edge> toIterate = new List<Edge>(intersectionSorted.Values);
            foreach (Edge e in toIterate)
            {
                Vertex v = e.StartVertex(toSplit);
                if (originalSorted.ContainsKey(v))
                {
                    foreach (Edge o in originalSorted[v])
                    {
                        if (o.EndVertex(toSplit) == e.EndVertex(toSplit))
                        {
                            if (true) // hier Test für geometrische Gleichheit
                            {
                                originalSorted.Remove(v, o);
                                break; // es kann ja nicht mit einer zweiten outline auch noch identisch sein
                            }
                        }
                    }
                }
                v = e.EndVertex(toSplit);
                if (originalSorted.ContainsKey(v))
                {
                    bool found = false;
                    foreach (Edge o in originalSorted[v])
                    {
                        if (o.EndVertex(toSplit) == e.StartVertex(toSplit))
                        {
                            if (true) // hier Test für geometrische Gleichheit
                            {   // gegenläufig: beide weg
                                originalSorted.Remove(v, o);
                                found = true;
                                break;
                            }
                        }
                    }
                    if (found)
                    {
                        intersectionSorted.Remove(e.StartVertex(toSplit), e);
                    }
                }
            }
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            // dc.Add(toSplit);
            foreach (Edge e in originalSorted.Values)
            {
                if (e.Curve3D != null)
                {
                    ICurve c = e.Curve3D.Clone();
                    if (!e.Forward(toSplit))
                    {
                        c.Reverse();
                    }
                    dc.Add(c as IGeoObject, e.GetHashCode());
                    Point pnt1 = Point.Construct();
                    pnt1.Location = e.Vertex1.Position;
                    dc.Add(pnt1, e.Vertex1.GetHashCode());
                    Point pnt2 = Point.Construct();
                    pnt2.Location = e.Vertex2.Position;
                    dc.Add(pnt2, e.Vertex2.GetHashCode());
                }
            }
            foreach (Edge e in intersectionSorted.Values)
            {
                if (e.Curve3D != null)
                {
                    ICurve c = e.Curve3D.Clone();
                    if (!e.Forward(toSplit))
                    {
                        c.Reverse();
                    }
                    dc.Add(c as IGeoObject, e.GetHashCode());
                    Point pnt1 = Point.Construct();
                    pnt1.Location = e.Vertex1.Position;
                    dc.Add(pnt1, e.Vertex1.GetHashCode());
                    Point pnt2 = Point.Construct();
                    pnt2.Location = e.Vertex2.Position;
                    dc.Add(pnt2, e.Vertex2.GetHashCode());
                }
            }
#endif
            if (intersectionSorted.Count == 0)
            {
                return false;
            }
            List<List<Edge>> outlines = new List<List<Edge>>();
            List<List<Edge>> holes = new List<List<Edge>>();
            while (intersectionSorted.Count > 0)
            {
                Edge edge = intersectionSorted.FirstItem.Value;
                List<Edge> outline = new List<Edge>();
                Vertex nextVertex = edge.EndVertex(toSplit);
                Vertex stopAt = edge.StartVertex(toSplit); // hier angekommen ist Schluss
                Edge goOn = edge;
                bool onlyIntersectionEdges = true; // Kuren, die nur aus Schnittlinien bestehen sind Löcher
                Set<Edge> periodics = new Set<Edge>();
                bool lastEdgeIsIntersection = true;
                intersectionSorted.Remove(stopAt, goOn);
                while (goOn != null)
                {
                    outline.Add(goOn);
                    if (originalEdges.Contains(goOn)) onlyIntersectionEdges = false;
                    if (goOn.EndVertex(toSplit) == stopAt) break; // fertig
                    // wie gehts weiter: wenn man auf einer Outline ist, geht es vorzugsweise mit intersection
                    // weiter, wenn man auf einer intersection ist, dann vorzugsweise mit outline
                    // wenn es mehrere Möglichkeiten gibt, muss noch genauer gecheckt werden
                    List<Edge> collection = null;
                    if (false) // lastEdgeIsIntersection)
                    {
                        if (originalSorted.ContainsKey(nextVertex))
                        {
                            collection = new List<Edge>(originalSorted[nextVertex]);
                            lastEdgeIsIntersection = false;
                        }
                        else
                        {
                            collection = new List<Edge>(intersectionSorted[nextVertex]);
                            lastEdgeIsIntersection = true;
                        }
                    }
                    else
                    {
                        if (intersectionSorted.ContainsKey(nextVertex))
                        {
                            collection = new List<Edge>(intersectionSorted[nextVertex]);
                            lastEdgeIsIntersection = true;
                        }
                        else
                        {
                            collection = new List<Edge>(originalSorted[nextVertex]);
                            lastEdgeIsIntersection = false;
                        }
                    }
                    goOn = null;
                    if (collection.Count == 1)
                    {
                        Edge e = collection[0];
                        Vertex v = e.StartVertex(toSplit);
                        if (v != stopAt)
                        {
                            if (lastEdgeIsIntersection) intersectionSorted.Remove(nextVertex, e);
                            else originalSorted.Remove(nextVertex, e);
                            goOn = e;
                            nextVertex = e.EndVertex(toSplit);
                        }
                    }
                    else if (collection.Count > 1)
                    {   // es gibt mehrere Möglichkeiten an einer Stelle weiterzugehen
                        // das muss noch implemenitert werden
                        throw new ApplicationException("multiple connection not implemented");
                    }
                    else
                    {
                        // bei count==0 sind wir fertig und hoffentlich am Ende angekommen
                        // das könnte man hier überprüfen
                    }
                }
                if (onlyIntersectionEdges)
                {
                    if (IsHole(toSplit, outline)) holes.Add(outline);
                    else outlines.Add(outline);
                }
                else outlines.Add(outline);
            }
            // TODO: hier outlines entfernen, die genau in sich rückläufig sind
            bool originalOutline = false;
            if (outlines.Count == 0)
            {   // es gibt nur Löcher, also muss die unbeschädigte original outline dazugehören
                outlines.Add(new List<Edge>(toSplit.OutlineEdges));
                originalOutline = true; // alle holes in der outline, ohne nachzuprüfen
            }
            for (int i = 0; i < toSplit.HoleCount; ++i)
            {   // ungeschnittene Löcher hinzufügen. Ob sie innerhalb oder außerhalb sind wird weiter
                // unten überprüft
                Set<Edge> hole = new Set<Edge>(toSplit.HoleEdges(i));
                if (hole.IsSubsetOf(originalEdges)) holes.Add(new List<Edge>(toSplit.HoleEdges(i)));
            }
            // TODO: jetzt ist es noch möglich, dass originale Löcher völlig unbehelligt blieben vom Schnitt
            // Diese gehören dazu, wenn sie innerhalb eines outlines liegen
            // Das muss noch implementiert werden
            // onlyIntersectionEdges tut wahrscheinlich nicht das Gewünschte. Man muss einfach alle outlines
            // danach untersuchen, ob es Hüllen oder Löcher sind. Wahrscheinlich genügt dazu die Richtung
            // Es bleibt allerdings die Frage, in welcher Outline die Löcher liegen noch zu klären.
            foreach (List<Edge> ol in outlines)
            {
                GeoPoint sp = ol[0].StartVertex(toSplit).Position;
                GeoPoint ep = ol[ol.Count - 1].EndVertex(toSplit).Position;
                if ((sp | ep) > precision) continue; // offene Kontur
                if (ol.Count == 2)
                {
                    if (ol[0].StartVertex(toSplit) == ol[1].EndVertex(toSplit))
                    {
                        if (Edge.IsGeometricallyEqual(ol[0], ol[1], true, false, precision))
                        {   // leere Hülle, zwei identische Edges
                            continue;
                        }
                    }
                }
                if (holes.Count == 0)
                {
                    Face fc = toSplit.Split(ol, null);
                    splittedFaces.Add(fc);
                }
                else if (originalOutline)
                {   // es gibt nur eine outline, alle Löcher sind drin
                    Face fc = toSplit.Split(ol, holes);
                    splittedFaces.Add(fc);
                }
                else
                {   // überpfüfen, ob ein Loch in der outline ist
                    List<List<Edge>> containedHoles = new List<List<Edge>>();
                    for (int i = 0; i < holes.Count; ++i)
                    {
                        if (ContainesHole(toSplit, ol, holes[i]))
                        {
                            containedHoles.Add(holes[i]);
                        }
                    }
                    for (int i = 0; i < containedHoles.Count; ++i)
                    {
                        holes.Remove(containedHoles[i]); // verbrauchte Löcher nicht nochmal testen
                    }
                    Face fc = toSplit.Split(ol, containedHoles);
                    splittedFaces.Add(fc);
                }
            }
            return true;
        }
        private bool ContainesHole(Face face, List<Edge> outline, List<Edge> hole)
        {
            ICurve2D[] bdroutline = new ICurve2D[outline.Count];
            for (int i = 0; i < bdroutline.Length; ++i)
            {
                bdroutline[i] = outline[i].Curve2D(face);
            }
            Border bdr = new Border(bdroutline);
            return bdr.GetPosition(hole[0].Curve2D(face).StartPoint) == Border.Position.Inside;
            // return Border.OutlineContainsPoint(outline, hole[0].Curve2D(face).StartPoint);
        }
        private bool IsHole(Face face, List<Edge> outline)
        {   // feststellen, ob die orientierte Liste von Edges rechtsrum (hole) oder linksrum (outline) geht
            GeoPoint sp = outline[0].StartVertex(face).Position;
            GeoPoint ep = outline[outline.Count - 1].EndVertex(face).Position;
            if ((sp | ep) > precision) return false;
            ICurve2D[] curves = new ICurve2D[outline.Count];
            for (int i = 0; i < curves.Length; ++i)
            {
                curves[i] = outline[i].Curve2D(face);

            }
            return !Border.CounterClockwise(curves);
        }
        protected override bool SplitNode(Node<BRepItem> node, BRepItem objectToAdd)
        {
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < node.list.Count; ++i)
            {
                if (node.list[i].edge != null)
                {
                    if (node.list[i].edge.Curve3D != null) dc.Add(node.list[i].edge.Curve3D as IGeoObject);
                }
                if (node.list[i].face != null) dc.Add(node.list[i].face);
                if (node.list[i].vertex != null) dc.Add(node.list[i].vertex.Position, System.Drawing.Color.Red, i);
            }
#endif
            if (node.deepth < 3 && node.list.Count > 3) return true; // noch einjustieren
            if (node.deepth > 8) return false; // Notbremse
            // Notbremse kann auftreten wenn mehrere Vertices einer Shell identisch sind oder Kanten
            // sich schneiden (dann sind 4 faces in einem Punkt, jeweils 2 von jeder Shell
            // solche Fälle müssten ggf vorab gechecked werden
            if (objectToAdd.Type == BRepItem.ItemType.Vertex)
            {   // keine zwei Vertices aus der selben Shell und auch Schnittvertices getrennt
                // von allen anderen
                // Warum keine zwei vertices aus der selben Shell? Das teilt den Octtree unnötig auf
                // wo gerkeine verschiedenen Shells beteiligt sind
                foreach (BRepItem bi in node.list)
                {
                    if (bi.Type == BRepItem.ItemType.Vertex)
                    {
                        //if (bi.vertex.Edges[0].PrimaryFace.Owner == objectToAdd.vertex.Edges[0].PrimaryFace.Owner ||
                        if (bi.isIntersection || objectToAdd.isIntersection)
                            return true;
                    }
                }
            }
            else if (objectToAdd.Type == BRepItem.ItemType.Face)
            {   // eine der beiden Shells darf nur einfach vertreten sein, warum?
                int nums1 = 0;
                int nums2 = 0;
                if (objectToAdd.face.Owner == shell1) ++nums1;
                else ++nums2;
                foreach (BRepItem bi in node.list)
                {
                    if (bi.Type == BRepItem.ItemType.Face)
                    {
                        if (bi.face.Owner == shell1) ++nums1;
                        else ++nums2;
                    }
                }
                // return (nums1 > 1 && nums2 > 1); warum?
            }
            return false;
        }
        // erstmal weglassen, nicht klar ob das was bringt. In OctTree auch auskommentiert
        //protected override bool FilterHitTest(object objectToAdd, OctTree<BRepItem>.Node<BRepItem> node)
        //{
        //    if (node.list == null) return false; // Abkürzung nur wenn es eine Liste hat
        //    BRepItem bri = objectToAdd as BRepItem;
        //    if (bri.Type==BRepItem.ItemType.Edge)
        //    {
        //        foreach (BRepItem  bi in node.list)
        //        {
        //            if (bi.Type is Vertex)
        //            {
        //                foreach (Edge e in bi.vertex.Edges)
        //                {
        //                    if (e == bri.edge) return true;
        //                }
        //            }
        //        }
        //    }
        //    return false;
        //}

        /// <summary>
        /// Checks whether the two shells intersect each other
        /// </summary>
        /// <returns></returns>
        public bool Intersect(out GeoPoint anyIntersectionPoint)
        {
            if (edgesToSplit.Count > 0)
            {
                foreach (List<Vertex> list in edgesToSplit.Values)
                {
                    if (list.Count > 0)
                    {
                        anyIntersectionPoint = list[0].Position;
                        return true;
                    }
                }
            }
            anyIntersectionPoint = GeoPoint.Origin;
            return false;
        }

        public GeoObjectList DebugEdgesToSplit
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                //Dictionary<Face, Set<Edge>> faceToIntersectionEdges;
                //Dictionary<Edge, List<Vertex>> edgesToSplit;
                //Dictionary<Face, Set<Edge>> facesToSplit; // Faces, dies gesplitted werden sollen und deren originale oder gesplittete
                ColorDef cdp = new ColorDef("point", System.Drawing.Color.Red);
                ColorDef cde = new ColorDef("edge", System.Drawing.Color.Blue);
                foreach (KeyValuePair<Edge, List<Vertex>> item in edgesToSplit)
                {
                    if (item.Key.Curve3D != null)
                    {
                        ((item.Key.Curve3D as IGeoObject) as IColorDef).ColorDef = cde;
                        res.Add(item.Key.Curve3D as IGeoObject);
                    }
                    foreach (Vertex v in item.Value)
                    {
                        Point p = Point.Construct();
                        p.Location = v.Position;
                        p.Symbol = PointSymbol.Cross;
                        p.ColorDef = cdp;
                        res.Add(p);
                    }
                }
                return res;
            }
        }
    }
#endif
#if DEBUG

    public class TestBRep
    {
        static public bool Collision(Shell s1, Shell s2, out GeoPoint collisionpoint)
        {
            BRepOperationOld bro = new BRepOperationOld(s1, s2, BRepOperationOld.Operation.testonly);
            return bro.Intersect(out collisionpoint);
        }
    }
#endif

    /// <summary>
    /// Preliminary
    /// </summary>

    public class CollisionDetection
    {
        Shell s1, s2;
        OctTree<Face> of1, of2;
        Set<Pair<Face, Face>> overlappingFaces;
        Dictionary<Edge, List<double>> IntersectedEdges1; // Schnittpunkte auf Kanten der ersten shell
        Dictionary<Edge, List<double>> IntersectedEdges2;
        /// <summary>
        /// Preliminary
        /// </summary>
        /// <param name="s1"></param>
        /// <param name="s2"></param>
        public CollisionDetection(Shell s1, Shell s2)
        {
            //s1.AssertOutwardOrientation();
            //s2.AssertOutwardOrientation();
            this.s1 = s1;
            this.s2 = s2;
        }
        /// <summary>
        /// Preliminary
        /// </summary>
        /// <param name="precision"></param>
        /// <param name="collisionPoint"></param>
        /// <returns></returns>
        public bool GetResultOld(double precision, out GeoPoint collisionPoint)
        {
            int tttc0 = System.Environment.TickCount;
            // zwei QuadTrees, die die Flächen enthalten
            // (es wäre für die Distance-Methode günstig, sie würden auch die kanten und Eckpunkte enthalten, tun sie aber z.Z. nicht)
            of1 = new OctTree<Face>(s1.GetBoundingCube(), precision);
            of2 = new OctTree<Face>(s2.GetBoundingCube(), precision);
            overlappingFaces = new Set<Pair<Face, Face>>();
            // s1.SplitPeriodicFaces(); // dauert und hilft nicht
            //s2.SplitPeriodicFaces();
            foreach (Face fc in s1.Faces)
            {
                of1.AddObject(fc);
            }
            foreach (Face fc in s2.Faces)
            {
                of2.AddObject(fc);
            }
            // alle überlappenden Faces bestimmen
            foreach (Face fc in s1.Faces)
            {
                Face[] close = of2.GetObjectsCloseTo(fc);
                for (int i = 0; i < close.Length; ++i)
                {
                    BoundingCube bc1 = fc.GetExtent(0.0);
                    BoundingCube bc2 = close[i].GetExtent(0.0);
                    if (bc1.Interferes(bc2))
                    {
                        ModOp2D m;
                        bool overlapping = Surfaces.Overlapping(fc.Surface, fc.Area.GetExtent(), close[i].Surface, close[i].Area.GetExtent(), precision, out m);
                        if (overlapping)
                        {
                            overlappingFaces.Add(new Pair<Face, Face>(fc, close[i])); // zuerst shell1, dann 2
                        }
                    }
                }
            }
            // System.Diagnostics.Trace.WriteLine("Overlapping: " + (System.Environment.TickCount - tttc0).ToString());
            // die Schnittpunkte der Kanten der einen Shell mit den Flächen der anderen Shell bestimmen
            // und umgekehrt
            IntersectedEdges1 = new Dictionary<Edge, List<double>>();
            IntersectedEdges2 = new Dictionary<Edge, List<double>>();
            foreach (Edge edge in s2.Edges)
            {
                if (edge.Curve3D == null) continue;
                BoundingCube curveExt = edge.Curve3D.GetExtent();
                Face[] close = of1.GetObjectsCloseTo(edge.Curve3D as IOctTreeInsertable);
                for (int i = 0; i < close.Length; ++i)
                {
                    if (overlappingFaces.Contains(new Pair<Face, Face>(close[i], edge.PrimaryFace))) continue;
                    if (overlappingFaces.Contains(new Pair<Face, Face>(close[i], edge.SecondaryFace))) continue;
                    if (!curveExt.Interferes(close[i].GetExtent(0.0))) continue;
                    GeoPoint[] ip;
                    GeoPoint2D[] uvOnFace;
                    double[] uOnCurve3D;
                    int tc0 = System.Environment.TickCount;
                    close[i].Intersect(edge, out ip, out uvOnFace, out uOnCurve3D);
                    if (ip != null && ip.Length > 0)
                    {
                        List<double> addTo;
                        if (ip.Length > 2)
                        {   // bei mehreren Schnittpunkten einer Kurve mit einer Fläche ist es
                            // möglich, dass beide praktisch tangential sind. Diese Fälle wollen 
                            // wir hier nicht berücksichtigen
                            bool tangential = true;
                            for (int j = 0; j < ip.Length; j++)
                            {
                                GeoVector v1 = edge.Curve3D.DirectionAt(uOnCurve3D[j]);
                                GeoVector v2 = close[i].Surface.GetNormal(uvOnFace[j]);
                                if (Math.Abs(v1 * v2) / (v1.Length * v2.Length) > 1e-4)
                                {
                                    tangential = false;
                                    break;
                                }
                            }
                            if (tangential) continue;
                        }
                        if (!IntersectedEdges2.TryGetValue(edge, out addTo))
                        {
                            addTo = new List<double>();
                            IntersectedEdges2[edge] = addTo;
                        }
                        addTo.AddRange(uOnCurve3D);
                    }
                    int tc1 = System.Environment.TickCount;
                    // System.Diagnostics.Trace.WriteLine("Intersection: Face: " + close[i].GetHashCode().ToString() + " Edge: " + edge.GetHashCode().ToString() + " Zeit: " + (tc1 - tc0).ToString() + ", " + close[i].Surface.GetType().ToString() + ", " + edge.Curve3D.GetType().ToString());
                }
            }
            // Feststellen, ob ein Kantenschnittpunkt auch wirklich tiefer als precision in die
            // andere Shell eintritt. Dazu werden die Endpunkte der Kante und die Zwischenpunkte
            // zwischen den Schnitten betrachtet. Das ist nicht unbedingt das richtige Maß
            // aber doch ziemlich gut
            int ttc0 = System.Environment.TickCount;
            int dbgcount = 0;
            foreach (KeyValuePair<Edge, List<double>> kv in IntersectedEdges2)
            {
                kv.Value.Sort();
                dbgcount++;
                int dc0 = System.Environment.TickCount;
                double lastdist = Distance(of1, kv.Key.Curve3D.StartPoint);
                int dc1 = System.Environment.TickCount;
                // System.Diagnostics.Trace.WriteLine("Distance: " + dbgcount.ToString() + ", " + (dc1 - dc0).ToString());
                if (lastdist < -precision)
                {
                    collisionPoint = kv.Key.Curve3D.PointAt(kv.Value[0]);
                    return true; // ein Punkt gefunden
                }
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    double nextdist;
                    dbgcount++;
                    dc0 = System.Environment.TickCount;
                    if (i == kv.Value.Count - 1) nextdist = Distance(of1, kv.Key.Curve3D.EndPoint);
                    else nextdist = Distance(of1, kv.Key.Curve3D.PointAt((kv.Value[i] + kv.Value[i + 1]) / 2.0));
                    dc1 = System.Environment.TickCount;
                    // System.Diagnostics.Trace.WriteLine("Distance: " + dbgcount.ToString() + ", " + (dc1 - dc0).ToString());
                    if (nextdist < -precision)
                    {
                        collisionPoint = kv.Key.Curve3D.PointAt(kv.Value[i]);
                        return true; // ein Punkt gefunden
                    }
                }
            }
            int ttc1 = System.Environment.TickCount;
            // System.Diagnostics.Trace.WriteLine("Gefunden: " + IntersectedEdges2.Count.ToString() + ", " + (ttc1 - ttc0).ToString());
            // umgekehrte Rollen
            foreach (Edge edge in s1.Edges)
            {
                if (edge.Curve3D == null) continue;
                BoundingCube curveExt = edge.Curve3D.GetExtent();
                Face[] close = of2.GetObjectsCloseTo(edge.Curve3D as IOctTreeInsertable);
                for (int i = 0; i < close.Length; ++i)
                {
                    if (overlappingFaces.Contains(new Pair<Face, Face>(edge.PrimaryFace, close[i]))) continue;
                    if (overlappingFaces.Contains(new Pair<Face, Face>(edge.SecondaryFace, close[i]))) continue;
                    if (!curveExt.Interferes(close[i].GetExtent(0.0))) continue;
                    GeoPoint[] ip;
                    GeoPoint2D[] uvOnFace;
                    double[] uOnCurve3D;
                    int tc0 = System.Environment.TickCount;
                    close[i].Intersect(edge, out ip, out uvOnFace, out uOnCurve3D);
                    if (ip != null && ip.Length > 0)
                    {
                        if (ip.Length > 2)
                        {   // bei mehreren Schnittpunkten einer Kurve mit einer Fläche ist es
                            // möglich, dass beide praktisch tangential sind. Diese Fälle wollen 
                            // wir hier nicht berücksichtigen
                            bool tangential = true;
                            for (int j = 0; j < ip.Length; j++)
                            {
                                GeoVector v1 = edge.Curve3D.DirectionAt(uOnCurve3D[j]);
                                GeoVector v2 = close[i].Surface.GetNormal(uvOnFace[j]);
                                if (Math.Abs(v1 * v2) / (v1.Length * v2.Length) > 1e-4)
                                {
                                    tangential = false;
                                    break;
                                }
                            }
                            if (tangential) continue;
                        }
                        List<double> addTo;
                        if (!IntersectedEdges1.TryGetValue(edge, out addTo))
                        {
                            addTo = new List<double>();
                            IntersectedEdges1[edge] = addTo;
                        }
                        addTo.AddRange(uOnCurve3D);
                    }
                    int tc1 = System.Environment.TickCount;
                    // System.Diagnostics.Trace.WriteLine("Intersection: Face: " + close[i].GetHashCode().ToString() + " Edge: " + edge.GetHashCode().ToString() + " Zeit: " + (tc1 - tc0).ToString());
                }
            }
            ttc0 = System.Environment.TickCount;
            foreach (KeyValuePair<Edge, List<double>> kv in IntersectedEdges1)
            {
                kv.Value.Sort();
                double lastdist = Distance(of2, kv.Key.Curve3D.StartPoint);
                if (lastdist < -precision)
                {
                    collisionPoint = kv.Key.Curve3D.PointAt(kv.Value[0]);
                    return true; // ein Punkt gefunden
                }
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    double nextdist;
                    if (i == kv.Value.Count - 1) nextdist = Distance(of2, kv.Key.Curve3D.EndPoint);
                    else nextdist = Distance(of2, kv.Key.Curve3D.PointAt((kv.Value[i] + kv.Value[i + 1]) / 2.0));
                    if (nextdist < -precision)
                    {
                        collisionPoint = kv.Key.Curve3D.PointAt(kv.Value[i]);
                        return true; // ein Punkt gefunden
                    }
                }
            }
            ttc1 = System.Environment.TickCount;
            // System.Diagnostics.Trace.WriteLine("Gefunden: " + IntersectedEdges1.Count.ToString() + ", " + (ttc1 - ttc0).ToString());
            collisionPoint = GeoPoint.Origin;
            int tttc1 = System.Environment.TickCount;
            // System.Diagnostics.Trace.WriteLine("Kein Schnittpunkt: " + (tttc1 - tttc0).ToString());


            return false;
        }

        public bool GetResult(double precision, out GeoPoint collisionPoint)
        {
            GeoObjectList dummy;
            return GetResult(precision, false, out collisionPoint, out dummy);
        }

        public bool GetResult(double precision, bool checkAllFaces, out GeoPoint collisionPoint, out GeoObjectList collidingFaces, bool fullTest = false)
        {
            bool collisionDetected = false;
            collisionPoint = GeoPoint.Origin;
            collidingFaces = new GeoObjectList();

            // zwei QuadTrees, die die Flächen enthalten
            // (es wäre für die Distance-Methode günstig, sie würden auch die kanten und Eckpunkte enthalten, tun sie aber z.Z. nicht)
            of1 = new OctTree<Face>(s1.GetBoundingCube(), precision);
            overlappingFaces = new Set<Pair<Face, Face>>();
            foreach (Face fc in s1.Faces)
            {
                of1.AddObject(fc);
            }

#if DEBUG
            int intscounter = 0;
#endif
            foreach (Edge edge in s2.Edges)
            {
                if (edge.Curve3D == null) continue;
                BoundingCube curveExt = edge.Curve3D.GetExtent();
                Face[] close = of1.GetObjectsCloseTo(edge.Curve3D as IOctTreeInsertable);
                for (int i = 0; i < close.Length; ++i)
                {
                    if (!curveExt.Interferes(close[i].GetExtent(0.0))) continue; // schneller Ausschlusstest
                    GeoPoint[] ip;
                    GeoPoint2D[] uvOnFace;
                    double[] uOnCurve3D;
                    int tc0 = System.Environment.TickCount;
                    close[i].Intersect(edge, out ip, out uvOnFace, out uOnCurve3D);
#if DEBUG
                    ++intscounter;
#endif
                    if (ip != null && ip.Length > 0)
                    {
                        for (int j = 0; j < ip.Length; j++)
                        {
                            GeoVector v1 = edge.Curve3D.DirectionAt(uOnCurve3D[j]);
                            GeoVector v2 = close[i].Surface.GetNormal(uvOnFace[j]);
                            if (Math.Abs(v1 * v2) / (v1.Length * v2.Length) > 1e-4)
                            {   // tangentiale Schnitte gelten nicht
                                collisionPoint = ip[j];
                                collidingFaces.Add(close[i]);
                                collidingFaces.Add(edge.PrimaryFace);
                                collidingFaces.Add(edge.SecondaryFace);
                                //return true;
                                collisionDetected = true;
                                if (!checkAllFaces)
                                    return collisionDetected;

                            }
                        }
                    }
                }
            }

            of2 = new OctTree<Face>(s2.GetBoundingCube(), precision);
            foreach (Face fc in s2.Faces)
            {
                of2.AddObject(fc);
            }
            // umgekehrte Rollen
            foreach (Edge edge in s1.Edges)
            {
                if (edge.Curve3D == null) continue;
                BoundingCube curveExt = edge.Curve3D.GetExtent();
                Face[] close = of2.GetObjectsCloseTo(edge.Curve3D as IOctTreeInsertable);
                for (int i = 0; i < close.Length; ++i)
                {
                    if (!curveExt.Interferes(close[i].GetExtent(0.0))) continue;
                    GeoPoint[] ip;
                    GeoPoint2D[] uvOnFace;
                    double[] uOnCurve3D;
                    close[i].Intersect(edge, out ip, out uvOnFace, out uOnCurve3D);
#if DEBUG
                    ++intscounter;
#endif
                    if (ip != null && ip.Length > 0)
                    {
                        for (int j = 0; j < ip.Length; j++)
                        {
                            GeoVector v1 = edge.Curve3D.DirectionAt(uOnCurve3D[j]);
                            GeoVector v2 = close[i].Surface.GetNormal(uvOnFace[j]);
                            if (Math.Abs(v1 * v2) / (v1.Length * v2.Length) > 1e-4)
                            {
                                collisionPoint = ip[j];
                                collidingFaces.Add(close[i]);
                                collidingFaces.Add(edge.PrimaryFace);
                                collidingFaces.Add(edge.SecondaryFace);
                                //return true;
                                collisionDetected = true;
                                if (!checkAllFaces)
                                    return collisionDetected;
                            }
                        }
                    }
                }
            }

            if (fullTest)
            {
                foreach (Face fc in s2.Faces)
                {
                    Face[] close = of1.GetObjectsCloseTo(fc);
                    for (int i = 0; i < close.Length; i++)
                    {
                        ICurve[] icvs = close[i].Intersect(fc);
                        if (icvs.Length > 0)
                        {
                            if (!collisionDetected)
                            {
                                collisionPoint = icvs[0].StartPoint;
                                collisionDetected = true;
                                if (!checkAllFaces)
                                    return collisionDetected;
                            }
                            collidingFaces.Add(close[i]);
                            collidingFaces.Add(fc);
                        }
                    }
                }
                foreach (Face fc in s1.Faces)
                {
                    Face[] close = of2.GetObjectsCloseTo(fc);
                    for (int i = 0; i < close.Length; i++)
                    {
                        ICurve[] icvs = close[i].Intersect(fc);
                        if (icvs.Length > 0)
                        {
                            if (!collisionDetected)
                            {
                                collisionPoint = icvs[0].StartPoint;
                                collisionDetected = true;
                                if (!checkAllFaces)
                                    return collisionDetected;
                            }
                            collidingFaces.Add(close[i]);
                            collidingFaces.Add(fc);
                        }
                    }
                }

            }
            return collisionDetected;
            //         collisionPoint = GeoPoint.Origin;
            //return false;
        }

        //public bool GetResult(double precision, out GeoPoint collisionPoint)
        //{
        //    // zwei QuadTrees, die die Flächen enthalten
        //    // (es wäre für die Distance-Methode günstig, sie würden auch die kanten und Eckpunkte enthalten, tun sie aber z.Z. nicht)
        //    of1 = new OctTree<Face>(s1.GetBoundingCube(), precision);
        //    overlappingFaces = new Set<Pair<Face, Face>>();
        //    foreach (Face fc in s1.Faces)
        //    {
        //        of1.AddObject(fc);
        //    }

        //    foreach (Edge edge in s2.Edges)
        //    {
        //        if (edge.Curve3D == null) continue;
        //        BoundingCube curveExt = edge.Curve3D.GetExtent();
        //        Face[] close = of1.GetObjectsCloseTo(edge.Curve3D as IOctTreeInsertable);
        //        for (int i = 0; i < close.Length; ++i)
        //        {
        //            if (!curveExt.Interferes(close[i].GetExtent(0.0))) continue; // schneller Ausschlusstest
        //            GeoPoint[] ip;
        //            GeoPoint2D[] uvOnFace;
        //            double[] uOnCurve3D;
        //            int tc0 = System.Environment.TickCount;
        //            close[i].Intersect(edge, out ip, out uvOnFace, out uOnCurve3D);
        //            if (ip != null && ip.Length > 0)
        //            {
        //                for (int j = 0; j < ip.Length; j++)
        //                {
        //                    GeoVector v1 = edge.Curve3D.DirectionAt(uOnCurve3D[j]);
        //                    GeoVector v2 = close[i].Surface.GetNormal(uvOnFace[j]);
        //                    if (Math.Abs(v1 * v2) / (v1.Length * v2.Length) > 1e-4)
        //                    {   // tangentiale Schnitte gelten nicht
        //                        collisionPoint = ip[j];
        //                        return true;
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    of2 = new OctTree<Face>(s2.GetBoundingCube(), precision);
        //    foreach (Face fc in s2.Faces)
        //    {
        //        of2.AddObject(fc);
        //    }
        //    // umgekehrte Rollen
        //    foreach (Edge edge in s1.Edges)
        //    {
        //        if (edge.Curve3D == null) continue;
        //        BoundingCube curveExt = edge.Curve3D.GetExtent();
        //        Face[] close = of2.GetObjectsCloseTo(edge.Curve3D as IOctTreeInsertable);
        //        for (int i = 0; i < close.Length; ++i)
        //        {
        //            if (!curveExt.Interferes(close[i].GetExtent(0.0))) continue;
        //            GeoPoint[] ip;
        //            GeoPoint2D[] uvOnFace;
        //            double[] uOnCurve3D;
        //            close[i].Intersect(edge, out ip, out uvOnFace, out uOnCurve3D);
        //            if (ip != null && ip.Length > 0)
        //            {
        //                for (int j = 0; j < ip.Length; j++)
        //                {
        //                    GeoVector v1 = edge.Curve3D.DirectionAt(uOnCurve3D[j]);
        //                    GeoVector v2 = close[i].Surface.GetNormal(uvOnFace[j]);
        //                    if (Math.Abs(v1 * v2) / (v1.Length * v2.Length) > 1e-4)
        //                    {
        //                        collisionPoint = ip[j];
        //                        return true;
        //                    }
        //                }
        //            }
        //        }
        //    }


        //    collisionPoint = GeoPoint.Origin;
        //    return false;
        //}

        private double Distance(OctTree<Face> of1, GeoPoint fromHere)
        {   // Berechne den Abstand des Punktes zur Shell, die durch einen OctTree gegeben ist
            // der Abstand hat ein Vorzeichen: >0 außen, <0: innen
            double res = double.MaxValue;
            double radius = 0.0; // der "Radius" der Suchbox im OctTree
            Face[] fcs = null;
            while (res == double.MaxValue)
            {
                fcs = of1.GetObjectsFromBox(new BoundingCube(fromHere, radius));
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int i = 0; i < fcs.Length; i++)
                {
                    dc.Add(fcs[i]);
                }
#endif
                // Nimm alle Faces, die sehr nahe an dem Punkt liegen, kann leer sein, kann auch sein, dass
                // ein Face noch näher an dem Punkt ist, aber nicht in dieser Liste
                Set<Edge> edges = new Set<Edge>();  // sind leider nicht im OctTree
                Set<Vertex> vertices = new Set<Vertex>(); // und die auch nicht
                for (int i = 0; i < fcs.Length; i++)
                {
                    edges.AddMany(fcs[i].AllEdges);
                    vertices.AddMany(fcs[i].Vertices);
                    double d = fcs[i].Distance(fromHere);
                    if (Math.Abs(d) < Math.Abs(res))
                    {
                        res = d;
                    }
                }
                foreach (Edge edg in edges)
                {
                    if (edg.Curve3D != null)
                    {
                        double cpos = edg.Curve3D.PositionOf(fromHere);
                        if (cpos > 0.0 && cpos < 1.0) // vertex wird noch extra überprüft
                        {   // eine echte Senkrechte auf die Kurve
                            GeoPoint f = edg.Curve3D.PointAt(cpos);

                            double d = f | fromHere;
                            if (d < Math.Abs(res))
                            {
                                GeoPoint2D spos = edg.PrimaryFace.Surface.PositionOf(f);
                                GeoVector n1 = edg.PrimaryFace.Surface.GetNormal(spos);
                                spos = edg.SecondaryFace.Surface.PositionOf(f);
                                GeoVector n2 = edg.SecondaryFace.Surface.GetNormal(spos);
                                // Die Summe der Normalenvektoren gibt die Normal in dem Kantenpunkt
                                // Wenn dieser Kantenpunkt tatsächlich der nächstgelegene Punkt
                                // ist, dann ist diese Überlegung richtig
                                if ((n1.Normalized + n2.Normalized) * (fromHere - f) < 0.0) res = -d;
                                else res = d;
                            }
                        }
                    }
                }
                foreach (Vertex vtx in vertices)
                {
                    double d = vtx.Position | fromHere;
                    if (d < Math.Abs(res))
                    {
                        Face[] vfcs = vtx.Faces;
                        GeoVector n = GeoVector.NullVector;
                        for (int i = 0; i < vfcs.Length; i++)
                        {
                            GeoPoint2D spos = vtx.GetPositionOnFace(vfcs[i]);
                            n = n + vfcs[i].Surface.GetNormal(spos).Normalized;
                        }
                        if (n * (fromHere - vtx.Position) < 0.0) res = -d;
                        else res = d;
                    }
                }
                if (res == double.MaxValue)
                {
                    if (radius == 0.0) radius = of1.Extend.Size / 3.0 / 16.0; // ein 16tel der Größe
                    else radius *= 2;
                }
            }
            if (Math.Abs(res) > radius)
            {   // es ist möglich, dass es noch ein Face gibt, welches näher an dem Punkt ist
                // aber noch nicht berücksichtigt wurde
                Set<Face> fc1 = new Set<Face>(of1.GetObjectsFromBox(new BoundingCube(fromHere, Math.Abs(res))));
                fc1.RemoveMany(fcs); // das sind die faces, die noch nicht untersucht sind und evtl. 
                // näher liegen als das schon gefundene res
                // alle noch nicht untersuchten Faces checken:
                Set<Edge> edges = new Set<Edge>();  // sind leider nicht im OctTree
                Set<Vertex> vertices = new Set<Vertex>(); // und die auch nicht
                foreach (Face fc in fc1)
                {
                    edges.AddMany(fc.AllEdges);
                    vertices.AddMany(fc.Vertices);
                    double d = fc.Distance(fromHere);
                    if (Math.Abs(d) < Math.Abs(res))
                    {
                        res = d;
                    }
                }
                // schade, dieser Code ist doppelt, aber sonst schwer zu organisieren:
                foreach (Edge edg in edges)
                {
                    if (edg.Curve3D != null)
                    {
                        double cpos = edg.Curve3D.PositionOf(fromHere);
                        if (cpos > 0.0 && cpos < 1.0) // vertex wird noch extra überprüft
                        {   // eine echte Senkrechte auf die Kurve
                            GeoPoint f = edg.Curve3D.PointAt(cpos);

                            double d = f | fromHere;
                            if (d < Math.Abs(res))
                            {
                                GeoPoint2D spos = edg.PrimaryFace.Surface.PositionOf(f);
                                GeoVector n1 = edg.PrimaryFace.Surface.GetNormal(spos);
                                spos = edg.SecondaryFace.Surface.PositionOf(f);
                                GeoVector n2 = edg.SecondaryFace.Surface.GetNormal(spos);
                                // Die Summe der Normalenvektoren gibt die Normal in dem Kantenpunkt
                                // Wenn dieser Kantenpunkt tatsächlich der nächstgelegene Punkt
                                // ist, dann ist diese Überlegung richtig
                                if ((n1.Normalized + n2.Normalized) * (fromHere - f) < 0.0) res = -d;
                                else res = d;
                            }
                        }
                    }
                }
                foreach (Vertex vtx in vertices)
                {
                    double d = vtx.Position | fromHere;
                    if (d < Math.Abs(res))
                    {
                        Face[] vfcs = vtx.Faces;
                        GeoVector n = GeoVector.NullVector;
                        for (int i = 0; i < vfcs.Length; i++)
                        {
                            GeoPoint2D spos = vtx.GetPositionOnFace(vfcs[i]);
                            n = n + vfcs[i].Surface.GetNormal(spos).Normalized;
                        }
                        if (n * (fromHere - vtx.Position) < 0.0) res = -d;
                        else res = d;
                    }
                }
            }
            return res;
        }
    }

    internal class IntersectionVertex : IComparable<IntersectionVertex>
    {
        public Vertex v; // uv Werte sind gesetzt
                         // Edge/Face Schnitt
                         // eines der beiden Objekte auf shell1 das andere auf shell2
        public Edge edge;
        public Face face;
        public double uOnEdge; // u-Parameter auf dem Edge
        public bool edgeIsOn1; // wenn true: Kante ist von shell1, face von shell2, sonst umgekehrt
        public bool isOnFaceBorder; // wenn true: der Schnittpunkt ist auf dem Rand von face. Das ist von Bedeutung, wenn man wissen will ob die Verbindung von zwei solchen Punkten
                                    // sicher innerhalb des Faces liegt, oder noch extra getestet werden muss

        int IComparable<IntersectionVertex>.CompareTo(IntersectionVertex other)
        {
            return v.GetHashCode().CompareTo(other.v.GetHashCode());
        }
        // hier braucht es noch einen Marker für jede egde (wenn gesetzt), ob man in Richtung dieser egde in das Solid eintaucht, oder hearuskommt
        // das geht auch bei edge/edge!
    }
    class DoubleFaceKey : IComparable<DoubleFaceKey>
    {   // dient als key in einem Dictionary von nodes, in dem zwei Faces von verschiedenen Shells enthalten sind
        public Face face1, face2;
        public DoubleFaceKey(Face f1, Face f2)
        {   // es ist wichtig, dass f1 und f2 nicht vertauscht werden (das waren sie früher)
            face1 = f1;
            face2 = f2;
            // eins davon kann auch null sein
        }
        public override int GetHashCode()
        {
            if (face1 == null) return face2.GetHashCode();
            else if (face2 == null) return face1.GetHashCode();
            else return face1.GetHashCode() + face2.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            DoubleFaceKey other = obj as DoubleFaceKey;
            if (other == null) return false;
            int hc11, hc12, hc21, hc22;
            if (face1 == null) hc11 = face2.GetHashCode();
            else if (face2 == null) hc11 = face1.GetHashCode();
            else hc11 = Math.Min(face1.GetHashCode(), face2.GetHashCode());
            if (face1 == null) hc12 = face2.GetHashCode();
            else if (face2 == null) hc12 = face1.GetHashCode();
            else hc12 = Math.Max(face1.GetHashCode(), face2.GetHashCode());

            if (other.face1 == null) hc21 = other.face2.GetHashCode();
            else if (other.face2 == null) hc21 = other.face1.GetHashCode();
            else hc21 = Math.Min(other.face1.GetHashCode(), other.face2.GetHashCode());
            if (other.face1 == null) hc22 = other.face2.GetHashCode();
            else if (other.face2 == null) hc22 = other.face1.GetHashCode();
            else hc22 = Math.Max(other.face1.GetHashCode(), other.face2.GetHashCode());

            return hc11 == hc21 && hc12 == hc22;
        }
        #region IComparable<DoubleFaceKey> Members
        int IComparable<DoubleFaceKey>.CompareTo(DoubleFaceKey other)
        {
            int hc11, hc12, hc21, hc22;
            if (face1 == null) hc11 = face2.GetHashCode();
            else if (face2 == null) hc11 = face1.GetHashCode();
            else hc11 = Math.Min(face1.GetHashCode(), face2.GetHashCode());
            if (face1 == null) hc12 = face2.GetHashCode();
            else if (face2 == null) hc12 = face1.GetHashCode();
            else hc12 = Math.Max(face1.GetHashCode(), face2.GetHashCode());

            if (other.face1 == null) hc21 = other.face2.GetHashCode();
            else if (other.face2 == null) hc21 = other.face1.GetHashCode();
            else hc21 = Math.Min(other.face1.GetHashCode(), other.face2.GetHashCode());
            if (other.face1 == null) hc22 = other.face2.GetHashCode();
            else if (other.face2 == null) hc22 = other.face1.GetHashCode();
            else hc22 = Math.Max(other.face1.GetHashCode(), other.face2.GetHashCode());

            int res = hc11.CompareTo(hc21);
            if (res == 0) res = hc12.CompareTo(hc22);
            return res;
        }
        #endregion
#if DEBUG
        GeoObjectList Debug
        {
            get
            {
                return new GeoObjectList(face1, face2);
            }
        }
#endif
    }
    class EdgeFaceKey : IComparable<EdgeFaceKey>
    {
        public Edge edge;
        public Face face;
        public EdgeFaceKey(Edge edge, Face face)
        {
            this.face = face;
            this.edge = edge;
        }
        public override bool Equals(object obj)
        {
            EdgeFaceKey other = obj as EdgeFaceKey;
            if (other == null) return false;
            return other.edge.GetHashCode() == this.edge.GetHashCode() && other.face.GetHashCode() == this.face.GetHashCode();
        }
        public override int GetHashCode()
        {
            return edge.GetHashCode() + face.GetHashCode();
        }
        #region IComparable<EdgeFaceKey> Members
        int IComparable<EdgeFaceKey>.CompareTo(EdgeFaceKey other)
        {
            int res = edge.GetHashCode().CompareTo(other.edge.GetHashCode());
            if (res == 0) res = face.GetHashCode().CompareTo(other.face.GetHashCode());
            return res;
        }
        #endregion
    }
    internal class DoubleVertexKey : IComparable<DoubleVertexKey>
    {
        public Vertex vertex1, vertex2;
        public DoubleVertexKey(Vertex f1, Vertex f2)
        {
            if (f1.GetHashCode() < f2.GetHashCode())
            {
                vertex1 = f1;
                vertex2 = f2;
            }
            else
            {
                vertex1 = f2;
                vertex2 = f1;
            }
        }
        public override int GetHashCode()
        {
            return vertex1.GetHashCode() + vertex2.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            DoubleVertexKey other = obj as DoubleVertexKey;
            if (other == null) return false;
            return this.vertex1.GetHashCode() == other.vertex1.GetHashCode() && this.vertex2.GetHashCode() == other.vertex2.GetHashCode();
        }
        #region IComparable<DoubleVertexKey> Members
        int IComparable<DoubleVertexKey>.CompareTo(DoubleVertexKey other)
        {
            int res = vertex1.GetHashCode().CompareTo(other.vertex1.GetHashCode());
            if (res == 0) res = vertex2.GetHashCode().CompareTo(other.vertex2.GetHashCode());
            return res;
        }
        #endregion
    }
    internal class OrderedDoubleVertexKey : IComparable<OrderedDoubleVertexKey>
    {
        public Vertex vertex1, vertex2;
        public OrderedDoubleVertexKey(Vertex f1, Vertex f2)
        {
            vertex1 = f1;
            vertex2 = f2;
        }
        public override int GetHashCode()
        {
            return vertex1.GetHashCode() + vertex2.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            OrderedDoubleVertexKey other = obj as OrderedDoubleVertexKey;
            if (other == null) return false;
            return this.vertex1.GetHashCode() == other.vertex1.GetHashCode() && this.vertex2.GetHashCode() == other.vertex2.GetHashCode();
        }
        #region IComparable<OrderedDoubleVertexKey> Members
        int IComparable<OrderedDoubleVertexKey>.CompareTo(OrderedDoubleVertexKey other)
        {
            int res = vertex1.GetHashCode().CompareTo(other.vertex1.GetHashCode());
            if (res == 0) res = vertex2.GetHashCode().CompareTo(other.vertex2.GetHashCode());
            return res;
        }
        #endregion
    }
    class VertexOcttree : OctTree<Vertex>
    {
        public VertexOcttree(BoundingCube ext, double precision) : base(ext, precision)
        {
            this.precision = precision;
        }
        protected override bool SplitNode(Node<Vertex> node, Vertex objectToAdd)
        {
            // vertices, die nahe beieinander liegen sollen als ein Vertex betrachtet werden, und bleiben so in einer Liste
            // alles andere wird getrennt
            if (node.list.Count > 0)
            {
                if ((node.list[0].Position | objectToAdd.Position) < precision) return false;
                return true;
            }
            return false; // kommt nicht vor, ohne Elemente, oder
        }
        internal void combineAll(Set<IntersectionVertex> intersectionVertices)
        {   // jede Liste, die mehr als einen Vertex enthält, hat nur solche mit geometrisch gleichen Punkten (siehe SplitNode)
            // deshalb werden alle weiteren in den ersten eingemischt
            Dictionary<Vertex, IntersectionVertex> backreference = new Dictionary<Vertex, IntersectionVertex>();
            foreach (IntersectionVertex iv in intersectionVertices)
            {
                backreference[iv.v] = iv; // könnte man auch an Vertex dranhängen mit einer dort einzufügendem Member
            }
            foreach (Node<Vertex> node in Leaves)
            {
                if (node.list.Count > 1)
                {
                    for (int i = 1; i < node.list.Count; i++)
                    {
                        node.list[0].MergeWith(node.list[i]); // damit ist node.list[i] völlig abgekoppelt
                        IntersectionVertex iv;
                        if (backreference.TryGetValue(node.list[i], out iv)) iv.v = node.list[0]; // auch noch ersetzen
                    }
                    node.list.RemoveRange(1, node.list.Count - 1); // Liste wird leer (bis auf den 1. wird vermutlich nicht gebraucht)
                }
            }
        }
    }
    class EdgeComparerByVertexAndFace : IEqualityComparer<Edge>
    {
        public bool Equals(Edge x, Edge y)
        {
            if ((x.Vertex1.GetHashCode() != y.Vertex1.GetHashCode()) && (x.Vertex1.GetHashCode() != y.Vertex2.GetHashCode())) return false;
            if ((x.Vertex2.GetHashCode() != y.Vertex1.GetHashCode()) && (x.Vertex2.GetHashCode() != y.Vertex2.GetHashCode())) return false;
            if ((x.PrimaryFace.GetHashCode() != y.PrimaryFace.GetHashCode()) && (x.PrimaryFace.GetHashCode() != y.SecondaryFace.GetHashCode())) return false;
            if ((x.SecondaryFace.GetHashCode() != y.PrimaryFace.GetHashCode()) && (x.SecondaryFace.GetHashCode() != y.SecondaryFace.GetHashCode())) return false;
            return true;
        }
        private int rotateLeft(int x, int n) { return (x << n) | (x >> (32 - n)); }
        public int GetHashCode(Edge obj)
        {
            return obj.Vertex1.GetHashCode() | rotateLeft(obj.Vertex2.GetHashCode(), 8) | rotateLeft(obj.PrimaryFace.GetHashCode(), 16) | rotateLeft(obj.SecondaryFace.GetHashCode(), 24);
        }
    }
    class Cycle : List<ICurve2D>
    {
        public bool isCounterClock; // muss auf oberen Level immer true sein, bei Löchern false
        private double area = double.MaxValue;
        public List<Cycle> holes; // es darf nur einen Level darunter geben, d.h. holes dürfen selbst keine Löcher haben
        public bool isInside(GeoPoint2D toTest)
        {
            int num = Border.GetBeamIntersectionCount(this, toTest);
            if (holes != null)
            {
                for (int i = 0; i < holes.Count; i++)
                {
                    if (!holes[i].isInside(toTest)) return false; // holes.isInside heißt ja nicht im Loch, sondern in der umgebenden Fläche
                }
            }
            return isCounterClock ^ ((num & 0x01) == 0); // true: gegenUhrzeigersinn und Anzahl ungerade -oder- Uhrzeigersinn und Anzahl gerade
        }
        internal void calcOrientation()
        {
            isCounterClock = Border.CounterClockwise(this);
        }
        internal double Area
        {
            get
            {
                if (area == double.MaxValue) area = Border.SignedArea(this);
                return area;
            }
        }
        public Set<Vertex> OutlineVertices
        {
            get
            {
                Set<Vertex> res = new Set<Vertex>();
                for (int i = 0; i < Count; i++)
                {
                    Edge edg = (this[i].UserData.GetData("edge") as Edge);
                    res.Add(edg.Vertex1);
                    res.Add(edg.Vertex2);
                }
                return res;
            }
        }
#if DEBUG
        public DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                for (int i = 0; i < Count; i++)
                {
                    res.Add(this[i], System.Drawing.Color.Red, (this[i].UserData.GetData("edge") as Edge).GetHashCode());
                }
                if (holes != null)
                {
                    for (int j = 0; j < holes.Count; j++)
                    {
                        for (int i = 0; i < holes[j].Count; i++)
                        {
                            res.Add(holes[j][i], System.Drawing.Color.Blue, (holes[j][i].UserData.GetData("edge") as Edge).GetHashCode());
                        }
                    }
                }
                return res;
            }
        }
#endif
        internal void AddHole(Cycle hole, Face thisFace, Face otherFace)
        {
            ModOp2D otherToThis;
            if (otherFace.Surface.SameGeometry(otherFace.Area.GetExtent(), thisFace.Surface, thisFace.Area.GetExtent(), Precision.eps, out otherToThis))
            {
                for (int i = 0; i < hole.Count; i++)
                {
                    ICurve2D tmp = hole[i];
                    Edge edg = hole[i].UserData.GetData("edge") as Edge;
                    edg.ReplaceFace(otherFace, thisFace, otherToThis);
                    hole[i] = edg.Curve2D(thisFace);
                    hole[i].UserData.CloneFrom(tmp.UserData); // nicht sicher, was davon gebraucht wird
                }
                if (holes == null) holes = new List<Cycle>();
                holes.Add(hole);
            }
            else
            {
                throw new ApplicationException("internal error in BRepOperation (AddHole)");
            }
        }
    }
    public static class Extension
    {
        public static bool ContainsSameFace(this Dictionary<Face, Set<Face>> dict, Face face, double precision)
        {
            if (dict.TryGetValue(face, out Set<Face> commonWith))
            {
                Set<Edge> edges = face.AllEdgesSet;
                Set<Vertex> vertices = new Set<Vertex>();
                foreach (Edge edg in edges)
                {
                    vertices.Add(edg.Vertex1);
                    vertices.Add(edg.Vertex2);
                }
                foreach (Face fce in commonWith)
                {
                    if (BRepOperation.IsSameFace(edges, vertices, fce, precision))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

    }
    public class BRepOperation : OctTree<BRepItem>
    {
        private Shell shell1;
        private Shell shell2;
        private PlaneSurface splittingOnplane; // when splitting a Shell with a plane, this is the surface
#if DEBUG
        Edge debugEdge;
#endif
        Dictionary<DoubleFaceKey, ModOp2D> overlappingFaces; // Faces von verschiedenen Shells, die auf der gleichen Surface beruhen und sich überlappen
        Dictionary<DoubleFaceKey, Set<Edge>> overlappingEdges; // relevante Kanten auf den overlappingFaces
        Dictionary<DoubleFaceKey, ModOp2D> oppositeFaces; // Faces von verschiedenen Shells, die auf der gleichen Surface beruhen und sich überlappen aber verschieden orientiert sind
        Dictionary<Face, Set<Face>> faceToOverlappingFaces; // schneller Zugriff von einem face zu den überlappenden aus der anderen shell
        Set<Face> cancelledfaces; // Faces, die sich gegenseitig auslöschen, also identisch sind nur andersrum orientiert
        Dictionary<Face, Set<Edge>> faceToIntersectionEdges; // faces of both shells with their intersection edges
        Dictionary<Face, Set<Face>> faceToCommonFaces; // faces which have overlapping common parts on them
        Dictionary<Edge, List<Vertex>> edgesToSplit;
        Set<Edge> edgesNotToUse; // these edges are identical to intersection edges, but are original edges. They must not be used when collecting faces
        Set<IntersectionVertex> intersectionVertices; // die Mange aller gefundenen Schnittpunkte (mit Rückverweis zu Kante und face)
        Dictionary<DoubleFaceKey, List<IntersectionVertex>> facesToIntersectionVertices; // Faces mit den zugehörigen Schnittpunkt
        Dictionary<Edge, Edge> intsEdgeToEdgeShell1; // diese IntersectionEdge ist identisch mit dieser kante auf Shell1
        Dictionary<Edge, Edge> intsEdgeToEdgeShell2;
        Dictionary<Edge, Edge> intsEdgeToIntsEdge; // zwei intersectionEdges sind identisch (beide Zuordnungen finden sich hier)

        /// <summary>
        /// Erzeuge die Schnitte zwischen Edges und Faces von verschiedenen shells.
        /// edgesToSplit, intersectionVertices und facesToIntersectionVertices werden gesetzt
        /// </summary>
        private void createEdgeFaceIntersections()
        {
            edgesToSplit = new Dictionary<Edge, List<Vertex>>();
            intersectionVertices = new Set<IntersectionVertex>();
            facesToIntersectionVertices = new Dictionary<DoubleFaceKey, List<IntersectionVertex>>();
            // zuerst die potentiellen Kandidaten sammeln aus den Blättern des OctTrees
            Dictionary<EdgeFaceKey, List<Node<BRepItem>>> edgesToFaces = new Dictionary<EdgeFaceKey, List<OctTree<BRepItem>.Node<BRepItem>>>();
            Set<Face> faces = new Set<Face>();
            foreach (Node<BRepItem> node in Leaves)
            {
                foreach (BRepItem first in node.list)
                {
                    if (first.Type == BRepItem.ItemType.Edge)
                    {
                        Edge edge = first.edge;
                        IGeoObjectOwner shell = edge.PrimaryFace.Owner;
                        foreach (BRepItem second in node.list)
                        {
                            if (second.Type == BRepItem.ItemType.Face)
                            {
                                if (second.face.Owner != shell)
                                {   // keine Schnitte von Kanten, die ganz im Face liegen
                                    Set<Face> overlap;
                                    if (faceToOverlappingFaces.TryGetValue(second.face, out overlap))
                                    {
                                        if (overlap.Contains(edge.PrimaryFace) || overlap.Contains(edge.SecondaryFace)) continue;
                                    }
                                    List<Node<BRepItem>> addInto;
                                    EdgeFaceKey efk = new EdgeFaceKey(edge, second.face);
                                    if (!edgesToFaces.TryGetValue(efk, out addInto))
                                    {
                                        addInto = new List<OctTree<BRepItem>.Node<BRepItem>>();
                                        edgesToFaces[efk] = addInto;
                                    }
                                    addInto.Add(node);
                                    faces.Add(second.face);
                                }
                            }
                        }
                    }
                }
            }
            foreach (Face fce in faces)
            {   // es ist ein Fahler, dass man das hier machen muss, aber ich habe den noch nicht gefunden
                fce.ForceAreaRecalc();
            }
            // edgesToFaces enthält jetzt alle schnittverdächtigen Paare
            // und dazu noch die nodes, wenn man Anfangswerte suchen würde...
#if DEBUG
            DebuggerContainer dcedges = new DebuggerContainer();
            DebuggerContainer dcfaces = new DebuggerContainer();
            int dbgcnt = 0;
            foreach (EdgeFaceKey ef in edgesToFaces.Keys)
            {
                dcedges.Add(ef.edge.Curve3D as IGeoObject, dbgcnt);
                dcfaces.Add(ef.face, dbgcnt);
            }
#endif
            foreach (EdgeFaceKey ef in edgesToFaces.Keys)
            {
                GeoPoint[] ip;
                GeoPoint2D[] uvOnFace;
                double[] uOnCurve3D;
                Border.Position[] position;
                double prec = precision / ef.edge.Curve3D.Length;
                ef.face.IntersectAndPosition(ef.edge, out ip, out uvOnFace, out uOnCurve3D, out position, precision);
                for (int i = 0; i < ip.Length; ++i)
                {
                    double sp = ef.edge.Curve3D.DirectionAt(uOnCurve3D[i]) * ef.face.Surface.GetNormal(uvOnFace[i]).Normalized;
                }
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (uOnCurve3D[i] < -prec || uOnCurve3D[i] > 1.0 + prec || position[i] == Border.Position.Outside)
                    {
                        // Die Endpunkte sollen mit erzeugt werden, damit daraus später Schnittkanten entstehen können
                        // Beim Aufteilen der kanten dürfen die Endpunkte allerdings nicht mit verwendet werden
                        continue;
                    }
                    Vertex v = new Vertex(ip[i]);
                    v.AddPositionOnFace(ef.face, uvOnFace[i]);
                    IntersectionVertex iv = new IntersectionVertex();
                    iv.v = v;
                    iv.edge = ef.edge;
                    iv.face = ef.face;
                    iv.uOnEdge = uOnCurve3D[i];
                    iv.isOnFaceBorder = (position[i] == Border.Position.OnCurve); // das ist für später: Verbindung zweier Schnittpunkte, die beide auf dem Rand liegen, ist nicht sicher innerhalb
                    if (ef.edge.PrimaryFace.Owner == shell1)
                    {
                        iv.edgeIsOn1 = true;
                        AddFacesToIntersectionVertices(ef.edge.PrimaryFace, ef.face, iv);
                        AddFacesToIntersectionVertices(ef.edge.SecondaryFace, ef.face, iv);
                    }
                    else
                    {
                        iv.edgeIsOn1 = false;
                        AddFacesToIntersectionVertices(ef.face, ef.edge.PrimaryFace, iv);
                        AddFacesToIntersectionVertices(ef.face, ef.edge.SecondaryFace, iv);
                    }
                    intersectionVertices.Add(iv);
                    if (!edgesToSplit.ContainsKey(ef.edge)) edgesToSplit[ef.edge] = new List<Vertex>();
                    edgesToSplit[ef.edge].Add(v);
                    if (position[i] == Border.Position.OnCurve)
                    {
                        List<Edge> touchedEdges = ef.face.FindEdges(uvOnFace[i]);
                        for (int j = 0; j < touchedEdges.Count; j++)
                        {
                            double cprec = prec / touchedEdges[j].Curve3D.Length; // darf natürlich nicht 0 sein!
                            double pos = touchedEdges[j].Curve3D.PositionOf(v.Position);
                            if (pos > cprec && pos < 1 - cprec)
                            {
                                if (!edgesToSplit.ContainsKey(touchedEdges[j])) edgesToSplit[touchedEdges[j]] = new List<Vertex>();
                                edgesToSplit[touchedEdges[j]].Add(v);
                            }
                        }
                    }
                    if (operation == Operation.testonly) return; // ein Schnittpunkt reicht hier
                }
            }
        }
        /// <summary>
        /// Combine all vertices of both shells and the intersection vertices
        /// </summary>
        private void combineVertices()
        {
            VertexOcttree vo = new VertexOcttree(this.Extend, this.precision);
            Vertex[] vv = shell1.Vertices;
            for (int i = 0; i < vv.Length; i++)
            {
                vo.AddObject(vv[i]);
            }
            vv = shell2.Vertices;
            for (int i = 0; i < vv.Length; i++)
            {
                Vertex[] close = vo.GetObjectsCloseTo(vv[i]);
                bool found = false;
                for (int j = 0; j < close.Length; j++)
                {
                    if ((close[j].Position | vv[i].Position) < precision)
                    {
                        close[j].MergeWith(vv[i]);
                        found = true;
                        break;
                    }
                }
                if (!found) vo.AddObject(vv[i]);
            }
            // eigentlich sind die intersectionVertices -nach dem Splitten der Kanten- shcon alle in den shells vorhanden, so dachte ich zuerst, aber:
            // Schnittpunkte an den Enden der Kanten werden wohl nicht verwendet und kommen hier noch dazu
            foreach (IntersectionVertex iv in intersectionVertices)
            {
                Vertex[] close = vo.GetObjectsCloseTo(iv.v);
                bool found = false;
                for (int j = 0; j < close.Length; j++)
                {
                    if ((close[j].Position | iv.v.Position) < precision)
                    {
                        close[j].MergeWith(iv.v);
                        iv.v = close[j];
                        found = true;
                        break;
                    }
                }
                if (!found) vo.AddObject(iv.v); // die sind alle verschieden
            }
            // vo.combineAll(intersectionVertices); // hier wird zusammengefasst
        }
        private void removeIdenticalOppositeFaces()
        {
            cancelledfaces = new Set<GeoObject.Face>();
            foreach (DoubleFaceKey dfk in oppositeFaces.Keys)
            {
                Set<Vertex> v1 = new Set<Vertex>(dfk.face1.Vertices);
                Set<Vertex> v2 = new Set<Vertex>(dfk.face2.Vertices);
                if (v1.IsEqualTo(v2))
                {
                    // there could be non identical faces with the same set of vertices. We should test this here!
                    cancelledfaces.Add(dfk.face1);
                    cancelledfaces.Add(dfk.face2);
                }
            }
        }
        private void AddFacesToIntersectionVertices(Face f1, Face f2, IntersectionVertex iv)
        {
            List<IntersectionVertex> list;
            if (!facesToIntersectionVertices.TryGetValue(new DoubleFaceKey(f1, f2), out list))
            {
                list = new List<IntersectionVertex>();
                facesToIntersectionVertices[new DoubleFaceKey(f1, f2)] = list;
            }
            list.Add(iv);
        }

        public enum Operation { union, intersection, difference, testonly }
        Operation operation;
        /// <summary>
        /// Prepare a brep operation for splitting a (closed) shell with a plane. Or for returning the compound shapes on the specified plane.
        /// Here we assume that "toSplit" is properly oriented and han no periodic faces (no seams)
        /// </summary>
        /// <param name="toSplit">the shell to split (must be closed)</param>
        /// <param name="splitBy">the plane to split by</param>
        public BRepOperation(Shell toSplit, Plane splitBy)
        {
            shell1 = toSplit.Clone() as Shell;   // clone the shell because its faces will be modified
            shell1.AssertOutwardOrientation();
            BoundingCube ext = shell1.GetExtent(0.0);
            Face fcpl = Face.MakeFace(new PlaneSurface(splitBy), new BoundingRect(GeoPoint2D.Origin, 2 * ext.DiagonalLength, 2 * ext.DiagonalLength));
            splittingOnplane = fcpl.Surface as PlaneSurface; // remember the plane by which we split to return a proper SplitResult
            Face fcpl1 = fcpl.Clone() as Face;
            fcpl1.MakeInverseOrientation();
            // shell2 = Shell.MakeShell(new Face[] { fcpl, fcpl1 }, true); // open shell, but the size of the face exceedes the shell to split
            shell2 = Shell.MakeShell(new Face[] { fcpl }, false); // open shell, but the size of the face exceedes the shell to split
            operation = Operation.difference;
            prepare();
        }

        public BRepOperation(Shell s1, Shell s2, Operation operation)
        {
            // im Konstruktor werden die Schnitte zwischen den shells berechnet. Mit Result, wird dann das Ergebnis geholt.
            // Da Result alles aufmischt, kann man es nicht zweimal (z.B. mit verschiedenen Operationen) verwenden. Insofern ist die Trennung
            // von Konstruktor und Result willkürlich. Besser sollte man statische Methoden machen Union, Intersection, Difference und alles andere private.
#if DEBUG
            //foreach (Face fce in s1.Faces)
            //{
            //    if (fce.Surface is ConicalSurface)
            //    {
            //        foreach (Edge edg in fce.AllEdgesIterated())
            //        {
            //            ICurve2D cv = fce.Surface.GetProjectedCurve(edg.Curve3D, 0.0);
            //            if (!edg.Forward(fce)) cv.Reverse();
            //            if (edg.PrimaryFace==fce)
            //            {
            //                edg.PrimaryCurve2D = cv;
            //            }
            //            else
            //            {
            //                edg.SecondaryCurve2D = cv;
            //            }
            //            ICurve cv3d = fce.Surface.Make3dCurve(cv);
            //        }
            //        Face.CheckOutlineDirection(fce, fce.OutlineEdges, Math.PI * 2, 0, null);
            //        for (int i = 0; i < fce.HoleCount; i++)
            //        {
            //            Face.CheckOutlineDirection(fce, fce.HoleEdges(i), Math.PI * 2, 0, null);
            //        }
            //        fce.ForceAreaRecal();
            //    }
            //}
            foreach (Edge dbgedg in s1.Edges)
            {
                if (dbgedg.Curve3D is InterpolatedDualSurfaceCurve)
                    (dbgedg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
            }
            foreach (Edge dbgedg in s2.Edges)
            {
                if (dbgedg.Curve3D is InterpolatedDualSurfaceCurve)
                    (dbgedg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
            }
            System.Diagnostics.Debug.Assert(s1.CheckConsistency());
            System.Diagnostics.Debug.Assert(s2.CheckConsistency());
#endif
            s1.RecalcVertices();
            s2.RecalcVertices();
#if DEBUG
            System.Diagnostics.Debug.Assert(s1.CheckConsistency());
            System.Diagnostics.Debug.Assert(s2.CheckConsistency());
#endif
            s1.SplitPeriodicFaces();
            s2.SplitPeriodicFaces();
            s1.AssertOutwardOrientation();
            s2.AssertOutwardOrientation();
            this.operation = operation;
#if DEBUG
            System.Diagnostics.Debug.Assert(s1.CheckConsistency());
            System.Diagnostics.Debug.Assert(s2.CheckConsistency());
            foreach (Edge dbgedg in s1.Edges)
            {
                if (dbgedg.Curve3D is InterpolatedDualSurfaceCurve)
                    (dbgedg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
            }
            foreach (Edge dbgedg in s2.Edges)
            {
                if (dbgedg.Curve3D is InterpolatedDualSurfaceCurve)
                    (dbgedg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
            }
#endif
            shell1 = s1.Clone() as Shell;   // hier wird gekloned, weil die Faces im Verlauf geändert werden und das Original
            shell2 = s2.Clone() as Shell;   // unverändert bleiben soll. ZUm Debuggen kann man das Klonen weglassen
            if (shell1.HasOpenEdgesEceptPoles()) shell1.TryConnectOpenEdges();
            if (shell2.HasOpenEdgesEceptPoles()) shell2.TryConnectOpenEdges();
            shell1.RecalcVertices();
            shell2.RecalcVertices();
            shell1.CombineConnectedFaces();
            shell2.CombineConnectedFaces();
#if DEBUG
            foreach (Edge edg in shell1.Edges)
            {
                edg.CheckConsistency();
            }
            foreach (Edge edg in shell2.Edges)
            {
                edg.CheckConsistency();
            }
#endif
#if DEBUG
            System.Diagnostics.Debug.Assert(shell1.CheckConsistency());
            System.Diagnostics.Debug.Assert(shell2.CheckConsistency());
            foreach (Edge dbgedg in s1.Edges)
            {
                if (dbgedg.Curve3D is InterpolatedDualSurfaceCurve)
                    (dbgedg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
            }
            foreach (Edge dbgedg in s2.Edges)
            {
                if (dbgedg.Curve3D is InterpolatedDualSurfaceCurve)
                    (dbgedg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
            }
            DebuggerContainer dcfcs = new CADability.DebuggerContainer();
            foreach (Face fce in shell1.Faces)
            {
                dcfcs.Add(fce.Clone(), fce.GetHashCode()); // die Faces werden kaputt gemacht, deshalb hier clones merken
                double ll = fce.GetExtent(0.0).Size * 0.01;
                ColorDef cd = new ColorDef("debug", System.Drawing.Color.Blue);
                SimpleShape ss = fce.Area;
                GeoPoint2D c = ss.GetExtent().GetCenter();
                GeoPoint pc = fce.Surface.PointAt(c);
                GeoVector nc = fce.Surface.GetNormal(c);
                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
                l.ColorDef = cd;
                dcfcs.Add(l);
            }
            foreach (Face fce in shell2.Faces)
            {
                dcfcs.Add(fce.Clone(), fce.GetHashCode()); // die Faces werden kaputt gemacht, deshalb hier clones merken
                double ll = fce.GetExtent(0.0).Size * 0.01;
                ColorDef cd = new ColorDef("debug", System.Drawing.Color.Brown);
                SimpleShape ss = fce.Area;
                GeoPoint2D c = ss.GetExtent().GetCenter();
                GeoPoint pc = fce.Surface.PointAt(c);
                GeoVector nc = fce.Surface.GetNormal(c);
                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
                l.ColorDef = cd;
                dcfcs.Add(l);
            }
#endif
            Vertex[] dumy = shell1.Vertices; // nur damits berechnet wird
            dumy = shell2.Vertices;
            if (operation == Operation.union)
            {
                shell1.ReverseOrientation();
                shell2.ReverseOrientation();
            }
            else if (operation == Operation.difference)
            {   // es ist ja shell1 - shell2, also Vereinigung mit dem inversen von shell2
                shell2.ReverseOrientation();
            }
#if DEBUG
            DebuggerContainer dc1 = new DebuggerContainer();
            foreach (Edge edg in shell1.Edges)
            {
                if (edg.Curve3D != null) dc1.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
                edg.CheckConsistency();
            }
            DebuggerContainer dc2 = new DebuggerContainer();
            foreach (Edge edg in shell2.Edges)
            {
                if (edg.Curve3D != null) dc2.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
                edg.CheckConsistency();
            }
#endif
            BoundingCube ext1 = shell1.GetExtent(0.0);
            BoundingCube ext2 = shell2.GetExtent(0.0);
            BoundingCube ext = ext1;
            ext.MinMax(ext2);
            // in rare cases the extension isn't a good choice, faces shouldn't exactely reside on the sides of the small cubes of the octtree
            // so we modify the extension a little, to make this case extremely unlikely. The best solution would be to check, whether a vertex
            // falls exactely on the side of a octtree-cube, then throw an exception and try with a different octtree location
            double extsize = ext.Size;
            ext.Expand(extsize * 1e-3);
            ext = ext.Modify(new GeoVector(extsize * 1e-4, extsize * 1e-4, extsize * 1e-4));
            Initialize(ext, extsize * 1e-6); // der OctTree
            // put all edges and faces into the octtree
            foreach (Edge edg in shell1.Edges)
            {
                base.AddObject(new BRepItem(this, edg));
#if DEBUG
                if (edg.Curve3D is InterpolatedDualSurfaceCurve) (edg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
#endif
            }
            foreach (Edge edg in shell2.Edges)
            {
                base.AddObject(new BRepItem(this, edg));
#if DEBUG
                if (edg.Curve3D is InterpolatedDualSurfaceCurve) (edg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
#endif
            }
            foreach (Face fc in shell1.Faces)
            {
                base.AddObject(new BRepItem(this, fc));
            }
            foreach (Face fc in shell2.Faces)
            {
                base.AddObject(new BRepItem(this, fc));
            }

            //GeoObjectList dbgle = DebugCubesByObject(shell2.Edges[27].Curve3D as IOctTreeInsertable);
            //GeoObjectList dbglf = DebugCubesByObject(shell1.Faces[27] as IOctTreeInsertable);
            findOverlappingFaces(); // setzt overlappingFaces, also Faces von verschiedenen shells, die sich teilweise überlappen oder identisch sind
            createEdgeFaceIntersections(); // Schnittpunkte von Edges mit Faces von verschiedenen shells
#if DEBUG
            DebuggerContainer dc3 = new DebuggerContainer();
            foreach (Edge edge in edgesToSplit.Keys)
            {
                dc3.Add(edge.Curve3D as IGeoObject, edge.GetHashCode());
            }
            foreach (IntersectionVertex iv in intersectionVertices)
            {
                Point pnt = Point.Construct();
                pnt.Location = iv.v.Position;
                pnt.Symbol = PointSymbol.Cross;
                dc3.Add(pnt, iv.v.GetHashCode());
            }
#endif
            if (operation != Operation.testonly)
            {   // Für testonly genügen die Kantenschnitte (fast)
                splitEdges(); // mit den gefundenen Schnittpunkten werden die Edges jetzt gesplittet
                combineVertices(); // alle geometrisch identischen Vertices werden zusammengefasst
                removeIdenticalOppositeFaces(); // Paare von entgegengesetzt orientierten Faces mit identischer Fläche werden entfernt
                createNewEdges(); // faceToIntersectionEdges wird gesetzt, also für jedes Face eine Liste der neuen Schnittkanten
                // createInnerFaceIntersections(); // Schnittpunkte zweier Faces, deren Kanten sich aber nicht schneiden, finden
                // createInnerFaceIntersections erstmal weglassen, macht noch zu viele probleme (Futter5.cdb)
                combineEdges(); // hier werden intsEdgeToEdgeShell1, intsEdgeToEdgeShell2 und intsEdgeToIntsEdge gesetzt, die aber z.Z. noch nicht verwendet werden

            }
#if DEBUG
            foreach (KeyValuePair<Face, Set<Edge>> item in faceToIntersectionEdges)
            {
                foreach (Edge edg in item.Value)
                {
                    dc3.Add(edg.Curve3D as IGeoObject, item.Key.GetHashCode());
                }
            }
            DebuggerContainer dc4 = new DebuggerContainer();
            Set<Vertex> dbgv = new Set<Vertex>();
            dbgv.AddMany(shell1.Vertices);
            dbgv.AddMany(shell2.Vertices); // kommt leider teilweise aus dem veralteten cache
            foreach (IntersectionVertex iv in intersectionVertices)
            {
                dbgv.Add(iv.v); // sollte ja schon drin sein!
            }
            foreach (Vertex v in dbgv)
            {
                Point pnt = Point.Construct();
                pnt.Location = v.Position;
                pnt.Symbol = PointSymbol.Cross;
                dc4.Add(pnt, v.GetHashCode());
            }
#endif
        }

        public static (Shell[] upperPart, Shell[] lowerPart) SplitByPlane(Shell shell, Plane pln)
        {
            BRepOperation brep = new BRepOperation(shell, pln);
            Shell[] upper = brep.Result();
            pln = new Plane(pln.Location, pln.DirectionY, pln.DirectionX); // the same plane, but reversed
            brep = new BRepOperation(shell, pln);
            Shell[] lower = brep.Result();
            return (upper, lower);
        }

        private void prepare()
        {
            BoundingCube ext1 = shell1.GetExtent(0.0);
            BoundingCube ext2 = shell2.GetExtent(0.0);
            BoundingCube ext = ext1;
            ext.MinMax(ext2);
            ext.Expand(ext.Size * 1e-6);
            Initialize(ext, ext.Size * 1e-6); // der OctTree
            // Alle edges und faces in den OctTree einfügen
            foreach (Edge edg in shell1.Edges)
            {
                base.AddObject(new BRepItem(this, edg));
#if DEBUG
                if (edg.Curve3D is InterpolatedDualSurfaceCurve) (edg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
#endif
            }
            foreach (Edge edg in shell2.Edges)
            {
                base.AddObject(new BRepItem(this, edg));
#if DEBUG
                if (edg.Curve3D is InterpolatedDualSurfaceCurve) (edg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
#endif
            }
            foreach (Face fc in shell1.Faces)
            {
                base.AddObject(new BRepItem(this, fc));
            }
            foreach (Face fc in shell2.Faces)
            {
                base.AddObject(new BRepItem(this, fc));
            }

            //GeoObjectList dbgle = DebugCubesByObject(shell2.Edges[27].Curve3D as IOctTreeInsertable);
            //GeoObjectList dbglf = DebugCubesByObject(shell1.Faces[27] as IOctTreeInsertable);
            findOverlappingFaces(); // setzt overlappingFaces, also Faces von verschiedenen shells, die sich teilweise überlappen oder identisch sind
            createEdgeFaceIntersections(); // Schnittpunkte von Edges mit Faces von verschiedenen shells
#if DEBUG
            DebuggerContainer dc3 = new DebuggerContainer();
            foreach (Edge edge in edgesToSplit.Keys)
            {
                dc3.Add(edge.Curve3D as IGeoObject, edge.GetHashCode());
            }
            foreach (IntersectionVertex iv in intersectionVertices)
            {
                Point pnt = Point.Construct();
                pnt.Location = iv.v.Position;
                pnt.Symbol = PointSymbol.Cross;
                dc3.Add(pnt, iv.v.GetHashCode());
            }
#endif
            if (operation != Operation.testonly)
            {   // Für testonly genügen die Kantenschnitte (fast)
                splitEdges(); // mit den gefundenen Schnittpunkten werden die Edges jetzt gesplittet
                combineVertices(); // alle geometrisch identischen Vertices werden zusammengefasst
                removeIdenticalOppositeFaces(); // Paare von entgegengesetzt orientierten Faces mit identischer Fläche werden entfernt
                createNewEdges(); // faceToIntersectionEdges wird gesetzt, also für jedes Face eine Liste der neuen Schnittkanten
                // createInnerFaceIntersections(); // Schnittpunkte zweier Faces, deren Kanten sich aber nicht schneiden, finden
                // createInnerFaceIntersections erstmal weglassen, macht noch zu viele probleme (Futter5.cdb)
                combineEdges(); // hier werden intsEdgeToEdgeShell1, intsEdgeToEdgeShell2 und intsEdgeToIntsEdge gesetzt, die aber z.Z. noch nicht verwendet werden

            }
        }

        private void createInnerFaceIntersections()
        {   // Hier sind Schnittkurven gesucht, die nicht durch kanten gehen. Z.B. zwei Zylinder, die sich nur knapp berühren.
            // Die durch Kantenschnitte ausgelösten Schnittkurven werden ja schon mit "createEdgeFaceIntersections" gefunden
            Set<DoubleFaceKey> candidates = new Set<DoubleFaceKey>(); // Kandidaten für sich schneidende Faces
            List<Node<BRepItem>> leaves = new List<Node<BRepItem>>(Leaves);
            foreach (Node<BRepItem> node in leaves)
            {
                foreach (BRepItem first in node.list)
                {
                    if (first.Type == BRepItem.ItemType.Face)
                    {
                        Face f1 = first.face;
                        IGeoObjectOwner shell = f1.Owner;
                        foreach (BRepItem second in node.list)
                        {
                            if (second.Type == BRepItem.ItemType.Face)
                            {
                                if (second.face.Owner != shell)
                                {   //wir brauchen die richtige Ordnung in den DoubleFaceKey Objekten:
                                    DoubleFaceKey df;
                                    if (shell == shell1)
                                    {
                                        df = new DoubleFaceKey(f1, second.face);
                                    }
                                    else
                                    {
                                        df = new DoubleFaceKey(second.face, f1);
                                    }
                                    if (df.face1.Surface is PlaneSurface)
                                    {   // Fälle, in denen es keine Schnittkurven gibt, die nicht auch die kanten des einen oder anderen faces schneiden, überspringen
                                        if (df.face2.Surface is PlaneSurface) continue;
                                        if (df.face2.Surface is CylindricalSurface) continue;
                                        if (df.face2.Surface is ConicalSurface) continue;
                                    }
                                    else if (df.face2.Surface is PlaneSurface)
                                    {   // Fälle, in denen es keine Schnittkurven gibt, die nicht auch die kanten des einen oder anderen faces schneiden, überspringen
                                        if (df.face1.Surface is PlaneSurface) continue;
                                        if (df.face1.Surface is CylindricalSurface) continue;
                                        if (df.face1.Surface is ConicalSurface) continue;
                                    }
                                    if ((df.face1.Surface is CylindricalSurface || df.face1.Surface is SphericalSurface || df.face1.Surface is ConicalSurface) &&
                                        (df.face2.Surface is CylindricalSurface || df.face2.Surface is SphericalSurface || df.face1.Surface is ConicalSurface))
                                    {
                                        // zwei (Halb-)Zylinder oder (Halb-)Kugeln können nur eine innere Schnittkurve haben. Wenn durch die Kanten schon eine gefunden
                                        // wurde, dann  braucht hier nicht weiter getestet zu werden
                                        if (facesToIntersectionVertices.ContainsKey(df)) continue;
                                        if (facesToIntersectionVertices.ContainsKey(new DoubleFaceKey(df.face2, df.face1))) continue;
                                    }
                                    // es gibt sicherlich noch mehr ausschließbare Fälle
                                    candidates.Add(df);
                                }
                            }
                        }

                    }
                }
            }
            foreach (DoubleFaceKey df in candidates)
            {
                BoundingRect ext1, ext2;
                ext1 = df.face1.Area.GetExtent();
                ext2 = df.face2.Area.GetExtent();
                ICurve[] innerCurves = Surfaces.IntersectInner(df.face1.Surface, ext1, df.face2.Surface, ext2);
                for (int i = 0; i < innerCurves.Length; i++)
                {   // es handelt sich immer um geschlossene Kurven
                    // Da es sich um echte innere Kurven handeln muss, also keine Schnitte mit den Kanten, genügt es, auf einen inneren Punkt zu testen
                    if (!df.face1.Area.Contains(df.face1.Surface.PositionOf(innerCurves[i].StartPoint), false)) continue;
                    if (!df.face2.Area.Contains(df.face2.Surface.PositionOf(innerCurves[i].StartPoint), false)) continue;
                    // es scheint besser zu sein, die Kurve aufzuteilen und zwei Kanten zu erzeugen
                    ICurve[] parts = innerCurves[i].Split(0.5);
                    if (parts.Length != 2) continue; // kommt nicht vor
                    Vertex v1 = null, v2 = null;
                    for (int j = 0; j < 2; j++)
                    {

                        ICurve2D c2df1 = df.face1.Surface.GetProjectedCurve(parts[j], precision);
                        ICurve2D c2df2 = df.face2.Surface.GetProjectedCurve(parts[j], precision);
                        bool dir = ((df.face1.Surface.GetNormal(c2df1.StartPoint) ^ df.face2.Surface.GetNormal(c2df2.StartPoint)) * parts[j].StartDirection) > 0;
                        GeoVector dbg1 = df.face1.Surface.GetNormal(c2df1.PointAt(0.5));
                        GeoVector dbg2 = df.face2.Surface.GetNormal(c2df2.PointAt(0.5));
                        GeoVector dbg3 = parts[j].StartDirection;
                        if (dir) c2df2.Reverse();
                        else c2df1.Reverse();
                        Edge edge = new Edge(df.face1, parts[j], df.face1, c2df1, dir, df.face2, c2df2, !dir);
                        if (j == 0)
                        {
                            edge.MakeVertices();
                            v1 = edge.Vertex1;
                            v2 = edge.Vertex2;
                        }
                        else
                        {
                            edge.Vertex1 = v2;
                            edge.Vertex2 = v1;
                        }
                        Set<Edge> addTo;
                        if (!faceToIntersectionEdges.TryGetValue(df.face1, out addTo))
                        {
                            addTo = new Set<Edge>(); // (new EdgeComparerByVertex()); // damit werden zwei Kanten mit gleichen Vertices nicht zugefügt, nutzt nichts
                            faceToIntersectionEdges[df.face1] = addTo;
                        }
                        addTo.Add(edge);
                        if (!faceToIntersectionEdges.TryGetValue(df.face2, out addTo))
                        {
                            addTo = new Set<Edge>(); //  (new EdgeComparerByVertex());
                            faceToIntersectionEdges[df.face2] = addTo;
                        }
                        addTo.Add(edge);
                    }
                }
            }
        }

        private void combineEdges()
        {
            // manche Schnittkanten sind identisch mit Kanten der Shells, nämlich genau dann, wenn Faces sich überlappen
            // (da die original-Kanten an den Durchstoßstellen aufgetielt sind und die Vertices zusammengefasst wurden,
            // lässt sich das rein combinatorisch bestimmen)
            // Es gibt zwei Fälle: 
            // 1. eine Schnittkante und die Kante einer Shell fallen zusammen
            // 2. eine Schittkante ist doppelt und zu jeder Shell gibt es eine passende Kante
            // (in beiden Fällen ist die Kante im Ergebnis nur einmal vertreten)
            // es sind auch Fälle denkbar, in denen eine Shell schon eine doppelte Kante hat. Die können wir hier nicht gebrauchen

            Set<Edge> intersectionEdges = new Set<Edge>();
            foreach (Set<Edge> se in faceToIntersectionEdges.Values)
            {
                intersectionEdges.AddMany(se);
            }
            intsEdgeToEdgeShell1 = new Dictionary<Edge, Edge>(); // diese IntersectionEdge ist identisch mit dieser kante auf Shell1
            intsEdgeToEdgeShell2 = new Dictionary<Edge, Edge>();
            intsEdgeToIntsEdge = new Dictionary<Edge, Edge>(); // zwei intersectionEdges sind identisch
            foreach (Edge edg in intersectionEdges)
            {
                foreach (Edge other in edg.Vertex1.AllEdges)
                {
                    if (other != edg && (other.Vertex1 == edg.Vertex2 || other.Vertex2 == edg.Vertex2) && SameEdge(edg, other, this.precision))
                    {
                        if (other.PrimaryFace.Owner == shell1 && other.SecondaryFace.Owner == shell1) intsEdgeToEdgeShell1[edg] = other;
                        else if (other.PrimaryFace.Owner == shell2 && other.SecondaryFace.Owner == shell2) intsEdgeToEdgeShell2[edg] = other;
                        else intsEdgeToIntsEdge[edg] = other; // wird nochmal mit vertauschten Rollen gefunden
                    }
                }
            }
#if DEBUG
            DebuggerContainer dc1 = new CADability.DebuggerContainer();
            DebuggerContainer dc2 = new CADability.DebuggerContainer();
            DebuggerContainer dc3 = new CADability.DebuggerContainer();
            foreach (Edge edg in intsEdgeToEdgeShell1.Keys)
            {
                dc1.Add(edg.Curve3D as IGeoObject);
            }
            foreach (Edge edg in intsEdgeToEdgeShell2.Keys)
            {
                dc2.Add(edg.Curve3D as IGeoObject);
            }
            foreach (Edge edg in intsEdgeToIntsEdge.Keys)
            {
                dc3.Add(edg.Curve3D as IGeoObject);
            }
#endif
        }

        private void GenerateOverlappingIntersections()
        {
            //throw new NotImplementedException();
        }
#if DEBUG
        public DebuggerContainer dbgFaceHashCodes
        {
            get
            {
                DebuggerContainer res = new DebuggerContainer();
                foreach (Face face in shell1.Faces)
                {
                    res.Add(face, face.GetHashCode());
                }
                foreach (Face face in shell2.Faces)
                {
                    res.Add(face, face.GetHashCode());
                }
                return res;
            }
        }
#endif

        private void findOverlappingFaces()
        {
            overlappingFaces = new Dictionary<DoubleFaceKey, ModOp2D>();
            oppositeFaces = new Dictionary<DoubleFaceKey, ModOp2D>();
            faceToOverlappingFaces = new Dictionary<Face, Set<Face>>();
            // Faces von verschiedenen Shells die identisch sind oder sich überlappen machen Probleme
            // beim Auffinden der Schnitte. Die Kanten und die Flächen berühren sich nur
            Set<DoubleFaceKey> candidates = new Set<DoubleFaceKey>(); // Kandidaten für parallele faces
            List<Node<BRepItem>> leaves = new List<Node<BRepItem>>(Leaves);
            Dictionary<Face, BRepItem> faceToBrepItem = new Dictionary<Face, BRepItem>();
            foreach (Node<BRepItem> node in leaves)
            {
                foreach (BRepItem first in node.list)
                {
                    if (first.Type == BRepItem.ItemType.Face)
                    {
                        Face f1 = first.face;
                        IGeoObjectOwner shell = f1.Owner;
                        foreach (BRepItem second in node.list)
                        {
                            if (second.Type == BRepItem.ItemType.Face)
                            {
                                if (second.face.Owner != shell)
                                {   //wir brauchen die richtige Ordnung in den DoubleFaceKey Objekten:
                                    if (shell == shell1)
                                    {
                                        candidates.Add(new DoubleFaceKey(f1, second.face));
                                    }
                                    else
                                    {
                                        candidates.Add(new DoubleFaceKey(second.face, f1));
                                    }
                                    faceToBrepItem[f1] = first;
                                    faceToBrepItem[second.face] = second; // to be able to remove from OctTree
                                }
                            }
                        }

                    }
                }
            }
            foreach (DoubleFaceKey df in candidates)
            {
                ModOp2D firstToSecond;
                BoundingRect ext1, ext2;
                df.face1.Surface.GetNaturalBounds(out ext1.Left, out ext1.Right, out ext1.Bottom, out ext1.Top);
                df.face2.Surface.GetNaturalBounds(out ext2.Left, out ext2.Right, out ext2.Bottom, out ext2.Top);
                // Achtung: SameGeometry geht z.Z. nur mit fester ModOp2D. Denkbar sind auch Fälle, bei denen es keine solche Modop gibt
                // aber dennoch die selbe Geometrie vorliegt: z.B. bei nicht-periodischem Zylinder oder verdrehter Kugel. Die brauchen wir aber auch!!!
                if (df.face1.Surface.SameGeometry(ext1, df.face2.Surface, ext2, this.precision, out firstToSecond))
                {   // es gilt nur, wenn die Orientierung die selbe ist
                    GeoVector n1 = df.face1.Surface.GetNormal(ext1.GetCenter());
                    GeoVector n2 = df.face2.Surface.GetNormal(firstToSecond * ext1.GetCenter());
                    if (n1 * n2 > 0)
                    {
                        overlappingFaces.Add(df, firstToSecond);
                        df.face1.UserData.Add("BRepIntersection.OverlapsWith", df.face2);
                        df.face2.UserData.Add("BRepIntersection.OverlapsWith", df.face1);
                    }
                    else
                    {
                        oppositeFaces.Add(df, firstToSecond);
                    }
                    Set<Face> setToAddTo;
                    if (!faceToOverlappingFaces.TryGetValue(df.face1, out setToAddTo))
                    {
                        setToAddTo = new Set<Face>();
                        faceToOverlappingFaces[df.face1] = setToAddTo;
                    }
                    setToAddTo.Add(df.face2);
                    if (!faceToOverlappingFaces.TryGetValue(df.face2, out setToAddTo))
                    {
                        setToAddTo = new Set<Face>();
                        faceToOverlappingFaces[df.face2] = setToAddTo;
                    }
                    setToAddTo.Add(df.face1);
                }
            }

            // tried to split faces at touching points: not necessary
            //foreach (DoubleFaceKey df in candidates)
            //{
            //    GeoPoint[] tp = df.face1.Surface.GetTouchingPoints(df.face1.Area.GetExtent(), df.face2.Surface, df.face2.Area.GetExtent());
            //    if (tp != null && tp.Length > 0)
            //    {
            //        for (int i = 0; i < tp.Length; i++)
            //        {
            //            GeoPoint2D uv1 = df.face1.PositionOf(tp[i]);
            //            GeoPoint2D uv2 = df.face2.PositionOf(tp[i]);
            //            if (df.face1.Contains(ref uv1, true) && df.face2.Contains(ref uv2, true))
            //            {
            //                Face[] replace1 = df.face1.SplitUv(uv1);
            //                Face[] replace2 = df.face2.SplitUv(uv1);
            //                shell1.ReplaceFace(df.face1, replace1, precision);
            //                shell2.ReplaceFace(df.face2, replace2, precision);
            //                base.RemoveObject(faceToBrepItem[df.face1]);
            //                base.RemoveObject(faceToBrepItem[df.face2]);
            //                for (int j = 0; j < replace1.Length; j++)
            //                {
            //                    base.AddObject(new BRepItem(this, replace1[j]));
            //                }
            //                for (int j = 0; j < replace2.Length; j++)
            //                {
            //                    base.AddObject(new BRepItem(this, replace2[j]));
            //                }
            //            }
            //        }
            //    }
            //}

        }
        /// <summary>
        /// Beschreibt ein Objekt, welches an Edge hängt und für alle möglichen Zusatzinfos bezüglich der Edge während der BRepOperation verwendet werden kann
        /// Damit kann man die umständliche Speicherung von Extras per UserData umgehen, und ist schneller.
        /// </summary>
        internal class EdgeInfo
        {
            EdgeOnFace primary, secondary;
            Edge edge;
            public Face oldConnection;
            public bool isIntersection;
            public bool isOverlapping;
            public Edge next, prev; // für generateCircle
            public EdgeInfo(Edge edge)
            {
                this.edge = edge;
            }
            internal EdgeOnFace getOnFace(Face face)
            {
                if (face == edge.PrimaryFace) return primary;
                else return secondary;
            }
            internal void setOnFace(EdgeOnFace onFace)
            {
                if (onFace.face == edge.PrimaryFace) primary = onFace;
                else secondary = onFace;
            }

            internal static EdgeInfo FromEdge(Edge edge)
            {
                if (edge.edgeInfo == null)
                {
                    edge.edgeInfo = new EdgeInfo(edge);
                    edge.edgeInfo.setOnFace(new EdgeOnFace(edge, edge.PrimaryFace, false));
                    edge.edgeInfo.setOnFace(new EdgeOnFace(edge, edge.SecondaryFace, false));
                }
                return edge.edgeInfo;
            }
        }
        internal class EdgeOnFace
        {
            public Edge edge;
            public Face face;
            public ICurve2D curve2d;
            public Vertex startVertex;
            public Vertex endVertex;
            public bool isIntersection;
            public List<EdgeOnFace> outgoing;

            public int hashCode;
            static private int hashCodeCounter = 0;

            public EdgeOnFace()
            {
                outgoing = new List<EdgeOnFace>();
                hashCode = ++hashCodeCounter;
            }

            public EdgeOnFace(Edge edge, Face face, bool isIntersection) : this()
            {
                this.edge = edge;
                this.face = face;
                this.isIntersection = isIntersection;
                curve2d = edge.Curve2D(face);
                startVertex = edge.StartVertex(face);
                endVertex = edge.EndVertex(face);
            }

            internal void addOutgoing(Edge oedge, Face face)
            {
                outgoing.Add(EdgeInfo.FromEdge(oedge).getOnFace(face));
            }
        }
        public CompoundShape SplitResult()
        {
            Shell[] shells = Result();
            CompoundShape res = new CompoundShape();
            for (int i = 0; i < shells.Length; i++)
            {
                foreach (Face fc in shells[i].Faces)
                {
                    ModOp2D fts;
                    if (fc.Surface is PlaneSurface && fc.Surface.SameGeometry(fc.Area.GetExtent(), splittingOnplane, fc.Area.GetExtent(), 0.0, out fts))
                    {
                        if (fts.IsIdentity)
                        { // this is a face created by the splitting plane
                            res.UniteDisjunct(fc.Area);
                        }
                    }
                }
            }
            return res;
        }
        private class LoopCollection : SortedDictionary<double, Pair<List<Edge>, ICurve2D[]>>
        {
            private class CompareReverse : IComparer<double>
            {
                int IComparer<double>.Compare(double x, double y)
                {
                    return -x.CompareTo(y); ;
                }
            }
            public LoopCollection() : base(new CompareReverse()) { }
            public void AddUnique(double d, Pair<List<Edge>, ICurve2D[]> val)
            {
                while (this.ContainsKey(d)) d = Geometry.NextDouble(d);
                Add(d, val);
            }
            public void AddUnique(List<Edge> loop, Face onThisFace)
            {
                ICurve2D[] loop2d = onThisFace.Get2DCurves(loop);
                AddUnique(Border.SignedArea(loop2d), new Pair<List<Edge>, ICurve2D[]>(loop, loop2d));
            }
        }
        /// <summary>
        /// A dictionary with keys of type double, which manages adding same key entries by incrementing the key until the value can be added
        /// </summary>
        /// <typeparam name="T"></typeparam>
        private class UniqueDoubleReverseDictionary<T> : SortedDictionary<double, T>
        {
            private class CompareReverse : IComparer<double>
            {
                int IComparer<double>.Compare(double x, double y)
                {
                    return -x.CompareTo(y); ;
                }
            }
            public UniqueDoubleReverseDictionary() : base(new CompareReverse()) { }
            public void AddUnique(double d, T val)
            {
                while (this.ContainsKey(d)) d = Geometry.NextDouble(d);
                Add(d, val);
            }
        }
        private class SetEquality<T> : IEqualityComparer<Set<T>>
        {
            bool IEqualityComparer<Set<T>>.Equals(Set<T> x, Set<T> y)
            {
                return x.IsEqualTo(y);
            }

            int IEqualityComparer<Set<T>>.GetHashCode(Set<T> obj)
            {
                int res = 0;
                foreach (T item in obj)
                {
                    res ^= item.GetHashCode();
                }
                return res;
            }
        }

        private class VertexConnectionSet
        {
            private Dictionary<DoubleVertexKey, ICurve> set = new Dictionary<DoubleVertexKey, ICurve>();
            public void Add(Edge edge)
            {
                set[new DoubleVertexKey(edge.Vertex1, edge.Vertex2)] = edge.Curve3D;
            }
            public bool Contains(Edge edge, double precision)
            {
                if (set.TryGetValue(new DoubleVertexKey(edge.Vertex1, edge.Vertex2), out ICurve crv))
                {
                    if (crv.DistanceTo(edge.Curve3D.PointAt(0.5)) < 10 * precision) return true;
                }
                return false;
            }
            public bool ContainsAll(IEnumerable<Edge> edges, double precision)
            {
                foreach (Edge edg in edges)
                {
                    if (!Contains(edg, precision)) return false;
                }
                return true;
            }
        }
        public Shell[] Result()
        {
            // this method relies on
            // - faceToIntersectionEdges: contains all faces, which intersect with other faces as keys and all the intersection edges, 
            //   which are produced by other faces on this face as values.
            // - overlappingFaces and oppositeFaces: they contain pairs of faces, which overlap, with either same or oppostie orientation.
            // 
            // The main algorithm does the following: split (or trimm or cut) a face according to the intersection edges on this face. 
            // All edges are oriented, so it is possible to find outer edges and holes. A face might produce zero, one or multiple trimmed faces.
            // When all faces are trimmed, connect the trimmed faces with the untouched faces. this is the result.
            //
            // When there are overlapping faces, cut them into non-overlapping parts before the main algorithm.
            //
            // This algorithm does not rely on geometry (coordinates of points and curves) but only on topological information, excetp for
            // - finding, whether two edges connecting the same vertices, are equal (SameEdge) and
            // - checking, whether a 2d loop of edges is inside another 2d loop of edges


#if DEBUG   // show the starting position: dcFaces: all faces with hashCodes of both shells and their normals
            // dcs1e and dcs2e: the 3d edges and their hashCodes
            // dcis: all intersection edges. Here the edges must build one ore more closed curves. there can not be open ends.
            // If there are open ends, some intersection calculation failed!
            DebuggerContainer dcFaces = new CADability.DebuggerContainer();
            foreach (Face fce in shell1.Faces)
            {
                dcFaces.Add(fce.Clone(), fce.GetHashCode()); // die Faces werden kaputt gemacht, deshalb hier clones merken
                double ll = fce.GetExtent(0.0).Size * 0.01;
                ColorDef cd = new ColorDef("debug", System.Drawing.Color.Blue);
                SimpleShape ss = fce.Area;
                GeoPoint2D c = ss.GetExtent().GetCenter();
                GeoPoint pc = fce.Surface.PointAt(c);
                GeoVector nc = fce.Surface.GetNormal(c);
                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
                l.ColorDef = cd;
                dcFaces.Add(l);
            }
            foreach (Face fce in shell2.Faces)
            {
                dcFaces.Add(fce.Clone(), fce.GetHashCode()); // use clones, because the faces might be destroyed in course of this routine
                double ll = fce.GetExtent(0.0).Size * 0.01;
                ColorDef cd = new ColorDef("debug", System.Drawing.Color.Brown);
                SimpleShape ss = fce.Area;
                GeoPoint2D c = ss.GetExtent().GetCenter();
                GeoPoint pc = fce.Surface.PointAt(c);
                GeoVector nc = fce.Surface.GetNormal(c);
                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
                l.ColorDef = cd;
                dcFaces.Add(l);
            }
            DebuggerContainer dcs1e = new CADability.DebuggerContainer();
            foreach (Edge edg in shell1.Edges)
            {
                if (edg.Curve3D != null) dcs1e.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
            }
            DebuggerContainer dcs2e = new CADability.DebuggerContainer();
            foreach (Edge edg in shell2.Edges)
            {
                if (edg.Curve3D != null) dcs2e.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
            }
            DebuggerContainer dcis = new CADability.DebuggerContainer(); // <----- dcis shows the intersection curves
            Set<Edge> ise = new Set<Edge>();
            foreach (KeyValuePair<Face, Set<Edge>> item in faceToIntersectionEdges)
            {
                ise.AddMany(item.Value);
            }
            foreach (Edge edg in ise)
            {
                if (edg.Curve3D != null) dcis.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
            }
            Dictionary<Face, DebuggerContainer> debugTrimmedFaces = new Dictionary<Face, DebuggerContainer>();
            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
            {
                debugTrimmedFaces[kv.Key] = new DebuggerContainer();
                debugTrimmedFaces[kv.Key].Add(kv.Key.Clone(), System.Drawing.Color.Black, kv.Key.GetHashCode());
                int dbgclr = 1;
                foreach (Edge edg in kv.Value)
                {
                    Face other = edg.OtherFace(kv.Key);
                    debugTrimmedFaces[kv.Key].Add(other.Clone() as Face, DebuggerContainer.FromInt(dbgclr++), other.GetHashCode());
                }
            }
            Dictionary<Face, GeoObjectList> faceToMixedEdgesDebug = new Dictionary<Face, GeoObjectList>();
            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
            {
                GeoObjectList l = new GeoObjectList();
                faceToMixedEdgesDebug[kv.Key] = l;
                l.Add(kv.Key);
                foreach (Edge edg in kv.Value)
                {
                    if (edg.Curve3D != null)
                    {
                        if (edg.Forward(kv.Key)) l.Add(edg.Curve3D as IGeoObject);
                        else
                        {
                            ICurve c3d = edg.Curve3D.Clone();
                            c3d.Reverse();
                            l.Add(c3d as IGeoObject);
                        }
                    }
                }
            }
#endif
#if DEBUG
            Dictionary<Face, DebuggerContainer> dbgFaceTointersectionEdges = new Dictionary<Face, DebuggerContainer>();
            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
            {
                DebuggerContainer dc = new DebuggerContainer();
                dbgFaceTointersectionEdges[kv.Key] = dc;
                int dbgc = 0;
                double arrowSize = kv.Key.Area.GetExtent().Size * 0.02;
                dc.Add(kv.Value, kv.Key, arrowSize, System.Drawing.Color.Red, 0);
                dc.Add(kv.Key.AllEdgesIterated(), kv.Key, arrowSize, System.Drawing.Color.Blue, 0);
            }
#endif
#if DEBUG
            DebuggerContainer dcif = new DebuggerContainer();
            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
            {
                dcif.Add(kv.Key, kv.Key.GetHashCode());
                foreach (Edge edg in kv.Value)
                {
                    dcif.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
                }
            }
            Dictionary<Face, DebuggerContainer> dbgEdgePositions = new Dictionary<Face, DebuggerContainer>();
            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
            {
                DebuggerContainer dc = new DebuggerContainer();
                dbgEdgePositions[kv.Key] = dc;
                int dbgc = 0;
                double arrowSize = kv.Key.Area.GetExtent().Size * 0.02;
                dc.Add(kv.Value, kv.Key, arrowSize, System.Drawing.Color.Red, 0);
                dc.Add(kv.Key.AllEdgesIterated(), kv.Key, arrowSize, System.Drawing.Color.Blue, 0);
            }
#endif
            Set<Face> discardedFaces = new Set<Face>(faceToIntersectionEdges.Keys); // these faces may not apper in the final result, because they will be trimmed
            Set<Face> trimmedFaces = new Set<Face>(); // collection of faces which are trimmed (splitted, cut, edged) during this process
            faceToCommonFaces = new Dictionary<Face, Set<Face>>(); // to each overlapping face associate the common parts with other faces (both orientations)
            Set<Face> usedByOverlapping = new Set<Face>();
            Set<Face> overlappingCommonFaces = CollectOverlappingCommonFaces(usedByOverlapping); // same oriented overlapping faces yield their common parts
            Set<Face> oppositeCommonFaces = CollectOppositeCommonFaces(discardedFaces); // same oriented overlapping faces yield their common parts
            VertexConnectionSet nonManifoldEdges = new VertexConnectionSet();
            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
            {   // faceToIntersectionEdges contains all faces, which are intersected by faces of the relative other shell, as well as those intersection edges
                Face faceToSplit = kv.Key;
#if DEBUG       // show the faceToSplit and all other faces, which caused the intersectionEdges
                // does not work for overlapping faces
                debugTrimmedFaces.TryGetValue(kv.Key, out DebuggerContainer dcInvolvedFaces);
#endif
                Set<Edge> faceEdges = new Set<Edge>(faceToSplit.AllEdgesSet); // all outline edges and holes of the face, used edges will be removed
                Set<Edge> intersectionEdges = kv.Value.Clone();
                Set<Edge> originalEdges = faceToSplit.AllEdgesSet;
                Set<Vertex> faceVertices = new Set<Vertex>(faceToSplit.Vertices);
#if DEBUG
                DebuggerContainer dcIntersectingFaces = new DebuggerContainer();
                foreach (Edge edg in intersectionEdges)
                {
                    dcIntersectingFaces.Add(edg.OtherFace(faceToSplit), edg.GetHashCode());
                }
#endif
                bool hasNonManifoldEdge = false;
                // some intersection edges are created twice (e.g. when an edge fo shell2 is contained in a face of shell1)
                // if the duplicates have the same orientation, discard one of the edges, if they have opposide direction, discard both
                Dictionary<Pair<Vertex, Vertex>, Edge> avoidDuplicates = new Dictionary<Pair<Vertex, Vertex>, Edge>();
                Dictionary<Pair<Vertex, Vertex>, Edge> avoidOriginalEdges = new Dictionary<Pair<Vertex, Vertex>, Edge>();
                foreach (Edge edg in faceToSplit.AllEdgesIterated())
                {
                    Pair<Vertex, Vertex> k = new Pair<Vertex, Vertex>(edg.StartVertex(faceToSplit), edg.EndVertex(faceToSplit));
                    avoidOriginalEdges[k] = edg;
                }
                foreach (Edge edg in intersectionEdges.Clone())
                {
                    Pair<Vertex, Vertex> k = new Pair<Vertex, Vertex>(edg.StartVertex(faceToSplit), edg.EndVertex(faceToSplit));
                    Pair<Vertex, Vertex> k1 = new Pair<Vertex, Vertex>(k.Second, k.First);
                    if (avoidDuplicates.ContainsKey(k) && SameEdge(avoidDuplicates[k], edg, precision))
                    {
                        intersectionEdges.Remove(edg); // this is a duplicate edge. It is probably also an original edge
                                                       // it is arbitrary, which intersection edge will be removed.
                        edg.DisconnectFromFace(faceToSplit); // disconnecting is important, because of the vertex->edge->face references
                    }
                    else if (avoidDuplicates.ContainsKey(k1) && SameEdge(avoidDuplicates[k1], edg, precision))
                    {   // Reverse duplicates: remove both intersection edges
                        intersectionEdges.Remove(edg);
                        intersectionEdges.Remove(avoidDuplicates[k1]);
                        edg.DisconnectFromFace(faceToSplit);
                        avoidDuplicates[k1].DisconnectFromFace(faceToSplit);
                        if ((avoidOriginalEdges.ContainsKey(k) && SameEdge(avoidOriginalEdges[k], edg, precision)) ||
                            (avoidOriginalEdges.ContainsKey(k1) && SameEdge(avoidOriginalEdges[k1], edg, precision)))
                        {
                            // two inverse intersection edges are also identical with an original edge:
                            // this will make an ambiguous situation
                            nonManifoldEdges.Add(edg);
                            hasNonManifoldEdge = true;
                        }
                    }
                    else
                    {
                        avoidDuplicates[k] = edg;
                    }
                }
                foreach (Edge edg in intersectionEdges.Clone())
                {
                    Pair<Vertex, Vertex> k = new Pair<Vertex, Vertex>(edg.StartVertex(faceToSplit), edg.EndVertex(faceToSplit));
                    Pair<Vertex, Vertex> k1 = new Pair<Vertex, Vertex>(k.Second, k.First);
                    if (avoidOriginalEdges.ContainsKey(k) && SameEdge(avoidOriginalEdges[k], edg, precision))
                    {
                        intersectionEdges.Remove(edg); // this is an intersection edge identical to an original outline of the face: remove the intersection edge
                        edg.DisconnectFromFace(faceToSplit); // disconnecting is important, because of the vertex->edge->face references
                    }
                    else if (avoidOriginalEdges.ContainsKey(k1) && SameEdge(avoidOriginalEdges[k1], edg, precision))
                    {   // this is an intersection edge invers to an original outline of the face: remove both the intersection edge and the original edge
                        intersectionEdges.Remove(edg);
                        originalEdges.Remove(avoidOriginalEdges[k1]);
                        edg.DisconnectFromFace(faceToSplit);
                    }
                }
                bool intersectionEdgeRemovedByCommonFace = false;
                if (faceToCommonFaces.TryGetValue(faceToSplit, out Set<Face> createdCommonfaces))
                {   // there have been common faces created using this face
                    Dictionary<Pair<Vertex, Vertex>, Edge> avoidCommonEdges = new Dictionary<Pair<Vertex, Vertex>, Edge>();
                    foreach (Face ccf in createdCommonfaces)
                    {
                        if (oppositeCommonFaces.Contains(ccf))
                        {   // an opposite face: use reverse edges
                            foreach (Edge edg in ccf.Edges)
                            {
                                bool reverse = ccf.UserData.Contains("BRepIntersection.IsOpposite");
                                // reverse means reverse to the face from shell1
                                if (faceToSplit.Owner == shell2) reverse = !reverse;
                                if (reverse) avoidCommonEdges.Add(new Pair<Vertex, Vertex>(edg.EndVertex(ccf), edg.StartVertex(ccf)), edg);
                                else avoidCommonEdges.Add(new Pair<Vertex, Vertex>(edg.StartVertex(ccf), edg.EndVertex(ccf)), edg);
                            }
                        }
                        else
                        {
                            foreach (Edge edg in ccf.Edges)
                            {
                                if (avoidCommonEdges.ContainsKey(new Pair<Vertex, Vertex>(edg.StartVertex(ccf), edg.EndVertex(ccf))))
                                {

                                }
                                avoidCommonEdges.Add(new Pair<Vertex, Vertex>(edg.StartVertex(ccf), edg.EndVertex(ccf)), edg);
                            }
                        }
                    }
                    // avoidCommonEdges are all the edges that are used by already created common faces
                    // identical intersection edges may not be used any more
                    foreach (Edge edg in intersectionEdges.Clone())
                    {
                        Pair<Vertex, Vertex> k = new Pair<Vertex, Vertex>(edg.StartVertex(faceToSplit), edg.EndVertex(faceToSplit));
                        if (avoidCommonEdges.ContainsKey(k) && SameEdge(avoidCommonEdges[k], edg, precision))
                        {
                            intersectionEdges.Remove(edg); // this is an intersection edge identical to an outline of a common face: remove the intersection edge
                            intersectionEdgeRemovedByCommonFace = true; // see below: faceToSplit may not be used
                            edg.DisconnectFromFace(faceToSplit); // disconnecting is important, because of the vertex->edge->face references
                        }
                    }
                }

                // now originalEdges contain all edges of the face, that could be used, intersectionEdges contain all edges that must be used
#if DEBUG       // show the original edges of the faceToSplit (blue) and the intersection edges (red) for this face, where duplicates and reverses are already removed
                // in this 2d display it should be easy to see, which loops should be generated
                double arrowSize = kv.Key.Area.GetExtent().Size * 0.01;
                DebuggerContainer dcedges = new DebuggerContainer();
                dcedges.Add(originalEdges, faceToSplit, arrowSize, System.Drawing.Color.Blue, -1);
                dcedges.Add(intersectionEdges, faceToSplit, arrowSize, System.Drawing.Color.Red, -1);

#endif
                if (intersectionEdges.Count == 0)
                {
                    // all intersection edges are identical or reverse to original edges. If they ware all identical, we can still use this face
                    if (originalEdges.Count == faceToSplit.AllEdgesCount && !usedByOverlapping.Contains(faceToSplit) && !faceToCommonFaces.ContainsSameFace(faceToSplit, precision))
                    {
                        if (hasNonManifoldEdge) trimmedFaces.Add(faceToSplit); // this face must be used, although it might be part of an open shell (hasNonManifoldEdge is very rare)
                        if (!intersectionEdgeRemovedByCommonFace) discardedFaces.Remove(faceToSplit); // allow this face be used for the final shells if needed
                    }
                    continue; // nothing to do here
                }
                LoopCollection loops = new LoopCollection();
                // loops: all loops for the trimmed face, (reverse-) sorted by size of 2d area . 
                // No problem with exactely same area (*Unique*DoubleReverseDictionary).
                // We need the array of 2d curves multiple times, so keep it as second part of the pair. 
                while (intersectionEdges.Count > 0)
                {
                    Edge edg = intersectionEdges.GetAny();
                    List<Edge> loop = FindLoop(edg, edg.StartVertex(faceToSplit), faceToSplit, intersectionEdges, originalEdges);
                    if (loop != null) loops.AddUnique(loop, faceToSplit);
                }

#if DEBUG
                DebuggerContainer dcloops = new DebuggerContainer();
                int dbgc = 0;
                foreach (Pair<List<Edge>, ICurve2D[]> item in loops.Values)
                {
                    dcloops.Add(item.First, faceToSplit, arrowSize, System.Drawing.Color.Blue, ++dbgc);
                }
#endif

                // If there is no positive loop, the outline loop of the face must be available and must be used.
                // actually we would have to topologically sort the loops in order to decide whether the faces outline must be used or not.
                // and what about the untouched holes of the face?
                double biggestArea = 0.0;
                foreach (double a in loops.Keys)
                {
                    if (Math.Abs(a) > Math.Abs(biggestArea)) biggestArea = a;
                }
                bool totalOutlineAdded = false;
                if (biggestArea < 0) // when no loop, we don't need the outline
                {
                    foreach (Pair<List<Edge>, ICurve2D[]> item in loops.Values) faceEdges.RemoveMany(item.First);
                    if (faceEdges.ContainsAll(faceToSplit.OutlineEdges))
                    {
                        // there is no outline loop, only holes (or nothing). We have to use the outline loop of the face, which is not touched
                        List<Edge> outline = new List<Edge>(faceToSplit.OutlineEdges);
                        loops.AddUnique(outline, faceToSplit);
                        faceEdges.RemoveMany(outline); // we would not need that
                        totalOutlineAdded = true;
                    }
                }
                // we also add the holes of the faceToSplit, as long as it was not used by intersections and is not enclosed by a bigger hole
                // created from intersection edges
                for (int i = 0; i < faceToSplit.HoleCount; i++)
                {
                    if (faceEdges.ContainsAll(faceToSplit.HoleEdges(i)))
                    {
                        List<Edge> hole = new List<Edge>(faceToSplit.HoleEdges(i));
                        ICurve2D[] c2ds = faceToSplit.Get2DCurves(hole);
                        // now check, wether this hole is enclosed by another hole
                        double area = Border.SignedArea(c2ds); // the negative area of this hole
                        GeoPoint2D testPoint = c2ds[0].StartPoint; // some point on this loop to test, wether this loop is not enclosed by another hole
                        bool enclosedByHole = false;
                        foreach (var loop in loops)
                        {
                            if (loop.Key < area) // an enclosing hole must be bigger, i.e. its area, which is negative, must be smaller (more negative)
                            {
                                if (!Border.IsInside(loop.Value.Second, testPoint))
                                {
                                    enclosedByHole = true;
                                    break;
                                }
                            }
                            if (enclosedByHole) break;
                        }
                        if (!enclosedByHole)
                        {
                            // in order to use a hole, it must be contained in a outer, positive loop
                            bool isContainedInOutline =  totalOutlineAdded;
                            foreach (var loop in loops)
                            {
                                if (loop.Key > 0) // an outline
                                {
                                    if (Border.IsInside(loop.Value.Second, testPoint))
                                    {
                                        isContainedInOutline = true;
                                        break;
                                    }
                                }
                                if (isContainedInOutline) break;
                            }
                            if (isContainedInOutline) loops.AddUnique(area, new Pair<List<Edge>, ICurve2D[]>(hole, c2ds));
                        }
                        faceEdges.RemoveMany(hole); // we would not need that
                    }
                }
                // Now all necessary loops are created. There is one or more outline (ccw) and zero or more holes
                // If we have more than one outline, we have to match the holes to their enclosing outline
                double[] areas = new double[loops.Count];
                Edge[][] edgeLoop = new Edge[loops.Count][];
                ICurve2D[][] loops2D = new ICurve2D[loops.Count][];
                loops.Keys.CopyTo(areas, 0);
                int ii = 0;
                foreach (Pair<List<Edge>, ICurve2D[]> item in loops.Values)
                {
                    edgeLoop[ii] = item.First.ToArray();
                    loops2D[ii] = item.Second;
                    ++ii;
                }
                List<List<int>> outlineToHoles = new List<List<int>>(); // for each outline a list (of indices) of the corresponding holes
                int numOutlines = 0;
                for (int i = 0; i < areas.Length; i++)
                {
                    if (areas[i] > 0) // an outline
                    {
                        ++numOutlines;
                        outlineToHoles.Add(new List<int>()); // holes go here
                    }
                    else // a hole
                    {
                        // since area is sorted, all outlines are now given
                        if (numOutlines == 1) outlineToHoles[0].Add(i);
                        else
                        {
                            for (int j = 0; j < outlineToHoles.Count; j++)
                            {
                                if (Border.IsInside(loops2D[j], loops2D[i][0].StartPoint))
                                {
                                    outlineToHoles[j].Add(i);
                                    break;
                                }
                            }
                        }
                    }
                }
#if DEBUG       // show the loops that create the new face(s)
                DebuggerContainer dcLoops = new DebuggerContainer();
                // Show all loops, beginning with biggest loop
                dbgc = 0;
                foreach (var item in loops)
                {
                    foreach (var loop in loops.Values)
                    {
                        dcLoops.Add(loop.Second, arrowSize, DebuggerContainer.FromInt(dbgc), dbgc);
                    }
                    ++dbgc;
                }
#endif
                // each outline (only one in most cases) creates a new Face. 
                for (int i = 0; i < numOutlines; i++)
                {
                    // maybe the new face is identical to one of the commonFaces
                    if (faceToCommonFaces.ContainsKey(faceToSplit)) // overlappingCommonFaces.Count > 0 || oppositeCommonFaces.Count > 0)
                    {
                        Set<Vertex> vertices = new Set<Vertex>(); // all vertices of the face to be created
                        Set<Edge> allEdges = new Set<Edge>();
                        foreach (Edge edg in edgeLoop[i])
                        {
                            allEdges.Add(edg);
                            vertices.Add(edg.Vertex1);
                            vertices.Add(edg.Vertex2);
                        }
                        for (int j = 0; j < outlineToHoles[i].Count; j++)
                        {
                            foreach (Edge edg in edgeLoop[outlineToHoles[i][j]])
                            {
                                allEdges.Add(edg);
                                vertices.Add(edg.Vertex1);
                                vertices.Add(edg.Vertex2);
                            }
                        }
                        bool faceIsCommonFace = false;
                        foreach (Face fce in faceToCommonFaces[faceToSplit])
                        {
                            if (IsSameFace(allEdges, vertices, fce, precision))
                            {
                                faceIsCommonFace = true;
                                break;
                            }
                        }
                        if (faceIsCommonFace)
                        {
                            for (int j = 0; j < outlineToHoles[i].Count; j++)
                            {
                                foreach (Edge edg in edgeLoop[outlineToHoles[i][j]])
                                {
                                    edg.DisconnectFromFace(faceToSplit);
                                }
                            }
                            foreach (Edge edg in edgeLoop[i])
                            {
                                edg.DisconnectFromFace(faceToSplit);
                            }
                            continue; // don't create this face, it already exists as a common face
                        }
                    }
                    Face fc = Face.Construct();
                    Edge[][] holes = new Edge[outlineToHoles[i].Count][]; // corresponding list of holes to outline number i
                    for (int j = 0; j < outlineToHoles[i].Count; j++)
                    {
                        holes[j] = edgeLoop[outlineToHoles[i][j]];
                        foreach (Edge edg in holes[j])
                        {
                            edg.ReplaceOrAddFace(faceToSplit, fc);
                        }
                    }
                    foreach (Edge edg in edgeLoop[i])
                    {
                        edg.ReplaceOrAddFace(faceToSplit, fc);
                    }
                    fc.Set(faceToSplit.Surface.Clone(), edgeLoop[i], holes); // we need a clone of the surface because two independant faces shall not have the identical surface
                    fc.CopyAttributes(faceToSplit);
#if DEBUG
                    System.Diagnostics.Debug.Assert(fc.CheckConsistency());
#endif
                    trimmedFaces.Add(fc);
                }
            }

            // Now trimmedFaces contains all faces which are cut by faces of the relative other shell, even those, where the other shell cuts 
            // exactely along existing edges and nothing has been created.
            // The faces, which have been cut, i.e. faceToIntersectionEdges.Keys, are invalid now, we disconnect all egdes from these faces
            trimmedFaces.AddMany(overlappingCommonFaces);
            discardedFaces.AddMany(usedByOverlapping);
#if DEBUG   // show all trimmed faces
            DebuggerContainer cdTrimmedFaces = new DebuggerContainer();
            foreach (Face fce in trimmedFaces)
            {
                cdTrimmedFaces.Add(fce.Clone(), fce.GetHashCode());
            }
            Set<Edge> openTrimmedEdges = new Set<Edge>();
            foreach (Face fce in trimmedFaces)
            {
                foreach (Edge edg in fce.AllEdges)
                {
                    if (edg.SecondaryFace == null)
                    {
                        openTrimmedEdges.Add(edg);
                    }
                }
            }
#endif
            // to avoid oppositeCommonFaces to be connected with the trimmedFaces, we destroy these faces
            foreach (Face fce in oppositeCommonFaces) fce.DisconnectAllEdges();
            foreach (Face fce in discardedFaces) fce.DisconnectAllEdges(); // to avoid connecting with discardedFaces
            // if we have two open edges in the trimmed faces which are identical, connect them
            Dictionary<DoubleVertexKey, Edge> trimmedEdges = new Dictionary<DoubleVertexKey, Edge>();
            foreach (Face fce in trimmedFaces)
            {
                foreach (Edge edg in fce.AllEdges)
                {
                    DoubleVertexKey dvk = new DoubleVertexKey(edg.Vertex1, edg.Vertex2);
                    if (nonManifoldEdges.Contains(edg, precision)) // is empty in most cases
                    {
                        if (edg.SecondaryFace != null)
                        {   // seperate nonManifold edges, they should not be used for collecting faces for the shell
                            edg.SecondaryFace.SeperateEdge(edg);
                        }
                    }
                    else if (edg.SecondaryFace == null || !trimmedFaces.Contains(edg.SecondaryFace) || !trimmedFaces.Contains(edg.PrimaryFace))
                    {   // only those edges, which 
                        if (trimmedEdges.TryGetValue(dvk, out Edge other))
                        {
                            if (other == edg) continue;
                            if (SameEdge(edg, other, precision))
                            {
                                if (edg.SecondaryFace != null)
                                {
                                    if (!trimmedFaces.Contains(edg.SecondaryFace)) edg.RemoveFace(edg.SecondaryFace);
                                    else if (!trimmedFaces.Contains(edg.PrimaryFace)) edg.RemoveFace(edg.PrimaryFace);
                                }
                                if (other.SecondaryFace != null)
                                {
                                    if (!trimmedFaces.Contains(other.SecondaryFace)) other.RemoveFace(other.SecondaryFace);
                                    else if (!trimmedFaces.Contains(other.PrimaryFace)) other.RemoveFace(other.PrimaryFace);
                                }
                                other.PrimaryFace.ReplaceEdge(other, edg);
                                trimmedEdges.Remove(dvk);
                            }
                        }
                        else
                        {
                            trimmedEdges[dvk] = edg;
                        }
                    }
                }
            }

#if DEBUG
            openTrimmedEdges = new Set<Edge>();
            foreach (Face fce in trimmedFaces)
            {
                foreach (Edge edg in fce.AllEdges)
                {
                    if (edg.SecondaryFace == null)
                    {
                        openTrimmedEdges.Add(edg);
                    }
                }
            }
#endif
            // All edges of trimmedFaces are connected to either other trimmedfaces or to remaining uncut faces of the two shells.
            // Collect all faces that are reachable from trimmedFaces
            Set<Face> allFaces = new Set<Face>(trimmedFaces);
            bool added = true;
            while (added)
            {
                added = false;
                foreach (Face fce in allFaces.Clone()) // use a clone to be able to add faces to allfaces in this foreach loop
                {
                    foreach (Edge edg in fce.AllEdgesIterated())
                    {
                        if (!allFaces.Contains(edg.PrimaryFace))
                        {
                            if (!discardedFaces.Contains(edg.PrimaryFace) && edg.IsOrientedConnection)
                            {
                                allFaces.Add(edg.PrimaryFace);
                                added = true;
                            }
                            else
                            {
                                edg.DisconnectFromFace(edg.PrimaryFace);
                            }
                        }
                        if (edg.SecondaryFace != null && !allFaces.Contains(edg.SecondaryFace))
                        {
                            if (!discardedFaces.Contains(edg.SecondaryFace) && edg.IsOrientedConnection)
                            {
                                allFaces.Add(edg.SecondaryFace);
                                added = true;
                            }
                            else
                            {
                                edg.DisconnectFromFace(edg.SecondaryFace);
                            }
                        }
                        else if (edg.SecondaryFace == null && !nonManifoldEdges.Contains(edg, precision))
                        {
                            Set<Edge> connecting = new Set<Edge>(Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2));
                            connecting.Remove(edg);
                            if (connecting.Count > 1)
                            {
                                Set<Edge> toRemove = new Set<Edge>();
                                foreach (Edge ce in connecting)
                                {
                                    if (!SameEdge(ce, edg, precision)) toRemove.Add(ce);
                                }
                                connecting.RemoveMany(toRemove);
                                foreach (Edge ce in connecting)
                                {
                                    if (ce.SecondaryFace == null && allFaces.Contains(ce.PrimaryFace))
                                    {   // edg is already connected with ce, but two different instances of the edge
                                        edg.MergeWith(ce);
                                        connecting.Clear();
                                        break;
                                    }
                                }
                            }
                            if (connecting.Count == 1) // if we have more than one possibility to connect, there is no criterion to decide which would be thr correct face
                                                       // so we hope to find the correct face with an other path.
                            {
                                Edge con = connecting.GetAny();
                                if (con.SecondaryFace == null && !discardedFaces.Contains(con.PrimaryFace) && SameEdge(con, edg, precision))
                                {   // this is an identical edge, which is not logically connected. This is probably an intersection which coincides with an existing edge.
                                    if (!allFaces.Contains(con.PrimaryFace))
                                    {
                                        allFaces.Add(con.PrimaryFace);
                                        added = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (allFaces.Count == 0 && cancelledfaces.Count > 0)
            {   // there were no intersections, only identical opposite faces, like when glueing two parts together
                // this remains empty in case of intersection and returns the full body in cas of union
                if (this.operation == Operation.union)
                {
                    foreach (Face fce in cancelledfaces) fce.DisconnectAllEdges();
                    allFaces.AddMany(shell1.Faces);
                    allFaces.AddMany(shell2.Faces);
                    allFaces.RemoveMany(cancelledfaces);
                }
            }
#if DEBUG
            foreach (Face fce in allFaces)
            {
                bool ok = fce.CheckConsistency();
            }
#endif

            // the following is probably only necessary when there were overlapping faces:
            // connect open edges in allFaces with each other
            Set<Edge> openEdges = new Set<Edge>();
            foreach (Face fce in allFaces)
            {
                foreach (Edge edg in fce.Edges)
                {
                    if (edg.SecondaryFace == null && !nonManifoldEdges.Contains(edg, precision)) openEdges.Add(edg);
                }
            }
            ConnectOpenEdges(openEdges);
            // What about "nonManifoldEdges"?
            // 
            List<Shell> res = new List<Shell>(); // the result of this method.
            // allfaces now contains all the trimmed faces plus the faces, which are (directly or indirectly) connected (via edges) to the trimmed faces
            List<Face> nonManifoldParts = new List<Face>();
            while (allFaces.Count > 0)
            {
                Set<Face> connected = extractConnectedFaces(allFaces, allFaces.GetAny());
                Shell shell = Shell.MakeShell(connected.ToArray());
#if DEBUG
                bool ok = shell.CheckConsistency();
#endif
                // res should not have open edges! If so, something went wrong
                if (shell.HasOpenEdgesEceptPoles())
                {
                    shell.TryConnectOpenEdges();
                }
                if (!shell.HasOpenEdgesEceptPoles())
                {
                    shell.CombineConnectedFaces(); // two connected faces which have the same surface are merged into one face
                    if (operation == Operation.union) shell.ReverseOrientation(); // both had been reversed and the intersection had been calculated
                    res.Add(shell);
                }
                else
                {
                    if (nonManifoldEdges.ContainsAll(shell.OpenEdges, precision))
                    {
                        nonManifoldParts.AddRange(shell.Faces);
                        shell = Shell.MakeShell(nonManifoldParts.ToArray());
                        shell.TryConnectOpenEdges();
                        if (!shell.HasOpenEdgesEceptPoles())
                        {
                            shell.CombineConnectedFaces(); // two connected faces which have the same surface are merged into one face
                            if (operation == Operation.union) shell.ReverseOrientation(); // both had been reversed and the intersection had been calculated
                            res.Add(shell);
                            nonManifoldParts.Clear();
                        }
                    }
                }
            }
            return res.ToArray();
        }

        private List<Edge> FindLoop(Edge edg, Vertex startVertex, Face onThisFace, Set<Edge> intersectionEdges, Set<Edge> originalEdges)
        {
            List<Edge> res = new List<Edge>();
            res.Add(edg);
            if (startVertex == null) startVertex = edg.StartVertex(onThisFace);
            Vertex endVertex = edg.EndVertex(onThisFace);
            Set<Vertex> usedVertices = new Set<Vertex>(); // to encounter inner loops
            usedVertices.Add(startVertex);
            while (!usedVertices.Contains(endVertex))
            {
                List<Edge> connected = endVertex.ConditionalEdges(delegate (Edge e)
                {
                    if (!intersectionEdges.Contains(e)) return false;
                    return e.StartVertex(onThisFace) == endVertex;
                });
                if (connected.Count > 1) // can intersection edges contain poles?
                {
                    // filter a pole:
                    for (int i = 0; i < connected.Count; i++)
                    {
                        if (connected[i].Curve3D == null)
                        {
                            res.Add(connected[i]); // insert a pole, usedVertices and endVertex stay the same
                            intersectionEdges.Remove(connected[i]); // so we don't find it again
                            connected.RemoveAt(i);
                            break;
                        }
                    }
                }
                if (connected.Count > 1)
                {   // very rare case:
                    // multiple intersection edges start from the current edge
                    // there should be only one valid path to the startVertex
                    List<Edge> toAdd = null;
                    for (int i = 0; i < connected.Count; i++)
                    {
                        List<Edge> sub = FindLoop(connected[i], startVertex, onThisFace, intersectionEdges, originalEdges);
                        if (sub != null)
                        {
                            if (toAdd != null) throw new ApplicationException("BRepOpration: cannot find loop"); // should never happen
                            toAdd = sub;
                        }
                    }
                    if (toAdd != null)
                    {
                        res.AddRange(toAdd);
                        endVertex = startVertex;
                        break; // we are done, the subLoop ends at startVertex
                    }
                    else
                    {
                        intersectionEdges.RemoveMany(res);
                        originalEdges.RemoveMany(res);
                        return null; // no path leads to the startVertex
                    }
                }
                else if (connected.Count == 1)
                {   // there is exactely one intersection edge starting at endVertex: use this edges
                    res.Add(connected[0]);
                    usedVertices.Add(endVertex);
                    endVertex = connected[0].EndVertex(onThisFace);
                    continue;
                }
                bool intersectionEdgeEndHere = false;
                bool lastEdgeIsOutline = originalEdges.Contains(res[res.Count - 1]);
                connected = endVertex.ConditionalEdges(delegate (Edge e)
                {
                    if (lastEdgeIsOutline && intersectionEdges.Contains(e) && e.EndVertex(onThisFace) == endVertex) intersectionEdgeEndHere = true; // there is an intersection edge ending at this current endvertex
                    if (!originalEdges.Contains(e)) return false;
                    return e.StartVertex(onThisFace) == endVertex;
                });
                if (connected.Count > 1)
                {
                    // filter a pole:
                    for (int i = 0; i < connected.Count; i++)
                    {
                        if (connected[i].Curve3D == null)
                        {
                            res.Add(connected[i]); // insert a pole, usedVertices and endVertex stay the same
                            intersectionEdges.Remove(connected[i]); // so we don't find it again
                            connected.RemoveAt(i);
                            break;
                        }
                    }
                }
                if (connected.Count == 0 || intersectionEdgeEndHere)
                {
                    // (connected.Count == 0): dead end, no connection at endVertex
                    // intersectionEdgeEndHere: cannot go on, because we are following original edges and crossing at a vertex, 
                    // where an intersection edge ends. This is not allowed (e.g.: breps4)
                    intersectionEdges.RemoveMany(res);
                    originalEdges.RemoveMany(res);
                    return null;
                }
                else if (connected.Count == 1)
                {
                    res.Add(connected[0]);
                    usedVertices.Add(endVertex);
                    endVertex = connected[0].EndVertex(onThisFace);
                }
                else
                {
                    throw new ApplicationException("BRepOpration: cannot find loop, too many connections"); // should never happen
                }
            }
            if (startVertex != endVertex)
            {
                // if we have encountered an inner loop: remove all edges at the beginning until we reach an edge starting at endVertex
                while (res.Count > 0 && res[0].StartVertex(onThisFace) != endVertex) res.RemoveAt(0);
            }
            intersectionEdges.RemoveMany(res);
            originalEdges.RemoveMany(res);
            return res;
        }

        //        private Shell[] TestNewApproachX()
        //        {
        //            // this method relies on
        //            // - faceToIntersectionEdges: contains all faces, which intersect with other faces as keys and all the intersection edges, 
        //            //   which are produced by other faces on this face.
        //            // - overlappingFaces and oppositeFaces: they contain pairs of faces, which overlap, with either same or oppostie orientation.
        //            // 
        //            // The main algorithm does the following: split (or trimm or cut) a face according to the intersection edges on this face. 
        //            // All edges are oriented, so it is possible to find outer edges and holes. A face might produce zero, one or multiple trimmed faces.
        //            // When all faces are trimmed, connect the trimmed faces with the untouched faces. this is the result.
        //            //
        //            // When there are overlapping faces, cut them into non-overlapping parts before the main algorithm.
        //            //
        //            // This algorithm does not rely on geometry (coordinates of points and curves) but only on topological information, excetp for
        //            // - finding, whether two edges connecting the same vertices, are equal (SameEdge) and
        //            // - checking, whether a 2d loop of edges is inside another 2d loop of edges


        //#if DEBUG   // show the starting position: dcFaces: all faces with hashCodes of both shells and their normals
        //            // dcs1e and dcs2e: the 3d edges and their hashCodes
        //            // dcis: all intersection edges. Here the edges must build one ore more closed curves. there can not be open ends.
        //            // If there are open ends, some intersection calculation failed!
        //            DebuggerContainer dcFaces = new CADability.DebuggerContainer();
        //            foreach (Face fce in shell1.Faces)
        //            {
        //                dcFaces.Add(fce.Clone(), fce.GetHashCode()); // die Faces werden kaputt gemacht, deshalb hier clones merken
        //                double ll = fce.GetExtent(0.0).Size * 0.01;
        //                ColorDef cd = new ColorDef("debug", System.Drawing.Color.Blue);
        //                SimpleShape ss = fce.Area;
        //                GeoPoint2D c = ss.GetExtent().GetCenter();
        //                GeoPoint pc = fce.Surface.PointAt(c);
        //                GeoVector nc = fce.Surface.GetNormal(c);
        //                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
        //                l.ColorDef = cd;
        //                dcFaces.Add(l);
        //            }
        //            foreach (Face fce in shell2.Faces)
        //            {
        //                dcFaces.Add(fce.Clone(), fce.GetHashCode()); // die Faces werden kaputt gemacht, deshalb hier clones merken
        //                double ll = fce.GetExtent(0.0).Size * 0.01;
        //                ColorDef cd = new ColorDef("debug", System.Drawing.Color.Brown);
        //                SimpleShape ss = fce.Area;
        //                GeoPoint2D c = ss.GetExtent().GetCenter();
        //                GeoPoint pc = fce.Surface.PointAt(c);
        //                GeoVector nc = fce.Surface.GetNormal(c);
        //                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
        //                l.ColorDef = cd;
        //                dcFaces.Add(l);
        //            }
        //            DebuggerContainer dcs1e = new CADability.DebuggerContainer();
        //            foreach (Edge edg in shell1.Edges)
        //            {
        //                if (edg.Curve3D != null) dcs1e.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
        //            }
        //            DebuggerContainer dcs2e = new CADability.DebuggerContainer();
        //            foreach (Edge edg in shell2.Edges)
        //            {
        //                if (edg.Curve3D != null) dcs2e.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
        //            }
        //            DebuggerContainer dcis = new CADability.DebuggerContainer();
        //            Set<Edge> ise = new Set<Edge>();
        //            foreach (KeyValuePair<Face, Set<Edge>> item in faceToIntersectionEdges)
        //            {
        //                ise.AddMany(item.Value);
        //            }
        //            foreach (Edge edg in ise)
        //            {
        //                if (edg.Curve3D != null) dcis.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
        //            }
        //            Dictionary<Face, DebuggerContainer> debugTrimmedFaces = new Dictionary<Face, DebuggerContainer>();
        //            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
        //            {
        //                debugTrimmedFaces[kv.Key] = new DebuggerContainer();
        //                debugTrimmedFaces[kv.Key].Add(kv.Key.Clone(), System.Drawing.Color.Black, kv.Key.GetHashCode());
        //                int dbgclr = 1;
        //                foreach (Edge edg in kv.Value)
        //                {
        //                    Face other = edg.OtherFace(kv.Key);
        //                    debugTrimmedFaces[kv.Key].Add(other.Clone() as Face, DebuggerContainer.FromInt(dbgclr++), other.GetHashCode());
        //                }
        //            }
        //            Dictionary<Face, GeoObjectList> faceToMixedEdgesDebug = new Dictionary<Face, GeoObjectList>();
        //            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
        //            {
        //                GeoObjectList l = new GeoObjectList();
        //                faceToMixedEdgesDebug[kv.Key] = l;
        //                l.Add(kv.Key);
        //                foreach (Edge edg in kv.Value)
        //                {
        //                    if (edg.Curve3D != null)
        //                    {
        //                        if (edg.Forward(kv.Key)) l.Add(edg.Curve3D as IGeoObject);
        //                        else
        //                        {
        //                            ICurve c3d = edg.Curve3D.Clone();
        //                            c3d.Reverse();
        //                            l.Add(c3d as IGeoObject);
        //                        }
        //                    }
        //                }
        //            }
        //#endif
        //#if DEBUG
        //            Dictionary<Face, DebuggerContainer> dbgFaceTointersectionEdges = new Dictionary<Face, DebuggerContainer>();
        //            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
        //            {
        //                DebuggerContainer dc = new DebuggerContainer();
        //                dbgFaceTointersectionEdges[kv.Key] = dc;
        //                int dbgc = 0;
        //                double arrowSize = kv.Key.Area.GetExtent().Size * 0.02;
        //                dc.Add(kv.Value, kv.Key, arrowSize, System.Drawing.Color.Red, 0);
        //                dc.Add(kv.Key.AllEdgesIterated(), kv.Key, arrowSize, System.Drawing.Color.Blue, 0);
        //            }
        //#endif
        //            Set<Face> discardedFaces = new Set<Face>(faceToIntersectionEdges.Keys); // these faces may not apper in the final result, because they will be trimmed
        //            // ReduceOverlappingFaces(discardedFaces); // ReduceOverlappingFaces may remove some faces from faceToIntersectionEdges.Keys and add new ones
        //            Set<Face> trimmedFaces = new Set<Face>(); // collection of faces which are trimmed (splitted, cut, edged) during this process
        //            faceToCommonFaces = new Dictionary<Face, Set<Face>>();
        //            Set<Face> overlappingCommonFaces = CollectOverlappingCommonFaces(discardedFaces); // same oriented overlapping faces yield their common parts
        //            Set<Face> oppositeCommonFaces = CollectOppositeCommonFaces(discardedFaces); // same oriented overlapping faces yield their common parts
        //            trimmedFaces.AddMany(overlappingCommonFaces); // these are part of the result
        //            SubtractCommonFaces(oppositeCommonFaces); // if there are common faces, remove them first, they might produce ambiguous intersection connections
        //#if DEBUG
        //            DebuggerContainer dcif = new DebuggerContainer();
        //            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
        //            {
        //                dcif.Add(kv.Key, kv.Key.GetHashCode());
        //                foreach (Edge edg in kv.Value)
        //                {
        //                    dcif.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
        //                }
        //            }
        //            Dictionary<Face, DebuggerContainer> dbgEdgePositions = new Dictionary<Face, DebuggerContainer>();
        //            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
        //            {
        //                DebuggerContainer dc = new DebuggerContainer();
        //                dbgEdgePositions[kv.Key] = dc;
        //                int dbgc = 0;
        //                double arrowSize = kv.Key.Area.GetExtent().Size * 0.02;
        //                dc.Add(kv.Value, kv.Key, arrowSize, System.Drawing.Color.Red, 0);
        //                dc.Add(kv.Key.AllEdgesIterated(), kv.Key, arrowSize, System.Drawing.Color.Blue, 0);
        //            }
        //#endif
        //            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
        //            {   // faceToIntersectionEdges contains all faces, which are intersected by faces of the relative other shell, as well as those intersection edges
        //                Face faceToSplit = kv.Key;
        //#if DEBUG       // show the faceToSplit and all other faces, which caused the intersectionEdges
        //                // does not work for overlapping faces
        //                debugTrimmedFaces.TryGetValue(kv.Key, out DebuggerContainer dcInvolvedFaces);
        //#endif
        //                Set<Edge> intersectionEdges = kv.Value.Clone();
        //                if (intersectionEdges.Count == 0)
        //                {   // this is probably a remaining part of an overlapping face, there is nothing to do
        //                    trimmedFaces.Add(kv.Key.CloneWithVertices());
        //                    kv.Key.DisconnectAllEdges(); // we now use the clone and destroy the original
        //                    continue;
        //                }
        //                Set<Vertex> faceVertices = new Set<Vertex>(faceToSplit.Vertices);
        //                // some intersection edges are created twice (e.g. when an edge fo shell2 is contained in a face of shell1)
        //                // if the duplicates have the same orientation, discard one of the edges, if they have opposide direction, discard both
        //                Dictionary<Pair<Vertex, Vertex>, Edge> avoidDuplicates = new Dictionary<Pair<Vertex, Vertex>, Edge>();
        //                Dictionary<Pair<Vertex, Vertex>, Edge> avoidOriginalEdges = new Dictionary<Pair<Vertex, Vertex>, Edge>();
        //                foreach (Edge edg in faceToSplit.AllEdgesIterated())
        //                {
        //                    Pair<Vertex, Vertex> k = new Pair<Vertex, Vertex>(edg.StartVertex(faceToSplit), edg.EndVertex(faceToSplit));
        //                    avoidOriginalEdges[k] = edg;
        //                }
        //                foreach (Edge edg in kv.Value)
        //                {
        //                    Pair<Vertex, Vertex> k = new Pair<Vertex, Vertex>(edg.StartVertex(faceToSplit), edg.EndVertex(faceToSplit));
        //                    Pair<Vertex, Vertex> k1 = new Pair<Vertex, Vertex>(k.Second, k.First);
        //                    if (avoidDuplicates.ContainsKey(k) && SameEdge(avoidDuplicates[k], edg, precision))
        //                    {
        //                        intersectionEdges.Remove(edg); // this is a duplicate edge. It is probably also an original edge
        //                                                       // it is arbitrary, which intersection edge will be removed.
        //                        edg.DisconnectFromFace(faceToSplit); // disconnecting is important, because of the vertex->edge->face references
        //                    }
        //                    else if (avoidDuplicates.ContainsKey(k1) && SameEdge(avoidDuplicates[k1], edg, precision))
        //                    {   // Reverse duplicates: remove both intersection edges
        //                        intersectionEdges.Remove(edg);
        //                        intersectionEdges.Remove(avoidDuplicates[k1]);
        //                        edg.DisconnectFromFace(faceToSplit);
        //                        avoidDuplicates[k1].DisconnectFromFace(faceToSplit);
        //                    }
        //                    else
        //                    {
        //                        // when this intersection edge is identical to an original edge of the faceToSplit, we remove the intersection edge
        //                        // we need this to avoid multiple intersection connections starting from the same common vertex
        //                        if (avoidOriginalEdges.ContainsKey(k) && SameEdge(avoidOriginalEdges[k], edg, precision) && avoidOriginalEdges[k].StartVertex(faceToSplit) == edg.StartVertex(faceToSplit))
        //                        {
        //                            intersectionEdges.Remove(edg);
        //                            edg.DisconnectFromFace(faceToSplit);
        //                        }
        //                        else
        //                        {
        //                            avoidDuplicates[k] = edg;
        //                        }
        //                    }
        //                }
        //                // TEST: make a situation, where one face (of shell1) has both a same oriented and an opposite oriented overlapping face from shell2.
        //                // This is hard to imagine, but possible. Maybe this will make a problem

        //                if (intersectionEdges.Count == 0)
        //                {
        //                    // there are no intersection edges at all, probably only original edges, which have been split
        //                    // we skip this face, but maybe we will need it for the result, so we mark it as usable, not destroyed
        //                    trimmedFaces.Add(kv.Key.CloneWithVertices()); // adding because of breps4.cdb
        //                    kv.Key.DisconnectAllEdges(); // we now use the clone and destroy the original
        //                    // discardedFaces.Remove(kv.Key);
        //                    continue;
        //                }

        //                Set<Vertex> intersectionVertices = new Set<Vertex>(); // all vertices of the intersection edges
        //                foreach (Edge edg in intersectionEdges)
        //                {
        //                    intersectionVertices.Add(edg.Vertex1);
        //                    intersectionVertices.Add(edg.Vertex2);
        //                }
        //                Set<Edge> faceEdges = new Set<Edge>(faceToSplit.AllEdgesSet); // all outline edges and holes of the face, used edges will be removed
        //                Set<Vertex> commonVertices = faceVertices.Intersection(intersectionVertices);
        //                // commonVertices: vertices for both intersection edges and outline edges
        //                // connections: this are ordered lists of connected edges in the topological sense of the face (outline ccw, hole cw)
        //                // they are collected in a dictionary, where the start vertex of the connection is the key
        //                // each vertex may only have contact with max. 2 intersection edges. If it contacts more, it must be added to the common vertices
        //                foreach (Vertex ivtx in intersectionVertices)
        //                {
        //                    if (ivtx.AllEdges.Intersection(intersectionEdges).Count > 2)
        //                    {
        //                        commonVertices.Add(ivtx); // this is a inner connection point of omre than two intersection edges (very rare)
        //                    }
        //                }
        //#if DEBUG
        //                DebuggerContainer dcie = new DebuggerContainer();
        //                foreach (Edge edg in intersectionEdges)
        //                {
        //                    ICurve2D c2d = edg.Curve2D(faceToSplit);
        //                    dcie.Add(c2d, System.Drawing.Color.Red, edg.GetHashCode());
        //                }
        //                foreach (Edge edg in faceToSplit.AllEdgesIterated())
        //                {
        //                    ICurve2D c2d = edg.Curve2D(faceToSplit);
        //                    dcie.Add(c2d, System.Drawing.Color.Blue, edg.GetHashCode());
        //                }
        //#endif
        //                Dictionary<Vertex, List<Edge>> orgConnections = new Dictionary<Vertex, List<Edge>>();
        //                Dictionary<Vertex, List<Edge>> intsConnections = new Dictionary<Vertex, List<Edge>>();
        //                // find all connections between common vertices
        //                foreach (Vertex vtx in commonVertices)
        //                {
        //                    List<Edge> connection = faceToSplit.FindConnection(vtx, commonVertices);
        //                    if (connection.Count > 0) // which must always be the case
        //                    {
        //                        //if (connection[0].StartVertex(faceToSplit) != connection[connection.Count - 1].EndVertex(faceToSplit))
        //                        //{   // we do not want closed connections here: these are loops in the faceToSplit's outline
        //                        orgConnections[vtx] = connection;
        //                        //}
        //                    }
        //                    do
        //                    {   // there may be more than one connection of intersectionEdges starting at the same vertex
        //                        // this is a big problem. How to deal with it?????
        //                        connection = Vertex.FindConnection(vtx, intersectionEdges, commonVertices, faceToSplit);
        //                        if (connection.Count > 0) // which may or may not be the case
        //                        {
        //                            intsConnections[vtx] = connection;
        //                            intersectionEdges.RemoveMany(connection);
        //                        }
        //                        else
        //                        {
        //                            break;
        //                        }
        //                    } while (intersectionEdges.Count > 0);
        //                }
        //#if DEBUG       // show all connections with their directions in uv of the faceToSplit
        //                DebuggerContainer dcConnections = new DebuggerContainer();
        //                double arrowSize = faceToSplit.Area.GetExtent().Size * 0.02;
        //                int dbgn = 0;
        //                foreach (KeyValuePair<Vertex, List<Edge>> item in intsConnections)
        //                {
        //                    dcConnections.Add(item.Value, faceToSplit, arrowSize, System.Drawing.Color.Red, dbgn++);
        //                }
        //                dbgn = 0;
        //                foreach (KeyValuePair<Vertex, List<Edge>> item in orgConnections)
        //                {
        //                    dcConnections.Add(item.Value, faceToSplit, arrowSize, System.Drawing.Color.Blue, dbgn++);
        //                }
        //#endif
        //                // Now we have connections (list of cosecutive connected edges) of the faces outline and of intersection edges
        //                // that connect vertices which have both outline and intersection vertices.
        //                // This should work for all cases except vertices were two intersections start and two intersections end.
        //                // Maybe we should split such vertices into two independant vertices at the beginning (which must still be implemented!)

        //                // Now original connections which are (geometrically) identical (but may be differently splitted into parts) to intersection connections will be removed.
        //                // When original connections which are geometrically reverse to intersection connections, both will be removed.
        //                List<Vertex> startVertices = new List<Vertex>(intsConnections.Keys);
        //                foreach (Vertex vtx in startVertices)
        //                {
        //                    List<Edge> intsConnection = intsConnections[vtx];
        //                    Vertex intsEdnVertex = intsConnection[intsConnection.Count - 1].EndVertex(faceToSplit);
        //                    List<Edge> connection;
        //                    if (orgConnections.TryGetValue(vtx, out connection))
        //                    {
        //                        Vertex ordEdnVertex = connection[connection.Count - 1].EndVertex(faceToSplit);
        //                        if (ordEdnVertex == intsEdnVertex)
        //                        {
        //                            // we have two connections, one with intersection edges, the other with original edges of the faceToSplit, which
        //                            // connect the same vertices. If they are (geometrically) identical, remove the orginal
        //                            if (SameConnection(intsConnection, connection, faceToSplit, precision))
        //                            {
        //                                orgConnections.Remove(vtx);
        //                            }
        //                        }
        //                    }
        //                    if (orgConnections.TryGetValue(intsEdnVertex, out connection))
        //                    {
        //                        Vertex orgEndVertex = connection[connection.Count - 1].EndVertex(faceToSplit);
        //                        if (orgEndVertex == vtx)
        //                        {
        //                            // we have two connections, one with intersection edges, the other with original edges of the faceToSplit, which
        //                            // connect the same vertices but in reverse order. If they are (geometrically) identical, remove both the orginal and the intersection
        //                            if (SameConnection(intsConnection, connection, faceToSplit, precision))
        //                            {
        //                                orgConnections.Remove(intsEdnVertex);
        //                                foreach (Edge edg in intsConnection)
        //                                {
        //                                    edg.DisconnectFromFace(faceToSplit); // they will not be used for trimmed faces
        //                                                                         // disconnecting is important, because an edge, which has no more faces, 
        //                                                                         // should not be reachable by its vertices
        //                                }
        //                                intsConnections.Remove(vtx);
        //                                intersectionEdges.RemoveMany(intsConnection);
        //                            }
        //                        }
        //                    }
        //                }
        //#if DEBUG       // show the important connections (intersection: red, original: blue) and the cancelled edges (original: cyan, intersection: yellow)
        //                DebuggerContainer dcRemaining = new DebuggerContainer();
        //                int dbgc = 0;
        //                arrowSize = faceToSplit.Area.GetExtent().Size * 0.02;
        //                foreach (KeyValuePair<Vertex, List<Edge>> item in intsConnections)
        //                {
        //                    dcRemaining.Add(item.Value, faceToSplit, arrowSize, System.Drawing.Color.Red, dbgc++);
        //                }
        //                foreach (KeyValuePair<Vertex, List<Edge>> item in orgConnections)
        //                {
        //                    dcRemaining.Add(item.Value, faceToSplit, arrowSize, System.Drawing.Color.Blue, dbgc++);
        //                }
        //                //dcRemaining.Add(faceEdges, faceToSplit, arrowSize, System.Drawing.Color.Cyan, -2);
        //                dcRemaining.Add(intersectionEdges, faceToSplit, arrowSize, System.Drawing.Color.DarkTurquoise, -1);

        //#endif
        //                UniqueDoubleReverseDictionary<Pair<List<Edge>, ICurve2D[]>> loops = new UniqueDoubleReverseDictionary<Pair<List<Edge>, ICurve2D[]>>();
        //                // loops: all loops for the trimmed face, (reverse-) sorted by size of 2d area (biggest positive first). 
        //                // No problem with exactely same area (*Unique*DoubleReverseDictionary).
        //                // We need the array of 2d curves multiple times, so keep it as second part of the pair. 
        //                while (intsConnections.Count > 0)
        //                {
        //                    KeyValuePair<Vertex, List<Edge>> first = intsConnections.First();
        //                    Vertex startVertex = first.Key;
        //                    List<Edge> loop = new List<Edge>();
        //                    Set<Vertex> connectingVertices = new Set<Vertex>(); // the points where the connection parts are connected together
        //                    List<Vertex> duplicateVertices = new List<Vertex>(); // this is to find (rare) self touching vertices
        //                    // start with a connection of intersection edges and follow with more connections until the start vertex is reached
        //                    // i.e. the loop is closed. Always prefer intersection connections to original connections
        //                    loop.AddRange(first.Value);
        //                    intsConnections.Remove(startVertex);
        //                    connectingVertices.Add(startVertex);
        //                    Vertex endVertex = loop[loop.Count - 1].EndVertex(faceToSplit);
        //                    bool openEnd = false; // if a non closed part was found
        //                    Dictionary<Vertex, List<Edge>> intsConnectionsBackup = new Dictionary<Vertex, List<Edge>>();
        //                    Dictionary<Vertex, List<Edge>> orgConnectionsBackup = new Dictionary<Vertex, List<Edge>>();
        //                    while (endVertex != startVertex)
        //                    {
        //                        List<Edge> part;
        //                        if (intsConnections.TryGetValue(endVertex, out part))
        //                        {
        //                            loop.AddRange(part);
        //                            intsConnectionsBackup[endVertex] = intsConnections[endVertex];
        //                            intsConnections.Remove(endVertex);
        //                            if (connectingVertices.Contains(endVertex)) duplicateVertices.Add(endVertex);
        //                            else connectingVertices.Add(endVertex);
        //                            endVertex = loop[loop.Count - 1].EndVertex(faceToSplit);
        //                        }
        //                        else if (orgConnections.TryGetValue(endVertex, out part))
        //                        {
        //                            loop.AddRange(part);
        //                            orgConnectionsBackup[endVertex] = orgConnections[endVertex];
        //                            orgConnections.Remove(endVertex);
        //                            if (connectingVertices.Contains(endVertex)) duplicateVertices.Add(endVertex);
        //                            else connectingVertices.Add(endVertex);
        //                            endVertex = loop[loop.Count - 1].EndVertex(faceToSplit);
        //                        }
        //                        else
        //                        {
        //                            // couldn't close a loop
        //                            // restore the used connections. the first intsConnection will not be restored, because it is not a candidate for a closed loop.
        //                            // The while loop will finish, because the first connection is not restored
        //                            // I think, this should not happen with topological clean faces
        //                            foreach (KeyValuePair<Vertex, List<Edge>> ints in intsConnectionsBackup)
        //                            {
        //                                intsConnections[ints.Key] = ints.Value;
        //                            }
        //                            foreach (KeyValuePair<Vertex, List<Edge>> org in orgConnectionsBackup)
        //                            {
        //                                orgConnections[org.Key] = org.Value;
        //                            }
        //                            openEnd = true;
        //                            break;
        //                        }
        //                    }
        //                    if (!openEnd)
        //                    {
        //                        // in rare cases the loop may be self-touching: 
        //                        if (duplicateVertices.Count > 0)
        //                        {   // a vertex is beeing used multiple times:
        //                            // these are selft-touching positions: split the loop at these vertices
        //                            for (int i = 0; i < duplicateVertices.Count; i++)
        //                            {
        //                                int startHere = -1, endHere = -1;
        //                                for (int j = 0; j < loop.Count; j++)
        //                                {
        //                                    if (loop[j].StartVertex(faceToSplit) == duplicateVertices[i])
        //                                    {
        //                                        if (startHere < 0) startHere = j;
        //                                        else
        //                                        {
        //                                            endHere = j;
        //                                            break;
        //                                        }
        //                                    }
        //                                }
        //                                if (endHere > 0)
        //                                {
        //                                    List<Edge> subloop = loop.GetRange(startHere, endHere - startHere);
        //                                    ICurve2D[] c2ds = faceToSplit.Get2DCurves(subloop);
        //                                    loops.AddUnique(Border.sArea(c2ds), new Pair<List<Edge>, ICurve2D[]>(subloop, c2ds));
        //                                    loop.RemoveRange(startHere, endHere - startHere);
        //                                }
        //                            }
        //                            if (loop.Count > 0) // which should always be the case
        //                            {
        //                                ICurve2D[] c2ds = faceToSplit.Get2DCurves(loop);
        //                                loops.AddUnique(Border.sArea(c2ds), new Pair<List<Edge>, ICurve2D[]>(loop, c2ds));
        //                            }
        //                        }
        //                        else
        //                        {
        //                            ICurve2D[] c2ds = faceToSplit.Get2DCurves(loop);
        //                            loops.AddUnique(Border.sArea(c2ds), new Pair<List<Edge>, ICurve2D[]>(loop, c2ds));
        //                        }
        //                    }
        //                }

        //                // loops contains all loops in which both intersection and original edges are involved. There maybe loops consisting only of intersection edges
        //                // We have to collect those also:
        //                while (intersectionEdges.Count > 0)
        //                {
        //                    Edge startEdg = intersectionEdges.GetAny();
        //                    List<Edge> loop = startEdg.FindLoop(intersectionEdges, faceToSplit);
        //                    intersectionEdges.RemoveMany(loop);
        //                    if (loop.Count > 0 && loop[0].StartVertex(faceToSplit) == loop[loop.Count - 1].EndVertex(faceToSplit))
        //                    {   // add only closed loops
        //                        ICurve2D[] c2ds = faceToSplit.Get2DCurves(loop);
        //                        loops.AddUnique(Border.sArea(c2ds), new Pair<List<Edge>, ICurve2D[]>(loop, c2ds));
        //                    }
        //                }

        //                // If there is no positive loop, the outline loop of the face must be available and must be used.
        //                // actually we would have to topologically sort the loops in order to decide whether the faces outline must be used or not.
        //                // and what about the untouched holes of the face?
        //                double biggestArea = 0.0;
        //                foreach (double a in loops.Keys)
        //                {
        //                    if (Math.Abs(a) > Math.Abs(biggestArea)) biggestArea = a;
        //                }
        //                if (biggestArea < 0) // when no loop, we don't need the outline
        //                {
        //                    foreach (Pair<List<Edge>, ICurve2D[]> item in loops.Values) faceEdges.RemoveMany(item.First);
        //                    if (faceEdges.ContainsAll(faceToSplit.OutlineEdges))
        //                    {
        //                        // there is no outline loop, only holes (or nothing). We have to use the outline loop of the face, which is not touched
        //                        List<Edge> outline = new List<Edge>(faceToSplit.OutlineEdges);
        //                        ICurve2D[] c2ds = faceToSplit.Get2DCurves(outline);
        //                        loops.AddUnique(Border.sArea(c2ds), new Pair<List<Edge>, ICurve2D[]>(outline, c2ds));
        //                        faceEdges.RemoveMany(outline); // we would not need that
        //                    }
        //                }
        //                if (loops.Count == 0)
        //                {
        //                    // probably the only intersection edges are reverse to original edges, i.e. intersection only exactely on already existing edges
        //                    // but with reverse direction: no trimmed face is created and this face will not be used in the result
        //                    continue;
        //                }
        //                // we also add the holes of the faceToSplit, as long as it was not used by intersections and is not enclosed by a bigger hole
        //                // created from intersection edges
        //                for (int i = 0; i < faceToSplit.HoleCount; i++)
        //                {
        //                    if (faceEdges.ContainsAll(faceToSplit.HoleEdges(i)))
        //                    {
        //                        List<Edge> hole = new List<Edge>(faceToSplit.HoleEdges(i));
        //                        ICurve2D[] c2ds = faceToSplit.Get2DCurves(hole);
        //                        // now check, wether this hole is enclosed by another hole
        //                        double area = Border.sArea(c2ds); // the negative area of this hole
        //                        GeoPoint2D testPoint = c2ds[0].StartPoint; // some point on this loop to test, wether this loop is not enclosed by another hole
        //                        bool enclosedByHole = false;
        //                        foreach (var loop in loops)
        //                        {
        //                            if (loop.Key < area) // an enclosing hole must be bigger, i.e. its area, which is negative, must be smaller (more negative)
        //                            {
        //                                if (!Border.IsInside(loop.Value.Second, testPoint))
        //                                {
        //                                    enclosedByHole = true;
        //                                    break;
        //                                }
        //                            }
        //                            if (enclosedByHole) break;
        //                        }
        //                        if (!enclosedByHole)
        //                        {
        //                            // in order to use a hole, it must be contained in a outer, positive loop
        //                            bool isContainedInOutline = false;
        //                            foreach (var loop in loops)
        //                            {
        //                                if (loop.Key > 0) // an outline
        //                                {
        //                                    if (Border.IsInside(loop.Value.Second, testPoint))
        //                                    {
        //                                        isContainedInOutline = true;
        //                                        break;
        //                                    }
        //                                }
        //                                if (isContainedInOutline) break;
        //                            }
        //                            if (isContainedInOutline) loops.AddUnique(Border.sArea(c2ds), new Pair<List<Edge>, ICurve2D[]>(hole, c2ds));
        //                        }
        //                        faceEdges.RemoveMany(hole); // we would not need that
        //                    }
        //                }
        //                // Now all necessary loops are created. There is one or more outline (ccw) and zero or more holes
        //                // If we have more than one outline, we have to match the holes to their enclosing outline
        //                double[] areas = new double[loops.Count];
        //                Edge[][] edgeLoop = new Edge[loops.Count][];
        //                ICurve2D[][] loops2D = new ICurve2D[loops.Count][];
        //                loops.Keys.CopyTo(areas, 0);
        //                int ii = 0;
        //                foreach (Pair<List<Edge>, ICurve2D[]> item in loops.Values)
        //                {
        //                    edgeLoop[ii] = item.First.ToArray();
        //                    loops2D[ii] = item.Second;
        //                    ++ii;
        //                }
        //                List<List<int>> outlineToHoles = new List<List<int>>(); // for each outline a list (of indices) of the corresponding holes
        //                int numOutlines = 0;
        //                for (int i = 0; i < areas.Length; i++)
        //                {
        //                    if (areas[i] > 0) // an outline
        //                    {
        //                        ++numOutlines;
        //                        outlineToHoles.Add(new List<int>()); // holes go here
        //                    }
        //                    else // a hole
        //                    {
        //                        // since area is sorted, all outlines are now given
        //                        if (numOutlines == 1) outlineToHoles[0].Add(i);
        //                        else
        //                        {
        //                            for (int j = 0; j < outlineToHoles.Count; j++)
        //                            {
        //                                if (Border.IsInside(loops2D[j], loops2D[i][0].StartPoint))
        //                                {
        //                                    outlineToHoles[j].Add(i);
        //                                    break;
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //#if DEBUG       // show the loops that create the new face(s)
        //                DebuggerContainer dcLoops = new DebuggerContainer();
        //                // Show all loops, beginning with biggest loop
        //                dbgc = 0;
        //                foreach (var item in loops)
        //                {
        //                    foreach (var loop in loops.Values)
        //                    {
        //                        dcLoops.Add(loop.Second, arrowSize, DebuggerContainer.FromInt(dbgc), dbgc);
        //                    }
        //                    ++dbgc;
        //                }
        //#endif
        //                // each outline (only one in most cases) creates a new Face. 
        //                for (int i = 0; i < numOutlines; i++)
        //                {
        //                    // maybe the new face is identical to one of the commonFaces
        //                    if (overlappingCommonFaces.Count > 0 || oppositeCommonFaces.Count > 0)
        //                    {
        //                        Set<Vertex> vertices = new Set<Vertex>(); // all vertices of the face to be created
        //                        Set<Edge> allEdges = new Set<Edge>();
        //                        foreach (Edge edg in edgeLoop[i])
        //                        {
        //                            allEdges.Add(edg);
        //                            vertices.Add(edg.Vertex1);
        //                            vertices.Add(edg.Vertex2);
        //                        }
        //                        for (int j = 0; j < outlineToHoles[i].Count; j++)
        //                        {
        //                            foreach (Edge edg in edgeLoop[outlineToHoles[i][j]])
        //                            {
        //                                allEdges.Add(edg);
        //                                vertices.Add(edg.Vertex1);
        //                                vertices.Add(edg.Vertex2);
        //                            }
        //                        }
        //                        bool faceIsCommonFace = false;
        //                        foreach (Face fce in Enumerable.Concat(overlappingCommonFaces, oppositeCommonFaces))
        //                        {
        //                            if (IsSameFace(allEdges, vertices, fce, precision))
        //                            {
        //                                faceIsCommonFace = true;
        //                                break;
        //                            }
        //                        }
        //                        if (faceIsCommonFace) continue; // don't create this face, it already exists as a common face
        //                    }
        //                    Face fc = Face.Construct();
        //                    Edge[][] holes = new Edge[outlineToHoles[i].Count][]; // corresponding list of holes to outline number i
        //                    for (int j = 0; j < outlineToHoles[i].Count; j++)
        //                    {
        //                        holes[j] = edgeLoop[outlineToHoles[i][j]];
        //                        foreach (Edge edg in holes[j])
        //                        {
        //                            edg.ReplaceOrAddFace(faceToSplit, fc);
        //                        }
        //                    }
        //                    foreach (Edge edg in edgeLoop[i])
        //                    {
        //                        edg.ReplaceOrAddFace(faceToSplit, fc);
        //                    }
        //                    fc.Set(faceToSplit.Surface.Clone(), edgeLoop[i], holes); // we need a clone of the surface because two independant faces shall not have the identical surface
        //                    fc.CopyAttributes(faceToSplit);
        //#if DEBUG
        //                    System.Diagnostics.Debug.Assert(fc.CheckConsistency());
        //#endif
        //                    trimmedFaces.Add(fc);
        //                }
        //            }

        //            // Now trimmedFaces contains all faces which are cut by faces of the relative other shell, even those, where the other shell cuts 
        //            // exactely along existing edges and nothing has been created.
        //            // The faces, which have been cut, i.e. faceToIntersectionEdges.Keys, are invalid now, we disconnect all egdes from these faces
        //            foreach (Face fce in discardedFaces)
        //            {
        //                foreach (Edge edg in fce.AllEdges)
        //                {
        //                    edg.DisconnectFromFace(fce);
        //                }
        //            }
        //#if DEBUG   // show all trimmed faces
        //            DebuggerContainer cdTrimmedFaces = new DebuggerContainer();
        //            foreach (Face fce in trimmedFaces)
        //            {
        //                cdTrimmedFaces.Add(fce.Clone(), fce.GetHashCode());
        //            }
        //            Set<Edge> openTrimmedEdges = new Set<Edge>();
        //            foreach (Face fce in trimmedFaces)
        //            {
        //                foreach (Edge edg in fce.AllEdges)
        //                {
        //                    if (edg.SecondaryFace == null)
        //                    {
        //                        openTrimmedEdges.Add(edg);
        //                    }
        //                }
        //            }
        //#endif
        //            // to avoid oppositeCommonFaces to be connected with the trimmedFaces, we destroy these faces
        //            foreach (Face fce in oppositeCommonFaces) fce.DisconnectAllEdges();
        //            foreach (Face fce in discardedFaces) fce.DisconnectAllEdges(); // to avoid connecting with discardedFaces
        //            // if we have two open edges in the trimmed faces which are identical, connect then
        //            Dictionary<DoubleVertexKey, Edge> trimmedEdges = new Dictionary<DoubleVertexKey, Edge>();
        //            foreach (Face fce in trimmedFaces)
        //            {
        //                foreach (Edge edg in fce.AllEdges)
        //                {
        //                    if (edg.SecondaryFace == null || !trimmedFaces.Contains(edg.SecondaryFace) || !trimmedFaces.Contains(edg.PrimaryFace))
        //                    {   // only those edges, which 
        //                        DoubleVertexKey dvk = new DoubleVertexKey(edg.Vertex1, edg.Vertex2);
        //                        if (trimmedEdges.TryGetValue(dvk, out Edge other))
        //                        {
        //                            if (other == edg) continue;
        //                            if (SameEdge(edg, other, precision))
        //                            {
        //                                if (edg.SecondaryFace != null)
        //                                {
        //                                    if (!trimmedFaces.Contains(edg.SecondaryFace)) edg.RemoveFace(edg.SecondaryFace);
        //                                    else if (!trimmedFaces.Contains(edg.PrimaryFace)) edg.RemoveFace(edg.PrimaryFace);
        //                                }
        //                                if (other.SecondaryFace != null)
        //                                {
        //                                    if (!trimmedFaces.Contains(other.SecondaryFace)) other.RemoveFace(other.SecondaryFace);
        //                                    else if (!trimmedFaces.Contains(other.PrimaryFace)) other.RemoveFace(other.PrimaryFace);
        //                                }
        //                                other.PrimaryFace.ReplaceEdge(other, edg);
        //                                trimmedEdges.Remove(dvk);
        //                            }
        //                        }
        //                        else
        //                        {
        //                            trimmedEdges[dvk] = edg;
        //                        }
        //                    }
        //                }
        //            }
        //            // the faces of trimmedFaces are either connected to other faces from trimmedFaces or to uncut faces from the original shells.
        //            // If there were duplicate intersection edges, this might result in open edges (secondaryFace==null) which should appear in pairs
        //            // and are combined by the following
        //            foreach (Face fce in trimmedFaces)
        //            {
        //                foreach (Edge edg in fce.AllEdges)
        //                {
        //                    if (edg.SecondaryFace == null)
        //                    {
        //                        Set<Edge> connecting = Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2) as Set<Edge>;
        //                        connecting.Remove(edg); // self
        //                        bool connected = false; // first try to connect with other trimmed face before connectiong with untrimmed original faces
        //                        foreach (Edge ce in connecting)
        //                        {
        //                            if (ce.SecondaryFace == null && trimmedFaces.Contains(ce.PrimaryFace) && SameEdge(edg, ce, precision))
        //                            {
        //                                ce.PrimaryFace.ReplaceEdge(ce, edg);
        //#if DEBUG
        //                                ce.PrimaryFace.CheckConsistency();
        //                                fce.CheckConsistency();
        //                                edg.SecondaryFace.CheckConsistency();
        //#endif
        //                                connected = true;
        //                            }
        //                        }
        //                        if (!connected)
        //                        {
        //                            //                            foreach (Edge ce in connecting)
        //                            //                            {
        //                            //                                if (ce.SecondaryFace == null && !discardedFaces.Contains(ce.PrimaryFace) && edg.Forward(edg.PrimaryFace) != ce.Forward(ce.PrimaryFace) && SameEdge(edg, ce, precision) && ce.PrimaryFace.OutlineEdges != null)
        //                            //                                {   // two matching open edges, orientation is also checked, no "möbius" connection!
        //                            //                                    if (edg.PrimaryFace != null && edg.SecondaryFace != null && edg.PrimaryFace != ce.PrimaryFace && edg.SecondaryFace != ce.PrimaryFace) continue; // cannot be replaced
        //                            //                                    ce.PrimaryFace.ReplaceEdge(ce, edg);
        //                            //#if DEBUG
        //                            //                                    ce.PrimaryFace.CheckConsistency();
        //                            //                                    fce.CheckConsistency();
        //                            //                                    edg.SecondaryFace.CheckConsistency();
        //                            //#endif
        //                            //                                }
        //                            //                            }
        //                        }
        //                    }
        //                }
        //            }

        //#if DEBUG
        //            openTrimmedEdges = new Set<Edge>();
        //            foreach (Face fce in trimmedFaces)
        //            {
        //                foreach (Edge edg in fce.AllEdges)
        //                {
        //                    if (edg.SecondaryFace == null)
        //                    {
        //                        openTrimmedEdges.Add(edg);
        //                    }
        //                }
        //            }
        //#endif
        //            // All edges of trimmedFaces are connected to either other trimmedfaces or to remaining uncut faces of the two shells.
        //            // Collect all faces that are reachable from trimmedFaces
        //            Set<Face> allFaces = new Set<Face>(trimmedFaces);
        //            bool added = true;
        //            while (added)
        //            {
        //                added = false;
        //                foreach (Face fce in allFaces.Clone()) // use a clone to be able to add faces to allfaces in this foreach loop
        //                {
        //                    foreach (Edge edg in fce.AllEdgesIterated())
        //                    {
        //                        if (!allFaces.Contains(edg.PrimaryFace))
        //                        {
        //                            if (!discardedFaces.Contains(edg.PrimaryFace) && edg.IsOrientedConnection)
        //                            {
        //                                allFaces.Add(edg.PrimaryFace);
        //                                added = true;
        //                            }
        //                            else
        //                            {
        //                                edg.DisconnectFromFace(edg.PrimaryFace);
        //                            }
        //                        }
        //                        if (edg.SecondaryFace != null && !allFaces.Contains(edg.SecondaryFace))
        //                        {
        //                            if (!discardedFaces.Contains(edg.SecondaryFace) && edg.IsOrientedConnection)
        //                            {
        //                                allFaces.Add(edg.SecondaryFace);
        //                                added = true;
        //                            }
        //                            else
        //                            {
        //                                edg.DisconnectFromFace(edg.SecondaryFace);
        //                            }
        //                        }
        //                        else if (edg.SecondaryFace == null)
        //                        {
        //                            Set<Edge> connecting = new Set<Edge>(Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2));
        //                            connecting.Remove(edg);
        //                            if (connecting.Count > 1)
        //                            {
        //                                Set<Edge> toRemove = new Set<Edge>();
        //                                foreach (Edge ce in connecting)
        //                                {
        //                                    if (!SameEdge(ce, edg, precision)) toRemove.Add(ce);
        //                                }
        //                                connecting.RemoveMany(toRemove);
        //                            }
        //                            if (connecting.Count == 1) // if we have more than one possibility to connect, there is no criterion to decide which would be thr correct face
        //                                                       // so we hope to find the correct face with an other path.
        //                            {
        //                                Edge con = connecting.GetAny();
        //                                if (con.SecondaryFace == null && !discardedFaces.Contains(con.PrimaryFace) && SameEdge(con, edg, precision))
        //                                {   // this is an identical edge, which is not logically connected. This is probably an intersection which coincides with an existing edge.
        //                                    allFaces.Add(con.PrimaryFace);
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            if (allFaces.Count == 0 && cancelledfaces.Count > 0)
        //            {   // there were no intersections, only identical opposite faces, like when glueing two parts together
        //                // this remains empty in case of intersection and returns the full body in cas of union
        //                if (this.operation == Operation.union)
        //                {
        //                    foreach (Face fce in cancelledfaces) fce.DisconnectAllEdges();
        //                    allFaces.AddMany(shell1.Faces);
        //                    allFaces.AddMany(shell2.Faces);
        //                    allFaces.RemoveMany(cancelledfaces);
        //                }
        //            }

        //            // the following is probably only necessary when there were overlapping faces:
        //            // connect open edges in allFaces with each other
        //            Set<Edge> openEdges = new Set<Edge>();
        //            foreach (Face fce in allFaces)
        //            {
        //                foreach (Edge edg in fce.Edges)
        //                {
        //                    if (edg.SecondaryFace == null) openEdges.Add(edg);
        //                }
        //            }
        //            ConnectOpenEdges(openEdges);
        //            List<Shell> res = new List<Shell>(); // the result of this method.
        //                                                 // allfaces now contains all the trimmed faces plus the faces, which are (directly or indirectly) connected (via edges) to the trimmed faces
        //            while (allFaces.Count > 0)
        //            {
        //                Set<Face> connected = extractConnectedFaces(allFaces, allFaces.GetAny());
        //                Shell shell = Shell.MakeShell(connected.ToArray());
        //#if DEBUG
        //                bool ok = shell.CheckConsistency();
        //#endif
        //                // res should not have open edges! If so, something went wrong
        //                if (shell.HasOpenEdgesEceptPoles())
        //                {
        //                    shell.TryConnectOpenEdges();
        //                }
        //                if (!shell.HasOpenEdgesEceptPoles())
        //                {
        //                    shell.CombineConnectedFaces(); // two connected faces which have the same surface are merged into one face
        //                    if (operation == Operation.union) shell.ReverseOrientation(); // both had been reversed and the intersection had been calculated
        //                    res.Add(shell);
        //                }
        //            }
        //            return res.ToArray();
        //        }

        private void SubtractCommonFaces(Set<Face> oppositeCommonFaces)
        {
            foreach (KeyValuePair<Face, Set<Face>> kv in faceToCommonFaces)
            {
                if (faceToIntersectionEdges.TryGetValue(kv.Key, out Set<Edge> intersectionEdges))
                {
                    // remove this face from faceToIntersectionEdges and add new faces
                    List<Face> result = new List<Face>();
                    result.Add(kv.Key);
                    foreach (Face fc in kv.Value) // subtract all these faces
                    {
                        List<Face> remaining = new List<Face>();
                        for (int i = 0; i < result.Count; i++)
                        {
                            bool secondIsOpposite = fc.UserData.ContainsData("BRepIntersection.IsOpposite");
                            List<Face> diff = Difference(result[i], fc, ModOp2D.Identity, secondIsOpposite);
                            if (diff.Count == 0)
                            {   // all or nothing
                                Dictionary<Face, Set<Edge>> common = Common(result[i], fc, ModOp2D.Identity);
                                if (common.Count == 0) remaining.Add(result[i]);
                            }
                            else
                            {
                                remaining.AddRange(diff);
                            }
                        }
                        result = remaining;
                    }
                    for (int i = 0; i < result.Count; i++)
                    {
                        Set<Edge> isedgs = new Set<Edge>();
                        foreach (Edge edg in intersectionEdges)
                        {
                            if (edg.PrimaryFace == kv.Key || edg.SecondaryFace == kv.Key)
                            {   // otherwise edg has already been related to another result[i]
                                GeoPoint2D mp = edg.Curve2D(kv.Key).PointAt(0.5);
                                if (result[i].Contains(ref mp, true))
                                {
                                    edg.ReplaceFace(kv.Key, result[i]);
                                    isedgs.Add(edg);
                                }
                            }
                        }
                        faceToIntersectionEdges.Add(result[i], isedgs);
                    }
                    faceToIntersectionEdges.Remove(kv.Key); // this one has been replaced by result[i], which is a part of it
                }
            }

        }

        public static bool IsSameFace(Set<Edge> edges, Set<Vertex> vertices, Face fce, double precision)
        {
            if (vertices != null)
            {
                Set<Vertex> fcev = new Set<Vertex>(fce.Vertices);
                if (!vertices.IsEqualTo(fcev))
                {
                    return false; // must have exactely the same vertices to be equal
                }
            }
            foreach (Edge edg in edges)
            {
                bool edgeFound = false;
                foreach (Edge edg1 in Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2))
                {
                    if (edg1.PrimaryFace == fce || edg1.SecondaryFace == fce)
                    {
                        if (SameEdge(edg, edg1, precision)) edgeFound = true;
                    }
                }
                if (!edgeFound) return false;
            }
            return true;
        }

        internal void ConnectOpenEdges(Set<Edge> openEdges)
        {
            foreach (Edge openEdge in openEdges)
            {
                if (openEdge.PrimaryFace == null) continue; // has already been merged
                if (openEdge.Vertex1 == openEdge.Vertex2)
                {
                    if (openEdge.Curve3D != null && openEdge.Curve3D.Length < Precision.eps)
                    {
                        openEdge.PrimaryFace.RemoveEdge(openEdge);
                    }
                    continue; // don't connect poles
                }
                foreach (Edge edg in Vertex.ConnectingEdges(openEdge.Vertex1, openEdge.Vertex2))
                {
                    if (edg == openEdge) continue;
                    if (!openEdges.Contains(edg)) continue;
                    if ((edg.PrimaryFace == openEdge.PrimaryFace || edg.SecondaryFace == openEdge.PrimaryFace) && BRepOperation.SameEdge(edg, openEdge, Precision.eps))
                    {
                        edg.RemoveFace(openEdge.PrimaryFace);
                        openEdge.PrimaryFace.ReplaceEdge(openEdge, edg);
                    }
                    else if (edg.SecondaryFace == null && openEdge.SecondaryFace == null && BRepOperation.SameEdge(edg, openEdge, Precision.eps))
                    {
                        if (openEdge.StartVertex(openEdge.PrimaryFace) != edg.StartVertex(edg.PrimaryFace))
                        {   // only correct oriented connections
                            openEdge.MergeWith(edg);
                            edg.DisconnectFromFace(openEdge.SecondaryFace);
                        }
                    }
                }
            }
        }

        private Set<Face> CollectOverlappingCommonFaces(Set<Face> discardedFaces)
        {
            Set<Face> commonFaces = new Set<Face>();
            foreach (KeyValuePair<DoubleFaceKey, ModOp2D> ov in overlappingFaces)
            {
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                dc.Add(ov.Key.face1);
                dc.Add(ov.Key.face2);
#endif
                Dictionary<Face, Set<Edge>> common = Common(ov.Key.face1, ov.Key.face2, ov.Value.GetInverse());
                if (common.Count > 0)
                {
                    discardedFaces.Add(ov.Key.face1);
                    discardedFaces.Add(ov.Key.face2);
                    Set<Face> ftc;
                    if (!faceToCommonFaces.TryGetValue(ov.Key.face1, out ftc)) faceToCommonFaces[ov.Key.face1] = ftc = new Set<Face>();
                    ftc.AddMany(common.Keys);
                    if (!faceToCommonFaces.TryGetValue(ov.Key.face2, out ftc)) faceToCommonFaces[ov.Key.face2] = ftc = new Set<Face>();
                    ftc.AddMany(common.Keys); // use the same faces, if we make clones, these clones will not be used in the result, but still exist when collecting faces
                    //foreach (Face fce in common.Keys)
                    //{
                    //    Face clone = fce.CloneWithVertices();
                    //    clone.ReplaceSurface(ov.Key.face2.Surface, ov.Value);
                    //    ftc.Add(clone);
                    //}
                }
                foreach (KeyValuePair<Face, Set<Edge>> item in common)
                {
                    commonFaces.Add(item.Key);
#if DEBUG
                    Face dbf = item.Key.Clone() as Face;
                    dbf.ColorDef = new ColorDef("violet", System.Drawing.Color.Violet);
                    dc.Add(dbf);
#endif
                }
            }
            return commonFaces;
        }
        private Set<Face> CollectOppositeCommonFaces(Set<Face> discardedFaces)
        {
            Set<Face> commonFaces = new Set<Face>();
            foreach (KeyValuePair<DoubleFaceKey, ModOp2D> op in oppositeFaces)
            {
                Dictionary<Face, Set<Edge>> common = Common(op.Key.face1, op.Key.face2, op.Value.GetInverse());
                if (common.Count > 0)
                {
                    discardedFaces.Add(op.Key.face1);
                    discardedFaces.Add(op.Key.face2);
                    Set<Face> ftc;
                    if (!faceToCommonFaces.TryGetValue(op.Key.face1, out ftc)) faceToCommonFaces[op.Key.face1] = ftc = new Set<Face>();
                    ftc.AddMany(common.Keys);
                    if (!faceToCommonFaces.TryGetValue(op.Key.face2, out ftc)) faceToCommonFaces[op.Key.face2] = ftc = new Set<Face>();
                    foreach (Face fce in common.Keys)
                    {
                        Face clone = fce.CloneWithVertices();
                        clone.ReplaceSurface(op.Key.face2.Surface, op.Value);
                        SimpleShape ss = clone.Area; // to make it valid, something with orientation is wrong when the "op.Value" matrix determinant is negative
                        ftc.Add(clone);
                        clone.UserData["BRepIntersection.IsOpposite"] = true; // clone is only used to subtract from other faces, here we need to mark that it is in the opposite faces
                        commonFaces.Add(clone); // 
                    }
                }
                commonFaces.AddMany(common.Keys);
            }
            return commonFaces;
        }

        private void ReplaceFaceToMixedEdges(Face toReplace, IEnumerable<Face> faces)
        {
            throw new NotImplementedException();
        }

        [Obsolete]
        private void ReduceOverlappingFaces(Set<Face> generatedFaces)
        {
            // overlappingFaces and oppositeFaces contain pairs of faces, that share the same surface and have the same or opposite orientation
            // Here we reduce these faces (split them into parts) so that the remaining parts don't overlap.
            // With same oriented overlapping, we make 3 parts: the symmetric difference and the common part. With opposite oriented overlapping,
            // we only make the symmetric difference parts. The intersection edges must be distributed onto the splitted parts
            // all new created faces are collected in generatedFaces
            Dictionary<Face, Set<Face>> replacedBy = new Dictionary<Face, Set<Face>>(); // this face from faceToIntersectionEdges has been replaced by these Faces
            while (overlappingFaces.Count > 0)
            {
                KeyValuePair<DoubleFaceKey, ModOp2D> kv = overlappingFaces.FirstOrDefault();
                // Split the two faces into 3 categories, each may have multiple faces or can be empty:
                // face1 minus face2, face2 minus face1 and common. And distribute the intersection edges of the original faces to the splitted faces
                Dictionary<Face, Set<Edge>> f1MinusF2 = DifferenceDeprecated(kv.Key.face1, kv.Key.face2, kv.Value.GetInverse(), false);
                Dictionary<Face, Set<Edge>> f2MinusF1 = DifferenceDeprecated(kv.Key.face2, kv.Key.face1, kv.Value, false);
                Dictionary<Face, Set<Edge>> common = Common(kv.Key.face1, kv.Key.face2, kv.Value.GetInverse());
                overlappingFaces.Remove(kv.Key);
                List<DoubleFaceKey> toRemove = new List<DoubleFaceKey>();
                List<KeyValuePair<DoubleFaceKey, ModOp2D>> toAdd = new List<KeyValuePair<DoubleFaceKey, ModOp2D>>();
                // now we have three sets of new faces. If face1 or face2 are also involved in other overlappings, we have to replace face1 by f1MinusF2 and common and
                // face2 by f2MinusF1 and common in these entries, i.e. remove the entries containing face1 or face2 and add new entries containing the splitted faces instead.
                foreach (KeyValuePair<DoubleFaceKey, ModOp2D> ov in overlappingFaces)
                {
                    if (ov.Key.face1 == kv.Key.face1)
                    {
                        toRemove.Add(ov.Key);
                        foreach (KeyValuePair<Face, Set<Edge>> kv1 in Enumerable.Concat(f1MinusF2, common))
                        {
                            DoubleFaceKey dfk = new DoubleFaceKey(kv1.Key, ov.Key.face2);
                            toAdd.Add(new KeyValuePair<DoubleFaceKey, ModOp2D>(dfk, ov.Value));
                        }
                    }
                    if (ov.Key.face2 == kv.Key.face2)
                    {
                        toRemove.Add(ov.Key);
                        foreach (KeyValuePair<Face, Set<Edge>> kv1 in Enumerable.Concat(f2MinusF1, common))
                        {
                            DoubleFaceKey dfk = new DoubleFaceKey(ov.Key.face1, kv1.Key);
                            toAdd.Add(new KeyValuePair<DoubleFaceKey, ModOp2D>(dfk, ov.Value));
                        }
                    }
                }
                foreach (DoubleFaceKey tr in toRemove)
                {
                    overlappingFaces.Remove(tr);
                }
                foreach (KeyValuePair<DoubleFaceKey, ModOp2D> ta in toAdd)
                {
                    overlappingFaces.Add(ta.Key, ta.Value);
                }
                faceToIntersectionEdges.Remove(kv.Key.face1);
                faceToIntersectionEdges.Remove(kv.Key.face2);
                foreach (KeyValuePair<Face, Set<Edge>> kv1 in Enumerable.Concat(Enumerable.Concat(f1MinusF2, common), f2MinusF1))
                {
                    faceToIntersectionEdges.Add(kv1.Key, kv1.Value);
                    generatedFaces.Add(kv1.Key);
                }
            }
            while (oppositeFaces.Count > 0)
            {
                KeyValuePair<DoubleFaceKey, ModOp2D> kv = oppositeFaces.FirstOrDefault();
                // Split the two faces into 3 categories, each may have multiple faces or can be empty:
                // face1 minus face2, face2 minus face1 and common. And distribute the intersection edges of the original faces to the splitted faces
                Dictionary<Face, Set<Edge>> f1MinusF2 = DifferenceDeprecated(kv.Key.face1, kv.Key.face2, kv.Value.GetInverse(), true);
                Dictionary<Face, Set<Edge>> f2MinusF1 = DifferenceDeprecated(kv.Key.face2, kv.Key.face1, kv.Value, true);
                oppositeFaces.Remove(kv.Key);
                List<DoubleFaceKey> toRemove = new List<DoubleFaceKey>();
                List<KeyValuePair<DoubleFaceKey, ModOp2D>> toAdd = new List<KeyValuePair<DoubleFaceKey, ModOp2D>>();
                foreach (KeyValuePair<DoubleFaceKey, ModOp2D> ov in oppositeFaces)
                {
                    if (ov.Key.face1 == kv.Key.face1)
                    {
                        toRemove.Add(ov.Key);
                        foreach (KeyValuePair<Face, Set<Edge>> kv1 in f1MinusF2)
                        {
                            DoubleFaceKey dfk = new DoubleFaceKey(kv1.Key, ov.Key.face2);
                            toAdd.Add(new KeyValuePair<DoubleFaceKey, ModOp2D>(dfk, ov.Value));
                        }
                    }
                    if (ov.Key.face2 == kv.Key.face2)
                    {
                        toRemove.Add(ov.Key);
                        foreach (KeyValuePair<Face, Set<Edge>> kv1 in f2MinusF1)
                        {
                            DoubleFaceKey dfk = new DoubleFaceKey(ov.Key.face1, kv1.Key);
                            toAdd.Add(new KeyValuePair<DoubleFaceKey, ModOp2D>(dfk, ov.Value));
                        }
                    }
                }
                foreach (DoubleFaceKey tr in toRemove)
                {
                    oppositeFaces.Remove(tr);
                }
                foreach (KeyValuePair<DoubleFaceKey, ModOp2D> ta in toAdd)
                {
                    oppositeFaces.Add(ta.Key, ta.Value);
                }
                faceToIntersectionEdges.Remove(kv.Key.face1);
                faceToIntersectionEdges.Remove(kv.Key.face2);
                foreach (KeyValuePair<Face, Set<Edge>> kv1 in Enumerable.Concat(f1MinusF2, f2MinusF1))
                {
                    faceToIntersectionEdges.Add(kv1.Key, kv1.Value);
                    generatedFaces.Add(kv1.Key);
                }
            }
        }

        private Dictionary<Face, Set<Edge>> Common(Face face1, Face face2, ModOp2D secondToFirst)
        {
            bool reverseSecond = secondToFirst.Determinant < 0;
            Dictionary<Face, Set<Edge>> res = new Dictionary<Face, Set<Edge>>();
            Set<Edge> toUse = new Set<Edge>();
            Set<Edge> ie1 = new Set<Edge>(); // empty set
            Set<Edge> ie2 = new Set<Edge>(); // empty set
            if (faceToIntersectionEdges.TryGetValue(face1, out Set<Edge> ie11)) ie1.AddMany(ie11);
            if (faceToIntersectionEdges.TryGetValue(face2, out Set<Edge> ie22)) ie2.AddMany(ie22);
            ie1.AddMany(face1.AllEdgesSet);
            ie2.AddMany(face2.AllEdgesSet);
            Face fc = Face.Construct(); // a placeholder for orientation only, it will not be fully constructed
            BoundingRect domain = face1.Area.GetExtent();
            foreach (Edge edg in face1.Edges)
            {
                // Add all edges of face1, which are inside face2
                // if face2 has an intersection edge identical to this edge, then it is inside face2
                Set<Edge> insideFace2 = (Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2) as Set<Edge>).Intersection(ie2);
                bool isInside = false, isOpposite = false;
                foreach (Edge edgi in insideFace2)
                {
                    if (SameEdge(edgi, edg, precision))
                    {
                        if (reverseSecond != (edgi.StartVertex(face2) == edg.StartVertex(face1))) isInside = true; // same direction
                        // else isOpposite = true;
                        //break;
                    }
                }
                if (!isInside && isOpposite) continue;
                if (!isInside)
                {   // not all edges are intersection edges: e.g. two concentric cylinders with the same radius, but different seams
                    if (face2.Contains(edg.Curve3D.PointAt(0.5), true)) isInside = true; // better in 2d with firstToSecond
                }
                if (isInside)
                {
                    Edge clone = edg.CloneWithVertices();
                    clone.SetPrimary(fc, edg.Curve2D(face1).Clone(), edg.Forward(face1));
                    toUse.Add(clone);
                }
            }
            foreach (Edge edg in face2.Edges)
            {
                // Add all edges of face2, which are inside face1
                Set<Edge> connectingEdges = Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2) as Set<Edge>;
                Set<Edge> cmn = connectingEdges.Intersection(toUse);
                if (cmn.Count > 0 && SameEdge(cmn.First(), edg, precision))
                {
                    continue; // this edge is common to face1 and face2, we already have it in toUse
                }
                Set<Edge> insideFace1 = connectingEdges.Intersection(ie1);
                bool isInside = false, isOpposite = false;
                foreach (Edge edgi in insideFace1)
                {
                    if (SameEdge(edgi, edg, precision))
                    {
                        if (reverseSecond != (edgi.StartVertex(face1) == edg.StartVertex(face2))) isInside = true; // same direction
                        // else isOpposite = true; // commented out because of breps2b
                        // break;
                    }
                }
                if (!isInside && isOpposite) continue;
                if (!isInside)
                {   // not all edges are intersection edges: e.g. two concentric cylinders with the same radius, but different seams
                    if (face1.Contains(edg.Curve3D.PointAt(0.5), true)) isInside = true;
                }
                if (isInside)
                {
                    Edge clone = edg.CloneWithVertices();
                    ICurve2D c2d;
                    if ((clone.Curve3D is InterpolatedDualSurfaceCurve))
                    {
                        bool onSurface1 = (clone.Curve3D as InterpolatedDualSurfaceCurve).Surface1 == face2.Surface;
                        (clone.Curve3D as InterpolatedDualSurfaceCurve).ReplaceSurface(face2.Surface, face1.Surface, secondToFirst);
                        if (onSurface1) c2d = (clone.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                        else c2d = (clone.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                        if (reverseSecond) c2d.Reverse();
                    }
                    else
                    {
                        c2d = edg.Curve2D(face2).GetModified(secondToFirst);
                    }
                    SurfaceHelper.AdjustPeriodic(face1.Surface, domain, c2d);
                    clone.SetPrimary(fc, c2d, edg.Forward(face2));
                    if (reverseSecond) clone.Reverse(fc);
                    toUse.Add(clone);
                }
            }
            Set<Edge> toDisconnect = toUse.Clone(); // toUse will be empty after GetLoops. We need to disconnect the edges from fc at the end
            List<List<Edge>> loops = GetLoops(toUse, fc);
            Dictionary<List<Edge>, List<List<Edge>>> loopsToHoles = SortLoopsTopologically(loops, fc);
            foreach (KeyValuePair<List<Edge>, List<List<Edge>>> loopToHoles in loopsToHoles)
            {
                Face face = Face.Construct();
                foreach (Edge edg in loopToHoles.Key) edg.ReplaceFace(fc, face);
                for (int i = 0; i < loopToHoles.Value.Count; i++)
                {
                    for (int j = 0; j < loopToHoles.Value[i].Count; j++)
                    {
                        loopToHoles.Value[i][j].ReplaceFace(fc, face);
                    }
                }
                face.Set(face1.Surface.Clone(), loopToHoles.Key, loopToHoles.Value);
                // the common part cannot contain intersection edges, or can it??? If so, see below (Difference)
                face.CopyAttributes(face1);
#if DEBUG
                face.UserData.Clear();
                face.UserData.Add("PartOf", face1.GetHashCode() + 100000 * face2.GetHashCode());
#endif
                res[face] = new Set<Edge>(); // empty set, the common part cannot contain intersection edges, 
                // because they would have to intersect both faces, which would mean a self intersection on one shell
            }
            foreach (Edge edg in toDisconnect)
            {
                edg.DisconnectFromFace(fc);
            }
            return res;

        }

        /// <summary>
        /// Make the difference face1 - face2 (which can be any number of faces, including none) and distribute the intersection edges of face1
        /// to the resulting faces
        /// </summary>
        /// <param name="face1"></param>
        /// <param name="face2"></param>
        /// <param name="secondToFirst"></param>
        /// <returns></returns>
        private Dictionary<Face, Set<Edge>> DifferenceDeprecated(Face face1, Face face2, ModOp2D secondToFirst, bool secondIsOpposite)
        {
            Dictionary<Face, Set<Edge>> res = new Dictionary<Face, Set<Edge>>();
            Set<Edge> toUse = new Set<Edge>();
            if (!faceToIntersectionEdges.TryGetValue(face1, out Set<Edge> ie1)) ie1 = new Set<Edge>(); // empty set
            if (!faceToIntersectionEdges.TryGetValue(face2, out Set<Edge> ie2)) ie2 = new Set<Edge>(); // empty set
            ie1.AddMany(face1.AllEdgesSet);
            ie2.AddMany(face2.AllEdgesSet);
            Face fc = Face.Construct(); // a placeholder for orientation only, it will not be fully constructed
            BoundingRect domain = face1.Area.GetExtent();
            foreach (Edge edg in face1.Edges)
            {
                // Add all edges of face1, which are not inside face2
                // if face2 has an intersection edge identical to this edge, then it is inside face2
                Set<Edge> insideFace2 = (Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2) as Set<Edge>).Intersection(ie2);
                if (insideFace2.Count == 0)
                {
                    Edge clone = edg.CloneWithVertices();
                    clone.SetPrimary(fc, edg.Curve2D(face1), edg.Forward(face1));
                    toUse.Add(clone);
                }
            }
            foreach (Edge edg in face2.Edges)
            {
                // Add all edges of face2, which are inside face1
                Set<Edge> connecting = Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2) as Set<Edge>;
                Set<Edge> insideFace1 = connecting.Intersection(ie1); // can be more than one
                bool isInside = false;
                foreach (Edge edgi in insideFace1)
                {
                    if (SameEdge(edgi, edg, precision))
                    {
                        isInside = true;
                        break;
                    }
                }
                if (isInside)
                {
                    Set<Edge> onFace1 = connecting.Intersection(face1.AllEdgesSet);
                    bool notOnFace1 = true;
                    foreach (Edge edg1 in onFace1)
                    {
                        if (SameEdge(edg1, edg, precision) && (edg1.StartVertex(face1) == edg.StartVertex(face2) != secondIsOpposite)) notOnFace1 = false;
                    }
                    if (notOnFace1)
                    {   // edg is not an edge on face1
                        Edge clone = edg.CloneWithVertices();
                        ICurve2D c2d = edg.Curve2D(face2).GetModified(secondToFirst);
                        //c2d = face1.Surface.GetProjectedCurve(edg.Curve3D, 0.0);
                        //if (!edg.Forward(face2)) c2d.Reverse();
                        SurfaceHelper.AdjustPeriodic(face1.Surface, domain, c2d);
                        clone.SetPrimary(fc, c2d, edg.Forward(face2));
                        if (!secondIsOpposite) clone.Reverse(fc);
                        toUse.Add(clone);
                    }
                }
            }
            if (toUse.Count > 0)
            {
                List<List<Edge>> loops = GetLoops(toUse, fc);
                Dictionary<List<Edge>, List<List<Edge>>> loopsToHoles = SortLoopsTopologically(loops, fc);
                foreach (KeyValuePair<List<Edge>, List<List<Edge>>> loopToHoles in loopsToHoles)
                {
                    Face face = Face.Construct();
                    foreach (Edge edg in loopToHoles.Key) edg.ReplaceFace(fc, face);
                    for (int i = 0; i < loopToHoles.Value.Count; i++)
                    {
                        for (int j = 0; j < loopToHoles.Value[i].Count; j++)
                        {
                            loopToHoles.Value[i][j].ReplaceFace(fc, face);
                        }
                    }
                    face.Set(face1.Surface.Clone(), loopToHoles.Key, loopToHoles.Value);
                    Set<Edge> onNewFace = face.AllEdgesSet;
                    Set<Edge> intersectionEdges = new Set<Edge>();
                    foreach (Edge ie in ie1)
                    {
                        Set<Edge> onOutline = (Vertex.ConnectingEdges(ie.Vertex1, ie.Vertex2) as Set<Edge>).Intersection(onNewFace);
                        bool isInside = false;
                        foreach (Edge edg in onOutline)
                        {
                            if (SameEdge(edg, ie, precision))
                            {
                                isInside = true;
                                break;
                            }
                        }
                        if (isInside)
                        {   // this intersection edge is on the outline of the new face
                            intersectionEdges.Add(ie.CloneReplaceFace(face1, face, true));
                        }
                        else
                        {
                            GeoPoint2D testPoint = ie.Curve2D(face1).PointAt(0.5);
                            if (face.Contains(ref testPoint, false))
                            {   // this intersection edge is inside the new face
                                intersectionEdges.Add(ie.CloneReplaceFace(face1, face, true));
                            }
                        }
                    }
                    face.CopyAttributes(face1);
                    res[face] = intersectionEdges;
                }
            }
            return res;
        }
        /// <summary>
        /// Make the difference face1 - face2 (which can be any number of faces, including none) and distribute the intersection edges of face1
        /// to the resulting faces
        /// </summary>
        /// <param name="face1"></param>
        /// <param name="face2"></param>
        /// <param name="secondToFirst"></param>
        /// <returns></returns>
        private List<Face> Difference(Face face1, Face face2, ModOp2D secondToFirst, bool secondIsOpposite)
        {
            List<Face> res = new List<Face>();
            Set<Edge> toUse = new Set<Edge>();
            if (!faceToIntersectionEdges.TryGetValue(face1, out Set<Edge> ie1)) ie1 = new Set<Edge>(); // empty set
            if (!faceToIntersectionEdges.TryGetValue(face2, out Set<Edge> ie2)) ie2 = new Set<Edge>(); // empty set
            ie1.AddMany(face1.AllEdgesSet);
            ie2.AddMany(face2.AllEdgesSet);
            Face fc = Face.Construct(); // a placeholder for orientation only, it will not be fully constructed
            BoundingRect domain = face1.Area.GetExtent();
            foreach (Edge edg in face1.Edges)
            {
                // Add all edges of face1, which are not inside face2
                // if face2 has an intersection edge identical to this edge, then it is inside face2
                Set<Edge> insideFace2 = (Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2) as Set<Edge>).Intersection(ie2);
                foreach (Edge if2 in insideFace2.Clone())
                {
                    if (!SameEdge(if2, edg, precision)) insideFace2.Remove(if2);
                }
                if (insideFace2.Count == 0)
                {
                    Edge clone = edg.CloneWithVertices();
                    clone.SetPrimary(fc, edg.Curve2D(face1), edg.Forward(face1));
                    toUse.Add(clone);
                }
            }
            foreach (Edge edg in face2.Edges)
            {
                // Add all edges of face2, which are inside face1
                Set<Edge> connecting = Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2) as Set<Edge>;
                Set<Edge> insideFace1 = connecting.Intersection(ie1); // can be more than one
                bool isInside = false;
                foreach (Edge edgi in insideFace1)
                {
                    if (SameEdge(edgi, edg, precision))
                    {
                        isInside = true;
                        break;
                    }
                }
                if (isInside)
                {
                    Set<Edge> onFace1 = connecting.Intersection(face1.AllEdgesSet);
                    bool notOnFace1 = true;
                    foreach (Edge edg1 in onFace1)
                    {
                        if (SameEdge(edg1, edg, precision) && (edg1.StartVertex(face1) == edg.StartVertex(face2) != secondIsOpposite)) notOnFace1 = false;
                    }
                    if (notOnFace1)
                    {   // edg is not an edge on face1
                        Edge clone = edg.CloneWithVertices();
                        ICurve2D c2d = edg.Curve2D(face2).GetModified(secondToFirst);
                        //c2d = face1.Surface.GetProjectedCurve(edg.Curve3D, 0.0);
                        //if (!edg.Forward(face2)) c2d.Reverse();
                        SurfaceHelper.AdjustPeriodic(face1.Surface, domain, c2d);
                        clone.SetPrimary(fc, c2d, edg.Forward(face2));
                        if (!secondIsOpposite) clone.Reverse(fc);
                        toUse.Add(clone);
                    }
                }
            }
            if (toUse.Count > 0)
            {
                List<List<Edge>> loops = GetLoops(toUse, fc);
                Dictionary<List<Edge>, List<List<Edge>>> loopsToHoles = SortLoopsTopologically(loops, fc);
                foreach (KeyValuePair<List<Edge>, List<List<Edge>>> loopToHoles in loopsToHoles)
                {
                    Face face = Face.Construct();
                    foreach (Edge edg in loopToHoles.Key) edg.ReplaceFace(fc, face);
                    for (int i = 0; i < loopToHoles.Value.Count; i++)
                    {
                        for (int j = 0; j < loopToHoles.Value[i].Count; j++)
                        {
                            loopToHoles.Value[i][j].ReplaceFace(fc, face);
                        }
                    }
                    face.Set(face1.Surface.Clone(), loopToHoles.Key, loopToHoles.Value);
                    Set<Edge> onNewFace = face.AllEdgesSet;
                    face.CopyAttributes(face1);
                    res.Add(face);
                }
            }
            return res;
        }

        /// <summary>
        /// Sort the set of loops into a dictionary, so that all dictionary entries have a positive loop (ccw, outline) as a key and all negative loops
        /// (cw, hole), which are located inside this outline, as value. All in respect to the provided face.
        /// </summary>
        /// <param name="loops"></param>
        /// <param name="face"></param>
        /// <returns></returns>
        private Dictionary<List<Edge>, List<List<Edge>>> SortLoopsTopologically(List<List<Edge>> loops, Face face)
        {
            Dictionary<int, List<int>> resIndices = new Dictionary<int, List<int>>();
            ICurve2D[][] loops2d = new ICurve2D[loops.Count][];
            for (int i = 0; i < loops.Count; i++)
            {
                loops2d[i] = new ICurve2D[loops[i].Count];
                for (int j = 0; j < loops[i].Count; j++)
                {
                    loops2d[i][j] = loops[i][j].Curve2D(face);
                }
            }
            UniqueDoubleReverseDictionary<int> sortedLoops = new UniqueDoubleReverseDictionary<int>();
            for (int i = 0; i < loops2d.Length; i++)
            {
                sortedLoops.AddUnique(Border.SignedArea(loops2d[i]), i);
            }
            // sortedLoops now contains the indices of all loop, beginning with the bigges outline, ending with the holes
            foreach (KeyValuePair<double, int> kv in sortedLoops)
            {
                if (kv.Key > 0) resIndices[kv.Value] = new List<int>(); // an outline with no holes yet
                else
                {   // a hole, here all outlines are already in the resulting dictionary
                    foreach (int outline in resIndices.Keys)
                    {
                        if (Border.IsInside(loops2d[outline], loops2d[kv.Value][0].StartPoint))
                        {
                            resIndices[outline].Add(kv.Value); // this loop, which is a hole, belongs to that outline
                            break;
                        }
                    }
                }
            }
            Dictionary<List<Edge>, List<List<Edge>>> res = new Dictionary<List<Edge>, List<List<Edge>>>();
            foreach (KeyValuePair<int, List<int>> item in resIndices)
            {
                res[loops[item.Key]] = new List<List<Edge>>();
                for (int i = 0; i < item.Value.Count; i++)
                {
                    res[loops[item.Key]].Add(loops[item.Value[i]]);
                }
            }
            return res;
        }

        /// <summary>
        /// Takes a set of edges and tries to connect them to closed loops. 
        /// </summary>
        /// <param name="workingSet">work on this set, which will be emptied</param>
        /// <param name="face">orientation in respect to this face</param>
        /// <returns></returns>
        private List<List<Edge>> GetLoops(Set<Edge> workingSet, Face face)
        {
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            foreach (Edge edg in workingSet)
            {
                dc.Add(edg.Curve2D(face), System.Drawing.Color.Red, edg.GetHashCode());
            }
#endif
            List<List<Edge>> res = new List<List<Edge>>();
            Dictionary<Vertex, int> allVertices = new Dictionary<Vertex, int>();
            foreach (Edge edg in workingSet)
            {
                if (!allVertices.TryGetValue(edg.Vertex1, out int flag))
                {
                    flag = 0;
                }
                if (edg.Forward(face)) flag |= 1;
                else flag |= 2;
                allVertices[edg.Vertex1] = flag;
                if (!allVertices.TryGetValue(edg.Vertex2, out flag))
                {
                    flag = 0;
                }
                if (edg.Forward(face)) flag |= 2;
                else flag |= 1;
                allVertices[edg.Vertex2] = flag;
            }
            foreach (KeyValuePair<Vertex, int> item in allVertices)
            {   // each vertex must have at least one incomming and one outgoing edge
                if (item.Value != 3)
                {
                    workingSet.RemoveMany(item.Key.AllEdges);
                    if (workingSet.Count == 0) return res;
                }
            }
            if (workingSet.Count == 0) return res;
            Edge next = workingSet.GetAny();
            Vertex startVertex = next.StartVertex(face);
            List<Edge> loop = new List<Edge>();
            while (next != null)
            {
                loop.Add(next);
                workingSet.Remove(next);
                if (next.EndVertex(face) == startVertex)
                {
                    res.Add(loop);
                    next = workingSet.GetAny();
                    loop = new List<Edge>(); // for next loop
                    if (next != null) startVertex = next.StartVertex(face);
                }
                else
                {
                    Set<Edge> possibleConnections = next.EndVertex(face).AllEdges.Intersection(workingSet);
                    Vertex endVertex = next.EndVertex(face);
                    next = null;
                    foreach (Edge edg in possibleConnections)
                    {
                        if (edg.StartVertex(face) == endVertex)
                        {
                            next = edg;
                            break;
                        }
                    }
                    if (next == null)
                    {
                        // this was an open connection
                        if (loop.Count > 0)
                        {   // remove only the last edge, which has no connection
                            workingSet.AddMany(loop);
                            workingSet.Remove(loop[loop.Count - 1]);
                            loop.Clear();
                        }
                        next = workingSet.GetAny(); // try with another starting point
                        if (next != null) startVertex = next.StartVertex(face);
                    }
                }
            }
            return res;
        }

        private List<List<Edge>> GetCommon(Face face1, Face face2, ModOp2D face2To1)
        {
            ModOp2D face1To2 = face2To1.GetInverse();
            Set<Vertex> commonVertices = new Set<Vertex>(face1.Vertices).Intersection(new Set<Vertex>(face2.Vertices));
            Dictionary<Vertex, List<Edge>> connections = new Dictionary<Vertex, List<Edge>>();
            foreach (Vertex vtx in commonVertices)
            {

                List<Edge> con1 = face1.FindConnection(vtx, commonVertices);
                List<Edge> con2 = face2.FindConnection(vtx, commonVertices);
                if (con1.Count > 0 && con2.Count > 0 && SameConnection(con1, con2, face1, face2, precision))
                {
                    connections[vtx] = con1;
                }
                else
                {
                    if (con1.Count > 0)
                    {
                        GeoPoint2D testPoint = face1To2 * con1[0].Curve2D(face1).PointAt(0.5);
                        if (face2.Contains(ref testPoint, false)) connections[vtx] = con1;
                    }
                    if (con2.Count > 0)
                    {
                        GeoPoint2D testPoint = face2To1 * con2[0].Curve2D(face2).PointAt(0.5);
                        if (face1.Contains(ref testPoint, false)) connections[vtx] = con2;
                    }
                }
            }
            // connections is all connections but no duplicates
            KeyValuePair<Vertex, List<Edge>> kv = Enumerable.FirstOrDefault(connections);
            Vertex startVertex = kv.Key;
            List<Edge> startSegment = kv.Value;
            List<List<Edge>> loops = new List<List<Edge>>();
            List<Edge> loop = new List<Edge>();
            while (kv.Key != null)
            {
                loop.AddRange(startSegment);
                connections.Remove(kv.Key);

            }
            return loops;
        }

        //private int CompareReverse(double x, double y) { return -x.CompareTo(y); }
        private int ComparePair(Pair<List<Edge>, ICurve2D[]> x, Pair<List<Edge>, ICurve2D[]> y)
        {
            if (x.First.Count == y.First.Count)
            {
                for (int i = 0; i < x.First.Count; i++)
                {
                    if (x.First[i].GetHashCode() != y.First[i].GetHashCode())
                    {
                        return (x.First[i].GetHashCode().CompareTo(y.First[i].GetHashCode()));
                    }
                }
                return 0;
            }
            else return x.First.Count.CompareTo(y.First.Count);
        }

        /// <summary>
        /// Check whether the two lists of (connected) edges describe the same (geometric) path.
        /// </summary>
        /// <param name="con1"></param>
        /// <param name="con2"></param>
        /// <param name="onThisFace"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        internal static bool SameConnection(List<Edge> con1, List<Edge> con2, Face onThisFace, double precision)
        {
            if (con1.Count == 1 && con2.Count == 1) return SameEdge(con1[0], con2[0], precision);
            else if (con1.Count >= con2.Count)
            {   // it is egnough to test the inner vertices of the connection with more inner vertices against the 3d curves of the other connection
                for (int i = 0; i < con1.Count - 1; i++)
                {
                    GeoPoint p = con1[i].EndVertex(onThisFace).Position;
                    bool hit = false;
                    for (int j = 0; j < con2.Count; j++)
                    {
                        if (con2[j].Curve3D != null) hit = con2[j].Curve3D.DistanceTo(p) < 10 * precision; // using precision might fail
                        if (hit) break;
                    }
                    if (!hit) return false;
                }
                return true;
            }
            else
            {
                return SameConnection(con2, con1, onThisFace, precision);
            }
        }
        internal static bool SameConnection(List<Edge> con1, List<Edge> con2, Face face1, Face face2, double precision)
        {
            if (con1.Count == 1 && con2.Count == 1) return SameEdge(con1[0], con2[0], precision);
            else if (con1.Count >= con2.Count) return SameConnection(con1, con2, face1, precision);
            else return SameConnection(con2, con1, face2, precision);

        }

        //        public Shell[] ResultOld()
        //        {
        //            /// the following should be removed sooner or later in favor of the new approach
        //            // *** good debugging position marked by ***
        //            List<Shell> res = new List<GeoObject.Shell>();
        //            if (faceToIntersectionEdges.Count == 0) // why was there && overlappingFaces.Count == 0
        //            {
        //                if (cancelledfaces.Count > 0)
        //                {
        //                    Set<Face> remainingFaces = new Set<Face>(shell1.Faces);
        //                    remainingFaces.AddMany(shell2.Faces);
        //                    remainingFaces.RemoveMany(cancelledfaces);
        //                    Set<Edge> openEdges = new Wintellect.PowerCollections.Set<CADability.Edge>();
        //                    foreach (Face fce in cancelledfaces)
        //                    {
        //                        foreach (Edge edg in fce.AllEdgesIterated())
        //                        {
        //                            edg.RemoveFace(fce);
        //                            openEdges.Add(edg);
        //                        }
        //                    }
        //                    while (openEdges.Count > 0)
        //                    {
        //                        Edge edg = openEdges.GetAny();
        //                        openEdges.Remove(edg);
        //                        Set<Edge> connecting = new Set<Edge>(Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2));
        //                        connecting.IntersectionWith(openEdges);
        //                        if (connecting.Count == 1)
        //                        {
        //                            Edge edg1 = connecting.GetAny();
        //                            openEdges.Remove(edg1);
        //                            edg1.PrimaryFace.ReplaceEdge(edg1, edg);
        //                        }
        //                        else
        //                        {
        //                            foreach (Edge cedge in connecting)
        //                            {
        //                                if (cedge.Curve3D != null && edg.Curve3D != null)
        //                                {
        //                                    if (cedge.Curve3D.DistanceTo(edg.Curve3D.PointAt(0.5)) < Precision.eps)
        //                                    {
        //                                        openEdges.Remove(cedge);
        //                                        cedge.PrimaryFace.ReplaceEdge(cedge, edg);
        //                                        break;
        //                                    }
        //                                }
        //                            }
        //                        }
        //                    }
        //                    while (remainingFaces.Count > 0)
        //                    {
        //                        Set<Face> sf = BRepOperation.extractConnectedFaces(remainingFaces, remainingFaces.GetAny());
        //                        Shell shell = Shell.MakeShell(sf.ToArray(), true);
        //                        openEdges = new Set<Edge>(shell.OpenEdges);
        //                        bool ok = true;
        //                        foreach (Edge edg in openEdges)
        //                        {
        //                            if (edg.Curve3D != null) ok = false;

        //                        }
        //                        if (ok) res.Add(shell);
        //#if DEBUG
        //                        // es gibt Reste, die nicht geschlossen sind
        //                        // System.Diagnostics.Debug.Assert(shell.OpenEdges.Length == 0);
        //#endif
        //                    }

        //#if DEBUG
        //                    for (int i = 0; i < res.Count; i++)
        //                    {
        //                        foreach (Face fce in res[i].Faces)
        //                        {
        //                            System.Diagnostics.Debug.Assert(fce.CheckConsistency());
        //                        }
        //                    }
        //#endif
        //                    if (operation == Operation.union)
        //                    {
        //                        for (int i = 0; i < res.Count; i++)
        //                        {
        //                            res[i].ReverseOrientation();
        //                        }
        //                    }
        //                    // der Aufrufer muss die Lage der Shells zueinander überprüfen und ggf. Solids mit Löchern daraus machen
        //#if DEBUG
        //                    // Shell dbgs = (Shell)Project.SerializeDeserialize(res[0]);
        //#endif
        //                }
        //                return res.ToArray();
        //            }
        //#if DEBUG
        //            DebuggerContainer dcfcs = new CADability.DebuggerContainer();
        //            foreach (Face fce in shell1.Faces)
        //            {
        //                dcfcs.Add(fce.Clone(), fce.GetHashCode()); // die Faces werden kaputt gemacht, deshalb hier clones merken
        //                double ll = fce.GetExtent(0.0).Size * 0.01;
        //                ColorDef cd = new ColorDef("debug", System.Drawing.Color.Blue);
        //                SimpleShape ss = fce.Area;
        //                GeoPoint2D c = ss.GetExtent().GetCenter();
        //                GeoPoint pc = fce.Surface.PointAt(c);
        //                GeoVector nc = fce.Surface.GetNormal(c);
        //                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
        //                l.ColorDef = cd;
        //                dcfcs.Add(l);
        //            }
        //            foreach (Face fce in shell2.Faces)
        //            {
        //                dcfcs.Add(fce.Clone(), fce.GetHashCode()); // die Faces werden kaputt gemacht, deshalb hier clones merken
        //                double ll = fce.GetExtent(0.0).Size * 0.01;
        //                ColorDef cd = new ColorDef("debug", System.Drawing.Color.Brown);
        //                SimpleShape ss = fce.Area;
        //                GeoPoint2D c = ss.GetExtent().GetCenter();
        //                GeoPoint pc = fce.Surface.PointAt(c);
        //                GeoVector nc = fce.Surface.GetNormal(c);
        //                Line l = Line.TwoPoints(pc, pc + ll * nc.Normalized);
        //                l.ColorDef = cd;
        //                dcfcs.Add(l);
        //            }
        //            DebuggerContainer dcs1e = new CADability.DebuggerContainer();
        //            foreach (Edge edg in shell1.Edges)
        //            {
        //                if (edg.Curve3D != null) dcs1e.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
        //            }
        //            DebuggerContainer dcs2e = new CADability.DebuggerContainer();
        //            foreach (Edge edg in shell2.Edges)
        //            {
        //                if (edg.Curve3D != null) dcs2e.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
        //            }
        //            DebuggerContainer dcis = new CADability.DebuggerContainer();
        //            Set<Edge> ise = new Set<Edge>();
        //            foreach (KeyValuePair<Face, Set<Edge>> item in faceToIntersectionEdges)
        //            {
        //                ise.AddMany(item.Value);
        //            }
        //            foreach (Edge edg in ise)
        //            {
        //                if (edg.Curve3D != null) dcis.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
        //            }
        //            // *** dcfcs: all invoved faces. dcis: all intersection edges, must all be connected, no open end
        //#endif
        //            Dictionary<Face, List<Face>> oppositeCandidates = new Dictionary<Face, List<Face>>();
        //            foreach (DoubleFaceKey dfk in oppositeFaces.Keys)
        //            {
        //                List<Face> list;
        //                if (!oppositeCandidates.TryGetValue(dfk.face1, out list))
        //                {
        //                    list = new List<Face>();
        //                    oppositeCandidates[dfk.face1] = list;
        //                }
        //                list.Add(dfk.face2);
        //                if (!oppositeCandidates.TryGetValue(dfk.face2, out list))
        //                {
        //                    list = new List<Face>();
        //                    oppositeCandidates[dfk.face2] = list;
        //                }
        //                list.Add(dfk.face1);
        //            }
        //            Set<Face> trimmedFaces = new Set<Face>(); // all new faces, which are trimmed parts of the original faces
        //            Set<Face> destroyedFaces = new Set<Face>(); // set of the original faces, that have been trimmed or are totally covered by opposite faces

        //            // overlapping faces (they have the same orientation): 
        //            // the intersection edges on the ionvolved faces yield the (face1-face2) parts and (face2-face1) parts.
        //            // in the following loop the common parts are created.
        //            Set<Edge> commonEdges = new Set<Edge>();
        //            foreach (DoubleFaceKey dfk in overlappingFaces.Keys)
        //            {
        //                Face face1 = dfk.face1;
        //                Face face2 = dfk.face2; // the two overlapping faces
        //                Set<Edge> commonIntersectionEdges = new Set<Edge>(); // we only need the outlines here, so we are not interested in the intersection edges
        //                if (faceToIntersectionEdges.ContainsKey(face1)) commonIntersectionEdges.UnionWith(faceToIntersectionEdges[face1]);
        //                if (faceToIntersectionEdges.ContainsKey(face2)) commonIntersectionEdges.UnionWith(faceToIntersectionEdges[face2]);
        //                ModOp2D mop12 = overlappingFaces[dfk]; // from surface of face1 to surface of face2
        //                ModOp2D mop21 = overlappingFaces[dfk].GetInverse(); // and vice versa
        //                Set<Edge> availableEdges = new Set<Edge>(); // all the edges that bound the common parts
        //                                                            // three sources for the edges:
        //                                                            // - common to both faces
        //                                                            // - edge of face1 which is inside face2 (easy to check, because common edges don't need to be checked here)
        //                                                            // - vice versa
        //                foreach (Edge edg in dfk.face1.AllEdgesIterated())
        //                {
        //                    foreach (Edge edg2 in Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2))
        //                    {
        //                        if (edg2 == edg || commonIntersectionEdges.Contains(edg2)) continue; // intersectionedges not used here, only outline
        //                        if ((edg2.PrimaryFace == dfk.face2 || edg2.SecondaryFace == dfk.face2) && SameEdge(edg, edg2, precision))
        //                        {
        //                            // there is an identical edge on the overlapping faces dfk.face1 and dfk.face2
        //                            Edge clone = edg.Clone();
        //                            clone.SetPrimary(dfk.face1, edg.Curve2D(dfk.face1).Clone(), edg.Forward(dfk.face1));
        //                            if (clone.edgeInfo == null) clone.edgeInfo = new EdgeInfo(clone);
        //                            clone.edgeInfo.oldConnection = edg.OtherFace(dfk.face1);
        //                            clone.SetVertices(edg.Vertex1, edg.Vertex2);
        //                            availableEdges.Add(clone);
        //                            commonEdges.Add(edg);
        //                            commonEdges.Add(edg2);
        //                        }
        //                    }
        //                }
        //                foreach (Edge edg in dfk.face1.AllEdgesIterated())
        //                {
        //                    if (commonEdges.Contains(edg)) continue; // already processed
        //                                                             // is this edge inside the bounds of face2?
        //                    GeoPoint2D poc = mop12 * edg.Curve2D(dfk.face1).PointAt(0.5);
        //                    if (dfk.face2.Contains(ref poc, false))
        //                    {
        //                        Edge clone = edg.Clone();
        //                        if (clone.Curve3D is InterpolatedDualSurfaceCurve)
        //                        {   // hier ist ein Dilemma: der clone einer Edge mit InterpolatedDualSurfaceCurve müsste wieder zwei Faces haben, hat es hier aber nicht
        //                            // deshalb hier mal mit BSplines annähern. Die Edge müsste ein Flag haben, mit dem man später wieder die InterpolatedDualSurfaceCurve machen kann
        //                            // wobei man die throughpoints des BSplines verwenden kann
        //                            clone.Curve3D = (clone.Curve3D as InterpolatedDualSurfaceCurve).ToBSpline(precision);
        //                            clone.SetPrimary(dfk.face1, (edg.Curve2D(dfk.face1) as InterpolatedDualSurfaceCurve.ProjectedCurve).ToBSpline(precision), edg.Forward(dfk.face1));
        //                        }
        //                        else
        //                        {
        //                            clone.SetPrimary(dfk.face1, edg.Curve2D(dfk.face1).Clone(), edg.Forward(dfk.face1));
        //                        }
        //                        if (clone.edgeInfo == null) clone.edgeInfo = new EdgeInfo(clone);
        //                        clone.edgeInfo.oldConnection = edg.OtherFace(dfk.face1);
        //                        clone.SetVertices(edg.Vertex1, edg.Vertex2);
        //                        availableEdges.Add(clone);
        //                    }
        //                }
        //                BoundingRect face1Domain = dfk.face1.Area.GetExtent();
        //                foreach (Edge edg in dfk.face2.AllEdgesIterated())
        //                {
        //                    if (commonEdges.Contains(edg)) continue; // already processed
        //                                                             // is this edge inside the bounds of face1?
        //                    GeoPoint2D poc = mop21 * edg.Curve2D(dfk.face2).PointAt(0.5);
        //                    if (dfk.face1.Contains(ref poc, false))
        //                    {
        //                        Edge clone = edg.Clone();
        //                        if (clone.Curve3D is InterpolatedDualSurfaceCurve)
        //                        {
        //                            clone.Curve3D = (clone.Curve3D as InterpolatedDualSurfaceCurve).ToBSpline(precision);
        //                            clone.SetPrimary(dfk.face1, (edg.Curve2D(dfk.face2) as InterpolatedDualSurfaceCurve.ProjectedCurve).ToBSpline(precision).GetModified(mop21), edg.Forward(dfk.face2));
        //                        }
        //                        else
        //                        {
        //                            clone.SetPrimary(dfk.face1, edg.Curve2D(dfk.face2).GetModified(mop21), edg.Forward(dfk.face2));
        //                        }
        //                        SurfaceHelper.AdjustPeriodic(dfk.face1.Surface, face1Domain, clone.PrimaryCurve2D);
        //                        if (clone.edgeInfo == null) clone.edgeInfo = new EdgeInfo(clone);
        //                        clone.edgeInfo.oldConnection = edg.OtherFace(dfk.face2);
        //                        clone.SetVertices(edg.Vertex1, edg.Vertex2);
        //                        availableEdges.Add(clone);
        //                    }
        //                }

        //#if DEBUG
        //                DebuggerContainer dc0 = new CADability.DebuggerContainer();
        //                double arrowsize = dfk.face1.Area.GetExtent().Size / 100.0;
        //                foreach (Edge edg in availableEdges)
        //                {
        //                    dc0.Add(edg.Curve2D(dfk.face1), System.Drawing.Color.Red, edg.GetHashCode());
        //                    // noch einen Richtungspfeil zufügen
        //                    GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
        //                    GeoVector2D dir = edg.Curve2D(dfk.face1).DirectionAt(0.5).Normalized;
        //                    arrowpnts[1] = edg.Curve2D(dfk.face1).PointAt(0.5);
        //                    arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
        //                    arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
        //                    Polyline2D pl2d = new Polyline2D(arrowpnts);
        //                    dc0.Add(pl2d, System.Drawing.Color.Red, edg.GetHashCode());
        //                }
        //                Set<Edge> dbgset = availableEdges.Clone();
        //                availableEdges = dbgset.Clone(); // if generateCycles failed, go back here to debug
        //                                                 // *** dc0: 2d image of loops with direction
        //#endif
        //                List<List<Edge>> cycles = generateCycles(availableEdges, dfk.face1, precision);
        //                Dictionary<int, List<int>> outlinesToHoles = SortCycles(cycles, dfk.face1);

        //                foreach (KeyValuePair<int, List<int>> oth in outlinesToHoles)
        //                {
        //                    Face fc = Face.Construct();
        //#if DEBUG
        //                    fc.UserData.Add("PartOf", dfk.face1.GetHashCode() + 100000 * dfk.face2.GetHashCode());
        //#endif
        //                    fc.CopyAttributes(dfk.face1);

        //                    Edge[] outline = cycles[oth.Key].ToArray();
        //                    for (int j = 0; j < outline.Length; j++)
        //                    {
        //                        outline[j].ReplaceFace(dfk.face1, fc);
        //                    }
        //                    // es kann Löcher geben, die außerhalb von outline liegen, oder solche, die innerhalb anderer Löcher liegen.
        //                    // Überschneidungen sind aber ausgeschlossen
        //                    Edge[][] holes = new Edge[oth.Value.Count][];
        //                    for (int j = 0; j < holes.Length; j++)
        //                    {
        //                        holes[j] = cycles[oth.Value[j]].ToArray();
        //                        for (int k = 0; k < holes[j].Length; k++)
        //                        {
        //                            holes[j][k].ReplaceFace(dfk.face1, fc);
        //                        }
        //                    }
        //                    // Surface wird hier gecloned, denn es können mehrere Faces mit der selben Surface entstehen
        //                    // und diese Surface würde sonst bei ReverseOrientation (bei union) ggf. mehrfach umgedreht
        //                    fc.Set(dfk.face1.Surface.Clone(), outline, holes);
        //                    foreach (Edge edg in fc.AllEdgesIterated())
        //                    {
        //                        if (edg.Curve3D != null) edg.SurfaceChanged(dfk.face1.Surface, fc.Surface);
        //                    }
        //                    trimmedFaces.Add(fc); // hat schon die richtigen Kanten
        //                    destroyedFaces.Add(dfk.face1);
        //                    destroyedFaces.Add(dfk.face2);
        //                }


        //            }
        //            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToIntersectionEdges)
        //            {
        //                Face faceToSplit = kv.Key;
        //                // kv.Key: face containing intersection edges
        //                // kv.Value: intersection edges on this face
        //                // this face (kv.Key) will be destroyed and new faces will be generated
        //                Set<Edge> availableEdges = new Set<Edge>(kv.Value); // 
        //                if (availableEdges.Count == 0) continue;
        //                Set<Edge> faceToSplitEdges = faceToSplit.AllEdgesSet;
        //                // if there is a path of connected edges in the faces outline, which is identical to an intersection edge,
        //                // the remove this intersection edge. If it is in the inverse direction, also remove the intersection edge.
        //                // (maybe we should also test the other way round: a single outline edge is identical to multiple intesrsection edges. 
        //                // But this case did not occur yet)
        //                foreach (Edge edg in kv.Value)
        //                {
        //                    List<List<Edge>> path = FindPath(faceToSplitEdges, edg.Vertex1, edg.Vertex2);
        //                    for (int i = 0; i < path.Count; i++)
        //                    {
        //                        bool identical = true;
        //                        for (int j = 0; j < path[i].Count; j++)
        //                        {
        //                            if (!SameOrPartialEdge(path[i][j], edg, precision))
        //                            {
        //                                identical = false;
        //                                break;
        //                            }
        //                        }
        //                        if (identical)
        //                        {
        //                            faceToSplitEdges.RemoveMany(path[i]);
        //                            if (path[i][0].StartVertex(faceToSplit) == edg.EndVertex(faceToSplit))
        //                            {
        //                                availableEdges.Remove(edg);
        //                            }
        //                            break;
        //                        }
        //                    }
        //                }
        //                availableEdges.AddMany(faceToSplitEdges);
        //                //foreach (Edge edg in faceToSplit.AllEdgesIterated())
        //                //{
        //                //    bool dontUseEdg = false;
        //                //    foreach (Edge tst in Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2))
        //                //    {
        //                //        if (availableEdges.Contains(tst) && SameEdge(tst, edg, precision))
        //                //        {   // the same edge is also an intersection edge
        //                //            dontUseEdg = true;
        //                //            if (tst.StartVertex(faceToSplit) == edg.EndVertex(faceToSplit))
        //                //            {
        //                //                // opposite direction: also remove intersection edge, because it is opposite to outline edge
        //                //                availableEdges.Remove(tst);
        //                //            }
        //                //            break;
        //                //        }
        //                //    }
        //                //    if (!dontUseEdg) usableOutlines.Add(edg); // normally we use the outline edge
        //                //}
        //                //availableEdges.AddMany(usableOutlines);
        //#if DEBUG
        //                Set<Vertex> allVtx = new Set<Vertex>();
        //                foreach (Edge edg in availableEdges)
        //                {
        //                    allVtx.Add(edg.Vertex1);
        //                    allVtx.Add(edg.Vertex2);
        //                }
        //                // Ausgangslage: alle Kanten in 2d mit Richtung
        //                DebuggerContainer dc0 = new CADability.DebuggerContainer();
        //                double arrowsize = faceToSplit.Area.GetExtent().Size / 100.0;
        //                //dc.Add(fc.DebugEdges2D.toShow);
        //                foreach (Edge edg in faceToSplit.AllEdgesIterated())
        //                {
        //                    dc0.Add(edg.Curve2D(faceToSplit), System.Drawing.Color.Blue, edg.GetHashCode());
        //                }
        //                foreach (Edge edg in kv.Value)
        //                {
        //                    dc0.Add(edg.Curve2D(faceToSplit), System.Drawing.Color.Red, edg.GetHashCode());
        //                    // noch einen Richtungspfeil zufügen
        //                    GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
        //                    GeoVector2D dir = edg.Curve2D(faceToSplit).DirectionAt(0.5).Normalized;
        //                    arrowpnts[1] = edg.Curve2D(faceToSplit).PointAt(0.5);
        //                    arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
        //                    arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
        //                    try
        //                    {
        //                        Polyline2D pl2d = new Polyline2D(arrowpnts);
        //                        dc0.Add(pl2d, System.Drawing.Color.Red, edg.GetHashCode());
        //                    }
        //                    catch { }
        //                }
        //                foreach (Vertex vtx in allVtx)
        //                {
        //                    GeoPoint2D vpos = vtx.GetPositionOnFace(faceToSplit);
        //                    Point pnt = Point.Construct();
        //                    pnt.Symbol = PointSymbol.Cross;
        //                    pnt.Location = new GeoPoint(vpos);
        //                    dc0.Add(pnt, vtx.GetHashCode());
        //                }
        //                Set<Edge> dbgset = availableEdges.Clone();
        //                availableEdges = dbgset.Clone(); // damit man wieder hierher zurückkann zum Debuggen
        //                                                 // *** dc0: 2d image of loops with direction: blue original outline and holes, red: intersection curves
        //#endif
        //                // Finde Zyklen, die linksrum gehen. Abhängig von der Operation muss man noch nacharbeiten
        //                List<List<Edge>> cycles = generateCycles(availableEdges, faceToSplit, precision);
        //                Dictionary<int, List<int>> outlinesToHoles = SortCycles(cycles, faceToSplit);
        //                // the following is not necessary, since SortCycles already removes empty cycles
        //                //List<int> outlineindices = new List<int>(outlinesToHoles.Keys);
        //                //// do not construct faces with empty outlines (self recurrent edges)
        //                //foreach (int oi in outlineindices)
        //                //{
        //                //    ICurve2D[] cycle2d = new ICurve2D[cycles[oi].Count];
        //                //    for (int i = 0; i < cycle2d.Length; i++)
        //                //    {
        //                //        cycle2d[i] = cycles[oi][i].Curve2D(faceToSplit);
        //                //    }
        //                //    if (Border.sArea(cycle2d) < faceToSplit.Area.GetExtent().Size * 1e-6) outlinesToHoles.Remove(oi);
        //                //}
        //                if (outlinesToHoles.Count == 0) continue;


        //                destroyedFaces.Add(faceToSplit); // das ist nicht mehr zu verwenden, auch wenn es keinen Zyklus enthält

        //                foreach (KeyValuePair<int, List<int>> oth in outlinesToHoles)
        //                {
        //                    Face fc = Face.Construct();
        //#if DEBUG
        //                    fc.UserData.Add("PartOf", faceToSplit.GetHashCode());
        //#endif
        //                    fc.CopyAttributes(faceToSplit);
        //                    Edge[] outline = cycles[oth.Key].ToArray();
        //                    for (int j = 0; j < outline.Length; j++)
        //                    {
        //                        outline[j].ReplaceFace(faceToSplit, fc);
        //                    }
        //                    // es kann Löcher geben, die außerhalb von outline liegen, oder solche, die innerhalb anderer Löcher liegen.
        //                    // Überschneidungen sind aber ausgeschlossen
        //                    Edge[][] holes = new Edge[oth.Value.Count][];
        //                    for (int j = 0; j < holes.Length; j++)
        //                    {
        //                        holes[j] = cycles[oth.Value[j]].ToArray();
        //                        for (int k = 0; k < holes[j].Length; k++)
        //                        {
        //                            holes[j][k].ReplaceFace(faceToSplit, fc);
        //                        }
        //                    }
        //                    // Surface wird hier gecloned, denn es können mehrere Faces mit der selben Surface entstehen
        //                    // und diese Surface würde sonst bei ReverseOrientation (bei union) ggf. mehrfach umgedreht
        //                    fc.Set(faceToSplit.Surface.Clone(), outline, holes);
        //#if DEBUG
        //                    SimpleShape dbga = fc.Area;
        //#endif
        //                    foreach (Edge edg in fc.AllEdgesIterated())
        //                    {
        //                        if (edg.Curve3D != null) edg.SurfaceChanged(faceToSplit.Surface, fc.Surface);
        //                    }
        //                    bool isOppositeFace = false;
        //                    if (oppositeCandidates.Count > 0)
        //                    {
        //                        List<Face> oppositeFaces;
        //                        if (oppositeCandidates.TryGetValue(faceToSplit, out oppositeFaces))
        //                        {
        //                            GeoPoint2D ip = fc.Area.GetSomeInnerPoint();
        //                            GeoPoint ip3d = fc.Surface.PointAt(ip);
        //                            for (int j = 0; j < oppositeFaces.Count; j++)
        //                            {
        //                                GeoPoint2D iponj = oppositeFaces[j].Surface.PositionOf(ip3d);
        //                                if (oppositeFaces[j].Area.Contains(iponj, false))
        //                                {
        //                                    isOppositeFace = true;
        //                                    break;
        //                                }
        //                            }
        //                        }
        //                    }
        //#if DEBUG
        //                    foreach (Edge edg in fc.AllEdgesIterated())
        //                    {
        //                        if (edg.Curve3D is InterpolatedDualSurfaceCurve)
        //                        {
        //                            if (edg.Forward(fc))
        //                            {
        //                                double d1 = edg.Curve3D.StartPoint | edg.StartVertex(fc).Position;
        //                                double d2 = edg.Curve3D.EndPoint | edg.EndVertex(fc).Position;
        //                                edg.Curve3D.StartPoint = edg.StartVertex(fc).Position;
        //                                edg.Curve3D.EndPoint = edg.EndVertex(fc).Position;
        //                            }
        //                            else
        //                            {
        //                                double d1 = edg.Curve3D.EndPoint | edg.StartVertex(fc).Position;
        //                                double d2 = edg.Curve3D.StartPoint | edg.EndVertex(fc).Position;
        //                            }
        //                        }
        //                    }
        //#endif
        //                    if (isOppositeFace)
        //                        destroyedFaces.Add(fc); // nicht verwenden
        //                    else
        //                    {
        //                        // es ist (bei overlapping) dummerweise möglich, dass beide Faces die gleichen trimmedFaces erzeugen
        //                        // die darf man dann nicht zweimal dazugeben
        //                        // ist es genügend, die Gleichheit der Vertices zu testen? auch noch Surface?
        //                        // Schade, dass fc.Vertices keinen Set liefert, es berechnet nämlich zuerst einen solchen
        //                        GeoPoint[] trianglePoint;
        //                        GeoPoint2D[] triangleUVPoint;
        //                        int[] triangleIndex;
        //                        BoundingCube triangleExtent;
        //                        fc.GetTriangulation(0.1, out trianglePoint, out triangleUVPoint, out triangleIndex, out triangleExtent);
        //                        Set<Vertex> svtx = new Set<Vertex>(fc.Vertices);
        //                        bool skip = false;
        //                        foreach (Face tfc in trimmedFaces)
        //                        {
        //                            if (svtx.IsEqualTo(new Set<Vertex>(tfc.Vertices)))
        //                            {
        //                                ModOp2D fts;
        //                                if (fc.Surface.SameGeometry(fc.Area.GetExtent(), tfc.Surface, tfc.Area.GetExtent(), precision, out fts))
        //                                {
        //                                    GeoPoint2D tuv = tfc.Area.GetSomeInnerPoint();
        //                                    GeoPoint t3d = tfc.Surface.PointAt(tuv);
        //                                    GeoPoint2D fcuv = fc.PositionOf(t3d);
        //                                    if (fc.Contains(ref fcuv, true))
        //                                    {
        //                                        if (Precision.SameNotOppositeDirection(tfc.Surface.GetNormal(tuv), fc.Surface.GetNormal(fcuv)))
        //                                        {
        //                                            skip = true;
        //                                            break;
        //                                        }
        //                                    }
        //                                }
        //                            }
        //                        }
        //                        if (skip) destroyedFaces.Add(fc);
        //                        else trimmedFaces.Add(fc); // hat schon die richtigen Kanten
        //                    }
        //                }
        //            }
        //            // *** trimmedFaces contains all faces that have been cut by intersection curves
        //            // opposite faces, which have not been split by intersection curves may reside inside or outside their opposite partner. 
        //            // If they are totally covered by the opposite face, then they may not be used
        //            foreach (DoubleFaceKey dfk in oppositeFaces.Keys)
        //            {
        //                // nur ein ganz vom anderen überdecktes face (da ein solches nicht zerschnitten wird) muss entfernt werden
        //                if (!destroyedFaces.Contains(dfk.face1))
        //                {   // überdeckt face2 face1?
        //                    Set<Vertex> toTest = new Set<Vertex>(dfk.face1.Vertices).Difference(new Set<Vertex>(dfk.face2.Vertices));
        //                    // alle Vertices von face1, die nicht auch noch in face2 sind.
        //                    // entweder sind alle innerhalb der Fläche von face2, oder alle außerhalb
        //                    if (toTest.Count == 0) destroyedFaces.Add(dfk.face1); // nicht sicher, ob diese Bedingung genügt
        //                    else
        //                    {
        //                        if (dfk.face2.Contains(dfk.face1.Surface.PointAt(dfk.face1.Area.GetSomeInnerPoint()), false)) destroyedFaces.Add(dfk.face1);
        //                        //Vertex tv = toTest.GetAny();
        //                        //GeoPoint2D uv = oppositeFaces[dfk].GetInverse() * tv.GetPositionOnFace(dfk.face1); // punkt von face1 im System von face2
        //                        //SurfaceHelper.AdjustPeriodic(dfk.face2.Surface, dfk.face2.Area.GetExtent(), ref uv);
        //                        //if (dfk.face2.Area.Contains(uv, false)) destroyedFaces.Add(dfk.face1);
        //                    }
        //                }
        //                if (!destroyedFaces.Contains(dfk.face2))
        //                {
        //                    Set<Vertex> toTest = new Set<Vertex>(dfk.face2.Vertices).Difference(new Set<Vertex>(dfk.face1.Vertices));
        //                    if (toTest.Count == 0) destroyedFaces.Add(dfk.face2); // nicht sicher, ob diese Bedingung genügt
        //                    else
        //                    {
        //                        if (dfk.face1.Contains(dfk.face2.Surface.PointAt(dfk.face2.Area.GetSomeInnerPoint()), false)) destroyedFaces.Add(dfk.face2);
        //                        // Vertex tv = toTest.GetAny();
        //                        // GeoPoint2D uv = oppositeFaces[dfk] * tv.GetPositionOnFace(dfk.face2); // punkt von face2 im System von face1
        //                        //SurfaceHelper.AdjustPeriodic(dfk.face1.Surface, dfk.face1.Area.GetExtent(), ref uv);
        //                        //if (dfk.face1.Area.Contains(uv, false)) destroyedFaces.Add(dfk.face2);
        //                    }
        //                }
        //            }
        //            //foreach (DoubleFaceKey dfk in overlappingFaces.Keys)
        //            //{
        //            //    destroyedFaces.Add(dfk.face1);
        //            //    destroyedFaces.Add(dfk.face2);
        //            //}
        //            // trimmedFaces kann Kanten enthalten, die nur ein Face haben, aber trotzdem mit andern Kanten von trimmedFaces zusammenfallen
        //            // diese sollen hier jetzt zusammengenäht werden (die Vertices sind ja eindeutig und nur einmal vorhanden)
        //            foreach (Face fc in trimmedFaces)
        //            {
        //                foreach (Edge edg in fc.AllEdgesIterated())
        //                {
        //                    if (destroyedFaces.Contains(edg.PrimaryFace)) edg.RemovePrimaryFace();
        //                    if (edg.SecondaryFace != null && destroyedFaces.Contains(edg.SecondaryFace)) edg.RemoveSecondaryFace();
        //                    if (edg.SecondaryFace == null)
        //                    {
        //                        foreach (Edge other in Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2))
        //                        {
        //                            if (other != edg && SameEdge(other, edg, precision))
        //                            {
        //                                if (trimmedFaces.Contains(other.PrimaryFace) && (other.SecondaryFace == null || !trimmedFaces.Contains(other.SecondaryFace)))
        //                                {
        //                                    if (other.SecondaryFace != null) destroyedFaces.Add(other.SecondaryFace);
        //                                    other.RemoveSecondaryFace();
        //                                    fc.ReplaceEdge(edg, other);
        //                                    other.UpdateInterpolatedDualSurfaceCurve();
        //                                }
        //                                else if ((other.SecondaryFace != null && trimmedFaces.Contains(other.SecondaryFace)) && !trimmedFaces.Contains(other.PrimaryFace))
        //                                {
        //                                    destroyedFaces.Add(other.PrimaryFace);
        //                                    other.RemovePrimaryFace();
        //                                    fc.ReplaceEdge(edg, other);
        //                                    other.UpdateInterpolatedDualSurfaceCurve();
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //#if DEBUG
        //                fc.CheckConsistency();
        //#endif
        //            }
        //            Set<Face> facesToAdd = new Set<Face>(trimmedFaces);
        //            Set<Face> allFaces = new Set<Face>();
        //            Set<Edge> toConnect = new Set<CADability.Edge>();
        //            Set<Edge> dontUse = new Set<Edge>();
        //            foreach (KeyValuePair<Edge, Edge> item in intsEdgeToEdgeShell1)
        //            {
        //                dontUse.Add(item.Value);
        //            }
        //            foreach (KeyValuePair<Edge, Edge> item in intsEdgeToEdgeShell2)
        //            {
        //                dontUse.Add(item.Value);
        //            }
        //            while (facesToAdd.Count > 0)
        //            {
        //                allFaces.AddMany(facesToAdd);
        //                Set<Face> moreFaces = new Set<Face>();
        //                foreach (Face fce in facesToAdd)
        //                {
        //                    foreach (Edge edg in fce.AllEdgesIterated())
        //                    {
        //                        if (!allFaces.Contains(edg.PrimaryFace) && !destroyedFaces.Contains(edg.PrimaryFace))
        //                        {
        //                            if (edgesNotToUse.Contains(edg))
        //                            {
        //                            }
        //                            else if (dontUse.Contains(edg))
        //                            {
        //                                edg.RemovePrimaryFace();
        //                            }
        //                            else
        //                            {
        //                                moreFaces.Add(edg.PrimaryFace);
        //                            }
        //                        }
        //                        if (edg.SecondaryFace != null && !allFaces.Contains(edg.SecondaryFace) && !destroyedFaces.Contains(edg.SecondaryFace))
        //                        {
        //                            if (edgesNotToUse.Contains(edg))
        //                            {
        //                            }
        //                            else if (dontUse.Contains(edg))
        //                            {
        //                                edg.RemoveSecondaryFace();
        //                            }
        //                            else
        //                            {
        //                                moreFaces.Add(edg.SecondaryFace);
        //                            }
        //                        }
        //                        if (edg.SecondaryFace == null && edg.edgeInfo != null && edg.edgeInfo.oldConnection != null && !destroyedFaces.Contains(edg.edgeInfo.oldConnection))
        //                        {
        //                            moreFaces.Add(edg.edgeInfo.oldConnection);
        //                        }
        //                        if (destroyedFaces.Contains(edg.SecondaryFace)) edg.RemoveSecondaryFace();
        //                        if (destroyedFaces.Contains(edg.PrimaryFace)) edg.RemovePrimaryFace();
        //                        if (edg.SecondaryFace == null) toConnect.Add(edg);
        //                    }
        //                }
        //                facesToAdd = moreFaces;
        //            }
        //            Set<Edge> allEdges = new Set<Edge>();
        //            foreach (Face fce in allFaces)
        //            {
        //                allEdges.AddMany(fce.AllEdgesIterated());
        //            }
        //            while (toConnect.Count > 0)
        //            {
        //                Edge edg = toConnect.GetAny();
        //                toConnect.Remove(edg);
        //                Set<Edge> connecting = new Set<Edge>(Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2));
        //                connecting.IntersectionWith(toConnect); // only the other open edges 
        //                connecting.RemoveAll(delegate (Edge e) // only the same geometry edges
        //                {
        //                    return !SameEdge(edg, e, precision);
        //                }
        //                );
        //                bool replaced = false;
        //                if (connecting.Count == 1)
        //                {
        //                    Edge edg1 = connecting.GetAny();
        //                    if (edg.Curve3D == null && edg1.Curve3D == null)
        //                    {   // zwei identische Pole
        //                        toConnect.Remove(edg1);
        //                        edg1.PrimaryFace.ReplaceEdge(edg1, edg);
        //                        replaced = true;
        //                    }
        //                    else
        //                    {
        //                        toConnect.Remove(edg1);
        //                        edg1.PrimaryFace.ReplaceEdge(edg1, edg);
        //                        replaced = true;
        //                    }
        //                }
        //                if (!replaced)
        //                {
        //                    connecting = new Set<Edge>(Vertex.ConnectingEdges(edg.Vertex1, edg.Vertex2));
        //                    connecting.Remove(edg);
        //                    foreach (Edge edg1 in connecting)
        //                    {
        //                        if ((edg.PrimaryFace == edg1.PrimaryFace || edg.PrimaryFace == edg1.SecondaryFace) && SameEdge(edg, edg1, precision))
        //                        {
        //                            edg.PrimaryFace.ReplaceEdge(edg, edg1);
        //                        }
        //                    }
        //                }
        //                if (edg.Curve3D is BSpline && edg.SecondaryFace != null)
        //                {
        //                    // InterpolatedDualSurfaceCurve is convertet to BSpline when new edges are created with overlappingFaces.
        //                    // These edges may be reconverted to InterpolatedDualSurfaceCurve, if the two involved surfaces are not tangential
        //                    GeoPoint2D uv1s = edg.Vertex1.GetPositionOnFace(edg.PrimaryFace);
        //                    GeoPoint2D uv1e = edg.Vertex2.GetPositionOnFace(edg.PrimaryFace);
        //                    GeoPoint2D uv2s = edg.Vertex1.GetPositionOnFace(edg.SecondaryFace);
        //                    GeoPoint2D uv2e = edg.Vertex2.GetPositionOnFace(edg.SecondaryFace);
        //                    GeoVector n1s = edg.PrimaryFace.Surface.GetNormal(uv1s);
        //                    GeoVector n1e = edg.PrimaryFace.Surface.GetNormal(uv1e);
        //                    GeoVector n2s = edg.SecondaryFace.Surface.GetNormal(uv2s);
        //                    GeoVector n2e = edg.SecondaryFace.Surface.GetNormal(uv2e);
        //                    if ((new Angle(n1s, n2s)).Radian < 0.1 || (new Angle(n1e, n2e)).Radian < 0.1)
        //                    {   // surfaces are tangential, no action necessary
        //                        // maybe the surfaces are identical and the edge will be eliminated later
        //                    }
        //                    else
        //                    {
        //                        if (edg.SecondaryCurve2D is InterpolatedDualSurfaceCurve.ProjectedCurve)
        //                        {
        //                            edg.Curve3D = (edg.SecondaryCurve2D as InterpolatedDualSurfaceCurve.ProjectedCurve).Curve3D;
        //                            edg.PrimaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface1;
        //                        }
        //                        else if (edg.PrimaryCurve2D is InterpolatedDualSurfaceCurve.ProjectedCurve)
        //                        {
        //                            edg.Curve3D = (edg.PrimaryCurve2D as InterpolatedDualSurfaceCurve.ProjectedCurve).Curve3D;
        //                            edg.SecondaryCurve2D = (edg.Curve3D as InterpolatedDualSurfaceCurve).CurveOnSurface2;
        //                        }
        //                    }
        //                }
        //            }

        //#if DEBUG
        //            foreach (Face fce in allFaces)
        //            {
        //                foreach (Edge edg in fce.AllEdges)
        //                {
        //                    if (edg.Curve3D is InterpolatedDualSurfaceCurve)
        //                    {
        //                        (edg.Curve3D as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
        //                    }
        //                }
        //            }
        //            foreach (Face fce in allFaces)
        //            {
        //                System.Diagnostics.Debug.Assert(fce.CheckConsistency());
        //            }
        //#endif
        //            foreach (Edge edg in allEdges)
        //            {
        //                edg.UpdateInterpolatedDualSurfaceCurve();
        //            }
        //            // allFaces ist eine nicht unbedingt zusammenhängende Menge von Faces, die jetzt zu einer oder mehreren Shells 
        //            // sortiert werden (das könnte in der vorigen Schleife gleich mit erledigt werden; oder?)
        //            while (allFaces.Count > 0)
        //            {
        //                Set<Face> sf = BRepOperation.extractConnectedFaces(allFaces, allFaces.GetAny());
        //                //Shell[] dbg = Make3D.SewFaces(sf.ToArray());
        //                Shell shell = Shell.MakeShell(sf.ToArray(), true);
        //                Set<Edge> openEdges = new Set<Edge>(shell.OpenEdges);
        //                if (openEdges.Count > 0) shell.TryConnectOpenEdges();
        //                openEdges = new Set<Edge>(shell.OpenEdges);
        //                bool ok = true;
        //                foreach (Edge edg in openEdges)
        //                {
        //                    if (edg.Curve3D != null) ok = false;

        //                }
        //                if (ok) res.Add(shell);
        //            }
        //#if DEBUG
        //            for (int i = 0; i < res.Count; i++)
        //            {
        //                foreach (Face fce in res[i].Faces)
        //                {
        //                    System.Diagnostics.Debug.Assert(fce.CheckConsistency());
        //                }
        //            }
        //#endif
        //            if (operation == Operation.union)
        //            {
        //                for (int i = 0; i < res.Count; i++)
        //                {
        //                    res[i].ReverseOrientation();
        //                }
        //            }
        //            for (int i = 0; i < res.Count; i++)
        //            {
        //                foreach (Edge edg in res[i].Edges)
        //                {
        //                    if (edg.Curve3D is IGeoObject)
        //                    {
        //                        (edg.Curve3D as IGeoObject).UserData.Remove("DebugIntersectionBy1");
        //                        (edg.Curve3D as IGeoObject).UserData.Remove("DebugIntersectionBy2");
        //                    }
        //                }
        //                foreach (Face fce in res[i].Faces)
        //                {
        //                    fce.UserData.Remove("PartOf");
        //                    fce.UserData.Remove("BRepIntersection.Cycles");
        //                    fce.UserData.Remove("BRepIntersection.UncheckedEdges");
        //                }
        //            }
        //#if DEBUG
        //            //for (int i = 0; i < res.Count; i++)
        //            //{
        //            //    System.Diagnostics.Debug.Assert(res[i].CheckConsistency());
        //            //}
        //#endif
        //            for (int i = 0; i < res.Count; i++)
        //            {
        //                res[i].CombineConnectedFaces();
        //            }
        //#if DEBUG
        //            //for (int i = 0; i < res.Count; i++)
        //            //{
        //            //    System.Diagnostics.Debug.Assert(res[i].CheckConsistency());
        //            //}
        //#endif
        //            // der Aufrufer muss die Lage der Shells zueinander überprüfen und ggf. Solids mit Löchern daraus machen
        //#if DEBUG
        //            //Shell dbgs = (Shell)Project.SerializeDeserialize(res[0]);
        //#endif
        //            return res.ToArray();
        //        }

        private List<List<Edge>> FindPath(Set<Edge> set, Vertex vertex1, Vertex vertex2)
        {   // find one or more paths (or none of course) of connected edges from the provided set, which goes from vertex1 to vertex2
            List<List<Edge>> res = new List<List<Edge>>();
            Set<Edge> startWith = vertex1.AllEdges.Intersection(set);
            foreach (Edge edg in startWith)
            {
                Vertex endVertex = edg.OtherVertex(vertex1);
                if (endVertex == vertex2)
                {
                    List<Edge> singleEdge = new List<Edge>();
                    singleEdge.Add(edg);
                    res.Add(singleEdge);
                }
                else
                {
                    Set<Edge> usable = set.Clone();
                    usable.Remove(edg);
                    List<List<Edge>> secondPart = FindPath(usable, endVertex, vertex2);
                    foreach (List<Edge> le in secondPart)
                    {
                        le.Insert(0, edg);
                    }
                    res.AddRange(secondPart);
                }
            }
            return res;
        }

        internal static bool SameEdge(Edge e1, Edge e2, double precision)
        {   // it is assumed that the two edges connect the same vertices 
            // it is tested whether they have the same geometry (but maybe different directions) 
            // (two half circles may connect the same vertices but are not geometrically identical when they describe differnt parts of the same circle)
            if (e1.Curve3D != null && e2.Curve3D != null) return e1.Curve3D.DistanceTo(e2.Curve3D.PointAt(0.5)) < 10 * precision; // nur precision war zu knapp
            return false;
        }

        internal static bool SameOrPartialEdge(Edge part, Edge full, double precision)
        {   // tests whether the two edges connect the same vertices and 
            // have the same geometry (but maybe different directions) (two half circles may connect the same vertices but are not geometrically identical
            // when they describe differnt parts of the same circle)
            if (part.Curve3D != null && full.Curve3D != null)
            {
                if (full.Curve3D.DistanceTo(part.Vertex1.Position) > 10 * precision) return false;
                if (full.Curve3D.DistanceTo(part.Vertex2.Position) > 10 * precision) return false;
                if (full.Curve3D.DistanceTo(part.Curve3D.PointAt(0.5)) > 10 * precision) return false;
                return true;
            }
            return false;
        }

        internal static IEnumerable<Pair<Edge, Edge>> EdgePairs(IList<Edge> edges)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                if (i == 0)
                    yield return new Pair<Edge, Edge>(edges[edges.Count - 1], edges[0]);
                else
                    yield return new Pair<Edge, Edge>(edges[i - 1], edges[i]);
            }
        }

        internal static List<List<Edge>> generateCycles(Set<Edge> edgesToUse, Face onThisFace, double precision)
        {
#if DEBUG
            Set<Edge> edgesToUseClone = new Set<Edge>(edgesToUse);
#endif
            Set<Edge> poles = new Set<Edge>(); // poles (e.g. on a sphere) will not be correct connected. But they are not intersected
            List<List<Edge>> cycles = new List<List<Edge>>();
            Set<Vertex> allVertices = new Set<Vertex>();
            foreach (Edge edg in edgesToUse)
            {
                allVertices.Add(edg.Vertex1);
                allVertices.Add(edg.Vertex2);
                if (edg.edgeInfo == null) edg.edgeInfo = new EdgeInfo(edg);
                else
                {
                    edg.edgeInfo.next = null;
                    edg.edgeInfo.prev = null;
                }
                if (edg.Vertex1 == edg.Vertex2) poles.Add(edg);
            }
            // ACHTUNG: Pole machen hier noch Probleme. Mit BRepTest8 testen!!!
            foreach (Vertex vtx in allVertices)
            {
                Set<Edge> outgoing = vtx.ConditionalEdgesSet(delegate (Edge e)
                {   // die relevanten Edges
                    if (!edgesToUse.Contains(e)) return false;
                    if (e.PrimaryFace == onThisFace) return true;
                    if (e.SecondaryFace == onThisFace) return true;
                    return false;
                });
                Edge[] oa = outgoing.ToArray();
                //for (int i = 0; i < oa.Length - 1; i++)
                //{
                //    for (int j = i + 1; j < oa.Length; j++)
                //    {
                //        if (oa[i].OtherVertex(vtx) == oa[j].OtherVertex(vtx))
                //        {
                //            if (SameEdge(oa[i], oa[j], precision))
                //            {
                //                if (oa[i].StartVertex(onThisFace) == oa[j].StartVertex(onThisFace))
                //                {   // gleich oreintiert
                //                    edgesToUse.Remove(oa[j]);
                //                    outgoing.Remove(oa[j]);
                //                }
                //                else
                //                {   // gegenläufig: beide entfernen
                //                    edgesToUse.Remove(oa[i]);
                //                    outgoing.Remove(oa[i]);
                //                    edgesToUse.Remove(oa[j]);
                //                    outgoing.Remove(oa[j]);
                //                }
                //            }
                //        }
                //    }
                //}
                // keine doppelten Edges mehr in outgoing
                if (outgoing.Count == 0) continue;
                else if (outgoing.Count == 1)
                {
                    Edge e = outgoing.GetAny();
                    if (e.StartVertex(onThisFace) == vtx) e.edgeInfo.prev = null;
                    else e.edgeInfo.next = null;
                }
                else if (outgoing.Count == 2)
                {   // der häufigste Fall
                    IEnumerator<Edge> en = outgoing.GetEnumerator();
                    en.MoveNext();
                    Edge e1 = en.Current;
                    en.MoveNext();
                    Edge e2 = en.Current;
                    bool e1out = e1.StartVertex(onThisFace) == vtx;
                    bool e2out = e2.StartVertex(onThisFace) == vtx;
                    if (e1out && !e2out)
                    {
                        e2.edgeInfo.next = e1;
                        e1.edgeInfo.prev = e2;
                    }
                    else if (!e1out && e2out)
                    {
                        e2.edgeInfo.prev = e1;
                        e1.edgeInfo.next = e2;
                    }
                    else if (e1out && e2out)
                    {
                        e2.edgeInfo.prev = null;
                        e1.edgeInfo.prev = null;
                    }
                    else
                    {
                        e1.edgeInfo.next = null;
                        e2.edgeInfo.next = null;
                    }
                }
                else
                {
                    SortedList<double, Edge> antiClockwiseEdges = new SortedList<double, Edge>();
                    bool doCircleTest = false;
                    foreach (Edge edg in outgoing)
                    {
                        ICurve2D c2d = edg.Curve2D(onThisFace);
                        double ang; // Winkel zwischen 0 und 2*PI
                        GeoPoint2D cnt = vtx.GetPositionOnFace(onThisFace);
                        if ((c2d.EndPoint | cnt) > (c2d.StartPoint | cnt)) ang = c2d.StartDirection.Angle;
                        else ang = (-c2d.EndDirection).Angle;
                        if (antiClockwiseEdges.ContainsKey(ang))
                        {
                            doCircleTest = true;
                            break;
                        }
                        antiClockwiseEdges.Add(ang, edg);
                    }
                    double frst = -1.0, last = 0.0;
                    if (!doCircleTest)
                    {
                        foreach (KeyValuePair<double, Edge> kv in antiClockwiseEdges)
                        {
                            if (frst < 0) frst = last = kv.Key;
                            else
                            {
                                if (kv.Key - last < 0.01)
                                {
                                    doCircleTest = true;
                                    break;
                                }
                                else
                                    last = kv.Key;
                            }
                        }
                    }
                    if (!doCircleTest) doCircleTest = (last - frst) > Math.PI * 2.0 - 0.01;
                    if (doCircleTest)
                    {
                        // maybe we have two edges, which are identical but opposite
                        // then we have to have the one leaving the vertex after the one entering it.
                        Set<Edge> leaving = new Set<Edge>();
                        foreach (Edge edg in outgoing)
                        {
                            foreach (Edge edg1 in outgoing)
                            {
                                if (edg != edg1 && SameEdge(edg, edg1, precision))
                                {
                                    if (edg.StartVertex(onThisFace) == vtx) leaving.Add(edg);
                                    else leaving.Add(edg1);
                                }
                            }
                        }
                        double minDist = double.MaxValue;
                        GeoPoint2D cnt = vtx.GetPositionOnFace(onThisFace);
                        foreach (Edge edg in outgoing)
                        {
                            ICurve2D c2d = edg.Curve2D(onThisFace);
                            minDist = Math.Min(minDist, Math.Max(c2d.EndPoint | cnt, c2d.StartPoint | cnt));
                        }
                        Circle2D circle2d = new Circle2D(cnt, minDist / 2.0);
                        antiClockwiseEdges.Clear();
                        // wenn es zwei gleiche gibt, dann zuerst der, der reingeht, dann der, der rausgeht
                        foreach (Edge edg in outgoing)
                        {
                            ICurve2D c2d = edg.Curve2D(onThisFace);
                            GeoPoint2DWithParameter[] ips = circle2d.Intersect(c2d);
                            SweepAngle sw = 0.0;
                            for (int i = 0; i < ips.Length; i++)
                            {
                                if (ips[i].par2 > 0.0 && ips[i].par2 < 1.0)
                                {
                                    double ang = (ips[i].p - cnt).Angle; // 0<=ang<2*PI
                                    if (leaving.Contains(edg))
                                    {
                                        ang += 1e-6; // this should be big egnough to seperate from the same but opposite edge
                                                     // and small egnough to not overtake the following
                                        if (ang >= 2.0 * Math.PI) ang -= 2.0 * Math.PI;
                                    }
                                    while (antiClockwiseEdges.ContainsKey(ang)) ang = Geometry.NextDouble(ang);
                                    // still same angle, no problem, the edges certainly have different directions
                                    antiClockwiseEdges.Add(ang, edg);
                                    break;
                                }
                            }
                        }
                    }
                    // antiClockwiseEdges contains all edges sorted anticklockwise around vtx on onThisFace
                    //List<Edge> acwe = new List<Edge>(antiClockwiseEdges.Values);
                    //for (int i = 0; i < acwe.Count; i++)
                    //{
                    //    if (vtx == acwe[i].StartVertex(onThisFace))
                    //    {
                    //        for (int j = 0; j < acwe.Count - 1; j++)
                    //        {
                    //            int k = (i + j + 1) % acwe.Count;
                    //            if (vtx == acwe[k].EndVertex(onThisFace) && !SameEdge(acwe[k],acwe[i],precision))
                    //            {
                    //                acwe[i].edgeInfo.prev = acwe[k];
                    //                acwe[k].edgeInfo.next = acwe[i];
                    //                break;
                    //            }
                    //        }
                    //    }
                    //    else
                    //    {
                    //        for (int j = 0; j < acwe.Count - 1; j++)
                    //        {
                    //            int k = (i - j - 1) % acwe.Count;
                    //            if (k < 0) k += acwe.Count;
                    //            if (vtx == acwe[k].StartVertex(onThisFace) && !SameEdge(acwe[k], acwe[i], precision))
                    //            {
                    //                acwe[i].edgeInfo.next = acwe[k];
                    //                acwe[k].edgeInfo.prev = acwe[i];
                    //                break;
                    //            }
                    //        }
                    //    }

                    //}
                    foreach (Pair<Edge, Edge> ep in EdgePairs(antiClockwiseEdges.Values))
                    {
                        Edge e1 = ep.First;
                        Edge e2 = ep.Second;
                        bool e1out = e1.StartVertex(onThisFace) == vtx;
                        bool e2out = e2.StartVertex(onThisFace) == vtx;
                        if (e1out && !e2out)
                        {
                            e2.edgeInfo.next = e1;
                            e1.edgeInfo.prev = e2;
                        }   // alle anderen Fälle bleiben null
                    }
                }
                // jetzt haben alle edges prev und next gesetzt, es sei denn, es sind Sackgassen
            }
            // Setting next and prev for poles
            foreach (Edge edg in poles)
            {
                bool found = false;
                for (int i = 0; i < onThisFace.OutlineEdges.Length; i++)
                {
                    if (onThisFace.OutlineEdges[i] == edg)
                    {
                        int next = i + 1;
                        if (next >= onThisFace.OutlineEdges.Length) next = 0;
                        edg.edgeInfo.next = onThisFace.OutlineEdges[(i + 1) % onThisFace.OutlineEdges.Length];
                        edg.edgeInfo.prev = onThisFace.OutlineEdges[(i + onThisFace.OutlineEdges.Length - 1) % onThisFace.OutlineEdges.Length];
                        edg.edgeInfo.prev.edgeInfo.next = edg;
                        edg.edgeInfo.next.edgeInfo.prev = edg;
                        found = true;
                        break;
                    }

                }
                for (int i = 0; i < onThisFace.HoleCount; i++)
                {
                    if (found) break;
                    for (int j = 0; j < onThisFace.HoleEdges(i).Length; j++)
                    {
                        if (onThisFace.HoleEdges(i)[j] == edg)
                        {
                            edg.edgeInfo.next = onThisFace.OutlineEdges[(j + 1) % onThisFace.HoleEdges(i).Length];
                            edg.edgeInfo.prev = onThisFace.OutlineEdges[(j + onThisFace.HoleEdges(i).Length - 1) % onThisFace.HoleEdges(i).Length];
                            edg.edgeInfo.prev.edgeInfo.next = edg;
                            edg.edgeInfo.next.edgeInfo.prev = edg;
                            found = true;
                            break;
                        }
                    }
                }
            }
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            foreach (Edge edg in edgesToUseClone)
            {
                ICurve2D c2d = edg.Curve2D(onThisFace);
                if (edg.edgeInfo.next != null) dc.Add(c2d, System.Drawing.Color.Red, edg.GetHashCode() + 10000 * edg.edgeInfo.next.GetHashCode());
                else dc.Add(c2d, System.Drawing.Color.Red, edg.GetHashCode());
            }
#endif
            while (edgesToUse.Count > 0)
            {
                List<Edge> cycle = new List<Edge>();
                cycle.Add(edgesToUse.GetAny());
                edgesToUse.Remove(cycle[0]);
                while (cycle[cycle.Count - 1].edgeInfo.next != cycle[0])
                {
                    Edge next = cycle[cycle.Count - 1].edgeInfo.next;
                    if (next == null) break;
                    cycle.Add(next);
                    edgesToUse.Remove(next);
                }
                if (cycle[cycle.Count - 1].edgeInfo.next != null) cycles.Add(cycle);
            }
            /* alter Text:
            // geometrisch identische Edges werden gesondert untersucht:
            // laufen sie in sich zurück, können sie gänzlich ignoriert werden (zwei sich berührende Löcher
            // sind sie gleich orientiert, könnte es sich um die Berührung einer positiv orientierten Outline innerhalb der äußeren Outline
            // handeln. diese kante ist dann nur einmal zu verwenden
            Edge[] toCheck = edgesToUse.ToArray();
            for (int i = 0; i < toCheck.Length - 1; i++)
            {
                for (int j = i + 1; j < toCheck.Length; j++)
                {
                    if ((toCheck[j].Vertex1 == toCheck[i].Vertex1 && toCheck[j].Vertex2 == toCheck[i].Vertex2) || (toCheck[j].Vertex2 == toCheck[i].Vertex1 && toCheck[j].Vertex1 == toCheck[i].Vertex2))
                    {
                        if (SameEdge(toCheck[i], toCheck[j], precision))
                        {
                            ICurve2D c2d = toCheck[i].Curve2D(onThisFace);
                            ICurve2D c2d1 = toCheck[j].Curve2D(onThisFace);
                            if ((c2d.StartPoint | c2d1.StartPoint) < (c2d.EndPoint | c2d1.StartPoint))
                            {
                                // selbe Orientierung
                                edgesToUse.Remove(toCheck[j]); // nur eine muss weg, die andere ist ein Duplikat, welche, das ist egal
                                break;
                            }
                            else
                            {   // gegenläufig
                                edgesToUse.Remove(toCheck[i]); // beide müssen weg, da gegenläufig
                                edgesToUse.Remove(toCheck[j]);
                                break;
                            }
                        }
                    }
                }
            }
            Set<Edge> total = edgesToUse.Clone();
            while (edgesToUse.Count > 0)
            {
                Edge startEdge = null; // Problem (in breps8, Seite mit 2 Löchern): ein durch Schnitt entstandene Umrandung berührt in einm vertex ein Loch des originals
                                       // dann stoßen wir beim Start mit einer originaledge auf ein Problem, nämlich die Schnittumrandung wird entfernt
                foreach (Edge edg in edgesToUse)
                {
                    if (edg.edgeInfo != null && edg.edgeInfo.isIntersection)
                    {
                        startEdge = edg;
                        break;
                    }
                }
                if (startEdge == null) startEdge = edgesToUse.GetAny();
                ICurve2D startCurve = startEdge.Curve2D(onThisFace);
                GeoVector2D startDir = startCurve.StartDirection;
                Vertex startVertex = startEdge.StartVertex(onThisFace);
                Vertex endVertex = startEdge.EndVertex(onThisFace);
                List<Edge> cycle = new List<Edge>();
                cycle.Add(startEdge);
                edgesToUse.Remove(startEdge);
                Set<Vertex> usedvertices = new Set<Vertex>();
                usedvertices.Add(startVertex);
                usedvertices.Add(endVertex);
                while (endVertex != startVertex)
                {
                    // war: Set<Edge> outgoing = edgesToUse.Intersection(endVertex.AllEdges);
                    // der Fehler: wir müssen alle Kanten untersuchen, sonst lösen wir ggf. esrt den güligen Kern heraus (Quadrat mit 4 nach innen versetzten Linien)
                    // und der Rest ist danach auch gültig
                    Set<Edge> outgoing = endVertex.ConditionalEdgesSet(delegate (Edge e)
                    {   // die relevanten Edges
                        if (e == startEdge) return false;
                        //if (!edgesToUse.Contains(e)) return false; // eingeführt wg. doppelter edges bei overlapping. macht evtl. bei SelfIntersection Probleme?
                        // wieder entfernt, stattdessen auf total prüfen
                        if (!total.Contains(e)) return false;
                        if (e.PrimaryFace == onThisFace) return true;
                        if (e.SecondaryFace == onThisFace) return true;
                        return false;
                    });
                    if (outgoing.Count == 0) break; // Sackgasse
                    Edge bestEdge = null;
                    double maxangle = -Math.PI;
                    GeoPoint2D currentEndPoint = startCurve.EndPoint;
                    GeoVector2D currentEndDir = startCurve.EndDirection;
                    bool bestIsReverse = false;
                    bool useCircleTest = false;
                    foreach (Edge edg in outgoing)
                    {
                        ICurve2D c2d = edg.Curve2D(onThisFace);
                        if ((c2d.StartPoint | currentEndPoint) < (c2d.EndPoint | currentEndPoint))
                        {
                            // die 2d Kurve läuft richtig rum
                            SweepAngle sw = new SweepAngle(currentEndDir, c2d.StartDirection);
                            if (Math.Abs(Math.Abs(sw.Radian) - Math.PI) < 0.01) // geht genau zurück
                            {
                                // hier eine andere methode verwenden: einen Kreis mit Radius minDist (noch zu bestimmen) um currentEndPoint legen
                                // und alle Schnittpunkte bestimmen. Den Winkel zu diesen Schnittpunkten berechnen
                                useCircleTest = true;
                                break;
                            }
                            if (Math.Abs(sw.Radian - maxangle) < 1e-6)
                            {   // zwei gleiche gehen ab
                                useCircleTest = true;
                                break;
                            }
                            else if (sw.Radian > maxangle)
                            {
                                maxangle = sw.Radian;
                                bestEdge = edg;
                                bestIsReverse = false;
                            }
                        }
                        else
                        {
                            // die 2d Kurve läuft verkehrt herum
                            SweepAngle sw = new SweepAngle(currentEndDir, -c2d.EndDirection);
                            if (Math.Abs(Math.Abs(sw.Radian) - Math.PI) < 0.01) // geht genau zurück
                            {
                                // hier eine andere methode verwenden: 
                                useCircleTest = true;
                                break;
                            }
                            if (Math.Abs(sw.Radian - maxangle) < 1e-6)
                            {   // zwei gleiche gehen ab
                                useCircleTest = true;
                                break;
                            }
                            else if (sw.Radian > maxangle)
                            {
                                maxangle = sw.Radian;
                                bestEdge = edg;
                                bestIsReverse = true;
                            }
                        }
                    }
                    if (useCircleTest)
                    {
                        // es gibt eine fast in sich zurücklaufende Kurve, deren Abzweigungsrichtung nicht gut bestimmbar ist. Hier ein anderer Test
                        // einen Kreis mit Radius minDist (halber Abstand des nächstgelegenen Vertex) um currentEndPoint legen
                        // und alle Schnittpunkte bestimmen. Den Winkel zu diesen Schnittpunkten berechnen. Das geht sicher gut, da sich die Kurven nicht schneiden.
                        double minDist = double.MaxValue;
                        foreach (Edge edg in outgoing)
                        {
                            ICurve2D c2d = edg.Curve2D(onThisFace);
                            if ((c2d.StartPoint | currentEndPoint) < (c2d.EndPoint | currentEndPoint))
                            {
                                minDist = Math.Min(minDist, c2d.EndPoint | currentEndPoint);
                            }
                            else
                            {
                                minDist = Math.Min(minDist, c2d.StartPoint | currentEndPoint);
                            }
                        }
                        Circle2D circle2d = new Circle2D(currentEndPoint, minDist / 2.0);
                        GeoPoint2DWithParameter[] ips = circle2d.Intersect(startCurve);
                        for (int i = 0; i < ips.Length; i++)
                        {
                            if (ips[i].par2 > 0.0 && ips[i].par2 < 1.0)
                            {
                                currentEndDir = currentEndPoint - ips[i].p;
                                break;
                            }
                        }

                        bestEdge = null;
                        maxangle = -Math.PI;
                        foreach (Edge edg in outgoing)
                        {
                            ICurve2D c2d = edg.Curve2D(onThisFace);
                            if ((c2d.StartPoint | currentEndPoint) < (c2d.EndPoint | currentEndPoint))
                            {
                                // die 2d Kurve läuft richtig rum
                                ips = circle2d.Intersect(c2d);
                                SweepAngle sw = 0.0;
                                for (int i = 0; i < ips.Length; i++)
                                {
                                    if (ips[i].par2 > 0.0 && ips[i].par2 < 1.0)
                                    {
                                        sw = new SweepAngle(currentEndDir, ips[i].p - currentEndPoint);
                                        break;
                                    }
                                }
                                if (sw.Radian > maxangle)
                                {
                                    maxangle = sw.Radian;
                                    bestEdge = edg;
                                    bestIsReverse = false;
                                }
                            }
                            else
                            {
                                // die 2d Kurve läuft verkehrt herum
                                ips = circle2d.Intersect(c2d);
                                SweepAngle sw = 0.0;
                                for (int i = 0; i < ips.Length; i++)
                                {
                                    if (ips[i].par2 > 0.0 && ips[i].par2 < 1.0)
                                    {
                                        sw = new SweepAngle(currentEndDir, ips[i].p - currentEndPoint);
                                        if (Math.Abs(sw.Radian - Math.PI) < 1e-2)
                                        {   // exakter Rücklauf, das könnte doppelte Kante sein
                                            sw = -Math.PI;
                                        }
                                        break;
                                    }
                                }
                                if (sw.Radian > maxangle)
                                {
                                    maxangle = sw.Radian;
                                    bestEdge = edg;
                                    bestIsReverse = true;
                                }
                            }
                        }

                    }
                    if (bestIsReverse) break; // es geht in die falsche Richtung weiter, alles verwerfen
                    else
                    {
                        if (!edgesToUse.Contains(bestEdge)) break; // das, mit dem es weitergehen soll, ist schon verbraucht.
                                                                   // das kann vorkommen, wenn zwei Zyklen gemeinsame Vertices haben
                        cycle.Add(bestEdge);
                        startEdge = bestEdge;
                        startCurve = startEdge.Curve2D(onThisFace);
                        endVertex = startEdge.EndVertex(onThisFace);
                        usedvertices.Add(endVertex);
                        edgesToUse.Remove(startEdge);
                    }
                }
                if (endVertex == startVertex)
                {
                    cycles.Add(cycle);
                }
            } */
            for (int j = 0; j < cycles.Count; j++)
            {   // check, whether a cycle has a vertex, which is used mor than twice. Like in a "8", consisting of 4 180° arcs and 3 vertices.
                // these cycles are splitted into two or more closed subcycles.
                Set<Vertex> findDuplicateUsedvertex = new Set<Vertex>();
                int ind = -1;
                for (int i = 0; i < cycles[j].Count; i++)
                {
                    if (poles.Contains(cycles[j][i])) continue;
                    if (findDuplicateUsedvertex.Contains(cycles[j][i].EndVertex(onThisFace)))
                    {
                        ind = (i + 1) % cycles[j].Count;
                        break;
                    }
                    else
                    {
                        findDuplicateUsedvertex.Add(cycles[j][i].EndVertex(onThisFace));
                    }
                }
                if (ind >= 0)
                {
                    Vertex sv = cycles[j][ind].StartVertex(onThisFace);
                    int startRemove = ind;
                    int endRemove = -1;
                    List<Edge> subCycle = new List<Edge>();
                    do
                    {
                        subCycle.Add(cycles[j][ind]);
                        ind = (ind + 1) % cycles[j].Count;
                        endRemove = ind;
                    }
                    while (cycles[j][ind].StartVertex(onThisFace) != sv);
                    if (startRemove < endRemove)
                    {
                        cycles[j].RemoveRange(startRemove, endRemove - startRemove);
                    }
                    else
                    {
                        cycles[j].RemoveRange(startRemove, cycles[j].Count - startRemove);
                        cycles[j].RemoveRange(0, endRemove);
                    }
                    cycles.Add(cycles[j]);
                    cycles.Add(subCycle);
                    cycles.RemoveAt(j); // do not proceed in j, because jth entry has just been removed
                    --j;
                }
            }

#if DEBUG
            // Ausgangslage: alle Kanten in 2d mit Richtung
            DebuggerContainer dc0 = new CADability.DebuggerContainer();
            double arrowsize = onThisFace.Area.GetExtent().Size / 100.0;
            for (int i = 0; i < cycles.Count; i++)
            {
                foreach (Edge edg in cycles[i])
                {
                    dc0.Add(edg.Curve2D(onThisFace), System.Drawing.Color.Red, i);
                    // noch einen Richtungspfeil zufügen
                    GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                    GeoVector2D dir = edg.Curve2D(onThisFace).DirectionAt(0.5).Normalized;
                    arrowpnts[1] = edg.Curve2D(onThisFace).PointAt(0.5);
                    arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                    arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                    Polyline2D pl2d = new Polyline2D(arrowpnts);
                    dc0.Add(pl2d, System.Drawing.Color.Red, edg.GetHashCode());
                }
            }
#endif
            return cycles;
        }

        internal static Dictionary<int, List<int>> SortCycles(List<List<Edge>> cycles, Face onThisFace)
        {
            Cycle[] cycles2d = new Cycle[cycles.Count];
            for (int i = 0; i < cycles.Count; i++)
            {
                cycles2d[i] = new Cycle();
                for (int j = 0; j < cycles[i].Count; j++)
                {
                    cycles2d[i].Add(cycles[i][j].Curve2D(onThisFace));
                }
                cycles2d[i].calcOrientation();
            }
            // nicht sortieren, muss synchron sein mit cycles
            //Array.Sort(cycles2d,delegate (Cycle c1, Cycle c2)
            //{
            //    return c2.Area.CompareTo(c1.Area);
            //});
#if DEBUG
            DebuggerContainer dc = new CADability.DebuggerContainer();
            for (int i = 0; i < cycles2d.Length; i++)
            {
                for (int j = 0; j < cycles2d[i].Count; j++)
                {
                    ICurve2D c2d = cycles2d[i][j];
                    // dc.Add(c2d, System.Drawing.Color.Red, cycles[i][j].GetHashCode());
                    dc.Add(c2d, System.Drawing.Color.Red, i);
                }
            }
#endif
            // es gibt u.U. mehrere Cycles. Die Orientierung stimmt schon, daran kann man Umrandungen und Löcher erkennen
            // Wir brauchen eine Zuordnung der (counterclock) Umrandungen zu den (nicht counterclock) Löchern
            // zu jedem Loch wird die nächstgrößere, es umgebende Umrandung gesucht
            // Außerdem gilt noch: wenn die unveränderte Umrandung (das ist auch immer die größte) eine weitere (counterclock) Umrandung enthält, dann gilt die äußere nicht
            int[] cyclesBySize = new int[cycles.Count];
            for (int i = 0; i < cyclesBySize.Length; i++)
            {
                cyclesBySize[i] = i;
            }
            Array.Sort(cyclesBySize, delegate (int i1, int i2)
            {
                return -Math.Abs(cycles2d[i1].Area).CompareTo(Math.Abs(cycles2d[i2].Area));
            });
            Dictionary<int, List<int>> outlinesToHoles = new Dictionary<int, List<int>>(); // enthält die Indizes der outlines und eine (ggf. leere) liste der Indizes der holes
            for (int i = 0; i < cyclesBySize.Length; i++)
            {
                if (cycles2d[cyclesBySize[i]].isCounterClock && cycles2d[cyclesBySize[i]].Area > Precision.eps)
                {
                    outlinesToHoles[cyclesBySize[i]] = new List<int>();
                }
            }
            for (int i = 0; i < cyclesBySize.Length; i++)
            {
                if (!cycles2d[cyclesBySize[i]].isCounterClock)
                {   // ein Loch, suche die nächstgrüßere Umrandung, die das Loch enthält
                    for (int j = i - 1; j >= 0; --j)
                    {
                        if (cycles2d[cyclesBySize[j]].isCounterClock)
                        {
                            if (cycles2d[cyclesBySize[j]].isInside(cycles2d[cyclesBySize[i]][0].PointAt(0.5))) // PointAt(0.5) because StartPoint may coincide with a vertex of the outer cycle
                            {
                                outlinesToHoles[cyclesBySize[j]].Add(cyclesBySize[i]);
                                break;
                            }
                        }
                        else
                        {   // ein Loch, direkt enthalten in einem größeren Loch (keine Hülle dazwischen) muss ignoriert werden
                            if (!cycles2d[cyclesBySize[j]].isInside(cycles2d[cyclesBySize[i]][0].PointAt(0.5)))
                            {   // es muss !cycles2d[cyclesBySize[j]].isInside heißen, da cycles2d[cyclesBySize[j]] ja ein Loch ist und isInside bei Löchern andersrum ist
                                break;
                            }
                        }
                    }
                }
            }
            // die größte Umrandung [!hat keine Löcher und, sie darf ruhig Löcher haben] enthält eine weitere Umrandung :
            // dann die größte Umrandung wegwerfen, das ist nämlich die Außenkontur, die nicht geschnitten wurde
            if (cyclesBySize.Length > 0)
            {
                int biggestOutline = cyclesBySize[0];
                if (outlinesToHoles.ContainsKey(biggestOutline))
                {
                    foreach (int key in outlinesToHoles.Keys)
                    {
                        if (key != biggestOutline)
                        {
                            if (cycles2d[biggestOutline].isInside(cycles2d[key][0].StartPoint))
                            {
                                bool isinside = true;
                                for (int i = 0; i < outlinesToHoles[biggestOutline].Count; i++)
                                {
                                    if (!cycles2d[outlinesToHoles[biggestOutline][i]].isInside(cycles2d[key][0].StartPoint))
                                    {
                                        isinside = false;
                                    }
                                }
                                if (isinside)
                                {
                                    outlinesToHoles.Remove(biggestOutline); // Die äußere Umrandung löschen
                                    break;
                                }
                            }
                        }
                    }
                }
            }
#if DEBUG
            DebuggerContainer dc0 = new CADability.DebuggerContainer();
            double arrowsize = onThisFace.Area.GetExtent().Size / 100.0;
            foreach (KeyValuePair<int, List<int>> item in outlinesToHoles)
            {
                foreach (Edge edg in cycles[item.Key])
                {
                    dc0.Add(edg.Curve2D(onThisFace), System.Drawing.Color.Red, item.Key);
                    // noch einen Richtungspfeil zufügen
                    GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                    GeoVector2D dir = edg.Curve2D(onThisFace).DirectionAt(0.5).Normalized;
                    arrowpnts[1] = edg.Curve2D(onThisFace).PointAt(0.5);
                    arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                    arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                    Polyline2D pl2d = new Polyline2D(arrowpnts);
                    dc0.Add(pl2d, System.Drawing.Color.Red, edg.GetHashCode());
                }
                for (int i = 0; i < item.Value.Count; i++)
                {
                    foreach (Edge edg in cycles[item.Value[i]])
                    {
                        dc0.Add(edg.Curve2D(onThisFace), System.Drawing.Color.Blue, item.Value[i]);
                        // noch einen Richtungspfeil zufügen
                        GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                        GeoVector2D dir = edg.Curve2D(onThisFace).DirectionAt(0.5).Normalized;
                        arrowpnts[1] = edg.Curve2D(onThisFace).PointAt(0.5);
                        arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                        arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                        Polyline2D pl2d = new Polyline2D(arrowpnts);
                        dc0.Add(pl2d, System.Drawing.Color.Red, edg.GetHashCode());
                    }
                }
            }
#endif
            return outlinesToHoles;
        }
        //        public Shell[] ResultOld()
        //        {
        //            List<Shell> res = new List<Shell>();

        //            // Ausgangsposition:
        //            // shell1 und shell2 sind ggf. invertiert, so dass hier zunächst nur die Intersection implementiert werden muss
        //            // die edges in den Faces sind bereits aufgebrochen, dort wo sie andere Faces oder Kanten schneiden
        //            // faceToIntersectionEdges enthält für jedes betroffene Face eine Liste von neuen Kanten, die bereits richtig orientiert sind
        //            // Die 2d kanten sind übrigens richtig orientiert: außen linksrum, Löcher rechtsrum

        //            // Vorgehensweise: zuerst die neuen Kanten verwenden: jede Schnittkante muss Teil des Ergebnisses sein. Diese faces
        //            // werden gecloned für jede geschlossene Kontur, die darauf ist. Edges dieser Faces, die nicht verwendet werden
        //            // werden als "nicht zum Ergebnis gehörend" markiert.
        //            // Faces ohne neue Schnittkanten gelten als zugehörig, wenn keine ihrer Kanten als "nicht zugehörig" markiert sind.

        //            // Für jedes face, welches Schnittkanten enthält, werden sog. Cycles genereiert, also Zyklen von zusammenhängenden Edges,
        //            // immer ausgehend von den Schnittkanten. Diese Zyklen können aber auch Original-Kanten enthalten.
        //            foreach (KeyValuePair<Face, Set<Edge>> item in faceToIntersectionEdges)
        //            {
        //                // 1. manche Schnittkanten sind u.U. doppelt. Dann wird willkürlich nur eine davon verwendet
        //                Set<Pair<int, int>> checkDuplicateIntersectionEdges = new Set<Pair<int, int>>();
        //                Set<Edge> toIgnore = new Set<Edge>(); // doppelte intersectionedges: nur eine verwenden, die andere ignorieren
        //                foreach (Edge edg in item.Value)
        //                {
        //                    Pair<int, int> v1v2 = new Pair<int, int>(edg.StartVertex(item.Key).GetHashCode(), edg.EndVertex(item.Key).GetHashCode());
        //                    if (checkDuplicateIntersectionEdges.Contains(v1v2))
        //                    {
        //                        toIgnore.Add(edg);
        //                    }
        //                    else
        //                    {
        //                        checkDuplicateIntersectionEdges.Add(v1v2);
        //                    }
        //                }
        //                // 2. Die Schnittkanten werden gesammelt, jede kante erhält eine Liste von Nachfolgern
        //                Set<ICurve2D> intsCurves = new Set<ICurve2D>(); // die neuen Schnittkanten des Faces
        //                Set<EdgeOnFace> intsEdges = new Set<EdgeOnFace>(); // soll intsCurves ersetzen, wird schon parallel zu intsCurves erzeugt, aber noch nicht verwenden (22.12.16)
        //                foreach (Edge edg in item.Value)
        //                {
        //                    if (toIgnore.Contains(edg)) continue; // brauchen keine Nachfolger und dürfen nicht in die Liste intsCurves
        //                    ICurve2D c2d = edg.Curve2D(item.Key);
        //                    Vertex v = edg.EndVertex(item.Key);
        //                    c2d.UserData.Add("endVertex", v);
        //                    c2d.UserData.Add("isIntersection", true);
        //                    c2d.UserData.Add("edge", edg);
        //                    intsCurves.Add(c2d);
        //                    EdgeOnFace eof = EdgeInfo.FromEdge(edg).getOnFace(item.Key);
        //                    eof.isIntersection = true;
        //                    Predicate<Edge> outgoing = delegate (Edge e)
        //                    {
        //                        if (e == edg) return false;
        //                        if (toIgnore.Contains(e)) return false;
        //                        if (e.StartVertex(item.Key) == v) return true;
        //                        return false;
        //                    };
        //                    List<ICurve2D> outgointCurves = new List<ICurve2D>();
        //                    foreach (Edge oedge in v.ConditionalEdges(outgoing))
        //                    {
        //                        outgointCurves.Add(oedge.Curve2D(item.Key));
        //                        eof.addOutgoing(oedge, item.Key);
        //                    }
        //                    c2d.UserData.Add("followedBy", outgointCurves);
        //                }
        //                // 3. Die Originalkanten erhalten auch Nachfolgerlisten
        //                Set<ICurve2D> originalCurves = new Set<ICurve2D>(); // die ursprünglichen Kanten des Faces
        //                foreach (Edge edg in item.Key.AllEdges)
        //                {
        //                    ICurve2D c2d = edg.Curve2D(item.Key);
        //                    Vertex v = edg.EndVertex(item.Key);
        //                    c2d.UserData.Add("endVertex", v);
        //                    c2d.UserData.Add("isIntersection", false);
        //                    c2d.UserData.Add("edge", edg);
        //                    bool edgIsSingular = edg.Vertex1 == edg.Vertex2;
        //                    originalCurves.Add(c2d);
        //                    EdgeOnFace eof = EdgeInfo.FromEdge(edg).getOnFace(item.Key); // sieht aus, als ob nicht mehr verwendet, oder?
        //                    eof.isIntersection = true;
        //                    Predicate<Edge> outgoing = delegate (Edge e)
        //                    {
        //                        if (e == edg) return false;
        //                        if (toIgnore.Contains(e)) return false;
        //                        if (e.StartVertex(item.Key) == v) return true;
        //                        return false;
        //                    };
        //                    List<ICurve2D> outgointCurves = new List<ICurve2D>();
        //                    Edge singularEdge = null;
        //                    foreach (Edge oedge in v.ConditionalEdges(outgoing))
        //                    {
        //                        if (!edgIsSingular && (oedge.Vertex1 == oedge.Vertex2)) singularEdge = oedge; // eine singuläre kante, z.B. Pol bei der Kugel. 
        //                        if (oedge != edg)
        //                        {
        //                            outgointCurves.Add(oedge.Curve2D(item.Key));
        //                            eof.addOutgoing(oedge, item.Key);
        //                        }
        //                    }
        //                    if (singularEdge != null)
        //                    {
        //                        // sonst gibt es 2 Nachfolger, deshalb hier erstmal die singuläre kante dazwischen klemmen
        //                        outgointCurves.Clear();
        //                        outgointCurves.Add(singularEdge.Curve2D(item.Key));
        //                    }
        //                    c2d.UserData.Add("followedBy", outgointCurves);
        //                }
        //#if DEBUG
        //                DebuggerContainer dc = new DebuggerContainer();
        //                foreach (ICurve2D c2d in intsCurves)
        //                {
        //                    dc.Add(c2d, System.Drawing.Color.Red, 0);
        //                }
        //                foreach (ICurve2D c2d in originalCurves)
        //                {
        //                    dc.Add(c2d, System.Drawing.Color.Green, 1);
        //                }
        //#endif
        //                // 4. Ausgehend von einer Schnittkurve wird ein Zyklus gesucht. Es muss immer einen geben.
        //                // Allerdings ist hier noch nicht geklärt, wie bei verschiedenen Operationen (Vereinigung, Schnitt...) die Richtung verwendet wird...
        //                // Alle Verbindungen sollten eindeutig sein. Lediglich wenn eine normale Kante auf eine intersection Kante stößt, gibt es zwei Nachfolger
        //                // dann soll die intersection Kante gelten
        //                List<Cycle> cycles = new List<Cycle>();
        //                ICurve2D start;
        //                while ((start = intsCurves.GetAny()) != null)
        //                {
        //                    Cycle cycle = new Cycle();
        //                    ICurve2D last = start;
        //                    do
        //                    {
        //                        if ((bool)last.UserData.GetData("isIntersection"))
        //                        {
        //#if DEBUG
        //                            System.Diagnostics.Debug.Assert(intsCurves.Contains(last));
        //#endif
        //                            intsCurves.Remove(last);
        //                        }
        //                        else
        //                        {
        //#if DEBUG
        //                            System.Diagnostics.Debug.Assert(originalCurves.Contains(last));
        //#endif
        //                            originalCurves.Remove(last);
        //                        }
        //                        cycle.Add(last);
        //                        Edge lastEdge = (last.UserData.GetData("edge") as Edge);
        //                        Edge duplicateEdge;
        //                        if (intsEdgeToEdgeShell1.TryGetValue(lastEdge, out duplicateEdge))
        //                        {   // zur Schnittkante gibt es evtl. eine identische Originalkante des faces. Die soll für weitere Untersuchungen für dieses face nicht mehr berücksichtigt werden
        //                            ICurve2D dup = duplicateEdge.Curve2D(item.Key);
        //                            if (dup != null) dup.UserData.Add("excluded", true);
        //                        }
        //                        if (intsEdgeToEdgeShell2.TryGetValue(lastEdge, out duplicateEdge))
        //                        {   // ebenso
        //                            ICurve2D dup = duplicateEdge.Curve2D(item.Key);
        //                            if (dup != null) dup.UserData.Add("excluded", true);
        //                        }
        //                        if (intsEdgeToIntsEdge.TryGetValue(lastEdge, out duplicateEdge))
        //                        {
        //                            // was bewirken doppelte intersection Kanten? Noch untersuchen!
        //                        }

        //                        List<ICurve2D> followers = last.UserData.GetData("followedBy") as List<ICurve2D>;
        //                        last = null;
        //                        if (followers.Count == 1) last = followers[0];
        //                        else
        //                        {   // mehrere nachfolger, einer muss intersection sein
        //                            for (int i = 0; i < followers.Count; i++)
        //                            {
        //                                if ((bool)followers[i].UserData.GetData("isIntersection"))
        //                                {
        //                                    last = followers[i];
        //                                }
        //                                else
        //                                {
        //                                    followers[i].UserData.Add("excluded", true);
        //                                    (followers[i].UserData.GetData("edge") as Edge).Kind = Edge.EdgeKind.excluded; // Kind wird sonst nicht verwendet
        //                                }
        //                            }
        //                            // es gab also eine Verzweigung, bei der auf der intersectionCurve weitergegangen wurde.
        //                            // leider kann man nur schwer bestimmen, wie weit die andere verzweigung entfernt werden soll
        //                            // deshalb erstmal nur merken
        //                        }
        //#if DEBUG
        //                        System.Diagnostics.Debug.Assert(last != null);
        //#endif
        //                    } while (last != start);
        //#if DEBUG
        //                    for (int i = 0; i < cycle.Count; i++)
        //                    {
        //                        dc.Add(cycle[i], System.Drawing.Color.Blue, i);
        //                    }
        //#endif
        //                    bool addCycle = true;
        //                    if (cycle.Count == 2)
        //                    {   // könnte es auch eine Kette in sich zurückkehrende Kanten gebene?
        //                        Edge e0 = cycle[0].UserData.GetData("edge") as Edge;
        //                        Edge e1 = cycle[1].UserData.GetData("edge") as Edge;
        //                        if (e0.EndVertex(item.Key) == e1.StartVertex(item.Key) && e0.StartVertex(item.Key) == e1.EndVertex(item.Key))
        //                        {
        //                            if (e0.Curve3D.SameGeometry(e1.Curve3D, precision))
        //                                addCycle = false;
        //                        }
        //                    }
        //                    if (addCycle)
        //                    {
        //                        cycle.calcOrientation();
        //                        cycles.Add(cycle);
        //                    }
        //                }
        //                // die cycles sind u.U. verschieden orientiert: linksrum sind Umrandungen, rechtrum sind Löcher
        //                // die Löcher werden jetzt in die Umrandungen eingeordnet.
        //                // es kann sein, dass löcher nicht eingeordnet werden können, dann muss die Umrandung des Faces als Hauptumrandung gelten
        //                for (int i = cycles.Count - 1; i >= 0; --i)
        //                {
        //                    if (!cycles[i].isCounterClock) // also ein Loch
        //                    {
        //                        for (int j = 0; j < cycles.Count; j++)
        //                        {
        //                            if (cycles[j].isCounterClock) // eine Hülle
        //                            {
        //                                if (cycles[j].isInside(cycles[i][0].StartPoint))
        //                                {
        //                                    if (cycles[j].holes == null) cycles[j].holes = new List<Cycle>();
        //                                    cycles[j].holes.Add(cycles[i]);
        //                                    cycles.RemoveAt(i);
        //                                    continue;
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //#if DEBUG
        //                // entweder alle cycles sind Hüllen (ggf. mit Löchern)
        //                // oder es sind löcher (die keine Hüllen enthalten)
        //                for (int i = 1; i < cycles.Count; i++)
        //                {
        //                    System.Diagnostics.Debug.Assert(cycles[i].isCounterClock == cycles[0].isCounterClock);
        //                }
        //#endif
        //                item.Key.UserData.Add("BRepIntersection.Cycles", cycles); // merken für spätere Weiterverarbeitung
        //                item.Key.UserData.Add("BRepIntersection.UncheckedEdges", originalCurves); // merken für spätere Weiterverarbeitung
        //                                                                                          // es gibt möglicherweise noch unberührte Löcher oder Umrandungen dieses Faces. ob die dazugehören oder nicht, 
        //                                                                                          // wird aber erst entschieden, wenn alle Faces mit intersectionCurves fertig sind

        //                // jetzt noch alle toten Zweige entfernen
        //                List<ICurve2D> toRemove = new List<ICurve2D>();
        //                foreach (ICurve2D c2d in originalCurves)
        //                {
        //                    if (c2d.UserData.ContainsData("excluded"))
        //                    {
        //                        ICurve2D next = c2d;
        //                        while (originalCurves.Contains(next))
        //                        {
        //                            toRemove.Add(next);
        //                            List<ICurve2D> fb = next.UserData.GetData("followedBy") as List<ICurve2D>;
        //                            if (fb.Count == 1)
        //                            {
        //                                next = fb[0];
        //                                (next.UserData.GetData("edge") as Edge).Kind = Edge.EdgeKind.excluded; // Kind wird sonst nicht verwendet
        //                            }
        //                            else break;
        //                        }
        //                    }
        //                }
        //                originalCurves.RemoveMany(toRemove);
        //            }

        //            // Die Faces mit Schnittkanten haben nun ihre Cycles (sortierte und orientierte Listen von 2d Kurven mit Rückverweis auf Edge)
        //            // wobei möglicherweise noch Kanten vorhanden sind, bei denen man nicht weiß, ob sie gelten sollen oder nicht
        //            foreach (Face fce in faceToIntersectionEdges.Keys)
        //            {
        //                Set<ICurve2D> originalCurves = fce.UserData.GetData("BRepIntersection.UncheckedEdges") as Set<ICurve2D>;
        //                if (originalCurves.Count > 0)
        //                {
        //                    List<ICurve2D> toRemove = new List<ICurve2D>();
        //                    foreach (ICurve2D c2d in originalCurves)
        //                    {
        //                        if ((c2d.UserData.GetData("edge") as Edge).Kind == Edge.EdgeKind.excluded) toRemove.Add(c2d);
        //                    }
        //                    originalCurves.RemoveMany(toRemove);
        //                }
        //                List<Cycle> cycles = fce.UserData.GetData("BRepIntersection.Cycles") as List<Cycle>;

        //                if (originalCurves.Count > 0)
        //                {   // es gibt noch unbenutzte Cyclen in den Face
        //                    // als max. eine Hülle und ggf. mehrere Löcher
        //                    while (originalCurves.Count > 0)
        //                    {
        //                        ICurve2D stw = originalCurves.GetAny();
        //                        ICurve2D next = stw;
        //                        Cycle nextCycle = new Cycle();
        //                        do
        //                        {
        //                            originalCurves.Remove(next);
        //                            nextCycle.Add(next);
        //                            next = (next.UserData.GetData("followedBy") as List<ICurve2D>)[0]; // es muss genau einen geben
        //                        } while (next != stw); // es muss einen geschlossenen Zyklus geben
        //                        nextCycle.calcOrientation();
        //                        if (nextCycle.isCounterClock && !cycles[0].isCounterClock)
        //                        {
        //                            // hier müssen alle Löcher enthalten sein, da ja nextCycle die einzige (und äußertse) Hülle des Faces ist
        //                            nextCycle.holes = new List<Cycle>();
        //                            for (int i = 0; i < cycles.Count; i++)
        //                            {
        //#if DEBUG
        //                                System.Diagnostics.Debug.Assert(nextCycle.isInside(cycles[i][0].StartPoint));
        //#endif
        //                                nextCycle.holes.Add(cycles[i]);
        //                            }
        //                            cycles.Clear();
        //                            cycles.Add(nextCycle); // es gibt nur den einen, der umschließt Löcher
        //                        }
        //                        else if (!nextCycle.isCounterClock && cycles[0].isCounterClock)
        //                        {
        //                            for (int i = 0; i < cycles.Count; i++)
        //                            {
        //                                if (cycles[i].isInside(nextCycle[0].StartPoint))
        //                                {
        //                                    if (cycles[i].holes == null) cycles[i].holes = new List<Cycle>();
        //                                    cycles[i].holes.Add(nextCycle);
        //                                    break;
        //                                }
        //                            }
        //                        }
        //                        // das mit "nicht verwertbare Kanten" ist, denke ich, nicht nötig
        //                        //else
        //                        //{   // als nicht verwertbare Kanten kennzeichnen
        //                        //    for (int i = 0; i < nextCycle.Count; i++)
        //                        //    {
        //                        //        (nextCycle[i].UserData.GetData("edge") as Edge).Kind = Edge.EdgeKind.excluded;
        //                        //    }
        //                        //}
        //                    }
        //                }
        //            }

        //            // in den zerschnittenen Faces gibt es einen oder mehrere Außenzyklen, die jeweils Löcher haben können.
        //            // Bei überlappenden Faces können aber identische Zyklen vorkommen, die nur einmal benötigt werden.
        //            // Diese sollen hier zusammengefasst werden:
        //            Set<Face> intersectionCandidates = new Set<Face>(faceToIntersectionEdges.Keys);
        //            foreach (Face fce in intersectionCandidates)
        //            {
        //                List<Cycle> cycles = fce.UserData.GetData("BRepIntersection.Cycles") as List<Cycle>;
        //                Face ovrl = fce.UserData.GetData("BRepIntersection.OverlapsWith") as Face;
        //                if (intersectionCandidates.Contains(ovrl) && cycles.Count > 0)
        //                {
        //                    List<Cycle> ocycles = ovrl.UserData.GetData("BRepIntersection.Cycles") as List<Cycle>;
        //                    foreach (Cycle c1 in cycles)
        //                    {
        //                        for (int i = ocycles.Count - 1; i >= 0; --i)
        //                        {
        //                            if (c1.OutlineVertices.IsEqualTo(ocycles[i].OutlineVertices))
        //                            {
        //                                if (ocycles[i].holes != null)
        //                                {
        //                                    // die Löcher der beiden zusammenführen
        //                                    if (c1.holes == null) c1.holes = new List<Cycle>();
        //                                    // die Löcher von ocycles hinzufügen, wenn noch nicht drin
        //                                    for (int j = 0; j < ocycles[i].holes.Count; j++)
        //                                    {
        //                                        Set<Vertex> vohole = ocycles[i].holes[j].OutlineVertices;
        //                                        bool skip = false;
        //                                        for (int k = 0; k < c1.holes.Count; k++)
        //                                        {
        //                                            if (c1.holes[k].OutlineVertices.IsEqualTo(vohole))
        //                                            {
        //                                                skip = true; // das loch gibts schon, nichts zu tun
        //                                                break;
        //                                            }
        //                                        }
        //                                        if (!skip) c1.AddHole(ocycles[i].holes[j], fce, ovrl); // c1.holes.Add(ocycles[i].holes[j]); // ACHTUNG: 2d Koordinatensystem umrechnen!!!
        //                                    }
        //                                }
        //                                ocycles.RemoveAt(i); // dieser Zyklus wird nicht mehr benötigt, da mit c1 auf fce bereits erledigt
        //                                                     // ocycles kann durchaus leer werden
        //                            }
        //                        }
        //                    }
        //                }

        //            }
        //            // jetzt sollen die neuen faces erzeugt werden

        //            Set<Face> intersectionFaces = new Set<Face>(); // hier werden alle zum Ergebnis gehörenden faces gesammelt
        //                                                           // es können aber mehrere getrennte Shells sein
        //                                                           // zuerst kommen die Faces, die durch Schnitte entstanden sind
        //            foreach (Face fce in faceToIntersectionEdges.Keys)
        //            {
        //                List<Cycle> cycles = fce.UserData.GetData("BRepIntersection.Cycles") as List<Cycle>;
        //#if DEBUG
        //                DebuggerContainer dc = new DebuggerContainer();
        //                for (int i = 0; i < cycles.Count; i++)
        //                {
        //                    System.Drawing.Color clr;
        //                    clr = System.Drawing.Color.Red;
        //                    for (int j = 0; j < cycles[i].Count; j++)
        //                    {
        //                        dc.Add(cycles[i][j], clr, i * 1000 + j);
        //                    }
        //                    clr = System.Drawing.Color.Blue;
        //                    if (cycles[i].holes != null)
        //                    {
        //                        for (int j = 0; j < cycles[i].holes.Count; j++)
        //                        {
        //                            for (int k = 0; k < cycles[i].holes[j].Count; k++)
        //                            {
        //                                dc.Add(cycles[i].holes[j][k], clr, i * 1000 + k);

        //                            }
        //                        }
        //                    }

        //                }
        //#endif
        //                List<List<Edge[]>> allEdges = new List<List<Edge[]>>();
        //                for (int i = 0; i < cycles.Count; i++)
        //                {
        //                    List<Edge[]> outlinePlusHoles = new List<Edge[]>();
        //                    List<Edge> oedges = new List<Edge>();
        //                    for (int j = 0; j < cycles[i].Count; j++)
        //                    {
        //                        oedges.Add(cycles[i][j].UserData.GetData("edge") as Edge);
        //                    }
        //                    outlinePlusHoles.Add(oedges.ToArray());
        //                    if (cycles[i].holes != null)
        //                    {
        //                        for (int j = 0; j < cycles[i].holes.Count; j++)
        //                        {
        //                            List<Edge> hedges = new List<Edge>();
        //                            for (int k = 0; k < cycles[i].holes[j].Count; k++)
        //                            {
        //                                hedges.Add(cycles[i].holes[j][k].UserData.GetData("edge") as Edge);
        //                            }
        //                            outlinePlusHoles.Add(hedges.ToArray());
        //                        }
        //                    }
        //                    allEdges.Add(outlinePlusHoles);
        //                }
        //                foreach (List<Edge[]> item in allEdges)
        //                {
        //                    Face intsFace = Face.MakeFace(fce.Surface, item, fce);
        //                    for (int j = 0; j < item.Count; j++)
        //                    {
        //                        for (int k = 0; k < item[j].Length; k++)
        //                        {
        //                            item[j][k].Curve2D(intsFace).UserData.Clear(); // UserData stört das Serialisieren, da es Set<> enthält, damit geht DebuggerVisualizer auch nicht
        //                            item[j][k].Kind = Edge.EdgeKind.unknown; // wieder zurücksetzen, damit es spätere operationen nicht stört
        //                        }
        //                    }
        //#if DEBUG
        //                    //debuggerContainer.Add(intsFace, fce.GetHashCode());
        //#endif
        //                    // nicht mehr nötig, schon zusammengefasst
        //                    //Face ovrl = fce.UserData.GetData("BRepIntersection.OverlapsWith") as Face;
        //                    //List<Face> der;
        //                    //if (ovrl != null)
        //                    //{   // gibt es ein identisches Face, abgeleitet von einem überlappenden?
        //                    //    if (derivedFaces.TryGetValue(ovrl, out der))
        //                    //    {
        //                    //        Set<Vertex> vtxs = new Set<Vertex>(intsFace.Vertices);
        //                    //        Set<Vertex> vtxso = new Set<Vertex>(intsFace.OutlineVertices);
        //                    //        for (int i = 0; i < der.Count; i++)
        //                    //        {
        //                    //            Set<Vertex> dvtxs = new Set<Vertex>(der[i].Vertices);
        //                    //            if (vtxs.IsEqualTo(dvtxs))
        //                    //            {
        //                    //                intsFace = null; // soll nicht zugefügt werden
        //                    //                break;
        //                    //            }
        //                    //            Set<Vertex> dvtxso = new Set<Vertex>(der[i].OutlineVertices);
        //                    //            if (vtxso.IsEqualTo(dvtxso))
        //                    //            {

        //                    //            }
        //                    //        }
        //                    //    }

        //                    //}

        //                    if (intsFace != null)
        //                    {
        //                        intersectionFaces.Add(intsFace);
        //                        //if (!derivedFaces.TryGetValue(fce, out der))
        //                        //{
        //                        //    der = new List<Face>();
        //                        //    derivedFaces[fce] = der;
        //                        //}
        //                        //der.Add(intsFace);
        //                    }
        //                }
        //            }
        //            foreach (Face fce in intersectionFaces)
        //            {
        //                fce.UserData.Clear();
        //                foreach (Edge edg in fce.AllEdgesIterated())
        //                {
        //                    edg.PrimaryCurve2D.UserData.Clear(); // UserData stört das Serialisieren, da es Set<> enthält, damit geht DebuggerVisualizer auch nicht
        //                    edg.SecondaryCurve2D.UserData.Clear(); // UserData stört das Serialisieren, da es Set<> enthält, damit geht DebuggerVisualizer auch nicht
        //                    (edg.Curve3D as IGeoObject).UserData.Clear();
        //                }
        //            }
        //#if DEBUG
        //            DebuggerContainer dcce = new DebuggerContainer();
        //#endif
        //            // intersectionFaces sind alle neu erzeugten Faces. Bei Overlapping kann es sein, dass zusammengehörende Edges als zwei unabhängige Edges vorkommen
        //            // diese werden jetzt zusammengefasst
        //            Dictionary<DoubleVertexKey, Pair<Edge, Face>> vertexToEdge = new Dictionary<DoubleVertexKey, Pair<Edge, Face>>();
        //            List<Pair<Edge, Face>> combineEdges = new List<Pair<Edge, Face>>();
        //            foreach (Face fce in intersectionFaces)
        //            {
        //                foreach (Edge edg in fce.AllEdgesIterated())
        //                {
        //                    DoubleVertexKey vk = new DoubleVertexKey(edg.StartVertex(fce), edg.EndVertex(fce));
        //                    Pair<Edge, Face> other;
        //                    if (vertexToEdge.TryGetValue(vk, out other))
        //                    {
        //                        if (other.First != edg && other.First.Curve3D.SameGeometry(edg.Curve3D, precision))
        //                        {
        //                            combineEdges.Add(new Pair<Edge, Face>(edg, fce));
        //                            combineEdges.Add(other);
        //#if DEBUG
        //                            dcce.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
        //                            dcce.Add(other.First.Curve3D as IGeoObject, other.First.GetHashCode());
        //#endif
        //                        }
        //                    }
        //                    else
        //                    {
        //                        vertexToEdge[vk] = new Pair<Edge, Face>(edg, fce);
        //                    }
        //                }
        //            }

        //#if DEBUG
        //            DebuggerContainer dcif = new DebuggerContainer();
        //            foreach (Face fc in intersectionFaces)
        //            {
        //                dcif.Add(fc);
        //                foreach (Edge edg in fc.AllEdgesIterated())
        //                {
        //                    if (edg.PrimaryFace == null || edg.SecondaryFace == null)
        //                    {

        //                    }
        //                    if (edg.PrimaryCurve2D == null || edg.SecondaryCurve2D == null)
        //                    {

        //                    }
        //                    if (edg.Curve2D(fc) == null)
        //                    {
        //                        // dieser Fehler tritt auf, wenn combineEdges mehr als 2 Kanten zu einer zusammenfassen will
        //                    }
        //                }
        //            }
        //#endif

        //            // Jetzt neue Edges erzeugen gemäß combineEdges
        //            for (int i = 0; i < combineEdges.Count; i += 2)
        //            {
        //                if (combineEdges[i].First.PrimaryFace == combineEdges[i].Second)
        //                {
        //                    combineEdges[i].First.SetSecondary(combineEdges[i + 1].Second, combineEdges[i + 1].First.Curve2D(combineEdges[i + 1].Second), combineEdges[i + 1].First.Forward(combineEdges[i + 1].Second));
        //                    combineEdges[i + 1].Second.exchangeEdge(combineEdges[i + 1].First, combineEdges[i].First);
        //                }
        //                else
        //                {
        //                    combineEdges[i].First.SetPrimary(combineEdges[i + 1].Second, combineEdges[i + 1].First.Curve2D(combineEdges[i + 1].Second), combineEdges[i + 1].First.Forward(combineEdges[i + 1].Second));
        //                    combineEdges[i + 1].Second.exchangeEdge(combineEdges[i + 1].First, combineEdges[i].First);
        //                }
        //            }
        //#if DEBUG
        //            foreach (Face fc in intersectionFaces)
        //            {
        //                foreach (Edge edg in fc.AllEdgesIterated())
        //                {
        //                    if (edg.PrimaryFace == null || edg.SecondaryFace == null)
        //                    {

        //                    }
        //                    if (edg.PrimaryCurve2D == null || edg.SecondaryCurve2D == null)
        //                    {

        //                    }
        //                    if (edg.Curve2D(fc) == null)
        //                    {
        //                        // dieser Fehler tritt auf, wenn combineEdges mehr als 2 Kanten zu einer zusammenfassen will
        //                    }
        //                }
        //            }
        //#endif

        //            // Jetzt werden die unveränderten Faces hinzugefügt, die von Kanten der SchnittFaces verwendet werden, aber nicht in den Schnittfaces enthalten sind
        //            Set<Face> facesToAdd = intersectionFaces;
        //            Set<Face> allFaces = new Set<Face>();
        //            while (facesToAdd.Count > 0)
        //            {
        //                allFaces.AddMany(facesToAdd);
        //                Set<Face> moreFaces = new Set<Face>();
        //                foreach (Face fce in facesToAdd)
        //                {
        //                    foreach (Edge edg in fce.AllEdgesIterated())
        //                    {
        //                        if (!allFaces.Contains(edg.PrimaryFace))
        //                        {
        //                            moreFaces.Add(edg.PrimaryFace);
        //                        }
        //                        if (!allFaces.Contains(edg.SecondaryFace))
        //                        {
        //                            moreFaces.Add(edg.SecondaryFace);
        //                        }
        //                    }
        //                }
        //                facesToAdd = moreFaces;
        //            }
        //            // allFaces ist eine nicht unbedingt zusammenhängende menge von Faces, die jetzt zu einer oder mehreren Shells 
        //            // sortiert werden (das könnte in der vorigen Schleife gleich mit erledigt werden; oder?)
        //            while (allFaces.Count > 0)
        //            {
        //                Set<Face> sf = extractConnectedFaces(allFaces, allFaces.GetAny());
        //                Shell shell = Shell.MakeShell(sf.ToArray());
        //                res.Add(shell);
        //#if DEBUG
        //                System.Diagnostics.Debug.Assert(shell.OpenEdges.Length == 0);
        //#endif
        //            }
        //            return res.ToArray();

        //        }
#if DEBUG
        internal static Set<Face> collectConnected = new Set<Face>();
#endif
        /// <summary>
        /// Return all the faces, which are directely or indirectely connected to "startWith" from the set "allFaces"
        /// Also remove thos found faces from "allfaces"
        /// </summary>
        /// <param name="allFaces"></param>
        /// <param name="startWith"></param>
        /// <param name="result"></param>
        internal static Set<Face> extractConnectedFaces(Set<Face> allFaces, Face startWith)
        {
#if DEBUG
            collectConnected.Add(startWith);
#endif
            Set<Face> result = new Set<Face>();
            result.Add(startWith);
            allFaces.Remove(startWith);
            foreach (Edge edge in startWith.AllEdgesIterated())
            {
                if (allFaces.Contains(edge.SecondaryFace) && edge.IsOrientedConnection)
                {
                    result.UnionWith(extractConnectedFaces(allFaces, edge.SecondaryFace));
                }
                if (allFaces.Contains(edge.PrimaryFace) && edge.IsOrientedConnection)
                {
                    result.UnionWith(extractConnectedFaces(allFaces, edge.PrimaryFace));
                }
            }
            return result;
        }

        /// <summary>
        /// Liefert die Vereinigung der beiden Shells. Das können mehrere Shells sein, denn es kann eine innere Höhlung entstehen.
        /// </summary>
        /// <returns></returns>
        public int GetOverlappingFaces(out Face[] onShell1, out Face[] onShell2, out ModOp2D[] firstToSecond)
        {
            onShell1 = new Face[overlappingFaces.Count];
            onShell2 = new Face[overlappingFaces.Count];
            firstToSecond = new ModOp2D[overlappingFaces.Count];
            int ind = 0;
            foreach (KeyValuePair<DoubleFaceKey, ModOp2D> kv in overlappingFaces)
            {
                onShell1[ind] = kv.Key.face1;
                onShell2[ind] = kv.Key.face2;
                firstToSecond[ind] = kv.Value;
                ++ind;
            }
            return overlappingFaces.Count;
        }
        internal void ConnectOpenEdges(Edge[] openEdges)
        {
            OrderedMultiDictionary<DoubleVertexKey, Edge> dict = new OrderedMultiDictionary<DoubleVertexKey, Edge>(true);
            for (int i = 0; i < openEdges.Length; ++i)
            {
                dict.Add(new DoubleVertexKey(openEdges[i].Vertex1, openEdges[i].Vertex2), openEdges[i]);
            }
            foreach (KeyValuePair<DoubleVertexKey, ICollection<Edge>> kv in dict)
            {
                if (kv.Value.Count == 2)
                {
                    Edge e1 = null;
                    Edge e2 = null;
                    foreach (Edge e in kv.Value)
                    {
                        if (e1 == null) e1 = e;
                        else e2 = e;
                    }
                    if (e1.Curve3D.SameGeometry(e2.Curve3D, precision))
                    {
                        e1.SetSecondary(e2.PrimaryFace, e2.Curve2D(e2.PrimaryFace), e2.Forward(e2.PrimaryFace));
                        e2.PrimaryFace.ReplaceEdge(e2, new Edge[] { e1 });
                    }
                }
            }
        }
        private void splitEdges()
        {
            // 1. Alle Kanten an den Schnittpunkten aufbrechen und für die betroffenen
            // Faces die Liste der möglichen neuen Kanten erstellen
            foreach (KeyValuePair<Edge, List<Vertex>> kv in edgesToSplit)
            {
                Edge edge = kv.Key;
                Set<Vertex> vertexSet = new Set<Vertex>(kv.Value); // einzelne vertices können doppelt vorkommen
                SortedList<double, Vertex> sortedVertices = new SortedList<double, Vertex>();
                double prec = precision / edge.Curve3D.Length; // darf natürlich nicht 0 sein!
                foreach (Vertex v in vertexSet)
                {
                    double pos = edge.Curve3D.PositionOf(v.Position);
                    if (pos > prec && pos < 1 - prec && !sortedVertices.ContainsKey(pos)) sortedVertices.Add(pos, v); // keine Endpunkte, sonst entstehen beim Aufteilen Stücke der Länge 0
                    if (v != edge.Vertex1 && v != edge.Vertex2) v.RemoveEdge(edge);
                }
                List<double> toRemove = new List<double>();
                double dlast = -1;
                foreach (double d in sortedVertices.Keys)
                {
                    if (d - dlast < 1e-10) toRemove.Add(d);
                    dlast = d;
                }
                for (int i = 0; i < toRemove.Count; i++)
                {
                    sortedVertices.Remove(toRemove[i]);
                }
                if (sortedVertices.Count > 0)
                {
                    Edge[] splitted = edge.Split(sortedVertices, precision);
                    // edge wird in beiden faces durch die gesplitteten ersetzt. edge selbst wird dadurch bedeutungslos.
                    // Die Vertices sollen nicht mehr auf die Edge zeigen, da später beim Finden der neuen Umrandungen sonst Mehrdeutigkeiten entstehen. 
                    edge.Vertex1.RemoveEdge(edge);
                    edge.Vertex2.RemoveEdge(edge);
                }
            }
        }
        private void createNewEdges()
        {
            overlappingEdges = new Dictionary<DoubleFaceKey, Set<Edge>>();
            faceToIntersectionEdges = new Dictionary<Face, Set<Edge>>();
            edgesNotToUse = new Set<Edge>();
            // wir haben eine Menge Schnittpunkte, die Face-Paaren zugeordnet sind. Für jedes Face-Paar, welches Schnittpunkte enthält sollen hier die neuen Kanten bestimmt werden
            // Probleme dabei sind: 
            // - es ist bei mehr als 2 Schnittpunkten nicht klar, welche Abschnitte dazugehören
            // - zwei Surfaces können mehr als eine Schnittkurve haben
            Set<Edge> created = new Set<CADability.Edge>(new EdgeComparerByVertexAndFace());
            foreach (KeyValuePair<DoubleFaceKey, List<IntersectionVertex>> item in facesToIntersectionVertices)
            {
                if (cancelledfaces.Contains(item.Key.face1) || cancelledfaces.Contains(item.Key.face2)) continue;
                // Es sollen keine Schnittkanten erzeugt werden, die identisch mit bestehenden Kanten sind. Das tritt auf, wenn
                // overlappingFaces involviert sind. Auf der anderen Fläche, die diese Kante nicht hat, sollen aber schon die Schnitte erzeugt werden
                Set<Edge> edgesOnOverlappingFaces = new Set<Edge>();
                Set<Face> overlapping1 = findOverlappingPartner(item.Key.face1);
                if (overlapping1.Count > 0)
                {
                    foreach (Edge edg in item.Key.face2.AllEdgesIterated())
                    {
                        if (overlapping1.Contains(edg.OtherFace(item.Key.face2))) edgesOnOverlappingFaces.Add(edg);
                    }
                }
                Set<Face> overlapping2 = findOverlappingPartner(item.Key.face2);
                if (overlapping2.Count > 0)
                {
                    foreach (Edge edg in item.Key.face1.AllEdgesIterated())
                    {
                        if (overlapping2.Contains(edg.OtherFace(item.Key.face1))) edgesOnOverlappingFaces.Add(edg);
                    }
                }
                Set<Edge> existsOnFace1 = edgesOnOverlappingFaces.Intersection(new Set<Edge>(item.Key.face1.AllEdges));
                Set<Edge> existsOnFace2 = edgesOnOverlappingFaces.Intersection(new Set<Edge>(item.Key.face2.AllEdges));
                Set<Edge> existsOnBothFaces = new Set<Edge>();
                List<GeoPoint> points = new List<GeoPoint>(item.Value.Count);
                Set<Vertex> involvedVertices = new Set<Vertex>();
                //bool onFace1Edge = false, onFace2Edge = false;
                for (int i = 0; i < item.Value.Count; i++)
                {
                    involvedVertices.Add(item.Value[i].v);
                    //if (item.Value[i].isOnFaceBorder)
                    //{
                    //    if (item.Value[i].face == item.Key.face1) onFace1Edge = true;
                    //    if (item.Value[i].face == item.Key.face2) onFace2Edge = true;
                    //}
                }
                //if (onFace1Edge)
                //{
                //    foreach (Vertex vtx in item.Key.face1.Vertices)
                //    {
                //        if (!involvedVertices.Contains(vtx))
                //        {
                //            if (item.Key.face2.Contains(vtx.Position,true))
                //            {
                //                involvedVertices.Add(vtx);
                //            }
                //        }
                //    }
                //}
#if DEBUG
                DebuggerContainer dc0 = new DebuggerContainer();
                dc0.Add(item.Key.face1, item.Key.face1.GetHashCode());
                foreach (Edge edg in item.Key.face1.AllEdges)
                {
                    dc0.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
                }
                dc0.Add(item.Key.face2, item.Key.face2.GetHashCode());
                foreach (Edge edg in item.Key.face2.AllEdges)
                {
                    dc0.Add(edg.Curve3D as IGeoObject, edg.GetHashCode());
                }
                foreach (Vertex vtx in involvedVertices)
                {
                    Point pnt = Point.Construct();
                    pnt.Location = vtx.Position;
                    pnt.Symbol = PointSymbol.Cross;
                    dc0.Add(pnt, vtx.GetHashCode());
                }
#endif
                // alle Vertices verwerfen, die Edges auf beiden faces darstellen. Der Schnitt existiert schon als Edge auf beiden Faces
                // Der DEnkfehler: 3 vertices, zwei werden entfernt, weil es zwischen beiden eine Edge gibt auf beiden Faces
                // ABER: zwischen zwei anderen vertices von den dreien gibt es eine Edge nur auf einem Face, die muss genommen werden!

                foreach (Edge edg1 in existsOnFace1)
                {
                    foreach (Edge edg2 in existsOnFace2)
                    {
                        if ((edg1.Vertex1 == edg2.Vertex1 && edg1.Vertex2 == edg2.Vertex2) || (edg1.Vertex1 == edg2.Vertex2 && edg1.Vertex2 == edg2.Vertex1))
                        {
                            // zwei edges auf overlappingFaces sind identisch, d.h. der Schnitt muss nicht berechnet werden, da er bereits als kante existiert
                            existsOnBothFaces.Add(edg1); // es geht ja nur um die vertices
                        }
                    }
                }
                if (involvedVertices.Count < 2) continue;

                List<Vertex> usedVertices = new List<Vertex>(involvedVertices.Count); // synchron zu Points, damit man nachher wieder die vertices findet
                foreach (Vertex v in involvedVertices)
                {
                    usedVertices.Add(v);
                    points.Add(v.Position);
                }
                ICurve[] c3ds;
                ICurve2D[] crvsOnSurface1;
                ICurve2D[] crvsOnSurface2;
                double[,] params3d;
                double[,] params2dFace1;
                double[,] params2dFace2;
                GeoPoint2D[] paramsuvsurf1;
                GeoPoint2D[] paramsuvsurf2;
                if (Surfaces.Intersect(item.Key.face1.Surface, item.Key.face1.Area.GetExtent(), item.Key.face2.Surface, item.Key.face2.Area.GetExtent(), points, out c3ds, out crvsOnSurface1, out crvsOnSurface2, out params3d, out params2dFace1, out params2dFace2, out paramsuvsurf1, out paramsuvsurf2, precision))
                {
                    if (usedVertices.Count < points.Count)
                    {
                        // There have been additional vertices created.
                        // This happens e.g. when two cylinders with the same diameter intersect
                        for (int i = usedVertices.Count; i < points.Count; i++)
                        {
                            if (!item.Key.face1.Contains(ref paramsuvsurf1[i], false) || !item.Key.face2.Contains(ref paramsuvsurf2[i], false))
                            {
                                for (int j = 0; j < c3ds.Length; j++)
                                {
                                    params3d[j, i] = double.MinValue; // this vertex is invalid
                                                                      // but in order to keep points and usedVertices in sync, we still have to add a vertex to usedVertices
                                }
                            }
                            Vertex v = new Vertex(points[i]);
                            usedVertices.Add(v);
                        }
                    }
                    for (int i = 0; i < c3ds.Length; i++) // meist nur eine Kurve
                    {   // die Orientierung der 3d Kurve ist zufällig, hat nichts mit der Topologie der Shell zu tun.
                        // Die beiden 2d Kurven haben die selbe Orientierung wie die 3d Kurve, aber die beiden 2d Kurven müssen letztlich
                        // gegenläufig orientiert sein, so dass hier entschiedn werden muss, welche umgedreht wird
                        SortedDictionary<double, int> sortedIv = new SortedDictionary<double, int>(); // Indizes nach u aufsteigend sortiert und nur für Kurve i (meist ohnehin nur eine)
                        for (int j = 0; j < points.Count; j++)
                        {
                            if (params3d[i, j] == double.MinValue) continue; // dieser Schnittpunkt liegt auf einer anderen Kurve
                            sortedIv[params3d[i, j]] = j;
                        }
                        // aus den mehreren Punkten (meist 2) unter Berücksichtigung der Richtungen Kanten erzeugen
                        // bei nicht geschlossenen Kurven sollten immer zwei aufeinanderfolgende Punkte einen gültigen Kurvenabschnitt bilden.
                        // Obwohl auch hierbei ein doppelt auftretenden Punkt ein Problem machen könnte (Fälle sind schwer vorstellbar).
                        // Im schlimmsten Fall müssten man für alle Abschnitte (nicht nur die geraden) 
                        // Bei geschlossenen Kurven ist nicht klar, welche Punktepaare gültige Kurvenabschnitte erzeugen. 
                        if (c3ds[i].IsClosed)
                        {
                            if (item.Key.face1.Area.Contains(crvsOnSurface1[i].StartPoint, false) && item.Key.face2.Area.Contains(crvsOnSurface2[i].StartPoint, false))
                            {
                                // der Startpunkt der geschlossenen 2d-Kurven (die ja dem Startpunkt der 3d Kurve entsprechen) gehört zu den gültigen Kurvenabschnitten
                                // also dürfen wir nicht mit dem kleinsten u beginnen, sondern müssen das Kurvenstück berücksichtigen, welches durch den Anfang  geht.
                                // Beachte: die trimm Funktion (sowohl in 2d als auch in 3d) muss berücksichtigen, dass bei geschlossenen Kurven, der 1. index größer sein kann
                                // als der 2. Index. Das bedeutet dann, das Stück, welches über "0" geht soll geliefert werden. Noch überprüfen, ob das überall implementiert ist
                                // hier wird das erste Element aus dem SortedDictionary entfernt und hinten wieder eingefügt
                                var fiter = sortedIv.GetEnumerator(); // so bekommt man das erste, First() scheint es komischerweise nicht zu geben
                                fiter.MoveNext();
                                double u0 = fiter.Current.Key;
                                int j0 = fiter.Current.Value;
                                sortedIv.Remove(u0);
                                sortedIv[u0 + 1] = j0;
                            }
                        }
                        List<int> ivIndices = new List<int>(sortedIv.Count);
                        foreach (int ind in sortedIv.Values)
                        {
                            ivIndices.Add(ind);
                        }
                        for (int j = 0; j < ivIndices.Count - 1; j++)
                        {

                            int j1 = ivIndices[j];
                            double u1 = params3d[i, j1];
                            int j2 = ivIndices[j + 1];
                            double u2 = params3d[i, j2];
                            // before trimming we test whether we need this curve at all
                            // if both endpoints of the curve are on the border of the face, the curve may be totally outside
                            bool isOnFace1Border = false, isOnFace2Border = false;
                            bool j1IsOnBorder = false, j2IsOnBorder = false;
                            foreach (IntersectionVertex iv in item.Value)
                            {
                                if (iv.v == usedVertices[j1] && iv.isOnFaceBorder)
                                {
                                    if (iv.edgeIsOn1) isOnFace1Border = true;
                                    else isOnFace2Border = true;
                                    j1IsOnBorder = true;
                                }
                                if (iv.v == usedVertices[j2] && iv.isOnFaceBorder)
                                {
                                    if (iv.edgeIsOn1) isOnFace1Border = true;
                                    else isOnFace2Border = true;
                                    j2IsOnBorder = true;
                                }
                            }

                            if ((j1IsOnBorder && j2IsOnBorder) || sortedIv.Count > 2)
                            {   // both endpoints are on the border: it is not sure that the intersectioncurve is inside the face
                                // more than two intersectionpoints: maybe some segment is outside
                                GeoPoint2D uv = crvsOnSurface1[i].PointAt((params2dFace1[i, j1] + params2dFace1[i, j2]) / 2.0);
                                if (!item.Key.face1.Contains(ref uv, true))
                                {   // it still might be a point on the edge,also test it in 3d, because it might be imprecise in 2d
                                    uv = item.Key.face1.Surface.PositionOf(c3ds[i].PointAt((u1 + u2) / 2.0));
                                    if (!item.Key.face1.Contains(ref uv, true)) continue;
                                }
                                uv = crvsOnSurface2[i].PointAt((params2dFace2[i, j1] + params2dFace2[i, j2]) / 2.0);
                                if (!item.Key.face2.Contains(ref uv, true))
                                {
                                    uv = item.Key.face2.Surface.PositionOf(c3ds[i].PointAt((u1 + u2) / 2.0));
                                    if (!item.Key.face2.Contains(ref uv, true)) continue;
                                }
                            }

                            ICurve tr = c3ds[i].Clone(); // InterpolatedDualSurfaceCurve muss die Surfaces erahlten
                            tr.Trim(u1, u2);
                            ICurve2D con1 = crvsOnSurface1[i].Trim(params2dFace1[i, j1], params2dFace1[i, j2]);
                            ICurve2D con2 = crvsOnSurface2[i].Trim(params2dFace2[i, j1], params2dFace2[i, j2]);
                            // hier am Besten aus InterpolatedDualSurfaceCurve BSplines machen, sowohl in 2d, als auch in 3d und ganz am Ende
                            // wieder zu InterpolatedDualSurfaceCurve machen. GGf mit Flag, damit das klar ist
                            // Problme wäre die Genauigkeit, wenn beim BRepOperation.generateCycles die Richtung genommen wird...
                            if (con1 is InterpolatedDualSurfaceCurve.ProjectedCurve && con2 is InterpolatedDualSurfaceCurve.ProjectedCurve &&
                                tr is InterpolatedDualSurfaceCurve)
                            {   // con1 und con2 müssen auf tr verweisen, sonst kann man das Face später nicht mit "ReverseOrientation" umdrehen. Dort wird nämlich die 
                                // surface verändert, und die muss bei allen Kurven die selbe sein
                                (con1 as InterpolatedDualSurfaceCurve.ProjectedCurve).SetCurve3d(tr as InterpolatedDualSurfaceCurve);
                                (con2 as InterpolatedDualSurfaceCurve.ProjectedCurve).SetCurve3d(tr as InterpolatedDualSurfaceCurve);
                            }
                            // das Kreuzprodukt im Start (oder End oder Mittel) -Punkt hat die selbe Reichung wie die 3d Kurve: con1 umdrehen
                            // andere Richtung: con2 umdrehen
                            // The cross product of the normals specifies the direction of the new edge, no matter where on the curve we compute it.
                            // But if the surfaces are tangential in a point the cross product of the normals will be 0. So we take the better one
                            // if both are bad (e.g. two same diameter cylinders), we take a point in the middle
                            bool dirs1;
                            GeoVector normalsCrossedStart = item.Key.face1.Surface.GetNormal(paramsuvsurf1[j1]) ^ item.Key.face2.Surface.GetNormal(paramsuvsurf2[j1]);
                            GeoVector normalsCrossedEnd = item.Key.face1.Surface.GetNormal(paramsuvsurf1[j2]) ^ item.Key.face2.Surface.GetNormal(paramsuvsurf2[j2]);
                            if (normalsCrossedStart.Length < Precision.eps && normalsCrossedEnd.Length < Precision.eps)
                            {
                                GeoPoint m = tr.PointAt(0.5);
                                GeoVector normalsCrossedMiddle = item.Key.face1.Surface.GetNormal(item.Key.face1.Surface.PositionOf(m)) ^ item.Key.face2.Surface.GetNormal(item.Key.face2.Surface.PositionOf(m));
                                if (normalsCrossedMiddle.Length < Precision.eps)
                                {
                                    // Still ignoring the case where there could be a real intersection e.g. when a surface crosses a plane like the "S" crosses the tangent at the middle
                                    // When this intersection curve coincides with an existing edge on one of the faces, we use the combined normalvector of both involved faces
                                    Set<Edge> existingEdges = new Set<Edge>(Vertex.ConnectingEdges(usedVertices[j1], usedVertices[j2]));
                                    GeoVector n1 = item.Key.face1.Surface.GetNormal(item.Key.face1.Surface.PositionOf(m)).Normalized;
                                    GeoVector n2 = item.Key.face2.Surface.GetNormal(item.Key.face2.Surface.PositionOf(m)).Normalized;
                                    Set<Edge> onFace1 = existingEdges.Intersection(new Set<Edge>(item.Key.face1.AllEdges));
                                    bool edgFound = false;
                                    foreach (Edge edg in onFace1)
                                    {
                                        if (edg.Curve3D != null && edg.Curve3D.DistanceTo(m) < Precision.eps)
                                        {
                                            Face otherFace = edg.OtherFace(item.Key.face1);
                                            n1 += otherFace.Surface.GetNormal(otherFace.Surface.PositionOf(m)).Normalized;
                                            edgFound = true;
                                            break;
                                        }
                                    }
                                    Set<Edge> onFace2 = existingEdges.Intersection(new Set<Edge>(item.Key.face2.AllEdges));
                                    foreach (Edge edg in onFace2)
                                    {
                                        if (edg.Curve3D != null && edg.Curve3D.DistanceTo(m) < Precision.eps)
                                        {
                                            Face otherFace = edg.OtherFace(item.Key.face2);
                                            n2 += otherFace.Surface.GetNormal(otherFace.Surface.PositionOf(m)).Normalized;
                                            edgFound = true;
                                            break;
                                        }
                                    }
                                    if (edgFound)
                                    {
                                        normalsCrossedMiddle = n1 ^ n2;
                                        if (normalsCrossedMiddle.Length < Precision.eps)
                                        {   // still not able to decide, the connecting face found is also tangential
                                            // now we go a little bit inside on the face with the edge. This is very seldom the case, so no problem making the same iteration once more
                                            foreach (Edge edg in onFace1)
                                            {
                                                if (edg.Curve3D != null && edg.Curve3D.DistanceTo(m) < Precision.eps)
                                                {
                                                    ICurve2D c2df1 = edg.Curve2D(item.Key.face1);
                                                    // from the middle of this edge go a small step into the inside of the face and see what the normal is at that point
                                                    GeoPoint2D mp = c2df1.PointAt(0.5);
                                                    GeoVector2D mdir = c2df1.DirectionAt(0.5).ToLeft().Normalized;
                                                    double len = item.Key.face1.Area.GetExtent().Size;
                                                    Line2D l2d = new Line2D(mp, mp + len * mdir);
                                                    double[] parts = item.Key.face1.Area.Clip(l2d, true);
                                                    if (parts.Length > 1)
                                                    {   // there is a point on face1 close to the intersectioncurve, which we can use for the normal
                                                        n1 += item.Key.face1.Surface.GetNormal(l2d.PointAt((parts[0] + parts[1]) / 2.0));
                                                    }
                                                    break;
                                                }
                                            }
                                            foreach (Edge edg in onFace2)
                                            {
                                                if (edg.Curve3D != null && edg.Curve3D.DistanceTo(m) < Precision.eps)
                                                {
                                                    ICurve2D c2df2 = edg.Curve2D(item.Key.face2);
                                                    // from the middle of this edge go a small step into the inside of the face and see what the normal is at that point
                                                    GeoPoint2D mp = c2df2.PointAt(0.5);
                                                    GeoVector2D mdir = c2df2.DirectionAt(0.5).ToLeft().Normalized;
                                                    double len = item.Key.face2.Area.GetExtent().Size;
                                                    Line2D l2d = new Line2D(mp, mp + len * mdir);
                                                    double[] parts = item.Key.face2.Area.Clip(l2d, true);
                                                    if (parts.Length > 1)
                                                    {   // there is a point on face2 close to the intersectioncurve, which we can use for the normal
                                                        n2 += item.Key.face2.Surface.GetNormal(l2d.PointAt((parts[0] + parts[1]) / 2.0));
                                                    }
                                                    break;
                                                }
                                            }
                                            normalsCrossedMiddle = n1 ^ n2;
                                        }
                                    }
                                    // else: it is a inner intersection. With simple surfaces this cannot be a real intersection
                                    // but with nurbs surfaces, this could be the case. This still has to be implemented
                                    if (normalsCrossedMiddle.Length < Precision.eps) continue;
                                }
                                dirs1 = (normalsCrossedMiddle * tr.DirectionAt(0.5)) > 0;
                            }
                            else if (normalsCrossedStart.Length > normalsCrossedEnd.Length)
                            {
                                dirs1 = (normalsCrossedStart * tr.StartDirection) > 0;
                            }
                            else
                            {
                                dirs1 = (normalsCrossedEnd * tr.EndDirection) > 0;

                            }
                            // bei diesem Skalarprodukt von 2 Vektoren, die entweder die selbe oder die entgegengesetzte Richtung haben ist ">0" unkritisch
                            if (dirs1) con2.Reverse();
                            else con1.Reverse();
                            GeoPoint2D uv1 = con1.PointAt(0.5);
                            GeoPoint2D uv2 = con2.PointAt(0.5);
                            if (!item.Key.face1.Contains(ref uv1, true) || !item.Key.face2.Contains(ref uv2, true)) continue;
                            Edge edge = new Edge(item.Key.face1, tr, item.Key.face1, con1, dirs1, item.Key.face2, con2, !dirs1);
                            edge.edgeInfo = new EdgeInfo(edge);
                            edge.edgeInfo.isIntersection = true;
#if DEBUG
                            (tr as IGeoObject).UserData.Add("DebugIntersectionBy1", item.Key.face1.GetHashCode());
                            (tr as IGeoObject).UserData.Add("DebugIntersectionBy2", item.Key.face2.GetHashCode());
                            if (con2 is InterpolatedDualSurfaceCurve.ProjectedCurve)
                            {
                                BSpline2D dbgbsp2d = (con2 as InterpolatedDualSurfaceCurve.ProjectedCurve).ToBSpline(0.0);
                            }
#endif
                            if (dirs1) // the trimming of BSplines is sometimes not very exact
                            {
                                if (con1 is BSpline2D)
                                {
                                    con1.StartPoint = item.Key.face1.PositionOf(tr.StartPoint);
                                    con1.EndPoint = item.Key.face1.PositionOf(tr.EndPoint);
                                }
                                if (con2 is BSpline2D)
                                {
                                    con2.StartPoint = item.Key.face2.PositionOf(tr.EndPoint);
                                    con2.EndPoint = item.Key.face2.PositionOf(tr.StartPoint);
                                }
                            }
                            else
                            {
                                if (con1 is BSpline2D)
                                {
                                    con1.StartPoint = item.Key.face1.PositionOf(tr.EndPoint);
                                    con1.EndPoint = item.Key.face1.PositionOf(tr.StartPoint);
                                }
                                if (con2 is BSpline2D)
                                {
                                    con2.StartPoint = item.Key.face2.PositionOf(tr.StartPoint);
                                    con2.EndPoint = item.Key.face2.PositionOf(tr.EndPoint);
                                }
                            }
                            edge.Vertex1 = usedVertices[j1];    // damit wird diese Kante mit den beiden Schnittvertices verbunden
                            edge.Vertex2 = usedVertices[j2];
#if TESTUSEALLEDGES
                            if (created.Contains(edge) || EdgesContainConnection(existsOnBothFaces, usedVertices[j1], usedVertices[j2]))
                            {
                                edge.Vertex1.RemoveEdge(edge);
                                edge.Vertex2.RemoveEdge(edge);
                            }
                            else
#endif
                            {
                                created.Add(edge);
                                // diese neue Kante in das Dictionary einfügen
                                bool addToFace1 = true, addToFace2 = true, rejected = false;
#if TESTUSEALLEDGES
                                foreach (Edge edg in Vertex.ConnectingEdges(edge.Vertex1, edge.Vertex2))
                                {   // bereits existierende kanten nicht neu erzeugen (breps8!), oder nur, wenn verschiedene Richtung?
                                    if (edg == edge) continue;
                                    if ((edg.PrimaryFace == item.Key.face1 || edg.SecondaryFace == item.Key.face1) && SameEdge(edge, edg, precision))
                                    {
                                        if ((edg.Forward(item.Key.face1) != edge.Forward(item.Key.face1)) == (edg.Vertex1 == edge.Vertex1))
                                        {   // the same edge exists on the shell but has opposite direction: do not use this edge at all
                                            // there will be another intersection with the third involved face with the correct direction.
                                            // Scenario: an edge of one shell lies inside the face of the other shell
                                            created.Remove(edge);
                                            edge.Vertex1.RemoveEdge(edge);
                                            edge.Vertex2.RemoveEdge(edge);
                                            rejected = true;
                                            if (edg.edgeInfo != null && edg.edgeInfo.isIntersection) // same as : if(created.Contains(edg))
                                            {   // the opposite edge is also an intersection edge. It must also be removed
                                                // see breps15
                                                created.Remove(edg);
                                                edg.Vertex1.RemoveEdge(edg);
                                                edg.Vertex2.RemoveEdge(edg);
                                                Set<Edge> mixedEdges;
                                                if (faceToMixedEdges.TryGetValue(edg.PrimaryFace, out mixedEdges))
                                                {
                                                    mixedEdges.Remove(edg);
                                                }
                                                if (faceToMixedEdges.TryGetValue(edg.SecondaryFace, out mixedEdges))
                                                {
                                                    mixedEdges.Remove(edg);
                                                }
                                            }
                                            else
                                            {
                                                edgesNotToUse.Add(edg);
                                            }
                                            continue;
                                        }
                                        addToFace1 = false;
                                    }
                                    if ((edg.PrimaryFace == item.Key.face2 || edg.SecondaryFace == item.Key.face2) && SameEdge(edge, edg, precision))
                                    {
                                        if ((edg.Forward(item.Key.face2) != edge.Forward(item.Key.face2)) == (edg.Vertex1 == edge.Vertex1))
                                        {
                                            created.Remove(edge);
                                            edge.Vertex1.RemoveEdge(edge);
                                            edge.Vertex2.RemoveEdge(edge);
                                            rejected = true;
                                            if (edg.edgeInfo != null && edg.edgeInfo.isIntersection) // same as : if (created.Contains(edg))
                                            {   // the opposite edge is also an intersection edge. It must also be removed
                                                created.Remove(edg);
                                                edg.Vertex1.RemoveEdge(edg);
                                                edg.Vertex2.RemoveEdge(edg);
                                                Set<Edge> mixedEdges;
                                                if (faceToMixedEdges.TryGetValue(edg.PrimaryFace, out mixedEdges))
                                                {
                                                    mixedEdges.Remove(edg);
                                                }
                                                if (faceToMixedEdges.TryGetValue(edg.SecondaryFace, out mixedEdges))
                                                {
                                                    mixedEdges.Remove(edg);
                                                }
                                            }
                                            else
                                            {
                                                edgesNotToUse.Add(edg);
                                            }
                                            continue;
                                        }
                                        addToFace2 = false;
                                    }
                                }
                                if (rejected) continue;
#endif
                                Edge[] splitted = null;
                                if (j1IsOnBorder && j2IsOnBorder)
                                {   // a very rare case (like in BRepTest30.cdb.json): the new intersecting edge starts and ends on the border of the face AND contains an already existing vertex of that face.
                                    // in this case we have to split the new edge
                                    if (isOnFace1Border)
                                    {   // first faster check: is the intersection edge tangential to an outline edge
                                        bool tangential = false;
                                        List<Edge> le = edge.Vertex1.EdgesOnFace(item.Key.face1);
                                        for (int k = 0; k < le.Count; k++)
                                        {
                                            if (le[k] == edge) continue;
                                            if (le[k].Vertex1 == edge.Vertex1 && Precision.SameDirection(le[k].Curve3D.StartDirection, edge.Curve3D.StartDirection, false)) tangential = true;
                                            else if (le[k].Vertex2 == edge.Vertex1 && Precision.SameDirection(le[k].Curve3D.EndDirection, edge.Curve3D.StartDirection, false)) tangential = true;
                                        }
                                        le = edge.Vertex2.EdgesOnFace(item.Key.face1);
                                        for (int k = 0; k < le.Count; k++)
                                        {
                                            if (le[k] == edge) continue;
                                            if (le[k].Vertex1 == edge.Vertex2 && Precision.SameDirection(le[k].Curve3D.StartDirection, edge.Curve3D.EndDirection, false)) tangential = true;
                                            else if (le[k].Vertex2 == edge.Vertex2 && Precision.SameDirection(le[k].Curve3D.EndDirection, edge.Curve3D.EndDirection, false)) tangential = true;
                                        }
                                        if (tangential)
                                        {
                                            SortedList<double, Vertex> splitPositions = new SortedList<double, Vertex>();
                                            foreach (Vertex vtx in item.Key.face1.Vertices)
                                            {
                                                if (vtx != edge.Vertex1 && vtx != edge.Vertex2)
                                                {
                                                    if (edge.Curve3D.DistanceTo(vtx.Position) < precision)
                                                    {
                                                        double u = edge.Curve3D.PositionOf(vtx.Position);
                                                        if (u > 1e-6 && u < 1 - 1e-6)
                                                        {
                                                            splitPositions.Add(u, vtx);
                                                        }
                                                    }
                                                }
                                            }
                                            if (splitPositions.Count > 0) splitted = edge.Split(splitPositions, precision);
                                        }
                                    }
                                    if (splitted == null && isOnFace2Border)
                                    {
                                        bool tangential = false;
                                        List<Edge> le = edge.Vertex1.EdgesOnFace(item.Key.face2);
                                        for (int k = 0; k < le.Count; k++)
                                        {
                                            if (le[k] == edge) continue;
                                            if (le[k].Vertex1 == edge.Vertex1 && Precision.SameDirection(le[k].Curve3D.StartDirection, edge.Curve3D.StartDirection, false)) tangential = true;
                                            else if (le[k].Vertex2 == edge.Vertex1 && Precision.SameDirection(le[k].Curve3D.EndDirection, edge.Curve3D.StartDirection, false)) tangential = true;
                                        }
                                        le = edge.Vertex2.EdgesOnFace(item.Key.face2);
                                        for (int k = 0; k < le.Count; k++)
                                        {
                                            if (le[k] == edge) continue;
                                            if (le[k].Vertex1 == edge.Vertex2 && Precision.SameDirection(le[k].Curve3D.StartDirection, edge.Curve3D.EndDirection, false)) tangential = true;
                                            else if (le[k].Vertex2 == edge.Vertex2 && Precision.SameDirection(le[k].Curve3D.EndDirection, edge.Curve3D.EndDirection, false)) tangential = true;
                                        }
                                        if (tangential)
                                        {
                                            SortedList<double, Vertex> splitPositions = new SortedList<double, Vertex>();
                                            foreach (Vertex vtx in item.Key.face2.Vertices)
                                            {
                                                if (vtx != edge.Vertex1 && vtx != edge.Vertex2)
                                                {
                                                    if (edge.Curve3D.DistanceTo(vtx.Position) < precision)
                                                    {
                                                        double u = edge.Curve3D.PositionOf(vtx.Position);
                                                        if (u > 1e-6 && u < 1 - 1e-6)
                                                        {
                                                            splitPositions.Add(u, vtx);
                                                        }
                                                    }
                                                }
                                            }
                                            if (splitPositions.Count > 0) splitted = edge.Split(splitPositions, precision);
                                        }
                                    }
                                }
                                Set<Edge> addTo;
                                if (addToFace1)
                                {
                                    if (!faceToIntersectionEdges.TryGetValue(item.Key.face1, out addTo))
                                    {
                                        addTo = new Set<Edge>(); // (new EdgeComparerByVertex()); // damit werden zwei Kanten mit gleichen Vertices nicht zugefügt, nutzt nichts
                                        faceToIntersectionEdges[item.Key.face1] = addTo;
                                    }
                                    if (splitted != null) addTo.AddMany(splitted);
                                    else addTo.Add(edge);
                                }
                                if (addToFace2)
                                {
                                    if (!faceToIntersectionEdges.TryGetValue(item.Key.face2, out addTo))
                                    {
                                        addTo = new Set<Edge>(); //  (new EdgeComparerByVertex());
                                        faceToIntersectionEdges[item.Key.face2] = addTo;
                                    }
                                    if (splitted != null) addTo.AddMany(splitted);
                                    else addTo.Add(edge);
                                }
                            }
                        }
                    }
                }
#if DEBUG
                else
                {

                }
#endif
            }
        }

        private static bool EdgesContainConnection(IEnumerable<Edge> edges, Vertex v1, Vertex v2)
        {
            foreach (Edge edg in edges)
            {
                if (edg.Vertex1 == v1 && edg.Vertex2 == v2) return true;
                if (edg.Vertex1 == v2 && edg.Vertex2 == v1) return true;
            }
            return false;
        }

        private Set<Face> findOverlappingPartner(Face face1)
        {
            Set<Face> res = new Set<Face>();
            foreach (DoubleFaceKey dfk in overlappingFaces.Keys)
            {
                if (dfk.face1 == face1) res.Add(dfk.face2);
                if (dfk.face2 == face1) res.Add(dfk.face1);
            }
            foreach (DoubleFaceKey dfk in oppositeFaces.Keys)
            {
                if (dfk.face1 == face1) res.Add(dfk.face2);
                if (dfk.face2 == face1) res.Add(dfk.face1);
            }
            return res;
        }
#if DEBUG
        public Dictionary<Face, GeoObjectList> dbgNewEdges
        {
            get
            {
                Dictionary<Face, GeoObjectList> res = new Dictionary<Face, GeoObjectList>();
                foreach (Face face in shell1.Faces)
                {
                    GeoObjectList list = new GeoObjectList();
                    list.AddRange(face.Area.DebugList); // das ist der bestehende Rand
                    Set<Edge> edges;
                    if (faceToIntersectionEdges.TryGetValue(face, out edges))
                    {   // das sind die neuen Kanten
                        foreach (Edge edge in edges)
                        {
                            ICurve2D c2d = edge.Curve2D(face);
                            list.Add(c2d.MakeGeoObject(Plane.XYPlane));
                        }
                    }
                    res[face] = list;
                }
                foreach (Face face in shell2.Faces)
                {
                    GeoObjectList list = new GeoObjectList();
                    list.AddRange(face.Area.DebugList); // das ist der bestehende Rand
                    Set<Edge> edges;
                    if (faceToIntersectionEdges.TryGetValue(face, out edges))
                    {   // das sind die neuen Kanten
                        foreach (Edge edge in edges)
                        {
                            ICurve2D c2d = edge.Curve2D(face);
                            list.Add(c2d.MakeGeoObject(Plane.XYPlane));
                        }
                    }
                    res[face] = list;
                }
                return res;
            }
        }

#endif
        private bool ContainesHole(Face face, List<Edge> outline, List<Edge> hole)
        {
            ICurve2D[] bdroutline = new ICurve2D[outline.Count];
            for (int i = 0; i < bdroutline.Length; ++i)
            {
                bdroutline[i] = outline[i].Curve2D(face);
            }
            Border bdr = new Border(bdroutline);
            return bdr.GetPosition(hole[0].Curve2D(face).StartPoint) == Border.Position.Inside;
            // return Border.OutlineContainsPoint(outline, hole[0].Curve2D(face).StartPoint);
        }
        private bool IsHole(Face face, List<Edge> outline)
        {   // feststellen, ob die orientierte Liste von Edges rechtsrum (hole) oder linksrum (outline) geht
            GeoPoint sp = outline[0].StartVertex(face).Position;
            GeoPoint ep = outline[outline.Count - 1].EndVertex(face).Position;
            if ((sp | ep) > precision) return false;
            ICurve2D[] curves = new ICurve2D[outline.Count];
            for (int i = 0; i < curves.Length; ++i)
            {
                curves[i] = outline[i].Curve2D(face);

            }
            return !Border.CounterClockwise(curves);
        }
        protected override bool SplitNode(Node<BRepItem> node, BRepItem objectToAdd)
        {
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < node.list.Count; ++i)
            {
                if (node.list[i].edge != null)
                {
                    if (node.list[i].edge.Curve3D != null) dc.Add(node.list[i].edge.Curve3D as IGeoObject);
                }
                if (node.list[i].face != null) dc.Add(node.list[i].face);
                if (node.list[i].vertex != null) dc.Add(node.list[i].vertex.Position, System.Drawing.Color.Red, i);
            }
#endif
            if (node.deepth < 3 && node.list.Count > 3) return true; // noch einjustieren
            if (node.deepth > 8) return false; // Notbremse
                                               // Notbremse kann auftreten wenn mehrere Vertices einer Shell identisch sind oder Kanten
                                               // sich schneiden (dann sind 4 faces in einem Punkt, jeweils 2 von jeder Shell
                                               // solche Fälle müssten ggf vorab gechecked werden
            if (objectToAdd.Type == BRepItem.ItemType.Vertex)
            {   // keine zwei Vertices aus der selben Shell und auch Schnittvertices getrennt
                // von allen anderen
                // Warum keine zwei vertices aus der selben Shell? Das teilt den Octtree unnötig auf
                // wo gerkeine verschiedenen Shells beteiligt sind
                foreach (BRepItem bi in node.list)
                {
                    if (bi.Type == BRepItem.ItemType.Vertex)
                    {
                        //if (bi.vertex.Edges[0].PrimaryFace.Owner == objectToAdd.vertex.Edges[0].PrimaryFace.Owner ||
                        if (bi.isIntersection || objectToAdd.isIntersection)
                            return true;
                    }
                }
            }
            else if (objectToAdd.Type == BRepItem.ItemType.Face)
            {   // eine der beiden Shells darf nur einfach vertreten sein, warum?
                int nums1 = 0;
                int nums2 = 0;
                if (objectToAdd.face.Owner == shell1) ++nums1;
                else ++nums2;
                foreach (BRepItem bi in node.list)
                {
                    if (bi.Type == BRepItem.ItemType.Face)
                    {
                        if (bi.face.Owner == shell1) ++nums1;
                        else ++nums2;
                    }
                }
                // return (nums1 > 1 && nums2 > 1); warum?
            }
            return false;
        }
        // erstmal weglassen, nicht klar ob das was bringt. In OctTree auch auskommentiert
        //protected override bool FilterHitTest(object objectToAdd, OctTree<BRepItem>.Node<BRepItem> node)
        //{
        //    if (node.list == null) return false; // Abkürzung nur wenn es eine Liste hat
        //    BRepItem bri = objectToAdd as BRepItem;
        //    if (bri.Type==BRepItem.ItemType.Edge)
        //    {
        //        foreach (BRepItem  bi in node.list)
        //        {
        //            if (bi.Type is Vertex)
        //            {
        //                foreach (Edge e in bi.vertex.Edges)
        //                {
        //                    if (e == bri.edge) return true;
        //                }
        //            }
        //        }
        //    }
        //    return false;
        //}

        /// <summary>
        /// Checks whether the two shells intersect each other
        /// </summary>
        /// <returns></returns>
        public bool Intersect(out GeoPoint anyIntersectionPoint)
        {
            if (edgesToSplit.Count > 0)
            {
                foreach (List<Vertex> list in edgesToSplit.Values)
                {
                    if (list.Count > 0)
                    {
                        anyIntersectionPoint = list[0].Position;
                        return true;
                    }
                }
            }
            anyIntersectionPoint = GeoPoint.Origin;
            return false;
        }

        public GeoObjectList DebugEdgesToSplit
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                //Dictionary<Face, Set<Edge>> faceToIntersectionEdges;
                //Dictionary<Edge, List<Vertex>> edgesToSplit;
                //Dictionary<Face, Set<Edge>> facesToSplit; // Faces, dies gesplitted werden sollen und deren originale oder gesplittete
                ColorDef cdp = new ColorDef("point", System.Drawing.Color.Red);
                ColorDef cde = new ColorDef("edge", System.Drawing.Color.Blue);
                foreach (KeyValuePair<Edge, List<Vertex>> item in edgesToSplit)
                {
                    if (item.Key.Curve3D != null)
                    {
                        ((item.Key.Curve3D as IGeoObject) as IColorDef).ColorDef = cde;
                        res.Add(item.Key.Curve3D as IGeoObject);
                    }
                    foreach (Vertex v in item.Value)
                    {
                        Point p = Point.Construct();
                        p.Location = v.Position;
                        p.Symbol = PointSymbol.Cross;
                        p.ColorDef = cdp;
                        res.Add(p);
                    }
                }
                return res;
            }
        }
    }

    /// <summary>
    /// Analog zu Border, jedoch nicht zwangsweise linksrum orientiert.
    /// Als innen wird alles betrachtet, was links von den Kurven liegt, also auch inverse Border möglich, die ein Loch im Unendlichen darstellen.
    /// Unveränderlich!
    /// </summary>
    //internal class Cycle
    //{
    //    private ICurve2D[] segments;
    //    private BoundingRect extend;
    //    public Cycle(ICurve2D[] segments)
    //    {
    //        this.segments = segments;
    //        extend = BoundingRect.EmptyBoundingRect;
    //        for (int i = 0; i < segments.Length; i++)
    //        {
    //            extend.MinMax(segments[i].GetExtent());
    //        }
    //    }
    //    public bool isInside(GeoPoint2D toTest)
    //    {
    //        for (int i = 0; i < segments.Length; i++)
    //        {
    //            GeoPoint2D ep = GeoPoint2D.Origin;
    //            double mindist = 0.0;
    //            double d = extend.GetUpperLeft() | toTest;
    //            if (d > mindist)
    //            {
    //                d = mindist;
    //                ep = extend.GetUpperLeft();
    //            }
    //            segments[i].Intersect(toTest, ep);
    //        }
    //        return false;
    //    }
    //}

    internal class BRepRoundEdges
    {
        Shell shell;
        List<Edge> edgesToRound;
        double precision;
        public BRepRoundEdges(Shell shell, Set<Edge> edges)
        {
            Dictionary<Edge, Edge> clonedEdges = new Dictionary<Edge, Edge>();
            this.shell = shell.Clone(clonedEdges);
            edgesToRound = new List<Edge>();
            foreach (Edge edg in edges)
            {
                if (clonedEdges.TryGetValue(edg, out Edge cloned))
                {
                    edgesToRound.Add(cloned);
                }
            }
            precision = shell.GetBoundingCube().Size * 1e-6;
        }
        public Shell Round(double radius, bool sharpVertices)
        {
            // Check, how often each vertex ist used by the edgesToRound. If it is used only once, this is an open edge.
            Dictionary<Vertex, int> numVertexUsage = new Dictionary<Vertex, int>();
            for (int i = 0; i < edgesToRound.Count; i++)
            {
                if (!numVertexUsage.TryGetValue(edgesToRound[i].Vertex1, out int val))
                {
                    numVertexUsage[edgesToRound[i].Vertex1] = 0;
                }
                numVertexUsage[edgesToRound[i].Vertex1] += 1;
                if (!numVertexUsage.TryGetValue(edgesToRound[i].Vertex2, out val))
                {
                    numVertexUsage[edgesToRound[i].Vertex2] = 0;
                }
                numVertexUsage[edgesToRound[i].Vertex2] += 1;
            }
            Dictionary<Edge, ISurface> roundingSurface = new Dictionary<Edge, ISurface>(); // the surface of the rounding for and edgeToRound
            Dictionary<Edge, Edge> tangentialEdgeOnPrimary = new Dictionary<Edge, Edge>(); // the track on edgeToRound.PrimaryFace
            Dictionary<Edge, Edge> tangentialEdgeOnSecondary = new Dictionary<Edge, Edge>(); // the track on edgeToRound.SecondaryFace
#if DEBUG
            List<Face> dbgfcs = new List<Face>();
#endif
            ISurface[][] srf = new ISurface[edgesToRound.Count][];
            ICurve2D[][] crv2dOnPrimary = new ICurve2D[edgesToRound.Count][];
            ICurve2D[][] crv2dOnSecondary = new ICurve2D[edgesToRound.Count][];
            ICurve2D[][] crv2dOnRoundSrfP = new ICurve2D[edgesToRound.Count][];
            ICurve2D[][] crv2dOnRoundSrfS = new ICurve2D[edgesToRound.Count][];
            for (int i = 0; i < edgesToRound.Count; i++)
            {
                srf[i] = RoundEdge(edgesToRound[i], radius, out crv2dOnPrimary[i], out crv2dOnSecondary[i], out crv2dOnRoundSrfP[i], out crv2dOnRoundSrfS[i], numVertexUsage[edgesToRound[i].Vertex1] == 1, numVertexUsage[edgesToRound[i].Vertex2] == 1);
#if DEBUG
                if (srf[i] != null)
                {
                    Line2D l2 = new Line2D(crv2dOnRoundSrfP[i][2].EndPoint, crv2dOnRoundSrfS[i][2].StartPoint);
                    Line2D l4 = new Line2D(crv2dOnRoundSrfS[i][2].EndPoint, crv2dOnRoundSrfP[i][2].StartPoint);
                    Face fc = Face.MakeFace(srf[i][2], new SimpleShape(new Border(new ICurve2D[] { crv2dOnRoundSrfP[i][2], l2, crv2dOnRoundSrfS[i][2], l4 })));
                    dbgfcs.Add(fc);
                }
#endif
            }
            //for (int i = 0; i < srf.Length - 1; i++)
            //{
            //    for (int j = i + 1; j < srf.Length; j++)
            //    {
            //        BoundingRect exti = crv2dOnRoundSrfP[i].GetExtent() + crv2dOnRoundSrfS[i].GetExtent();
            //        BoundingRect extj = crv2dOnRoundSrfP[j].GetExtent() + crv2dOnRoundSrfS[j].GetExtent();
            //        srf[i].SetBounds(exti);
            //        srf[j].SetBounds(extj);
            //        ICurve[] iscrv = srf[i].Intersect(exti, srf[j], extj);
            //    }
            //}
            // 
            return null;
        }

        /// <summary>
        /// Returns a surface, which rounds the two faces of the provided edge with the given radius. The surface is correct oriented
        /// Also returns the two 2d curves on primary and secondary face of the edge, which define the tangential touching curve of the surface
        /// to the two faces. The orientation of the 2d curves is ccw, but the closing bounds at the open sides of the surfaces
        /// are not created.
        /// The surface returned is composed by up to 5 surfaces, which can be connected. The middle surface [2] is actually tangential to
        /// both surfaces of the edge. surfacee [1] is tangential to only one of the two surfaces, the other surface does not exist here.
        /// surface [0] is a cylindrical extension to exceed the faces by at least "radius" length. surface [3] and [4] are analogous
        /// at the end of the edge. If the rounding surface is a simple surface (cylindrical, toroidal) all segments are returned as one
        /// surface at index 2.
        /// </summary>
        /// <param name="etr"></param>
        /// <param name="edgeOnPrimary"></param>
        /// <param name="edgeOnSecondary"></param>
        /// <returns></returns>
        private ISurface[] RoundEdge(Edge etr, double radius, out ICurve2D[] curveOnPrimary, out ICurve2D[] curveOnSecondary, out ICurve2D[] curveOnRuledSurfaceP, out ICurve2D[] curveOnRuledSurfaceS, bool openAtStart, bool openAtEnd)
        {
            curveOnRuledSurfaceP = curveOnRuledSurfaceS = curveOnPrimary = curveOnSecondary = null; // preset to enable returns whe unsucessfull
            ISurface[] resSurface = new ISurface[5];
            curveOnPrimary = new ICurve2D[5];
            curveOnSecondary = new ICurve2D[5];
            curveOnRuledSurfaceP = new ICurve2D[5];
            curveOnRuledSurfaceS = new ICurve2D[5];
            Vertex sv1 = etr.StartVertex(etr.PrimaryFace);
            GeoPoint2D uv1 = sv1.GetPositionOnFace(etr.PrimaryFace);
            GeoPoint2D uv2 = sv1.GetPositionOnFace(etr.SecondaryFace);
            GeoVector n1 = etr.PrimaryFace.Surface.GetNormal(uv1);
            GeoVector n2 = etr.SecondaryFace.Surface.GetNormal(uv2);
            if (Precision.SameDirection(n1, n2, false)) return null; // the faces are tangential
            // The rounding surface is a circle, swept on a curve, which is the intersection curve of the two offset surfaces
            // to which side have we to move the surfaces?
            GeoVector startDir;
            if (etr.Forward(etr.PrimaryFace)) startDir = etr.Curve3D.StartDirection;
            else startDir = -etr.Curve3D.EndDirection;
            bool roundInside = (n1 ^ n2) * startDir < 0;
            if (!roundInside) radius = -radius;
            // create the offset surfaces
            ISurface offsetPrim = etr.PrimaryFace.Surface.GetOffsetSurface(radius);
            ISurface offsetSec = etr.SecondaryFace.Surface.GetOffsetSurface(radius);
            BoundingRect primExt = etr.PrimaryFace.GetUVBounds();
            BoundingRect secExt = etr.SecondaryFace.GetUVBounds();
            // there is a problem with the length of the intersection curve: in some cases we need a longer curve
            // which still must be on the offsetPrim or offsetSec even if it isn't on both. We can easily extend lines or arcs
            // but not InterpolatedDualSurfaceCurves, when one of the surfaces isn't defined any more.
            // maybe we should then either see, if the surfaces are defined outside their bounds (NURBS only exist inside their bounds)
            // or extend the uv durve on the offset surface tangential to the existing uv curve, and use this curve as an axis.
            // There is no maximum length, that would suffice in all cases.
            // For round connections, it would be egnough, when the intersection curve of the offset surfaces would extend by +radius
            // to both sides.
            // For open ends, it would be egneogh, when the crv2dOnOffset1 and crv2dOnOffset2 extend to the outline of the faces
            // For sharp connections there is no limit. We should limit sharp connections to angles up to 90°, so the same as for round connections would apply.
            // We should pass the connection tpe for the ends as a parameter.
            // 
            //// Lets use the FixedU/V curves of the bounds of one face to intersect with the other surface and vice versa. 
            //// Then we will find sample points for the intersection, that extend to the bounds of both surfaces.
            //List<GeoPoint> seedPoints = new List<GeoPoint>();
            //foreach ((ISurface srf, BoundingRect ext, ISurface other) in new(ISurface, BoundingRect, ISurface)[] {
            //    (offsetPrim, primExt, offsetSec),
            //    (offsetSec, secExt, offsetPrim) })
            //{
            //    foreach (ICurve crv in new ICurve[] {
            //        srf.FixedU(ext.Left, ext.Bottom, ext.Top),
            //        srf.FixedU(ext.Right, ext.Bottom, ext.Top),
            //        srf.FixedV(ext.Bottom, ext.Left, ext.Right),
            //        srf.FixedV(ext.Top, ext.Left, ext.Right)})
            //    {
            //        other.Intersect(crv, BoundingRect.InfinitBoundingRect, out GeoPoint[] ips, out GeoPoint2D[] uv, out double[] u);
            //        seedPoints.AddRange(ips);
            //    }
            //}

            // We create up to 5 parts of surfaces, which are connected.
            // The central part is the common intersection curve inside both extends. It is build around the axis of the intersection curve
            ICurve[] crvs = offsetPrim.Intersect(primExt, offsetSec, secExt);
            if (crvs.Length > 0)
            {
                ICurve commonIntersection = crvs[0];
                if (crvs.Length > 1)
                {
                    double minDist = etr.Curve3D.DistanceTo(commonIntersection.PointAt(0.5));
                    for (int i = 1; i < crvs.Length; i++)
                    {
                        double d = etr.Curve3D.DistanceTo(crvs[i].PointAt(0.5));
                        if (d < minDist)
                        {
                            minDist = d;
                            commonIntersection = crvs[i];
                        }
                    }
                }
                GeoPoint2D spuv1 = offsetPrim.PositionOf(commonIntersection.StartPoint);
                GeoPoint2D spuv2 = offsetSec.PositionOf(commonIntersection.StartPoint);
                GeoPoint2D epuv1 = offsetPrim.PositionOf(commonIntersection.EndPoint);
                GeoPoint2D epuv2 = offsetSec.PositionOf(commonIntersection.EndPoint);
                GeoVector n = (offsetPrim.GetNormal(spuv1).Normalized + offsetSec.GetNormal(spuv2).Normalized).Normalized;
                Ellipse e = Ellipse.Construct();
                Plane pln = new Plane(commonIntersection.StartPoint, n, n ^ commonIntersection.StartDirection);
                e.SetArcPlaneCenterRadius(pln, commonIntersection.StartPoint, Math.Abs(radius));
                e.StartParameter = Math.PI / 2.0;
                e.SweepParameter = Math.PI;
                ISurface roundingSurface = ExtrudeArc(commonIntersection, radius * offsetPrim.GetNormal(spuv1).Normalized, radius * offsetSec.GetNormal(spuv2).Normalized, Math.Abs(radius));
                if (roundingSurface != null)
                {
                    ICurve2D crv2dOnOffset1 = offsetPrim.GetProjectedCurve(commonIntersection, Precision.eps);
                    ICurve crvOnPrimary = etr.PrimaryFace.Surface.Make3dCurve(crv2dOnOffset1);
                    ICurve2D crv2dOnOffset2 = offsetSec.GetProjectedCurve(commonIntersection, Precision.eps);
                    // crv2dOnOffset1 and crv2dOnOffset2 are also the 2d curves of the touching curves on primary and secondary face
                    crv2dOnOffset2.Reverse(); // reverse one of the curves to make a closed bound
                    ICurve crvOnSecondary = etr.SecondaryFace.Surface.Make3dCurve(crv2dOnOffset2);
                    // check, whether the surface is correct oriented:
                    GeoVector srfnrm = roundingSurface.GetNormal(roundingSurface.PositionOf(crvOnPrimary.PointAt(0.5)));
                    GeoVector prfnrm = etr.PrimaryFace.Surface.GetNormal(etr.PrimaryFace.Surface.PositionOf(crvOnPrimary.PointAt(0.5)));
                    double sameorientation = srfnrm * prfnrm;
                    if (sameorientation < 0.0) roundingSurface.ReverseOrientation();
                    // now make a closed border of 4 curves, the two tangential curves and two lines, to check the orientation
                    // of the tangential curves
                    ICurve2D crv2d1 = roundingSurface.GetProjectedCurve(crvOnPrimary, Precision.eps); // 2d on the rounding surface
                    ICurve2D crv2d3 = roundingSurface.GetProjectedCurve(crvOnSecondary, Precision.eps);
                    if (Math.Abs(crv2d1.StartPoint.x - crv2d3.EndPoint.x) > Math.PI)
                    {   // the rounding surface is periodic in u (rsp. x) because it is an arc
                        // chose the segment, which is less than PI 
                        if (crv2d1.StartPoint.x < crv2d3.EndPoint.x) crv2d3.Move(-Math.PI * 2, 0.0);
                        else crv2d3.Move(Math.PI * 2, 0.0);
                    }
                    Line2D l2 = new Line2D(crv2d1.EndPoint, crv2d3.StartPoint);
                    Line2D l4 = new Line2D(crv2d3.EndPoint, crv2d1.StartPoint);
                    double area = Border.SignedArea(new ICurve2D[] { crv2d1, l2, crv2d3, l4 });
                    // at this point, the 2d curves on the rounding surface and on the primary and secondary have the same orientation.
                    // as edges, they must be reverse to each other
                    if (area < 0)
                    {
                        crv2d1.Reverse();
                        crv2d3.Reverse();
#if DEBUG
                        l2.Reverse();
                        l4.Reverse();
                        Line2D tmp = l2;
                        l2 = l4;
                        l4 = tmp;
#endif
                    }
                    else
                    {
                        crv2dOnOffset1.Reverse();
                        crv2dOnOffset2.Reverse();
                    }
#if DEBUG
                    Face fc = Face.MakeFace(roundingSurface, new SimpleShape(new Border(new ICurve2D[] { crv2d1, l2, crv2d3, l4 })));
#endif
                    curveOnPrimary[2] = crv2dOnOffset1;
                    curveOnSecondary[2] = crv2dOnOffset2;
                    curveOnRuledSurfaceP[2] = crv2d1;
                    curveOnRuledSurfaceS[2] = crv2d3;
                    resSurface[2] = roundingSurface;
                }
                foreach ((ISurface srf, BoundingRect ext, ICurve2D[] crv2d, int fwdInd, int bckInd) in new (ISurface, BoundingRect, ICurve2D[], int, int)[]
                    {
                        (offsetPrim, primExt, curveOnPrimary, 3, 1),
                        (offsetSec, secExt, curveOnSecondary, 1, 3)
                    })
                {
                    ClipRect clr = new ClipRect(ext);
                    foreach ((GeoPoint2D p2d, GeoVector2D dir2d, int ind, GeoPoint2D uvp, GeoPoint2D uvs) in new (GeoPoint2D, GeoVector2D, int, GeoPoint2D, GeoPoint2D)[] {
                        (crv2d[2].StartPoint, -crv2d[2].StartDirection, fwdInd, spuv1, spuv2),
                        (crv2d[2].EndPoint, crv2d[2].EndDirection, bckInd, epuv1, epuv2)
                    })
                    {
                        if (ext.GetPosition(p2d, ext.Size * 1e-6) == BoundingRect.Position.inside)
                        {   // make a line from p2d to the bounds of primExt and extrude an arc along the 3d curve of offsetPrim
                            GeoPoint2D ep = p2d + ext.Size * dir2d;
                            GeoPoint2D sp2d = p2d;
                            if (clr.ClipLine(ref sp2d, ref ep))
                            {
                                Line2D l2d = new Line2D(sp2d, ep);
                                ICurve axis = srf.Make3dCurve(l2d);
                                ISurface extendingSurface = ExtrudeArc(axis, radius * offsetPrim.GetNormal(uvp).Normalized, radius * offsetSec.GetNormal(uvs).Normalized, Math.Abs(radius));
                                crv2d[ind] = l2d;
                                resSurface[ind] = extendingSurface;
                            }
                        }
                    }
                }
            }
            return resSurface;
        }

        /// <summary>
        /// Create a surface by moving an arc or circle along the provided curve. If the curve is a line or an circular arc,
        /// we get a cylindrical or toriodal surface, otherwise a GeneralSweptCurve is created.
        /// </summary>
        /// <param name="along"></param>
        /// <param name="startDir"></param>
        /// <param name="endDir"></param>
        /// <returns></returns>
        static public ISurface ExtrudeArc(ICurve along, GeoVector startDir, GeoVector endDir, double radius)
        {
            if (along is Line)
            {
                // make a cylindrical surface with the seam in the opposite direction of startDir and endDir
                GeoVector dirz = along.StartDirection.Normalized;
                GeoVector dirx = -(startDir + endDir).Normalized;
                GeoVector diry = (dirx ^ dirz).Normalized;
                CylindricalSurface cs = new CylindricalSurface(along.StartPoint, radius * dirx, radius * diry, dirz);
                return cs;
            }
            if (along is Ellipse && (along as Ellipse).IsCircle)
            {
                Ellipse eAlong = (along as Ellipse);
                GeoVector dirz = eAlong.Normal;
                GeoVector dirx = -(startDir + endDir).Normalized;
                GeoVector diry = (dirx ^ dirz).Normalized;
                ToroidalSurface ts = new ToroidalSurface(eAlong.Center, dirx, diry, dirz, eAlong.Radius, radius);
                return ts;
            }
            // else make a GeneralSweptCurve
            return null;
        }

    }
    internal class BRepSelfIntersection
    {
        private Shell shell;
        private OctTree<BRepItem> octTree;
        private OctTree<Face> originalFaces;
        private double offset;
        Dictionary<Edge, List<Vertex>> edgesToSplit;
        Set<IntersectionVertex> intersectionVertices;
        Dictionary<DoubleFaceKey, List<IntersectionVertex>> facesToIntersectionVertices;
        Dictionary<DoubleFaceKey, ModOp2D> overlappingFaces; // Faces von verschiedenen Shells, die auf der gleichen Surface beruhen und sich überlappen
        Dictionary<Face, Set<Edge>> faceToMixedEdges; // die neuen durch Schnitte entstandenen Kanten
#if DEBUG
        DebuggerContainer debuggerContainer;
#endif
        /// <summary>
        /// Remove all parts of the self intersecting shell, which are closer than offset to the original shell
        /// </summary>
        /// <param name="shell"></param>
        /// <param name="original"></param>
        /// <param name="offset"></param>
        public BRepSelfIntersection(Shell shell, Shell original, double offset)
        {
            this.shell = shell.Clone() as Shell;
            this.shell.SplitPeriodicFaces();
            this.shell.SplitSingularFaces();
            this.shell.DisconnectSingularEdges();
            this.shell.AssertOutwardOrientation();
            this.offset = offset;
#if DEBUG
            debuggerContainer = new DebuggerContainer();
            foreach (Face fce in this.shell.Faces)
            {
                debuggerContainer.Add(fce.Clone(), fce.GetHashCode()); // die Faces werden kaputt gemacht, deshalb hier clones merken
            }
            foreach (Edge edg in this.shell.Edges)
            {
                if (edg.Curve3D != null)
                {
                    debuggerContainer.Add((edg.Curve3D.Clone() as IGeoObject), edg.GetHashCode());
                }
            }
#endif
            Vertex[] dumy = this.shell.Vertices; // nur damits berechnet wird
            BoundingCube ext = this.shell.GetExtent(0.0);
            ext.Expand(ext.Size * 1e-5);
            octTree = new OctTree<BRepItem>(ext, ext.Size * 1e-6);
            foreach (Face fce in this.shell.Faces) octTree.AddObject(new BRepItem(octTree, fce));
            foreach (Edge edg in this.shell.Edges) octTree.AddObject(new BRepItem(octTree, edg));
            originalFaces = new OctTree<Face>(ext, ext.Size * 1e-6);
            foreach (Face fce in original.Faces) originalFaces.AddObject(fce);

            findOverlappingFaces(); // sammelt in overlappingFaces alle Faces, die sich überlappen
            createEdgeFaceIntersections(); // findet Schnittpunkte von Edges und Faces und ordnet diese den Faces und Edges zu
            splitEdges(); // mit den gefundenen Schnittpunkten werden die Edges jetzt gesplittet
            combineVertices(); // alle geometrisch identischen Vertices werden zusammengefasst
            createNewEdges(); // faceToIntersectionEdges wird gesetzt, also für jedes Face eine Liste der neuen Schnittkanten
            createSelfintersectionEdges();
            splitNewEdges();
#if DEBUG
            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToMixedEdges)
            {
                Face fc = kv.Key;
                DebuggerContainer dc = new CADability.DebuggerContainer();
                double arrowsize = fc.Area.GetExtent().Size / 100.0;
                //dc.Add(fc.DebugEdges2D.toShow);
                foreach (Edge edg in fc.AllEdgesIterated())
                {
                    dc.Add(edg.Curve2D(fc), System.Drawing.Color.Blue, edg.GetHashCode());
                }
                foreach (Edge edg in kv.Value)
                {
                    dc.Add(edg.Curve2D(fc), System.Drawing.Color.Red, edg.GetHashCode());
                    // noch einen Richtungspfeil zufügen
                    GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                    GeoVector2D dir = edg.Curve2D(fc).DirectionAt(0.5).Normalized;
                    arrowpnts[1] = edg.Curve2D(fc).PointAt(0.5);
                    arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                    arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                    Polyline2D pl2d = new Polyline2D(arrowpnts);
                    dc.Add(pl2d, System.Drawing.Color.Red, edg.GetHashCode());
                }
                DebuggerContainer dcf = new CADability.DebuggerContainer();
                dcf.Add(fc, 0);
                foreach (Edge edg in kv.Value)
                {
                    dcf.Add(edg.OtherFace(fc), 1);
                }

                DebuggerContainer dcok = new CADability.DebuggerContainer();
                foreach (Edge edg in fc.AllEdgesIterated())
                {
                    if (distOk(edg.Vertex1.Position) && distOk(edg.Vertex2.Position))
                        dcok.Add(edg.Curve2D(fc), System.Drawing.Color.Blue, edg.GetHashCode());
                }
                foreach (Edge edg in kv.Value)
                {
                    if (distOk(edg.Vertex1.Position) && distOk(edg.Vertex2.Position))
                        dcok.Add(edg.Curve2D(fc), System.Drawing.Color.Red, edg.GetHashCode());
                }

            }
#endif
            //createInnerFaceIntersections(); // Schnittpunkte zweier Faces, deren Kanten sich aber nicht schneiden, finden
            // combineEdges(); // das müsste noch ausgeführt werden overlapping faces und so, oder?
        }

        public Shell[] Result(bool allowOpenEdges)
        {
            if (faceToMixedEdges.Count == 0)
            {
                Edge[] openEdges = shell.OpenEdges;
                if (openEdges.Length == 0 || allowOpenEdges) return new Shell[] { this.shell }; // keine Überschneidungen, die shell bleibt unverändert
                Set<Face> unusedfaces = new Set<Face>(shell.Faces);
                List<Shell> lres = new List<Shell>();
                while (unusedfaces.Count > 0)
                {
                    Set<Face> connected = new Set<Face>();
                    collectFaces(unusedfaces.GetAny(), unusedfaces, connected);
                    Shell sh = Shell.Construct();
                    sh.SetFaces(connected.ToArray());
                    bool ok = true;
                    Edge[] oe = sh.OpenEdges;
                    for (int i = 0; i < oe.Length; i++)
                    {
                        if (oe[i].Curve3D != null) ok = false;
                    }
                    if (ok) lres.Add(sh);
                }
                return lres.ToArray();
            }
#if DEBUG
            DebuggerContainer dcfcs = new CADability.DebuggerContainer();
            foreach (Face fce in shell.Faces)
            {
                dcfcs.Add(fce.Clone(), fce.GetHashCode()); // die Faces werden kaputt gemacht, deshalb hier clones merken
            }
#endif
            List<Face> trimmedFaces = new List<Face>();
            Set<Face> destroyedFaces = new Set<Face>();
            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToMixedEdges)
            {
                destroyedFaces.Add(kv.Key); // das ist nicht mehr zu verwenden, auch wenn es keinen Zyklus enthält

                // hier haben wir edges in kv.Value und andere in kv.Key.AllEdges
                // die sind bereits über ihre vertices richtig miteinander verbunden
                // (es gibt hoffentlich keine geschlossenen Kanten)
                Set<Edge> unusedEdges = new Set<Edge>(kv.Key.AllEdgesIterated());
                unusedEdges.AddMany(kv.Value);
#if DEBUG
                Set<Vertex> allVtx = new Set<Vertex>();
                foreach (Edge edg in unusedEdges)
                {
                    allVtx.Add(edg.Vertex1);
                    allVtx.Add(edg.Vertex2);
                }
                // Ausgangslage: alle Kanten in 2d mit Richtung
                DebuggerContainer dc0 = new CADability.DebuggerContainer();
                double arrowsize = kv.Key.Area.GetExtent().Size / 100.0;
                //dc.Add(fc.DebugEdges2D.toShow);
                foreach (Edge edg in kv.Key.AllEdgesIterated())
                {
                    dc0.Add(edg.Curve2D(kv.Key), System.Drawing.Color.Blue, edg.GetHashCode());
                }
                foreach (Edge edg in kv.Value)
                {
                    dc0.Add(edg.Curve2D(kv.Key), System.Drawing.Color.Red, edg.GetHashCode());
                    // noch einen Richtungspfeil zufügen
                    GeoPoint2D[] arrowpnts = new GeoPoint2D[3];
                    GeoVector2D dir = edg.Curve2D(kv.Key).DirectionAt(0.5).Normalized;
                    arrowpnts[1] = edg.Curve2D(kv.Key).PointAt(0.5);
                    arrowpnts[0] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToLeft();
                    arrowpnts[2] = arrowpnts[1] - arrowsize * dir + arrowsize * dir.ToRight();
                    Polyline2D pl2d = new Polyline2D(arrowpnts);
                    dc0.Add(pl2d, System.Drawing.Color.Red, edg.GetHashCode());
                }
                foreach (Vertex vtx in allVtx)
                {
                    GeoPoint2D vpos = vtx.GetPositionOnFace(kv.Key);
                    Point pnt = Point.Construct();
                    pnt.Symbol = PointSymbol.Cross;
                    pnt.Location = new GeoPoint(vpos);
                    dc0.Add(pnt, vtx.GetHashCode());
                }
#endif
                // fange mit irgend einer kante an. Gehe immer am Ende linksrum weiter, bis du wieder am Anfang ankommst
                // das wird ein geschlossener Zyklus.
                // wenn es am Ende mehrere Möglichkeiten gibt, dann gehe nach links.
                // wenn die linkeste Folge-Kante in Gegenrichtung läuft oder es keine Folgekante gibt, dann entferne alle angesammelten Kanten und verwerfe den Zyklus.
                List<List<Edge>> cycles = BRepOperation.generateCycles(unusedEdges, kv.Key, Precision.eps); // haben wir hier keine precision?
#if DEBUG
                DebuggerContainer dc = new CADability.DebuggerContainer();
                for (int i = 0; i < cycles.Count; i++)
                {
                    for (int j = 0; j < cycles[i].Count; j++)
                    {
                        ICurve2D c2d = cycles[i][j].Curve2D(kv.Key);
                        // dc.Add(c2d, System.Drawing.Color.Red, cycles[i][j].GetHashCode());
                        dc.Add(c2d, System.Drawing.Color.Red, i);
                    }
                }
#endif
                Cycle[] cycles2d = new Cycle[cycles.Count];
                for (int i = 0; i < cycles.Count; i++)
                {
                    cycles2d[i] = new Cycle();
                    for (int j = 0; j < cycles[i].Count; j++)
                    {
                        cycles2d[i].Add(cycles[i][j].Curve2D(kv.Key));
                    }
                    cycles2d[i].calcOrientation();
                }
                for (int i = 0; i < cycles2d.Length; i++)
                {
                    List<int> holeIndices = new List<int>();
                    if (cycles2d[i].isCounterClock)
                    {
                        for (int j = 0; j < cycles2d.Length; j++)
                        {
                            if (i != j)
                            {
                                if (!cycles2d[j].isCounterClock && cycles2d[i].isInside(cycles2d[j][0].StartPoint))
                                {
                                    holeIndices.Add(j); // nur die inneren Löcher nehmen
                                }
                            }
                        }
                        holeIndices.Sort(delegate (int i1, int i2)
                        {
                            return cycles2d[i1].Area.CompareTo(cycles2d[i2].Area);
                        });
                        for (int j = holeIndices.Count - 1; j > 0; --j)
                        {
                            // ein kleines, welches in einem größeren steckt, entfernen
                            for (int k = 0; k < j; k++)
                            {
                                if (!cycles2d[holeIndices[k]].isInside(cycles2d[holeIndices[j]][0].StartPoint))
                                {
                                    holeIndices.RemoveAt(j);
                                    break;
                                }
                            }
                        }
                        Face fc = Face.Construct();
#if DEBUG
                        fc.UserData.Add("PartOf", kv.Key.GetHashCode());
#endif
                        Edge[] outline = cycles[i].ToArray();
                        for (int j = 0; j < outline.Length; j++)
                        {
                            outline[j].ReplaceFace(kv.Key, fc);
                        }
                        // es kann Löcher geben, die außerhalb von outline liegen, oder solche, die innerhalb anderer Löcher liegen.
                        // Überschneidungen sind aber ausgeschlossen
                        Edge[][] holes = new Edge[holeIndices.Count][];
                        for (int j = 0; j < holes.Length; j++)
                        {
                            holes[j] = cycles[holeIndices[j]].ToArray();
                            for (int k = 0; k < holes[j].Length; k++)
                            {
                                holes[j][k].ReplaceFace(kv.Key, fc);
                            }
                        }
                        fc.Set(kv.Key.Surface, outline, holes);
#if DEBUG
                        GeoPoint[] trianglePoint;
                        GeoPoint2D[] triangleUVPoint;
                        int[] triangleIndex;
                        BoundingCube triangleExtent;
                        fc.GetTriangulation(0.01, out trianglePoint, out triangleUVPoint, out triangleIndex, out triangleExtent);
                        System.Diagnostics.Debug.Assert(fc.CheckConsistency());
#endif
                        trimmedFaces.Add(fc); // hat schon die richtigen Kanten
                    }
                }
                // aus den gefundenen Cycles müssen jetzt neue Faces gemacht werden.
                // Dazu alle linksrum orientierten Cycles nehmen und von den rechtsrum orientierten (das sind immer unveränderte Löcher)
                // schauen, in welches äußere Umrandung sie gehören (wenn überhaupt)
                // dann noch die Edges vom alten face auf die neuen faces übertragen.
            }
#if DEBUGser // es gab Fehler beim Serialisieren, die wurden hier getestet. Stehengelassen zum Nachahmen
            Stream stream = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File, null));
            formatter.AssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple;
            // formatter.TypeFormat = System.Runtime.Serialization.Formatters.FormatterTypeStyle.XsdString; // XsdString macht das wechseln von .NET Frameworks schwierig
            formatter.Serialize(stream, trimmedFaces);
            stream.Seek(0, SeekOrigin.Begin);
            formatter = new BinaryFormatter();
            List<Face> tfread = (List<Face>)formatter.Deserialize(stream);
            foreach (Face fc in tfread)
            {
                GeoPoint[] trianglePoint;
                GeoPoint2D[] triangleUVPoint;
                int[] triangleIndex;
                BoundingCube triangleExtent;
                fc.GetTriangulation(0.01, out trianglePoint, out triangleUVPoint, out triangleIndex, out triangleExtent);
                foreach (Edge edg in fc.AllEdgesIterated())
                {
                    if (edg.Curve3D!=null)
                    {
                        ICurve cv = edg.Curve3D.Approximate(true, 0.01);
                    }
                }
            }

#endif
            Set<Face> facesToAdd = new Set<Face>(trimmedFaces);
            Set<Face> allFaces = new Set<Face>();
            while (facesToAdd.Count > 0)
            {
                allFaces.AddMany(facesToAdd);
                Set<Face> moreFaces = new Set<Face>();
                foreach (Face fce in facesToAdd)
                {
                    foreach (Edge edg in fce.AllEdgesIterated())
                    {
                        if (!allFaces.Contains(edg.PrimaryFace) && !destroyedFaces.Contains(edg.PrimaryFace))
                        {
#if DEBUG
                            System.Diagnostics.Debug.Assert(edg.PrimaryFace.CheckConsistency());
                            System.Diagnostics.Trace.WriteLine("zufügen: " + edg.GetHashCode().ToString() + ", " + fce.GetHashCode().ToString() + ": " + edg.PrimaryFace.GetHashCode().ToString());
                            //System.Diagnostics.Trace.WriteLine("Face " + fce.GetHashCode().ToString() + ": (Edge " + edg.GetHashCode().ToString() + ") -> " + edg.PrimaryFace.GetHashCode().ToString());
#endif
                            moreFaces.Add(edg.PrimaryFace);
                        }
                        if (edg.SecondaryFace != null && !allFaces.Contains(edg.SecondaryFace) && !destroyedFaces.Contains(edg.SecondaryFace))
                        {
#if DEBUG
                            System.Diagnostics.Trace.WriteLine("zufügen: " + edg.GetHashCode().ToString() + ", " + fce.GetHashCode().ToString() + ": " + edg.SecondaryFace.GetHashCode().ToString());
                            System.Diagnostics.Debug.Assert(edg.SecondaryFace.CheckConsistency());
                            //System.Diagnostics.Trace.WriteLine("Face " + fce.GetHashCode().ToString() + ": (Edge " + edg.GetHashCode().ToString() + ") -> " + edg.SecondaryFace.GetHashCode().ToString());
#endif
                            moreFaces.Add(edg.SecondaryFace);
                        }
                    }
                }
                facesToAdd = moreFaces;
            }

#if DEBUG
            foreach (Face fce in allFaces)
            {
                System.Diagnostics.Debug.Assert(fce.CheckConsistency());
            }
#endif
            // allFaces ist eine nicht unbedingt zusammenhängende menge von Faces, die jetzt zu einer oder mehreren Shells 
            // sortiert werden (das könnte in der vorigen Schleife gleich mit erledigt werden; oder?)
            List<Shell> res = new List<GeoObject.Shell>();
            while (allFaces.Count > 0)
            {
                Set<Face> sf = BRepOperation.extractConnectedFaces(allFaces, allFaces.GetAny());
                Shell shell = Shell.MakeShell(sf.ToArray());
                Edge[] oe = shell.OpenEdges;
                bool ok = true;
                for (int i = 0; i < oe.Length; i++)
                {
                    if (oe[i].Curve3D != null) ok = false;
                }
                if (ok || allowOpenEdges) res.Add(shell);
#if DEBUG
                // es gibt Reste, die nicht geschlossen sind
                // System.Diagnostics.Debug.Assert(shell.OpenEdges.Length == 0);
#endif
            }
#if DEBUG
            for (int i = 0; i < res.Count; i++)
            {
                foreach (Face fce in res[i].Faces)
                {
                    System.Diagnostics.Debug.Assert(fce.CheckConsistency());
                }
            }
#endif
            // hier noch überprüfen: negativ orientierte Shells verwerfen,
            // Shells in einer Shell müsste eigentlich als Solid mit mehreren Shells exportiert werden
            // oder der Aufrufer macht das
            return res.ToArray();
        }


        private void collectFaces(Face startWith, Set<Face> unusedfaces, Set<Face> connected)
        {
            unusedfaces.Remove(startWith);
            connected.Add(startWith);
            foreach (Edge edg in startWith.AllEdgesIterated())
            {
                if (edg.PrimaryFace != null && unusedfaces.Contains(edg.PrimaryFace)) collectFaces(edg.PrimaryFace, unusedfaces, connected);
                if (edg.SecondaryFace != null && unusedfaces.Contains(edg.SecondaryFace)) collectFaces(edg.SecondaryFace, unusedfaces, connected);
            }
        }

        private void splitNewEdges()
        {
            Set<Edge> allNewEdges = new Set<Edge>();
            foreach (KeyValuePair<Face, Set<Edge>> kv in faceToMixedEdges)
            {
                foreach (Edge edg in kv.Value)
                {
                    allNewEdges.Add(edg);
                }
            }
            OctTree<Vertex> newVertices = new OctTree<Vertex>(this.octTree.Extend, this.octTree.precision);
            // hier werden die neu erzeugten Vertices gesammelt ung ggf. mehrfach verwendet
            foreach (Edge edg in allNewEdges)
            {
                BRepItem[] close = octTree.GetObjectsCloseTo(edg.Curve3D as IOctTreeInsertable);
                SortedDictionary<double, GeoPoint> ips = new SortedDictionary<double, GeoPoint>();
                for (int i = 0; i < close.Length; i++)
                {
                    if (close[i].face != null)
                    {
                        if (close[i].face != edg.PrimaryFace && close[i].face != edg.SecondaryFace)
                        {
                            GeoPoint[] ip;
                            GeoPoint2D[] uvOnFace;
                            double[] uOnCurve;
                            close[i].face.Intersect(edg, out ip, out uvOnFace, out uOnCurve);
                            for (int j = 0; j < ip.Length; j++)
                            {   // leider kritisch hier: wann gilt ein Punkt als Schnittpunkt: wenn er nicht unter octtree.precision fallen würde
                                // mit seinem Anstand zum Start/Endpunkt der Kurve selbst
                                if ((edg.Curve3D.StartPoint | edg.Curve3D.PointAt(uOnCurve[j])) > octTree.precision && (edg.Curve3D.EndPoint | edg.Curve3D.PointAt(uOnCurve[j])) > octTree.precision)
                                // if (uOnCurve[j] >= 1e-6 && uOnCurve[j] <= 1 - 1e-6)
                                {
                                    ips[uOnCurve[j]] = ip[j];
                                }
                            }
                        }
                    }
                }
                if (ips.Count > 0)
                {
                    List<Edge> splEdges = new List<Edge>();
                    Vertex sv = edg.Vertex1;
                    double su = 0.0;
                    BoundingRect br1 = edg.PrimaryCurve2D.GetExtent(); // die geteilten 2d Kurven müssen in der richtigen "domain" liegen (bei periodischen Flächen)
                    BoundingRect br2 = edg.SecondaryCurve2D.GetExtent();
                    foreach (KeyValuePair<double, GeoPoint> kv in ips) // die kommen ja hoffentlich nach u sortiert
                    {
                        // gibts an dem Punkt schon einen vertex?
                        Vertex[] candidates = newVertices.GetObjectsFromBox(new BoundingCube(kv.Value, octTree.precision));
                        Vertex ev = null;
                        for (int i = 0; i < candidates.Length; i++)
                        {
                            if ((candidates[i].Position | kv.Value) < octTree.precision)
                            {
                                ev = candidates[i];
                            }
                        }
                        if (ev == null)
                        {   // wenn nicht, dann neu erzeugen
                            ev = new Vertex(kv.Value);
                            newVertices.AddObject(ev);
                        }
                        ICurve cv = edg.Curve3D.Clone();
                        cv.Trim(su, kv.Key);
                        Edge part = new Edge(edg.Owner, cv);
                        part.UseVertices(sv, ev);
                        splEdges.Add(part);
                        sv = ev;
                        su = kv.Key;
                        ICurve2D c2d = edg.PrimaryFace.Surface.GetProjectedCurve(cv, Precision.eps);
                        SurfaceHelper.AdjustPeriodic(edg.PrimaryFace.Surface, br1, c2d);
                        if (!edg.Forward(edg.PrimaryFace)) c2d.Reverse();
                        part.SetPrimary(edg.PrimaryFace, c2d, edg.Forward(edg.PrimaryFace));
                        c2d = edg.SecondaryFace.Surface.GetProjectedCurve(cv, Precision.eps);
                        SurfaceHelper.AdjustPeriodic(edg.SecondaryFace.Surface, br2, c2d);
                        if (!edg.Forward(edg.SecondaryFace)) c2d.Reverse();
                        part.SetSecondary(edg.SecondaryFace, c2d, edg.Forward(edg.SecondaryFace));
                    }
                    // und noch das letzte Stück
                    {
                        Vertex ev = edg.Vertex2;
                        ICurve cv = edg.Curve3D.Clone();
                        cv.Trim(su, 1.0);
                        Edge part = new Edge(edg.Owner, cv);
                        part.UseVertices(sv, ev);
                        splEdges.Add(part);
                        ICurve2D c2d = edg.PrimaryFace.Surface.GetProjectedCurve(cv, Precision.eps);
                        SurfaceHelper.AdjustPeriodic(edg.PrimaryFace.Surface, br1, c2d);
                        if (!edg.Forward(edg.PrimaryFace)) c2d.Reverse();
                        part.SetPrimary(edg.PrimaryFace, c2d, edg.Forward(edg.PrimaryFace));
                        c2d = edg.SecondaryFace.Surface.GetProjectedCurve(cv, Precision.eps);
                        SurfaceHelper.AdjustPeriodic(edg.SecondaryFace.Surface, br2, c2d);
                        if (!edg.Forward(edg.SecondaryFace)) c2d.Reverse();
                        part.SetSecondary(edg.SecondaryFace, c2d, edg.Forward(edg.SecondaryFace));
                    }
                    faceToMixedEdges[edg.PrimaryFace].Remove(edg);
                    faceToMixedEdges[edg.PrimaryFace].AddMany(splEdges);
                    faceToMixedEdges[edg.SecondaryFace].Remove(edg);
                    faceToMixedEdges[edg.SecondaryFace].AddMany(splEdges);
                    edg.Vertex1.RemoveEdge(edg); // die Kante vollstän aushängen, damit beim Konturverfolgen über die Vertices diese nicht berücksichtigt wird
                    edg.Vertex2.RemoveEdge(edg);
#if DEBUG
                    DebuggerContainer dc = new CADability.DebuggerContainer();
                    dc.Add(edg.Curve2D(edg.PrimaryFace), System.Drawing.Color.Red, -1);
                    dc.Add(edg.Curve2D(edg.SecondaryFace), System.Drawing.Color.Blue, -1);
                    for (int i = 0; i < splEdges.Count; i++)
                    {
                        dc.Add(splEdges[i].Curve2D(edg.PrimaryFace), System.Drawing.Color.Red, i);
                        dc.Add(splEdges[i].Curve2D(edg.SecondaryFace), System.Drawing.Color.Blue, i);
                    }
#endif
                }
            }
        }

        private bool distOk(GeoPoint position)
        {
            Face[] closeFaces = originalFaces.GetObjectsFromBox(new BoundingCube(position, Math.Abs(offset)));
            for (int i = 0; i < closeFaces.Length; i++)
            {
                if (closeFaces[i].Distance(position) < Math.Abs(offset) * (1 - 1e-6)) return false;
            }
            return true;
        }

        private void findOverlappingFaces()
        {
            overlappingFaces = new Dictionary<DoubleFaceKey, ModOp2D>();
            // Faces von verschiedenen Shells die identisch sind oder sich überlappen machen Probleme
            // beim Auffinden der Schnitte. Die Kanten und die Flächen berühren sich nur
            Set<DoubleFaceKey> candidates = new Set<DoubleFaceKey>(); // Kandidaten für parallele faces
            List<OctTree<BRepItem>.Node<BRepItem>> leaves = new List<OctTree<BRepItem>.Node<BRepItem>>(octTree.Leaves);
            foreach (OctTree<BRepItem>.Node<BRepItem> node in leaves)
            {
                foreach (BRepItem first in node.list)
                {
                    if (first.Type == BRepItem.ItemType.Face)
                    {
                        foreach (BRepItem second in node.list)
                        {
                            if (second.Type == BRepItem.ItemType.Face)
                            {
                                if (second.face != first.face)
                                {
                                    candidates.Add(new DoubleFaceKey(first.face, second.face));
                                }
                            }
                        }

                    }
                }
            }
            foreach (DoubleFaceKey df in candidates)
            {
                ModOp2D firstToSecond;
                BoundingRect ext1, ext2;
                df.face1.Surface.GetNaturalBounds(out ext1.Left, out ext1.Right, out ext1.Bottom, out ext1.Top);
                df.face2.Surface.GetNaturalBounds(out ext2.Left, out ext2.Right, out ext2.Bottom, out ext2.Top);
                // Achtung: SameGeometry geht z.Z. nur mit fester ModOp2D. Denkbar sind auch Fälle, bei denen es keine solche Modop gibt
                // aber dennoch die selbe Geometrie vorliegt: z.B. bei nicht-periodischem Zylinder oder verdrehter Kugel. Die brauchen wir aber auch!!!
                if (df.face1.Surface.SameGeometry(ext1, df.face2.Surface, ext2, octTree.precision, out firstToSecond))
                {   // es gilt nur, wenn die Orientierung die selbe ist
                    GeoVector n1 = df.face1.Surface.GetNormal(ext1.GetCenter());
                    GeoVector n2 = df.face2.Surface.GetNormal(firstToSecond * ext1.GetCenter());
                    if (n1 * n2 > 0)
                    {
                        overlappingFaces.Add(df, firstToSecond);
                        df.face1.UserData.Add("BRepIntersection.OverlapsWith", df.face2);
                        df.face2.UserData.Add("BRepIntersection.OverlapsWith", df.face1);
                    }
                }
            }
        }
        /// <summary>
        /// Erzeuge die Schnitte zwischen Edges und Faces von verschiedenen shells.
        /// edgesToSplit, intersectionVertices und facesToIntersectionVertices werden gesetzt
        /// </summary>
        private void createEdgeFaceIntersections()
        {
            edgesToSplit = new Dictionary<Edge, List<Vertex>>();
            intersectionVertices = new Set<IntersectionVertex>();
            facesToIntersectionVertices = new Dictionary<DoubleFaceKey, List<IntersectionVertex>>();
            // zuerst die potentiellen Kandidaten sammeln aus den Blättern des OctTrees
            Dictionary<EdgeFaceKey, List<OctTree<BRepItem>.Node<BRepItem>>> edgesToFaces = new Dictionary<EdgeFaceKey, List<OctTree<BRepItem>.Node<BRepItem>>>();
#if DEBUG
            DebuggerContainer dc0 = new DebuggerContainer();
#endif
            foreach (OctTree<BRepItem>.Node<BRepItem> node in octTree.Leaves)
            {
                foreach (BRepItem first in node.list)
                {
                    if (first.Type == BRepItem.ItemType.Edge)
                    {
                        Edge edge = first.edge;
#if DEBUG
                        if (edge.GetHashCode() == 117)
                        {
                            dc0.Add(edge.Curve3D as IGeoObject, edge.GetHashCode());
                        }
#endif
                        IGeoObjectOwner shell = edge.PrimaryFace.Owner;
                        foreach (BRepItem second in node.list)
                        {
                            if (second.Type == BRepItem.ItemType.Face)
                            {   // es gibt offene Kanten hier: negative Rundungsflächen werden garnicht erzeugt
                                if (second.face != edge.PrimaryFace && second.face != edge.SecondaryFace) // edge.SecondaryFace kann ruhig null sein, gebraucht wirds trotzdem
                                {   // keine Schnitte von Kanten, mit dem eigenen Face
                                    if (overlappingFaces.ContainsKey(new DoubleFaceKey(edge.PrimaryFace, second.face)) ||
                                        overlappingFaces.ContainsKey(new DoubleFaceKey(edge.SecondaryFace, second.face)) ||
                                        overlappingFaces.ContainsKey(new DoubleFaceKey(second.face, edge.PrimaryFace)) ||
                                        overlappingFaces.ContainsKey(new DoubleFaceKey(second.face, edge.SecondaryFace))) continue;
                                    List<OctTree<BRepItem>.Node<BRepItem>> addInto;
                                    EdgeFaceKey efk = new EdgeFaceKey(edge, second.face);
#if DEBUG
                                    if (edge.GetHashCode() == 117 && second.face.GetHashCode() == 45)
                                    {
                                        dc0.Add(second.face, second.face.GetHashCode());
                                    }
#endif
                                    if (!edgesToFaces.TryGetValue(efk, out addInto))
                                    {
                                        addInto = new List<OctTree<BRepItem>.Node<BRepItem>>();
                                        edgesToFaces[efk] = addInto;
                                    }
                                    addInto.Add(node);
                                }
                            }
                        }

                    }
                }
            }
            // edgesToFaces enthält jetzt alle schnittverdächtigen Paare
            // und dazu noch die nodes, wenn man Anfangswerte suchen würde...
            foreach (EdgeFaceKey ef in edgesToFaces.Keys)
            {
                Set<Vertex> commonVertices = new Set<Vertex>(ef.face.Vertices).Intersection(new Set<Vertex>(new Vertex[] { ef.edge.Vertex1, ef.edge.Vertex2 }));
                GeoPoint[] ip;
                GeoPoint2D[] uvOnFace;
                double[] uOnCurve3D;
                Border.Position[] position;
                if (ef.edge.Curve3D.Length < 1e-14) continue;
                double prec = octTree.precision / ef.edge.Curve3D.Length;
                ef.face.IntersectAndPosition(ef.edge, out ip, out uvOnFace, out uOnCurve3D, out position);
                for (int i = 0; i < ip.Length; ++i)
                {
                    if (uOnCurve3D[i] < -prec || uOnCurve3D[i] > 1.0 + prec || position[i] == Border.Position.Outside)
                    {
                        // Die Endpunkte sollen mit erzeugt werden, damit daraus später Schnittkanten entstehen können
                        // Beim Aufteilen der kanten dürfen die Endpunkte allerdings nicht mit verwendet werden
                        continue;
                    }
                    // wenn edge und face einen gemeinsamen vertex haben, dann muss das auch ein Schnittpunkt sein.
                    // wenn der Schnitt tangential ist, dann ist das hier manchmal knapp und wird deshalb hier nachgebessert
                    foreach (Vertex vtx in commonVertices)
                    {
                        if ((vtx.Position | ip[i]) < octTree.precision * 100)
                        {
                            ip[i] = vtx.Position;
                            uvOnFace[i] = vtx.GetPositionOnFace(ef.face);
                            if (uOnCurve3D[i] < 0.1) uOnCurve3D[i] = 0.0;
                            if (uOnCurve3D[i] > 0.9) uOnCurve3D[i] = 1.0;
                            position[i] = Border.Position.OnCurve;
                        }
                    }
#if DEBUG
                    foreach (Vertex vtx in ef.face.Vertices)
                    {
                        double d1 = vtx.Position | ef.edge.Vertex1.Position;
                        double d2 = vtx.Position | ef.edge.Vertex2.Position;
                    }
#endif
                    Vertex v = new Vertex(ip[i]);
                    v.AddPositionOnFace(ef.face, uvOnFace[i]);
                    IntersectionVertex iv = new IntersectionVertex();
                    iv.v = v;
                    iv.edge = ef.edge;
                    iv.face = ef.face;
                    iv.uOnEdge = uOnCurve3D[i];
                    iv.isOnFaceBorder = (position[i] == Border.Position.OnCurve); // das ist für später: Verbindung zweier Schnittpunkte, die beide auf dem Rand liegen, ist nicht sicher innerhalb
                    AddFacesToIntersectionVertices(ef.edge.PrimaryFace, ef.face, iv);
                    if (ef.edge.SecondaryFace != null) // es gibt auch offene Kanten!
                        AddFacesToIntersectionVertices(ef.edge.SecondaryFace, ef.face, iv);
                    intersectionVertices.Add(iv);
                    if (!edgesToSplit.ContainsKey(ef.edge)) edgesToSplit[ef.edge] = new List<Vertex>();
                    edgesToSplit[ef.edge].Add(v);
                }
            }
        }
        private void AddFacesToIntersectionVertices(Face f1, Face f2, IntersectionVertex iv)
        {
            List<IntersectionVertex> list;
            if (!facesToIntersectionVertices.TryGetValue(new DoubleFaceKey(f1, f2), out list))
            {
                list = new List<IntersectionVertex>();
                facesToIntersectionVertices[new DoubleFaceKey(f1, f2)] = list;
            }
            list.Add(iv);
        }
        private void splitEdges()
        {
            // 1. Alle Kanten an den Schnittpunkten aufbrechen und für die betroffenen
            // Faces die Liste der möglichen neuen Kanten erstellen
            foreach (KeyValuePair<Edge, List<Vertex>> kv in edgesToSplit)
            {
                Edge edge = kv.Key;
                Set<Vertex> vertexSet = new Set<Vertex>(kv.Value); // einzelne vertices können doppelt vorkommen
                SortedList<double, Vertex> sortedVertices = new SortedList<double, Vertex>();
                double prec = octTree.precision / edge.Curve3D.Length; // darf natürlich nicht 0 sein!
                foreach (Vertex v in vertexSet)
                {
                    double pos = edge.Curve3D.PositionOf(v.Position);
                    if (pos > prec && pos < 1 - prec && !sortedVertices.ContainsKey(pos)) sortedVertices.Add(pos, v); // keine Endpunkte, sonst entstehen beim Aufteilen Stücke der Länge 0
                    if (v != edge.Vertex1 && v != edge.Vertex2) v.RemoveEdge(edge);
                }
                if (sortedVertices.Count > 0)
                {
                    Edge[] splitted = edge.Split(sortedVertices, octTree.precision);
                    // edge wird in beiden faces durch die gesplitteten ersetzt. edge selbst wird dadurch bedeutungslos.
                    // Die Vertices sollen nicht mehr auf die Edge zeigen, da später beim Finden der neuen Umrandungen sonst Mehrdeutigkeiten entstehen. 
                    edge.Vertex1.RemoveEdge(edge);
                    edge.Vertex2.RemoveEdge(edge);
                }
            }
        }
        private void combineVertices()
        {
            VertexOcttree vo = new VertexOcttree(octTree.Extend, octTree.precision);
            Vertex[] vv = shell.Vertices;
            for (int i = 0; i < vv.Length; i++)
            {
                vo.AddObject(vv[i]);
            }
            // eigentlich sind die intersectionVertices -nach dem Splitten der Kanten- shcon alle in den shells vorhanden, so dachte ich zuerst, aber:
            // Schnittpunkte an den Enden der Kanten werden wohl nicht verwendet und kommen hier noch dazu
            foreach (IntersectionVertex iv in intersectionVertices)
            {
                vo.AddObject(iv.v); // die sind alle verschieden
            }
            vo.combineAll(intersectionVertices); // hier wird zusammengefasst
        }
        private void createSelfintersectionEdges()
        {
            // ein Torus ist ja immer in 2 Hälften geteilt, so dass sich eigentlich die beiden unabhängigen Teile schneiden
            // Der Schnitt ist allerdings in 3d singulär, und sollte als solcher eine extrabehandlung erfahren.
            // Sich selbst durchdringende Flächen sollten immer Paare gegenläufigen von Kurven erzeugen
            foreach (Face fc in shell.Faces)
            {
                ICurve2D[] sis = fc.Surface.GetSelfIntersections(fc.Area.GetExtent());
                if (sis != null && sis.Length > 0)
                {
                    for (int i = 0; i < sis.Length; i++)
                    {
                        double[] parts = fc.Area.Clip(sis[i], true);
                        for (int j = 0; j < parts.Length - 1; j += 2)
                        {

                        }
                    }
                }
            }
        }
        private void createNewEdges()
        {
            faceToMixedEdges = new Dictionary<Face, Set<Edge>>();
            // wir haben eine Menge Schnittpunkte, die Face-Paaren zugeordnet sind. Für jedes Face-Paar, welches Schnittpunkte enthält sollen hier die neuen Kanten bestimmt werden
            // Probleme dabei sind: 
            // - es ist bei mehr als 2 Schnittpunkten nicht klar, welche Abschnitte dazugehören
            // - zwei Surfaces können mehr als eine Schnittkurve haben
            Set<Edge> created = new Set<Edge>(new EdgeComparerByVertexAndFace());
            foreach (KeyValuePair<DoubleFaceKey, List<IntersectionVertex>> item in facesToIntersectionVertices)
            {
                List<Vertex> toConnectWith = new List<Vertex>(); // diese 3 Listen müssen synchron sein
                List<bool> isOnFaceBorder = new List<bool>();
                List<GeoPoint> points = new List<GeoPoint>();
                Set<int> usedVertices = new Set<int>(); // diese vertices nicht (mehr) verwenden
                Set<Edge> commonEdges = new Set<Edge>(item.Key.face1.AllEdges).Intersection(new Set<Edge>(item.Key.face2.AllEdges));
                // Eckpunkte, die die Endpunkte einer gemeinsamen Kante darstellen, nicht verwenden, die würden ja genau diese Kante liefern
                Set<Vertex> commonVtx = new Set<Vertex>();
                foreach (Edge edg in commonEdges)
                {
                    commonVtx.Add(edg.Vertex1);
                    commonVtx.Add(edg.Vertex2);
                    usedVertices.Add(edg.Vertex1.GetHashCode());
                    usedVertices.Add(edg.Vertex2.GetHashCode());
                }
                for (int i = 0; i < item.Value.Count; i++)
                {
                    if (!usedVertices.Contains(item.Value[i].v.GetHashCode()))
                    {
                        toConnectWith.Add(item.Value[i].v);
                        isOnFaceBorder.Add(item.Value[i].isOnFaceBorder);
                        points.Add(item.Value[i].v.Position);
                        usedVertices.Add(item.Value[i].v.GetHashCode());
                    }
                }
                commonVtx = (new Set<Vertex>(item.Key.face1.Vertices).Intersection(new Set<Vertex>(item.Key.face2.Vertices))).Difference(commonVtx);
                // das sind alle gemeinsamen Eckpunkte, die nicht zu gemeinsamen Kanten gehören. Dis müssen Start- oder Enpunkt von Kurven sein
                foreach (Vertex vtx in commonVtx)
                {
                    if (!usedVertices.Contains(vtx.GetHashCode()))
                    {
                        toConnectWith.Add(vtx);
                        isOnFaceBorder.Add(true);
                        points.Add(vtx.Position);
                        usedVertices.Add(vtx.GetHashCode());
                    }
                }
                if (toConnectWith.Count == 0) continue;
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                dc.Add(item.Key.face1, 1);
                dc.Add(item.Key.face2, 2);
                for (int i = 0; i < item.Value.Count; i++)
                {
                    dc.Add(item.Value[i].edge.Curve3D as IGeoObject, 10 + i);
                }
                for (int i = 0; i < points.Count; i++)
                {
                    dc.Add(points[i], System.Drawing.Color.Blue, i);
                }
#endif
                ICurve[] c3ds;
                ICurve2D[] crvsOnSurface1;
                ICurve2D[] crvsOnSurface2;
                double[,] params3d;
                double[,] params2dFace1;
                double[,] params2dFace2;
                GeoPoint2D[] paramsuvsurf1;
                GeoPoint2D[] paramsuvsurf2;
                if (Surfaces.Intersect(item.Key.face1.Surface, item.Key.face1.Area.GetExtent(), item.Key.face2.Surface, item.Key.face2.Area.GetExtent(), points, out c3ds, out crvsOnSurface1, out crvsOnSurface2, out params3d, out params2dFace1, out params2dFace2, out paramsuvsurf1, out paramsuvsurf2, octTree.precision))
                {
                    for (int i = 0; i < c3ds.Length; i++) // meist nur eine Kurve
                    {   // die Orientierung der 3d Kurve ist zufällig, hat nichts mit der Topologie der Shell zu tun.
                        // Die beiden 2d Kurven haben die selbe Orientierung wie die 3d Kurve, aber die beiden 2d Kurven müssen letztlich
                        // gegenläufig orientiert sein, so dass hier entschiedn werden muss, welche umgedreht wird
                        SortedDictionary<double, int> sortedIv = new SortedDictionary<double, int>(); // Indizes nach u aufsteigend sortiert und nur für Kurve i (meist ohnehin nur eine)
                        for (int j = 0; j < points.Count; j++)
                        {
                            // IntersectionVertex iv = item.Value[j];
                            if (params3d[i, j] == double.MinValue) continue; // dieser Schnittpunkt liegt auf einer anderen Kurve
                            sortedIv[params3d[i, j]] = j;
                        }
                        if (c3ds[i] is InterpolatedDualSurfaceCurve)
                        {
                            foreach (Vertex vtx in commonVtx)
                            {
                                double pos1 = c3ds[i].StartPoint | vtx.Position;
                                double pos2 = c3ds[i].EndPoint | vtx.Position;
                            }
                        }
                        // aus den mehreren Punkten (meist 2) unter Berücksichtigung der Richtungen Kanten erzeugen
                        // bei nicht geschlossenen Kurven sollten immer zwei aufeinanderfolgende Punkte einen gültigen Kurvenabschnitt bilden.
                        // Obwohl auch hierbei ein doppelt auftretenden Punkt ein Problem machen könnte (Fälle sind schwer vorstellbar).
                        // Im schlimmsten Fall müssten man für alle Abschnitte (nicht nur die geraden) 
                        // Bei geschlossenen Kurven ist nicht klar, welche Punktepaare gültige Kurvenabschnitte erzeugen. 
                        if (c3ds[i].IsClosed)
                        {
                            if (item.Key.face1.Area.Contains(crvsOnSurface1[i].StartPoint, false) && item.Key.face2.Area.Contains(crvsOnSurface2[i].StartPoint, false))
                            {
                                // der Startpunkt der geschlossenen 2d-Kurven (die ja dem Startpunkt der 3d Kurve entsprechen) gehört zu den gültigen Kurvenabschnitten
                                // also dürfen wir nicht mit dem kleinsten u beginnen, sondern müssen das Kurvenstück berücksichtigen, welches durch den Anfang  geht.
                                // Beachte: die trimm Funktion (sowohl in 2d als auch in 3d) muss berücksichtigen, dass bei geschlossenen Kurven, der 1. index größer sein kann
                                // als der 2. Index. Das bedeutet dann, das Stück, welches über "0" geht soll geliefert werden. Noch überprüfen, ob das überall implementiert ist
                                // hier wird das erste Element aus dem SortedDictionary entfernt und hinten wieder eingefügt
                                var fiter = sortedIv.GetEnumerator(); // so bekommt man das erste, First() scheint es komischerweise nicht zu geben
                                fiter.MoveNext();
                                double u0 = fiter.Current.Key;
                                int j0 = fiter.Current.Value;
                                sortedIv.Remove(u0);
                                sortedIv[u0 + 1] = j0;
                            }
                            //double dbgu1 = Math.Min(params3d[0], params3d[1]);
                            //double dbgu2 = Math.Max(params3d[0], params3d[1]);
                            //ICurve[] parts = c3ds[i].Split(dbgu1,dbgu2);
                            //ICurve dbgtr = c3ds[i].Clone();
                            //dbgtr.Trim(dbgu2, dbgu1);
                            //ICurve2D dbg1 = crvsOnSurface1[i];
                            //ICurve2D dbg2 = crvsOnSurface2[i];
                            //ICurve2D tr1 = dbg1.Trim(dbgu2, dbgu1);
                            //ICurve2D tr1r = dbg1.Trim(dbgu1, dbgu2);
                            //ICurve2D tr2 = dbg2.Trim(dbgu2, dbgu1);
                            //ICurve2D tr2r = dbg2.Trim(dbgu1, dbgu2);
                        }
                        var iter = sortedIv.GetEnumerator();
                        while (iter.MoveNext())
                        {
                            int j1 = iter.Current.Value;
                            double u1 = params3d[i, j1];
                            if (!iter.MoveNext()) break;
                            int j2 = iter.Current.Value;
                            double u2 = params3d[i, j2];
                            ICurve tr = c3ds[i].Clone();
                            tr.Trim(u1, u2);

                            if (Math.Abs(params2dFace1[i, j2] - params2dFace1[i, j1]) < 1e-6 || Math.Abs(params2dFace2[i, j2] - params2dFace2[i, j1]) < 1e-6) continue;
                            ICurve2D con1 = crvsOnSurface1[i].Trim(params2dFace1[i, j1], params2dFace1[i, j2]);
                            ICurve2D con2 = crvsOnSurface2[i].Trim(params2dFace2[i, j1], params2dFace2[i, j2]);
                            //if (params2dFace1[j1]<params2dFace1[j2]) con1 = crvsOnSurface1[i].Trim(params2dFace1[j1], params2dFace1[j2]);
                            //else
                            //{
                            //    con1 = crvsOnSurface1[i].Trim(params2dFace1[j2], params2dFace1[j1]);
                            //    con1.Reverse();
                            //}
                            //if (params2dFace2[j1]<params2dFace2[j2]) con2 = crvsOnSurface2[i].Trim(params2dFace2[j1], params2dFace2[j2]);
                            //else
                            //{
                            //    con2 = crvsOnSurface2[i].Trim(params2dFace2[j2], params2dFace2[j1]);
                            //    con2.Reverse();
                            //}
                            // das Kreuzprodukt im Start (oder End oder Mittel) -Punkt hat die selbe Reichung wie die 3d Kurve: con2 umdrehen
                            // andere Richtung: con1 umdrehen
                            double skp = ((item.Key.face1.Surface.GetNormal(paramsuvsurf1[j1]) ^ item.Key.face2.Surface.GetNormal(paramsuvsurf2[j1])) * tr.StartDirection);
                            // es kann sein, dass die Flächen in j1 tangential sind, dann kann man hier nicht gut messen
                            if (Math.Abs(skp) < 1e-6)
                            {
                                double skpe = ((item.Key.face1.Surface.GetNormal(paramsuvsurf1[j2]) ^ item.Key.face2.Surface.GetNormal(paramsuvsurf2[j2])) * tr.EndDirection);
                                if (Math.Abs(skpe) > Math.Abs(skp)) skp = skpe;
                            }
                            bool dirs1 = skp < 0;
                            if (offset < 0) dirs1 = !dirs1; // das ist mal ein erster Versuch, die Orientierung zu verwenden, scheint zu funktionieren
                                                            // bei diesem Skalarprodukt von 2 Vektoren, die entweder die selbe oder die entgegengesetzte Richtung haben ist ">0" unkritisch
                            if (dirs1) con2.Reverse();
                            else con1.Reverse();
                            if ((isOnFaceBorder[j1] && isOnFaceBorder[j2]))
                            {   // beide Endpunkte der Schnittlinie befinden sich auf dem Rand der Faces: dann ist nicht sicher, dass die Schnittkurve innerhalb des Faces liegt
                                GeoPoint2D uv = con1.PointAt(0.5);
                                if (!item.Key.face1.Contains(ref uv, true)) continue;
                                uv = con2.PointAt(0.5);
                                if (!item.Key.face2.Contains(ref uv, true)) continue;
                            }
                            // man muss con1 oder con2 ggf. umdrehen!!
                            Edge edge = new Edge(item.Key.face1, tr, item.Key.face1, con1, dirs1, item.Key.face2, con2, !dirs1);
                            edge.Vertex1 = toConnectWith[j1];    // damit wird diese Kante mit den beiden Schnittvertices verbunden
                            edge.Vertex2 = toConnectWith[j2];
                            if (created.Contains(edge))
                            {
                                edge.Vertex1.RemoveEdge(edge);
                                edge.Vertex2.RemoveEdge(edge);
                            }
                            else
                            {
                                created.Add(edge);
                                // diese neue Kante in das Dictionary einfügen
                                Set<Edge> addTo;
                                if (!faceToMixedEdges.TryGetValue(item.Key.face1, out addTo))
                                {
                                    addTo = new Set<Edge>(); // (new EdgeComparerByVertex()); // damit werden zwei Kanten mit gleichen Vertices nicht zugefügt, nutzt nichts
                                    faceToMixedEdges[item.Key.face1] = addTo;
                                }
                                addTo.Add(edge);
                                if (!faceToMixedEdges.TryGetValue(item.Key.face2, out addTo))
                                {
                                    addTo = new Set<Edge>(); //  (new EdgeComparerByVertex());
                                    faceToMixedEdges[item.Key.face2] = addTo;
                                }
                                addTo.Add(edge);
                            }
                        }
                    }
                }
            }
        }
    }
#if DEBUG
    public class BRepTester
    {
        public static void Test(Face fc1, Face fc2)
        {
            DebuggerContainer dc = new CADability.DebuggerContainer();
            List<GeoPoint> points = new List<GeoPoint>();
            foreach (Edge edg in fc2.AllEdgesIterated())
            {
                GeoPoint[] ip;
                GeoPoint2D[] uvOnFace;
                double[] uOnCurve3D;
                Border.Position[] position;
                fc1.IntersectAndPosition(edg, out ip, out uvOnFace, out uOnCurve3D, out position);
                for (int i = 0; i < ip.Length; i++)
                {
                    dc.Add(ip[i], System.Drawing.Color.Blue, i);
                    points.Add(ip[i]);
                }
            }
            foreach (Edge edg in fc1.AllEdgesIterated())
            {
                GeoPoint[] ip;
                GeoPoint2D[] uvOnFace;
                double[] uOnCurve3D;
                Border.Position[] position;
                fc2.IntersectAndPosition(edg, out ip, out uvOnFace, out uOnCurve3D, out position);
                for (int i = 0; i < ip.Length; i++)
                {
                    dc.Add(ip[i], System.Drawing.Color.Red, i);
                    points.Add(ip[i]);
                }
            }
            dc.Add(fc1);
            dc.Add(fc2);
            ICurve[] c3ds;
            ICurve2D[] crvsOnSurface1;
            ICurve2D[] crvsOnSurface2;
            double[,] params3d;
            double[,] params2dFace1;
            double[,] params2dFace2;
            GeoPoint2D[] paramsuvsurf1;
            GeoPoint2D[] paramsuvsurf2;
            if (Surfaces.Intersect(fc1.Surface, fc1.Area.GetExtent(), fc2.Surface, fc2.Area.GetExtent(), points, out c3ds, out crvsOnSurface1, out crvsOnSurface2, out params3d, out params2dFace1, out params2dFace2, out paramsuvsurf1, out paramsuvsurf2, Precision.eps))
            {
                for (int i = 0; i < c3ds.Length; i++)
                {
                    for (int j = 0; j < points.Count; j++)
                    {
                        double d = c3ds[i].StartPoint | points[j];
                        d = c3ds[i].EndPoint | points[j];
                    }
                }
            }
        }
    }
#endif

    internal class BRepSewFaces
    {

    }
}
