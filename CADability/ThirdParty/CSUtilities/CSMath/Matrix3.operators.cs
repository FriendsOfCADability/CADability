namespace CSMath
{
	public partial struct Matrix3
	{
		public static XYZ operator *(Matrix3 matrix, XYZ value)
		{
			return new XYZ(matrix.m00 * value.X + matrix.m01 * value.Y + matrix.m02 * value.Z,
							matrix.m10 * value.X + matrix.m11 * value.Y + matrix.m12 * value.Z,
							matrix.m20 * value.X + matrix.m21 * value.Y + matrix.m22 * value.Z);
		}

		public static Matrix3 operator *(Matrix3 a, Matrix3 b)
		{
			return new Matrix3(a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20, a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21, a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22,
							   a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20, a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21, a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22,
							   a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20, a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21, a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22);
		}
	}
}
