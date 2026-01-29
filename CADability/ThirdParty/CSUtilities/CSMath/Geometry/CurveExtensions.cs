using System;
using System.Collections.Generic;

namespace CSMath.Geometry
{
	public static class CurveExtensions
	{
		public static XYZ PolarCoordinateRelativeToCenter(double angle, XYZ center,  XYZ normal, XYZ startPoint, double ratio = 1)
		{
			XYZ prep = XYZ.Cross(normal, startPoint);
			double radius = startPoint.Normalize().GetLength();
			return radius * (Math.Cos(angle) * startPoint + (double)ratio * Math.Sin(angle) * prep) + center;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="precision"></param>
		/// <param name="center"></param>
		/// <param name="startAngle"></param>
		/// <param name="endAngle"></param>
		/// <param name="normal"></param>
		/// <param name="startPoint">Start point of the curve relative to center.</param>
		/// <param name="ratio"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentOutOfRangeException"></exception>
		public static List<XYZ> PolygonalVertexes(int precision, XYZ center, double startAngle, double endAngle, XYZ normal, XYZ startPoint, double ratio = 1)
		{
			if (precision < 2)
			{
				throw new ArgumentOutOfRangeException(nameof(precision), precision, "The precision must be equal or greater than two.");
			}

			List<XYZ> points = new();

			double start = startAngle;
			double end = endAngle;
			if (end <= start)
			{
				end += MathHelper.TwoPI;
			}

			XYZ prep = XYZ.Cross(normal, startPoint);

			double portion = startPoint.Normalize().GetLength();
			double radius = ratio * portion;

			double delta = (end - start) / (precision - 1);

			for (int i = 0; i < precision; i++, start += delta)
			{
				points.Add(portion * MathHelper.Cos(start) * startPoint + radius * MathHelper.Sin(start) * prep + center);
			}

			return points;
		}
	}
}