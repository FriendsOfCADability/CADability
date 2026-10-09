using CADability.GeoObject;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="Shell.CloseOpenEdges"/>, the repair the STEP import applies to a CLOSED_SHELL with open edges. A loop of open
    /// edges on the common surface of its faces is closed by a new face on that surface, if the loop is a hole between the
    /// faces. If the loop is the outline of the faces (e.g. a single planar face), it used to be closed in the same way, which
    /// added a coincident face with the same orientation: an inconsistent face and a "solid" without volume.
    /// </summary>
    [TestClass]
    public class ShellCloseOpenEdgesTests
    {
        private static Face Rectangle(double w, double h) => Face.MakeFace(new PlaneSurface(Plane.XYPlane), new SimpleShape(Border.MakeRectangle(0, w, 0, h)));

        [TestMethod]
        public void a_hole_is_closed_but_the_outline_of_a_face_is_not()
        {
            // a box 100 x 60 x 10, whose top face has a circular hole: the disc, which fills the hole, is missing
            Solid box = Make3D.MakePrism(Rectangle(100, 60), new GeoVector(0, 0, 10), null) as Solid;
            List<Face> faces = box.Shells[0].Faces.Select(face => (Face)face.Clone()).ToList();
            faces.Remove(faces.Single(face => face.GetExtent(0.0).Zmin > 5));
            Face top = Face.MakeFace(new PlaneSurface(new Plane(new GeoPoint(0, 0, 10), GeoVector.XAxis, GeoVector.YAxis)),
                new SimpleShape(Border.MakeRectangle(0, 100, 0, 60), Border.MakeCircle(new GeoPoint2D(50, 30), 10)));
            faces.Add(top);
            Shell withHole = Shell.FromFaces(faces.ToArray(), true);
            Assert.AreEqual(1, withHole.OpenEdges.Length, "the circle of the hole");
            Assert.IsTrue(withHole.CloseOpenEdges());
            Assert.AreEqual(7, withHole.Faces.Length);
            Assert.IsTrue(withHole.Faces.All(face => face.CheckConsistency()));
            Solid closed = Solid.MakeSolid(withHole);
            Assert.AreEqual(100 * 60 * 10, closed.Volume(1e-4), 1e-6);

            // a single rectangle: there is nothing that could close it
            Shell single = Shell.FromFaces(new Face[] { Rectangle(100, 60) }, true);
            Assert.AreEqual(4, single.OpenEdges.Length);
            Assert.IsFalse(single.CloseOpenEdges());
            Assert.AreEqual(1, single.Faces.Length, "no coincident face is added");
            Assert.AreEqual(4, single.OpenEdges.Length);
            Assert.IsTrue(single.Faces[0].CheckConsistency());
        }
    }
}
