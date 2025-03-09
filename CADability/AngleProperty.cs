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

        public virtual bool OnCommand(string MenuId)
        {
            return ((ICommandHandler)angleProperty).OnCommand(MenuId);
        }

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

    public class AngleProperty : EditableProperty<Angle>, ICommandHandler
    {
        private NumberFormatInfo numberFormatInfo;

        public AngleProperty(IFrame frame, string resourceId = null) : base(resourceId, "MenuId.Angle")
        {
            InitFormat(frame);
        }

        public AngleProperty(GetValueDelegate getValueDelegate, SetValueDelegate setValueDelegate, IFrame frame, string resourceId = null) : base(getValueDelegate, setValueDelegate, resourceId, "MenuId.Angle")
        {
            InitFormat(frame);
        }

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

        public bool PreferNegativeValues { get; set; }
        public void SetAngle(double l)
        {
            SetValue(l, true);
        }
        public double GetAngle()
        {
            return GetValue();
        }
        public void AngleChanged()
        {
            propertyPage?.Refresh(this);
        }

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
        [Obsolete("Parameter autoModifyWithMouse no longer supported, use AngleProperty(IFrame frame, string resourceId) instead")]
        public AngleProperty(string resourceId, IFrame frame, bool autoModifyWithMouse) : this(frame, resourceId)
        {
        }
        [Obsolete("use AngleProperty.SetValueDelegate instead")]
        public delegate void SetAngleDelegate(Angle angle);
        [Obsolete("use AngleProperty.GetValueDelegate instead")]
        public delegate Angle GetAngleDelegate();
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
        [Obsolete("method has no functionality, remove this call")]
        public void CheckMouseButton(bool Check) { }

        [Obsolete("use delegate AngleProperty.LabelTextChanged instead")]
		public new delegate void LabelChangedDelegate(LengthProperty sender, string newLabel);
		
        [Obsolete("use delegate AngleProperty.LabelTextChanged instead")]
#pragma warning disable CS0067 // Event is never used
        public event LabelChangedDelegate LabelChangedEvent;
#pragma warning restore CS0067 // Event is never used
        #endregion

    }
}
