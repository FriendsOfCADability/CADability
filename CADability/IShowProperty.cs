using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

namespace CADability.UserInterface
{
    /// <summary>
    /// Wird mit StartModifying==true aufgerufen, wenn der Anwender den MouseButton drückt.
    /// sender und propertyInfo können dazu verwendet werden, den Punkt ohne kenntnis des Objektes
    /// zu verändern.
    /// Wird mit StartModifying==false aufgerufen, wenn der Anwender in das Editfeld eintippt.
    /// Dann soll die MausAktion für diesen Punkt abgebrochen werden.
    /// </summary>
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
    /// Selectable: der Text kann markiert werden, Editable: der Text kann geändert werden, Bold: fett gedruckt,
    /// ContextMenu: beim Rechtsklick wird ein ContextMenu benötigt
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
        /// <see cref="StringTable.AddStrings"/>)
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
        /// <see cref="labelTextInternal"/> (if not null) or <see cref="StringTable.GetString"/>(resourceId+".Label")
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
        /// <see cref="StringTable.GetString"/>(resourceId+".DetailedInfo") or 
        /// <see cref="StringTable.GetString"/>(resourceId+".ShortInfo")
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
        /// Implementation of <see cref="IShowProperty.LabelType"/>.
        /// Returns <see cref="ShowPropertyLabelFlags.Selectable"/>
        /// </summary>
        public virtual ShowPropertyLabelFlags LabelType
        {
            get { return ShowPropertyLabelFlags.Selectable; }
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.EntryType"/>.
        /// Returns <see cref="ShowPropertyEntryType.SimpleEntry"/>
        /// </summary>
        public virtual ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.SimpleEntry;
            }
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.SubEntriesCount"/>.
        /// Returns 0.
        /// </summary>
        public virtual int SubEntriesCount
        {
            get
            {
                return 0;
            }
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.SubEntries"/>.
        /// Returns an empty array.
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
        /// <summary>
        /// Implementation of <see cref="IShowProperty.StateChangedEvent"/>.
        /// </summary>
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
        /// <summary>
        /// Implementation of <see cref="IShowProperty.Hidden"/>. 
        /// </summary>
        public virtual bool Hidden
        {
            get { return hidden; }
            set { hidden = value; } // TODO: hier noch PropertyTreeView benachrichtigen!
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.Hidden"/>. 
        /// </summary>
        public virtual bool ReadOnly
        {
            get { return readOnly; }
            set
            {
                readOnly = value;
                // nur merken, die subentries gibts ggf noch garnicht
            }
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.Refresh"/>. 
        /// </summary>
        public virtual void Refresh()
        {
        }
        /// <summary>
        /// Implements <see cref="CADability.UserInterface.IShowProperty.OnEnterPressed ()"/>
        /// </summary>
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

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.PropertyEntryChangedStateEvent"/>.
        /// </summary>
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
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                return subEntriesInternal;
            }
        }

    }

    /// <summary>
    /// Das Interface, das von dem PropertyDisplay Control zu Verfügung gestellt werden muss.
    /// CADability implementiert mit PropertyDisplay ein Control, welches dieses Interface 
    /// zu Verfügung stellt. Das PropertyDisplay Control kann aber auch  durch ein Control
    /// des Anwenders ersetzt werden. Dieses muss lediglich das IPropertyTreeView Interface
    /// implementieren.
    /// </summary>

    public interface IPropertyTreeView
    {
        /// <summary>
        /// Liefert das aktive View Objekt. Dieses kann auch fehlen, dann wird null geliefert.
        /// </summary>
        IView ActiveView { get; set; }
        /// <summary>
        /// Liefert die Standard Zeilenhöhe der Anzeige
        /// </summary>
        int DefaultLineHeight { get; }
        /// <summary>
        /// Fügt das im Parameter gegebene Objekt zur Anzeige hinzu.
        /// </summary>
        /// <param name="ToAdd">Dieses Objekt soll angezeigt werden</param>
        /// <param name="TopMost">true: an oberster Stelle, false: an unterster Stelle.</param>
        void AddShowProperty(IShowProperty ToAdd, bool TopMost);
        /// <summary>
        /// Das im Parameter gegebene Objekt soll nicht mehr angezeigt werden.
        /// </summary>
        /// <param name="ToRemove">Die zu entfernende Anzeige.</param>
        void RemoveShowProperty(IShowProperty ToRemove);
        /// <summary>
        /// Ersetzt eine Anzeige durch eine andere.
        /// </summary>
        /// <param name="ToReplace">Die zu ersetzende Anzeige</param>
        /// <param name="ToInsert">Die neue Anzeige</param>
        void ReplaceShowProperty(IShowProperty ToReplace, IShowProperty ToInsert);
        /// <summary>
        /// Determins whether the given IShowProperty LookForThis is one of the root
        /// ShowProperties.
        /// </summary>
        /// <param name="LookForThis">look for this IShowProperty object</param>
        /// <returns>true, if LookForThis is a root ShowProperty, false otherwise</returns>
        bool HasTopLevelShowProperty(IShowProperty LookForThis);
        /// <summary>
        /// Das im Parameter gegebene Objekt soll markiert dargestellt werden.
        /// </summary>
        /// <param name="NewSelection">Das neu zu markierende objekt.</param>
        void SelectEntry(IShowProperty NewSelection);
        /// <summary>
        /// Das im Parameter gegebene Objekt muss neu dargestellt werden. Z.B wenn sich die 
        /// Liste der Unterobjekte geändert hat.
        /// </summary>
        /// <param name="ToRefresh">Das neu darzustellende Objekt.</param>
        void Refresh(IShowProperty ToRefresh);
        /// <summary>
        /// Das im Parameter gegebene Objekt soll sichtbar dargestellt werden.
        /// </summary>
        /// <param name="ToShow">Das anzuzeigende Objekt.</param>
        void MakeVisible(IShowProperty ToShow);
        /// <summary>
        /// Der LabelText des im Parameter gegebenen Objektes soll editiert werden.
        /// </summary>
        /// <param name="ToEdit">Das zu editiernde Objekt</param>
        void StartEditLabel(IShowProperty ToEdit);
        /// <summary>
        /// Die Untereinträge des im Parameter gegebenen Objektes sollenn (erzeugt und) sichtbar sein.
        /// </summary>
        /// <param name="ToOpen"></param>
        void OpenSubEntries(IShowProperty ToOpen, bool open);
        /// <summary>
        /// Die Untereinträge des im Parameter gegebenen Objektes sollenn (erzeugt und) sichtbar sein.
        /// </summary>
        /// <param name="HasCurrentFocus">Das aktuell fokussierte Objekt</param>	
        ///<param name="Forward">=wahr setzt den Fukus auf das nächste Objekt der Tab-Reighenfolge, = falsch auf das verhergehende</param>	
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
    /// Controls, die dieses interface implementieren, können genaueren Einfluß auf das
    /// Erscheinen im InfoPopup nehmen, z.B. dynamisch sich ändernde Texte
    /// oder verschiedene ToolTips, je nach Position
    /// </summary>

    public interface IInfoProvider
    {
        /// <summary>
        /// Liefert die Positionsnummer für mehrere verschiedene Texte
        /// </summary>
        /// <param name="ScreenCursorPosition">Bildschirmposition des Cursors</param>
        /// <returns>Die Positionsnummer, oder 0, wenn es nur einen Text gibt, 
        /// oder -1, wenn an dieser Stelle nichts angezeigt werden soll.</returns>
        int GetPositionIndex(Point ScreenCursorPosition);
        /// <summary>
        ///  Liefert den darzustellenden InfoText, ggf. abhängig von der Positionsnummer
        ///  und dem Level (wie ausführlich)
        /// </summary>
        /// <param name="Index">Die Positionsnummer aus GetPositionIndex</param>
        /// <param name="Level">Der Level (0: einfach, 1: ausführlich)</param>
        /// <returns>Der darzustellende Text</returns>
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

    /// <summary>
    /// Interface for Objects in the ControlCenter. 
    /// The ControlCenter is unlimited in the number of controls it may contain in contrast to a Windows.Forms.Control which is limited.
    /// Objects implementing this interface act like Controls getting only those notifications provided in this interface.
    /// this interface enables the implementing object to react on mouse and paint messages like a Control does.
    /// PRELIMINARY! may be extended in future!
    /// </summary>

}
