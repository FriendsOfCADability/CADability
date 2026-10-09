using System.Runtime.CompilerServices;
using CADability.GeoObject;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// Regression tests with the files attached to the issues, in addition to the synthetic cases in
    /// <see cref="PlaneFromPointsTests"/> and <see cref="NurbsSurfaceSimpleSurfaceTests"/>.
    /// </summary>
    [TestClass]
    public class IssueFilesRegressionTests
    {
        private static string TestFile(string folder, string name, [CallerFilePath] string thisFile = "")
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile), "Files", folder, name);

        private static IEnumerable<IGeoObject> Flattened(IEnumerable<IGeoObject> objects)
        {
            foreach (IGeoObject go in objects)
            {
                yield return go;
                if (go is Block block) foreach (IGeoObject child in Flattened(block.Children)) yield return child;
                if (go is GeoObject.Path path) foreach (IGeoObject child in Flattened(path.Curves.Cast<IGeoObject>())) yield return child;
            }
        }

        /// <summary>
        /// Issue #147: splines about 366000 units away from the origin were reported as UnderDetermined, because
        /// Plane.FromPoints fitted the uncentred coordinates, and were flattened onto a line ("1095": the "0" was drawn as
        /// a horizontal line, 6.4 x 0.5 instead of 6.4 x 9.6).
        /// </summary>
        [DataTestMethod]
        [DataRow("issue147.dxf")]
        [DataRow("issue147_2.dxf")]
        [DataRow("issue147_dreieck.dxf")]
        public void splines_far_from_the_origin_keep_their_shape(string fileName)
        {
            string file = TestFile("Dxf", fileName);
            Assert.IsTrue(File.Exists(file), $"test file not found: {file}");
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            List<BSpline> splines = Flattened(project.GetActiveModel().AllObjects).OfType<BSpline>().ToList();
            Assert.IsTrue(splines.Count > 0, "the file contains splines");
            foreach (BSpline bsp in splines)
            {
                ICurve curve = bsp;
                Assert.AreEqual(PlanarState.Planar, curve.GetPlanarState());
                Assert.AreEqual(1.0, curve.GetPlane().Normal.z, 1e-12, "normal " + curve.GetPlane().Normal.ToString());
                BoundingCube poles = BoundingCube.EmptyBoundingCube;
                foreach (GeoPoint p in bsp.Poles) poles.MinMax(p);
                BoundingCube points = BoundingCube.EmptyBoundingCube;
                for (int i = 0; i <= 400; i++) points.MinMax(curve.PointAt(i / 400.0));
                // a curve lies within the convex hull of its poles; a collapsed curve spans only a fraction of it in one direction
                Assert.IsTrue(points.Xmax - points.Xmin > 0.5 * (poles.Xmax - poles.Xmin), $"width {points.Xmax - points.Xmin} of poles' {poles.Xmax - poles.Xmin}");
                Assert.IsTrue(points.Ymax - points.Ymin > 0.5 * (poles.Ymax - poles.Ymin), $"height {points.Ymax - points.Ymin} of poles' {poles.Ymax - poles.Ymin}");
                Assert.AreEqual(0.0, points.Zmax - points.Zmin, 1e-9, "the curve stays flat");
            }
        }

        /// <summary>
        /// Issue #204: the plane of the outline in X-Y_Inverted.dxf came out with the normal (1.15e-15, 2e-17, -1) and the
        /// x-axis (0, -1, 0), so the shape was mirrored and its arcs were exported with the extrusion -Z.
        /// </summary>
        [TestMethod]
        public void the_outline_of_a_drawing_in_the_xy_plane_gets_the_positive_z_plane()
        {
            string file = TestFile("Dxf", "issue204.dxf");
            Assert.IsTrue(File.Exists(file), $"test file not found: {file}");
            Project project = Project.ReadFromFile(file, "dxf");
            Assert.IsNotNull(project);
            GeoObjectList contours = new GeoObjectList(project.GetActiveModel().AllObjects.Where(go => go.Layer?.Name == "Outer_Loop").ToList());
            Assert.IsTrue(contours.Count > 0, "the file has curves on the layer Outer_Loop");
            CompoundShape shape = CompoundShape.CreateFromList(contours, 0.001, out Plane plane);
            Assert.IsNotNull(shape);
            Assert.AreEqual(1.0, plane.Normal.z, 1e-12, "normal " + plane.Normal.ToString());
            Assert.AreEqual(1.0, plane.DirectionX.x, 1e-9, "x-axis " + plane.DirectionX.ToString());
        }

        /// <summary>
        /// Issue #347: a NURBS face of about 1.2e-6 x 1 parameter units, singular at umax, made GetSimpleSurface and the
        /// triangulation loop forever while importing a STEP file. The face was extracted into a cdb file.
        /// </summary>
        [TestMethod]
        public void a_tiny_singular_nurbs_face_can_be_simplified_and_triangulated()
        {
            string file = TestFile("CDB", "issue347.cdb.json");
            Assert.IsTrue(File.Exists(file), $"test file not found: {file}");
            Project project = null;
            try
            {
                project = Project.ReadFromFile(file, "cdb");
            }
            catch (TypeInitializationException e) when (e.GetBaseException() is PlatformNotSupportedException)
            {
                Assert.Inconclusive("reading a project needs System.Drawing printing, which is only available on Windows");
            }
            Assert.IsNotNull(project);
            List<Face> faces = new List<Face>();
            foreach (IGeoObject go in project.GetActiveModel().AllObjects)
            {
                if (go is Face f) faces.Add(f);
                else if (go is Shell sh) faces.AddRange(sh.Faces);
                else if (go is Solid so) foreach (Shell s in so.Shells) faces.AddRange(s.Faces);
            }
            Face face = faces.Single();
            NurbsSurface surface = face.Surface as NurbsSurface;
            Assert.IsNotNull(surface, "the face has a NURBS surface");

            Task simplify = Task.Factory.StartNew(() => surface.GetSimpleSurface(Precision.eps, out ISurface _, out ModOp2D _), TaskCreationOptions.LongRunning);
            Assert.IsTrue(simplify.Wait(20000), "GetSimpleSurface did not return (infinite loop, issue #347)");
            int triangles = 0;
            Task triangulate = Task.Factory.StartNew(() =>
            {
                face.GetTriangulation(0.01, out GeoPoint[] _, out GeoPoint2D[] _, out int[] index, out BoundingCube _);
                triangles = index.Length / 3;
            }, TaskCreationOptions.LongRunning);
            Assert.IsTrue(triangulate.Wait(20000), "the triangulation did not return (infinite loop, issue #347)");
            Assert.IsTrue(triangles > 0, "the face is triangulated");
        }
    }
}
