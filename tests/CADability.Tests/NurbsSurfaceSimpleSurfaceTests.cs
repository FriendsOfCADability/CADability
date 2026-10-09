using CADability.GeoObject;

namespace CADability.Tests
{
    // Regression tests for issue #347: NurbsSurface.GetSimpleSurface and GetCanonicalForm looped forever for
    // surfaces with a tiny parameter domain and a singularity at the domain boundary, because the sample
    // parameters were searched with a fixed singularity tolerance of 1e-6, which covered the whole domain.
    [TestClass]
    public class NurbsSurfaceSimpleSurfaceTests
    {
        // generous limit: the calls return within milliseconds when they terminate at all
        private const int TimeoutMilliseconds = 20000;

        /// <summary>
        /// Degree 2x2 NURBS patch with the u-domain [0, uSpan]. All poles of the last u-row coincide, so the
        /// surface is singular at umax. With <paramref name="singularAtUMin"/> the first u-row collapses too.
        /// </summary>
        private static NurbsSurface MakeSurface(double uSpan, bool singularAtUMin)
        {
            GeoPoint[,] poles = new GeoPoint[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    poles[i, j] = new GeoPoint(i, j, (i == 1 && j == 1) ? 0.3 : 0.0);
                }
            }
            for (int j = 0; j < 3; j++) poles[2, j] = new GeoPoint(2, 1, 0); // singular at umax
            if (singularAtUMin)
            {
                for (int j = 0; j < 3; j++) poles[0, j] = new GeoPoint(0, 1, 0); // singular at umin
            }
            return new NurbsSurface(poles, null, new double[] { 0, uSpan }, new double[] { 0, 1 },
                new int[] { 3, 3 }, new int[] { 3, 3 }, 2, 2, false, false);
        }

        /// <summary>
        /// Runs <paramref name="action"/> on a background task and fails instead of hanging the test run
        /// when it does not return in time.
        /// </summary>
        private static void AssertTerminates(Action action, string name)
        {
            Task task = Task.Factory.StartNew(action, TaskCreationOptions.LongRunning);
            Assert.IsTrue(task.Wait(TimeoutMilliseconds), name + " did not return (infinite loop, issue #347)");
        }

        [TestMethod]
        public void GetCanonicalForm_TinyDomainSingularAtMax_Terminates()
        {
            NurbsSurface ns = MakeSurface(5e-7, false);
            AssertTerminates(() => ns.GetCanonicalForm(1e-3, null), "GetCanonicalForm");
        }

        [TestMethod]
        public void GetSimpleSurface_TinyDomainSingularAtBothEnds_Terminates()
        {
            NurbsSurface ns = MakeSurface(1.5e-6, true);
            AssertTerminates(() => ns.GetSimpleSurface(0.0, out ISurface _, out ModOp2D _), "GetSimpleSurface");
        }

        [TestMethod]
        public void GetSimpleSurface_RegularDomainSingularAtMax_Terminates()
        {
            // control case, which already worked before the fix
            NurbsSurface ns = MakeSurface(1e-3, true);
            AssertTerminates(() => ns.GetSimpleSurface(0.0, out ISurface _, out ModOp2D _), "GetSimpleSurface");
            AssertTerminates(() => ns.GetCanonicalForm(1e-3, null), "GetCanonicalForm");
        }
    }
}
