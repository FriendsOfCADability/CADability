using CADability.UserInterface;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CADability.Forms
{
    internal class ContextMenuWithHandler : ContextMenuStrip
    {
        ICommandHandler commandHandler;
        public string menuID;

        public ContextMenuWithHandler(ToolStripItem[] items, ICommandHandler Handler, string menuID)
        {
            Items.AddRange(items);
            commandHandler = Handler;
            this.menuID = menuID;
        }

        public ContextMenuWithHandler(MenuWithHandler[] definition)
        {
            this.menuID = null;
            commandHandler = null;
            for (int i = 0; i < definition.Length; i++)
            {
                Items.Add(MenuItemWithHandler.CreateItem(definition[i]));
            }
            this.Closed += (s, e) =>
            {
                MenuItemWithHandler.HideToolTip();
                BeginInvoke(new Action(() => { if (!IsDisposed) Dispose(); }));
            };
        }

        private void RecurseCommandState(MenuItemWithHandler miid)
        {
            foreach (ToolStripItem item in miid.DropDownItems)
            {
                MenuItemWithHandler submiid = item as MenuItemWithHandler;
                if (submiid != null)
                {
                    if (commandHandler != null)
                    {
                        CommandState commandState = new CommandState();
                        commandHandler.OnUpdateCommand((submiid.Tag as MenuWithHandler).ID, commandState);
                        submiid.Enabled = commandState.Enabled;
                        submiid.Checked = commandState.Checked;
                    }
                    if (submiid.HasDropDownItems) RecurseCommandState(submiid);
                }
            }
        }
        public void UpdateCommand()
        {
            foreach (ToolStripItem item in Items)
            {
                MenuItemWithHandler miid = item as MenuItemWithHandler;
                if (miid != null && miid.Tag is MenuWithHandler menuWithHandler)
                {
                    if (menuWithHandler.Target != null)
                    {
                        CommandState commandState = new CommandState();
                        menuWithHandler.Target.OnUpdateCommand(menuWithHandler.ID, commandState);
                        miid.Enabled = commandState.Enabled;
                        miid.Checked = commandState.Checked;
                        if (miid.HasDropDownItems) RecurseCommandState(miid);
                    }
                }
            }
        }
        protected override void OnOpening(CancelEventArgs e)
        {
            UpdateCommand();
            base.OnOpening(e);
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
        public void SetCommandHandler(ICommandHandler hc)
        {
            foreach (ToolStripItem item in Items)
            {
                MenuItemWithHandler submiid = item as MenuItemWithHandler;
                if (submiid != null)
                {
                    (submiid.Tag as MenuWithHandler).Target = hc;
                    if (submiid.HasDropDownItems) SetCommandHandler(submiid, hc);
                }
            }
            this.commandHandler = hc;
        }
        private void SetCommandHandler(MenuItemWithHandler miid, ICommandHandler hc)
        {
            foreach (ToolStripItem item in miid.DropDownItems)
            {
                MenuItemWithHandler submiid = item as MenuItemWithHandler;
                if (submiid != null)
                {
                    (submiid.Tag as MenuWithHandler).Target = hc;
                    if (submiid.HasDropDownItems) SetCommandHandler(submiid, hc);
                }
            }
        }
        public delegate void MenuItemSelectedDelegate(string menuId);
        public event MenuItemSelectedDelegate MenuItemSelectedEvent;
        public void FireMenuItemSelected(string menuId)
        {
            if (MenuItemSelectedEvent != null) MenuItemSelectedEvent(menuId);
        }
        private bool ProcessShortCut(Keys keys, ToolStripMenuItem mi)
        {
            if (mi.HasDropDownItems)
            {
                foreach (ToolStripItem item in mi.DropDownItems)
                {
                    if (item is ToolStripMenuItem mii && ProcessShortCut(keys, mii))
                        return true;
                }
            }
            else if (mi.ShortcutKeys == keys)
            {
                MenuItemWithHandler miid = mi as MenuItemWithHandler;
                if (miid != null && commandHandler != null)
                {
                    CommandState commandState = new CommandState();
                    commandHandler.OnUpdateCommand((miid.Tag as MenuWithHandler).ID, commandState);
                    if (commandState.Enabled)
                        commandHandler.OnCommand((miid.Tag as MenuWithHandler).ID);
                }
                return true;
            }
            return false;
        }
        internal bool ProcessShortCut(Keys keys)
        {
            foreach (ToolStripItem item in Items)
            {
                if (item is ToolStripMenuItem mi && ProcessShortCut(keys, mi))
                    return true;
            }
            return false;
        }
    }

    class MenuItemWithHandler : ToolStripMenuItem
    {
        private static Timer hoverTimer;
        public static ToolTip toolTip;
        private static Timer autoHideTimer;
        private static MenuItemWithHandler currentItem;
        private static string currentToolTipText;

        private static Keys ShortcutFromString(string p)
        {
            if (string.IsNullOrEmpty(p) || p == "None") return Keys.None;
            if (Enum.TryParse<Shortcut>(p, out Shortcut s)) return (Keys)(int)s;
            return Keys.None;
        }

        static MenuItemWithHandler()
        {
            hoverTimer = new Timer();
            hoverTimer.Interval = 500;
            hoverTimer.Tick += HoverTimer_Tick;

            toolTip = new ToolTip();
            toolTip.AutoPopDelay = 10000;
            toolTip.InitialDelay = 0;
            toolTip.ReshowDelay = 0;
            toolTip.ShowAlways = true;

            autoHideTimer = new Timer();
            autoHideTimer.Interval = 5000;
            autoHideTimer.Tick += AutoHideTimer_Tick;
        }

        internal static ToolStripItem CreateItem(MenuWithHandler definition)
        {
            if (definition.Text == "-") return new ToolStripSeparator();
            return new MenuItemWithHandler(definition);
        }

        public MenuItemWithHandler(MenuWithHandler definition) : base()
        {
            Text = definition.Text;
            Tag = definition;
            if (!string.IsNullOrEmpty(definition.Shortcut))
            {
                ShortcutKeys = ShortcutFromString(definition.Shortcut);
                ShowShortcutKeys = definition.ShowShortcut;
            }

            if (definition.ImageIndex >= 0)
            {
                int ind = definition.ImageIndex;
                if (ind >= 10000) ind = ind - 10000 + ButtonImages.OffsetUserImages;
                if (ind < ButtonImages.ButtonImageList.Images.Count)
                    Image = ButtonImages.ButtonImageList.Images[ind];
            }

            if (definition.SubMenus != null)
            {
                for (int i = 0; i < definition.SubMenus.Length; i++)
                {
                    DropDownItems.Add(CreateItem(definition.SubMenus[i]));
                }
            }

            MouseEnter += OnMenuItemMouseEnter;
            MouseLeave += (s, e) =>
            {
                hoverTimer.Stop();
                HideToolTip();
                if (ReferenceEquals(currentItem, this))
                {
                    currentItem = null;
                    currentToolTipText = "";
                }
            };
        }

        private void OnMenuItemMouseEnter(object sender, EventArgs e)
        {
            MenuWithHandler definition = Tag as MenuWithHandler;
            if (definition?.Target != null) definition.Target.OnSelected(definition, true);

            currentItem = this;
            currentToolTipText = "";
            if (StringTable.IsStringDefined(definition.ID))
            {
                string tt = StringTable.GetString(definition.ID, StringTable.Category.info);
                if (string.IsNullOrEmpty(tt) || tt.StartsWith("missing string:")) tt = StringTable.GetString(definition.ID, StringTable.Category.label);
                if (!string.IsNullOrEmpty(tt) && !tt.StartsWith("missing string:")) currentToolTipText = tt;
            }

            hoverTimer.Stop();
            if (!string.IsNullOrEmpty(currentToolTipText))
                hoverTimer.Start();

            HideToolTip();
        }

        protected override void OnClick(EventArgs e)
        {
            HideToolTip();
            hoverTimer.Stop();
            MenuWithHandler definition = (Tag as MenuWithHandler);
            if (definition.Target != null) definition.Target.OnCommand(definition.ID);

            // find and dispose the root ContextMenuWithHandler to avoid blank menu text accumulation
            // defer disposal so WinForms can finish its closing sequence before the object is released
            ToolStrip ts = this.Owner;
            while (ts != null && !(ts is ContextMenuWithHandler))
            {
                if (ts is ToolStripDropDownMenu ddm && ddm.OwnerItem != null)
                    ts = ddm.OwnerItem.Owner;
                else break;
            }
            if (ts is ContextMenuWithHandler rootMenu && !rootMenu.IsDisposed)
            {
                rootMenu.BeginInvoke((Action)(() =>
                {
                    if (!rootMenu.IsDisposed)
                        rootMenu.Dispose();
                }));
            }
        }

        protected override void OnDropDownShow(EventArgs e)
        {
            HideToolTip();

            foreach (ToolStripItem item in DropDownItems)
            {
                MenuItemWithHandler submiid = item as MenuItemWithHandler;
                if (submiid != null)
                {
                    MenuWithHandler definition = submiid.Tag as MenuWithHandler;
                    if (definition?.Target != null)
                    {
                        CommandState commandState = new CommandState();
                        if (definition.Target.OnUpdateCommand(definition.ID, commandState))
                        {
                            submiid.Enabled = commandState.Enabled;
                            submiid.Checked = commandState.Checked;
                        }
                    }
                }
            }
            base.OnDropDownShow(e);
        }

        private static void HoverTimer_Tick(object sender, EventArgs e)
        {
            hoverTimer.Stop();
            if (currentItem != null && !string.IsNullOrEmpty(currentToolTipText))
            {
                Point mousePos = Cursor.Position;
                Form ownerForm = Form.ActiveForm;
                if (ownerForm != null)
                {
                    Point formPos = ownerForm.PointToClient(mousePos);
                    toolTip.Show(currentToolTipText, ownerForm, formPos.X + 10, formPos.Y + 10);
                    autoHideTimer.Stop();
                    autoHideTimer.Start();
                }
            }
        }

        private static void AutoHideTimer_Tick(object sender, EventArgs e)
        {
            HideToolTip();
        }

        internal static void HideToolTip()
        {
            Form ownerForm = currentItem?.GetOwnerForm();
            if (ownerForm != null)
                toolTip.Hide(ownerForm);
            autoHideTimer.Stop();
        }

        private Form GetOwnerForm()
        {
            return Form.ActiveForm;
        }
    }

    class MenuManager
    {
        static internal ContextMenuWithHandler MakeContextMenu(MenuWithHandler[] definition)
        {
            return new ContextMenuWithHandler(definition);
        }

        static internal MenuStrip MakeMainMenu(MenuWithHandler[] definition)
        {
            MenuStrip res = new MenuStrip();
            for (int i = 0; i < definition.Length; i++)
            {
                res.Items.Add(MenuItemWithHandler.CreateItem(definition[i]));
            }
            res.MenuDeactivate += (s, e) => MenuItemWithHandler.HideToolTip();
            return res;
        }
    }
}
