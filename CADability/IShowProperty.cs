using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace CADability.UserInterface
{
    /// <summary>
    /// Called with <paramref name="startModifying"/> true when the user presses the mouse button to modify a value (e.g. a
    /// point) with the mouse; <paramref name="sender"/> identifies the property, so that the value can be changed without
    /// knowing the object. Called with <paramref name="startModifying"/> false when the user types into the edit field
    /// instead: the mouse action for this value must then be cancelled.
    /// </summary>
    /// <param name="sender">the property entry whose value is modified</param>
    /// <param name="startModifying">true: the modification with the mouse starts, false: it is cancelled</param>
    public delegate void ModifyWithMouseDelegate(IPropertyEntry sender, bool startModifying);
    public delegate void EditingDelegate(IShowProperty sender, char keyPressed);
    public delegate void FocusChangedDelegate(IPropertyTreeView sender, IShowProperty newFocus, IShowProperty oldFocus);

    public delegate void PropertyEntryChangedStateDelegate(IPropertyEntry sender, StateChangedArgs args);
    public delegate void StateChangedDelegate(IShowProperty sender, StateChangedArgs args);

    public class StateChangedArgs : EventArgs
    {
        public enum State { OpenSubEntries, CollapseSubEntries, Selected, UnSelected, SubEntrySelected, Added, Removed, EditEnded }
        private State state;
        public StateChangedArgs(State state)
        {
            this.state = state;
        }
        public State EventState
        {
            get { return state; }
        }
        public IShowProperty SelectedChild;
    }

    public enum MouseButtonMode { MouseLocked, MouseInactive, MouseActive }

    /// <summary>
    /// Interface for IShowProperty objects, which implement MouseButton modification.
    /// </summary>

    public interface IConstructProperty
    {
        void SetFocus();
        GeoPoint GetHotspotPosition();
        void StartModifyWithMouse();
        void SetHotspotPosition(GeoPoint Here);
    }

    /// <summary>
    /// The kind of an entry in the property grid.
    /// </summary>
    public enum ShowPropertyEntryType
    {
        /// <summary>
        /// A simple group title, no control to manipulate that entry
        /// </summary>
        GroupTitle,
        /// <summary>
        /// A simple entry with a control (e.g. edit box)
        /// </summary>
        SimpleEntry,
        /// <summary>
        /// An owner drawn entry
        /// </summary>
        OwnerDrawEntry,
        /// <summary>
        /// A separator
        /// </summary>
        Seperator,
        /// <summary>
        /// Do not use
        /// </summary>
        Command,
        /// <summary>
        /// A control that uses the whole space, not the split layout with a label on the left side.
        /// </summary>
        Control
    };
    /// <summary>
    /// How the label of an entry in the property page behaves: whether it can be selected, whether its text can be
    /// edited, whether it is printed bold and whether a right click shows a context menu. The flags can be combined.
    /// </summary>
    [Flags]
    public enum ShowPropertyLabelFlags
    {
        /// <summary>
        /// Entry may be selected (almost ever true)
        /// </summary>
        Selectable = 1,
        /// <summary>
        /// Label may be edited, good for named values
        /// </summary>
        Editable = 2,
        /// <summary>
        /// Label in bold letters
        /// </summary>
        Bold = 4,
        /// <summary>
        /// Label in Highlight color (red)
        /// </summary>
        Highlight = 8,
        /// <summary>
        /// Label is selected
        /// </summary>
        Selected = 16,
        /// <summary>
        /// Label is checked
        /// </summary>
        Checked = 32,
        /// <summary>
        /// Label is a link, currently not supported
        /// </summary>
        Link = 64,
        /// <summary>
        /// There is a context menu for this entry
        /// </summary>
        ContextMenu = 128,
        /// <summary>
        /// A OK Button should be displayed for this entry (usually in the context of an action)
        /// </summary>
        OKButton = 256,
        /// <summary>
        /// A cancel button should be displayed for this entry (usually in the context of an action)
        /// </summary>
        CancelButton = 512,
        /// <summary>
        /// Allows the label to be destination of dradrop operations
        /// </summary>
        AllowDrag = 1024
    };

    /// <summary>
    /// Objects that implement this interface can be displayed
    /// </summary>

    public interface IShowProperty
    {
        // TODO: IShowProperty erweitern:
        // Es wäre schön, man könnte von einem IShowProperty direkt verlangen,
        // dass seine SubEntries öffnet bzw. schließt oder dass es selektiert wird.
        // z.Z. muss man sich immer ein IPropertyTreeView und dann dort die entsprechend
        // Methode aufrufen. Also:
        // void ShowOpen(bool open);
        // vois ShowSelected(); // hier kein Parameter
        // Die Methoden Opened bzw. Selected sind Notifikationen vom IPropertyTreeView 
        // Der event StateChanged gibt diese Notifikationen nach außen weiter.
        // Und dann bräuchte man noch IsOpen bzw. IsSelected um den Zustand abzufragen...

        /// <summary>
        /// Called when the entry was added to the control center or to a parent entry. The parameter
        /// represents the tree view object to which the entry was added. Each Added is later matched by a call to <see cref="Removed"/>.
        /// </summary>
        /// <param name="propertyTreeView">the tree view</param>
        void Added(IPropertyTreeView propertyTreeView);
        /// <summary>
        /// Called whent the entry was selected.
        /// </summary>
        void Selected();
        /// <summary>
        /// Called whent the entry was unselected.
        /// </summary>
        void UnSelected();
        /// <summary>
        /// Called when a child entry of this entry was selected.
        /// </summary>
        /// <param name="theSelectedChild"></param>
        void ChildSelected(IShowProperty theSelectedChild);
        /// <summary>
        /// Notifies the item that its subitems will be shown (IsOpen==true) or that
        /// the treeview collapses the subitems (IsOpen==false). The Item should fire the
        /// <see cref="StateChangedEvent"/> event with the appropriate parameters.
        /// </summary>
        /// <param name="IsOpen">Treeview was opened or collpsed</param>
        void Opened(bool IsOpen);
        /// <summary>
        /// Matches the calls to <see cref="Added"/>. A good place to disconnect events and free resources.
        /// </summary>
        /// <param name="propertyTreeView"></param>
        void Removed(IPropertyTreeView propertyTreeView);
        /// <summary>
        /// Event that is fired when the state of this entry changed, e.g. the entry was selected.
        /// </summary>
        event StateChangedDelegate StateChangedEvent;

        /// <summary>
        /// Opens this item to show the subentries of this item in the treeview, that contains this IShowProperty object.
        /// respectively closes or collapses this item to hide the subentries.
        /// </summary>
        /// <param name="open">true: open, false: collapse</param>
        void ShowOpen(bool open);
        /// <summary>
        /// Forces this item to be selected in the treeview. The item must be visible.
        /// </summary>
        void Select();

        /// <summary>
        /// Returns the label text, which is usually displayed on the left side of the control.
        /// </summary>
        string LabelText { get; }
        /// <summary>
        /// Returns the help link for the help control.
        /// </summary>
        string HelpLink { get; }
        /// <summary>
        /// Returns the text for the tooltip to display when the mouse cursor rests on the label.
        /// </summary>
        string InfoText { get; }
        /// <summary>
        /// Returns the type of the label, see <see cref="ShowPropertyLabelFlags"/>.
        /// </summary>
        ShowPropertyLabelFlags LabelType { get; }
        /// <summary>
        /// Returns the type of the entry, see <see cref="ShowPropertyEntryType"/>.
        /// </summary>
        ShowPropertyEntryType EntryType { get; }
        /// <summary>
        /// Returns the number of subentries if any or 0 if none.
        /// </summary>
        int SubEntriesCount { get; }
        /// <summary>
        /// Returns the array of subentries to this entry.
        /// </summary>
        IShowProperty[] SubEntries { get; }
        /// <summary>
        /// Returns the height of this entry if it is ownerdrawn.
        /// </summary>
        int OwnerDrawHeight { get; }
        /// <summary>
        /// Do not use anymore.
        /// </summary>
        /// <param name="TabIndex"></param>
        void SetTabIndex(ref int TabIndex);
        /// <summary>
        /// Will be called when the text of a label changed because it was edited by the user.
        /// Will only be used if <see cref="ShowPropertyLabelFlags.Editable"/>is specified in the <see cref="LabelType"/> property.
        /// </summary>
        /// <param name="NewText">the new text</param>
        void LabelChanged(string NewText);
        /// <summary>
        /// Will be called when the entry got the focus.
        /// </summary>
        void SetFocus();
        /// <summary>
        /// If an IShowProperty object is hidden, it will not appear in the TreeView
        /// of the control center. Most Properties are not hidden by default.
        /// </summary>
        bool Hidden { get; set; }
        /// <summary>
        /// If an IShowProperty object is read only, its value should be fixed. The subProperties should also be read only.
        /// </summary>
        bool ReadOnly { get; set; }
        /// <summary>
        /// Forces the entry to refresh its contents
        /// </summary>
        void Refresh();
        /// <summary>
        /// Alt+Enter was pressed in the controlcenter while this entry was selected
        /// </summary>
        void OnEnterPressed();
        void OnVisibilityChanged(bool isVisible);
        bool IsSelected { get; set; }
    }
    /// <summary>
    /// Deprecated: for new objects use <see cref="IPropertyEntry"/> instead.
    /// Deprecated standard implementation of <see cref="IShowProperty"/>. Implements many interface methods in a 
    /// standard way as a virtual method to give derived classes the possibility to override
    /// these implementations.
    /// </summary>
    public class IShowPropertyImpl : IShowProperty, IPropertyEntry
    {
        protected IPropertyPage propertyTreeView => propertyPage;
        /// <summary>
        /// Overrides the label text, which is normally retrieved from the <see cref="StringTable"/>
        /// with the <see cref="resourceIdInternal"/>
        /// </summary>
        protected string labelTextInternal;
        
        /// <summary>
        /// The resourceId specifies both the label text and the tooltip text. The label text
        /// is loaded from the <see cref="StringTable"/> with the ID resourceId+".Label" the 
        /// tooltip text is loaded with the id resourceId+".ShortInfo" or resourceId+".DetailedInfo".
        /// You may extend the StringTable and ues your own resourceId values according to this scheme. (see
        /// <see cref="StringTable.AddStrings(System.Xml.XmlDocument)"/>)
        /// </summary>
        protected string resourceIdInternal;
        private bool hidden; // default: false!
        private bool readOnly; // default: false!
        protected ShowPropertyLabelFlags flagsToSuppress = (ShowPropertyLabelFlags)0;
        public void SuppressFlags(ShowPropertyLabelFlags flagsToSuppress)
        {
            this.flagsToSuppress = flagsToSuppress;
        }
        private IFrame frame; // Ersatz, falls noch nicht im propertyTreeView angezeigt
        /// <summary>
        /// Returns a IFrame when this IShowProperty has been added to a <see cref="IPropertyTreeView"/>,
        /// returns null otherwise.
        /// </summary>
        public virtual IFrame Frame
        {
            get
            {
                if (propertyPage != null) frame = propertyPage.Frame;
                if (frame == null) frame = FrameImpl.MainFrame;
                return frame;
            }
            set
            {
                frame = value;
            }
        }
        protected IShowPropertyImpl(IFrame frame)
        {
            this.frame = frame;
        }
        protected IShowPropertyImpl()
        {
            this.frame = null;
        }

        event PropertyEntryChangedStateDelegate IPropertyEntry.PropertyEntryChangedStateEvent
        {
            add
            {
                this.StateChangedEvent += delegate (IShowProperty sender, StateChangedArgs args)
                {
                    value(this, args);
                };
            }
            remove
            {
                this.StateChangedEvent -= delegate (IShowProperty sender, StateChangedArgs args)
                {
                    value(this, args);
                };
            }
        }
        #region IShowProperty Members
        /* So ist die Lösung um interface methoden nicht public machen zu müssen
         * ist aber viel Schreibartbeit
         */
        void IShowProperty.Added(IPropertyTreeView propertyTreeView)
        {
            Added(propertyTreeView);
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.Added"/>.
        /// Momorizes the given <see cref="IPropertyTreeView"/>, raises the <see cref="IShowProperty.StateChangedEvent"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public virtual void Added(IPropertyTreeView propertyTreeView)
        {
            PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.Added));
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.Selected"/>, 
        /// raises the <see cref="IShowProperty.StateChangedEvent"/>
        /// </summary>
        public virtual void Selected()
        {
            PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.Selected));
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.UnSelected"/>, 
        /// raises the <see cref="IShowProperty.StateChangedEvent"/>
        /// </summary>
        public virtual void UnSelected()
        {
            PropertyEntryChangedState(new StateChangedArgs(StateChangedArgs.State.UnSelected));
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.ChildSelected"/>, 
        /// raises the <see cref="IShowProperty.StateChangedEvent"/>
        /// </summary>
        /// <param name="theSelectedChild">the now selected child property</param>
        public virtual void ChildSelected(IShowProperty theSelectedChild)
        {
            StateChangedArgs sca = new StateChangedArgs(StateChangedArgs.State.SubEntrySelected);
            sca.SelectedChild = theSelectedChild;
            PropertyEntryChangedState(sca);
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.Opened"/>, 
        /// raises the <see cref="IShowProperty.StateChangedEvent"/>
        /// </summary>
        /// <param name="IsOpen">true: now open, false: now closed</param>
        public virtual void Opened(bool IsOpen)
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
                foreach (IShowProperty sub in SubEntries)
                {
                    // readOnly vererbt sich auf alle Untereinträge
                    // aber ShowPropertyface ist z.B. nicht readonly, die Untereinträge aber wohl
                    // deshalb darf hier readOnly nicht auf false gesetzt werden
                    if (readOnly) sub.ReadOnly = true;
                }
            }
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.Removed"/>, 
        /// raises the <see cref="IShowProperty.StateChangedEvent"/>
        /// </summary>
        /// <param name="propertyTreeView">removed from this <see cref="IPropertyTreeView"/></param>
        public virtual void Removed(IPropertyTreeView propertyTreeView)
        {
            if (StateChangedEvent != null)
            {
                StateChangedEvent(this, new StateChangedArgs(StateChangedArgs.State.Removed));
            }
            if (toolTipInhibited)
            {   // fals es vergessen wurde
                toolTipInhibited = false;
            }
            propertyPage = null;
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.ShowOpen"/>, opens or closes this entry
        /// if it is currently contained in a <see cref="IPropertyTreeView"/>
        /// </summary>
        /// <param name="open">true: open, false: close</param>
        public virtual void ShowOpen(bool open)
        {
            propertyPage?.OpenSubEntries(this, open);
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.Select"/>, selects this entry
        /// if it is currently contained in a <see cref="IPropertyTreeView"/>
        /// </summary>
        public virtual void Select()
        {
            propertyPage?.SelectEntry(this);
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.LabelText"/>, returns either
        /// <see cref="labelTextInternal"/> (if not null) or <see cref="StringTable.GetString(string)"/>(resourceId+".Label")
        /// Whe set, <see cref="labelTextInternal"/> is set.
        /// </summary>
        public virtual string LabelText
        {
            get
            {
                if (labelTextInternal != null) return labelTextInternal;
                return StringTable.GetString(resourceIdInternal + ".Label");
            }
            set
            {
                labelTextInternal = value;
            }
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.InfoText"/>, returns 
        /// <see cref="StringTable.GetString(string)"/>(resourceId+".DetailedInfo") or 
        /// <see cref="StringTable.GetString(string)"/>(resourceId+".ShortInfo")
        /// </summary>
        public virtual string InfoText
        {
            get
            {
                if (resourceIdInternal != null) return StringTable.GetString(resourceIdInternal + ".DetailedInfo");
                return null;
            }
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.HelpLink"/>, returns 
        /// <see cref="resourceIdInternal"/>.
        /// </summary>
        public virtual string HelpLink { get { return resourceIdInternal; } }
        /// <summary>
        /// How the label of this entry behaves (see <see cref="ShowPropertyLabelFlags"/>): whether it can be selected or
        /// edited, is bold or has a context menu. The default implementation returns <see cref="ShowPropertyLabelFlags.Selectable"/>.
        /// </summary>
        public virtual ShowPropertyLabelFlags LabelType
        {
            get { return ShowPropertyLabelFlags.Selectable; }
        }
        /// <summary>
        /// The kind of entry (see <see cref="ShowPropertyEntryType"/>): a simple entry, a group title, an editable value...
        /// It decides how the property page displays the entry. The default implementation returns
        /// <see cref="ShowPropertyEntryType.SimpleEntry"/>.
        /// </summary>
        public virtual ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.SimpleEntry;
            }
        }
        /// <summary>
        /// The number of <see cref="SubEntries"/>, 0 for an entry that cannot be opened. The default implementation returns 0.
        /// </summary>
        public virtual int SubEntriesCount
        {
            get
            {
                return 0;
            }
        }
        /// <summary>
        /// The sub entries shown below this entry when it is opened in the property page. The default implementation has
        /// none and returns an empty array.
        /// </summary>
        public virtual IShowProperty[] SubEntries
        {
            get { return new IShowProperty[0]; }
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.OwnerDrawHeight"/>.
        /// Returns 0.
        /// </summary>
        public virtual int OwnerDrawHeight
        {
            get
            {
                return 0;
            }
        }
        void IShowProperty.SetTabIndex(ref int TabIndex) { }
        /// <inheritdoc/>
        public event StateChangedDelegate StateChangedEvent;
        /// <summary>
        /// Implementation of <see cref="IShowProperty.LabelChanged"/>. Override if <see cref="LabelType"/>
        /// is <see cref="ShowPropertyLabelFlags.Editable"/>.
        /// </summary>
        /// <param name="NewText">the new text</param>
        public virtual void LabelChanged(string NewText) { }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.SetFocus"/>. 
        /// Does nothing, override when needed.
        /// </summary>
        public virtual void SetFocus()
        {
        }
        /// <inheritdoc/>
        public virtual bool Hidden
        {
            get { return hidden; }
            set { hidden = value; } // TODO: hier noch PropertyTreeView benachrichtigen!
        }
        /// <inheritdoc/>
        public virtual bool ReadOnly
        {
            get { return readOnly; }
            set
            {
                readOnly = value;
                // nur merken, die subentries gibts ggf noch garnicht
            }
        }
        /// <inheritdoc/>
        public virtual void Refresh()
        {
        }
        /// <inheritdoc/>
        public virtual void OnEnterPressed()
        {
        }
        public virtual void OnVisibilityChanged(bool isVisible)
        {
        }
        public virtual bool IsSelected
        {
            get
            {
                return (propertyTreeView.GetCurrentSelection() == this);
            }
            set
            {
                if (value) propertyTreeView.SelectEntry(this);
                else
                {
                    if (propertyTreeView.GetCurrentSelection() == this)
                        propertyTreeView.SelectEntry(null);
                }
            }
        }
        private bool toolTipInhibited;
        #endregion
        #region Helper Methoden
        /// <summary>
        /// Static helper method to concatenate two arrays of <see cref="IShowProperty"/>.
        /// </summary>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static IShowProperty[] Concat(IShowProperty[] left, IShowProperty[] right)
        {
            IShowProperty[] res = new IShowProperty[left.Length + right.Length];
            Array.Copy(left, 0, res, 0, left.Length);
            Array.Copy(right, 0, res, left.Length, right.Length);
            return res;
        }
        /// <summary>
        /// Calls the Refresh method of several standard implementations of <see cref="IShowProperty"/>.
        /// </summary>
        /// <param name="sp"></param>
        public static void Update(IShowProperty sp)
        {
            // das würde besser in das IShowProperty interface gehören
            if (sp is GeoPointProperty)
            {
                (sp as GeoPointProperty).Refresh();
            }
            else if (sp is GeoVectorProperty)
            {
                (sp as GeoVectorProperty).Refresh();
            }
            else if (sp is LengthProperty)
            {
                (sp as LengthProperty).Refresh();
            }
            else if (sp is AngleProperty)
            {
                (sp as AngleProperty).AngleChanged();
            }
            if (sp is IShowPropertyImpl)
            {
                IShowPropertyImpl sp1 = (sp as IShowPropertyImpl);
                if (sp1.IsOpen)
                {
                    for (int j = 0; j < sp1.SubEntriesCount; ++j)
                    {
                        Update(sp1.SubEntries[j]);
                    }
                }
            }
            // wenn die nicht offen sind gibts hier Probleme
            // und überprüfen geht hier nicht
            //			for (int i=0; i<sp.SubEntriesCount; ++i)
            //			{
            //				Update(sp.SubEntries[i]);
            //			}
        }
        public static Color SelectedBckgColor
        {
            get
            {
                return SystemColors.Highlight;
            }
        }
        public static Color SelectedTextColor
        {
            get
            {
                return SystemColors.HighlightText;
            }
        }
        public static Color UnselectedBckgColor
        {
            get
            {
                return SystemColors.Window;
            }
        }
        public static Color UnselectedTextColor
        {
            get
            {
                return SystemColors.WindowText;
            }
        }
        internal bool IsOpen
        {
            get
            {

                if (propertyTreeView != null)
                    return (this as IPropertyEntry).IsOpen;
                return false;
            }
        }
        internal void InhibitToolTipWhileFocus(bool focused)
        {
            if (focused)
            {
                if (!toolTipInhibited && Settings.GlobalSettings.GetBoolValue("ControlCenter.InhibitToolTipWhileFocused", false))
                {
                    toolTipInhibited = true;
                }
            }
            else
            {
                if (toolTipInhibited)
                {
                    toolTipInhibited = false;
                }
            }
        }
        #endregion
        // a quick implementation of IPropertyEntry based on the old IShowProperty implementation. 
        // should be disentangled in future
        private bool isOpen = false;
        bool IPropertyEntry.IsOpen
        {
            get
            {
                return isOpen;
            }
            set
            {
                isOpen = value;
                Opened(isOpen);
            }
        }

        protected virtual bool HasDropDownButton => false; // must be overridden in thos properties, which provide a drop down list (MultipleChoiceProperty)
        protected virtual bool ValueIsEditable => !ReadOnly;
        public virtual PropertyEntryType Flags
        {
            get
            {
                PropertyEntryType res = (PropertyEntryType)LabelType; // has the same flags
                if (EntryType == ShowPropertyEntryType.GroupTitle) res |= PropertyEntryType.GroupTitle;
                if (EntryType == ShowPropertyEntryType.Seperator) res |= PropertyEntryType.Seperator;
                if (HasDropDownButton) res |= PropertyEntryType.DropDown;
                if (ValueIsEditable && Value != null) res |= PropertyEntryType.ValueEditable;
                if (SubEntriesCount > 0) res |= PropertyEntryType.HasSubEntries;
                return res;
            }
        }
        public virtual string Label => LabelText;
        public virtual string Value => null;
        //public string ToolTip => StringTable.GetString(HelpLink, StringTable.Category.info);
        public string ResourceId => HelpLink;
        public object Parent { get; set; }
        public virtual IPropertyEntry[] SubItems
        {
            get
            {
                List<IPropertyEntry> res = new List<IPropertyEntry>();
                for (int i = 0; i < SubEntriesCount; i++)
                {
                    if (SubEntries[i] is IPropertyEntry)
                    {
                        res.Add(SubEntries[i] as IPropertyEntry);
                    }
                }
                return res.ToArray();
            }
        }
        public virtual void ButtonClicked(PropertyEntryButton button)
        {
            throw new NotImplementedException();
        }
        public int OpenOrCloseSubEntries()
        {
            if ((this as IPropertyEntry).IsOpen)
            {
                int n = SubItems.Length;
                (this as IPropertyEntry).IsOpen = false;
                return -n;
            }
            else
            {
                (this as IPropertyEntry).IsOpen = true;
                return SubItems.Length;
            }
        }
        protected IPropertyPage propertyPage;
        public virtual void Added(IPropertyPage pp)
        {
            propertyPage = pp;
            // Parent = pp;  no! all items and subitems will have the Added called, but changing theis parent would be wrong
        }
        public virtual void Removed(IPropertyPage pp)
        {
            propertyPage = null;
        }
        public virtual MenuWithHandler[] ContextMenu
        {
            get
            {
                return null;
            }
        }
        public string[] GetDropDownList()
        {   // must be implemented in those properties which set HasDropDownButton to true
            throw new NotImplementedException();
        }
        public virtual void StartEdit(bool editValue) { }
        public virtual void EndEdit(bool aborted, bool modified, string newValue) { }
        public virtual bool EditTextChanged(string newValue) { return true; }
        public virtual string GetErrorText(){ return ""; }
        int IPropertyEntry.Index { get; set; }
        int IPropertyEntry.IndentLevel { get; set; }
        public virtual void Selected(IPropertyEntry previousSelected)
        {
            Selected();
        }
        public virtual void UnSelected(IPropertyEntry nowSelected)
        {
            UnSelected();
        }
        public virtual void ListBoxSelected(int selectedIndex) { }
        public bool DeferUpdate { get; set; } = false;
        bool IPropertyEntry.IsLocked { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <inheritdoc/>
        public event PropertyEntryChangedStateDelegate PropertyEntryChangedStateEvent;
        protected void PropertyEntryChangedState(StateChangedArgs args)
        {
            PropertyEntryChangedStateEvent?.Invoke(this, args);
        }

        public virtual IPropertyEntry FindSubItem(string helpResourceID)
        {   // also check for LabeText, which would be the name of an item in the list
            if (ResourceId == helpResourceID || (string.IsNullOrEmpty(ResourceId) && labelTextInternal == helpResourceID)) return this;
            for (int i = 0; i < SubItems.Length; i++)
            {
                IPropertyEntry found = SubItems[i].FindSubItem(helpResourceID);
                if (found != null) return found;
            }
            return null;
        }
    }

    /// <summary>
    /// Simple group-entry into the treeview of the control center. The resourceId
    /// specifies the text that is displayed. Use AddSubEntry to add al the subentries
    /// for this group entry. All subentries must be added before this group entry is displayed.
    /// </summary>

    public class ShowPropertyGroup : PropertyEntryImpl
    {
        public ShowPropertyGroup(string resourceId)
        {
            base.resourceIdInternal = resourceId;
            subEntriesInternal = new IPropertyEntry[0];
        }
        protected IPropertyEntry[] subEntriesInternal;
        public void ClearSubEntries()
        {
            subEntriesInternal = new IPropertyEntry[0];
        }
        public void AddSubEntry(IPropertyEntry ToAdd)
        {
            ArrayList al = new ArrayList(subEntriesInternal);
            al.Add(ToAdd);
            subEntriesInternal = (IPropertyEntry[])al.ToArray(typeof(IPropertyEntry));
        }
        public void AddSubEntries(params IPropertyEntry[] ToAdd)
        {
            ArrayList al = new ArrayList(subEntriesInternal);
            al.AddRange(ToAdd);
            subEntriesInternal = (IPropertyEntry[])al.ToArray(typeof(IPropertyEntry));
        }
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        /// <inheritdoc/>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                return subEntriesInternal;
            }
        }

    }

    /// <summary>
    /// The interface a property display control provides to the entries (<see cref="IShowProperty"/>) it shows. CADability
    /// implements it in its properties explorer; an application can replace that control by its own, which only has to
    /// implement this interface.
    /// </summary>
    public interface IPropertyTreeView
    {
        /// <summary>
        /// The active view, or null when there is none.
        /// </summary>
        IView ActiveView { get; set; }
        /// <summary>
        /// The standard height of a line of the display, in pixels.
        /// </summary>
        int DefaultLineHeight { get; }
        /// <summary>
        /// Adds the entry to the display, as a root entry.
        /// </summary>
        /// <param name="ToAdd">the entry to show</param>
        /// <param name="TopMost">true: as the first entry, false: as the last entry</param>
        void AddShowProperty(IShowProperty ToAdd, bool TopMost);
        /// <summary>
        /// Removes the entry from the display.
        /// </summary>
        /// <param name="ToRemove">the entry to remove</param>
        void RemoveShowProperty(IShowProperty ToRemove);
        /// <summary>
        /// Replaces an entry of the display by another one, at the same position.
        /// </summary>
        /// <param name="ToReplace">the entry to replace</param>
        /// <param name="ToInsert">the new entry</param>
        void ReplaceShowProperty(IShowProperty ToReplace, IShowProperty ToInsert);
        /// <summary>
        /// Determins whether the given IShowProperty LookForThis is one of the root
        /// ShowProperties.
        /// </summary>
        /// <param name="LookForThis">look for this IShowProperty object</param>
        /// <returns>true, if LookForThis is a root ShowProperty, false otherwise</returns>
        bool HasTopLevelShowProperty(IShowProperty LookForThis);
        /// <summary>
        /// Selects (highlights) the entry.
        /// </summary>
        /// <param name="NewSelection">the entry to select</param>
        void SelectEntry(IShowProperty NewSelection);
        /// <summary>
        /// Redisplays the entry, e.g. after its value or its list of sub entries has changed.
        /// </summary>
        /// <param name="ToRefresh">the entry to redisplay</param>
        void Refresh(IShowProperty ToRefresh);
        /// <summary>
        /// Scrolls the display so that the entry is visible, opening its parents if necessary.
        /// </summary>
        /// <param name="ToShow">the entry to show</param>
        void MakeVisible(IShowProperty ToShow);
        /// <summary>
        /// Starts editing the label text of the entry.
        /// </summary>
        /// <param name="ToEdit">the entry whose label is edited</param>
        void StartEditLabel(IShowProperty ToEdit);
        /// <summary>
        /// The sub entries of the object given in the parameter shall be (created and) visible.
        /// </summary>
        /// <param name="ToOpen">The object whose sub entries are to be opened or closed</param>
        /// <param name="open">true to open (show) the sub entries, false to close them</param>
        void OpenSubEntries(IShowProperty ToOpen, bool open);
        /// <summary>
        /// Moves the focus from the object given in the parameter to the next or previous object in the tab order.
        /// </summary>
        /// <param name="CurrentProperty">The currently focused object</param>
        /// <param name="Forward">true sets the focus on the next object in the tab order, false on the previous one</param>
        void SelectNext(IShowProperty CurrentProperty, bool Forward);
        /// <summary>
        /// Determins whether this IPropertyTreeView is on top of all IPropertyTreeView objects.
        /// Several IPropertyTreeViews reside in the ControlCenter in different tab pages. Determins
        /// whether this IPropertyTreeView is in the selected tab page.
        /// </summary>
        /// <returns>true if on top, false otherwise</returns>
        bool IsOnTop();
        bool IsOpen(IShowProperty toTest);
        void PopupContextMenu(IShowProperty entry);
        /// <summary>
        /// Finds the <see cref="IShowProperty"/> object with the given help link (<see cref="IShowProperty.HelpLink"/>).
        /// The help link is used as a kind of unique id. It is usually the the resource id for the label text.
        /// If the desired entry is not visible it will not be found. You will first have to open
        /// the sub entries of the parent entry (<see cref="OpenSubEntries"/>).
        /// </summary>
        /// <param name="HelpLink">The help link for the desired entry</param>
        /// <returns>The IShowProperty entry</returns>
        IShowProperty FindFromHelpLink(string HelpLink);
        IShowProperty GetCurrentSelection();
        IShowProperty GetParent(IShowProperty child);
        void ChildFocusChanged(IShowProperty child, bool gotFocus);
        /// <summary>
        /// Returns the <see cref="IFrame"/> object of the context of this IPropertyTreeView
        /// </summary>
        /// <returns></returns>
        IFrame GetFrame();
        event FocusChangedDelegate FocusChangedEvent;
        /// <summary>
        /// Checks, whether a Focus change from oldFocus to newFocus implies a focus change from
        /// "toTest" or one of its child entries to some entry outside of "toTest". If oldFocus
        /// is not toTest or one of its children "false" is returned. If the focus changes
        /// from one child of toTest to another or from a child of toTest to toTest itself or
        /// from toTest to one of its children, false is returned.
        /// </summary>
        /// <param name="toTest">the entry beeing examined</param>
        /// <param name="oldFocus">the entry that lost the focus</param>
        /// <param name="newFocus">the entry that got the focus</param>
        /// <returns>true if toTest lost the focus</returns>
        bool FocusLeft(IShowProperty toTest, IShowProperty oldFocus, IShowProperty newFocus);
        /// <summary>
        /// Checks, whether a Focus change from oldFocus to newFocus implies a focus change to
        /// "toTest" or one of its child entries from some entry outside of "toTest". If newFocus
        /// is not toTest or one of its children "false" is returned. If the focus changes
        /// from one child of toTest to another or from a child of toTest to toTest itself or
        /// from toTest to one of its children, false is returned.
        /// </summary>
        /// <param name="toTest">the entry beeing examined</param>
        /// <param name="oldFocus">the entry that lost the focus</param>
        /// <param name="newFocus">the entry that got the focus</param>
        /// <returns>true if toTest lost the focus</returns>
        bool FocusEntered(IShowProperty toTest, IShowProperty oldFocus, IShowProperty newFocus);
        string[] AllIds { get; }
        Rectangle GetBoundsForControl(Rectangle position);
        Point GetPointForControl(Point loc);
        void SetFont(Font font);

        void Dispose();
    }

    /// <summary>
    /// Implemented by controls that want more control over what the info popup (tooltip) shows, e.g. texts that
    /// change dynamically or different texts depending on the position of the cursor.
    /// </summary>
    public interface IInfoProvider
    {
        /// <summary>
        /// Returns an index that tells which of several texts is to be shown at the cursor position.
        /// </summary>
        /// <param name="ScreenCursorPosition">the position of the cursor in screen coordinates</param>
        /// <returns>the index of the text, 0 when there is only one text, or -1 when nothing is to be shown at this position</returns>
        int GetPositionIndex(Point ScreenCursorPosition);
        /// <summary>
        /// Returns the text to show in the info popup for the given position index and level of detail.
        /// </summary>
        /// <param name="Index">the position index from <see cref="GetPositionIndex"/></param>
        /// <param name="Level">the level of detail (short or detailed)</param>
        /// <returns>the text to show</returns>
        string GetInfoText(int Index, InfoLevelMode Level);
        /// <summary>
        /// Retursn a preferred position for the InfoPopup (tooltip) relative to the cursor
        /// position. If 0 is returned, the InfoPopup will find a default position. A positive
        /// value is the number of pixels below the cursor position, a negative value above the
        /// cursor position.
        /// </summary>
        /// <param name="Index"></param>
        /// <returns></returns>
        int GetVerticalPosition(int Index);
    }

    /* IWindowlessControl:
     * Der jeweilige Container enthält statt Controls WindowlessControls.
     * Er kennt gleichzeitig die Rechtecke, auf der sich die Controls aufhalten.
     * Man könnte mit einem QuadTree die WindowlessControls halten, um schnell über die Koordinate
     * zum Control zu kommen, muss aber nicht.
     * Mouse und Paint Messages werden weitergeleitet an das oder die betreffenden WindowlessControls.
     * Beim MouseMove sucht der Container das betreffende Control und gibt dorthin den MouseMove mit 
     * korrigierter Koordinate. Beim Paint können es mehrere Koordinaten sein.
     * Ein WindowlessControl kennt auch das Control in dem es sich aufhält. Es kann bei Bedarf 
     * ein EditControl erzeugen und dem ParentControl zufügen und auch wieder entfernen.
     */

    // Interface for Objects in the ControlCenter. 
    // The ControlCenter is unlimited in the number of controls it may contain in contrast to a Windows.Forms.Control which is limited.
    // Objects implementing this interface act like Controls getting only those notifications provided in this interface.
    // this interface enables the implementing object to react on mouse and paint messages like a Control does.
    // PRELIMINARY! may be extended in future!

}
