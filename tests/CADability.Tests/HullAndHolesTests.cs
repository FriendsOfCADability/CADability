using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CADability.Tests
{
    /// <summary>
    /// Checks <see cref="Shell.GetConnectedFaceSets"/> and <see cref="Shell.GetHullAndHoles"/> on shells with cavities.
    /// The hollow shells are assembled manually from a hull and inverted inner shells, so the tests do not depend on the
    /// boolean operations.
    /// </summary>
    [TestClass]
    public class HullAndHolesTests
    {
        private const double TriangulationPrecision = 0.01;

        /// <summary>
        /// Creates a shell consisting of the outer faces of <paramref name="hull"/> and the inverted faces of the
        /// <paramref name="holes"/>, which must be located inside the hull.
        /// </summary>
        private static Shell MakeHollowShell(Solid hull, params Solid[] holes)
        {
            Shell res = hull.Shells[0];
            foreach (Solid hole in holes)
            {
                Shell inner = hole.Shells[0];
                inner.ReverseOrientation(); // the normals of a cavity point into the cavity
                res.AddInnerHole(inner.Faces);
            }
            return res;
        }

        [TestMethod]
        public void SingleShellHasNoHoles()
        {
            Solid box = Make3D.MakeBox(new GeoPoint(1.0, 2.0, 3.0), 10.0 * GeoVector.XAxis, 20.0 * GeoVector.YAxis, 30.0 * GeoVector.ZAxis);
            Shell shell = box.Shells[0];

            List<HashSet<Face>> parts = shell.GetConnectedFaceSets();
            Assert.AreEqual(1, parts.Count, "a box consists of a single connected component");
            Assert.AreEqual(6, parts[0].Count, "the component must contain all faces of the box");

            (HashSet<Face> hull, HashSet<Face>[] holes) = shell.GetHullAndHoles();
            Assert.AreEqual(0, holes.Length, "a box has no holes");
            CollectionAssert.AreEquivalent(shell.Faces, hull.ToArray(), "the hull must contain all faces");
        }

        [TestMethod]
        public void HullAndHolesAreSeparated()
        {
            // a box with a spherical and a cylindrical cavity
            Solid box = Make3D.MakeBox(new GeoPoint(0.0, 0.0, 0.0), 40.0 * GeoVector.XAxis, 40.0 * GeoVector.YAxis, 40.0 * GeoVector.ZAxis);
            Solid sphere = Make3D.MakeSphere(new GeoPoint(10.0, 10.0, 20.0), 5.0);
            Solid cylinder = Make3D.MakeCylinder(new GeoPoint(28.0, 28.0, 8.0), 4.0 * GeoVector.XAxis, 20.0 * GeoVector.ZAxis);
            HashSet<Face> hullFaces = box.Shells[0].Faces.ToHashSet();
            HashSet<Face> sphereFaces = sphere.Shells[0].Faces.ToHashSet();
            HashSet<Face> cylinderFaces = cylinder.Shells[0].Faces.ToHashSet();
            Shell shell = MakeHollowShell(box, sphere, cylinder);

            List<HashSet<Face>> parts = shell.GetConnectedFaceSets();
            Assert.AreEqual(3, parts.Count, "hull and two cavities are three connected components");
            Assert.AreEqual(shell.Faces.Length, parts.Sum(p => p.Count), "each face must be contained in exactly one component");

            (HashSet<Face> hull, HashSet<Face>[] holes) = shell.GetHullAndHoles();
            CollectionAssert.AreEquivalent(hullFaces.ToArray(), hull.ToArray(), "the hull must consist of the faces of the box");
            Assert.AreEqual(2, holes.Length, "the two cavities must be reported as holes");
            Assert.IsTrue(holes[0].SetEquals(sphereFaces) && holes[1].SetEquals(cylinderFaces)
                       || holes[0].SetEquals(cylinderFaces) && holes[1].SetEquals(sphereFaces), "the holes must be the sphere and the cylinder");

            // the hull encloses a positive, the cavities enclose a negative volume
            Assert.IsTrue(Shell.SignedVolume(hull, TriangulationPrecision) > 0.0, "the hull must enclose a positive volume");
            Assert.IsTrue(Shell.SignedVolume(holes[0], TriangulationPrecision) < 0.0, "a hole must enclose a negative volume");
            Assert.IsTrue(Shell.SignedVolume(holes[1], TriangulationPrecision) < 0.0, "a hole must enclose a negative volume");
        }

        [TestMethod]
        public void HullIsNotTheFirstFace()
        {
            // the same body, but the faces of a cavity come first. The hull must be found by the sign of the volume,
            // not by the position in the face array
            Solid box = Make3D.MakeBox(new GeoPoint(0.0, 0.0, 0.0), 40.0 * GeoVector.XAxis, 40.0 * GeoVector.YAxis, 40.0 * GeoVector.ZAxis);
            Solid sphere = Make3D.MakeSphere(new GeoPoint(20.0, 20.0, 20.0), 8.0);
            HashSet<Face> hullFaces = box.Shells[0].Faces.ToHashSet();
            Shell shell = MakeHollowShell(box, sphere);
            Face[] reordered = shell.Faces.Reverse().ToArray(); // the sphere was added last, so now it comes first
            Assert.IsFalse(hullFaces.Contains(reordered[0]), "the first face must not belong to the hull for this test");
            shell.SetFaces(reordered);

            (HashSet<Face> hull, HashSet<Face>[] holes) = shell.GetHullAndHoles();
            Assert.AreEqual(1, holes.Length);
            CollectionAssert.AreEquivalent(hullFaces.ToArray(), hull.ToArray(), "the hull must consist of the faces of the box");
        }

        [TestMethod]
        public void InvertedShellHullIsTheOutermostPart()
        {
            // an inverted hollow shell, as it is used as an intermediate result of union and difference: all signs are
            // reversed, the outermost component now encloses a negative volume
            Solid box = Make3D.MakeBox(new GeoPoint(0.0, 0.0, 0.0), 40.0 * GeoVector.XAxis, 40.0 * GeoVector.YAxis, 40.0 * GeoVector.ZAxis);
            Solid sphere = Make3D.MakeSphere(new GeoPoint(20.0, 20.0, 20.0), 8.0);
            HashSet<Face> hullFaces = box.Shells[0].Faces.ToHashSet();
            Shell shell = MakeHollowShell(box, sphere);
            shell.ReverseOrientation();

            (HashSet<Face> hull, HashSet<Face>[] holes) = shell.GetHullAndHoles();
            Assert.AreEqual(1, holes.Length);
            CollectionAssert.AreEquivalent(hullFaces.ToArray(), hull.ToArray(), "the outermost component must be reported as the hull");
        }
    }
}
