using CADability.GeoObject;
using CADability.Shapes;
using System;

namespace CADability.Tests
{
    /// <summary>
    /// The triangulation of a face on a <see cref="SurfaceOfRevolution"/> made from a spline overestimated its area, the
    /// more the larger the angle of the rotation: 951.5 instead of 946.4 for half a turn, 1451.9 instead of 1419.6 for
    /// three quarters. The ear clipping of the densely sampled outline made long and thin fans, splitting their edges kept
    /// the triangles thin, and thin triangles overlapped and folded over their neighbours while staying within the
    /// deflection along their edges. Finer triangulations of such faces also ran into the limit of 100000 triangles and
    /// stopped refining.
    /// </summary>
    [TestClass]
    public class TriangulationTests
    {
        private static Face RevolvedSpline(double sweep)
        {
            BSpline bsp = BSpline.Construct();
            bsp.ThroughPoints(new[] { new GeoPoint(5, 0, 0), new GeoPoint(12, 0, 5), new GeoPoint(8, 0, 12), new GeoPoint(15, 0, 20) }, 3, false);
            SurfaceOfRevolution surface = new SurfaceOfRevolution(bsp, GeoPoint.Origin, GeoVector.ZAxis);
            // the spline is parametrized from 0 to 1, u is the angle of the rotation around the z-axis
            return Face.MakeFace(surface, new SimpleShape(Border.MakeRectangle(0, sweep, 0, 1)));
        }

        /// <summary>The area the spline sweeps, by Pappus's theorem: the angle times the integral of r ds.</summary>
        private static double PappusArea(Face face, double sweep)
        {
            ICurve curve = ((SurfaceOfRevolution)face.Surface).BasisCurve;
            const int n = 20000;
            double moment = 0.0;
            GeoPoint last = curve.PointAt(0.0);
            for (int i = 1; i <= n; i++)
            {
                GeoPoint p = curve.PointAt(i / (double)n);
                GeoPoint m = new GeoPoint(last, p);
                moment += Math.Sqrt(m.x * m.x + m.y * m.y) * (p | last);
                last = p;
            }
            return sweep * moment;
        }

        [TestMethod]
        [DataRow(Math.PI / 2, 0.01)]
        [DataRow(Math.PI, 0.01)]
        [DataRow(1.5 * Math.PI, 0.01)]
        [DataRow(Math.PI, 0.002)]
        [DataRow(1.5 * Math.PI, 0.002)]
        public void RevolvedSplineHasTheAreaOfTheSurface(double sweep, double precision)
        {
            Face face = RevolvedSpline(sweep);
            face.GetTriangulation(precision, out GeoPoint[] points, out GeoPoint2D[] uv, out int[] triangles, out _);
            double area = 0.0, foldedArea = 0.0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                GeoVector normal = (points[triangles[i + 1]] - points[triangles[i]]) ^ (points[triangles[i + 2]] - points[triangles[i]]);
                area += normal.Length / 2.0;
                // the triangles are counter-clockwise in the parameter space, so their normals point to the side of the
                // surface normal, unless they are folded over
                GeoPoint2D center = new GeoPoint2D(uv[triangles[i]], uv[triangles[i + 1]], uv[triangles[i + 2]]);
                if (normal * face.Surface.GetNormal(center) < 0) foldedArea += normal.Length / 2.0;
            }
            double expected = PappusArea(face, sweep);
            Assert.AreEqual(expected, area, 1e-3 * expected, "area");
            Assert.IsTrue(foldedArea < 1e-4 * expected, $"folded triangles with an area of {foldedArea}");
        }
    }
}
