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
    public interface IControlCenter
    {
        IPropertyPage AddPropertyPage(string titleId, int iconId);
        IPropertyPage ActivePropertyPage { get; }
        IPropertyPage GetPropertyPage(string titleId);
        void DisplayHelp(string helpID);
        bool ShowPropertyPage(string titleId);
        bool RemovePropertyPage(string titleId);
        IFrame Frame { get; set; }
        void PreProcessKeyDown(KeyEventArgs e);
        void HideEntry(string entryId, bool hide);
        IPropertyEntry FindItem(string name);
    }
    public struct MouseState
    {
        public MouseState(MouseFlags flags, MouseButton button, int x, int y, int wheelDiff)
        {
            this.flags = flags;
            this.button = button;
            this.x = x;
            this.y = y;
            this.wheelDiff = wheelDiff;
        }
        public enum MouseFlags { up, down, click, entering, leaving };
        public enum MouseButton { left, middle, right, wheel };
        public MouseFlags flags;
        public MouseButton button;
        public int x, y;
        public int wheelDiff;
    }
    public enum PropertyEntryButton { contextMenu, dropDown, ok, cancel, check, value, locked, directMenu, doubleclick };
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
        /// Clicking on the entry calls<see cref="IPropertyEntry.ButtonClicked(PropertyEntryButton)"/> with the flag <see cref="PropertyEntryButton.directMenu", displayed as right arrow/>
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
        /// Returns the sub-items, will only be called when <see cref="EntryType.HasSubEntries"/> is set.
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
        /// Notification that the label or value part is being edited. Will only be called when <see cref="Flags> contains <see cref="PropertyEntryType.ValueEditable"/> or <see cref="PropertyEntryType.LabelEditable"> is set.
        /// </summary>
        /// <param name="editValue">true: editing the value part, false: editing the label part</param>
        void StartEdit(bool editValue);
        /// <summary>
        /// Notification when the editing process is being terminated. 
        /// </summary>
        /// <param name="aborted">true: the edit has been aborted, false: normal end of edit process</param>
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
        /// <param name="selectedIndex"></param>
        void Selected(IPropertyEntry previousSelected);
        /// <summary>
        /// This item was selected in the property page but is no more selected. The item <paramref name="nowSelected"/> will be selected instead (may be null).
        /// </summary>
        /// <param name="selectedIndex"></param>
        void UnSelected(IPropertyEntry nowSelected);
        /// <summary>
        /// Select this entry
        /// </summary>
        void Select();
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
    public delegate void PreProcessKeyDown(KeyEventArgs keyEventArgs);
    /// <summary>
    /// notifies the event receiver, that the selection has changed
    /// </summary>
    /// <param name="unselected">the previous selected item</param>
    /// <param name="selected">the itam, which just has been selected</param>
    public delegate void SelectionChanged(IPropertyEntry unselected, IPropertyEntry selected);
    public interface IPropertyPage
    {
        void BringToFront();
        void Add(IPropertyEntry toAdd, bool showOpen);
        void Remove(IPropertyEntry toRemove);
        void Clear();
        void Refresh(IPropertyEntry toRefresh);
        IPropertyEntry Selected { get; set;  }
        void OpenSubEntries(IPropertyEntry toOpenOrClose, bool open);
        IPropertyEntry GetCurrentSelection();
        void SelectEntry(IPropertyEntry toSelect);
        void MakeVisible(IPropertyEntry toShow);
        IPropertyEntry FindFromHelpLink(string helpResourceID, bool searchTreeAndOpen = false);
        IFrame Frame { get; }
        void StartEditLabel(IPropertyEntry ToEdit);
        #region IPropertyTreeView adapters, should later be removed
        IView ActiveView { get; } // use Frame.ActiveView instead
        IPropertyEntry GetParent(IShowProperty child); // use IPropertyEntry.Parent instead
        IFrame GetFrame(); // use the Frame property instead
        bool IsOpen(IShowProperty toTest); // use IPropertyEntry.IsOpen
        #endregion
        bool ContainsEntry(IPropertyEntry entryWithTextBox);
        bool IsOnTop();
        event PreProcessKeyDown OnPreProcessKeyDown;
        event SelectionChanged OnSelectionChanged;
    }

    public interface IUIService
    {
        GeoObjectList GetDataPresent(object data);
        Substitutes.Keys ModifierKeys { get; }
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
        Substitutes.DialogResult ShowSaveFileDlg(string id, string title, string filter, ref int filterIndex, ref string fileName);
        Substitutes.DialogResult ShowMessageBox(string text, string caption, Substitutes.MessageBoxButtons buttons);
        Substitutes.DialogResult ShowColorDialog(ref Color color);
        /// <summary>
        /// Returns a bitmap from a bitmap strip, name has the format filename:index(, where filename should be part of the resources?)
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        Bitmap GetBitmap(string name);
        event EventHandler ApplicationIdle;
        IPaintTo3D CreatePaintInterface(Bitmap paintToBitmap, double precision);
        DialogResult ShowPageSetupDlg(ref PrintDocument printDocument1, PageSettings pageSettings, out int width, out int height, out bool landscape);
        DialogResult ShowPrintDlg(ref PrintDocument printDocument);
        void SetClipboardData(GeoObjectList objects, bool copy);
        object GetClipboardData(Type typeOfData);
        bool HasClipboardData(Type typeOfData);
        /// <summary>
        /// Shows or hides a progress bar. 
        /// </summary>
        /// <param name="show">true: show, false: hide</param>
        /// <param name="percent">progress percentage (0..100)</param>
        /// <param name="Title">title of the progress bar, null: don't change the current title</param>
        void ShowProgressBar(bool show, double percent = 0.0, string title = null);
    }

}
