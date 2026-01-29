using System;
using System.Collections.Generic;

namespace CSMath
{
	public partial struct Matrix4
	{
		/// <summary>
		/// Multiplies two matrices.
		/// </summary>
		/// <returns>A new instance containing the result.</returns>
		public static Matrix4 Multiply(Matrix4 a, Matrix4 b)
		{
			Matrix4 result = new Matrix4();

			var rows = a.GetRows();
			var cols = b.GetCols();

			result.m00 = rows[0].Dot(cols[0]);
			result.m10 = rows[0].Dot(cols[1]);
			result.m20 = rows[0].Dot(cols[2]);
			result.m30 = rows[0].Dot(cols[3]);
			result.m01 = rows[1].Dot(cols[0]);
			result.m11 = rows[1].Dot(cols[1]);
			result.m21 = rows[1].Dot(cols[2]);
			result.m31 = rows[1].Dot(cols[3]);
			result.m02 = rows[2].Dot(cols[0]);
			result.m12 = rows[2].Dot(cols[1]);
			result.m22 = rows[2].Dot(cols[2]);
			result.m32 = rows[2].Dot(cols[3]);
			result.m03 = rows[3].Dot(cols[0]);
			result.m13 = rows[3].Dot(cols[1]);
			result.m23 = rows[3].Dot(cols[2]);
			result.m33 = rows[3].Dot(cols[3]);

			return result;
		}

		/// <summary>
		/// Multiplies two matrices.
		/// </summary>
		/// <returns>A new instance containing the result.</returns>
		public static Matrix4 operator *(Matrix4 a, Matrix4 b)
		{
			return Matrix4.Multiply(a, b);
		}

		/// <summary>Multiply the matrix and a coordinate</summary>
		/// <param name="matrix"></param>
		/// <param name="value"></param>
		/// <returns>Result matrix</returns>
		public static XYZ operator *(Matrix4 matrix, XYZ value)
		{
			XYZM xyzm = new XYZM(value.X, value.Y, value.Z, 1);
			List<XYZM> rows = matrix.GetRows();

			XYZ result = new XYZ
			{
				X = rows[0].Dot(xyzm),
				Y = rows[1].Dot(xyzm),
				Z = rows[2].Dot(xyzm)
			};

			return result;
		}

		/// <summary>Multiply the matrix and XYZM</summary>
		/// <param name="matrix"></param>
		/// <param name="v"></param>
		/// <returns>Result matrix</returns>
		public static XYZM operator *(Matrix4 matrix, XYZM v)
		{
			return new XYZM(
				matrix.m00 * v.X + matrix.m10 * v.Y + matrix.m20 * v.Z + matrix.m30 * v.M,
				matrix.m01 * v.X + matrix.m11 * v.Y + matrix.m21 * v.Z + matrix.m31 * v.M,
				matrix.m02 * v.X + matrix.m12 * v.Y + matrix.m22 * v.Z + matrix.m32 * v.M,
				matrix.m03 * v.X + matrix.m13 * v.Y + matrix.m23 * v.Z + matrix.m33 * v.M);
		}
	}
}
