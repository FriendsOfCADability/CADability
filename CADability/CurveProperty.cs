using CADability.GeoObject;

namespace CADability.UserInterface
{
    /// <summary>
    /// Property entry (a selectable group title) which represents a single <see cref="ICurve"/> in the property grid. It shows the
    /// description of the curve as its label and raises <see cref="SelectionChangedEvent"/> when the user selects it. Used by
    /// <see cref="CurvesProperty"/> to list the curves a curve input of a construction has found under the mouse.
    /// </summary>

    public class CurveProperty : IShowPropertyImpl
    {
        private ICurve curve;
        private new bool IsSelected;
        private string contextMenuId; // MenueId für ContextMenu
        private ICommandHandler handleContextMenu;
        /// <summary>
        /// Creates the entry for <paramref name="curve"/>; the description of the curve (<see cref="IGeoObject.Description"/>)
        /// becomes its label.
        /// </summary>
        /// <param name="curve">The curve represented by this entry</param>
        public CurveProperty(ICurve curve)
        {
            this.curve = curve;
            IsSelected = false;
            base.resourceIdInternal = "Curve.Object"; // kommt nicht dran, oder?
        }
        /// <summary>
        /// Sets whether the entry is displayed as selected (<see cref="ShowPropertyLabelFlags.Selected"/>) without raising
        /// <see cref="SelectionChangedEvent"/>. <see cref="CurvesProperty"/> uses it to mark the current curve.
        /// </summary>
        /// <param name="IsSelected">true to display the entry as selected</param>
        public void SetSelected(bool IsSelected)
        {
            this.IsSelected = IsSelected;
        }
        /// <summary>
        /// Delegate for <see cref="SelectionChangedEvent"/>.
        /// </summary>
        /// <param name="cp">The entry that was selected</param>
        /// <param name="selectedCurve">The curve represented by that entry</param>
        public delegate void SelectionChangedDelegate(CurveProperty cp, ICurve selectedCurve);
        /// <summary>
        /// Raised when the user selects this entry in the property page.
        /// </summary>
        public event SelectionChangedDelegate SelectionChangedEvent;
        #region IShowPropertyImpl Overrides
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.GroupTitle;
            }
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override void Selected()
        {
            this.IsSelected = true;
            if (SelectionChangedEvent != null)
            {
                SelectionChangedEvent(this, curve);
            }
        }
        /// <inheritdoc/>
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
