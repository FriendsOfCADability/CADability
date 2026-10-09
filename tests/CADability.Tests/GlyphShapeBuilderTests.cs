using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System.Drawing;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for the construction of glyph areas from outline data (no installed font needed). The outline data
    /// is given in the form GDI+ delivers it (GraphicsPath.PathPoints / PathTypes).
    /// </summary>
    [TestClass]
    public class GlyphShapeBuilderTests
    {
        // GDI+ path point types
        private const byte Start = 0;
        private const byte Line = 1;
        private const byte Close = 0x80;

        /// <summary>
        /// Appends a closed polygon (as GDI+ would deliver it: start point, lines, close flag on the last point).
        /// </summary>
        private static void AddPolygon(List<PointF> points, List<byte> types, params (double x, double y)[] vertices)
        {
            for (int i = 0; i < vertices.Length; ++i)
            {
                // GDI+ has a downward y axis, PathDataToPath2D flips it (y = 1 - y)
                points.Add(new PointF((float)vertices[i].x, (float)(1.0 - vertices[i].y)));
                byte t = i == 0 ? Start : Line;
                if (i == vertices.Length - 1) t |= Close;
                types.Add(t);
            }
        }

        private static Path2D[] MakePaths(params (double x, double y)[][] polygons)
        {
            List<PointF> points = new List<PointF>();
            List<byte> types = new List<byte>();
            foreach (var polygon in polygons) AddPolygon(points, types, polygon);
            return GlyphShapeBuilder.PathDataToPath2D(points.ToArray(), types.ToArray(), 1);
        }

        private static int CountTriangles(CompoundShape cs)
        {
            int res = 0;
            PlaneSurface pls = new PlaneSurface(Plane.XYPlane);
            foreach (SimpleShape ss in cs.SimpleShapes)
            {
                Face fc = Face.MakeFace(pls, ss);
                fc.GetTriangulation(0.05, out GeoPoint[] tp, out GeoPoint2D[] tuv, out int[] ti, out BoundingCube te);
                res += ti.Length / 3;
            }
            return res;
        }

        [TestMethod]
        public void PathDataToPath2D_ClosedSubpath_GivesClosedPath()
        {
            Path2D[] paths = MakePaths(new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) });
            Assert.AreEqual(1, paths.Length);
            Assert.IsTrue(paths[0].IsClosed);
            Assert.AreEqual(1.0, paths[0].GetExtent().Width, 1e-6);
            Assert.AreEqual(1.0, paths[0].GetExtent().Height, 1e-6);
        }

        [TestMethod]
        public void PathDataToPath2D_BezierWithCoincidingControlPoints_IsLeftOut()
        {
            // Gigi and the condensed instances of Bahnschrift contain Bezier segments whose four control points
            // coincide. Their approximation is an empty path, which made PathDataToPath2D throw an
            // IndexOutOfRangeException, so every text in these fonts failed.
            // A unit square (GDI+ coordinates, y downwards) with such a segment at its upper right corner:
            PointF[] points =
            {
                new PointF(0f, 1f), new PointF(1f, 1f), new PointF(1f, 0f),
                new PointF(1f, 0f), new PointF(1f, 0f), new PointF(1f, 0f),
                new PointF(0f, 0f)
            };
            byte[] types = { Start, Line, Line, 3, 3, 3, Line | Close };
            foreach (int fontPrecision in new int[] { 0, 1, 2 })
            {
                Path2D[] paths = GlyphShapeBuilder.PathDataToPath2D(points, types, fontPrecision);
                Assert.AreEqual(1, paths.Length, "precision " + fontPrecision);
                Assert.IsTrue(paths[0].IsClosed, "precision " + fontPrecision);
                GlyphShapeBuilder.ShapeResult res = GlyphShapeBuilder.MakeShape(paths);
                Assert.AreEqual(1.0, res.Shape.Area, 1e-6, "precision " + fontPrecision);
            }
        }

        [TestMethod]
        public void MakeShape_SquareWithHole_IsOneShapeWithOneHole()
        {
            // the orientation of the contours does not matter (as it never did): outer counterclockwise, hole counterclockwise as well
            Path2D[] paths = MakePaths(
                new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) },
                new[] { (0.25, 0.25), (0.75, 0.25), (0.75, 0.75), (0.25, 0.75) });
            CompoundShape cs = GlyphShapeBuilder.MakeShape(paths).Shape;
            Assert.AreEqual(1, cs.SimpleShapes.Length);
            Assert.AreEqual(1, cs.SimpleShapes[0].NumHoles);
            Assert.AreEqual(0.75, cs.Area, 1e-6);
            Assert.IsTrue(CountTriangles(cs) > 0);
        }

        [TestMethod]
        public void MakeShape_IslandInHole_IsTwoShapes()
        {
            // like "©": a ring and a filled square inside the hole of the ring
            Path2D[] paths = MakePaths(
                new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) },
                new[] { (0.1, 0.1), (0.9, 0.1), (0.9, 0.9), (0.1, 0.9) },
                new[] { (0.4, 0.4), (0.6, 0.4), (0.6, 0.6), (0.4, 0.6) });
            CompoundShape cs = GlyphShapeBuilder.MakeShape(paths).Shape;
            Assert.AreEqual(2, cs.SimpleShapes.Length);
            Assert.AreEqual(1.0 - 0.64 + 0.04, cs.Area, 1e-6);
            Assert.IsTrue(CountTriangles(cs) > 0);
        }

        [TestMethod]
        public void MakeShape_OverlappingContours_AreUnited()
        {
            // a "+" made of two overlapping rectangles with the same orientation (as in variable fonts)
            Path2D[] paths = MakePaths(
                new[] { (0.0, 0.4), (1.0, 0.4), (1.0, 0.6), (0.0, 0.6) },
                new[] { (0.4, 0.0), (0.6, 0.0), (0.6, 1.0), (0.4, 1.0) });
            GlyphShapeBuilder.ShapeResult res = GlyphShapeBuilder.MakeShape(paths);
            Assert.AreEqual(1, res.UnitedContours);
            Assert.AreEqual(1, res.Shape.SimpleShapes.Length);
            Assert.AreEqual(0.2 + 0.2 - 0.04, res.Shape.Area, 1e-6); // the overlap is not counted twice
            Assert.IsTrue(CountTriangles(res.Shape) > 0);
        }

        [TestMethod]
        public void MakeShape_OverlappingContourWithOppositeOrientation_IsSubtracted()
        {
            // a bar crossing the outline, drawn in the opposite direction: with the non-zero winding rule it removes the overlap
            Path2D[] paths = MakePaths(
                new[] { (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0) },
                new[] { (0.4, -0.5), (0.4, 1.5), (0.6, 1.5), (0.6, -0.5) });
            GlyphShapeBuilder.ShapeResult res = GlyphShapeBuilder.MakeShape(paths);
            Assert.AreEqual(1, res.SubtractedOverlappingContours);
            Assert.AreEqual(2, res.Shape.SimpleShapes.Length);
            Assert.AreEqual(0.8, res.Shape.Area, 1e-6);
            Assert.IsTrue(CountTriangles(res.Shape) > 0);
        }

        [TestMethod]
        public void MakeShape_SelfOverlappingContour_LikeTheLetterE_GivesAreaWithHole()
        {
            // The "e" of Roboto Flex Light (OFL, https://github.com/googlefonts/roboto-flex), as GDI+ delivers it (em = 1, y downwards):
            // bowl and bar are a single contour which overlaps itself, the eye of the "e" is not a contour of its own.
            // The same construction is used by Bahnschrift Light, Source Sans 3, Recursive, ... and produced no triangles at all.
            (float x, float y, byte t)[] data =
            {
                (0.04150f, 0.67871f, 0), (0.04150f, 0.75618f, 3), (0.06272f, 0.81950f, 3),
                (0.10514f, 0.86865f, 3), (0.14757f, 0.91781f, 3), (0.20855f, 0.94238f, 3),
                (0.28809f, 0.94238f, 3), (0.33605f, 0.94238f, 3), (0.37665f, 0.93254f, 3),
                (0.40991f, 0.91284f, 3), (0.44317f, 0.89315f, 3), (0.46804f, 0.87164f, 3),
                (0.48454f, 0.84831f, 3), (0.43490f, 0.80745f, 1), (0.41916f, 0.82753f, 3),
                (0.39920f, 0.84443f, 3), (0.37500f, 0.85815f, 3), (0.35080f, 0.87188f, 3),
                (0.32183f, 0.87874f, 3), (0.28809f, 0.87874f, 3), (0.22993f, 0.87874f, 3),
                (0.18715f, 0.85962f, 3), (0.15975f, 0.82137f, 3), (0.13235f, 0.78312f, 3),
                (0.11865f, 0.73557f, 3), (0.11865f, 0.67871f, 3), (0.11865f, 0.66309f, 1),
                (0.11865f, 0.60612f, 3), (0.13173f, 0.55854f, 3), (0.15788f, 0.52035f, 3),
                (0.18403f, 0.48215f, 3), (0.22287f, 0.46305f, 3), (0.27441f, 0.46305f, 3),
                (0.32031f, 0.46305f, 3), (0.35476f, 0.47713f, 3), (0.37777f, 0.50529f, 3),
                (0.40077f, 0.53345f, 3), (0.41227f, 0.57086f, 3), (0.41227f, 0.61751f, 3),
                (0.41227f, 0.62451f, 1), (0.08659f, 0.62451f, 1), (0.08659f, 0.68620f, 1),
                (0.48861f, 0.68620f, 1), (0.48861f, 0.65430f, 1), (0.48861f, 0.57487f, 3),
                (0.46994f, 0.51253f, 3), (0.43262f, 0.46729f, 3), (0.39529f, 0.42204f, 3),
                (0.34196f, 0.39941f, 3), (0.27262f, 0.39941f, 3), (0.20307f, 0.39941f, 3),
                (0.14716f, 0.42399f, 3), (0.10490f, 0.47314f, 3), (0.06264f, 0.52230f, 3),
                (0.04150f, 0.58561f, 3), (0.04150f, 0.66309f, 131)
            };
            PointF[] points = new PointF[data.Length];
            byte[] types = new byte[data.Length];
            for (int i = 0; i < data.Length; ++i)
            {
                points[i] = new PointF(data[i].x, data[i].y);
                types[i] = data[i].t;
            }
            foreach (int fontPrecision in new int[] { 0, 1, 2 })
            {
                Path2D[] paths = GlyphShapeBuilder.PathDataToPath2D(points, types, fontPrecision);
                Assert.AreEqual(1, paths.Length);
                Assert.IsTrue(paths[0].IsClosed);
                GlyphShapeBuilder.ShapeResult res = GlyphShapeBuilder.MakeShape(paths);
                Assert.AreEqual(1, res.SelfIntersectingPaths, "precision " + fontPrecision);
                Assert.AreEqual(1, res.Shape.SimpleShapes.Length, "precision " + fontPrecision);
                Assert.AreEqual(1, res.Shape.SimpleShapes[0].NumHoles, "precision " + fontPrecision);
                // the exact area (non-zero winding) of the glyph is 0.10599, the polygon approximation is a little smaller
                Assert.AreEqual(0.106, res.Shape.Area, 0.004, "precision " + fontPrecision);
                Assert.IsTrue(CountTriangles(res.Shape) > 0, "precision " + fontPrecision);
            }
        }

        [TestMethod]
        public void MakeShape_ReversedInnerLoop_IsAHole()
        {
            // a hole drawn as part of the outer contour (as in the "&" of Roboto Flex or the "g" of Recursive): the contour enters
            // the inner area through a narrow "keyhole", the inner loop runs in the opposite direction and the two sides of the
            // keyhole cross each other
            (double x, double y)[] contour =
            {
                (0.0, 0.0), (1.0, 0.0), (1.0, 1.0), (0.0, 1.0), (0.0, 0.55),
                (0.2, 0.45), // into the inner area
                (0.2, 0.8), (0.8, 0.8), (0.8, 0.2), (0.2, 0.2), // the inner loop, clockwise
                (0.3, 0.55),
                (0.0, 0.45) // out again, crossing the way in
            };
            Path2D[] paths = MakePaths(contour);
            GlyphShapeBuilder.ShapeResult res = GlyphShapeBuilder.MakeShape(paths);
            Assert.AreEqual(1, res.SelfIntersectingPaths);
            Assert.AreEqual(1, res.Shape.SimpleShapes.Length);
            Assert.AreEqual(1, res.Shape.SimpleShapes[0].NumHoles);
            Assert.AreEqual(0.649833, res.Shape.Area, 1e-6); // the area with the non-zero winding rule (calculated with skia pathops)
            Assert.IsTrue(CountTriangles(res.Shape) > 0);
        }

        [TestMethod]
        public void MakeShape_FigureEight_BothLobesAreFilled()
        {
            // a contour crossing itself once: both lobes have a non-zero winding number
            (double x, double y)[] contour = { (0.0, 0.0), (1.0, 1.0), (1.0, 0.0), (0.0, 1.0) };
            Path2D[] paths = MakePaths(contour);
            GlyphShapeBuilder.ShapeResult res = GlyphShapeBuilder.MakeShape(paths);
            Assert.AreEqual(1, res.SelfIntersectingPaths);
            Assert.AreEqual(0.5, res.Shape.Area, 1e-6);
            Assert.IsTrue(CountTriangles(res.Shape) > 0);
        }

        [TestMethod]
        public void ResolveSelfIntersections_SimplePolygon_ReturnsNull()
        {
            GeoPoint2D[] square = { new GeoPoint2D(0, 0), new GeoPoint2D(1, 0), new GeoPoint2D(1, 1), new GeoPoint2D(0, 1) };
            Assert.IsNull(GlyphShapeBuilder.ResolveSelfIntersections(square));
        }

        [TestMethod]
        public void WindingNumber_InsideAndOutside()
        {
            GeoPoint2D[] square = { new GeoPoint2D(0, 0), new GeoPoint2D(1, 0), new GeoPoint2D(1, 1), new GeoPoint2D(0, 1) };
            Assert.AreEqual(1, GlyphShapeBuilder.WindingNumber(square, new GeoPoint2D(0.5, 0.5)));
            Assert.AreEqual(0, GlyphShapeBuilder.WindingNumber(square, new GeoPoint2D(1.5, 0.5)));
            Array.Reverse(square);
            Assert.AreEqual(-1, GlyphShapeBuilder.WindingNumber(square, new GeoPoint2D(0.5, 0.5)));
        }
    }
}
