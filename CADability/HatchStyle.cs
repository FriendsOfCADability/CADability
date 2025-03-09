using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Runtime.Serialization;


namespace CADability.Attribute
{

    public interface IHatchStyle
    {
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
        public new IAttributeList Parent
        {
            set { parent = value as HatchStyleList; }
            get { return parent; }
        }
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
		/// in the given plane. If a HatchStyle returns null, it must implement the
		/// <see cref="Paint"/> and the <see cref="HitTest"/> methods.
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
        public abstract HatchStyle Clone();
        internal abstract void Update(bool AddMissingToList);
        #region IShowPropertyImpl overrides
        public override string LabelText
        {
            get
            {
                return Name;
            }
        }
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
        public override bool EditTextChanged(string newValue)
        {
            return true;
        }
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
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
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
