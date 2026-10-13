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
        /// <summary>
        /// Handler signature of <see cref="SetIntEvent"/>.
        /// </summary>
        /// <param name="sender">The property entry whose value has been changed</param>
        /// <param name="newValue">The new value</param>
        public delegate void SetIntDelegate(IntegerProperty sender, int newValue);
        /// <summary>
        /// Handler signature of <see cref="GetIntEvent"/>.
        /// </summary>
        /// <param name="sender">The property entry asking for the value</param>
        /// <returns>The current value</returns>
        public delegate int GetIntDelegate(IntegerProperty sender);
        /// <summary>
        /// When a handler is attached, the entry asks it for the current value instead of reading the reflected property of the connected
        /// object or the internal <see cref="IntegerValue"/>. Called whenever the display is refreshed (<see cref="IntChanged"/>,
        /// <see cref="Refresh"/>) and before a new value is set.
        /// </summary>
        public event GetIntDelegate GetIntEvent;
        /// <summary>
        /// Raised by <see cref="SetInt(int)"/> with the new value when the user has changed it (by typing, with the spin button or from
        /// the context menu of the special values), so that the owner can take it over. When a handler is attached, the reflected property
        /// of the connected object is not set.
        /// </summary>
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
        /// <summary>
        /// Creates an entry that reads and writes the value through reflection on a property of the owning object. When no property with
        /// exactly that name exists, the first property whose name ends with <paramref name="propertyName"/> (ignoring case) is used.
        /// </summary>
        /// <param name="objectWithInt">The object that owns the value</param>
        /// <param name="propertyName">Name of a public property of type int on <paramref name="objectWithInt"/></param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null to keep the default</param>
        public IntegerProperty(object objectWithInt, string propertyName, string resourceId)
        {
            Initialize(objectWithInt, propertyName, resourceId);
            NotifyOnLostFocusOnly = false;
        }
        /// <summary>
        /// Creates an entry that holds the value itself; it is accessed through <see cref="IntegerValue"/>, or through
        /// <see cref="GetIntEvent"/> and <see cref="SetIntEvent"/> when handlers are attached.
        /// </summary>
        /// <param name="initialValue">The initial value of the entry</param>
        /// <param name="resourceId">Resource id of the label text and the tooltip, or null to keep the default</param>
        public IntegerProperty(int initialValue, string resourceId)
        {
            IsSetting = false;
            if (resourceId != null) this.resourceIdInternal = resourceId;
            internalValue = initialValue;
            text = initialValue.ToString();
            NotifyOnLostFocusOnly = false;
        }
        /// <summary>
        /// Creates an entry for a value of the <see cref="Settings"/>: the value is held in <see cref="IntegerValue"/> and saved together
        /// with the setting name.
        /// </summary>
        /// <param name="resourceId">Resource id of the label text and the tooltip</param>
        /// <param name="settingName">The name under which the value is addressed in the settings</param>
        public IntegerProperty(string resourceId, string settingName)
        {
            this.settingName = settingName;
            Initialize(this, "IntegerValue", resourceId);
            NotifyOnLostFocusOnly = false;
        }
        /// <summary>
        /// Defines the range of the value and whether a spin button (up/down arrows) is displayed next to the value. The range is stored
        /// but currently not enforced when a value is entered.
        /// </summary>
        /// <param name="min">Smallest allowed value</param>
        /// <param name="max">Largest allowed value</param>
        /// <param name="showupdown">true to display the spin button</param>
        public void SetMinMax(int min, int max, bool showupdown)
        {
            minValue = min;
            maxValue = max;
            showUpDown = showupdown;
            //numericUpDown.Minimum = min;
            //numericUpDown.Maximum = max;
            //numericUpDown.Increment = 1;
        }
        /// <summary>
        /// Defines a special value that is displayed by a text instead of the number (e.g. "automatic" for 0). All special values are
        /// offered in the context menu of the entry; selecting one sets the value.
        /// </summary>
        /// <param name="val">The value</param>
        /// <param name="resourceId">Resource id of the text displayed for <paramref name="val"/>; a leading "!" of the string resource
        /// is removed</param>
        public void AddSpecialValue(int val, string resourceId)
        {
            if (specialValues == null) specialValues = new Dictionary<int, string>();
            specialValues[val] = resourceId;
        }
        /// <summary>
        /// The value held by the entry itself: it is the value displayed when no object and no <see cref="GetIntEvent"/> is connected, and
        /// the value saved when the entry is a setting. Setting it updates the display but does not notify an owner; use
        /// <see cref="SetInt(int)"/> for that.
        /// </summary>
        public int IntegerValue
        {
            get { return internalValue; }
            set
            {
                internalValue = value;
                SetText(internalValue);
            }
        }
        /// <inheritdoc/>
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
        /// <summary>
        /// Without functionality: was meant to restrict the input to the spin button, always returns false.
        /// </summary>
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
        /// <summary>
        /// Converts the entry to its <see cref="IntegerValue"/>.
        /// </summary>
        /// <param name="ip">The entry</param>
        /// <returns>The value held by the entry</returns>
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
        /// <summary>
        /// Transfers a new value to the owner: raises <see cref="SetIntEvent"/> or, when no handler is attached, sets the reflected property
        /// of the connected object. Does nothing when the value equals the current one. The displayed text is not updated here but by the
        /// following <see cref="Refresh"/> or <see cref="IntChanged"/>. Called when the user edits the value and by the settings.
        /// </summary>
        /// <param name="d">The new value</param>
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
        /// <summary>
        /// Without functionality: set to false by every constructor and never evaluated, the value is transferred with every change.
        /// </summary>
        public bool NotifyOnLostFocusOnly { get; set; }
        private int GetInt()
        {
            if (GetIntEvent != null)
            {
                return GetIntEvent(this);
            }
            else if (TheProperty!=null)
            {
                MethodInfo mi = TheProperty.GetGetMethod();
                object[] prm = new Object[0];
                return (int)mi.Invoke(ObjectWithInt, prm);
            } else
            {
                return internalValue;
            }
        }

        /// <summary>
        /// Must be called by the owner of this property when the integer value has been changed from outside (not by
        /// the user editing this property) to update the displayed value.
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override void StartEdit(bool editValue)
        {
            valueBeforeEdit = internalValue;
        }
        /// <inheritdoc/>
        public override bool EditTextChanged(string newValue)
        {
            using (Frame.Project.Undo.ContextFrame(this))
            {
                return Decode(newValue);
            }
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                string[] menuIDs = new string[specialValues.Count];
                specialValues.Values.CopyTo(menuIDs, 0);
                return MenuResource.CreateContextMenuWithHandler(menuIDs, this);
            }
        }
        // Overrides IShowPropertyImpl.UnSelected()
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("InternalValue", internalValue, internalValue.GetType());
            info.AddValue("resourceId", resourceIdInternal, resourceIdInternal.GetType());
            info.AddValue("SettingName", settingName, typeof(string)); // kann auch null sein
            info.AddValue("SpecialValues", specialValues, typeof(Dictionary<int, string>));
        }
        /// <summary>
        /// Parameterless constructor for the JSON deserialization (<see cref="IJsonSerialize"/>); the state is restored by
        /// <see cref="SetObjectData(IJsonReadData)"/>.
        /// </summary>
        protected IntegerProperty() { } // needed for IJsonSerialize
        /// <summary>
        /// Writes the value, the resource id, the setting name and the special values for the JSON serialization
        /// (<see cref="IJsonSerialize"/>); used when this entry is part of the settings.
        /// </summary>
        /// <param name="data">Receives the data</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("InternalValue", internalValue);
            data.AddProperty("resourceId", resourceIdInternal);
            if (settingName != null) data.AddProperty("SettingName", settingName);
            if (specialValues != null) data.AddProperty("SpecialValues", specialValues);
        }

        /// <summary>
        /// Restores the value, the resource id, the setting name and the special values from the JSON serialization
        /// (<see cref="IJsonSerialize"/>) and registers for the <see cref="IJsonSerializeDone"/> callback, which reconnects the entry to
        /// its own <see cref="IntegerValue"/>.
        /// </summary>
        /// <param name="data">The data to read from</param>
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

        /// <inheritdoc/>
        protected override string ValueToText(int val)
        {
            return val.ToString();
        }

        /// <inheritdoc/>
        protected override bool TextToValue(string input, out int val)
        {
            return int.TryParse(input, out val);
        }

        #endregion
    }
}
