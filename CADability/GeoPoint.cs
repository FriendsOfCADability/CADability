using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.Serialization;
using CADability.GeoObject;
using MathNet.Numerics.LinearAlgebra.Double;

namespace CADability
{
    internal class DebugFormat
    {
        public const string Coordinate = "f3";
        public const string Angle = "f3";
    }

    /// <summary>
    /// A 3-dimensional point with double components. The components are directly accesible
    /// to achieve maximum speed.
    /// <note>
    /// <para>Keep in mind that this is a value type. Passing a value type as a (non ref) parameter and changing it's value
    /// inside the invoked method leaves the original unchanged.
    /// </para>
    /// </note>
    /// </summary>
    [Serializable]
    [DebuggerDisplayAttribute("(x,y,z)={DebugString}")]
    [JsonVersion(serializeAsStruct = true, version = 1)]
    public struct GeoPoint : ISerializable, IJsonSerialize, IExportStep
    {
        /// <summary>
        /// x-component
        /// </summary>
        public double x;
        /// <summary>
        /// y-component
        /// </summary>
        public double y;
        /// <summary>
        /// z-component
        /// </summary>
        public double z;
        internal static double sqr(double a) { return a * a; }
        // public GeoPoint() { x = y = 0.0; }
        /// <summary>
        /// Constructs a new GeoPoint with given x and y components, z-component will be 0.0
        /// </summary>
        /// <param name="x">x-component</param>
        /// <param name="y">y-component</param>
        public GeoPoint(double x, double y)
        {
            this.x = x;
            this.y = y;
            this.z = 0.0;
        }
        /// <summary>
        /// Constructs a new GeoPoint from a 2-dimensional point and a z-component.
        /// </summary>
        /// <param name="p">supplies the x- and y-component</param>
        /// <param name="z">z-component</param>
        public GeoPoint(GeoPoint2D p, double z)
        {
            this.x = p.x;
            this.y = p.y;
            this.z = z;
        }
        /// <summary>
        /// Constructs a new GeoPoint with given x, y and z components.
        /// </summary>
        /// <param name="x">x-component</param>
        /// <param name="y">y-component</param>
        /// <param name="z">z-component</param>
        public GeoPoint(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        /// <summary>
        /// Creates a GeoPoint with the z-coordinate 0.0 and x,y set to p.x,p.y
        /// </summary>
        /// <param name="p">two dimensional point in the x/y Plane</param>
        public GeoPoint(GeoPoint2D p)
        {
            x = p.x;
            y = p.y;
            z = 0.0;
        }
        /// <summary>
        /// Constructs a new GeoPoint as on offset from an existing GeoPoint.
        /// </summary>
        /// <param name="StartWith">from here</param>
        /// <param name="OffsetX">x-offset</param>
        /// <param name="OffsetY">y-offset</param>
        /// <param name="OffsetZ">z-offset</param>
        public GeoPoint(GeoPoint StartWith, double OffsetX, double OffsetY, double OffsetZ)
        {
            x = StartWith.x + OffsetX;
            y = StartWith.y + OffsetY;
            z = StartWith.z + OffsetZ;
        }
        /// <summary>
        /// Constructs a new GeoPoint in the middle between two other GeoPoints
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        public GeoPoint(GeoPoint p1, GeoPoint p2)
        {
            x = (p1.x + p2.x) / 2;
            y = (p1.y + p2.y) / 2;
            z = (p1.z + p2.z) / 2;
        }

        /// <summary>
        /// Constructs a new GeoPoint by linear interpolation between two points: <c>(1 - ratio) * p1 + ratio * p2</c>.
        /// A <paramref name="ratio"/> of 0.0 yields <paramref name="p1"/>, 1.0 yields <paramref name="p2"/>, values outside
        /// the range [0, 1] extrapolate beyond the two points.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        /// <param name="ratio">position between the two points</param>
        public GeoPoint(GeoPoint p1, GeoPoint p2, double ratio)
        {
            x = (1 - ratio) * p1.x + ratio * p2.x;
            y = (1 - ratio) * p1.y + ratio * p2.y;
            z = (1 - ratio) * p1.z + ratio * p2.z;
        }
        /// <summary>
        /// Constructs a ne GeoPoint at the geometric middle of the provided points
        /// </summary>
        /// <param name="p"></param>
        public GeoPoint(params GeoPoint[] p)
        {
            x = 0.0;
            y = 0.0;
            z = 0.0;
            for (int i = 0; i < p.Length; ++i)
            {
                x += p[i].x;
                y += p[i].y;
                z += p[i].z;
            }
            x /= p.Length;
            y /= p.Length;
            z /= p.Length;
        }
        internal GeoPoint(PointF p)
        {
            x = p.X;
            y = p.Y;
            z = 0.0;
        }
        /// <summary>
        /// Constructs a new GeoPoint from a MathNet.Numerics vector with exactly three entries (x, y, z), typically the
        /// solution of a linear system. Throws an <see cref="ApplicationException"/> when the vector has a different length.
        /// </summary>
        /// <param name="d">vector with the three components</param>
        public GeoPoint(Vector d)
        {
            if (d.Count == 3)
            {
                x = d[0];
                y = d[1];
                z = d[2];
            }
            else
            {
                throw new ApplicationException("GeoPoint constructor: Length of array must be 3");
            }
        }
        /// <summary>
        /// Returns the distance from this point to the given point.
        /// </summary>
        /// <param name="To">target point</param>
        /// <returns>distance</returns>
        public double Distance(GeoPoint To)
        {
            return Math.Sqrt(sqr(x - To.x) + sqr(y - To.y) + sqr(z - To.z));
        }
        /// <summary>
        /// Returns the length of the polyline through the given points, i.e. the sum of the distances between consecutive
        /// points. Returns 0.0 for fewer than two points. Used e.g. to check whether the poles of a BSpline are degenerate.
        /// </summary>
        /// <param name="points">the points in order</param>
        /// <returns>sum of the distances between consecutive points</returns>
        public static double Distance(GeoPoint[] points)
        {
            double res = 0.0;
            for (int i = 1; i < points.Length; i++)
            {
                res += points[i - 1] | points[i];
            }
            return res;
        }
        internal double TaxicabDistance(GeoPoint To)
        {	// Betragsnorm, heißt echt auf Englisch Taxicab oder Manhattan
            return Math.Abs(x - To.x) + Math.Abs(y - To.y) + Math.Abs(z - To.z);
        }
        internal double Size
        {   // Größenordnung für Fehlerabschätzung
            get
            {
                return Math.Abs(x) + Math.Abs(y) + Math.Abs(z);
            }
        }
        /// <summary>
        /// True, when all three components are ordinary numbers, i.e. neither NaN nor infinite. <see cref="Invalid"/> and
        /// points that could not be computed carry NaN, and every comparison with NaN is false, so test this property
        /// before using a point that may have failed.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return !double.IsNaN(x) && !double.IsNaN(y) && !double.IsNaN(z) && !double.IsInfinity(x) && !double.IsInfinity(y) && !double.IsInfinity(z);
            }
        }
        /// <summary>
        /// Gives access to the components by index: 0 is <see cref="x"/>, 1 is <see cref="y"/>, 2 is <see cref="z"/>.
        /// Any other index throws an <see cref="IndexOutOfRangeException"/>. Convenient for loops over the coordinates.
        /// </summary>
        /// <param name="index">0, 1 or 2</param>
        /// <value>the component with that index</value>
        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    default: throw new IndexOutOfRangeException("index for GeoPoint must be 0, 1 or 2");
                }
            }
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException("index for GeoPoint must be 0, 1 or 2");
                }
            }
        }
        /// <summary>
        /// Central projection of this point onto a plane: the line through <paramref name="center"/> and this point is
        /// intersected with <paramref name="plane"/>, and the intersection is returned in the 2-dimensional coordinate system
        /// of the plane. Throws a <see cref="PlaneException"/> when that line is parallel to the plane and a
        /// <see cref="GeoVectorException"/> when this point coincides with <paramref name="center"/>. For a perpendicular
        /// projection use <see cref="Plane.Project(GeoPoint)"/>.
        /// </summary>
        /// <param name="plane">the plane to project onto</param>
        /// <param name="center">the center (eye point) of the projection</param>
        /// <returns>the projected point in the coordinate system of the plane</returns>
        public GeoPoint2D Project(Plane plane, GeoPoint center)
        {
            return plane.Project(plane.Intersect(this, this - center));
        }

        /// <summary>
        /// Determins whether two GeoPoints are exactly equal. Use <see cref="Precision.IsEqual(GeoPoint, GeoPoint)"/>
        /// if you need more control over precision.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">secont point</param>
        /// <returns>true if equal</returns>
        public static bool operator ==(GeoPoint p1, GeoPoint p2)
        {
            return p1.x == p2.x && p1.y == p2.y && p1.z == p2.z;
        }
        /// <summary>
        /// Determins whether two GeoPoints are not equal. Use <see cref="Precision.IsEqual(GeoPoint, GeoPoint)"/>
        /// if you need more control over precision.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">secont point</param>
        /// <returns>true if not equal</returns>
        public static bool operator !=(GeoPoint p1, GeoPoint p2)
        {
            return p1.x != p2.x || p1.y != p2.y || p1.z != p2.z;
        }
        /// <summary>
        /// Returns the vector that points from p1 to p2.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">secont point</param>
        /// <returns>the vector</returns>
        public static GeoVector operator -(GeoPoint p1, GeoPoint p2)
        {
            return new GeoVector(p1.x - p2.x, p1.y - p2.y, p1.z - p2.z);
        }
        /// <summary>
        /// Adds a vector (offset) to a GeoPoint
        /// </summary>
        /// <param name="p">the point</param>
        /// <param name="v">the vector</param>
        /// <returns>offset point</returns>
        public static GeoPoint operator +(GeoPoint p, GeoVector v)
        {
            return new GeoPoint(p.x + v.x, p.y + v.y, p.z + v.z);
        }
        /// <summary>
        /// Subtracts a vector (offset) from a GeoPoint
        /// </summary>
        /// <param name="p">the point</param>
        /// <param name="v">the vector</param>
        /// <returns>offset point</returns>
        public static GeoPoint operator -(GeoPoint p, GeoVector v)
        {
            return new GeoPoint(p.x - v.x, p.y - v.y, p.z - v.z);
        }
        /// <summary>
        /// Negates all components, i.e. reflects the point at the origin.
        /// </summary>
        /// <param name="p">the point</param>
        /// <returns>the point with negated components</returns>
        public static GeoPoint operator -(GeoPoint p)
        {
            return new GeoPoint(-p.x, -p.y, -p.z);
        }
        /// <summary>
        /// Returns the distance of the two points
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        /// <returns>the distance</returns>
        public static double operator |(GeoPoint p1, GeoPoint p2)
        {
            return Geometry.Dist(p1, p2);
        }
        /// <summary>
        /// Returns the squared distance of the two points. Cheaper than the distance (the <c>|</c> operator) because no
        /// square root is taken; use it when distances are only compared.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        /// <returns>the squared distance</returns>
        public static double operator &(GeoPoint p1, GeoPoint p2)
        {
            return (p1.x - p2.x) * (p1.x - p2.x) + (p1.y - p2.y) * (p1.y - p2.y) + (p1.z - p2.z) * (p1.z - p2.z);
        }
        /// <summary>
        /// Applies a 3x3 MathNet.Numerics matrix to the point, i.e. a linear transformation without translation.
        /// The matrix must have three rows and three columns, which is only checked in debug builds. For affine
        /// transformations use <see cref="ModOp"/>.
        /// </summary>
        /// <param name="m">the 3x3 matrix</param>
        /// <param name="p">the point</param>
        /// <returns>the transformed point</returns>
        public static GeoPoint operator *(Matrix m, GeoPoint p)
        {
            System.Diagnostics.Debug.Assert(m.ColumnCount == 3 && m.RowCount == 3);
            return new GeoPoint(m[0, 0] * p.x + m[0, 1] * p.y + m[0, 2] * p.z, m[1, 0] * p.x + m[1, 1] * p.y + m[1, 2] * p.z, m[2, 0] * p.x + m[2, 1] * p.y + m[2, 2] * p.z);
        }

        /// <summary>
        /// Converts the point to an array of its components, e.g. to pass it to numerical routines.
        /// </summary>
        /// <param name="p">the point</param>
        /// <returns>the array {x, y, z}</returns>
        public static implicit operator double[](GeoPoint p) => new double[] { p.x, p.y, p.z };

        /// <summary>
        /// returns the origin, same as new GeoPoint(0.0,0.0,0.0);
        /// </summary>
        public static GeoPoint Origin { get { return new GeoPoint(0.0, 0.0, 0.0); } }
        /// <summary>
        /// returns new GeoPoint(NaN, NaN, NaN);
        /// </summary>
        public static GeoPoint Invalid { get { return new GeoPoint(double.NaN, double.NaN, double.NaN); } }
        /// <summary>
        /// Overrides object.Equals
        /// </summary>
        /// <param name="o">object to compare with</param>
        /// <returns>true if equal</returns>
        public override bool Equals(Object o)
        {
            if (o.GetType() == typeof(GeoPoint))
            {
                GeoPoint p = (GeoPoint)o;
                return (x == p.x && y == p.y && z == p.z);
            }
            else return false;
        }
        /// <summary>
        /// Overrides object.GetHashCode()
        /// </summary>
        /// <returns>hashcode</returns>
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        }
        /// <summary>
        /// Overrides object.ToString()
        /// </summary>
        /// <returns>x.ToString()+", "+y.ToString()+", "+z.ToString()</returns>
        public override string ToString()
        {
            return x.ToString() + ", " + y.ToString() + ", " + z.ToString();
        }
        internal string DebugString
        {
            get
            {
                return x.ToString(DebugFormat.Coordinate) + ", " + y.ToString(DebugFormat.Coordinate) + ", " + z.ToString(DebugFormat.Coordinate);
            }
        }
        /// <summary>
        /// Returns this point modified by <see cref="ModOp"/> m as a System.Drawing.PointF
        /// </summary>
        /// <param name="m">modify by</param>
        /// <returns>modified point</returns>
        public PointF ToPointF(ModOp m)
        {
            GeoPoint p = m * this;
            return new PointF((float)p.x, (float)p.y);
        }
        internal PointF ToPointF()
        {
            return new PointF((float)x, (float)y);
        }
        /// <summary>
        /// returns a 2-dimensional point by ommiting the z-component.
        /// </summary>
        /// <returns></returns>
        public GeoPoint2D To2D()
        {
            return new GeoPoint2D(x, y);
        }
        /// <summary>
        /// Returns the vector that points from the origin to this point
        /// </summary>
        /// <returns></returns>
        public GeoVector ToVector()
        {
            return new GeoVector(x, y, z);
        }
        /// <summary>
        /// Creates a new GeoPoint at the center of the points provided in the parameter list.
        /// </summary>
        /// <param name="p">List of points</param>
        /// <returns>The geometric center of p</returns>
        public static GeoPoint Center(params GeoPoint[] p)
        {
            GeoPoint res = p[0];
            for (int i = 1; i < p.Length; i++)
            {
                res.x += p[i].x;
                res.y += p[i].y;
                res.z += p[i].z;
            }
            res.x /= p.Length;
            res.y /= p.Length;
            res.z /= p.Length;
            return res;
        }

        #region ISerializable Members
        /// <summary>
        /// Constructor for ISerializable
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public GeoPoint(SerializationInfo info, StreamingContext context)
        {
            //			x = (double)info.GetValue("X",typeof(double));
            //			y = (double)info.GetValue("Y",typeof(double));
            //			z = (double)info.GetValue("Z",typeof(double));
            x = info.GetDouble("X");
            y = info.GetDouble("Y");
            z = info.GetDouble("Z");
            //x = y = z = 0.0;
            //SerializationInfoEnumerator e = info.GetEnumerator();
            //while (e.MoveNext())
            //{
            //    switch (e.Name)
            //    {   
            //        default:
            //            break;
            //        case "X":
            //            x = (double)e.Value;
            //            break;
            //        case "Y":
            //            y = (double)e.Value;
            //            break;
            //        case "Z":
            //            z = (double)e.Value;
            //            break;
            //    }
            //}

        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("X", x, typeof(double));
            info.AddValue("Y", y, typeof(double));
            info.AddValue("Z", z, typeof(double));
        }
        #endregion
        #region IJsonSerialize Members
        internal GeoPoint(IJsonReadStruct data)
        {
            x = data.GetValue<double>();
            y = data.GetValue<double>();
            z = data.GetValue<double>();
        }

        /// <summary>
        /// Writes the three components for the JSON serialization (<see cref="IJsonSerialize"/>). The struct is serialized inline as a
        /// plain list of values and read back by the constructor taking an <see cref="IJsonReadStruct"/>.
        /// </summary>
        /// <param name="data">receives the values</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(x, y, z);
        }

        /// <summary>
        /// Does nothing. Value types serialized inline are restored by the constructor taking an <see cref="IJsonReadStruct"/>;
        /// this method only exists to satisfy <see cref="IJsonSerialize"/>.
        /// </summary>
        /// <param name="data">not used</param>
        public void SetObjectData(IJsonReadData data)
        {
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            return export.WriteDefinition("CARTESIAN_POINT('',(" + export.ToString(x) + ","
                + export.ToString(y) + "," + export.ToString(z) + "))");
        }

        #endregion
    }

    /// <summary>
    /// ApplicationException thrown by some <see cref="GeoVector"/> operations.
    /// </summary>

    public class GeoVectorException : System.ApplicationException
    {
        /// <summary>
        /// Type of exception.
        /// </summary>
        public enum tExceptionType
        {
            /// <summary>
            /// a null vector was involved in an operation that cannot deal with a null vector.
            /// </summary>
            NullVector
        };
        /// <summary>
        /// Type of exception.
        /// </summary>
        public tExceptionType ExceptionType;
        internal GeoVectorException(tExceptionType ExceptionType)
        {
            this.ExceptionType = ExceptionType;
        }
    }

    /// <summary>
    /// A 3-dimensional vector with double x,y and z components. The vector is not necessary normalized.
    /// <alert class="caution">
    /// Keep in mind that this is a value type. Passing a value type as a (non ref) parameter and changing it's value
    /// inside the invoked method leaves the original unchanged.
    /// </alert>
    /// </summary>
    [Serializable]
    [JsonVersion(serializeAsStruct = true, version = 1)]
    [DebuggerDisplayAttribute("(x,y,z)={DebugString}")]
    public struct GeoVector : ISerializable, IJsonSerialize, IExportStep
    {
        /// <summary>
        /// x-component
        /// </summary>
        public double x;
        /// <summary>
        /// y-component
        /// </summary>
        public double y;
        /// <summary>
        /// z-component
        /// </summary>
        public double z;
        /// <summary>
        /// Constructs a new GeoVector with the given components
        /// </summary>
        /// <param name="x">x-component</param>
        /// <param name="y">y-component</param>
        /// <param name="z">z-component</param>
        public GeoVector(double x, double y, double z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        /// <summary>
        /// Constructs a new GeoVector from a 2d vector assumed in the xy plane
        /// </summary>
        public GeoVector(GeoVector2D v)
        {
            this.x = v.x;
            this.y = v.y;
            this.z = 0.0;
        }
        /// <summary>
        /// Constructs a new GeoVector which points from the  StartPoint to the EndPoint.
        /// </summary>
        /// <param name="StartPoint">from here</param>
        /// <param name="EndPoint">to here</param>
        public GeoVector(GeoPoint StartPoint, GeoPoint EndPoint)
        {
            x = EndPoint.x - StartPoint.x;
            y = EndPoint.y - StartPoint.y;
            z = EndPoint.z - StartPoint.z;
        }
        /// <summary>
        /// Constructs a new GeoVector by linear interpolation between two vectors: <c>(1 - ratio) * v1 + ratio * v2</c>.
        /// A <paramref name="ratio"/> of 0.0 yields <paramref name="v1"/>, 1.0 yields <paramref name="v2"/>. The result is
        /// not normalized.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <param name="ratio">position between the two vectors</param>
        public GeoVector(GeoVector v1, GeoVector v2, double ratio)
        {
            x = (1 - ratio) * v1.x + ratio * v2.x;
            y = (1 - ratio) * v1.y + ratio * v2.y;
            z = (1 - ratio) * v1.z + ratio * v2.z;
        }
        /// <summary>
        /// Constructs a new GeoVector as the arithmetic mean of the given vectors (their sum divided by their number).
        /// The result is not normalized; an empty list yields NaN components.
        /// </summary>
        /// <param name="v">the vectors to average</param>
        public GeoVector(params GeoVector[] v)
        {
            x = 0.0;
            y = 0.0;
            z = 0.0;
            for (int i = 0; i < v.Length; ++i)
            {
                x += v[i].x;
                y += v[i].y;
                z += v[i].z;
            }
            x /= v.Length;
            y /= v.Length;
            z /= v.Length;
        }

        /// <summary>
        /// Constructs a new GeoVector with the given angles a longitude and latitude.
        /// The resulting GeoVector is normed.
        /// </summary>
        /// <param name="longitude"></param>
        /// <param name="latitude"></param>
        public GeoVector(Angle longitude, Angle latitude)
        {
            double c = Math.Cos(latitude);
            x = c * Math.Cos(longitude.Radian);
            y = c * Math.Sin(longitude.Radian);
            z = Math.Sin(latitude);
        }
        /// <summary>
        /// Returns true, if this vector is exactly the nullvector. Use <see cref="Precision.IsNullVector(GeoVector)"/>
        /// if you need more control over precision.
        /// </summary>
        /// <returns>true if nullvector</returns>
        public bool IsNullVector()
        {
            return (x == 0.0 && y == 0.0 && z == 0.0);
        }
        /// <summary>
        /// Returns true, when the vector is an ordinary number, false when it is <see cref="Invalid"/> or the result of a
        /// failed computation. Only the x-component is tested for NaN.
        /// </summary>
        /// <returns>true if valid</returns>
        public bool IsValid()
        {
            return !double.IsNaN(x);
        }
        /// <summary>
        /// Determines whether this vector and the othe vector are perpendicular. Use <see cref="Precision.IsPerpendicular(GeoVector, GeoVector, bool)"/>
        /// if you need more control over precision.
        /// </summary>
        /// <param name="other">other vector</param>
        /// <returns>true if perpendicular</returns>
        public bool IsPerpendicular(GeoVector other)
        {	// ggf. zweite methode mit Epsilon machen
            if (Length == 0.0) return true; // oder was?
            if (other.Length == 0.0) return true; // oder was?
            return Math.Abs(this * other) / (Length * other.Length) < 1e-6;
        }
        /// <summary>
        /// Converts the vector to an array of its components, e.g. to pass it to numerical routines.
        /// </summary>
        /// <param name="v">the vector</param>
        /// <returns>the array {x, y, z}</returns>
        public static implicit operator double[](GeoVector v) => new double[] { v.x, v.y, v.z };
        /// <summary>
        /// Gives access to the components by index: 0 is <see cref="x"/>, 1 is <see cref="y"/>, 2 is <see cref="z"/>.
        /// Any other index throws an <see cref="IndexOutOfRangeException"/>. Convenient for loops over the coordinates.
        /// </summary>
        /// <param name="index">0, 1 or 2</param>
        /// <value>the component with that index</value>
        public double this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    default: throw new IndexOutOfRangeException("index for GeoVector must be 0, 1 or 2");
                }
            }
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException("index for GeoVector must be 0, 1 or 2");
                }
            }
        }

        /// <summary>
        /// Cross product of the to vectors.
        /// </summary>
        /// <param name="left">left operand</param>
        /// <param name="right">right operand</param>
        /// <returns>Cross product</returns>
        public static GeoVector operator ^(GeoVector left, GeoVector right)
        {
            GeoVector Result;
            Result.x = left.y * right.z - left.z * right.y;
            Result.y = left.z * right.x - left.x * right.z;
            Result.z = left.x * right.y - left.y * right.x;
            return Result;

        }
        /// <summary>
        /// Reverses the driection of the given vector
        /// </summary>
        /// <param name="v">vector to reverse</param>
        /// <returns>reversed vector</returns>
        public static GeoVector operator -(GeoVector v)
        {
            return new GeoVector(-v.x, -v.y, -v.z);
        }
        /// <summary>
        /// Determins whether the given vectors are exactly equal. Use <see cref="Precision.SameDirection(GeoVector, GeoVector, bool)"/>
        /// if you need more control over precision.
        /// </summary>
        /// <param name="v1">first operand</param>
        /// <param name="v2">second operand</param>
        /// <returns>true if equal</returns>
        public static bool operator ==(GeoVector v1, GeoVector v2)
        {
            return (v1.x == v2.x && v1.y == v2.y && v1.z == v2.z);
        }
        /// <summary>
        /// Determins whether the given vectors are not exactly equal. Use <see cref="Precision.SameDirection(GeoVector, GeoVector, bool)"/>
        /// if you need more control over precision.
        /// </summary>
        /// <param name="v1">first operand</param>
        /// <param name="v2">second operand</param>
        /// <returns>true if not equal</returns>
        public static bool operator !=(GeoVector v1, GeoVector v2)
        {
            return !(v1.x == v2.x && v1.y == v2.y && v1.z == v2.z);
        }
        /// <summary>
        /// Overrides object.Equals.
        /// </summary>
        /// <param name="obj">object to compare with</param>
        /// <returns>true if exactly equal</returns>
        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(GeoVector)) return false;
            return this == (GeoVector)obj;
        }
        /// <summary>
        /// Overrides object.GetHashCode
        /// </summary>
        /// <returns>the hashcode</returns>
        public override int GetHashCode()
        {
            return x.GetHashCode() | y.GetHashCode() | z.GetHashCode();
        }
        /// <summary>
        /// Calculates the scalar product (dot product, inner product) of the
        /// two given vectors
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>the scalar product</returns>
        public static double operator *(GeoVector v1, GeoVector v2)
        {
            return v1.x * v2.x + v1.y * v2.y + v1.z * v2.z;
        }
        /// <summary>
        /// Applies a 3x3 MathNet.Numerics matrix to the vector, i.e. a linear transformation without translation.
        /// The matrix must have three rows and three columns, which is only checked in debug builds. For affine
        /// transformations use <see cref="ModOp"/>.
        /// </summary>
        /// <param name="m">the 3x3 matrix</param>
        /// <param name="v">the vector</param>
        /// <returns>the transformed vector</returns>
        public static GeoVector operator *(Matrix m, GeoVector v)
        {
            System.Diagnostics.Debug.Assert(m.ColumnCount == 3 && m.RowCount == 3);
            return new GeoVector(m[0, 0] * v.x + m[0, 1] * v.y + m[0, 2] * v.z, m[1, 0] * v.x + m[1, 1] * v.y + m[1, 2] * v.z, m[2, 0] * v.x + m[2, 1] * v.y + m[2, 2] * v.z);
        }
        /// <summary>
        /// Scales the given vector by the given double
        /// </summary>
        /// <param name="d">factor</param>
        /// <param name="v">vector</param>
        /// <returns>scaled vector</returns>
        public static GeoVector operator *(double d, GeoVector v)
        {
            return new GeoVector(d * v.x, d * v.y, d * v.z);
        }
        /// <summary>
        /// Divides the given GeoVector <paramref name="v"/> by the given scalar value <paramref name="v"/>.
        /// </summary>
        /// <param name="v">vector</param>
        /// <param name="d">divider</param>
        /// <returns>scaled vector</returns>
        public static GeoVector operator /(GeoVector v, double d)
        {
            return new GeoVector(v.x / d, v.y / d, v.z / d);
        }
        /// <summary>
        /// Adds the two vectors.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>sum</returns>
        public static GeoVector operator +(GeoVector v1, GeoVector v2)
        {
            return new GeoVector(v1.x + v2.x, v1.y + v2.y, v1.z + v2.z);
        }
        /// <summary>
        /// Subtracts the second vector from the first vector
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>difference</returns>
        public static GeoVector operator -(GeoVector v1, GeoVector v2)
        {
            return new GeoVector(v1.x - v2.x, v1.y - v2.y, v1.z - v2.z);
        }
        /// <summary>
        /// Returns the cosine of the angle between the two vectors. Throws an arithmetic exception if any of the vectors
        /// is the nullvector.
        /// </summary>
        /// <param name="v1">First vector</param>
        /// <param name="v2">Second vector</param>
        /// <returns>Cosine of the inner angle</returns>
        public static double Cos(GeoVector v1, GeoVector v2)
        {
            return (v1 * v2) / (v1.Length * v2.Length);
        }
        /// <summary>
        /// The x-axis, same as new GeoVector(1.0,0.0,0.0);
        /// </summary>
        public static readonly GeoVector XAxis = new GeoVector(1.0, 0.0, 0.0);
        /// <summary>
        /// The y-axis, same as new GeoVector(0.0,1.0,0.0);
        /// </summary>
        public static readonly GeoVector YAxis = new GeoVector(0.0, 1.0, 0.0);
        /// <summary>
        /// The z-axis, same as new GeoVector(0.0,0.0,1.0);
        /// </summary>
        public static readonly GeoVector ZAxis = new GeoVector(0.0, 0.0, 1.0);
        /// <summary>
        /// An invalid GeoVector (NaN,NaN,NaN)
        /// </summary>
        public static GeoVector Invalid { get { return new GeoVector(double.NaN, double.NaN, double.NaN); } }
        /// <summary>
        /// The nullvector, same as new GeoVector(0.0,0.0,0.0);
        /// </summary>
        public static readonly GeoVector NullVector = new GeoVector(0.0, 0.0, 0.0);
        /// <summary>
        /// The three main axes <see cref="XAxis"/>, <see cref="YAxis"/> and <see cref="ZAxis"/> as an array, convenient for iterating over all axes.
        /// </summary>
        internal static readonly GeoVector[] MainAxis = new GeoVector[] { XAxis, YAxis, ZAxis };
        /// <summary>
        /// Returns the length of this vector.
        /// </summary>
        public double Length
        {
            get { return Math.Sqrt(x * x + y * y + z * z); }
            set
            {
                double f = value / Math.Sqrt(x * x + y * y + z * z);
                x *= f;
                y *= f;
                z *= f;
            }
        }
        /// <summary>
        /// The squared length of this vector (x*x + y*y + z*z). Cheaper than <see cref="Length"/> because no square root is
        /// taken; use it when lengths are only compared.
        /// </summary>
        public double LengthSqared
        {
            get { return x * x + y * y + z * z; }
        }
        /// <summary>
        /// The taxicab (Manhattan) length of this vector, i.e. the sum of the absolute values of the components. A cheap
        /// estimate of the size of the vector for tolerance checks, not the euclidean <see cref="Length"/>.
        /// </summary>
        public double TaxicabLength
        {
            get
            {
                return Math.Abs(x) + Math.Abs(y) + Math.Abs(z);
            }
        }
        /// <summary>
        /// Normalizes this vector. After this operation the vector will have the <see cref="Length"/> 1.0
        /// Throws a <see cref="GeoVectorException"/> if the vector is the nullvector.
        /// </summary>
        public void Norm()
        {
            if (Length <= 0.0) throw new GeoVectorException(GeoVectorException.tExceptionType.NullVector);
            double f = 1 / Length;
            x *= f;
            y *= f;
            z *= f;
        }
        /// <summary>
        /// A copy of this vector scaled to <see cref="Length"/> 1.0; this vector itself remains unchanged (see <see cref="Norm"/>
        /// to normalize in place). Throws a <see cref="GeoVectorException"/> if this vector is the nullvector.
        /// </summary>
        public GeoVector Normalized
        {
            get
            {
                if (Length <= 0.0) throw new GeoVectorException(GeoVectorException.tExceptionType.NullVector);
                double f = 1 / Length;
                return new GeoVector(x * f, y * f, z * f);
            }
        }

        /// <summary>
        /// Normalizes this vector to <see cref="Length"/> 1.0 like <see cref="Norm"/>, but leaves the nullvector unchanged
        /// instead of throwing an exception.
        /// </summary>
        public void NormIfNotNull()
        {
            if (Length <= 0.0) return;
            double f = 1 / Length;
            x *= f;
            y *= f;
            z *= f;
        }
        /// <summary>
        ///  Reverses this vector.
        /// </summary>
        public void Reverse()
        {
            x = -x;
            y = -y;
            z = -z;
        }
        #region CndOCas Conversion
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        public GeoVector(SerializationInfo info, StreamingContext context)
        {
            x = (double)info.GetValue("X", typeof(double));
            y = (double)info.GetValue("Y", typeof(double));
            z = (double)info.GetValue("Z", typeof(double));
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("X", x, typeof(double));
            info.AddValue("Y", y, typeof(double));
            info.AddValue("Z", z, typeof(double));
        }

        #endregion
        /// <inheritdoc/>
        public override string ToString()
        {
            return "(" + x.ToString() + ", " + y.ToString() + ", " + z.ToString() + ")";
        }
        internal string DebugString
        {
            get
            {
                return x.ToString(DebugFormat.Coordinate) + ", " + y.ToString(DebugFormat.Coordinate) + ", " + z.ToString(DebugFormat.Coordinate);
            }
        }
        /// <summary>
        /// Returns the coresponding 2d vector by omitting the z coordinate
        /// </summary>
        /// <returns>the coresponding 2d vector</returns>
        public GeoVector2D To2D()
        {
            return new GeoVector2D(x, y);
        }
        /// <summary>
        /// Computes two unit vectors that are perpendicular to this vector and to each other, e.g. to span a plane
        /// perpendicular to an axis. <paramref name="dirx"/>, <paramref name="diry"/> and this vector form a right handed
        /// system. The choice of <paramref name="dirx"/> is arbitrary but deterministic, it depends on which component of
        /// this vector is the largest. Throws a <see cref="GeoVectorException"/> if this vector is the nullvector.
        /// </summary>
        /// <param name="dirx">first perpendicular unit vector</param>
        /// <param name="diry">second perpendicular unit vector, the normalized cross product of this vector and <paramref name="dirx"/></param>
        public void ArbitraryNormals(out GeoVector dirx, out GeoVector diry)
        {
            switch (MainDirection)
            {
                case GeoVector.Direction.XAxis:
                    dirx = (this ^ GeoVector.YAxis).Normalized;
                    break;
                case GeoVector.Direction.YAxis:
                    dirx = (this ^ GeoVector.ZAxis).Normalized;
                    break;
                default:
                case GeoVector.Direction.ZAxis:
                    dirx = (this ^ GeoVector.XAxis).Normalized;
                    break;
            }
            diry = (this ^ dirx).Normalized;
        }
        internal enum Direction { XAxis, YAxis, ZAxis };
        internal Direction MainDirection
        {
            get
            {
                if (Math.Abs(x) > Math.Abs(y))
                {
                    if (Math.Abs(x) > Math.Abs(z)) return Direction.XAxis;
                    else return Direction.ZAxis;
                }
                else
                {
                    if (Math.Abs(y) > Math.Abs(z)) return Direction.YAxis;
                    else return Direction.ZAxis;
                }
            }
        }
#if DEBUG
        CADability.GeoObject.IGeoObject Debug
        {
            get
            {
                CADability.GeoObject.Line l = CADability.GeoObject.Line.Construct();
                l.SetTwoPoints(GeoPoint.Origin, GeoPoint.Origin + this);
                return l;
            }
        }
#endif
        /// <summary>
        /// Returns the bisector vector of the two provided vectors. The result will be normalized (length = 1)
        /// </summary>
        /// <param name="v1">First vector</param>
        /// <param name="v2">Second vector</param>
        /// <returns>The bisector</returns>
        public static GeoVector Bisector(GeoVector v1, GeoVector v2)
        {
            return (v1.Normalized + v2.Normalized).Normalized;
        }

        /// <summary>
        /// Constructor for the JSON deserialization (<see cref="IJsonSerialize"/>): reads the three components in the order
        /// <see cref="GetObjectData(IJsonWriteData)"/> wrote them. It is found by reflection, there is no need to call it directly.
        /// </summary>
        /// <param name="data">supplies the values</param>
        public GeoVector(IJsonReadStruct data)
        {
            x = data.GetValue<double>();
            y = data.GetValue<double>();
            z = data.GetValue<double>();
        }
        /// <summary>
        /// Writes the three components for the JSON serialization (<see cref="IJsonSerialize"/>). The struct is serialized inline as a
        /// plain list of values and read back by the constructor taking an <see cref="IJsonReadStruct"/>.
        /// </summary>
        /// <param name="data">receives the values</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(x, y, z);
        }

        /// <summary>
        /// Does nothing. Value types serialized inline are restored by the constructor taking an <see cref="IJsonReadStruct"/>;
        /// this method only exists to satisfy <see cref="IJsonSerialize"/>.
        /// </summary>
        /// <param name="data">not used</param>
        public void SetObjectData(IJsonReadData data)
        {
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            return export.WriteDefinition("DIRECTION('',(" + export.ToString(x) + ","
                + export.ToString(y) + "," + export.ToString(z) + "))");
        }
    }

    internal struct FreeCoordSys
    {
        public GeoPoint Location;
        public GeoVector DirectionX;
        public GeoVector DirectionY;
        public GeoVector DirectionZ;
        public FreeCoordSys(GeoPoint loc, GeoVector dirx, GeoVector diry, GeoVector dirz)
        {
            Location = loc;
            DirectionX = dirx;
            DirectionY = diry;
            DirectionZ = dirz;
        }
        public Plane plane
        {
            get
            {
                return new Plane(Location, DirectionX.Normalized, DirectionY.Normalized);
            }
        }
        public bool IsNormalized
        {
            get
            {
                if (Math.Abs(DirectionX.Length - 1.0) > 1e-6) return false;
                if (Math.Abs(DirectionY.Length - 1.0) > 1e-6) return false;
                if (Math.Abs(DirectionZ.Length - 1.0) > 1e-6) return false;
                if (!Precision.IsPerpendicular(DirectionX, DirectionY, true)) return false;
                if (!Precision.IsPerpendicular(DirectionY, DirectionZ, true)) return false;
                if (!Precision.IsPerpendicular(DirectionX, DirectionZ, true)) return false;
                return true;
            }
        }
        public override string ToString()
        {
            return Location.ToString() + " X:" + DirectionX.ToString() + " Y:" + DirectionY.ToString() + " Z:" + DirectionZ.ToString();
        }
    }
    /// <summary>
    /// An axis given by a location and a direction. There is no orientation for an
    /// x-direction or y-direction. If you need that use <see cref="CoordSys"/>
    /// <alert class="caution">
    /// Keep in mind that this is a value type. Passing a value type as a (non ref) parameter and changing it's value
    /// inside the invoked method leaves the original unchanged.
    /// </alert>
    /// </summary>
    [Serializable()]
    [JsonVersion(serializeAsStruct = true, version = 1)]
    public struct Axis : ISerializable, IJsonSerialize
    {
        /// <summary>
        /// An arbitrary location of the axis
        /// </summary>
        public GeoPoint Location;
        /// <summary>
        /// The direction of the axis
        /// </summary>
        public GeoVector Direction;
        /// <summary>
        /// Creates a new axis from two points
        /// </summary>
        /// <param name="StartPoint">startpoint</param>
        /// <param name="EndPoint">direction point</param>
        public Axis(GeoPoint StartPoint, GeoPoint EndPoint)
        {
            Location = StartPoint;
            Direction = new GeoVector(StartPoint, EndPoint);
        }
        /// <summary>
        /// Creates a new axis from a location and a direction
        /// </summary>
        /// <param name="location">location</param>
        /// <param name="direction">direction</param>
        public Axis(GeoPoint location, GeoVector direction)
        {
            Location = location;
            Direction = direction;
        }
        /// <summary>
        /// An axis marked as invalid: its <see cref="Location"/> is <see cref="GeoPoint.Invalid"/> and its <see cref="Direction"/>
        /// the nullvector. Returned by methods that could not determine an axis; test with <see cref="IsValid"/>.
        /// </summary>
        static public Axis InvalidAxis
        {
            get
            {
                return new Axis(GeoPoint.Invalid, GeoVector.NullVector);
            }
        }
        /// <summary>
        /// True, when the <see cref="Location"/> is a valid point (see <see cref="GeoPoint.IsValid"/>), false for
        /// <see cref="InvalidAxis"/>. The direction is not checked.
        /// </summary>
        public bool IsValid
        {
            get { return Location.IsValid; }
        }
        /// <summary>
        /// Determines whether the other axis describes the same infinite line: the directions are parallel within
        /// <see cref="Precision.epsa"/> (opposite orientation counts as parallel) and the location of <paramref name="other"/>
        /// lies on this axis within <see cref="Precision.eps"/>. See also <see cref="Precision.SameAxis(Axis, Axis)"/>.
        /// </summary>
        /// <param name="other">the axis to compare with</param>
        /// <returns>true if both axes are geometrically the same line</returns>
        public bool SameGeometry(Axis other)
        {
            if (Precision.SameDirection(Direction,other.Direction,false))
            {
                if (Geometry.DistPL(other.Location, this) < Precision.eps) return true;
            }
            return false;
        }
        /// <summary>
        /// Returns the perpendicular distance of the point to the infinite line of this axis. The result is never negative.
        /// If the direction is the nullvector, the distance to <see cref="Location"/> is returned.
        /// </summary>
        /// <param name="p">the point</param>
        /// <returns>the distance</returns>
        public double Distance(GeoPoint p)
        {
            return Geometry.DistPL(p, this);
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        public Axis(SerializationInfo info, StreamingContext context)
        {
            Location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            Direction = (GeoVector)info.GetValue("Direction", typeof(GeoVector));
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Location", Location, typeof(GeoPoint));
            info.AddValue("Direction", Direction, typeof(GeoVector));
        }
        /// <summary>
        /// Constructor for the JSON deserialization (<see cref="IJsonSerialize"/>): reads the location and the direction in the order
        /// <see cref="GetObjectData(IJsonWriteData)"/> wrote them. It is found by reflection, there is no need to call it directly.
        /// </summary>
        /// <param name="data">supplies the values</param>
        public Axis(IJsonReadStruct data)
        {
            Location = data.GetValue<GeoPoint>();
            Direction = data.GetValue<GeoVector>();
        }
        /// <summary>
        /// Writes the location and the direction for the JSON serialization (<see cref="IJsonSerialize"/>). The struct is serialized inline as a
        /// plain list of values and read back by the constructor taking an <see cref="IJsonReadStruct"/>.
        /// </summary>
        /// <param name="data">receives the values</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(Location, Direction);
        }

        /// <summary>
        /// Does nothing. Value types serialized inline are restored by the constructor taking an <see cref="IJsonReadStruct"/>;
        /// this method only exists to satisfy <see cref="IJsonSerialize"/>.
        /// </summary>
        /// <param name="data">not used</param>
        public void SetObjectData(IJsonReadData data)
        {
        }

        /// <summary>
        /// Returns the part of this infinite axis inside the given cube as a <see cref="Line"/> object, e.g. to display the
        /// axis of a cylinder within the extent of a model. If the axis does not pass through the cube, the returned line is
        /// degenerate (both points at the origin).
        /// </summary>
        /// <param name="ext">the cube to clip with</param>
        /// <returns>the clipped axis as a line</returns>
        public Line Clip(BoundingCube ext)
        {
            Line res = Line.Construct();
            if (ext.ClipAxis(this, out GeoPoint startPoint, out GeoPoint endPoint))
            {
                res.SetTwoPoints(startPoint, endPoint);
            }
            return res;
        }
        #endregion
    }

    /// <summary>
    /// A 2-dimensional point with double x and y components.
    /// <alert class="caution">
    /// Keep in mind that this is a value type. Passing a value type as a (non ref) parameter and changing it's value
    /// inside the invoked method leaves the original unchanged.
    /// </alert>
    /// </summary>
    [Serializable()]
    [DebuggerDisplayAttribute("(x,y)={DebugString}")]
    [JsonVersion(serializeAsStruct = true, version = 1)]
    public struct GeoPoint2D : ISerializable, IJsonSerialize
    {
        /// <summary>
        /// x-component
        /// </summary>
        public double x;
        /// <summary>
        /// y-component
        /// </summary>
        public double y;
        /// <summary>
        /// Creates a new GeoPoint2D with the given components
        /// </summary>
        /// <param name="x">x-component</param>
        /// <param name="y">y-component</param>
        public GeoPoint2D(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
        /// <summary>
        /// Creates a new GeoPoint2D in the middle of the two given points
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        public GeoPoint2D(GeoPoint2D p1, GeoPoint2D p2)
        {
            x = (p1.x + p2.x) / 2.0;
            y = (p1.y + p2.y) / 2.0;
        }
        /// <summary>
        /// Creates a new GeoPoint2D at the specified offset to the provided GeoPoint2D <paramref name="p"/>
        /// </summary>
        /// <param name="p">Original point</param>
        /// <param name="x">Offset in x</param>
        /// <param name="y">Offset in y</param>
        public GeoPoint2D(GeoPoint2D p, double x, double y)
        {
            this.x = p.x + x;
            this.y = p.y + y;
        }
        /// <summary>
        /// Creates a new GeoPoint2D at the specified offset to the provided GeoPoint2D <paramref name="c"/>
        /// </summary>
        /// <param name="c">Original point</param>
        /// <param name="d">Distance to the original point</param>
        /// <param name="a">Angle to the original point</param>
        public GeoPoint2D(GeoPoint2D c, double d, Angle a)
        {
            x = c.x + d * Math.Cos(a.Radian);
            y = c.y + d * Math.Sin(a.Radian);
        }
        internal GeoPoint2D(GeoPoint p)
        {
            x = p.x;
            y = p.y;
        }
        internal GeoPoint2D(PointF p)
        {
            x = p.X;
            y = p.Y;
        }
        /// <summary>
        /// Constructs a ne GeoPoint at the geometric middle of the provided points
        /// </summary>
        /// <param name="p"></param>
        public GeoPoint2D(params GeoPoint2D[] p)
        {
            x = 0.0;
            y = 0.0;
            for (int i = 0; i < p.Length; ++i)
            {
                x += p[i].x;
                y += p[i].y;
            }
            x /= p.Length;
            y /= p.Length;
        }
        /// <summary>
        /// Constructs a new GeoPoint2D by linear interpolation between two points: <c>(1 - ratio) * p1 + ratio * p2</c>.
        /// A <paramref name="ratio"/> of 0.0 yields <paramref name="p1"/>, 1.0 yields <paramref name="p2"/>, values outside
        /// the range [0, 1] extrapolate beyond the two points.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        /// <param name="ratio">position between the two points</param>
        public GeoPoint2D(GeoPoint2D p1, GeoPoint2D p2, double ratio)
        {
            x = (1 - ratio) * p1.x + ratio * p2.x;
            y = (1 - ratio) * p1.y + ratio * p2.y;
        }
        /// <summary>
        /// Converts the point to an array of its components, e.g. to pass it to numerical routines.
        /// </summary>
        /// <param name="p">the point</param>
        /// <returns>the array {x, y}</returns>
        public static implicit operator double[](GeoPoint2D p) => new double[] { p.x, p.y };
        internal double TaxicabDistance(GeoPoint2D To)
        {	// Betragsnorm, heißt echt auf Englisch Taxicab oder Manhattan
            return Math.Abs(x - To.x) + Math.Abs(y - To.y);
        }
        /// <summary>
        /// Returns a 2d vector that points to this point from the origin
        /// </summary>
        /// <returns>The 2d vector</returns>
        public GeoVector2D ToVector()
        {
            return new GeoVector2D(x, y);
        }
        /// <summary>
        /// Returns true, if this point is left of the given line.
        /// </summary>
        /// <param name="from">Startpoint of the line</param>
        /// <param name="to">Endpoint of the line</param>
        /// <returns>true if left</returns>
        public bool IsLeftOf(GeoPoint2D from, GeoPoint2D to)
        {
            double dx = to.x - from.x;
            double dy = to.y - from.y;

            return (dy * x - dx * y + from.y * to.x - from.x * to.y) < 0.0;
        }
        /// <summary>
        /// The point as a <see cref="System.Drawing.PointF"/> with single precision components, for use with System.Drawing.
        /// Precision is lost in the conversion.
        /// </summary>
        public System.Drawing.PointF PointF
        {
            get { return new System.Drawing.PointF((float)x, (float)y); }
        }
        /// <summary>
        /// Returns the 2D point which is the offset of <paramref name="v"/> from <paramref name="p"/>.
        /// </summary>
        /// <param name="p">Original point</param>
        /// <param name="v">Offset</param>
        /// <returns>The offset point</returns>
        public static GeoPoint2D operator +(GeoPoint2D p, GeoVector2D v)
        {
            return new GeoPoint2D(p.x + v.x, p.y + v.y);
        }
        /// <summary>
        /// Subtracts a vector (offset) from a point.
        /// </summary>
        /// <param name="p">the point</param>
        /// <param name="v">the offset</param>
        /// <returns>the offset point</returns>
        public static GeoPoint2D operator -(GeoPoint2D p, GeoVector2D v)
        {
            return new GeoPoint2D(p.x - v.x, p.y - v.y);
        }
        /// <summary>
        /// Returns the vector that points from <paramref name="b"/> to <paramref name="a"/>.
        /// </summary>
        /// <param name="a">end point of the vector</param>
        /// <param name="b">start point of the vector</param>
        /// <returns>the vector from b to a</returns>
        public static GeoVector2D operator -(GeoPoint2D a, GeoPoint2D b)
        {
            return new GeoVector2D(a.x - b.x, a.y - b.y);
        }
        /// <summary>
        /// Returns the point in the middle of the two points, same as <c>new GeoPoint2D(a, b)</c>. An unusual use of the
        /// division operator, kept as a shorthand.
        /// </summary>
        /// <param name="a">first point</param>
        /// <param name="b">second point</param>
        /// <returns>the midpoint</returns>
        public static GeoPoint2D operator /(GeoPoint2D a, GeoPoint2D b)
        {
            return new GeoPoint2D(a, b);
        }
        /// <summary>
        /// Determines whether two points are exactly equal. Use <see cref="Precision.IsEqual(GeoPoint2D, GeoPoint2D)"/>
        /// if you need more control over precision.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        /// <returns>true if equal</returns>
        public static bool operator ==(GeoPoint2D p1, GeoPoint2D p2)
        {
            return p1.x == p2.x && p1.y == p2.y;
        }
        /// <summary>
        /// Determines whether two points are not exactly equal. Use <see cref="Precision.IsEqual(GeoPoint2D, GeoPoint2D)"/>
        /// if you need more control over precision.
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        /// <returns>true if not equal</returns>
        public static bool operator !=(GeoPoint2D p1, GeoPoint2D p2)
        {
            return p1.x != p2.x || p1.y != p2.y;
        }
        /// <summary>
        /// Returns the distance of the two points
        /// </summary>
        /// <param name="p1">first point</param>
        /// <param name="p2">second point</param>
        /// <returns>the distance</returns>
        public static double operator |(GeoPoint2D p1, GeoPoint2D p2)
        {
            return Geometry.Dist(p1, p2);
        }
        /// <summary>
        /// Square distance. Not a very nice operator symbol, but cannot overload ||, which I would prefer
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public static double operator &(GeoPoint2D p1, GeoPoint2D p2)
        {
            return (p1.x - p2.x) * (p1.x - p2.x) + (p1.y - p2.y) * (p1.y - p2.y);
        }
        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (!(o is GeoPoint2D)) return false;
            GeoPoint2D second = (GeoPoint2D)o;
            return x == second.x && y == second.y;
        }
        /// <inheritdoc/>
        public override string ToString()
        {
            return "(" + x.ToString() + ", " + y.ToString() + ")";
        }
        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return x.GetHashCode() | y.GetHashCode();
        }
        /// <summary>
        /// The origin (0.0, 0.0). This is a static field rather than a property for speed; do not assign to it.
        /// </summary>
        public static GeoPoint2D Origin = new GeoPoint2D(0.0, 0.0);
        /// <summary>
        /// A point marked as invalid, both components are NaN. Returned by methods that could not compute a point; test
        /// with <see cref="IsValid"/>, because comparisons with NaN are always false. This is a static field rather than a
        /// property for speed; do not assign to it.
        /// </summary>
        public static GeoPoint2D Invalid = new GeoPoint2D(double.NaN, double.NaN); // changed from maxvalue to nan, hopefully with no side effects!
        /// <summary>
        /// True, when neither component is NaN, false for <see cref="Invalid"/> and for points that could not be computed.
        /// Infinite components are not detected, unlike <see cref="GeoPoint.IsValid"/>.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return !double.IsNaN(x) && !double.IsNaN(y);
            }
        }
        /// <summary>
        /// True, when at least one component is NaN; the negation of <see cref="IsValid"/>.
        /// </summary>
        public bool IsNan
        {
            get
            {
                return double.IsNaN(x) || double.IsNaN(y);
            }
        }
        internal string DebugString
        {
            get
            {
                return x.ToString(DebugFormat.Coordinate) + "; " + y.ToString(DebugFormat.Coordinate);
            }
        }

        private class GeoPoint2DCompareX : IComparer
        {
            public GeoPoint2DCompareX() { }
            #region IComparer Members

            public int Compare(object x, object y)
            {
                GeoPoint2D p1 = (GeoPoint2D)x;
                GeoPoint2D p2 = (GeoPoint2D)y;
                if (p1.x < p2.x) return -1;
                if (p1.x > p2.x) return 1;
                return 0;
            }

            #endregion
        }
        /// <summary>
        /// A non-generic <see cref="IComparer"/> that orders points by their x-component only, for use with
        /// <see cref="Array.Sort(Array, IComparer)"/>. <see cref="GeoPoint2DICompareX"/> is the generic counterpart.
        /// </summary>
        public static IComparer CompareX = new GeoPoint2DCompareX();
        /// <summary>
        /// Comparer that orders <see cref="GeoPoint2D"/> objects by their x-component only; points with equal x-components
        /// are considered equal regardless of y. Use with <see cref="List{T}.Sort(IComparer{T})"/>. <see cref="CompareX"/>
        /// is the non-generic counterpart.
        /// </summary>
        public class GeoPoint2DICompareX : IComparer<GeoPoint2D>
        {
            /// <summary>
            /// Creates the comparer. It has no state.
            /// </summary>
            public GeoPoint2DICompareX() { }

            #region IComparer<GeoPoint2D> Members

            /// <summary>
            /// Compares the x-components of the two points.
            /// </summary>
            /// <param name="x">first point</param>
            /// <param name="y">second point</param>
            /// <returns>-1 if the x-component of <paramref name="x"/> is smaller, 1 if it is greater, 0 if both are equal</returns>
            public int Compare(GeoPoint2D x, GeoPoint2D y)
            {
                GeoPoint2D p1 = (GeoPoint2D)x;
                GeoPoint2D p2 = (GeoPoint2D)y;
                if (p1.x < p2.x) return -1;
                if (p1.x > p2.x) return 1;
                return 0;
            }

            #endregion
        }
        /// <summary>
        /// Comparer that orders <see cref="GeoPoint2D"/> objects by their distance to a fixed reference point, the closest
        /// first. Used to sort e.g. intersection points by their distance to a given position:
        /// <c>points.Sort(new GeoPoint2D.CompareCloseTo(position))</c>.
        /// </summary>
        public class CompareCloseTo : IComparer<GeoPoint2D>
        {   // sortiert die Punkte im Abstand zu "ToHere"
            private GeoPoint2D ToHere;
            /// <summary>
            /// Creates a comparer for the distance to the given point.
            /// </summary>
            /// <param name="ToHere">the reference point the distances are measured to</param>
            public CompareCloseTo(GeoPoint2D ToHere)
            {
                this.ToHere = ToHere;
            }
            #region IComparer<GeoPoint2D> Members

            int IComparer<GeoPoint2D>.Compare(GeoPoint2D x, GeoPoint2D y)
            {
                double d1 = Geometry.Dist(x, ToHere);
                double d2 = Geometry.Dist(y, ToHere);
                return d1.CompareTo(d2);
            }

            #endregion
        }

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        GeoPoint2D(SerializationInfo info, StreamingContext context)
        {
            x = (double)info.GetValue("X", typeof(double));
            y = (double)info.GetValue("Y", typeof(double));
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("X", x);
            info.AddValue("Y", y);
        }
        #endregion

        /// <summary>
        /// Returns the geometric center (arithmetic mean) of the given points. The list must not be empty.
        /// </summary>
        /// <param name="p">the points</param>
        /// <returns>the center of the points</returns>
        public static GeoPoint2D Center(params GeoPoint2D[] p)
        {
            GeoPoint2D res = p[0];
            for (int i = 1; i < p.Length; i++)
            {
                res.x += p[i].x;
                res.y += p[i].y;
            }
            res.x /= p.Length;
            res.y /= p.Length;
            return res;
        }
        /// <summary>
        /// Returns the signed area of the closed polygon through the given points (the last point is connected to the
        /// first). The area is positive when the polygon runs counterclockwise and negative when it runs clockwise, so the
        /// sign also tells the orientation of an outline. Returns 0.0 for an empty array.
        /// </summary>
        /// <param name="polygon">the vertices of the polygon in order</param>
        /// <returns>the signed area</returns>
        public static double Area(GeoPoint2D[] polygon)
        {
            double sum = 0.0;
            if (polygon.Length > 0)
            {
                for (int i = 0; i < polygon.Length - 1; ++i)
                {
                    sum += (polygon[i].x - polygon[i + 1].x) * (polygon[i].y + polygon[i + 1].y);
                }
                sum += (polygon[polygon.Length - 1].x - polygon[0].x) * (polygon[polygon.Length - 1].y + polygon[0].y);
            }
            return sum / 2.0;
        }

        internal static bool InnerIntersection(GeoPoint2D[] polygon)
        {
            for (int i = 0; i < polygon.Length - 3; ++i)
            {
                for (int j = i + 2; j < polygon.Length - 1; ++j)
                {
                    if (Geometry.SegmentIntersection(polygon[i], polygon[i + 1], polygon[j], polygon[j + 1])) return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Constructor for the JSON deserialization (<see cref="IJsonSerialize"/>): reads the two components in the order
        /// <see cref="GetObjectData(IJsonWriteData)"/> wrote them. It is found by reflection, there is no need to call it directly.
        /// </summary>
        /// <param name="data">supplies the values</param>
        public GeoPoint2D(IJsonReadStruct data)
        {
            x = data.GetValue<double>();
            y = data.GetValue<double>();
        }
        /// <summary>
        /// Writes the two components for the JSON serialization (<see cref="IJsonSerialize"/>). The struct is serialized inline as a
        /// plain list of values and read back by the constructor taking an <see cref="IJsonReadStruct"/>.
        /// </summary>
        /// <param name="data">receives the values</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(x, y);
        }

        /// <summary>
        /// Does nothing. Value types serialized inline are restored by the constructor taking an <see cref="IJsonReadStruct"/>;
        /// this method only exists to satisfy <see cref="IJsonSerialize"/>.
        /// </summary>
        /// <param name="data">not used</param>
        public void SetObjectData(IJsonReadData data)
        {
        }

    }

    /// <summary>
    /// A two dimensional vector with double x and y components.
    /// <alert class="caution">
    /// Keep in mind that this is a value type. Passing a value type as a (non ref) parameter and changing it's value
    /// inside the invoked method leaves the original unchanged.
    /// </alert>
    /// </summary>
    [Serializable()]
    [JsonVersion(serializeAsStruct = true, version = 1)]
    [DebuggerDisplayAttribute("(x,y)={DebugString}")]
    public struct GeoVector2D : ISerializable, IJsonSerialize
    {
        /// <summary>
        /// x-component
        /// </summary>
        public double x;
        /// <summary>
        /// y-component
        /// </summary>
        public double y;
        /// <summary>
        /// Constructs a new GeoVector2D with the given components.
        /// </summary>
        /// <param name="x">x-component</param>
        /// <param name="y">y-component</param>
        public GeoVector2D(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
        /// <summary>
        /// Constructs the unit vector pointing in the given direction. The angle is measured counterclockwise from the
        /// x-axis, so the angle 0 yields <see cref="XAxis"/>.
        /// </summary>
        /// <param name="dir">direction of the vector</param>
        public GeoVector2D(Angle dir)
        {
            x = Math.Cos(dir);
            y = Math.Sin(dir);
        }
        /// <summary>
        /// Constructs a new GeoVector2D from the x- and y-components of a 3-dimensional vector, i.e. its projection onto
        /// the x/y plane. The z-component is dropped.
        /// </summary>
        /// <param name="v3">the 3-dimensional vector</param>
        public GeoVector2D(GeoVector v3)
        {
            this.x = v3.x;
            this.y = v3.y;
        }
        /// <summary>
        /// Converts the vector to an array of its components, e.g. to pass it to numerical routines.
        /// </summary>
        /// <param name="p">the vector</param>
        /// <returns>the array {x, y}</returns>
        public static implicit operator double[](GeoVector2D p) => new double[] { p.x, p.y };
        /// <summary>
        /// Returns true, when both components are ordinary numbers. A vector that failed to be computed
        /// carries NaN or infinity, and every comparison against such a value is false, which lets a
        /// broken value travel through an iteration unnoticed instead of stopping it.
        /// </summary>
        public bool IsValid
        {
            get
            {
                return !double.IsNaN(x) && !double.IsNaN(y) && !double.IsInfinity(x) && !double.IsInfinity(y);
            }
        }
        /// <summary>
        /// Normalizes this vector. After this operation the vector will have the <see cref="Length"/> 1.0. Throws a
        /// <see cref="GeoVectorException"/> if the vector is the nullvector.
        /// </summary>
        public void Norm()
        {
            if (Length <= 0.0) throw new GeoVectorException(GeoVectorException.tExceptionType.NullVector);
            double f = 1 / Length;
            x *= f;
            y *= f;
        }
        /// <summary>
        /// A copy of this vector scaled to <see cref="Length"/> 1.0; this vector itself remains unchanged (see <see cref="Norm"/>
        /// to normalize in place). Throws a <see cref="GeoVectorException"/> if this vector is the nullvector.
        /// </summary>
        public GeoVector2D Normalized
        {
            get
            {
                if (Length <= 0.0) throw new GeoVectorException(GeoVectorException.tExceptionType.NullVector);
                double f = 1 / Length;
                return new GeoVector2D(x * f, y * f);
            }
        }
        /// <summary>
        /// The length of this vector. Setting it scales the vector to the new length, keeping its direction; setting the
        /// length of the nullvector yields NaN components.
        /// </summary>
        public double Length
        {
            get
            {
                return Math.Sqrt(Geometry.sqr(x) + Geometry.sqr(y));
            }
            set
            {
                double l = Math.Sqrt(Geometry.sqr(x) + Geometry.sqr(y));
                double f = value / l;
                x *= f;
                y *= f;
            }
        }
        /// <summary>
        /// The taxicab (Manhattan) length of this vector, i.e. the sum of the absolute values of the components. A cheap
        /// estimate of the size of the vector for tolerance checks, not the euclidean <see cref="Length"/>.
        /// </summary>
        public double TaxicabLength
        {
            get
            {
                return Math.Abs(x) + Math.Abs(y);
            }
        }
        /// <summary>
        /// The direction of this vector as an angle, measured counterclockwise from the x-axis in the range [0, 2*pi)
        /// (0 for the nullvector). Setting it rotates the vector to that direction, keeping its length.
        /// </summary>
        public Angle Angle
        {
            get
            {
                return new Angle(x, y);
            }
            set
            {
                double l = Length;
                x = l * Math.Cos(value);
                y = l * Math.Sin(value);
            }
        }
        /// <summary>
        /// Simple test, whether the vector is more horizontal than vertical
        /// </summary>
        public bool IsMoreHorizontal
        {
            get
            {
                return Math.Abs(x) > Math.Abs(y);
            }
        }
        /// <summary>
        /// Adds the two vectors.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>sum</returns>
        public static GeoVector2D operator +(GeoVector2D v1, GeoVector2D v2)
        {
            return new GeoVector2D(v1.x + v2.x, v1.y + v2.y);
        }
        /// <summary>
        /// Subtracts the second vector from the first vector.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>difference</returns>
        public static GeoVector2D operator -(GeoVector2D v1, GeoVector2D v2)
        {
            return new GeoVector2D(v1.x - v2.x, v1.y - v2.y);
        }
        /// <summary>
        /// Reverses the direction of the given vector.
        /// </summary>
        /// <param name="v1">vector to reverse</param>
        /// <returns>reversed vector</returns>
        public static GeoVector2D operator -(GeoVector2D v1)
        {
            return new GeoVector2D(-v1.x, -v1.y);
        }
        /// <summary>
        /// Calculates the scalar product (dot product, inner product) of the two given vectors.
        /// </summary>
        /// <param name="v1">first vector</param>
        /// <param name="v2">second vector</param>
        /// <returns>the scalar product</returns>
        public static double operator *(GeoVector2D v1, GeoVector2D v2)
        {	// das Skalarprodukt
            return v1.x * v2.x + v1.y * v2.y;
        }
        /// <summary>
        /// Scales the given vector by the given factor.
        /// </summary>
        /// <param name="d">factor</param>
        /// <param name="v">vector</param>
        /// <returns>scaled vector</returns>
        public static GeoVector2D operator *(double d, GeoVector2D v)
        {
            return new GeoVector2D(d * v.x, d * v.y);
        }
        /// <summary>
        /// Returns the cosine of the angle between the two vectors. Throws an arithmetic exception if any of the vectors
        /// is the nullvector.
        /// </summary>
        /// <param name="v1">First vector</param>
        /// <param name="v2">Second vector</param>
        /// <returns>Cosine of the inner angle</returns>
        public static double Cos(GeoVector2D v1, GeoVector2D v2)
        {
            return (v1 * v2) / (v1.Length * v2.Length);
        }
        /// <summary>
        /// Returns this vector rotated by 90 degrees counterclockwise, i.e. the perpendicular vector pointing to the left of
        /// this one. The length is unchanged.
        /// </summary>
        /// <returns>the vector rotated to the left</returns>
        public GeoVector2D ToLeft()
        {
            return new GeoVector2D(-y, x);
        }
        /// <summary>
        /// Returns this vector rotated by 90 degrees clockwise, i.e. the perpendicular vector pointing to the right of this
        /// one. The length is unchanged.
        /// </summary>
        /// <returns>the vector rotated to the right</returns>
        public GeoVector2D ToRight()
        {
            return new GeoVector2D(y, -x);
        }
        /// <summary>
        /// Returns the reversed vector, same as the unary minus operator.
        /// </summary>
        /// <returns>the reversed vector</returns>
        public GeoVector2D Opposite()
        {
            return new GeoVector2D(-x, -y);
        }
        /// <summary>
        /// Returns true, if this vector is exactly the nullvector. Use <see cref="Precision.IsNullVector(GeoVector2D)"/>
        /// if you need more control over precision.
        /// </summary>
        /// <returns>true if nullvector</returns>
        public bool IsNullVector()
        {
            return (x == 0.0 && y == 0.0);
        }
        /// <summary>
        /// The x-axis, same as new GeoVector2D(1.0, 0.0).
        /// </summary>
        public static GeoVector2D XAxis
        {
            get { return new GeoVector2D(1.0, 0.0); }
        }
        /// <summary>
        /// The y-axis, same as new GeoVector2D(0.0, 1.0).
        /// </summary>
        public static GeoVector2D YAxis
        {
            get { return new GeoVector2D(0.0, 1.0); }
        }
        /// <summary>
        /// Returns the unit vector pointing in the given direction. The angle is given in radians, measured
        /// counterclockwise from the x-axis.
        /// </summary>
        /// <param name="angle">direction in radians</param>
        /// <returns>the unit vector in that direction</returns>
        public static GeoVector2D FromAngle(double angle)
        {
            return new GeoVector2D(Math.Cos(angle), Math.Sin(angle));
        }
        /// <summary>
        /// The nullvector, same as new GeoVector2D(0.0, 0.0).
        /// </summary>
        public static GeoVector2D NullVector
        {
            get { return new GeoVector2D(0.0, 0.0); }
        }
        /// <summary>
        /// Returns the area of the parallelogram defined by the two vectors. The result will be positive
        /// if the sweep-direction from <paramref name="from"/> to <paramref name="to"/> is counterclockwise
        /// otherwise it will be negative. If you need the angle of the triangle divide the result by 2.0
        /// </summary>
        /// <param name="from">First vector</param>
        /// <param name="to">Second vector</param>
        /// <returns>the area</returns>
        public static double Area(GeoVector2D from, GeoVector2D to)
        {
            return from.x * to.y - from.y * to.x;
        }
        /// <summary>
        /// Returns a positive value if the second vector turns to the left relative to the direction of the first vector,
        /// a negative value if it turns to the right and 0.0 if the vectors are parallel. The value is the length of the crossproduct
        /// of the two vectors in 3D or the area of the parallelogram build by the two vectors.
        /// </summary>
        /// <param name="v1">First vector</param>
        /// <param name="v2">Second vector</param>
        /// <returns>The orientation</returns>
        public static double Orientation(GeoVector2D v1, GeoVector2D v2)
        {
            return v1.x * v2.y - v1.y * v2.x;
        }
        //		public static implicit operator CndHlp2D.GeoVector2D(GeoVector2D v)
        //		{
        //			return new CndHlp2D.GeoVector2D(v.x,v.y);
        //		}
        internal string DebugString
        {
            get
            {
                return x.ToString(DebugFormat.Coordinate) + "; " + y.ToString(DebugFormat.Coordinate);
            }
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        GeoVector2D(SerializationInfo info, StreamingContext context)
        {
            x = (double)info.GetValue("X", typeof(double));
            y = (double)info.GetValue("Y", typeof(double));
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("X", x);
            info.AddValue("Y", y);
        }

        /// <summary>
        /// Constructor for the JSON deserialization (<see cref="IJsonSerialize"/>): reads the two components in the order
        /// <see cref="GetObjectData(IJsonWriteData)"/> wrote them. It is found by reflection, there is no need to call it directly.
        /// </summary>
        /// <param name="data">supplies the values</param>
        public GeoVector2D(IJsonReadStruct data)
        {
            x = data.GetValue<double>();
            y = data.GetValue<double>();
        }
        /// <summary>
        /// Writes the two components for the JSON serialization (<see cref="IJsonSerialize"/>). The struct is serialized inline as a
        /// plain list of values and read back by the constructor taking an <see cref="IJsonReadStruct"/>.
        /// </summary>
        /// <param name="data">receives the values</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(x, y);
        }

        /// <summary>
        /// Does nothing. Value types serialized inline are restored by the constructor taking an <see cref="IJsonReadStruct"/>;
        /// this method only exists to satisfy <see cref="IJsonSerialize"/>.
        /// </summary>
        /// <param name="data">not used</param>
        public void SetObjectData(IJsonReadData data)
        {
        }
        #endregion
    }

}
