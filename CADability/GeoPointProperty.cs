using CADability.Actions;
using System;
using System.Globalization;
using Action = CADability.Actions.Action;

namespace CADability.UserInterface
{

    /// <summary>
    /// A simple hotspot (<see cref="IHotSpot"/>) at a fixed position that is not bound to a property entry: it has no tooltip and no
    /// context menu and is never hidden. Dragging it raises <see cref="StartDragHotspotEvent"/>, so the owner can start an action that
    /// modifies its object, e.g. the handle at the corner of a rectangle that changes its size. The owner creates the hotspot with its
    /// position and updates <see cref="Position"/> when the object changes. A <see cref="GeoPointProperty"/> is itself a hotspot, so
    /// this class is only needed where there is no point property behind the handle.
    /// </summary>
    public class GeoPointHotSpot : IHotSpot
    {
        private GeoPoint position;
        private bool isSelected;
        /// <summary>
        /// Handler signature of <see cref="StartDragHotspotEvent"/>.
        /// </summary>
        /// <param name="sender">The hotspot that is being dragged</param>
        public delegate void StartDragHotspotDelegate(GeoPointHotSpot sender);
        /// <summary>
        /// Raised when the user starts dragging the hotspot (left mouse button pressed on the hotspot and the mouse moved away). The
        /// handler typically starts an action that follows the mouse and modifies the object.
        /// </summary>
        public event StartDragHotspotDelegate StartDragHotspotEvent;
        /// <summary>
        /// Creates a hotspot at the given position; it is initially not selected.
        /// </summary>
        /// <param name="position">The position of the hotspot in world coordinates</param>
        public GeoPointHotSpot(GeoPoint position)
        {
            this.position = position;
            isSelected = false;
        }
        /// <summary>
        /// The position of the hotspot in world coordinates. The owner updates it when the object changes; setting it only stores the
        /// value, it does not repaint the hotspot.
        /// </summary>
        public GeoPoint Position
        {
            get
            {
                return position;
            }
            set
            {
                position = value;
            }
        }

        #region IHotSpot Members

        /// <inheritdoc/>
        public virtual GeoPoint GetHotspotPosition()
        {
            return position;
        }

        bool IHotSpot.IsSelected
        {
            get
            {
                return isSelected;
            }
            set
            {
                isSelected = value;
            }
        }

        void IHotSpot.StartDrag(IFrame frame)
        {
            if (StartDragHotspotEvent != null) StartDragHotspotEvent(this);
        }

        string IHotSpot.GetInfoText(InfoLevelMode Level)
        {
            return null;
        }
        /// <summary>
        /// Always null: this hotspot has no resource id and no label text.
        /// </summary>
        public string ResourceId
        {
            get { return null; }
        }

        MenuWithHandler[] IHotSpot.ContextMenu
        {
            get
            {
                return null;
            }
        }
        bool IHotSpot.Hidden
        {
            get { return false; }
        }

        #endregion
    }

    /// <summary>
    /// Property entry for the control center that displays and edits a <see cref="GeoPoint"/>, e.g. the start point of a line or the
    /// center of a circle. The point is connected to its owner via the base class mechanisms of <see cref="EditableProperty{T}"/>: a
    /// get/set delegate pair, reflection on a named property of the owning object, or <see cref="EditableProperty{T}.OnGetValue"/> and
    /// <see cref="EditableProperty{T}.OnSetValue"/>. The value is always a point in the global coordinate system. It is displayed as
    /// "x y z" (or "x y" when <see cref="DisplayZComponent"/> is false), in the drawing plane of the active view when
    /// <see cref="UseLocalSystem"/> is set, and can be expanded to sub entries for the single coordinates. The entry is itself an
    /// <see cref="IHotSpot"/>, so a selected object can show it as a draggable handle in the drawing area. Its context menu
    /// ("MenuId.Point") offers to modify the point with the mouse and to construct it as a midpoint, a point on an object, an
    /// intersection point, a polar point or a point offset by a vector; <see cref="FilterCommandEvent"/> lets the owner intercept these
    /// commands. Number format and digits are taken from the formatting settings of the <see cref="IFrame"/>.
    /// </summary>
    public class GeoPointProperty : EditableProperty<GeoPoint>, IUserData, ICommandHandler, IHotSpot
    {
        private NumberFormatInfo numberFormatInfo;
        private GeneralGeoPointAction generalGeoPointAction;
        private bool autoModifyWithMouse;

        /// <summary>
        /// Delegate definition for the <see cref="SelectionChangedEvent"/>
        /// </summary>
        /// <param name="sender">this object</param>
        /// <param name="isSelected">true if now selected, false otherwise</param>
        public delegate void SelectionChangedDelegate(GeoPointProperty sender, bool isSelected);
        /// <summary>
        /// This <see cref="SelectionChangedDelegate"/> event will be raised when this GeoPointProperty
        /// gets selected (the user clicks on the label or forwards the focus by pressing the tab key) or unselected.
        /// </summary>
        public event SelectionChangedDelegate SelectionChangedEvent;
        private IPropertyEntry[] subItems;
        /// <summary>
        /// Creates a point property entry that is not yet connected to a value. Set <see cref="EditableProperty{T}.OnGetValue"/> and
        /// <see cref="EditableProperty{T}.OnSetValue"/> before the entry is used.
        /// </summary>
        /// <param name="frame">The frame whose formatting settings (digits, decimal separator) are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public GeoPointProperty(IFrame frame, string resourceId = null) : base(resourceId, "MenuId.Point")
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates a point property entry that reads and writes the value through the given delegates.
        /// </summary>
        /// <param name="getValueDelegate">Returns the current point (in global coordinates) from the object that owns it</param>
        /// <param name="setValueDelegate">Transfers a new point (in global coordinates) to the owning object</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public GeoPointProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, IFrame frame, string resourceId = null) : base(getValueDelegate, setValueDelegate, resourceId, "MenuId.Point")
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates a point property entry that reads and writes the value through reflection on a property of the owning object.
        /// </summary>
        /// <param name="ObjectWithGeoPoint">The object that owns the value, e.g. a line or a block reference</param>
        /// <param name="PropertyName">Name of a (public or non-public) instance property of type <see cref="GeoPoint"/> on <paramref name="ObjectWithGeoPoint"/></param>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="autoModifyWithMouse">Not evaluated by this constructor; kept for compatibility with older code</param>
        public GeoPointProperty(object ObjectWithGeoPoint, string PropertyName, string resourceId, IFrame frame, bool autoModifyWithMouse = true)
            : base(ObjectWithGeoPoint, PropertyName, resourceId, "MenuId.Point")
        {
            InitFormat(frame);
        }

        private void InitFormat(IFrame frame)
        {
            Frame = frame;
            numberFormatInfo = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
            int decsym = Settings.GlobalSettings.GetIntValue("Formatting.Decimal", 0); // Systemeinstellung | Punkt | Komma
            if (decsym == 1) numberFormatInfo.NumberDecimalSeparator = ".";
            else if (decsym == 2) numberFormatInfo.NumberDecimalSeparator = ",";
            numberFormatInfo.NumberDecimalDigits = frame.GetIntSetting("Formatting.Coordinate.Digits", 3);
        }

        /// <summary>
        /// When true (the default), the point is displayed as "x y z", otherwise as "x y" only (unless <see cref="AlwaysZComponent"/> is
        /// set). Entered text may always consist of one, two or three numbers, missing coordinates are 0. Set to false e.g. by
        /// <see cref="MultiGeoPointProperty"/> for the vertices of planar objects.
        /// </summary>
        public bool DisplayZComponent { get; set; } = true;
        /// <summary>
        /// When true, the z coordinate is displayed even if <see cref="DisplayZComponent"/> is false.
        /// </summary>
        public bool AlwaysZComponent { get; set; }
        /// <summary>
        /// When true, the coordinates are displayed and entered in the local system of the drawing plane of the active view
        /// (<see cref="Projection.DrawingPlane"/>); the value of the property itself remains a point in the global system. When false
        /// (the default), global coordinates are shown.
        /// </summary>
        public bool UseLocalSystem { get; set; }

        /// <summary>
        /// The x component of the value in the coordinate system used for the display (see <see cref="UseLocalSystem"/>). The "X" sub entry reads and writes it by reflection.
        /// Setting it transfers the modified value to the owning object and records the component in <see cref="InputFromSubEntries"/>.
        /// </summary>
        protected double X
        {
            get
            {
                return GlobalToLocal(GetValue()).x;
            }
            set
            {
                GeoPoint p = GlobalToLocal(GetValue());
                p.x = value;
                InputFromSubEntries |= EInputFromSubEntries.x;
                SetValue(LocalToGlobal(p), true);
            }
        }
        /// <summary>
        /// The y component of the value in the coordinate system used for the display (see <see cref="UseLocalSystem"/>). The "Y" sub entry reads and writes it by reflection.
        /// Setting it transfers the modified value to the owning object and records the component in <see cref="InputFromSubEntries"/>.
        /// </summary>
        protected double Y
        {
            get
            {
                return GlobalToLocal(GetValue()).y;
            }
            set
            {
                GeoPoint p = GlobalToLocal(GetValue());
                p.y = value;
                InputFromSubEntries |= EInputFromSubEntries.y;
                SetValue(LocalToGlobal(p), true);
            }
        }
        /// <summary>
        /// The z component of the value in the coordinate system used for the display (see <see cref="UseLocalSystem"/>). The "Z" sub entry reads and writes it by reflection.
        /// Setting it transfers the modified value to the owning object and records the component in <see cref="InputFromSubEntries"/>.
        /// </summary>
        protected double Z
        {
            get
            {
                return GlobalToLocal(GetValue()).z;
            }
            set
            {
                GeoPoint p = GlobalToLocal(GetValue());
                p.z = value;
                InputFromSubEntries |= EInputFromSubEntries.z;
                SetValue(LocalToGlobal(p), true);
            }
        }
        /// <summary>
        /// Sets the point: transfers it to the owning object (through the set delegate or the reflected property) and refreshes the
        /// display. Used by the owner and by actions that construct the value.
        /// </summary>
        /// <param name="l">The new point in the global coordinate system</param>
        public void SetGeoPoint(GeoPoint l)
        {
            SetValue(l, true);
        }
        /// <summary>
        /// Returns the current point as read from the owning object (or the value held by this entry when no object is connected).
        /// </summary>
        /// <returns>The point in the global coordinate system</returns>
        public GeoPoint GetGeoPoint()
        {
            return GetValue();
        }
        /// <summary>
        /// Must be called by the owner when the point has been changed from outside (not through this entry), so that the displayed
        /// value is refreshed. Has no effect while the entry is not on a property page.
        /// </summary>
        public void GeoPointChanged()
        {
            propertyPage?.Refresh(this);
        }

        private GeoPoint LocalToGlobal(GeoPoint p)
        {
            if (UseLocalSystem)
            {
                Plane drawingPlane = Frame.ActiveView.Projection.DrawingPlane;
                return drawingPlane.ToGlobal(p);
            }
            return p;
        }
        private GeoPoint GlobalToLocal(GeoPoint p)
        {
            if (UseLocalSystem)
            {
                Plane drawingPlane = Frame.ActiveView.Projection.DrawingPlane;
                return drawingPlane.ToLocal(p);
            }
            return p;
        }
        /// <inheritdoc/>
        protected override bool TextToValue(string text, out GeoPoint val)
        {
            string trimmed = text;
            char[] WhiteSpace = new char[] { (char)0x9, (char)0xA, (char)0xB, (char)0xC, (char)0xD, (char)0x20, (char)0xA0, (char)0x2000, (char)0x2001, (char)0x2002, (char)0x2003, (char)0x2004, (char)0x2005, (char)0x2006, (char)0x2007, (char)0x2008, (char)0x2009, (char)0x200A, (char)0x200B, (char)0x3000, (char)0xFEFF };
            string[] Parts = trimmed.Split(WhiteSpace);
            GeoPoint p = new GeoPoint(0.0, 0.0, 0.0);
            bool success = false;
            if (Parts.Length >= 3)
            {
                try
                {
                    p.x = double.Parse(Parts[0], numberFormatInfo);
                    p.y = double.Parse(Parts[1], numberFormatInfo);
                    p.z = double.Parse(Parts[2], numberFormatInfo);
                    success = true;
                }
                catch (System.FormatException)
                {
                    if (Parts[0] == "-" || Parts[1] == "-" || Parts[2] == "-") success = true; // allow to start with "-", some p components will be 0
                }
            }
            else if (Parts.Length == 2)
            {
                try
                {
                    p.x = double.Parse(Parts[0], numberFormatInfo);
                    p.y = double.Parse(Parts[1], numberFormatInfo);
                    p.z = 0.0;
                    success = true;
                }
                catch (System.FormatException)
                {
                    if (Parts[0] == "-" || Parts[1] == "-") success = true; // allow to start with "-"
                }
            }
            else if (Parts.Length == 1)
            {
                try
                {
                    p.x = double.Parse(Parts[0], numberFormatInfo);
                    p.y = 0.0;
                    p.z = 0.0;
                    success = true;
                }
                catch (System.FormatException)
                {
                    if (Parts[0] == "-") success = true; // allow to start with "-", p.x will stay 0
                }
            }
            if (success)
            {   // the text value might be in a local coordinate system, the value itself is always in the global system
                val = LocalToGlobal(p);
                return true;
            }
            else
            {
                try
                {
                    //Scripting s = new Scripting();
                    //val = s.GetGeoPoint(Frame.Project.NamedValues, trimmed); // always global
                    //return true;
                }
                catch //(ScriptingException)
                {
                }
            }
            val = GeoPoint.Invalid;
            return false;
        }
        /// <inheritdoc/>
        protected override string ValueToText(GeoPoint p)
        {
            p = GlobalToLocal(p);
            if (DisplayZComponent || AlwaysZComponent)
            {
                return p.x.ToString("f", numberFormatInfo) + " " + p.y.ToString("f", numberFormatInfo) + " " + p.z.ToString("f", numberFormatInfo);
            }
            else
            {
                return p.x.ToString("f", numberFormatInfo) + " " + p.y.ToString("f", numberFormatInfo);
            }

        }
        /// <inheritdoc/>
        public override void Selected(IPropertyEntry previousSelected)
        {
            if (previousSelected is GeoPointProperty gp) SelectionChangedEvent?.Invoke(gp, false);
            SelectionChangedEvent?.Invoke(this, true);
            base.Selected(previousSelected);
        }
        /// <inheritdoc/>
        public override PropertyEntryType Flags => base.Flags | PropertyEntryType.HasSubEntries;
        /// <inheritdoc/>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subItems == null)
                {
                    subItems = new IPropertyEntry[3];
                    subItems[0] = new DoubleProperty(this, "X", "GeoPoint.XValue", Frame)
                    {
                        DecimalDigits = Frame.GetIntSetting("Formatting.Coordinate.ComponentsDigits", 3),
                    };
                    subItems[1] = new DoubleProperty(this, "Y", "GeoPoint.YValue", Frame)
                    {
                        DecimalDigits = Frame.GetIntSetting("Formatting.Coordinate.ComponentsDigits", 3),
                    };
                    subItems[2] = new DoubleProperty(this, "Z", "GeoPoint.ZValue", Frame)
                    {
                        DecimalDigits = Frame.GetIntSetting("Formatting.Coordinate.ComponentsDigits", 3),
                    };
                }
                return subItems;
            }
        }
       
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            if (FilterCommandEvent != null)
            {
                bool handled = false;
                FilterCommandEvent(this, MenuId, null, ref handled);
                if (handled) return true;
            }
            // maybe this property is a subproperty (e.g. Multipoint) then use the parent commandhandler first
            ICommandHandler ch = propertyPage.GetParent(this) as ICommandHandler;
            if (ch != null)
            {
                if (ch.OnCommand(MenuId)) return true;
            }
            switch (MenuId)
            {
                case "MenuId.Point.ModifyWithMouse":
                    if (ModifyWithMouse != null) ModifyWithMouse(this, false);
                    else if (autoModifyWithMouse)
                    {
                        generalGeoPointAction = new GeneralGeoPointAction(this);
                        Frame.SetAction(generalGeoPointAction);
                    }
                    return true;
                case "MenuId.Point.IntermediatePoint":
                    Frame.SetAction(new ConstructMidPoint(this));
                    return true;
                case "MenuId.Point.ObjectPoint":
                    Frame.SetAction(new ConstructObjectPoint(this));
                    return true;
                case "MenuId.Point.IntersectionTwoCurves":
                    Frame.SetAction(new ConstructIntersectPoint(this));
                    return true;
                case "MenuId.Point.OffsetByVector":
                    Frame.SetAction(new ConstructVectorPoint(this));
                    return true;
                case "MenuId.Point.Polar":
                    Frame.SetAction(new ConstructPolarPoint(this));
                    return true;
                case "MenuId.Point.NameVariable":
                    Frame.Project.SetNamedValue(null, GetGeoPoint());
                    return true;
                case "MenuId.Point.FormatSettings":
                    {
                        Frame.ShowPropertyDisplay("Global");
                        IPropertyPage pd = Frame.GetPropertyDisplay("Global");
                        IPropertyEntry sp = pd.FindFromHelpLink("Setting.Formatting");
                        if (sp != null)
                        {
                            pd.OpenSubEntries(sp, true);
                            sp = pd.FindFromHelpLink("Setting.Formatting.Coordinate");
                            if (sp != null)
                            {
                                pd.OpenSubEntries(sp, true);
                                pd.SelectEntry(sp);
                            }
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            if (FilterCommandEvent != null)
            {
                bool handled = false;
                FilterCommandEvent(this, MenuId, CommandState, ref handled);
                if (handled) return true;
            }
            // maybe this property is a subproperty (e.g. Multipoint) then use the parent commandhandler first
            ICommandHandler ch = propertyPage.GetParent(this) as ICommandHandler;
            if (ch != null)
            {
                if (ch.OnUpdateCommand(MenuId, CommandState)) return true;
            }
            switch (MenuId)
            {
                case "MenuId.Point.ModifyWithMouse":
                    //CommandState.Checked = isModifyingWithMouse;
                    CommandState.Enabled = (!ReadOnly) && ((ModifyWithMouse != null) || autoModifyWithMouse);
                    return true;
                case "MenuId.Point.IntermediatePoint":
                case "MenuId.Point.ObjectPoint":
                case "MenuId.Point.IntersectionTwoCurves":
                case "MenuId.Point.OffsetByVector":
                case "MenuId.Point.Polar":
                    CommandState.Enabled = !ReadOnly;
                    return true;
                case "MenuId.Point.NameVariable":
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

        #region deprecated adaption to old implementation of GeoPointProperty
        /// <summary>
        /// Obsolete: creates the entry like <see cref="GeoPointProperty(IFrame, string)"/>. When <paramref name="autoModifyWithMouse"/> is
        /// true and no <see cref="EditableProperty{T}.ModifyWithMouse"/> delegate is set, the context menu command "modify with mouse"
        /// starts a <see cref="GeneralGeoPointAction"/> on this entry.
        /// </summary>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="autoModifyWithMouse">true to let the context menu start a <see cref="GeneralGeoPointAction"/> when no delegate is set</param>
        [Obsolete("Parameter autoModifyWithMouse no longer supported, use GeoPointProperty(IFrame frame, string resourceId) instead")]
        public GeoPointProperty(string resourceId, IFrame frame, bool autoModifyWithMouse) : this(frame, resourceId)
        {
            this.autoModifyWithMouse = autoModifyWithMouse;
        }
        /// <summary>
        /// Obsolete handler signature of <see cref="SetGeoPointEvent"/>; use <see cref="EditableProperty{T}.SetValueDelegate"/> instead.
        /// </summary>
        /// <param name="sender">The property entry whose value is set</param>
        /// <param name="p">The new point</param>
        [Obsolete("use GeoPointProperty.SetValueDelegate instead")]
        public delegate void SetGeoPointDelegate(GeoPointProperty sender, GeoPoint p);
        /// <summary>
        /// Obsolete handler signature of <see cref="GetGeoPointEvent"/>; use <see cref="EditableProperty{T}.GetValueDelegate"/> instead.
        /// </summary>
        /// <param name="sender">The property entry asking for the value</param>
        /// <returns>The current point</returns>
        [Obsolete("use GeoPointProperty.GetValueDelegate instead")]
        public delegate GeoPoint GetGeoPointDelegate(GeoPointProperty sender);
        /// <summary>
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.OnSetValue"/>, so it is called whenever a new value is
        /// to be transferred to the owner. Only one handler is kept (adding replaces the previous one), removing clears the delegate.
        /// </summary>
        [Obsolete("use delegate GeoPointProperty.OnSetValue instead")]
        public event SetGeoPointDelegate SetGeoPointEvent
        {
            add
            {
                base.OnSetValue = delegate (GeoPoint l) { value(this, l); };
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
        [Obsolete("use delegate GeoPointProperty.OnGetValue instead")]
        public event GetGeoPointDelegate GetGeoPointEvent
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
        [Obsolete("use delegate GeoPointProperty.ModifyWithMouse instead")]
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
        [Obsolete("use delegate GeoPointProperty.LockedChanged instead")]
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
        /// Obsolete and without functionality.
        /// </summary>
        [Obsolete("has no funtionality")]
        public bool TabIsSpecialKeyEvent { get; internal set; }
        /// <summary>
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.LabelTextChanged"/>, which is called when the user has
        /// edited the label text of the entry. Only one handler is kept, removing clears the delegate.
        /// </summary>
        [Obsolete("use delegate GeoPointProperty.LabelTextChanged instead")]
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
        /// <summary>
        /// Obsolete and without functionality.
        /// </summary>
        [Obsolete("has no funtionality")]
        public bool IsModifyingWithMouse { get; internal set; }
        #endregion

        #region IHotSpot implementation
        bool IHotSpot.IsSelected { get => propertyPage.Selected == this; set => propertyPage.SelectEntry(this); }
        MenuWithHandler[] IHotSpot.ContextMenu => ContextMenu; // ?? not sure
        bool IHotSpot.Hidden => false;
        GeoPoint IHotSpot.GetHotspotPosition()
        {
            return GetValue();
        }
        void IHotSpot.StartDrag(IFrame frame)
        {
            if (!ReadOnly)
            {
                (this as IConstructProperty).StartModifyWithMouse();
            }
        }
        string IHotSpot.GetInfoText(InfoLevelMode Level)
        {
            return Label;
        }
        #endregion

        /// <summary>
        /// Flags that record which coordinates of the point have been entered through the x, y and z sub entries; see
        /// <see cref="InputFromSubEntries"/>.
        /// </summary>
        public enum EInputFromSubEntries { x = 0x1, y = 0x2, z = 0x4 }
        /// <summary>
        /// Records which coordinates the user has entered through the sub entries (the flags are set by the sub entries and not cleared by
        /// this class). A construct action reads it in its set-value handler: when single coordinates have been typed in, the input is only
        /// partially fixed and the remaining coordinates still follow the mouse; when all three flags are set, or none because the whole
        /// point was typed in, the input counts as fixed.
        /// </summary>
        public EInputFromSubEntries InputFromSubEntries = 0;
        internal GeoPoint GetPartiallyFixed(GeoPoint p)
        {
            return p;
        }
        internal void RefreshPartially()
        {
            propertyPage.Refresh(this);
        }

        /// <summary>
        /// Delegate definition for <see cref="ModifiedByActionEvent"/>
        /// </summary>
        /// <param name="sender">this object</param>
        public delegate void ModifiedByActionDelegate(GeoPointProperty sender);
        /// <summary>
        /// This <see cref="ModifiedByActionDelegate"/> event will be raised when the GeoPoint was modified by
        /// some <see cref="Action"/>. ( The <see cref="SetGeoPointEvent"/> will also be raised or the property will be set)
        /// </summary>
        public event ModifiedByActionDelegate ModifiedByActionEvent;
        internal void ModifiedByAction(Action action)
        {
            if (ModifiedByActionEvent != null) ModifiedByActionEvent(this);
        }
        /// <summary>
        /// Intended to force the display in absolute coordinates regardless of the formatting settings; set by
        /// <see cref="DrawingPlaneProperty"/> for the origin of the drawing plane, but currently not evaluated by this class. Can only be
        /// set from within CADability.
        /// </summary>
        public bool ForceAbsolute { get; internal set; }
        // the following should be removed and the caller should call SetContextMenu with itself as commandhandler
        /// <summary>
        /// The menu id (from the menu resource) of the context menu of this entry, "MenuId.Point" by default. Setting it replaces the
        /// context menu by the menu with the given id and makes this entry the <see cref="ICommandHandler"/> of that menu (e.g. "MenuId.IndexedPoint" for the vertices of a <see cref="MultiGeoPointProperty"/>).
        /// Prefer <see cref="EditableProperty{T}.SetContextMenu(string, ICommandHandler)"/> with an own command handler.
        /// </summary>
        public string ContextMenuId { get => GetContextMenuId(); set => SetContextMenu(value,this); }
        /// <summary>
        /// Delegate definition for <see cref="FilterCommandEvent"/>
        /// </summary>
        /// <param name="sender">this object</param>
        /// <param name="menuId">menu id of the selected menu entry</param>
        /// <param name="commandState">when not null, asks for the state of the menu</param>
        /// <param name="handled">set to true if handled</param>
        public delegate void FilterCommandDelegate(GeoPointProperty sender, string menuId, CommandState commandState, ref bool handled);
        /// <summary>
        /// When a context menue is selected or about to popup this event is raised to allow a
        /// consumer to process the command instead of this GeoPointProperty object itself.
        /// Provide a handler here if you want to process some or all menu commands.
        /// </summary>
        public event FilterCommandDelegate FilterCommandEvent;
    }
}


