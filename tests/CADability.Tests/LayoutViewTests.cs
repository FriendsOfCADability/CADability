using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Runtime.CompilerServices;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// A <see cref="LayoutView"/> can be shown without GDI+: it creates its GDI+ <c>PrintDocument</c> only when the user
    /// prints or opens the page setup (issue #561). Before, the constructor created it, and outside Windows showing a layout
    /// threw a PlatformNotSupportedException.
    /// </summary>
    [TestClass]
    public class LayoutViewTests
    {
        private static string CdbFile(string name, [CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "CDB", name);

        private static readonly FieldInfo printDocument = typeof(LayoutView).GetField("printDocument", BindingFlags.NonPublic | BindingFlags.Instance);

        [TestMethod]
        public void ANewLayoutCanBeShownWithoutPrinting()
        {
            Project project = Project.CreateSimpleProject();
            Layout layout = new Layout(project) { PaperWidth = 297, PaperHeight = 210 };
            LayoutView view = new LayoutView(layout, project);
            Assert.IsNull(printDocument.GetValue(view), "no print document before printing");
            Assert.AreSame(layout, view.Layout);
        }

        [TestMethod]
        public void ALayoutWithPageSettingsCanBeShownWithoutPrinting()
        {
            // the layouts of a 1.x file with GDI+ page settings, read into the neutral PageSetup
            Project project = Project.ReadFromFile(CdbFile("pagesettings-1x-net48.cdb.json"), "cdb");
            Assert.IsNotNull(project);
            Assert.IsTrue(project.LayoutCount > 0);
            for (int i = 0; i < project.LayoutCount; ++i)
            {
                Layout layout = project.GetLayout(i);
                Assert.IsNotNull(layout.PageSetup ?? project.DefaultPageSetup, layout.Name);
                LayoutView view = new LayoutView(layout, project);
                Assert.IsNull(printDocument.GetValue(view), layout.Name + ": no print document before printing");
            }
        }
    }
}
