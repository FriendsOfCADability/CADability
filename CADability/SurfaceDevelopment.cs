using CADability.Curve2D;
using CADability.Shapes;
using System;
using System.Collections.Generic;

namespace CADability.GeoObject
{
    /// <summary>
    /// Implemented by surfaces that can be developed (unrolled) into the plane without
    /// distortion. Test for it the way you would test for <see cref="ICylinder"/> or
    /// <see cref="ISurfaceWithRadius"/>.
    /// <para>
    /// Development is an isometry: it maps the (u,v) parameter domain onto a flat pattern
    /// so that every curve keeps its length. That is what makes the result usable for
    /// fabrication - rolling a sheet, marking an intersection on a tube, or driving a
    /// machine that reaches the surface with a rotary axis.
    /// </para>
    /// <para>
    /// Only singly curved surfaces qualify. Spheres, tori and general NURBS report
    /// <see cref="IsDevelopable"/> false: they cannot be flattened without stretching,
    /// and returning an approximation would be worse than refusing.
    /// </para>
    /// </summary>
    public interface IDevelopableSurface
    {
        /// <summary>
        /// True if this surface can be developed into the plane without distortion. A
        /// surface of the right kind may still say false - an elliptical cylinder or a
        /// sheared parametrisation, for instance - so it must be tested rather than
        /// inferred from the type.
        /// </summary>
        bool IsDevelopable { get; }

        /// <summary>
        /// Maps a (u,v) parameter pair to its position on the flat pattern. Only
        /// meaningful when <see cref="IsDevelopable"/> is true.
        /// </summary>
        GeoPoint2D Develop(GeoPoint2D uv);

        /// <summary>
        /// Inverse of <see cref="Develop"/>: a flat pattern position back to (u,v).
        /// </summary>
        GeoPoint2D DevelopInverse(GeoPoint2D flat);
    }

    /// <summary>
    /// Develops curves and faces on <see cref="IDevelopableSurface">developable surfaces</see>
    /// into their flat patterns.
    /// </summary>
    public static class SurfaceDevelopment
    {
        /// <summary>
        /// How far a sampled curve may be subdivided. Twelve levels is 4096 segments,
        /// which is far past what any real precision asks for; it stops a curve that
        /// never converges - a degenerate parametrisation, say - from running away.
        /// </summary>
        private const int MaxSubdivision = 12;

        /// <summary>
        /// Develops a curve lying on the surface into the flat pattern, approximating to
        /// the given precision. Returns null if the surface cannot be developed.
        /// </summary>
        /// <param name="surface">The surface the curve lies on</param>
        /// <param name="curve">The 3d curve</param>
        /// <param name="precision">Maximum deviation of the approximation, in the units of the flat pattern</param>
        public static ICurve2D DevelopCurve(ISurface surface, ICurve curve, double precision)
        {
            if (surface == null || curve == null)
                return null;

            if (!(surface is IDevelopableSurface developable) || !developable.IsDevelopable)
                return null;

            ICurve2D uvCurve = surface.GetProjectedCurve(curve, precision);
            if (uvCurve == null)
                return null;

            return DevelopCurve(developable, uvCurve, precision);
        }

        /// <summary>
        /// Develops a curve already expressed in the surface's (u,v) parameters.
        /// </summary>
        /// <param name="surface">The surface, which must be developable</param>
        /// <param name="uvCurve">The curve in parameter space</param>
        /// <param name="precision">Maximum deviation of the approximation</param>
        /// <remarks>
        /// The seam needs no special handling. <see cref="ISurface.GetProjectedCurve"/>
        /// returns a continuous parameter curve and lets u run outside one period where
        /// the curve does - an arc from -0.3 to +0.3 rad comes back as 5.98 to 6.58, not
        /// as two pieces either side of a jump. Folding those values back into one period
        /// would be the thing that broke the flat curve, not the thing that fixed it.
        /// </remarks>
        public static ICurve2D DevelopCurve(IDevelopableSurface surface, ICurve2D uvCurve,
                                            double precision)
        {
            if (surface == null || uvCurve == null || !surface.IsDevelopable)
                return null;

            if (precision <= 0.0)
                precision = Precision.eps;

            List<double> positions = new List<double> { 0.0, 1.0 };
            Subdivide(surface, uvCurve, precision, 0.0, 1.0, 0, positions);
            positions.Sort();

            GeoPoint2D[] flat = new GeoPoint2D[positions.Count];
            for (int i = 0; i < positions.Count; i++)
                flat[i] = surface.Develop(uvCurve.PointAt(positions[i]));

            if (flat.Length == 2)
                return new Line2D(flat[0], flat[1]);

            return new Polyline2D(flat);
        }

        /// <summary>
        /// Develops a face into its flat pattern: outline plus inner contours. Returns
        /// null if the face's surface cannot be developed.
        /// </summary>
        /// <remarks>
        /// The seam is not a parameter here, and deliberately so. A CADability face
        /// already carries an explicit outline in (u,v), so whatever produced the face has
        /// placed the seam; moving it means splitting that outline against a new u value
        /// and closing it again, which is a two-dimensional boolean operation rather than
        /// a development. A caller who needs the pattern cut elsewhere should rotate the
        /// surface about its axis before developing.
        /// </remarks>
        public static CompoundShape DevelopFace(Face face, double precision)
        {
            if (face?.Surface == null)
                return null;

            if (!(face.Surface is IDevelopableSurface developable) || !developable.IsDevelopable)
                return null;

            SimpleShape area = face.Area;
            if (area == null)
                return null;

            Border outline = DevelopBorder(developable, area.Outline, precision);
            if (outline == null)
                return null;

            List<Border> holes = new List<Border>();
            if (area.Holes != null)
            {
                foreach (Border hole in area.Holes)
                {
                    Border developed = DevelopBorder(developable, hole, precision);
                    if (developed != null)
                        holes.Add(developed);
                }
            }

            return new CompoundShape(new SimpleShape(outline, holes.ToArray()));
        }

        private static Border DevelopBorder(IDevelopableSurface surface, Border border,
                                            double precision)
        {
            if (border?.Segments == null || border.Segments.Length == 0)
                return null;

            List<ICurve2D> developed = new List<ICurve2D>();
            foreach (ICurve2D segment in border.Segments)
            {
                ICurve2D flat = DevelopCurve(surface, segment, precision);
                if (flat != null)
                    developed.Add(flat);
            }

            if (developed.Count == 0)
                return null;

            return new Border(developed.ToArray(), true);
        }

        /// <summary>
        /// Splits [from,to] until the straight chord between the developed endpoints stays
        /// within <paramref name="precision"/> of the developed midpoint. The test is on
        /// the flat pattern rather than in parameter space, because that is where the
        /// tolerance is meant: the flat image of a straight line on a cylinder is not a
        /// straight line, and how far it bends has nothing to do with how long the
        /// parameter interval is.
        /// </summary>
        private static void Subdivide(IDevelopableSurface surface, ICurve2D uvCurve,
                                      double precision, double from, double to, int depth,
                                      List<double> positions)
        {
            if (depth >= MaxSubdivision)
                return;

            double middle = (from + to) / 2.0;

            GeoPoint2D a = surface.Develop(uvCurve.PointAt(from));
            GeoPoint2D b = surface.Develop(uvCurve.PointAt(to));
            GeoPoint2D m = surface.Develop(uvCurve.PointAt(middle));

            if ((m | new GeoPoint2D(a, b)) <= precision)
                return;

            positions.Add(middle);
            Subdivide(surface, uvCurve, precision, from, middle, depth + 1, positions);
            Subdivide(surface, uvCurve, precision, middle, to, depth + 1, positions);
        }

    }
}
