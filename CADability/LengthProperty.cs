using System;
using System.Globalization;

namespace CADability.UserInterface
{
    /// <summary>
    /// Implements a hotspot <see cref="IHotSpot"/> to manipulate a length via a length property
    /// </summary>
    public class LengthHotSpot : IHotSpot, ICommandHandler
    {
        private LengthProperty lengthProperty;
        /// <summary>
        /// The position of the hotspot in world coordinates, returned by <see cref="GetHotspotPosition"/>. The owner of the hotspot
        /// keeps it up to date, typically a point at the distance of the length from a reference point, e.g. a point on a circle for its radius.
        /// </summary>
        public GeoPoint Position;
        /// <summary>
        /// Constructs a hotspot to manipulate a length property
        /// </summary>
        /// <param name="lengthProperty">the property to be manipulated</param>
        public LengthHotSpot(LengthProperty lengthProperty)
        {
            this.lengthProperty = lengthProperty;
        }
        #region IHotSpot Members
        /// <inheritdoc/>
        public GeoPoint GetHotspotPosition()
        {
            return Position;
        }
        /// <summary>
        /// The selection state of the hotspot, which is the selection state of the associated <see cref="LengthProperty"/> on its property page:
        /// selecting the hotspot selects the property entry in the control center and vice versa.
        /// </summary>
        public bool IsSelected
        {
            get
            {
                return lengthProperty.IsSelected;
            }
            set
            {
                lengthProperty.IsSelected = value;
            }
        }
        /// <inheritdoc/>
        public void StartDrag(IFrame frame)
        {
            lengthProperty.StartModifyWithMouse();
        }
        /// <summary>
        /// Returns the tooltip text of the hotspot, which is the label of the associated <see cref="LengthProperty"/>.
        /// </summary>
        /// <param name="Level">Requested info level; ignored, the label is returned for every level</param>
        /// <returns>The label text of the associated property</returns>
        public string GetInfoText(CADability.UserInterface.InfoLevelMode Level)
        {
            return lengthProperty.Label;
        }
        /// <summary>
        /// The resource id of the associated <see cref="LengthProperty"/>, which identifies its label text.
        /// </summary>
        public string ResourceId
        {
            get { return lengthProperty.ResourceId; }
        }
        MenuWithHandler[] IHotSpot.ContextMenu
        {
            get
            {
                return lengthProperty.ContextMenu;
            }
        }
        /// <summary>
        /// True when the associated property is read-only; the hotspot is then not displayed.
        /// </summary>
        public bool Hidden { get { return lengthProperty.ReadOnly; } }
        #endregion

        /// <summary>
        /// Forwards the menu command to the associated <see cref="LengthProperty"/>, which processes the commands of its context menu
        /// (modify with mouse, measure a distance, double or halve the value, format settings).
        /// </summary>
        /// <param name="MenuId">Id of the menu command</param>
        /// <returns>true if the property handled the command, false otherwise</returns>
        virtual public bool OnCommand(string MenuId)
        {
            return ((ICommandHandler)lengthProperty).OnCommand(MenuId);
        }

        /// <summary>
        /// Forwards the request for the state of a menu item to the associated <see cref="LengthProperty"/>.
        /// </summary>
        /// <param name="MenuId">Id of the menu command</param>
        /// <param name="CommandState">State object to modify (enabled, checked)</param>
        /// <returns>true if the property handled the request, false otherwise</returns>
        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return ((ICommandHandler)lengthProperty).OnUpdateCommand(MenuId, CommandState);
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
    }


    /// <summary>
    /// Exception type associated with <see cref="LengthProperty"/>. It is currently not thrown by CADability and is kept for
    /// compatibility.
    /// </summary>
    public class LengthPropertyException : System.ApplicationException
    {
        /// <summary>
        /// Creates the exception with the given message.
        /// </summary>
        /// <param name="msg">The message that describes the error</param>
        public LengthPropertyException(string msg)
            : base(msg)
        {
        }
    }

    /// <summary>
    /// Property entry for the control center that displays and edits a length (a double value in the units of the model), e.g. the
    /// radius of a circle, the length of a line or the size of a text. The value is connected to its owner via the base class
    /// mechanisms of <see cref="EditableProperty{T}"/> (delegates, reflection or <see cref="EditableProperty{T}.OnGetValue"/> and
    /// <see cref="EditableProperty{T}.OnSetValue"/>). The number of decimal digits comes from the "Formatting.GeneralDouble" setting.
    /// Its context menu ("MenuId.Length") offers to modify the length with the mouse (<see cref="EditableProperty{T}.ModifyWithMouse"/>),
    /// to measure it as the length of a curve, the distance of two points, of a point and a curve or of two curves, to double or halve
    /// it, to store it as a named value and to open the format settings. A <see cref="LengthHotSpot"/> shows the entry as a draggable
    /// handle in the drawing area.
    /// </summary>
    public class LengthProperty : EditableProperty<double>, ICommandHandler
    {
        private NumberFormatInfo numberFormatInfo;

        /// <summary>
        /// Creates a length property entry that is not yet connected to a value. Set <see cref="EditableProperty{T}.OnGetValue"/> and
        /// <see cref="EditableProperty{T}.OnSetValue"/> before the entry is used.
        /// </summary>
        /// <param name="frame">The frame whose formatting settings (digits, decimal separator) are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public LengthProperty(IFrame frame, string resourceId = null) : base(resourceId, "MenuId.Length")
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates a length property entry that reads and writes the value through the given delegates.
        /// </summary>
        /// <param name="getValueDelegate">Returns the current length from the object that owns it</param>
        /// <param name="setValueDelegate">Transfers a new length to the owning object</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public LengthProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, IFrame frame, string resourceId = null) : base(getValueDelegate, setValueDelegate, resourceId, "MenuId.Length")
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates a length property entry that reads and writes the value through reflection on a property of the owning object.
        /// </summary>
        /// <param name="ObjectWithLength">The object that owns the value, e.g. a circle or a text</param>
        /// <param name="PropertyName">Name of a (public or non-public) instance property of type <see cref="System.Double"/> on <paramref name="ObjectWithLength"/></param>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="autoModifyWithMouse">Not evaluated by this constructor; kept for compatibility with older code</param>
        public LengthProperty(object ObjectWithLength, string PropertyName, string resourceId, IFrame frame, bool autoModifyWithMouse = true)
            : base(ObjectWithLength, PropertyName, resourceId, "MenuId.Length")
        {
            InitFormat(frame);
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
        /// Sets the length: transfers it to the owning object (through the set delegate or the reflected property) and refreshes the
        /// display. Used by the owner and by actions that construct the value.
        /// </summary>
        /// <param name="l">The new length in the units of the model</param>
        public void SetLength(double l)
        {
            SetValue(l, true);
        }
        /// <summary>
        /// Returns the current length as read from the owning object (or the value held by this entry when no object is connected).
        /// </summary>
        /// <returns>The length in the units of the model</returns>
        public double GetLength()
        {
            return GetValue();
        }
        /// <summary>
        /// Must be called by the owner when the length has been changed from outside (not through this entry), so that the displayed
        /// value is refreshed. Has no effect while the entry is not on a property page.
        /// </summary>
        public void LengthChanged()
        {
            propertyPage?.Refresh(this);
        }

        /// <inheritdoc/>
        protected override bool TextToValue(string text, out double val)
        {
            bool success = false;
            if (numberFormatInfo.NumberDecimalSeparator == ".")
                text = text.Replace(",", ".");

            if (numberFormatInfo.NumberDecimalSeparator == ",")
                text = text.Replace(".", ",");

            //Remove duplicate NumberDecimalSeparator from end to start.
            text = StringHelper.RemoveExtraStrings(text, numberFormatInfo.NumberDecimalSeparator);
                
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
            
            return success;
        }
        
        /// <inheritdoc/>
        protected override string ValueToText(double val)
        {
            return val.ToString("f", numberFormatInfo);
        }
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Length.ModifyWithMouse":
                    ModifyWithMouse?.Invoke(this, false);
                    return true;
                case "MenuId.Length.DistanceOfCurve":
                    Frame.SetAction(new CADability.Actions.ConstructDistanceOfCurve(this));
                    return true;
                case "MenuId.Length.DistanceTwoPoints":
                    Frame.SetAction(new CADability.Actions.ConstructDistanceTwoPoints(this));
                    return true;
                case "MenuId.Length.DistancePointCurve":
                    Frame.SetAction(new CADability.Actions.ConstructDistancePointCurve(this));
                    return true;
                case "MenuId.Length.DistanceTwoCurves":
                    Frame.SetAction(new CADability.Actions.ConstructDistanceTwoCurves(this));
                    return true;
                case "MenuId.Length.DoubleValue":
                    this.SetLength(this.GetLength() * 2.0);
                    return true;
                case "MenuId.Length.HalfValue":
                    this.SetLength(this.GetLength() * 0.5);
                    return true;
                case "MenuId.Length.NameVariable":
                    Frame.Project.SetNamedValue(null, GetLength());
                    return true;
                case "MenuId.Length.FormatSettings":
                    {
                        Frame.ShowPropertyDisplay("Global");
                        IPropertyPage pd = Frame.GetPropertyDisplay("Global");
                        IPropertyEntry sp = pd.FindFromHelpLink("Setting.Formatting");
                        if (sp != null)
                        {
                            pd.OpenSubEntries(sp, true);
                            sp = pd.FindFromHelpLink("Setting.Formatting.GeneralDouble");
                            if (sp != null)
                            {
                                pd.OpenSubEntries(sp, true);
                                pd.SelectEntry(sp);
                            }
                        }
                    }
                    return true;
                    // return false;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Length.ModifyWithMouse":
                    CommandState.Enabled = !ReadOnly;
                    //CommandState.Checked = isModifyingWithMouse;
                    return true;
                case "MenuId.Length.DistanceOfCurve":
                case "MenuId.Length.DistanceTwoPoints":
                case "MenuId.Length.DistancePointCurve":
                case "MenuId.Length.DistanceTwoCurves":
                case "MenuId.Length.DoubleValue":
                case "MenuId.Length.HalfValue":
                    CommandState.Enabled = !ReadOnly;
                    return true;
                case "MenuId.Length.NameVariable":
                    return false;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

        #region deprecated adaption to old implementation of LengthProperty
        /// <summary>
        /// Obsolete: creates the entry like <see cref="LengthProperty(IFrame, string)"/>; <paramref name="autoModifyWithMouse"/> is ignored.
        /// </summary>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="autoModifyWithMouse">Ignored</param>
        [Obsolete("Parameter autoModifyWithMouse no longer supported, use LengthProperty(IFrame frame, string resourceId) instead")]
        public LengthProperty(string resourceId, IFrame frame, bool autoModifyWithMouse) : this(frame, resourceId)
        {
        }
        /// <summary>
        /// Obsolete handler signature of <see cref="SetLengthEvent"/>; use <see cref="EditableProperty{T}.SetValueDelegate"/> instead.
        /// </summary>
        /// <param name="sender">The property entry whose value is set</param>
        /// <param name="l">The new length</param>
        [Obsolete("use LengthProperty.SetValueDelegate instead")]
        public delegate void SetLengthDelegate(LengthProperty sender, double l);
        /// <summary>
        /// Obsolete handler signature of <see cref="GetLengthEvent"/>; use <see cref="EditableProperty{T}.GetValueDelegate"/> instead.
        /// </summary>
        /// <param name="sender">The property entry asking for the value</param>
        /// <returns>The current length</returns>
        [Obsolete("use LengthProperty.GetValueDelegate instead")]
        public delegate double GetLengthDelegate(LengthProperty sender);
        /// <summary>
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.OnSetValue"/>, so it is called whenever a new value is
        /// to be transferred to the owner. Only one handler is kept (adding replaces the previous one), removing clears the delegate.
        /// </summary>
        [Obsolete("use delegate LengthProperty.OnSetValue instead")]
        public event SetLengthDelegate SetLengthEvent
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
        [Obsolete("use delegate LengthProperty.OnGetValue instead")]
        public event GetLengthDelegate GetLengthEvent
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
        [Obsolete("use delegate LengthProperty.ModifyWithMouse instead")]
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
        [Obsolete("use delegate LengthProperty.LockedChanged instead")]
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
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.LabelTextChanged"/>, which is called when the user has
        /// edited the label text of the entry. Only one handler is kept, removing clears the delegate.
        /// </summary>
        [Obsolete("use delegate LengthProperty.LabelTextChanged instead")]
        public event LabelChangedDelegate LabelChangedEvent
        {
            add
            {
                LabelTextChanged = value;
            }
            remove
            {
                LabelTextChanged = null;
            }
        }
        #endregion

    }
}
