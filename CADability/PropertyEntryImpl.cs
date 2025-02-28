using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CADability.UserInterface
{
    public abstract class PropertyEntryImpl : IPropertyEntry, IShowProperty
    {
        protected string resourceIdInternal;
        private string labelText;
        private IFrame frame;
        private readonly IShowPropertyImpl dumy = null; // will never be set. Dummy implementation of IShowProperty

        public PropertyEntryImpl()
        {

        }
        public PropertyEntryImpl(IFrame Frame)
        {
            frame = Frame;
        }
        /// <summary>
        /// Provides the resource id for the label text
        /// </summary>
        /// <param name="resourceId"></param>
        public PropertyEntryImpl(string resourceId)
        {
            this.resourceIdInternal = resourceId;
        }
        protected void PropertyEntryChangedState(StateChangedArgs args)
        {
            PropertyEntryChangedStateEvent?.Invoke(this, args);
        }
        protected IFrame Frame
        {
            get
            {
                if (frame != null) return frame;
                if (propertyPage != null && propertyPage.Frame != null) return propertyPage.Frame;
                if (Parent != null && Parent is PropertyEntryImpl pei) return pei.Frame;
                if (Parent != null && Parent is IPropertyPage pp) return pp.Frame;
                return ActiveFrame.Frame;
            }
            set
            {
                frame = value;
            }
        }
        public virtual void Refresh()
        {
            propertyPage?.Refresh(this);
        }
        #region IPropertyEntry implementation
        protected IPropertyPage propertyPage;
        private bool isOpen;
        public bool IsOpen
        {
            get => isOpen;
            set
            {
                isOpen = value;
                Opened(isOpen);
            }
        }

        public abstract PropertyEntryType Flags { get; }

        public string Label
        {
            get
            {
                if (LabelText != null) return LabelText;
                if (resourceIdInternal != null) return StringTable.GetString(resourceIdInternal);
                throw new NotImplementedException("A label text must be provided");
            }
        }
        public virtual string LabelText
        {
            get
            {
                return labelText;
            }
            set
            {
                labelText = value;
            }
        }

        /// <summary>
        /// Must be overridden, when Flags contains PropertyEntryType.ValueEditable
        /// </summary>
        public virtual string Value => null;

        public virtual string ResourceId
        {
            get
            {
                return resourceIdInternal;
            }
        }

        public object Parent { get; set; }
        public int Index { get; set; }
        public int IndentLevel { get; set; }

        /// <summary>
        /// Must be overridden, when Flags contains PropertyEntryType.HasSubEntries. See <see cref="IPropertyEntry.SubItems"/>
        /// </summary>
        public virtual IPropertyEntry[] SubItems => null;

        /// <summary>
        /// Must be overridden, when Flags contains PropertyEntryType.ContextMenu. See <see cref="IPropertyEntry.ContextMenu"/>
        /// </summary>
        public virtual MenuWithHandler[] ContextMenu => null;

        public virtual bool DeferUpdate { get; set; }

        public event PropertyEntryChangedStateDelegate PropertyEntryChangedStateEvent;

        public virtual void Added(IPropertyPage pp)
        {
            propertyPage = pp;
            PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.Added));
        }

        /// <summary>
        /// Must be overridden, when Flags contains PropertyEntryType.HasSpinButton or CancelButton or OKButton
        /// </summary>
        /// <param name="button"></param>
        public virtual void ButtonClicked(PropertyEntryButton button)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// May be overridden when Flags contains PropertyEntryType.ValueEditable. See <see cref="IPropertyEntry.EditTextChanged(string)"/>,
        /// </summary>
        /// <param name="newValue"></param>
        /// <returns></returns>
        public virtual bool EditTextChanged(string newValue)
        {
            return true; // standard: don't care
        }

        /// <summary>
        /// Must be overridden when Flags contains PropertyEntryType.ValueEditable. See <see cref="IPropertyEntry.EndEdit(bool, bool, string)"/>
        /// </summary>
        /// <param name="aborted"></param>
        /// <param name="modified"></param>
        /// <param name="newValue"></param>
        public virtual void EndEdit(bool aborted, bool modified, string newValue)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Must be overridden when Flags contains PropertyEntryType.DropDown, See <see cref="IPropertyEntry.GetDropDownList"/>
        /// </summary>
        /// <returns></returns>
        public virtual string[] GetDropDownList()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Must be overridden when Flags contains PropertyEntryType.DropDown
        /// </summary>
        /// <param name="selectedIndex"></param>
        public virtual void ListBoxSelected(int selectedIndex)
        {
            throw new NotImplementedException();
        }

        public virtual void Opened(bool isOpen)
        {
            if (IsOpen)
            {
                PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.OpenSubEntries));
            }
            else
            {
                PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.CollapseSubEntries));
            }
            if (IsOpen)
            {
                foreach (IPropertyEntry sub in SubItems)
                {
                    sub.Parent = this;
                    if (ReadOnly) sub.ReadOnly = true;
                }
            }
        }

        public virtual int OpenOrCloseSubEntries()
        {
            if (IsOpen)
            {
                int n = SubItems.Length;
                IsOpen = false;
                return -n;
            }
            else
            {
                IsOpen = true;
                return SubItems.Length;
            }
        }

        public virtual void Removed(IPropertyPage pp)
        {
            propertyPage?.Frame?.Project?.Undo.ClearContext(); // not the best place to do this here, but needed in the following scenario:
            // a property of an IGeoObject is being edited and the user clicks into the canvas, which in turn removes this property without ending the context
            PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.Removed));
            propertyPage = null;
        }

        public virtual void Select()
        {
            propertyPage?.SelectEntry(this);
        }

        public virtual void Selected(IPropertyEntry previousSelected)
        {
            PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.Selected));
        }

        /// <summary>
        /// May be overridden, when Flags contains PropertyEntryType.ValueEditable or PropertyEntryType.LabelEditable to handle which part is being edited
        /// </summary>
        /// <param name="editValue"></param>
        public virtual void StartEdit(bool editValue)
        {

        }

        public virtual void UnSelected(IPropertyEntry nowSelected)
        {
            PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.UnSelected));
        }
        public virtual bool ReadOnly { get; set; }
        public virtual IPropertyEntry FindSubItem(string helpResourceID)
        {
            if (this.ResourceId == helpResourceID || (string.IsNullOrEmpty(ResourceId) && labelText == helpResourceID)) return this;
            if (SubItems != null)
            {
                for (int i = 0; i < SubItems.Length; i++)
                {
                    SubItems[i].Parent = this; // this is necessary here: if the subitems have not been opened yet, the parent has not been set. this search goes throu all subitems, open or not
                    IPropertyEntry found = SubItems[i].FindSubItem(helpResourceID);
                    if (found != null) return found;
                }
            }
            return null;
        }
        public virtual bool IsLocked { get => false; set => throw new NotImplementedException(); }

        #endregion

        #region dumy implementation of IShowProperty. Will be removed when IShowProperty is eliminated. dumy is always null
        public event StateChangedDelegate StateChangedEvent
        {
            add
            {
                this.PropertyEntryChangedStateEvent += delegate (IPropertyEntry sender, StateChangedArgs args)
                {
                    value(this, args);
                };
            }
            remove
            {
                this.PropertyEntryChangedStateEvent -= delegate (IPropertyEntry sender, StateChangedArgs args)
                {
                    value(this, args);
                };
            }
        }
        string IShowProperty.LabelText => Label;
        string IShowProperty.HelpLink => ((IShowProperty)dumy).HelpLink;
        string IShowProperty.InfoText => ((IShowProperty)dumy).InfoText;
        ShowPropertyLabelFlags IShowProperty.LabelType => ((IShowProperty)dumy).LabelType;
        ShowPropertyEntryType IShowProperty.EntryType => ((IShowProperty)dumy).EntryType;
        public int SubEntriesCount => SubEntries == null ? 0 : SubEntries.Length;
        public IShowProperty[] SubEntries
        {
            get
            {
                IShowProperty[] res = new IShowProperty[SubItems.Length];
                for (int i = 0; i < res.Length; i++)
                {
                    res[i] = SubItems[i] as IShowProperty;
                }
                return res;
            }
        }
        int IShowProperty.OwnerDrawHeight => ((IShowProperty)dumy).OwnerDrawHeight;
        bool IShowProperty.Hidden { get => ((IShowProperty)dumy).Hidden; set => ((IShowProperty)dumy).Hidden = value; }
        bool IShowProperty.IsSelected { get => ((IShowProperty)dumy).IsSelected; set => ((IShowProperty)dumy).IsSelected = value; }
        void IShowProperty.Added(IPropertyTreeView propertyTreeView)
        {
            ((IShowProperty)dumy).Added(propertyTreeView);
        }
        void IShowProperty.Selected()
        {
            ((IShowProperty)dumy).Selected();
        }
        void IShowProperty.UnSelected()
        {
            ((IShowProperty)dumy).UnSelected();
        }
        void IShowProperty.ChildSelected(IShowProperty theSelectedChild)
        {
            ((IShowProperty)dumy).ChildSelected(theSelectedChild);
        }

        public static IPropertyEntry[] Concat(IPropertyEntry[] left, IPropertyEntry[] right)
        {
            IPropertyEntry[] res = new IPropertyEntry[left.Length + right.Length];
            Array.Copy(left, 0, res, 0, left.Length);
            Array.Copy(right, 0, res, left.Length, right.Length);
            return res;
        }

        void IShowProperty.Removed(IPropertyTreeView propertyTreeView)
        {
            ((IShowProperty)dumy).Removed(propertyTreeView);
        }
        void IShowProperty.ShowOpen(bool open)
        {
            ((IShowProperty)dumy).ShowOpen(open);
        }
        void IShowProperty.SetTabIndex(ref int TabIndex)
        {
            ((IShowProperty)dumy).SetTabIndex(ref TabIndex);
        }
        void IShowProperty.LabelChanged(string NewText)
        {
            // ((IShowProperty)dumy).LabelChanged(NewText);
        }
        void IShowProperty.SetFocus()
        {
            // if (dumy is IPropertyEntry pe) pe.SetFocus();
        }
        void IShowProperty.Refresh()
        {
            ((IShowProperty)dumy).Refresh();
        }
        void IShowProperty.OnEnterPressed()
        {
            ((IShowProperty)dumy).OnEnterPressed();
        }
        void IShowProperty.OnVisibilityChanged(bool isVisible)
        {
            ((IShowProperty)dumy).OnVisibilityChanged(isVisible);
        }
        #endregion
    }
}
