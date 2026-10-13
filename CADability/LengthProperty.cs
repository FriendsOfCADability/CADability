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
                return lengthProperty.IsSelected;
            }
            set
            {
                lengthProperty.IsSelected = value;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.IHotSpot.StartDrag(IFrame)"/>
        /// </summary>
        public void StartDrag(IFrame frame)
        {
            lengthProperty.StartModifyWithMouse();
        }
        public string GetInfoText(CADability.UserInterface.InfoLevelMode Level)
        {
            return lengthProperty.Label;
        }
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
        public bool Hidden { get { return lengthProperty.ReadOnly; } }
        #endregion

        virtual public bool OnCommand(string MenuId)
        {
            return ((ICommandHandler)lengthProperty).OnCommand(MenuId);
        }

        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return ((ICommandHandler)lengthProperty).OnUpdateCommand(MenuId, CommandState);
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
    }


    public class LengthPropertyException : System.ApplicationException
    {
        public LengthPropertyException(string msg)
            : base(msg)
        {
        }
    }

    public class LengthProperty : EditableProperty<double>, ICommandHandler
    {
        private NumberFormatInfo numberFormatInfo;

        public LengthProperty(IFrame frame, string resourceId = null) : base(resourceId, "MenuId.Length")
        {
            InitFormat(frame);
        }

        public LengthProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, IFrame frame, string resourceId = null) : base(getValueDelegate, setValueDelegate, resourceId, "MenuId.Length")
        {
            InitFormat(frame);
        }

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

        public void SetLength(double l)
        {
            SetValue(l, true);
        }
        public double GetLength()
        {
            return GetValue();
        }
        public void LengthChanged()
        {
            propertyPage?.Refresh(this);
        }

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
        [Obsolete("Parameter autoModifyWithMouse no longer supported, use LengthProperty(IFrame frame, string resourceId) instead")]
        public LengthProperty(string resourceId, IFrame frame, bool autoModifyWithMouse) : this(frame, resourceId)
        {
        }
        [Obsolete("use LengthProperty.SetValueDelegate instead")]
        public delegate void SetLengthDelegate(LengthProperty sender, double l);
        [Obsolete("use LengthProperty.GetValueDelegate instead")]
        public delegate double GetLengthDelegate(LengthProperty sender);
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
        [Obsolete("method has no functionality, remove this call")]
        public void CheckMouseButton(bool Check) { }
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
