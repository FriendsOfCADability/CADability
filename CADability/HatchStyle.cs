using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Runtime.Serialization;


namespace CADability.Attribute
{

    /// <summary>
    /// Implemented by objects that carry a <see cref="HatchStyle"/> attribute, in CADability the <see cref="Hatch"/>
    /// object and the <see cref="Style"/>. The <see cref="HatchStyleList"/>, the <see cref="Style"/> and the
    /// <see cref="Filter"/> use this interface to read and replace the hatch style of an object without knowing its type.
    /// </summary>
    public interface IHatchStyle
    {
        /// <summary>
        /// The hatch style of the object, or null when none is set. For a <see cref="Hatch"/> setting it is an undoable
        /// change that raises the change events and causes the content of the hatch to be regenerated.
        /// </summary>
        HatchStyle HatchStyle { get; set; }
    }
    /// <summary>
    /// Abstract base class for all hatchstyles. A hatchstyle is used to define the
    /// interior of a <see cref="Hatch"/> object.
    /// </summary>
    [Serializable()]
    public abstract class HatchStyle : PropertyEntryImpl, ISerializable, INamedAttribute, ICommandHandler
    {
        private string name;
        private HatchStyleList parent;
        /// <summary>
        /// The <see cref="HatchStyleList"/> that contains this hatch style, or null when it is not in a list. The list
        /// sets it when the style is added. Assigning a list of another type stores null. Hides the untyped
        /// <see cref="PropertyEntryImpl.Parent"/>.
        /// </summary>
        public new IAttributeList Parent
        {
            set { parent = value as HatchStyleList; }
            get { return parent; }
        }
        /// <summary>
        /// Notifies the containing <see cref="HatchStyleList"/> (and through it the <see cref="Project"/> or the
        /// <see cref="Settings"/>) that a property of this hatch style has been changed, so that the change can be
        /// undone and the hatches using this style can be updated. Derived classes call it after they changed a
        /// property. Does nothing when the style is not in a list.
        /// </summary>
        /// <param name="propertyName">name of the public property that was changed, used by the undo mechanism to restore the old value</param>
        /// <param name="propertyOldValue">the value of the property before the change</param>
        protected void FireDidChange(string propertyName, object propertyOldValue)
        {
            if (parent != null)
            {
                ReversibleChange change = new ReversibleChange(this, propertyName, propertyOldValue);
                (parent as IAttributeList).AttributeChanged(this, change);
            }
        }
        internal virtual void Init(Project pr)
        {   // ein neuer Hatchstyle soll initialisiert werden, damit vernünftige Werte drinstehen
            // leere default implementierung
        }
        /// <summary>
        /// Creates a hatch style without a name that is not contained in a list. Only called by the constructors of
        /// the derived classes.
        /// </summary>
        public HatchStyle()
        {
            //
            // TODO: Add constructor logic here
            //
        }
        /// <summary>
        /// Gets or sets the name of the HatchStyle
        /// </summary>
        public string Name
        {
            get { return name; }
            set
            {
                if (parent != null && !(parent as IAttributeList).MayChangeName(this, value))
                {
                    throw new NameAlreadyExistsException(parent, this, value, name);
                }
                string OldName = name;
                name = value;
                FireDidChange("Name", OldName);
                if (parent != null) (parent as IAttributeList).NameChanged(this, OldName);
            }
        }
        IPropertyEntry INamedAttribute.GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList)
        {
            return null;
        }
        /// <summary>
		/// Generates the lines or other GeoObjects that fill out the given shape
		/// in the given plane. Derived classes must override this method.
		/// Default implementation returns null.
		/// </summary>
		/// <param name="shape">The shape to be filled</param>
		/// <param name="plane">The plane of the shape</param>
		/// <returns>The resulting GeoObjects or null</returns>
		public virtual GeoObjectList GenerateContent(CompoundShape shape, Plane plane)
        {
            return null;
        }
        /// <summary>
        /// Returns an object to display the properties of the HatchStyle
        /// </summary>
        /// <returns></returns>
        public abstract IShowProperty GetShowProperty();
        /// <summary>
        /// Creates a copy of this hatch style of the same type, with the same name and the same settings. The copy
        /// refers to the same colour, line width and line pattern objects and is not contained in a list. Used when a
        /// <see cref="HatchStyleList"/> is cloned and when hatch styles are copied between the project and the global settings.
        /// </summary>
        /// <returns>the copy</returns>
        public abstract HatchStyle Clone();
        internal abstract void Update(bool AddMissingToList);
        #region IShowPropertyImpl overrides
        /// <inheritdoc/>
        public override string LabelText
        {
            get
            {
                return Name;
            }
        }
        /// <inheritdoc/>
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType flags = PropertyEntryType.GroupTitle | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.LabelEditable | PropertyEntryType.HasSubEntries;
                if ((parent != null) && (parent.Current == this))
                    flags |= PropertyEntryType.Bold;
                return flags;
            }
        }
        /// <inheritdoc/>
        public override bool EditTextChanged(string newValue)
        {
            return true;
        }
        /// <inheritdoc/>
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            if (!aborted)
            {
                try
                {
                    Name = newValue;
                }
                catch (NameAlreadyExistsException) { }
            }
        }
        /// <inheritdoc/>
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.HatchStyleEntry", false, this);
            }
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected HatchStyle(SerializationInfo info, StreamingContext context)
        {
            name = (string)info.GetValue("Name", typeof(string));
        }
        /// <summary>
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Name", name);
        }
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.HatchStyleListEntry.Delete":
                    {
                        HatchStyleList hsl = parent;
                        if (hsl != null)
                        {
                            hsl.Remove(this);
                            if (propertyPage != null) propertyPage.Refresh(hsl);
                        }
                        return true;
                    }
                case "MenuId.HatchStyleListEntry.Edit":
                    propertyPage.StartEditLabel(this);
                    return true;
                case "MenuId.HatchStyleListEntry.Current":
                    {
                        var dbg = propertyPage.GetParent(this);
                        HatchStyleList hsl = parent; //  propertyPage.GetParent(this) as HatchStyleList;
                        if (hsl != null)
                        {
                            hsl.Current = this;
                            propertyPage.Refresh(hsl);
                        }
                        return true;
                    }
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add HatchStyle.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }
}
