using System.Collections;
using System.Collections.Generic;

namespace CADability.UserInterface
{
    /// <summary>
    /// A simple container for several <see cref="IPropertyEntry"/> entries in the
    /// treeview of the controlcenter. Add subentries to this group before the group
    /// ist displayed in the treeview. If you add or remove subentries while the
    /// group is displayed you will have to call <see cref="IPropertyTreeView.Refresh"/>.
    /// </summary>

    public class SimplePropertyGroup : PropertyEntryImpl
    {
        private List<IPropertyEntry> subentries;
        public SimplePropertyGroup(string resourceId)
        {
            this.resourceIdInternal = resourceId;
            subentries = new List<IPropertyEntry>();
        }
        public void Add(IPropertyEntry subEntry)
        {
            if (subEntry!=null) subentries.Add(subEntry);
        }
        public void Add(IPropertyEntry[] subEntries)
        {
            this.subentries.AddRange(subEntries);
        }
        public void Add(IEnumerable<IPropertyEntry> subEntries)
        {
            this.subentries.AddRange(subEntries);
        }
        public void Remove(IPropertyEntry subEntry)
        {
            subentries.Remove(subEntry);
        }
        public void RemoveAll()
        {
            subentries.Clear();
        }
        #region IPropertyEntry Members
        public override PropertyEntryType Flags => PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                return subentries.ToArray();
            }
        }
        #endregion
    }
}
