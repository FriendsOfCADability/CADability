using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using Wintellect.PowerCollections;

namespace CADability
{
    /// <summary>
    /// Describes a vertex of an <see cref="Edge"/>. A vertex is the start or endpoint of an edge.
    /// It connects at least two edges but can belong to any number of edges.
    /// </summary>

    [DebuggerDisplayAttribute("Vertex, hc = {hashCode.ToString()}"), Serializable()]
    public class Vertex : IComparable<Vertex>, IOctTreeInsertable, ISerializable, IDeserializationCallback, IJsonSerialize, IExportStep
    {
        private GeoPoint position;
        private HashSet<Edge> edges;
        Dictionary<Face, GeoPoint2D> uvposition; // a cache of already calculated uv positions on faces
        internal static int hashCodeCounter = 0;
        private int hashCode;
        private Edge[] deserializedEdges; // we need this, because we cannot serialize/deserialize Set<Edge>
        public Vertex(GeoPoint position)
        {
            this.position = position;
            edges = new HashSet<Edge>();
            uvposition = new Dictionary<Face, GeoPoint2D>();
            hashCode = hashCodeCounter++;
#if DEBUG
            if (37747 == hashCode || 37844 == hashCode) { }
#endif
        }
        internal void AddEdge(Edge edge)
        {
            lock (edges)
            {
                edges.Add(edge);
                lock (uvposition)
                {
                    if (edge.PrimaryFace != null && edge.PrimaryFace.Surface != null && !uvposition.ContainsKey(edge.PrimaryFace))
                    {
                        uvposition[edge.PrimaryFace] = edge.PrimaryFace.PositionOf(position);
                    }
                    if (edge.SecondaryFace != null && edge.SecondaryFace.Surface != null && !uvposition.ContainsKey(edge.SecondaryFace))
                    {
                        uvposition[edge.SecondaryFace] = edge.SecondaryFace.PositionOf(position);
                    }
                }
            }
        }
        internal void AddEdge(Edge edge, GeoPoint2D pruv, GeoPoint2D scuv)
        {
            lock (edges)
            {
                edges.Add(edge);
                lock (uvposition)
                {
                    if (!uvposition.ContainsKey(edge.PrimaryFace))
                    {
                        uvposition[edge.PrimaryFace] = pruv;
                    }
                    if (!uvposition.ContainsKey(edge.SecondaryFace))
                    {
                        uvposition[edge.SecondaryFace] = scuv;
                    }
                }
            }
        }
        internal void AddEdge(Edge edge, GeoPoint2D pruv)
        {
            lock (edges)
            {
                edges.Add(edge);
                lock (uvposition)
                {
                    if (!uvposition.ContainsKey(edge.PrimaryFace))
                    {
                        uvposition[edge.PrimaryFace] = pruv;
                    }
                }
            }
        }
        internal void RemoveEdge(Edge edge)
        {
            lock (edges)
            {
                edges.Remove(edge);
            }
        }
        internal void RemoveAllEdges()
        {
            lock (edges)
            {
                edges.Clear();
            }
        }
        internal void Modify(ModOp m)
        {
            position = m * position;
        }
        /// <summary>
        /// Gets the position of this vertex
        /// </summary>
        public GeoPoint Position
        {
            get
            {
                return position;
            }
            internal set
            {
                position = value; // take care when setting the position of an existing vertex!
            }
        }
        /// <summary>
        /// Gets the list of edges of this vertex
        /// </summary>
        public Edge[] Edges
        {
            get
            {
                lock (edges)
                {
                    return edges.ToArray();
                }
            }
        }
        public List<Edge> EdgesOnFace(Face onThisFace)
        {
            List<Edge> res = new List<Edge>();
            foreach (Edge edge in edges)
            {
                if (edge.PrimaryFace == onThisFace || edge.SecondaryFace == onThisFace)
                    res.Add(edge);
            }
            return res;
        }
        /// <summary>
        /// Returns a list of all edges in this vertex that satisfy the provided condition.
        /// </summary>
        /// <param name="pr">The condition</param>
        /// <returns>Edges that satisfy the condition</returns>
        public List<Edge> ConditionalEdges(Predicate<Edge> pr)
        {

            lock (edges)
            {
                List<Edge> res = new List<Edge>(edges.Where(e => pr(e)));
                return res;
            }
        }
        public Set<Edge> ConditionalEdgesSet(Predicate<Edge> pr)
        {
            lock (edges)
            {
                return new Set<Edge>(edges.Where(e => pr(e)));
            }
        }
        internal void AddPositionOnFace(Face fc, GeoPoint2D uv)
        {
            lock (uvposition)
            {
                uvposition[fc] = uv;
            }
        }
        internal void RemovePositionOnFace(Face face)
        {
            lock (uvposition)
            {
                uvposition.Remove(face);
            }
        }
        public GeoPoint2D GetPositionOnFace(Face fc)
        {
            GeoPoint2D res;
            lock (uvposition)
            {
                if (uvposition.TryGetValue(fc, out res)) return res;
                GeoPoint2D uv = fc.PositionOf(position);
                uvposition[fc] = uv;
                return uv;
            }
        }
        public Face[] Faces
        {
            get
            {
                lock (uvposition)
                {
                    List<Face> res = new List<Face>(uvposition.Keys);
                    return res.ToArray();
                }
            }
        }
        public HashSet<Face> InvolvedFaces
        {
            get
            {
                HashSet<Face> res = new HashSet<Face>();
                foreach (Edge edge in edges)
                {
                    res.Add(edge.PrimaryFace);
                    if (edge.SecondaryFace != null) res.Add(edge.SecondaryFace);
                }
                return res;
            }
        }
        public Set<Edge> AllEdges
        {
            get
            {
                return new Set<Edge>(edges);
            }
        }
        public override int GetHashCode()
        {
            return hashCode;
        }
        public override bool Equals(object obj)
        {
            Vertex other = obj as Vertex;
            if (other == null) return false;
            return other.hashCode == hashCode;
        }
        #region IComparable<Vertex> Members
        int IComparable<Vertex>.CompareTo(Vertex other)
        {
            return hashCode.CompareTo(other.hashCode);
        }
        #endregion
        public static IEnumerable<Edge> ConnectingEdges(Vertex v1, Vertex v2)
        {
            lock (v1.edges)
            {
                return (v1.edges.Intersect(v2.edges));
            }
        }
        public static Edge SingleConnectingEdge(Vertex v1, Vertex v2)
        {
            lock (v1.edges)
            {
                return v1.edges.Intersect(v2.edges).FirstOrDefault();
            }
        }

        internal IGeoObject DebugPoint
        {
            get
            {
                Point res = Point.Construct();
                res.Location = position;
                res.Symbol = PointSymbol.Cross;
                return res;
            }
        }

        internal void MergeWith(Vertex ev)
        {
            if (ev == this) return;
#if DEBUG
            Set<Edge> both = new Set<Edge>(edges.Intersect(ev.edges));
            double dist = Position | ev.Position;
#endif
            foreach (Edge edge in ev.Edges)
            {
                if (edge.PrimaryFace != null) edge.ReplaceVertex(ev, this);
            }
            lock (ev.uvposition)
            {
                foreach (KeyValuePair<Face, GeoPoint2D> kv in ev.uvposition)
                {
                    lock (uvposition)
                    {
                        if (!uvposition.ContainsKey(kv.Key)) uvposition[kv.Key] = kv.Value;
                    }
                }
            }
        }

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {

            return new BoundingCube(position);
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Contains(position, precision);
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            throw new NotImplementedException();
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new NotImplementedException();
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            throw new NotImplementedException();
        }

        internal void AdjustCoordinate(GeoPoint p)
        {
#if DEBUG
            if (1070 == this.hashCode || 1063 == this.hashCode)
            {

            }
#endif
            GeoPoint mp = new GeoPoint(position, p); // point in between the two starting positions
            // collect all involved surfaces
            Set<Face> surfaces = new Set<Face>();
            foreach (Edge edg in edges)
            {
                surfaces.Add(edg.PrimaryFace);
                if (edg.SecondaryFace != null)
                    surfaces.Add(edg.SecondaryFace);
            }
            // find surfaces, which are not tangential at this position
            Dictionary<GeoVector, Face> nonTangentialSurfaces = new Dictionary<GeoVector, Face>();
            foreach (Face srf in surfaces)
            {
                GeoVector n = srf.Surface.GetNormal(srf.Surface.PositionOf(mp));
                if (n.IsNullVector()) continue; // a pole
                bool found = false;
                foreach (KeyValuePair<GeoVector, Face> kv in nonTangentialSurfaces)
                {
                    GeoVector cross = kv.Key.Normalized ^ n.Normalized;
                    double cl = cross.Length;
                    if (cl < 1e-3)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) nonTangentialSurfaces[n] = srf;
            }
            if (nonTangentialSurfaces.Count >= 3)
            {
                Face[] fcs = new Face[nonTangentialSurfaces.Count];
                nonTangentialSurfaces.Values.CopyTo(fcs, 0);
                GeoPoint2D uv1, uv2, uv3;
                Surfaces.NewtonIntersect(fcs[0].Surface, fcs[0].Area.GetExtent(), fcs[1].Surface, fcs[1].Area.GetExtent(), fcs[2].Surface, fcs[2].Area.GetExtent(), ref mp, out uv1, out uv2, out uv3);
                if ((!fcs[0].Area.Contains(uv1, true) && fcs[0].Area.Distance(uv1) > fcs[0].Area.GetExtent().Size * 1e-4)
                    || (!fcs[1].Area.Contains(uv2, true) && fcs[1].Area.Distance(uv2) > fcs[0].Area.GetExtent().Size * 1e-4)
                    || (!fcs[2].Area.Contains(uv3, true) && fcs[2].Area.Distance(uv3) > fcs[2].Area.GetExtent().Size * 1e-4))
                {
                    mp = position; // invalid recalculation, e.g.: three planes with a common intersection line like in 3567_0005_01_02.stp
                }
            }
            else if (nonTangentialSurfaces.Count == 2)
            {   // use the two non tangential surfaces and a plane perpendicular to both surfaces in that point
                // to calculate a good point
                Face[] fcs = new Face[nonTangentialSurfaces.Count];
                GeoVector[] nrm = new GeoVector[nonTangentialSurfaces.Count];
                nonTangentialSurfaces.Values.CopyTo(fcs, 0);
                nonTangentialSurfaces.Keys.CopyTo(nrm, 0);
                PlaneSurface pls = new PlaneSurface(new Plane(mp, nrm[0], nrm[1]));
                GeoPoint2D uv1, uv2, uv3;
                Surfaces.NewtonIntersect(fcs[0].Surface, fcs[0].Area.GetExtent(), fcs[1].Surface, fcs[1].Area.GetExtent(), pls, BoundingRect.HalfInfinitBoundingRect, ref mp, out uv1, out uv2, out uv3);
            }

            position = mp;
            foreach (Edge edg in edges)
            {
                if (edg.Curve3D != null && edg.Vertex1 != edg.Vertex2)
                {
                    try
                    {
                        if (edg.Vertex1 == this && !Precision.IsEqual(edg.Curve3D.EndPoint, mp)) edg.Curve3D.StartPoint = mp;
                        else if (edg.Vertex2 == this && !Precision.IsEqual(edg.Curve3D.StartPoint, mp)) edg.Curve3D.EndPoint = mp;
                        else if (edg.Vertex1 != this && edg.Vertex2 != this) throw new ApplicationException("Edge-Vertex mismatch");
                    }
                    catch { }
                }
            }
        }

        #region IJsonSerialize Members
        protected Vertex()
        {
            edges = new HashSet<Edge>();
            uvposition = new Dictionary<Face, GeoPoint2D>();
            hashCode = hashCodeCounter++;
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Position", position);
            List<Edge> usableEdges = new List<Edge>();
            foreach (Edge edge in edges)
            {
                if (edge.Owner is Face owner)
                {
                    if (edge.PrimaryFace == owner || edge.SecondaryFace == owner) usableEdges.Add(edge);
                    else
                    {
                        edge.Owner = edge.PrimaryFace;
                        usableEdges.Add(edge);
                    }
                }

            }
            data.AddProperty("Edges", usableEdges.ToArray());
        }
        public void SetObjectData(IJsonReadData data)
        {
            position = data.GetProperty<GeoPoint>("Position");
            edges = new HashSet<Edge>((data.GetProperty<Edge[]>("Edges")));
        }
        #endregion
        #region ISerializable
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Position", position);
            info.AddValue("Edges", edges.ToArray());
        }

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            edges = new HashSet<Edge>(deserializedEdges);
            deserializedEdges = null;
        }

        protected Vertex(SerializationInfo info, StreamingContext context)
        {
            position = (GeoPoint)info.GetValue("Position", typeof(GeoPoint));
            deserializedEdges = (Edge[])info.GetValue("Edges", typeof(Edge[]));
            uvposition = new Dictionary<Face, GeoPoint2D>();
            hashCode = hashCodeCounter++;
        }

        /// <summary>
        /// Find an edge, which is leaving the vertex on the provided face
        /// </summary>
        /// <param name="onThisFace"></param>
        /// <returns></returns>
        internal Edge FindOutgoing(Face onThisFace)
        {
            Set<Edge> edgOnFace = onThisFace.AllEdgesSet;
            lock (edges)
            {
                foreach (Edge edg in edges.Intersect(edgOnFace))
                {
                    if (edg.StartVertex(onThisFace) == this) return edg;
                }
                return null;
            }
        }

        /// <summary>
        /// Find a connection (of edges) between startVertex and the first stopVertex on the provided face
        /// </summary>
        /// <param name="startVertex"></param>
        /// <param name="stopVertices"></param>
        /// <param name="onThisFace"></param>
        /// <returns></returns>
        internal static List<Edge> FindConnection(Vertex startVertex, Set<Edge> usableEdges, Set<Vertex> stopVertices, Face onThisFace)
        {
            List<Edge> res = new List<Edge>();
            do
            {
                Edge edg = startVertex.ConditionalEdgesSet(delegate (Edge e)
                {   // edges from usableEdges that start at startVertex (on onThisFace)
                    if (!usableEdges.Contains(e)) return false;
                    return e.StartVertex(onThisFace) == startVertex;
                }).GetAny(); // this should be exactely one
                if (edg == null) return res; // this should never be the case
                res.Add(edg);
                startVertex = edg.EndVertex(onThisFace);
                if (stopVertices.Contains(startVertex)) return res;
            } while (res.Count < usableEdges.Count); // this condition should never be met, it is an emergency exit (maybe better throw an exception)
            return res;
        }

        internal static GeoPoint[] ToGeoPointArray(IEnumerable<Vertex> list)
        {
            List<GeoPoint> res = new List<GeoPoint>();
            foreach (Vertex vtx in list)
            {
                res.Add(vtx.position);
            }
            return res.ToArray();
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            if (!export.VertexToDefInd.TryGetValue(this, out int vn))
            {
                // #43=CARTESIAN_POINT('Vertex',(25.,25.,50.));
                // #44=VERTEX_POINT('',#43) ;
                int cp = (Position as IExportStep).Export(export, false);
                vn = export.WriteDefinition("VERTEX_POINT('',#" + cp.ToString() + ")");
                export.VertexToDefInd[this] = vn;
            }
            return vn;
        }
        /// <summary>
        /// Check whether this vertex is a periodic vertex, i.e. there is another edge on another face, which has the same geometry
        /// Like a vertex on the seam of a cylinder
        /// </summary>
        /// <param name="face"></param>
        /// <returns></returns>
        internal bool IsPeriodicOnFace(Face face)
        {
            foreach (Edge edge in AllEdges)
            {
                if (edge.PrimaryFace == face || edge.SecondaryFace == face)
                {
                    if (edge.ConnectsSameSurfaces()) return true;
                }
            }
            return false;
        }

        #endregion
    }
}
