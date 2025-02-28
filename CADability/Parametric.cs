using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CADability
{
    /// <summary>
    /// Modifies a shell according to different operations. The operations are OffsetFaces, MoveFaces, ModifyRadius, ModifyFilletRadius (and more to come).
    /// The modification takes two steps: first applying the operation to the surfaces of the faces, second recalculating the egdes and vertices.
    /// This pair of steps may be repeated with different operations. Finally you get the result by calling Result(). If the modification is not possible, the result bwill be null.
    /// 
    /// The parametric must preserve the "topology" of the Shell: each face, edge, vertex in the provided shell must exactely correspond to a face, edge, vertex in the modified shell.
    /// And the modification must be possible, i.e. holes must be inside the shell, no self intersection of the shell and more constraints.
    /// The individual operations may yield invalid shells, while the result of several such operations may be valid.
    /// </summary>
    public class Parametric
    {
        private readonly Shell clonedShell;  // the clone of the shell on which to work
        private readonly Dictionary<Face, Face> faceDict; // original to cloned faces
        private readonly Dictionary<Edge, Edge> edgeDict; // original to cloned edges
        private readonly Dictionary<Vertex, Vertex> vertexDict; // original to cloned vertices
        private readonly HashSet<Edge> edgesToRecalculate; // when all surface modifications are done, these edges have to be recalculated
        private readonly HashSet<Vertex> verticesToRecalculate; // these vertices need to be recalculated, because the underlying surfaces changed
        private readonly HashSet<Face> modifiedFaces; // these faces have been (partially) modified, do not modify twice
        private readonly HashSet<Face> constrainedFaces; // these faces must be adapted to their adjacent faces
        private readonly Dictionary<Edge, ICurve> tangentialEdgesModified; // tangential connection between two faces which both have been modified
        private HashSet<object> affectedObjects; // faces, edges, vertices that have been affected by this operation (faces only when surface has been modified)

        public Parametric(Shell shell)
        {
            if (!shell.State.HasFlag(Shell.ShellFlags.FacesCombined)) shell.CombineConnectedFaces();
            faceDict = new Dictionary<Face, Face>();
            edgeDict = new Dictionary<Edge, Edge>();
            vertexDict = new Dictionary<Vertex, Vertex>();
            clonedShell = shell.Clone(edgeDict, vertexDict, faceDict);
            clonedShell.Layer = shell.Layer;
            // clonedShell.CombineConnectedFaces(); clonedShell must have the same topology as shell (a face in clonedShell for each face in shell)
            edgesToRecalculate = new HashSet<Edge>();
            modifiedFaces = new HashSet<Face>();
            constrainedFaces = new HashSet<Face>();
            verticesToRecalculate = new HashSet<Vertex>();
            tangentialEdgesModified = new Dictionary<Edge, ICurve>();
            affectedObjects = new HashSet<object>();
        }
        /// <summary>
        /// Modify the thickness or gauge of a part (or the whole) shell. <paramref name="faces"/> already contains all the faces, which have to be modified or offset.
        /// there is no need to look at connected faces
        /// </summary>
        /// <param name="faces">The faces to modify (offset)</param>
        /// <param name="offset">the offset by which the faces are offset</param>
        internal void OffsetFaces(IEnumerable<Face> faces, double offset)
        {
            foreach (Face face in faces)
            {
                if (!faceDict.TryGetValue(face, out Face faceToMove)) faceToMove = face; // toMove may be from the original shell or from the cloned shell
                modifiedFaces.Add(faceToMove);
                ISurface offsetSurface = faceToMove.Surface.GetOffsetSurface(offset);
                foreach (Edge edge in faceToMove.Edges)
                {   // tangential edges must connect two of the provided faces, so they appear twice here
                    if (edge.IsTangentialEdge() && !tangentialEdgesModified.ContainsKey(edge))
                    {
                        if (faceToMove.Surface is PlaneSurface || !(edge.OtherFace(faceToMove).Surface is PlaneSurface))
                        {   // if the other face of this edge is planar, this other face will also be iterated and the tangential egde will then be set
                            ICurve2D c2d = edge.Curve2D(faceToMove);
                            ICurve c3d = offsetSurface.Make3dCurve(c2d);
                            if (!edge.Forward(face)) c3d.Reverse();
                            tangentialEdgesModified[edge] = c3d;
                        }
                    }
                    verticesToRecalculate.Add(edge.Vertex1);
                    verticesToRecalculate.Add(edge.Vertex2);
                    edgesToRecalculate.UnionWith(edge.Vertex1.AllEdges);
                    edgesToRecalculate.UnionWith(edge.Vertex2.AllEdges);
                }
                faceToMove.Surface = offsetSurface;
                affectedObjects.Add(faceToMove);
            }
        }

        /// <summary>
        /// Specifies the faces which should be moved or kept on its place. Faces to be moved kept on its place have a null-vector as the value 
        /// in the provided dictionary <paramref name="toMove"/>. Other faces must be moved according to the provided vector.
        /// Typically there are two vectors as values of <paramref name="toMove"/>: one is the null-vector, the other one the movement, or both
        /// are the half movement in opposite directions. The <paramref name="mainMovement"/> is used to keep faces in place, which have surfaces
        /// that are invariant in this direction.
        /// </summary>
        /// <param name="toMove"></param>
        /// <param name="mainMovement"></param>
        public void MoveFaces(Dictionary<Face, GeoVector> toMove, GeoVector mainMovement, bool moveConnected = false)
        {
            Dictionary<Face, GeoVector> nextLevel = new Dictionary<Face, GeoVector>();
            foreach (KeyValuePair<Face, GeoVector> item in toMove)
            {
                Face tm = item.Key;
                if (!faceDict.TryGetValue(tm, out Face faceToMove)) faceToMove = tm; // toMove may be from the original shell or from the cloned shell
                GeoVector offset = item.Value;
                ModOp move = ModOp.Translate(offset);
                if (modifiedFaces.Contains(faceToMove))
                {
                    constrainedFaces.Add(faceToMove);
                }
                else
                {
                    modifiedFaces.Add(faceToMove);
                    foreach (Edge edge in faceToMove.Edges)
                    {
                        Face otherFace = edge.OtherFace(faceToMove);
                        bool tangential = edge.IsTangentialEdge();
                        if (!modifiedFaces.Contains(otherFace) && tangential)
                        {
                            tangentialEdgesModified[edge] = edge.Curve3D.CloneModified(move); // these edges play a role in calculating the new vertices
                                                                                              // the edges will be recalculated in "Result()", but here we need the already modified curve for intersection purposes
                            if (!otherFace.Surface.IsExtruded(mainMovement))
                            {
                                if (nextLevel.ContainsKey(otherFace) && nextLevel[otherFace] != offset)
                                {   // there are two different requirements to move this face. This face may not propagate its movement and must be modified
                                    // according to the constraints by the surrounding faces
                                    nextLevel.Remove(otherFace);
                                    constrainedFaces.Add(otherFace);
                                }
                                else
                                {
                                    nextLevel[otherFace] = offset; // only if offset is not the extrusion direction of the otherFace
                                }
                            }
                        }
                        else if (tangential && !tangentialEdgesModified.ContainsKey(edge))
                        {
                            tangentialEdgesModified[edge] = edge.Curve3D.CloneModified(move); // these edges play a role in calculating the new vertices
                        }
                        else if (!tangential && moveConnected && !modifiedFaces.Contains(otherFace) && !otherFace.Surface.IsExtruded(mainMovement))
                        {   // should we move nontangential connected faces or not. both cases make sense. Now decided by parameter
                            nextLevel[otherFace] = offset; // only if offset is not the extrusion direction of the otherFace
                        }
                        verticesToRecalculate.Add(edge.Vertex1);
                        verticesToRecalculate.Add(edge.Vertex2);
                        edgesToRecalculate.UnionWith(edge.Vertex1.AllEdges);
                        edgesToRecalculate.UnionWith(edge.Vertex2.AllEdges);
                    }
                    if (moveConnected)
                    {
                        foreach (Vertex vertex in faceToMove.Vertices)
                        {   // there might be a face which only shares a vertex with faceToMove but not an edge (typically when there are 4 or more edges in a vertex)
                            HashSet<Face> facesToTest = vertex.InvolvedFaces;
                            facesToTest.ExceptWith(modifiedFaces);
                            facesToTest.ExceptWith(constrainedFaces);
                            facesToTest.ExceptWith(nextLevel.Keys);
                            foreach (Face face in facesToTest)
                            {
                                if (!(new HashSet<Edge>(face.AllEdges)).Intersect(faceToMove.AllEdges).Any())
                                {   // this face has a common vertex with faceToMove, but no common edge
                                    if (!face.Surface.IsExtruded(mainMovement)) nextLevel[face] = offset;
                                }
                            }
                        }
                    }
                    if (!offset.IsNullVector()) faceToMove.ModifySurfaceOnly(move); // move after all tangential test have been made, otherwise the tangential tests fail
                }
            }
            foreach (Face item in modifiedFaces)
            {   // remove those faces from nextLevel and constrainedFaces which already have been modified
                nextLevel.Remove(item);
                constrainedFaces.Remove(item);
            }
            if (nextLevel.Any()) MoveFaces(nextLevel, mainMovement, moveConnected);
        }
        /// <summary>
        /// For faces on <see cref="CylindricalSurface"/>, <see cref="ToroidalSurface"/>, (maybe not <see cref="SphericalSurface"/> maybe some swept curve surface)
        /// this face should change its radius resp. <see cref="ToroidalSurface.MinorRadius"/>.
        /// <para>When it is tangential in the direction of the circle to other faces (like a rounded edge is tangential to the faces of the original edge),
        /// then its position will be changed, so that it is still tangential to these faces (i.e. changing the radius of a rounded edge).</para> 
        /// <para>When it is tangential in the other direction (e.g. a cylinder followed by a torus segment, in a pipe or at multiple rounded edges) 
        /// these tangential faces also change their radius and move to the same axis as their predecessor.</para>
        /// </summary>
        /// <param name="toModify">a face from the original shell</param>
        /// <param name="newRadius">the new radius of the surface</param>
        public bool ModifyRadius(IEnumerable<Face> toModify, double newRadius)
        {
            if (newRadius <= 0.0) return false;
            HashSet<Face> sameSurfaceFaces = new HashSet<Face>();
            HashSet<Edge> sameSurfaceEdges = new HashSet<Edge>();
            HashSet<Face> toModifySet = new HashSet<Face>(toModify);
            HashSet<Face> alreadyModified = new HashSet<Face>();
            foreach (Face tm in toModifySet)
            {
                if (alreadyModified.Contains(tm)) continue;
                if (!faceDict.TryGetValue(tm, out Face faceToModify)) return false; // must be a face from the original shell
                CollectSameSurfaceFaces(faceToModify, sameSurfaceFaces, sameSurfaceEdges);
                if (sameSurfaceFaces.Count >= 1)
                {
                    // this is probably a split full cylinder or torus. we ignore the case that a combination of two parts with the same surface is tangential to other surface
                    // in this case the position remains unchanged
                    foreach (Face face in sameSurfaceFaces)
                    {   // set all faces with identical surfaces the new radius
                        ISurface modifiedSurface = null;
                        if (face.Surface is ICylinder cyl)
                        {
                            modifiedSurface = face.Surface.Clone();
                            (modifiedSurface as ICylinder).Radius = newRadius;
                        }
                        else if (face.Surface is ToroidalSurface tor)
                        {
                            modifiedSurface = new ToroidalSurface(tor.Location, tor.XAxis.Normalized, tor.YAxis.Normalized, tor.ZAxis.Normalized, tor.XAxis.Length, newRadius);
                        }
                        else if (face.Surface is SphericalSurface sph)
                        {
                            modifiedSurface = new SphericalSurface(sph.Location, newRadius * sph.XAxis.Normalized, newRadius * sph.YAxis.Normalized, newRadius * sph.ZAxis.Normalized);
                        }
                        face.Surface = modifiedSurface;
                        affectedObjects.Add(face);
                        verticesToRecalculate.UnionWith(face.Vertices);
                        modifiedFaces.Add(face);
                        alreadyModified.Add(face);
                    }
                    foreach (Vertex vtx in verticesToRecalculate)
                    {
                        edgesToRecalculate.UnionWith(vtx.AllEdges);
                    }
                    foreach (Edge edge in sameSurfaceEdges)
                    {   // the new tangential edges are easy to calculate here
                        if (edge.Forward(edge.PrimaryFace)) tangentialEdgesModified[edge] = edge.PrimaryFace.Surface.Make3dCurve(edge.PrimaryCurve2D);
                        else tangentialEdgesModified[edge] = edge.SecondaryFace.Surface.Make3dCurve(edge.SecondaryCurve2D);
                    }
                    // missing: follow the pipe!
                }
            }
            return true;
            //else if (faceToModify.Surface is ISurfaceOfArcExtrusion extrusion)
            //{   // there are no other faces with the same surface, so we have to check, whether this face is tangential to some other faces, whether it is a fillet
            //    // there can be tangential edges between the fillet and the two faces of the original edge or to the previous or next fillet
            //    // there cannot be a fillet composed of two parts with the same surface, because these would be combined by Shell.CombineConnectedFaces

            //    HashSet<Face> lengthwayTangential = new HashSet<Face>(); // the two faces that this fillet rounds
            //    HashSet<Edge> crosswayTangential = new HashSet<Edge>(); // the following or previous fillet
            //    foreach (Edge edge in faceToModify.Edges)
            //    {
            //        Face otherFace = edge.OtherFace(faceToModify);
            //        if (edge.IsTangentialEdge())
            //        {
            //            if (edge.Curve2D(faceToModify).DirectionAt(0.5).IsMoreHorizontal != extrusion.ExtrusionDirectionIsV) lengthwayTangential.Add(otherFace);
            //            else crosswayTangential.Add(edge);
            //        }
            //    }
            //    if (lengthwayTangential.Count == 2)
            //    {
            //        // a cylinder or torus or swept curve as a fillet to two surfaces:
            //        // 1. find the new axis
            //        ICurve axis = extrusion.Axis(faceToModify.Domain); // a line for a cylinder, an arc for a torus, some 3d curve for a swept curve
            //        Face[] t = lengthwayTangential.ToArray();
            //        GeoPoint mp = axis.PointAt(0.5);
            //        //double d = t[0].Surface.GetDistance(mp); // this should be the current radius, unfortunately GetDistance is the absolute value
            //        GeoPoint2D fp = t[0].Surface.PositionOf(mp);
            //        double par = Geometry.LinePar(t[0].Surface.PointAt(fp), t[0].Surface.GetNormal(fp), mp);
            //        double offset;
            //        if (par > 0) offset = newRadius;
            //        else offset = -newRadius;
            //        ISurface surface0 = t[0].Surface.GetOffsetSurface(offset);
            //        ISurface surface1 = t[1].Surface.GetOffsetSurface(offset);
            //        ICurve[] cvs = surface0.Intersect(t[0].Domain, surface1, t[1].Domain); // this should yield the new axis
            //        ICurve newAxis = Hlp.GetClosest(cvs, crv => crv.DistanceTo(mp));
            //        if (newAxis != null)
            //        {
            //            ISurfaceOfArcExtrusion modifiedSurface = faceToModify.Surface.Clone() as ISurfaceOfArcExtrusion;
            //            modifiedSurface.ModifyAxis(newAxis.PointAt(newAxis.PositionOf(newAxis.PointAt(0.5))));
            //            modifiedSurface.Radius = newRadius;
            //            faceToModify.Surface = modifiedSurface as ISurface;
            //            verticesToRecalculate.UnionWith(faceToModify.Vertices);
            //            foreach (Vertex vtx in verticesToRecalculate)
            //            {
            //                edgesToRecalculate.UnionWith(vtx.AllEdges);
            //            }
            //            modifiedFaces.Add(faceToModify);
            //            // this modified face is tangential to t[0] and t[1]. The edges between this faceToModify and t[0] resp. t[1] need to be recalculated
            //            // in order to have a curve for recalculating the vertices in Result()
            //            foreach (Edge edg in faceToModify.Edges)
            //            {
            //                for (int i = 0; i < 2; i++)
            //                {
            //                    if (edg.OtherFace(faceToModify) == t[i])
            //                    {
            //                        ICurve[] crvs = faceToModify.Surface.Intersect(faceToModify.Domain, t[i].Surface, t[i].Domain);
            //                        ICurve crv = Hlp.GetClosest(crvs, c => c.DistanceTo(edg.Vertex1.Position) + c.DistanceTo(edg.Vertex2.Position));
            //                        if (crv != null) // which must be the case, because the surfaces are tangential
            //                        {
            //                            edg.Curve3D = crv;
            //                            tangentialEdgesModified[edg] = crv;
            //                        }
            //                    }

            //                }
            //            }
            //            // follow the crossway tangential faces
            //            foreach (Edge edge in crosswayTangential)
            //            {
            //                followCrosswayTangential(edge, axis, newRadius);
            //            }
            //            return true;
            //        }
            //    }
            //}
            //return false;
        }

        private void followCrosswayTangential(Edge edge, ICurve axis, double newRadius)
        {   // one of the faces of edge has been modified, the other face (which should be a ISurfaceOfExtrusion-face) must now be adapted to the correct axis
            Face faceToModify = null;
            if (!modifiedFaces.Contains(edge.PrimaryFace)) faceToModify = edge.PrimaryFace;
            if (!modifiedFaces.Contains(edge.SecondaryFace)) faceToModify = edge.SecondaryFace; // can only be one of the faces
            if (faceToModify is ISurfaceOfArcExtrusion extrusion) // which it should be
            {
                ISurfaceOfArcExtrusion modifiedSurface = faceToModify.Surface.Clone() as ISurfaceOfArcExtrusion;
                modifiedSurface.ModifyAxis(axis.PointAt(0.5));
                modifiedSurface.Radius = newRadius;
                faceToModify.Surface = modifiedSurface as ISurface;
                affectedObjects.Add(faceToModify);
                verticesToRecalculate.UnionWith(faceToModify.Vertices);
                foreach (Vertex vtx in verticesToRecalculate)
                {
                    edgesToRecalculate.UnionWith(vtx.AllEdges);
                }
                modifiedFaces.Add(faceToModify);
                // follow the crossway tangential faces
                foreach (Edge edg in faceToModify.Edges)
                {
                    Face otherFace = edge.OtherFace(faceToModify);
                    if (edg.IsTangentialEdge())
                    {
                        if (edg.Curve2D(faceToModify).DirectionAt(0.5).IsMoreHorizontal == extrusion.ExtrusionDirectionIsV && otherFace.Surface is ISurfaceOfArcExtrusion arcExtrusion)
                        {
                            followCrosswayTangential(edg, arcExtrusion.Axis(otherFace.Domain), newRadius);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Find all faces that share a common surface
        /// </summary>
        /// <param name="face">start with this face</param>
        /// <param name="sameSurfaceFaces">the faces found</param>
        /// <param name="sameSurfaceEdges">the edges, that connect these faces</param>
        private void CollectSameSurfaceFaces(Face face, HashSet<Face> sameSurfaceFaces, HashSet<Edge> sameSurfaceEdges)
        {
            if (sameSurfaceFaces.Contains(face)) return; // already tested
            sameSurfaceFaces.Add(face);
            foreach (Edge edge in face.Edges)
            {
                Face otherFace = edge.OtherFace(face);
                if (edge.IsTangentialEdge())
                {
                    if (otherFace.Surface.SameGeometry(otherFace.Domain, face.Surface, face.Domain, Precision.eps, out _))
                    {
                        sameSurfaceEdges.Add(edge);
                        CollectSameSurfaceFaces(otherFace, sameSurfaceFaces, sameSurfaceEdges);
                    }
                }
            }
        }
        /// <summary>
        /// A fillet is to be modified with a new radius. The parameter <paramref name="toModify"/> contains all relevant faces, which are either
        /// faces where the surface is a <see cref="ISurfaceOfArcExtrusion"/> or a <see cref="SphericalSurface"/>. There is no need to follow
        /// these faces, the caller is responsible for this.
        /// </summary>
        /// <param name="toModify"></param>
        /// <param name="newRadius"></param>
        /// <returns>true, if possible (but not guaranteed to be possible)</returns>
        public bool ModifyFilletRadius(Face[] toModify, double newRadius)
        {
            HashSet<Face> toModifySet = new HashSet<Face>();
            for (int i = 0; i < toModify.Length; i++)
            {
                toModifySet.Add(faceDict[toModify[i]]);
            }
            foreach (Face faceToModify in toModifySet)
            {
                if (faceToModify.Surface is ISurfaceOfArcExtrusion extrusion)
                {

                    ICurve axis = extrusion.Axis(faceToModify.Domain); // a line for a cylinder, an arc for a torus, some 3d curve for a swept curve
                    HashSet<Face> lengthwayTangential = new HashSet<Face>(); // the two faces that this fillet rounds
                    HashSet<Edge> crosswayTangential = new HashSet<Edge>(); // the following or previous fillet
                    foreach (Edge edge in faceToModify.Edges)
                    {
                        Face otherFace = edge.OtherFace(faceToModify);
                        if (edge.IsTangentialEdge())
                        {
                            if (edge.Curve2D(faceToModify).DirectionAt(0.5).IsMoreHorizontal != extrusion.ExtrusionDirectionIsV) lengthwayTangential.Add(otherFace);
                            else crosswayTangential.Add(edge);
                        }
                    }
                    if (lengthwayTangential.Count != 2) continue; // there must be two other faces tangential in the extrusion direction
                    Face[] t = lengthwayTangential.ToArray();
                    GeoPoint mp = axis.PointAt(0.5);
                    //double d = t[0].Surface.GetDistance(mp); // this should be the current radius, unfortunately GetDistance is the absolute value
                    GeoPoint2D fp = t[0].Surface.PositionOf(mp);
                    double par = Geometry.LinePar(t[0].Surface.PointAt(fp), t[0].Surface.GetNormal(fp), mp);
                    double offset;
                    if (par > 0) offset = newRadius;
                    else offset = -newRadius;
                    ISurface surface0 = t[0].Surface.GetOffsetSurface(offset);
                    ISurface surface1 = t[1].Surface.GetOffsetSurface(offset);
                    ICurve[] cvs = surface0.Intersect(t[0].Domain, surface1, t[1].Domain); // this should yield the new axis
                    ICurve newAxis = Hlp.GetClosest(cvs, crv => crv.DistanceTo(mp));
                    if (newAxis != null)
                    {
                        ISurfaceOfArcExtrusion modifiedSurface = faceToModify.Surface.Clone() as ISurfaceOfArcExtrusion;
                        modifiedSurface.ModifyAxis(newAxis.PointAt(newAxis.PositionOf(newAxis.PointAt(0.5))));
                        modifiedSurface.Radius = newRadius;
                        faceToModify.Surface = modifiedSurface as ISurface;
                        affectedObjects.Add(faceToModify);
                        verticesToRecalculate.UnionWith(faceToModify.Vertices);
                        foreach (Vertex vtx in verticesToRecalculate)
                        {
                            edgesToRecalculate.UnionWith(vtx.AllEdges);
                        }
                        modifiedFaces.Add(faceToModify);
                        // this modified face is tangential to t[0] and t[1]. The edges between this faceToModify and t[0] resp. t[1] need to be recalculated
                        // in order to have a curve for recalculating the vertices in Result()
                        foreach (Edge edg in faceToModify.Edges)
                        {
                            for (int i = 0; i < 2; i++)
                            {
                                if (edg.OtherFace(faceToModify) == t[i])
                                {
                                    ICurve[] crvs = faceToModify.Surface.Intersect(faceToModify.Domain, t[i].Surface, t[i].Domain);
                                    ICurve crv = Hlp.GetClosest(crvs, c => c.DistanceTo(edg.Vertex1.Position) + c.DistanceTo(edg.Vertex2.Position));
                                    if (crv != null) // which must be the case, because the surfaces are tangential
                                    {
                                        if (!edg.Curve3D.SameGeometry(crv, Precision.eps)) affectedObjects.Add(edg);
                                        CopyAttributes(edg.Curve3D, crv);
                                        edg.Curve3D = crv;
                                        tangentialEdgesModified[edg] = crv;
                                    }
                                }

                            }
                        }
                        foreach (Edge edg in crosswayTangential)
                        {
                            if (toModifySet.Contains(edg.OtherFace(faceToModify)) && edg.IsTangentialEdge() && !tangentialEdgesModified.ContainsKey(edg))
                            {   // this is a tangential edge between two adjacent parts of the fillet
                                ICurve crv = faceToModify.Surface.Make3dCurve(edg.Curve2D(faceToModify));
                                tangentialEdgesModified[edg] = crv;
                            }
                        }
                    }
                }
                if (faceToModify.Surface is CylindricalSurfaceNP cyl)
                {   // this is similar to ISurfaceOfArcExtrusion, but it isn't an extrusion in u or v
                    ICurve axis = (cyl as ICylinder).Axis.Clip(faceToModify.GetExtent(0.0)); // the axis as a clipped line
                    HashSet<Face> lengthwayTangential = new HashSet<Face>(); // the two faces that this fillet rounds
                    HashSet<Edge> crosswayTangential = new HashSet<Edge>(); // the following or previous fillet
                    foreach (Edge edge in faceToModify.Edges)
                    {
                        Face otherFace = edge.OtherFace(faceToModify);
                        if (edge.IsTangentialEdge())
                        {
                            if (edge.Curve2D(faceToModify) is Line2D) lengthwayTangential.Add(otherFace);
                            else crosswayTangential.Add(edge);
                        }
                    }
                    // this cylinder may or may not have length-way tangential curves 
                    CylindricalSurfaceNP modifiedSurface = faceToModify.Surface.Clone() as CylindricalSurfaceNP;
                    (modifiedSurface as ICylinder).Radius = newRadius;
                    Face[] t = lengthwayTangential.ToArray();
                    GeoPoint mp = axis.PointAt(0.5);
                    //double d = t[0].Surface.GetDistance(mp); // this should be the current radius, unfortunately GetDistance is the absolute value
                    GeoPoint2D fp = t[0].Surface.PositionOf(mp);
                    double par = Geometry.LinePar(t[0].Surface.PointAt(fp), t[0].Surface.GetNormal(fp), mp);
                    double offset;
                    if (par > 0) offset = newRadius;
                    else offset = -newRadius;
                    ISurface surface0 = t[0].Surface.GetOffsetSurface(offset);
                    ISurface surface1 = t[1].Surface.GetOffsetSurface(offset);
                    ICurve[] cvs = surface0.Intersect(t[0].Domain, surface1, t[1].Domain); // this should yield the new axis
                    ICurve newAxis = Hlp.GetClosest(cvs, crv => crv.DistanceTo(mp));
                    if (newAxis != null)
                    {
                        (modifiedSurface as ICylinder).Axis = new Axis(Geometry.DropPL((modifiedSurface as ICylinder).Axis.Location, newAxis.StartPoint, newAxis.StartDirection), newAxis.StartDirection);

                        faceToModify.Surface = modifiedSurface as ISurface;
                        affectedObjects.Add(faceToModify);
                        verticesToRecalculate.UnionWith(faceToModify.Vertices);
                        foreach (Vertex vtx in verticesToRecalculate)
                        {
                            edgesToRecalculate.UnionWith(vtx.AllEdges);
                        }
                        modifiedFaces.Add(faceToModify);

                        // this modified face is tangential to the lengthwayTangential faces (if any). These need to be recalculated
                        // in order to have a curve for recalculating the vertices in Result()
                        for (int i = 0; i < t.Length; i++)
                        {
                            foreach (Edge edg in faceToModify.Edges)
                            {
                                if (edg.OtherFace(faceToModify) == t[i])
                                {
                                    if (toModifySet.Contains(t[i]))
                                    {   // a tangential line connecting this cylinder with another face, that is also to modify
                                        // the u/v system of this cylinder has changed
                                        ICurve crv = Line.TwoPoints(modifiedSurface.PointAt(modifiedSurface.PositionOf(edg.Curve3D.StartPoint)), modifiedSurface.PointAt(modifiedSurface.PositionOf(edg.Curve3D.EndPoint)));
                                        if (!edg.Curve3D.SameGeometry(crv, Precision.eps)) affectedObjects.Add(edg);
                                        CopyAttributes(edg.Curve3D, crv);
                                        edg.Curve3D = crv;
                                        tangentialEdgesModified[edg] = crv;
                                    }
                                    else
                                    {
                                        ICurve[] crvs = faceToModify.Surface.Intersect(faceToModify.Domain, t[i].Surface, t[i].Domain);
                                        ICurve crv = Hlp.GetClosest(crvs, c => c.DistanceTo(edg.Vertex1.Position) + c.DistanceTo(edg.Vertex2.Position));
                                        if (crv != null) // which must be the case, because the surfaces are tangential
                                        {
                                            if (!edg.Curve3D.SameGeometry(crv, Precision.eps)) affectedObjects.Add(edg);
                                            CopyAttributes(edg.Curve3D, crv);
                                            edg.Curve3D = crv;
                                            tangentialEdgesModified[edg] = crv;
                                        }
                                    }
                                }
                            }
                        }
                        foreach (Edge edg in crosswayTangential)
                        {
                            if (toModifySet.Contains(edg.OtherFace(faceToModify)) && edg.IsTangentialEdge() && !tangentialEdgesModified.ContainsKey(edg))
                            {   // this is a tangential edge between two adjacent parts of the fillet
                                ICurve crv = faceToModify.Surface.Make3dCurve(edg.Curve2D(faceToModify));
                                tangentialEdgesModified[edg] = crv;
                            }
                        }
                    }
                }
                else if (faceToModify.Surface is SphericalSurface sph)
                {
                    // there must be 3 tangential faces with ISurfaceOfArcExtrusion surfaces
                }
            }
            return modifiedFaces.Count > 0;
        }
        private void ModifyConstrainedFaces()
        {
            while (constrainedFaces.Any())
            {
                Face toModify = constrainedFaces.First();
                HashSet<Edge> constrainedEdges = new HashSet<Edge>(toModify.AllEdgesSet.Intersect(tangentialEdgesModified.Keys));
                HashSet<Face> otherFaces = new HashSet<Face>();
                foreach (Edge edge in constrainedEdges)
                {
                    Face other = edge.OtherFace(toModify);
                    if (!constrainedFaces.Contains(other)) otherFaces.Add(other);
                }
                List<ISurface> otherSurfaces = new List<ISurface>();
                foreach (Face face in otherFaces)
                {
                    otherSurfaces.Add(face.Surface);
                }
                constrainedFaces.Remove(toModify);
                ISurface modified = Surfaces.ModifyTangential(toModify.Surface, otherSurfaces);
                if (modified != null)
                {
                    toModify.Surface = modified;
                    affectedObjects.Add(toModify);
                    verticesToRecalculate.UnionWith(toModify.Vertices);
                    edgesToRecalculate.UnionWith(toModify.AllEdges);
                }
            }
        }
        public Shell Result()
        {
            foreach (Face face in modifiedFaces)
            {   // checks the topology of the bounds in 2d: no intersection or overlapping, holes inside the outline
                if (!face.Check2DBounds())
                {
                    return null;
                }
            }
            // should do more checks here: e.g. self intersection
            if (clonedShell.CheckConsistency())
            {
                return clonedShell;
            }
            return null;
        }
        public bool Apply()
        {
            try
            {
                ModifyConstrainedFaces();
                HashSet<Face> involvedFaces = new HashSet<Face>();
                HashSet<Vertex> irrelevantVertices = new HashSet<Vertex>();
                foreach (Vertex vertex in verticesToRecalculate)
                {
                    bool done = false;
                    // first lets see, whether two tangential surfaces are involved with this vertex. Then we need to intersect the tangential edge with the third surface
                    HashSet<Edge> tm = new HashSet<Edge>(vertex.AllEdges);
                    IEnumerable<Edge> toTest;
                    toTest = tm.Intersect(tangentialEdgesModified.Keys);
                    if (!toTest.Any()) toTest = tm; // preferably use edges from tangentialEdgesModified, if there are none use all edges
                    foreach (Edge edge in toTest) // there are at least three
                    {
                        ICurve crv; // either a already moved tangential edge or an unmodified edge
                        if (!tangentialEdgesModified.TryGetValue(edge, out crv) && edge.IsTangentialEdge()) crv = edge.Curve3D;
                        if (crv != null)
                        {   // this vertex is the start or end vertex of a tangential edge. We cannot use the intersection of 3 surfaces to calculate its new position
                            foreach (Face face in vertex.InvolvedFaces)
                            {
                                if (face != edge.PrimaryFace && face != edge.SecondaryFace)
                                {   // this is the face (or very rare one of the faces) that is not part of the tangential edges
                                    face.Surface.Intersect(crv, face.Domain, out GeoPoint[] ips, out GeoPoint2D[] uvOnSurface, out double[] uOnCurve); // the domain should only be used for periodic adjustment!
                                    if (ips.Length == 0)
                                    {
                                        BoundingCube ext = face.GetBoundingCube();
                                        if (crv.Extend(ext.DiagonalLength, ext.DiagonalLength))
                                        {   // a second try with the extended curve
                                            face.Surface.Intersect(crv, face.Domain, out ips, out uvOnSurface, out uOnCurve);
                                        }
                                    }
                                    if (ips.Length > 0) // there should always be such an intersection point, otherwise there is no way to modify the shell
                                    {
                                        if (ips.Length > 1)
                                        {
                                            // find best point: closer to startpoint or endpoint of crv, depending on vertex, set it on ips[0]
                                            // the following was sometimes incorrect, we use the old vertex position to find the closest candidate
                                            //GeoPoint testPoint = GeoPoint.Invalid;
                                            //if (edge.Vertex1 == vertex) testPoint = crv.StartPoint;
                                            //else if (edge.Vertex2 == vertex) testPoint = crv.EndPoint;
                                            //if (testPoint.IsValid)
                                            //{
                                            //    ips[0] = Hlp.GetClosest(ips, p => p | testPoint);
                                            //}
                                            ips[0] = Hlp.GetClosest(ips, p => p | vertex.Position);
                                        }
                                        if (!Precision.IsEqual(ips[0], vertex.Position)) affectedObjects.Add(vertex);
                                        vertex.Position = ips[0];
                                        done = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (done) break;
                    }
                    Face[] faces = vertex.InvolvedFaces.ToArray();
                    if (!done && faces.Length >= 3)
                    {   // here we would need to be more selective
                        GeoPoint ip = vertex.Position;
                        // maybe two faces have an identical surface (a split periodic surface)
                        double mindist = double.MaxValue;
                        foreach (Edge edg in vertex.AllEdges)
                        {
                            if (edg.PrimaryFace.Surface.SameGeometry(edg.PrimaryFace.Domain, edg.SecondaryFace.Surface, edg.SecondaryFace.Domain, Precision.eps, out ModOp2D dumy))
                            {
                                HashSet<Face> vertexFaces = new HashSet<Face>(vertex.InvolvedFaces);
                                vertexFaces.Remove(edg.PrimaryFace);
                                vertexFaces.Remove(edg.SecondaryFace);
                                if (vertexFaces.Count > 0)
                                {
                                    Face fc = vertexFaces.First();
                                    fc.Surface.Intersect(edg.Curve3D, fc.Domain, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve);
                                    for (int i = 0; i < ips.Length; i++)
                                    {
                                        double d = ips[i] | vertex.Position;
                                        if (d < mindist)
                                        {
                                            mindist = d;
                                            ip = ips[i];
                                        }
                                    }
                                }
                            }
                        }
                        if (mindist != double.MaxValue)
                        {
                            if (!Precision.IsEqual(ip, vertex.Position)) affectedObjects.Add(vertex);
                            vertex.Position = ip;
                            done = true;
                        }
                        if (!done)
                        {
                            if (Surfaces.IntersectThreeSurfaces(faces[0].Surface, faces[0].Domain, faces[1].Surface, faces[1].Domain,
                                faces[2].Surface, faces[2].Domain, ref ip, out GeoPoint2D uv0, out GeoPoint2D uv1, out GeoPoint2D uv2))
                            {
                                if (!Precision.IsEqual(ip, vertex.Position)) affectedObjects.Add(vertex);
                                vertex.Position = ip;
                                done = true;
                            }
                        }
                    }
                    if (!done && faces.Length == 2)
                    {
                        // here the vertex position is not relevant: a vertex on only two faces may reside anywhere on the edges
                        irrelevantVertices.Add(vertex);
                        // need to implement the edge creation of edges with only two surfaces (open or closed, but open can be removed)
                        done = true;
                    }
                    if (!done)
                    {
                        // return false; // position of vertex could not be calculated
                        // now we go on, maybe the next operation yields correct vertices again
                    }
                }
                foreach (Edge edge in edgesToRecalculate)
                {
                    List<GeoPoint> seeds = new List<GeoPoint>(); // for the intersection
                    if (!irrelevantVertices.Contains(edge.Vertex1)) seeds.Add(edge.Vertex1.Position); // the vertices have already their new positions, the edges must start and end here
                    if (!irrelevantVertices.Contains(edge.Vertex2)) seeds.Add(edge.Vertex2.Position);
                    // remove duplicate seeds
                    if (seeds.Count == 2 && Precision.IsEqual(seeds[0], seeds[1])) seeds.RemoveAt(1);
                    ICurve crv = null;
                    if (edge.PrimaryFace.Surface.SameGeometry(edge.PrimaryFace.Domain, edge.SecondaryFace.Surface, edge.SecondaryFace.Domain, Precision.eps, out ModOp2D firstToSecond))
                    {   // this is probably a seam of two periodic parts with the same surface
                        GeoPoint2D sp = edge.PrimaryFace.Surface.PositionOf(seeds[0]);
                        GeoPoint2D ep = edge.PrimaryFace.Surface.PositionOf(seeds[1]);
                        SurfaceHelper.AdjustPeriodic(edge.PrimaryFace.Surface, edge.PrimaryFace.Domain, ref sp);
                        SurfaceHelper.AdjustPeriodic(edge.PrimaryFace.Surface, edge.PrimaryFace.Domain, ref ep);
                        Line2D l2d = new Line2D(sp, ep);
                        crv = edge.PrimaryFace.Surface.Make3dCurve(l2d);
                    }
                    if (seeds.Count == 0)
                    {   // the vertex (vertices) of this edge could not be recalculated. It is probably a closed curve, so we only have two surfaces for the vertices involved.
                        // if the curve is not closed Face.CombineConnectedSameSurfaceEdges did not work correctly
                        ICurve[] crvs = Surfaces.Intersect(edge.PrimaryFace.Surface, edge.SecondaryFace.Surface);
                        crv = null;
                        if (crvs != null)
                        {
                            crv = Hlp.GetClosest(crvs, cv => cv.DistanceTo(edge.Vertex1.Position));
                        }
                        // here we might have an orientation problem. If the curve is closed, we project it into one of the surfaces and test, whether it has the same orientation
                        // in the 2d space
                        if (crv != null && crv.IsClosed)
                        {
                            ICurve2D c2d = edge.PrimaryFace.Surface.GetProjectedCurve(crv, 0.0);
                            double a = c2d.GetArea();
                            double b = edge.Curve2D(edge.PrimaryFace).GetArea();
                            if (Math.Sign(a) != Math.Sign(b)) crv.Reverse();
                        }
                    }
                    if (crv == null && seeds.Count == 2)
                    {   // the seeds mus lie in the surfaces, if not the face should have been moved
                        crv = Surfaces.Intersect(edge.PrimaryFace.Surface, edge.PrimaryFace.Domain, edge.SecondaryFace.Surface, edge.SecondaryFace.Domain, seeds);
                    }
                    if (crv != null)
                    {   // if we have start and end point, then use these to confirm direction, otherwise it was a closed curve and is already oriented correctly
                        if (seeds.Count == 2)
                        {
                            if ((crv.StartPoint | seeds[0]) > (crv.StartPoint | seeds[1])) crv.Reverse();
                            if (crv is InterpolatedDualSurfaceCurve)
                            { // what to do here?
                            }
                            else
                            {
                                crv.StartPoint = seeds[0];
                                crv.EndPoint = seeds[1];
                            }
                        }
                        if (!edge.Curve3D.SameGeometry(crv, Precision.eps)) affectedObjects.Add(edge);
                        CopyAttributes(edge.Curve3D, crv);
                        edge.Curve3D = crv;
                        edge.PrimaryCurve2D = edge.PrimaryFace.Surface.GetProjectedCurve(crv, 0.0);
                        if (!edge.Forward(edge.PrimaryFace)) edge.PrimaryCurve2D.Reverse();
                        SurfaceHelper.AdjustPeriodic(edge.PrimaryFace.Surface, edge.PrimaryFace.Domain, edge.PrimaryCurve2D);
                        edge.SecondaryCurve2D = edge.SecondaryFace.Surface.GetProjectedCurve(crv, 0.0);
                        if (!edge.Forward(edge.SecondaryFace)) edge.SecondaryCurve2D.Reverse();
                        SurfaceHelper.AdjustPeriodic(edge.SecondaryFace.Surface, edge.SecondaryFace.Domain, edge.SecondaryCurve2D);
                        edge.Orient();
                        involvedFaces.Add(edge.PrimaryFace);
                        involvedFaces.Add(edge.SecondaryFace);
                    }
                    else
                    {
                        // return false; // edge could not be recalculated, the modification is not possible
                    }
                }
                foreach (Face face in involvedFaces)
                {
                    face.ForceAreaRecalc();
                }
                foreach (Face face in involvedFaces)
                {   // checks the topology of the bounds in 2d: no intersection or overlapping, holes inside the outline
                    if (!face.Check2DBounds())
                    {
                        //return false;
                    }
                }
                if (clonedShell.CheckConsistency())
                {
                    return true;
                }
                else
                {
                    // return false;
                }
                return true;
            }
            finally
            {   // clear all sets to allow multiple usage of the parametrics
                edgesToRecalculate.Clear();
                verticesToRecalculate.Clear();
                modifiedFaces.Clear();
                constrainedFaces.Clear();
                tangentialEdgesModified.Clear();
            }
        }

        private void CopyAttributes(ICurve from, ICurve to)
        {
            IGeoObject gfrom = from as IGeoObject;
            IGeoObject gto = to as IGeoObject;
            gto.CopyAttributes(gfrom);
        }

        private HashSet<Face> ReverseDictionaryLookUp(HashSet<Face> toreverse)
        {
            HashSet<Face> res = new HashSet<Face>();
            foreach (Face face in toreverse)
            {
                foreach (KeyValuePair<Face, Face> item in faceDict)
                {
                    if (item.Value == face) res.Add(item.Key);
                }
            }
            return res;
        }
        public void GetDictionaries(out Dictionary<Face, Face> faceDict, out Dictionary<Edge, Edge> edgeDict, out Dictionary<Vertex, Vertex> vertexDict)
        {
            faceDict = this.faceDict;
            edgeDict = this.edgeDict;
            vertexDict = this.vertexDict;
        }
        public IEnumerable<object> GetAffectedObjects()
        {
            return affectedObjects;
        }
    }
}
