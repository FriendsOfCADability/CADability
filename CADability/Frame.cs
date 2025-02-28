using CADability.Actions;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
#else
using System.Drawing;
#endif
using System.IO;
using System.Reflection;

using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using Action = CADability.Actions.Action;
using CADability.Substitutes;
using CADability.Attribute;

namespace CADability
{

    /// <summary>
    /// This class provides access to the current active frame (IFrame) object.
    /// It is typically the one and only SingleDocumentFrame in the application but
    /// may be different in future, when there will be a MultiDocumenFrame.
    /// </summary>
    public class ActiveFrame
    {
        /// <summary>
        /// Gets the currently active frame (IFrame), may be null
        /// </summary>
        public static IFrame Frame;
    }

    /// <summary>
    /// Delegate definition for <see cref="IFrame.ProcessContextMenuEvent"/>, which is raised when a context menu is about to be executed.
    /// </summary>
    /// <param name="target">The commandhandler, to which this command is targeted</param>
    /// <param name="MenuId">The menuid of the command (see MenuResource.xml)</param>
    /// <param name="Processed">Set to true, if you have processed this command and no further action is required, leave unmodified if CADability should process this command</param>
    public delegate void ProcessContextMenuDelegate(ICommandHandler target, string MenuId, ref bool Processed);
    /// <summary>
    /// Delegate definition for <see cref="IFrame.UpdateContextMenuEvent"/>, which is raised, when a context menu is about to be displayed.
    /// </summary>
    /// <param name="target">The commandhandler, to which this command is targeted</param>
    /// <param name="MenuId">The menuid of the command (see MenuResource.xml)</param>
    /// <param name="CommandState">Yu can modify the Enabled and Checked properties of the CommandState</param>
    /// <param name="Processed">Set to true, when you have provided "Enabled" and "Checked" properties</param>
    public delegate void UpdateContextMenuDelegate(ICommandHandler target, string MenuId, CommandState CommandState, ref bool Processed);
    /// <summary>
    /// Delegate definition for <see cref="IFrame.ProcessCommandEvent"/>, which is raised when the user
    /// selects a menu command from the main menu.
    /// </summary>
    /// <param name="MenuId">The menuid of the command (see MenuResource.xml)</param>
    /// <param name="Processed">Set to true, if you have processed this command and no further action is required, leave unmodified if CADability should process this command</param>
    public delegate void ProcessCommandDelegate(string MenuId, ref bool Processed);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="MenuId"></param>
    /// <param name="CommandState"></param>
    /// <param name="Processed"></param>
    public delegate void UpdateCommandDelegate(string MenuId, CommandState CommandState, ref bool Processed);
    public delegate void UIEventHandler(string MenuId, bool Active);
    public delegate void ProjectClosedDelegate(Project theProject, IFrame theFrame);
    public delegate void ProjectOpenedDelegate(Project theProject, IFrame theFrame);
    public delegate void ViewsChangedDelegate(IFrame theFrame);
    public delegate void ControlCenterCreatedDelegate(IControlCenter controlCenter);
    public delegate void ActionStartedDelegate(Action action);
    public delegate void ActionTerminatedDelegate(Action action);


    public interface IFrame
    {
        IControlCenter ControlCenter { get; }
        IPropertyPage GetPropertyDisplay(string Name);
        IPropertyEntry ContextMenuSource { get; set; }
        IUIService UIService { get; }
        /// <summary>
        /// Returns the currently active view
        /// </summary>
        IView ActiveView { get; set; }
        /// <summary>
        /// Returns an array of all views currently open in the frame
        /// </summary>
        IView[] AllViews { get; }
        /// <summary>
        /// Update and execute menus with this command handler
        /// </summary>
        ICommandHandler CommandHandler { get; }
        /// <summary>
        /// Returns the project that is open in this frame
        /// </summary>
        Project Project { get; }
        ActionStack ActionStack { get; }
        /// <summary>
        /// Starts the provided action. The currently active action will either be aborted or
        /// pushed down on the action stack until the provided action terminates.
        /// </summary>
        /// <param name="Action"></param>
        void SetAction(Action Action);
        /// <summary>
        /// Removes the active action froom the action stack. Usually actions terminate themselves
        /// by calling <see cref="Action.RemoveThisAction"/>.
        /// </summary>
        void RemoveActiveAction();
        /// <summary>
        /// Returns the currently active action. Call <see cref="Action.GetID"/> to find out more about 
        /// that action.
        /// </summary>
        Action ActiveAction { get; }
        /// <summary>
        /// Returns the menu id of the currently running action
        /// </summary>
        string CurrentMenuId { get; }
        /// <summary>
        /// Gets or sets the current <see cref="SnapPointFinder.SnapModes"/>
        /// </summary>
        SnapPointFinder.SnapModes SnapMode { get; set; }
        /// <summary>
        /// Provide a event handler if you want to be notified on command routed through this frame or to
        /// execute command handlers for some routed commands-
        /// </summary>
        event ProcessCommandDelegate ProcessCommandEvent;
        /// <summary>
        /// Provide a event handler if you want to control the appearnce of commands in the menu or toolbar 
        /// (set the enabled and check flags).
        /// </summary>
        event UpdateCommandDelegate UpdateCommandEvent;
        /// <summary>
        /// Provide a event handler if you want to be notified when a project is beeing closed.
        /// </summary>
        event ProjectClosedDelegate ProjectClosedEvent;
        /// <summary>
        /// Provide a event handler if you want to be notified when a project is opened.
        /// </summary>
        event ProjectOpenedDelegate ProjectOpenedEvent;
        /// <summary>
        /// Provide a event handler if you want to be notified when new views are created or views are closed.
        /// </summary>
        event ViewsChangedDelegate ViewsChangedEvent;
        /// <summary>
        /// Provide a event handler if you want to be notified on contex menue commands routed through this frame or to
        /// execute command handlers for some routed commands-
        /// </summary>
        event ProcessContextMenuDelegate ProcessContextMenuEvent;
        /// <summary>
        /// Provide a event handler if you want to control the appearance of commands in a context the menu 
        /// (set the enabled and check flags).
        /// </summary>
        event UpdateContextMenuDelegate UpdateContextMenuEvent;
        void ProcessContextMenu(ICommandHandler target, string MenuId, ref bool Processed);
        void UpdateContextMenu(ICommandHandler target, string MenuId, CommandState CommandState, ref bool Processed);
        /// <summary>
        /// Brings the property page with the <paramref name="Name"/> to front (in the tab control)
        /// </summary>
        /// <param name="Name"></param>
        void ShowPropertyDisplay(string Name);
        /// <summary>
        /// Returtns the name of the currently selected property page of the control center
        /// </summary>
        /// <returns></returns>
        string GetActivePropertyDisplay();
        /// <summary>
        /// If the <see cref="ActiveView"/> is not a <see cref="ModelView"/> then the first ModelView of the current <see cref="Project"/> will be displayed
        /// </summary>
        void AssureModelView();
        /// <summary>
        /// Sets the focus to a given entry in a given tabpage of the controlcenter.
        /// The names of the standard tabpgaes are:
        /// "Action","Project","Global","View","Symbol". If resourceId is null
        /// only the specified tabpage is selected.
        /// </summary>
        /// <param name="tabPageName">Name of a tabpage</param>
        /// <param name="resourceId">resource id of an entry or null</param>
        /// <param name="openEntry">the subentries of this entry should be opened</param>
        /// <param name="popupCOntextMenu">the context menu of this entry should be displayed</param>
        bool SetControlCenterFocus(string tabPageName, string resourceId, bool openEntry, bool popupCOntextMenu);
        /// <summary>
        /// Returns the global settings
        /// </summary>
        Settings GlobalSettings { get; set; }
        /// <summary>
                                                    /// Gets the <see cref="Settings"/> for the provided <paramref name="Name"/>. First the <see cref="Project"/>s settings are 
                                                    /// checked, if it is not defined there, the global settings will be queried.
                                                    /// </summary>
                                                    /// <param name="Name"></param>
                                                    /// <returns></returns>
        object GetSetting(string Name);
        /// <summary>
        /// Gets the boolen setting for the <paramref name="Name"/>. If not found <paramref name="Default"/> will be returned.
        /// <seealso cref="GetSetting(string)"/>
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        bool GetBooleanSetting(string Name, bool Default);
        /// <summary>
        /// Gets the double setting for the <paramref name="Name"/>. If not found <paramref name="Default"/> will be returned.
        /// <seealso cref="GetSetting(string)"/>
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        double GetDoubleSetting(string Name, double Default);
        /// <summary>
        /// Gets the integer  setting for the <paramref name="Name"/>. If not found <paramref name="Default"/> will be returned.
        /// <seealso cref="GetSetting(string)"/>
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        int GetIntSetting(string Name, int Default);
        /// <summary>
        /// Gets the color setting for the <paramref name="Name"/>. If not found <paramref name="Default"/> will be returned.
        /// <seealso cref="GetSetting(string)"/>
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        Color GetColorSetting(string Name, Color Default);
        /// <summary>
        /// Gets the string setting for the <paramref name="Name"/>. If not found <paramref name="Default"/> will be returned.
        /// <seealso cref="GetSetting(string)"/>
        /// </summary>
        /// <param name="Name"></param>
        /// <param name="Default"></param>
        /// <returns></returns>
        string GetStringSetting(string Name, string Default);
        /// <summary>
        /// Register here to get notified when a setting has changed
        /// </summary>
        event SettingChangedDelegate SettingChangedEvent;
        /// <summary>
        /// When <see cref="Solid"/> objects get inserted into a model, this is how it will be handeled
        /// </summary>
        Make3D.SolidModellingMode SolidModellingMode { get; set; }
        /// <summary>
        /// Gets or sets the selected objects. There must be no other action running when this
        /// property is used. If there is another action running, nothing will be set and the get
        /// property returns null
        /// </summary>
        GeoObjectList SelectedObjects { get; set; }
        /// <summary>
        /// This event will raised when the ControlCenter has been created. You can use the method ControlCenter.HideEntry
        /// to suppress entries in the ControlCenter
        /// </summary>
        event ControlCenterCreatedDelegate ControlCenterCreatedEvent;
        /// <summary>
        /// Notification that a <see cref="Action"/> started
        /// </summary>
        event ActionStartedDelegate ActionStartedEvent;
        /// <summary>
        /// Notification that a <see cref="Action"/> terminated
        /// </summary>
        event ActionTerminatedDelegate ActionTerminatedEvent;
        void RaiseActionStartedEvent(Action Action);
        void RaiseActionTerminatedEvent(Action Action);
        void UpdateMRUMenu(string[] mruFiles);
    }

    /// <summary>
    /// A standard implementation of the IFrame interface, used by CadFrame and the old SingleDocumentFrame
    /// </summary>
    public abstract class FrameImpl : IFrame, ICommandHandler
    {
        private ActionStack actionStack; // the action stack
        private Project project; // the project in this frame
        // the different kind of views
        private Dictionary<string, ModelView> modelViews;
        private List<AnimatedView> animatedViews;
#if !WEBASSEMBLY
        private Dictionary<string, LayoutView> layoutViews;
        private List<GDI2DView> gdiViews;
#endif
        private IPropertyPage globalProperties; // the property page for the display of the global settings
        private IPropertyPage projectProperties; // the property page for the display of the project properties
        private IPropertyPage actionProperties; // the property page for the display of the action properties
        private IPropertyPage viewProperties; // the property page for the display of the views and their properties
        // private PropertyPage symbolProperties; // no more symbol library, nobody uses it
        private string[] mruFiles; // the list of most recently used files
        private Make3D.SolidModellingMode solidModellingMode = Make3D.SolidModellingMode.unchanged;
        public Make3D.SolidModellingMode SolidModellingMode { get { return solidModellingMode; } set { solidModellingMode = value; } }
        private static double mouseWheelZoomFactor
        {   // this mechanism delays reading the global settings
            get
            {
                return Settings.GlobalSettings.GetDoubleValue("MouseWheelZoomFactor", 1.1);
            }
        }
        private ICanvas canvas;
        public static IFrame MainFrame; // there is usually only one frame and sometimes we need services like the active view or the UIServices that we can get from here
        public FrameImpl()
        {
            MainFrame = this;
            actionStack = new ActionStack(this);
            modelViews = new Dictionary<string, ModelView>();
#if !WEBASSEMBLY
            layoutViews = new Dictionary<string, LayoutView>();
            gdiViews = new List<GDI2DView>();
#endif
            animatedViews = new List<AnimatedView>();
            mruFiles = MRUFiles.GetMRUFiles();
            Settings settings = Settings.GlobalSettings; // to initiate load
            Settings.GlobalSettings.SetValue("Experimental.ParallelOcttree", true);
            snapMode = (SnapPointFinder.SnapModes)Settings.GlobalSettings.GetIntValue("KeepState.SnapMode", 0);
        }
        public FrameImpl(IControlCenter cc, ICanvas canvas) : this()
        {
            cc.Frame = this;
            ControlCenter = cc;
            this.canvas = canvas;
        }
        public FrameImpl(Project pr, ModelView modelView, IControlCenter cc) : this()
        {
            ControlCenter = cc;
            Project = pr;
            ActiveView = modelView;
        }
        public FrameImpl(ICanvas canvas) : this()
        {
            this.canvas = canvas;
        }

        /// <summary>
        /// Returns the <see cref="ICommandHandler">CommandHandler</see> of this frame. All menu and toolbar commands
        /// are routed through this command handler. If the tollbars and menus of CADability are used no action has to
        /// be taken. If you use your own menues or toolbars call <see cref="ICommandHandler.OnCommand"/> and
        /// <see cref="ICommandHandler.OnUpdateCommand"/> for all commands concerning CADability.
        /// </summary>
        public ICommandHandler CommandHandler { get { return this; } }
        /// <summary>
        /// Gets or sets the project handles in this farme
        /// </summary>
        public Project Project
        {
            get { return project; }
            set
            {
                if (project == value) return;
                if (actionStack.ActiveAction != null)
                {   // diese Zeilen braucht es damit das ControlCenter nicht aus dem tritt kommt
                    // wenn beim Beenden eine Aktion offen stehen bleibt gibt es sonst Probleme.
                    // z.B. Trimmen, dann Projekt wechseln, dann großen Pfad erzeugen
                    //while (!(actionStack.ActiveAction is SelectObjectsAction))
                    //{
                    //    actionStack.RemoveActiveAction();
                    //}
                    while (actionStack.ActiveAction != null)
                    {
                        actionStack.RemoveActiveAction();
                    }
                }
                ProjectClosedEvent?.Invoke(project, this);
                if (project != null)
                {
                    project.ViewChangedEvent -= new CADability.Project.ViewChangedDelegate(OnProjectViewChanged);
                    project.RefreshEvent -= new CADability.Project.RefreshDelegate(project_RefreshEvent);
                }
                project = value;
                modelViews.Clear();
#if !WEBASSEMBLY
                layoutViews.Clear();
                gdiViews.Clear();
#endif
                animatedViews.Clear();
                ProjectOpenedEvent?.Invoke(project, this);
                ConstructAction.ClearLastStyles(); // müsste eigentlich über einen statischen event laufen

                project.RefreshEvent += new CADability.Project.RefreshDelegate(project_RefreshEvent);
                project.ViewChangedEvent += new CADability.Project.ViewChangedDelegate(OnProjectViewChanged);

                SetProjectProperties();
                FileNameChangedEvent?.Invoke(project.FileName);
                UpdateViewMenu(true);
                // OnSplit(ESplitMode.single); no view splitting in this implementation
                foreach (IView view in AllViews)
                {
                    view.Connect(canvas);
                }
                IView active = FindView(project.ActiveViewName);
                if (active != null) ActiveView = active;
                else ActiveView = FirstModelView;
                // canvas.ShowView(ActiveView); // setting the ActiveView already calls canvas.ShowView
                // die Views in dem View Tab anzeigen
                SetViewProperties();
                if (actionStack.ActiveAction == null)
                {
                    actionStack.SetAction(new SelectObjectsAction(this));
                }
                actionStack.OnViewsChanged();
                ViewsChangedEvent?.Invoke(this);
            }
        }
        public abstract IUIService UIService { get; }
        public IView[] AllViews
        {
            get
            {
                List<IView> res = new List<IView>();
                foreach (KeyValuePair<string, ModelView> item in modelViews)
                {
                    res.Add(item.Value);
                }
#if !WEBASSEMBLY
                foreach (KeyValuePair<string, LayoutView> item in layoutViews)
                {
                    res.Add(item.Value);
                }
                res.AddRange(gdiViews);
#endif
                res.AddRange(animatedViews);
                return res.ToArray();
            }
        }
        private IView activeView;
        public IView ActiveView
        {
            get
            {
                return activeView;
            }
            set
            {
                if (activeView != value)
                {
                    activeView = value;
                    canvas.ShowView(activeView);
                    SetViewProperties();
                }
            }
        }
        public void AddView(IView toAdd)
        {
            if (toAdd is ModelView)
            {
                ModelView mv = toAdd as ModelView;
                project.AddProjectedModel(mv.Name, mv.Model, mv.Projection);
                modelViews.Add(mv.Name, mv);
                SetViewProperties();
            }
            if (toAdd is AnimatedView)
            {
                project.AnimatedViews.Add(toAdd as AnimatedView);
                animatedViews.Add(toAdd as AnimatedView);
                SetViewProperties();
            }
#if !WEBASSEMBLY

            if (toAdd is GDI2DView)
            {
                project.GdiViews.Add(toAdd as GDI2DView);
                gdiViews.Add(toAdd as GDI2DView);
                SetViewProperties();
            }
            if (toAdd is LayoutView)
            {
                LayoutView lv = toAdd as LayoutView;
                project.AddLayout(lv.Layout);
                layoutViews.Add(lv.Layout.Name, lv);
                SetViewProperties();
            }
#endif
        }
        public void RemoveView(IView toRemove)
        {
            if (toRemove is AnimatedView)
            {
                project.AnimatedViews.Remove(toRemove as AnimatedView);
                animatedViews.Remove(toRemove as AnimatedView);
                SetViewProperties();
            }
            else if (toRemove is ModelView)
            {
                project.RemoveModelView(toRemove as ModelView);
                modelViews.Remove((toRemove as ModelView).Name);
                SetViewProperties();
            }
#if !WEBASSEMBLY
            else if (toRemove is GDI2DView)
            {
                project.GdiViews.Remove(toRemove as GDI2DView);
                gdiViews.Remove(toRemove as GDI2DView);
                SetViewProperties();
            }
            else if (toRemove is LayoutView)
            {
                LayoutView lv = toRemove as LayoutView;
                project.RemoveLayout(lv.Layout);
                layoutViews.Remove(lv.Layout.Name);
                SetViewProperties();
            }
#endif
        }


        //public Control DisplayArea => throw new NotImplementedException();

        private IControlCenter controlCenter;
        public IControlCenter ControlCenter
        {
            get { return controlCenter; }
            set
            {
                if (controlCenter == value) return;
                controlCenter = value;
                if (ControlCenterCreatedEvent != null) ControlCenterCreatedEvent(controlCenter);

                //ImageList imageList = new ImageList();
                //imageList.ImageSize = new Size(16, 16);

                //System.Drawing.Bitmap bmp = BitmapTable.GetBitmap("Icons.bmp");
                //Color clr = bmp.GetPixel(0, 0);
                //if (clr.A != 0) bmp.MakeTransparent(clr);
                //imageList.Images.AddStrip(bmp);
                //ControlCenter.ImageList = imageList;

                // folgende Seiten werden in das ControlCenter eingefügt

                // 1. die Seite für die Aktionen
                //actionProperties = new ShowProperty(this) as IPropertyPage;
                //actionProperties.resourceIdInfo = "ActionTabPage";
                //TabPage actionTabPage = new TabPage("Action");
                //actionTabPage.ImageIndex = 1;
                //actionTabPage.Controls.Add(actionProperties);
                //controlCenter.AddTabPage(actionTabPage);
                //actionProperties.Dock = System.Windows.Forms.DockStyle.Fill;
                actionProperties = ControlCenter.AddPropertyPage("Action", 1);

                // 2. die Seite für die Einstellungen der Zeiochnung
                //projectProperties = new CADability.UserInterface.ShowProperty(this);
                //projectProperties.resourceIdInfo = "ProjectTabPage";
                //TabPage projectTabPage = new TabPage("Project");
                //projectTabPage.ImageIndex = 2;
                //projectTabPage.Controls.Add(projectProperties);
                //controlCenter.AddTabPage(projectTabPage);
                //projectProperties.Dock = System.Windows.Forms.DockStyle.Fill;
                projectProperties = ControlCenter.AddPropertyPage("Project", 2);

                // 3. die Seite für die globalen Einstellungen
                //globalProperties = new CADability.UserInterface.ShowProperty(this);
                //globalProperties.resourceIdInfo = "GlobalTabPage";
                //TabPage GlobalTabPage = new TabPage("Global");
                //GlobalTabPage.ImageIndex = 0;
                //GlobalTabPage.Controls.Add(globalProperties);
                //controlCenter.AddTabPage(GlobalTabPage);
                //globalProperties.Dock = System.Windows.Forms.DockStyle.Fill;
                //globalProperties.AddShowProperty(Settings.GlobalSettings, true);
                globalProperties = ControlCenter.AddPropertyPage("Global", 0);
                globalProperties.Add(Settings.GlobalSettings, true);
                // 4. die Seite für die Eigenschaften der Ansicht

                //viewProperties = new ShowProperty(this);
                //viewProperties.resourceIdInfo = "ViewTabPage";
                //TabPage ViewTabPage = new TabPage("View");
                //ViewTabPage.ImageIndex = 3;
                //ViewTabPage.Controls.Add(viewProperties);
                //controlCenter.AddTabPage(ViewTabPage);
                //viewProperties.Dock = System.Windows.Forms.DockStyle.Fill;
                viewProperties = ControlCenter.AddPropertyPage("View", 3);
                // 5. die Seite für die Eigenschaften der Symbolbibliothek

                //symbolProperties = new ShowProperty(this);
                //symbolProperties.resourceIdInfo = "SymbolTabPage";
                //symbolMainProperty = new SymbolMainProperty(symbolProperties);
                //symbolProperties.AddShowProperty(symbolMainProperty, true);
                //TabPage SymbolTabPage = new TabPage("Symbol");
                //SymbolTabPage.ImageIndex = 4;
                //SymbolTabPage.Controls.Add(symbolProperties);
                //controlCenter.AddTabPage(SymbolTabPage);
                //symbolProperties.Dock = System.Windows.Forms.DockStyle.Fill;

                SetViewProperties();

                actionProperties.BringToFront();
                if (ActiveAction is SelectObjectsAction)
                {
                    ActiveAction.OnInactivate(null, false);
                    ActiveAction.OnActivate(null, false);
                }

                if (project != null)
                {
                    SetProjectProperties(); // die sind u.U. nicht gesetzt
                }
            }
        }

        /// <summary>
        /// Gets or set the GlobalSettings object (<see cref="Settings"/>). The global settings
        /// are saved in the file CADability.GlobalSettings.bin. If you set a different settings object
        /// you are responsible for saving the content.
        /// </summary>
        public Settings GlobalSettings
        {
            get { return Settings.GlobalSettings; }
            set
            {
                Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
                Settings.GlobalSettings = value;
                Settings.AddMissingSettings();
                Settings.GlobalSettings.SettingChangedEvent += new SettingChangedDelegate(OnSettingChanged);
                IAttributeListContainer alc = Settings.GlobalSettings as IAttributeListContainer;
                if (alc != null)
                {
                    if (alc.ColorList != null) alc.ColorList.menuResourceId = "MenuId.Settings.ColorList";
                    if (alc.LayerList != null) alc.LayerList.menuResourceId = "MenuId.Settings.LayerList";
                    if (alc.LineWidthList != null) alc.LineWidthList.menuResourceId = "MenuId.Settings.LineWidthList";
                    if (alc.LinePatternList != null) alc.LinePatternList.menuResourceId = "MenuId.Settings.LinePatternList";
                    if (alc.HatchStyleList != null) alc.HatchStyleList.menuResourceId = "MenuId.Settings.HatchStyleList";
                    if (alc.DimensionStyleList != null) alc.DimensionStyleList.menuResourceId = "MenuId.Settings.DimStyleList";
                    if (alc.StyleList != null) alc.StyleList.menuResourceId = "MenuId.Settings.StyleList";
                }

                globalProperties.Clear();
                globalProperties.Add(Settings.GlobalSettings, true);
                globalProperties.Refresh(Settings.GlobalSettings);
            }
        }
        private void OnSettingChanged(string Name, object NewValue)
        {	
            object o = this.GetSetting(Name);
            SettingChangedEvent?.Invoke(Name, NewValue);
            // }
            if (Name == "Ruler.Show" && NewValue != null)
            {
                // this.condorScrollableCtrls[activeControl].ShowRuler = ((int)NewValue != 0);
            }
        }


        public string CurrentMenuId { get; set; }

        public GeoObjectList SelectedObjects
        {
            get
            {
                if (actionStack.ActiveAction is SelectObjectsAction)
                {
                    return (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                }
                return null;
            }
            set
            {
                if (actionStack.ActiveAction is SelectObjectsAction)
                {
                    (actionStack.ActiveAction as SelectObjectsAction).SetSelectedObjects(value, false);
                }
                else
                {
                    SelectObjectsAction soa = actionStack.FindAction(typeof(SelectObjectsAction)) as SelectObjectsAction;
                    if (soa != null) soa.SetSelectedObjects(value, false);
                }
            }
        }


        public bool DesignMode => throw new NotImplementedException();

        IPropertyEntry IFrame.ContextMenuSource { get; set; }

        public event ProcessCommandDelegate ProcessCommandEvent;
        public event UpdateCommandDelegate UpdateCommandEvent;
        public event ProjectClosedDelegate ProjectClosedEvent;
        public event ProjectOpenedDelegate ProjectOpenedEvent;
        public event ViewsChangedDelegate ViewsChangedEvent;
        public event ProcessContextMenuDelegate ProcessContextMenuEvent;
        public event UpdateContextMenuDelegate UpdateContextMenuEvent;
        public event SettingChangedDelegate SettingChangedEvent;
        public event ControlCenterCreatedDelegate ControlCenterCreatedEvent;
        public event ActionStartedDelegate ActionStartedEvent;
        public event ActionTerminatedDelegate ActionTerminatedEvent;

        public void AssureModelView()
        {
            throw new NotImplementedException();
        }

        public void FocusChanged(IPropertyTreeView inThisView, IShowProperty thisItem, bool gotFocus)
        {
            throw new NotImplementedException();
        }

        public string GetActivePropertyDisplay()
        {
            return "Action";
        }

        public bool GetBooleanSetting(string Name, bool Default)
        {
            object s = GetSetting(Name);
            if (s != null)
            {
                return Settings.GetBoolValue(s);
            }
            return Default;
        }
        public double GetDoubleSetting(string Name, double Default)
        {
            object s = GetSetting(Name);
            if (s != null)
            {
                try
                {
                    return Settings.GetDoubleValue(s);
                }
                catch (InvalidCastException)
                {
                    return Default;
                }
            }
            return Default;
        }
        public int GetIntSetting(string Name, int Default)
        {
            object s = GetSetting(Name);
            if (s != null)
            {
                try
                {
                    return Settings.GetIntValue(s);
                }
                catch (InvalidCastException)
                {
                    return Default;
                }
            }
            return Default;
        }
        public Color GetColorSetting(string Name, Color Default)
        {
            object s = GetSetting(Name);
            if (s != null)
            {
                if (s.GetType() == typeof(Color)) return (Color)s;
                if (s is ColorSetting) return (s as ColorSetting).Color;
            }
            return Default;
        }
        public string GetStringSetting(string Name, string Default)
        {
            object s = GetSetting(Name);
            if (s != null)
            {
                if (s.GetType() == typeof(string)) return (string)s;
            }
            return Default;
        }

        public object GetSetting(string Name)
        {   // is there anything else but GlobalSettings? There was the idea about local settings, but I think it is never used.
            return Settings.GlobalSettings.GetValue(Name);
        }

        public bool PreProcessMouseWheel(MouseEventArgs e)
        {
            throw new NotImplementedException();
        }
        public virtual void PreProcessKeyDown(KeyEventArgs e)
        {
            // it is difficult to find an order of processing: 
            // the construct-action processes the enter key, no chance to use it in a list-box for the selection
            // the property page processes the tab key, no chance to use it in the action for "next modal input field"
            // we need to replace the KeyEventArgs class by a CADability class anyhow, we could introduce a first-pass, second-pass member
            // to solve this problem
            if (e.Handled) return;
            Action a = ActiveAction;
            if (a == null) return;
            // System.Diagnostics.Trace.WriteLine(e.KeyData.ToString() + "---" + e.KeyCode.ToString());
            switch (e.KeyData & (Keys)0x0FF) // was KeyCode, but didn't work
            {
                case Keys.Escape:
                    e.Handled = a.OnEscape(e.KeyData);
                    break;
                case Keys.Delete:
                    e.Handled = a.OnDelete(e.KeyData);
                    break;
                case Keys.Enter:
                    e.Handled = a.OnEnter(e.KeyData);
                    break;
                case Keys.Tab:
                    if (!e.Control) e.Handled = a.OnTab(e.KeyData); // Ctrl+Tab is used for switching between property pages
                    break;
            }
            if (!e.Handled) ControlCenter.PreProcessKeyDown(e);
        }
        public virtual void UpdateMRUMenu(string[] mruFiles)
        {
        }

        public void Dispose()
        {
            while (ActiveAction != null)
            {
                RemoveActiveAction();
            }
            Settings.GlobalSettings.SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
            Settings.SaveGlobalSettings();
            Settings.GlobalSettings.Dispose(); // it will be reloaded when access is needed
            if (project != null)
            {
                project.ViewChangedEvent -= new CADability.Project.ViewChangedDelegate(OnProjectViewChanged);
                project.RefreshEvent -= new CADability.Project.RefreshDelegate(project_RefreshEvent);
            }
            if (MainFrame == this) MainFrame = null;
        }
        public ActionStack ActionStack => actionStack;
        /// <summary>
        /// Implements <see cref="IFrame.SetAction"/>.
        /// </summary>
        /// <param name="Action"></param>
        public void SetAction(Action Action)
        {
            actionStack.SetAction(Action);
        }
        /// <summary>
        /// Implements <see cref="IFrame.RemoveActiveAction"/>.
        /// </summary>
        public void RemoveActiveAction()
        {
            actionStack.RemoveActiveAction();
        }
        /// <summary>
        /// Implements <see cref="IFrame.ActiveAction"/>.
        /// </summary>
        public Action ActiveAction
        {
            get { return actionStack.ActiveAction; }
        }
        void IFrame.ProcessContextMenu(ICommandHandler target, string MenuId, ref bool Processed)
        {
            ProcessContextMenuEvent?.Invoke(target, MenuId, ref Processed);
        }

        void IFrame.UpdateContextMenu(ICommandHandler target, string MenuId, CommandState CommandState, ref bool Processed)
        {
            UpdateContextMenuEvent?.Invoke(target, MenuId, CommandState, ref Processed);
        }
        void IFrame.RaiseActionStartedEvent(Action action)
        {
            ActionStartedEvent?.Invoke(action);
        }

        void IFrame.RaiseActionTerminatedEvent(Action action)
        {
            ActionTerminatedEvent?.Invoke(action);
        }

        public bool SetControlCenterFocus(string tabPageName, string resourceId, bool openEntry, bool popupCOntextMenu)
        {
            if (controlCenter != null)
            {
                bool ok = controlCenter.ShowPropertyPage(tabPageName);
                if (ok && resourceId != null)
                {
                    //foreach (Control ctrl in tp.Controls)
                    //{
                    //    if (ctrl is IPropertyTreeView)
                    //    {
                    //        IPropertyTreeView tv = ctrl as IPropertyTreeView;
                    //        IShowProperty isp = tv.FindFromHelpLink(resourceId);
                    //        if (isp != null)
                    //        {
                    //            tv.SelectEntry(isp);
                    //            if (openEntry)
                    //            {
                    //                tv.OpenSubEntries(isp, true);
                    //            }
                    //            if (popupCOntextMenu)
                    //            {
                    //                tv.PopupContextMenu(isp);
                    //            }
                    //            return true;
                    //        }
                    //    }
                    //}
                }
                else
                {

                }
            }
            return false;
        }

        public void ShowPropertyDisplay(string Name)
        {
            ControlCenter?.ShowPropertyPage(Name);
        }

        IPropertyPage IFrame.GetPropertyDisplay(string Name)
        {
            return ControlCenter?.GetPropertyPage(Name);
        }

#region handling menu commands
        public virtual bool OnCommand(string MenuId)
        {
            CurrentMenuId = MenuId; // ist das hier die richtige Stelle?
            bool Processed = false;
            ProcessCommandEvent?.Invoke(MenuId, ref Processed);
            if (Processed) return true;
            if (MenuId.StartsWith("MenuId.File.Mru.File"))
            {
                // menu: most recently used file n
                // kopiert aus file open, sollte zusammengefasst werden
                if (project != null)
                {
                    if (!project.SaveModified()) return true; // "Abbrechen" wurde gedrückt
                }
                string filename = null;
                string filenr = MenuId.Substring("MenuId.File.Mru.File".Length);
                try
                {
                    int n = int.Parse(filenr);
                    if (n <= mruFiles.Length && n > 0)
                    {
                        filename = mruFiles[mruFiles.Length - n];
                    }
                    else
                    {
                        filename = null; // soll nicht zugefügt werden
                    }
                }
                catch (FormatException) { filename = null; }
                catch (OverflowException) { filename = null; }
                OnFileOpen(filename);
                return true;
            }
            // TODO: show appropriate view
            if (MenuId.StartsWith("MenuId.View.Layout.LayoutView."))
            {   // Layout View anzeigen
#if !WEBASSEMBLY
                string layoutName = MenuId.Substring("MenuId.View.Layout.LayoutView.".Length);
                foreach (LayoutView lv in layoutViews.Values)
                {
                    if (lv.Layout.Name == layoutName)
                    {
                        //condorScrollableCtrls[activeControl].SetView(lv);
                        //lv.Initialize(condorScrollableCtrls[activeControl].CondorCtrl);
                        //if (!lv.IsInitialized)
                        //{
                        //    lv.ZoomTotal(1.1);
                        //}
                        //InvalidateAll();
                        //controlCenter.ShowTabPage(controlCenter.FindTabPage(viewProperties));
                        //viewProperties.SelectEntry(lv);
                        //viewProperties.OpenSubEntries(lv);
                        //while (!(actionStack.ActiveAction is SelectObjectsAction) && (actionStack.ActiveAction != null))
                        //{
                        //    RemoveActiveAction();
                        //}
                        return true;
                    }
                }
#endif
            }
            if (MenuId.StartsWith("MenuId.View.Model.ModelView."))
            {   // ModelView anzeigen
                string modelViewName = MenuId.Substring("MenuId.View.Model.ModelView.".Length);
                foreach (ModelView mv in modelViews.Values)
                {
                    if (mv.Name == modelViewName)
                    {
                        // mv.Frame = this;
                        //if (!mv.IsConnected()) mv.Connect(condorScrollableCtrls[activeControl].CondorCtrl);
                        //condorScrollableCtrls[activeControl].SetView(mv);
                        //InvalidateAll();
                        //controlCenter.ShowTabPage(controlCenter.FindTabPage(viewProperties));
                        //viewProperties.SelectEntry(mv);
                        //viewProperties.OpenSubEntries(mv);
                        return true;
                    }
                }
            }
            GeoPoint fixPoint = GeoPoint.Invalid;
            {
                if (ActiveView is ModelView mv && mv.FixPointValid) fixPoint = mv.FixPoint;
            }
            switch (MenuId)
            {
                case "MenuId.App.About":
                    OnAppAbout();
                    return true;
                case "MenuId.File.New":
                    {
                        return GenerateNewProject();
                    }
                case "MenuId.File.Open":
                    if (project != null)
                    {
                        if (!project.SaveModified()) return true; // "Abbrechen" wurde gedrückt
                        ProjectClosedEvent?.Invoke(project, this);
                    }
                    OnFileOpen(null);
                    return true;
                case "MenuId.File.Save":
                    project.WriteToFile(project.FileName);
                    FileNameChangedEvent?.Invoke(project.FileName);
                    if (project.FileName != null) MRUFiles.AddPath(project.FileName, "cdb");
                    UpdateMRUMenu(MRUFiles.GetMRUFiles());
                    return true;
                case "MenuId.File.Save.As":
                    project.WriteToFile(null);
                    FileNameChangedEvent?.Invoke(project.FileName);
                    if (project.FileName != null) MRUFiles.AddPath(project.FileName, "cdb");
                    UpdateMRUMenu(MRUFiles.GetMRUFiles());
                    return true;
                case "MenuId.Zoom.Detail":
                    SetAction(new ZoomAction());
                    return true;
                case "MenuId.Zoom.DetailPlus":
                    ActiveView.ZoomDelta(1 / mouseWheelZoomFactor);
                    return false;
                case "MenuId.Zoom.DetailMinus":
                    ActiveView.ZoomDelta(mouseWheelZoomFactor);
                    return false;
                case "MenuId.Zoom.Total":
                    ActiveView.ZoomTotal(1.1);
                    return true;
                case "MenuId.Thinlinesonly":
                    SetControlCenterFocus("View", "ModelView", true, false);
                    SetControlCenterFocus("View", "ModelView.LineWidthMode", true, false);
                    return true;
                case "MenuId.Repaint":
                    ActiveView.InvalidateAll();
                    return true;
                case "MenuId.ViewPoint":
                    SetAction(new ViewPointAction());
                    return true;
                case "MenuId.ViewFixPoint":
                    SetAction(new ViewFixPointAction());
                    return true;
                case "MenuId.Scroll":
                    SetAction(new ScrollAction());
                    return true;
                case "MenuId.Zoom":
                    SetAction(new ZoomPlusMinusAction());
                    return true;
                case "MenuId.ZAxisUp":
                    if (ActiveView is ModelView)
                    {
                        (ActiveView as ModelView).ZAxisUp = !(ActiveView as ModelView).ZAxisUp;
                        (ActiveView as ModelView).Recalc();
                        (ActiveView as ModelView).Invalidate();
                    }
                    return true;
                case "MenuId.Projection.Direction.FromTop":
                    // (ActiveView as ModelView).ProjectedModel.SetViewDirection(-GeoVector.ZAxis, false);
                    if (Settings.GlobalSettings.GetBoolValue("ModelView.AnimateViewChange", true))
                    {
                        ActiveView.Projection.SetDirectionAnimated(-GeoVector.ZAxis, GeoVector.YAxis, ActiveView.Model, Settings.GlobalSettings.GetBoolValue("ModelView.AutoZoomTotal", false),
                            ActiveView.Canvas, fixPoint);
                    }
                    else
                    {
                        ActiveView.Projection.SetDirection(-GeoVector.ZAxis, GeoVector.YAxis, ActiveView.Model.Extent);
                        ActiveView.InvalidateAll();
                        ActiveView.ZoomToRect(ActiveView.Model.GetExtentForZoomTotal(ActiveView.Projection) * 1.1); // .ZoomTotal(condorScrollableCtrls[activeControl].CondorCtrl.ClientRectangle, 1.1);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromFront":
                    if (Settings.GlobalSettings.GetBoolValue("ModelView.AnimateViewChange", true))
                    {
                        ActiveView.Projection.SetDirectionAnimated(GeoVector.YAxis, GeoVector.ZAxis, ActiveView.Model, Settings.GlobalSettings.GetBoolValue("ModelView.AutoZoomTotal", false),
                            ActiveView.Canvas, fixPoint);
                    }
                    else
                    {
                        ActiveView.Projection.SetDirection(GeoVector.YAxis, GeoVector.ZAxis, ActiveView.Model.Extent);
                        ActiveView.InvalidateAll();
                        ActiveView.ZoomToRect(ActiveView.Model.GetExtentForZoomTotal(ActiveView.Projection) * 1.1);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromBack":
                    if (Settings.GlobalSettings.GetBoolValue("ModelView.AnimateViewChange", true))
                    {
                        ActiveView.Projection.SetDirectionAnimated(-GeoVector.YAxis, GeoVector.ZAxis, ActiveView.Model, Settings.GlobalSettings.GetBoolValue("ModelView.AutoZoomTotal", false),
                            ActiveView.Canvas, fixPoint);
                    }
                    else
                    {
                        ActiveView.Projection.SetDirection(-GeoVector.YAxis, GeoVector.ZAxis, ActiveView.Model.Extent);
                        ActiveView.InvalidateAll();
                        ActiveView.ZoomToRect(ActiveView.Model.GetExtentForZoomTotal(ActiveView.Projection) * 1.1);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromLeft":
                    if (Settings.GlobalSettings.GetBoolValue("ModelView.AnimateViewChange", true))
                    {
                        ActiveView.Projection.SetDirectionAnimated(GeoVector.XAxis, GeoVector.ZAxis, ActiveView.Model, Settings.GlobalSettings.GetBoolValue("ModelView.AutoZoomTotal", false),
                            ActiveView.Canvas, fixPoint);
                    }
                    else
                    {
                        ActiveView.Projection.SetDirection(GeoVector.XAxis, GeoVector.ZAxis, ActiveView.Model.Extent);
                        ActiveView.InvalidateAll();
                        ActiveView.ZoomToRect(ActiveView.Model.GetExtentForZoomTotal(ActiveView.Projection) * 1.1);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromRight":
                    if (Settings.GlobalSettings.GetBoolValue("ModelView.AnimateViewChange", true))
                    {
                        ActiveView.Projection.SetDirectionAnimated(-GeoVector.XAxis, GeoVector.ZAxis, ActiveView.Model, Settings.GlobalSettings.GetBoolValue("ModelView.AutoZoomTotal", false),
                            ActiveView.Canvas, fixPoint);

                    }
                    else
                    {
                        ActiveView.Projection.SetDirection(-GeoVector.XAxis, GeoVector.ZAxis, ActiveView.Model.Extent);
                        ActiveView.InvalidateAll();
                        ActiveView.ZoomToRect(ActiveView.Model.GetExtentForZoomTotal(ActiveView.Projection) * 1.1);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromBottom":
                    if (Settings.GlobalSettings.GetBoolValue("ModelView.AnimateViewChange", true))
                    {
                        ActiveView.Projection.SetDirectionAnimated(GeoVector.ZAxis, GeoVector.YAxis, ActiveView.Model, Settings.GlobalSettings.GetBoolValue("ModelView.AutoZoomTotal", false),
                            ActiveView.Canvas, fixPoint);
                    }
                    else
                    {
                        ActiveView.Projection.SetDirection(GeoVector.ZAxis, GeoVector.YAxis, ActiveView.Model.Extent);
                        ActiveView.InvalidateAll();
                        ActiveView.ZoomToRect(ActiveView.Model.GetExtentForZoomTotal(ActiveView.Projection) * 1.1);
                    }
                    return true;
                case "MenuId.Projection.Direction.Orthogonal":
                    GeoVector dir = -ActiveView.Projection.DrawingPlane.Normal;
                    dir.Norm();
                    if (Settings.GlobalSettings.GetBoolValue("ModelView.AnimateViewChange", true))
                    {
                        ActiveView.Projection.SetDirectionAnimated(dir, GeoVector.ZAxis, ActiveView.Model, Settings.GlobalSettings.GetBoolValue("ModelView.AutoZoomTotal", false),
                            ActiveView.Canvas, fixPoint);
                    }
                    else
                    {
                        ActiveView.Projection.SetDirection(dir, GeoVector.ZAxis, ActiveView.Model.Extent);
                        ActiveView.InvalidateAll();
                        ActiveView.ZoomToRect(ActiveView.Model.GetExtentForZoomTotal(ActiveView.Projection) * 1.1);
                    }
                    return true;
                case "MenuId.Projection.Direction.Isometric":
                    GeoVector iso = new GeoVector(-1, -1, -1);
                    iso.Norm();
                    if (Settings.GlobalSettings.GetBoolValue("ModelView.AnimateViewChange", true))
                    {
                        ActiveView.Projection.SetDirectionAnimated(iso, GeoVector.ZAxis, ActiveView.Model, Settings.GlobalSettings.GetBoolValue("ModelView.AutoZoomTotal", false),
                            ActiveView.Canvas, fixPoint);
                    }
                    else
                    {
                        ActiveView.Projection.SetDirection(iso, GeoVector.ZAxis, ActiveView.Model.Extent);
                        ActiveView.InvalidateAll();
                        ActiveView.ZoomToRect(ActiveView.Model.GetExtentForZoomTotal(ActiveView.Projection) * 1.1);
                    }
                    return true;
                case "MenuId.Projection.Direction.Perspective":
                    {
                        BoundingCube ext = ActiveView.Model.Extent;
                        GeoPoint viewPoint = ext.GetCenter() + ext.Size * new GeoVector(1, 1, 1);
                        ActiveView.Projection.SetPerspective(viewPoint, new GeoVector(-1, -1, -1), ext, ext.GetCenter());
                        ActiveView.InvalidateAll();
                        ActiveView.ZoomToRect(ActiveView.Model.GetExtentForZoomTotal(ActiveView.Projection) * 1.1);
                        return true;
                    }
                case "MenuId.File.Print":
                    // OnPrint();
                    return true;
                case "MenuId.File.Print.Setup":
                    // OnPrintSetup();
                    return true;
                case "MenuId.File.Print.SelectPrinter":
                    // OnSelectPrinter();
                    return true;
                case "MenuId.Edit.SelectAll":
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        (actionStack.ActiveAction as SelectObjectsAction).SelectAllVisibleObjects(ActiveView);
                        // SetSelectedObjects(project.GetActiveModel().AllObjects, true);
                    }
                    return true;
                case "MenuId.Colors":
                    SetControlCenterFocus("Project", "ColorList", true, false);
                    return true;
                case "MenuId.Layer":
                    SetControlCenterFocus("Project", "LayerList", true, false);
                    return true;
                case "MenuId.LineWidth":
                    SetControlCenterFocus("Project", "LineWidthList", true, false);
                    return true;
                case "MenuId.LinePattern":
                    SetControlCenterFocus("Project", "LinePatternList", true, false);
                    return true;
                case "MenuId.Hatchstyle":
                    SetControlCenterFocus("Project", "HatchStyleList", true, false);
                    return true;
                case "MenuId.Dimstyle":
                    SetControlCenterFocus("Project", "DimensionStyleList", true, false);
                    return true;
                case "MenuId.Style":
                    SetControlCenterFocus("Project", "StyleList", true, false);
                    return true;
                case "MenuId.SetGrid":
                    SetControlCenterFocus("View", "ModelView", true, false);
                    SetControlCenterFocus("View", "Grid", true, false);
                    return true;
                case "MenuId.Activate.Grid": OnSnapGrid(); return true;
                case "MenuId.Activate.Ortho": OnSnapOrtho(); return true;
                case "MenuId.Snap.ObjectSnapPoint": OnSnapSnapPoint(); return true;
                case "MenuId.Snap.ObjectPoint": OnSnapObjectPoint(); return true;
                case "MenuId.Snap.DropPoint": OnSnapDropPoint(); return true;
                case "MenuId.Snap.ObjectCenter": OnSnapCenter(); return true;
                case "MenuId.Snap.TangentPoint": OnSnapTangentPoint(); return true;
                case "MenuId.Snap.Intersections": OnSnapIntersections(); return true;
                case "MenuId.Snap.Surface": OnSnapSurface(); return true;
                case "MenuId.Select":
                    if (actionStack.ActiveAction.GetID() != "SelectObjects")
                    {
                        actionStack.ActiveAction.OnCommand("MenuId.Select.Notify"); // damit kann die Aktion entscheiden ob sie einfach abbrechen will oder explizit reagieren will
                        actionStack.ActiveAction.OnEscape();
                        // das bricht die aktive Aktion ab, ist das immer so gewünscht?
                    }
                    return true;
                case "MenuId.Constr.Point":
                    SetAction(new ConstrPoint());
                    return true;
                case "MenuId.Constr.Line.TwoPoints":
                    SetAction(new ConstrLine2Points());
                    return true;
                case "MenuId.Constr.Line.PointLengthAngle":
                    SetAction(new ConstrLineLengthAngle());
                    return true;
                case "MenuId.Constr.Line.MiddlePerp":
                    SetAction(new ConstrLineMiddlePerp());
                    return true;
                case "MenuId.Constr.Line.BisectAngle":
                    SetAction(new ConstrLineBisectAngle());
                    return true;
                case "MenuId.Constr.Line.ParallelDist":
                    SetAction(new ConstrLineParallel());
                    return true;
                case "MenuId.Constr.Line.ParallelPoint":
                    SetAction(new ConstrLineParallelPoint());
                    return true;
                case "MenuId.Constr.Line.PointTangent":
                    SetAction(new ConstrLinePointTangent());
                    return true;
                case "MenuId.Constr.Line.TwoTangents":
                    SetAction(new ConstrLine2Tangents());
                    return true;
                case "MenuId.Constr.Line.TangentAngle":
                    SetAction(new ConstrLineTangentAngle());
                    return true;
                case "MenuId.Constr.Line.PointPerp":
                    SetAction(new ConstrLinePointPerp());
                    return true;
                case "MenuId.Constr.Rect.PointWidthHeightAngle":
                    SetAction(new ConstrRectPointWidthHeightAngle());
                    return true;
                case "MenuId.Constr.Rect.2PointsHeight":
                    SetAction(new ConstrRect2PointsHeight());
                    return true;
                case "MenuId.Constr.Rect.2DiagonalPoints":
                    SetAction(new ConstrRect2DiagonalPoints());
                    return true;
                case "MenuId.Constr.Rect.Parallelogram":
                    SetAction(new ConstrRectParallelogram());
                    return true;
                case "MenuId.Constr.Circle.CenterRadius":
                    SetAction(new ConstrCircleCenterRadius());
                    return true;
                case "MenuId.Constr.Circle.CenterPoint":
                    SetAction(new ConstrCircleCenterPoint());
                    return true;
                case "MenuId.Constr.Circle.2Points":
                    SetAction(new ConstrCircle2Points());
                    return true;
                case "MenuId.Constr.Circle.3Points":
                    SetAction(new ConstrCircle3Points());
                    return true;
                case "MenuId.Constr.Circle.TwoPointsRadius":
                    SetAction(new ConstrCircleTwoPointsRadius());
                    return true;
                case "MenuId.Constr.Circle.TwoTangentsPoint":
                    SetAction(new ConstrCircle2TangentsPoint());
                    return true;
                case "MenuId.Constr.Circle.TangentPointRadius":
                    SetAction(new ConstrCircleTangentPointRadius());
                    return true;
                case "MenuId.Constr.Circle.ThreeTangents":
                    SetAction(new ConstrCircle3Tangents());
                    return true;
                case "MenuId.Constr.Circle.TwoTangentsRadius":
                    SetAction(new ConstrCircle2TangentsRadius());
                    return true;
                case "MenuId.Constr.Arc.CenterRadius":
                    SetAction(new ConstrArcCenterRadius());
                    return true;
                case "MenuId.Constr.Arc.2PointsRadius":
                    SetAction(new ConstrArc2PointsRadius());
                    return true;
                case "MenuId.Constr.Arc.CenterStartEnd":
                    SetAction(new ConstrArcCenterStartEnd());
                    return true;
                case "MenuId.Constr.Arc.3Points":
                    SetAction(new ConstrArc3Points());
                    return true;
                case "MenuId.Constr.Arc.ThreeTangents":
                    SetAction(new ConstrArc3Tangents());
                    return true;
                case "MenuId.Constr.Arc.TwoTangentsRadius":
                    SetAction(new ConstrArc2TangentsRadius());
                    return true;
                case "MenuId.Constr.Arc.TwoTangentsPoint":
                    SetAction(new ConstrArc2TangentsPoint());
                    return true;
                case "MenuId.Constr.Arc.TangentPointRadius":
                    SetAction(new ConstrArcTangentPointRadius());
                    return true;
                case "MenuId.Constr.Ellipse.CenterRadius":
                    SetAction(new ConstrEllipseCenterRadius());
                    return true;
                case "MenuId.Constr.Ellipse.2PointsDirections":
                    SetAction(new ConstrEllipse2PointsDirections());
                    return true;
                case "MenuId.Constr.Ellipsearc.CenterRadius":
                    SetAction(new ConstrEllipseArcCenterRadius());
                    return true;
                case "MenuId.Constr.Ellipsearc.2PointsDirections":
                    SetAction(new ConstrEllipseArc2PointsDirections());
                    return true;
                case "MenuId.Constr.Sphere":
                    SetAction(new Constr3DSphere());
                    return true;
                case "MenuId.Constr.Box":
                    SetAction(new Constr3DBox());
                    return true;
                case "MenuId.Constr.Cylinder":
                    SetAction(new Constr3DCylinder());
                    return true;
                case "MenuId.Constr.Cone":
                    SetAction(new Constr3DCone());
                    return true;
                case "MenuId.Constr.Torus":
                    SetAction(new Constr3DTorus());
                    return true;
                case "MenuId.Constr.Text.1Point":
                    SetAction(new ConstrText1Point());
                    return true;
                case "MenuId.Constr.Text.2Points":
                    SetAction(new ConstrText2Points());
                    return true;
                case "MenuId.Constr.Horizontal.Dimension":
                    SetAction(new ConstrDimensionPoints(ConstrDimensionPoints.DimDirection.Horizontal));
                    return true;
                case "MenuId.Constr.Vertical.Dimension":
                    SetAction(new ConstrDimensionPoints(ConstrDimensionPoints.DimDirection.Vertical));
                    return true;
                case "MenuId.Constr.Sloping.Dimension":
                    SetAction(new ConstrDimensionPoints(ConstrDimensionPoints.DimDirection.Sloping));
                    return true;
                case "MenuId.Constr.Distance.Dimension":
                    SetAction(new ConstrDimensionDistance());
                    return true;
                case "MenuId.Constr.Radius.Dimension":
                    SetAction(new ConstrDimensionArc(ConstrDimensionArc.DimArcType.Radius));
                    return true;
                case "MenuId.Constr.Diameter.Dimension":
                    SetAction(new ConstrDimensionArc(ConstrDimensionArc.DimArcType.Diameter));
                    return true;
                case "MenuId.Constr.Angle.Dimension.3p":
                    SetAction(new ConstrDimensionDirection3Points());
                    return true;
                case "MenuId.Constr.Angle.Dimension.Objects":
                    SetAction(new ConstrDimensionDirectionObjects());
                    return true;
                case "MenuId.Constr.Angle.Dimension.4p":
                    SetAction(new ConstrDimensionDirection4Points());
                    return true;
                case "MenuId.Constr.Point.Dimension":
                    SetAction(new ConstrDimensionLabelPoint(ConstrDimensionLabelPoint.DimLabelType.Point));
                    return true;
                case "MenuId.Constr.Labeling.Dimension":
                    SetAction(new ConstrDimensionLabelPoint(ConstrDimensionLabelPoint.DimLabelType.Labeling));
                    return true;
                case "MenuID.Constr.BSpline.Points":
                    SetAction(new ConstrBSplinePoints());
                    return true;
                case "MenuId.Constr.Polyline":
                    SetAction(new ConstrPolylinePoints());
                    return true;
                case "MenuId.Constr.Picture.RefPoint2Directions":
                    SetAction(new ConstrPicturePoint2Directions());
                    return true;
                case "MenuId.Constr.Picture.RefPointWidthHeight":
                    SetAction(new ConstrPicturePointWidthHeight());
                    return true;
                //case "MenuId.Constr.Picture":
                //    SetAction(new ConstrPicture());
                //    return true;
                case "MenuId.Constr.Symbol":
                    ShowPropertyDisplay("Symbol");
                    return true;
                case "MenuId.Constr.Hatch.Inside":
                    SetAction(new ConstrHatchInside(ConstrHatchInside.HatchMode.simple));
                    return true;
                case "MenuId.Constr.Hatch.WithHoles":
                    SetAction(new ConstrHatchInside(ConstrHatchInside.HatchMode.excludeHoles));
                    return true;
                case "MenuId.Constr.Hatch.Hull":
                    SetAction(new ConstrHatchInside(ConstrHatchInside.HatchMode.hull));
                    return true;
                case "MenuId.Trim":
                    SetAction(new ToolsTrim());
                    return true;
                case "MenuId.Trim.Split":
                    SetAction(new ToolsTrimSplit());
                    return true;
                case "MenuId.Expand":
                    SetAction(new ToolsExpand());
                    return true;
                case "MenuId.Connect":
                    SetAction(new ToolsConnect());
                    return true;
                case "MenuId.Cut.Off":
                    SetAction(new ToolsCutOff());
                    return true;
                case "MenuId.CutOffMultiple":
                    SetAction(new ToolsCutOffMultiple());
                    return true;
                case "MenuId.Round.Off":
                    SetAction(new ToolsRoundOff());
                    return true;
                case "MenuId.Round.In":
                    SetAction(new ToolsRoundIn());
                    return true;
                case "MenuId.Aequidist":
                    SetAction(new ConstructAequidist(null));
                    return true;
                case "MenuId.Constr.Face.FromObject":
                    {
                        if (actionStack.ActiveAction is SelectObjectsAction)
                        {
                            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                            if (sel.Count > 0)
                            {
                                Face fc = Face.MakeFace(sel);
                                if (fc != null)
                                {
                                    using (Project.Undo.UndoFrame)
                                    {
                                        (actionStack.ActiveAction as SelectObjectsAction).ClearSelectedObjects();
                                        // ActiveView.Model.Remove(sel);
                                        Project.SetDefaults(fc);
                                        ActiveView.Model.Add(fc);
                                        (actionStack.ActiveAction as SelectObjectsAction).SetSelectedObjects(new GeoObjectList(fc));
                                    }
                                }
                                //Constr3DMakeFace temp = new Constr3DMakeFace();
                                //GeoObjectList faceGeneric = temp.makeFaceDo(sel, this);
                                //if (faceGeneric == null)
                                //    (actionStack.ActiveAction as SelectObjectsAction).SetSelectedObjects(sel);
                                //else (actionStack.ActiveAction as SelectObjectsAction).SetSelectedObjects(faceGeneric);
                            }
                            else SetAction(new Constr3DMakeFace());
                        }
                        else SetAction(new Constr3DMakeFace());
                    }
                    return true;
                case "MenuId.Constr.Face.ObjectExtrude":
                    {
                        if (actionStack.ActiveAction is SelectObjectsAction)
                        {
                            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                            if (sel.Count > 0)
                                SetAction(new Constr3DPathExtrude(sel));
                            else SetAction(new Constr3DPathExtrude((GeoObjectList)null));
                        }
                        else SetAction(new Constr3DPathExtrude((GeoObjectList)null));
                    }
                    return true;
                case "MenuId.Constr.Face.ObjectRotate":
                    {
                        if (actionStack.ActiveAction is SelectObjectsAction)
                        {
                            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                            if (sel.Count > 0)
                                SetAction(new Constr3DPathRotate(sel));
                            else SetAction(new Constr3DPathRotate((GeoObjectList)null));
                        }
                        else SetAction(new Constr3DPathRotate((GeoObjectList)null));
                    }
                    return true;
                case "MenuId.Constr.Solid.FaceExtrude":
                    {
                        if (actionStack.ActiveAction is SelectObjectsAction)
                        {
                            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                            if (sel.Count > 0)
                                SetAction(new Constr3DFaceExtrude(sel));
                            else SetAction(new Constr3DFaceExtrude((GeoObjectList)null));

                        }
                        else SetAction(new Constr3DFaceExtrude((GeoObjectList)null));
                    }
                    return true;
                case "MenuId.Constr.Solid.FaceRotate":
                    {
                        if (actionStack.ActiveAction is SelectObjectsAction)
                        {
                            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                            if (sel.Count > 0)
                                SetAction(new Constr3DFaceRotate(sel));
                            else SetAction(new Constr3DFaceRotate((GeoObjectList)null));
                        }
                        else SetAction(new Constr3DFaceRotate((GeoObjectList)null));
                    }
                    return true;
                case "MenuId.Constr.Solid.ScrewPath":
                    {
                        if (actionStack.ActiveAction is SelectObjectsAction)
                        {
                            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                            if (Constr3DScrewPath.canUseList(sel))
                                SetAction(new Constr3DScrewPath(sel));
                            else SetAction(new Constr3DScrewPath((GeoObjectList)null));
                        }
                        else SetAction(new Constr3DScrewPath((GeoObjectList)null));
                    }
                    return true;
                case "MenuId.Constr.Solid.RuledSolid":
                    {
                        if (actionStack.ActiveAction is SelectObjectsAction)
                        {
                            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                            if (sel.Count > 1) Constr3DRuledSolid.ruledSolidDo(sel, this);
                            else
                            {
                                if (sel.Count > 0)
                                    SetAction(new Constr3DRuledSolid(sel, this));
                                else SetAction(new Constr3DRuledSolid((GeoObjectList)null, this));
                            }
                        }
                        else SetAction(new Constr3DRuledSolid((GeoObjectList)null, this));
                    }
                    //                   SetAction(new Constr3DRuledSolid((GeoObjectList)null));
                    return true;
                case "MenuId.Constr.Face.RuledFace":
                    {
                        if (actionStack.ActiveAction is SelectObjectsAction)
                        {
                            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                            if (sel.Count > 1) Constr3DRuledFace.ruledFaceDo(sel, this);
                            else
                            {
                                if (sel.Count > 0)
                                    SetAction(new Constr3DRuledFace(sel, this));
                                else SetAction(new Constr3DRuledFace((GeoObjectList)null, this));
                            }
                        }
                        else SetAction(new Constr3DRuledFace((GeoObjectList)null, this));
                    }
                    //                   SetAction(new Constr3DRuledFace((GeoObjectList)null));
                    return true;
                case "MenuId.PlaneIntersection":
                    SetAction(new Constr3DPlaneIntersection());
                    return true;
                case "MenuId.Fillet":
                    SetAction(new Constr3DFillet());
                    return true;
                case "MenuId.Chamfer":
                    SetAction(new Constr3DChamfer());
                    return true;
                case "MenuId.Constr.Solid.ModifyMode.Unite":
                    {
                        if (solidModellingMode == Make3D.SolidModellingMode.unite)
                            solidModellingMode = Make3D.SolidModellingMode.unchanged;
                        else solidModellingMode = Make3D.SolidModellingMode.unite;
                    }
                    return true;
                case "MenuId.Constr.Solid.ModifyMode.Subtract":
                    {
                        if (solidModellingMode == Make3D.SolidModellingMode.subtract)
                            solidModellingMode = Make3D.SolidModellingMode.unchanged;
                        else solidModellingMode = Make3D.SolidModellingMode.subtract;
                    }
                    return true;
                case "MenuId.SelectedObjects.SewFaces":
                    {
                        if (actionStack.ActiveAction is SelectObjectsAction soa)
                        {
                            return (soa as ICommandHandler).OnCommand("MenuId.SelectedObjects.SewFaces");
                        }
                    }
                    return true;
                case "MenuId.SelectedEdges.Round":
                    {   // funktioniert soweit, muss aber noch mit solid getestet werden
                        // und braucht für eine Aktion einen "MultiEdgeInput"
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects().Clone();
                        if (sel.Count > 0)
                        {
                            List<Edge> edges = new List<Edge>();
                            for (int i = 0; i < sel.Count; ++i)
                            {
                                if (sel[i].Owner is Edge) edges.Add((sel[i].Owner as Edge));
                            }
                            if (edges.Count > 0)
                            {
                            }

                        }
                    }
                    return true;
                //case "MenuId.Constr.Solid.Fuse":
                //    {
                //        if (actionStack.ActiveAction is SelectObjectsAction)
                //        {
                //            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects().Clone();
                //            if (sel.Count > 0)
                //            {
                //            }
                //        }
                //    }
                //    return true;
                case "MenuId.Constr.Solid.ShellToSolid":
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        GeoObjectList toAdd = new GeoObjectList();
                        GeoObjectList toRemove = new GeoObjectList();
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        if (sel.Count > 0)
                        {
                            for (int i = 0; i < sel.Count; i++)
                            {
                                if (sel[i] is Shell && (sel[i] as Shell).OpenEdges.Length == 0)
                                {
                                    Solid sld = Solid.Construct();
                                    sld.SetShell(sel[i] as Shell);
                                    toAdd.Add(sld);
                                    toRemove.Add(sel[i]);
                                }
                            }
                            if (toRemove.Count > 0)
                            {
                                (actionStack.ActiveAction as SelectObjectsAction).ClearSelectedObjects();
                                ActiveView.Model.Remove(toRemove);
                                ActiveView.Model.Add(toAdd);
                                (actionStack.ActiveAction as SelectObjectsAction).SetSelectedObjects(toAdd);
                            }
                        }

                    }
                    return true;

                case "MenuId.Measure.Point.IntermediatePoint":
                    SetAction(new CADability.Actions.ConstructMidPoint(null));
                    return true;
                case "MenuId.Measure.Point.ObjectPoint":
                    SetAction(new CADability.Actions.ConstructObjectPoint(null));
                    return true;
                case "MenuId.Measure.Point.IntersectionTwoCurves":
                    SetAction(new CADability.Actions.ConstructIntersectPoint(null));
                    return true;
                case "MenuId.Measure.Point.OffsetByVector":
                    SetAction(new CADability.Actions.ConstructVectorPoint(null));
                    return true;
                case "MenuId.Measure.Point.Polar":
                    SetAction(new CADability.Actions.ConstructPolarPoint(null));
                    return true;
                case "MenuId.Measure.Length.DistanceOfCurve":
                    SetAction(new CADability.Actions.ConstructDistanceOfCurve(null));
                    return true;
                case "MenuId.Measure.Length.DistanceTwoPoints":
                    SetAction(new CADability.Actions.ConstructDistanceTwoPoints(null));
                    return true;
                case "MenuId.Measure.Length.DistancePointCurve":
                    SetAction(new CADability.Actions.ConstructDistancePointCurve(null));
                    return true;
                case "MenuId.Measure.Length.DistanceTwoCurves":
                    SetAction(new CADability.Actions.ConstructDistanceTwoCurves(null));
                    return true;
                case "MenuId.Measure.Vector.DirectionOfCurve":
                    SetAction(new CADability.Actions.ConstructDirectionOfCurve(null));
                    return true;
                case "MenuId.Measure.Vector.DirectionOfSurface":
                    SetAction(new CADability.Actions.ConstructDirectionOfSurface(null));
                    return true;
                case "MenuId.Measure.Vector.DirectionTwoPoints":
                    SetAction(new CADability.Actions.ConstructDirectionTwoPoints(null));
                    return true;
                case "MenuId.Measure":
                    SetAction(new Measure());
                    return true;
                case "MenuId.RoundMultiple":
                    SetAction(new ToolsRoundMultiple());
                    return true;
                case "MenuId.Import":
                    OnFileImport();
                    return true;
                case "MenuId.Export":
                    Export();
                    return true;
                case "MenuId.Export.Settings":
                    SetControlCenterFocus("Global", "DxfDwg", true, false);
                    return true;
                case "MenuId.View.NewAnimatedView":
                    if (project != null)
                    {
                        AnimatedView newAnimatedView = new AnimatedView(project, project.GetActiveModel(), this);
                        newAnimatedView.Name = project.GetNewAnimatedViewName();
                        project.AnimatedViews.Add(newAnimatedView);
                        animatedViews.Add(newAnimatedView);
                        this.ActiveView = newAnimatedView;
                        newAnimatedView.SelectionEnabled = true;
                    }
                    return true;
#if !WEBASSEMBLY
                case "MenuId.View.NewGDIView":
                    if (project != null)
                    {
                        GDI2DView newGDIView = new GDI2DView(project, project.GetActiveModel(), this);
                        newGDIView.Name = project.GetNewGDI2DViewName();
                        project.GdiViews.Add(newGDIView);
                        gdiViews.Add(newGDIView);
                        this.ActiveView = newGDIView;
                        newGDIView.ZoomToModelExtent(1.1);
                    }
                    return true;
#endif
            }
            // forward command handling to the project
            if ((project as ICommandHandler).OnCommand(MenuId)) return true;
            // forward command handling to the actions
            if (actionStack.OnCommand(MenuId)) return true;
            // forward command handling to the active view
            if ((ActiveView is ICommandHandler) && (ActiveView as ICommandHandler).OnCommand(MenuId)) return true;
            return false; // could not handle this command
        }
        bool checkModelExist()
        {
            return modelViews.Count > 0;
        }
        public virtual bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            // 1. let the user decide
            bool Processed = false;
            UpdateCommandEvent?.Invoke(MenuId, CommandState, ref Processed);
            if (Processed) return true;
            // 2. let the frame decide
            switch (MenuId)
            {
                case "MenuId.View.Model":
                    UpdateViewMenu(false); // die ModelViews können umbenannt worden sein ...
                    return true;
                case "MenuId.View.Layout":
                    UpdateViewMenu(false); // die ModelViews können umbenannt worden sein ...
                    return true;
                case "MenuId.Snap.ObjectSnapPoint":
                    CommandState.Checked = (snapMode & SnapPointFinder.SnapModes.SnapToObjectSnapPoint) != 0;
                    return true;
                case "MenuId.Activate.Grid":
                    CommandState.Checked = (snapMode & SnapPointFinder.SnapModes.SnapToGridPoint) != 0;
                    return true;
                case "MenuId.Activate.Ortho":
                    CommandState.Checked = (snapMode & SnapPointFinder.SnapModes.AdjustOrtho) != 0;
                    return true;
                case "MenuId.Snap.ObjectPoint":
                    CommandState.Checked = (snapMode & SnapPointFinder.SnapModes.SnapToObjectPoint) != 0;
                    return true;
                case "MenuId.Snap.DropPoint":
                    CommandState.Checked = (snapMode & SnapPointFinder.SnapModes.SnapToDropPoint) != 0;
                    return true;
                case "MenuId.Snap.ObjectCenter":
                    CommandState.Checked = (snapMode & SnapPointFinder.SnapModes.SnapToObjectCenter) != 0;
                    return true;
                case "MenuId.Snap.TangentPoint":
                    CommandState.Checked = (snapMode & SnapPointFinder.SnapModes.SnapToTangentPoint) != 0;
                    return true;
                case "MenuId.Snap.Intersections":
                    CommandState.Checked = (snapMode & SnapPointFinder.SnapModes.SnapToIntersectionPoint) != 0;
                    return true;
                case "MenuId.Snap.Surface":
                    CommandState.Checked = (snapMode & SnapPointFinder.SnapModes.SnapToFaceSurface) != 0;
                    return true;
                case "MenuId.Edit.SelectAll":
                    CommandState.Enabled = (actionStack.ActiveAction is SelectObjectsAction);
                    return true;
                case "MenuId.Constr.Point":
                case "MenuId.Constr.Line.TwoPoints":
                case "MenuId.Constr.Line.PointLengthAngle":
                case "MenuId.Constr.Line.MiddlePerp":
                case "MenuId.Constr.Line.BisectAngle":
                case "MenuId.Constr.Line.ParallelDist":
                case "MenuId.Constr.Line.ParallelPoint":
                case "MenuId.Constr.Line.PointTangent":
                case "MenuId.Constr.Line.TwoTangents":
                case "MenuId.Constr.Line.TangentAngle":
                case "MenuId.Constr.Line.PointPerp":
                case "MenuId.Constr.Rect.PointWidthHeightAngle":
                case "MenuId.Constr.Rect.2PointsHeight":
                case "MenuId.Constr.Rect.2DiagonalPoints":
                case "MenuId.Constr.Rect.Parallelogram":
                case "MenuId.Constr.Circle.CenterRadius":
                case "MenuId.Constr.Circle.CenterPoint":
                case "MenuId.Constr.Circle.2Points":
                case "MenuId.Constr.Circle.3Points":
                case "MenuId.Constr.Circle.TwoPointsRadius":
                case "MenuId.Constr.Circle.ThreeTangents":
                case "MenuId.Constr.Circle.TwoTangentsPoint":
                case "MenuId.Constr.Circle.TwoTangentsRadius":
                case "MenuId.Constr.Circle.TangentPointRadius":
                case "MenuId.Constr.Arc.CenterRadius":
                case "MenuId.Constr.Arc.2PointsRadius":
                case "MenuId.Constr.Arc.CenterStartEnd":
                case "MenuId.Constr.Arc.3Points":
                case "MenuId.Constr.Arc.ThreeTangents":
                case "MenuId.Constr.Arc.TwoTangentsPoint":
                case "MenuId.Constr.Arc.TwoTangentsRadius":
                case "MenuId.Constr.Arc.TangentPointRadius":
                case "MenuId.Constr.Ellipse.CenterRadius":
                case "MenuId.Constr.Ellipse.2PointsDirections":
                case "MenuId.Constr.Ellipsearc.CenterRadius":
                case "MenuId.Constr.Ellipsearc.2PointsDirections":
                case "MenuId.Constr.Text.1Point":
                case "MenuId.Constr.Text.2Points":
                case "MenuId.Constr.Horizontal.Dimension":
                case "MenuId.Constr.Vertical.Dimension":
                case "MenuId.Constr.Sloping.Dimension":
                case "MenuId.Constr.Distance.Dimension":
                case "MenuId.Constr.Radius.Dimension":
                case "MenuId.Constr.Diameter.Dimension":
                case "MenuId.Constr.Angle.Dimension.3p":
                case "MenuId.Constr.Angle.Dimension.Objects":
                case "MenuId.Constr.Angle.Dimension.4p":
                case "MenuId.Constr.Point.Dimension":
                case "MenuId.Constr.Labeling.Dimension":
                case "MenuID.Constr.BSpline.Points":
                case "MenuId.Constr.Polyline":
                case "MenuId.Constr.Hatch.Inside":
                case "MenuId.Constr.Hatch.WithHoles":
                case "MenuId.Constr.Hatch.Hull":
                case "MenuId.Constr.Picture.RefPoint2Directions":
                case "MenuId.Constr.Picture.RefPointWidthHeight":
                case "MenuId.Constr.3DSphere":
                case "MenuId.Constr.3DBox":
                case "MenuId.Constr.3DCylinder":
                case "MenuId.Constr.3DCone":
                case "MenuId.Constr.3DTorus":
                case "MenuId.Trim":
                case "MenuId.Trim.Split":
                case "MenuId.Expand":
                case "MenuId.Connect":
                case "MenuId.Cut.Off":
                case "MenuId.CutOffMultiple":
                case "MenuId.Round.Off":
                case "MenuId.Round.In":
                case "MenuId.Aequidist":
                case "MenuId.Solid.RuledSolid":
                case "MenuId.PlaneIntersection":
                case "MenuId.Fillet":
                case "MenuId.Chamfer":
                case "MenuId.Point.IntermediatePoint":
                case "MenuId.Point.ObjectPoint":
                case "MenuId.Point.IntersectionTwoCurves":
                case "MenuId.Point.OffsetByVector":
                case "MenuId.Point.Polar":
                case "MenuId.Length.DistanceOfCurve":
                case "MenuId.Length.DistanceTwoPoints":
                case "MenuId.Length.DistancePointCurve":
                case "MenuId.Length.DistanceTwoCurves":
                case "MenuId.Vector.DirectionOfCurve":
                case "MenuId.Vector.DirectionOfSurface":
                case "MenuId.Vector.DirectionTwoPoints":
                case "MenuId.Measure":
                case "MenuId.RoundMultiple":
                    // für alle Konstruktionsaktionen gilt: es muss wenigstens einen ModelView geben
                    CommandState.Enabled = checkModelExist();
                    break;
                case "MenuId.Constr.Face.ObjectExtrude":
                case "MenuId.Constr.Face.ObjectRotate":
                    CommandState.Enabled = checkModelExist();
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        if (sel.Count > 0)
                            CommandState.Enabled = checkModelExist() && Constr3DPathExtrude.pathTest(sel);
                    }
                    break;
                case "MenuId.Constr.Solid.FaceExtrude":
                    CommandState.Enabled = checkModelExist();
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        if (sel.Count > 0)
                            CommandState.Enabled = checkModelExist() && Constr3DFaceExtrude.faceTestExtrude(sel);
                    }
                    break;
                case "MenuId.Constr.Face.FromObject":
                    CommandState.Enabled = checkModelExist();
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        CommandState.Enabled = checkModelExist() && sel.Count > 0;
                        //if (sel.Count > 0)
                        //{
                        //    Constr3DMakeFace temp = new Constr3DMakeFace();
                        //    GeoObjectList faceGeneric = temp.makeFaceDo(sel, this);
                        //    if (faceGeneric != null) CommandState.Enabled = true;
                        //}
                    }
                    break;
                case "MenuId.Constr.Solid.FaceRotate":
                    // für alle Konstruktionsaktionen gilt: es muss wenigstens einen ModelView geben
                    CommandState.Enabled = checkModelExist();
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        if (sel.Count > 0)
                            CommandState.Enabled = checkModelExist() && Constr3DFaceRotate.faceTestRotate(sel);
                    }
                    break;
                case "MenuId.Constr.Solid.ScrewPath":
                    {
                        CommandState.Enabled = checkModelExist();
                        if (actionStack.ActiveAction is SelectObjectsAction)
                        {
                            GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                            CommandState.Enabled = checkModelExist() && Constr3DScrewPath.canUseList(sel);
                        }
                    }
                    break;
                case "MenuId.Constr.Solid.RuledSolid":
                    // für alle Konstruktionsaktionen gilt: es muss wenigstens einen ModelView geben
                    CommandState.Enabled = checkModelExist();
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        if (sel.Count > 0)
                            CommandState.Enabled = checkModelExist() && Constr3DRuledSolid.ruledSolidTest((actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects(), ActiveView.Model);
                    }
                    break;
                case "MenuId.Constr.Face.RuledFace":
                    // für alle Konstruktionsaktionen gilt: es muss wenigstens einen ModelView geben
                    CommandState.Enabled = checkModelExist();
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        if (sel.Count > 0)
                            CommandState.Enabled = checkModelExist() && Constr3DRuledFace.ruledFaceTest((actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects(), ActiveView.Model);
                    }
                    break;
                case "MenuId.SelectedObjects.SewFaces":
                    CommandState.Enabled = checkModelExist();
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        // überprüfen, ob auf oberster Ebene mehrere face oder shell objekte sind
                        int count = 0;
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        if (sel.Count > 0)
                            for (int i = 0; i < sel.Count; i++)
                            {
                                if (sel[i] is Face || sel[i] is Shell) ++count;
                            }
                        CommandState.Enabled = checkModelExist() && (count > 1); // mindestens zwei
                    }
                    return true;
                case "MenuId.Constr.Solid.Fuse":
                    CommandState.Enabled = checkModelExist();
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        // überprüfen, ob auf oberster Ebene mehrere face oder shell objekte sind
                        int count = 0;
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        if (sel.Count > 0)
                            for (int i = 0; i < sel.Count; i++)
                            {
                                if (sel[i] is Solid) ++count;
                            }
                        CommandState.Enabled = checkModelExist() && (count > 1); // mindestens zwei
                    }
                    return true;
                case "MenuId.Constr.Solid.ShellToSolid":
                    CommandState.Enabled = checkModelExist();
                    if (actionStack.ActiveAction is SelectObjectsAction)
                    {
                        // überprüfen, ob auf oberster Ebene mehrere face oder shell objekte sind
                        int count = 0;
                        GeoObjectList sel = (actionStack.ActiveAction as SelectObjectsAction).GetSelectedObjects();
                        if (sel.Count > 0)
                        {
                            for (int i = 0; i < sel.Count; i++)
                            {
                                if (sel[i] is Shell && (sel[i] as Shell).OpenEdges.Length == 0) ++count;
                            }
                        }
                        CommandState.Enabled = checkModelExist() && (count > 0);
                    }
                    return true;
                case "MenuId.Import.Dxf2D":
                case "MenuId.Import.Drw":
                    //CommandState.Enabled = CADability.ImportCondor4.Condor4Installed;
                    break;
                case "MenuId.Constr.Solid.ModifyMode.Unite":
                    CommandState.Checked = (solidModellingMode == Make3D.SolidModellingMode.unite);
                    break;
                case "MenuId.Constr.Solid.ModifyMode.Subtract":
                    CommandState.Checked = (solidModellingMode == Make3D.SolidModellingMode.subtract);
                    break;
                case "MenuId.ViewPoint":
                    CommandState.Checked = actionStack.ActiveAction is ViewPointAction;
                    return true;
                case "MenuId.ViewFixPoint":
                    CommandState.Checked = actionStack.ActiveAction is ViewFixPointAction;
                    return true;
                case "MenuId.Scroll":
                    CommandState.Checked = actionStack.ActiveAction is ScrollAction;
                    return true;
                case "MenuId.Zoom":
                    CommandState.Checked = actionStack.ActiveAction is ZoomPlusMinusAction;
                    return true;
                case "MenuId.ZAxisUp":
                    if (ActiveView is ModelView)
                    {
                        CommandState.Checked = (ActiveView as ModelView).ZAxisUp;
                    }
                    else
                    {
                        CommandState.Enabled = false;
                    }
                    return true;
                case "MenuId.Projection.Direction.FromTop":
                    if (ActiveView is ModelView)
                    {
                        CommandState.Checked = Precision.SameNotOppositeDirection((ActiveView as ModelView).Projection.Direction, -GeoVector.ZAxis);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromFront":
                    if (ActiveView is ModelView)
                    {
                        CommandState.Checked = Precision.SameNotOppositeDirection((ActiveView as ModelView).Projection.Direction, GeoVector.YAxis);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromBack":
                    if (ActiveView is ModelView)
                    {
                        CommandState.Checked = Precision.SameNotOppositeDirection((ActiveView as ModelView).Projection.Direction, -GeoVector.YAxis);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromLeft":
                    if (ActiveView is ModelView)
                    {
                        CommandState.Checked = Precision.SameNotOppositeDirection((ActiveView as ModelView).Projection.Direction, GeoVector.XAxis);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromRight":
                    if (ActiveView is ModelView)
                    {
                        CommandState.Checked = Precision.SameNotOppositeDirection((ActiveView as ModelView).Projection.Direction, -GeoVector.XAxis);
                    }
                    return true;
                case "MenuId.Projection.Direction.FromBottom":
                    if (ActiveView is ModelView)
                    {
                        CommandState.Checked = Precision.SameNotOppositeDirection((ActiveView as ModelView).Projection.Direction, GeoVector.ZAxis);
                    }
                    return true;
                case "MenuId.Projection.Direction.Isometric":
                    if (ActiveView is ModelView)
                    {
                        GeoVector iso = new GeoVector(-1, -1, -1);
                        iso.Norm();
                        CommandState.Checked = Precision.SameNotOppositeDirection((ActiveView as ModelView).Projection.Direction, iso);
                    }
                    return true;

                default: break;
            }
            // forward command handling to the project
            if (project != null && (project as ICommandHandler).OnUpdateCommand(MenuId, CommandState)) return true;
            // forward command handling to the actions
            if (actionStack.OnUpdateCommand(MenuId, CommandState)) return true;
            // forward command handling to the active view
            if ((ActiveView is ICommandHandler) && (ActiveView as ICommandHandler).OnUpdateCommand(MenuId, CommandState)) return true;
            return false; // could not handle this command
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
#endregion

        private void OnProjectViewChanged(Project sender, IView viewWhichChanged)
        {
            string oldname = null;
#if !WEBASSEMBLY
            if (viewWhichChanged is LayoutView)
            {
                LayoutView lv = viewWhichChanged as LayoutView;
                foreach (KeyValuePair<string, LayoutView> kv in layoutViews)
                {
                    if (kv.Value == lv)
                    {
                        oldname = kv.Key;
                        break;
                    }
                }
                if (oldname != null)
                {
                    layoutViews.Remove(oldname);
                }
                layoutViews[lv.Layout.Name] = lv;
            }
#endif
            if (viewWhichChanged is ModelView)
            {
                ModelView mv = viewWhichChanged as ModelView;
                foreach (KeyValuePair<string, ModelView> kv in modelViews)
                {
                    if (kv.Value == mv)
                    {
                        oldname = kv.Key;
                        break;
                    }
                }
                if (oldname != null)
                {
                    modelViews.Remove(oldname);
                }
                modelViews[mv.ProjectedModel.Name] = mv;
            }
            if (viewWhichChanged == null)
            {   // einer wurde entfernt
#if !WEBASSEMBLY
                foreach (KeyValuePair<string, LayoutView> kv in layoutViews)
                {
                    if (!project.FindLayoutName(kv.Key))
                    {
                        layoutViews.Remove(kv.Key);
                        break;
                    }
                }
#endif
                foreach (KeyValuePair<string, ModelView> kv in modelViews)
                {
                    if (!project.FindModelViewName(kv.Key))
                    {
                        modelViews.Remove(kv.Key);
                        break;
                    }
                }
            }
            this.UpdateViewMenu(true);
            this.SetViewProperties();
        }
        private void UpdateViewMenu(bool createViews)
        {
            project.AssureViews(); // to make sure there is at least one ModelView and one LayoutView
                                   // TODO: update the main menu: invoke an event which tells the application that the main menue changed
                                   // because the View Menu contains the names of the views
                                   //MainMenu mm = (this as IFrameInternal).FindMainMenu();
                                   //if (mm != null)
                                   //{
                                   //    int dbg = MenuResource.RemoveMenuItems(mm, "MenuId.View.Model.ModelView.");
                                   //    dbg = MenuResource.RemoveMenuItems(mm, "MenuId.View.Layout.LayoutView.");
                                   //    project.AssureViews();
                                   //    for (int i = 0; i < project.LayoutCount; ++i)
                                   //    {
                                   //        Layout l = project.GetLayout(i);
                                   //        MenuResource.AppendMenuItem(mm, "MenuId.View.Layout", "MenuId.View.Layout.LayoutView." + l.Name, l.Name);
                                   //    }
                                   //    for (int i = 0; i < project.ModelViewCount; ++i)
                                   //    {
                                   //        string name = project.GetModelViewName(i);
                                   //        MenuResource.AppendMenuItem(mm, "MenuId.View.Model", "MenuId.View.Model.ModelView." + name, name);
                                   //    }
                                   //}

            if (createViews)
            {
                // Alle Views erzeugen, aber noch nicht anzeigen
#if !WEBASSEMBLY
                for (int i = 0; i < project.LayoutCount; ++i)
                {
                    Layout l = project.GetLayout(i);
                    if (!layoutViews.ContainsKey(l.Name))
                    {
                        LayoutView lv = new LayoutView(l, project);
                        layoutViews[l.Name] = lv;
                    }
                }
#endif
                for (int i = 0; i < project.ModelViewCount; ++i)
                {
                    ProjectedModel pm = project.GetProjectedModel(i);
                    if (!modelViews.ContainsKey(pm.Name))
                    {
                        ModelView mv = new ModelView(project);
                        mv.ProjectedModel = pm;
                        modelViews[pm.Name] = mv;
                    }
                }
                animatedViews = new List<AnimatedView>(project.AnimatedViews);
#if !WEBASSEMBLY
                gdiViews = new List<GDI2DView>(project.GdiViews);
#endif

            }
        }
        private void SetViewProperties()
        {
            if (viewProperties == null) return;
            viewProperties.Clear();
            List<IView> views = new List<IView>();
            foreach (ModelView mv in modelViews.Values)
            {
                views.Add(mv);
            }
#if !WEBASSEMBLY
            foreach (LayoutView lv in layoutViews.Values)
            {
                views.Add(lv);
            }
            foreach (GDI2DView gv in gdiViews)
            {
                views.Add(gv);
            }
#endif
            foreach (AnimatedView av in animatedViews)
            {
                views.Add(av);
            }
            viewProperties.Add(new MultiViewProperty(views.ToArray(), this), true);
            bool showScrollBars = Settings.GlobalSettings.GetBoolValue("ShowScrollBars", true);
        }
        private ModelView FirstModelView
        {
            get
            {
                Dictionary<string, ModelView>.ValueCollection.Enumerator e = modelViews.Values.GetEnumerator();
                e.MoveNext();
                return e.Current;
            }
        }
#if !WEBASSEMBLY
        private LayoutView FirstLayoutView
        {
            get
            {
                Dictionary<string, LayoutView>.ValueCollection.Enumerator e = layoutViews.Values.GetEnumerator();
                e.MoveNext();
                return e.Current;
            }
        }
#endif
        private void project_RefreshEvent(object sender, EventArgs args)
        {
            // TODO: somehow inform the derived class to update the window
        }
        /// <summary>
        /// Refreshes the properties in the propertypage
        /// </summary>
        public void SetProjectProperties()
        {
            if (projectProperties != null)
            {
                projectProperties.Clear();
                projectProperties.Add(project, true);
            }
        }
        public delegate void FileNameChangedDelegate(string NewProjectName);
        public event FileNameChangedDelegate FileNameChangedEvent;
        /// <summary>
        /// Find a View with the provided name.
        /// </summary>
        /// <param name="name">name of the View</param>
        /// <returns>The View, if found, null otherwise</returns>
        public IView FindView(string name)
        {
            foreach (IView vw in modelViews.Values)
            {
                if (vw.Name == project.ActiveViewName)
                {
                    return vw;
                }
            }
#if !WEBASSEMBLY
            foreach (IView vw in layoutViews.Values)
            {
                if (vw.Name == project.ActiveViewName)
                {
                    return vw;
                }
            }
            foreach (IView vw in gdiViews)
            {
                if (vw.Name == project.ActiveViewName)
                {
                    return vw;
                }
            }
#endif
            foreach (IView vw in animatedViews)
            {
                if (vw.Name == project.ActiveViewName)
                {
                    return vw;
                }
            }
            return null;
        }
        private static int lastFileType = 1;
        private void OnFileOpen(string fileName)
        {
            if (fileName == null)
            {
                string filter = StringTable.GetString("File.CADability.Filter") + "|" +
                    StringTable.GetString("File.Dxf.Filter") + "|" +
                    StringTable.GetString("File.Dwg.Filter") + "|" +
                    StringTable.GetString("File.STEP.Filter");
                int filterIndex = lastFileType;
                if (UIService.ShowOpenFileDlg("MenuId.File.Open", StringTable.GetString("MenuId.File.Open"), filter, ref filterIndex, out fileName) == Substitutes.DialogResult.OK)
                {
                    //Application.DoEvents();
                    //System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
                    Project newproject = null;
                    string filetype = "";
                    switch (filterIndex)
                    {
                        case 1: filetype = "cdb"; break;
                        case 2: filetype = "dxf"; break;
                        case 3: filetype = "dwg"; break;
                        case 4: filetype = "stp"; break;
                    }
                    lastFileType = filterIndex;
                    newproject = Project.ReadFromFile(fileName, filetype);
                    if (newproject != null)
                    {
                        Project = newproject;
                        if (filterIndex != 1)
                        {   // if it was no a CADability file, zoom to extent
                            ModelView mv = FirstModelView;
                            // and show the first NodelView
                            if (mv != null)
                            {
                                int tc0 = System.Environment.TickCount;
                                mv.ZoomTotal(1.1);
                                int tc1 = System.Environment.TickCount - tc0;
                                System.Diagnostics.Trace.WriteLine("Zoom Total: " + tc1.ToString());
                            }
                        }
                        MRUFiles.AddPath(fileName, filetype);
                        UpdateMRUMenu(MRUFiles.GetMRUFiles());
                    }
                }
            }
            else
            {
                try
                {
                    string fileext = "cdb";
                    if (fileName.Contains(";"))
                    {
                        string[] spl = fileName.Split(';');
                        if (spl.Length == 2)
                        {
                            fileName = spl[0];
                            fileext = spl[1];
                        }
                    }
                    //Application.DoEvents();
                    // System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor; don't want to use windows forms
                    Project newproject = Project.ReadFromFile(fileName, fileext);
                    if (newproject == null) return;
                    Project = newproject;
                    FileNameChangedEvent?.Invoke(fileName);
                    if (fileext != "cdb")
                    {
                        // get a raw model extent to know a precision for the triangulation
                        // parallel triangulate all faces with this precision to show a progress bar
                        // then zoom total 
                        ModelView mv = FirstModelView;
                        // display the first ModelView
                        if (mv != null)
                        {
                            Projection fromTop = Projection.FromTop;
                            BoundingRect ext = mv.Model.GetExtent(fromTop);
                            // System.Diagnostics.Trace.WriteLine("Starting ParallelTriangulation " + Environment.TickCount.ToString());
                            fromTop.SetPlacement(mv.DisplayRectangle, ext);
                            double precision = 1.0 / fromTop.WorldToDeviceFactor;
                            mv.Model.SerialTriangulation(precision); // for easier Debugging
                            // mv.Model.ParallelTriangulation(precision);
                            // System.Diagnostics.Trace.WriteLine("Starting OctTree " + Environment.TickCount.ToString());
                            mv.Model.InitOctTree();
                            // System.Diagnostics.Trace.WriteLine("OctTree done " + Environment.TickCount.ToString());
                            mv.ZoomTotal(1.1);
                            // System.Diagnostics.Trace.WriteLine("ZoomTotal done " + Environment.TickCount.ToString());
                            // mv.ZoomToModelExtent(condorScrollableCtrls[activeControl].ClientRectangle, 1.1);
                        }
                    }
                }
                catch (FileNotFoundException)
                {
                    UIService.ShowMessageBox(StringTable.GetFormattedString("Error.FileNotFound", fileName), StringTable.GetString("Errormessage.Import"), MessageBoxButtons.OK);
                }
            }
            // since a project may use alot of memory, this is a good place to free that memory, which was used by the previous project
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }
        /// <summary>
        /// Generates a new project, saves the current project if necessary and sets the
        /// new project as the current project.
        /// </summary>
        /// <returns></returns>
        public bool GenerateNewProject()
        {
            if (project != null)
            {
                if (!project.SaveModified()) return true; // saving was aborted
                ProjectClosedEvent?.Invoke(project, this);
            }

            Project newproject = Project.CreateSimpleProject();
            Project = newproject;
            ModelView mv = FirstModelView;
            if (mv != null)
            {
                mv.ZoomTotal(1.1);
            }

            return true;
        }
        private void OnAppAbout()
        {   // don't want to use windows forms
            //Form about = new Form();
            //WebBrowser wb = new WebBrowser();
            //Assembly ThisAssembly = Assembly.GetExecutingAssembly();
            //System.IO.Stream str = ThisAssembly.GetManifestResourceStream("CADability.About.html");
            //wb.DocumentStream = str;
            //about.Controls.Add(wb);
            //wb.Dock = DockStyle.Fill;
            //wb.Navigating += new WebBrowserNavigatingEventHandler(AboutNavigating);
            //AssemblyName an = ThisAssembly.GetName();
            //about.Text = "About CADability, Version: " + an.Version.Major.ToString().ToString() + "." + an.Version.Minor.ToString() + "." + an.Version.Build.ToString() + "." + an.Version.MinorRevision.ToString();
            //about.Location = this.frameControl.PointToScreen(this.frameControl.Location);
            //about.Size = this.frameControl.Size;
            //// about.DesktopBounds
            //about.Show();

        }
        private void Export()
        {
            string filter =
                StringTable.GetString("File.Dxf.Filter") + "|" +
                StringTable.GetString("File.STEP.Filter") + "|" +
                StringTable.GetString("File.STL.Filter") + "|" +
                StringTable.GetString("File.Html.Filter");
            int filterIndex = lastFileType;
            string filename = null;
            if (UIService.ShowSaveFileDlg("MenuId.Export", StringTable.GetString("MenuId.Export"), filter, ref filterIndex, ref filename) == Substitutes.DialogResult.OK)
            {
                string[] extensions = filter.Split('|');
                string ext = extensions[filterIndex * 2 - 1];
                ext = ext.Substring(2);
                if (ext.IndexOf(';') > 0)
                {
                    ext = ext.Substring(0, ext.IndexOf(';'));
                }
                Project.Export(filename, ext);
            }
        }
        private void OnFileImport()
        {

            string filter = StringTable.GetString("File.CADability.Filter") + "|" +
                StringTable.GetString("File.Dxf.Filter") + "|" +
                StringTable.GetString("File.Dwg.Filter") + "|" +
                StringTable.GetString("File.STEP.Filter") + "|" +
                StringTable.GetString("File.STL.Filter");
            int filterIndex = lastFileType;
            if (UIService.ShowOpenFileDlg("MenuId.Import", StringTable.GetString("MenuId.Import"), filter, ref filterIndex, out string fileName) == Substitutes.DialogResult.OK)
            {
                //Application.DoEvents();
                //System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
                Project newproject = null;
                switch (filterIndex)
                {
                    case 1: newproject = CADability.Project.ReadFromFile(fileName, "cdb"); break;
                    case 2: newproject = CADability.Project.ReadFromFile(fileName, "dxf"); break;
                    case 3: newproject = CADability.Project.ReadFromFile(fileName, "dwg"); break;
                    case 4: newproject = CADability.Project.ReadFromFile(fileName, "stp"); break;
                    case 5:
                        {
                            ImportSTL importSTL = new ImportSTL();
                            Shell[] shells = importSTL.Read(fileName);
                            if (shells!=null)
                            {
                                newproject = CADability.Project.CreateSimpleProject();
                                Model model = newproject.GetModel(0);
                                for (int i = 0; i < shells.Length; i++)
                                {
                                    newproject.SetDefaults(shells[i]);
                                    if (shells[i].HasOpenEdgesExceptPoles())
                                    {
                                        model.Add(shells[i]);
                                    }
                                    else
                                    {
                                        Solid sld = Solid.Construct();
                                        sld.SetShell(shells[i]);
                                        newproject.SetDefaults(sld);
                                        model.Add(sld);
                                    }
                                }
                            }
                        }
                        break;
                }
                lastFileType = filterIndex;
                if (newproject != null)
                {
                    if (newproject.GetModelCount() == 1)
                    {
                        Model addInto = null;
                        if (ActiveView is ModelView)
                        {
                            addInto = (ActiveView as ModelView).Model;
                        }
                        else
                        {
                            addInto = project.GetActiveModel();
                        }
                        foreach (IGeoObject go in newproject.GetModel(0).AllObjects)
                        {
                            go.UpdateAttributes(project);
                        }
                        addInto.Import(newproject.GetModel(0).AllObjects, fileName);
                    }
                    else if (newproject.GetModelCount() > 1)
                    {
                        using (Project.Undo.UndoFrame)
                        {
                            for (int i = 0; i < newproject.GetModelCount(); ++i)
                            {
                                project.AddModel(newproject.GetModel(i));
                            }
                        }
                    }
                }
            }
        }

#region SnapModes implementation
        private SnapPointFinder.SnapModes snapMode;
        /// <summary>
        /// The snapping mode for mouse movements when interactively constructing objects
        /// </summary>
        public SnapPointFinder.SnapModes SnapMode
        {
            get { return snapMode; }
            set
            {
                snapMode = value;
                Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
            }
        }
        public void OnSnapGrid()
        {
            snapMode ^= SnapPointFinder.SnapModes.SnapToGridPoint;
            Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
        }
        public void OnSnapOrtho()
        {
            snapMode ^= SnapPointFinder.SnapModes.AdjustOrtho;
            Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
        }
        public void OnSnapSnapPoint()
        {
            snapMode ^= SnapPointFinder.SnapModes.SnapToObjectSnapPoint;
            Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
        }
        public void OnSnapObjectPoint()
        {
            snapMode ^= SnapPointFinder.SnapModes.SnapToObjectPoint;
            Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
        }
        public void OnSnapDropPoint()
        {
            snapMode ^= SnapPointFinder.SnapModes.SnapToDropPoint;
            Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
        }
        public void OnSnapCenter()
        {
            snapMode ^= SnapPointFinder.SnapModes.SnapToObjectCenter;
            Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
        }
        public void OnSnapTangentPoint()
        {
            snapMode ^= SnapPointFinder.SnapModes.SnapToTangentPoint;
            Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
        }
        public void OnSnapIntersections()
        {
            snapMode ^= SnapPointFinder.SnapModes.SnapToIntersectionPoint;
            Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
        }
        public void OnSnapSurface()
        {
            snapMode ^= SnapPointFinder.SnapModes.SnapToFaceSurface;
            Settings.GlobalSettings.SetValue("KeepState.SnapMode", (int)snapMode);
        }

#endregion


        public delegate bool DragDropDelegate(DragEventArgs e);
        public delegate GeoObjectList DragGetDataDelegate(DragEventArgs e);
        public event DragDropDelegate FilterDragDrop;
        public event DragDropDelegate FilterDragEnter;
        public event DragDropDelegate FilterDragLeave;
        public event DragDropDelegate FilterDragOver;
        public event DragGetDataDelegate FilterDragGetData;
        void ControlCenterCreated(IControlCenter cc)
        {
        }

    }
}
