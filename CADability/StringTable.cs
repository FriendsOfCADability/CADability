using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml;

namespace CADability.UserInterface
{
    public enum InfoLevelMode { NoInfo, SimpleInfo, DetailedInfo };

    /// <summary>
    /// StringTable is a class that returns language dependent strings from (hard-coded)
    /// string IDs. All language dependent user interface texts are processed here.
    /// CADability contains an XML file as a primary string resource, containing all user 
    /// interface texts in German and English language.
    /// You can Add more strings and more languages by calling <see cref="AddStrings"/>,
    /// or by simply providing an XML file named "CADability.StringTable.xxx.xml" (where xxx
    /// stands for any language or application specific abbreviation) in the directory where
    /// CADability.dll is located.
    /// </summary>
    public class StringTable
    {
        private struct Strings
        {
            public string label;
            public string tip;
            public string info;
        }
        private static Dictionary<string, string> allLanguages; // languageId -> languageName
        private static Dictionary<string, Dictionary<string, Strings>> allStrings; // stringId -> languageId -> text
        private static string activeLanguage;
        private static string defaultLanguage;
#if DEBUG
        private static HashSet<string> notifiedMissingString = new HashSet<string>();
#endif
        static StringTable()
        {
            allLanguages = new Dictionary<string, string>();
            allStrings = new Dictionary<string, Dictionary<string, Strings>>();

            Assembly ThisAssembly = Assembly.GetExecutingAssembly();
            //System.IO.Stream str = ThisAssembly.GetManifestResourceStream("CADability.StringTable.xml");
            //XmlDocument stringXmlDocument = new XmlDocument();
            //stringXmlDocument.Load(str);

            string lan = System.Globalization.CultureInfo.CurrentUICulture.Name;
            if (lan.StartsWith("de"))
                activeLanguage = "deutsch";
            else
                activeLanguage = "english";
            defaultLanguage = "deutsch";

            System.IO.Stream str = ThisAssembly.GetManifestResourceStream("CADability.StringTableDeutsch.xml");
            XmlDocument stringXmlDocument = new XmlDocument();
            stringXmlDocument.Load(str);
            AddStrings(stringXmlDocument);

            str = ThisAssembly.GetManifestResourceStream("CADability.StringTableEnglish.xml");
            stringXmlDocument = new XmlDocument();
            stringXmlDocument.Load(str);
            AddStrings(stringXmlDocument);


            int pos = ThisAssembly.Location.LastIndexOf('\\');
            if (pos > 0)
            {   // in WebAssemby there is no Location
                string cd = ThisAssembly.Location.Substring(0, pos);
                string[] xmlfiles = System.IO.Directory.GetFiles(cd, "CADability.StringTable.*.xml");
                foreach (string filename in xmlfiles)
                {
                    try
                    {
                        FileStream stream = File.Open(filename, FileMode.Open);
                        XmlDocument doc = new XmlDocument();
                        doc.Load(stream);
                        AddStrings(doc);
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException) throw (e);
                    }
                }
            }
            AddString("deutsch", "MenuId.ToggleDebugFlag", Category.label, "Debug Flag");
            AddString("english", "MenuId.ToggleDebugFlag", Category.label, "Debug Flag");
#if DEBUG
            File.Delete(@"C:\Temp\MissingString.txt");
#endif
        }

        public static void AddStrings(System.IO.Stream str)
        {
            XmlDocument stringXmlDocument = new XmlDocument();
            stringXmlDocument.Load(str);
            AddStrings(stringXmlDocument);
        }
        public static void AddStrings(XmlDocument doc)
        {
            string language = "unknown";
            XmlNode lan = doc.SelectSingleNode("root/language");
            if (lan != null)
            {
                XmlNode atrid = lan.Attributes.GetNamedItem("id");
                language = atrid.Value;
                string languageName = lan.InnerText;
                allLanguages[language] = languageName;
            }
            XmlNodeList list = doc.SelectNodes("root/data");
            foreach (XmlNode node in list)
            {
                XmlNode atrname = node.Attributes.GetNamedItem("name");
                string name = atrname.Value;
                if (name != null)
                {
                    if (!allStrings.ContainsKey(name))
                    {
                        allStrings[name] = new Dictionary<string, Strings>();
                    }
                    Dictionary<string, Strings> entry = allStrings[name];
                    Strings val = new Strings();
                    XmlNode label = node.SelectSingleNode("label");
                    if (label != null) val.label = label.InnerText;
                    XmlNode tip = node.SelectSingleNode("tip");
                    if (tip != null) val.tip = tip.InnerText;
                    XmlNode info = node.SelectSingleNode("info");
                    if (info != null) val.info = info.InnerText;
                    entry[language] = val;
                }
            }
        }

        /// <summary>
        /// Gets or sets the active language
        /// </summary>
        public static string ActiveLanguage
        {
            get
            {
                return activeLanguage;
            }
            set
            {
                activeLanguage = value;
                if (ActiveLanguageChangedEvent != null) ActiveLanguageChangedEvent(activeLanguage);
            }
        }
        /// <summary>
        /// Delegate definition for the <see cref="ActiveLanguageChangedEvent"/>.
        /// </summary>
        /// <param name="newActiveLanguage">the name of the new active language</param>
        public delegate void ActiveLanguageChangedDelegate(string newActiveLanguage);
        /// <summary>
        /// Event which is fired when the active languae of the user interface is changed
        /// </summary>
        public static event ActiveLanguageChangedDelegate ActiveLanguageChangedEvent;
        /// <summary>
        /// Adds the strings of the given xml document to the string table.
        /// </summary>
        /// <param name="doc">xml document to parse</param>
        /// <remarks>
        /// See file "StringTable.xml" for the required xml schema.
        /// </remarks>
        /// <summary>
        /// Adds a single string to the Stringtable. This can be used for adding new strings or
        /// overriding existing strings.
        /// </summary>
        /// <param name="language">the language of the string</param>
        /// <param name="resourceID">the ID of the string</param>
        /// <param name="text">the string</param>
        public static void AddString(string language, string resourceID, Category cat, string text)
        {
            Strings strings = new Strings();
            if (!allStrings.ContainsKey(resourceID))
            {
                allStrings[resourceID] = new Dictionary<string, Strings>();
            }
            else
            {
                if (allStrings[resourceID].ContainsKey(language))
                {
                    strings = allStrings[resourceID][language];
                }
            }
            switch (cat)
            {
                case Category.label: strings.label = text; break;
                case Category.info: strings.info = text; break;
                case Category.tip: strings.tip = text; break;
            }
            allStrings[resourceID][language] = strings;
        }
        /// <summary>
        /// Catogory of resource strings
        /// </summary>
        public enum Category
        {
            /// <summary>
            /// The main item like it appears int a label of the control center or in en menu entry
            /// </summary>
            label,
            /// <summary>
            /// Detailed help information if detailed tooltips are selected
            /// </summary>
            info,
            /// <summary>
            /// Short tooltip information
            /// </summary>
            tip
        }
        public static bool IsStringDefined(string Name)
        {
            string searchstring = Name;
            if (Name.EndsWith(".Label")) searchstring = Name.Substring(0, Name.Length - 6);
            else if (Name.EndsWith(".ShortInfo")) searchstring = Name.Substring(0, Name.Length - 10);
            else if (Name.EndsWith(".DetailedInfo")) searchstring = Name.Substring(0, Name.Length - 13);
            return allStrings.ContainsKey(searchstring);
        }
        /// <summary>
        /// Returns the string with the given ID and the given <see cref="Category"/>in the <see cref="ActiveLanguage"/>. If the there is no 
        /// appropriate entry in the ActiveLanguage then the default language is searched. If there is still no 
        /// entry
        /// "missing string: "+Name is returned. 
        /// </summary>
        /// <param name="Name">Name or ID of the string</param>
        /// <param name="cat">the Category of the required string</param>
        /// <returns>the value of the string entry</returns>
        public static string GetString(string Name, Category cat)
        {
            // ist noch ein bisschen holperig: wenn est den Stringeintrag zwar in einer Sprache gibt
            // dort aber info z.B. leer ist und info gesucht ist, dann wird hier nichts gefunden. Es ist
            // kein Fallback auf unterem Level...
            if (Name == null) return "null!!!";
            string res = null;
            Dictionary<string, Strings> entry;
            if (allStrings.TryGetValue(Name, out entry))
            {
                Strings found;
                if (entry.TryGetValue(activeLanguage, out found))
                {
                    switch (cat)
                    {
                        case Category.label: res = found.label; break;
                        case Category.info: res = found.info; break;
                        case Category.tip: res = found.tip; break;
                    }
                    if (cat == Category.tip && String.IsNullOrEmpty(res)) res = found.info;
                }
                else
                {
                    if (entry.TryGetValue(defaultLanguage, out found))
                    {
                        switch (cat)
                        {
                            case Category.label: res = found.label; break;
                            case Category.info: res = found.info; break;
                            case Category.tip: res = found.tip; break;
                        }
                        if (cat == Category.tip && String.IsNullOrEmpty(res)) res = found.info;
                    }
                    else
                    {
                        foreach (Strings val in entry.Values)
                        {
                            switch (cat)
                            {
                                case Category.label: res = val.label; break;
                                case Category.info: res = val.info; break;
                                case Category.tip: res = val.tip; break;
                            }
                        }
                    }
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Name)) return "";
#if DEBUG
                if (!notifiedMissingString.Contains(Name))
                {
                    notifiedMissingString.Add(Name);
                    string txt = $@"
    <data name=""{Name}"">
		<label>???</label>
		<info>???</info>
	</data>";
                    File.AppendAllText(@"C:\Temp\MissingString.txt", txt);
                }
#endif
                return "missing string: " + Name;
            }
            if (res == null)
            {
                return "missing string: " + Name;
            }
            return res;
        }
        /// <summary>
        /// --- Depricated --- use GetString(string, Category) instead.
        /// Returns the string with the given ID in the <see cref="ActiveLanguage"/>. If the there is no 
        /// appropriate entry in the ActiveLanguage then the default language is searched. If there is still no 
        /// entry
		/// "missing string: "+Name is returned. 
		/// </summary>
		/// <param name="Name">Name or ID of the string</param>
		/// <returns>the value of the string entry</returns>
		public static string GetString(string Name)
        {
            if (Name.EndsWith(".Label")) return GetString(Name.Substring(0, Name.Length - 6), Category.label);
            else if (Name.EndsWith(".ShortInfo")) return GetString(Name.Substring(0, Name.Length - 10), Category.tip);
            else if (Name.EndsWith(".DetailedInfo")) return GetString(Name.Substring(0, Name.Length - 13), Category.info);
            else return GetString(Name, Category.label);
            //if (Name == null) return "null!!!";
            //try
            //{
            //    Dictionary<string, string> entry = allStrings[Name]; // wirft ggf. KeyNotFoundException
            //    try
            //    {
            //        return entry[activeLanguage];
            //    }
            //    catch (KeyNotFoundException)
            //    {
            //        try
            //        {
            //            return entry[defaultLanguage];
            //        }
            //        catch (KeyNotFoundException)
            //        {
            //            foreach (string val in entry.Values)
            //            {
            //                return val;
            //            }
            //            return "missing string: " + Name;
            //        }
            //    }
            //}
            //catch (KeyNotFoundException)
            //{
            //    return "missing string: " + Name;
            //}
        }
        /// <summary>
		/// Returns a formatted string. The string with the ID "Name" from the string resource
		/// is formatted by substituting the {0}, {1} ... substring with the string values
		/// of the args objects.
		/// If there is a formatting error, the unformatted string will be returned.
		/// </summary>
		/// <param name="Name">Name or ID of the string</param>
		/// <param name="args">Variable number of arguments for formatting</param>
		/// <returns>The formatted string</returns>
		public static string GetFormattedString(string Name, params object[] args)
        {
            string str = GetString(Name);
            try
            {
                return string.Format(str, args);
            }
            catch (FormatException)
            {
                return str;
            }
        }
        /// <summary>
        /// Returns an array of strings from the string table. The array is created from a single
        /// entry in the string table. The first character of this entry is the separator and
        /// the remaining string is splitted by this separator. 
        /// </summary>
        /// <param name="Name">Name or ID of the string</param>
        /// <returns>The exploded string</returns>
        public static string[] GetSplittedStrings(string Name)
        {
            string org = GetString(Name);
            string[] spl = org.Split(org[0]); // der erste string ist leer
            string[] res = new string[spl.Length - 1];
            for (int i = 1; i < spl.Length; ++i)
            {
                res[i - 1] = spl[i];
            }
            return res;
        }

        public static string[] GetLanguageNames()
        {
            List<string> res = new List<string>();
            foreach (KeyValuePair<string, string> lan in allLanguages)
            {   // muss die gleiche Reihenfolge haben wie unten SetActiveLanguage
                res.Add(lan.Value);
            }
            return res.ToArray();
        }
        public static void SetActiveLanguage(int index)
        {
            int i = 0;
            foreach (KeyValuePair<string, string> lan in allLanguages)
            {
                if (i >= index) // index kann auch -1 sein
                {
                    ActiveLanguage = lan.Key;
                    break;
                }
                ++i;
            }
            // darf nur einmal aufgerufen werden. Es wird genau beim Laden der GlobalSettings aufgerufen
            // damit der event nicht zweimal drinsteht wird er zuerst entfernt. Wenn er noch nicht drin war, so macht das nichts
            Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnGlobalSettingsChanged);
            Settings.GlobalSettings.SettingChangedEvent += new SettingChangedDelegate(OnGlobalSettingsChanged);
        }
        internal static int GetActiveLanguage()
        {
            int i = 0;
            foreach (KeyValuePair<string, string> lan in allLanguages)
            {
                if (ActiveLanguage == lan.Key) break;
                ++i;
            }
            // darf nur einmal aufgerufen werden. Es wird genau beim Laden der GlobalSettings aufgerufen
            Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnGlobalSettingsChanged);
            Settings.GlobalSettings.SettingChangedEvent += new SettingChangedDelegate(OnGlobalSettingsChanged);
            return i;
        }
        internal static void RemoveSettingsChanged()
        {
            Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnGlobalSettingsChanged);
        }

        public static void Dispose()
        {
            Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnGlobalSettingsChanged);
            allLanguages.Clear();
            allStrings.Clear();
            allLanguages = null;
            allStrings = null;
            activeLanguage = null;
            defaultLanguage = null;

            if (ActiveLanguageChangedEvent != null)
            {
                Delegate[] all = ActiveLanguageChangedEvent.GetInvocationList();
                for (int i = 0; i < all.Length; i++)
                {
                    Delegate removed = Delegate.RemoveAll(all[i], ActiveLanguageChangedEvent);
                }
                all = ActiveLanguageChangedEvent.GetInvocationList();
            }
        }

        static void OnGlobalSettingsChanged(string Name, object NewValue)
        {
            if (Name == "Language")
            {
                try
                {
                    int index = (int)NewValue;
                    int i = 0;
                    foreach (KeyValuePair<string, string> lan in allLanguages)
                    {
                        if (i >= index) // index kann auch -1 sein
                        {
                            ActiveLanguage = lan.Key;
                            string msg = StringTable.GetString("Language.ChangeMessage");
                            //System.Windows.Forms.MessageBox.Show(msg, System.Windows.Forms.Application.ProductName, System.Windows.Forms.MessageBoxButtons.OK);
                            break;
                        }
                        ++i;
                    }
                }
                catch (InvalidCastException) { } // NewValue muss int sein
            }
        }
    }
}
