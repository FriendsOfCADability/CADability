using CADability.GeoObject;
using System;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    using System.Collections.Generic;
    using UserInterface;

    /// <summary>
    /// Interface supported by all objects that have a Layer proerty
    /// </summary>

    public interface ILayer
    {
        /// <summary>
        /// Gets or sets the layer.
        /// </summary>
		Layer Layer { get; set; }
    }
    /*
	public class LayerListEntry :IShowPropertyImpl,ICommandHandler
	{
		private Layer layer;
		private LayerList layerList;
		private IShowProperty[] showProperties;
		private DisplayLayerCtrl control;
		public LayerListEntry(Layer l, LayerList ll)
		{
			layer = l;
			layerList = ll;
		}
		#region IShowProperty Members
		
		public override string LabelText
		{
			get
			{
				return layer.Name;
			}
		}

		public override ShowPropertyLabelFlags LabelType
		{
			get
			{
				ShowPropertyLabelFlags flags = ShowPropertyLabelFlags.Editable|ShowPropertyLabelFlags.Selectable|ShowPropertyLabelFlags.ContextMenu|ShowPropertyLabelFlags.ContextMenu ;
				if( layerList.Current==layer )
					flags |= ShowPropertyLabelFlags.Bold;
				return flags;
			}
		}
        /// <inheritdoc/>
        public override int SubEntriesCount
		{
			get
			{
				
				return 4;
			}
		}
        /// <inheritdoc/>
        public override IShowProperty[] SubEntries
		{
			get
			{
				if (showProperties==null)
				{
					ImageList LayerImageList = BitmapTable.GetImageList("Layer.png",16,16);
					showProperties = new IShowProperty[4];
					BooleanProperty prop0 = new BooleanProperty(layer,"Visible",
						"Layer.Visibility",
						"Layer.Visibility.Values");
					prop0.SetImages(LayerImageList,0,1);
					showProperties[0] = prop0;
					BooleanProperty prop1 = new BooleanProperty(layer,"Printable",
						"Layer.Printability",
						"Layer.Printability.Values");
					prop1.SetImages(LayerImageList,4,5);
					showProperties[1] = prop1;
					BooleanProperty prop2 = new BooleanProperty(layer,"Pickable",
						"Layer.Pickability",
						"Layer.Pickability.Values");
					prop2.SetImages(LayerImageList,2,3);
					showProperties[2] = prop2;
					ColorList ct = layerList.ColorList ;
					if(ct != null)
					{
						ct.DidModifyEvent +=new DidModifyDelegate(ct_DidModify);
						showProperties[3] = new ColorSelectionProperty(layer,"ColorDef","Layer.Color",ct,0);
					}
				}
				return showProperties;
			}
		}

		public override Control GetValueControl(int Width, int Height)
		{
			if (control==null)
			{	// erstmaliges Erzeugen des Controls
				control = new DisplayLayerCtrl(layer);
			}
			return control;
		}
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.GetContextMenu"/>, 
        /// returns the context menu with the id "MenuId.LayerListEntry".
        /// (see <see cref="MenuResource.LoadContextMenu"/>)
        /// </summary>
        public override ContextMenu GetContextMenu()
		{
			ContextMenu res = MenuResource.LoadContextMenu("MenuId.LayerListEntry",this);
			foreach( MenuItem mi in res.MenuItems)
			{
				MenuItemWithID miWid = mi as MenuItemWithID;
				switch( miWid.ID)
				{
					case "MenuId.LayerListEntry.Delete":
						if(layerList.Current == layer )
							miWid.Enabled=false;
						break;
					case "MenuId.LayerListEntry.Current":
						if(layerList.Current == layer )
						{
							miWid.Checked =true;
							miWid.Enabled=false;
						}
						else
						{
							miWid.Checked =false;
							miWid.Enabled=true;
						}break;
				}
			}
			return  res;
		}

		public override void LabelChanged(string NewText)
		{
			layer.Name = NewText;
		}

	
		public override void SetFocus(){control.Focus();}
		public override Control[] FocusableControls()
		{
			Control[] ctrls = new Control[1];
			ctrls[0] = control;
			return ctrls;
		}
		#endregion

		private void ct_DidModify(object sender, EventArgs args)
		{
			if (propertyPage!=null) propertyPage.Refresh(showProperties[3]);
		}
		#region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
		{
			switch( MenuId )
			{
				case "MenuId.LayerListEntry.Edit":
					if (propertyPage!=null) propertyPage.StartEditLabel(this);
					return true;
				case "MenuId.LayerListEntry.Delete":
					layerList.Remove(layer);
					if (propertyPage!=null) propertyPage.Refresh(layerList);
					return true;
				case "MenuId.LayerListEntry.Current":
					layerList.Current = layer;
					if (propertyPage!=null) propertyPage.Refresh(layerList);
					return true;
			}
			return false;
		}
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
		{
			// TODO: betreffende MenueIds behandeln
			return false;
		}
		#endregion
	}
*/
    /// <summary>
    /// Selects which layers an operation takes into account: all layers of a <see cref="LayerList"/>, only the layers
    /// that are visible, or only the layers whose objects may be picked. The visibility of layers is kept per view
    /// (see <see cref="CheckedLayerList"/>); CADability itself does not use this enumeration any more, it is kept for
    /// applications built on it.
    /// </summary>
    public enum LayerUsage
    {
        /// <summary>All layers, regardless of their visibility or pickability.</summary>
        UseAllLayer,
        /// <summary>Only the layers that are visible.</summary>
        OnlyVisible,
        /// <summary>Only the layers whose objects can be picked.</summary>
        OnlyPickable
    }


    /// <summary>
    /// Equality comparer that treats two <see cref="Layer"/> objects as equal when they have the same <see cref="Layer.Name"/>,
    /// regardless of whether they are the same instance. Useful as the comparer of a dictionary or hash set keyed by layer
    /// when the layers may come from different lists (e.g. a project and its clone). A shared instance is available as
    /// <see cref="Layer.LayerComparer"/>.
    /// </summary>
    public class LayerComparer : IEqualityComparer<Layer>
    {
        #region IEqualityComparer<Layer> Members

        bool IEqualityComparer<Layer>.Equals(Layer x, Layer y)
        {
            return (x.Name == y.Name);
        }

        int IEqualityComparer<Layer>.GetHashCode(Layer obj)
        {
            return obj.Name.GetHashCode();
        }

        #endregion
    }
    /// <summary>
    /// A Layer is an attribute to <see cref="IGeoObject"/> objects. 
    /// With several layers you can group the GeoObjects in a <see cref="Model"/>.
    /// In a <see cref="ModelView"/> you can select which layers are visible.
    /// In a <see cref="Filter"/> you can select which layers are be pickable
    /// The property <see cref="DisplayOrder"/> affects the order of display or printing
    /// if the according feature is enabled in a <see cref="ModelView"/> or <see cref="Layout"/>.
    /// Layer implements the <see cref="IShowProperty"/> interface to present ist properties.
    /// </summary>
    [Serializable()]
    public class Layer : PropertyEntryImpl, ISerializable, INamedAttribute,
            ICommandHandler, IJsonSerialize
    {
        private string name; // name of the Layer
        private int displayOrder;
        private int transparency;
        /// <summary>
        /// The <see cref="LayerList"/> this layer belongs to. After some deliberation it seemed most sensible that a layer knows
        /// the list it is contained in: all alternatives with events are not cheaper, on the contrary. And there must be exactly
        /// one list, since otherwise e.g. the associated color list cannot be determined. Changes of the layer's properties are
        /// forwarded to this list.
        /// </summary>
        private LayerList parent; // die Liste, in der der Layer steckt.
        private void FireDidChange(string propertyName, object propertyOldValue)
        {   // Eine Eigenschaft des Layers hat sich geändert.
            // Wird an parent, also LayerListe weitergeleitet
            if (parent != null)
            {
                ReversibleChange change = new ReversibleChange(this, propertyName, propertyOldValue);
                (parent as IAttributeList).AttributeChanged(this, change);
            }
        }
        private IPropertyEntry[] showProperties;
        /// <summary>
        /// Creates a layer without a name, with <see cref="DisplayOrder"/> and <see cref="Transparency"/> set to 0. Used by
        /// the JSON deserialization and by <see cref="Clone"/>; set <see cref="Name"/> before adding the layer to a
        /// <see cref="LayerList"/>.
        /// </summary>
        public Layer()
        {	// kein Name
            displayOrder = 0;
            transparency = 0;
        }
        /// <summary>
        /// Creates a layer with the given name. The layer does not belong to a list yet; add it to the <see cref="LayerList"/>
        /// of the project (or use <see cref="LayerList.CreateOrFind(string)"/>) before assigning it to objects.
        /// </summary>
        /// <param name="Name">the name of the layer, must be unique within the list it is added to</param>
        public Layer(string Name) : this()
        {
            name = Name;
        }

        /// <summary>
        /// A shared instance of <see cref="CADability.Attribute.LayerComparer"/>, which compares layers by name only.
        /// </summary>
        public static LayerComparer LayerComparer = new LayerComparer();

        /// <summary>
        /// The name of the layer, which identifies it within its <see cref="LayerList"/> and is shown in the control center.
        /// Setting a name that is already used by another layer of the list throws a <see cref="NameAlreadyExistsException"/>.
        /// Otherwise the list is re-sorted and the change is forwarded to the owner of the list (the project), which records
        /// it for undo.
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
                if (parent != null) (parent as IAttributeList).NameChanged(this, OldName);
                FireDidChange("Name", OldName);
            }
        }
        /// <summary>
        /// Creates a copy of this layer with the same name, <see cref="DisplayOrder"/> and <see cref="Transparency"/>.
        /// The copy does not belong to any <see cref="LayerList"/>.
        /// </summary>
        /// <returns>the copy</returns>
        public Layer Clone()
        {
            Layer res = new Layer();
            res.name = name;
            res.displayOrder = displayOrder;
            res.transparency = transparency;
            return res;
        }
        internal new LayerList Parent
        {
            get { return parent; }
            set { parent = value; }
        }
        IAttributeList INamedAttribute.Parent
        {
            get { return parent; }
            set { parent = value as LayerList; }
        }
        IPropertyEntry INamedAttribute.GetSelectionProperty(string key, Project project, GeoObjectList geoObjectList)
        {
            return null;
        }

        #region IPropertyEntry Members
        /// <inheritdoc/>
        public override PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType flags = PropertyEntryType.LabelEditable | PropertyEntryType.Selectable | PropertyEntryType.ContextMenu | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
                if (parent!=null && parent.Current != null && parent.Current.Name == Name) flags |= PropertyEntryType.Bold;
                return flags;
            }
        }
        /// <inheritdoc/>
        public override string LabelText
        {
            get { return name; }
        }
        /// <inheritdoc/>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (showProperties == null)
                {
                    showProperties = new IPropertyEntry[2];
                    IntegerProperty ip = new IntegerProperty(displayOrder, "Layer.DisplayOrder");
                    ip.GetIntEvent += new IntegerProperty.GetIntDelegate(GetDisplayOrder);
                    ip.SetIntEvent += new IntegerProperty.SetIntDelegate(SetDisplayOrder);
                    ip.SetMinMax(int.MinValue, int.MaxValue, true);
                    showProperties[0] = ip;
                    IntegerProperty ipt = new IntegerProperty(displayOrder, "Layer.Transparency");
                    ipt.GetIntEvent += new IntegerProperty.GetIntDelegate(GetTransparency);
                    ipt.SetIntEvent += new IntegerProperty.SetIntDelegate(SetTransparency);
                    ipt.IntegerValue = transparency;
                    ipt.SetMinMax(0, 255, true);
                    showProperties[1] = ipt;
                }
                return showProperties;
            }
        }
        void SetDisplayOrder(IntegerProperty sender, int newValue)
        {
            displayOrder = newValue;
        }
        int GetDisplayOrder(IntegerProperty sender)
        {
            return displayOrder;
        }
        void SetTransparency(IntegerProperty sender, int newValue)
        {
            Transparency = newValue;
        }
        int GetTransparency(IntegerProperty sender)
        {
            return transparency;
        }
        /// <inheritdoc/>
        public override bool EditTextChanged(string newValue)
        {
            return true;
        }
        /// <inheritdoc/>
        public override void Added(IPropertyPage propertyPage)
        {
            base.Added(propertyPage);
            base.resourceIdInternal = "LayerName";
        }
        /*	public override void Removed(IPropertyTreeView propertyPage)
            {
                ColorList ct = FindColorList();
                if(ct != null)
                {
                    ct.DidModify -= new DidModifyDelegate(ColorListDidModify);
                }
                base.Removed (propertyPage);
            }
    */
        /// <inheritdoc/>
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.LayerListEntry", false, this);
            }
        }
        /// <inheritdoc/>
        public override void EndEdit(bool aborted, bool modified, string newValue)
        {
            if (!aborted && newValue != Name)
            try
            {
                Name = newValue;
            }
            catch (NameAlreadyExistsException)
            {
            }
        }
        #endregion

        /*		private void ColorListDidModify(object sender, EventArgs args)
                {	// wenn sich die ColorList ändert, dann muss sich das in der Liste der angezeigten Farben wiederspiegeln
                    propertyPage.Refresh(this);
                }
                */
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Layer(SerializationInfo info, StreamingContext context)
        {
            name = (string)info.GetValue("Name", typeof(string));
            try
            {
                displayOrder = info.GetInt32("DisplayOrder");
            }
            catch (SerializationException)
            {
                displayOrder = 0;
            }
            try
            {
                transparency = info.GetInt32("Transparency");
            }
            catch (SerializationException)
            {
                transparency = 0;
            }
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
            info.AddValue("DisplayOrder", displayOrder);
            info.AddValue("Transparency", transparency);
        }

        #endregion
        #region IJsonSerialize
        /// <summary>
        /// Writes the name, display order and transparency of this layer for the JSON serialization (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">receives the data</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Name", name);
            data.AddProperty("DisplayOrder", displayOrder);
            data.AddProperty("Transparency", transparency);
        }
        /// <summary>
        /// Restores this layer from the JSON data written by <see cref="GetObjectData(IJsonWriteData)"/> (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">the data to read from</param>
        public void SetObjectData(IJsonReadData data)
        {
            name = data.GetProperty<string>("Name");
            displayOrder = data.GetProperty<int>("DisplayOrder");
            transparency = data.GetProperty<int>("Transparency");
        }
        #endregion
        /// <summary>
        /// The z-index of the layer: when a view or a print-out is set to use the display order (e.g.
        /// <see cref="GDI2DView.UseDisplayOrder"/>), the objects are painted in ascending order of the display order of their
        /// layers, so objects on a layer with a higher value cover objects on a layer with a lower value. Layers without
        /// special needs keep the default 0. Setting the value does not raise change notifications.
        /// </summary>
        public int DisplayOrder
        {
            get
            {
                return displayOrder;
            }
            set
            {
                displayOrder = value;
            }
        }
        /// <summary>
        /// Gets or sets the transparency of this layer: 0 is not transparent, 255 is totally transparent
        /// </summary>
        public int Transparency
        {
            get
            {
                return transparency;
            }
            set
            {
                int oldtransparence = transparency;
                transparency = value;
                FireDidChange("Transparency", oldtransparence);
            }
        }

        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            if (parent == null) return false;
            switch (MenuId)
            {
                case "MenuId.LayerListEntry.Edit":
                    if (propertyPage != null) propertyPage.StartEditLabel(this);
                    return true;
                case "MenuId.LayerListEntry.Delete":
                    parent.Remove(this);
                    if (propertyPage != null) propertyPage.Refresh(parent);
                    return true;
                case "MenuId.LayerListEntry.Current":
                    parent.Current = this;
                    if (propertyPage != null) propertyPage.Refresh(parent);
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.LayerListEntry.Delete":
                    if (parent.Current == this)
                        CommandState.Enabled = false;
                    break;
                case "MenuId.LayerListEntry.Current":
                    if (parent.Current == this)
                    {
                        CommandState.Checked = true;
                        CommandState.Enabled = false;
                    }
                    else
                    {
                        CommandState.Checked = false;
                        CommandState.Enabled = true;
                    }
                    break;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }


    /// <summary>
    /// Property entry for the control center which lets the user choose a <see cref="Layer"/> from a <see cref="LayerList"/>
    /// in a drop-down list. When constructed for an object implementing <see cref="ILayer"/>, the selection is assigned to
    /// that object's <see cref="ILayer.Layer"/> and the entry follows changes of the object's layer; in any case the
    /// selection is reported through <see cref="LayerChangedEvent"/>. GeoObjects show it among their attribute properties.
    /// </summary>
    public class LayerSelectionProperty : MultipleChoiceProperty
    {
        private LayerList layerList;
        private ILayer objectWithLayer;
        private IGeoObject toWatch;

        /// <summary>
        /// Creates a layer selection that is not bound to an object. The choice of the user is only reported via
        /// <see cref="LayerChangedEvent"/>; used e.g. to set the layer of several selected objects at once.
        /// </summary>
        /// <param name="resourceId">resource id of the label text (see <see cref="StringTable"/>)</param>
        /// <param name="layerList">the list whose layers are offered</param>
        /// <param name="select">the initially selected layer, or null for no selection</param>
        public LayerSelectionProperty(string resourceId, LayerList layerList, Layer select)
        {
            this.layerList = layerList;
            base.resourceIdInternal = resourceId;
            choices = new string[layerList.Count];
            int i = 0;
            foreach (Layer lay in layerList)
            {
                choices[i] = lay.Name;
                i++;
            }
            if (select != null) base.SelectedText = select.Name;
        }

        /// <summary>
        /// Creates a layer selection bound to <paramref name="ObjectWithLayer"/>, without the "undefined" entry. Same as
        /// <see cref="LayerSelectionProperty(ILayer, string, LayerList, bool)"/> with <c>includeUndefined</c> set to false.
        /// </summary>
        /// <param name="ObjectWithLayer">the object whose layer is shown and set; usually a GeoObject</param>
        /// <param name="resourceId">resource id of the label text (see <see cref="StringTable"/>)</param>
        /// <param name="ll">the list whose layers are offered</param>
        public LayerSelectionProperty(ILayer ObjectWithLayer, string resourceId, LayerList ll) :
            this(ObjectWithLayer, resourceId, ll, false)
        { }
        /// <summary>
        /// Creates a layer selection bound to <paramref name="ObjectWithLayer"/>: the current layer of the object is
        /// preselected, and when the user chooses an entry, the object's <see cref="ILayer.Layer"/> is set and
        /// <see cref="LayerChangedEvent"/> is raised. With <paramref name="includeUndefined"/> the list starts with an
        /// additional entry (string resource "Layer.Undefined") that stands for "no layer" (null) and is selected when the
        /// object has no layer; a <see cref="Style"/> uses this to leave the layer open.
        /// </summary>
        /// <param name="ObjectWithLayer">the object whose layer is shown and set; may be null, then the selection is only reported by the event</param>
        /// <param name="resourceId">resource id of the label text (see <see cref="StringTable"/>)</param>
        /// <param name="ll">the list whose layers are offered</param>
        /// <param name="includeUndefined">true to offer the additional "undefined" entry</param>
        public LayerSelectionProperty(ILayer ObjectWithLayer, string resourceId, LayerList ll, bool includeUndefined)
        {
            layerList = ll;
            base.resourceIdInternal = resourceId;
            if (includeUndefined)
            {
                choices = new string[ll.Count + 1];
                int i = 0;
                foreach (Layer lay in ll)
                {
                    choices[i + 1] = lay.Name;
                    i++;
                }
                string undef = StringTable.GetString("Layer.Undefined");
                // sollte es den Namen schon geben, werden solange - davor und dahintergemacht, bis es den Namen mehr gibt
                while (layerList.Find(undef) != null) undef = "-" + undef + "-";
                choices[0] = undef;
                objectWithLayer = ObjectWithLayer;
                if (objectWithLayer != null && objectWithLayer.Layer != null)
                {
                    SelectedText = objectWithLayer.Layer.Name;
                }
                else
                {
                    SelectedText = undef;
                }
            }
            else
            {
                choices = new string[ll.Count];
                int i = 0;
                foreach (Layer lay in ll)
                {
                    choices[i] = lay.Name;
                    i++;
                }
                objectWithLayer = ObjectWithLayer;
                if (objectWithLayer != null && objectWithLayer.Layer != null)
                    SelectedText = objectWithLayer.Layer.Name;
            }
            toWatch = objectWithLayer as IGeoObject;
        }

        /// <summary>
        /// Handler for <see cref="LayerChangedEvent"/>.
        /// </summary>
        /// <param name="selected">the layer the user selected; null when the "undefined" entry was chosen</param>
        public delegate void LayerChangedDelegate(Layer selected);
        /// <summary>
        /// Raised when the user has selected an entry of the drop-down list, after the layer of the connected object (if any)
        /// has been set.
        /// </summary>
        public event LayerChangedDelegate LayerChangedEvent;
        /// <inheritdoc/>
        protected override void OnSelectionChanged(string selected)
        {
            if (selected == null) return;
            Layer lay = layerList.Find(selected); // kann null sein
            if (objectWithLayer != null) objectWithLayer.Layer = lay;
            base.OnSelectionChanged(selected);
            if (LayerChangedEvent != null) LayerChangedEvent(lay);
        }
        /// <inheritdoc/>
        public override void Added(IPropertyPage propertyPage)
        {
            base.Added(propertyPage);
            if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Removed(IPropertyPage)"/>
        /// </summary>
        /// <param name="propertyPage">the IPropertyTreeView from which it was removed</param>
        public override void Removed(IPropertyPage propertyPage)
        {
            base.Removed(propertyPage);
            if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
        }
        private void GeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (Sender == toWatch && Change.OnlyAttributeChanged && propertyPage != null)
            {
                if ((Change as GeoObjectChange).MethodOrPropertyName == "Layer" ||
                    (Change as GeoObjectChange).MethodOrPropertyName == "Style")
                {
                    if (toWatch.Layer != null) base.SelectedText = toWatch.Layer.Name;
                    else base.SelectedText = null;
                    propertyPage.Refresh(this);
                }
            }
        }
        /// <summary>
        /// The GeoObject whose layer this entry shows and modifies (the object given to the constructor). Setting it
        /// redirects the entry to another object, which must implement <see cref="ILayer"/>: the entry stops watching the
        /// previous object and follows the layer changes of the new one. Used when the object under construction is replaced
        /// while its properties are displayed.
        /// </summary>
        public IGeoObject Connected
        {   // mit dieser Property kann man das kontrollierte Geoobjekt ändern
            get { return toWatch; }
            set
            {
                if (base.propertyPage != null)
                {   // dann ist diese Property schon Added und nicht removed
                    if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
                }
                toWatch = value;
                objectWithLayer = value as ILayer;
                if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
            }
        }
    }
}

