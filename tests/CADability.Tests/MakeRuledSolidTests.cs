using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #424: <see cref="Make3D.MakeRuledSolid"/> and <see cref="Make3D.MakeRuledShell"/> threw
    /// <c>NotImplementedException</c> when the paths could not be connected, although their documentation promised null
    /// and the construct actions test for null.
    /// </summary>
    [TestClass]
    public class MakeRuledSolidTests
    {
        private static GeoObject.Path Polygon(double z, params GeoPoint2D[] corners)
        {
            ICurve[] curves = new ICurve[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                GeoPoint2D a = corners[i];
                GeoPoint2D b = corners[(i + 1) % corners.Length];
                curves[i] = Line.MakeLine(new GeoPoint(a.x, a.y, z), new GeoPoint(b.x, b.y, z));
            }
            GeoObject.Path path = GeoObject.Path.Construct();
            path.Set(curves);
            return path;
        }

        private static GeoObject.Path OpenPolyline(double z, params GeoPoint2D[] points)
        {
            ICurve[] curves = new ICurve[points.Length - 1];
            for (int i = 0; i < curves.Length; i++)
            {
                curves[i] = Line.MakeLine(new GeoPoint(points[i].x, points[i].y, z), new GeoPoint(points[i + 1].x, points[i + 1].y, z));
            }
            GeoObject.Path path = GeoObject.Path.Construct();
            path.Set(curves);
            return path;
        }

        private static readonly GeoPoint2D[] square = { new GeoPoint2D(0, 0), new GeoPoint2D(10, 0), new GeoPoint2D(10, 10), new GeoPoint2D(0, 10) };
        private static readonly GeoPoint2D[] triangle = { new GeoPoint2D(0, 0), new GeoPoint2D(10, 0), new GeoPoint2D(5, 10) };

        [TestMethod]
        public void TwoSquaresGiveABox()
        {
            Solid solid = Make3D.MakeRuledSolid(Polygon(0.0, square), Polygon(5.0, square), null);
            Assert.IsNotNull(solid);
            Assert.AreEqual(500.0, solid.Volume(1e-6), 1e-6);
        }

        [TestMethod]
        public void DifferentNumberOfSegmentsGivesNull()
        {
            Assert.IsNull(Make3D.MakeRuledSolid(Polygon(0.0, square), Polygon(5.0, triangle), null));
            Assert.IsNull(Make3D.MakeRuledShell(Polygon(0.0, square), Polygon(5.0, triangle), null));
        }

        [TestMethod]
        public void OpenPathGivesNoSolidButAShell()
        {
            GeoObject.Path lower = OpenPolyline(0.0, new GeoPoint2D(0, 0), new GeoPoint2D(10, 0), new GeoPoint2D(10, 10));
            GeoObject.Path upper = OpenPolyline(5.0, new GeoPoint2D(0, 0), new GeoPoint2D(10, 0), new GeoPoint2D(10, 10));
            Assert.IsNull(Make3D.MakeRuledSolid(lower, upper, null), "an open path does not bound a solid");
            IGeoObject shell = Make3D.MakeRuledShell(lower, upper, null);
            Assert.IsInstanceOfType(shell, typeof(Shell));
            Assert.AreEqual(2, ((Shell)shell).Faces.Length, "one ruled face per pair of segments");
        }
    }
}
