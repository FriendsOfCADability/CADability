using System;

namespace CADability
{
    /// <summary>
    /// Precision specifies the order of size of a typical model. For example 
    /// two points are considered geometrically equal if their distance is less than
    /// Precision.eps. Default value for Precision.eps is 1e-6.
    /// Precision.epsa is the angular precision. Two directions are considered
    /// equal if their angular difference is less than Precision.epsa.
    /// </summary>

    public class Precision
    {
        private static double globalEps = 1e-6, globalEpsa = 1e-6;
        // The values of the innermost PrecisionOverride on this thread, 0.0 where there is none: a [ThreadStatic] field
        // cannot be initialized, it is 0.0 on every new thread.
        [ThreadStatic] private static double threadEps, threadEpsa;
        // The number of PrecisionOverrides alive on all threads. As long as there is none, eps and epsa are read
        // without accessing the thread static fields.
        private static int activeOverrides;
        /// <summary>
        /// The maximum distance for which two points are considered geometrically equal. Setting it changes the value
        /// for all threads, a <see cref="PrecisionOverride"/> changes it only for the thread that creates it.
        /// </summary>
        public static double eps
        {
            get { return activeOverrides == 0 || threadEps == 0.0 ? globalEps : threadEps; }
            set { globalEps = value; }
        }
        // public static double size = 1000.0;
        /// <summary>
        /// The maximum difference in radians for two angles to be considered geometrically equal. Setting it changes the
        /// value for all threads, a <see cref="PrecisionOverride"/> changes it only for the thread that creates it.
        /// </summary>
        public static double epsa
        {
            get { return activeOverrides == 0 || threadEpsa == 0.0 ? globalEpsa : threadEpsa; }
            set { globalEpsa = value; }
        }
        internal static (double eps, double epsa) BeginOverride(double eps, double epsa)
        {
            (double, double) previous = (threadEps, threadEpsa);
            System.Threading.Interlocked.Increment(ref activeOverrides);
            threadEps = eps;
            threadEpsa = epsa;
            return previous;
        }
        internal static void EndOverride((double eps, double epsa) previous)
        {
            threadEps = previous.eps;
            threadEpsa = previous.epsa;
            System.Threading.Interlocked.Decrement(ref activeOverrides);
        }
        /// <summary>
        /// Intended to derive <see cref="eps"/> from the size of a model. Not implemented: the call has no effect, the
        /// precision values stay as they are.
        /// </summary>
        /// <param name="m">the model, not used</param>
        public static void SetFromModel(Model m)
        {
            // TODO: size ist maximale Modellgröße, eps = size *1e-9
        }
        /// <summary>
        /// Determins, whether the given points are almost identical, i.e. the distance
        /// of the points is less than eps.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        /// <returns>true: almost identical, false: different</returns>
        public static bool IsEqual(GeoPoint p1, GeoPoint p2)
        {
            return Geometry.Dist(p1, p2) < eps;
        }

        /// <summary>
        /// Determines whether all points are almost identical: each point must be within <see cref="eps"/> of its successor
        /// in the array. Returns true for an empty array or a single point.
        /// </summary>
        /// <param name="pnts">the points to test</param>
        /// <returns>true: all points coincide, false: two consecutive points are further apart than eps</returns>
        public static bool IsEqual(GeoPoint[] pnts)
        {
            for (int i = 0; i < pnts.Length - 1; i++)
            {
                if (Geometry.Dist(pnts[i], pnts[i + 1]) > eps) return false;
            }
            return true;
        }

        /// <summary>
        /// Determines whether the given 2d points are almost identical, i.e. the distance of the points is less than
        /// <see cref="eps"/>.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        /// <returns>true: almost identical, false: different</returns>
        public static bool IsEqual(GeoPoint2D p1, GeoPoint2D p2)
        {
            return Geometry.Dist(p1, p2) < eps;
        }

        /// <summary>
        /// Determines whether the two vectors are almost identical, i.e. the sum of the absolute differences of their
        /// components is less than <see cref="eps"/>. Unlike <see cref="SameDirection(GeoVector, GeoVector, bool)"/> this
        /// compares the vectors themselves including their lengths, not only their directions.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>true: almost identical, false: different</returns>
        public static bool IsEqual(GeoVector v1, GeoVector v2)
        {
            return (Math.Abs(v1.x - v2.x) + Math.Abs(v1.y - v2.y) + Math.Abs(v1.z - v2.z)) < eps;
        }

        /// <summary>
        /// Determines whether the two 2d vectors are almost identical, i.e. the sum of the absolute differences of their
        /// components is less than <see cref="eps"/>. Unlike <see cref="SameDirection(GeoVector2D, GeoVector2D, bool)"/> this
        /// compares the vectors themselves including their lengths, not only their directions.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>true: almost identical, false: different</returns>
        public static bool IsEqual(GeoVector2D v1, GeoVector2D v2)
        {
            return (Math.Abs(v1.x - v2.x) + Math.Abs(v1.y - v2.y)) < eps;
        }

        /// <summary>
        /// Determines whether the two angles are almost identical, i.e. their difference is less than <see cref="epsa"/>.
        /// The wrap-around at 2*pi is taken into account, so an angle slightly below 2*pi equals an angle slightly above 0.
        /// </summary>
        /// <param name="a1">first angle</param>
        /// <param name="a2">second angle</param>
        /// <returns>true: almost identical, false: different</returns>
        public static bool IsEqual(Angle a1, Angle a2)
        {
            double a = Math.Abs(a1.Radian - a2.Radian);
            return a < epsa || Math.PI * 2.0 - a < epsa;
        }
        /// <summary>
        /// Determines whether the two values are almost identical, i.e. their difference is less than <see cref="eps"/>.
        /// Intended for lengths and coordinates; for angles use <see cref="IsEqual(Angle, Angle)"/>, which compares with
        /// <see cref="epsa"/>.
        /// </summary>
        /// <param name="d1">first value</param>
        /// <param name="d2">second value</param>
        /// <returns>true: almost identical, false: different</returns>
        public static bool IsEqual(double d1, double d2)
        {
            return Math.Abs(d1 - d2) < eps;
        }
        /// <summary>
		/// Determins, whether the directions of the given vectors are almost identical, i.e.
		/// the angular difference is less than epsa. This is also true for opposite directions.
		/// </summary>
		/// <param name="v1">first vector</param>
		/// <param name="v2">second vector</param>
		/// <param name="VectorsAreNormalized">true, if the vectors are already normalized, false if not or unknown</param>
		/// <returns>true: almost same direction, false: different directions</returns>
		public static bool SameDirection(GeoVector v1, GeoVector v2, bool VectorsAreNormalized)
        {
            GeoVector cross = v1 ^ v2;
            double cl = cross.Length;
            if (VectorsAreNormalized)
            {
                return cl < epsa; // im kleinen ist der Sinus gleich dem Bogenmaß
            }
            else
            {
                return cl / (v1.Length * v2.Length) < epsa;
            }
        }
        /// <summary>
        /// Determines whether the two vectors point in the same direction: they must be parallel within <see cref="epsa"/>
        /// and have the same orientation. <see cref="SameDirection(GeoVector, GeoVector, bool)"/> in contrast also accepts
        /// opposite vectors. The lengths of the vectors do not matter; the result is false if one of them is the nullvector.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>true: same direction and orientation, false otherwise</returns>
        public static bool SameNotOppositeDirection(GeoVector v1, GeoVector v2)
        {
            GeoVector cross = v1 ^ v2;
            double cl = cross.Length;
            double f;
            if (Math.Abs(v2.x) > Math.Abs(v2.y))
            {
                if (Math.Abs(v2.x) > Math.Abs(v2.z))
                {
                    f = v1.x / v2.x;
                }
                else
                {
                    f = v1.z / v2.z;
                }
            }
            else
            {
                if (Math.Abs(v2.y) > Math.Abs(v2.z))
                {
                    f = v1.y / v2.y;
                }
                else
                {
                    f = v1.z / v2.z;
                }
            }
            return (f > 0.0) && (cl / (v1.Length * v2.Length) < epsa);
        }
        /// <summary>
        /// Determines whether the two vectors point in opposite directions: they must be parallel within <see cref="epsa"/>
        /// and have opposite orientation. Same as <see cref="SameNotOppositeDirection(GeoVector, GeoVector)"/> with the
        /// second vector reversed.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>true: opposite directions, false otherwise</returns>
        public static bool OppositeDirection(GeoVector v1, GeoVector v2)
        {
            return SameNotOppositeDirection(v1, -v2);
        }
        /// <summary>
        /// Determines whether the two 2d vectors point in opposite directions: they must be parallel within <see cref="epsa"/>
        /// and have opposite orientation. Same as <see cref="SameNotOppositeDirection(GeoVector2D, GeoVector2D, bool)"/> with
        /// the second vector reversed.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>true: opposite directions, false otherwise</returns>
        public static bool OppositeDirection(GeoVector2D v1, GeoVector2D v2)
        {
            return SameNotOppositeDirection(v1, -v2, false);
        }
        /// <summary>
        /// Determins, whether the directions of the given vectors are almost identical, i.e.
        /// the angular difference is less than epsa.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <param name="VectorsAreNormalized">true, if the vectors are already normalized, false if not or unknown</param>
        /// <returns>true: almost same direction, false: different directions</returns>
        public static bool SameDirection(GeoVector2D v1, GeoVector2D v2, bool VectorsAreNormalized)
        {
            double cl = Math.Abs(v1.x * v2.y - v1.y * v2.x);
            if (VectorsAreNormalized)
            {
                return cl < epsa; // im kleinen ist der Sinus gleich dem Bogenmaß
            }
            else
            {
                return cl / (v1.Length * v2.Length) < epsa;
            }
        }
        /// <summary>
        /// Determines whether the two 2d vectors point in the same direction with the same orientation: their angles (see
        /// <see cref="GeoVector2D.Angle"/>) differ by less than <see cref="epsa"/>.
        /// <see cref="SameDirection(GeoVector2D, GeoVector2D, bool)"/> in contrast also accepts opposite vectors.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <param name="VectorsAreNormalized">not used, the lengths of the vectors do not matter</param>
        /// <returns>true: same direction and orientation, false otherwise</returns>
        public static bool SameNotOppositeDirection(GeoVector2D v1, GeoVector2D v2, bool VectorsAreNormalized)
        {
            Angle a1 = new Angle(v1);
            Angle a2 = new Angle(v2);
            return IsEqual(a1, a2);
        }
        /// <summary>
        /// Determines whether the two vectors are almost perpendicular, i.e. the cosine of the angle between them is less
        /// than <see cref="epsa"/>. Returns true if one of the vectors is shorter than <see cref="eps"/>, because a nullvector
        /// has no direction.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <param name="VectorsAreNormalized">not used, the vectors are normalized internally</param>
        /// <returns>true: perpendicular, false: not perpendicular</returns>
        public static bool IsPerpendicular(GeoVector v1, GeoVector v2, bool VectorsAreNormalized)
        {
            if (v1.Length < eps) return true; // oder was?
            if (v2.Length < eps) return true; // oder was?
            return Math.Abs(v1 * v2) / (v1.Length * v2.Length) < epsa;
        }
        /// <summary>
        /// Determines whether the two 2d vectors are almost perpendicular, i.e. the cosine of the angle between them is less
        /// than <see cref="epsa"/>. Returns true if one of the vectors is shorter than <see cref="eps"/>, because a nullvector
        /// has no direction.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <param name="VectorsAreNormalized">not used, the vectors are normalized internally</param>
        /// <returns>true: perpendicular, false: not perpendicular</returns>
        public static bool IsPerpendicular(GeoVector2D v1, GeoVector2D v2, bool VectorsAreNormalized)
        {
            if (v1.Length < eps) return true; // oder was?
            if (v2.Length < eps) return true; // oder was?
            return Math.Abs(v1 * v2) / (v1.Length * v2.Length) < epsa;
        }
        /// <summary>
		/// Determins, whether the length of the given vector is almost 0, i.e.
		/// the length is less than eps
		/// </summary>
		/// <param name="v">the vector to test</param>
		/// <returns>true if null-vector, false otherwise</returns>
		public static bool IsNullVector(GeoVector v)
        {
            return v.Length < eps;
        }
        /// <summary>
        /// Determines whether the vector is almost normalized, i.e. its length differs from 1.0 by less than <see cref="eps"/>.
        /// </summary>
        /// <param name="v">the vector to test</param>
        /// <returns>true if the length is almost 1.0, false otherwise</returns>
        public static bool IsNormedVector(GeoVector v)
        {
            return Math.Abs(v.Length - 1.0) < eps;
        }
        /// <summary>
        /// Determines whether the length of the given 2d vector is almost 0, i.e. the length is less than <see cref="eps"/>.
        /// </summary>
        /// <param name="v">the vector to test</param>
        /// <returns>true if null-vector, false otherwise</returns>
        public static bool IsNullVector(GeoVector2D v)
        {
            return v.Length < eps;
        }
        /// <summary>
        /// Determines whether the sweep angle is almost 0, i.e. its absolute value is less than <see cref="epsa"/>.
        /// </summary>
        /// <param name="sw">the sweep angle to test</param>
        /// <returns>true if almost 0, false otherwise</returns>
        public static bool IsNull(SweepAngle sw)
        {
            return Math.Abs((double)sw) < epsa;
        }
        /// <summary>
        /// Determines whether the angle is almost 0, i.e. its value is less than <see cref="epsa"/>. Since an
        /// <see cref="Angle"/> is always in the range [0, 2*pi), an angle slightly below 2*pi is not considered null; use
        /// <see cref="IsEqual(Angle, Angle)"/> with 0.0 when that case matters.
        /// </summary>
        /// <param name="a">the angle to test</param>
        /// <returns>true if almost 0, false otherwise</returns>
        public static bool IsNull(Angle a)
        {
            return Math.Abs((double)a) < epsa;
        }
        /// <summary>
        /// Determines whether the value is almost 0, i.e. its absolute value is less than <see cref="eps"/>. Intended for
        /// lengths and coordinates; for angles use <see cref="IsNull(Angle)"/> or <see cref="IsNull(SweepAngle)"/>.
        /// </summary>
        /// <param name="l">the value to test</param>
        /// <returns>true if almost 0, false otherwise</returns>
        public static bool IsNull(double l)
        {
            return Math.Abs(l) < eps;
        }

        /// <summary>
        /// Determins, whether the two planes are almost identical, i.e. the angular difference
        /// is less than epsa and the distance of then location of p2 to the plane p1 is less than
        /// eps. The DirectionX, DirectionY and Location properties of the two planes may be 
        /// completely different, the two coordinate systems of the planes may be different.
        /// </summary>
        /// <param name="p1">first plane</param>
        /// <param name="p2">second plane</param>
        /// <returns></returns>
        public static bool IsEqual(Plane p1, Plane p2)
        {
            if (SameDirection(p1.Normal, p2.Normal, true))
            {
                return Math.Abs(p1.Distance(p2.Location)) < eps;
            }
            return false;
        }
        /// <summary>
        /// Determins, whether the 3D point is on the plane. This is true when either the distance
        /// of the point to the plane is less than eps, or the elevation of the vector from the 
        /// location of the plane to the point is less than epsa
        /// </summary>
        /// <param name="p">The point</param>
        /// <param name="pl">The plane</param>
        /// <returns>true, if the point is on the plane</returns>
        public static bool IsPointOnPlane(GeoPoint p, Plane pl)
        {
            if (Math.Abs(pl.Distance(p)) < eps) return true;
            else return false;
            //GeoVector v1 = (p-pl.Location)^pl.DirectionX;
            //GeoVector v2 = (p-pl.Location)^pl.DirectionY;
            //double l1 = v1.Length;
            //double l2 = v2.Length; // beide können nicht 0 sein
            //if (l1>l2) return SameDirection(v1/l1,pl.Normal,true);
            //else return SameDirection(v2/l2,pl.Normal,true);
        }
        /// <summary>
        /// Determines whether the point lies on the line segment from <paramref name="startPoint"/> to
        /// <paramref name="endPoint"/>: its distance to the line must be less than <see cref="eps"/> and it must lie between
        /// the two end points (within <see cref="eps"/>). Points on the extension of the segment are not on the line; use
        /// <see cref="IsPointOnAxis(GeoPoint, Axis)"/> for the infinite line.
        /// </summary>
        /// <param name="testPoint">the point to test</param>
        /// <param name="startPoint">start point of the segment</param>
        /// <param name="endPoint">end point of the segment</param>
        /// <returns>true, if the point is on the segment</returns>
        public static bool IsPointOnLine(GeoPoint testPoint, GeoPoint startPoint, GeoPoint endPoint)
        {
            if (Math.Abs(Geometry.DistPL(testPoint, startPoint, endPoint)) < eps)
            {
                return Math.Abs((testPoint | startPoint) + (testPoint | endPoint) - (startPoint | endPoint)) < eps;
            }
            return false;
        }
        /// <summary>
        /// Determines whether the point lies on the (infinite) axis, i.e. its distance to the axis is below eps.
        /// </summary>
        public static bool IsPointOnAxis(GeoPoint testPoint, Axis axis)
        {
            return Math.Abs(Geometry.DistPL(testPoint, axis)) < eps;
        }
        /// <summary>
        /// Determines whether all points lie on a straight line. The points are expected to be ordered
        /// from start to end, the line is the connection of the first and the last one.
        /// </summary>
        /// <param name="pnts">the points to test, at least two</param>
        /// <returns>true, if all points are colinear</returns>
        public static bool IsColinear(GeoPoint2D[] pnts)
        {
            for (int i = 1; i < pnts.Length - 1; i++)
            {
                if (!IsPointOnLine(pnts[i], pnts[0], pnts[pnts.Length - 1])) return false;
            }
            return true;
        }
        /// <summary>
        /// Determines whether the 2d point lies on the line segment from <paramref name="startPoint"/> to
        /// <paramref name="endPoint"/>: its distance to the line must be less than <see cref="eps"/> and it must lie between
        /// the two end points (within <see cref="eps"/>). Points on the extension of the segment are not on the line.
        /// </summary>
        /// <param name="testPoint">the point to test</param>
        /// <param name="startPoint">start point of the segment</param>
        /// <param name="endPoint">end point of the segment</param>
        /// <returns>true, if the point is on the segment</returns>
        public static bool IsPointOnLine(GeoPoint2D testPoint, GeoPoint2D startPoint, GeoPoint2D endPoint)
        {
            if (Math.Abs(Geometry.DistPL(testPoint, startPoint, endPoint)) < eps)
            {
                return Math.Abs((testPoint | startPoint) + (testPoint | endPoint) - (startPoint | endPoint)) < eps;
            }
            return false;
        }
        /// <summary>
        /// Determines whether the two axes describe the same infinite line: the location of each axis lies on the other axis
        /// within <see cref="eps"/> and the directions are parallel within <see cref="epsa"/>. Opposite orientations count
        /// as the same axis. Like <see cref="Axis.SameGeometry(Axis)"/>, but tests both locations.
        /// </summary>
        /// <param name="a1">first axis</param>
        /// <param name="a2">second axis</param>
        /// <returns>true, if both axes are the same line</returns>
        public static bool SameAxis(Axis a1, Axis a2)
        {
            return Geometry.DistPL(a1.Location,a2)<eps && Geometry.DistPL(a2.Location, a1) < eps && SameDirection(a1.Direction, a2.Direction, false);
            // IsPointOnLine only checks in between start- and endpoint
            //return IsPointOnLine(a1.Location, a2.Location, a2.Location + a2.Direction) && SameDirection(a1.Direction, a2.Direction, false);
        }
        /// <summary>
        /// Determines, whether the given vector is in the given plane
        /// </summary>
        /// <param name="dir">The direction</param>
        /// <param name="pl">The plane</param>
        /// <returns>true, if the direction is in the plane</returns>
        public static bool IsDirectionInPlane(GeoVector dir, Plane pl)
        {
            return IsPerpendicular(dir, pl.Normal, false);
        }
        /// <summary>
        /// Returns true if the distance of each point from p to c is less than eps.
        /// </summary>
        /// <param name="c">the center to test to</param>
        /// <param name="p">points to test</param>
        /// <returns>true if all points are close, false otherwise</returns>
        public static bool IsEqual(GeoPoint2D c, params GeoPoint2D[] p)
        {
            for (int i = 0; i < p.Length; i++)
            {
                if (!IsEqual(c, p[i])) return false;
            }
            return true;
        }
        /// <summary>
        /// Returns true if the distance of each point from p to c is less than eps.
        /// </summary>
        /// <param name="c">the center to test to</param>
        /// <param name="p">points to test</param>
        /// <returns>true if all points are close, false otherwise</returns>
        public static bool IsEqual(GeoPoint c, params GeoPoint[] p)
        {
            for (int i = 0; i < p.Length; i++)
            {
                if (!IsEqual(c, p[i])) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Use this class to temporary override the values for the precision. The best
    /// way to use this class is the C# using statement. The constructor of this
    /// class overrides the values of <see cref="Precision"/> for the current thread, other threads
    /// keep their values. The <see cref="Dispose"/> method restores the previous values, it must be called
    /// on the same thread (so there must be no await inside the using statement).
    /// </summary>

    public class PrecisionOverride : IDisposable
    {
        private (double eps, double epsa) previous;
        private bool disposed;
        /// <summary>
        /// Sets Precision.eps for the current thread.
        /// </summary>
        /// <param name="eps">The new Precision.eps value</param>
        public PrecisionOverride(double eps)
        {
            previous = Precision.BeginOverride(eps, Precision.epsa);
        }

        /// <summary>
        /// Sets Precision.eps and Precision.epsa for the current thread.
        /// </summary>
        /// <param name="eps">The new Precision.eps value</param>
        /// <param name="epsa">The new Precision.epsa value</param>
        public PrecisionOverride(double eps, double epsa)
        {
            previous = Precision.BeginOverride(eps, epsa);
        }

        #region IDisposable Members

        /// <summary>
        /// Restores the previous (at time of construction of this object) <see cref="Precision"/> values.
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Precision.EndOverride(previous);
        }

        #endregion
    }
}
