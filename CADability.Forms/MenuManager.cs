using CADability.UserInterface;
using System;
using System.Windows.Forms;

namespace CADability.Forms
{
    internal class ContextMenuWithHandler : ContextMenuStrip
    {
        ICommandHandler commandHandler;
        public string menuID;

        internal static ToolStripItem CreateItem(MenuWithHandler def)
        {
            if (def.ID == "SEPARATOR" || def.Text == "-")
                return new ToolStripSeparator();
            return new MenuItemWithHandler(def);
        }

        public ContextMenuWithHandler(ToolStripMenuItem[] menuItems, ICommandHandler handler, string menuID) : base()
        {
            commandHandler = handler;
            this.menuID = menuID;
            Items.AddRange(menuItems);
        }

        public ContextMenuWithHandler(MenuWithHandler[] definition) : base()
        {
            menuID = null;
            commandHandler = null;
            foreach (var def in definition)
                Items.Add(CreateItem(def));
            Closed += (s, e) => MenuItemWithHandler.HideToolTip();
        }

        private void RecurseCommandState(MenuItemWithHandler miid)
        {
            foreach (ToolStripItem mi in miid.DropDownItems)
            {
                if (mi is MenuItemWithHandler submiid)
                {
                    if (commandHandler != null)
                    {
                        CommandState cs = new CommandState();
                        commandHandler.OnUpdateCommand((submiid.Tag as MenuWithHandler).ID, cs);
                        submiid.Enabled = cs.Enabled;
                        submiid.Checked = cs.Checked;
                    }
                    if (submiid.HasDropDownItems) RecurseCommandState(submiid);
                }
            }
        }

        public void UpdateCommand()
        {
            foreach (ToolStripItem mi in Items)
            {
                if (mi is MenuItemWithHandler miid && mi.Tag is MenuWithHandler mwh && mwh.Target != null)
                {
                    CommandState cs = new CommandState();
                    mwh.Target.OnUpdateCommand(mwh.ID, cs);
                    miid.Enabled = cs.Enabled;
                    miid.Checked = cs.Checked;
                    if (miid.HasDropDownItems) RecurseCommandState(miid);
                }
            }
        }

        protected override void OnOpening(System.ComponentModel.CancelEventArgs e)
        {
            UpdateCommand();
            base.OnOpening(e);
        }

        protected override void OnClosed(ToolStripDropDownClosedEventArgs e)
        {
            base.OnClosed(e);
        }

        public void SetCommandHandler(ICommandHandler hc)
        {
            foreach (ToolStripItem mi in Items)
            {
                if (mi is MenuItemWithHandler submiid)
                {
                    (submiid.Tag as MenuWithHandler).Target = hc;
                    if (submiid.HasDropDownItems) SetCommandHandler(submiid, hc);
                }
            }
            commandHandler = hc;
        }

        private void SetCommandHandler(MenuItemWithHandler miid, ICommandHandler hc)
        {
            foreach (ToolStripItem mi in miid.DropDownItems)
            {
                if (mi is MenuItemWithHandler submiid)
                {
                    (submiid.Tag as MenuWithHandler).Target = hc;
                    if (submiid.HasDropDownItems) SetCommandHandler(submiid, hc);
                }
            }
        }

        public delegate void MenuItemSelectedDelegate(string menuId);
        public event MenuItemSelectedDelegate MenuItemSelectedEvent;
        public void FireMenuItemSelected(string menuId) => MenuItemSelectedEvent?.Invoke(menuId);

        private bool ProcessShortCut(Keys keys, ToolStripMenuItem mi)
        {
            if (mi.HasDropDownItems)
            {
                foreach (ToolStripItem mii in mi.DropDownItems)
                    if (mii is ToolStripMenuItem tsmi && ProcessShortCut(keys, tsmi))
                        return true;
            }
            else if (mi.ShortcutKeys == keys && mi is MenuItemWithHandler miid)
            {
                CommandState cs = new CommandState();
                commandHandler.OnUpdateCommand((miid.Tag as MenuWithHandler).ID, cs);
                if (cs.Enabled) commandHandler.OnCommand((miid.Tag as MenuWithHandler).ID);
                return true;
            }
            return false;
        }

        internal bool ProcessShortCut(Keys keys)
        {
            foreach (ToolStripItem mi in Items)
                if (mi is ToolStripMenuItem tsmi && ProcessShortCut(keys, tsmi))
                    return true;
            return false;
        }
    }

    class MenuItemWithHandler : ToolStripMenuItem
    {
        private static ToolTip toolTip = new ToolTip
        {
            AutoPopDelay = 10000,
            InitialDelay = 0,
            ReshowDelay = 0,
            ShowAlways = true
        };

        internal static void HideToolTip()
        {
            if (Form.ActiveForm != null) toolTip.Hide(Form.ActiveForm);
        }

        private static Keys ShortcutKeysFromString(string p)
        {
            if (string.IsNullOrEmpty(p) || p == "None") return Keys.None;
            Keys mods = Keys.None;
            string key = p;
            if (key.StartsWith("CtrlShift")) { mods = Keys.Control | Keys.Shift; key = key.Substring(9); }
            else if (key.StartsWith("Ctrl")) { mods = Keys.Control; key = key.Substring(4); }
            else if (key.StartsWith("Alt")) { mods = Keys.Alt; key = key.Substring(3); }
            else if (key.StartsWith("Shift")) { mods = Keys.Shift; key = key.Substring(5); }

            switch (key)
            {
                case "Del": return mods | Keys.Delete;
                case "Ins": return mods | Keys.Insert;
                case "BkSp": case "Bksp": return mods | Keys.Back;
                case "DownArrow": return mods | Keys.Down;
                case "UpArrow": return mods | Keys.Up;
                case "LeftArrow": return mods | Keys.Left;
                case "RightArrow": return mods | Keys.Right;
                case "F1": return mods | Keys.F1;
                case "F2": return mods | Keys.F2;
                case "F3": return mods | Keys.F3;
                case "F4": return mods | Keys.F4;
                case "F5": return mods | Keys.F5;
                case "F6": return mods | Keys.F6;
                case "F7": return mods | Keys.F7;
                case "F8": return mods | Keys.F8;
                case "F9": return mods | Keys.F9;
                case "F10": return mods | Keys.F10;
                case "F11": return mods | Keys.F11;
                case "F12": return mods | Keys.F12;
                default:
                    if (key.Length == 1)
                    {
                        char c = key[0];
                        if (c >= 'A' && c <= 'Z') return mods | (Keys)c;
                        if (c >= '0' && c <= '9') return mods | (Keys)(Keys.D0 + (c - '0'));
                    }
                    return Keys.None;
            }
        }

        public MenuItemWithHandler(MenuWithHandler definition) : base()
        {
            Text = definition.Text;
            Tag = definition;
            if (!string.IsNullOrEmpty(definition.Shortcut))
            {
                ShortcutKeys = ShortcutKeysFromString(definition.Shortcut);
                ShowShortcutKeys = definition.ShowShortcut;
            }
            if (StringTable.IsStringDefined(definition.ID))
            {
                string tt = StringTable.GetString(definition.ID, StringTable.Category.info);
                if (string.IsNullOrEmpty(tt) || tt.StartsWith("missing string:"))
                    tt = StringTable.GetString(definition.ID, StringTable.Category.label);
                if (!string.IsNullOrEmpty(tt) && !tt.StartsWith("missing string:"))
                    ToolTipText = tt;
            }
            if (definition.SubMenus != null)
            {
                foreach (var sub in definition.SubMenus)
                    DropDownItems.Add(ContextMenuWithHandler.CreateItem(sub));
            }
            DropDownOpening += HandleDropDownOpening;
        }

        protected override void OnClick(EventArgs e)
        {
            HideToolTip();
            MenuWithHandler definition = Tag as MenuWithHandler;
            if (definition?.Target != null) definition.Target.OnCommand(definition.ID);
        }

        private void HandleDropDownOpening(object sender, EventArgs e)
        {
            HideToolTip();
            foreach (ToolStripItem mi in DropDownItems)
            {
                MenuWithHandler definition = mi.Tag as MenuWithHandler;
                if (definition?.Target != null)
                {
                    CommandState cs = new CommandState();
                    if (definition.Target.OnUpdateCommand(definition.ID, cs))
                    {
                        mi.Enabled = cs.Enabled;
                        if (mi is MenuItemWithHandler miw) miw.Checked = cs.Checked;
                    }
                }
            }
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
            foreach (var def in definition)
                res.Items.Add(ContextMenuWithHandler.CreateItem(def));
            res.MenuDeactivate += (s, e) => MenuItemWithHandler.HideToolTip();
            return res;
        }
    }
}
