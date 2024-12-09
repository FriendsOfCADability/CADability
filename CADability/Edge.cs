using CADability.Actions;
using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using Wintellect.PowerCollections;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability
{
    /*
     * Edge (Kante) ist ein abstraktes Ding, kein GeoObject. Es ist die Kante einer Fläche (Face) oder auch die
     * gemeinsame Kante zweier Flächen. Es gibt zum einen ein ICurve, welches die 3D Form der Kante darstellt,
     * zum anderen Rückverweise auf die Flächen und ICurve2D Objekte, die die Kanten im (u/v) System der 
     * Flächen darstellen. Jede Kante existiert nur einmal, auch wenn sie zu zwei Flächen gehört.
     * Es kann auch die (innere) Kante eines Loches sein, oder nur eine Kante auf der Fläche, die als 
     * Umrisskurve dient.
     */

    /// <summary>
    /// Mit dieser Klasse kann eine ICurve2D in einem Border verwurstet werden, also z.B. gesplittet werden
    /// und man weiß imme noch aus welcher Edge es kommt.
    /// </summary>
    internal class EdgeReference : IManageUserData
    {
        public static string UserDataName = "CADability.EdgeReference"; // mit diesem Namen wird es in der UserData Liste gehalten
        public Edge referencedEdge;
        public EdgeReference previous; // gehörte vorher zu dieser Edge
        public EdgeReference(Edge referencedEdge)
        {
            this.referencedEdge = referencedEdge;
        }

        #region IManageUserData Members

        object IManageUserData.Clone()
        {
            return new EdgeReference(referencedEdge);
        }

        CADability.UserInterface.IShowProperty IManageUserData.GetShowProperty()
        {
            return null;
        }

        void IManageUserData.Overriding(IManageUserData newValue)
        {
            EdgeReference nv = newValue as EdgeReference;
            if (nv != null)
            {
                nv.previous = this;
            }
        }

        #endregion
    }

    /// <summary>
    /// Describes an edge as imported from a step file. The edge may be decomposed into multiple edges for CADability faces
    /// in case of periodic faces.
    /// </summary>
    internal class StepEdgeDescriptor
    {
#if DEBUG
        public int stepDefiningIndex;
#endif
        private static string lockStatic = ""; // a private static object to assure each instance has a unique id
        private static int idCounter = 0;
        public int id;
        public ICurve curve;
        public Vertex vertex1, vertex2;
        public bool forward;
        public bool isSeam = false; // is part of a seam, the same edge exists in different directions on the same face
        public ICurve2D curve2d;
        public List<Edge> createdEdges; // With periodic surfaces the edge might be split. Then we have two (ore even more) edges corresponding to a single step edge.
        public class Locker
        {
            public StepEdgeDescriptor original;
            public Locker(StepEdgeDescriptor original)
            {
                this.original = original;
            }
        }
        public Locker locker;

        public StepEdgeDescriptor(ICurve curve)
        {
            this.curve = curve;
            this.vertex1 = null;
            this.vertex2 = null;
            this.forward = true;
            this.createdEdges = new List<Edge>(1); // empty list, so that clones use the same list
            lock (lockStatic)
            {
                this.id = idCounter++;
            }
            locker = new Locker(this);
        }
        public StepEdgeDescriptor(ICurve curve, Vertex vertex1, Vertex vertex2, bool forward)
        {
            this.curve = curve;
            this.vertex1 = vertex1;
            this.vertex2 = vertex2;
            this.forward = forward;
            this.createdEdges = new List<Edge>(1); // empty list, so that clones use the same list
            lock (lockStatic)
            {
                id = idCounter++;
            }
            locker = new Locker(this);
        }
        public StepEdgeDescriptor(StepEdgeDescriptor toClone, Vertex vertex1, Vertex vertex2, bool forward)
        {
            this.curve = toClone.curve;
            this.createdEdges = toClone.createdEdges;
            this.vertex1 = vertex1;
            this.vertex2 = vertex2;
            this.forward = forward;
#if DEBUG
            this.stepDefiningIndex = toClone.stepDefiningIndex;
#endif
            lock (lockStatic)
            {
                id = idCounter++;
            }
            locker = toClone.locker;
        }
        public StepEdgeDescriptor(Edge edge, bool forward)
        {
            curve = edge.Curve3D;
            createdEdges = new List<Edge>(1); // in most cases only one Edge, multiple edges arise only when splitting cyclical (periodic) faces
            createdEdges.Add(edge);
            vertex1 = edge.Vertex1;
            vertex2 = edge.Vertex2;
            this.forward = forward;
            lock (lockStatic)
            {
                id = idCounter++;
            }
            locker = new Locker(this);
        }
        internal void MakeVertices()
        {
            vertex1 = new Vertex(curve.StartPoint);
            vertex2 = new Vertex(curve.EndPoint);
        }
        public Vertex FirstVertex
        {
            get
            {
                if (forward) return vertex1;
                else return vertex2;
            }
        }
        public Vertex LastVertex
        {
            get
            {
                if (forward) return vertex2;
                else return vertex1;
            }
        }
    }

    /* KONZEPT: exakte 3D Kurve und 2D Kurve
     * Bei Schnitten von Zylinder, Kegel, Kugel, Torus untereinander entstehen Kurven,die angenähert werden müssen (NURBS).
     * Statt dieser Kurven könnte man eine neue Art Kurve definieren, die durch wenige Punkte (n) gegeben ist. 
     * Die PointAt  für u==i/n sind damit definiert, die Zwischenpunkte werden so definiert: gemäß dem u wird
     * auf der Verbindung der benachbarten beiden Strützpunkte die Position auf dieser Linie bestimmt und eine
     * Ebene senkrecht zu dieser Verbindung in dem gebenen Punkt. Diese Ebene schneidet beide Oberflächen in einer
     * Ellipse (Parabel, Hyperbel) (bis auf Torus) und die Schnittpunkte der Ellipsen ist der gesuchte Punkt. PositionOf ist identisch mit
     * Position of der durch die Punkte gegebenen Polylinie. Diese Kurve ist immer exakt und vermutlich auch nicht 
     * langsamer als ein NURBS. Wenn wir Wendepunkte in der Fläche ausschließen können kann man den Kurvenpunkt
     * mit Newton-Verfahren schnell annähern. Bei Nurbs ist das aber nicht sicher. Bei Torus wohl schon.
     * 
     * 
     * 2D Kurve als Projektion einer 3D Kurve: Die 2D Kurve ist mit identischem u Parameter (evtl. 1-u) und PositionOf
     * aus der 3D Kurve bestimmt. Auch DirectionAt geht einfach durch die Tangentialebene in dem zugehörigen Flächenpunkt.
     * Man kann eine Dreieckshülle bilden (Wendepunkte finden?) und wie die NURBS verschneiden.
     * 
     */

    /// <summary>
    /// Edge is a abstract description of an egde on a <see cref="Face"/>. An Edge may belong to one or two faces.
    /// Edges don't exist without faces (use <see cref="IGeoObject"/> and <see cref="ICurve"/>derived classes for
    /// simple 3d curves). The Edge is defined in several ways, which are overdetermined and therfore must always
    /// be in a consistent state: It is the pur 3-dimensional curve, an <see cref="ICurve"/>, and on each Face
    /// the edge is defined as a 2-dimensional curve on the surface (see <see cref="Face.Surface"/>), which has 
    /// a 2-dimensional (u/v) coordinate system (parametric space).
    /// The Edge may be an outer or an inner edge on each face or some curve on the inside of a face (typically an
    /// outlining curve for a certain <see cref="Projection"/>). The edge may not be outside of a face.
    /// </summary>
    [Serializable()]
    [DebuggerDisplayAttribute("Edge, hc = {hashCode.ToString()}")]
    public class Edge : ISerializable, IGeoObjectOwner, IDeserializationCallback, IComparable<Edge>, IJsonSerialize, IJsonSerializeDone, IExportStep
    {
        private ICurve curve3d;
        private Face primaryFace;
        private ICurve2D curveOnPrimaryFace;
        private bool forwardOnPrimaryFace;
        private Face secondaryFace; // kann fehlen
        private ICurve2D curveOnSecondaryFace; // kann fehlen
        private bool forwardOnSecondaryFace;
        private IGeoObject owner; // es gibt nur einen Besitzer, wenn sie zu zwei Faces gehört, dann ist es mindestens ein Shell
        private Edge isPartOf;
        private double startAtOriginal, endAtOriginal; // start und endparameter auf dem originaledge (wenn isPartOf true ist)
        internal static int hashCodeCounter = 0; // jedes Face bekommt eine Nummer, damit ist es für den HasCode Algorithmus einfach
        private int hashCode;
        private Vertex v1, v2;
        private bool oriented; // obsolete, if false (forwardOnPrimaryFace, forwardOnSecondaryFace) have not yet been calculated
        enum EdgeKind { unknown, sameSurface, tangential, sharp }
        private EdgeKind edgeKind = EdgeKind.unknown;
        internal BRepOperation.EdgeInfo edgeInfo; // only used for BRepOperation
        // TODO: überprüfen, ob isPartOf und startAtOriginal, endAtOriginal noch gebraucht wird (evtl. zu einem Objekt machen)
        // TODO: ist owner nicht immer primaryFace?
        public void FreeCachedMemory()
        {
        }
        internal void MakeVertices(Vertex known1, Vertex known2, Vertex known3, Vertex known4)
        {
            if (v1 == null)
            {
                if (curve3d != null)
                {
                    GeoPoint sp = curve3d.StartPoint;
                    GeoPoint ep = curve3d.EndPoint;
                    if (Precision.IsEqual(sp, known1.Position))
                    {
                        v1 = known1;
                    }
                    else if (Precision.IsEqual(sp, known2.Position))
                    {
                        v1 = known2;
                    }
                    else if (Precision.IsEqual(sp, known3.Position))
                    {
                        v1 = known3;
                    }
                    else if (Precision.IsEqual(sp, known4.Position))
                    {
                        v1 = known4;
                    }
                    else
                    {
                        v1 = new Vertex(sp);
                    }
                    v1.AddEdge(this);
                    if (Precision.IsEqual(ep, known1.Position))
                    {
                        v2 = known1;
                    }
                    else if (Precision.IsEqual(ep, known2.Position))
                    {
                        v2 = known2;
                    }
                    else if (Precision.IsEqual(ep, known3.Position))
                    {
                        v2 = known3;
                    }
                    else if (Precision.IsEqual(ep, known4.Position))
                    {
                        v2 = known4;
                    }
                    else
                    {
                        v2 = new Vertex(sp);
                    }
                    v2.AddEdge(this);
                }
                else
                {   // singulär
                    GeoPoint p = primaryFace.Surface.PointAt(curveOnPrimaryFace.EndPoint);
                    if (Precision.IsEqual(p, known1.Position))
                    {
                        v1 = known1;
                    }
                    else if (Precision.IsEqual(p, known2.Position))
                    {
                        v1 = known2;
                    }
                    else
                    {   // sollte nicht vorkommen
                        v1 = new Vertex(p);
                    }
                    v1.AddEdge(this);
                    v2 = v1;
                }
            }
        }
        internal void MakeVertices(Vertex known1, Vertex known2)
        {
            if (v1 == null)
            {
                if (curve3d != null)
                {
                    GeoPoint sp = curve3d.StartPoint;
                    GeoPoint ep = curve3d.EndPoint;
                    if (Precision.IsEqual(sp, known1.Position))
                    {
                        v1 = known1;
                    }
                    else if (Precision.IsEqual(sp, known2.Position))
                    {
                        v1 = known2;
                    }
                    else
                    {
                        v1 = new Vertex(sp);
                    }
                    v1.AddEdge(this);
                    if (Precision.IsEqual(ep, known1.Position))
                    {
                        v2 = known1;
                    }
                    else if (Precision.IsEqual(ep, known2.Position))
                    {
                        v2 = known2;
                    }
                    else
                    {
                        v2 = new Vertex(sp);
                    }
                    v2.AddEdge(this);
                }
                else
                {   // singulär
                    GeoPoint p = primaryFace.Surface.PointAt(curveOnPrimaryFace.EndPoint);
                    if (Precision.IsEqual(p, known1.Position))
                    {
                        v1 = known1;
                    }
                    else if (Precision.IsEqual(p, known2.Position))
                    {
                        v1 = known2;
                    }
                    else
                    {   // sollte nicht vorkommen
                        v1 = new Vertex(p);
                    }
                    v1.AddEdge(this);
                    v2 = v1;
                }
            }
        }
        internal void MakeVertices()
        {
            if (v1 == null)
            {
                if (curve3d != null)
                {
                    GeoPoint sp = curve3d.StartPoint;
                    GeoPoint ep = curve3d.EndPoint;
                    if (Precision.IsEqual(sp, ep))
                    {
                        v2 = v1 = new Vertex(sp);
                        v1.AddEdge(this);
                        SpreadVertex(v1);
                    }
                    else
                    {
                        v1 = new Vertex(sp);
                        GeoPoint2D pruv;
                        if (forwardOnPrimaryFace) pruv = curveOnPrimaryFace.StartPoint;
                        else pruv = curveOnPrimaryFace.EndPoint;
                        if (secondaryFace != null)
                        {
                            GeoPoint2D scuv;
                            if (forwardOnSecondaryFace) scuv = curveOnSecondaryFace.StartPoint;
                            else scuv = curveOnSecondaryFace.EndPoint;
                            v1.AddEdge(this, pruv, scuv);
                        }
                        else
                        {
                            v1.AddEdge(this, pruv);
                        }
                        if (v2 == null)
                        {
                            v2 = new Vertex(ep);
                            if (forwardOnPrimaryFace) pruv = curveOnPrimaryFace.EndPoint;
                            else pruv = curveOnPrimaryFace.StartPoint;
                            if (secondaryFace != null)
                            {
                                GeoPoint2D scuv;
                                if (forwardOnSecondaryFace) scuv = curveOnSecondaryFace.EndPoint;
                                else scuv = curveOnSecondaryFace.StartPoint;
                                v2.AddEdge(this, pruv, scuv);
                            }
                            else
                            {
                                v2.AddEdge(this, pruv);
                            }
                            SpreadVertex(v2);
                        }
                        else
                        {
                            v2.AddEdge(this);
                        }
                        SpreadVertex(v1);
                    }
                }
                else
                {   // singulär
                    GeoPoint p = primaryFace.Surface.PointAt(curveOnPrimaryFace.EndPoint);
                    v1 = new Vertex(p);
                    v1.AddEdge(this, curveOnPrimaryFace.EndPoint);
                    v2 = v1;
                    SpreadVertex(v1);
                }
            }
            if (v2 == null) // also einer schon gesetzt
            {
                if (curve3d != null)
                {
                    GeoPoint sp = curve3d.StartPoint;
                    GeoPoint ep = curve3d.EndPoint;
                    GeoPoint2D pruv, scuv = GeoPoint2D.Origin;
                    if ((v1.Position | sp) < (v1.Position | ep))
                    {
                        v2 = new Vertex(ep);
                        if (forwardOnPrimaryFace) pruv = curveOnPrimaryFace.EndPoint;
                        else pruv = curveOnPrimaryFace.StartPoint;
                        if (secondaryFace != null)
                        {
                            if (forwardOnSecondaryFace) scuv = curveOnSecondaryFace.EndPoint;
                            else scuv = curveOnSecondaryFace.StartPoint;
                        }
                    }
                    else
                    {
                        v2 = new Vertex(sp);
                        if (forwardOnPrimaryFace) pruv = curveOnPrimaryFace.StartPoint;
                        else pruv = curveOnPrimaryFace.EndPoint;
                        if (secondaryFace != null)
                        {
                            if (forwardOnSecondaryFace) scuv = curveOnSecondaryFace.StartPoint;
                            else scuv = curveOnSecondaryFace.EndPoint;
                        }
                    }
                    if (secondaryFace != null)
                    {
                        v2.AddEdge(this, pruv, scuv);
                    }
                    else
                    {
                        v2.AddEdge(this, pruv);
                    }
                    SpreadVertex(v2);
                }
                else
                {   // singulär und v1 schon gesetzt
                    v2 = v1;
                    SpreadVertex(v1);
                }
            }
        }
        private void SpreadVertex(Vertex v)
        {   // gib diesen Vertex weiter an die mit dieser Kante verbunden Kanten
            GeoPoint2D pruv, scuv = GeoPoint2D.Origin;
            if (primaryFace != null)
            {
                Edge[] adj = primaryFace.FindAdjacentEdges(this);
                for (int i = 0; i < adj.Length; ++i)
                {
                    adj[i].SetVertex(v);
                }
            }
            if (secondaryFace != null && secondaryFace != primaryFace)
            {
                Edge[] adj = secondaryFace.FindAdjacentEdges(this);
                for (int i = 0; i < adj.Length; ++i)
                {
                    adj[i].SetVertex(v);
                }
            }
        }
        private void SetVertex(Vertex v)
        {
            if (curve3d != null)
            {
                GeoPoint sp = curve3d.StartPoint;
                GeoPoint ep = curve3d.EndPoint;
                if (Precision.IsEqual(v.Position, sp))
                {
                    if (v1 == null)
                    {
                        v1 = v;
                        GeoPoint2D pruv;
                        if (forwardOnPrimaryFace) pruv = curveOnPrimaryFace.StartPoint;
                        else pruv = curveOnPrimaryFace.EndPoint;
                        if (secondaryFace != null)
                        {
                            GeoPoint2D scuv;
                            if (forwardOnSecondaryFace) scuv = curveOnSecondaryFace.StartPoint;
                            else scuv = curveOnSecondaryFace.EndPoint;
                            v1.AddEdge(this, pruv, scuv);
                        }
                        else
                        {
                            v1.AddEdge(this, pruv);
                        }
                        SpreadVertex(v);
                    }
                    else
                    {
                        if (Precision.IsEqual(ep, sp) && v2 == null)
                        {
                            v2 = v;
                        }
                        else
                        {
                            // throw new ApplicationException("CADability internal error, Edge.SetVertex");
                        }
                    }
                }
                if (Precision.IsEqual(v.Position, ep))
                {
                    if (v2 == null)
                    {
                        v2 = v;
                        GeoPoint2D pruv;
                        if (forwardOnPrimaryFace) pruv = curveOnPrimaryFace.EndPoint;
                        else pruv = curveOnPrimaryFace.StartPoint;
                        if (secondaryFace != null)
                        {
                            GeoPoint2D scuv;
                            if (forwardOnSecondaryFace) scuv = curveOnSecondaryFace.EndPoint;
                            else scuv = curveOnSecondaryFace.StartPoint;
                            v2.AddEdge(this, pruv, scuv);
                        }
                        else
                        {
                            v2.AddEdge(this, pruv);
                        }
                        SpreadVertex(v);
                    }
                    else
                    {
                        // throw new ApplicationException("CADability internal error, Edge.SetVertex");
                    }
                }
            }
            else
            {   // singulär
                GeoPoint p = primaryFace.Surface.PointAt(curveOnPrimaryFace.StartPoint);
                if (Precision.IsEqual(v.Position, p))
                {
                    if (v1 == null)
                    {
                        v1 = v;
                        v2 = v;
                        v.AddEdge(this);
                        SpreadVertex(v); // singulär, muss weiter verfolgt werden
                    }
                    else
                    {
                        // throw new ApplicationException("CADability internal error, Edge.SetVertex");
                    }
                }
            }
        }
        public Vertex Vertex1
        {
            get
            {
                if (v1 == null) MakeVertices();
                return v1;
            }
            internal set
            {
                if (v1 != null) v1.RemoveEdge(this);
                v1 = value;
                if (primaryFace != null && secondaryFace != null)
                {
                    GeoPoint2D pruv, scuv;
                    if (forwardOnPrimaryFace) pruv = PrimaryCurve2D.StartPoint;
                    else pruv = PrimaryCurve2D.EndPoint;
                    if (forwardOnSecondaryFace) scuv = SecondaryCurve2D.StartPoint;
                    else scuv = SecondaryCurve2D.EndPoint;
                    v1.AddEdge(this, pruv, scuv);

                }
                else if (primaryFace != null)
                {
                    if (forwardOnPrimaryFace) v1.AddEdge(this, PrimaryCurve2D.StartPoint);
                    else v1.AddEdge(this, PrimaryCurve2D.EndPoint);
                }
            }
        }
        public Vertex Vertex2
        {
            get
            {
                if (v2 == null) MakeVertices();
                return v2;
            }
            internal set
            {
                if (v2 != null) v2.RemoveEdge(this);
                v2 = value;
                if (primaryFace != null && secondaryFace != null)
                {
                    GeoPoint2D pruv, scuv;
                    if (!forwardOnPrimaryFace) pruv = PrimaryCurve2D.StartPoint;
                    else pruv = PrimaryCurve2D.EndPoint;
                    if (!forwardOnSecondaryFace) scuv = SecondaryCurve2D.StartPoint;
                    else scuv = SecondaryCurve2D.EndPoint;
                    v2.AddEdge(this, pruv, scuv);

                }
                else if (primaryFace != null)
                {
                    if (!forwardOnPrimaryFace) v2.AddEdge(this, PrimaryCurve2D.StartPoint);
                    else v2.AddEdge(this, PrimaryCurve2D.EndPoint);
                }
            }
        }
        /// <summary>
        /// Returns the start vertex in the direction of the edge on the provided face
        /// </summary>
        /// <param name="onThisFace">The face that specifies the orientation</param>
        /// <returns>The requested vertex</returns>
        public Vertex StartVertex(Face onThisFace)
        {
            if (!oriented) Orient();
            if (v1 == null || v2 == null) MakeVertices();
            if (onThisFace == primaryFace)
            {
                if (forwardOnPrimaryFace) return v1;
                else return v2;
            }
            if (onThisFace == secondaryFace)
            {
                if (forwardOnSecondaryFace) return v1;
                else return v2;
            }
            return null;
        }
        internal GeoPoint2D EndPosition(Face onThisFace)
        {
            if (onThisFace == primaryFace)
            {
                return curveOnPrimaryFace.EndPoint;
            }
            if (onThisFace == secondaryFace)
            {
                return curveOnSecondaryFace.EndPoint;
            }
            throw new System.ApplicationException("Edge.EndPosition, wrong parameter");
        }

        internal void SetVerticesPositions()
        {
            if (v1 != null && curve3d != null) v1.Position = curve3d.StartPoint;
            if (v2 != null && curve3d != null) v2.Position = curve3d.EndPoint;
        }
        internal void ReflectModification()
        {
            if (primaryFace.Surface.UvChangesWithModification)
            {
                PrimaryCurve2D = primaryFace.Surface.GetProjectedCurve(Curve3D, 0.0);
                if (!forwardOnPrimaryFace) PrimaryCurve2D.Reverse();
            }
            if (secondaryFace != null && secondaryFace.Surface.UvChangesWithModification)
            {
                SecondaryCurve2D = secondaryFace.Surface.GetProjectedCurve(Curve3D, 0.0);
                if (!forwardOnSecondaryFace) SecondaryCurve2D.Reverse();
            }
            if (curveOnPrimaryFace is ProjectedCurve pcp) pcp.ReflectModification(primaryFace.Surface, curve3d);
            if (curveOnSecondaryFace is ProjectedCurve pcs) pcs.ReflectModification(secondaryFace.Surface, curve3d);
        }

        internal GeoPoint2D StartPosition(Face onThisFace)
        {
            if (onThisFace == primaryFace)
            {
                return curveOnPrimaryFace.StartPoint;
            }
            if (onThisFace == secondaryFace)
            {
                return curveOnSecondaryFace.StartPoint;
            }
            throw new System.ApplicationException("Edge.StartPosition, wrong parameter");
        }
        public Vertex EndVertex(Face onThisFace)
        {
            if (!oriented) Orient();
            if (v1 == null || v2 == null) MakeVertices();
            if (onThisFace == primaryFace)
            {
                if (forwardOnPrimaryFace) return v2;
                else return v1;
            }
            if (onThisFace == secondaryFace)
            {
                if (forwardOnSecondaryFace) return v2;
                else return v1;
            }
            return null;
        }
        internal void Orient()
        {
            if (curveOnPrimaryFace == null) return;
            if (curve3d is Ellipse && (curve3d as Ellipse).IsClosed)
            {   // geschlosssene Kreise sind manchmal mit ihren 2d geschlossenen Bögen nicht synchron.
                // wäre natürlich gut zu wissen, woher das kommt
                if (curveOnPrimaryFace is Arc2D && curveOnPrimaryFace.IsClosed)
                {
                    GeoPoint2D sp = primaryFace.Surface.PositionOf(curve3d.StartPoint);
                    // und das müsste natürlich der Startpunkt unseres 2d Bogens sein
                    Arc2D a2d = curveOnPrimaryFace as Arc2D;
                    a2d.StartAngle = new Angle(sp.x - a2d.Center.x, sp.y - a2d.Center.y);
                }
                if (curveOnSecondaryFace is Arc2D)
                {
                    GeoPoint2D sp = secondaryFace.Surface.PositionOf(curve3d.StartPoint);
                    // und das müsste natürlich der Startpunkt unseres 2d Bogens sein
                    Arc2D a2d = curveOnSecondaryFace as Arc2D;
                    a2d.StartAngle = new Angle(sp.x - a2d.Center.x, sp.y - a2d.Center.y);
                }
            }
            // Die Richtung der Kanten ist so definiert:
            // Die 3D Kurve hat eine beliebige Richtung
            // Auf einem Face gehet die Outline linksrum, die Holes rechtsrum
            // forwardOnPrimaryFace bedeutet die 3D Kurve ist im Sinne des primaryFaces richtig orientiert
            // forwardOnPrimaryFace und forwardOnSecondaryFace müssen somit immer entgegengesetzt sein
            // enn zwei Flächen richtig zusammengesetzt sind
            // die 2d Kurven sind im Sinne des Faces orientiert
            if (curve3d != null && v1 != null && v2 != null)
            {
                if (Precision.IsEqual(v1.Position, v2.Position))
                {   // Start- und Endpunkt gleich: innere Punkte testen
                    double pos1 = curve3d.PositionOf(primaryFace.Surface.PointAt(curveOnPrimaryFace.PointAt(0.333)));
                    double pos2 = curve3d.PositionOf(primaryFace.Surface.PointAt(curveOnPrimaryFace.PointAt(0.666)));
                    forwardOnPrimaryFace = pos1 < pos2;
                    // wenn richtig verbunden, dann muss forwardOnSecondaryFace immer das Gegenteil von forwardOnPrimaryFace sein
                    if (secondaryFace != null)
                    {
                        pos1 = curve3d.PositionOf(secondaryFace.Surface.PointAt(curveOnSecondaryFace.PointAt(0.333)));
                        pos2 = curve3d.PositionOf(secondaryFace.Surface.PointAt(curveOnSecondaryFace.PointAt(0.666)));
                        forwardOnSecondaryFace = pos1 < pos2;
                    }
                }
                else
                {
                    double pos1 = curve3d.PositionOf(primaryFace.Surface.PointAt(curveOnPrimaryFace.StartPoint));
                    double pos2 = curve3d.PositionOf(primaryFace.Surface.PointAt(curveOnPrimaryFace.EndPoint));
                    forwardOnPrimaryFace = pos1 < pos2;
                    // wenn richtig verbunden, dann muss forwardOnSecondaryFace immer das Gegenteil von forwardOnPrimaryFace sein
                    if (secondaryFace != null)
                    {
                        pos1 = curve3d.PositionOf(secondaryFace.Surface.PointAt(curveOnSecondaryFace.StartPoint));
                        pos2 = curve3d.PositionOf(secondaryFace.Surface.PointAt(curveOnSecondaryFace.EndPoint));
                        forwardOnSecondaryFace = pos1 < pos2;
                    }
                }
            }
            oriented = true;
        }
        public static Edge MakeEdge(Face onThisFace, ICurve2D curve)
        {
            Edge res = new Edge();
            res.primaryFace = onThisFace;
            res.curve3d = onThisFace.Surface.Make3dCurve(curve); // Zugriff auf Surface macht z.Z. Area, und das kann 
            res.curveOnPrimaryFace = curve;
            res.forwardOnPrimaryFace = true; // wird das gebraucht?
            res.owner = onThisFace;
            return res;
        }
        internal static Edge MakeEdge(Face onThisFace, ISurface surface, ICurve2D curve)
        {
            Edge res = new Edge();
            res.primaryFace = onThisFace;
            bool isSingular = false;
            if (curve is Line2D)
            {
                if (Math.Abs(curve.StartPoint.y - curve.EndPoint.y) < 1e-8)
                {
                    double[] vs = surface.GetVSingularities();
                    for (int i = 0; i < vs.Length; i++)
                    {
                        if (Math.Abs(curve.StartPoint.y - vs[i]) < 1e-8) isSingular = true;
                    }
                }
                if (Math.Abs(curve.StartPoint.x - curve.EndPoint.x) < 1e-8)
                {
                    double[] us = surface.GetUSingularities();
                    for (int i = 0; i < us.Length; i++)
                    {
                        if (Math.Abs(curve.StartPoint.x - us[i]) < 1e-8) isSingular = true;
                    }
                }
            }
            if (isSingular) res.curve3d = null;
            else
            {
                res.curve3d = surface.Make3dCurve(curve);
                if (res.curve3d != null) (res.curve3d as IGeoObject).Owner = res;
            }
            res.curveOnPrimaryFace = curve;
            res.forwardOnPrimaryFace = true; // wird das gebraucht?
            res.owner = onThisFace;
            return res;
        }
        public override int GetHashCode()
        {
            return hashCode;
        }
        internal Edge()
        {
            edgeKind = EdgeKind.unknown;
            hashCode = hashCodeCounter++; // 
#if DEBUG
            if (hashCode == 1214)
            {
            }
#endif
        }
        /// <summary>
        /// INTERNAL: partial construction, must be completed by SetFace
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="curve3d"></param>
        internal Edge(IGeoObject owner, ICurve curve3d)
            : this()
        {
            this.owner = owner;
            this.curve3d = curve3d;
            if (curve3d != null) (curve3d as IGeoObject).Owner = this;
        }
        internal Edge(ICurve crv, Vertex v1, Vertex v2)
            : this()
        {
            this.owner = null;
            this.curve3d = crv;
            if (curve3d != null) (curve3d as IGeoObject).Owner = this;
            Vertex1 = v1;
            Vertex2 = v2;
            v1.AddEdge(this);
            v2.AddEdge(this);
        }
        /// <summary>
        /// Creates an edge which is the edge of two connected faces.
        /// </summary>
        /// <param name="curve3d">the 3-dimensional curve</param>
        /// <param name="primaryFace">first face</param>
        /// <param name="curveOnPrimaryFace">2-d curve on first face</param>
        /// <param name="secondaryFace">seconsd face</param>
        /// <param name="curveOnSecondaryFace">2-d curve on second face</param>
        public Edge(IGeoObject owner, ICurve curve3d, Face primaryFace, ICurve2D curveOnPrimaryFace, bool forwardOnPrimaryFace, Face secondaryFace, ICurve2D curveOnSecondaryFace, bool forwardOnSecondaryFace)
            : this()
        {
            this.owner = owner;
            this.curve3d = curve3d;
            this.primaryFace = primaryFace;
            this.curveOnPrimaryFace = curveOnPrimaryFace;
            this.forwardOnPrimaryFace = forwardOnPrimaryFace;
            this.secondaryFace = secondaryFace;
            this.curveOnSecondaryFace = curveOnSecondaryFace;
            this.forwardOnSecondaryFace = forwardOnSecondaryFace;
            if (curve3d != null) (curve3d as IGeoObject).Owner = this;
            oriented = true;
        }

        /// <summary>
        /// Creates an edge which is the edge of two connected faces.
        /// </summary>
        /// <param name="curve3d">the 3-dimensional curve</param>
        /// <param name="primaryFace">first face</param>
        /// <param name="curveOnPrimaryFace">2-d curve on first face</param>
        public Edge(IGeoObject owner, ICurve curve3d, Face primaryFace, ICurve2D curveOnPrimaryFace, bool forwardOnPrimaryFace)
            : this()
        {
            this.owner = owner;
            this.curve3d = curve3d;
            this.primaryFace = primaryFace;
            if (curveOnPrimaryFace == null)
            {
                this.curveOnPrimaryFace = primaryFace.Surface.GetProjectedCurve(curve3d, 0.0);
                if (!forwardOnPrimaryFace) this.curveOnPrimaryFace.Reverse();
            }
            else
            {
                this.curveOnPrimaryFace = curveOnPrimaryFace;
            }
            this.forwardOnPrimaryFace = forwardOnPrimaryFace;
            if (curve3d is IGeoObject go)
            {
                go.Owner = this;
                go.Style = Face.EdgeStyle;
            }
            oriented = true;
        }
        public IEnumerable<Face> Faces
        {
            get
            {
                if (primaryFace != null) yield return primaryFace;
                if (secondaryFace != null) yield return secondaryFace;
            }
        }
        /// <summary>
        /// Gets the 3D curve of this edge. Dont modify this curve or the edge will be in an inconsistent state.
        /// </summary>
        public ICurve Curve3D
        {
            get { return curve3d; }
            internal set
            {
                curve3d = value;
                if (curve3d is IGeoObject go) go.Owner = this;
            }
        }
        internal void Modify(ModOp m)
        {   // verändert nur die 3D Kurve, gleichzeitig müssen die Surfaces verändert werden
            // das kann aber nur von höherer Warte aus geschehen
            if (curve3d is IGeoObject)
            {
                (curve3d as IGeoObject).Modify(m);
                v1 = null;
                v2 = null;
            }
        }
#if DEBUG
        public void analyse()
        {
            ICurve cp = primaryFace.Surface.Make3dCurve(curveOnPrimaryFace);
            ICurve cs = secondaryFace.Surface.Make3dCurve(curveOnSecondaryFace);
            double dbg1 = cp.StartPoint | cs.StartPoint;
            double dbg2 = cp.StartPoint | cs.EndPoint;
            double dbg3 = cp.EndPoint | cs.StartPoint;
            double dbg4 = cp.EndPoint | cs.EndPoint;
            double maxerror = 0;
            for (int i = 0; i < 1000; i++)
            {
                GeoPoint p1 = cp.PointAt(i / 1000.0);
                double u = cs.PositionOf(p1);
                GeoPoint p2 = cs.PointAt(u);
                dbg1 = p1 | p2;
                if (dbg1 > maxerror) maxerror = dbg1;
            }
        }
#endif

        internal bool MakeRegular(Set<Face> affectedFaces)
        {
            // Wenn beide Surfaces gesetzt sind, dann soll durch die beiden Vertices und ggf. innere Punkte
            // eine exakte Darstellung der 3d und 2d Kurven gefunden werden.
            if (Curve3D == null || secondaryFace == null) return false;

            if (Curve3D is BSpline)
            {
                List<GeoPoint> pnts = new List<GeoPoint>();
                pnts.Add(Vertex1.Position);
                pnts.Add(curve3d.PointAt(0.5));
                pnts.Add(Vertex2.Position);
                InterpolatedDualSurfaceCurve idsc = new InterpolatedDualSurfaceCurve(primaryFace.Surface, primaryFace.GetUVBounds(), secondaryFace.Surface, secondaryFace.GetUVBounds(), pnts);
                ICurve2D pc2d = primaryFace.Surface.GetProjectedCurve(idsc, 0.0);
                ICurve2D sc2d = secondaryFace.Surface.GetProjectedCurve(idsc, 0.0);
                double dbg1 = (idsc.StartPoint | Curve3D.StartPoint) + (idsc.EndPoint | Curve3D.EndPoint);
                double dbg2 = (pc2d.StartPoint | PrimaryCurve2D.StartPoint) + (pc2d.EndPoint | PrimaryCurve2D.EndPoint);
                double dbg2a = (pc2d.StartPoint | PrimaryCurve2D.EndPoint) + (pc2d.EndPoint | PrimaryCurve2D.StartPoint);
                double dbg3 = (sc2d.StartPoint | SecondaryCurve2D.StartPoint) + (sc2d.EndPoint | SecondaryCurve2D.EndPoint);
                double dbg3a = (sc2d.StartPoint | SecondaryCurve2D.EndPoint) + (sc2d.EndPoint | SecondaryCurve2D.StartPoint);
                if (dbg2 > dbg2a)
                {
                    pc2d.Reverse();
                }
                if (dbg3 > dbg3a)
                {
                    sc2d.Reverse();
                }
                dbg2 = (pc2d.StartPoint | PrimaryCurve2D.StartPoint) + (pc2d.EndPoint | PrimaryCurve2D.EndPoint);
                dbg3 = (sc2d.StartPoint | SecondaryCurve2D.StartPoint) + (sc2d.EndPoint | SecondaryCurve2D.EndPoint);
                this.curve3d = idsc;
                this.curveOnPrimaryFace = pc2d;
                this.curveOnSecondaryFace = sc2d;
                affectedFaces.Add(primaryFace);
                affectedFaces.Add(secondaryFace);
            }

            return false;
        }
        /// <summary>
        /// Return true, wenn 2d and 3d curves have the same start- and endpoints
        /// </summary>
        /// <returns></returns>
        internal bool IsCongruent()
        {
            if (this.curve3d == null) return true; // singulär bleibt singulär
            double prec = Precision.eps * 10;
            if (secondaryFace != null)
            {
                GeoPoint sp = secondaryFace.Surface.PointAt(curveOnSecondaryFace.StartPoint);
                GeoPoint ep = secondaryFace.Surface.PointAt(curveOnSecondaryFace.EndPoint);
                GeoPoint mp = secondaryFace.Surface.PointAt(curveOnSecondaryFace.PointAt(0.5));
                // Orientierung ist hier leider (noch) nicht bekannt
                double d1 = (sp | curve3d.StartPoint) + (ep | curve3d.EndPoint);
                double d2 = (ep | curve3d.StartPoint) + (sp | curve3d.EndPoint);
                //double d3 = mp | curve3d.PointAt(curve3d.PositionOf(mp));
                if (d1 > prec && d2 > prec) return false;
            }
            {
                GeoPoint sp = primaryFace.Surface.PointAt(curveOnPrimaryFace.StartPoint);
                GeoPoint ep = primaryFace.Surface.PointAt(curveOnPrimaryFace.EndPoint);
                double d1 = (sp | curve3d.StartPoint) + (ep | curve3d.EndPoint);
                double d2 = (ep | curve3d.StartPoint) + (sp | curve3d.EndPoint);
                if (d1 > prec && d2 > prec) return false;
            }
            return true;
        }
        public void RecalcCurve3D()
        {   // nur wenn die Kurve nicht in Ordnung ist, wird neu berechnet
            // das ist noch nicht gut! Nach dem Import sollten die Vertices zuerst bestimmt werden (aus 3 Flächen)
            // und dann erst die Kurven, wobei man sich auf die exakten Vertices beziehen kann
#if DEBUG
#endif
        }

        internal void RepairAdjust()
        {
            Vertex1.AdjustCoordinate(Vertex1.Position);
            Vertex2.AdjustCoordinate(Vertex2.Position);
            curve3d.StartPoint = Vertex1.Position;
            curve3d.EndPoint = Vertex2.Position;
            if (curve3d is InterpolatedDualSurfaceCurve)
            {
                (curve3d as InterpolatedDualSurfaceCurve).RecalcSurfacePoints(primaryFace.Area.GetExtent(), secondaryFace.Area.GetExtent());
                if (PrimaryCurve2D is InterpolatedDualSurfaceCurve.ProjectedCurve)
                {
                    bool rev = (PrimaryCurve2D as InterpolatedDualSurfaceCurve.ProjectedCurve).IsReversed;
                    PrimaryCurve2D = (curve3d as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                    if (rev) PrimaryCurve2D.Reverse();
                }
                if (SecondaryCurve2D is InterpolatedDualSurfaceCurve.ProjectedCurve)
                {
                    bool rev = (SecondaryCurve2D as InterpolatedDualSurfaceCurve.ProjectedCurve).IsReversed;
                    SecondaryCurve2D = (curve3d as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                    if (rev) SecondaryCurve2D.Reverse();
                }
            }
        }

        internal bool SameVertices(Edge other)
        {
            return ((other.Vertex1 == Vertex1 && other.Vertex2 == Vertex2) || (other.Vertex1 == Vertex2 && other.Vertex2 == Vertex1));
        }

        private bool RepairEdge(double error)
        {
            return false;
        }

        /// <summary>
        /// Use the vertices in the provided Set. If the endpoints of the curve don't coincide with one of the vertices, create a new one and add it to the set.
        /// </summary>
        /// <param name="toUse"></param>
        internal void UseVertices(Set<Vertex> toUse, double precision = 1e-6)
        {
            if (curve3d != null)
            {
                GeoPoint sp = curve3d.StartPoint;
                GeoPoint ep = curve3d.EndPoint;
                Vertex oldV1 = v1;
                if (v1 != null) v1.RemoveEdge(this);
                v1 = null;
                double bestDist = double.MaxValue;
                foreach (Vertex v0 in toUse)
                {
                    double d = sp | v0.Position;
                    if (d < precision && d < bestDist)
                    {
                        v1 = v0;
                        bestDist = d;
                    }
                }
                if (v1 == null)
                {
                    if (oldV1 == null)
                    {
                        oldV1 = new Vertex(sp);
                    }
                    v1 = oldV1;
                    toUse.Add(v1);
                }
                if (v1 != null) v1.AddEdge(this);
                Vertex oldV2 = v2;
                if (v2 != null) v2.RemoveEdge(this);
                v2 = null;
                bestDist = double.MaxValue;
                foreach (Vertex v0 in toUse)
                {
                    double d = ep | v0.Position;
                    if (d < precision && d < bestDist)
                    {
                        v2 = v0;
                        bestDist = d;
                    }
                }
                if (v2 == null)
                {
                    if (oldV2 == null)
                    {
                        oldV2 = new Vertex(ep);
                    }
                    v2 = oldV2;
                    toUse.Add(v2);
                }
                if (v2 != null) v2.AddEdge(this);
            }
            else
            {   // singulär
                GeoPoint p = primaryFace.Surface.PointAt(curveOnPrimaryFace.EndPoint);
                Vertex oldV1 = v1;
                double bestDist = double.MaxValue;
                foreach (Vertex v0 in toUse)
                {
                    double d = p | v0.Position;
                    if (d < precision && d < bestDist)
                    {
                        v1 = v2 = v0;
                        bestDist = d;
                    }
                }
                if (v1 == null)
                {
                    if (oldV1 == null)
                    {
                        oldV1 = new Vertex(p);
                    }
                    v1 = v2 = oldV1;
                    toUse.Add(v1);
                }
                if (v1 != null) v1.AddEdge(this);
            }

        }
        internal void UseVertices(params Vertex[] toUse)
        {
            if (curve3d != null)
            {
                GeoPoint sp = curve3d.StartPoint;
                GeoPoint ep = curve3d.EndPoint;
                Vertex oldV1 = v1;
                if (v1 != null) v1.RemoveEdge(this);
                v1 = null;
                foreach (Vertex v0 in toUse)
                {
                    if (Precision.IsEqual(sp, v0.Position))
                    {
                        v1 = v0;
                        break;
                    }
                }
                if (v1 == null) v1 = oldV1;
                if (v1 != null)
                {
                    v1.AddEdge(this);
                    if (oldV1 != null && oldV1 != v1) v1.MergeWith(oldV1);
                }
                Vertex oldV2 = v2;
                if (v2 != null) v2.RemoveEdge(this);
                v2 = null;
                foreach (Vertex v0 in toUse)
                {
                    if (Precision.IsEqual(ep, v0.Position))
                    {
                        v2 = v0;
                        break;
                    }
                }
                if (v2 == null) v2 = oldV2;
                if (v2 != null)
                {
                    v2.AddEdge(this);
                    if (oldV2 != null && oldV2 != v2) v2.MergeWith(oldV2);
                }
            }
            else
            {   // singulär
                GeoPoint p = primaryFace.Surface.PointAt(curveOnPrimaryFace.EndPoint);
                Vertex oldV1 = v1;
                foreach (Vertex v0 in toUse)
                {
                    if (Precision.IsEqual(p, v0.Position))
                    {
                        v1 = v2 = v0;
                        break;
                    }
                }
                if (v1 == null)
                {
                    v1 = v2 = oldV1;
                }
                if (v1 != null) v1.AddEdge(this);
            }
        }

        internal void UseVerticesForce(params Vertex[] toUse)
        {
            if (curve3d != null)
            {
                GeoPoint sp = curve3d.StartPoint;
                GeoPoint ep = curve3d.EndPoint;
                if (v1 != null) v1.RemoveEdge(this);
                v1 = null;
                double minDist = double.MaxValue;
                foreach (Vertex v0 in toUse)
                {
                    double d = sp | v0.Position;
                    if (d < minDist)
                    {
                        minDist = d;
                        v1 = v0;
                    }
                }
                v1.AddEdge(this);
                if (v2 != null) v2.RemoveEdge(this);
                v2 = null;
                minDist = double.MaxValue;
                foreach (Vertex v0 in toUse)
                {
                    double d = ep | v0.Position;
                    if (d < minDist)
                    {
                        minDist = d;
                        v2 = v0;
                    }
                }
                v2.AddEdge(this);
            }
            else if (curveOnPrimaryFace != null)
            {   // singulär
                GeoPoint p = primaryFace.Surface.PointAt(curveOnPrimaryFace.EndPoint);
                if (v1 != null) v1.RemoveEdge(this);
                if (v2 != null) v2.RemoveEdge(this);
                v1 = v2 = null;
                double minDist = double.MaxValue;
                foreach (Vertex v0 in toUse)
                {
                    double d = p | v0.Position;
                    if (d < minDist)
                    {
                        minDist = d;
                        v1 = v2 = v0;
                    }
                }
                v1.AddEdge(this);
            }
            else if (toUse.Length > 0)
            {   // incomplete edge definition of a pole, there should only be one vertex in toUse
                v1 = v2 = toUse[0];
                v1.AddEdge(this);
            }
        }

        private bool RepairEdgeFrom2D(GeoPoint[] vtx)
        {
            // die 3d Kurve ist schlecht und wird jetzt aus einer der 2D Kurven berechnet
            bool done = false;
            if (secondaryFace != null)
            {
                if (primaryFace.Surface is NurbsSurface)
                {
                    curve3d = secondaryFace.Surface.Make3dCurve(curveOnSecondaryFace);
                    done = true;
                }
            }
            if (!done)
            {
                curve3d = primaryFace.Surface.Make3dCurve(curveOnPrimaryFace);
            }
            double d1 = (curve3d.StartPoint | vtx[0]) + (curve3d.EndPoint | vtx[1]);
            double d2 = (curve3d.StartPoint | vtx[1]) + (curve3d.EndPoint | vtx[0]);
            if (d2 < d1) curve3d.Reverse();
            return true;
        }

        public void RecalcCurve2D(Face onThisface)
        {   // gefährlich, da Nähte zweimal auf dem selben Face!
            if (this.curve3d == null) return;
            if (onThisface == primaryFace)
            {
                curveOnPrimaryFace = primaryFace.internalSurface.GetProjectedCurve(this.curve3d, Precision.eps);
                if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
            }
            else if (onThisface == secondaryFace)
            {
                curveOnSecondaryFace = secondaryFace.internalSurface.GetProjectedCurve(curve3d, Precision.eps);
                if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
            }
            else throw new ApplicationException("Invalid parameter in RecalcCurve2D");
        }
        internal void RecalcFromSurface(Dictionary<Face, ModOp2D> faceModOps, double maxError)
        {   // wird verwendet, nachdem Flächen durch reguläre Flächen ersetzt wurden
            if (primaryFace != null && secondaryFace != null && curve3d != null)
            {
                List<GeoPoint> points = new List<GeoPoint>();
                // die Liste könnte man besser gestalten
                double[] sp = curve3d.GetSavePositions();
                if (sp.Length == 2)
                {   // mit 2 Punkten geht das weiter unten nicht
                    sp = new double[] { 0, 0.25, 0.5, 0.75, 1.0 };
                }
                for (int i = 0; i < sp.Length; i++)
                {
                    points.Add(curve3d.PointAt(sp[i]));
                }
#if DEBUG
                Polyline dbg = Polyline.Construct();
                dbg.SetPoints(points.ToArray(), false);
#endif
                points.Clear();
                points.Add(curve3d.PointAt(0.0));
                points.Add(curve3d.PointAt(0.5));
                points.Add(curve3d.PointAt(1.0));
                BoundingRect bnds1 = BoundingRect.EmptyBoundingRect;
                primaryFace.internalSurface.GetNaturalBounds(out bnds1.Left, out bnds1.Right, out bnds1.Bottom, out bnds1.Top);
                BoundingRect bnds2 = BoundingRect.EmptyBoundingRect;
                secondaryFace.internalSurface.GetNaturalBounds(out bnds2.Left, out bnds2.Right, out bnds2.Bottom, out bnds2.Top);
                if (bnds1.IsInfinite)
                {
                    ModOp2D mop;
                    if (faceModOps.TryGetValue(primaryFace, out mop))
                    {
                        bnds1 = curveOnPrimaryFace.GetExtent();
                        bnds1.Modify(mop);
                    }
                    else
                    {
                        bnds1.MakeEmpty();
                        for (int i = 0; i < points.Count; i++)
                        {
                            bnds1.MinMax(primaryFace.internalSurface.PositionOf(points[i]));
                        }
                    }
                }
                if (bnds2.IsInfinite)
                {
                    ModOp2D mop;
                    if (faceModOps.TryGetValue(secondaryFace, out mop))
                    {
                        bnds2 = curveOnSecondaryFace.GetExtent();
                        bnds2.Modify(mop);
                    }
                    else
                    {
                        bnds2.MakeEmpty();
                        for (int i = 0; i < points.Count; i++)
                        {
                            bnds2.MinMax(secondaryFace.internalSurface.PositionOf(points[i]));
                        }
                    }
                }
                bool needsOrientation = true;
                if (!(curve3d is Line) && !(curve3d is Ellipse) && primaryFace != secondaryFace)
                {   // Linien und Ellipsen sollen erhalten bleiben, denn das sind meist schon die richtigen
                    // Kurven, wenn auch die unterliegenden Flächen NURBS Flächen waren
                    curve3d = CheckSimpleIntersection(primaryFace.internalSurface, bnds1, secondaryFace.internalSurface, bnds2, out curveOnPrimaryFace, out curveOnSecondaryFace, points, maxError);
                    // Problemfall tangential immer noch nicht richtig gelöst
                }
                else
                {   // 3d ist Linie oder Kreisbogen oder Naht
                    curveOnPrimaryFace = primaryFace.internalSurface.GetProjectedCurve(curve3d, Precision.eps);
                    curveOnSecondaryFace = secondaryFace.internalSurface.GetProjectedCurve(curve3d, Precision.eps);
                    // mit der ModOp wird es zu ungenau
                    //ModOp2D m;
                    //if (faceModOps.TryGetValue(primaryFace, out m))
                    //{
                    //    curveOnPrimaryFace = curveOnPrimaryFace.GetModified(m);
                    //    if (m.Determinant < 0.0)
                    //    {
                    //        curveOnPrimaryFace.Reverse();
                    //    }
                    //}
                    //if (faceModOps.TryGetValue(secondaryFace, out m))
                    //{
                    //    curveOnSecondaryFace = curveOnSecondaryFace.GetModified(m);
                    //    GeoPoint dbg1 = secondaryFace.internalSurface.PointAt(curveOnSecondaryFace.StartPoint);
                    //    GeoPoint dbg2 = secondaryFace.internalSurface.PointAt(curveOnSecondaryFace.EndPoint);
                    //    if (m.Determinant < 0.0)
                    //    {
                    //        curveOnSecondaryFace.Reverse();
                    //    }
                    //}
                    //needsOrientation = false;
                }
                if (curve3d != null)
                {
                    if (needsOrientation)
                    {
                        if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                        if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
                    }
                }
                else
                {
                    curve3d = Surfaces.Intersect(primaryFace.internalSurface, bnds1, secondaryFace.internalSurface, bnds2, points);
                    curveOnPrimaryFace = primaryFace.internalSurface.GetProjectedCurve(curve3d, Precision.eps);
                    if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                    curveOnSecondaryFace = secondaryFace.internalSurface.GetProjectedCurve(curve3d, Precision.eps);
                    if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
                }
                (curve3d as IGeoObject).Owner = this;
            }
            else if (primaryFace != null && curve3d != null)
            {   // die 3D Kurve ist das Ausgangsdatum, keine zweite Fläche vorhanden, also kann man nichts neu berechnen
                RecalcCurve2D(primaryFace);
            }
            else
            {
                // singuläre Kante nur 2D Kurven neu bestimmen
                ModOp2D mop;
                if (faceModOps.TryGetValue(primaryFace, out mop))
                {
                    curveOnPrimaryFace = curveOnPrimaryFace.GetModified(mop);
                }
                else throw new ApplicationException("internal error: Edge.RecalcFromSurface, singular edge");
                if (secondaryFace != null)
                {   // sollte bei einer singulären Kante nicht vorkommen, oder?
                    if (faceModOps.TryGetValue(secondaryFace, out mop))
                    {
                        curveOnSecondaryFace = curveOnSecondaryFace.GetModified(mop);
                    }
                    else throw new ApplicationException("internal error: Edge.RecalcFromSurface, singular edge");
                }
            }
        }

        internal void CopyGeometry(Edge edge)
        {
            Curve3D = edge.Curve3D; // maybe we have to deal here with dualsurfacecurveas
            PrimaryCurve2D = edge.PrimaryCurve2D;
            SecondaryCurve2D = edge.SecondaryCurve2D;
            v1.Position = edge.v1.Position;
            v2.Position = edge.v2.Position;
        }

        private ICurve CheckSimpleIntersection(ISurface surface1, BoundingRect bnds1, ISurface surface2, BoundingRect bnds2, out ICurve2D curve1, out ICurve2D curve2, List<GeoPoint> points, double maxError)
        {
            // nachdem Flächen regularisiert wurden, ist es u.U. schwierig die Kanten zu bestimmen
            // Es kann sein, dass die Kurven tangential zu beiden Flächen sind, dann sind Schnitte schwierig
            if (surface2 is PlaneSurface && !(surface1 is PlaneSurface))
            {   // wenn Ebene, dann als erster Parameter
                return CheckSimpleIntersection(surface2, bnds2, surface1, bnds1, out curve2, out curve1, points, maxError);
            }
            // Ebene mit Ebene sollte einfach den ersten und letzten Punkt liefern
            // Ebene mit Zylinder oder Kegel oder SurfaceOfLinearExtrusion kann Linie oder Ellipse liefern
            // Zylinder und Kegel miteinander kann eine Linie liefern
            // Ebene mit Torus kann einen Kreis liefern
            if (surface1 is PlaneSurface && surface2 is ToroidalSurface)
            {
                GeoVector n1 = (surface1 as PlaneSurface).Normal;
                GeoVector n2 = (surface2 as ToroidalSurface).Axis;
                GeoVector cross = n1.Normalized ^ n2.Normalized;
                double cl = cross.Length;
            }
            curve1 = null;
            curve2 = null;
            if (points.Count > 2)
            {
                GeoPoint2D[] uv1 = new GeoPoint2D[points.Count];
                GeoPoint2D[] uv2 = new GeoPoint2D[points.Count];
                for (int i = 0; i < points.Count; i++)
                {
                    uv1[i] = surface1.PositionOf(points[i]);
                    uv2[i] = surface2.PositionOf(points[i]);
                    GeoVector n1 = surface1.GetNormal(uv1[i]);
                    GeoVector n2 = surface2.GetNormal(uv2[i]);
                    GeoVector z = n1.Normalized ^ n2.Normalized; // gleiche Richtung? wir wollen nur die Tangentialen Schnitte hier abfangen
                    if (z.Length > 0.01) return null;
                    //GeoPoint p1 = surface1.PointAt(uv1[i]);
                    //GeoPoint p2 = surface2.PointAt(uv2[i]);
                    //if ((p1 | p2) > maxError) return null;
                }
                GeoPoint2D loc;
                GeoVector2D dir;
                double lf = Geometry.LineFit(uv1, out loc, out dir);
                if (lf < bnds1.Size * 1e-4)
                {
                    curve1 = new Line2D(Geometry.DropPL(uv1[0], loc, dir), Geometry.DropPL(uv1[uv1.Length - 1], loc, dir));
                }
                else
                {
                    GeoPoint2D cnt;
                    double rad;
                    double cf = Geometry.CircleFit(uv1, out cnt, out rad);
                    if (cf < bnds1.Size * 1e-4)
                    {
                        Arc2D a2d = new Arc2D(cnt, rad, uv1[0], uv1[uv1.Length - 1], true);
                        int mid = uv1.Length / 2;
                        double par = a2d.PositionOf(uv1[mid]);
                        if (par < 0.0 || par > 1.0)
                        {
                            a2d = new Arc2D(cnt, rad, uv1[0], uv1[uv1.Length - 1], false);
                        }
                        par = a2d.PositionOf(uv1[mid]);
                        if (par > 0.0 && par < 1.0) curve1 = a2d;
                    }
                }
                if (curve1 != null)
                {   // also 1. Kurve macht Sinn, 2. Kurve sollte maximal eine Linie sein, da 2. Fläche keine Ebene
                    // oder beide Flächen Ebene
                    lf = Geometry.LineFit(uv2, out loc, out dir);
                    if (lf < bnds2.Size * 1e-4)
                    {
                        curve2 = new Line2D(Geometry.DropPL(uv2[0], loc, dir), Geometry.DropPL(uv2[uv2.Length - 1], loc, dir));
                    }
                }
                if (curve1 != null && curve2 != null)
                {
                    ICurve c13d = surface1.Make3dCurve(curve1);
                    ICurve c23d = surface2.Make3dCurve(curve2);
                    // gute Lösungen werden im folgenden verworfen:
                    //if ((c13d.StartPoint | c23d.StartPoint) > maxError) return null;
                    //if ((c13d.EndPoint | c23d.EndPoint) > maxError) return null;
                    //if ((c13d.PointAt(0.5) | c23d.PointAt(0.5)) > maxError) return null;

                    return c13d;
                }
            }
            return null;
        }

        internal void DisconnectFromVertices()
        {
            if (v1 != null) v1.RemoveEdge(this);
            if (v2 != null) v2.RemoveEdge(this);
        }
        internal void DisconnectFromFace(Face toRemove)
        {   // may not be connected to the provided face
            // keep vertices
            if (toRemove == primaryFace) RemovePrimaryFace();
            else if (toRemove == secondaryFace) RemoveSecondaryFace();
            if (v1 != null) v1.RemovePositionOnFace(toRemove); // better remove, even if still used, because here we cannot decide, whether this is still used, 
            if (v2 != null) v2.RemovePositionOnFace(toRemove); // and PositionOnFace is just a cache, it will be recalculated in case it is still used
            if (primaryFace == null) // secondary is always null then
            {   // this edge is not used anymore, disconnect it from its vertices
                if (v1 != null) v1.RemoveEdge(this);
                if (v2 != null) v2.RemoveEdge(this);
            }
        }
        internal void RemoveFace(Face toRemove)
        {
            if (toRemove == primaryFace) RemovePrimaryFace();
            else if (toRemove == secondaryFace) RemoveSecondaryFace();
            else throw new System.ApplicationException("Edge.RemoveFace called with invalid Face");
            if (v1 != null) v1.RemovePositionOnFace(toRemove); // better remove, even if still used, because here we cannot decide, whether this is still used, 
            if (v2 != null) v2.RemovePositionOnFace(toRemove); // and PositionOnFace is just a cache, it will be recalculated in case it is still used
            if (primaryFace == null)
            {
                // die Kante existiert nicht mehr, also auch aus den Vertexlisten herauslösen
                // Das braucht man in Shell.SplitPeriodicFaces, hat dort allerdings keinen Zugriff auf v1 und v2
                if (v1 != null) v1.RemoveEdge(this);
                if (v2 != null) v2.RemoveEdge(this);
            }
        }

        /// <summary>
        /// Returns the 2-dimensional curve of this edge in the u/v system of the surface of the given face.
        /// If this is not an edge on the given face, null will be returned.
        /// </summary>
        /// <param name="onThisFace">on this face</param>
        /// <returns>the 2-dimensional curve</returns>
        public ICurve2D Curve2D(Face onThisFace)
        {
            if (onThisFace == primaryFace) return curveOnPrimaryFace;
            if (onThisFace == secondaryFace) return curveOnSecondaryFace;
            return null;
        }
        /// <summary>
        /// Returns the 2-dimensional curve of this edge in the u/v system of the surface of the given face.
        /// If this curve has two different representations on the provided Face (which is the case for a seam 
        /// on a periodic surface) then a representation is returned, which is not in the provided array <paramref name="doNotReturn"/>.
        /// </summary>
        /// <param name="onThisFace">Face, on which the 2d curve resides</param>
        /// <param name="doNotReturn">List of curves which should not be returned</param>
        /// <returns>The 2d representation of this edge or null</returns>
        public ICurve2D Curve2D(Face onThisFace, ICurve2D[] doNotReturn)
        {
            if (primaryFace == secondaryFace)
            {   // geschlossene grenzen beim Zylinder (z.B.) bestehen aus einer edge mit zwei verschiedenen Curve2D
                // Objekten. Wir brauchen einmal das eine und dann das andere
                for (int i = 0; i < doNotReturn.Length; ++i)
                {
                    if (doNotReturn[i] == null) break; // die werden von vorne aufgefüllt, bei null ist Schluss
                    if (object.ReferenceEquals(doNotReturn[i], curveOnPrimaryFace))
                    {
                        return curveOnSecondaryFace;
                    }
                }
                return curveOnPrimaryFace;
            }
            else
            {
                if (onThisFace == primaryFace) return curveOnPrimaryFace;
                if (onThisFace == secondaryFace) return curveOnSecondaryFace;
                return null;
            }
        }
        /// <summary>
        /// Returns a new edge whith a clone of the 3d curve, but null references to <see cref="PrimaryFace"/> and <see cref="SecondaryFace"/>.
        /// </summary>
        /// <returns>Copy of this edge</returns>
        public Edge Clone()
        {
            Edge res = new Edge();
            if (curve3d != null)
            {
                res.curve3d = curve3d.Clone();
                (res.curve3d as IGeoObject).Owner = res;
                //res.v1 = v1; // usage of vertices introduced later, make sure it is expected this way
                //res.v2 = v2;
                //if (v1 != null) v1.AddEdge(res);
                //if (v2 != null) v2.AddEdge(res);
            }
            return res;
        }
        public Edge Clone(Dictionary<Vertex, Vertex> clonedVertices)
        {
            Edge res = new Edge();
            if (curve3d != null)
            {
                res.curve3d = curve3d.Clone();
                (res.curve3d as IGeoObject).Owner = res;
            }
            // use Vertex1 instead of v1, because v1 might be null
            if (!clonedVertices.TryGetValue(Vertex1, out Vertex cv1))
            {
                cv1 = new Vertex(Vertex1.Position);
                clonedVertices[Vertex1] = cv1;
            }
            if (!clonedVertices.TryGetValue(Vertex2, out Vertex cv2))
            {
                cv2 = new Vertex(Vertex2.Position);
                clonedVertices[Vertex2] = cv2;
            }
            res.SetVertices(cv1, cv2);
            return res;
        }

        public Edge CloneReplaceFace(Face toReplace, Face replaceWith, bool removeOtherFace)
        {
            Edge res = new Edge();
            if (curve3d != null)
            {
                res.curve3d = curve3d.Clone();
                (res.curve3d as IGeoObject).Owner = res;
            }
            res.v1 = v1;
            res.v2 = v2;
            if (v1 != null) v1.AddEdge(res);
            if (v2 != null) v2.AddEdge(res);
            if (toReplace == primaryFace)
            {
                res.primaryFace = replaceWith;
                res.curveOnPrimaryFace = curveOnPrimaryFace;
                res.forwardOnPrimaryFace = forwardOnPrimaryFace;
                if (secondaryFace != null && !removeOtherFace)
                {
                    res.secondaryFace = secondaryFace;
                    res.curveOnSecondaryFace = curveOnSecondaryFace;
                    res.forwardOnSecondaryFace = forwardOnSecondaryFace;
                }
            }
            else
            {
                if (removeOtherFace)
                {
                    res.primaryFace = replaceWith;
                    res.curveOnPrimaryFace = curveOnSecondaryFace;
                    res.forwardOnPrimaryFace = forwardOnSecondaryFace;
                }
                else
                {
                    res.primaryFace = primaryFace;
                    res.curveOnPrimaryFace = curveOnPrimaryFace;
                    res.forwardOnPrimaryFace = forwardOnPrimaryFace;
                    res.secondaryFace = replaceWith;
                    res.curveOnSecondaryFace = curveOnSecondaryFace;
                    res.forwardOnSecondaryFace = forwardOnSecondaryFace;
                }
            }
            res.owner = res.primaryFace;
#if DEBUG
            if (res.primaryFace != null)
            {
                GeoPoint p1 = res.StartVertex(res.primaryFace).Position;
                if (!Precision.IsEqual(p1, res.primaryFace.Surface.PointAt(res.curveOnPrimaryFace.StartPoint)))
                { }
            }
            if (res.secondaryFace != null)
            {
                GeoPoint p1 = res.StartVertex(res.secondaryFace).Position;
                if (!Precision.IsEqual(p1, res.secondaryFace.Surface.PointAt(res.curveOnSecondaryFace.StartPoint)))
                { }
            }
#endif
            return res;
        }
        public Edge CloneWithVertices()
        {
            Edge res = new Edge();
            if (curve3d != null)
            {
                res.curve3d = curve3d.Clone();
                (res.curve3d as IGeoObject).Owner = res;
                res.v1 = v1;
                res.v2 = v2;
                if (v1 != null) v1.AddEdge(res);
                if (v2 != null) v2.AddEdge(res);
            }
            return res;
        }



        /// <summary>
        /// Checks the orientation of this edge. The curve of this edge (<see cref="Curve3D"/>) has a 
        /// orientation (<see cref="ICurve.StartPoint"/> and <see cref="ICurve.EndPoint"/>). The corresponding 2d curves on the face are also oriented,
        /// so that seen from the outside the 2d curves are oriented counterclockwise. If the 3d curve orientation is the same as the 2d orientation on the
        /// provided face <paramref name="onThisFace"/>, then true is returned, otherwise false is returned.
        /// </summary>
        /// <param name="onThisFace">Face, on which the orientation of the 3d curve is checked.</param>
        /// <returns>true, if forward oriented, false otherwise</returns>
        public bool Forward(Face onThisFace)
        {
            if (!oriented) Orient();
            if (onThisFace == primaryFace) return forwardOnPrimaryFace;
            if (onThisFace == secondaryFace) return forwardOnSecondaryFace;
            return false; // dürfte nicht vorkommen
        }
        public bool SameGeometry(Edge other, double precision)
        {
            // erstmal von offener Kurve ausgehen
            bool reverse;
            return Curves.SameGeometry(this.curve3d, other.curve3d, precision, out reverse);
        }
        public bool Overlaps(Edge other, double precision, out double from, out double to, out double otherFrom, out double otherTo)
        {
            if (this.curve3d != null && other.curve3d != null)
            {
                return Curves.Overlapping(curve3d, other.curve3d, precision, out from, out to, out otherFrom, out otherTo);
            }
            from = to = otherFrom = otherTo = 0.0;
            return false;
        }
        internal void SetPrimary(Face primaryFace, ICurve2D curveOnPrimaryFace, bool forwardOnPrimaryFace)
        {
            this.primaryFace = primaryFace;
            this.curveOnPrimaryFace = curveOnPrimaryFace;
            this.forwardOnPrimaryFace = forwardOnPrimaryFace;
            oriented = true;
            edgeKind = EdgeKind.unknown;
        }
        internal void SetPrimary(Face fc, bool forward)
        {
            primaryFace = fc;
            curveOnPrimaryFace = fc.Surface.GetProjectedCurve(curve3d, Precision.eps);
            if (!forward) curveOnPrimaryFace.Reverse();
            forwardOnPrimaryFace = forward;
            oriented = true;
            edgeKind = EdgeKind.unknown;
        }
        internal void SetSecondary(Face fc, bool forward)
        {
            secondaryFace = fc;
            curveOnSecondaryFace = fc.Surface.GetProjectedCurve(curve3d, Precision.eps);
            if (!forward) curveOnSecondaryFace.Reverse();
            forwardOnSecondaryFace = forward;
            oriented = true;
            edgeKind = EdgeKind.unknown;
        }
        internal void UpdateInterpolatedDualSurfaceCurve()
        {
            if (curve3d is InterpolatedDualSurfaceCurve)
            {
                InterpolatedDualSurfaceCurve dsc = (curve3d as InterpolatedDualSurfaceCurve);
                if (secondaryFace != null)
                {
                    // InterpolatedDualSurfaceCurve is converted to BSpline when new edges are created with overlappingFaces.
                    // These edges may be reconverted to InterpolatedDualSurfaceCurve, if the two involved surfaces are not tangential
                    GeoPoint2D uv1s = Vertex1.GetPositionOnFace(PrimaryFace);
                    GeoPoint2D uv1e = Vertex2.GetPositionOnFace(PrimaryFace);
                    GeoPoint2D uv2s = Vertex1.GetPositionOnFace(SecondaryFace);
                    GeoPoint2D uv2e = Vertex2.GetPositionOnFace(SecondaryFace);
                    GeoVector n1s = PrimaryFace.Surface.GetNormal(uv1s);
                    GeoVector n1e = PrimaryFace.Surface.GetNormal(uv1e);
                    GeoVector n2s = SecondaryFace.Surface.GetNormal(uv2s);
                    GeoVector n2e = SecondaryFace.Surface.GetNormal(uv2e);
                    BoundingRect bounds1 = primaryFace.Area.GetExtent();
                    BoundingRect bounds2 = secondaryFace.Area.GetExtent();
                    if ((new Angle(n1s, n2s)).Radian < 0.01 && (new Angle(n1e, n2e)).Radian < 0.01) // the condition was "||", but we get along with curves that are tangential at one end
                    {   // surfaces are tangential, this edge.curve3d cannot remain a InterpolatedDualSurfaceCurve
                        curve3d = (curve3d as InterpolatedDualSurfaceCurve).ToBSpline(0.0);
                        curveOnPrimaryFace = PrimaryFace.Surface.GetProjectedCurve(curve3d, 0.0);
                        SurfaceHelper.AdjustPeriodic(PrimaryFace.Surface, bounds1, curveOnPrimaryFace);
                        if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                        curveOnSecondaryFace = SecondaryFace.Surface.GetProjectedCurve(curve3d, 0.0);
                        SurfaceHelper.AdjustPeriodic(SecondaryFace.Surface, bounds2, curveOnSecondaryFace);
                        if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
                    }
                    else
                    {
                        dsc = new InterpolatedDualSurfaceCurve(primaryFace.Surface, bounds1, secondaryFace.Surface, bounds2, new List<GeoPoint>(dsc.BasePoints));
                        curveOnPrimaryFace = dsc.CurveOnSurface1;
                        if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                        curveOnSecondaryFace = dsc.CurveOnSurface2;
                        if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
                        this.curve3d = dsc;
#if DEBUG
                        dsc.CheckSurfaceParameters();
#endif
                        return;


                        //BoundingRect bounds1 = curveOnPrimaryFace.GetExtent();
                        //BoundingRect bounds2 = curveOnSecondaryFace.GetExtent();
                        // sicher ist hier, dass StartVertex und EndVertex stimmen
                        // also Vertex1, Vertex2 und forwardOnPrimaryFace, forwardOnSecondaryFace sind richtig gesetzt
                        if (dsc.Surface1 == primaryFace.internalSurface && dsc.Surface2 == secondaryFace.internalSurface)
                        {   // nichts zu tun
                            if (curveOnPrimaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve)
                            {
                                (curveOnPrimaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).SetCurve3d(dsc);
                                (curveOnPrimaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).IsOnSurface1 = true;
                            }
                            else
                            {
                                dsc.RecalcSurfacePoints(primaryFace.Area.GetExtent(), secondaryFace.Area.GetExtent());
                            }
                            if (curveOnSecondaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve)
                            {
                                (curveOnSecondaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).SetCurve3d(dsc);
                                (curveOnSecondaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).IsOnSurface1 = false;
                            }
                            else
                            {
                                dsc.RecalcSurfacePoints(primaryFace.Area.GetExtent(), secondaryFace.Area.GetExtent());
                            }
                            curveOnPrimaryFace = dsc.CurveOnSurface1;
                            if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                            curveOnSecondaryFace = dsc.CurveOnSurface2;
                            if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
                            dsc.AdjustPeriodic(bounds1, bounds2);
#if DEBUG
                            dsc.CheckSurfaceParameters();
#endif
                            return; // die 2d Kurven stimmen hoffentlich auch. Wenn es Fälle gibt, wo nicht, dann muss das untere noch ausgeführt werden
                        }
                        else if (dsc.Surface2 == primaryFace.internalSurface && dsc.Surface1 == secondaryFace.internalSurface)
                        {   // surfaces vertauschen
                            dsc.SetSurfaces(primaryFace.internalSurface, secondaryFace.internalSurface, true);
                            dsc.RecalcSurfacePoints(bounds1, bounds2);
                            dsc.AdjustPeriodic(bounds1, bounds2);
                            curveOnPrimaryFace = dsc.CurveOnSurface1;
                            if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                            curveOnSecondaryFace = dsc.CurveOnSurface2;
                            if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
#if DEBUG
                            dsc.CheckSurfaceParameters();
#endif
                            return; // die 2d Kurven stimmen hoffentlich auch. Wenn es Fälle gibt, wo nicht, dann muss das untere noch ausgeführt werden
                        }
                        else
                        {   // es sind vermutlich clones der surfaces gegeben.
                            // am einfachsten zu unterscheiden, wenn verschiedene Typen
                            if (secondaryFace.internalSurface.GetType() != primaryFace.internalSurface.GetType())
                            {
                                if (dsc.Surface1.GetType() == primaryFace.internalSurface.GetType() && dsc.Surface2.GetType() == secondaryFace.internalSurface.GetType())
                                {
                                    dsc.SetSurfaces(primaryFace.internalSurface, secondaryFace.internalSurface, false);
                                }
                                else
                                {   // surfaces vertauschen
                                    dsc.SetSurfaces(primaryFace.internalSurface, secondaryFace.internalSurface, true);
                                }
                            }
                            else
                            {
                                ModOp2D fts;
                                if (dsc.Surface1.SameGeometry(primaryFace.Area.GetExtent(), primaryFace.internalSurface, primaryFace.Area.GetExtent(), Precision.eps, out fts))
                                {
                                    dsc.SetSurfaces(primaryFace.internalSurface, secondaryFace.internalSurface, false);
                                }
                                else
                                {   // surfaces vertauschen
                                    dsc.SetSurfaces(primaryFace.internalSurface, secondaryFace.internalSurface, true);
                                }
                            }
                        }
                        if ((dsc.StartPoint | Vertex1.Position) + (dsc.EndPoint | Vertex2.Position) > (dsc.StartPoint | Vertex2.Position) + (dsc.EndPoint | Vertex1.Position))
                        {
                            dsc.Reverse();
                        }
                        curveOnPrimaryFace = dsc.CurveOnSurface1;
                        if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                        curveOnSecondaryFace = dsc.CurveOnSurface2;
                        if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
                        dsc.RecalcSurfacePoints(primaryFace.Area.GetExtent(), secondaryFace.Area.GetExtent());
                        dsc.AdjustPeriodic(bounds1, bounds2);
#if DEBUG
                        dsc.CheckSurfaceParameters();
#endif
                    }
                }
                else
                {
                    // zu BSpline machen
                    curve3d = dsc.ToBSpline(Precision.eps);
                    if (curveOnPrimaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve)
                    {
                        curveOnPrimaryFace = (curveOnPrimaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).ToBSpline(Precision.eps);
                    }
                }
            }
            else if (secondaryFace != null && (curveOnPrimaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve || curveOnSecondaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve))
            {
                if (Curve3D is BSpline && (Curve3D as BSpline).ThroughPoints3dExist) // das war mal eine InterpolatedDualSurfaceCurve
                {
                    GeoPoint2D uv1s = Vertex1.GetPositionOnFace(PrimaryFace);
                    GeoPoint2D uv1e = Vertex2.GetPositionOnFace(PrimaryFace);
                    GeoPoint2D uv2s = Vertex1.GetPositionOnFace(SecondaryFace);
                    GeoPoint2D uv2e = Vertex2.GetPositionOnFace(SecondaryFace);
                    GeoVector n1s = PrimaryFace.Surface.GetNormal(uv1s);
                    GeoVector n1e = PrimaryFace.Surface.GetNormal(uv1e);
                    GeoVector n2s = SecondaryFace.Surface.GetNormal(uv2s);
                    GeoVector n2e = SecondaryFace.Surface.GetNormal(uv2e);
                    if ((new Angle(n1s, n2s)).Radian < 0.1 || (new Angle(n1e, n2e)).Radian < 0.1)
                    {   // surfaces are tangential, this edge.curve3d cannot remain a InterpolatedDualSurfaceCurve
                        curveOnPrimaryFace = PrimaryFace.Surface.GetProjectedCurve(curve3d, 0.0);
                        if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                        curveOnSecondaryFace = SecondaryFace.Surface.GetProjectedCurve(curve3d, 0.0);
                        if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
                    }
                    else
                    {
                        BoundingRect bounds1 = curveOnPrimaryFace.GetExtent();
                        BoundingRect bounds2 = curveOnSecondaryFace.GetExtent();
                        InterpolatedDualSurfaceCurve dsc = new InterpolatedDualSurfaceCurve(primaryFace.Surface, bounds1,
                            secondaryFace.Surface, bounds2, new List<GeoPoint>((Curve3D as BSpline).ThroughPoint));
                        curveOnPrimaryFace = dsc.CurveOnSurface1;
                        if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                        curveOnSecondaryFace = dsc.CurveOnSurface2;
                        if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
                        dsc.RecalcSurfacePoints(primaryFace.Area.GetExtent(), secondaryFace.Area.GetExtent());
                        dsc.AdjustPeriodic(primaryFace.Area.GetExtent(), secondaryFace.Area.GetExtent());
#if DEBUG
                        dsc.CheckSurfaceParameters();
#endif
                    }
                }
            }
            else
            {
                if (curveOnPrimaryFace is ProjectedCurve)
                {
                    curveOnPrimaryFace = new ProjectedCurve(curve3d, primaryFace.Surface, forwardOnPrimaryFace, primaryFace.Domain);
                }
                if (curveOnSecondaryFace is ProjectedCurve)
                {
                    curveOnSecondaryFace = new ProjectedCurve(curve3d, secondaryFace.Surface, forwardOnSecondaryFace, secondaryFace.Domain);
                }
            }
        }

        internal void PaintTo3D(IPaintTo3D paintTo3D)
        {
            IGeoObjectImpl go = Curve3D as IGeoObjectImpl;
            // there was the idea to paint edges with different colors, depending on their function: there are edges connecting faces on same surfaces,
            // edges connecting tangential surfaces and edges between non tangential faces, i.e. sharp bends. It doesn't look nice to show them with differen colors
            // so we now use only black
            if (go != null)
            {
                paintTo3D.SetColor(Color.Black);
                go.PaintTo3D(paintTo3D);
            }
            //if (edgeKind == EdgeKind.unknown)
            //{
            //    edgeKind = EdgeKind.sharp;
            //    if (secondaryFace == null) edgeKind = EdgeKind.sharp;
            //    else if (primaryFace.Surface.SameGeometry(primaryFace.Domain, secondaryFace.Surface, secondaryFace.Domain, Precision.eps, out _)) edgeKind = EdgeKind.sameSurface;
            //    else if ((primaryFace.Surface is PlaneSurface && (secondaryFace.Surface is CylindricalSurface || secondaryFace.Surface is ConicalSurface || secondaryFace.Surface is ToroidalSurface)) ||
            //        (secondaryFace.Surface is PlaneSurface && (primaryFace.Surface is CylindricalSurface || primaryFace.Surface is ConicalSurface || primaryFace.Surface is ToroidalSurface)))
            //    {
            //        // we only need to check a single position
            //        GeoVector n1 = primaryFace.Surface.GetNormal(Vertex1.GetPositionOnFace(primaryFace));
            //        GeoVector n2 = secondaryFace.Surface.GetNormal(Vertex1.GetPositionOnFace(secondaryFace));
            //        if (Precision.SameNotOppositeDirection(n1, n2)) edgeKind = EdgeKind.tangential;
            //    }
            //    else
            //    {
            //        GeoVector n1 = primaryFace.Surface.GetNormal(Vertex1.GetPositionOnFace(primaryFace));
            //        GeoVector n2 = secondaryFace.Surface.GetNormal(Vertex1.GetPositionOnFace(secondaryFace));
            //        if (Precision.SameNotOppositeDirection(n1, n2))
            //        {
            //            n1 = primaryFace.Surface.GetNormal(Vertex2.GetPositionOnFace(primaryFace));
            //            n2 = secondaryFace.Surface.GetNormal(Vertex2.GetPositionOnFace(secondaryFace));
            //            if (Precision.SameNotOppositeDirection(n1, n2))
            //            {
            //                // now there could be cases, where we would have to check more points, and I don't know, how to tell, so we check only the middle point
            //                GeoPoint m = curve3d.PointAt(0.5);
            //                n1 = primaryFace.Surface.GetNormal(primaryFace.Surface.PositionOf(m));
            //                n2 = secondaryFace.Surface.GetNormal(secondaryFace.Surface.PositionOf(m));
            //                if (Precision.SameNotOppositeDirection(n1, n2)) edgeKind = EdgeKind.tangential;
            //            }
            //        }
            //    }
            //    if (go!=null)
            //    {
            //        (go as IColorDef).ColorDef = null;
            //        (go as ILinePattern).LinePattern = null;
            //        (go as ILineWidth).LineWidth = LineWidth.ThinLine;
            //    }
            //}
            //if (edgeKind == EdgeKind.sameSurface || Curve3D == null) return;
            //if (go != null)
            //{
            //    if (edgeKind == EdgeKind.tangential) paintTo3D.SetColor(Color.FromArgb(128, 192, 192, 192));
            //    if (edgeKind == EdgeKind.sharp) paintTo3D.SetColor(Color.Black);
            //    go.PaintTo3D(paintTo3D);
            //}
        }

        internal void SetSecondary(Face secondaryFace, ICurve2D curveOnSecondaryFace, bool forwardOnSecondaryFace)
        {
#if DEBUG
            if (hashCode == 1469 || hashCode == 1468)
            {
            }
#endif
            this.secondaryFace = secondaryFace;
            this.curveOnSecondaryFace = curveOnSecondaryFace;
            this.forwardOnSecondaryFace = forwardOnSecondaryFace;
        }
        public Face PrimaryFace
        {
            get
            {
                return primaryFace;
            }
        }
        public Face SecondaryFace
        {
            get
            {
                return secondaryFace;
            }
            internal set
            {
                secondaryFace = value;
            }
        }
        internal IGeoObject Owner
        {
            get
            {
                return owner;
            }
            set
            {   // beim Einlesen notwendig
                owner = value;
            }
        }
        internal Edge IsPartOf
        {
            get
            {
                return isPartOf;
            }
            set
            {
                isPartOf = value;
            }
        }
        internal ICurve2D PrimaryCurve2D
        {
            get
            {
                return curveOnPrimaryFace;
            }
            set
            {
                curveOnPrimaryFace = value;
            }
        }
        internal ICurve2D SecondaryCurve2D
        {   // wird benötigt, wenn primaryFace==secondaryFace, denn curveOnSecondaryFace wird ggf verändert
            get
            {
                return curveOnSecondaryFace;
            }
            set
            {
                curveOnSecondaryFace = value;
            }
        }
        internal bool IsPeriodicEdge
        {
            get
            {
                return (primaryFace == secondaryFace);
                // das kann nur vorkommen, wenn die Fläche periodic ist, oder?
            }
        }
        /// <summary>
        /// Stellt fest, ob diese Edge teil einer anderen Edge ist. Das das über geometrische Eigenschaften läuft
        /// ist blöd, denn man könnte es besser beim Erzeugen (Face.Split) bestimmen, ist aber dort sehr umständlich
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        internal bool CheckSplit(Edge original)
        {
            // this hat nur primaryFace, nicht secondary, da es ja beim splitten entstanden ist
            bool primary = false;
            if (original.primaryFace.Surface == this.primaryFace.Surface)
            {
                primary = true;
            }
            else if (original.secondaryFace != null && original.secondaryFace.Surface == this.primaryFace.Surface)
            {
                primary = false;
            }
            else
            {
                return false; // wann kommt man hierhin?
            }
            {   // haben schonmal das selbe Oberflächenobjekt
                ICurve2D orgcurve2d;
                if (primary) orgcurve2d = original.curveOnPrimaryFace;
                else orgcurve2d = original.curveOnSecondaryFace;
                // jetzt nur noch orgcurve2d und curveOnPrimaryFace vergleichen
                if (orgcurve2d.MinDistance(curveOnPrimaryFace.StartPoint) < Precision.eps &&
                    orgcurve2d.MinDistance(curveOnPrimaryFace.EndPoint) < Precision.eps &&
                    orgcurve2d.MinDistance(curveOnPrimaryFace.PointAt(0.5)) < Precision.eps)
                {
                    if (orgcurve2d.IsParameterOnCurve(orgcurve2d.PositionOf(curveOnPrimaryFace.StartPoint)))
                    {
                        isPartOf = original;
                        if (original.curve3d != null && curve3d != null)
                        {
                            startAtOriginal = original.curve3d.PositionOf(curve3d.StartPoint);
                            endAtOriginal = original.curve3d.PositionOf(curve3d.EndPoint);
                            if (original.curve3d.IsClosed && endAtOriginal < Precision.eps)
                            {
                                double mpos = original.curve3d.PositionOf(curve3d.PointAt(0.5));
                                if (mpos > startAtOriginal) endAtOriginal = 1.0;
                            }
                        }
                        else
                        {
                            startAtOriginal = 0.0;
                            endAtOriginal = 1.0;
                        }
                        return true;
                    }
                }
            }
            return false;
        }
        internal double StartAtOriginal
        {
            get
            {
                return startAtOriginal;
            }
            set
            {
                startAtOriginal = value;
            }
        }
        internal double EndAtOriginal
        {
            get
            {
                return endAtOriginal;
            }
            set
            {
                endAtOriginal = value;
            }
        }
        internal bool IsSame(Edge original)
        {   // wird nur mit Tangentenkurven aufegrufen. Hoffentlich ist das mit dem Mittelpunkt BSpline kompatibel
            // Kein Problem: die 3D Kurve wird ja nicht umgedreht
            if (curve3d == null || original.curve3d == null) return false;
            if (Precision.IsEqual(curve3d.PointAt(0.5), original.curve3d.PointAt(0.5)))
            {
                GeoPoint sp1 = curve3d.StartPoint;
                GeoPoint sp2 = original.curve3d.StartPoint;
                GeoPoint ep1 = curve3d.EndPoint;
                GeoPoint ep2 = original.curve3d.EndPoint;
                return (Precision.IsEqual(sp1, sp2) && Precision.IsEqual(ep1, ep2)) ||
                    (Precision.IsEqual(sp1, ep2) && Precision.IsEqual(ep1, sp2));
            }
            return false;
        }
        internal bool IsOrientedConnection
        {
            get
            {
                if (secondaryFace == null) return true;
                if (!oriented) Orient();
                return forwardOnPrimaryFace != forwardOnSecondaryFace;
            }
        }
        internal static void CheckSplittedEdges(Edge[] originals, Edge[] splitted)
        {
            for (int i = 0; i < splitted.Length; i++)
            {
                for (int j = 0; j < originals.Length; j++)
                {
                    if (splitted[i].CheckSplit(originals[j]))
                        break;
                }
            }
        }

        #region IJsonSerialize Members
        public void GetObjectData(IJsonWriteData data)
        {
            owner = primaryFace;
            if (curve3d != null)
            {
                if (curve3d is IGeoObject go) go.Owner = this;
                data.AddProperty("Curve3d", curve3d);
            }
            if (primaryFace != null) data.AddProperty("PrimaryFace", primaryFace);
            if (curveOnPrimaryFace != null) data.AddProperty("CurveOnPrimaryFace", curveOnPrimaryFace);
            data.AddProperty("ForwardOnPrimaryFace", forwardOnPrimaryFace);
            if (secondaryFace != null)
            {
                data.AddProperty("SecondaryFace", secondaryFace);
                data.AddProperty("CurveOnSecondaryFace", curveOnSecondaryFace);
                data.AddProperty("ForwardOnSecondaryFace", forwardOnSecondaryFace);
            }
            if (v1 != null) data.AddProperty("Vertex1", v1);
            if (v2 != null) data.AddProperty("Vertex2", v2);
        }
        public void SetObjectData(IJsonReadData data)
        {
            curve3d = data.GetPropertyOrDefault<ICurve>("Curve3d");
            primaryFace = data.GetPropertyOrDefault<Face>("PrimaryFace");
            curveOnPrimaryFace = data.GetPropertyOrDefault<ICurve2D>("CurveOnPrimaryFace");
            forwardOnPrimaryFace = data.GetPropertyOrDefault<bool>("ForwardOnPrimaryFace");
            secondaryFace = data.GetPropertyOrDefault<Face>("SecondaryFace");
            curveOnSecondaryFace = data.GetPropertyOrDefault<ICurve2D>("CurveOnSecondaryFace");
            forwardOnSecondaryFace = data.GetPropertyOrDefault<bool>("ForwardOnSecondaryFace");
            v1 = data.GetPropertyOrDefault<Vertex>("Vertex1");
            v2 = data.GetPropertyOrDefault<Vertex>("Vertex2");
            data.RegisterForSerializationDoneCallback(this);
        }
        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            if (curve3d != null) (curve3d as IGeoObject).Owner = this;
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Implements the constructor for deserialization.
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        protected Edge(SerializationInfo info, StreamingContext context)
            : this()
        {   // besser mit iterator, sonst zu viele exceptions, die so lange dauern
            // alles andere ist ohnehin null
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                switch (e.Name)
                {
                    case "Curve3d":
                        curve3d = e.Value as ICurve;
                        break;
                    case "PrimaryFace":
                        primaryFace = e.Value as Face;
                        break;
                    case "CurveOnPrimaryFace":
                        curveOnPrimaryFace = e.Value as ICurve2D;
                        break;
                    case "ForwardOnPrimaryFace":
                        forwardOnPrimaryFace = InfoReader.ReadBool(e.Value);
                        break;
                    case "SecondaryFace":
                        secondaryFace = e.Value as Face;
                        break;
                    case "CurveOnSecondaryFace":
                        curveOnSecondaryFace = e.Value as ICurve2D;
                        break;
                    case "ForwardOnSecondaryFace":
                        forwardOnSecondaryFace = InfoReader.ReadBool(e.Value);
                        break;
                    case "Vertex1":
                        v1 = e.Value as Vertex;
                        break;
                    case "Vertex2":
                        v2 = e.Value as Vertex;
                        break;
                }
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {   // ICurve and ICurve2D must be serializable
            // Vertices are beeing saved now
            if (curve3d != null)
            {
                info.AddValue("Curve3d", curve3d, curve3d.GetType());
            }
            if (primaryFace != null) info.AddValue("PrimaryFace", primaryFace, primaryFace.GetType());
            if (curveOnPrimaryFace != null) info.AddValue("CurveOnPrimaryFace", curveOnPrimaryFace, curveOnPrimaryFace.GetType());
            info.AddValue("ForwardOnPrimaryFace", forwardOnPrimaryFace, typeof(bool));
            if (secondaryFace != null)
            {
                info.AddValue("SecondaryFace", secondaryFace, secondaryFace.GetType());
                info.AddValue("CurveOnSecondaryFace", curveOnSecondaryFace, curveOnSecondaryFace.GetType());
                info.AddValue("ForwardOnSecondaryFace", forwardOnSecondaryFace, typeof(bool));
            }
            if (v1 != null) info.AddValue("Vertex1", v1);
            if (v2 != null) info.AddValue("Vertex2", v2);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            // RecalcCurve3D(); warum, das wurde doch soeben eingelesen
            // außerdem kann man u.U. mit den Flächen und curveOnPrimaryFace etc. noch nicht rechnen!
            if (curve3d != null) (curve3d as IGeoObject).Owner = this;
        }
        #endregion
        internal void CopyPrimary(Edge edge, Face face)
        {
            this.primaryFace = face; // surface muss identisch sein
            this.curveOnPrimaryFace = edge.curveOnPrimaryFace;
            this.forwardOnPrimaryFace = edge.forwardOnPrimaryFace;
        }
        internal void CopySecondary(Edge edge, Face face)
        {
            // in edge ist nur primary gesetzt
            this.secondaryFace = face; // surface muss identisch sein
            this.curveOnSecondaryFace = edge.curveOnPrimaryFace;
            this.forwardOnSecondaryFace = edge.forwardOnPrimaryFace;
        }
        #region IGeoObjectOwner Members

        void IGeoObjectOwner.Remove(IGeoObject toRemove)
        {

        }

        void IGeoObjectOwner.Add(IGeoObject toAdd)
        {

        }

        #endregion
        internal bool IsSingular()
        {
            return curve3d == null;
        }
        internal bool IsSeam()
        {
            return primaryFace == secondaryFace;
        }
        internal bool ConnectsSameSurfaces()
        {
            if (secondaryFace == null) return false;
            return primaryFace.SameSurface(secondaryFace);
        }
        internal GeoPoint2D[] GetTriangulationBasis(Face face, double precision, ICurve2D c2d)
        {   // liefert die 2D Polygone für diese Kante in der richtigen Richtung
            // gemäß der geforderten Genauigkeit. Der letzte Punkt wird nicht geliefert

            // Optimieren: nacher werden die 3D Punkte wieder berechnet, obwohl sie hier schon vorliegen
            // die 2D Kurve ist sicher richtig orientiert, die 3D Kurve nicht.

            // hier wird ein Cache benötigt, es wird meistens zwimal aufgerufen
            // beim Cache bitte das lock nicht vergessen

            if (curve3d == null) return new GeoPoint2D[] { c2d.StartPoint }; // singulär
            // gibt es eine gleichmäßige Aufteilung, die zum Ziel führt.
            // z.B. Linie/Kreis auf Ebene
            if (curve3d is Line)
            {   // es wird nur der Startpunkt benötigt
                return new GeoPoint2D[] { c2d.StartPoint };
            }
            if (curve3d is Polyline && c2d is Polyline2D)
            {
                Polyline2D pl = c2d as Polyline2D;
                if (pl.IsClosed && (pl.GetVertex(0) != pl.GetVertex(pl.VertexCount - 1)))
                {
                    return pl.Vertex;
                }
                else
                {
                    GeoPoint2D[] res = new GeoPoint2D[pl.VertexCount - 1];
                    Array.Copy(pl.Vertex, res, pl.VertexCount - 1);
                    return res;
                }
            }
            if (curve3d is Polyline)
            {
                // we assume that polylines as an edge refer to some kond of approximation that needs not to be refined
                Polyline pl3d = (curve3d as Polyline);
                bool fw = Forward(face);
                GeoPoint2D[] res = new GeoPoint2D[pl3d.Vertices.Length - 1];
                for (int i = 0; i < pl3d.Vertices.Length - 1; i++)
                {
                    if (fw) res[i] = face.Surface.PositionOf(pl3d.Vertices[i]);
                    else res[i] = face.Surface.PositionOf(pl3d.Vertices[pl3d.Vertices.Length - 1 - i]);
                    SurfaceHelper.AdjustPeriodic(face.Surface, face.Domain, ref res[i]);
                }
                return res;

            }
            if (curve3d is Ellipse)
            {
                Ellipse e = curve3d as Ellipse;
                if (e.IsCircle)
                // if (false) // warum war das ausgeklammert ???
                {   // ein Kreis im 3D sollte immer im 2D gleichmäßig mit dem Winkel laufen. Wenn nicht
                    // hier ausgrenzen
                    double alpha;
                    if (e.Radius > precision)
                    {
                        alpha = 2 * Math.Acos((e.Radius - precision) / e.Radius);
                        //double dr = (e.Radius - precision); // das ist das selbe
                        //alpha = 2 * Math.Atan2(Math.Sqrt(e.Radius * e.Radius - dr * dr), dr);
                    }
                    else
                    {
                        alpha = Math.PI / 2.0; // Viertel
                    }
                    int n = Math.Max(2, (int)(Math.Abs(e.SweepParameter) / alpha + 1));
                    GeoPoint2D[] res = new GeoPoint2D[n];
                    for (int i = 0; i < n; ++i)
                    {
                        res[i] = c2d.PointAt((double)i / (double)n);
                    }
                    return res;
                }
            }
            if (c2d is Line2D)
            {   // eingeführt für NURBS Flächen von degree1 und geraden Begrenzungen
                // dort brauchen wir die echten Polygone als Rand
                Line2D l2d = (c2d as Line2D);
                ICurve c3d = null;
                if (l2d.Length > Precision.eps)
                {
                    try
                    {
                        if (Math.Abs(l2d.StartPoint.y - l2d.EndPoint.y) < Precision.eps)
                        {   // horizontale Linie
                            // Problem, wenn die Surface periodic ist und die Kurve über die periodische Naht geht
                            if (!CrossingPeriodicSeam(l2d.StartPoint.x, l2d.EndPoint.x, face, true))
                            {
                                if (l2d.StartPoint.x < l2d.EndPoint.x)
                                {
                                    c3d = face.Surface.FixedV(l2d.StartPoint.y, l2d.StartPoint.x, l2d.EndPoint.x);
                                }
                                else
                                {
                                    c3d = face.Surface.FixedV(l2d.StartPoint.y, l2d.EndPoint.x, l2d.StartPoint.x);
                                    if (c3d != null) c3d.Reverse();
                                }
                            }
                            else
                            {
                            }
                        }
                        else if (Math.Abs(l2d.StartPoint.x - l2d.EndPoint.x) < Precision.eps)
                        {
                            // Problem, wenn die Surface periodic ist und die Kurve über die periodische Naht geht
                            if (!CrossingPeriodicSeam(l2d.StartPoint.y, l2d.EndPoint.y, face, false))
                            {
                                if (l2d.StartPoint.y < l2d.EndPoint.y)
                                {
                                    c3d = face.Surface.FixedU(l2d.StartPoint.x, l2d.StartPoint.y, l2d.EndPoint.y);
                                }
                                else
                                {
                                    c3d = face.Surface.FixedU(l2d.StartPoint.x, l2d.EndPoint.y, l2d.StartPoint.y);
                                    if (c3d != null) c3d.Reverse();
                                }
                            }
                        }
                    }
                    catch
                    {
                        c3d = null;
                    }
                }
                // Folgendes auskommentiert, da Approximate nicht gut funktioniert, wenn die 2d und 3d Kurven nicht zusammenpassen
                //if (c3d != null)
                //{
                //    double[] sp = c3d.GetSavePositions();
                //    SortedDictionary<double, GeoPoint2D> parpoint = new SortedDictionary<double, GeoPoint2D>();
                //    for (int i = 0; i < sp.Length; i++)
                //    {
                //        parpoint[sp[i]] = l2d.PointAt(sp[i]);
                //    }
                //    for (int i = 0; i < sp.Length - 1; i++)
                //    {
                //        Approximate(parpoint, c2d, face.Surface, sp[i], sp[i + 1], precision);
                //    }
                //    GeoPoint2D[] res = new GeoPoint2D[parpoint.Count - 1];
                //    int k = 0;
                //    foreach (GeoPoint2D p in parpoint.Values)
                //    {
                //        if (k < res.Length) res[k] = p;
                //        ++k;
                //    }
                //    return res;
                //}
            }
            // allgemeine Lösung
            // ... hier noch eine spezielle Lösung, wenn curve3d eine PolyLine ist, dann müssen die Abschnitte der Polyline 
            // erzeugt werden. Vielleicht wie ein paar Zeilen weiter oben
            // folgendes ausgeklammert, da bei dem rek. Aufruf curve3d unverändert bleibt und diese im Zweifelsfall verwendet wird
            //if (c2d is Polyline2D)
            //{   // gibt es auch Path2D?

            //    Polyline2D pl2d = c2d as Polyline2D;
            //    List<GeoPoint2D> res = new List<GeoPoint2D>();
            //    for (int i = 0; i < pl2d.VertexCount - 1; i++)
            //    {
            //        res.AddRange(GetTriangulationBasis(face, precision, new Line2D(pl2d.GetVertex(i), pl2d.GetVertex(i + 1))));
            //    }
            //    return res.ToArray();
            //}

            {
                ICurve app3d = curve3d.Approximate(true, precision);
                if (curve3d is Path || curve3d is Polyline)
                {   // in diesem Fall ist die 3d Kurve aus Stücken zusammengesetzt, und es ist wichtig im 2d den Ecken möglichst nahe zu kommen
                    GeoPoint[] vert3d = null;
                    if (app3d is Path)
                    {   // hier wird erstmal einfach gleichverteilt
                        vert3d = (app3d as Path).Vertices;
                    }
                    else if (app3d is Polyline)
                    {
                        vert3d = (app3d as Polyline).Vertices;
                    }
                    if (vert3d != null)
                    {
                        double[] pars = new double[vert3d.Length];
                        for (int i = 0; i < vert3d.Length; i++)
                        {
                            pars[i] = c2d.PositionOf(face.Surface.PositionOf(vert3d[i]));
                        }
                        Array.Sort(pars);   // hier noch überprüfen, ob nicht zu ungleichmäßig oder identische Punkte
                        GeoPoint2D[] res = new GeoPoint2D[pars.Length - 1];
                        for (int i = 0; i < res.Length; i++)
                        {
                            res[i] = c2d.PointAt(pars[i]);
                        }
                        return res;
                    }
                }
                if (app3d is Path)
                {   // hier wird erstmal einfach gleichverteilt
                    int n = (app3d as Path).CurveCount;
                    GeoPoint2D[] res = new GeoPoint2D[n];
                    for (int i = 0; i < res.Length; i++)
                    {
                        //if (i == 0)
                        //{
                        //    if (Forward(face)) res[i] = face.Surface.PositionOf(curve3d.StartPoint);
                        //    else res[i] = face.Surface.PositionOf(curve3d.EndPoint);
                        //}
                        //else
                        res[i] = c2d.PointAt(i / (double)n);
                    }
                    return res;
                }
                else if (app3d is Polyline)
                {
                    int n = (app3d as Polyline).PointCount;
                    if ((n & 1) == 1) ++n;
                    GeoPoint2D[] res = new GeoPoint2D[n];
                    for (int i = 0; i < res.Length; i++)
                    {
                        res[i] = c2d.PointAt(i / (double)n);
                        //GeoPoint2D dbg = face.Surface.PositionOf((app3d as Polyline).Vertices[i]);
                    }
                    return res;
                }
                else if (app3d is Line)
                {
                    return new GeoPoint2D[] { c2d.StartPoint };
                }
                else // sollte nie drankommen
                {
                    if (c2d.Length < Precision.eps)
                        return new GeoPoint2D[] { c2d.StartPoint };
                    SortedDictionary<double, GeoPoint2D> parpoint = new SortedDictionary<double, GeoPoint2D>();
                    parpoint[0.0] = c2d.StartPoint;
                    parpoint[0.5] = c2d.PointAt(0.5);
                    parpoint[1.0] = c2d.EndPoint;
                    // zur Erhöhung der Geschwindigkeit noch ein paralleles 3D array
                    Approximate(parpoint, c2d, face.Surface, 0.0, 0.5, precision);
                    Approximate(parpoint, c2d, face.Surface, 0.5, 1.0, precision);
                    GeoPoint2D[] res = new GeoPoint2D[parpoint.Count - 1];
                    int i = 0;
                    foreach (GeoPoint2D p in parpoint.Values)
                    {
                        if (i < res.Length) res[i] = p;
                        ++i;
                    }
                    return res;
                }
            }
        }

        private bool CrossingPeriodicSeam(double p1, double p2, Face face, bool uperiod)
        {
            if (uperiod)
            {
                return (face.Surface.IsUPeriodic && Math.Floor(p1 / face.Surface.UPeriod) != Math.Floor(p2 / face.Surface.UPeriod));
            }
            else
            {
                return (face.Surface.IsVPeriodic && Math.Floor(p1 / face.Surface.VPeriod) != Math.Floor(p2 / face.Surface.VPeriod));
            }
        }

        private double findMaxDist(double spar, double epar, ICurve2D curve2d, ISurface surface, out double dist)
        {
            double par0 = spar;
            double par1 = (2 * spar + epar) / 3.0;
            double par2 = (spar + 2 * epar) / 3.0;
            double par3 = epar;
            GeoPoint p0 = surface.PointAt(curve2d.PointAt(par0));
            GeoPoint p1 = surface.PointAt(curve2d.PointAt(par1));
            GeoPoint p2 = surface.PointAt(curve2d.PointAt(par2));
            GeoPoint p3 = surface.PointAt(curve2d.PointAt(par3));
            double d0 = curve3d.DistanceTo(p0);
            double d1 = curve3d.DistanceTo(p1);
            double d2 = curve3d.DistanceTo(p2);
            double d3 = curve3d.DistanceTo(p3);
            for (int i = 0; i < 10; i++) // 10 Iterationen
            {
                if (d0 + d1 < d2 + d3)
                {   // bei d0 entfernen
                    p0 = p1; // p3 bleibt
                    par0 = par1;
                    d0 = d1;
                }
                else
                {
                    p3 = p2;
                    par3 = par2;
                    d3 = d2;
                }
                par1 = (2 * par0 + par3) / 3.0;
                par2 = (par0 + 2 * par3) / 3.0;
                p1 = surface.PointAt(curve2d.PointAt(par1));
                p2 = surface.PointAt(curve2d.PointAt(par2));
                d1 = curve3d.DistanceTo(p1);
                d2 = curve3d.DistanceTo(p2);
            }
            dist = (d1 + d2) / 2.0;
            return (par1 + par2) / 2.0;
        }

        private void Approximate(SortedDictionary<double, GeoPoint2D> parpoint, ICurve2D curve2d, ISurface surface, double spar, double epar, double precision)
        {
            if ((epar - spar) < 1e-3)
            {
                List<GeoPoint2D> dbgp = new List<GeoPoint2D>(parpoint.Values);
                Polyline2D pl2d = new Polyline2D(dbgp.ToArray());

                return;
            }
            GeoPoint sp = surface.PointAt(parpoint[spar]);
            GeoPoint ep = surface.PointAt(parpoint[epar]);
            double ipar = (spar + epar) / 2.0;
            // GeoPoint2D mp2d = curve2d.PointAt(ipar);
            GeoPoint2D mp2d;
            double dist = surface.MaxDist(parpoint[spar], parpoint[epar], out mp2d);
            double distCurve = 0.0;
            ipar = curve2d.PositionOf(mp2d);
            //ipar = findMaxDist(spar, epar, curve2d, surface, out dist);
            if (ipar <= spar || ipar >= epar)
            {   // sollte eigentlich nicht vorkommen, dann aber halt in der Mitte teilen
                ipar = (spar + epar) / 2.0;
            }
            GeoPoint mp = surface.PointAt(mp2d); // mp2d ist hier noch der Zwischenpunkt auf der Linie
            mp2d = curve2d.PointAt(ipar);
            // es ist nicht nur der Abstand zur Fläche, sondern auch zur Randkurve gefragt!
#if DEBUG
            if (curve3d != null)
            {
                ICurve dbgcv = curve3d.Approximate(true, precision);
                GeoPoint[] pnts = new GeoPoint[100];
                for (int i = 0; i < 100; i++)
                {
                    pnts[i] = surface.PointAt(curve2d.PointAt(i / 100.0));
                }
                try
                {
                    Polyline pdbg = Polyline.Construct();
                    pdbg.SetPoints(pnts, false);
                }
                catch (PolylineException) { };
            }
#endif
            if (curve3d != null) dist = Math.Max(dist, curve3d.DistanceTo(mp));
            // dist ist ja schon der Abstand zur Randkurve
            if (dist > precision || (epar - spar) < 0.05) // max 20 Punkte
            {
                parpoint[ipar] = mp2d;
                Approximate(parpoint, curve2d, surface, spar, ipar, precision);
                Approximate(parpoint, curve2d, surface, ipar, epar, precision);
            }
            //else
            //{
            //    GeoPoint2D mlin = new GeoPoint2D(parpoint[spar], parpoint[epar]);
            //    mp = new GeoPoint(sp, ep); // Mittelpunkt in 3D
            //    if ((mp | surface.PointAt(mlin)) > precision) // mit diesem Test arbeitet die Triangulierung
            //    {   // und deshalb muss er hier auch ausgeführt werden, denn die Kante als Außenkante
            //        // wird in der Triangulierung nie aufgeteilt, aber die beiden anderen Dreicksseiten können
            //        // sonst u.U. niemals die Bedingung einhalten
            //        parpoint[ipar] = mp2d;
            //        Approximate(parpoint, curve2d, surface, spar, ipar, precision);
            //        Approximate(parpoint, curve2d, surface, ipar, epar, precision);
            //    }
            //}
        }

        internal void SetFace(Face face, bool forward)
        {
            if (primaryFace == null)
            {
                primaryFace = face;
                this.owner = primaryFace; // oder?
                curveOnPrimaryFace = face.internalSurface.GetProjectedCurve(curve3d, 0.0);
                if (!forward) curveOnPrimaryFace.Reverse();
                forwardOnPrimaryFace = forward;
                oriented = true;
            }
            else if (secondaryFace == null)
            {
                secondaryFace = face;
                curveOnSecondaryFace = face.internalSurface.GetProjectedCurve(curve3d, 0.0);
                if (!forward) curveOnSecondaryFace.Reverse();
                forwardOnSecondaryFace = forward;
                oriented = true;
            }
            else throw new System.ApplicationException("Edge.SetFace called with already two faces set");
        }
        internal void SetFace(Face face, ICurve2D curve2D, bool forward)
        {
            if (primaryFace == null)
            {
                primaryFace = face;
                this.owner = primaryFace; // oder?
                curveOnPrimaryFace = curve2D;
                if (!forward) curveOnPrimaryFace.Reverse();
                forwardOnPrimaryFace = forward;
                oriented = true;
            }
            else if (secondaryFace == null)
            {
                secondaryFace = face;
                curveOnSecondaryFace = curve2D;
                if (!forward) curveOnSecondaryFace.Reverse();
                forwardOnSecondaryFace = forward;
                oriented = true;
            }
            else throw new System.ApplicationException("Edge.SetFace called with already two faces set");
        }
        internal double FindPeriodicPosition(ICurve2D c2d, GeoPoint2D p, ISurface surface)
        {   // das ist PositionOf unter Berücksichtigung der Periodizität
            if (surface.IsUPeriodic)
            {
                BoundingRect ext = c2d.GetExtent();
                double cx = (ext.Left + ext.Right) / 2.0;
                if (p.x > cx)
                {
                    double dx = Math.Abs(p.x - cx);
                    while (Math.Abs((p.x - surface.UPeriod) - cx) < dx)
                    {
                        dx = Math.Abs((p.x - surface.UPeriod) - cx);
                        p.x -= surface.UPeriod;
                    }
                }
                else
                {
                    double dx = Math.Abs(p.x - cx);
                    while (Math.Abs((p.x + surface.UPeriod) - cx) < dx)
                    {
                        dx = Math.Abs((p.x + surface.UPeriod) - cx);
                        p.x += surface.UPeriod;
                    }
                }
            }
            if (surface.IsVPeriodic)
            {
                BoundingRect ext = c2d.GetExtent();
                double cy = (ext.Bottom + ext.Top) / 2.0;
                if (p.y > cy)
                {
                    double dy = Math.Abs(p.y - cy);
                    while (Math.Abs((p.y - surface.VPeriod) - cy) < dy)
                    {
                        dy = Math.Abs((p.y - surface.VPeriod) - cy);
                        p.y -= surface.VPeriod;
                    }
                }
                else
                {
                    double dy = Math.Abs(p.y - cy);
                    while (Math.Abs((p.y + surface.VPeriod) - cy) < dy)
                    {
                        dy = Math.Abs((p.y + surface.VPeriod) - cy);
                        p.y += surface.VPeriod;
                    }
                }
            }
            //if (surface.IsUPeriodic && surface.IsVPeriodic)
            //{
            //    BoundingRect ext = c2d.GetExtent();
            //    int imin = (int)Math.Floor((p.x - ext.Left) / surface.UPeriod);
            //    int imax = (int)Math.Floor((p.x - ext.Right) / surface.UPeriod);
            //    int jmin = (int)Math.Floor((p.y - ext.Bottom) / surface.VPeriod);
            //    int jmax = (int)Math.Floor((p.x - ext.Top) / surface.VPeriod);
            //    for (int i = imin; i < imax; ++i)
            //    {
            //        for (int j = jmin; j < jmax; ++j)
            //        {
            //            double res = c2d.PositionOf(new GeoPoint2D(p.x + i * surface.UPeriod, p.y + j * surface.VPeriod));
            //            if (res >= 0.0 && res <= 1.0) return res;
            //        }
            //    }
            //}
            //else if (surface.IsUPeriodic)
            //{
            //    BoundingRect ext = c2d.GetExtent();
            //    // if (p.x < ext.Left) p.x += surface.UPeriod;
            //    int imin = (int)Math.Floor((ext.Left-p.x) / surface.UPeriod);
            //    int imax = (int)Math.Floor((ext.Right - p.x) / surface.UPeriod);
            //    for (int i = imin; i <= imax; ++i)
            //    {
            //        double res = c2d.PositionOf(new GeoPoint2D(p.x + i * surface.UPeriod, p.y));
            //        if (res >= 0.0 && res <= 1.0) return res;
            //    }
            //}
            //else if (surface.IsVPeriodic)
            //{
            //    BoundingRect ext = c2d.GetExtent();
            //    int jmin = (int)Math.Floor((p.y - ext.Bottom) / surface.VPeriod);
            //    int jmax = (int)Math.Floor((p.x - ext.Top) / surface.VPeriod);
            //    for (int j = jmin; j < jmax; ++j)
            //    {
            //        double res = c2d.PositionOf(new GeoPoint2D(p.x, p.y + j * surface.VPeriod));
            //        if (res >= 0.0 && res <= 1.0) return res;
            //    }
            //}
            return c2d.PositionOf(p); // in p ist die Periode ggf. angeglichen
        }
        internal bool IsDualSurfaceEdge
        {
            get
            {
                if (curve3d is InterpolatedDualSurfaceCurve)
                {
                    InterpolatedDualSurfaceCurve idsc = curve3d as InterpolatedDualSurfaceCurve;
                    if (curveOnPrimaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve && curveOnSecondaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve)
                    {
                        if ((curveOnPrimaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).Curve3D == idsc && (curveOnSecondaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).Curve3D == idsc) return true;
                    }
                }
                return false;
            }
        }
        internal Edge[] Split(double position)
        {
            SortedList<double, Vertex> sortedVertices = new SortedList<double, Vertex>();
            Vertex v = new Vertex(curve3d.PointAt(position));
            sortedVertices[position] = v;
            return Split(sortedVertices, Precision.eps);
        }

        internal Edge[] Split(SortedList<double, Vertex> sortedVertices, double precision)
        {
            if (!oriented) Orient();
            List<Edge> res = new List<Edge>(sortedVertices.Count + 1);
            if (IsDualSurfaceEdge)
            {   // dieser Fall ist speziell, da die 2d Kurven und die 3d Kurve voneinander abhängig sind und dies auch im Ergebnis so wiedergespiegelt sein muss
                double startpos = 0.0;
                double endpos = sortedVertices.Keys[0];
                Vertex startVertex = v1;
                Vertex endVertex = sortedVertices.Values[0];
                for (int i = 0; i < sortedVertices.Count + 1; ++i)
                {
                    Edge splittedEdge = new Edge();
                    splittedEdge.oriented = true; // denn diese ist schon orientiert
                    splittedEdge.owner = owner;
                    splittedEdge.primaryFace = primaryFace;
                    splittedEdge.secondaryFace = secondaryFace;
                    splittedEdge.forwardOnPrimaryFace = forwardOnPrimaryFace;
                    splittedEdge.forwardOnSecondaryFace = forwardOnSecondaryFace;
                    splittedEdge.curve3d = (curve3d as InterpolatedDualSurfaceCurve).CloneTrimmed(startpos, endpos, curveOnPrimaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve, curveOnSecondaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve, out splittedEdge.curveOnPrimaryFace, out splittedEdge.curveOnSecondaryFace);
#if DEBUG
                    (splittedEdge.curve3d as InterpolatedDualSurfaceCurve).CheckSurfaceParameters();
#endif
                    (splittedEdge.curve3d as IGeoObject).Owner = splittedEdge;
                    if ((startVertex.Position | endVertex.Position) > precision)
                    {   // es kommt vor, dass die Vertices gleich sind, wenn genau am Anfang bzw. Ende geschnitten wurde
                        splittedEdge.v1 = startVertex;
                        splittedEdge.v2 = endVertex;
                        startVertex.AddEdge(splittedEdge);
                        endVertex.AddEdge(splittedEdge);
                        res.Add(splittedEdge);
                    }

                    // für die nächste Runde:
                    startVertex = endVertex;
                    startpos = endpos;
                    if (i < sortedVertices.Count - 1)
                    {
                        endpos = sortedVertices.Keys[i + 1];
                        endVertex = sortedVertices.Values[i + 1];
                    }
                    else
                    {
                        endpos = 1.0;
                        endVertex = v2;
                    }
                }
            }
            else
            {
                double startpos = 0.0;
                double endpos = sortedVertices.Keys[0];
                double s2d1;
                if (forwardOnPrimaryFace) s2d1 = 0.0;
                else s2d1 = 1.0;
                // es ist bei folgenden Aufrufen von FindPeriodicPosition schade, dass die u.U. bereits berechnete 2D Position nicht verwendet wird
                // denn dann wäre man ja bereits sicher mit dem Punkt in der Periode. Man bräuchte dazu "Vertex.Has2DPosiiton(Face)"
                double e2d1 = FindPeriodicPosition(curveOnPrimaryFace, sortedVertices.Values[0].GetPositionOnFace(primaryFace), primaryFace.Surface);
                double s2d2 = 0.0;
                double e2d2 = 0.0;
                // der vertex braucht für jedes betroffene face einen u/v Wert. der wird sonst so soft berechnet
                if (secondaryFace != null)
                {
                    if (forwardOnSecondaryFace) s2d2 = 0.0;
                    else s2d2 = 1.0;
                    e2d2 = FindPeriodicPosition(curveOnSecondaryFace, sortedVertices.Values[0].GetPositionOnFace(secondaryFace), secondaryFace.Surface);
                    GeoPoint2D dbg = secondaryFace.Surface.PositionOf(sortedVertices.Values[0].Position);
                }
                if (v1 == null || v2 == null) MakeVertices();
                Vertex startVertex = v1;
                Vertex endVertex = sortedVertices.Values[0];
                for (int i = 0; i < sortedVertices.Count + 1; ++i)
                {
                    Edge splittedEdge = new Edge();
                    splittedEdge.oriented = true; // denn diese ist schon orientiert
                    splittedEdge.owner = owner;
                    splittedEdge.primaryFace = primaryFace;
                    splittedEdge.secondaryFace = secondaryFace;
                    splittedEdge.forwardOnPrimaryFace = forwardOnPrimaryFace;
                    splittedEdge.forwardOnSecondaryFace = forwardOnSecondaryFace;
                    splittedEdge.curve3d = curve3d.Clone();
                    splittedEdge.curve3d.Trim(startpos, endpos);
                    if (curveOnPrimaryFace is ProjectedCurve)
                    {
                        splittedEdge.curveOnPrimaryFace = primaryFace.Surface.GetProjectedCurve(splittedEdge.curve3d, 0.0);
                        if (!splittedEdge.forwardOnPrimaryFace) splittedEdge.curveOnPrimaryFace.Reverse();
                    }
                    else
                    {
                        if (s2d1 < e2d1) splittedEdge.curveOnPrimaryFace = curveOnPrimaryFace.Trim(s2d1, e2d1);
                        else splittedEdge.curveOnPrimaryFace = curveOnPrimaryFace.Trim(e2d1, s2d1);
                    }
                    s2d1 = e2d1;
                    if (i < sortedVertices.Count - 1)
                        e2d1 = FindPeriodicPosition(curveOnPrimaryFace, sortedVertices.Values[i + 1].GetPositionOnFace(primaryFace), primaryFace.Surface);
                    else
                    {   // Position of macht probleme bei zyklischen, deshalb so:
                        if (forwardOnPrimaryFace) e2d1 = 1.0;
                        else e2d1 = 0.0;
                    }
                    if (secondaryFace != null)
                    {
                        if (curveOnSecondaryFace is ProjectedCurve)
                        {
                            splittedEdge.curveOnSecondaryFace = secondaryFace.Surface.GetProjectedCurve(splittedEdge.curve3d, 0.0);
                            if (!splittedEdge.forwardOnSecondaryFace) splittedEdge.curveOnSecondaryFace.Reverse();
                        }
                        else
                        {
                            if (s2d2 < e2d2) splittedEdge.curveOnSecondaryFace = curveOnSecondaryFace.Trim(s2d2, e2d2);
                            else splittedEdge.curveOnSecondaryFace = curveOnSecondaryFace.Trim(e2d2, s2d2);
                        }
                        s2d2 = e2d2;
                        if (i < sortedVertices.Count - 1) e2d2 = FindPeriodicPosition(curveOnSecondaryFace, sortedVertices.Values[i + 1].GetPositionOnFace(secondaryFace), secondaryFace.Surface);
                        else
                        {   // Position of macht probleme bei zyklischen, deshalb so:
                            if (forwardOnSecondaryFace) e2d2 = 1.0;
                            else e2d2 = 0.0;
                        }
                    }
                    if ((startVertex.Position | endVertex.Position) > precision)
                    {   // es kommt vor, dass die Vertices gleich sind, wenn genau am Anfang bzw. Ende geschnitten wurde
                        splittedEdge.v1 = startVertex;
                        splittedEdge.v2 = endVertex;
                        startVertex.AddEdge(splittedEdge);
                        endVertex.AddEdge(splittedEdge);
                        res.Add(splittedEdge);
                    }
                    // für die nächste Runde:
                    startVertex = endVertex;
                    startpos = endpos;
                    if (i < sortedVertices.Count - 1)
                    {
                        endpos = sortedVertices.Keys[i + 1];
                        endVertex = sortedVertices.Values[i + 1];
                    }
                    else
                    {
                        endpos = 1.0;
                        endVertex = v2;
                    }
                }
            }
            Edge[] resa = res.ToArray();
            if (primaryFace != null) primaryFace.ReplaceEdge(this, resa);
            if (secondaryFace != null) secondaryFace.ReplaceEdge(this, resa);
            if (v1 != null) v1.RemoveEdge(this); // diese beiden braucht man in Shell.ReplaceFace.
            // diese Kante soll ja rausgelöst und ersetzt werden
            if (v2 != null) v2.RemoveEdge(this);
#if DEBUG
            DebuggerContainer dc;
            //if (primaryFace.GetHashCode()==2098 || primaryFace.GetHashCode() == 2198)
            //{
            //    dc = primaryFace.DebugEdges2D;
            //}
            //if (secondaryFace.GetHashCode() == 2098 || secondaryFace.GetHashCode() == 2198)
            //{
            //    dc = secondaryFace.DebugEdges2D;
            //}
#endif
            return resa;
        }
        internal void ReverseCurve3D()
        {
            if (curve3d != null)
            {
                curve3d.Reverse();
                forwardOnPrimaryFace = !forwardOnPrimaryFace;
                forwardOnSecondaryFace = !forwardOnSecondaryFace;
                Vertex tmp = v1;
                v1 = v2;
                v2 = tmp;
                if (curve3d is InterpolatedDualSurfaceCurve)
                {   // not sure, whether this is the right place to do this. But it is definitely needed
                    // it should always be valid to call ReverseCurve3D without changing the consitency of the faces
                    curveOnPrimaryFace = (curve3d as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                    if (!forwardOnPrimaryFace) curveOnPrimaryFace.Reverse();
                    if (curveOnSecondaryFace != null)
                    {
                        curveOnSecondaryFace = (curve3d as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                        if (!forwardOnSecondaryFace) curveOnSecondaryFace.Reverse();
                    }
                    //if (curveOnPrimaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve)
                    //{
                    //    (curveOnPrimaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).Reverse();
                    //}
                    //if (curveOnSecondaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve)
                    //{
                    //    (curveOnSecondaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).Reverse();
                    //}
                }
                if (curveOnPrimaryFace is ProjectedCurve pcp) pcp.IsReverse = !forwardOnPrimaryFace;
                if (curveOnSecondaryFace is ProjectedCurve pcs) pcs.IsReverse = !forwardOnSecondaryFace;
                PrimaryFace?.InvalidateArea();
                SecondaryFace?.InvalidateArea();
            }
        }
        internal void Reverse(Face onThisFace)
        {
            if (!oriented) Orient();
            if (primaryFace == onThisFace)
            {
                forwardOnPrimaryFace = !forwardOnPrimaryFace;
                curveOnPrimaryFace.Reverse();
            }
            else if (secondaryFace == onThisFace)
            {
                forwardOnSecondaryFace = !forwardOnSecondaryFace;
                curveOnSecondaryFace.Reverse();
            }
        }
        internal void ReverseOrientation(Face onThisFace)
        {   // wird von Face.ReverseOrientation verwendet
            if (primaryFace == onThisFace)
            {
                forwardOnPrimaryFace = !forwardOnPrimaryFace;
            }
            else if (secondaryFace == onThisFace)
            {
                forwardOnSecondaryFace = !forwardOnSecondaryFace;
            }
        }
        /// <summary>
        /// Replace the face "toReplace" by "replaceWith". If "toReplace" is neither primary nor secondary face of this edge, "replaceWith" will be used as 
        /// primary or secondary face, whichever is free (==null)
        /// </summary>
        /// <param name="toReplace"></param>
        /// <param name="replaceWith"></param>
        internal void ReplaceOrAddFace(Face toReplace, Face replaceWith)
        {

            if (toReplace == primaryFace) primaryFace = replaceWith;
            else if (toReplace == secondaryFace) secondaryFace = replaceWith;
            else if (primaryFace == null) primaryFace = replaceWith;
            else if (secondaryFace == null) secondaryFace = replaceWith;
            else throw new ApplicationException("ReplaceOrAddFace, invalid parameter");
            owner = replaceWith;
        }
        internal void ReplaceFace(Face from, Face to)
        {
            if (from == primaryFace) primaryFace = to;
            else if (from == secondaryFace) secondaryFace = to;
            else throw new ApplicationException("ReplaceFace, invalid parameter");
        }
        internal bool ReplaceFace(Face from, Face to, ModOp2D m)
        {
            if (from == primaryFace)
            {
                GeoPoint2D expmp2d = m * PrimaryCurve2D.PointAt(0.5); // the expected middle point used to find correct period for periodic surfaces
                primaryFace = to;
                if (Curve3D is InterpolatedDualSurfaceCurve)
                {
                    UpdateInterpolatedDualSurfaceCurve();
                }
                else if (m.IsNull)
                {
                    PrimaryCurve2D = from.Surface.GetProjectedCurve(Curve3D, 0.0);
                    if (!forwardOnPrimaryFace) PrimaryCurve2D.Reverse();
                    this.owner = primaryFace;
                }
                else if (PrimaryCurve2D is ProjectedCurve)
                {
                    PrimaryCurve2D = primaryFace.Surface.GetProjectedCurve(curve3d, 0.0);
                    if (!forwardOnPrimaryFace) PrimaryCurve2D.Reverse();
                    this.owner = primaryFace;
                }
                else
                {
                    PrimaryCurve2D = PrimaryCurve2D.GetModified(m);
                    this.owner = primaryFace;
                    if (m.Determinant < 0) forwardOnPrimaryFace = !forwardOnPrimaryFace;
                }
                if (primaryFace.Surface.IsUPeriodic)
                {
                    GeoPoint2D mp2d = PrimaryCurve2D.PointAt(0.5);
                    while (mp2d.x > expmp2d.x + primaryFace.Surface.UPeriod * 0.5)
                    {
                        PrimaryCurve2D.Move(-primaryFace.Surface.UPeriod, 0);
                        mp2d = PrimaryCurve2D.PointAt(0.5);
                    }
                    while (mp2d.x < expmp2d.x - primaryFace.Surface.UPeriod * 0.5)
                    {
                        PrimaryCurve2D.Move(primaryFace.Surface.UPeriod, 0);
                        mp2d = PrimaryCurve2D.PointAt(0.5);
                    }
                }
                if (primaryFace.Surface.IsVPeriodic)
                {
                    GeoPoint2D mp2d = PrimaryCurve2D.PointAt(0.5);
                    while (mp2d.y > expmp2d.y + primaryFace.Surface.VPeriod * 0.5)
                    {
                        PrimaryCurve2D.Move(0, -primaryFace.Surface.VPeriod);
                        mp2d = PrimaryCurve2D.PointAt(0.5);
                    }
                    while (mp2d.y < expmp2d.y - primaryFace.Surface.VPeriod * 0.5)
                    {
                        PrimaryCurve2D.Move(0, primaryFace.Surface.VPeriod);
                        mp2d = PrimaryCurve2D.PointAt(0.5);
                    }
                }
                return true;
            }
            else if (from == secondaryFace)
            {
                GeoPoint2D expmp2d = m * SecondaryCurve2D.PointAt(0.5); // the expected middle point used to find correct period for periodic surfaces
                secondaryFace = to;
                if (Curve3D is InterpolatedDualSurfaceCurve)
                {
                    UpdateInterpolatedDualSurfaceCurve();
                }
                else if (m.IsNull)
                {
                    SecondaryCurve2D = from.Surface.GetProjectedCurve(Curve3D, 0.0);
                    if (!forwardOnSecondaryFace) SecondaryCurve2D.Reverse();
                }
                else if (PrimaryCurve2D is ProjectedCurve)
                {
                    SecondaryCurve2D = secondaryFace.Surface.GetProjectedCurve(curve3d, 0.0);
                    if (!forwardOnSecondaryFace) SecondaryCurve2D.Reverse();
                }
                else
                {
                    SecondaryCurve2D = SecondaryCurve2D.GetModified(m);
                    if (m.Determinant < 0) forwardOnSecondaryFace = !forwardOnSecondaryFace;
                }
                if (secondaryFace.Surface.IsUPeriodic)
                {
                    GeoPoint2D mp2d = SecondaryCurve2D.PointAt(0.5);
                    while (mp2d.x > expmp2d.x + secondaryFace.Surface.UPeriod * 0.5)
                    {
                        SecondaryCurve2D.Move(-secondaryFace.Surface.UPeriod, 0);
                        mp2d = SecondaryCurve2D.PointAt(0.5);
                    }
                    while (mp2d.x < expmp2d.x - secondaryFace.Surface.UPeriod * 0.5)
                    {
                        SecondaryCurve2D.Move(secondaryFace.Surface.UPeriod, 0);
                        mp2d = SecondaryCurve2D.PointAt(0.5);
                    }
                }
                if (secondaryFace.Surface.IsVPeriodic)
                {
                    GeoPoint2D mp2d = SecondaryCurve2D.PointAt(0.5);
                    while (mp2d.y > expmp2d.y + secondaryFace.Surface.VPeriod * 0.5)
                    {
                        SecondaryCurve2D.Move(0, -secondaryFace.Surface.VPeriod);
                        mp2d = SecondaryCurve2D.PointAt(0.5);
                    }
                    while (mp2d.y < expmp2d.y - secondaryFace.Surface.VPeriod * 0.5)
                    {
                        SecondaryCurve2D.Move(0, secondaryFace.Surface.VPeriod);
                        mp2d = SecondaryCurve2D.PointAt(0.5);
                    }
                }
                return true;
            }
            else return false;
        }
        internal ICurve2D ModifyCurve2D(Face face, ICurve2D[] alreadyModified, ModOp2D m)
        {   // Achtung, nicht die selbe Kurve 2 mal modifizieren

            if (primaryFace == secondaryFace && alreadyModified != null)
            {   // geschlossene grenzen beim Zylinder (z.B.) bestehen aus einer edge mit zwei verschiedenen Curve2D
                // Objekten. Wir brauchen einmal das eine und dann das andere
                bool primary = true;
                for (int i = 0; i < alreadyModified.Length; ++i)
                {
                    if (alreadyModified[i] == null) break; // die werden von vorne aufgefüllt, bei null ist Schluss
                    if (object.ReferenceEquals(alreadyModified[i], curveOnPrimaryFace))
                    {
                        primary = false;
                        break;
                    }
                }
                oriented = false;
                if (primary)
                {
                    curveOnPrimaryFace = curveOnPrimaryFace.GetModified(m);
                    return curveOnPrimaryFace;
                }
                else
                {
                    curveOnSecondaryFace = curveOnSecondaryFace.GetModified(m);
                    return curveOnSecondaryFace;
                }
            }
            else
            {
                oriented = false;
                if (face == primaryFace)
                {
                    if (curveOnPrimaryFace != null)
                        curveOnPrimaryFace = curveOnPrimaryFace.GetModified(m);
                    return curveOnPrimaryFace;
                }
                else if (face == secondaryFace)
                {
                    if (curveOnSecondaryFace != null)
                        curveOnSecondaryFace = curveOnSecondaryFace.GetModified(m);
                    return curveOnSecondaryFace;
                }
                else return null;
            }
        }
        internal void SetNotOriented()
        {
            oriented = false;
        }
        #region IComparable<Edge> Members
        int IComparable<Edge>.CompareTo(Edge other)
        {
            return hashCode.CompareTo(other.hashCode);
        }
        #endregion

        internal static List<Vertex> RecalcVertices(IEnumerable<Edge> edges)
        {
            BoundingCube ext = BoundingCube.EmptyBoundingCube;
            List<Vertex> allVertices = new List<Vertex>();
            foreach (Edge edg in edges)
            {
                if (edg.v1 != null) allVertices.Add(edg.v1);
                else if (edg.Curve3D != null)
                {
                    edg.v1 = new Vertex(edg.Curve3D.StartPoint);
                    edg.v1.AddEdge(edg);
                    allVertices.Add(edg.v1);
                }
                if (edg.v2 != null) allVertices.Add(edg.v2);
                else if (edg.Curve3D != null)
                {
                    edg.v2 = new Vertex(edg.Curve3D.EndPoint);
                    edg.v2.AddEdge(edg);
                    allVertices.Add(edg.v2);
                }
                if (edg.v1 != null) ext.MinMax(edg.v1.Position);
                if (edg.v2 != null) ext.MinMax(edg.v2.Position);
            }
            double prec = ext.Size * 1e-8;
            OctTree<Vertex> vertexOctTree = new OctTree<Vertex>(ext, prec);
            for (int i = 0; i < allVertices.Count; i++)
            {
                bool duplicateFound = false;
                Vertex[] close = vertexOctTree.GetObjectsFromBox(new BoundingCube(allVertices[i].Position, prec));
                for (int j = 0; j < close.Length; j++)
                {
                    if ((allVertices[i].Position | close[j].Position) < prec)
                    {
                        close[j].MergeWith(allVertices[i]);
                        duplicateFound = true;
                        break;
                    }
                }
                if (!duplicateFound) vertexOctTree.AddObject(allVertices[i]);
            }
            return new List<Vertex>(vertexOctTree.GetAllObjects());
        }

        internal void RemoveSecondaryFace()
        {
            secondaryFace = null;
            curveOnSecondaryFace = null;
        }

        internal void RemovePrimaryFace()
        {
            primaryFace = secondaryFace;
            curveOnPrimaryFace = curveOnSecondaryFace;
            forwardOnPrimaryFace = forwardOnSecondaryFace;
            secondaryFace = null;
            curveOnSecondaryFace = null;
        }

        internal void ReplaceVertex(Vertex toReplace, Vertex replaceWith)
        {
            toReplace.RemoveEdge(this);
            replaceWith.AddEdge(this);
            if (v1 == toReplace)
            {
                v1 = replaceWith;
            }
            if (v2 == toReplace) // beide vertices können gleich sein
            {
                v2 = replaceWith;
            }
            primaryFace.ClearVertices();
            if (secondaryFace != null) secondaryFace.ClearVertices();
        }
        /// <summary>
        /// Stellt fest, ob die beiden geometrisch identisch sind. Die Endpunkte sind meist schon getestet,
        /// die Richtung kann auch eine Rolle spielen.
        /// </summary>
        /// <param name="e1"></param>
        /// <param name="e2"></param>
        /// <param name="vertexTested"></param>
        /// <param name="forward"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        internal static bool IsGeometricallyEqual(Edge e1, Edge e2, bool vertexTested, bool forward, double precision)
        {   // hier einfach mal mit der Überlegung, dass Kanten sich nur in den Endpunkten schneiden der Test nach dem Mittelpunkt
            // vielleicht mit komplexen Kurven besser anders zu testen
            GeoPoint m1 = e1.curve3d.PointAt(0.5);
            GeoPoint m2 = e2.curve3d.PointAt(0.5);
            return (m1 | m2) < precision;
        }

        internal void ExchangeFaces()
        {   // wird bei SplitSeam gebraucht
            Face tempface = secondaryFace;
            ICurve2D tempcurve = curveOnSecondaryFace;
            bool tempforward = forwardOnSecondaryFace;
            secondaryFace = primaryFace;
            curveOnSecondaryFace = curveOnPrimaryFace;
            forwardOnSecondaryFace = forwardOnPrimaryFace;
            primaryFace = tempface;
            curveOnPrimaryFace = tempcurve;
            forwardOnPrimaryFace = tempforward;
        }

        /// <summary>
        /// Both edges must have only a primary face set (secondaryFace==null). The provided edge "ce" is merged (fused) with this one.
        /// "ce" will not be usable furthermore.
        /// </summary>
        /// <param name="ce"></param>
        internal void MergeWith(Edge ce)
        {
            if (secondaryFace == null && ce.secondaryFace == null)
            {
                ce.primaryFace.ReplaceEdge(ce, this);
            }
        }

        /// <summary>
        /// Returns the other face, which contains this edge. If there is no such face, null will be returned.
        /// </summary>
        /// <param name="thisFace">one of the faces of this edge</param>
        /// <returns>the other face</returns>
        public Face OtherFace(Face thisFace)
        {
            if (thisFace == primaryFace) return secondaryFace;
            else return primaryFace;
        }
        public Vertex OtherVertex(Vertex v)
        {
            if (v == v1) return v2;
            else return v1;
        }
        internal void Disconnect()
        {
            v1.RemoveEdge(this);
            v2.RemoveEdge(this);
        }

        internal void SurfaceChanged(ISurface oldSurface, ISurface newSurface)
        {
            // oder eine ICurve Methode machen, die das implementiert
            if (curve3d is InterpolatedDualSurfaceCurve)
            {
                (curve3d as InterpolatedDualSurfaceCurve).ReplaceSurface(oldSurface, newSurface);
                if (curveOnPrimaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve) (curveOnPrimaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).ReplaceSurface(oldSurface, newSurface);
                if (curveOnSecondaryFace is InterpolatedDualSurfaceCurve.ProjectedCurve) (curveOnSecondaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).ReplaceSurface(oldSurface, newSurface);
            }
        }

        internal void CheckConsistency()
        {
            if (curve3d is InterpolatedDualSurfaceCurve)
            {   // InterpolatedDualSurfaceCurve macht nur Sinn, wenn sie mit diesem Edge übereinstimmt. 
                // Es wäre besser, die InterpolatedDualSurfaceCurve würde einen (Rück-)Verweis auf die Edge haben und Surface1/2 und ProjectedCurve von hier nehmen
                if ((curve3d as InterpolatedDualSurfaceCurve).Surface1 != primaryFace.internalSurface && (curve3d as InterpolatedDualSurfaceCurve).Surface2 != primaryFace.internalSurface) throw new ApplicationException("InterpolatedDualSurfaceCurve: wrong surface");
                if ((curve3d as InterpolatedDualSurfaceCurve).Surface1 != secondaryFace.internalSurface && (curve3d as InterpolatedDualSurfaceCurve).Surface2 != secondaryFace.internalSurface) throw new ApplicationException("InterpolatedDualSurfaceCurve: wrong surface");
                if ((curveOnPrimaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).Curve3D != curve3d) throw new ApplicationException("InterpolatedDualSurfaceCurve: wrong parent curve");
                if (curveOnSecondaryFace != null && (curveOnSecondaryFace as InterpolatedDualSurfaceCurve.ProjectedCurve).Curve3D != curve3d) throw new ApplicationException("InterpolatedDualSurfaceCurve: wrong parent curve");
            }
        }

        internal void SetVertices(Vertex vertex1, Vertex vertex2)
        {
            if (v1 != null) v1.RemoveEdge(this);
            v1 = vertex1;
            v1.AddEdge(this);
            if (v2 != null) v2.RemoveEdge(this);
            v2 = vertex2;
            v2.AddEdge(this);
        }

        internal static void Connect(Edge edge1, Edge edge2, Face face)
        {
            if (edge1.Forward(face) && edge2.Forward(face))
            {
                if (edge1.v2 == null)
                {
                    if (edge1.Curve3D != null) edge1.v2 = new CADability.Vertex(edge1.curve3d.EndPoint);
                    else edge1.v2 = new CADability.Vertex(edge2.curve3d.StartPoint);
                }
                edge2.v1 = edge1.v2;
                edge1.v2.AddEdge(edge1);
                edge2.v1.AddEdge(edge2);
            }
            else if (!edge1.Forward(face) && edge2.Forward(face))
            {
                if (edge1.v1 == null)
                {
                    if (edge1.Curve3D != null) edge1.v1 = new CADability.Vertex(edge1.curve3d.StartPoint);
                    else edge1.v1 = new CADability.Vertex(edge2.curve3d.StartPoint);
                }
                edge2.v1 = edge1.v1;
                edge1.v1.AddEdge(edge1);
                edge2.v1.AddEdge(edge2);
            }
            else if (edge1.Forward(face) && !edge2.Forward(face))
            {
                if (edge1.v2 == null)
                {
                    if (edge1.Curve3D != null) edge1.v2 = new CADability.Vertex(edge1.curve3d.EndPoint);
                    else edge1.v2 = new CADability.Vertex(edge2.curve3d.EndPoint);
                }
                edge2.v2 = edge1.v2;
                edge1.v2.AddEdge(edge1);
                edge2.v2.AddEdge(edge2);
            }
            else if (!edge1.Forward(face) && edge2.Forward(face))
            {
                if (edge1.v1 == null)
                {
                    if (edge1.Curve3D != null) edge1.v1 = new CADability.Vertex(edge1.curve3d.StartPoint);
                    else edge1.v1 = new CADability.Vertex(edge2.curve3d.EndPoint);
                }
                edge2.v2 = edge1.v1;
                edge1.v1.AddEdge(edge1);
                edge2.v2.AddEdge(edge2);
            }
        }
        /// <summary>
        /// Find a loop (closed list of connected edges) starting with this edges and using edges from the provided (available) set.
        /// </summary>
        /// <param name="available"></param>
        /// <param name="onThisFace"></param>
        /// <returns></returns>
        internal List<Edge> FindLoop(Set<Edge> available, Face onThisFace)
        {
            List<Edge> res = new List<Edge>();
            res.Add(this);
            Vertex startVertex = StartVertex(onThisFace);
            Vertex endVertex = EndVertex(onThisFace);
            while (startVertex != endVertex)
            {
                Edge goOnWith = null;
                foreach (Edge edg in endVertex.AllEdges)
                {
                    if (available.Contains(edg) && edg.StartVertex(onThisFace) == endVertex)
                    {
                        goOnWith = edg;
                        break;
                    }
                }
                if (goOnWith != null)
                {
                    endVertex = goOnWith.EndVertex(onThisFace);
                    res.Add(goOnWith);
                    if (res.Count > available.Count) break;// there is a inner loop in "available"
                }
                else
                {
                    break; // loop not closed
                }
            }
            return res;
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            if (curve3d == null) return -1;
            if (Vertex1 == Vertex2) return -1; // no closed curves, this must be a minimal curve
            if (!export.EdgeToDefInd.TryGetValue(this, out int ec))
            {
                // #64=EDGE_CURVE('',#44,#58,#63,.F.) ;
                int nv1 = (Vertex1 as IExportStep).Export(export, false);
                int nv2 = (Vertex2 as IExportStep).Export(export, false);
                int cn = (curve3d as IExportStep).Export(export, false);
                ec = export.WriteDefinition("EDGE_CURVE('',#" + nv1.ToString() + ",#" + nv2.ToString() + ",#" + cn.ToString() + ",.T.)");
                export.EdgeToDefInd[this] = ec;
            }
            // #69=ORIENTED_EDGE('',*,*,#64,.F.);
            string orient; // topLevel used as direction
            if (topLevel) orient = ",.T.";
            else orient = ",.F.";
#if DEBUG
            IntegerProperty ip = (curve3d as IGeoObject).UserData.GetData("Step.DefiningIndex") as IntegerProperty;
            int iv = 0;
            if (ip != null) iv = ip.IntegerValue;
            return export.WriteDefinition("ORIENTED_EDGE('" + iv.ToString() + "',*,*,#" + ec.ToString() + orient + ")");

#else
            return export.WriteDefinition("ORIENTED_EDGE('',*,*,#" + ec.ToString() + orient + ")");
#endif
        }

        public bool IsConnected(Edge other)
        {
            return other.v1 == v1 || other.v1 == v2 || other.v2 == v1 || other.v2 == v2;
        }
        public bool IsTangentialEdge()
        {
            if (secondaryFace == null) return false;
            if (primaryFace.Surface.SameGeometry(primaryFace.Domain, secondaryFace.Surface, secondaryFace.Domain, Precision.eps, out _)) return true;
            if ((primaryFace.Surface is PlaneSurface && (secondaryFace.Surface is CylindricalSurface || secondaryFace.Surface is ConicalSurface || secondaryFace.Surface is ToroidalSurface)) ||
                (secondaryFace.Surface is PlaneSurface && (primaryFace.Surface is CylindricalSurface || primaryFace.Surface is ConicalSurface || primaryFace.Surface is ToroidalSurface)))
            {
                // we only need to check a single position
                GeoVector n1 = primaryFace.Surface.GetNormal(Vertex1.GetPositionOnFace(primaryFace));
                GeoVector n2 = secondaryFace.Surface.GetNormal(Vertex1.GetPositionOnFace(secondaryFace));
                return Precision.SameNotOppositeDirection(n1, n2);
            }
            else
            {
                GeoVector n1 = primaryFace.Surface.GetNormal(Vertex1.GetPositionOnFace(primaryFace));
                GeoVector n2 = secondaryFace.Surface.GetNormal(Vertex1.GetPositionOnFace(secondaryFace));
                if (!Precision.SameNotOppositeDirection(n1, n2)) return false;
                n1 = primaryFace.Surface.GetNormal(Vertex2.GetPositionOnFace(primaryFace));
                n2 = secondaryFace.Surface.GetNormal(Vertex2.GetPositionOnFace(secondaryFace));
                if (!Precision.SameNotOppositeDirection(n1, n2)) return false;
                // now there could be cases, where we would have to check more points, and I don't know, how to tell, so we check only the middle point
                GeoPoint m = curve3d.PointAt(0.5);
                n1 = primaryFace.Surface.GetNormal(primaryFace.Surface.PositionOf(m));
                n2 = secondaryFace.Surface.GetNormal(secondaryFace.Surface.PositionOf(m));
                return Precision.SameNotOppositeDirection(n1, n2);
            }
        }
        public bool IsPartOfHole(Face onThisFace)
        {
            for (int i = 0; i < onThisFace.HoleCount; i++)
            {
                Edge[] hole = onThisFace.HoleEdges(i);
                for (int j = 0; j < hole.Length; j++)
                {
                    if (hole[j] == this) return true;
                }
            }
            return false;
        }
        internal MenuWithHandler[] GetContextMenu(IFrame frame)
        {
            MenuWithHandler mhdist = new MenuWithHandler();
            mhdist.ID = "MenuId.Parametrics.DistanceTo";
            mhdist.Text = StringTable.GetString("MenuId.Parametrics.DistanceTo", StringTable.Category.label);
            mhdist.Target = new ParametricsDistanceActionOld(this, frame);
            return new MenuWithHandler[] { mhdist };
        }
#if DEBUG
        public bool IsDebug
        {
            get
            {
                if (curve3d is IGeoObjectImpl) return (curve3d as IGeoObjectImpl).IsDebug;
                return false;
            }
        }


#endif
    }
}
