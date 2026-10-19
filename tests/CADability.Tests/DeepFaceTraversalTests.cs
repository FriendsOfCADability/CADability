using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Regression for issue #486: the faces of a shell were collected by a recursive depth first search
    /// (Make3D.extractConnectedFaces, BRepOperation.extractConnectedFaces, BRepSelfIntersection.collectFaces), which nests
    /// once per connected face. A few thousand faces overflowed the stack, and a StackOverflowException terminates the
    /// process. The tests run on a thread with a small stack, so that the old recursion fails with far fewer faces than it
    /// does on a 1 MB stack.
    /// </summary>
    [TestClass]
    public class DeepFaceTraversalTests
    {
        private const int SmallStack = 256 * 1024;

        private static void RunOnSmallStack(Action action)
        {
            Exception failure = null;
            Thread thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception e) { failure = e; }
            }, SmallStack);
            thread.Start();
            Assert.IsTrue(thread.Join(TimeSpan.FromMinutes(5)), "the operation did not finish");
            if (failure != null) throw new AssertFailedException(failure.ToString());
        }

        /// <summary>a flat grid of n x n squares, each split into two triangles</summary>
        private static Face[] TriangleGrid(int n)
        {
            List<Face> faces = new List<Face>();
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    GeoPoint p00 = new GeoPoint(i, j, 0), p10 = new GeoPoint(i + 1, j, 0);
                    GeoPoint p01 = new GeoPoint(i, j + 1, 0), p11 = new GeoPoint(i + 1, j + 1, 0);
                    faces.Add(Face.MakeFace(p00, p10, p11));
                    faces.Add(Face.MakeFace(p00, p11, p01));
                }
            }
            return faces.ToArray();
        }

        [TestMethod]
        public void sewing_thousands_of_triangles_does_not_overflow_the_stack()
        {
            Face[] triangles = TriangleGrid(40);
            Shell[] shells = null;
            RunOnSmallStack(() => shells = Make3D.SewFaces(triangles));
            Assert.AreEqual(1, shells.Length);
            Assert.AreEqual(triangles.Length, shells[0].Faces.Length);
        }

        [TestMethod]
        public void sewing_keeps_separate_parts_apart()
        {
            Face[] first = TriangleGrid(3);
            Face[] second = TriangleGrid(2);
            ModOp move = ModOp.Translate(0, 0, 10);
            foreach (Face face in second) face.Modify(move);
            Shell[] shells = Make3D.SewFaces(first.Concat(second).ToArray());
            CollectionAssert.AreEquivalent(new[] { first.Length, second.Length }, shells.Select(s => s.Faces.Length).ToArray());
        }

        [TestMethod]
        public void subtracting_from_a_prism_with_thousands_of_sides_does_not_overflow_the_stack()
        {
            const int sides = 3000;
            GeoPoint[] points = new GeoPoint[sides];
            for (int i = 0; i < sides; i++)
            {
                double a = 2.0 * Math.PI * i / sides;
                points[i] = new GeoPoint(100 * Math.Cos(a), 100 * Math.Sin(a), 0);
            }
            Solid prism = Make3D.MakePrism(Polyline.FromPoints(points, true), 10 * GeoVector.ZAxis, null, false) as Solid;
            Assert.IsNotNull(prism);
            Assert.AreEqual(sides + 2, prism.Shells[0].Faces.Length);
            // a box that cuts a notch into the top face, away from the sides
            Solid box = Make3D.MakeBox(new GeoPoint(-10, -10, 5), 20 * GeoVector.XAxis, 20 * GeoVector.YAxis, 10 * GeoVector.ZAxis);
            Solid[] result = null;
            RunOnSmallStack(() => result = Solid.Subtract(prism, box));
            Assert.AreEqual(1, result.Length);
            // all sides, the bottom, the top with a hole and the five faces of the notch
            Assert.AreEqual(sides + 2 + 5, result[0].Shells[0].Faces.Length);
        }
    }
}
