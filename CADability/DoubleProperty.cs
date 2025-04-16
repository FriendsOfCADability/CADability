using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace CADability.UserInterface
{

    /// <summary>
    /// Implements a hotspot <see cref="IHotSpot"/> to manipulate a length via a length property
    /// </summary>

    public class DoubleHotSpot : IHotSpot
    {
        private DoubleProperty doubleProperty;
        public GeoPoint Position;
        /// <summary>
        /// Constructs a hotspot to manipulate a double property
        /// </summary>
        /// <param name="doubleProperty">the property to be manipulated</param>
        public DoubleHotSpot(DoubleProperty doubleProperty)
        {
            this.doubleProperty = doubleProperty;
        }
        #region IHotSpot Members
        /// <summary>
        /// Implements <see cref="CADability.IHotSpot.GetHotspotPosition ()"/>
        /// </summary>
        /// <returns></returns>
        public GeoPoint GetHotspotPosition()
        {
            return Position;
        }
        public bool IsSelected
        {
            get
            {
                return doubleProperty.IsSelected;
            }
            set
            {
                doubleProperty.IsSelected = value;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.IHotSpot.StartDrag(IFrame)"/>
        /// </summary>
        public void StartDrag(IFrame frame)
        {
            doubleProperty.StartModifyWithMouse();
        }
        public string GetInfoText(CADability.UserInterface.InfoLevelMode Level)
        {
            return doubleProperty.LabelText;
        }
        public string ResourceId
        {
            get { return doubleProperty.ResourceId; }
        }
        public MenuWithHandler[] ContextMenu
        {
            get
            {
                return doubleProperty.ContextMenu;
            }
        }
        public bool Hidden { get { return doubleProperty.ReadOnly; } }
        #endregion
    }

    [Serializable()]
    public class DoubleProperty : EditableProperty<double>, IJsonSerialize, ISerializable
    {
        private NumberFormatInfo numberFormatInfo;
        private string settingName = "";

        public DoubleProperty(IFrame frame, string resourceId = null) : base(resourceId, null)
        {
            InitFormat(frame);
        }

        public DoubleProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, IFrame frame, string resourceId = null) : base(getValueDelegate, setValueDelegate, resourceId, null)
        {
            InitFormat(frame);
        }

        public DoubleProperty(object ObjectWithLength, string PropertyName, string resourceId, IFrame frame, bool autoModifyWithMouse = true)
            : base(ObjectWithLength, PropertyName, resourceId, null)
        {
            InitFormat(frame);
        }

        public DoubleProperty(string SettingName, string resourceId, double initialValue, IFrame frame) : base(resourceId, null)
        {
            if (frame != null) InitFormat(frame);
            else
            {
                numberFormatInfo = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
                numberFormatInfo.NumberDecimalDigits = 29;
            }
            SetValue(initialValue, false);
        }

        private void InitFormat(IFrame frame)
        {
            numberFormatInfo = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
            int decsym = Settings.GlobalSettings.GetIntValue("Formatting.Decimal", 0); // Systemeinstellung | Punkt | Komma
                                                                                       // wenn 0, dann unverändert
            if (decsym == 1) numberFormatInfo.NumberDecimalSeparator = ".";
            else if (decsym == 2) numberFormatInfo.NumberDecimalSeparator = ",";
            if (frame != null)
            {
                numberFormatInfo.NumberDecimalDigits = frame.GetIntSetting("Formatting.GeneralDouble", 3);
            }
            else
            {
                numberFormatInfo.NumberDecimalDigits = 3;
            }
        }

        public void SetDouble(double l)
        {
            SetValue(l, true);
        }
        public double GetDouble()
        {
            return GetValue();
        }
        public void DoubleChanged()
        {
            propertyPage?.Refresh(this);
        }

        public int DecimalDigits { get => numberFormatInfo.NumberDecimalDigits; set => numberFormatInfo.NumberDecimalDigits = value; }

        protected override bool TextToValue(string text, out double val)
        {
            bool success = false;
            if (numberFormatInfo.NumberDecimalSeparator == ".") text = text.Replace(",", ".");
            if (numberFormatInfo.NumberDecimalSeparator == ",") text = text.Replace(".", ",");
            val = 0.0;
            try
            {
                val = double.Parse(text, numberFormatInfo);
                success = true;
            }
            catch (FormatException)
            {
                if (text == "-") success = true; // allow to start with "-"
            }
            catch (OverflowException)
            {
            }
            //Locked = false;
            if (!success)
            {
                try
                {
                    //Scripting s = new Scripting();
                    //IFrame Frame = propertyPage.Frame;
                    //if (Frame != null)
                    //{
                    //    if (numberFormatInfo.NumberDecimalSeparator == ",") text = text.Replace(",", "."); // this is ambiguous: there might be function calls with commas, 
                    //    // which are destroyed here, but I don't know what to do. When using formulas, the user should be forced to have "." as decimal separator
                    //    val = s.GetDouble(Frame.Project.NamedValues, text);
                    //    success = true;
                    //}
                }
                catch //(ScriptingException)
                {
                }
            }
            return success;

        }
        protected override string ValueToText(double val)
        {
            if (numberFormatInfo.NumberDecimalDigits == 29)
            {
                if (Math.Abs(val)<1.0) return val.ToString("0.#########################", numberFormatInfo);
                else return val.ToString("G29", numberFormatInfo);
            }
            return val.ToString("f", numberFormatInfo);
        }

        #region deprecated adaption to old implementation of DoubleProperty
        [Obsolete("Parameter autoModifyWithMouse no longer supported, use DoubleProperty(IFrame frame, string resourceId) instead")]
        public DoubleProperty(string resourceId, IFrame frame) : this(frame, resourceId)
        {
        }
        [Obsolete("Use DoubleProperty.GetDouble or DoubleProperty.SetDouble instead")]
        public double DoubleValue
        {
            get { return GetDouble(); }
            set
            {
                SetDouble(value);
            }
        }
        [Obsolete("use DoubleProperty.SetValueDelegate instead")]
        public delegate void SetDoubleDelegate(DoubleProperty sender, double l);
        [Obsolete("use DoubleProperty.GetValueDelegate instead")]
        public delegate double GetDoubleDelegate(DoubleProperty sender);
        [Obsolete("use delegate DoubleProperty.OnSetValue instead")]
        public event SetDoubleDelegate SetDoubleEvent
        {
            add
            {
                base.OnSetValue = delegate (double l) { value(this, l); };
            }
            remove
            {
                base.OnSetValue = null;
            }
        }
        [Obsolete("use delegate DoubleProperty.OnGetValue instead")]
        public event GetDoubleDelegate GetDoubleEvent
        {
            add
            {
                base.OnGetValue = delegate ()
                {
                    return value(this);
                };
            }
            remove
            {
                base.OnGetValue = null;
            }
        }
        [Obsolete("use delegate DoubleProperty.ModifyWithMouse instead")]
        public event ModifyWithMouseDelegate ModifyWithMouseEvent
        {
            add
            {
                ModifyWithMouse = delegate (IPropertyEntry pe, bool start)
                {
                    value(this, start);
                };
            }
            remove
            {
                ModifyWithMouse = null;
            }
        }
        [Obsolete("use delegate DoubleProperty.LockedChanged instead")]
        public event LockedChangedDelegate LockedChangedEvent
        {
            add
            {
                LockedChanged = delegate (bool locked)
                {
                    value(locked);
                };
            }
            remove
            {
                LockedChanged = null;
            }
        }
        [Obsolete("method has no functionality, remove this call")]
        public void CheckMouseButton(bool Check) { }
        [Obsolete("use delegate DoubleProperty.LabelTextChanged instead")]
		public new delegate void LabelChangedDelegate(DoubleProperty sender, string newLabel);
        [Obsolete("use delegate DoubleProperty.LabelTextChanged instead")]
#pragma warning disable CS0067 // Event is never used
        public event LabelChangedDelegate LabelChangedEvent;
#pragma warning restore CS0067 // Event is never used
        #endregion
        #region IJsonSerialize implementation (when this property is used as a setting)
        /// <summary>
        /// Empty constructor for IJsonSerialize  deserialisation
        /// </summary>
        protected DoubleProperty()
        {
            numberFormatInfo = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
            numberFormatInfo.NumberDecimalDigits = 29;
        }
        public virtual void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("ResourceId", resourceIdInternal);
            data.AddProperty("Value", GetValue());
            if (!string.IsNullOrEmpty(settingName)) data.AddProperty("SettingName", settingName);
        }
        public virtual void SetObjectData(IJsonReadData data)
        {
            resourceIdInternal = data.GetProperty<string>("ResourceId");
            SetValue(data.GetProperty<double>("Value"), false);
            if (data.HasProperty("SettingName")) settingName = data.GetStringProperty("SettingName");
        }
        #endregion
        #region ISerializable
        protected DoubleProperty(SerializationInfo info, StreamingContext context)
        {
            SetValue(info.GetDouble("InternalValue"), false);
            resourceIdInternal = (string)info.GetValue("resourceId", typeof(string));
            settingName = (string)info.GetValue("SettingName", typeof(string));
            //try
            //{
            //    minValue = info.GetDouble("MinValue");
            //    maxValue = info.GetDouble("MaxValue");
            //}
            //catch (SerializationException)
            //{
            //    minValue = double.MinValue;
            //    maxValue = double.MaxValue;
            //}
            numberFormatInfo = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
            numberFormatInfo.NumberDecimalDigits = 29;
        }
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("InternalValue", GetValue(), typeof(double));
            info.AddValue("resourceId", resourceIdInternal, resourceIdInternal.GetType());
            info.AddValue("SettingName", settingName, settingName.GetType());
            //info.AddValue("MinValue", minValue);
            //info.AddValue("MaxValue", maxValue);
        }
        #endregion
    }

}
