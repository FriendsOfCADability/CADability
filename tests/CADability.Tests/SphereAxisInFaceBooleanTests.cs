using CADability.Curve2D;
using CADability.GeoObject;
using System.Collections.Generic;
using System.Linq;

namespace CADability.Tests
{
    /// <summary>
    /// Boolean operations of a cube of 10 with a sphere whose axis lies in a face of the cube. The plane of that face then
    /// cuts the sphere in a great circle through both poles, two meridians in the uv space of the sphere.
    /// <list type="bullet">
    /// <item>A sphere centred on the middle of an edge or on a corner, with its axis parallel to an edge, had neither union
    /// nor difference: <c>SphericalSurface.HitTest(BoundingCube)</c> missed slabs through the sphere, so faces were lost in
    /// the octtree. Fixed by the exact HitTest of #521.</item>
    /// <item>With the axis tilted in the face, the union and the difference of a sphere on the middle of the face were
    /// null: at a pole the u of a point is arbitrary, and the pole came out at u = pi/2. <c>Surfaces.Intersect</c> widened
    /// the bounds of the half sphere u = pi..2pi to include it, so that half also got the meridian of the other half.</item>
    /// <item>A sphere centred on a corner with its axis tilted in the face x = 10 gave only the sphere as the union: the edge
    /// of the cube along x crosses the seam of the sphere exactly, at (10 - r, 10, 10), and the intersection curves on one half
    /// sphere form a loop that touches its outline there. The test whether this hole lies in the outline used its start
    /// point, the touching vertex, and failed; the hole is now tested with an inner point and inserted into the outline.</item>
    /// <item>The difference of the cube and a sphere on the middle of an edge, tilted by 1.2 about x, was 0.08 % too small:
    /// <c>Shell.CombineConnectedFaces</c> merged the two parts of the sphere across its seam, and the pole edge of one part,
    /// which spans its u range at the pole, shrank to a point. Faces with a pole edge are no longer combined.</item>
    /// <item>The union with a sphere on a corner rotated by 0.7 about (1, 2, 3) was 1e-4 too large: <c>Face.ReverseOrientation</c>
    /// kept the arc approximation of a 2d curve close to the pole, which was 0.16 off the edge, because it compared the exact
    /// curve with that approximation, not with the curve before the reversal.</item>
    /// </list>
    /// </summary>
    [TestClass]
    public class SphereAxisInFaceBooleanTests
    {
        private static Solid Cube() => Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis);

        /// <summary>
        /// The exact volumes of the union and the difference, when the part <paramref name="fractionInside"/> of the sphere
        /// lies inside the cube.
        /// </summary>
        private static void AssertVolumes(Solid sphere, double radius, double fractionInside, string what)
        {
            double sphereVolume = 4.0 / 3.0 * Math.PI * radius * radius * radius;
            double union = 1000 + sphereVolume * (1 - fractionInside);
            double difference = 1000 - sphereVolume * fractionInside;
            Solid u = Solid.Unite(Cube(), sphere.Clone() as Solid);
            Assert.IsNotNull(u, what + ": no union");
            Assert.AreEqual(union, u.Volume(1e-6), 1e-5 * union, what + ": volume of the union");
            Solid[] d = Solid.Subtract(Cube(), sphere.Clone() as Solid);
            Assert.AreEqual(1, d.Length, what + ": number of solids of the difference");
            Assert.AreEqual(difference, d[0].Volume(1e-6), 1e-5 * difference, what + ": volume of the difference");
        }

        [TestMethod]
        public void SphereCentredOnTheMiddleOfAnEdge()
        {
            AssertVolumes(Make3D.MakeSphere(new GeoPoint(10, 5, 10), 3), 3, 0.25, "sphere on the middle of an edge");
        }

        [TestMethod]
        public void SphereCentredOnACorner()
        {
            AssertVolumes(Make3D.MakeSphere(new GeoPoint(10, 10, 10), 4), 4, 0.125, "sphere on a corner");
        }

        [TestMethod]
        [DataRow(0.1)]
        [DataRow(0.3)]
        [DataRow(0.7)]
        [DataRow(1.2)]
        public void SphereWithItsAxisTiltedInAFace(double angle)
        {
            GeoPoint centre = new GeoPoint(10, 5, 5);
            Solid sphere = Make3D.MakeSphere(centre, 3);
            sphere.Modify(ModOp.Rotate(centre, GeoVector.XAxis, new Angle(angle))); // the axis stays in the plane x = 10
            AssertVolumes(sphere, 3, 0.5, $"sphere tilted by {angle}");
        }

        [TestMethod]
        [DataRow(2.0, 0.3)]
        [DataRow(3.0, 0.1)]
        [DataRow(3.0, 0.7)]
        [DataRow(4.0, 1.2)]
        public void SphereOnACornerWithTheEdgeCrossingItsSeam(double radius, double angle)
        {
            GeoPoint centre = new GeoPoint(10, 10, 10);
            Solid sphere = Make3D.MakeSphere(centre, radius);
            sphere.Modify(ModOp.Rotate(centre, GeoVector.XAxis, new Angle(angle))); // the seam stays at (10 - r, 10, 10), on an edge of the cube
            AssertVolumes(sphere, radius, 0.125, $"sphere of radius {radius} on a corner, tilted by {angle}");
        }

        [TestMethod]
        public void SphereOnAnEdgeWithItsPoleOnTheIntersection()
        {
            GeoPoint centre = new GeoPoint(10, 5, 10);
            Solid sphere = Make3D.MakeSphere(centre, 3);
            sphere.Modify(ModOp.Rotate(centre, GeoVector.XAxis, new Angle(1.2))); // a pole at (10, 7.8, 8.9), on the face x = 10
            AssertVolumes(sphere, 3, 0.25, "sphere on an edge, tilted by 1.2");
            Solid[] d = Solid.Subtract(Cube(), sphere);
            foreach (Face face in d[0].Shells[0].Faces)
            {   // the pole edge must not have shrunk to a point and become a hole
                Assert.AreEqual(0, face.HoleCount, "a face of the difference has a hole");
            }
        }

        [TestMethod]
        [DataRow(3.0)]
        [DataRow(4.0)]
        public void SphereOnACornerRotatedInSpace(double radius)
        {
            GeoPoint centre = new GeoPoint(10, 10, 10);
            Solid sphere = Make3D.MakeSphere(centre, radius);
            sphere.Modify(ModOp.Rotate(centre, new GeoVector(1, 2, 3), new Angle(0.7)));
            Solid union = Solid.Unite(Cube(), sphere);
            Assert.IsNotNull(union);
            foreach (Face face in union.Shells[0].Faces)
            {   // the 2d curves of the faces must follow their edges; one of them was a coarse arc approximation close to the pole
                foreach (Edge edge in face.AllEdges)
                {
                    if (edge.Curve3D == null) continue;
                    ICurve2D c2d = edge.Curve2D(face);
                    for (int i = 0; i <= 32; i++)
                    {
                        double d = edge.Curve3D.DistanceTo(face.Surface.PointAt(c2d.PointAt(i / 32.0)));
                        Assert.IsTrue(d < 1e-4, $"the 2d curve of an edge of a {face.Surface.GetType().Name} is {d} off the edge");
                    }
                }
            }
            double sphereVolume = 4.0 / 3.0 * Math.PI * radius * radius * radius;
            double expected = 1000 + sphereVolume * 7.0 / 8.0;
            Assert.AreEqual(expected, union.Volume(1e-6), 3e-5 * expected, "volume of the union");
        }

        /// <summary>
        /// Each half of the tilted sphere must get only its own meridian when the poles are passed as points of the
        /// intersection, as the boolean operation does.
        /// </summary>
        [TestMethod]
        public void PolesDoNotWidenTheBoundsOfAHalfSphere()
        {
            GeoPoint centre = new GeoPoint(10, 5, 5);
            Solid sphere = Make3D.MakeSphere(centre, 3);
            sphere.Modify(ModOp.Rotate(centre, GeoVector.XAxis, new Angle(0.1)));
            Face plane = Cube().Shells[0].Faces.Single(f => f.Surface is PlaneSurface ps && Math.Abs(ps.Plane.Distance(centre)) < 1e-9);
            foreach (Face half in sphere.Shells[0].Faces)
            {
                BoundingRect bounds = half.Area.GetExtent();
                List<GeoPoint> poles = half.AllEdges.Where(e => e.Curve3D == null).Select(e => e.Vertex1.Position).ToList();
                Assert.AreEqual(2, poles.Count);
                Assert.IsTrue(Surfaces.Intersect(half.Surface, bounds, plane.Surface, plane.Area.GetExtent(), poles,
                    out ICurve[] curves, out ICurve2D[] _, out ICurve2D[] _, out double[,] _, out double[,] _, out double[,] _,
                    out GeoPoint2D[] _, out GeoPoint2D[] _, Precision.eps));
                Assert.AreEqual(1, curves.Length, "a half sphere got the meridian of the other half");
                GeoPoint2D mid = half.Surface.PositionOf(curves[0].PointAt(0.5));
                SurfaceHelper.AdjustPeriodic(half.Surface, bounds, ref mid);
                Assert.IsTrue(bounds.Contains(mid), "the meridian is not on this half");
            }
        }

        /// <summary>
        /// The position of a point on a <see cref="ProjectedCurve"/> follows exactly from its 3d curve. It was interpolated
        /// from the 2d points and up to a few 1e-6 off, so the 2d curves of the intersection edges, which the boolean
        /// operation trims at their vertices, ended beside them.
        /// </summary>
        [TestMethod]
        public void PositionOnAProjectedCurveIsExact()
        {
            GeoPoint centre = new GeoPoint(10, 10, 10);
            Solid sphere = Make3D.MakeSphere(centre, 3);
            sphere.Modify(ModOp.Rotate(centre, GeoVector.XAxis, new Angle(0.3)));
            ISurface surface = sphere.Shells[0].Faces[0].Surface;
            Ellipse circle = Ellipse.Construct(); // in the plane y = 10, neither a meridian nor a circle of latitude of the sphere
            circle.SetArcPlaneCenterRadiusAngles(new Plane(centre, GeoVector.XAxis, GeoVector.ZAxis), centre, 3.0, 2.0, 1.5);
            ProjectedCurve projected = new ProjectedCurve(circle, surface, true, sphere.Shells[0].Faces[0].Area.GetExtent());
            foreach (double t in new[] { 0.0, 0.13, 0.37, 0.5, 0.81, 1.0 })
            {
                GeoPoint2D uv = surface.PositionOf(circle.PointAt(t));
                double position = projected.PositionOf(uv);
                Assert.AreEqual(t, position, 1e-10, $"position of the point at {t}");
                Assert.AreEqual(0.0, surface.PointAt(projected.PointAt(position)) | circle.PointAt(t), 1e-10);
            }
        }
    }
}
