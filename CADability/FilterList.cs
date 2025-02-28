using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;


namespace CADability.Attribute
{
    /// <summary>
    /// A FilterList is a list of <see cref="Filter"/>s. The <see cref="Project"/> owns a FilterList which is used
    /// by the <see cref="CADAbility.Actions.SelectObjectsAction"/> to decide which <see cref="IGeoObject"/>s may be selected or must be rejected.
    /// You may add a handler to the <see cref="PreFilterEvent"/> event to do your own filtering or use the
    /// default filtering machanism (or both). The FilterList of the project is displayed in the ControlCenter
    /// and may be interactively manipulated.
    /// </summary>
    [Serializable]
    public class FilterList : IShowPropertyImpl, ISerializable, ICommandHandler, INameChange, IDeserializationCallback
    {
        private Filter[] unsortedEntries;
        private SortedList entries; // string(Name) -> Filter
        private IAttributeListContainer owner; // Besitzer, entweder Projekt oder Settings
                                               /// <summary>
                                               /// Constructs a empty FilterList
                                               /// </summary>
        public FilterList()
        {
            entries = new SortedList();
            resourceIdInternal = "FilterList";
            propertyPages = new List<IPropertyPage>();
        }
        /// <summary>
        /// Remove the provided <see cref="Filter"/> from the list
        /// </summary>
        /// <param name="f">Filter to remove</param>
		public void Remove(Filter f)
        {
            for (int i = 0; i < propertyPages.Count; i++) if (propertyPages[i].IsOnTop()) propertyPages[i].OpenSubEntries(this, false);
            entries.Remove(f.Name);
            subEntries = null;
            for (int i = 0; i < propertyPages.Count; i++)
            {
                if (propertyPages[i].IsOnTop())
                {
                    propertyPages[i].Refresh(this);
                    propertyPages[i].OpenSubEntries(this, true);
                }
            }
        }
        /// <summary>
        /// Add the provided <see cref="Filter"/> to the list
        /// </summary>
        /// <param name="f">Filter to add</param>
		public void Add(Filter f)
        {
            entries[f.Name] = f;
            f.Parent = this;
            subEntries = null;
            for (int i = 0; i < propertyPages.Count; i++)
            {
                if (propertyPages[i].IsOnTop())
                {
                    propertyPages[i].OpenSubEntries(this, false);
                    propertyPages[i].Refresh(this);
                    propertyPages[i].OpenSubEntries(this, true);
                }
            }
        }
        /// <summary>
        /// Use all active filters in this list to check whether an object is accepted or not.
        /// If there is a <see cref="PreFilterEvent"/> the this event will be called.
        /// </summary>
        /// <param name="go">Object to check</param>
        /// <returns>true: accepted, false: rejected</returns>
		public bool Accept(IGeoObject go)
        {
            bool accepted = true;
            bool doInternalFiltering = true;
            if (PreFilterEvent != null) PreFilterEvent(this, go, out accepted, out doInternalFiltering);
            if (!accepted) return false;
            if (!doInternalFiltering) return accepted;
            foreach (DictionaryEntry de in entries)
            {
                Filter f = de.Value as Filter;
                if (f.IsActive)
                {
                    if (!f.Accept(go)) return false;
                }
            }
            return true;
        }
        /// <summary>
        /// Indexer to access individual <see cref="Filter"/>s by index
        /// </summary>
        /// <param name="index">index of the required Filter</param>
        /// <returns>The Filter</returns>
        public Filter this[int index]
        {
            get
            {
                return (Filter)entries.GetByIndex(index);
            }
        }
        public Filter FindFilter(string name)
        {
            foreach (DictionaryEntry de in entries)
            {
                if ((de.Key as string) == name) return de.Value as Filter;
            }
            return null;
        }
        /// <summary>
        /// Returns the number of <see cref="Filter"/>s in this list.
        /// </summary>
        public int Count
        {
            get
            {
                return entries.Count;
            }
        }
        /// <summary>
        /// Delegate definition for an FilterList event to allow pre-filtering of acceptance of IGeoObject objects.
        /// </summary>
        /// <param name="filterList">Filterlist, which issues the event</param>
        /// <param name="go">the object to check</param>
        /// <param name="accepted">Set true to accept, false to reject</param>
        /// <param name="doInternalFiltering">Set true to continue with filters from the list</param>
        public delegate void PreFilterDelegate(FilterList filterList, IGeoObject go, out bool accepted, out bool doInternalFiltering);
        /// <summary>
        /// Event to add custom filter.
        /// </summary>
        public event PreFilterDelegate PreFilterEvent;
        internal IAttributeListContainer AttributeListContainer
        {
            get { return owner; }
            set { owner = value; }
        }
#region IShowProperty Members
        private List<IPropertyPage> propertyPages;
        public override void Added(IPropertyPage pp)
        {
            propertyPages.Add(pp);
            base.Added(pp);
        }
        public override void Removed(IPropertyPage pp)
        {
            propertyPages.Remove(pp);
            base.Removed(pp);
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
        /// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
        /// </summary>
        public override ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.GroupTitle;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>, 
        /// returns the number of subentries in this property view.
        /// </summary>
        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }
        IShowProperty[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (subEntries == null)
                {
                    subEntries = new IShowProperty[entries.Count];
                    int i = 0;
                    foreach (DictionaryEntry de in entries)
                    {
                        subEntries[i] = de.Value as Filter;
                        ++i;
                    }
                }
                return subEntries;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.FilterList", false, this);
            }
        }
#endregion
#region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected FilterList(SerializationInfo info, StreamingContext context)
            : this()
        {
            try
            {
                unsortedEntries = info.GetValue("UnsortedEntries", typeof(Filter[])) as Filter[];
            }
            catch (SerializationException)
            {   // alte Dateien beinhalten entries direkt
                entries = info.GetValue("Entries", typeof(SortedList)) as SortedList;
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // info.AddValue("Entries", entries);
            // SortedList abspeichern macht u.U. Probleme bei Versionswechsel von .NET
            // deshalb jetzt nur Array abspeichern (23.9.2011)
            Filter[] filtera = new Filter[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                filtera[i] = (Filter)entries.GetByIndex(i);
            }
            info.AddValue("UnsortedEntries", filtera);
        }

#endregion
#region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (unsortedEntries != null)
            {
                entries = new SortedList();
                for (int i = 0; i < unsortedEntries.Length; i++)
                {
                    entries.Add(unsortedEntries[i].Name, unsortedEntries[i]);
                }
                unsortedEntries = null;
            }
            foreach (DictionaryEntry de in entries)
            {
                (de.Value as Filter).Parent = this;
            }
        }
#endregion
#region ICommandHandler Members
        private IShowProperty FindEntry(string name)
        {
            for (int i = 0; i < SubEntries.Length; ++i)
            {
                Filter f = SubEntries[i] as Filter;
                if (f != null)
                {
                    if (f.Name == name) return f;
                }
            }
            return null;
        }
        public Filter AddNewFilter()
        {
            Filter newFilter = Filter.Construct();
            int i = 1;
            string prefix = StringTable.GetString("Filter.NewFilter.Prefix");
            while (entries.ContainsKey(prefix + i.ToString())) ++i;
            newFilter.Name = prefix + i.ToString();
            newFilter.Parent = this;
            entries.Add(newFilter.Name, newFilter);
            newFilter.IsActive = true;
            subEntries = null;
            for (i = 0; i < propertyPages.Count; i++)
            {
                if (propertyPages[i].IsOnTop())
                {
                    propertyPages[i].OpenSubEntries(this, false);
                    propertyPages[i].Refresh(this);
                    propertyPages[i].OpenSubEntries(this, true);
                    IPropertyEntry sp = FindEntry(newFilter.Name) as IPropertyEntry;
                    if (sp != null)
                    {
                        propertyPages[i].SelectEntry(sp);
                        propertyPages[i].StartEditLabel(sp);
                    }
                }
                else
                {
                    propertyPages[i].OpenSubEntries(this, false);
                    propertyPages[i].Refresh(this);
                }
            }
            return newFilter;
        }
        internal void DeactivateAll()
        {
            foreach (DictionaryEntry de in entries)
            {
                (de.Value as Filter).IsActive = false;
            }
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.FilterList.AddFilter":
                    AddNewFilter();
                    return true;
                case "MenuId.FilterList.DeactivateAll":
                    DeactivateAll();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add FilterList.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        #region INameChange Members

        public bool MayChangeName(object namedObject, string newName)
        {
            Filter f = namedObject as Filter;
            if (f.Name == newName) return true; // garkeine Änderung
            return !entries.ContainsKey(newName); // OK, wenns den Namen nicht gibt
        }

        public void NameChanged(object namedObject, string oldName)
        {
            Filter f = namedObject as Filter;
            entries.Remove(oldName);
            entries.Add(f.Name, f);
            subEntries = null;
            for (int i = 0; i < propertyPages.Count; i++)
            {
                if (propertyPages[i].IsOnTop())
                {
                    propertyPages[i].Refresh(this);
                    propertyPages[i].OpenSubEntries(this, true);
                    IPropertyEntry sp = FindEntry(f.Name) as IPropertyEntry;
                    if (sp != null)
                    {
                        propertyPages[i].SelectEntry(sp);
                    }
                }
            }
        }

#endregion

    }
}
