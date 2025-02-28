using CADability.GeoObject;
using CADability.Substitutes;
using System;
using System.Collections.Generic;


namespace CADability.UserInterface
{
    internal class GeoObjectListProperty : IShowPropertyImpl, ICommandHandler
    {
        ListWithEvents<IGeoObject> list;
        string contextMenuId;
        ICommandHandler contextHandler;
        public GeoObjectListProperty(ListWithEvents<IGeoObject> list, string resourceId)
        {
            this.list = list;
            base.resourceIdInternal = resourceId;
        }
        public GeoObjectListProperty(ListWithEvents<IGeoObject> list, string resourceId, string contextMenuId, ICommandHandler contextHandler)
        {
            this.list = list;
            base.resourceIdInternal = resourceId;
            this.contextMenuId = contextMenuId;
            this.contextHandler = contextHandler;
        }
        #region IShowProperty implementation
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.AllowDrag;
                if (contextHandler != null)
                {
                    res |= ShowPropertyLabelFlags.ContextMenu;
                }
                return res;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                if (contextMenuId != null)
                {
                    return MenuResource.LoadMenuDefinition(contextMenuId, false, contextHandler);
                }
                return null;

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
        IShowProperty[] subEntries;
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
                    List<IShowProperty> res = new List<IShowProperty>();
                    foreach (IGeoObject go in list)
                    {
                        SimpleNameProperty sp = new SimpleNameProperty(go.Description, go, base.resourceIdInternal, "MenuId.GeoObjectList");
                        sp.OnCommandEvent += new SimpleNameProperty.OnCommandDelegate(OnCommand);
                        sp.OnUpdateCommandEvent += new SimpleNameProperty.OnUpdateCommandDelegate(OnUpdateCommand);
                        res.Add(sp);
                    }
                    subEntries = res.ToArray();
                }
                return subEntries;
            }
        }
        void OnFocusChanged(IPropertyTreeView sender, IShowProperty NewFocus, IShowProperty OldFocus)
        {
            if (sender.FocusLeft(this, OldFocus, NewFocus))
            {
            }
            else if (sender.FocusEntered(this, OldFocus, NewFocus))
            {
                if (NewFocus == this)
                {
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        av.SetSelectedObjects(new GeoObjectList(list.ToArray()));
                    }
                }
                else if (NewFocus is SimpleNameProperty)
                {
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        av.SetSelectedObject((NewFocus as SimpleNameProperty).AssociatedObject as IGeoObject);
                    }
                }
            }
            else
            {   // möglicherweise Änderung innerhalb der Child objects
                if (propertyTreeView.GetParent(NewFocus) == this)
                {
                    if (NewFocus is SimpleNameProperty)
                    {
                        if (Frame.ActiveView is AnimatedView)
                        {
                            AnimatedView av = Frame.ActiveView as AnimatedView;
                            av.SetSelectedObject((NewFocus as SimpleNameProperty).AssociatedObject as IGeoObject);
                        }
                    }
                }
            }
        }
        bool OnUpdateCommand(SimpleNameProperty sender, string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.GeoObjectList.Remove":
                    return true;
                case "MenuId.GeoObjectList.Show":
                    return true;
            }
            return false;
        }
        bool OnCommand(SimpleNameProperty sender, string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.GeoObjectList.Remove":
                    list.Remove(sender.AssociatedObject as IGeoObject);
                    return true;
                case "MenuId.GeoObjectList.Show":
                    if (Frame.ActiveView is AnimatedView)
                    {
                        AnimatedView av = Frame.ActiveView as AnimatedView;
                        av.SetSelectedObject(sender.AssociatedObject as IGeoObject);
                    }
                    return true;
            }
            return false;
        }
        //public override void OnDragDrop(DragEventArgs drgevent)
        //{
        //    drgevent.Effect = DragDropEffects.Link;
        //    if (isDragging && Frame.ActiveView is AnimatedView)
        //    {
        //        AnimatedView av = Frame.ActiveView as AnimatedView;
        //        foreach (IGeoObject go in av.DraggingObjects)
        //        {
        //            if (!list.Contains(go))
        //            {
        //                list.Add(go);
        //            }
        //        }
        //        subEntries = null;
        //        propertyTreeView.Refresh(this);
        //        propertyTreeView.OpenSubEntries(this, true);
        //    }
        //}
        //public override void OnDragEnter(DragEventArgs drgevent)
        //{
        //    IDataObject data = drgevent.Data;
        //    if ((Frame.ActiveView is AnimatedView) && data.GetDataPresent(System.Windows.Forms.DataFormats.Serializable))
        //    {
        //        GeoObjectList goL = data.GetData(System.Windows.Forms.DataFormats.Serializable) as GeoObjectList;
        //        if (goL != null && goL.Count > 0)
        //        {
        //            drgevent.Effect = System.Windows.Forms.DragDropEffects.Link;
        //            isDragging = true;
        //        }
        //    }
        //}
        //public override void OnDragOver(System.Windows.Forms.DragEventArgs drgevent)
        //{
        //    if (isDragging)
        //    {
        //        drgevent.Effect = System.Windows.Forms.DragDropEffects.Link;
        //    }
        //}
        //public override void OnDragLeave(EventArgs e)
        //{
        //    isDragging = false;
        //}
#endregion
#region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }
}
