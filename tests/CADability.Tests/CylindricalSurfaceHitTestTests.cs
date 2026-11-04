using System;
using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// CylindricalSurface.HitTest(BoundingCube, out GeoPoint2D) decides whether the unbounded cylinder passes through a box
    /// (used by Face.HitBoundingCube when the faces are sorted into an octtree). A missed hit loses the face in that part of the
    /// octtree, so the test must never say no when the cylinder passes through the box.
    /// </summary>
    [TestClass]
    public class CylindricalSurfaceHitTestTests
    {
        private static CylindricalSurface Cylinder(double radius = 10.0)
            => new CylindricalSurface(GeoPoint.Origin, radius * GeoVector.XAxis, radius * GeoVector.YAxis, GeoVector.ZAxis);

        /// <summary>The point of the result must be on the cylinder and inside the box.</summary>
        private static void AssertValidHit(ISurface cylinder, BoundingCube box, GeoPoint2D uv)
        {
            GeoPoint p = cylinder.PointAt(uv);
            Assert.IsTrue(cylinder.GetDistance(p) < 1e-6, $"the point {p} is not on the cylinder");
            BoundingCube enlarged = box;
            enlarged.Expand(1e-6);
            Assert.IsTrue(enlarged.Contains(p), $"the point {p} is not inside the box");
        }

        [TestMethod]
        public void BoxInsideTheCylinderIsNoHit()
        {
            Assert.IsFalse(Cylinder().HitTest(new BoundingCube(-3, 3, -3, 3, 0, 100), out _));
        }

        [TestMethod]
        public void BoxFarOutsideIsNoHit()
        {
            Assert.IsFalse(Cylinder().HitTest(new BoundingCube(20, 30, 20, 30, 0, 1), out _));
        }

        [TestMethod]
        public void BoxBesideTheCylinderIsNoHit()
        {   // the projection of the box comes close to the cylinder, but stays outside
            Assert.IsFalse(Cylinder().HitTest(new BoundingCube(-20, 20, 10.5, 12, 0, 1), out _));
        }

        /// <summary>
        /// All corners are outside, but the edges along x pass through the cylinder: the case the former implementation missed.
        /// </summary>
        [TestMethod]
        public void EdgeThroughTheCylinderWithAllCornersOutsideIsAHit()
        {
            CylindricalSurface cylinder = Cylinder();
            BoundingCube box = new BoundingCube(-20, 20, 8, 9, 0, 1);
            Assert.IsTrue(cylinder.HitTest(box, out GeoPoint2D uv));
            AssertValidHit(cylinder, box, uv);
        }

        /// <summary>The axis passes through the box, all corners are outside.</summary>
        [TestMethod]
        public void AxisThroughTheBoxIsAHit()
        {
            CylindricalSurface cylinder = Cylinder();
            BoundingCube box = new BoundingCube(-20, 20, -20, 20, 5, 6);
            Assert.IsTrue(cylinder.HitTest(box, out GeoPoint2D uv));
            AssertValidHit(cylinder, box, uv);
        }

        [TestMethod]
        public void BoxAcrossTheWallIsAHit()
        {
            CylindricalSurface cylinder = Cylinder();
            BoundingCube box = new BoundingCube(9, 11, -1, 1, 0, 1);
            Assert.IsTrue(cylinder.HitTest(box, out GeoPoint2D uv));
            AssertValidHit(cylinder, box, uv);
        }

        [TestMethod]
        public void BoxTouchingTheCylinderIsAHit()
        {
            CylindricalSurface cylinder = Cylinder();
            BoundingCube box = new BoundingCube(-1, 1, 10, 11, 0, 1); // touches the cylinder along the line x == 0, y == 10
            Assert.IsTrue(cylinder.HitTest(box, out GeoPoint2D uv));
            AssertValidHit(cylinder, box, uv);
        }

        /// <summary>
        /// Random boxes against tilted, elliptical cylinders: whenever samples on the surface of the box are found on both sides
        /// of the cylinder, the cylinder passes through the box and the test must say so. When it says so, the point must be
        /// valid. A hit without samples on both sides is only accepted when the box is close to the cylinder.
        /// </summary>
        [TestMethod]
        public void RandomBoxesAgreeWithSampling()
        {
            Random random = new Random(4711);
            int hits = 0, misses = 0;
            for (int n = 0; n < 2000; n++)
            {
                GeoVector zAxis = new GeoVector(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() + 0.2);
                zAxis.Norm();
                GeoVector xAxis = zAxis ^ GeoVector.XAxis;
                if (xAxis.Length < 0.1) xAxis = zAxis ^ GeoVector.YAxis;
                xAxis.Norm();
                GeoVector yAxis = zAxis ^ xAxis;
                double rx = 2 + 8 * random.NextDouble(), ry = 2 + 8 * random.NextDouble();
                CylindricalSurface cylinder = new CylindricalSurface(new GeoPoint(random.NextDouble(), random.NextDouble(), 0),
                    rx * xAxis, ry * yAxis, zAxis);

                double x0 = -15 + 30 * random.NextDouble(), y0 = -15 + 30 * random.NextDouble(), z0 = -15 + 30 * random.NextDouble();
                BoundingCube box = new BoundingCube(x0, x0 + 0.1 + 12 * random.NextDouble(), y0, y0 + 0.1 + 12 * random.NextDouble(),
                    z0, z0 + 0.1 + 12 * random.NextDouble());

                bool inside = false, outside = false;
                double closest = double.MaxValue; // the smallest |orientation| of a sample
                const int m = 12;
                for (int i = 0; i <= m; i++)
                {
                    for (int j = 0; j <= m; j++)
                    {
                        for (int k = 0; k <= m; k++)
                        {
                            if (i != 0 && i != m && j != 0 && j != m && k != 0 && k != m) continue; // only the surface of the box
                            GeoPoint p = new GeoPoint(box.Xmin + i * (box.Xmax - box.Xmin) / m, box.Ymin + j * (box.Ymax - box.Ymin) / m,
                                box.Zmin + k * (box.Zmax - box.Zmin) / m);
                            double o = cylinder.Orientation(p);
                            if (o < 0) inside = true; else outside = true;
                            closest = Math.Min(closest, Math.Abs(o));
                        }
                    }
                }
                bool hit = cylinder.HitTest(box, out GeoPoint2D uv);
                if (inside && outside)
                {
                    Assert.IsTrue(hit, $"case {n}: the cylinder passes through the box, but HitTest says no");
                }
                if (hit)
                {
                    AssertValidHit(cylinder, box, uv);
                    if (!(inside && outside)) Assert.IsTrue(closest < 0.5, $"case {n}: a hit far from the cylinder");
                    ++hits;
                }
                else ++misses;
            }
            // the random boxes must cover both outcomes, otherwise this test says nothing
            Assert.IsTrue(hits > 200 && misses > 200, $"hits {hits}, misses {misses}");
        }
    }
}
