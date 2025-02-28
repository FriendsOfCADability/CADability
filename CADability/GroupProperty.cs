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
        public GroupProperty(string resourceId, IPropertyEntry[] subEntries)
        {
            this.resourceIdInternal = resourceId;
            this.subEntries = subEntries;
            flags = PropertyEntryType.GroupTitle | PropertyEntryType.Selectable;
            if (subEntries.Length > 0) flags |= PropertyEntryType.HasSubEntries;
        }
        public void SetSubEntries(IPropertyEntry[] subEntries)
        {
            if (subEntries.Length > 0) flags |= PropertyEntryType.HasSubEntries;
            this.subEntries = subEntries;
            propertyPage?.Refresh(this);
        }
        #region IShowProperty Members

        public override PropertyEntryType Flags
        {
            get { return flags; }
        }
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
