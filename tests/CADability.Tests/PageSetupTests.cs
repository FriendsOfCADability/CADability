using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// Project files keep the page settings of the project and of the layouts as a neutral <see cref="PageSetup"/> instead
    /// of the GDI+ <c>PageSettings</c>, which can only be serialized on .NET Framework (issue #412, step 7).
    /// pagesettings-1x-net48.cdb and .cdb.json were written by CADability 1.x on .NET Framework 4.8 with the printer
    /// "Microsoft Print to PDF": the project has A3 landscape with the margins 50, 60, 70, 80, the layout "A4 portrait" has A4
    /// portrait with the margins 10, 20, 30, 40 (in hundredths of an inch).
    /// </summary>
    [TestClass]
    public class PageSetupTests
    {
        private static string CdbFile(string name, [CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "CDB", name);

        private const double Inch100 = 0.254; // mm

        private static void AssertPage(PageSetup page, string paper, int kind, int width, int height, bool landscape, int[] margins, string printer, string what)
        {
            Assert.IsNotNull(page, what);
            Assert.AreEqual(paper, page.PaperName, what);
            Assert.AreEqual(kind, page.PaperKind, what);
            Assert.AreEqual(width * Inch100, page.PaperWidth, 1e-9, what);
            Assert.AreEqual(height * Inch100, page.PaperHeight, 1e-9, what);
            Assert.AreEqual(landscape, page.Landscape, what);
            CollectionAssert.AreEqual(margins.Select(m => m * Inch100).ToArray(), new[] { page.MarginLeft, page.MarginRight, page.MarginTop, page.MarginBottom }, what);
            Assert.AreEqual(printer, page.PrinterName, what);
        }

        private static void AssertLegacyPages(Project project, string printer, string file)
        {
            Assert.IsNotNull(project, "could not read " + file);
            AssertPage(project.DefaultPageSetup, "A3", 8, 1169, 1654, true, new[] { 50, 60, 70, 80 }, printer, file + ": the project");
            Layout layout = Enumerable.Range(0, project.LayoutCount).Select(i => project.GetLayout(i)).Single(l => l.Name == "A4 portrait");
            AssertPage(layout.PageSetup, "A4", 9, 827, 1169, false, new[] { 10, 20, 30, 40 }, printer, file + ": the layout");
        }

        [TestMethod]
        public void A1xJsonFileWithPageSettingsOpens()
        {
            // the JSON format has the public properties, so the printer name is the resolved default printer
            AssertLegacyPages(Project.ReadFromFile(CdbFile("pagesettings-1x-net48.cdb.json"), "cdb"), "Microsoft Print to PDF", "pagesettings-1x-net48.cdb.json");
        }

        [TestMethod]
        [DoNotParallelize] // enables the BinaryFormatter for the process
        public void A1xBinaryFileWithPageSettingsOpens()
        {
            AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
            try
            {
#pragma warning disable SYSLIB0011 // the binary format is only read, for old files
                new BinaryFormatter().Serialize(new MemoryStream(), 1);
#pragma warning restore SYSLIB0011
            }
            catch (Exception e) when (e is NotSupportedException || e is PlatformNotSupportedException)
            {
                Assert.Inconclusive("this runtime has no BinaryFormatter: " + e.Message);
            }
            using (FileStream stream = File.OpenRead(CdbFile("pagesettings-1x-net48.cdb")))
            {
                // the binary format has the private fields, where an unchanged printer name is empty: the default printer
#pragma warning disable CS0618 // ReadFromStream is kept for old binary files
                AssertLegacyPages(Project.ReadFromStream(stream), null, "pagesettings-1x-net48.cdb");
#pragma warning restore CS0618
            }
        }

        [TestMethod]
        public void PageSetupsSurviveAJsonRoundTrip()
        {
            Project project = Project.CreateSimpleProject();
            project.DefaultPageSetup = new PageSetup { PaperName = "A3", PaperKind = 8, PaperWidth = 297, PaperHeight = 420, Landscape = true, MarginLeft = 1, MarginRight = 2, MarginTop = 3, MarginBottom = 4, PrinterName = "Plotter" };
            Layout layout = new Layout(project) { Name = "custom", PageSetup = new PageSetup { PaperName = "Strip", PaperWidth = 100, PaperHeight = 1000 } };
            project.AddLayout(layout);
            MemoryStream written = new MemoryStream();
            project.WriteToJson(written);
            string json = System.Text.Encoding.UTF8.GetString(written.ToArray());
            Assert.IsFalse(json.Contains("System.Drawing"), "no GDI+ types in the file");

            Project read = Project.ReadFromJson(new MemoryStream(written.ToArray()));
            PageSetup page = read.DefaultPageSetup;
            Assert.AreEqual(("A3", 8, 297.0, 420.0, true, "Plotter"), (page.PaperName, page.PaperKind, page.PaperWidth, page.PaperHeight, page.Landscape, page.PrinterName));
            Assert.AreEqual((1.0, 2.0, 3.0, 4.0), (page.MarginLeft, page.MarginRight, page.MarginTop, page.MarginBottom));
            Layout readLayout = Enumerable.Range(0, read.LayoutCount).Select(i => read.GetLayout(i)).Single(l => l.Name == "custom");
            Assert.AreEqual(("Strip", 100.0, 1000.0, false), (readLayout.PageSetup.PaperName, readLayout.PageSetup.PaperWidth, readLayout.PageSetup.PaperHeight, readLayout.PageSetup.Landscape));
        }

        [TestMethod]
        public void TheProjectHandsOutCopies()
        {
            Project project = Project.CreateSimpleProject();
            Assert.IsNull(project.DefaultPageSetup, "no page settings: the printer's defaults");
            PageSetup page = new PageSetup { PaperName = "A4" };
            project.DefaultPageSetup = page;
            page.PaperName = "changed";
            Assert.AreEqual("A4", project.DefaultPageSetup.PaperName);
            project.DefaultPageSetup.PaperName = "changed";
            Assert.AreEqual("A4", project.DefaultPageSetup.PaperName);
        }

#if WINDOWS
        [TestMethod]
        public void APageSetupIsAppliedToAPrinterAndSavedOnNet8()
        {
            Project project = Project.CreateSimpleProject();
            project.DefaultPageSetup = new PageSetup { PaperName = "A3", PaperKind = 8, PaperWidth = 1169 * Inch100, PaperHeight = 1654 * Inch100, Landscape = true, MarginLeft = 50 * Inch100, MarginRight = 60 * Inch100, MarginTop = 70 * Inch100, MarginBottom = 80 * Inch100 };
            System.Drawing.Printing.PrintDocument document = project.PrintDocument; // what the page setup dialog works on
            if (!document.PrinterSettings.IsValid) Assert.Inconclusive("no printer installed");
            System.Drawing.Printing.PageSettings settings = document.DefaultPageSettings;
            bool hasA3 = document.PrinterSettings.PaperSizes.Cast<System.Drawing.Printing.PaperSize>().Any(p => p.RawKind == 8);
            if (hasA3) Assert.AreEqual(8, settings.PaperSize.RawKind, "the A3 paper of the printer");
            Assert.AreEqual((1169, 1654), (settings.PaperSize.Width, settings.PaperSize.Height));
            Assert.IsTrue(settings.Landscape);
            Assert.AreEqual((50, 60, 70, 80), (settings.Margins.Left, settings.Margins.Right, settings.Margins.Top, settings.Margins.Bottom));
            // with a PrintDocument, CADability 1.x on .NET 8 could not save the project (PageSettings is not serializable there)
            MemoryStream written = new MemoryStream();
            project.WriteToJson(written);
            PageSetup read = Project.ReadFromJson(new MemoryStream(written.ToArray())).DefaultPageSetup;
            Assert.AreEqual(("A3", true), (read.PaperName, read.Landscape));
            Assert.AreEqual(document.PrinterSettings.PrinterName, read.PrinterName);
        }
#endif
    }
}
