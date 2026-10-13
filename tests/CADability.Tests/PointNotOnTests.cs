using CADability.GeoObject;
using System.Reflection;

namespace CADability.Tests
{
    /// <summary>
    /// #416, finding 14: when two shells do not cross, <see cref="BRepOperation"/> decides with a point of one shell, which is not
    /// on the other, whether one contains the other. When every vertex and every face centre lay on the other shell, it took the
    /// first vertex, a point on the boundary, for which <see cref="Shell.Contains"/> is undefined.
    /// </summary>
    [TestClass]
    public class PointNotOnTests
    {
        private static readonly MethodInfo pointNotOn = typeof(BRepOperation).GetMethod("PointNotOn", BindingFlags.NonPublic | BindingFlags.Static);

        private static GeoPoint? PointNotOn(Shell shell, Shell other) => (GeoPoint?)pointNotOn.Invoke(null, new object[] { shell, other, 1e-6 });

        private static Solid Cube() => Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis);

        /// <summary>
        /// The cube with a bump on its top face, away from the centre: every vertex and every face centre of the cube lies on it,
        /// but the part of the top face under the bump does not.
        /// </summary>
        [TestMethod]
        public void a_point_under_a_bump_is_found()
        {
            Solid cube = Cube();
            Solid bump = Make3D.MakeBox(new GeoPoint(6, 6, 9), 3 * GeoVector.XAxis, 3 * GeoVector.YAxis, 2 * GeoVector.ZAxis);
            Solid bumped = Solid.Unite(Cube(), bump);
            Assert.IsNotNull(bumped);
            Shell other = bumped.Shells[0];
            Assert.IsTrue(cube.Shells[0].Vertices.All(v => other.Faces.Any(f => Math.Abs(f.Distance(v.Position)) < 1e-6)), "test setup: the vertices are on the other shell");

            GeoPoint? p = PointNotOn(cube.Shells[0], other);
            Assert.IsTrue(p.HasValue, "there is a point of the cube which is not on the bumped cube");
            Assert.IsTrue(other.Faces.All(f => Math.Abs(f.Distance(p.Value)) > 1e-6), $"{p} is not on the other shell");
            Assert.IsTrue(other.Contains(p.Value), "and it is inside: the cube lies in the bumped cube");
        }

        [TestMethod]
        public void identical_shells_have_no_such_point()
        {
            Solid cube = Cube();
            Assert.IsNull(PointNotOn(cube.Shells[0], ((Solid)cube.Clone()).Shells[0]), "the boundaries coincide");
        }
    }
}
