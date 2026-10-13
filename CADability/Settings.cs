using CADability.Actions;
using CADability.GeoObject;
using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization;
#if WEBASSEMBLY
using CADability.WebDrawing;
#else
using System.Drawing;
#endif


namespace CADability
{
    using CADability.Attribute;
    using CADability.Substitutes;
    using System.Collections.Generic;
    using System.Threading;
    using UserInterface;

    internal class SettingSerializationBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            try
            {
                // System.Diagnostics.Trace.WriteLine(typeName);
                if (typeName.Contains("CADability, Version="))
                {
                    int ind = typeName.IndexOf("CADability, Version=");
                    int ind1 = typeName.IndexOf(",", ind + 20);
                    typeName = typeName.Remove(ind + 20, ind1 - ind - 20);
                    typeName = typeName.Insert(ind + 20, "*");
                }
                Type t = Type.GetType(typeName);
                if (t == null && typeName == "Color") // das macht Probleme wg. verschiedenem Framework
                {
                    Color clr = Color.Black;
                    t = clr.GetType();
                }
                if (t == null && typeName.StartsWith("System.Collections.Generic.Dictionary") && typeName.Contains("System.String")
                    && typeName.Contains("System.Windows.Forms.Shortcut")) // das macht Probleme wg. verschiedenem Framework
                {
                    //Dictionary<string, Shortcut> dct = new Dictionary<string, Shortcut>();
                    //t = dct.GetType();
                }
                if (t == null)
                {
                    t = Type.GetType(typeName + ", " + assemblyName);
                    if (t == null)
                    {
                        t = Type.GetType(typeName);
                    }
                    if (t == null)
                    {
                        t = typeof(DummySerialized);
                    }
                }
                return t;
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
                return typeof(DummySerialized);
            }
        }
    }
    [Serializable]
    internal class DummySerialized : ISerializable
    {
        Dictionary<string, object> deserializedObjects;
        #region ISerializable Members
        protected DummySerialized(SerializationInfo info, StreamingContext context)
        {
            deserializedObjects = new Dictionary<string, object>();
            SerializationInfoEnumerator e = info.GetEnumerator();
            while (e.MoveNext())
            {
                deserializedObjects[e.Name] = e.Value;
            }
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // sollte nicht drankommen
        }

        #endregion
    }

    /// <summary>
    /// Exception thrown by the methods of the <see cref="Settings"/> class, e.g. when a setting is accessed with an invalid name.
    /// </summary>

    public class SettingsException : System.Exception
    {
        /// <summary>
        /// Creates the exception with the given message.
        /// </summary>
        /// <param name="Info">The message (<see cref="Exception.Message"/>)</param>
        public SettingsException(string Info)
            : base(Info)
        {
        }
    }

    /// <summary>
    /// A setting has been changed
    /// </summary>
    public delegate void SettingChangedDelegate(string Name, object NewValue);
    /// <summary>
    /// Implemented by objects that can be values of a <see cref="Settings"/> object and can be changed interactively,
    /// e.g. <see cref="BooleanProperty"/>, <see cref="ColorSetting"/>, <see cref="MultipleChoiceSetting"/> and Settings
    /// itself (for sub settings). The containing Settings object subscribes to the event to notice changes of its values
    /// and forwards them as its own <see cref="Settings.SettingChangedEvent"/>.
    /// </summary>
    public interface ISettingChanged
    {
        /// <summary>
        /// Raised when the value has been changed. The handler receives the name of the setting and the new value.
        /// </summary>
        event SettingChangedDelegate SettingChangedEvent;
    }
    /// <summary>
    /// Delegate for the notification of a change of a value
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="NewValue"></param>
    public delegate void ValueChangedDelegate(object sender, object NewValue);


    /// <summary>
    /// Lets the application control where the global settings (<see cref="Settings.GlobalSettings"/>) are stored: attach a
    /// handler to <see cref="GetGlobalSettingsFileName"/> before the settings are first used to provide the file name, or
    /// set <see cref="neverSaveGlobalSettings"/> to suppress saving. Without a handler the file
    /// "CADability.GlobalSettings.json" in the current directory is used if it exists there and the directory is
    /// writable, otherwise the same file in the local application data folder (or in "My Documents" if that is not
    /// available).
    /// </summary>
    public class SettingsGlobalFileName
    {
        /// <summary>
        /// Delegate for <see cref="GetGlobalSettingsFileName"/>.
        /// </summary>
        /// <returns>The full path of the global settings file, or null to use the default location</returns>
        public delegate string GetGlobalSettingsFileNameDelegate();
        /// <summary>
        /// Queried for the name of the global settings file when the global settings are loaded or saved. Attach a handler
        /// before <see cref="Settings"/> is first used (the first access loads the settings), e.g. at application start. If no
        /// handler is attached or it returns null, the default location is used (see the class description).
        /// </summary>
        static public event GetGlobalSettingsFileNameDelegate GetGlobalSettingsFileName;
        /// <summary>
        /// When true, <see cref="Settings.SaveGlobalSettings()"/> does nothing, so the global settings are never written to
        /// disk, e.g. for tests or when running from a read-only location. Default is false.
        /// </summary>
        static public bool neverSaveGlobalSettings = false;
        internal static string GetName()
        {
            if (GetGlobalSettingsFileName != null) return GetGlobalSettingsFileName();
            return null;
        }
    }

    /// <summary>
    /// This class is used to give access and store information that influences global behavior of the
    /// CADability system.
    /// There is a static variable <see cref="Settings.GlobalSettings"/>. This is the only use of settings in CADability.
    /// This class behaves as a hierarchical dictionary. The keys are strings, which may have the form "mainkey.subkey"
    /// The values are objects, i.e. any kind of data.
    /// If the objects implement the IShowProperty interface they are displayed in the global setting tab of the controlcenter.
    /// There are some classes like <see cref="ColorSetting"/>, <see cref="IntegerProperty"/>, <see cref="DoubleProperty"/>,
    /// <see cref="StringProperty"/> which can be used as a setting value. If you instead simply use a double or string value
    /// the setting will be only available to programming code but not to the user in the ControlCenter.
    /// </summary>
    [Serializable()]
    public class Settings : IShowPropertyImpl, ISerializable, ISettingChanged, IAttributeListContainer, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
    {
        private Hashtable entries; // Zugriff auf die Settings über den Namen
        [Serializable()]
        private class Pair : IJsonSerialize
        {
            public string Name;
            public object Value;
            public Pair(string name, object val)
            {
                Name = name;
                Value = val;
            }
            protected Pair() // Constructor for JSonSerialize
            {
            }
            public void GetObjectData(IJsonWriteData data)
            {
                data.AddProperty("Name", Name);
                data.AddProperty("Value", Value);
            }

            public void SetObjectData(IJsonReadData data)
            {
                Name = data.GetStringProperty("Name");
                Value = data.GetProperty<object>("Value");
            }
        }
        private ArrayList sortedEntries; // enthält (Name,Value)-Paare, MasterDaten
        private bool modified;
        private bool deserialized; // leider notwendig um das deserialisieren und den callback in eine vernünftige ordnung zu bringen
        /// <summary>
        /// Internal state: the name (key) of this Settings object within the Settings object that contains it as sub settings,
        /// null for a root object like the global settings. It qualifies the names passed to
        /// <see cref="SettingChangedEvent"/> ("mainkey.subkey").
        /// </summary>
        protected string myName; // Name dieses Settings, wenn in einem anderen Setting anthalten
        private static Settings globalSettings;
        /// <summary>
        /// The global settings contain many different settings or configurations for the program execution.
        /// The settings are displayed in the "global" tab of the controlcenter. User code may add or remove settings.
        /// <see cref="Settings.AddSetting"/>.
        /// </summary>
        public static Settings GlobalSettings
        {
            get
            {
                if (globalSettings == null) Reload();
                return globalSettings;
            }
            set
            {
                globalSettings = value;
            }
        }
        /// <summary>
        /// Writes the global settings (<see cref="GlobalSettings"/>) in JSON format to the given file, overwriting an existing
        /// file. <see cref="SaveGlobalSettings()"/> uses the standard file.
        /// </summary>
        /// <param name="FileName">The name of the file to write</param>
        public static void SaveGlobalSettings(string FileName)
        {
            Stream stream = File.Open(FileName, FileMode.Create);
            JsonSerialize js = new JsonSerialize();
            js.ToStream(stream, GlobalSettings);
            stream.Close();
        }
        /// <summary>
        /// Writes the global settings to the global settings file (see <see cref="SettingsGlobalFileName"/>), unless
        /// <see cref="SettingsGlobalFileName.neverSaveGlobalSettings"/> is set. The frame calls it when it is disposed, so
        /// that changes made in the control center persist.
        /// </summary>
        public static void SaveGlobalSettings()
        {
            //Assembly ThisAssembly = Assembly.GetExecutingAssembly();
            //int lastSlash = ThisAssembly.Location.LastIndexOf('\\');
            //SaveGlobalSettings(ThisAssembly.Location.Substring(0, lastSlash + 1) + "CADability.GlobalSettings.bin");
            if (SettingsGlobalFileName.neverSaveGlobalSettings) return;
            InitGlobalSettingsFileName();
            SaveGlobalSettings(GlobalSettingsFileName);
        }
        /// <summary>
        /// Reads the global settings from the given JSON file and makes them the <see cref="GlobalSettings"/>, disposing the
        /// previous ones. The active language of the <see cref="StringTable"/> is set from the "Language" setting of the file
        /// before the file is read a second time, so that language dependent choices are built correctly. Exceptions (file
        /// not found, invalid content) are propagated. Normally the settings are loaded automatically when
        /// <see cref="GlobalSettings"/> is first used, which falls back to default settings if the file cannot be read and
        /// adds settings that are missing in the file; this method alone does not add them.
        /// </summary>
        /// <param name="FileName">The name of the file to read</param>
        public static void LoadGlobalSettings(string FileName)
        {
            Stream stream = null;
            try
            {
                stream = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                JsonSerialize jsonSerialize = new JsonSerialize();
                if (globalSettings != null) globalSettings.Dispose();
                GlobalSettings = (Settings)jsonSerialize.FromStream(stream);
                // die Sprache muss vor DeserializationDone gesetzt sein, denn dort werden bereits Comboboxen generiert
                int lid = GlobalSettings.GetLanguageId();
                StringTable.SetActiveLanguage(lid);
                //finishDeserialization.DeserializationDone();
                GlobalSettings.resourceIdInternal = "GlobalSettings";
                GlobalSettings.modified = false;
                // und nochmal einlesen, jetzt mit der richtigen SparchID
                stream.Seek(0, SeekOrigin.Begin);
                jsonSerialize = new JsonSerialize();
                GlobalSettings = (Settings)jsonSerialize.FromStream(stream);
                // FinishDeserialization kommt wohl nicht mehr dran...
                GlobalSettings.resourceIdInternal = "GlobalSettings";
                GlobalSettings.modified = false;
            }
            finally
            {
                stream?.Close();
            }
        }
        static string GlobalSettingsFileName;
        static void InitGlobalSettingsFileName()
        {
            string filename = "\\CADability.GlobalSettings.json";
            GlobalSettingsFileName = SettingsGlobalFileName.GetName();
            if (GlobalSettingsFileName == null)
            {
                string path = Directory.GetCurrentDirectory();
                FileInfo fi = new FileInfo(path + filename);
                DirectoryInfo di = new DirectoryInfo(path);
                if (!fi.Exists || (di.Attributes & FileAttributes.ReadOnly) != 0)
                {   // wenns ihn noch nicht gibt, dann bei LocalApplicationData machen
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    di = new DirectoryInfo(path);
                    if (!di.Exists || (di.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    }
                }
                GlobalSettingsFileName = path + filename;
            }
        }
        static Settings()
        {
            Reload();
        }
        internal static void Reload()
        {
            try
            {
                InitGlobalSettingsFileName();
                LoadGlobalSettings(GlobalSettingsFileName);
            }
            catch(Exception ex)
            {
                if (ex is ThreadAbortException)
                    throw;
                else                
                    globalSettings = new Settings();                
            }
            
            if (globalSettings == null)
            {
                // LoadGlobalSettings fängt exceptions ab, deshalb hier nochmal der test
                globalSettings = new Settings();
            }
            // nach dem Laden oder neu Erstellen der GlobalSettings wird sichergestellt, dass
            // alle gewünschten Settings darin enthalten sind.
            // Die reihenfolge wie die einzelnen Settings hier zugefügt werden ist entscheidend 
            // für die Darstellung im ControlCenter
            GlobalSettings.resourceIdInternal = "GlobalSettings";

            // die Sprache muss als erstes stehen, da einige der folgenden properties schon auf die Stringtable
            // zurückgreifen und dazu die activelanguage gesetzt sein muss
            if (!GlobalSettings.ContainsSetting("Language"))
            {
                string[] values = StringTable.GetLanguageNames();
                MultipleChoiceSetting mcs = new MultipleChoiceSetting("Language.Selected", "Language", values);
                GlobalSettings.AddSetting("Language", mcs);
                int al = StringTable.GetActiveLanguage();
                mcs.SetSelection(al);
                mcs.CurrentSelection = mcs.CurrentIndex;
            }
            else
            {
                MultipleChoiceSetting mcs = GlobalSettings.GetValue("Language") as MultipleChoiceSetting;
                if (mcs != null)
                {
                    mcs.choices = StringTable.GetLanguageNames();
                    StringTable.SetActiveLanguage(mcs.CurrentSelection);
                    mcs.CurrentSelection = mcs.CurrentSelection; // die choices waren beim Lesen noch nicht vorhanden
                }
            }
            AddMissingSettings();
        }
        /// <summary>
        /// Obsolete, does nothing. It remains from the time when CADability used OpenCascade as its geometry kernel.
        /// </summary>
        /// <param name="allow">Not used</param>
        public static void AllowOpenCascadeMultiThreading(bool allow)
        {
        }
        internal static void AddMissingSettings()
        {

            if (!GlobalSettings.ContainsSetting("ColorList") || GlobalSettings.ColorList.Count == 0)
            {
                GlobalSettings.AddSetting("ColorList", ColorList.GetDefault());
            }
            if (GlobalSettings.ColorList.Current == null)
                GlobalSettings.ColorList.Current = GlobalSettings.ColorList[0];
            (GlobalSettings as IAttributeListContainer).ColorList.menuResourceId = "MenuId.Settings.ColorList";

            if (!GlobalSettings.ContainsSetting("LayerList"))
            {
                LayerList ll = LayerList.GetDefault();
                GlobalSettings.AddSetting("LayerList", ll);
            }
            (GlobalSettings as IAttributeListContainer).LayerList.menuResourceId = "MenuId.Settings.LayerList";

            if (!GlobalSettings.ContainsSetting("LineWidthList"))
            {
                LineWidthList lwl = LineWidthList.GetDefault();
                GlobalSettings.AddSetting("LineWidthList", lwl);
            }
            (GlobalSettings as IAttributeListContainer).LineWidthList.menuResourceId = "MenuId.Settings.LineWidthList";

            if (!GlobalSettings.ContainsSetting("LinePatternList"))
            {
                LinePatternList lwl = LinePatternList.GetDefault();
                GlobalSettings.AddSetting("LinePatternList", lwl);
            }
            (GlobalSettings as IAttributeListContainer).LinePatternList.menuResourceId = "MenuId.Settings.LinePatternList";

            if (!GlobalSettings.ContainsSetting("HatchStyleList"))
            {
                HatchStyleList hsl = HatchStyleList.GetDefault(GlobalSettings);
                GlobalSettings.AddSetting("HatchStyleList", hsl);
            }
            (GlobalSettings as IAttributeListContainer).HatchStyleList.menuResourceId = "MenuId.Settings.HatchStyleList";

            if (!GlobalSettings.ContainsSetting("DimensionStyleList"))
            {
                DimensionStyleList dsl = DimensionStyleList.GetDefault();
                GlobalSettings.AddSetting("DimensionStyleList", dsl);
            }
            (GlobalSettings as IAttributeListContainer).DimensionStyleList.menuResourceId = "MenuId.Settings.DimStyleList";

            if (!GlobalSettings.ContainsSetting("StyleList"))
            {
                StyleList sl = StyleList.GetDefault(GlobalSettings);
                GlobalSettings.AddSetting("StyleList", sl);
                (sl as IAttributeList).Initialize();
            }
            (GlobalSettings as IAttributeListContainer).StyleList.menuResourceId = "MenuId.Settings.StyleList";

            if (!GlobalSettings.ContainsSetting("DefaultModelSize"))
            {
                GlobalSettings.AddSetting("DefaultModelSize", new BoundingCube(0, 100, 0, 100, 0, 100));
            }
            if (!GlobalSettings.ContainsSetting("Formatting"))
            {
                GlobalSettings.AddSetting("Formatting", StandardFormatting);
            }
            else
            {
                Settings s = GlobalSettings.GetSubSetting("Formatting");
                s.resourceIdInternal = "Setting.Formatting";
            }
            if (!GlobalSettings.ContainsSetting("Formatting.Angle"))
            {
                Settings FormattingSetting = GlobalSettings.GetSubSetting("Formatting");
                Settings Angle = new Settings();
                Angle.myName = "Angle";
                Angle.resourceIdInternal = "Setting.Formatting.Angle";
                MultipleChoiceSetting mcang = new MultipleChoiceSetting("Setting.Formatting.Angle.Mode", "Mode");
                mcang.CurrentSelection = 0; // |Grad (dezimal)|Grad (Minuten)|Grad (Minuten,Sekunden)|Bogenmaß
                Angle.AddSetting("Mode", mcang);
                IntegerProperty dec = new IntegerProperty("Setting.Formatting.Angle.Digits", "Digits");
                dec.IntegerValue = 3;
                Angle.AddSetting("Digits", dec);
                FormattingSetting.AddSetting("Angle", Angle);
            }
            if (!GlobalSettings.ContainsSetting("Formatting.Radius"))
            {
                Settings FormattingSetting = GlobalSettings.GetSubSetting("Formatting");
                BooleanProperty mcradius = new BooleanProperty("Setting.Formatting.Radius", "Setting.Formatting.Radius.Values", "Radius");
                mcradius.BooleanValue = true; // 
                FormattingSetting.AddSetting("Radius", mcradius);
            }
            if (!GlobalSettings.ContainsSetting("Formatting.GeneralDouble"))
            {
                Settings FormattingSetting = GlobalSettings.GetSubSetting("Formatting");
                IntegerProperty generalnumdigits = new IntegerProperty("Setting.Formatting.GeneralDouble", "GeneralDouble");
                generalnumdigits.IntegerValue = 3; // Nachkommastellen einzelner Komponenten
                FormattingSetting.AddSetting("GeneralDouble", generalnumdigits);
            }
            if (!GlobalSettings.ContainsSetting("Select"))
            {
                GlobalSettings.AddSetting("Select", SelectObjectsSettings.GetDefault());
            }
            else
            {
                if (GlobalSettings.ContainsSetting("Select.UseFixPoint"))
                {
                    GlobalSettings.RemoveSetting("Select.UseFixPoint");
                }
            }
            if (!GlobalSettings.ContainsSetting("Action"))
            {
                GlobalSettings.AddSetting("Action", new ActionSettings(true));
            }
            else
            {
                ActionSettings acts = GlobalSettings.GetValue("Action") as ActionSettings;
                if (acts != null)
                {
                    acts.Initialize();
                }
            }
            if (!(GlobalSettings.GetValue("Action.PopProperties") is BooleanProperty))
            {	// das soll BoolenProperty sein, war früher anders
                GlobalSettings.RemoveSetting("Action");
                GlobalSettings.AddSetting("Action", new ActionSettings(true));
            }
            Settings colorSettings = null;
            if (!GlobalSettings.ContainsSetting("Colors"))
            {
                colorSettings = new Settings();
                colorSettings.myName = "Colors";
                colorSettings.resourceIdInternal = "Setting.Colors";
                GlobalSettings.AddSetting("Colors", colorSettings);
            }
            else
            {
                colorSettings = GlobalSettings.GetSubSetting("Colors");
            }
            if (!colorSettings.ContainsSetting("Background"))
            {
                ColorSetting cs = new ColorSetting("Background", "Setting.Colors.Background");
                cs.Color = Color.AliceBlue;
                colorSettings.AddSetting("Background", cs);
            }
            if (!colorSettings.ContainsSetting("Grid"))
            {
                ColorSetting cs = new ColorSetting("Grid", "Setting.Colors.Grid");
                cs.Color = Color.LightGoldenrodYellow;
                colorSettings.AddSetting("Grid", cs);
            }
            if (!colorSettings.ContainsSetting("Feedback"))
            {
                ColorSetting cs = new ColorSetting("Feedback", "Setting.Colors.Feedback");
                cs.Color = Color.DarkGray;
                colorSettings.AddSetting("Feedback", cs);
            }
            if (!colorSettings.ContainsSetting("Layout"))
            {
                ColorSetting cs = new ColorSetting("Layout", "Setting.Colors.Layout");
                cs.Color = Color.LightYellow;
                colorSettings.AddSetting("Layout", cs);
            }
            if (!colorSettings.ContainsSetting("Drawingplane"))
            {
                ColorSetting cs = new ColorSetting("Drawingplane", "Setting.Colors.Drawingplane");
                cs.Color = Color.LightSkyBlue;
                colorSettings.AddSetting("Drawingplane", cs);
            }
            if (!colorSettings.ContainsSetting("ActiveFrame"))
            {
                ColorSetting cs = new ColorSetting("ActiveFrame", "Setting.Colors.ActiveFrame");
                cs.Color = Color.LightBlue;
                colorSettings.AddSetting("ActiveFrame", cs);
            }
            if (!GlobalSettings.ContainsSetting("Snap"))
            {
                Settings SnapSetting = new Settings();
                SnapSetting.myName = "Snap";
                SnapSetting.resourceIdInternal = "Setting.Snap";
                GlobalSettings.AddSetting("Snap", SnapSetting);
                BooleanProperty Snap30 = new BooleanProperty("Snap.Snap30", "YesNo.Values", "Snap30");
                Snap30.BooleanValue = false;
                SnapSetting.AddSetting("Circle30", Snap30);
                BooleanProperty Snap45 = new BooleanProperty("Snap.Snap45", "YesNo.Values", "Snap45");
                Snap45.BooleanValue = false;
                SnapSetting.AddSetting("Circle45", Snap45);
            }
            if (!GlobalSettings.ContainsSetting("Snap.SnapLocalOrigin"))
            {
                Settings SnapSetting = GlobalSettings.GetSubSetting("Snap");
                BooleanProperty SnapLocalOrigin = new BooleanProperty("Snap.SnapLocalOrigin", "YesNo.Values", "SnapLocalOrigin");
                SnapLocalOrigin.BooleanValue = true;
                SnapSetting.AddSetting("SnapLocalOrigin", SnapLocalOrigin);
            }
            if (!GlobalSettings.ContainsSetting("Snap.SnapGlobalOrigin"))
            {
                Settings SnapSetting = GlobalSettings.GetSubSetting("Snap");
                BooleanProperty SnapGlobalOrigin = new BooleanProperty("Snap.SnapGlobalOrigin", "YesNo.Values", "SnapGlobalOrigin");
                SnapGlobalOrigin.BooleanValue = true;
                SnapSetting.AddSetting("SnapGlobalOrigin", SnapGlobalOrigin);
            }
            if (!GlobalSettings.ContainsSetting("Approximate"))
            {
                Settings ApproximateSetting = new Settings();
                ApproximateSetting.resourceIdInternal = "Approximate";
                GlobalSettings.AddSetting("Approximate", ApproximateSetting);
                DoubleProperty prec = new DoubleProperty("Precision", "Approximate.Precision", 0.001, null);
                ApproximateSetting.AddSetting("Precision", prec);
                MultipleChoiceSetting mcs = new MultipleChoiceSetting("Approximate.Mode", "Approximate");
                mcs.CurrentSelection = 0;
                ApproximateSetting.AddSetting("Mode", mcs);
            }
            if (!GlobalSettings.ContainsSetting("Path"))
            {
                Settings PathSetting = new Settings();
                PathSetting.resourceIdInternal = "Path";
                GlobalSettings.AddSetting("Path", PathSetting);
                DoubleProperty gap = new DoubleProperty("MaxGap", "Path.MaxGap", 0.001, null);
                PathSetting.AddSetting("MaxGap", gap);
            }
            if (!GlobalSettings.ContainsSetting("UseReferences"))
            {
                MultipleChoiceSetting mcs = new MultipleChoiceSetting("Symbols.UseReferences", "UseReferences");
                GlobalSettings.AddSetting("UseReferences", mcs);
            }

            if (!GlobalSettings.ContainsSetting("Ruler"))
            {
                Settings RulerSetting = new Settings();
                RulerSetting.resourceIdInternal = "Ruler";
                RulerSetting.myName = "Ruler";
                GlobalSettings.AddSetting("Ruler", RulerSetting);
                MultipleChoiceSetting mcs = new MultipleChoiceSetting("Ruler.Show", "Show");
                mcs.CurrentSelection = 0; // kein Lineal anzeigen
                RulerSetting.AddSetting("Show", mcs);
            }
            if (!GlobalSettings.ContainsSetting("DxfDwg"))
            {
                Settings DxfDwgSetting = new Settings();
                DxfDwgSetting.resourceIdInternal = "DxfDwg";
                DxfDwgSetting.myName = "DxfDwg";
                GlobalSettings.AddSetting("DxfDwg", DxfDwgSetting);
                MultipleChoiceSetting mcsVersion = new MultipleChoiceSetting("DxfDwg.Version", "Version");
                mcsVersion.CurrentSelection = 7;
                DxfDwgSetting.AddSetting("Version", mcsVersion);
            }
            // "Format" (DWG/DXF/DXB) and "Text" (Fit/Original) were never read, the export dialog chooses the format
            if (GlobalSettings.ContainsSetting("DxfDwg.Format")) GlobalSettings.RemoveSetting("DxfDwg.Format");
            if (GlobalSettings.ContainsSetting("DxfDwg.Text")) GlobalSettings.RemoveSetting("DxfDwg.Text");
            if (!GlobalSettings.ContainsSetting("DxfDwg.ImportDimension"))
            {
                Settings DxfDwgSetting = GlobalSettings.GetSubSetting("DxfDwg");
                MultipleChoiceSetting mcsDimension = new MultipleChoiceSetting("DxfDwg.ImportDimension", "ImportDimension");
                mcsDimension.CurrentSelection = 0;
                DxfDwgSetting.AddSetting("ImportDimension", mcsDimension);
            }
            if (!GlobalSettings.ContainsSetting("DxfDwg.ExportDimension"))
            {
                Settings DxfDwgSetting = GlobalSettings.GetSubSetting("DxfDwg");
                MultipleChoiceSetting mcsDimension = new MultipleChoiceSetting("DxfDwg.ExportDimension", "ExportDimension");
                mcsDimension.CurrentSelection = 0;
                DxfDwgSetting.AddSetting("ExportDimension", mcsDimension);
            }
            if (!GlobalSettings.ContainsSetting("StepImport"))
            {
                Settings StepImportSetting = new Settings();
                StepImportSetting.resourceIdInternal = "StepImport";
                StepImportSetting.myName = "StepImport";
                GlobalSettings.AddSetting("StepImport", StepImportSetting);
                BooleanProperty parallel = new BooleanProperty("StepImport.Parallel", "YesNo.Values", "Parallel");
                parallel.BooleanValue = true;
                StepImportSetting.AddSetting("Parallel", parallel);
                BooleanProperty combineFaces = new BooleanProperty("StepImport.CombineFaces", "YesNo.Values", "CombineFaces");
                combineFaces.BooleanValue = true;
                StepImportSetting.AddSetting("CombineFaces", combineFaces);
            }
            if (!GlobalSettings.ContainsSetting("StepImport.Blocks"))
            {
                Settings StepImportSetting = GlobalSettings.GetSubSetting("StepImport");
                BooleanProperty makeBlocks = new BooleanProperty("StepImport.Blocks", "YesNo.Values", "Blocks");
                makeBlocks.BooleanValue = true;
                StepImportSetting.AddSetting("Blocks", makeBlocks);
            }
            if (!GlobalSettings.ContainsSetting("StepImport.PreferNonPeriodic"))
            {
                Settings StepImportSetting = GlobalSettings.GetSubSetting("StepImport");
                BooleanProperty preferNonPeriodic = new BooleanProperty("StepImport.PreferNonPeriodic", "YesNo.Values", "PreferNonPeriodic");
                preferNonPeriodic.BooleanValue = false;
                StepImportSetting.AddSetting("PreferNonPeriodic", preferNonPeriodic);
            }
            if (!GlobalSettings.ContainsSetting("Grid"))
            {
                Settings GridSetting = new Settings();
                GridSetting.resourceIdInternal = "Grid";
                GridSetting.myName = "Grid";
                GlobalSettings.AddSetting("Grid", GridSetting);
                DoubleProperty dxProperty = new DoubleProperty("XDistance", "Grid.XDistance", 10.0, null);
                DoubleProperty dyProperty = new DoubleProperty("YDistance", "Grid.YDistance", 10.0, null);
                MultipleChoiceSetting mcs = new MultipleChoiceSetting("Grid.DisplayMode", "DisplayMode");
                mcs.CurrentSelection = 0;
                GridSetting.AddSetting("XDistance", dxProperty);
                GridSetting.AddSetting("YDistance", dyProperty);
                GridSetting.AddSetting("DisplayMode", mcs);
                GlobalSettings.AddSetting("Grid", GridSetting);
            }
            if (!GlobalSettings.ContainsSetting("ShortCut"))
            {
                // GlobalSettings.AddSetting("ShortCut", new ShortCuts());
            }
            if (!GlobalSettings.ContainsSetting("Font"))
            {
                Settings FontSetting = new Settings();
                FontSetting.resourceIdInternal = "Font";
                FontSetting.myName = "Font";
                GlobalSettings.AddSetting("Font", FontSetting);
                MultipleChoiceSetting mcs = new MultipleChoiceSetting("Font.DisplayMode", "DisplayMode");
                mcs.CurrentSelection = 0;
                FontSetting.AddSetting("DisplayMode", mcs);
                MultipleChoiceSetting mcsp = new MultipleChoiceSetting("Font.Precision", "Precision");
                mcsp.CurrentSelection = 0;
                FontSetting.AddSetting("Precision", mcsp);
                GlobalSettings.AddSetting("Font", FontSetting);
            }
            if (!GlobalSettings.ContainsSetting("Printing"))
            {
                Settings PrintSetting = new Settings();
                PrintSetting.resourceIdInternal = "Printing";
                PrintSetting.myName = "Printing";
                GlobalSettings.AddSetting("Printing", PrintSetting);
                MultipleChoiceSetting mcs = new MultipleChoiceSetting("Printing.Mode", "PrintingMode");
                mcs.CurrentSelection = 0;
                PrintSetting.AddSetting("PrintingMode", mcs);
                // hier sollten noch mehr Einstellungen folgen
            }
            if (!GlobalSettings.ContainsSetting("Printing.GDIShading"))
            {
                Settings PrintSetting = GlobalSettings.GetSubSetting("Printing");
                BooleanProperty GDIShading = new BooleanProperty("Printing.GDIShading", "YesNo.Values", "GDIShading");
                GDIShading.BooleanValue = true;
                PrintSetting.AddSetting("GDIShading", GDIShading);
            }
            if (!GlobalSettings.ContainsSetting("Printing.GDIBitmap"))
            {
                Settings PrintSetting = GlobalSettings.GetSubSetting("Printing");
                BooleanProperty GDIBitmap = new BooleanProperty("Printing.GDIBitmap", "YesNo.Values", "GDIBitmap");
                GDIBitmap.BooleanValue = true;
                PrintSetting.AddSetting("GDIBitmap", GDIBitmap);
            }
            if (!GlobalSettings.ContainsSetting("Printing.GDICoverage"))
            {
                Settings PrintSetting = GlobalSettings.GetSubSetting("Printing");
                BooleanProperty GDICoverage = new BooleanProperty("Printing.GDICoverage", "YesNo.Values", "GDICoverage");
                GDICoverage.BooleanValue = false;
                PrintSetting.AddSetting("GDICoverage", GDICoverage);
            }
            if (!GlobalSettings.ContainsSetting("Printing.UseZOrder"))
            {
                Settings PrintSetting = GlobalSettings.GetSubSetting("Printing");
                BooleanProperty UseZOrder = new BooleanProperty("Printing.UseZOrder", "YesNo.Values", "UseZOrder");
                UseZOrder.BooleanValue = true;
                PrintSetting.AddSetting("UseZOrder", UseZOrder);
            }
            if (!GlobalSettings.ContainsSetting("Experimental"))
            {
                Settings ExperimentalSetting = new Settings();
                ExperimentalSetting.resourceIdInternal = "Experimental";
                ExperimentalSetting.myName = "Experimental";
                GlobalSettings.AddSetting("Experimental", ExperimentalSetting);
            }
            if (!GlobalSettings.ContainsSetting("Experimental.TestNewContextMenu"))
            {
                Settings ExperimentalSetting = GlobalSettings.GetSubSetting("Experimental");
                BooleanProperty TestNewContextMenu = new BooleanProperty("Experimental.TestNewContextMenu", "YesNo.Values", "TestNewContextMenu");
                TestNewContextMenu.BooleanValue = true;
                ExperimentalSetting.AddSetting("TestNewContextMenu", TestNewContextMenu);
            }
        }
        private static Settings StandardFormatting
        {
            get
            {
                Settings res = new Settings();
                res.myName = "Formatting";
                res.resourceIdInternal = "Setting.Formatting";
                MultipleChoiceSetting mcdim = new MultipleChoiceSetting("Setting.Formatting.Dimension", "Dimension");
                mcdim.CurrentSelection = 2; // immer 2D | vorzugsweise 2D | immer 3D
                res.AddSetting("Dimension", mcdim);

                MultipleChoiceSetting mcdec = new MultipleChoiceSetting("Setting.Formatting.Decimal", "Decimal");
                mcdec.CurrentSelection = 0; // Systemeinstellung | Punkt | Komma
                res.AddSetting("Decimal", mcdec);

                MultipleChoiceSetting mcsys = new MultipleChoiceSetting("Setting.Formatting.System", "System");
                mcsys.CurrentSelection = 0; // lokal | absolut | beides
                res.AddSetting("System", mcsys);

                // eingeschachtelt: Koordinaten
                Settings coordSystem = new Settings();
                coordSystem.myName = "Coordinate";
                coordSystem.resourceIdInternal = "Setting.Formatting.Coordinate";

                IntegerProperty coordnum = new IntegerProperty("Setting.Formatting.Coordinate.Digits", "Digits");
                coordnum.IntegerValue = 1; // Nachkommastellen Übersicht
                coordSystem.AddSetting("Digits", coordnum);

                IntegerProperty coordnumcomp = new IntegerProperty("Setting.Formatting.Coordinate.ComponentsDigits", "ComponentsDigits");
                coordnumcomp.IntegerValue = 3; // Nachkommastellen einzelner Komponenten
                coordSystem.AddSetting("ComponentsDigits", coordnumcomp);

                IntegerProperty generalnumdigits = new IntegerProperty("Setting.Formatting.GeneralDouble", "GeneralDouble");
                generalnumdigits.IntegerValue = 3; // Nachkommastellen einzelner Komponenten
                res.AddSetting("GeneralDouble", generalnumdigits);

                MultipleChoiceSetting mcshowz = new MultipleChoiceSetting("Setting.Formatting.Coordinate.ZValue", "ZValue");
                mcshowz.CurrentSelection = 0; // darstellen | nicht darstellen
                coordSystem.AddSetting("ZValue", mcshowz);

                res.AddSetting("Coordinate", coordSystem);

                // eingeschachtelt: Vektoren (Digits wie Koordinaten)
                Settings vector = new Settings();
                vector.myName = "Vector";
                vector.resourceIdInternal = "Setting.Formatting.Vector";

                MultipleChoiceSetting vectormode = new MultipleChoiceSetting("Setting.Formatting.Vector.Mode", "Mode");
                vectormode.CurrentSelection = 0; // als Winkel | polar | X-Y-Z-Werte
                vector.AddSetting("Mode", vectormode);

                res.AddSetting("Vector", vector);


                // fehlt noch: 
                // Winkel: Grad mit Dezimalstellen, Grad Minuten, Grad Minuten Sekunden
                // Vektoren brauchen auch Dezimalstellen, Längen auch.

                return res;
            }
        }
        /// <summary>
        /// GlobalSettings has registered the Application.ApplicationExit event. This call saves the settings
        /// and unregisters the event.
        /// </summary>
        public static void ShutDown()
        {
            // GlobalSettings.OnApplicationExit(null, null);
            // Application.ApplicationExit -= new EventHandler(GlobalSettings.OnApplicationExit);
            GlobalSettings = null;
        }
        /// <summary>
        /// Creates an empty settings object. Sub settings are created automatically by
        /// <see cref="AddSetting(string, object)"/> with a dotted name; the deserialization also uses this constructor.
        /// </summary>
        public Settings()
        {
            entries = new Hashtable();
            sortedEntries = new ArrayList();
            modified = false;
        }

        //~Settings()
        //{
        //    if (this == GlobalSettings)
        //    {
        //        GlobalSettings.Dispose();
        //    }
        //}

        /// <summary>
        /// Creates an empty settings object with the given resource id, which determines the label and the tooltip when the
        /// settings are shown in the control center (see <see cref="IShowPropertyImpl.resourceIdInternal"/>).
        /// </summary>
        /// <param name="ResourceID">The resource id for the <see cref="StringTable"/></param>
        public Settings(string ResourceID)
        {
            entries = new Hashtable();
            sortedEntries = new ArrayList();
            modified = false;
            resourceIdInternal = ResourceID;
        }

        /// <summary>
        /// Adds a setting with the given name and value, replacing an existing setting of that name. A name of the form
        /// "mainkey.subkey" puts the value into the sub settings "mainkey", which are created if necessary. Values that
        /// implement <see cref="ISettingChanged"/> are observed so that their changes raise <see cref="SettingChangedEvent"/>,
        /// attribute lists (<see cref="IAttributeList"/>) get this object as their owner. Raises
        /// <see cref="SettingChangedEvent"/> with the new value and marks the settings as modified. The settings keep the
        /// order in which they were added, which is the order of display in the control center.
        /// </summary>
        /// <param name="Name">The name (key) of the setting, dotted for sub settings</param>
        /// <param name="Value">The value. An object implementing <see cref="IShowProperty"/> (e.g. <see cref="BooleanProperty"/>,
        /// <see cref="IntegerProperty"/>, <see cref="DoubleProperty"/>, <see cref="ColorSetting"/>,
        /// <see cref="MultipleChoiceSetting"/>) is shown and can be edited in the control center; a plain value (bool, int,
        /// double, string) is only accessible by code</param>
        public void AddSetting(string Name, object Value)
        {
            string[] Parts = Name.Split(new Char[] { '.' }, 2); // beim 1. Punkt auftrennen
            modified = true;
            if (Parts.Length == 1)
            {
                // überschreibt bestehenden. Vollständigkeitshalber müsste der bestehende 
                // die events abmelden
                RemoveSetting(Name);
                entries[Name] = Value;
                sortedEntries.Add(new Pair(Name, Value));
                ISettingChanged sc = Value as ISettingChanged;
                if (sc != null) sc.SettingChangedEvent += new SettingChangedDelegate(OnSettingChanged);

                IAttributeList al = Value as IAttributeList;
                if (al != null) al.Owner = this;

                INotifyModification nm = Value as INotifyModification;
                if (nm != null) nm.DidModifyEvent += new DidModifyDelegate(OnDidModify);
            }
            else if (Parts.Length == 2)
            {
                Settings SubSettings = entries[Parts[0]] as Settings;
                if (SubSettings == null)
                {
                    SubSettings = new Settings();
                    SubSettings.myName = Parts[0];
                    entries[Parts[0]] = SubSettings;
                    sortedEntries.Add(new Pair(Parts[0], SubSettings));

                }
                SubSettings.AddSetting(Parts[1], Value);
            }

            if (SettingChangedEvent != null) SettingChangedEvent(Name, Value);
        }
        /// <summary>
        /// Removes the setting with the given name (dotted names address sub settings). Raises
        /// <see cref="SettingChangedEvent"/> with a null value before the setting is removed. Nothing happens if there is
        /// no such setting.
        /// </summary>
        /// <param name="Name">The name of the setting</param>
        public void RemoveSetting(string Name)
        {
            //TODO: fehlt noch events entfernen
            string[] Parts = Name.Split(new Char[] { '.' }, 2); // beim 1. Punkt auftrennen
            if (Parts.Length == 1)
            {
                if (entries.Contains(Name))
                {
                    OnSettingChanged(Name, null);
                    entries.Remove(Name);
                    for (int i = 0; i < sortedEntries.Count; ++i)
                    {
                        Pair p = sortedEntries[i] as Pair;
                        if (p.Name == Name)
                        {
                            sortedEntries.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            else
            {
                Settings SubSettings = entries[Parts[0]] as Settings;
                if (SubSettings != null)
                {
                    SubSettings.RemoveSetting(Parts[1]);
                }
            }
        }
        /// <summary>
        /// Checks whether a setting with the given name exists (dotted names address sub settings).
        /// </summary>
        /// <param name="Name">The name of the setting</param>
        /// <returns>true if the setting exists</returns>
        public bool ContainsSetting(string Name)
        {
            string[] Parts = Name.Split(new Char[] { '.' }, 2); // beim 1. Punkt auftrennen
            if (Parts.Length == 1)
            {
                return entries.ContainsKey(Name);
            }
            else if (Parts.Length == 2)
            {
                Settings SubSettings = entries[Parts[0]] as Settings;
                if (SubSettings != null)
                {
                    return SubSettings.ContainsSetting(Parts[1]);
                }
                else
                {
                    return false;
                }
            }
            return false;
        }
        /// <summary>
        /// Changes the order of the settings, which is the order of display in the control center: the setting
        /// <paramref name="toMove"/> is placed directly before or after the setting <paramref name="moveTo"/>. Both names
        /// must be direct entries of this settings object (not dotted); if one of them does not exist, nothing happens.
        /// </summary>
        /// <param name="toMove">The name of the setting to move</param>
        /// <param name="moveTo">The name of the setting that marks the new position</param>
        /// <param name="setBefore">true to place it before <paramref name="moveTo"/>, false to place it after</param>
        public void RearrangeSetting(string toMove, string moveTo, bool setBefore)
        {
            Pair sourcePair = null;
            Pair targetPair = null;
            foreach (Pair p in sortedEntries)
            {
                if (p.Name == toMove)
                {
                    sourcePair = p;
                }
                else if (p.Name == moveTo)
                {
                    targetPair = p;
                }
            }
            if (sourcePair != null && targetPair != null)
            {
                sortedEntries.Remove(sourcePair);
                int ind = sortedEntries.IndexOf(targetPair);
                if (!setBefore) ++ind;
                sortedEntries.Insert(ind, sourcePair);
            }
        }
        internal int GetLanguageId()
        {   // das wird aufgerufen wenn das Objekt noch nicht DeserializationDone bekommen hat. Aber
            // sortedEntries sollte schon existieren
            if (sortedEntries == null) return -1;
            foreach (Pair p in sortedEntries)
            {
                if (p.Name == "Language")
                {
                    MultipleChoiceSetting mcs = p.Value as MultipleChoiceSetting;
                    if (mcs != null)
                    {
                        return mcs.CurrentSelection;
                    }
                }
            }
            return -1;
        }
        /// <summary>
        /// Returns the value of the setting with the given name. A dotted name ("mainkey.subkey") addresses a setting in the
        /// sub settings "mainkey". The value is the object as it was added: a plain value or a property object like
        /// <see cref="BooleanProperty"/>; the typed methods <see cref="GetBoolValue(string, bool)"/>,
        /// <see cref="GetIntValue(string, int)"/>, <see cref="GetDoubleValue(string, double)"/> and
        /// <see cref="GetStringValue(string, string)"/> unwrap it.
        /// </summary>
        /// <param name="Name">The name of the setting</param>
        /// <returns>The value, or null if there is no such setting</returns>
        public object GetValue(string Name)
        {
            // wenn der Name mit Punkten getrennt ist, so handelt es sich um SubSettings
            string[] Parts = Name.Split(new Char[] { '.' }, 2); // beim 1. Punkt auftrennen
            if (Parts.Length == 1) return entries[Name];
            else if (Parts.Length == 2)
            {
                Settings SubSettings = entries[Parts[0]] as Settings;
                if (SubSettings != null) return SubSettings.GetValue(Parts[1]);
            }
            return null;
        }
        /// <summary>
        /// Returns the setting with the given name as a double. The setting may be stored as a double or as a
        /// <see cref="DoubleProperty"/>.
        /// </summary>
        /// <param name="name">The name of the setting (dotted for sub settings)</param>
        /// <param name="defaultValue">The value to return if the setting does not exist or has another type</param>
        /// <returns>The value of the setting or <paramref name="defaultValue"/></returns>
        public double GetDoubleValue(string name, double defaultValue)
        {
            object o = GetValue(name);
            if (o is double d) 
	            return d;
            if (o is DoubleProperty property) 
	            return property.GetDouble();

            return defaultValue;
        }
        /// <summary>
        /// Returns the setting with the given name as an int. The setting may be stored as an int, a double (truncated), an
        /// <see cref="IntegerProperty"/> or a <see cref="MultipleChoiceSetting"/> (the index of the current selection).
        /// </summary>
        /// <param name="name">The name of the setting (dotted for sub settings)</param>
        /// <param name="defaultValue">The value to return if the setting does not exist or has another type</param>
        /// <returns>The value of the setting or <paramref name="defaultValue"/></returns>
        public int GetIntValue(string name, int defaultValue)
        {
            object o = GetValue(name);
            switch (o)
            {
	            case int i:
		            return i;
	            case double d:
		            return (int)d;
	            case IntegerProperty property:
		            return (int)property;
	            case MultipleChoiceSetting setting:
		            return setting.CurrentSelection;
	            default:
		            return defaultValue;
            }
        }
        /// <summary>
        /// Returns the setting with the given name as a bool. The setting may be stored as a bool or as a
        /// <see cref="BooleanProperty"/>.
        /// </summary>
        /// <param name="name">The name of the setting (dotted for sub settings)</param>
        /// <param name="defaultValue">The value to return if the setting does not exist or has another type</param>
        /// <returns>The value of the setting or <paramref name="defaultValue"/></returns>
        public bool GetBoolValue(string name, bool defaultValue)
        {
            object o = GetValue(name);
            switch (o)
            {
	            case bool b:
		            return b;
	            case BooleanProperty property:
		            return property.BooleanValue;
	            default:
		            return defaultValue;
            }
        }
        /// <summary>
        /// Returns the setting with the given name as a string. A <see cref="StringProperty"/> yields its text, any other
        /// existing value is converted with ToString().
        /// </summary>
        /// <param name="name">The name of the setting (dotted for sub settings)</param>
        /// <param name="defaultValue">The value to return if the setting does not exist</param>
        /// <returns>The value of the setting or <paramref name="defaultValue"/></returns>
        public string GetStringValue(string name, string defaultValue)
        {
            object o = GetValue(name);
            if (o is StringProperty property) 
	            return property.GetString();
            if (o != null) 
	            return o.ToString();
            return defaultValue;
        }
        /// <summary>
        /// Returns the names of all settings, one entry per setting in display order; the entries of a sub settings object
        /// follow its own entry with dotted names (e.g. "Snap.Circle30"), so every entry is a valid name for
        /// <see cref="GetValue(string)"/>. <see cref="Dispose"/> uses it to unsubscribe from all settings.
        /// </summary>
        /// <returns>One name per setting</returns>
        public string[] GetAllKeys()
        {
            List<string> res = new List<string>();
            for (int i = 0; i < sortedEntries.Count; i++)
            {
                Pair p = sortedEntries[i] as Pair;
                res.Add(p.Name);
                if (p.Value is Settings settings)
                {
                    string[] sub = settings.GetAllKeys();
                    for (int j = 0; j < sub.Length; j++)
                    {
                        res.Add(p.Name + "." + sub[j]);
                    }
                }
            }
            return res.ToArray();
        }
        /// <summary>
        /// Sets the value of the setting with the given name (dotted names address sub settings, which are created if
        /// necessary). A setting that does not exist is added (<see cref="AddSetting(string, object)"/>). If the existing
        /// setting is a property object (<see cref="BooleanProperty"/>, <see cref="IntegerProperty"/>,
        /// <see cref="DoubleProperty"/>, <see cref="ColorSetting"/>, <see cref="MultipleChoiceSetting"/>), the plain value
        /// (bool, int, double, <see cref="Color"/>, index of the selection) is set into that object, otherwise the value is
        /// replaced. A null value is ignored. Raises <see cref="SettingChangedEvent"/> and marks the settings as modified.
        /// </summary>
        /// <param name="name">The name of the setting</param>
        /// <param name="newValue">The new value</param>
        /// <exception cref="SettingsException">if the type of the new value does not fit the existing setting, or the name is invalid</exception>
        public void SetValue(string name, object newValue)
        {
            SetValue(name, newValue, false);
            OnSettingChanged(name, newValue);
        }
        private void SetValue(string name, object NewValue, bool notify)
        {
            // wenn der Name mit Punkten getrennt ist, so handelt es sich um SubSettings
            string[] Parts = name.Split(new Char[] { '.' }, 2); // beim 1. Punkt auftrennen
            if (Parts.Length == 2)
            {
                Settings SubSettings = entries[Parts[0]] as Settings;
                if (SubSettings == null) AddSetting(Parts[0], new Settings());
                SubSettings = entries[Parts[0]] as Settings;
                if (SubSettings != null) SubSettings.SetValue(Parts[1], NewValue, false);
            }
            else if (Parts.Length == 1)
            {
                if (NewValue == null)
                {
                }
                else if (entries.ContainsKey(name))
                {
                    if (NewValue.GetType() == entries[name].GetType())
                    {
                        entries[name] = NewValue; // damit sind die primitiven Typen abgedeckt
                    }
                    else
                    {	// der Key existiert, aber hat einen anderen Typ. Das sind die Fälle
                        // in denen ein IShowProperty Typ existiert. Wenn der cast von NewValue
                        // nicht klappt, dann gibt es halt eine exception, aber das ist explizit
                        // so gewollt
                        if (entries[name].GetType() == typeof(BooleanProperty))
                        {
                            ((BooleanProperty)(entries[name])).BooleanValue = (bool)NewValue;
                        }
                        else if (entries[name].GetType() == typeof(IntegerProperty) && NewValue is int value)
                        {
                            ((IntegerProperty)(entries[name])).SetInt(value);
                        }
                        else if (entries[name].GetType() == typeof(DoubleProperty) && NewValue is double newValue)
                        {
                            ((DoubleProperty)(entries[name])).SetDouble(newValue);
                        }
                        else if (entries[name].GetType() == typeof(ColorSetting))
                        {
                            ((ColorSetting)(entries[name])).Color = (Color)NewValue;
                        }
                        else if (entries[name].GetType() == typeof(MultipleChoiceSetting))
                        {
                            ((MultipleChoiceSetting)(entries[name])).SetSelection((int)NewValue);
                        }
                        else if (entries[name].GetType() == typeof(bool) && NewValue is BooleanProperty)
                        {
                            entries[name] = NewValue;
                        }
                        else if (entries[name].GetType() == typeof(double) && NewValue is int)
                        {   // maybe was read as a double from global settings
                            entries[name] = NewValue;
                        }
                        // TODO: andere PropertyTypen implementieren
                        else
                        {
                            throw new SettingsException("SetValue: invalid value type");
                        }
                    }
                    // Update der sortedEntries fehlte (26.08.16)
                    for (int i = 0; i < sortedEntries.Count; ++i)
                    {
                        Pair p = sortedEntries[i] as Pair;
                        if (p.Name == name)
                        {
                            p.Value = entries[name];
                            break;
                        }
                    }

                }
                else
                {
                    this.AddSetting(name, NewValue);
                }
            }
            else
            {
                throw new SettingsException("SetValue: parameter Name is invalid");
            }
        }
        /// <summary>
        /// Converts the given object into a bool. Values of type bool can be stored in the settings in two ways: as a primitive
        /// bool and as a <see cref="BooleanProperty"/>, which additionally knows how it can be changed interactively by the user
        /// in a property grid. If the given object has another type, an InvalidCastException is thrown.
        /// </summary>
        /// <param name="TheValue">An object, usually taken from the settings</param>
        /// <returns>The bool value of the object</returns>
        public static bool GetBoolValue(object TheValue)
        {
            if (TheValue.GetType() == typeof(bool)) return (bool)TheValue;
            if (TheValue.GetType() == typeof(BooleanProperty)) return (bool)(BooleanProperty)TheValue;
            throw new InvalidCastException("Settings.GetBoolValue: type of parameter is invalid");
        }
        /// <summary>
        /// Converts an object taken from the settings into an int. An int, an <see cref="IntegerProperty"/> and a
        /// <see cref="MultipleChoiceSetting"/> (the index of the current selection) are accepted; any other object is cast to
        /// int, which throws an <see cref="InvalidCastException"/> when that is not possible.
        /// </summary>
        /// <param name="TheValue">An object, usually taken from the settings</param>
        /// <returns>The int value of the object</returns>
        public static int GetIntValue(object TheValue)
        {
            if (TheValue.GetType() == typeof(int)) return (int)TheValue;
            if (TheValue.GetType() == typeof(IntegerProperty)) return (int)(IntegerProperty)TheValue;
            if (TheValue.GetType() == typeof(MultipleChoiceSetting)) return (TheValue as MultipleChoiceSetting).CurrentSelection;
            else return (int)TheValue; // auf die Gefahr hin, dass der cast nicht geht
        }
        /// <summary>
        /// Converts an object taken from the settings into a double. A double and a <see cref="DoubleProperty"/> are
        /// accepted; any other object is cast to double, which throws an <see cref="InvalidCastException"/> when that is
        /// not possible.
        /// </summary>
        /// <param name="theValue">An object, usually taken from the settings</param>
        /// <returns>The double value of the object</returns>
        public static double GetDoubleValue(object theValue)
        {
            if (theValue is double value) 
	            return value;
            if (theValue is DoubleProperty property) 
	            return property.GetDouble();
            return (double)theValue;
        }
        /// <summary>
        /// Returns the sub settings (the group) with the given name, e.g. "Snap" or "Printing" for groups of the global
        /// settings. Dotted names address nested groups.
        /// </summary>
        /// <param name="Name">The name of the group</param>
        /// <returns>The sub settings, or null if there is no such setting or it is not a Settings object</returns>
        public Settings GetSubSetting(string Name)
        {
            object o = GetValue(Name);
            if (o is Settings settings) return settings;
            return null;
        }
        /// <summary>
        /// Whether the settings have been changed since they were loaded or saved. All modifying methods and the changes
        /// made in the control center set it; writing the settings resets it. The global settings are saved when the frame
        /// is disposed, regardless of this flag.
        /// </summary>
        public bool Modified
        {
            get
            {
                return modified;
            }
            set
            {
                modified = value;
                bool b = (this == GlobalSettings);
            }
        }
        private IShowProperty[] ShowProperties; // die Anzeige wird hier lokal gehalten, um die TabIndizes setzen zu können
        /// <summary>
        /// Discards the cached property entries of this settings object and refreshes its display in the control center, if
        /// it is shown there. Call it after adding or removing settings while the settings are displayed.
        /// </summary>
        public void RefreshSubentries()
        {
            ShowProperties = null;
            if (propertyTreeView != null)
                propertyTreeView.Refresh(this);


        }
        #region IShowPropertyImpl Overrides
        //		public override string LabelText
        //		{
        //			get
        //			{
        //				return StringTable.GetString(resourceId);
        //			}
        //			set
        //			{
        //				base.LabelText = value; // sollte nicht vorkommen
        //			}
        //		}

        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
        /// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
        /// </summary>
        public override ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.GroupTitle;
            }
        }
        /// <inheritdoc/>
        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }
        /// <inheritdoc/>
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (ShowProperties == null)
                {
                    ArrayList al = new ArrayList();
                    foreach (Pair p in sortedEntries)
                    {
                        if (p.Value is IShowProperty value)
                        {
                            if (value is Settings settings)
                            {
                                if (settings.resourceIdInternal == null) continue;
                                if (settings.resourceIdInternal.Length == 0) continue;
                            }
                            al.Add(value);
                        }
                    }
                    ShowProperties = (IShowProperty[])al.ToArray(typeof(IShowProperty));
                }
                return ShowProperties;
            }
        }
        /// <inheritdoc/>
        public override void Added(IPropertyPage propertyTreeView)
        {
            if (ShowProperties != null)
            {
                for (int i = 0; i < ShowProperties.Length; i++)
                {
                    if (ShowProperties[i] is ISettingChanged)
                    {
                        (ShowProperties[i] as ISettingChanged).SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
                        (ShowProperties[i] as ISettingChanged).SettingChangedEvent += new SettingChangedDelegate(OnSettingChanged);
                    }
                }
            }
            base.Added(propertyTreeView);
        }
        /// <inheritdoc/>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            if (ShowProperties != null)
            {
                for (int i = 0; i < ShowProperties.Length; i++)
                {
                    if (ShowProperties[i] is ISettingChanged)
                    {
                        (ShowProperties[i] as ISettingChanged).SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
                    }
                }
            }
            base.Removed(propertyTreeView);

        }
        /// <summary>
        /// Releases the global settings: if this object is the <see cref="GlobalSettings"/>, it unsubscribes from the events of
        /// its values and sets <see cref="GlobalSettings"/> to null, so that they are reloaded from the file on the next
        /// access. For other settings objects it does nothing. The frame calls it when it is disposed, after saving the
        /// settings.
        /// </summary>
        public void Dispose()
        {
            if (GlobalSettings == this)
            {
                // remove all event-references to this GlobalSettings
                string[] allKeys = GlobalSettings.GetAllKeys();
                for (int i = 0; i < allKeys.Length; i++)
                {
                    object s = GlobalSettings.GetValue(allKeys[i]);
                    if (s is ISettingChanged sc) sc.SettingChangedEvent -= OnSettingChanged;
                    if (s is INotifyModification nm) nm.DidModifyEvent -= OnDidModify;
                }
                // StringTable.RemoveSettingsChanged();
                GlobalSettings = null;
            }
        }
        /// <inheritdoc/>
        public override string HelpLink
        {
            get
            {
                return base.HelpLink;
            }
        }

        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Settings(SerializationInfo info, StreamingContext context)
        {
            try
            {
                sortedEntries = (ArrayList)info.GetValue("SortedEntries", typeof(ArrayList));
            }
            catch (SerializationException)
            {	// früher wurden die unsortierten entries abgespeichert
                entries = (Hashtable)info.GetValue("Entries", typeof(Hashtable));
            }
            try
            {
                resourceIdInternal = (string)info.GetValue("ResourceId", typeof(string));
            }
            catch (SerializationException)
            {	// resourceId sollte hier unverändert sein
                resourceIdInternal = "";
            }
            try
            {
                myName = (string)info.GetValue("Name", typeof(string));
            }
            catch (SerializationException)
            {
            }
            deserialized = false;
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("SortedEntries", sortedEntries, sortedEntries.GetType());
            info.AddValue("ResourceId", resourceIdInternal, typeof(string));
            info.AddValue("Name", myName, typeof(string));
            modified = false;
        }
        /// <summary>
        /// Implements <see cref="IJsonSerialize.GetObjectData(IJsonWriteData)"/>: writes the settings as name/value pairs in
        /// display order (sub settings recursively), the resource id and the name of this settings object. Resets
        /// <see cref="Modified"/>.
        /// </summary>
        /// <param name="data">Receives the data</param>
        public virtual void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("SortedEntries", sortedEntries);
            data.AddProperty("ResourceId", resourceIdInternal);
            data.AddProperty("Name", myName);
            modified = false;
        }

        /// <summary>
        /// Implements <see cref="IJsonSerialize.SetObjectData(IJsonReadData)"/>: reads the data written by
        /// <see cref="GetObjectData(IJsonWriteData)"/> and registers for <see cref="SerializationDone(JsonSerialize)"/>,
        /// where the lookup table and the event subscriptions are built.
        /// </summary>
        /// <param name="data">The data to read from</param>
        public virtual void SetObjectData(IJsonReadData data)
        {
            sortedEntries = data.GetProperty<ArrayList>("SortedEntries");
            resourceIdInternal = data.GetProperty<string>("ResourceId");
            myName = data.GetProperty<string>("Name");
            data.RegisterForSerializationDoneCallback(this);
        }
        /// <summary>
        /// Implements <see cref="IJsonSerializeDone.SerializationDone(JsonSerialize)"/>: called when all objects of the file
        /// have been read; completes the deserialization (<see cref="OnDeserialization()"/>).
        /// </summary>
        /// <param name="jsonSerialize">The serializer</param>
        public virtual void SerializationDone(JsonSerialize jsonSerialize)
        {
            OnDeserialization();
        }

        #endregion
        #region IDeserializationCallback Members
        /// <summary>
        /// Completes the deserialization (binary or JSON): builds the lookup table of the settings from the stored ordered
        /// list (or, for very old files, the list from the table), subscribes to the change events of the values, makes this
        /// object the owner of the attribute lists it contains and resets <see cref="Modified"/>. Does nothing when called
        /// a second time.
        /// </summary>
        protected void OnDeserialization()
        {
            if (deserialized) return;
            if (entries == null || entries.Count == 0)
            {
                entries = new Hashtable();
                foreach (Pair p in sortedEntries)
                {
                    entries[p.Name] = p.Value;
                }
            }
            else
            {	// früher wurden die unsortierten entries abgespeichert
                sortedEntries = new ArrayList();
                foreach (DictionaryEntry de in entries)
                {
                    sortedEntries.Add(new Pair(de.Key as string, de.Value));
                }
            }
            foreach (Pair p in sortedEntries)
            {
                ISettingChanged sc = p.Value as ISettingChanged;
                if (sc != null) sc.SettingChangedEvent += new SettingChangedDelegate(OnSettingChanged);

                INotifyModification nm = p.Value as INotifyModification;
                if (nm != null) nm.DidModifyEvent += new DidModifyDelegate(OnDidModify);

                IAttributeList al = p.Value as IAttributeList;
                if (al != null)
                {
                    al.Owner = this;
                }
            }
            modified = false;
            deserialized = true;
        }
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            OnDeserialization();
        }
        #endregion

        private void OnApplicationExit(object sender, EventArgs e)
        {	// nur GlobalSettings meldet sich dort an, deshalb hier problemlos:
            // das kommt nicht mehr dran, wird jetzt von SingleDocumentFrame.Dispose ausgelöst
            // if (this == GlobalSettings) das greift nicht, warum?
            {
                if (modified)
                {
                    //				if (MessageBox.Show(StringTable.GetString("GlobalSettings.SaveModified"),Application.ProductName,MessageBoxButtons.YesNo, MessageBoxIcon.Question)==DialogResult.Yes)
                    //				{
                    SaveGlobalSettings();
                    modified = false;
                    //				}
                }
            }
        }

        private void OnSettingChanged(string Name, object NewValue)
        {
            if (SettingChangedEvent != null)
            {
                if (myName != null) SettingChangedEvent(myName + "." + Name, NewValue);
                else SettingChangedEvent(Name, NewValue);
            }
            if (myName == null && Name == "Solid.OctTreeAlsoCheckInside" && NewValue is bool value)
            {
                Solid.octTreeAlsoCheckInside = value;
            }
            modified = true;
        }

        #region ISettingChanged Members

        /// <summary>
        /// Raised when a setting has been added, removed or changed, by the methods of this class or interactively in the
        /// control center. The handler receives the name of the setting, qualified with the names of the sub settings
        /// ("mainkey.subkey"), and the new value (null when the setting was removed). CADability itself listens to this
        /// event of the <see cref="GlobalSettings"/>, e.g. the frame and the views.
        /// </summary>
        public event CADability.SettingChangedDelegate SettingChangedEvent;

        #endregion

        #region IAttributeListContainer Members
        IAttributeList IAttributeListContainer.GetList(string keyName)
        {
            return GetValue(keyName) as IAttributeList;
        }
        int IAttributeListContainer.ListCount
        {
            get
            {
                int res = 0;
                foreach (DictionaryEntry list in entries)
                {
                    if (list.Value is IAttributeList)
                        res++;
                }
                return res;
            }
        }
        IAttributeList IAttributeListContainer.List(int keyIndex)
        {
            int i = 0;
            foreach (DictionaryEntry list in entries)
            {
                if (list.Value is IAttributeList value)
                {
                    if (i == keyIndex)
                        return value;
                    i++;
                }
            }
            return null;
        }
        string IAttributeListContainer.ListKeyName(int keyIndex)
        {
            int i = 0;
            foreach (DictionaryEntry list in entries)
            {
                if (list.Value is IAttributeList)
                {
                    if (i == keyIndex)
                        return list.Key as string;
                    i++;
                }
            }
            return null;
        }
        void IAttributeListContainer.Add(string KeyName, IAttributeList ToAdd)
        {
            if (entries.ContainsKey(KeyName))
                throw new AttributeException("KeyName already exists in Settings", AttributeException.AttributeExceptionType.InvalidArg);
            if (ToAdd != null)
            {
                entries.Add(KeyName, ToAdd);
                sortedEntries.Add(new Pair(KeyName, ToAdd));
                ToAdd.Owner = this;
                if (propertyTreeView != null)
                {
                    ShowProperties = null;
                    propertyTreeView.Refresh(this);
                }
            }
        }
        void IAttributeListContainer.Remove(string KeyName)
        {
            entries.Remove(KeyName);
            for (int i = 0; i < sortedEntries.Count; ++i)
            {
                Pair p = sortedEntries[i] as Pair;
                if (p.Name == KeyName)
                {
                    sortedEntries.RemoveAt(i);
                    break;
                }
            }
        }
        /// <summary>
        /// The colours (<see cref="CADability.Attribute.ColorList"/>) stored in these settings under the key "ColorList", or null if
        /// there is none. The global settings contain the default attribute lists, which a new <see cref="Project"/> clones
        /// as its initial lists.
        /// </summary>
        public ColorList ColorList
        {
            get
            {
                return GetValue("ColorList") as ColorList;
            }
        }
        /// <summary>
        /// The layers (<see cref="CADability.Attribute.LayerList"/>) stored in these settings under the key "LayerList", or null if
        /// there is none. The global settings contain the default attribute lists, which a new <see cref="Project"/> clones
        /// as its initial lists.
        /// </summary>
        public LayerList LayerList
        {
            get
            {
                return GetValue("LayerList") as LayerList;
            }
        }
        /// <summary>
        /// The hatch styles (<see cref="CADability.Attribute.HatchStyleList"/>) stored in these settings under the key "HatchStyleList", or null if
        /// there is none. The global settings contain the default attribute lists, which a new <see cref="Project"/> clones
        /// as its initial lists.
        /// </summary>
        public HatchStyleList HatchStyleList
        {
            get
            {
                return GetValue("HatchStyleList") as HatchStyleList;
            }
        }
        /// <summary>
        /// The dimension styles (<see cref="CADability.Attribute.DimensionStyleList"/>) stored in these settings under the key "DimensionStyleList", or null if
        /// there is none. The global settings contain the default attribute lists, which a new <see cref="Project"/> clones
        /// as its initial lists.
        /// </summary>
        public DimensionStyleList DimensionStyleList
        {
            get
            {
                return GetValue("DimensionStyleList") as DimensionStyleList;
            }
        }
        /// <summary>
        /// The line widths (<see cref="CADability.Attribute.LineWidthList"/>) stored in these settings under the key "LineWidthList", or null if
        /// there is none. The global settings contain the default attribute lists, which a new <see cref="Project"/> clones
        /// as its initial lists.
        /// </summary>
        public LineWidthList LineWidthList
        {
            get
            {
                return GetValue("LineWidthList") as LineWidthList;
            }
        }
        /// <summary>
        /// The line patterns (<see cref="CADability.Attribute.LinePatternList"/>) stored in these settings under the key "LinePatternList", or null if
        /// there is none. The global settings contain the default attribute lists, which a new <see cref="Project"/> clones
        /// as its initial lists.
        /// </summary>
        public LinePatternList LinePatternList
        {
            get
            {
                return GetValue("LinePatternList") as LinePatternList;
            }
        }
        /// <summary>
        /// The styles (<see cref="CADability.Attribute.StyleList"/>) stored in these settings under the key "StyleList", or null if
        /// there is none. The global settings contain the default attribute lists, which a new <see cref="Project"/> clones
        /// as its initial lists.
        /// </summary>
        public StyleList StyleList
        {
            get
            {
                return GetValue("StyleList") as StyleList;
            }
        }
        void IAttributeListContainer.AttributeChanged(IAttributeList list, INamedAttribute attribute, ReversibleChange change)
        {
            modified = true;
        }
        bool IAttributeListContainer.RemovingItem(IAttributeList list, INamedAttribute attribute, string resourceId)
        {
            if (list.Count < 2)
            {
                FrameImpl.MainFrame.UIService.ShowMessageBox(StringTable.GetString(resourceId + ".DontRemoveLastItem"), StringTable.GetString(resourceId + ".Label"), MessageBoxButtons.OK);
                return false; // nicht entfernen
            }
            return true;
        }
        void IAttributeListContainer.UpdateList(IAttributeList list) { }
        #endregion

        private void OnDidModify(object sender, EventArgs args)
        {
            modified = true;
        }

    }
}
