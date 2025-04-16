using CADability.Curve2D;
using System.Linq;

namespace CADability.Tests
{
	[TestClass]
	public class Arc2DTests
	{
		[TestMethod]
		public void Trim_ZeroToZero_ReturnsNull()
		{
			var arc = new Arc2D(new GeoPoint2D(0, 0), 10, new Angle(0), new SweepAngle(90));
			var trimmed = arc.Trim(0.0, 0.0);
			Assert.IsNull(trimmed);
		}

		[TestMethod]
		public void Trim_FirstHalf_ReturnsValidArc()
		{
			var arc = new Arc2D(new GeoPoint2D(0, 0), 10, new Angle(0), new SweepAngle(90));
			var trimmed = arc.Trim(0.0, 0.5);
			Assert.IsNotNull(trimmed);
			Assert.IsTrue(trimmed.Length > 0);
		}

		[TestMethod]
		public void Trim_ReversedHalf_ReturnsValidArc()
		{
			var arc = new Arc2D(new GeoPoint2D(0, 0), 10, new Angle(0), new SweepAngle(90));
			var trimmed = arc.Trim(0.5, 0.0);
			Assert.IsNotNull(trimmed);
			Assert.IsTrue(trimmed.Length > 0);
		}

		[TestMethod]
		public void Trim_AlmostZeroSweep_ReturnsNull()
		{
			var arc = new Arc2D(new GeoPoint2D(0, 0), 10, new Angle(0), new SweepAngle(90));
			var trimmed = arc.Trim(0.5, 0.500000001);
			Assert.IsNull(trimmed);
		}
	}
}
