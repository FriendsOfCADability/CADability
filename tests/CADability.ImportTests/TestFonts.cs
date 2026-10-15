using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CADability.ImportTests
{
    /// <summary>
    /// Texts get their fonts from FontProvider.Current. The tests add Liberation Sans (Files/Fonts) to the fonts of the system,
    /// so that texts have a font on every machine, also on a build server without any fonts installed (issue #412).
    /// </summary>
    [TestClass]
    public static class TestFonts
    {
        public static string Directory => System.IO.Path.Combine(AppContext.BaseDirectory, "Files", "Fonts");

        [AssemblyInitialize]
        public static void UseTestFonts(TestContext context)
        {
            FontProvider.Current = new ManagedFontProvider(new[] { Directory }, true);
        }
    }
}
