using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace CADability.Tests
{
    /// <summary>
    /// CADability.Forms makes its GDI font provider the default, so that Windows Forms applications show their texts as before
    /// issue #412, but a provider the application has set itself is kept. The tests change the static state of
    /// <see cref="FontProvider"/>, so they do not run in parallel with other tests and restore it afterwards.
    /// </summary>
    [TestClass]
    public class FontProviderDefaultTests
    {
        private sealed class NamedProvider : FontProvider
        {
            public readonly string Name;
            public NamedProvider(string name) { Name = name; }
            public override string[] GetFamilyNames() => new string[0];
            public override FontFace GetFace(string familyName, bool bold, bool italic) => null;
        }

        private static readonly FieldInfo current = typeof(FontProvider).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo chosenByApplication = typeof(FontProvider).GetField("chosenByApplication", BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// Runs <paramref name="test"/> as in a fresh process, where the application has not chosen a provider yet, and restores
        /// the provider of the other tests afterwards.
        /// </summary>
        private static void AsInAFreshProcess(Action test)
        {
            object savedCurrent = current.GetValue(null), savedChosen = chosenByApplication.GetValue(null);
            try
            {
                current.SetValue(null, null);
                chosenByApplication.SetValue(null, false);
                test();
            }
            finally
            {
                current.SetValue(null, savedCurrent);
                chosenByApplication.SetValue(null, savedChosen);
                CADability.GeoObject.FontCache.GlobalFontCache.Clear();
                CADability.GeoObject.Text.ResetFontFamilyNames();
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void TheDefaultIsTheManagedProvider()
        {
            AsInAFreshProcess(() => Assert.IsInstanceOfType(FontProvider.Current, typeof(ManagedFontProvider)));
        }

        [TestMethod]
        [DoNotParallelize]
        public void AFrontEndCanReplaceTheDefault()
        {
            AsInAFreshProcess(() =>
            {
                FontProvider.SetDefault(() => new NamedProvider("front end"));
                Assert.AreEqual("front end", ((NamedProvider)FontProvider.Current).Name);
            });
            // also after the managed default was already used
            AsInAFreshProcess(() =>
            {
                Assert.IsInstanceOfType(FontProvider.Current, typeof(ManagedFontProvider));
                FontProvider.SetDefault(() => new NamedProvider("front end"));
                Assert.AreEqual("front end", ((NamedProvider)FontProvider.Current).Name);
            });
        }

        [TestMethod]
        [DoNotParallelize]
        public void TheProviderOfTheApplicationIsKept()
        {
            // set before the front end is loaded
            AsInAFreshProcess(() =>
            {
                FontProvider.Current = new NamedProvider("application");
                FontProvider.SetDefault(() => new NamedProvider("front end"));
                Assert.AreEqual("application", ((NamedProvider)FontProvider.Current).Name);
            });
            // set after the front end is loaded
            AsInAFreshProcess(() =>
            {
                FontProvider.SetDefault(() => new NamedProvider("front end"));
                FontProvider.Current = new NamedProvider("application");
                FontProvider.SetDefault(() => new NamedProvider("front end"));
                Assert.AreEqual("application", ((NamedProvider)FontProvider.Current).Name);
            });
        }

#if WINDOWS
        private static readonly MethodInfo makeDefault = typeof(CADability.Forms.GdiFontProvider).GetMethod("MakeDefault", BindingFlags.NonPublic | BindingFlags.Static);

        [TestMethod]
        [DoNotParallelize]
        public void CADabilityFormsMakesGdiTheDefault()
        {
            FieldInfo madeDefault = typeof(CADability.Forms.GdiFontProvider).GetField("madeDefault", BindingFlags.NonPublic | BindingFlags.Static);
            object savedMadeDefault = madeDefault.GetValue(null);
            try
            {
                AsInAFreshProcess(() =>
                {
                    madeDefault.SetValue(null, 0);
                    makeDefault.Invoke(null, null);
                    Assert.IsInstanceOfType(FontProvider.Current, typeof(CADability.Forms.GdiFontProvider));
                });
                AsInAFreshProcess(() =>
                {
                    madeDefault.SetValue(null, 0);
                    FontProvider.Current = new ManagedFontProvider();
                    makeDefault.Invoke(null, null);
                    Assert.IsInstanceOfType(FontProvider.Current, typeof(ManagedFontProvider), "the choice of the application is kept");
                });
            }
            finally
            {
                madeDefault.SetValue(null, savedMadeDefault);
            }
        }

        [TestMethod]
        [DataRow(typeof(CADability.Forms.CadControl))]
        [DataRow(typeof(CADability.Forms.CadCanvas))]
        [DataRow(typeof(CADability.Forms.CadFrame))]
        [DataRow(typeof(CADability.Forms.PaintToOpenGL))]
        [DataRow(typeof(CADability.Forms.PaintToGDI))]
        public void TheMainClassesOfFormsMakeGdiTheDefault(Type formsClass)
        {
            // the static constructor calls GdiFontProvider.MakeDefault: look for "call MakeDefault" in its IL
            ConstructorInfo cctor = formsClass.TypeInitializer;
            Assert.IsNotNull(cctor, formsClass.Name + " has no static constructor");
            byte[] il = cctor.GetMethodBody().GetILAsByteArray();
            byte[] token = BitConverter.GetBytes(makeDefault.MetadataToken);
            bool found = false;
            for (int i = 0; i + 4 < il.Length && !found; ++i)
                found = il[i] == 0x28 && il[i + 1] == token[0] && il[i + 2] == token[1] && il[i + 3] == token[2] && il[i + 4] == token[3];
            Assert.IsTrue(found, formsClass.Name + " does not call GdiFontProvider.MakeDefault");
        }
#endif
    }
}
