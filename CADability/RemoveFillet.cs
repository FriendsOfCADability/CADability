using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CADability
{
    public class RemoveFillet
    {
        Shell shell;
        HashSet<Face> fillets;
        public RemoveFillet(Shell shell, HashSet<Face> fillets)
        {   // make a clone of the shell to work on and use the fillets of the cloned shell
            Dictionary<Edge, Edge> clonedEdges = new Dictionary<Edge, Edge>();
            Dictionary<Vertex, Vertex> clonedVertices = new Dictionary<Vertex, Vertex>();
            Dictionary<Face, Face> clonedFaces = new Dictionary<Face, Face>();
            this.shell = shell.Clone(clonedEdges, clonedVertices, clonedFaces);
            this.fillets = new HashSet<Face>();
            foreach (Face face in fillets)
            {
                this.fillets.Add(clonedFaces[face]);
            }
        }

        public Shell Result()
        {
            // the fillets edges fall into one of three categories: 
            // - lengthwayTangential: edges that are tangential to other faces, i.e. they round the other faces
            // - crossway: edges that connect fillets
            // - filletEnd: here the fillets end or are otherwise broken up
            HashSet<Face> lengthwayTangential = new HashSet<Face>(); // the two faces that this fillet rounds
            HashSet<Edge> crossway = new HashSet<Edge>(); // connection to the following or previous fillet
            HashSet<Edge> lengthway = new HashSet<Edge>(); // connection between fillet and rounded face
            HashSet<Edge> filletEnd = new HashSet<Edge>(); // an edge which is the end of a fillet
            Dictionary<Edge, Face> forking = new Dictionary<Edge, Face>(); // a spherical face, where the fillets fork
            foreach (Face face in fillets)
            {
                if (face.Surface is ISurfaceOfArcExtrusion extrusion)
                {
                    foreach (Edge edge in face.Edges)
                    {
                        Face otherFace = edge.OtherFace(face);
                        if (edge.IsTangentialEdge() && edge.Curve2D(face).DirectionAt(0.5).IsMoreHorizontal != extrusion.ExtrusionDirectionIsV)
                        {
                            lengthwayTangential.Add(otherFace);
                            lengthway.Add(edge);
                        }
                        else if (fillets.Contains(otherFace)) crossway.Add(edge);
                        else filletEnd.Add(edge);
                    }
                }
                else if (face.Surface is SphericalSurface)
                {
                    foreach (Edge edge in face.Edges)
                    {
                        Face otherFace = edge.OtherFace(face);
                        if (fillets.Contains(otherFace)) crossway.Add(edge);
                    }
                }
            }
            Dictionary<Edge, Vertex> crosswayToVertex = new Dictionary<Edge, Vertex>();
            Dictionary<Vertex, Vertex> verticesToReplace = new Dictionary<Vertex, Vertex>();

            foreach (Edge edg in Extensions.Combine<Edge>(crossway, filletEnd))
            {
                HashSet<Face> involved = edg.Vertex1.InvolvedFaces;
                involved.UnionWith(edg.Vertex2.InvolvedFaces);
                involved.ExceptWith(fillets);
                HashSet<Edge> involvedCrosswayEdges = new HashSet<Edge>();
                involvedCrosswayEdges.Add(edg);
                if (involved.Count < 3)
                {   // maybe the edge is part of a spherical face
                    Face sph = null;
                    if (edg.PrimaryFace.Surface is SphericalSurface && fillets.Contains(edg.PrimaryFace)) sph = edg.PrimaryFace;
                    else if (edg.SecondaryFace.Surface is SphericalSurface && fillets.Contains(edg.SecondaryFace)) sph = edg.SecondaryFace;
                    if (sph != null)
                    {
                        foreach (Edge spedge in sph.AllEdges)
                        {
                            if (spedge != edg && fillets.Contains(spedge.OtherFace(sph)))
                            {
                                involvedCrosswayEdges.Add(spedge);
                                involved.UnionWith(spedge.Vertex1.InvolvedFaces);
                                involved.UnionWith(spedge.Vertex2.InvolvedFaces);
                            }
                        }
                    }
                    involved.ExceptWith(fillets);
                }
                if (involved.Count >= 3) // which should be the case 
                {
                    Face[] ia = involved.ToArray();
                    GeoPoint ip = edg.Curve3D.PointAt(0.5);
                    if (Surfaces.IntersectThreeSurfaces(ia[0].Surface, ia[0].Domain, ia[1].Surface, ia[1].Domain, ia[2].Surface, ia[2].Domain, ref ip, out GeoPoint2D uv0, out GeoPoint2D uv1, out GeoPoint2D uv2))
                    {   // there should be an intersection point close to the middle of the crossway edge
                        Vertex v = new Vertex(ip);
                        foreach (Edge edge in involvedCrosswayEdges)
                        {
                            crosswayToVertex[edge] = v;
                            verticesToReplace[edge.Vertex1] = v;
                            verticesToReplace[edge.Vertex2] = v;
                        }
                    }
                    else
                    {   // maybe two surfaces are identical with different domains: find a common edge
                        ip = edg.Curve3D.PointAt(0.5);
                        bool found = false;
                        for (int i = 0; i < involved.Count - 1; i++)
                        {
                            for (int j = i + 1; j < involved.Count; j++)
                            {
                                IEnumerable<Edge> commonEdges = new HashSet<Edge>(ia[i].AllEdges).Intersect(ia[j].AllEdges);
                                foreach (Edge e in commonEdges)
                                {
                                    if (e.IsConnected(edg))
                                    {
                                        for (int k = 0; k < involved.Count; k++)
                                        {
                                            if (k != i && k != j)
                                            {
                                                ia[k].Surface.Intersect(e.Curve3D, ia[k].Domain, out GeoPoint[] ips, out GeoPoint2D[] uvOnK, out double[] uOnCurve);
                                                if (ips.Length > 0)
                                                {
                                                    double dist = double.MaxValue;
                                                    GeoPoint p = GeoPoint.Origin;
                                                    for (int l = 0; l < ips.Length; l++)
                                                    {
                                                        double d = ips[l] | ip;
                                                        if (d < dist)
                                                        {
                                                            dist = d;
                                                            p = ips[l];
                                                        }
                                                    }
                                                    Vertex v = new Vertex(p);
                                                    foreach (Edge edge in involvedCrosswayEdges)
                                                    {
                                                        crosswayToVertex[edge] = v;
                                                        verticesToReplace[edge.Vertex1] = v;
                                                        verticesToReplace[edge.Vertex2] = v;
                                                    }
                                                    found = true;
                                                    break;
                                                }
                                            }
                                        }
                                        if (found) break;
                                    }
                                    if (found) break;
                                }
                                if (found) break;
                            }
                            if (found) break;
                        }
                    }
                }
                if (!fillets.Contains(edg.PrimaryFace)) edg.PrimaryFace.RemoveEdge(edg, true); // remove the fillets crossway connections from normal (non-fillet) faces
                if (!fillets.Contains(edg.SecondaryFace)) edg.SecondaryFace.RemoveEdge(edg, true);
            }
            HashSet<Face> facesToRecalculate = new HashSet<Face>();
            // replace all length-way edges
            foreach (Face face in fillets)
            {
                if (face.Surface is ISurfaceOfArcExtrusion extrusion)
                {
                    HashSet<Edge> all = new HashSet<Edge>(face.AllEdges);
                    List<Edge> lw = new List<Edge>(all.Intersect(lengthway)); // this should be two edges
                    List<Edge> cw = new List<Edge>(all.Intersect(crossway.Union(filletEnd))); // this should also be two edges
                    if (lw.Count == 2 && cw.Count == 2)
                    {
                        Face[] toReplaceEdge = new Face[2];
                        for (int i = 0; i < 2; i++)
                        {
                            if (lw[i].PrimaryFace == face) toReplaceEdge[i] = lw[i].SecondaryFace;
                            else toReplaceEdge[i] = lw[i].PrimaryFace;
                        }
                        Vertex v0 = crosswayToVertex[cw[0]];
                        Vertex v1 = crosswayToVertex[cw[1]];
                        BoundingRect domain0 = toReplaceEdge[0].Domain;
                        domain0.MinMax(toReplaceEdge[0].PositionOf(v0.Position));
                        domain0.MinMax(toReplaceEdge[0].PositionOf(v1.Position));
                        BoundingRect domain1 = toReplaceEdge[1].Domain;
                        domain1.MinMax(toReplaceEdge[1].PositionOf(v0.Position));
                        domain1.MinMax(toReplaceEdge[1].PositionOf(v1.Position));
                        ICurve[] newCurves = toReplaceEdge[0].Surface.Intersect(domain0, toReplaceEdge[1].Surface, domain1);
                        ICurve newCurve = Hlp.GetClosest(newCurves, crv => crv.DistanceTo(v0.Position) + crv.DistanceTo(v1.Position));
                        if (newCurve != null)
                        {
                            double pos0 = newCurve.PositionOf(v0.Position);
                            double pos1 = newCurve.PositionOf(v1.Position);
                            if (pos1 < pos0)
                            {
                                newCurve.Reverse();
                                pos0 = 1 - pos0;
                                pos1 = 1 - pos1;
                            }
                            newCurve.Trim(pos0, pos1);
                            Edge newEdge = new Edge(newCurve, v0, v1);
                            bool fw0, fw1;
                            if (lw[0].Curve3D.DirectionAt(0.5) * newCurve.DirectionAt(0.5) > 0) fw0 = lw[0].Forward(toReplaceEdge[0]);
                            else fw0 = !lw[0].Forward(toReplaceEdge[0]);
                            if (lw[1].Curve3D.DirectionAt(0.5) * newCurve.DirectionAt(0.5) > 0) fw1 = lw[1].Forward(toReplaceEdge[1]);
                            else fw1 = !lw[1].Forward(toReplaceEdge[1]);
                            newEdge.SetPrimary(toReplaceEdge[0], fw0);
                            newEdge.SetSecondary(toReplaceEdge[1], fw1);
                            newEdge.PrimaryCurve2D = toReplaceEdge[0].GetProjectedCurve(newCurve, newEdge.Forward(toReplaceEdge[0]));
                            newEdge.SecondaryCurve2D = toReplaceEdge[1].GetProjectedCurve(newCurve, newEdge.Forward(toReplaceEdge[1]));
                            toReplaceEdge[0].SubstitudeEdge(lw[0], newEdge);
                            toReplaceEdge[1].SubstitudeEdge(lw[1], newEdge);
                            lw[0].DisconnectFromFace(toReplaceEdge[0]);
                            lw[1].DisconnectFromFace(toReplaceEdge[1]);
                            facesToRecalculate.Add(toReplaceEdge[0]);
                            facesToRecalculate.Add(toReplaceEdge[1]);
                        }
                    }
                }
            }
            foreach (Face face in shell.Faces)
            {
                if (fillets.Contains(face)) continue;
                foreach (Edge edge in face.Edges)
                {
                    if (verticesToReplace.ContainsKey(edge.Vertex1))
                    {
                        if (verticesToReplace.ContainsKey(edge.Vertex2))
                        {
                            Vertex v1 = verticesToReplace[edge.Vertex1];
                            Vertex v2 = verticesToReplace[edge.Vertex2];
                            BoundingRect primDomain = edge.PrimaryFace.Domain;
                            primDomain.MinMax(edge.PrimaryFace.PositionOf(v1.Position));
                            primDomain.MinMax(edge.PrimaryFace.PositionOf(v2.Position));
                            BoundingRect secDomain = edge.SecondaryFace.Domain;
                            secDomain.MinMax(edge.SecondaryFace.PositionOf(v1.Position));
                            secDomain.MinMax(edge.SecondaryFace.PositionOf(v2.Position));
                            ICurve[] newCurves = edge.PrimaryFace.Surface.Intersect(primDomain, edge.SecondaryFace.Surface, secDomain);
                            ICurve newCurve = Hlp.GetClosest(newCurves, crv => crv.DistanceTo(v1.Position) + crv.DistanceTo(v2.Position));
                            if (newCurve == null && edge.PrimaryFace.Surface.SameGeometry(primDomain, edge.SecondaryFace.Surface, secDomain, Precision.eps, out ModOp2D dumy))
                            {   // this edge connects two same-surface faces, like two half shells of a cylinder
                                newCurve = Line.TwoPoints(v1.Position, v2.Position);
                            }
                            if (newCurve != null)
                            {
                                double pos0 = newCurve.PositionOf(v1.Position);
                                double pos1 = newCurve.PositionOf(v2.Position);
                                if (pos1 < pos0)
                                {
                                    newCurve.Reverse();
                                    pos0 = 1 - pos0;
                                    pos1 = 1 - pos1;
                                }
                                newCurve.Trim(pos0, pos1);
                                edge.Curve3D = newCurve;
                                edge.PrimaryCurve2D = edge.PrimaryFace.GetProjectedCurve(newCurve, edge.Forward(edge.PrimaryFace));
                                edge.SecondaryCurve2D = edge.SecondaryFace.GetProjectedCurve(newCurve, edge.Forward(edge.SecondaryFace));
                                edge.Vertex1 = v1;
                                edge.Vertex2 = v2;
                                facesToRecalculate.Add(edge.PrimaryFace);
                                facesToRecalculate.Add(edge.SecondaryFace);
                            }
                        }
                        else
                        {
                            Vertex v1 = verticesToReplace[edge.Vertex1];
                            BoundingRect primDomain = edge.PrimaryFace.Domain;
                            primDomain.MinMax(edge.PrimaryFace.PositionOf(v1.Position));
                            BoundingRect secDomain = edge.SecondaryFace.Domain;
                            secDomain.MinMax(edge.SecondaryFace.PositionOf(v1.Position));
                            ICurve[] newCurves = edge.PrimaryFace.Surface.Intersect(primDomain, edge.SecondaryFace.Surface, secDomain);
                            ICurve newCurve = Hlp.GetClosest(newCurves, crv => crv.DistanceTo(v1.Position));
                            if (newCurve == null && edge.PrimaryFace.Surface.SameGeometry(primDomain, edge.SecondaryFace.Surface, secDomain, Precision.eps, out ModOp2D dumy))
                            {   // this edge connects two same-surface faces, like two half shells of a cylinder
                                newCurve = Line.TwoPoints(v1.Position, edge.Vertex2.Position);
                            }
                            if (newCurve != null)
                            {
                                double pos0 = newCurve.PositionOf(v1.Position);
                                double pos1 = newCurve.PositionOf(edge.Vertex2.Position);
                                if (pos1 < pos0)
                                {
                                    newCurve.Reverse();
                                    pos0 = 1 - pos0;
                                    pos1 = 1 - pos1;
                                }
                                newCurve.Trim(pos0, pos1);
                                edge.Curve3D = newCurve;
                                edge.PrimaryCurve2D = edge.PrimaryFace.GetProjectedCurve(newCurve, edge.Forward(edge.PrimaryFace));
                                edge.SecondaryCurve2D = edge.SecondaryFace.GetProjectedCurve(newCurve, edge.Forward(edge.SecondaryFace));
                                edge.Vertex1 = v1;
                                facesToRecalculate.Add(edge.PrimaryFace);
                                facesToRecalculate.Add(edge.SecondaryFace);
                            }
                        }
                    }
                    else if (verticesToReplace.ContainsKey(edge.Vertex2))
                    {
                        Vertex v2 = verticesToReplace[edge.Vertex2];
                        BoundingRect primDomain = edge.PrimaryFace.Domain;
                        primDomain.MinMax(edge.PrimaryFace.PositionOf(v2.Position));
                        BoundingRect secDomain = edge.SecondaryFace.Domain;
                        secDomain.MinMax(edge.SecondaryFace.PositionOf(v2.Position));
                        ICurve[] newCurves = edge.PrimaryFace.Surface.Intersect(primDomain, edge.SecondaryFace.Surface, secDomain);
                        ICurve newCurve = Hlp.GetClosest(newCurves, crv => crv.DistanceTo(v2.Position));
                        if (newCurve == null && edge.PrimaryFace.Surface.SameGeometry(primDomain, edge.SecondaryFace.Surface, secDomain, Precision.eps, out ModOp2D dumy))
                        {   // this edge connects two same-surface faces, like two half shells of a cylinder
                            newCurve = Line.TwoPoints(edge.Vertex1.Position, v2.Position);
                        }
                        if (newCurve != null)
                        {
                            double pos0 = newCurve.PositionOf(edge.Vertex1.Position);
                            double pos1 = newCurve.PositionOf(v2.Position);
                            if (pos1 < pos0)
                            {
                                newCurve.Reverse();
                                pos0 = 1 - pos0;
                                pos1 = 1 - pos1;
                            }
                            newCurve.Trim(pos0, pos1);
                            edge.Curve3D = newCurve;
                            edge.PrimaryCurve2D = edge.PrimaryFace.GetProjectedCurve(newCurve, edge.Forward(edge.PrimaryFace));
                            edge.SecondaryCurve2D = edge.SecondaryFace.GetProjectedCurve(newCurve, edge.Forward(edge.SecondaryFace));
                            edge.Vertex2 = v2;
                            facesToRecalculate.Add(edge.PrimaryFace);
                            facesToRecalculate.Add(edge.SecondaryFace);
                        }
                    }
                }
            }
            foreach (Face face in facesToRecalculate)
            {
                face.ForceAreaRecalc();
            }
            HashSet<Face> facesWithoutFillets = new HashSet<Face>(shell.Faces);
            facesWithoutFillets.ExceptWith(fillets);
            shell.SetFaces(facesWithoutFillets.ToArray());
            if (shell.CheckConsistency()) return shell;
            return null;
        }
    }
}
