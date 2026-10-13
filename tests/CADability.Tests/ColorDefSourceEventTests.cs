using System.Drawing;
using CADability.Attribute;

namespace CADability.Tests
{
    /// <summary>
    /// Issue #426: the setter of <see cref="ColorDef.Source"/> invoked <c>ColorWillChangeEvent</c> under
    /// <c>if (ColorDidChangeEvent != null)</c>, so the did-change subscribers were never called, the will-change
    /// subscribers twice, and a ColorDef with only a did-change subscriber threw a NullReferenceException.
    /// </summary>
    [TestClass]
    public class ColorDefSourceEventTests
    {
        [TestMethod]
        public void SourceSetterRaisesDidChangeOnce()
        {
            ColorDef cd = new ColorDef("test", Color.Red, ColorDef.ColorSource.fromObject);
            int did = 0;
            cd.ColorDidChangeEvent += (sender, args) => did++;
            cd.Source = ColorDef.ColorSource.fromParent; // only a did-change subscriber: threw before the fix
            Assert.AreEqual(ColorDef.ColorSource.fromParent, cd.Source);
            Assert.AreEqual(1, did);
        }

        [TestMethod]
        public void SourceSetterRaisesWillChangeAndDidChangeOnceEach()
        {
            ColorDef cd = new ColorDef("test", Color.Red, ColorDef.ColorSource.fromObject);
            int will = 0, did = 0;
            cd.ColorWillChangeEvent += (sender, args) => { will++; Assert.AreEqual(0, did, "will-change is raised before did-change"); };
            cd.ColorDidChangeEvent += (sender, args) => did++;
            cd.Source = ColorDef.ColorSource.fromName;
            Assert.AreEqual(1, will);
            Assert.AreEqual(1, did);
            cd.Source = ColorDef.ColorSource.fromName; // no change, no events
            Assert.AreEqual(1, will);
            Assert.AreEqual(1, did);
        }
    }
}
