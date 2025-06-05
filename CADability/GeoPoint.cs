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
        public bool IsValid
        {
            get
            {
                return !double.IsNaN(x) && !double.IsNaN(y) && !double.IsNaN(z) && !double.IsInfinity(x) && !double.IsInfinity(y) && !double.IsInfinity(z);
            }
        }
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
        public GeoPoint2D Project(Plane plane, GeoPoint center)
        {
            return plane.Project(plane.Intersect(this, this - center));
        }

        /// <summary>
        /// Determins whether two GeoPoints are exactly equal. Use <see cref="Precision.IsEqual"/>
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
        /// Determins whether two GeoPoints are not equal. Use <see cref="Precision.IsEqual"/>
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
        public static double operator &(GeoPoint p1, GeoPoint p2)
        {
            return (p1.x - p2.x) * (p1.x - p2.x) + (p1.y - p2.y) * (p1.y - p2.y) + (p1.z - p2.z) * (p1.z - p2.z);
        }
        public static GeoPoint operator *(Matrix m, GeoPoint p)
        {
            System.Diagnostics.Debug.Assert(m.ColumnCount == 3 && m.RowCount == 3);
            return new GeoPoint(m[0, 0] * p.x + m[0, 1] * p.y + m[0, 2] * p.z, m[1, 0] * p.x + m[1, 1] * p.y + m[1, 2] * p.z, m[2, 0] * p.x + m[2, 1] * p.y + m[2, 2] * p.z);
        }

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

        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(x, y, z);
        }

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
        public GeoVector(GeoVector v1, GeoVector v2, double ratio)
        {
            x = (1 - ratio) * v1.x + ratio * v2.x;
            y = (1 - ratio) * v1.y + ratio * v2.y;
            z = (1 - ratio) * v1.z + ratio * v2.z;
        }
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
        /// Returns true, if this vector is exactly the nullvector. Use <see cref="Precision.IsNullVector"/>
        /// if you need more control over precision.
        /// </summary>
        /// <returns>true if nullvector</returns>
        public bool IsNullVector()
        {
            return (x == 0.0 && y == 0.0 && z == 0.0);
        }
        public bool IsValid()
        {
            return !double.IsNaN(x);
        }
        /// <summary>
        /// Determines whether this vector and the othe vector are perpendicular. Use <see cref="Precision.IsPerpendicular"/>
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
        public static implicit operator double[](GeoVector v) => new double[] { v.x, v.y, v.z };
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
        /// Determins whether the given vectors are exactly equal. Use <see cref="Precision.SameDirection"/>
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
        /// Determins whether the given vectors are not exactly equal. Use <see cref="Precision.SameDirection"/>
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
        /// Gut zum Iterieren über alle Achsen
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
        public double LengthSqared
        {
            get { return x * x + y * y + z * z; }
        }
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
        public GeoVector Normalized
        {
            get
            {
                if (Length <= 0.0) throw new GeoVectorException(GeoVectorException.tExceptionType.NullVector);
                double f = 1 / Length;
                return new GeoVector(x * f, y * f, z * f);
            }
        }

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
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("X", x, typeof(double));
            info.AddValue("Y", y, typeof(double));
            info.AddValue("Z", z, typeof(double));
        }

        #endregion
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

        public GeoVector(IJsonReadStruct data)
        {
            x = data.GetValue<double>();
            y = data.GetValue<double>();
            z = data.GetValue<double>();
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(x, y, z);
        }

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
        static public Axis InvalidAxis
        {
            get
            {
                return new Axis(GeoPoint.Invalid, GeoVector.NullVector);
            }
        }
        public bool IsValid
        {
            get { return Location.IsValid; }
        }
        public bool SameGeometry(Axis other)
        {
            if (Precision.SameDirection(Direction,other.Direction,false))
            {
                if (Geometry.DistPL(other.Location, this) < Precision.eps) return true;
            }
            return false;
        }
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
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Location", Location, typeof(GeoPoint));
            info.AddValue("Direction", Direction, typeof(GeoVector));
        }
        public Axis(IJsonReadStruct data)
        {
            Location = data.GetValue<GeoPoint>();
            Direction = data.GetValue<GeoVector>();
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(Location, Direction);
        }

        public void SetObjectData(IJsonReadData data)
        {
        }

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
        public GeoPoint2D(GeoPoint2D p1, GeoPoint2D p2, double ratio)
        {
            x = (1 - ratio) * p1.x + ratio * p2.x;
            y = (1 - ratio) * p1.y + ratio * p2.y;
        }
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
        public static GeoPoint2D operator -(GeoPoint2D p, GeoVector2D v)
        {
            return new GeoPoint2D(p.x - v.x, p.y - v.y);
        }
        public static GeoVector2D operator -(GeoPoint2D a, GeoPoint2D b)
        {
            return new GeoVector2D(a.x - b.x, a.y - b.y);
        }
        public static GeoPoint2D operator /(GeoPoint2D a, GeoPoint2D b)
        {
            return new GeoPoint2D(a, b);
        }
        public static bool operator ==(GeoPoint2D p1, GeoPoint2D p2)
        {
            return p1.x == p2.x && p1.y == p2.y;
        }
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
        public override bool Equals(object o)
        {
            if (!(o is GeoPoint2D)) return false;
            GeoPoint2D second = (GeoPoint2D)o;
            return x == second.x && y == second.y;
        }
        public override string ToString()
        {
            return "(" + x.ToString() + ", " + y.ToString() + ")";
        }
        public override int GetHashCode()
        {
            return x.GetHashCode() | y.GetHashCode();
        }
        public static GeoPoint2D Origin = new GeoPoint2D(0.0, 0.0);
        public static GeoPoint2D Invalid = new GeoPoint2D(double.NaN, double.NaN); // changed from maxvalue to nan, hopefully with no side effects!
        public bool IsValid
        {
            get
            {
                return !double.IsNaN(x) && !double.IsNaN(y);
            }
        }
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
        public static IComparer CompareX = new GeoPoint2DCompareX();
        public class GeoPoint2DICompareX : IComparer<GeoPoint2D>
        {
            public GeoPoint2DICompareX() { }

            #region IComparer<GeoPoint2D> Members

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
        public class CompareCloseTo : IComparer<GeoPoint2D>
        {   // sortiert die Punkte im Abstand zu "ToHere"
            private GeoPoint2D ToHere;
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
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("X", x);
            info.AddValue("Y", y);
        }
        #endregion

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
        public GeoPoint2D(IJsonReadStruct data)
        {
            x = data.GetValue<double>();
            y = data.GetValue<double>();
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(x, y);
        }

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
        public double x;
        public double y;
        public GeoVector2D(double x, double y)
        {
            this.x = x;
            this.y = y;
        }
        public GeoVector2D(Angle dir)
        {
            x = Math.Cos(dir);
            y = Math.Sin(dir);
        }
        public GeoVector2D(GeoVector v3)
        {
            this.x = v3.x;
            this.y = v3.y;
        }
        public static implicit operator double[](GeoVector2D p) => new double[] { p.x, p.y };
        public void Norm()
        {
            if (Length <= 0.0) throw new GeoVectorException(GeoVectorException.tExceptionType.NullVector);
            double f = 1 / Length;
            x *= f;
            y *= f;
        }
        public GeoVector2D Normalized
        {
            get
            {
                if (Length <= 0.0) throw new GeoVectorException(GeoVectorException.tExceptionType.NullVector);
                double f = 1 / Length;
                return new GeoVector2D(x * f, y * f);
            }
        }
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
        public double TaxicabLength
        {
            get
            {
                return Math.Abs(x) + Math.Abs(y);
            }
        }
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
        public static GeoVector2D operator +(GeoVector2D v1, GeoVector2D v2)
        {
            return new GeoVector2D(v1.x + v2.x, v1.y + v2.y);
        }
        public static GeoVector2D operator -(GeoVector2D v1, GeoVector2D v2)
        {
            return new GeoVector2D(v1.x - v2.x, v1.y - v2.y);
        }
        public static GeoVector2D operator -(GeoVector2D v1)
        {
            return new GeoVector2D(-v1.x, -v1.y);
        }
        public static double operator *(GeoVector2D v1, GeoVector2D v2)
        {	// das Skalarprodukt
            return v1.x * v2.x + v1.y * v2.y;
        }
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
        public GeoVector2D ToLeft()
        {
            return new GeoVector2D(-y, x);
        }
        public GeoVector2D ToRight()
        {
            return new GeoVector2D(y, -x);
        }
        public GeoVector2D Opposite()
        {
            return new GeoVector2D(-x, -y);
        }
        /// <summary>
        /// Returns true, if this vector is exactly the nullvector. Use <see cref="Precision.IsNullVector"/>
        /// if you need more control over precision.
        /// </summary>
        /// <returns>true if nullvector</returns>
        public bool IsNullVector()
        {
            return (x == 0.0 && y == 0.0);
        }
        public static GeoVector2D XAxis
        {
            get { return new GeoVector2D(1.0, 0.0); }
        }
        public static GeoVector2D YAxis
        {
            get { return new GeoVector2D(0.0, 1.0); }
        }
        public static GeoVector2D FromAngle(double angle)
        {
            return new GeoVector2D(Math.Cos(angle), Math.Sin(angle));
        }
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
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("X", x);
            info.AddValue("Y", y);
        }

        public GeoVector2D(IJsonReadStruct data)
        {
            x = data.GetValue<double>();
            y = data.GetValue<double>();
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(x, y);
        }

        public void SetObjectData(IJsonReadData data)
        {
        }
        #endregion
    }

}
