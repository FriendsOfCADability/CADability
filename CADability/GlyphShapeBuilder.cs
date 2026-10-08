using CADability.Curve2D;
using CADability.Shapes;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace CADability.GeoObject
{
    /// <summary>
    /// Converts the outline of a glyph, as delivered by GDI+ (<see cref="System.Drawing.Drawing2D.GraphicsPath"/> point and
    /// type arrays), into <see cref="Path2D"/> objects and builds the filled area of the glyph as a <see cref="CompoundShape"/>.
    /// This class has no dependency on a device context or an installed font, so the shape construction can be tested
    /// with recorded outline data. It is used by <see cref="FontCache"/>.
    /// </summary>
    internal static class GlyphShapeBuilder
    {
        /// <summary>
        /// Result of <see cref="MakeShape(Path2D[], double)"/> together with some diagnostic information.
        /// </summary>
        internal struct ShapeResult
        {
            /// <summary>The filled area of the glyph, may be empty</summary>
            public CompoundShape Shape;
            /// <summary>Number of closed paths in the input</summary>
            public int ClosedPaths;
            /// <summary>Number of closed paths whose border could not be used (e.g. zero area)</summary>
            public int DegeneratePaths;
            /// <summary>Number of closed paths that intersect themselves and have been split into loops</summary>
            public int SelfIntersectingPaths;
            /// <summary>Number of contours that were united with an other contour because they overlap</summary>
            public int UnitedContours;
            /// <summary>Number of contours that were subtracted as holes</summary>
            public int SubtractedContours;
            /// <summary>Number of contours that were subtracted because they overlap and are oriented like a hole</summary>
            public int SubtractedOverlappingContours;
            /// <summary>Number of contours that were dropped (identical to an other contour or not usable)</summary>
            public int DroppedContours;
        }

        private static void AddToPath2D(List<ICurve2D> addto, List<GeoPoint2D> points, bool spline, int fontPrecision)
        {
            if (spline)
            {
                for (int i = 0; i < points.Count - 3; i += 3)
                {

                    double[] knots = new double[2];
                    int[] multiplicities = new int[2];
                    knots[0] = 0;
                    knots[1] = 1;
                    multiplicities[0] = 4;
                    multiplicities[1] = 4;
                    GeoPoint2D[] pp = new GeoPoint2D[4];
                    points.CopyTo(i, pp, 0, 4);
                    // Some fonts (e.g. Gigi and the condensed instances of Bahnschrift) contain Bezier segments whose
                    // control points all coincide. Such a segment has no length, its approximation is an empty Path2D,
                    // and a Path2D cannot be made of an empty one. Start and end point are the same, so leaving it out
                    // keeps the outline connected.
                    if (Precision.IsEqual(pp[0], pp[1]) && Precision.IsEqual(pp[0], pp[2]) && Precision.IsEqual(pp[0], pp[3])) continue;
                    BSpline2D bsp = new BSpline2D(pp, null, knots, multiplicities, 3, false, 0.0, 1.0);
                    // addto.Add(bsp);
                    switch (fontPrecision)
                    {
                        case 0: // grob
                            AddApproximation(addto, bsp.Approximate(true, 0.2));
                            break;
                        case 1: // mittel
                            AddApproximation(addto, bsp.Approximate(true, 0.05));
                            break;
                        case 2:
                            // addto.Add(bsp.Approximate(true, 0.005));
                            addto.Add(bsp); // wenn man den BSpline selbst zufügt, dann könnte man auch mit dynamischer Auflösung arbeiten
                            break;
                    }
                }
            }
            else
            {
                try
                {
                    if (points.Count > 1)
                    {
                        if (points.Count > 2 || points[0] != points[1])
                        {   // zwei identische Punkte gibt exception und ist langsam. Deshalb hier ausschließen
                            Polyline2D pl = new Polyline2D(points.ToArray());
                            addto.Add(pl);
                        }
                    }
                }
                catch (Polyline2DException) { } // nur zwei identische Punkte
            }
            points.RemoveRange(0, points.Count - 1); // den letzten als ersten drinlassen
        }

        /// <summary>
        /// Adds the approximation of a Bezier segment, unless it is empty (a segment that is too short to be approximated).
        /// </summary>
        private static void AddApproximation(List<ICurve2D> addto, ICurve2D approximation)
        {
            if (approximation == null) return;
            if (approximation is Path2D p2d && p2d.SubCurvesCount == 0) return;
            addto.Add(approximation);
        }

        /// <summary>
        /// Converts the point and type arrays of a <see cref="System.Drawing.Drawing2D.GraphicsPath"/> (as returned by
        /// GraphicsPath.PathPoints and GraphicsPath.PathTypes after AddString) into closed or open 2d paths. The y coordinate
        /// is flipped (y = 1 - y), because GDI+ uses a downward y axis. Each closed subpath (CloseSubpath flag) is closed
        /// with a line back to its start point.
        /// </summary>
        /// <param name="pathPoints">GraphicsPath.PathPoints</param>
        /// <param name="pathTypes">GraphicsPath.PathTypes</param>
        /// <param name="fontPrecision">0: rough, 1: middle, 2: fine (keeps the splines)</param>
        /// <returns>The paths of the glyph, one for each subpath</returns>
        internal static Path2D[] PathDataToPath2D(PointF[] pathPoints, byte[] pathTypes, int fontPrecision)
        {
            List<Path2D> res = new List<Path2D>();
            if (pathPoints == null || pathTypes == null || pathPoints.Length == 0) return res.ToArray();
            List<PointF> pp = new List<PointF>(pathPoints);
            List<byte> pt = new List<byte>(pathTypes);
            int last0 = -1;
            for (int i = 0; i < pt.Count; ++i)
            {
                pp[i] = new PointF(pp[i].X, (float)(1.0f - pp[i].Y));
                if ((pt[i] & 0x01) == 0) last0 = i;
                if ((pt[i] & 0x80) != 0 && last0 >= 0)
                {
                    pt[i] = (byte)(pt[i] & 0x7F);
                    pt.Insert(i + 1, (byte)(pt[last0] | 0x81));
                    pp.Insert(i + 1, pp[last0]);
                    ++i;
                    last0 = -1;
                }

            }
            if (pp == null || pp.Count == 0)
            {
                return res.ToArray();
            }
            List<GeoPoint2D> current = new List<GeoPoint2D>();
            int mode = 0; // 0 noch nicht bekannt, 1: Linie, 3 Spline
            List<ICurve2D> segment = new List<ICurve2D>();
            for (int i = 0; i < pp.Count; ++i)
            {
                switch (pt[i] & 0x03)
                {
                    case 0: // neuer Anfang
                        if (current.Count > 1)
                        {
                            AddToPath2D(segment, current, mode == 3, fontPrecision);
                            AddPath(res, segment);
                        }
                        segment.Clear();
                        current.Clear();
                        current.Add(new GeoPoint2D(pp[i]));
                        mode = 0;
                        break;
                    case 1:
                        if (mode == 3)
                        {   // Spline beenden, polylinie anfangen
                            AddToPath2D(segment, current, true, fontPrecision);
                        }
                        current.Add(new GeoPoint2D(pp[i]));
                        mode = 1;
                        break;
                    case 2:
                    case 3:
                        if (mode == 1)
                        {
                            AddToPath2D(segment, current, false, fontPrecision);
                        }
                        current.Add(new GeoPoint2D(pp[i]));
                        mode = 3;
                        break;
                }
            }

            if (current.Count > 1)
            {
                AddToPath2D(segment, current, mode == 3, fontPrecision);
                AddPath(res, segment);
            }
            return res.ToArray();
        }

        private static void AddPath(List<Path2D> res, List<ICurve2D> segment)
        {
            if (segment.Count == 0) return;
            try
            {
                res.Add(new Path2D(segment.ToArray()));
            }
            catch (ApplicationException)
            {   // segments not connected: a degenerated segment has been dropped in AddToPath2D. Force the connection,
                // a slightly distorted glyph is better than a missing one
                res.Add(new Path2D(segment.ToArray(), true));
            }
        }

        /// <summary>
        /// Builds the filled area of a glyph from its outline paths. Only closed paths are used. The paths are combined
        /// according to their nesting: a contour completely inside an other contour is a hole, a contour inside a hole
        /// is a filled island again. Fonts are drawn with the non-zero winding rule, so contours may overlap:
        /// <list type="bullet">
        /// <item>A contour that intersects itself (e.g. the "e", "6" or "9" of many variable fonts, where the bowl and
        /// the bar are drawn as a single overlapping contour) is split into simple loops at its self intersections.
        /// A loop whose interior has a non-zero winding number with respect to the original contour is filled,
        /// otherwise it is a hole.</item>
        /// <item>Separate contours that overlap each other are united when they have the same orientation as the
        /// outer contour of the glyph, otherwise they are subtracted.</item>
        /// </list>
        /// Contours without such overlaps are treated exactly as before: contained contours are holes.
        /// </summary>
        /// <param name="paths">The outline paths of the glyph</param>
        /// <returns>The shape and some statistics</returns>
        internal static ShapeResult MakeShape(Path2D[] paths)
        {
            ShapeResult result = new ShapeResult();
            List<Contour> contours = new List<Contour>();
            bool referenceReversed = false; // orientation of the biggest contour, this is an outer contour of the glyph
            double biggestSize = -1.0;
            for (int i = 0; i < paths.Length; ++i)
            {
                if (!paths[i].IsClosed) continue;
                ++result.ClosedPaths;
                try
                {
                    GeoPoint2D[] polygon = ToPolygon(paths[i]);
                    List<SimpleShape> resolved = ResolveSelfIntersections(polygon);
                    if (resolved != null)
                    {   // a self intersecting contour: the filled regions (non-zero winding rule) replace the contour
                        ++result.SelfIntersectingPaths;
                        bool reversed = OriginalOrientationReversed(polygon);
                        for (int j = 0; j < resolved.Count; ++j) contours.Add(new Contour(resolved[j], reversed));
                        if (resolved.Count == 0) ++result.DegeneratePaths;
                    }
                    else
                    {
                        BoundingRect pext = new BoundingRect(polygon);
                        if (polygon.Length < 3 || Math.Abs(GeoPoint2D.Area(polygon)) <= pext.Width * pext.Height * 1e-9)
                        {   // no area (the exact area of the border with splines would be expensive to calculate)
                            ++result.DegeneratePaths;
                            continue;
                        }
                        Border bdr = paths[i].MakeBorder(out bool reversed);
                        if (!bdr.IsClosed)
                        {
                            ++result.DegeneratePaths;
                            continue;
                        }
                        contours.Add(new Contour(new SimpleShape(bdr), reversed));
                    }
                    BoundingRect ext = paths[i].GetExtent();
                    double size = ext.Width * ext.Height;
                    if (size > biggestSize)
                    {
                        biggestSize = size;
                        referenceReversed = OriginalOrientationReversed(polygon);
                    }
                }
                catch (Exception ex) when (!(ex is System.Threading.ThreadAbortException))
                {
                    ++result.DegeneratePaths;
                }
            }
            // sort the contours by the size of their extent, the biggest is the first outer contour
            contours.Sort((c1, c2) =>
            {
                BoundingRect e1 = c1.Shape.GetExtent();
                BoundingRect e2 = c2.Shape.GetExtent();
                return (e1.Width * e1.Height).CompareTo(e2.Width * e2.Height);
            });
            List<Contour> sortedList = contours;
            CompoundShape res = new CompoundShape(); // empty
            while (sortedList.Count > 0)
            {
                Contour outer = sortedList[sortedList.Count - 1]; // the biggest shape, this is an outer contour
                sortedList.RemoveAt(sortedList.Count - 1); // remove it from the list
                CompoundShape cs = new CompoundShape(outer.Shape);
                bool changed = true;
                while (changed)
                {   // after uniting with an overlapping contour, other contours may now be contained in the result,
                    // so the test is repeated until nothing changes
                    changed = false;
                    for (int i = sortedList.Count - 1; i >= 0; --i)
                    {
                        Contour other = sortedList[i];
                        // overlapping contours: the font is drawn with the non-zero winding rule, a contour with the same
                        // orientation as the outer contour adds to the area, a contour with the opposite orientation removes from it.
                        bool additive = other.Reversed == referenceReversed;
                        SimpleShape.Position pos;
                        try
                        {
                            pos = GetPosition(cs, other.Shape);
                        }
                        catch (Exception ex) when (!(ex is System.Threading.ThreadAbortException))
                        {
                            pos = SimpleShape.Position.disjunct; // will be a shape of its own
                        }
                        switch (pos)
                        {
                            case SimpleShape.Position.firstcontainscecond:
                                {
                                    // a hole (when it is inside a hole of cs, GetPosition returns disjunct and it will be a shape of its own)
                                    // this is decided by the containment only (as it always has been) and not by the orientation,
                                    // because there are fonts with inconsistent orientation
                                    CompoundShape sub = Apply(cs, other.Shape, false);
                                    if (sub != null) cs = sub;
                                    ++result.SubtractedContours;
                                    sortedList.RemoveAt(i);
                                    break;
                                }
                            case SimpleShape.Position.intersecting:
                                {
                                    CompoundShape combined = Apply(cs, other.Shape, additive);
                                    if (combined != null)
                                    {
                                        cs = combined;
                                        if (additive) ++result.UnitedContours;
                                        else ++result.SubtractedOverlappingContours;
                                        sortedList.RemoveAt(i);
                                        changed = true;
                                    }
                                    else if (additive)
                                    {   // the boolean operation failed (e.g. the shapes only touch each other), keep the contour as a
                                        // shape of its own (this was the behaviour before overlapping contours were considered)
                                        ++result.DroppedContours;
                                        sortedList.RemoveAt(i);
                                        res.UniteDisjunct(other.Shape);
                                    }
                                    else
                                    {   // a hole that could not be subtracted, ignore it
                                        ++result.DroppedContours;
                                        sortedList.RemoveAt(i);
                                    }
                                    break;
                                }
                            case SimpleShape.Position.identical:
                                ++result.DroppedContours; // a duplicate contour
                                sortedList.RemoveAt(i);
                                break;
                            default:
                                // disjunct or secondcontainsfirst: will be handled as a shape of its own
                                break;
                        }
                    }
                }
                res = res + cs;
            }
            result.Shape = res;
            return result;
        }

        private struct Contour
        {
            public readonly SimpleShape Shape;
            public readonly bool Reversed; // true, when the original path had to be reversed to make a counterclockwise border
            public Contour(SimpleShape shape, bool reversed)
            {
                Shape = shape;
                Reversed = reversed;
            }
        }

        /// <summary>
        /// Returns the vertices of the (closed) path as a polygon. Curves are approximated by lines. The last vertex is not
        /// repeated at the end.
        /// </summary>
        internal static GeoPoint2D[] ToPolygon(Path2D path)
        {
            List<GeoPoint2D> pts = new List<GeoPoint2D>();
            AddVertices(pts, path);
            while (pts.Count > 1 && (pts[0] | pts[pts.Count - 1]) < Precision.eps) pts.RemoveAt(pts.Count - 1);
            return pts.ToArray();
        }

        private static void AddVertices(List<GeoPoint2D> pts, ICurve2D curve)
        {
            if (curve is Path2D path)
            {
                for (int i = 0; i < path.SubCurves.Length; ++i) AddVertices(pts, path.SubCurves[i]);
            }
            else if (curve is Polyline2D polyline)
            {
                GeoPoint2D[] vtx = polyline.Vertex;
                for (int i = 0; i < vtx.Length; ++i) AddVertex(pts, vtx[i]);
            }
            else if (curve is Line2D)
            {
                AddVertex(pts, curve.StartPoint);
                AddVertex(pts, curve.EndPoint);
            }
            else
            {
                ICurve2D approx = null;
                try
                {
                    approx = curve.Approximate(true, 0.001); // glyph size is 1
                }
                catch (Exception ex) when (!(ex is System.Threading.ThreadAbortException)) { }
                if (approx is Polyline2D || approx is Path2D || approx is Line2D)
                {
                    AddVertices(pts, approx);
                }
                else
                {   // should not happen, sample the curve
                    for (int i = 0; i <= 16; ++i) AddVertex(pts, curve.PointAt(i / 16.0));
                }
            }
        }

        private static void AddVertex(List<GeoPoint2D> pts, GeoPoint2D p)
        {
            if (pts.Count == 0 || (pts[pts.Count - 1] | p) > Precision.eps) pts.Add(p);
        }

        /// <summary>
        /// Winding number of the point with respect to the closed polygon (Dan Sunday's algorithm).
        /// </summary>
        internal static int WindingNumber(GeoPoint2D[] polygon, GeoPoint2D p)
        {
            int wn = 0;
            int n = polygon.Length;
            for (int i = 0; i < n; ++i)
            {
                GeoPoint2D a = polygon[i];
                GeoPoint2D b = polygon[(i + 1) % n];
                if (a.y <= p.y)
                {
                    if (b.y > p.y && IsLeft(a, b, p) > 0) ++wn;
                }
                else
                {
                    if (b.y <= p.y && IsLeft(a, b, p) < 0) --wn;
                }
            }
            return wn;
        }

        private static double IsLeft(GeoPoint2D a, GeoPoint2D b, GeoPoint2D p)
        {
            return (b.x - a.x) * (p.y - a.y) - (p.x - a.x) * (b.y - a.y);
        }

        /// <summary>
        /// True, when the polygon is oriented clockwise, i.e. it would have to be reversed to make a counterclockwise border.
        /// </summary>
        private static bool OriginalOrientationReversed(GeoPoint2D[] polygon)
        {
            return GeoPoint2D.Area(polygon) < 0.0;
        }

        /// <summary>
        /// Resolves the self intersections of a closed polygon according to the non-zero winding rule. Returns null when the
        /// polygon does not intersect itself. Otherwise the polygon is split at its self intersections into a planar graph,
        /// the faces of this graph with a non-zero winding number are the filled area. Adjacent filled faces are united,
        /// so the result is a list of disjoint simple shapes (with holes). Faces with (almost) no area are omitted.
        /// </summary>
        internal static List<SimpleShape> ResolveSelfIntersections(GeoPoint2D[] polygon)
        {
            int n = polygon.Length;
            if (n < 4) return null;
            BoundingRect ext = new BoundingRect(polygon);
            double size = Math.Max(ext.Width, ext.Height);
            if (size == 0.0) return null;
            double tol = size * 1e-8; // points closer than this are identical
            // all intersection points of non adjacent edges
            List<GeoPoint2D> locations = new List<GeoPoint2D>(); // unique intersection points, index is the id
            List<SortedDictionary<double, int>> onEdge = new List<SortedDictionary<double, int>>(n); // for each edge: parameter -> id
            for (int i = 0; i < n; ++i) onEdge.Add(null);
            for (int i = 0; i < n; ++i)
            {
                GeoPoint2D a1 = polygon[i], a2 = polygon[(i + 1) % n];
                for (int j = i + 2; j < n; ++j)
                {
                    if (i == 0 && j == n - 1) continue; // adjacent
                    GeoPoint2D b1 = polygon[j], b2 = polygon[(j + 1) % n];
                    if (Math.Max(a1.x, a2.x) < Math.Min(b1.x, b2.x) - tol || Math.Min(a1.x, a2.x) > Math.Max(b1.x, b2.x) + tol) continue;
                    if (Math.Max(a1.y, a2.y) < Math.Min(b1.y, b2.y) - tol || Math.Min(a1.y, a2.y) > Math.Max(b1.y, b2.y) + tol) continue;
                    if (!SegmentIntersection(a1, a2, b1, b2, tol, out double s, out double t, out GeoPoint2D ip)) continue;
                    int id = -1;
                    for (int k = 0; k < locations.Count; ++k)
                    {
                        if ((locations[k] | ip) < tol)
                        {
                            id = k;
                            break;
                        }
                    }
                    if (id < 0)
                    {
                        id = locations.Count;
                        locations.Add(ip);
                    }
                    AddOnEdge(onEdge, i, s, id, tol);
                    AddOnEdge(onEdge, j, t, id, tol);
                }
            }
            if (locations.Count == 0) return null;
            // the polygon with the intersection points inserted, each vertex with the id of the intersection (-1: no intersection)
            List<GeoPoint2D> pts = new List<GeoPoint2D>();
            List<int> ids = new List<int>();
            for (int i = 0; i < n; ++i)
            {
                int vertexId = -1;
                List<KeyValuePair<double, int>> inner = new List<KeyValuePair<double, int>>();
                if (onEdge[i] != null)
                {
                    foreach (KeyValuePair<double, int> kv in onEdge[i])
                    {
                        if (kv.Key <= 0.0) vertexId = kv.Value; // at the start vertex of this edge
                        else if (kv.Key >= 1.0)
                        {   // at the end vertex, i.e. the start vertex of the next edge
                            int nextEdge = (i + 1) % n;
                            if (onEdge[nextEdge] == null) onEdge[nextEdge] = new SortedDictionary<double, int>();
                            if (!onEdge[nextEdge].ContainsKey(0.0)) onEdge[nextEdge][0.0] = kv.Value;
                        }
                        else inner.Add(kv);
                    }
                }
                if (vertexId >= 0)
                {
                    pts.Add(locations[vertexId]);
                    ids.Add(vertexId);
                }
                else
                {
                    pts.Add(polygon[i]);
                    ids.Add(-1);
                }
                foreach (KeyValuePair<double, int> kv in inner)
                {
                    pts.Add(locations[kv.Value]);
                    ids.Add(kv.Value);
                }
            }
            // the first vertex may have got an id from the last edge (parameter 1.0), this was added to onEdge[0] too late: fix it here
            if (onEdge[0] != null && onEdge[0].ContainsKey(0.0) && ids[0] < 0)
            {
                ids[0] = onEdge[0][0.0];
                pts[0] = locations[ids[0]];
            }
            // the planar graph: vertices (intersection points are shared), half edges
            List<GeoPoint2D> vertices = new List<GeoPoint2D>();
            Dictionary<int, int> idToVertex = new Dictionary<int, int>();
            int[] seqVertex = new int[pts.Count];
            for (int k = 0; k < pts.Count; ++k)
            {
                if (ids[k] >= 0)
                {
                    if (!idToVertex.TryGetValue(ids[k], out int vi))
                    {
                        vi = vertices.Count;
                        vertices.Add(pts[k]);
                        idToVertex[ids[k]] = vi;
                    }
                    seqVertex[k] = vi;
                }
                else
                {
                    seqVertex[k] = vertices.Count;
                    vertices.Add(pts[k]);
                }
            }
            List<int> heFrom = new List<int>(); // half edge 2*e: from->to, 2*e+1: to->from
            List<int> heTo = new List<int>();
            for (int k = 0; k < pts.Count; ++k)
            {
                int a = seqVertex[k], b = seqVertex[(k + 1) % pts.Count];
                if (a == b) continue;
                heFrom.Add(a); heTo.Add(b);
                heFrom.Add(b); heTo.Add(a);
            }
            int nhe = heFrom.Count;
            if (nhe < 6) return null;
            // outgoing half edges of each vertex, sorted counterclockwise by angle
            List<int>[] outgoing = new List<int>[vertices.Count];
            for (int h = 0; h < nhe; ++h)
            {
                if (outgoing[heFrom[h]] == null) outgoing[heFrom[h]] = new List<int>();
                outgoing[heFrom[h]].Add(h);
            }
            double[] angle = new double[nhe];
            for (int h = 0; h < nhe; ++h)
            {
                GeoPoint2D a = vertices[heFrom[h]], b = vertices[heTo[h]];
                angle[h] = Math.Atan2(b.y - a.y, b.x - a.x);
            }
            int[] posInOutgoing = new int[nhe];
            for (int v = 0; v < vertices.Count; ++v)
            {
                if (outgoing[v] == null) continue;
                outgoing[v].Sort((h1, h2) => angle[h1].CompareTo(angle[h2]));
                for (int k = 0; k < outgoing[v].Count; ++k) posInOutgoing[outgoing[v][k]] = k;
            }
            // next half edge of the face on the left side of a half edge: the predecessor of the twin in counterclockwise order
            int[] next = new int[nhe];
            for (int h = 0; h < nhe; ++h)
            {
                int twin = h ^ 1;
                List<int> og = outgoing[heTo[h]];
                int pos = posInOutgoing[twin];
                next[h] = og[(pos - 1 + og.Count) % og.Count];
            }
            // trace the faces
            int[] faceOf = new int[nhe];
            for (int h = 0; h < nhe; ++h) faceOf[h] = -1;
            List<List<int>> faces = new List<List<int>>(); // half edges of each face
            for (int h = 0; h < nhe; ++h)
            {
                if (faceOf[h] >= 0) continue;
                List<int> face = new List<int>();
                int current = h;
                int guard = 0;
                while (faceOf[current] < 0 && guard++ <= nhe)
                {
                    faceOf[current] = faces.Count;
                    face.Add(current);
                    current = next[current];
                }
                faces.Add(face);
            }
            // keep the faces with a non-zero winding number
            double minArea = size * size * 1e-7;
            bool[] keep = new bool[faces.Count];
            int nkeep = 0;
            for (int f = 0; f < faces.Count; ++f)
            {
                GeoPoint2D[] fp = new GeoPoint2D[faces[f].Count];
                for (int k = 0; k < fp.Length; ++k) fp[k] = vertices[heFrom[faces[f][k]]];
                double area = GeoPoint2D.Area(fp);
                if (area <= minArea) continue; // the unbounded face (negative area) or a sliver
                // a point inside the face: a little to the left of the middle of the longest edge
                int longest = -1;
                double maxlen = -1.0;
                for (int k = 0; k < faces[f].Count; ++k)
                {
                    int h = faces[f][k];
                    double len = vertices[heFrom[h]] | vertices[heTo[h]];
                    if (len > maxlen)
                    {
                        maxlen = len;
                        longest = h;
                    }
                }
                GeoPoint2D a = vertices[heFrom[longest]], b = vertices[heTo[longest]];
                GeoVector2D dir = (b - a);
                dir.Norm();
                GeoPoint2D test = new GeoPoint2D((a.x + b.x) / 2.0 - dir.y * tol * 10, (a.y + b.y) / 2.0 + dir.x * tol * 10);
                if (WindingNumber(polygon, test) != 0)
                {
                    keep[f] = true;
                    ++nkeep;
                }
            }
            List<SimpleShape> res = new List<SimpleShape>();
            if (nkeep == 0) return res;
            // the boundary of the union of the kept faces: half edges of kept faces whose twin is not in a kept face
            bool[] boundary = new bool[nhe];
            for (int h = 0; h < nhe; ++h) boundary[h] = keep[faceOf[h]] && !keep[faceOf[h ^ 1]];
            List<GeoPoint2D[]> outer = new List<GeoPoint2D[]>();
            List<GeoPoint2D[]> holes = new List<GeoPoint2D[]>();
            bool[] used = new bool[nhe];
            for (int h = 0; h < nhe; ++h)
            {
                if (!boundary[h] || used[h]) continue;
                List<GeoPoint2D> loop = new List<GeoPoint2D>();
                int current = h;
                int guard = 0;
                while (boundary[current] && !used[current] && guard++ <= nhe)
                {
                    used[current] = true;
                    loop.Add(vertices[heFrom[current]]);
                    // the next boundary half edge: rotate from the twin counterclockwise backwards until a boundary edge is found
                    List<int> og = outgoing[heTo[current]];
                    int pos = posInOutgoing[current ^ 1];
                    int found = -1;
                    for (int k = 1; k <= og.Count; ++k)
                    {
                        int cand = og[(pos - k + og.Count) % og.Count];
                        if (boundary[cand])
                        {
                            found = cand;
                            break;
                        }
                    }
                    if (found < 0) break;
                    current = found;
                }
                if (loop.Count < 3) continue;
                GeoPoint2D[] lp = loop.ToArray();
                double area = GeoPoint2D.Area(lp);
                if (Math.Abs(area) <= minArea) continue;
                if (area > 0) outer.Add(lp);
                else holes.Add(lp);
            }
            // assign the holes to the smallest outer loop containing them
            List<Border>[] holesOf = new List<Border>[outer.Count];
            for (int i = 0; i < holes.Count; ++i)
            {
                // a point inside the hole: a little to the right of the middle of its longest edge (the hole is clockwise)
                int longest = 0;
                double maxlen = -1.0;
                for (int k = 0; k < holes[i].Length; ++k)
                {
                    double len = holes[i][k] | holes[i][(k + 1) % holes[i].Length];
                    if (len > maxlen)
                    {
                        maxlen = len;
                        longest = k;
                    }
                }
                GeoPoint2D a = holes[i][longest], b = holes[i][(longest + 1) % holes[i].Length];
                GeoVector2D dir = (b - a);
                dir.Norm();
                GeoPoint2D test = new GeoPoint2D((a.x + b.x) / 2.0 + dir.y * tol * 10, (a.y + b.y) / 2.0 - dir.x * tol * 10);
                int best = -1;
                double bestArea = double.MaxValue;
                for (int j = 0; j < outer.Count; ++j)
                {
                    if (WindingNumber(outer[j], test) != 0)
                    {
                        double oa = GeoPoint2D.Area(outer[j]);
                        if (oa < bestArea)
                        {
                            bestArea = oa;
                            best = j;
                        }
                    }
                }
                if (best < 0) continue; // should not happen
                if (holesOf[best] == null) holesOf[best] = new List<Border>();
                try
                {
                    holesOf[best].Add(new Border(holes[i]));
                }
                catch (Exception ex) when (!(ex is System.Threading.ThreadAbortException)) { }
            }
            for (int j = 0; j < outer.Count; ++j)
            {
                try
                {
                    Border ob = new Border(outer[j]);
                    if (holesOf[j] == null) res.Add(new SimpleShape(ob));
                    else res.Add(new SimpleShape(ob, holesOf[j].ToArray()));
                }
                catch (Exception ex) when (!(ex is System.Threading.ThreadAbortException)) { }
            }
            return res;
        }

        private static void AddOnEdge(List<SortedDictionary<double, int>> onEdge, int edge, double par, int id, double tol)
        {
            if (onEdge[edge] == null) onEdge[edge] = new SortedDictionary<double, int>();
            if (par < 0.0) par = 0.0;
            if (par > 1.0) par = 1.0;
            if (!onEdge[edge].ContainsKey(par)) onEdge[edge][par] = id;
        }

        /// <summary>
        /// Intersection of the segments a1-a2 and b1-b2. Touching points within tol are intersections as well, parallel segments are not.
        /// </summary>
        private static bool SegmentIntersection(GeoPoint2D a1, GeoPoint2D a2, GeoPoint2D b1, GeoPoint2D b2, double tol, out double s, out double t, out GeoPoint2D ip)
        {
            double dax = a2.x - a1.x, day = a2.y - a1.y;
            double dbx = b2.x - b1.x, dby = b2.y - b1.y;
            double det = dax * dby - day * dbx;
            s = t = 0.0;
            ip = a1;
            double la = Math.Sqrt(dax * dax + day * day);
            double lb = Math.Sqrt(dbx * dbx + dby * dby);
            if (la < tol || lb < tol) return false;
            if (Math.Abs(det) < 1e-12 * la * lb) return false; // parallel
            double ex = b1.x - a1.x, ey = b1.y - a1.y;
            s = (ex * dby - ey * dbx) / det;
            t = (ex * day - ey * dax) / det;
            double tols = tol / la, tolt = tol / lb;
            if (s < -tols || s > 1.0 + tols || t < -tolt || t > 1.0 + tolt) return false;
            if (s < 0.0) s = 0.0;
            if (s > 1.0) s = 1.0;
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            ip = new GeoPoint2D(a1.x + s * dax, a1.y + s * day);
            return true;
        }

        private static SimpleShape.Position GetPosition(CompoundShape cs, SimpleShape other)
        {
            SimpleShape.Position res = SimpleShape.Position.disjunct;
            for (int i = 0; i < cs.SimpleShapes.Length; ++i)
            {
                SimpleShape.Position pos = SimpleShape.GetPosition(cs.SimpleShapes[i], other);
                switch (pos)
                {
                    case SimpleShape.Position.firstcontainscecond:
                    case SimpleShape.Position.identical:
                        return pos;
                    case SimpleShape.Position.intersecting:
                    case SimpleShape.Position.secondcontainsfirst:
                        res = SimpleShape.Position.intersecting;
                        break;
                }
            }
            return res;
        }

        /// <summary>
        /// Unites or subtracts <paramref name="other"/> with/from <paramref name="cs"/>. Returns null when the result is not usable
        /// (exception or empty result of a union).
        /// </summary>
        private static CompoundShape Apply(CompoundShape cs, SimpleShape other, bool unite)
        {
            try
            {
                CompoundShape res;
                if (unite) res = cs + new CompoundShape(other);
                else res = cs - new CompoundShape(other);
                if (res == null) return null;
                if (unite && (res.Empty || res.Area < cs.Area * (1.0 - 1e-6))) return null; // a union can not become smaller
                return res;
            }
            catch (Exception ex) when (!(ex is System.Threading.ThreadAbortException))
            {
                return null;
            }
        }
    }
}
