namespace CADability.Tests
{
    /// <summary>
    /// Issue #427: <see cref="Settings.GetAllKeys"/> returned strings of the form "name (value)", which
    /// <see cref="Settings.Dispose"/> passed to <see cref="Settings.GetValue(string)"/>, so the unsubscription from the
    /// global settings never found a setting.
    /// </summary>
    [TestClass]
    public class SettingsGetAllKeysTests
    {
        [TestMethod]
        public void KeysAreTheDottedNamesOfAllSettings()
        {
            Settings settings = new Settings();
            settings.AddSetting("A", 1);
            Settings sub = new Settings();
            sub.AddSetting("B", 2);
            sub.AddSetting("C", "three");
            settings.AddSetting("Sub", sub);
            settings.AddSetting("D", 4.0);

            string[] keys = settings.GetAllKeys();
            CollectionAssert.AreEqual(new[] { "A", "Sub", "Sub.B", "Sub.C", "D" }, keys);
        }

        [TestMethod]
        public void EveryKeyResolvesWithGetValue()
        {
            Settings settings = new Settings();
            settings.AddSetting("A", 1);
            Settings sub = new Settings();
            sub.AddSetting("B", 2);
            settings.AddSetting("Sub", sub);

            foreach (string key in settings.GetAllKeys())
            {
                Assert.IsNotNull(settings.GetValue(key), "key " + key);
            }
            Assert.AreEqual(2, settings.GetValue("Sub.B"));
        }
    }
}
