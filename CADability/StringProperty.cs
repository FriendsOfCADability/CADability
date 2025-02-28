using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;

namespace CADability.UserInterface
{
    [Serializable()]
    public class StringProperty : EditableProperty<string>, ISerializable, ISettingChanged
    {
        public StringProperty(object ObjectWithProperty, string PropertyName, string resourceId) : base(ObjectWithProperty, PropertyName, resourceId)
        {
        }

        public StringProperty(string stringValue, string resourceId)
        {
            base.SetValue(stringValue, false);
            base.resourceIdInternal = resourceId;
        }

        protected override bool TextToValue(string text, out string val)
        {
            val = text;
            return true;
        }

        protected override string ValueToText(string val)
        {
            return val;
        }

        #region deprecated adaption to old implementation of StringProperty
        [Obsolete("use delegate StringProperty.OnSetValue instead")]
        public delegate void SetStringDelegate(StringProperty sender, string newValue);
        [Obsolete("use delegate StringProperty.OnGetValue instead")]
        public delegate string GetStringDelegate(StringProperty sender);
        [Obsolete("use delegate StringProperty.OnGetValue instead")]
        public event GetStringDelegate GetStringEvent
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
        [Obsolete("use delegate StringProperty.OnSetValue instead")]
        public event SetStringDelegate SetStringEvent
        {
            add
            {
                base.OnSetValue = delegate (string l) { value(this, l); };
            }
            remove
            {
                base.OnSetValue = null;
            }
        }
        [Obsolete("use DeferUpdate instead")]
        public bool NotifyOnLostFocusOnly
        {
            set
            {
                DeferUpdate = value;
            }
        }
        [Obsolete("use SetValue instead")]
        public void SetString(string val)
        {
            SetValue(val, true);
        }
        [Obsolete("use GetValue instead")]
        public string GetString()
        {
            return GetValue();
        }
        [Obsolete("use delegate StringProperty.OnSetValue instead")]
        public delegate void StringChangedDelegate(object sender, EventArgs e);
        [Obsolete("use delegate StringProperty.OnSetValue instead")]
        public event StringChangedDelegate StringChangedEvent
        {
            add
            {
                base.OnSetValue = delegate (string l) { value(this, new EventArgs()); };
            }
            remove
            {
                base.OnSetValue = null;
            }
        }
        #endregion

        #region ISerializable Members
        protected StringProperty(SerializationInfo info, StreamingContext context)
        {   // wird z.Z. nur für DebuggerVisualizer gebraucht
            base.resourceIdInternal = (string)info.GetValue("ResourceId", typeof(string));
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("InternalValue", GetValue(), typeof(string));
            info.AddValue("ResourceId", resourceIdInternal, typeof(string));
        }
        #endregion

        #region ISettingChanged Members
        public event CADability.SettingChangedDelegate SettingChangedEvent;
        #endregion
    }
}

