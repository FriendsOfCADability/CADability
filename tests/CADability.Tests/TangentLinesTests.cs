using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability.Tests
{
	[TestClass]
	public class TangentLinesTests
	{
		const double Tolerance = 1e-6;

		[TestMethod]
		public void TangentLines_Circles_ReturnsFourTangents()
		{
			// Arrange
			var circle1 = new Circle2D(new GeoPoint2D(0, 0), 10);
			var circle2 = new Circle2D(new GeoPoint2D(30, 0), 5);

			// Act
			GeoPoint2D[] tangents = Curves2D.TangentLines(circle1, circle2);

			// Assert
			Assert.AreEqual(8, tangents.Length, "Should return 4 tangent lines (8 points total)");
		}


		[TestMethod]
		public void Tangents_TouchCirclePerimeters()
		{
			var circle1 = new Circle2D(new GeoPoint2D(0, 0), 10);
			var circle2 = new Circle2D(new GeoPoint2D(30, 0), 5);

			var tangents = Curves2D.TangentLines(circle1, circle2);

			for (int i = 0; i < tangents.Length; i += 2)
			{
				var p1 = tangents[i];
				var p2 = tangents[i + 1];

				double d1 = (p1 | circle1.Center);
				double d2 = (p2 | circle2.Center);

				Assert.IsTrue(Math.Abs(d1 - circle1.Radius) < Tolerance, $"Tangents[{i}] not on circle1");
				Assert.IsTrue(Math.Abs(d2 - circle2.Radius) < Tolerance, $"Tangents[{i + 1}] not on circle2");
			}
		}

		[TestMethod]
		public void Tangents_AreGeometricallyDistinct()
		{
			var circle1 = new Circle2D(new GeoPoint2D(0, 0), 10);
			var circle2 = new Circle2D(new GeoPoint2D(30, 0), 5);

			var tangents = Curves2D.TangentLines(circle1, circle2);

			Assert.AreEqual(8, tangents.Length, "Expected 4 tangent lines (8 points)");

			// Compute Y midpoint of each tangent line
			double[] midY = new double[4];
			for (int i = 0; i < 8; i += 2)
			{
				midY[i / 2] = (tangents[i].y + tangents[i + 1].y) / 2.0;
			}

			// Check that not all midpoints are the same (i.e. the lines are distinct)
			bool allSame = true;
			for (int i = 1; i < midY.Length; i++)
			{
				if (Math.Abs(midY[i] - midY[0]) > 1e-6)
				{
					allSame = false;
					break;
				}
			}

			Assert.IsFalse(allSame, "Tangents should be geometrically distinct (not all midpoints equal)");
		}

		[TestMethod]
		public void NoTangents_WhenCirclesAreNested()
		{
			var circle1 = new Circle2D(new GeoPoint2D(0, 0), 10);
			var circle2 = new Circle2D(new GeoPoint2D(0, 0), 5);

			var tangents = Curves2D.TangentLines(circle1, circle2);

			Assert.AreEqual(0, tangents.Length, "No tangents should exist between concentric circles.");
		}
	}
}
