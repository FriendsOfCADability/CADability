using System;
using System.Globalization;
using Action = CADability.Actions.Action;

namespace CADability.UserInterface
{
    /// <summary>
    /// A hotspot (<see cref="IHotSpot"/>) that stands in for a <see cref="GeoVectorProperty"/> in the drawing area: a draggable handle
    /// at <see cref="Position"/> through which the user can modify the vector with the mouse. Selecting the hotspot selects the property
    /// entry in the control center, dragging it calls <see cref="EditableProperty{T}.StartModifyWithMouse"/> on the property, and its
    /// context menu and menu commands are those of the property. The owner of the property (e.g. the property entry of a text, a
    /// bitmap or an ellipse) creates the hotspot and updates <see cref="Position"/> when the object changes.
    /// </summary>
    public class GeoVectorHotSpot : IHotSpot, ICommandHandler
    {
        private readonly GeoVectorProperty geoVectorProperty;
        /// <summary>
        /// The position of the hotspot in world coordinates, returned by <see cref="GetHotspotPosition"/>. The owner of the hotspot
        /// keeps it up to date, typically the point where the vector is attached to the object.
        /// </summary>
        public GeoPoint Position;
        /// <summary>
        /// Creates a hotspot that manipulates the given vector property.
        /// </summary>
        /// <param name="geoVectorProperty">The property whose vector is modified when the hotspot is dragged</param>
        public GeoVectorHotSpot(GeoVectorProperty geoVectorProperty)
        {
            this.geoVectorProperty = geoVectorProperty;
        }
        #region IHotSpot Members
        /// <inheritdoc/>
        public GeoPoint GetHotspotPosition()
        {
            return Position;
        }
        /// <summary>
        /// The selection state of the hotspot, which is the selection state of the associated <see cref="GeoVectorProperty"/> on its property page:
        /// selecting the hotspot selects the property entry in the control center and vice versa.
        /// </summary>
        public bool IsSelected
        {
            get
            {
                return geoVectorProperty.IsSelected;
            }
            set
            {
                geoVectorProperty.IsSelected = value;
            }
        }
        /// <inheritdoc/>
        public void StartDrag(IFrame frame)
        {
            geoVectorProperty.StartModifyWithMouse();
        }
        /// <summary>
        /// Returns the tooltip text of the hotspot, which is the label of the associated <see cref="GeoVectorProperty"/>.
        /// </summary>
        /// <param name="Level">Requested info level; ignored, the label is returned for every level</param>
        /// <returns>The label text of the associated property</returns>
        public string GetInfoText(CADability.UserInterface.InfoLevelMode Level)
        {
            return geoVectorProperty.Label;
        }
        /// <summary>
        /// The resource id of the associated <see cref="GeoVectorProperty"/>, which identifies its label text.
        /// </summary>
        public string ResourceId
        {
            get { return geoVectorProperty.ResourceId; }
        }
        MenuWithHandler[] IHotSpot.ContextMenu
        {
            get
            {
                return geoVectorProperty.ContextMenu;
            }
        }
        /// <summary>
        /// True when the associated property is read-only; the hotspot is then not displayed.
        /// </summary>
        public bool Hidden { get { return geoVectorProperty.ReadOnly; } }
        #endregion
        /// <summary>
        /// Forwards the menu command to the associated <see cref="GeoVectorProperty"/>, which processes the commands of its context menu
        /// (modify with mouse, direction of a curve or a surface, direction from two points, format settings).
        /// </summary>
        /// <param name="MenuId">Id of the menu command</param>
        /// <returns>true if the property handled the command, false otherwise</returns>
        virtual public bool OnCommand(string MenuId)
        {
            return ((ICommandHandler)geoVectorProperty).OnCommand(MenuId);
        }
                /// <summary>
                /// Forwards the request for the state of a menu item to the associated <see cref="GeoVectorProperty"/>.
                /// </summary>
                /// <param name="MenuId">Id of the menu command</param>
                /// <param name="CommandState">State object to modify (enabled, checked)</param>
                /// <returns>true if the property handled the request, false otherwise</returns>
                virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return ((ICommandHandler)geoVectorProperty).OnUpdateCommand(MenuId, CommandState);
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
    }

    /// <summary>
    /// Property entry for the control center that displays and edits a <see cref="GeoVector"/>, e.g. the direction of a line or the
    /// axis of an ellipse. The vector is connected to its owner via the base class mechanisms of <see cref="EditableProperty{T}"/>: a
    /// get/set delegate pair, reflection on a named property of the owning object, or <see cref="EditableProperty{T}.OnGetValue"/> and
    /// <see cref="EditableProperty{T}.OnSetValue"/>. The value is always a vector in the global coordinate system. It is displayed
    /// either by its x, y and z components (in the drawing plane of the active view when the "Formatting.System" setting selects the
    /// local system) or, when <see cref="IsAngle"/> is set, as an angle in degrees in <see cref="PlaneForAngle"/>. The entry can be
    /// expanded to sub entries for the single components (or for longitude and latitude). Its context menu ("MenuId.Vector") offers to
    /// modify the vector with the mouse, to take the direction of a curve or a surface, to define it by two points, to store it as a
    /// named value and to open the format settings; <see cref="FilterCommandEvent"/> lets the owner take part in these commands.
    /// Number format and digits are taken from the formatting settings of the <see cref="IFrame"/>. A <see cref="GeoVectorHotSpot"/>
    /// shows the entry as a draggable handle in the drawing area.
    /// </summary>
    public class GeoVectorProperty : EditableProperty<GeoVector>, IUserData, ICommandHandler
    {
        private NumberFormatInfo numberFormatInfo;
        private int componentsDigits;
        private enum DisplayMode { ShowAngle, ShowPolar, ShowComponents, ShowComponents2D }; // als Winkel | polar | X-Y-Z-Werte | X-Y-Werte 
        private DisplayMode displayMode;
        private enum AngleMode { Degree, DegreeMinute, DegreeMinuteSecond, Radiants }; // Grad (dezimal)|Grad (Minuten)|Grad (Minuten,Sekunden)|Bogenmaß
        private AngleMode angleMode;
        private int angleDigits;
        private enum DisplayCoordinateSystem { local, absolute, both };
        private DisplayCoordinateSystem displayCoordinateSystem; // welches Koordinatensystem für die Darstellung
        private Plane drawingPlane; // spiegelt die aktuelle drawingplane wieder
        private bool displayZComponent; // true: z-Wert darstellen, false: nur x,y-Werte darstellen
        private bool alwaysAbsoluteCoordinateSystem; // immer im basoluten Koordinatensystem darstellen

        /// <summary>
        /// Delegate definition for the <see cref="SelectionChangedEvent"/>
        /// </summary>
        /// <param name="sender">this object</param>
        /// <param name="isSelected">true if now selected, false otherwise</param>
        public delegate void SelectionChangedDelegate(GeoVectorProperty sender, bool isSelected);
        /// <summary>
        /// This <see cref="SelectionChangedDelegate"/> event will be raised when this GeoVectorProperty
        /// gets selected (the user clicks on the label or forwards the focus by pressing the tab key) or unselected.
        /// </summary>
        public event SelectionChangedDelegate SelectionChangedEvent;
        private IPropertyEntry[] subItems;
        /// <summary>
        /// Creates a vector property entry that is not yet connected to a value. Set <see cref="EditableProperty{T}.OnGetValue"/> and
        /// <see cref="EditableProperty{T}.OnSetValue"/> before the entry is used.
        /// </summary>
        /// <param name="frame">The frame whose formatting settings (coordinate system, digits, decimal separator, vector and angle mode)
        /// are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public GeoVectorProperty(IFrame frame, string resourceId = null) : base(resourceId, "MenuId.Vector")
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates a vector property entry that reads and writes the value through the given delegates.
        /// </summary>
        /// <param name="getValueDelegate">Returns the current vector (in global coordinates) from the object that owns it</param>
        /// <param name="setValueDelegate">Transfers a new vector (in global coordinates) to the owning object</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null</param>
        public GeoVectorProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, IFrame frame, string resourceId = null) : base(getValueDelegate, setValueDelegate, resourceId, "MenuId.Vector")
        {
            InitFormat(frame);
        }

        /// <summary>
        /// Creates a vector property entry that reads and writes the value through reflection on a property of the owning object.
        /// </summary>
        /// <param name="ObjectWithGeoVector">The object that owns the value, e.g. a line or a surface</param>
        /// <param name="PropertyName">Name of a (public or non-public) instance property of type <see cref="GeoVector"/> on <paramref name="ObjectWithGeoVector"/></param>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="autoModifyWithMouse">Not evaluated by this constructor; kept for compatibility with older code</param>
        public GeoVectorProperty(object ObjectWithGeoVector, string PropertyName, string resourceId, IFrame frame, bool autoModifyWithMouse = true)
            : base(ObjectWithGeoVector, PropertyName, resourceId, "MenuId.Vector")
        {
            InitFormat(frame);
        }

        private void InitFormat(IFrame frame)
        {
			if (frame.GetSetting("Formatting.System") is MultipleChoiceSetting formattingSystem && formattingSystem.CurrentSelection >= 0)
			{
				displayCoordinateSystem = (DisplayCoordinateSystem)formattingSystem.CurrentSelection;
			}
			else
			{
				displayCoordinateSystem = DisplayCoordinateSystem.local;
			}
			if (frame.GetSetting("Formatting.Coordinate.ZValue") is MultipleChoiceSetting formattingZValue && formattingZValue.CurrentSelection >= 0)
			{
				displayZComponent = formattingZValue.CurrentSelection == 0;
			}
			else
			{
				displayZComponent = true;
			}
			alwaysAbsoluteCoordinateSystem = false;
            displayMode = (DisplayMode)frame.GetIntSetting("Formatting.Vector.Mode", 0);
            numberFormatInfo = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
            int decsym = Settings.GlobalSettings.GetIntValue("Formatting.Decimal", 0); // Systemeinstellung | Punkt | Komma
            // wenn 0, dann unverändert
            if (decsym == 1) numberFormatInfo.NumberDecimalSeparator = ".";
            else if (decsym == 2) numberFormatInfo.NumberDecimalSeparator = ",";
            numberFormatInfo.NumberDecimalDigits = frame.GetIntSetting("Formatting.Coordinate.Digits", 3);
            componentsDigits = frame.GetIntSetting("Formatting.Coordinate.ComponentsDigits", 3);
            angleMode = (AngleMode)frame.GetIntSetting("Formatting.Angle.Mode", 0);
            angleDigits = frame.GetIntSetting("Formatting.Angle.Digits", 3);
            this.Frame = frame;
        }

        /// <summary>
        /// Counterpart of <see cref="GeoPointProperty.DisplayZComponent"/>. A vector is currently always displayed with all three
        /// components, so this value has no effect on the display.
        /// </summary>
        public bool DisplayZComponent { get; set; } = true;
        /// <summary>
        /// Counterpart of <see cref="GeoPointProperty.AlwaysZComponent"/>; currently without effect, see <see cref="DisplayZComponent"/>.
        /// </summary>
        public bool AlwaysZComponent { get; set; }
        /// <summary>
        /// Counterpart of <see cref="GeoPointProperty.UseLocalSystem"/>. For a vector the coordinate system of the display is chosen by
        /// the "Formatting.System" setting of the frame, this value is currently not evaluated.
        /// </summary>
        public bool UseLocalSystem { get; set; }
        /// <summary>
        /// When true, the vector is a direction that is displayed and entered as an angle in degrees, measured in <see cref="PlaneForAngle"/>
        /// (or in the drawing plane of the active view when no valid plane is set): a single number is the angle in the plane, two numbers
        /// are longitude and latitude. The sub entries then show the angle(s) instead of the x, y and z components. When false (the
        /// default), the vector is displayed by its components. Construct actions set it for direction inputs, e.g. the direction of a line.
        /// Setting the value refreshes the entry on the property page.
        /// </summary>
        public bool IsAngle
        {
            get
            {
                return isAngle;
            }
            set
            {
                isAngle = value;
                propertyPage?.Refresh(this);
            }
        }
        /// <summary>
        /// The plane in which the angle is measured when <see cref="IsAngle"/> is true: the displayed angle is the angle between the x-axis
        /// of the plane and the projection of the vector onto the plane. When the plane is not valid (the default), the drawing plane of
        /// the active view is used. Setting the value refreshes the entry on the property page.
        /// </summary>
        public Plane PlaneForAngle
        {
            get { return planeForAngle; }
            set
            {
                planeForAngle = value;
                propertyPage?.Refresh(this);
            }
        }
        /// <summary>
        /// Marks the vector as a pure direction whose length is irrelevant (e.g. the direction of a line); owners set it together with
        /// <see cref="IsAngle"/>. The display currently does not depend on it, setting it only refreshes the entry on the property page.
        /// </summary>
        public bool IsNormedVector
        {
            get
            {
                return isNormedVector;
            }
            set
            {
                isNormedVector = value;
                propertyPage?.Refresh(this);
            }
        }

        private Angle OnGetLongitude()
        {	// Winkel in der X/Y Ebene
            GeoVector v = GetGeoVector();
            // hier erstmal nur global:
            GeoVector2D v2d = Frame.ActiveView.Projection.DrawingPlane.Project(v);
            return new Angle(v2d);
        }
        private void OnSetLongitude(Angle a)
        {
            GeoVector v = this.GetGeoVector();
            double l = v.Length;
            v.x = Math.Cos(a) * l;
            v.y = Math.Sin(a) * l;
            v.z = 0.0;
            // z bleibt unverändert
            Frame.ActiveView.Projection.DrawingPlane.ToGlobal(v);
            this.SetGeoVector(v);
        }
        private Angle OnGetLatitude()
        {
            GeoVector l = Frame.ActiveView.Projection.DrawingPlane.ToLocal(GetGeoVector());
            return new Angle(Math.Sqrt(l.x * l.x + l.y * l.y), l.z);
        }
        private void OnSetLatitude(Angle a)
        {
            GeoVector l = Frame.ActiveView.Projection.DrawingPlane.ToLocal(GetGeoVector());
            GeoVector nl = new GeoVector(l.x, l.y, 0);
            nl.Length = l.Length;
            ModOp rot = ModOp.Rotate(nl ^ Frame.ActiveView.Projection.DrawingPlane.Normal, a); // cross product correct?
            SetValue(Frame.ActiveView.Projection.DrawingPlane.ToGlobal(rot * nl), true);
        }
        private double OnGetX()
        {
            GeoVector p = GlobalToLocal(GetValue());
            return p.x;
        }
        private void OnSetX(double l)
        {
            GeoVector p = GlobalToLocal(GetValue());
            p.x = l;
            InputFromSubEntries |= EInputFromSubEntries.z;
            SetValue(LocalToGlobal(p), true);
        }
        private double OnGetY()
        {
            GeoVector p = GlobalToLocal(GetValue());
            return p.y;
        }
        private void OnSetY(double l)
        {
            GeoVector p = GlobalToLocal(GetValue());
            p.y = l;
            InputFromSubEntries |= EInputFromSubEntries.z;
            SetValue(LocalToGlobal(p), true);
        }
        private double OnGetZ()
        {
            GeoVector p = GlobalToLocal(GetValue());
            return p.z;
        }
        private void OnSetZ(double l)
        {
            GeoVector p = GlobalToLocal(GetValue());
            p.z = l;
            InputFromSubEntries |= EInputFromSubEntries.z;
            SetValue(LocalToGlobal(p), true);
        }

        /// <summary>
        /// The x component of the value in the coordinate system used for the display (the drawing plane of the active view when the "Formatting.System" setting selects the local system, the global system otherwise). Not used by the sub entries of this class, which work with delegates; available to derived classes.
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
                GeoVector p = GlobalToLocal(GetValue());
                p.x = value;
                InputFromSubEntries |= EInputFromSubEntries.x;
                SetValue(LocalToGlobal(p), true);
            }
        }
        /// <summary>
        /// The y component of the value in the coordinate system used for the display (the drawing plane of the active view when the "Formatting.System" setting selects the local system, the global system otherwise). Not used by the sub entries of this class, which work with delegates; available to derived classes.
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
                GeoVector p = GlobalToLocal(GetValue());
                p.y = value;
                InputFromSubEntries |= EInputFromSubEntries.y;
                SetValue(LocalToGlobal(p), true);
            }
        }
        /// <summary>
        /// The z component of the value in the coordinate system used for the display (the drawing plane of the active view when the "Formatting.System" setting selects the local system, the global system otherwise). Not used by the sub entries of this class, which work with delegates; available to derived classes.
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
                GeoVector p = GlobalToLocal(GetValue());
                p.z = value;
                InputFromSubEntries |= EInputFromSubEntries.z;
                SetValue(LocalToGlobal(p), true);
            }
        }
        /// <summary>
        /// Sets the vector: transfers it to the owning object (through the set delegate or the reflected property) and refreshes the
        /// display. Used by the owner and by actions that construct the value.
        /// </summary>
        /// <param name="l">The new vector in the global coordinate system</param>
        public void SetGeoVector(GeoVector l)
        {
            SetValue(l, true);
        }
        /// <summary>
        /// Returns the current vector as read from the owning object (or the value held by this entry when no object is connected).
        /// </summary>
        /// <returns>The vector in the global coordinate system</returns>
        public GeoVector GetGeoVector()
        {
            return GetValue();
        }
        /// <summary>
        /// Must be called by the owner when the vector has been changed from outside (not through this entry), so that the displayed
        /// value is refreshed. Has no effect while the entry is not on a property page.
        /// </summary>
        public void GeoVectorChanged()
        {
            propertyPage?.Refresh(this);
        }

        /// <inheritdoc/>
        protected override bool TextToValue(string text, out GeoVector val)
        {
            string trimmed = text;
            trimmed = trimmed.Replace("°", ""); // remove the degree character
            char[] WhiteSpace = new char[] { (char)0x9, (char)0xA, (char)0xB, (char)0xC, (char)0xD, (char)0x20, (char)0xA0, (char)0x2000, (char)0x2001, (char)0x2002, (char)0x2003, (char)0x2004, (char)0x2005, (char)0x2006, (char)0x2007, (char)0x2008, (char)0x2009, (char)0x200A, (char)0x200B, (char)0x3000, (char)0xFEFF };
            string[] Parts = trimmed.Split(WhiteSpace);
            GeoVector v = new GeoVector(0.0, 0.0, 0.0);
            bool success = false;
            if (isAngle)
            {
                Plane pl = planeForAngle;
                if (!pl.IsValid())
                {
                    pl = Frame.ActiveView.Projection.DrawingPlane;
                }
                if (Parts.Length == 1)
                {
                    try
                    {
                        Angle a = new Angle();
                        a.Degree = double.Parse(Parts[0], numberFormatInfo);
                        v = pl.ToGlobal(new GeoVector2D(a));
                        success = true;
                    }
                    catch (System.FormatException)
                    {
                        if (Parts[0] == "-") success = true; // allow to start with "-", v will be unchanged
                    }
                }
                else if (Parts.Length == 2)
                {
                    try
                    {
                        Angle longitude = new Angle(), latitude = new Angle();
                        longitude.Degree = double.Parse(Parts[0], numberFormatInfo);
                        latitude.Degree = double.Parse(Parts[1], numberFormatInfo);
                        v = pl.ToGlobal(new GeoVector(longitude, latitude));
                        success = true;
                    }
                    catch (System.FormatException)
                    {
                        if (Parts[0] == "-" || Parts[1] == "-") success = true; // allow to start with "-"
                    }
                }
            }
            else
            {
                if (Parts.Length >= 3)
                {
                    try
                    {
                        v.x = double.Parse(Parts[0], numberFormatInfo);
                        v.y = double.Parse(Parts[1], numberFormatInfo);
                        v.z = double.Parse(Parts[2], numberFormatInfo);
                        success = true;
                    }
                    catch (System.FormatException)
                    {
                        if (Parts[0] == "-" || Parts[1] == "-" || Parts[2] == "-") success = true; // allow to start with "-", some v components will be 0
                    }
                }
                else if (Parts.Length == 2)
                {
                    try
                    {
                        v.x = double.Parse(Parts[0], numberFormatInfo);
                        v.y = double.Parse(Parts[1], numberFormatInfo);
                        v.z = 0.0;
                        success = true;
                    }
                    catch (System.FormatException)
                    {
                        if (Parts[0] == "-" || Parts[1] == "-") success = true; // allow to start with "-", some v components will be 0
                    }
                }
                else if (Parts.Length == 1)
                {
                    try
                    {
                        v.x = double.Parse(Parts[0], numberFormatInfo);
                        v.y = 0.0;
                        v.z = 0.0;
                        success = true;
                    }
                    catch (System.FormatException)
                    {
                        if (Parts[0] == "-") success = true; // allow to start with "-", some v components will be 0
                    }
                }
            }
            if (success)
            {   // ggf. lokale Eingabe berücksichtigen
                val = LocalToGlobal(v);
                return true;
            }
            else
            {
                if (isAngle)
                {
                    try
                    {
                        //Scripting s = new Scripting();
                        //if (Frame != null)
                        //{
                        //    Plane pl = planeForAngle;
                        //    if (!pl.IsValid())
                        //    {
                        //        pl = Frame.ActiveView.Projection.DrawingPlane;
                        //    }
                        //    Angle a = new Angle();
                        //    a.Degree = s.GetDouble(Frame.Project.NamedValues, trimmed);
                        //    val = pl.ToGlobal(new GeoVector2D(a));
                        //    return true;
                        //}
                    }
                    catch //(ScriptingException)
                    {
                    }
                }
                if (!success)
                {
                    try
                    {
                        //Scripting s = new Scripting();
                        //if (Frame != null)
                        //{
                        //    val = s.GetGeoVector(Frame.Project.NamedValues, trimmed);
                        //    return true;
                        //}
                    }
                    catch //(ScriptingException)
                    {
                    }
                }
                val = GeoVector.Invalid;
                return false;
            }
        }
        /// <inheritdoc/>
        protected override string ValueToText(GeoVector p)
        {
            if (isAngle)
            {
                Plane pl;
                if (planeForAngle.IsValid()) pl = planeForAngle;
                else
                {
                    if (Frame.ActiveView is ModelView)
                    {
                        pl = Frame.ActiveView.Projection.DrawingPlane;
                    }
                    else
                    {
                        pl = Plane.XYPlane;
                    }
                }
                GeoVector v = pl.ToLocal(p);
                Angle a = new Angle(v.x, v.y);
                return a.Degree.ToString("f", numberFormatInfo) + "°";
            }
            else
            {
                p = GlobalToLocal(p);
                return p.x.ToString("f", numberFormatInfo) + " " + p.y.ToString("f", numberFormatInfo) + " " + p.z.ToString("f", numberFormatInfo);
            }
        }
        /// <inheritdoc/>
        public override void Selected(IPropertyEntry previousSelected)
        {
            if (previousSelected is GeoVectorProperty gp) SelectionChangedEvent?.Invoke(gp, false);
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
                    if (isAngle)
                    {
                        // hier braucht es m.E. keine Alternativen: die Darstellung in der
                        // ungeöffneten Zeile zeigt nur den Winkel in der Ebene, beim
                        // Aufklappen gibt es zwei Winkel, Ebene und Erhebung
                        // if (isNormedVector)
                        if (true) // Winkel ist immer normiert
                        {
                            switch (displayMode)
                            {
                                case DisplayMode.ShowAngle:
                                    {
                                        subItems = new IPropertyEntry[1];
                                        AngleProperty ap0 = new AngleProperty(Frame, "GeoVector.Longitude");
                                        subItems[0] = ap0;
                                        ap0.OnGetValue = OnGetLongitude;
										ap0.OnSetValue = OnSetLongitude;
                                        ap0.AngleChanged(); // erstmalig initialisieren
                                    }
                                    break;
                                case DisplayMode.ShowPolar:
                                    {
                                        subItems = new IPropertyEntry[2];
                                        AngleProperty ap0 = new AngleProperty(Frame, "GeoVector.Longitude");
                                        subItems[0] = ap0;
										ap0.OnGetValue = OnGetLongitude;
                                        ap0.OnSetValue = OnSetLongitude;
                                        ap0.AngleChanged(); // erstmalig initialisieren
                                        AngleProperty ap1 = new AngleProperty(Frame, "GeoVector.Latitude");
                                        subItems[1] = ap1;
                                        ap1.OnGetValue = OnGetLongitude;
										ap1.OnSetValue = OnSetLongitude;
                                        ap1.AngleChanged(); // erstmalig initialisieren
                                    }
                                    break;
                                case DisplayMode.ShowComponents:
                                    {
                                        subItems = new IPropertyEntry[3];
                                        DoubleProperty dp0 = new DoubleProperty(Frame, "GeoVector.XValue");
                                        dp0.OnGetValue = OnGetX;
                                        dp0.OnSetValue = OnSetX;
                                        dp0.DecimalDigits = componentsDigits;
                                        dp0.Refresh(); // erstmalig initialisieren
                                        subItems[0] = dp0;
                                        DoubleProperty dp1 = new DoubleProperty(Frame, "GeoVector.YValue");
                                        dp1.OnGetValue = OnGetY;
                                        dp1.OnSetValue = OnSetY;
                                        dp1.DecimalDigits = componentsDigits;
                                        dp1.Refresh(); // erstmalig initialisieren
                                        subItems[1] = dp1;
                                        DoubleProperty dp2 = new DoubleProperty(Frame, "GeoVector.ZValue");
                                        dp2.OnGetValue = OnGetZ;
										dp2.OnSetValue = OnSetZ;
                                        dp2.DecimalDigits = componentsDigits;
                                        dp2.Refresh(); // erstmalig initialisieren
                                        subItems[2] = dp2;
                                    }
                                    break;
                                case DisplayMode.ShowComponents2D:
                                    {
                                        subItems = new IPropertyEntry[2];
                                        DoubleProperty dp0 = new DoubleProperty(Frame, "GeoVector.XValue");
                                        dp0.OnGetValue = OnGetX;
										dp0.OnSetValue = OnSetX;
                                        dp0.DecimalDigits = componentsDigits;
                                        dp0.DoubleChanged(); // erstmalig initialisieren
                                        subItems[0] = dp0;
                                        DoubleProperty dp1 = new DoubleProperty(Frame, "GeoVector.YValue");
										dp1.OnGetValue = OnGetY;
                                        dp1.OnSetValue = OnSetY;
                                        dp1.DecimalDigits = componentsDigits;
                                        dp1.DoubleChanged(); // erstmalig initialisieren
                                        subItems[1] = dp1;
                                    }
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (displayMode == DisplayMode.ShowComponents2D)
                        {
                            subItems = new IPropertyEntry[2];
                            DoubleProperty dp0 = new DoubleProperty(Frame, "GeoVector.XValue");
                            dp0.OnGetValue = OnGetX;
							dp0.OnSetValue = OnSetX;
                            dp0.DecimalDigits = componentsDigits;
                            dp0.DoubleChanged(); // erstmalig initialisieren
                            subItems[0] = dp0;
                            DoubleProperty dp1 = new DoubleProperty(Frame, "GeoVector.YValue");
							dp1.OnGetValue = OnGetY;
                            dp1.OnSetValue = OnSetY;
                            dp1.DecimalDigits = componentsDigits;
                            dp1.DoubleChanged(); // erstmalig initialisieren
                            subItems[1] = dp1;
                        }
                        else
                        {
                            subItems = new IPropertyEntry[3];
                            DoubleProperty dp0 = new DoubleProperty(Frame, "GeoVector.XValue");
                            dp0.OnGetValue = OnGetX;
							dp0.OnSetValue = OnSetX;
                            dp0.DecimalDigits = componentsDigits;
                            dp0.DoubleChanged(); // erstmalig initialisieren
                            subItems[0] = dp0;
                            DoubleProperty dp1 = new DoubleProperty(Frame, "GeoVector.YValue");
							dp1.OnGetValue = OnGetY;
                            dp1.OnSetValue = OnSetY;
                            dp1.DecimalDigits = componentsDigits;
                            dp1.DoubleChanged(); // erstmalig initialisieren
                            subItems[1] = dp1;
                            DoubleProperty dp2 = new DoubleProperty(Frame, "GeoVector.ZValue");
							dp2.OnGetValue = OnGetZ;
                            dp2.OnSetValue = OnSetZ;
                            dp0.DecimalDigits = componentsDigits;
                            dp2.DoubleChanged(); // erstmalig initialisieren
                            subItems[2] = dp2;
                        }
                    }
                }
                for (int i = 0; i < subItems.Length; i++)
                {
                    if (subItems[i] is DoubleProperty)
                    {
                        (subItems[i] as DoubleProperty).ReadOnly = ReadOnly;
                    }
                    if (subItems[i] is AngleProperty)
                    {
                        (subItems[i] as AngleProperty).ReadOnly = ReadOnly;
                    }
                }
                return subItems;
            }
        }

        private GeoVector LocalToGlobal(GeoVector v)
        {
            if (displayCoordinateSystem == DisplayCoordinateSystem.local && !alwaysAbsoluteCoordinateSystem)
            {
                drawingPlane = Frame.ActiveView.Projection.DrawingPlane;
                v = drawingPlane.ToGlobal(v);
            }
            return v;
        }
        private GeoVector GlobalToLocal(GeoVector p)
        {
            if (displayCoordinateSystem == DisplayCoordinateSystem.local && !alwaysAbsoluteCoordinateSystem)
            {
                Plane drawingPlane = Frame.ActiveView.Projection.DrawingPlane;
                return drawingPlane.ToLocal(p);
            }
            return p;
        }


        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Vector.ModifyWithMouse":
					ModifyWithMouse?.Invoke(this, false);
					return true;
                case "MenuId.Vector.DirectionOfCurve":
                    Frame.SetAction(new CADability.Actions.ConstructDirectionOfCurve(this));
                    return true;
                case "MenuId.Vector.DirectionOfSurface":
                    Frame.SetAction(new CADability.Actions.ConstructDirectionOfSurface(this));
                    return true;
                case "MenuId.Vector.DirectionTwoPoints":
                    Frame.SetAction(new CADability.Actions.ConstructDirectionTwoPoints(this));
                    return true;
                case "MenuId.Vector.NameVariable":
                    Frame.Project.SetNamedValue(null, GetGeoVector());
                    return true;
                // return false;
                case "MenuId.Vector.FormatSettings":
                    {
                        Frame.ShowPropertyDisplay("Global");
                        IPropertyPage pd = Frame.GetPropertyDisplay("Global");
                        IPropertyEntry sp = pd.FindFromHelpLink("Setting.Formatting");
                        if (sp != null)
                        {
                            pd.OpenSubEntries(sp, true);
                            sp = pd.FindFromHelpLink("Setting.Formatting.Vector");
                            if (sp != null)
                            {
                                pd.OpenSubEntries(sp, true);
                                pd.SelectEntry(sp);
                            }
                        }
                    }
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            if (FilterCommandEvent != null)
            {
                bool handled = false;
                FilterCommandEvent(this, MenuId, CommandState, ref handled);
                if (handled) return true;
            }
            switch (MenuId)
            {
                case "MenuId.Vector.ModifyWithMouse":
                    CommandState.Enabled = (ModifyWithMouse != null);
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion

        #region deprecated adaption to old implementation of GeoVectorProperty
        /// <summary>
        /// Obsolete: creates the entry like <see cref="GeoVectorProperty(IFrame, string)"/>; <paramref name="autoModifyWithMouse"/> is ignored.
        /// </summary>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="frame">The frame whose formatting settings are used for the display</param>
        /// <param name="autoModifyWithMouse">Ignored</param>
        [Obsolete("Parameter autoModifyWithMouse no longer supported, use GeoVectorProperty(IFrame frame, string resourceId) instead")]
        public GeoVectorProperty(string resourceId, IFrame frame, bool autoModifyWithMouse) : this(frame, resourceId)
        {
        }
        /// <summary>
        /// Obsolete handler signature of <see cref="SetGeoVectorEvent"/>; use <see cref="EditableProperty{T}.SetValueDelegate"/> instead.
        /// </summary>
        /// <param name="sender">The property entry whose value is set</param>
        /// <param name="p">The new vector</param>
        [Obsolete("use GeoVectorProperty.SetValueDelegate instead")]
        public delegate void SetGeoVectorDelegate(GeoVectorProperty sender, GeoVector p);
        /// <summary>
        /// Obsolete handler signature of <see cref="GetGeoVectorEvent"/>; use <see cref="EditableProperty{T}.GetValueDelegate"/> instead.
        /// </summary>
        /// <param name="sender">The property entry asking for the value</param>
        /// <returns>The current vector</returns>
        [Obsolete("use GeoVectorProperty.GetValueDelegate instead")]
        public delegate GeoVector GetGeoVectorDelegate(GeoVectorProperty sender);
        /// <summary>
        /// Obsolete: adding a handler installs it as <see cref="EditableProperty{T}.OnSetValue"/>, so it is called whenever a new value is
        /// to be transferred to the owner. Only one handler is kept (adding replaces the previous one), removing clears the delegate.
        /// </summary>
        [Obsolete("use delegate GeoVectorProperty.OnSetValue instead")]
        public event SetGeoVectorDelegate SetGeoVectorEvent
        {
            add
            {
                base.OnSetValue = delegate (GeoVector l) { value(this, l); };
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
        [Obsolete("use delegate GeoVectorProperty.OnGetValue instead")]
        public event GetGeoVectorDelegate GetGeoVectorEvent
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
        [Obsolete("use delegate GeoVectorProperty.ModifyWithMouse instead")]
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
        [Obsolete("use delegate GeoVectorProperty.LockedChanged instead")]
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
        [Obsolete("use delegate GeoVectorProperty.LabelTextChanged instead")]
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

        /// <summary>
        /// Flags that record which components of the vector have been entered through the x, y and z sub entries; see
        /// <see cref="InputFromSubEntries"/>.
        /// </summary>
        public enum EInputFromSubEntries { x = 0x1, y = 0x2, z = 0x4 }
        /// <summary>
        /// Records which components of the vector the user has entered through the sub entries (the flags are set by the sub entries and
        /// not cleared by this class). The counterpart <see cref="GeoPointProperty.InputFromSubEntries"/> is evaluated by construct
        /// actions to recognize partially fixed input; for a vector the value is currently not evaluated.
        /// </summary>
        public EInputFromSubEntries InputFromSubEntries = 0;
        private bool isAngle;
        private Plane planeForAngle;
        private bool isNormedVector;

        internal GeoVector GetPartiallyFixed(GeoVector p)
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
        public delegate void ModifiedByActionDelegate(GeoVectorProperty sender);
        /// <summary>
        /// This <see cref="ModifiedByActionDelegate"/> event will be raised when the GeoVector was modified by
        /// some <see cref="Action"/>. ( The <see cref="SetGeoVectorEvent"/> will also be raised or the property will be set)
        /// </summary>
        public event ModifiedByActionDelegate ModifiedByActionEvent;
        internal void ModifiedByAction(Action action)
        {
			ModifiedByActionEvent?.Invoke(this);
		}
        /// <summary>
        /// Intended to force the display in the absolute coordinate system regardless of the "Formatting.System" setting; currently not
        /// evaluated by this class. Can only be set from within CADability.
        /// </summary>
        public bool ForceAbsolute { get; internal set; }
        // the following should be removed and the caller should call SetContextMenu with itself as commandhandler
        /// <summary>
        /// The menu id (from the menu resource) of the context menu of this entry, "MenuId.Vector" by default. Setting it replaces the
        /// context menu by the menu with the given id and makes this entry the <see cref="ICommandHandler"/> of that menu (e.g. "MenuId.Projection.Direction" for the projection direction of a view).
        /// Prefer <see cref="EditableProperty{T}.SetContextMenu(string, ICommandHandler)"/> with an own command handler.
        /// </summary>
        public string ContextMenuId { get => GetContextMenuId(); set => SetContextMenu(value, this); }
        /// <summary>
        /// Delegate definition for <see cref="FilterCommandEvent"/>
        /// </summary>
        /// <param name="sender">this object</param>
        /// <param name="menuId">menu id of the selected menu entry</param>
        /// <param name="commandState">when not null, asks for the state of the menu</param>
        /// <param name="handled">set to true if handled</param>
        public delegate void FilterCommandDelegate(GeoVectorProperty sender, string menuId, CommandState commandState, ref bool handled);
        /// <summary>
        /// When a context menue is selected or about to popup this event is raised to allow a
        /// consumer to process the command instead of this GeoVectorProperty object itself.
        /// Provide a handler here if you want to process some or all menu commands.
        /// </summary>
        public event FilterCommandDelegate FilterCommandEvent;
    }
}


