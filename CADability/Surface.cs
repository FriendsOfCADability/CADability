using CADability.Attribute;
using CADability.Curve2D;
using MathNet.Numerics.Optimization;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using Wintellect.PowerCollections;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System.Linq;
#if WEBASSEMBLY
using CADability.WebDrawing;
#else
using System.Drawing;
#endif

namespace CADability.GeoObject
{
    /* Die Flächen in OpenCascade sind folgende:
     * Geom_CylindricalSurface, Geom_BezierSurface, Geom_BSplineSurface (=NURBS), Geom_ConicalSurface, 
     * Geom_CylindricalSurface, Geom_OffsetSurface, Geom_SphericalSurface, Geom_SurfaceOfLinearExtrusion,
     * Geom_SurfaceOfRevolution, Geom_ToroidalSurface.
     * 
     * Man kann aus allen Flächen NURBS machen, insofern müssen wir nicht alle implementieren, aber das ist
     * halt nur die Annäherung.
     * 
     * Man muss für alle Flächen die sie bestimmenden Daten finden, meist sieht man das im Konstruktor.
     * Eine Achse (Ax3) ist durch location, directionx und direction y bestimmt. Geom_SurfaceOfLinearExtrusion und 
     * Geom_SurfaceOfRevolution haben zusätzlich eine Kurve als Datum, (BasisCurve), die man mit CndHlp3D
     * static Edge FromCurve(Geom.Curve curve) in ein Edge und von dortaus mit IGeoObjectImpl.FromHlp3DEdge
     * in ein IGeoObject und somit in ein ICurve umwandeln kann. Die einzigen Daten, die ISurface abgeleitete Objekte
     * haben sind double, GeoPoint, GeoVector, ICurve. Das ist alles serialisierbar.
     */

    public enum RuledSurfaceMode { notRuled, ruledInU, ruledInV, planar, local }
    /// <summary>
    /// The ISurface interface must be implemented by all 3-dimensional unbound surfaces that are used by the
    /// <see cref="Face"/> object. The surface has a well defined 2-dimensional coordinate system, usually referred to
    /// as the u/v system.
    /// </summary>

    public interface ISurface
    {
        /// <summary>
        /// Returns a clone of this surface modified by the given ModOp.
        /// </summary>
        /// <param name="m">how to modify</param>
        ISurface GetModified(ModOp m);
        /// <summary>
        /// Returns a 3-dimensional curve from the given 2-dimensional curve. the 2-dimensional curve
        /// is interpreted in the u/v system of the surface.
        /// </summary>
        /// <param name="curve2d">the base curve</param>
        /// <returns>corresponding 3-d curve</returns>
        ICurve Make3dCurve(ICurve2D curve2d);
        /// <summary>
        /// Returns the normal vector (perpendicular to the surface) at the given u/v point
        /// </summary>
        /// <param name="uv">position of normal</param>
        /// <returns>normal vector</returns>
        GeoVector GetNormal(GeoPoint2D uv);
        /// <summary>
        /// Returns the direction at the given u/v point in direction of the u-axis
        /// </summary>
        /// <param name="uv">position</param>
        /// <returns>direction</returns>
        GeoVector UDirection(GeoPoint2D uv);
        /// <summary>
        /// Returns the direction at the given u/v point in direction of the v-axis
        /// </summary>
        /// <param name="uv">position</param>
        /// <returns>direction</returns>
        GeoVector VDirection(GeoPoint2D uv);
        /// <summary>
        /// Returns the 3-dimensional point at the given u/v point
        /// </summary>
        /// <param name="uv">position</param>
        /// <returns>point</returns>
        GeoPoint PointAt(GeoPoint2D uv);
        /// <summary>
        /// Returns the u/v position of the given point. It is assumed that the point is on the surface,
        /// if not the result is undetermined.
        /// </summary>
        /// <param name="p">point</param>
        /// <returns>position</returns>
        GeoPoint2D PositionOf(GeoPoint p);
        /// <summary>
        /// Returns the point and the two derivations of the suface in a single call. It returns the same result as calling
        /// <see cref="PointAt"/>, <see cref="VDirection"/> und <see cref="VDirection"/> succesively but is often faster
        /// than the three seperate calls.
        /// </summary>
        /// <param name="uv">Point in the parameter space</param>
        /// <param name="location">Resulting 3D point</param>
        /// <param name="du">Resulting derivation in u</param>
        /// <param name="dv">Resulting derivation in v</param>
        void DerivationAt(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv);
        /// <summary>
        /// Returns the point, the two first derivations and the three second derivations of the surface at the provided parameter position.
        /// 
        /// </summary>
        /// <param name="uv">Point in the parameter space</param>
        /// <param name="location">Resulting 3D point</param>
        /// <param name="du">Resulting derivation in u</param>
        /// <param name="dv">Resulting derivation in v</param>
        /// <param name="duu"></param>
        /// <param name="dvv"></param>
        /// <param name="duv"></param>
        void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv);
        /// <summary>
        /// Returns the intersection curve(s) of this surface with the given plane. An empty array is returned if there is no intersection.
        /// umin, umax, vmin, vmax define the Parameterspace of this surface (not of the PlaneSurface) for the intersection. It is also the periodic domain
        /// in which the 2d curve for this surface will be returned, if this surface is periodic. the resulting curves may exceed the area provided by umin, umax, vmin, vmax.
        /// </summary>
        /// <param name="pl">plane to intersect with</param>
        /// <returns>intersection curves</returns>
        IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision);
        /// <summary>
        /// Returns curves where direction is perpendicular to the normal vector
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        ICurve2D[] GetTangentCurves(GeoVector direction, double umin, double umax, double vmin, double vmax);
        /// <summary>
        /// Gets spans of the parameterspace that are guaranteed to contain only one inflection point.
        /// The returned intu should contain umin as first and umax as last value (same with v)
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="intu"></param>
        /// <param name="intv"></param>
        void GetSafeParameterSteps(double umin, double umax, double vmin, double vmax, out double[] intu, out double[] intv);
        /// <summary>
        /// Returns true if the given projection makes the surface disappear, i.e. degenerate to an edge.
        /// </summary>
        /// <param name="p">the projection</param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns>true if vanishing, falso otherwise</returns>
        bool IsVanishingProjection(Projection p, double umin, double umax, double vmin, double vmax);
        /// <summary>
        /// Returns the intersectionpoints of this surface with the line given by the parameters.
        /// Teh returned point are in the parametric (u/v) space of this surface.
        /// </summary>
        /// <param name="startPoint">startpoint of the line</param>
        /// <param name="direction">direction of the line</param>
        /// <returns></returns>
        GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction);
        /// <summary>
        /// Returns true, if this surface is periodic in the u direction (e.g. a cylinder)
        /// false otherwise.
        /// </summary>
        bool IsUPeriodic { get; }
        /// <summary>
        /// Returns true, if this surface is periodic in the v direction (e.g. a torus)
        /// false otherwise.
        /// </summary>
        bool IsVPeriodic { get; }
        /// <summary>
        /// Returns the u priod of this surface if it is <see cref="IsUPeriodic">u periodic</see>
        /// 0.0 otherwise
        /// </summary>
        double UPeriod { get; }
        /// <summary>
        /// Returns the v priod of this surface if it is <see cref="IsVPeriodic">v periodic</see>
        /// 0.0 otherwise
        /// </summary>
        double VPeriod { get; }
        /// <summary>
        /// returns the values for the u parameter where this surface is singular i.e. changing
        /// v with this u parameter fixed doesn't change the 3D point.
        /// </summary>
        /// <returns>list of u singularities</returns>
        double[] GetUSingularities();

        /// <summary>
        /// returns the values for the v parameter where this surface is singular i.e. changing
        /// u with this v parameter fixed doesn't change the 3D point.
        /// </summary>
        /// <returns>list of v singularities</returns>
        double[] GetVSingularities();
        /// <summary>
        /// Makes a <see cref="Face"/> from this surface with the given bounds in the parametric (u/v) space.
        /// </summary>
        /// <param name="simpleShape">the bounds</param>
        /// <returns>the created face or null</returns>
        Face MakeFace(CADability.Shapes.SimpleShape simpleShape);
        /// <summary>
        /// Gets the minimum and maximumm valus for the z-coordinate of a rectangular patch (in parametric space) 
        /// of this surface under a certain projection
        /// </summary>
        /// <param name="p">the projection</param>
        /// <param name="umin">left bound of the rectangular patch</param>
        /// <param name="umax">right bound of the rectangular patch</param>
        /// <param name="vmin">bottom bound of the rectangular patch</param>
        /// <param name="vmax">top bound of the rectangular patch</param>
        /// <param name="zMin">returned minimum</param>
        /// <param name="zMax">returned maximum</param>
        void GetZMinMax(Projection p, double umin, double umax, double vmin, double vmax, ref double zMin, ref double zMax);
        /// <summary>
        /// Modifies this surface into a more canonical form and returns the modification for the parametric
        /// space which reverses this modification in 2d. Curves in the parametric space of this surface will become
        /// invalid unless modified by the returned transformation.
        /// </summary>
        /// <returns>2d modification for the parametric space</returns>
        ModOp2D MakeCanonicalForm();
        /// <summary>
        /// Returns an identical but independant copy of this surface
        /// </summary>
        /// <returns></returns>
        ISurface Clone();
        /// <summary>
        /// Modifies this surface with the given operation
        /// </summary>
        /// <param name="m">how to modif</param>
        void Modify(ModOp m);
        /// <summary>
        /// Copies the data of the given surface to this surface. The two surfaces are guaranteed to be of the
        /// same type. (Used after <see cref="Clone"/> and <see cref="Modify"/> to restore the original values).
        /// </summary>
        /// <param name="CopyFrom">where to copy the data from</param>
        void CopyData(ISurface CopyFrom);
        /// <summary>
        /// Create a NurbSurface as an approximation of this surface
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        NurbsSurface Approximate(double umin, double umax, double vmin, double vmax, double precision);
        /// <summary>
        /// Returns the projection of the given curve in 2D coordinates. Should only be used for curves 
        /// that are close to the surface.
        /// </summary>
        /// <param name="curve"></param>
        /// <returns></returns>
        ICurve2D GetProjectedCurve(ICurve curve, double precision); // muss mit BoundingRect domain erweitert werden
        /// <summary>
        /// Returns the intersection of the provided <paramref name="curve"/> with this surface. 
        /// The result may be empty.
        /// </summary>
        /// <param name="curve">The curve to be intersected with</param>
        /// <param name="ips">Resulting 3d intersection points</param>
        /// <param name="uvOnFaces">u/v values of the intersection points on this surface</param>
        /// <param name="uOnCurve3Ds">u parameter of intersection points on the curve</param>
        void Intersect(ICurve curve, BoundingRect uvExtent, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds);
        /// <summary>
        /// Returns a double value, if this surface is parallel to the other surface. Returns double.MaxValue otherwise.
        /// In most cases the bounds may be ignored.
        /// </summary>
        /// <param name="thisBounds"></param>
        /// <param name="other"></param>
        /// <param name="otherBounds"></param>
        /// <returns></returns>
        double IsParallel(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds);

        /// <summary>
        /// Returns the intersection curves between this surface and the provided other surface.
        /// Both surfaces are bound by rectangles.
        /// </summary>
        /// <param name="thisBounds">Bounds for this surface</param>
        /// <param name="other">Other surface</param>
        /// <param name="otherBounds">Bounds of other surface</param>
        /// <returns>Array of intersection curves</returns>
        ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds);
        /// <summary>
        /// Reverses the orientation of this surface. The normal vector will point to the other side after this operation.
        /// The returned <see cref="ModOp2D"/> determins how (u,v) coordinates of the parameter space have to be 
        /// transformed to define the same 3d point.
        /// </summary>
        /// <returns>Transformation of the parameter space</returns>
        ModOp2D ReverseOrientation();
        /// <summary>
        /// Returns true if this surface and the other surface are geometrically identical, i.e. describe the same surface
        /// in 3D space. The may have a different u/v system. The returned <paramref name="firstToSecond"/> contains
        /// the ModOp to convert from the u/v system of the first surface to the second surface.
        /// </summary>
        /// <param name="thisBounds">Bounds for this surface</param>
        /// <param name="other">Other surface</param>
        /// <param name="otherBounds">Bounds of other surface</param>
        /// <param name="precision">Required precision</param>
        /// <param name="firstToSecond">Transformation between different u/v systems</param>
        /// <returns>True if the surfaces are geometrically equal</returns>
        bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond);
        /// <summary>
        /// Returns a surface that is "parallel" to this surface, i.e. each point on this surface corresponds a 
        /// point on the returned surface that has the same (u,v) coordinates and has the 3d coordinates oft the
        /// point plus offset*Normal at this point
        /// </summary>
        /// <param name="offset">Offset to this surface</param>
        /// <returns>The offset surface</returns>
        ISurface GetOffsetSurface(double offset);
        /// <summary>
        /// Returns the natural bounds of the surface. The returned values may be infinite
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
        /// <summary>
        /// Checks whether this surface restricted by the provided parameters interferes with the provided cube.
        /// </summary>
        /// <param name="cube">Bounding cube for the test</param>
        /// <param name="umin">Minimum for the u parameter</param>
        /// <param name="umax">Maximum for the u parameter</param>
        /// <param name="vmin">Minimum for the v parameter</param>
        /// <param name="vmax">Maximum for the v parameter</param>
        /// <returns>true if the cube and the surface interfere</returns>
        bool HitTest(BoundingCube cube, double umin, double umax, double vmin, double vmax);
        /// <summary>
        /// Returns true, if this surface interferes with the provided cube. If this is the case
        /// uv will contain a point (in the parameter system of the surface) which is inside the cube
        /// </summary>
        bool HitTest(BoundingCube cube, out GeoPoint2D uv);
        /// <summary>
        /// Returns true, if this surface divides the space into two parts. If the surfaces is Oriented 
        /// <see cref="Orientation"/> returns a valid result
        /// </summary>
        bool Oriented { get; }
        /// <summary>
        /// Returns the orientation of the provided point. The sign of the result may be used to distinguish
        /// between inside and outside.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        double Orientation(GeoPoint p);
        /// <summary>
        /// Returns an array of points in parametric space where there are extrema in direction of x-, y- or z-axis.
        /// The normal vector in a extremum is parallel to one of the axis and the surface has a relative maximum or
        /// minimum in this direction.
        /// </summary>
        /// <returns>s.a.</returns>
        GeoPoint2D[] GetExtrema();
        /// <summary>
        /// Returns the extent of a patch of the surface clipped rectangular in the 2d parameter space
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        BoundingCube GetPatchExtent(BoundingRect uvPatch, bool rough = false);
        /// <summary>
        /// Returns a curve where the u parameter of this surface is fixed and the v parameter starts a vmin and ends at vmax
        /// </summary>
        /// <param name="u"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        ICurve FixedU(double u, double vmin, double vmax);
        /// <summary>
        /// Returns a curve where the v parameter of this surface is fixed and the u parameter starts a umin and ends at umax
        /// </summary>
        /// <param name="v"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <returns></returns>
        ICurve FixedV(double v, double umin, double umax);
        double[] GetPolynomialParameters();
        /// <summary>
        /// Mostly used internally
        /// </summary>
        /// <param name="boundingRect"></param>
        void SetBounds(BoundingRect boundingRect);
        /// <summary>
        /// Returns a list of perpendicular foot points of the surface. The list may be empty
        /// </summary>
        /// <param name="fromHere">Source point for the perpendicular foot</param>
        /// <returns>Array of foot-points, may be empty</returns>
        GeoPoint2D[] PerpendicularFoot(GeoPoint fromHere);
        bool HasDiscontinuousDerivative(out ICurve2D[] discontinuities);
        /// <summary>
        /// If this surface is periodic in u or v or both return a non-periodic surface
        /// which describes the same geometric surface but with a different parametric system.
        /// It also removes poles and is useful for non-periodic surfaces with a single pole.
        /// </summary>
        /// <param name="orientedCurves">3d curves, which describe the outline, that will be used. this is needed by some surfaces to determine the maximum definition area</param>
        /// <returns></returns>
        ISurface GetNonPeriodicSurface(ICurve[] orientedCurves);
        /// <summary>
        /// Returns a parallelepiped (a prism with parallelograms) defined by the parameters <paramref name="loc"/>,
        /// <paramref name="dir1"/>, <paramref name="dir2"/>, <paramref name="dir3"/> which completeley covers or encloses
        /// the patch of the surface defined by the <paramref name="uvpatch"/>. There are obviously many solutions
        /// to this problem but a parallelepiped with minimum volume would be preferred. This method is used
        /// to optimate intersection algorithms.
        /// </summary>
        /// <param name="uvpatch">The patch of the surface in parametric space</param>
        /// <param name="loc">One vertex of the result</param>
        /// <param name="dir1">One of the three vectors of the parallelepiped</param>
        /// <param name="dir2">One of the three vectors of the parallelepiped</param>
        /// <param name="dir3">One of the three vectors of the parallelepiped</param>
        void GetPatchHull(BoundingRect uvpatch, out GeoPoint loc, out GeoVector dir1, out GeoVector dir2, out GeoVector dir3);
        /// <summary>
        /// returns wheather the surface is linear in u or v direction
        /// </summary>
        RuledSurfaceMode IsRuled { get; }
        /// <summary>
        /// used internally. the maximum distance of the 3d curve, formed by the uv-line from sp to ep, to the 3d-line from PointAt(sp) to PointAt(ep)
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="ep"></param>
        /// <param name="surface"></param>
        /// <param name="mp">The uv point where this distance occurres</param>
        /// <returns></returns>
        double MaxDist(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp);
        /// <summary>
        /// Returns the intersection curves between this surface and the provided other surface.
        /// Both surfaces are bound by rectangles.
        /// </summary>
        /// <param name="thisBounds">Bounds for this surface</param>
        /// <param name="other">Other surface</param>
        /// <param name="otherBounds">Bounds of other surface</param>
        /// <returns>Array of intersection curves</returns>
        ICurve Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, GeoPoint seed);
        /// <summary>
        /// Returns intersectionCurves 
        /// </summary>
        /// <param name="bounds1"></param>
        /// <param name="surface2"></param>
        /// <param name="bounds2"></param>
        /// <returns></returns>
        IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> extremePositions = null);
        /// <summary>
        /// Returns a List of self intersection curves in the u/v system
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        ICurve2D[] GetSelfIntersections(BoundingRect bounds);
        /// <summary>
        /// Returns a list of points where the surfaces touch each other (where the surfaces are tangential)
        /// </summary>
        /// <param name="thisBounds"></param>
        /// <param name="other"></param>
        /// <param name="otherBounds"></param>
        /// <returns></returns>
        GeoPoint[] GetTouchingPoints(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds);
        /// <summary>
        /// Returns a simpler form of this surface: SurfaceOfLinearExtrusion might be a cylinder, NURBS might be a sphere etc.
        /// Returns null, if there is no simpler form
        /// </summary>
        /// <param name="precision">maximal allowed deviation of the new surface</param>
        /// <returns></returns>
        ISurface GetCanonicalForm(double precision, BoundingRect? bounds = null);
        /// <summary>
        /// Find positions in the uv system, where the connection of the points are perpendicular on both surfaces. the resulting list <paramref name="extremePositions"/> may be 
        /// only partially filled, the missing uv values may be double.NaN, because this is what we need in most cases.
        /// </summary>
        /// <param name="thisBounds"></param>
        /// <param name="other"></param>
        /// <param name="otherBounds"></param>
        /// <param name="extremePositions"></param>
        /// <returns></returns>
        int GetExtremePositions(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, out List<Tuple<double, double, double, double>> extremePositions);
        /// <summary>
        /// Find positions on the surface and on the curve where the connection of these points are perpendicular to the surface and to the curve
        /// </summary>
        /// <param name="domain">valid area for the surface</param>
        /// <param name="curve3D">the curve</param>
        /// <param name="positions">the positions found: first two doubles are u,v on the surface, third is u on the curve</param>
        /// <returns></returns>
        int GetExtremePositions(BoundingRect domain, ICurve curve3D, out List<Tuple<double, double, double>> positions);
        /// <summary>
        /// Returns the distance of the provided point <paramref name="p"/> to the (unlimited) surface.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        double GetDistance(GeoPoint p);
        /// <summary>
        /// returns true, if the provided direction can be interpreted as an extrusion direction of the surface
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        bool IsExtruded(GeoVector direction);
        /// <summary>
        /// returns true, if the provided direction can be interpreted as a surface of rotation around the provided <paramref name="rotationAxis"/>
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        bool IsRotated(Axis rotationAxis);
        /// <summary>
        /// Returns a context menu to change certain parameters of the surface of a face
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="face"></param>
        /// <returns></returns>
        MenuWithHandler[] GetContextMenuForParametrics(IFrame frame, Face face);
        bool UvChangesWithModification { get; }
        /// <summary>
        /// Gets the IPropertyEntry to display this surface in the property grid (may be null)
        /// </summary>
        IPropertyEntry GetPropertyEntry(IFrame frame);
        /// <summary>
        /// Modifies this surface to best approximate the provided points. 
        /// </summary>
        /// <param name="toPoints"></param>
        /// <returns>The sum of the squared errors</returns>
        double Fit(IEnumerable<GeoPoint> toPoints);
    }

    /// <summary>
    /// Soll von Flächen implementiert werden, deren Schnitte mit Ebenen einfach sind (Linien, Ellipsen) um numerisch iterierte
    /// Lösungen zu vermeiden
    /// </summary>
    internal interface ISurfacePlaneIntersection
    {
        ICurve2D[] GetPlaneIntersection(Plane plane, double umin, double umax, double vmin, double vmax);
    }
    /// <summary>
    /// Interface of a surface rotating in u direction. Should be implemented by all surfaces, which can be looked at this was
    /// </summary>
    public interface ISurfaceOfRevolution
    {
        /// <summary>
        /// The axis for the revolution
        /// </summary>
        Axis Axis { get; }
        /// <summary>
        /// The curve, that is rotated
        /// </summary>
        ICurve Curve { get; }
    }
    /// <summary>
    /// Interface of an extrusion surface. All surfaces, that can be interpreted as an extrusion should implement this interface
    /// </summary>
    public interface ISurfaceOfExtrusion
    {
        /// <summary>
        /// The axis of extrusion. The axis must start at the bottom of the domain and go up to the top of the domain (or left and right, depending on <see cref="ExtrusionDirectionIsV"/>).
        /// </summary>
        ICurve Axis(BoundingRect domain);
        IOrientation Orientation { get; }
        /// <summary>
        /// The curve, which is moved along the axis, e.g. an arc, when this is a <see cref="CylindricalSurface"/>
        /// </summary>
        ICurve ExtrudedCurve { get; }
        /// <summary>
        /// The direction of extrusion is the v-parameter of the surface (when false, it is the u-parameter)
        /// </summary>
        bool ExtrusionDirectionIsV { get; }
        /// <summary>
        /// Modify the axis to make it pass the provided <paramref name="throughPoint"/> (and stay parallel to the original axis)
        /// </summary>
        /// <param name="throughPoint"></param>
        /// <returns>true, when possible</returns>
        bool ModifyAxis(GeoPoint throughPoint);
    }

    /// <summary>
    /// This surface interface is mainly for fillets
    /// </summary>
    public interface ISurfaceOfArcExtrusion : ISurfaceOfExtrusion
    {
        double Radius { get; set; }
    }
    public interface INonPeriodicSurfaceConversion
    {
        GeoPoint2D ToPeriodic(GeoPoint2D uv);
        GeoPoint2D FromPeriodic(GeoPoint2D uv);
        ICurve2D ToPeriodic(ICurve2D curve2d);
        ICurve2D FromPeriodic(ICurve2D curve2d);
    }

    internal class DirMinimum
    /* Todo:
     * In SR1 Derivation2At durch DerivationAt ersetzen. Poff!
     * Setzbarmachung von domainstretchfactor und tolerancefactor
    */
    {
        // Setzbare Variablen die als Paramter dienen
        private ISurface surface; // Das Surface auf dem gearbeitet wird
        private BoundingRect Rect; // Suchbereich
        private GeoPoint2D x; // Startwert, wird im Verlauf geändert! Default: Rect.GetCenter() bzw. (start+end)/2
        private GeoVector direction; // Suchrichtung, essentiell
        private BoundingRect domain; // Maximaler Suchbereich, default Rect (recommend)
        private BoundingRect surfacedomain; // Maximal domain of the surface. 
        private double a = 1; // Startschrittweite für Linesearch, default 1
        private double c = 0.0001; // Armijokonstante 1, default: 0.0001 (recommend)
        private double d = 0.1; // Eigenwertkorrektur für Hesse-Matrix, default: 0.1
        private double e = 10; // Eigenwertkorrektur für inverse der Hesse-Approx-Matrix, default = 10;
        private double epsilon = 0.0000000001; // Genauigkeit für Norm des Gradienten, default 0.0000000001
        private double r = 0.0000001; // Stabilitätskonstante für SR1, default 0.0000001 ; vgl. p. 145 in Numerical Optimization
        private double plength = 1; // Maximale Länge des Abstiegsvektors p, es sollte eher dies als a geändert werden!
        private double allowedoutruns = 5; // Maximale Anzahl an Iterationsschritten, die Punkte außerhalb von Rect liegen, default 5
        private int smallchange = 7; // Gibt an, wie oft sich der Gradient nur Minimal ändern darf, default 7
        private double smallchangefactor = 0.9; // Faktor um wieviel der Gradient je Schritt kleiner werden soll, default 0.1
        private int maxtries = 3; // Wie oft soll bei uv.x=4, uv.x=1, neu versucht werden

        private double domainstretchfactor = 1.03; // Gibt an, um wieviel doamin gestreckt werden soll, wenn der nächste Pkt. nicht zulässig ist.
        private double tolerancefactor = 1000; // Gibt an, um welchen Faktor der Gradient größer sein darf als epsilon um dann noch ggfs. die Schrittweite "manuell" zu setzen oder domain zu vergrößern.

        // Rein interne Variablen
        private GeoPoint2D p, ap, xap, uv, start, end;
        private bool success;
        private double steplength;
        private GeoPoint loctemp = new GeoPoint(0, 0, 0), value;
        private GeoVector dutemp, dvtemp, duutemp, dvvtemp, duvtemp;
        private GeoVector2D dir;
        private double loc, du, dv, duu, dvv, duv;
        private int stagnation;
        // Die beiden Schnuckies sollten jeweils auf 0 gesetzt werden.
        private double gradnorm = 0;
        private double lastgradnorm = 0;
        private double du2, dv2;
        private double l, t, temp1, temp2, temp3;
        private double gradient1, gradient2, p1D, hesse;
        private double y1, y2; // (y1,y2) is typically just (du2-du,dv2-dv)
        private int outrun;
        private int tries;

        // Gibt an, welches Verfahren zuletzt benutzt wurde, dies ist für etwaige Continue-Methoden existenziell, sonst produziert sich da mglws. sehr viel Käse.
        private char verfahren = '0'; // 1 -> NewtonRect, 2 -> NewtonLine, 3 -> SR1Recht, 4 -> SR1Line

        // Standardkonstruktor
        // Erhält die Fläche und bestimmt den maximalen Definitionsbereich selbiger.
        public DirMinimum(ISurface surface)
        {
            this.surface = surface;
            double umin, vmin, umax, vmax;
            surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            surfacedomain = new BoundingRect(umin, vmin, umax, vmax);
        }


        // Bei einem Fehlschlag kann über diese Methode der letzte zulässige Punkt, der Funktionswert und die Norm des Gradienten abgerufen werden
        public void GetLast(out GeoPoint2D x, out GeoPoint loc, out double norm)
        {
            if (verfahren == '1' || verfahren == '2')
            {
                x = this.x;
            }
            else
            {
                x = this.xap;
            }
            loc = this.loctemp;
            norm = this.gradnorm;
        }
        public GeoPoint2D Getxap()
        {
            return xap;
        }
        // Diese Mehtode bestimmt den Abstand von x in Richtung p zum Rechteckt. Zweck ist hierbei, zu wissen, wie nah man am Rand ist um ggfs. das Verfahren mit neuem Punkt zu starten.
        private double DistanceToBorder(ref BoundingRect Rect)
        {
            if (p.x > 0)
            {
                if (p.y > 0)
                {
                    temp1 = System.Math.Min((Rect.Right - x.x) / p.x, (Rect.Top - x.y) / p.y);
                    return System.Math.Sqrt(System.Math.Pow(temp1 * p.x, 2) + System.Math.Pow(temp1 * p.y, 2));
                }
                else if (p.y < 0)
                {
                    temp1 = System.Math.Min((Rect.Right - x.x) / p.x, (Rect.Bottom - x.y) / p.y);
                    return System.Math.Sqrt(System.Math.Pow(temp1 * p.x, 2) + System.Math.Pow(temp1 * p.y, 2));
                }
                else
                {
                    temp1 = (Rect.Right - x.x) / p.x;
                    return System.Math.Sqrt(System.Math.Pow(temp1 * p.x, 2) + System.Math.Pow(temp1 * p.y, 2));
                }
            }
            else if (p.x < 0)
            {
                if (p.y > 0)
                {
                    temp1 = System.Math.Min((Rect.Left - x.x) / p.x, (Rect.Top - x.y) / p.y);
                    return System.Math.Sqrt(System.Math.Pow(temp1 * p.x, 2) + System.Math.Pow(temp1 * p.y, 2));
                }
                else if (p.y < 0)
                {
                    temp1 = System.Math.Min((Rect.Left - x.x) / p.x, (Rect.Bottom - x.y) / p.y);
                    return System.Math.Sqrt(System.Math.Pow(temp1 * p.x, 2) + System.Math.Pow(temp1 * p.y, 2));
                }
                else
                {
                    temp1 = (Rect.Left - x.x) / p.x;
                    return System.Math.Sqrt(System.Math.Pow(temp1 * p.x, 2) + System.Math.Pow(temp1 * p.y, 2));
                }
            }
            else
            {
                if (p.y > 0)
                {
                    temp1 = (Rect.Top - x.y) / p.y;
                    return System.Math.Sqrt(System.Math.Pow(temp1 * p.x, 2) + System.Math.Pow(temp1 * p.y, 2));
                }
                else if (p.y < 0)
                {
                    temp1 = (Rect.Bottom - x.y) / p.y;
                    return System.Math.Sqrt(System.Math.Pow(temp1 * p.x, 2) + System.Math.Pow(temp1 * p.y, 2));
                }
                else
                {
                    return -1; // This usually can't happen.
                }
            }
        }
        public double DistanceToBorder()
        {
            return DistanceToBorder(ref this.Rect);
        }


        // Liefert false, falls ein Parameter <= 0 ist.
        public bool SetAllParams(double a, double c, double d, double e, double epsilon, double r, double plength, int allowedoutruns, int smallchange, double smallchangefactor, int maxtries, double domainstretchfactor, double tolerancefactor)
        {
            if ((a > 0) && (c > 0) && (d > 0) && (e > 0) && (epsilon > 0) && (r > 0) && (plength > 0) && (allowedoutruns >= 0) && (smallchange > 0) && (smallchangefactor > 0) && (maxtries > 0) && (domainstretchfactor > 1) && (tolerancefactor > 1))
            {
                this.a = a;
                this.c = c;
                this.d = d;
                this.e = e;
                this.epsilon = epsilon;
                this.r = r;
                this.plength = plength;
                this.allowedoutruns = allowedoutruns;
                this.smallchange = smallchange;
                this.smallchangefactor = smallchangefactor;
                this.maxtries = maxtries;
                this.tolerancefactor = tolerancefactor;
                this.domainstretchfactor = domainstretchfactor;
                return true;
            }
            return false;
        }
        public bool SetEpsilon(double epsilon)
        {
            if (epsilon > 0)
            {
                this.epsilon = epsilon;
                return true;
            }
            return false;
        }
        public bool SetPlength(double plength)
        {
            if (plength > 0)
            {
                this.plength = plength;
                return true;
            }
            return false;
        }
        public bool SetSmallchange(int smallchange, double smallchangefactor)
        {
            if ((smallchange > 0) && (smallchangefactor > 0))
            {
                this.smallchange = smallchange;
                this.smallchangefactor = smallchangefactor;
                return true;
            }
            return false;
        }
        public bool SetInitialSteplength(double a)
        {
            if (a > 0)
            {
                this.a = a;
                return true;
            }
            return false;
        }
        public bool SetAllowedOutruns(int allowedoutruns)
        {
            if (allowedoutruns >= 0)
            {
                this.allowedoutruns = allowedoutruns;
                return true;
            }
            return false;
        }
        public bool SetMaxtries(int maxtries)
        {
            if (maxtries > 0)
            {
                this.maxtries = maxtries;
                return true;
            }
            return false;
        }
        public bool SetDomainStrachfactor(double domainstretchfactor, double tolerancefactor)
        {
            if ((domainstretchfactor) > 1 && (tolerancefactor > 1))
            {
                this.domainstretchfactor = domainstretchfactor;
                this.tolerancefactor = tolerancefactor;
                return true;
            }
            return false;
        }
        public void SetDefaults()
        {
            a = 1; // Startschrittweite für Linesearch, default 1
            c = 0.0001; // Armijokonstante 1, default: 0.0001 (recommend)
            d = 0.1; // Eigenwertkorrektur für Hesse-Matrix, default: 0.1
            e = 10; // Eigenwertkorrektur für inverse der Hesse-Approx-Matrix, default = 10;
            epsilon = 0.0000000001; // Genauigkeit für Norm des Gradienten, default 0.0000000001
            r = 0.0000001; // Stabilitätskonstante für SR1, default 0.0000001 ; vgl. p. 145 in Numerical Optimization
            plength = 1; // Maximale Länge des Abstiegsvektors p, es sollte eher dies als a geändert werden!
            allowedoutruns = 5; // Maximale Anzahl an Iterationsschritten, die Punkte außerhalb von Rect liegen, default 5
            smallchange = 7; // Gibt an, wie oft sich der Gradient nur Minimal ändern darf, default 7
            smallchangefactor = 0.9; // Faktor um wieviel der Gradient je Schritt kleiner werden soll, default 0.1
            maxtries = 3; // Wie oft soll bei uv.x=4, d.h. effektive Schrittweite 0 neu versucht werden
            tolerancefactor = 1000;
            domainstretchfactor = 1.03;
        }

        // If you have effecticly steplength 0, and you have enough space left to border in directtion p, you can manually set the steplength and define a new startpoint x
        // If you use Newton, just call one of the standardmethods below with startpoint xap, if you use SR1, then use one of continuemethods to use approximation of the hessian from the last step
        public bool Setxap(double steplength)
        {
            xap = new GeoPoint2D(x.x + steplength * p.x, x.y + steplength * p.y);
            if (Rect.Contains(xap))
            {
                return true;
            }
            return false;
        }


        // Die Methoden liefern bei Erfolg true, und die Werte. 
        // Bei false gilt: 
        // uv.x=0 -> Schrittweite 0, Verfahren wird stationär, 
        // uv.x=1 -> Definitonsbereich des Surface wurde verlassen
        // uv.x=2 -> Gegebenes Rechteck wurde mehr als 5 mal verlassen, die Lsg. liegt vmtl. außerhalb
        // uv.x=3 -> Kein nennenswerter Fortschritt
        // uv.x=4 -> Schrittweise > 0, aber praktisch 0, d.h. x = xap. Wahrscheinlich nahe am Minimum, mglws. aber auch am Rand und die Fläche ist dort flach
        // uv.x=5 -> Gradient ist hinreichend klein, die Hessematrix aber nicht pos. defintit., d.h. es liegt entweder ein Maximum oder Sattelpunkt vor, oder aber es lässt sich keine Aussagen treffen
        // uv.x=6 -> Falsche Fortsetzungsmethode ausgewählt.
        // uv.x=7 -> Das Minimum wurde gefunden, liegt aber nicht in Rect.
        // uv.x=8 -> uv.x=1 & Gradient ist um mindestens tolarancefactor größer als epsilon, es steht zu erwarten, dass kein Minimum ex.
        // Beachte: uv.x=5 kann nur eintreten bei einem Newtonverfahren. Im Quasi-Newtonverfahren kann nur die geschätze Hesse-Matrix bestimmt werden, diese mglws. indefinit.
        // Beachte: Ist Rect größer als der Definitonsbereich des Surface, so gibt es möglicherweise einen Fehler!
        // Beachte: value wird bei false immer als (0,0,0) zurückgeliefert.
        // Beachte: domain und Rect werden automatisch verkleinert, falls surfacedomain nicht domain enthält bzw. domain nicht Rect.

        // Methoden für Suche nach dem Minimum auf gegebenem Rechteckt mittels Newtonverfahren
        public bool GetNewtonRect(GeoVector dir, BoundingRect Rect, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, Rect);
            this.Rect = domain;
            x = Rect.GetCenter();
            this.direction = dir;

            ManageNewtonRect();

            uv = this.uv;
            value = this.value;
            return success;
        }
        public bool GetNewtonRect(GeoVector dir, BoundingRect Rect, GeoPoint2D x0, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, Rect);
            this.Rect = domain;

            if (!Rect.Contains(x0))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            this.x = x0;
            this.direction = dir;

            ManageNewtonRect();

            uv = this.uv;
            value = this.value;
            return success;
        }
        public bool GetNewtonRect(GeoVector dir, BoundingRect Rect, BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            this.Rect = BoundingRect.Common(this.domain, Rect);
            x = Rect.GetCenter();
            this.direction = dir;

            ManageNewtonRect();

            uv = this.uv;
            value = this.value;
            return success;
        }
        public bool GetNewtonRect(GeoVector dir, BoundingRect Rect, BoundingRect domain, GeoPoint2D x0, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            this.Rect = BoundingRect.Common(this.domain, Rect);
            if (!Rect.Contains(x0))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            this.x = x0;
            this.direction = dir;

            ManageNewtonRect();

            uv = this.uv;
            value = this.value;
            return success;
        }
        // Continuemethods fpr NewtonRect. Be sure, you didn't changed x, if you do so, the derivates used for your x are the ones from the x you override
        // Plus, we don't check if x is in domain, we don't expect, you changed x
        // If you want to change x, use one of the methods above
        public bool ContinueNewtonRect(out GeoPoint2D uv, out GeoPoint value)
        {
            if (verfahren == '1')
            {
                success = NewtonRectangle();
                uv = this.uv;
                value = this.value;
                return success;
            }
            uv = new GeoPoint2D(6, 0);
            value = new GeoPoint(0, 0, 0);
            return false;
        }
        public bool ContinueNewtonRect(BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            if (!domain.Contains(x))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            if (verfahren == '1')
            {
                success = NewtonRectangle();
                uv = this.uv;
                value = this.value;
                return success;
            }
            uv = new GeoPoint2D(6, 0);
            value = new GeoPoint(0, 0, 0);
            return false;
        }

        // Methoden für Suche nach dem Minimum auf gegebener Geraden mittels Newtonverfahren
        public bool GetNewtonLine(GeoVector dir, GeoPoint2D start, GeoPoint2D end, out GeoPoint2D uv, out GeoPoint value)
        {
            Rect = new BoundingRect(Math.Min(start.x, end.x), Math.Min(start.y, end.y), Math.Max(start.x, end.x), Math.Max(start.y, end.y)); domain = Rect;
            Rect = BoundingRect.Common(surfacedomain, Rect);
            domain = Rect;
            x = Rect.GetCenter();
            //x = new GeoPoint2D(start, end);
            this.direction = dir;
            this.start = start;
            this.end = end;

            ManageNewtonLine();

            uv = this.uv;
            value = this.value;
            return success;
        }
        public bool GetNewtonLine(GeoVector dir, GeoPoint2D start, GeoPoint2D end, GeoPoint2D x0, out GeoPoint2D uv, out GeoPoint value)
        {
            Rect = BoundingRect.Common(surfacedomain, new BoundingRect(new GeoPoint2D(start, end), System.Math.Abs(end.x - start.x), System.Math.Abs(end.y - start.y)));
            this.domain = Rect;
            x = x0;
            if (!Rect.Contains(x))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            if (!(((end.x - start.x) / x.x) == ((end.y - start.y) / x.y)))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }

            this.direction = dir;

            this.start = start;
            this.end = end;

            ManageNewtonLine();

            uv = this.uv;
            value = this.value;
            return success;
        }
        public bool GetNewtonLine(GeoVector dir, GeoPoint2D start, GeoPoint2D end, BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            this.Rect = BoundingRect.Common(this.domain, new BoundingRect(new GeoPoint2D(start, end), System.Math.Abs(end.x - start.x), System.Math.Abs(end.y - start.y)));
            x = Rect.GetCenter();
            //x = new GeoPoint2D(start, end);
            this.direction = dir;

            this.start = start;
            this.end = end;

            ManageNewtonLine();

            uv = this.uv;
            value = this.value;
            return success;
        }
        public bool GetNewtonLine(GeoVector dir, GeoPoint2D start, GeoPoint2D end, BoundingRect domain, GeoPoint2D x0, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            this.Rect = BoundingRect.Common(this.domain, new BoundingRect(new GeoPoint2D(start, end), System.Math.Abs(end.x - start.x), System.Math.Abs(end.y - start.y)));

            x = x0;
            if (!Rect.Contains(x))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            if (!(((end.x - start.x) / x.x) == ((end.y - start.y) / x.y)))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }

            this.direction = dir;

            this.start = start;
            this.end = end;

            ManageNewtonLine();

            uv = this.uv;
            value = this.value;
            return success;
        }
        // Continuemethods for NewtonRect. Be sure, you didn't changed x, if you do so, the derivates used for your x are the ones from the x you override
        // Plus, we don't check if x is in domain, we don't expect, you changed x
        // If you want to change x, use one of the methods above
        public bool ContinueNewtonLine(out GeoPoint2D uv, out GeoPoint value)
        {
            if (verfahren == '2')
            {
                success = NewtonLine();
                uv = this.uv;
                value = this.value;
                return success;
            }
            uv = new GeoPoint2D(6, 0);
            value = new GeoPoint(0, 0, 0);
            return false;
        }
        public bool ContinueNewtonLine(BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            if (!domain.Contains(x))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            if (verfahren == '1')
            {
                success = NewtonLine();
                uv = this.uv;
                value = this.value;
                return success;
            }
            uv = new GeoPoint2D(6, 0);
            value = new GeoPoint(0, 0, 0);
            return false;
        }

        // Methoden für Suche nach dem Minimum auf gegebenem Rechteckt mittels SR1
        public bool GetSR1Rect(GeoVector dir, BoundingRect Rect, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, Rect);
            this.Rect = domain;
            x = Rect.GetCenter();
            this.direction = dir;

            ManageSR1Rect();

            uv = this.uv;
            value = this.value;
            return success;

        }
        public bool GetSR1Rect(GeoVector dir, BoundingRect Rect, GeoPoint2D x0, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, Rect);
            this.Rect = domain;
            if (!Rect.Contains(x0))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            this.x = x0;
            this.direction = dir;

            ManageSR1Rect();

            uv = this.uv;
            value = this.value;
            return success;
        }
        public bool GetSR1Rect(GeoVector dir, BoundingRect Rect, BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            this.Rect = BoundingRect.Common(this.domain, Rect);

            x = Rect.GetCenter();
            this.direction = dir;

            ManageSR1Rect();

            uv = this.uv;
            value = this.value;
            return success;
        }
        public bool GetSR1Rect(GeoVector dir, BoundingRect Rect, BoundingRect domain, GeoPoint2D x0, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            this.Rect = BoundingRect.Common(this.domain, Rect);
            if (!Rect.Contains(x0))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            this.x = x0;
            this.direction = dir;

            ManageSR1Rect();

            uv = this.uv;
            value = this.value;
            return success;
        }
        // It's strongly recommend to use these one heres only if xap is nearly x or if, that's the standard case, you change Rect/domain and want from x go further.
        // Continuemethods don't call the Managemethods. You want them twice? Call them twice.
        public bool ContinueSR1Rect(out GeoPoint2D uv, out GeoPoint value)
        {
            if (verfahren == '3')
            {
                if (!domain.Contains(xap))
                {
                    uv = new GeoPoint2D(1, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }
                surface.DerivationAt(xap, out loctemp, out dutemp, out dvtemp);
                loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
                du2 = direction * dutemp;
                dv2 = direction * dvtemp;
                gradnorm = System.Math.Sqrt(du2 * du2 + dv2 * dv2);
                uv = this.uv;
                value = this.value;
                return SR1Rectangle();
            }
            uv = new GeoPoint2D(6, 0);
            value = new GeoPoint(0, 0, 0);
            return false;
        }
        public bool ContinueSR1Rect(GeoPoint2D xap0, out GeoPoint2D uv, GeoPoint value)
        {
            // Überprüft ob der manuell gesetzte Startpunkt zulässig ist
            if (!Rect.Contains(xap0))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            this.xap = xap0;
            return ContinueSR1Rect(out uv, out value);
        }
        public bool ContinueSR1Rect(BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(domain, surfacedomain);
            return ContinueSR1Rect(out uv, out value);
        }
        public bool ContinueSR1Rect(GeoPoint2D xap0, BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(domain, surfacedomain);
            if (!Rect.Contains(xap0))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            this.xap = xap0;
            return ContinueSR1Rect(out uv, out value);
        }

        // Methoden für Suche nach dem Minimum auf gegebener Geraden mittels SR1
        public bool GetSR1Line(GeoVector dir, GeoPoint2D start, GeoPoint2D end, out GeoPoint2D uv, out GeoPoint value)
        {
            //this.Rect = new BoundingRect(new GeoPoint2D(start.x, start.y), System.Math.Abs(end.x-start.x),System.Math.Abs(end.y-start.y));
            Rect = BoundingRect.Common(new BoundingRect(Math.Min(start.x, end.x), Math.Min(start.y, end.y), Math.Max(start.x, end.x), Math.Max(start.y, end.y)), surfacedomain);
            domain = Rect;
            x = new GeoPoint2D(start, end);
            this.start = start;
            this.end = end;
            this.direction = dir;

            ManageSR1Line();
            uv = this.uv;
            value = this.value;

            return success;
        }
        public bool GetSR1Line(GeoVector dir, GeoPoint2D start, GeoPoint2D end, GeoPoint2D x0, out GeoPoint2D uv, out GeoPoint value)
        {
            Rect = BoundingRect.Common(new BoundingRect(Math.Min(start.x, end.x), Math.Min(start.y, end.y), Math.Max(start.x, end.x), Math.Max(start.y, end.y)), surfacedomain);
            this.domain = Rect;
            x = x0;
            if (!Rect.Contains(x))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            // Testet, ob der Startpunkt auf der Geraden liegt entlang der gesucht wird.
            if (!(((end.x - start.x) / x.x) == ((end.y - start.y) / x.y)))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }

            this.direction = dir;

            ManageSR1Line();
            uv = this.uv;
            value = this.value;

            return success;
        }
        public bool GetSR1Line(GeoVector dir, GeoPoint2D start, GeoPoint2D end, BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(domain, surfacedomain);
            this.Rect = BoundingRect.Common(new BoundingRect(new GeoPoint2D(start, end), System.Math.Abs(end.x - start.x), System.Math.Abs(end.y - start.y)), this.domain);
            x = new GeoPoint2D(start, end);
            this.direction = dir;

            ManageSR1Line();
            uv = this.uv;
            value = this.value;

            return success;
        }
        public bool GetSR1Line(GeoVector dir, GeoPoint2D start, GeoPoint2D end, BoundingRect domain, GeoPoint2D x0, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(domain, surfacedomain);
            this.Rect = BoundingRect.Common(new BoundingRect(new GeoPoint2D(start, end), System.Math.Abs(end.x - start.x), System.Math.Abs(end.y - start.y)), this.domain);
            x = x0;
            if (!Rect.Contains(x))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            // Testet, ob der Startpunkt auf der gegebenen Geraden liegt.
            if (!(((end.x - start.x) / x.x) == ((end.y - start.y) / x.y)))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }

            this.direction = dir;

            ManageSR1Line();
            uv = this.uv;
            value = this.value;

            return success;
        }
        // It's strongly recommend to use these one heres only if xap is nearly x or if, that's the standard case, you change Rect/domain and want from x go further.
        // Continuemethods don't call the Managemethods.
        public bool ContinueSR1Line(out GeoPoint2D uv, out GeoPoint value)
        {
            if (verfahren == '4')
            {
                if (!domain.Contains(xap))
                {
                    uv = new GeoPoint2D(1, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }
                surface.DerivationAt(xap, out loctemp, out dutemp, out dvtemp);
                loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
                du2 = direction * dutemp;
                dv2 = direction * dvtemp;
                gradient2 = du2 * dir.x + dv2 * dir.y;
                gradnorm = System.Math.Abs(gradient2);
                uv = this.uv;
                value = this.value;
                return SR1Line();
            }
            uv = new GeoPoint2D(6, 0);
            value = new GeoPoint(0, 0, 0);
            return false;
        }
        public bool ContinueSR1Line(GeoPoint2D xap0, out GeoPoint2D uv, GeoPoint value)
        {
            // Überprüft ob der manuell gesetzte Startpunkt zulässig ist
            if (!Rect.Contains(xap0))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            this.xap = xap0;
            return ContinueSR1Line(out uv, out value);
        }
        public bool ContinueSR1Line(BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            return ContinueSR1Line(out uv, out value);
        }
        public bool ContinueSR1Line(GeoPoint2D xap0, BoundingRect domain, out GeoPoint2D uv, out GeoPoint value)
        {
            this.domain = BoundingRect.Common(surfacedomain, domain);
            if (!Rect.Contains(xap0))
            {
                uv = new GeoPoint2D(1, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            this.xap = xap0;
            return ContinueSR1Line(out uv, out value);
        }

        // Manager für das Newtonverfahren. Regelt Verhalten bei Fehlern und versucht diese ggfs. zu korrigieren
        // Setzt die Variable succcess. uv und value setzt das Newtonverfahren.
        // Eventuell auch hier ContinueMethoden bereitstellen! Spart ggfs. einmal ableiten doppelt berechnen!
        private void ManageNewtonRect()
        {
            tries = 0;
            success = StartNewtonRectangle();
            do
            {
                tries++;
                if (success)
                {
                    return;
                }
                else
                {
                    // Falls also praktische Schrittweite 0, wird neuer Startpunkt versucht.
                    if (uv.x == 4)
                    {
                        Setxap(DistanceToBorder() / 2);
                        x = xap;
                        success = StartNewtonRectangle();
                    }
                    else
                    {
                        if (uv.x == 1)
                        {
                            if (Stretchdomain())
                            {
                                success = NewtonRectangle();
                            }
                            else
                            {
                                success = false;
                                return;
                            }
                        }
                        // Bei anderen Fehlern wird die Schleife beendet!
                        else
                        {
                            return;
                        }
                    }
                }
            } while (tries <= maxtries);
        }
        private void ManageNewtonLine()
        {
            tries = 0;
            success = StartNewtonLine();
            do
            {
                tries++;
                if (success)
                {
                    return;
                }
                else
                {
                    // Falls also praktische Schrittweite 0, wird neuer Startpunkt versucht.
                    if (uv.x == 4)
                    {
                        Setxap(DistanceToBorder() / 2);
                        x = xap;
                        success = StartNewtonLine();
                    }
                    else
                    {
                        if (uv.x == 1)
                        {
                            if (Stretchdomain())
                            {
                                success = NewtonLine();
                            }
                            else
                            {
                                success = false;
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            } while (tries <= maxtries);
        }
        private void ManageSR1Rect()
        {
            tries = 0;
            success = StartSR1Rectangle();
            do
            {
                tries++;
                if (success)
                {
                    return;
                }
                else
                {
                    // Falls also praktische Schrittweite 0, wird neuer Startpunkt versucht.
                    if (uv.x == 4)
                    {
                        Setxap(DistanceToBorder() / 2);
                        x = xap;
                        success = StartSR1Rectangle();
                    }
                    else
                    {
                        if (uv.x == 1)
                        {
                            if (Stretchdomain())
                            {
                                if (InterpolationStepLength())
                                {
                                    success = ContinueSR1Rect(out uv, out value);
                                }
                            }
                            else
                            {
                                success = false;
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            } while (tries <= maxtries);
        }
        private void ManageSR1Line()
        {
            tries = 0;
            success = StartSR1Line();
            do
            {
                tries++;
                if (success)
                {
                    return;
                }
                else
                {
                    // Falls also praktische Schrittweite 0, wird neuer Startpunkt versucht.
                    if (uv.x == 4)
                    {
                        Setxap(DistanceToBorder() / 2);
                        x = xap;
                        success = StartSR1Line();
                    }
                    else
                    {
                        if (uv.x == 1)
                        {
                            if (Stretchdomain())
                            {
                                if (InterpolationStepLength())
                                {
                                    success = ContinueSR1Line(out uv, out value);
                                }
                            }
                            else
                            {
                                success = false;
                                return;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            } while (tries <= maxtries);
        }

        // Newtonverfahren, diese unterteilen sich wg. der Continuemethoden in zwei Teile. Der erste berechnet die Ableitung im Startpunkt. Ist die schon bekannt, kann dies übersprungen werden
        private bool StartNewtonRectangle()
        {
            verfahren = '1';

            surface.Derivation2At(x, out loctemp, out dutemp, out dvtemp, out duutemp, out dvvtemp, out duvtemp);
            loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
            du = direction * dutemp;
            dv = direction * dvtemp;
            duu = direction * duutemp;
            dvv = direction * dvvtemp;
            duv = direction * duvtemp;
            gradnorm = System.Math.Sqrt(du * du + dv * dv);
            lastgradnorm = gradnorm;
            return NewtonRectangle();
        }
        private bool NewtonRectangle()
        {
            // Newton-Verfahren mit Hessematrix-Modifikation
            // See Numerical Optimization, Nocedal Wright, p.48 ff for further information

            outrun = 0;
            stagnation = 0;

            while (gradnorm > epsilon)
            {
                // Sicherstellen, dass der kleinere Eigenwert "groß" genug ist, d.h. größer d und insbes. positiv
                l = (duu + dvv) / 2 - System.Math.Sqrt(System.Math.Pow((duu - dvv) / 2, 2) + duv * duv);
                t = System.Math.Max(0, d - l);
                duu = duu + t;
                dvv = dvv + t;
                // Weil beide Eigenwerte nun > 0 sind, ist die Determinante der mod. Hesseschen ungleich 0
                p = new GeoPoint2D(-(dvv * du - duv * dv) / (duu * dvv - duv * duv), -(duu * dv - duv * du) / ((duu * dvv - duv * duv)));
                // Setzt p auf Länge plength falls p länger ist
                if (p.x * p.x + p.y * p.y > plength)
                {
                    p = new GeoPoint2D(p.x * plength / System.Math.Sqrt(p.x * p.x + p.y * p.y), p.y * plength / System.Math.Sqrt(p.x * p.x + p.y * p.y));
                }

                if (!this.InterpolationStepLength())
                {
                    uv = new GeoPoint2D(steplength, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }
                // Falls die effektive Schrittweite 0 ist
                if (x == xap)
                {
                    uv = new GeoPoint2D(4, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                x = xap;
                // Überprüft ob der neue Wert im geg. Definitonsbereich ist
                if (!Rect.Contains(x))
                {
                    outrun++;
                }
                if (outrun > allowedoutruns)
                {
                    uv = new GeoPoint2D(2, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                surface.Derivation2At(x, out loctemp, out dutemp, out dvtemp, out duutemp, out dvvtemp, out duvtemp);
                loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
                du = direction * dutemp;
                dv = direction * dvtemp;
                duu = direction * duutemp;
                dvv = direction * dvvtemp;
                duv = direction * duvtemp;
                gradnorm = System.Math.Sqrt(du * du + dv * dv);
                // Hier wird sichergestellt, dass genügend Fortschritt gemacht wird
                if (!CheckChange())
                {
                    uv = new GeoPoint2D(3, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }
            }
            if (((duu + dvv) / 2 - System.Math.Sqrt(System.Math.Pow((duu - dvv) / 2, 2) + duv * duv)) <= 0)
            {
                uv = new GeoPoint2D(5, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            if (!Rect.Contains(x))
            {
                uv = new GeoPoint2D(7, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            uv = x;
            value = loctemp;
            return true;
        }
        private bool StartNewtonLine()
        {
            verfahren = '2';
            surface.Derivation2At(x, out loctemp, out dutemp, out dvtemp, out duutemp, out dvvtemp, out duvtemp);
            loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
            du = direction * dutemp;
            dv = direction * dvtemp;
            duu = direction * duutemp;
            dvv = direction * dvvtemp;
            duv = direction * duvtemp;
            gradient1 = du * dir.x + dv * dir.y;
            gradnorm = System.Math.Abs(gradient1);
            lastgradnorm = gradnorm;
            return NewtonLine();
        }
        private bool NewtonLine()
        {
            // Newton-Verfahren mit Hessematrix-Modifikation
            // See Numerical Optimization, Nocedal Wright, p.48 ff for further information
            outrun = 0;
            stagnation = 0;

            dir = new GeoVector2D(end.x - start.x, end.y - start.y).Normalized;

            while (gradnorm > epsilon)
            {
                hesse = dir.x * (dir.x * duu + dir.y * duv) + dir.y * (dir.x * duv + dir.y * dvv);
                hesse = hesse + System.Math.Max(0, d - hesse);

                p = new GeoPoint2D((-gradient1 / hesse) * dir.x, (-gradient1 / hesse) * dir.y);

                if (!this.InterpolationStepLength())
                {
                    uv = new GeoPoint2D(steplength, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                // Falls die effektive Schrittweite 0 ist
                if (x == xap)
                {
                    uv = new GeoPoint2D(4, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                x = xap;
                if (!Rect.Contains(x))
                {
                    outrun++;
                }
                if (outrun > allowedoutruns)
                {
                    uv = new GeoPoint2D(2, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                surface.Derivation2At(x, out loctemp, out dutemp, out dvtemp, out duutemp, out dvvtemp, out duvtemp);
                loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
                du = direction * dutemp;
                dv = direction * dvtemp;
                duu = direction * duutemp;
                dvv = direction * dvvtemp;
                duv = direction * duvtemp;
                gradient1 = du * dir.x + dv * dir.y;
                gradnorm = System.Math.Abs(gradient1);
                if (!CheckChange())
                {
                    uv = new GeoPoint2D(3, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }
            }
            if ((dir.x * (dir.x * duu + dir.y * duv) + dir.y * (dir.x * duv + dir.y * dvv)) <= 0)
            {
                uv = new GeoPoint2D(5, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            if (!Rect.Contains(x))
            {
                uv = new GeoPoint2D(7, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            uv = x;
            value = loctemp;
            return true;
        }


        // SR1-Verfahren. Dieses unterteilt sich wg. der Continue-Methode in zwei Teile. Im ersten wird die erste Näherung bestimmt, hat man schon eine, dann kann man das eigentliche Verfahren aufrufen
        private bool StartSR1Rectangle()
        {
            verfahren = '3';
            duv = 0;

            surface.DerivationAt(x, out loctemp, out dutemp, out dvtemp);
            loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
            du = direction * dutemp;
            dv = direction * dvtemp;

            p = new GeoPoint2D(-du, -dv);

            // The first thing we have to do is build up a first good approx. of the Hessian, see Nocedal, Wright, p142 ff.
            if (!this.InterpolationStepLength())
            {
                uv = new GeoPoint2D(steplength, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }

            ap = new GeoPoint2D(steplength * p.x, steplength * p.y);

            surface.DerivationAt(xap, out loctemp, out dutemp, out dvtemp);
            du2 = direction * dutemp;
            dv2 = direction * dvtemp;

            // Wir testen auch mal diese Punkt ob er gut ist, falls ja, hören wir eben auf
            if (System.Math.Sqrt(du2 * du2 + dv2 * dv2) < epsilon)
            {
                uv = xap;
                value = loctemp;
                return true;
            }

            y1 = du2 - du;
            y2 = dv2 - dv;
            duu = (y1 * ap.x + y2 * ap.y) / (y1 * y1 + y2 * y2);
            dvv = duu;

            // Now we have the first Approximation. Let's make Eigenvaluecorrection
            l = (duu + dvv) / 2 - System.Math.Sqrt(System.Math.Pow((duu - dvv) / 2, 2) + duv * duv);
            if (l <= 0)
            {
                t = -l + e;
                duu = duu + t;
                dvv = dvv + t;
            }

            // Neue Richtung bestimmen, eigentlich geht's hier erst richtig los
            p = new GeoPoint2D(-(duu * du), -(dvv * dv));
            if (!this.InterpolationStepLength())
            {
                uv = new GeoPoint2D(steplength, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }

            surface.DerivationAt(xap, out loctemp, out dutemp, out dvtemp);
            loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
            du2 = direction * dutemp;
            dv2 = direction * dvtemp;

            gradnorm = System.Math.Sqrt(du2 * du2 + dv2 * dv2);

            return SR1Rectangle();
        }
        private bool SR1Rectangle()
        {
            // SR1 or SSR1 namely Symmetric (Scaled) Rank One is Quasi-Newton-Method
            // For further Information, see Nocedal, Wright: Numerical Optimization, p. 144 ff.
            // and Hassan, Mansor, June: Convergence of a Positive-Definit Symmetric Scaled Rank One Method, in
            // Matematika, 2002, Jilid 18, bil. 2
            // Note, we use some different method: If the approxed Hessian isn't positive definit, we enforce this by Eigenvaluemodification
            // Thus we have still a descent direction and the above Paper should be applicable. Since nearly the lokal minimizer Hessian is pos. def., we expect
            // the approximation to be pos. def. to, plus we get a descent direction.
            // Note: Since we use here duu, dvv, duv, this are in truth the approxed entries of the inverse of the hessian

            outrun = 0;
            stagnation = 0;

            while (gradnorm > epsilon)
            {
                // Neue Hesse-Approx. Diese Hängt ab vom Gradient in x_k und x_(k+1) und der Schrittweite, sowieso der vorherigen
                ap = new GeoPoint2D(steplength * p.x, steplength * p.y);
                y1 = du2 - du;
                y2 = dv2 - dv;
                temp1 = ap.x - (duu * y1 + duv * y2);
                temp2 = ap.y - (duv * y1 + dvv * y2);
                temp3 = temp1 * y1 + temp2 * y2;
                // Offensichtlich wird hier geteilt, die Zahl sollte eine gewisse Mindestgröße haben, p.145 in Numerical Optimization
                if (System.Math.Abs(temp3) >= (r * System.Math.Sqrt(y1 * y1 + y2 * y2) * System.Math.Sqrt(temp1 * temp1 + temp2 * temp2)))
                {
                    duu = duu + (temp1 * temp1) / temp3;
                    duv = duv + (temp1 * temp2) / temp3;
                    dvv = dvv + (temp2 * temp2) / temp3;

                    // Sicherstellen, dass der kleinere Eigenwert "groß" genug ist, d.h. größer d und insbes. positiv
                    l = (duu + dvv) / 2 - System.Math.Sqrt(System.Math.Pow((duu - dvv) / 2, 2) + duv * duv);
                    if (l <= 0)
                    {
                        t = -l + e;
                        duu = duu + t;
                        dvv = dvv + t;
                    }
                }
                // Bestimmung der neuen Richtung und des neuen Punktes und Verwerfung des alten
                du = du2;
                dv = dv2;
                x = xap;
                p = new GeoPoint2D(-(duu * du + duv * dv), -(duv * du + dvv * dv));
                if ((p.x * p.x + p.y * p.y) > plength)
                {
                    p = new GeoPoint2D(p.x * plength / System.Math.Sqrt(p.x * p.x + p.y * p.y), p.y * plength / System.Math.Sqrt(p.x * p.x + p.y * p.y));
                }

                if (!this.InterpolationStepLength())
                {
                    uv = new GeoPoint2D(steplength, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                // Falls die effektive Schrittweite 0 ist
                if (x == xap)
                {
                    uv = new GeoPoint2D(4, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                // Überprüft ob der neue Wert im geg. Definitonsbereich ist
                if (!Rect.Contains(xap))
                {
                    outrun++;
                }
                if (outrun > allowedoutruns)
                {
                    x = xap;
                    uv = new GeoPoint2D(2, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                // Bestimmt die Werte am neuen Punkt.
                surface.DerivationAt(xap, out loctemp, out dutemp, out dvtemp);
                loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
                du2 = direction * dutemp;
                dv2 = direction * dvtemp;
                gradnorm = System.Math.Sqrt(du2 * du2 + dv2 * dv2);

                if (!CheckChange())
                {
                    x = xap;
                    uv = new GeoPoint2D(3, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

            }
            if (!Rect.Contains(xap))
            {
                uv = new GeoPoint2D(7, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            uv = xap;
            value = loctemp;
            return true;
        }
        private bool StartSR1Line()
        {
            verfahren = '4';
            dir = new GeoVector2D(end.x - start.x, end.y - start.y).Normalized;

            surface.DerivationAt(x, out loctemp, out dutemp, out dvtemp);
            loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
            du = direction * dutemp;
            dv = direction * dvtemp;
            gradient1 = dir.x * du + dir.y * dv;

            // Testet, ob der Startpkt schon gut genug ist
            if (System.Math.Abs(gradient1) < epsilon)
            {
                uv = x;
                value = loctemp;
                return true;
            }

            p1D = -gradient1;

            p = new GeoPoint2D(-dir.x * gradient1, -dir.y * gradient1);

            if (!this.InterpolationStepLength())
            {
                uv = new GeoPoint2D(steplength, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }


            surface.DerivationAt(xap, out loctemp, out dutemp, out dvtemp);
            du2 = direction * dutemp;
            dv2 = direction * dvtemp;
            gradient2 = dir.x * du2 + dir.y * dv2;

            // Testet, ob der Punkt vielleicht schon gut genug ist
            if (System.Math.Abs(gradient2) < epsilon)
            {
                uv = xap;
                value = loctemp;
                return true;
            }

            hesse = steplength * p1D;

            // Anpassung der Eigenwerte auf 0 < l < e
            if (hesse < 0)
            {
                hesse = e;
            }
            else
            {
                hesse = System.Math.Min(hesse, e);
            }


            p1D = -hesse * gradient1;
            p = new GeoPoint2D(dir.x * p1D, dir.y * p1D);

            if (!this.InterpolationStepLength())
            {
                uv = new GeoPoint2D(steplength, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }

            // Obiges diente soweit dazu, eine ganz gute erste Annäherung zu finden. Genaueres steht im SR1Rectangle

            surface.DerivationAt(xap, out loctemp, out dutemp, out dvtemp);
            loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
            du2 = direction * dutemp;
            dv2 = direction * dvtemp;
            gradient2 = dir.x * du2 + dir.y * dv2;

            gradnorm = System.Math.Abs(gradient2);

            return SR1Line();
        }
        private bool SR1Line()
        {

            // SR1 or SSR1 namely Symmetric (Scaled) Rank One is Quasi-Newton-Method
            // For further Information, see Nocedal, Wright: Numerical Optimization, p. 144 ff.
            // and Hassan, Mansor, June: Convergence of a Positive-Definit Symmetric Scaled Rank One Method, in
            // Matematika, 2002, Jilid 18, bil. 2
            // Note, we use some different method: If the approxed Hessian isn't positive definit, we enforce this by Eigenvaluemodification
            // Thus we have still a descent direction and the above Paper should be applicable. Since nearly the lokal minimizer Hessian is pos. def., we expect
            // the approximation to be pos. def. to, plus we get a descent direction.
            // Note: hesse is in truth the approximation of the inverse of the hessian

            outrun = 0;
            stagnation = 0;

            while (gradnorm > epsilon)
            {
                // Neue Hesseapproximation, siehe SR1Rectangle    
                y1 = gradient2 - gradient1;
                temp1 = (steplength * p1D) - (hesse * y1);
                temp3 = temp1 * y1;
                // Offensichtlich wird nun geteilt, der Nenner sollte eine gewinne Mindesgröße besitzen.
                // Tatsächlich sollte temp3 aber nicht zu klein sein, da aber die Eigenwerte durch e beschränkt sind, darf temp3 auch nahezu 0 sein.
                if (temp3 != 0)
                {
                    // SR1-Update
                    hesse = hesse + temp1 * temp1 / temp3;

                    // Anpassung Eigenwert auf 0 < l < e
                    if (hesse < 0)
                    {
                        hesse = e;
                    }
                    else
                    {
                        hesse = System.Math.Min(hesse, e);
                    }
                }
                du = du2;
                dv = dv2;
                x = xap;
                gradient1 = gradient2;
                p1D = -hesse * gradient2;
                if (p1D > plength)
                {
                    p1D = plength * System.Math.Sign(p1D);
                }
                p = new GeoPoint2D(dir.x * p1D, dir.y * p1D);

                if (!this.InterpolationStepLength())
                {
                    uv = new GeoPoint2D(steplength, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                // Wird de facto kein Fortschritt erziehlt, so beenden wir den Algorithmus
                if (x == xap)
                {
                    uv = new GeoPoint2D(4, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                // Guckt, ob wir unseren vorg. Bereich verlassen haben, sollte nicht zu oft vorkommen.
                if (!Rect.Contains(xap))
                {
                    outrun++;
                }
                if (outrun > allowedoutruns)
                {
                    x = xap;
                    uv = new GeoPoint2D(2, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }

                surface.DerivationAt(xap, out loctemp, out dutemp, out dvtemp);
                loc = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
                du2 = direction * dutemp;
                dv2 = direction * dvtemp;
                gradient2 = dir.x * du2 + dir.y * dv2;
                gradnorm = System.Math.Abs(gradient2);

                if (!CheckChange())
                {
                    x = xap;
                    uv = new GeoPoint2D(3, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }
            }
            if (!Rect.Contains(xap))
            {
                uv = new GeoPoint2D(7, 0);
                value = new GeoPoint(0, 0, 0);
                return false;
            }
            uv = xap;
            value = loctemp;
            return true;
        }

        // Schrittweitenbestimmung
        // Diese Methode setzt die Schrittweite und berechnet den Punkt xap als x+steplength*p
        private bool InterpolationStepLength()
        {
            // Numerical Optimisation, Nocedal & Wright, p. 57 ff.
            // a is the start value, typically 1. Aim is, to satisfy Armijo-Condition (see p. 33)
            // x is the actual point and p the choosen direction

            GeoPoint loctemp;

            double locx = loc;
            double abl = p.x * du + p.y * dv; // Ableitung von \Phi, geg. als Richtungsabl. des Surface in Richtung p
            double a1, a2;

            // Stellt sicher, dass die Schrittweite nicht aus dem Defintionsbereich des Surface hinausgeht.
            // Die Methode domaincheck und Newparam setzen xap und steplength
            // Die Methode setzt auch den Fehlercode der auftreten kann, dies geschieht durch setzen von steplength, dieses wird von der diese Methode aufrufenden Methode in uv.x gesetzt
            this.Newparam(a);
            if (!this.domainCheck(20))
            {
                return false;
            }
            a1 = steplength;

            loctemp = surface.PointAt(xap);
            double locxa1p = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;

            // Testet ob geg. a schon gut genug ist.
            if (locxa1p <= (locx + c * a1 * abl))
            {
                return true;
            }

            // Quadratische Interpolation, zur Bestimmung einer besseren Schrittweite
            if ((locxa1p - locx - abl * a1) == 0)
            {
                a2 = -(abl * a1 * a1) / (2 * (locxa1p - locx - abl * (a1 + (1E-010))));
            }
            else
            {
                a2 = -(abl * a1 * a1) / (2 * (locxa1p - locx - abl * a1));
            }

            // Stellt sicher, dass die Schrittweite nicht aus dem Defintionsbereich des Surface hinausgeht.
            this.Newparam(a2);
            if (!this.domainCheck(20))
            {
                return false;
            }
            a2 = this.steplength;

            loctemp = surface.PointAt(xap);
            double locxa2p = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
            if (locxa2p <= (locx + c * a2 * abl))
            {
                steplength = a2;
                return true;
            }
            double s, t, a1swap;

            // Ist die Schrittweite immernoch zu groß, so wird kubisch interpoliert, dies geschieht iterativ, bis Wolfe-Bed. erfüllt ist.
            do
            {
                // Berechnet neuen zu prüf. Wert und überschreibt die ungenutzen Var. für den nächsten Schritt
                s = (locxa2p - locx - abl * a2) / (a2 * a2 * (a2 - a1)) - (locxa1p - locx - abl * a1) / (a1 * a1 * (a2 - a1));
                // Es sollte noch dafür gesorgt werden, dass s nicht 0 wird. Idee warum das sein kann fehlt zZ.
                t = -a1 * (locxa1p - locx - abl * a2) / (a2 * a2 * (a2 - a1)) + a2 * (locxa1p - locx - abl * a1) / (a1 * a1 * (a2 - a1));
                a1swap = a1;
                a1 = a2;
                locxa1p = locxa2p;
                a2 = ((-t + System.Math.Sqrt(t * t - 3 * s * abl)) / (3 * s));

                // Stellt sicher, dass die Schrittweite nicht aus dem Defintionsbereich des Surface hinausgeht.
                this.Newparam(a2);
                if (!this.domainCheck(20))
                {
                    return false;
                }
                a2 = this.steplength;

                loctemp = surface.PointAt(xap);
                locxa2p = direction.x * loctemp.x + direction.y * loctemp.y + direction.z * loctemp.z;
            } while (locxa2p > (locx + c * a2 * abl));

            return true;
        }

        // If the gradient isn't to big this function sets new domain, if this domain is in direction bigger, it returns true, else false
        // Is the gradient to big, we return false and conclude, there's no local minimizer on Rect
        private bool Stretchdomain()
        {
            if (gradnorm <= epsilon * tolerancefactor)
            {
                temp1 = DistanceToBorder(ref domain);
                domain = BoundingRect.Common(surfacedomain, domain * domainstretchfactor); // Hier könnte noch Feintuning hin!
                // Wir überprüfen hiermit, ob die neue domain in Richtung p größer wurde. Sollte dies nicht der Fall sein, brechen wir ab.
                if (temp1 == DistanceToBorder(ref domain))
                {
                    uv = new GeoPoint2D(1, 0);
                    value = new GeoPoint(0, 0, 0);
                    return false;
                }
                return true;
            }
            uv = new GeoPoint2D(8, 0);
            value = new GeoPoint(0, 0, 0);
            return false;
        }


        // Überprüft Definitionsbereiche
        private void Newparam(double a)
        {
            this.steplength = a;
            this.xap = new GeoPoint2D(x.x + a * p.x, x.y + a * p.y);
        } // Neue Schrittweite
        private bool domainCheck(int n)
        {
            if (this.steplength == 0)
            {
                return false;
            }
            for (int i = 0; i <= n; i++)
            {
                if (domain.Contains(xap))
                {
                    return true;
                }
                else
                {
                    this.steplength = steplength / 2;
                    this.xap = new GeoPoint2D(x.x + steplength * p.x, x.y + steplength * p.y);
                }
            }
            if (domain.Contains(xap))
            {
                return true;
            }
            this.steplength = 1;
            return false;
        }

        // Hilfsmittel um mangelnde Konvergenz auszusperren
        private bool CheckChange()
        {
            if (lastgradnorm * smallchangefactor <= gradnorm)
            {
                stagnation++;
                if (stagnation > smallchange)
                {
                    return false;
                }
            }
            lastgradnorm = gradnorm;
            return true;
        }

    }

    // TODO: implement IPropertyEntry with GetPropertyEntry and protected virtual methods, remove IShowPropertyImpl
    /// <summary>
    /// Internal helper class for <see cref="ISurface"/> implementation.
    /// </summary>
    public abstract class ISurfaceImpl : ISurface, IOctTreeInsertable
    {
        protected GeoPoint2D[] extrema; // Achtung, muss bei Modify auf null gesetzt werden
        internal BoxedSurface boxedSurface;
        internal BoundingRect usedArea = BoundingRect.EmptyBoundingRect;
        internal BoxedSurfaceEx boxedSurfaceEx;
        internal virtual BoxedSurfaceEx BoxedSurfaceEx
        {
            get
            {
                if (boxedSurfaceEx == null)
                {
                    BoundingRect ext = new BoundingRect();
                    GetNaturalBounds(out ext.Left, out ext.Right, out ext.Bottom, out ext.Top);
                    if (this is NurbsSurface)
                    {   // make sure not to exceed bound of a NURBS surface
                        NurbsSurface ns = (this as NurbsSurface);
                        if (!ns.IsUPeriodic)
                        {
                            ext.Left = Math.Max(ns.UKnots[0], ext.Left);
                            ext.Right = Math.Min(ns.UKnots[ns.UKnots.Length - 1], ext.Right);
                        }
                        if (!ns.IsVPeriodic)
                        {
                            ext.Bottom = Math.Max(ns.VKnots[0], ext.Bottom);
                            ext.Top = Math.Min(ns.VKnots[ns.VKnots.Length - 1], ext.Top);
                        }
                    }
                    else if (this is NonPeriodicSurface)
                    {
                        if (!usedArea.IsEmpty() && (ext.IsInfinite || ext.IsEmpty() || ext.IsInvalid())) ext = usedArea;
                    }
                    else if (usedArea != BoundingRect.EmptyBoundingRect)
                    {
                        // the usedArea can differ from natural bounds in periodic cases: we may not restrict to 0..2*pi!
                        //if (!ext.IsEmpty()) ext = BoundingRect.Intersect(ext, usedArea * 1.01);
                        ext = usedArea * 1.01;
                        // make it slightly bigger. This is often the extent of the Face.Area, which is sometimes not very accurate
                        // and it makes problems with Intersect
                    }
                    if (ext.IsInfinite) ext = usedArea * 1.01;
                    boxedSurfaceEx = new BoxedSurfaceEx(this, ext); // removed ext*1.01, because NURBS surfaces are not well defined outside of their bounds

                }
                return boxedSurfaceEx;
            }
        }


#if DEBUG
        virtual public GeoObjectList DebugGrid
        {
            get
            {
                double umin = usedArea.Left;
                double umax = usedArea.Right;
                double vmin = usedArea.Bottom;
                double vmax = usedArea.Top;
                if (usedArea == BoundingRect.EmptyBoundingRect)
                {
                    GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                }
                if (umin == double.MinValue)
                {
                    if (IsUPeriodic)
                    {
                        umin = 0;
                        umax = UPeriod;
                    }
                    else
                    {
                        umin = 0;
                        umax = 100;
                    }
                }
                if (vmin == double.MinValue)
                {
                    if (IsVPeriodic)
                    {
                        vmin = 0;
                        vmax = VPeriod;
                    }
                    else
                    {
                        vmin = 0;
                        vmax = 100;
                    }

                }
                GeoObjectList res = new GeoObjectList();
                int n = 25;
                for (int i = 0; i <= n; i++)
                {   // über die Diagonale
                    GeoPoint[] pu = new GeoPoint[n + 1];
                    GeoPoint[] pv = new GeoPoint[n + 1];
                    for (int j = 0; j <= n; j++)
                    {
                        pu[j] = PointAt(new GeoPoint2D(umin + j * (umax - umin) / n, vmin + i * (vmax - vmin) / n));
                        pv[j] = PointAt(new GeoPoint2D(umin + i * (umax - umin) / n, vmin + j * (vmax - vmin) / n));
                    }
                    try
                    {
                        Polyline plu = Polyline.Construct();
                        plu.SetPoints(pu, false);
                        res.Add(plu);
                    }
                    catch (PolylineException)
                    {   // ein Pol!
                        Point pntu = Point.Construct();
                        pntu.Location = pu[0];
                        pntu.Symbol = PointSymbol.Cross;
                        res.Add(pntu);
                    }
                    try
                    {
                        Polyline plv = Polyline.Construct();
                        plv.SetPoints(pv, false);
                        res.Add(plv);
                    }
                    catch (PolylineException)
                    {
                        Point pntv = Point.Construct();
                        pntv.Location = pv[0];
                        pntv.Symbol = PointSymbol.Cross;
                        res.Add(pntv);
                    }
                }
                GeoPoint2D c2d = new GeoPoint2D((umax + umin) / 2, (vmax + vmin) / 2);
                GeoPoint c3d = PointAt(c2d);
                Line centerNormal = Line.TwoPoints(c3d, c3d + res.GetExtent().Size * 0.1 * GetNormal(c2d));
                res.Add(centerNormal);
                return res;
            }
        }
        virtual public GeoObjectList DebugDirectionsGrid
        {
            get
            {
                double umin = usedArea.Left;
                double umax = usedArea.Right;
                double vmin = usedArea.Bottom;
                double vmax = usedArea.Top;
                if (usedArea == BoundingRect.EmptyBoundingRect)
                {
                    GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                }
                if (umin == double.MinValue)
                {
                    if (IsUPeriodic)
                    {
                        umin = 0;
                        umax = UPeriod;
                    }
                    else
                    {
                        umin = 0;
                        umax = 100;
                    }
                }
                if (vmin == double.MinValue)
                {
                    if (IsVPeriodic)
                    {
                        vmin = 0;
                        vmax = VPeriod;
                    }
                    else
                    {
                        vmin = 0;
                        vmax = 100;
                    }

                }
                GeoObjectList res = new GeoObjectList();
                int n = 25;
                double length = 0.0;
                for (int i = 0; i <= n; i++)
                {   // über die Diagonale
                    GeoPoint[] pu = new GeoPoint[n + 1];
                    GeoPoint[] pv = new GeoPoint[n + 1];
                    for (int j = 0; j <= n; j++)
                    {
                        pu[j] = PointAt(new GeoPoint2D(umin + j * (umax - umin) / n, vmin + i * (vmax - vmin) / n));
                        pv[j] = PointAt(new GeoPoint2D(umin + i * (umax - umin) / n, vmin + j * (vmax - vmin) / n));
                    }
                    try
                    {
                        Polyline plu = Polyline.Construct();
                        plu.SetPoints(pu, false);
                        length += plu.Length;
                    }
                    catch (PolylineException)
                    {   // ein Pol!
                    }
                    try
                    {
                        Polyline plv = Polyline.Construct();
                        plv.SetPoints(pv, false);
                        length += plv.Length;
                    }
                    catch (PolylineException)
                    {
                    }
                }
                length /= 50.0; // durchschnittliche Länge einer linie
                length /= 25.0; // durchschnittliche Maschengröße
                Attribute.ColorDef cdu = new Attribute.ColorDef("diru", Color.Red);
                Attribute.ColorDef cdv = new Attribute.ColorDef("dirv", Color.Green);
                for (int i = 0; i <= n; i++)
                {
                    for (int j = 0; j <= n; j++)
                    {
                        GeoVector diru = UDirection(new GeoPoint2D(umin + j * (umax - umin) / n, vmin + i * (vmax - vmin) / n));
                        GeoVector dirv = VDirection(new GeoPoint2D(umin + j * (umax - umin) / n, vmin + i * (vmax - vmin) / n));
                        GeoPoint loc = PointAt(new GeoPoint2D(umin + j * (umax - umin) / n, vmin + i * (vmax - vmin) / n));
                        Line l1 = Line.TwoPoints(loc, loc + length * diru.Normalized);
                        l1.ColorDef = cdu;
                        Line l2 = Line.TwoPoints(loc, loc + length * dirv.Normalized);
                        l2.ColorDef = cdv;
                        res.Add(l1);
                        res.Add(l2);
                    }
                }

                return res;
            }
        }
        virtual public Face DebugAsFace
        {
            get
            {
                BoundingRect ext = usedArea;
                if (ext == BoundingRect.EmptyBoundingRect)
                {
                    GetNaturalBounds(out ext.Left, out ext.Right, out ext.Bottom, out ext.Top);
                    if (IsUPeriodic)
                    {
                        ext.Left = 0;
                        ext.Right = UPeriod;
                    }
                    else
                    {
                        ext.Left = 0;
                        ext.Right = 100;
                    }
                    if (IsVPeriodic)
                    {
                        ext.Bottom = 0;
                        ext.Top = VPeriod;
                    }
                    else
                    {
                        ext.Bottom = 0;
                        ext.Top = 100;
                    }

                }
                return Face.MakeFace(this, new CADability.Shapes.SimpleShape(ext));
            }
        }
        static int idcounter = 0;
        public int uniqueid;
#endif
        protected ISurfaceImpl(BoundingRect? usedArea = null)
        {
#if DEBUG
            uniqueid = idcounter++;
#endif
            if (usedArea.HasValue) this.usedArea = usedArea.Value;
        }
        protected void InvalidateSecondaryData()
        {
            extrema = null;
            boxedSurface = null;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.FixedU (double, double, double)"/>
        /// </summary>
        /// <param name="u"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public abstract ICurve FixedU(double u, double vmin, double vmax);
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.FixedV (double, double, double)"/>
        /// </summary>
        /// <param name="u"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <returns></returns>
        public abstract ICurve FixedV(double u, double umin, double umax);
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetModified (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public abstract ISurface GetModified(ModOp m);
        //{
        //    throw new ApplicationException("GetModified must be implemented");
        //}
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.Make3dCurve (ICurve2D)"/>
        /// </summary>
        /// <param name="curve2d"></param>
        /// <returns></returns>
        public virtual ICurve Make3dCurve(CADability.Curve2D.ICurve2D curve2d)
        {
            if (curve2d is Curve2DAspect)
            {
                ICurve res = (curve2d as Curve2DAspect).Get3DCurve(this);
                if (res != null) return res;
            }
            if (curve2d is BSpline2D)
            {   // selber machen OCas liefert manchmal Unsinn
                BSpline res = BSpline.Construct();
                List<GeoPoint> points = new List<GeoPoint>();
                List<double> pos = new List<double>();
                BSpline2D b2d = curve2d as BSpline2D;
                double[] knots = b2d.Knots;
                for (int i = 0; i < knots.Length; ++i)
                {
                    GeoPoint2D uv = b2d.PointAtParam(knots[i]);
                    pos.Add(knots[i]);
                    points.Add((this as ISurface).PointAt(uv));
                }
                bool closed = Precision.IsEqual(points[0], points[points.Count - 1]);
                // bool ok = res.ThroughPoints(points.ToArray(), Math.Max(3, b2d.Degree), closed);
                bool ok = res.ThroughPoints(points.ToArray(), 3, closed);
                // TODO: hier noch mit Genauigkeit iterieren...
                if (ok)
                {
                    return res;
                }
                else
                {
                    Line l = Line.Construct();
                    l.SetTwoPoints((this as ISurface).PointAt(b2d.StartPoint), (this as ISurface).PointAt(b2d.EndPoint));
                    return l;
                }
            }
            else if (curve2d is InterpolatedDualSurfaceCurve.ProjectedCurve)
            {
                InterpolatedDualSurfaceCurve.ProjectedCurve pc = curve2d as InterpolatedDualSurfaceCurve.ProjectedCurve;
                if ((pc.IsOnSurface1 && pc.Curve3D.Surface1 == this) || (!pc.IsOnSurface1 && pc.Curve3D.Surface2 == this))
                {
                    // es kann sich nur um die ganze Curve3D handeln oder einen Teil davon
                    double pos1 = pc.Curve3D.PositionOf(PointAt(pc.StartPoint));
                    double pos2 = pc.Curve3D.PositionOf(PointAt(pc.EndPoint));
                    if (pos1 >= 0.0 && pos1 <= 1.0 && pos2 >= 0.0 && pos2 <= 1.0)
                    {
                        bool reversed = false;
                        if (pos2 < pos1)
                        {
                            reversed = true;
                            double tmp = pos1;
                            pos1 = pos2;
                            pos2 = tmp;
                        }
                        ICurve res = pc.Curve3D.Clone() as ICurve;
                        res.Trim(pos1, pos2);
                        if (reversed) res.Reverse();
                        return res;
                    }
                }
            }
            // kein else, sondern das folgende ist der Notfall, wenn sonst nichts greift
            {
                if (curve2d is Line2D) // dieser Text könnte eigentlich in der Basismethode stehen
                {
                    if (Math.Abs(curve2d.StartDirection.x) < Precision.eps)
                    {
                        return FixedU(curve2d.StartPoint.x, curve2d.StartPoint.y, curve2d.EndPoint.y);
                    }
                    else if (Math.Abs(curve2d.StartDirection.y) < Precision.eps)
                    {
                        return FixedV(curve2d.StartPoint.y, curve2d.StartPoint.x, curve2d.EndPoint.x);
                    }
                }

                // hier brachial mit einer gewissen Anzahl von Punkten
                int n = 10; // einfach mal so, muss man ggf. ändern
                GeoPoint[] pnts = new GeoPoint[n + 1];

                for (int i = 0; i < n + 1; i++)
                {
                    pnts[i] = (this as ISurface).PointAt(curve2d.PointAt((double)i / (double)n));
                }
                bool closed = Precision.IsEqual(pnts[0], pnts[pnts.Length - 1]);
                BSpline res = BSpline.Construct();
                bool ok = res.ThroughPoints(pnts, 3, closed);
                // TODO: hier noch mit Genauigkeit iterieren...
                if (ok)
                {
                    return res;
                }
                else
                {
                    Line l = Line.Construct();
                    l.SetTwoPoints((this as ISurface).PointAt(curve2d.StartPoint), (this as ISurface).PointAt(curve2d.EndPoint));
                    return l;
                }
            }
            //CndHlp3D.Surface sf = Helper;
            //GeneralCurve2D g2d = curve2d as GeneralCurve2D;
            //CndHlp3D.Edge edge = sf.Make3DCurve(g2d.Entity2D);
            //return IGeoObjectImpl.FromHlp3DEdge(edge) as ICurve;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetNormal (GeoPoint2D)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <returns></returns>
        public virtual GeoVector GetNormal(GeoPoint2D uv)
        {
            return (this as ISurface).UDirection(uv) ^ (this as ISurface).VDirection(uv);
            // return new GeoVector(Helper.GetNormal(uv.ToCndHlp()));
        }
        public abstract GeoVector UDirection(GeoPoint2D uv);
        public abstract GeoVector VDirection(GeoPoint2D uv);
        public abstract GeoPoint PointAt(GeoPoint2D uv);
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual GeoPoint2D PositionOf(GeoPoint p)
        {
            GeoPoint2D res;
            if (BoxedSurfaceEx.PositionOf(p, out res))
            {
                return res;
            }
            else
            {

                // hat nicht konvergiert, der Punkt liegt möglicherweise
                // knapp außerhalb der NaturalBounds
                double umin, umax, vmin, vmax;
                GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                double[] us = GetUSingularities();
                double[] vs = GetVSingularities();
                double minerror = double.MaxValue;
                res = GeoPoint2D.Origin;
                for (int i = 0; i < us.Length; i++)
                {
                    GeoPoint pole = PointAt(new GeoPoint2D(us[i], (vmax + vmin) / 2.0));
                    double d = pole | p;
                    if (d < minerror)
                    {
                        minerror = d;
                        res = new GeoPoint2D(us[i], (vmax + vmin) / 2.0);
                    }
                }
                for (int i = 0; i < vs.Length; i++)
                {
                    GeoPoint pole = PointAt(new GeoPoint2D((umax + umin) / 2.0, vs[i]));
                    double d = pole | p;
                    if (d < minerror)
                    {
                        minerror = d;
                        res = new GeoPoint2D((umax + umin) / 2.0, vs[i]);
                    }
                }
                if (umin != double.MinValue && vmin != double.MinValue) // das sollte als Gültigkeitsabfrage genügen
                {
                    double u, v, d;
                    GeoPoint2D p2d;
                    GeoPoint p0;
                    if (us.Length == 0 || Math.Abs(us[0] - umin) > 1e-6) // not a pole
                    {
                        v = FixedU(umin, vmin, vmax).PositionOf(p);
                        v = vmin + v * (vmax - vmin);
                        p2d = new GeoPoint2D(umin, v);
                        p0 = (this as ISurface).PointAt(p2d);
                        d = p0 | p;
                        if (d < minerror)
                        {
                            minerror = d;
                            res = p2d;
                        }
                    }
                    if (us.Length == 0 || Math.Abs(us[us.Length - 1] - umax) > 1e-6) // not a pole
                    {
                        v = FixedU(umax, vmin, vmax).PositionOf(p);
                        v = vmin + v * (vmax - vmin);
                        p2d = new GeoPoint2D(umax, v);
                        p0 = (this as ISurface).PointAt(p2d);
                        d = p0 | p;
                        if (d < minerror)
                        {
                            minerror = d;
                            res = p2d;
                        }
                    }
                    if (vs.Length == 0 || Math.Abs(vs[0] - vmin) > 1e-6) // not a pole
                    {
                        u = FixedV(vmin, umin, umax).PositionOf(p);
                        u = umin + u * (umax - umin);
                        p2d = new GeoPoint2D(u, vmin);
                        p0 = (this as ISurface).PointAt(p2d);
                        d = p0 | p;
                        if (d < minerror)
                        {
                            minerror = d;
                            res = p2d;
                        }
                    }
                    if (vs.Length == 0 || Math.Abs(vs[vs.Length - 1] - vmax) > 1e-6) // not a pole
                    {
                        u = FixedV(vmax, umin, umax).PositionOf(p);
                        u = umin + u * (umax - umin);
                        p2d = new GeoPoint2D(u, vmax);
                        p0 = (this as ISurface).PointAt(p2d);
                        d = p0 | p;
                        if (d < minerror)
                        {
                            minerror = d;
                            res = p2d;
                        }
                    }
                    // hier liegt res auf einer Kante. In diesem Punkt legen wir jetzt die Tangentialebene an
                    // und bestimmen den Punkt auf dieser Ebene. Sonst kleben die Punkte außerhalb immer an der
                    // Kante und das ist schlecht für GetProjectedCurve
                    GeoVector dirx;
                    GeoVector diry;
                    GeoPoint loc;
                    this.DerivationAt(res, out loc, out dirx, out diry);
                    Matrix mtx = DenseMatrix.OfRowArrays(dirx, diry, dirx ^ diry);
                    Vector b = new DenseVector(p - loc);
                    if (!Precision.IsNullVector(dirx) && !Precision.IsNullVector(diry))
                    {
                        Vector x = (Vector)mtx.Transpose().Solve(b);
                        if (x.IsValid())
                        {
                            GeoPoint2D res1 = new GeoPoint2D(res.x + x[0], res.y + x[1]);
                            double du = umax - umin;
                            double dv = vmax - vmin;
                            if (res1.x >= umin - du / 2.0 && res1.x <= umax + du / 2.0 && res1.y >= vmin - dv / 2.0 && res1.y <= vmax + dv / 2.0) res = res1;
                            // res = res1;
                            // ACHTUNG: den Punkt nicht künstlich in die Grenzen drücken aber nicht zu weit entfernt
                        }
                    }
                }
                return res;
            }
            // return new GeoPoint2D(Helper.PositionOf(p.ToCndHlp()));
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.DerivationAt (GeoPoint2D, out GeoPoint, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <param name="location"></param>
        /// <param name="du"></param>
        /// <param name="dv"></param>
        public virtual void DerivationAt(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv)
        {
            location = (this as ISurface).PointAt(uv);
            du = (this as ISurface).UDirection(uv);
            dv = (this as ISurface).VDirection(uv);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.Derivation2At (GeoPoint2D, out GeoPoint, out GeoVector, out GeoVector, out GeoVector, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="uv"></param>
        /// <param name="location"></param>
        /// <param name="du"></param>
        /// <param name="dv"></param>
        /// <param name="duu"></param>
        /// <param name="dvv"></param>
        /// <param name="duv"></param>
        public virtual void Derivation2At(GeoPoint2D uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv)
        {
            throw new NotImplementedException("Derivation2At must be implemented");
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetPlaneIntersection (PlaneSurface, double, double, double, double, double)"/>
        /// </summary>
        /// <param name="pl"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public virtual IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision)
        {
            return BoxedSurfaceEx.GetPlaneIntersection(pl, umin, umax, vmin, vmax, precision);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetLineIntersection (GeoPoint, GeoVector)"/>
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="direction"></param>
        /// <returns></returns>
        public virtual GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            Polynom impl = GetImplicitPolynomial();
            if (impl != null)
            {
                List<GeoPoint2D> res = new List<GeoPoint2D>();
                Polynom toSolve = impl.Substitute(new Polynom(direction.x, "u", startPoint.x, ""), new Polynom(direction.y, "u", startPoint.y, ""), new Polynom(direction.z, "u", startPoint.z, ""));
                double[] roots = toSolve.Roots();
                for (int i = 0; i < roots.Length; i++)
                {
                    GeoPoint p1 = startPoint + roots[i] * direction;
                    res.Add(PositionOf(p1));
                }
                return res.ToArray();
            }
            return BoxedSurfaceEx.GetLineIntersection(startPoint, direction);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetSafeParameterSteps (double, double, double, double, out double[], out double[])"/>
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="intu"></param>
        /// <param name="intv"></param>
        public virtual void GetSafeParameterSteps(double umin, double umax, double vmin, double vmax, out double[] intu, out double[] intv)
        {
            int n = 4;
            intu = new double[n];
            intv = new double[n];
            for (int i = 0; i < n; i++)
            {
                intu[i] = umin + i * (umax - umin) / (n - 1);
                intv[i] = vmin + i * (vmax - vmin) / (n - 1);
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetTangentCurves (GeoVector, double, double, double, double)"/>
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public virtual ICurve2D[] GetTangentCurves(GeoVector direction, double umin, double umax, double vmin, double vmax)
        {
            FindTangentCurves ftc = new FindTangentCurves(this);
            return ftc.GetTangentCurves(direction, umin, umax, vmin, vmax);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.IsVanishingProjection (Projection, double, double, double, double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public virtual bool IsVanishingProjection(Projection p, double umin, double umax, double vmin, double vmax)
        {
            return false;
        }
        public virtual bool IsUPeriodic
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        public virtual bool IsVPeriodic
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        public virtual double UPeriod
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        public virtual double VPeriod
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        public virtual bool IsUClosed
        {
            get
            {
                return IsUPeriodic;
            }
        }
        public virtual bool IsVClosed
        {
            get
            {
                return IsVPeriodic;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetUSingularities ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double[] GetUSingularities()
        {
            return new double[0];
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetVSingularities ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double[] GetVSingularities()
        {
            return new double[0];
        }

        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.MakeFace (SimpleShape)"/>
        /// </summary>
        /// <param name="simpleShape"></param>
        /// <returns></returns>
        public virtual Face MakeFace(CADability.Shapes.SimpleShape simpleShape)
        {
            Face res = Face.Construct();
            Edge[] outline = new Edge[simpleShape.Outline.Segments.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                outline[i] = new Edge(res, Make3dCurve(simpleShape.Outline.Segments[i]), res, simpleShape.Outline.Segments[i], true);
            }
            Edge[][] holes = new Edge[simpleShape.NumHoles][];
            for (int j = 0; j < holes.Length; j++)
            {
                holes[j] = new Edge[simpleShape.Hole(j).Segments.Length];
                for (int i = 0; i < holes[j].Length; i++)
                {
                    holes[j][i] = new Edge(res, Make3dCurve(simpleShape.Hole(j).Segments[i]), res, simpleShape.Hole(j).Segments[i], true);
                }
            }
            res.Set(this, outline, holes);
            return res;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetZMinMax (Projection, double, double, double, double, ref double, ref double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="zMin"></param>
        /// <param name="zMax"></param>
        public virtual void GetZMinMax(Projection p, double umin, double umax, double vmin, double vmax, ref double zMin, ref double zMax)
        {
            throw new NotImplementedException("GetZMinMax must be implemented by derived surface");
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.MakeCanonicalForm ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual ModOp2D MakeCanonicalForm()
        {
            return ModOp2D.Identity;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual ISurface Clone()
        {
            throw new NotImplementedException("Clone must be implemented by derived surface");
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public virtual void Modify(ModOp m)
        {
            throw new NotImplementedException("Modify must be implemented by derived surface");
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.CopyData (ISurface)"/>
        /// </summary>
        /// <param name="CopyFrom"></param>
        public virtual void CopyData(ISurface CopyFrom)
        {
            throw new NotImplementedException("CopyData must be implemented by derived surface");
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.Approximate (double, double, double, double, double)"/>
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public virtual NurbsSurface Approximate(double umin, double umax, double vmin, double vmax, double precision)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetProjectedCurve (ICurve, double)"/>
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public virtual ICurve2D GetProjectedCurve(ICurve curve, double precision)
        {   // ganz schlecht, unbedingt in die Objekte verlegen und StandardKurven erkennen
            if (curve is Polyline)
            {
                Polyline pl = curve as Polyline;
                List<ICurve2D> res = new List<ICurve2D>();
                GeoPoint2D lastEndPoint = GeoPoint2D.Invalid;
                for (int i = 0; i < pl.Vertices.Length - 1; i++)
                {
                    Line ln = Line.Construct();
                    ln.SetTwoPoints(pl.Vertices[i], pl.Vertices[i + 1]);
                    ICurve2D c2d = GetProjectedCurve(ln, precision);
                    if (c2d != null)
                    {
                        if (lastEndPoint.IsValid) SurfaceHelper.AdjustPeriodicStartPoint(this, lastEndPoint, c2d);
                        lastEndPoint = c2d.EndPoint;
                        res.Add(c2d);
                    }
                }
                return new Path2D(res.ToArray());
            }
            if (curve is InterpolatedDualSurfaceCurve)
            {
                if (this == (curve as InterpolatedDualSurfaceCurve).Surface1) // oder besser geometrische Gleichheit prüfen
                {
                    return (curve as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                }
                else if (this == (curve as InterpolatedDualSurfaceCurve).Surface2)
                {
                    return (curve as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                }
                // Test auf geometrische Gleichheit
                ModOp2D firstToSecond;
                if (this.SameGeometry(this.usedArea, (curve as InterpolatedDualSurfaceCurve).Surface1, ((curve as InterpolatedDualSurfaceCurve).Surface1 as ISurfaceImpl).usedArea, precision, out firstToSecond)) // oder besser geometrische Gleichheit prüfen
                {
                    if (firstToSecond.IsAlmostIdentity(precision)) return (curve as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                    else return (curve as InterpolatedDualSurfaceCurve).CurveOnSurface1.GetModified(firstToSecond); // ist die ModOp so richtigrum?
                }
                else if (this.SameGeometry(this.usedArea, (curve as InterpolatedDualSurfaceCurve).Surface2, ((curve as InterpolatedDualSurfaceCurve).Surface2 as ISurfaceImpl).usedArea, precision, out firstToSecond)) // oder besser geometrische Gleichheit prüfen
                {
                    if (firstToSecond.IsAlmostIdentity(precision)) return (curve as InterpolatedDualSurfaceCurve).CurveOnSurface2;
                    else return (curve as InterpolatedDualSurfaceCurve).CurveOnSurface2.GetModified(firstToSecond); // ist die ModOp so richtigrum?
                }
            }
            if (!IsUPeriodic && !IsVPeriodic)
            {
                BoundingRect restricted = BoundingRect.EmptyBoundingRect;
                if (usedArea.Left > double.MinValue && usedArea.Right < double.MaxValue && usedArea.Bottom > double.MinValue && usedArea.Top < double.MaxValue && !usedArea.IsEmpty())
                {
                    restricted = usedArea;
                }
                else
                {
                    GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
                    if (umin > double.MinValue && umax < double.MaxValue && vmin > double.MinValue && vmax < double.MaxValue) restricted = new BoundingRect(umin, vmin, umax, vmax);
                }
                return new ProjectedCurve(curve, this, true, restricted, precision);
            }
            if (!usedArea.IsInfinite)
                return new ProjectedCurve(curve, this, true, BoundingRect.EmptyBoundingRect, precision);
            else
                return new ProjectedCurve(curve, this, true, usedArea, precision);

            //Unreachable code
            /*
            int n = 16;
            bool ok = false;
            BSpline2D b2d = null;
            List<GeoPoint> poles = new List<GeoPoint>();
            List<GeoPoint2D> pole2d = new List<GeoPoint2D>();
            GeoPoint2D cnt2d;
            if (usedArea.IsInvalid())
            {
                GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax);
                cnt2d = new GeoPoint2D((umin + umax) / 2.0, (vmin + vmax) / 2.0);
            }
            else cnt2d = usedArea.GetCenter();
            double[] us = GetUSingularities();
            for (int i = 0; i < us.Length; i++)
            {
                GeoPoint pl = PointAt(new GeoPoint2D(us[i], cnt2d.y));
                poles.Add(pl);
                pole2d.Add(new GeoPoint2D(us[i], double.NaN));
            }
            double[] vs = GetVSingularities();
            for (int i = 0; i < vs.Length; i++)
            {
                GeoPoint pl = PointAt(new GeoPoint2D(cnt2d.x, vs[i]));
                poles.Add(pl);
                pole2d.Add(new GeoPoint2D(double.NaN, vs[i]));
            }
            double poleprec = curve.Length * 1e-3;
            while (!ok)
            {
                GeoPoint2D[] through = new GeoPoint2D[n + 1];
                GeoVector2D[] dirs = new GeoVector2D[n + 1];
                for (int i = 0; i < n; ++i)
                {
                    GeoPoint pi = curve.PointAt((double)i / (double)n);
                    bool closeToPole = false;
                    GeoPoint2D polePosition = GeoPoint2D.Origin;
                    for (int j = 0; j < poles.Count; j++)
                    {
                        if ((poles[j] | pi) < poleprec)
                        {
                            closeToPole = true;
                            polePosition = pole2d[j];
                            break;
                        }
                    }
                    through[i] = PositionOf(pi);
                    GeoVector dir3d = curve.DirectionAt((double)i / (double)n);
                    try
                    {
                        dirs[i] = Geometry.Dir2D(UDirection(through[i]), VDirection(through[i]), dir3d);
                    }
                    catch (ModOpException)
                    {
                        dirs[i] = GeoVector2D.NullVector;
                    }
                    if (closeToPole)
                    {
                        if (!double.IsNaN(polePosition.x)) through[i].x = polePosition.x;
                        if (!double.IsNaN(polePosition.y)) through[i].y = polePosition.y;
                    }
                }
                through[n] = PositionOf(curve.EndPoint);
                if (curve.IsClosed) through[n] = through[0];
#if DEBUG
                DebuggerContainer dcdirs = new DebuggerContainer();
                GeoPoint[] p3d = new GeoPoint[through.Length];
                GeoPoint[] op3d = new GeoPoint[through.Length];
                for (int i = 0; i < p3d.Length; i++)
                {
                    op3d[i] = curve.PointAt((double)i / (double)n);
                    p3d[i] = PointAt(through[i]);
                }
                Polyline dbgpl = Polyline.Construct();
                if (!Precision.IsEqual(p3d))
                {
                    dbgpl.SetPoints(p3d, false);
                    dcdirs.Add(dbgpl);
                }
                for (int i = 0; i < through.Length - 1; i++)
                {
                    if (!dirs[i].IsNullVector())
                    {
                        Line2D l2d = new Line2D(through[i], through[i] + 10 * dirs[i].Normalized);
                        dcdirs.Add(l2d);
                    }
                }
#endif
                // if the 3d curve starts or ends at a pole (singularity) we must adapt the u or v value
                us = GetUSingularities();
                for (int i = 0; i < us.Length; i++)
                {
                    if (Math.Abs(through[0].x - us[i]) < 1e-5)
                    {
                        through[0].y = through[1].y;
                    }
                    if (Math.Abs(through[n].x - us[i]) < 1e-5)
                    {
                        through[n].y = through[n - 1].y;
                    }
                }
                vs = GetVSingularities();
                for (int i = 0; i < vs.Length; i++)
                {
                    if (Math.Abs(through[0].y - vs[i]) < 1e-5)
                    {
                        through[0].x = through[1].x;
                    }
                    if (Math.Abs(through[n].y - vs[i]) < 1e-5)
                    {
                        through[n].x = through[n - 1].x;
                    }
                }
                if ((this as ISurface).IsUPeriodic)
                {
                    for (int i = 0; i < through.Length - 1; i++)
                    {
                        while (Math.Abs(through[i].x - through[i + 1].x) > Math.Abs(through[i].x - (through[i + 1].x - (this as ISurface).UPeriod)))
                        {
                            through[i + 1].x -= (this as ISurface).UPeriod;
                        }
                        while (Math.Abs(through[i].x - through[i + 1].x) > Math.Abs(through[i].x - (through[i + 1].x + (this as ISurface).UPeriod)))
                        {
                            through[i + 1].x += (this as ISurface).UPeriod;
                        }
                    }
                }
                if ((this as ISurface).IsVPeriodic)
                {
                    for (int i = 0; i < through.Length - 1; i++)
                    {
                        while (Math.Abs(through[i].y - through[i + 1].y) > Math.Abs(through[i].y - (through[i + 1].y - (this as ISurface).VPeriod)))
                        {
                            through[i + 1].y -= (this as ISurface).VPeriod;
                        }
                        while (Math.Abs(through[i].y - through[i + 1].y) > Math.Abs(through[i].y - (through[i + 1].y + (this as ISurface).VPeriod)))
                        {
                            through[i + 1].y += (this as ISurface).VPeriod;
                        }
                    }
                }
                BoundingRect text = new BoundingRect(through);
                try
                {
                    b2d = new BSpline2D(through, 3, Precision.IsEqual(through[0], through[through.Length - 1])); //  curve.IsClosed);
                }
                catch (NurbsException)
                {
                    List<GeoPoint2D> cleanThroughPoints = new List<GeoPoint2D>();
                    cleanThroughPoints.Add(through[0]);
                    double len = 0.0;
                    for (int i = 1; i < through.Length; i++) len += through[i] | through[i - 1];
                    for (int i = 1; i < through.Length - 1; i++)
                    {
                        if ((through[i] | cleanThroughPoints[cleanThroughPoints.Count - 1]) > len * 1e-4) cleanThroughPoints.Add(through[i]);
                    }
                    if ((through[through.Length - 1] | cleanThroughPoints[cleanThroughPoints.Count - 1]) > len * 1e-4) cleanThroughPoints.Add(through[through.Length - 1]);
                    else cleanThroughPoints[cleanThroughPoints.Count - 1] = through[through.Length - 1];
                    b2d = new BSpline2D(cleanThroughPoints.ToArray(), 3, Precision.IsEqual(through[0], through[through.Length - 1])); //  curve.IsClosed);
                }
                ok = true;
                for (int i = 0; i < n; ++i)
                {
                    if (precision > 0.0)
                    {
                        GeoPoint2D pb = b2d.PointAt((i + 0.5) / (double)n);
                        GeoPoint2D po = PositionOf(curve.PointAt((i + 0.5) / (double)n));
                        SurfaceHelper.AdjustPeriodic(this, text, ref po);
                        if ((pb | po) > precision)
                        {
                            double d0 = b2d.Distance(po); // maybe curve and b2d are not running synchronously in their parameters, then this test may still verify precision
                            if (d0 > precision)
                            {
                                ok = false;
                                break;
                            }
                        }
                    }
                    if (!dirs[i].IsNullVector())
                    {
                        SweepAngle sw = new SweepAngle(through[i + 1] - through[i], dirs[i]);
                        if (Math.Abs(sw.Radian) > 0.2) // about 10° deviation
                        {   // this is the case e.g. a spiral winding on a cylindrical surface and all points are in a line, but actually on a different winding
                            ok = false;
                            break;
                        }
                    }
                }
                //if (ok)
                //{   // maybe we need a criterion which says whether we accept self intersections or not
                //    double[] si = b2d.GetSelfIntersections();
                //    if (si.Length > 0) ok = false;
                //}
                n *= 2;
                if (ok || n > 1024) break;
            }
            return b2d;
            */
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.Intersect (ICurve, BoundingRect, out GeoPoint[], out GeoPoint2D[], out double[])"/>
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="uvExtent"></param>
        /// <param name="ips"></param>
        /// <param name="uvOnFaces"></param>
        /// <param name="uOnCurve3Ds"></param>
        public virtual void Intersect(ICurve curve, BoundingRect uvExtent, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {   // nach und nach selbst implementieren, bei den einzelnen Flächen und hier generell
            //try
            //{
            //    CndHlp3D.Surface sf = Helper;
            //    CndHlp3D.GeoPoint3D[] ip3d = sf.GetCurveIntersection((curve as ICndHlp3DEdge).Edge);
            //    ips = GeoPoint.FromCndHlp(ip3d);
            //}
            //catch (OpenCascade.Exception)
            //{
            //    ips = new GeoPoint[0];
            //}
            //uvOnFaces = new GeoPoint2D[ips.Length];
            //uOnCurve3Ds = new double[ips.Length];
            //for (int i = 0; i < ips.Length; ++i)
            //{
            //    uvOnFaces[i] = this.PositionOf(ips[i]);
            //    uOnCurve3Ds[i] = curve.PositionOf(ips[i]);
            //}
            Polynom implpol = GetImplicitPolynomial();
            if (implpol != null)
                if (curve is IDualSurfaceCurve)
                {
                    IDualSurfaceCurve dsc = (curve as IDualSurfaceCurve);
                    // Surfaces.Intersect(this,uvExtent,dsc.Surface1,dsc.Curve2D1.GetExtent(),dsc.Surface2,dsc.Curve2D2.GetExtent(),)
                }
            BoxedSurfaceEx.Intersect(curve, uvExtent, out ips, out uvOnFaces, out uOnCurve3Ds);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.Intersect (BoundingRect, ISurface, BoundingRect)"/>
        /// </summary>
        /// <param name="thisBounds"></param>
        /// <param name="other"></param>
        /// <param name="otherBounds"></param>
        /// <returns></returns>
        public virtual ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            GetExtremePositions(thisBounds, other, otherBounds, out List<Tuple<double, double, double, double>> extremePositions);
            if (usedArea.IsEmpty() || usedArea.IsInfinite) usedArea = thisBounds;
            return BoxedSurfaceEx.Intersect(thisBounds, other, otherBounds, null, extremePositions);
        }
        public virtual ICurve Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, GeoPoint seed)
        {
            if (usedArea.IsEmpty()) usedArea = thisBounds;
            ICurve[] sol = BoxedSurfaceEx.Intersect(thisBounds, other, otherBounds, new List<GeoPoint>(new GeoPoint[] { seed }));
            if (sol == null || sol.Length == 0) sol = Intersect(thisBounds, other, otherBounds);
            for (int i = 0; i < sol.Length; i++)
            {
                if (sol[i].DistanceTo(seed) < Precision.eps) return sol[i];
            }
            return null;
        }

        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.ReverseOrientation ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual ModOp2D ReverseOrientation()
        {
            throw new NotImplementedException("ReverseOrientation must be implemented");
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.SameGeometry (BoundingRect, ISurface, BoundingRect, double, out ModOp2D)"/>
        /// </summary>
        /// <param name="thisBounds"></param>
        /// <param name="other"></param>
        /// <param name="otherBounds"></param>
        /// <param name="precision"></param>
        /// <param name="firstToSecond"></param>
        /// <returns></returns>
        public virtual bool SameGeometry(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, double precision, out ModOp2D firstToSecond)
        {
            // hier bräuchte man die allgemeine triangulierung oder so ...
            if (this.GetType() != other.GetType())
            {
                firstToSecond = ModOp2D.Null;
                return false;
            }
            return Surfaces.Overlapping(this, thisBounds, other, otherBounds, precision, out firstToSecond);
        }
        public virtual double IsParallel(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {   // should be implemented for standard surfaces
            return double.MaxValue;
        }

        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetOffsetSurface (double)"/>
        /// </summary>
        /// <param name="offset"></param>
        /// <returns></returns>
        public virtual ISurface GetOffsetSurface(double offset)
        {
            return new OffsetSurface(this, offset);
        }
        public virtual ISurface GetOffsetSurface(double offset, out ModOp2D mod)
        {
            mod = ModOp2D.Null;
            return null;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetNaturalBounds (out double, out double, out double, out double)"/>
        /// </summary>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        public virtual void GetNaturalBounds(out double umin, out double umax, out double vmin, out double vmax)
        {
            if (usedArea.IsEmpty())
            {
                umin = double.MinValue;
                umax = double.MaxValue;
                vmin = double.MinValue;
                vmax = double.MaxValue;
            }
            else
            {
                umin = usedArea.Left;
                umax = usedArea.Right;
                vmin = usedArea.Bottom;
                vmax = usedArea.Top;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.HitTest (BoundingCube, double, double, double, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="umin"></param>
        /// <param name="umax"></param>
        /// <param name="vmin"></param>
        /// <param name="vmax"></param>
        /// <returns></returns>
        public virtual bool HitTest(BoundingCube cube, double umin, double umax, double vmin, double vmax)
        {
            throw new NotImplementedException("HitTest must be implemented");
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.HitTest (BoundingCube, out GeoPoint2D)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public virtual bool HitTest(BoundingCube cube, out GeoPoint2D uv)
        {
            Polynom implicitSurface = GetImplicitPolynomial();
            uv = GeoPoint2D.Origin;
            if (implicitSurface != null)
            {
                double cntDist = implicitSurface.Eval(cube.GetCenter());
                // if (cube.DiagonalLength / 2 < Math.Abs(cntDist)) return false; // the cube is too far away from the surface
                // the distance only works for certain surfaces (plane, sphere, cylinder, not for torus, cone)
                int cntSign = Math.Sign(cntDist);
                GeoPoint[] vertices = cube.Points;
                int[] vertexSign = new int[vertices.Length];
                bool differentSign = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertexSign[i] = Math.Sign(implicitSurface.Eval(vertices[i]));
                    differentSign |= vertexSign[i] != cntSign;
                }
                int[,] ln = cube.LineNumbers;
                if (differentSign)
                {   // there must be at least 3 edges of the cube passing through the surface
                    // we should return them all, but the parameter allows only one
                    for (int i = 0; i < 12; i++)
                    {
                        if (vertexSign[ln[i, 0]] != vertexSign[ln[i, 1]])
                        {   // the line from vertices[ln[i,0]] to vertices[ln[i,1]] intersects the surface.
                            GeoPoint startPoint = vertices[ln[i, 0]];
                            GeoVector direction = vertices[ln[i, 1]] - startPoint;
                            Polynom toSolve = implicitSurface.Substitute(Polynom.Line3d(startPoint, direction));
                            double[] roots = toSolve.Roots();
                            for (int j = 0; j < roots.Length; j++)
                            {
                                if (roots[j] >= 0 && roots[j] <= 1)
                                {
                                    GeoPoint p = startPoint + roots[j] * direction;
                                    uv = PositionOf(p);
                                    return true;
                                }
                            }

                        }
                    }
                }
                else
                {
                    // no cube edge intersects the surface (an odd number of times) and the surface is close to the cube
                    // test all edges of the cube against the surface
                    for (int i = 0; i < 12; i++)
                    {
                        GeoPoint startPoint = vertices[ln[i, 0]];
                        GeoVector direction = vertices[ln[i, 1]] - startPoint;
                        Polynom toSolve = implicitSurface.Substitute(Polynom.Line3d(startPoint, direction));
                        double[] roots = toSolve.Roots();
                        for (int j = 0; i < roots.Length; i++)
                        {
                            if (roots[j] >= 0 && roots[j] <= 1)
                            {
                                GeoPoint p = startPoint + roots[j] * direction;
                                uv = PositionOf(p);
                                return true;
                            }
                        }
                    }
                }
#if DEBUG
                // find a plane which separates the surface and the cube
                // doesn't help
                //PolynomVector normalPolynom = new PolynomVector(implicitSurface.Derivate(1, 0, 0), implicitSurface.Derivate(0, 1, 0), implicitSurface.Derivate(0, 0, 1));
                //GeoPoint center = cube.GetCenter();
                //GeoVector normal = normalPolynom.Eval(center);
                //normal.ArbitraryNormals(out GeoVector dirx, out GeoVector diry);
                //Polynom plane = implicitSurface.Substitute(new Polynom(center.x, "", dirx.x, "u", diry.x, "v"), new Polynom(center.y, "", dirx.y, "u", diry.y, "v"), new Polynom(center.z, "", dirx.z, "u", diry.z, "v"));
                //double[] uminmax = plane.Derivate(1, 0).Roots();
                //double[] vminmax = plane.Derivate(0, 1).Roots();
#endif
                // no cube edge intersects the surface any number of times
                // Maybe a sphere or a torus touch the cube without intersection one of it's edges
                GeoPoint2D[] extrema = this.GetExtrema(); // the extrema in axis direction
                for (int i = 0; i < extrema.Length; i++)
                {
                    if (cube.Contains(PointAt(extrema[i])))
                    {
                        uv = extrema[i];
                        return true;
                    }
                }
                // a torus may pass through the cube without interfering with the edges: check the foot points from the center
                GeoPoint2D[] footPoints = this.PerpendicularFoot(cube.GetCenter());
                for (int i = 0; i < footPoints.Length; i++)
                {
                    if (cube.Contains(PointAt(footPoints[i])))
                    {
                        uv = footPoints[i];
                        return true;
                    }
                }
                return false;
            }
            return this.BoxedSurfaceEx.HitTest(cube, out uv);
        }
        private bool DebugNewHitTest(BoundingCube cube, out GeoPoint2D uv)
        {
            Polynom implicitSurface = GetImplicitPolynomial();
            uv = GeoPoint2D.Origin;
            if (implicitSurface != null)
            {
                double cntDist = implicitSurface.Eval(cube.GetCenter());
                if (cube.DiagonalLength / 2 < Math.Abs(cntDist)) return false; // the cube is too far away from the surface
                int cntSign = Math.Sign(cntDist);
                GeoPoint[] vertices = cube.Points;
                int[] vertexSign = new int[vertices.Length];
                bool differentSign = false;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertexSign[i] = Math.Sign(implicitSurface.Eval(vertices[i]));
                    differentSign |= vertexSign[i] != cntSign;
                }
                int[,] ln = cube.LineNumbers;
                if (differentSign)
                {   // there must be at least 3 edges of the cube passing through the surface
                    // we should return them all, but the parameter allows only one
                    for (int i = 0; i < 12; i++)
                    {
                        if (vertexSign[ln[i, 0]] != vertexSign[ln[i, 1]])
                        {   // the line from vertices[ln[i,0]] to vertices[ln[i,1]] intersects the surface.
                            GeoPoint startPoint = vertices[ln[i, 0]];
                            GeoVector direction = vertices[ln[i, 1]] - startPoint;
                            Polynom toSolve = implicitSurface.Substitute(Polynom.Line3d(startPoint, direction));
                            double[] roots = toSolve.Roots();
                            for (int j = 0; i < roots.Length; i++)
                            {
                                if (roots[j] >= 0 && roots[j] <= 1)
                                {
                                    GeoPoint p = startPoint + roots[j] * direction;
                                    uv = PositionOf(p);
                                    return true;
                                }
                            }

                        }
                    }
                }
                else
                {
                    // no cube edge intersects the surface (an odd number of times) and the surface is close to the cube
                    // test all edges of the cube against the surface
                    for (int i = 0; i < 12; i++)
                    {
                        GeoPoint startPoint = vertices[ln[i, 0]];
                        GeoVector direction = vertices[ln[i, 1]] - startPoint;
                        Polynom toSolve = implicitSurface.Substitute(Polynom.Line3d(startPoint, direction));
                        double[] roots = toSolve.Roots();
                        for (int j = 0; i < roots.Length; i++)
                        {
                            if (roots[j] >= 0 && roots[j] <= 1)
                            {
                                GeoPoint p = startPoint + roots[j] * direction;
                                uv = PositionOf(p);
                                return true;
                            }
                        }
                    }
                }
                // no cube edge intersects the surface any number of times
                // Maybe a sphere or a torus touch the cube without intersection one of it's edges
                GeoPoint2D[] extrema = this.GetExtrema(); // the extrema in axis direction
                for (int i = 0; i < extrema.Length; i++)
                {
                    if (cube.Contains(PointAt(extrema[i])))
                    {
                        uv = extrema[i];
                        return true;
                    }
                }
                // a torus may pass through the cube without interfering with the edges: check the foot points from the center
                GeoPoint2D[] footPoints = this.PerpendicularFoot(cube.GetCenter());
                for (int i = 0; i < footPoints.Length; i++)
                {
                    if (cube.Contains(PointAt(footPoints[i])))
                    {
                        uv = footPoints[i];
                        return true;
                    }
                }
                return false;
            }
            return false;
        }
        public virtual bool Oriented
        {
            get
            {
                return false;
            }
        }
        public virtual RuledSurfaceMode IsRuled
        {
            get
            {
                return RuledSurfaceMode.notRuled;
            }
        }

        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.Orientation (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual double Orientation(GeoPoint p)
        {
            return 0.0;
        }
        protected virtual double[] GetSaveUSteps()
        {
            return new double[0];
        }
        protected virtual double[] GetSaveVSteps()
        {
            return new double[0];
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetExtrema ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual GeoPoint2D[] GetExtrema()
        {
            if (extrema == null)
            {
                List<GeoPoint2D> res = new List<GeoPoint2D>();
                double[] usteps = GetSaveUSteps();
                if (usteps.Length > 0)
                {
                    double[] vsteps = GetSaveVSteps();
                    double epsu = (usteps[usteps.Length - 1] - usteps[0]) * 1e-6;
                    double epsv = (vsteps[vsteps.Length - 1] - vsteps[0]) * 1e-6;
                    for (int i = 1; i < usteps.Length; ++i)
                    {
                        for (int j = 1; j < vsteps.Length; ++j)
                        {
                            GeoPoint2D u00 = new GeoPoint2D(usteps[i - 1], vsteps[j - 1]);
                            GeoPoint2D u01 = new GeoPoint2D(usteps[i - 1], vsteps[j]);
                            GeoPoint2D u10 = new GeoPoint2D(usteps[i], vsteps[j - 1]);
                            GeoPoint2D u11 = new GeoPoint2D(usteps[i], vsteps[j]);
                            GeoVector v00 = GetNormal(u00); // nicht normieren wg. Nullvektoren
                            GeoVector v01 = GetNormal(u01);
                            GeoVector v10 = GetNormal(u10);
                            GeoVector v11 = GetNormal(u11);
                            GeoPoint2D p;
                            //if (ApproxExtreme(GeoVector.XAxis, u00, u01, u10, u11, v00, v01, v10, v11, epsu, epsv, out p)) res.Add(p);
                            //if (ApproxExtreme(-GeoVector.XAxis, u00, u01, u10, u11, v00, v01, v10, v11, epsu, epsv, out p)) res.Add(p);
                            //if (ApproxExtreme(GeoVector.YAxis, u00, u01, u10, u11, v00, v01, v10, v11, epsu, epsv, out p)) res.Add(p);
                            //if (ApproxExtreme(-GeoVector.YAxis, u00, u01, u10, u11, v00, v01, v10, v11, epsu, epsv, out p)) res.Add(p);
                            //if (ApproxExtreme(GeoVector.ZAxis, u00, u01, u10, u11, v00, v01, v10, v11, epsu, epsv, out p)) res.Add(p);
                            //if (ApproxExtreme(-GeoVector.ZAxis, u00, u01, u10, u11, v00, v01, v10, v11, epsu, epsv, out p)) res.Add(p);
                            // jeweils zwei Dreiecke untersuchen
                            if (FindExtreme(GeoVector.XAxis, u00, u01, u10, v00, v01, v10, epsu, epsv, out p)) res.Add(p);
                            if (FindExtreme(GeoVector.XAxis, u11, u01, u10, v11, v01, v10, epsu, epsv, out p)) res.Add(p);
                            if (FindExtreme(GeoVector.YAxis, u00, u01, u10, v00, v01, v10, epsu, epsv, out p)) res.Add(p);
                            if (FindExtreme(GeoVector.YAxis, u11, u01, u10, v11, v01, v10, epsu, epsv, out p)) res.Add(p);
                            if (FindExtreme(GeoVector.ZAxis, u00, u01, u10, v00, v01, v10, epsu, epsv, out p)) res.Add(p);
                            if (FindExtreme(GeoVector.ZAxis, u11, u01, u10, v11, v01, v10, epsu, epsv, out p)) res.Add(p);
                        }
                    }
                }
                extrema = res.ToArray();
            }
            return extrema;
        }
        private static double[] AllCurveAxtrema(ICurve curve)
        {
            double[] ex = curve.GetExtrema(GeoVector.XAxis);
            double[] ey = curve.GetExtrema(GeoVector.YAxis);
            double[] ez = curve.GetExtrema(GeoVector.ZAxis);
            double[] res = new double[ex.Length + ey.Length + ez.Length];
            int j = 0;
            for (int i = 0; i < ex.Length; ++i)
            {
                res[j] = ex[i];
                ++j;
            }
            for (int i = 0; i < ey.Length; ++i)
            {
                res[j] = ey[i];
                ++j;
            }
            for (int i = 0; i < ez.Length; ++i)
            {
                res[j] = ez[i];
                ++j;
            }
            return res;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetPatchExtent (BoundingRect)"/>
        /// </summary>
        /// <param name="uvPatch"></param>
        /// <returns></returns>
        public virtual BoundingCube GetPatchExtent(BoundingRect uvPatch, bool rough)
        {   // kann natürlich in den einzelnen flächen besser gelöst werden
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            GeoPoint2D[] extr = GetExtrema();
            for (int i = 0; i < extr.Length; ++i)
            {
                if (uvPatch.Contains(extr[i]))
                {
                    res.MinMax((this as ISurface).PointAt(extr[i]));
                }
            }
            ICurve fix;
            double[] cex;
            fix = FixedU(uvPatch.Left, uvPatch.Bottom, uvPatch.Top);
            if (fix != null)
            {
                res.MinMax(fix.StartPoint);
                res.MinMax(fix.EndPoint);
                cex = AllCurveAxtrema(fix);
                for (int i = 0; i < cex.Length; ++i)
                {
                    res.MinMax(fix.PointAt(cex[i]));
                }
            }
            fix = FixedU(uvPatch.Right, uvPatch.Bottom, uvPatch.Top);
            if (fix != null)
            {
                res.MinMax(fix.StartPoint);
                res.MinMax(fix.EndPoint);
                cex = AllCurveAxtrema(fix);
                for (int i = 0; i < cex.Length; ++i)
                {
                    res.MinMax(fix.PointAt(cex[i]));
                }
            }
            fix = FixedV(uvPatch.Bottom, uvPatch.Left, uvPatch.Right);
            if (fix != null)
            {
                res.MinMax(fix.StartPoint);
                res.MinMax(fix.EndPoint);
                cex = AllCurveAxtrema(fix);
                for (int i = 0; i < cex.Length; ++i)
                {
                    res.MinMax(fix.PointAt(cex[i]));
                }
            }
            fix = FixedV(uvPatch.Top, uvPatch.Left, uvPatch.Right);
            if (fix != null)
            {
                res.MinMax(fix.StartPoint);
                res.MinMax(fix.EndPoint);
                cex = AllCurveAxtrema(fix);
                for (int i = 0; i < cex.Length; ++i)
                {
                    res.MinMax(fix.PointAt(cex[i]));
                }
            }
            return res;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetPolynomialParameters ()"/>
        /// </summary>
        /// <returns></returns>
        public virtual double[] GetPolynomialParameters()
        {
            return null;
        }
        public virtual Polynom GetImplicitPolynomial()
        {
            return null;
        }

        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.SetBounds (BoundingRect)"/>
        /// </summary>
        /// <param name="boundingRect"></param>
        public virtual void SetBounds(BoundingRect boundingRect)
        {
            usedArea = boundingRect;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.PerpendicularFoot (GeoPoint)"/>
        /// </summary>
        /// <param name="fromHere"></param>
        /// <returns></returns>
        public virtual GeoPoint2D[] PerpendicularFoot(GeoPoint fromHere)
        {
            GeoPoint2D pos = PositionOf(fromHere);
            GeoPoint loc;
            GeoVector du, dv;
            DerivationAt(pos, out loc, out du, out dv);
            double d = Geometry.DistPL(fromHere, loc, du ^ dv);
            // if (Precision.IsEqual(fromHere, loc) || Precision.SameDirection(du ^ dv, fromHere - loc, false))
            // bei PositionOf in der BoxedSurfaces ist das Abbruchkriterium der Abstand des Punktes von der Normalen in pos.
            // hier sollte man also das gleiche Kriterium wählen
            if (Precision.IsEqual(fromHere, loc) || d < Precision.eps * 100)
            {
                return new GeoPoint2D[] { pos };
            }
            else
            {
                return new GeoPoint2D[0];
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.HasDiscontinuousDerivative (out ICurve2D[])"/>
        /// </summary>
        /// <param name="discontinuities"></param>
        /// <returns></returns>
        public virtual bool HasDiscontinuousDerivative(out ICurve2D[] discontinuities)
        {   // kommt nur bei einigen komischen Flächen vor und bei NURBS mit degree==1 und mehr als 2 knots
            discontinuities = null;
            return false;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetNonPeriodicSurface (ICurve[])"/>
        /// </summary>
        /// <param name="maxOutline"></param>
        /// <returns></returns>
        public virtual ISurface GetNonPeriodicSurface(ICurve[] orientedCurves)
        {
            return null;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ISurface.GetPatchHull (BoundingRect, out GeoPoint, out GeoVector, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="uvpatch"></param>
        /// <param name="loc"></param>
        /// <param name="dir1"></param>
        /// <param name="dir2"></param>
        /// <param name="dir3"></param>
        public virtual void GetPatchHull(BoundingRect uvpatch, out GeoPoint loc, out GeoVector dir1, out GeoVector dir2, out GeoVector dir3)
        {
            BoxedSurfaceEx.GetPatchHull(uvpatch, out loc, out dir1, out dir2, out dir3);
        }
        public virtual GeoPoint[] GetTouchingPoints(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds)
        {
            throw new NotImplementedException();
        }
        public virtual ISurface GetCanonicalForm(double precision, BoundingRect? bounds)
        {
            return null;
        }
        public GeoPoint2D PositionOf(GeoPoint p, BoundingRect domain)
        {
            GeoPoint2D res = PositionOf(p);
            SurfaceHelper.AdjustPeriodic(this, domain, ref res);
            return res;
        }
        public virtual int GetExtremePositions(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, out List<Tuple<double, double, double, double>> extremePositions)
        {
            extremePositions = new List<Tuple<double, double, double, double>>();
            GeoPoint2D uv11 = thisBounds.GetCenter();
            GeoPoint2D uv22 = otherBounds.GetCenter();
            try
            {
                double maxerror = GaussNewtonMinimizer.SurfaceExtrema(this, thisBounds, other, otherBounds, ref uv11, ref uv22);
                if (maxerror < Precision.eps)
                {
                    if ((this.PointAt(uv11) | other.PointAt(uv22)) < Precision.eps)
                    {
                        // this is an intersection point. try to find another intersection point and take the point in the middle
                        // this will not be an extreme point but a good additional point for BoxedSurface.Intersect additionalSearchPositions
                        foreach (GeoPoint2D uv111 in new GeoPoint2D[] { thisBounds.GetLowerLeft(), thisBounds.GetLowerRight(), thisBounds.GetUpperLeft(), thisBounds.GetUpperRight() })
                        {   // try with different starting points to find more intersection points
                            GeoPoint2D uv2 = other.PositionOf(this.PointAt(uv111));
                            GeoPoint2D uv1 = uv111;
                            maxerror = GaussNewtonMinimizer.SurfaceExtrema(this, thisBounds, other, otherBounds, ref uv1, ref uv2);
                            if (maxerror < Precision.eps && (uv1 | uv11) > Precision.eps && (uv2 | uv22) > Precision.eps)
                            {   //we have another intersection point and we will use the point in between
                                if ((this.PointAt(uv1) | other.PointAt(uv2)) < Precision.eps)
                                {   // a second intersection point
                                    extremePositions.Add(new Tuple<double, double, double, double>((uv1.x + uv11.x) / 2.0, (uv1.y + uv11.y) / 2.0, (uv2.x + uv22.x) / 2.0, (uv2.y + uv22.y) / 2.0));
                                }
                                else
                                {   // a real extreme point, which is not an intersection point
                                    extremePositions.Add(new Tuple<double, double, double, double>(uv1.x, uv1.y, uv2.x, uv2.y));
                                }
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (thisBounds.Contains(uv11)) extremePositions.Add(new Tuple<double, double, double, double>(uv11.x, uv11.y, double.NaN, double.NaN));
                        if (otherBounds.Contains(uv22)) extremePositions.Add(new Tuple<double, double, double, double>(double.NaN, double.NaN, uv22.x, uv22.y));
                    }
                }
            }
            catch (NotImplementedException) { } // some surfaces don't implement second derivation (e.g. OffsetSurface) which is needed in GaussNewtonMinimizer.SurfaceExtrema
            // else: search with different starting points
            return extremePositions.Count;
        }
        public virtual int GetExtremePositions(BoundingRect domain, ICurve curve3D, out List<Tuple<double, double, double>> positions)
        {   // to implement
            positions = new List<Tuple<double, double, double>>();
            GeoPoint2D uv1 = domain.GetCenter();
            double u2 = 0.5;
            double maxerror = GaussNewtonMinimizer.SurfaceCurveExtrema(this, domain, curve3D, 0.0, 1.0, ref uv1, ref u2);
            if (maxerror < Precision.eps)
            {
                positions.Add(new Tuple<double, double, double>(uv1.x, uv1.y, u2));
            }
            return positions.Count;
        }
        public virtual double GetDistance(GeoPoint p)
        {
            return PointAt(PositionOf(p)) | p;
        }
        public virtual bool IsExtruded(GeoVector direction)
        {
            return false;
        }
        public virtual bool IsRotated(Axis rotationAxis)
        {
            return false;
        }
        public virtual MenuWithHandler[] GetContextMenuForParametrics(IFrame frame, Face face)
        {
            return new MenuWithHandler[0];
        }
        public virtual bool UvChangesWithModification => false;
        public virtual bool CanStretch => false;
        public virtual double Fit(IEnumerable<GeoPoint> points)
        {
            // we are looking for the best transformation of the points so that the distance to the surface of all points becomes minimal
            // transformation: orthonormal with scaling and translation
            GeoPoint[] pnts = points.ToArray();

#if DEBUG
            {
                double err = 0.0;
                for (int i = 0; i < pnts.Length; i++) err += pnts[i] & PointAt(PositionOf(pnts[i])); // squared distance
                double derr = 0.0;
                for (int i = 0; i < pnts.Length; i++) derr += this.GetDistance(pnts[i]);
            }
            //for (int i = 0; i < pnts.Length; i++)
            //{
            //    pnts[i] = new GeoPoint(pnts[i].x, pnts[i].y, pnts[i].z + 1);
            //}
#endif
            Vector<double> observedX = new DenseVector(pnts.Length); // there is no need to set values
            Vector<double> observedY = new DenseVector(pnts.Length); // this is the data we want to achieve, namely 0.0
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-15, maximumIterations: 20);
            NonlinearMinimizationResult mres;
            if (CanStretch)
            {
                IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                    new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                    {
                        // 
                        DenseVector res = new DenseVector(pnts.Length);
                        double a = vd[0]; // rotation around x-axis
                        double b = vd[1]; // rotation around y-axis
                        double c = vd[2]; // rotation around z-axis
                        double fx = vd[3]; // scaling factor x
                        double fy = vd[4]; // scaling factor y
                        double fz = vd[5]; // scaling factor z
                        double sa = Math.Sin(a);
                        double ca = Math.Cos(a);
                        double sb = Math.Sin(b);
                        double cb = Math.Cos(b);
                        double sc = Math.Sin(c);
                        double cc = Math.Cos(c);
                        GeoVector t = new GeoVector(vd[6], vd[7], vd[8]); // translation 
                        Matrix m = DenseMatrix.OfArray(new double[,] { { fx, 0, 0 }, { 0, fy, 0 }, { 0, 0, fz } }) *
                            DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                            DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                            DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                        ModOp trsf = new ModOp(m.ToArray(), t);
                        for (int i = 0; i < pnts.Length; i++)
                        {
                            GeoPoint p = trsf * pnts[i];
                            GeoPoint2D uv = PositionOf(p);
                            GeoPoint o = PointAt(uv);
                            GeoVector n = GetNormal(uv).Normalized;
                            res[i] = n * (p - o); // (signed) distance from the plane
                        }
#if DEBUG
                        double err = 0.0;
                        for (int i = 0; i < pnts.Length; i++) err += res[i] * res[i];
#endif
                        return res;
                    }),
                    new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                    {
                        var prime = new DenseMatrix(pnts.Length, 9);
                        double a = vd[0]; // rotation around x-axis
                        double b = vd[1]; // rotation around y-axis
                        double c = vd[2]; // rotation around z-axis
                        double fx = vd[3]; // scaling factor x
                        double fy = vd[4]; // scaling factor y
                        double fz = vd[5]; // scaling factor z
                        double sa = Math.Sin(a);
                        double ca = Math.Cos(a);
                        double sb = Math.Sin(b);
                        double cb = Math.Cos(b);
                        double sc = Math.Sin(c);
                        double cc = Math.Cos(c);
                        GeoVector t = new GeoVector(vd[6], vd[7], vd[8]); // translation 
                        Matrix m = DenseMatrix.OfArray(new double[,] { { fx, 0, 0 }, { 0, fy, 0 }, { 0, 0, fz } }) *
                            DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                            DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                            DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                        ModOp trsf = new ModOp(m.ToArray(), t);
                        for (int i = 0; i < pnts.Length; i++)
                        {
                            GeoPoint p = trsf * pnts[i];
                            GeoPoint2D uv = PositionOf(p);
                            GeoPoint o = PointAt(uv);
                            GeoVector n = GetNormal(uv).Normalized;

                            prime[i, 0] = fy * n.y * (sb * sc * (ca * p.y - sa * p.z) + cc * (-ca * p.z - sa * p.y)) + fx * n.x * (sb * cc * (ca * p.y - sa * p.z) - sc * (-ca * p.z - sa * p.y)) + cb * fz * n.z * (ca * p.y - sa * p.z);
                            prime[i, 1] = fz * n.z * (-sb * (ca * p.z + sa * p.y) - cb * p.x) + sc * fy * n.y * (cb * (ca * p.z + sa * p.y) - sb * p.x) + cc * fx * n.x * (cb * (ca * p.z + sa * p.y) - sb * p.x);
                            prime[i, 2] = fx * n.x * (-sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - cc * (ca * p.y - sa * p.z)) + fy * n.y * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z));
                            prime[i, 3] = n.x * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z));
                            prime[i, 4] = n.y * (sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) + cc * (ca * p.y - sa * p.z));
                            prime[i, 5] = n.z * (cb * (ca * p.z + sa * p.y) - sb * p.x);
                            prime[i, 6] = n.x;
                            prime[i, 7] = n.y;
                            prime[i, 8] = n.z;
                        }
                        return prime;
                    }), observedX, observedY);
                mres = lm.FindMinimum(iom, new DenseVector(new double[] { 0, 0, 0, 1, 1, 1, 0, 0, 0 }));
                if (true)
                {
                    double a = mres.MinimizingPoint[0]; // rotation around x-axis
                    double b = mres.MinimizingPoint[1]; // rotation around y-axis
                    double c = mres.MinimizingPoint[2]; // rotation around z-axis
                    double fx = mres.MinimizingPoint[3]; // scaling factor x
                    double fy = mres.MinimizingPoint[4]; // scaling factor y
                    double fz = mres.MinimizingPoint[5]; // scaling factor z
                    double sa = Math.Sin(a);
                    double ca = Math.Cos(a);
                    double sb = Math.Sin(b);
                    double cb = Math.Cos(b);
                    double sc = Math.Sin(c);
                    double cc = Math.Cos(c);
                    GeoVector t = new GeoVector(mres.MinimizingPoint[6], mres.MinimizingPoint[7], mres.MinimizingPoint[8]); // translation 
                    Matrix m = DenseMatrix.OfArray(new double[,] { { fx, 0, 0 }, { 0, fy, 0 }, { 0, 0, fz } }) *
                        DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                        DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                        DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                    ModOp trsf = new ModOp(m.ToArray(), t);
                    Modify(trsf.GetInverse());
                }
            }
            else
            {
                IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                    new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                    {
                        // 
                        DenseVector res = new DenseVector(pnts.Length);
                        double a = vd[0]; // rotation around x-axis
                        double b = vd[1]; // rotation around y-axis
                        double c = vd[2]; // rotation around z-axis
                        double f = vd[3]; // scaling factor
                        double sa = Math.Sin(a);
                        double ca = Math.Cos(a);
                        double sb = Math.Sin(b);
                        double cb = Math.Cos(b);
                        double sc = Math.Sin(c);
                        double cc = Math.Cos(c);
                        GeoVector t = new GeoVector(vd[4], vd[5], vd[6]); // translation 
                        Matrix m = DenseMatrix.CreateDiagonal(3, 3, f) *
                            DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                            DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                            DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                        ModOp trsf = new ModOp(m.ToArray(), t);
                        for (int i = 0; i < pnts.Length; i++)
                        {
                            GeoPoint p = trsf * pnts[i];
                            GeoPoint2D uv = PositionOf(p);
                            GeoPoint o = PointAt(uv);
                            res[i] = (p - o) * (p - o); // square distance
                        }
#if DEBUG
                        double err = 0.0;
                        for (int i = 0; i < pnts.Length; i++) err += res[i];
#endif
                        return res;
                    }),
                    new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                    {
                        var prime = new DenseMatrix(pnts.Length, 7);

                        double a = vd[0]; // rotation around x-axis
                        double b = vd[1]; // rotation around y-axis
                        double c = vd[2]; // rotation around z-axis
                        double f = vd[3]; // scaling factor
                        double sa = Math.Sin(a);
                        double ca = Math.Cos(a);
                        double sb = Math.Sin(b);
                        double cb = Math.Cos(b);
                        double sc = Math.Sin(c);
                        double cc = Math.Cos(c);
                        GeoVector t = new GeoVector(vd[4], vd[5], vd[6]); // translation 
                        Matrix m = DenseMatrix.CreateDiagonal(3, 3, f) *
                            DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                            DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                            DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                        ModOp trsf = new ModOp(m.ToArray(), t);
                        for (int i = 0; i < pnts.Length; i++)
                        {
                            GeoPoint p = trsf * pnts[i];
                            GeoPoint2D uv = PositionOf(p);
                            GeoPoint o = PointAt(uv);

                            prime[i, 0] = -2 * cb * f * (ca * p.y - sa * p.z) * (-t.z - f * (cb * (ca * p.z + sa * p.y) - sb * p.x) + o.z) - 2 * f * (sb * sc * (ca * p.y - sa * p.z) + cc * (-ca * p.z - sa * p.y)) * (-t.y - f * (sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) + cc * (ca * p.y - sa * p.z)) + o.y) - 2 * f * (sb * cc * (ca * p.y - sa * p.z) - sc * (-ca * p.z - sa * p.y)) * (-t.x - f * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z)) + o.x);
                            prime[i, 1] = -2 * f * (-sb * (ca * p.z + sa * p.y) - cb * p.x) * (-t.z - f * (cb * (ca * p.z + sa * p.y) - sb * p.x) + o.z) - 2 * sc * f * (cb * (ca * p.z + sa * p.y) - sb * p.x) * (-t.y - f * (sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) + cc * (ca * p.y - sa * p.z)) + o.y) - 2 * cc * f * (cb * (ca * p.z + sa * p.y) - sb * p.x) * (-t.x - f * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z)) + o.x);
                            prime[i, 2] = -2 * f * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z)) * (-t.y - f * (sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) + cc * (ca * p.y - sa * p.z)) + o.y) - 2 * f * (-sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - cc * (ca * p.y - sa * p.z)) * (-t.x - f * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z)) + o.x);
                            prime[i, 3] = 2 * (sb * p.x - cb * (ca * p.z + sa * p.y)) * (-t.z - f * (cb * (ca * p.z + sa * p.y) - sb * p.x) + o.z) + 2 * (-sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - cc * (ca * p.y - sa * p.z)) * (-t.y - f * (sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) + cc * (ca * p.y - sa * p.z)) + o.y) + 2 * (sc * (ca * p.y - sa * p.z) - cc * (sb * (ca * p.z + sa * p.y) + cb * p.x)) * (-t.x - f * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z)) + o.x);
                            prime[i, 4] = -2 * (-t.x - f * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z)) + o.x);
                            prime[i, 5] = -2 * (-t.y - f * (sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) + cc * (ca * p.y - sa * p.z)) + o.y);
                            prime[i, 6] = -2 * (-t.z - f * (cb * (ca * p.z + sa * p.y) - sb * p.x) + o.z);
                        }
                        return prime;
                    }), observedX, observedY);
                mres = lm.FindMinimum(iom, new DenseVector(new double[] { 0, 0, 0, 1, 0, 0, 0 }));
                if (true)
                {
                    double a = mres.MinimizingPoint[0]; // rotation around x-axis
                    double b = mres.MinimizingPoint[1]; // rotation around y-axis
                    double c = mres.MinimizingPoint[2]; // rotation around z-axis
                    double f = mres.MinimizingPoint[3]; // scaling factor
                    double sa = Math.Sin(a);
                    double ca = Math.Cos(a);
                    double sb = Math.Sin(b);
                    double cb = Math.Cos(b);
                    double sc = Math.Sin(c);
                    double cc = Math.Cos(c);
                    GeoVector t = new GeoVector(mres.MinimizingPoint[4], mres.MinimizingPoint[5], mres.MinimizingPoint[6]); // translation 
                    Matrix m = DenseMatrix.CreateDiagonal(3, 3, f) *
                        DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                        DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                        DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                    ModOp trsf = new ModOp(m.ToArray(), t);
#if DEBUG
                    double err1 = 0.0;
                    double derr1 = 0.0;
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        GeoPoint p = trsf * pnts[i];
                        GeoPoint2D uv = PositionOf(p);
                        GeoPoint o = PointAt(uv);
                        double r = (p - o) * (p - o); // (signed) distance from the plane
                        err1 += r;
                        derr1 += GetDistance(trsf * pnts[i]);
                    }
#endif
                    Modify(trsf.GetInverse());
#if DEBUG
                    double err2 = 0.0;
                    double derr2 = 0.0;
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        GeoPoint p = pnts[i];
                        GeoPoint2D uv = PositionOf(p);
                        GeoPoint o = PointAt(uv);
                        GeoVector n = GetNormal(uv).Normalized;
                        double r = n * (p - o); // (signed) distance from the plane
                        err2 += r;
                        derr2 += GetDistance(pnts[i]);
                    }
#endif
                }
            }
#if DEBUG
            {
                double err = 0.0;
                for (int i = 0; i < pnts.Length; i++) err += pnts[i] & PointAt(PositionOf(pnts[i])); // squared distance
            }
#endif
            return mres.ModelInfoAtMinimum.Value;
        }
        public virtual double FitOld(IEnumerable<GeoPoint> points)
        {
            // we are looking for the best transformation of the points so that the distance to the surface of all points becomes minimal
            // transformation: orthonormal with scaling and translation
            GeoPoint[] pnts = points.ToArray();

#if DEBUG
            {
                double err = 0.0;
                for (int i = 0; i < pnts.Length; i++) err += pnts[i] & PointAt(PositionOf(pnts[i])); // squared distance
                double derr = 0.0;
                for (int i = 0; i < pnts.Length; i++) derr += this.GetDistance(pnts[i]);
            }
#endif
            Vector<double> observedX = new DenseVector(pnts.Length); // there is no need to set values
            Vector<double> observedY = new DenseVector(pnts.Length); // this is the data we want to achieve, namely 0.0
            LevenbergMarquardtMinimizer lm = new LevenbergMarquardtMinimizer(gradientTolerance: 1e-15, maximumIterations: 20);
            NonlinearMinimizationResult mres;
            if (CanStretch)
            {
                IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                    new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                    {
                        // 
                        DenseVector res = new DenseVector(pnts.Length);
                        double a = vd[0]; // rotation around x-axis
                        double b = vd[1]; // rotation around y-axis
                        double c = vd[2]; // rotation around z-axis
                        double fx = vd[3]; // scaling factor x
                        double fy = vd[4]; // scaling factor y
                        double fz = vd[5]; // scaling factor z
                        double sa = Math.Sin(a);
                        double ca = Math.Cos(a);
                        double sb = Math.Sin(b);
                        double cb = Math.Cos(b);
                        double sc = Math.Sin(c);
                        double cc = Math.Cos(c);
                        GeoVector t = new GeoVector(vd[6], vd[7], vd[8]); // translation 
                        Matrix m = DenseMatrix.OfArray(new double[,] { { fx, 0, 0 }, { 0, fy, 0 }, { 0, 0, fz } }) *
                            DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                            DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                            DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                        ModOp trsf = new ModOp(m.ToArray(), t);
                        for (int i = 0; i < pnts.Length; i++)
                        {
                            GeoPoint p = trsf * pnts[i];
                            GeoPoint2D uv = PositionOf(p);
                            GeoPoint o = PointAt(uv);
                            GeoVector n = GetNormal(uv).Normalized;
                            res[i] = n * (p - o); // (signed) distance from the plane
                        }
#if DEBUG
                        double err = 0.0;
                        for (int i = 0; i < pnts.Length; i++) err += res[i] * res[i];
#endif
                        return res;
                    }),
                    new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                    {
                        var prime = new DenseMatrix(pnts.Length, 9);
                        double a = vd[0]; // rotation around x-axis
                        double b = vd[1]; // rotation around y-axis
                        double c = vd[2]; // rotation around z-axis
                        double fx = vd[3]; // scaling factor x
                        double fy = vd[4]; // scaling factor y
                        double fz = vd[5]; // scaling factor z
                        double sa = Math.Sin(a);
                        double ca = Math.Cos(a);
                        double sb = Math.Sin(b);
                        double cb = Math.Cos(b);
                        double sc = Math.Sin(c);
                        double cc = Math.Cos(c);
                        GeoVector t = new GeoVector(vd[6], vd[7], vd[8]); // translation 
                        Matrix m = DenseMatrix.OfArray(new double[,] { { fx, 0, 0 }, { 0, fy, 0 }, { 0, 0, fz } }) *
                            DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                            DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                            DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                        ModOp trsf = new ModOp(m.ToArray(), t);
                        for (int i = 0; i < pnts.Length; i++)
                        {
                            GeoPoint p = trsf * pnts[i];
                            GeoPoint2D uv = PositionOf(p);
                            GeoPoint o = PointAt(uv);
                            GeoVector n = GetNormal(uv).Normalized;

                            prime[i, 0] = fy * n.y * (sb * sc * (ca * p.y - sa * p.z) + cc * (-ca * p.z - sa * p.y)) + fx * n.x * (sb * cc * (ca * p.y - sa * p.z) - sc * (-ca * p.z - sa * p.y)) + cb * fz * n.z * (ca * p.y - sa * p.z);
                            prime[i, 1] = fz * n.z * (-sb * (ca * p.z + sa * p.y) - cb * p.x) + sc * fy * n.y * (cb * (ca * p.z + sa * p.y) - sb * p.x) + cc * fx * n.x * (cb * (ca * p.z + sa * p.y) - sb * p.x);
                            prime[i, 2] = fx * n.x * (-sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - cc * (ca * p.y - sa * p.z)) + fy * n.y * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z));
                            prime[i, 3] = n.x * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z));
                            prime[i, 4] = n.y * (sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) + cc * (ca * p.y - sa * p.z));
                            prime[i, 5] = n.z * (cb * (ca * p.z + sa * p.y) - sb * p.x);
                            prime[i, 6] = n.x;
                            prime[i, 7] = n.y;
                            prime[i, 8] = n.z;
                        }
                        return prime;
                    }), observedX, observedY);
                mres = lm.FindMinimum(iom, new DenseVector(new double[] { 0, 0, 0, 1, 1, 1, 0, 0, 0 }));
                if (true)
                {
                    double a = mres.MinimizingPoint[0]; // rotation around x-axis
                    double b = mres.MinimizingPoint[1]; // rotation around y-axis
                    double c = mres.MinimizingPoint[2]; // rotation around z-axis
                    double fx = mres.MinimizingPoint[3]; // scaling factor x
                    double fy = mres.MinimizingPoint[4]; // scaling factor y
                    double fz = mres.MinimizingPoint[5]; // scaling factor z
                    double sa = Math.Sin(a);
                    double ca = Math.Cos(a);
                    double sb = Math.Sin(b);
                    double cb = Math.Cos(b);
                    double sc = Math.Sin(c);
                    double cc = Math.Cos(c);
                    GeoVector t = new GeoVector(mres.MinimizingPoint[6], mres.MinimizingPoint[7], mres.MinimizingPoint[8]); // translation 
                    Matrix m = DenseMatrix.OfArray(new double[,] { { fx, 0, 0 }, { 0, fy, 0 }, { 0, 0, fz } }) *
                        DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                        DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                        DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                    ModOp trsf = new ModOp(m.ToArray(), t);
                    Modify(trsf.GetInverse());
                }
            }
            else
            {
                IObjectiveModel iom = ObjectiveFunction.NonlinearModel(
                    new Func<Vector<double>, Vector<double>, Vector<double>>(delegate (Vector<double> vd, Vector<double> ox) // function
                    {
                        // 
                        DenseVector res = new DenseVector(pnts.Length);
                        double a = vd[0]; // rotation around x-axis
                        double b = vd[1]; // rotation around y-axis
                        double c = vd[2]; // rotation around z-axis
                        double f = vd[3]; // scaling factor
                        double sa = Math.Sin(a);
                        double ca = Math.Cos(a);
                        double sb = Math.Sin(b);
                        double cb = Math.Cos(b);
                        double sc = Math.Sin(c);
                        double cc = Math.Cos(c);
                        GeoVector t = new GeoVector(vd[4], vd[5], vd[6]); // translation 
                        Matrix m = DenseMatrix.CreateDiagonal(3, 3, f) *
                            DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                            DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                            DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                        ModOp trsf = new ModOp(m.ToArray(), t);
                        for (int i = 0; i < pnts.Length; i++)
                        {
                            GeoPoint p = trsf * pnts[i];
                            GeoPoint2D uv = PositionOf(p);
                            GeoPoint o = PointAt(uv);
                            GeoVector n = GetNormal(uv).Normalized;
                            res[i] = n * (p - o); // (signed) distance from the plane
                        }
#if DEBUG
                        double err = 0.0;
                        for (int i = 0; i < pnts.Length; i++) err += res[i] * res[i];
#endif
                        return res;
                    }),
                    new Func<Vector<double>, Vector<double>, Matrix<double>>(delegate (Vector<double> vd, Vector<double> ox) // derivatives
                    {
                        var prime = new DenseMatrix(pnts.Length, 7);

                        double a = vd[0]; // rotation around x-axis
                        double b = vd[1]; // rotation around y-axis
                        double c = vd[2]; // rotation around z-axis
                        double f = vd[3]; // scaling factor
                        double sa = Math.Sin(a);
                        double ca = Math.Cos(a);
                        double sb = Math.Sin(b);
                        double cb = Math.Cos(b);
                        double sc = Math.Sin(c);
                        double cc = Math.Cos(c);
                        GeoVector t = new GeoVector(vd[4], vd[5], vd[6]); // translation 
                        Matrix m = DenseMatrix.CreateDiagonal(3, 3, f) *
                            DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                            DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                            DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                        ModOp trsf = new ModOp(m.ToArray(), t);
                        for (int i = 0; i < pnts.Length; i++)
                        {
                            GeoPoint p = trsf * pnts[i];
                            GeoPoint2D uv = PositionOf(p);
                            GeoPoint o = PointAt(uv);
                            GeoVector n = GetNormal(uv).Normalized;

                            prime[i, 0] = f * n.y * (sb * sc * (ca * p.y - sa * p.z) + cc * (-ca * p.z - sa * p.y)) + f * n.x * (sb * cc * (ca * p.y - sa * p.z) - sc * (-ca * p.z - sa * p.y)) + cb * f * n.z * (ca * p.y - sa * p.z);
                            prime[i, 1] = f * n.z * (-sb * (ca * p.z + sa * p.y) - cb * p.x) + sc * f * n.y * (cb * (ca * p.z + sa * p.y) - sb * p.x) + cc * f * n.x * (cb * (ca * p.z + sa * p.y) - sb * p.x);
                            prime[i, 2] = f * n.z * (-sb * (ca * p.z + sa * p.y) - cb * p.x) + sc * f * n.y * (cb * (ca * p.z + sa * p.y) - sb * p.x) + cc * f * n.x * (cb * (ca * p.z + sa * p.y) - sb * p.x);
                            prime[i, 3] = n.y * (sc * (sb * (ca * p.z + sa * p.y) + cb * p.x) + cc * (ca * p.y - sa * p.z)) + n.x * (cc * (sb * (ca * p.z + sa * p.y) + cb * p.x) - sc * (ca * p.y - sa * p.z)) + n.z * (cb * (ca * p.z + sa * p.y) - sb * p.x);
                            prime[i, 4] = n.x;
                            prime[i, 5] = n.y;
                            prime[i, 6] = n.z;
                        }
                        return prime;
                    }), observedX, observedY);
                mres = lm.FindMinimum(iom, new DenseVector(new double[] { 0, 0, 0, 1, 0, 0, 0 }));
                if (true)
                {
                    double a = mres.MinimizingPoint[0]; // rotation around x-axis
                    double b = mres.MinimizingPoint[1]; // rotation around y-axis
                    double c = mres.MinimizingPoint[2]; // rotation around z-axis
                    double f = mres.MinimizingPoint[3]; // scaling factor
                    double sa = Math.Sin(a);
                    double ca = Math.Cos(a);
                    double sb = Math.Sin(b);
                    double cb = Math.Cos(b);
                    double sc = Math.Sin(c);
                    double cc = Math.Cos(c);
                    GeoVector t = new GeoVector(mres.MinimizingPoint[4], mres.MinimizingPoint[5], mres.MinimizingPoint[6]); // translation 
                    Matrix m = DenseMatrix.CreateDiagonal(3, 3, f) *
                        DenseMatrix.OfArray(new double[,] { { cc, -sc, 0 }, { sc, cc, 0 }, { 0, 0, 1 } }) *
                        DenseMatrix.OfArray(new double[,] { { cb, 0, sb }, { 0, 1, 0 }, { -sb, 0, cb } }) *
                        DenseMatrix.OfArray(new double[,] { { 1, 0, 0 }, { 0, ca, -sa }, { 0, sa, ca } });
                    ModOp trsf = new ModOp(m.ToArray(), t);
#if DEBUG
                    double err1 = 0.0;
                    double derr1 = 0.0;
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        GeoPoint p = trsf * pnts[i];
                        GeoPoint2D uv = PositionOf(p);
                        GeoPoint o = PointAt(uv);
                        GeoVector n = GetNormal(uv).Normalized;
                        double r = n * (p - o); // (signed) distance from the plane
                        err1 += r * r;
                        derr1 += GetDistance(trsf * pnts[i]);
                    }
#endif
                    Modify(trsf.GetInverse());
#if DEBUG
                    double err2 = 0.0;
                    double derr2 = 0.0;
                    for (int i = 0; i < pnts.Length; i++)
                    {
                        GeoPoint p = pnts[i];
                        GeoPoint2D uv = PositionOf(p);
                        GeoPoint o = PointAt(uv);
                        GeoVector n = GetNormal(uv).Normalized;
                        double r = n * (p - o); // (signed) distance from the plane
                        err2 += r * r;
                        derr2 += GetDistance(pnts[i]);
                    }
#endif
                }
            }
#if DEBUG
            {
                double err = 0.0;
                for (int i = 0; i < pnts.Length; i++) err += pnts[i] & PointAt(PositionOf(pnts[i])); // squared distance
            }
#endif
            return mres.ModelInfoAtMinimum.Value;
        }
        public abstract IPropertyEntry GetPropertyEntry(IFrame frame);
#if DEBUG
        // Starte mit dem Mittelpunkt
        // Betrache die Kurve f(u) = u²*d2+u*d1+d0 (d2 ist die 2. Ableitung in der Richtung der beiden Punkte, d1 die 1., d0 der Punkt selbst),
        // Der Abstand f(u) zur Sekante ist gegeben durch |(f(u)-sp3d)^(f(u)-ep3d)|, welches es zu maximieren gilt, also Ableitung = 0
        // Da das zu schwierig ist, suche eine ModOp, die sp3d->(0,0,0) und ep3d->(1,0,0) abbildet (nur Skalierung und Drehung, keine Verzerrung), dann unterscheiden
        // sich die beiden Vektoren des Kreuzprodukts nur in der x-Komponente, das Ergebnis ist
        // aber immer noch 4. Potenz in und davon muss noch die Länge genommen werden, also 8. Potenz, das ist nicht lösbar
        // Wenn man das aber ausrechnet bleibt für das Kreuzprodukt nur f(u), also 2. Potenz:
        // [0, f(u).z*(f(u).x-1) - (f(u).z*f(u).x, f(u).x*f(u).y-f(u).y*(f(u).x-1)] = 
        // [0, f(u).z*((f(u).x-1) - f(u).x), f(u).y*(f(u).x-(f(u).x-1))] = 
        // [0, f(u).z*(-1), f(u).y*(1))]
        // Die Länge davon ist 4. Potenz, Abgeleitet 3. Potenz, das kann man 0 setzen!
        // (u^2*d2z+u*d1z+d0z)^2+(u^2*d2y+u*d1y+d0y)^2                                         (1*)
        // die 2. Ableitung: 2*(d1z+2*d2z*u)^2+2*(d1y+2*d2y*u)^2+4*d2z*(d0z+d1z*u+d2z*u^2)+4*d2y*(d0y+d1y*u+d2y*u^2)
        // um Minimum von Maximum unterscheiden zu können

        // Dazu Maxima Eingabe:
        // (u^2*d2z+u* d1z+d0z)^2+(u^2*d2y+u* d1z+d0y)^2; /* Länge des (nicht normierten quadratischen) Kreuzproduktes gemäß (1*); */;
        // diff(%, u);
        // u^3*ratsimp(%/u^3); /* Koeefizienten von u bestimmen */;

        // Ergibt: (4*d2y^2+4*d2z^2)*u^3+(6*d1z*d2y+6*d1z*d2z)*u^2+(4*d1z^2+4*d0y*d2y+4*d0z*d2z)*u+(2*d0y+2*d0z)*d1z

        // Der Versuch, die 3D Kurve anzunähern bringt nichts:
        public virtual double MaxDistN(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp)
        {
            GeoPoint sp3d = PointAt(sp);
            GeoPoint ep3d = PointAt(ep);
            GeoVector dir = ep3d - sp3d;
            double len = dir.Length;
            double cosa = dir * GeoVector.XAxis / len;
            double sina = Math.Sqrt(1 - cosa * cosa);
            ModOp toUnit = ModOp.Scale(1.0 / len) * ModOp.Rotate(dir ^ GeoVector.XAxis, sina, cosa) * ModOp.Translate(-sp3d.x, -sp3d.y, -sp3d.z);

            mp = new GeoPoint2D(sp, ep);
            //double dbg1 = 1.0, dbg2 = 1.0, dbg3 = 1.0; das war der Versuch, die richtigen Faktoren für d2 zu finden
            //for (int ii = 0; ii < 27; ii++)
            //{
            //    switch (ii % 3)
            //    {
            //        case 0: dbg1 = 0.5; break;
            //        case 1: dbg1 = 1.0; break;
            //        case 2: dbg1 = 2.0; break;
            //    }
            //    switch ((ii / 3) % 3)
            //    {
            //        case 0: dbg2 = 0.5; break;
            //        case 1: dbg2 = 1.0; break;
            //        case 2: dbg2 = 2.0; break;
            //    }
            //    switch ((ii / 9) % 3)
            //    {
            //        case 0: dbg3 = 0.5; break;
            //        case 1: dbg3 = 1.0; break;
            //        case 2: dbg3 = 2.0; break;
            //    }
            //    System.Diagnostics.Trace.WriteLine("dbg1, dbg2, dbg3: " + dbg1.ToString() + ", " + dbg2.ToString() + ", " + dbg3.ToString());

            double u0 = 0.5;
            for (int k = 0; k < 10; ++k)
            {
                mp = sp + u0 * (ep - sp);
                GeoPoint location;
                GeoVector du, dv, duu, dvv, duv;
                this.Derivation2At(mp, out location, out du, out dv, out duu, out dvv, out duv);

                double len2 = sp | ep;
                double a = (ep.x - sp.x) / len2;
                double b = (ep.y - sp.y) / len2;
                GeoVector d1 = a * (toUnit * du) + b * (toUnit * dv);
                GeoVector d2 = 0.5 * (a * a * 1.0 * (toUnit * duu) + b * b * 1.0 * (toUnit * dvv) + a * b * 1.0 * (toUnit * duv));
                GeoPoint d0 = toUnit * location; // sind die Koeffizienten für f(u), f(0) liefert d0, u ist also auf mp bezogen
                GeoPoint foot = Geometry.DropPL(location, sp3d, ep3d);
                double err = d1.Normalized * (toUnit * (foot - location).Normalized);
                if (Math.Abs(err) < 1e-5) return location | foot; // der Winkel zwischen dem Lot und der Kurvenrichtung ist fast Null
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                ModOp fromUnit = toUnit.GetInverse();
                List<GeoPoint> dbgpnts = new List<GeoPoint>();
                List<GeoPoint> dbgsrf = new List<GeoPoint>();
                for (double d = -0.5; d <= 0.5; d += 0.01)
                {
                    GeoPoint pdbg = fromUnit * (d0 + d * d * d2 + d * d1);
                    dbgpnts.Add(pdbg);
                    pdbg = PointAt(sp + (d + 0.5) * (ep - sp));
                    dbgsrf.Add(pdbg);
                }
                Polyline pldbg = Polyline.Construct();
                pldbg.SetPoints(dbgpnts.ToArray(), false);
                dc.Add(pldbg);
                pldbg = Polyline.Construct();
                pldbg.SetPoints(dbgsrf.ToArray(), false);
                dc.Add(pldbg);
                Line dbgline = Line.Construct();
                dbgline.SetTwoPoints(sp3d, ep3d);
                dc.Add(dbgline);
#endif
                double[] x = new double[3];
                int n = Geometry.ragle3fast(4 * d2.y * d2.y + 4 * d2.z * d2.z, 6 * d1.y * d2.y + 6 * d1.z * d2.z, (2 * d1.y * d1.y + 2 * d1.z * d1.z + 4 * d0.y * d2.y + 4 * d0.z * d2.z), 2 * d0.z * d1.z + 2 * d0.y * d1.y, x);
                double offset = double.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    if (Math.Abs(x[i]) < Math.Abs(offset))
                    {
                        double diff2 = 2 * sqr(d1.z + 2 * d2.z * x[i]) + 2 * sqr(d1.y + 2 * d2.y * x[i]) + 4 * d2.z * (d0.z + d1.z * x[i] + d2.z * x[i] * x[i]) + 4 * d2.y * (d0.y + d1.y * x[i] + d2.y * x[i] * x[i]);
                        // nur wenn die 2. Ableitung negativ ist, ist es ein Maximum
                        if (diff2 <= 0.0)
                        {
                            offset = x[i];
                            // wenn wir außerhalb von [0,1] geraten, dann auf [0,1] eingrenzen
                            if (k == 0)
                            {   // Wenn es einen Wendepunkt gibt im Segment, dann kann das im 1. Schritt (bei 0.5) rausfliegen
                                if (u0 + offset <= 0.0) offset = -0.25;
                                if (u0 + offset >= 1.0) offset = 0.25;
                            }
                        }
                    }
                }
                if (u0 + offset > 0.0 && u0 + offset < 1.0)
                {
                    u0 += offset;
                }
                else
                    break;
#if DEBUG
                System.Diagnostics.Trace.WriteLine("err, offest: " + err.ToString() + ", " + offset.ToString());
                GeoPoint pu0 = fromUnit * (d0 + offset * offset * d2 + offset * d1);
                GeoPoint ft = Geometry.DropPL(pu0, sp3d, ep3d);
                dbgline = Line.Construct();
                dbgline.SetTwoPoints(pu0, ft);
                dc.Add(dbgline);
                pu0 = PointAt(sp + u0 * (ep - sp));
                dbgline = Line.Construct();
                dbgline.SetTwoPoints(pu0, ft);
                dc.Add(dbgline);
#endif
            }
            //}
            {   // das Verfahren konvergiert nicht, zumindest nicht in dem Abschnitt
                // das sollte nur vorkommen, wenn die Funktion ausgeartet ist, also Ableitungen 0 oder so
                double max = 0;
                mp = GeoPoint2D.Origin; // wg. Compiler
                for (double d = 0.25; d < 1; d += 0.25)
                {
                    GeoPoint2D mpd = new GeoPoint2D(sp, ep, d);
                    GeoPoint mp3d = PointAt(mpd);
                    double dd = Geometry.DistPL(mp3d, sp3d, ep3d);
                    if (dd > max)
                    {
                        mp = mpd;
                        max = dd;
                    }
                }
                return max;
            }
        }
#endif
        protected static double sqr(double x) { return x * x; }
        protected static double cube(double x) { return x * x * x; }
        protected static double quad(double x) { return x * x * x * x; }
        protected static double exp32(double x) { return Math.Sqrt(x * x * x); }
        protected static double exp52(double x) { return Math.Sqrt(x * x * x * x * x); }
        public virtual double MaxDist(GeoPoint2D sp, GeoPoint2D ep, out GeoPoint2D mp)
        {
            GeoPoint sp3d, ep3d; // start und enpunkt in 3d, der maximale Abstand zu dieser Linie wird gesucht
            sp3d = PointAt(sp);
            ep3d = PointAt(ep);
            // Kurzfassung zur Überbrückung der Probleme an 3 Stellen messen:
            double max = 0;
            mp = GeoPoint2D.Origin; // wg. Compiler
            for (double d = 0.25; d < 1; d += 0.25)
            {
                GeoPoint2D mpd = new GeoPoint2D(sp, ep, d);
                GeoPoint mp3d = PointAt(mpd);
                double dd = Geometry.DistPL(mp3d, sp3d, ep3d);
                if (dd > max)
                {
                    mp = mpd;
                    max = dd;
                }
            }
            return max;

            //Unreachable code
            /*
            BoxedSurfaceEx.RawPointNormalAt(sp, out sp3d, out sn); // Punkt und Normale auf die Fläche am Startpunkt
            BoxedSurfaceEx.RawPointNormalAt(ep, out ep3d, out en); // Punkt und Normale auf die Fläche am Startpunkt
            d3d = ep3d - sp3d; // die Richtung der 3d-Linie
            double md = -1.0;
            mp = new GeoPoint2D(sp, ep); // falls kein Punkt gefunden wird
            if (new SweepAngle(sn, en).Radian < 0.1)
            {   // die Fläche könnte z.B. eine Ebene sein, bei der u oder v-Richtung gebogen sind.
                // dann sind die Normalenvektoren alle gleich und immer senkrecht zu der Verbindung sp3d - ep3d
                // newton liefert dann  Mist, deshalb hier einfach Bisektion:
            }
            else
            {
                GeoPoint2D[] t = newtonFindTangent(d3d, sp, ep, sn, en, (sp | ep) * 1e-4);
                for (int i = 0; i < t.Length; i++)
                {
                    GeoPoint2D p = t[i];
                    double d = Geometry.DistPL(BoxedSurfaceEx.RawPointAt(p), sp3d, ep3d);
                    if (d > md)
                    {
                        md = d;
                        mp = t[i];
                    }
                }
            }
            if (md < 0)
            {   // hier wie obige Bedingung oder nichts mit newton gefundn
                // hier nicht "RawPointAt" verwenden, denn es gibt ein Problem bei einer in sich verzerrten Ebene. Da liefert diese Funktion falsche Ergebnisse
                //GeoPoint mp0 = BoxedSurfaceEx.RawPointAt(sp);
                //GeoPoint mp3 = BoxedSurfaceEx.RawPointAt(ep);
                GeoPoint mp0 = sp3d = PointAt(sp);
                GeoPoint mp3 = ep3d = PointAt(ep);
                GeoPoint2D spi = sp; // schnurren zusammen
                GeoPoint2D epi = ep;
                for (int i = 0; i < 10; i++)
                {
                    //GeoPoint mp1 = BoxedSurfaceEx.RawPointAt(new GeoPoint2D(spi, epi, 1 / 3.0));
                    //GeoPoint mp2 = BoxedSurfaceEx.RawPointAt(new GeoPoint2D(spi, epi, 2 / 3.0));
                    GeoPoint mp1 = PointAt(new GeoPoint2D(spi, epi, 1 / 3.0));
                    GeoPoint mp2 = PointAt(new GeoPoint2D(spi, epi, 2 / 3.0));
                    if (Geometry.DistPL(mp1, sp3d, ep3d) < Geometry.DistPL(mp2, sp3d, ep3d))
                    {   // am Anfang ersetzen
                        spi = new GeoPoint2D(spi, epi, 1 / 3.0);
                        mp0 = mp1;
                    }
                    else
                    {   // am Ende ersetzen
                        epi = new GeoPoint2D(spi, epi, 2 / 3.0);
                        mp3 = mp2;
                    }
                }
                mp = new GeoPoint2D(spi, epi);
                md = Geometry.DistPL(PointAt(mp), sp3d, ep3d);
            }
#if DEBUG
            //DebuggerContainer dc = new DebuggerContainer();
            //Line dbgl = Line.Construct();
            //dbgl.SetTwoPoints(PointAt(sp), PointAt(ep));
            //dc.Add(dbgl);
            //GeoPoint ppmm = PointAt(mp);
            //Line dbgl1 = Line.Construct();
            //dbgl1.SetTwoPoints(ppmm, PointAt(sp));
            //dc.Add(dbgl1);
            //Line dbgl2 = Line.Construct();
            //dbgl2.SetTwoPoints(ppmm, PointAt(ep));
            //dc.Add(dbgl2);
            //GeoPoint[] pg1 = new GeoPoint[100];
            //GeoPoint[] pg2 = new GeoPoint[100];
            //for (int i = 0; i < 100; i++)
            //{
            //    GeoPoint2D uv = new GeoPoint2D(sp, ep, i / 100.0);
            //    pg1[i] = PointAt(uv);
            //    BoxedSurfaceEx.RawPointNormalAt(uv, out pg2[i], out sn); // Punkt und Normale auf die Fläche am Startpunkt
            //}
            //Polyline pl1 = Polyline.Construct();
            //Polyline pl2 = Polyline.Construct();
            //try
            //{
            //    pl1.SetPoints(pg1, false);
            //    dc.Add(pl1, 1);
            //}
            //catch (PolylineException) { }
            //try
            //{
            //    pl2.SetPoints(pg2, false);
            //    dc.Add(pl2, 2);
            //}
            //catch (PolylineException) { }
#endif
            return md;
            */
        }

        private GeoPoint2D[] newtonFindTangent(GeoVector dir3d, GeoPoint2D sp, GeoPoint2D ep, GeoVector sn, GeoVector en, double precision)
        {
            double scsn = dir3d * sn; // Skalarprodukt am Anfang und am Ende, sollte verschiedene Vorzeichen haben, sonst schneidet die Linie die Fläche
            double scen = dir3d * en;
            if (Math.Sign(scsn) == Math.Sign(scen))
            {   // gleiche Richtung, d.h. aufteilen, sollte selten vorkommen
                if (scsn == 0.0)
                {   // an beiden Stellen tangential, wir suchen hier nur dazwischen, wenn in der Mitte nicht tangential ist
                    GeoPoint2D mp = new GeoPoint2D(sp, ep);
                    GeoPoint mp3d;
                    GeoVector mn;
                    BoxedSurfaceEx.RawPointNormalAt(mp, out mp3d, out mn); // Punkt und Normale auf die Fläche am Mittelpunkt
                    double scmn = dir3d * mn;
                    if (scmn != 0.0)
                    {
                        GeoPoint2D[] t1 = newtonFindTangent(dir3d, sp, mp, sn, mn, precision);
                        GeoPoint2D[] t2 = newtonFindTangent(dir3d, mp, ep, mn, en, precision);
                        if (t1.Length == 0) return t2;
                        if (t2.Length == 0) return t1;
                        GeoPoint2D[] res = new GeoPoint2D[t1.Length + t2.Length];
                        Array.Copy(t1, res, t1.Length);
                        Array.Copy(t2, 0, res, t1.Length, t2.Length);
                        return res;
                    }
                    else
                    {
                        return new GeoPoint2D[0]; // d.h. überall tangential
                    }
                }
                else
                {   // gleiche Richtung, in der Mitte aufteilen, wenn nicht schon zu klein
                    if ((sp | ep) < precision) return new GeoPoint2D[0];
                    GeoPoint2D mp = new GeoPoint2D(sp, ep);
                    GeoPoint mp3d;
                    GeoVector mn;
                    BoxedSurfaceEx.RawPointNormalAt(mp, out mp3d, out mn); // Punkt und Normale auf die Fläche am Mittelpunkt
                    double scmn = dir3d * mn;
                    if (Math.Sign(scmn) != Math.Sign(scsn))
                    {
                        GeoPoint2D[] t1 = newtonFindTangent(dir3d, sp, mp, sn, mn, precision);
                        GeoPoint2D[] t2 = newtonFindTangent(dir3d, mp, ep, mn, en, precision);
                        if (t1.Length == 0) return t2;
                        if (t2.Length == 0) return t1;
                        GeoPoint2D[] res = new GeoPoint2D[t1.Length + t2.Length];
                        Array.Copy(t1, res, t1.Length);
                        Array.Copy(t2, 0, res, t1.Length, t2.Length);
                        return res;
                    }
                    else
                    {   // pathologischer Fall: in der Nähe einer Singularität oder so
                        return new GeoPoint2D[0]; // d.h. überall tangential
                    }
                }
            }
            else
            {
                bool doNewton = true; // wenn das Ergebnis mal schlechter wird, dann auf Bisection umschalten
                int sgnsp = Math.Sign(scsn);
                int sgnep = Math.Sign(scen);
                if (sgnsp == 0) sgnsp = -sgnep;
                if (sgnep == 0) sgnep = -sgnsp; // jetzt sicher -1 und +1
                int dbgc = 0;
                while ((sp | ep) > precision && dbgc < 10)
                {
                    ++dbgc;
                    double s = 0.5;
                    if (doNewton)
                    {
                        s = -scsn / (scen - scsn); // sollte zwischen 0 und 1 liegen, da verschiedene Vorzeichen wird nie durch 0 geteilt
                        if (s < 0.1) s = 0.1;
                        if (s > 0.9) s = 0.9; // damit man nicht am Anfang oder Ende kleben bleibt
                    }
                    GeoPoint2D mp = Geometry.LinePos(sp, ep, s);
                    // GeoVector mn = GetNormal(mp);// Normalenvektor am Zwischenpunkt, leider normiert, passt nicht mit dem anderen zusammen
                    GeoPoint mp3d;
                    GeoVector mn;
                    BoxedSurfaceEx.RawPointNormalAt(mp, out mp3d, out mn); // Punkt und Normale auf die Fläche am Mittelpunkt
                    double scmn = dir3d * mn;
                    if (scmn == 0.0) return new GeoPoint2D[] { mp }; // genauer Treffer
                    if (Math.Sign(scmn) != sgnsp)
                    {   // mit dem Anfangsstück weitermachen
                        ep = mp;
                        if (Math.Abs(scen) < Math.Abs(scmn)) doNewton = false; // muss kleiner werden, sonst kein Newton
                        scen = scmn;
                    }
                    else
                    {
                        sp = mp;
                        if (Math.Abs(scsn) < Math.Abs(scmn)) doNewton = false;
                        scsn = scmn;
                    }
                }
                return new GeoPoint2D[] { new GeoPoint2D(sp, ep) };
            }

        }

        private bool NewtonIntersect(Polynom implicitSurface, BoundingRect uvExtent, ICurve curve, out double[] uOnCurve3Ds)
        {
            TetraederHull th = new TetraederHull(curve);
            double[] ips = th.Intersect(implicitSurface);
            uOnCurve3Ds = null;
            return false;
        }
        private bool FindExtreme(GeoVector dir, GeoPoint2D par1, GeoPoint2D par2, GeoPoint2D par3, GeoVector v1, GeoVector v2, GeoVector v3, double epsu, double epsv, out GeoPoint2D p)
        {   // die Vektoren sind nicht normiert, es kann vorkommen, dass es null-Vektoren gibt
            // An 3 Stellen der Oberfläche sind die Normalenvektoeren gegeben. Wenn sie die gewünschte Richtung einschließen
            // dann sollte die Stelle an der der Normalenvektor und dir identisch sind innerhalb des dreiecks liegen
            // bei Sattelflächen kann es auch nach außen wendern
            Matrix m = DenseMatrix.OfRowArrays(v1, v2, v3);
            Vector b = new DenseVector(dir);
            try
            {
                Vector s = (Vector)m.Transpose().Solve(b);
                if ((s[0] >= 0.0 && s[1] >= 0.0 && s[2] >= 0.0) ||
                    (s[0] <= 0.0 && s[1] <= 0.0 && s[2] <= 0.0))
                {   // die gesuchte Richtung wird positiv oder negativ aufgespannt
                    if (Math.Abs(par1.x - par2.x) + Math.Abs(par2.x - par3.x) + Math.Abs(par3.x - par1.x) > epsu ||
                    Math.Abs(par1.y - par2.y) + Math.Abs(par2.y - par3.y) + Math.Abs(par3.y - par1.y) > epsv)
                    {   // noch nicht genau genug
                        // Zwischenpunkte betrachten: die Dreieckseiten werden halbiert und die vier entstehenden
                        // Dreiecke weiter betrachtet
                        GeoPoint2D par12 = new GeoPoint2D(par1, par2);
                        GeoPoint2D par23 = new GeoPoint2D(par2, par3);
                        GeoPoint2D par31 = new GeoPoint2D(par3, par1);
                        GeoVector v12 = GetNormal(par12);
                        GeoVector v23 = GetNormal(par23);
                        GeoVector v31 = GetNormal(par31);
                        // die 4 Teildreicke betrachten
                        if (FindExtreme(dir, par12, par23, par31, v12, v23, v31, epsu, epsv, out p)) return true;
                        if (FindExtreme(dir, par1, par12, par31, v1, v12, v31, epsu, epsv, out p)) return true;
                        if (FindExtreme(dir, par2, par12, par23, v2, v12, v23, epsu, epsv, out p)) return true;
                        if (FindExtreme(dir, par3, par23, par31, v3, v23, v31, epsu, epsv, out p)) return true;
                        // hier angekommen liegt das gesuchte Ergebnis außerhalb des Dreiecks durch par1,par2,par3
                        // Das passiert bei Sattelflächen (z.B. beim Torus)
                        // es ist also eine der drei ursprünglichen Dreiecksseiten, aus dem wir hier rausgefallen sind
                        m = DenseMatrix.OfRowArrays(v1, v12, v2);
                        try
                        {
                            s = (Vector)m.Transpose().Solve(b);
                            if ((s[0] >= 0.0 && s[1] >= 0.0 && s[2] >= 0.0) ||
                                (s[0] <= 0.0 && s[1] <= 0.0 && s[2] <= 0.0))
                            {
                                // die Verbindung par1<->par2 macht ein Problem
                                // spiegele par3 an par12 und suche in den beiden Dreiecken
                                // die Seite par1<->par2 kommt in der neuen Suche nicht vor
                                GeoPoint2D par4 = par12 + (par12 - par3);
                                GeoVector v4 = GetNormal(par4).Normalized;
                                if (FindExtreme(dir, par1, par12, par4, v1, v12, v4, epsu, epsv, out p)) return true;
                                if (FindExtreme(dir, par2, par12, par4, v2, v12, v4, epsu, epsv, out p)) return true;
                            }
                        }
                        catch (ApplicationException) { } // macht nix, liegen in einer Ebene, waren also nicht der Auslöser für das Problem
                        // analog mit den beiden anderen Seiten
                        m = DenseMatrix.OfRowArrays(v2, v23, v3);
                        try
                        {
                            s = (Vector)m.Transpose().Solve(b);
                            if ((s[0] >= 0.0 && s[1] >= 0.0 && s[2] >= 0.0) ||
                                (s[0] <= 0.0 && s[1] <= 0.0 && s[2] <= 0.0))
                            {
                                GeoPoint2D par4 = par23 + (par23 - par1);
                                GeoVector v4 = GetNormal(par4).Normalized;
                                if (FindExtreme(dir, par2, par23, par4, v2, v23, v4, epsu, epsv, out p)) return true;
                                if (FindExtreme(dir, par3, par23, par4, v3, v23, v4, epsu, epsv, out p)) return true;
                            }
                        }
                        catch (ApplicationException) { }
                        m = DenseMatrix.OfRowArrays(v3, v31, v1);
                        try
                        {
                            s = (Vector)m.Transpose().Solve(b);
                            if ((s[0] >= 0.0 && s[1] >= 0.0 && s[2] >= 0.0) ||
                                (s[0] <= 0.0 && s[1] <= 0.0 && s[2] <= 0.0))
                            {
                                GeoPoint2D par4 = par31 + (par31 - par2);
                                GeoVector v4 = GetNormal(par4).Normalized;
                                if (FindExtreme(dir, par3, par31, par4, v3, v31, v4, epsu, epsv, out p)) return true;
                                if (FindExtreme(dir, par1, par31, par4, v1, v31, v4, epsu, epsv, out p)) return true;
                            }
                        }
                        catch (ApplicationException) { }

                        return false; // sollte nicht drankommen
                    }
                    else
                    {   // die Mitte nehmen
                        p = new GeoPoint2D((par1.x + par2.x + par3.x) / 3.0, (par1.y + par2.y + par3.y) / 3.0);
                        return true;
                    }
                }
                // hier liegt die Richtung nicht in der aufgespannten Fläche
            }
            catch (ApplicationException)
            {   // nicht lösbar, also eben oder in einer Richtung linear
            }
            p = GeoPoint2D.Origin;
            return false;
        }
        private bool ApproxExtreme(GeoVector dir, GeoPoint2D par1, GeoPoint2D par2, GeoPoint2D par3, GeoPoint2D par4, GeoVector v1, GeoVector v2, GeoVector v3, GeoVector v4, double epsu, double epsv, out GeoPoint2D p)
        {
            // KONVERGIERT NICHT GUT, man müsste mit den 2. Ableitungen arbeiten
            // Alle Vektoren sind normiert.
            // Bestimme den schlechtesten, damit dieser nachher ausgetauscht wird
            int worst = 0;
            double d = double.MaxValue;
            double sc = dir * v1;
            if (sc < d)
            {
                worst = 1;
                d = sc;
            }
            sc = Math.Abs(dir * v2);
            if (sc < d)
            {
                worst = 2;
                d = sc;
            }
            sc = Math.Abs(dir * v3);
            if (sc < d)
            {
                worst = 3;
                d = sc;
            }
            sc = Math.Abs(dir * v4);
            if (sc < d)
            {
                worst = 4;
                d = sc;
            }
            while (d < 1 - 1e-6)
            {
                // Gleichungssystem:
                // a1*par1u + b1*par1v + c1 = v1x
                // a2*par1u + b2*par1v + c2 = v1y
                // a3*par1u + b3*par1v + c3 = v1z und das Gleiche mit par2, par3 und par4 bzw v2, v3 und v4
                // ergibt 12 Gleichungen mit 9 Unbekannten. Würde man nur die drei auf einer Linie liegenden
                // parameterwerte nehmen, wäre das System linear abhängig
                // Aus der Lösung (a1,b1,c1,a2,b2,c2,a3,b3,c3) ergibt sich folgendes:
                // a1*paru + b1*parv + c1 = dirx
                // a2*paru + b2*parv + c2 = diry
                // a3*paru + b3*parv + c3 = dirz wobei hier paru ubd parv gesucht sind. Zu beachten ist, dass elle Vekoren
                // normiert sein müssen. Das System ist überbestimmt
                Matrix m = new DenseMatrix(12, 9);
                m[0, 0] = par1.x; m[0, 1] = par1.y; m[0, 2] = 1.0;
                m[1, 3] = par1.x; m[1, 4] = par1.y; m[1, 5] = 1.0;
                m[2, 6] = par1.x; m[2, 7] = par1.y; m[2, 8] = 1.0;

                m[3, 0] = par2.x; m[3, 1] = par2.y; m[3, 2] = 1.0;
                m[4, 3] = par2.x; m[4, 4] = par2.y; m[4, 5] = 1.0;
                m[5, 6] = par2.x; m[5, 7] = par2.y; m[5, 8] = 1.0;

                m[6, 0] = par3.x; m[6, 1] = par3.y; m[6, 2] = 1.0;
                m[7, 3] = par3.x; m[7, 4] = par3.y; m[7, 5] = 1.0;
                m[8, 6] = par3.x; m[8, 7] = par3.y; m[8, 8] = 1.0;

                m[9, 0] = par3.x; m[9, 1] = par3.y; m[9, 2] = 1.0;
                m[10, 3] = par3.x; m[10, 4] = par3.y; m[10, 5] = 1.0;
                m[11, 6] = par3.x; m[11, 7] = par3.y; m[11, 8] = 1.0;

                Vector b = new DenseVector(12);
                b[0] = v1.x;
                b[1] = v1.y;
                b[2] = v1.z;
                b[3] = v2.x;
                b[4] = v2.y;
                b[5] = v2.z;
                b[6] = v3.x;
                b[7] = v3.y;
                b[8] = v3.z;
                b[9] = v4.x;
                b[10] = v4.y;
                b[11] = v4.z;
                try
                {
                    Vector s = (Vector)m.Solve(b);
                    m = new DenseMatrix(3, 2);
                    m[0, 0] = s[0];
                    m[0, 1] = s[1];
                    m[1, 0] = s[3];
                    m[1, 1] = s[4];
                    m[2, 0] = s[6];
                    m[2, 1] = s[7];
                    b = new DenseVector(3);
                    b[0] = dir.x - s[2];
                    b[1] = dir.y - s[5];
                    b[2] = dir.z - s[8];
                    s = (Vector)m.Solve(b);
                    p.x = s[0];
                    p.y = s[1];
                    GeoVector v = GetNormal(p).Normalized;

                    double newsc = dir * v;
                    if (newsc < d)
                    {   // konvergiert nicht
                        p = GeoPoint2D.Origin;
                        return false;
                    }
                    switch (worst)
                    {
                        case 1:
                            par1 = p;
                            v1 = v;
                            break;
                        case 2:
                            par2 = p;
                            v2 = v;
                            break;
                        case 3:
                            par3 = p;
                            v3 = v;
                            break;
                        case 4:
                            par4 = p;
                            v4 = v;
                            break;
                    }
                    d = double.MaxValue;
                    sc = Math.Abs(dir * v1);
                    if (sc < d)
                    {
                        worst = 1;
                        d = sc;
                    }
                    sc = Math.Abs(dir * v2);
                    if (sc < d)
                    {
                        worst = 2;
                        d = sc;
                    }
                    sc = Math.Abs(dir * v3);
                    if (sc < d)
                    {
                        worst = 3;
                        d = sc;
                    }
                    sc = Math.Abs(dir * v4);
                    if (sc < d)
                    {
                        worst = 4;
                        d = sc;
                    }
                }
                catch (ApplicationException)
                {
                    p = GeoPoint2D.Origin;
                    return false;
                }
            }
            p = new GeoPoint2D((par1.x + par2.x + par3.x + par4.x) / 4.0, (par1.y + par2.y + par3.y + par4.y) / 4.0);
            GeoVector dbg = GetNormal(p).Normalized;
            return true;
        }
        internal static void Debug(Model m)
        {
        }
        internal void CheckZMinMax(Projection p, double u, double v, ref double zMin, ref double zMax)
        {
            GeoPoint pp = p.UnscaledProjection * (this as ISurface).PointAt(new GeoPoint2D(u, v));
            if (pp.z < zMin) zMin = pp.z;
            if (pp.z > zMax) zMax = pp.z;
        }
        public BSpline Refine(GeoPoint[] geopoints, int degree, bool closed, PlaneSurface pl, double precision)
        {
            Plane pln = new Plane(pl.Location, pl.DirectionX, pl.DirectionY);
            List<GeoPoint> p = new List<GeoPoint>(geopoints);
            BSpline bsp = BSpline.Construct();
            bsp.ThroughPoints(p.ToArray(), degree, closed);
            return bsp; //Debug

            //Unreachable code
            /*
            int maxThroughPoints = 200;
            int next_index = 1;
            int initp = p.Count;
            bool tocheck = false;
            //System.Diagnostics.Trace.WriteLine("==> Einmal mit: "+ p.Count + " Punkten");
            double us = 0;
            double ue = 1;
            do
            {
                double um = us;
                int i = next_index;
                if (i == 1)
                {
                    //um = (us + (bsp as ICurve).PositionOf(p[i])) / 2;
                    um = (us + bsp.PositionOfThroughPoint(i)) / 2;
                }
                else if (i == p.Count - 1)
                {
                    //um = ((bsp as ICurve).PositionOf(p[i - 1]) + ue) / 2;
                    um = (bsp.PositionOfThroughPoint(i) + ue) / 2;
                }
                else
                {
                    //um = ((bsp as ICurve).PositionOf(p[i - 1]) + (bsp as ICurve).PositionOf(p[i])) / 2;
                    um = (bsp.PositionOfThroughPoint(i - 1) + bsp.PositionOfThroughPoint(i)) / 2;
                }

                GeoPoint pm = (bsp as ICurve).PointAt(um);
                GeoVector dirline = pln.Normal ^ (bsp as ICurve).DirectionAt(um);
                GeoPoint2D[] sp = GetLineIntersection(pm, dirline);
                GeoPoint pcor = new GeoPoint();
                if (sp.Length != 0)
                {
                    double dmi = double.MaxValue;
                    for (int k = 0; k < sp.Length; k++)
                    {
                        GeoPoint tmp = (this as ISurface).PointAt(sp[k]);
                        double d = Geometry.Dist(pm, tmp);
                        if (d < dmi)
                        {
                            dmi = d;
                            pcor = tmp;
                        }
                    }
                    if (dmi <= precision)
                    {
                        next_index += 1;
                    }
                    else
                    {
                        next_index += 2;
                        p.Insert(i, pcor);
                        tocheck = true;
                    }
                }
                else
                {
                    next_index += 1;
                }
            } while (next_index < p.Count);

            if (tocheck && p.Count < maxThroughPoints)
            {
                return Refine(p.ToArray(), degree, closed, pl, precision);
            }
            else
            {
                if (initp == p.Count)
                    return bsp;
                else
                {
                    BSpline bspr = BSpline.Construct();
                    bspr.ThroughPoints(p.ToArray(), degree, closed);
                    return bspr;
                }
            }
            */
        }

        #region IOctTreeInsertable Members

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {   // IOctTreeInsertable wird nur verwendet um octtree.GetObjectsCloseTo aufzurufen, das sollte keinen extent verlangen
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            GeoPoint2D uv;
            return HitTest(cube, out uv);
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public virtual IDualSurfaceCurve[] GetDualSurfaceCurves(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> extremePositions)
        {
            if (this is ISurfaceOfRevolution && other is ISurfaceOfRevolution && !(this is SurfaceOfRevolution) && !(other is SurfaceOfRevolution)) // SurfaceOfRevolution rotates in v!
            {
                ISurfaceOfRevolution tsor = (this as ISurfaceOfRevolution);
                ISurfaceOfRevolution osor = (other as ISurfaceOfRevolution);
                if (Precision.SameDirection(tsor.Axis.Direction, osor.Axis.Direction, false))
                {
                    if (Geometry.DistPL(osor.Axis.Location, tsor.Axis.Location, tsor.Axis.Direction) < Precision.eps)
                    {   // identical axis of this surface and other surface
                        // The plane given by the location and common axis, and the startpoint of the 3d curve. The plane coordinate system is normalized
                        try
                        {
                            ICurve tfu = this.FixedU(thisBounds.Left, thisBounds.Bottom, thisBounds.Top);
                            ICurve ofu = other.FixedU(other.PositionOf(tfu.StartPoint).x, otherBounds.Bottom, otherBounds.Top);
                            Plane cpln;
                            if (Curves.GetCommonPlane(tfu, ofu, out cpln))
                            {
                                double[] ippars = Curves.Intersect(tfu, ofu, true);

                                //ICurve tcrv = tsor.Curve;
                                //GeoVector tdir = tcrv.StartDirection;
                                //if (Precision.SameDirection(tdir, tsor.Axis.Direction, false)) tdir = tcrv.EndDirection;
                                //if (Precision.SameDirection(tdir, tsor.Axis.Direction, false)) tdir = tcrv.StartPoint - tsor.Axis.Location;
                                //Plane tpln = new Plane(tsor.Axis.Location, tsor.Axis.Direction, tdir);
                                //ICurve2D t2d = tcrv.GetProjectedCurve(tpln);
                                //ICurve ocrv = osor.Curve;
                                //GeoVector odir = ocrv.StartDirection;
                                //if (Precision.SameDirection(odir, tsor.Axis.Direction, false)) odir = ocrv.EndDirection;
                                //if (Precision.SameDirection(odir, tsor.Axis.Direction, false)) odir = ocrv.StartPoint - tsor.Axis.Location;
                                //Plane opln = new Plane(tsor.Axis.Location, tsor.Axis.Direction, odir);
                                //ICurve2D o2d = ocrv.GetProjectedCurve(opln);
                                //GeoPoint2DWithParameter[] ips = t2d.Intersect(o2d);
                                List<DualSurfaceCurve> dscs = new List<DualSurfaceCurve>();
                                for (int i = 0; i < ippars.Length; i++)
                                {
                                    GeoPoint ip = tfu.PointAt(ippars[i]);
                                    GeoPoint2D tuv = this.PositionOf(ip);
                                    GeoPoint2D ouv = other.PositionOf(ip);
                                    ICurve c3d = this.FixedV(tuv.y, thisBounds.Left, thisBounds.Right);
                                    ICurve oc3d = other.FixedV(ouv.y, otherBounds.Left, otherBounds.Right);
                                    if (c3d is Ellipse && oc3d is Ellipse)
                                    {
                                        Plane pln = (c3d as Ellipse).Plane;
                                        Arc2D tarc = c3d.GetProjectedCurve(pln) as Arc2D; // must be an arc
                                        Arc2D oarc = oc3d.GetProjectedCurve(pln) as Arc2D; // must also be an arc
                                        if (tarc != null && oarc != null)
                                        {
                                            GeoPoint2D tsp = this.PositionOf(c3d.StartPoint);
                                            GeoPoint2D tep = this.PositionOf(c3d.EndPoint);
                                            SurfaceHelper.AdjustPeriodic(this, thisBounds, ref tsp);
                                            SurfaceHelper.AdjustPeriodic(this, thisBounds, ref tep);
                                            GeoPoint2D osp = other.PositionOf(c3d.StartPoint);
                                            GeoPoint2D oep = other.PositionOf(c3d.EndPoint);
                                            SurfaceHelper.AdjustPeriodic(other, otherBounds, ref osp);
                                            SurfaceHelper.AdjustPeriodic(other, otherBounds, ref oep);
                                            Line2D tc2d = new Line2D(tsp, tep);
                                            Line2D oc2d = new Line2D(osp, oep);
                                            // double pt = c3d.PositionOf(this.PointAt(tc2d.PointAt(0.5)));
                                            // double po = c3d.PositionOf(other.PointAt(oc2d.PointAt(0.5)));
                                            // if ((0 < pt && pt < 1) && (0 < po && po < 1)) // should be 0.5 // && was || before, but this was definitely wrong! we must check, whether tarc and oarc overlap
                                            // previous line was wrong, comment was right: we must check, whether tarc and oarc overlap
                                            // These are two 2d arcs on the same planeso at least two start/endpoints must be within the other arc
                                            int inside = 0;
                                            double overlap = 0.0;
                                            double tp = tarc.PositionOf(oarc.StartPoint);
                                            overlap += 0.5 - Math.Abs(tp - 0.5);
                                            if (tp >= 0 && tp <= 1) ++inside;
                                            tp = tarc.PositionOf(oarc.EndPoint);
                                            overlap += 0.5 - Math.Abs(tp - 0.5);
                                            if (tp >= 0 && tp <= 1) ++inside;
                                            tp = oarc.PositionOf(tarc.StartPoint);
                                            overlap += 0.5 - Math.Abs(tp - 0.5);
                                            if (tp >= 0 && tp <= 1) ++inside;
                                            tp = oarc.PositionOf(tarc.EndPoint);
                                            overlap += 0.5 - Math.Abs(tp - 0.5);
                                            if (tp >= 0 && tp <= 1) ++inside;
                                            // it still might be, that the two arcs touch at all endpoints but describe different halfs of a circle
                                            // if all tp are 0 or 1, the resulting overlap will be 0
                                            if (overlap<10*Precision.eps)
                                            {   // either full identical or opposite arcs
                                                tp = tarc.PositionOf(oarc.PointAt(0.5));
                                            }
                                            if (inside >= 2 && tp>0 && tp<1) dscs.Add(new DualSurfaceCurve(c3d, this, tc2d, other, oc2d));
                                            // else: did choose wrong part of arc, no common intersection curve
                                        }
                                    }
                                }
                                return dscs.ToArray();
                            }
                        }
                        catch
                        {
                            // plane Exception possible?
                        }
                    }
                }
            }

            // sollte für Ebene, Cylinder, Kegel, Kugel, Torus überschrieben werden
            ModOp2D mop;
            if (SameGeometry(thisBounds, other, otherBounds, Precision.eps, out mop)) return new IDualSurfaceCurve[0]; // surfaces are identical, no intersection
            ICurve[] cvs = BoxedSurfaceEx.Intersect(thisBounds, other, otherBounds, seeds, extremePositions);
            if (cvs.Length == 0 && seeds.Count == 2)
            {
                InterpolatedDualSurfaceCurve idscv = new InterpolatedDualSurfaceCurve(this, thisBounds, other, otherBounds, seeds[0], seeds[1]);
                return new IDualSurfaceCurve[] { idscv };
            }
            DualSurfaceCurve[] res = new DualSurfaceCurve[cvs.Length];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = new DualSurfaceCurve(cvs[i], this, this.GetProjectedCurve(cvs[i], Precision.eps), other, other.GetProjectedCurve(cvs[i], Precision.eps));
            }
            return res;
        }

        public virtual ICurve2D[] GetSelfIntersections(BoundingRect bounds)
        {
            return null;
        }


        #endregion
    }


    public class Surfaces
    {
        internal static ICurve Intersect(PlaneSurface surface1, BoundingRect bounds1, CylindricalSurface surface2, BoundingRect bounds2, List<GeoPoint> points)
        {
            IDualSurfaceCurve[] cvs = surface2.GetPlaneIntersection(surface1, bounds2.Left, bounds2.Right, bounds2.Bottom, bounds2.Top, 0.0);
            if (cvs.Length == 2 && cvs[0].Curve3D is Line l0 && cvs[1].Curve3D is Line l1)
            {   // two lines, find the closer one
                if ((l0 as ICurve).DistanceTo(points[0]) < (l1 as ICurve).DistanceTo(points[0])) return l0;
                else return l1;
            }
            for (int i = 0; i < cvs.Length; ++i)
            {
                ICurve c3d = cvs[i].Curve3D;
                double spar = c3d.PositionOf(points[0]);
                double epar = c3d.PositionOf(points[points.Count - 1]);
                if (c3d is Line)
                {   // eine von zwei Linien ist die falsche, ist aber egal hier
                    Line line = Line.Construct();
                    line.SetTwoPoints(points[0], points[points.Count - 1]);
                    return line;
                }
                else if (c3d is Ellipse)
                {   // richtiger Abschnitt der Ellipse finden, kann über 0 gehen
                    // es ist immer eine volle Ellipse, unabhängig von umin...vmax
                    // Startparameter ist immer 0.0
                    Ellipse elli = c3d as Ellipse;
                    elli.StartParameter = 0.0;
                    elli.SweepParameter = Math.PI * 2.0; // damit klare Verhältnisse vorliegen
                    elli.StartParameter = elli.ParameterOf(points[0]);
                    epar = elli.PositionOf(points[points.Count - 1]); // zwischen 0 und 1
                    elli.SweepParameter = epar * Math.PI * 2.0;
                    if (points.Count > 2)
                    {   // einfacher Fall, Zwischenpunkte prüfen
                        int j = points.Count / 2; // mittlerer Punkt
                        double pos = elli.PositionOf(points[j]);
                        if (pos < 0.0 || pos > 1.0)
                        {
                            elli.SweepParameter = elli.SweepParameter - 2.0 * Math.PI;
                        }
                        double dbg = elli.PositionOf(points[points.Count - 1]);
                    }
                    else
                    {   // kein Zwischenpunkt, eigentlich Lage in bounds2 überprüfen
                        GeoPoint2D tp = surface2.PositionOf(elli.PointAt(0.5));
                        SurfaceHelper.AdjustPeriodic(surface2, bounds2, ref tp);
                        if (tp.x < bounds2.Left || tp.x > bounds2.Right) elli.SweepParameter = elli.SweepParameter - 2.0 * Math.PI;
                        //if (elli.SweepParameter > Math.PI)
                        //{
                        //    elli.SweepParameter = elli.SweepParameter - 2.0 * Math.PI;
                        //}
                    }
                    return elli;
                }
            }
            return null;
        }
        internal static ICurve Intersect(PlaneSurface surface1, BoundingRect bounds1, ToroidalSurface surface2, BoundingRect bounds2, List<GeoPoint> points)
        {
            IDualSurfaceCurve[] cvs = surface2.GetPlaneIntersection(surface1, bounds2.Left, bounds2.Right, bounds2.Bottom, bounds2.Top, 0.0);
            if (cvs.Length == 1) return cvs[0].Curve3D;
            if (cvs.Length > 1)
            {
                // the toroidal bounds are typically only a quarter of the full 0..2*PI square, so there should only be one valid solution inside
                for (int i = 0; i < cvs.Length; i++)
                {
                    if (bounds2.Contains(cvs[i].Curve2D1.PointAt(0.5))) return cvs[i].Curve3D;
                }
                return cvs[0].Curve3D; // no good constraint
            }
            return null;
        }
        /// <summary>
        /// Intersect two surfaces where we can calculate exact intersection curves without approximation. this should be implemented for plane, cylinder, cone, sphere and torus.
        /// The resulting intersection curves are either infinite or closed.
        /// </summary>
        /// <param name="surface1"></param>
        /// <param name="surface2"></param>
        /// <returns>null, if not implemented for the provided surfaces, otherwise the intersection curves, which also may be none (empty array)</returns>
        internal static ICurve[] Intersect(ISurface surface1, ISurface surface2)
        {   // this should be used for intersections, where no seed points can be calculated. It must be implemented for all combinations of canonical surfaces (plane, cylinder, cone, sphere, torus), 
            if (surface1 is PlaneSurface ps1 && surface2 is PlaneSurface ps2)
            {   // the simplest case
                if (ps1.Plane.Intersect(ps2.Plane, out GeoPoint loc, out GeoVector dir))
                {
                    return new ICurve[] { Line.TwoPoints(loc, loc + dir) };
                }
                else return new ICurve[0];
            }
            if (surface2 is PlaneSurface)
            {   // swap surfaces to need less checks
                ISurface tmp = surface1;
                surface1 = surface2;
                surface2 = tmp;
            }
            if (surface1 is PlaneSurface ps)
            {
                if (surface2 is ICylinder cy)
                {   // a plane and a cylinder
                    if (Precision.IsPerpendicular(cy.Axis.Direction, ps.Normal, false))
                    {   // two lines, tangential or no result
                        Plane lower = new Plane(cy.Axis.Location, cy.Axis.Direction);
                        GeoPoint2D sp2d = lower.Project(ps.Location);
                        GeoVector2D dir2d = lower.Project(ps.Normal).ToLeft();
                        GeoPoint2D[] ips = Geometry.IntersectLC(sp2d, dir2d, GeoPoint2D.Origin, cy.Radius);
                        ICurve[] res = new ICurve[ips.Length];
                        for (int i = 0; i < ips.Length; i++)
                        {
                            GeoPoint p = lower.ToGlobal(ips[i]);
                            res[i] = Line.TwoPoints(p, p + cy.Axis.Direction);
                        }
                        return res;
                    }
                    // otherwise we have a circle or an ellipse
                    GeoPoint cnt = ps.Plane.Intersect(cy.Axis.Location, cy.Axis.Direction);
                    Ellipse elli = Ellipse.Construct();
                    if (Precision.SameDirection(ps.Plane.Normal, cy.Axis.Direction, false))
                    {   // this is a perpendicular intersection, the result will be a circle
                        cy.Axis.Direction.ArbitraryNormals(out GeoVector dirx, out GeoVector diry);
                        dirx.Length = cy.Radius;
                        diry.Length = cy.Radius;
                        elli.SetEllipseArcCenterAxis(cnt, dirx, diry, 0, Math.PI * 2.0);
                        return new ICurve[] { elli };
                    }
                    else if (!Precision.IsPerpendicular(cy.Axis.Direction, ps.Plane.Normal, false))
                    {
                        GeoVector minAx = cy.Axis.Direction ^ ps.Plane.Normal;
                        minAx.Length = cy.Radius;
                        GeoPoint2D[] ips = surface2.GetLineIntersection(cnt, minAx ^ ps.Plane.Normal);
                        if (ips != null && ips.Length > 0)
                        {
                            GeoVector majAx = surface2.PointAt(ips[0]) - cnt;
                            elli.SetEllipseCenterAxis(cnt, majAx, minAx);
                            return new ICurve[] { elli };
                        }
                        else return null;
                    }
                }
                if (surface2 is ISphere sph)
                {
                    double d = ps.Plane.Distance(sph.Center);
                    if (d < sph.Radius)
                    {
                        double r = Math.Sqrt(sph.Radius * sph.Radius - d * d);
                        Ellipse elli = Ellipse.Construct();
                        elli.SetCirclePlaneCenterRadius(ps.Plane, ps.Plane.ToGlobal(ps.Plane.Project(sph.Center)), r);
                        return new ICurve[] { elli };
                    }
                    return null;
                }
                if (surface2 is ICone icn)
                {

                }
            }
            return null;
            // still to implement intersections of standard surfaces:
            // //plane - plane
            // //plane - cylinder
            // //plane - sphere
            // plane - cone
            // plane - torus
            // cylinder - cylinder
            // cylinder - sphere
            // cylinder - cone
            // cylinder - torus
            // sphere - sphere
            // sphere - cone
            // sphere - torus
            // cone - cone
            // cone - torus
            // torus - torus
            // the results may be lines, ellipses or InterpolatedDualSurfaceCurves (8 points should be enough)
        }
        internal static ICurve Intersect(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, List<GeoPoint> points)
        {   // es muss einen Schnitt geben, sonst wird das hier garnicht aufgerufen, schließlich gibt es ja auch schon Punkte
            if (surface1 is PlaneSurface && surface2 is PlaneSurface && points.Count == 2)
            {   // hier sind die beiden Punkte schon bekannt und richtig
                Line line = Line.Construct();
                line.SetTwoPoints(points[0], points[points.Count - 1]);
                return line;
            }
            if (surface1 is PlaneSurface && surface2 is CylindricalSurface)
            {
                ICurve res = Intersect(surface1 as PlaneSurface, bounds1, surface2 as CylindricalSurface, bounds2, points);
                if (res != null) return res;
            }
            if (surface2 is PlaneSurface && surface1 is CylindricalSurface)
            {
                ICurve res = Intersect(surface2 as PlaneSurface, bounds2, surface1 as CylindricalSurface, bounds1, points);
                if (res != null) return res;
            }
            if (surface1 is PlaneSurface && surface2 is ToroidalSurface)
            {
                ICurve res = Intersect(surface1 as PlaneSurface, bounds1, surface2 as ToroidalSurface, bounds2, points);
                if (res != null) return res;
            }
            if (surface2 is PlaneSurface && surface1 is ToroidalSurface)
            {
                ICurve res = Intersect(surface2 as PlaneSurface, bounds2, surface1 as ToroidalSurface, bounds1, points);
                if (res != null) return res;
            }
            {
                ICurve[] crvs = surface1.Intersect(bounds1, surface2, bounds2);
                for (int i = 0; i < crvs.Length; i++)
                {
                    double dsum = 0.0;
                    for (int j = 0; j < points.Count; j++)
                    {
                        dsum += crvs[i].DistanceTo(points[j]);
                    }
                    if (dsum < points.Count * Precision.eps) return crvs[i];
                }
            }
            // kein bekannter Fall. Hier ist es aber gefährlich wenn wir tangential sind,
            // denn dann funktioniert InterpolatedDualSurfaceCurve sehr schlecht, vor allem die Richtung ger Kurve
            // wird oft falsch
            bool tangential = false;
            for (int i = 0; i < points.Count; i++)
            {
                GeoPoint2D uv1 = surface1.PositionOf(points[i]);
                GeoPoint2D uv2 = surface2.PositionOf(points[i]);
                GeoVector n1 = surface1.GetNormal(uv1);
                GeoVector n2 = surface2.GetNormal(uv2);
                GeoVector z = n1.Normalized ^ n2.Normalized;
                if (z.Length < 0.1) // auf 0.1 reduziert, da immer noch Konvergenzprobleme
                {
                    tangential = true; // wenn nur an einer Stelle tangential, dann tangential. Die Kurven werden sonst zu schlecht!
                    break;
                }
            }
            if (tangential)
            {
                ICurve res = surface1.Intersect(bounds1, surface2, bounds2, points[0]);
                if (res != null)
                {
                    if (points.Count == 2)
                    {   // these are supposed to be start- and endpoint of the curve
                        // better return all of the curve, used in this way by Parametrics
                        //if ((res.StartPoint | points[0]) + (res.EndPoint | points[1]) > (res.StartPoint | points[1]) + (res.EndPoint | points[0])) res.Reverse();
                        //// maybe we have to trimm here
                        //res.StartPoint = points[0];
                        //res.EndPoint = points[1];
                    }
                    return res;
                }
                return null; // tangentiale Flächen können keine dualsurfacecurve haben, das konvergiert nicht
            }
            {
                InterpolatedDualSurfaceCurve idsc = new InterpolatedDualSurfaceCurve(surface1, bounds1, surface2, bounds2, points);
                return idsc;
            }
            //return null;
        }
        internal static bool Intersect(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, List<GeoPoint> points,
            out ICurve[] crvs3d, out ICurve2D[] crvsOnSurface1, out ICurve2D[] crvsOnSurface2, out double[,] params3d, out double[,] params2dsurf1, out double[,] params2dsurf2,
            out GeoPoint2D[] paramsuvsurf1, out GeoPoint2D[] paramsuvsurf2, double precision)
        {   // vor allem für BRepOperation.
            // es dient z.Z. vor allem dafür, einfache Schnitte schnell zu berechnen und das Ergebnis in 3d und 2d mit allen u und uv Werten für die gegebenen Punkte zu liefern,
            // damit diese nicht hinterher mehrfach neu berechnet werden müssen.
            // ein Schnitt zweier surfaces kann auch mehrere Schnittlinien liefern (vor allem bei NURBS, aber auch Zylinder/Ebene, Zylinder Kugel u.s.w.)
            // Die Punkte kommen meist von mehreren Edge/Face Schnitten

            // zuordnen der Punkte auf die Kurve(n)
            List<ICurve> lcrvs3d = new List<ICurve>(); // Ergebnisse als Listen
            List<ICurve2D> lcrvsOnSurface1 = new List<ICurve2D>();
            List<ICurve2D> lcrvsOnSurface2 = new List<ICurve2D>();
            paramsuvsurf1 = new GeoPoint2D[points.Count];
            paramsuvsurf2 = new GeoPoint2D[points.Count];
            for (int j = 0; j < points.Count; j++)
            {
                paramsuvsurf1[j] = surface1.PositionOf(points[j]);
                paramsuvsurf2[j] = surface2.PositionOf(points[j]);
                SurfaceHelper.AdjustPeriodic(surface1, bounds1, ref paramsuvsurf1[j]);
                SurfaceHelper.AdjustPeriodic(surface2, bounds2, ref paramsuvsurf2[j]);
                bounds1.MinMax(paramsuvsurf1[j]);
                bounds2.MinMax(paramsuvsurf2[j]);
            }
            IDualSurfaceCurve[] dscs = surface1.GetDualSurfaceCurves(bounds1, surface2, bounds2, points, null);

            if (points.Count > paramsuvsurf1.Length)
            {   // there were points added by GetDualSurfaceCurves
                paramsuvsurf1 = new GeoPoint2D[points.Count];
                paramsuvsurf2 = new GeoPoint2D[points.Count];
                for (int j = 0; j < points.Count; j++)
                {
                    paramsuvsurf1[j] = surface1.PositionOf(points[j]);
                    paramsuvsurf2[j] = surface2.PositionOf(points[j]);
                    SurfaceHelper.AdjustPeriodic(surface1, bounds1, ref paramsuvsurf1[j]);
                    SurfaceHelper.AdjustPeriodic(surface2, bounds2, ref paramsuvsurf2[j]);
                }
            }
            // if there are closed curves in the result we try to split them
            List<IDualSurfaceCurve> brokenClosedCurves = new List<IDualSurfaceCurve>();
            List<int> toRemove = new List<int>();
            for (int i = 0; i < dscs.Length; i++)
            {
                if (dscs[i].Curve3D.IsClosed)
                {
                    List<GeoPoint> pointsOnCurve = new List<GeoPoint>();
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (dscs[i].Curve3D.DistanceTo(points[j]) < precision)
                        {
                            pointsOnCurve.Add(points[j]);
                        }
                    }
                    if (pointsOnCurve.Count > 1)
                    {
                        ICurve[] parts3D = dscs[i].Curve3D.Split(dscs[i].Curve3D.PositionOf(pointsOnCurve[0]), dscs[i].Curve3D.PositionOf(pointsOnCurve[1]));
                        if (parts3D.Length == 2)
                        {
                            brokenClosedCurves.Add(new DualSurfaceCurve(parts3D[0], surface1, surface1.GetProjectedCurve(parts3D[0], precision), surface2, surface2.GetProjectedCurve(parts3D[0], precision)));
                            brokenClosedCurves.Add(new DualSurfaceCurve(parts3D[1], surface1, surface1.GetProjectedCurve(parts3D[1], precision), surface2, surface2.GetProjectedCurve(parts3D[1], precision)));
                            toRemove.Add(i);
                        }
                    }
                }
            }
            if (brokenClosedCurves.Count > 0)
            {
                List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>(dscs);
                for (int i = toRemove.Count - 1; i >= 0; --i)
                {
                    res.RemoveAt(toRemove[i]);
                }
                res.AddRange(brokenClosedCurves);
                dscs = res.ToArray();
            }
            params3d = new double[dscs.Length, points.Count];
            params2dsurf1 = new double[dscs.Length, points.Count];
            params2dsurf2 = new double[dscs.Length, points.Count];
            for (int i = 0; i < dscs.Length; i++)
            {
                ICurve cv = dscs[i].Curve3D;
                lcrvs3d.Add(cv);
                ICurve2D cvons1 = dscs[i].Curve2D1;
                ICurve2D cvons2 = dscs[i].Curve2D2;
                SurfaceHelper.AdjustPeriodic(surface1, bounds1, cvons1);
                SurfaceHelper.AdjustPeriodic(surface2, bounds2, cvons2);
                lcrvsOnSurface1.Add(cvons1);
                lcrvsOnSurface2.Add(cvons2);
                for (int j = 0; j < points.Count; j++)
                {
                    double d = cv.DistanceTo(points[j]);
                    if (d < precision) // auf die nächstgelegene Kurve mappen
                    {
                        params3d[i, j] = cv.PositionOf(points[j]);
                        params2dsurf1[i, j] = cvons1.PositionOf(paramsuvsurf1[j]);
                        params2dsurf2[i, j] = cvons2.PositionOf(paramsuvsurf2[j]);
                        if (params2dsurf1[i, j] == double.MinValue || params2dsurf2[i, j] == double.MinValue)
                        {
                            params3d[i, j] = double.MinValue; // indication: the point "j" doesn't belong to curve i
                            params2dsurf1[i, j] = double.MinValue;
                            params2dsurf2[i, j] = double.MinValue;
                        }
                    }
                    else
                    {
                        params3d[i, j] = double.MinValue; // indication: the point "j" doesn't belong to curve i
                        params2dsurf1[i, j] = double.MinValue;
                        params2dsurf2[i, j] = double.MinValue;
                    }
                }
            }
            //Set<int> unusedPoints = new Set<int>();
            //for (int i = 0; i < points.Count; i++)
            //{
            //    unusedPoints.Add(i);
            //}
            //while (unusedPoints.Count > 0)
            //{
            //    int ind = unusedPoints.GetAny();
            //    ICurve cv = Intersect(surface1, bounds1, surface2, bounds2, points[ind]);
            //    if (cv != null)
            //    {
            //        lcrvs3d.Add(cv);
            //        ICurve2D cvons1 = surface1.GetProjectedCurve(cv, Precision.eps);
            //        ICurve2D cvons2 = surface2.GetProjectedCurve(cv, Precision.eps);
            //        SurfaceHelper.AdjustPeriodic(surface1, bounds1, cvons1);
            //        SurfaceHelper.AdjustPeriodic(surface2, bounds2, cvons2);
            //        lcrvsOnSurface1.Add(cvons1);
            //        lcrvsOnSurface2.Add(cvons2);
            //        for (int i = 0; i < points.Count; i++)
            //        {
            //            bool close = cv.DistanceTo(points[i]) < Precision.eps;
            //            if (unusedPoints.Contains(i) && close)
            //            {
            //                unusedPoints.Remove(i);
            //                params3d[i] = cv.PositionOf(points[i]);
            //                paramsuvsurf1[i] = surface1.PositionOf(points[i]);
            //                paramsuvsurf2[i] = surface2.PositionOf(points[i]);
            //                SurfaceHelper.AdjustPeriodic(surface1, bounds1, ref paramsuvsurf1[i]);
            //                SurfaceHelper.AdjustPeriodic(surface2, bounds2, ref paramsuvsurf2[i]);
            //                params2dsurf1[i] = cvons1.PositionOf(paramsuvsurf1[i]);
            //                params2dsurf2[i] = cvons2.PositionOf(paramsuvsurf2[i]);
            //            }
            //            if (close) pointOnCurveindex[i]= lcrvs3d.Count - 1;
            //        }
            //    }
            //    unusedPoints.Remove(ind); // der muss weg, damit keine Endlosschleife
            //}
            crvs3d = lcrvs3d.ToArray();
            crvsOnSurface1 = lcrvsOnSurface1.ToArray();
            crvsOnSurface2 = lcrvsOnSurface2.ToArray();
            return crvs3d.Length > 0;
        }
        internal static ICurve Intersect(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, GeoPoint seed)
        {
            // zwei beliebige Flächen sollen geschnitten werden, beide mit endlichen Intervall. Bei periodischen ist dieses intervall jeweils kleiner als die Periode.
            // Es gibt ausgehend von "seed" nur maximal eine Kurve als Ergebnis.
            // Dieses Ergebnis wird von Intersect verwendet, um einen teil der gegebenen punkte abzuarbeiten und wenn nicht alle verbraucht, nochmal mit einem anderen punkt aufzurufen
            if (surface1 is PlaneSurface)
            {
                if (surface2 is PlaneSurface)
                {   // Ebene/Ebene, noch implementieren

                }
                if (surface2 is CylindricalSurface)
                {
                    CylindricalSurface cyl = (surface2 as CylindricalSurface);
                    ICurve[] icvs = cyl.Intersect(bounds2, surface1, bounds1);
                    for (int i = 0; i < icvs.Length; i++)
                    {
                        if (icvs[i].DistanceTo(seed) < Precision.eps) return icvs[i];
                    }
                    IDualSurfaceCurve[] cvs = cyl.GetPlaneIntersection(surface1 as PlaneSurface, bounds2.Left, bounds2.Right, bounds2.Bottom, bounds2.Top, Precision.eps);
                    if (cvs != null)
                    {
                        if (cvs.Length == 2)
                        {   // zwei Linien, hier mit bounds1 trimmen
                            int ind = 1;    // welche der beiden Linien ist gemeint?
                            if (cvs[0].Curve3D.DistanceTo(seed) < cvs[1].Curve3D.DistanceTo(seed)) ind = 0;
                            GeoPoint2D sp = surface1.PositionOf(cvs[ind].Curve3D.StartPoint);
                            GeoPoint2D ep = surface1.PositionOf(cvs[ind].Curve3D.EndPoint);
                            GeoVector2D dir = ep - sp;
                            double pmin = double.MinValue, pmax = double.MaxValue;
                            if (Math.Abs(dir.x) > 1e-10)
                            {
                                double p = (bounds1.Left - sp.x) / dir.x;
                                if (p > 0 && p < pmax) pmax = p;
                                if (p <= 0 && p > pmin) pmin = p;
                                p = (bounds1.Right - sp.x) / dir.x;
                                if (p > 0 && p < pmax) pmax = p;
                                if (p <= 0 && p > pmin) pmin = p;
                            }
                            if (Math.Abs(dir.y) > 1e-10)
                            {
                                double p = (bounds1.Bottom - sp.y) / dir.y;
                                if (p > 0 && p < pmax) pmax = p;
                                if (p <= 0 && p > pmin) pmin = p;
                                p = (bounds1.Top - sp.y) / dir.y;
                                if (p > 0 && p < pmax) pmax = p;
                                if (p <= 0 && p > pmin) pmin = p;
                            }

                            if (pmin != double.MinValue && pmax != double.MaxValue)
                            {
                                Line res = Line.Construct();
                                res.SetTwoPoints(surface1.PointAt(sp + pmin * dir), surface1.PointAt(sp + pmax * dir));
                                if (res.Length > 0) return res; // könnte man auch noch mit dem Zylinder trimmen, ist aber denke ich nicht nötig
                            }
                            return null;
                        }
                        else
                        {
                            if (cvs[0].Curve3D is Ellipse)
                            {
                                // genau eine Ellipse
                                Ellipse elli = cvs[0].Curve3D as Ellipse;
                                GeoPoint p1 = elli.Plane.Intersect(surface2.PointAt(bounds2.GetLowerLeft()), cyl.Axis);
                                GeoPoint p2 = elli.Plane.Intersect(surface2.PointAt(bounds2.GetLowerRight()), cyl.Axis);
                                GeoPoint p3 = elli.Plane.Intersect(surface2.PointAt(bounds2.GetLowerMiddle()), cyl.Axis); // als Testpunkt, welcher Teil geliefert werden soll
                                double par1 = elli.PositionOf(p1);
                                double par2 = elli.PositionOf(p2);
                                ICurve[] parts = elli.Split(par1, par2);
                                if (parts.Length == 2)
                                {   // trimmen an der Ebene wohl nicht nötig
                                    if (parts[0].DistanceTo(p3) < parts[1].DistanceTo(p3)) return parts[0];
                                    else return parts[1];
                                }
                            }
                            else if (cvs[0].Curve3D is Line)
                            {
                                GeoPoint2D sp = surface1.PositionOf(cvs[0].Curve3D.StartPoint);
                                GeoPoint2D ep = surface1.PositionOf(cvs[0].Curve3D.EndPoint);
                                GeoVector2D dir = ep - sp;
                                double pmin = double.MinValue, pmax = double.MaxValue;
                                if (Math.Abs(dir.x) > 1e-10)
                                {
                                    double p = (bounds1.Left - sp.x) / dir.x;
                                    if (p > 0 && p < pmax) pmax = p;
                                    if (p <= 0 && p > pmin) pmin = p;
                                    p = (bounds1.Right - sp.x) / dir.x;
                                    if (p > 0 && p < pmax) pmax = p;
                                    if (p <= 0 && p > pmin) pmin = p;
                                }
                                if (Math.Abs(dir.y) > 1e-10)
                                {
                                    double p = (bounds1.Bottom - sp.y) / dir.y;
                                    if (p > 0 && p < pmax) pmax = p;
                                    if (p <= 0 && p > pmin) pmin = p;
                                    p = (bounds1.Top - sp.y) / dir.y;
                                    if (p > 0 && p < pmax) pmax = p;
                                    if (p <= 0 && p > pmin) pmin = p;
                                }
                                if (pmin != double.MinValue && pmax != double.MaxValue)
                                {
                                    Line res = Line.Construct();
                                    res.SetTwoPoints(surface1.PointAt(sp + pmin * dir), surface1.PointAt(sp + pmax * dir));
                                    if (res.Length > 0) return res; // könnte man auch noch mit dem Zylinder trimmen, ist aber denke ich nicht nötig
                                }
                                return null;
                            }

                        }
                    }

                }
                else if (surface2 is ConicalSurface)
                {
                    ConicalSurface cnl = (surface2 as ConicalSurface);
                    IDualSurfaceCurve[] cvs = cnl.GetPlaneIntersection(surface1 as PlaneSurface, bounds1.Left, bounds1.Right, bounds1.Bottom, bounds1.Top, Precision.eps);
                    if (cvs != null)
                    {
                        if (cvs.Length == 2 && cvs[0].Curve3D is Line && cvs[1].Curve3D is Line)
                        {   // zwei Linien, hier mit bounds1 trimmen
                            int ind = 1;    // welche der beiden Linien ist gemeint?
                            if (cvs[0].Curve3D.DistanceTo(seed) < cvs[1].Curve3D.DistanceTo(seed)) ind = 0;
                            GeoPoint2D sp = surface1.PositionOf(cvs[ind].Curve3D.StartPoint);
                            GeoPoint2D ep = surface1.PositionOf(cvs[ind].Curve3D.EndPoint);
                            GeoVector2D dir = ep - sp;
                            double pmin = double.MinValue, pmax = double.MaxValue;
                            if (Math.Abs(dir.x) > 1e-10)
                            {
                                double p = (bounds1.Left - sp.x) / dir.x;
                                if (p > 0 && p < pmax) pmax = p;
                                if (p < 0 && p > pmin) pmin = p;
                                p = (bounds1.Right - sp.x) / dir.x;
                                if (p > 0 && p < pmax) pmax = p;
                                if (p < 0 && p > pmin) pmin = p;
                            }
                            if (Math.Abs(dir.y) > 1e-10)
                            {
                                double p = (bounds1.Bottom - sp.y) / dir.y;
                                if (p > 0 && p < pmax) pmax = p;
                                if (p < 0 && p > pmin) pmin = p;
                                p = (bounds1.Top - sp.y) / dir.y;
                                if (p > 0 && p < pmax) pmax = p;
                                if (p < 0 && p > pmin) pmin = p;
                            }
                            Line res = Line.Construct();
                            res.SetTwoPoints(surface1.PointAt(sp + pmin * dir), surface1.PointAt(sp + pmax * dir));
                            return res; // könnte man auch noch mit dem Zylinder trimmen, ist aber denke ich nicht nötig
                        }
                        else if (cvs.Length == 1 && cvs[0].Curve3D is Ellipse)
                        {
                            // genau eine Ellipse
                            Ellipse elli = cvs[0].Curve3D as Ellipse;
                            Line ln1 = cnl.FixedU(bounds2.Left, bounds2.Bottom, bounds2.Top) as Line;
                            Line ln2 = cnl.FixedU(bounds2.Right, bounds2.Bottom, bounds2.Top) as Line;
                            Line ln3 = cnl.FixedU((bounds2.Left + bounds2.Right) / 2.0, bounds2.Bottom, bounds2.Top) as Line;
                            GeoPoint p1 = elli.Plane.Intersect(ln1.StartPoint, ln1.StartDirection);
                            GeoPoint p2 = elli.Plane.Intersect(ln2.StartPoint, ln2.StartDirection);
                            GeoPoint p3 = elli.Plane.Intersect(ln3.StartPoint, ln3.StartDirection); // als Testpunkt, welcher Teil geliefert werden soll
                            double par1 = elli.PositionOf(p1);
                            double par2 = elli.PositionOf(p2);
                            ICurve[] parts = elli.Split(par1, par2);
                            if (parts.Length == 2)
                            {   // trimmen an der Ebene wohl nicht nötig
                                if (parts[0].DistanceTo(p3) < parts[1].DistanceTo(p3)) return parts[0];
                                else return parts[1];
                            }
                        }
                        else
                        {
                            // Hyperbeln, Parabel auf jeder Hälfte des Doppelkegels eine
                            // der Kegel ist nie ein Doppelkegel, deshalb muss immer die Kurve geliefert werden, deren Koordinaten in der Einheitsform positiv sind
                            // bounds ist immer in y nur positiv oder nur negativ
                            for (int i = 0; i < cvs.Length; i++)
                            {
                                if (Math.Sign(cnl.PositionOf(cvs[i].Curve3D.PointAt(0.5)).y) == Math.Sign(bounds2.Bottom + bounds2.Top)) return cvs[i].Curve3D;
                            }
                        }
                    }

                }
            }
            else if (surface2 is PlaneSurface)
            {
                return Intersect(surface2, bounds2, surface1, bounds1, seed);
            }
            // allgemeine Lösung (hier noch die Methode mit seed in ISurface einführen!)
            ICurve[] ic = surface1.Intersect(bounds1, surface2, bounds2);
            for (int i = 0; i < ic.Length; i++)
            {
                if (ic[i].DistanceTo(seed) < Precision.eps) return ic[i];
            }
            return null;
        }
        private static ICurve BestTangentialCurve(ISurface surface1, ISurface surface2, List<GeoPoint> points)
        {
            return null;
        }
        internal static bool PlaneIntersection(Plane pln, ISurface surface1, ISurface surface2, out GeoPoint[] ip, out GeoPoint2D[] uv1, out GeoPoint2D[] uv2)
        {
            List<GeoPoint> lip = new List<CADability.GeoPoint>();
            List<GeoPoint2D> luv1 = new List<CADability.GeoPoint2D>();
            List<GeoPoint2D> luv2 = new List<CADability.GeoPoint2D>();

            // wird oft aufgerufen mit 2 mal Zylinder/Kegel/Kugel/Ebene 
            ICurve2D[] c2d1 = CurvesOnPlane(pln, surface1);
            ICurve2D[] c2d2 = CurvesOnPlane(pln, surface2);
            for (int i = 0; i < c2d1.Length; i++)
            {
                for (int j = 0; j < c2d2.Length; j++)
                {
                    GeoPoint2DWithParameter[] ip2d = c2d1[i].Intersect(c2d2[j]);
                    for (int k = 0; k < ip2d.Length; k++)
                    {
                        GeoPoint ipk = pln.ToGlobal(ip2d[k].p);
                        lip.Add(ipk);
                        luv1.Add(surface1.PositionOf(ipk));
                        luv2.Add(surface2.PositionOf(ipk));
                    }
                }
            }

            ip = lip.ToArray();
            uv1 = luv1.ToArray();
            uv2 = luv2.ToArray();
            return ip.Length > 0;
        }
        private static ICurve2D[] CurvesOnPlane(Plane pln, ISurface surface)
        {
            double umin, umax, vmin, vmax;
            surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            if (surface is ISurfacePlaneIntersection) // noch für Ebene, Kugel, Kegel implementieren
            {
                return (surface as ISurfacePlaneIntersection).GetPlaneIntersection(pln, umin, umax, vmin, vmax);
            }
            // bei folgendem wird überflüssigerweise auch die Kurve auf surface berechnet, die oft ein BSpline ist
            IDualSurfaceCurve[] dscs1 = surface.GetPlaneIntersection(new GeoObject.PlaneSurface(pln), umin, umax, vmin, vmax, Precision.eps);
            ICurve2D[] res = new ICurve2D[dscs1.Length];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = dscs1[i].Curve2D2;
            }
            return res;
        }
        /// <summary>
        /// Try to find a single intersection point of three surfaces close to a provided seed point <paramref name="ip"/>.
        /// </summary>
        /// <param name="surface1"></param>
        /// <param name="bounds1"></param>
        /// <param name="surface2"></param>
        /// <param name="bounds2"></param>
        /// <param name="surface3"></param>
        /// <param name="bounds3"></param>
        /// <param name="ip"></param>
        /// <param name="uv1"></param>
        /// <param name="uv2"></param>
        /// <param name="uv3"></param>
        /// <returns></returns>
        internal static bool IntersectThreeSurfaces(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, ISurface surface3, BoundingRect bounds3, ref GeoPoint ip,
            out GeoPoint2D uv1, out GeoPoint2D uv2, out GeoPoint2D uv3)
        {
            ISurface[] surfaces = new ISurface[] { surface1, surface2, surface3 };
            BoundingRect[] bounds = new BoundingRect[] { bounds1, bounds2, bounds3 };
            // if one of the surfaces is a planar surface handle it on the plans 2d system
            List<GeoPoint> candidates = new List<GeoPoint>();
            for (int i = 0; i < 3; i++)
            {
                if (surfaces[i] is PlaneSurface pls)
                {
                    int o1 = (i + 1) % 3;
                    int o2 = (i + 2) % 3;
                    IDualSurfaceCurve[] dsc1 = surfaces[o1].GetPlaneIntersection(pls, bounds[o1].Left, bounds[o1].Right, bounds[o1].Bottom, bounds[o1].Top, 0.0);
                    IDualSurfaceCurve[] dsc2 = surfaces[o2].GetPlaneIntersection(pls, bounds[o2].Left, bounds[o2].Right, bounds[o2].Bottom, bounds[o2].Top, 0.0);
                    if (dsc1 != null && dsc2 != null)
                    {
                        for (int j = 0; j < dsc1.Length; ++j)
                        {
                            for (int k = 0; k < dsc2.Length; k++)
                            {
                                ICurve2D c1, c2;
                                if (dsc1[j].Surface1 == pls) c1 = dsc1[j].Curve2D1;
                                else c1 = dsc1[j].Curve2D2;
                                if (dsc2[k].Surface1 == pls) c2 = dsc2[k].Curve2D1;
                                else c2 = dsc2[k].Curve2D2;
                                GeoPoint2DWithParameter[] ips2d = c1.Intersect(c2);
                                for (int l = 0; l < ips2d.Length; l++)
                                {
                                    candidates.Add(pls.PointAt(ips2d[l].p));
                                }
                            }
                        }
                    }
                }
            }
            if (candidates.Count > 0)
            {   // here we could check, which of the candidates is more precise
                GeoPoint lip = ip;
                ip = Hlp.GetClosest(candidates, p => p | lip);
                uv1 = surface1.PositionOf(ip);
                uv2 = surface2.PositionOf(ip);
                uv3 = surface3.PositionOf(ip);
                return true;
            }
            return NewtonIntersect(surface1, bounds1, surface2, bounds2, surface3, bounds3, ref ip, out uv1, out uv2, out uv3);
        }
        internal static bool NewtonIntersect(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, ISurface surface3, BoundingRect bounds3, ref GeoPoint ip,
            out GeoPoint2D uv1, out GeoPoint2D uv2, out GeoPoint2D uv3)
        {
            // ausgehend vom Anfangspunkt wird versucht ein Schnittpunkt mit Newton zu finden. Konvergiert Newton nicht
            // innerhalb der bounds, wird false geliefert. Der Aufrufer muss dann ggf. unterteilen und weiter suchen
            // Verwendet wird das z.Z. von BRepOperation
            // zuerst noch die einfachen Fälle aussortieren
            uv1 = surface1.PositionOf(ip);
            uv2 = surface2.PositionOf(ip);
            uv3 = surface3.PositionOf(ip);
            GeoPoint p1 = surface1.PointAt(uv1);
            GeoPoint p2 = surface2.PointAt(uv2);
            GeoPoint p3 = surface3.PointAt(uv3);
            GeoVector u1 = surface1.UDirection(uv1);
            GeoVector v1 = surface1.VDirection(uv1);
            GeoVector u2 = surface2.UDirection(uv2);
            GeoVector v2 = surface2.VDirection(uv2);
            GeoVector u3 = surface3.UDirection(uv3);
            GeoVector v3 = surface3.VDirection(uv3);
            double error = (p1 | p2) + (p2 | p3) + (p3 | p1);
            while (error > 0)
            {   // Newton, allgeimer Schnitt von 3 Ebenen mit u/v Lösungen
                // Mit Normalengleichungen der Ebene braucht man nur 3 Gleichungen, muss aber dann noch die 3 u/v Punkte bestimmen
                // Wenn die Flächen recht tangential sind, ist die Fehlerabfrage schlecht, da man in uv noch sehr daneben liegen kann.
                // BRep-Operationen brauchen aber genaue Werte. Deshalb wird hier bis zu "Rauschen" konvergiert, durch die error/2 Bedingung
                // aber maximal 48 mal
                Matrix m = DenseMatrix.OfArray(new double[6, 6] { { u1.x, v1.x, -u2.x, -v2.x, 0, 0 }, { u1.y, v1.y, -u2.y, -v2.y, 0, 0 }, { u1.z, v1.z, -u2.z, -v2.z, 0, 0 }, { 0, 0, -u2.x, -v2.x, u3.x, v3.x }, { 0, 0, -u2.y, -v2.y, u3.y, v3.y }, { 0, 0, -u2.z, -v2.z, u3.z, v3.z } });
                try
                {
                    Vector x = (Vector)m.Solve(new DenseVector(new double[] { p2.x - p1.x, p2.y - p1.y, p2.z - p1.z, p2.x - p3.x, p2.y - p3.y, p2.z - p3.z }));
                    if (!x.IsValid())
                    {
                        if (error < Precision.eps) break; // geht wohl nicht besser
                        else return false;
                    }
                    uv1.x += x[0];
                    uv1.y += x[1];
                    uv2.x += x[2];
                    uv2.y += x[3];
                    uv3.x += x[4];
                    uv3.y += x[5];
                    p1 = surface1.PointAt(uv1);
                    p2 = surface2.PointAt(uv2);
                    p3 = surface3.PointAt(uv3);
                    double e = (p1 | p2) + (p2 | p3) + (p3 | p1);
                    if (e > error / 2.0)
                    {   // konvergiert nicht gut
                        if (e < Precision.eps) break; // wir sind ja schon gut
                        else return false;
                    }
                    error = e;
                    u1 = surface1.UDirection(uv1);
                    v1 = surface1.VDirection(uv1);
                    u2 = surface2.UDirection(uv2);
                    v2 = surface2.VDirection(uv2);
                    u3 = surface3.UDirection(uv3);
                    v3 = surface3.VDirection(uv3);
                }
                catch (ApplicationException)
                {
                    return false;
                }
            }
            ip = new GeoPoint(p1, p2, p3);
            return true;
        }
        internal static bool Overlapping(ISurface surface1, BoundingRect bounds1, ISurface surface2, BoundingRect bounds2, double precision, out ModOp2D From1To2)
        {   // zwei Oberflächen überlappen sich wenn sie eine gemeinsame Fläche haben
            // hier erstmal Abfragen nach gleichem Typ dort ist es besser zu lösen

            if ((surface1.GetType() == surface2.GetType()) && (!(surface1 is NurbsSurface)) && (!(surface1 is RuledSurface)))
            {
                return surface1.SameGeometry(bounds1, surface2, bounds2, Precision.eps, out From1To2);
            }
            // erste Bedingung ist, dass es mindestens zwei Eckunkte gibt, die in der jeweils anderen Fläche liegen
            // dann gibt es mehrere Fälle:
            // Alle Eckpunkte der einen Fläche liegen in der anderen: dann braucht man nur die eine zu berücksichtigen
            // gemischt: also es gibt einen von 1, der in 2 liegt und einen von 2, der in 1 liegt (oder jeweils mehrere)
            // Ein solches gemischte nicht identisches Paar wird gebraucht. Es halt also uv Werte in beiden Flächen
            // der uv Zwischenpunkt in beiden Flächen muss also auch in 3d identisch sein. Damit haben wir 
            // zwei uv-Tripel, die aufeinander abgebildet eine ModOp2D geben
            // Wir brinden das 2. Rechteck in das uv System des ersten und betrachten die Schnittfläche.
            // Auf dieser Fläche muss alles identisch sein. Es genügt die Basispunkte und die Normalen dort
            // zu betrachten
            // leider können sich die beiden Flächen auch so überdecken, dass keine Eckpunkte der einen in der jeweils anderen liegen
            // dann müssen aber die Kanten sich schneiden, jeweils 2 von der einen mit 2 von der anderen Fläche
            // hier wird nicht berücksichtigt, dass die Flächen völlig zueinander verzerrt sind, da gäbe es dann auch keine ModOp2D
            BoxedSurfaceEx bs1 = (surface1 as ISurfaceImpl).BoxedSurfaceEx;
            BoxedSurfaceEx bs2 = (surface2 as ISurfaceImpl).BoxedSurfaceEx;
            From1To2 = ModOp2D.Identity;
            if (!bs1.IsCloseTo(bs2)) return false;
            // die Eckpunkte bestimmen
            GeoPoint ll2 = surface2.PointAt(bounds2.GetLowerLeft());
            GeoPoint lr2 = surface2.PointAt(bounds2.GetLowerRight());
            GeoPoint ul2 = surface2.PointAt(bounds2.GetUpperLeft());
            GeoPoint ur2 = surface2.PointAt(bounds2.GetUpperRight());

            GeoPoint ll1 = surface1.PointAt(bounds2.GetLowerLeft());
            GeoPoint lr1 = surface1.PointAt(bounds2.GetLowerRight());
            GeoPoint ul1 = surface1.PointAt(bounds2.GetUpperLeft());
            GeoPoint ur1 = surface1.PointAt(bounds2.GetUpperRight());

            GeoPoint2D ll2on1, lr2on1, ul2on1, ur2on1, ll1on2, lr1on2, ul1on2, ur1on2;
            bool valid1 = false, valid2 = false;
            if (bs1.IsCloseTo(ll2))
            {
                // valid1 = true;
                ll2on1 = surface1.PositionOf(ll2);
                valid1 |= bounds1.Contains(ll2on1);
            }
            else ll2on1 = GeoPoint2D.Invalid;
            if (bs1.IsCloseTo(lr2))
            {
                // valid1 = true;
                lr2on1 = surface1.PositionOf(lr2);
                valid1 |= bounds1.Contains(lr2on1);
            }
            else lr2on1 = GeoPoint2D.Invalid;
            if (bs1.IsCloseTo(ul2))
            {
                // valid1 = true;
                ul2on1 = surface1.PositionOf(ul2);
                valid1 |= bounds1.Contains(ul2on1);
            }
            else ul2on1 = GeoPoint2D.Invalid;
            if (bs1.IsCloseTo(ur2))
            {
                // valid1 = true;
                ur2on1 = surface1.PositionOf(ur2);
                valid1 |= bounds1.Contains(ur2on1);
            }
            else ur2on1 = GeoPoint2D.Invalid;

            if (bs2.IsCloseTo(ll1))
            {
                // valid2 = true;
                ll1on2 = surface2.PositionOf(ll1);
                valid2 |= bounds1.Contains(ll1on2);
            }
            else ll1on2 = GeoPoint2D.Invalid;
            if (bs2.IsCloseTo(lr1))
            {
                // valid2 = true;
                lr1on2 = surface2.PositionOf(lr1);
                valid2 |= bounds1.Contains(lr1on2);
            }
            else lr1on2 = GeoPoint2D.Invalid;
            if (bs2.IsCloseTo(ul1))
            {
                // valid2 = true;
                ul1on2 = surface2.PositionOf(ul1);
                valid2 |= bounds1.Contains(ul1on2);
            }
            else ul1on2 = GeoPoint2D.Invalid;
            if (bs2.IsCloseTo(ur1))
            {
                // valid2 = true;
                ur1on2 = surface2.PositionOf(ur1);
                valid2 |= bounds1.Contains(ur1on2);
            }
            else ur1on2 = GeoPoint2D.Invalid;

            if (!valid1 && !valid2)
            {   // jetzt immer noch die Möglichkeit, dass sie sich überschneiden ohne gemeinsamen Eckpunkt
                ICurve bottom1 = surface1.FixedV(bounds1.Bottom, bounds1.Left, bounds1.Right);
                ICurve top1 = surface1.FixedV(bounds1.Top, bounds1.Left, bounds1.Right);
                ICurve left1 = surface1.FixedU(bounds1.Left, bounds1.Bottom, bounds1.Top);
                ICurve right1 = surface1.FixedU(bounds1.Right, bounds1.Bottom, bounds1.Top);

                ICurve bottom2 = surface2.FixedV(bounds2.Bottom, bounds2.Left, bounds2.Right);
                ICurve top2 = surface2.FixedV(bounds2.Top, bounds2.Left, bounds2.Right);
                ICurve left2 = surface2.FixedU(bounds2.Left, bounds2.Bottom, bounds2.Top);
                ICurve right2 = surface2.FixedU(bounds2.Right, bounds2.Bottom, bounds2.Top);

                // jede Kurve der 1. Fläche mit jeder der 2. Fläche schneiden
                double[] ipars = Curves.Intersect(bottom1, bottom2, true);

                // funktioniert noch nicht, nur für ebene Kurven
                From1To2 = ModOp2D.Identity;

                return false;
            }
            else
            {   // und hier kommt die schwierige Aufgabe, die gemeinsame Überschneidungsfläche zu finden und zu sehen, ob wir innerhalb dieser 
                // auch identisch sind
                // eigentlich müssten wir die Schnitte der 4 Kanten miteinander testen
                // um den jeweiligen uv Bereich zu bestimmen, in dem die Punkte getestet werden müssen
                double[] intu;
                double[] intv;
                bool pointsChecked = false; ;
                surface2.GetSafeParameterSteps(bounds2.Left, bounds2.Right, bounds2.Bottom, bounds2.Top, out intu, out intv);
                for (int i = 0; i < intu.Length; ++i)
                {
                    for (int j = 0; j < intv.Length; ++j)
                    {
                        GeoPoint2D uv2 = new GeoPoint2D(intu[i], intv[j]);
                        GeoPoint testPoint = surface2.PointAt(uv2);
                        GeoVector normal = surface2.GetNormal(uv2);
                        if (bs1.IsClose(testPoint))
                        {
                            GeoPoint2D uv1;
                            if (bs1.PositionOf(testPoint, out uv1))
                            {
                                // hier noch überprüfen, ob testPoint senkrecht über uv1 liegt, sonst 
                                // gibts Probleme am Rand
                                pointsChecked = true;
                                if ((surface1.PointAt(uv1) | testPoint) > precision) return false;
                                if (!Precision.SameDirection(surface1.GetNormal(uv1), normal, false)) return false;
                            }
                        }
                    }
                }
                // und auch noch umgekehrt testen:
                surface1.GetSafeParameterSteps(bounds1.Left, bounds1.Right, bounds1.Bottom, bounds1.Top, out intu, out intv);
                for (int i = 0; i < intu.Length; ++i)
                {
                    for (int j = 0; j < intv.Length; ++j)
                    {
                        GeoPoint2D uv1 = new GeoPoint2D(intu[i], intv[j]);
                        GeoPoint testPoint = surface1.PointAt(uv1);
                        GeoVector normal = surface1.GetNormal(uv1);
                        if (bs2.IsClose(testPoint))
                        {
                            GeoPoint2D uv2;
                            if (bs2.PositionOf(testPoint, out uv2))
                            {
                                // hier noch überprüfen, ob testPoint senkrecht über uv1 liegt, sonst 
                                // gibts Probleme am Rand
                                pointsChecked = true;
                                if ((surface2.PointAt(uv2) | testPoint) > precision) return false;
                                if (!Precision.SameDirection(surface2.GetNormal(uv2), normal, false)) return false;
                            }
                        }
                    }
                }
                // besser so: eine Liste von uv-Paaren erstellen, die sicher in beiden Flächen vorkommen:
                // also die Eckpunkte der einen in der anderen und umgekehrt
                // und die Schnittpunkte der Randkurven (nicht Berührpunkte z.B. bei überlappenden Kurven)
                // ein konvexes Polygon daraus bilden. Die Eckpunkte (sind es nicht immer 4?) auf Gleichheit überprüfen
                // und ggf. noch Innenpunkte bzw. Normalenvektoren (bei diesen ist die Genauigkeit ein Problem)
                return pointsChecked;
            }

            // From1To2 = ModOp2D.Identity;

            // return false;
        }

        internal static IDualSurfaceCurve[] IntersectInner(ISurface surface1, BoundingRect ext1, ISurface surface2, BoundingRect ext2)
        {
            int ep = surface1.GetExtremePositions(ext1, surface2, ext2, out List<Tuple<double, double, double, double>> extremePositions);
            List<IDualSurfaceCurve> res = new List<IDualSurfaceCurve>();
            if (extremePositions != null && extremePositions.Count > 0)
            {   // lets calculate some seeds from the fixed curves on one of the surfaces
                List<GeoPoint> seeds = new List<GeoPoint>();
                for (int i = 0; i < extremePositions.Count; i++)
                {
                    ICurve crv = null;
                    if (!double.IsNaN(extremePositions[i].Item1) && ext1.Left <= extremePositions[i].Item1 && ext1.Right >= extremePositions[i].Item1) crv = surface1.FixedU(extremePositions[i].Item1, ext2.Bottom, ext2.Top);
                    else if (!double.IsNaN(extremePositions[i].Item2) && ext1.Bottom <= extremePositions[i].Item2 && ext1.Top >= extremePositions[i].Item2) crv = surface1.FixedV(extremePositions[i].Item2, ext2.Left, ext2.Right);
                    if (!double.IsNaN(extremePositions[i].Item3) && ext2.Left <= extremePositions[i].Item3 && ext2.Right >= extremePositions[i].Item3) crv = surface2.FixedU(extremePositions[i].Item3, ext1.Bottom, ext1.Top);
                    else if (!double.IsNaN(extremePositions[i].Item4) && ext2.Bottom <= extremePositions[i].Item4 && ext2.Top >= extremePositions[i].Item4) crv = surface2.FixedV(extremePositions[i].Item4, ext1.Left, ext1.Right);
                    if (crv != null)
                    {
                        if (!double.IsNaN(extremePositions[i].Item1) && !double.IsNaN(extremePositions[i].Item2))
                        {
                            // crv is on surface1
                            surface2.Intersect(crv, ext2, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds);
                            for (int j = 0; j < ips.Length; j++)
                            {
                                if (ext2.Contains(uvOnFaces[j]) && uOnCurve3Ds[j] >= 0.0 && uOnCurve3Ds[j] <= 1.0) seeds.Add(ips[j]);
                            }

                        }
                        else
                        {
                            // crv is on surface2
                            surface1.Intersect(crv, ext1, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds);
                            for (int j = 0; j < ips.Length; j++)
                            {
                                if (ext1.Contains(uvOnFaces[j]) && uOnCurve3Ds[j] >= 0.0 && uOnCurve3Ds[j] <= 1.0) seeds.Add(ips[j]);
                            }
                        }
                    }
                }
                // seeds are not sorted. The curve must be closed. We simply repeat the first seed as the last seed
                if (seeds.Count > 1)
                {   // seeds are not sorted. But if we have 4 seeds, two curves have been used , the first curve created seed 0 and 1, the second 2 and 3
                    // so we better exchange 1 and 2
                    if (seeds.Count == 4)
                    {
                        GeoPoint tmp = seeds[1];
                        seeds[1] = seeds[2];
                        seeds[2] = tmp;
                    }
                    seeds.Add(seeds.First());
                    IDualSurfaceCurve[] candidates = surface1.GetDualSurfaceCurves(ext1, surface2, ext2, seeds, null);
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        if (candidates[i].Curve3D.IsClosed) res.Add(candidates[i]);
                        else if (Precision.Equals(candidates[i].Curve3D.StartPoint, candidates[i].Curve3D.EndPoint)) res.Add(candidates[i]);
                    }
                }
            }
            if (res.Count == 0 && ep == -1)
            {
                // GetExtremePositions is not implemented for all surface combinations. If it fails (returns -1) and GetDualSurfaceCurves didnt return anything
                // then we have to use burte force to get a seed point
                // problem with helicalsurface in "Stativgewinde1.cdb.json"
                //GeoPoint seed = IntersectionSeedPoint(surface1, ext1, surface2, ext2);
                //if (seed.IsValid)
                //{
                //    GeoPoint2D uv1 = surface1.PositionOf(seed);
                //    SurfaceHelper.AdjustPeriodic(surface1, ext1, ref uv1);
                //    GeoPoint2D uv2 = surface1.PositionOf(seed);
                //    SurfaceHelper.AdjustPeriodic(surface2, ext2, ref uv2);
                //    if (!ext1.Contains(uv1)) uv1 = GeoPoint2D.Invalid;
                //    if (!ext2.Contains(uv2)) uv2 = GeoPoint2D.Invalid;
                //    if (uv1.IsValid || uv2.IsValid)
                //    {
                //        extremePositions = new List<Tuple<double, double, double, double>>();
                //        extremePositions.Add(new Tuple<double, double, double, double>(uv1.x, uv1.y, uv2.x, uv2.y));
                //        candidates = surface1.GetDualSurfaceCurves(ext1, surface2, ext2, null, extremePositions);
                //        res = new List<IDualSurfaceCurve>();
                //        for (int i = 0; i < candidates.Length; i++)
                //        {
                //            if (candidates[i].Curve3D.IsClosed) res.Add(candidates[i]);
                //            else if (Precision.Equals(candidates[i].Curve3D.StartPoint, candidates[i].Curve3D.EndPoint)) res.Add(candidates[i]);
                //        }
                //    }
                //}
            }
            return res.ToArray();
        }
        private static GeoPoint IntersectionSeedPoint(ISurface surface1, BoundingRect ext1, ISurface surface2, BoundingRect ext2)
        {
            Face fc1 = Face.MakeFace(surface1, new SimpleShape(ext1.ToBorder()));
            Face fc2 = Face.MakeFace(surface2, new SimpleShape(ext2.ToBorder()));
            BoundingCube ext = fc1.GetExtent(0.0) + fc1.GetExtent(0.0);
            bool found = false;
            GeoPoint seed = GeoPoint.Invalid;
            bool SplitTestFunction(OctTree<Face>.Node<Face> node, Face objectToAdd)
            {
                if (found) return false;
                if (node.list != null && node.list.Count > 0 && !node.list.Contains(objectToAdd))
                {
                    if (node.size > ext.Size * 1e-3) return true;
                    GeoPoint testPoint = node.center;
                    if (NewtonIntersect(surface1, ext1, surface2, ext2, ref testPoint))
                    {
                        found = true;
                        seed = testPoint;
                        return false;
                    }
                }
                return false;
            }
            OctTree<Face> ot = new OctTree<Face>(ext, Precision.eps, SplitTestFunction);
            ot.AddObject(fc1);
            ot.AddObject(fc2);
            return seed;
        }

        private static bool NewtonIntersect(ISurface surface1, BoundingRect ext1, ISurface surface2, BoundingRect ext2, ref GeoPoint testPoint)
        {
            GeoPoint2D uv1 = surface1.PositionOf(testPoint);
            SurfaceHelper.AdjustPeriodic(surface1, ext1, ref uv1);
            GeoPoint2D uv2 = surface2.PositionOf(testPoint);
            SurfaceHelper.AdjustPeriodic(surface2, ext2, ref uv2);
            double dist = surface1.PointAt(uv1) | surface2.PointAt(uv2);
            while (dist > Precision.eps)
            {
                Plane pln1 = new Plane(surface1.PointAt(uv1), surface1.GetNormal(uv1));
                Plane pln2 = new Plane(surface2.PointAt(uv2), surface2.GetNormal(uv2));
                if (pln1.Intersect(pln2, out GeoPoint loc, out GeoVector dir))
                {
                    testPoint = Geometry.DropPL(testPoint, loc, dir);
                    uv1 = surface1.PositionOf(testPoint);
                    uv2 = surface2.PositionOf(testPoint);
                    double newdist = surface1.PointAt(uv1) | surface2.PointAt(uv2);
                    if (newdist > dist) return false;
                    else dist = newdist;
                }
                else break;
            }
            return (dist <= Precision.eps);
        }
        internal static bool NewtonIntersect(ISurface surface, BoundingRect ext, ICurve crv, ref GeoPoint testPoint)
        {
            GeoPoint2D uv = surface.PositionOf(testPoint);
            SurfaceHelper.AdjustPeriodic(surface, ext, ref uv);
            double u = crv.PositionOf(testPoint);
            double dist = surface.PointAt(uv) | crv.PointAt(u);
            try
            {
                int maxIteration = 10;
                while (maxIteration > 0) // Precision.eps)
                {
                    --maxIteration;
                    Plane pln = new Plane(surface.PointAt(uv), surface.GetNormal(uv));
                    GeoPoint loc = testPoint;
                    GeoVector dir = crv.DirectionAt(u);
                    testPoint = pln.Intersect(testPoint, dir);
                    uv = surface.PositionOf(testPoint);
                    u = crv.PositionOf(testPoint);
                    double newdist = surface.PointAt(uv) | testPoint;
                    if (newdist >= dist) break;
                    else dist = newdist;
                }
            }
            catch (PlaneException) { } // we are tangential!
            return (dist <= Precision.eps);
        }

        internal static bool NewtonPerpendicular(ISurface surface, BoundingRect ext, ICurve crv, ref GeoPoint testPoint)
        {

            //GeoVector du, dv, duu, dvv, duv;
            //GeoPoint loc;
            //surface.Derivation2At(uv, out loc, out du, out dv, out duu, out dvv, out duv);
            //GeoVector n = du ^ dv;
            //GeoVector diruv = n ^ dir;
            //GeoVector2D duvs = Geometry.Dir2D(du, dv, diruv);
            //surface.DerivationAt(uv + duvs, out loc, out du, out dv);
            //n = du ^ dv;
            //surface.DerivationAt(uv - duvs, out loc, out du, out dv);
            //n = du ^ dv;
            //surface.DerivationAt(uv + duvs.ToLeft(), out loc, out du, out dv);
            //n = du ^ dv;
            //surface.DerivationAt(uv + duvs.ToRight(), out loc, out du, out dv);
            //n = du ^ dv;

            return false;
        }
        /// <summary>
        /// A <paramref name="surface"/> should be tangential to other <paramref name="tangentialSurfaces"/>. This is used in the parametrics
        /// and only implemented for a few cases here.
        /// 
        /// </summary>
        /// <param name="surface"></param>
        /// <param name="tangentialSurfaces"></param>
        /// <returns></returns>
        internal static ISurface ModifyTangential(ISurface surface, List<ISurface> tangentialSurfaces)
        {
            if (surface is PlaneSurface pls)
            {
                if (tangentialSurfaces.Count == 2 && tangentialSurfaces[0] is CylindricalSurface cyl0 && tangentialSurfaces[1] is CylindricalSurface cyl1)
                {
                    // a plane tangential to two cylinders
                    if (Precision.SameDirection(cyl0.Axis, cyl1.Axis, false))
                    {
                        PlaneSurface res = null;
                        Plane pln = new Plane(cyl0.Location, cyl0.Axis);
                        Circle2D c0 = new Circle2D(GeoPoint2D.Origin, cyl0.RadiusX);
                        Circle2D c1 = new Circle2D(pln.Project(cyl1.Location), cyl1.RadiusX);
                        GeoPoint2D[] tp = Curves2D.TangentLines(c0, c1);
                        double mindist = double.MaxValue;
                        for (int i = 0; i < tp.Length; i += 2)
                        {
                            GeoPoint tp0 = pln.ToGlobal(tp[i]);
                            GeoPoint tp1 = pln.ToGlobal(tp[i + 1]);
                            double d = pls.GetDistance(tp0) + pls.GetDistance(tp1);
                            if (d < mindist)
                            {
                                mindist = d;
                                res = new PlaneSurface(tp0, tp1 - tp0, pln.Normal);
                            }
                        }
                        return res;
                    }

                }
            }
            else if (surface is CylindricalSurface cyl)
            {
                if (tangentialSurfaces.Count == 2 && tangentialSurfaces[0] is ToroidalSurface tor0 && tangentialSurfaces[1] is ToroidalSurface tor1)
                {
                    if (Precision.SameDirection(tor0.ZAxis, tor1.ZAxis, false))
                    {
                        Plane pln = new Plane(tor0.Location, tor0.XAxis, tor0.YAxis);
                        Circle2D c0 = new Circle2D(GeoPoint2D.Origin, tor0.XAxis.Length);
                        Circle2D c1 = new Circle2D(pln.Project(tor1.Location), tor1.XAxis.Length);
                        GeoPoint2D[] tp = Curves2D.TangentLines(c0, c1);
                        double mindist = double.MaxValue;
                        CylindricalSurface res = null;
                        for (int i = 0; i < tp.Length; i += 2)
                        {
                            GeoPoint tp0 = pln.ToGlobal(tp[i]);
                            GeoPoint tp1 = pln.ToGlobal(tp[i + 1]);
                            double d = Geometry.DistPL(tp0, cyl.Location, cyl.Axis) + Geometry.DistPL(tp1, cyl.Location, cyl.Axis);
                            if (d < mindist)
                            {
                                mindist = d;
                                GeoVector dirz = tp1 - tp0;
                                GeoVector dirx = dirz ^ pln.Normal;
                                GeoVector diry = pln.Normal;
                                dirx.Length = tor0.MinorRadius;
                                diry.Length = tor0.MinorRadius;
                                res = new CylindricalSurface(tp0, dirx, diry, dirz);
                            }
                        }
                        return res;
                    }
                }
            }

            return null;
        }
        /// <summary>
        /// Find intersection curves of two cylinders. Result may be 0, 1 or 2 curves. Infinite lines (parallel or tangential cylinders) are returned as finite Lines.
        /// The other cases result in closed curves (ellipses or BSplines) with arbitrary start/end points
        /// </summary>
        /// <param name="cylinder1"></param>
        /// <param name="cylinder2"></param>
        /// <returns></returns>
        internal static ICurve[] Intersect(ICylinder cylinder1, ICylinder cylinder2)
        {
            if (Precision.SameDirection(cylinder1.Axis.Direction, cylinder2.Axis.Direction, false))
            {

            }
            throw new NotImplementedException();
        }
    }
    internal class FindTangentCurves
    {   // während der Kurvenfindung braucht es einige Daten die global bleiben, damit nicht so viel über
        // die Parameter übergeben werden muss
        class UnableToFindZero : ApplicationException
        {
            int uind;
            int vind;
            public UnableToFindZero(int uind, int vind)
            {
                this.uind = uind;
                this.vind = vind;
            }
        }
        ISurface surface; // die Kurve selbst
        GeoVector direction; // die Betrachtungsrichtung
        double[] usteps; // Parameterschritte
        double[] vsteps;
        double?[,] uSections;
        double?[,] vSections;
        bool[,] uSectionsUsed;
        bool[,] vSectionsUsed;
        double precisionU, precisionV;
        public FindTangentCurves(ISurface surface)
        {
            this.surface = surface;
        }
        private void FindCurveFromUSection(List<GeoPoint2D> curve, int uind, int vind)
        {
            // suche eine Kante, die einen Nullpunkt hat
            // ind ist der Index in den uSections, par der v-Parameter
            if (CheckUSection(curve, uind, vind - 1, uSections[vind, uind].Value, vsteps[vind])) return;
            if (CheckUSection(curve, uind, vind + 1, uSections[vind, uind].Value, vsteps[vind])) return;
            if (CheckVSection(curve, uind, vind, uSections[vind, uind].Value, vsteps[vind])) return;
            if (CheckVSection(curve, uind + 1, vind, uSections[vind, uind].Value, vsteps[vind])) return;
            if (CheckVSection(curve, uind, vind - 1, uSections[vind, uind].Value, vsteps[vind])) return;
            if (CheckVSection(curve, uind + 1, vind - 1, uSections[vind, uind].Value, vsteps[vind])) return;
        }
        private void FindCurveFromVSection(List<GeoPoint2D> curve, int uind, int vind)
        {
            // suche eine Kante, die einen Nullpunkt hat
            // ind ist der Index in den uSections, par der v-Parameter
            if (CheckVSection(curve, uind - 1, vind, usteps[uind], vSections[uind, vind].Value)) return;
            if (CheckVSection(curve, uind + 1, vind, usteps[uind], vSections[uind, vind].Value)) return;
            if (CheckUSection(curve, uind, vind, usteps[uind], vSections[uind, vind].Value)) return;
            if (CheckUSection(curve, uind, vind + 1, usteps[uind], vSections[uind, vind].Value)) return;
            if (CheckUSection(curve, uind - 1, vind, usteps[uind], vSections[uind, vind].Value)) return;
            if (CheckUSection(curve, uind - 1, vind + 1, usteps[uind], vSections[uind, vind].Value)) return;
        }
        private bool CheckUSection(List<GeoPoint2D> curve, int uind, int vind, double u, double v)
        {
            if (uind >= 0 && uind < usteps.Length - 1 && vind >= 0 && vind < vsteps.Length)
            {
                if (uSections[vind, uind].HasValue && !uSectionsUsed[vind, uind])
                {
                    {
                        AddIntermediatePoints(curve, u, v, uSections[vind, uind].Value, vsteps[vind]);
                        curve.Add(new GeoPoint2D(uSections[vind, uind].Value, vsteps[vind]));
                        uSectionsUsed[vind, uind] = true;
                        FindCurveFromUSection(curve, uind, vind);
                        return true;
                    }
                }
            }
            return false;
        }
        private bool CheckVSection(List<GeoPoint2D> curve, int uind, int vind, double u, double v)
        {
            if (uind >= 0 && uind < usteps.Length && vind >= 0 && vind < vsteps.Length - 1)
            {
                if (vSections[uind, vind].HasValue && !vSectionsUsed[uind, vind])
                {
                    {
                        AddIntermediatePoints(curve, u, v, usteps[uind], vSections[uind, vind].Value);
                        curve.Add(new GeoPoint2D(usteps[uind], vSections[uind, vind].Value));
                        vSectionsUsed[uind, vind] = true;
                        FindCurveFromVSection(curve, uind, vind);
                        return true;
                    }
                }
            }
            return false;
        }
        private void AddIntermediatePoints(List<GeoPoint2D> curve, double u1, double v1, double u2, double v2)
        {
            double u = (u1 + u2) / 2.0;
            double v = (v1 + v2) / 2.0;

            double tanuv1 = GetTangentValue(u1, v1);
            double tanuv2 = GetTangentValue(u2, v2);

            double umin = 0.0, umax = 0.0, vmin = 0.0, vmax = 0.0;
            int uind = 0, vind = 0;
            for (int i = 0; i < usteps.Length; ++i)
            {
                if (usteps[i] < u)
                {
                    umin = umax = usteps[i];
                    uind = i;
                }
                else
                {
                    umax = usteps[i];
                    break;
                }
            }
            for (int i = 0; i < vsteps.Length; ++i)
            {
                if (vsteps[i] < v)
                {
                    vmin = vmax = vsteps[i];
                    vind = i;
                }
                else
                {
                    vmax = vsteps[i];
                    break;
                }
            }
            double fneg = double.MaxValue;
            double fpos = double.MaxValue;
            double du = v2 - v1;    // senkrecht dazu
            double dv = u1 - u2;
            if (du > 0.0)
            {
                fpos = (umax - u) / du;
                fneg = (u - umin) / du;
            }
            else if (du < 0.0)
            {
                fpos = (umin - u) / du;
                fneg = (u - umax) / du;
            }
            if (dv > 0.0)
            {
                fpos = Math.Min(fpos, (vmax - v) / dv);
                fneg = Math.Min(fneg, (v - vmin) / dv);
            }
            else if (dv < 0.0)
            {
                fpos = Math.Min(fpos, (vmin - v) / dv);
                fneg = Math.Min(fneg, (v - vmax) / dv);
            }
            // wir gehen in der Mitte quer zur Verbindungsstrecke, dabei ist das Problem, wie weit darf oder muss man ausladen
            // um einen Vorzeichenwechsel zu erreichen und ohne eine weiter Kurve einzugreifen.
            // ob es dafür einen vernünftige Lösung gibt???
            // fpos bzw. fneg sind so gewählt, dass das Kästchen nicht verlassen wird.
            // Wenn es dabei keine Nullstelle gibt, muss feiner aufgetilt werden. Das hat zur Folge,
            // dass die Kurve ein Kästchen nicht auf einer Seite verlassen kann und wieder in die selbe Seite
            // eintreten kann.
            double tan1 = GetTangentValue(u + fpos * du, v + fpos * dv);
            double tan2 = GetTangentValue(u - fneg * du, v - fneg * dv);
            if (DifferentSign(tan1, tan2))
            {
                double u0, v0;
                FindZero(u + fpos * du, v + fpos * dv, tan1, u - fneg * du, v - fneg * dv, tan2, out u0, out v0);
                // der neue Punkt muss in der Nähe sein, sonst kann es sein, dass man immer hin und her springt zwischen 2 verschiedenen Kurven
                if ((new GeoPoint2D(u0, v0) | new GeoPoint2D(u, v)) > (new GeoPoint2D(u1, v1) | new GeoPoint2D(u2, v2)) / 2.0)
                {
                    // der neue Punkt liegt außerhalb des Kreises um die Strecke (u1,v1) -> (u2,v2)
                    // damit wären die neuen Abschnitte länger als der bestehende und es kann sein, dass wir nicht konvergieren
                    // also hier einfach den Zwischenpunkt unter den Tisch fallen lassen
                }
                else if ((Math.Abs(u - u0) < precisionU && Math.Abs(v - v0) < precisionV) || curve.Count > 1000)
                {
                    curve.Add(new GeoPoint2D(u0, v0));
                }
                else
                {
                    AddIntermediatePoints(curve, u1, v1, u0, v0);
                    curve.Add(new GeoPoint2D(u0, v0));
                    AddIntermediatePoints(curve, u0, v0, u2, v2);
                }
            }
            else
            {
                // es kann keine Nullstelle gefunden werden, wir müssen halt feiner Aufteilen
                throw new UnableToFindZero(uind, vind);
            }
        }
        private void FindZero(double u1, double v1, double tan1, double u2, double v2, double tan2, out double u, out double v)
        {   // erstmal als primitive bisection imeplementieren
            u = (u1 + u2) / 2.0;
            v = (v1 + v2) / 2.0;
            for (int i = 0; i < 48; i++)
            {
                double tan = GetTangentValue(u, v);
                if (DifferentSign(tan, tan1))
                {
                    tan2 = tan;
                    u2 = u;
                    v2 = v;
                }
                else
                {
                    tan1 = tan;
                    u1 = u;
                    v1 = v;
                }
                u = (u1 + u2) / 2.0;
                v = (v1 + v2) / 2.0;
            }
        }
        private static bool DifferentSign(double d1, double d2)
        {   // am linken rand gehört die null dazu, am rechten nicht
            return (d1 > 0.0 && d2 <= 0.0) || (d1 <= 0.0 && d2 > 0.0);
        }
        private double GetTangentValue(double u, double v)
        {   // das Skalarprodukt von Blickrichtung und Normalenvektor. Das wird null bei tangentialer Blickrichtung.
            return direction * surface.GetNormal(new GeoPoint2D(u, v));
        }
        public virtual ICurve2D[] GetTangentCurves(GeoVector direction, double umin, double umax, double vmin, double vmax)
        {
            this.direction = direction;
            precisionU = Math.Abs(umax - umin) * 1e-3;
            precisionV = Math.Abs(vmax - vmin) * 1e-3;
            /*
             * Erzeuge ein Schachbrett aus den GetSafeParameterSteps. Finde alle unterteilungspunkte auf den Kanten.
             * Erzeuge offene oder geschlossene 2d Punktfolgen aus diesen unterbrechungen. Ein Schachfeld sollte
             * keine oder zwei Kanten unterbrochen haben. Ungerade ist nicht möglich. Sind 4 Kanten unterbrochen,
             * so müsste man nochmals unterteilen. Schneiden können sich die Konturlinien nicht, dass sie sich
             * berühren, kann ich mir nicht vorstellen, ist aber theoretisch denkbar. (Konturlinien teilen die 
             * Fläche in einen positiven und einen negativen Bereich, deshalb können sie sich nicht schneiden)
             * Dann erzeuge Zwischenpunkte in der Punktfolge. Wenn der Mittelpunkt und der Zwischenpunkt im 3D
             * (oder besser in der projektionsfläche von direction) ein gewisses Maß unterschreiten, dann ist man fertig.
             * Mache aus den punktfolgen NURBS Kurven.
             */
            surface.GetSafeParameterSteps(umin, umax, vmin, vmax, out usteps, out vsteps);
            // das einzige Problem sind hier Singularitäten. Hier ein Versuch, damit umzugehen:
            // Singularitäten kommen nur am Anfang oder Ende vor, also diese bereiche überprüfen
            GeoVector ntest = surface.GetNormal(new GeoPoint2D(umin, (vmin + vmax) / 2.0));
            if (Precision.IsNullVector(ntest))
            {
                usteps[0] += (usteps[1] - usteps[0]) * 1e-3;
            }
            ntest = surface.GetNormal(new GeoPoint2D(umax, (vmin + vmax) / 2.0));
            if (Precision.IsNullVector(ntest))
            {
                usteps[usteps.Length - 1] -= (usteps[usteps.Length - 1] - usteps[usteps.Length - 2]) * 1e-3;
            }
            ntest = surface.GetNormal(new GeoPoint2D((umin + umax) / 2.0, vmin));
            if (Precision.IsNullVector(ntest))
            {
                vsteps[0] += (vsteps[1] - vsteps[0]) * 1e-3;
            }
            ntest = surface.GetNormal(new GeoPoint2D((umin + umax) / 2.0, vmax));
            if (Precision.IsNullVector(ntest))
            {
                vsteps[vsteps.Length - 1] -= (vsteps[vsteps.Length - 1] - vsteps[vsteps.Length - 2]) * 1e-3;
            }
            bool success = false;
            while (!success)
            {
                success = true; // wird nur im catch wieder auf false gesetzt
                double[,] vertex = new double[usteps.Length, vsteps.Length];
                // das sind die Ecken im Schachbrett
                for (int i = 0; i < usteps.Length; i++)
                {
                    for (int j = 0; j < vsteps.Length; j++)
                    {
                        vertex[i, j] = GetTangentValue(usteps[i], vsteps[j]);
                    }
                }
                // Jetzt haben alle Knoten einen Wert. Wenn zwei benachbarte verschiedenes Vorzeichen haben, dann
                // interpolieren und den Punkt bestimmen. Mit diesem Punkt eine neue Kette beginnen und die Kante als
                // benutzt markieren. Jetzt gucken, wos möglicherweise weitergeht, wenns keine Randkante ist.
                // Damit man nicht vorwärts und rückwärts suchen muss, am besten mit den Randkanten anfangen.
                // Wenn die alle aufgebraucht sind, gibt es nur noch geschlossene innere kanten.
                uSections = new double?[vsteps.Length, usteps.Length - 1];
                vSections = new double?[usteps.Length, vsteps.Length - 1];
                uSectionsUsed = new bool[vsteps.Length, usteps.Length - 1];
                vSectionsUsed = new bool[usteps.Length, vsteps.Length - 1];
                // uSections sind die Nullpunkte bei festem v in u-Richtung
                // uSectionsUsed besagt, ob sie schon benutzt wurden

                try
                {
                    for (int j = 0; j < vsteps.Length; j++)
                    {
                        for (int i = 0; i < usteps.Length - 1; i++)
                        {
                            if (DifferentSign(vertex[i, j], vertex[i + 1, j]))
                            {
                                double u, v;
                                FindZero(usteps[i], vsteps[j], vertex[i, j], usteps[i + 1], vsteps[j], vertex[i + 1, j], out u, out v);
                                uSections[j, i] = u;
                            }
                        }
                    }
                    for (int i = 0; i < usteps.Length; i++)
                    {
                        for (int j = 0; j < vsteps.Length - 1; j++)
                        {
                            if (DifferentSign(vertex[i, j], vertex[i, j + 1]))
                            {
                                double u, v;
                                FindZero(usteps[i], vsteps[j], vertex[i, j], usteps[i], vsteps[j + 1], vertex[i, j + 1], out u, out v);
                                vSections[i, j] = v;
                            }
                        }
                    }

                    // Achtung: wenn Maschen entstehen, bei denen 2 Kurven durchgehen, dann  muss man feiner aufteilen
                    List<List<GeoPoint2D>> allCurves = new List<List<GeoPoint2D>>();
                    for (int i = 0; i < usteps.Length - 1; ++i)
                    {
                        // unterer Rand in U-Richtung
                        if (uSections[0, i].HasValue && !uSectionsUsed[0, i])
                        {
                            List<GeoPoint2D> curve = new List<GeoPoint2D>();
                            allCurves.Add(curve);
                            curve.Add(new GeoPoint2D(uSections[0, i].Value, vsteps[0]));
                            uSectionsUsed[0, i] = true;
                            FindCurveFromUSection(curve, i, 0);
                        }
                        // oberer Rand in U-Richtung
                        if (uSections[vsteps.Length - 1, i].HasValue && !uSectionsUsed[vsteps.Length - 1, i])
                        {
                            List<GeoPoint2D> curve = new List<GeoPoint2D>();
                            allCurves.Add(curve);
                            curve.Add(new GeoPoint2D(uSections[vsteps.Length - 1, i].Value, vsteps[vsteps.Length - 1]));
                            uSectionsUsed[vsteps.Length - 1, i] = true;
                            FindCurveFromUSection(curve, i, vsteps.Length - 1);
                        }
                    }
                    for (int i = 0; i < vsteps.Length - 1; ++i)
                    {
                        // linker Rand
                        if (vSections[0, i].HasValue && !vSectionsUsed[0, i])
                        {
                            List<GeoPoint2D> curve = new List<GeoPoint2D>();
                            allCurves.Add(curve);
                            curve.Add(new GeoPoint2D(usteps[0], vSections[0, i].Value));
                            vSectionsUsed[0, i] = true;
                            FindCurveFromVSection(curve, 0, i);
                        }
                        // rechter Rand
                        if (vSections[usteps.Length - 1, i].HasValue && !vSectionsUsed[usteps.Length - 1, i])
                        {
                            List<GeoPoint2D> curve = new List<GeoPoint2D>();
                            allCurves.Add(curve);
                            curve.Add(new GeoPoint2D(usteps[usteps.Length - 1], vSections[usteps.Length - 1, i].Value));
                            vSectionsUsed[usteps.Length - 1, i] = true;
                            FindCurveFromVSection(curve, usteps.Length - 1, i);
                        }
                    }
                    // und jetzt noch die inneren Kurven, die müssen immer geschlossen sein...
                    for (int i = 0; i < usteps.Length - 1; ++i)
                    {
                        for (int j = 1; j < vsteps.Length - 2; ++j)
                        {
                            // in U-Richtung
                            if (uSections[j, i].HasValue && !uSectionsUsed[j, i])
                            {
                                List<GeoPoint2D> curve = new List<GeoPoint2D>();
                                allCurves.Add(curve);
                                curve.Add(new GeoPoint2D(uSections[j, i].Value, vsteps[j]));
                                uSectionsUsed[j, i] = true;
                                FindCurveFromUSection(curve, i, j);
                                // throw new ApplicationException("hier noch das schließen implementieren");
                                // hier noch schließen
                            }
                        }
                    }
                    for (int i = 0; i < vsteps.Length - 1; ++i)
                    {
                        for (int j = 1; j < usteps.Length - 2; ++j)
                        {
                            if (vSections[j, i].HasValue && !vSectionsUsed[j, i])
                            {
                                List<GeoPoint2D> curve = new List<GeoPoint2D>();
                                allCurves.Add(curve);
                                curve.Add(new GeoPoint2D(usteps[j], vSections[j, i].Value));
                                vSectionsUsed[j, i] = true;
                                FindCurveFromVSection(curve, j, i);
                                // throw new ApplicationException("hier noch das schließen implementieren");
                                // hier noch schließen
                            }
                        }
                    }
                    // jetzt liegen alle Konturlinien als offene oder geschlossene Polylinien vor
                    // man könnte natürlich NURBS draus machen, das muss man noch sehen
                    List<ICurve2D> res = new List<ICurve2D>();
                    for (int i = 0; i < allCurves.Count; i++)
                    {
                        if (allCurves[i].Count > 1)
                        {
                            try
                            {
                                Polyline2D p2d = new Polyline2D(allCurves[i].ToArray());
                                res.Add(p2d);
                            }
                            catch (Polyline2DException)
                            {   // alle Punkte einer Kurve identisch
                            }
                        }
                    }
                    // Es muss noch implementiert werden: Singularitäten finden, denn dort müssen die
                    // Flächen aufgeteilt werden. Singularitäten sind die Punkte, an denen die 3D Konturkurve in Richtung
                    // der Blickrichtung geht. Dazu muss man das Minimum der Länge des Kreuzproduktes der direction mit zwei
                    // aufeinanderfolgenden Punkten (beide normiert auf die Länge 1) bestimmen. Wo das Minimum auftritt
                    // könnte eine Singularität liegen, ggf. dort noch Zwischenpunkte suchen.
                    // Das Thema Singularitäten ist doch etwas umfangreicher, denn es müssten nicht nur die hier gefundenen
                    // Konturlinien überprüft werden, sondern auch allgemein alle Randlinien der Faces. 
                    for (int i = 0; i < allCurves.Count; i++)
                    {
                    }
                    return res.ToArray();
                }
                catch (UnableToFindZero)
                {
                    // hier kommen wir hin, wenn in einem Kästchen die Kurve nicht bestimmt werden konnte.
                    // es gibt zwei Gründe: die Kurve verlässt an einer Kante das Kästchen und tritt an der selben Kante
                    // wieder ein, ODER es sind zwei Kurven, die an verschiedenen Kanten eintreten und an einer Kante
                    // wieder austreten. In beiden Fällen muss man genauer aufteilen. Diese Fälle sind aber sehr selten
                    // und tief in der Rekursion, so dass ExceptionHandling in diesem Fall OK ist.
                    // Notbremse bei patologischen Fällen, damit keine Endlosschleife.
                    if (usteps.Length + vsteps.Length < 1000)
                    {
                        success = false;
                        // usteps und vsteps mit Zwischenpunkten versehen und das ganze Spielchen von neuem machen
                        double[] dusteps = new double[usteps.Length * 2 - 1];
                        double[] dvsteps = new double[vsteps.Length * 2 - 1];
                        for (int i = 0; i < usteps.Length - 1; i++)
                        {
                            dusteps[2 * i] = usteps[i];
                            dusteps[2 * i + 1] = (usteps[i] + usteps[i + 1]) / 2.0;
                        }
                        for (int i = 0; i < vsteps.Length - 1; i++)
                        {
                            dvsteps[2 * i] = vsteps[i];
                            dvsteps[2 * i + 1] = (vsteps[i] + vsteps[i + 1]) / 2.0;
                        }
                        dusteps[usteps.Length * 2 - 2] = usteps[usteps.Length - 1];
                        dvsteps[vsteps.Length * 2 - 2] = vsteps[vsteps.Length - 1];
                        usteps = dusteps;
                        vsteps = dvsteps;
                    }
                    // ansonsten die Hoffnung aufgeben und kein Ergebnis liefern...
                }
            }
            return new ICurve2D[0];
        }
    }

    /// <summary>
    /// Ein Klasse, die ein Surface Objekt mit Würfeln einhüllt: Jeder Patch hat einen BoundingCube. Alle BoundingCubes
    /// sind in einem OctTree enthalten. Wenn ein Würfelchen verkleinert werden muss, dann wird es aus dem
    /// OctTree entfernt und die kleinen werden eingefügt. Die Würfelchen können sich überlappen
    /// </summary>
    internal class BoxedSurface
    {
        class DidntConverge : ApplicationException
        {
            public DidntConverge() { }
        }
        class Cube : IOctTreeInsertable
        {   // 
            public BoundingCube boundingCube;
            public BoundingRect uvPatch;
            public GeoPoint pll, plr, pul, pur; // die 4 Eckpunkte und die 4 Richtungen
            public GeoVector nll, nlr, nul, nur; // die Normalen in den Ecken
                                                 // hier auch noch die 4 Eckpunkte speichern, die werden vermutlich auch öfter gebraucht
            #region IOctTreeInsertable Members
            BoundingCube IOctTreeInsertable.GetExtent(double precision)
            {
                return boundingCube;
            }
            bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
            {
                return cube.Interferes(boundingCube);
            }
            bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
            {   // wird hoffentlich nicht gebraucht
                throw new Exception("The method or operation is not implemented.");
            }
            double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
            {   // wird hoffentlich nicht gebraucht
                throw new Exception("The method or operation is not implemented.");
            }
            bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
            {
                throw new Exception("The method or operation is not implemented.");
            }
            #endregion
        }
        OctTree<Cube> octtree;
        BoundingRect uvbounds;
        ISurface surface;
        public BoxedSurface(ISurface surface, BoundingRect extent)
        {
            this.surface = surface;
            double[] usteps, vsteps;
            surface.GetSafeParameterSteps(extent.Left, extent.Right, extent.Bottom, extent.Top, out usteps, out vsteps);
            uvbounds = new BoundingRect(usteps[0], vsteps[0], usteps[usteps.Length - 1], vsteps[vsteps.Length - 1]);
            octtree = new OctTree<Cube>(surface.GetPatchExtent(new BoundingRect(usteps[0], vsteps[0], usteps[usteps.Length - 1], vsteps[vsteps.Length - 1])), 0.0);
            for (int i = 0; i < usteps.Length - 1; ++i)
            {
                for (int j = 0; j < vsteps.Length - 1; ++j)
                {
                    AddCube(new BoundingRect(usteps[i], vsteps[j], usteps[i + 1], vsteps[j + 1]));
                }
            }
        }

        private void AddCube(BoundingRect uvPatch)
        {
            Cube cube = new Cube();
            cube.uvPatch = uvPatch;
            cube.pll = surface.PointAt(uvPatch.GetLowerLeft());
            cube.plr = surface.PointAt(uvPatch.GetLowerRight());
            cube.pul = surface.PointAt(uvPatch.GetUpperLeft());
            cube.pur = surface.PointAt(uvPatch.GetUpperRight());
            cube.nll = surface.GetNormal(uvPatch.GetLowerLeft());
            cube.nlr = surface.GetNormal(uvPatch.GetLowerRight());
            cube.nul = surface.GetNormal(uvPatch.GetUpperLeft());
            cube.nur = surface.GetNormal(uvPatch.GetUpperRight());
            cube.nll.NormIfNotNull();
            cube.nlr.NormIfNotNull();
            cube.nul.NormIfNotNull();
            cube.nur.NormIfNotNull();
            bool toosmall = uvPatch.Size < (uvbounds.Size * 1e-3) || uvPatch.Width < (uvbounds.Width * 1e-3) || uvPatch.Height < (uvbounds.Height * 1e-3);
            // Notbremse: an singulären Stellen wird der Patch zu klein

            // die Normalen sind normiert oder???
            // Die Normalen sollen keine größeren Winkel einschließen als 45° (der Wert kann noch besser einjustiert werden
            double lim = Math.Sqrt(2.0) / 2.0; //45°, es gibt aber keinen logischen Grund dafür
            if ((Math.Abs(cube.nll * cube.nlr) < lim || Math.Abs(cube.nll * cube.nul) < lim || Math.Abs(cube.nll * cube.nur) < lim ||
                Math.Abs(cube.nlr * cube.nul) < lim || Math.Abs(cube.nlr * cube.nur) < lim || Math.Abs(cube.nul * cube.nur) < lim) && !toosmall)
            {   // hier einfach zu vierteilen leiefrt manchmal schlechte Ergebnisse
                // deshalb hier besser überprüfen und nur zweiteilen
                bool splitu = false;
                if ((Math.Abs(cube.nll * cube.nlr) < lim || Math.Abs(cube.nul * cube.nur) < lim)) splitu = true;
                else if (Math.Abs(cube.nll * cube.nul) < lim || Math.Abs(cube.nlr * cube.nur) < lim) splitu = false;
                else
                {   // Überschreitung nur in der Diagonalen, wir überprüfen den Raumabstand der Eckpunkte der Masche
                    splitu = ((cube.pll | cube.plr) + (cube.pul | cube.pur) > (cube.pll | cube.pul) + (cube.plr | cube.pur));
                }
                if (splitu)
                {
                    double um = (uvPatch.Left + uvPatch.Right) / 2.0;
                    BoundingRect left, right;
                    left = uvPatch;
                    left.Right = um;
                    right = uvPatch;
                    right.Left = um;
                    AddCube(left);
                    AddCube(right);
                }
                else
                {
                    double vm = (uvPatch.Bottom + uvPatch.Top) / 2.0;
                    BoundingRect bottom, top;
                    bottom = uvPatch;
                    bottom.Top = vm;
                    top = uvPatch;
                    top.Bottom = vm;
                    AddCube(bottom);
                    AddCube(top);
                }
                //GeoPoint2D cnt = uvPatch.GetCenter();
                //BoundingRect ll, lr, ul, ur;
                //ll = uvPatch;
                //ll.Right = cnt.x;
                //ll.Top = cnt.y;
                //lr = uvPatch;
                //lr.Left = cnt.x;
                //lr.Top = cnt.y;
                //ul = uvPatch;
                //ul.Right = cnt.x;
                //ul.Bottom = cnt.y;
                //ur = uvPatch;
                //ur.Left = cnt.x;
                //ur.Bottom = cnt.y;
                //AddCube(ll);
                //AddCube(lr);
                //AddCube(ul);
                //AddCube(ur);
            }
            else
            {
                cube.boundingCube = surface.GetPatchExtent(uvPatch);
                octtree.AddObject(cube);
            }
        }
        /// <summary>
        /// Stellt fest, ob die Fläche von dem BoundingCube getroffen wird und wenn ja liefert es einen inneren Flächenpunkt
        /// zurück. Der OctTree wird bei deiser Gelegenheit u.U. verfeinert
        /// </summary>
        /// <param name="test"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public bool HitTest(BoundingCube test, out GeoPoint2D uv)
        {
            Cube[] hits = octtree.GetObjectsFromBox(test);
            List<Cube> untested = new List<Cube>();
            for (int i = 0; i < hits.Length; ++i)
            {
                if (test.Interferes(hits[i].boundingCube))
                {
                    uv = hits[i].uvPatch.GetCenter();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    uv = hits[i].uvPatch.GetLowerLeft();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    uv = hits[i].uvPatch.GetLowerRight();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    uv = hits[i].uvPatch.GetUpperLeft();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    uv = hits[i].uvPatch.GetUpperRight();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    if (SplitHit(hits[i], test, out uv, untested)) return true;
                }
            }
            while (untested.Count > 0)
            {
                List<Cube> stillToTest = new List<Cube>();
                foreach (Cube cb in untested)
                {
                    if (SplitHit(cb, test, out uv, stillToTest)) return true;
                }
                untested = stillToTest;
            }
            uv = GeoPoint2D.Origin;
            return false;
        }
        private bool SplitHit(Cube toSplit, BoundingCube test, out GeoPoint2D uv, List<Cube> unknown)
        {
            // Teile toSplit auf bis entweder ein Treffer mit test gefunden ist
            // oder keine Überschneidung mehr da ist
            octtree.RemoveObject(toSplit);
            Cube[] subCubes = new Cube[4];
            subCubes[0] = new Cube();
            subCubes[1] = new Cube();
            subCubes[2] = new Cube();
            subCubes[3] = new Cube();
            double hcenter = (toSplit.uvPatch.Left + toSplit.uvPatch.Right) / 2.0;
            double vcenter = (toSplit.uvPatch.Bottom + toSplit.uvPatch.Top) / 2.0;
            subCubes[0].uvPatch = new BoundingRect(toSplit.uvPatch.Left, toSplit.uvPatch.Bottom, hcenter, vcenter);
            subCubes[1].uvPatch = new BoundingRect(toSplit.uvPatch.Left, vcenter, hcenter, toSplit.uvPatch.Top);
            subCubes[2].uvPatch = new BoundingRect(hcenter, toSplit.uvPatch.Bottom, toSplit.uvPatch.Right, vcenter);
            subCubes[3].uvPatch = new BoundingRect(hcenter, vcenter, toSplit.uvPatch.Right, toSplit.uvPatch.Top);
            for (int i = 0; i < 4; ++i)
            {
                subCubes[i].boundingCube = surface.GetPatchExtent(new BoundingRect(subCubes[i].uvPatch.Left, subCubes[i].uvPatch.Bottom, subCubes[i].uvPatch.Right, subCubes[i].uvPatch.Top));
                octtree.AddObject(subCubes[i]);
            }
            for (int i = 0; i < 4; ++i)
            {
                if (test.Interferes(subCubes[i].boundingCube) && subCubes[i].boundingCube.Size > Precision.eps * 100)
                {
                    uv = subCubes[i].uvPatch.GetCenter();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    uv = subCubes[i].uvPatch.GetLowerLeft();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    uv = subCubes[i].uvPatch.GetLowerRight();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    uv = subCubes[i].uvPatch.GetUpperLeft();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    uv = subCubes[i].uvPatch.GetUpperRight();
                    if (test.Contains(surface.PointAt(uv))) return true;
                    unknown.Add(subCubes[i]);
                }
            }
            uv = GeoPoint2D.Origin;
            return false;
        }
        public GeoPoint2D PositionOf(GeoPoint p3d)
        {
            Cube[] cubes = octtree.GetObjectsFromPoint(p3d);
            if (cubes.Length == 0)
            {
                double size = octtree.Extend.Size / 10000;
                while (cubes.Length == 0)
                {
                    cubes = octtree.GetObjectsFromBox(new BoundingCube(p3d, size));
                    size *= 2.0;
                }
            }
            // hier kennen wir also einige cubes, die von Octtree gefunden wurden. Wir suchen den Besten
            double mindist = double.MaxValue;
            Cube found = null;
            for (int i = 0; i < cubes.Length; i++)
            {
                double d = cubes[i].boundingCube.GetCenter() | p3d;
                if (d < mindist)
                {
                    found = cubes[i];
                    mindist = d;
                }
            }
            try
            {   // es muss einen besten Würfel geben
                return PositionOf(p3d, found);
            }
            catch (DidntConverge)
            {
                try
                {
                    for (int i = 0; i < cubes.Length; i++)
                        if (found != cubes[i])
                        {
                            return PositionOf(p3d, cubes[i]);
                        }
                }
                catch (DidntConverge) { }
                SplitCubes(cubes);
            }
            throw new DidntConverge(); // sollte nicht vorkommen, der Punkt ist außerhalb der Fläche
        }
        public GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            Cube[] cubes = octtree.GetObjectsFromLine(startPoint, direction, 0.0);
            for (int i = 0; i < cubes.Length; i++)
            {
                if (cubes[i].boundingCube.Interferes(startPoint, direction, 0.0, false))
                {   // der OctTree liefert halt auch cubes, die nicht die Linie berühren
                    res.AddRange(GetLineIntersection(startPoint, direction, cubes[i]));
                }
            }
            return res.ToArray();
        }
        private IEnumerable<GeoPoint2D> GetLineIntersection(GeoPoint startPoint, GeoVector direction, Cube cube)
        {
            // Um die Performance zu steigern könnte NewtonLineIntersection auch mahrere Schnittpunkte liefern, die auch außerhalb
            // des Patches liegen und die entsprechenden Patches würden dann nicht mehr verwendet. Ist aber schwierig.
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            GeoPoint2D uvSurface;
            if (NewtonLineIntersection(cube.uvPatch, startPoint, direction, out uvSurface))
            {
                res.Add(uvSurface);
                // Es kann sein, dass es in diesem Patch noch einen zweiten Schnittpunkt gibt (mehr als zwei sollte
                // es nicht geben, sonst ist die Aufteilung des BoxedSurface schlecht.
                // das Kriterium, ob es noch einen Schnittpunkt geben könnte ist schwierig. Hier wird gechecked, ob
                // die Normalenvektoren in den Ecken und an dem Schnittpunkt die gleiche Richtung haben
                // wie die Gerade. Wenn nicht könnte noch ein Schnitt drin sein. Das ist aber nicht notwendig. 
                // Vermutlich besser aber aufwendiger wäre es zu prüfen, ob es ein Maximum oder Minimum in der 
                // Richtung der Geraden gibt.
                int sgn = Math.Sign(surface.GetNormal(cube.uvPatch.GetLowerLeft()) * direction);
                bool split = false;
                split = split || Math.Sign(surface.GetNormal(cube.uvPatch.GetLowerRight()) * direction) != sgn;
                split = split || Math.Sign(surface.GetNormal(cube.uvPatch.GetUpperLeft()) * direction) != sgn;
                split = split || Math.Sign(surface.GetNormal(cube.uvPatch.GetUpperRight()) * direction) != sgn;
                split = split || Math.Sign(surface.GetNormal(uvSurface) * direction) != sgn;
                //double dbg1 = surface.GetNormal(cube.uvPatch.GetLowerLeft()) * direction;
                //double dbg2 = surface.GetNormal(cube.uvPatch.GetLowerRight()) * direction;
                //double dbg3 = surface.GetNormal(cube.uvPatch.GetUpperLeft()) * direction;
                //double dbg4 = surface.GetNormal(cube.uvPatch.GetUpperRight()) * direction;
                //double dbg5 = surface.GetNormal(uvSurface) * direction;
                if (split)
                {
#if DEBUG
                    {
                        DebuggerContainer dc = new DebuggerContainer();
                        Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(cube.uvPatch.ToBorder()));
                        dc.Add(fc);
                        Line l3d = Line.MakeLine(startPoint, startPoint + direction);
                        dc.Add(l3d);
                        dc.Add(cube.boundingCube.GetSolid());
                    }
#endif
                    // wir betrachten hier den uvPatch und teilen ihn so in 4 Stücke, dass der gefundene Schnittpunkt
                    // nicht dabei ist. Gehen aber nicht rekursiv sondern machen das nur einmal
                    double eps = cube.uvPatch.Size / 1000; // größe des quadratischen Loches im Patch
                    BoundingRect[] subpatches = new BoundingRect[]{
                        new BoundingRect(uvSurface.x + eps, uvSurface.y - eps, cube.uvPatch.Right, cube.uvPatch.Top),
                        new BoundingRect(cube.uvPatch.Left, uvSurface.y + eps, uvSurface.x + eps, cube.uvPatch.Top),
                        new BoundingRect(cube.uvPatch.Left, cube.uvPatch.Bottom, uvSurface.x - eps, uvSurface.y + eps),
                        new BoundingRect(uvSurface.x - eps, cube.uvPatch.Bottom, cube.uvPatch.Right, uvSurface.y - eps)};
                    for (int i = 0; i < subpatches.Length; ++i)
                    {
                        if (subpatches[i].Left < subpatches[i].Right && subpatches[i].Bottom < subpatches[i].Top)
                        {   // kann bei Schnitten am Rand vorkommen
                            if (NewtonLineIntersection(subpatches[i], startPoint, direction, out uvSurface))
                            {
                                res.Add(uvSurface);
                            }
                        }
                    }
                }
            }
            return res;
        }

        private bool NewtonLineIntersection(BoundingRect boundingRect, GeoPoint startPoint, GeoVector direction, out GeoPoint2D uvSurface)
        {   // sollte es einen Schnittpunkt in diesem Patch geben, so muss der auch gefunden werden.
            // Es ist nicht sicher, ob von den 4 Eckpunkten und dem Mittelpunkt ausgehend ein Schnittpunkt mit dem Newtonverfahren
            // sicher gefunden wird. Kann ja auch sein, dass es einen Schnittpunkt gibt, aber alle Anfangsbedingungen konvergieren
            // zu einem anderen Schnittpunkt. Das Beispiel müsste noch gefunden werden und als Kriterium für die Aufteilung
            // in Würfel verwendet werden.
            double umin, umax, vmin, vmax;
            surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            BoundingRect maximumuvRect = new BoundingRect(umin, vmin, umax, vmax); // damit es nicht rausläuft, besser wäre auf die grenze klippen, oder?
            for (int c = 0; c < 5; ++c)
            {
                // mit verschiedenen Startpunkten versuchen. Wenn von keinem Startpunkt aus eine Lösung zu finden ist, dann
                // aufgeben
                switch (c)
                {
                    default:
                    case 0:
                        uvSurface = boundingRect.GetCenter();
                        break;
                    case 1:
                        uvSurface = boundingRect.GetLowerLeft();
                        break;
                    case 2:
                        uvSurface = boundingRect.GetUpperRight();
                        break;
                    case 3:
                        uvSurface = boundingRect.GetUpperLeft();
                        break;
                    case 4:
                        uvSurface = boundingRect.GetLowerRight();
                        break;
                }
                GeoVector udir = surface.UDirection(uvSurface);
                GeoVector vdir = surface.VDirection(uvSurface); // die müssen auch von der Länge her stimmen!
                GeoPoint loc = surface.PointAt(uvSurface);
                double error = Geometry.DistPL(loc, startPoint, direction);
                int errorcount = 0;
                int outside = 0; // wenn ein Schnitt außerhalb ist, dann nicht gleich aufgeben, erst wenn zweimal hintereinander außerhalb
                                 // damit am Rand oszillierende Punkte gefunden werden können
#if DEBUG
                {
                    DebuggerContainer dc = new DebuggerContainer();
                    try
                    {
                        Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(boundingRect));
                        dc.Add(fc);
                    }
                    catch (Polyline2DException) { }
                    Plane pln = new Plane(loc, udir, vdir);
                    Face plnfc = Face.MakeFace(new PlaneSurface(pln), new CADability.Shapes.SimpleShape(CADability.Shapes.Border.MakeRectangle(-1, 1, -1, 1)));
                    dc.Add(plnfc);
                    Line ln = Line.Construct();
                    ln.SetTwoPoints(startPoint - direction, startPoint + direction);
                    dc.Add(ln);
                }
#endif

                while (true) // entweder kommt break oder return
                {
                    Matrix m = DenseMatrix.OfColumnArrays(udir, vdir, direction);
                    Vector s = (Vector)m.Solve(new DenseVector(startPoint - loc));
                    if (s.IsValid())
                    {
                        double du = s[0];
                        double dv = s[1];
                        double l = s[2];
                        uvSurface.x += du; // oder -=
                        uvSurface.y += dv; // oder -=
                        loc = surface.PointAt(uvSurface);
                        udir = surface.UDirection(uvSurface);
                        vdir = surface.VDirection(uvSurface); // die müssen auch von der Länge her stimmen!
                        double e = Geometry.DistPL(loc, startPoint, direction);
#if DEBUG
                        DebuggerContainer dc = new DebuggerContainer();
                        try
                        {
                            Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(boundingRect));
                            dc.Add(fc);
                        }
                        catch (Polyline2DException) { }
                        try
                        {
                            Plane pln = new Plane(loc, udir, vdir);
                            Face plnfc = Face.MakeFace(new PlaneSurface(pln), new CADability.Shapes.SimpleShape(CADability.Shapes.Border.MakeRectangle(-1, 1, -1, 1)));
                            dc.Add(plnfc);
                        }
                        catch (PlaneException) { }
                        Line ln = Line.Construct();
                        ln.SetTwoPoints(startPoint - direction, startPoint + direction);
                        dc.Add(ln);
#endif
                        if (!boundingRect.Contains(uvSurface))
                        {
                            if (!maximumuvRect.Contains(uvSurface)) break; // denn damit kann man nicht weiterrechnen
                                                                           // man könnte höchsten auf den maximumuvRect Bereich klippen
                            ++outside;
                            if (outside > 2) break; // läuft aus dem Patch raus. Mit anderem Startwert versuchen
                        }
                        else
                        {
                            outside = 0; // innerhalb, zurücksetzen
                        }
                        if (e >= error)
                        {
                            ++errorcount;
                            // manchmal machts einen kleinen Schlenker bevor es konvergiert, dann nicht gleich abbrechen
                            if (errorcount > 5) break; // konvergiert nicht, mit diesem Startwert abbrechen
                        }
                        else if (e < Math.Max(Precision.eps, loc.Size * 1e-6))
                        {
                            return true; // gefunden!
                        }
                        error = e;
                    }
                    else
                    {   // singuläre matrix
                        break;
                    }
                }
            }
            uvSurface = boundingRect.GetCenter(); // muss halt einen Wert haben
            return false; // keine Lösung gefunden
        }
        public ICurve[] GetSelfIntersection()
        {
            // Die Idee: zwei Cubes überlappen sich. Betrachte die u und v Richtungen an den Ecken. 
            // Wenn zwei davon in entgegengesetzte Richtungen gehen (>90°), dann besteht die Möglichkeit der
            // Selbstüberschneideung. Dann noch weiter aufteilen. Letztlich feste u oder v Kurvenabschnitte
            // mit der Fläche schneiden.
            return null;
        }
        class ComputeIntersectionCurve
        {
            class UVPatch : IQuadTreeInsertable
            {   // auf jedem Patch befinden sich maximal zwei IntersectionPoints.
                // im Zweifelsfall bei den Eckpunkten werden solche Patches ignoriert, die nur einen Punkt haben
                public BoundingRect extent;
                public IntersectionPoint point1;
                public IntersectionPoint point2;
                public void Add(IntersectionPoint intersectionPoint)
                {
                    if (point1 == null) point1 = intersectionPoint;
                    else point2 = intersectionPoint;
                }
                internal void Remove(IntersectionPoint toRemove)
                {
                    if (point1 == toRemove)
                    {
                        point1 = point2;
                        point2 = null;
                    }
                    else if (point2 == toRemove)
                    {
                        point2 = null;
                    }
                }

                #region IQuadTreeInsertable Members

                BoundingRect IQuadTreeInsertable.GetExtent()
                {
                    return extent;
                }

                bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
                {
                    return extent.Interferes(ref rect);
                }

                object IQuadTreeInsertable.ReferencedObject
                {
                    get { throw new Exception("The method or operation is not implemented."); }
                }

                #endregion
            }
            class IntersectionPoint
            {   // Ein Punkt ist immer der Schnittpunkt einer Masche, also einer Begrenzung eines uvPatches
                // Wenn Punkte genau auf Ecken liegen, dann sind auch onPatch3 und onPatch4 besetzt
                public GeoPoint p;
                public GeoPoint2D pSurface1; // das ist immer die BoxedSurface
                public GeoPoint2D pSurface2; // das ist die andere Fläche
                public bool fixedu; // Schnittpunkt einer Kante mit festem u, wenn false mit festem v
                public bool isOnPatchVertex; // liegt genau auf einer Ecke des Patches
                                             // der Punkt liegt auf Kanten dieser beiden Patches (kann auch nur einer sein, Rand, wie isses mit Ecken?)
                public UVPatch onPatch1;
                public UVPatch onPatch2;
                int hashCode;
                static int hashCodeCounter = 0;
                public IntersectionPoint()
                {
                    hashCode = ++hashCodeCounter;
                }

                public void AddPatch(UVPatch uvPatch)
                {
                    if (isOnPatchVertex)
                    {   // es handelt sich um einen Eckpunkt eines Patches, dann gelten immer nur die oberen oder rechten
                        if (fixedu)
                        {   // der Punkt muss Unterkante des Patches sein, denn hier sollen nur die oberen gelten
                            if (uvPatch.extent.Top == pSurface1.y) return;
                        }
                        else
                        {
                            if (uvPatch.extent.Right == pSurface1.x) return;
                        }
                    }
                    if (onPatch1 == null) onPatch1 = uvPatch;
                    else if (onPatch2 == null) onPatch2 = uvPatch;
                    else
                    {   // darf nicht vorkommen, für Breakpoint
                    }
                    uvPatch.Add(this);
                }
                public override int GetHashCode()
                {
                    return hashCode;
                }
                public override bool Equals(object obj)
                {
                    return (obj as IntersectionPoint).hashCode == hashCode;
                }
                internal void RemovePatch(UVPatch uVPatch)
                {
                    if (onPatch1 == uVPatch)
                    {
                        onPatch1 = onPatch2;
                        onPatch2 = null;
                    }
                    else if (onPatch2 == uVPatch)
                    {
                        onPatch2 = null;
                    }
                }
            }
            BoxedSurface boxedSurface;
            ISurfaceImpl toIntersectWith;
            BoundingRect uvSize;
            Dictionary<double, List<IntersectionPoint>> uIntersections; // Schnittpunkte zu festem u bereits bestimmt
            Dictionary<double, List<IntersectionPoint>> vIntersections;
            QuadTree<UVPatch> patches;
            Set<IntersectionPoint> intersectionPoints; // Schnittpunkte bislang gefunden
            List<IntersectionPoint> onPatchVertex; // verworfene Schnittpunkte, da sie doppelt vorkommen und genau auf
                                                   // dem Eck eines Patches liegen. Möglicherweise müssen Kurven daran wieder zusammengefügt werden
            public ComputeIntersectionCurve(BoxedSurface boxedSurface, ISurfaceImpl toIntersectWith, double umin, double umax, double vmin, double vmax)
            {
                this.boxedSurface = boxedSurface;
                this.toIntersectWith = toIntersectWith;
                uIntersections = new Dictionary<double, List<IntersectionPoint>>();
                vIntersections = new Dictionary<double, List<IntersectionPoint>>();
                uvSize = new BoundingRect(umin, vmin, umax, vmax);
                patches = new QuadTree<UVPatch>(uvSize);
                intersectionPoints = new Set<IntersectionPoint>();
                onPatchVertex = new List<IntersectionPoint>();
            }
            List<IntersectionPoint> FixedParameterIntersections(double uv, bool uParameter)
            {   // das soll ALLE Schnittpunkte für einen festen u bzw. v wert liefern. 
                List<IntersectionPoint> res = new List<IntersectionPoint>();
                if (uParameter)
                {
                    if (!uIntersections.TryGetValue(uv, out res))
                    {
                        ICurve fu = boxedSurface.surface.FixedU(uv, boxedSurface.uvbounds.Bottom, boxedSurface.uvbounds.Top);

                        GeoPoint[] ips;
                        GeoPoint2D[] uvOnSurface;
                        double[] uOnCurve;
                        toIntersectWith.Intersect(fu, uvSize, out ips, out uvOnSurface, out uOnCurve);
                        NewtonMend(toIntersectWith, fu, ref ips, ref uvOnSurface, ref uOnCurve);
                        res = new List<IntersectionPoint>();
                        for (int i = 0; i < ips.Length; ++i)
                        {
                            IntersectionPoint ip = new IntersectionPoint();
                            ip.p = ips[i];
                            double vOnSurface1 = boxedSurface.uvbounds.Bottom + uOnCurve[i] * boxedSurface.uvbounds.Height; // s.u.
                            ip.pSurface1 = new GeoPoint2D(uv, vOnSurface1);
                            // ip.pSurface1 = boxedSurface.surface.PositionOf(ips[i]);
                            ip.pSurface2 = uvOnSurface[i];
                            ip.fixedu = true;
                            res.Add(ip);
                            // fehlen noch die Patches
                        }
                        uIntersections[uv] = res;
                    }
                }
                else
                {
                    if (!vIntersections.TryGetValue(uv, out res))
                    {
                        ICurve fv = boxedSurface.surface.FixedV(uv, boxedSurface.uvbounds.Left, boxedSurface.uvbounds.Right);
                        GeoPoint[] ips;
                        GeoPoint2D[] uvOnSurface;
                        double[] uOnCurve;
                        toIntersectWith.Intersect(fv, uvSize, out ips, out uvOnSurface, out uOnCurve);
                        NewtonMend(toIntersectWith, fv, ref ips, ref uvOnSurface, ref uOnCurve);
                        res = new List<IntersectionPoint>();
                        for (int i = 0; i < ips.Length; ++i)
                        {
                            IntersectionPoint ip = new IntersectionPoint();
                            ip.p = ips[i];
                            // wir müssen auf den "natürlichen" Parameter von u kommen
                            // uOnCurve ist im 0..1 System der Kurve. hoffentlich geht das linear
                            double uOnSurface1 = boxedSurface.uvbounds.Left + uOnCurve[i] * boxedSurface.uvbounds.Width;
                            ip.pSurface1 = new GeoPoint2D(uOnSurface1, uv);
                            ip.pSurface1 = boxedSurface.surface.PositionOf(ips[i]);
                            ip.pSurface2 = uvOnSurface[i];
                            ip.fixedu = false;
                            res.Add(ip);
                            // fehlen noch die Patches
                        }
                        vIntersections[uv] = res;
                    }
                }
                return res;
            }

            private void NewtonMend(ISurface surface, ICurve curve, ref GeoPoint[] ips, ref GeoPoint2D[] uvOnSurface, ref double[] uOnCurve)
            {   // Mend heißt reparieren, nachbessern. Das Abbruchkriterium beim Schnittpunktfinden ist, wenn der Punkt auf der Kurve
                // und der Punkt auf der Fläche weniger als eps Abstand haben. Das ist aber nicht gut, denn bei flachen Schnitten
                // kann da immer noch zu weit vom echten Schnittpunkt entfernt sein. Deshalb hier Nachbessern mit Newton und
                // Abbruch unter Einbeziehung des Winkels.
                for (int i = 0; i < ips.Length; ++i)
                {
                    GeoVector udir = surface.UDirection(uvOnSurface[i]);
                    GeoVector vdir = surface.VDirection(uvOnSurface[i]);
                    GeoVector normal = udir ^ vdir;
                    GeoVector direction = curve.DirectionAt(uOnCurve[i]);
                    double eps = Precision.eps * Math.Abs(GeoVector.Cos(normal, direction));
                    GeoPoint pOnSurface = surface.PointAt(uvOnSurface[i]);
                    GeoPoint pOnCurve = curve.PointAt(uOnCurve[i]);
                    double error = pOnCurve | pOnSurface;
                    while (error > eps)
                    {
                        Matrix m = DenseMatrix.OfColumnArrays(udir, vdir, direction);
                        Vector s = (Vector)m.Solve(new DenseVector(pOnCurve - pOnSurface));
                        if (s.IsValid())
                        {
                            double du = s[0];
                            double dv = s[1];
                            double l = s[2];
                            uvOnSurface[i].x += du;
                            uvOnSurface[i].y += dv;
                            uOnCurve[i] -= l;
                            pOnSurface = surface.PointAt(uvOnSurface[i]);
                            pOnCurve = curve.PointAt(uOnCurve[i]);
                            udir = surface.UDirection(uvOnSurface[i]);
                            vdir = surface.VDirection(uvOnSurface[i]);
                            normal = udir ^ vdir;
                            direction = curve.DirectionAt(uOnCurve[i]);
                            eps = Precision.eps * Math.Abs(GeoVector.Cos(normal, direction));
                            double e = pOnCurve | pOnSurface;
                            if (e >= error) return; // konvergiert nicht, sollte beim Nachbessern nicht vorkommen
                            error = e;
                        }
                        else // singuläre Matrix
                        {
                            return;
                        }
                    }
                    ips[i] = new GeoPoint(pOnSurface, pOnCurve);
                }
            }
            void CheckIntersectinPoints(Cube cube)
            {
                List<IntersectionPoint> left = FixedParameterIntersections(cube.uvPatch.Left, true);
                List<IntersectionPoint> right = FixedParameterIntersections(cube.uvPatch.Right, true);
                List<IntersectionPoint> bottom = FixedParameterIntersections(cube.uvPatch.Bottom, false);
                List<IntersectionPoint> top = FixedParameterIntersections(cube.uvPatch.Top, false);
                List<IntersectionPoint> inPatch = new List<IntersectionPoint>();
                UVPatch uvPatch = new UVPatch();
                uvPatch.extent = cube.uvPatch;
                // erstmal alle suchen, die auch in dem Patch liegen, FixedParameterIntersections liefert nämlich alle
                for (int i = 0; i < left.Count; ++i)
                {   // zuerst einrastern auf Eckpunkte, so es ein Eckpunkt ist
                    if (Math.Abs(left[i].pSurface1.y - cube.uvPatch.Bottom) < boxedSurface.uvbounds.Height * 1e-6)
                    {
                        left[i].pSurface1.y = cube.uvPatch.Bottom;
                        left[i].isOnPatchVertex = true;
                    }
                    if (Math.Abs(left[i].pSurface1.y - cube.uvPatch.Top) < boxedSurface.uvbounds.Height * 1e-6)
                    {
                        left[i].pSurface1.y = cube.uvPatch.Top;
                        left[i].isOnPatchVertex = true;
                    }
                    if (left[i].pSurface1.y >= cube.uvPatch.Bottom && left[i].pSurface1.y <= cube.uvPatch.Top)
                    //if (left[i].pSurface1.y >= cube.uvPatch.Bottom - boxedSurface.uvbounds.Height * 1e-6 && left[i].pSurface1.y <= cube.uvPatch.Top + boxedSurface.uvbounds.Height * 1e-6)
                    {
                        inPatch.Add(left[i]);
                    }
                }
                for (int i = 0; i < right.Count; ++i)
                {
                    if (Math.Abs(right[i].pSurface1.y - cube.uvPatch.Bottom) < boxedSurface.uvbounds.Height * 1e-6)
                    {
                        right[i].pSurface1.y = cube.uvPatch.Bottom;
                        right[i].isOnPatchVertex = true;
                    }
                    if (Math.Abs(right[i].pSurface1.y - cube.uvPatch.Top) < boxedSurface.uvbounds.Height * 1e-6)
                    {
                        right[i].pSurface1.y = cube.uvPatch.Top;
                        right[i].isOnPatchVertex = true;
                    }
                    if (right[i].pSurface1.y >= cube.uvPatch.Bottom && right[i].pSurface1.y <= cube.uvPatch.Top)
                    //if (right[i].pSurface1.y >= cube.uvPatch.Bottom - boxedSurface.uvbounds.Height * 1e-6 && right[i].pSurface1.y <= cube.uvPatch.Top + boxedSurface.uvbounds.Height * 1e-6)
                    {
                        inPatch.Add(right[i]);
                    }
                }
                for (int i = 0; i < bottom.Count; ++i)
                {
                    if (Math.Abs(bottom[i].pSurface1.x - cube.uvPatch.Left) < boxedSurface.uvbounds.Width * 1e-6)
                    {
                        bottom[i].pSurface1.x = cube.uvPatch.Left;
                        bottom[i].isOnPatchVertex = true;
                    }
                    if (Math.Abs(bottom[i].pSurface1.x - cube.uvPatch.Right) < boxedSurface.uvbounds.Width * 1e-6)
                    {
                        bottom[i].pSurface1.x = cube.uvPatch.Right;
                        bottom[i].isOnPatchVertex = true;
                    }
                    if (bottom[i].pSurface1.x >= cube.uvPatch.Left && bottom[i].pSurface1.x <= cube.uvPatch.Right)
                    //if (bottom[i].pSurface1.x >= cube.uvPatch.Left - boxedSurface.uvbounds.Width * 1e-6 && bottom[i].pSurface1.x <= cube.uvPatch.Right + boxedSurface.uvbounds.Width * 1e-6)
                    {
                        inPatch.Add(bottom[i]);
                    }
                }
                for (int i = 0; i < top.Count; ++i)
                {
                    if (Math.Abs(top[i].pSurface1.x - cube.uvPatch.Left) < boxedSurface.uvbounds.Width * 1e-6)
                    {
                        top[i].pSurface1.x = cube.uvPatch.Left;
                        top[i].isOnPatchVertex = true;
                    }
                    if (Math.Abs(top[i].pSurface1.x - cube.uvPatch.Right) < boxedSurface.uvbounds.Width * 1e-6)
                    {
                        top[i].pSurface1.x = cube.uvPatch.Right;
                        top[i].isOnPatchVertex = true;
                    }
                    if (top[i].pSurface1.x >= cube.uvPatch.Left && top[i].pSurface1.x <= cube.uvPatch.Right)
                    //if (top[i].pSurface1.x >= cube.uvPatch.Left - boxedSurface.uvbounds.Width * 1e-6 && top[i].pSurface1.x <= cube.uvPatch.Right + boxedSurface.uvbounds.Width * 1e-6)
                    {
                        inPatch.Add(top[i]);
                    }
                }
                // liegt ein Schnittpunkt genau auf dem Eck eunes uvPatches, dann kommt er oft doppelt vor
                // für jede Seite einmal. Einer wird hier verworfen. Das kann zur Folge haben, dass Kurven
                // unterbrochen sind. Mit Hilve von onPatchVertex könnte man sie wieder zusammensetzen
                bool removed;
                do
                {
                    removed = false;
                    for (int i = 0; i < inPatch.Count - 1; ++i)
                    {
                        for (int j = i + 1; j < inPatch.Count; ++j)
                        {
                            if ((inPatch[i].p | inPatch[j].p) < 2 * Precision.eps ||
                                (inPatch[i].pSurface1 | inPatch[j].pSurface1) < cube.uvPatch.Size * 1e-6)
                            {   // jeder Punkt hat den Fehler Precision.eps, deshalb 2*
                                onPatchVertex.Add(inPatch[j]);
                                inPatch.RemoveAt(j);
                                removed = true;
                                break; // es gibt ja immer nur Paare und j>i, also problemlos abbrechen
                                       // leider gibt es bei Tangenten oft vielfache fast identische Punkte
                                       // das müsste man besser dort lösen, dann würde diese Schleife einfach reichen
                            }
                        }
                    }
                } while (removed);
                if (inPatch.Count < 2)
                {
                    return;
                }
                else if (inPatch.Count == 2)
                {
                    for (int i = 0; i < inPatch.Count; ++i)
                    {
                        inPatch[i].AddPatch(uvPatch);
                        intersectionPoints.Add(inPatch[i]);
                    }
                }
                else
                {
                    // hier könnten welche auf den Ecken sitzen, das muss zuerst gechecked werden
                    // allgemein: Mehrfachschnitte, also aufteilen
#if DEBUG
                    DebuggerContainer dc = new DebuggerContainer();
                    try
                    {
                        Face fcdbg = Face.MakeFace(boxedSurface.surface, new CADability.Shapes.SimpleShape(uvPatch.extent));
                        dc.Add(fcdbg);
                    }
                    catch (Polyline2DException) { }
                    BoundingRect uvplane = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < inPatch.Count; ++i)
                    {
                        uvplane.MinMax(inPatch[i].pSurface2);
                        Point pnt = Point.Construct();
                        pnt.Location = inPatch[i].p;
                        pnt.Symbol = PointSymbol.Cross;
                        pnt.ColorDef = new CADability.Attribute.ColorDef("xxx", Color.Black);
                        dc.Add(pnt);
                    }
                    uvplane.Inflate(1.0, 1.0);
                    Face fcpln = Face.MakeFace(this.toIntersectWith, new CADability.Shapes.SimpleShape(uvplane));
                    dc.Add(fcpln);
#endif
                    Cube[] cubes = boxedSurface.SplitCube(cube); // wird echt, also auch im octtree aufgeteilt
                    for (int i = 0; i < cubes.Length; ++i)
                    {
                        CheckIntersectinPoints(cubes[i]);
                    }
                }
            }
            public IDualSurfaceCurve[] GetIntersectionCurves()
            {
                // Hängt bei parallelen Flächen, wird total aufgeteilt wenn die Flächen identisch sind.
                Cube[] cubes = boxedSurface.octtree.GetObjectsCloseTo(toIntersectWith);
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                for (int i = 0; i < cubes.Length; ++i)
                {
                    dc.Add(cubes[i].boundingCube.AsBox);
                }
#endif
                for (int i = 0; i < cubes.Length; ++i)
                {
                    CheckIntersectinPoints(cubes[i]);
                }
                IntersectionPoint startwith = FindSinglePatchIntersectionPoint();
                List<List<IntersectionPoint>> curves = new List<List<IntersectionPoint>>();
                while (startwith != null)
                {   // offene Kurven, Eckpunkte im uvPatch sind noch nicht berücksichtigt...
                    List<IntersectionPoint> curve = new List<IntersectionPoint>();
                    IntersectionPoint goOnWith = startwith;
                    while (goOnWith != null)
                    {   // immer mit onPatch1 weitergehen, da der Punkt bei seiner Verwendung den Ausgangspatch verliert
                        curve.Add(goOnWith);
                        IntersectionPoint toRemove = goOnWith;
                        if (goOnWith.onPatch1 == null)
                        {
                            goOnWith = null;
                        }
                        else
                        {
                            if (goOnWith.onPatch1.point1 == goOnWith)
                            {
                                goOnWith = goOnWith.onPatch1.point2;
                            }
                            else
                            {
                                goOnWith = goOnWith.onPatch1.point1;
                            }
                        }
                        if (goOnWith != null) goOnWith.RemovePatch(toRemove.onPatch1);
                        RemoveIntersectionPoint(toRemove); // erst hier entfernen, da er auch aus den Patches entfernt wird
                    }
                    if (curve.Count > 1) curves.Add(curve);
                    startwith = FindSinglePatchIntersectionPoint();
                }
                if (intersectionPoints.Count > 0)
                {
                    startwith = intersectionPoints.GetAny(); // irgend ein Anfang, jetzt gibts geschlossene
                }
                while (startwith != null)
                {   // geschlossene Kurven
                    List<IntersectionPoint> curve = new List<IntersectionPoint>();
                    IntersectionPoint goOnWith = startwith;
                    while (goOnWith != null)
                    {
                        if (curve.Count == 0 || ((curve[curve.Count - 1].p | goOnWith.p) > Precision.eps))
                        {   // nicht den gleichen Punkt zweimal
                            curve.Add(goOnWith);
                        }
                        intersectionPoints.Remove(goOnWith);
                        if (goOnWith.onPatch1.point1 == goOnWith)
                        {
                            goOnWith = goOnWith.onPatch1.point2;
                        }
                        else
                        {
                            goOnWith = goOnWith.onPatch1.point1;
                        }
                        if (!intersectionPoints.Contains(goOnWith)) break;
                        if (goOnWith == startwith)
                        {
                            if (curve.Count == 0 || ((curve[curve.Count - 1].p | goOnWith.p) > Precision.eps))
                            {
                                curve.Add(goOnWith); // damit es geschlossen wird
                            }
                            break;
                        }
                    }
                    if (curve.Count > 1) curves.Add(curve);
                    if (intersectionPoints.Count > 0)
                    {
                        startwith = intersectionPoints.GetAny(); // irgend ein Anfang, jetzt gibts geschlossene
                    }
                    else
                    {
                        startwith = null;
                    }
                }
                IDualSurfaceCurve[] res = new IDualSurfaceCurve[curves.Count];
                for (int i = 0; i < curves.Count; ++i)
                {
                    res[i] = MakeDualSurfaceCurve(curves[i]);
                }
                return res;
            }

            private void RemoveIntersectionPoint(IntersectionPoint toRemove)
            {
                if (toRemove.onPatch1 != null) toRemove.onPatch1.Remove(toRemove);
                if (toRemove.onPatch2 != null) toRemove.onPatch2.Remove(toRemove);
                intersectionPoints.Remove(toRemove);
            }

            private IDualSurfaceCurve MakeDualSurfaceCurve(List<IntersectionPoint> list)
            {   // es können sehr viele Punkte entstanden sein auf der Kurve, wenn die Fläche in viele 
                // Stücke unterteilt werden musste. zu viele Punkte stören bei der weiteren Verwendung der Kurve
                // z.B. hohe Dreieckszahlen u.s.w. deshalb wird hier reduziert, es bleiben mindestens 5, oder?
#if DEBUG
                GeoPoint[] pl = new GeoPoint[list.Count];
                for (int i = 0; i < pl.Length; ++i)
                {
                    pl[i] = list[i].p;
                }
                Polyline pol = Polyline.Construct();
                pol.SetPoints(pl, false);
#endif
                while (list.Count > 10)
                {
                    bool removed = false;
                    for (int i = list.Count - 2; i > 0; --i)
                    {
                        if (Geometry.DistPL(list[i].p, list[i + 1].p, list[i - 1].p) < (list[i + 1].p | list[i - 1].p) * 1e-3)
                        {
                            list.RemoveAt(i);
                            --i;
                            removed = true;
                        }
                        if (list.Count < 3) break;
                    }
                    if (!removed) break;
                }
                InterpolatedDualSurfaceCurve.SurfacePoint[] basePoints = new InterpolatedDualSurfaceCurve.SurfacePoint[list.Count];
                for (int i = 0; i < list.Count; ++i)
                {
                    basePoints[i] = new InterpolatedDualSurfaceCurve.SurfacePoint(list[i].p, list[i].pSurface1, list[i].pSurface2);
                }
                InterpolatedDualSurfaceCurve isc = new InterpolatedDualSurfaceCurve(boxedSurface.surface, toIntersectWith, basePoints);
                return isc.ToDualSurfaceCurve();
            }

            private IntersectionPoint FindSinglePatchIntersectionPoint()
            {
                IntersectionPoint res = null;
                foreach (IntersectionPoint ip in intersectionPoints)
                {
                    if (ip.onPatch2 == null)
                    {
                        res = ip;
                        break;
                    }
                }
                if (res != null) intersectionPoints.Remove(res);
                return res;
            }
#if DEBUG
            DebuggerContainer Debug
            {
                get
                {
                    Set<UVPatch> allPatches = new Set<UVPatch>();
                    foreach (IntersectionPoint ip in intersectionPoints)
                    {
                        allPatches.Add(ip.onPatch1);
                        if (ip.onPatch2 != null)
                        {
                            allPatches.Add(ip.onPatch2);
                        }
                    }
                    DebuggerContainer dc = new DebuggerContainer();
                    foreach (UVPatch uvp in allPatches)
                    {
                        dc.Add(new Line2D(new GeoPoint2D(uvp.extent.Left, uvp.extent.Bottom), new GeoPoint2D(uvp.extent.Right, uvp.extent.Bottom)), Color.Blue, 0);
                        dc.Add(new Line2D(new GeoPoint2D(uvp.extent.Right, uvp.extent.Bottom), new GeoPoint2D(uvp.extent.Right, uvp.extent.Top)), Color.Blue, 0);
                        dc.Add(new Line2D(new GeoPoint2D(uvp.extent.Right, uvp.extent.Top), new GeoPoint2D(uvp.extent.Left, uvp.extent.Top)), Color.Blue, 0);
                        dc.Add(new Line2D(new GeoPoint2D(uvp.extent.Left, uvp.extent.Top), new GeoPoint2D(uvp.extent.Left, uvp.extent.Bottom)), Color.Blue, 0);
                        if (uvp.point1 != null && uvp.point2 != null)
                        {
                            dc.Add(new Line2D(uvp.point1.pSurface1, uvp.point2.pSurface1), Color.Red, 0);
                        }
                    }
                    return dc;
                }
            }
#endif

        }

        public virtual IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision)
        {
            ComputeIntersectionCurve cic = new ComputeIntersectionCurve(this, pl, umin, umax, vmin, vmax);
#if DEBUG
            Cube[] allCubes = this.octtree.GetAllObjects();
            // System.Diagnostics.Trace.WriteLine("Anzahl der Boxes: " + allCubes.Length.ToString());
#endif
            return cic.GetIntersectionCurves();
        }
        public void Intersect(ICurve curve, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {
            List<GeoPoint> lips = new List<GeoPoint>();
            List<GeoPoint2D> luvOnFace = new List<GeoPoint2D>();
            List<double> luOnCurve = new List<double>();
            if (curve is ISimpleCurve)
            {
                Cube[] cubes = octtree.GetObjectsCloseTo(curve as IOctTreeInsertable);
                for (int j = 0; j < cubes.Length; ++j)
                {
                    if ((curve as IOctTreeInsertable).HitTest(ref cubes[j].boundingCube, 0.0))
                    {   // es werden zu viele Würfel geliefert, GetCurveIntersection macht den Test nicht
                        GetCurveIntersection(curve as ISimpleCurve, cubes[j], lips, luvOnFace, luOnCurve);
                    }
                }
            }
            else
            {
                TetraederHull th = new TetraederHull(curve); // das muss in der Kurve gespeichert werden!!!
                for (int i = 0; i < th.TetraederBase.Length - 1; ++i)
                {
                    Cube[] cubes;

                    if (th.IsLine(i))
                    {
                        cubes = octtree.GetObjectsCloseTo(new OctTreeLine(th.TetraederBase[i], th.TetraederBase[i + 1]));
                    }
                    else if (th.IsTriangle(i))
                    {
                        cubes = octtree.GetObjectsCloseTo(new OctTreeTriangle(th.TetraederBase[i], th.TetraederVertex[2 * i], th.TetraederBase[i + 1]));
                    }
                    else
                    {
                        cubes = octtree.GetObjectsCloseTo(new OctTreeTetraeder(th.TetraederBase[i], th.TetraederVertex[2 * i], th.TetraederVertex[2 * i + 1], th.TetraederBase[i + 1]));
                    }
                    for (int j = 0; j < cubes.Length; ++j)
                    {
                        if (cubes[j].boundingCube.Interferes(th.TetraederBase[i], th.TetraederBase[i + 1], th.TetraederVertex[2 * i], th.TetraederVertex[2 * i + 1]))
                        {   // es werden zu viele Würfel geliefert, GetCurveIntersection macht den Test nicht
                            GetCurveIntersection(curve, th.TetraederParams[i], th.TetraederParams[i + 1], cubes[j], lips, luvOnFace, luOnCurve);
                        }
                    }
                }
            }
            uvOnFaces = luvOnFace.ToArray();
            ips = lips.ToArray();
            uOnCurve3Ds = luOnCurve.ToArray();
        }

        private void GetCurveIntersection(ISimpleCurve curve, Cube cube, List<GeoPoint> lips, List<GeoPoint2D> luvOnFace, List<double> luOnCurve)
        {
            GeoPoint ip;
            GeoPoint2D uv;
            double u;
            switch (NewtonCurveIntersection(curve, cube, out ip, out uv, out u))
            {
                case CurveIntersectionMode.simpleIntersection:
                    {
                        lips.Add(ip);
                        luvOnFace.Add(uv);
                        luOnCurve.Add(u);
                    }
                    break;
                case CurveIntersectionMode.noIntersection:
                    {   // wenn Newton keinen Schnittpunkt findet, dann kann es mehrere Gründe haben (die man vielleicht unterscheiden sollte)
                        // 1. Newton läuft aus dem Patch heraus. Newton würde z.B. hin und her pendeln wenn der Schnittpunkt an einem Wendepunkt
                        // liegt. Vielleicht sollte man den Patch etwas größer ansetzen, damit solche Fälle glatt laufen
                        // 2. Newton konvergiert nicht. Das passoert nur, wenn man zu weit vom Schnittpunkt weg ist. Dann also unterteilen.
                        // 3. Es gibt keinen Schnittpunkt. Dann braucht man auch nicht aufteilen.
                        // Die Fälle sind allerdings nicht leicht zu unterscheiden, deshalb hier einfach mal auf 1/100 der Maximalgröße 
                        // abprüfen und dann abbrechen. Das ist aber nicht sehr effektiv
                        if (cube.boundingCube.Size > octtree.Extend.Size / 100)
                        {
                            Cube[] splitted = SplitCube(cube);
                            for (int i = 0; i < splitted.Length; ++i)
                            {
                                if ((curve as IOctTreeInsertable).HitTest(ref splitted[i].boundingCube, 0.0))
                                {
                                    GetCurveIntersection(curve, splitted[i], lips, luvOnFace, luOnCurve);
                                }
                            }
                        }
                    }
                    break;
                case CurveIntersectionMode.curveInSurface:
                    // hier keine Punkte hinzuufügen
                    break;
            }

        }
        private enum CurveIntersectionMode { noIntersection, simpleIntersection, curveInSurface }
        private CurveIntersectionMode NewtonCurveIntersection(ISimpleCurve curve, Cube cube, out GeoPoint ip, out GeoPoint2D uv, out double u)
        {
            ICurve icurve = curve as ICurve;
            uv = cube.uvPatch.GetCenter();
            u = 0.5; // in der Mitte, unwichtig
            GeoVector udir = surface.UDirection(uv);
            GeoVector vdir = surface.VDirection(uv); // die müssen auch von der Länge her stimmen!
            GeoPoint loc = surface.PointAt(uv);
            GeoPoint curvepoint = icurve.PointAt(u);
            double error = curvepoint | loc;
            int errorcount = 0;
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            try
            {
                Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(cube.uvPatch));
                dc.Add(fc);
            }
            catch (Polyline2DException) { }
            dc.Add(curve as IGeoObject);
#endif
            ip = new GeoPoint(loc, curvepoint);
            while (error > Math.Max(Precision.eps, loc.Size * 1e-6))
            {
                double[] pars = curve.GetPlaneIntersection(new Plane(loc, udir, vdir));
                if (pars.Length == 0)
                {
                    if (CheckCurveInSurface(icurve, 0.0, 1.0, cube, u, uv))
                    {
                        return CurveIntersectionMode.curveInSurface;
                    }
                    else
                    {
                        return CurveIntersectionMode.noIntersection;
                    }
                }
                double bestDistance = double.MaxValue;
                for (int i = 0; i < pars.Length; ++i)
                {
                    GeoPoint cp = icurve.PointAt(pars[i]);
                    double d = loc | cp;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        curvepoint = cp;
                        u = pars[i];
                    }
                }

                Matrix m = DenseMatrix.OfColumnArrays(udir, vdir, udir ^ vdir);
                Vector s = (Vector)m.Solve(new DenseVector(curvepoint - loc));
                if (s.IsValid())
                {
                    double du = s[0];
                    double dv = s[1];
                    uv.x += du;
                    uv.y += dv;
                    loc = surface.PointAt(uv);
                    double e = loc | curvepoint;
                    if (e >= error)
                    {
                        ++errorcount;
                        // manchmal machts einen kleinen Schlenker bevor es konvergiert, dann nicht gleich abbrechen
                        if (errorcount > 5) // konvergiert nicht, Patch aufteilen
                            if (CheckCurveInSurface(icurve, 0.0, 1.0, cube, u, uv))
                            {
                                return CurveIntersectionMode.curveInSurface;
                            }
                            else
                            {
                                return CurveIntersectionMode.noIntersection;
                            }
                    }
                    if (!cube.uvPatch.Contains(uv))
                    {
                        if (CheckCurveInSurface(icurve, 0.0, 1.0, cube, u, uv))
                        {
                            return CurveIntersectionMode.curveInSurface;
                        }
                        else
                        {
                            return CurveIntersectionMode.noIntersection;
                        }
                    }
                    error = e;
                }
                else
                {   // singuläre matrix
                    //if (CheckCurveInSurface(icurve, 0.0, 1.0, cube, u, uv))
                    //{
                    //    return CurveIntersectionMode.curveInSurface;
                    //}
                    //else
                    //{
                    //    return CurveIntersectionMode.noIntersection;
                    //}
                    return CurveIntersectionMode.noIntersection;
                }
                udir = surface.UDirection(uv);
                vdir = surface.VDirection(uv);
            }
            ip = new GeoPoint(loc, curvepoint);
            return CurveIntersectionMode.simpleIntersection;
        }
        private CurveIntersectionMode NewtonCurveIntersection(ICurve curve, double spar, double epar, Cube cube, out GeoPoint ip, out GeoPoint2D uv, out double u)
        {
            // bei den Kurven, die leicht einen exakten Schnitt mit einer Ebene bestimmen können, also Linie, Kreis, Ellipse
            // sollte hier nicht mit der Tangente gearbeitet werden sondern mit der echten Kurve (bei Linie isses egal)
            uv = cube.uvPatch.GetCenter();
            u = (spar + epar) / 2.0;
            GeoVector udir = surface.UDirection(uv);
            GeoVector vdir = surface.VDirection(uv); // die müssen auch von der Länge her stimmen!
            GeoPoint loc = surface.PointAt(uv);
            GeoVector curvedir = curve.DirectionAt(u);
            GeoPoint curvepoint = curve.PointAt(u);
            double error = curvepoint | loc;
            int errorcount = 0;
#if DEBUG
            {
                DebuggerContainer dc = new DebuggerContainer();
                try
                {
                    Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(cube.uvPatch));
                    dc.Add(fc);
                }
                catch (Polyline2DException) { }
                dc.Add(curve as IGeoObject);
            }
#endif
            ip = new GeoPoint(loc, curvepoint);
            while (error > Math.Max(Precision.eps, loc.Size * 1e-6))
            {
                Matrix m = DenseMatrix.OfColumnArrays(udir, vdir, curvedir);
                Vector s = (Vector)m.Solve(new DenseVector(curvepoint - loc));
                if (s.IsValid())
                {
                    double du = s[0];
                    double dv = s[1];
                    double dcurve = s[2];
                    uv.x += du; // oder -=
                    uv.y += dv; // oder -=
                    loc = surface.PointAt(uv);
                    u -= dcurve; // oder -=?
                    curvepoint = curve.PointAt(u);
                    double e = loc | curvepoint;
                    if (e >= error)
                    {
                        ++errorcount;
                        // manchmal machts einen kleinen Schlenker bevor es konvergiert, dann nicht gleich abbrechen
                        if (errorcount > 5) // konvergiert nicht, Patch aufteilen
                            if (CheckCurveInSurface(curve, spar, epar, cube, u, uv))
                            {
                                return CurveIntersectionMode.curveInSurface;
                            }
                            else
                            {
                                return CurveIntersectionMode.noIntersection;
                            }
                    }
                    if (!cube.uvPatch.Contains(uv) || u < spar || u > epar)
                    {
                        if (CheckCurveInSurface(curve, spar, epar, cube, u, uv))
                        {
                            return CurveIntersectionMode.curveInSurface;
                        }
                        else
                        {
                            return CurveIntersectionMode.noIntersection;
                        }
                    }
                    error = e;
                }
                else
                {   // singuläre matrix
                    //if (CheckCurveInSurface(curve, spar, epar, cube, u, uv))
                    //{
                    //    return CurveIntersectionMode.curveInSurface;
                    //}
                    //else
                    //{
                    //    return CurveIntersectionMode.noIntersection;
                    //}
                    return CurveIntersectionMode.noIntersection;
                }
                udir = surface.UDirection(uv);
                vdir = surface.VDirection(uv);
                curvedir = curve.DirectionAt(u);
            }
            ip = new GeoPoint(loc, curvepoint);
            return CurveIntersectionMode.simpleIntersection;
        }

        private bool CheckCurveInSurface(ICurve curve, double spar, double epar, Cube cube, double u, GeoPoint2D uv)
        {   // das Problem: stelle fest, ob die Kurve in der Fläche liegt. Natürlich kann die Kurve auch aus der Fläche herausragen
            // und das macht ein Problem. Vielleicht mit GetNaturalBounds arbeiten? Wie ist PositionOf definiert, wenn der Punkt 
            // neben der Fläche liegt?
            GeoPoint p = curve.PointAt(u);
            uv = surface.PositionOf(p);
            GeoPoint p1 = surface.PointAt(uv);
            if (!Precision.IsEqual(p, p1)) return false;
            double umin, umax, vmin, vmax;
            surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            BoundingRect uvpatch = new BoundingRect(umin, vmin, umax, vmax);
            if (Precision.IsPerpendicular(curve.DirectionAt(u), surface.GetNormal(uv), false))
            {
                // man müsste hier noch die Umgebung testen, aber man weiß ja nicht in welchem Bereich surface überhaupt definiert ist
                p = curve.PointAt(spar);
                uv = surface.PositionOf(p);
                if (uvpatch.GetPosition(uv, cube.uvPatch.Size / 1000) == BoundingRect.Position.inside)
                {   // ein gültiger Punkt, da im patch
                    p1 = surface.PointAt(uv);
                    if (!Precision.IsEqual(p, p1))
                    {
                        return false;
                    }
                }
                p = curve.PointAt(epar);
                uv = surface.PositionOf(p);
                if (uvpatch.GetPosition(uv, cube.uvPatch.Size / 1000) == BoundingRect.Position.inside)
                {   // ein gültiger Punkt, da im patch
                    p1 = surface.PointAt(uv);
                    if (!Precision.IsEqual(p, p1))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private void GetCurveIntersection(ICurve curve, double spar, double epar, Cube cube, List<GeoPoint> lips, List<GeoPoint2D> luvOnFace, List<double> luOnCurve)
        {   // versucht mit Newtonverfahren einen Schnittpunkt zu finden, wenn nicht, dann wird gesplittet (sowohl Kurve als auch Fläche)
            // und bei Interference von Tetraeder mit Würfel nochmal probiert
            GeoPoint ip;
            GeoPoint2D uv;
            double u;
            switch (NewtonCurveIntersection(curve, spar, epar, cube, out ip, out uv, out u))
            {
                case CurveIntersectionMode.simpleIntersection:
                    {
                        lips.Add(ip);
                        luvOnFace.Add(uv);
                        luOnCurve.Add(u);
                    }
                    break;
                case CurveIntersectionMode.noIntersection:
                    {
                        if (cube.boundingCube.Size > octtree.Extend.Size / 100)
                        {   // siehe case CurveIntersectionMode.noIntersection von GetCurveIntersection(ISimpleCurve curve
                            Cube[] splitted = SplitCube(cube);
                            GeoPoint p1 = curve.PointAt(spar);
                            GeoPoint p2 = curve.PointAt(epar);
                            GeoPoint tv1, tv2, tv3, tv4, pm;
                            double parm;
                            TetraederHull.SplitTetraeder(curve, p1, p2, spar, epar, out pm, out parm, out tv1, out tv2, out tv3, out tv4);
                            for (int i = 0; i < splitted.Length; ++i)
                            {
                                if (splitted[i].boundingCube.Interferes(p1, pm, tv1, tv2))
                                {
                                    GetCurveIntersection(curve, spar, parm, splitted[i], lips, luvOnFace, luOnCurve);
                                }
                                if (splitted[i].boundingCube.Interferes(pm, p2, tv3, tv4))
                                {
                                    GetCurveIntersection(curve, parm, epar, splitted[i], lips, luvOnFace, luOnCurve);
                                }
                            }
                        }
                    }
                    break;
                case CurveIntersectionMode.curveInSurface:
                    // hier keine Punkte hinzuufügen
                    break;
            }
        }
        private GeoPoint2D PositionOf(GeoPoint p3d, Cube found)
        {
            // Suche vom u/v Mittelpunkt von found ausgehend mit dem Tangentenverfahren den Fußpunkt
            // wenn der patch verlassen wird, dann exception
            GeoPoint2D uv = found.uvPatch.GetCenter();
            GeoVector dirx = surface.UDirection(uv);
            GeoVector diry = surface.VDirection(uv);
            GeoVector dirz = dirx ^ diry; // die Normale
            GeoPoint loc = surface.PointAt(uv);
            BoundingRect natbound = new BoundingRect();
            surface.GetNaturalBounds(out natbound.Left, out natbound.Right, out natbound.Bottom, out natbound.Top);
            // kann auch unendlich (double.MinValue/MaxValue) werden, macht aber nix...
            double mindist = Geometry.DistPL(p3d, loc, dirz);
            int missed = 0;
            while (mindist > Precision.eps * 100)
            {
                Matrix m = DenseMatrix.OfRowArrays(dirx, diry, dirz);
                Vector res = (Vector)m.Transpose().Solve(new DenseVector(p3d - loc));
                uv.x += res[0];
                uv.y += res[1];
                if (!found.uvPatch.Contains(uv))
                {
                    ++missed;
                    if (missed > 2) throw (new DidntConverge());
                    if (!natbound.Contains(uv))
                    {   // wenn ganz außerhalb, dann auf die Grenze setzen
                        // gut, solange es konvergiert
                        if (uv.x < natbound.Left) uv.x = natbound.Left;
                        if (uv.x > natbound.Right) uv.x = natbound.Right;
                        if (uv.y < natbound.Bottom) uv.y = natbound.Bottom;
                        if (uv.y > natbound.Top) uv.y = natbound.Top;
                    }
                }
                else
                {
                    missed = 0; // damit kann es hin und her springen
                }
                dirx = surface.UDirection(uv);
                diry = surface.VDirection(uv);
                loc = surface.PointAt(uv);
                dirz = dirx ^ diry; // die Normale
                double d = Geometry.DistPL(p3d, loc, dirz);
                if (d > mindist) throw (new DidntConverge());
                mindist = d;
            }
            return uv;
        }
        private void SplitCubes(Cube[] cubes)
        {
            for (int i = 0; i < cubes.Length; i++)
            {
                SplitCube(cubes[i]);
            }
        }
        private Cube[] SplitCube(Cube toSplit)
        {   // hier wird das Objekt selbst verfeinert
            lock (this)
            {
                octtree.RemoveObject(toSplit);
                Cube[] subCubes = new Cube[4];
                subCubes[0] = new Cube();
                subCubes[1] = new Cube();
                subCubes[2] = new Cube();
                subCubes[3] = new Cube();
                // Beim Teilen ist streng darauf zu achten, dass die Grenzen von aneinander anliegenden Patches
                // identisch sind, also sich nicht durch Rundungsfehler unterscheiden
                double hcenter = (toSplit.uvPatch.Left + toSplit.uvPatch.Right) / 2.0;
                double vcenter = (toSplit.uvPatch.Bottom + toSplit.uvPatch.Top) / 2.0;
                subCubes[0].uvPatch = new BoundingRect(toSplit.uvPatch.Left, toSplit.uvPatch.Bottom, hcenter, vcenter);
                subCubes[1].uvPatch = new BoundingRect(toSplit.uvPatch.Left, vcenter, hcenter, toSplit.uvPatch.Top);
                subCubes[2].uvPatch = new BoundingRect(hcenter, toSplit.uvPatch.Bottom, toSplit.uvPatch.Right, vcenter);
                subCubes[3].uvPatch = new BoundingRect(hcenter, vcenter, toSplit.uvPatch.Right, toSplit.uvPatch.Top);
                for (int i = 0; i < 4; ++i)
                {
                    subCubes[i].boundingCube = surface.GetPatchExtent(new BoundingRect(subCubes[i].uvPatch.Left, subCubes[i].uvPatch.Bottom, subCubes[i].uvPatch.Right, subCubes[i].uvPatch.Top));
                    octtree.AddObject(subCubes[i]);
                }
                return subCubes;
            }
        }
        private Cube[] SubCubes(Cube toSplit)
        {   // hier werden nur Teilwürfel geliefert, aber es wird nicht verfeinert
            Cube[] subCubes = new Cube[4];
            subCubes[0] = new Cube();
            subCubes[1] = new Cube();
            subCubes[2] = new Cube();
            subCubes[3] = new Cube();
            double hcenter = (toSplit.uvPatch.Left + toSplit.uvPatch.Right) / 2.0;
            double vcenter = (toSplit.uvPatch.Bottom + toSplit.uvPatch.Top) / 2.0;
            subCubes[0].uvPatch = new BoundingRect(toSplit.uvPatch.Left, toSplit.uvPatch.Bottom, hcenter, vcenter);
            subCubes[1].uvPatch = new BoundingRect(toSplit.uvPatch.Left, vcenter, hcenter, toSplit.uvPatch.Top);
            subCubes[2].uvPatch = new BoundingRect(hcenter, toSplit.uvPatch.Bottom, toSplit.uvPatch.Right, vcenter);
            subCubes[3].uvPatch = new BoundingRect(hcenter, vcenter, toSplit.uvPatch.Right, toSplit.uvPatch.Top);
            for (int i = 0; i < 4; ++i)
            {
                subCubes[i].boundingCube = surface.GetPatchExtent(new BoundingRect(subCubes[i].uvPatch.Left, subCubes[i].uvPatch.Bottom, subCubes[i].uvPatch.Right, subCubes[i].uvPatch.Top));
            }
            return subCubes;
        }
#if DEBUG
        internal DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer dc = new DebuggerContainer();
                double[] usteps, vsteps;
                double umin, umax, vmin, vmax;
                surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                surface.GetSafeParameterSteps(umin, umax, vmin, vmax, out usteps, out vsteps);
                Cube[] all = octtree.GetObjectsFromBox(surface.GetPatchExtent(new BoundingRect(usteps[0], vsteps[0], usteps[usteps.Length - 1], vsteps[vsteps.Length - 1])));
                for (int i = 0; i < all.Length; ++i)
                {
                    try
                    {
                        Solid sld = all[i].boundingCube.GetSolid();
                        dc.Add(sld);
                    }
                    catch (ApplicationException) { }
                }
                return dc;
            }
        }
        internal int DebugCount
        {
            get
            {
                Cube[] all = octtree.GetAllObjects();
                return all.Length;
            }
        }
#endif
    }


    /// <summary>
    /// Ein Klasse, die ein Surface Objekt mit Würfeln einhüllt: Jeder Patch hat einen BoundingCube. Alle BoundingCubes
    /// sind in einem OctTree enthalten. Wenn ein Würfelchen verkleinert werden muss, dann wird es aus dem
    /// OctTree entfernt und die kleinen werden eingefügt. Die Würfelchen können sich überlappen
    /// </summary>
    internal class BoxedSurfaceEx
    {
        public class DidntConverge : ApplicationException
        {
            public DidntConverge() { }
        }
        internal class ParEpi : IOctTreeInsertable, IQuadTreeInsertable, IDebuggerVisualizer
        {   // ParallelEpiped, zu deutsch auch Spat oder Parallelotop genannt
            // Gegeben durch Punkt und drei Vektoren, die ein Rechtssystem bilden sollen, bei Ebenen Stücken könnte normal auch 0 sein
            // diru ist die gemittelte u-Richtung, dirv die gemittelte v Richtung
            public GeoPoint loc;
            public GeoVector diru, dirv, normal;
            public ModOp toUnit; // Abbildung in den Koordinatenraum des Parallelepipeds, so dass dieses den Einheitswürfel beschreibtm [0,1]
            public BoundingRect uvPatch; // hüllt diesen Patch ein
            public GeoPoint pll, plr, pul, pur; // die 4 Eckpunkte und die 4 Richtungen
            public GeoVector nll, nlr, nul, nur; // die Normalen in den Ecken
            public bool isFlat;
            internal bool isFolded; // hier kann man nicht mit den Ableitungen rechnen, da die Normalen kreuz und quer stehen
                                    // WeakReference<Matrix> quad3dTo2d; // a matrix, which converts 3d points to 2d uv positions with a quadratic approximation
            WeakReference quad3dTo2d; // a matrix, which converts 3d points to 2d uv positions with a quadratic approximation
#if DEBUG
            public static int idCounter = 0;
            public int id;
#endif
            public ParEpi()
            {
#if DEBUG
                id = idCounter++;
#endif
            }
            #region IOctTreeInsertable Members
            BoundingCube IOctTreeInsertable.GetExtent(double precision)
            {
                GeoPoint locz = loc + normal;
                return new BoundingCube(loc, loc + diru, loc + dirv, loc + diru + dirv, locz, locz + diru, locz + dirv, locz + diru + dirv);
            }
            bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
            {
                return cube.Interferes(loc, diru, dirv, normal);
            }
            bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
            {   // wird hoffentlich nicht gebraucht
                throw new Exception("The method or operation is not implemented.");
            }
            double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
            {   // wird hoffentlich nicht gebraucht
                throw new Exception("The method or operation is not implemented.");
            }
            bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
            {
                throw new Exception("The method or operation is not implemented.");
            }
            #endregion
            public double Size
            {
                get
                {
                    return diru.Length + dirv.Length + normal.Length;
                }
            }
            public GeoPoint GetCenter()
            {
                return loc + 0.5 * diru + 0.5 * dirv + 0.5 * normal;
            }
            public bool Interferes(GeoPoint startPoint, GeoVector direction, double maxdist, bool onlyForward)
            {
                if (isFlat)
                {
                    // loc + a*diru + b*dirv == sp + c*direction
                    // a*diru + b*dirv -c*direction = sp - loc
                    Matrix m = DenseMatrix.OfRowArrays(diru, dirv, direction);
                    Vector s = (Vector)m.Transpose().Solve(new DenseVector(startPoint - loc));
                    if (s.IsValid())
                    {
                        return (s[0] >= 0.0) && (s[0] <= 1.0) && (s[1] >= 0.0) && (s[1] <= 1.0);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return BoundingCube.UnitBoundingCube.Interferes(toUnit * startPoint, toUnit * direction, maxdist, onlyForward);
                }
            }
            public bool Interferes(GeoPoint startPoint, GeoPoint endPoint)
            {
                if (isFlat)
                {
                    // loc + a*diru + b*dirv == sp + c*direction
                    // a*diru + b*dirv -c*direction = sp - loc
                    Matrix m = DenseMatrix.OfRowArrays(diru, dirv, endPoint - startPoint);
                    Vector s = (Vector)m.Transpose().Solve(new DenseVector(startPoint - loc));
                    if (s.IsValid())
                    {
                        return (s[0] >= 0.0) && (s[0] <= 1.0) && (s[1] >= 0.0) && (s[1] <= 1.0);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    startPoint = toUnit * startPoint;
                    endPoint = toUnit * endPoint;
                    return BoundingCube.UnitBoundingCube.Interferes(ref startPoint, ref endPoint);
                }
            }
            public bool ClipLine(ref GeoPoint sp, ref GeoPoint ep)
            {
                GeoPoint usp = toUnit * sp;
                GeoPoint uep = toUnit * ep;
                if (BoundingCube.UnitBoundingCube.ClipLine(ref usp, ref uep))
                {
                    ModOp inv = toUnit.GetInverse();
                    sp = inv * usp;
                    ep = inv * uep;
                    return true;
                }
                return false;
            }
            public Face[] GetSides()
            {
                GeoPoint locz = loc + normal;
                GeoPoint[] points = new GeoPoint[] { loc, loc + diru, loc + dirv, loc + diru + dirv, locz, locz + diru, locz + dirv, locz + diru + dirv };
                Face[] faces = new Face[6];
                faces[0] = Face.MakeFace(points[0], points[1], points[3], points[2]);
                faces[1] = Face.MakeFace(points[0], points[4], points[5], points[1]);
                faces[2] = Face.MakeFace(points[0], points[2], points[6], points[4]);
                faces[3] = Face.MakeFace(points[1], points[5], points[7], points[3]);
                faces[4] = Face.MakeFace(points[2], points[3], points[7], points[6]);
                faces[5] = Face.MakeFace(points[4], points[6], points[7], points[5]);
                return faces;
            }
            public void GetPlanes(out GeoPoint[] pos, out GeoVector[] dirx, out GeoVector[] diry)
            {
                pos = new GeoPoint[6];
                dirx = new GeoVector[6];
                diry = new GeoVector[6];
                GeoPoint locz = loc + normal;
                GeoPoint[] points = new GeoPoint[] { loc, loc + diru, loc + dirv, loc + diru + dirv, locz, locz + diru, locz + dirv, locz + diru + dirv };
                pos[0] = points[1];
                pos[1] = points[4];
                pos[2] = points[2];
                pos[3] = points[5];
                pos[4] = points[3];
                pos[5] = points[6];
                dirx[0] = points[0] - points[1];
                dirx[1] = points[0] - points[4];
                dirx[2] = points[0] - points[2];
                dirx[3] = points[1] - points[5];
                dirx[4] = points[2] - points[3];
                dirx[5] = points[4] - points[6];
                diry[0] = points[3] - points[1];
                diry[1] = points[5] - points[4];
                diry[2] = points[6] - points[2];
                diry[3] = points[7] - points[5];
                diry[4] = points[7] - points[3];
                diry[5] = points[7] - points[6];
            }
            public Solid GetSolid()
            {
                Shell sh = Shell.Construct();
                sh.SetFaces(GetSides());
                Solid so = Solid.Construct();
                so.SetShell(sh);
                return so;
            }
            internal Solid AsBox
            {
                get
                {
                    return GetSolid();
                }
            }
            internal BoundingCube BoundingCube
            {
                get
                {
                    GeoPoint locz = loc + normal;
                    return new BoundingCube(loc, loc + diru, loc + dirv, loc + diru + dirv, locz, locz + diru, locz + dirv, locz + diru + dirv);
                }
            }
            internal bool Interferes(GeoPoint tb1, GeoPoint tb2, GeoPoint t3, GeoPoint t4)
            {   // Test mit Tetraeder
                return BoundingCube.UnitBoundingCube.Interferes(toUnit * tb1, toUnit * tb2, toUnit * t3, toUnit * t4);
            }
            internal bool Interferes(ParEpi other)
            {
                if (isFlat)
                {
                    if (other.isFlat)
                    {   // this is not correct!!!
                        GeoPoint p1 = other.pll;
                        GeoPoint p2 = other.plr;
                        GeoPoint p3 = other.pul;
                        GeoPoint p4 = other.pur;
                        if (this.ClipLine(ref p1, ref p2)) return true;
                        if (this.ClipLine(ref p2, ref p3)) return true;
                        if (this.ClipLine(ref p3, ref p4)) return true;
                        if (this.ClipLine(ref p4, ref p1)) return true;
                        return false;
                    }
                    else
                    {
                        return other.Interferes(this);
                    }
                }
                return BoundingCube.UnitBoundingCube.Interferes(toUnit * other.loc, toUnit * other.diru, toUnit * other.dirv, toUnit * other.normal);
            }
            internal bool Interferes(ICurve curve, double u1, double u2, GeoPoint tb1, GeoPoint tb2, GeoPoint t3, GeoPoint t4)
            {   // geht das Kurvenstück durch diesen ParEpi?
                // Endpunkte werden zu oft getestet, private Methode ohne Endpunkttest machen!
                if (!Interferes(tb1, tb2, t3, t4)) return false;
                if (BoundingCube.UnitBoundingCube.Contains(toUnit * tb1)) return true;
                if (BoundingCube.UnitBoundingCube.Contains(toUnit * tb2)) return true;
                // Start u. Endpunkt nicht drin, wohl aber das Thetraeder, da heißt es aufteilen
                if (u2 - u1 > 1e-3)
                {
                    GeoPoint tv1, tv2, tv3, tv4, pm;
                    double parm;
                    TetraederHull.SplitTetraeder(curve, tb1, tb2, u1, u2, out pm, out parm, out tv1, out tv2, out tv3, out tv4);
                    return Interferes(curve, u1, parm, tb1, pm, tv1, tv2) | Interferes(curve, parm, u2, pm, tb2, tv3, tv4);
                }
                return true; // Aufteilung zu klein und noch nichts enschieden, sicherheitshalber true liefern
            }
            internal bool Contains(GeoPoint p)
            {
                return BoundingCube.UnitBoundingCube.Contains(toUnit * p);
            }

            /// <summary>
            /// Fast calculation for a starting position for a PositionOf for the provided surface and a 3d point p3d, of which the position is queried.
            /// Uses a weak reference to a matrix, which is calculated on the first call or when it has been claimed by GC
            /// </summary>
            /// <param name="p3d"></param>
            /// <param name="surface"></param>
            /// <returns></returns>
            internal GeoPoint2D PositionOf(GeoPoint p3d, ISurface surface)
            {
                // calculate quadric for u anf v parameter
                // ax² + by² + cz² + dxy + eyz + fxz + gx + hy + iz + j == u (same for v)
                // we need 10 samples for a linear system, or we could make a larger system and solve it with QR
                Matrix x = null;
                if (quad3dTo2d != null && quad3dTo2d.IsAlive)
                {
                    x = quad3dTo2d.Target as Matrix;
                }
                if (x == null)
                {
                    Matrix m = new DenseMatrix(10, 10);
                    Matrix b = new DenseMatrix(10, 2);
                    for (int i = 0; i < 10; i++)
                    {
                        GeoPoint2D uv;
                        if (i < 4)
                        {
                            uv.x = uvPatch.Left + (i % 3) * uvPatch.Width / 2.0;
                            uv.y = uvPatch.Bottom + (i / 3) * uvPatch.Height / 2.0;
                        }
                        else if (i < 8)
                        {
                            uv.x = uvPatch.Left + ((i + 1) % 3) * uvPatch.Width / 2.0;
                            uv.y = uvPatch.Bottom + ((i + 1) / 3) * uvPatch.Height / 2.0;
                        }
                        else
                        {
                            if (uvPatch.Width > uvPatch.Height)
                            {
                                uv.x = uvPatch.Left + (i % 4 + 1) * uvPatch.Width / 3.0;
                                uv.y = uvPatch.Bottom + uvPatch.Height / 2.0;
                            }
                            else
                            {
                                uv.x = uvPatch.Left + uvPatch.Width / 2.0;
                                uv.y = uvPatch.Bottom + (i % 4 + 1) * uvPatch.Height / 3.0;
                            }
                        }
                        b[i, 0] = uv.x;
                        b[i, 1] = uv.y;
                        GeoPoint p = surface.PointAt(uv);
                        m[i, 0] = p.x * p.x;
                        m[i, 1] = p.y * p.y;
                        m[i, 2] = p.z * p.z;
                        m[i, 3] = p.x * p.y;
                        m[i, 4] = p.y * p.z;
                        m[i, 5] = p.x * p.z;
                        m[i, 6] = p.x;
                        m[i, 7] = p.y;
                        m[i, 8] = p.z;
                        m[i, 9] = 1.0;
                    }
                    x = (Matrix)m.Solve(b);
                    if (!x.IsValid()) x = new DenseMatrix(0, 0); // we need this to state, that there is no quadratic form (maybe linear in one direction)
                    quad3dTo2d = new WeakReference(x);
                }
                if (x.IsValid() && x.RowCount > 0)
                {
                    GeoPoint2D res = new GeoPoint2D(
                    x[0, 0] * p3d.x * p3d.x +
                    x[1, 0] * p3d.y * p3d.y +
                    x[2, 0] * p3d.z * p3d.z +
                    x[3, 0] * p3d.x * p3d.y +
                    x[4, 0] * p3d.y * p3d.z +
                    x[5, 0] * p3d.x * p3d.z +
                    x[6, 0] * p3d.x +
                    x[7, 0] * p3d.y +
                    x[8, 0] * p3d.z +
                    x[9, 0] * 1.0,
                    x[0, 1] * p3d.x * p3d.x +
                    x[1, 1] * p3d.y * p3d.y +
                    x[2, 1] * p3d.z * p3d.z +
                    x[3, 1] * p3d.x * p3d.y +
                    x[4, 1] * p3d.y * p3d.z +
                    x[5, 1] * p3d.x * p3d.z +
                    x[6, 1] * p3d.x +
                    x[7, 1] * p3d.y +
                    x[8, 1] * p3d.z +
                    x[9, 1] * 1.0);
                    if (uvPatch.ContainsEps(res, -0.1)) return res; // allow 10% outside (maybe this is too much)
                }
                return GeoPoint2D.Invalid;
            }

            public BoundingRect GetExtent()
            {
                return uvPatch;
            }

            public bool HitTest(ref BoundingRect rect, bool includeControlPoints)
            {
                return uvPatch.Interferes(ref rect);
            }

            GeoObjectList IDebuggerVisualizer.GetList()
            {
#if DEBUG
                Solid sld = GetSolid();
                IntegerProperty ip = new IntegerProperty(id, "Debug.Hint");
                sld.UserData.Add("Debug", ip);
                Layer l = new Attribute.Layer("ParEpi");
                l.Transparency = 180;
                sld.Layer = l;
                GeoObjectList res = new GeoObjectList(sld);
                res.Add(Line.TwoPoints(pll, plr));
                res.Add(Line.TwoPoints(plr, pur));
                res.Add(Line.TwoPoints(pur, pul));
                res.Add(Line.TwoPoints(pul, pll));
                res.Add(Line.TwoPoints(pll, pll + normal.Length * nll.Normalized));
                res.Add(Line.TwoPoints(plr, plr + normal.Length * nlr.Normalized));
                res.Add(Line.TwoPoints(pul, pul + normal.Length * nul.Normalized));
                res.Add(Line.TwoPoints(pur, pur + normal.Length * nur.Normalized));
                return new GeoObjectList(sld);
#else
                return null;
#endif
            }

            public object ReferencedObject
            {
                get { return null; }
            }

            public double Volume
            {
                get
                {
                    return normal * (diru ^ dirv);
                }
            }
        }
        QuadTree<ParEpi> quadtree;
        OctTree<ParEpi> octtree;
        BoundingRect uvbounds;
        ISurface surface;
        double[] uSingularities;
        double[] vSingularities;
        GeoPoint2D[] extrema;
        public BoxedSurfaceEx(ISurface surface, BoundingRect extent)
        {
            if (extent.IsEmpty() || extent.IsInfinite) throw new ApplicationException("BoxedSurfaceEx with undefined extent");
            this.surface = surface;
            uSingularities = surface.GetUSingularities();
            vSingularities = surface.GetVSingularities();
            double[] usteps, vsteps;
            surface.GetSafeParameterSteps(extent.Left, extent.Right, extent.Bottom, extent.Top, out usteps, out vsteps);
            // make at least 4 ParEpis
            if (usteps.Length == 2) usteps = new double[] { usteps[0], (usteps[0] + usteps[1]) / 2.0, usteps[1] };
            if (vsteps.Length == 2) vsteps = new double[] { vsteps[0], (vsteps[0] + vsteps[1]) / 2.0, vsteps[1] };
            // Versuchsweise:
#if DEBUGx
            if (surface is NurbsSurface)
            {
                GeoPoint2D cnt = extent.GetCenter();
                GeoPoint ll = surface.PointAt(extent.GetLowerLeft());
                GeoPoint lr = surface.PointAt(extent.GetLowerRight());
                GeoPoint ul = surface.PointAt(extent.GetUpperLeft());
                GeoPoint ur = surface.PointAt(extent.GetUpperRight());
                GeoVector normal = (ur - ll) ^ (lr - ul);
                DebuggerContainer dc = new DebuggerContainer();
                Line l = Line.Construct();
                l.SetTwoPoints(ur, ll);
                // dc.Add(l);
                l = Line.Construct();
                l.SetTwoPoints(lr, ul);
                // dc.Add(l);
                l = Line.Construct();
                GeoPoint mp = new GeoPoint(ll, lr, ul, ur);
                l.SetTwoPoints(mp, mp + 10 * normal);
                // dc.Add(l);

                GeoPoint pc = surface.PointAt(cnt);
                GeoVector diru, dirv;
                surface.DerivationAt(cnt, out pc, out diru, out dirv);
                l = Line.Construct();
                l.SetTwoPoints(pc, pc + 10 * diru);
                dc.Add(l);
                l = Line.Construct();
                l.SetTwoPoints(pc, pc + 10 * dirv);
                dc.Add(l);

                GeoVector du;
                GeoVector dv;
                GeoVector duu;
                GeoVector dvv;
                GeoVector duv;
                surface.Derivation2At(cnt, out pc, out du, out dv, out duu, out dvv, out duv);
                l = Line.Construct();
                l.SetTwoPoints(pc, pc + 10 * du);
                dc.Add(l);
                l = Line.Construct();
                l.SetTwoPoints(pc, pc + 10 * dv);
                dc.Add(l);


                GeoPoint2D better;
                double umin, umax, vmin, vmax;
                (surface as NurbsSurface).GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                findMaxPlaneDist(cnt, (umax - umin) + (vmax - vmin), normal, out better);
                DirMinimum dm = new DirMinimum(surface);
                GeoPoint2D uv;
                GeoPoint val;
                bool ok = dm.GetSR1Rect(normal, extent, out uv, out val);

                findMaxPlaneDist(extent.GetLowerLeft(), extent.GetLowerRight(), normal, out uv);
                GeoPoint p1 = surface.PointAt(uv);
                findMaxPlaneDist(extent.GetLowerRight(), extent.GetUpperRight(), normal, out uv);
                GeoPoint p2 = surface.PointAt(uv);
                findMaxPlaneDist(extent.GetUpperRight(), extent.GetUpperLeft(), normal, out uv);
                GeoPoint p3 = surface.PointAt(uv);
                findMaxPlaneDist(extent.GetUpperLeft(), extent.GetLowerLeft(), normal, out uv);
                GeoPoint p4 = surface.PointAt(uv);
                l = Line.Construct();
                l.SetTwoPoints(p1, p2);
                dc.Add(l);
                l = Line.Construct();
                l.SetTwoPoints(p2, p3);
                dc.Add(l);
                l = Line.Construct();
                l.SetTwoPoints(p3, p4);
                dc.Add(l);
                l = Line.Construct();
                l.SetTwoPoints(p4, p1);
                dc.Add(l);

                DebuggerContainer dc1 = new DebuggerContainer();
                GeoObjectList dbggr = (surface as NurbsSurface).DebugGrid;
            }
#endif
            // don't uncomment the following, unless there is a better solution for C.17.2.063.0000.STEP
            //if (surface is NurbsSurface)
            //{   // das muss noch in NurbsSurface geklärt werden. Aber AddCube passt ja auch auf, und bei nicht allzuwilden NurbsSurfaces sollte das gut gehen
            //    usteps = new double[] { extent.Left, extent.Left + extent.Width * 0.333, extent.Left + extent.Width * 0.666, extent.Right };
            //    vsteps = new double[] { extent.Bottom, extent.Bottom + extent.Height * 0.333, extent.Bottom + extent.Height * 0.666, extent.Top };
            //}
            uvbounds = new BoundingRect(usteps[0], vsteps[0], usteps[usteps.Length - 1], vsteps[vsteps.Length - 1]);
            octtree = new OctTree<ParEpi>(surface.GetPatchExtent(new BoundingRect(usteps[0], vsteps[0], usteps[usteps.Length - 1], vsteps[vsteps.Length - 1]), true), 0.0); // besser true
            quadtree = new QuadTree<ParEpi>(extent);
            quadtree.MaxDeepth = -1; // dynamisch
            for (int i = 0; i < usteps.Length - 1; ++i)
            {
                for (int j = 0; j < vsteps.Length - 1; ++j)
                {
                    AddCube(new BoundingRect(usteps[i], vsteps[j], usteps[i + 1], vsteps[j + 1]));
                }
            }
            // extrema = surface.GetExtrema(); wird erst bei Bedarf berechnet
#if DEBUG
            int num = octtree.GetAllObjects().Length;
            if (num > maxCnt)
            {
                maxCnt = num;
                // System.Diagnostics.Trace.WriteLine("BoxedSurfaceEx, count: " + maxCnt.ToString());
            }
            //ParEpi[] allParEpis = octtree.GetAllObjects();
            //double maxVloume = 0;
            //double avgVolume = 0;
            //DebuggerContainer[] dcs = new DebuggerContainer[allParEpis.Length];
            //ColorDef cdGreen = new ColorDef("green", Color.Green);
            //ColorDef cdRed = new ColorDef("red", Color.Red);
            //Layer trnsp = new Layer("transparent");
            //trnsp.Transparency = 128;
            //Layer opq = new Layer("opaque");
            //for (int i = 0; i < allParEpis.Length; i++)
            //{
            //    double v = allParEpis[i].Volume;
            //    if (v > maxVloume) maxVloume = v;
            //    avgVolume += v;
            //    dcs[i] = new DebuggerContainer();
            //    Solid sld = allParEpis[i].AsBox;
            //    sld.ColorDef = cdGreen;
            //    sld.Layer = trnsp;
            //    dcs[i].Add(sld);
            //    Face fc = Face.MakeFace(surface, allParEpis[i].uvPatch);
            //    fc.ColorDef = cdRed;
            //    fc.Layer = opq;
            //    dcs[i].Add(fc);
            //    Line l = Line.TwoPoints(allParEpis[i].pll, allParEpis[i].pll + allParEpis[i].nll);
            //    l.ColorDef = cdRed;
            //    l.Layer = opq;
            //    dcs[i].Add(l);
            //    l = Line.TwoPoints(allParEpis[i].plr, allParEpis[i].plr + allParEpis[i].nlr);
            //    l.ColorDef = cdRed;
            //    l.Layer = opq;
            //    dcs[i].Add(l);
            //    l = Line.TwoPoints(allParEpis[i].pul, allParEpis[i].pul + allParEpis[i].nul);
            //    l.ColorDef = cdRed;
            //    l.Layer = opq;
            //    dcs[i].Add(l);
            //    l = Line.TwoPoints(allParEpis[i].pur, allParEpis[i].pur + allParEpis[i].nur);
            //    l.ColorDef = cdRed;
            //    l.Layer = opq;
            //    dcs[i].Add(l);
            //}
            //avgVolume /= allParEpis.Length;
            //if (maxVloume > avgVolume * 10)
            //{

            //}
#endif
        }
#if DEBUG
        static int maxCnt = 0;
        static int dbgcnt = 0;
#endif
        private void AddCube(BoundingRect uvPatch)
        {
            if (surface is IRestrictedDomain rd)
            {
                if (!(rd.IsInside(uvPatch.GetLowerLeft()) || rd.IsInside(uvPatch.GetLowerRight()) || rd.IsInside(uvPatch.GetUpperLeft()) || rd.IsInside(uvPatch.GetUpperRight()))) return;
            }
            bool singu = IsUSingularity(uvPatch.Left) || IsUSingularity(uvPatch.Right);
            bool singv = IsVSingularity(uvPatch.Bottom) || IsVSingularity(uvPatch.Top);
            ParEpi cube = new ParEpi();
            cube.uvPatch = uvPatch;
            cube.pll = surface.PointAt(uvPatch.GetLowerLeft());
            cube.plr = surface.PointAt(uvPatch.GetLowerRight());
            cube.pul = surface.PointAt(uvPatch.GetUpperLeft());
            cube.pur = surface.PointAt(uvPatch.GetUpperRight());
            cube.nll = surface.GetNormal(uvPatch.GetLowerLeft());
            cube.nlr = surface.GetNormal(uvPatch.GetLowerRight());
            cube.nul = surface.GetNormal(uvPatch.GetUpperLeft());
            cube.nur = surface.GetNormal(uvPatch.GetUpperRight());
            cube.nll.NormIfNotNull();
            cube.nlr.NormIfNotNull();
            cube.nul.NormIfNotNull();
            cube.nur.NormIfNotNull();
#if DEBUG
            DebuggerContainer dc1 = new DebuggerContainer();
            dc1.Add(Line.MakeLine(cube.pll, cube.plr));
            dc1.Add(Line.MakeLine(cube.plr, cube.pur));
            dc1.Add(Line.MakeLine(cube.pur, cube.pul));
            dc1.Add(Line.MakeLine(cube.pul, cube.pll));
            dc1.Add(Line.MakeLine(cube.pll, cube.pll + cube.nll));
            dc1.Add(Line.MakeLine(cube.plr, cube.plr + cube.nlr));
            dc1.Add(Line.MakeLine(cube.pul, cube.pul + cube.nul));
            dc1.Add(Line.MakeLine(cube.pur, cube.pur + cube.nur));
#endif
            bool toosmall = uvPatch.Size < (uvbounds.Size * 1e-5) || uvPatch.Width < (uvbounds.Width * 1e-3) || uvPatch.Height < (uvbounds.Height * 1e-3);
            // Notbremse: an singulären Stellen wird der Patch zu klein
            GeoVector udirl = surface.UDirection(uvPatch.GetMiddleLeft());
            GeoVector udirr = surface.UDirection(uvPatch.GetMiddleRight());
            GeoVector vdirb = surface.VDirection(uvPatch.GetLowerMiddle());
            GeoVector vdirt = surface.VDirection(uvPatch.GetUpperMiddle());
            udirl.NormIfNotNull();
            udirr.NormIfNotNull();
            vdirb.NormIfNotNull();
            vdirt.NormIfNotNull();
            // die Normalen sind normiert oder???
            // Die Normalen sollen keine größeren Winkel einschließen als 45° (der Wert kann noch besser einjustiert werden
            double lim = Math.Sqrt(2.0) / 2.0; //45°, es gibt aber keinen logischen Grund dafür
            lim = 0.5; // changed to 60°
                       //if ((Math.Abs(cube.nll * cube.nlr) < lim || Math.Abs(cube.nll * cube.nul) < lim || Math.Abs(cube.nll * cube.nur) < lim ||
                       //    Math.Abs(cube.nlr * cube.nul) < lim || Math.Abs(cube.nlr * cube.nur) < lim || Math.Abs(cube.nul * cube.nur) < lim) && !toosmall)
                       // Math.Abs entfernt, denn sehr große Winkel (>135°) fallen sonst raus...
            GeoVector ncnt = surface.GetNormal(uvPatch.GetCenter());
            ncnt.NormIfNotNull();
            // normals at singular points are invalid. Replace them with normals at the center
            if (IsUSingularity(uvPatch.Left))
            {
                cube.nll = cube.nul = ncnt;
            }
            if (IsUSingularity(uvPatch.Right))
            {
                cube.nlr = cube.nur = ncnt;
            }
            if (IsVSingularity(uvPatch.Bottom))
            {
                cube.nll = cube.nlr = ncnt;
            }
            if (IsVSingularity(uvPatch.Top))
            {
                cube.nul = cube.nur = ncnt;
            }
            bool isFolded = false; // der Patch enthält eine Falte, die Normalen an den Ecken zeigen in völlig verschiedene Richtungen und wir sind bei einem 20tel der Fläche angekommen
            if ((cube.nll * ncnt) < 0 || (cube.nlr * ncnt) < 0 || (cube.nul * ncnt) < 0 || (cube.nur * ncnt) < 0)
            {
#if DEBUG
                GeoObjectList dbgl = new GeoObjectList();
                for (int i = 0; i < 10; i++)
                {
                    GeoPoint[] pnts = new GeoPoint[10];
                    for (int j = 0; j < 10; j++)
                    {
                        GeoPoint2D uv = new GeoPoint2D(cube.uvPatch.Left + i * cube.uvPatch.Width / 9, cube.uvPatch.Bottom + j * cube.uvPatch.Height / 9);
                        pnts[j] = surface.PointAt(uv);
                    }
                    try
                    {
                        if (!Precision.IsEqual(pnts))
                        {
                            Polyline pl = Polyline.FromPoints(pnts);
                            dbgl.Add(pl);
                        }
                    }
                    catch (PolylineException)
                    { }
                }
                dbgl.Add(Line.TwoPoints(cube.pll, cube.pll + 10 * cube.nll));
                dbgl.Add(Line.TwoPoints(cube.plr, cube.plr + 10 * cube.nlr));
                dbgl.Add(Line.TwoPoints(cube.pul, cube.pul + 10 * cube.nul));
                dbgl.Add(Line.TwoPoints(cube.pur, cube.pur + 10 * cube.nur));
                dbgl.Add(Line.TwoPoints(surface.PointAt(uvPatch.GetCenter()), surface.PointAt(uvPatch.GetCenter()) + 20 * ncnt));
                DebuggerContainer dcextra = new DebuggerContainer();
                for (int i = 0; i < 20; i++)
                {
                    GeoPoint2D uv = new GeoPoint2D((cube.uvPatch.Left + cube.uvPatch.Right) / 2, cube.uvPatch.Bottom + i * 1.0 / 19);
                    GeoPoint p0 = surface.PointAt(uv);
                    try
                    {
                        GeoPoint p1 = p0 + 10 * surface.UDirection(uv).Normalized;
                        dcextra.Add(Line.TwoPoints(p0, p1), i);
                    }
                    catch { }
                }
#endif
                isFolded = uvPatch.Size < (uvbounds.Size * 0.05) || uvPatch.Width < (uvbounds.Width * 0.05) || uvPatch.Height < (uvbounds.Height * 0.05);
            }

            if (((cube.nll * cube.nlr) < lim || (cube.nll * cube.nul) < lim || (cube.nll * cube.nur) < lim ||
                (cube.nlr * cube.nul) < lim || (cube.nlr * cube.nur) < lim || (cube.nul * cube.nur) < lim ||
                (udirl * udirr) < 0.1 || (vdirb * vdirt) < 0.1) && !toosmall && !isFolded)
            // condition (udirl * udirr) < 0.1 || (vdirb * vdirt) < 0.1 ) is for flat rotated curves, which are rotated more than about 65°
            {   // Bedingung (udirl * udirr) < lim || (vdirb * vdirt) < lim eingeführt, denn ein fast flacher Toruspatch, der in u 180° hat, in v aber nur wenig
                // besteht sonst die Prüfung, ist aber nicht gut!
                // hier einfach zu vierteilen leiefrt manchmal schlechte Ergebnisse
                // deshalb hier besser überprüfen und nur zweiteilen
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                Face fc = Face.MakeFace(this.surface, uvPatch);
                dc.Add(fc, 0);
                double len = ((cube.plr | cube.pul) + (cube.pll | cube.pur)) / 4.0;
                dc.Add(Line.TwoPoints(cube.pll, cube.pll + len * cube.nll));
                dc.Add(Line.TwoPoints(cube.plr, cube.plr + len * cube.nlr));
                dc.Add(Line.TwoPoints(cube.pul, cube.pul + len * cube.nul));
                dc.Add(Line.TwoPoints(cube.pur, cube.pur + len * cube.nur));
                dc.Add(Line.TwoPoints(surface.PointAt(uvPatch.GetCenter()), surface.PointAt(uvPatch.GetCenter()) + len * ncnt));
                dc.Add(Line.TwoPoints(cube.pll, cube.pll + len * surface.UDirection(uvPatch.GetLowerLeft())), 0);
                dc.Add(Line.TwoPoints(cube.pll, cube.pll + len * surface.VDirection(uvPatch.GetLowerLeft())), 1);
                dc.Add(Line.TwoPoints(surface.PointAt(uvPatch.GetMiddleLeft()), surface.PointAt(uvPatch.GetMiddleLeft()) + len * udirl));
                dc.Add(Line.TwoPoints(surface.PointAt(uvPatch.GetMiddleRight()), surface.PointAt(uvPatch.GetMiddleRight()) + len * udirr));
                dc.Add(Line.TwoPoints(surface.PointAt(uvPatch.GetLowerMiddle()), surface.PointAt(uvPatch.GetLowerMiddle()) + len * vdirb));
                dc.Add(Line.TwoPoints(surface.PointAt(uvPatch.GetUpperMiddle()), surface.PointAt(uvPatch.GetUpperMiddle()) + len * vdirt));
#endif
                int split = 0; // 1: split in u, 2: split in v, 3: split both
                if (((cube.nll * cube.nlr) < lim || (cube.nul * cube.nur) < lim) || (udirl * udirr) < lim) split = 1;
                else if ((cube.nll * cube.nul) < lim || (cube.nlr * cube.nur) < lim || (vdirb * vdirt) < lim) split = 2;
                else
                {   // only diagonal corners are divergent, no good criterion which parameter to split. So we split both
                    // splitu = ((cube.pll | cube.plr) + (cube.pul | cube.pur) < (cube.pll | cube.pul) + (cube.plr | cube.pur)); // changed > to < (12.3.20)
                    split = 3;
                }
                if (split == 1)
                {
                    double um = (uvPatch.Left + uvPatch.Right) / 2.0;
                    BoundingRect left, right;
                    left = uvPatch;
                    left.Right = um;
                    right = uvPatch;
                    right.Left = um;
                    AddCube(left);
                    AddCube(right);
                }
                else if (split == 2)
                {
                    double vm = (uvPatch.Bottom + uvPatch.Top) / 2.0;
                    BoundingRect bottom, top;
                    bottom = uvPatch;
                    bottom.Top = vm;
                    top = uvPatch;
                    top.Bottom = vm;
                    AddCube(bottom);
                    AddCube(top);
                }
                else if (split == 3)
                {
                    GeoPoint2D cnt = uvPatch.GetCenter();
                    BoundingRect ll, lr, ul, ur;
                    ll = uvPatch;
                    ll.Right = cnt.x;
                    ll.Top = cnt.y;
                    lr = uvPatch;
                    lr.Left = cnt.x;
                    lr.Top = cnt.y;
                    ul = uvPatch;
                    ul.Right = cnt.x;
                    ul.Bottom = cnt.y;
                    ur = uvPatch;
                    ur.Left = cnt.x;
                    ur.Bottom = cnt.y;
                    AddCube(ll);
                    AddCube(lr);
                    AddCube(ul);
                    AddCube(ur);
                }
            }
            else // if (!toosmall) why, we need these
            {
                cube.isFolded = isFolded;
                calcParallelEpiped(cube);
                // cube.boundingCube = surface.GetPatchExtent(uvPatch);
                try
                {
                    octtree.AddObject(cube);
                }
                catch { }
#if DEBUG
                ++dbgcnt;
                //Face dbgfc = Face.MakeFace(surface, cube.uvPatch);
                //GeoObjectList dbglist = (cube as IDebuggerVisualizer).GetList();
                //dbglist.Add(dbgfc);
                //ISurface dbgsrf = (surface as NurbsSurface).GetCanonicalForm(Precision.eps, null);
                //Face dbgfc = Face.MakeFace(dbgsrf, new BoundingRect(0, 0, Math.PI, Math.PI));
#endif
                quadtree.AddObject(cube);
            }
        }

        public void GetPatchHull(BoundingRect uvpatch, out GeoPoint loc, out GeoVector dir1, out GeoVector dir2, out GeoVector dir3)
        {
            // is not woking
            // Bestimmung der 4 Eckpunkte
            GeoPoint pll = surface.PointAt(uvbounds.GetLowerLeft());
            GeoPoint plr = surface.PointAt(uvbounds.GetLowerLeft());
            GeoPoint pul = surface.PointAt(uvbounds.GetUpperLeft());
            GeoPoint pur = surface.PointAt(uvbounds.GetUpperRight());
            // Bestimmung der drei Richtungen
            GeoVector normal = (pur - pll) ^ (pul - plr); // senkrecht zu den beiden Diagonalen, Länge zunächst beliebig
            GeoVector diru = (plr - pll) + (pur - pul); // gemittelte u-Richtung, Länge zunächst beliebig
            GeoVector dirv = (pul - pll) + (pur - plr); // dgl. in v
            try
            {
                Matrix m = (Matrix)DenseMatrix.OfColumnArrays(diru, dirv, normal).Inverse();
                if (m.IsValid())
                {
                    // zunächst verwenden wir einen Kubus, da hier die minima/maxima einfacher zu bestimmen sind
                    BoundingCube bc = new BoundingCube(m * pll, m * plr, m * pul, m * pur);
                    // bestimme minima und maxima in alle 6 Richtungen
                    GeoVector[] dirs = new GeoVector[6];
                    dirs[0] = normal;
                    dirs[1] = -normal;
                    dirs[2] = diru;
                    dirs[3] = -diru;
                    dirs[4] = dirv;
                    dirs[5] = -dirv;
                    DirMinimum dm = new DirMinimum(surface);
                    GeoPoint2D uv;
                    GeoPoint mx;
                    for (int i = 0; i < dirs.Length; i++)
                    {
                        // Maximum auf der Fläche
                        if (dm.GetNewtonRect(dirs[i], uvpatch, out uv, out mx))
                        {
                            bc.MinMax(m * mx);
                        }
                        // maximum auf der Randkurve
                        if (dm.GetNewtonLine(dirs[i], uvpatch.GetLowerLeft(), uvpatch.GetLowerRight(), out uv, out mx))
                        {
                            bc.MinMax(m * mx);
                        }
                        if (dm.GetNewtonLine(dirs[i], uvpatch.GetLowerRight(), uvpatch.GetUpperRight(), out uv, out mx))
                        {
                            bc.MinMax(m * mx);
                        }
                        if (dm.GetNewtonLine(dirs[i], uvpatch.GetUpperLeft(), uvpatch.GetUpperRight(), out uv, out mx))
                        {
                            bc.MinMax(m * mx);
                        }
                        if (dm.GetNewtonLine(dirs[i], uvpatch.GetLowerLeft(), uvpatch.GetUpperLeft(), out uv, out mx))
                        {
                            bc.MinMax(m * mx);
                        }
                    }
                    loc = GeoPoint.Origin + bc.Xmin * diru + bc.Ymin * dirv + bc.Zmin * normal;
                    dir1 = bc.XDiff * diru;
                    dir2 = bc.YDiff * dirv;
                    if (bc.ZDiff == 0.0) bc.Zmax = bc.Zmin + Precision.eps;
                    dir3 = bc.ZDiff * normal; // ZDiff==0.0 ist ein echtes problem, also ein komplett ebenes stück
                                              // das sollte man extra vermerken und in verschiedenen Situationen darauf rücksicht nehmen

                    //m = Matrix.RowVector(dir1, dir2, dir3).Inverse();
                    //cube.toUnit.SetData(m, m * (-cube.loc));

                    // TEST:
                    //m = Matrix.RowVector(cube.diru, cube.dirv, cube.normal).Inverse();
                    //bc = new BoundingCube(cube.toUnit * cube.pll, cube.toUnit * cube.plr, cube.toUnit * cube.pul, cube.toUnit * cube.pur);
                }
                else
                {   // wenigstens zwei Vektoren sind linear abhängig, das sollte nicht vorkommen
                    System.Diagnostics.Debug.Assert(false, "inavlid Patch in ParallelEpiped");
                }
            }
            catch (System.ApplicationException)
            {   // die Matrix ist singulär, d.h. aber dass die u und v Richtung parallel sind
                // und das sollte bei keiner Surface vorkommen. Selbst wenn singulär, dann doch nicht
                // über einen ganzen Patch singulär
                System.Diagnostics.Debug.Assert(false, "inavlid Patch in ParallelEpiped");
            }
            // damit das Ergebnis besetzt ist:
            loc = GeoPoint.Origin;
            dir1 = GeoVector.NullVector;
            dir2 = GeoVector.NullVector;
            dir3 = GeoVector.NullVector;
        }
        private void calcParallelEpiped(ParEpi cube)
        {
            // das Parallelepiped wird hier so berechnet:
            // bestimme die 3 Richtungen gemäß der 4 3d-Eckpunkte des u/v Patches
            // (die sind nicht sicher optimal, aber einfach zu bestimmen und i.A. recht gut)
            // Nimm die 4 3d-Eckpunkte in das Parallelepiped und dann noch die Extrema
            // der 4 Begrenzungskurven in den jeweiligen Richtungen. Es werden aber nicht die echten
            // Extrema genommen, sondern die durch die Richtungen aufgespannten Tetraederpunkte.
            // Zulezt werden noch die Schnittpunkte von jeweils 3 Ebenen genommen, die tangential
            // durch die Eckpunkte gehen.
            // VERBESSERUNG: 
            // 1. Gib der Fläche selbst eine Chance ein gutes Parallelepiped zu bestimmen. Die Standarflächen
            // können das bestimmt sehr gut und schnell.
            // 2. Verwende DirMinimum zum bestimmen der Minima in die 3 Richtungen, sowohl der Randkurven als auch der 
            // Flächen insgesamt. Das ist sicher viel kleiner als unser grob abgeschätztes Parallelepiped in diesem Code hier.
            // "GetPatchHull" soll diese Aufgabe übernehmen.

            // 7.7.2016: VERBESSERUNG: (noch nicht implementiert)
            // Verwende findMaxPlaneDist um Maximum der Fläche in Normalenrichtung (cube.normal) zu bestimmen
            // wenn kein Maximum, dann Maxima der 4 Randkurven in Normalenrichtung bestimmen.
            // Maxima der Kurven zu den Seitenflächen bestimmen, also UDirection zu cube.udir^cube.normal, v analog
            // Diese gefundenen Maxima für bc.MinMax verwenden. Der cube wird damit viel kleiner!
            cube.normal = (cube.pur - cube.pll) ^ (cube.pul - cube.plr); // senkrecht zu den beiden Diagonalen, Länge zunächst beliebig
            if (IsVSingularity(cube.uvPatch.Bottom) || IsVSingularity(cube.uvPatch.Top) || IsUSingularity(cube.uvPatch.Left) || IsUSingularity(cube.uvPatch.Right))
            {
                cube.normal = surface.GetNormal(cube.uvPatch.GetCenter());
            }
            cube.diru = (cube.plr - cube.pll) + (cube.pur - cube.pul); // gemittelte u-Richtung, Länge zunächst beliebig
            cube.dirv = (cube.pul - cube.pll) + (cube.pur - cube.plr); // dgl. in v
                                                                       // die 3 Vektoren sind jetzt schonmal die richtigen, die Längen stimmen noch nicht und auch der Ort fehlt
                                                                       // diverse Punkte in dieses System bringen um es dann zu normieren
            if (cube.isFolded)
            {   // hier nur grob
                cube.loc = new GeoPoint(cube.pll, cube.plr, cube.pul, cube.pur);
                cube.normal = new GeoVector(cube.nll, cube.nlr, cube.nul, cube.nur);
                cube.diru = (cube.plr - cube.pll) + (cube.pur - cube.pul);
                cube.dirv = (cube.pul - cube.pll) + (cube.pur - cube.plr);
                Matrix m = DenseMatrix.OfColumnArrays(cube.diru, cube.dirv, cube.normal);

                if (m.Rank() < 3)
                {
                }
                else
                {
                    m = (Matrix)m.Inverse();
                    GeoPoint mpll = m * cube.pll;
                    GeoPoint mplr = m * cube.plr;
                    GeoPoint mpul = m * cube.pul;
                    GeoPoint mpur = m * cube.pur;
                    GeoPoint loc = new GeoPoint(Math.Min(Math.Min(mpll.x, mplr.x), Math.Min(mpul.x, mpur.x)), Math.Min(Math.Min(mpll.y, mplr.y), Math.Min(mpul.y, mpur.y)), Math.Min(Math.Min(mpll.z, mplr.z), Math.Min(mpul.z, mpur.z)));

                    cube.toUnit.SetData(m, -loc);
                    mpll = cube.toUnit * cube.pll;
                    mplr = cube.toUnit * cube.plr;
                    mpul = cube.toUnit * cube.pul;
                    mpur = cube.toUnit * cube.pur;
                    cube.loc = cube.toUnit.GetInverse() * GeoPoint.Origin;
                }

            }
            else
            {
                try
                {
                    Matrix m = (Matrix)DenseMatrix.OfColumnArrays(cube.diru, cube.dirv, cube.normal).Inverse();
                    BoundingCube bc = new BoundingCube(m * cube.pll, m * cube.plr, m * cube.pul, m * cube.pur);
                    // für die neue Methode (7.7.2016)
                    //                GeoPoint2D found;
                    //                bool innerMaximum = false;
                    //#if DEBUG
                    //                GeoPoint dbg1 = cube.pll;
                    //                GeoPoint dbg2 = cube.pur;
                    //#endif
                    //                if (findMaxPlaneDist(cube.uvPatch.GetCenter(), cube.diru ^ cube.dirv, out found))
                    //                {
                    //                    if (cube.uvPatch.Contains(found))
                    //                    {
                    //                        GeoPoint pp = surface.PointAt(found);
                    //                        bc.MinMax(m * pp);
                    //                        innerMaximum = true;
                    //#if DEBUG
                    //                        dbg1 = pp;
                    //#endif
                    //                    }
                    //                }
                    //                if (findMaxPlaneDist(cube.uvPatch.GetCenter(), cube.normal, out found))
                    //                {
                    //                    if (cube.uvPatch.Contains(found))
                    //                    {
                    //                        GeoPoint pp = surface.PointAt(found);
                    //                        bc.MinMax(m * pp);
                    //                        innerMaximum = true;
                    //#if DEBUG
                    //                        dbg2 = pp;
                    //#endif
                    //                    }
                    //                }
                    // Die Tetraederpunkte der Kurvenstücke mit einschließen. Hier ist nicht gewährleistet, dass die
                    // gutartige sind, also keine extremen Wendepunkte haben. ggf eine Methode machen, die eine Liste
                    // solcher Punkte liefert, dann auch nur einen im flachen Fall
                    double dbgsz = bc.Size;
                    GeoPoint tv1, tv2;
                    if (!IsVSingularity(cube.uvPatch.Bottom))
                    {
                        TetraederHull.GetTetraederPoints(cube.pll, cube.plr, surface.UDirection(cube.uvPatch.GetLowerLeft()), surface.UDirection(cube.uvPatch.GetLowerRight()), out tv1, out tv2);
                        bc.MinMax(m * tv1);
                        bc.MinMax(m * tv2);
                    }
                    if (!IsVSingularity(cube.uvPatch.Top))
                    {
                        TetraederHull.GetTetraederPoints(cube.pul, cube.pur, surface.UDirection(cube.uvPatch.GetUpperLeft()), surface.UDirection(cube.uvPatch.GetUpperRight()), out tv1, out tv2);
                        bc.MinMax(m * tv1);
                        bc.MinMax(m * tv2);
                    }
                    if (!IsUSingularity(cube.uvPatch.Left))
                    {
                        TetraederHull.GetTetraederPoints(cube.pll, cube.pul, surface.VDirection(cube.uvPatch.GetLowerLeft()), surface.VDirection(cube.uvPatch.GetUpperLeft()), out tv1, out tv2);
                        bc.MinMax(m * tv1);
                        bc.MinMax(m * tv2);
                    }
                    if (!IsUSingularity(cube.uvPatch.Right))
                    {
                        TetraederHull.GetTetraederPoints(cube.plr, cube.pur, surface.VDirection(cube.uvPatch.GetLowerRight()), surface.VDirection(cube.uvPatch.GetUpperRight()), out tv1, out tv2);
                        bc.MinMax(m * tv1);
                        bc.MinMax(m * tv2);
                    }
                    // die 4 tangentialebenen in den Eckpunkten zum Schnitt bringen und diese Punkte dazufügen
                    GeoPoint ip;
                    if (Plane.Intersect3Planes(cube.pll, cube.nll, cube.plr, cube.nlr, cube.pul, cube.nul, out ip))
                    {
                        GeoPoint mip = m * ip;
                        if (mip.x >= bc.Xmin && mip.x <= bc.Xmax && mip.y >= bc.Ymin && mip.y <= bc.Ymax)
                        {   // darf sich nur auf z auswirken, Schnittpunkte außerhalb gelten nicht (Sattelflächen oder so)
                            bc.Zmin = Math.Min(bc.Zmin, mip.z);
                            bc.Zmax = Math.Max(bc.Zmax, mip.z);
                        }
                    }
                    if (Plane.Intersect3Planes(cube.pll, cube.nll, cube.plr, cube.nlr, cube.pur, cube.nur, out ip))
                    {
                        GeoPoint mip = m * ip;
                        if (mip.x >= bc.Xmin && mip.x <= bc.Xmax && mip.y >= bc.Ymin && mip.y <= bc.Ymax)
                        {   // darf sich nur auf z auswirken, Schnittpunkte außerhalb gelten nicht (Sattelflächen oder so)
                            bc.Zmin = Math.Min(bc.Zmin, mip.z);
                            bc.Zmax = Math.Max(bc.Zmax, mip.z);
                        }
                    }
                    if (Plane.Intersect3Planes(cube.pll, cube.nll, cube.pul, cube.nul, cube.pur, cube.nur, out ip))
                    {
                        GeoPoint mip = m * ip;
                        if (mip.x >= bc.Xmin && mip.x <= bc.Xmax && mip.y >= bc.Ymin && mip.y <= bc.Ymax)
                        {   // darf sich nur auf z auswirken, Schnittpunkte außerhalb gelten nicht (Sattelflächen oder so)
                            bc.Zmin = Math.Min(bc.Zmin, mip.z);
                            bc.Zmax = Math.Max(bc.Zmax, mip.z);
                        }
                    }
                    if (Plane.Intersect3Planes(cube.plr, cube.nlr, cube.pul, cube.nul, cube.pur, cube.nur, out ip))
                    {
                        GeoPoint mip = m * ip;
                        if (mip.x >= bc.Xmin && mip.x <= bc.Xmax && mip.y >= bc.Ymin && mip.y <= bc.Ymax)
                        {   // darf sich nur auf z auswirken, Schnittpunkte außerhalb gelten nicht (Sattelflächen oder so)
                            bc.Zmin = Math.Min(bc.Zmin, mip.z);
                            bc.Zmax = Math.Max(bc.Zmax, mip.z);
                        }
                    }
                    // den Ort und die Längen so setzen, dass ein Einheitswürfel entsteht
                    cube.loc = GeoPoint.Origin + bc.Xmin * cube.diru + bc.Ymin * cube.dirv + bc.Zmin * cube.normal;
                    cube.diru = bc.XDiff * cube.diru;
                    cube.dirv = bc.YDiff * cube.dirv;
                    if (bc.ZDiff == 0.0) bc.Zmax = bc.Zmin + Precision.eps;
                    double zdiff = bc.ZDiff;
                    if (zdiff == 0.0) zdiff = Precision.eps;
                    GeoVector normal = zdiff * cube.normal; // ZDiff==0.0 ist ein echtes problem, also ein komplett ebenes stück
                    if (normal.IsNullVector()) normal = cube.normal;
                    cube.normal = normal; // ZDiff==0.0 ist ein echtes problem, also ein komplett ebenes stück
                                          // das sollte man extra vermerken und in verschiedenen Situationen darauf rücksicht nehmen
                    cube.isFlat = bc.ZDiff < Precision.eps;
                    if (cube.normal.IsNullVector())
                    {
                    }

                    m = (Matrix)DenseMatrix.OfColumnArrays(cube.diru, cube.dirv, cube.normal).Inverse();
                    cube.toUnit.SetData(m, m * (-cube.loc));

#if DEBUGx
                    BoundingCube dbgext = new BoundingCube(cube.pll, cube.plr, cube.pul, cube.pur);
                    // System.Diagnostics.Trace.WriteLine("ParallelEpiped " + cube.id.ToString() + ": " + cube.Volume.ToString());
                    //if (dbgext.Size < cube.BoundingCube.Size * 0.01)
                    if (cube.Volume > 1000)
                    {
                        GeoObjectList dbgl = new GeoObjectList();
                        dbgl.Add(cube.AsBox);
                        for (int i = 0; i < 10; i++)
                        {
                            GeoPoint[] pnts = new GeoPoint[10];
                            for (int j = 0; j < 10; j++)
                            {
                                GeoPoint2D uv = new GeoPoint2D(cube.uvPatch.Left + i * cube.uvPatch.Width / 9, cube.uvPatch.Bottom + j * cube.uvPatch.Height / 9);
                                pnts[j] = surface.PointAt(uv);
                            }
                            Polyline pl = Polyline.FromPoints(pnts);
                            dbgl.Add(pl);
                        }
                    }
#endif
                    //#if DEBUG
                    //                DebuggerContainer dc = new DebuggerContainer();
                    //                Face fc = Face.MakeFace(surface, new SimpleShape(cube.uvPatch.ToBorder()));
                    //                dc.Add(fc);
                    //                dc.Add(cube.AsBox);
                    //                Line l = Line.Construct();
                    //                l.SetTwoPoints(dbg1, dbg2);
                    //                dc.Add(l);
                    //#endif

                    // TEST:
                    //m = Matrix.RowVector(cube.diru, cube.dirv, cube.normal).Inverse();
                    bc = new BoundingCube(cube.toUnit * cube.pll, cube.toUnit * cube.plr, cube.toUnit * cube.pul, cube.toUnit * cube.pur);
                }
                catch (System.ApplicationException)
                {   // die Matrix ist singulär, d.h. aber dass die u und v Richtung parallel sind
                    // und das sollte bei keiner Surface vorkommen. Selbst wenn singulär, dann doch nicht
                    // über einen ganzen Patch singulär
                    cube.loc = new GeoPoint(cube.pll, cube.plr, cube.pul, cube.pur);
                    cube.normal = new GeoVector(cube.nll, cube.nlr, cube.nul, cube.nur);
                    cube.diru = (cube.plr - cube.pll) + (cube.pur - cube.pul);
                    cube.dirv = (cube.pul - cube.pll) + (cube.pur - cube.plr);
                    Matrix m = DenseMatrix.OfColumnArrays(cube.diru, cube.dirv, cube.normal);
                    if (m.Rank() < 3)
                    {
                    }
                    else
                    {
                        m = (Matrix)m.Inverse();
                        cube.toUnit.SetData(m, m * (-cube.loc));
                    }
                    // System.Diagnostics.Debug.Assert(false, "inavlid Patch in ParallelEpiped");
                }
            }
        }
        public BoundingCube GetRawExtent()
        {
            return octtree.Extend;
        }
        private bool IsVSingularity(double v)
        {
            for (int i = 0; i < vSingularities.Length; i++)
            {
                if (Math.Abs(v - vSingularities[i]) < 1e-8) return true;
            }
            return false;
        }
        private bool IsUSingularity(double u)
        {
            for (int i = 0; i < uSingularities.Length; i++)
            {
                if (Math.Abs(u - uSingularities[i]) < 1e-8) return true;
            }
            return false;
        }
        /// <summary>
        /// Determins, whether the surface is hit by the provided bounding cube, and if so returns an arbitrary point of the surface.
        /// Problem: we would need several points, if there are more disjunct surface segments which interfere with the cube
        /// </summary>
        /// <param name="test"></param>
        /// <param name="uv"></param>
        /// <returns></returns>
        public bool HitTest(BoundingCube test, out GeoPoint2D uv)
        {
            ParEpi[] hits = octtree.GetObjectsFromBox(test);
            List<ParEpi> totest = new List<ParEpi>();
            List<ParEpi> untested = new List<ParEpi>();
            for (int i = 0; i < hits.Length; ++i)
            {
                if (test.Interferes(hits[i].loc, hits[i].diru, hits[i].dirv, hits[i].normal))
                {
                    // test the the four known points of the patch
                    if (test.Contains(hits[i].pll))
                    {
                        uv = hits[i].uvPatch.GetLowerLeft();
                        return true;
                    }
                    if (test.Contains(hits[i].pul))
                    {
                        uv = hits[i].uvPatch.GetUpperLeft();
                        return true;
                    }
                    if (test.Contains(hits[i].plr))
                    {
                        uv = hits[i].uvPatch.GetLowerRight();
                        return true;
                    }
                    if (test.Contains(hits[i].pur))
                    {
                        uv = hits[i].uvPatch.GetUpperRight();
                        return true;
                    }
                    uv = hits[i].uvPatch.GetCenter();
                    if (test.Contains(surface.PointAt(uv))) return true; // test the center of the patch
                    totest.Add(hits[i]);
                }
            }
            if (totest.Count == 0)
            {
                uv = GeoPoint2D.Invalid;
                return false;
            }

            for (int i = 0; i < totest.Count; ++i)
            {
                // tests, whether an edge of the testcube intersects with the patch
                GeoPoint[,] lines = test.Lines;
                Line line = Line.Construct();
                for (int k = 0; k < 12; k++)
                {
                    if (totest[i].Interferes(lines[k, 0], lines[k, 1]))
                    {
                        line.SetTwoPoints(lines[k, 0], lines[k, 1]);
                        GeoPoint[] ips;
                        GeoPoint2D[] uvOnFaces;
                        double[] uOnCurve3Ds;
                        surface.Intersect(line, totest[i].uvPatch, out ips, out uvOnFaces, out uOnCurve3Ds);
                        for (int j = 0; j < ips.Length; j++)
                        {
                            if (totest[i].uvPatch.Contains(uvOnFaces[j]) && uOnCurve3Ds[j] >= 0.0 && uOnCurve3Ds[j] <= 1.0)
                            {
                                uv = uvOnFaces[j];
                                return true;
                            }
                        }
                    }
                }
                if (extrema == null) calcExtrema();
                // Hat die Fläche lokale Extrema? Also Beulen, so dass die Fläche eine Ebene
                // schneiden kann, ohne dass die Randkurven eines uv-Patches die Ebene schneiden?
                // gibt es keine solchen Extrema, dann genügt es die Randkurven mit dem cube zu schneiden
                // und die Würfenkanten mit der Fläche zu schneiden. Wenn beides keine Schnitte gibt, dann
                // gibt es keinen Treffer
                if (extrema.Length > 0)
                {   // es gibt Extrema (in Achsenrichtung). Ist dieses Patch davon betroffen?
                    // Nur dann muss man aufteilen
                    bool buldgeInPatch = false;
                    for (int j = 0; j < extrema.Length; j++)
                    {
                        if (totest[i].uvPatch.Contains(extrema[j]))
                        {
                            buldgeInPatch = true;
                            break;
                        }
                    }
                    if (buldgeInPatch)
                    {
                        if (SplitHit(totest[i], test, out uv, untested)) return true;
                    }
                }

            }
            // untested enthält eine Liste noch ungetesteter Spate, die jetzt hier reduziert werden
            while (untested.Count > 0)
            {
                List<ParEpi> stillToTest = new List<ParEpi>();
                foreach (ParEpi cb in untested)
                {
                    if (SplitHit(cb, test, out uv, stillToTest)) return true;
                }
                untested = stillToTest;
            }
#if DEBUG
            if (extrema != null)
            {
                GeoObjectList dbgex = new GeoObject.GeoObjectList();
                for (int i = 0; i < extrema.Length; i++)
                {
                    Point pp = Point.Construct();
                    pp.Location = surface.PointAt(extrema[i]);
                    pp.Symbol = PointSymbol.Cross;
                    dbgex.Add(pp);
                }
            }
#endif
            uv = GeoPoint2D.Origin;
            return false;
        }

        private void calcExtrema()
        {
            if (extrema != null) return;
            ParEpi[] allcubes = octtree.GetAllObjects();
            List<GeoPoint2D> extr = new List<GeoPoint2D>();
            foreach (GeoVector dir in GeoVector.MainAxis)
            {
                foreach (ParEpi cube in allcubes)
                {
                    // liegt dir überhaupt in dem vom Patch "aufgespannten" Raum?
                    // 3 Normalenvektoren geben mit a*n1+b*n2+c*n3==dir die Bedingung, wenn a,b und c das geiche Vorzeichen haben
                    bool dotest = false;
                    Matrix m = DenseMatrix.OfColumnArrays(cube.nll, cube.nlr, cube.nul);
                    Vector x = (Vector)m.Solve(new DenseVector(dir));
                    if (x.IsValid() && Math.Sign(x[0]) == Math.Sign(x[1]) && Math.Sign(x[0]) == Math.Sign(x[2]))
                    {
#if DEBUG
                        //DebuggerContainer dc = new CADability.DebuggerContainer();
                        //Face fce = Face.MakeFace(surface, new SimpleShape(Border.MakeRectangle(cube.uvPatch)));
                        //dc.Add(fce);
                        //dc.Add(cube.AsBox);
                        //dc.Add(Line.MakeLine(GeoPoint.Origin, GeoPoint.Origin + cube.nll));
                        //dc.Add(Line.MakeLine(GeoPoint.Origin, GeoPoint.Origin + cube.nlr));
                        //dc.Add(Line.MakeLine(GeoPoint.Origin, GeoPoint.Origin + cube.nul));
#endif
                        dotest = true;
                    }
                    if (!dotest)
                    {
                        m = DenseMatrix.OfColumnArrays(cube.nur, cube.nul, cube.nlr);
                        x = (Vector)m.Solve(new DenseVector(dir));
                        if (x.IsValid() && Math.Sign(x[0]) == Math.Sign(x[1]) && Math.Sign(x[0]) == Math.Sign(x[2]))
                        {
                            dotest = true;
                        }
                    }
                    if (dotest)
                    {
                        GeoPoint2D mp;
                        if (findMaxPlaneDist(cube.uvPatch.GetCenter(), cube.uvPatch.Size, dir, out mp))
                        {
                            if (cube.uvPatch.Contains(mp)) extr.Add(mp);
                        }
                    }
                }
            }
            extrema = extr.ToArray();
        }

        private bool SplitHit(ParEpi toSplit, BoundingCube test, out GeoPoint2D uv, List<ParEpi> unknown)
        {
            // Teile toSplit auf bis entweder ein Treffer mit test gefunden ist
            // oder keine Überschneidung mehr da ist
            // Die aufgeteilten werden nur auf ihre Eckpunkte untersucht, und darauf ob das Parallelepiped mit dem Würfel
            // sich schneidet. Kein Schnitt: return false, ein Punkt drin: return true
            // ansonsten wird es in die Liste der unbekannten aufgenommen
            octtree.RemoveObject(toSplit);
            quadtree.RemoveObject(toSplit);
            ParEpi[] subCubes = new ParEpi[4];
            subCubes[0] = new ParEpi();
            subCubes[1] = new ParEpi();
            subCubes[2] = new ParEpi();
            subCubes[3] = new ParEpi();
            double hcenter = (toSplit.uvPatch.Left + toSplit.uvPatch.Right) / 2.0;
            double vcenter = (toSplit.uvPatch.Bottom + toSplit.uvPatch.Top) / 2.0;
            subCubes[0].uvPatch = new BoundingRect(toSplit.uvPatch.Left, toSplit.uvPatch.Bottom, hcenter, vcenter);
            subCubes[1].uvPatch = new BoundingRect(toSplit.uvPatch.Left, vcenter, hcenter, toSplit.uvPatch.Top);
            subCubes[2].uvPatch = new BoundingRect(hcenter, toSplit.uvPatch.Bottom, toSplit.uvPatch.Right, vcenter);
            subCubes[3].uvPatch = new BoundingRect(hcenter, vcenter, toSplit.uvPatch.Right, toSplit.uvPatch.Top);
            for (int i = 0; i < 4; ++i)
            {
                subCubes[i].pll = surface.PointAt(subCubes[i].uvPatch.GetLowerLeft());
                subCubes[i].plr = surface.PointAt(subCubes[i].uvPatch.GetLowerRight());
                subCubes[i].pul = surface.PointAt(subCubes[i].uvPatch.GetUpperLeft());
                subCubes[i].pur = surface.PointAt(subCubes[i].uvPatch.GetUpperRight());
                subCubes[i].nll = surface.GetNormal(subCubes[i].uvPatch.GetLowerLeft());
                subCubes[i].nlr = surface.GetNormal(subCubes[i].uvPatch.GetLowerRight());
                subCubes[i].nul = surface.GetNormal(subCubes[i].uvPatch.GetUpperLeft());
                subCubes[i].nur = surface.GetNormal(subCubes[i].uvPatch.GetUpperRight());
                subCubes[i].nll.NormIfNotNull();
                subCubes[i].nlr.NormIfNotNull();
                subCubes[i].nul.NormIfNotNull();
                subCubes[i].nur.NormIfNotNull();
                calcParallelEpiped(subCubes[i]);
                octtree.AddObject(subCubes[i]);
                quadtree.AddObject(subCubes[i]);
            }
            for (int i = 0; i < 4; ++i)
            {
                if (test.Interferes(subCubes[i].loc, subCubes[i].diru, subCubes[i].dirv, subCubes[i].normal) && subCubes[i].Size > Precision.eps * 100)
                {
                    //uv = subCubes[i].uvPatch.GetCenter();
                    //if (test.Contains(surface.PointAt(uv))) return true;
                    // Mittelpunkt wird erst beim Aufteilen wieder berechnet, hier nur die bereits bekannten Punkte
                    if (test.Contains(subCubes[i].pll))
                    {
                        uv = subCubes[i].uvPatch.GetLowerLeft();
                        return true;
                    }
                    if (test.Contains(subCubes[i].pul))
                    {
                        uv = subCubes[i].uvPatch.GetUpperLeft();
                        return true;
                    }
                    if (test.Contains(subCubes[i].plr))
                    {
                        uv = subCubes[i].uvPatch.GetLowerRight();
                        return true;
                    }
                    if (test.Contains(subCubes[i].pur))
                    {
                        uv = subCubes[i].uvPatch.GetUpperRight();
                        return true;
                    }
                    unknown.Add(subCubes[i]);
                }
            }
            uv = GeoPoint2D.Origin;
            return false;
        }
        internal bool IsClose(GeoPoint p3d)
        {
            ParEpi[] cubes = octtree.GetObjectsFromPoint(p3d);
            for (int i = 0; i < cubes.Length; ++i)
            {
                if (cubes[i].Contains(p3d)) return true;
            }
            return false;
        }
        internal void RawPointNormalAt(GeoPoint2D uv, out GeoPoint location, out GeoVector normal)
        {
            foreach (ParEpi pe in quadtree.ObjectsFromRect(new BoundingRect(uv)))
            {
                if (pe.uvPatch.Contains(uv))
                {
                    double u = (uv.x - pe.uvPatch.Left) / (pe.uvPatch.Right - pe.uvPatch.Left);
                    double v = (uv.y - pe.uvPatch.Bottom) / (pe.uvPatch.Top - pe.uvPatch.Bottom);
                    // u und v sind zwischen 0 und 1
                    location = new GeoPoint(new GeoPoint(pe.pll, pe.plr, u), new GeoPoint(pe.pul, pe.pur, u), v);
                    normal = new GeoVector(new GeoVector(pe.nll, pe.nlr, u), new GeoVector(pe.nul, pe.nur, u), v);
#if DEBUG
                    //GeoPoint ll = surface.PointAt(uv);
                    //GeoVector nn = surface.GetNormal(uv);
#endif
                    return;
                }
            }
            location = surface.PointAt(uv);
            normal = surface.GetNormal(uv);
        }
        internal GeoPoint RawPointAt(GeoPoint2D uv)
        {
            foreach (ParEpi pe in quadtree.ObjectsFromRect(new BoundingRect(uv)))
            {
                if (pe.uvPatch.Contains(uv))
                {
                    double u = (uv.x - pe.uvPatch.Left) / (pe.uvPatch.Right - pe.uvPatch.Left);
                    double v = (uv.y - pe.uvPatch.Bottom) / (pe.uvPatch.Top - pe.uvPatch.Bottom);
                    // u und v sind zwischen 0 und 1
                    return new GeoPoint(new GeoPoint(pe.pll, pe.plr, u), new GeoPoint(pe.pul, pe.pur, u), v);
                }
            }
            return surface.PointAt(uv);
        }
        public bool PositionOf(GeoPoint p3d, out GeoPoint2D res)
        {
            ParEpi[] cubes = octtree.GetObjectsFromPoint(p3d);
            List<ParEpi> containingPoint = new List<ParEpi>();
            for (int i = 0; i < cubes.Length; i++)
            {
                if (cubes[i].Contains(p3d)) containingPoint.Add(cubes[i]);
            }
            if (containingPoint.Count > 0) cubes = containingPoint.ToArray();
            if (cubes.Length == 0)
            {
                double size = octtree.Extend.Size / 10000;
                while (cubes.Length == 0)
                {
                    cubes = octtree.GetObjectsFromBox(new BoundingCube(p3d, size));
                    size *= 2.0;
                }
            }
            // hier kennen wir also einige cubes, die von Octtree gefunden wurden. Wir suchen den Besten
            res = GeoPoint2D.Origin;
            double minDist = double.MaxValue;
            for (int i = 0; i < cubes.Length; i++)
            {
                GeoPoint2D tmp;
                if (PositionOf(p3d, cubes[i], out tmp, out double d))
                {
                    if (d < minDist)
                    {
                        minDist = d;
                        res = tmp;
                    }
                }
            }
            if (minDist < double.MaxValue) return true;

            if (uSingularities.Length > 0)
            {
                double v = (uvbounds.Bottom + uvbounds.Top) / 2.0;
                for (int i = 0; i < uSingularities.Length; i++)
                {
                    GeoPoint pole = surface.PointAt(new GeoPoint2D(uSingularities[i], v));
                    double d = pole | p3d;
                    if (d < minDist)
                    {
                        res = new GeoPoint2D(uSingularities[i], v);
                        minDist = d;
                    }
                }
            }
            if (vSingularities.Length > 0)
            {
                double u = (uvbounds.Left + uvbounds.Right) / 2.0;
                for (int i = 0; i < vSingularities.Length; i++)
                {
                    GeoPoint pole = surface.PointAt(new GeoPoint2D(u, vSingularities[i]));
                    double d = pole | p3d;
                    if (d < minDist)
                    {
                        res = new GeoPoint2D(u, vSingularities[i]);
                        minDist = d;
                    }
                }
            }
            if (minDist < Precision.eps) return true;
            //BoundingRect ext = BoundingRect.EmptyBoundingRect;
            //for (int i = 0; i < cubes.Length; i++)
            //{
            //    ext.MinMax(cubes[i].uvPatch);
            //}

            //ICurve crv = surface.FixedU((ext.Left + ext.Right) / 2, ext.Bottom, ext.Top);
            //double u = crv.PositionOf(p3d);
            //GeoPoint pu = crv.PointAt(u);
            //crv = surface.FixedV((ext.Bottom + ext.Top) / 2, ext.Left, ext.Right);
            //double v = crv.PositionOf(p3d);
            //GeoPoint pv = crv.PointAt(v);

            return false; // der Punkt ist außerhalb der Fläche
        }
        public GeoPoint2D[] GetLineIntersection(GeoPoint startPoint, GeoVector direction)
        {
#if DEBUG
            //DebuggerContainer dc = Debug;
#endif
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            ParEpi[] cubes = octtree.GetObjectsFromLine(startPoint, direction, 0.0);
            for (int i = 0; i < cubes.Length; i++)
            {
                if (cubes[i].Interferes(startPoint, direction, 0.0, false))
                {   // der OctTree liefert halt auch cubes, die nicht die Linie berühren
                    List<GeoPoint2D> ips = GetLineIntersection(startPoint, direction, cubes[i]);
                    bool add = true;
                    for (int j = 0; j < ips.Count; j++)
                    {   // wir wollen die Punkte nur einfach. meist gibt es nur einen
                        for (int k = 0; k < res.Count; k++)
                        {
                            double d = ips[j] | res[k];
                            if (d < cubes[i].uvPatch.Size * 1e-6) add = false;
                        }
                        if (add) res.Add(ips[j]);
                    }
                }
            }
            return res.ToArray();
        }
        private List<GeoPoint2D> GetLineIntersection(GeoPoint startPoint, GeoVector direction, ParEpi cube)
        {
            // Um die Performance zu steigern könnte NewtonLineIntersection auch mahrere Schnittpunkte liefern, die auch außerhalb
            // des Patches liegen und die entsprechenden Patches würden dann nicht mehr verwendet. Ist aber schwierig.
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            GeoPoint2D uvSurface;
            if (NewtonLineIntersection(cube.uvPatch, startPoint, direction, out uvSurface))
            {
                res.Add(uvSurface);
                // Es kann sein, dass es in diesem Patch noch einen zweiten Schnittpunkt gibt (mehr als zwei sollte
                // es nicht geben, sonst ist die Aufteilung des BoxedSurfaceEx schlecht.
                // das Kriterium, ob es noch einen Schnittpunkt geben könnte ist schwierig. Hier wird gechecked, ob
                // die Normalenvektoren in den Ecken und an dem Schnittpunkt die gleiche Richtung haben
                // wie die Gerade. Wenn nicht könnte noch ein Schnitt drin sein. Das ist aber nicht notwendig. 
                // Vermutlich besser aber aufwendiger wäre es zu prüfen, ob es ein Maximum oder Minimum in der 
                // Richtung der Geraden gibt.
                int sgn = Math.Sign(surface.GetNormal(cube.uvPatch.GetLowerLeft()) * direction);
                bool split = false;
                split = split || Math.Sign(surface.GetNormal(cube.uvPatch.GetLowerRight()) * direction) != sgn;
                split = split || Math.Sign(surface.GetNormal(cube.uvPatch.GetUpperLeft()) * direction) != sgn;
                split = split || Math.Sign(surface.GetNormal(cube.uvPatch.GetUpperRight()) * direction) != sgn;
                split = split || Math.Sign(surface.GetNormal(uvSurface) * direction) != sgn;
                //double dbg1 = surface.GetNormal(cube.uvPatch.GetLowerLeft()) * direction;
                //double dbg2 = surface.GetNormal(cube.uvPatch.GetLowerRight()) * direction;
                //double dbg3 = surface.GetNormal(cube.uvPatch.GetUpperLeft()) * direction;
                //double dbg4 = surface.GetNormal(cube.uvPatch.GetUpperRight()) * direction;
                //double dbg5 = surface.GetNormal(uvSurface) * direction;
                if (split)
                {
#if DEBUG
                    //{
                    //    DebuggerContainer dc = new DebuggerContainer();
                    //    Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(cube.uvPatch.ToBorder()));
                    //    dc.Add(fc);
                    //    Line l3d = Line.MakeLine(startPoint, startPoint + direction);
                    //    dc.Add(l3d);
                    //    dc.Add(cube.GetSolid());
                    //}
#endif
                    // wir betrachten hier den uvPatch und teilen ihn so in 4 Stücke, dass der gefundene Schnittpunkt
                    // nicht dabei ist. Gehen aber nicht rekursiv sondern machen das nur einmal
                    double eps = cube.uvPatch.Size / 1000; // größe des quadratischen Loches im Patch
                    BoundingRect[] subpatches = new BoundingRect[]{
                        new BoundingRect(uvSurface.x + eps, uvSurface.y - eps, cube.uvPatch.Right, cube.uvPatch.Top),
                        new BoundingRect(cube.uvPatch.Left, uvSurface.y + eps, uvSurface.x + eps, cube.uvPatch.Top),
                        new BoundingRect(cube.uvPatch.Left, cube.uvPatch.Bottom, uvSurface.x - eps, uvSurface.y + eps),
                        new BoundingRect(uvSurface.x - eps, cube.uvPatch.Bottom, cube.uvPatch.Right, uvSurface.y - eps)};
                    for (int i = 0; i < subpatches.Length; ++i)
                    {
                        if (subpatches[i].Left < subpatches[i].Right && subpatches[i].Bottom < subpatches[i].Top)
                        {   // kann bei Schnitten am Rand vorkommen
                            if (NewtonLineIntersection(subpatches[i], startPoint, direction, out uvSurface))
                            {
                                res.Add(uvSurface);
                            }
                        }
                    }
                }
            }
            return res;
        }
        /// <summary>
        /// Finde die Stelle mit maximalen oder minimalen Abstand der Kurve (gegeben durch die Strecke [sp, ep] in uv) zu der gegebenen Ebene (norm)
        /// Die Ebene geht gewöhnlich durch die beiden Punkte bei sp und ep, dann gibt es immer ein dazwischenliegendes Maximum.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="ep"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal bool findMaxPlaneDist(GeoPoint2D sp, GeoPoint2D ep, GeoVector norm, out GeoPoint2D result)
        {
            // finde den u/v Wert zwischen sp und ep, wo die Tangentialebene parallel zur Sekante liegt
            // Die Funktion f(u, v) = (u-uc)²*d2u + (v-vc)²*d2v + (u-uc) * (v-vc) * duv + (u-uc) * du + (v-vc) * dv + p0 nähert die Fläche in dem Punkt (uc,vc) an
            // mit u = u0 + a*t, v = v0 + b*t
            // g(t) = (u0 + a*t -uc)²*d2u + (v0 + b*t-vc)²*d2v + (u0 + a*t-uc)*(v0 + b*t-vc)*duv + (u0 + a*t-uc)*du + (v0 + b*t-vc)*dv + p0 ist die angenäherte Kurve von sp nach ep
            // g'(t) = a*duv*(b*t+v0-vc)+2*b*d2v*(b*t+v0-vc)+b*duv*(a*t+u0-uc)+2*a*d2u*(a*t+u0-uc)+b*dv+a*du
            // g'(t)*n = 
            // (a*duvx*(b*t+v0-vc)+2*b*d2vx*(b*t+v0-vc)+b*duvx*(a*t+u0-uc)+2*a*d2ux*(a*t+u0-uc)+b*dvx+a*dux)*nx + 
            // (a*duvy*(b*t+v0-vc)+2*b*d2vy*(b*t+v0-vc)+b*duvy*(a*t+u0-uc)+2*a*d2uy*(a*t+u0-uc)+b*dvy+a*duy)*ny + 
            // (a*duvz*(b*t+v0-vc)+2*b*d2vz*(b*t+v0-vc)+b*duvz*(a*t+u0-uc)+2*a*d2uz*(a*t+u0-uc)+b*dvz+a*duz)*nz
            // muss null werden, d.h. die Richtung der Kurve muss senkrecht zur normalen stehen

            // konvergiert meist sehr schnell (in 2 Schritten, wenn nicht patologisch)
            GeoPoint location;
            GeoVector du;
            GeoVector dv;
            GeoVector duu;
            GeoVector dvv;
            GeoVector duv;
            double a = ep.x - sp.x;
            double b = ep.y - sp.y;
            GeoVector2D ab = new GeoVector2D(a, b);
            double t = 0.5;
            result = sp + t * ab;
            try // wenn die Ableitungen 0 werden z.B.
            {
                norm.Norm();

                surface.Derivation2At(result, out location, out du, out dv, out duu, out dvv, out duv);
                GeoVector d2u = 0.5 * duu;
                GeoVector d2v = 0.5 * dvv;
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                GeoPoint lastPoint = location;
#endif
                double u0 = sp.x;
                double v0 = sp.y;
                double uc = result.x;
                double vc = result.y;
                double a2 = (2.0 * a);
                double b2 = (2.0 * b);
                double aa = a * a;
                double aa2 = (2.0 * aa);
                double bb = b * b;
                double bb2 = (2.0 * bb);
                double ab2 = (a2 * b);
                double bduvz = (b * duv.z);
                double bduvy = (b * duv.y);
                double bduvx = (b * duv.x);
                double aduvz = (a * duv.z);
                double aduvy = (a * duv.y);
                double aduvx = (a * duv.x);

                GeoVector curvedir = a * du + b * dv;

                double err = Math.Abs(curvedir.Normalized * norm); // dieser Wert sollte 0 werden
                bool atend = false; // wenn das t rausläuft, dann wird atend gesetzt. Wenn es dann nochmal rausläuft, dann ist das Ende das Maximum
                for (int k = 0; k < 10; k++)
                {
                    t = (((-b) * dv.x - a * du.x) * norm.x + ((-b) * dv.y - a * du.y) * norm.y + ((-b) * dv.z - a * du.z) * norm.z + ((((-2.0) * a) * d2u.z - bduvz) * norm.z + (((-2.0) * a) * d2u.y - bduvy) * norm.y + (((-2.0) * a) * d2u.x - bduvx) * norm.x) * u0 + ((a2 * d2u.z + bduvz) * norm.z + (a2 * d2u.y + bduvy) * norm.y + (a2 * d2u.x + bduvx) * norm.x) * uc + ((((-2.0) * b) * d2v.z - aduvz) * norm.z + (((-2.0) * b) * d2v.y - aduvy) * norm.y + (((-2.0) * b) * d2v.x - aduvx) * norm.x) * v0 + ((b2 * d2v.z + aduvz) * norm.z + (b2 * d2v.y + aduvy) * norm.y + (b2 * d2v.x + aduvx) * norm.x) * vc) / ((aa2 * d2u.z + bb2 * d2v.z + ab2 * duv.z) * norm.z + (aa2 * d2u.y + bb2 * d2v.y + ab2 * duv.y) * norm.y + (aa2 * d2u.x + bb2 * d2v.x + ab2 * duv.x) * norm.x);

                    if (t < 0.0)
                    {
                        t = 0.0;
                        if (atend)
                        {
                            result = sp;
                            return true;
                        }
                        atend = true;
                    }
                    else if (t > 1.0)
                    {
                        t = 1.0;
                        if (atend)
                        {
                            result = ep;
                            return true;
                        }
                        atend = true;
                    }
                    else atend = false;
                    result = sp + t * ab;
                    uc = result.x; // fürs nächste t
                    vc = result.y;
                    surface.Derivation2At(result, out location, out du, out dv, out duu, out dvv, out duv);
                    d2u = 0.5 * duu;
                    d2v = 0.5 * dvv;
                    bduvz = (b * duv.z); // fürs nächste t
                    bduvy = (b * duv.y);
                    bduvx = (b * duv.x);
                    aduvz = (a * duv.z);
                    aduvy = (a * duv.y);
                    aduvx = (a * duv.x);

                    curvedir = a * du + b * dv;
                    double te = Math.Abs(curvedir.Normalized * norm); // der neue Fehler
                    if (te > err) return false; // konvergiert nicht
                    err = te;
                    if (err < 1e-10) return true;
#if DEBUG
                    Line l = Line.Construct();
                    l.SetTwoPoints(lastPoint, location);
                    dc.Add(l);
                    lastPoint = location;
#endif
                }

                return true;
            }
            catch (ApplicationException)
            {
                return false;
            }
        }
        /// <summary>
        /// Newton Verfahren zum Finden eines Maximums für eine gegebene Richtung, von einem (u,v) Punkt ausgehend
        /// Es wird ein Polynom 2. Grades in u und v in diesem Punkt erzeugt, dessen 1. und 2. Ableitungen mit der Fläche übereinstimmen, das also die Fläche in diesem Punkt approximiert.
        /// Für dieses Polynom kann einfach das maximum in diese Richtung gefunden werden (2 lineare Gleichungen, so dass diru und dirv senkrecht zu gegebenen Rictung stehen)
        /// Dieser (u,v) Punkt wird auf die Fläche übertragen für den nächsten Iterationsschritt. 
        /// </summary>
        /// <param name="startpos"></param>
        /// <param name="normal"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal bool findMaxPlaneDist(GeoPoint2D startpos, double maxStepSize, GeoVector normal, out GeoPoint2D result)
        {
            // duu, dv u.s.w: die 1. und 2. Ableitungen nach u und v, d2u = 1/2 * duu
            // Die Funktion f(u,v) = u²*d2u+v²*d2v+u*v*duv+u*du+v*dv+p0 nähert die Fläche in einem Punkt an (d2u u.s.w. sind 3d Vektoren/Punkte)
            // df/du(u,v) = 2*u*d2u + du + v*duv, df/dv(u,v) = 2*v*d2v + dv + u*duv sind die beiden Richtungen der angenäherten Fläche
            //  (2*u*d2u + du + v*duv)*n = 0 und (2*v*d2v + dv + u*duv)*n = 0 (Skalarproduke, Richtungen senkrecht zur gegebenen Normale)
            // 2*d2u ist die 2. Ableitung des Nurbs, duu = 2*d2u, daraus ergeben sich die beiden Gleichungen:
            // u * (duu * n) + v * (duv * n) = -du*n und
            // u * (duv * n) + v * (dvv * n) = -dv*n
            // konvertiert gut, wenn in der Nähe der Lösung, sonst kann es wild rumspringen. Dann wird der Schritt solange verkleinert, biss es wieder stabil läuft
            GeoPoint location;
            GeoVector du;
            GeoVector dv;
            GeoVector duu;
            GeoVector dvv;
            GeoVector duv;
            result = startpos;
            try // wenn die Ableitungen 0 werden z.B.
            {
                normal.NormIfNotNull();
                surface.Derivation2At(result, out location, out du, out dv, out duu, out dvv, out duv);
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                GeoPoint lastPoint = location;
#endif
                double err = Math.Abs(du.Normalized * normal) + Math.Abs(dv.Normalized * normal); // dieser Wert sollte 0 werden
                for (int k = 0; k < 10; k++)
                {
                    Matrix m = new DenseMatrix(2, 2);
                    m[0, 0] = duu * normal;
                    m[1, 0] = m[0, 1] = duv * normal;
                    m[1, 1] = dvv * normal;
                    Vector b = new DenseVector(2);
                    b[0] = -du * normal;
                    b[1] = -dv * normal;
                    Vector x;

                    if (m.QR().IsFullRank) x = (Vector)m.QR().Solve(b); // qrd ist stabiler, wenns um sehr kleine Werte geht, oder?
                    else x = (Vector)m.Solve(b);
                    if (x.IsValid())
                    {
                        GeoVector2D step = new GeoVector2D(x[0], x[1]);
                        if (step.Length > maxStepSize * 2) return false;
                        double te;
                        do
                        {
                            surface.Derivation2At(result + step, out location, out du, out dv, out duu, out dvv, out duv);
                            te = Math.Abs(du.Normalized * normal) + Math.Abs(dv.Normalized * normal);
                            if (te < err) break;
                            step = 0.3 * step; // wenn der Fehler nicht kleiner wird, dann einen drittel Schritt weitergehen
                        } while (step.Length > 1e-10);
                        err = te;
                        result = result + step;
                        if (err < 1e-10) break;
#if DEBUG
                        Line l = Line.Construct();
                        l.SetTwoPoints(lastPoint, location);
                        dc.Add(l);
                        lastPoint = location;
#endif
                    }
                    else return false; // linear (zumindest in einer Richtung)
                }

                return true;
            }
            catch (ApplicationException)
            {
                return false;
            }
        }

        private bool NewtonLineIntersection(BoundingRect boundingRect, GeoPoint startPoint, GeoVector direction, out GeoPoint2D uvSurface)
        {   // sollte es einen Schnittpunkt in diesem Patch geben, so muss der auch gefunden werden.
            // Es ist nicht sicher, ob von den 4 Eckpunkten und dem Mittelpunkt ausgehend ein Schnittpunkt mit dem Newtonverfahren
            // sicher gefunden wird. Kann ja auch sein, dass es einen Schnittpunkt gibt, aber alle Anfangsbedingungen konvergieren
            // zu einem anderen Schnittpunkt. Das Beispiel müsste noch gefunden werden und als Kriterium für die Aufteilung
            // in Würfel verwendet werden.
            double umin, umax, vmin, vmax;
            surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            if (umin == double.MinValue || umax == double.MaxValue)
            {
                umin = uvbounds.Left;
                umax = uvbounds.Right;
            }
            if (vmin == double.MinValue || vmax == double.MaxValue)
            {
                vmin = uvbounds.Bottom;
                vmax = uvbounds.Top;
            }
            BoundingRect maximumuvRect = new BoundingRect(umin, vmin, umax, vmax); // damit es nicht rausläuft, besser wäre auf die grenze klippen, oder?
            for (int c = 0; c < 5; ++c)
            {
                // mit verschiedenen Startpunkten versuchen. Wenn von keinem Startpunkt aus eine Lösung zu finden ist, dann
                // aufgeben
                switch (c)
                {
                    default:
                    case 0:
                        uvSurface = boundingRect.GetCenter();
                        break;
                    case 1:
                        uvSurface = boundingRect.GetLowerLeft();
                        break;
                    case 2:
                        uvSurface = boundingRect.GetUpperRight();
                        break;
                    case 3:
                        uvSurface = boundingRect.GetUpperLeft();
                        break;
                    case 4:
                        uvSurface = boundingRect.GetLowerRight();
                        break;
                }
                GeoVector udir = surface.UDirection(uvSurface);
                GeoVector vdir = surface.VDirection(uvSurface); // die müssen auch von der Länge her stimmen!
                GeoPoint loc = surface.PointAt(uvSurface);
                double error = Geometry.DistPL(loc, startPoint, direction);
                int errorcount = 0;
                int outside = 0; // wenn ein Schnitt außerhalb ist, dann nicht gleich aufgeben, erst wenn zweimal hintereinander außerhalb
                                 // damit am Rand oszillierende Punkte gefunden werden können
#if DEBUG
                {
                    //DebuggerContainer dc = new DebuggerContainer();
                    //try
                    //{
                    //    Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(boundingRect));
                    //    dc.Add(fc);
                    //}
                    //catch (Polyline2DException) { }
                    //try
                    //{
                    //    Plane pln = new Plane(loc, udir, vdir);
                    //    Face plnfc = Face.MakeFace(new PlaneSurface(loc, udir, vdir, udir ^ vdir), new CADability.Shapes.SimpleShape(CADability.Shapes.Border.MakeRectangle(0, 1, 0, 1)));
                    //    dc.Add(plnfc);
                    //    Line ln = Line.Construct();
                    //    ln.SetTwoPoints(startPoint - direction, startPoint + direction);
                    //    dc.Add(ln);
                    //}
                    //catch (PlaneException) { }
                }
#endif

                while (true) // entweder kommt break oder return
                {
                    Matrix m = DenseMatrix.OfColumnArrays(udir, vdir, direction);
                    Vector s = (Vector)m.Solve(new DenseVector(startPoint - loc));
                    if (s.IsValid())
                    {
                        double du = s[0];
                        double dv = s[1];
                        if (du > umax - umin) du = umax - umin;
                        if (du < umin - umax) du = umin - umax;
                        if (dv > vmax - vmin) dv = vmax - vmin;
                        if (dv < vmin - vmax) dv = vmin - vmax;
                        double l = s[2];
                        uvSurface.x += du; // oder -=
                        uvSurface.y += dv; // oder -=
                        loc = surface.PointAt(uvSurface);
                        udir = surface.UDirection(uvSurface);
                        vdir = surface.VDirection(uvSurface); // die müssen auch von der Länge her stimmen!
                        double e = Geometry.DistPL(loc, startPoint, direction);
#if DEBUG
                        //DebuggerContainer dc = new DebuggerContainer();
                        //try
                        //{
                        //    Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(boundingRect));
                        //    dc.Add(fc);
                        //}
                        //catch (Polyline2DException) { }
                        //try
                        //{
                        //    Plane pln = new Plane(loc, udir, vdir);
                        //    Face plnfc = Face.MakeFace(new PlaneSurface(pln), new CADability.Shapes.SimpleShape(CADability.Shapes.Border.MakeRectangle(-1, 1, -1, 1)));
                        //    dc.Add(plnfc);
                        //}
                        //catch (PlaneException) { }
                        //Line ln = Line.Construct();
                        //ln.SetTwoPoints(startPoint - direction, startPoint + direction);
                        //dc.Add(ln);
                        //ln = Line.Construct();
                        //ln.SetTwoPoints(loc, loc + udir);
                        //dc.Add(ln, 1);
                        //ln = Line.Construct();
                        //ln.SetTwoPoints(loc, loc + vdir);
                        //dc.Add(ln, 2);
#endif
                        if (!boundingRect.Contains(uvSurface))
                        {
                            if (!maximumuvRect.Contains(uvSurface)) break; // denn damit kann man nicht weiterrechnen
                                                                           // man könnte höchsten auf den maximumuvRect Bereich klippen
                            ++outside;
                            if (outside > 2) break; // läuft aus dem Patch raus. Mit anderem Startwert versuchen
                        }
                        else
                        {
                            outside = 0; // innerhalb, zurücksetzen
                        }
                        if (e >= error)
                        {
                            ++errorcount;
                            // manchmal machts einen kleinen Schlenker bevor es konvergiert, dann nicht gleich abbrechen
                            if (errorcount > 5) break; // konvergiert nicht, mit diesem Startwert abbrechen
                        }
                        else if (e < Math.Max(Precision.eps, loc.Size * 1e-6))
                        {
                            return true; // gefunden!
                        }
                        error = e;
                    }
                    else
                    {   // singuläre matrix
                        break;
                    }
                }
            }
            uvSurface = boundingRect.GetCenter(); // muss halt einen Wert haben
            return false; // keine Lösung gefunden
        }
        public ICurve[] GetSelfIntersection()
        {
            // Die Idee: zwei Cubes überlappen sich. Betrachte die u und v Richtungen an den Ecken. 
            // Wenn zwei davon in entgegengesetzte Richtungen gehen (>90°), dann besteht die Möglichkeit der
            // Selbstüberschneideung. Dann noch weiter aufteilen. Letztlich feste u oder v Kurvenabschnitte
            // mit der Fläche schneiden.
            return null;
        }
        class ComputeIntersectionCurve
        {
            class UVPatch : IQuadTreeInsertable
            {   // auf jedem Patch befinden sich maximal zwei IntersectionPoints.
                // im Zweifelsfall bei den Eckpunkten werden solche Patches ignoriert, die nur einen Punkt haben
                public BoundingRect extent;
                public IntersectionPoint point1;
                public IntersectionPoint point2;
                public void Add(IntersectionPoint intersectionPoint)
                {
                    if (point1 == null) point1 = intersectionPoint;
                    else point2 = intersectionPoint;
                }
                internal void Remove(IntersectionPoint toRemove)
                {
                    if (point1 == toRemove)
                    {
                        point1 = point2;
                        point2 = null;
                    }
                    else if (point2 == toRemove)
                    {
                        point2 = null;
                    }
                }

                #region IQuadTreeInsertable Members

                BoundingRect IQuadTreeInsertable.GetExtent()
                {
                    return extent;
                }

                bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
                {
                    return extent.Interferes(ref rect);
                }

                object IQuadTreeInsertable.ReferencedObject
                {
                    get { throw new Exception("The method or operation is not implemented."); }
                }

                #endregion
            }
            class IntersectionPoint
            {   // Ein Punkt ist immer der Schnittpunkt einer Masche, also einer Begrenzung eines uvPatches
                // Wenn Punkte genau auf Ecken liegen, dann sind auch onPatch3 und onPatch4 besetzt
                public GeoPoint p;
                public GeoPoint2D pSurface1; // das ist immer die BoxedSurfaceEx
                public GeoPoint2D pSurface2; // das ist die andere Fläche
                public bool fixedu; // Schnittpunkt einer Kante mit festem u, wenn false mit festem v
                public bool isOnPatchVertex; // liegt genau auf einer Ecke des Patches
                                             // der Punkt liegt auf Kanten dieser beiden Patches (kann auch nur einer sein, Rand, wie isses mit Ecken?)
                public UVPatch onPatch1;
                public UVPatch onPatch2;
                int hashCode;
                static int hashCodeCounter = 0;
                public IntersectionPoint()
                {
                    hashCode = ++hashCodeCounter;
                }

                public void AddPatch(UVPatch uvPatch)
                {
                    if (isOnPatchVertex)
                    {   // es handelt sich um einen Eckpunkt eines Patches, dann gelten immer nur die oberen oder rechten
                        if (fixedu)
                        {   // der Punkt muss Unterkante des Patches sein, denn hier sollen nur die oberen gelten
                            if (uvPatch.extent.Top == pSurface1.y) return;
                        }
                        else
                        {
                            if (uvPatch.extent.Right == pSurface1.x) return;
                        }
                    }
                    if (onPatch1 == null) onPatch1 = uvPatch;
                    else if (onPatch2 == null) onPatch2 = uvPatch;
                    else
                    {   // darf nicht vorkommen, für Breakpoint
                    }
                    uvPatch.Add(this);
                }
                public override int GetHashCode()
                {
                    return hashCode;
                }
                public override bool Equals(object obj)
                {
                    return (obj as IntersectionPoint).hashCode == hashCode;
                }
                internal void RemovePatch(UVPatch uVPatch)
                {
                    if (onPatch1 == uVPatch)
                    {
                        onPatch1 = onPatch2;
                        onPatch2 = null;
                    }
                    else if (onPatch2 == uVPatch)
                    {
                        onPatch2 = null;
                    }
                }
            }
            BoxedSurfaceEx BoxedSurfaceEx;
            ISurfaceImpl toIntersectWith;
            BoundingRect uvSize;
            Dictionary<double, List<IntersectionPoint>> uIntersections; // Schnittpunkte zu festem u bereits bestimmt
            Dictionary<double, List<IntersectionPoint>> vIntersections;
            QuadTree<UVPatch> patches;
            Set<IntersectionPoint> intersectionPoints; // Schnittpunkte bislang gefunden
            List<IntersectionPoint> onPatchVertex; // verworfene Schnittpunkte, da sie doppelt vorkommen und genau auf
                                                   // dem Eck eines Patches liegen. Möglicherweise müssen Kurven daran wieder zusammengefügt werden
            public ComputeIntersectionCurve(BoxedSurfaceEx BoxedSurfaceEx, ISurfaceImpl toIntersectWith, double umin, double umax, double vmin, double vmax)
            {
                this.BoxedSurfaceEx = BoxedSurfaceEx;
                this.toIntersectWith = toIntersectWith;
                uIntersections = new Dictionary<double, List<IntersectionPoint>>();
                vIntersections = new Dictionary<double, List<IntersectionPoint>>();
                uvSize = new BoundingRect(umin, vmin, umax, vmax);
                patches = new QuadTree<UVPatch>(uvSize);
                intersectionPoints = new Set<IntersectionPoint>();
                onPatchVertex = new List<IntersectionPoint>();
            }
            List<IntersectionPoint> FixedParameterIntersections(double uv, bool uParameter)
            {   // das soll ALLE Schnittpunkte für einen festen u bzw. v wert liefern. 
                List<IntersectionPoint> res = new List<IntersectionPoint>();
                if (uParameter)
                {
                    if (!uIntersections.TryGetValue(uv, out res))
                    {
                        ICurve fu = BoxedSurfaceEx.surface.FixedU(uv, BoxedSurfaceEx.uvbounds.Bottom, BoxedSurfaceEx.uvbounds.Top);

                        GeoPoint[] ips;
                        GeoPoint2D[] uvOnSurface;
                        double[] uOnCurve;
                        toIntersectWith.Intersect(fu, uvSize, out ips, out uvOnSurface, out uOnCurve);
                        NewtonMend(toIntersectWith, fu, ref ips, ref uvOnSurface, ref uOnCurve);
                        res = new List<IntersectionPoint>();
                        for (int i = 0; i < ips.Length; ++i)
                        {
                            IntersectionPoint ip = new IntersectionPoint();
                            ip.p = ips[i];
                            double vOnSurface1 = BoxedSurfaceEx.uvbounds.Bottom + uOnCurve[i] * BoxedSurfaceEx.uvbounds.Height; // s.u.
                            ip.pSurface1 = new GeoPoint2D(uv, vOnSurface1);
                            // ip.pSurface1 = BoxedSurfaceEx.surface.PositionOf(ips[i]);
                            ip.pSurface2 = uvOnSurface[i];
                            ip.fixedu = true;
                            res.Add(ip);
                            // fehlen noch die Patches
                        }
                        uIntersections[uv] = res;
                    }
                }
                else
                {
                    if (!vIntersections.TryGetValue(uv, out res))
                    {
                        ICurve fv = BoxedSurfaceEx.surface.FixedV(uv, BoxedSurfaceEx.uvbounds.Left, BoxedSurfaceEx.uvbounds.Right);
                        GeoPoint[] ips;
                        GeoPoint2D[] uvOnSurface;
                        double[] uOnCurve;
                        toIntersectWith.Intersect(fv, uvSize, out ips, out uvOnSurface, out uOnCurve);
                        NewtonMend(toIntersectWith, fv, ref ips, ref uvOnSurface, ref uOnCurve);
                        res = new List<IntersectionPoint>();
                        for (int i = 0; i < ips.Length; ++i)
                        {
                            IntersectionPoint ip = new IntersectionPoint();
                            ip.p = ips[i];
                            // wir müssen auf den "natürlichen" Parameter von u kommen
                            // uOnCurve ist im 0..1 System der Kurve. hoffentlich geht das linear
                            double uOnSurface1 = BoxedSurfaceEx.uvbounds.Left + uOnCurve[i] * BoxedSurfaceEx.uvbounds.Width;
                            ip.pSurface1 = new GeoPoint2D(uOnSurface1, uv);
                            // ip.pSurface1 = BoxedSurfaceEx.surface.PositionOf(ips[i]);
                            ip.pSurface2 = uvOnSurface[i];
                            ip.fixedu = false;
                            res.Add(ip);
                            // fehlen noch die Patches
                        }
                        vIntersections[uv] = res;
                    }
                }
                return res;
            }

            private void NewtonMend(ISurface surface, ICurve curve, ref GeoPoint[] ips, ref GeoPoint2D[] uvOnSurface, ref double[] uOnCurve)
            {   // Mend heißt reparieren, nachbessern. Das Abbruchkriterium beim Schnittpunktfinden ist, wenn der Punkt auf der Kurve
                // und der Punkt auf der Fläche weniger als eps Abstand haben. Das ist aber nicht gut, denn bei flachen Schnitten
                // kann da immer noch zu weit vom echten Schnittpunkt entfernt sein. Deshalb hier Nachbessern mit Newton und
                // Abbruch unter Einbeziehung des Winkels.
                for (int i = 0; i < ips.Length; ++i)
                {
                    GeoVector udir = surface.UDirection(uvOnSurface[i]);
                    GeoVector vdir = surface.VDirection(uvOnSurface[i]);
                    GeoVector normal = udir ^ vdir;
                    GeoVector direction = curve.DirectionAt(uOnCurve[i]);
                    double eps = Precision.eps * Math.Abs(GeoVector.Cos(normal, direction));
                    GeoPoint pOnSurface = surface.PointAt(uvOnSurface[i]);
                    GeoPoint pOnCurve = curve.PointAt(uOnCurve[i]);
                    double error = pOnCurve | pOnSurface;
                    while (error > eps)
                    {
                        Matrix m = DenseMatrix.OfColumnArrays(udir, vdir, direction);
                        Vector s = (Vector)m.Solve(new DenseVector(pOnCurve - pOnSurface));
                        if (s.IsValid())
                        {
                            double du = s[0];
                            double dv = s[1];
                            double l = s[2];
                            uvOnSurface[i].x += du;
                            uvOnSurface[i].y += dv;
                            uOnCurve[i] -= l;
                            pOnSurface = surface.PointAt(uvOnSurface[i]);
                            pOnCurve = curve.PointAt(uOnCurve[i]);
                            udir = surface.UDirection(uvOnSurface[i]);
                            vdir = surface.VDirection(uvOnSurface[i]);
                            normal = udir ^ vdir;
                            direction = curve.DirectionAt(uOnCurve[i]);
                            eps = Precision.eps * Math.Abs(GeoVector.Cos(normal, direction));
                            double e = pOnCurve | pOnSurface;
                            if (e >= error) return; // konvergiert nicht, sollte beim Nachbessern nicht vorkommen
                            error = e;
                        }
                        else // singuläre Matrix
                        {
                            return;
                        }
                    }
                    ips[i] = new GeoPoint(pOnSurface, pOnCurve);
                }
            }
            void CheckIntersectinPoints(ParEpi cube)
            {
                List<IntersectionPoint> left = FixedParameterIntersections(cube.uvPatch.Left, true);
                List<IntersectionPoint> right = FixedParameterIntersections(cube.uvPatch.Right, true);
                List<IntersectionPoint> bottom = FixedParameterIntersections(cube.uvPatch.Bottom, false);
                List<IntersectionPoint> top = FixedParameterIntersections(cube.uvPatch.Top, false);
                List<IntersectionPoint> inPatch = new List<IntersectionPoint>();
                UVPatch uvPatch = new UVPatch();
                uvPatch.extent = cube.uvPatch;
                // erstmal alle suchen, die auch in dem Patch liegen, FixedParameterIntersections liefert nämlich alle
                for (int i = 0; i < left.Count; ++i)
                {   // zuerst einrastern auf Eckpunkte, so es ein Eckpunkt ist
                    if (Math.Abs(left[i].pSurface1.y - cube.uvPatch.Bottom) < BoxedSurfaceEx.uvbounds.Height * 1e-6)
                    {
                        left[i].pSurface1.y = cube.uvPatch.Bottom;
                        left[i].isOnPatchVertex = true;
                    }
                    if (Math.Abs(left[i].pSurface1.y - cube.uvPatch.Top) < BoxedSurfaceEx.uvbounds.Height * 1e-6)
                    {
                        left[i].pSurface1.y = cube.uvPatch.Top;
                        left[i].isOnPatchVertex = true;
                    }
                    if (left[i].pSurface1.y >= cube.uvPatch.Bottom && left[i].pSurface1.y <= cube.uvPatch.Top)
                    {
                        inPatch.Add(left[i]);
                    }
                }
                for (int i = 0; i < right.Count; ++i)
                {
                    if (Math.Abs(right[i].pSurface1.y - cube.uvPatch.Bottom) < BoxedSurfaceEx.uvbounds.Height * 1e-6)
                    {
                        right[i].pSurface1.y = cube.uvPatch.Bottom;
                        right[i].isOnPatchVertex = true;
                    }
                    if (Math.Abs(right[i].pSurface1.y - cube.uvPatch.Top) < BoxedSurfaceEx.uvbounds.Height * 1e-6)
                    {
                        right[i].pSurface1.y = cube.uvPatch.Top;
                        right[i].isOnPatchVertex = true;
                    }
                    if (right[i].pSurface1.y >= cube.uvPatch.Bottom && right[i].pSurface1.y <= cube.uvPatch.Top)
                    {
                        inPatch.Add(right[i]);
                    }
                }
                for (int i = 0; i < bottom.Count; ++i)
                {
                    if (Math.Abs(bottom[i].pSurface1.x - cube.uvPatch.Left) < BoxedSurfaceEx.uvbounds.Width * 1e-6)
                    {
                        bottom[i].pSurface1.x = cube.uvPatch.Left;
                        bottom[i].isOnPatchVertex = true;
                    }
                    if (Math.Abs(bottom[i].pSurface1.x - cube.uvPatch.Right) < BoxedSurfaceEx.uvbounds.Width * 1e-6)
                    {
                        bottom[i].pSurface1.x = cube.uvPatch.Right;
                        bottom[i].isOnPatchVertex = true;
                    }
                    if (bottom[i].pSurface1.x >= cube.uvPatch.Left && bottom[i].pSurface1.x <= cube.uvPatch.Right)
                    {
                        inPatch.Add(bottom[i]);
                    }
                }
                for (int i = 0; i < top.Count; ++i)
                {
                    if (Math.Abs(top[i].pSurface1.x - cube.uvPatch.Left) < BoxedSurfaceEx.uvbounds.Width * 1e-6)
                    {
                        top[i].pSurface1.x = cube.uvPatch.Left;
                        top[i].isOnPatchVertex = true;
                    }
                    if (Math.Abs(top[i].pSurface1.x - cube.uvPatch.Right) < BoxedSurfaceEx.uvbounds.Width * 1e-6)
                    {
                        top[i].pSurface1.x = cube.uvPatch.Right;
                        top[i].isOnPatchVertex = true;
                    }
                    if (top[i].pSurface1.x >= cube.uvPatch.Left && top[i].pSurface1.x <= cube.uvPatch.Right)
                    {
                        inPatch.Add(top[i]);
                    }
                }
                // liegt ein Schnittpunkt genau auf dem Eck eines uvPatches, dann kommt er oft doppelt vor
                // für jede Seite einmal. Einer wird hier verworfen. Das kann zur Folge haben, dass Kurven
                // unterbrochen sind. Mit Hilve von onPatchVertex könnte man sie wieder zusammensetzen
                bool removed;
                do
                {
                    removed = false;
                    for (int i = 0; i < inPatch.Count - 1; ++i)
                    {
                        for (int j = i + 1; j < inPatch.Count; ++j)
                        {
                            if ((inPatch[i].p | inPatch[j].p) < 2 * Precision.eps ||
                                (Math.Abs(inPatch[i].pSurface1.x - inPatch[j].pSurface1.x) < cube.uvPatch.Width * 1e-6 &&
                                Math.Abs(inPatch[i].pSurface1.y - inPatch[j].pSurface1.y) < cube.uvPatch.Height * 1e-6))
                            {   // jeder Punkt hat den Fehler Precision.eps, deshalb 2*
                                onPatchVertex.Add(inPatch[j]);
                                inPatch.RemoveAt(j);
                                removed = true;
                                break; // es gibt ja immer nur Paare und j>i, also problemlos abbrechen
                                       // leider gibt es bei Tangenten oft vielfache fast identische Punkte
                                       // das müsste man besser dort lösen, dann würde diese Schleife einfach reichen
                            }
                        }
                    }
                } while (removed);
                if (inPatch.Count < 2)
                {
                    return;
                }
                else if (inPatch.Count == 2)
                {
                    for (int i = 0; i < inPatch.Count; ++i)
                    {
                        inPatch[i].AddPatch(uvPatch);
                        intersectionPoints.Add(inPatch[i]);
                    }
                }
                else
                {
                    // hier könnten welche auf den Ecken sitzen, das muss zuerst gechecked werden
                    // allgemein: Mehrfachschnitte, also aufteilen
#if DEBUG
                    DebuggerContainer dc = new DebuggerContainer();
                    try
                    {
                        Face fcdbg = Face.MakeFace(BoxedSurfaceEx.surface, new CADability.Shapes.SimpleShape(uvPatch.extent));
                        dc.Add(fcdbg);
                    }
                    catch (Polyline2DException) { }
                    BoundingRect uvplane = BoundingRect.EmptyBoundingRect;
                    for (int i = 0; i < inPatch.Count; ++i)
                    {
                        uvplane.MinMax(inPatch[i].pSurface2);
                        Point pnt = Point.Construct();
                        pnt.Location = inPatch[i].p;
                        pnt.Symbol = PointSymbol.Cross;
                        pnt.ColorDef = new CADability.Attribute.ColorDef("xxx", Color.Black);
                        dc.Add(pnt);
                    }
                    uvplane.Inflate(1.0, 1.0);
                    Face fcpln = Face.MakeFace(this.toIntersectWith, new CADability.Shapes.SimpleShape(uvplane));
                    dc.Add(fcpln);
#endif
                    ParEpi[] cubes = BoxedSurfaceEx.SplitCube(cube); // wird echt, also auch im octtree aufgeteilt
                    for (int i = 0; i < cubes.Length; ++i)
                    {
                        CheckIntersectinPoints(cubes[i]);
                    }
                }
            }
            public IDualSurfaceCurve[] GetIntersectionCurves(BoundingRect br)
            {
                // Hängt bei parallelen Flächen, wird total aufgeteilt wenn die Flächen identisch sind.
                ParEpi[] cubes = BoxedSurfaceEx.octtree.GetObjectsCloseTo(toIntersectWith);
                for (int i = 0; i < cubes.Length; ++i)
                {
                    if (cubes[i].uvPatch.Interferes(ref br)) CheckIntersectinPoints(cubes[i]);
                }
                IntersectionPoint startwith = FindSinglePatchIntersectionPoint();
                List<List<IntersectionPoint>> curves = new List<List<IntersectionPoint>>();
                while (startwith != null)
                {   // offene Kurven, Eckpunkte im uvPatch sind noch nicht berücksichtigt...
                    List<IntersectionPoint> curve = new List<IntersectionPoint>();
                    IntersectionPoint goOnWith = startwith;
                    while (goOnWith != null)
                    {   // immer mit onPatch1 weitergehen, da der Punkt bei seiner Verwendung den Ausgangspatch verliert
                        if (intersectionPoints.Contains(goOnWith))
                        {
                            curve.Add(goOnWith);
                        }
                        else break;
                        IntersectionPoint toRemove = goOnWith;
                        if (goOnWith.onPatch1 == null)
                        {
                            goOnWith = null;
                        }
                        else
                        {
                            if (goOnWith.onPatch1.point1 == goOnWith)
                            {
                                goOnWith = goOnWith.onPatch1.point2;
                            }
                            else
                            {
                                goOnWith = goOnWith.onPatch1.point1;
                            }
                        }
                        if (goOnWith != null) goOnWith.RemovePatch(toRemove.onPatch1);
                        RemoveIntersectionPoint(toRemove); // erst hier entfernen, da er auch aus den Patches entfernt wird
                    }
                    if (curve.Count > 1) curves.Add(curve);
                    startwith = FindSinglePatchIntersectionPoint();
                }
                if (intersectionPoints.Count > 0)
                {
                    startwith = intersectionPoints.GetAny(); // irgend ein Anfang, jetzt gibts geschlossene
                }
                while (startwith != null)
                {   // geschlossene Kurven
                    List<IntersectionPoint> curve = new List<IntersectionPoint>();
                    IntersectionPoint goOnWith = startwith;
                    while (goOnWith != null)
                    {
                        if (curve.Count == 0 || ((curve[curve.Count - 1].p | goOnWith.p) > Precision.eps))
                        {   // nicht den gleichen Punkt zweimal
                            curve.Add(goOnWith);
                        }
                        intersectionPoints.Remove(goOnWith);
                        if (goOnWith.onPatch1.point1 == goOnWith)
                        {
                            goOnWith = goOnWith.onPatch1.point2;
                        }
                        else
                        {
                            goOnWith = goOnWith.onPatch1.point1;
                        }
                        if (!intersectionPoints.Contains(goOnWith)) break;
                        if (goOnWith == startwith)
                        {
                            if (curve.Count == 0 || ((curve[curve.Count - 1].p | goOnWith.p) > Precision.eps))
                            {
                                curve.Add(goOnWith); // damit es geschlossen wird
                            }
                            break;
                        }
                    }
                    if (curve.Count > 1) curves.Add(curve);
                    if (intersectionPoints.Count > 0)
                    {
                        startwith = intersectionPoints.GetAny(); // irgend ein Anfang, jetzt gibts geschlossene
                    }
                    else
                    {
                        startwith = null;
                    }
                }
                IDualSurfaceCurve[] res = new IDualSurfaceCurve[curves.Count];
                for (int i = 0; i < curves.Count; ++i)
                {
                    res[i] = MakeDualSurfaceCurve(curves[i]);
                }
                //res[1].Curve2D1.PointAt(0.55555);
                //GeoPoint2D[] p2d = new GeoPoint2D[100];
                //for (int i = 0; i < 100; i++)
                //{
                //    p2d[i] = res[1].Curve2D1.PointAt(i/100.0);
                //}
                //Polyline2D pl2d = new Polyline2D(p2d);
                return res;
            }

            private void RemoveIntersectionPoint(IntersectionPoint toRemove)
            {
                if (toRemove.onPatch1 != null) toRemove.onPatch1.Remove(toRemove);
                if (toRemove.onPatch2 != null) toRemove.onPatch2.Remove(toRemove);
                intersectionPoints.Remove(toRemove);
            }

            private IDualSurfaceCurve MakeDualSurfaceCurve(List<IntersectionPoint> list)
            {   // es können sehr viele Punkte entstanden sein auf der Kurve, wenn die Fläche in viele 
                // Stücke unterteilt werden musste. zu viele Punkte stören bei der weiteren Verwendung der Kurve
                // z.B. hohe Dreieckszahlen u.s.w. deshalb wird hier reduziert, es bleiben mindestens 5, oder?
#if DEBUG
                GeoPoint[] pl = new GeoPoint[list.Count];
                for (int i = 0; i < pl.Length; ++i)
                {
                    pl[i] = list[i].p;
                }
                Polyline pol = Polyline.Construct();
                pol.SetPoints(pl, false);
#endif
                while (list.Count > 10)
                {
                    bool removed = false;
                    for (int i = list.Count - 2; i > 0; --i)
                    {
                        if (Geometry.DistPL(list[i].p, list[i + 1].p, list[i - 1].p) < (list[i + 1].p | list[i - 1].p) * 1e-3)
                        {
                            list.RemoveAt(i);
                            --i;
                            removed = true;
                        }
                        if (list.Count < 3) break;
                    }
                    if (!removed) break;
                }
                InterpolatedDualSurfaceCurve.SurfacePoint[] basePoints = new InterpolatedDualSurfaceCurve.SurfacePoint[list.Count];
                for (int i = 0; i < list.Count; ++i)
                {
                    basePoints[i] = new InterpolatedDualSurfaceCurve.SurfacePoint(list[i].p, list[i].pSurface1, list[i].pSurface2);
                }
                InterpolatedDualSurfaceCurve isc = new InterpolatedDualSurfaceCurve(BoxedSurfaceEx.surface, toIntersectWith, basePoints);
                return isc.ToDualSurfaceCurve();
            }

            private IntersectionPoint FindSinglePatchIntersectionPoint()
            {
                IntersectionPoint res = null;
                foreach (IntersectionPoint ip in intersectionPoints)
                {
                    if (ip.onPatch2 == null)
                    {
                        res = ip;
                        break;
                    }
                }
                if (res != null) intersectionPoints.Remove(res);
                return res;
            }
#if DEBUG
            DebuggerContainer Debug
            {
                get
                {
                    Set<UVPatch> allPatches = new Set<UVPatch>();
                    foreach (IntersectionPoint ip in intersectionPoints)
                    {
                        allPatches.Add(ip.onPatch1);
                        if (ip.onPatch2 != null)
                        {
                            allPatches.Add(ip.onPatch2);
                        }
                    }
                    DebuggerContainer dc = new DebuggerContainer();
                    foreach (UVPatch uvp in allPatches)
                    {
                        dc.Add(new Line2D(new GeoPoint2D(uvp.extent.Left, uvp.extent.Bottom), new GeoPoint2D(uvp.extent.Right, uvp.extent.Bottom)), Color.Blue, 0);
                        dc.Add(new Line2D(new GeoPoint2D(uvp.extent.Right, uvp.extent.Bottom), new GeoPoint2D(uvp.extent.Right, uvp.extent.Top)), Color.Blue, 0);
                        dc.Add(new Line2D(new GeoPoint2D(uvp.extent.Right, uvp.extent.Top), new GeoPoint2D(uvp.extent.Left, uvp.extent.Top)), Color.Blue, 0);
                        dc.Add(new Line2D(new GeoPoint2D(uvp.extent.Left, uvp.extent.Top), new GeoPoint2D(uvp.extent.Left, uvp.extent.Bottom)), Color.Blue, 0);
                        if (uvp.point1 != null && uvp.point2 != null)
                        {
                            dc.Add(new Line2D(uvp.point1.pSurface1, uvp.point2.pSurface1), Color.Red, 0);
                        }
                    }
                    return dc;
                }
            }
#endif

        }

        public virtual IDualSurfaceCurve[] GetPlaneIntersection(PlaneSurface pl, double umin, double umax, double vmin, double vmax, double precision)
        {
            Plane pln = pl.Plane;
            ParEpi[] cubesOnPlane = octtree.GetObjectsFromPlane(pln);
            if (cubesOnPlane.Length == 0) return new IDualSurfaceCurve[0];
            BoundingRect plbounds = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < cubesOnPlane.Length; i++)
            {
                plbounds.MinMax(pln.Project(cubesOnPlane[i].pll));
                plbounds.MinMax(pln.Project(cubesOnPlane[i].plr));
                plbounds.MinMax(pln.Project(cubesOnPlane[i].pul));
                plbounds.MinMax(pln.Project(cubesOnPlane[i].pur)); // genügt es, die 4 Punkte der anderen Surface zu nehmen?
                                                                   // es soll schnell gehen, die Ausdehnung selbst ist nicht so wichtig
            }
            PlaneSurface other = pl.Clone() as PlaneSurface;
            plbounds.Inflate(plbounds.Size); // damit sollte es auf jeden Fall groß genug sein
            other.usedArea = plbounds; // ohne usedArea kann man keine BoxedSurface davon machen, und das braucht Intersect
            ICurve[] cvs = Intersect(new BoundingRect(umin, vmin, umax, vmax), other, plbounds, new List<GeoPoint>());
#if DEBUG
            Face dbgfc = Face.MakeFace(other, new SimpleShape(Border.MakeRectangle(plbounds)));
#endif
            IDualSurfaceCurve[] res = new IDualSurfaceCurve[cvs.Length];
            for (int i = 0; i < res.Length; i++)
            {
                res[i] = cvs[i] as IDualSurfaceCurve;
            }
            return res;

            //Unreachable code
            /*
            // alter Text:
            ComputeIntersectionCurve cic = new ComputeIntersectionCurve(this, pl, umin, umax, vmin, vmax);
#if DEBUG
            ParEpi[] allCubes = this.octtree.GetAllObjects();
            // System.Diagnostics.Trace.WriteLine("Anzahl der Boxes: " + allCubes.Length.ToString());
#endif
            return cic.GetIntersectionCurves(new BoundingRect(umin, vmin, umax, vmax));
            */
        }
        public virtual IDualSurfaceCurve[] GetSurfaceIntersection(ISurface surface, double umin, double umax, double vmin, double vmax, double precision)
        {
            // um die bessere Methode "Intersect" zu verwenden (siehe GetPlaneIntersection), müsste man einen Bereich auf "surface" kennen
            // im konkreten Aufruffall das mal abchecken.
            ComputeIntersectionCurve cic = new ComputeIntersectionCurve(this, surface as ISurfaceImpl, umin, umax, vmin, vmax);
            return cic.GetIntersectionCurves(new BoundingRect(umin, vmin, umax, vmax));
        }
        struct Position
        {
            public double u;   // auf der Kurve
            public GeoPoint2D uv; // auf der Fläche
            public BoundingRect patch; // Flächenpatch
            public GeoPoint pcurve;
            public GeoPoint psurface;
            public double distance; // vorzeichenbehafteter Abstand
            public GeoVector dir; // Richtung der Kurve
            public GeoVector normal; // normale der Fläche
        }
        public void IntersectEx(ICurve curve, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {
            List<GeoPoint> lips = new List<GeoPoint>();
            List<GeoPoint2D> luvOnFace = new List<GeoPoint2D>();
            List<double> luOnCurve = new List<double>();
            Dictionary<double, Position> relevantPositions = new Dictionary<double, Position>();
            TetraederHull th = new TetraederHull(curve); // das muss in der Kurve gespeichert werden!!!
                                                         // alle relevanten ParEpis sammeln
            Set<ParEpi> relevantCubes = new Set<ParEpi>();
            for (int i = 0; i < th.TetraederBase.Length - 1; ++i)
            {
                ParEpi[] cubes;
                if (th.IsLine(i))
                {
                    cubes = octtree.GetObjectsCloseTo(new OctTreeLine(th.TetraederBase[i], th.TetraederBase[i + 1]));
                }
                else if (th.IsTriangle(i))
                {
                    cubes = octtree.GetObjectsCloseTo(new OctTreeTriangle(th.TetraederBase[i], th.TetraederVertex[2 * i], th.TetraederBase[i + 1]));
                }
                else
                {
                    cubes = octtree.GetObjectsCloseTo(new OctTreeTetraeder(th.TetraederBase[i], th.TetraederVertex[2 * i], th.TetraederVertex[2 * i + 1], th.TetraederBase[i + 1]));
                }
                relevantCubes.AddMany(cubes);
            }
            // alle Basispunkte, wenn sie denn in der Nähe der Fläche sind zu den relevanten Punkten hinzufügen
            for (int i = 0; i < th.TetraederBase.Length; ++i)
            {
                GeoPoint2D uv;
                if (PositionOf(th.TetraederBase[i], out uv))
                {   // leider ist der cube hier unbekannt, deshalb muss er noch gesucht werden
                    GeoPoint surfp = surface.PointAt(uv);
                    ParEpi[] cubes = octtree.GetObjectsFromPoint(surfp);
                    BoundingRect patch = BoundingRect.EmptyBoundingRect;
                    for (int j = 0; j < cubes.Length; j++)
                    {
                        if (cubes[j].uvPatch.Contains(uv))
                        {
                            patch = cubes[j].uvPatch;
                            relevantCubes.Remove(cubes[j]);
                            break;
                        }
                    }
                    if (!patch.IsEmpty())
                    {
                        Position pos = new Position();
                        pos.u = th.TetraederParams[i];
                        pos.uv = uv;
                        pos.patch = patch;
                        pos.pcurve = curve.PointAt(pos.u);
                        pos.psurface = surfp;
                        pos.normal = surface.GetNormal(uv).Normalized;
                        pos.distance = Geometry.LinePar(surfp, pos.normal, pos.pcurve);
                        pos.dir = curve.DirectionAt(pos.u);
                        relevantPositions[pos.u] = pos;
                    }
                }
            }
            // wenn die Kurve groß is gegenüber der Fläche kann die Liste der relevanten Punkte auch leer sein
            // jetzt müssen alle noch nicht berücksichtigten ParEpis noch Punkte generieren
            foreach (ParEpi cube in relevantCubes)
            {
                double u = curve.PositionOf(cube.GetCenter());
                GeoPoint pcurve = curve.PointAt(u);
                GeoPoint2D uv;
                if (PositionOf(pcurve, out uv))
                {
                    if (cube.uvPatch.Contains(uv))
                    {   // wie oben zufügen
                    }
                }
            }
            // alle Patches im Verlauf der relevanten Punkte müssen zusammenhängen. Wenn Lücken sind, dann Zwischenpunkte
            // nehmen und Patch bestimmen...
            uvOnFaces = luvOnFace.ToArray();
            ips = lips.ToArray();
            uOnCurve3Ds = luOnCurve.ToArray();
        }

        public void Intersect(ICurve curve, BoundingRect uvExtent, out GeoPoint[] ips, out GeoPoint2D[] uvOnFaces, out double[] uOnCurve3Ds)
        {
            // Überlegungen 19.11.09: 
            // 1.: Die BoxedSurface sollte aus nicht zu verzerrten ParEpis bestehen: wenn die u oder v Richtung
            // mehr als das 8-fache der jeweiligen anderen Richtung ist, dann diese Richtung aufteilen.
            // 2.: bestimme Punkte auf der Kurve: alle Eckpunkte der Tetraederkurve und noch zusätzliche Punkte. Zu jedem 
            // Punkt brauchen wir die entsprechende uv Position auf der Fläche - wenn es eine gibt. Für jedes in Frage kommende ParEpi 
            // brauchen wir einen Punkt. Zusätzlich noch EIntritts bzw. Austrittspunkte, also von Punkten, die außerhalb der
            // Hülle liegen zu Punkten die innerhalb liegen. Die Punkte als Dictionary u->(uv, Abstand, n*dir)
            // 3.: Mit diesen Punkten als Basis können wir fortschreiten: Haben zwei Punkte verschiedenen Abstand (Vorzeichen)
            // dann mit Newton od Bisektion (Abhängig davon wie steil und ob Newton konvergiert) Für Punkte mit gleichem
            // Abstand (Vorzeichen) und verschiedenem Vorzeichen von n*dir (das sagt annähren oder entfernen) Zwischenpunkte
            // bestimmen. Entweder man findet verschiedenen Abstand, dann wie oben (Newton oder Bisektieon) oder man findet
            // n*dir==0.0, dann Minimum ohne Schnittpunkt gefunden.

            List<GeoPoint> lips = new List<GeoPoint>();
            List<GeoPoint2D> luvOnFace = new List<GeoPoint2D>();
            List<double> luOnCurve = new List<double>();
            if (curve is ISimpleCurve)
            {
                ParEpi[] cubes = octtree.GetObjectsCloseTo(curve as IOctTreeInsertable);
                for (int j = 0; j < cubes.Length; ++j)
                {
                    BoundingCube bc = cubes[j].BoundingCube;
                    if (cubes[j].uvPatch.Interferes(ref uvExtent) && (curve as IOctTreeInsertable).HitTest(ref bc, 0.0))
                    {   // only check the relevant cubes
                        // there is a bug: GetCurveIntersection only finds single intersection points where there might be multiple intersections
                        GetCurveIntersection(curve as ISimpleCurve, cubes[j], lips, luvOnFace, luOnCurve);
                    }
                }
            }
            else
            {
                TetraederHull th = new TetraederHull(curve); // das muss in der Kurve gespeichert werden!!!
                /* nicht genügend abgesicherter Test für keinen Schnittpunkt
                // zuerst den Fall ausschließen, dass es keine Schnittpunkte gibt
                // Alle Tetraederpunkte auf ihre uv Position bezüglich der Fläche bestimmen
                // liegen alle außerhalb oder alle auf der selben Seite der Fläche, dann gibt es keinen Schnitt
                List<GeoPoint2D> uvPoints = new List<GeoPoint2D>();
                List<double> orientation = new List<double>();
                for (int i = 0; i < th.TetraederBase.Length; ++i)
                {
                    GeoPoint2D uv;
                    if (PositionOf(th.TetraederBase[i], out uv))
                    {
                        GeoPoint loc;
                        GeoVector du, dv;
                        surface.DerivationAt(uv, out loc, out du, out dv);
                        Matrix m = Matrix.RowVector(udir, vdir, udir ^ vdir);
                        Matrix s = m.SaveSolve(Matrix.RowVector(th.TetraederBase[i] - loc));
                        if (s != null)
                        {
                            uvPoints.Add(uv);
                            orientation.Add(s[2, 0]); // über oder unter der Tangentialebene
                        }
                    }
                }
                bool noIntersection = uvPoints.Count == 0;
                bool left = true;
                bool right = true;
                bool bottom = true;
                bool top = true;
                double umin,umx,vmin,vmax;
                surface.GetNaturalBounds(out umin, out umx, out vmin, out vmax);
                for (int i = 0; i < uvPoints.Count; i++)
                {
                    if (uvPoints[i].x > umin) left = false;
                    if (uvPoints[i].x < umax) right = false;
                    if (uvPoints[i].y > vmin) bottom = false;
                    if (uvPoints[i].y < vmax) top = false;
                }
                noIntersection |= left | right | bottom | top; // count==0: es ist und bleibt true
                if (!noIntersection)
                {
                    // alle Punkte auf der selben Seite?
                    bool neg = true, pos = true;
                    for (int i = 0; i <orientation.Count; i++)
                    {
                        if (orientation[i] >= 0) neg = false;
                        if (orientation[i] <= 0) pos = false;
                    }
                    noIntersection |= neg | pos;
                }
                */
                for (int i = 0; i < th.TetraederBase.Length - 1; ++i)
                {
                    ParEpi[] cubes;

                    if (th.IsLine(i))
                    {
                        cubes = octtree.GetObjectsCloseTo(new OctTreeLine(th.TetraederBase[i], th.TetraederBase[i + 1]));
                    }
                    else if (th.IsTriangle(i))
                    {
                        cubes = octtree.GetObjectsCloseTo(new OctTreeTriangle(th.TetraederBase[i], th.TetraederVertex[2 * i], th.TetraederBase[i + 1]));
                    }
                    else
                    {
                        cubes = octtree.GetObjectsCloseTo(new OctTreeTetraeder(th.TetraederBase[i], th.TetraederVertex[2 * i], th.TetraederVertex[2 * i + 1], th.TetraederBase[i + 1]));
                    }
                    for (int j = 0; j < cubes.Length; ++j)
                    {
                        if (cubes[j].uvPatch.Interferes(ref uvExtent) && cubes[j].Interferes(th.TetraederBase[i], th.TetraederBase[i + 1], th.TetraederVertex[2 * i], th.TetraederVertex[2 * i + 1]))
                        {
                            if (cubes[j].Interferes(curve, th.TetraederParams[i], th.TetraederParams[i + 1], th.TetraederBase[i], th.TetraederBase[i + 1], th.TetraederVertex[2 * i], th.TetraederVertex[2 * i + 1]))
                            {
                                GeoPoint2D uvStart = cubes[j].uvPatch.GetCenter();
                                double tStart = (th.TetraederParams[i] + th.TetraederParams[i + 1]) / 2;
                                bool found = false;
                                GeoPoint ip;
                                if (curve is InterpolatedDualSurfaceCurve dsc)
                                {
                                    GeoPoint closePoint = new GeoPoint(surface.PointAt(uvStart), curve.PointAt(tStart));
                                    ////Polynom implicitSurface1 = null, implicitSurface2 = null, implicitSurface3 = null;
                                    ////if (dsc.Surface1 is ISurfaceImpl si1) implicitSurface1 = si1.GetImplicitPolynomial();
                                    ////if (dsc.Surface2 is ISurfaceImpl si2) implicitSurface2 = si2.GetImplicitPolynomial();
                                    ////if (surface is ISurfaceImpl si3) implicitSurface3 = si3.GetImplicitPolynomial();
                                    ////if (implicitSurface1 != null && implicitSurface2 != null && implicitSurface3 != null)
                                    ////{
                                    ////    // Performance test: 3 surfaces intersection in parametric and implicit form: implicit form is a little slower
                                    ////    for (int ii = 0; ii < 1000; ii++)
                                    ////    {
                                    ////        ip = closePoint;
                                    ////        BoxedSurfaceExtension.SurfacesIntersectionLM(implicitSurface1, implicitSurface2, implicitSurface3, ref ip);
                                    ////    }
                                    ////    for (int ii = 0; ii < 1000; ii++)
                                    ////    {
                                    ////        GeoPoint2D uv11 = dsc.Surface1.PositionOf(curve.PointAt(tStart));
                                    ////        GeoPoint2D uv22 = dsc.Surface2.PositionOf(curve.PointAt(tStart));
                                    ////        ip = closePoint;
                                    ////        BoxedSurfaceExtension.SurfacesIntersectionLM(dsc.Surface1, dsc.Surface2, surface, ref uv11, ref uv22, ref uvStart, ref ip);
                                    ////    }
                                    ////}
                                    GeoPoint2D uv1 = dsc.Surface1.PositionOf(curve.PointAt(tStart));
                                    GeoPoint2D uv2 = dsc.Surface2.PositionOf(curve.PointAt(tStart));
                                    ip = closePoint;
                                    if (BoxedSurfaceExtension.SurfacesIntersectionLM(dsc.Surface1, dsc.Surface2, surface, ref uv1, ref uv2, ref uvStart, ref ip))
                                    {
                                        luOnCurve.Add(curve.PositionOf(ip));
                                        luvOnFace.Add(uvStart);
                                        lips.Add(ip);
                                        found = true;
                                    }
                                }
                                // TODO: introduce flags to indicate whether surface or curve provide 2nd derivative
                                if (!found && BoxedSurfaceExtension.CurveSurfaceIntersection(surface, curve, ref uvStart, ref tStart, out ip))
                                {
                                    if (cubes[j].uvPatch.Contains(uvStart) && th.TetraederParams[i] <= tStart && tStart <= th.TetraederParams[i + 1])
                                    {
                                        luOnCurve.Add(tStart);
                                        luvOnFace.Add(uvStart);
                                        lips.Add(ip);
                                        found = true;
                                    }
                                }
                                //if (BoxedSurfaceExtension.CurveSurfaceIntersection(surface, curve, cubes[j].uvPatch, th.TetraederParams[i], th.TetraederParams[i + 1], ref uvStart, ref tStart, out ip))
                                //{
                                //    // Performance test: almost the same, CurveSurfaceIntersectionwith the TrustRegionNewtonCGMinimizer is a little slower than LevenbergMarquardtMinimizer
                                //    // TrustRegionDogLegMinimizer and TrustRegionNewtonCGMinimizer are about the same
                                //    for (int ii = 0; ii < 1000; ii++)
                                //    {
                                //        uvStart = cubes[j].uvPatch.GetCenter();
                                //        tStart = (th.TetraederParams[i] + th.TetraederParams[i + 1]) / 2;
                                //        BoxedSurfaceExtension.CurveSurfaceIntersection(surface, curve, cubes[j].uvPatch, th.TetraederParams[i], th.TetraederParams[i + 1], ref uvStart, ref tStart, out ip);
                                //    }
                                //    for (int ii = 0; ii < 1000; ii++)
                                //    {
                                //        uvStart = cubes[j].uvPatch.GetCenter();
                                //        tStart = (th.TetraederParams[i] + th.TetraederParams[i + 1]) / 2;
                                //        BoxedSurfaceExtension.CurveSurfaceIntersectionDL(surface, curve, cubes[j].uvPatch, th.TetraederParams[i], th.TetraederParams[i + 1], ref uvStart, ref tStart, out ip);
                                //    }
                                //}
                                if (!found && BoxedSurfaceExtension.CurveSurfaceIntersectionLM(surface, curve, ref uvStart, ref tStart, out ip))
                                {
                                    if (cubes[j].uvPatch.Contains(uvStart) && th.TetraederParams[i] <= tStart && tStart <= th.TetraederParams[i + 1])
                                    {
                                        luOnCurve.Add(tStart);
                                        luvOnFace.Add(uvStart);
                                        lips.Add(ip);
                                        found = true;
                                    }
                                }
                                if (!found) GetCurveIntersection(curve, th.TetraederParams[i], th.TetraederParams[i + 1], cubes[j], lips, luvOnFace, luOnCurve);
                            }
                        }
                    }
                }
            }
            bool startPointIncluded = false;
            bool endPointIncluded = false;
            for (int i = lips.Count - 1; i > 0; --i)
            {   // doppelt gefundene Punkte eliminieren, epsilon ist ein problem!
                if (Math.Abs(luOnCurve[i] - luOnCurve[i - 1]) < 1e-8)
                {
                    lips.RemoveAt(i);
                    luOnCurve.RemoveAt(i);
                    luvOnFace.RemoveAt(i);
                }
            }
            for (int i = 0; i < lips.Count; ++i)
            {   // doppelt gefundene Punkte eliminieren, epsilon ist ein problem!
                if (Math.Abs(luOnCurve[i]) < 1e-5) startPointIncluded = true;
                if (Math.Abs(1.0 - luOnCurve[i]) < 1e-5) endPointIncluded = true;
            }
            if (!startPointIncluded)
            {
                GeoPoint2D[] ft = surface.PerpendicularFoot(curve.StartPoint);
                for (int i = 0; i < ft.Length; i++)
                {
                    if ((surface.PointAt(ft[i]) | curve.StartPoint) < Precision.eps)
                    {
                        lips.Add(curve.StartPoint);
                        luOnCurve.Add(0.0);
                        luvOnFace.Add(ft[i]);
                        break;
                    }
                }
            }
            if (!endPointIncluded)
            {
                GeoPoint2D[] ft = surface.PerpendicularFoot(curve.EndPoint);
                for (int i = 0; i < ft.Length; i++)
                {
                    if ((surface.PointAt(ft[i]) | curve.EndPoint) < Precision.eps)
                    {
                        lips.Add(curve.EndPoint);
                        luOnCurve.Add(1.0);
                        luvOnFace.Add(ft[i]);
                        break;
                    }
                }
            }
            uvOnFaces = luvOnFace.ToArray();
            ips = lips.ToArray();
            uOnCurve3Ds = luOnCurve.ToArray();
            for (int i = 0; i < uvOnFaces.Length; i++)
            {
                SurfaceHelper.AdjustPeriodic(surface, uvbounds, ref uvOnFaces[i]);
            }
        }

        private void GetCurveIntersection(ISimpleCurve curve, ParEpi cube, List<GeoPoint> lips, List<GeoPoint2D> luvOnFace, List<double> luOnCurve)
        {
            GeoPoint ip;
            GeoPoint2D uv;
            double u;
            switch (NewtonCurveIntersection(curve, cube, out ip, out uv, out u))
            {
                case CurveIntersectionMode.simpleIntersection:
                    {
                        lips.Add(ip);
                        luvOnFace.Add(uv);
                        luOnCurve.Add(u);
                    }
                    break;
                case CurveIntersectionMode.noIntersection:
                    {   // wenn Newton keinen Schnittpunkt findet, dann kann es mehrere Gründe haben (die man vielleicht unterscheiden sollte)
                        // 1. Newton läuft aus dem Patch heraus. Newton würde z.B. hin und her pendeln wenn der Schnittpunkt an einem Wendepunkt
                        // liegt. Vielleicht sollte man den Patch etwas größer ansetzen, damit solche Fälle glatt laufen
                        // 2. Newton konvergiert nicht. Das passoert nur, wenn man zu weit vom Schnittpunkt weg ist. Dann also unterteilen.
                        // 3. Es gibt keinen Schnittpunkt. Dann braucht man auch nicht aufteilen.
                        // Die Fälle sind allerdings nicht leicht zu unterscheiden, deshalb hier einfach mal auf 1/100 der Maximalgröße 
                        // abprüfen und dann abbrechen. Das ist aber nicht sehr effektiv
                        if (cube.Size > octtree.Extend.Size / 100)
                        {
                            ParEpi[] splitted = SplitCube(cube);
                            for (int i = 0; i < splitted.Length; ++i)
                            {
                                BoundingCube bc = splitted[i].BoundingCube;
                                // der Test ist leider recht mager, aber die Kurve mit ToUnit zu modifizieren und dann zu testen ist 
                                // zu aufwendig. Vielleicht extra interface, welches den Test mit Parallelepiped zulässt machen
                                if ((curve as IOctTreeInsertable).HitTest(ref bc, 0.0))
                                {
                                    GetCurveIntersection(curve, splitted[i], lips, luvOnFace, luOnCurve);
                                }
                            }
                        }
                    }
                    break;
                case CurveIntersectionMode.curveInSurface:
                    // hier keine Punkte hinzuufügen
                    break;
            }

        }
        private enum CurveIntersectionMode { noIntersection, simpleIntersection, curveInSurface, tangential }
        private CurveIntersectionMode NewtonCurveIntersection(ISimpleCurve curve, ParEpi cube, out GeoPoint ip, out GeoPoint2D uv, out double u)
        {
            ICurve icurve = curve as ICurve;
            uv = cube.uvPatch.GetCenter();
            u = 0.5; // in der Mitte, unwichtig
                     // BoxedSurfaceExtension.CurveSurfaceIntersection(surface, curve as ICurve, ref uv, ref u);
            GeoVector udir = surface.UDirection(uv);
            GeoVector vdir = surface.VDirection(uv); // die müssen auch von der Länge her stimmen!
            GeoPoint loc = surface.PointAt(uv);
            GeoPoint curvepoint = icurve.PointAt(u);
            double error = curvepoint | loc;
            int errorcount = 0;
#if DEBUG
            //DebuggerContainer dc = new DebuggerContainer();
            //try
            //{
            //    Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(cube.uvPatch));
            //    dc.Add(fc);
            //}
            //catch (Polyline2DException) { }
            //dc.Add(curve as IGeoObject);
#endif
            ip = new GeoPoint(loc, curvepoint);
            try
            {
                while (error > 0) //  Math.Max(Precision.eps, loc.Size * 1e-6))
                {
                    double[] pars = curve.GetPlaneIntersection(new Plane(loc, udir, vdir));
                    if (pars.Length == 0)
                    {
                        if (CheckCurveInSurface(icurve, 0.0, 1.0, cube, u, uv))
                        {
                            return CurveIntersectionMode.curveInSurface;
                        }
                        else
                        {
                            return CurveIntersectionMode.noIntersection;
                        }
                    }
                    double bestDistance = double.MaxValue;
                    for (int i = 0; i < pars.Length; ++i)
                    {
                        GeoPoint cp = icurve.PointAt(pars[i]);
                        double d = loc | cp;
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            curvepoint = cp;
                            u = pars[i];
                        }
                    }

                    //Matrix m = Matrix.RowVector(udir, vdir, udir ^ vdir);
                    Matrix m = DenseMatrix.OfColumnArrays(udir, vdir, icurve.DirectionAt(u));
                    Vector s = (Vector)m.Solve(new DenseVector(curvepoint - loc));
                    if (s.IsValid())
                    {
                        double du = s[0];
                        double dv = s[1];
                        uv.x += du;
                        uv.y += dv;
                        loc = surface.PointAt(uv);
                        double e = loc | curvepoint;
                        if (e >= error / 2.0)
                        {
                            if (e < Math.Max(Precision.eps, loc.Size * 1e-6))
                            {   // Ziel erreicht, genauer gehts nicht
                                break;
                            }
                            ++errorcount;
                            // manchmal machts einen kleinen Schlenker bevor es konvergiert, dann nicht gleich abbrechen
                            if (errorcount > 5) // konvergiert nicht, Patch aufteilen
                            {
                                if (CheckCurveInSurface(icurve, 0.0, 1.0, cube, u, uv))
                                {
                                    return CurveIntersectionMode.curveInSurface;
                                }
                                else
                                {
                                    return CurveIntersectionMode.noIntersection;
                                }
                            }
                        }
                        else
                        {
                            error = e;
                        }
                        if (!cube.uvPatch.ContainsEps(uv, -0.1))
                        {
                            ++errorcount;
                            // manchmal machts einen kleinen Schlenker bevor es konvergiert, dann nicht gleich abbrechen
                            if (errorcount > 5) // konvergiert nicht, Patch aufteilen
                            {
                                if (CheckCurveInSurface(icurve, 0.0, 1.0, cube, u, uv))
                                {
                                    return CurveIntersectionMode.curveInSurface;
                                }
                                else
                                {
                                    return CurveIntersectionMode.noIntersection;
                                }
                            }
                        }
                    }
                    else
                    {   // singuläre matrix
                        //if (CheckCurveInSurface(icurve, 0.0, 1.0, cube, u, uv))
                        //{
                        //    return CurveIntersectionMode.curveInSurface;
                        //}
                        //else
                        //{
                        //    return CurveIntersectionMode.noIntersection;
                        //}
                        return CurveIntersectionMode.noIntersection;
                    }
                    udir = surface.UDirection(uv);
                    vdir = surface.VDirection(uv);
                }
            }
            catch
            {
                return CurveIntersectionMode.noIntersection;
            }
            if (errorcount > 0 && !cube.uvPatch.ContainsEps(uv, -0.1)) return CurveIntersectionMode.noIntersection;
            ip = new GeoPoint(loc, curvepoint);
            return CurveIntersectionMode.simpleIntersection;
        }
        private CurveIntersectionMode NewtonCurveIntersection(ICurve curve, double spar, double epar, ParEpi cube, out GeoPoint ip, out GeoPoint2D uv, out double u)
        {
            if (epar <= spar)
            {
                ip = GeoPoint.Origin;
                uv = GeoPoint2D.Origin;
                u = 0.0;
                return CurveIntersectionMode.noIntersection;
            }
            // bei den Kurven, die leicht einen exakten Schnitt mit einer Ebene bestimmen können, also Linie, Kreis, Ellipse
            // sollte hier nicht mit der Tangente gearbeitet werden sondern mit der echten Kurve (bei Linie isses egal)
            uv = cube.uvPatch.GetCenter();
            u = (spar + epar) / 2.0;
            if (!cube.Contains(curve.PointAt(u)))
            {   // es kommt vor, dass der Anfangspunkt der Kurve (Kurve sehr lang im Vergleich zum cube) zu schlecht ist
                u = curve.PositionOf(cube.GetCenter());
            }
            GeoVector udir = surface.UDirection(uv);
            GeoVector vdir = surface.VDirection(uv); // die müssen auch von der Länge her stimmen!
            GeoPoint loc = surface.PointAt(uv);
            GeoVector curvedir = curve.DirectionAt(u);
            GeoPoint curvepoint = curve.PointAt(u);
            double error = curvepoint | loc;
            int errorcount = 0;
            bool firstpass = true;
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            try
            {
                Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(cube.uvPatch));
                dc.Add(fc);
            }
            catch (Polyline2DException) { }
            ICurve trcurve = curve.Clone();
            trcurve.Trim(spar, epar);
            dc.Add(trcurve as IGeoObject);
            dc.Add(curvepoint, Color.Red, 0);
            dc.Add(loc, Color.Green, 1);
#endif
            ip = new GeoPoint(loc, curvepoint);
            while (error > 0) // Math.Max(Precision.eps, loc.Size * 1e-6))
            {
                double tan = (udir ^ vdir).Normalized * curvedir.Normalized;
                if (Math.Abs(tan) < 0.01) return CurveIntersectionMode.tangential;
                Matrix m = DenseMatrix.OfColumnArrays(udir, vdir, curvedir);
                Vector s = (Vector)m.Solve(new DenseVector(curvepoint - loc));
                if (s.IsValid())
                {
                    double du = s[0];
                    double dv = s[1];
                    double dcurve = s[2];
                    uv.x += du; // oder -=
                    uv.y += dv; // oder -=
                    loc = surface.PointAt(uv);
                    u -= dcurve; // oder -=?
                    curvepoint = curve.PointAt(u);
                    double e = loc | curvepoint;
                    if (e >= error / 2.0 && !firstpass) // konvergiert nicht gut, ist aber selten
                    {
                        if (e < Math.Max(Precision.eps, loc.Size * 1e-10))
                        {   // ziel erreicht, genauer gehts nicht
                            error = e;
                            break;
                        }
                        ++errorcount;
                        // manchmal machts einen kleinen Schlenker bevor es konvergiert, dann nicht gleich abbrechen
                        if (errorcount > 5) // konvergiert nicht, Patch aufteilen
                            if (CheckCurveInSurface(curve, spar, epar, cube, u, uv))
                            {
                                return CurveIntersectionMode.curveInSurface;
                            }
                            else
                            {
                                return CurveIntersectionMode.noIntersection;
                            }
                    }
                    else
                    {
                        error = e;
                        firstpass = false;
                    }
                    if (!cube.uvPatch.ContainsEps(uv, -0.1) || u < spar || u > epar)
                    {
                        ++errorcount;
                        // manchmal machts einen kleinen Schlenker bevor es konvergiert, dann nicht gleich abbrechen
                        if (errorcount > 5) // konvergiert nicht, Patch aufteilen
                        {
                            if (CheckCurveInSurface(curve, spar, epar, cube, u, uv))
                            {
                                return CurveIntersectionMode.curveInSurface;
                            }
                            else
                            {
                                return CurveIntersectionMode.noIntersection;
                            }
                        }
                    }
                }
                else
                {   // singuläre matrix
                    return CurveIntersectionMode.noIntersection;
                }
                udir = surface.UDirection(uv);
                vdir = surface.VDirection(uv);
                curvedir = curve.DirectionAt(u);
            }
            if (errorcount > 0 && (!cube.uvPatch.ContainsEps(uv, -0.1) || u < spar || u > epar)) return CurveIntersectionMode.noIntersection;
            ip = new GeoPoint(loc, curvepoint);
            return CurveIntersectionMode.simpleIntersection;
        }

        internal static bool NewtonCurveIntersection(ICurve curve, ISurface surface, BoundingRect bounds, ref GeoPoint ip, out GeoPoint2D uv, out double u)
        {
            // bei den Kurven, die leicht einen exakten Schnitt mit einer Ebene bestimmen können, also Linie, Kreis, Ellipse
            // sollte hier nicht mit der Tangente gearbeitet werden sondern mit der echten Kurve (bei Linie isses egal)
            uv = surface.PositionOf(ip);
            u = curve.PositionOf(ip);
            GeoVector udir = surface.UDirection(uv);
            GeoVector vdir = surface.VDirection(uv); // die müssen auch von der Länge her stimmen!
            GeoPoint loc = surface.PointAt(uv);
            GeoVector curvedir = curve.DirectionAt(u);
            GeoPoint curvepoint = curve.PointAt(u);
            double error = curvepoint | loc;
#if DEBUG
            {
                DebuggerContainer dc = new DebuggerContainer();
                try
                {
                    Face fc = Face.MakeFace(surface, new CADability.Shapes.SimpleShape(bounds));
                    dc.Add(fc);
                }
                catch (Polyline2DException) { }
                dc.Add(curve as IGeoObject);
            }
#endif
            ip = new GeoPoint(loc, curvepoint);
            while (error > 0)
            {
                Matrix m = DenseMatrix.OfColumnArrays(udir, vdir, curvedir);
                Vector s = (Vector)m.Solve(new DenseVector(curvepoint - loc));
                if (s.IsValid())
                {
                    double du = s[0];
                    double dv = s[1];
                    double dcurve = s[2];
                    uv.x += du; // oder -=
                    uv.y += dv; // oder -=
                    loc = surface.PointAt(uv);
                    u -= dcurve; // oder -=?
                    curvepoint = curve.PointAt(u);
                    double e = loc | curvepoint;
                    if (e >= error / 2.0) // damit es zu Ende kommt
                    {
                        break;
                    }
                    error = e;
                }
                else
                {   // singuläre matrix
                    //if (CheckCurveInSurface(curve, spar, epar, cube, u, uv))
                    //{
                    //    return CurveIntersectionMode.curveInSurface;
                    //}
                    //else
                    //{
                    //    return CurveIntersectionMode.noIntersection;
                    //}
                    break;
                }
                udir = surface.UDirection(uv);
                vdir = surface.VDirection(uv);
                curvedir = curve.DirectionAt(u);
            }
            if (error < Precision.eps)
            {
                ip = new GeoPoint(loc, curvepoint);
                return true;
            }
            return false;
        }

        private bool CheckCurveInSurface(ICurve curve, double spar, double epar, ParEpi cube, double u, GeoPoint2D uv)
        {   // das Problem: stelle fest, ob die Kurve in der Fläche liegt. Natürlich kann die Kurve auch aus der Fläche herausragen
            // und das macht ein Problem. Vielleicht mit GetNaturalBounds arbeiten? Wie ist PositionOf definiert, wenn der Punkt 
            // neben der Fläche liegt?
            double umin, umax, vmin, vmax;
            surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
            if (umin == double.MinValue || umax == double.MaxValue)
            {
                umin = uvbounds.Left;
                umax = uvbounds.Right;
            }
            if (vmin == double.MinValue || vmax == double.MaxValue)
            {
                vmin = uvbounds.Bottom;
                vmax = uvbounds.Top;
            }

            List<double> checkBetween = new List<double>(); // zwei Punkte auf der Kurve, mit denen wir überprüfen wollen, die 
                                                            // also potentiell auf der Fläche liegen
            GeoPoint sp = curve.PointAt(spar);
            if (IsClose(sp))
            {
                GeoPoint2D uv1 = surface.PositionOf(sp);
                if (Precision.IsEqual(surface.PointAt(uv1), sp))
                    checkBetween.Add(spar);
            }
            GeoPoint ep = curve.PointAt(epar);
            if (IsClose(ep))
            {
                GeoPoint2D uv1 = surface.PositionOf(ep);
                if (Precision.IsEqual(surface.PointAt(uv1), ep))
                    checkBetween.Add(epar);
            }
            if (checkBetween.Count < 2)
            {
                // also nicht die ganze Kurve liegt drin, sondern höchstens ein Punkt
                double[] par1;
                double[] par2;
                GeoPoint[] ip;
                ICurve frame = surface.FixedU(umin, vmin, vmax);
                if (!IsUSingularity(umin))
                {
                    Curves.Intersect(frame, curve, out par1, out par2, out ip);
                    if (par2.Length > 0)
                    {
                        checkBetween.AddRange(par2);
                    }
                }
                if (!IsUSingularity(umax))
                {
                    frame = surface.FixedU(umax, vmin, vmax);
                    Curves.Intersect(frame, curve, out par1, out par2, out ip);
                    if (par2.Length > 0)
                    {
                        checkBetween.AddRange(par2);
                    }
                }
                if (!IsVSingularity(vmin))
                {
                    frame = surface.FixedV(vmin, umin, umax);
                    Curves.Intersect(frame, curve, out par1, out par2, out ip);
                    if (par2.Length > 0)
                    {
                        checkBetween.AddRange(par2);
                    }
                }
                if (!IsVSingularity(umax))
                {
                    frame = surface.FixedV(vmax, umin, umax);
                    Curves.Intersect(frame, curve, out par1, out par2, out ip);
                    if (par2.Length > 0)
                    {
                        checkBetween.AddRange(par2);
                    }
                }
            }
            if (checkBetween.Count < 2) return false; // die Kurve liegt bicht drin
            checkBetween.Sort();
            for (int i = checkBetween.Count - 1; i > 0; --i)
            {
                if (checkBetween[i] - checkBetween[i - 1] < 1e-6) checkBetween.RemoveAt(i);
            }
            if (checkBetween.Count < 2) return false; // die Kurve liegt bicht drin

            double tu = checkBetween[0];
            GeoPoint p = curve.PointAt(tu);
            uv = surface.PositionOf(p);
            if (!Precision.IsPerpendicular(curve.DirectionAt(tu), surface.GetNormal(uv), false)) return false;
            tu = checkBetween[checkBetween.Count - 1];
            p = curve.PointAt(tu);
            uv = surface.PositionOf(p);
            if (!Precision.IsPerpendicular(curve.DirectionAt(tu), surface.GetNormal(uv), false)) return false;
            return true; // zwei Punkte und deren Tangente getestet
        }

        private void GetCurveIntersection(ICurve curve, double spar, double epar, ParEpi cube, List<GeoPoint> lips, List<GeoPoint2D> luvOnFace, List<double> luOnCurve)
        {   // versucht mit Newtonverfahren einen Schnittpunkt zu finden, wenn nicht, dann wird gesplittet (sowohl Kurve als auch Fläche)
            // und bei Interference von Tetraeder mit Würfel nochmal probiert
            GeoPoint ip;
            GeoPoint2D uv;
            double u;
            if (curve is IDualSurfaceCurve)
            {
                IDualSurfaceCurve dsc = (curve as IDualSurfaceCurve);
                BoundingRect ext1 = BoundingRect.InfinitBoundingRect;
                dsc.Surface1.GetNaturalBounds(out ext1.Left, out ext1.Right, out ext1.Bottom, out ext1.Top);
                BoundingRect ext2 = BoundingRect.InfinitBoundingRect;
                dsc.Surface2.GetNaturalBounds(out ext2.Left, out ext2.Right, out ext2.Bottom, out ext2.Top);
                // die ext werden garnicht verwendet
                // wenn es mehrere Schnittpunkte der surface zwischen spar und epar gibt, dann haben wir hier ein Problem:
                // der Anfangspunkt muss stimmen
                GeoPoint sp = curve.PointAt(spar);
                GeoPoint ep = curve.PointAt(epar);
                if (cube.ClipLine(ref sp, ref ep))
                {
                    ip = new GeoPoint(sp, ep); // Mittelpunkt auf dem geclippten Stück
                }
                else
                {
                    GeoPoint mp = Geometry.DropPL(cube.GetCenter(), sp, ep);
                    double lpos = Geometry.LinePar(sp, ep, mp);
                    if (lpos >= 0 && lpos <= 1)
                    {
                        double mpar = spar + lpos * (epar - spar);
                        ip = curve.PointAt(mpar);
                    }
                    else
                    {
                        ip = new GeoPoint(surface.PointAt(cube.uvPatch.GetCenter()), curve.PointAt((spar + epar) / 2.0)); // Mittelpunkt der kurve und Mittelpunkt des uvPatches
                    }
                }
                GeoPoint2D uv1, uv2, uv3;
                if (Surfaces.NewtonIntersect(this.surface, cube.uvPatch, dsc.Surface1, ext1, dsc.Surface2, ext2, ref ip, out uv1, out uv2, out uv3))
                {
                    if (cube.Contains(ip))
                    {
                        lips.Add(ip);
                        luvOnFace.Add(uv1);
                        luOnCurve.Add(curve.PositionOf(ip));
                        return; // gibt es ein Problem, wenn es 2 Schnittpunkt gibt?
                                // also: gewissermaßen ja: ein ParEpi und ein Tetraeder sollten immer nur einen Schnittpunkt enthalten
                                // deshalb wurde hier derTest eingeführt, ob der cube auch den Schnittpunkt enthält, denn wenn nicht, dann sind wir möglicherweise aus dem cube rausgerutscht,
                                // obwohl darin doch noch ein Schnittpunkt war. Aber durch das nun folgende aufteilen finden wir dann den richtigen
                    }
                }
            }
            if ((!cube.Contains(curve.PointAt(spar)) || !cube.Contains(curve.PointAt(epar))) && !(curve is Line) && curve is IExplicitPCurve3D)
            {
                // Kurve erstmal so kürzen, dass sie ganz in cube liegt. 
                GeoPoint[] loc;
                GeoVector[] dirx, diry;
                cube.GetPlanes(out loc, out dirx, out diry); // das sind die 6 Seiten als Faces mit PlaneSurface und SimpleShape
                ExplicitPCurve3D excrv = (curve as IExplicitPCurve3D).GetExplicitPCurve3D();
                List<double> planeIntersectionParams = new List<double>();
                for (int i = 0; i < loc.Length; i++)
                {
                    double[] ips = excrv.GetPlaneIntersection(loc[i], dirx[i], diry[i]);
                    for (int j = 0; j < ips.Length; j++)
                    {
                        GeoPoint p = excrv.PointAt(ips[j]);
                        Matrix m = DenseMatrix.OfRowArrays(dirx[i], diry[i], dirx[i] ^ diry[i]);
                        Vector mres = (Vector)m.Transpose().Solve(new DenseVector(p - loc[i]));
                        if (mres.IsValid())
                        {
                            double x = mres[0];
                            double y = mres[1]; // mres[2, 0] muss ja 0 sein
                            if (x >= 0 && x <= 1 && y >= 0 && y <= 1)
                            {
                                // eintretender oder austretender Schnitt? Wenn die Seiten richtig orientiert wären, wäre das einfach
                                planeIntersectionParams.Add(curve.PositionOf(p)); // curve und excrv haben nicht die gleichen Parameter
                            }
                        }
                    }
                }
                // welcher Abschnitt gilt jetzt
                if (planeIntersectionParams.Count > 0)
                {
                    planeIntersectionParams.Sort();
                    if (spar < planeIntersectionParams[0]) planeIntersectionParams.Insert(0, spar);
                    if (epar > planeIntersectionParams[planeIntersectionParams.Count - 1]) planeIntersectionParams.Add(epar);
                    for (int i = 0; i < planeIntersectionParams.Count - 1; i++)
                    {
                        GeoPoint p = curve.PointAt((planeIntersectionParams[i] + planeIntersectionParams[i + 1]) / 2.0);
                        if (cube.Contains(p))
                        {
                            if (planeIntersectionParams[i] == spar)
                            {
                                epar = planeIntersectionParams[i + 1];
                                break;
                            }
                            else if (planeIntersectionParams[i + 1] == epar)
                            {
                                spar = planeIntersectionParams[i];
                                break;
                            }
                            else
                            {
                                spar = planeIntersectionParams[i];
                                epar = planeIntersectionParams[i + 1];
                                break;
                            }
                        }
                    }
                }
            }
            switch (NewtonCurveIntersection(curve, spar, epar, cube, out ip, out uv, out u))
            {
                case CurveIntersectionMode.simpleIntersection:
                    {
                        if (cube.Contains(ip))
                        {
                            lips.Add(ip);
                            luvOnFace.Add(uv);
                            luOnCurve.Add(u);
                            return;
                        }
                    }
                    break;
                case CurveIntersectionMode.noIntersection:
                    {
                    }
                    break;
                case CurveIntersectionMode.curveInSurface:
                    // hier keine Punkte hinzuufügen
                    return;
                case CurveIntersectionMode.tangential:
                    {   // Bisection mit spar, epar
                        GeoPoint sp = curve.PointAt(spar);
                        GeoPoint ep = curve.PointAt(epar);
                        double sd = MinDist(sp);
                        double ed = MinDist(ep);
                        if (sd == double.MaxValue || ed == double.MaxValue) break;
                        GeoPoint mp;
                        while (Math.Sign(sd) != Math.Sign(ed))
                        {
                            if (sd == 0.0)
                            {
                                lips.Add(sp);
                                luvOnFace.Add(surface.PositionOf(sp));
                                luOnCurve.Add(spar);
                                break;
                            }
                            else if (ed == 0.0)
                            {
                                lips.Add(sp);
                                luvOnFace.Add(surface.PositionOf(sp));
                                luOnCurve.Add(spar);
                                break;
                            }
                            else if (epar - spar < 1e-6)
                            {
                                mp = new GeoPoint(sp, ep);
                                GeoPoint mps = surface.PointAt(surface.PositionOf(mp));
                                if ((mp | mps) < Precision.eps)
                                {
                                    lips.Add(mp);
                                    luvOnFace.Add(surface.PositionOf(mp));
                                    luOnCurve.Add((spar + epar) / 2.0);
                                }
                                break;
                            }
                            double mpar = (spar + epar) / 2.0;
                            mp = curve.PointAt(mpar);
                            double ms = MinDist(mp);
                            if (Math.Sign(ms) != Math.Sign(sd))
                            {
                                ed = ms;
                                epar = mpar;
                                ep = mp;
                            }
                            else
                            {
                                sd = ms;
                                spar = mpar;
                                sp = mp;
                            }
                        }
                    }
                    return;
            }
            // nichts gefunden, also splitten
            if (cube.Size > octtree.Extend.Size / 100)
            {   // siehe case CurveIntersectionMode.noIntersection von GetCurveIntersection(ISimpleCurve curve
                ParEpi[] splitted = SplitCube(cube);
                GeoPoint p1 = curve.PointAt(spar);
                GeoPoint p2 = curve.PointAt(epar);
                GeoPoint tv1, tv2, tv3, tv4, pm;
                double parm;
                TetraederHull.SplitTetraeder(curve, p1, p2, spar, epar, out pm, out parm, out tv1, out tv2, out tv3, out tv4);
                for (int i = 0; i < splitted.Length; ++i)
                {
                    if (splitted[i].Interferes(p1, pm, tv1, tv2))
                    {
                        if (splitted[i].Interferes(curve, spar, parm, p1, pm, tv1, tv2))
                        {
                            GetCurveIntersection(curve, spar, parm, splitted[i], lips, luvOnFace, luOnCurve);
                        }
                    }
                    if (splitted[i].Interferes(pm, p2, tv3, tv4))
                    {
                        if (splitted[i].Interferes(curve, parm, epar, pm, p2, tv3, tv4))
                        {
                            GetCurveIntersection(curve, parm, epar, splitted[i], lips, luvOnFace, luOnCurve);
                        }
                    }
                }
            }

        }

        private double MinDist(GeoPoint p)
        {
            GeoPoint2D[] fps = surface.PerpendicularFoot(p); // PerpendicularFoot hier implementieren und cube in Betracht ziehen
            double mindist = double.MaxValue;
            GeoPoint2D bestUv = GeoPoint2D.Origin;
            GeoPoint bestPoint = GeoPoint.Origin;
            for (int i = 0; i < fps.Length; i++)
            {
                GeoPoint p0 = surface.PointAt(fps[i]);
                double d = p0 | p;
                if (d < mindist)
                {
                    mindist = d;
                    bestUv = fps[i];
                    bestPoint = p0;
                }
            }
            if (mindist < double.MaxValue)
            {
                GeoVector nor = surface.GetNormal(bestUv);
                if (((p - bestPoint) * nor) > 0)
                {
                    return mindist;
                }
                else
                {
                    return -mindist;
                }
            }
            return double.MaxValue; // sollte nicht vorkommen
        }

        private bool PositionOfWithFixedCurves(GeoPoint p3d, ParEpi found, out GeoPoint2D res)
        {
            res = found.uvPatch.GetCenter();
            GeoVector dirx = surface.UDirection(res);
            GeoVector diry = surface.VDirection(res);
            GeoVector dirz = dirx ^ diry; // die Normale
            GeoPoint loc = surface.PointAt(res);
            BoundingRect natbound = new BoundingRect();
            surface.GetNaturalBounds(out natbound.Left, out natbound.Right, out natbound.Bottom, out natbound.Top);
            BoundingRect natbound2 = natbound;
            natbound2.Inflate(natbound.Width, natbound.Height);
            // kann auch unendlich (double.MinValue/MaxValue) werden, macht aber nix...
            double mindist = Geometry.DistPL(p3d, loc, dirz);
            int missed = 0;
            bool acceptDiverge = true;
            bool fixedu = true;
            while (mindist > Precision.eps) // war *100 (18.7.14)
            {
                if (fixedu)
                {
                    double u = (res.x - natbound.Left) / natbound.Width;
                    ICurve crv = surface.FixedU(u, natbound.Bottom, natbound.Top);
                    double v = crv.PositionOf(p3d);
                    res.y = crv.PositionToParameter(v);
                    fixedu = false;
                }
                else
                {
                    double v = (res.y - natbound.Bottom) / natbound.Height;
                    ICurve crv = surface.FixedV(v, natbound.Left, natbound.Right);
                    double u = crv.PositionOf(p3d);
                    res.x = crv.PositionToParameter(u);
                    fixedu = true;
                }
                if (!found.uvPatch.ContainsEps(res, -0.001))
                {
                    ++missed;
                    // versuchsweise auch nach außen laufen lassen
                    //if (missed > 2) return false;
                    if (!natbound2.Contains(res)) return false;
                    if (!natbound.Contains(res)) // wieder eingeführt, da bei manchen NURBS Endlosschleife
                    {   // wenn ganz außerhalb, dann auf die Grenze setzen
                        // gut, solange es konvergiert
                        SurfaceHelper.AdjustPeriodic(surface, natbound, ref res);
                        if (res.x < natbound.Left) res.x = natbound.Left;
                        if (res.x > natbound.Right) res.x = natbound.Right;
                        if (res.y < natbound.Bottom) res.y = natbound.Bottom;
                        if (res.y > natbound.Top) res.y = natbound.Top;
                    }
                }
                else
                {
                    missed = 0; // damit kann es hin und her springen
                }
                dirx = surface.UDirection(res);
                diry = surface.VDirection(res);
                loc = surface.PointAt(res);
                dirz = dirx ^ diry; // die Normale
                double d = Geometry.DistPL(p3d, loc, dirz);
                if (d > mindist * 0.9)
                {
                    if (!acceptDiverge) return false; // konvergiert nicht oder schlecht "*0.9" hinzugefügt (18.7.14)
                    else
                    {
                        acceptDiverge = false; // einmal divergieren ist ok
                    }
                }
                mindist = d;
            }
            if (double.IsNaN(mindist)) return false;
            if (missed > 2)
            {
                BoundingRect brcopy = found.uvPatch;
                brcopy.Inflate(brcopy.Width / 100, brcopy.Height / 100);
                bool ok = brcopy.Contains(res);
                return ok;
            }
            return true;

        }

        /// <summary>
        /// Newton approximation of footpoint of p3d on this path of the surface.
        /// </summary>
        /// <param name="p3d"></param>
        /// <param name="found"></param>
        /// <param name="res"></param>
        /// <returns></returns>
        private bool PositionOf(GeoPoint p3d, ParEpi found, out GeoPoint2D res, out double mindist)
        {
#if DEBUGx
            // bool dbg = PositionOfWithFixedCurves(p3d, found, out res); 
            res = found.PositionOf(p3d, surface); // this is usually a good guess by the ParEpi
            if (!res.IsValid) res = found.uvPatch.GetCenter();
            GeoPoint2D start = res;
            var at = PerformanceTimer.AllTimers;
            using (new PerformanceTick("LM"))
            {
                for (int i = 0; i < 100; i++)
                {
                    res = start;
                    BoxedSurfaceExtension.PositionOfLM(surface, p3d, ref res, out double mdmn);
                }
            }
            using (new PerformanceTick("MN"))
            {
                for (int i = 0; i < 100; i++)
                {
                    res = start;
                    BoxedSurfaceExtension.PositionOfMN(surface, p3d, ref res, out double mdmn);
                }
            }
#endif
            // Suche vom u/v Mittelpunkt von found ausgehend mit dem Tangentenverfahren den Fußpunkt
            // wenn der patch verlassen wird, dann exception

            res = found.PositionOf(p3d, surface); // this is usually a good guess by the ParEpi
            if (!res.IsValid) res = found.uvPatch.GetCenter();
            GeoVector dirx = surface.UDirection(res);
            GeoVector diry = surface.VDirection(res);
            GeoVector dirz = dirx ^ diry; // die Normale
            GeoPoint loc = surface.PointAt(res);
            BoundingRect natbound = new BoundingRect();
            surface.GetNaturalBounds(out natbound.Left, out natbound.Right, out natbound.Bottom, out natbound.Top);
            BoundingRect natbound2 = natbound;
            natbound2.Inflate(natbound.Width, natbound.Height); // may also be infinite
            mindist = Geometry.DistPL(p3d, loc, dirz);
            int missed = 0;
            bool acceptDiverge = true;
            while (mindist > Math.Min(Precision.eps, found.Size * 1e-4)) // war *100 (18.7.14)
            {
                if (dirx.IsNullVector() || diry.IsNullVector() || dirz.IsNullVector()) return false;
                Matrix m = DenseMatrix.OfRowArrays(dirx, diry, dirz);
                Vector mres = (Vector)m.Transpose().Solve(new DenseVector(p3d - loc));
                if (!mres.IsValid()) return false;
                res.x += mres[0];
                res.y += mres[1];
                if (!found.uvPatch.ContainsEps(res, -0.05))
                {
                    ++missed;
                    // versuchsweise auch nach außen laufen lassen
                    //if (missed > 2) return false;
                    if (!natbound2.Contains(res) && missed > 1) return false; // too far outside
                                                                              // if the 2d point is outside the natural bounds we stopped the whole process, but I don't see a reason why, as long as it converges
                                                                              //if (!natbound.Contains(res)) // wieder eingeführt, da bei manchen NURBS Endlosschleife
                                                                              //{   // wenn ganz außerhalb, dann auf die Grenze setzen
                                                                              //    // gut, solange es konvergiert
                                                                              //    SurfaceHelper.AdjustPeriodic(surface, natbound, ref res);
                                                                              //    if (res.x < natbound.Left) res.x = natbound.Left;
                                                                              //    if (res.x > natbound.Right) res.x = natbound.Right;
                                                                              //    if (res.y < natbound.Bottom) res.y = natbound.Bottom;
                                                                              //    if (res.y > natbound.Top) res.y = natbound.Top;
                                                                              //}
                }
                else
                {
                    missed = 0; // damit kann es hin und her springen
                }
                dirx = surface.UDirection(res);
                diry = surface.VDirection(res);
                loc = surface.PointAt(res);
                dirz = dirx ^ diry; // die Normale
                double d = Geometry.DistPL(p3d, loc, dirz);
                if (d > mindist * 0.9)
                {
                    if (!acceptDiverge)
                    {
                        if (mindist < Precision.eps) break;
                        if (Math.Abs(mindist - d) < Precision.eps * 0.1) break; // doesn't change any more
                        return false; // konvergiert nicht oder schlecht "*0.9" hinzugefügt (18.7.14)
                    }
                    else
                    {
                        acceptDiverge = false; // einmal divergieren ist ok
                    }
                }
                mindist = d;
            }
            if (double.IsNaN(mindist)) return false;

            if (missed > 2)
            {   // need this case when an almost closed nurbs surface finds a wrong point on the other side of the domain (like in MO21775-001-00.stp)
                BoundingRect brcopy = found.uvPatch;
                brcopy.Inflate(brcopy.Width / 100, brcopy.Height / 100);
                bool ok = brcopy.Contains(res);
                return ok;
            }
            return true;
        }
        private void SplitCubes(ParEpi[] cubes)
        {
            for (int i = 0; i < cubes.Length; i++)
            {
                SplitCube(cubes[i]);
            }
        }
        private ParEpi[] SplitCube(ParEpi toSplit)
        {   // hier wird das Objekt selbst verfeinert
            lock (this)
            {
                octtree.RemoveObject(toSplit);
                quadtree.RemoveObject(toSplit);
                ParEpi[] subCubes = new ParEpi[4];
                subCubes[0] = new ParEpi();
                subCubes[1] = new ParEpi();
                subCubes[2] = new ParEpi();
                subCubes[3] = new ParEpi();
                // Beim Teilen ist streng darauf zu achten, dass die Grenzen von aneinander anliegenden Patches
                // identisch sind, also sich nicht durch Rundungsfehler unterscheiden
                double hcenter = (toSplit.uvPatch.Left + toSplit.uvPatch.Right) / 2.0;
                double vcenter = (toSplit.uvPatch.Bottom + toSplit.uvPatch.Top) / 2.0;
                subCubes[0].uvPatch = new BoundingRect(toSplit.uvPatch.Left, toSplit.uvPatch.Bottom, hcenter, vcenter);
                subCubes[1].uvPatch = new BoundingRect(toSplit.uvPatch.Left, vcenter, hcenter, toSplit.uvPatch.Top);
                subCubes[2].uvPatch = new BoundingRect(hcenter, toSplit.uvPatch.Bottom, toSplit.uvPatch.Right, vcenter);
                subCubes[3].uvPatch = new BoundingRect(hcenter, vcenter, toSplit.uvPatch.Right, toSplit.uvPatch.Top);
                for (int i = 0; i < 4; ++i)
                {
                    subCubes[i].pll = surface.PointAt(subCubes[i].uvPatch.GetLowerLeft());
                    subCubes[i].plr = surface.PointAt(subCubes[i].uvPatch.GetLowerRight());
                    subCubes[i].pul = surface.PointAt(subCubes[i].uvPatch.GetUpperLeft());
                    subCubes[i].pur = surface.PointAt(subCubes[i].uvPatch.GetUpperRight());
                    subCubes[i].nll = surface.GetNormal(subCubes[i].uvPatch.GetLowerLeft());
                    subCubes[i].nlr = surface.GetNormal(subCubes[i].uvPatch.GetLowerRight());
                    subCubes[i].nul = surface.GetNormal(subCubes[i].uvPatch.GetUpperLeft());
                    subCubes[i].nur = surface.GetNormal(subCubes[i].uvPatch.GetUpperRight());
                    subCubes[i].nll.NormIfNotNull();
                    subCubes[i].nlr.NormIfNotNull();
                    subCubes[i].nul.NormIfNotNull();
                    subCubes[i].nur.NormIfNotNull();
                    calcParallelEpiped(subCubes[i]);
                    //subCubes[i].boundingCube = surface.GetPatchExtent(new BoundingRect(subCubes[i].uvPatch.Left, subCubes[i].uvPatch.Bottom, subCubes[i].uvPatch.Right, subCubes[i].uvPatch.Top));
                    octtree.AddObject(subCubes[i]);
                    quadtree.AddObject(subCubes[i]);
                }
                return subCubes;
            }
        }
        private ParEpi[] SubCubes(ParEpi toSplit)
        {   // hier werden nur Teilwürfel geliefert, aber es wird nicht verfeinert
            ParEpi[] subCubes = new ParEpi[4];
            subCubes[0] = new ParEpi();
            subCubes[1] = new ParEpi();
            subCubes[2] = new ParEpi();
            subCubes[3] = new ParEpi();
            double hcenter = (toSplit.uvPatch.Left + toSplit.uvPatch.Right) / 2.0;
            double vcenter = (toSplit.uvPatch.Bottom + toSplit.uvPatch.Top) / 2.0;
            subCubes[0].uvPatch = new BoundingRect(toSplit.uvPatch.Left, toSplit.uvPatch.Bottom, hcenter, vcenter);
            subCubes[1].uvPatch = new BoundingRect(toSplit.uvPatch.Left, vcenter, hcenter, toSplit.uvPatch.Top);
            subCubes[2].uvPatch = new BoundingRect(hcenter, toSplit.uvPatch.Bottom, toSplit.uvPatch.Right, vcenter);
            subCubes[3].uvPatch = new BoundingRect(hcenter, vcenter, toSplit.uvPatch.Right, toSplit.uvPatch.Top);
            for (int i = 0; i < 4; ++i)
            {
                subCubes[i].pll = surface.PointAt(subCubes[i].uvPatch.GetLowerLeft());
                subCubes[i].plr = surface.PointAt(subCubes[i].uvPatch.GetLowerRight());
                subCubes[i].pul = surface.PointAt(subCubes[i].uvPatch.GetUpperLeft());
                subCubes[i].pur = surface.PointAt(subCubes[i].uvPatch.GetUpperRight());
                subCubes[i].nll = surface.GetNormal(subCubes[i].uvPatch.GetLowerLeft());
                subCubes[i].nlr = surface.GetNormal(subCubes[i].uvPatch.GetLowerRight());
                subCubes[i].nul = surface.GetNormal(subCubes[i].uvPatch.GetUpperLeft());
                subCubes[i].nur = surface.GetNormal(subCubes[i].uvPatch.GetUpperRight());
                subCubes[i].nll.NormIfNotNull();
                subCubes[i].nlr.NormIfNotNull();
                subCubes[i].nul.NormIfNotNull();
                subCubes[i].nur.NormIfNotNull();
                calcParallelEpiped(subCubes[i]);
                //subCubes[i].boundingCube = surface.GetPatchExtent(new BoundingRect(subCubes[i].uvPatch.Left, subCubes[i].uvPatch.Bottom, subCubes[i].uvPatch.Right, subCubes[i].uvPatch.Top));
            }
            return subCubes;
        }
#if DEBUG
        internal DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer dc = new DebuggerContainer();
                //double[] usteps, vsteps;
                //double umin, umax, vmin, vmax;
                //surface.GetNaturalBounds(out umin, out umax, out vmin, out vmax);
                //surface.GetSafeParameterSteps(umin, umax, vmin, vmax, out usteps, out vsteps);
                //ParEpi[] all = octtree.GetObjectsFromBox(surface.GetPatchExtent(new BoundingRect(usteps[0], vsteps[0], usteps[usteps.Length - 1], vsteps[vsteps.Length - 1])));
                ParEpi[] all = octtree.GetAllObjects();
                Layer transparent = new Layer("Transparent");
                transparent.Transparency = 100;
                Layer solid = new Layer("Solid");
                ColorDef cd = new ColorDef("Green", Color.Green);
                ColorDef cdr = new ColorDef("Red", Color.Red);
                for (int i = 0; i < all.Length; ++i)
                {
                    try
                    {
                        Solid sld = all[i].GetSolid();
                        sld.ColorDef = cd;
                        sld.Layer = transparent;
                        dc.Add(sld, all[i].id);
                        //dc.Add((all[i] as IDebuggerVisualizer).GetList()); // mit 4-Eck und Normalen
                    }
                    catch (ApplicationException) { }
                }
                double umin = uvbounds.Left;
                double umax = uvbounds.Right;
                double vmin = uvbounds.Bottom;
                double vmax = uvbounds.Top;
                if (surface is IRestrictedDomain restrictedDomain)
                {
                    int n = 10;
                    for (int i = 1; i < n; i++)
                    {
                        Line2D l2d = new Line2D(new GeoPoint2D(umin + i * (umax - umin) / n, vmin), new GeoPoint2D(umin + i * (umax - umin) / n, vmax));
                        double[] ips = restrictedDomain.Clip(l2d);
                        for (int j = 0; j < ips.Length; j += 2)
                        {
                            dc.Add(surface.Make3dCurve(l2d.Trim(ips[j], ips[j + 1])) as IGeoObject);
                        }
                        l2d = new Line2D(new GeoPoint2D(umin, vmin + i * (vmax - vmin) / n), new GeoPoint2D(umax, vmin + i * (vmax - vmin) / n));
                        ips = restrictedDomain.Clip(l2d);
                        for (int j = 0; j < ips.Length; j += 2)
                        {
                            dc.Add(surface.Make3dCurve(l2d.Trim(ips[j], ips[j + 1])) as IGeoObject);
                        }
                    }
                }
                else
                {
                    int n = 50;
                    for (int i = 0; i <= n; i++)
                    {   // über die Diagonale
                        GeoPoint[] pu = new GeoPoint[n + 1];
                        GeoPoint[] pv = new GeoPoint[n + 1];
                        for (int j = 0; j <= n; j++)
                        {
                            pu[j] = surface.PointAt(new GeoPoint2D(umin + j * (umax - umin) / n, vmin + i * (vmax - vmin) / n));
                            pv[j] = surface.PointAt(new GeoPoint2D(umin + i * (umax - umin) / n, vmin + j * (vmax - vmin) / n));
                        }
                        try
                        {
                            Polyline plu = Polyline.Construct();
                            plu.SetPoints(pu, false);
                            plu.ColorDef = cdr;
                            plu.Layer = solid;
                            dc.Add(plu);
                        }
                        catch (PolylineException)
                        {
                            Point pntu = Point.Construct();
                            pntu.Location = pu[0];
                            pntu.Symbol = PointSymbol.Cross;
                            pntu.ColorDef = cdr;
                            pntu.Layer = solid;
                            dc.Add(pntu);
                        }
                        try
                        {
                            Polyline plv = Polyline.Construct();
                            plv.SetPoints(pv, false);
                            plv.ColorDef = cdr;
                            plv.Layer = solid;
                            dc.Add(plv);
                        }
                        catch (PolylineException)
                        {
                            Point pntv = Point.Construct();
                            pntv.Location = pv[0];
                            pntv.Symbol = PointSymbol.Cross;
                            pntv.ColorDef = cdr;
                            pntv.Layer = solid;
                            dc.Add(pntv);
                        }
                    }
                }
                return dc;
            }
        }
        internal int DebugCount
        {
            get
            {
                ParEpi[] all = octtree.GetAllObjects();
                return all.Length;
            }
        }
#endif

        internal bool IsCloseTo(BoxedSurfaceEx bs2)
        {   // stellt fest, ob es sich überschneidende Spate gibt
            ParEpi[] all2 = bs2.octtree.GetAllObjects();
            for (int i = 0; i < all2.Length; ++i)
            {
                ParEpi[] all1 = octtree.GetObjectsCloseTo(all2[i]);
                for (int j = 0; j < all1.Length; ++j)
                {
                    if (all1[j].Interferes(all2[i])) return true;
                }
            }
            return false;
        }
        internal bool IsCloseTo(GeoPoint p)
        {   // stellt fest, ob es sich überschneidende Spate gibt
            ParEpi[] all1 = octtree.GetObjectsFromPoint(p);
            for (int j = 0; j < all1.Length; ++j)
            {
                if (all1[j].Contains(p))
                {
                    return true;
                }
            }
            return false;
        }
        private struct GeoPoint3d2d2d
        {
            public GeoPoint p3d;
            public GeoPoint2D uv1, uv2;
            public GeoPoint3d2d2d(GeoPoint p3d, GeoPoint2D uv1, GeoPoint2D uv2)
            {
                this.p3d = p3d;
                this.uv1 = uv1;
                this.uv2 = uv2;
            }

            internal void AdjustPeriodic(BoxedSurfaceEx bs1, BoxedSurfaceEx bs2, BoundingRect bounds1, BoundingRect bounds2)
            {
                if (bs1.surface.IsUPeriodic)
                {
                    while (bounds1.Right - uv1.x > bs1.surface.UPeriod) uv1.x += bs1.surface.UPeriod;
                    while (uv1.x - bounds1.Left > bs1.surface.UPeriod) uv1.x -= bs1.surface.UPeriod;
                }
                if (bs1.surface.IsVPeriodic)
                {
                    while (bounds1.Top - uv1.y > bs1.surface.VPeriod) uv1.y += bs1.surface.VPeriod;
                    while (uv1.y - bounds1.Bottom > bs1.surface.VPeriod) uv1.y -= bs1.surface.VPeriod;
                }
                if (bs2.surface.IsUPeriodic)
                {
                    while (bounds2.Right - uv2.x > bs2.surface.UPeriod) uv2.x += bs2.surface.UPeriod;
                    while (uv2.x - bounds2.Left > bs2.surface.UPeriod) uv2.x -= bs2.surface.UPeriod;
                }
                if (bs2.surface.IsVPeriodic)
                {
                    while (bounds2.Top - uv2.y > bs2.surface.VPeriod) uv2.y += bs2.surface.VPeriod;
                    while (uv2.y - bounds2.Bottom > bs2.surface.VPeriod) uv2.y -= bs2.surface.VPeriod;
                }
            }
        }
        private class LinkedIntersectionPoint : IOctTreeInsertable
        {
            public GeoPoint ip;
            public GeoPoint2D uv1, uv2;
            public GeoVector cross; // kreuzprodukt der Normalenvektoren in diesem Punkt: Richtung der Kurve, NullVector wenn tangential
            public GeoVector2D dir1, dir2; // Richtungen im jeweiligen uv System
            public LinkedIntersectionPoint next1, next2, prev1, prev2, next, prev; // verkettete Listen in uv1, uv2, zusammengemixt
            public int ui1, vi1, ui2, vi2; // index der fixedu/v Linie, zum Verketten
            public Tuple<int, int> enterOnSurf1, enterOnSurf2; // Feldnummer merken, wo dieser Punkt Eintrittspunkt ist
            [Flags]
            public enum emode { in1 = 1, in2 = 2, onVertex1 = 4, onVertex2 = 8, seed = 16, bottom = 32, top = 64, left = 128, right = 256, isCyclicalStart = 512 }
            public emode mode;
#if DEBUG
            public static int idCounter = 0;
            public int id;
            public LinkedIntersectionPoint()
            {
                id = idCounter++;
            }
            public override int GetHashCode()
            {
                return id;
            }
#endif

            internal static List<LinkedIntersectionPoint> CreateIntersections(ISurface surface1, ISurface surface2, BoundingRect bounds1, BoundingRect bounds2, bool on1, double par, double pmin, double pmax, bool fixedu)
            {
                List<LinkedIntersectionPoint> res = new List<LinkedIntersectionPoint>();
                ICurve crv;
                if (on1)
                {
                    if (fixedu) crv = surface1.FixedU(par, pmin, pmax);
                    else crv = surface1.FixedV(par, pmin, pmax);
                }
                else
                {
                    if (fixedu) crv = surface2.FixedU(par, pmin, pmax);
                    else crv = surface2.FixedV(par, pmin, pmax);
                }
                if (crv == null)
                {
                    return res;
                }
                GeoPoint[] ips;
                GeoPoint2D[] uvOnFaces;
                double[] uOnCurve3Ds;
                if (on1) surface2.Intersect(crv, bounds2, out ips, out uvOnFaces, out uOnCurve3Ds);
                else surface1.Intersect(crv, bounds1, out ips, out uvOnFaces, out uOnCurve3Ds);
                for (int i = 0; i < ips.Length; i++)
                {
                    LinkedIntersectionPoint lip = new LinkedIntersectionPoint();
                    lip.ip = ips[i];
                    if (on1)
                    {
                        lip.uv2 = uvOnFaces[i];
                        lip.mode = emode.in1;
                        lip.uv1 = surface1.PositionOf(ips[i]);
                        if (fixedu) lip.uv1.x = par;
                        else lip.uv1.y = par;
                    }
                    else
                    {
                        lip.uv1 = uvOnFaces[i];
                        lip.mode = emode.in2;
                        lip.uv2 = surface2.PositionOf(ips[i]);
                        if (fixedu) lip.uv2.x = par; // einrasten
                        else lip.uv2.y = par;
                    }
                    lip.AdjustPeriodic(surface1, surface2, bounds1, bounds2);
                    if (!bounds1.ContainsEps(lip.uv1, -1e-6) || !bounds2.ContainsEps(lip.uv2, -1e-6)) continue;
                    lip.ui1 = lip.vi1 = lip.ui2 = lip.vi2 = -1;
                    GeoVector diru1, dirv1, diru2, dirv2;
                    GeoPoint ip;
                    surface1.DerivationAt(lip.uv1, out ip, out diru1, out dirv1);
                    surface2.DerivationAt(lip.uv2, out ip, out diru2, out dirv2);
                    GeoVector n1 = (diru1 ^ dirv1).Normalized;
                    GeoVector n2 = (diru2 ^ dirv2).Normalized;
                    lip.cross = n1 ^ n2;
                    if (lip.cross.Length < 1e-4)
                    {   // annähernd tangential
                        lip.dir1 = GeoVector2D.NullVector;
                        lip.dir2 = GeoVector2D.NullVector;
                    }
                    else
                    {
                        Matrix m = DenseMatrix.OfColumnArrays(diru1, dirv1, n1);
                        Vector s = (Vector)m.Solve(new DenseVector(lip.cross));
                        if (s.IsValid())
                        {
                            lip.dir1 = new GeoVector2D(s[0], s[1]).Normalized;
                        }
                        m = DenseMatrix.OfColumnArrays(diru2, dirv2, n2);
                        s = (Vector)m.Solve(new DenseVector(lip.cross));
                        if (s.IsValid())
                        {
                            lip.dir2 = new GeoVector2D(s[0], s[1]).Normalized;
                        }
                    }
                    res.Add(lip);
                }
                return res;
            }

            private void AdjustPeriodic(ISurface surface1, ISurface surface2, BoundingRect bounds1, BoundingRect bounds2)
            {
                SurfaceHelper.AdjustPeriodic(surface1, bounds1, ref uv1);
                SurfaceHelper.AdjustPeriodic(surface2, bounds2, ref uv2);
            }

            internal static LinkedIntersectionPoint CreateFromSeed(ISurface surface1, ISurface surface2, BoundingRect bounds1, BoundingRect bounds2, GeoPoint ip)
            {
                LinkedIntersectionPoint lip = new LinkedIntersectionPoint();
                lip.ip = ip;

                lip.uv2 = surface2.PositionOf(ip);
                lip.mode = emode.seed;
                lip.uv1 = surface1.PositionOf(ip);
                lip.AdjustPeriodic(surface1, surface2, bounds1, bounds2);
                lip.ui1 = lip.vi1 = lip.ui2 = lip.vi2 = -1;
                GeoVector diru1, dirv1, diru2, dirv2;
                surface1.DerivationAt(lip.uv1, out ip, out diru1, out dirv1);
                surface2.DerivationAt(lip.uv2, out ip, out diru2, out dirv2);
                GeoVector n1 = (diru1 ^ dirv1).Normalized;
                GeoVector n2 = (diru2 ^ dirv2).Normalized;
                lip.cross = n1 ^ n2;
                if (lip.cross.Length < 1e-4)
                {   // annähernd tangential
                    lip.dir1 = GeoVector2D.NullVector;
                    lip.dir2 = GeoVector2D.NullVector;
                }
                else
                {
                    Matrix m = DenseMatrix.OfColumnArrays(diru1, dirv1, n1);
                    Vector s = (Vector)m.Solve(new DenseVector(lip.cross));
                    if (s.IsValid())
                    {
                        lip.dir1 = new GeoVector2D(s[0], s[1]);
                    }
                    m = DenseMatrix.OfColumnArrays(diru2, dirv2, n2);
                    s = (Vector)m.Solve(new DenseVector(lip.cross));
                    if (s.IsValid())
                    {
                        lip.dir2 = new GeoVector2D(s[0], s[1]);
                    }
                }
                return lip;
            }

            BoundingCube IOctTreeInsertable.GetExtent(double precision)
            {
                return new CADability.BoundingCube(ip);
            }

            bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
            {
                return cube.Contains(ip);
            }

            bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
            {
                throw new NotImplementedException();
            }

            bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
            {
                throw new NotImplementedException();
            }

            double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
            {
                throw new NotImplementedException();
            }

            internal void MergeWith(LinkedIntersectionPoint other)
            {
                if (other.mode.HasFlag(emode.in1))
                {
                    if (other.ui1 != -1) ui1 = other.ui1;
                    if (other.vi1 != -1) vi1 = other.vi1;
                    uv1 = other.uv1;
                    mode |= emode.in1;
                }
                if (other.mode.HasFlag(emode.in2))
                {
                    if (other.ui2 != -1) ui2 = other.ui2;
                    if (other.vi2 != -1) vi2 = other.vi2;
                    uv2 = other.uv2;
                    mode |= emode.in2;
                }
                if (other.mode.HasFlag(emode.seed))
                {   // seed überschreibt die Daten, sollte genauer sein!
                    uv1 = other.uv1;
                    uv2 = other.uv2;
                    ip = other.ip;
                    mode |= emode.seed;
                }

            }

            internal void AddToGrid(Dictionary<Tuple<int, int>, List<LinkedIntersectionPoint>> surf1ips, Dictionary<Tuple<int, int>, List<LinkedIntersectionPoint>> surf2ips, List<double> uknots1, List<double> vknots1, List<double> uknots2, List<double> vknots2)
            {
                int uind = ui1;
                int vind = vi1;
                if (uind < 0) uind = findInd(uknots1, uv1.x);
                if (vind < 0) vind = findInd(vknots1, uv1.y);
                List<LinkedIntersectionPoint> llip;
                if (!surf1ips.TryGetValue(new Tuple<int, int>(uind, vind), out llip))
                {
                    llip = new List<LinkedIntersectionPoint>();
                    surf1ips[new Tuple<int, int>(uind, vind)] = llip;
                }
                llip.Add(this);
                if (ui1 >= 0 && vi1 >= 0) // auf Kreuzung
                {
                    if (!surf1ips.TryGetValue(new Tuple<int, int>(uind + 1, vind + 1), out llip))
                    {
                        llip = new List<LinkedIntersectionPoint>();
                        surf1ips[new Tuple<int, int>(uind + 1, vind + 1)] = llip;
                    }
                    llip.Add(this);
                }
                if (ui1 >= 0) // auf Kante
                {
                    if (!surf1ips.TryGetValue(new Tuple<int, int>(uind + 1, vind), out llip))
                    {
                        llip = new List<LinkedIntersectionPoint>();
                        surf1ips[new Tuple<int, int>(uind + 1, vind)] = llip;
                    }
                    llip.Add(this);
                }
                if (vi1 >= 0) // auf Kante
                {
                    if (!surf1ips.TryGetValue(new Tuple<int, int>(uind, vind + 1), out llip))
                    {
                        llip = new List<LinkedIntersectionPoint>();
                        surf1ips[new Tuple<int, int>(uind, vind + 1)] = llip;
                    }
                    llip.Add(this);
                }
                // desgleichem mit 2
                uind = ui2;
                vind = vi2;
                if (uind < 0) uind = findInd(uknots2, uv2.x);
                if (vind < 0) vind = findInd(vknots2, uv2.y);
                if (!surf2ips.TryGetValue(new Tuple<int, int>(uind, vind), out llip))
                {
                    llip = new List<LinkedIntersectionPoint>();
                    surf2ips[new Tuple<int, int>(uind, vind)] = llip;
                }
                llip.Add(this);
                if (ui2 >= 0 && vi2 >= 0) // auf Kreuzung
                {
                    if (!surf2ips.TryGetValue(new Tuple<int, int>(uind + 1, vind + 1), out llip))
                    {
                        llip = new List<LinkedIntersectionPoint>();
                        surf2ips[new Tuple<int, int>(uind + 1, vind + 1)] = llip;
                    }
                    llip.Add(this);
                }
                if (ui2 >= 0) // auf Kante
                {
                    if (!surf2ips.TryGetValue(new Tuple<int, int>(uind + 1, vind), out llip))
                    {
                        llip = new List<LinkedIntersectionPoint>();
                        surf2ips[new Tuple<int, int>(uind + 1, vind)] = llip;
                    }
                    llip.Add(this);
                }
                if (vi2 >= 0) // auf Kante
                {
                    if (!surf2ips.TryGetValue(new Tuple<int, int>(uind, vind + 1), out llip))
                    {
                        llip = new List<LinkedIntersectionPoint>();
                        surf2ips[new Tuple<int, int>(uind, vind + 1)] = llip;
                    }
                    llip.Add(this);
                }
            }

            private int findInd(List<double> knots, double val)
            {   // lineare Suche nicht effizient
                for (int i = 0; i < knots.Count - 1; i++)
                {
                    if (val >= knots[i] && val <= knots[i + 1]) return i + 1;
                }
                if (val < knots[0]) return 0;
                if (val > knots[knots.Count - 1]) return knots.Count;
                return -1; // kommt nicht vor
            }
        }
        internal ICurve[] Intersect(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, List<GeoPoint> seeds, List<Tuple<double, double, double, double>> additionalSearchPositions = null)
        {
            List<ICurve> res = new List<GeoObject.ICurve>();
            // die u bzw. v Werte, die von den sich evtl schneidenden ParEpi verwendet werden
            // das ergibt für jede Surface ein Gitter
            SortedSet<double> uVal1 = new SortedSet<double>();
            SortedSet<double> vVal1 = new SortedSet<double>();
            SortedSet<double> uVal2 = new SortedSet<double>();
            SortedSet<double> vVal2 = new SortedSet<double>();
            if ((other as ISurfaceImpl).usedArea.IsEmpty()) (other as ISurfaceImpl).usedArea = otherBounds;
            BoxedSurfaceEx otherBS = (other as ISurfaceImpl).BoxedSurfaceEx;
            foreach (ParEpi pe in octtree.GetAllObjects())
            {
                if (pe.uvPatch.Interferes(ref thisBounds))
                {
                    foreach (ParEpi otherPe in otherBS.octtree.GetObjectsCloseTo(pe))
                    {
                        if (otherPe.uvPatch.Interferes(ref otherBounds))
                        {
                            if (pe.Interferes(otherPe))
                            {
                                uVal1.Add(pe.uvPatch.Left);
                                uVal1.Add(pe.uvPatch.Right);
                                vVal1.Add(pe.uvPatch.Bottom);
                                vVal1.Add(pe.uvPatch.Top);
                                uVal2.Add(otherPe.uvPatch.Left);
                                uVal2.Add(otherPe.uvPatch.Right);
                                vVal2.Add(otherPe.uvPatch.Bottom);
                                vVal2.Add(otherPe.uvPatch.Top);
                            }
                        }
                    }
                }
            }
            if (seeds == null && additionalSearchPositions != null)
            {   // this is a special case: typically this is called to find intersections of two faces, where the edge/face intersections already have been calculated and are provided in the seeds.
                // But it is possible that two faces have closed loops of intersection curves, which do not cross their bounds (edges). (E.g. two spheres intersect without edge intersection)
                // If so, there must be some u or v parameters for additional checks provided. These "additionalSearchPositions" may have u or v values on this or the other surface.
                for (int i = 0; i < additionalSearchPositions.Count; i++)
                {
                    if (!double.IsNaN(additionalSearchPositions[i].Item1)) addOrAdjust(uVal1, additionalSearchPositions[i].Item1);
                    if (!double.IsNaN(additionalSearchPositions[i].Item2)) addOrAdjust(vVal1, additionalSearchPositions[i].Item2);
                    if (!double.IsNaN(additionalSearchPositions[i].Item3)) addOrAdjust(uVal2, additionalSearchPositions[i].Item3);
                    if (!double.IsNaN(additionalSearchPositions[i].Item4)) addOrAdjust(vVal2, additionalSearchPositions[i].Item4);
                }
            }
            if (uVal1.Count == 0) return res.ToArray(); // nichts, keine Überlappung
            bool splitted = true; // die uVal1 u.s.w. Listen können erweitert werden, wenn zu viele Punkte in einem Feld liegen. Dann muss von vorne begonnen werden
            int splitcount = 0;
            while (splitted)
            {
                splitted = false;
                if (++splitcount > 10) return res.ToArray(); // something went wrong with splitting, no result
#if DEBUG
                LinkedIntersectionPoint.idCounter = 0;
#endif
                double u1prec = smallesDiff(uVal1) * 1e-3 * (splitcount + 1); // Genauigkeit für einzelne Parameter, hier sollte kein Schnittpunkt sein
                double v1prec = smallesDiff(vVal1) * 1e-3 * (splitcount + 1); // *(splitcount+1): to avoid endless ping pong
                double u2prec = smallesDiff(uVal2) * 1e-3 * (splitcount + 1);
                double v2prec = smallesDiff(vVal2) * 1e-3 * (splitcount + 1);
#if DEBUG
                DebuggerContainer dcuv1 = new DebuggerContainer();
                DebuggerContainer dcuv2 = new DebuggerContainer();
                DebuggerContainer dc3d1 = new DebuggerContainer();
                DebuggerContainer dc3d2 = new DebuggerContainer();
                foreach (double u in uVal1)
                {
                    dcuv1.Add(new Line2D(new GeoPoint2D(u, vVal1.Min), new GeoPoint2D(u, vVal1.Max)), Color.Black, 1);
                    dc3d1.Add(surface.FixedU(u, vVal1.Min, vVal1.Max) as IGeoObject, Color.Black);
                }
                foreach (double v in vVal1)
                {
                    dcuv1.Add(new Line2D(new GeoPoint2D(uVal1.Min, v), new GeoPoint2D(uVal1.Max, v)), Color.Black, 1);
                    dc3d1.Add(surface.FixedV(v, uVal1.Min, uVal1.Max) as IGeoObject, Color.Black);
                }
                foreach (double u in uVal2)
                {
                    dcuv2.Add(new Line2D(new GeoPoint2D(u, vVal2.Min), new GeoPoint2D(u, vVal2.Max)), Color.Black, 1);
                    dc3d2.Add(other.FixedU(u, vVal2.Min, vVal2.Max) as IGeoObject, Color.Black);
                }
                foreach (double v in vVal2)
                {
                    dcuv2.Add(new Line2D(new GeoPoint2D(uVal2.Min, v), new GeoPoint2D(uVal2.Max, v)), Color.Black, 1);
                    dc3d2.Add(other.FixedV(v, uVal2.Min, uVal2.Max) as IGeoObject, Color.Black);
                }
#endif
                Set<LinkedIntersectionPoint> allIps = new Set<LinkedIntersectionPoint>();
                List<double> uknots1 = new List<double>(uVal1); // die kommen dann ja hoffentlich sortiert
                List<double> vknots1 = new List<double>(vVal1);
                List<double> uknots2 = new List<double>(uVal2);
                List<double> vknots2 = new List<double>(vVal2);
                BoundingRect bounds1 = new BoundingRect(uVal1.Min, vVal1.Min, uVal1.Max, vVal1.Max);
                BoundingRect bounds2 = new BoundingRect(uVal2.Min, vVal2.Min, uVal2.Max, vVal2.Max);
                List<LinkedIntersectionPoint> l;
                for (int i = 0; i < uknots1.Count; i++)
                {
                    l = LinkedIntersectionPoint.CreateIntersections(surface, other, bounds1, bounds2, true, uknots1[i], vVal1.Min, vVal1.Max, true);
                    foreach (LinkedIntersectionPoint lip in l)
                    {
                        lip.ui1 = i;
                        if (testAndChangeParameter(lip.uv1.y, vVal1, v1prec))
                        {
                            splitted = true; // damit wird neu angefangen
                            break;
                        }
                    }
                    if (splitted) break;
                    allIps.AddMany(l);
                }
                if (splitted) continue;
                for (int i = 0; i < vknots1.Count; i++)
                {
                    l = LinkedIntersectionPoint.CreateIntersections(surface, other, bounds1, bounds2, true, vknots1[i], uVal1.Min, uVal1.Max, false);
                    foreach (LinkedIntersectionPoint lip in l)
                    {
                        lip.vi1 = i;
                        if (testAndChangeParameter(lip.uv1.x, uVal1, u1prec))
                        {
                            splitted = true; // damit wird neu angefangen
                            break;
                        }
                    }
                    if (splitted) break;
                    allIps.AddMany(l);
                }
                if (splitted) continue;
                for (int i = 0; i < uknots2.Count; i++)
                {
                    l = LinkedIntersectionPoint.CreateIntersections(surface, other, bounds1, bounds2, false, uknots2[i], vVal2.Min, vVal2.Max, true);
                    foreach (LinkedIntersectionPoint lip in l)
                    {
                        lip.ui2 = i;
                        if (testAndChangeParameter(lip.uv2.y, vVal2, v2prec))
                        {
                            splitted = true; // damit wird neu angefangen
                            break;
                        }
                    }
                    if (splitted) break;
                    allIps.AddMany(l);
                }
                if (splitted) continue;
                for (int i = 0; i < vknots2.Count; i++)
                {
                    l = LinkedIntersectionPoint.CreateIntersections(surface, other, bounds1, bounds2, false, vknots2[i], uVal2.Min, uVal2.Max, false);
                    foreach (LinkedIntersectionPoint lip in l)
                    {
                        lip.vi2 = i;
                        if (testAndChangeParameter(lip.uv2.x, uVal2, u2prec))
                        {
                            splitted = true; // damit wird neu angefangen
                            break;
                        }
                    }
                    if (splitted) break;
                    allIps.AddMany(l);
                }
                if (splitted) continue;
                //if (seeds != null)
                //{
                //    for (int i = 0; i < seeds.Length; i++)
                //    {
                //        allIps.Add(LinkedIntersectionPoint.CreateFromSeed(surface, other, bounds1, bounds2, seeds[i]));
                //    }
                //}
#if DEBUG
                double d1 = ((uVal1.Max - uVal1.Min) + (vVal1.Max - vVal1.Min)) / 100.0;
                double d2 = ((uVal2.Max - uVal2.Min) + (vVal2.Max - vVal2.Min)) / 100.0;
                int ipcont = -1;
                foreach (LinkedIntersectionPoint lip in allIps)
                {
                    ++ipcont;
                    if ((lip.mode & LinkedIntersectionPoint.emode.in1) != 0)
                    {
                        if (lip.dir1.IsNullVector())
                        {
                            Circle2D c2d = new Circle2D(lip.uv1, d1);
                            dcuv1.Add(c2d, Color.Red, lip.id);
                            c2d = new Circle2D(lip.uv2, d2);
                            dcuv2.Add(c2d, Color.Blue, lip.id);
                        }
                        else
                        {
                            Line2D l2d = new Line2D(lip.uv1, lip.uv1 + d1 * lip.dir1.Normalized);
                            dcuv1.Add(l2d, Color.Red, lip.id);
                            l2d = new Line2D(lip.uv2, lip.uv2 + d2 * lip.dir2.Normalized);
                            dcuv2.Add(l2d, Color.Blue, lip.id);
                        }
                    }
                    if ((lip.mode & LinkedIntersectionPoint.emode.in2) != 0)
                    {
                        if (lip.dir2.IsNullVector())
                        {
                            Circle2D c2d = new Circle2D(lip.uv2, d2);
                            dcuv2.Add(c2d, Color.Red, lip.id);
                            c2d = new Circle2D(lip.uv1, d1);
                            dcuv1.Add(c2d, Color.Blue, lip.id);
                        }
                        else
                        {
                            Line2D l2d = new Line2D(lip.uv2, lip.uv2 + d2 * lip.dir2.Normalized);
                            dcuv2.Add(l2d, Color.Red, lip.id);
                            l2d = new Line2D(lip.uv1, lip.uv1 + d1 * lip.dir1.Normalized);
                            dcuv1.Add(l2d, Color.Blue, lip.id);
                        }
                    }
                    if ((lip.mode & LinkedIntersectionPoint.emode.seed) != 0)
                    {
                        if (lip.dir2.IsNullVector())
                        {
                            Circle2D c2d = new Circle2D(lip.uv2, d2);
                            dcuv2.Add(c2d, Color.Green, lip.id);
                            c2d = new Circle2D(lip.uv1, d1);
                            dcuv1.Add(c2d, Color.Green, lip.id);
                        }
                        else
                        {
                            Line2D l2d = new Line2D(lip.uv2, lip.uv2 + d2 * lip.dir2.Normalized);
                            dcuv2.Add(l2d, Color.Green, lip.id);
                            l2d = new Line2D(lip.uv1, lip.uv1 + d1 * lip.dir1.Normalized);
                            dcuv1.Add(l2d, Color.Green, lip.id);
                        }

                    }
                }
#endif
                // doppelte entfernen
                double prec = (octtree.Extend.Size + otherBS.octtree.Extend.Size) * 1e-6;
                OctTree<LinkedIntersectionPoint> ipocttree = new OctTree<LinkedIntersectionPoint>(octtree.Extend + otherBS.octtree.Extend, octtree.precision);
                Set<LinkedIntersectionPoint> toRemove = new Set<LinkedIntersectionPoint>();
                foreach (LinkedIntersectionPoint lip in allIps)
                {
                    if (ipocttree.IsEmpty) ipocttree.AddObject(lip);
                    else
                    {
                        LinkedIntersectionPoint[] close = ipocttree.GetObjectsCloseTo(lip);
                        bool merged = false;
                        for (int i = 0; i < close.Length; i++)
                        {
                            if ((close[i].ip | lip.ip) < prec)
                            {
                                close[i].MergeWith(lip);
                                merged = true;
                                toRemove.Add(lip);
                                break;
                            }
                        }
                        if (!merged) ipocttree.AddObject(lip);
                    }
                }
                allIps.RemoveMany(toRemove);
                // in das durch die u und v-Werte gegebene Schachbrett einsortieren
                // Index 0 ist links bzw. unterhalb des Rasters, Index knots.Count ist rechts bzw. oberhalb des Rasters. So können auch Punkte außerhalb sinnvoll einsortiert werden
                Dictionary<Tuple<int, int>, List<LinkedIntersectionPoint>> surf1ips = new Dictionary<Tuple<int, int>, List<LinkedIntersectionPoint>>();
                Dictionary<Tuple<int, int>, List<LinkedIntersectionPoint>> surf2ips = new Dictionary<Tuple<int, int>, List<LinkedIntersectionPoint>>();
                foreach (LinkedIntersectionPoint lip in allIps)
                {
                    lip.AddToGrid(surf1ips, surf2ips, uknots1, vknots1, uknots2, vknots2);
                }
#if DEBUG
                for (int i = 0; i < allIps.Debug.Length; i++)
                {
                    if (allIps.Debug[i].mode == LinkedIntersectionPoint.emode.seed)
                    {

                    }
                }
#endif
                // stelle in der jeweiligen Fläche die Verbindungen innerhalb eines Schachbretts her
                for (int s = 1; s <= 2; ++s) // beide surfaces
                {
                    if (splitted) break;
                    Dictionary<Tuple<int, int>, List<LinkedIntersectionPoint>> surfips;
                    if (s == 1) surfips = surf1ips;
                    else surfips = surf2ips;
                    foreach (KeyValuePair<Tuple<int, int>, List<LinkedIntersectionPoint>> kv in surfips)
                    {
                        if (splitted) break;
                        // die Liste enthält alle Schnittpunkte in diesem Schachbrettfeld
                        // wir suchen jetzt nur die, die zu surface1 gehören
                        List<LinkedIntersectionPoint> entering = new List<LinkedIntersectionPoint>();
                        List<LinkedIntersectionPoint> leaving = new List<LinkedIntersectionPoint>();
                        List<LinkedIntersectionPoint> tangential = new List<LinkedIntersectionPoint>();

                        for (int i = 0; i < kv.Value.Count; i++)
                        {
                            LinkedIntersectionPoint lip = kv.Value[i];
                            GeoVector2D dir;
                            int ui, vi;
                            LinkedIntersectionPoint.emode testMode;
                            if (s == 1)
                            {
                                dir = lip.dir1;
                                ui = lip.ui1;
                                vi = lip.vi1;
                                testMode = LinkedIntersectionPoint.emode.in1;
                            }
                            else
                            {
                                dir = lip.dir2;
                                ui = lip.ui2;
                                vi = lip.vi2;
                                testMode = LinkedIntersectionPoint.emode.in2;
                            }
                            if (lip.mode.HasFlag(testMode))
                            {
                                if (dir.IsNullVector()) tangential.Add(lip);
                                else if (ui >= 0 && vi >= 0)
                                {   // liegt auf einem Eck
                                    if (ui == kv.Key.Item1 && vi == kv.Key.Item2)
                                    {   // ist rechter oberer Eckpunkt
                                        if (dir.x <= 0 && dir.y <= 0) entering.Add(lip);
                                        if (dir.x >= 0 && dir.y >= 0) leaving.Add(lip);
                                    }
                                    else if (ui + 1 == kv.Key.Item1 && vi == kv.Key.Item2)
                                    {   // ist linker oberer Eckpunkt
                                        if (dir.x >= 0 && dir.y <= 0) entering.Add(lip);
                                        if (dir.x <= 0 && dir.y >= 0) leaving.Add(lip);
                                    }
                                    else if (ui == kv.Key.Item1 && vi + 1 == kv.Key.Item2)
                                    {   // ist rechter unterer Eckpunkt
                                        if (dir.x <= 0 && dir.y >= 0) entering.Add(lip);
                                        if (dir.x >= 0 && dir.y <= 0) leaving.Add(lip);
                                    }
                                    else if (ui + 1 == kv.Key.Item1 && vi + 1 == kv.Key.Item2)
                                    {   // ist linker unterer Eckpunkt
                                        if (dir.x >= 0 && dir.y >= 0) entering.Add(lip);
                                        if (dir.x <= 0 && dir.y <= 0) leaving.Add(lip);
                                    }
                                    else
                                    {

                                    }
                                }
                                else if (ui >= 0)
                                {   // linke oder rechte Kante
                                    if (ui == kv.Key.Item1)
                                    {   // ist rechte Kante
                                        if (dir.x <= 0) entering.Add(lip);
                                        if (dir.x >= 0) leaving.Add(lip);
                                    }
                                    else if (ui + 1 == kv.Key.Item1)
                                    {   // ist linke Kante
                                        if (dir.x >= 0) entering.Add(lip);
                                        if (dir.x <= 0) leaving.Add(lip);
                                    }
                                    else
                                    {

                                    }
                                }
                                else if (vi >= 0)
                                {   // untere oder obere Kante
                                    if (vi == kv.Key.Item2)
                                    {   // ist obere Kante
                                        if (dir.y <= 0) entering.Add(lip);
                                        if (dir.y >= 0) leaving.Add(lip);
                                    }
                                    else if (vi + 1 == kv.Key.Item2)
                                    {   // ist untere Kante
                                        if (dir.y >= 0) entering.Add(lip);
                                        if (dir.y <= 0) leaving.Add(lip);
                                    }
                                    else
                                    {

                                    }
                                }
                            }
                        }
                        if (tangential.Count == 1 && (entering.Count + leaving.Count) == 1)
                        {
                            if (entering.Count == 0) entering.Add(tangential[0]);
                            else leaving.Add(tangential[0]);
                            tangential.Clear();
                        }
                        if (entering.Count > 1 || leaving.Count > 1)
                        {
                            // in diesem patch kann man nicht eindeutig verbinden. Wir müssen eine zusätzliche Trennlinie einführen und von vorne anfangen
                            for (int ii = 0; ii < entering.Count; ii++)
                            {
                                for (int jj = 0; jj < leaving.Count; jj++)
                                {
                                    if (s == 1)
                                    {
                                        if (entering[ii].ui1 == leaving[jj].ui1 && entering[ii].ui1 >= 0)
                                        {
                                            vVal1.Add((entering[ii].uv1.y + leaving[jj].uv1.y) / 2.0);
                                            splitted = true;
                                            break;
                                        }
                                        else if (entering[ii].vi1 == leaving[jj].vi1 && entering[ii].vi1 >= 0)
                                        {
                                            uVal1.Add((entering[ii].uv1.x + leaving[jj].uv1.x) / 2.0);
                                            splitted = true;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        if (entering[ii].ui2 == leaving[jj].ui2 && entering[ii].ui2 >= 0)
                                        {
                                            vVal2.Add((entering[ii].uv2.y + leaving[jj].uv2.y) / 2.0);
                                            splitted = true;
                                            break;
                                        }
                                        else if (entering[ii].vi2 == leaving[jj].vi2 && entering[ii].vi2 >= 0)
                                        {
                                            uVal2.Add((entering[ii].uv2.x + leaving[jj].uv2.x) / 2.0);
                                            splitted = true;
                                            break;
                                        }
                                    }
                                    if (splitted) break;
                                }
                                if (splitted) break;
                            }
                            if (!splitted)
                            {   // maybe there is a intersection tangential to a fixed-u or fixed-v line. This can be removed by varying the uvals or vvals by a small amount
                                if (entering.Count > 1)
                                {
                                    if (s == 1)
                                    {
                                        double badval = double.MaxValue;
                                        bool badu = true;
                                        bool up = true;

                                        double umin = double.MaxValue;
                                        double vmin = double.MaxValue;
                                        double umax = double.MinValue;
                                        double vmax = double.MinValue;
                                        for (int ii = 0; ii < entering.Count; ii++)
                                        {
                                            if (entering[ii].ui1 >= 0 && Math.Abs(entering[ii].dir1.x) < 1e-3)
                                            {
                                                badval = entering[ii].uv1.x;
                                                badu = true;
                                                up = entering[ii].dir1.x < 0;
                                            }
                                            if (entering[ii].vi1 >= 0 && Math.Abs(entering[ii].dir1.y) < 1e-3)
                                            {
                                                badval = entering[ii].uv1.y;
                                                badu = false;
                                                up = entering[ii].dir1.y < 0;
                                            }
                                            umin = Math.Min(umin, entering[ii].uv1.x);
                                            umax = Math.Max(umax, entering[ii].uv1.x);
                                            vmin = Math.Min(vmin, entering[ii].uv1.y);
                                            vmax = Math.Max(vmax, entering[ii].uv1.y);
                                        }
                                        if (badval < double.MaxValue)
                                        {
                                            if (badu)
                                            {
                                                uVal1.Remove(badval);
                                                if (up) uVal1.Add(badval + u1prec);
                                                else uVal1.Add(badval - u1prec);
                                            }
                                            else
                                            {
                                                vVal1.Remove(badval);
                                                if (up) vVal1.Add(badval + v1prec);
                                                else vVal1.Add(badval - v1prec);
                                            }
                                        }
                                        else
                                        {
                                            uVal1.Add((umax + umin) / 2.0);
                                            vVal1.Add((vmax + vmin) / 2.0);
                                        }

                                        splitted = true;
                                        break;
                                    }
                                    else
                                    {
                                        double badval = double.MaxValue;
                                        bool badu = true;
                                        bool up = true;

                                        double umin = double.MaxValue;
                                        double vmin = double.MaxValue;
                                        double umax = double.MinValue;
                                        double vmax = double.MinValue;
                                        for (int ii = 0; ii < entering.Count; ii++)
                                        {
                                            if (entering[ii].ui2 >= 0 && Math.Abs(entering[ii].dir2.x) < 1e-3)
                                            {
                                                badval = entering[ii].uv2.x;
                                                badu = true;
                                                up = entering[ii].dir2.x < 0;
                                            }
                                            if (entering[ii].vi2 >= 0 && Math.Abs(entering[ii].dir2.y) < 1e-3)
                                            {
                                                badval = entering[ii].uv2.y;
                                                badu = false;
                                                up = entering[ii].dir2.y < 0;
                                            }
                                            umin = Math.Min(umin, entering[ii].uv2.x);
                                            umax = Math.Max(umax, entering[ii].uv2.x);
                                            vmin = Math.Min(vmin, entering[ii].uv2.y);
                                            vmax = Math.Max(vmax, entering[ii].uv2.y);
                                        }
                                        if (badval < double.MaxValue)
                                        {
                                            if (badu)
                                            {
                                                uVal2.Remove(badval);
                                                if (up) uVal2.Add(badval + u2prec);
                                                else uVal2.Add(badval - u2prec);
                                            }
                                            else
                                            {
                                                vVal2.Remove(badval);
                                                if (up) vVal2.Add(badval + v2prec);
                                                else vVal2.Add(badval - v2prec);
                                            }
                                        }
                                        else
                                        {
                                            uVal2.Add((umax + umin) / 2.0);
                                            vVal2.Add((vmax + vmin) / 2.0);
                                        }

                                        splitted = true;
                                        break;
                                    }
                                }
                                else // leaving.Count>1
                                {
                                    if (s == 1)
                                    {
                                        double badval = double.MaxValue;
                                        bool badu = true;
                                        bool up = true;

                                        double umin = double.MaxValue;
                                        double vmin = double.MaxValue;
                                        double umax = double.MinValue;
                                        double vmax = double.MinValue;
                                        for (int ii = 0; ii < leaving.Count; ii++)
                                        {
                                            if (leaving[ii].ui1 >= 0 && Math.Abs(leaving[ii].dir1.x) < 1e-3)
                                            {
                                                badval = leaving[ii].uv1.x;
                                                badu = true;
                                                up = leaving[ii].dir1.x < 0;
                                            }
                                            if (leaving[ii].vi1 >= 0 && Math.Abs(leaving[ii].dir1.y) < 1e-3)
                                            {
                                                badval = leaving[ii].uv1.y;
                                                badu = false;
                                                up = leaving[ii].dir1.y < 0;
                                            }
                                            umin = Math.Min(umin, leaving[ii].uv1.x);
                                            umax = Math.Max(umax, leaving[ii].uv1.x);
                                            vmin = Math.Min(vmin, leaving[ii].uv1.y);
                                            vmax = Math.Max(vmax, leaving[ii].uv1.y);
                                        }
                                        if (badval < double.MaxValue)
                                        {
                                            if (badu)
                                            {
                                                uVal1.Remove(badval);
                                                if (up) uVal1.Add(badval + u1prec);
                                                else uVal1.Add(badval - u1prec);
                                            }
                                            else
                                            {
                                                vVal1.Remove(badval);
                                                if (up) vVal1.Add(badval + v1prec);
                                                else vVal1.Add(badval - v1prec);
                                            }
                                        }
                                        else
                                        {
                                            uVal1.Add((umax + umin) / 2.0);
                                            vVal1.Add((vmax + vmin) / 2.0);
                                        }

                                        splitted = true;
                                        break;
                                    }
                                    else
                                    {
                                        double badval = double.MaxValue;
                                        bool badu = true;
                                        bool up = true;

                                        double umin = double.MaxValue;
                                        double vmin = double.MaxValue;
                                        double umax = double.MinValue;
                                        double vmax = double.MinValue;
                                        for (int ii = 0; ii < leaving.Count; ii++)
                                        {
                                            if (leaving[ii].ui2 >= 0 && Math.Abs(leaving[ii].dir2.x) < 1e-3)
                                            {
                                                badval = leaving[ii].uv2.x;
                                                badu = true;
                                                up = leaving[ii].dir2.x < 0;
                                            }
                                            if (leaving[ii].vi2 >= 0 && Math.Abs(leaving[ii].dir2.y) < 1e-3)
                                            {
                                                badval = leaving[ii].uv2.y;
                                                badu = false;
                                                up = leaving[ii].dir2.y < 0;
                                            }
                                            umin = Math.Min(umin, leaving[ii].uv2.x);
                                            umax = Math.Max(umax, leaving[ii].uv2.x);
                                            vmin = Math.Min(vmin, leaving[ii].uv2.y);
                                            vmax = Math.Max(vmax, leaving[ii].uv2.y);
                                        }
                                        if (badval < double.MaxValue)
                                        {
                                            if (badu)
                                            {
                                                uVal2.Remove(badval);
                                                if (up) uVal2.Add(badval + u2prec);
                                                else uVal2.Add(badval - u2prec);
                                            }
                                            else
                                            {
                                                vVal2.Remove(badval);
                                                if (up) vVal2.Add(badval + v2prec);
                                                else vVal2.Add(badval - v2prec);
                                            }
                                        }
                                        else
                                        {
                                            uVal2.Add((umax + umin) / 2.0);
                                            vVal2.Add((vmax + vmin) / 2.0);
                                        }

                                        splitted = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (entering.Count == 1 && leaving.Count == 1)
                        {
                            if (s == 1)
                            {
#if DEBUG
                                if (entering[0].next1 != null || leaving[0].prev1 != null || entering[0].enterOnSurf1 != null)
                                {

                                }
#endif
                                entering[0].next1 = leaving[0];
                                leaving[0].prev1 = entering[0];
                            }
                            else
                            {
#if DEBUG
                                if (entering[0].next2 != null || leaving[0].prev2 != null || entering[0].enterOnSurf2 != null)
                                {

                                }
#endif
                                entering[0].next2 = leaving[0];
                                leaving[0].prev2 = entering[0];
                            }
                        }
                        if (entering.Count == 1)
                        {
                            if (s == 1)
                            {
                                entering[0].enterOnSurf1 = kv.Key;
                            }
                            else
                            {
                                entering[0].enterOnSurf2 = kv.Key;
                            }
                        }
                    }
                }
                if (splitted) continue;
                // in jedem Feld des Schachbretts die jeweiligen Punkte des anderen Schachbretts in die Kette einfügen
                // Jeder Punkt weiß, in welchem Feld er Eintrittspunkt ist
#if DEBUG
                foreach (LinkedIntersectionPoint lip in allIps)
                {
                    lip.next = null;
                    lip.prev = null;
                }
#endif
                List<LinkedIntersectionPoint> startPointOn1 = new List<LinkedIntersectionPoint>(); // werden nicht weiter verwendet, oder?
                List<LinkedIntersectionPoint> startPointOn2 = new List<LinkedIntersectionPoint>();
                List<LinkedIntersectionPoint> enterPoints = new List<LinkedIntersectionPoint>();
                foreach (LinkedIntersectionPoint lip in allIps)
                {
                    if (lip.prev1 == null) // lip.next1 != null ist nicht notwendig, kann auch der einzige Punkt sein
                    {
                        if ((lip.enterOnSurf1 != null && ((lip.ui1 == 0) || lip.ui1 == uknots1.Count - 1)) ||
                        (lip.enterOnSurf1 != null && ((lip.vi1 == 0) || lip.vi1 == vknots1.Count - 1)))
                            enterPoints.Add(lip);
                        else
                            startPointOn1.Add(lip);
                    }
                    if (lip.prev2 == null) // lip.next2 != null ist nicht notwendig, kann auch der einzige Punkt sein
                    {
                        if ((lip.enterOnSurf2 != null && ((lip.ui2 == 0) || lip.ui2 == uknots2.Count - 1)) ||
                        (lip.enterOnSurf2 != null && ((lip.vi2 == 0) || lip.vi2 == vknots2.Count - 1)))
                        {
                            enterPoints.Add(lip);
                        }
                        else
                            startPointOn2.Add(lip);
                    }
                }
                LinkedIntersectionPoint cyclicalStartPoint = null;
                if (enterPoints.Count == 0 && allIps.Count > 0)
                {   // probably a closed curve, not crossing the bounds of either thisBound nor otherBounds
                    cyclicalStartPoint = allIps.GetAny();
                    cyclicalStartPoint.mode |= LinkedIntersectionPoint.emode.isCyclicalStart;
                    enterPoints.Add(cyclicalStartPoint); // for the following loop 
                }
                List<LinkedIntersectionPoint> startPoints = new List<LinkedIntersectionPoint>(enterPoints); // in case of cyclical curves we cann add more to startPoints
                                                                                                            // all intersection points are now connected via next1/prev1 or next2/prev2. these two chains are now combined so that next/prev are valid
                foreach (LinkedIntersectionPoint enterPoint in enterPoints)
                {
                    LinkedIntersectionPoint currentOn1 = null, currentOn2 = null, current = null;
                    if (enterPoint.enterOnSurf1 != null)
                    {
                        current = currentOn1 = enterPoint;
                    }
                    else if (enterPoint.enterOnSurf2 != null)
                    {
                        current = currentOn2 = enterPoint;
                    }
                    while (current != null)
                    {
                        if (current.mode.HasFlag(LinkedIntersectionPoint.emode.in1) && current.mode.HasFlag(LinkedIntersectionPoint.emode.in2))
                        {
                            if (current.next2 != null && surf1ips[current.enterOnSurf1].Contains(current.next2))
                            {
                                current.next = current.next2;
                                currentOn2 = current.next2;
                            }
                            else if (current.next1 != null && surf2ips[current.enterOnSurf2].Contains(current.next1))
                            {
                                current.next = current.next1;
                                currentOn1 = current.next1;
                            }
                            else
                            {   // should not happen, only when next on both grids is again on both grids
                                if (current.next2 != null)
                                {
                                    current.next = current.next2;
                                    currentOn2 = current.next2;
                                }
                                else if (current.next1 != null)
                                {
                                    current.next = current.next1;
                                    currentOn1 = current.next1;
                                }
                            }

                        }
                        else if (current.mode.HasFlag(LinkedIntersectionPoint.emode.in1))
                        {
                            if (currentOn2 == null && current.enterOnSurf1 != null)
                            {
                                foreach (LinkedIntersectionPoint s2 in surf1ips[current.enterOnSurf1])
                                {
                                    if (s2.mode.HasFlag(LinkedIntersectionPoint.emode.in2))
                                    {
                                        if (s2.prev2 == null)
                                        {
                                            currentOn2 = s2;
                                            break;
                                        }
                                        else if (current == cyclicalStartPoint)
                                        {
                                            LinkedIntersectionPoint s22 = s2;
                                            while (s22.prev2 != null && surf1ips[current.enterOnSurf1].Contains(s22.prev2)) s22 = s22.prev2;
                                            currentOn2 = s22;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (currentOn2 != null && current.enterOnSurf1 != null && surf1ips[current.enterOnSurf1].Contains(currentOn2))
                            {
                                current.next = currentOn2;
                                currentOn2 = currentOn2.next2;
                            }
                            else
                            {
                                current.next = current.next1;
                            }
                            currentOn1 = current.next1;
                        }
                        else if (current.mode.HasFlag(LinkedIntersectionPoint.emode.in2))
                        {
                            if (currentOn1 == null && current.enterOnSurf2 != null)
                            {
                                foreach (LinkedIntersectionPoint s1 in surf2ips[current.enterOnSurf2])
                                {
                                    if (s1.mode.HasFlag(LinkedIntersectionPoint.emode.in1))
                                    {
                                        if (s1.prev1 == null)
                                        {
                                            currentOn1 = s1;
                                            break;
                                        }
                                        else if (current == cyclicalStartPoint)
                                        {   // find first intersectionpoint on surface1 which is in the mesh of current
                                            LinkedIntersectionPoint s11 = s1;
                                            while (s11.prev1 != null && surf2ips[current.enterOnSurf2].Contains(s11.prev1)) s11 = s11.prev1;
                                            currentOn1 = s11;
                                            break;
                                        }
                                    }
                                }
                            }
                            if (currentOn1 != null && current.enterOnSurf2 != null && surf2ips[current.enterOnSurf2].Contains(currentOn1))
                            {
                                current.next = currentOn1;
                                currentOn1 = currentOn1.next1;
                            }
                            else
                            {
                                current.next = current.next2;
                            }
                            currentOn2 = current.next2;
                        }
                        current = current.next;
                        if (current != null && current == cyclicalStartPoint)
                        {
                            current = null;
                            // we have closed a loop, maybe there are more loops
                            foreach (LinkedIntersectionPoint lip in allIps)
                            {
                                if (lip.next == null)
                                {
                                    current = cyclicalStartPoint = lip;
                                    startPoints.Add(current);
                                    current.mode |= LinkedIntersectionPoint.emode.isCyclicalStart;
                                }
                            }
                        }
                    }
                }
                //                foreach (LinkedIntersectionPoint lip in allIps)
                //                {   // die Rückverkettung machen
                //                    if (lip.next != null)
                //                    {
                //                        if (lip.enterOnSurf1 != null && ((lip.enterOnSurf1.Item1 == 0) || lip.enterOnSurf1.Item1 == uknots1.Count)) continue;
                //                        if (lip.enterOnSurf1 != null && ((lip.enterOnSurf1.Item2 == 0) || lip.enterOnSurf1.Item2 == vknots1.Count)) continue;
                //                        if (lip.enterOnSurf2 != null && ((lip.enterOnSurf2.Item1 == 0) || lip.enterOnSurf2.Item1 == uknots2.Count)) continue;
                //                        if (lip.enterOnSurf2 != null && ((lip.enterOnSurf2.Item2 == 0) || lip.enterOnSurf2.Item2 == vknots2.Count)) continue;
                //#if DEBUG
                //                        System.Diagnostics.Trace.Assert(lip.next.prev == lip);
                //#endif
                //                    }
                //                }
                // die Seeds sind noch nicht einsortiert (es sei denn, sie sind mit einem Schnittpunkt identisch)
                // geschlossene Kurven sind noch nicht berücksichtigt!
                // List<LinkedIntersectionPoint> endPoints = new List<LinkedIntersectionPoint>();
                //foreach (LinkedIntersectionPoint lip in allIps)
                //{
                //    if (lip.prev == null && lip.mode != LinkedIntersectionPoint.emode.seed) startPoints.Add(lip);
                //    // if (lip.next == null) endPoints.Add(lip);
                //}
                //List<LinkedIntersectionPoint> singularSeeds = new List<LinkedIntersectionPoint>();
                //for (int i = 0; i < startPoints.Count; i++)
                //{
                //    if (startPoints[i].next == null && startPoints[i].mode == LinkedIntersectionPoint.emode.seed)
                //    {
                //        // ein einzelner seed, der nicht auf einer kante liegt und nicht eingebunden ist
                //        singularSeeds.Add(startPoints[i]);
                //    }
                //}
#if DEBUG
                foreach (LinkedIntersectionPoint lip in allIps)
                {
                    //if ((lip.mode & LinkedIntersectionPoint.emode.in1) != 0)
                    //{
                    //    if (lip.next1 != null)
                    //    {
                    //        Line2D l2d = new Line2D(lip.uv1, lip.next1.uv1);
                    //        dcuv1.Add(l2d, Color.Black, lip.id);
                    //    }
                    //}
                    //if ((lip.mode & LinkedIntersectionPoint.emode.in2) != 0)
                    //{
                    //    if (lip.next2 != null)
                    //    {
                    //        Line2D l2d = new Line2D(lip.uv2, lip.next2.uv2);
                    //        dcuv2.Add(l2d, Color.Black, lip.id);
                    //    }
                    //}
                }
                for (int i = 0; i < startPoints.Count; i++)
                {
                    List<GeoPoint> points = new List<GeoPoint>();
                    LinkedIntersectionPoint st = startPoints[i];
                    bool isClosed = st.mode.HasFlag(LinkedIntersectionPoint.emode.isCyclicalStart);
                    while (st != null)
                    {
                        points.Add(st.ip);
                        st = st.next;
                        if (st == startPoints[i]) break; // Schleife, geschlossen
                        if (points.Count > allIps.Count)
                        {
                            // das darf nicht vorkommen, hier ein BreakPoint
                            break;
                        }
                    }
                    if (points.Count > 1)
                    {
                        Polyline pl = Polyline.Construct();
                        pl.SetPoints(points.ToArray(), isClosed);
                        dc3d1.Add(pl, Color.Red);
                        dc3d2.Add(pl, Color.Red);
                    }
                }
                //List<LinkedIntersectionPoint> spon1 = new List<GeoObject.BoxedSurfaceEx.LinkedIntersectionPoint>();
                //foreach (LinkedIntersectionPoint lip in allIps)
                //{
                //    if (lip.mode.HasFlag(LinkedIntersectionPoint.emode.in1) && lip.prev1 == null) spon1.Add(lip);
                //    if (lip.next1 != null)
                //    {
                //        Line2D l2d = new Line2D(lip.uv1, lip.next1.uv1);
                //        dcuv1.Add(l2d.Trim(0.0, 0.9), Color.Orange, 1);
                //    }
                //    if (lip.next2 != null)
                //    {
                //        Line2D l2d = new Line2D(lip.uv2, lip.next2.uv2);
                //        dcuv2.Add(l2d.Trim(0.0, 0.9), Color.Orange, 1);
                //    }
                //}
#endif
                // folgender Test stellt fest, ob zwei Punkte unmittelbar einen Zyklus in einem der beiden uv-Netze bilden.
                // Das könnte eine fälschlische Verbindung zweier unabhängiger Schnittkurven sein
                foreach (LinkedIntersectionPoint lip in allIps)
                {
                    if (lip.next1 != null && lip.next1.next1 == lip)
                    {
                        // zwei aufeinanderfolgende sind zyklisch: Zwischenstufe einfügen
                        if (Math.Abs(lip.uv1.x - lip.next1.uv1.x) > Math.Abs(lip.uv1.y - lip.next1.uv1.y))
                        {
                            if (uVal1.Add((lip.uv1.x + lip.next1.uv1.x) / 2.0))
                            {
                                splitted = true;
                                break;
                            }
                        }
                        else
                        {
                            if (vVal1.Add((lip.uv1.y + lip.next1.uv1.y) / 2.0))
                            {
                                splitted = true;
                                break;
                            }
                        }
                    }
                    if (lip.next2 != null && lip.next2.next2 == lip)
                    {
                        // zwei aufeinanderfolgende sind zyklisch: Zwischenstufe einfügen
                        if (Math.Abs(lip.uv2.x - lip.next2.uv2.x) > Math.Abs(lip.uv2.y - lip.next2.uv2.y))
                        {
                            if (uVal2.Add((lip.uv2.x + lip.next2.uv2.x) / 2.0))
                            {
                                splitted = true;
                                break;
                            }
                        }
                        else
                        {
                            if (vVal2.Add((lip.uv2.y + lip.next2.uv2.y) / 2.0))
                            {
                                splitted = true;
                                break;
                            }
                        }
                    }
                }
                if (splitted) continue;

                for (int i = 0; i < startPoints.Count; i++)
                {
                    List<GeoPoint> points = new List<GeoPoint>();
                    List<GeoPoint2D> uvpoints1 = new List<GeoPoint2D>();
                    List<GeoPoint2D> uvpoints2 = new List<GeoPoint2D>();
                    LinkedIntersectionPoint st = startPoints[i];
                    double totlen = 0.0;
                    while (st != null)
                    {
                        if (points.Count > 0)
                        {
                            totlen += points[points.Count - 1] | st.ip;
                        }
                        points.Add(st.ip);
                        uvpoints1.Add(st.uv1);
                        uvpoints2.Add(st.uv2);
                        st = st.next;
                        if (st == startPoints[i]) break; // Schleife, geschlossen
                        if (points.Count > allIps.Count) break; // eine innere Schleife ist entstanden. das darf nicht passieren
                    }
                    if (points.Count > allIps.Count)
                    {   // eine innere Schleife, das ist nicht erlaubt. Auftrennen, dort wo das Segment nach innen trifft
                        Set<LinkedIntersectionPoint> connected = new Set<LinkedIntersectionPoint>();
                        st = startPoints[i];
                        LinkedIntersectionPoint last = null;
                        while (st != null)
                        {
                            if (connected.Contains(st))
                            {
                                if (last.mode.HasFlag(LinkedIntersectionPoint.emode.in1) && st.mode.HasFlag(LinkedIntersectionPoint.emode.in1))
                                {
                                    if (Math.Abs(last.uv1.x - st.uv1.x) > Math.Abs(last.uv1.y - st.uv1.y))
                                    {
                                        uVal1.Add((last.uv1.x + st.uv1.x) / 2.0);
                                        splitted = true;
                                        break;
                                    }
                                    else
                                    {
                                        vVal1.Add((last.uv1.y + st.uv1.y) / 2.0);
                                        splitted = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    if (Math.Abs(last.uv2.x - st.uv2.x) > Math.Abs(last.uv2.y - st.uv2.y))
                                    {
                                        uVal2.Add((last.uv2.x + st.uv2.x) / 2.0);
                                        splitted = true;
                                        break;
                                    }
                                    else
                                    {
                                        vVal2.Add((last.uv2.y + st.uv2.y) / 2.0);
                                        splitted = true;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                connected.Add(st);
                            }
                            last = st;
                            st = st.next;
                            if (st == startPoints[i]) break; // Schleife, geschlossen
                        }
                        if (splitted) break;
                    }
                    if (splitted) break;
                    if (points.Count > 1)
                    {
                        if (seeds != null && seeds.Count > 0 && !startPoints[i].mode.HasFlag(LinkedIntersectionPoint.emode.isCyclicalStart))
                        {   // einzelne seeds kurz vor oder nach der kurve noch anfügen. Im inneren ist es nicht nötig, die werden später mit PositionOf gefunden
                            Polyline2D p2d1 = new Polyline2D(uvpoints1.ToArray());
                            Polyline2D p2d2 = new Polyline2D(uvpoints2.ToArray());
                            for (int j = 0; j < seeds.Count; j++)
                            // for (int j = 0; j < singularSeeds.Count; j++)
                            {
                                GeoPoint2D uv1 = this.surface.PositionOf(seeds[j]);
                                SurfaceHelper.AdjustPeriodic(this.surface, thisBounds, ref uv1);
                                GeoPoint2D uv2 = other.PositionOf(seeds[j]);
                                SurfaceHelper.AdjustPeriodic(other, otherBounds, ref uv2);
                                double pos1 = p2d1.PositionOf(uv1);
                                double pos2 = p2d2.PositionOf(uv2);
                                double dp1 = uv1 | p2d1.PointAt(pos1);
                                double dp2 = uv2 | p2d2.PointAt(pos2);
                                if (dp1 < p2d1.Length * 1e-2 && dp2 < p2d2.Length * 1e-2)
                                {
                                    if (pos1 < 1e-10 && pos2 < 1e-10 && pos1 > -0.5 && pos2 > -0.5)
                                    {
                                        uvpoints1.Insert(0, uv1);
                                        uvpoints2.Insert(0, uv2);
                                        points.Insert(0, seeds[j]);
                                        if (j < seeds.Count - 1)
                                        {   // beim letzten mal natürlich nicht mehr nötig
                                            p2d1 = new Polyline2D(uvpoints1.ToArray()); // die Polylines müssen neu gemacht werden, denn zwei fast identische seeds hinter dem Ende werden sonst in zufälliger Reihenfolge eingefügt
                                            p2d2 = new Polyline2D(uvpoints2.ToArray());
                                        }
                                    }
                                    else if (pos1 > 1 - 1e-10 && pos2 > 1 - 1e-10 && pos1 < 1.5 && pos2 < 1.5)
                                    {
                                        uvpoints1.Add(uv1);
                                        uvpoints2.Add(uv2);
                                        points.Add(seeds[j]);
                                        if (j < seeds.Count - 1)
                                        {
                                            p2d1 = new Polyline2D(uvpoints1.ToArray()); // die Polylines müssen neu gemacht werden, denn zwei fast identische seeds hinter dem Ende werden sonst in zufälliger Reihenfolge eingefügt
                                            p2d2 = new Polyline2D(uvpoints2.ToArray());
                                        }
                                    }
                                }
                            }
                        }
                        double mindist = totlen * 0.001; // zu eng beieinanderliegende Punkt entfernen
                        for (int j = 0; j < points.Count - 1; j++)
                        {
                            double d = points[j] | points[j + 1];
                            if (d < mindist)
                            {
                                if (j == 0)
                                {
                                    points.RemoveAt(1);
                                    uvpoints1.RemoveAt(1);
                                    uvpoints2.RemoveAt(1);
                                }
                                else if (j == points.Count - 2)
                                {
                                    uvpoints1.RemoveAt(points.Count - 2);
                                    uvpoints2.RemoveAt(points.Count - 2);
                                    points.RemoveAt(points.Count - 2);
                                }
                                else
                                {
                                    if ((points[j] | points[j - 1]) < (points[j + 1] | points[j + 2]))
                                    {
                                        points.RemoveAt(j);
                                        uvpoints1.RemoveAt(j);
                                        uvpoints2.RemoveAt(j);
                                    }
                                    else
                                    {
                                        points.RemoveAt(j + 1);
                                        uvpoints1.RemoveAt(j + 1);
                                        uvpoints2.RemoveAt(j + 1);
                                    }
                                }
                                --j;
                            }
                        }
                        if (startPoints[i].mode.HasFlag(LinkedIntersectionPoint.emode.isCyclicalStart))
                        {   // a closed curve, repeat the first point
                            points.Add(points[0]);
                            uvpoints1.Add(uvpoints1[0]);
                            uvpoints2.Add(uvpoints2[0]);
                        }
                        res.Add(new InterpolatedDualSurfaceCurve(surface, thisBounds, other, otherBounds, points, uvpoints1, uvpoints2));
#if DEBUG
                        ICurve2D c2d = (res[0] as InterpolatedDualSurfaceCurve).CurveOnSurface1;
                        //c2d.PointAt(0.001);
                        //c2d.PointAt(0.999);
                        GeoPoint dbgp = res[0].PointAt(0.48);
#endif
                    }
                    if (splitted)
                    {
                        res.Clear();
                        continue;
                    }
                }
            }
            return res.ToArray();
        }

        private bool testAndChangeParameter(double val, SortedSet<double> ssd, double prec)
        {   // entfernt den Wert val aus dem Set (wenn er bis auf prec drinnen ist) und ersetzt ihn durch einen um prec veränderten
            SortedSet<double> found = ssd.GetViewBetween(val - prec, val + prec);
            if (found.Count == 0) return false;
            double foundval = found.Min; // hat sowieso nur einen
            double toAdd;
            if (foundval == ssd.Min) toAdd = foundval - 4 * prec;
            else if (foundval == ssd.Max) toAdd = foundval + 4 * prec;
            else if (val < foundval) toAdd = foundval + 4 * prec;
            else toAdd = foundval - 4 * prec;
            ssd.Remove(foundval); // diesen Wert jetzt korrigieren. Prec ist ja 1/1000 des intervalls, deshalb kann man das problemlos addieren/subtrahieren
            ssd.Add(toAdd);
            return true;
        }

        private double smallesDiff(SortedSet<double> ssd)
        {
            SortedSet<double>.Enumerator ssde = ssd.GetEnumerator();
            if (!ssde.MoveNext()) return 0.0;
            double last = ssde.Current;
            double res = double.MaxValue;
            while (ssde.MoveNext())
            {
                double d = ssde.Current;
                res = Math.Min(res, d - last);
                last = d;
            }
            return res;
        }

        private void addOrAdjust(SortedSet<double> ssd, double val)
        {   // add this new value to the set. If there is a similar value, replace it, except if it is a minimum or maximum, then don't add it
            if (val < ssd.Min || val > ssd.Max) return; // don't add a value outside the bounds
            double prec = (ssd.Max - ssd.Min) * 1e-4;
            SortedSet<double> found = ssd.GetViewBetween(val - prec, val + prec); // there should be only one at maximum
            if (found.Count == 0)
            {
                ssd.Add(val);
            }
            else
            {
                double foundval = found.Min; // there is probably only one
                if (foundval == ssd.Min) return; // don't modify the minimum
                else if (foundval == ssd.Max) return; // don't modify the maximum
                else
                {
                    ssd.Remove(foundval); // replace this one by the new value
                    ssd.Add(val);
                }
            }
        }

        internal ICurve[] IntersectOld(BoundingRect thisBounds, ISurface other, BoundingRect otherBounds, GeoPoint[] seeds)
        {
            // IntersectNew(thisBounds, other, otherBounds, seeds);
            // Idee: ohne Seed, liefert ggf mehrere Kurven
            // alle Boxen gegeneinander in schneiden: Die Kanten der Patches mit dem anderen Patch schneiden und umgekehrt:
            // kein Schittpunkt: es könnte eine innere geschlossene Kurve geben (zunächst ignorieren)
            // zwei Schnittpunkte: ein Kurvenstück für DualSurfaceCurve gefunden
            // mehrere Schnittpunkte (muss gerade Zahl sein): Patches unterteilen, bis jeweils nur zwei Punkte. Macht bei Berührung Probleme
            // Alle Schnipsel zusammenfassen
            BoxedSurfaceEx otherBS = (other as ISurfaceImpl).BoxedSurfaceEx;
            // gecachte Schnittpunkte, damit für jedes u bzw. v nur einmal die Schnitte mit der anderen Fläche berechnet werden müssen
            Dictionary<double, List<GeoPoint3d2d2d>> uInts = new Dictionary<double, List<GeoPoint3d2d2d>>();
            Dictionary<double, List<GeoPoint3d2d2d>> vInts = new Dictionary<double, List<GeoPoint3d2d2d>>();
            Dictionary<double, List<GeoPoint3d2d2d>> ouInts = new Dictionary<double, List<GeoPoint3d2d2d>>();
            Dictionary<double, List<GeoPoint3d2d2d>> ovInts = new Dictionary<double, List<GeoPoint3d2d2d>>();
#if DEBUG
            Set<ParEpi> usedForIntersection = new Set<ParEpi>();
            SortedSet<double> uVal = new SortedSet<double>();
            SortedSet<double> vVal = new SortedSet<double>();
            SortedSet<double> ouVal = new SortedSet<double>();
            SortedSet<double> ovVal = new SortedSet<double>();
#endif
            List<GeoPoint3d2d2d> ips = new List<GeoPoint3d2d2d>();
            foreach (ParEpi pe in octtree.GetAllObjects())
            {
                if (pe.uvPatch.Interferes(ref thisBounds))
                {
                    foreach (ParEpi otherPe in otherBS.octtree.GetObjectsCloseTo(pe))
                    {
                        if (otherPe.uvPatch.Interferes(ref otherBounds))
                        {
                            if (pe.Interferes(otherPe))
                            {
#if DEBUG
                                usedForIntersection.Add(pe);
                                usedForIntersection.Add(otherPe);
                                uVal.Add(pe.uvPatch.Left);
                                uVal.Add(pe.uvPatch.Right);
                                vVal.Add(pe.uvPatch.Bottom);
                                vVal.Add(pe.uvPatch.Top);
                                ouVal.Add(otherPe.uvPatch.Left);
                                ouVal.Add(otherPe.uvPatch.Right);
                                ovVal.Add(otherPe.uvPatch.Bottom);
                                ovVal.Add(otherPe.uvPatch.Top);
#endif

                                ips.AddRange(FindPatchIntersection(this, otherBS, pe, otherPe, uInts, vInts, ouInts, ovInts, thisBounds, otherBounds));
                            }
                        }
                    }
                }
            }
#if DEBUG
            DebuggerContainer dccrv = new DebuggerContainer();
            ColorDef cdt = new ColorDef("this", Color.Red);
            ColorDef cdo = new ColorDef("other", Color.Blue);
            foreach (double u in uVal)
            {
                ICurve crv = this.surface.FixedU(u, thisBounds.Bottom, thisBounds.Top);
                (crv as IColorDef).ColorDef = cdt;
                dccrv.Add(crv as IGeoObject, 0);
            }
            foreach (double v in vVal)
            {
                ICurve crv = this.surface.FixedV(v, thisBounds.Left, thisBounds.Right);
                (crv as IColorDef).ColorDef = cdt;
                dccrv.Add(crv as IGeoObject, 0);
            }
            foreach (double u in ouVal)
            {
                ICurve crv = other.FixedU(u, otherBounds.Bottom, otherBounds.Top);
                (crv as IColorDef).ColorDef = cdo;
                dccrv.Add(crv as IGeoObject, 1);
            }
            foreach (double v in ovVal)
            {
                ICurve crv = other.FixedV(v, otherBounds.Left, otherBounds.Right);
                (crv as IColorDef).ColorDef = cdo;
                dccrv.Add(crv as IGeoObject, 1);
            }
#endif
            // ips enthält jetzt in 2er Gruppen zusammenhängende Kurvenschnipsel, die jetzt noch sortiert werden müssen
            // wie sind die Schnipsel mit thisBounds/otherBounds verschnitten?
            // man kann in beiden uv-Systemen sortieren, hier erstmal quick and dirty
#if DEBUG
            DebuggerContainer dc0 = new DebuggerContainer();
            ColorDef green = new ColorDef("green", Color.Green);
            ColorDef red = new ColorDef("red", Color.Red);
            ColorDef blue = new ColorDef("blue", Color.Blue);
            ColorDef black = new ColorDef("black", Color.Black);
            GeoObjectList lst = Debug.toShow;
            for (int i = 0; i < lst.Count; i++)
            {
                (lst[i] as IColorDef).ColorDef = green;
            }
            dc0.Add(lst);
            foreach (ParEpi pe in usedForIntersection) // die sind dann doppelt drin
            {
                Solid sld = pe.AsBox;
                sld.ColorDef = black;
                //dc0.Add(sld);
            }
            lst = otherBS.Debug.toShow;
            for (int i = 0; i < lst.Count; i++)
            {
                (lst[i] as IColorDef).ColorDef = red;
            }
            dc0.Add(lst);
            for (int i = 0; i < ips.Count; i += 2)
            {
                Line l = Line.TwoPoints(ips[i].p3d, ips[i + 1].p3d);
                l.ColorDef = blue;
                dc0.Add(l, i);
            }
            DebuggerContainer dc3d = new DebuggerContainer();
            DebuggerContainer dc1 = new DebuggerContainer();
            foreach (ParEpi pe in octtree.GetAllObjects())
            {
                Polyline pl = Polyline.Construct();
                pl.SetRectangle(new GeoPoint(pe.uvPatch.GetLowerLeft()), pe.uvPatch.Width * GeoVector.XAxis, pe.uvPatch.Height * GeoVector.YAxis);
                pl.ColorDef = blue;
                dc1.Add(pl, pe.id);
            }
            foreach (KeyValuePair<double, List<GeoPoint3d2d2d>> item in uInts)
            {
                for (int j = 0; j < item.Value.Count; j++)
                {
                    Point pnt = Point.Construct();
                    pnt.Symbol = PointSymbol.Cross;
                    pnt.Location = new GeoPoint(item.Value[j].uv1);
                    pnt.ColorDef = green;
                    dc1.Add(pnt, j);
                    Point pnt3 = Point.Construct();
                    pnt3.Symbol = PointSymbol.Cross;
                    pnt3.Location = item.Value[j].p3d;
                    pnt3.ColorDef = green;
                    dc3d.Add(pnt3, 1000 + j);
                }
            }
            foreach (KeyValuePair<double, List<GeoPoint3d2d2d>> item in vInts)
            {
                for (int j = 0; j < item.Value.Count; j++)
                {
                    Point pnt = Point.Construct();
                    pnt.Symbol = PointSymbol.Cross;
                    pnt.Location = new GeoPoint(item.Value[j].uv1);
                    pnt.ColorDef = red;
                    dc1.Add(pnt, j);
                    Point pnt3 = Point.Construct();
                    pnt3.Symbol = PointSymbol.Cross;
                    pnt3.Location = item.Value[j].p3d;
                    pnt3.ColorDef = red;
                    dc3d.Add(pnt3, 2000 + j);
                }
            }

            DebuggerContainer dc2 = new DebuggerContainer();
            foreach (ParEpi pe in otherBS.octtree.GetAllObjects())
            {
                Polyline pl = Polyline.Construct();
                pl.SetRectangle(new GeoPoint(pe.uvPatch.GetLowerLeft()), pe.uvPatch.Width * GeoVector.XAxis, pe.uvPatch.Height * GeoVector.YAxis);
                pl.ColorDef = blue;
                dc2.Add(pl, pe.id);
            }
            foreach (KeyValuePair<double, List<GeoPoint3d2d2d>> item in ouInts)
            {
                for (int j = 0; j < item.Value.Count; j++)
                {
                    Point pnt = Point.Construct();
                    pnt.Symbol = PointSymbol.Cross;
                    pnt.Location = new GeoPoint(item.Value[j].uv2);
                    pnt.ColorDef = green;
                    dc2.Add(pnt, j);
                    Point pnt3 = Point.Construct();
                    pnt3.Symbol = PointSymbol.Cross;
                    pnt3.Location = item.Value[j].p3d;
                    pnt3.ColorDef = green;
                    dc3d.Add(pnt3, 3000 + j);
                }
            }
            foreach (KeyValuePair<double, List<GeoPoint3d2d2d>> item in ovInts)
            {
                for (int j = 0; j < item.Value.Count; j++)
                {
                    Point pnt = Point.Construct();
                    pnt.Symbol = PointSymbol.Cross;
                    pnt.Location = new GeoPoint(item.Value[j].uv2);
                    pnt.ColorDef = red;
                    dc2.Add(pnt, j);
                    Point pnt3 = Point.Construct();
                    pnt3.Symbol = PointSymbol.Cross;
                    pnt3.Location = item.Value[j].p3d;
                    pnt3.ColorDef = red;
                    dc3d.Add(pnt3, 4000 + j);
                }
            }
            DebuggerContainer dc4 = new DebuggerContainer();
            {
                foreach (KeyValuePair<double, List<GeoPoint3d2d2d>> item in uInts)
                {
                    ICurve crv = this.surface.FixedU(item.Key, thisBounds.Bottom, thisBounds.Top);
                    dc4.Add(crv as IGeoObject, 3);
                }
                foreach (KeyValuePair<double, List<GeoPoint3d2d2d>> item in ovInts)
                {
                    ICurve crv = this.surface.FixedV(item.Key, thisBounds.Left, thisBounds.Right);
                    dc4.Add(crv as IGeoObject, 3);
                }
                foreach (KeyValuePair<double, List<GeoPoint3d2d2d>> item in ouInts)
                {
                    ICurve crv = other.FixedU(item.Key, otherBounds.Bottom, otherBounds.Top);
                    dc4.Add(crv as IGeoObject, 3);
                }
                foreach (KeyValuePair<double, List<GeoPoint3d2d2d>> item in ovInts)
                {
                    ICurve crv = other.FixedV(item.Key, otherBounds.Left, otherBounds.Right);
                    dc4.Add(crv as IGeoObject, 3);
                }
            }

#endif

            if (ips.Count > 0)
            {
                List<List<GeoPoint3d2d2d>> curveParts = new List<List<GeoPoint3d2d2d>>();
                List<GeoPoint3d2d2d> currentList = new List<GeoPoint3d2d2d>();
                GeoPoint3d2d2d sp = ips[0];
                GeoPoint3d2d2d ep = ips[1];
                ips.RemoveRange(0, 2); // oder besser von hinten?
                currentList.Add(sp);
                currentList.Add(ep);
                while (ips.Count >= 2)
                {
                    bool found = false;
                    for (int i = 0; i < ips.Count; i++)
                    {
                        if (ips[i].Equals(currentList[0]))
                        {   // vorne anfügen
                            if ((i & 0x1) == 0)
                            {   // gerader Index, der und der nächste gilt
                                currentList.Insert(0, ips[i]);
                                currentList.Insert(0, ips[i + 1]);
                                ips.RemoveRange(i, 2);
                                found = true;
                                break;
                            }
                            else
                            {
                                currentList.Insert(0, ips[i]);
                                currentList.Insert(0, ips[i - 1]);
                                ips.RemoveRange(i - 1, 2);
                                found = true;
                                break;
                            }
                        }
                        if (ips[i].Equals(currentList[currentList.Count - 1]))
                        {   // vorne anfügen
                            if ((i & 0x1) == 0)
                            {   // gerader Index, der und der nächste gilt
                                currentList.Add(ips[i]);
                                currentList.Add(ips[i + 1]);
                                ips.RemoveRange(i, 2);
                                found = true;
                                break;
                            }
                            else
                            {
                                currentList.Add(ips[i]);
                                currentList.Add(ips[i - 1]);
                                ips.RemoveRange(i - 1, 2);
                                found = true;
                                break;
                            }
                        }
                    }
                    if (!found)
                    {
                        curveParts.Add(currentList);
                        currentList = new List<GeoPoint3d2d2d>();
                        currentList.Add(ips[0]);
                        currentList.Add(ips[1]);
                        ips.RemoveRange(0, 2);
                    }
                }
                curveParts.Add(currentList); // anthält jetzt alle Stücke
                bool concatenated = true;
                while (curveParts.Count > 1 && concatenated)
                {   // wenn zwei Gitterlinien der beiden uv-Aufteilungen sich fast schneiden, dann kann es eine Lücke geben. Diese wird hier geschlossen
                    // das ist unschön, es könnte vermutlich gelöst werden, wenn zuerst die Schnittpunkte der Gitterlinien mit der jeweils anderen fläche bestimmt würden (uInts u.s.w)
                    // und dann sehr nahe beieinaderliegende punkte zusammengefasst würden
                    concatenated = false;
                    double eps = (GetRawExtent().Size + otherBS.GetRawExtent().Size) * 1e-5;
                    for (int i = 0; i < curveParts.Count; i++)
                    {
                        for (int j = 0; j < curveParts.Count; j++)
                        {
                            if (i != j)
                            {
                                if ((curveParts[i][0].p3d | curveParts[j][0].p3d) < eps)
                                {
                                    curveParts[i].Reverse();
                                    curveParts[i].AddRange(curveParts[j]);
                                    curveParts.RemoveAt(j);
                                    concatenated = true;
                                    break;
                                }
                                if ((curveParts[i][curveParts[i].Count - 1].p3d | curveParts[j][0].p3d) < eps)
                                {
                                    curveParts[i].AddRange(curveParts[j]);
                                    curveParts.RemoveAt(j);
                                    concatenated = true;
                                    break;
                                }
                                if ((curveParts[i][0].p3d | curveParts[j][curveParts[j].Count - 1].p3d) < eps)
                                {
                                    curveParts[i].InsertRange(0, curveParts[j]);
                                    curveParts.RemoveAt(j);
                                    concatenated = true;
                                    break;
                                }
                                if ((curveParts[i][curveParts[i].Count - 1].p3d | curveParts[j][curveParts[j].Count - 1].p3d) < eps)
                                {
                                    curveParts[j].Reverse();
                                    curveParts[i].AddRange(curveParts[j]);
                                    curveParts.RemoveAt(j);
                                    concatenated = true;
                                    break;
                                }
                            }
                        }
                        if (concatenated) break;
                    }
                }
                List<ICurve> res = new List<GeoObject.ICurve>();
                for (int i = 0; i < curveParts.Count; i++)
                {
                    List<GeoPoint> pnts = new List<GeoPoint>();
                    for (int j = 0; j < curveParts[i].Count; j++)
                    {
                        if (j == 0 || !Precision.IsEqual(pnts[pnts.Count - 1], curveParts[i][j].p3d))
                            pnts.Add(curveParts[i][j].p3d);
                    }
                    if (pnts.Count > 1) res.Add(new InterpolatedDualSurfaceCurve(surface, thisBounds, other, otherBounds, pnts));
                }
                return res.ToArray();
            }
            return new ICurve[0];
        }

        private static List<GeoPoint3d2d2d> FindPatchIntersection(BoxedSurfaceEx bs1, BoxedSurfaceEx bs2, ParEpi pe1, ParEpi pe2, Dictionary<double, List<GeoPoint3d2d2d>> uInts1, Dictionary<double, List<GeoPoint3d2d2d>> vInts1, Dictionary<double, List<GeoPoint3d2d2d>> uInts2, Dictionary<double, List<GeoPoint3d2d2d>> vInts2, BoundingRect bounds1, BoundingRect bounds2)
        {
            // schneide die 4 Kanten dieses Patches mit dem anderen Patch
            List<GeoPoint3d2d2d> res = new List<GeoPoint3d2d2d>();// uv1 ist für bs1, uv2 für bs2
            List<GeoPoint3d2d2d> ips322;
            GeoPoint[] ips;
            GeoPoint2D[] uvOnFaces;
            double[] uOnCurve3Ds;
            double up1 = 0.0, vp1 = 0.0, up2 = 0.0, vp2 = 0.0; // die Perioden
            if (bs1.surface.IsUPeriodic) up1 = bs1.surface.UPeriod;
            if (bs1.surface.IsVPeriodic) vp1 = bs1.surface.VPeriod;
            if (bs2.surface.IsUPeriodic) up2 = bs2.surface.UPeriod;
            if (bs2.surface.IsVPeriodic) vp2 = bs2.surface.VPeriod;
            BoundingRect pe1uvPatch = pe1.uvPatch;
            BoundingRect pe2uvPatch = pe2.uvPatch;
            pe1uvPatch.Inflate(pe1uvPatch.Width * 1e-5, pe1uvPatch.Height * 1e-5);
            pe2uvPatch.Inflate(pe2uvPatch.Width * 1e-5, pe2uvPatch.Height * 1e-5);
            if (!uInts1.TryGetValue(pe1.uvPatch.Left, out ips322))
            {
                // wir verwenden hier die surface.Intersect Methode, da boxedSurface immer gleich iteriert
                bs2.surface.Intersect(bs1.surface.FixedU(pe1.uvPatch.Left, bounds1.Bottom, bounds1.Top), bounds2, out ips, out uvOnFaces, out uOnCurve3Ds);
                ips322 = new List<GeoPoint3d2d2d>();
                uInts1[pe1.uvPatch.Left] = ips322;
                for (int i = 0; i < ips.Length; i++)
                {
                    GeoPoint2D uv = bs1.surface.PositionOf(ips[i]);
                    uv.x = pe1.uvPatch.Left; // angleichen wg. Vergleich
                    SurfaceHelper.AdjustPeriodic(bs1.surface, pe1.uvPatch, ref uv);
                    ips322.Add(new GeoPoint3d2d2d(ips[i], uv, uvOnFaces[i]));
                }
            }
            for (int i = 0; i < ips322.Count; i++)
            {
                if (pe1uvPatch.ContainsPeriodic(ips322[i].uv1, up1, vp1) && pe2uvPatch.ContainsPeriodic(ips322[i].uv2, up2, vp2)) res.Add(ips322[i]);
            }
            if (!uInts1.TryGetValue(pe1.uvPatch.Right, out ips322))
            {
                bs2.surface.Intersect(bs1.surface.FixedU(pe1.uvPatch.Right, bounds1.Bottom, bounds1.Top), bounds2, out ips, out uvOnFaces, out uOnCurve3Ds);
                ips322 = new List<GeoPoint3d2d2d>();
                uInts1[pe1.uvPatch.Right] = ips322;
                for (int i = 0; i < ips.Length; i++)
                {
                    GeoPoint2D uv = bs1.surface.PositionOf(ips[i]);
                    uv.x = pe1.uvPatch.Right; // angleichen wg. Vergleich
                    SurfaceHelper.AdjustPeriodic(bs1.surface, pe1.uvPatch, ref uv);
                    ips322.Add(new GeoPoint3d2d2d(ips[i], uv, uvOnFaces[i]));
                }
            }
            for (int i = 0; i < ips322.Count; i++)
            {
                if (pe1uvPatch.ContainsPeriodic(ips322[i].uv1, up1, vp1) && pe2uvPatch.ContainsPeriodic(ips322[i].uv2, up2, vp2)) res.Add(ips322[i]);
            }
            if (!vInts1.TryGetValue(pe1.uvPatch.Bottom, out ips322))
            {
                bs2.surface.Intersect(bs1.surface.FixedV(pe1.uvPatch.Bottom, bounds1.Left, bounds1.Right), bounds2, out ips, out uvOnFaces, out uOnCurve3Ds);
                ips322 = new List<GeoPoint3d2d2d>();
                vInts1[pe1.uvPatch.Bottom] = ips322;
                for (int i = 0; i < ips.Length; i++)
                {
                    GeoPoint2D uv = bs1.surface.PositionOf(ips[i]);
                    uv.y = pe1.uvPatch.Bottom; // angleichen wg. Vergleich
                    SurfaceHelper.AdjustPeriodic(bs1.surface, pe1.uvPatch, ref uv);
                    ips322.Add(new GeoPoint3d2d2d(ips[i], uv, uvOnFaces[i]));
                }
            }
            for (int i = 0; i < ips322.Count; i++)
            {
                if (pe1uvPatch.ContainsPeriodic(ips322[i].uv1, up1, vp1) && pe2uvPatch.ContainsPeriodic(ips322[i].uv2, up2, vp2)) res.Add(ips322[i]);
            }
            if (!vInts1.TryGetValue(pe1.uvPatch.Top, out ips322))
            {
                bs2.surface.Intersect(bs1.surface.FixedV(pe1.uvPatch.Top, bounds1.Left, bounds1.Right), bounds2, out ips, out uvOnFaces, out uOnCurve3Ds);
                ips322 = new List<GeoPoint3d2d2d>();
                vInts1[pe1.uvPatch.Top] = ips322;
                for (int i = 0; i < ips.Length; i++)
                {
                    GeoPoint2D uv = bs1.surface.PositionOf(ips[i]);
                    uv.y = pe1.uvPatch.Top; // angleichen wg. Vergleich
                    SurfaceHelper.AdjustPeriodic(bs1.surface, pe1.uvPatch, ref uv);
                    ips322.Add(new GeoPoint3d2d2d(ips[i], uv, uvOnFaces[i]));
                }
            }
            for (int i = 0; i < ips322.Count; i++)
            {
                if (pe1uvPatch.ContainsPeriodic(ips322[i].uv1, up1, vp1) && pe2uvPatch.ContainsPeriodic(ips322[i].uv2, up2, vp2)) res.Add(ips322[i]);
            }
            // jetzt mit vertauschten Rollen:
            if (!uInts2.TryGetValue(pe2.uvPatch.Left, out ips322))
            {
                bs1.surface.Intersect(bs2.surface.FixedU(pe2.uvPatch.Left, bounds2.Bottom, bounds2.Top), bounds1, out ips, out uvOnFaces, out uOnCurve3Ds);
                ips322 = new List<GeoPoint3d2d2d>();
                uInts2[pe2.uvPatch.Left] = ips322;
                for (int i = 0; i < ips.Length; i++)
                {
                    GeoPoint2D uv = bs2.surface.PositionOf(ips[i]);
                    uv.x = pe2.uvPatch.Left; // angleichen wg. Vergleich
                    SurfaceHelper.AdjustPeriodic(bs2.surface, pe2.uvPatch, ref uv);
                    ips322.Add(new GeoPoint3d2d2d(ips[i], uvOnFaces[i], uv));
                }
            }
            for (int i = 0; i < ips322.Count; i++)
            {
                if (pe1uvPatch.ContainsPeriodic(ips322[i].uv1, up1, vp1) && pe2uvPatch.ContainsPeriodic(ips322[i].uv2, up2, vp2)) res.Add(ips322[i]);
            }
            if (!uInts2.TryGetValue(pe2.uvPatch.Right, out ips322))
            {
                bs1.surface.Intersect(bs2.surface.FixedU(pe2.uvPatch.Right, bounds2.Bottom, bounds2.Top), bounds1, out ips, out uvOnFaces, out uOnCurve3Ds);
                ips322 = new List<GeoPoint3d2d2d>();
                uInts2[pe2.uvPatch.Right] = ips322;
                for (int i = 0; i < ips.Length; i++)
                {
                    GeoPoint2D uv = bs2.surface.PositionOf(ips[i]);
                    uv.x = pe2.uvPatch.Right; // angleichen wg. Vergleich
                    SurfaceHelper.AdjustPeriodic(bs2.surface, pe2.uvPatch, ref uv);
                    ips322.Add(new GeoPoint3d2d2d(ips[i], uvOnFaces[i], uv));
                }
            }
            for (int i = 0; i < ips322.Count; i++)
            {
                if (pe1uvPatch.ContainsPeriodic(ips322[i].uv1, up1, vp1) && pe2uvPatch.ContainsPeriodic(ips322[i].uv2, up2, vp2)) res.Add(ips322[i]);
            }
            if (!vInts2.TryGetValue(pe2.uvPatch.Bottom, out ips322))
            {
                bs1.surface.Intersect(bs2.surface.FixedV(pe2.uvPatch.Bottom, bounds2.Left, bounds2.Right), bounds1, out ips, out uvOnFaces, out uOnCurve3Ds);
                ips322 = new List<GeoPoint3d2d2d>();
                vInts2[pe2.uvPatch.Bottom] = ips322;
                for (int i = 0; i < ips.Length; i++)
                {
                    GeoPoint2D uv = bs2.surface.PositionOf(ips[i]);
                    uv.y = pe2.uvPatch.Bottom; // angleichen wg. Vergleich
                    SurfaceHelper.AdjustPeriodic(bs2.surface, pe2.uvPatch, ref uv);
                    ips322.Add(new GeoPoint3d2d2d(ips[i], uvOnFaces[i], uv));
                }
            }
            for (int i = 0; i < ips322.Count; i++)
            {
                GeoPoint2D dbg = bs2.surface.PositionOf(ips322[i].p3d);
                if (pe1uvPatch.ContainsPeriodic(ips322[i].uv1, up1, vp1) && pe2uvPatch.ContainsPeriodic(ips322[i].uv2, up2, vp2)) res.Add(ips322[i]);
            }
            if (!vInts2.TryGetValue(pe2.uvPatch.Top, out ips322))
            {
                bs1.surface.Intersect(bs2.surface.FixedV(pe2.uvPatch.Top, bounds2.Left, bounds2.Right), bounds1, out ips, out uvOnFaces, out uOnCurve3Ds);
                ips322 = new List<GeoPoint3d2d2d>();
                vInts2[pe2.uvPatch.Top] = ips322;
                for (int i = 0; i < ips.Length; i++)
                {
                    GeoPoint2D uv = bs2.surface.PositionOf(ips[i]);
                    uv.y = pe2.uvPatch.Top; // angleichen wg. Vergleich
                    SurfaceHelper.AdjustPeriodic(bs2.surface, pe2.uvPatch, ref uv);
                    ips322.Add(new GeoPoint3d2d2d(ips[i], uvOnFaces[i], uv));
                }
            }
            for (int i = 0; i < ips322.Count; i++)
            {
                if (pe1uvPatch.ContainsPeriodic(ips322[i].uv1, up1, vp1) && pe2uvPatch.ContainsPeriodic(ips322[i].uv2, up2, vp2)) res.Add(ips322[i]);
            }
#if DEBUGx
            Face fc1 = Face.MakeFace(bs1.surface, new SimpleShape(Border.MakeRectangle(pe1.uvPatch)));
            Face fc2 = Face.MakeFace(bs2.surface, new SimpleShape(Border.MakeRectangle(pe2.uvPatch)));
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(fc1);
            dc.Add(fc2);
            for (int i = 0; i < res.Count; i++)
            {
                dc.Add(res[i].p3d, Color.Blue, i);
            }
#endif
            // in res müsste man identische entfernen, solche, die ge nau auf Ecken der Patches fallen
            double prec = (pe1.Size + pe2.Size) * 1e-5; // auf 1e-7 geändert, da zuviel entfernt wurde. Das Resultat hatte dann eine Lücke, sehr schlecht für BRep! Braucht man das überhaupt?
            for (int i = res.Count - 1; i > 0; --i)
            {
                for (int j = 0; j < i; j++)
                {
                    if ((res[i].p3d | res[j].p3d) < prec)
                    {
                        res.RemoveAt(i);
                        break;
                    }
                }
            }
            if (res.Count == 0 || res.Count == 2) return res;
            if (res.Count == 1)
            {   // genau ein Eck gestreift
                res.Clear();
                return res;
            }
            // wenn es mehr als 2 sind, dann müssten sie entsprechend sortiert sein. Jeweils 2 aufeinanderfolgende sind ein zusammenhängendes Kurvenstück
            // Durch rekursiven Aufruf werden immer nur 2er Päckchen geliefert, wenn auch mehrere, die sind aber dann sortiert
            // Berührungen machen Probleme!!!
            ParEpi[] sub1 = bs1.SubCubes(pe1);
            ParEpi[] sub2 = bs2.SubCubes(pe2);
            res.Clear();
            for (int i = 0; i < sub1.Length; i++)
            {
                for (int j = 0; j < sub2.Length; j++)
                {
                    if (sub1[i].Interferes(sub2[j]))
                    {
                        res.AddRange(FindPatchIntersection(bs1, bs2, sub1[i], sub2[j], uInts1, vInts1, uInts2, vInts2, bounds1, bounds2));
                    }
                }
            }
            return res;
        }
        internal GeoPoint2D[] PositionOfNormal(GeoVector normal)
        {
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            foreach (ParEpi pe in octtree.GetAllObjects())
            {
                bool check = false; // check, whether normal is in the span of normals of this patch
                GeoVector span = Geometry.ReBase(normal, pe.nll, pe.nlr, pe.nur);
                if ((span.x >= 0 && span.y >= 0 && span.z >= 0) || (span.x <= 0 && span.y <= 0 && span.z <= 0)) check = true;
                if (!check)
                {
                    span = Geometry.ReBase(normal, pe.nll, pe.nur, pe.nul);
                    if ((span.x >= 0 && span.y >= 0 && span.z >= 0) || (span.x <= 0 && span.y <= 0 && span.z <= 0)) check = true;
                }
                if (check)
                {
                    // to implement: use GaussNewtonMinimizer to calculate a uv value, where normal is normal to the surface
                }
            }
            return res.ToArray();
        }


    }

    public class SurfaceHelper
    {
        public IEnumerator<ICurve> BoundingCurves(ISurface srf, BoundingRect ext)
        {
            yield return srf.FixedU(ext.Left, ext.Bottom, ext.Top);
            yield return srf.FixedU(ext.Right, ext.Bottom, ext.Top);
            yield return srf.FixedV(ext.Bottom, ext.Left, ext.Right);
            yield return srf.FixedV(ext.Top, ext.Left, ext.Right);

        }
        public static void AdjustPeriodic(double uperiod, double vperiod, ref GeoPoint2D p)
        {
            if (uperiod > 0.0)
            {
                while (p.x > uperiod) p.x -= uperiod;
                while (p.x < 0.0) p.x += uperiod;
            }
            if (vperiod > 0.0)
            {
                while (p.y > vperiod) p.y -= vperiod;
                while (p.y < 0.0) p.y += vperiod;
            }
        }
        internal static void AdjustPeriodic(ISurface surface, BoundingRect bounds, SimpleShape ss)
        {
            if (surface.IsUPeriodic || surface.IsVPeriodic)
            {
                GeoPoint2D mp = ss.GetExtent().GetCenter();
                double du = 0.0, dv = 0.0;
                if (surface.IsUPeriodic)
                {
                    double um = (bounds.Left + bounds.Right) / 2;
                    while (Math.Abs(mp.x + du - um) > Math.Abs(mp.x + du - surface.UPeriod - um)) du -= surface.UPeriod;
                    while (Math.Abs(mp.x + du - um) > Math.Abs(mp.x + du + surface.UPeriod - um)) du += surface.UPeriod;
                }
                if (surface.IsVPeriodic)
                {
                    double vm = (bounds.Bottom + bounds.Top) / 2;
                    while (Math.Abs(mp.y + dv - vm) > Math.Abs(mp.y + dv - surface.VPeriod - vm)) dv -= surface.VPeriod;
                    while (Math.Abs(mp.y + dv - vm) > Math.Abs(mp.y + dv + surface.VPeriod - vm)) dv += surface.VPeriod;
                }
                if (du != 0.0 || dv != 0.0)
                {
                    ss.Move(du, dv);
                }
            }

        }
        internal static (double du, double dv) AdjustPeriodic(ISurface surface, BoundingRect bounds, Border bdr)
        {
            if (surface.IsUPeriodic || surface.IsVPeriodic)
            {
                GeoPoint2D mp = bdr.Extent.GetCenter();
                double du = 0.0, dv = 0.0;
                if (surface.IsUPeriodic)
                {
                    double um = (bounds.Left + bounds.Right) / 2;
                    while (Math.Abs(mp.x + du - um) > Math.Abs(mp.x + du - surface.UPeriod - um)) du -= surface.UPeriod;
                    while (Math.Abs(mp.x + du - um) > Math.Abs(mp.x + du + surface.UPeriod - um)) du += surface.UPeriod;
                }
                if (surface.IsVPeriodic)
                {
                    double vm = (bounds.Bottom + bounds.Top) / 2;
                    while (Math.Abs(mp.y + dv - vm) > Math.Abs(mp.y + dv - surface.VPeriod - vm)) dv -= surface.VPeriod;
                    while (Math.Abs(mp.y + dv - vm) > Math.Abs(mp.y + dv + surface.VPeriod - vm)) dv += surface.VPeriod;
                }
                if (du != 0.0 || dv != 0.0)
                {
                    bdr.Move(du, dv);
                }
                return (du, dv);
            }
            return (0, 0);
        }
        public static void AdjustPeriodic(ISurface surface, BoundingRect bounds, ICurve2D cv2d)
        {
            if (surface.IsUPeriodic || surface.IsVPeriodic)
            {
                GeoPoint2D mp = cv2d.PointAt(0.5);
                double du = 0.0, dv = 0.0;
                if (surface.IsUPeriodic)
                {
                    double um = (bounds.Left + bounds.Right) / 2;
                    while (Math.Abs(mp.x + du - um) > Math.Abs(mp.x + du - surface.UPeriod - um)) du -= surface.UPeriod;
                    while (Math.Abs(mp.x + du - um) > Math.Abs(mp.x + du + surface.UPeriod - um)) du += surface.UPeriod;
                }
                if (surface.IsVPeriodic)
                {
                    double vm = (bounds.Bottom + bounds.Top) / 2;
                    while (Math.Abs(mp.y + dv - vm) > Math.Abs(mp.y + dv - surface.VPeriod - vm)) dv -= surface.VPeriod;
                    while (Math.Abs(mp.y + dv - vm) > Math.Abs(mp.y + dv + surface.VPeriod - vm)) dv += surface.VPeriod;
                }
                if (du != 0.0 || dv != 0.0)
                {
                    cv2d.Move(du, dv);
                }
            }
        }
        internal static void AdjustPeriodicStartPoint(ISurface surface, GeoPoint2D startPoint, ICurve2D cv2d)
        {
            if (surface.IsUPeriodic || surface.IsVPeriodic)
            {
                GeoPoint2D sp = cv2d.StartPoint;
                double du = 0.0, dv = 0.0;
                if (surface.IsUPeriodic)
                {
                    double spx = startPoint.x;
                    while (Math.Abs(sp.x + du - spx) > Math.Abs(sp.x + du - surface.UPeriod - spx)) du -= surface.UPeriod;
                    while (Math.Abs(sp.x + du - spx) > Math.Abs(sp.x + du + surface.UPeriod - spx)) du += surface.UPeriod;
                }
                if (surface.IsVPeriodic)
                {
                    double spy = startPoint.y;
                    while (Math.Abs(sp.y + dv - spy) > Math.Abs(sp.y + dv - surface.VPeriod - spy)) dv -= surface.VPeriod;
                    while (Math.Abs(sp.y + dv - spy) > Math.Abs(sp.y + dv + surface.VPeriod - spy)) dv += surface.VPeriod;
                }
                if (du != 0.0 || dv != 0.0)
                {
                    cv2d.Move(du, dv);
                }
            }
        }
        internal static void AdjustPeriodicStartPoint(ISurface surface, GeoPoint2D startPoint, ref GeoPoint2D toAdjust)
        {
            if (surface.IsUPeriodic || surface.IsVPeriodic)
            {
                double du = 0.0, dv = 0.0;
                if (surface.IsUPeriodic)
                {
                    double spx = startPoint.x;
                    while (Math.Abs(toAdjust.x + du - spx) > Math.Abs(toAdjust.x + du - surface.UPeriod - spx)) du -= surface.UPeriod;
                    while (Math.Abs(toAdjust.x + du - spx) > Math.Abs(toAdjust.x + du + surface.UPeriod - spx)) du += surface.UPeriod;
                }
                if (surface.IsVPeriodic)
                {
                    double spy = startPoint.y;
                    while (Math.Abs(toAdjust.y + dv - spy) > Math.Abs(toAdjust.y + dv - surface.VPeriod - spy)) dv -= surface.VPeriod;
                    while (Math.Abs(toAdjust.y + dv - spy) > Math.Abs(toAdjust.y + dv + surface.VPeriod - spy)) dv += surface.VPeriod;
                }
                if (du != 0.0 || dv != 0.0)
                {
                    toAdjust.x += du;
                    toAdjust.y += dv;
                }
            }
        }

        internal static void AdjustPeriodic(ISurface surface, BoundingRect bounds, GeoPoint2D[] points)
        {
            if (surface.IsUPeriodic || surface.IsVPeriodic)
            {
                GeoPoint2D mp = points[points.Length / 2]; // der mittlere Punkt
                double du = 0.0, dv = 0.0;
                if (surface.IsUPeriodic)
                {
                    double um = (bounds.Left + bounds.Right) / 2;
                    while (Math.Abs(mp.x + du - um) > Math.Abs(mp.x + du - surface.UPeriod - um)) du -= surface.UPeriod;
                    while (Math.Abs(mp.x + du - um) > Math.Abs(mp.x + du + surface.UPeriod - um)) du += surface.UPeriod;
                }
                if (surface.IsVPeriodic)
                {
                    double vm = (bounds.Bottom + bounds.Top) / 2;
                    while (Math.Abs(mp.y + dv - vm) > Math.Abs(mp.y + dv - surface.VPeriod - vm)) dv -= surface.VPeriod;
                    while (Math.Abs(mp.y + dv - vm) > Math.Abs(mp.y + dv + surface.VPeriod - vm)) dv += surface.VPeriod;
                }
                if (du != 0.0 || dv != 0.0)
                {
                    GeoVector2D d = new GeoVector2D(du, dv);
                    for (int i = 0; i < points.Length; i++)
                    {
                        points[i] += d;
                    }
                }
            }
        }
        public static void AdjustPeriodic(ISurface surface, BoundingRect bounds, ref GeoPoint2D p2d)
        {
            if (surface.IsUPeriodic || surface.IsVPeriodic)
            {
                if (surface.IsUPeriodic)
                {
                    double um = (bounds.Left + bounds.Right) / 2;
                    while (Math.Abs(p2d.x - um) > Math.Abs(p2d.x - surface.UPeriod - um)) p2d.x -= surface.UPeriod;
                    while (Math.Abs(p2d.x - um) > Math.Abs(p2d.x + surface.UPeriod - um)) p2d.x += surface.UPeriod;
                }
                if (surface.IsVPeriodic)
                {
                    double vm = (bounds.Bottom + bounds.Top) / 2;
                    while (Math.Abs(p2d.y - vm) > Math.Abs(p2d.y - surface.VPeriod - vm)) p2d.y -= surface.VPeriod;
                    while (Math.Abs(p2d.y - vm) > Math.Abs(p2d.y + surface.VPeriod - vm)) p2d.y += surface.VPeriod;
                }
            }
        }
        internal static void AdjustUPeriodic(ISurface surface, BoundingRect bounds, ref double u)
        {
            if (surface.IsUPeriodic)
            {
                double um = (bounds.Left + bounds.Right) / 2;
                while (Math.Abs(u - um) > Math.Abs(u - surface.UPeriod - um)) u -= surface.UPeriod;
                while (Math.Abs(u - um) > Math.Abs(u + surface.UPeriod - um)) u += surface.UPeriod;
            }
        }
        internal static void AdjustUPeriodic(ISurface surface, double umin, double umax, ref double u)
        {
            if (surface.IsUPeriodic)
            {
                double um = (umin + umax) / 2;
                while (Math.Abs(u - um) > Math.Abs(u - surface.UPeriod - um)) u -= surface.UPeriod;
                while (Math.Abs(u - um) > Math.Abs(u + surface.UPeriod - um)) u += surface.UPeriod;
            }
        }

        public static void MinMaxCurvature(ISurface surface, GeoPoint2D uv, out ICurve minCurvature, out ICurve maxCurvature)
        {   // to be tested
            surface.Derivation2At(uv, out GeoPoint location, out GeoVector du, out GeoVector dv, out GeoVector duu, out GeoVector dvv, out GeoVector duv);
            Matrix I = DenseMatrix.Create(2, 2, 0);
            I[0, 0] = du * du;
            I[0, 1] = I[1, 0] = du * dv;
            I[1, 1] = dv * dv;
            GeoVector n = (du ^ dv).Normalized;
            Matrix II = DenseMatrix.Create(2, 2, 0);
            II[0, 0] = duu * n;
            II[0, 1] = I[1, 0] = duv * n;
            II[1, 1] = dvv * n;
            Matrix S = (Matrix)I.Inverse().Multiply(II); // Weingarten
            Evd<double> evd = S.Evd();
            Vector<System.Numerics.Complex> eigenValues = evd.EigenValues;
            Matrix eigenVectors = (Matrix)evd.EigenVectors;
            minCurvature = maxCurvature = null;
            if (eigenValues.Count > 0)
            {
                if (eigenValues[0].Imaginary == 0.0)
                {
                    GeoVector dir = eigenVectors[0, 0] * du + eigenVectors[1, 0] * dv;
                    if (eigenValues[0].Real != 0.0)
                    {
                        double rad = 1.0 / eigenValues[0].Real;
                        GeoPoint cnt = location + rad * n;
                        Ellipse elli = Ellipse.Construct();
                        elli.SetCirclePlaneCenterRadius(new Plane(cnt, n, dir), cnt, Math.Abs(rad));
                        minCurvature = elli;
                    }
                    else
                    {
                        Line line = Line.Construct();
                        line.SetTwoPoints(location, location + dir);
                        minCurvature = line;
                    }
                }
            }
            if (eigenValues.Count > 1) // what about a double eigenvalue?
            {
                if (eigenValues[1].Imaginary == 0.0)
                {
                    GeoVector dir = eigenVectors[0, 1] * du + eigenVectors[1, 1] * dv;
                    if (eigenValues[1].Real != 0.0)
                    {
                        double rad = 1.0 / eigenValues[1].Real;
                        GeoPoint cnt = location + rad * n;
                        Ellipse elli = Ellipse.Construct();
                        elli.SetCirclePlaneCenterRadius(new Plane(cnt, n, dir), cnt, Math.Abs(rad));
                        minCurvature = elli;
                    }
                    else
                    {
                        Line line = Line.Construct();
                        line.SetTwoPoints(location, location + dir);
                        minCurvature = line;
                    }
                }
            }
        }

        public static GeoPoint2D[] GetExtrema(ISurface surface, BoundingRect domain, GeoVector dir)
        {
            ISurface srf = surface.Clone();
            if (!Precision.SameDirection(dir, GeoVector.ZAxis, false))
            {   // modify the surface, so that dir is the z-axis
                srf.Modify(ModOp.Rotate(GeoPoint.Origin, dir, GeoVector.ZAxis));
            }
            GeoPoint2D[] uv = srf.GetExtrema();
            List<GeoPoint2D> res = new List<GeoPoint2D>();
            for (int i = 0; i < uv.Length; i++)
            {
                if (Precision.SameDirection(srf.GetNormal(uv[i]), GeoVector.ZAxis, false)) res.Add(uv[i]);
            }
            return res.ToArray();
        }
    }
}
