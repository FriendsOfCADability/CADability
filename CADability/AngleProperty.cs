using System;
using System.Globalization;

namespace CADability.UserInterface
{
    /// <summary>
    /// Implements a hotspot <see cref="IHotSpot"/> to manipulate an angle via an angle property
    /// </summary>

    public class AngleHotSpot : IHotSpot, ICommandHandler
    {
        private readonly AngleProperty angleProperty;
        /// <summary>
        /// The position of the hotspot
        /// </summary>
        public GeoPoint Position;
        /// <summary>
        /// Constructs a hotspot to manipulate a length property
        /// </summary>
        /// <param name="angleProperty">the property to be manipulated</param>
        public AngleHotSpot(AngleProperty angleProperty)
        {
            this.angleProperty = angleProperty;
        }
        #region IHotSpot Members
        GeoPoint IHotSpot.GetHotspotPosition()
        {
            return Position;
        }
        bool IHotSpot.IsSelected
        {
            get
            {
                return angleProperty.IsSelected;
            }
            set
            {
                angleProperty.IsSelected = value;
            }
        }
        void IHotSpot.StartDrag(IFrame frame)
        {
            (angleProperty as IConstructProperty).StartModifyWithMouse();
        }
        string IHotSpot.GetInfoText(CADability.UserInterface.InfoLevelMode Level)
        {
            return angleProperty.Label;
        }
        /// <summary>
        /// The resource id of the associated <see cref="AngleProperty"/>, which identifies its label text.
        /// </summary>
        public string ResourceId
        {
            get { return angleProperty.ResourceId; }
        }
        MenuWithHandler[] IHotSpot.ContextMenu
        {
            get
            {
                return angleProperty.ContextMenu;
            }
        }
        bool IHotSpot.Hidden { get { return angleProperty.ReadOnly; } }
        #endregion

        /// <summary>
        /// Forwards the menu command to the associated <see cref="AngleProperty"/>, which processes the commands of its context menu
        /// (modify with mouse, angle from two points, add or subtract 30°, 45°, 90° or 180°).
        /// </summary>
        /// <param name="MenuId">Id of the menu command</param>
        /// <returns>true if the property handled the command, false otherwise</returns>
        public virtual bool OnCommand(string MenuId)
        {
            return ((ICommandHandler)angleProperty).OnCommand(MenuId);
        }

        /// <summary>
        /// Forwards the request for the state of a menu item to the associated <see cref="AngleProperty"/>.
        /// </summary>
        /// <param name="MenuId">Id of the menu command</param>
        /// <param name="CommandState">State object to modify (enabled, checked)</param>
        /// <returns>true if the property handled the request, false otherwise</returns>
        public virtual bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return ((ICommandHandler)angleProperty).OnUpdateCommand(MenuId, CommandState);
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
    }
    /// <summary>
    /// Exception thrown by <see cref="AngleProperty"/>.
    /// </summary>

    public class AnglePropertyException : System.ApplicationException
    {
        internal AnglePropertyException(string msg) : base(msg)
        {
        }
    }

    /// <summary>
    /// Property entry for the control center that displays and edits an <see cref="Angle"/>, e.g. the start angle of an arc or the
    /// rotation of a text. The value is an <see cref="Angle"/> in radians, connected to its owner via the base class mechanisms of
    /// <see cref="EditableProperty{T}"/> (delegates, reflection or <see cref="EditableProperty{T}.OnGetValue"/> and
    /// <see cref="EditableProperty{T}.OnSetValue"/>); it is displayed and entered in degrees with the number of decimals of the
    /// "Formatting.Angle.Digits" setting (see also <see cref="PreferNegativeValues"/>). Its context menu ("MenuId.Angle") offers to
    /// modify the angle with the mouse (<see cref="EditableProperty{T}.ModifyWithMouse"/>), to define it by two points and to add or
    /// subtract 30°, 45°, 90° or 180°. An <see cref="AngleHotSpot"/> shows the entry as a draggable handle in the drawing area.
    /// </summary>
    public class AngleProperty : EditableProperty<Angle>, ICommandHandler
    {
        private NumberFormatInfo numberFormatInfo;

        /// <summary>
        /// Creates an angle property entry that is not yet connected to a value. Set <see cref="EditableProperty{T}.OnGetValue"/> and
        /// <see cref="EditableProperty{T}.OnSetValue"/> before the entry is used.
        /// </summary>
        /// <param name="frame">The frame whose formatting settings (digits, decimal separator) are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public AngleProperty(IFrame frame, string resourceId = null) : base(resourceId, "MenuId.Angle")
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates an angle property entry that reads and writes the value through the given delegates.
        /// </summary>
        /// <param name="getValueDelegate">Returns the current angle from the object that owns it</param>
        /// <param name="setValueDelegate">Transfers a new angle to the owning object</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public AngleProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, IFrame frame, string resourceId = null) : base(getValueDelegate, setValueDelegate, resourceId, "MenuId.Angle")
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates an angle property entry that reads and writes the value through reflection on a property of the owning object.
        /// </summary>
        /// <param name="ObjectWithAngle">The object that owns the value, e.g. an arc or a text</param>
        /// <param name="PropertyName">Name of a (public or non-public) instance property of type <see cref="Angle"/> on <paramref name="ObjectWithAngle"/></param>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="autoModifyWithMouse">Not evaluated by this constructor; kept for compatibility with older code</param>
        public AngleProperty(object ObjectWithAngle, string PropertyName, string resourceId, IFrame frame, bool autoModifyWithMouse = true)
            : base(ObjectWithAngle, PropertyName, resourceId, "MenuId.Angle")
        {
            InitFormat(frame);
        }

        private void InitFormat(IFrame frame)
        {
            numberFormatInfo = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
            int decsym = Settings.GlobalSettings.GetIntValue("Formatting.Decimal", 0); // Systemeinstellung | Punkt | Komma
            if (decsym == 1) numberFormatInfo.NumberDecimalSeparator = ".";
            else if (decsym == 2) numberFormatInfo.NumberDecimalSeparator = ",";
            numberFormatInfo.NumberDecimalDigits = Settings.GlobalSettings.GetIntValue("Formatting.Angle.Digits", 3);
        }

        /// <summary>
        /// When true, angles greater than 180° are displayed as negative angles (the value minus 360°), e.g. 270° is shown as -90°. The
        /// stored value is not changed. Default is false, i.e. the display is in the range from 0° to 360°.
        /// </summary>
        public bool PreferNegativeValues { get; set; }
        /// <summary>
        /// Sets the angle: transfers it to the owning object (through the set delegate or the reflected property) and refreshes the
        /// display. Used by the owner, by the context menu commands and by actions that construct the angle.
        /// </summary>
        /// <param name="l">The new angle in radians</param>
        public void SetAngle(double l)
        {
            SetValue(l, true);
        }
        /// <summary>
        /// Returns the current angle as read from the owning object (or the value held by this entry when no object is connected).
        /// </summary>
        /// <returns>The angle in radians</returns>
        public double GetAngle()
        {
            return GetValue();
        }
        /// <summary>
        /// Must be called by the owner when the angle has been changed from outside (not through this entry), so that the displayed
        /// value is refreshed. Has no effect while the entry is not on a property page.
        /// </summary>
        public void AngleChanged()
        {
            propertyPage?.Refresh(this);
        }

        /// <inheritdoc/>
        protected override bool TextToValue(string text, out Angle val)
        {
            string trimmed = text;
            bool success = false;
            double l = 0.0;
            try
            {
                l = double.Parse(trimmed, numberFormatInfo);
                success = true;
            }
            catch (FormatException)
            {
                if (trimmed == "-") success = true; // allow to start with "-"
            }
            catch (OverflowException) { } // no valid input
            if (!success)
            {
                try
                {
                    //Scripting s = new Scripting();
                    //if (Frame != null)
                    //{
                    //    l = s.GetDouble(Frame.Project.NamedValues, trimmed);
                    //    success = true;
                    //}
                }
                catch //(ScriptingException)
                {
                }
            }
            if (success)
            {
                val = Angle.FromDegree(l);
            }
            else
            {
                val = 0;
            }
            return success;
        }
        /// <inheritdoc/>
        protected override string ValueToText(Angle val)
        {
            double lval = val.Degree;
            if (PreferNegativeValues && lval > 180) lval = lval - 360;
            return lval.ToString("f", numberFormatInfo);
        }
#region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Angle.ModifyWithMouse":
                    if (ModifyWithMouse != null) ModifyWithMouse(this, false);
                    return true;
                case "MenuId.Angle.TwoPoints":
                    Frame.SetAction(new CADability.Actions.ConstructAngleTwoPoints(this));
                    return true;
                case "MenuId.Angle.TangentOfCurve":
                    return false;
                case "MenuId.Angle.Formula":
                    return false;
                case "MenuId.Angle.NameVariable":
                    return false;
                case "MenuId.Angle.FromVariable":
                    return false;
                case "MenuId.Angle.Plus30":
                    SetAngle(GetAngle() + Math.PI / 6.0);
                    return true;
                case "MenuId.Angle.Plus45":
                    SetAngle(GetAngle() + Math.PI / 4.0);
                    return true;
                case "MenuId.Angle.Plus90":
                    SetAngle(GetAngle() + Math.PI / 2.0);
                    return true;
                case "MenuId.Angle.Minus30":
                    SetAngle(GetAngle() - (SweepAngle)(Math.PI / 6.0));
                    return true;
                case "MenuId.Angle.Minus45":
                    SetAngle(GetAngle() - (SweepAngle)(Math.PI / 4.0));
                    return true;
                case "MenuId.Angle.Minus90":
                    SetAngle(GetAngle() - (SweepAngle)(Math.PI / 2.0));
                    return true;
                case "MenuId.Angle.Opposite":
                    SetAngle(GetAngle() + Math.PI);
                    return true;
            }
            return Frame.CommandHandler.OnCommand(MenuId);
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Angle.ModifyWithMouse":
                    CommandState.Enabled = !ReadOnly;
                    //CommandState.Checked = this.IsModifyingWithMouse;
                    return true;
                case "MenuId.Angle.TwoPoints":
                case "MenuId.Angle.TangentOfCurve":
                case "MenuId.Angle.Formula":
                case "MenuId.Angle.FromVariable":
                case "MenuId.Angle.Plus30":
                case "MenuId.Angle.Plus45":
                case "MenuId.Angle.Plus90":
                case "MenuId.Angle.Minus30":
                case "MenuId.Angle.Minus45":
                case "MenuId.Angle.Minus90":
                case "MenuId.Angle.Opposite":
                    CommandState.Enabled = !ReadOnly;
                    return true;
                case "MenuId.Angle.NameVariable":
                    return false;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

        #region deprecated adaption to old implementation of AngleProperty
        /// <summary>
        /// Obsolete: creates the entry like <see cref="AngleProperty(IFrame, string)"/>; <paramref name="autoModifyWithMouse"/> is ignored.
        /// </summary>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="autoModifyWithMouse">Ignored</param>
        [Obsolete("Parameter autoModifyWithMouse no longer supported, use AngleProperty(IFrame frame, string resourceId) instead")]
        public AngleProperty(string resourceId, IFrame frame, bool autoModifyWithMouse) : this(frame, resourceId)
        {
        }
        /// <summary>
        /// Obsolete handler signature of <see cref="SetAngleEvent"/>; use <see cref="EditableProperty{T}.SetValueDelegate"/> instead.
        /// </summary>
        /// <param name="angle">The new angle</param>
        [Obsolete("use AngleProperty.SetValueDelegate instead")]
        public delegate void SetAngleDelegate(Angle angle);
        /// <summary>
        /// Obsolete handler signature of <see cref="GetAngleEvent"/>; use <see cref="EditableProperty{T}.GetValueDelegate"/> instead.
        /// </summary>
        /// <returns>The current angle</returns>
        [Obsolete("use AngleProperty.GetValueDelegate instead")]
        public delegate Angle GetAngleDelegate();
        /// <summary>
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.OnSetValue"/>, so it is called whenever a new value is
        /// to be transferred to the owner. Only one handler is kept (adding replaces the previous one), removing clears the delegate.
        /// </summary>
        [Obsolete("use delegate AngleProperty.OnSetValue instead")]
        public event SetAngleDelegate SetAngleEvent
        {
            add
            {
                base.OnSetValue = delegate (Angle l) { value(l); };
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
        [Obsolete("use delegate AngleProperty.OnGetValue instead")]
        public event GetAngleDelegate GetAngleEvent
        {
            add
            {
                base.OnGetValue = delegate ()
                {
                    return value();
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
        [Obsolete("use delegate AngleProperty.ModifyWithMouse instead")]
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
        [Obsolete("use delegate AngleProperty.LockedChanged instead")]
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
		/// Note that the sender is declared as <see cref="LengthProperty"/>.
		/// </summary>
		/// <param name="sender">The property entry whose label changed</param>
		/// <param name="newLabel">The new label text</param>
        [Obsolete("use delegate AngleProperty.LabelTextChanged instead")]
		public new delegate void LabelChangedDelegate(LengthProperty sender, string newLabel);
		
        /// <summary>
        /// Obsolete and never raised; use <see cref="EditableProperty{T}.LabelTextChanged"/> instead.
        /// </summary>
        [Obsolete("use delegate AngleProperty.LabelTextChanged instead")]
#pragma warning disable CS0067 // Event is never used
        public event LabelChangedDelegate LabelChangedEvent;
#pragma warning restore CS0067 // Event is never used
        #endregion

    }
}
