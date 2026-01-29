using System;

namespace CSMath
{
	public partial struct XY : IVector, IEquatable<XY>
	{
		/// <inheritdoc/>
		public uint Dimension { get { return 2; } }

		/// <summary>
		/// Specifies the X-value of the vector component
		/// </summary>
		public double X { get; set; }

		/// <summary>
		/// Specifies the Y-value of the vector component
		/// </summary>
		public double Y { get; set; }

		public static readonly XY AxisX = new XY(1, 0);

		public static readonly XY AxisY = new XY(0, 1);

		public static readonly XY NaN = new XY(double.NaN);

		public static readonly XY Zero = new XY(0, 0);

		/// <summary>
		/// Constructor with the coordinate components
		/// </summary>
		/// <param name="x">Value of the X-coordinate</param>
		/// <param name="y">Value of the Y-coordinate</param>
		public XY(double x, double y)
		{
			X = x;
			Y = y;
		}

		/// <summary>
		/// Constructs a vector whose components are all the single specified value.
		/// </summary>
		/// <param name="value">The element to fill the vector with.</param>
		public XY(double value) : this(value, value) { }

		/// <summary>
		/// Computes the cross product of two coordinates.
		/// </summary>
		/// <param name="xy1">The first coordinate.</param>
		/// <param name="xy2">The second coordinate.</param>
		/// <returns>The cross product.</returns>
		public static double Cross(XY xy1, XY xy2)
		{
			return xy1.X * xy2.Y - xy1.Y * xy2.X;
		}

		/// <summary>
		/// Obtains the polar point of another point.
		/// </summary>
		/// <param name="u">Reference point.</param>
		/// <param name="distance">Distance from point u.</param>
		/// <param name="angle">Angle in radians.</param>
		/// <returns>The polar point of the specified point.</returns>
		public static XY Polar(XY u, double distance, double angle)
		{
			XY dir = new XY(Math.Cos(angle), Math.Sin(angle));
			return u + dir * distance;
		}

		/// <summary>
		/// Rotates a vector.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="angle">Rotation angles in radians.</param>
		/// <returns>The rotated vector.</returns>
		public static XY Rotate(XY value, double angle)
		{
			double sin = Math.Sin(angle);
			double cos = Math.Cos(angle);
			return new XY(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
		}

		/// <inheritdoc/>
		public override bool Equals(object? obj)
		{
			if (!(obj is XY other))
				return false;

			return this.Equals(other);
		}

		/// <summary>
		/// Indicates whether this instance and a specified object are equal with in a specific precison.
		/// </summary>
		/// <param name="other"></param>
		/// <param name="digits">number of decimals</param>
		/// <returns></returns>
		public bool Equals(XY other, int digits)
		{
			return other.IsEqual(this, digits);
		}

		/// <inheritdoc/>
		public bool Equals(XY other)
		{
			return this.X == other.X && this.Y == other.Y;
		}

		/// <summary>
		/// Get the angle formed by the components of this vector.
		/// </summary>
		/// <returns>Angle in radians.</returns>
		public double GetAngle()
		{
			return Math.Atan2(Y, X);
		}

		/// <summary>
		/// Obtains the angle of a line defined by two points.
		/// </summary>
		/// <param name="v">Point defining the line.</param>
		/// <returns>Angle in radians.</returns>
		public double GetAngle(XY v)
		{
			XY dir = v - this;
			return dir.GetAngle();
		}

		/// <inheritdoc/>
		public override int GetHashCode()
		{
			return this.X.GetHashCode() ^ this.Y.GetHashCode();
		}

		/// <summary>
		/// Get the perpendicular vector.
		/// </summary>
		/// <returns></returns>
		public XY Perpendicular()
		{
			return new XY(-this.Y, this.X);
		}

		/// <summary>
		/// Obtains the polar point of another point.
		/// </summary>
		/// <param name="distance">Distance from point u.</param>
		/// <param name="angle">Angle in radians.</param>
		/// <returns>The polar point of the specified point.</returns>
		public XY Polar(double distance, double angle)
		{
			XY dir = new(Math.Cos(angle), Math.Sin(angle));
			return this + dir * distance;
		}

		/// <inheritdoc/>
		public override string ToString()
		{
			return $"{X},{Y}";
		}

		/// <summary>
		/// Converts the numeric value of this instance to its equivalent string representation
		/// using the specified culture-specific format information.
		/// </summary>
		/// <param name="cultureInfo">An object that supplies culture-specific formatting information.</param>
		/// <returns>The string representation of the value of this instance as specified by provider.</returns>
		public string ToString(IFormatProvider? cultureInfo)
		{
			return $"{X.ToString(cultureInfo)},{Y.ToString(cultureInfo)}";
		}

		/// <inheritdoc/>
		public double this[int index]
		{
			get
			{
				switch (index)
				{
					case 0:
						return X;

					case 1:
						return Y;

					default:
						throw new IndexOutOfRangeException($"The index must be between 0 and {this.Dimension}.");
				}
			}
			set
			{
				switch (index)
				{
					case 0:
						X = value;
						break;

					case 1:
						Y = value;
						break;

					default:
						throw new IndexOutOfRangeException($"The index must be between 0 and {this.Dimension}.");
				}
			}
		}
	}
}