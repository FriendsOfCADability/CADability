using System;

namespace CADability
{
    /// <summary>
    /// Modern matrix utilities for OpenGL 2.1+ shader-based rendering
    /// </summary>
    /// <remarks>
    /// Provides utilities for working with 4x4 matrices used in shaders.
    /// Separates from fixed-pipeline matrix operations (glMatrixMode, glLoadMatrix).
    /// </remarks>
    public static class GLMatrix
    {
        /// <summary>
        /// Creates a 4x4 identity matrix
        /// </summary>
        public static double[] Identity()
        {
            return new double[16]
            {
                1, 0, 0, 0,
                0, 1, 0, 0,
                0, 0, 1, 0,
                0, 0, 0, 1
            };
        }

        /// <summary>
        /// Creates a translation matrix
        /// </summary>
        public static double[] Translation(double x, double y, double z)
        {
            var m = Identity();
            m[12] = x;
            m[13] = y;
            m[14] = z;
            return m;
        }

        /// <summary>
        /// Creates a scale matrix
        /// </summary>
        public static double[] Scale(double x, double y, double z)
        {
            return new double[16]
            {
                x, 0, 0, 0,
                0, y, 0, 0,
                0, 0, z, 0,
                0, 0, 0, 1
            };
        }

        /// <summary>
        /// Creates a rotation matrix around the X axis (in radians)
        /// </summary>
        public static double[] RotationX(double radians)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);

            return new double[16]
            {
                1, 0,  0, 0,
                0, c, -s, 0,
                0, s,  c, 0,
                0, 0,  0, 1
            };
        }

        /// <summary>
        /// Creates a rotation matrix around the Y axis (in radians)
        /// </summary>
        public static double[] RotationY(double radians)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);

            return new double[16]
            {
                 c, 0, s, 0,
                 0, 1, 0, 0,
                -s, 0, c, 0,
                 0, 0, 0, 1
            };
        }

        /// <summary>
        /// Creates a rotation matrix around the Z axis (in radians)
        /// </summary>
        public static double[] RotationZ(double radians)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);

            return new double[16]
            {
                c, -s, 0, 0,
                s,  c, 0, 0,
                0,  0, 1, 0,
                0,  0, 0, 1
            };
        }

        /// <summary>
        /// Multiplies two 4x4 matrices
        /// </summary>
        public static double[] Multiply(double[] a, double[] b)
        {
            if (a == null || b == null || a.Length != 16 || b.Length != 16)
            {
                throw new ArgumentException("Matrices must be 4x4 (16 elements)");
            }

            double[] result = new double[16];

            // Standard matrix multiplication
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    int i = row * 4 + col;
                    result[i] = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        result[i] += a[row * 4 + k] * b[k * 4 + col];
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Calculates the determinant of a 4x4 matrix
        /// </summary>
        public static double Determinant(double[] m)
        {
            if (m == null || m.Length != 16)
            {
                throw new ArgumentException("Matrix must be 4x4 (16 elements)");
            }

            // Use Laplace expansion for 4x4 determinant
            double det = 0;

            det += m[0] * (m[5] * (m[10] * m[15] - m[14] * m[11]) - m[9] * (m[6] * m[15] - m[14] * m[7]) + m[13] * (m[6] * m[11] - m[10] * m[7]));
            det -= m[4] * (m[1] * (m[10] * m[15] - m[14] * m[11]) - m[9] * (m[2] * m[15] - m[14] * m[3]) + m[13] * (m[2] * m[11] - m[10] * m[3]));
            det += m[8] * (m[1] * (m[6] * m[15] - m[14] * m[7]) - m[5] * (m[2] * m[15] - m[14] * m[3]) + m[13] * (m[2] * m[7] - m[6] * m[3]));
            det -= m[12] * (m[1] * (m[6] * m[11] - m[10] * m[7]) - m[5] * (m[2] * m[11] - m[10] * m[3]) + m[9] * (m[2] * m[7] - m[6] * m[3]));

            return det;
        }

        /// <summary>
        /// Inverts a 4x4 matrix
        /// </summary>
        public static double[] Invert(double[] m)
        {
            if (m == null || m.Length != 16)
            {
                throw new ArgumentException("Matrix must be 4x4 (16 elements)");
            }

            double det = Determinant(m);
            if (Math.Abs(det) < 1e-10)
            {
                throw new InvalidOperationException("Matrix is singular and cannot be inverted");
            }

            double[] result = new double[16];

            // Calculate inverse using adjugate matrix
            result[0] = (m[5] * (m[10] * m[15] - m[14] * m[11]) - m[9] * (m[6] * m[15] - m[14] * m[7]) + m[13] * (m[6] * m[11] - m[10] * m[7])) / det;
            result[4] = -(m[4] * (m[10] * m[15] - m[14] * m[11]) - m[8] * (m[6] * m[15] - m[14] * m[7]) + m[12] * (m[6] * m[11] - m[10] * m[7])) / det;
            result[8] = (m[4] * (m[9] * m[15] - m[13] * m[11]) - m[8] * (m[5] * m[15] - m[13] * m[7]) + m[12] * (m[5] * m[11] - m[9] * m[7])) / det;
            result[12] = -(m[4] * (m[9] * m[14] - m[13] * m[10]) - m[8] * (m[5] * m[14] - m[13] * m[6]) + m[12] * (m[5] * m[10] - m[9] * m[6])) / det;

            result[1] = -(m[1] * (m[10] * m[15] - m[14] * m[11]) - m[9] * (m[2] * m[15] - m[14] * m[3]) + m[13] * (m[2] * m[11] - m[10] * m[3])) / det;
            result[5] = (m[0] * (m[10] * m[15] - m[14] * m[11]) - m[8] * (m[2] * m[15] - m[14] * m[3]) + m[12] * (m[2] * m[11] - m[10] * m[3])) / det;
            result[9] = -(m[0] * (m[9] * m[15] - m[13] * m[11]) - m[8] * (m[2] * m[15] - m[13] * m[3]) + m[12] * (m[2] * m[11] - m[9] * m[3])) / det;
            result[13] = (m[0] * (m[9] * m[14] - m[13] * m[10]) - m[8] * (m[2] * m[14] - m[13] * m[2]) + m[12] * (m[2] * m[10] - m[9] * m[6])) / det;

            result[2] = (m[1] * (m[6] * m[15] - m[14] * m[7]) - m[5] * (m[2] * m[15] - m[14] * m[3]) + m[13] * (m[2] * m[7] - m[6] * m[3])) / det;
            result[6] = -(m[0] * (m[6] * m[15] - m[14] * m[7]) - m[4] * (m[2] * m[15] - m[14] * m[3]) + m[12] * (m[2] * m[7] - m[6] * m[3])) / det;
            result[10] = (m[0] * (m[5] * m[15] - m[13] * m[7]) - m[4] * (m[2] * m[15] - m[13] * m[3]) + m[12] * (m[2] * m[7] - m[5] * m[3])) / det;
            result[14] = -(m[0] * (m[5] * m[14] - m[13] * m[6]) - m[4] * (m[2] * m[14] - m[13] * m[2]) + m[12] * (m[2] * m[6] - m[5] * m[2])) / det;

            result[3] = -(m[1] * (m[6] * m[11] - m[10] * m[7]) - m[5] * (m[2] * m[11] - m[10] * m[3]) + m[9] * (m[2] * m[7] - m[6] * m[3])) / det;
            result[7] = (m[0] * (m[6] * m[11] - m[10] * m[7]) - m[4] * (m[2] * m[11] - m[10] * m[3]) + m[8] * (m[2] * m[7] - m[6] * m[3])) / det;
            result[11] = -(m[0] * (m[5] * m[11] - m[9] * m[7]) - m[4] * (m[2] * m[11] - m[9] * m[3]) + m[8] * (m[2] * m[7] - m[5] * m[3])) / det;
            result[15] = (m[0] * (m[5] * m[10] - m[9] * m[6]) - m[4] * (m[2] * m[10] - m[9] * m[2]) + m[8] * (m[2] * m[6] - m[5] * m[2])) / det;

            return result;
        }

        /// <summary>
        /// Creates an orthographic projection matrix
        /// </summary>
        public static double[] Ortho(double left, double right, double bottom, double top, double near, double far)
        {
            double[] m = new double[16];

            m[0] = 2.0 / (right - left);
            m[5] = 2.0 / (top - bottom);
            m[10] = -2.0 / (far - near);
            m[12] = -(right + left) / (right - left);
            m[13] = -(top + bottom) / (top - bottom);
            m[14] = -(far + near) / (far - near);
            m[15] = 1.0;

            return m;
        }

        /// <summary>
        /// Creates a perspective projection matrix
        /// </summary>
        public static double[] Perspective(double fovy, double aspect, double near, double far)
        {
            double f = 1.0 / Math.Tan(fovy / 2.0);
            double[] m = new double[16];

            m[0] = f / aspect;
            m[5] = f;
            m[10] = (far + near) / (near - far);
            m[11] = -1.0;
            m[14] = (2.0 * far * near) / (near - far);

            return m;
        }

        /// <summary>
        /// Creates a "look at" view matrix
        /// </summary>
        public static double[] LookAt(double eyeX, double eyeY, double eyeZ,
                                      double centerX, double centerY, double centerZ,
                                      double upX, double upY, double upZ)
        {
            // Forward = normalize(center - eye)
            double fX = centerX - eyeX, fY = centerY - eyeY, fZ = centerZ - eyeZ;
            double fLen = Math.Sqrt(fX * fX + fY * fY + fZ * fZ);
            fX /= fLen; fY /= fLen; fZ /= fLen;

            // Side = normalize(forward x up)
            double sX = fY * upZ - fZ * upY;
            double sY = fZ * upX - fX * upZ;
            double sZ = fX * upY - fY * upX;
            double sLen = Math.Sqrt(sX * sX + sY * sY + sZ * sZ);
            sX /= sLen; sY /= sLen; sZ /= sLen;

            // Up = side x forward
            double uX = sY * fZ - sZ * fY;
            double uY = sZ * fX - sX * fZ;
            double uZ = sX * fY - sY * fX;

            // Build matrix
            double[] m = new double[16];
            m[0] = sX; m[4] = sY; m[8] = sZ; m[12] = 0;
            m[1] = uX; m[5] = uY; m[9] = uZ; m[13] = 0;
            m[2] = -fX; m[6] = -fY; m[10] = -fZ; m[14] = 0;
            m[3] = 0; m[7] = 0; m[11] = 0; m[15] = 1;

            double[] trans = Translation(-eyeX, -eyeY, -eyeZ);
            return Multiply(m, trans);
        }
    }
}
