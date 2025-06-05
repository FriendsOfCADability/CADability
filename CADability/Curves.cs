using CADability.Curve2D;
using System;
using System.Collections;
using System.Collections.Generic;

namespace CADability.GeoObject
{
    /// <summary>
    /// Exception thrown by object implementing <see cref="ICurve"/>.
    /// </summary>

    public class CurveException : System.ApplicationException
    {
        /// <summary>
        /// Enumeration of different causes for this exception
        /// </summary>
		public enum Mode
        {
            /// <summary>
            /// Internal error
            /// </summary>
            Internal,
            /// <summary>
            /// General error
            /// </summary>
            General,
            /// <summary>
            /// No plane defined
            /// </summary>
            NoPlane
        }
        /// <summary>
        /// Cause of this exception
        /// </summary>
		public Mode ExeptionMode;
        internal CurveException(string msg, Mode m) : base(msg)
        {
            ExeptionMode = m;
        }
        internal CurveException(Mode m)
        {
            ExeptionMode = m;
        }
    }

    /// <summary>
    /// Enumeration for the classification of the planar state of a curve
    /// </summary>
	public enum PlanarState
    {
        /// <summary>
        /// The curve resides in a certain plane
        /// </summary>
        Planar,
        /// <summary>
        /// There are several planes for this curve, i.e. the curve is a line
        /// </summary>
        UnderDetermined,
        /// <summary>
        /// The curve is not planar
        /// </summary>
        NonPlanar,
        /// <summary>
        /// The state is unknown or not yet calculated
        /// </summary>
        Unknown
    }
    /// <summary>
    /// ---Yet to be used---
    /// Imagine a coordinate system moving along a 3d curve. Th z-axis always showing in direction of the curve. The x- and y-axis are still arbitrary. This orientation interface
    /// defines the direction of the x-axis at some point
    /// </summary>
    public interface IOrientation
    {
        GeoVector OrientationAt(double u);
    }
    /// <summary>
    /// Interface implemented by one-dimensional geometric objects (eg. line, circle, bspline etc.).
    /// The curve may be open or closed.
    /// </summary>
    public interface ICurve
    {
        /// <summary>
        /// The start-point of the curve. If the curve is closed, this is the point where 
        /// the parameter is 0.0. 
        /// </summary>
        GeoPoint StartPoint { get; set; }
        /// <summary>
        /// The endpoint of the curve. If the curve is closed, this is the point where 
        /// the parameter is 1.0. 
        /// </summary>
        GeoPoint EndPoint { get; set; }
        /// <summary>
        /// The direction of the curve at the start-point. If the curve is closed, this is the direction where 
        /// the parameter is 0.0. 
        /// </summary>
        GeoVector StartDirection { get; }
        /// <summary>
        /// The direction of the curve at the endpoint. If the curve is closed, this is the direction where 
        /// the parameter is 1.0. 
        /// </summary>
        GeoVector EndDirection { get; }
        /// <summary>
        /// Returns the direction of the curve at a specified point. The parameter position of the start-point
        /// is 0.0, the parameter position of the endpoint is 1.0. Directions of a position outside
        /// this interval may be undefined.
        /// </summary>
        /// <param name="Position">Position on the curve</param>
        /// <returns>The requested direction</returns>
        GeoVector DirectionAt(double Position);
        /// <summary>
        /// Returns the point of the given position. The parameter position of the start-point
        /// is 0.0, the parameter position of the endpoint is 1.0. Points of a position outside
        /// this interval may be undefined. The correlation between the parameter Position and the
        /// resulting point may be not linear.
        /// </summary>
        /// <param name="Position">Position on the curve</param>
        /// <returns>The requested point</returns>
        GeoPoint PointAt(double Position);
        /// <summary>
        /// Returns the parameter of the point at the given position. The position must be in the interval [0, Length].
        /// The correlation between the parameter Position and the
        /// resulting position is linear. The result is usually used for <see cref="PointAt"/> or <see cref="DirectionAt"/>.
        /// </summary>
        /// <param name="position">Position on the curve</param>
        /// <returns>The requested position</returns>
        double PositionAtLength(double position);
        /// <summary>
        /// Returns the parametric position of the point on the curve. This function is inverse to
        /// <seealso cref="DirectionAt"/>. If the given point is not on the curve, the result is the
        /// position of a point on the curve, that is close to the given point, but not necessary
        /// of the closest point.
        /// </summary>
        /// <param name="p">Point, for which the position is requested</param>
        /// <returns>the requested position</returns>
        double PositionOf(GeoPoint p);
        /// <summary>
        /// Similar to <see cref="PositionOf(GeoPoint)"/>. If the point is not on the curve and there are several
        /// solutions then the solution closest to the parameter prefer will be returned.
        /// </summary>
        /// <param name="p">Point, for which the position is requested</param>
        /// <param name="prefer">preferable solution close to this value</param>
        /// <returns>the requested position</returns>
        double PositionOf(GeoPoint p, double prefer);
        /// <summary>
        /// Similar to the <seealso cref="PointAt"/> method. Returns the same result, if the point
        /// is on the curve. If the point is not on the curve the problem is looked at in the given plane.
        /// i.e. the closest point on the projected 2d curve from the projected 2d point is used.
        /// </summary>
        /// <param name="p">Point, whos position is requested</param>
        /// <param name="pl">Plane for the computation of the closest point</param>
        /// <returns>the requested position</returns>
        double PositionOf(GeoPoint p, Plane pl);
        /// <summary>
        /// Returns the length of the curve.
        /// </summary>
        double Length { get; }
        /// <summary>
        /// Splits the curve at the given position. The position must be in the interval 0..1
        /// and the curve must not be closed. For closed curves <see cref="Split(double, double)"/>.
        /// </summary>
        /// <param name="Position">Where to split</param>
        /// <returns>the splitted curve(s)</returns>
        ICurve[] Split(double Position);
        /// <summary>
        /// Splits the closed curve into two open curves at the given positions.
        /// </summary>
        /// <param name="Position1">first Position</param>
        /// <param name="Position2">second Position</param>
        /// <returns>the splitted curve(s)</returns>
        ICurve[] Split(double Position1, double Position2);
        /// <summary>
        /// Determines, whether the curve is closed or open.
        /// </summary>
        bool IsClosed { get; }
        /// <summary>
        /// Determines, whether this curve is singular, i.e. it is only a point, returns the same value for each parameter
        /// </summary>
        bool IsSingular { get; }
        /// <summary>
        /// Reverses the direction of the curve.
        /// </summary>
        void Reverse();
        /// <summary>
        /// Modifies start and endpoint of this curve. StartPos must be less than EndPos.
        /// if StartPos is less than 0.0 or EndPos greater than 1.0 this only works for lines and
        /// (circular or elliptical) arcs.
        /// </summary>
        /// <param name="StartPos">New start position</param>
        /// <param name="EndPos">New end position</param>
        void Trim(double StartPos, double EndPos);
        /// <summary>
        /// Returns an identical copy of this curve
        /// </summary>
        /// <returns></returns>
        ICurve Clone();
        /// <summary>
        /// Returns a modified copy of this curve
        /// </summary>
        /// <param name="m">modification</param>
        /// <returns>modified copy</returns>
        ICurve CloneModified(ModOp m);
        /// <summary>
        /// Determins the state of the curve in space. A curve may be either
        /// Planar (e.g. a cicle), NonPlanar (e.g. a polyline with vertices that
        /// dont shear a common plane) or UnderDetermined (e.g. a line defines a 
        /// sheaf of planes (Ebenenbüschel))
        /// </summary>
        /// <returns></returns>
        PlanarState GetPlanarState();
        /// <summary>
        /// Determins the plane in which the curve resides. Throws CurveException if
        /// the curve's PlanarState is NonPlanar or UnderDetermined.
        /// </summary>
        /// <returns>The plane</returns>
        Plane GetPlane();
        /// <summary>
        /// Determines whether the curve resides in the given plane.
        /// </summary>
        /// <param name="p">The plane for the test</param>
        /// <returns>true, when the curve resides in the plane</returns>
        bool IsInPlane(Plane p);
        /// <summary>
        /// Orthogonally projects this curve into the given plane. Returns
        /// the 2D curve in the coordinate system of the given plane.
        /// </summary>
        /// <param name="p">The plane this curve will be projected on</param>
        /// <returns>The 2D curve in the given plane</returns>
        ICurve2D GetProjectedCurve(Plane p);
        /// <summary>
        /// Returns a description of the curve, used in labels of the controlcenter.
        /// </summary>
        string Description { get; }
        /// <summary>
        /// Returns true, if the curve is composed of other curves. <see cref="Path"/>s and <see cref="Polyline"/>s are composed curves.
        /// </summary>
		bool IsComposed { get; }
        /// <summary>
        /// Returns subcurves of composed curves. <see cref="Path"/>s and <see cref="Polyline"/>s are composed curves.
        /// </summary>
		ICurve[] SubCurves { get; }
        /// <summary>
        /// Returns a <see cref="Path"/> or a <see cref="Polyline"/> that approximates the curve with lines
        /// or lines and arcs.
        /// </summary>
        /// <param name="linesOnly">true: no arcs, only lines</param>
        /// <param name="maxError">Maximum derivation from the exact curve</param>
        /// <returns>The Approximation</returns>
		ICurve Approximate(bool linesOnly, double maxError);
        /// <summary>
        /// Returns a list of positions where the curve has the same or opposite direction as the given direction.
        /// Mainly used for visualisation purposes. If there are no such points (which is true in most cases) 
        /// An empty array should be returned.
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        double[] TangentPosition(GeoVector direction);
        /// <summary>
        /// Returns the self intersection position of this curve. The returned array contains pairs of parameter values.
        /// Each intersectionpoint corresponds to two parameters.
        /// </summary>
        /// <returns>Parameters of the intersection points</returns>
        double[] GetSelfIntersections();
        /// <summary>
        /// Returns true, if this curve and the provided curve describe the same curve in space (maybe opposite direction)
        /// </summary>
        /// <param name="other">The curve to be compared with</param>
        /// <param name="precision">The required precision</param>
        /// <returns>true, if geometrically equal</returns>
        bool SameGeometry(ICurve other, double precision);
        /// <summary>
        /// Returns the bounging cube for this curve
        /// </summary>
        /// <returns>The cube bounding this curve</returns>
        BoundingCube GetExtent();
        /// <summary>
        /// Determins whether this curve interferes with the provided cube.
        /// </summary>
        /// <param name="cube">Cube to check interference with</param>
        /// <returns>True, if curve and cube interfere</returns>
        bool HitTest(BoundingCube cube);
        /// <summary>
        /// Returns some positions (parameter values between 0.0 and 1.0) that can savely be used for Approximation purposes
        /// Usually not used by external applications
        /// </summary>
        /// <returns></returns>
        double[] GetSavePositions();
        /// <summary>
        /// Returns points of the curve (parameter values between 0.0 and 1.0) where the curve is tangential to
        /// a plane defined by the normal vector <paramref name="direction"/>. These points are minima or maxima in that
        /// direction.
        /// </summary>
        /// <param name="direction">Direction for the extrema</param>
        /// <returns>Positions of the extrema, if any</returns>
        double[] GetExtrema(GeoVector direction);
        /// <summary>
        /// Returns the parameters of the intersection points with the provided plane
        /// </summary>
        /// <param name="plane">To intersect with</param>
        /// <returns>Intersection parameters, may be empty</returns>
        double[] GetPlaneIntersection(Plane plane);
        /// <summary>
        /// Returns the minimal distance of point p to the curve. 
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        double DistanceTo(GeoPoint p);
        /// <summary>
        /// Tries to get the point and the first and second derivative of the curve at the specified position. (0..1)
        /// Some curves do not implement the second derivative and hence will return false.
        /// </summary>
        /// <param name="position">Position where to calculate point and derivatives</param>
        /// <param name="point">The point at the required position</param>
        /// <param name="deriv1">The first derivative at the provided parameter</param>
        /// <param name="deriv2">The second derivative at the provided parameter</param>
        /// <returns></returns>
        bool TryPointDeriv2At(double position, out GeoPoint point, out GeoVector deriv1, out GeoVector deriv2);
        /// <summary>
        /// convert the parameter, which is in a natural system (0..2*pi for circle, first knot to last knot for bspline) to the 0..1 normalized position
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns>the position, 0..1 when inside the bounds</returns>
        double ParameterToPosition(double parameter);
        /// <summary>
        /// convert the position (which is in the 0..1 interval) to the natural parameter value
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        double PositionToParameter(double position);
        /// <summary>
        /// Extend the curve at the beginning or/and end. Not all curves are extendable, if they are not extendable they should return false.
        /// The units is the actual length of the curve, not the parameter space.
        /// </summary>
        /// <param name="atStart"></param>
        /// <param name="atEnd"></param>
        /// <returns></returns>
        bool Extend(double atStart, double atEnd);
    }
    /// <summary>
    /// Enumeration for direction of curve extension
    /// </summary>
    public enum ExtentedableCurveDirection
    {
        /// <summary>
        /// Extend only forward
        /// </summary>
        forward,
        /// <summary>
        /// Extend only backward
        /// </summary>
        backward,
        /// <summary>
        /// Extend in both directions
        /// </summary>
        both
    }
    /// <summary>
    /// Interface for ICurve implementing objects that can be extended in one or two directions
    /// </summary>

    public interface IExtentedableCurve
    {
        /// <summary>
        /// Returns the IOctTreeInsertable object categorizing this curve. Usually used to search
        /// in an <see cref="OctTree{T}"/>.
        /// </summary>
        /// <param name="direction">Extend direction</param>
        /// <returns>Interface for OctTree access</returns>
        IOctTreeInsertable GetExtendedCurve(ExtentedableCurveDirection direction);

    }

    /// <summary>
    /// Wird von einfachen Kurven implementiert um schnellere Schnitte berechnen zu können.
    /// Das kann auch noch aufgebohrt werden
    /// </summary>
    internal interface ISimpleCurve
    {
        double[] GetPlaneIntersection(Plane pln);
    }

    /// <summary>
    /// This class provides static methods concerning the interaction
    /// of two or more ICurve objects.
    /// </summary>

    public class Curves
    {
        /// <summary>
        /// Determines the common plane of the two curves. If there is a common plane,
        /// the Parameter CommonPlane gets the result ant the function returns true. Otherwise
        /// the function returns false.
        /// </summary>
        /// <param name="c1">first curve</param>
        /// <param name="c2">second curve</param>
        /// <param name="CommonPlane">the resulting common plane</param>
        /// <returns></returns>
        public static bool GetCommonPlane(ICurve c1, ICurve c2, out Plane CommonPlane)
        {   // kommt erstaunlicherweise ohne Zugriff auf die konkreten Kurven aus
            PlanarState ps1 = c1.GetPlanarState();
            PlanarState ps2 = c2.GetPlanarState();
            if (ps1 == PlanarState.NonPlanar || ps2 == PlanarState.NonPlanar)
            {
                CommonPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0); // braucht leider ein Ergebnis
                return false;
            }
            if (ps1 == PlanarState.Planar)
            {
                if (ps2 == PlanarState.Planar)
                {
                    Plane p1 = c1.GetPlane();
                    Plane p2 = c2.GetPlane();
                    if (Precision.IsEqual(p1, p2))
                    {
                        CommonPlane = p1;
                        return true;
                    }
                }
                else
                {   // ps2 ist UnderDetermined, also einen Punkt mit p1 testen
                    Plane p1 = c1.GetPlane();
                    if (c2.IsInPlane(p1))
                    {
                        CommonPlane = p1;
                        return true;
                    }
                }
            }
            else
            {   // ps1 ist UnderDetermined
                if (ps2 == PlanarState.Planar)
                {
                    Plane p2 = c2.GetPlane();
                    if (c1.IsInPlane(p2))
                    {
                        CommonPlane = p2;
                        return true;
                    }
                }
                else
                {
                    // ps1 und ps2 UnderDetermined. Das können nur zwei Linien sein (oder
                    // polylines mit einem Segment oder ähnliches)
                    GeoVector v1 = c1.StartDirection;
                    GeoVector v2 = c2.StartDirection;
                    if (Precision.SameDirection(v1, v2, false))
                    {   // d.h. parallel
                        GeoVector v3 = c2.StartPoint - c1.StartPoint;
                        if (!Precision.SameDirection(v1, v3, false))
                        {
                            try
                            {
                                CommonPlane = new Plane(c1.StartPoint, v1, v3);
                                return true;
                            }
                            catch (PlaneException)
                            {
                                CommonPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0); // braucht leider ein Ergebnis
                                return false;
                            }
                        } // else: colinear, geht also nicht
                    }
                    else
                    {
                        try
                        {
                            Plane p = new Plane(c1.StartPoint, v1, v2);
                            if (c2.IsInPlane(p))
                            {
                                CommonPlane = p;
                                return true;
                            }
                            GeoPoint[] pnts = new GeoPoint[4];
                            pnts[0] = c1.StartPoint;
                            pnts[1] = c1.EndPoint;
                            pnts[2] = c2.StartPoint;
                            pnts[3] = c2.EndPoint;
                            p = Plane.FromPoints(pnts, out double maxDist, out bool isLinear);
                            if (maxDist < Precision.eps)
                            {
                                CommonPlane = p;
                                return true;
                            }
                        }
                        catch (PlaneException)
                        {
                            CommonPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0); // braucht leider ein Ergebnis
                            return false;
                        }
                    }
                }
            }
            CommonPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0); // braucht leider ein Ergebnis
            return false;
        }
        /// <summary>
        /// Determines the commomn plane of a point and a curve. Returns the common plane in the 
        /// parameter and true if there is such a plane. Returns false, if the point lies not 
        /// in the plane. <seealso cref="Precision"/>
        /// </summary>
        /// <param name="p">The point</param>
        /// <param name="c2">The curve</param>
        /// <param name="CommonPlane">The common plane</param>
        /// <returns>true, if there is a common plane, else false</returns>
        public static bool GetCommonPlane(GeoPoint p, ICurve c2, out Plane CommonPlane)
        {   // kommt erstaunlicherweise ohne Zugriff auf die konkreten Kurven aus
            PlanarState ps2 = c2.GetPlanarState();
            if (ps2 == PlanarState.NonPlanar)
            {
                CommonPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0); // braucht leider ein Ergebnis
                return false;
            }

            if (ps2 == PlanarState.Planar)
            {
                Plane p2 = c2.GetPlane();
                if (Precision.IsPointOnPlane(p, p2))
                {
                    CommonPlane = p2;
                    return true;
                }
            }
            else
            {   // UnderDetermined, also Linie oder so
                GeoVector v1 = p - c2.StartPoint;
                GeoVector v2 = c2.StartDirection;
                if (!Precision.SameDirection(v1, v2, false) && !Precision.IsNullVector(v1))
                {
                    Plane pl = new Plane(p, v1, v2);
                    if (c2.IsInPlane(pl))
                    {
                        CommonPlane = pl;
                        return true;
                    }
                }
            }
            CommonPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0); // braucht leider ein Ergebnis
            return false;
        }
        /// <summary>
        /// Tries to find a plane that contains most of the given curves.
        /// </summary>
        /// <param name="curves">List of ICurves</param>
        /// <param name="CommonPlane">Returns common plane</param>
        /// <returns>Returns wether a common plan was found or not</returns>
        public static bool GetCommonPlane(IList<ICurve> curves, out Plane CommonPlane)
        {
            CommonPlane = new Plane(Plane.StandardPlane.XYPlane, 0.0);
            if (curves.Count == 0) return false;
            if (curves.Count == 1)
            {
                if (curves[0].GetPlanarState() == PlanarState.Planar)
                {
                    CommonPlane = curves[0].GetPlane();
                    return true;
                }
                return false;
            }
            for (int i = 0; i < curves.Count; ++i)
            {   // schneller Vorstest, geht halt nicht bei nur Linien
                if (curves[i].GetPlanarState() == PlanarState.Planar)
                {   // nimm die erste ebene Kurve
                    // und teste ob die anderen alle drin sind
                    Plane pln = curves[i].GetPlane();
                    bool ok = true;
                    for (int j = 0; j < curves.Count; ++j)
                    {
                        if (i != j)
                        {
                            if (curves[j].GetPlanarState() == PlanarState.NonPlanar) return false; // das wars schon
                            if (!curves[j].IsInPlane(pln))
                            {
                                ok = false;
                                break;
                            }
                        }
                    }
                    if (!ok) break; // ausführlicher Test mit der Suche nach der besten
                    CommonPlane = pln;
                    return true; // fertig, alle sind drin.
                }
            }
            GeoPoint[] pnts = new GeoPoint[curves.Count * 2];
            for (int i = 0; i < curves.Count; ++i)
            {
                pnts[2 * i] = curves[i].StartPoint;
                pnts[2 * i + 1] = curves[i].EndPoint;
            }
            CommonPlane = Plane.FromPoints(pnts, out double maxdist, out bool isLinear);
            if (isLinear)
            {
                Plane pln = new Plane(pnts[0], curves[0].StartDirection);
                CommonPlane = new Plane(pnts[0], curves[0].StartDirection, pln.DirectionX);
                return true;
            }
            // if (isLinear) return true; // liefert halt eine passende Ebene (von vielen möglichen)
            if (maxdist > 10 * Precision.eps) return false;
            for (int i = 0; i < curves.Count; ++i)
            {
                if (!curves[i].IsInPlane(CommonPlane)) return false;
            }
            return true;            
        }
        /// <summary>
        /// Returns the parameters of the intersection points of curve1 with curve2.
        /// Parameters are with respect to curve1. The two curves must reside in a common plane.
        /// </summary>
        /// <param name="curve1">First curve for intersection</param>
        /// <param name="curve2">Second curve for intersection</param>
        /// <param name="onlyInside">if true, only intersction point that are actually on the curves are returned
        /// otherwise also intersection points on the extension of ther curves are returned</param>
        /// <returns>Parameters for the intersection points</returns>
        public static double[] Intersect(ICurve curve1, ICurve curve2, bool onlyInside)
        {
            ArrayList res = new ArrayList();
            if (GetCommonPlane(curve1, curve2, out Plane pl))
            {
                ICurve2D c12d = curve1.GetProjectedCurve(pl);
                ICurve2D c22d = curve2.GetProjectedCurve(pl);
                GeoPoint2DWithParameter[] pp = c12d.Intersect(c22d);
                for (int i = 0; i < pp.Length; ++i)
                {
                    if (!onlyInside || (c12d.IsParameterOnCurve(pp[i].par1) && c22d.IsParameterOnCurve(pp[i].par2)))
                    {
                        // double pos = curve1.PositionOf(pl.ToGlobal(c12d.PointAt(pp[i].par1)));
                        // geht z.Z. davon aus, dass die Parametrierung im 2d und 3d gleich läuft, ansonsten mit obiger Zeile
                        res.Add(pp[i].par1);
                    }
                }
            }
            return (double[])(res.ToArray(typeof(double)));
        }
        /// <summary>
        /// Calculates the intersection points of the two curves.
        /// </summary>
        /// <param name="curve1">First curve</param>
        /// <param name="curve2">Second curve</param>
        /// <param name="par1">Resulting parameters for the first curve</param>
        /// <param name="par2">Resulting parameters for the second curve</param>
        /// <param name="intersection">Three dimensional intersection points</param>
        /// <returns>Number of intersection points</returns>
        public static int Intersect(ICurve curve1, ICurve curve2, out double[] par1, out double[] par2, out GeoPoint[] intersection)
        {
            // wenn es eine gemeinsame Ebene gibt, dann in dieser Ebene schneiden
            if (GetCommonPlane(curve1, curve2, out Plane pln))
            {
                ICurve2D c2d1 = curve1.GetProjectedCurve(pln);
                ICurve2D c2d2 = curve2.GetProjectedCurve(pln);
                GeoPoint2DWithParameter[] ips = c2d1.Intersect(c2d2);
                // geht die Parametrierung der projizierten Kurven analog zu den Originalen?
                // da wir ja nur in die Ebene Projizieren, in der sich duie Kurven ohnehin befinden, müsste das stimmen
                // das muss aber noch getestet werden
                par1 = new double[ips.Length];
                par2 = new double[ips.Length];
                intersection = new GeoPoint[ips.Length];
                for (int i = 0; i < ips.Length; ++i)
                {
                    par1[i] = ips[i].par1;
                    par2[i] = ips[i].par2;
                    intersection[i] = pln.ToGlobal(ips[i].p);
                }
                return ips.Length;
            }
            // eine Linie und eine nichtebene Kurve noch gesondert prüfen
            if (curve1.GetPlanarState() == PlanarState.Planar)
            {
                double[] ips = curve2.GetPlaneIntersection(curve1.GetPlane());
                List<double> lpar1 = new List<double>();
                List<double> lpar2 = new List<double>();
                List<GeoPoint> lip = new List<GeoPoint>();
                for (int i = 0; i < ips.Length; ++i)
                {
                    GeoPoint p = curve2.PointAt(ips[i]);
                    double ppar1 = curve1.PositionOf(p);
                    GeoPoint p1 = curve1.PointAt(ppar1);
                    if (Precision.IsEqual(p, p1))
                    {
                        lpar1.Add(ppar1);
                        lpar2.Add(ips[i]);
                        lip.Add(new GeoPoint(p, p1));
                    }
                }
                par1 = lpar1.ToArray();
                par2 = lpar2.ToArray();
                intersection = lip.ToArray();
                return par1.Length;
            }
            if (curve2.GetPlanarState() == PlanarState.Planar)
            {
                return Intersect(curve2, curve1, out par2, out par1, out intersection);
            }
            // Fehlt noch die Abfrage nach einer Linie, denn die ist ja nicht planar

            // allgemeiner Fall: zwei nicht ebene Kurven
            TetraederHull th1 = new TetraederHull(curve1);
            TetraederHull th2 = new TetraederHull(curve2);
            return th1.Intersect(th2, out par1, out par2, out intersection);
        }
        private static void ApproxPoints(ArrayList al, ICurve curve, double par1, double par2, double maxError)
        {
            GeoPoint p1 = curve.PointAt(par1);
            GeoPoint p2 = curve.PointAt(par2);
            GeoPoint p = new GeoPoint(p1, p2); // Zwischenpunkt
            double par = (par1 + par2) / 2;
            if (Geometry.Dist(curve.PointAt(par), p) < maxError || (par2 - par1) < 1e-6)
            {
                al.Add(p2);
            }
            else
            {
                ApproxPoints(al, curve, par1, par, maxError);
                ApproxPoints(al, curve, par, par2, maxError);
            }
        }
        internal static ICurve ApproximateLinear(ICurve curve, double maxError)
        {
            if (maxError == 0.0) maxError = Precision.eps;
            ArrayList al = new ArrayList();
            double d = 0.25;
            al.Add(curve.StartPoint);
            for (int i = 0; i < 4; ++i)
            {
                ApproxPoints(al, curve, i * d, (i + 1) * d, maxError);
            }
            Polyline pl = Polyline.Construct();
            pl.SetPoints((GeoPoint[])al.ToArray(typeof(GeoPoint)), false);
            return pl;
        }
        internal static ICurve ApproximateLinear(ICurve curve, double[] positions, double maxError)
        {
            if (maxError == 0.0) maxError = Precision.eps;
            ArrayList al = new ArrayList();
            al.Add(curve.StartPoint);
            for (int i = 0; i < positions.Length - 1; ++i)
            {
                ApproxPoints(al, curve, positions[i], positions[i + 1], maxError);
            }
            try
            {
                Polyline pl = Polyline.Construct();
                pl.SetPoints((GeoPoint[])al.ToArray(typeof(GeoPoint)), false);
                return pl;
            }
            catch (PolylineException)
            {
                Line l = Line.Construct();
                l.SetTwoPoints(curve.StartPoint, curve.EndPoint);
                return l;
            }
        }
        internal static void Approximate(IFrame frame, IGeoObject toApproximate)
        {   // die Approximation gemäß globaler Einstellung aus einem ShowProperty heraus,
            // also mit SelectObjectsAction als aktiver Aktion
            ICurve app = (toApproximate as ICurve).Approximate(frame.GetIntSetting("Approximate.Mode", 0) == 0, frame.GetDoubleSetting("Approximate.Precision", 0.01));
            Actions.SelectObjectsAction soa = frame.ActiveAction as Actions.SelectObjectsAction;
            IGeoObjectOwner addTo = toApproximate.Owner;
            if (addTo == null) addTo = frame.ActiveView.Model;
            using (frame.Project.Undo.UndoFrame)
            {
                addTo.Remove(toApproximate);
                IGeoObject go = app as IGeoObject;
                go.CopyAttributes(toApproximate);
                addTo.Add(go);
                soa.SetSelectedObjects(new GeoObjectList(go));
            }
        }

        internal static bool SameGeometry(ICurve curve1, ICurve curve2, double precision, out bool reverse)
        {   // erstmal für nicht geschlossene Kurven
            double d1 = (curve1.StartPoint | curve2.StartPoint) + (curve1.EndPoint | curve2.EndPoint);
            double d2 = (curve1.EndPoint | curve2.StartPoint) + (curve1.StartPoint | curve2.EndPoint);
            if (curve1.IsClosed && curve2.IsClosed || (d1 < 2 * precision && d2 < 2 * precision))
            {
                reverse = curve1.StartDirection * curve2.StartDirection < 0;
            }
            else
            {
                reverse = d1 > d2;
                if (d1 > 2 * precision && d2 > 2 * precision) return false;
            }
            if (reverse)
            {
                curve2 = curve2.Clone();
                curve2.Reverse();
                return curve1.SameGeometry(curve2, precision);
            }
            else
            {
                return curve1.SameGeometry(curve2, precision);
            }
        }
        /// <summary>
        /// Tries to combine two curves. crv1 and crv2 must be correct oriented and the endpoint of crv1 mut be the start-point of crv2
        /// </summary>
        /// <param name="crv1"></param>
        /// <param name="crv2"></param>
        /// <returns>the combined curve or null, if not possible</returns>
        public static ICurve Combine(ICurve crv1, ICurve crv2, double precision)
        {
            if (crv1 == null || crv2 == null) return null;
            if ((crv1.EndPoint | crv2.StartPoint) > precision) return null;
            if (crv1 is InterpolatedDualSurfaceCurve ip1 && crv2 is InterpolatedDualSurfaceCurve ip2)
            {
                if ((ip1.Surface1.SameGeometry(ip1.Domain1, ip2.Surface1, ip2.Domain1, precision, out _) && ip1.Surface2.SameGeometry(ip1.Domain2, ip2.Surface2, ip2.Domain2, precision, out _)) ||
                    (ip1.Surface1.SameGeometry(ip1.Domain1, ip2.Surface2, ip2.Domain2, precision, out _) && ip1.Surface2.SameGeometry(ip1.Domain2, ip2.Surface1, ip2.Domain1, precision, out _)))
                {
                    List<GeoPoint> basepoints = new List<GeoPoint>();
                    basepoints.AddRange(ip1.BasePoints);
                    basepoints.RemoveAt(basepoints.Count - 1);
                    basepoints.AddRange(ip2.BasePoints);
                    return new InterpolatedDualSurfaceCurve(ip1.Surface1, ip1.Domain1, ip1.Surface2, ip1.Domain2, basepoints); // the domains are too small, but this is not a problem
                }
            }
            if (GetCommonPlane(crv1, crv2, out Plane pln))
            {
                ICurve2D e2d1 = crv1.GetProjectedCurve(pln);
                ICurve2D e2d2 = crv2.GetProjectedCurve(pln);
                ICurve2D fused = e2d1.GetFused(e2d2, precision);
                if (fused != null) return fused.MakeGeoObject(pln) as ICurve;
            }
            else if (crv1 is Line && crv2 is Line)
            {
                Line res = Line.Construct();
                res.SetTwoPoints(crv1.StartPoint, crv2.EndPoint);
                return res;
            }
            // non planar BSplines should be implemented
            return null;
        }
        /// <summary>
        /// Returns true if curve1 and curve2 are overlapping curves. The overlapping intervalls for both curves are
        /// returned in <paramref name="from1"/>, <paramref name="to1"/>, <paramref name="from2"/> and <paramref name="to2"/>.
        /// </summary>
        /// <param name="curve1">First curve</param>
        /// <param name="curve2">Second curve</param>
        /// <param name="precision">Required precision</param>
        /// <param name="from1">Starting parameter for first curve</param>
        /// <param name="to1">Ending parameter for first curve</param>
        /// <param name="from2">Starting parameter for second curve</param>
        /// <param name="to2">Ending parameter for second curve</param>
        /// <returns></returns>
        public static bool Overlapping(ICurve curve1, ICurve curve2, double precision, out double from1, out double to1, out double from2, out double to2)
        {
            from1 = to1 = from2 = to2 = 0.0; // für den fals Fall
            List<double> c1 = new List<double>();
            List<double> c2 = new List<double>();
            double u = curve1.PositionOf(curve2.StartPoint);
            GeoPoint p = curve1.PointAt(u);
            if (u >= 0.0 && u <= 1.0 && (p | curve2.StartPoint) < precision)
            {
                c1.Add(u);
                c2.Add(0.0);
            }
            u = curve1.PositionOf(curve2.EndPoint);
            p = curve1.PointAt(u);
            if (u >= 0.0 && u <= 1.0 && (p | curve2.EndPoint) < precision)
            {
                c1.Add(u);
                c2.Add(1.0);
            }
            u = curve2.PositionOf(curve1.StartPoint);
            p = curve2.PointAt(u);
            if (u >= 0.0 && u <= 1.0 && (p | curve1.StartPoint) < precision)
            {
                c2.Add(u);
                c1.Add(0.0);
            }
            u = curve2.PositionOf(curve1.EndPoint);
            p = curve2.PointAt(u);
            if (u >= 0.0 && u <= 1.0 && (p | curve1.EndPoint) < precision)
            {
                c2.Add(u);
                c1.Add(1.0);
            }
            if (c1.Count < 2) return false;
            if (c1.Count > 2)
            {   // eine Kurve liegt ganz in der anderen und Start oder Endpunkt sind gleich (oder identische Kurven)
                if (c1.Count == 4)
                {   // nichts tun, die ersten beiden sind schon OK
                }
                else
                {   // also 3 Punkte, einer davon ist doppelt
                    if (c1[1] == 0.0 && c1[2] == 1.0)
                    {   // die beiden letzten Abfragen haben gegriffen, den 1. Punkt wegwerfen
                        c1.RemoveAt(0);
                        c2.RemoveAt(0);
                    }
                    // ansonsten haben die beiden ersten Abfragen geegriffen, der 3. Punkt wird nicht verwednet
                }
            }
            // Dir Richtungen müssen gleich sein, jedoch ist das schwer mit precision in Einklang zu bringen
            // Hier jetzt die notwendigen Zwischenpunkte betrachten
            // bool intermediatePointChecked = false;
            double[] sp = curve1.GetSavePositions();
            double umin = Math.Min(c1[0], c1[1]);
            double umax = Math.Max(c1[0], c1[1]);
            for (int i = 0; i < sp.Length; i++)
            {
                if (sp[i] > umin && sp[i] < umax)
                {
                    GeoPoint p1 = curve1.PointAt(sp[i]);
                    GeoPoint p2 = curve2.PointAt(curve2.PositionOf(p1));
                    if ((p1 | p2) > precision) return false;
                    //intermediatePointChecked = true;
                }
            }
            if (umax - umin < 1e-6)
            {
                // Sonderfall: nur Start/Endpunkte sind identisch. Es sollte nur dann true geliefert werden
                // wenn die Kurven dort tangential sind
                if (!Precision.SameDirection(curve1.DirectionAt(c1[0]), curve2.DirectionAt(c2[0]), false)) return false;
                if (!Precision.SameDirection(curve1.DirectionAt(c1[1]), curve2.DirectionAt(c2[1]), false)) return false;
            }
            sp = curve2.GetSavePositions();
            umin = Math.Min(c2[0], c2[1]);
            umax = Math.Max(c2[0], c2[1]);
            for (int i = 0; i < sp.Length; i++)
            {
                if (sp[i] > umin && sp[i] < umax)
                {
                    GeoPoint p1 = curve2.PointAt(sp[i]);
                    GeoPoint p2 = curve1.PointAt(curve1.PositionOf(p1));
                    if ((p1 | p2) > precision) return false;
                    //intermediatePointChecked = true;
                }
            }
            {   // die mittelpunkte im Parameterbereich müssen auch im anderen Parameterbereich liegen
                GeoPoint p1 = curve1.PointAt((c1[0] + c1[1]) / 2.0);
                double pos = curve2.PositionOf(p1);
                if (pos < 0.0 || pos > 1.0) return false; // eigentlich sollte die strengere Bedingung gelten: im Intervall c2[0]..c2[1].
                GeoPoint p2 = curve2.PointAt((c2[0] + c2[1]) / 2.0);
                pos = curve1.PositionOf(p2);
                if (pos < 0.0 || pos > 1.0) return false; // eigentlich sollte die strengere Bedingung gelten: im Intervall c2[0]..c2[1].
                // if ((p1 | p2) > precision) return false;
            }
            from1 = c1[0];
            to1 = c1[1];
            from2 = c2[0];
            to2 = c2[1];
            return true;
        }
        public static bool NewtonMinDist(ICurve curve1, ref double par1, ICurve curve2, ref double par2)
        {   // find the points on both curves where the connecting line of the two curves is perpendicular on both
            int numoutside = 0;
            while (numoutside < 3)
            {
                GeoPoint s1 = curve1.PointAt(par1);
                GeoVector d1 = curve1.DirectionAt(par1);
                GeoPoint s2 = curve2.PointAt(par2);
                GeoVector d2 = curve2.DirectionAt(par2);
                double d = Geometry.DistLL(s1, d1, s2, d2, out double pp1, out double pp2);
                par1 += pp1;
                par2 += pp2;
                if (curve1.IsClosed)
                {
                    if (par1 < 0) par1 += 1;
                    if (par1 > 1) par1 -= 1;
                }
                if (curve2.IsClosed)
                {
                    if (par2 < 0) par2 += 1;
                    if (par2 > 1) par2 -= 1;
                }
                if (par1 < -1e-6 || par1 > 1 + 1e-6 || par2 < -1e-6 || par2 > 1 + 1e-6) ++numoutside;
                if (Math.Abs(pp1) < 1e-6 && Math.Abs(pp2) < 1e-6)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
