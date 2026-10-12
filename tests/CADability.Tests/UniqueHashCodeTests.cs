using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CADability.Tests
{
    /// <summary>
    /// Faces, edges and vertices take their hash code from a static counter, and the boolean operations order them by it
    /// (DoubleFaceKey, EdgeFaceKey and others), so two of them with the same hash code count as the same object. The counters
    /// were incremented with ++, which is not atomic: objects created at the same time on different threads, as in two
    /// boolean operations or a parallel STEP import, could get the same hash code, and a boolean operation then lost an
    /// intersection and returned no solid now and then (BooleanNearlyCoincidentTests, run in parallel).
    /// </summary>
    [TestClass]
    public class UniqueHashCodeTests
    {
        private static void AssertUnique(string what, Func<object> create)
        {
            const int n = 500000;
            int[] hashCodes = new int[n];
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount) }, i => hashCodes[i] = create().GetHashCode());
            HashSet<int> distinct = new HashSet<int>(hashCodes);
            Assert.AreEqual(n, distinct.Count, $"{what}: {n - distinct.Count} hash codes were given twice");
        }

        [TestMethod]
        public void FacesCreatedInParallelHaveDifferentHashCodes()
        {
            AssertUnique("faces", () => Face.Construct());
        }

        [TestMethod]
        public void EdgesCreatedInParallelHaveDifferentHashCodes()
        {
            AssertUnique("edges", () => new Edge(null, null, null, null, true, null, null, true));
        }

        [TestMethod]
        public void VerticesCreatedInParallelHaveDifferentHashCodes()
        {
            AssertUnique("vertices", () => new Vertex(GeoPoint.Origin));
        }
    }
}
