using CADability.Curve2D;
using CADability.Shapes;
using System;
using System.Collections.Generic;
using Wintellect.PowerCollections;

namespace CADability.GeoObject
{
    internal class SplitShellWithCurvesException : ApplicationException
    {
        public SplitShellWithCurvesException()
            : base()
        {
        }
        public SplitShellWithCurvesException(string msg)
            : base(msg)
        {
        }
    }
    internal class SplitShellWithCurves
    {
        Shell shell;
        Path closedBorder;
        OctTree<Face> faceOcttree;
        double precision;
        Face[] vertexToFace;
        Dictionary<Face, List<ICurve2D>> all2DCurves;
        Dictionary<Edge, List<double>> splitedEdges;
        Set<Vertex> outsideVertices;
        List<Face> resultingFaces;
        public SplitShellWithCurves(Shell shell, Path closedBorder, double precision)
        {
            this.shell = shell;
            this.closedBorder = closedBorder;
            faceOcttree = new OctTree<Face>(shell.GetExtent(precision), precision);
            for (int i = 0; i < shell.Faces.Length; i++)
            {
                faceOcttree.AddObject(shell.Faces[i]);
            }
            this.precision = precision;
        }
        public Shell GetInsidePart()
        {
            closedBorder.Flatten();
            if (!closedBorder.IsClosed)
            {
                Line l = Line.Construct();
                l.SetTwoPoints(closedBorder.EndPoint, closedBorder.StartPoint);
                closedBorder.Add(l);
            }
            vertexToFace = new Face[closedBorder.CurveCount];
            for (int i = 0; i < closedBorder.CurveCount; i++)
            {
                Face fc = FindClosestFace(closedBorder.Curve(i).StartPoint);
                if (fc == null) throw new SplitShellWithCurvesException();
                vertexToFace[i] = fc;
            }
            all2DCurves = new Dictionary<Face, List<ICurve2D>>();
            splitedEdges = new Dictionary<Edge, List<double>>();
            for (int i = 0; i < vertexToFace.Length; i++)
            {
                InsertCurve(i);
            }
            // jetzt die Faces beschneiden und eine neue Shell bauen
            Vertex[] vertices = shell.Vertices;
            outsideVertices = new Set<Vertex>();
            for (int i = 0; i < vertices.Length; i++)
            {   // wenn der Vertex eine Außenkante hat, dann gilt er als außenliegend
                Edge[] vedges = vertices[i].Edges;
                for (int j = 0; j < vedges.Length; j++)
                {
                    if (vedges[j].SecondaryFace == null)
                    {
                        FollowVertex(vertices[j]);
                    }
                }
            }
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            foreach (Vertex vtx in outsideVertices)
            {
                Point pnt = Point.Construct();
                pnt.Location = vtx.Position;
                pnt.Symbol = PointSymbol.Circle;
                dc.Add(pnt, vtx.GetHashCode());
            }
#endif
            // alle Faces, die aufgesplitted werden müssen:
            Set<Face> splittedFaces = new Set<Face>();
            foreach (Edge edge in splitedEdges.Keys)
            {
                splittedFaces.Add(edge.PrimaryFace);
                if (edge.SecondaryFace != null) splittedFaces.Add(edge.SecondaryFace);
            }
            resultingFaces = new List<Face>();
            for (int i = 0; i < shell.Faces.Length; i++)
            {
                Face face = shell.Faces[i];
                if (splittedFaces.Contains(face))
                {
                    AddSplittedFace(face);
                }
                else
                {
                    if (!outsideVertices.Contains(face.Vertices[0]))
                    {
                        resultingFaces.Add(face.Clone() as Face);
                    }
                }
            }
            GeoObjectList res = Make3D.SewFacesAndShells(new GeoObjectList(resultingFaces.ToArray()));
            if (res.Count == 1) return res[0] as Shell;
            return null;
        }
        private void AddSplittedFace(Face face)
        {
            List<ICurve> edgeCurves = new List<ICurve>();
            List<ICurve2D> curves2d = new List<ICurve2D>();
#if DEBUG
            Vertex[] vtx = face.Vertices;
            bool[] outside = new bool[vtx.Length];
            Polyline pln = Polyline.Construct();
            for (int i = 0; i < vtx.Length; i++)
            {
                outside[i] = outsideVertices.Contains(vtx[i]);
                pln.AddPoint(vtx[i].Position);
            }
            DebuggerContainer dc = new DebuggerContainer();
#endif
            Edge[] edges = face.AllEdges; // sollte keine Löcher haben
            for (int i = 0; i < edges.Length; i++)
            {
                List<double> positions;
                if (splitedEdges.TryGetValue(edges[i], out positions))
                {
                    List<double> pos = new List<double>(positions);
                    pos.Add(0.0);
                    pos.Add(1.0);
                    pos.Sort();
                    int startj;
                    Vertex startVertex;
                    ICurve2D c2d = edges[i].Curve2D(face);
#if DEBUG
                    dc.Add(c2d, System.Drawing.Color.Red, i);
#endif
                    if ((face.Surface.PointAt(c2d.StartPoint) | edges[i].Vertex1.Position) <
                        (face.Surface.PointAt(c2d.StartPoint) | edges[i].Vertex2.Position))
                    {
                        startVertex = edges[i].Vertex1;
                    }
                    else
                    {
                        startVertex = edges[i].Vertex2;
                        // die Kurve geht andersrum als die Kante, also Kantenschnittpunkte umdrehen
                        pos.Reverse();
                    }
                    if (outsideVertices.Contains(startVertex)) startj = 1;
                    else startj = 0;
                    for (int j = startj; j < pos.Count - 1; j += 2)
                    {
                        double pos1 = c2d.PositionOf(face.Surface.PositionOf(edges[i].Curve3D.PointAt(pos[j])));
                        double pos2 = c2d.PositionOf(face.Surface.PositionOf(edges[i].Curve3D.PointAt(pos[j + 1])));
                        if (pos2 < pos1)
                        {
                            double tmp = pos1;
                            pos1 = pos2;
                            pos2 = tmp;
                        }
                        ICurve2D trimmed = c2d.Trim(pos1, pos2);
                        curves2d.Add(trimmed);
#if DEBUG
                        dc.Add(trimmed, System.Drawing.Color.Blue, i);
#endif
                    }
                }
                else
                {
                    if (!outsideVertices.Contains(edges[i].Vertex1))
                    {
#if DEBUG
                        dc.Add(edges[i].Curve2D(face), System.Drawing.Color.Green, i);
#endif
                        curves2d.Add(edges[i].Curve2D(face));
                    }
                }
            }
            List<ICurve2D> lc2d;
            if (all2DCurves.TryGetValue(face, out lc2d))
            {
                for (int i = 0; i < lc2d.Count; i++)
                {
#if DEBUG
                    dc.Add(lc2d[i], System.Drawing.Color.HotPink, i);
#endif
                    // die folgende Kurve mit Start- und Endpunkt anpassen, denn sie ist evtl ungenau...
                    curves2d.Add(lc2d[i]);
                }
            }
            // alle zuletzt hinzugefügten Kurven magnetisch einschnappen lassen
            for (int i = all2DCurves.Count - lc2d.Count; i < all2DCurves.Count; i++)
            {
                AddAndAdjust2DCurve(curves2d, i);
            }
            // eigentlich wäre mehr Information vorhanden, als dass man diese Kurven völlig neu sortieren müsste
            // aber so ist es nun mal einfacher
            ICurve2D[] acsd = new ICurve2D[curves2d.Count];
            for (int i = 0; i < acsd.Length; i++)
            {
                acsd[i] = curves2d[i].Clone();
            }
            Reduce2D r2d = new Reduce2D();
            r2d.Add(acsd);
            r2d.OutputMode = Reduce2D.Mode.Paths;
            ICurve2D[] red = r2d.Reduced;
            if (red.Length == 1)
            {
                Border bdr = new Border(red[0]);
                SimpleShape ss = new SimpleShape(bdr);
                Face splitted = Face.MakeFace(face.Surface, ss);
                resultingFaces.Add(splitted);
            }

            //CompoundShape cs = CompoundShape.CreateFromList(acsd, Precision.eps);
            //if (cs != null && cs.SimpleShapes.Length == 1)
            //{
            //    SimpleShape ss = cs.SimpleShapes[0];
            //    Face splitted = Face.MakeFace(face.Surface, ss);
            //    resultingFaces.Add(splitted);
            //}
            //else
            //{
            //}
        }

        private void AddAndAdjust2DCurve(List<ICurve2D> curves2d, int ind)
        {
            double smin = double.MaxValue, emin = double.MaxValue;
            GeoPoint2D sp = GeoPoint2D.Origin, ep = GeoPoint2D.Origin;
            ICurve2D iCurve2D = curves2d[ind];
            for (int i = 0; i < curves2d.Count; i++)
            {
                if (ind != i)
                {
                    double d = iCurve2D.StartPoint | curves2d[i].StartPoint;
                    if (d < smin)
                    {
                        smin = d;
                        sp = curves2d[i].StartPoint;
                    }
                    d = iCurve2D.StartPoint | curves2d[i].EndPoint;
                    if (d < smin)
                    {
                        smin = d;
                        sp = curves2d[i].EndPoint;
                    }
                    d = iCurve2D.EndPoint | curves2d[i].StartPoint;
                    if (d < emin)
                    {
                        emin = d;
                        ep = curves2d[i].StartPoint;
                    }
                    d = iCurve2D.EndPoint | curves2d[i].EndPoint;
                    if (d < emin)
                    {
                        emin = d;
                        ep = curves2d[i].EndPoint;
                    }
                }
            }
            iCurve2D.StartPoint = sp;
            iCurve2D.EndPoint = ep;
        }
        private void FollowVertex(Vertex vertex)
        {
            if (outsideVertices.Contains(vertex)) return;
            outsideVertices.Add(vertex);
            Edge[] vedges = vertex.Edges;
            for (int i = 0; i < vedges.Length; i++)
            {
                Edge edge = vedges[i];
                if (splitedEdges.ContainsKey(edge))
                {
                    int numSplits = splitedEdges[edge].Count;
                    if ((numSplits & 1) == 1) continue; // ungerade Anzahl von Schnitten, der andere vertex ist innerhalb
                }
                // den anderen vertex weiterverfolgen
                if (edge.Vertex1 == vertex) FollowVertex(edge.Vertex2);
                else FollowVertex(edge.Vertex1);
            }
        }
        private Face FindClosestFace(GeoPoint toPoint)
        {
            Face[] near = faceOcttree.GetObjectsFromBox(new BoundingCube(toPoint, precision));
            double minDist = double.MaxValue;
            Face found = null;
            for (int i = 0; i < near.Length; i++)
            {
                GeoPoint2D uv = near[i].Surface.PositionOf(toPoint);
                GeoPoint p = near[i].Surface.PointAt(uv);
                double d = p | toPoint;
                if (d < minDist)
                {
                    if (near[i].Area.Contains(uv, true))
                    {
                        minDist = d;
                        found = near[i];
                    }
                }
            }
            return found;
        }
        private Edge[] FindCloseEdges(Face face, GeoPoint fromHere)
        {
            List<Edge> res = new List<Edge>();
            Edge[] all = face.AllEdges;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Curve3D != null)
                {
                    if (all[i].Curve3D.DistanceTo(fromHere) < precision)
                    {
                        res.Add(all[i]);
                    }
                }
            }
            return res.ToArray();
        }
        private bool SplitAndInsertCurve(ICurve toInsert, Face face1, Face face2, ICurve2D curveOnFace1, double startOnFace1, out ICurve2D curveOnFace2, out double startOnFace2, out double endOnFace2)
        {
            curveOnFace2 = null;
            startOnFace2 = -1.0;
            endOnFace2 = -1.0;
            double[] clp1 = face1.Area.Clip(curveOnFace1, true);
            double endOnFace1 = 0.0;
            for (int i = 0; i < clp1.Length; i += 2)
            {
                if (clp1[i] == startOnFace1)
                {
                    endOnFace1 = clp1[i + 1];
                }
            }
            if (endOnFace1 == 0.0) return false; // sollte nicht vorkommen
            GeoPoint leaveFace1 = face1.Surface.PointAt(curveOnFace1.PointAt(endOnFace1));
            Edge[] close = FindCloseEdges(face1, leaveFace1);
            Edge commonEdge = null;
            for (int i = 0; i < close.Length; i++)
            {
                if (close[i].OtherFace(face1) == face2)
                {
                    commonEdge = close[i];
                    break;
                }
            }
            if (commonEdge == null) return false;
            curveOnFace2 = face2.Surface.GetProjectedCurve(toInsert, precision);
            AdjustPeriodic(face2, curveOnFace2, leaveFace1);
            double[] clp2 = face2.Area.Clip(curveOnFace2, true);
            for (int i = 0; i < clp2.Length; i += 2)
            {
                GeoPoint enterFace2 = face2.Surface.PointAt(curveOnFace2.PointAt(clp2[i]));
                if (commonEdge.Curve3D.DistanceTo(enterFace2) < precision)
                {   // a good common edge which both 2d curves connect
                    double pos = (commonEdge.Curve3D.PositionOf(enterFace2) + commonEdge.Curve3D.PositionOf(leaveFace1)) / 2.0;
                    List<double> edgePositions;
                    if (!splitedEdges.TryGetValue(commonEdge, out edgePositions))
                    {
                        splitedEdges[commonEdge] = new List<double>();
                        edgePositions = splitedEdges[commonEdge];
                    }
                    edgePositions.Add(pos);
                    ICurve2D clipped1 = curveOnFace1.Trim(startOnFace1, endOnFace1);
                    GeoPoint2D uv1 = face1.Surface.PositionOf(commonEdge.Curve3D.PointAt(pos));
                    clipped1.EndPoint = uv1;
                    List<ICurve2D> lc2d;
                    if (!all2DCurves.TryGetValue(face1, out lc2d))
                    {
                        lc2d = new List<ICurve2D>();
                        all2DCurves[face1] = lc2d;
                    }
                    lc2d.Add(clipped1);
                    startOnFace2 = clp2[i];
                    endOnFace2 = clp2[i + 1];
                    return true;
                }
            }
            return false;
        }

        private void AdjustPeriodic(Face face2, ICurve2D curveOnFace2, GeoPoint leaveFace1)
        {
            if (!face2.Surface.IsUPeriodic && !face2.Surface.IsVPeriodic) return;
            double uperiod = 0.0, vperiod = 0.0;
            if (face2.Surface.IsUPeriodic) uperiod = face2.Surface.UPeriod;
            if (face2.Surface.IsVPeriodic) vperiod = face2.Surface.VPeriod;
            GeoPoint2D pos = face2.Surface.PositionOf(leaveFace1);
            if (!face2.area.Contains(pos, true))
            {   // dieser Punkt muss eigentlich in Area anthalten sein, er kann höchstens um die Periode verschoben sein
                bool found = false;
                CompoundShape tst = face2.area.Expand((uperiod + vperiod) / 100);
                if (tst.Contains(pos, true)) found = true;
                if (!found && uperiod > 0.0)
                {
                    if (tst.Contains(pos + uperiod * GeoVector2D.XAxis, true))
                    {
                        pos = pos + uperiod * GeoVector2D.XAxis;
                        found = true;
                    }
                    if (!found && tst.Contains(pos - uperiod * GeoVector2D.XAxis, true))
                    {
                        pos = pos - uperiod * GeoVector2D.XAxis;
                        found = true;
                    }
                }
                if (!found && vperiod > 0.0)
                {
                    if (tst.Contains(pos + vperiod * GeoVector2D.YAxis, true))
                    {
                        pos = pos + vperiod * GeoVector2D.YAxis;
                        found = true;
                    }
                    if (!found && tst.Contains(pos - vperiod * GeoVector2D.YAxis, true))
                    {
                        pos = pos - vperiod * GeoVector2D.YAxis;
                        found = true;
                    }
                }
            }
            // jetzt die Kurve so verschieben, dass der Punkt draufliegt
            double du = 0.0, dv = 0.0;
            double dist = curveOnFace2.MinDistance(pos);
            if (uperiod > 0.0)
            {
                double d = curveOnFace2.MinDistance(pos + uperiod * GeoVector2D.XAxis);
                if (d < dist)
                {
                    du = -uperiod;
                    dist = d;
                }
                d = curveOnFace2.MinDistance(pos - uperiod * GeoVector2D.XAxis);
                if (d < dist)
                {
                    du = uperiod;
                    dist = d;
                }
            }
            if (vperiod > 0.0)
            {
                double d = curveOnFace2.Distance(pos + vperiod * GeoVector2D.YAxis);
                if (d < dist)
                {
                    dv = -vperiod;
                    dist = d;
                }
                d = curveOnFace2.Distance(pos - vperiod * GeoVector2D.YAxis);
                if (d < dist)
                {
                    dv = vperiod;
                    dist = d;
                }
            }
            if (du != 0.0 || dv != 0.0) curveOnFace2.Move(du, dv);
        }
        private void InsertCurve(int ind)
        {
            ICurve toInsert = closedBorder.Curve(ind);
            Face fromFace = vertexToFace[ind];
            int ind1 = ind + 1;
            if (ind1 >= vertexToFace.Length) ind1 = 0;
            Face toFace = vertexToFace[ind1];
            ICurve2D c2d = fromFace.Surface.GetProjectedCurve(toInsert, precision);
            double[] clp = fromFace.Area.Clip(c2d, true);
            if (clp == null || clp.Length < 2) throw new SplitShellWithCurvesException("curve outside of face");
            if (clp.Length == 2 && clp[0] == 0.0 && clp[1] == 1.0)
            {
                if (fromFace == toFace)
                {
                    List<ICurve2D> lc2d;
                    if (!all2DCurves.TryGetValue(fromFace, out lc2d))
                    {
                        lc2d = new List<ICurve2D>();
                        all2DCurves[fromFace] = lc2d;
                    }
                    lc2d.Add(c2d);
                }
                else throw new SplitShellWithCurvesException("endpoint on wrong face");
            }
            else
            {   // die Kurve geht in ein anderes Face
                Face face1 = fromFace;
                ICurve2D curveOnFace1 = c2d;
                double startOnFace1 = clp[0]; // sollte immer 0.0 sein, oder?
                double endOnFace1 = clp[1];
                Face face2 = null;
                ICurve2D curveOnFace2 = null;
                double startOnFace2 = 0.0, endOnFace2 = 0.0;

                bool success;
                do
                {
                    success = false;
                    GeoPoint onEdge = face1.Surface.PointAt(curveOnFace1.PointAt(endOnFace1));
                    Edge[] close = FindCloseEdges(face1, onEdge);
                    if (close.Length == 0) throw new SplitShellWithCurvesException("no connecting edge found");
                    for (int i = 0; i < close.Length; i++)
                    {
                        face2 = close[i].OtherFace(face1);
                        success = SplitAndInsertCurve(toInsert, face1, face2, curveOnFace1, startOnFace1, out curveOnFace2, out startOnFace2, out endOnFace2);
                        if (success)
                        {
                            face1 = face2;
                            startOnFace1 = startOnFace2;
                            endOnFace1 = endOnFace2;
                            curveOnFace1 = curveOnFace2;
                            break;
                        }
                    }
                    if (success && face1 == toFace && endOnFace2 == 1.0)
                    {
                        ICurve2D clipped2 = curveOnFace2.Trim(startOnFace2, endOnFace2);
                        List<ICurve2D> lc2d;
                        if (!all2DCurves.TryGetValue(face2, out lc2d))
                        {
                            lc2d = new List<ICurve2D>();
                            all2DCurves[face2] = lc2d;
                        }
                        lc2d.Add(clipped2);
                        break;
                    }
                } while (success);
            }
        }
    }
}
