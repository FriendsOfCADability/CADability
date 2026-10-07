using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Intersection curves and their 2d curves in files. The two ProjectedCurve classes became one, as in ShapeIt, and the
    /// 2d curve of an intersection now lies in the periods of the uv values its InterpolatedDualSurfaceCurve stores. The
    /// files in Files/Json were written by version 1.4.0, before that change: a cone with a bore, whose edges are
    /// intersection curves with the old nested ProjectedCurve, and the solid of issue153.stp, with 27 plain
    /// ProjectedCurves on NURBS faces.
    /// </summary>
    [TestClass]
    public class IntersectionCurveFileTests
    {
        public TestContext TestContext { get; set; }

        /// <summary>The volume of the bored cone, integrated over its faces with the code that wrote the file.</summary>
        private const double BoredConeVolume = 11817.2059;

        private Solid ReadSolid(string fileName)
        {
            string file = System.IO.Path.Combine(TestContext.DeploymentDirectory, TestContext.TestName, fileName);
            Assert.IsTrue(File.Exists(file), file);
            Solid solid = JsonSerialize.FromString(File.ReadAllText(file)) as Solid;
            Assert.IsNotNull(solid);
            return solid;
        }

        private static Solid BoredCone()
        {
            GeoPoint b = new GeoPoint(100.0, 50.0, 20.0);
            Solid cone = Make3D.MakeCone(b, GeoVector.XAxis, 30.0 * GeoVector.ZAxis, 20.0, 0.0);
            Solid bore = Make3D.MakeCylinder(b + new GeoVector(0.0, -50.0, 10.0), 3.0 * GeoVector.XAxis, 100.0 * GeoVector.YAxis);
            Solid[] res = Solid.Subtract(cone, bore);
            Assert.AreEqual(1, res.Length, "the bore must leave one solid");
            return res[0];
        }

        /// <summary>
        /// The faces of <paramref name="solid"/> are consistent and closed, and the volume of the triangulation agrees with
        /// the volume integrated over the faces. A 2d curve one period away from where its face is still gives the same
        /// integrated volume, because the boundary integral does not see the period, but the triangulation of that face
        /// goes wrong.
        /// </summary>
        private static void AssertSound(Solid solid, string what)
        {
            Shell shell = solid.Shells[0];
            Assert.AreEqual(0, shell.OpenEdgesExceptPoles.Length, $"{what}: open edges");
            foreach (Face fc in shell.Faces) Assert.IsTrue(fc.CheckConsistency(), $"{what}: inconsistent {fc.Surface.GetType().Name}");
            double integrated = ShellMetrics.IntegratedVolume(shell);
            Assert.AreEqual(integrated, solid.Volume(0.001), integrated * 1e-4, $"{what}: triangulated and integrated volume");
        }

        private static int CurvesOfIntersection(Solid solid)
        {
            int n = 0;
            foreach (Edge edge in solid.Shells[0].Edges)
            {
                if (edge.PrimaryCurve2D is ProjectedCurve p && p.IsCurveOfIntersection) n++;
                if (edge.SecondaryCurve2D is ProjectedCurve s && s.IsCurveOfIntersection) n++;
            }
            return n;
        }

        [TestMethod]
        [DeploymentItem(@"Files/Json/bored_cone_1.4.0.json", nameof(an_intersection_curve_written_by_1_4_0_is_read))]
        public void an_intersection_curve_written_by_1_4_0_is_read()
        {
            Solid solid = ReadSolid("bored_cone_1.4.0.json");
            Assert.AreEqual(8, CurvesOfIntersection(solid), "the nested ProjectedCurves are read into the one class");
            AssertSound(solid, "bored cone of 1.4.0");
            Assert.AreEqual(BoredConeVolume, ShellMetrics.IntegratedVolume(solid.Shells[0]), BoredConeVolume * 1e-6);
        }

        [TestMethod]
        [DeploymentItem(@"Files/Json/issue153_solid_1.4.0.json", nameof(plain_projected_curves_written_by_1_4_0_are_read))]
        public void plain_projected_curves_written_by_1_4_0_are_read()
        {
            Solid solid = ReadSolid("issue153_solid_1.4.0.json");
            int plain = 0;
            foreach (Edge edge in solid.Shells[0].Edges)
            {
                if (ProjectedCurve.IsPlain(edge.PrimaryCurve2D)) plain++;
                if (ProjectedCurve.IsPlain(edge.SecondaryCurve2D)) plain++;
            }
            Assert.AreEqual(27, plain);
            AssertSound(solid, "issue153");
            Assert.AreEqual(552468.93243, ShellMetrics.IntegratedVolume(solid.Shells[0]), 552468.93243 * 1e-9);
        }

        [TestMethod]
        public void a_bored_cone_survives_a_json_round_trip()
        {
            // The intersection curves are read before the faces set the domains of their surfaces, and a cylinder does not
            // write its domain. The curve must keep the uv values in the periods it wrote them in, the 2d curves of the
            // faces refer to them.
            Solid bored = BoredCone();
            AssertSound(bored, "bored cone");
            Solid read = JsonSerialize.FromString(JsonSerialize.ToString(bored)) as Solid;
            Assert.IsNotNull(read);
            Assert.AreEqual(8, CurvesOfIntersection(read));
            AssertSound(read, "bored cone after the round trip");
            Assert.AreEqual(ShellMetrics.IntegratedVolume(bored.Shells[0]), ShellMetrics.IntegratedVolume(read.Shells[0]), BoredConeVolume * 1e-9);
        }
    }
}
