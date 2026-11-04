using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// A face triangulation stops refining at a maximum number of triangles. A precision that needs more (the precision is
    /// absolute, 1e-6 on a sphere of radius 3 needs millions of triangles) stopped it half way, with triangles across half the
    /// sphere: the volume of a sphere was off by 1e-8 at 1e-4, by 6e-5 at 1e-6 and by 9e-4 at 1e-7. And since a face keeps its
    /// finest triangulation, the volume depended on the precision asked before. <see cref="Shell.Volume(double)"/> now
    /// triangulates all faces again with a coarser precision when a face is incomplete, and a face does not use an
    /// incomplete triangulation for a coarser precision.
    /// </summary>
    [TestClass]
    public class TriangulationLimitTests
    {
        private const double Radius = 3.0;
        private static readonly double SphereVolume = 4.0 / 3.0 * Math.PI * Radius * Radius * Radius;

        [TestMethod]
        [DataRow(1e-4)]
        [DataRow(1e-6)]
        [DataRow(1e-7)]
        public void VolumeOfASphereAtAPrecisionBeyondTheLimit(double precision)
        {
            Solid sphere = Make3D.MakeSphere(GeoPoint.Origin, Radius);
            Assert.AreEqual(SphereVolume, sphere.Volume(precision), 1e-6 * SphereVolume, $"volume at {precision}");
        }

        [TestMethod]
        public void VolumeDoesNotDependOnATooFineTriangulationBefore()
        {
            Solid sphere = Make3D.MakeSphere(GeoPoint.Origin, Radius);
            foreach (Face face in sphere.Shells[0].Faces) face.GetTriangulation(1e-7, out _, out _, out _, out _); // too fine, incomplete
            // 2.5e-7 off when the sphere is triangulated with 1e-3 from the start, 9e-4 off with the incomplete triangulation
            Assert.AreEqual(SphereVolume, sphere.Volume(1e-3), 1e-6 * SphereVolume, "the incomplete triangulation was used for 1e-3");
        }
    }
}
