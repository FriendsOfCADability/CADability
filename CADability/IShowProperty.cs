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
    /// <summary>
    /// Handler for a key typed while the label or value of an entry is being edited: <paramref name="sender"/> is the
    /// entry, <paramref name="keyPressed"/> the typed character. A remnant of the old control center; the current
    /// property page raises no event of this type.
    /// </summary>
    /// <param name="sender">the entry being edited</param>
    /// <param name="keyPressed">the character that was typed</param>
    public delegate void EditingDelegate(IShowProperty sender, char keyPressed);
    /// <summary>
    /// Handler for <see cref="IPropertyTreeView.FocusChangedEvent"/>: the keyboard focus moved from <paramref name="oldFocus"/>
    /// to <paramref name="newFocus"/> within the tree view <paramref name="sender"/>. Either entry may be null when the
    /// focus came from or went to a control outside the tree view.
    /// </summary>
    /// <param name="sender">the tree view in which the focus changed</param>
    /// <param name="newFocus">the entry that received the focus, or null</param>
    /// <param name="oldFocus">the entry that lost the focus, or null</param>
    public delegate void FocusChangedDelegate(IPropertyTreeView sender, IShowProperty newFocus, IShowProperty oldFocus);

    /// <summary>
    /// Handler for <see cref="IPropertyEntry.PropertyEntryChangedStateEvent"/>: the entry <paramref name="sender"/> was
    /// added to or removed from a property page, selected or unselected, opened or closed, or an edit of its value
    /// ended. <paramref name="args"/> tells which of these happened (<see cref="StateChangedArgs.EventState"/>).
    /// </summary>
    /// <param name="sender">the entry whose state changed</param>
    /// <param name="args">the kind of change</param>
    public delegate void PropertyEntryChangedStateDelegate(IPropertyEntry sender, StateChangedArgs args);
    /// <summary>
    /// Handler for <see cref="IShowProperty.StateChangedEvent"/>, the counterpart of <see cref="PropertyEntryChangedStateDelegate"/>
    /// for the older <see cref="IShowProperty"/> entries: the state of <paramref name="sender"/> changed as described by
    /// <paramref name="args"/>.
    /// </summary>
    /// <param name="sender">the entry whose state changed</param>
    /// <param name="args">the kind of change</param>
    public delegate void StateChangedDelegate(IShowProperty sender, StateChangedArgs args);

    /// <summary>
    /// The argument of <see cref="IPropertyEntry.PropertyEntryChangedStateEvent"/> and <see cref="IShowProperty.StateChangedEvent"/>:
    /// tells which change of state an entry of the property page reports (<see cref="EventState"/>). When a sub entry
    /// was selected, <see cref="SelectedChild"/> names it. The entry creates the object when it raises the event, a
    /// handler only reads it.
    /// </summary>
    public class StateChangedArgs : EventArgs
    {
        /// <summary>
        /// The kinds of state change an entry reports.
        /// </summary>
        public enum State
        {
            /// <summary>The entry was opened, its sub entries are now shown.</summary>
            OpenSubEntries,
            /// <summary>The entry was closed, its sub entries are now hidden.</summary>
            CollapseSubEntries,
            /// <summary>The entry became the selected entry of its property page.</summary>
            Selected,
            /// <summary>The entry is no longer the selected entry of its property page.</summary>
            UnSelected,
            /// <summary>One of the sub entries of the entry was selected, <see cref="StateChangedArgs.SelectedChild"/> tells which one. Only the older <see cref="IShowPropertyImpl"/> entries report it.</summary>
            SubEntrySelected,
            /// <summary>The entry was added to a property page.</summary>
            Added,
            /// <summary>The entry was removed from its property page.</summary>
            Removed,
            /// <summary>Editing the value of the entry in the text box ended and the value has been set (reported by <see cref="EditableProperty{T}"/>).</summary>
            EditEnded
        }
        private State state;
        /// <summary>
        /// Creates the argument for the given kind of change.
        /// </summary>
        /// <param name="state">the change that is reported</param>
        public StateChangedArgs(State state)
        {
            this.state = state;
        }
        /// <summary>
        /// The change that the event reports.
        /// </summary>
        public State EventState
        {
            get { return state; }
        }
        /// <summary>
        /// For <see cref="State.SubEntrySelected"/>: the sub entry that was selected. Null for the other states.
        /// </summary>
        public IShowProperty SelectedChild;
    }

    /// <summary>
    /// The states of the mouse button that entries of the old control center showed next to a value which can be
    /// modified with the mouse (e.g. a point). Not used by the current property page; it remains for compatibility only.
    /// </summary>
    public enum MouseButtonMode
    {
        /// <summary>The value is being modified with the mouse, the button is locked in its pressed state.</summary>
        MouseLocked,
        /// <summary>The button is shown but cannot be pressed.</summary>
        MouseInactive,
        /// <summary>The button can be pressed to start modifying the value with the mouse.</summary>
        MouseActive
    }

    /// <summary>
    /// Interface for IShowProperty objects, which implement MouseButton modification.
    /// </summary>

    public interface IConstructProperty
    {
        /// <summary>
        /// Selects the entry in its property page so that the value can be typed, e.g. after the user picked the
        /// hotspot of the value in the drawing.
        /// </summary>
        void SetFocus();
        /// <summary>
        /// Returns the position of the hotspot which represents the value in the drawing. For a point property this is
        /// the point itself, for a length, radius or angle it is the position the owner set with <see cref="SetHotspotPosition"/>.
        /// </summary>
        /// <returns>the hotspot position in world coordinates</returns>
        GeoPoint GetHotspotPosition();
        /// <summary>
        /// Starts modifying the value with the mouse, as if the user had dragged its hotspot: the entry raises its
        /// ModifyWithMouse event (see <see cref="ModifyWithMouseDelegate"/>) and the owning action or object follows the
        /// mouse until the user clicks or types a value.
        /// </summary>
        void StartModifyWithMouse();
        /// <summary>
        /// Sets the position of the hotspot in the drawing which represents this value. Used by the owner of values that
        /// have no position of their own, like a length, a radius or an angle (e.g. the end of the major axis for the
        /// major radius of an ellipse).
        /// </summary>
        /// <param name="Here">the hotspot position in world coordinates</param>
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
        /// <summary>
        /// Notification that the entry became visible or invisible in the control center, e.g. because its tab page was
        /// brought to the front or hidden. The current property page does not send this notification.
        /// </summary>
        /// <param name="isVisible">true: the entry is now visible, false: it is now hidden</param>
        void OnVisibilityChanged(bool isVisible);
        /// <summary>
        /// Whether this entry is the selected entry of its property page. Setting it to true selects the entry (it must
        /// be visible), setting it to false unselects it when it is the selected entry. The hotspots of the editable
        /// properties forward their selection state (<see cref="IHotSpot.IsSelected"/>) to this property.
        /// </summary>
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
        /// <summary>
        /// The property page this entry is currently shown in, or null. The older name of <see cref="propertyPage"/>,
        /// kept for the derived classes that still use it.
        /// </summary>
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
        /// <summary>
        /// The label flags passed to <see cref="SuppressFlags"/>. The base class only stores them; none of the entries
        /// shipped with CADability evaluates them.
        /// </summary>
        protected ShowPropertyLabelFlags flagsToSuppress = (ShowPropertyLabelFlags)0;
        /// <summary>
        /// Asks the entry not to show the given label flags (e.g. no context menu). The flags are only stored in
        /// <see cref="flagsToSuppress"/>; a derived class would have to remove them from its <see cref="LabelType"/>,
        /// which none of the entries shipped with CADability does.
        /// </summary>
        /// <param name="flagsToSuppress">the flags that should not appear in <see cref="LabelType"/></param>
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
        /// <summary>
        /// Creates the entry with the frame it belongs to, which <see cref="Frame"/> returns as long as the entry is not
        /// shown in a property page.
        /// </summary>
        /// <param name="frame">the frame, may be null</param>
        protected IShowPropertyImpl(IFrame frame)
        {
            this.frame = frame;
        }
        /// <summary>
        /// Creates the entry without a frame; <see cref="Frame"/> then returns the frame of the property page the entry
        /// is shown in, or the main frame (<see cref="FrameImpl.MainFrame"/>).
        /// </summary>
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
        /// <summary>
        /// Implementation of <see cref="IShowProperty.OnVisibilityChanged"/>. Does nothing, override when needed.
        /// </summary>
        /// <param name="isVisible">true: the entry is now visible, false: it is now hidden</param>
        public virtual void OnVisibilityChanged(bool isVisible)
        {
        }
        /// <summary>
        /// Implementation of <see cref="IShowProperty.IsSelected"/>: asks the property page for its current selection;
        /// setting it selects or unselects this entry in the property page. May only be used while the entry is shown
        /// in a property page.
        /// </summary>
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
        /// <summary>
        /// The background color of the selected entry (the system highlight color). A remnant of the old control center;
        /// the current property page uses the system colors directly.
        /// </summary>
        public static Color SelectedBckgColor
        {
            get
            {
                return SystemColors.Highlight;
            }
        }
        /// <summary>
        /// The text color of the selected entry (the system highlight text color). A remnant of the old control center;
        /// the current property page uses the system colors directly.
        /// </summary>
        public static Color SelectedTextColor
        {
            get
            {
                return SystemColors.HighlightText;
            }
        }
        /// <summary>
        /// The background color of an unselected entry (the system window color). A remnant of the old control center;
        /// the current property page uses the system colors directly.
        /// </summary>
        public static Color UnselectedBckgColor
        {
            get
            {
                return SystemColors.Window;
            }
        }
        /// <summary>
        /// The text color of an unselected entry (the system window text color). A remnant of the old control center;
        /// the current property page uses the system colors directly.
        /// </summary>
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

        /// <summary>
        /// Whether the entry offers a drop down list of choices for its value, which adds <see cref="PropertyEntryType.DropDown"/>
        /// to <see cref="Flags"/>. The default is false, and no entry derived from this class returns true at present:
        /// drop down lists are provided by <see cref="MultipleChoiceProperty"/>, which derives from <see cref="PropertyEntryImpl"/>.
        /// </summary>
        protected virtual bool HasDropDownButton => false; // must be overridden in thos properties, which provide a drop down list (MultipleChoiceProperty)
        /// <summary>
        /// Whether the value of the entry may be edited in the text box of the property page: by default true unless
        /// <see cref="ReadOnly"/> is set. Together with a non-null <see cref="Value"/> it yields
        /// <see cref="PropertyEntryType.ValueEditable"/> in <see cref="Flags"/>.
        /// </summary>
        protected virtual bool ValueIsEditable => !ReadOnly;
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Flags"/> for the current property page, derived from the older
        /// properties of this class: the <see cref="LabelType"/> flags (which have the same values),
        /// <see cref="PropertyEntryType.GroupTitle"/> or <see cref="PropertyEntryType.Seperator"/> from <see cref="EntryType"/>,
        /// <see cref="PropertyEntryType.DropDown"/> from <see cref="HasDropDownButton"/>, <see cref="PropertyEntryType.ValueEditable"/>
        /// from <see cref="ValueIsEditable"/> and <see cref="PropertyEntryType.HasSubEntries"/> when <see cref="SubEntriesCount"/>
        /// is greater than 0.
        /// </summary>
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
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Label"/>: returns <see cref="LabelText"/>.
        /// </summary>
        public virtual string Label => LabelText;
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Value"/>: the text shown in the value column. The default is null,
        /// which means the entry has no value column; entries that show a value override it.
        /// </summary>
        public virtual string Value => null;
        //public string ToolTip => StringTable.GetString(HelpLink, StringTable.Category.info);
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.ResourceId"/>: returns <see cref="HelpLink"/>, i.e. the resource id
        /// of the entry, which the property page uses for the tool tip and which <see cref="FindSubItem"/> and
        /// <see cref="IPropertyPage.FindFromHelpLink"/> use to identify the entry.
        /// </summary>
        public string ResourceId => HelpLink;
        /// <inheritdoc/>
        public object Parent { get; set; }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.SubItems"/>: those of the <see cref="SubEntries"/> that implement
        /// <see cref="IPropertyEntry"/>; entries that only implement <see cref="IShowProperty"/> are left out.
        /// </summary>
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
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.ButtonClicked"/>. The base implementation throws a
        /// <see cref="NotImplementedException"/>; an entry whose <see cref="Flags"/> announce a button (OK, cancel, check
        /// box, lock symbol, direct menu, value as button) must override it.
        /// </summary>
        /// <param name="button">the button that was clicked</param>
        public virtual void ButtonClicked(PropertyEntryButton button)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.OpenOrCloseSubEntries"/>: toggles <see cref="IPropertyEntry.IsOpen"/>,
        /// which notifies the entry via <see cref="Opened"/>.
        /// </summary>
        /// <returns>the number of sub items that became visible, or the negative number of sub items that were hidden</returns>
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
        /// <summary>
        /// The property page this entry is currently shown in, set by <see cref="Added(IPropertyPage)"/> and cleared by
        /// <see cref="Removed(IPropertyPage)"/>. Null while the entry is not shown.
        /// </summary>
        protected IPropertyPage propertyPage;
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Added"/>: memorizes the property page in <see cref="propertyPage"/>.
        /// Unlike <see cref="Added(IPropertyTreeView)"/> it does not raise the state changed event. Override it to
        /// subscribe to events while the entry is displayed, and call the base implementation.
        /// </summary>
        /// <param name="pp">the property page the entry was added to</param>
        public virtual void Added(IPropertyPage pp)
        {
            propertyPage = pp;
            // Parent = pp;  no! all items and subitems will have the Added called, but changing theis parent would be wrong
        }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Removed"/>: forgets the property page. Override it to unsubscribe
        /// from events, and call the base implementation.
        /// </summary>
        /// <param name="pp">the property page the entry was removed from</param>
        public virtual void Removed(IPropertyPage pp)
        {
            propertyPage = null;
        }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.ContextMenu"/>: the default returns null. An entry whose
        /// <see cref="LabelType"/> contains <see cref="ShowPropertyLabelFlags.ContextMenu"/> must override it.
        /// </summary>
        public virtual MenuWithHandler[] ContextMenu
        {
            get
            {
                return null;
            }
        }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.GetDropDownList"/>. Entries derived from this class have no drop
        /// down list (see <see cref="HasDropDownButton"/>), the method throws a <see cref="NotImplementedException"/>.
        /// </summary>
        /// <returns>the list of choices; this implementation never returns</returns>
        public string[] GetDropDownList()
        {   // must be implemented in those properties which set HasDropDownButton to true
            throw new NotImplementedException();
        }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.StartEdit"/>: does nothing. Override it when the entry has to
        /// prepare for an edit, e.g. remember the value before the edit.
        /// </summary>
        /// <param name="editValue">true: the value is edited, false: the label is edited</param>
        public virtual void StartEdit(bool editValue) { }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.EndEdit"/>: does nothing. An entry with an editable value or label
        /// overrides it to take over (or, when aborted, discard) the text that was entered.
        /// </summary>
        /// <param name="aborted">true: the edit was aborted (Escape), false: it ended normally</param>
        /// <param name="modified">true: the text was changed while editing</param>
        /// <param name="newValue">the text as it was when editing ended</param>
        public virtual void EndEdit(bool aborted, bool modified, string newValue) { }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.EditTextChanged"/>: accepts every text and returns true. Override
        /// it to check the text while it is typed and to apply it immediately.
        /// </summary>
        /// <param name="newValue">the current text of the text box</param>
        /// <returns>true when the text is acceptable, false to show the error symbol with the text of <see cref="GetErrorText"/></returns>
        public virtual bool EditTextChanged(string newValue) { return true; }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.GetErrorText"/>: returns an empty string. Override it together with
        /// <see cref="EditTextChanged"/> to explain why a text was rejected.
        /// </summary>
        /// <returns>the error text, empty by default</returns>
        public virtual string GetErrorText(){ return ""; }
        int IPropertyEntry.Index { get; set; }
        int IPropertyEntry.IndentLevel { get; set; }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.Selected"/>: forwards to the parameterless <see cref="Selected()"/>
        /// of the older interface, which raises the state changed event.
        /// </summary>
        /// <param name="previousSelected">the entry that was selected before, may be null</param>
        public virtual void Selected(IPropertyEntry previousSelected)
        {
            Selected();
        }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.UnSelected"/>: forwards to the parameterless <see cref="UnSelected()"/>
        /// of the older interface, which raises the state changed event.
        /// </summary>
        /// <param name="nowSelected">the entry that is selected instead, may be null</param>
        public virtual void UnSelected(IPropertyEntry nowSelected)
        {
            UnSelected();
        }
        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.ListBoxSelected"/>: does nothing, since entries derived from this
        /// class have no drop down list.
        /// </summary>
        /// <param name="selectedIndex">the index of the chosen item in the list of <see cref="GetDropDownList"/></param>
        public virtual void ListBoxSelected(int selectedIndex) { }
        /// <inheritdoc/>
        public bool DeferUpdate { get; set; } = false;
        bool IPropertyEntry.IsLocked { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        /// <inheritdoc/>
        public event PropertyEntryChangedStateDelegate PropertyEntryChangedStateEvent;
        /// <summary>
        /// Raises <see cref="PropertyEntryChangedStateEvent"/> with the given arguments. The standard implementations of
        /// <see cref="Added(IPropertyTreeView)"/>, <see cref="Selected()"/>, <see cref="UnSelected()"/>, <see cref="ChildSelected"/>
        /// and <see cref="Opened"/> use it; a derived class may report further changes.
        /// </summary>
        /// <param name="args">the change to report</param>
        protected void PropertyEntryChangedState(StateChangedArgs args)
        {
            PropertyEntryChangedStateEvent?.Invoke(this, args);
        }

        /// <summary>
        /// Implementation of <see cref="IPropertyEntry.FindSubItem"/>: returns this entry when its <see cref="ResourceId"/>
        /// (or, when it has none, its label text) equals <paramref name="helpResourceID"/>, otherwise searches the
        /// <see cref="SubItems"/> recursively, whether they are open or not.
        /// </summary>
        /// <param name="helpResourceID">the resource id (or label text) to look for</param>
        /// <returns>the entry found, or null</returns>
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
        /// <summary>
        /// Creates an empty group whose label is the string <paramref name="resourceId"/> of the <see cref="StringTable"/>.
        /// Add the sub entries with <see cref="AddSubEntry"/> or <see cref="AddSubEntries"/> before the group is displayed.
        /// </summary>
        /// <param name="resourceId">the resource id of the label text</param>
        public ShowPropertyGroup(string resourceId)
        {
            base.resourceIdInternal = resourceId;
            subEntriesInternal = new IPropertyEntry[0];
        }
        /// <summary>
        /// The sub entries of the group, which <see cref="SubItems"/> returns. Never null.
        /// </summary>
        protected IPropertyEntry[] subEntriesInternal;
        /// <summary>
        /// Removes all sub entries from the group. The property page is not notified; when the group is already
        /// displayed, call <see cref="PropertyEntryImpl.Refresh()"/> afterwards.
        /// </summary>
        public void ClearSubEntries()
        {
            subEntriesInternal = new IPropertyEntry[0];
        }
        /// <summary>
        /// Appends an entry to the sub entries of the group.
        /// </summary>
        /// <param name="ToAdd">the entry to add</param>
        public void AddSubEntry(IPropertyEntry ToAdd)
        {
            ArrayList al = new ArrayList(subEntriesInternal);
            al.Add(ToAdd);
            subEntriesInternal = (IPropertyEntry[])al.ToArray(typeof(IPropertyEntry));
        }
        /// <summary>
        /// Appends several entries to the sub entries of the group, in the given order.
        /// </summary>
        /// <param name="ToAdd">the entries to add</param>
        public void AddSubEntries(params IPropertyEntry[] ToAdd)
        {
            ArrayList al = new ArrayList(subEntriesInternal);
            al.AddRange(ToAdd);
            subEntriesInternal = (IPropertyEntry[])al.ToArray(typeof(IPropertyEntry));
        }
        /// <inheritdoc/>
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
        /// <summary>
        /// Tells whether the sub entries of the entry are currently shown.
        /// </summary>
        /// <param name="toTest">the entry to test</param>
        /// <returns>true when the entry is open, false when it is closed or not displayed</returns>
        bool IsOpen(IShowProperty toTest);
        /// <summary>
        /// Shows the context menu of the entry, as if the user had clicked its context menu button.
        /// </summary>
        /// <param name="entry">the entry whose context menu is shown</param>
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
        /// <summary>
        /// Returns the selected entry of the display.
        /// </summary>
        /// <returns>the selected entry, or null when no entry is selected</returns>
        IShowProperty GetCurrentSelection();
        /// <summary>
        /// Returns the entry that contains <paramref name="child"/> among its sub entries.
        /// </summary>
        /// <param name="child">the entry whose parent is wanted</param>
        /// <returns>the parent entry, or null for a root entry</returns>
        IShowProperty GetParent(IShowProperty child);
        /// <summary>
        /// Notification from an entry that a control it owns received or lost the keyboard focus, so that the display
        /// can update its selection and raise <see cref="FocusChangedEvent"/>.
        /// </summary>
        /// <param name="child">the entry whose control changed its focus</param>
        /// <param name="gotFocus">true: the control received the focus, false: it lost the focus</param>
        void ChildFocusChanged(IShowProperty child, bool gotFocus);
        /// <summary>
        /// Returns the <see cref="IFrame"/> object of the context of this IPropertyTreeView
        /// </summary>
        /// <returns></returns>
        IFrame GetFrame();
        /// <summary>
        /// Raised when the keyboard focus moves from one entry of the display to another, or enters or leaves the
        /// display (see <see cref="FocusChangedDelegate"/>).
        /// </summary>
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
        /// <summary>
        /// The help links (<see cref="IShowProperty.HelpLink"/>) of all entries the display currently contains.
        /// </summary>
        string[] AllIds { get; }
        /// <summary>
        /// Converts a rectangle given relative to the line of an entry into the client coordinates of the display, for
        /// entries that place their own control (e.g. an edit box) over their line.
        /// </summary>
        /// <param name="position">the rectangle relative to the entry's line, in pixels</param>
        /// <returns>the rectangle in the client coordinates of the display</returns>
        Rectangle GetBoundsForControl(Rectangle position);
        /// <summary>
        /// Converts a point given relative to the line of an entry into the client coordinates of the display, the
        /// counterpart of <see cref="GetBoundsForControl"/> for a single point.
        /// </summary>
        /// <param name="loc">the point relative to the entry's line, in pixels</param>
        /// <returns>the point in the client coordinates of the display</returns>
        Point GetPointForControl(Point loc);
        /// <summary>
        /// Sets the font in which the display draws its entries.
        /// </summary>
        /// <param name="font">the new font</param>
        void SetFont(Font font);

        /// <summary>
        /// Releases the display; every displayed entry gets <see cref="IShowProperty.Removed"/> first.
        /// </summary>
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
