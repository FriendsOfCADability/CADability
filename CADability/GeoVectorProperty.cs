using System;
using System.Globalization;
using System.Reflection;
using CADability.GeoObject;
using Action = CADability.Actions.Action;

namespace CADability.UserInterface
{
    public class GeoVectorHotSpot : IHotSpot, ICommandHandler
    {
        private GeoVectorProperty geoVectorProperty;
        public GeoPoint Position;
        public GeoVectorHotSpot(GeoVectorProperty geoVectorProperty)
        {
            this.geoVectorProperty = geoVectorProperty;
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
                return geoVectorProperty.IsSelected;
            }
            set
            {
                geoVectorProperty.IsSelected = value;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.IHotSpot.StartDrag(IFrame)"/>
        /// </summary>
        public void StartDrag(IFrame frame)
        {
            geoVectorProperty.StartModifyWithMouse();
        }
        public string GetInfoText(CADability.UserInterface.InfoLevelMode Level)
        {
            return geoVectorProperty.Label;
        }
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
        public bool Hidden { get { return geoVectorProperty.ReadOnly; } }
        #endregion
        virtual public bool OnCommand(string MenuId)
        {
            return ((ICommandHandler)geoVectorProperty).OnCommand(MenuId);
        }
                virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return ((ICommandHandler)geoVectorProperty).OnUpdateCommand(MenuId, CommandState);
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
    }

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
        public GeoVectorProperty(IFrame frame, string resourceId = null) : base(resourceId, "MenuId.Vector")
        {
            InitFormat(frame);
        }

        public GeoVectorProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, IFrame frame, string resourceId = null) : base(getValueDelegate, setValueDelegate, resourceId, "MenuId.Vector")
        {
            InitFormat(frame);
        }

        public GeoVectorProperty(object ObjectWithGeoVector, string PropertyName, string resourceId, IFrame frame, bool autoModifyWithMouse = true)
            : base(ObjectWithGeoVector, PropertyName, resourceId, "MenuId.Vector")
        {
            InitFormat(frame);
        }

        private void InitFormat(IFrame frame)
        {
            MultipleChoiceSetting formattingSystem = frame.GetSetting("Formatting.System") as MultipleChoiceSetting;
            if (formattingSystem != null && formattingSystem.CurrentSelection >= 0)
            {
                displayCoordinateSystem = (DisplayCoordinateSystem)formattingSystem.CurrentSelection;
            }
            else
            {
                displayCoordinateSystem = DisplayCoordinateSystem.local;
            }
            MultipleChoiceSetting formattingZValue = frame.GetSetting("Formatting.Coordinate.ZValue") as MultipleChoiceSetting;
            if (formattingZValue != null && formattingZValue.CurrentSelection >= 0)
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

        public bool DisplayZComponent { get; set; } = true;
        public bool AlwaysZComponent { get; set; }
        public bool UseLocalSystem { get; set; }
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
        public Plane PlaneForAngle
        {
            get { return planeForAngle; }
            set
            {
                planeForAngle = value;
                propertyPage?.Refresh(this);
            }
        }
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
        private double OnGetX(DoubleProperty sender)
        {
            GeoVector p = GlobalToLocal(GetValue());
            return p.x;
        }
        private void OnSetX(DoubleProperty sender, double l)
        {
            GeoVector p = GlobalToLocal(GetValue());
            p.x = l;
            InputFromSubEntries |= EInputFromSubEntries.z;
            SetValue(LocalToGlobal(p), true);
        }
        private double OnGetY(DoubleProperty sender)
        {
            GeoVector p = GlobalToLocal(GetValue());
            return p.y;
        }
        private void OnSetY(DoubleProperty sender, double l)
        {
            GeoVector p = GlobalToLocal(GetValue());
            p.y = l;
            InputFromSubEntries |= EInputFromSubEntries.z;
            SetValue(LocalToGlobal(p), true);
        }
        private double OnGetZ(DoubleProperty sender)
        {
            GeoVector p = GlobalToLocal(GetValue());
            return p.z;
        }
        private void OnSetZ(DoubleProperty sender, double l)
        {
            GeoVector p = GlobalToLocal(GetValue());
            p.z = l;
            InputFromSubEntries |= EInputFromSubEntries.z;
            SetValue(LocalToGlobal(p), true);
        }

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
        public void SetGeoVector(GeoVector l)
        {
            SetValue(l, true);
        }
        public GeoVector GetGeoVector()
        {
            return GetValue();
        }
        public void GeoVectorChanged()
        {
            propertyPage?.Refresh(this);
        }

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
        public override void Selected(IPropertyEntry previousSelected)
        {
            if (previousSelected is GeoVectorProperty gp) SelectionChangedEvent?.Invoke(gp, false);
            SelectionChangedEvent?.Invoke(this, true);
            base.Selected(previousSelected);
        }
        public override PropertyEntryType Flags => base.Flags | PropertyEntryType.HasSubEntries;
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
                                        AngleProperty ap0 = new AngleProperty("GeoVector.Longitude", Frame, false);
                                        subItems[0] = ap0;
                                        ap0.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnGetLongitude);
                                        ap0.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnSetLongitude);
                                        ap0.AngleChanged(); // erstmalig initialisieren
                                    }
                                    break;
                                case DisplayMode.ShowPolar:
                                    {
                                        subItems = new IPropertyEntry[2];
                                        AngleProperty ap0 = new AngleProperty("GeoVector.Longitude", Frame, false);
                                        subItems[0] = ap0;
                                        ap0.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnGetLongitude);
                                        ap0.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnSetLongitude);
                                        ap0.AngleChanged(); // erstmalig initialisieren
                                        AngleProperty ap1 = new AngleProperty("GeoVector.Latitude", Frame, false);
                                        subItems[1] = ap1;
                                        ap1.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnGetLatitude);
                                        ap1.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnSetLatitude);
                                        ap1.AngleChanged(); // erstmalig initialisieren
                                    }
                                    break;
                                case DisplayMode.ShowComponents:
                                    {
                                        subItems = new IPropertyEntry[3];
                                        DoubleProperty dp0 = new DoubleProperty("GeoVector.XValue", Frame);
                                        dp0.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetX);
                                        dp0.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetX);
                                        dp0.DecimalDigits = componentsDigits;
                                        dp0.Refresh(); // erstmalig initialisieren
                                        subItems[0] = dp0;
                                        DoubleProperty dp1 = new DoubleProperty("GeoVector.YValue", Frame);
                                        dp1.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetY);
                                        dp1.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetY);
                                        dp1.DecimalDigits = componentsDigits;
                                        dp1.Refresh(); // erstmalig initialisieren
                                        subItems[1] = dp1;
                                        DoubleProperty dp2 = new DoubleProperty("GeoVector.ZValue", Frame);
                                        dp2.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetZ);
                                        dp2.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetZ);
                                        dp2.DecimalDigits = componentsDigits;
                                        dp2.Refresh(); // erstmalig initialisieren
                                        subItems[2] = dp2;
                                    }
                                    break;
                                case DisplayMode.ShowComponents2D:
                                    {
                                        subItems = new IPropertyEntry[2];
                                        DoubleProperty dp0 = new DoubleProperty("GeoVector.XValue", Frame);
                                        dp0.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetX);
                                        dp0.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetX);
                                        dp0.DecimalDigits = componentsDigits;
                                        dp0.DoubleChanged(); // erstmalig initialisieren
                                        subItems[0] = dp0;
                                        DoubleProperty dp1 = new DoubleProperty("GeoVector.YValue", Frame);
                                        dp1.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetY);
                                        dp1.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetY);
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
                            DoubleProperty dp0 = new DoubleProperty("GeoVector.XValue", Frame);
                            dp0.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetX);
                            dp0.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetX);
                            dp0.DecimalDigits = componentsDigits;
                            dp0.DoubleChanged(); // erstmalig initialisieren
                            subItems[0] = dp0;
                            DoubleProperty dp1 = new DoubleProperty("GeoVector.YValue", Frame);
                            dp1.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetY);
                            dp1.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetY);
                            dp1.DecimalDigits = componentsDigits;
                            dp1.DoubleChanged(); // erstmalig initialisieren
                            subItems[1] = dp1;
                        }
                        else
                        {
                            subItems = new IPropertyEntry[3];
                            DoubleProperty dp0 = new DoubleProperty("GeoVector.XValue", Frame);
                            dp0.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetX);
                            dp0.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetX);
                            dp0.DecimalDigits = componentsDigits;
                            dp0.DoubleChanged(); // erstmalig initialisieren
                            subItems[0] = dp0;
                            DoubleProperty dp1 = new DoubleProperty("GeoVector.YValue", Frame);
                            dp1.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetY);
                            dp1.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetY);
                            dp1.DecimalDigits = componentsDigits;
                            dp1.DoubleChanged(); // erstmalig initialisieren
                            subItems[1] = dp1;
                            DoubleProperty dp2 = new DoubleProperty("GeoVector.ZValue", Frame);
                            dp2.GetDoubleEvent += new CADability.UserInterface.DoubleProperty.GetDoubleDelegate(OnGetZ);
                            dp2.SetDoubleEvent += new CADability.UserInterface.DoubleProperty.SetDoubleDelegate(OnSetZ);
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
                    if (ModifyWithMouse != null) ModifyWithMouse(this, false);
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
        [Obsolete("Parameter autoModifyWithMouse no longer supported, use GeoVectorProperty(IFrame frame, string resourceId) instead")]
        public GeoVectorProperty(string resourceId, IFrame frame, bool autoModifyWithMouse) : this(frame, resourceId)
        {
        }
        [Obsolete("use GeoVectorProperty.SetValueDelegate instead")]
        public delegate void SetGeoVectorDelegate(GeoVectorProperty sender, GeoVector p);
        [Obsolete("use GeoVectorProperty.GetValueDelegate instead")]
        public delegate GeoVector GetGeoVectorDelegate(GeoVectorProperty sender);
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
        [Obsolete("method has no functionality, remove this call")]
        public void CheckMouseButton(bool Check) { }
        [Obsolete("has no funtionality")]
        public bool TabIsSpecialKeyEvent { get; internal set; }
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
        [Obsolete("has no funtionality")]
        public bool IsModifyingWithMouse { get; internal set; }
        #endregion

        public enum EInputFromSubEntries { x = 0x1, y = 0x2, z = 0x4 }
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
            if (ModifiedByActionEvent != null) ModifiedByActionEvent(this);
        }
        public bool ForceAbsolute { get; internal set; }
        // the following should be removed and the caller should call SetContextMenu with itself as commandhandler
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


