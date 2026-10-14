using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CADability.UserInterface
{
    /// <summary>
    /// The base class for entries of the property page (<see cref="IPropertyEntry"/>): it keeps the property page, the
    /// parent, the index and the open state of the entry, raises <see cref="PropertyEntryChangedStateEvent"/> and
    /// provides a default (mostly empty or throwing) implementation of the notifications. A derived class provides
    /// <see cref="Flags"/> and a label (<see cref="LabelText"/> or a resource id) and overrides what its flags announce:
    /// <see cref="Value"/> and the edit methods for an editable value, <see cref="SubItems"/> for sub entries,
    /// <see cref="ContextMenu"/>, <see cref="GetDropDownList"/> and so on. The entries of CADability (e.g.
    /// <see cref="GeoPointProperty"/>, <see cref="MultipleChoiceProperty"/>, <see cref="ShowPropertyGroup"/>) derive
    /// from it. It replaces the older <see cref="IShowPropertyImpl"/> and implements <see cref="IShowProperty"/> only
    /// as far as the property page still needs it.
    /// </summary>
    public abstract class PropertyEntryImpl : IPropertyEntry, IShowProperty
    {
        /// <summary>
        /// The resource id of the entry: <see cref="Label"/> looks it up in the <see cref="StringTable"/> when no
        /// <see cref="LabelText"/> is set, and <see cref="ResourceId"/> returns it. Set by the constructor
        /// <see cref="PropertyEntryImpl(string)"/> or by the derived class.
        /// </summary>
        protected string resourceIdInternal;
        private string labelText;
        private IFrame frame;
        private readonly IShowPropertyImpl dumy = null; // will never be set. Dummy implementation of IShowProperty

        /// <summary>
        /// Creates the entry without a frame and without a resource id; the derived class sets
        /// <see cref="resourceIdInternal"/> or <see cref="LabelText"/> itself.
        /// </summary>
        public PropertyEntryImpl()
        {

        }
        /// <summary>
        /// Creates the entry with the frame it belongs to, which <see cref="Frame"/> returns even while the entry is
        /// not shown in a property page.
        /// </summary>
        /// <param name="Frame">the frame</param>
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
        /// <summary>
        /// Raises <see cref="PropertyEntryChangedStateEvent"/> with the given arguments. The notifications of this class
        /// (<see cref="Added(IPropertyPage)"/>, <see cref="Removed(IPropertyPage)"/>, <see cref="Opened(bool)"/>, <see cref="Selected(IPropertyEntry)"/>,
        /// <see cref="UnSelected(IPropertyEntry)"/>) use it; a derived class may report further changes, e.g.
        /// <see cref="StateChangedArgs.State.EditEnded"/>.
        /// </summary>
        /// <param name="args">the change to report</param>
        protected void PropertyEntryChangedState(StateChangedArgs args)
        {
            PropertyEntryChangedStateEvent?.Invoke(this, args);
        }
        /// <summary>
        /// The frame the entry belongs to: the frame given to the constructor, otherwise the frame of the property page
        /// the entry is shown in, otherwise the frame of its parent entry or page, and as a last resort the active
        /// frame (<see cref="ActiveFrame.Frame"/>). Setting it overrides this lookup.
        /// </summary>
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
        /// <summary>
        /// Asks the property page to redisplay this entry (see <see cref="IPropertyPage.Refresh"/>); call it after the
        /// label, the value or the sub entries changed. Does nothing while the entry is not shown.
        /// </summary>
        public virtual void Refresh()
        {
            propertyPage?.Refresh(this);
        }
        #region IPropertyEntry implementation
        /// <summary>
        /// The property page the entry is currently shown in, set by <see cref="Added(IPropertyPage)"/> and cleared by
        /// <see cref="Removed(IPropertyPage)"/>. Null while the entry is not shown.
        /// </summary>
        protected IPropertyPage propertyPage;
        private bool isOpen;
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.IsOpen"/>. Setting it calls <see cref="Opened(bool)"/>; the property
        /// page sets it (via <see cref="OpenOrCloseSubEntries"/>) when the user opens or closes the entry.
        /// </summary>
        public bool IsOpen
        {
            get => isOpen;
            set
            {
                isOpen = value;
                Opened(isOpen);
            }
        }

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Flags"/>: the derived class states what the entry is and what the
        /// user can do with it. The flags must match the overridden members, e.g. <see cref="PropertyEntryType.HasSubEntries"/>
        /// requires <see cref="SubItems"/>, <see cref="PropertyEntryType.ValueEditable"/> requires <see cref="Value"/> and
        /// <see cref="EndEdit"/>, <see cref="PropertyEntryType.DropDown"/> requires <see cref="GetDropDownList"/> and
        /// <see cref="ListBoxSelected"/>.
        /// </summary>
        public abstract PropertyEntryType Flags { get; }

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Label"/>: returns <see cref="LabelText"/> when it is set, otherwise
        /// the string of the <see cref="StringTable"/> for <see cref="resourceIdInternal"/>. Throws a
        /// <see cref="NotImplementedException"/> when neither is set.
        /// </summary>
        public string Label
        {
            get
            {
                if (LabelText != null) return LabelText;
                if (resourceIdInternal != null) return StringTable.GetString(resourceIdInternal);
                throw new NotImplementedException("A label text must be provided");
            }
        }
        /// <summary>
        /// An explicit label text. When set (not null) it is shown instead of the text from the <see cref="StringTable"/>;
        /// use it for entries whose label is not a fixed resource string, e.g. the name of an item. Call <see cref="Refresh()"/>
        /// after changing it while the entry is displayed.
        /// </summary>
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

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.ResourceId"/>: returns <see cref="resourceIdInternal"/>. The
        /// property page looks up the tool tip of the entry in the <see cref="StringTable"/> with it, hides entries by
        /// it (<see cref="IControlCenter.HideEntry"/>) and <see cref="FindSubItem"/> identifies the entry by it.
        /// </summary>
        public virtual string ResourceId
        {
            get
            {
                return resourceIdInternal;
            }
        }

        /// <inheritdoc/>
        public object Parent { get; set; }
        /// <inheritdoc/>
        public int Index { get; set; }
        /// <inheritdoc/>
        public int IndentLevel { get; set; }

        /// <summary>
        /// Must be overridden, when Flags contains PropertyEntryType.HasSubEntries. See <see cref="IPropertyEntry.SubItems"/>
        /// </summary>
        public virtual IPropertyEntry[] SubItems => null;

        /// <summary>
        /// Must be overridden, when Flags contains PropertyEntryType.ContextMenu. See <see cref="IPropertyEntry.ContextMenu"/>
        /// </summary>
        public virtual MenuWithHandler[] ContextMenu => null;

        /// <inheritdoc/>
        public virtual bool DeferUpdate { get; set; }

        /// <inheritdoc/>
        public event PropertyEntryChangedStateDelegate PropertyEntryChangedStateEvent;

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Added"/>: memorizes the page in <see cref="propertyPage"/> and
        /// reports <see cref="StateChangedArgs.State.Added"/>. Override it to subscribe to events while the entry is
        /// displayed, and call the base implementation.
        /// </summary>
        /// <param name="pp">the property page the entry was added to</param>
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
        /// Implementation of <see cref="IPropertyEntry.GetErrorText"/>: returns an empty string. Override it together
        /// with <see cref="EditTextChanged"/> to explain why a text was rejected.
        /// </summary>
        /// <returns>the error text, empty by default</returns>
        public virtual string GetErrorText()
        {
            return "";
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

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Opened"/>: reports <see cref="StateChangedArgs.State.OpenSubEntries"/>
        /// or <see cref="StateChangedArgs.State.CollapseSubEntries"/> and, when the entry was opened, makes it the
        /// <see cref="IPropertyEntry.Parent"/> of its <see cref="SubItems"/> and passes <see cref="ReadOnly"/> on to them.
        /// </summary>
        /// <param name="isOpen">true: the entry was opened, false: it was closed</param>
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

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.OpenOrCloseSubEntries"/>: toggles <see cref="IsOpen"/>, which
        /// notifies the entry via <see cref="Opened(bool)"/>. Requires <see cref="SubItems"/> to be non-null.
        /// </summary>
        /// <returns>the number of sub items that became visible, or the negative number of sub items that were hidden</returns>
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

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Removed"/>: closes the undo context of the project (an edit may
        /// have been interrupted by removing the entry), reports <see cref="StateChangedArgs.State.Removed"/> and forgets
        /// the page. Override it to unsubscribe from events, and call the base implementation.
        /// </summary>
        /// <param name="pp">the property page the entry was removed from</param>
        public virtual void Removed(IPropertyPage pp)
        {
            propertyPage?.Frame?.Project?.Undo.ClearContext(); // not the best place to do this here, but needed in the following scenario:
            // a property of an IGeoObject is being edited and the user clicks into the canvas, which in turn removes this property without ending the context
            PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.Removed));
            propertyPage = null;
        }

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Select"/>: selects this entry in its property page. Does nothing
        /// while the entry is not shown; the entry must be displayed, i.e. its parents must be open.
        /// </summary>
        public virtual void Select()
        {
            propertyPage?.SelectEntry(this);
        }

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Selected"/>: reports <see cref="StateChangedArgs.State.Selected"/>.
        /// </summary>
        /// <param name="previousSelected">the entry that was selected before, may be null</param>
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

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.UnSelected"/>: reports <see cref="StateChangedArgs.State.UnSelected"/>.
        /// </summary>
        /// <param name="nowSelected">the entry that is selected instead, may be null</param>
        public virtual void UnSelected(IPropertyEntry nowSelected)
        {
            PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.UnSelected));
        }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.ReadOnly"/>. When the entry is opened, a read only entry makes
        /// its <see cref="SubItems"/> read only as well.
        /// </summary>
        public virtual bool ReadOnly { get; set; }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.FindSubItem"/>: returns this entry when its <see cref="ResourceId"/>
        /// (or, when it has none, its <see cref="LabelText"/>) equals <paramref name="helpResourceID"/>, otherwise
        /// searches the <see cref="SubItems"/> recursively, whether they are open or not, setting their parent on the way.
        /// </summary>
        /// <param name="helpResourceID">the resource id (or label text) to look for</param>
        /// <returns>the entry found, or null</returns>
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
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.IsLocked"/>: by default the entry is never locked, and setting it
        /// throws a <see cref="NotImplementedException"/>. An entry with <see cref="PropertyEntryType.Lockable"/> in its
        /// <see cref="Flags"/> overrides it (see <see cref="EditableProperty{T}"/>).
        /// </summary>
        public virtual bool IsLocked { get => false; set => throw new NotImplementedException(); }

        #endregion

        #region dumy implementation of IShowProperty. Will be removed when IShowProperty is eliminated. dumy is always null
        /// <summary>
        /// Implementation of <see cref="IShowProperty.StateChangedEvent"/> for code that still uses the older interface:
        /// a handler added here is called whenever <see cref="PropertyEntryChangedStateEvent"/> is raised, and removing
        /// the handler removes the forwarder that was added for it.
        /// </summary>
        public event StateChangedDelegate StateChangedEvent
        {
            add
            {
                if (value == null) return;
                PropertyEntryChangedStateDelegate forwarder = (sender, args) => value(this, args);
                if (stateChangedForwarders == null) stateChangedForwarders = new List<KeyValuePair<StateChangedDelegate, PropertyEntryChangedStateDelegate>>();
                stateChangedForwarders.Add(new KeyValuePair<StateChangedDelegate, PropertyEntryChangedStateDelegate>(value, forwarder));
                this.PropertyEntryChangedStateEvent += forwarder;
            }
            remove
            {
                if (value == null || stateChangedForwarders == null) return;
                int i = stateChangedForwarders.FindLastIndex(p => p.Key.Equals(value));
                if (i < 0) return;
                this.PropertyEntryChangedStateEvent -= stateChangedForwarders[i].Value;
                stateChangedForwarders.RemoveAt(i);
            }
        }
        private List<KeyValuePair<StateChangedDelegate, PropertyEntryChangedStateDelegate>> stateChangedForwarders; // handler -> forwarder, for the remove accessor
        string IShowProperty.LabelText => Label;
        string IShowProperty.HelpLink => ((IShowProperty)dumy).HelpLink;
        string IShowProperty.InfoText => ((IShowProperty)dumy).InfoText;
        ShowPropertyLabelFlags IShowProperty.LabelType => ((IShowProperty)dumy).LabelType;
        ShowPropertyEntryType IShowProperty.EntryType => ((IShowProperty)dumy).EntryType;
        /// <summary>
        /// Implementation of <see cref="IShowProperty.SubEntriesCount"/>: the number of <see cref="SubItems"/>, which
        /// must not be null.
        /// </summary>
        public int SubEntriesCount => SubEntries == null ? 0 : SubEntries.Length;
        /// <summary>
        /// Implementation of <see cref="IShowProperty.SubEntries"/>: the <see cref="SubItems"/> as <see cref="IShowProperty"/>
        /// objects (null for an item that does not implement it). <see cref="SubItems"/> must not be null.
        /// </summary>
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

        /// <summary>
        /// Concatenates two arrays of entries, e.g. the geometric properties of an object and its attribute properties,
        /// into one array to return from <see cref="SubItems"/>.
        /// </summary>
        /// <param name="left">the first entries</param>
        /// <param name="right">the entries that follow</param>
        /// <returns>a new array with the entries of <paramref name="left"/> followed by those of <paramref name="right"/></returns>
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
