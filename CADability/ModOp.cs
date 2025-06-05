using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using System;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability
{
    /// <summary>
    /// Exception class for modification operations
    /// </summary>

    public class ModOpException : System.ApplicationException
    {
        /// <summary>
        /// ExceptionType
        /// </summary>
        public enum tExceptionType
        {
            /// <summary>
            /// Inversion of a <see cref="ModOp"/> or <see cref="ModOp2D"/> failed
            /// </summary>
            InversionFailed,
            /// <summary>
            /// Invalid parameter specified
            /// </summary>
            InvalidParameter
        };
        /// <summary>
        /// Type of exception
        /// </summary>
        public tExceptionType ExceptionType;
        internal ModOpException(tExceptionType ExceptionType)
        {
            this.ExceptionType = ExceptionType;
        }
        internal ModOpException(string msg, tExceptionType ExceptionType)
            : base(msg)
        {
            this.ExceptionType = ExceptionType;
        }
    }

    /// <summary>
    /// Ein "Objekt-Wrapper" um die ModOp. Manchmal ist es sinnvoll in Klassen eine
    /// ModOp zu halten, die ggf. noch nicht berechnet, also null ist. Hierzu dient diese
    /// interne Klasse.
    /// </summary>
    internal class ModOpRef
    {
        public ModOp ModOp;
        public ModOpRef(ModOp m)
        {
            this.ModOp = m;
        }
    }

    /// <summary>
    /// A 2-dimensional modification operation implemented as a homogenous matix 3*2. You can apply
    /// such modificaion to <see cref="GeoVector2D"/> and <see cref="GeoPoint2D"/> points or to <see cref="CADability.Curve2D.ICurve2D"/>
    /// implementing objects.
    /// </summary>
    [
        Serializable()]
    public struct ModOp2D : ISerializable
    {
        private double Matrix00, Matrix01, Matrix02, Matrix10, Matrix11, Matrix12;
        /// <summary>
        /// Gets or sets the 3*2 Matrix that defines this mmodification
        /// </summary>
        public double[,] Matrix
        {
            get
            {
                double[,] res = new double[2, 3];
                res[0, 0] = Matrix00;
                res[0, 1] = Matrix01;
                res[0, 2] = Matrix02;
                res[1, 0] = Matrix10;
                res[1, 1] = Matrix11;
                res[1, 2] = Matrix12;
                return res;
            }
            set
            {
                Matrix00 = value[0, 0];
                Matrix01 = value[0, 1];
                Matrix02 = value[0, 2];
                Matrix10 = value[1, 0];
                Matrix11 = value[1, 1];
                Matrix12 = value[1, 2];
            }
        }
        /// <summary>
        /// Creates a modification according to the given coefficients 
        /// </summary>
        /// <param name="Matrix00"></param>
        /// <param name="Matrix01"></param>
        /// <param name="Matrix02"></param>
        /// <param name="Matrix10"></param>
        /// <param name="Matrix11"></param>
        /// <param name="Matrix12"></param>
        public ModOp2D(double Matrix00, double Matrix01, double Matrix02, double Matrix10, double Matrix11, double Matrix12)
        {
            this.Matrix00 = Matrix00;
            this.Matrix01 = Matrix01;
            this.Matrix02 = Matrix02;
            this.Matrix10 = Matrix10;
            this.Matrix11 = Matrix11;
            this.Matrix12 = Matrix12;
        }
        /// <summary>
        /// A modification, that transforms the x-axis to v1, the y-axis to v2 and the origin to loc
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        /// <param name="loc"></param>
        internal ModOp2D(GeoVector2D v1, GeoVector2D v2, GeoPoint2D loc)
        {
            this.Matrix00 = v1.x;
            this.Matrix01 = v2.x;
            this.Matrix02 = loc.x;
            this.Matrix10 = v1.y;
            this.Matrix11 = v2.y;
            this.Matrix12 = loc.y;
        }
        public ModOp2D(double[,] m)
        {
            if (m.GetLength(0) == 2 && m.GetLength(1) == 2)
            {
                Matrix00 = m[0, 0];
                Matrix01 = m[0, 1];
                Matrix02 = 0.0;
                Matrix10 = m[1, 0];
                Matrix11 = m[1, 1];
                Matrix12 = 0.0;
            }
            else if (m.GetLength(0) >= 2 && m.GetLength(1) >= 3)
            {
                Matrix00 = m[0, 0];
                Matrix01 = m[0, 1];
                Matrix02 = m[0, 2];
                Matrix10 = m[1, 0];
                Matrix11 = m[1, 1];
                Matrix12 = m[1, 2];
            }
            else
            {
                Matrix00 = Matrix11 = 0.0;
                Matrix01 = Matrix02 = 0.0;
                Matrix10 = Matrix12 = 0.0;
            }
        }
        /// <summary>
        /// Returns the inverse of this modification. 
        /// </summary>
        /// <returns>inverse</returns>
        public ModOp2D GetInverse()
        {

            // nach http://mathworld.wolfram.com/MatrixInverse.html
            double d = Determinant;
            if (Math.Abs(d) < 1e-16) throw new ModOpException(ModOpException.tExceptionType.InversionFailed);
            ModOp2D res = new ModOp2D(Matrix11 / d, -Matrix01 / d, 0.0, -Matrix10 / d, Matrix00 / d, 0.0);
            res.Matrix02 = -(res.Matrix00 * Matrix02 + res.Matrix01 * Matrix12);
            res.Matrix12 = -(res.Matrix10 * Matrix02 + res.Matrix11 * Matrix12);
            return res;
        }
        /// <summary>
        /// Gets the determinant of this modification
        /// </summary>
        public double Determinant
        {
            get
            {
                return Matrix00 * Matrix11 - Matrix01 * Matrix10;
            }
        }
        /// <summary>
        /// Returns the scaling factor of this modification
        /// </summary>
        public double Factor
        {
            get
            {
                return Math.Sqrt(Math.Abs(Matrix00 * Matrix11 - Matrix01 * Matrix10));
            }
        }
        /// <summary>
        /// Returns true, if this modification is the identity.
        /// </summary>
        public bool IsIdentity
        {
            get
            {
                return
                    Matrix00 == 1.0 &&
                    Matrix11 == 1.0 &&
                    Matrix02 == 0.0 &&
                    Matrix01 == 0.0 &&
                    Matrix12 == 0.0 &&
                    Matrix10 == 0.0;
            }
        }
        public bool IsAlmostIdentity(double precision)
        {
            if ((GeoPoint2D.Origin | this * GeoPoint2D.Origin) > precision) return false;
            if ((new GeoPoint2D(1, 0) | this * new GeoPoint2D(1, 0)) > precision) return false;
            if ((new GeoPoint2D(0, 1) | this * new GeoPoint2D(0, 1)) > precision) return false;
            return true;
        }
        public bool IsIsogonal
        {
            get
            {
                if (!Precision.IsPerpendicular(new GeoVector2D(Matrix00, Matrix10), new GeoVector2D(Matrix01, Matrix11), false)) return false;
                if (!Precision.IsPerpendicular(new GeoVector2D(Matrix00 + Matrix10, Matrix01 + Matrix11), new GeoVector2D(Matrix00 - Matrix10, -Matrix01 + Matrix11), false)) return false;
                return true;
            }
        }
        /// <summary>
        /// Creates a modification which is the identity
        /// </summary>
        public static ModOp2D Identity
        {
            get
            {
                ModOp2D res;
                res.Matrix00 = res.Matrix11 = 1.0;
                res.Matrix01 = res.Matrix02 = 0.0;
                res.Matrix10 = res.Matrix12 = 0.0;
                return res;
            }
        }
        /// <summary>
        /// Creates a modification which is null, i.e. no valid ModOp2D
        /// </summary>
        public static ModOp2D Null
        {
            get
            {
                ModOp2D res;
                res.Matrix00 = res.Matrix11 = 0.0;
                res.Matrix01 = res.Matrix02 = 0.0;
                res.Matrix10 = res.Matrix12 = 0.0;
                return res;
            }
        }
        public bool IsNull
        {
            get
            {
                return Matrix00 == 0.0 && Matrix11 == 0.0 &&
                        Matrix01 == 0.0 && Matrix02 == 0.0 &&
                        Matrix10 == 0.0 && Matrix12 == 0.0;
            }
        }
        /// <summary>
        /// Creates a translation
        /// </summary>
        /// <param name="dx">x-offset</param>
        /// <param name="dy">y-offset</param>
        /// <returns>resulting modification</returns>
        public static ModOp2D Translate(double dx, double dy)
        {
            ModOp2D res = Identity;
            res.Matrix02 = dx;
            res.Matrix12 = dy;
            return res;
        }
        /// <summary>
        /// Creates a translation
        /// </summary>
        /// <param name="offset">offset vector</param>
        /// <returns>resulting modification</returns>
        public static ModOp2D Translate(GeoVector2D offset)
        {
            ModOp2D res = Identity;
            res.Matrix02 = offset.x;
            res.Matrix12 = offset.y;
            return res;
        }
        /// <summary>
        /// Creates a rotation about a fixpoint
        /// </summary>
        /// <param name="Center">the fixpoint</param>
        /// <param name="Rotation">the rotation angle</param>
        /// <returns>resulting modification</returns>
        public static ModOp2D Rotate(GeoPoint2D Center, SweepAngle Rotation)
        {
            ModOp2D res;
            res.Matrix00 = Math.Cos(Rotation);
            res.Matrix01 = -Math.Sin(Rotation);
            res.Matrix10 = -res.Matrix01;
            res.Matrix11 = res.Matrix00;
            res.Matrix02 = -Center.x * res.Matrix00 + Center.y * res.Matrix10 + Center.x;
            res.Matrix12 = Center.x * res.Matrix01 - Center.y * res.Matrix00 + Center.y;
            return res;
        }
        /// <summary>
        /// Creates a rotation about the origin
        /// </summary>
        /// <param name="Rotation">the rotation angle</param>
        /// <returns>resulting modification</returns>
        public static ModOp2D Rotate(SweepAngle Rotation)
        {
            ModOp2D res;
            res.Matrix00 = Math.Cos(Rotation);
            res.Matrix01 = -Math.Sin(Rotation);
            res.Matrix10 = -res.Matrix01;
            res.Matrix11 = res.Matrix00;
            res.Matrix02 = 0.0;
            res.Matrix12 = 0.0;
            return res;
        }
        public static ModOp2D Scale(double factor)
        {
            ModOp2D res;
            res.Matrix00 = factor;
            res.Matrix01 = 0.0;
            res.Matrix10 = 0.0;
            res.Matrix11 = factor;
            res.Matrix02 = 0.0;
            res.Matrix12 = 0.0;
            return res;
        }
        public static ModOp2D Scale(double factorx, double factory)
        {
            ModOp2D res;
            res.Matrix00 = factorx;
            res.Matrix01 = 0.0;
            res.Matrix10 = 0.0;
            res.Matrix11 = factory;
            res.Matrix02 = 0.0;
            res.Matrix12 = 0.0;
            return res;
        }
        public static ModOp2D Scale(GeoPoint2D center, double factor)
        {
            return ModOp2D.Translate(center.x, center.y) * ModOp2D.Scale(factor) * ModOp2D.Translate(-center.x, -center.y);
        }
        public static ModOp2D Scale(GeoPoint2D center, double factorx, double factory)
        {
            return ModOp2D.Translate(center.x, center.y) * ModOp2D.Scale(factorx, factory) * ModOp2D.Translate(-center.x, -center.y);
        }
        /// <summary>
        /// Constructs a modification, that transforms the src vectors to the dst vectors.
        /// The length of src and dst must be 2. The resulting modification is any kind of an affinity, that 
        /// projects the src vectors to the dst vectors. The origin (0,0) remains fixed.
        /// </summary>
        /// <param name="src">source vectors</param>
        /// <param name="dst">destination vectors</param>
        /// <returns>resulting transformation</returns>
        public static ModOp2D Fit(GeoVector2D[] src, GeoVector2D[] dst)
        {
            // 4 Unbekannte
            // m00*src0x + m01*src0y                       = dst0x
            //                       m10*src0x + m11*src0y = dst0y
            // m00*src1x + m01*src1y                       = dst1x
            //                       m10*src1x + m11*src1y = dst1y
            double[,] a = {
                {src[0].x,src[0].y,0.0,0.0},
                {0.0,0.0,src[0].x,src[0].y},
                {src[1].x,src[1].y,0.0,0.0},
                {0.0,0.0,src[1].x,src[1].y}};
            Matrix m = DenseMatrix.OfArray(a);
            Vector r = (Vector)m.Solve(new DenseVector(new double[] { dst[0].x, dst[0].y, dst[1].x, dst[1].y }));
            return new ModOp2D(r[0], r[1], 0.0, r[2], r[3], 0.0);
        }
        /// <summary>
        /// Constructs a modification, that transforms the Src points to the Dst points.
        /// The length of Src and Dst must be equal and less than 4. If the length is
        /// 1, the resulting ModOp is a translation, if the length is 2, the parameter
        /// DoScale decides whether the resulting ModOp is a translation and rotation 
        /// (DoScale==false) or translation, rotation and scaling (DoScale==true).
        /// If the length is 3, the resulting modification is any kind of an affinity, that 
        /// projects the src points to the dst points. 
        /// </summary>
        /// <param name="Src">source points</param>
        /// <param name="Dst">destination points</param>
        /// <param name="DoScale">scaling if two point pairs are given</param>
        /// <returns>resulting transformation</returns>
        public static ModOp2D Fit(GeoPoint2D[] Src, GeoPoint2D[] Dst, bool DoScale)
        {
            if (Src.Length != Dst.Length) throw new ModOpException("ModOp.Fit: Src and Dst must have the same length", ModOpException.tExceptionType.InvalidParameter);
            if (Src.Length < 1) throw new ModOpException("ModOp.Fit: at least one point must be given", ModOpException.tExceptionType.InvalidParameter);
            if (Src.Length == 1)
            {
                return Translate(Dst[0] - Src[0]);
            }
            else if (Src.Length == 2)
            {
                if (!DoScale)
                {
                    SweepAngle r = new SweepAngle(Src[1] - Src[0], Dst[1] - Dst[0]);
                    return ModOp2D.Translate(Dst[0] - Src[0]) * ModOp2D.Rotate(Src[0], r);
                }
                else
                {
                    /* m10==-m01, m00==m11: das ist Drehung und Skalierung ohne Verzerrung, gibt 4 Gleichungen:
                     * 
                     * m00*s0x + m01*s0y + m02 + 0  = p0x
                     * m00*s0y +-m01*s0x +  0  +m12 = p0y
                     * m00*s1x + m01*s1y + m02 + 0  = p1x
                     * m00*s1y +-m01*s1x +  0  +m12 = p1y
                     */
                    double[,] a = { { Src[0].x, Src[0].y,  1.0, 0.0 },
                                { Src[0].y, -Src[0].x, 0.0, 1.0 },
                                { Src[1].x, Src[1].y,  1.0, 0.0 },
                                { Src[1].y, -Src[1].x, 0.0, 1.0 } };
                    double[] b = { Dst[0].x, Dst[0].y, Dst[1].x, Dst[1].y };

                    Vector x = (Vector)DenseMatrix.OfArray(a).Solve(new DenseVector(b));
                    if (x.IsValid())
                    {
                        return new ModOp2D(x[0], x[1], x[2], -x[1], x[0], x[3]);
                    }
                    else
                    {
                        throw new ModOpException(ModOpException.tExceptionType.InvalidParameter);
                    }
                }
            }
            else if (Src.Length == 3)
            {
                double[,] a = new double[6, 6];
                double[] b = new double[6];

                a[0, 2] = 1.0;
                a[1, 5] = 1.0;
                a[2, 2] = 1.0;
                a[3, 5] = 1.0;
                a[4, 2] = 1.0;
                a[5, 5] = 1.0;
                a[0, 0] = Src[0].x; a[0, 1] = Src[0].y;
                a[1, 3] = Src[0].x; a[1, 4] = Src[0].y;
                a[2, 0] = Src[1].x; a[2, 1] = Src[1].y;
                a[3, 3] = Src[1].x; a[3, 4] = Src[1].y;
                a[4, 0] = Src[2].x; a[4, 1] = Src[2].y;
                a[5, 3] = Src[2].x; a[5, 4] = Src[2].y;

                b[0] = Dst[0].x; b[1] = Dst[0].y;
                b[2] = Dst[1].x; b[3] = Dst[1].y;
                b[4] = Dst[2].x; b[5] = Dst[2].y;

                Vector x = (Vector)DenseMatrix.OfArray(a).Solve(new DenseVector(b));
                if (x.IsValid())
                {
                    return new ModOp2D(x[0], x[1], x[2], x[3], x[4], x[5]);
                }
                else
                {
                    throw new ModOpException(ModOpException.tExceptionType.InvalidParameter);
                }
            }
            else if (Src.Length > 3)
            {   // überbestimmt: 
                // dst = m*src
                // dstix = m00*srcix+m01*srciy+m02
                // dstiy = m10*srcix+m11*srciy+m12
                Matrix a = new DenseMatrix(2 * Src.Length, 6); // mit 0 vorbesetzt
                Vector b = new DenseVector(2 * Src.Length);

                for (int i = 0; i < Src.Length; i++)
                {
                    a[2 * i + 0, 0] = Src[i].x;
                    a[2 * i + 0, 1] = Src[i].y;
                    a[2 * i + 0, 2] = 1.0;

                    a[2 * i + 1, 3] = Src[i].x;
                    a[2 * i + 1, 4] = Src[i].y;
                    a[2 * i + 1, 5] = 1.0;

                    b[2 * i + 0] = Dst[i].x;
                    b[2 * i + 1] = Dst[i].y;
                }
                var qrd = a.QR();
                if (qrd.IsFullRank)
                {
                    Vector x = (Vector)qrd.Solve(b);
                    return new ModOp2D(x[0], x[1], x[2], x[3], x[4], x[5]);
                }
                else
                {
                    throw new ModOpException(ModOpException.tExceptionType.InvalidParameter);
                }
            }

            throw new NotImplementedException();
        }
        /// <summary>
        /// Combines two modification into one modification. If two modifications have to be applied to
        /// several <see cref="GeoPoint2D"/>s or <see cref="GeoVector2D"/>s 
        /// it is faster to use the combination.
        /// </summary>
        /// <param name="lhs">second modification</param>
        /// <param name="rhs">first modification</param>
        /// <returns>the combination</returns>
        public static ModOp2D operator *(ModOp2D lhs, ModOp2D rhs)
        {
            ModOp2D res;
            res.Matrix00 = lhs.Matrix00 * rhs.Matrix00 + lhs.Matrix01 * rhs.Matrix10;
            res.Matrix01 = lhs.Matrix00 * rhs.Matrix01 + lhs.Matrix01 * rhs.Matrix11;
            res.Matrix02 = lhs.Matrix00 * rhs.Matrix02 + lhs.Matrix01 * rhs.Matrix12 + lhs.Matrix02;
            res.Matrix10 = lhs.Matrix10 * rhs.Matrix00 + lhs.Matrix11 * rhs.Matrix10;
            res.Matrix11 = lhs.Matrix10 * rhs.Matrix01 + lhs.Matrix11 * rhs.Matrix11;
            res.Matrix12 = lhs.Matrix10 * rhs.Matrix02 + lhs.Matrix11 * rhs.Matrix12 + lhs.Matrix12;
            return res;
        }
        /// <summary>
        /// Modifies the given point by this modification.
        /// </summary>
        /// <param name="m">modification</param>
        /// <param name="p">point</param>
        /// <returns>modified point</returns>
        public static GeoPoint2D operator *(ModOp2D m, GeoPoint2D p)
        {
            return new GeoPoint2D(m.Matrix00 * p.x + m.Matrix01 * p.y + m.Matrix02,
                    m.Matrix10 * p.x + m.Matrix11 * p.y + m.Matrix12);

        }
        /// <summary>
        /// Modifies the given vector by this modification
        /// </summary>
        /// <param name="m">modification</param>
        /// <param name="v">vector</param>
        /// <returns>modified vector</returns>
        public static GeoVector2D operator *(ModOp2D m, GeoVector2D v)
        {
            return new GeoVector2D(m.Matrix00 * v.x + m.Matrix01 * v.y, m.Matrix10 * v.x + m.Matrix11 * v.y);
        }
        /// <summary>
        /// Multiplies the double value by the scaling factor of this modification
        /// </summary>
        /// <param name="m">modification</param>
        /// <param name="d">input value</param>
        /// <returns>modified value</returns>
        public static double operator *(ModOp2D m, double d)
        {
            return Math.Sqrt(Math.Abs(m.Determinant)) * d;
        }
        /// <summary>
        /// Returns a 2d ModOp which represents the 2d part of a 3d ModOp. 
        /// </summary>
        /// <param name="m">3d ModOp</param>
        /// <returns>2d ModOp</returns>
        public static ModOp2D XYPart(ModOp m)
        {
            double[,] mat3 = m.Matrix;
            return new ModOp2D(mat3[0, 0], mat3[0, 1], mat3[0, 3], mat3[1, 0], mat3[1, 1], mat3[1, 3]);
        }
        /// <summary>
        /// Returns a 2d modification, that transforms from the coordinate system of the first
        /// plane to the coordinate system of the second plane. If the planes ar not coincident,
        /// the first plane is projected onto the second plane.
        /// </summary>
        /// <param name="FromPlane">Source plane</param>
        /// <param name="ToPlane">Destination plane</param>
        /// <returns>See above</returns>
        public static ModOp2D PlaneToplane(Plane FromPlane, Plane ToPlane)
        {
            ModOp m = ModOp.Transform(FromPlane.CoordSys, ToPlane.CoordSys);
            double[,] mat = m.Matrix;
            return new ModOp2D(mat[0, 0], mat[0, 1], mat[0, 3], mat[1, 0], mat[1, 1], mat[1, 3]);
        }
        /// <value>
        /// Returns the <see cref="System.Drawing.Drawing2D.Matrix"/> object equivalent to this
        /// modification.
        /// </value>
        public System.Drawing.Drawing2D.Matrix Matrix2D
        {
            get
            {
                return new System.Drawing.Drawing2D.Matrix((float)Matrix00, (float)Matrix10, (float)Matrix01, (float)Matrix11, (float)Matrix02, (float)Matrix12);
            }
        }
        public ModOp To3D()
        {
            return new ModOp(Matrix00, Matrix01, 0.0, Matrix02, Matrix10, Matrix11, 0.0, Matrix12, 0.0, 0.0, 1.0, 0.0);
        }
        internal double At(int i, int j)
        {
            switch (i * 3 + j)
            {
                case 0:
                    return Matrix00;
                case 1:
                    return Matrix01;
                case 2:
                    return Matrix02;
                case 3:
                    return Matrix10;
                case 4:
                    return Matrix11;
                case 5:
                    return Matrix12;
                default:
                    throw (new ModOpException(ModOpException.tExceptionType.InvalidParameter));
            }
        }
        public double this[int i, int j]
        {
            get
            {
                switch (i * 3 + j)
                {
                    case 0:
                        return Matrix00;
                    case 1:
                        return Matrix01;
                    case 2:
                        return Matrix02;
                    case 3:
                        return Matrix10;
                    case 4:
                        return Matrix11;
                    case 5:
                        return Matrix12;
                    default:
                        throw (new ModOpException(ModOpException.tExceptionType.InvalidParameter));
                }
            }
            set
            {
                switch (i * 3 + j)
                {
                    case 0:
                        Matrix00 = value;
                        break;
                    case 1:
                        Matrix01 = value;
                        break;
                    case 2:
                        Matrix02 = value;
                        break;
                    case 3:
                        Matrix10 = value;
                        break;
                    case 4:
                        Matrix11 = value;
                        break;
                    case 5:
                        Matrix12 = value;
                        break;
                    default:
                        throw (new ModOpException(ModOpException.tExceptionType.InvalidParameter));
                }
            }
        }

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        ModOp2D(SerializationInfo info, StreamingContext context)
        {
            Matrix00 = (double)info.GetValue("Matrix00", typeof(double));
            Matrix01 = (double)info.GetValue("Matrix01", typeof(double));
            Matrix02 = (double)info.GetValue("Matrix02", typeof(double));
            Matrix10 = (double)info.GetValue("Matrix10", typeof(double));
            Matrix11 = (double)info.GetValue("Matrix11", typeof(double));
            Matrix12 = (double)info.GetValue("Matrix12", typeof(double));
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Matrix00", Matrix00, typeof(double));
            info.AddValue("Matrix01", Matrix01, typeof(double));
            info.AddValue("Matrix02", Matrix02, typeof(double));
            info.AddValue("Matrix10", Matrix10, typeof(double));
            info.AddValue("Matrix11", Matrix11, typeof(double));
            info.AddValue("Matrix12", Matrix12, typeof(double));
        }
        #endregion
    }

    /// <summary>
    /// Homogenuos matrix for 3 dimensions, i.e. 4x4 matrix. Mainly used for perspective views.
    /// </summary>
    [Serializable()]
    public struct Matrix4 : ISerializable
    {
        public Matrix<double> hm;
        public Matrix4(Matrix<double> hm)
        {
            if (hm == null)
            {
                this.hm = DenseMatrix.OfArray(new double[,] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });
            }
            else
            {
                if (hm.RowCount != 4 || hm.ColumnCount != 4) throw new ApplicationException("Matrix4: wrong number of rows or columns");
                this.hm = (Matrix)hm.Clone();
            }
        }
        public Matrix4(double[,] m)
        {
            hm = DenseMatrix.OfArray(m);
        }
        public Matrix4(ModOp m)
        {
            this.hm = DenseMatrix.OfArray(m.Matrix); // new Matrix(m);
        }
        /// <summary>
        /// Modifies the given point by this modification.
        /// </summary>
        /// <param name="m">modification</param>
        /// <param name="p">point</param>
        /// <returns>modified point</returns>
        public static GeoPoint operator *(Matrix4 m, GeoPoint p)
        {
            if (m.hm == null) return p; // so you don't need to check
            double d = m.hm[3, 0] * p.x + m.hm[3, 1] * p.y + m.hm[3, 2] * p.z + m.hm[3, 3];
            double x = (m.hm[0, 0] * p.x + m.hm[0, 1] * p.y + m.hm[0, 2] * p.z + m.hm[0, 3]) / d;
            double y = (m.hm[1, 0] * p.x + m.hm[1, 1] * p.y + m.hm[1, 2] * p.z + m.hm[1, 3]) / d;
            double z = (m.hm[2, 0] * p.x + m.hm[2, 1] * p.y + m.hm[2, 2] * p.z + m.hm[2, 3]) / d;
            return new GeoPoint(x, y, z);
        }
        public static GeoVector operator *(Matrix4 m, GeoVector v)
        {
            if (m.hm == null) return v; // so you don't need to check
            double d = m.hm[3, 0] * v.x + m.hm[3, 1] * v.y + m.hm[3, 2] * v.z + m.hm[3, 3];
            double x = (m.hm[0, 0] * v.x + m.hm[0, 1] * v.y + m.hm[0, 2] * v.z) / d;
            double y = (m.hm[1, 0] * v.x + m.hm[1, 1] * v.y + m.hm[1, 2] * v.z) / d;
            double z = (m.hm[2, 0] * v.x + m.hm[2, 1] * v.y + m.hm[2, 2] * v.z) / d;
            return new GeoVector(x, y, z);
        }
        public static Matrix4 operator *(Matrix4 l, Matrix4 r)
        {
            if (l.hm == null || r.hm == null) return new Matrix4((double[,])null);
            Matrix4 res = new Matrix4((Matrix)(l.hm * r.hm));
            return res;
        }
        public Matrix4 GetInverse()
        {
            Matrix<double> inv = hm.Inverse();
            if (inv != null) return new Matrix4(inv);
            else return new Matrix4((double[,])null);
        }
        public static explicit operator double[,](Matrix4 m)
        {
            return m.hm.ToArray();
        }
        public Matrix Matrix
        {
            get
            {
                return (Matrix)hm;
            }
        }
        #region ISerializable Members
        Matrix4(SerializationInfo info, StreamingContext context)
        {
            this.hm = DenseMatrix.OfArray((double[,])info.GetValue("Matrix", typeof(double[,])));
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (hm == null)
            {
                hm = DenseMatrix.OfArray(new double[,] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }); ; //.Identity(4, 4);
            }
            info.AddValue("Matrix", (double[,])hm.ToArray());
        }
        #endregion
        public double Determinant
        {
            get
            {
                return hm.Determinant();
            }
        }
        public bool IsValid
        {
            get
            {
                return hm != null;
            }
        }
    }

    /// <summary>
    /// A 3-dimensional modification operation implemented as a homogenous matrix 4*3.
    /// You can apply such a modification to <see cref="GeoPoint"/>s or <see cref="GeoVector"/>s
    /// or you can use it for GeoObjects <see cref="GeoObject.IGeoObject.Modify"/>.
    /// If you want to move, rotate, scale reflect or generally modify a GeoObject you will need this class.
    /// Use the static methods to create ModOps that do a required modification (like <see cref="Rotate"/>, <see cref="Translate"/> etc.
    /// </summary>
    [Serializable()]
    [JsonVersion(serializeAsStruct = true, version = 1)]
    public struct ModOp : ISerializable, IJsonSerialize
    {
        /// <summary>
        /// Die homogene Matrix, die die Modifikationsoperation darstellt.
        /// Sie hat drei Zeilen und vier Spalten. Die vierte Zeile hätte immer
        /// den Wert (0,0,0,1) und wird nicht in den Daten dargestellt.
        /// </summary>
        private double Matrix00, Matrix01, Matrix02, Matrix03, Matrix10, Matrix11, Matrix12, Matrix13, Matrix20, Matrix21, Matrix22, Matrix23;
        private double ScalingFactor;
        /// <summary>
        /// Kind of operation
        /// </summary>
        public enum ModificationMode
        {
            /// <summary>
            /// Identity: leaves everything unchanged
            /// </summary>
            Identity,
            /// <summary>
            /// Rotation: rotation about an axis
            /// </summary>
            Rotation,
            /// <summary>
            /// Translation: moves <see cref="GeoPoint"/>s, leaves <see cref="GeoVector"/>s unchanged,
            /// </summary>
            Translation,
            /// <summary>
            /// PntMirror: reflection about a point,
            /// </summary>
            PntMirror,
            /// <summary>
            /// Ax1Mirror: reflection about an axis,
            /// </summary>
            Ax1Mirror,
            /// <summary>
            /// Ax2Mirror: reflection at a plane,
            /// </summary>
            Ax2Mirror,
            /// <summary>
            /// Scale: scaling,
            /// </summary>
            Scale,
            /// <summary>
            /// CompoundTrsf: combination of the modes mentioned before
            /// </summary>
            CompoundTrsf,
            /// <summary>
            /// Other: distortion
            /// </summary>
            Other
        }; // Other ist Verzerrung, alles andere ist nicht verzerrend
        private ModificationMode mode;

        /// <summary>
        /// Gets the kind of operation this ModOp performs.
        /// </summary>
        public ModificationMode Mode
        {
            get { return mode; }
        }
        /// <summary>
        /// Gets the scaling factor if appropriate
        /// </summary>
        public double Factor
        {
            get { return Math.Pow(Math.Abs(Determinant), 1.0 / 3.0); }
        }
        /// <summary>
        /// Gets or sets the Matrix that defines this mmodification
        /// </summary>
        public double[,] Matrix
        {
            get
            {
                double[,] res = new double[3, 4];
                res[0, 0] = Matrix00;
                res[0, 1] = Matrix01;
                res[0, 2] = Matrix02;
                res[0, 3] = Matrix03;
                res[1, 0] = Matrix10;
                res[1, 1] = Matrix11;
                res[1, 2] = Matrix12;
                res[1, 3] = Matrix13;
                res[2, 0] = Matrix20;
                res[2, 1] = Matrix21;
                res[2, 2] = Matrix22;
                res[2, 3] = Matrix23;
                return res;
            }
            set
            {
                Matrix00 = value[0, 0];
                Matrix01 = value[0, 1];
                Matrix02 = value[0, 2];
                Matrix03 = value[0, 3];
                Matrix10 = value[1, 0];
                Matrix11 = value[1, 1];
                Matrix12 = value[1, 2];
                Matrix13 = value[1, 3];
                Matrix20 = value[2, 0];
                Matrix21 = value[2, 1];
                Matrix22 = value[2, 2];
                Matrix23 = value[2, 3];
            }
        }

        public Matrix ToMatrix()
        {
            double[,] A = new double[4, 4];
            double[,] mm = Matrix;
            for (int i = 0; i < 3; ++i)
            {
                for (int j = 0; j < 4; ++j)
                {
                    A[i, j] = mm[i, j];
                }
            }
            A[3, 3] = 1.0; // A[3,k] = 0.0; nicht nötig
            return DenseMatrix.OfArray(A);
        }
        internal ModOp(GeoVector vx, GeoVector vy, GeoVector vz, GeoPoint loc)
        {   // setzt die XY Ebene in die durch die Vektoren und den Punkt gegebene Ebene um
            Matrix00 = vx.x;
            Matrix10 = vx.y;
            Matrix20 = vx.z;
            Matrix01 = vy.x;
            Matrix11 = vy.y;
            Matrix21 = vy.z;
            Matrix02 = vz.x;
            Matrix12 = vz.y;
            Matrix22 = vz.z;
            Matrix03 = loc.x;
            Matrix13 = loc.y;
            Matrix23 = loc.z;
            ScalingFactor = Matrix00;
            mode = ModificationMode.Other;
        }
        /// <summary>
        /// Creates a modification according to the given coefficients 
        /// </summary>
        /// <param name="m00"></param>
        /// <param name="m01"></param>
        /// <param name="m02"></param>
        /// <param name="m03"></param>
        /// <param name="m10"></param>
        /// <param name="m11"></param>
        /// <param name="m12"></param>
        /// <param name="m13"></param>
        /// <param name="m20"></param>
        /// <param name="m21"></param>
        /// <param name="m22"></param>
        /// <param name="m23"></param>
        public ModOp(double m00, double m01, double m02, double m03, double m10, double m11, double m12, double m13, double m20, double m21, double m22, double m23)
        {
            Matrix00 = m00;
            Matrix01 = m01;
            Matrix02 = m02;
            Matrix03 = m03;
            Matrix10 = m10;
            Matrix11 = m11;
            Matrix12 = m12;
            Matrix13 = m13;
            Matrix20 = m20;
            Matrix21 = m21;
            Matrix22 = m22;
            Matrix23 = m23;
            ScalingFactor = m00;
            mode = ModificationMode.Other;
        }
        public ModOp(double[,] m)
        {
            if (m.GetLength(0) == 3 && m.GetLength(1) == 3)
            {
                Matrix00 = m[0, 0];
                Matrix01 = m[0, 1];
                Matrix02 = m[0, 2];
                Matrix03 = 0.0;
                Matrix10 = m[1, 0];
                Matrix11 = m[1, 1];
                Matrix12 = m[1, 2];
                Matrix13 = 0.0;
                Matrix20 = m[2, 0];
                Matrix21 = m[2, 1];
                Matrix22 = m[2, 2];
                Matrix23 = 0.0;
                ScalingFactor = m[0, 0];
                mode = ModificationMode.Other;
            }
            else if (m.GetLength(0) >= 3 && m.GetLength(1) >= 4)
            {
                Matrix00 = m[0, 0];
                Matrix01 = m[0, 1];
                Matrix02 = m[0, 2];
                Matrix03 = m[0, 3];
                Matrix10 = m[1, 0];
                Matrix11 = m[1, 1];
                Matrix12 = m[1, 2];
                Matrix13 = m[1, 3];
                Matrix20 = m[2, 0];
                Matrix21 = m[2, 1];
                Matrix22 = m[2, 2];
                Matrix23 = m[2, 3];
                ScalingFactor = m[0, 0];
                mode = ModificationMode.Other;
            }
            else
            {
                Matrix00 = Matrix11 = Matrix22 = 1.0;
                Matrix01 = Matrix02 = Matrix03 = 0.0;
                Matrix10 = Matrix12 = Matrix13 = 0.0;
                Matrix20 = Matrix21 = Matrix23 = 0.0;
                ScalingFactor = 1.0;
                mode = ModificationMode.Identity;
            }
        }
        public ModOp(double[,] m, GeoVector trans) : this(m)
        {
            Matrix03 = trans.x;
            Matrix13 = trans.y;
            Matrix23 = trans.z;
        }


        /// <summary>
        /// Creates a modification that leaves everything unchanged.
        /// </summary>
        public static ModOp Identity
        {
            get
            {
                ModOp res;
                res.Matrix00 = res.Matrix11 = res.Matrix22 = 1.0;
                res.Matrix01 = res.Matrix02 = res.Matrix03 = 0.0;
                res.Matrix10 = res.Matrix12 = res.Matrix13 = 0.0;
                res.Matrix20 = res.Matrix21 = res.Matrix23 = 0.0;
                res.ScalingFactor = 1.0;
                res.mode = ModificationMode.Identity;
                return res;
            }
        }
        /// <summary>
        /// Creates a modification that makes everythin to 0
        /// </summary>
        public static ModOp Null
        {
            get
            {
                ModOp res;
                res.Matrix00 = res.Matrix11 = res.Matrix22 = 0.0;
                res.Matrix01 = res.Matrix02 = res.Matrix03 = 0.0;
                res.Matrix10 = res.Matrix12 = res.Matrix13 = 0.0;
                res.Matrix20 = res.Matrix21 = res.Matrix23 = 0.0;
                res.ScalingFactor = 0.0;
                res.mode = ModificationMode.Other;
                return res;
            }
        }
        /// <summary>
        /// Modifies the given 3 dimensional point and returns the 2d point, omitting the z-coordinate
        /// </summary>
        /// <param name="p">the point to project</param>
        /// <returns>resulting 2d point</returns>
        internal GeoPoint2D Project(GeoPoint p)
        {
            return new GeoPoint2D(Matrix00 * p.x + Matrix01 * p.y + Matrix02 * p.z + Matrix03,
                Matrix10 * p.x + Matrix11 * p.y + Matrix12 * p.z + Matrix13);
        }
        /// <summary>
        /// Modifies the given 3 dimensional vector and returns the 2d vector, omitting the z-coordinate
        /// </summary>
        /// <param name="p">the vector to project</param>
        /// <returns>the resulting vector</returns>
        internal GeoVector2D Project(GeoVector v)
        {
            return new GeoVector2D(Matrix00 * v.x + Matrix01 * v.y + Matrix02 * v.z,
                Matrix10 * v.x + Matrix11 * v.y + Matrix12 * v.z);
        }
        /// <summary>
        /// Constructs a ModOp, that maps the Src coordinate system to the Dst coordinate system
        /// </summary>
        /// <param name="Src">source coordinate system</param>
        /// <param name="Dst">destination coordinate system</param>
        /// <returns>resulting transformation</returns>
        public static ModOp Transform(CoordSys Src, CoordSys Dst)
        {
            GeoVector[] vctSrc = new GeoVector[3];
            vctSrc[0] = Src.DirectionX;
            vctSrc[1] = Src.DirectionY;
            vctSrc[2] = Src.Normal;
            GeoVector[] vctDst = new GeoVector[3];
            vctDst[0] = Dst.DirectionX;
            vctDst[1] = Dst.DirectionY;
            vctDst[2] = Dst.Normal;
            ModOp f = Fit(vctSrc, vctDst);
            f.mode = ModificationMode.CompoundTrsf;
            GeoPoint p = f * Src.Location;
            return Translate(Dst.Location - p) * f;
        }
        /// <summary>
        /// Constructs a ModOp, that performs a translation by the given offsets
        /// </summary>
        /// <param name="dx">offset in direction of the x-axis</param>
        /// <param name="dy">offset in direction of the y-axis</param>
        /// <param name="dz">offset in direction of the z-axis</param>
        /// <returns>resulting transformation</returns>
        public static ModOp Translate(double dx, double dy, double dz)
        {
            ModOp res = Identity;
            res.Matrix03 = dx;
            res.Matrix13 = dy;
            res.Matrix23 = dz;
            res.ScalingFactor = 1.0;
            res.mode = ModificationMode.Translation;
            return res;
        }
        /// <summary>
        /// Constructs a ModOp, that performs a translation by the given offset vector
        /// </summary>
        /// <param name="offset">offset vector</param>
        /// <returns>resulting transformation</returns>
        public static ModOp Translate(GeoVector offset)
        {
            ModOp res = Identity;
            res.Matrix03 = offset.x;
            res.Matrix13 = offset.y;
            res.Matrix23 = offset.z;
            res.ScalingFactor = 1.0;
            res.mode = ModificationMode.Translation;
            return res;
        }
        internal static ModOp Rotate(int Axis, SweepAngle Rotation)
        {	// Drehung um die x- y- bzw z-Achse
            double s = Math.Sin(Rotation);
            double c = Math.Cos(Rotation);
            ModOp res = Identity;
            switch (Axis)
            {
                case 0: // rotate about x-axis
                    res.Matrix11 = c;
                    res.Matrix12 = -s;
                    res.Matrix21 = s;
                    res.Matrix22 = c;
                    break;
                case 1: // rotate about y-axis
                    res.Matrix22 = c;
                    res.Matrix20 = -s;
                    res.Matrix02 = s;
                    res.Matrix00 = c;
                    break;
                case 2: // rotate about z-axis
                    res.Matrix00 = c;
                    res.Matrix01 = -s;
                    res.Matrix10 = s;
                    res.Matrix11 = c;
                    break;
            }
            res.mode = ModificationMode.Rotation;
            return res;
        }
        internal static double sqr(double x) { return x * x; }
        /// <summary>
        /// Creates a modification that performs a rotation about an axis through the origina
        /// </summary>
        /// <param name="Axis">direction of the axis</param>
        /// <param name="Rotation">rotation angle</param>
        /// <returns>the resulting modification</returns>
        public static ModOp Rotate(GeoVector Axis, SweepAngle Rotation)
        {
            // siehe http://mathworld.wolfram.com/RodriguesRotationFormula.html
            Axis.Norm();
            double s = Math.Sin(Rotation);
            double c = Math.Cos(Rotation);
            return new ModOp(
            c + sqr(Axis.x) * (1 - c), Axis.x * Axis.y * (1 - c) - Axis.z * s, Axis.y * s + Axis.x * Axis.z * (1 - c), 0.0,
            Axis.z * s + Axis.x * Axis.y * (1 - c), c + sqr(Axis.y) * (1 - c), -Axis.x * s + Axis.y * Axis.z * (1 - c), 0.0,
            -Axis.y * s + Axis.x * Axis.z * (1 - c), Axis.x * s + Axis.y * Axis.z * (1 - c), c + sqr(Axis.z) * (1 - c), 0.0);

            //double az = Math.Atan2(Axis.y, Axis.x); // rotate by -az to end up in the xz Ebene
            //double ay = Math.Atan2(Axis.z, Math.Sqrt(sqr(Axis.y) + sqr(Axis.x))); // rotate by -ay to end up as x-axis
            //return ModOp.Rotate(2, az) * ModOp.Rotate(1, -ay) * ModOp.Rotate(0, Rotation) * ModOp.Rotate(1, ay) * ModOp.Rotate(2, -az);
        }
        public static ModOp Rotate(GeoVector Axis, double sin, double cos)
        {
            // siehe http://mathworld.wolfram.com/RodriguesRotationFormula.html
            Axis.Norm();
            double s = sin;
            double c = cos;
            return new ModOp(
            c + sqr(Axis.x) * (1 - c), Axis.x * Axis.y * (1 - c) - Axis.z * s, Axis.y * s + Axis.x * Axis.z * (1 - c), 0.0,
            Axis.z * s + Axis.x * Axis.y * (1 - c), c + sqr(Axis.y) * (1 - c), -Axis.x * s + Axis.y * Axis.z * (1 - c), 0.0,
            -Axis.y * s + Axis.x * Axis.z * (1 - c), Axis.x * s + Axis.y * Axis.z * (1 - c), c + sqr(Axis.z) * (1 - c), 0.0);
        }
        /// <summary>
        /// Creates a modification that performs a rotation about an axis through the given point
        /// </summary>
        /// <param name="FixPoint">point on the axis</param>
        /// <param name="Axis">direction of the axis</param>
        /// <param name="Rotation">rotation angle</param>
        /// <returns>the resulting modification</returns>
        public static ModOp Rotate(GeoPoint FixPoint, GeoVector Axis, SweepAngle Rotation)
        {
            GeoVector v = new GeoVector(FixPoint.x, FixPoint.y, FixPoint.z);
            return ModOp.Translate(v) * ModOp.Rotate(Axis, Rotation) * ModOp.Translate(-v);
        }
        /// <summary>
        /// Creates a rotation around the fixpoint that moves the vector <paramref name="from"/> to the vector <paramref name="to"/>.
        /// </summary>
        /// <param name="FixPoint">Fixpoint for the rotation</param>
        /// <param name="from">Source vector</param>
        /// <param name="to">Destination vector</param>
        /// <returns>The modification that performs the rotation</returns>
        public static ModOp Rotate(GeoPoint fixPoint, GeoVector from, GeoVector to)
        {
            GeoVector axis = from ^ to;
            if (axis.IsNullVector()) return ModOp.Identity;
            SweepAngle sw = new SweepAngle(from, to);
            return ModOp.Rotate(fixPoint, axis, sw);
        }
        /// <summary>
        /// Creates a modification that performs a scaling about the origin
        /// </summary>
        /// <param name="Factor">scaling factor</param>
        /// <returns>the resulting modification</returns>
        public static ModOp Scale(double Factor)
        {
            ModOp res = Identity;
            res.Matrix00 = Factor;
            res.Matrix11 = Factor;
            res.Matrix22 = Factor;
            res.mode = ModificationMode.Scale;
            return res;
        }
        /// <summary>
        /// Creates a modification that performs a scaling about a given point
        /// </summary>
        /// <param name="FixPoint">fixpoint for the scaling</param>
        /// <param name="Factor">scaling factor</param>
        /// <returns>the resulting modification</returns>
        public static ModOp Scale(GeoPoint FixPoint, double Factor)
        {
            GeoVector v = new GeoVector(FixPoint.x, FixPoint.y, FixPoint.z);
            return ModOp.Translate(v) * ModOp.Scale(Factor) * ModOp.Translate(-v);
        }
        /// <summary>
        /// Creates a modification that performs a scaling with different factors
        /// in x,y and z direction
        /// </summary>
        /// <param name="FactorX">scaling in the direction of the x-axis</param>
        /// <param name="FactorY">scaling in the direction of the y-axis</param>
        /// <param name="FactorZ">scaling in the direction of the z-axis</param>
        /// <returns>the resulting modification</returns>
        public static ModOp Scale(double FactorX, double FactorY, double FactorZ)
        {
            ModOp res = Identity;
            res.Matrix00 = FactorX;
            res.Matrix11 = FactorY;
            res.Matrix22 = FactorZ;
            res.mode = ModificationMode.Other; // denn Scale gilt nur, wenn alles gleich ist
            return res;
        }
        /// <summary>
        /// Creates a modification that performs a scaling in a given direction
        /// </summary>
        /// <param name="Factor">scaling factor for the given direction</param>
        /// <param name="Direction">direction for the scaling</param>
        /// <returns>the resulting modification</returns>
        public static ModOp Scale(GeoVector Direction, double Factor)
        {
            double az = Math.Atan2(Direction.y, Direction.x); // rotate by -az to end up in the xz Ebene
            double ay = Math.Atan2(Direction.z, Math.Sqrt(sqr(Direction.y) + sqr(Direction.x))); // rotate by -ay to end up as x-axis
            return ModOp.Rotate(2, az) * ModOp.Rotate(1, -ay) * ModOp.Scale(Factor, 1.0, 1.0) * ModOp.Rotate(1, ay) * ModOp.Rotate(2, -az);
        }
        /// <summary>
        /// Creates a modification that performs a scaling in a given direction with a fixpoint
        /// </summary>
        /// <param name="Factor">scaling factor for the given direction</param>
        /// <param name="Direction">direction for the scaling</param>
        /// <param name="FixPoint">fixpoint for the scaling</param>
        /// <returns>the resulting modification</returns>
        public static ModOp Scale(GeoPoint FixPoint, GeoVector Direction, double Factor)
        {
            GeoVector v = new GeoVector(FixPoint.x, FixPoint.y, FixPoint.z);
            return ModOp.Translate(v) * ModOp.Scale(Direction, Factor) * ModOp.Translate(-v);
        }
        internal static ModOp From2D(ModOp2D m)
        {
            double[,] mat2d = m.Matrix;
            ModOp res;

            res.Matrix00 = mat2d[0, 0];
            res.Matrix01 = mat2d[0, 1];
            res.Matrix02 = 0.0;
            res.Matrix03 = mat2d[0, 2];

            res.Matrix10 = mat2d[1, 0];
            res.Matrix11 = mat2d[1, 1];
            res.Matrix12 = 0.0;
            res.Matrix13 = mat2d[1, 2];

            res.Matrix20 = 0.0;
            res.Matrix21 = 0.0;
            res.Matrix22 = 1.0;
            res.Matrix23 = 0.0;

            res.ScalingFactor = Math.Sqrt(Math.Abs(m.Determinant));
            res.mode = ModificationMode.Other;

            return res;
        }
        /// <summary>
        /// Constructs a ModOp, that performs a reflection about the y/z plane
        /// </summary>
        /// <returns>resulting transformation</returns>
        public static ModOp ReflectPlane()
        {
            ModOp res = Identity;
            res.Matrix00 = -1.0;
            res.mode = ModificationMode.Ax2Mirror;
            return res;
        }
        /// <summary>
        /// Constructs a ModOp, that performs a reflection about the given plane
        /// </summary>
        /// <param name="pln">plane for reflection</param>
        /// <returns>resulting transformation</returns>
        public static ModOp ReflectPlane(Plane pln)
        {
            double az = Math.Atan2(pln.Normal.y, pln.Normal.x); // rotate by -az to end up in the xz Ebene
            double ay = Math.Atan2(pln.Normal.z, Math.Sqrt(sqr(pln.Normal.y) + sqr(pln.Normal.x))); // rotate by -ay to end up as x-axis
            GeoVector v = new GeoVector(pln.Location.x, pln.Location.y, pln.Location.z);
            return ModOp.Translate(v) * ModOp.Rotate(2, az) * ModOp.Rotate(1, -ay) * ModOp.ReflectPlane() * ModOp.Rotate(1, ay) * ModOp.Rotate(2, -az) * ModOp.Translate(-v);
        }
        /// <summary>
        /// Constructs a ModOp, that performs a reflection about the given point
        /// </summary>
        /// <param name="pln">point for reflection</param>
        /// <returns>resulting transformation</returns>
        public static ModOp ReflectPoint(GeoPoint center)
        {
            return new ModOp(-1, 0, 0, 2 * center.x, 0, -1, 0, 2 * center.y, 0, 0, -1, 2 * center.z);
        }
        internal static ModOp Fit(GeoVector[] Src, GeoVector[] Dst)
        {
            if (Src.Length != 3 || Dst.Length != 3) throw new ModOpException("ModOp.Fit: unable to perform fit", ModOpException.tExceptionType.InvalidParameter);
            double[,] a = new double[9, 9];
            a[0, 0] = Src[0].x;
            a[0, 1] = Src[0].y;
            a[0, 2] = Src[0].z;
            a[1, 3] = Src[0].x;
            a[1, 4] = Src[0].y;
            a[1, 5] = Src[0].z;
            a[2, 6] = Src[0].x;
            a[2, 7] = Src[0].y;
            a[2, 8] = Src[0].z;
            a[3, 0] = Src[1].x;
            a[3, 1] = Src[1].y;
            a[3, 2] = Src[1].z;
            a[4, 3] = Src[1].x;
            a[4, 4] = Src[1].y;
            a[4, 5] = Src[1].z;
            a[5, 6] = Src[1].x;
            a[5, 7] = Src[1].y;
            a[5, 8] = Src[1].z;
            a[6, 0] = Src[2].x;
            a[6, 1] = Src[2].y;
            a[6, 2] = Src[2].z;
            a[7, 3] = Src[2].x;
            a[7, 4] = Src[2].y;
            a[7, 5] = Src[2].z;
            a[8, 6] = Src[2].x;
            a[8, 7] = Src[2].y;
            a[8, 8] = Src[2].z;
            MathNet.Numerics.LinearAlgebra.Double.Matrix A = MathNet.Numerics.LinearAlgebra.Double.DenseMatrix.OfArray(a);
            MathNet.Numerics.LinearAlgebra.Double.Matrix B = MathNet.Numerics.LinearAlgebra.Double.DenseMatrix.OfColumnArrays(new double[] { Dst[0].x, Dst[0].y, Dst[0].z, Dst[1].x, Dst[1].y, Dst[1].z, Dst[2].x, Dst[2].y, Dst[2].z });
            MathNet.Numerics.LinearAlgebra.Double.Matrix X = (MathNet.Numerics.LinearAlgebra.Double.Matrix)A.Solve(B);
            ////Matrix A = new Matrix(a);
            ////Matrix B = new Matrix(new double[] { Dst[0].x, Dst[0].y, Dst[0].z, Dst[1].x, Dst[1].y, Dst[1].z, Dst[2].x, Dst[2].y, Dst[2].z }, 9);
            ////Matrix X = A.SaveSolve(B);
            ModOp res = ModOp.Identity;
            if (!double.IsNaN(X[0, 0]) && !double.IsInfinity(X[0, 0]))
            {
                res.Matrix00 = X[0, 0];
                res.Matrix01 = X[1, 0];
                res.Matrix02 = X[2, 0];
                res.Matrix10 = X[3, 0];
                res.Matrix11 = X[4, 0];
                res.Matrix12 = X[5, 0];
                res.Matrix20 = X[6, 0];
                res.Matrix21 = X[7, 0];
                res.Matrix22 = X[8, 0];
            }
            else throw new ModOpException("ModOp.Fit: unable to perform fit", ModOpException.tExceptionType.InvalidParameter);
            res.mode = ModificationMode.Other;
            // ModOp old = FitOld(Src, Dst);
            return res;

        }
        internal static ModOp FitOld(GeoVector[] Src, GeoVector[] Dst)
        {	// Abbildung von 3 Vektoren
            if (Src.Length != 3 || Dst.Length != 3) throw new ModOpException("ModOp.Fit: unable to perform fit", ModOpException.tExceptionType.InvalidParameter);
            double[,] a = new double[9, 9];
            double[] b = new double[9];
            a[0, 0] = Src[0].x;
            a[0, 1] = Src[0].y;
            a[0, 2] = Src[0].z;
            a[1, 3] = Src[0].x;
            a[1, 4] = Src[0].y;
            a[1, 5] = Src[0].z;
            a[2, 6] = Src[0].x;
            a[2, 7] = Src[0].y;
            a[2, 8] = Src[0].z;
            a[3, 0] = Src[1].x;
            a[3, 1] = Src[1].y;
            a[3, 2] = Src[1].z;
            a[4, 3] = Src[1].x;
            a[4, 4] = Src[1].y;
            a[4, 5] = Src[1].z;
            a[5, 6] = Src[1].x;
            a[5, 7] = Src[1].y;
            a[5, 8] = Src[1].z;
            a[6, 0] = Src[2].x;
            a[6, 1] = Src[2].y;
            a[6, 2] = Src[2].z;
            a[7, 3] = Src[2].x;
            a[7, 4] = Src[2].y;
            a[7, 5] = Src[2].z;
            a[8, 6] = Src[2].x;
            a[8, 7] = Src[2].y;
            a[8, 8] = Src[2].z;
            b[0] = Dst[0].x;
            b[1] = Dst[0].y;
            b[2] = Dst[0].z;
            b[3] = Dst[1].x;
            b[4] = Dst[1].y;
            b[5] = Dst[1].z;
            b[6] = Dst[2].x;
            b[7] = Dst[2].y;
            b[8] = Dst[2].z;
            Vector x = (Vector)DenseMatrix.OfArray(a).Solve(new DenseVector(b));
            if (!x.IsValid()) throw new ModOpException("ModOp.Fit: unable to perform fit", ModOpException.tExceptionType.InvalidParameter);
            ModOp res = ModOp.Identity;
            res.Matrix00 = x[0];
            res.Matrix01 = x[1];
            res.Matrix02 = x[2];
            res.Matrix10 = x[3];
            res.Matrix11 = x[4];
            res.Matrix12 = x[5];
            res.Matrix20 = x[6];
            res.Matrix21 = x[7];
            res.Matrix22 = x[8];
            res.mode = ModificationMode.Other;
            return res;
        }
        /// <summary>
        /// Constructs a ModOp, that transforms the Src points to the Dst points.
        /// The length of Src and Dst must be equal and less than 5. If the length is
        /// 1, the resulting ModOp is a translation, if the length is 2, the parameter
        /// DoScale decides whether the resulting ModOp is a translation and rotation 
        /// (DoScale==false) or translation, rotation and scaling (DoScale==true).
        /// If the length is 3 or 4, the resulting ModOp is any kind of an affinity, that 
        /// projects the src points to the dst points. 
        /// </summary>
        /// <param name="Src">source points</param>
        /// <param name="Dst">destination points</param>
        /// <param name="DoScale">scaling if two point pairs are given</param>
        /// <returns>resulting transformation</returns>
        public static ModOp Fit(GeoPoint[] Src, GeoPoint[] Dst, bool DoScale)
        {
            if (Src.Length != Dst.Length) throw new ModOpException("ModOp.Fit: Src and Dst must have the same length", ModOpException.tExceptionType.InvalidParameter);
            if (Src.Length < 1) throw new ModOpException("ModOp.Fit: at least one point must be given", ModOpException.tExceptionType.InvalidParameter);
            if (Src.Length == 1)
            {
                return Translate(Dst[0] - Src[0]);
            }
            if (Src.Length == 2)
            {
                GeoVector s1 = Src[1] - Src[0];
                GeoVector d1 = Dst[1] - Dst[0];
                // die Verschiebung wird so gewählt, dass die Mitten der beiden Punktpaare aufeinander abgebildet werden
                // ist für AnimatedView so das beste
                GeoPoint srccenter = new GeoPoint(Src[0], Src[1]);
                GeoPoint dtscenter = new GeoPoint(Dst[0], Dst[1]);
                // Es fehlen noch zwei Vektoren für die Abbildung von 3 Vektoren
                // Wir nehmen zwei Vektoren, die auf s1 senkrecht stehen und auch auf d1
                GeoVector s2 = s1 ^ d1; // wenn der 0 ist, dann ist es eine Parallelverschiebung.
                if (s2.IsNullVector())
                {   // beide Vektoren parallel, es ist nur eine Verschiebung
                    ModOp res = ModOp.Translate(dtscenter - srccenter);
                    if (DoScale)
                    {   // zusätzlich skalieren
                        double factor = d1.Length / s1.Length;
                        ModOp scale = Scale(Dst[0], Dst[1] - Dst[0], factor);
                        return scale * res;
                    }
                    else return res;
                }
                else
                {   // s2 steht auf s1 und d1 senkrecht
                    GeoVector s3 = s1 ^ s2;
                    GeoVector d3 = d1 ^ s2;
                    if (!DoScale)
                    {
                        s1.Norm();
                        s2.Norm();
                        s3.Norm();
                        d1.Norm();
                        d3.Norm();
                    }
                    ModOp res = ModOp.Fit(new GeoVector[] { s1, s2, s3 }, new GeoVector[] { d1, s2, d3 });
                    // erst in den Ursprung ziehen, dann Fit, dann in das Ziel ziehen, Fit lässt den Ursprung unverändert
                    return ModOp.Translate(dtscenter.x, dtscenter.y, dtscenter.z) * res * ModOp.Translate(-srccenter.x, -srccenter.y, -srccenter.z);
                }

                //GeoPoint[] AllPoints = new GeoPoint[Src.Length + Dst.Length];
                //Src.CopyTo(AllPoints, 0);
                //Dst.CopyTo(AllPoints, Src.Length);
                //double MaxDist;
                //Plane Invariant = Plane.FromPoints(AllPoints, out MaxDist);
                //// dieses FromPoints ist irgendwie instabil, ich würde es hier lieber nicht verwenden...
                //// i.A. liegen die 4 Punkte in einer Ebene (Zeichenebene). Wenn nicht, dann ist
                //// die Sache nicht eindeutig und das Ergebnis hier liefert eine der möglichen Lösungen
                //Invariant.Location = Dst[0];
                //ModOp move = Translate(Dst[0] - Src[0]);
                //GeoVector2D s1 = Invariant.Project(Src[1]) - Invariant.Project(Src[0]);
                //GeoVector2D d1 = Invariant.Project(Dst[1]) - GeoPoint2D.Origin;
                //if (s1.Length < Precision.eps || d1.Length < Precision.eps) throw new ModOpException("ModOp.Fit: unable to perform fit operation", ModOpException.tExceptionType.InvalidParameter);
                //SweepAngle alpha = new SweepAngle(s1, d1);
                //ModOp rot = Rotate(Dst[0], Invariant.Normal, alpha);
                //if (DoScale)
                //{
                //    double factor = d1.Length / s1.Length;
                //    ModOp scale = Scale(Dst[0], Dst[1] - Dst[0], factor);
                //    return scale * rot * move;
                //}
                //else
                //{
                //    return rot * move;
                //}
            }
            if (Src.Length == 3)
            {
                //GeoPoint [] AllPoints = new GeoPoint[Src.Length+Dst.Length];
                //Src.CopyTo(AllPoints,0);
                //Dst.CopyTo(AllPoints,Src.Length);
                //double MaxDist;
                //Plane Invariant = Plane.FromPoints(AllPoints,out MaxDist);
                // wir brauchen 3 Vektoren, die aufeinander abgebildet werden, der Parameter
                // liefert aber nur 2. deshalb Ebene suchen, deren Normalenvektor invariant bleibt
                // is eigentlich nicht ganz einzusehen, es gibt doch 3 Vektoren, sind schließlich 3 unabhängige Punkte

                // Nochmal: es sind 12 Unbekannte aber nur 9 Gleichungen, das Problem ist also unterbestimmt
                // Die Abblidung geht von einer Ebene in eine andere Ebene (oder meist sogar in dieselbe).
                // was mit Punkten außerhalb dieser Ebenen geschieht ist also unklar. Hier wird es jetzt so implementiert
                // dass der Normalenvektor der Quellebene zum Normalenvektor der Zielebene wird und
                // die Länge unverändert bleibt.
                GeoVector[] From = new GeoVector[3];
                GeoVector[] To = new GeoVector[3];
                From[0] = Src[1] - Src[0];
                From[1] = Src[2] - Src[0];
                From[2] = From[0] ^ From[1];
                From[2].Norm();
                To[0] = Dst[1] - Dst[0];
                To[1] = Dst[2] - Dst[0];
                To[2] = To[0] ^ To[1];
                To[2].Norm();
                ModOp f = Fit(From, To); // die macht bereits alles bis auf die Verschiebung
                GeoPoint p = f * Src[0];
                return Translate(Dst[0] - p) * f;
            }
            if (Src.Length == 4)
            {
                double[,] a = new double[12, 12];
                double[] b = new double[12];
                a[0, 0] = Src[0].x;
                a[0, 1] = Src[0].y;
                a[0, 2] = Src[0].z;
                a[0, 3] = 1.0;
                a[1, 4] = Src[0].x;
                a[1, 5] = Src[0].y;
                a[1, 6] = Src[0].z;
                a[1, 7] = 1.0;
                a[2, 8] = Src[0].x;
                a[2, 9] = Src[0].y;
                a[2, 10] = Src[0].z;
                a[2, 11] = 1.0;

                a[3, 0] = Src[1].x;
                a[3, 1] = Src[1].y;
                a[3, 2] = Src[1].z;
                a[3, 3] = 1.0;
                a[4, 4] = Src[1].x;
                a[4, 5] = Src[1].y;
                a[4, 6] = Src[1].z;
                a[4, 7] = 1.0;
                a[5, 8] = Src[1].x;
                a[5, 9] = Src[1].y;
                a[5, 10] = Src[1].z;
                a[5, 11] = 1.0;

                a[6, 0] = Src[2].x;
                a[6, 1] = Src[2].y;
                a[6, 2] = Src[2].z;
                a[6, 3] = 1.0;
                a[7, 4] = Src[2].x;
                a[7, 5] = Src[2].y;
                a[7, 6] = Src[2].z;
                a[7, 7] = 1.0;
                a[8, 8] = Src[2].x;
                a[8, 9] = Src[2].y;
                a[8, 10] = Src[2].z;
                a[8, 11] = 1.0;

                a[9, 0] = Src[3].x;
                a[9, 1] = Src[3].y;
                a[9, 2] = Src[3].z;
                a[9, 3] = 1.0;
                a[10, 4] = Src[3].x;
                a[10, 5] = Src[3].y;
                a[10, 6] = Src[3].z;
                a[10, 7] = 1.0;
                a[11, 8] = Src[3].x;
                a[11, 9] = Src[3].y;
                a[11, 10] = Src[3].z;
                a[11, 11] = 1.0;

                b[0] = Dst[0].x;
                b[1] = Dst[0].y;
                b[2] = Dst[0].z;
                b[3] = Dst[1].x;
                b[4] = Dst[1].y;
                b[5] = Dst[1].z;
                b[6] = Dst[2].x;
                b[7] = Dst[2].y;
                b[8] = Dst[2].z;
                b[9] = Dst[3].x;
                b[10] = Dst[3].y;
                b[11] = Dst[3].z;
                Vector x = (Vector)DenseMatrix.OfArray(a).Solve(new DenseVector(b));
                if (!x.IsValid()) throw new ModOpException("ModOp.Fit: unable to perform fit", ModOpException.tExceptionType.InvalidParameter);
                ModOp res = ModOp.Identity;
                res.Matrix00 = x[0];
                res.Matrix01 = x[1];
                res.Matrix02 = x[2];
                res.Matrix03 = x[3];
                res.Matrix10 = x[4];
                res.Matrix11 = x[5];
                res.Matrix12 = x[6];
                res.Matrix13 = x[7];
                res.Matrix20 = x[8];
                res.Matrix21 = x[9];
                res.Matrix22 = x[10];
                res.Matrix23 = x[11];
                res.mode = ModificationMode.Other;
                return res;

                //GeoVector[] From = new GeoVector[3];
                //GeoVector[] To = new GeoVector[3];
                //From[0] = Src[1] - Src[0];
                //From[1] = Src[2] - Src[0];
                //From[2] = Src[3] - Src[0];
                //To[0] = Dst[1] - Dst[0];
                //To[1] = Dst[2] - Dst[0];
                //To[2] = Dst[3] - Dst[0];
                //ModOp f = Fit(From, To); // die macht bereits alles bis auf die Verschiebung
                //GeoPoint p = f * Src[0];
                //return Translate(Dst[0] - p) * f;
            }
            throw new NotImplementedException();
        }
        public static ModOp Fit(GeoPoint srcLoc, GeoVector[] src, GeoPoint dstLoc, GeoVector[] dst)
        {
            return Translate(dstLoc - GeoPoint.Origin) * Fit(src, dst) * Translate(GeoPoint.Origin - srcLoc);
        }
        internal static ModOp Fit(FreeCoordSys src, FreeCoordSys dst)
        {
            return Fit(src.Location, new GeoVector[] { src.DirectionX, src.DirectionY, src.DirectionZ }, dst.Location, new GeoVector[] { dst.DirectionX, dst.DirectionY, dst.DirectionZ });
        }
        /// <summary>
        /// Combines two modification into one modification. If two modifications have to be applied to
        /// several <see cref="GeoPoint"/>s or <see cref="GeoVector"/>s or to <see cref="GeoObject.IGeoObject"/>s
        /// it is faster to use the combination.
        /// </summary>
        /// <param name="lhs">second modification</param>
        /// <param name="rhs">first modification</param>
        /// <returns>the combination</returns>
        public static ModOp operator *(ModOp lhs, ModOp rhs)
        {
            ModOp res;
            res.Matrix00 = lhs.Matrix00 * rhs.Matrix00 + lhs.Matrix01 * rhs.Matrix10 + lhs.Matrix02 * rhs.Matrix20;
            res.Matrix01 = lhs.Matrix00 * rhs.Matrix01 + lhs.Matrix01 * rhs.Matrix11 + lhs.Matrix02 * rhs.Matrix21;
            res.Matrix02 = lhs.Matrix00 * rhs.Matrix02 + lhs.Matrix01 * rhs.Matrix12 + lhs.Matrix02 * rhs.Matrix22;
            res.Matrix03 = lhs.Matrix00 * rhs.Matrix03 + lhs.Matrix01 * rhs.Matrix13 + lhs.Matrix02 * rhs.Matrix23 + lhs.Matrix03;
            res.Matrix10 = lhs.Matrix10 * rhs.Matrix00 + lhs.Matrix11 * rhs.Matrix10 + lhs.Matrix12 * rhs.Matrix20;
            res.Matrix11 = lhs.Matrix10 * rhs.Matrix01 + lhs.Matrix11 * rhs.Matrix11 + lhs.Matrix12 * rhs.Matrix21;
            res.Matrix12 = lhs.Matrix10 * rhs.Matrix02 + lhs.Matrix11 * rhs.Matrix12 + lhs.Matrix12 * rhs.Matrix22;
            res.Matrix13 = lhs.Matrix10 * rhs.Matrix03 + lhs.Matrix11 * rhs.Matrix13 + lhs.Matrix12 * rhs.Matrix23 + lhs.Matrix13;
            res.Matrix20 = lhs.Matrix20 * rhs.Matrix00 + lhs.Matrix21 * rhs.Matrix10 + lhs.Matrix22 * rhs.Matrix20;
            res.Matrix21 = lhs.Matrix20 * rhs.Matrix01 + lhs.Matrix21 * rhs.Matrix11 + lhs.Matrix22 * rhs.Matrix21;
            res.Matrix22 = lhs.Matrix20 * rhs.Matrix02 + lhs.Matrix21 * rhs.Matrix12 + lhs.Matrix22 * rhs.Matrix22;
            res.Matrix23 = lhs.Matrix20 * rhs.Matrix03 + lhs.Matrix21 * rhs.Matrix13 + lhs.Matrix22 * rhs.Matrix23 + lhs.Matrix23;
            res.ScalingFactor = lhs.ScalingFactor * rhs.ScalingFactor;
            switch (lhs.mode)
            {
                case ModificationMode.Other:
                    res.mode = ModificationMode.Other;
                    break;
                case ModificationMode.Identity:
                    res.mode = rhs.mode;
                    break;
                case ModificationMode.Translation:
                    switch (rhs.mode)
                    {
                        case ModificationMode.Identity:
                        case ModificationMode.Translation:
                            res.mode = ModificationMode.Translation;
                            break;
                        case ModificationMode.Other:
                            res.mode = ModificationMode.Other;
                            break;
                        default:
                            res.mode = ModificationMode.CompoundTrsf;
                            break;
                    }
                    break;
                case ModificationMode.Scale:
                    switch (rhs.mode)
                    {
                        case ModificationMode.Identity:
                        case ModificationMode.Scale:
                            res.mode = ModificationMode.Scale;
                            break;
                        case ModificationMode.Other:
                            res.mode = ModificationMode.Other;
                            break;
                        default:
                            res.mode = ModificationMode.CompoundTrsf;
                            break;
                    }
                    break;

                case ModificationMode.Rotation:
                case ModificationMode.PntMirror:
                case ModificationMode.Ax1Mirror:
                case ModificationMode.Ax2Mirror:
                case ModificationMode.CompoundTrsf:
                default:
                    switch (rhs.mode)
                    {
                        case ModificationMode.Identity:
                            res.mode = lhs.mode;
                            break;
                        case ModificationMode.Other:
                            res.mode = ModificationMode.Other;
                            break;
                        default:
                            res.mode = ModificationMode.CompoundTrsf;
                            break;
                    }
                    break;
            }
            return res;
        }
        /// <summary>
        /// Returns the inverse of this modification. 
        /// </summary>
        /// <returns>inverse</returns>
        public ModOp GetInverse()
        {
            // see http://mathworld.wolfram.com/MatrixInverse.html
            double d = Determinant;
            if (Math.Abs(d) < 1e-32)
                throw new ModOpException(ModOpException.tExceptionType.InversionFailed);
            try
            {
                ModOp res = new ModOp(
                    ((Matrix11 * Matrix22) - (Matrix12 * Matrix21)) / d,
                    ((Matrix02 * Matrix21) - (Matrix01 * Matrix22)) / d,
                    ((Matrix01 * Matrix12) - (Matrix02 * Matrix11)) / d,
                    0,
                    ((Matrix12 * Matrix20) - (Matrix10 * Matrix22)) / d,
                    ((Matrix00 * Matrix22) - (Matrix02 * Matrix20)) / d,
                    ((Matrix02 * Matrix10) - (Matrix00 * Matrix12)) / d,
                    0,
                    ((Matrix10 * Matrix21) - (Matrix11 * Matrix20)) / d,
                    ((Matrix01 * Matrix20) - (Matrix00 * Matrix21)) / d,
                    ((Matrix00 * Matrix11) - (Matrix01 * Matrix10)) / d,
                    0);
                // Translation: must reverse the translation of the origin
                res.Matrix03 = -(res.Matrix00 * Matrix03 + res.Matrix01 * Matrix13 + res.Matrix02 * Matrix23);
                res.Matrix13 = -(res.Matrix10 * Matrix03 + res.Matrix11 * Matrix13 + res.Matrix12 * Matrix23);
                res.Matrix23 = -(res.Matrix20 * Matrix03 + res.Matrix21 * Matrix13 + res.Matrix22 * Matrix23);
                res.mode = mode; // reflection, rotation, translation mode is not change by inversion
                return res;
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
                throw new ModOpException(ModOpException.tExceptionType.InversionFailed);
            }
        }
        internal System.Drawing.Drawing2D.Matrix Matrix2D
        {
            get
            {
                return new System.Drawing.Drawing2D.Matrix((float)Matrix00, (float)Matrix01, (float)Matrix10, (float)Matrix11, (float)Matrix12, (float)Matrix02);
            }
        }
        /// <summary>
        /// Returns the determinant of the matrix of this modification.
        /// </summary>
        public double Determinant
        {
            get
            {
                return Matrix00 * Matrix11 * Matrix22 - Matrix00 * Matrix12 * Matrix21 - Matrix01 * Matrix10 * Matrix22
                    + Matrix01 * Matrix12 * Matrix20 + Matrix02 * Matrix10 * Matrix21 - Matrix02 * Matrix11 * Matrix20;
            }
        }
        /// <summary>
        /// Gets the orientation of this ModOp. True means orientation is preserved (e.g. lefthanded yields lefthanded), false
        /// means orientation is reversed (lefthanded yields righthanded and vice versa) when this ModOp is applied to vectors.
        /// </summary>
        public bool Oriented
        {
            get
            {
                GeoVector dirx = new GeoVector(Matrix00, Matrix01, Matrix02);
                GeoVector diry = new GeoVector(Matrix10, Matrix11, Matrix12);
                GeoVector dirz = new GeoVector(Matrix20, Matrix21, Matrix22);
                return (dirx ^ diry) * dirz > 0.0; // this was wrong fixed: 22.2.19
            }
        }
        /// <summary>
        /// Returns true if orthogonal vectors stay orthogonal after transformation and no scaling is performed
        /// </summary>
        public bool IsOrthogonal
        {
            get
            {
                if (Math.Abs(Math.Abs(Determinant) - 1) > 1e-10) return false;
                if (!Precision.IsPerpendicular(new GeoVector(Matrix00, Matrix10, Matrix20), new GeoVector(Matrix01, Matrix11, Matrix21), false)) return false;
                if (!Precision.IsPerpendicular(new GeoVector(Matrix00, Matrix10, Matrix20), new GeoVector(Matrix02, Matrix12, Matrix22), false)) return false;
                if (!Precision.IsPerpendicular(new GeoVector(Matrix02, Matrix12, Matrix22), new GeoVector(Matrix01, Matrix11, Matrix21), false)) return false;
                return true;
            }
        }
        /// <summary>
        /// Same as <see cref="IsOrthogonal"/> but with scaling allowed
        /// </summary>
        public bool IsIsogonal
        {
            get
            {
                if (!Precision.IsPerpendicular(new GeoVector(Matrix00, Matrix10, Matrix20), new GeoVector(Matrix01, Matrix11, Matrix21), false)) return false;
                if (!Precision.IsPerpendicular(new GeoVector(Matrix00, Matrix10, Matrix20), new GeoVector(Matrix02, Matrix12, Matrix22), false)) return false;
                if (!Precision.IsPerpendicular(new GeoVector(Matrix02, Matrix12, Matrix22), new GeoVector(Matrix01, Matrix11, Matrix21), false)) return false;
                return true;
            }
        }
        /// <summary>
        /// Returns true for ModOps that are 0.0 in all components. Uninitialized ModOp objects will return true.
        /// Usually used for a test, whether it has been initialized, since a 0 ModOp shouldn't occur in normal circumstances.
        /// </summary>
        public bool IsNull
        {
            get
            {
                if (Matrix00 != 0.0) return false;
                if (Matrix01 != 0.0) return false;
                if (Matrix02 != 0.0) return false;
                if (Matrix03 != 0.0) return false;
                if (Matrix10 != 0.0) return false;
                if (Matrix11 != 0.0) return false;
                if (Matrix12 != 0.0) return false;
                if (Matrix13 != 0.0) return false;
                if (Matrix20 != 0.0) return false;
                if (Matrix21 != 0.0) return false;
                if (Matrix22 != 0.0) return false;
                if (Matrix23 != 0.0) return false;
                return true;
            }
        }
        public bool IsValid
        {
            get
            {
                double[,] m = Matrix;
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        if (double.IsNaN(m[i, j])) return false;
                    }
                }
                return true;
            }
        }
        /// <summary>
        /// Returns the translation vector of this ModOp
        /// </summary>
        public GeoVector Translation
        {
            get
            {
                return new GeoVector(Matrix03, Matrix13, Matrix23);
            }
            set
            {
                Matrix03 = value.x;
                Matrix13 = value.y;
                Matrix23 = value.z;
            }
        }
        public ModOp2D To2D()
        {
            return new ModOp2D(Matrix00, Matrix01, Matrix03, Matrix10, Matrix11, Matrix13);
        }
        /// <summary>
        /// Modifies the given point by this modification.
        /// </summary>
        /// <param name="m">modification</param>
        /// <param name="p">point</param>
        /// <returns>modified point</returns>
        public static GeoPoint operator *(ModOp m, GeoPoint p)
        {
            if (m.mode == ModificationMode.Identity) return p;
            else if (m.mode == ModificationMode.Translation)
            {
                return new GeoPoint(p.x + m.Matrix03, p.y + m.Matrix13, p.z + m.Matrix23);
            }
            else
            {
                return new GeoPoint(m.Matrix00 * p.x + m.Matrix01 * p.y + m.Matrix02 * p.z + m.Matrix03,
                                    m.Matrix10 * p.x + m.Matrix11 * p.y + m.Matrix12 * p.z + m.Matrix13,
                                    m.Matrix20 * p.x + m.Matrix21 * p.y + m.Matrix22 * p.z + m.Matrix23);
            }

        }
        /// <summary>
        /// Modifies the given 2d point by this modification. The point is assumed in the x/y plane
        /// </summary>
        /// <param name="m">modification</param>
        /// <param name="p">point</param>
        /// <returns>modified point</returns>
        public static GeoPoint operator *(ModOp m, GeoPoint2D p)
        {
            if (m.mode == ModificationMode.Identity) return new GeoPoint(p);
            else if (m.mode == ModificationMode.Translation)
            {
                return new GeoPoint(p.x + m.Matrix03, p.y + m.Matrix13, m.Matrix23);
            }
            else
            {
                return new GeoPoint(m.Matrix00 * p.x + m.Matrix01 * p.y + m.Matrix03,
                                    m.Matrix10 * p.x + m.Matrix11 * p.y + m.Matrix13,
                                    m.Matrix20 * p.x + m.Matrix21 * p.y + m.Matrix23);
            }

        }
        /// <summary>
        /// Modifies the given vector by this modification
        /// </summary>
        /// <param name="m">modification</param>
        /// <param name="v">vector</param>
        /// <returns>modified vector</returns>
        public static GeoVector operator *(ModOp m, GeoVector v)
        {
            if (m.mode == ModificationMode.Identity) return v;
            else if (m.mode == ModificationMode.Translation)
            {
                return v;
            }
            else
            {
                return new GeoVector(m.Matrix00 * v.x + m.Matrix01 * v.y + m.Matrix02 * v.z,
                                     m.Matrix10 * v.x + m.Matrix11 * v.y + m.Matrix12 * v.z,
                                     m.Matrix20 * v.x + m.Matrix21 * v.y + m.Matrix22 * v.z);
            }

        }
        /// <summary>
        /// Modifies the given 2d vector by this modification, The vector is assumed in the x/y plane
        /// </summary>
        /// <param name="m">modification</param>
        /// <param name="v">vector</param>
        /// <returns>modified vector</returns>
        public static GeoVector operator *(ModOp m, GeoVector2D v)
        {
            if (m.mode == ModificationMode.Identity) return new GeoVector(v);
            else if (m.mode == ModificationMode.Translation)
            {
                return new GeoVector(v);
            }
            else
            {
                return new GeoVector(m.Matrix00 * v.x + m.Matrix01 * v.y,
                    m.Matrix10 * v.x + m.Matrix11 * v.y,
                    m.Matrix20 * v.x + m.Matrix21 * v.y);
            }

        }
        /// <summary>
        /// Multiplies the double value by the scaling factor of this modification
        /// </summary>
        /// <param name="m">modification</param>
        /// <param name="d">input value</param>
        /// <returns>modified value</returns>
        public static double operator *(ModOp m, double d)
        {
            return m.ScalingFactor * d;
        }
        public static Plane operator *(ModOp m, Plane pln)
        {
            Plane res = new Plane(pln);
            res.Modify(m);
            return res;
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        ModOp(SerializationInfo info, StreamingContext context)
        {
            Matrix00 = (double)info.GetValue("Matrix00", typeof(double));
            Matrix01 = (double)info.GetValue("Matrix01", typeof(double));
            Matrix02 = (double)info.GetValue("Matrix02", typeof(double));
            Matrix03 = (double)info.GetValue("Matrix03", typeof(double));
            Matrix10 = (double)info.GetValue("Matrix10", typeof(double));
            Matrix11 = (double)info.GetValue("Matrix11", typeof(double));
            Matrix12 = (double)info.GetValue("Matrix12", typeof(double));
            Matrix13 = (double)info.GetValue("Matrix13", typeof(double));
            Matrix20 = (double)info.GetValue("Matrix20", typeof(double));
            Matrix21 = (double)info.GetValue("Matrix21", typeof(double));
            Matrix22 = (double)info.GetValue("Matrix22", typeof(double));
            Matrix23 = (double)info.GetValue("Matrix23", typeof(double));
            ScalingFactor = (double)info.GetValue("ScalingFactor", typeof(double));
            mode = (ModificationMode)info.GetValue("Mode", typeof(ModificationMode));
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Matrix00", Matrix00, typeof(double));
            info.AddValue("Matrix01", Matrix01, typeof(double));
            info.AddValue("Matrix02", Matrix02, typeof(double));
            info.AddValue("Matrix03", Matrix03, typeof(double));
            info.AddValue("Matrix10", Matrix10, typeof(double));
            info.AddValue("Matrix11", Matrix11, typeof(double));
            info.AddValue("Matrix12", Matrix12, typeof(double));
            info.AddValue("Matrix13", Matrix13, typeof(double));
            info.AddValue("Matrix20", Matrix20, typeof(double));
            info.AddValue("Matrix21", Matrix21, typeof(double));
            info.AddValue("Matrix22", Matrix22, typeof(double));
            info.AddValue("Matrix23", Matrix23, typeof(double));
            info.AddValue("ScalingFactor", ScalingFactor, typeof(double));
            info.AddValue("Mode", mode, typeof(ModificationMode));
        }
        public ModOp(IJsonReadStruct data)
        {
            Matrix00 = data.GetValue<double>();
            Matrix01 = data.GetValue<double>();
            Matrix02 = data.GetValue<double>();
            Matrix03 = data.GetValue<double>();
            Matrix10 = data.GetValue<double>();
            Matrix11 = data.GetValue<double>();
            Matrix12 = data.GetValue<double>();
            Matrix13 = data.GetValue<double>();
            Matrix20 = data.GetValue<double>();
            Matrix21 = data.GetValue<double>();
            Matrix22 = data.GetValue<double>();
            Matrix23 = data.GetValue<double>();
            ScalingFactor = data.GetValue<double>();
            mode = data.GetValue<ModificationMode>();
        }

        public void GetObjectData(IJsonWriteData data)
        {
            data.AddValues(Matrix00, Matrix01, Matrix02, Matrix03, Matrix10, Matrix11, Matrix12, Matrix13, Matrix20, Matrix21, Matrix22, Matrix23, ScalingFactor, mode);
        }

        public void SetObjectData(IJsonReadData data)
        {
        }
        #endregion

        //internal double Item(int i, int j)
        //{
        //    switch (i * 4 + j)
        //    {
        //        case 0:
        //            return Matrix00;
        //        case 1:
        //            return Matrix01;
        //        case 2:
        //            return Matrix02;
        //        case 3:
        //            return Matrix03;
        //        case 4:
        //            return Matrix10;
        //        case 5:
        //            return Matrix11;
        //        case 6:
        //            return Matrix12;
        //        case 7:
        //            return Matrix13;
        //        case 8:
        //            return Matrix20;
        //        case 9:
        //            return Matrix21;
        //        case 10:
        //            return Matrix22;
        //        case 11:
        //            return Matrix23;
        //        default:
        //            throw (new ModOpException(ModOpException.tExceptionType.InvalidParameter));
        //    }
        //}

        public double this[int i, int j]
        {
            get
            {
                switch (i * 4 + j)
                {
                    case 0:
                        return Matrix00;
                    case 1:
                        return Matrix01;
                    case 2:
                        return Matrix02;
                    case 3:
                        return Matrix03;
                    case 4:
                        return Matrix10;
                    case 5:
                        return Matrix11;
                    case 6:
                        return Matrix12;
                    case 7:
                        return Matrix13;
                    case 8:
                        return Matrix20;
                    case 9:
                        return Matrix21;
                    case 10:
                        return Matrix22;
                    case 11:
                        return Matrix23;
                    default:
                        throw (new ModOpException(ModOpException.tExceptionType.InvalidParameter));
                }
            }
            set
            {
                switch (i * 4 + j)
                {
                    case 0:
                        Matrix00 = value;
                        break;
                    case 1:
                        Matrix01 = value;
                        break;
                    case 2:
                        Matrix02 = value;
                        break;
                    case 3:
                        Matrix03 = value;
                        break;
                    case 4:
                        Matrix10 = value;
                        break;
                    case 5:
                        Matrix11 = value;
                        break;
                    case 6:
                        Matrix12 = value;
                        break;
                    case 7:
                        Matrix13 = value;
                        break;
                    case 8:
                        Matrix20 = value;
                        break;
                    case 9:
                        Matrix21 = value;
                        break;
                    case 10:
                        Matrix22 = value;
                        break;
                    case 11:
                        Matrix23 = value;
                        break;
                    default:
                        throw (new ModOpException(ModOpException.tExceptionType.InvalidParameter));
                }
            }
        }
        internal void SetData(Matrix m, GeoPoint loc)
        {   // eine affine Abbildung und eine Translation
            Matrix00 = m[0, 0];
            Matrix01 = m[0, 1];
            Matrix02 = m[0, 2];
            Matrix03 = loc.x;
            Matrix10 = m[1, 0];
            Matrix11 = m[1, 1];
            Matrix12 = m[1, 2];
            Matrix13 = loc.y;
            Matrix20 = m[2, 0];
            Matrix21 = m[2, 1];
            Matrix22 = m[2, 2];
            Matrix23 = loc.z;
            mode = ModificationMode.CompoundTrsf;
        }

        internal bool IsIdentity(double prec)
        {
            if (Math.Abs(1 - Matrix00) > prec) return false;
            if (Math.Abs(1 - Matrix11) > prec) return false;
            if (Math.Abs(1 - Matrix22) > prec) return false;
            if (Math.Abs(Matrix01) > prec) return false;
            if (Math.Abs(Matrix02) > prec) return false;
            if (Math.Abs(Matrix10) > prec) return false;
            if (Math.Abs(Matrix12) > prec) return false;
            if (Math.Abs(Matrix20) > prec) return false;
            if (Math.Abs(Matrix21) > prec) return false;
            if (Math.Abs(Matrix03) > prec) return false;
            if (Math.Abs(Matrix13) > prec) return false;
            if (Math.Abs(Matrix23) > prec) return false;
            return true;
        }
    }
}
