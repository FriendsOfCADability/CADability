using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;

namespace CADability.Tests
{
    /// <summary>
    /// Accuracy check for <see cref="GeoObject.Shell.GetMassProperties(double, out double, out GeoPoint, out double[,])"/>,
    /// <see cref="GeoObject.Shell.Centroid(double)"/> and <see cref="GeoObject.Shell.InertiaTensor(double)"/>. The bodies are
    /// created by <see cref="Make3D"/> and compared with the analytic values of volume, center of gravity and inertia tensor.
    /// A rotated box additionally checks the off diagonal elements, which are zero for all axis aligned bodies.
    /// </summary>
    [TestClass]
    public class MassPropertiesTests
    {
        public TestContext TestContext { get; set; }

        private const double TriangulationPrecision = 0.01;
        private const double Tolerance = 1e-3; // relative error, referring to the size of the tensor resp. the body

        [TestMethod]
        public void BoxMassProperties()
        {
            double a = 4.0, b = 6.0, c = 10.0;
            GeoPoint location = new GeoPoint(1.0, 2.0, 3.0);
            Solid box = Make3D.MakeBox(location, a * GeoVector.XAxis, b * GeoVector.YAxis, c * GeoVector.ZAxis);
            Shell shell = box.Shells[0];
            shell.GetMassProperties(TriangulationPrecision, out double volume, out GeoPoint cog, out double[,] tensor);

            double m = a * b * c;
            double[,] expected = Diagonal(m * (b * b + c * c) / 12.0, m * (a * a + c * c) / 12.0, m * (a * a + b * b) / 12.0);
            Check("Box", m, new GeoPoint(location.x + a / 2.0, location.y + b / 2.0, location.z + c / 2.0), expected, volume, cog, tensor);
        }

        [TestMethod]
        public void RotatedBoxMassProperties()
        {
            // the same box, but rotated by 30 degrees around the z-axis and by 20 degrees around the (new) x-axis. The expected
            // tensor is the tensor of the axis aligned box transformed into the world system: T = R * Tlocal * R transposed
            double a = 4.0, b = 6.0, c = 10.0;
            ModOp rot = ModOp.Rotate(GeoPoint.Origin, GeoVector.ZAxis, new SweepAngle(30.0 * Math.PI / 180.0))
                      * ModOp.Rotate(GeoPoint.Origin, GeoVector.XAxis, new SweepAngle(20.0 * Math.PI / 180.0));
            GeoVector dx = rot * (a * GeoVector.XAxis), dy = rot * (b * GeoVector.YAxis), dz = rot * (c * GeoVector.ZAxis);
            GeoPoint location = new GeoPoint(-7.0, 5.0, 2.0);
            Solid box = Make3D.MakeBox(location, dx, dy, dz);
            Shell shell = box.Shells[0];
            shell.GetMassProperties(TriangulationPrecision, out double volume, out GeoPoint cog, out double[,] tensor);

            double m = a * b * c;
            double[,] local = Diagonal(m * (b * b + c * c) / 12.0, m * (a * a + c * c) / 12.0, m * (a * a + b * b) / 12.0);
            double[,] expected = Rotate(local, dx.Normalized, dy.Normalized, dz.Normalized);
            Check("Rotated box", m, location + 0.5 * (dx + dy + dz), expected, volume, cog, tensor);
        }

        [TestMethod]
        public void SphereMassProperties()
        {
            double r = 20.0;
            GeoPoint center = new GeoPoint(10.0, -5.0, 3.0);
            Solid sphere = Make3D.MakeSphere(center, r);
            Assert.IsNotNull(sphere, "could not create the sphere");
            Shell shell = sphere.Shells[0];
            shell.GetMassProperties(TriangulationPrecision, out double volume, out GeoPoint cog, out double[,] tensor);

            double m = 4.0 / 3.0 * Math.PI * r * r * r;
            double i = 2.0 / 5.0 * m * r * r;
            Check("Sphere", m, center, Diagonal(i, i, i), volume, cog, tensor);
        }

        [TestMethod]
        public void CylinderMassProperties()
        {
            double r = 8.0, h = 25.0;
            GeoPoint location = new GeoPoint(3.0, 4.0, -6.0);
            Solid cylinder = Make3D.MakeCylinder(location, r * GeoVector.XAxis, h * GeoVector.ZAxis);
            Assert.IsNotNull(cylinder, "could not create the cylinder");
            Shell shell = cylinder.Shells[0];
            shell.GetMassProperties(TriangulationPrecision, out double volume, out GeoPoint cog, out double[,] tensor);

            double m = Math.PI * r * r * h;
            double ir = m * (3.0 * r * r + h * h) / 12.0; // perpendicular to the axis
            double ia = m * r * r / 2.0; // around the axis
            Check("Cylinder", m, location + 0.5 * h * GeoVector.ZAxis, Diagonal(ir, ir, ia), volume, cog, tensor);
        }

        [TestMethod]
        public void TorusMassProperties()
        {
            double bigRadius = 30.0, smallRadius = 10.0;
            GeoPoint center = new GeoPoint(-2.0, 7.0, 1.0);
            Solid torus = Make3D.MakeTorus(center, GeoVector.ZAxis, bigRadius, smallRadius);
            Assert.IsNotNull(torus, "could not create the torus");
            Shell shell = torus.Shells[0];
            shell.GetMassProperties(TriangulationPrecision, out double volume, out GeoPoint cog, out double[,] tensor);

            double m = 2.0 * Math.PI * Math.PI * bigRadius * smallRadius * smallRadius;
            double ia = m * (bigRadius * bigRadius + 0.75 * smallRadius * smallRadius); // around the axis of symmetry
            double ir = m * (0.5 * bigRadius * bigRadius + 0.625 * smallRadius * smallRadius); // around a diameter
            Check("Torus", m, center, Diagonal(ir, ir, ia), volume, cog, tensor);
        }

        [TestMethod]
        public void InertiaTensorWithReferencePoint()
        {
            // the tensor with respect to an arbitrary point must be the tensor at the center of gravity plus the Steiner terms
            double a = 4.0, b = 6.0, c = 10.0;
            GeoPoint location = new GeoPoint(1.0, 2.0, 3.0);
            Solid box = Make3D.MakeBox(location, a * GeoVector.XAxis, b * GeoVector.YAxis, c * GeoVector.ZAxis);
            Shell shell = box.Shells[0];
            double[,] tensor = shell.InertiaTensor(TriangulationPrecision, GeoPoint.Origin);

            // the analytic tensor of the box with respect to the origin: integral of (y²+z²) etc. over the box
            double m = a * b * c;
            GeoPoint cog = new GeoPoint(location.x + a / 2.0, location.y + b / 2.0, location.z + c / 2.0);
            double[,] expected = Diagonal(m * (b * b + c * c) / 12.0, m * (a * a + c * c) / 12.0, m * (a * a + b * b) / 12.0);
            expected[0, 0] += m * (cog.y * cog.y + cog.z * cog.z);
            expected[1, 1] += m * (cog.z * cog.z + cog.x * cog.x);
            expected[2, 2] += m * (cog.x * cog.x + cog.y * cog.y);
            expected[0, 1] = expected[1, 0] = -m * cog.x * cog.y;
            expected[1, 2] = expected[2, 1] = -m * cog.y * cog.z;
            expected[2, 0] = expected[0, 2] = -m * cog.z * cog.x;

            AssertTensor("Box at origin", expected, tensor);
        }

        private void Check(string name, double expectedVolume, GeoPoint expectedCog, double[,] expectedTensor,
            double volume, GeoPoint cog, double[,] tensor)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine();
            report.AppendLine($"{name}: volume {volume:F6} (expected {expectedVolume:F6}), rel.err {(volume - expectedVolume) / expectedVolume:P4}");
            report.AppendLine($"{name}: center of gravity {cog} (expected {expectedCog})");
            report.AppendLine($"{name}: inertia tensor");
            for (int i = 0; i < 3; i++) report.AppendLine($"    {tensor[i, 0],16:F4} {tensor[i, 1],16:F4} {tensor[i, 2],16:F4}");
            report.AppendLine($"{name}: expected");
            for (int i = 0; i < 3; i++) report.AppendLine($"    {expectedTensor[i, 0],16:F4} {expectedTensor[i, 1],16:F4} {expectedTensor[i, 2],16:F4}");
            TestContext.WriteLine(report.ToString());
            System.Diagnostics.Trace.WriteLine(report.ToString());

            Assert.AreEqual(expectedVolume, volume, Tolerance * Math.Abs(expectedVolume), $"{name}: wrong volume");
            // the center of gravity is compared to the size of the body, which is derived from the volume
            double size = Math.Pow(Math.Abs(expectedVolume), 1.0 / 3.0);
            Assert.IsTrue((cog | expectedCog) < Tolerance * size, $"{name}: wrong center of gravity: {cog}, expected {expectedCog}");
            AssertTensor(name, expectedTensor, tensor);
        }

        private static void AssertTensor(string name, double[,] expected, double[,] tensor)
        {
            // the off diagonal elements are compared to the size of the diagonal, they may be zero
            double scale = Math.Abs(expected[0, 0]) + Math.Abs(expected[1, 1]) + Math.Abs(expected[2, 2]);
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Assert.AreEqual(expected[i, j], tensor[i, j], Tolerance * scale,
                        $"{name}: wrong inertia tensor at [{i},{j}]: {tensor[i, j]}, expected {expected[i, j]}");
                }
            }
        }

        private static double[,] Diagonal(double ixx, double iyy, double izz)
        {
            double[,] res = new double[3, 3];
            res[0, 0] = ixx;
            res[1, 1] = iyy;
            res[2, 2] = izz;
            return res;
        }

        /// <summary>
        /// Transforms the tensor, which refers to the provided (orthonormal) axes, into the world system: R * tensor * R transposed,
        /// where the columns of R are the axes.
        /// </summary>
        private static double[,] Rotate(double[,] tensor, GeoVector dirX, GeoVector dirY, GeoVector dirZ)
        {
            double[,] r = new double[3, 3];
            r[0, 0] = dirX.x; r[1, 0] = dirX.y; r[2, 0] = dirX.z;
            r[0, 1] = dirY.x; r[1, 1] = dirY.y; r[2, 1] = dirY.z;
            r[0, 2] = dirZ.x; r[1, 2] = dirZ.y; r[2, 2] = dirZ.z;
            double[,] res = new double[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    double sum = 0.0;
                    for (int k = 0; k < 3; k++)
                    {
                        for (int l = 0; l < 3; l++) sum += r[i, k] * tensor[k, l] * r[j, l];
                    }
                    res[i, j] = sum;
                }
            }
            return res;
        }
    }
}
