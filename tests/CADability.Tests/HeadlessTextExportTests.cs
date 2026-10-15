using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// A text is created, measured and exported without a frame and without GDI (issue #412, step 9): this runs in the Linux
    /// CI, where there is no GDI+. The font is Liberation Sans, which the tests register (see <see cref="TestFonts"/>).
    /// Afterwards the finalizers run, and the process must survive them.
    /// </summary>
    [TestClass]
    public class HeadlessTextExportTests
    {
        private static Text MakeText(string textString)
        {
            Text text = Text.Construct();
            text.Font = "Liberation Sans";
            text.TextString = textString;
            text.Location = new GeoPoint(10, 20, 0);
            text.LineDirection = GeoVector.XAxis;
            text.GlyphDirection = GeoVector.YAxis;
            text.TextSize = 5.0;
            return text;
        }

        [TestMethod]
        public void ATextIsSavedAndReadWithoutFrameOrGdi()
        {
            Assert.IsNull(FrameImpl.MainFrame, "the tests run without a frame");
            Project project = Project.CreateSimpleProject();
            Text text = MakeText("Headless 412");
            project.GetActiveModel().Add(text);
            BoundingCube extent = text.GetBoundingCube();
            Assert.IsFalse(extent.IsEmpty);
            Assert.IsTrue(extent.Xmax - extent.Xmin > 20 && extent.Ymax - extent.Ymin > 2, "the text has the size of its glyphs");

            MemoryStream written = new MemoryStream();
            project.WriteToJson(written);
            Text read = Project.ReadFromJson(new MemoryStream(written.ToArray())).GetActiveModel().AllObjects.OfType<Text>().Single();
            Assert.AreEqual("Headless 412", read.TextString);
            Assert.AreEqual("Liberation Sans", read.Font);
            BoundingCube readExtent = read.GetBoundingCube();
            Assert.AreEqual(extent.Xmin, readExtent.Xmin, 1e-9);
            Assert.AreEqual(extent.Xmax, readExtent.Xmax, 1e-9);
            Assert.AreEqual(extent.Ymax, readExtent.Ymax, 1e-9);
        }

        [TestMethod]
        public void ATextIsExportedToDxfAndReadBackWithoutFrameOrGdi()
        {
            Project project = Project.CreateSimpleProject();
            project.GetActiveModel().Add(MakeText("Headless 412"));
            string file = Path.Combine(Path.GetTempPath(), "HeadlessTextExportTests-" + Guid.NewGuid().ToString("N") + ".dxf");
            try
            {
                Assert.IsTrue(project.Export(file, "dxf"), "the DXF file was not written");
                Project imported = Project.ReadFromFile(file, "dxf");
                Text text = imported.GetActiveModel().AllObjects.OfType<Text>().Single();
                Assert.AreEqual("Headless 412", text.TextString);
                Assert.AreEqual(5.0, text.TextSize, 1e-6);
                Assert.AreEqual(new GeoPoint(10, 20, 0), text.Location);
                Assert.IsFalse(text.GetBoundingCube().IsEmpty, "the imported text is measured with the font of the kernel");
            }
            finally
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void TheProcessSurvivesTheFinalizersAfterTexts()
        {
            // the GDI version of the FontCache had a finalizer that threw without GDI and ended the process (issue #412)
            for (int i = 0; i < 20; ++i)
            {
                Text text = MakeText("finalize " + i);
                text.GetOutline();
                text.GetBoundingCube();
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
