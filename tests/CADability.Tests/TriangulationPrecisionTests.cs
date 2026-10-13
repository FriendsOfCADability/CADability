using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// How far the triangles of <see cref="Face.GetTriangulation"/> lie from the surface (#414). The edges are approximated
    /// with the requested precision, the triangles inside a face may deviate by about five times as much, which the
    /// documentation says now; a caller that needs about the same deviation inside the face asks for a fifth of the precision.
    /// </summary>
    [TestClass]
    public class TriangulationPrecisionTests
    {
        private const double SphereRadius = 40, TorusRadius = 50, TubeRadius = 20;

        private static double SphereDistance(GeoPoint p) => Math.Abs((p - GeoPoint.Origin).Length - SphereRadius);

        private static double TorusDistance(GeoPoint p)
        {
            double ring = Math.Sqrt(p.x * p.x + p.y * p.y) - TorusRadius;
            return Math.Abs(Math.Sqrt(ring * ring + p.z * p.z) - TubeRadius);
        }

        /// <summary>The largest distance of a triangle centroid from the surface, as a multiple of <paramref name="precision"/>.</summary>
        private static double WorstCentroid(Solid solid, Func<GeoPoint, double> distance, double precision)
        {
            double worst = 0.0;
            foreach (Face face in ((Solid)solid.Clone()).Shells[0].Faces) // a fresh copy has no cached triangulation
            {
                face.GetTriangulation(precision, out GeoPoint[] points, out _, out int[] index, out _);
                for (int i = 0; i < index.Length; i += 3)
                {
                    GeoPoint centroid = new GeoPoint(points[index[i]], points[index[i + 1]], points[index[i + 2]]);
                    worst = Math.Max(worst, distance(centroid) / precision);
                }
            }
            return worst;
        }

        [TestMethod]
        public void TrianglesInsideAFaceDeviateUpToFiveTimesThePrecision()
        {
            (string name, Solid solid, Func<GeoPoint, double> distance)[] cases =
            {
                ("sphere", Make3D.MakeSphere(GeoPoint.Origin, SphereRadius), SphereDistance),
                ("torus", Make3D.MakeTorus(GeoPoint.Origin, GeoVector.ZAxis, TorusRadius, TubeRadius), TorusDistance),
            };
            foreach ((string name, Solid solid, Func<GeoPoint, double> distance) in cases)
            {
                foreach (double precision in new[] { 0.1 })
                {
                    double asked = WorstCentroid(solid, distance, precision);
                    Assert.IsTrue(asked > 1.5, $"{name}, {precision}: the interior uses the factor ({asked:F2} times the precision)");
                    Assert.IsTrue(asked < 5.5, $"{name}, {precision}: {asked:F2} times the precision");
                    // asking for a fifth keeps the interior close to the precision (about 1.2 times it at most)
                    double bounded = WorstCentroid(solid, distance, precision / 5.0) / 5.0;
                    Assert.IsTrue(bounded < 1.5, $"{name}, {precision} / 5: {bounded:F2} times the precision");
                }
            }
        }
    }
}
