using CADability.Curve2D;
using CADability.Shapes;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="PrecisionOverride"/> changed the global <see cref="Precision.eps"/>, so every other thread computed with the
    /// overridden precision while it was alive. <see cref="Border.Clip"/> overrides the precision for every call, and boolean
    /// operations or imports running in parallel (the parallel STEP import, the tests) got different results now and then.
    /// The override now only applies to the thread that creates it.
    /// </summary>
    [TestClass]
    public class PrecisionOverrideTests
    {
        [TestMethod]
        public void an_override_applies_only_to_its_own_thread()
        {
            double global = Precision.eps, globalAngle = Precision.epsa;
            double seenByOtherThread = 0.0, seenInside = 0.0, seenInsideAngle = 0.0;
            using (ManualResetEventSlim overridden = new ManualResetEventSlim(), read = new ManualResetEventSlim())
            {
                Task owner = Task.Factory.StartNew(() =>
                {
                    using (new PrecisionOverride(1e-9, 1e-8))
                    {
                        seenInside = Precision.eps;
                        seenInsideAngle = Precision.epsa;
                        overridden.Set();
                        read.Wait(10000);
                    }
                }, TaskCreationOptions.LongRunning);
                Assert.IsTrue(overridden.Wait(10000));
                seenByOtherThread = Precision.eps;
                Assert.AreEqual(globalAngle, Precision.epsa);
                read.Set();
                Assert.IsTrue(owner.Wait(10000));
            }
            Assert.AreEqual(1e-9, seenInside);
            Assert.AreEqual(1e-8, seenInsideAngle);
            Assert.AreEqual(global, seenByOtherThread, "another thread keeps the global precision");
        }

        [TestMethod]
        public void nested_overrides_restore_the_previous_values()
        {
            double global = Precision.eps, globalAngle = Precision.epsa;
            PrecisionOverride outer = new PrecisionOverride(1e-7);
            Assert.AreEqual(1e-7, Precision.eps);
            Assert.AreEqual(globalAngle, Precision.epsa, "an override of eps alone keeps epsa");
            using (new PrecisionOverride(1e-9, 1e-9))
            {
                Assert.AreEqual(1e-9, Precision.eps);
                Assert.AreEqual(1e-9, Precision.epsa);
            }
            Assert.AreEqual(1e-7, Precision.eps);
            Assert.AreEqual(globalAngle, Precision.epsa);
            outer.Dispose();
            outer.Dispose(); // a second Dispose changes nothing
            Assert.AreEqual(global, Precision.eps);
            Assert.AreEqual(globalAngle, Precision.epsa);
        }

        [TestMethod]
        public void clipping_on_another_thread_does_not_change_the_precision_here()
        {
            double global = Precision.eps;
            Border border = Border.MakeRectangle(0, 1, 0, 1); // Clip overrides the precision with 2e-9
            Line2D line = new Line2D(new GeoPoint2D(-1, 0.5), new GeoPoint2D(2, 0.5));
            using (CancellationTokenSource stop = new CancellationTokenSource())
            {
                Task clipping = Task.Factory.StartNew(() =>
                {
                    while (!stop.IsCancellationRequested) border.Clip(line, true);
                }, TaskCreationOptions.LongRunning);
                int differing = 0;
                DateTime end = DateTime.Now.AddMilliseconds(300);
                while (DateTime.Now < end)
                {
                    for (int i = 0; i < 1000; i++) if (Precision.eps != global) ++differing;
                }
                stop.Cancel();
                Assert.IsTrue(clipping.Wait(10000));
                Assert.AreEqual(0, differing, "reads of Precision.eps that saw the precision of Border.Clip");
            }
            Assert.AreEqual(2, border.Clip(line, true).Length, "the clipping itself still works");
        }
    }
}
