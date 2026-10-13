using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace CADability.UserInterface
{
    /// <summary>
    /// Delegate for <see cref="BooleanProperty.BooleanChangedEvent"/>.
    /// </summary>
    /// <param name="sender">The <see cref="BooleanProperty"/> whose value changed</param>
    /// <param name="NewValue">The new value</param>
    public delegate void BooleanChangedDelegate(object sender, bool NewValue);

        /// <summary>
        /// A boolean property entry, which also can be used as a setting (hence serializable)
        /// </summary>
        [Serializable()]
    public class BooleanProperty : MultipleChoiceProperty, ISerializable, IDeserializationCallback, ISettingChanged, IJsonSerialize, IJsonSerializeDone
    {
        private object objectWithProperty;
        private string propertyName;
        private string BooleanTextTrue;
        private string BooleanTextFalse;
        private string resourceIdValues;
        private String settingName; // wenn in einem Setting verwendet, so ist hier der Name des Wertes
        private bool internalValue; // der boolen Wert selbst, wenn Setting

        private void Initialize(object ObjectWithProperty, string PropertyName, string resourceIdLabel, string resourceIdValues)
        {
            // beim deserialisieren sind die Werte resourceIdLabel und resourceIdValues bereits gesetzt
            if (resourceIdLabel != null) this.resourceIdInternal = resourceIdLabel;
            if (resourceIdValues != null) this.resourceIdValues = resourceIdValues;
            objectWithProperty = ObjectWithProperty;
            propertyName = PropertyName;
            propertyLabelText = StringTable.GetString(this.resourceIdInternal);
            string BooleanText = StringTable.GetString(this.resourceIdValues);
            // mit folgendem konnte geklärt werden, wer frühzeitig die global settings läd
            // so dass eine Anwendung keine chance hat vorher die StringTable zu laden.
            //if (!StringTable.IsStringDefined(this.resourceIdValues))
            //{
            //    MessageBox.Show(System.Environment.StackTrace, "BooleanProperty");
            //}
            if (BooleanText != null && BooleanText.Length > 0)
            {   // erwartet wird ein String mit beiden Werten, wobei das erste Zeichen 
                // das Trennzeichen ist, also z.B. "|wahr|falsch"
                char sep = BooleanText[0];
                string[] split = BooleanText.Split(new char[] { sep });
                if (split.Length > 2)
                {	// das erste Stück ist leer
                    BooleanTextTrue = split[1];
                    BooleanTextFalse = split[2];
                }
            }
            if (BooleanTextTrue == null)
            {
                BooleanTextTrue = true.ToString();
                BooleanTextFalse = false.ToString();
            }
            choices = new string[] { BooleanTextTrue, BooleanTextFalse };

            PropertyInfo propertyInfo = objectWithProperty.GetType().GetProperty(propertyName);
            MethodInfo mi = propertyInfo.GetGetMethod();
            object[] prm = new Object[0];
            bool val = (bool)mi.Invoke(objectWithProperty, prm);
            if (val) SelectedText = BooleanTextTrue;
            else SelectedText = BooleanTextFalse;
        }
        /// <summary>
        /// Delegate for <see cref="SetBooleanEvent"/>.
        /// </summary>
        /// <param name="val">The value selected by the user</param>
        public delegate void SetBooleanDelegate(bool val);
        /// <summary>
        /// Delegate for <see cref="GetBooleanEvent"/>.
        /// </summary>
        /// <returns>The current value to display</returns>
        public delegate bool GetBooleanDelegate();
        /// <summary>
        /// Raised when the user selects a value. When a handler is attached, it receives the new value instead of the
        /// property of the object given to the constructor being set.
        /// </summary>
        public event SetBooleanDelegate SetBooleanEvent;
        /// <summary>
        /// Queried whenever the entry is displayed. When a handler is attached, its result is shown instead of the value of
        /// the property of the object given to the constructor.
        /// </summary>
        public event GetBooleanDelegate GetBooleanEvent;
        /// <summary>
        /// Creates a BooleanProperty that displays and manipulates the boolean property of another object.
        /// </summary>
        /// <param name="ObjectWithProperty">The object that holds the boolean property</param>
        /// <param name="PropertyName">Name of the property (it is accessed via reflection)</param>
        /// <param name="resourceIdLabel">Resource id of the label and its tooltips. The tooltip texts are looked up under this
        /// resource id followed by ".ShortInfo" and ".DetailedInfo" respectively; ".Images" gives the name of an image list
        /// in the resources and the indices into it</param>
        /// <param name="resourceIdValues">Resource id of the texts for true and false. The text must start with a separator
        /// character, followed by the text for true, the separator again and the text for false, e.g. "|yes|no"</param>
        public BooleanProperty(object ObjectWithProperty, string PropertyName, string resourceIdLabel, string resourceIdValues)
        {
            Initialize(ObjectWithProperty, PropertyName, resourceIdLabel, resourceIdValues);
        }
        /// <summary>
        /// Creates a BooleanProperty that displays and manipulates the boolean property of another object. The texts for true
        /// and false are looked up under <paramref name="resourceId"/> followed by ".Values".
        /// </summary>
        /// <param name="ObjectWithProperty">The object that holds the boolean property</param>
        /// <param name="PropertyName">Name of the property (it is accessed via reflection)</param>
        /// <param name="resourceId">Resource id of the label; the texts for true and false are found under resourceId + ".Values"</param>
        public BooleanProperty(object ObjectWithProperty, string PropertyName, string resourceId)
        {
            Initialize(ObjectWithProperty, PropertyName, resourceId, resourceId + ".Values");
        }
        /// <summary>
        /// Creates a BooleanProperty which holds its own value and is not connected to another object. Use
        /// <see cref="GetBooleanEvent"/> and <see cref="SetBooleanEvent"/> or <see cref="BooleanValue"/> to communicate with it.
        /// </summary>
        /// <param name="resourceIdLabel">Resource id of the label; the text is found under resourceIdLabel + ".Label"</param>
        /// <param name="resourceIdValues">Resource id of the texts for true and false. The text must start with a separator
        /// character, followed by the text for true, the separator again and the text for false, e.g. "|yes|no"</param>
        public BooleanProperty(string resourceIdLabel, string resourceIdValues)
        {
            this.resourceIdInternal = resourceIdLabel;
            this.resourceIdValues = resourceIdValues;
            string BooleanText = StringTable.GetString(this.resourceIdValues);
            if (BooleanText != null && BooleanText.Length > 0)
            {   // erwartet wird ein String mit beiden Werten, wobei das erste Zeichen 
                // das Trennzeichen ist, also z.B. "|wahr|falsch"
                char sep = BooleanText[0];
                string[] split = BooleanText.Split(new char[] { sep });
                if (split.Length > 2)
                {   // das erste Stück ist leer
                    BooleanTextTrue = split[1];
                    BooleanTextFalse = split[2];
                }
            }
            if (BooleanTextTrue == null)
            {
                BooleanTextTrue = true.ToString();
                BooleanTextFalse = false.ToString();
            }
            base.choices = new string[] { BooleanTextTrue, BooleanTextFalse };
            base.propertyLabelText = StringTable.GetString(resourceIdInternal + ".Label");
            SelectedText = BooleanTextTrue;
        }
        /// <summary>
        /// Creates a BooleanProperty, which contains its own boolean value and can display
        /// and manipulate it. The value is available via the property BooleanValue.
        /// </summary>
        /// <param name="resourceIdLabel">ResourceId for the label and its tooltips. The
        /// texts for the tooltips are looked up under this ResourceId followed by ".ShortInfo" or ".DetailedInfo"
        /// respectively. Under ".Images" the name and the indices of an ImageList
        /// in the resource are found</param>
        /// <param name="resourceIdValues">ResourceId for the texts for true and false. The text must
        /// start with a separator character, followed by the text for true, followed by the separator,
        /// followed by the text for false, e.g. "|yes|no"</param>
        /// <param name="settingName">Name of the value when this property is used in a setting. It is passed
        /// as the name in the <see cref="SettingChangedEvent"/></param>
        public BooleanProperty(string resourceIdLabel, string resourceIdValues, string settingName)
        {
            this.settingName = settingName;
            Initialize(this, "BooleanValue", resourceIdLabel, resourceIdValues);
        }
        /// <summary>
        /// The boolean value held by this entry. When the entry is connected to an object or to <see cref="GetBooleanEvent"/>,
        /// it is updated from there whenever the entry is displayed. Setting it updates the displayed text and raises
        /// <see cref="MultipleChoiceProperty.SelectionChangedEvent"/> and <see cref="MultipleChoiceProperty.ValueChangedEvent"/>,
        /// but does not write the value to the connected object.
        /// </summary>
        public bool BooleanValue
        {
            get { return internalValue; }
            set
            {
                internalValue = value;
                if (value)
                    base.OnSelectionChanged(BooleanTextTrue);
                else
                    base.OnSelectionChanged(BooleanTextFalse);
            }
        }
        /// <summary>
        /// Converts the property to the value it holds internally (<see cref="BooleanValue"/>).
        /// </summary>
        /// <param name="bp">The property</param>
        /// <returns>The boolean value</returns>
        public static explicit operator bool(BooleanProperty bp) { return bp.internalValue; }
        //public void SetImages(ImageList ImageList, int IndexTrue, int IndexFalse)
        //{
        //    images = ImageList;
        //    imageIndices = new int[2];
        //    imageIndices[0] = IndexTrue;
        //    imageIndices[1] = IndexFalse;
        //}
        /// <inheritdoc/>
        protected override void OnSelectionChanged(string selected)
        {
            internalValue = selected == BooleanTextTrue;
            if (SetBooleanEvent != null)
            {
                SetBooleanEvent(internalValue);
            }
            else if (objectWithProperty != null)
            {
                PropertyInfo propertyInfo = objectWithProperty.GetType().GetProperty(propertyName);
                MethodInfo mi = propertyInfo.GetSetMethod();
                object[] prm = new object[1];
                prm[0] = internalValue;
                mi.Invoke(objectWithProperty, prm);
            }
            base.OnSelectionChanged(selected);
            if (SettingChangedEvent != null) SettingChangedEvent(settingName, this);
            if (BooleanChangedEvent != null) BooleanChangedEvent(this, internalValue);
        }
        /// <inheritdoc/>
        public override string Value
        {
            get
            {
                if (GetBooleanEvent != null)
                {
                    BooleanValue = GetBooleanEvent();
                }
                else if (objectWithProperty != null)
                {
                    PropertyInfo propertyInfo = objectWithProperty.GetType().GetProperty(propertyName);
                    MethodInfo mi = propertyInfo.GetGetMethod();
                    object[] prm = new Object[0];
                    BooleanValue = (bool)mi.Invoke(objectWithProperty, prm);
                }
                if (BooleanValue) return BooleanTextTrue;
                else return BooleanTextFalse;
            }
        }
        /// <summary>
        /// Raised after the user selected a value and it was passed on to the connected object or to
        /// <see cref="SetBooleanEvent"/>.
        /// </summary>
        public event BooleanChangedDelegate BooleanChangedEvent;
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected BooleanProperty(SerializationInfo info, StreamingContext context)
        {
            internalValue = (bool)info.GetValue("InternalValue", typeof(bool));
            resourceIdInternal = (string)info.GetValue("ResourceIdLabel", typeof(string));
            resourceIdValues = (string)info.GetValue("ResourceIdValues", typeof(string));
            settingName = (string)info.GetValue("SettingName", typeof(string));
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
            info.AddValue("ResourceIdLabel", resourceIdInternal, resourceIdInternal.GetType());
            info.AddValue("ResourceIdValues", resourceIdValues, resourceIdValues.GetType());
            info.AddValue("SettingName", settingName, settingName.GetType());
        }
        /// <summary>
        /// Writes the value, the resource ids and the setting name for the JSON serialization (<see cref="IJsonSerialize"/>).
        /// Only properties which hold their own value (see the constructor with the setting name) are serialized.
        /// </summary>
        /// <param name="data">Receives the data</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("InternalValue", internalValue);
            data.AddProperty("ResourceIdLabel", resourceIdInternal);
            data.AddProperty("ResourceIdValues", resourceIdValues);
            data.AddProperty("SettingName", settingName);
        }
        /// <summary>
        /// Empty constructor for the JSON deserialization; the data is set by <see cref="SetObjectData(IJsonReadData)"/>.
        /// </summary>
        protected BooleanProperty() { } // empty constructor for Json
        /// <summary>
        /// Reads the data written by <see cref="GetObjectData(IJsonWriteData)"/>. The texts are initialized from the resource
        /// ids once the deserialization is complete.
        /// </summary>
        /// <param name="data">Provides the data</param>
        public void SetObjectData(IJsonReadData data)
        {
            internalValue = data.GetProperty<bool>("InternalValue");
            resourceIdInternal = data.GetProperty<string>("ResourceIdLabel");
            resourceIdValues = data.GetProperty<string>("ResourceIdValues");
            settingName = data.GetProperty<string>("SettingName");
            data.RegisterForSerializationDoneCallback(this);
        }

        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            // es werden nur solche Objekte deserialisiert, die ihren boolen Wert selbst
            // verwalten. 
            this.Initialize(this, "BooleanValue", null, null);
        }

        void IJsonSerializeDone.SerializationDone(JsonSerialize jsonSerialize)
        {
            bool val = internalValue;
            this.Initialize(this, "BooleanValue", null, null);
            internalValue = val;
        }

        #endregion
        #region ISettingChanged Members

        /// <summary>
        /// Raised when the user changes the value while the property is used as a setting (implements
        /// <see cref="ISettingChanged"/>). The handler receives the setting name given to the constructor and this property.
        /// </summary>
        public event CADability.SettingChangedDelegate SettingChangedEvent;

        #endregion
    }
}
