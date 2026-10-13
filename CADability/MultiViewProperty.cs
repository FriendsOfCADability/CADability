

namespace CADability.UserInterface
{
    /// <summary>
    /// Property entry that groups the property entries of several views (<see cref="IView"/>) of a project under one
    /// title in the property grid and provides the context menu for managing the views ("MenuId.ViewProperty").
    /// </summary>

    public class MultiViewProperty : IShowPropertyImpl
    {
        private IView[] condorViews;
        private IFrame frame;
        private IShowProperty[] subEntries;
        public MultiViewProperty(IView[] condorViews, IFrame frame)
        {
            this.condorViews = condorViews;
            this.frame = frame;
            base.resourceIdInternal = "MultiViewProperty";
        }
        #region IShowProperty Members
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.ViewProperty", false, frame.Project);
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
        /// <inheritdoc/>
        public override int SubEntriesCount
        {
            get
            {
                return condorViews.Length;
            }
        }
        /// <inheritdoc/>
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (subEntries == null)
                {
                    subEntries = new IShowProperty[condorViews.Length];
                    for (int i = 0; i < condorViews.Length; ++i)
                    {
                        subEntries[i] = condorViews[i].GetShowProperties(frame);
                    }
                }
                return subEntries;
            }
        }
        public override void Removed(IPropertyTreeView propertyTreeView)
        {
            base.Removed(propertyTreeView);
            subEntries = null;
            frame = null;
        }
        #endregion
    }
}
