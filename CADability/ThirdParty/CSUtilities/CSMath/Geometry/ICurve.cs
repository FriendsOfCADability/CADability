using System.Collections.Generic;

namespace CSMath.Geometry
{
	public interface ICurve
	{
		/// <summary>
		///Specifies the center of the curve.
		/// </summary>
		XYZ Center { get; }

		/// <summary>
		/// Ratio of minor axis to major axis.
		/// </summary>
		/// <remarks>
		/// Value is 1 for a circle.
		/// </remarks>
		double RadiusRatio { get; }

		/// <summary>
		/// Converts the curve in a list of vertexes.
		/// </summary>
		/// <param name="precision">Number of vertexes generated.</param>
		/// <returns>A list vertexes that represents the curve expressed in object coordinate system.</returns>
		List<XYZ> PolygonalVertexes(int precision);

		/// <summary>
		/// Calculate the local point on the curve for a given angle relative to the center.
		/// </summary>
		/// <param name="angle">Angle in radians.</param>
		/// <returns>A local point on the curve for the given angle relative to the center.</returns>
		XYZ PolarCoordinateRelativeToCenter(double angle);
	}
}