using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// Turns a set of closed polygons that may intersect themselves and each other into
    /// non intersecting outline/hole groups, which is what a constrained (Delaunay) triangulation needs.
    /// The input follows the usual convention: counterclockwise loops are outlines, clockwise
    /// loops are holes; the region described by the input is the set of points with a winding
    /// number of 1 or more.
    /// <para>
    /// Such polygons appear when the outline of a <see cref="Face"/> is approximated by
    /// polylines: two curves that touch tangentially (or a hole that comes very close to the
    /// outline) are approximated with different points, so their polygons cross each other even
    /// though the curves do not. The crossings are small and unavoidable, but they make the
    /// input invalid for a constrained triangulation.
    /// </para>
    /// <para>
    /// The implementation computes the planar arrangement of all segments: all segments are
    /// split at their mutual intersections, the resulting graph is traversed face by face and
    /// each face gets its winding number. The boundary of the faces with a winding number of 1
    /// or more is the result. Everything is done with line segments only, no
    /// <see cref="Shapes.Border"/>, <see cref="Shapes.SimpleShape"/> or
    /// <see cref="Shapes.CompoundShape"/> is involved.
    /// </para>
    /// </summary>
    public class PolygonRegion
    {
        /// <summary>
        /// Splits <paramref name="loops"/> (first loop: counterclockwise outline, following
        /// loops: clockwise holes) into groups of non intersecting loops. Each group of the
        /// result starts with a counterclockwise outline, followed by the clockwise holes of
        /// that outline. Loops with an area of less than <paramref name="minArea"/> are omitted.
        /// </summary>
        public static GeoPoint2D[][][] Subdivide(GeoPoint2D[][] loops, double minArea)
        {
            PolygonRegion pr = new PolygonRegion(loops);
            if (!pr.Build()) return new GeoPoint2D[0][][];
            return pr.Extract(minArea);
        }

        // points closer than this fraction of the extent's diagonal are considered identical
        private const double relTol = 1e-9;

        private readonly GeoPoint2D[][] loops;
        private double tol;         // absolute version of relTol
        private double cellSize;    // grid cell size of the vertex lookup
        private GeoPoint2D gridOrigin;

        private GeoPoint2D[] segFrom, segTo;    // the input segments
        private List<double>[] segParams;       // where the input segments have to be split

        private readonly List<GeoPoint2D> vtx = new List<GeoPoint2D>();
        private readonly Dictionary<long, List<int>> vtxGrid = new Dictionary<long, List<int>>();

        // undirected edges of the arrangement
        private readonly List<int> edgeFrom = new List<int>();
        private readonly List<int> edgeTo = new List<int>();
        // net number of input segments running from edgeFrom to edgeTo: crossing the edge from
        // its right to its left side changes the winding number by this value
        private readonly List<int> edgeDelta = new List<int>();
        private readonly Dictionary<long, int> edgeMap = new Dictionary<long, int>();

        // half edges: 2*e runs edgeFrom->edgeTo, 2*e+1 runs the other way, so h^1 is the twin
        private int[] heFrom, heTo, heDelta, heNext, heCycle, heFace, hePos;
        private List<int>[] outgoing;   // per vertex the outgoing half edges, sorted by direction
        private double[] cycleArea;
        private int cycleCount;
        private int[] faceWinding;      // indexed by the face's representative cycle

        private PolygonRegion(GeoPoint2D[][] loops)
        {
            this.loops = loops;
        }

        private bool Build()
        {
            if (!CollectSegments()) return false;
            SplitAtIntersections();
            if (!BuildEdges()) return false;
            BuildHalfEdges();
            FindCycles();
            BuildFaces();
            ComputeWinding();
            return true;
        }

        #region arrangement
        private bool CollectSegments()
        {
            List<GeoPoint2D> from = new List<GeoPoint2D>();
            List<GeoPoint2D> to = new List<GeoPoint2D>();
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < loops.Length; ++i)
            {
                GeoPoint2D[] lp = loops[i];
                if (lp == null || lp.Length < 3) continue;
                for (int j = 0; j < lp.Length; ++j)
                {
                    from.Add(lp[j]);
                    to.Add(lp[(j + 1) % lp.Length]);
                    ext.MinMax(lp[j]);
                }
            }
            if (from.Count < 3) return false;
            double diag = Math.Sqrt(ext.Width * ext.Width + ext.Height * ext.Height);
            if (diag <= 0.0) return false;
            tol = diag * relTol;
            cellSize = 4.0 * tol;
            gridOrigin = ext.GetLowerLeft();
            segFrom = from.ToArray();
            segTo = to.ToArray();
            segParams = new List<double>[segFrom.Length];
            for (int i = 0; i < segFrom.Length; ++i) segParams[i] = new List<double>();
            return true;
        }

        /// <summary>
        /// Collects for every segment the parameters (0..1) where another segment touches or
        /// crosses it. Candidate pairs are found by sorting the segments by the left edge of
        /// their bounding box and only comparing overlapping ones.
        /// </summary>
        private void SplitAtIntersections()
        {
            int n = segFrom.Length;
            double[] xmin = new double[n], xmax = new double[n], ymin = new double[n], ymax = new double[n];
            for (int i = 0; i < n; ++i)
            {
                xmin[i] = Math.Min(segFrom[i].x, segTo[i].x);
                xmax[i] = Math.Max(segFrom[i].x, segTo[i].x);
                ymin[i] = Math.Min(segFrom[i].y, segTo[i].y);
                ymax[i] = Math.Max(segFrom[i].y, segTo[i].y);
            }
            int[] order = new int[n];
            for (int i = 0; i < n; ++i) order[i] = i;
            Array.Sort(order, (a, b) => xmin[a].CompareTo(xmin[b]));
            for (int ii = 0; ii < n; ++ii)
            {
                int i = order[ii];
                for (int jj = ii + 1; jj < n; ++jj)
                {
                    int j = order[jj];
                    if (xmin[j] > xmax[i] + tol) break; // sorted by xmin, no later segment can overlap
                    if (ymax[j] < ymin[i] - tol || ymin[j] > ymax[i] + tol) continue;
                    Intersect(i, j);
                }
            }
        }

        private void Intersect(int i, int j)
        {
            double rx = segTo[i].x - segFrom[i].x, ry = segTo[i].y - segFrom[i].y;
            double sx = segTo[j].x - segFrom[j].x, sy = segTo[j].y - segFrom[j].y;
            double lr = Math.Sqrt(rx * rx + ry * ry), ls = Math.Sqrt(sx * sx + sy * sy);
            if (lr == 0.0 || ls == 0.0) return;
            double dx = segFrom[j].x - segFrom[i].x, dy = segFrom[j].y - segFrom[i].y;
            double den = rx * sy - ry * sx;
            if (Math.Abs(den) > 1e-12 * lr * ls)
            {
                double t = (dx * sy - dy * sx) / den;
                double u = (dx * ry - dy * rx) / den;
                if (t >= -tol / lr && t <= 1.0 + tol / lr && u >= -tol / ls && u <= 1.0 + tol / ls)
                {
                    segParams[i].Add(Math.Min(1.0, Math.Max(0.0, t)));
                    segParams[j].Add(Math.Min(1.0, Math.Max(0.0, u)));
                }
            }
            else
            {   // parallel: only collinear segments matter, they have to be split at each other's ends
                if (Math.Abs(dx * ry - dy * rx) / lr > tol) return;
                AddProjection(i, segFrom[j], rx, ry, lr);
                AddProjection(i, segTo[j], rx, ry, lr);
                AddProjection(j, segFrom[i], sx, sy, ls);
                AddProjection(j, segTo[i], sx, sy, ls);
            }
        }

        private void AddProjection(int i, GeoPoint2D p, double dx, double dy, double len)
        {
            double t = ((p.x - segFrom[i].x) * dx + (p.y - segFrom[i].y) * dy) / (len * len);
            if (t > -tol / len && t < 1.0 + tol / len) segParams[i].Add(Math.Min(1.0, Math.Max(0.0, t)));
        }

        /// <summary>
        /// Splits the input segments at the collected parameters and accumulates the resulting
        /// sub segments into the undirected edges of the arrangement. Coincident points are
        /// merged, sub segments running in opposite directions cancel each other out in
        /// <see cref="edgeDelta"/>.
        /// </summary>
        private bool BuildEdges()
        {
            for (int i = 0; i < segFrom.Length; ++i)
            {
                List<double> par = segParams[i];
                par.Add(0.0);
                par.Add(1.0);
                par.Sort();
                int prev = -1;
                for (int k = 0; k < par.Count; ++k)
                {
                    if (k > 0 && par[k] - par[k - 1] <= 0.0) continue; // identical parameters
                    double t = par[k];
                    GeoPoint2D p = new GeoPoint2D(segFrom[i].x + t * (segTo[i].x - segFrom[i].x),
                                                  segFrom[i].y + t * (segTo[i].y - segFrom[i].y));
                    int v = VertexIndex(p);
                    if (v == prev) continue; // the split parameters were closer than the tolerance
                    if (prev >= 0) AddEdge(prev, v);
                    prev = v;
                }
            }
            return edgeFrom.Count > 0;
        }

        private int VertexIndex(GeoPoint2D p)
        {
            int gx = (int)Math.Floor((p.x - gridOrigin.x) / cellSize);
            int gy = (int)Math.Floor((p.y - gridOrigin.y) / cellSize);
            for (int dx = -1; dx <= 1; ++dx)
            {
                for (int dy = -1; dy <= 1; ++dy)
                {
                    List<int> bucket;
                    if (!vtxGrid.TryGetValue(GridKey(gx + dx, gy + dy), out bucket)) continue;
                    for (int k = 0; k < bucket.Count; ++k)
                    {
                        GeoPoint2D q = vtx[bucket[k]];
                        if (Math.Abs(q.x - p.x) <= tol && Math.Abs(q.y - p.y) <= tol) return bucket[k];
                    }
                }
            }
            vtx.Add(p);
            long key = GridKey(gx, gy);
            if (!vtxGrid.TryGetValue(key, out List<int> own)) vtxGrid[key] = own = new List<int>();
            own.Add(vtx.Count - 1);
            return vtx.Count - 1;
        }

        private static long GridKey(int gx, int gy)
        {
            return ((long)gx << 32) | (uint)gy;
        }

        private void AddEdge(int a, int b)
        {
            int lo = Math.Min(a, b), hi = Math.Max(a, b);
            long key = ((long)lo << 32) | (uint)hi;
            if (edgeMap.TryGetValue(key, out int ind))
            {
                edgeDelta[ind] += (a == lo) ? 1 : -1;
            }
            else
            {
                edgeMap[key] = edgeFrom.Count;
                edgeFrom.Add(lo);
                edgeTo.Add(hi);
                edgeDelta.Add((a == lo) ? 1 : -1);
            }
        }

        private void BuildHalfEdges()
        {
            int m = edgeFrom.Count;
            heFrom = new int[2 * m];
            heTo = new int[2 * m];
            heDelta = new int[2 * m];
            for (int e = 0; e < m; ++e)
            {
                heFrom[2 * e] = edgeFrom[e]; heTo[2 * e] = edgeTo[e]; heDelta[2 * e] = edgeDelta[e];
                heFrom[2 * e + 1] = edgeTo[e]; heTo[2 * e + 1] = edgeFrom[e]; heDelta[2 * e + 1] = -edgeDelta[e];
            }
            outgoing = new List<int>[vtx.Count];
            for (int h = 0; h < heFrom.Length; ++h)
            {
                if (outgoing[heFrom[h]] == null) outgoing[heFrom[h]] = new List<int>();
                outgoing[heFrom[h]].Add(h);
            }
            double[] angle = new double[heFrom.Length];
            for (int h = 0; h < heFrom.Length; ++h)
            {
                angle[h] = Math.Atan2(vtx[heTo[h]].y - vtx[heFrom[h]].y, vtx[heTo[h]].x - vtx[heFrom[h]].x);
            }
            hePos = new int[heFrom.Length];
            for (int v = 0; v < outgoing.Length; ++v)
            {
                if (outgoing[v] == null) continue;
                outgoing[v].Sort((a, b) => angle[a].CompareTo(angle[b]));
                for (int k = 0; k < outgoing[v].Count; ++k) hePos[outgoing[v][k]] = k;
            }
            // the face left of a half edge continues with the first half edge clockwise from the
            // twin, so following heNext walks around a face and keeps it on the left
            heNext = new int[heFrom.Length];
            for (int h = 0; h < heFrom.Length; ++h)
            {
                int twin = h ^ 1;
                List<int> lst = outgoing[heFrom[twin]];
                heNext[h] = lst[(hePos[twin] - 1 + lst.Count) % lst.Count];
            }
        }

        private void FindCycles()
        {
            heCycle = new int[heFrom.Length];
            for (int h = 0; h < heCycle.Length; ++h) heCycle[h] = -1;
            List<double> areas = new List<double>();
            for (int h = 0; h < heCycle.Length; ++h)
            {
                if (heCycle[h] >= 0) continue;
                int id = areas.Count;
                double a = 0.0;
                int g = h;
                while (heCycle[g] < 0)
                {
                    heCycle[g] = id;
                    a += vtx[heFrom[g]].x * vtx[heTo[g]].y - vtx[heTo[g]].x * vtx[heFrom[g]].y;
                    g = heNext[g];
                }
                areas.Add(a / 2.0);
            }
            cycleArea = areas.ToArray();
            cycleCount = cycleArea.Length;
        }
        #endregion

        #region faces and winding numbers
        // union find over the cycles: a face is bounded by one cycle per connected component of
        // the graph it touches, so all these cycles have to end up in one set
        private int[] facePar;

        private int FaceOf(int cycle)
        {
            while (facePar[cycle] != cycle) cycle = facePar[cycle] = facePar[facePar[cycle]];
            return cycle;
        }

        private void UniteFaces(int a, int b)
        {
            a = FaceOf(a); b = FaceOf(b);
            if (a != b) facePar[a] = b;
        }

        private void BuildFaces()
        {
            facePar = new int[cycleCount];
            for (int i = 0; i < cycleCount; ++i) facePar[i] = i;

            // connected components of the graph
            int[] vertPar = new int[vtx.Count];
            for (int i = 0; i < vertPar.Length; ++i) vertPar[i] = i;
            for (int e = 0; e < edgeFrom.Count; ++e)
            {
                int a = FindVertex(vertPar, edgeFrom[e]), b = FindVertex(vertPar, edgeTo[e]);
                if (a != b) vertPar[a] = b;
            }
            // per component: the leftmost vertex and the cycle with the unbounded side on its left
            Dictionary<int, int> leftmost = new Dictionary<int, int>();
            Dictionary<int, int> outerCycle = new Dictionary<int, int>();
            for (int v = 0; v < vtx.Count; ++v)
            {
                if (outgoing[v] == null) continue;
                int c = FindVertex(vertPar, v);
                if (!leftmost.TryGetValue(c, out int best) || vtx[v].x < vtx[best].x
                    || (vtx[v].x == vtx[best].x && vtx[v].y < vtx[best].y)) leftmost[c] = v;
            }
            for (int h = 0; h < heFrom.Length; ++h)
            {
                int c = FindVertex(vertPar, heFrom[h]);
                if (!outerCycle.TryGetValue(c, out int best) || cycleArea[heCycle[h]] < cycleArea[best])
                    outerCycle[c] = heCycle[h];
            }
            // the outer cycle of a component bounds the face that contains the whole component:
            // a ray to the left from the leftmost vertex finds it
            int unbounded = -1;
            foreach (KeyValuePair<int, int> kv in leftmost)
            {
                int outer = outerCycle[kv.Key];
                int hit = RayToTheLeft(vtx[kv.Value], vertPar, kv.Key);
                if (hit < 0)
                {   // nothing to the left, so this component is not contained in another one
                    if (unbounded < 0) unbounded = outer;
                    else UniteFaces(outer, unbounded);
                }
                else UniteFaces(outer, heCycle[hit]);
            }
            unboundedFace = unbounded < 0 ? -1 : FaceOf(unbounded);
        }

        private int unboundedFace = -1;

        private static int FindVertex(int[] par, int v)
        {
            while (par[v] != v) v = par[v] = par[par[v]];
            return v;
        }

        /// <summary>
        /// Shoots a ray from <paramref name="p"/> in negative x direction and returns the half
        /// edge of the closest edge it hits, oriented so that <paramref name="p"/> lies on its
        /// left side. Edges of the component <paramref name="skipComponent"/> are ignored.
        /// Returns -1 when the ray hits nothing.
        /// </summary>
        private int RayToTheLeft(GeoPoint2D p, int[] vertPar, int skipComponent)
        {
            int found = -1;
            double bestx = 0.0;
            for (int h = 0; h < heFrom.Length; h += 2)
            {
                if (FindVertex(vertPar, heFrom[h]) == skipComponent) continue;
                GeoPoint2D a = vtx[heFrom[h]], b = vtx[heTo[h]];
                if ((a.y > p.y) == (b.y > p.y)) continue; // does not cross the ray's level
                double x = a.x + (p.y - a.y) * (b.x - a.x) / (b.y - a.y);
                if (x >= p.x) continue;
                if (found < 0 || x > bestx)
                {
                    bestx = x;
                    // the half edge running downwards has its left side towards positive x
                    found = a.y > b.y ? h : h ^ 1;
                }
            }
            return found;
        }

        /// <summary>
        /// The unbounded face has the winding number 0, crossing a half edge from its right to
        /// its left side adds <see cref="heDelta"/>.
        /// </summary>
        private void ComputeWinding()
        {
            heFace = new int[heFrom.Length];
            for (int h = 0; h < heFrom.Length; ++h) heFace[h] = FaceOf(heCycle[h]);
            List<int>[] faceHalfEdges = new List<int>[cycleCount];
            for (int h = 0; h < heFrom.Length; ++h)
            {
                if (faceHalfEdges[heFace[h]] == null) faceHalfEdges[heFace[h]] = new List<int>();
                faceHalfEdges[heFace[h]].Add(h);
            }
            faceWinding = new int[cycleCount];
            bool[] known = new bool[cycleCount];
            if (unboundedFace < 0) return;
            Stack<int> todo = new Stack<int>();
            known[unboundedFace] = true;
            todo.Push(unboundedFace);
            while (todo.Count > 0)
            {
                int f = todo.Pop();
                List<int> hes = faceHalfEdges[f];
                if (hes == null) continue;
                for (int k = 0; k < hes.Count; ++k)
                {
                    int h = hes[k];
                    int other = heFace[h ^ 1];
                    if (known[other]) continue;
                    faceWinding[other] = faceWinding[f] - heDelta[h];
                    known[other] = true;
                    todo.Push(other);
                }
            }
            for (int f = 0; f < cycleCount; ++f)
            {
                if (!known[f]) faceWinding[f] = 0; // unreachable, treat as outside
            }
        }
        #endregion

        #region result
        private GeoPoint2D[][][] Extract(double minArea)
        {
            bool[] inside = new bool[cycleCount];
            for (int f = 0; f < cycleCount; ++f) inside[f] = faceWinding[f] >= 1;

            // neighbouring faces that both belong to the region form one connected part of it
            int[] regionPar = new int[cycleCount];
            for (int i = 0; i < cycleCount; ++i) regionPar[i] = i;
            bool[] isBoundary = new bool[heFrom.Length];
            for (int h = 0; h < heFrom.Length; ++h)
            {
                bool a = inside[heFace[h]], b = inside[heFace[h ^ 1]];
                if (a && b)
                {
                    int ra = FindVertex(regionPar, heFace[h]), rb = FindVertex(regionPar, heFace[h ^ 1]);
                    if (ra != rb) regionPar[ra] = rb;
                }
                isBoundary[h] = a && !b; // the region is on the left, the outside on the right
            }

            Dictionary<int, List<GeoPoint2D[]>> parts = new Dictionary<int, List<GeoPoint2D[]>>();
            bool[] used = new bool[heFrom.Length];
            List<GeoPoint2D> poly = new List<GeoPoint2D>();
            for (int h0 = 0; h0 < heFrom.Length; ++h0)
            {
                if (!isBoundary[h0] || used[h0]) continue;
                poly.Clear();
                int h = h0;
                while (!used[h])
                {
                    used[h] = true;
                    poly.Add(vtx[heFrom[h]]);
                    // the next boundary half edge is found by turning around the common vertex,
                    // skipping the edges that lie inside the region
                    int g = heNext[h];
                    int guard = 0;
                    while (!isBoundary[g] && ++guard < heFrom.Length) g = heNext[g ^ 1];
                    if (!isBoundary[g]) break;
                    h = g;
                }
                if (poly.Count < 3) continue;
                int region = FindVertex(regionPar, heFace[h0]);
                if (!parts.TryGetValue(region, out List<GeoPoint2D[]> lst)) parts[region] = lst = new List<GeoPoint2D[]>();
                lst.Add(poly.ToArray());
            }

            List<GeoPoint2D[][]> res = new List<GeoPoint2D[][]>();
            foreach (List<GeoPoint2D[]> lst in parts.Values)
            {
                List<GeoPoint2D[]> outlines = new List<GeoPoint2D[]>();
                List<GeoPoint2D[]> holes = new List<GeoPoint2D[]>();
                for (int i = 0; i < lst.Count; ++i)
                {
                    double a = GeoPoint2D.Area(lst[i]);
                    if (a > 0.0) { if (a > minArea) outlines.Add(lst[i]); }
                    else if (-a > minArea) holes.Add(lst[i]);
                }
                for (int i = 0; i < outlines.Count; ++i)
                {
                    List<GeoPoint2D[]> group = new List<GeoPoint2D[]>();
                    group.Add(outlines[i]);
                    for (int j = 0; j < holes.Count; ++j)
                    {
                        if (outlines.Count == 1 || SmallestContaining(outlines, holes[j]) == i) group.Add(holes[j]);
                    }
                    res.Add(group.ToArray());
                }
            }
            return res.ToArray();
        }

        /// <summary>
        /// Index of the smallest outline containing <paramref name="hole"/>, -1 when there is
        /// none. Only needed when one connected part of the region has more than one outline,
        /// which happens when the region touches itself in a single point.
        /// </summary>
        private int SmallestContaining(List<GeoPoint2D[]> outlines, GeoPoint2D[] hole)
        {
            int found = -1;
            double best = 0.0;
            for (int i = 0; i < outlines.Count; ++i)
            {
                if (!Contains(outlines[i], hole)) continue;
                double a = GeoPoint2D.Area(outlines[i]);
                if (found < 0 || a < best) { found = i; best = a; }
            }
            return found;
        }

        private bool Contains(GeoPoint2D[] outline, GeoPoint2D[] inner)
        {
            for (int i = 0; i < inner.Length; ++i)
            {   // the loops come from the same arrangement, so a point of inner is either a
                // vertex of outline or it is not on outline at all
                bool shared = false;
                for (int j = 0; j < outline.Length && !shared; ++j)
                {
                    shared = Math.Abs(outline[j].x - inner[i].x) <= tol && Math.Abs(outline[j].y - inner[i].y) <= tol;
                }
                if (shared) continue;
                return Winding(outline, inner[i]) != 0;
            }
            return false;
        }

        private static int Winding(GeoPoint2D[] polygon, GeoPoint2D p)
        {
            int w = 0;
            for (int i = 0; i < polygon.Length; ++i)
            {
                GeoPoint2D a = polygon[i], b = polygon[(i + 1) % polygon.Length];
                if (a.y <= p.y)
                {
                    if (b.y > p.y && (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y) > 0) ++w;
                }
                else
                {
                    if (b.y <= p.y && (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y) < 0) --w;
                }
            }
            return w;
        }
        #endregion
    }
}
