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
        /// <summary>
        /// The position of the hotspot in world coordinates, returned by <see cref="GetHotspotPosition"/>. The owner of the hotspot
        /// keeps it up to date, typically the position of the value in the drawing, e.g. the text position of a dimension.
        /// </summary>
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
        /// <inheritdoc/>
        public GeoPoint GetHotspotPosition()
        {
            return Position;
        }
        /// <summary>
        /// The selection state of the hotspot, which is the selection state of the associated <see cref="DoubleProperty"/> on its property page:
        /// selecting the hotspot selects the property entry in the control center and vice versa.
        /// </summary>
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
        /// <inheritdoc/>
        public void StartDrag(IFrame frame)
        {
            doubleProperty.StartModifyWithMouse();
        }
        /// <summary>
        /// Returns the tooltip text of the hotspot, which is the label of the associated <see cref="DoubleProperty"/>.
        /// </summary>
        /// <param name="Level">Requested info level; ignored, the label is returned for every level</param>
        /// <returns>The label text of the associated property</returns>
        public string GetInfoText(CADability.UserInterface.InfoLevelMode Level)
        {
            return doubleProperty.LabelText;
        }
        /// <summary>
        /// The resource id of the associated <see cref="DoubleProperty"/>, which identifies its label text.
        /// </summary>
        public string ResourceId
        {
            get { return doubleProperty.ResourceId; }
        }
        /// <summary>
        /// The context menu of the associated <see cref="DoubleProperty"/>. A double property has no standard context menu, so the menu is
        /// empty unless one was set with <see cref="EditableProperty{T}.SetContextMenu(string, ICommandHandler)"/> or
        /// <see cref="EditableProperty{T}.PrependContextMenu"/>.
        /// </summary>
        public MenuWithHandler[] ContextMenu
        {
            get
            {
                return doubleProperty.ContextMenu;
            }
        }
        /// <summary>
        /// True when the associated property is read-only; the hotspot is then not displayed.
        /// </summary>
        public bool Hidden { get { return doubleProperty.ReadOnly; } }
        #endregion
    }

    /// <summary>
    /// Property entry for the control center that displays and edits a plain floating point number without a unit and without a
    /// context menu, e.g. a scale factor, a single coordinate (the x, y and z sub entries of <see cref="GeoPointProperty"/> and
    /// <see cref="GeoVectorProperty"/>) or a setting value. The value is connected to its owner via the base class mechanisms of
    /// <see cref="EditableProperty{T}"/> (delegates, reflection or <see cref="EditableProperty{T}.OnGetValue"/> and
    /// <see cref="EditableProperty{T}.OnSetValue"/>); created with a setting name it holds the value itself and is saved as part of the
    /// <see cref="Settings"/>, which is why the class is serializable. The number of decimal digits comes from the
    /// "Formatting.GeneralDouble" setting and can be changed with <see cref="DecimalDigits"/>. For a length with the length context
    /// menu use <see cref="LengthProperty"/>, for an angle <see cref="AngleProperty"/>.
    /// </summary>
    [Serializable()]
    public class DoubleProperty : EditableProperty<double>, IJsonSerialize, ISerializable
    {
        private NumberFormatInfo numberFormatInfo;
        private string settingName = "";

        /// <summary>
        /// Creates a double property entry that is not yet connected to a value. Set <see cref="EditableProperty{T}.OnGetValue"/> and
        /// <see cref="EditableProperty{T}.OnSetValue"/> before the entry is used.
        /// </summary>
        /// <param name="frame">The frame whose formatting settings (digits, decimal separator) are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public DoubleProperty(IFrame frame, string resourceId = null) : base(resourceId, null)
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates a double property entry that reads and writes the value through the given delegates.
        /// </summary>
        /// <param name="getValueDelegate">Returns the current value from the object that owns it</param>
        /// <param name="setValueDelegate">Transfers a new value to the owning object</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public DoubleProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, IFrame frame, string resourceId = null) : base(getValueDelegate, setValueDelegate, resourceId, null)
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates a double property entry that reads and writes the value through reflection on a property of the owning object.
        /// </summary>
        /// <param name="ObjectWithLength">The object that owns the value, e.g. a dimension style or a drive</param>
        /// <param name="PropertyName">Name of a (public or non-public) instance property of type <see cref="System.Double"/> on <paramref name="ObjectWithLength"/></param>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="autoModifyWithMouse">Not evaluated by this constructor; kept for compatibility with older code</param>
        public DoubleProperty(object ObjectWithLength, string PropertyName, string resourceId, IFrame frame, bool autoModifyWithMouse = true)
            : base(ObjectWithLength, PropertyName, resourceId, null)
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates a property entry that holds its value itself (no connected object), as used for the values of the <see cref="Settings"/>
        /// (e.g. "Approximate.Precision" or "Grid.XDistance").
        /// </summary>
        /// <param name="SettingName">Name of the setting; currently not stored by this constructor</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="initialValue">The initial value of the entry</param>
        /// <param name="frame">The frame whose formatting settings are used for the display, or null: the value is then displayed with
        /// all significant digits and the decimal separator of the current culture</param>
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

        /// <summary>
        /// Sets the value: transfers it to the owning object (through the set delegate or the reflected property) and refreshes the
        /// display. Used by the owner and by actions that construct the value.
        /// </summary>
        /// <param name="l">The new value</param>
        public void SetDouble(double l)
        {
            SetValue(l, true);
        }
        /// <summary>
        /// Returns the current value as read from the owning object (or the value held by this entry when no object is connected).
        /// </summary>
        /// <returns>The current value</returns>
        public double GetDouble()
        {
            return GetValue();
        }
        /// <summary>
        /// Must be called by the owner when the value has been changed from outside (not through this entry), so that the displayed
        /// value is refreshed. Has no effect while the entry is not on a property page.
        /// </summary>
        public void DoubleChanged()
        {
            propertyPage?.Refresh(this);
        }

        /// <summary>
        /// The number of decimal digits used to display the value, initialized from the "Formatting.GeneralDouble" setting of the frame (3
        /// when there is no frame). The special value 29 displays the value with all significant digits instead of a fixed number of
        /// decimals. Changing it does not refresh the display, call <see cref="DoubleChanged"/> for that.
        /// </summary>
        public int DecimalDigits { get => numberFormatInfo.NumberDecimalDigits; set => numberFormatInfo.NumberDecimalDigits = value; }

        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <summary>
        /// Obsolete: creates the entry like <see cref="DoubleProperty(IFrame, string)"/>, only the parameters are swapped.
        /// </summary>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        [Obsolete("Parameter autoModifyWithMouse no longer supported, use DoubleProperty(IFrame frame, string resourceId) instead")]
        public DoubleProperty(string resourceId, IFrame frame) : this(frame, resourceId)
        {
        }
        /// <summary>
        /// Obsolete: reads the value like <see cref="GetDouble"/> and sets it like <see cref="SetDouble(double)"/>.
        /// </summary>
        [Obsolete("Use DoubleProperty.GetDouble or DoubleProperty.SetDouble instead")]
        public double DoubleValue
        {
            get { return GetDouble(); }
            set
            {
                SetDouble(value);
            }
        }
        /// <summary>
        /// Obsolete handler signature of <see cref="SetDoubleEvent"/>; use <see cref="EditableProperty{T}.SetValueDelegate"/> instead.
        /// </summary>
        /// <param name="sender">The property entry whose value is set</param>
        /// <param name="l">The new value</param>
        [Obsolete("use DoubleProperty.SetValueDelegate instead")]
        public delegate void SetDoubleDelegate(DoubleProperty sender, double l);
        /// <summary>
        /// Obsolete handler signature of <see cref="GetDoubleEvent"/>; use <see cref="EditableProperty{T}.GetValueDelegate"/> instead.
        /// </summary>
        /// <param name="sender">The property entry asking for the value</param>
        /// <returns>The current value</returns>
        [Obsolete("use DoubleProperty.GetValueDelegate instead")]
        public delegate double GetDoubleDelegate(DoubleProperty sender);
        /// <summary>
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.OnSetValue"/>, so it is called whenever a new value is
        /// to be transferred to the owner. Only one handler is kept (adding replaces the previous one), removing clears the delegate.
        /// </summary>
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
        /// <summary>
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.OnGetValue"/>, so it is asked for the current value
        /// whenever the entry needs it. Only one handler is kept (adding replaces the previous one), removing clears the delegate.
        /// </summary>
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
        /// <summary>
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.ModifyWithMouse"/>. The handler is called with true
        /// when the user starts dragging the hotspot of this entry and with false when the "modify with mouse" command of the context menu
        /// is selected. Only one handler is kept, removing clears the delegate.
        /// </summary>
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
        /// <summary>
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.LockedChanged"/>, which is called with the new state
        /// whenever <see cref="EditableProperty{T}.Locked"/> changes. Only one handler is kept, removing clears the delegate.
        /// </summary>
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
        /// <summary>
        /// Obsolete and without functionality; the call can be removed.
        /// </summary>
        /// <param name="Check">Ignored</param>
        [Obsolete("method has no functionality, remove this call")]
        public void CheckMouseButton(bool Check) { }
		/// <summary>
		/// Obsolete handler signature of <see cref="LabelChangedEvent"/>; hides <see cref="EditableProperty{T}.LabelChangedDelegate"/>.
		/// </summary>
		/// <param name="sender">The property entry whose label changed</param>
		/// <param name="newLabel">The new label text</param>
        [Obsolete("use delegate DoubleProperty.LabelTextChanged instead")]
		public new delegate void LabelChangedDelegate(DoubleProperty sender, string newLabel);
        /// <summary>
        /// Obsolete and never raised; use <see cref="EditableProperty{T}.LabelTextChanged"/> instead.
        /// </summary>
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
        /// <summary>
        /// Writes the resource id, the value and (when present) the setting name for the JSON serialization (<see cref="IJsonSerialize"/>);
        /// used when this entry is part of the settings.
        /// </summary>
        /// <param name="data">Receives the data</param>
        public virtual void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("ResourceId", resourceIdInternal);
            data.AddProperty("Value", GetValue());
            if (!string.IsNullOrEmpty(settingName)) data.AddProperty("SettingName", settingName);
        }
        /// <summary>
        /// Restores the resource id, the value and the setting name from the JSON serialization (<see cref="IJsonSerialize"/>). The value
        /// is then held by the entry itself, no object is connected.
        /// </summary>
        /// <param name="data">The data to read from</param>
        public virtual void SetObjectData(IJsonReadData data)
        {
            resourceIdInternal = data.GetProperty<string>("ResourceId");
            SetValue(data.GetProperty<double>("Value"), false);
            if (data.HasProperty("SettingName")) settingName = data.GetStringProperty("SettingName");
        }
        #endregion
        #region ISerializable
        /// <summary>
        /// Constructor for the legacy binary deserialization (<see cref="System.Runtime.Serialization.ISerializable"/>): restores the
        /// value, the resource id and the setting name. The value is displayed with all significant digits.
        /// </summary>
        /// <param name="info">The serialized data</param>
        /// <param name="context">The source of the deserialization</param>
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
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">Receives the data</param>
        /// <param name="context">The destination of the serialization</param>
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
