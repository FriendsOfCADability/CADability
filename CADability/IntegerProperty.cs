using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;

namespace CADability.UserInterface
{
    /// <summary>
    /// Display an editable integer value in the property grid.
    /// </summary>
    [Serializable()]
    public class IntegerProperty : EditableProperty<int>, ISerializable, ICommandHandler, IDeserializationCallback, IJsonSerialize, IJsonSerializeDone
    {
        private object ObjectWithInt;
        private PropertyInfo TheProperty;
        private string text;
        private bool IsSetting;
        //private bool highlight;
        private int minValue; // Grenzen für die Eingabe
        private int maxValue;
        private bool showUpDown; // Pfeil auf und ab Control anzeigen
        private int internalValue; // der interne Wert, wenn nicht auf ein anderes Objekt bezogen
        private string settingName; // wenn in einem Setting verwendet, so ist hier der Name des Wertes
        private Dictionary<int, string> specialValues;
        public delegate void SetIntDelegate(IntegerProperty sender, int newValue);
        public delegate int GetIntDelegate(IntegerProperty sender);
        public event GetIntDelegate GetIntEvent;
        public event SetIntDelegate SetIntEvent;
        private void Initialize(object objectWithInt, string propertyName, string resourceId)
        {
            IsSetting = false;
            if (resourceId != null) this.resourceIdInternal = resourceId;
            this.ObjectWithInt = objectWithInt;
            TheProperty = objectWithInt.GetType().GetProperty(propertyName);
            if (TheProperty == null)
            {   // speziell für ERSACAD: dort haben wg. der flexiblen Namensvergabe die Properties manchmal
                // einen prefix, der nicht entsprechend geändert wird. Hier wird, wenn eine passende Property
                // nicht gefunden wird, eine solche gesucht, die mit dem namen endet. Ggf könnte man auch den Typ sicherstellen
                PropertyInfo[] props = objectWithInt.GetType().GetProperties();
                for (int i = 0; i < props.Length; i++)
                {
                    if (props[i].Name.EndsWith(propertyName, true, CultureInfo.InvariantCulture))
                    {
                        TheProperty = props[i];
                        break;
                    }
                }
            }
            IntChanged();
        }
        public IntegerProperty(object objectWithInt, string propertyName, string resourceId)
        {
            Initialize(objectWithInt, propertyName, resourceId);
            NotifyOnLostFocusOnly = false;
        }
        public IntegerProperty(int initialValue, string resourceId)
        {
            IsSetting = false;
            if (resourceId != null) this.resourceIdInternal = resourceId;
            internalValue = initialValue;
            text = initialValue.ToString();
            NotifyOnLostFocusOnly = false;
        }
        public IntegerProperty(string resourceId, string settingName)
        {
            this.settingName = settingName;
            Initialize(this, "IntegerValue", resourceId);
            NotifyOnLostFocusOnly = false;
        }
        public void SetMinMax(int min, int max, bool showupdown)
        {
            minValue = min;
            maxValue = max;
            showUpDown = showupdown;
            //numericUpDown.Minimum = min;
            //numericUpDown.Maximum = max;
            //numericUpDown.Increment = 1;
        }
        public void AddSpecialValue(int val, string resourceId)
        {
            if (specialValues == null) specialValues = new Dictionary<int, string>();
            specialValues[val] = resourceId;
        }
        public int IntegerValue
        {
            get { return internalValue; }
            set
            {
                internalValue = value;
                SetText(internalValue);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Refresh ()"/>
        /// </summary>
        public override void Refresh()
        {
            internalValue = GetInt();
            if (specialValues != null)
            {
                foreach (KeyValuePair<int, string> kv in specialValues)
                {
                    if (kv.Key == internalValue)
                    {
                        text = StringTable.GetString(kv.Value);
                        if (text.StartsWith("!")) text = text.Substring(1);
                        return;
                    }
                }
            }
            SetText(internalValue);
        }
        public bool UpDownOnly
        {
            set
            {
                //numericUpDown.ReadOnly = value;
            }
            get
            {
                //return numericUpDown.ReadOnly;
                return false;
            }
        }
        public static explicit operator int(IntegerProperty ip) { return ip.IntegerValue; }
        private void SetText(int d)
        {
            if (specialValues != null && specialValues.TryGetValue(d, out string sv))
            {
                text = StringTable.GetString(sv);
            }
            else
            {
                text = d.ToString();
            }
            propertyPage?.Refresh(this);
        }
        public void SetInt(int d)
        {
            if (d == internalValue)
                return;
            internalValue = d;
            if (SetIntEvent != null)
            {
                try
                {
                    IsSetting = true;
                    SetIntEvent(this, d);
                }
                finally
                {
                    IsSetting = false;
                }
            }
            else if (TheProperty != null)
            {
                MethodInfo mi = TheProperty.GetSetMethod();
                object[] prm = new object[1];
                prm[0] = d;
                try
                {
                    IsSetting = true;
                    mi.Invoke(ObjectWithInt, prm);
                }
                finally
                {
                    IsSetting = false;
                }
            }
        }
        public bool NotifyOnLostFocusOnly { get; set; }
        private int GetInt()
        {
            if (GetIntEvent != null)
            {
                return GetIntEvent(this);
            }
            else
            {
                MethodInfo mi = TheProperty.GetGetMethod();
                object[] prm = new Object[0];
                return (int)mi.Invoke(ObjectWithInt, prm);
            }
        }

        /// <summary>
        /// Der Besitzer dieses Objektes muss GeoPointChanged aufrufen, um eine neue Anzeige zu erzwingen.
        /// </summary>
        public void IntChanged()
        {
            if (IsSetting) return; // wir sind selbst der Urheber
            int d = GetInt();
            if (specialValues != null)
            {
                foreach (KeyValuePair<int, string> kv in specialValues)
                {
                    if (kv.Key == d)
                    {
                        text = StringTable.GetString(kv.Value);
                        if (text.StartsWith("!")) text = text.Substring(1);
                        propertyPage?.Refresh(this);
                        return;
                    }
                }
            }
            SetText(d);
        }

        #region IShowPropertyImpl Overrides
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType res = PropertyEntryType.Selectable | PropertyEntryType.ValueEditable;
                //if (highlight) res |= PropertyEntryType.Highlight;
                if (showUpDown) res |= PropertyEntryType.HasSpinButton;
                if (specialValues != null && specialValues.Count > 0)
                    res |= PropertyEntryType.ContextMenu;
                return res;
            }
        }
        public override string Value
        {
            get
            {
                // SetText(internalValue); no! internalValue is not up to date
                if (text == null) SetText(internalValue);
                return text;
            }
        }
        private bool Decode(string txt)
        {
            bool success = false;
            string trimmed;
            trimmed = txt.Trim();
            try
            {
                int d = int.Parse(trimmed);
                SetInt(d);
                success = true;
            }
            catch (FormatException)
            {
            }
            catch (System.OverflowException)
            {
            }
            return success;
        }
        private int valueBeforeEdit;
        public override void StartEdit(bool editValue)
        {
            valueBeforeEdit = internalValue;
        }
        public override bool EditTextChanged(string newValue)
        {
            using (Frame.Project.Undo.ContextFrame(this))
            {
                return Decode(newValue);
            }
        }
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            if (aborted)
            {
                SetInt(valueBeforeEdit);
            }
            else if (modified)
            {
                Decode(newValue);
            }
            Frame.Project.Undo.ClearContext();
            Refresh(); // because of SetText
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                string[] menuIDs = new string[specialValues.Count];
                specialValues.Values.CopyTo(menuIDs, 0);
                return MenuResource.CreateContextMenuWithHandler(menuIDs, this);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.UnSelected ()"/>
        /// </summary>
        //public override void UnSelected()
        //{
        //    if (minValue != maxValue)
        //    {   // 0, 0 wenn nicht gesetzt
        //        if (internalValue < minValue)
        //        {
        //            IntegerValue = minValue;
        //        }
        //        if (internalValue > maxValue)
        //        {
        //            IntegerValue = maxValue;
        //        }
        //    }
        //    base.UnSelected();
        //}
        #endregion

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected IntegerProperty(SerializationInfo info, StreamingContext context)
        {
            internalValue = (int)info.GetValue("InternalValue", typeof(int));
            resourceIdInternal = (string)info.GetValue("resourceId", typeof(string));
            settingName = (string)info.GetValue("SettingName", typeof(string));
            try
            {
                specialValues = (Dictionary<int, string>)info.GetValue("SpecialValues", typeof(Dictionary<int, string>));
            }
            catch (SerializationException)
            {
                specialValues = null;
            }
            NotifyOnLostFocusOnly = false;
        }

        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("InternalValue", internalValue, internalValue.GetType());
            info.AddValue("resourceId", resourceIdInternal, resourceIdInternal.GetType());
            info.AddValue("SettingName", settingName, typeof(string)); // kann auch null sein
            info.AddValue("SpecialValues", specialValues, typeof(Dictionary<int, string>));
        }
        protected IntegerProperty() { } // needed for IJsonSerialize
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("InternalValue", internalValue);
            data.AddProperty("resourceId", resourceIdInternal);
            if (settingName != null) data.AddProperty("SettingName", settingName);
            if (specialValues != null) data.AddProperty("SpecialValues", specialValues);
        }

        public void SetObjectData(IJsonReadData data)
        {
            internalValue = data.GetProperty<int>("InternalValue");
            resourceIdInternal = data.GetProperty<string>("resourceId");
            settingName = data.GetPropertyOrDefault<string>("SettingName");
            specialValues = data.GetPropertyOrDefault<Dictionary<int, string>>("SpecialValues");
            data.RegisterForSerializationDoneCallback(this);
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            this.Initialize(this, "IntegerValue", null);
        }
        #endregion

        #region IDeserializationCallback Members
        // nicht FinishDeserialization wg. Debuggen
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            this.Initialize(this, "IntegerValue", null);
        }

        #endregion
		
        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string menuId)
        {
            foreach (KeyValuePair<int, string> kv in specialValues)
            {
                if (kv.Value == menuId)
                {
                    text = StringTable.GetString(menuId);
                    if (text.StartsWith("!")) text = text.Substring(1);
                    propertyPage?.Refresh(this);
                    SetInt(kv.Key);
                    SetText(kv.Key);
                    return true;
                }
            }
            return false;
        }

        bool ICommandHandler.OnUpdateCommand(string menuId, CommandState commandState)
        {
            return true;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        protected override string ValueToText(int val)
        {
            return val.ToString();
        }

        protected override bool TextToValue(string input, out int val)
        {
            return int.TryParse(input, out val);
        }

        #endregion
    }
}
