using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// Darstellung eines GeoObjectInputs im ControlCenter
    /// </summary>
    internal class GeoObjectProperty : IShowPropertyImpl
    {
        private IFrame frame;
        private IGeoObject[] geoObjects;
        private IGeoObject selectedGeoObject;
        private IShowProperty[] subEntries;
        private bool highlight;
        public delegate void SelectionChangedDelegate(GeoObjectProperty cp, IGeoObject selectedGeoObject);
        public event SelectionChangedDelegate SelectionChangedEvent;
        public GeoObjectProperty(string resourceId, IFrame frame)
        {
            this.resourceIdInternal = resourceId;
            this.frame = frame;
            geoObjects = new IGeoObject[0]; // leer initialisieren
        }
        public void SetGeoObjects(IGeoObject[] geoObjects, IGeoObject selectedGeoObject)
        {
            this.geoObjects = geoObjects;
            this.selectedGeoObject = selectedGeoObject;
            subEntries = null; // weg damit, damit es neu gemacht wird
            if (propertyTreeView != null)
            {
                this.Select();
                propertyTreeView.Refresh(this);
                if (geoObjects.Length > 0) propertyTreeView.OpenSubEntries(this, true);
            }
        }
        public void SetSelectedGeoObject(IGeoObject geoObject)
        {	// funktioniert so nicht, selbst Refresh nutzt nichts.
            if (subEntries != null)
            {
                for (int i = 0; i < geoObjects.Length; ++i)
                {
                    GeoObjectProperty cp = subEntries[i] as GeoObjectProperty;
                    // cp.SetSelected(geoObjects[i] == geoObject);
                }
            }
        }
        public bool Highlight
        {
            get
            {
                return highlight;
            }
            set
            {
                highlight = value;
                if (propertyTreeView != null) propertyTreeView.Refresh(this);
            }
        }
        #region IShowPropertyImpl Overrides
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
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Selectable;
                if (highlight) res |= ShowPropertyLabelFlags.Highlight;
                return res;
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
                return geoObjects.Length;
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
                if (subEntries == null)
                {
                    subEntries = new IShowProperty[geoObjects.Length];
                    for (int i = 0; i < geoObjects.Length; ++i)
                    {
                        SimpleNameProperty cp = new SimpleNameProperty(geoObjects[i].Description, geoObjects[i], "GeoObject.Object");
                        cp.SetSelected(geoObjects[i] == selectedGeoObject);
                        cp.SelectionChangedEvent += new CADability.UserInterface.SimpleNameProperty.SelectionChangedDelegate(OnGeoObjectSelectionChanged);
                        subEntries[i] = cp;
                    }
                }
                return subEntries;
            }
        }
        #endregion
        private void OnGeoObjectSelectionChanged(SimpleNameProperty cp, object selectedGeoObject)
        {
            if (SelectionChangedEvent != null) SelectionChangedEvent(this, selectedGeoObject as IGeoObject);
        }
    }

    /// <summary>
    /// Anzeige einer einfachen stringbasierten Property. Ein Objekt kann damit gekoppelt sein und 
    /// wird bei dem Event SelectionChangedEvent gemeldet. Ansonsten funktionslos.
    /// </summary>
    internal sealed class SimpleNameProperty : IShowPropertyImpl, ICommandHandler
    {
        private readonly string name;

        private readonly string contextMenuResourceId;
        public SimpleNameProperty(string name, object associatedObject, string resourceId)
        {
            this.name = name;
            this.AssociatedObject = associatedObject;
            IsSelected = false;
            base.resourceIdInternal = resourceId;
        }
        public SimpleNameProperty(string name, object associatedObject, string resourceId, string contextMenuResourceId)
        {
            this.name = name;
            this.AssociatedObject = associatedObject;
            IsSelected = false;
            base.resourceIdInternal = resourceId;
            this.contextMenuResourceId = contextMenuResourceId;
        }
        public void SetSelected(bool isSelected)
        {
            this.IsSelected = isSelected;
        }
        public object AssociatedObject { get; }

        public delegate void SelectionChangedDelegate(SimpleNameProperty cp, object associatedObject);
        public event SelectionChangedDelegate SelectionChangedEvent;
        #region IShowPropertyImpl Overrides
        public override MenuWithHandler[] ContextMenu => MenuResource.LoadMenuDefinition(contextMenuResourceId, false, this);

        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                ShowPropertyLabelFlags res;
                if (IsSelected) res = ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.Selected;
                else res = ShowPropertyLabelFlags.Selectable;
                if (contextMenuResourceId != null)
                {
                    res |= ShowPropertyLabelFlags.ContextMenu;
                }
                return res;
            }
        }
        public override ShowPropertyEntryType EntryType => ShowPropertyEntryType.GroupTitle;

        public override string LabelText
        {
            get => name;
            set => base.LabelText = value;
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Selected"/>
        /// </summary>
        public override void Selected()
        {
	        this.IsSelected = true;
	        SelectionChangedEvent?.Invoke(this, AssociatedObject);
        }
        #endregion

        #region ICommandHandler Members
        public delegate bool OnCommandDelegate(SimpleNameProperty sender, string menuId);
        public delegate bool OnUpdateCommandDelegate(SimpleNameProperty sender, string menuId, CommandState commandState);
        public event OnCommandDelegate OnCommandEvent;
        public event OnUpdateCommandDelegate OnUpdateCommandEvent;
        bool ICommandHandler.OnCommand(string menuId)
        {
            if (OnCommandEvent != null) 
	            return OnCommandEvent(this, menuId);
            return false;
        }

        bool ICommandHandler.OnUpdateCommand(string menuId, CommandState commandState)
        {
            if (OnUpdateCommandEvent != null) 
	            return OnUpdateCommandEvent(this, menuId, commandState);
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
    }
}
