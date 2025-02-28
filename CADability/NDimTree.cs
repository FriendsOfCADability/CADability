using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;

namespace CADability
{
    abstract class NDimTree<T>
    {
        public class Node
        {
            /// <summary>
            /// the subnodes, may be null. Also individual indizes may be null.
            /// The number of sub nodes is 2^dimension, the indexing follows the rule from <see cref="SubNodeIndex"/>.
            /// </summary>
            public Node[] subNode;
            /// <summary>
            /// Set of <see cref="INDimTreeInsertable"/> objects in this NDimTree
            /// This set is the contents of the leaf of the tree. If it is null, the tree has been subdevided and subTree containst the branches.
            /// Implemented as a set, because it is faster to unite with another set
            /// </summary>
            public HashSet<T> objects;
            /// <summary>
            /// Back reference to the parent node.
            /// </summary>
            public Node parent;
            /// <summary>
            /// Back reference to the root.
            /// </summary>
            public NDimTree<T> root;
            /// <summary>
            /// Deepth of this node in the tree
            /// </summary>
            public int deepth;
            public readonly double[] minExtent;
            public readonly double[] maxExtent;
#if DEBUG
            static int idCounter = 0; // for debugging identification
            int id;
#endif
            public Node(double[] min, double[] max)
            {
#if DEBUG
                id = ++idCounter;
#endif
                minExtent = min;
                maxExtent = max;
            }
            /// <summary>
            /// Returns the index of the sub node, where <paramref name="upperPart"/> is true, if the upper part of this dimension is requested.
            /// </summary>
            /// <param name="upperPart">denotes whether the upper (or lower) part of a certain dimension index is requested</param>
            /// <returns></returns>
            public int SubNodeIndex(bool[] upperPart)
            {
                int res = 0;
                for (int i = 0; i < upperPart.Length; i++)
                {
                    if (upperPart[i]) res += (1 << i);
                }
                return res;
            }

            internal void AddObject(T objectToAdd)
            {
                if (!root.objectsAreInfinite)
                {   // Testing the extent is in most cases faster than HitTest. When "objectsAreInfinite" is true, we cannot get the extent.
                    if (root.GetExtent(objectToAdd, out double[] min, out double[] max))
                    {
                        for (int i = 0; i < minExtent.Length; i++)
                        {
                            if (minExtent[i] > max[i]) return; // nothing to do, because the extent of the object doesn't overlap the extent of this node
                            if (maxExtent[i] < min[i]) return;
                        }
                    }
                }
                if (!root.HitTest(objectToAdd, minExtent, maxExtent)) return; // this node doesn't interfere with the object
                if (subNode == null && objects == null) objects = new HashSet<T>(); // the node was empty
                if (subNode == null && root.SplitNode(this, objectToAdd))
                {   // this node (which is a leaf) must be splitted into sub nodes
                    subNode = root.MakeSubNodes(this);
                    for (int i = 0; i < subNode.Length; i++)
                    {   // distribute the objects to the sub nodes
                        subNode[i].root = root;
                        subNode[i].parent = this;
                        subNode[i].deepth = deepth + 1;
                        foreach (T item in objects)
                        {
                            subNode[i].AddObject(item);
                        }
                    }
                    objects = null; // the set has been distibuted to the subnodes, it is not needed any more in this node
                }
                if (subNode != null)
                {
                    for (int i = 0; i < subNode.Length; i++)
                    {
                        subNode[i].AddObject(objectToAdd);
                    }
                }
                else
                {
                    // this is a leaf, which dosn't need to be splitted
                    objects.Add(objectToAdd);
                }
            }

            internal void AddLeafs(List<Node> leafs, Func<Node, bool> filter)
            {
                if (subNode != null)
                {
                    for (int i = 0; i < subNode.Length; i++)
                    {
                        subNode[i].AddLeafs(leafs, filter);
                    }
                }
                else if (objects != null && objects.Count > 0)
                {
                    if (filter == null || filter(this)) leafs.Add(this);
                }
            }
            /// <summary>
            /// Split this node. Usually an empty node at the beginning, but fully implemented
            /// </summary>
            /// <param name="numLevels"></param>
            internal void Split(int numLevels)
            {
                subNode = root.MakeSubNodes(this);
                for (int i = 0; i < subNode.Length; i++)
                {   // distribute the objects to the sub nodes
                    subNode[i].root = root;
                    subNode[i].parent = this;
                    subNode[i].deepth = deepth + 1;
                    if (objects != null)
                    {
                        foreach (T item in objects)
                        {
                            subNode[i].AddObject(item);
                        }
                    }
                    if (numLevels > 1) subNode[i].Split(numLevels - 1);
                }
                objects = null; // the set has been distibuted to the subnodes, it is not needed any more in this node

            }
        }

        protected readonly double[] minExtent;
        protected readonly double[] maxExtent;
        protected readonly int dimension;
        protected readonly bool objectsAreInfinite; // don't use GetExtent
        protected Node node; // the root node
        public NDimTree(double[] min, double[] max, bool objectsAreInfinite = false)
        {
            minExtent = min;
            maxExtent = max;
            this.objectsAreInfinite = objectsAreInfinite;
            dimension = min.Length;
            node = new Node(min, max)
            {
                root = this,
                parent = null,
                deepth = 0
            };
        }
        abstract protected bool GetExtent(T obj, out double[] min, out double[] max);
        abstract protected bool HitTest(T obj, double[] min, double[] max);
        /// <summary>
        /// Criterion whether to split a node when it contains too many leaves. The default implemenation yield a dynamically balanced
        /// tree which allows more leaves in deeper nodes.
        /// </summary>
        /// <param name="node">The node beeing checked</param>
        /// <param name="objectToAdd">The object beeing added</param>
        /// <returns>true, if node should be splitted, false otherwise</returns>
        protected virtual bool SplitNode(Node node, T objectToAdd)
        {
            return node.objects.Count > (1 << (node.deepth));
        }
        /// <summary>
        /// Creates the sub nodes for the provided node, which is about to be splitted.
        /// The standard implementation devides the extent of the node in half for each dimension and creates 2^dimension new nodes
        /// with the appropriate extents set. Override this method if you want the extent to be unevenly splitted or if you want
        /// to return fewer sub-nodes, which don't fill the eintire extent (because some part of the extent will not be used). No sub-node
        /// may exceed the extent of it's parent node.
        /// </summary>
        /// <param name="node">The node, which is to be splitted</param>
        /// <returns>Array of sub-nodes</returns>
        protected virtual Node[] MakeSubNodes(Node node)
        {
            Node[] res = new Node[1 << dimension];
            double[] midpoint = new double[dimension];
            for (int i = 0; i < midpoint.Length; i++)
            {
                midpoint[i] = (node.minExtent[i] + node.maxExtent[i]) / 2.0;
            }
            for (int i = 0; i < res.Length; i++)
            {
                double[] minExtent = node.minExtent.Clone() as double[];
                double[] maxExtent = node.maxExtent.Clone() as double[];
                for (int j = 0; j < dimension; j++)
                {
                    if ((i & (1 << j)) != 0) minExtent[j] = midpoint[j];
                    else maxExtent[j] = midpoint[j];
                }
                res[i] = new Node(minExtent, maxExtent);
            }
            return res;
        }
        /// <summary>
        /// Add the provided object to the tree. This may split some nodes and cause calls to the method of other objects already in the tree.
        /// </summary>
        /// <param name="objectToAdd">Object beeing added</param>
        public void AddObject(T objectToAdd)
        {   // There is no test, whether the object exceeds the bounds (n-dimensional size) of the tree. The tree must be constructed with the maximum extent and only
            // represents the part inside this extent
            node.AddObject(objectToAdd);
        }
        public List<Node> GetAllLeafs(Func<Node, bool> filter)
        {
            List<Node> res = new List<Node>();
            node.AddLeafs(res, filter);
            return res;
        }
        private class EnumerateTSet : IEnumerator<HashSet<T>>, IEnumerable<HashSet<T>>
        {
            List<Node> list;
            int currentI;
            public EnumerateTSet(List<Node> list)
            {
                this.list = list;
                currentI = -1;
            }
            public HashSet<T> Current => list[currentI].objects;

            object IEnumerator.Current => list[currentI].objects;

            public void Dispose()
            {

            }

            public IEnumerator<HashSet<T>> GetEnumerator()
            {
                return this;
            }

            public bool MoveNext()
            {
                return ++currentI < list.Count;
            }

            public void Reset()
            {
                currentI = -1;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this;
            }
        }
        public IEnumerable<HashSet<T>> GetBestCluster(Func<Node, bool> filter)
        {
            List<Node> allNodes = GetAllLeafs(filter);
            allNodes.Sort(delegate (Node n1, Node n2)
            {
                int res = n2.deepth.CompareTo(n1.deepth);
                if (res == 0) res = n2.objects.Count.CompareTo(n1.objects.Count);
                return res;
            });
#if DEBUG
            //GeoVector normal1 = new GeoVector(allNodes[0].minExtent[0], allNodes[0].minExtent[1], allNodes[0].minExtent[2]);
            //GeoVector normal2 = new GeoVector(allNodes[0].maxExtent[0], allNodes[0].maxExtent[1], allNodes[0].maxExtent[2]);
            //GeoPoint p1 = center + allNodes[0].minExtent[3] * normal1;
            //GeoPoint p2 = center + allNodes[0].maxExtent[3] * normal2;
            //Plane pl1 = new Plane(p1, normal1);
            //Plane pl2 = new Plane(p2, normal2);
            //PlaneSurface ps1 = new PlaneSurface(pl1);
            //PlaneSurface ps2 = new PlaneSurface(pl2);
            //Face fc1 = Face.MakeFace(ps1, BoundingRect.UnitBoundingRect);
            //Face fc2 = Face.MakeFace(ps2, BoundingRect.UnitBoundingRect);
#endif
            return new EnumerateTSet(allNodes);
        }

    }

    class VertexCloudToPlanes : NDimTree<Vertex>
    {
        private GeoPoint center;
        public VertexCloudToPlanes(IEnumerable<Vertex> vertices, BoundingCube extent) : base(new double[] { -1, -1, -1, 0 }, new double[] { 1, 1, 1, extent.DiagonalLength / 2.0 }, true)
        {
            center = extent.GetCenter();
            node.Split(2);
            foreach (Vertex vtx in vertices)
            {
                AddObject(vtx);
            }
        }
        protected override bool GetExtent(Vertex obj, out double[] min, out double[] max)
        {   // will not be called, since the planes, represented by the nodes, are infinite
            throw new System.NotImplementedException();
        }
        protected override bool HitTest(Vertex v, double[] min, double[] max)
        {
            // min and max describe a family of planes defined by x*n=d, where min[] or max[] are {nx, ny, nz, d}
            // Since this family of planes is continuous it contains a plane which contains v if two of the 16 extreme planes
            // have the vertex v on different sides.
            double x = v.Position.x - center.x;
            double y = v.Position.y - center.y;
            double z = v.Position.z - center.z;
            int s0 = 0;
            for (int i = 0; i < 16; i++)
            {
                double nx, ny, nz, d;
                if ((i & 1) == 0) nx = min[0];
                else nx = max[0];
                if ((i & 2) == 0) ny = min[1];
                else ny = max[1];
                if ((i & 4) == 0) nz = min[2];
                else nz = max[2];
                if ((i & 8) == 0) d = min[3];
                else d = max[3];
                int s = Math.Sign(x * nx + y * ny + z * nz - d);
                if (i == 0) s0 = s;
                else if (s0 != s) return true; // different signs of distance to vertex from two extreme planes
            }
            return false; // all planes have the vertex on the same side. There is no plane in the family which contains the point.
        }
        protected override Node[] MakeSubNodes(Node node)
        {
            List<Node> res = new List<Node>();
            double[] midpoint = new double[dimension];
            for (int i = 0; i < midpoint.Length; i++)
            {
                midpoint[i] = (node.minExtent[i] + node.maxExtent[i]) / 2.0;
            }
            for (int i = 0; i < (1 << dimension); i++)
            {
                double[] minExtent = node.minExtent.Clone() as double[];
                double[] maxExtent = node.maxExtent.Clone() as double[];
                for (int j = 0; j < dimension; j++)
                {
                    if ((i & (1 << j)) != 0) minExtent[j] = midpoint[j];
                    else maxExtent[j] = midpoint[j];
                }
                BoundingCube ext = new BoundingCube(minExtent[0], maxExtent[0], minExtent[1], maxExtent[1], minExtent[2], maxExtent[2]);
                double d1 = ext.MinDistTo(GeoPoint.Origin);
                double d2 = ext.MaxDistTo(GeoPoint.Origin);
                if (d1 <= 1.0 && d2 >= 1.0) res.Add(new Node(minExtent, maxExtent));
            }
            return res.ToArray();
        }

        /// <summary>
        /// Gets the best set of vertices first, not so good sets later. 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<HashSet<Vertex>> GetBestPlanes()
        {
            return GetBestCluster(filter: delegate (Node n)
            {
                return n.objects.Count > 8;
            });
        }
    }

    class EdgeCloudToPlanes : NDimTree<Edge>
    {
        static int[,] edgeind = new int[,] // indizes of the 12 edges as binary, because they always go from min to max (0 to 1) and only change one bit
        {
            {0b000, 0b001 },
            {0b000, 0b010 },
            {0b000, 0b100 },
            {0b001, 0b101 },
            {0b001, 0b011 },
            {0b010, 0b011 },
            {0b010, 0b110 },
            {0b100, 0b101 },
            {0b100, 0b110 },
            {0b011, 0b111 },
            {0b110, 0b111 },
            {0b101, 0b111 }
        };
        private GeoPoint center;
        public EdgeCloudToPlanes(IEnumerable<Edge> edges, BoundingCube extent) : base(new double[] { -1, -1, -1, 0 }, new double[] { 1, 1, 1, extent.DiagonalLength / 2.0 }, true)
        {   // symmetric unit cube, when splitting, the inner cubes at 0 are eliminated
            center = extent.GetCenter();
            node.Split(2); // split for two levels, this makes less than 16*16 nodes, because the inner nodes which contain the origin are not created
            foreach (Edge edg in edges)
            {
                AddObject(edg);
            }
        }
        protected override bool GetExtent(Edge obj, out double[] min, out double[] max)
        {   // will not be called, since the planes, represented by the nodes, are infinite
            throw new System.NotImplementedException();
        }
        protected override bool HitTest(Edge e, double[] min, double[] max)
        {
#if DEBUG
            //GeoObjectList dbgl = new GeoObjectList();
            //GeoVector normal1 = new GeoVector(min[0], min[1], min[2]);
            //GeoVector normal2 = new GeoVector(max[0], max[1], max[2]);
            //GeoPoint p1 = center + min[3] * normal1;
            //GeoPoint p2 = center + max[3] * normal2;
            //if (!normal1.IsNullVector() && !normal2.IsNullVector())
            //{
            //    Plane pl1 = new Plane(p1, normal1);
            //    Plane pl2 = new Plane(p2, normal2);
            //    PlaneSurface ps1 = new PlaneSurface(pl1);
            //    PlaneSurface ps2 = new PlaneSurface(pl2);
            //    GeoPoint2D p2d1 = pl1.Project(e.Vertex1.Position);
            //    GeoPoint2D p2d2 = pl2.Project(e.Vertex1.Position);
            //    Face fc1 = Face.MakeFace(ps1, new BoundingRect(p2d1, 1, 1));
            //    Face fc2 = Face.MakeFace(ps2, new BoundingRect(p2d2, 1, 1));
            //    dbgl.Add(fc1);
            //    dbgl.Add(fc2);
            //}
            //dbgl.Add(e.Curve3D as IGeoObject);
#endif
            GeoVector edgdir = (e.Vertex2.Position - e.Vertex1.Position).Normalized;
            double x = edgdir.x;
            double y = edgdir.y;
            double z = edgdir.z;
            bool hit = false;
            int s0 = 0;
            for (int i = 0; i < 16; i++)
            {
                double nx, ny, nz, d;
                if ((i & 1) == 0) nx = min[0];
                else nx = max[0];
                if ((i & 2) == 0) ny = min[1];
                else ny = max[1];
                if ((i & 4) == 0) nz = min[2];
                else nz = max[2];
                if ((i & 8) == 0) d = min[3];
                else d = max[3];
                int s = Math.Sign(x * nx + y * ny + z * nz);
                if (i == 0) s0 = s;
                else if (s0 != s)
                {
                    hit = true;
                    break;
                }
            }
            if (!hit) return false; // done: no appropriate normal vector possible
            x = e.Vertex1.Position.x - center.x;
            y = e.Vertex1.Position.y - center.y;
            z = e.Vertex1.Position.z - center.z;
            s0 = 0;
            for (int i = 0; i < 16; i++)
            {
                double nx, ny, nz, d;
                if ((i & 1) == 0) nx = min[0];
                else nx = max[0];
                if ((i & 2) == 0) ny = min[1];
                else ny = max[1];
                if ((i & 4) == 0) nz = min[2];
                else nz = max[2];
                if ((i & 8) == 0) d = min[3];
                else d = max[3];
                int s = Math.Sign(x * nx + y * ny + z * nz - d);
                if (i == 0) s0 = s;
                else if (s0 != s) return true;
            }
            return false;

            //Unreachable code
            /*
            // min and max describe a family of planes defined by x*n=d, where min[] or max[] are {nx, ny, nz, d}
            // now ther is a cube spanned by the min-nx,ny,nz and max-nx,ny,nz
            // if this cube interferes with the plane normal to edgedir (and origin)
            double[] sp = new double[8];
            GeoVector[] nn = new GeoVector[8];
            for (int i = 0; i < 8; i++)
            {
                double nx, ny, nz;
                if ((i & 1) == 0) nx = min[0];
                else nx = max[0];
                if ((i & 2) == 0) ny = min[1];
                else ny = max[1];
                if ((i & 4) == 0) nz = min[2];
                else nz = max[2];
                nn[i] = new GeoVector(nx, ny, nz);
                sp[i] = edgdir * nn[i];
            }
            // now we have 12 edges of this cube:
            List<GeoVector> extremeNormals = new List<GeoVector>(4);
            for (int i = 0; i < 12; ++i)
            {
                if (Math.Sign(sp[edgeind[i, 0]]) != Math.Sign(sp[edgeind[i, 1]]))
                {
                    double r = sp[edgeind[i, 0]] / (sp[edgeind[i, 1]] - sp[edgeind[i, 0]]);
                    r = Math.Abs(r);
                    GeoVector en = (1 - r) * nn[edgeind[i, 0]] + r * nn[edgeind[i, 1]];
                    if (!en.IsNullVector()) extremeNormals.Add(en);
                }
            }
#if DEBUG
            //GeoObjectList dbglist = new GeoObjectList();
            //BoundingCube nbc = new BoundingCube(min[0], max[0], min[1], max[1], min[2], max[2]);
            //dbglist.Add(nbc.AsBox);
            //for (int i = 0; i < extremeNormals.Count; i++)
            //{
            //    if (!nbc.Contains(GeoPoint.Origin + extremeNormals[i], 1e-3))
            //    {

            //    }
            //    Line l = Line.TwoPoints(GeoPoint.Origin, GeoPoint.Origin + extremeNormals[i]);
            //    dbglist.Add(l);
            //}
            //for (int i = 0; i < nn.Length; i++)
            //{
            //    Line l = Line.TwoPoints(GeoPoint.Origin, GeoPoint.Origin + nn[i]);
            //    dbglist.Add(l);
            //}
            //dbglist.Add(e.Curve3D as IGeoObject);
            //PlaneSurface ps = new PlaneSurface(new Plane(GeoPoint.Origin, edgdir));
            //dbglist.Add(Face.MakeFace(ps, BoundingRect.UnitBoundingRect));

            //for (int i = 0; i < extremeNormals.Count; i++)
            //{
            //    GeoVector normale = extremeNormals[i];
            //    GeoPoint pe = center + min[3] * normale;
            //    Plane pl1 = new Plane(pe, normale);
            //    PlaneSurface ps1 = new PlaneSurface(pl1);
            //    GeoPoint2D p2d1 = pl1.Project(e.Vertex1.Position);
            //    Face fc1 = Face.MakeFace(ps1, new BoundingRect(p2d1, 1, 1));
            //    dbgl.Add(fc1);
            //    pe = center + max[3] * normale;
            //    pl1 = new Plane(pe, normale);
            //    ps1 = new PlaneSurface(pl1);
            //    p2d1 = pl1.Project(e.Vertex1.Position);
            //    fc1 = Face.MakeFace(ps1, new BoundingRect(p2d1, 1, 1));
            //    dbgl.Add(fc1);
            //}

#endif
            if (extremeNormals.Count == 0) return false; // min and max include no possible normal for a plane passing through the edge
            s0 = 0;
            x = e.Vertex1.Position.x - center.x;
            y = e.Vertex1.Position.y - center.y;
            z = e.Vertex1.Position.z - center.z;
            GeoVector testPoint = new GeoVector(x, y, z); // to be able to use scalar product
            for (int i = 0; i < extremeNormals.Count; i++)
            {
                double d;
                if ((i & 8) == 0) d = min[3];
                else d = max[3];
                int s = Math.Sign(extremeNormals[i] * testPoint - d);
                if (i == 0) s0 = s;
                else if (s0 != s) return true; // different signs of distance to vertex from two extreme planes
            }
            return false; // all planes have the vertex on the same side. There is no plane in the family which contains the point.
            */
        }
        protected override Node[] MakeSubNodes(Node node)
        {
            List<Node> res = new List<Node>();
            double[] midpoint = new double[dimension];
            for (int i = 0; i < midpoint.Length; i++)
            {
                midpoint[i] = (node.minExtent[i] + node.maxExtent[i]) / 2.0;
            }
            for (int i = 0; i < (1 << dimension); i++)
            {
                double[] minExtent = node.minExtent.Clone() as double[];
                double[] maxExtent = node.maxExtent.Clone() as double[];
                for (int j = 0; j < dimension; j++)
                {
                    if ((i & (1 << j)) != 0) minExtent[j] = midpoint[j];
                    else maxExtent[j] = midpoint[j];
                }
                BoundingCube ext = new BoundingCube(minExtent[0], maxExtent[0], minExtent[1], maxExtent[1], minExtent[2], maxExtent[2]);
                double d1 = ext.MinDistTo(GeoPoint.Origin);
                double d2 = ext.MaxDistTo(GeoPoint.Origin);
                if (d1 <= 1.0 && d2 >= 1.0) res.Add(new Node(minExtent, maxExtent));
            }
            return res.ToArray();
        }

        /// <summary>
        /// Gets the best set of vertices first, not so good sets later. 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<HashSet<Edge>> GetBestPlanes()
        {
#if DEBUG
            List<Node> allNodes = GetAllLeafs(null);
            allNodes.Sort(delegate (Node n1, Node n2)
            {
                int res = n2.deepth.CompareTo(n1.deepth);
                if (res == 0) res = n2.objects.Count.CompareTo(n1.objects.Count);
                return res;
            });
            GeoVector normal1 = new GeoVector(allNodes[193].minExtent[0], allNodes[193].minExtent[1], allNodes[193].minExtent[2]);
            GeoVector normal2 = new GeoVector(allNodes[193].maxExtent[0], allNodes[193].maxExtent[1], allNodes[193].maxExtent[2]);
            GeoPoint p1 = center + allNodes[193].minExtent[3] * normal1;
            GeoPoint p2 = center + allNodes[193].maxExtent[3] * normal2;
            Plane pl1 = new Plane(p1, normal1);
            Plane pl2 = new Plane(p2, normal2);
            PlaneSurface ps1 = new PlaneSurface(pl1);
            PlaneSurface ps2 = new PlaneSurface(pl2);
            Face fc1 = Face.MakeFace(ps1, BoundingRect.UnitBoundingRect);
            Face fc2 = Face.MakeFace(ps2, BoundingRect.UnitBoundingRect);
#endif
            return GetBestCluster(filter: delegate (Node n)
            {
                return n.objects.Count > 8;
            });
        }
    }

}
