using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CADability
{
    /// <summary>
    /// Opt-in measurement of how <see cref="InterpolatedDualSurfaceCurve"/> and the 2d curves on its two surfaces,
    /// see <see cref="ProjectedCurve.IsCurveOfIntersection"/>, behave on real data. It answers the questions the
    /// planned cleanup of these classes depends on:
    /// <list type="bullet">
    /// <item>which way a point of the curve is actually computed, and how often that fails silently,</item>
    /// <item>whether the per-curve flag "isTangential" matches the geometry along the base points,</item>
    /// <item>whether the uv values the curve stores agree with what <see cref="ISurface.PositionOf"/> returns for
    /// the domain of the surface, point by point and as a continuous row,</item>
    /// <item>who creates, clones and mutates these curves.</item>
    /// </list>
    /// <para>
    /// It is switched on by setting the environment variable <see cref="EnvironmentVariable"/> to the path of the
    /// report to write. It counts and logs, it never asserts and never throws,
    /// and while it is off every hook is a single test of a static readonly bool.
    /// </para>
    /// <para>
    /// While it is on, it evaluates the surfaces at the base points (PositionOf, GetNormal) and walks the stack for
    /// every construction. That may fill caches earlier than a run without the measurement would, so reference runs
    /// for the regression baselines are made with the measurement switched off.
    /// </para>
    /// </summary>
    internal static class DualSurfaceCurveDiagnostics
    {
        /// <summary>
        /// Name of the environment variable. Set it to a file path to switch the measurement on, e.g.
        /// <c>CADABILITY_IDSC_DIAGNOSTICS=C:\temp\idsc.txt</c>.
        /// </summary>
        public const string EnvironmentVariable = "CADABILITY_IDSC_DIAGNOSTICS";

        /// <summary>True when the measurement is switched on.</summary>
        public static readonly bool Enabled;

        /// <summary>|n1 x n2| of the unit normals below this value counts as a point where the surfaces touch. The
        /// same threshold as the marching uses.</summary>
        private const double tangentialSine = 1e-3;
        private const int maxSamples = 40;
        private static readonly TimeSpan writeInterval = TimeSpan.FromSeconds(2);

        private static readonly string reportPath;
        private static readonly object sync = new object();
        private static readonly Stopwatch sinceLastWrite = Stopwatch.StartNew();
        private static long observations;

        // ---- constructions --------------------------------------------------------------------------------

        private enum Contact { Transversal, TouchesAtAnEnd, TouchesInside, Tangential, NoNormal }
        private static readonly string[] contactNames =
            { "transversal", "touches at an end only", "touches inside", "tangential throughout", "no normal available" };

        private sealed class CreatorStats
        {
            public long count;
            public double milliseconds;
            public long basePoints;
            public long flagTrue;
            public long flagContradicts;
        }
        private static readonly Dictionary<string, CreatorStats> creators = new Dictionary<string, CreatorStats>();
        // [Contact, isTangential ? 1 : 0]
        private static readonly long[,] contactByFlag = new long[5, 2];

        /// <summary>How a stored uv value relates to a reference value for the same 3d point.</summary>
        private enum UvAgreement { Equal, PeriodShift, OtherParameter, FirstOff, SecondOff }

        // per surface type, [0 = end point, 1 = inner point, UvAgreement]
        private static readonly Dictionary<string, long[,]> uvAtConstruction = new Dictionary<string, long[,]>();

        // ---- the uv values of the base points as a continuous row -------------------------------------------

        /// <summary>
        /// How the stored uv values of the base points relate to the chain the curve computes: the first point from
        /// PositionOf, which honours the domain of the surface, each further one from PositionOf moved by whole periods
        /// next to its predecessor. The curve used bounds for this until they were found to give the same values.
        /// </summary>
        private enum ChainAgreement { NotPeriodic, Equal, WholeShift, Partial, StoredJumps, NotComparable }
        private static readonly string[] chainNames = { "not periodic", "equal", "whole shift", "partial", "stored jumps", "not comparable" };
        private static readonly Dictionary<string, long[]> chainAtConstruction = new Dictionary<string, long[]>();
        private static readonly List<string> chainSamples = new List<string>();

        // ---- point refinement -----------------------------------------------------------------------------

        private static readonly Dictionary<string, long> refinementBranches = new Dictionary<string, long>();
        // per branch: [on both surfaces and in the plane (eps), within 100 eps, further off, tangent disagrees with plane normal]
        private static readonly Dictionary<string, long[]> refinementQuality = new Dictionary<string, long[]>();
        // [sine bucket: < 1e-3, < 1e-2, >= 1e-2, no normal][isTangential ? 1 : 0], results of a solver only
        private static readonly long[,] refinementContactByFlag = new long[4, 2];

        // ---- everything else ------------------------------------------------------------------------------

        private static readonly Dictionary<string, long> operations = new Dictionary<string, long>();

        private static readonly List<string> fallbackSamples = new List<string>();
        private static readonly List<string> offSurfaceSamples = new List<string>();
        private static readonly List<string> uvSamples = new List<string>();
        private static readonly List<string> failureSamples = new List<string>();

        static DualSurfaceCurveDiagnostics()
        {
            try
            {
                reportPath = Environment.GetEnvironmentVariable(EnvironmentVariable);
                Enabled = !string.IsNullOrWhiteSpace(reportPath);
                if (Enabled) AppDomain.CurrentDomain.ProcessExit += (s, e) => WriteReport();
            }
            catch
            {
                Enabled = false;
            }
        }

        /// <summary>State captured at the beginning of a constructor.</summary>
        internal sealed class ConstructionProbe
        {
            internal long startTimestamp;
        }

        /// <summary>
        /// Called first thing in a constructor of <see cref="InterpolatedDualSurfaceCurve"/>. Returns null while
        /// the measurement is off, which makes the matching <see cref="EndConstruction"/> a no-op.
        /// </summary>
        internal static ConstructionProbe BeginConstruction()
        {
            if (!Enabled) return null;
            return new ConstructionProbe { startTimestamp = Stopwatch.GetTimestamp() };
        }

        /// <summary>
        /// Called last thing in a constructor (or after deserialization, then with an explicit
        /// <paramref name="creator"/>). Records who made the curve and what it looks like.
        /// </summary>
        internal static void EndConstruction(ConstructionProbe probe, ISurface surface1, ISurface surface2,
            InterpolatedDualSurfaceCurve.SurfacePoint[] basePoints, bool isTangential, string creator = null)
        {
            if (probe == null || !Enabled) return;
            try
            {
                double milliseconds = (Stopwatch.GetTimestamp() - probe.startTimestamp) * 1000.0 / Stopwatch.Frequency;
                if (creator == null) creator = Creator();
                Contact contact = ClassifyContact(surface1, surface2, basePoints);
                bool contradicts = (isTangential && (contact == Contact.Transversal || contact == Contact.TouchesAtAnEnd))
                                || (!isTangential && contact == Contact.Tangential);
                double tol3d = Tolerance3d(basePoints);

                // evaluate outside the lock, the surfaces may take their time
                UvAgreement[] uv1 = ClassifyStoredUv(surface1, basePoints, true, tol3d);
                UvAgreement[] uv2 = ClassifyStoredUv(surface2, basePoints, false, tol3d);
                ChainAgreement c1 = ClassifyChain(surface1, basePoints, true, tol3d, out string chainDetail1);
                ChainAgreement c2 = ClassifyChain(surface2, basePoints, false, tol3d, out string chainDetail2);

                lock (sync)
                {
                    if (!creators.TryGetValue(creator, out CreatorStats stats))
                    {
                        stats = new CreatorStats();
                        creators[creator] = stats;
                    }
                    stats.count++;
                    stats.milliseconds += milliseconds;
                    stats.basePoints += basePoints.Length;
                    if (isTangential) stats.flagTrue++;
                    if (contradicts) stats.flagContradicts++;
                    contactByFlag[(int)contact, isTangential ? 1 : 0]++;
                    AddUv(uvAtConstruction, surface1, uv1, creator, "construction, surface1", basePoints, true);
                    AddUv(uvAtConstruction, surface2, uv2, creator, "construction, surface2", basePoints, false);
                    AddChain(chainAtConstruction, surface1, c1, chainDetail1, "construction, surface1 <- " + creator);
                    AddChain(chainAtConstruction, surface2, c2, chainDetail2, "construction, surface2 <- " + creator);
                    Observed();
                }
            }
            catch
            {   // a measurement must never change the outcome of the run it measures
            }
        }

        private static ChainAgreement ClassifyChain(ISurface surface, InterpolatedDualSurfaceCurve.SurfacePoint[] basePoints, bool onSurface1, double tol3d, out string detail)
        {
            detail = null;
            if (!surface.IsUPeriodic && !surface.IsVPeriodic) return ChainAgreement.NotPeriodic;
            double uPeriod = surface.IsUPeriodic ? surface.UPeriod : 0.0, vPeriod = surface.IsVPeriodic ? surface.VPeriod : 0.0;
            for (int i = 1; i < basePoints.Length; i++)
            {
                GeoPoint2D a = onSurface1 ? basePoints[i - 1].psurface1 : basePoints[i - 1].psurface2;
                GeoPoint2D b = onSurface1 ? basePoints[i].psurface1 : basePoints[i].psurface2;
                if ((uPeriod > 0.0 && Math.Abs(b.x - a.x) > uPeriod / 2) || (vPeriod > 0.0 && Math.Abs(b.y - a.y) > vPeriod / 2))
                {
                    detail = "jump after point " + (i - 1).ToString(CultureInfo.InvariantCulture) + ": " + Format(a) + " -> " + Format(b);
                    return ChainAgreement.StoredJumps;
                }
            }
            bool havePrevious = false, haveShift = false, partial = false;
            GeoPoint2D previous = GeoPoint2D.Origin;
            double shiftU = 0.0, shiftV = 0.0;
            int compared = 0;
            for (int i = 0; i < basePoints.Length; i++)
            {
                GeoPoint p3d = basePoints[i].p3d;
                GeoPoint2D stored = onSurface1 ? basePoints[i].psurface1 : basePoints[i].psurface2;
                GeoPoint2D chain = surface.PositionOf(p3d);
                if (havePrevious) InterpolatedDualSurfaceCurve.SurfacePoint.FixSurfacePoint2D(ref chain, previous, surface.IsUPeriodic, uPeriod, surface.IsVPeriodic, vPeriod);
                UvAgreement a = Compare(surface, stored, chain, p3d, tol3d);
                if (a != UvAgreement.Equal && a != UvAgreement.PeriodShift) continue; // a pole or a point off the surface says nothing about periods
                previous = chain;
                havePrevious = true;
                compared++;
                double ku = uPeriod > 0.0 ? Math.Round((stored.x - chain.x) / uPeriod) : 0.0;
                double kv = vPeriod > 0.0 ? Math.Round((stored.y - chain.y) / vPeriod) : 0.0;
                if (!haveShift)
                {
                    shiftU = ku;
                    shiftV = kv;
                    haveShift = true;
                }
                else if (ku != shiftU || kv != shiftV) partial = true;
            }
            if (compared == 0) return ChainAgreement.NotComparable;
            if (partial)
            {
                detail = "shift at the first point " + shiftU.ToString(CultureInfo.InvariantCulture) + ", " + shiftV.ToString(CultureInfo.InvariantCulture) + " periods, then others";
                return ChainAgreement.Partial;
            }
            if (shiftU == 0.0 && shiftV == 0.0) return ChainAgreement.Equal;
            detail = "stored = chain + (" + shiftU.ToString(CultureInfo.InvariantCulture) + ", " + shiftV.ToString(CultureInfo.InvariantCulture) + ") periods, stored first point " +
                Format(onSurface1 ? basePoints[0].psurface1 : basePoints[0].psurface2);
            return ChainAgreement.WholeShift;
        }

        private static void AddChain(Dictionary<string, long[]> into, ISurface surface, ChainAgreement agreement, string detail, string origin)
        {
            Increment(into, TypeName(surface), (int)agreement, chainNames.Length);
            if (agreement != ChainAgreement.NotPeriodic && agreement != ChainAgreement.Equal && chainSamples.Count < maxSamples)
            {
                TryGetDomain(surface, out BoundingRect domain);
                chainSamples.Add(string.Format(CultureInfo.InvariantCulture, "{0,-26} {1}: {2}  domain {3}   ({4})",
                    TypeName(surface), agreement, detail, Format(domain), origin));
            }
        }

        private static string Format(BoundingRect r) => r.IsEmpty() ? "empty" : string.Format(CultureInfo.InvariantCulture, "[{0:G6}, {1:G6}] x [{2:G6}, {3:G6}]", r.Left, r.Right, r.Bottom, r.Top);

        /// <summary>A point was found in the cache of already computed positions.</summary>
        internal static void RecordCacheHit()
        {
            if (!Enabled) return;
            try
            {
                lock (sync)
                {
                    Increment(refinementBranches, "cache hit");
                    Observed();
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// A point of the curve has been computed by one of the branches of the point refinement.
        /// <paramref name="isFallback"/> marks the path that returns the point of the approximation without any
        /// refinement, but stores it as if it were exact.
        /// </summary>
        internal static void RecordRefinement(string branch, bool isFallback, bool planeFromSpline, bool isTangential,
            ISurface surface1, GeoPoint2D uv1, ISurface surface2, GeoPoint2D uv2, Plane plane, GeoPoint p)
        {
            if (!Enabled) return;
            try
            {
                string key = branch + (planeFromSpline ? ", plane from the spline" : ", plane from the base polygon");
                GeoPoint p1 = surface1.PointAt(uv1);
                GeoPoint p2 = surface2.PointAt(uv2);
                double gap = p1 | p2;
                double offPlane = Math.Abs(plane.Distance(p));
                int quality = (gap <= Precision.eps && offPlane <= Precision.eps) ? 0
                            : (gap <= 100 * Precision.eps && offPlane <= 100 * Precision.eps) ? 1 : 2;
                double sine = Sine(surface1, uv1, surface2, uv2, out GeoVector tangent);
                int sineBucket = double.IsNaN(sine) ? 3 : sine < tangentialSine ? 0 : sine < 1e-2 ? 1 : 2;
                // a transversal result whose tangent n1 x n2 is far from the normal of the plane most probably lies
                // on another branch of the intersection
                bool tangentDisagrees = !isFallback && sineBucket == 2
                    && Math.Abs(tangent.Normalized * plane.Normal.Normalized) < Math.Cos(20.0 / 180.0 * Math.PI);
                string stack = (isFallback || quality == 2) ? Callers(6) : null;
                lock (sync)
                {
                    Increment(refinementBranches, key);
                    if (!refinementQuality.TryGetValue(key, out long[] q))
                    {
                        q = new long[4];
                        refinementQuality[key] = q;
                    }
                    q[quality]++;
                    if (tangentDisagrees) q[3]++;
                    if (!isFallback) refinementContactByFlag[sineBucket, isTangential ? 1 : 0]++;
                    string sample = string.Format(CultureInfo.InvariantCulture, "{0}: gap={1:G3} offPlane={2:G3} sine={3:G3} isTangential={4}\n      <- {5}",
                        key, gap, offPlane, sine, isTangential, stack);
                    if (isFallback && fallbackSamples.Count < maxSamples) fallbackSamples.Add(sample);
                    else if (!isFallback && quality == 2 && offSurfaceSamples.Count < maxSamples) offSurfaceSamples.Add(sample);
                    Observed();
                }
            }
            catch
            {
            }
        }

        /// <summary>Counts an operation, e.g. a clone of the 3d curve made by a 2d curve.</summary>
        internal static void Count(string operation)
        {
            if (!Enabled) return;
            try
            {
                lock (sync)
                {
                    Increment(operations, operation);
                    Observed();
                }
            }
            catch
            {
            }
        }

        /// <summary>A constructor threw and the caller swallowed the exception.</summary>
        internal static void RecordConstructionFailure(string where, Exception ex)
        {
            if (!Enabled) return;
            try
            {
                lock (sync)
                {
                    Increment(operations, "constructor threw, caught in " + where);
                    if (failureSamples.Count < maxSamples)
                        failureSamples.Add(where + ": " + ex.GetType().Name + ": " + ex.Message + "\n      at " + FirstFrames(ex, 4));
                    Observed();
                }
            }
            catch
            {
            }
        }

        // ---- classification -------------------------------------------------------------------------------

        private static Contact ClassifyContact(ISurface surface1, ISurface surface2, InterpolatedDualSurfaceCurve.SurfacePoint[] basePoints)
        {
            bool anyKnown = false, allTouch = true, touchAtEnd = false, touchInside = false;
            for (int i = 0; i < basePoints.Length; i++)
            {
                double sine = Sine(surface1, basePoints[i].psurface1, surface2, basePoints[i].psurface2, out _);
                if (double.IsNaN(sine)) continue;
                anyKnown = true;
                if (sine >= tangentialSine) allTouch = false;
                else if (i == 0 || i == basePoints.Length - 1) touchAtEnd = true;
                else touchInside = true;
            }
            if (!anyKnown) return Contact.NoNormal;
            if (allTouch) return Contact.Tangential;
            if (touchInside) return Contact.TouchesInside;
            if (touchAtEnd) return Contact.TouchesAtAnEnd;
            return Contact.Transversal;
        }

        /// <summary>|n1 x n2| of the unit normals, NaN where a normal is not available (e.g. at a pole).</summary>
        private static double Sine(ISurface surface1, GeoPoint2D uv1, ISurface surface2, GeoPoint2D uv2, out GeoVector cross)
        {
            cross = GeoVector.NullVector;
            GeoVector n1 = surface1.GetNormal(uv1);
            GeoVector n2 = surface2.GetNormal(uv2);
            if (!(n1.Length > 0.0) || !(n2.Length > 0.0)) return double.NaN;
            cross = n1.Normalized ^ n2.Normalized;
            return cross.Length;
        }

        private static UvAgreement[] ClassifyStoredUv(ISurface surface, InterpolatedDualSurfaceCurve.SurfacePoint[] basePoints, bool onSurface1, double tol3d)
        {
            UvAgreement[] res = new UvAgreement[basePoints.Length];
            for (int i = 0; i < basePoints.Length; i++)
            {
                GeoPoint2D stored = onSurface1 ? basePoints[i].psurface1 : basePoints[i].psurface2;
                GeoPoint2D reference = surface.PositionOf(basePoints[i].p3d);
                res[i] = Compare(surface, stored, reference, basePoints[i].p3d, tol3d);
            }
            return res;
        }

        /// <summary>
        /// Compares two parameter values <paramref name="first"/> and <paramref name="second"/> which should both
        /// describe <paramref name="p3d"/>.
        /// </summary>
        private static UvAgreement Compare(ISurface surface, GeoPoint2D first, GeoPoint2D second, GeoPoint p3d, double tol3d)
        {
            if ((surface.PointAt(first) | p3d) > tol3d) return UvAgreement.FirstOff;
            if ((surface.PointAt(second) | p3d) > tol3d) return UvAgreement.SecondOff;
            double du = first.x - second.x;
            double dv = first.y - second.y;
            bool shifted = false;
            if (surface.IsUPeriodic && surface.UPeriod > 0.0)
            {
                double k = Math.Round(du / surface.UPeriod);
                if (k != 0.0)
                {
                    du -= k * surface.UPeriod;
                    shifted = true;
                }
            }
            if (surface.IsVPeriodic && surface.VPeriod > 0.0)
            {
                double k = Math.Round(dv / surface.VPeriod);
                if (k != 0.0)
                {
                    dv -= k * surface.VPeriod;
                    shifted = true;
                }
            }
            double tolU = 1e-6 * Math.Max(1.0, surface.IsUPeriodic ? surface.UPeriod : Math.Max(Math.Abs(first.x), Math.Abs(second.x)));
            double tolV = 1e-6 * Math.Max(1.0, surface.IsVPeriodic ? surface.VPeriod : Math.Max(Math.Abs(first.y), Math.Abs(second.y)));
            if (Math.Abs(du) <= tolU && Math.Abs(dv) <= tolV) return shifted ? UvAgreement.PeriodShift : UvAgreement.Equal;
            return UvAgreement.OtherParameter; // same 3d point, different parameters: a pole or another singularity
        }

        private static bool TryGetDomain(ISurface surface, out BoundingRect domain)
        {
            domain = BoundingRect.EmptyBoundingRect;
            if (surface == null) return false;
            try
            {
                domain = surface is ISurfaceImpl simpl ? simpl.usedArea : BoundingRect.EmptyBoundingRect;
                return !domain.IsEmpty();
            }
            catch
            {   // ScaledSurface throws on Domain
                return false;
            }
        }

        private static double Tolerance3d(InterpolatedDualSurfaceCurve.SurfacePoint[] basePoints)
        {
            BoundingBox ext = BoundingBox.EmptyBoundingCube;
            for (int i = 0; i < basePoints.Length; i++) ext.MinMax(basePoints[i].p3d);
            return Math.Max(100 * Precision.eps, 1e-6 * ext.Size);
        }

        // ---- bookkeeping ----------------------------------------------------------------------------------

        private static void AddUv(Dictionary<string, long[,]> into, ISurface surface, UvAgreement[] agreement, string creator, string origin,
            InterpolatedDualSurfaceCurve.SurfacePoint[] basePoints, bool onSurface1)
        {
            string type = TypeName(surface);
            if (!into.TryGetValue(type, out long[,] counts))
            {
                counts = new long[2, 5];
                into[type] = counts;
            }
            int last = agreement.Length - 1;
            for (int i = 0; i <= last; i++)
            {
                UvAgreement a = agreement[i];
                counts[(i == 0 || i == last) ? 0 : 1, (int)a]++;
                if (a != UvAgreement.Equal && uvSamples.Count < maxSamples)
                {
                    GeoPoint2D stored = onSurface1 ? basePoints[i].psurface1 : basePoints[i].psurface2;
                    uvSamples.Add(string.Format(CultureInfo.InvariantCulture, "{0,-26} {1,-24} point {2} of {3}: {4}, stored {5}{6}",
                        type, origin, i, last + 1, a, Format(stored), creator == null ? "" : "   <- " + creator));
                }
            }
        }

        private static void Increment(Dictionary<string, long[]> into, string key, int index, int length)
        {
            if (!into.TryGetValue(key, out long[] counts))
            {
                counts = new long[length];
                into[key] = counts;
            }
            counts[index]++;
        }

        private static void Increment(Dictionary<string, long> into, string key)
        {
            into.TryGetValue(key, out long n);
            into[key] = n + 1;
        }

        /// <summary>Must be called under the lock. Writes the report every <see cref="writeInterval"/>, because the
        /// test host does not reliably run ProcessExit handlers.</summary>
        private static void Observed()
        {
            observations++;
            if (observations <= 4 || sinceLastWrite.Elapsed > writeInterval)
            {
                WriteReportNoLock();
                sinceLastWrite.Restart();
            }
        }

        private static string TypeName(ISurface surface) => surface == null ? "(null)" : surface.GetType().Name;

        private static string Format(GeoPoint2D p) => string.Format(CultureInfo.InvariantCulture, "({0:G8}, {1:G8})", p.x, p.y);

        /// <summary>
        /// The two methods which asked for the new curve: the first frame outside the constructors of
        /// <see cref="InterpolatedDualSurfaceCurve"/> and the one above it.
        /// </summary>
        private static string Creator()
        {
            StackTrace stack = new StackTrace(1, false);
            List<string> names = new List<string>();
            for (int i = 0; i < stack.FrameCount && names.Count < 2; i++)
            {
                MethodBase method = stack.GetFrame(i)?.GetMethod();
                if (method == null) continue;
                Type type = method.DeclaringType;
                if (type == typeof(DualSurfaceCurveDiagnostics)) continue;
                if (method is ConstructorInfo && type == typeof(InterpolatedDualSurfaceCurve)) continue;
                names.Add(Name(type) + "." + method.Name);
            }
            return names.Count > 0 ? string.Join(" <- ", names) : "(unknown)";
        }

        private static string Callers(int depth)
        {
            StackTrace stack = new StackTrace(2, false);
            List<string> names = new List<string>();
            for (int i = 0; i < stack.FrameCount && names.Count < depth; i++)
            {
                MethodBase method = stack.GetFrame(i)?.GetMethod();
                if (method == null) continue;
                names.Add(Name(method.DeclaringType) + "." + method.Name);
            }
            return string.Join(" <- ", names);
        }

        private static string FirstFrames(Exception ex, int depth)
        {
            StackTrace stack = new StackTrace(ex, false);
            List<string> names = new List<string>();
            for (int i = 0; i < stack.FrameCount && names.Count < depth; i++)
            {
                MethodBase method = stack.GetFrame(i)?.GetMethod();
                if (method != null) names.Add(Name(method.DeclaringType) + "." + method.Name);
            }
            return string.Join(" <- ", names);
        }

        /// <summary>The type name, with the enclosing type for nested types (so that the two ProjectedCurve classes
        /// can be told apart) and without the compiler generated closure classes.</summary>
        private static string Name(Type type)
        {
            if (type == null) return "?";
            while (type.Name.StartsWith("<", StringComparison.Ordinal) && type.DeclaringType != null) type = type.DeclaringType;
            return type.DeclaringType != null ? type.DeclaringType.Name + "+" + type.Name : type.Name;
        }

        // ---- report ---------------------------------------------------------------------------------------

        /// <summary>
        /// Writes the report. Called automatically while the measurement runs and when the process exits; public
        /// so a single test can ask for it explicitly.
        /// </summary>
        public static void WriteReport()
        {
            if (!Enabled) return;
            lock (sync) WriteReportNoLock();
        }

        private static void WriteReportNoLock()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("InterpolatedDualSurfaceCurve diagnostics");
                sb.AppendLine("========================================");
                sb.AppendLine("observations: " + observations.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine();

                AppendCreators(sb);
                AppendContact(sb);
                AppendUv(sb, "3. Stored uv of the base points against PositionOf, at construction", uvAtConstruction);
                AppendChain(sb, "5d. Stored uv of the base points against the chain, at construction", chainAtConstruction);
                AppendRefinement(sb);
                AppendCounters(sb, "7. Operations", operations);
                AppendSamples(sb, "Samples: fallback, the unrefined point was stored as exact", fallbackSamples);
                AppendSamples(sb, "Samples: a solver result accepted although it is off the surfaces or off the plane", offSurfaceSamples);
                AppendSamples(sb, "Samples: stored uv not equal to PositionOf", uvSamples);
                AppendSamples(sb, "Samples: exceptions from the constructor", failureSamples);
                AppendSamples(sb, "Samples: stored uv of the base points not equal to the chain", chainSamples);

                File.WriteAllText(reportPath, sb.ToString());
            }
            catch
            {   // reporting must not break the run either
            }
        }

        private static void AppendCreators(StringBuilder sb)
        {
            Caption(sb, "1. Constructions by creator (frame that called the constructor <- its caller)");
            if (creators.Count == 0)
            {
                sb.AppendLine("  (nothing observed)");
                sb.AppendLine();
                return;
            }
            sb.AppendLine("  flag contradicts = isTangential is true but the surfaces intersect transversally at all inner points,");
            sb.AppendLine("  or isTangential is false and the surfaces touch at every base point.");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,10} {1,12} {2,9} {3,7} {4,10} {5,11}  {6}",
                "count", "ms total", "ms avg", "points", "tang=true", "contradicts", "creator"));
            long total = 0;
            double totalMs = 0.0;
            foreach (KeyValuePair<string, CreatorStats> e in creators.OrderByDescending(e => e.Value.milliseconds))
            {
                CreatorStats s = e.Value;
                total += s.count;
                totalMs += s.milliseconds;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,10} {1,12:F0} {2,9:F2} {3,7:F1} {4,10} {5,11}  {6}",
                    s.count, s.milliseconds, s.milliseconds / s.count, (double)s.basePoints / s.count, s.flagTrue, s.flagContradicts, e.Key));
            }
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,10} {1,12:F0}  TOTAL", total, totalMs));
            sb.AppendLine();
        }

        private static void AppendContact(StringBuilder sb)
        {
            Caption(sb, "2. Contact of the surfaces along the base points against the flag isTangential (|n1 x n2| < 1e-3 is a touching point)");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-26} {1,14} {2,14}", "", "flag false", "flag true"));
            for (int i = 0; i < contactNames.Length; i++)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-26} {1,14} {2,14}", contactNames[i], contactByFlag[i, 0], contactByFlag[i, 1]));
            }
            sb.AppendLine();
        }

        private static readonly string[] uvNames = { "equal", "period shift", "other param", "stored off", "PositionOf off" };

        private static void AppendUv(StringBuilder sb, string caption, Dictionary<string, long[,]> counts)
        {
            Caption(sb, caption);
            if (counts.Count == 0)
            {
                sb.AppendLine("  (nothing observed)");
                sb.AppendLine();
                return;
            }
            sb.AppendLine("  other param = both describe the same 3d point with parameters that do not differ by periods (pole).");
            sb.AppendLine("  stored off  = the stored uv does not reproduce the 3d point of the base point.");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-30} {1,-6} {2,12} {3,12} {4,12} {5,12} {6,14}",
                "surface type", "point", uvNames[0], uvNames[1], uvNames[2], uvNames[3], uvNames[4]));
            foreach (KeyValuePair<string, long[,]> e in counts.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                for (int row = 0; row < 2; row++)
                {
                    long[,] c = e.Value;
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-30} {1,-6} {2,12} {3,12} {4,12} {5,12} {6,14}",
                        row == 0 ? e.Key : "", row == 0 ? "ends" : "inner", c[row, 0], c[row, 1], c[row, 2], c[row, 3], c[row, 4]));
                }
            }
            sb.AppendLine();
        }

        private static void AppendChain(StringBuilder sb, string caption, Dictionary<string, long[]> counts)
        {
            Caption(sb, caption);
            if (counts.Count == 0)
            {
                sb.AppendLine("  (nothing observed)");
                sb.AppendLine();
                return;
            }
            sb.AppendLine("  chain = the first point from PositionOf (in the domain), every further one moved by periods next to its predecessor.");
            sb.AppendLine("  whole shift = stored and chain differ by the same periods everywhere; partial = by different ones.");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-30} {1,13} {2,10} {3,12} {4,10} {5,13} {6,15}",
                "surface type", chainNames[0], chainNames[1], chainNames[2], chainNames[3], chainNames[4], chainNames[5]));
            foreach (KeyValuePair<string, long[]> e in counts.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                long[] c = e.Value;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-30} {1,13} {2,10} {3,12} {4,10} {5,13} {6,15}",
                    e.Key, c[0], c[1], c[2], c[3], c[4], c[5]));
            }
            sb.AppendLine();
        }

        private static void AppendRefinement(StringBuilder sb)
        {
            Caption(sb, "6. Point refinement (ApproximatePosition)");
            if (refinementBranches.Count == 0)
            {
                sb.AppendLine("  (nothing observed)");
                sb.AppendLine();
                return;
            }
            sb.AppendLine("  exact = |p1 - p2| and the distance to the plane both <= Precision.eps; near = within 100 eps; off = further.");
            sb.AppendLine("  wrong tangent = transversal result whose n1 x n2 is more than 20 degrees off the plane normal (other branch?).");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,12} {1,10} {2,10} {3,10} {4,13}  {5}",
                "calls", "exact", "near", "off", "wrong tangent", "branch"));
            foreach (KeyValuePair<string, long> e in refinementBranches.OrderByDescending(e => e.Value))
            {
                refinementQuality.TryGetValue(e.Key, out long[] q);
                if (q == null) sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,12} {1,10} {2,10} {3,10} {4,13}  {5}", e.Value, "", "", "", "", e.Key));
                else sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,12} {1,10} {2,10} {3,10} {4,13}  {5}", e.Value, q[0], q[1], q[2], q[3], e.Key));
            }
            sb.AppendLine();
            sb.AppendLine("  |n1 x n2| at the points found by a solver, against the flag isTangential of the curve:");
            string[] buckets = { "< 1e-3 (touching)", "< 1e-2", ">= 1e-2 (transversal)", "no normal" };
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-24} {1,14} {2,14}", "", "flag false", "flag true"));
            for (int i = 0; i < buckets.Length; i++)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-24} {1,14} {2,14}", buckets[i], refinementContactByFlag[i, 0], refinementContactByFlag[i, 1]));
            }
            sb.AppendLine();
        }

        private static void AppendCounters(StringBuilder sb, string caption, Dictionary<string, long> counts)
        {
            Caption(sb, caption);
            if (counts.Count == 0) sb.AppendLine("  (nothing observed)");
            foreach (KeyValuePair<string, long> e in counts.OrderByDescending(e => e.Value))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,12}  {1}", e.Value, e.Key));
            }
            sb.AppendLine();
        }

        private static void AppendSamples(StringBuilder sb, string caption, List<string> samples)
        {
            Caption(sb, caption + " (at most " + maxSamples + ")");
            if (samples.Count == 0) sb.AppendLine("  (none)");
            else foreach (string s in samples) sb.AppendLine("  " + s);
            sb.AppendLine();
        }

        private static void Caption(StringBuilder sb, string caption)
        {
            sb.AppendLine(caption);
            sb.AppendLine(new string('-', Math.Min(caption.Length, 110)));
        }
    }
}
