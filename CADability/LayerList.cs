using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using CheckState = CADability.Substitutes.CheckState;

namespace CADability.Attribute
{
    /// <summary>
    /// Eine Liste von Layern, immer alphabetisch nach Namen sortiert.
    /// Die Namen müssen eindeutig sein. Das Zufügen bzw. Umbenennen eines Layers, so
    /// dass Mehrdeutigkeiten entstehen würden, führt zu einer Exception.
    /// </summary>
    [Serializable]
    public class LayerList : PropertyEntryImpl, INotifyModification, ISerializable,
        ICollection, IAttributeList, ICommandHandler, IDeserializationCallback
    {
        private Layer[] unsortedEntries;
        private SortedList entries;
        private IAttributeListContainer owner; // Besitzer, entweder Projekt oder Settings
        private Layer current;
        internal string menuResourceId;
        IPropertyEntry[] showProperties;
        public delegate void LayerAddedDelegate(LayerList sender, Layer added);
        public delegate void LayerRemovedDelegate(LayerList sender, Layer removed);
        public event LayerAddedDelegate LayerAddedEvent;
        public event LayerRemovedDelegate LayerRemovedEvent;
        public LayerList()
        {
            entries = new SortedList();
            resourceIdInternal = "LayerList";
        }
        /// <summary>
        /// Fügt einen neuen Layer der Liste zu.
        /// </summary>
        /// <param name="LayerToAdd">Der neue Layer</param>
        public void Add(Layer LayerToAdd)
        {
            if (entries.Contains(LayerToAdd.Name))
            {
                throw new NameAlreadyExistsException(this, LayerToAdd, LayerToAdd.Name);
            }
            LayerToAdd.Parent = this;
            entries.Add(LayerToAdd.Name, LayerToAdd);
            if (entries.Count == 1)
                current = LayerToAdd;
            if (DidModifyEvent != null) DidModifyEvent(this, null);
            if (LayerAddedEvent != null) LayerAddedEvent(this, LayerToAdd);
            showProperties = null;
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        /// <summary>
        /// Entfernt einen Layer aus der Liste. Kein Propblem, wenn der Layer nicht in der Liste ist.
        /// </summary>
        /// <param name="LayerToRemove"></param>
        public void Remove(Layer LayerToRemove)
        {
            if (entries.Contains(LayerToRemove.Name))
            {
                if (owner != null)
                {
                    if (!owner.RemovingItem(this, LayerToRemove, "LayerList")) return;
                }
                entries.Remove(LayerToRemove.Name);
                LayerToRemove.Parent = null;
                showProperties = null;
                if (DidModifyEvent != null) DidModifyEvent(this, null);
                if (LayerRemovedEvent != null) LayerRemovedEvent(this, LayerToRemove);
                if (propertyPage != null) propertyPage.Refresh(this);
            }
        }
        private void RemoveAll() // nur intern verwenden, da nicht RemovingItem aufgerufen wird
        {
            entries.Clear();
            showProperties = null;
            if (propertyPage != null) propertyPage.OpenSubEntries(this, false); // zuklappen
        }
        /// <summary>
        /// Liefert den Layer mit dem im Parameter gegebenen Namen. 
        /// Liefert null, wenn kein solcher Layer in der Liste existiert.
        /// </summary>
        /// <param name="Name"></param>
        /// <returns></returns>
        public Layer Find(string Name)
        {
            return entries[Name] as Layer;
        }
        public Layer CreateOrFind(string Name)
        {
            Layer res = Find(Name);
            if (res != null) return res;
            res = new Layer(Name);
            Add(res);
            return res;
        }
        public Layer this[int Index]
        {
            get
            {
                if (Index >= 0 && Index < entries.Count)
                    return entries.GetByIndex(Index) as Layer;
                return null;
            }
        }
        public int FindIndex(Layer ly)
        {
            for (int i = 0; i < entries.Count; ++i)
            {
                if (entries.GetByIndex(i) == ly) return i;
            }
            return -1;
        }
        public Layer[] ToArray()
        {
            Layer[] res = new Layer[entries.Count];
            int i = 0;
            foreach (Layer l in entries.Values)
            {
                res[i] = l;
                ++i;
            }
            return res;
        }
        /// <summary>
        /// Alle Layer sollen sich mit ihrer ColorDef Property auf ColorDef Objekte
        /// aus der im Parameter gegebenen Liste beziehen. Die Liste wird ggf. erweitert.
        /// </summary>
        /// <param name="ct">Die Liste der benannten Farben</param>
        internal LayerList clone()
        {
            LayerList res = new LayerList();
            foreach (DictionaryEntry de in entries)
                res.Add((de.Value as Layer).Clone());
            if (current != null)
            {
                res.current = res.Find(current.Name);
            }
            else
            {
                res.current = null;
            }
            return res;
        }
        public ColorList ColorList
        {
            get
            {
                if (owner != null)
                {
                    return owner.ColorList;
                }
                return null;
            }
        }
        public IAttributeListContainer Owner
        {
            get { return owner; }
            set { owner = value; }
        }
        /*
		/// <summary>
		/// Gets the LayerList in sync with the ColorList.
		/// The layer in the layer list may refer to a color. There is a color list in the same
		/// context. (The context is given by an object that implements <see cref="IAttributeListContainer"/>,
		/// currently there are <see cref="Project"/> and <see cref="Settings"/> that implement IAttributeListContainer)
		/// This Update method redirects all references of layers in this list to colors in the related
		/// color list. If a color is not in the colorlist, it will be added. 
		/// </summary>
		public void Update()
		{
			ColorList cl = ColorList;
			for (int i=0; i<entries.Count; ++i)
			{
				Layer l = entries.GetByIndex(i) as Layer;
				ColorDef cd = l.ColorDef;
				if (cd!=null)
				{
					if (cd.Source==ColorDef.ColorSource.fromName)
					{
						ColorDef found = cl.GetColorDef(cd.Name);
						if (found!=null)
						{	// im Layer die Farbe ersetzen
							if (cd!=found) l.ColorDef = found;
						} 
						else
						{	// die Farbe gibt es in der ColorList nicht, also erzeugen
							cl.AddColorDef(cd);
						}
					}
				}
			}
		}
*/
        /// <summary>
        /// Liefert eine LayerList aus der StringResource. Wird benötigt, wenn keine
        /// globale LayerListe gegeben ist.
        /// </summary>
        /// <returns>Die Default LayerList</returns>
        public static LayerList GetDefault()
        {
            LayerList res = new LayerList();
            string defaultLayerList = StringTable.GetString("LayerList.Default");
            string[] split = defaultLayerList.Split(new char[] { defaultLayerList[0] });
            foreach (string Name in split)
            {
                if (Name.Length > 0)
                {
                    Layer NewLayer = new Layer(Name);
                    res.Add(NewLayer);
                }
            }
            return res;
        }
        #region INotifyModification Members
        public event CADability.DidModifyDelegate DidModifyEvent;
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected LayerList(SerializationInfo info, StreamingContext context)
            : this()
        {
            try
            {
                unsortedEntries = info.GetValue("UnsortedEntries", typeof(Layer[])) as Layer[];
            }
            catch (SerializationException)
            {   // alte Dateien beinhalten entries direkt
                entries = (SortedList)info.GetValue("Entries", typeof(SortedList));
            }
            current = (Layer)InfoReader.Read(info, "Current", typeof(Layer));
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
            Layer[] layerArray = new Layer[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                layerArray[i] = (Layer)entries.GetByIndex(i);
            }
            info.AddValue("UnsortedEntries", layerArray);
            info.AddValue("Current", current);
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
                Layer l = de.Value as Layer;
                if (l != null)
                {
                    l.Parent = this;
                }
            }
        }
        #endregion
        #region ICollection Members

        public bool IsSynchronized
        {
            get
            {
                return entries.IsSynchronized;
            }
        }

        public int Count
        {
            get
            {
                return entries.Count;
            }
        }

        public void CopyTo(Array array, int index)
        {
            entries.Values.CopyTo(array, index);
        }

        public object SyncRoot
        {
            get
            {
                return entries.SyncRoot;
            }
        }

        #endregion
        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return entries.Values.GetEnumerator();
        }

        #endregion
        #region IPropertyEntry Members
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType flags = PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
                return flags;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (showProperties == null)
                {
                    showProperties = new IPropertyEntry[entries.Count];
                    for (int i = 0; i < entries.Count; i++)
                    {
                        Layer l = entries.GetByIndex(i) as Layer;
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
                else return MenuResource.LoadMenuDefinition("MenuId.LayerList", false, this);
            }
        }
        #endregion
        #region IAttributeList Members
        void IAttributeList.Add(INamedAttribute toAdd)
        {
            if (toAdd is Layer && Find(toAdd.Name) == null)
                Add((Layer)toAdd);
        }
        INamedAttribute IAttributeList.Item(int Index)
        {
            return entries.GetByIndex(Index) as INamedAttribute;
        }
        void IAttributeList.AttributeChanged(INamedAttribute attribute, ReversibleChange change)
        {
            if (owner != null) owner.AttributeChanged(this, attribute, change);
        }
        bool IAttributeList.MayChangeName(INamedAttribute Attribute, string newName)
        {
            Layer l = Attribute as Layer;
            if (l.Name == newName) return true; // garkeine Änderung
            return !entries.ContainsKey(newName); // OK, wenns den Namen nicht gibt
        }
        void IAttributeList.NameChanged(INamedAttribute Attribute, string oldName)
        {
            Layer l = Attribute as Layer;
            entries.Remove(oldName);
            entries.Add(l.Name, l);
            if (DidModifyEvent != null) DidModifyEvent(this, null); // ist das ok? der Modelview brauchts
        }
        void IAttributeList.Initialize()
        {
            string defaultLayerList = StringTable.GetString("LayerList.Default");
            string[] split = defaultLayerList.Split(new char[] { defaultLayerList[0] });
            foreach (string Name in split)
            {
                if (Name.Length > 0)
                {
                    Layer NewLayer = new Layer(Name);
                    Add(NewLayer);
                }
            }
            current = this[0];
        }
        IAttributeList IAttributeList.Clone() { return clone() as IAttributeList; }
        void IAttributeList.Update(bool AddMissingToList)
        {
            OnUpdateFromProject();
        }
        void IAttributeList.Update(IGeoObject Object2Update)
        {
            if (Object2Update is ILayer)
            {
                Layer oldL = (Object2Update as ILayer).Layer;
                if (oldL == null) return;
                Layer l = Find(oldL.Name);
                if (l == null)
                    Add(oldL);
                else
                    (Object2Update as ILayer).Layer = l;
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
        public Layer Current
        {
            get
            {
                if (current == null && entries.Count > 0)
                {
                    current = entries.GetByIndex(0) as Layer;
                }
                return current;
            }
            set
            {
                if (entries[value.Name] != null)
                    current = value;
            }
        }
        private void CreateNewLayer()
        {
            string NewLayerName = StringTable.GetString("LayerList.NewLayerName");
            int MaxNr = 0;
            foreach (DictionaryEntry de in entries)
            {
                string Name = de.Key.ToString();
                if (Name.StartsWith(NewLayerName))
                {
                    try
                    {
                        int nr = int.Parse(Name.Substring(NewLayerName.Length));
                        if (nr > MaxNr) MaxNr = nr;
                    }
                    catch (ArgumentNullException) { } // hat garkeine Nummer
                    catch (FormatException) { } // hat was anderes als nur Ziffern
                    catch (OverflowException) { } // zu viele Ziffern
                }
            }
            MaxNr += 1; // nächste freie Nummer
            NewLayerName += MaxNr.ToString();
            Add(new Layer(NewLayerName));

            showProperties = null;
            propertyPage.Refresh(this);
            propertyPage.OpenSubEntries(this, true);
            propertyPage.StartEditLabel(showProperties[entries.IndexOfKey(NewLayerName)] as IPropertyEntry);
        }
        #region ICommandHandler Members
        private void OnAddFromGlobal()
        {
            foreach (Layer l in Settings.GlobalSettings.LayerList.entries.Values)
            {
                if (!entries.ContainsKey(l.Name))
                {
                    this.Add(l.Clone());
                }
            }
        }
        private void OnMakeGlobal()
        {
            Settings.GlobalSettings.LayerList.RemoveAll();
            foreach (Layer l in entries.Values)
            {
                Settings.GlobalSettings.LayerList.Add(l.Clone());
            }
        }
        private void OnUpdateFromProject()
        {
            Dictionary<string, Layer> used = new Dictionary<string, Layer>();
            foreach (Layer layer in entries.Values)
            {
                used[layer.Name] = layer;
            }
            Project project = this.owner as Project;
            if (project == null && Frame != null) project = Frame.Project;
            if (project != null)
            {
                foreach (Model m in project)
                {
                    foreach (IGeoObject go in m) GetAllLayer(used, go);
                }
                foreach (KeyValuePair<string, Layer> ds in used)
                {
                    if (!entries.ContainsKey(ds.Key))
                    {
                        this.Add(ds.Value);
                    }
                }
            }
        }
        private void OnRemoveUnused()
        {
            try
            {
                Dictionary<string, Layer> used = new Dictionary<string, Layer>();
                foreach (Model m in Frame.Project)
                {
                    foreach (IGeoObject go in m) GetAllLayer(used, go);
                }
                bool found = false;
                do
                {
                    found = false;
                    if (entries.Count > 1)
                    {
                        foreach (Layer ds in entries.Values)
                        {
                            if (!used.ContainsKey(ds.Name))
                            {
                                Remove(ds);
                                found = true;
                                break;
                            }
                        }
                    }
                } while (found);
            }
            catch (InvalidOperationException)
            {   // soll mal bei der Iteration vorgekommen sein (Mail vom 21.10 13, Nürnberger) kann ich mir aber nicht erklären
            }
        }
        private void GetAllLayer(Dictionary<string, Layer> collect, IGeoObject go)
        {
            if (go.Layer != null)
            {
                Layer found = null;
                if (collect.TryGetValue(go.Layer.Name, out found))
                {
                    go.Layer = found; // damit wird bei Namensgleichheit alles auf dieses Layer gesetzt
                }
                else
                {
                    collect[go.Layer.Name] = go.Layer;
                }
            }
            if (go is Block)
            {
                foreach (IGeoObject child in (go as Block).Children)
                    GetAllLayer(collect, child);
            }
        }
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.LayerList.New":
                    CreateNewLayer();
                    return true;
                case "MenuId.LayerList.RemoveUnused":
                    OnRemoveUnused();
                    return true;
                case "MenuId.LayerList.UpdateFromProject":
                    OnUpdateFromProject();
                    return true;
                case "MenuId.LayerList.AddFromGlobal":
                    OnAddFromGlobal();
                    return true;
                case "MenuId.LayerList.MakeGlobal":
                    OnMakeGlobal();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.LayerList.RemoveUnused":
                case "MenuId.LayerList.UpdateFromProject":
                case "MenuId.LayerList.AddFromGlobal":
                case "MenuId.LayerList.MakeGlobal":
                    CommandState.Enabled = owner is Project;
                    return true;
                default:
                    return false;
            }
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }
    /// <summary>
    /// A collapsable treeview entry for the ControlCenter that presents to the user all layers of the
    /// provided list with a checkbox next to each layer. The titel of the entry is specified by
    /// the provided resourceId. 
    /// </summary>
    [Serializable]
    public class CheckedLayerList : IShowPropertyImpl, ICommandHandler, ISerializable, IDeserializationCallback
    {
        LayerList layerList;
        List<Layer> checkedLayers;
        Layer[] deserializedCheckedLayers;
        /// <summary>
        /// Creates a new <see cref="CheckedLayerlist"/> with the provided layer list and a set of
        /// initially checked layers
        /// </summary>
        /// <param name="layerList">List of all layers to display</param>
        /// <param name="checkedLayers">Subset of the layerlist that is to be checked initially</param>
        /// <param name="resourceId">Resource id for the title of the Controlcenter entry</param>
        public CheckedLayerList(LayerList layerList, Layer[] checkedLayers, string resourceId)
        {
            this.layerList = layerList;
            if (checkedLayers == null)
            {
                this.checkedLayers = new List<Layer>(layerList.ToArray());
            }
            else
            {
                this.checkedLayers = new List<Layer>(checkedLayers);
            }
            this.resourceIdInternal = resourceId;
            layerList.LayerAddedEvent += new LayerList.LayerAddedDelegate(OnLayerAdded);
            layerList.LayerRemovedEvent += new LayerList.LayerRemovedDelegate(OnLayerRemoved);
            layerList.DidModifyEvent += new DidModifyDelegate(OnLayerModified);
        }
        public void Set(Layer l, bool check)
        {
            if (check)
            {
                if (!checkedLayers.Contains(l))
                {
                    checkedLayers.Add(l);
                    if (CheckStateChangedEvent != null) CheckStateChangedEvent(l, true);
                }
            }
            else
            {
                if (checkedLayers.Contains(l))
                {
                    checkedLayers.Remove(l);
                    if (CheckStateChangedEvent != null) CheckStateChangedEvent(l, false);
                }
            }
            subEntries = null;
            if (propertyPage != null)
            {
                propertyPage.Refresh(this);
                propertyPage.OpenSubEntries(this, true);
            }
        }

        void OnLayerModified(object sender, EventArgs args)
        {
            Refresh();
        }
        void OnLayerRemoved(LayerList sender, Layer removed)
        {
            Refresh();
        }
        void OnLayerAdded(LayerList sender, Layer added)
        {
            Refresh();
        }
        #region IShowProperty Members
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
                    subEntries = new IShowProperty[layerList.Count];
                    for (int i = 0; i < layerList.Count; ++i)
                    {
                        CheckState checkState;
                        if (checkedLayers.Contains(layerList[i]))
                            checkState = CheckState.Checked;
                        else
                            checkState = CheckState.Unchecked;
                        CheckProperty cp = new CheckProperty("VisibleLayer.Entry", checkState);
                        cp.CheckStateChangedEvent += new CADability.UserInterface.CheckProperty.CheckStateChangedDelegate(OnCheckStateChanged);
                        cp.LabelText = layerList[i].Name;
                        subEntries[i] = cp;
                    }
                }
                return subEntries;
            }
        }
        public override MenuWithHandler[] ContextMenu => MenuResource.LoadMenuDefinition("MenuId.VisibleLayer", false, this);
        #endregion
        private void OnCheckStateChanged(string label, CheckState state)
        {
            Layer l = layerList.Find(label);
            if (l == null) return;
            if (state == CheckState.Checked)
            {
                if (!checkedLayers.Contains(l))
                {
                    checkedLayers.Add(l);
                }
            }
            else
            {
                checkedLayers.Remove(l);
            }
            if (CheckStateChangedEvent != null) CheckStateChangedEvent(l, state == CheckState.Checked);
        }
        #region ICommandHandler Members
        /// <summary>
        /// Implements <see cref="CADability.UserInterface.ICommandHandler.OnCommand (string)"/>
        /// </summary>
        /// <param name="MenuId"></param>
        /// <returns></returns>
        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.VisibleLayer.SelectAll":
                    checkedLayers.Clear();
                    checkedLayers.AddRange(layerList.ToArray());
                    subEntries = null;
                    if (propertyPage != null)
                    {
                        propertyPage.Refresh(this);
                        propertyPage.OpenSubEntries(this, true);
                    }
                    return true;
                case "MenuId.VisibleLayer.SelectNone":
                    checkedLayers.Clear();
                    subEntries = null;
                    if (propertyPage != null)
                    {
                        propertyPage.Refresh(this);
                        propertyPage.OpenSubEntries(this, true);
                    }
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Implements <see cref="CADability.UserInterface.ICommandHandler.OnUpdateCommand (string, CommandState)"/>
        /// </summary>
        /// <param name="MenuId"></param>
        /// <param name="CommandState"></param>
        /// <returns></returns>
        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add CheckedAttributes.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        public void Refresh()
        {
            subEntries = null;
            if (propertyPage != null) propertyPage.Refresh(this);
        }
        /// <summary>
        /// Returns all checked Layers
        /// </summary>
        public Layer[] Checked
        {
            get
            {
                return checkedLayers.ToArray();
            }
        }
        // Tests whether the provided layer is checked.
        public bool IsLayerChecked(Layer l)
        {
            return checkedLayers.Contains(l);
        }
        /// <summary>
        /// Delegate definition for the <see cref="CheckStateChangedEvent"/>, which is called when the user 
        /// changes the checkbox next to a layer
        /// </summary>
        /// <param name="layer">Layer which is changed</param>
        /// <param name="isChecked">New value of the checked attribute</param>
        public delegate void CheckStateChangedDelegate(Layer layer, bool isChecked);
        /// <summary>
        /// Event that gets called when the user changes the checbox next to a layer.
        /// </summary>
        public event CheckStateChangedDelegate CheckStateChangedEvent;

        #region ISerializable Members
        protected CheckedLayerList(SerializationInfo info, StreamingContext context)
        {
            resourceIdInternal = info.GetString("ResourceId");
            try
            {
                deserializedCheckedLayers = info.GetValue("CheckedLayers", typeof(Layer[])) as Layer[];
            }
            catch (SerializationException)
            {
                deserializedCheckedLayers = new Layer[0];
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
                deserializedCheckedLayers = new Layer[0];
            }
            layerList = info.GetValue("LayerList", typeof(LayerList)) as LayerList;
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ResourceId", resourceIdInternal);
            info.AddValue("CheckedLayers", checkedLayers.ToArray(), typeof(Layer[]));
            info.AddValue("LayerList", layerList, typeof(LayerList));
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            checkedLayers = new List<Layer>(deserializedCheckedLayers);
            deserializedCheckedLayers = null; // wird nicht mehr gebraucht
            layerList.LayerAddedEvent += new LayerList.LayerAddedDelegate(OnLayerAdded);
            layerList.LayerRemovedEvent += new LayerList.LayerRemovedDelegate(OnLayerRemoved);
            layerList.DidModifyEvent += new DidModifyDelegate(OnLayerModified);
        }
        #endregion
    }
}
