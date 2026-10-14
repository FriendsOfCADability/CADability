using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CADability.Forms
{
    class ToolBars
    {
        /// <summary>
        /// The Tag of a ToolStripSplitButton or ToolStripButton. Used to handle the clicks on this button
        /// </summary>
        private class TagInfo : ICommandHandler
        {
            private readonly string menuId;
            private readonly bool showLastSubMenu; // if true: in a popup menu change the button image to the last executed command of this menu
            private string lastSubMenuId; // the MenuId of the last executed command of this popup menu
            private readonly ICommandHandler commandHandler;
            private readonly ToolStripItem parent;
            public TagInfo(string menuId, ICommandHandler commandHandler, ToolStripItem parent, bool showLastSubMenu = false)
            {
                this.menuId = menuId;
                this.showLastSubMenu = showLastSubMenu;
                lastSubMenuId = "";
                this.commandHandler = commandHandler;
                this.parent = parent;
            }
            bool ICommandHandler.OnCommand(string MenuId)
            {   // ToolStripSplitButton create a popup menu, which is directed to this tag.
                // The ToolStripSplitButton contains the icon of the last selected menu item of the sub menu,
                // which will be used, when the button (and not the drop down part) of the ToolStripSplitButton is clicked.
                if (showLastSubMenu)
                {
                    lastSubMenuId = MenuId;
                    int ImageIndex = MenuResource.FindImageIndex(MenuId);
                    if (ImageIndex >= 0)
                    {
                        parent.Image = ButtonImages.ButtonImageList.Images[ImageIndex];
                    }
                }
                return commandHandler.OnCommand(MenuId);
            }
            bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
            {
                return commandHandler.OnUpdateCommand(MenuId, CommandState);
            }
            void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
            internal void UpdateCommandState()
            {
                CommandState commandState = new CommandState(); // default: enabled but not checked
                if (commandHandler.OnUpdateCommand(menuId, commandState))
                {
                    parent.Enabled = commandState.Enabled;
                    if (parent is ToolStripButton btn)
                    {
                        btn.Checked = commandState.Checked;
                    }
                }
            }
            internal void ButtonClicked(object sender, EventArgs e)
            {
                if (sender is ToolStripSplitButton)
                {
                    ToolStripSplitButton btn = sender as ToolStripSplitButton;
                    if (!btn.ButtonPressed || lastSubMenuId == "")
                    {   // clicked on the dropdown button, or first time click in the button
                        ContextMenuWithHandler cm = MenuManager.MakeContextMenu(MenuResource.LoadMenuDefinition(menuId, false, this));
                        Control btnParent = parent.GetCurrentParent();
                        System.Drawing.Point pnt;
                        if (!btnParent.Visible)
                        {
                            pnt = btnParent.PointToScreen(new System.Drawing.Point(btn.Bounds.Left, btn.Bounds.Bottom));
                            btnParent = Application.OpenForms[0];
                            pnt = btnParent.PointToClient(pnt);
                        }
                        else
                        {
                            pnt = new System.Drawing.Point(btn.Bounds.Left, btn.Bounds.Bottom);
                        }
                        cm.Show(btnParent, pnt);
                        // if a menu item will be selected, the tagInfo.lastSubMenuId will be set to the selected menu item
                        // and next time, tagInfo.lastSubMenuId will be executed imediately
                    }
                    else
                    {   // tagInfo.lastSubMenuId contains the menu id of the last selected menu item. This will be executed
                        bool ok = commandHandler.OnCommand(lastSubMenuId);
                        // if not ok, the command has not been handled
                    }
                }
                else
                {
                    bool ok = commandHandler.OnCommand(menuId);
                    // if not ok, the command has not been handled
                }
            }
        }
        /// <summary>
        /// Make some standard toolbars, handling their commands with the provided <paramref name="commandHandler"/>
        /// </summary>
        /// <param name="name">one of: File, Edit, Zoom, Construct, Snap, Object</param>
        /// <param name="commandHandler"></param>
        /// <returns></returns>
        public static ToolStrip MakeStandardToolStrip(string name, ICommandHandler commandHandler)
        {
            switch (name)
            {
                case "File":
                    return MakeToolStrip(new string[] {
                        "MenuId.File.New",
                        "MenuId.File.Open",
                        "MenuId.File.Save",
                        "MenuId.File.Print"}, name, commandHandler);
                case "Edit":
                    return MakeToolStrip(new string[] {
                        "MenuId.Edit.Cut",
                        "MenuId.Edit.Copy",
                        "MenuId.Edit.Paste",
                        "MenuId.Edit.Undo",
                        "MenuId.Edit.Redo"}, name, commandHandler);
                case "Zoom":
                    return MakeToolStrip(new string[] {
                        "MenuId.Zoom.Total",
                        "MenuId.Zoom.Detail",
                        "MenuId.Zoom.DetailPlus",
                        "MenuId.Zoom.DetailMinus",
                        "MenuId.Repaint",
                        "MenuId.ViewPoint",
                        "MenuId.ViewFixPoint",
                        "MenuId.Scroll",
                        "MenuId.Zoom",
                        "MenuId.ZAxisUp",
                        "MenuId.Projection.Direction",
                        "MenuId.View.Split"}, name, commandHandler);
                case "Construct":
                    return MakeToolStrip(new string[] {
                        "MenuId.Select",
                        "MenuId.Constr.Point",
                        "MenuId.Constr.Line",
                        "MenuId.Constr.Rect",
                        "MenuId.Constr.Circle",
                        "MenuId.Constr.Arc",
                        "MenuId.Constr.Ellipse",
                        "MenuId.Constr.Ellipsearc",
                        "MenuId.Constr.Polyline",
                        "MenuID.Constr.BSpline.Points",
                        "MenuId.Constr.3DObjects",
                        "MenuId.Constr.Text",
                        "MenuId.Constr.Hatch",
                        "MenuId.Constr.Picture",
                        "MenuId.Constr.Dimension",
                        "MenuId.Constr.Face",
                        "MenuId.Constr.Solid",
                        "MenuId.Tools"}, name, commandHandler);
                case "Snap":
                    return MakeToolStrip(new string[] {
                        "MenuId.Activate.Grid",
                        "MenuId.Activate.Ortho",
                        "MenuId.Snap.ObjectSnapPoint",
                        "MenuId.Snap.ObjectPoint",
                        "MenuId.Snap.DropPoint",
                        "MenuId.Snap.ObjectCenter",
                        "MenuId.Snap.TangentPoint",
                        "MenuId.Snap.Intersections",
                        "MenuId.Snap.Surface"}, name, commandHandler);
                case "Object":
                    return MakeToolStrip(new string[] {
                        "MenuId.Object.Move",
                        "MenuId.Object.Rotate",
                        "MenuId.Object.Scale",
                        "MenuId.Object.Reflect",
                        "MenuId.Object.Snap"}, name, commandHandler);
            }
            return null;
        }
        /// <summary>
        /// Make a ToolStrip, which contains all the provided <paramref name="menuIds"/> and uses the provided <paramref name="commandHandler"/>
        /// </summary>
        /// <param name="menuIds">menu ids to display in thei toolstrip</param>
        /// <param name="name">name of the ToolStrip (its <c>Name</c> property)</param>
        /// <param name="commandHandler">command handler to handle the clicks on the buttons</param>
        /// <returns></returns>
        public static ToolStrip MakeToolStrip(string[] menuIds, string name, ICommandHandler commandHandler)
        {   // there are two kinds of buttons: simple buttons (ToolStripButton), which immediately execute the command when clicked and
            // combo buttons (ToolStripSplitButton), which show a popup menu when clicked. The latter also may change their image to 
            // display the last clicked menu item: if so, on click, they execute the last clicked menu item, if not, the show the popup menu.
            ToolStrip res = new ToolStrip();
            res.Name = name;
            for (int i = 0; i < menuIds.Length; i++)
            {
                int ImageIndex = MenuResource.FindImageIndex(menuIds[i]);
                if (MenuResource.IsPopup(menuIds[i])) // this is a popup menu id. On click, we show the popup menu (sub menu)
                {
                    ToolStripSplitButton btn = new ToolStripSplitButton();
                    TagInfo tagInfo = new TagInfo(menuIds[i], commandHandler, btn, true);
                    btn.Tag = tagInfo;
                    if (ImageIndex >= 0)
                    {
                        btn.DisplayStyle = ToolStripItemDisplayStyle.Image;
                        btn.ImageScaling = ToolStripItemImageScaling.None;
                        btn.Image = ButtonImages.ButtonImageList.Images[ImageIndex];
                    }
                    else
                    {
                        btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
                    }
                    btn.Name = StringTable.GetString(menuIds[i]); // not sure, what the name is used for
                    btn.Size = new System.Drawing.Size(24, 24);
                    btn.Text = StringTable.GetString(menuIds[i]);
                    btn.Click += tagInfo.ButtonClicked;
                    res.Items.Add(btn);
                }
                else
                {
                    ToolStripButton btn = new ToolStripButton();
                    TagInfo tagInfo = new TagInfo(menuIds[i], commandHandler, btn);
                    btn.Tag = tagInfo;
                    if (ImageIndex >= 0)
                    {
                        btn.DisplayStyle = ToolStripItemDisplayStyle.Image;
                        btn.ImageScaling = ToolStripItemImageScaling.None;
                        btn.Image = ButtonImages.ButtonImageList.Images[ImageIndex];
                    }
                    else
                    {
                        btn.DisplayStyle = ToolStripItemDisplayStyle.Text;
                    }
                    btn.Name = StringTable.GetString(menuIds[i]);
                    btn.Size = new System.Drawing.Size(24, 24);
                    btn.Text = StringTable.GetString(menuIds[i]); // will be shown as a tooltip
                    btn.Click += tagInfo.ButtonClicked;
                    res.Items.Add(btn);
                }
            }
            return res;
        }
        /// <summary>
        /// Use in OnIdle to update the state (checked, enabled) of all visible buttons
        /// </summary>
        /// <param name="toolStripPanel"></param>
        public static void UpdateCommandState(ToolStripPanel toolStripPanel)
        {
            foreach (Control control in toolStripPanel.Controls)
            {
                if (control is ToolStrip toolStrip)
                {
                    foreach (ToolStripItem item in toolStrip.Items)
                    {
                        if (item.Tag is TagInfo tagInfo)
                        {
                            tagInfo.UpdateCommandState();
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Save the positions of the toolbars in the application settings.
        /// </summary>
        /// <param name="topToolStripContainer"></param>
        public static void SaveToolbarPositions(ToolStripContainer topToolStripContainer)
        {   // very simple serialisation of the position of the controls
            StringBuilder saveToolbarPosition = new StringBuilder();
            foreach (Control control in topToolStripContainer.TopToolStripPanel.Controls)
            {
                if (control is ToolStrip toolStrip)
                {
                    saveToolbarPosition.Append(toolStrip.Name);
                    saveToolbarPosition.Append(":");
                    saveToolbarPosition.Append(toolStrip.Location.X.ToString());
                    saveToolbarPosition.Append(",");
                    saveToolbarPosition.Append(toolStrip.Location.Y.ToString());
                    saveToolbarPosition.Append(";");
                }
            }
            Properties.Settings.Default["ToolbarPositions"] = saveToolbarPosition.ToString();
            Properties.Settings.Default.Save(); // Saves settings in application configuration file
        }
        /// <summary>
        /// Create some standard toolbars and restore the positions from the application settings
        /// </summary>
        /// <param name="topToolStripContainer"></param>
        /// <param name="commandHandler"></param>
        public static void CreateOrRestoreToolbars(ToolStripContainer topToolStripContainer, ICommandHandler commandHandler)
        {
            string savedToolbarPosition = Properties.Settings.Default["ToolbarPositions"] as string;
            SortedDictionary<string, Point> toolBarPosition = new SortedDictionary<string, Point>();
            foreach (string position in savedToolbarPosition.Split(';'))
            {
                string[] parts = position.Split(':');
                if (parts.Length == 2)
                {
                    string[] xy = parts[1].Split(',');
                    if (xy.Length == 2)
                    {
                        try
                        {
                            toolBarPosition[parts[0]] = new Point(int.Parse(xy[0]), int.Parse(xy[1]));
                        }
                        catch { }
                    }
                }
            }
            if (toolBarPosition.Count == 5)
            {   // it is a valid dictionary
                // we have to sort the entries by location from top left to bottom right
                string[] keys = new string[toolBarPosition.Count];
                toolBarPosition.Keys.CopyTo(keys, 0);
                Array.Sort(keys, delegate (string s1, string s2)
                {
                    int c = toolBarPosition[s1].Y.CompareTo(toolBarPosition[s2].Y);
                    if (c == 0) c = toolBarPosition[s1].X.CompareTo(toolBarPosition[s2].X);
                    return c;
                });
                for (int i = 0; i < keys.Length; i++)
                {
                    if (toolBarPosition.TryGetValue(keys[i], out Point point)) topToolStripContainer.TopToolStripPanel.Join(ToolBars.MakeStandardToolStrip(keys[i], commandHandler), point);
                }
            }
            else
            {
                topToolStripContainer.TopToolStripPanel.Join(ToolBars.MakeStandardToolStrip("Object", commandHandler), 0);
                topToolStripContainer.TopToolStripPanel.Join(ToolBars.MakeStandardToolStrip("Zoom", commandHandler), 0);
                topToolStripContainer.TopToolStripPanel.Join(ToolBars.MakeStandardToolStrip("File", commandHandler), 0);
                topToolStripContainer.TopToolStripPanel.Join(ToolBars.MakeStandardToolStrip("Snap", commandHandler), 1);
                topToolStripContainer.TopToolStripPanel.Join(ToolBars.MakeStandardToolStrip("Construct", commandHandler), 1);
            }
        }
    }
}
