using System.Linq;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Make3D.SewFaces keeps the orientation of an open shell: "outward" has no meaning for it, and feature.split
    /// decides the sides of a split by the orientation of the surface it was given.
    /// </summary>
    [TestClass]
    public class SewFacesOrientationTests
    {
        /// <summary>
        /// A spiral wall of one and a half turns: from its middle the ray along a normal that points to the axis hits
        /// the inner and the outer winding, which the outward test of a closed shell takes for "pointing inside" and
        /// used to turn the face around.
        /// </summary>
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void OpenShellKeepsTheOrientationOfItsFace(bool normalToTheAxis)
        {
            const int n = 31;
            GeoPoint[,] points = new GeoPoint[n, 4];
            for (int i = 0; i < n; i++)
            {
                double a = 3 * Math.PI * i / (n - 1), r = 10 + 4 * a; // Archimedean spiral
                for (int j = 0; j < 4; j++) points[i, j] = new GeoPoint(r * Math.Cos(a), r * Math.Sin(a), 5.0 * j);
            }
            NurbsSurface spiral = new NurbsSurface(points, 3, 3, false, false);
                        BoundingRect domain = new BoundingRect(spiral.UKnots.First(), spiral.VKnots.First(), spiral.UKnots.Last(), spiral.VKnots.Last());
            Face face = Face.MakeFace(spiral, domain);
            if (normalToTheAxis) face.ReverseOrientation();
            GeoPoint2D uv = domain.GetCenter();
            GeoPoint p = face.Surface.PointAt(uv);
            GeoVector before = face.Surface.GetNormal(uv).Normalized;
            GeoVector toAxis = new GeoVector(-p.x, -p.y, 0).Normalized;
            Assert.AreEqual(normalToTheAxis, before * toAxis > 0, "test setup: the normal points to the side it was made for");

            Shell[] shells = Make3D.SewFaces(new[] { face });

            Assert.AreEqual(1, shells.Length);
            Face sewn = shells[0].Faces[0];
            GeoVector after = sewn.Surface.GetNormal(sewn.Surface.PositionOf(p)).Normalized;
            Assert.IsTrue(before * after > 0.99, $"the normal {before} became {after}");
        }

        /// <summary>A closed shell still gets its faces turned outward.</summary>
        [TestMethod]
        public void ClosedShellIsStillOrientedOutward()
        {
            Solid box = Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis);
            Face[] faces = box.Shells[0].Faces.Select(f => (Face)f.Clone()).ToArray();
            foreach (Face f in faces) f.ReverseOrientation(); // all the wrong way round
            Shell[] shells = Make3D.SewFaces(faces);
            Assert.AreEqual(1, shells.Length);
            Assert.AreEqual(1000.0, Solid.MakeSolid(shells[0]).Volume(1e-6), 1e-6);
        }
    }
}
