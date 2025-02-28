using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    /// <summary>
    /// The DimensionStyleList contains the list of available dimension styles (<see cref="DimensionStyle"/>). It is
    /// owned by the project or the global settings. It implements IShowProperty to
    /// make it available in the tree voew of the control center.
    /// </summary>
    [Serializable]
    public class DimensionStyleList : IShowPropertyImpl, ISerializable,
        ICommandHandler, IAttributeList, IDeserializationCallback
    {
        private IAttributeListContainer owner;
        private SortedList entries; // DimensionStyle
        private DimensionStyle[] unsortedEntries; // zunächst mal nur zum Speichern/Restaurieren
        private DimensionStyle current;
        internal string menuResourceId;
        private IShowProperty[] showProperties; // Anzeige der Liste
        public DimensionStyleList()
        {
            entries = new SortedList();
            resourceIdInternal = "DimensionStyleList";
        }

        public DimensionStyle this[int Index]
        {
            get
            {
                DimensionStyle res = entries.GetByIndex(Index) as DimensionStyle;
                return res;
            }
        }
        internal DimensionStyleList Clone()
        {
            DimensionStyleList res = new DimensionStyleList();
            foreach (DictionaryEntry de in entries)
                res.Add((de.Value as DimensionStyle).Clone());
            if (current != null)
                res.current = res.Find(current.Name);
            if (res.current == null && res.Count > 0) res.current = res[0];
            return res;
        }

        internal static DimensionStyleList GetDefault()
        {
            DimensionStyleList res = new DimensionStyleList();
            DimensionStyle ds = DimensionStyle.GetDefault();
            res.Add(ds);
            return res;
        }
        /// <summary>
        /// Add a dimensionstyle to the list. If there is already a dimension styl with the same name
        /// in the list, the <see cref="NameAlreadyExistsException"/> will be thrown.
        /// </summary>
        /// <param name="ToAdd">dimension style to add</param>
        public void Add(DimensionStyle ToAdd)
        {
            if (entries.Contains(ToAdd.Name))
            {
                throw new NameAlreadyExistsException(this, ToAdd, ToAdd.Name);
            }
            ToAdd.Parent = this; // der einzelne Stil braucht z.B. die ColorList
            // dazu sucht er Parent und über IAttributeList.Owner findet er die ColorList
            // ToAdd.LayerChangedEvent += new Condor.Layer.LayerChangedDelegate(OnLayerChanged);
            // wie die Benachrichtigung läuft, muss noch geklärt werden
            entries.Add(ToAdd.Name, ToAdd);
            if (entries.Count == 1)
                current = this[0];
            // if (DidModify!=null) DidModify(null,null);
            if (base.propertyTreeView != null)
            {
                showProperties = null; // damit anschiließend neu erzeugt wird
                base.propertyTreeView.Refresh(this);
            }
            if (DidModifyEvent != null) DidModifyEvent(this, null); // ist das ok? der Modelview brauchts
        }
        /// <summary>
        /// Implements <see cref="CADability.Attribute.IAttributeList.Find (string)"/>
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public DimensionStyle Find(string Name)
        {
            return entries[Name] as DimensionStyle; // null, wenns ihn nicht gibt
        }

        public DimensionStyle Current
        {
            get
            {
                if (current == null && entries.Count > 0)
                {
                    current = this[0];
                }
                return current;
            }
            set { current = value; }
        }
        /// <summary>
        /// The dimensionstyle ToRemove will be removed from the list. If there is no such dimension
        /// style, nothing will be changed.
        /// </summary>
        /// <param name="ToRemove"></param>
        public void Remove(DimensionStyle ToRemove)
        {
            if (entries.Contains(ToRemove.Name))
            {
                if (owner != null)
                {
                    if (!owner.RemovingItem(this, ToRemove, "DimensionStyleList")) return;
                }
                entries.Remove(ToRemove.Name);
                showProperties = null;
                if (propertyTreeView != null) propertyTreeView.Refresh(this);
            }
            if (DidModifyEvent != null) DidModifyEvent(this, null); // ist das ok? der Modelview brauchts
        }
        private void RemoveAll() // nur intern verwenden, da nicht RemovingItem aufgerufen wird
        {
            entries.Clear();
            showProperties = null;
            if (propertyTreeView != null) propertyTreeView.OpenSubEntries(this, false); // zuklappen
        }
        public event CADability.DidModifyDelegate DidModifyEvent;
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected DimensionStyleList(SerializationInfo info, StreamingContext context)
            : this()
        {
            try
            {
                unsortedEntries = info.GetValue("UnsortedEntries", typeof(DimensionStyle[])) as DimensionStyle[];
            }
            catch (SerializationException)
            {   // alte Dateien beinhalten entries direkt
                entries = (SortedList)info.GetValue("Entries", typeof(SortedList));
            }
            current = (DimensionStyle)(info.GetValue("CurrentDimensionStyle", typeof(DimensionStyle)));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // info.AddValue("Entries",entries);
            // SortedList abspeichern macht u.U. Probleme bei Versionswechsel von .NET
            // deshalb jetzt nur Array abspeichern (23.9.2011)
            DimensionStyle[] dima = new DimensionStyle[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                dima[i] = (DimensionStyle)entries.GetByIndex(i);
            }
            info.AddValue("UnsortedEntries", dima);
            info.AddValue("CurrentDimensionStyle", current);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (unsortedEntries != null)
            {   // entries als SortedList sollte so nicht mehr verwendet werden, besser
                // Set<DimensionStyle,EqualityComparer>
                // The items are enumerated in sorted order. So heißt es im Set
                entries = new SortedList();
                for (int i = 0; i < unsortedEntries.Length; i++)
                {
                    entries.Add(unsortedEntries[i].Name, unsortedEntries[i]);
                }
                unsortedEntries = null;
            }
            foreach (DictionaryEntry de in entries)
            {
                DimensionStyle ds = de.Value as DimensionStyle;
                ds.Parent = this;
            }
        }
        #endregion
        #region IShowProperty Members
        //public override string InfoText
        //{
        //    get
        //    {
        //        return  StringTable.GetString("DimensionStyleList.InfoText");
        //    }
        //}
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu;
            }
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
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>,
        /// returns the number of subentries in this property view.
        /// </summary>
        public override int SubEntriesCount
        {
            get
            {
                return entries.Count;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>,
        /// returns the subentries in this property view.
        /// </summary>
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (showProperties == null)
                {
                    showProperties = new IShowProperty[entries.Count];
                    for (int i = 0; i < entries.Count; i++)
                    {
                        DimensionStyle l = entries.GetByIndex(i) as DimensionStyle;
                        // showProperties[i] = new LayerListEntry(l,this);
                        showProperties[i] = l;
                    }
                }
                return showProperties;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                if (menuResourceId != null) return MenuResource.LoadMenuDefinition(menuResourceId, false, this);
                else return MenuResource.LoadMenuDefinition("MenuId.DimStyleEntry", false, this);
            }
        }
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.DimStyleList.UpdateAllDimensions":
                    OnUpdateAllDimensions();
                    return true;
                case "MenuId.DimStyleList.New":
                    ICommandHandler sub = (Current as ICommandHandler);
                    if (sub != null && propertyTreeView != null)
                    {
                        propertyTreeView.OpenSubEntries(this, true);
                        sub.OnCommand("MenuId.DimStyleEntry.Clone");
                        propertyTreeView.SelectEntry(sub as IPropertyEntry);
                    }
                    return true;
                case "MenuId.DimStyleList.RemoveUnused":
                    OnRemoveUnused();
                    return true;
                case "MenuId.DimStyleList.UpdateFromProject":
                    OnUpdateFromProject();
                    return true;
                case "MenuId.DimStyleList.AddFromGlobal":
                    OnAddFromGlobal();
                    return true;
                case "MenuId.DimStyleList.MakeGlobal":
                    OnMakeGlobal();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.DimStyleList.UpdateAllDimensions":
                    CommandState.Enabled = true;
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        private void OnAddFromGlobal()
        {
            foreach (DimensionStyle ds in Settings.GlobalSettings.DimensionStyleList.entries.Values)
            {
                if (!entries.ContainsKey(ds.Name))
                {
                    this.Add(ds.Clone());
                }
            }
        }
        private void OnMakeGlobal()
        {
            Settings.GlobalSettings.DimensionStyleList.RemoveAll();
            foreach (DimensionStyle ds in entries.Values)
            {
                Settings.GlobalSettings.DimensionStyleList.Add(ds.Clone());
            }
        }
        private void OnUpdateFromProject()
        {
            Hashtable used = GetAllDimensionStyles();
            foreach (DimensionStyle ds in used.Keys)
            {
                if (!entries.ContainsKey(ds.Name))
                {
                    this.Add(ds);
                }
            }
        }
        private void OnRemoveUnused()
        {
            Hashtable used = GetAllDimensionStyles();
            bool found = false;
            do
            {
                found = false;
                if (entries.Count > 1)
                {
                    foreach (DimensionStyle ds in entries.Values)
                    {
                        if (!used.ContainsKey(ds))
                        {
                            Remove(ds);
                            found = true;
                            break;
                        }
                    }
                }
            } while (found);
        }
        private void GetAllDimensionStyles(Hashtable collect, IGeoObject go)
        {
            if (go is Dimension)
            {
                Dimension dim = go as Dimension;
                DimensionStyle dimst = dim.DimensionStyle;
                collect[dimst] = null; // Hastable wird als set verwendet
            }
            else if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    GetAllDimensionStyles(collect, child);
            }
        }
        private Hashtable GetAllDimensionStyles()
        {
            Hashtable res = new Hashtable();
            foreach (Model m in Frame.Project)
            {
                foreach (IGeoObject go in m) GetAllDimensionStyles(res, go);
            }
            return res;
        }
        private void UpdateAllDimensions(IGeoObject go)
        {
            if (go is Dimension)
            {
                Dimension dim = go as Dimension;
                DimensionStyle dimst = dim.DimensionStyle;
                if (dimst != null && entries.Contains(dimst.Name))
                {
                    dim.Recalc();
                }
            }
            else if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    UpdateAllDimensions(child);
            }
        }
        private void OnUpdateAllDimensions()
        {
            foreach (Model m in Frame.Project)
            {
                foreach (IGeoObject go in m) UpdateAllDimensions(go);
            }
        }
        #endregion
        #region IAttributeList Members
        void IAttributeList.Add(INamedAttribute toAdd)
        {
            if (toAdd is DimensionStyle && Find(toAdd.Name) == null)
                Add((DimensionStyle)toAdd);
        }
        public int Count
        {
            get
            {
                return entries.Count;
            }
        }

        INamedAttribute IAttributeList.Item(int Index)
        {
            return entries.GetByIndex(Index) as INamedAttribute;
        }

        public IAttributeListContainer Owner
        {
            get
            {
                return owner;
            }
            set
            {
                owner = value;
            }
        }
        void IAttributeList.AttributeChanged(INamedAttribute attribute, ReversibleChange change)
        {
            if (owner != null) owner.AttributeChanged(this, attribute, change);
        }
        bool IAttributeList.MayChangeName(INamedAttribute attribute, string newName)
        {
            if (attribute.Name == newName) return true;
            return Find(newName) == null;
        }
        void IAttributeList.NameChanged(INamedAttribute Attribute, string oldName)
        {
            DimensionStyle ds = Attribute as DimensionStyle;
            entries.Remove(oldName);
            entries.Add(ds.Name, ds);
        }
        void IAttributeList.Initialize()
        {
            entries.Clear();
            current = DimensionStyle.GetDefault();
            Add(current);
        }
        IAttributeList IAttributeList.Clone() { return Clone() as IAttributeList; }
        void IAttributeList.Update(bool AddMissingToList)
        {
            foreach (DimensionStyle ds in entries.Values)
            {
                ds.Update(AddMissingToList);
            }
        }
        void IAttributeList.Update(IGeoObject Object2Update)
        {
            if (Object2Update is IDimensionStyle)
            {
                DimensionStyle oldDS = (Object2Update as IDimensionStyle).DimensionStyle;
                if (oldDS == null) return;
                DimensionStyle ds = Find(oldDS.Name);
                if (ds == null)
                    Add(oldDS);
                else
                    (Object2Update as IDimensionStyle).DimensionStyle = ds;
            }
        }

        INamedAttribute IAttributeList.Find(string Name)
        {
            return Find(Name) as INamedAttribute;
        }
        INamedAttribute IAttributeList.Current
        {
            get
            {
                return current as INamedAttribute;
            }
        }
        #endregion
    }
}
