using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace CADability.UserInterface
{
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
        public delegate void SetBooleanDelegate(bool val);
        public delegate bool GetBooleanDelegate();
        public event SetBooleanDelegate SetBooleanEvent;
        public event GetBooleanDelegate GetBooleanEvent;
        /// <summary>
        /// Erzeugt ein BooleanProperty Objekt, welches die boolean Property eines anderen
        /// Objektes darstellt und manipuliert.
        /// </summary>
        /// <param name="ObjectWithProperty">das Objekt, welches die boolean Property enthält</param>
        /// <param name="PropertyName">der Name der Property (wird für Reflection verwendet)</param>
        /// <param name="resourceIdLabel">ResourceId für den Label und dessen ToolTips. Die
        /// Texte für die ToolTips werden unter dieser ResourceId gefolgt von ".ShortInfo" bzw. ".DetailedInfo"
        /// gesucht. Unter ".Images" findet sich der Name und die Indizes einer ImageList
        /// in der Resource</param>
        /// <param name="resourceIdValues">ResourceId für die Texte für true bzw. false. Der Text muss
        /// mit einem Trennzeichen beginnen, gefolgt von dem Text für true, gefolgt von dem Trennzeichen,
        /// gefolgt von dem Wert für false, z.B. "|ja|nein"</param>
        public BooleanProperty(object ObjectWithProperty, string PropertyName, string resourceIdLabel, string resourceIdValues)
        {
            Initialize(ObjectWithProperty, PropertyName, resourceIdLabel, resourceIdValues);
        }
        public BooleanProperty(object ObjectWithProperty, string PropertyName, string resourceId)
        {
            Initialize(ObjectWithProperty, PropertyName, resourceId, resourceId + ".Values");
        }
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
        /// Erzeugt eine BooleanProperty, welches einen eigenen boolean Wert enthält und
        /// diesen darstellen und manipulieren kann. Der Wert ist über die Property BooleanValue
        /// verfügbar.
        /// </summary>
        /// <param name="resourceIdLabel">ResourceId für den Label und dessen ToolTips. Die
        /// Texte für die ToolTips werden unter dieser ResourceId gefolgt von ".ShortInfo" bzw. ".DetailedInfo"
        /// gesucht. Unter ".Images" findet sich der Name und die Indizes einer ImageList
        /// in der Resource</param>
        /// <param name="resourceIdValues">ResourceId für die Texte für true bzw. false. Der Text muss
        /// mit einem Trennzeichen beginnen, gefolgt von dem Text für true, gefolgt von dem Trennzeichen,
        /// gefolgt von dem Wert für false, z.B. "|ja|nein"</param>
        public BooleanProperty(string resourceIdLabel, string resourceIdValues, string settingName)
        {
            this.settingName = settingName;
            Initialize(this, "BooleanValue", resourceIdLabel, resourceIdValues);
        }
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
        public static explicit operator bool(BooleanProperty bp) { return bp.internalValue; }
        //public void SetImages(ImageList ImageList, int IndexTrue, int IndexFalse)
        //{
        //    images = ImageList;
        //    imageIndices = new int[2];
        //    imageIndices[0] = IndexTrue;
        //    imageIndices[1] = IndexFalse;
        //}
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
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("InternalValue", internalValue, internalValue.GetType());
            info.AddValue("ResourceIdLabel", resourceIdInternal, resourceIdInternal.GetType());
            info.AddValue("ResourceIdValues", resourceIdValues, resourceIdValues.GetType());
            info.AddValue("SettingName", settingName, settingName.GetType());
        }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("InternalValue", internalValue);
            data.AddProperty("ResourceIdLabel", resourceIdInternal);
            data.AddProperty("ResourceIdValues", resourceIdValues);
            data.AddProperty("SettingName", settingName);
        }
        protected BooleanProperty() { } // empty constructor for Json
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

        public event CADability.SettingChangedDelegate SettingChangedEvent;

        #endregion
    }
}
