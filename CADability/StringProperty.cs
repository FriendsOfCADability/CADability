using System;
using System.Runtime.Serialization;

namespace CADability.UserInterface
{
    /// <summary>
    /// Property page entry to edit a string, e.g. the name of an object or the text of a
    /// <see cref="CADability.GeoObject.Text"/>. The value is connected to a property of an object via reflection or via
    /// the <see cref="EditableProperty{T}.OnGetValue"/> and <see cref="EditableProperty{T}.OnSetValue"/> delegates; the
    /// text is taken as it is typed.
    /// </summary>
    [Serializable()]
    public class StringProperty : EditableProperty<string>, ISerializable
    {
        /// <summary>
        /// Creates the entry for a string property of <paramref name="ObjectWithProperty"/>, which is accessed via reflection.
        /// </summary>
        /// <param name="ObjectWithProperty">The object holding the property</param>
        /// <param name="PropertyName">Name of the string property of that object</param>
        /// <param name="resourceId">Resource id of the label text</param>
        public StringProperty(object ObjectWithProperty, string PropertyName, string resourceId) : base(ObjectWithProperty, PropertyName, resourceId)
        {
        }

        /// <summary>
        /// Creates the entry with an initial value and no connected object. The entered text can be queried with
        /// <see cref="GetString"/> or received via <see cref="EditableProperty{T}.OnSetValue"/>.
        /// </summary>
        /// <param name="stringValue">The initial text</param>
        /// <param name="resourceId">Resource id of the label text</param>
        public StringProperty(string stringValue, string resourceId)
        {
            base.SetValue(stringValue, false);
            base.resourceIdInternal = resourceId;
        }

        /// <inheritdoc/>
        protected override bool TextToValue(string text, out string val)
        {
            val = text;
            return true;
        }

        /// <inheritdoc/>
        protected override string ValueToText(string val)
        {
            return val;
        }

        /// <summary>
        /// Sets the text, passes it on to the connected object (or <see cref="EditableProperty{T}.OnSetValue"/>) and refreshes
        /// the entry.
        /// </summary>
        /// <param name="val">The new text</param>
        public void SetString(string val)
        {
            SetValue(val, true);
        }
        /// <summary>
        /// Returns the current text, taken from the connected object (or <see cref="EditableProperty{T}.OnGetValue"/>) when
        /// there is one.
        /// </summary>
        /// <returns>The current text</returns>
        public string GetString()
        {
            return GetValue();
        }

        #region deprecated adaption to old implementation of StringProperty
        /// <summary>
        /// Obsolete delegate for <see cref="SetStringEvent"/>; use <see cref="EditableProperty{T}.OnSetValue"/> instead.
        /// </summary>
        /// <param name="sender">The entry</param>
        /// <param name="newValue">The new text</param>
        [Obsolete("use delegate StringProperty.OnSetValue instead")]
        public delegate void SetStringDelegate(StringProperty sender, string newValue);
        /// <summary>
        /// Obsolete delegate for <see cref="GetStringEvent"/>; use <see cref="EditableProperty{T}.OnGetValue"/> instead.
        /// </summary>
        /// <param name="sender">The entry</param>
        /// <returns>The text to display</returns>
        [Obsolete("use delegate StringProperty.OnGetValue instead")]
        public delegate string GetStringDelegate(StringProperty sender);
        /// <summary>
        /// Obsolete: adding a handler sets <see cref="EditableProperty{T}.OnGetValue"/> to call it, removing one clears the
        /// delegate. Only a single handler is supported.
        /// </summary>
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
        /// <summary>
        /// Obsolete: adding a handler sets <see cref="EditableProperty{T}.OnSetValue"/> to call it, removing one clears the
        /// delegate. Only a single handler is supported.
        /// </summary>
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
        /// <summary>
        /// Obsolete: sets <see cref="PropertyEntryImpl.DeferUpdate"/>, i.e. whether the text is passed on only when editing
        /// ends rather than with every keystroke.
        /// </summary>
        [Obsolete("use DeferUpdate instead")]
        public bool NotifyOnLostFocusOnly
        {
            set
            {
                DeferUpdate = value;
            }
        }
        
        /// <summary>
        /// Obsolete delegate for <see cref="StringChangedEvent"/>; use <see cref="EditableProperty{T}.OnSetValue"/> instead.
        /// </summary>
        /// <param name="sender">The entry</param>
        /// <param name="e">Not used</param>
        [Obsolete("use delegate StringProperty.OnSetValue instead")]
        public delegate void StringChangedDelegate(object sender, EventArgs e);
        /// <summary>
        /// Obsolete: adding a handler sets <see cref="EditableProperty{T}.OnSetValue"/> to call it (without the value; query
        /// <see cref="GetString"/>), removing one clears the delegate. Only a single handler is supported.
        /// </summary>
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
        /// <summary>
        /// Constructor for the legacy binary deserialization (<see cref="ISerializable"/>); only the resource id is restored.
        /// Currently only needed by the debugger visualizer.
        /// </summary>
        /// <param name="info">Provides the data</param>
        /// <param name="context">The source of the deserialization</param>
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
    }
}

