using System.Runtime.CompilerServices;
using System.Text;
using CADability.GeoObject;
using Path = CADability.GeoObject.Path;

namespace CADability.Tests
{
    /// <summary>
    /// The progress that <see cref="JsonSerialize"/> reports while a cdb file is read or written (#367).
    /// </summary>
    [TestClass]
    public class JsonProgressTests
    {
        /// <summary>
        /// Follows the progress like the progress bar of <see cref="Project"/> does.
        /// </summary>
        private class Recorder : IJsonProgress
        {
            private double increment = 1, offset;
            private long steps;
            public long TotalSteps { get; private set; }
            public double Position => offset + increment * steps;
            public double Max { get; private set; }
            public void SetStepIncrement(double increment)
            {
                offset = Position;
                this.increment = increment;
                steps = 0;
            }
            public void DoStep(int doneSteps = 1)
            {
                steps += doneSteps;
                TotalSteps += doneSteps;
                if (Position > Max) Max = Position;
            }
            public void SetCompleted() { }
        }

        private static string CdbFile(string name, [CallerFilePath] string thisFile = "")
            => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile), "Files", "CDB", name);

        /// <summary>
        /// A project with objects in two models that contain other objects: a block with children, a reference to a block
        /// that is in no model, a path and a solid.
        /// </summary>
        private static Project ProjectWithTwoModels()
        {
            Project project = Project.CreateSimpleProject();
            Model first = project.GetModel(0);
            first.Add(Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0)));
            Block block = Block.Construct();
            block.Add(Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(0, 10, 0)));
            block.Add(Line.TwoPoints(new GeoPoint(0, 10, 0), new GeoPoint(10, 10, 0)));
            first.Add(block);
            Block referenced = Block.Construct();
            referenced.Add(Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(5, 5, 0)));
            first.Add(BlockRef.Construct(referenced));

            Model second = new Model();
            second.Add(Path.FromSegments(new ICurve[]
            {
                Line.TwoPoints(new GeoPoint(0, 0, 0), new GeoPoint(10, 0, 0)),
                Line.TwoPoints(new GeoPoint(10, 0, 0), new GeoPoint(10, 10, 0))
            }, true));
            second.Add(Make3D.MakeBox(GeoPoint.Origin, 10 * GeoVector.XAxis, 10 * GeoVector.YAxis, 10 * GeoVector.ZAxis));
            project.AddModel(second);
            return project;
        }

        [TestMethod]
        public void writing_does_as_many_steps_as_counted_before()
        {
            Project project = ProjectWithTwoModels();
            int counted = project.CountSerializedGeoObjects();
            Recorder recorder = new Recorder();
            recorder.SetStepIncrement(100.0 / counted);
            using (MemoryStream stream = new MemoryStream())
            {
                new JsonSerialize().ToStream(stream, project, recorder, false);
            }
            Assert.AreEqual(counted, recorder.TotalSteps);
            Assert.AreEqual(100.0, recorder.Max, 1e-9);
        }

        [TestMethod]
        public void reading_stays_within_100_percent()
        {
            Recorder recorder = new Recorder();
            Project project;
            using (FileStream stream = File.OpenRead(CdbFile("issue347.cdb.json")))
            {
                project = Project.ReadFromJson(stream, recorder);
            }
            Assert.IsNotNull(project);
            Assert.IsTrue(recorder.Max > 0, "no progress reported");
            Assert.IsTrue(recorder.Max <= 100 + 1e-9, $"progress went up to {recorder.Max} percent");
        }

        // The tokenizer reads the whole file at once (#367). When the file ends inside a string, it must report the end of
        // the file, which the reader turns into a syntax error, and not a string of negative length.
        [TestMethod]
        public void a_file_ending_inside_a_string_is_a_syntax_error()
        {
            string text = File.ReadAllText(CdbFile("issue347.cdb.json"));
            int cut = text.IndexOf("\"CADability.Project\"") + 5; // inside the type name
            Assert.IsTrue(cut > 5);
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(text.Substring(0, cut))))
            {
                ApplicationException e = Assert.ThrowsException<ApplicationException>(() => Project.ReadFromJson(stream));
                StringAssert.Contains(e.Message, "Syntax error");
            }
        }
    }
}
