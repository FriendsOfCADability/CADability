using System.Reflection;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #433: <see cref="Shell.SameGeometry"/> read the field behind <see cref="Shell.Edges"/> instead of the
    /// property, which fills it lazily, and threw a NullReferenceException on a shell whose edges had not been computed.
    /// </summary>
    [TestClass]
    public class ShellSameGeometryTests
    {
        private static Shell Box(double size)
        {
            return Make3D.MakeBox(GeoPoint.Origin, size * GeoVector.XAxis, size * GeoVector.YAxis, size * GeoVector.ZAxis).Shells[0];
        }

        [TestMethod]
        public void WorksBeforeTheEdgesWereComputed()
        {
            Shell shell = Box(10.0);
            Shell other = Box(10.0);
            FieldInfo edgesField = typeof(Shell).GetField("edges", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(edgesField);
            Assert.IsNull(edgesField.GetValue(shell), "a fresh box shell has not computed its edges yet");

            Assert.IsTrue(shell.SameGeometry(other));
        }

        [TestMethod]
        public void DifferentBoxesAreNotTheSame()
        {
            Assert.IsFalse(Box(10.0).SameGeometry(Box(12.0)));
        }
    }
}
