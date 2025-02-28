using CADability.UserInterface;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
#else
using System.Drawing;
#endif
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// ColorSetting is intended for objects to be used in <see cref="Settings"/>. It provides a <see cref="Color"/>
    /// with the <see cref="IShowProperty"/> interface.
    /// </summary>
    [Serializable()]
    public class ColorSetting : IShowPropertyImpl, ISerializable, ISettingChanged
    {
        private Color color;
        private string settingName;
        public ColorSetting()
        {
        }
        public ColorSetting(string settingName, string resourceId)
        {
            this.settingName = settingName;
            this.resourceIdInternal = resourceId;
        }
        public Color Color
        {
            get { return color; }
            set
            {
                color = value;
                if (SettingChangedEvent != null) SettingChangedEvent(settingName, this);
            }
        }
        public static implicit operator Color(ColorSetting cs)
        {
            return cs.color;
        }
        #region IShowPropertyImpl Overrides
        public override string Label { get => StringTable.GetString(resourceIdInternal); }
        public override string Value
        {
            get
            {
                return "[[ColorBox:" + Color.R.ToString() + ":" + Color.G.ToString() + ":" + Color.B.ToString() + "]]" + Color.Name;
            }

        }
        public override PropertyEntryType Flags => PropertyEntryType.Selectable|PropertyEntryType.ValueAsButton;
        public override void ButtonClicked(PropertyEntryButton button)
        {
            Color clr = Color;
            if (Frame.UIService.ShowColorDialog(ref clr)==Substitutes.DialogResult.OK)
            {
                Color = clr;
                propertyPage.Refresh(this);
            }
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected ColorSetting(SerializationInfo info, StreamingContext context)
        {
            try
            {
                color = (Color)info.GetValue("Color", typeof(Color));
                object dbg = info.GetValue("Color", typeof(object));
            }
            catch (Exception)
            {
                color = Color.Black;
            }
            resourceIdInternal = (string)info.GetValue("ResourceIdLabel", typeof(string));
            settingName = (string)info.GetValue("SettingName", typeof(string));
        }

        #region ISettingChanged Members
        private event CADability.SettingChangedDelegate SettingChangedEvent;
        #endregion
        event SettingChangedDelegate ISettingChanged.SettingChangedEvent
        {
            add
            {
                SettingChangedEvent += value;
            }

            remove
            {
                SettingChangedEvent -= value;
            }
        }

        //[OnDeserializing()]
        //internal void OnDeserializingMethod(StreamingContext context)
        //{
        //    System.Diagnostics.Trace.WriteLine("OnDeserializingMethod: " + this.GetType().ToString());
        //}

        //[OnDeserialized()]
        //internal void OnDeserializedMethod(StreamingContext context)
        //{
        //    System.Diagnostics.Trace.WriteLine("OnDeserializedMethod: " + this.GetType().ToString());
        //}

        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Color", color, color.GetType());
            info.AddValue("ResourceIdLabel", resourceIdInternal, resourceIdInternal.GetType());
            info.AddValue("SettingName", settingName, settingName.GetType());
        }
        #endregion
    }
}
