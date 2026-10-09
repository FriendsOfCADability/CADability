using CADability.Curve2D;
using CADability.Shapes;
using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CADability.GeoObject
{
    /// <summary>
    /// A reference to a edge. This is mainly used as a UserData: the Clone method does not clone the edge but returns this object, so you won't have an infinite loop
    /// when the userdata of a edge refers to itself.
    /// </summary>
    public class EdgeReference : ICloneable
    {
        public EdgeReference(Edge edge) { Edge = edge; }
        public Edge Edge { get; }
        public object Clone()
        {
            return this; // don't clone the edge, this would result in an infinite loop
        }
    }

    /// <summary>
    /// Extension methods for the analysis of shells, faces and edges.
    /// </summary>
    public static class ShellExtensions
    {
        /// <summary>
        /// Finds the faces of the shell which are parallel to <paramref name="distanceFrom"/> (see <see cref="Surfaces.ParallelDistance"/>):
        /// for a sheet metal part or the flat walls of a rectangular tube these are the faces on the other side of the material,
        /// and the distance is the thickness. The lists contain the faces, the distances and the points on both faces.
        /// </summary>
        /// <returns>the number of faces found</returns>
        public static int GetFaceDistances(this Shell shell, Face distanceFrom, GeoPoint touchingPoint, out List<Face> distanceTo, out List<double> distance, out List<GeoPoint> pointsFrom, out List<GeoPoint> pointsTo)
        {
            distanceTo = new List<Face>();
            distance = new List<double>();
            pointsFrom = new List<GeoPoint>();
            pointsTo = new List<GeoPoint>();
            foreach (Face face in shell.Faces)
            {
                if (face == distanceFrom) continue;
                if (Surfaces.ParallelDistance(distanceFrom.Surface, distanceFrom.Domain, face.Surface, face.Domain, touchingPoint, out GeoPoint2D uv1, out GeoPoint2D uv2))
                {
                    GeoPoint pFrom = distanceFrom.Surface.PointAt(uv1);
                    GeoPoint pTo = face.Surface.PointAt(uv2);
                    double dist = pFrom | pTo;
                    if (dist > Precision.eps)
                    {
                        distanceTo.Add(face);
                        distance.Add(dist);
                        pointsFrom.Add(pFrom);
                        pointsTo.Add(pTo);
                    }
                }
            }
            return distanceTo.Count;
        }
        /// <summary>
        /// How two faces meet at an edge, see <see cref="Adjacency(Edge)"/>.
        /// </summary>
        public enum AdjacencyType
        {
            Unknown,
            Open,
            SameSurface,
            Tangent,
            Convex,
            Concave,
            Mixed
        }
        /// <summary>
        /// Classifies the edge by the two faces it connects: open (only one face), on the same surface, tangential, or with a
        /// convex (outer) or concave (inner) bend. The outer wall of a tube meets its end faces with convex edges, a cut-out
        /// in the wall is bounded by concave edges when seen from the outer wall.
        /// </summary>
        public static AdjacencyType Adjacency(this Edge edge)
        {
            if (edge.Curve3D == null) return AdjacencyType.Unknown; // a pole
            if (edge.SecondaryFace != null)
            {
                if (edge.PrimaryFace.Surface.SameGeometry(edge.PrimaryFace.Domain, edge.SecondaryFace.Surface, edge.SecondaryFace.Domain, Precision.eps, out ModOp2D _)) return AdjacencyType.SameSurface;
                if (edge.IsTangentialEdge()) return AdjacencyType.Tangent;
                // there should be a test for "mixed"
                Vertex v1 = edge.StartVertex(edge.PrimaryFace);
                GeoPoint2D uvp = v1.GetPositionOnFace(edge.PrimaryFace);
                GeoPoint2D uvs = v1.GetPositionOnFace(edge.SecondaryFace);
                GeoVector curveDir;
                if (edge.Forward(edge.PrimaryFace)) curveDir = edge.Curve3D.StartDirection;
                else curveDir = -edge.Curve3D.EndDirection;
                double orientation = curveDir * (edge.PrimaryFace.Surface.GetNormal(uvp) ^ edge.SecondaryFace.Surface.GetNormal(uvs));
                if (orientation > 0) return AdjacencyType.Convex;
                else return AdjacencyType.Concave;
            }
            else
            {
                return AdjacencyType.Open;
            }
        }
        /// <summary>
        /// True, when all edges of the face are convex (or concave, if <paramref name="reverse"/>) or tangential.
        /// </summary>
        public static bool AllEdgesAreConvex(this Face face, bool reverse)
        {
            AdjacencyType toFollow = reverse ? AdjacencyType.Concave : AdjacencyType.Convex;
            foreach (Edge edge in face.AllEdges)
            {
                if (edge.Adjacency() != AdjacencyType.Tangent && edge.Adjacency() != toFollow) return false;
            }
            return true;
        }
        /// <summary>
        /// Groups the faces into regions which are connected by convex (or concave, if <paramref name="reverse"/>) or
        /// tangential edges.
        /// </summary>
        public static List<List<Face>> GetConvexParts(IEnumerable<Face> faces, bool reverse)
        {
            // HashSet<Face> faces = new HashSet<Face>(shell.Faces);
            var visited = new HashSet<Face>();
            var result = new List<List<Face>>();

            AdjacencyType toFollow = reverse ? AdjacencyType.Concave : AdjacencyType.Convex;

            foreach (var face in faces)
            {
                if (visited.Contains(face))
                    continue;

                // start a new region
                var region = new List<Face>();
                var queue = new Queue<Face>();
                queue.Enqueue(face);
                visited.Add(face);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    region.Add(current);

                    foreach (var edge in current.Edges)
                    {
                        // only respect convex or tangential connections
                        if (edge.Adjacency() != toFollow &&
                            edge.Adjacency() != AdjacencyType.Tangent)
                            continue;

                        Face neighbor = edge.OtherFace(current);

                        if (neighbor == null || visited.Contains(neighbor))
                            continue;

                        if (faces.Contains(neighbor))
                        {
                            queue.Enqueue(neighbor);
                            visited.Add(neighbor);
                        }
                    }
                }

                result.Add(region);
            }

            return result;
        }

        /// <summary>
        /// Finds the (u,v) on <paramref name="surface"/> that minimises
        /// the distance to <paramref name="target"/>.
        /// </summary>
        /// <param name="surface">Parametric surface f(u,v).</param>
        /// <param name="target">3-D point P.</param>
        /// <param name="startValue">Initial guess (u0,v0).</param>
        /// <param name="tol">Tolerance in world units.</param>
        /// <param name="maxIter">Maximum number of Newton steps.</param>
        public static GeoPoint2D PositionOf(
            this ISurface surface,
            GeoPoint target,
            GeoPoint2D startValue,
            double tol = 1e-8,
            int maxIter = 30)
        {
            var uv = new GeoPoint2D(startValue.x, startValue.y);

            for (int iter = 0; iter < maxIter; iter++)
            {
                // First- and second-order surface data
                surface.Derivation2At(
                    uv, out GeoPoint loc, out GeoVector du, out GeoVector dv,
                    out GeoVector duu, out GeoVector dvv, out GeoVector duv);

                GeoVector r = loc - target;               // residual f-P
                double resLen = r.Length;
                if (resLen < tol) return uv;              // converged

                // Gradient of g
                var g = Vector<double>.Build.DenseOfArray(new[]
                {
                r * du,                               // dot product
                r * dv
            });

                // Hessian of g
                var H = Matrix<double>.Build.DenseOfArray(new[,]
                {
                { du * du + r * duu,  du * dv + r * duv },
                { du * dv + r * duv,  dv * dv + r * dvv }
            });

                // Solve H Δ = -∇g  (fallback to damped GN if necessary)
                Vector<double> delta;
                try
                {
                    delta = H.Solve(-g);
                }
                catch (Exception)                   // singular Hessian
                {
                    // Levenberg-Marquardt fallback: (H + λI) Δ = -∇g
                    double lambda = 1e-4 * H.Diagonal().Maximum();
                    delta = (H + lambda * Matrix<double>.Build.DenseIdentity(2)).Solve(-g);
                }

                // Update parameters
                uv.x += delta[0];
                uv.y += delta[1];

                if (delta.L2Norm() < tol) return uv;      // small step → done
            }

            // If we get here we did not converge
            return GeoPoint2D.Invalid;
        }
    }
}
