using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>

    public class CurveProperty : IShowPropertyImpl
    {
        private ICurve curve;
        private new bool IsSelected;
        private string contextMenuId; // MenueId für ContextMenu
        private ICommandHandler handleContextMenu;
        public CurveProperty(ICurve curve)
        {
            this.curve = curve;
            IsSelected = false;
            base.resourceIdInternal = "Curve.Object"; // kommt nicht dran, oder?
        }
        public void SetSelected(bool IsSelected)
        {
            this.IsSelected = IsSelected;
        }
        public delegate void SelectionChangedDelegate(CurveProperty cp, ICurve selectedCurve);
        public event SelectionChangedDelegate SelectionChangedEvent;
        #region IShowPropertyImpl Overrides
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Selectable;
                if (IsSelected) res |= ShowPropertyLabelFlags.Selected;
                if (contextMenuId != null) res |= ShowPropertyLabelFlags.ContextMenu;
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
        public override string LabelText
        {
            get
            {
                if (curve != null) return curve.Description;
                return base.LabelText;
            }
            set
            {
                base.LabelText = value;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Selected"/>
        /// </summary>
        public override void Selected()
        {
            this.IsSelected = true;
            if (SelectionChangedEvent != null)
            {
                SelectionChangedEvent(this, curve);
            }
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

        #endregion
    }
}
