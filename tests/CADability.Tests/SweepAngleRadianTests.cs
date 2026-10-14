using System;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #422: the setter of <see cref="SweepAngle.Radian"/> tested <c>a &lt; 2π</c> instead of <c>a &lt; -2π</c> and
    /// turned a full clockwise turn (-2π) into a counterclockwise one (+2π).
    /// </summary>
    [TestClass]
    public class SweepAngleRadianTests
    {
        [TestMethod]
        public void FullClockwiseTurnStaysClockwise()
        {
            SweepAngle sa = new SweepAngle(0.0);
            sa.Radian = -2.0 * Math.PI;
            Assert.AreEqual(-2.0 * Math.PI, sa.Radian, 1e-12);
        }

        [TestMethod]
        public void ValuesInRangeAreKept()
        {
            SweepAngle sa = new SweepAngle(0.0);
            foreach (double value in new[] { 2.0 * Math.PI, Math.PI, 0.5, 0.0, -0.5, -Math.PI, -2.0 * Math.PI + 1e-6 })
            {
                sa.Radian = value;
                Assert.AreEqual(value, sa.Radian, 1e-12, "value " + value);
            }
        }

        [TestMethod]
        public void ValuesOutOfRangeAreReducedModulo4Pi()
        {
            SweepAngle sa = new SweepAngle(0.0);
            sa.Radian = 4.0 * Math.PI + 1.0;
            Assert.AreEqual(1.0, sa.Radian, 1e-12);
            sa.Radian = -4.0 * Math.PI - 1.0;
            Assert.AreEqual(-1.0, sa.Radian, 1e-12);
        }
    }
}
