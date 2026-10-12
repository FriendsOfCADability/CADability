using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability;
using CADability.GeoObject;

namespace CADability.ImportTests
{
    /// <summary>
    /// Regression for issue #365: STEP imports running at the same time (e.g. tests executed in
    /// parallel) failed at random. Two static lookup tables were shared between imports without
    /// being safe for that:
    /// 1. <c>ImportStep.Item.TypeOfName</c> was refilled by every <see cref="ImportStep"/>
    ///    constructor, so concurrent constructors wrote into the same dictionary and corrupted it
    ///    ("Destination array was not long enough" or "Operations that change non-concurrent
    ///    collections must have exclusive access").
    /// 2. <c>StepSyntax.SubType</c> and <c>StepSyntax.Parameter</c> were built lazily and assigned
    ///    before they were filled, so a second import could look up entity parameters in a
    ///    half-built table (KeyNotFoundException "context_of_items").
    /// Both tables are created once per process, so this test only exercises the race when it is
    /// the first STEP import of the test run, which it is in this project.
    /// </summary>
    [TestClass]
    public class StepParallelImportTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DeploymentItem(@"Files/Step/issue101.stp", nameof(import_step_in_parallel_succeeds))]
        public void import_step_in_parallel_succeeds()
        {
            var file = System.IO.Path.Combine(this.TestContext.DeploymentDirectory, this.TestContext.TestName, "issue101.stp");
            Assert.IsTrue(File.Exists(file));

            const int imports = 64;
            var failures = new ConcurrentQueue<Exception>();
            var faceCounts = new ConcurrentQueue<int>();
            using (var start = new ManualResetEventSlim(false))
            {
                // Release all threads at once, so that they reach the static initialization together.
                var tasks = Enumerable.Range(0, imports).Select(_ => Task.Factory.StartNew(() =>
                {
                    start.Wait();
                    try
                    {
                        var project = Project.ReadFromFile(file, "stp");
                        faceCounts.Enqueue(project.GetActiveModel().AllObjects.Sum(CountFaces));
                    }
                    catch (Exception e)
                    {
                        failures.Enqueue(e);
                    }
                }, TaskCreationOptions.LongRunning)).ToArray();
                start.Set();
                Task.WaitAll(tasks);
            }

            Assert.AreEqual(0, failures.Count, failures.FirstOrDefault()?.ToString());
            Assert.AreEqual(imports, faceCounts.Count);
            // issue101.stp contains a single solid with 25 faces
            foreach (int faces in faceCounts) Assert.AreEqual(25, faces);
        }

        private static int CountFaces(IGeoObject go)
        {
            switch (go)
            {
                case Solid solid: return solid.Shells.Sum(shell => shell.Faces.Length);
                case Shell shell: return shell.Faces.Length;
                case Face _: return 1;
                case Block block: return Enumerable.Range(0, block.NumChildren).Sum(i => CountFaces(block.Child(i)));
                default: return 0;
            }
        }
    }
}
