using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.Serialization.Formatters.Binary;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// A picture is created, measured and exported without a frame and without GDI (issue #412, step 9): this runs in the
    /// Linux CI, where there is no GDI+. Afterwards the finalizers run, and the process must survive them.
    /// </summary>
    [TestClass]
    public class HeadlessPictureExportTests
    {
        // a PNG of 3×2 pixels
        private static readonly byte[] Png3x2 = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAMAAAACCAIAAAASFvFNAAAAG0lEQVR4nAXBAQEAAACCIDe5qbHOCgCHIeSyPCtwBd1HI03nAAAAAElFTkSuQmCC");

        private static Project MakeProject()
        {
            Project project = Project.CreateSimpleProject();
            Picture picture = Picture.Construct();
            picture.Set(Png3x2, new GeoPoint(1, 2, 3), new GeoVector(30, 0, 0), new GeoVector(0, 20, 0));
            project.GetActiveModel().Add(picture);
            project.GetActiveModel().Add(Line.TwoPoints(GeoPoint.Origin, new GeoPoint(100, 0, 0)));
            return project;
        }

        [TestMethod]
        public void APictureIsMeasuredWithoutFrameOrGdi()
        {
            Assert.IsNull(FrameImpl.MainFrame, "the tests run without a frame");
            Picture picture = MakeProject().GetActiveModel().AllObjects.OfType<Picture>().Single();
            Assert.AreEqual((3, 2), (picture.PixelWidth, picture.PixelHeight));
            BoundingCube extent = picture.GetBoundingCube();
            Assert.AreEqual((1.0, 31.0, 2.0, 22.0), (extent.Xmin, extent.Xmax, extent.Ymin, extent.Ymax));
            picture.SetHeight(40, true);
            Assert.AreEqual(60, picture.DirectionWidth.Length, 1e-12, "the aspect ratio of the pixels");
        }

        [TestMethod]
        public void AProjectWithAPictureIsExportedWithoutFrameOrGdi()
        {
            // DXF and STL have no pictures: the other objects are written, and the picture does not stop the export
            Project project = MakeProject();
            string dxf = Path.Combine(Path.GetTempPath(), "HeadlessPictureExportTests-" + Guid.NewGuid().ToString("N") + ".dxf");
            string stl = Path.ChangeExtension(dxf, ".stl");
            try
            {
                Assert.IsTrue(project.Export(dxf, "dxf"), "the DXF file was not written");
                Assert.AreEqual(1, Project.ReadFromFile(dxf, "dxf").GetActiveModel().AllObjects.OfType<Line>().Count());
                project.ExportToSTL(stl);
                Assert.IsTrue(File.Exists(stl));
            }
            finally
            {
                File.Delete(dxf);
                File.Delete(stl);
            }
        }

        [TestMethod]
        [DoNotParallelize] // enables the BinaryFormatter for the process
        public void AProjectWithAPictureIsWrittenInTheBinaryFormatWithoutGdi()
        {
            // the binary format used to contain the GDI+ bitmap, which could only be written and read with GDI+
            AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
            try
            {
#pragma warning disable SYSLIB0011 // the binary format is only tested, for old applications
                new BinaryFormatter().Serialize(new MemoryStream(), 1);
#pragma warning restore SYSLIB0011
            }
            catch (Exception e) when (e is NotSupportedException || e is PlatformNotSupportedException)
            {
                Assert.Inconclusive("this runtime has no BinaryFormatter: " + e.Message);
            }
            MemoryStream written = new MemoryStream();
            MakeProject().WriteToStream(written);
#pragma warning disable CS0618 // ReadFromStream is kept for binary files
            Project read = Project.ReadFromStream(new MemoryStream(written.ToArray()));
#pragma warning restore CS0618
            CollectionAssert.AreEqual(Png3x2, read.GetActiveModel().AllObjects.OfType<Picture>().Single().ImageData);
        }

        [TestMethod]
        public void TheProcessSurvivesTheFinalizersAfterPictures()
        {
            for (int i = 0; i < 20; ++i)
            {
                Project project = MakeProject();
                string stl = Path.Combine(Path.GetTempPath(), "HeadlessPictureExportTests-" + Guid.NewGuid().ToString("N") + ".stl");
                project.ExportToSTL(stl); // paints the picture, which decodes it with GDI+ where there is GDI+
                File.Delete(stl);
                Picture picture = project.GetActiveModel().AllObjects.OfType<Picture>().Single();
                ((Picture)picture.Clone()).ImageData = Png3x2;
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
