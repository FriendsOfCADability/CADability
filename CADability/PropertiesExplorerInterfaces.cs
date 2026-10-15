using CADability.GeoObject;
using CADability.Substitutes;
using System;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Drawing.Printing;

/* Connection to the new user-interface provided by CADability.Forms
 * 
 */
namespace CADability.UserInterface
{
    /// <summary>
    /// The control center (properties explorer) of the user interface: a tabbed control that holds the property pages
    /// (<see cref="IPropertyPage"/>) in which CADability shows the properties of the current action, of the project,
    /// of the global settings and of the view. CADability.Forms implements it; the kernel reaches it through
    /// <see cref="IFrame.ControlCenter"/> and creates the standard pages with <see cref="AddPropertyPage"/>.
    /// </summary>
    public interface IControlCenter
    {
        /// <summary>
        /// Creates a new, empty property page as a tab of the control center. The text and the tool tip of the tab are
        /// taken from the <see cref="StringTable"/> with the id <paramref name="titleId"/>+"TabPage".
        /// </summary>
        /// <param name="titleId">the id of the page, by which <see cref="GetPropertyPage"/> and <see cref="ShowPropertyPage"/> find it again</param>
        /// <param name="iconId">the index of the tab's icon in the image list of the control center</param>
        /// <returns>the new page</returns>
        IPropertyPage AddPropertyPage(string titleId, int iconId);
        /// <summary>
        /// The property page whose tab is currently selected, or null when there is none.
        /// </summary>
        IPropertyPage ActivePropertyPage { get; }
        /// <summary>
        /// Returns the property page that was created with the given id.
        /// </summary>
        /// <param name="titleId">the id given to <see cref="AddPropertyPage"/>, e.g. "Action", "Project", "Global" or "View" for the standard pages</param>
        /// <returns>the page, or null when there is none with this id</returns>
        IPropertyPage GetPropertyPage(string titleId);
        /// <summary>
        /// Shows the help topic with the given id. CADability.Forms does not implement it (the method throws a
        /// <see cref="NotImplementedException"/>) and nothing in CADability calls it.
        /// </summary>
        /// <param name="helpID">the id of the help topic, usually the resource id of an entry</param>
        void DisplayHelp(string helpID);
        /// <summary>
        /// Brings the property page with the given id to the front, i.e. selects its tab.
        /// </summary>
        /// <param name="titleId">the id of the page</param>
        /// <returns>true when the page exists, false otherwise</returns>
        bool ShowPropertyPage(string titleId);
        /// <summary>
        /// Removes the property page with the given id from the control center.
        /// </summary>
        /// <param name="titleId">the id of the page</param>
        /// <returns>true when the page was removed, false when there is none with this id</returns>
        bool RemovePropertyPage(string titleId);
        /// <summary>
        /// The frame this control center belongs to. The frame sets it when it takes over the control center; the pages
        /// created afterwards get it as their <see cref="IPropertyPage.Frame"/>.
        /// </summary>
        IFrame Frame { get; set; }
        /// <summary>
        /// Lets the control center handle a key before the focused control gets it: Tab and the arrow keys move the
        /// selection, Ctrl+Tab switches the page, Enter and Escape end or abort an edit, and the rest is passed on to
        /// <see cref="IPropertyPage.OnPreProcessKeyDown"/> of the active page. The frame calls it with the keys the
        /// active action did not handle; <see cref="KeyEventArgs.Handled"/> is set when the key was consumed.
        /// </summary>
        /// <param name="e">the key event</param>
        void PreProcessKeyDown(KeyEventArgs e);
        /// <summary>
        /// Hides (or shows again) every entry whose <see cref="IPropertyEntry.ResourceId"/> equals <paramref name="entryId"/>
        /// on all pages of the control center. Takes effect when the pages are next rebuilt, e.g. when an entry is
        /// added, opened or closed.
        /// </summary>
        /// <param name="entryId">the resource id of the entries to hide</param>
        /// <param name="hide">true: hide the entries, false: show them again</param>
        void HideEntry(string entryId, bool hide);
        /// <summary>
        /// Finds the entry with the given resource id (or label text) on the active property page, searching open and
        /// closed entries (see <see cref="IPropertyPage.FindFromHelpLink"/>).
        /// </summary>
        /// <param name="name">the resource id or label text of the entry</param>
        /// <returns>the entry, or null when there is no active page or no such entry</returns>
        IPropertyEntry FindItem(string name);
    }
    /// <summary>
    /// The state of the mouse as a property page would pass it to an entry that draws and handles itself: the kind of
    /// event, the button, the cursor position and the wheel movement. Not used by the current property page.
    /// </summary>
    public struct MouseState
    {
        /// <summary>
        /// Creates the mouse state from its parts.
        /// </summary>
        /// <param name="flags">the kind of mouse event</param>
        /// <param name="button">the button the event refers to</param>
        /// <param name="x">the x coordinate of the cursor</param>
        /// <param name="y">the y coordinate of the cursor</param>
        /// <param name="wheelDiff">the movement of the mouse wheel</param>
        public MouseState(MouseFlags flags, MouseButton button, int x, int y, int wheelDiff)
        {
            this.flags = flags;
            this.button = button;
            this.x = x;
            this.y = y;
            this.wheelDiff = wheelDiff;
        }
        /// <summary>
        /// The kind of mouse event.
        /// </summary>
        public enum MouseFlags
        {
            /// <summary>A button was released.</summary>
            up,
            /// <summary>A button was pressed.</summary>
            down,
            /// <summary>A button was clicked (pressed and released).</summary>
            click,
            /// <summary>The cursor entered the area of the entry.</summary>
            entering,
            /// <summary>The cursor left the area of the entry.</summary>
            leaving
        };
        /// <summary>
        /// The mouse button an event refers to.
        /// </summary>
        public enum MouseButton
        {
            /// <summary>The left button.</summary>
            left,
            /// <summary>The middle button.</summary>
            middle,
            /// <summary>The right button.</summary>
            right,
            /// <summary>The mouse wheel, see <see cref="MouseState.wheelDiff"/>.</summary>
            wheel
        };
        /// <summary>
        /// The kind of mouse event.
        /// </summary>
        public MouseFlags flags;
        /// <summary>
        /// The button the event refers to.
        /// </summary>
        public MouseButton button;
        /// <summary>
        /// The position of the cursor, in pixels relative to the entry.
        /// </summary>
        public int x, y;
        /// <summary>
        /// The movement of the mouse wheel (the delta of the wheel event), 0 for other events.
        /// </summary>
        public int wheelDiff;
    }
    /// <summary>
    /// The buttons and click targets of an entry in the property page, passed to <see cref="IPropertyEntry.ButtonClicked"/>
    /// to tell which one the user clicked. Which of them an entry shows follows from its <see cref="PropertyEntryType"/> flags.
    /// </summary>
    public enum PropertyEntryButton
    {
        /// <summary>The context menu button at the right end of the entry. The property page shows the entry's <see cref="IPropertyEntry.ContextMenu"/> itself and does not report this click.</summary>
        contextMenu,
        /// <summary>The drop down button of a <see cref="PropertyEntryType.DropDown"/> entry. The property page shows the list of <see cref="IPropertyEntry.GetDropDownList"/> itself and does not report this click.</summary>
        dropDown,
        /// <summary>The OK button (<see cref="PropertyEntryType.OKButton"/>), e.g. to finish an action.</summary>
        ok,
        /// <summary>The cancel button (<see cref="PropertyEntryType.CancelButton"/>), e.g. to abort an action.</summary>
        cancel,
        /// <summary>The check box of a <see cref="PropertyEntryType.Checkable"/> entry; the entry toggles its state and reflects it in its <see cref="IPropertyEntry.Value"/>.</summary>
        check,
        /// <summary>The value part of a <see cref="PropertyEntryType.ValueAsButton"/> entry, whose value is not editable but acts as a button.</summary>
        value,
        /// <summary>The lock symbol of a <see cref="PropertyEntryType.Lockable"/> entry; the entry toggles <see cref="IPropertyEntry.IsLocked"/>.</summary>
        locked,
        /// <summary>The arrow of a <see cref="PropertyEntryType.DirectMenu"/> entry, also sent when Enter is pressed on such an entry; the entry executes its command.</summary>
        directMenu,
        /// <summary>The label of the selected entry was double clicked.</summary>
        doubleclick
    };
    /// <summary>
    /// The flags that describe an entry of the property page (<see cref="IPropertyEntry.Flags"/>): what it is (a group
    /// title, a separator, an entry with sub entries), how its label is drawn (bold, highlighted, selected, checked) and
    /// what the user can do with it (select it, edit the label or the value, open a drop down list or a context menu,
    /// click a button). The property page reads them to lay out and draw the entry and to decide which notifications
    /// to send to it.
    /// </summary>
    [Flags]
    public enum PropertyEntryType
    {
        /// <summary>
        /// Entry may be selected (not selectable: GroupTitles)
        /// </summary>
        Selectable = 1 << 0,
        /// <summary>
        /// Label may be edited, good for named values
        /// </summary>
        LabelEditable = 1 << 1,
        /// <summary>
        /// Label in bold letters
        /// </summary>
        Bold = 1 << 2,
        /// <summary>
        /// Label in Highlight color (red)
        /// </summary>
        Highlight = 1 << 3,
        /// <summary>
        /// Entry is selected
        /// </summary>
        Selected = 1 << 4,
        /// <summary>
        /// Entry is checked
        /// </summary>
        Checked = 1 << 5,
        /// <summary>
        /// Label is a link, currently not supported
        /// </summary>
        Link = 1 << 6,
        /// <summary>
        /// There is a context menu for this entry
        /// </summary>
        ContextMenu = 1 << 7,
        /// <summary>
        /// A OK Button should be displayed for this entry (usually in the context of an action)
        /// </summary>
        OKButton = 1 << 8,
        /// <summary>
        /// A cancel button should be displayed for this entry (usually in the context of an action)
        /// </summary>
        CancelButton = 1 << 9,
        /// <summary>
        /// Allows the label to be destination of drag-drop operations
        /// </summary>
        AllowDrag = 1 << 10,
        /// <summary>
        /// A simple group title, no control to manipulate that entry
        /// </summary>
        GroupTitle = 1 << 11,
        /// <summary>
        /// This entry is a separator
        /// </summary>
        Seperator = 1 << 12,
        /// <summary>
        /// This entry ha a drop down list for its selection
        /// </summary>
        DropDown = 1 << 13,
        /// <summary>
        /// The value can be edited
        /// </summary>
        ValueEditable = 1 << 14,
        /// <summary>
        /// This entry has sub-entries
        /// </summary>
        HasSubEntries = 1 << 15,
        /// <summary>
        /// This entry has a spin button (up/down button)
        /// </summary>
        HasSpinButton = 1 << 16,
        /// <summary>
        /// A label with a check-box, Changes reported by ButtonClicked, Value == "0" (not checked), "1" (checked), "2" (undetermined or disabled)
        /// </summary>
        Checkable = 1 << 17,
        /// <summary>
        /// Clicking on the (non editable) value calls <see cref="IPropertyEntry.ButtonClicked(PropertyEntryButton)"/> with the flag <see cref="PropertyEntryButton.value"/>
        /// </summary>
        ValueAsButton = 1 << 18,
        /// <summary>
        /// The edit part of this entry displays the lock symbol on the rigth hand side
        /// </summary>
        Lockable = 1 << 19,
        /// <summary>
        /// Clicking on the entry calls<see cref="IPropertyEntry.ButtonClicked(PropertyEntryButton)"/> with the flag <see cref="PropertyEntryButton.directMenu"/>, displayed as right arrow
        /// </summary>
        DirectMenu = 1 << 20,
        /// <summary>
        /// This entry may have a shortcut. The shortcut is provided in the <see cref="IPropertyEntry.Label"/> as [[sF]] at the end of the text.
        /// The Syntax is [[xY]], where x is 's' for shift, 'c' for ctrl 'a' for alt or missing and Y is the key. There may also be two modifiers [[scF]] for Shift+Ctrl+F.
        /// Even when this flag is provided there may be no shortcut if the label text doesn't contain one
        /// </summary>
        Shortcut = 1 << 21,
    }
    /// <summary>
    /// Describes a single line in a tab page in the control center. Must be implemented by displayable properties like GeoPointProperty.
    /// </summary>
    public interface IPropertyEntry
    {
        /// <summary>
        /// Is the entry currently displayed with its sub-entries?
        /// </summary>
        bool IsOpen { get; set; }
        /// <summary>
        /// Notification for the entry that the open/closed state has changed
        /// </summary>
        /// <param name="isOpen">true: is now open (sub-entries are shown), false: is closed (sub-entries are hidden)</param>
        void Opened(bool isOpen);
        /// <summary>
        /// Gets the type of this entry
        /// </summary>
        PropertyEntryType Flags { get; }
        /// <summary>
        /// The label text
        /// </summary>
        string Label { get; }
        /// <summary>
        /// The values text (may contain drawing hints like "[[ColorBox:0:128:255]]Pink")
        /// </summary>
        string Value { get; }
        /// <summary>
        /// A tool-tip, or null if no tool-tip should be displayed
        /// </summary>
        string ResourceId { get; }
        /// <summary>
        /// if sub-entries are show, then hide them, if not, then show them (user clicked on the + or - symbol for opening or closing the sub-entries)
        /// </summary>
        /// <returns></returns>
        int OpenOrCloseSubEntries();
        /// <summary>
        /// Notification that the user clicked one of the buttons of the entry (see <see cref="PropertyEntryButton"/>): the
        /// context menu or drop down button, ok or cancel of an edit, the check box, the lock symbol or the direct menu
        /// arrow, or double clicked the value.
        /// </summary>
        /// <param name="button">the button that was clicked</param>
        void ButtonClicked(PropertyEntryButton button);
        /// <summary>
        /// Has been added to the property page, can be used to do some initialization.
        /// </summary>
        /// <param name="pp">property page, it has been added to, keep it to use it for Refresh notification</param>
        void Added(IPropertyPage pp);
        /// <summary>
        /// Has been removed from the property page
        /// </summary>
        /// <param name="pp"></param>
        void Removed(IPropertyPage pp);
        /// <summary>
        /// Gets or sets the parent. The implementer must simply keep this reference, it is either a IPropertyEntry or a IPropertyPage for the root objects
        /// </summary>
        object Parent { get; set; }
        /// <summary>
        /// The line number of this entry in the property page. The implementer must simply keep this value
        /// </summary>
        int Index { get; set; }
        /// <summary>
        /// The indentation of this entry (according to it's position int the property tree). The implementer must simply keep this value
        /// </summary>
        int IndentLevel { get; set; }
        /// <summary>
        /// Returns the sub-items, will only be called when <see cref="PropertyEntryType.HasSubEntries"/> is set.
        /// </summary>
        IPropertyEntry[] SubItems { get; }
        /// <summary>
        /// Gets a ContextMenue for this entry
        /// </summary>
        /// <returns></returns>
        MenuWithHandler[] ContextMenu { get; }
        /// <summary>
        /// Gets the list of choices (may contain drawing hints, like background color)
        /// </summary>
        /// <returns></returns>
        string[] GetDropDownList();
        /// <summary>
        /// Notification that the label or value part is being edited. Will only be called when <see cref="Flags"/> contains <see cref="PropertyEntryType.ValueEditable"/> or <see cref="PropertyEntryType.LabelEditable"/>.
        /// </summary>
        /// <param name="editValue">true: editing the value part, false: editing the label part</param>
        void StartEdit(bool editValue);
        /// <summary>
        /// Notification when the editing process is being terminated. 
        /// </summary>
        /// <param name="aborted">true: the edit has been aborted, false: normal end of edit process</param>
        /// <param name="modified">true: the text has been modified while editing, false: the text is unchanged</param>
        /// <param name="newValue">the text as it was when editing ended</param>
        void EndEdit(bool aborted, bool modified, string newValue);
        /// <summary>
        /// The text being edited has changed
        /// </summary>
        /// <param name="newValue">The new text</param>
        /// <returns>true, when the property would accept this value</returns>
        bool EditTextChanged(string newValue);
        /// <summary>
        /// This item is now selected in the property page. The item <paramref name="previousSelected"/> was selected before (may be null).
        /// </summary>
        /// <param name="previousSelected">the entry that was selected before, may be null</param>
        void Selected(IPropertyEntry previousSelected);
        /// <summary>
        /// This item was selected in the property page but is no more selected. The item <paramref name="nowSelected"/> will be selected instead (may be null).
        /// </summary>
        /// <param name="nowSelected">the entry that will be selected instead, may be null</param>
        void UnSelected(IPropertyEntry nowSelected);
        /// <summary>
        /// Select this entry
        /// </summary>
        void Select();
        /// <summary>
        /// Raised when the state of the entry changes: it was added to or removed from a property page, selected or
        /// unselected, opened or closed, or an edit of its value ended. The argument tells which (see
        /// <see cref="StateChangedArgs.State"/>). The owner of an entry uses it e.g. to show feedback in the view while
        /// the entry is selected.
        /// </summary>
        event PropertyEntryChangedStateDelegate PropertyEntryChangedStateEvent;
        /// <summary>
        /// The indicated <paramref name="selectedIndex"/> has been selected in the listbox
        /// </summary>
        /// <param name="selectedIndex"></param>
        void ListBoxSelected(int selectedIndex);
        /// <summary>
        /// Only meaningful when <see cref="PropertyEntryType.ValueEditable"/> is set. 
        /// In this case the changes by typing do not notify the system until the text-box is closed (i.e. the focus leaves the text box)
        /// </summary>
        bool DeferUpdate { get; set; }
        /// <summary>
        /// Only meaningful when <see cref="PropertyEntryType.ValueEditable"/> is set. 
        /// If read-only is true, the value cannot be edited.
        /// </summary>
        bool ReadOnly { get; set; }

        /// <summary>
        /// Finds this entry or one of its sub entries (recursively, whether they are open or not) by its
        /// <see cref="ResourceId"/>; an entry without a resource id matches when its <see cref="Label"/> equals
        /// <paramref name="helpResourceID"/>.
        /// </summary>
        /// <param name="helpResourceID">the resource id (or label text) to look for</param>
        /// <returns>the entry found, or null</returns>
        IPropertyEntry FindSubItem(string helpResourceID);
        /// <summary>
        /// Is the lockable entry is currently locked
        /// </summary>
        bool IsLocked { get; set; }
        /// <summary>
        /// Will be called when <see cref="EditTextChanged"/> returned false. You can provide an error message, which is displayed when the mouse hovers over the 
        /// error symbol.
        /// </summary>
        /// <returns></returns>
        string GetErrorText();
    }
    /// <summary>
    /// Handler for <see cref="IPropertyPage.OnPreProcessKeyDown"/>: a key was pressed while the page is active and
    /// neither the action nor the control center handled it. The handler sets <see cref="KeyEventArgs.Handled"/> when
    /// it consumes the key.
    /// </summary>
    /// <param name="keyEventArgs">the key event</param>
    public delegate void PreProcessKeyDown(KeyEventArgs keyEventArgs);
    /// <summary>
    /// notifies the event receiver, that the selection has changed
    /// </summary>
    /// <param name="unselected">the previous selected item</param>
    /// <param name="selected">the itam, which just has been selected</param>
    public delegate void SelectionChanged(IPropertyEntry unselected, IPropertyEntry selected);
    /// <summary>
    /// A page (tab) of the control center (<see cref="IControlCenter"/>) which shows a tree of entries
    /// (<see cref="IPropertyEntry"/>): root entries are added with <see cref="Add"/>, their sub entries appear when
    /// they are opened. The page selects, opens, edits and redraws entries and notifies them through the methods of
    /// <see cref="IPropertyEntry"/>. CADability.Forms implements it; an entry receives its page in
    /// <see cref="IPropertyEntry.Added"/>, and the kernel gets the standard pages from <see cref="IControlCenter.GetPropertyPage"/>.
    /// </summary>
    public interface IPropertyPage
    {
        /// <summary>
        /// Makes this page the active page of the control center, i.e. selects its tab.
        /// </summary>
        void BringToFront();
        /// <summary>
        /// Appends a root entry to the page and selects it. The entry gets <see cref="IPropertyEntry.Added"/>, and its
        /// <see cref="IPropertyEntry.Parent"/> is set to the page.
        /// </summary>
        /// <param name="toAdd">the entry to add</param>
        /// <param name="showOpen">true: also open the entry so that its sub entries are shown</param>
        void Add(IPropertyEntry toAdd, bool showOpen);
        /// <summary>
        /// Removes a root entry (one that was added with <see cref="Add"/>) from the page. An edit in progress is ended
        /// first; the entry and its displayed sub entries get <see cref="IPropertyEntry.Removed"/>.
        /// </summary>
        /// <param name="toRemove">the root entry to remove</param>
        void Remove(IPropertyEntry toRemove);
        /// <summary>
        /// Removes all entries from the page.
        /// </summary>
        void Clear();
        /// <summary>
        /// Redraws the entry after its label, value or flags changed. When the entry is open, its sub entries are
        /// collected again, so a changed <see cref="IPropertyEntry.SubItems"/> array is reflected as well. When the
        /// entry is being edited, the text box is updated with its new <see cref="IPropertyEntry.Value"/>.
        /// </summary>
        /// <param name="toRefresh">the entry to redisplay</param>
        void Refresh(IPropertyEntry toRefresh);
        /// <summary>
        /// The selected entry of the page, or null. Setting it selects the entry, which must be displayed on the page:
        /// the previously selected entry gets <see cref="IPropertyEntry.UnSelected"/>, the new one
        /// <see cref="IPropertyEntry.Selected"/>, an editable value gets its text box or drop down list, and
        /// <see cref="OnSelectionChanged"/> is raised.
        /// </summary>
        IPropertyEntry Selected { get; set;  }
        /// <summary>
        /// Opens or closes the entry, i.e. shows or hides its sub entries. Opening an entry that is already open
        /// rebuilds its sub entries, which serves as a refresh after the sub entries changed.
        /// </summary>
        /// <param name="toOpenOrClose">the entry, it must be displayed on the page</param>
        /// <param name="open">true: show the sub entries, false: hide them</param>
        void OpenSubEntries(IPropertyEntry toOpenOrClose, bool open);
        /// <summary>
        /// Returns the selected entry of the page, the same as <see cref="Selected"/>.
        /// </summary>
        /// <returns>the selected entry, or null when none is selected</returns>
        IPropertyEntry GetCurrentSelection();
        /// <summary>
        /// Selects the entry, the same as setting <see cref="Selected"/>; null is ignored. The page is not brought to
        /// the front.
        /// </summary>
        /// <param name="toSelect">the entry to select, it must be displayed on the page</param>
        void SelectEntry(IPropertyEntry toSelect);
        /// <summary>
        /// Scrolls the page so that the entry is in view. The entry must be displayed, i.e. its parents must be open.
        /// </summary>
        /// <param name="toShow">the entry to scroll to</param>
        void MakeVisible(IPropertyEntry toShow);
        /// <summary>
        /// Finds an entry by its <see cref="IPropertyEntry.ResourceId"/> (or, for an entry without resource id, by its
        /// label). The displayed entries are searched first; with <paramref name="searchTreeAndOpen"/> the sub entries
        /// of closed entries are searched as well (<see cref="IPropertyEntry.FindSubItem"/>). Despite the name the
        /// found entry is not opened or selected.
        /// </summary>
        /// <param name="helpResourceID">the resource id (or label text) of the entry</param>
        /// <param name="searchTreeAndOpen">true: also search entries that are not displayed because their parent is closed</param>
        /// <returns>the entry, or null when there is none</returns>
        IPropertyEntry FindFromHelpLink(string helpResourceID, bool searchTreeAndOpen = false);
        /// <summary>
        /// The frame the page belongs to (the frame of its control center). Entries use it to reach the project, the
        /// active view and the UI services.
        /// </summary>
        IFrame Frame { get; }
        /// <summary>
        /// Opens the text box on the label of the entry, e.g. to let the user name a newly created item. The entry gets
        /// <see cref="IPropertyEntry.StartEdit"/> with false and, when the edit ends, <see cref="IPropertyEntry.EndEdit"/>.
        /// </summary>
        /// <param name="ToEdit">the entry whose label is edited, it must be displayed on the page</param>
        void StartEditLabel(IPropertyEntry ToEdit);
        #region IPropertyTreeView adapters, should later be removed
        /// <summary>
        /// The active view of the frame. Kept for the older entries, use <see cref="IFrame.ActiveView"/> instead.
        /// </summary>
        IView ActiveView { get; } // use Frame.ActiveView instead
        /// <summary>
        /// Returns the parent entry of the given entry. Kept for the older entries, use <see cref="IPropertyEntry.Parent"/> instead.
        /// </summary>
        /// <param name="child">the entry whose parent is wanted</param>
        /// <returns>the parent entry, or null for a root entry (whose parent is the page)</returns>
        IPropertyEntry GetParent(IShowProperty child); // use IPropertyEntry.Parent instead
        /// <summary>
        /// Returns the frame the page belongs to. Kept for the older entries, use <see cref="Frame"/> instead.
        /// </summary>
        /// <returns>the frame</returns>
        IFrame GetFrame(); // use the Frame property instead
        /// <summary>
        /// Tells whether the sub entries of the entry are shown. Kept for the older entries, use
        /// <see cref="IPropertyEntry.IsOpen"/> instead.
        /// </summary>
        /// <param name="toTest">the entry to test</param>
        /// <returns>true when the entry is open</returns>
        bool IsOpen(IShowProperty toTest); // use IPropertyEntry.IsOpen
        #endregion
        /// <summary>
        /// Tells whether the entry is currently displayed on this page, as a root entry or as a sub entry of an open entry.
        /// </summary>
        /// <param name="entryWithTextBox">the entry to look for</param>
        /// <returns>true when the entry is displayed on this page</returns>
        bool ContainsEntry(IPropertyEntry entryWithTextBox);
        /// <summary>
        /// Tells whether this page is the active page of the control center, i.e. the one whose tab is selected and
        /// which the user sees.
        /// </summary>
        /// <returns>true when this page is in front</returns>
        bool IsOnTop();
        /// <summary>
        /// Raised for a key pressed while this page is active which neither the action nor the control center handled
        /// (see <see cref="PreProcessKeyDown"/>). An entry may subscribe to react to keys while it is displayed.
        /// </summary>
        event PreProcessKeyDown OnPreProcessKeyDown;
        /// <summary>
        /// Raised after the selected entry of the page changed; the handler gets the previously and the newly selected
        /// entry, either of which may be null (see <see cref="SelectionChanged"/>).
        /// </summary>
        event SelectionChanged OnSelectionChanged;
    }

    /// <summary>
    /// The services of the user interface framework which the kernel needs but cannot provide itself: dialogs (file,
    /// color, message box, page setup, print), the clipboard, the state of the mouse and the modifier keys, bitmaps, a
    /// progress bar and an offscreen paint interface. CADability.Forms implements it with Windows Forms; the kernel
    /// reaches it through <see cref="IFrame.UIService"/>. The dialog methods use the substitutes of the Windows Forms
    /// types in the namespace CADability.Substitutes, so the kernel does not depend on Windows Forms.
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// Extracts the CADability objects from the data of a drag and drop operation. The views use it to accept
        /// objects dropped onto the canvas.
        /// </summary>
        /// <param name="data">the data object of the drag and drop operation (a System.Windows.Forms.IDataObject in CADability.Forms)</param>
        /// <returns>the objects, or null when the data contains no CADability objects</returns>
        GeoObjectList GetDataPresent(object data);
        /// <summary>
        /// The modifier keys (Shift, Control, Alt) that are currently pressed. Actions and views use it to decide e.g.
        /// whether an object is copied instead of moved, or added to the selection instead of replacing it.
        /// </summary>
        Substitutes.Keys ModifierKeys { get; }
        /// <summary>
        /// The current position of the mouse cursor in screen coordinates (pixels); a view converts it to its client
        /// coordinates.
        /// </summary>
        Point CurrentMousePosition { get; }
        /// <summary>
        /// Shows an open file dialog. The <paramref name="id"/> is used to cache the last directory, so with different ids you get different
        /// start directories. The <paramref name="filter"/> is a windows OpenFileDialog filter, <paramref name="filterIndex"/> specifies, 
        /// which of the filters should be used as default, <paramref name="fileName"/> returns the name of the selected file.
        /// </summary>
        /// <param name="id">to keep default directories of different usages apart</param>
        /// <param name="title">title of the dialog, or simply open file, when title is null</param>
        /// <param name="filter"></param>
        /// <param name="filterIndex"></param>
        /// <param name="fileName">the filename</param>
        /// <returns></returns>
        Substitutes.DialogResult ShowOpenFileDlg(string id, string title, string filter, ref int filterIndex, out string fileName);
        /// <summary>
        /// Shows a save file dialog. As with <see cref="ShowOpenFileDlg"/>, <paramref name="id"/> keys the last used
        /// directory, <paramref name="filter"/> and <paramref name="filterIndex"/> select the file types, and
        /// <paramref name="fileName"/> receives the chosen name when the user confirms.
        /// </summary>
        /// <param name="id">to keep default directories of different usages apart</param>
        /// <param name="title">title of the dialog, or null for the default title</param>
        /// <param name="filter">the filter string in the format of the Windows Forms file dialogs</param>
        /// <param name="filterIndex">in: the index of the initially selected filter, out: the index of the filter the user selected</param>
        /// <param name="fileName">out: the selected file name; unchanged when the dialog was cancelled</param>
        /// <returns><see cref="DialogResult.OK"/> when the user confirmed, <see cref="DialogResult.Cancel"/> otherwise</returns>
        Substitutes.DialogResult ShowSaveFileDlg(string id, string title, string filter, ref int filterIndex, ref string fileName);
        /// <summary>
        /// Shows a modal message box with the given text, caption and buttons.
        /// </summary>
        /// <param name="text">the message</param>
        /// <param name="caption">the title of the message box</param>
        /// <param name="buttons">the buttons to offer</param>
        /// <returns>the button the user pressed, e.g. <see cref="DialogResult.Yes"/> or <see cref="DialogResult.OK"/></returns>
        Substitutes.DialogResult ShowMessageBox(string text, string caption, Substitutes.MessageBoxButtons buttons);
        /// <summary>
        /// Shows a color selection dialog, initialized with <paramref name="color"/>.
        /// </summary>
        /// <param name="color">in: the initial color, out: the selected color when the dialog was confirmed</param>
        /// <returns><see cref="DialogResult.OK"/> when the user confirmed, <see cref="DialogResult.Cancel"/> otherwise</returns>
        Substitutes.DialogResult ShowColorDialog(ref Color color);
        /// <summary>
        /// Returns a bitmap from a bitmap strip, name has the format filename:index(, where filename should be part of the resources?)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Bitmap GetBitmap(string name);
        /// <summary>
        /// Raised when the message loop of the application has no messages to process (Application.Idle in Windows
        /// Forms). The <see cref="AnimatedView"/> subscribes to it to advance its animation.
        /// </summary>
        event EventHandler ApplicationIdle;
        /// <summary>
        /// Creates a paint interface that renders into the given bitmap instead of a window, e.g. to print a view or
        /// to produce an image of a model.
        /// </summary>
        /// <param name="paintToBitmap">the bitmap to render into</param>
        /// <param name="precision">the precision for the tessellation of curves and surfaces, in model units</param>
        /// <returns>the paint interface, initialized for the bitmap</returns>
        IPaintTo3D CreatePaintInterface(Bitmap paintToBitmap, double precision);
        /// <summary>
        /// Shows the page setup dialog for printing and returns the chosen paper size and orientation.
        /// </summary>
        /// <param name="printDocument1">in: the document to set up, out: the document with the chosen settings</param>
        /// <param name="pageSettings">the current page settings</param>
        /// <param name="width">the width of the paper in hundredths of an inch, 0 when the dialog was cancelled</param>
        /// <param name="height">the height of the paper in hundredths of an inch, 0 when the dialog was cancelled</param>
        /// <param name="landscape">true when landscape orientation was chosen, false otherwise or when the dialog was cancelled</param>
        /// <returns><see cref="DialogResult.OK"/> when the user confirmed, <see cref="DialogResult.Cancel"/> otherwise</returns>
        [Obsolete("The GDI+ printing dialogs move into CADability.Forms in CADability 2.0 (issue #412).")]
        DialogResult ShowPageSetupDlg(ref PrintDocument printDocument1, PageSettings pageSettings, out int width, out int height, out bool landscape);
        /// <summary>
        /// Shows the print dialog (printer selection) for the document.
        /// </summary>
        /// <param name="printDocument">in: the document to print, out: the document with the chosen printer settings</param>
        /// <returns><see cref="DialogResult.OK"/> when the user confirmed and the document may be printed, <see cref="DialogResult.Cancel"/> otherwise</returns>
        [Obsolete("The GDI+ printing dialogs move into CADability.Forms in CADability 2.0 (issue #412).")]
        DialogResult ShowPrintDlg(ref PrintDocument printDocument);
        /// <summary>
        /// Puts the objects on the clipboard, serialized in CADability's own format, so that they can be pasted with
        /// <see cref="GetClipboardData"/>, also into another CADability application. An empty list is ignored.
        /// </summary>
        /// <param name="objects">the objects to copy</param>
        /// <param name="copy">true: the data stays on the clipboard after the application exits, false: it is removed then</param>
        void SetClipboardData(GeoObjectList objects, bool copy);
        /// <summary>
        /// Returns the objects that <see cref="SetClipboardData"/> put on the clipboard.
        /// </summary>
        /// <returns>a new list with the objects, or null when the clipboard holds no CADability objects</returns>
        GeoObjectList GetClipboardData();
        /// <summary>
        /// Tells whether the clipboard holds data that <see cref="GetClipboardData"/> can read; used to enable the
        /// paste command.
        /// </summary>
        /// <returns>true when there are CADability objects on the clipboard</returns>
        bool HasClipboardData();
        /// <summary>
        /// Shows or hides a progress bar. 
        /// </summary>
        /// <param name="show">true: show, false: hide</param>
        /// <param name="percent">progress percentage (0..100)</param>
        /// <param name="title">title of the progress bar, null: don't change the current title</param>
        void ShowProgressBar(bool show, double percent = 0.0, string title = null);
    }

}
