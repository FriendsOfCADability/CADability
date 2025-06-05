using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CADability.Forms
{

    public class PropertiesExplorer : Control, IControlCenter
    {
        private List<PropertyPage> tabPages; // the TabPages with titleId and iconId
        TabControl tabControl; // the TabControl containing the tab pages
        public IFrame Frame { get; set; }
        HashSet<string> entriesToHide;
        public ImageList ImageList { set { tabControl.ImageList = value; } }
        public PropertiesExplorer()
        {
            tabPages = new List<PropertyPage>();

            listBox = new ListBox(); // a ListBox which only becomes visible (and positioned and filled) when a drop-down button of an PropertyEntry has been clicked
            listBox.Visible = false;
            listBox.LostFocus += ListBox_LostFocus;
            listBox.SelectedIndexChanged += ListBox_SelectedIndexChanged;
            listBox.MouseClick += ListBox_MouseClick;
            listBox.DrawMode = DrawMode.OwnerDrawFixed;
            listBox.Name = "the list box";
            listBox.TabStop = false;
            listBox.DrawItem += ListBox_DrawItem;

            textBox = new TextBox();
            textBox.Visible = false;
            textBox.KeyUp += TextBox_KeyUp;
            textBox.Name = "the text box";
            textBox.TabStop = false;

            labelExtension = new Label();
            labelExtension.Visible = false;
            labelExtension.Name = "the label extension";
            labelExtension.TabStop = false;
            labelExtension.Paint += LabelExtension_Paint;

            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.Name = "the tab control";
            tabControl.TabStop = false; // we have our own tab handling mechanism
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.ItemSize = new Size(LogicalToDeviceUnits(27), LogicalToDeviceUnits(21)); //LogicalToDeviceUnits to support HighDpi, otherwise tab will be very small.
            tabControl.DrawItem += tabControl_DrawItem;
            tabControl.ShowToolTips = true;

            Controls.Add(tabControl);
            Controls.Add(listBox);
            Controls.Add(textBox);
            Controls.Add(labelExtension);

            ImageList imageList = new ImageList();
            imageList.ImageSize = new Size(16, 16);

            System.Drawing.Bitmap bmp = BitmapTable.GetBitmap("Icons.bmp");
            Color clr = bmp.GetPixel(0, 0);
            if (clr.A != 0) bmp.MakeTransparent(clr);
            imageList.Images.AddStrip(bmp);
            tabControl.ImageList = imageList;

            entriesToHide = new HashSet<string>();
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ActivePropertyPage != null) //Can be null if the selected tab is not a PropertyPage.
            {
                if (EntryWithTextBox != null && !ActivePropertyPage.ContainsEntry(EntryWithTextBox))
                {
                    EntryWithTextBox.EndEdit(true, textBox.Modified, textBox.Text);
                    HideTextBox();
                }
                if (EntryWithListBox != null && !ActivePropertyPage.ContainsEntry(EntryWithTextBox))
                {
                    HideListBox();
                }
                ActivePropertyPage.SelectEntry(ActivePropertyPage.GetCurrentSelection()); // this shows the text-box or list-box
            }
        }
        private static PropertyPage GetPropertyPage(IPropertyEntry propertyEntry)
        {
            IPropertyEntry pe = propertyEntry;
            while (pe != null)
            {
                if (pe.Parent is IPropertyPage propertyPage) return propertyPage as PropertyPage;
                else pe = pe.Parent as IPropertyEntry;
            }
            return null;
        }

        #region listBox 
        ListBox listBox; // the only listbox, which is hidden and, when needed, moved and filled before it is shown
        public void ShowListBox(Rectangle screenLocation, string[] items, int selected, IPropertyEntry sender)
        {
            if (EntryWithListBox == sender) return;
            EntryWithListBox = sender;
            int height = items.Length * screenLocation.Height + 2 * SystemInformation.BorderSize.Height + listBox.Margin.Top + listBox.Margin.Bottom;
            Rectangle entryRect = RectangleToClient(screenLocation);
            Rectangle bounds;
            if (entryRect.Top > ClientRectangle.Height - entryRect.Bottom)
            {
                // place the list box above the entry
                height = Math.Min(height, entryRect.Top);
                bounds = new Rectangle(entryRect.Left, entryRect.Top - height, entryRect.Width, height);
            }
            else
            {
                // place the list box below the entry
                height = Math.Min(height, ClientRectangle.Height - entryRect.Bottom);
                bounds = new Rectangle(entryRect.Left, entryRect.Bottom, entryRect.Width, height);
            }
            listBox.Items.AddRange(items);
            listBox.Visible = true;
            listBox.BringToFront();
            listBox.ItemHeight = screenLocation.Height;
            listBox.SelectedIndex = selected;
            listBox.Bounds = bounds;
            listBox.Show();
            listBox.Focus();
        }
        public void HideListBox()
        {
            if (EntryWithListBox != null)
            {
                PropertyPage pp = GetPropertyPage(EntryWithListBox);
                IPropertyEntry elb = EntryWithListBox;
                EntryWithListBox = null;
                pp?.Refresh(elb);
            }
            listBox.Hide();
            listBox.Items.Clear();
        }
        private void ListBox_MouseClick(object sender, MouseEventArgs e)
        {
            EntryWithListBox.ListBoxSelected(listBox.SelectedIndex);
            HideListBox();
        }
        private void ListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // we (currently) do not react on a simple change (e.g. with the arrow keys) of the selection, only when clicked or enter pressed we notify the entry
        }
        private void ListBox_LostFocus(object sender, EventArgs e)
        {
            if (EntryWithListBox != null)
            {   // nothing has been selected, the user clicked somewhere else
                HideListBox();
            }
        }
        private void ListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            string text = String.Empty;
            if (e.Index > -1)
                text = listBox.Items[e.Index].ToString();
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                e.Graphics.FillRectangle(SystemBrushes.Highlight, e.Bounds);
                PropertyPage.DrawString(e.Graphics, text, this.Font, SystemBrushes.HighlightText, e.Bounds, e.State == DrawItemState.Checked);
            }
            else
            {
                e.Graphics.FillRectangle(SystemBrushes.Window, e.Bounds);
                PropertyPage.DrawString(e.Graphics, text, this.Font, SystemBrushes.ControlText, e.Bounds, e.State == DrawItemState.Checked);
            }
        }
        #endregion

        #region textBox
        TextBox textBox; // there is only one TextBox for editing labels or values. It is normally hidden and moved, filled and activated when needed
        bool endEidtCalled;
        private ErrorProvider errorProvider = new ErrorProvider();
        public void ShowTextBox(Rectangle screenLocation, string initialText, IPropertyEntry sender, Point screenClickPos)
        {
            if (EntryWithTextBox != null) EntryWithTextBox.EndEdit(true, textBox.Modified, textBox.Text);
            EntryWithTextBox = sender;
            textBox.Text = initialText;
            textBox.Modified = false;
            textBox.Visible = true;
            textBox.Bounds = RectangleToClient(screenLocation);
            textBox.BringToFront();
            int lastInd = Math.Max(initialText.Length - 1, 0);
            Point lastCharPos = textBox.GetPositionFromCharIndex(initialText.Length - 1);
            int charWidth = textBox.Font.Height * 5 / 7; // some guess
            if (initialText.Length > 0) charWidth = lastCharPos.X / initialText.Length;
            Point clickPos = textBox.PointToClient(screenClickPos);
            int charindex = textBox.GetCharIndexFromPosition(new Point(clickPos.X + charWidth / 2, clickPos.Y));
            if (clickPos.X > lastCharPos.X + charWidth)
            {   // clicked behind the last character: select all
                textBox.SelectAll();
            }
            else
            {   // clicked somewhere inside the text
                textBox.Select(charindex, 0);
            }
            textBox.Show();
            textBox.Focus();
        }
        public void HideTextBox()
        {
            // we cannot call EntryWithTextBox.EndEdit, because we don't know, whether it was aborted or not.
            // EntryWithTextBox.EndEdit must be called where Tab, Enter or click occurs
            if (EntryWithTextBox != null)
            {
                PropertyPage pp = GetPropertyPage(EntryWithTextBox);
                IPropertyEntry etb = EntryWithTextBox;
                EntryWithTextBox = null;
                pp?.Refresh(etb);
            }
            textBox.Hide();
        }
        private void TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (EntryWithTextBox != null && e.KeyCode != Keys.Tab && textBox.Modified)
            {   // EditTextChanged fixes the input of a ConstructAction, i.e. the mouse-move is not forwarded to this input any more
                // so if you start entering text into a text-box you don't want the mouse-movement to alter this text.
                // Now using the member variable textBoxIsUpdatingFromKeystroke, which is true, when a change happens because of a keystroke
                // in the text-box.
                textBoxIsUpdatingFromKeystroke = true;
                try
                {
                    bool ok = EntryWithTextBox.EditTextChanged(textBox.Text);
                    textBox.Modified = false; // so on EndEdit we do not update the same value twice
                    if (ok) errorProvider.SetError(textBox, ""); // this mechanism allows to set an error to the input field. 
                    else errorProvider.SetError(textBox, EntryWithTextBox.GetErrorText()); // Must be implemented in ConstructAction.IInputObject or EditableProperty
                }
                finally
                {
                    textBoxIsUpdatingFromKeystroke = false;
                }

            }
        }
        bool textBoxIsUpdatingFromKeystroke = false;
        #endregion

        #region labelextension
        Label labelExtension; // there is only one LabelExtension for editing labels or values. It is normally hidden and moved, filled and activated when needed
        public void ShowLabelExtension(Rectangle screenLocation, string initialText, IPropertyEntry sender)
        {
            EntryWithLabelExtension = sender;
            labelExtension.Font = textBox.Font;
            labelExtension.BackColor = textBox.BackColor;
            labelExtension.Text = initialText;
            labelExtension.Visible = true;
            labelExtension.Bounds = RectangleToClient(screenLocation);
            labelExtension.BringToFront();
            labelExtension.Show();
            labelExtension.Focus();
        }
        public void HideLabelExtension()
        {
            EntryWithLabelExtension = null;
            labelExtension.Hide();
        }
        private void LabelExtension_Paint(object sender, PaintEventArgs e)
        {
            StringFormat stringFormat = new StringFormat(StringFormat.GenericDefault);
            stringFormat.FormatFlags = StringFormatFlags.NoWrap;
            stringFormat.LineAlignment = StringAlignment.Center;
            stringFormat.Alignment = StringAlignment.Center;
            e.Graphics.DrawString(EntryWithLabelExtension.Label, Font, SystemBrushes.ControlText, labelExtension.Bounds, stringFormat);
        }
        #endregion

        private void tabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            try
            {
                TabPage tp = tabControl.TabPages[e.Index];
                Image img = tabControl.ImageList.Images[tp.ImageIndex];
                int x = e.Bounds.Left + (e.Bounds.Width - img.Width) / 2;
                int y = e.Bounds.Top + (e.Bounds.Height - img.Height) / 2;
                if ((e.State & DrawItemState.Selected) == 0) y += 2;
                Brush brush;
                if ((e.State & DrawItemState.Selected) == 0)
                    brush = new SolidBrush(SystemColors.Control);
                else
                    brush = new SolidBrush(SystemColors.Window);
                e.Graphics.FillRectangle(brush, e.Bounds);
                brush.Dispose();
                e.Graphics.DrawImageUnscaled(img, x, y);
            }
            catch (ArgumentException) { }
        }
        public IPropertyPage AddPropertyPage(string titleId, int iconId)
        {
            PropertyPage res = new PropertyPage(titleId, iconId, this);
            tabPages.Add(res);
            res.Dock = DockStyle.Fill;
            res.Frame = Frame;
            TabPage tp = new TabPage();
            tp.Controls.Add(res);
            tabControl.TabPages.Add(tp);
            tp.Text = StringTable.GetString(titleId + "TabPage");
            tp.ImageIndex = iconId;
            tp.ToolTipText = StringTable.GetString(titleId + "TabPage", StringTable.Category.tip);
            tp.Tag = res;

            return res;
        }
        public void AddTabPage(TabPage tp)
        {
            tabControl.TabPages.Add(tp);
        }
        public IPropertyPage ActivePropertyPage
        {
            get
            {
                IPropertyPage selected = null;
                if (tabControl.SelectedTab != null)
                {
                    selected = tabControl.SelectedTab.Tag as IPropertyPage;
                }
                return selected;
            }
        }
        public TabPage ActiveTabPage
        {
            get { return tabControl.SelectedTab; }
        }
        public bool SelectNextEntry(bool forward)
        {
            PropertyPage sel = ActivePropertyPage as PropertyPage;
            if (sel != null)
            {
                sel.SelectNextPropertyEntry(forward);
                return true;
            }
            return false;
        }
        public IPropertyPage GetPropertyPage(string titleId)
        {
            for (int i = 0; i < tabPages.Count; i++)
            {
                if (tabPages[i].TitleId == titleId) return tabPages[i];
            }
            return null;
        }
        public void DisplayHelp(string helpID)
        {
            throw new NotImplementedException();
        }
        public bool ShowPropertyPage(string titleId)
        {
            for (int i = 0; i < tabPages.Count; i++)
            {
                if (tabPages[i].TitleId == titleId)
                {
                    if (!tabPages[i].Visible) tabPages[i].Show();
                    tabControl.SelectedIndex = i;
                    return true;
                }
            }
            return false;
        }
        public bool RemovePropertyPage(string titleId)
        {
            for (int i = 0; i < tabPages.Count; i++)
            {
                if (tabPages[i].TitleId == titleId)
                {
                    tabControl.TabPages.Remove(tabPages[i].Parent as TabPage);
                    tabPages.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }
        public void Refresh(IPropertyEntry toRefresh)
        {
            // we get here e.g. when a textBox is open and we start to enter a value. then in the construct action the "highlight" flag is removed and
            // refresh is called. But we are in the middle of entering text and changing the value would not be desired.
            if (EntryWithTextBox == toRefresh && textBox.Visible && !textBoxIsUpdatingFromKeystroke)
            {
                textBox.BeginInvoke((Action)delegate () // may be called from a worker thread
                {
                    if (EntryWithTextBox == toRefresh) // this was necessary because it might be called after the textBox entry had changed
                    {
                        bool allSelected = textBox.SelectionLength == textBox.Text.Length;
                        //Save caret position here. If the textBox.Text property is set, the caret will be moved to the first position.
                        //e.g. If you input text into the LengthProperty the cursor will always move to the first position
                        int caretPos = textBox.SelectionStart;
                        if (textBox.Text != toRefresh.Value)
                        {
                            textBox.Text = toRefresh.Value;
                            //Restore last position
                            textBox.SelectionStart = caretPos;
                        }

                        textBox.Modified = false;
                        if (allSelected) textBox.SelectAll();
                        textBox.Update(); // to show smooth updating of the text when the mouse is moving
                    }
                });
            }
        }
        public void UnSelected(IPropertyEntry unselected)
        {   // the entry with the textBox or listBox is being unselected (by as mouseClick on a different entry), so we accept the input as valid (not aborted)
            if (EntryWithTextBox == unselected)
            {
                EntryWithTextBox.EndEdit(false, textBox.Modified, textBox.Text);
                HideTextBox();
            }
            else if (EntryWithListBox == unselected)
            {
                EntryWithListBox.ListBoxSelected(listBox.SelectedIndex);
                HideListBox();
            }
        }
        public void Selected(IPropertyEntry selectedEntry)
        {
            if (EntryWithTextBox != null && EntryWithTextBox != selectedEntry) HideTextBox();
            if (EntryWithListBox != null && EntryWithListBox != selectedEntry) HideListBox();
        }
        public void PreProcessKeyDown(Substitutes.KeyEventArgs e)
        {   // this is being called after the ActiveAction has preprocessed (and not handled) the key down message
            // we can handle it here, before it gets handled by maybe and edit text box or something else (menu?)
            switch ((Substitutes.Keys)((int)e.KeyData & 0x0FFFF)) // was KeyCode, but didn't work
            {
                case Substitutes.Keys.Tab:
                    if ((e.KeyData & Substitutes.Keys.Control) != 0)
                    {   // we use the Ctrl key to switch between property pages
                        int sel = -1;
                        for (int i = 0; i < tabControl.TabPages.Count; i++)
                        {
                            if (tabControl.TabPages[i] == tabControl.SelectedTab)
                            {
                                sel = i;
                                break;
                            }
                        }
                        if (sel >= 0)
                        {
                            if ((e.KeyData & Substitutes.Keys.Shift) != 0) sel = (sel + tabControl.TabPages.Count - 1) % tabControl.TabPages.Count;
                            else sel = (sel + 1) % tabControl.TabPages.Count;
                            tabControl.SelectedTab = tabControl.TabPages[sel];
                        }
                    }
                    else
                    {
                        if (EntryWithTextBox != null)
                        {
                            EntryWithTextBox.EndEdit(false, textBox.Modified, textBox.Text);
                            HideTextBox();
                        }
                        else if (EntryWithListBox != null)
                        {
                            EntryWithListBox.ListBoxSelected(listBox.SelectedIndex);
                            HideListBox();
                        }
                        SelectNextEntry((e.KeyData & Substitutes.Keys.Shift) == 0);
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    break;
                case Substitutes.Keys.Enter:
                    if (EntryWithTextBox != null)
                    {
                        EntryWithTextBox.EndEdit(false, textBox.Modified, textBox.Text);
                        HideTextBox();
                    }
                    else if (EntryWithListBox != null)
                    {
                        EntryWithListBox.ListBoxSelected(listBox.SelectedIndex);
                        HideListBox();
                    }
                    else if (ActivePropertyPage is PropertyPage pp)
                    {
                        pp.OnEnter((e.KeyData & Substitutes.Keys.Control) != 0);
                    }
                    e.Handled = true;
                    break;
                case Substitutes.Keys.Escape:
                    if (EntryWithTextBox != null)
                    {
                        EntryWithTextBox.EndEdit(true, textBox.Modified, textBox.Text);
                        HideTextBox();
                        e.Handled = true;
                    }
                    else if (EntryWithListBox != null)
                    {
                        // no notification of the entry, nothing has changed until now
                        HideListBox();
                        e.Handled = true;
                    }
                    else if (ActivePropertyPage is PropertyPage pp)
                    {
                        e.Handled = pp.OnEscape((e.KeyData & Substitutes.Keys.Control) != 0);
                    }
                    break;
                case Substitutes.Keys.Down:
                    e.Handled = SelectNextEntry(true);
                    break;
                case Substitutes.Keys.Up:
                    e.Handled = SelectNextEntry(false);
                    break;
            }
            if (!e.SuppressKeyPress)
            {
                (ActivePropertyPage as PropertyPage).PreProcessKeyDown(e);
            }
        }
        public void HideEntry(string entryId, bool hide)
        {
            if (hide) entriesToHide.Add(entryId);
            else entriesToHide.Remove(entryId);
        }
        public bool isHidden(string entryId)
        {
            return entriesToHide.Contains(entryId);
        }
        public IPropertyEntry EntryWithListBox { get; private set; }
        public IPropertyEntry EntryWithTextBox { get; private set; }
        public IPropertyEntry EntryWithLabelExtension { get; private set; }
        protected override void Dispose(bool disposing)
        {
            listBox.LostFocus -= ListBox_LostFocus;
            listBox.DrawItem -= ListBox_DrawItem;
            textBox.KeyUp -= TextBox_KeyUp;
            labelExtension.Paint -= LabelExtension_Paint;
            tabControl.SelectedIndexChanged -= TabControl_SelectedIndexChanged;
            tabControl.DrawItem -= tabControl_DrawItem;
            listBox.Dispose();
            textBox.Dispose();
            labelExtension.Dispose();
            for (int i = 0; i < tabPages.Count; i++)
            {
                tabPages[i].Dispose();
            }
            tabControl.Dispose();
            base.Dispose(disposing);
        }
        public IPropertyEntry FindItem(string name)
        {
            IPropertyPage pp = ActivePropertyPage;
            if (pp != null)
            {
                return pp.FindFromHelpLink(name, true);
            }
            return null;
        }

    }
}

