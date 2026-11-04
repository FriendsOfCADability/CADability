using System;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// HitTest(BoundingCube, out GeoPoint2D) of the sphere, the torus and the cone (the cylinder has its own tests in
    /// CylindricalSurfaceHitTestTests). It is used by Face.HitBoundingCube to sort faces into an octtree: a missed hit loses the
    /// face in that part of the octtree, a returned point which is not on the surface or not inside the box makes the face test
    /// the wrong point.
    /// </summary>
    [TestClass]
    public class SurfaceHitTestTests
    {
        private static SphericalSurface Sphere(double radius)
            => new SphericalSurface(GeoPoint.Origin, radius * GeoVector.XAxis, radius * GeoVector.YAxis, radius * GeoVector.ZAxis);
        private static ToroidalSurface Torus(double major, double minor)
            => new ToroidalSurface(GeoPoint.Origin, GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, major, minor);

        private static void AssertHit(ISurface surface, BoundingCube box)
        {
            Assert.IsTrue(surface.HitTest(box, out GeoPoint2D uv), "the surface passes through the box, but HitTest says no");
            AssertValidPoint(surface, box, uv, "");
        }
        private static void AssertValidPoint(ISurface surface, BoundingCube box, GeoPoint2D uv, string what)
        {
            GeoPoint p = surface.PointAt(uv);
            Assert.IsTrue(surface.GetDistance(p) < 1e-6, $"{what}the point {p} is not on the surface");
            BoundingCube enlarged = box;
            enlarged.Expand(1e-6 * Math.Max(1.0, box.Size));
            Assert.IsTrue(enlarged.Contains(p), $"{what}the point {p} is not inside the box");
        }

        /// <summary>Neither a corner nor an edge nor an extreme point of the sphere is in the box: missed before.</summary>
        [TestMethod]
        public void SphereSlab() => AssertHit(Sphere(10), new BoundingCube(8, 9, -20, 20, -20, 20));

        [TestMethod]
        public void SphereInsideTheBox() => AssertHit(Sphere(1), new BoundingCube(-5, 5, -5, 5, -5, 5));

        [TestMethod]
        public void SphereNoHit()
        {
            Assert.IsFalse(Sphere(10).HitTest(new BoundingCube(-3, 3, -3, 3, -3, 3), out _), "inside");
            Assert.IsFalse(Sphere(10).HitTest(new BoundingCube(8, 9, 8, 9, -1, 1), out _), "outside, close to the sphere");
        }

        /// <summary>A slab through the tube: the returned point was on the other side of the torus before.</summary>
        [TestMethod]
        public void TorusSlabThroughTheTube() => AssertHit(Torus(10, 2), new BoundingCube(9.9, 10.1, -20, 20, -5, 5));

        /// <summary>A slab across the hole, the torus only crosses the interior of its faces: missed before.</summary>
        [TestMethod]
        public void TorusSlabAcrossTheHole() => AssertHit(Torus(10, 4), new BoundingCube(-30, 30, -30, 30, 0.5, 0.6));

        [TestMethod]
        public void TorusNoHit()
        {
            Assert.IsFalse(Torus(10, 2).HitTest(new BoundingCube(-3, 3, -3, 3, -1, 1), out _), "in the hole");
            Assert.IsFalse(Torus(10, 2).HitTest(new BoundingCube(9.5, 10.5, -0.5, 0.5, -0.5, 0.5), out _), "inside the tube");
        }

        /// <summary>A tilted cone, its axis passes through a slab: the point must be inside the slab.</summary>
        [TestMethod]
        public void ConeTiltedAxisThroughASlab()
        {
            GeoVector z = new GeoVector(0.5, 0, 1); z.Norm();
            GeoVector x = GeoVector.YAxis ^ z; x.Norm();
            GeoVector y = z ^ x;
            AssertHit(new ConicalSurface(GeoPoint.Origin, x, y, z, Math.PI / 4), new BoundingCube(-30, 30, -30, 30, 10, 10.4));
        }

        [TestMethod]
        public void ConeNoHit()
        {
            ConicalSurface cone = new ConicalSurface(GeoPoint.Origin, GeoVector.XAxis, GeoVector.YAxis, GeoVector.ZAxis, Math.PI / 4);
            Assert.IsFalse(cone.HitTest(new BoundingCube(-1, 1, -1, 1, 5, 6), out _), "inside the upper half");
            Assert.IsFalse(cone.HitTest(new BoundingCube(-1, 1, -1, 1, -6, -5), out _), "inside the lower half");
            Assert.IsFalse(cone.HitTest(new BoundingCube(10, 11, -1, 1, -1, 1), out _), "outside");
        }

        /// <summary>
        /// Random boxes against randomly placed spheres, tori and cones, compared with a sampling of the whole box: when samples
        /// are found on both sides of the surface, HitTest must say so, and every returned point must be valid.
        /// </summary>
        [DataTestMethod]
        [DataRow("sphere")]
        [DataRow("torus")]
        [DataRow("cone")]
        public void RandomBoxesAgreeWithSampling(string kind)
        {
            Random random = new Random(4711);
            int hits = 0;
            for (int n = 0; n < 1500; n++)
            {
                GeoVector z = new GeoVector(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);
                z.Norm();
                GeoVector x = z ^ GeoVector.XAxis;
                if (x.Length < 0.1) x = z ^ GeoVector.YAxis;
                x.Norm();
                GeoVector y = z ^ x;
                GeoPoint c = new GeoPoint(random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2);
                ISurface surface;
                switch (kind)
                {
                    case "sphere":
                        double r = 2 + 8 * random.NextDouble();
                        surface = new SphericalSurface(c, r * x, r * y, r * z);
                        break;
                    case "torus":
                        double major = 4 + 6 * random.NextDouble();
                        surface = new ToroidalSurface(c, x, y, z, major, major * (0.1 + 0.8 * random.NextDouble()));
                        break;
                    default:
                        surface = new ConicalSurface(c, x, y, z, 0.2 + random.NextDouble());
                        break;
                }
                double x0 = -15 + 30 * random.NextDouble(), y0 = -15 + 30 * random.NextDouble(), z0 = -15 + 30 * random.NextDouble();
                double size = Math.Pow(2, random.Next(-3, 4));
                BoundingCube box = new BoundingCube(x0, x0 + size * (0.2 + random.NextDouble()), y0, y0 + size * (0.2 + random.NextDouble()),
                    z0, z0 + size * (0.2 + random.NextDouble()));

                bool negative = false, positive = false;
                const int m = 10;
                for (int i = 0; i <= m; i++)
                    for (int j = 0; j <= m; j++)
                        for (int k = 0; k <= m; k++)
                        {
                            GeoPoint p = new GeoPoint(box.Xmin + i * (box.Xmax - box.Xmin) / m, box.Ymin + j * (box.Ymax - box.Ymin) / m,
                                box.Zmin + k * (box.Zmax - box.Zmin) / m);
                            if (surface.Orientation(p) < 0) negative = true; else positive = true;
                        }
                bool hit = surface.HitTest(box, out GeoPoint2D uv);
                if (negative && positive) Assert.IsTrue(hit, $"case {n}: the {kind} passes through the box, but HitTest says no");
                if (hit)
                {
                    AssertValidPoint(surface, box, uv, $"case {n}: ");
                    ++hits;
                }
            }
            Assert.IsTrue(hits > 50, $"only {hits} hits, the random boxes don't test much");
        }
    }
}
