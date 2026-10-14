using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="OffsetSurface.GetModified"/> under a modification that is not isogonal (issue #415). The image of an offset
    /// surface under a scaling with different factors along the axes is no offset surface; it used to be returned as the offset
    /// of the scaled base surface at a single distance (offset times the cube root of the determinant). Now the surface is
    /// approximated in the domain of its face, in the same (u, v) parametrisation, so the point at (u, v) of the modified
    /// surface is the modified point at (u, v) of the original surface.
    /// </summary>
    [TestClass]
    public class OffsetSurfaceModifyTests
    {
        /// <summary>The offset of a cylinder of radius 10 around the z-axis at the distance 2: a cylinder of radius 12.</summary>
        private static Face OffsetCylinderFace()
        {
            CylindricalSurface cylinder = new CylindricalSurface(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, GeoVector.ZAxis);
            return Face.MakeFace(new OffsetSurface(cylinder, 2.0), new BoundingRect(0.0, 0.0, 1.5 * Math.PI, 20.0));
        }

        /// <summary>The offset of a sphere of radius 5 at the distance 1, without the regions around the poles.</summary>
        private static Face OffsetSphereFace()
        {
            SphericalSurface sphere = new SphericalSurface(new GeoPoint(1, 2, 3), 5 * GeoVector.XAxis, 5 * GeoVector.YAxis, 5 * GeoVector.ZAxis);
            return Face.MakeFace(new OffsetSurface(sphere, 1.0), new BoundingRect(0.0, -1.0, Math.PI, 1.0));
        }

        /// <summary>
        /// The modified face: its surface at (u, v) is <paramref name="m"/> applied to the original surface at (u, v), and its
        /// edges lie on its surface.
        /// </summary>
        private static Face AssertModifiedPointwise(Face original, ModOp m)
        {
            Face modified = original.Clone() as Face;
            modified.Modify(m);
            BoundingRect domain = original.Domain;
            for (int i = 0; i <= 10; i++)
            {
                for (int j = 0; j <= 10; j++)
                {
                    GeoPoint2D uv = new GeoPoint2D(domain.Left + i * domain.Width / 10, domain.Bottom + j * domain.Height / 10);
                    GeoPoint expected = m * original.Surface.PointAt(uv);
                    Assert.AreEqual(0.0, modified.Surface.PointAt(uv) | expected, 1e-5, $"point at {uv}");
                }
            }
            foreach (Edge edge in modified.AllEdges)
            {
                for (int k = 0; k <= 4; k++)
                {
                    GeoPoint p = edge.Curve3D.PointAt(k / 4.0);
                    Assert.AreEqual(0.0, p | modified.Surface.PointAt(modified.Surface.PositionOf(p)), 1e-5, "the edges lie on the surface");
                }
            }
            return modified;
        }

        [TestMethod]
        public void an_offset_cylinder_scaled_along_y_keeps_its_offset_in_x_and_doubles_it_in_y()
        {
            Face original = OffsetCylinderFace();
            Face modified = AssertModifiedPointwise(original, ModOp.Scale(1, 2, 1));
            // the offset of 2 stays 2 in x and becomes 4 in y; a single offset would be 2 * 2^(1/3) in both directions
            GeoPoint inX = modified.Surface.PointAt(new GeoPoint2D(0.0, 5.0));
            GeoPoint inY = modified.Surface.PointAt(new GeoPoint2D(Math.PI / 2, 5.0));
            Assert.AreEqual(12.0, inX.x, 1e-5);
            Assert.AreEqual(24.0, inY.y, 1e-5);
        }

        [TestMethod]
        public void an_offset_sphere_scaled_differently_along_each_axis()
        {
            AssertModifiedPointwise(OffsetSphereFace(), ModOp.Scale(2, 1, 0.5));
        }

        /// <summary>A shear used to throw a <see cref="NotImplementedException"/>.</summary>
        [TestMethod]
        public void an_offset_cylinder_sheared()
        {
            ModOp shear = new ModOp(1, 0.5, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0); // x' = x + y / 2
            AssertModifiedPointwise(OffsetCylinderFace(), shear);
        }

        [TestMethod]
        public void an_isogonal_modification_keeps_the_offset_surface()
        {
            Face face = OffsetCylinderFace();
            ModOp m = ModOp.Scale(2.0) * ModOp.Rotate(GeoVector.XAxis, new SweepAngle(0.3));
            Face modified = AssertModifiedPointwise(face, m);
            Assert.IsInstanceOfType(modified.Surface, typeof(OffsetSurface));
            Assert.AreEqual(4.0, (modified.Surface as OffsetSurface).Offset, 1e-12);
        }

        [TestMethod]
        public void the_approximation_has_the_parametrisation_of_the_offset_surface()
        {
            OffsetSurface surface = OffsetSphereFace().Surface as OffsetSurface;
            NurbsSurface approximation = surface.Approximate(0.2, 2.9, -0.8, 1.1, 1e-6);
            Random random = new Random(415);
            for (int i = 0; i < 200; i++)
            {
                GeoPoint2D uv = new GeoPoint2D(0.2 + 2.7 * random.NextDouble(), -0.8 + 1.9 * random.NextDouble());
                Assert.AreEqual(0.0, approximation.PointAt(uv) | surface.PointAt(uv), 1e-6, $"point at {uv}");
            }
        }
    }
}
