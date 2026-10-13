using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml;

namespace CADability.UserInterface
{

    /// <summary>
    /// A simple structure describing a menu or an menu item (without the use of Windows.Forms).
    /// If <see cref="ID"/> is null, this is a main menu, if <see cref="SubMenus"/> is null, it is a simple menu item.
    /// If <see cref="SubMenus"/> is not null, this structure contains a list of sub-menus.
    /// This structure must be converted to a platform dependent menu and displayed accordingly.
    /// The selection of the menu item is handled by <see cref="Target"/>, but it can also be handled directly by setting <see cref="OnCommand"/> 
    /// ( and <see cref="OnUpdateCommand"/> and  <see cref="OnSelected"/>).
    /// </summary>
    public class MenuWithHandler: ICommandHandler
    {
        Func<string, bool> onCommand;
        Func<string, CommandState, bool> onUpdateCommand;
        Action<MenuWithHandler, bool> onSelected;
        /// <summary>
        /// Sets a handler that processes the command when this menu item is clicked. It receives the menu id and returns
        /// true if it handled the command. Setting it makes this item its own <see cref="Target"/>; a handler set here
        /// takes precedence over the target. Write-only.
        /// </summary>
        public Func<string, bool> OnCommand
        {
            set
            {
                Target = this;
                onCommand = value;
            }
        }
        /// <summary>
        /// Sets a handler that updates the <see cref="CommandState"/> (enabled, checked) of this menu item before it is
        /// shown. It receives the menu id and the state to modify and returns true if it handled the request. Setting it
        /// makes this item its own <see cref="Target"/>. Write-only.
        /// </summary>
        public Func<string, CommandState, bool> OnUpdateCommand
        {
            set
            {
                Target = this;
                onUpdateCommand = value;
            }
        }
        /// <summary>
        /// Sets a handler that is notified when this menu item is highlighted (second argument true) or loses the
        /// highlight (false), e.g. to show a preview in the view. Setting it makes this item its own <see cref="Target"/>.
        /// Write-only.
        /// </summary>
        public Action<MenuWithHandler, bool> OnSelected
        {
            set
            {
                Target = this;
                onSelected = value;
            }
        }

        /// <summary>
        /// Creates an empty menu item without id, text and image. Set <see cref="ID"/>, <see cref="Text"/> and
        /// <see cref="Target"/> (or the <c>OnXxx</c> handlers) before use.
        /// </summary>
        public MenuWithHandler()
        {
            ImageIndex = -1;
        }
        /// <summary>
        /// creates a new menu item with the provided <paramref name="menuId"/>. If <paramref name="resourceId"/> is null, the menuId is used as the resourceId.
        /// The resourceId specifies the menu text from the string table.
        /// </summary>
        /// <param name="menuId">menu id which is passed on to the <see cref="ICommandHandler.OnCommand(string)"/> call</param>
        /// <param name="resourceId">the id of the menu text from the string table</param>
        public MenuWithHandler(string menuId, string resourceId=null)
        {
            ImageIndex = -1;
            ID = menuId;
            if (resourceId == null) Text = StringTable.GetString(menuId, StringTable.Category.label);
            else Text = StringTable.GetString(resourceId, StringTable.Category.label);
        }
        /// <summary>
        /// The id of the menu item, e.g. "MenuId.Edit.Cut". It is passed to <see cref="ICommandHandler.OnCommand"/> when
        /// the item is clicked. Null for a main menu, "SEPARATOR" for a separator.
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// The text displayed for the item, usually looked up in the <see cref="StringTable"/> by the id. "-" for a separator.
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// Index of the item's image in the application's button image list, or -1 (the default) for no image. Loaded
        /// from the "IconNr" attribute of the menu resource.
        /// </summary>
        public int ImageIndex { get; set; }
        /// <summary>
        /// True when the menu resource marks the item with the flag "NotSticky", meant for toolbar buttons that must not
        /// stay pressed. CADability itself does not evaluate it.
        /// </summary>
        public bool NotSticky { get; set; }
        /// <summary>
        /// The "Position" attribute of the item in the menu resource, meant for ordering when menus are merged; 0 if not
        /// given. CADability itself does not evaluate it.
        /// </summary>
        public int Position { get; set; }
        /// <summary>
        /// The <see cref="ICommandHandler"/> that receives the click on this item (<see cref="ICommandHandler.OnCommand"/>),
        /// the request for its state (<see cref="ICommandHandler.OnUpdateCommand"/>) and the highlight notification
        /// (<see cref="ICommandHandler.OnSelected"/>). Menus loaded with
        /// <see cref="MenuResource.LoadMenuDefinition(string, bool, ICommandHandler)"/> get the handler passed there.
        /// </summary>
        public ICommandHandler Target { get; set; }
        /// <summary>
        /// The items of the sub-menu that opens from this item, or null when the item is a simple command.
        /// </summary>
        public MenuWithHandler[] SubMenus { get; set; }
        /// <summary>
        /// True when the menu resource marks the item with MdiList="true" (a Windows.Forms concept for listing the open
        /// MDI windows). Not used by CADability.
        /// </summary>
        public bool MdiList { get; internal set; }
        /// <summary>
        /// The keyboard shortcut of the item as given by the "Shortcut" attribute of the menu resource, as the name of a
        /// value of the <see cref="CADability.Substitutes.Shortcut"/> enumeration (e.g. "CtrlC"), or null. The platform
        /// specific menu converts it to a key combination.
        /// </summary>
        public string Shortcut { get; internal set; }
        /// <summary>
        /// True when the shortcut is to be displayed next to the menu text; set together with <see cref="Shortcut"/>.
        /// </summary>
        public bool ShowShortcut { get; internal set; }
        /// <summary>
        /// A new menu item that represents a separator line (id "SEPARATOR", text "-"). Each access returns a new instance.
        /// </summary>
        public static MenuWithHandler Separator
        {
            get
            {
                MenuWithHandler res = new MenuWithHandler();
                res.ID = "SEPARATOR";
                res.Text = "-";
                return res;
            }
        }

        bool ICommandHandler.OnCommand(string MenuId)
        {
            if (onCommand != null) return onCommand(MenuId);
            else if (Target != this) return Target.OnCommand(MenuId);
            else return false;
        }

        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            if (onUpdateCommand != null) return onUpdateCommand(MenuId, CommandState);
            else if (Target != this) return Target.OnUpdateCommand(MenuId, CommandState);
            else return false;
        }

        void ICommandHandler.OnSelected(MenuWithHandler selectedMenu, bool selected)
        {
            if (onSelected != null) onSelected(selectedMenu, selected);
            else if (Target!=this) Target.OnSelected(selectedMenu, selected);
        }
    }

    /// <summary>
    /// The MenuResource class provides methods to load and manipulate a menu resource that is used by CADability
    /// and its applications to compose the main menu, popup and context menues.
    /// </summary>

    public class MenuResource
    {
        static private XmlDocument mMenuDocument;
        static MenuResource()
        {
            Assembly ThisAssembly = Assembly.GetExecutingAssembly();
            System.IO.Stream str = ThisAssembly.GetManifestResourceStream("CADability.MenuResource.xml");
            mMenuDocument = new XmlDocument();
            mMenuDocument.Load(str);
        }
        /// <summary>
        /// Replaces the standard menu resource by the provided XML document
        /// </summary>
        /// <param name="XmlStream">The document opend as a stream</param>
        static public void SetMenuResource(System.IO.Stream XmlStream)
        {
            mMenuDocument = new XmlDocument();
            mMenuDocument.Load(XmlStream);
        }
        /// <summary>
        /// Replaces the standard menu resource by the provided XML document
        /// </summary>
        /// <param name="XmlMenuDocument">The document</param>
        static public void SetMenuResource(XmlDocument XmlMenuDocument)
        {
            mMenuDocument = XmlMenuDocument;
        }
        /// <summary>
        /// Adds all subnodes of "CustomMenus" to the standard menu resource (e.g. additional context menus).
        /// </summary>
        /// <param name="XmlStream">Stream containing the XML document</param>
        static public void AddMenuResource(System.IO.Stream XmlStream)
        {
            XmlDocument ToAdd = new XmlDocument();
            ToAdd.Load(XmlStream);

            XmlNode toImport = ToAdd.SelectSingleNode("CustomMenus");

            XmlNode menus = mMenuDocument.ImportNode(toImport, true);
            XmlNode root = mMenuDocument.SelectSingleNode("Menus");
            foreach (XmlNode n in menus)
                root.InsertAfter(n.Clone(), root.LastChild);
        }
        /*
                static private XmlNode RecursiveSearch(string  menuID, XmlNode root)
                {
                    XmlNode res = null;
                    XmlNodeList oldPopups = root.SelectNodes("Popup");
                    foreach( XmlNode o in oldPopups)
                    {
                        if( o.Attributes["MenuId"].Value == menuID)
                            return o;
                        XmlNodeList subPopups = o.SelectNodes("Popup");
                        foreach( XmlNode subs in subPopups)
                            if( (res =  RecursiveSearch(menuID, subs)) != null)
                                return res;
                    }
                    return res;
                }

                static public void ReplaceMenus(System.IO.Stream XmlStream)
                {
                    XmlDocument ToAdd = new XmlDocument();
                    ToAdd.Load(XmlStream);
                    XmlNode toImport  = ToAdd.SelectSingleNode("CustomMenus");
                    XmlNode menus = mMenuDocument.ImportNode( toImport,true);
                    XmlNodeList newPopups = menus.SelectNodes("Popup");
                    XmlNode root = mMenuDocument.SelectSingleNode("Menus");
                    XmlNodeList oldPopups = menus.SelectNodes("Popup");
                    XmlNode main = mMenuDocument.SelectSingleNode("MainMenu");
                    foreach( XmlNode n in newPopups)
                    {
                        XmlNode found =RecursiveSearch(n.Attributes["MenuId"].Value,root);
                        if( found == null)
                            found =RecursiveSearch(n.Attributes["MenuId"].Value,main);
                        if( found != null)
                        {
                            found.ParentNode.InsertBefore(n, found);
                            found.ParentNode.RemoveChild(found);
                        }
                    }
                }
        */
        /// <summary>
        /// replaces all popup menus contained in subnodes of "CustomMenus" of the provided document.
        /// </summary>
        /// <param name="XmlStream">Stream containing the XML document</param>
        static public void ReplaceMenus(System.IO.Stream XmlStream)
        {
            XmlDocument ToAdd = new XmlDocument();
            ToAdd.Load(XmlStream);
            XmlNode toImport = ToAdd.SelectSingleNode("CustomMenus");
            XmlNodeList newPopups = toImport.SelectNodes("//Popup");
            XmlNode root = mMenuDocument.SelectSingleNode("Menus");
            foreach (XmlNode n in newPopups)
            {
                string MenuId = "//Popup[@MenuId =\"" + n.Attributes["MenuId"].Value + "\"]";
                XmlNode found = root.SelectSingleNode(MenuId);
                if (found != null)
                {
                    XmlNode menu = mMenuDocument.ImportNode(n, true);
                    found.ParentNode.InsertBefore(menu, found);
                    found.ParentNode.RemoveChild(found);
                }
            }
        }
        /// <summary>
        /// Creates a flat context menu from the provided menu ids. The text of each entry is looked up in the
        /// <see cref="StringTable"/> with the menu id as resource id, all entries use the provided command handler.
        /// </summary>
        /// <param name="menuIDs">Ids of the menu entries</param>
        /// <param name="Handler">Command handler which handles the menu commands</param>
        /// <returns>The menu entries</returns>
        static public MenuWithHandler[] CreateContextMenuWithHandler(string[] menuIDs, ICommandHandler Handler)
        {
            MenuWithHandler[] res = new MenuWithHandler[menuIDs.Length];
            for (int i = 0; i < menuIDs.Length; ++i)
            {
                MenuWithHandler mid = new MenuWithHandler();
                mid.ID = menuIDs[i];
                mid.Text = StringTable.GetString(menuIDs[i]);
                mid.Target = Handler;
                res[i] = mid;
            }
            return res;
        }
        /// <summary>
        /// Loads a menu from the menu resource with the provided name. For the unmodified standard resource
        /// the name of the main menu will be "SDI Menu"
        /// </summary>
        /// <param name="MenuName">Name of the menu to load: the "Name" attribute of a "MainMenu" node or the "MenuId" attribute of a "Popup" node</param>
        /// <param name="main">true to load a main menu, false to load a popup (context) menu</param>
        /// <param name="handler">Command handler which handles the menu commands</param>
        /// <returns>The menu entries</returns>
        static public MenuWithHandler[] LoadMenuDefinition(string MenuName, bool main, ICommandHandler handler)
        {
            List<MenuWithHandler> result = new List<MenuWithHandler>();
            ICommandHandler Handler = handler;
            XmlNode MenuNode;
            if (main) MenuNode = mMenuDocument.SelectSingleNode("Menus/MainMenu[@Name='" + MenuName + "']");
            else MenuNode = mMenuDocument.SelectSingleNode("Menus//Popup[@MenuId='" + MenuName + "']");
            for (XmlNode nd = MenuNode.FirstChild; nd != null; nd = nd.NextSibling)
            {
                LoadMenu(nd, result, Handler);
            }
            return result.ToArray(); ;
        }
        static internal string[] AllContextMenus()
        {   // liefert alle popup menues auf unterster Ebene, die mit Shortcuts="true" gekennzeichnet sind
            List<string> res = new List<string>();
            XmlNodeList nl = mMenuDocument.SelectNodes("Menus/Popup[@Shortcuts='true']");
            foreach (XmlNode o in nl)
            {
                //XmlNode sca = o.Attributes.GetNamedItem("Shortcuts");
                //if (sca != null && sca.Value == "true")
                //{
                res.Add(o.Attributes["MenuId"].Value);
                //}
            }
            return res.ToArray();
        }
        /// <summary>
        /// Returns true if the provided name is a popup menu (context menu)
        /// </summary>
        /// <param name="MenuName"></param>
        /// <returns></returns>
        static public bool IsPopup(string MenuName)
        {
            XmlNode MenuNode = mMenuDocument.SelectSingleNode("Menus//Popup[@MenuId='" + MenuName + "']");
            return MenuNode != null;
        }
        /// <summary>
        /// Returns the ids of the menu items directly contained in the popup menu <paramref name="MenuName"/> of the menu
        /// resource, in the order of their definition. Nested popups are not included.
        /// </summary>
        /// <param name="MenuName">Id of the popup menu</param>
        /// <returns>The menu ids; an empty array if there is no such popup</returns>
        static public string[] GetPopupItems(string MenuName)
        {
            XmlNode MenuNode = mMenuDocument.SelectSingleNode("Menus//Popup[@MenuId='" + MenuName + "']");
            List<string> res = new List<string>();
            if (MenuNode != null)
            {
                for (XmlNode ndi = MenuNode.FirstChild; ndi != null; ndi = ndi.NextSibling)
                {
                    if (ndi.Name == "MenuItem")
                    {
                        string subMenuId = ndi.Attributes.GetNamedItem("MenuId").Value;
                        res.Add(subMenuId);
                    }
                }
            }
            return res.ToArray();
        }
        static private void LoadMenu(XmlNode nd, List<MenuWithHandler> MenuItems, ICommandHandler Handler)
        {
            if (nd.Name == "Popup")
            {
                MenuWithHandler newitem = new MenuWithHandler();
                newitem.ID = nd.Attributes.GetNamedItem("MenuId").Value;
#if (DEBUG)
#else
                if (newitem.ID == "MenuId.3D") return;
#endif
                XmlNode IconNr = nd.Attributes.GetNamedItem("IconNr");
                if (IconNr != null) newitem.ImageIndex = int.Parse(IconNr.Value);
                else newitem.ImageIndex = -1;

                XmlNode Position = nd.Attributes.GetNamedItem("Position");
                if (Position != null) newitem.Position = int.Parse(IconNr.Value);
                else newitem.Position = 0;

                XmlNode Flag = nd.Attributes.GetNamedItem("Flag");
                if (Flag != null)
                {
                    bool NotSticky = false;
                    string[] flags = Flag.Value.Split('|');
                    foreach (string flag in flags)
                    {
                        if (flag == "NotSticky") NotSticky = true;
                    }
                    if (NotSticky) newitem.NotSticky = true;
                }
                newitem.Text = StringTable.GetString(newitem.ID);
                newitem.Target = Handler;
                // to implement: 
                //XmlNode mt = nd.Attributes.GetNamedItem("MergeType");
                //if (mt != null)
                //{
                //    switch (mt.Value)
                //    {
                //        case "Add": newitem.MergeType = MenuMerge.Add; break;
                //        case "Remove": newitem.MergeType = MenuMerge.Remove; break;
                //        default: newitem.MergeType = MenuMerge.Add; break;
                //    }
                //}
                //XmlNode mo = nd.Attributes.GetNamedItem("MergeOrder");
                //if (mo != null)
                //{
                //    newitem.MergeOrder = int.Parse(mo.Value);
                //}
                // MdiList seems not to be used
                XmlNode ml = nd.Attributes.GetNamedItem("MdiList");
                if (ml != null)
                {
                    newitem.MdiList = ml.Value == "true";
                }
                MenuItems.Add(newitem);
                List<MenuWithHandler> items = new List<MenuWithHandler>();
                for (XmlNode ndi = nd.FirstChild; ndi != null; ndi = ndi.NextSibling)
                {
                    LoadMenu(ndi, items, Handler);
                }
                newitem.SubMenus = items.ToArray();
            }
            else if (nd.Name == "MenuItem")
            {
                string MenuId = nd.Attributes.GetNamedItem("MenuId").Value;
                MenuWithHandler newitem = new MenuWithHandler();
                newitem.ID = nd.Attributes.GetNamedItem("MenuId").Value;
                XmlNode IconNr = nd.Attributes.GetNamedItem("IconNr");
                if (IconNr != null)
                {
                    try
                    {
                        newitem.ImageIndex = int.Parse(IconNr.Value);
                    }
                    catch (FormatException)
                    {
                        newitem.ImageIndex = -1;
                    }
                    catch (OverflowException)
                    {
                        newitem.ImageIndex = -1;
                    }
                }
                else newitem.ImageIndex = -1;
                XmlNode Position = nd.Attributes.GetNamedItem("Position");
                if (Position != null)
                {
                    newitem.Position = int.Parse(Position.Value);
                }
                else
                {
                    newitem.Position = 0;
                }

                XmlNode Flag = nd.Attributes.GetNamedItem("Flag");
                if (Flag != null)
                {
                    bool NotSticky = false;
                    string[] flags = Flag.Value.Split('|');
                    foreach (string flag in flags)
                    {
                        if (flag == "NotSticky") NotSticky = true;
                    }
                    if (NotSticky) newitem.NotSticky = true;
                }

                XmlNode shortCut = nd.Attributes.GetNamedItem("Shortcut");
                if (shortCut != null)
                {
                    newitem.Shortcut = shortCut.Value;
                    newitem.ShowShortcut = true;
                }
                // save shortcuts as strings not as ShortCuts!
                //ShortCuts shortcuts = Settings.GlobalSettings.GetValue("ShortCut") as ShortCuts;
                //if (shortcuts != null)
                //{
                //    Shortcut sc;
                //    if (shortcuts.mapping.TryGetValue(newitem.ID, out sc))
                //    {
                //        if (sc != Shortcut.None)
                //        {
                //            newitem.Shortcut = sc;
                //        }
                //    }
                //}
                newitem.Target = Handler;
                if (MenuId == "SEPARATOR")
                {
                    newitem.Text = "-";
                }
                else
                {
                    if (MenuId.StartsWith("MenuId.File.Mru.File"))
                    {
                        string filenr = MenuId.Substring(20);
                        try
                        {
                            int n = int.Parse(filenr);
                            string[] files = MRUFiles.GetMRUFiles();
                            if (n <= files.Length && n > 0)
                            {
                                string[] parts = files[files.Length - n].Split(';');
                                if (parts.Length > 1)
                                    newitem.Text = parts[0];
                            }
                            else
                            {
                                newitem.Text = null; // soll nicht zugefügt werden
                            }
                        }
                        catch (FormatException) { newitem.Text = null; }
                        catch (OverflowException) { newitem.Text = null; }
                    }
                    else
                    {
                        newitem.Text = StringTable.GetString(newitem.ID);
                    }
                }
                XmlNode ml = nd.Attributes.GetNamedItem("MdiList");
                if (ml != null)
                {
                    newitem.MdiList = ml.Value == "true";
                }
                if (newitem.Text != null && newitem.Text.Length > 0) MenuItems.Add(newitem);
            }
        }
        /// <summary>
        /// Returns the image index (the "IconNr" attribute) of the menu item or popup menu with the given id, as used for
        /// the toolbar buttons.
        /// </summary>
        /// <param name="MenuID">Id of the menu item or popup menu</param>
        /// <returns>The image index, or -1 if there is no such item or it has no icon</returns>
        static public int FindImageIndex(string MenuID)
        {
            XmlNode MenuNode = mMenuDocument.SelectSingleNode("Menus//MenuItem[@MenuId='" + MenuID + "']");
            if (MenuNode == null)
            {
                MenuNode = mMenuDocument.SelectSingleNode("Menus//Popup[@MenuId='" + MenuID + "']");
            }
            if (MenuNode != null)
            {
                XmlNode IconNr = MenuNode.Attributes.GetNamedItem("IconNr");
                if (IconNr != null)
                {
                    int ind = int.Parse(IconNr.Value);
                    // ist der Index größer als 10000, dann ist es ein Image vom User.
                    // Diese sind ohne Lücke an die CONDOR Images drangehängt, so
                    // erfolg die Index Umrechnung:
                    //if (ind >= 10000) ind = ind - 10000 + ButtonImages.OffsetUserImages;
                    return ind;
                }
            }
            return -1;
        }

        internal static bool IsNotSticky(string MenuID)
        {
            XmlNode MenuNode = mMenuDocument.SelectSingleNode("Menus//MenuItem[@MenuId='" + MenuID + "']");
            if (MenuNode == null)
            {
                MenuNode = mMenuDocument.SelectSingleNode("Menus//Popup[@MenuId='" + MenuID + "']");
            }
            if (MenuNode != null)
            {
                XmlNode IconNr = MenuNode.Attributes.GetNamedItem("IconNr");
                XmlNode Flag = MenuNode.Attributes.GetNamedItem("Flag");
                if (Flag != null)
                {
                    string[] flags = Flag.Value.Split('|');
                    foreach (string flag in flags)
                    {
                        if (flag == "NotSticky") return true;
                    }
                }
            }
            return false;
        }

    }
}
