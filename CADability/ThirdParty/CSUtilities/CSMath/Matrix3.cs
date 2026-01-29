using System;
using System.Text;
using System.Threading;

namespace CSMath
{
	public partial struct Matrix3
	{
		/// <summary>
		/// 4-dimensional zero matrix.
		/// </summary>
		public static readonly Matrix3 Zero = new Matrix3(
			0.0, 0.0, 0.0,
			0.0, 0.0, 0.0,
			0.0, 0.0, 0.0);

		/// <summary>
		/// 4-dimensional identity matrix.
		/// </summary>
		public static readonly Matrix3 Identity = new Matrix3(
			1.0, 0.0, 0.0,
			0.0, 1.0, 0.0,
			0.0, 0.0, 1.0);

		#region Public Fields

		/// <summary>
		/// Value at column 0, row 0 of the matrix.
		/// </summary>
		public double m00;
		/// <summary>
		/// Value at column 0, row 1 of the matrix.
		/// </summary>
		public double m01;
		/// <summary>
		/// Value at column 0, row 2 of the matrix.
		/// </summary>
		public double m02;

		/// <summary>
		/// Value at column 1, row 0 of the matrix.
		/// </summary>
		public double m10;
		/// <summary>
		/// Value at column 1, row 1 of the matrix.
		/// </summary>
		public double m11;
		/// <summary>
		/// Value at column 1, row 2 of the matrix.
		/// </summary>
		public double m12;

		/// <summary>
		/// Value at column 2, row 0 of the matrix.
		/// </summary>
		public double m20;
		/// <summary>
		/// Value at column 2, row 1 of the matrix.
		/// </summary>
		public double m21;
		/// <summary>
		/// Value at column 2, row 2 of the matrix.
		/// </summary>
		public double m22;

		#endregion Public Fields

		public Matrix3(
			double m00, double m10, double m20,
			double m01, double m11, double m21,
			double m02, double m12, double m22)
		{
			//Col 0
			this.m00 = m00;
			this.m01 = m01;
			this.m02 = m02;

			//Col 1
			this.m10 = m10;
			this.m11 = m11;
			this.m12 = m12;

			//Col 2
			this.m20 = m20;
			this.m21 = m21;
			this.m22 = m22;
		}

		public Matrix3(Matrix4 matrix)
		{
			this.m00 = matrix.m00;
			this.m01 = matrix.m01;
			this.m02 = matrix.m02;
			this.m10 = matrix.m10;
			this.m11 = matrix.m11;
			this.m12 = matrix.m12;
			this.m20 = matrix.m20;
			this.m21 = matrix.m21;
			this.m22 = matrix.m22;
		}

		/// <summary>
		/// Obtains the transpose matrix.
		/// </summary>
		/// <returns>Transpose matrix.</returns>
		public Matrix3 Transpose()
		{
			return new Matrix3(this.m00, this.m10, this.m20,
							this.m01, this.m11, this.m21,
							this.m02, this.m12, this.m22);
		}

		/// <summary>
		/// Gets the rotation matrix from the normal vector (extrusion direction) of an entity.
		/// </summary>
		/// <param name="zAxis">Normal vector.</param>
		/// <returns>Rotation matrix.</returns>
		public static Matrix3 ArbitraryAxis(XYZ zAxis)
		{
			zAxis.Normalize();

			if (zAxis.Equals(XYZ.AxisZ))
			{
				return Matrix3.Identity;
			}

			XYZ wY = XYZ.AxisY;
			XYZ wZ = XYZ.AxisZ;
			XYZ aX;

			if ((Math.Abs(zAxis.X) < 1 / 64.0) && (Math.Abs(zAxis.Y) < 1 / 64.0))
			{
				aX = XYZ.Cross(wY, zAxis);
			}
			else
			{
				aX = XYZ.Cross(wZ, zAxis);
			}

			aX.Normalize();

			XYZ aY = XYZ.Cross(zAxis, aX);
			aY.Normalize();

			return new Matrix3(aX.X, aY.X, zAxis.X, aX.Y, aY.Y, zAxis.Y, aX.Z, aY.Z, zAxis.Z);
		}

		/// <summary>
		/// Builds a rotation matrix for a rotation around the z-axis.
		/// </summary>
		/// <param name="angle">The counter-clockwise angle in radians.</param>
		/// <returns>The resulting Matrix3 instance.</returns>
		/// <remarks>Matrix3 adopts the convention of using column vectors to represent a transformation matrix.</remarks>
		public static Matrix3 RotationZ(double angle)
		{
			double cos = Math.Cos(angle);
			double sin = Math.Sin(angle);
			return new Matrix3(cos, -sin, 0.0,
							   sin, cos, 0.0,
							   0.0, 0.0, 1.0);
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			string separator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
			StringBuilder s = new StringBuilder();
			s.Append(string.Format("|{0}{2} {0}{2} {1}|" + Environment.NewLine, this.m00, this.m01, this.m02, separator));
			s.Append(string.Format("|{0}{2} {0}{2} {1}|" + Environment.NewLine, this.m10, this.m11, this.m12, separator));
			s.Append(string.Format("|{0}{2} {0}{2} {1}|", this.m20, this.m21, this.m22, separator));
			return s.ToString();
		}
	}
}
