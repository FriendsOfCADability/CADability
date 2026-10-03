using System;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Tests for <see cref="Make3D.MakePrism"/> with a closed planar curve as profile. This used to throw a
    /// NullReferenceException for every profile: Face.Set computes the domain of a side face from the 2d curves of its
    /// edges, and the vertical edges got their 2d curves only after Face.Set.
    /// </summary>
    [TestClass]
    public class MakePrismTests
    {
        private static GeoObject.Path Profile(params ICurve[] curves)
        {
            GeoObject.Path profile = GeoObject.Path.Construct();
            Assert.IsTrue(profile.Set(curves), "the profile is not connected");
            return profile;
        }

        private static GeoObject.Path Rectangle() => Profile(
            Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(40, 0, 0)),
            Line.TwoPoints(new GeoPoint(40, 0, 0), new GeoPoint(40, 30, 0)),
            Line.TwoPoints(new GeoPoint(40, 30, 0), new GeoPoint(0, 30, 0)),
            Line.TwoPoints(new GeoPoint(0, 30, 0), new GeoPoint(0, 0, 0)));

        /// <summary>the rectangle 40 x 30 with the corner at (40, 30) rounded by the radius 5</summary>
        private static GeoObject.Path RoundedRectangle()
        {
            Ellipse arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(Plane.XYPlane, new GeoPoint(35, 25, 0), 5, 0.0, Math.PI / 2.0);
            return Profile(
                Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(40, 0, 0)),
                Line.TwoPoints(new GeoPoint(40, 0, 0), new GeoPoint(40, 25, 0)),
                arc,
                Line.TwoPoints(new GeoPoint(35, 30, 0), new GeoPoint(0, 30, 0)),
                Line.TwoPoints(new GeoPoint(0, 30, 0), new GeoPoint(0, 0, 0)));
        }

        private static Solid PrismSolid(ICurve profile, GeoVector extrusion)
        {
            IGeoObject prism = Make3D.MakePrism(profile, extrusion, null, false);
            Assert.IsInstanceOfType(prism, typeof(Solid));
            Shell shell = (prism as Solid).Shells[0];
            Assert.AreEqual(0, shell.OpenEdgesExceptPoles.Length, "the prism has open edges");
            Assert.IsTrue(shell.CheckConsistency(), "the prism is not consistent");
            return prism as Solid;
        }

        [TestMethod]
        public void prism_of_a_rectangle()
        {
            Solid prism = PrismSolid(Rectangle(), 20 * GeoVector.ZAxis);
            Assert.AreEqual(6, prism.Shells[0].Faces.Length);
            Assert.AreEqual(24000, prism.Volume(0.01), 24000 * 1e-6);
        }

        [TestMethod]
        public void prism_of_a_rectangle_extruded_against_the_normal()
        {
            Solid prism = PrismSolid(Rectangle(), -20 * GeoVector.ZAxis);
            Assert.AreEqual(24000, prism.Volume(0.01), 24000 * 1e-6);
        }

        [TestMethod]
        public void prism_of_a_profile_with_an_arc()
        {
            Solid prism = PrismSolid(RoundedRectangle(), 20 * GeoVector.ZAxis);
            Assert.AreEqual(7, prism.Shells[0].Faces.Length);
            double expected = (1200 - 25 + Math.PI * 25 / 4) * 20;
            Assert.AreEqual(expected, prism.Volume(0.01), expected * 1e-6);
        }

        /// <summary>a single closed curve is split into two halves, the side faces meet at a seam of the cylinder</summary>
        [TestMethod]
        public void prism_of_a_circle()
        {
            Ellipse circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(Plane.XYPlane, new GeoPoint(10, 20, 0), 10);
            Solid prism = PrismSolid(circle, 20 * GeoVector.ZAxis);
            double expected = Math.PI * 100 * 20;
            // Volume(0.01) of a cylinder is off by about 1e-6 relative (Make3D.MakeCylinder as well), a finer precision is slow
            Assert.AreEqual(expected, prism.Volume(0.01), expected * 1e-5);
        }

        [TestMethod]
        public void path_to_shell_yields_the_open_side_faces()
        {
            IGeoObject prism = Make3D.MakePrism(RoundedRectangle(), 20 * GeoVector.ZAxis, null, true);
            Assert.IsInstanceOfType(prism, typeof(Shell));
            Shell shell = prism as Shell;
            Assert.AreEqual(5, shell.Faces.Length);
            Assert.AreEqual(10, shell.OpenEdgesExceptPoles.Length, "the bottom and top outline should be open");
        }
    }
}
