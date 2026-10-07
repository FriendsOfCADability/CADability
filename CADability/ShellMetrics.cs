using CADability.Curve2D;
using CADability.Shapes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CADability.GeoObject
{
    /// <summary>
    /// Measures a shell: the enclosed volume and the surface area integrated over the parameter domains of the faces
    /// (exact up to the quadrature, independent of the triangulation), the size of the shell and some topological
    /// counts. Ported from the ShapeIt application, where it measures the results of the modeling operations.
    /// </summary>
    public static class ShellMetrics
    {
        /// <summary>
        /// The triangulation precision, relative to the size of the shell.
        /// <para>
        /// size/4000 since 2026-09-07, size/1000 before that. The volume is still summed over the triangulation,
        /// so the mesh decides how close the number is; measured by halving the precision step by step, the
        /// recorded volume of the two NURBS shells of DifferenceBug15 sat 1.6e-4 and 7.6e-4 away from the value
        /// the sequence converges to - more than the 1e-4 the baselines are compared with, which is exactly why
        /// that case kept showing up in the diff. Four times finer brings the residual movement to about 1e-5.
        /// Quadrics were never the problem: the 3/4 sag correction in <see cref="Shell.SignedVolume"/> is tuned
        /// for them and DifferenceBug9 was within 3e-6 even at size/1000.
        /// </para>
        /// </summary>
        private const double RelativeTriangulationPrecision = 2.5e-4;

        /// <summary>
        /// The size of the shell, from the exact geometry only - vertex positions and edge curves, never from a
        /// triangulation - so it is the same number in every run. This is the absolute floor for comparing
        /// coordinates near zero, and the base <see cref="PrecisionFor"/> derives the mesh precision from.
        /// </summary>
        public static double SizeOf(Shell shell)
        {
            BoundingBox box = ExactExtentOf(shell);
            double size = box.IsEmpty ? 1.0 : box.Size;
            return Math.Max(size, 1e-6);
        }

        /// <summary>The extent of the shell from vertices and edge curves only, see <see cref="SizeOf"/>.</summary>
        private static BoundingBox ExactExtentOf(Shell shell)
        {
            BoundingBox box = BoundingBox.EmptyBoundingCube;
            foreach (Vertex vertex in shell.Vertices) box.MinMax(vertex.Position);
            foreach (Edge edge in shell.Edges) if (edge.Curve3D != null) box.MinMax(edge.Curve3D.GetExtent());
            return box;
        }

        /// <summary>
        /// The precision volume, area and extent are computed with. It must not be 0.0, which is what the rest of
        /// the code passes: <see cref="Face.AssureTriangles"/> then reuses whatever triangulation happens to exist
        /// - however coarse it was made - and invents "extent size / 10" when there is none.
        /// <para>
        /// Asking for an explicit precision is only half the story: a face keeps a triangulation that is fine enough
        /// already, so a measurement which must not depend on earlier calls should run on a copy of the shell.
        /// </para>
        /// </summary>
        public static double PrecisionFor(Shell shell) => SizeOf(shell) * RelativeTriangulationPrecision;

        /// <summary>
        /// A pole edge has no 3d curve and starts and ends at the same vertex - the degenerate boundary at the
        /// apex of a cone or at the pole of a sphere. It is a boundary in parameter space only and must not be
        /// counted as an edge of the solid.
        /// </summary>
        public static bool IsPoleEdge(Edge edge) => edge.Curve3D == null;

        /// <summary>Edges that really are edges of the solid, i.e. everything except the poles.</summary>
        public static int RealEdgeCount(Shell shell) => shell.Edges.Count(e => !IsPoleEdge(e));

        /// <summary>The number of inner loops (holes) over all faces of the shell.</summary>
        public static int HoleLoopCount(Shell shell)
        {
            int count = 0;
            foreach (Face face in shell.Faces) count += face.HoleCount;
            return count;
        }

        /// <summary>
        /// V - E + F - R with E counting only real edges (no poles) and R the number of inner loops. Subtracting
        /// R is what makes the number meaningful for faces with holes, which the plain V - E + F does not handle:
        /// the Euler-Poincare formula for a b-rep requires every face to be a disk.
        /// <para>
        /// For a single closed shell the result is 2 - 2*genus: 2 for anything sphere-like, 0 with one through
        /// hole, -2 with two, and so on. It is a topology fingerprint, not an assertion - a wrong value in the
        /// baseline diff means faces, edges or loops got lost or duplicated.
        /// </para>
        /// </summary>
        public static int EulerCharacteristic(Shell shell)
            => shell.Vertices.Length - RealEdgeCount(shell) + shell.Faces.Length - HoleLoopCount(shell);

        /// <summary>
        /// The 3d surface area: the integral of |Su x Sv| over the parameter domain of every face, by the same
        /// boundary integral the volume uses, see <see cref="IntegrateOverDomain"/>. A planar face is closed form,
        /// |Su x Sv| is constant there and the area is that times the exact area of the domain - which makes a
        /// trimmed disc pi*r^2 instead of the inscribed polygon.
        /// <para>
        /// The history, because both earlier versions were steps in the right direction and both left the mesh in
        /// the value. Until 2026-08-25 the flat triangles were summed, which underestimates a curved face by an
        /// amount that only falls with the square of the mesh size. Until 2026-09-24 the uv triangles partitioned
        /// the domain, with the shortfall along a curved outline corrected by the factor domain/covered and a
        /// switch back to the flat sum beyond 2 percent. That was the same weakness the volume had at the apex of
        /// a trimmed cone, where the triangulation drops the degenerate triangles: the small part
        /// coneCylConeMinusCyl#1 of ConeCylinderCommonInsphere, whose exact area is 410.4147, was recorded as
        /// 410.80, 412.75, 413.27 and 412.78 by successive versions of unrelated code.
        /// </para>
        /// <para>
        /// The mesh is triangulated only for a face that has to fall back to the flat sum.
        /// </para>
        /// </summary>
        public static double SurfaceArea(Shell shell)
        {
            double precision = PrecisionFor(shell);
            double size = SizeOf(shell);
            double sum = 0.0;
            foreach (Face face in shell.Faces) sum += IntegratedFaceArea(face, precision, size);
            return sum;
        }

        /// <summary>
        /// The number of faces whose area fell back to the flat triangle sum since the process started.
        /// Diagnostics only, like <see cref="VolumeFallbackCount"/>.
        /// </summary>
        public static int AreaFallbackCount => areaFallbackCount;
        private static int areaFallbackCount;

        /// <summary>The area of one face: closed form for a plane, the boundary integral otherwise, see <see cref="SurfaceArea"/>.</summary>
        private static double IntegratedFaceArea(Face face, double precision, double size)
        {
            ISurface surface = face.Surface;
            if (surface != null)
            {
                SimpleShape shape;
                double domain;
                BoundingRect rect;
                try { shape = face.Area; domain = shape.Area; rect = shape.GetExtent(); }
                catch (Exception) { shape = null; domain = 0.0; rect = BoundingRect.EmptyBoundingRect; }
                if (shape != null && domain > 0.0)
                {
                    if (surface is PlaneSurface)
                    {   // constant integrand: exact, and it needs neither the mesh nor a quadrature
                        double jacobian = Evaluate(surface, rect.GetCenter(), AreaDensity);
                        if (jacobian > 0.0) return jacobian * domain;
                    }
                    else
                    {
                        double green = IntegrateOverDomain(surface, shape, rect, AreaDensity, size * size);
                        if (green > 0.0) return green;
                    }
                }
            }
            System.Threading.Interlocked.Increment(ref areaFallbackCount);
            return FlatArea(face, precision);
        }

        /// <summary><c>|Su x Sv|</c>, the integrand of the area.</summary>
        private static double AreaDensity(GeoPoint location, GeoVector du, GeoVector dv) => (du ^ dv).Length;

        /// <summary>The sum of the flat triangles of the mesh, the fallback of <see cref="IntegratedFaceArea"/>.</summary>
        private static double FlatArea(Face face, double precision)
        {
            face.GetTriangulation(precision, out GeoPoint[] points, out GeoPoint2D[] _, out int[] indices, out _);
            if (indices == null) return 0.0;
            double flat = 0.0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                GeoVector a = points[indices[i + 1]] - points[indices[i]];
                GeoVector b = points[indices[i + 2]] - points[indices[i]];
                flat += 0.5 * (a ^ b).Length;
            }
            return flat;
        }

        /// <summary>
        /// The enclosed volume, integrated over the parameter domain of every face rather than summed over its
        /// triangles.
        /// <para>
        /// By the divergence theorem the volume of a closed body is <c>1/3 * closed integral of (r - c).n dA</c>
        /// for any fixed point c, and for a parametrized face <c>n dA = (Su x Sv) du dv</c>, so one face
        /// contributes
        /// </para>
        /// <para><c>integral over D of f(u,v) du dv,   f = (S(u,v) - c).(Su x Sv) / 3</c></para>
        /// <para>
        /// That integral over the uv domain D is turned into an integral over the BOUNDARY of D by Green's
        /// theorem, see <see cref="GreenFaceIntegral"/>. The boundary is the 2d outline the face carries anyway,
        /// so the triangulation does not enter the value: it is used only to read the orientation of the face,
        /// see <see cref="OrientationOf"/>, and for the fallback of a face the boundary route cannot handle.
        /// Planar faces keep their closed form, a constant integrand times the area of the domain.
        /// </para>
        /// <para>
        /// Until 2026-09-24 the domain was partitioned by the uv triangles of the mesh, with the shortfall along a
        /// curved outline corrected by the factor domain/covered, and a switch to the tetrahedron sum beyond 2
        /// percent. That was good for most faces and bad at a pole: the triangulation drops the degenerate
        /// triangles next to the apex of a cone, so 2 to 23 percent of the domain of a trimmed pointed cone went
        /// uncovered depending on the mesh, and since the integrand vanishes towards the apex the global factor
        /// overcorrected. Measured on a pointed cone with a box cut out of it, five meshes gave errors between
        /// -0.07 and -1.8 percent, jumping whenever the mesh flipped the route. The boundary integral has no such
        /// switch, and a pole needs no special case: it is a line of constant v in the uv plane and contributes
        /// nothing to an integral over dv.
        /// </para>
        /// <para>
        /// c is the center of the exact extent of the shell, not the origin. For a closed shell that changes
        /// nothing, the shift adds <c>1/3 * c . closed integral of n dA</c>, which vanishes. But the contribution
        /// of every single face shrinks to the size of the shell, and with it whatever error a face carries - a
        /// shell far away from the origin used to be a sum of large face terms that nearly cancel.
        /// </para>
        /// <para>
        /// What no quadrature can remove is the domain D itself, whose boundary is only as good as the 2d
        /// curves of the face, and a ProjectedCurve is an approximation. That is the floor.
        /// </para>
        /// </summary>
        public static double IntegratedVolume(Shell shell) => IntegratedVolume(shell, PrecisionFor(shell));

        /// <summary>
        /// <see cref="IntegratedVolume(Shell)"/> with the mesh precision given explicitly. The mesh only decides
        /// the orientation of each face and the fallback here, so the answer is not supposed to depend on it -
        /// which is what <c>VolumeTests.IntegratedVolumeMatchesAnalyticOnEveryMesh</c> uses this overload to check.
        /// </summary>
        public static double IntegratedVolume(Shell shell, double precision)
        {
            BoundingBox box = ExactExtentOf(shell);
            GeoPoint center = box.IsEmpty ? GeoPoint.Origin : box.GetCenter();
            double size = SizeOf(shell);
            double sum = 0.0;
            foreach (Face face in shell.Faces)
            {
                face.GetTriangulation(precision, out GeoPoint[] points, out GeoPoint2D[] uvPoints, out int[] indices, out _);
                if (indices == null) continue;
                sum += IntegratedFaceVolume(face, points, uvPoints, indices, precision, center, size);
            }
            return sum;
        }

        /// <summary>
        /// The number of faces that fell back to the tetrahedron sum since the process started. Diagnostics only:
        /// a test reads it before and after a measurement to see whether the boundary route held.
        /// </summary>
        public static int VolumeFallbackCount => volumeFallbackCount;
        private static int volumeFallbackCount;

        /// <summary>
        /// The contribution of one face: closed form for a plane, the boundary integral for everything else,
        /// and the tetrahedron sum as the fallback when the orientation or the boundary cannot be trusted.
        /// </summary>
        private static double IntegratedFaceVolume(Face face, GeoPoint[] points, GeoPoint2D[] uvPoints, int[] indices,
            double precision, GeoPoint center, double size)
        {
            ISurface surface = face.Surface;
            double orientation = surface == null ? 0.0 : OrientationOf(surface, points, uvPoints, indices);
            if (surface != null && orientation != 0.0)
            {
                SimpleShape shape;
                double domain;
                BoundingRect rect;
                try { shape = face.Area; domain = shape.Area; rect = shape.GetExtent(); }
                catch (Exception) { shape = null; domain = 0.0; rect = BoundingRect.EmptyBoundingRect; }
                if (shape != null && domain > 0.0)
                {
                    if (surface is PlaneSurface)
                    {   // On a plane the integrand is CONSTANT. S(u,v) = P + u*e1 + v*e2 and Su x Sv = e1 x e2, and
                        // both e1 and e2 are perpendicular to their own cross product, so the u and v terms drop out
                        // and only (P - c).(e1 x e2) is left. The integral is that times the area of the domain,
                        // which SimpleShape gives exactly - no quadrature, nothing left to converge.
                        double density = Evaluate(surface, rect.GetCenter(), (location, du, dv) => VolumeDensity(location, du, dv, center));
                        if (!double.IsNaN(density)) return orientation * density * domain;
                    }
                    else
                    {
                        double green = IntegrateOverDomain(surface, shape, rect,
                            (location, du, dv) => VolumeDensity(location, du, dv, center), size * size * size);
                        if (!double.IsNaN(green)) return orientation * green;
                    }
                }
            }
            System.Threading.Interlocked.Increment(ref volumeFallbackCount);
            return FallbackVolume(face, precision, center);
        }

        /// <summary><c>(S(u,v) - c).(Su x Sv) / 3</c>, the integrand of the volume.</summary>
        private static double VolumeDensity(GeoPoint location, GeoVector du, GeoVector dv, GeoPoint center)
        {
            GeoVector normal = du ^ dv;
            return ((location.x - center.x) * normal.x + (location.y - center.y) * normal.y
                  + (location.z - center.z) * normal.z) / 3.0;
        }

        /// <summary>
        /// The integral of <paramref name="integrand"/>, a function of S, Su and Sv, over the parameter domain of a
        /// face, as the boundary integral of <see cref="GreenFaceIntegral"/>. Volume and area differ only in the
        /// integrand. The derivatives come from <see cref="ISurface.DerivativeAt"/> when that passes
        /// <see cref="DerivativesAreConsistent"/>, otherwise from central differences of PointAt.
        /// </summary>
        /// <param name="scale">The magnitude the tolerance is relative to, size^3 for a volume, size^2 for an area</param>
        /// <returns>The integral, NaN when it cannot be computed reliably</returns>
        private static double IntegrateOverDomain(ISurface surface, SimpleShape shape, BoundingRect rect,
            Func<GeoPoint, GeoVector, GeoVector, double> integrand, double scale)
        {
            Func<GeoPoint2D, double> density;
            if (DerivativesAreConsistent(surface, rect)) density = uv => Evaluate(surface, uv, integrand);
            else
            {
                double hu = DifferenceStep * rect.Width, hv = DifferenceStep * rect.Height;
                density = uv => EvaluateDifferenced(surface, uv, hu, hv, integrand);
            }
            KnotLines(surface, rect, out double[] uKnots, out double[] vKnots);
            return GreenFaceIntegral(shape, rect, density, scale, uKnots, vKnots);
        }

        /// <summary>
        /// The integrand at a parameter point, with S, Su and Sv from DerivativeAt. At a pole one of the derivatives
        /// is the null vector and the value is a genuine 0. Where the surface cannot be evaluated at all the result
        /// is NaN, so that the face goes to the fallback instead of silently integrating a zero there.
        /// </summary>
        private static double Evaluate(ISurface surface, GeoPoint2D uv, Func<GeoPoint, GeoVector, GeoVector, double> integrand)
        {
            try
            {
                surface.DerivationAt(uv, out GeoPoint location, out GeoVector du, out GeoVector dv);
                double res = integrand(location, du, dv);
                return double.IsInfinity(res) ? double.NaN : res;
            }
            catch (Exception) { return double.NaN; }
        }

        /// <summary>
        /// <see cref="Evaluate"/> with the derivatives taken as central differences of <see cref="ISurface.PointAt"/>,
        /// for a surface that fails <see cref="DerivativesAreConsistent"/>. Four more points per evaluation, good to
        /// about 1e-9 - and the face stays on the boundary route instead of going to the triangles, which would not
        /// fit its exactly integrated neighbours: their common edge is a polyline on one side and the exact curve
        /// on the other, and for the volume of SurfaceOfRevolution1 that mix was 7e-4 off.
        /// </summary>
        private static double EvaluateDifferenced(ISurface surface, GeoPoint2D uv, double hu, double hv,
            Func<GeoPoint, GeoVector, GeoVector, double> integrand)
        {
            try
            {
                GeoPoint location = surface.PointAt(uv);
                GeoVector du = (1.0 / (2.0 * hu)) * (surface.PointAt(new GeoPoint2D(uv.x + hu, uv.y)) - surface.PointAt(new GeoPoint2D(uv.x - hu, uv.y)));
                GeoVector dv = (1.0 / (2.0 * hv)) * (surface.PointAt(new GeoPoint2D(uv.x, uv.y + hv)) - surface.PointAt(new GeoPoint2D(uv.x, uv.y - hv)));
                double res = integrand(location, du, dv);
                return double.IsInfinity(res) ? double.NaN : res;
            }
            catch (Exception) { return double.NaN; }
        }

        /// <summary>
        /// Whether <see cref="ISurface.DerivativeAt"/> really is the derivative of <see cref="ISurface.PointAt"/>,
        /// checked against central differences at 3 x 3 points of the domain rectangle.
        /// <para>
        /// The integrand is built from DerivativeAt, the tetrahedron sum only from points, so a surface that gets its
        /// derivatives wrong makes the integral wrong while the triangles stay right - and such a surface exists:
        /// SurfaceOfRevolution returns the v derivative with respect to the normalized position on its profile
        /// curve instead of the v parameter, 24 percent off in SurfaceOfRevolution1. Its mantle then contributed
        /// 15261 instead of 20060, and the old rectangle route was just as wrong, only differently (the recorded
        /// volume was 29633 where the triangles converge to 36696). A face that fails this check is integrated
        /// with <see cref="EvaluateDifferenced"/>.
        /// </para>
        /// </summary>
        private static bool DerivativesAreConsistent(ISurface surface, BoundingRect rect)
        {
            double hu = DifferenceStep * rect.Width, hv = DifferenceStep * rect.Height;
            if (!(hu > 0.0) || !(hv > 0.0)) return false;
            try
            {
                for (int i = 1; i <= 3; i++)
                {
                    for (int j = 1; j <= 3; j++)
                    {
                        GeoPoint2D uv = new GeoPoint2D(rect.Left + i * rect.Width / 4.0, rect.Bottom + j * rect.Height / 4.0);
                        surface.DerivationAt(uv, out GeoPoint _, out GeoVector du, out GeoVector dv);
                        GeoVector differenceU = (1.0 / (2.0 * hu)) * (surface.PointAt(new GeoPoint2D(uv.x + hu, uv.y)) - surface.PointAt(new GeoPoint2D(uv.x - hu, uv.y)));
                        GeoVector differenceV = (1.0 / (2.0 * hv)) * (surface.PointAt(new GeoPoint2D(uv.x, uv.y + hv)) - surface.PointAt(new GeoPoint2D(uv.x, uv.y - hv)));
                        // relative to the larger of the two, because one of them may vanish at a pole
                        double scale = Math.Max(differenceU.Length, differenceV.Length);
                        if (!(scale > 0.0)) continue;
                        if (!((du - differenceU).Length <= DerivativeConsistency * scale && (dv - differenceV).Length <= DerivativeConsistency * scale))
                            return false;
                    }
                }
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Step of the central differences, relative to the width and height of the domain.</summary>
        private const double DifferenceStep = 1e-5;
        /// <summary>
        /// How far DerivativeAt may differ from the central difference. The difference itself is good to about 1e-9
        /// with the step above, a wrong scaling is off by percent - this sits far from both.
        /// </summary>
        private const double DerivativeConsistency = 1e-5;

        /// <summary>
        /// Relative accuracy the boundary integral is driven to, relative to size^3 of the shell for a volume and
        /// size^2 for an area. Far below
        /// the 1e-4 the baselines are compared with, so the quadrature itself never shows in a diff. 1e-9 moved
        /// the values of the NURBS faces of DifferenceBug14 and 15 by 2e-8 and cost 16 percent more time.
        /// </summary>
        private const double GreenRelativeTolerance = 1e-8;

        /// <summary>
        /// How far the integrated derivative of a 2d segment may miss its chord, relative to the size of the uv
        /// domain, before <see cref="ICurve2D.DirectionAt"/> is taken not to be the derivative. What this has to
        /// catch is a direction that is normalized or in another parametrization, which misses by percent; the
        /// splines of a NurbsSurface face in DifferenceBug14 miss by 1e-7, harmless for the integral, and a
        /// limit that tight sent the whole face to the fallback.
        /// </summary>
        private const double DerivativeCheck = 1e-4;

        /// <summary>
        /// The integral of <paramref name="density"/> over the domain of a face, as an integral over its boundary.
        /// <para>
        /// With <c>F(u,v) = integral from u0 to u of f(t,v) dt</c>, so that <c>dF/du = f</c>, Green's theorem gives
        /// </para>
        /// <para><c>integral over D of f du dv = closed integral over the boundary of D of F dv</c></para>
        /// <para>
        /// with the boundary run counterclockwise. Both integrals are adaptive Gauss-Kronrod: the outer one along
        /// each 2d segment of the outline and of the holes, the inner one along the line of constant v from u0 to
        /// the point on the boundary. That line may leave D, but it stays inside the bounding rectangle of the
        /// domain, where the surface is defined. A segment of constant v - a pole line among them - has dv = 0
        /// and costs no inner integral at all.
        /// </para>
        /// <para>
        /// The orientation of each border is taken from its own signed area, which comes out of the same nodes
        /// (f = 1, so F = u - u0), rather than trusted from the storage convention. And the derivative of each
        /// segment is checked against its chord: the substitution is only right if DirectionAt is the derivative
        /// with respect to the normalized position, and a curve for which it is not makes the face return NaN.
        /// </para>
        /// </summary>
        /// <param name="scale">The magnitude the tolerance is relative to</param>
        /// <param name="uKnots">Values of u inside the domain where the surface is less smooth, see <see cref="KnotLines"/></param>
        /// <param name="vKnots">The same for v</param>
        /// <returns>The integral, NaN when it cannot be computed reliably</returns>
        private static double GreenFaceIntegral(SimpleShape shape, BoundingRect rect, Func<GeoPoint2D, double> density, double scale,
            double[] uKnots, double[] vKnots)
        {
            double u0 = (rect.Left + rect.Right) / 2.0; // the middle halves the length of the inner integrals
            double outerTolerance = GreenRelativeTolerance * scale;
            // An error in F is multiplied by the total variation of v along the boundary, about twice the height of
            // the domain for a simple outline. The factor 0.01 keeps that inner noise well below what the outer
            // error estimate reacts to, otherwise the outer refinement would chase it up to the panel limit.
            double innerTolerance = 0.01 * outerTolerance / Math.Max(rect.Height, 1e-12);
            double uvScale = rect.Width + rect.Height;
            Func<double, double, double> antiderivative = (u, v) =>
                GaussKronrod(t => density(new GeoPoint2D(t, v)), u0, u, innerTolerance, uKnots);

            double outline = BorderIntegral(shape.Outline, antiderivative, u0, outerTolerance, uvScale, uKnots, vKnots, out double outlineArea);
            if (double.IsNaN(outline) || outlineArea == 0.0) return double.NaN;
            double result = Math.Sign(outlineArea) * outline;
            foreach (Border hole in shape.Holes)
            {
                double inHole = BorderIntegral(hole, antiderivative, u0, outerTolerance, uvScale, uKnots, vKnots, out double holeArea);
                if (double.IsNaN(inHole)) return double.NaN;
                // A degenerate hole - UniteBug1 has one that is a single line of length 0 - encloses nothing and
                // contributes nothing. Unlike the outline, it is no reason to give up on the face.
                if (holeArea == 0.0) continue;
                result -= Math.Sign(holeArea) * inHole;
            }
            return result;
        }

        /// <summary>
        /// <c>closed integral of F dv</c> along one border in its stored direction, and its signed area
        /// <c>closed integral of (u - u0) dv</c> in <paramref name="signedArea"/>. NaN when a segment fails the
        /// derivative check or the integrand cannot be evaluated.
        /// </summary>
        private static double BorderIntegral(Border border, Func<double, double, double> antiderivative, double u0,
            double tolerance, double uvScale, double[] uKnots, double[] vKnots, out double signedArea)
        {
            double sum = 0.0;
            signedArea = 0.0;
            double derivativeTolerance = DerivativeCheck * uvScale;
            for (int i = 0; i < border.Count; i++)
            {
                ICurve2D segment = border[i];
                // The DirectionAt of a projected curve (the plain one and the one of an intersection curve) comes from the
                // 3d curve, normalized and in another parametrization - not the derivative of PointAt. A spline through its
                // points has both.
                if (segment is ProjectedCurve || segment is InterpolatedDualSurfaceCurve.ProjectedCurve)
                {
                    BSpline2D approx = BSpline2D.Approximate(segment.PointAt, 1e-7 * Math.Max(uvScale, 1e-12));
                    if (approx != null) segment = approx;
                }
                Vector4 part = GaussKronrod(t =>
                {
                    GeoPoint2D p = segment.PointAt(t);
                    GeoVector2D d = segment.DirectionAt(t);
                    double flux = d.y == 0.0 ? 0.0 : antiderivative(p.x, p.y) * d.y;
                    return new Vector4(flux, (p.x - u0) * d.y, d.x, d.y);
                }, 0.0, 1.0, tolerance, 0.1 * derivativeTolerance, KnotCrossings(segment, uKnots, vKnots));
                if (double.IsNaN(part.A) || double.IsNaN(part.B)) return double.NaN;
                // against the ends of what was integrated: the end points a curve reports may be snapped to its
                // vertex, a ProjectedCurve does that, and differ from PointAt(1.0) by the approximation error
                GeoVector2D chord = segment.PointAt(1.0) - segment.PointAt(0.0);
                if (!(Math.Abs(part.C - chord.x) <= derivativeTolerance && Math.Abs(part.D - chord.y) <= derivativeTolerance))
                    return double.NaN;
                sum += part.A;
                signedArea += part.B;
            }
            return sum;
        }

        /// <summary>
        /// The knot lines of a spline surface inside the domain rectangle, as sorted distinct values of u and of v.
        /// Empty for every other surface: the analytic ones are smooth, and a surface that is not smooth somewhere
        /// else is still integrated correctly, only by bisection.
        /// </summary>
        private static void KnotLines(ISurface surface, BoundingRect rect, out double[] uKnots, out double[] vKnots)
        {
            uKnots = vKnots = Array.Empty<double>();
            if (surface is NurbsSurface nurbs)
            {
                uKnots = nurbs.UKnots.Where(u => u > rect.Left && u < rect.Right).Distinct().OrderBy(u => u).ToArray();
                vKnots = nurbs.VKnots.Where(v => v > rect.Bottom && v < rect.Top).Distinct().OrderBy(v => v).ToArray();
            }
        }

        /// <summary>
        /// The positions where a border segment crosses a knot line, sorted. Along such a line the outer integrand
        /// is less smooth for the same reason the inner one is. A straight segment is solved directly; a curved one
        /// is sampled at <see cref="CrossingSamples"/> positions and every change of side is bisected down. A
        /// crossing between two samples that leaves and re-enters within that step is missed, and costs nothing
        /// but a little more bisection in the quadrature - this is about speed, not about correctness.
        /// <para>
        /// Worth it: the trimmed NURBS faces of DifferenceBug14 and 15, bounded by ProjectedCurves, took 1.4 to
        /// 1.7 seconds each while only the straight segments were split.
        /// </para>
        /// </summary>
        private static double[] KnotCrossings(ICurve2D segment, double[] uKnots, double[] vKnots)
        {
            if (uKnots.Length == 0 && vKnots.Length == 0) return Array.Empty<double>();
            List<double> crossings = new List<double>();
            if (segment is Line2D)
            {
                GeoPoint2D start = segment.StartPoint, end = segment.EndPoint;
                if (end.x != start.x) foreach (double u in uKnots) crossings.Add((u - start.x) / (end.x - start.x));
                if (end.y != start.y) foreach (double v in vKnots) crossings.Add((v - start.y) / (end.y - start.y));
            }
            else
            {
                GeoPoint2D[] samples = new GeoPoint2D[CrossingSamples + 1];
                for (int i = 0; i <= CrossingSamples; i++) samples[i] = segment.PointAt((double)i / CrossingSamples);
                foreach (double u in uKnots) AddCrossings(segment, samples, p => p.x - u, crossings);
                foreach (double v in vKnots) AddCrossings(segment, samples, p => p.y - v, crossings);
            }
            return crossings.Where(t => t > 0.0 && t < 1.0).Distinct().OrderBy(t => t).ToArray();
        }

        /// <summary>How finely a curved border segment is sampled to find where it crosses a knot line.</summary>
        private const int CrossingSamples = 64;

        /// <summary>Every change of sign of <paramref name="side"/> between two samples, bisected to about 1e-12.</summary>
        private static void AddCrossings(ICurve2D segment, GeoPoint2D[] samples, Func<GeoPoint2D, double> side, List<double> crossings)
        {
            int n = samples.Length - 1;
            for (int i = 0; i < n; i++)
            {
                double s0 = side(samples[i]), s1 = side(samples[i + 1]);
                if (s0 == 0.0 || s0 * s1 >= 0.0) continue;
                double low = (double)i / n, high = (double)(i + 1) / n;
                for (int k = 0; k < 40; k++)
                {
                    double mid = (low + high) / 2.0;
                    if (side(segment.PointAt(mid)) * s0 > 0.0) low = mid;
                    else high = mid;
                }
                crossings.Add((low + high) / 2.0);
            }
        }

        /// <summary>Four values integrated together along a border segment.</summary>
        private readonly struct Vector4
        {
            public readonly double A, B, C, D;
            public Vector4(double a, double b, double c, double d) { A = a; B = b; C = c; D = d; }
            public static Vector4 operator +(Vector4 x, Vector4 y) => new Vector4(x.A + y.A, x.B + y.B, x.C + y.C, x.D + y.D);
            public static Vector4 operator -(Vector4 x, Vector4 y) => new Vector4(x.A - y.A, x.B - y.B, x.C - y.C, x.D - y.D);
            public static Vector4 operator *(double s, Vector4 x) => new Vector4(s * x.A, s * x.B, s * x.C, s * x.D);
            public Vector4 Abs() => new Vector4(Math.Abs(A), Math.Abs(B), Math.Abs(C), Math.Abs(D));
            public bool IsNaN => double.IsNaN(A) || double.IsNaN(B) || double.IsNaN(C) || double.IsNaN(D);
        }

        /// <summary>One interval of the adaptive quadrature with its value and error estimate.</summary>
        private readonly struct Panel
        {
            public readonly double From, To;
            public readonly Vector4 Value, Error;
            public Panel(double from, double to, Vector4 value, Vector4 error) { From = from; To = to; Value = value; Error = error; }
        }

        /// <summary>
        /// Most intervals one adaptive integral is split into. Reached only where the integrand is not smooth or
        /// noisier than the tolerance; the result is then the best those intervals give, instead of a quadrature
        /// that refines without end.
        /// </summary>
        private const int MaxQuadraturePanels = 50;

        // The 15 point Kronrod rule and the 7 point Gauss rule embedded in it, on [-1, 1] (QUADPACK qk15).
        // The Kronrod nodes 1, 3 and 5 and the center are the Gauss nodes.
        private static readonly double[] kronrodNodes =
        {
            0.991455371120812639206854697526329, 0.949107912342758524526189684047851,
            0.864864423359769072789712788640926, 0.741531185599394439863864773280788,
            0.586087235467691130294144845693013, 0.405845151377397166906606412076961,
            0.207784955007898467600689403773245, 0.0,
        };
        private static readonly double[] kronrodWeights =
        {
            0.022935322010529224963732008058970, 0.063092092629978553290700663189204,
            0.104790010322250183839876322541518, 0.140653259715525918745189590510238,
            0.169004726639267902826583426598550, 0.190350578064785409913256402421014,
            0.204432940075298892414161999234649, 0.209482141084727828012999174891714,
        };
        private static readonly double[] gaussWeights =
        {
            0.129484966168869693270611432679082, 0.279705391489276667901467771423780,
            0.381830050505118944950369775488975, 0.417959183673469387755102040816327,
        };

        /// <summary>The scalar case of <see cref="GaussKronrod(Func{double, Vector4}, double, double, double, double, double[])"/>.</summary>
        private static double GaussKronrod(Func<double, double> f, double a, double b, double tolerance, double[] breaks)
            => GaussKronrod(t => new Vector4(f(t), 0.0, 0.0, 0.0), a, b, tolerance, double.PositiveInfinity, breaks).A;

        /// <summary>
        /// Globally adaptive Gauss-Kronrod integral of f from a to b (b may be less than a), four values at once:
        /// the interval with the largest error is split until the summed error of the first value is within
        /// <paramref name="tolerance"/> and that of the last two - the derivative of a border segment, which the
        /// chord check compares - within <paramref name="derivativeTolerance"/>. The second value, a signed area,
        /// is only ever used for its sign and does not drive the refinement.
        /// <para>
        /// The error of an interval is QUADPACK's estimate, not the plain difference between the Kronrod and the
        /// embedded Gauss value. That difference is the error of the 7 point rule, and the 15 point value that
        /// is actually returned is orders of magnitude better - taken at face value it made the quadrature refine
        /// smooth integrands many times over, which made the regression tests several times slower. The estimate
        /// also carries QUADPACK's floor of 50 machine epsilons of the integral of |f|, so rounding noise in the
        /// integrand cannot force a split either.
        /// </para>
        /// <para>
        /// The values of the intervals are summed in the order of the intervals, so the result does not depend on
        /// the order the splits happened in. NaN as soon as the integrand is NaN anywhere.
        /// </para>
        /// <para>
        /// <paramref name="breaks"/> are the points where the integrand is known to be less smooth, the knots of
        /// a spline surface. The quadrature starts with the intervals between those that lie between a and b,
        /// instead of discovering them by bisection: a cubic NurbsSurface is only C1 or C2 across a knot line, and
        /// without the knots the bisection ran into the panel limit on every inner integral - 8.6 seconds for one
        /// untrimmed NURBS face of DifferenceBug14, which is a polynomial within each knot span and integrated
        /// exactly by one 15 point rule.
        /// </para>
        /// </summary>
        private static Vector4 GaussKronrod(Func<double, Vector4> f, double a, double b, double tolerance, double derivativeTolerance,
            double[] breaks)
        {
            List<Panel> panels = new List<Panel>();
            double from = a;
            if (breaks.Length > 0)
            {
                double low = Math.Min(a, b), high = Math.Max(a, b), margin = 1e-12 * (high - low);
                IEnumerable<double> inside = breaks.Where(x => x > low + margin && x < high - margin);
                foreach (double x in b > a ? inside : inside.Reverse())
                {
                    panels.Add(KronrodPanel(f, from, x));
                    from = x;
                }
            }
            panels.Add(KronrodPanel(f, from, b));
            if (panels.Count == 1 && (panels[0].Value.IsNaN || IsConverged(panels[0].Error, tolerance, derivativeTolerance))) return panels[0].Value;
            int maxPanels = MaxQuadraturePanels + panels.Count;
            while (true)
            {
                Vector4 value = new Vector4(), error = new Vector4();
                foreach (Panel panel in panels) { value += panel.Value; error += panel.Error; }
                if (value.IsNaN || IsConverged(error, tolerance, derivativeTolerance) || panels.Count >= maxPanels)
                    return value;
                int worst = 0;
                double worstWeight = -1.0;
                for (int i = 0; i < panels.Count; i++)
                {
                    double weight = Math.Max(panels[i].Error.A / tolerance, (panels[i].Error.C + panels[i].Error.D) / derivativeTolerance);
                    if (weight > worstWeight) { worstWeight = weight; worst = i; }
                }
                Panel split = panels[worst];
                double mid = (split.From + split.To) / 2.0;
                panels[worst] = KronrodPanel(f, split.From, mid);
                panels.Insert(worst + 1, KronrodPanel(f, mid, split.To));
            }
        }

        private static bool IsConverged(Vector4 error, double tolerance, double derivativeTolerance)
            => error.A <= tolerance && error.C <= derivativeTolerance && error.D <= derivativeTolerance;

        /// <summary>
        /// The 15 point Kronrod value over one interval and the QUADPACK error estimate of each component,
        /// <c>resasc * min(1, (200 |K - G| / resasc)^1.5)</c>, at least 50 machine epsilons of the integral of |f|.
        /// </summary>
        private static Panel KronrodPanel(Func<double, Vector4> f, double a, double b)
        {
            double half = (b - a) / 2.0, mid = (a + b) / 2.0;
            Span<Vector4> values = stackalloc Vector4[15];
            values[14] = f(mid);
            Vector4 kronrod = kronrodWeights[7] * values[14], gauss = gaussWeights[3] * values[14];
            Vector4 absolute = kronrodWeights[7] * values[14].Abs();
            for (int k = 0; k < 7; k++)
            {
                double x = half * kronrodNodes[k];
                values[2 * k] = f(mid - x);
                values[2 * k + 1] = f(mid + x);
                Vector4 pair = values[2 * k] + values[2 * k + 1];
                kronrod += kronrodWeights[k] * pair;
                absolute += kronrodWeights[k] * (values[2 * k].Abs() + values[2 * k + 1].Abs());
                if ((k & 1) == 1) gauss += gaussWeights[k >> 1] * pair;
            }
            Vector4 mean = 0.5 * kronrod;
            Vector4 spread = kronrodWeights[7] * (values[14] - mean).Abs();
            for (int k = 0; k < 7; k++)
                spread += kronrodWeights[k] * ((values[2 * k] - mean).Abs() + (values[2 * k + 1] - mean).Abs());
            double length = Math.Abs(half);
            Vector4 difference = (half * (kronrod - gauss)).Abs();
            Vector4 error = new Vector4(
                QuadpackError(difference.A, length * spread.A, length * absolute.A),
                QuadpackError(difference.B, length * spread.B, length * absolute.B),
                QuadpackError(difference.C, length * spread.C, length * absolute.C),
                QuadpackError(difference.D, length * spread.D, length * absolute.D));
            return new Panel(a, b, half * kronrod, error);
        }

        private const double MachineEpsilon = 2.220446049250313e-16;

        private static double QuadpackError(double difference, double resasc, double resabs)
        {
            double error = difference;
            if (resasc != 0.0 && error != 0.0) error = resasc * Math.Min(1.0, Math.Pow(200.0 * error / resasc, 1.5));
            return Math.Max(error, 50.0 * MachineEpsilon * resabs);
        }

        /// <summary>
        /// Whether <c>Su x Sv</c> points the way the triangulation winds this face (+1) or the other way (-1),
        /// 0 when it cannot be decided.
        /// <para>
        /// The face itself will not say: <c>orientedOutward</c> is private and the surface of a face may be
        /// stored reversed. The triangulation is the right reference anyway, because it is what
        /// <see cref="Shell.SignedVolume"/> reads the orientation from - so both agree on the sign of every
        /// face by construction, and a shell that comes out negative there comes out negative here.
        /// </para>
        /// <para>
        /// Decided by majority over a sample of triangles rather than by the first one: a single triangle can
        /// be degenerate or sit almost edge on to the surface normal, and one wrong face would put the whole
        /// volume out by twice its contribution.
        /// </para>
        /// </summary>
        private static double OrientationOf(ISurface surface, GeoPoint[] points, GeoPoint2D[] uvPoints, int[] indices)
        {
            if (indices == null || uvPoints == null || points == null || uvPoints.Length != points.Length) return 0.0;
            int agree = 0, disagree = 0;
            int step = Math.Max(3, indices.Length / (3 * 16) * 3); // at most about 16 samples, always whole triangles
            for (int i = 0; i < indices.Length; i += step)
            {
                GeoVector fromMesh = (points[indices[i + 1]] - points[indices[i]])
                                   ^ (points[indices[i + 2]] - points[indices[i]]);
                if (Precision.IsNullVector(fromMesh)) continue;
                GeoPoint2D uv = new GeoPoint2D(uvPoints[indices[i]], uvPoints[indices[i + 1]], uvPoints[indices[i + 2]]);
                GeoVector fromSurface;
                try
                {
                    surface.DerivationAt(uv, out GeoPoint _, out GeoVector du, out GeoVector dv);
                    fromSurface = du ^ dv;
                }
                catch (Exception) { continue; }
                if (Precision.IsNullVector(fromSurface)) continue;
                double dot = fromMesh.Normalized * fromSurface.Normalized;
                if (dot > 0.1) ++agree;
                else if (dot < -0.1) ++disagree;
            }
            if (agree == 0 && disagree == 0) return 0.0;
            return agree >= disagree ? 1.0 : -1.0;
        }

        /// <summary>
        /// What one face contributes when its parameter domain cannot be used as the region to integrate over:
        /// exactly what <see cref="Shell.SignedVolume"/> would have made of it, tetrahedra over the flat
        /// triangles plus the 3/4 sag correction.
        /// <para>
        /// Going through SignedVolume rather than summing the tetrahedra here is the point. The sag correction
        /// is what makes that sum usable on a curved face, so dropping it would leave the fallback WORSE than
        /// the method this replaces - and a fallback that loses against the thing it falls back from is not a
        /// fallback. This way the value can only ever improve on <see cref="Shell.SignedVolume"/>, never
        /// regress below it.
        /// </para>
        /// <para>
        /// The tetrahedra are spanned from the same point <paramref name="center"/> the boundary integral of the
        /// other faces refers to. One face contributes a different amount for a different reference point, only
        /// the sum over a closed shell is independent of it - so the references must not be mixed.
        /// </para>
        /// </summary>
        private static double FallbackVolume(Face face, double precision, GeoPoint center)
        {
            try { return Shell.SignedVolume(new[] { face }, precision, center); }
            catch (Exception) { return 0.0; }
        }

        public static double TotalEdgeLength(Shell shell)
        {
            double sum = 0.0;
            foreach (Edge edge in shell.Edges) if (edge.Curve3D != null) sum += edge.Curve3D.Length;
            return sum;
        }

        /// <summary>
        /// The relative difference below which two volumes or areas count as equal for <see cref="SortCanonically"/>.
        /// Far above what the integration leaves (about 1e-9), far below any real difference between two parts.
        /// </summary>
        private const double SortTolerance = 1e-6;

    }
}
