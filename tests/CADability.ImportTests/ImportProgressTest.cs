using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CADability.DXF;

namespace CADability.ImportTests
{
    /// <summary>
    /// The progress that <see cref="Import"/> reports while a DXF or DWG file is read and converted (#367), and the DWG
    /// import through <see cref="Import"/>, which recognizes a DWG file by its content.
    /// </summary>
    [TestClass]
    public class ImportProgressTest
    {
        /// <summary>
        /// Follows the progress like the progress bar of <see cref="Project"/> does.
        /// </summary>
        private class Recorder : IDxfProgress
        {
            private double increment = 1, offset;
            private long steps;
            public double Position => offset + increment * steps;
            public double Max { get; private set; }
            public int Completed { get; private set; }
            public void SetStepIncrement(double increment)
            {
                offset = Position;
                this.increment = increment;
                steps = 0;
            }
            public void DoStep(int doneSteps = 1)
            {
                steps += doneSteps;
                if (Position > Max) Max = Position;
            }
            public void SetCompleted() { ++Completed; }
        }

        // the sample drawings of the ACadSharp submodule
        private static string Sample(string name, [CallerFilePath] string thisFile = "")
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), "..", "..", "external", "ACadSharp", "samples", name));

        [TestMethod]
        public void dwg_is_imported_with_progress()
        {
            string file = Sample("sample_AC1018.dwg");
            Assert.IsTrue(File.Exists(file), $"test file not found: {file}");
            Recorder recorder = new Recorder();
            Project project = new Import(file, recorder).Project;
            Assert.IsNotNull(project);
            Assert.IsTrue(project.GetModel(0).Count > 0, "no objects imported");
            Assert.IsTrue(recorder.Max >= 60 && recorder.Max <= 100 + 1e-9, $"progress went up to {recorder.Max} percent");
            Assert.AreEqual(1, recorder.Completed);
        }

        [TestMethod]
        public void dwg_with_a_dxf_extension_is_imported()
        {
            string file = Path.Combine(Path.GetTempPath(), $"cadability-{Guid.NewGuid():N}.dxf");
            try
            {
                File.Copy(Sample("sample_AC1018.dwg"), file);
                Project project = Project.ReadFromFile(file, "dxf");
                Assert.IsNotNull(project);
                Assert.IsTrue(project.GetModel(0).Count > 0, "no objects imported");
            }
            finally
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void dxf_is_imported_with_progress()
        {
            string file = Sample("sample_AC1018_ascii.dxf");
            Assert.IsTrue(File.Exists(file), $"test file not found: {file}");
            Recorder recorder = new Recorder();
            Project project = new Import(file, recorder).Project;
            Assert.IsNotNull(project);
            Assert.IsTrue(recorder.Max >= 60 && recorder.Max <= 100 + 1e-9, $"progress went up to {recorder.Max} percent");
            Assert.AreEqual(1, recorder.Completed);
        }

        [TestMethod]
        public void a_damaged_dwg_completes_the_progress()
        {
            string file = Path.Combine(Path.GetTempPath(), $"cadability-{Guid.NewGuid():N}.dwg");
            try
            {
                File.WriteAllBytes(file, System.Text.Encoding.ASCII.GetBytes("AC1018").Concat(new byte[200]).ToArray());
                Recorder recorder = new Recorder();
                bool thrown = false;
                try
                {
                    new Import(file, recorder);
                }
                catch (Exception)
                {
                    thrown = true;
                }
                Assert.IsTrue(thrown, "a damaged file must not be read");
                Assert.AreEqual(1, recorder.Completed);
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
