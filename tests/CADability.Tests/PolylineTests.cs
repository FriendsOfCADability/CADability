using CADability.GeoObject;

namespace CADability.Tests
{
	[TestClass]
	public class PolylineTests
	{
		[TestMethod]
		public void Position_ShouldReturnMaxValue_WhenRayIsTrulyParallelToPlane()
		{
			var vertices = new GeoPoint[]
			{
				new GeoPoint(0, 0, 0),
				new GeoPoint(10, 0, 0),
				new GeoPoint(10, 10, 0),
				new GeoPoint(0, 10, 0)
			};
			var polyline = Polyline.Construct();
			polyline.SetPoints(vertices, true);

			// Ray direction lies in plane → should trigger PlaneException
			var rayStart = new GeoPoint(5, 5, 10);
			var rayDir = GeoVector.XAxis; // parallel to the plane

			double result = polyline.Position(rayStart, rayDir, 0.01);

			Assert.AreEqual(double.MaxValue, result);
		}

	}
}
