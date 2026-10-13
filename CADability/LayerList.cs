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
    /// A list of <see cref="Layer"/>s, always sorted alphabetically by name. The names must be unique: adding or
    /// renaming a layer so that a name would occur twice throws a <see cref="NameAlreadyExistsException"/>.
    /// The list is owned by a project or by the global settings, keeps a current layer and is displayed in the
    /// property page with its layers as sub entries.
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
        /// <summary>
        /// Handler for <see cref="LayerAddedEvent"/>.
        /// </summary>
        /// <param name="sender">the list the layer was added to</param>
        /// <param name="added">the layer that was added</param>
        public delegate void LayerAddedDelegate(LayerList sender, Layer added);
        /// <summary>
        /// Handler for <see cref="LayerRemovedEvent"/>.
        /// </summary>
        /// <param name="sender">the list the layer was removed from</param>
        /// <param name="removed">the layer that was removed</param>
        public delegate void LayerRemovedDelegate(LayerList sender, Layer removed);
        /// <summary>
        /// Raised after a layer has been added to the list by <see cref="Add(Layer)"/> (this includes
        /// <see cref="CreateOrFind(string)"/> and the commands of the context menu). Views use it to update their lists of
        /// visible layers (<see cref="CheckedLayerList"/>).
        /// </summary>
        public event LayerAddedDelegate LayerAddedEvent;
        /// <summary>
        /// Raised after a layer has been removed from the list by <see cref="Remove(Layer)"/>.
        /// </summary>
        public event LayerRemovedDelegate LayerRemovedEvent;
        /// <summary>
        /// Creates an empty layer list without a current layer. A new <see cref="Project"/> gets a clone of the global layer
        /// list, so this constructor is rarely needed; <see cref="GetDefault"/> creates a list with the default layers.
        /// </summary>
        public LayerList()
        {
            entries = new SortedList();
            resourceIdInternal = "LayerList";
        }
        /// <summary>
        /// Adds a new layer to the list. The first layer added becomes the current layer. A
        /// <see cref="NameAlreadyExistsException"/> is thrown if there is already a layer with the same name.
        /// </summary>
        /// <param name="LayerToAdd">the layer to add</param>
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
        /// Removes a layer from the list. Nothing happens if the layer is not in the list or if the owner of the list
        /// (project or settings) refuses the removal, e.g. because the layer is still in use.
        /// </summary>
        /// <param name="LayerToRemove">the layer to remove</param>
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
        /// Returns the layer with the given name, or null if there is no such layer in the list.
        /// </summary>
        /// <param name="Name">the name of the layer</param>
        /// <returns>the layer, or null</returns>
        public Layer Find(string Name)
        {
            return entries[Name] as Layer;
        }
        /// <summary>
        /// Returns the layer with the given name, creating and adding it to the list when there is none yet. This is the
        /// usual way to get a layer for an object: <c>project.LayerList.CreateOrFind("3D")</c>.
        /// </summary>
        /// <param name="Name">the name of the layer</param>
        /// <returns>the existing or newly created layer</returns>
        public Layer CreateOrFind(string Name)
        {
            Layer res = Find(Name);
            if (res != null) return res;
            res = new Layer(Name);
            Add(res);
            return res;
        }
        /// <summary>
        /// Returns the layer at the given position in the alphabetical order of the names, or null when the index is out of
        /// range.
        /// </summary>
        /// <param name="Index">index from 0 to <see cref="Count"/>-1</param>
        /// <returns>the layer, or null</returns>
        public Layer this[int Index]
        {
            get
            {
                if (Index >= 0 && Index < entries.Count)
                    return entries.GetByIndex(Index) as Layer;
                return null;
            }
        }
        /// <summary>
        /// Returns the position of the given layer object in the alphabetical order of the list, as used by the indexer.
        /// </summary>
        /// <param name="ly">the layer to look for (compared by reference)</param>
        /// <returns>the index, or -1 when the layer is not in this list</returns>
        public int FindIndex(Layer ly)
        {
            for (int i = 0; i < entries.Count; ++i)
            {
                if (entries.GetByIndex(i) == ly) return i;
            }
            return -1;
        }
        /// <summary>
        /// Returns all layers of the list as an array, sorted alphabetically by name.
        /// </summary>
        /// <returns>a new array with the layers; empty when the list is empty</returns>
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
        /// Creates a copy of this list containing clones of all layers. The current layer of the
        /// copy is the clone of the current layer of this list.
        /// </summary>
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
        /// <summary>
        /// The <see cref="CADability.Attribute.ColorList"/> of the owner of this list (the project or the global settings), or
        /// null when the list has no owner.
        /// </summary>
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
        /// <summary>
        /// The <see cref="Project"/> or <see cref="Settings"/> that holds this list. It is set by the owner when the list is
        /// added to it; the list asks it before removing a layer (see <see cref="Remove(Layer)"/>) and forwards changes of its
        /// layers to it.
        /// </summary>
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
        /// Creates a layer list with the layer names taken from the string resource "LayerList.Default".
        /// Used when no global layer list is available.
        /// </summary>
        /// <returns>the default layer list</returns>
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
        /// <summary>
        /// Raised when a layer is added, removed or renamed (<see cref="INotifyModification"/>). Changes of the other
        /// properties of a layer are forwarded to the <see cref="Owner"/> instead.
        /// </summary>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
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

        /// <summary>
        /// Implements <see cref="ICollection.IsSynchronized"/>. The list is not synchronized, the value is false.
        /// </summary>
        public bool IsSynchronized
        {
            get
            {
                return entries.IsSynchronized;
            }
        }

        /// <summary>
        /// The number of layers in the list.
        /// </summary>
        public int Count
        {
            get
            {
                return entries.Count;
            }
        }

        /// <summary>
        /// Copies the layers, in alphabetical order of their names, to the given array (<see cref="ICollection.CopyTo(Array, int)"/>).
        /// </summary>
        /// <param name="array">the destination array, which must be large enough</param>
        /// <param name="index">the index in <paramref name="array"/> at which copying begins</param>
        public void CopyTo(Array array, int index)
        {
            entries.Values.CopyTo(array, index);
        }

        /// <summary>
        /// An object that can be used to lock the list for synchronized access (<see cref="ICollection.SyncRoot"/>).
        /// </summary>
        public object SyncRoot
        {
            get
            {
                return entries.SyncRoot;
            }
        }

        #endregion
        #region IEnumerable Members

        /// <summary>
        /// Enumerates the layers in alphabetical order of their names, so <c>foreach (Layer l in layerList)</c> can be used.
        /// </summary>
        /// <returns>an enumerator over the <see cref="Layer"/> objects</returns>
        public IEnumerator GetEnumerator()
        {
            return entries.Values.GetEnumerator();
        }

        #endregion
        #region IPropertyEntry Members
        /// <inheritdoc/>
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType flags = PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
                return flags;
            }
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <summary>
        /// The current layer: the layer that newly constructed objects get (see <see cref="Project.SetDefaults(IGeoObject)"/>),
        /// shown in bold in the control center. When no current layer has been set, the first layer of the list is used.
        /// Only a layer that belongs to this list can be made current; a layer that is not in the list is ignored.
        /// </summary>
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
        /// Creates a new <see cref="CheckedLayerList"/> with the provided layer list and a set of
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
        /// <summary>
        /// Checks or unchecks the given layer programmatically. When the state actually changes, the
        /// <see cref="CheckStateChangedEvent"/> is raised just as if the user had clicked the checkbox, and the entry is
        /// refreshed in the control center.
        /// </summary>
        /// <param name="l">the layer</param>
        /// <param name="check">true to check, false to uncheck the layer</param>
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
        /// <inheritdoc/>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable;
            }
        }
        /// <inheritdoc/>
        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }
        IShowProperty[] subEntries;
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // TODO:  Add CheckedAttributes.OnUpdateCommand implementation
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
        /// <inheritdoc/>
        public override void Refresh()
        {
            subEntries = null;
            propertyPage?.Refresh(this);
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
        /// <summary>
        /// Tests whether the given layer is checked, e.g. whether it is visible in the view this list belongs to.
        /// </summary>
        /// <param name="l">the layer (compared by reference)</param>
        /// <returns>true if the layer is checked</returns>
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
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
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
