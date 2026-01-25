using CADability.Curve2D;
using CADability.Shapes;

namespace CADability.Tests
{
	[TestClass]
	public class BorderTests
	{
		[TestMethod]
		public void GetParallel_OffsetCausesArcToDisappear_DoesNotThrowException()
		{
			// This test reproduces the issue from https://github.com/FriendsOfCADability/CADability/issues/XXX
			// When an arc of the border disappears for the computed offset, it should not throw an exception
			
			BorderBuilder bb = new BorderBuilder();
			bb.AddSegment(new Line2D(new GeoPoint2D(2, 0), new GeoPoint2D(8, 0)));
			bb.AddSegment(new Arc2D(new GeoPoint2D(8, 2), 2, Angle.A270, SweepAngle.Deg(90)));
			bb.AddSegment(new Line2D(new GeoPoint2D(10, 2), new GeoPoint2D(10, 8)));
			bb.AddSegment(new Arc2D(new GeoPoint2D(8, 8), 2, Angle.A0, SweepAngle.Deg(90)));
			bb.AddSegment(new Line2D(new GeoPoint2D(8, 10), new GeoPoint2D(2, 10)));
			bb.AddSegment(new Arc2D(new GeoPoint2D(2, 8), 2, Angle.A90, SweepAngle.Deg(90)));
			bb.AddSegment(new Line2D(new GeoPoint2D(0, 8), new GeoPoint2D(0, 2)));
			bb.AddSegment(new Arc2D(new GeoPoint2D(2, 2), 2, Angle.A180, SweepAngle.Deg(90)));

			Border b = bb.BuildBorder(false);

			// This should work
			Border[] b1 = b.GetParallel(-1.5, false, 0.001, 0);
			Assert.IsNotNull(b1);

			// This used to throw NullReferenceException before the fix
			Border[] b2 = b.GetParallel(-2.5, false, 0.001, 0);
			Assert.IsNotNull(b2);
		}

		[TestMethod]
		public void GetParallel_SmallOffset_ReturnsValidBorder()
		{
			// Create a simple closed border
			BorderBuilder bb = new BorderBuilder();
			bb.AddSegment(new Line2D(new GeoPoint2D(0, 0), new GeoPoint2D(10, 0)));
			bb.AddSegment(new Line2D(new GeoPoint2D(10, 0), new GeoPoint2D(10, 10)));
			bb.AddSegment(new Line2D(new GeoPoint2D(10, 10), new GeoPoint2D(0, 10)));
			bb.AddSegment(new Line2D(new GeoPoint2D(0, 10), new GeoPoint2D(0, 0)));

			Border b = bb.BuildBorder(false);

			// Get parallel border with small offset
			Border[] result = b.GetParallel(-1.0, false, 0.001, 0);
			
			Assert.IsNotNull(result);
			Assert.IsTrue(result.Length > 0);
		}
	}
}
