using System;
using System.Linq;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="Make3D.MakePrismMiter(SimpleShape, GeoPoint, GeoVector, GeoVector, GeoVector, GeoVector)"/>. This used
    /// to throw a NullReferenceException for every shape: Face.Set computes the domain of a face from the 2d curves of its edges,
    /// and the edges got their 2d curves only after Face.Set.
    /// </summary>
    /// <remarks>
    /// All profiles run along z with the "up" direction y, so the 2d x-axis of the shape is the global x-axis. A miter
    /// direction of +x or -x cuts the ends at 45 degrees: the start along z = x or z = -x, the end along z = l - x or
    /// z = l + x for the length l.
    /// </remarks>
    [TestClass]
    public class MakePrismMiterTests
    {
        private static Solid Miter(SimpleShape shape, GeoPoint location, GeoVector mainDirection, GeoVector startMiter, GeoVector endMiter)
        {
            Solid prism = Make3D.MakePrismMiter(shape, location, GeoVector.YAxis, mainDirection, startMiter, endMiter);
            Assert.IsNotNull(prism);
            Shell shell = prism.Shells[0];
            Assert.AreEqual(0, shell.OpenEdgesExceptPoles.Length, "the prism has open edges");
            Assert.IsTrue(shell.CheckConsistency(), "the prism is not consistent");
            return prism;
        }

        /// <summary>the rectangle 40 x 30 with a hole of radius 5 in its center (20, 15)</summary>
        private static SimpleShape RectangleWithHole() =>
            new SimpleShape(Border.MakeRectangle(0, 40, 0, 30), Border.MakeCircle(new GeoPoint2D(20, 15), 5));

        private static readonly double RectangleWithHoleArea = 1200 - Math.PI * 25;

        /// <summary>start along z = x, end along z = 50 + x: the cuts are parallel, every fibre has the length 50</summary>
        [TestMethod]
        public void parallel_miter_cuts()
        {
            Solid prism = Miter(RectangleWithHole(), GeoPoint.Origin, 50 * GeoVector.ZAxis, GeoVector.XAxis, -GeoVector.XAxis);
            Assert.AreEqual(4 + 2 + 2, prism.Shells[0].Faces.Length, "four sides, the hole in two halves, bottom and top");
            double expected = RectangleWithHoleArea * 50;
            Assert.AreEqual(expected, prism.Volume(0.001), expected * 1e-5);
        }

        /// <summary>start along z = -x, end along z = 50 + x: a fibre at x has the length 50 + 2x, 90 at the centroid</summary>
        [TestMethod]
        public void diverging_miter_cuts()
        {
            Solid prism = Miter(RectangleWithHole(), GeoPoint.Origin, 50 * GeoVector.ZAxis, -GeoVector.XAxis, -GeoVector.XAxis);
            double expected = RectangleWithHoleArea * 90;
            Assert.AreEqual(expected, prism.Volume(0.001), expected * 1e-5);
        }

        /// <summary>a round profile: the side is a cylinder, the edges of the cuts are ellipses on it</summary>
        [TestMethod]
        public void round_profile()
        {
            SimpleShape circle = new SimpleShape(Border.MakeCircle(new GeoPoint2D(20, 15), 10));
            Solid prism = Miter(circle, GeoPoint.Origin, 50 * GeoVector.ZAxis, -GeoVector.XAxis, -GeoVector.XAxis);
            double expected = Math.PI * 100 * 90;
            Assert.AreEqual(expected, prism.Volume(0.001), expected * 1e-5);
        }

        /// <summary>
        /// The square 30 x 30 whose right side bulges out as an arc around (15, 15) from -45 to +45 degrees: the arc crosses
        /// the seam of its cylinder at the angle 0.
        /// </summary>
        [TestMethod]
        public void arc_across_the_seam()
        {
            double r = 15 * Math.Sqrt(2);
            Border outline = new Border(new ICurve2D[] {
                new Line2D(new GeoPoint2D(0, 0), new GeoPoint2D(30, 0)),
                new Arc2D(new GeoPoint2D(15, 15), r, new GeoPoint2D(30, 0), new GeoPoint2D(30, 30), true),
                new Line2D(new GeoPoint2D(30, 30), new GeoPoint2D(0, 30)),
                new Line2D(new GeoPoint2D(0, 30), new GeoPoint2D(0, 0)) });
            Solid prism = Miter(new SimpleShape(outline), GeoPoint.Origin, 50 * GeoVector.ZAxis, GeoVector.XAxis, -GeoVector.XAxis);
            double area = 900 + r * r / 2 * (Math.PI / 2 - 1);
            double expected = area * 50;
            Assert.AreEqual(expected, prism.Volume(0.001), expected * 1e-5);
        }

        /// <summary>
        /// The corner of a frame: a vertical bar with its end cut for a profile going on in +x, and a horizontal bar starting
        /// there with its start cut for the profile coming from -z. Both cuts lie on the plane x + z = 100 and meet exactly.
        /// </summary>
        [TestMethod]
        public void the_cuts_of_a_corner_meet()
        {
            SimpleShape rectangle = new SimpleShape(Border.MakeRectangle(0, 40, 0, 30));
            Solid vertical = Miter(rectangle, GeoPoint.Origin, 100 * GeoVector.ZAxis, -GeoVector.XAxis, GeoVector.XAxis);
            Solid horizontal = Miter(rectangle, new GeoPoint(0, 0, 100), 100 * GeoVector.XAxis, -GeoVector.ZAxis, GeoVector.ZAxis);
            GeoPoint[] OnCut(Solid s) => s.Shells[0].Vertices.Select(v => v.Position)
                .Where(p => Math.Abs(p.x + p.z - 100) < 1e-6).OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
            GeoPoint[] verticalCut = OnCut(vertical);
            GeoPoint[] horizontalCut = OnCut(horizontal);
            Assert.AreEqual(4, verticalCut.Length);
            Assert.AreEqual(4, horizontalCut.Length);
            for (int i = 0; i < 4; i++) Assert.IsTrue(Precision.IsEqual(verticalCut[i], horizontalCut[i]), $"{verticalCut[i]} != {horizontalCut[i]}");
            // the outer corner at (0, y, 100), the inner one at (40, y, 60)
            Assert.IsTrue(verticalCut.Any(p => Precision.IsEqual(p, new GeoPoint(0, 0, 100))));
            Assert.IsTrue(verticalCut.Any(p => Precision.IsEqual(p, new GeoPoint(40, 30, 60))));
            // the two bars touch on the cut and unite to one solid
            Solid corner = Solid.Unite(vertical, horizontal);
            Assert.IsNotNull(corner);
            Assert.IsTrue(corner.Shells[0].CheckConsistency());
            double expected = vertical.Volume(0.001) + horizontal.Volume(0.001);
            Assert.AreEqual(expected, corner.Volume(0.001), expected * 1e-5);
        }
    }
}
