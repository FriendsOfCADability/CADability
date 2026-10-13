namespace CADability.UserInterface
{
    /// <summary>
    /// A simple entry for the showproperty tree, wich is represented by a GruopTitle
    /// eintry which contains some subentries. A folder in the treeview of the controlcenter.
    /// The subentries mus be specified in the constructur.
    /// </summary>
    public class GroupProperty : PropertyEntryImpl
    {
        private IPropertyEntry[] subEntries;
        PropertyEntryType flags;
        /// <summary>
        /// Creates the group with its sub entries.
        /// </summary>
        /// <param name="resourceId">Resource id of the title text</param>
        /// <param name="subEntries">The entries shown below the title; may be empty</param>
        public GroupProperty(string resourceId, IPropertyEntry[] subEntries)
        {
            this.resourceIdInternal = resourceId;
            this.subEntries = subEntries;
            flags = PropertyEntryType.GroupTitle | PropertyEntryType.Selectable;
            if (subEntries.Length > 0) flags |= PropertyEntryType.HasSubEntries;
        }
        /// <summary>
        /// Replaces the sub entries and refreshes the display when the group is shown on a property page. Used e.g. by
        /// <see cref="CADability.GeoObject.Shell"/> to update the list of its parametric properties.
        /// </summary>
        /// <param name="subEntries">The new sub entries</param>
        public void SetSubEntries(IPropertyEntry[] subEntries)
        {
            if (subEntries.Length > 0) flags |= PropertyEntryType.HasSubEntries;
            this.subEntries = subEntries;
            propertyPage?.Refresh(this);
        }
        #region IShowProperty Members

        /// <inheritdoc/>
        public override PropertyEntryType Flags
        {
            get { return flags; }
        }
        /// <summary>
        /// Replaces the <see cref="Flags"/> of the entry, e.g. to add <see cref="PropertyEntryType.LabelEditable"/> so that
        /// the user can rename the group.
        /// </summary>
        /// <param name="flags">The new flags; they should keep <see cref="PropertyEntryType.GroupTitle"/> and
        /// <see cref="PropertyEntryType.HasSubEntries"/></param>
        public void SetFlags(PropertyEntryType flags)
        {
            this.flags = flags;
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>, 
        /// returns the number of subentries in this property view.
        /// </summary>
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                return subEntries;
            }
        }
        #endregion
    }
}
