using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability
{
    /// <summary>
    /// Exact/adaptive geometric predicates for <see cref="CDTriangulation"/>.
    /// Orient2D uses a floating point filter and falls back to exact expansion arithmetic
    /// (following Shewchuk's adaptive predicates) when the filter cannot decide the sign:
    /// orientation errors would corrupt the mesh topology. InCircle only uses the floating
    /// point filter and reports "cocircular" (0) when undecidable: incircle ties merely
    /// influence which diagonal is chosen, never the validity of the mesh.
    /// </summary>
    internal static class CDTPredicates
    {
        private const double epsilon = 1.1102230246251565e-16; // 2^-53
        private const double splitter = 134217729.0; // 2^27 + 1, for Dekker splitting
        private static readonly double ccwErrBound = (3.0 + 16.0 * epsilon) * epsilon;
        private static readonly double iccErrBound = (10.0 + 96.0 * epsilon) * epsilon;

        /// <summary>
        /// Sign of the (doubled, signed) area of the triangle (a,b,c): positive if (a,b,c) is
        /// counterclockwise, negative if clockwise, exactly 0 if collinear.
        /// </summary>
        public static double Orient2D(GeoPoint2D a, GeoPoint2D b, GeoPoint2D c)
        {
            double detleft = (a.x - c.x) * (b.y - c.y);
            double detright = (a.y - c.y) * (b.x - c.x);
            double det = detleft - detright;
            if (detleft > 0)
            {
                if (detright <= 0) return det;
            }
            else if (detleft < 0)
            {
                if (detright >= 0) return det;
            }
            else return det;
            double detsum = Math.Abs(detleft) + Math.Abs(detright);
            if (Math.Abs(det) >= ccwErrBound * detsum) return det;
            return Orient2DExact(a, b, c);
        }

        // Exact evaluation of the 2x2 determinant as a sum of 6 exact products, accumulated
        // into a floating point expansion. Only the sign is relevant to callers.
        private static double Orient2DExact(GeoPoint2D a, GeoPoint2D b, GeoPoint2D c)
        {
            double[] e = new double[16];
            int elen = 0;
            elen = AccumulateProduct(e, elen, a.x, b.y);
            elen = AccumulateProduct(e, elen, -a.x, c.y);
            elen = AccumulateProduct(e, elen, -c.x, b.y);
            elen = AccumulateProduct(e, elen, -a.y, b.x);
            elen = AccumulateProduct(e, elen, a.y, c.x);
            elen = AccumulateProduct(e, elen, b.x, c.y);
            for (int i = elen - 1; i >= 0; --i)
            {
                if (e[i] != 0.0) return e[i]; // largest magnitude component determines the sign
            }
            return 0.0;
        }

        // adds the exact value of a*b (as a two-component expansion) to the expansion e
        private static int AccumulateProduct(double[] e, int elen, double a, double b)
        {
            double x = a * b;
            // Dekker splitting to get the exact roundoff of a*b
            double ca = splitter * a;
            double ahi = ca - (ca - a);
            double alo = a - ahi;
            double cb = splitter * b;
            double bhi = cb - (cb - b);
            double blo = b - bhi;
            double err = x - (ahi * bhi);
            err -= alo * bhi;
            err -= ahi * blo;
            double y = (alo * blo) - err; // a*b == x + y exactly
            if (y != 0.0) elen = GrowExpansion(e, elen, y);
            if (x != 0.0) elen = GrowExpansion(e, elen, x);
            return elen;
        }

        // adds the scalar b to the expansion e in place (Shewchuk's grow-expansion), the sum
        // stays exact; components are ordered by increasing magnitude
        private static int GrowExpansion(double[] e, int elen, double b)
        {
            double q = b;
            for (int i = 0; i < elen; ++i)
            {
                double enow = e[i];
                double sum = q + enow;
                double bvirt = sum - q;
                double avirt = sum - bvirt;
                double bround = enow - bvirt;
                double around = q - avirt;
                e[i] = around + bround;
                q = sum;
            }
            e[elen] = q;
            return elen + 1;
        }

        /// <summary>
        /// Positive if d lies strictly inside the circumcircle of the counterclockwise triangle
        /// (a,b,c), negative if strictly outside, 0 if (numerically) cocircular. When the
        /// floating point filter cannot decide, 0 is returned (treated as "not inside" by
        /// callers), which is always safe for mesh validity.
        /// </summary>
        public static double InCircle(GeoPoint2D a, GeoPoint2D b, GeoPoint2D c, GeoPoint2D d)
        {
            double adx = a.x - d.x, ady = a.y - d.y;
            double bdx = b.x - d.x, bdy = b.y - d.y;
            double cdx = c.x - d.x, cdy = c.y - d.y;

            double bdxcdy = bdx * cdy, cdxbdy = cdx * bdy;
            double alift = adx * adx + ady * ady;
            double cdxady = cdx * ady, adxcdy = adx * cdy;
            double blift = bdx * bdx + bdy * bdy;
            double adxbdy = adx * bdy, bdxady = bdx * ady;
            double clift = cdx * cdx + cdy * cdy;

            double det = alift * (bdxcdy - cdxbdy) + blift * (cdxady - adxcdy) + clift * (adxbdy - bdxady);
            double permanent = (Math.Abs(bdxcdy) + Math.Abs(cdxbdy)) * alift
                             + (Math.Abs(cdxady) + Math.Abs(adxcdy)) * blift
                             + (Math.Abs(adxbdy) + Math.Abs(bdxady)) * clift;
            double errbound = iccErrBound * permanent;
            if (det > errbound || -det > errbound) return det;
            return 0.0;
        }
    }

    /// <summary>
    /// Prototype of a new face triangulation, intended to replace <see cref="Triangulation"/>:
    /// a constrained Delaunay triangulation (CDT) of the uv outline with Chew/Ruppert style
    /// refinement driven by 3d criteria.
    ///
    /// Concept:
    /// 1. The uv space is normalized per axis by an approximate arc length reparametrization
    ///    (piecewise linear, monotone), so that distances in the normalized space roughly
    ///    correspond to 3d distances. This makes an isotropic Delaunay criterion meaningful
    ///    even for strongly distorted parametrizations (cone, sphere, uneven NURBS knots).
    /// 2. All input points are inserted into a Delaunay triangulation (incremental
    ///    Bowyer-Watson) using exact orientation predicates; the outline/hole segments are
    ///    then enforced as constraint edges. Constraint edges are never split - adjacent
    ///    faces must share the identical boundary tessellation.
    /// 3. Triangles are refined by inserting Steiner points until (a) interior edges deviate
    ///    less than maxDeflection from the surface (checked with ISurface.MaxDist, the point
    ///    of maximal deviation is used as the Steiner point), (b) the surface point at the
    ///    triangle center is close enough to the triangle plane and (c) triangles are well
    ///    shaped in 3d (circumcenter insertion; skipped when it would require splitting a
    ///    boundary segment).
    /// 4. A final pass exchanges diagonals when this improves the minimal 3d angle
    ///    (data dependent flips), since the Delaunay property holds in the normalized uv
    ///    space, not exactly in 3d.
    ///
    /// Singularities (e.g. the apex line of a cone, poles of a sphere) are detected via
    /// GetU/VSingularities: all boundary vertices on a singular line share the identical 3d
    /// point, resulting 3d-degenerate triangles are removed from the output, and the 3d shape
    /// criterion is suspended for triangles at the singularity (a uv-thin triangle at a pole
    /// is a perfectly fine "pie slice" in 3d).
    /// </summary>
    public class CDTriangulation
    {
        private static int Next(int i) { return (i + 1) % 3; }
        private static int Prev(int i) { return (i + 2) % 3; }

        // triangle with vertex indices (counterclockwise in normalized uv), neighbor triangle
        // indices and constraint flags. Edge i runs from v[i] to v[Next(i)]; n[i] is the
        // triangle sharing edge i (-1: none); bit i of constrained marks edge i as a fixed
        // outline/hole segment which must never be split or flipped.
        private class Tri
        {
            public int[] v = new int[3];
            public int[] n = new int[3] { -1, -1, -1 };
            public byte constrained;
            public bool alive = true;
        }

        private struct CavityEdge
        {
            public int a, b;        // directed edge of the cavity boundary (cavity on the left)
            public int outer;       // triangle beyond that edge (-1: none)
            public bool constrained;
        }

        private struct EdgeDefl
        {
            public double dist;
            public GeoPoint2D mp;
        }

        // monotone piecewise linear mapping between an original parameter axis and its
        // arc-length-normalized counterpart
        private class AxisMap
        {
            private readonly double[] orig;
            private readonly double[] mapped;
            public AxisMap(double[] orig, double[] mapped)
            {
                this.orig = orig;
                this.mapped = mapped;
            }
            public double ToNorm(double x) { return Interpolate(orig, mapped, x); }
            public double ToOrig(double y) { return Interpolate(mapped, orig, y); }
            /// <summary>
            /// The local d(normalized)/d(original) of the mapping at <paramref name="x"/>, i.e. the
            /// average 3d scale this axis was normalized with in the interval containing x. Compared
            /// against the true local scale it tells how faithful the normalization is here.
            /// </summary>
            public double Slope(double x)
            {
                int n = orig.Length;
                int i;
                if (x <= orig[0]) i = 0;
                else if (x >= orig[n - 1]) i = n - 2;
                else
                {
                    i = Array.BinarySearch(orig, x);
                    if (i < 0) i = ~i - 1;
                    if (i > n - 2) i = n - 2;
                }
                double f = orig[i + 1] - orig[i];
                if (f == 0.0) return 0.0;
                return (mapped[i + 1] - mapped[i]) / f;
            }
            private static double Interpolate(double[] from, double[] to, double x)
            {
                int n = from.Length;
                int i;
                if (x <= from[0]) i = 0;
                else if (x >= from[n - 1]) i = n - 2;
                else
                {
                    i = Array.BinarySearch(from, x);
                    if (i < 0) i = ~i - 1;
                    if (i > n - 2) i = n - 2;
                }
                double f = (from[i + 1] - from[i]);
                if (f == 0.0) return to[i];
                return to[i] + (x - from[i]) * (to[i + 1] - to[i]) / f;
            }
        }

        private readonly ISurface surface;
        private readonly double maxDeflection;
        private readonly double maxBendingRad;
        private readonly GeoPoint2D[][] loops;

        /// <summary>
        /// When true, interior edges where the normals of the two adjacent 3d triangles differ
        /// by more than maxBending are refined. Off by default: the legacy triangulation
        /// effectively never used its maxBending parameter, and enabling it can dominate the
        /// deflection criterion on strongly curved surfaces.
        /// </summary>
        public bool ApplyBendingCriterion = false;
        /// <summary>
        /// Minimal angle (radians) a 3d triangle should have; skinnier triangles are refined
        /// by circumcenter insertion where possible without splitting boundary segments.
        /// </summary>
        public double MinAngle3D = 20.0 * Math.PI / 180.0;
        /// <summary>
        /// How far the true local 3d scale may deviate from the average scale the normalized uv
        /// space was built with before shape refinement is suspended there, see
        /// <see cref="NormalizationMismatch"/>. Only the shape criterion is affected; accuracy is
        /// governed by the deflection criteria, which do not depend on the normalization.
        /// </summary>
        public double MaxNormalizationMismatch = 4.0;

        /// <summary>true when the input polygons intersect themselves or each other (invalid input)</summary>
        public bool innerIntersection;
        /// <summary>true when the inside/outside classification found contradictions (invalid input)</summary>
        public bool classificationConflict;
        // Steiner point counters for diagnostics
        public int SteinerByEdgeDeflection, SteinerByFaceDeflection, SteinerByBending, SteinerByQuality, FlipCount;

        // vertex data, all lists parallel. Layout: [0..inputVertexCount) the input outline/hole
        // points in input order (the output relies on this: Face derives edge indices from the
        // input polyline lengths), then the 3 super triangle vertices, then Steiner points.
        private readonly List<GeoPoint2D> uv = new List<GeoPoint2D>();      // original uv
        private readonly List<GeoPoint2D> nuv = new List<GeoPoint2D>();     // normalized uv
        private readonly List<GeoPoint> pnt = new List<GeoPoint>();         // 3d points
        private readonly List<bool> singular = new List<bool>();            // vertex lies on a singular parameter line
        private readonly List<byte> singularKind = new List<byte>();        // 0 none, 1 on a u-singularity (u = value), 2 on a v-singularity (v = value)
        private readonly List<double> singularValue = new List<double>();   // the singular parameter value (only valid when singularKind != 0)
        private readonly List<int> aliasOf = new List<int>();               // duplicate input points map to their first occurrence
        private readonly List<int> vertexTri = new List<int>();             // some triangle containing this vertex (hint, may be stale)
        // the singular parameter lines of the surface, determined once in ComputeVertexData, so that
        // Steiner points landing on one of them can be classified the same way input points are
        private double[] uSingularities, vSingularities;
        private double uSingEps, vSingEps;
        // the one 3d point every parameter of a singular line maps to, keyed as in ComputeVertexData
        private readonly Dictionary<double, GeoPoint> singularPoint = new Dictionary<double, GeoPoint>();
        private int inputVertexCount;
        private int superBase = -1;

        private readonly List<Tri> tris = new List<Tri>();
        private int lastTri = -1; // walk start hint for point location

        private AxisMap uMap, vMap;
        private BoundingRect extent;
        private double eps3;      // 3d tolerance for degeneracy tests (relative to model size)
        private double diagN;     // diagonal of the normalized uv extent

        private readonly Dictionary<long, EdgeDefl> edgeDeflCache = new Dictionary<long, EdgeDefl>();
        private readonly Random rnd = new Random(0x5eed);

        private const int KindNone = 0, KindEdge = 1, KindFace = 2, KindBend = 3, KindQuality = 4;

        /// <summary>
        /// Prepares the triangulation of a face given by <paramref name="surface"/> and its uv
        /// outline. All work happens in <see cref="GetSimpleTriangles"/>.
        /// </summary>
        /// <param name="points">first array: counterclockwise outline, further arrays: clockwise holes;
        /// the points are expected to already satisfy the required accuracy along the outline and
        /// are never moved, removed or subdivided</param>
        /// <param name="surface">the surface</param>
        /// <param name="maxDeflection">maximum allowed distance of interior triangle edges from the surface</param>
        /// <param name="maxBending">maximum angle between adjacent 3d triangles (only used when
        /// <see cref="ApplyBendingCriterion"/> is set)</param>
        public CDTriangulation(GeoPoint2D[][] points, ISurface surface, double maxDeflection, Angle maxBending)
        {
            this.loops = points ?? new GeoPoint2D[0][];
            this.surface = surface;
            this.maxDeflection = maxDeflection;
            this.maxBendingRad = maxBending.Radian;
        }

        /// <summary>
        /// Computes the triangulation. The first vertices of <paramref name="p2d"/>/<paramref name="p3d"/>
        /// are exactly the input points in input order, Steiner points follow. Triangles are
        /// counterclockwise in uv.
        /// </summary>
        /// <param name="splitInaccurateEdges">when false, no interior Steiner points are created
        /// (pure constrained Delaunay triangulation of the outline)</param>
        public void GetSimpleTriangles(out GeoPoint2D[] p2d, out GeoPoint[] p3d, out int[] triangles, bool splitInaccurateEdges)
        {
            p2d = new GeoPoint2D[0];
            p3d = new GeoPoint[0];
            triangles = new int[0];

            CollectInputVertices();
            if (inputVertexCount < 3) return;
            extent = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < inputVertexCount; ++i) extent.MinMax(uv[i]);
            if (extent.Width <= 0.0 || extent.Height <= 0.0) return;

            BuildNormalization();
            ComputeVertexData();
            CreateSuperTriangle();
            InsertInputPoints();
            InsertConstraints();
            ClassifyAndRemoveOutside();
            if (lastTri < 0) return; // nothing inside
            if (splitInaccurateEdges) Refine();
            ImproveByFlips(splitInaccurateEdges);
            BuildOutput(out p2d, out p3d, out triangles);
        }

        #region setup
        private void CollectInputVertices()
        {
            for (int i = 0; i < loops.Length; ++i)
            {
                if (loops[i] == null) continue;
                for (int j = 0; j < loops[i].Length; ++j) uv.Add(loops[i][j]);
            }
            inputVertexCount = uv.Count;
        }

        private void BuildNormalization()
        {
            double[] intu, intv;
            try
            {
                surface.GetSafeParameterSteps(extent.Left, extent.Right, extent.Bottom, extent.Top, out intu, out intv);
            }
            catch (Exception)
            {
                intu = null;
                intv = null;
            }
            double[] us = RefineStations(intu, extent.Left, extent.Right);
            double[] vs = RefineStations(intv, extent.Bottom, extent.Top);
            uMap = BuildAxisMap(us, vs, true);
            vMap = BuildAxisMap(vs, us, false);
        }

        // station values covering [lo, hi]: the surface's safe steps (knots, quadrants, ...)
        // plus uniform subdivision so that no interval is longer than 1/8 of the range
        private static double[] RefineStations(double[] given, double lo, double hi)
        {
            List<double> st = new List<double>();
            st.Add(lo);
            st.Add(hi);
            if (given != null)
            {
                for (int i = 0; i < given.Length; ++i)
                {
                    if (given[i] > lo && given[i] < hi) st.Add(given[i]);
                }
            }
            st.Sort();
            double weps = (hi - lo) * 1e-9;
            for (int i = st.Count - 1; i > 0; --i)
            {
                if (st[i] - st[i - 1] < weps) st.RemoveAt(i);
            }
            double maxLen = (hi - lo) / 8.0;
            List<double> res = new List<double>();
            for (int i = 0; i < st.Count - 1; ++i)
            {
                res.Add(st[i]);
                if (res.Count < 64)
                {
                    int parts = (int)Math.Ceiling((st[i + 1] - st[i]) / maxLen - 1e-9);
                    for (int k = 1; k < parts; ++k) res.Add(st[i] + (st[i + 1] - st[i]) * k / parts);
                }
            }
            res.Add(st[st.Count - 1]);
            return res.ToArray();
        }

        // per-interval scale = average |dS/du| (resp. |dS/dv|) sampled across the other axis;
        // cumulated this yields an approximate arc length parametrization of the axis
        private AxisMap BuildAxisMap(double[] stations, double[] crossStations, bool isU)
        {
            List<double> cross = new List<double>();
            int nCross = crossStations.Length - 1;
            int step = Math.Max(1, nCross / 8);
            for (int i = 0; i < nCross; i += step) cross.Add(0.5 * (crossStations[i] + crossStations[i + 1]));
            double[] scale = new double[stations.Length - 1];
            double maxScale = 0.0;
            for (int i = 0; i < scale.Length; ++i)
            {
                double mid = 0.5 * (stations[i] + stations[i + 1]);
                double s = 0.0;
                for (int j = 0; j < cross.Count; ++j)
                {
                    try
                    {
                        GeoVector d = isU ? surface.UDirection(new GeoPoint2D(mid, cross[j]))
                                          : surface.VDirection(new GeoPoint2D(cross[j], mid));
                        s += d.Length;
                    }
                    catch (Exception) { }
                }
                scale[i] = s / cross.Count;
                if (scale[i] > maxScale) maxScale = scale[i];
            }
            double[] mapped = new double[stations.Length];
            if (maxScale <= 0.0)
            {   // completely degenerate sampling: fall back to the identity
                for (int i = 0; i < stations.Length; ++i) mapped[i] = stations[i] - stations[0];
            }
            else
            {
                // near a singularity the true scale goes to 0; a floor keeps the mapping
                // strictly monotone (and invertible)
                double minScale = maxScale * 1e-3;
                mapped[0] = 0.0;
                for (int i = 0; i < scale.Length; ++i)
                {
                    mapped[i + 1] = mapped[i] + (stations[i + 1] - stations[i]) * Math.Max(scale[i], minScale);
                }
            }
            return new AxisMap(stations, mapped);
        }

        private GeoPoint2D ToNorm(GeoPoint2D p) { return new GeoPoint2D(uMap.ToNorm(p.x), vMap.ToNorm(p.y)); }
        private GeoPoint2D ToOrig(GeoPoint2D p) { return new GeoPoint2D(uMap.ToOrig(p.x), vMap.ToOrig(p.y)); }

        private void ComputeVertexData()
        {
            try { uSingularities = surface.GetUSingularities(); } catch (Exception) { }
            try { vSingularities = surface.GetVSingularities(); } catch (Exception) { }
            uSingEps = extent.Width * 1e-7;
            vSingEps = extent.Height * 1e-7;

            for (int i = 0; i < inputVertexCount; ++i)
            {
                GeoPoint2D p = uv[i];
                nuv.Add(ToNorm(p));
                byte sKind = SingularityAt(p, out double sValue);
                pnt.Add(SurfacePoint(p, sKind, sValue));
                singular.Add(sKind != 0);
                singularKind.Add(sKind);
                singularValue.Add(sValue);
                aliasOf.Add(i);
                vertexTri.Add(-1);
            }
            BoundingBox bb = BoundingBox.EmptyBoundingBox;
            for (int i = 0; i < inputVertexCount; ++i) bb.MinMax(pnt[i]);
            double diag3 = bb.DiagonalLength;
            eps3 = diag3 > 0.0 ? diag3 * 1e-9 : 0.0;
        }

        /// <summary>
        /// Classifies a parameter point against the singular lines of the surface: 0 none, 1 on a
        /// u-singularity, 2 on a v-singularity, with the singular parameter value in
        /// <paramref name="value"/>. Applied to Steiner points as well as to the input points -
        /// a point that lands on a singular line is one, no matter how it was created.
        /// </summary>
        private byte SingularityAt(GeoPoint2D p, out double value)
        {
            if (uSingularities != null)
            {
                for (int k = 0; k < uSingularities.Length; ++k)
                {
                    if (Math.Abs(p.x - uSingularities[k]) < uSingEps) { value = uSingularities[k]; return 1; }
                }
            }
            if (vSingularities != null)
            {
                for (int k = 0; k < vSingularities.Length; ++k)
                {
                    if (Math.Abs(p.y - vSingularities[k]) < vSingEps) { value = vSingularities[k]; return 2; }
                }
            }
            value = 0.0;
            return 0;
        }

        /// <summary>
        /// The 3d point of a parameter point. All points of one singular line must map to the
        /// identical 3d point - the degeneracy tests compare 3d points, so evaluating the surface
        /// twice at two parameters of the same pole and getting two slightly different points would
        /// defeat them. So it is evaluated once per singular line and cached.
        /// </summary>
        private GeoPoint SurfacePoint(GeoPoint2D p, byte sKind, double sValue)
        {
            if (sKind == 0) return surface.PointAt(p);
            // offset the key of a v-singularity to distinguish it from a u-singularity of equal value
            double key = sKind == 1 ? sValue : sValue + 1e100;
            if (!singularPoint.TryGetValue(key, out GeoPoint pt))
            {
                pt = surface.PointAt(p);
                singularPoint[key] = pt;
            }
            return pt;
        }
        #endregion

        #region delaunay triangulation of the input points
        private int AddSteinerVertex(GeoPoint2D orig, GeoPoint2D norm)
        {
            uv.Add(orig);
            nuv.Add(norm);
            pnt.Add(GeoPoint.Origin); // filled after successful insertion
            // A Steiner point can land on a singular line just like an input point can, and everything
            // that keys off `singular` - the deflection measured along the iso-parameter path, the
            // suspended shape criterion, the removal of 3d-degenerate triangles - is wrong for it
            // otherwise. Classifying it here is what makes those consistent for the whole mesh.
            byte sKind = SingularityAt(orig, out double sValue);
            singular.Add(sKind != 0);
            singularKind.Add(sKind);
            singularValue.Add(sValue);
            aliasOf.Add(uv.Count - 1);
            vertexTri.Add(-1);
            return uv.Count - 1;
        }

        private int NewTri(int a, int b, int c)
        {
            Tri t = new Tri();
            t.v[0] = a; t.v[1] = b; t.v[2] = c;
            tris.Add(t);
            return tris.Count - 1;
        }

        private void CreateSuperTriangle()
        {
            BoundingRect next = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < inputVertexCount; ++i) next.MinMax(nuv[i]);
            double d = Math.Max(next.Width, next.Height);
            if (d <= 0.0) d = 1.0;
            diagN = Math.Sqrt(next.Width * next.Width + next.Height * next.Height);
            if (diagN <= 0.0) diagN = d;
            double cx = 0.5 * (next.Left + next.Right);
            double cy = 0.5 * (next.Bottom + next.Top);
            double m = 30.0 * d;
            superBase = uv.Count;
            GeoPoint2D[] sup = new GeoPoint2D[]
            {
                new GeoPoint2D(cx - m, cy - m),
                new GeoPoint2D(cx + m, cy - m),
                new GeoPoint2D(cx, cy + m)
            };
            for (int i = 0; i < 3; ++i)
            {
                uv.Add(ToOrig(sup[i])); // only for debugging, never evaluated on the surface
                nuv.Add(sup[i]);
                pnt.Add(GeoPoint.Origin);
                singular.Add(false);
                singularKind.Add(0);
                singularValue.Add(0.0);
                aliasOf.Add(uv.Count - 1);
                vertexTri.Add(0);
            }
            lastTri = NewTri(superBase, superBase + 1, superBase + 2);
        }

        private void InsertInputPoints()
        {
            // randomized insertion order for the expected O(n log n) behavior of incremental
            // Delaunay insertion; vertex indices (and thereby the output order) stay untouched
            int[] order = new int[inputVertexCount];
            for (int i = 0; i < order.Length; ++i) order[i] = i;
            for (int i = order.Length - 1; i > 0; --i)
            {
                int j = rnd.Next(i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
            List<int> newTris = new List<int>();
            for (int i = 0; i < order.Length; ++i)
            {
                int vi = order[i];
                int coincident;
                if (!TryInsertVertex(vi, lastTri, false, newTris, out coincident))
                {
                    if (coincident >= 0) aliasOf[vi] = aliasOf[coincident]; // duplicate input point
                    // otherwise the point could not be inserted (degenerate situation); it
                    // stays as an unused vertex
                }
            }
        }

        // Locates the triangle whose closure contains p. Returns -1 when the walk is blocked
        // by a constrained edge or leaves the mesh (only possible during refinement).
        // zeroMask: bit i set when p lies exactly on edge i of the returned triangle.
        private int Locate(GeoPoint2D p, int start, out int zeroMask)
        {
            zeroMask = 0;
            int t = start;
            if (t < 0 || t >= tris.Count || !tris[t].alive) t = AnyAliveTriangle();
            if (t < 0) return -1;
            int maxSteps = 4 * tris.Count + 100;
            for (int steps = 0; steps < maxSteps; ++steps)
            {
                Tri tr = tris[t];
                int neg1 = -1, neg2 = -1;
                zeroMask = 0;
                for (int i = 0; i < 3; ++i)
                {
                    double o = CDTPredicates.Orient2D(nuv[tr.v[i]], nuv[tr.v[Next(i)]], p);
                    if (o < 0)
                    {
                        if (neg1 < 0) neg1 = i; else neg2 = i;
                    }
                    else if (o == 0) zeroMask |= (1 << i);
                }
                if (neg1 < 0) return t;
                int cross = neg1;
                if (neg2 >= 0 && (rnd.Next() & 1) == 1) cross = neg2; // random tie break against cycling
                int nb = tr.n[cross];
                bool blocked = nb < 0 || (tr.constrained & (1 << cross)) != 0;
                if (blocked && neg2 >= 0)
                {
                    int other = (cross == neg1) ? neg2 : neg1;
                    if (tr.n[other] >= 0 && (tr.constrained & (1 << other)) == 0)
                    {
                        t = tr.n[other];
                        continue;
                    }
                }
                if (blocked) return -1;
                t = nb;
            }
            return LocateLinear(p, out zeroMask);
        }

        private int LocateLinear(GeoPoint2D p, out int zeroMask)
        {
            zeroMask = 0;
            for (int t = 0; t < tris.Count; ++t)
            {
                if (!tris[t].alive) continue;
                Tri tr = tris[t];
                int zm = 0;
                bool inside = true;
                for (int i = 0; i < 3 && inside; ++i)
                {
                    double o = CDTPredicates.Orient2D(nuv[tr.v[i]], nuv[tr.v[Next(i)]], p);
                    if (o < 0) inside = false;
                    else if (o == 0) zm |= (1 << i);
                }
                if (inside)
                {
                    zeroMask = zm;
                    return t;
                }
            }
            return -1;
        }

        private int AnyAliveTriangle()
        {
            for (int t = tris.Count - 1; t >= 0; --t)
            {
                if (tris[t].alive) return t;
            }
            return -1;
        }

        private static int EdgeSlot(Tri t, int a, int b)
        {
            for (int i = 0; i < 3; ++i)
            {
                if (t.v[i] == a && t.v[Next(i)] == b) return i;
            }
            return -1;
        }

        // Bowyer-Watson insertion of vertex vi. Returns false without modifying the mesh when
        // the point coincides with an existing vertex (coincidentWith set), lies on a
        // constrained edge, or - with abortOnConstraintEncroach - when the insertion would
        // require growing the cavity across a constrained edge (Chew's rule: since boundary
        // segments must never be split, such an insertion is abandoned).
        private bool TryInsertVertex(int vi, int start, bool abortOnConstraintEncroach, List<int> newTris, out int coincidentWith)
        {
            coincidentWith = -1;
            newTris.Clear();
            GeoPoint2D p = nuv[vi];
            int zeroMask;
            int loc = Locate(p, start, out zeroMask);
            if (loc < 0) return false;
            Tri lt = tris[loc];
            if ((zeroMask & (zeroMask - 1)) != 0)
            {   // two edges report "exactly on": p coincides with the vertex shared by them
                if ((zeroMask & 3) == 3) coincidentWith = lt.v[1];
                else if ((zeroMask & 6) == 6) coincidentWith = lt.v[2];
                else coincidentWith = lt.v[0];
                return false;
            }
            int extraSeed = -1;
            if (zeroMask != 0)
            {
                int ze = 0;
                while ((zeroMask & (1 << ze)) == 0) ++ze;
                if ((lt.constrained & (1 << ze)) != 0) return false; // exactly on a constrained edge: never split
                extraSeed = lt.n[ze];
                if (extraSeed < 0) return false; // on an edge with nothing beyond: would create a degenerate triangle
            }
            List<int> cavity = new List<int>();
            List<CavityEdge> boundary = new List<CavityEdge>();
            if (!CollectCavity(p, loc, extraSeed, abortOnConstraintEncroach, cavity, boundary)) return false;
            BuildStar(vi, cavity, boundary, newTris);
            lastTri = newTris[0];
            return true;
        }

        private bool CollectCavity(GeoPoint2D p, int seed, int extraSeed, bool abortOnConstraintEncroach,
            List<int> cavity, List<CavityEdge> boundary)
        {
            cavity.Clear();
            HashSet<int> inCav = new HashSet<int>();
            Stack<int> stack = new Stack<int>();
            inCav.Add(seed); cavity.Add(seed); stack.Push(seed);
            if (extraSeed >= 0 && tris[extraSeed].alive && inCav.Add(extraSeed))
            {
                cavity.Add(extraSeed);
                stack.Push(extraSeed);
            }
            bool encroached = false;
            while (stack.Count > 0)
            {
                Tri tr = tris[stack.Pop()];
                for (int i = 0; i < 3; ++i)
                {
                    int nb = tr.n[i];
                    if (nb >= 0 && inCav.Contains(nb)) continue;
                    bool constrainedEdge = (tr.constrained & (1 << i)) != 0;
                    if (!constrainedEdge && nb >= 0)
                    {
                        Tri nbr = tris[nb];
                        if (CDTPredicates.InCircle(nuv[nbr.v[0]], nuv[nbr.v[1]], nuv[nbr.v[2]], p) > 0)
                        {
                            inCav.Add(nb); cavity.Add(nb); stack.Push(nb);
                        }
                    }
                    else if (constrainedEdge && !encroached)
                    {
                        // p inside the diametral circle of a constrained edge means the Delaunay
                        // cavity is clipped by the constraint (the point "encroaches" the segment)
                        GeoPoint2D ea = nuv[tr.v[i]], eb = nuv[tr.v[Next(i)]];
                        if ((ea.x - p.x) * (eb.x - p.x) + (ea.y - p.y) * (eb.y - p.y) < 0) encroached = true;
                    }
                }
            }
            if (abortOnConstraintEncroach && encroached) return false;

            // the cavity must be star shaped as seen from p, otherwise the star construction
            // would create inverted triangles; grow it where the conservative incircle filter
            // left a non-star-shaped pocket
            for (int repair = 0; repair < 1000; ++repair)
            {
                boundary.Clear();
                bool grown = false;
                for (int ci = 0; ci < cavity.Count && !grown; ++ci)
                {
                    Tri tr = tris[cavity[ci]];
                    for (int i = 0; i < 3; ++i)
                    {
                        int nb = tr.n[i];
                        if (nb >= 0 && inCav.Contains(nb)) continue;
                        CavityEdge ce;
                        ce.a = tr.v[i];
                        ce.b = tr.v[Next(i)];
                        ce.outer = nb;
                        ce.constrained = (tr.constrained & (1 << i)) != 0;
                        boundary.Add(ce);
                        if (CDTPredicates.Orient2D(nuv[ce.a], nuv[ce.b], p) <= 0)
                        {
                            if (nb < 0 || ce.constrained) return false; // cannot repair without crossing the boundary
                            inCav.Add(nb); cavity.Add(nb);
                            grown = true;
                            break;
                        }
                    }
                }
                if (!grown) return true;
            }
            return false;
        }

        private void BuildStar(int vi, List<int> cavity, List<CavityEdge> boundary, List<int> newTris)
        {
            for (int i = 0; i < cavity.Count; ++i) tris[cavity[i]].alive = false;
            Dictionary<int, int> byFirst = new Dictionary<int, int>(boundary.Count);
            for (int i = 0; i < boundary.Count; ++i)
            {
                CavityEdge ce = boundary[i];
                int nt = NewTri(ce.a, ce.b, vi); // ccw: p lies strictly left of every boundary edge
                Tri ntr = tris[nt];
                ntr.n[0] = ce.outer;
                if (ce.constrained) ntr.constrained |= 1;
                if (ce.outer >= 0)
                {
                    int os = EdgeSlot(tris[ce.outer], ce.b, ce.a);
                    if (os >= 0) tris[ce.outer].n[os] = nt;
                }
                byFirst[ce.a] = nt;
                newTris.Add(nt);
                vertexTri[ce.a] = nt;
            }
            // link the star triangles among each other: triangle (a,b,vi) shares edge (b,vi)
            // with the triangle starting at b
            for (int i = 0; i < boundary.Count; ++i)
            {
                CavityEdge ce = boundary[i];
                int x = byFirst[ce.a];
                int y;
                if (byFirst.TryGetValue(ce.b, out y))
                {
                    tris[x].n[1] = y;
                    tris[y].n[2] = x;
                }
            }
            vertexTri[vi] = newTris[0];
        }
        #endregion

        #region constraint insertion
        private void InsertConstraints()
        {
            int offset = 0;
            for (int i = 0; i < loops.Length; ++i)
            {
                if (loops[i] == null) continue;
                int len = loops[i].Length;
                if (len >= 3)
                {
                    for (int j = 0; j < len; ++j)
                    {
                        int a = offset + j;
                        int b = offset + (j + 1) % len;
                        InsertConstraint(aliasOf[a], aliasOf[b]);
                    }
                }
                offset += len;
            }
        }

        // collects all alive triangles containing vertex a (local flood fill, robust against
        // interrupted fans)
        private List<int> FanOf(int a)
        {
            List<int> res = new List<int>();
            int start = vertexTri[a];
            if (start < 0 || start >= tris.Count || !tris[start].alive || IndexOfVertex(tris[start], a) < 0)
            {
                start = -1;
                for (int t = tris.Count - 1; t >= 0 && start < 0; --t)
                {
                    if (tris[t].alive && IndexOfVertex(tris[t], a) >= 0) start = t;
                }
                if (start < 0) return res;
                vertexTri[a] = start;
            }
            HashSet<int> visited = new HashSet<int>();
            Stack<int> stack = new Stack<int>();
            visited.Add(start); stack.Push(start);
            while (stack.Count > 0)
            {
                int t = stack.Pop();
                res.Add(t);
                Tri tr = tris[t];
                int ia = IndexOfVertex(tr, a);
                // the two edges incident to a are edge ia (a -> next) and edge Prev(ia) (prev -> a)
                int[] slots = new int[] { ia, Prev(ia) };
                for (int k = 0; k < 2; ++k)
                {
                    int nb = tr.n[slots[k]];
                    if (nb >= 0 && tris[nb].alive && IndexOfVertex(tris[nb], a) >= 0 && visited.Add(nb)) stack.Push(nb);
                }
            }
            return res;
        }

        private static int IndexOfVertex(Tri t, int v)
        {
            for (int i = 0; i < 3; ++i)
            {
                if (t.v[i] == v) return i;
            }
            return -1;
        }

        private bool MarkExistingEdge(int a, int b)
        {
            List<int> fan = FanOf(a);
            for (int i = 0; i < fan.Count; ++i)
            {
                Tri tr = tris[fan[i]];
                int s = EdgeSlot(tr, a, b);
                if (s < 0)
                {
                    s = EdgeSlot(tr, b, a);
                    if (s < 0) continue;
                }
                tr.constrained |= (byte)(1 << s);
                int nb = tr.n[s];
                if (nb >= 0)
                {
                    int os = EdgeSlot(tris[nb], tr.v[Next(s)], tr.v[s]);
                    if (os >= 0) tris[nb].constrained |= (byte)(1 << os);
                }
                return true;
            }
            return false;
        }

        private void InsertConstraint(int a, int b)
        {
            for (int guard = 0; guard < uv.Count; ++guard) // the loop advances a along collinear vertices
            {
                if (a == b) return;
                if (MarkExistingEdge(a, b)) return;

                // find the fan triangle of a whose interior the segment a->b enters
                GeoPoint2D pa = nuv[a], pb = nuv[b];
                List<int> fan = FanOf(a);
                int startTri = -1, collinearWith = -1;
                for (int f = 0; f < fan.Count && startTri < 0 && collinearWith < 0; ++f)
                {
                    Tri tr = tris[fan[f]];
                    int ia = IndexOfVertex(tr, a);
                    int x = tr.v[Next(ia)], y = tr.v[Prev(ia)];
                    double ox = CDTPredicates.Orient2D(pa, nuv[x], pb); // b relative to a->x
                    double oy = CDTPredicates.Orient2D(pa, pb, nuv[y]); // y relative to a->b
                    if (ox == 0 && (nuv[x].x - pa.x) * (pb.x - pa.x) + (nuv[x].y - pa.y) * (pb.y - pa.y) > 0)
                    {
                        collinearWith = x; // an existing vertex lies exactly on the segment
                    }
                    else if (oy == 0 && (nuv[y].x - pa.x) * (pb.x - pa.x) + (nuv[y].y - pa.y) * (pb.y - pa.y) > 0)
                    {
                        collinearWith = y;
                    }
                    else if (ox > 0 && oy > 0) startTri = fan[f];
                }
                if (collinearWith >= 0)
                {   // the constraint passes exactly through another vertex: enforce it piecewise
                    // (geometrically identical, the boundary polygon is not changed)
                    InsertConstraint(a, collinearWith);
                    a = collinearWith;
                    continue;
                }
                if (startTri < 0)
                {
                    innerIntersection = true; // degenerate input (e.g. zero area sector at a)
                    return;
                }
                int onSegment;
                if (!MarchAndRetriangulate(a, b, startTri, out onSegment)) return;
                if (onSegment < 0) return; // completed
                a = onSegment; // continue behind the collinear vertex that interrupted the march
            }
        }

        // Removes all triangles crossed by the segment a->b (starting in startTri) and
        // retriangulates the two resulting pseudo polygons so that (a,b) becomes an edge,
        // which is then marked as constrained. When a vertex is encountered exactly on the
        // segment, the march stops there: the constraint (a,onSegment) is completed and the
        // caller continues with (onSegment, b).
        private bool MarchAndRetriangulate(int a, int b, int startTri, out int onSegment)
        {
            onSegment = -1;
            GeoPoint2D pa = nuv[a], pb = nuv[b];
            List<int> crossed = new List<int>();
            List<int> left = new List<int>();
            List<int> right = new List<int>();
            HashSet<int> crossedSet = new HashSet<int>();

            Tri st = tris[startTri];
            int ia = IndexOfVertex(st, a);
            int xv = st.v[Next(ia)], yv = st.v[Prev(ia)]; // x right of a->b, y left of a->b
            crossed.Add(startTri); crossedSet.Add(startTri);
            right.Add(xv); left.Add(yv);
            int curTri = startTri;
            int curSlot = Next(ia); // edge (x,y), directed right->left
            int target = b;
            for (int guard = 0; guard < tris.Count + 10; ++guard)
            {
                Tri ct = tris[curTri];
                if ((ct.constrained & (1 << curSlot)) != 0)
                {   // the segment crosses another constrained edge: invalid input
                    innerIntersection = true;
                    return false;
                }
                int t2 = ct.n[curSlot];
                if (t2 < 0)
                {
                    innerIntersection = true;
                    return false;
                }
                int rv = ct.v[curSlot], lv = ct.v[Next(curSlot)]; // current crossing edge right/left vertex
                int slot2 = EdgeSlot(tris[t2], lv, rv);
                if (slot2 < 0)
                {
                    innerIntersection = true; // inconsistent neighborhood
                    return false;
                }
                Tri tr2 = tris[t2];
                int z = tr2.v[Prev(slot2)];
                crossed.Add(t2); crossedSet.Add(t2);
                if (aliasOf[z] == target || z == target)
                {
                    break; // reached b
                }
                double oz = CDTPredicates.Orient2D(pa, pb, nuv[z]);
                if (oz == 0)
                {   // a vertex exactly on the segment: complete the piece a..z here
                    onSegment = z;
                    target = z;
                    break;
                }
                if (oz > 0)
                {
                    left.Add(z);
                    curSlot = Next(slot2); // edge (rv, z)
                }
                else
                {
                    right.Add(z);
                    curSlot = Prev(slot2); // edge (z, lv)
                }
                curTri = t2;
            }

            // record the border of the crossed region (with outer neighbors and constraint
            // flags) before deleting anything
            List<CavityEdge> border = new List<CavityEdge>();
            for (int i = 0; i < crossed.Count; ++i)
            {
                Tri tr = tris[crossed[i]];
                for (int e = 0; e < 3; ++e)
                {
                    int nb = tr.n[e];
                    if (nb >= 0 && crossedSet.Contains(nb)) continue;
                    CavityEdge ce;
                    ce.a = tr.v[e]; ce.b = tr.v[Next(e)];
                    ce.outer = nb;
                    ce.constrained = (tr.constrained & (1 << e)) != 0;
                    border.Add(ce);
                }
            }
            for (int i = 0; i < crossed.Count; ++i) tris[crossed[i]].alive = false;

            List<int> newTris = new List<int>();
            TriangulatePseudoPolygon(a, target, left, newTris);
            right.Reverse();
            TriangulatePseudoPolygon(target, a, right, newTris);

            // link everything via directed edges
            Dictionary<long, int> dir = new Dictionary<long, int>(); // directed edge -> new triangle
            for (int i = 0; i < newTris.Count; ++i)
            {
                Tri tr = tris[newTris[i]];
                for (int e = 0; e < 3; ++e)
                {
                    dir[DirectedKey(tr.v[e], tr.v[Next(e)])] = newTris[i];
                }
            }
            for (int i = 0; i < newTris.Count; ++i)
            {
                Tri tr = tris[newTris[i]];
                for (int e = 0; e < 3; ++e)
                {
                    int mate;
                    if (dir.TryGetValue(DirectedKey(tr.v[Next(e)], tr.v[e]), out mate)) tr.n[e] = mate;
                }
                for (int e = 0; e < 3; ++e) vertexTri[tr.v[e]] = newTris[i];
            }
            for (int i = 0; i < border.Count; ++i)
            {
                CavityEdge ce = border[i];
                int nt;
                if (dir.TryGetValue(DirectedKey(ce.a, ce.b), out nt))
                {
                    Tri tr = tris[nt];
                    int s = EdgeSlot(tr, ce.a, ce.b);
                    tr.n[s] = ce.outer;
                    if (ce.constrained) tr.constrained |= (byte)(1 << s);
                    if (ce.outer >= 0)
                    {
                        int os = EdgeSlot(tris[ce.outer], ce.b, ce.a);
                        if (os >= 0) tris[ce.outer].n[os] = nt;
                    }
                }
                else innerIntersection = true; // retriangulation did not reproduce the border
            }
            // mark the enforced segment
            int tAB, tBA;
            if (dir.TryGetValue(DirectedKey(a, target), out tAB))
            {
                Tri tr = tris[tAB];
                tr.constrained |= (byte)(1 << EdgeSlot(tr, a, target));
            }
            if (dir.TryGetValue(DirectedKey(target, a), out tBA))
            {
                Tri tr = tris[tBA];
                tr.constrained |= (byte)(1 << EdgeSlot(tr, target, a));
            }
            lastTri = newTris.Count > 0 ? newTris[0] : AnyAliveTriangle();
            return true;
        }

        private static long DirectedKey(int a, int b) { return ((long)(uint)a << 32) | (uint)b; }

        // Anglada's pseudo polygon retriangulation: chain contains the vertices on the left of
        // the directed base edge u->w, ordered from u to w. Chooses the apex c whose
        // circumcircle (u,w,c) contains no other chain vertex and recurses.
        private void TriangulatePseudoPolygon(int u, int w, List<int> chain, List<int> newTris)
        {
            if (chain.Count == 0) return;
            int ci = -1;
            for (int i = 0; i < chain.Count; ++i)
            {
                if (CDTPredicates.Orient2D(nuv[u], nuv[w], nuv[chain[i]]) > 0)
                {
                    if (ci < 0) ci = i;
                    else if (CDTPredicates.InCircle(nuv[u], nuv[w], nuv[chain[ci]], nuv[chain[i]]) > 0) ci = i;
                }
            }
            if (ci < 0) ci = 0; // degenerate (collinear) pocket: take any vertex
            int c = chain[ci];
            newTris.Add(NewTri(u, w, c));
            if (ci > 0) TriangulatePseudoPolygon(u, c, chain.GetRange(0, ci), newTris);
            if (ci < chain.Count - 1) TriangulatePseudoPolygon(c, w, chain.GetRange(ci + 1, chain.Count - ci - 1), newTris);
        }
        #endregion

        #region inside/outside classification
        private bool ContainsSuperVertex(Tri t)
        {
            for (int i = 0; i < 3; ++i)
            {
                if (t.v[i] >= superBase && t.v[i] < superBase + 3) return true;
            }
            return false;
        }

        private void ClassifyAndRemoveOutside()
        {
            int n = tris.Count;
            sbyte[] state = new sbyte[n]; // 0 unknown, 1 outside, 2 inside
            Queue<int> queue = new Queue<int>();
            int seed = -1;
            for (int i = 0; i < n && seed < 0; ++i)
            {
                if (tris[i].alive && ContainsSuperVertex(tris[i])) seed = i;
            }
            if (seed < 0) return;
            state[seed] = 1;
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                int t = queue.Dequeue();
                Tri tr = tris[t];
                for (int i = 0; i < 3; ++i)
                {
                    int nb = tr.n[i];
                    if (nb < 0 || !tris[nb].alive) continue;
                    // crossing a constrained (outline/hole) edge toggles inside/outside
                    sbyte ns = ((tr.constrained & (1 << i)) != 0) ? (state[t] == 1 ? (sbyte)2 : (sbyte)1) : state[t];
                    if (state[nb] == 0)
                    {
                        state[nb] = ns;
                        queue.Enqueue(nb);
                    }
                    else if (state[nb] != ns) classificationConflict = true;
                }
            }
            for (int i = 0; i < n; ++i)
            {
                if (!tris[i].alive) continue;
                if (state[i] != 2 || ContainsSuperVertex(tris[i])) tris[i].alive = false;
            }
            lastTri = -1;
            for (int i = 0; i < n; ++i)
            {
                if (!tris[i].alive) continue;
                Tri tr = tris[i];
                for (int e = 0; e < 3; ++e)
                {
                    if (tr.n[e] >= 0 && !tris[tr.n[e]].alive) tr.n[e] = -1;
                }
                lastTri = i;
            }
        }
        #endregion

        #region refinement
        private EdgeDefl GetEdgeDeflection(int a, int b)
        {
            long key = a < b ? DirectedKey(a, b) : DirectedKey(b, a);
            EdgeDefl ed;
            if (!edgeDeflCache.TryGetValue(key, out ed))
            {
                // When one endpoint lies on a singular line, all points of that line map to the
                // same 3d point, so the uv path to the endpoint may be chosen freely without
                // changing the 3d chord. The straight uv segment would sweep across the
                // singularity (e.g. spiral around a cone apex) and report a deviation the actual
                // mesh edge does not have; measuring along the iso-parameter path (e.g. the cone
                // ruling / sphere meridian) reflects the true chord-to-surface distance.
                GeoPoint2D pa = uv[a], pb = uv[b];
                if (singularKind[a] != 0 && singularKind[b] == 0) pa = ProjectOntoSingularity(a, pb);
                else if (singularKind[b] != 0 && singularKind[a] == 0) pb = ProjectOntoSingularity(b, pa);
                GeoPoint2D mp;
                ed.dist = surface.MaxDist(pa, pb, out mp);
                ed.mp = mp;
                edgeDeflCache[key] = ed;
            }
            return ed;
        }

        // point on the singular line of vertex s sharing the free parameter of the other endpoint
        private GeoPoint2D ProjectOntoSingularity(int s, GeoPoint2D other)
        {
            if (singularKind[s] == 1) return new GeoPoint2D(singularValue[s], other.y);
            return new GeoPoint2D(other.x, singularValue[s]);
        }

        private int SingularVertexCount(Tri t)
        {
            int c = 0;
            for (int i = 0; i < 3; ++i)
            {
                if (singular[t.v[i]]) ++c;
            }
            return c;
        }

        private static double Min3DAngle(GeoPoint p1, GeoPoint p2, GeoPoint p3)
        {
            double a = p2 | p3, b = p1 | p3, c = p1 | p2;
            if (a <= 0 || b <= 0 || c <= 0) return 0.0;
            double angA = Math.Acos(Clamp((b * b + c * c - a * a) / (2 * b * c)));
            double angB = Math.Acos(Clamp((a * a + c * c - b * b) / (2 * a * c)));
            double angC = Math.PI - angA - angB;
            return Math.Min(angA, Math.Min(angB, angC));
        }

        private static double Clamp(double x) { return Math.Max(-1.0, Math.Min(1.0, x)); }

        private static bool Circumcenter(GeoPoint2D a, GeoPoint2D b, GeoPoint2D c, out GeoPoint2D cc)
        {
            double abx = b.x - a.x, aby = b.y - a.y, acx = c.x - a.x, acy = c.y - a.y;
            double d = 2.0 * (abx * acy - aby * acx);
            double sz = abx * abx + aby * aby + acx * acx + acy * acy;
            if (Math.Abs(d) < 1e-12 * sz)
            {
                cc = a;
                return false;
            }
            double b2 = abx * abx + aby * aby;
            double c2 = acx * acx + acy * acy;
            cc = new GeoPoint2D(a.x + (acy * b2 - aby * c2) / d, a.y + (abx * c2 - acx * b2) / d);
            return true;
        }

        // evaluates the refinement criteria in priority order and proposes a Steiner point
        private int EvaluateTriangle(int t, double qualityMinLenN, double qualityMinLen3d, out GeoPoint2D normPos, out GeoPoint2D origPos)
        {
            normPos = GeoPoint2D.Origin;
            origPos = GeoPoint2D.Origin;
            Tri tr = tris[t];
            GeoPoint pa = pnt[tr.v[0]], pb = pnt[tr.v[1]], pc = pnt[tr.v[2]];

            // 1) chord deflection of interior edges: the point of maximal deviation reported
            //    by MaxDist is the optimal split position
            for (int i = 0; i < 3; ++i)
            {
                if ((tr.constrained & (1 << i)) != 0) continue; // boundary is fixed and assumed accurate
                if (tr.n[i] < 0) continue;
                int e1 = tr.v[i], e2 = tr.v[Next(i)];
                if (singular[e1] && singular[e2] && (pnt[e1] | pnt[e2]) < eps3) continue; // zero 3d chord along a singularity
                EdgeDefl ed = GetEdgeDeflection(e1, e2);
                if (ed.dist > maxDeflection)
                {
                    origPos = ed.mp;
                    normPos = ToNorm(ed.mp);
                    return KindEdge;
                }
            }

            GeoVector nrm = (pb - pa) ^ (pc - pa);
            double area2 = nrm.Length; // twice the 3d area
            double lmax = Math.Max(pa | pb, Math.Max(pb | pc, pc | pa));
            bool degenerate3d = lmax <= 0 || area2 < eps3 * lmax; // minimal height below tolerance

            // 2) deflection of the triangle interior: catches saddle-like situations where all
            //    three edges are accurate but the center is not
            if (!degenerate3d)
            {
                GeoPoint2D cN = new GeoPoint2D((nuv[tr.v[0]].x + nuv[tr.v[1]].x + nuv[tr.v[2]].x) / 3.0,
                                               (nuv[tr.v[0]].y + nuv[tr.v[1]].y + nuv[tr.v[2]].y) / 3.0);
                GeoPoint2D cO = ToOrig(cN);
                GeoPoint pcen = surface.PointAt(cO);
                double dist = Math.Abs((pcen - pa) * nrm) / area2;
                if (dist > maxDeflection)
                {
                    normPos = cN;
                    origPos = cO;
                    return KindFace;
                }
            }

            // 3) bending between adjacent 3d triangles (optional, see ApplyBendingCriterion)
            if (ApplyBendingCriterion && maxBendingRad > 0 && !degenerate3d)
            {
                for (int i = 0; i < 3; ++i)
                {
                    if ((tr.constrained & (1 << i)) != 0) continue;
                    int nb = tr.n[i];
                    if (nb < 0 || !tris[nb].alive) continue;
                    Tri nbt = tris[nb];
                    GeoVector nrm2 = (pnt[nbt.v[1]] - pnt[nbt.v[0]]) ^ (pnt[nbt.v[2]] - pnt[nbt.v[0]]);
                    double l2 = nrm2.Length;
                    if (l2 < eps3 * eps3) continue;
                    double ang = Math.Acos(Clamp((nrm * nrm2) / (area2 * l2)));
                    if (ang > maxBendingRad)
                    {
                        int e1 = tr.v[i], e2 = tr.v[Next(i)];
                        EdgeDefl ed = GetEdgeDeflection(e1, e2);
                        if (ed.dist > eps3) origPos = ed.mp;
                        else origPos = new GeoPoint2D(uv[e1], uv[e2], 0.5);
                        normPos = ToNorm(origPos);
                        return KindBend;
                    }
                }
            }

            // 4) 3d shape quality: refine skinny triangles by circumcenter insertion. Skipped at
            //    singularities (a uv-thin triangle at a pole is fine in 3d and cannot be improved
            //    anyway), where the normalization does not describe the surface, and when the
            //    triangle is already at the sizing floor
            if (!degenerate3d && SingularVertexCount(tr) == 0)
            {
                double minAng = Min3DAngle(pa, pb, pc);
                if (minAng < MinAngle3D)
                {
                    double shortestN = double.MaxValue;
                    double shortest3d = double.MaxValue;
                    // Anisotropy within the triangle: the circumcenter of a triangle whose edges are
                    // stretched very differently by the parametrization is not the point that
                    // improves it in 3d. Cheap, but it only sees this one triangle, and the spread
                    // it measures shrinks as the triangles do - so it cannot be the only guard.
                    double minRatio = double.MaxValue, maxRatio = 0.0;
                    for (int i = 0; i < 3; ++i)
                    {
                        double l = nuv[tr.v[i]] | nuv[tr.v[Next(i)]];
                        double l3 = pnt[tr.v[i]] | pnt[tr.v[Next(i)]];
                        if (l < shortestN) shortestN = l;
                        if (l3 < shortest3d) shortest3d = l3;
                        if (l > 0)
                        {
                            double r = l3 / l;
                            if (r < minRatio) minRatio = r;
                            if (r > maxRatio) maxRatio = r;
                        }
                    }
                    bool anisotropic = minRatio <= 0 || maxRatio > 4.0 * minRatio;
                    if (!anisotropic && shortestN > qualityMinLenN && shortest3d > qualityMinLen3d
                        && NormalizationMismatch(Centroid(tr)) <= MaxNormalizationMismatch)
                    {
                        GeoPoint2D cc;
                        if (Circumcenter(nuv[tr.v[0]], nuv[tr.v[1]], nuv[tr.v[2]], out cc))
                        {
                            normPos = cc;
                            origPos = ToOrig(cc);
                            return KindQuality;
                        }
                    }
                }
            }
            return KindNone;
        }

        /// <summary>The centroid of a triangle in original uv.</summary>
        private GeoPoint2D Centroid(Tri tr)
        {
            return new GeoPoint2D((uv[tr.v[0]].x + uv[tr.v[1]].x + uv[tr.v[2]].x) / 3.0,
                                  (uv[tr.v[0]].y + uv[tr.v[1]].y + uv[tr.v[2]].y) / 3.0);
        }

        /// <summary>
        /// How badly the normalized uv space misrepresents the surface at <paramref name="p"/>: the
        /// worst ratio, over both axes, between the true local 3d scale and the average scale the
        /// axis was normalized with. 1 means the normalization is locally faithful, large values
        /// mean a circumcenter computed in normalized uv says nothing about 3d.
        /// <para>
        /// This is what a per-axis arc length normalization cannot express: on a cone the
        /// parametrization is polar - u is an angle, v the radius - so |dS/du| depends on v, and
        /// <see cref="BuildAxisMap"/> can only store one average per u interval. Near the apex the
        /// true scale is far below that average, a circumcenter lands where it does not improve the
        /// 3d angle, and the triangle is refined again: the shape criterion never converges and
        /// stops only at the vertex cap. Measuring the mismatch at a POSITION rather than from the
        /// triangle's own edges is the point - the per-triangle spread shrinks with the triangle,
        /// so refinement switches that guard off just when it is needed most.
        /// </para>
        /// </summary>
        private double NormalizationMismatch(GeoPoint2D p)
        {
            double worst = 1.0;
            try
            {
                worst = Math.Max(worst, ScaleMismatch(surface.UDirection(p).Length, uMap.Slope(p.x)));
                worst = Math.Max(worst, ScaleMismatch(surface.VDirection(p).Length, vMap.Slope(p.y)));
            }
            catch (Exception)
            {   // a surface that cannot be differentiated here: treat it as unusable for shape refinement
                return double.MaxValue;
            }
            return worst;
        }

        private static double ScaleMismatch(double local, double mapped)
        {
            if (mapped <= 0.0 || local <= 0.0) return double.MaxValue;
            return local > mapped ? local / mapped : mapped / local;
        }

        private void Refine()
        {
            // Sizing floor for the quality criterion: never refine for shape reasons far below the
            // boundary sampling density (deflection refinement is not limited by this). Kept in both
            // spaces on purpose. The normalized one bounds the mesh in the space the circumcenter is
            // computed in; the 3d one is what actually bounds it near a singularity, where a fixed
            // length in normalized uv covers a 3d distance that goes to zero, so the normalized
            // floor alone permits arbitrarily many triangles in a vanishingly small piece of surface.
            List<double> boundaryLens = new List<double>();
            List<double> boundaryLens3d = new List<double>();
            for (int t = 0; t < tris.Count; ++t)
            {
                if (!tris[t].alive) continue;
                Tri tr = tris[t];
                for (int e = 0; e < 3; ++e)
                {
                    if ((tr.constrained & (1 << e)) != 0 && tr.n[e] < 0)
                    {
                        boundaryLens.Add(nuv[tr.v[e]] | nuv[tr.v[Next(e)]]);
                        boundaryLens3d.Add(pnt[tr.v[e]] | pnt[tr.v[Next(e)]]);
                    }
                }
            }
            double qualityMinLenN, qualityMinLen3d;
            if (boundaryLens.Count > 0)
            {
                boundaryLens.Sort();
                boundaryLens3d.Sort();
                qualityMinLenN = boundaryLens[boundaryLens.Count / 2] * 0.1;
                qualityMinLen3d = boundaryLens3d[boundaryLens3d.Count / 2] * 0.1;
            }
            else
            {
                qualityMinLenN = diagN * 1e-3;
                qualityMinLen3d = 0.0;
            }

            int maxVerts = Math.Max(20000, inputVertexCount * 50); // hard safety cap
            Stack<int> work = new Stack<int>();
            for (int t = 0; t < tris.Count; ++t)
            {
                if (tris[t].alive) work.Push(t);
            }
            List<int> newTris = new List<int>();
            while (work.Count > 0)
            {
                int t = work.Pop();
                if (t >= tris.Count || !tris[t].alive) continue;
                GeoPoint2D np, op;
                int kind = EvaluateTriangle(t, qualityMinLenN, qualityMinLen3d, out np, out op);
                if (kind == KindNone) continue;
                if (uv.Count >= maxVerts) break;
                int vi = AddSteinerVertex(op, np);
                int coincident;
                if (!TryInsertVertex(vi, t, kind == KindQuality, newTris, out coincident))
                {
                    // insertion rejected (encroached constraint, coincidence, sizing floor):
                    // the criterion stays unmet here - by design, since boundary segments must
                    // never be split. The vertex remains unused and is dropped from the output.
                    continue;
                }
                pnt[vi] = SurfacePoint(op, singularKind[vi], singularValue[vi]);
                switch (kind)
                {
                    case KindEdge: ++SteinerByEdgeDeflection; break;
                    case KindFace: ++SteinerByFaceDeflection; break;
                    case KindBend: ++SteinerByBending; break;
                    case KindQuality: ++SteinerByQuality; break;
                }
                for (int i = 0; i < newTris.Count; ++i)
                {
                    work.Push(newTris[i]);
                    int outer = tris[newTris[i]].n[0]; // shape unchanged, but bending state may have changed
                    if (outer >= 0 && tris[outer].alive) work.Push(outer);
                }
            }
        }
        #endregion

        #region data dependent flips
        // The refinement maintains the Delaunay property in the normalized uv space; this pass
        // exchanges diagonals where the other diagonal yields a better minimal 3d angle.
        private void ImproveByFlips(bool enforceDeflection)
        {
            Stack<int> work = new Stack<int>();
            int aliveCount = 0;
            for (int t = 0; t < tris.Count; ++t)
            {
                if (tris[t].alive)
                {
                    work.Push(t);
                    ++aliveCount;
                }
            }
            int cap = 20 * aliveCount + 1000;
            while (work.Count > 0 && FlipCount < cap)
            {
                int t = work.Pop();
                if (t >= tris.Count || !tris[t].alive) continue;
                Tri tr = tris[t];
                for (int e = 0; e < 3; ++e)
                {
                    if ((tr.constrained & (1 << e)) != 0) continue;
                    int nb = tr.n[e];
                    if (nb < 0 || !tris[nb].alive) continue;
                    int A = tr.v[e], B = tr.v[Next(e)], C = tr.v[Prev(e)];
                    Tri nbt = tris[nb];
                    int j = EdgeSlot(nbt, B, A);
                    if (j < 0) continue;
                    int D = nbt.v[Prev(j)];
                    // the flipped diagonal is only valid when the quad is strictly convex in uv
                    if (CDTPredicates.Orient2D(nuv[A], nuv[D], nuv[C]) <= 0) continue;
                    if (CDTPredicates.Orient2D(nuv[D], nuv[B], nuv[C]) <= 0) continue;
                    double cur = Math.Min(Min3DAngle(pnt[A], pnt[B], pnt[C]), Min3DAngle(pnt[B], pnt[A], pnt[D]));
                    double flipped = Math.Min(Min3DAngle(pnt[A], pnt[D], pnt[C]), Min3DAngle(pnt[D], pnt[B], pnt[C]));
                    if (flipped <= cur + 1e-12) continue;
                    if (enforceDeflection)
                    {   // the new interior edge must satisfy the deflection requirement
                        EdgeDefl ed = GetEdgeDeflection(C, D);
                        if (ed.dist > maxDeflection) continue;
                    }
                    // perform the flip, reusing both triangle slots
                    int tBC = tr.n[Next(e)], tCA = tr.n[Prev(e)];
                    int tAD = nbt.n[Next(j)], tDB = nbt.n[Prev(j)];
                    bool cBC = (tr.constrained & (1 << Next(e))) != 0;
                    bool cCA = (tr.constrained & (1 << Prev(e))) != 0;
                    bool cAD = (nbt.constrained & (1 << Next(j))) != 0;
                    bool cDB = (nbt.constrained & (1 << Prev(j))) != 0;
                    tr.v[0] = A; tr.v[1] = D; tr.v[2] = C;
                    tr.n[0] = tAD; tr.n[1] = nb; tr.n[2] = tCA;
                    tr.constrained = (byte)((cAD ? 1 : 0) | (cCA ? 4 : 0));
                    nbt.v[0] = D; nbt.v[1] = B; nbt.v[2] = C;
                    nbt.n[0] = tDB; nbt.n[1] = tBC; nbt.n[2] = t;
                    nbt.constrained = (byte)((cDB ? 1 : 0) | (cBC ? 2 : 0));
                    if (tAD >= 0)
                    {
                        int s = EdgeSlot(tris[tAD], D, A);
                        if (s >= 0) tris[tAD].n[s] = t;
                    }
                    if (tBC >= 0)
                    {
                        int s = EdgeSlot(tris[tBC], C, B);
                        if (s >= 0) tris[tBC].n[s] = nb;
                    }
                    vertexTri[A] = t; vertexTri[C] = t; vertexTri[D] = t; vertexTri[B] = nb;
                    ++FlipCount;
                    work.Push(t); work.Push(nb);
                    if (tAD >= 0) work.Push(tAD);
                    if (tBC >= 0) work.Push(tBC);
                    if (tCA >= 0) work.Push(tCA);
                    if (tDB >= 0) work.Push(tDB);
                    break; // t changed, re-examine it from the worklist
                }
            }
        }
        #endregion

        #region output
        private void BuildOutput(out GeoPoint2D[] p2d, out GeoPoint[] p3d, out int[] triangles)
        {
            List<int> emit = new List<int>();
            for (int t = 0; t < tris.Count; ++t)
            {
                if (!tris[t].alive) continue;
                Tri tr = tris[t];
                if (ContainsSuperVertex(tr)) continue;
                // drop 3d-degenerate triangles along a singular line (e.g. the strip of uv
                // triangles collapsing onto a cone apex): they have zero area in 3d, so
                // removing them leaves no gap
                int s1 = -1, s2 = -1;
                for (int i = 0; i < 3; ++i)
                {
                    if (singular[tr.v[i]])
                    {
                        if (s1 < 0) s1 = tr.v[i]; else s2 = tr.v[i];
                    }
                }
                if (s2 >= 0 && (pnt[s1] | pnt[s2]) < eps3) continue;
                emit.Add(t);
            }
            bool[] used = new bool[uv.Count];
            for (int i = 0; i < emit.Count; ++i)
            {
                Tri tr = tris[emit[i]];
                for (int k = 0; k < 3; ++k) used[tr.v[k]] = true;
            }
            // the first inputVertexCount output vertices are exactly the input points in input
            // order (callers rely on this); unused Steiner and super vertices are dropped
            int[] remap = new int[uv.Count];
            for (int i = 0; i < remap.Length; ++i) remap[i] = -1;
            int cnt = inputVertexCount;
            for (int i = 0; i < inputVertexCount; ++i) remap[i] = i;
            for (int i = inputVertexCount; i < uv.Count; ++i)
            {
                if (used[i] && !(i >= superBase && i < superBase + 3)) remap[i] = cnt++;
            }
            p2d = new GeoPoint2D[cnt];
            p3d = new GeoPoint[cnt];
            for (int i = 0; i < uv.Count; ++i)
            {
                if (remap[i] >= 0)
                {
                    p2d[remap[i]] = uv[i];
                    p3d[remap[i]] = pnt[i];
                }
            }
            triangles = new int[emit.Count * 3];
            int ind = 0;
            for (int i = 0; i < emit.Count; ++i)
            {
                Tri tr = tris[emit[i]];
                triangles[ind++] = remap[tr.v[0]];
                triangles[ind++] = remap[tr.v[1]];
                triangles[ind++] = remap[tr.v[2]];
            }
        }
        #endregion
    }
}
