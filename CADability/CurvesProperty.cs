using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>
    internal class CurvesProperty : IShowPropertyImpl
    {
        private IFrame frame;
        private ICurve[] curves;
        private ICurve selectedCurve;
        private bool highlight;
        private IShowProperty[] subEntries;
        public delegate void SelectionChangedDelegate(CurvesProperty cp, ICurve selectedCurve);
        public event SelectionChangedDelegate SelectionChangedEvent;
        private string contextMenuId; // MenueId für ContextMenu
        private ICommandHandler handleContextMenu;
        public CurvesProperty(string resourceId, IFrame frame)
        {
            this.resourceIdInternal = resourceId;
            this.frame = frame;
            curves = new ICurve[0]; // leer initialisieren
        }
        public void SetCurves(ICurve[] curves, ICurve selectedCurve)
        {
            this.curves = curves;
            this.selectedCurve = selectedCurve;
            subEntries = null; // weg damit, damit es neu gemacht wird
            if (propertyTreeView != null)
            {
                propertyTreeView.Refresh(this);
                if (curves.Length > 0) propertyTreeView.OpenSubEntries(this, true);
            }
        }
        public void SetSelectedCurve(ICurve curve)
        {   // funktioniert so nicht, selbst Refresh nutzt nichts.
            if (subEntries != null)
            {
                for (int i = 0; i < curves.Length; ++i)
                {
                    CurveProperty cp = subEntries[i] as CurveProperty;
                    cp.SetSelected(curves[i] == curve);
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
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Selectable;
                if (contextMenuId != null) res |= ShowPropertyLabelFlags.ContextMenu;
                if (highlight) res |= ShowPropertyLabelFlags.Highlight;
                return res;
            }
        }
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
                return curves.Length;
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
                    subEntries = new IShowProperty[curves.Length];
                    for (int i = 0; i < curves.Length; ++i)
                    {
                        CurveProperty cp = new CurveProperty(curves[i]);
                        cp.SetSelected(curves[i] == selectedCurve);
                        cp.SelectionChangedEvent += new CADability.UserInterface.CurveProperty.SelectionChangedDelegate(OnCurveSelectionChanged);
                        subEntries[i] = cp;
                    }
                }
                return subEntries;
            }
        }
        #endregion

        private void OnCurveSelectionChanged(CurveProperty cp, ICurve selectedCurve)
        {
            if (SelectionChangedEvent != null) SelectionChangedEvent(this, selectedCurve);
        }

        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                if (contextMenuId != null)
                {
                    Frame.ContextMenuSource = this;
                    return MenuResource.LoadMenuDefinition(contextMenuId, false, handleContextMenu);
                }
                return null;
            }
        }
        internal void SetContextMenu(string menuId, ICommandHandler handler)
        {
            this.contextMenuId = menuId;
            this.handleContextMenu = handler;
        }
    }
}
