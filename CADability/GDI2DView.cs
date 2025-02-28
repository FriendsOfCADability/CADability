#if !WEBASSEMBLY
using CADability.Actions;
using CADability.Attribute;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.Serialization;
using Wintellect.PowerCollections;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;
using PaintEventArgs = CADability.Substitutes.PaintEventArgs;

namespace CADability
{
    [Serializable]
    public class GDI2DView : IShowPropertyImpl, ICommandHandler, IView, IActionInputView, ISerializable, IDeserializationCallback
    {
        private Project project;
        private Model model;
        private string name; // der Name für die Darstellung im ControlCenter
        private Projection projection; // die gerade gülitge Projektion
        private Color? backgroundColor;
        private CheckedLayerList visibleLayers;
        private Color selectColor;
        private int selectWidth;
        private int dragWidth;
        private bool allowDrop;
        private bool allowDrag;
        private bool allowContextMenu;
        private bool useDisplayOrder;
        private BlockRef dragBlock; // ein Symbol wird aus der Bibliothek per DragDrop plaziert
        private bool modelNeedsRepaint;
        private double paperWidth; // Darstellung wie im alten CONDOR weißese Papier mit schwarzem Außengebiet
        private double paperHeight; // Darstellung wie im alten CONDOR weißese Papier mit schwarzem Außengebiet
        private bool showPaper; // Darstellung wie im alten CONDOR weißese Papier mit schwarzem Außengebiet
        private bool showCoordCross; // Darstellung des Koordinatenkreuzes
        private SortedDictionary<int, List<IGeoObject>> sortedGeoObjectsByDisplayOrder;
        private bool deferRefresh; // bei Änderungen nich Darstellen
        // für andere Actionen
        public event PaintView PaintDrawingEvent;
        public event PaintView PaintSelectEvent;
        public event PaintView PaintActiveEvent;
        public event PaintView PaintBackgroundEvent;
        /// <summary>
        /// Provide an event handler for the mouse move message here if you want to manipulate the mouse input to this ModelView
        /// </summary>
        public event MouseFilterDelegate MouseMove;
        /// <summary>
        /// Provide an event handler for the mouse down message here if you want to manipulate the mouse input to this ModelView
        /// </summary>
        public event MouseFilterDelegate MouseDown;
        /// <summary>
        /// Provide an event handler for the mouse up message here if you want to manipulate the mouse input to this ModelView
        /// </summary>
        public event MouseFilterDelegate MouseUp;
        /// <summary>
        /// Provide an event handler for the mouse wheel message here if you want to manipulate the mouse input to this ModelView
        /// </summary>
        public event MouseFilterDelegate MouseWheel;
        /// <summary>
        /// Provide an event handler for the mouse double click message here if you want to manipulate the mouse input to this ModelView
        /// </summary>
        public event MouseFilterDelegate MouseDoubleClick;
        // Zoom und Scroll
        private System.Drawing.Point lastPanPosition;
        private GeoPoint fixPoint;
        private bool fixPointValid;
        private static double mouseWheelZoomFactor;
        private IGeoObject lastSnapObject;
        private SnapPointFinder.DidSnapModes lastSnapMode;
        IPaintTo3DList drawing;

        protected GDI2DView()
        {
            if (Settings.GlobalSettings != null) mouseWheelZoomFactor = Settings.GlobalSettings.GetDoubleValue("MouseWheelZoomFactor", 1.1);
            base.resourceIdInternal = "GDI2DView";
            if (Settings.GlobalSettings != null)
            {
                allowDrag = Settings.GlobalSettings.GetBoolValue("AllowDrag", true);
                allowDrop = Settings.GlobalSettings.GetBoolValue("AllowDrop", true);
                allowContextMenu = Settings.GlobalSettings.GetBoolValue("AllowContextMenu", true);
                paperWidth = Settings.GlobalSettings.GetDoubleValue("GDI2DView.Paper.Width", 0.0);
                paperHeight = Settings.GlobalSettings.GetDoubleValue("GDI2DView.Paper.Height", 0.0);
            }
            showPaper = (paperWidth > 0.0 && paperHeight > 0.0);
            showCoordCross = true;
            deferRefresh = false;
        }
        protected void Init()
        {
            if (Settings.GlobalSettings != null)
            {
                allowDrag = Settings.GlobalSettings.GetBoolValue("AllowDrag", true);
                allowDrop = Settings.GlobalSettings.GetBoolValue("AllowDrop", true);
                allowContextMenu = Settings.GlobalSettings.GetBoolValue("AllowContextMenu", true);
                paperWidth = Settings.GlobalSettings.GetDoubleValue("GDI2DView.Paper.Width", 0.0);
                paperHeight = Settings.GlobalSettings.GetDoubleValue("GDI2DView.Paper.Height", 0.0);
            }
            visibleLayers = new CheckedLayerList(project.LayerList, project.LayerList.ToArray(), "AnimatedView.VisibleLayers");
            visibleLayers.CheckStateChangedEvent += new CheckedLayerList.CheckStateChangedDelegate(OnVisibleLayersChanged);
            if (Frame != null)
            {
                selectColor = Frame.GetColorSetting("Select.SelectColor", Color.Yellow); // die Farbe für die selektierten Objekte
                selectWidth = Frame.GetIntSetting("Select.SelectWidth", 2);
                dragWidth = Frame.GetIntSetting("Select.DragWidth", 5);
            }
            projection = new Projection(Projection.StandardProjection.FromTop);
            projection.SetExtent(model.Extent);
            projection.Precision = model.Extent.Size / 10000; // damit es nicht 0 ist
            int griddisplaymode = Settings.GlobalSettings.GetIntValue("Grid.DisplayMode", 1);
            if (griddisplaymode > 0)
            {
                projection.Grid.DisplayMode = (Grid.Appearance)(griddisplaymode - 1);
                projection.Grid.Show = true;
            }
            else
            {
                projection.Grid.Show = false;
            }
            projection.Grid.XDistance = Settings.GlobalSettings.GetDoubleValue("Grid.XDistance", 10.0);
            projection.Grid.YDistance = Settings.GlobalSettings.GetDoubleValue("Grid.YDistance", 10.0);
            modelNeedsRepaint = false;
        }
        /// <summary>
        /// Creates a new AnimatedView object. In oder to display this view on the screen you need to add this view to a
        /// <see cref="IFrame"/> and set it as the <see cref="IFrame.ActiveView"/>.
        /// </summary>
        /// <param name="project">The project that contains the lists of all schedules (if needed)</param>
        /// <param name="model">The model that is displayed and contains the list of all drives</param>
        /// <param name="frame">The frame which is the context of this view</param>
        public GDI2DView(Project project, Model model, IFrame frame)
            : this()
        {
            this.project = project;
            this.model = model;
            this.Frame = frame;
            this.projection = Projection.FromTop;
            Init(); // nachdem Projekt u.s.w. gesetzt sind
        }
        void OnModelGeoObjectRemoved(IGeoObject go)
        {
            modelNeedsRepaint = true;
            canvas?.Invalidate();
        }
        void OnModelGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            modelNeedsRepaint = true;
            canvas?.Invalidate();
            projection.SetExtent(model.Extent);
            projection.CalcOpenGlMatrix();
        }
        void OnModelGeoObjectAdded(IGeoObject go)
        {
            modelNeedsRepaint = true;
            canvas?.Invalidate();
            projection.SetExtent(model.Extent); // wg. ERSACAD: die Ausdehnung des Modells kann sich geändert haben (vor allem in Z) und das wurde beim Picken nicht berücksichtigt.
            projection.CalcOpenGlMatrix();
        }
        public Color BackgroundColor
        {
            get
            {
                if (!backgroundColor.HasValue)
                {
                    backgroundColor = Color.AliceBlue;
                    if (Frame != null)
                    {
                        backgroundColor = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
                    }
                    else
                    {
                        object s = Settings.GlobalSettings.GetValue("Colors.Background");
                        if (s != null)
                        {
                            if (s.GetType() == typeof(Color)) backgroundColor = (Color)s;
                            if (s is ColorSetting) backgroundColor = (s as ColorSetting).Color;
                        }
                    }
                }
                return backgroundColor.Value;
            }
            set
            {
                backgroundColor = value;
            }
        }
        private void OnVisibleLayersChanged(Layer layer, bool isChecked)
        {
            modelNeedsRepaint = true;
            canvas?.Invalidate();
        }
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
            }
        }
        public double PaperWidth
        {
            get
            {
                return paperWidth;
            }
            set
            {
                paperWidth = value;
                (this as IView).InvalidateAll();
            }
        }
        public double PaperHeight
        {
            get
            {
                return paperHeight;
            }
            set
            {
                paperHeight = value;
                (this as IView).InvalidateAll();
            }
        }
        public bool ShowPaper
        {
            get
            {
                return showPaper;
            }
            set
            {
                showPaper = value;
                (this as IView).InvalidateAll();
            }
        }
        public bool ShowCoordCross
        {
            get
            {
                return showCoordCross;
            }
            set
            {
                showCoordCross = value;
            }
        }
        public bool UseDisplayOrder
        {
            get
            {
                return useDisplayOrder;
            }
            set
            {
                useDisplayOrder = value;
                modelNeedsRepaint = true;
                (this as IView).InvalidateAll();
            }
        }
        bool thinLinesOnly;
        /// <summary>
        /// Use only thin lines for display
        /// </summary>
        public bool ThinLinesOnly
        {
            get
            {
                return thinLinesOnly;
            }
            set
            {
                thinLinesOnly = value;
            }
        }
        public void PrintSinglePage(PrintDocument pd)
        {
            PrintToGDI pdg = new PrintToGDI(this);
            pd.PrintPage += new PrintPageEventHandler(pdg.OnPrintPage);
            try
            {
                pd.Print();
            }
            catch (Exception)
            {
            }
            pd.PrintPage -= new PrintPageEventHandler(pdg.OnPrintPage);
        }
        public Model Model
        {
            get
            {
                return model;
            }
            set
            {
                model = value;
                modelNeedsRepaint = true;
                (this as IView).InvalidateAll();
            }
        }
        public CheckedLayerList VisibleLayers
        {
            get
            {
                return visibleLayers;
            }
        }
        /// <summary>
        /// If set to true, any updates to the model will be ignored until set to false
        /// </summary>
        public bool DeferRefresh
        {
            get
            {
                return deferRefresh;
            }
            set
            {
                deferRefresh = value;
                if (!deferRefresh) (this as IView).InvalidateAll();
            }
        }
        #region IShowProperty implementation
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.Editable | ShowPropertyLabelFlags.ContextMenu;
                if (Frame != null && Frame.ActiveView == this)
                {
                    res |= ShowPropertyLabelFlags.Bold;
                }
                return res;
            }
        }
        public override string LabelText
        {
            get
            {
                if (name != null) return name;
                return base.LabelText;
            }
            set
            {
                // noch checken ob erlaubt....
                name = value;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.LabelChanged (string)"/>
        /// </summary>
        /// <param name="NewText"></param>
        public override void LabelChanged(string NewText)
        {
            if (NewText == this.Name) return;
            for (int i = 0; i < Frame.Project.GdiViews.Count; i++)
            {
                if (Frame.Project.GdiViews[i].Name == NewText)
                {
                    return; // Name schon vorhanden
                }
            }
            this.name = NewText;
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.GDI2DView", false, this);
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.EntryType"/>, 
        /// returns <see cref="ShowPropertyEntryType.GroupTitle"/>.
        /// </summary>
        public override ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.GroupTitle;
            }
        }
        IShowProperty[] subEntries;
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntriesCount"/>, 
        /// returns the number of subentries in this property view.
        /// </summary>
        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (subEntries == null)
                {	// man könnte auch die Events bei Added setzen und bei Removed wieder entfernen ...
                    ShowPropertyGroup spg = new ShowPropertyGroup("GDI2DView.PaperSettings");
                    DoubleProperty paperWidth = new DoubleProperty(this, "PaperWidth", "GDI2DView.PaperSettings.PaperWidth", Frame);
                    DoubleProperty paperHeight = new DoubleProperty(this, "PaperHeight", "GDI2DView.PaperSettings.PaperHeight", Frame);
                    BooleanProperty showPaper = new BooleanProperty(this, "ShowPaper", "GDI2DView.PaperSettings.ShowPaper", "GDI2DView.PaperSettings.ShowPaperValues");
                    BooleanProperty useZOrder = new BooleanProperty(this, "UseDisplayOrder", "GDI2DView.UseDisplayOrder", "GDI2DView.UseDisplayOrderValues");
                    spg.AddSubEntries(showPaper, paperWidth, paperHeight, useZOrder);
                    string[] modelNames = new string[Frame.Project.GetModelCount()];
                    for (int i = 0; i < Frame.Project.GetModelCount(); ++i)
                    {
                        modelNames[i] = Frame.Project.GetModel(i).Name;
                    }
                    string modelName;
                    if (Model != null) modelName = Model.Name;
                    else modelName = Frame.Project.GetActiveModel().Name;
                    MultipleChoiceProperty modelSelection = new MultipleChoiceProperty("ModelSelection", modelNames, modelName);
                    modelSelection.ValueChangedEvent += new ValueChangedDelegate(ModelSelectionChanged);
                    subEntries = new IShowProperty[] { modelSelection, visibleLayers, projection.Grid, spg };
                }
                return subEntries;
            }
        }
        private void ModelSelectionChanged(object sender, object NewValue)
        {
            Model m = project.FindModel(NewValue as String);
            if (m == null) return;
            Model = m; // das macht schon alles
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Added (IPropertyTreeView)"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Added(IPropertyPage propertyTreeView)
        {
            base.Added(propertyTreeView);
            selectColor = Frame.GetColorSetting("Select.SelectColor", Color.Yellow); // die Farbe für die selektierten Objekte
            selectWidth = Frame.GetIntSetting("Select.SelectWidth", 2);
            dragWidth = Frame.GetIntSetting("Select.DragWidth", 5);
            project.ModelsChangedEvent += new CADability.Project.ModelsChangedDelegate(OnModelsChanged);
            Frame.SettingChangedEvent += new SettingChangedDelegate(OnSettingChanged);
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Removed (IPropertyTreeView)"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Removed(IPropertyTreeView propertyTreeView)
        {
            project.ModelsChangedEvent -= new CADability.Project.ModelsChangedDelegate(OnModelsChanged);
            Frame.SettingChangedEvent -= new SettingChangedDelegate(OnSettingChanged);
            base.Removed(propertyTreeView);
        }
        private void OnModelsChanged(Project sender, Model model, bool added)
        {
            subEntries = null;
            if (propertyTreeView != null)
            {
                propertyTreeView.Refresh(this);
            }
        }
        #endregion
        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.GDI2DView.Show":
                    base.Frame.ActiveView = this;
                    return true;
                case "MenuId.GDI2DView.Rename":
                    propertyTreeView.StartEditLabel(this);
                    return true;
                case "MenuId.GDI2DView.Remove":
                    project.GdiViews.Remove(this);
                    if (propertyTreeView != null)
                    {
                        propertyTreeView.Refresh(propertyTreeView.GetParent(this));
                    }
                    return true;
                case "MenuId.GDI2DView.Print":
                    if (project.printDocument == null) project.printDocument = new PrintDocument();
                    PrintSinglePage(project.printDocument);
                    return true;
                case "MenuId.Zoom.Total":
                    ZoomToModelExtent(1.1);
                    return true;
                // die folgenden beiden gibts z.Z. nur bei Mauell:
                case "MenuId.GDI2DView.CenterVertical":
                    PositionObjects.AlignPageVCenter(Frame.SelectedObjects, paperHeight, Frame.Project);
                    return true;
                case "MenuId.GDI2DView.CenterHorizontal":
                    PositionObjects.AlignPageHCenter(Frame.SelectedObjects, paperWidth, Frame.Project);
                    return true;
            }
            return false;
        }

        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.GDI2DView.Show":
                    return true;
                case "MenuId.GDI2DView.Rename":
                    return true;
                case "MenuId.GDI2DView.Remove":
                    return true;
                case "MenuId.GDI2DView.CenterVertical":
                case "MenuId.GDI2DView.CenterHorizontal":
                case "MenuId.GDI2DView.Print":
                    CommandState.Enabled = (paperHeight > 0.0 && paperWidth > 0.0);
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
        #region ICondorViewInternal Members
        public Substitutes.DragDropEffects DoDragDrop(GeoObjectList dragList, Substitutes.DragDropEffects all)
        {
            return DragDropEffects.None;
        }
        private ICanvas canvas;
        void IView.Connect(ICanvas canvas)
        {
            this.canvas = canvas;
        }
        void IView.Disconnect(ICanvas canvas)
        {
        }

        GeoObjectList IView.GetDataPresent(object data)
        {
            return Frame.UIService.GetDataPresent(data);
        }

        DragDropEffects IView.DoDragDrop(GeoObjectList dragList, DragDropEffects all)
        {
            return canvas.DoDragDrop(dragList, all);
        }
        void IView.OnPaint(PaintEventArgs e)
        {
            IPaintTo3D paintToGDI = canvas.PaintTo3D;
            if (modelNeedsRepaint && !deferRefresh)
            {
                //Graphics g = Graphics.FromImage(drawing);
                //PaintToGDI paintToGDI = new PaintToGDI(projection, g);
                //paintToGDI.ThinLinesOnly = thinLinesOnly;
                paintToGDI.OpenList();
                if (showPaper)
                {
                    //g.Clear(Color.Black); // eine Farbe für außerhalb des papiers in GlobalSettings gibt es noch nicht
                    paintToGDI.Clear(Color.Black);
                    paintToGDI.SetColor(BackgroundColor);
                    PointF p1 = this.projection.ProjectF(GeoPoint.Origin);
                    PointF p2 = this.projection.ProjectF(new GeoPoint(paperWidth, paperHeight, 0.0));
                    paintToGDI.FillRect2D(p1, p2);
                }
                else
                {
                    paintToGDI.Clear(BackgroundColor);
                }
                (paintToGDI as IPaintTo3D).AvoidColor(BackgroundColor);
                bool backgroundOnTop = Settings.GlobalSettings.GetBoolValue("GDI2DView.BackgroundOnTop", false);
                if (!backgroundOnTop) PaintBackground(paintToGDI);
                if (useDisplayOrder)
                {   // neue Liste machen, also gleichzeitig alte löschen, wenn vorhanden
                    sortedGeoObjectsByDisplayOrder = new SortedDictionary<int, List<IGeoObject>>();
                }
                foreach (IGeoObject go in model)
                {
                    PaintRecursive(go, paintToGDI);
                }
                modelNeedsRepaint = false;
                if (useDisplayOrder)
                {
                    foreach (List<IGeoObject> lgo in sortedGeoObjectsByDisplayOrder.Values)
                    {
                        foreach (IGeoObject go in lgo)
                        {
                            go.PaintTo3D(paintToGDI);
                        }
                    }
                }
                if (backgroundOnTop) PaintBackground(paintToGDI);
                drawing = paintToGDI.CloseList();
                //g.Dispose();
            }
            if (drawing != null) paintToGDI.List(drawing);
            // e.Graphics.DrawImageUnscaled(drawing, 0, 0);
            if (PaintBackgroundEvent != null)
            {
                PaintBackgroundEvent(e.ClipRectangle, this, paintToGDI);
                //System.Drawing.Bitmap bmpBackground = new System.Drawing.Bitmap(canvas.ClientRectangle.Width, canvas.ClientRectangle.Height);
                //Graphics g = Graphics.FromImage(bmpBackground);
                //g.Clear(Color.FromArgb(0, 0, 0, 0));
                //PaintToGDI paintToSelect = new PaintToGDI(projection, g);
                //paintToSelect.ThinLinesOnly = thinLinesOnly;
                //PaintBackgroundEvent(e.ClipRectangle, this, paintToSelect);
                //g.Dispose();
                //e.Graphics.DrawImageUnscaled(bmpBackground, 0, 0);
                //bmpBackground.Dispose();
            }
            if (PaintSelectEvent != null)
            {
                PaintSelectEvent(e.ClipRectangle, this, paintToGDI);
                //System.Drawing.Bitmap bmpSelect = new System.Drawing.Bitmap(canvas.ClientRectangle.Width, canvas.ClientRectangle.Height);
                //Graphics g = Graphics.FromImage(bmpSelect);
                //g.Clear(Color.FromArgb(0, 0, 0, 0));
                //PaintToGDI paintToSelect = new PaintToGDI(projection, g);
                //paintToSelect.ThinLinesOnly = thinLinesOnly;
                //PaintSelectEvent(e.ClipRectangle, this, paintToSelect);
                //g.Dispose();
                //e.Graphics.DrawImageUnscaled(bmpSelect, 0, 0);
                //bmpSelect.Dispose();
            }
            if (PaintActiveEvent != null)
            {
                PaintActiveEvent(e.ClipRectangle, this, paintToGDI);
                //System.Drawing.Bitmap bmpActive = new System.Drawing.Bitmap(canvas.ClientRectangle.Width, canvas.ClientRectangle.Height);
                //Graphics g = Graphics.FromImage(bmpActive);
                //g.Clear(Color.FromArgb(0, 0, 0, 0));
                //PaintToGDI paintToSelect = new PaintToGDI(projection, g);
                //paintToSelect.ThinLinesOnly = thinLinesOnly;
                //PaintActiveEvent(e.ClipRectangle, this, paintToSelect);
                //g.Dispose();
                //e.Graphics.DrawImageUnscaled(bmpActive, 0, 0);
                //bmpActive.Dispose();
            }
            if (dragBlock != null)
            {
                //PaintToGDI paintDragBlock = new PaintToGDI(projection, e.Graphics); // direkt zeichnen, nicht mit Bitmap
                //paintDragBlock.ThinLinesOnly = thinLinesOnly;
                dragBlock.PaintTo3D(paintToGDI);
            }
        }
        void IView.OnSizeChanged(Rectangle oldRectangle)
        {
            Size newSize = Size.Empty;
            if (canvas != null) newSize = canvas.ClientRectangle.Size;
            if (newSize.Width == 0 || newSize.Height == 0) return;
            modelNeedsRepaint = true;
        }
        void IView.HScroll(double Position)
        {
            Rectangle d = (this as IView).DisplayRectangle;
            BoundingRect e = GetDisplayExtent();
            if (e.IsEmpty() || d.IsEmpty) return;
            e = e * 1.1; // kommt mehrfach vor, konfigurierbar machen!
            double hPart = d.Width / e.Width; // dieser Anteil wird dargestellt: 1.0: Alles, >1.0 mehr als alles, <1.0 der Anteil
            if (hPart >= 1.0) return; // kein Scrollbereich frei, alles abgedeckt
            double NewLeftPos = d.Left - (e.Width - d.Width) * Position;
            int HScrollOffset = (int)(NewLeftPos - e.Left); // Betrag in Pixel
            if (HScrollOffset == 0) return;
            // if (paintBuffer != null) paintBuffer.HScroll(HScrollOffset); hier könnte man das alte Bitmap noch gebrauchen
            canvas?.Invalidate();
            Scroll(HScrollOffset, 0.0);
            modelNeedsRepaint = true;
        }
        void IView.VScroll(double Position)
        {
            Rectangle d = (this as IView).DisplayRectangle;
            BoundingRect e = GetDisplayExtent();
            if (e.IsEmpty() || d.IsEmpty) return;
            e = e * 1.1; // kommt mehrfach vor, konfigurierbar machen!
            double vPart = d.Height / e.Height;
            if (vPart >= 1.0) return; // kein Scrollbereich frei, alles abgedeckt
            double NewBottomPos = d.Bottom - (d.Height - e.Height) * Position;
            int VScrollOffset = (int)(NewBottomPos - e.Top); // Betrag in Pixel
            if (VScrollOffset == 0) return;
            // if (paintBuffer != null) paintBuffer.VScroll(VScrollOffset);
            canvas?.Invalidate();
            Scroll(0.0, VScrollOffset);
            modelNeedsRepaint = true;
        }
        void IView.ZoomDelta(double f)
        {
            double Factor = f;
            BoundingRect rct = GetVisibleBoundingRect();
            System.Drawing.Rectangle clr = canvas.ClientRectangle;
            System.Drawing.Point p = new System.Drawing.Point((clr.Left + clr.Right) / 2, (clr.Bottom + clr.Top) / 2);
            GeoPoint2D p2 = this.projection.PointWorld2D(p);
            rct.Right = p2.x + (rct.Right - p2.x) * Factor;
            rct.Left = p2.x + (rct.Left - p2.x) * Factor;
            rct.Bottom = p2.y + (rct.Bottom - p2.y) * Factor;
            rct.Top = p2.y + (rct.Top - p2.y) * Factor;
            (this as IView).ZoomToRect(rct);
            //projection.SetPlacement(condorCtrl.ClientRectangle, rct);

            canvas?.Invalidate();
            modelNeedsRepaint = true;
            RecalcScrollPosition();
        }
        void IView.OnMouseDown(MouseEventArgs eIn)
        {
            MouseEventArgs e = eIn;
            if (MouseDown != null) if (MouseDown(this, ref e)) return;
            bool doScroll = e.Button == MouseButtons.Middle;
            if (e.Button == MouseButtons.Left && (Frame.UIService.ModifierKeys & Keys.Control) != 0) doScroll = true;
            if (doScroll)
            {
                lastPanPosition = new System.Drawing.Point(e.X, e.Y);
            }
            Frame.ActiveView = this;
            Frame.ActionStack.OnMouseDown(e, this);
        }

        void IView.OnMouseEnter(EventArgs e)
        {
            Frame.ActionStack.OnMouseEnter(e, this);
        }
        void IView.OnMouseHover(EventArgs e)
        {
            Frame.ActionStack.OnMouseHover(e, this);
        }
        void IView.OnMouseLeave(EventArgs e)
        {
            Frame.ActionStack.OnMouseLeave(e, this);
        }
        void IView.OnMouseMove(MouseEventArgs eIn)
        {
            MouseEventArgs e = eIn;
            if (MouseMove != null) if (MouseMove(this, ref e)) return;
            bool doScroll = e.Button == MouseButtons.Middle;
            bool doDirection = false;
            if (e.Button == MouseButtons.Middle && (Frame.UIService.ModifierKeys & Keys.Control) != 0)
            {
                doScroll = false;
                doDirection = true;
            }
            if (doScroll)
            {
                (this as IView).SetCursor("SizeAll");
                int HScrollOffset = e.X - lastPanPosition.X;
                int VScrollOffset = e.Y - lastPanPosition.Y;
                projection.MovePlacement(HScrollOffset, VScrollOffset);
                modelNeedsRepaint = true;
                canvas?.Invalidate();
                lastPanPosition = new System.Drawing.Point(e.X, e.Y);
                RecalcScrollPosition();
            }
            if (doDirection)
            {
                int HOffset = e.X - lastPanPosition.X;
                int VOffset = e.Y - lastPanPosition.Y;
                projection.SetExtent(model.Extent);
                if (VOffset != 0 || HOffset != 0)
                {
                    //if (Math.Abs(VOffset) > Math.Abs(HOffset)) HOffset = 0;
                    //else VOffset = 0;
                    // bringt keine Vorteile
                    lastPanPosition = new System.Drawing.Point(e.X, e.Y);
                    GeoVector haxis = projection.InverseProjection * GeoVector.XAxis;
                    GeoVector vaxis = projection.InverseProjection * GeoVector.YAxis;
                    ModOp mh = ModOp.Rotate(vaxis, SweepAngle.Deg(HOffset / 5.0));
                    ModOp mv = ModOp.Rotate(haxis, SweepAngle.Deg(VOffset / 5.0));

                    ModOp project = projection.UnscaledProjection * mv * mh;
                    // jetzt noch die Z-Achse einrichten. Die kann senkrecht nach oben oder unten
                    // zeigen. Wenn die Z-Achse genau auf den Betrachter zeigt oder genau von ihm weg,
                    // dann wird keine Anpassung vorgenommen, weils sonst zu sehr wackelt
                    GeoVector z = project * GeoVector.ZAxis;
                    if (true) // (ZAxisUp) gibts in diesem View nicht
                    {
                        const double mindeg = 0.05; // nur etwas aufrichten, aber in jedem Durchlauf
                        if (z.y < -0.1)
                        {   // Z-Achse soll nach unten zeigen
                            Angle a = new Angle(-GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
                            if (a.Radian > mindeg) a.Radian = mindeg;
                            if (a.Radian < -mindeg) a.Radian = -mindeg;
                            if (z.x < 0)
                            {
                                project = project * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
                            }
                            else
                            {
                                project = project * ModOp.Rotate(vaxis ^ haxis, a.Radian);
                            }
                            z = project * GeoVector.ZAxis;
                        }
                        else if (z.y > 0.1)
                        {
                            Angle a = new Angle(GeoVector2D.YAxis, new GeoVector2D(z.x, z.y));
                            if (a.Radian > mindeg) a.Radian = mindeg;
                            if (a.Radian < -mindeg) a.Radian = -mindeg;
                            if (z.x < 0)
                            {
                                project = project * ModOp.Rotate(vaxis ^ haxis, a.Radian);
                            }
                            else
                            {
                                project = project * ModOp.Rotate(vaxis ^ haxis, -a.Radian);
                            }
                            z = project * GeoVector.ZAxis;
                        }
                    }
                    // Fixpunkt bestimmen
                    System.Drawing.Point clcenter = canvas.ClientRectangle.Location;
                    clcenter.X += canvas.ClientRectangle.Width / 2;
                    clcenter.Y += canvas.ClientRectangle.Height / 2;
                    // ium Folgenden ist Temporary auf true zu setzen und ein Mechanismus zu finden
                    // wie der QuadTree von ProjektedModel berechnet werden soll
                    //GeoVector newDirection = mv * mh * Projection.Direction;
                    //GeoVector oldDirection = Projection.Direction;
                    //GeoVector perp = oldDirection ^ newDirection;
                    if (fixPointValid)
                        SetViewDirection(project, fixPoint, true);
                    else
                        SetViewDirection(project, projection.UnProject(clcenter), true);
                    if (Math.Abs(HOffset) > Math.Abs(VOffset))
                    {
                        if (HOffset > 0) (this as IView).SetCursor("PanEast");
                        else (this as IView).SetCursor("PanWest");
                    }
                    else
                    {
                        if (VOffset > 0) (this as IView).SetCursor("PanSouth");
                        else (this as IView).SetCursor("PanNorth");
                    }
                }
                modelNeedsRepaint = true;
                canvas?.Invalidate();
            }
            else
            {
                //if (projectedModelNeedsRecalc)
                //{
                //    projectedModelNeedsRecalc = false;
                //    projectedModel.RecalcAll(false);
                //    RecalcScrollPosition();
                //}
            }
            if (!doScroll && !doDirection)
            {
                //condorCtrl.Frame.ActiveView = this; // activeview soll sich nur bei Klick ändern
                Frame.ActionStack.OnMouseMove(e, this);
            }
        }
        void IView.OnMouseUp(MouseEventArgs eIn)
        {
            MouseEventArgs e = eIn;
            if (MouseUp != null) if (MouseUp(this, ref e)) return;
            if (e.Button == MouseButtons.Middle && (Frame.UIService.ModifierKeys & Keys.Control) != 0 && (Frame.UIService.ModifierKeys & Keys.Shift) != 0)
            {
                GeoPoint wp;
                SnapPointFinder.SnapModes oldSnapMode = Frame.SnapMode;
                Frame.SnapMode = SnapPointFinder.SnapModes.SnapToObjectPoint;
                SnapPointFinder.DidSnapModes dsm = (this as IView).AdjustPoint(e.Location, out wp, null);
                Frame.SnapMode = oldSnapMode;
                if (dsm != SnapPointFinder.DidSnapModes.DidNotSnap)
                {
                    fixPoint = wp;
                    fixPointValid = true;
                }
                else
                {
                    fixPointValid = false;
                }
            }
            Frame.ActiveView = this;
            Frame.ActionStack.OnMouseUp(e, this);
        }
        void IView.OnMouseWheel(MouseEventArgs e)
        {
            if (MouseWheel != null) if (MouseWheel(this, ref e)) return;
            double Factor;
            if (e.Delta > 0) Factor = 1.0 / mouseWheelZoomFactor;
            else if (e.Delta < 0) Factor = mouseWheelZoomFactor;
            else return;
            BoundingRect rct = GetVisibleBoundingRect();
            System.Drawing.Point p = new System.Drawing.Point(e.X, e.Y);
            p = canvas.PointToClient(Frame.UIService.CurrentMousePosition);
            GeoPoint2D p2 = projection.PointWorld2D(p);
            rct.Right = p2.x + (rct.Right - p2.x) * Factor;
            rct.Left = p2.x + (rct.Left - p2.x) * Factor;
            rct.Bottom = p2.y + (rct.Bottom - p2.y) * Factor;
            rct.Top = p2.y + (rct.Top - p2.y) * Factor;
            (this as IView).ZoomToRect(rct);
            // FrameInternal.ActionStack.OnMouseWheel(e,this);
        }
        void IView.OnMouseDoubleClick(MouseEventArgs e)
        {
            if (MouseDoubleClick != null) if (MouseDoubleClick(this, ref e)) return;
        }
        void IView.OnDragDrop(DragEventArgs drgevent)
        {
            if ((drgevent.AllowedEffect & DragDropEffects.Move) > 0 && (drgevent.KeyState & 8) == 0)
                drgevent.Effect = DragDropEffects.Move;
            else
                drgevent.Effect = DragDropEffects.Copy;
            //if (FrameInternal.FilterDragDrop(drgevent)) return;
            if (drgevent.Effect == DragDropEffects.None) return;
            if (dragBlock != null)
            {
                System.Drawing.Point p = canvas.PointToClient(new System.Drawing.Point(drgevent.X, drgevent.Y));
                GeoPoint newRefGP = projection.DrawingPlanePoint(p);
                Block toDrop = dragBlock.ReferencedBlock;
                ModOp mop = ModOp.Translate(newRefGP - toDrop.RefPoint);
                toDrop.Modify(mop);
                for (int i = toDrop.Count - 1; i >= 0; i--)
                {
                    IGeoObject go = toDrop.Child(i);
                    if (go.Style != null && go.Style.Name == "CADability.EdgeStyle")
                    {
                        Layer l = go.Layer;
                        go.Style = Frame.Project.StyleList.Current;
                        // Layer wieder setzen? Ich denke nicht!
                    }
                    AttributeListContainer.UpdateObjectAttrinutes(Frame.Project, go);
                }
                bool move = false;
                if (drgevent.Effect == DragDropEffects.Move)
                {
                    if (toDrop.UserData.Contains("DragGuid") && model.UserData.Contains("DragGuid"))
                    {
                        Guid g1 = (Guid)toDrop.UserData.GetData("DragGuid");
                        Guid g2 = (Guid)model.UserData.GetData("DragGuid");
                        if (g1 == g2)
                        {
                            move = true;
                            model.UserData.Add("DragGuid:" + g1.ToString(), mop); // modop an SelectObjectsAction weitergeben
                        }
                    }
                }
                if (!move)
                {
                    model.Add(toDrop.Children);
                }
                dragBlock = null;
                canvas?.Invalidate();
            }
        }
        void IView.OnDragEnter(DragEventArgs drgevent)
        {
            if ((drgevent.AllowedEffect & DragDropEffects.Move) > 0 && (drgevent.KeyState & 8) == 0)
                drgevent.Effect = DragDropEffects.Move;
            else
                drgevent.Effect = DragDropEffects.Copy;
            //FrameInternal.FilterDragEnter(drgevent); // das boolsche Ergebnis wird nicht verwendet. Ist das OK?
            //if (drgevent.Effect == DragDropEffects.None) return;
            //GeoObjectList importedData = FrameInternal.FilterDragGetData(drgevent);
            //if (importedData == null)
            //{
            GeoObjectList importedData = Frame.UIService.GetDataPresent(drgevent.Data);
            //}
            if (importedData != null)
            {
                Block bl = Block.Construct();

                for (int i = 0; i < importedData.Count; i++)
                    bl.Add(importedData[i]);
                if (importedData.UserData.Contains("DragDownPoint"))
                    bl.RefPoint = (GeoPoint)importedData.UserData.GetData("DragDownPoint");
                else
                {
                    BoundingRect br = importedData.GetExtent(projection, false, false);
                    GeoPoint2D gp = br.GetCenter();
                    bl.RefPoint = new GeoPoint(gp.x, gp.y);
                }
                if (importedData.UserData.Contains("DragGuid"))
                {
                    bl.UserData.Add("DragGuid", importedData.UserData.GetData("DragGuid"));
                }
                dragBlock = BlockRef.Construct(bl);
                // neue Position ausrechnen
                System.Drawing.Point p = canvas.PointToClient(new System.Drawing.Point(drgevent.X, drgevent.Y));
                GeoPoint newRefGP = projection.DrawingPlanePoint(p);

                ModOp mop = ModOp.Translate(newRefGP - dragBlock.RefPoint);
                dragBlock.Modify(mop);
                canvas?.Invalidate();
            }
        }
        void IView.OnDragLeave(EventArgs e)
        {
            //if (FrameInternal.FilterDragLeave(null)) return;
            dragBlock = null;
            canvas?.Invalidate();
        }
        void IView.OnDragOver(DragEventArgs drgevent)
        {
            if ((drgevent.AllowedEffect & DragDropEffects.Move) > 0 && (drgevent.KeyState & 8) == 0)
                drgevent.Effect = DragDropEffects.Move;
            else
                drgevent.Effect = DragDropEffects.Copy;
            //if (FrameInternal.FilterDragOver(drgevent)) return;
            if (dragBlock != null)
            {
                System.Drawing.Point p = canvas.PointToClient(new System.Drawing.Point(drgevent.X, drgevent.Y));
                GeoPoint newRefGP = projection.DrawingPlanePoint(p);
                ModOp mop = ModOp.Translate(newRefGP - dragBlock.RefPoint);
                dragBlock.Modify(mop);
                canvas?.Invalidate();
            }
        }
        private void ShowGrid(IPaintTo3D PaintToBackground, bool displayGrid, bool displayDrawingPlane)
        {
            if (Frame == null) return;
            Color bckgnd = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
            Color clrgrd = Frame.GetColorSetting("Colors.Grid", Color.LightGoldenrodYellow);
            clrgrd = Color.FromArgb(128, clrgrd.R, clrgrd.G, clrgrd.B);
            // Raster darstellen
            Projection pr = this.projection;
            double factor, ddx, ddy;
            pr.GetPlacement(out factor, out ddx, out ddy);
            BoundingCube bc = model.Extent;
            bc.MinMax(model.MinExtend);
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymin, bc.Zmin)));
            ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymin, bc.Zmax)));
            ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymax, bc.Zmin)));
            ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymax, bc.Zmax)));
            ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymin, bc.Zmin)));
            ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymin, bc.Zmax)));
            ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymax, bc.Zmin)));
            ext.MinMax(pr.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymax, bc.Zmax)));
            ext.Inflate(ext.Width, ext.Height);

            // höchstens 200 Einheiten in der jeweiligen Richtung
            double dx = pr.Grid.XDistance;
            double dy = pr.Grid.YDistance;
            while (ext.Width / dx > 200) dx = dx * 2.0;
            while (ext.Height / dy > 200) dy = dy * 2.0;
            int xstart = (int)(ext.Left / dx - 1);
            int xend = (int)(ext.Right / dx + 1);
            int ystart = (int)(ext.Bottom / dy - 1);
            int yend = (int)(ext.Top / dy + 1);
            if (xend - xstart < 250 && yend - ystart < 250 && pr.Grid.Show && displayGrid)
            {
                switch (pr.Grid.DisplayMode)
                {
                    case Grid.Appearance.marks:
                        PaintToBackground.SetColor(clrgrd);
                        PaintToBackground.SetLineWidth(null); // 1.0 würde mit dem faktor multipliziert
                        PaintToBackground.SetLinePattern(null);
                        GeoPoint[] line1 = new GeoPoint[2];
                        GeoPoint[] line2 = new GeoPoint[2];
                        for (int x = xstart; x <= xend; ++x)
                        {
                            for (int y = ystart; y <= yend; ++y)
                            {
                                line1[0] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(x * dx - 3.0 / factor, y * dy));
                                line1[1] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(x * dx + 3.0 / factor, y * dy));
                                line2[0] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(x * dx, y * dy - 3.0 / factor));
                                line2[1] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(x * dx, y * dy + 3.0 / factor));
                                PaintToBackground.Polyline(line1);
                                PaintToBackground.Polyline(line2);
                            }
                        }
                        break;
                    case Grid.Appearance.dots:
                        // das ist nicht sehr optimal, man könnte zunächst auf das Pixelsystem
                        // runterrechnen und dann zeichnen
                        GeoPoint[] points = new GeoPoint[(xend - xstart + 1) * (yend - ystart + 1)];
                        for (int x = xstart; x <= xend; ++x)
                        {
                            for (int y = ystart; y <= yend; ++y)
                            {
                                GeoPoint2D p = new GeoPoint2D(x * dx, y * dy);
                                points[(x - xstart) * (yend - ystart + 1) + y - ystart] = pr.DrawingPlane.ToGlobal(p);
                            }
                        }
                        PaintToBackground.SetColor(clrgrd);
                        if (pr.Grid.DisplayMode == Grid.Appearance.dots)
                        {
                            PaintToBackground.Points(points, 1.0f, PointSymbol.Dot);
                        }
                        else
                        {
                            PaintToBackground.Points(points, 2.0f, PointSymbol.Dot);
                        }
                        break;
                    case Grid.Appearance.lines:
                        PaintToBackground.SetColor(clrgrd);
                        PaintToBackground.SetLineWidth(null); // 1.0 würde mit dem faktor multipliziert
                        PaintToBackground.SetLinePattern(null);
                        GeoPoint[] line = new GeoPoint[2];
                        for (int i = xstart; i <= xend; ++i)
                        {
                            line[0] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(i * dx, ystart * dy));
                            line[1] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(i * dx, yend * dy));
                            PaintToBackground.Polyline(line);
                        }

                        for (int i = ystart; i <= yend; ++i)
                        {
                            line[0] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(xstart * dx, i * dy));
                            line[1] = pr.DrawingPlane.ToGlobal(new GeoPoint2D(xend * dx, i * dy));
                            PaintToBackground.Polyline(line);
                        }
                        break;
                    case Grid.Appearance.fields:
                        // der Hintergrund ist ja bereits gefüllt, die komische
                        // Anfangsbedingung bei y dient dem Versatz der ungeraden Reihen
                        // und dass es in allen Zoomstufen gleich bleibt.
                        PaintToBackground.PushState();
                        PaintToBackground.Blending(true);
                        PaintToBackground.SetColor(clrgrd);
                        for (int x = xstart; x <= xend; ++x)
                        {
                            for (int y = ystart + ((ystart + x) % 2); y <= yend; y += 2)
                            {
                                GeoPoint2D pp1 = new GeoPoint2D(x * dx, y * dy);
                                GeoPoint2D pp2 = new GeoPoint2D((x + 1) * dx, y * dy);
                                GeoPoint2D pp3 = new GeoPoint2D((x + 1) * dx, (y + 1) * dy);
                                GeoPoint2D pp4 = new GeoPoint2D(x * dx, (y + 1) * dy);
                                GeoPoint[] pf = new GeoPoint[4];
                                pf[0] = pr.DrawingPlane.ToGlobal(pp1);
                                pf[1] = pr.DrawingPlane.ToGlobal(pp2);
                                pf[2] = pr.DrawingPlane.ToGlobal(pp3);
                                pf[3] = pr.DrawingPlane.ToGlobal(pp4);
                                GeoVector[] normals = new GeoVector[4];
                                normals[0] = pr.DrawingPlane.Normal;
                                normals[1] = pr.DrawingPlane.Normal;
                                normals[2] = pr.DrawingPlane.Normal;
                                normals[3] = pr.DrawingPlane.Normal;
                                PaintToBackground.Triangle(pf, normals, new int[] { 0, 1, 2, 0, 2, 3 });
                            }
                        }
                        PaintToBackground.PopState();
                        break;
                }
            }
            else
            {   // raster in dieser Ansicht zu fein
            }
            if (pr.ShowDrawingPlane && displayDrawingPlane)
            {
                Color clrdrwpln = Frame.GetColorSetting("Colors.Drawingplane", Color.LightSkyBlue);
                PaintToBackground.PushState();
                PaintToBackground.Blending(true);
                PaintToBackground.SetColor(clrdrwpln);
                GeoPoint2D pp1 = new GeoPoint2D(xstart * dx, ystart * dy);
                GeoPoint2D pp2 = new GeoPoint2D(xend * dx, ystart * dy);
                GeoPoint2D pp3 = new GeoPoint2D(xend * dx, yend * dy);
                GeoPoint2D pp4 = new GeoPoint2D(xstart * dx, yend * dy);
                GeoPoint[] pf = new GeoPoint[4];
                pf[0] = pr.DrawingPlane.ToGlobal(pp1);
                pf[1] = pr.DrawingPlane.ToGlobal(pp2);
                pf[2] = pr.DrawingPlane.ToGlobal(pp3);
                pf[3] = pr.DrawingPlane.ToGlobal(pp4);
                GeoVector[] normals = new GeoVector[4];
                normals[0] = pr.DrawingPlane.Normal;
                normals[1] = pr.DrawingPlane.Normal;
                normals[2] = pr.DrawingPlane.Normal;
                normals[3] = pr.DrawingPlane.Normal;
                PaintToBackground.Triangle(pf, normals, new int[] { 0, 1, 2, 0, 2, 3 });
                PaintToBackground.PopState();
            }
        }
        private void PaintCoordCross(IPaintTo3D PaintToBackground, Projection pr, Color infocolor, Plane plane, bool local)
        {
            GeoPoint2D scr0 = pr.WorldToWindow(GeoPoint.Origin);
            GeoPoint2D scrx = pr.WorldToWindow(GeoPoint.Origin + GeoVector.XAxis);
            GeoPoint2D scry = pr.WorldToWindow(GeoPoint.Origin + GeoVector.YAxis);
            GeoPoint2D scrz = pr.WorldToWindow(GeoPoint.Origin + GeoVector.ZAxis);
            double scale = Math.Max(Math.Max(scrx | scr0, scry | scr0), scrz | scr0);
            double size;
            if (local) size = 13 / scale;
            else size = 20 / scale;
            GeoPoint org = plane.Location;

            PaintToBackground.UseZBuffer(false);
            PaintToBackground.SetColor(infocolor);
            PaintToBackground.SetLineWidth(null);
            PaintToBackground.SetLinePattern(null);
            PaintToBackground.Polyline(new GeoPoint[] { org, org + size * plane.DirectionX });
            PaintToBackground.Polyline(new GeoPoint[] { org, org + size * plane.DirectionY });
            PaintToBackground.Polyline(new GeoPoint[] { org, org + size * plane.Normal });
            GeoPoint p = org + size * plane.DirectionX;
            GeoVector v = plane.DirectionX ^ pr.Direction;
            if (!v.IsNullVector())
            {
                v.Norm();
                PaintTo3D.Arrow1(PaintToBackground, p + size / 4 * v, p - size / 4 * v, p + size / 2 * plane.DirectionX);
            }
            p = org + size * plane.DirectionY;
            v = plane.DirectionY ^ pr.Direction;
            if (!v.IsNullVector())
            {
                v.Norm();
                PaintTo3D.Arrow1(PaintToBackground, p + size / 4 * v, p - size / 4 * v, p + size / 2 * plane.DirectionY);
            }
            p = org + size * plane.Normal;
            v = plane.Normal ^ pr.Direction;
            if (!v.IsNullVector())
            {
                v.Norm();
                PaintTo3D.Arrow1(PaintToBackground, p + size / 4 * v, p - size / 4 * v, p + size / 2 * plane.Normal);
            }
            // if (!local && !pr.IsPerspective)
            {
                double d = size;
                PaintToBackground.PrepareText("Arial", "xyz", FontStyle.Regular);
                PaintToBackground.SetColor(infocolor);
                // die Buchstaben x,y,z am Ende der Achsen in Richtung der ProjectionPlane
                if (!Precision.SameDirection(pr.ProjectionPlane.Normal, plane.DirectionX, true))
                    PaintToBackground.Text(d * pr.ProjectionPlane.DirectionX, d * pr.ProjectionPlane.DirectionY, org + 2 * size * plane.DirectionX, "Arial", "x", FontStyle.Regular, Text.AlignMode.Center, Text.LineAlignMode.Center);
                if (!Precision.SameDirection(pr.ProjectionPlane.Normal, plane.DirectionY, true))
                    PaintToBackground.Text(d * pr.ProjectionPlane.DirectionX, d * pr.ProjectionPlane.DirectionY, org + 2 * size * plane.DirectionY, "Arial", "y", FontStyle.Regular, Text.AlignMode.Center, Text.LineAlignMode.Center);
                if (!Precision.SameDirection(pr.ProjectionPlane.Normal, plane.Normal, true))
                    PaintToBackground.Text(d * pr.ProjectionPlane.DirectionX, d * pr.ProjectionPlane.DirectionY, org + 2 * size * plane.Normal, "Arial", "z", FontStyle.Regular, Text.AlignMode.Center, Text.LineAlignMode.Center);
            }
        }
        private void PaintBackground(IPaintTo3D PaintToBackground)
        {
            CADability.ModelView.BackgroungTaskHandled bth = CADability.ModelView.BackgroungTaskHandled.Nothing;
            // if (PrePaintBackground != null) PrePaintBackground(PaintToBackground, this, out bth);
            Projection pr = this.projection;
            bool displayGrid = (bth & CADability.ModelView.BackgroungTaskHandled.Grid) == 0;
            bool displayDrawingPlane = (bth & CADability.ModelView.BackgroungTaskHandled.DrawingPlane) == 0;
            bool displayCoordCross = (bth & CADability.ModelView.BackgroungTaskHandled.CoordCross) == 0 && showCoordCross;
            ShowGrid(PaintToBackground, displayGrid, displayDrawingPlane);
            Color bckgnd = Color.AliceBlue;
            if (Frame != null)
                bckgnd = Frame.GetColorSetting("Colors.Background", Color.AliceBlue);
            Color infocolor;
            if (bckgnd.GetBrightness() > 0.5) infocolor = System.Drawing.Color.Black;
            else infocolor = System.Drawing.Color.White;
            if (displayCoordCross)
            {
                PaintCoordCross(PaintToBackground, pr, infocolor, Plane.XYPlane, false);
                if (!Precision.IsEqual(pr.DrawingPlane.Location, GeoPoint.Origin))
                {
                    if (bckgnd.GetBrightness() > 0.5) infocolor = System.Drawing.Color.DarkGray;
                    else infocolor = System.Drawing.Color.LightGray;
                    PaintCoordCross(PaintToBackground, pr, infocolor, pr.DrawingPlane, true);
                }
            }
        }
        public event CADability.ScrollPositionChanged ScrollPositionChangedEvent;
        private void Scroll(double dx, double dy)
        {
            projection.MovePlacement(dx, dy);
        }
        public bool AllowDrop
        {
            get
            {
                return allowDrop;
            }
            set
            {
                allowDrop = value;
            }
        }
        public bool AllowDrag
        {
            get
            {
                return allowDrag;
            }
            set
            {
                allowDrag = value;
            }
        }
        public bool AllowContextMenu
        {
            get
            {
                return allowContextMenu;
            }
            set
            {
                allowContextMenu = value;
            }
        }
        private BoundingRect GetVisibleBoundingRect()
        {
            Rectangle clr = canvas.ClientRectangle;
            return projection.BoundingRectWorld2d(clr.Left, clr.Right, clr.Bottom, clr.Top);
        }
        private void SetViewDirection(ModOp project, GeoPoint fixPoint, bool mouseIsDown)
        {
            PointF before = projection.ProjectF(fixPoint);
            projection.SetExtent(model.Extent);
            projection.SetUnscaledProjection(project);
            PointF after = projection.ProjectF(fixPoint);
            projection.MovePlacement(before.X - after.X, before.Y - after.Y);

            canvas?.Invalidate();
        }
        private void PaintRecursive(IGeoObject go, IPaintTo3D paintToGDI)
        {
            if (!go.IsVisible) return; // unsichtbar, nicht ausgeben, gilt auch für Blöcke, oder?
                                       // blöde Abfrage, aber massive Schraffuren sollen direkt (als gefüllter Pfad) gezeichnet werden
            go.PrePaintTo3D(paintToGDI); // von Block abgeleiteten Objekten (Connector z.B.) die Möglichkeit
                                         // geben sich neu zu berechnen
            if (go.NumChildren > 0 && !(go is Hatch && (go as Hatch).HatchStyle is HatchStyleSolid))
            {
                for (int i = 0; i < go.NumChildren; i++)
                {
                    PaintRecursive(go.Child(i), paintToGDI);
                }
            }
            else
            {
                if (visibleLayers == null || go.Layer == null || visibleLayers.IsLayerChecked(go.Layer))
                {
                    if (useDisplayOrder)
                    {
                        int ind = 0;
                        if (go.Layer != null) ind = go.Layer.DisplayOrder;
                        List<IGeoObject> insertInto;
                        if (!sortedGeoObjectsByDisplayOrder.TryGetValue(ind, out insertInto))
                        {
                            insertInto = new List<IGeoObject>();
                            sortedGeoObjectsByDisplayOrder[ind] = insertInto;
                        }
                        insertInto.Add(go);
                    }
                    // hier müsste man das was in PrintToGDI beriets implementiert ist
                    // übernehmen, um die Objekte in richtige 3D Ordnung zu bringen
                    // falls man echtes 3D Überdecken haben möchte
                    else
                    {
                        go.PaintTo3D(paintToGDI);
                    }
                }
            }
        }
        private void OnSettingChanged(string Name, object NewValue)
        {
            if (Name == "Colors.Background" || Name == "Colors.Grid")
            {
                if (Name == "Colors.Background")
                {
                    if (NewValue is ColorSetting)
                    {
                        BackgroundColor = (NewValue as ColorSetting).Color;
                    }
                    if (NewValue is Color)
                    {
                        BackgroundColor = (Color)NewValue;
                    }
                }
                modelNeedsRepaint = true;
                canvas?.Invalidate();
            }
        }
        #endregion
        #region IView Members
        ProjectedModel IView.ProjectedModel
        {   // sollte nicht aufgerufen werden
            get { return null; }
        }
        Model IView.Model
        {
            get { return model; }
        }
        Projection IView.Projection
        {
            get { return projection; }
            set { projection = value.Clone(); }
        }
        ICanvas IView.Canvas
        {
            get { return canvas; }
        }
        string IView.PaintType => "GDI";
        void IView.SetCursor(string cursor)
        {
            (this as IView).Canvas.Cursor = cursor;
        }
        void IView.Invalidate(PaintBuffer.DrawingAspect aspect, System.Drawing.Rectangle ToInvalidate)
        {
            canvas?.Invalidate();
        }
        void IView.InvalidateAll()
        {
            canvas?.Invalidate();
            modelNeedsRepaint = true;
        }
        void IView.SetPaintHandler(PaintBuffer.DrawingAspect aspect, PaintView PaintHandler)
        {
            switch (aspect)
            {
                case PaintBuffer.DrawingAspect.Background: PaintBackgroundEvent += PaintHandler; break;
                case PaintBuffer.DrawingAspect.Drawing: PaintDrawingEvent += PaintHandler; break;
                case PaintBuffer.DrawingAspect.Select: PaintSelectEvent += PaintHandler; break;
                case PaintBuffer.DrawingAspect.Active: PaintActiveEvent += PaintHandler; break;
            }
        }
        void IView.RemovePaintHandler(PaintBuffer.DrawingAspect aspect, PaintView PaintHandler)
        {
            switch (aspect)
            {
                case PaintBuffer.DrawingAspect.Background: PaintBackgroundEvent -= PaintHandler; break;
                case PaintBuffer.DrawingAspect.Drawing: PaintDrawingEvent -= PaintHandler; break;
                case PaintBuffer.DrawingAspect.Select: PaintSelectEvent -= PaintHandler; break;
                case PaintBuffer.DrawingAspect.Active: PaintActiveEvent -= PaintHandler; break;
            }
        }
        System.Drawing.Rectangle IView.DisplayRectangle
        {
            get
            {
                return canvas.ClientRectangle;
            }
        }
        void IView.ZoomToRect(BoundingRect World2D)
        {
            if (World2D.Width + World2D.Height < 1e-6) return;
            projection.SetExtent(model.Extent);
            projection.SetPlacement((this as IView).DisplayRectangle, World2D);
            modelNeedsRepaint = true;
            (this as IView).Invalidate(PaintBuffer.DrawingAspect.All, (this as IView).DisplayRectangle);
            RecalcScrollPosition(); // war auskommentiert, am 23.6.14 wieder aktiviert wg. ERSACAD
        }
        SnapPointFinder.DidSnapModes IView.AdjustPoint(System.Drawing.Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore)
        {
            SnapPointFinder spf = new SnapPointFinder();
            spf.FilterList = this.Frame.Project.FilterList;
            spf.Init(MousePoint, projection, Frame.SnapMode, 5);
            spf.SnapMode = Frame.SnapMode;
            spf.Snap30 = Frame.GetBooleanSetting("Snap.Circle30", false);
            spf.Snap45 = Frame.GetBooleanSetting("Snap.Circle45", false);
            spf.SnapLocalOrigin = Frame.GetBooleanSetting("Snap.SnapLocalOrigin", false);
            spf.SnapGlobalOrigin = Frame.GetBooleanSetting("Snap.SnapGlobalOrigin", false);
            spf.IgnoreList = ToIgnore;
            model.AdjustPoint(spf, projection, new Set<Layer>(visibleLayers.Checked));
            WorldPoint = spf.SnapPoint; // ist auch gesetzt, wenn nicht gefangen (gemäß DrawingPlane)
            lastSnapObject = spf.BestObject;
            lastSnapMode = spf.DidSnap;
            return spf.DidSnap;
        }
        SnapPointFinder.DidSnapModes IView.AdjustPoint(GeoPoint BasePoint, System.Drawing.Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore)
        {
            SnapPointFinder spf = new SnapPointFinder();
            spf.FilterList = this.Frame.Project.FilterList;
            spf.Init(MousePoint, projection, Frame.SnapMode, 5, BasePoint);
            spf.SnapMode = Frame.SnapMode;
            spf.Snap30 = Frame.GetBooleanSetting("Snap.Circle30", false);
            spf.Snap45 = Frame.GetBooleanSetting("Snap.Circle45", false);
            spf.SnapLocalOrigin = Frame.GetBooleanSetting("Snap.SnapLocalOrigin", false);
            spf.SnapGlobalOrigin = Frame.GetBooleanSetting("Snap.SnapGlobalOrigin", false);
            spf.IgnoreList = ToIgnore;
            model.AdjustPoint(spf, projection, new Set<Layer>(visibleLayers.Checked));
            WorldPoint = spf.SnapPoint;
            lastSnapObject = spf.BestObject;
            lastSnapMode = spf.DidSnap;
            return spf.DidSnap;
        }
        CADability.GeoObject.GeoObjectList IView.PickObjects(System.Drawing.Point MousePoint, PickMode pickMode)
        {
            GeoPoint2D p0 = projection.PointWorld2D(MousePoint);
            double d = 5.0 * projection.DeviceToWorldFactor;
            BoundingRect pickrect = new BoundingRect(p0, d, d);
            return model.GetObjectsFromRect(pickrect, projection, null, pickMode, null);
        }
        IShowProperty IView.GetShowProperties(IFrame Frame)
        {
            return this;
        }
        CADability.GeoObject.IGeoObject IView.LastSnapObject
        {
            get { return lastSnapObject; }
        }
        CADability.GeoObject.SnapPointFinder.DidSnapModes IView.LastSnapMode
        {
            get { return lastSnapMode; }
        }

        #endregion
        /// <summary>
        /// Zooms to the extend of the model. The projection direction is not changed.
        /// </summary>
        /// <param name="factor"></param>
        public void ZoomToModelExtent(double factor)
        {
            if (canvas != null)
            {
                if (canvas.ClientRectangle.Width == 0 && canvas.ClientRectangle.Height == 0) return;
                BoundingRect ext = model.GetExtent(projection);
                if (showPaper && paperHeight > 0 && paperWidth > 0)
                {
                    GeoPoint2D p1 = projection.ProjectUnscaled(GeoPoint.Origin);
                    GeoPoint2D p2 = projection.ProjectUnscaled(new GeoPoint(paperWidth, paperHeight, 0.0));
                    ext.MinMax(new GeoPoint2D(p1));
                    ext.MinMax(new GeoPoint2D(p2));
                }
                if (ext.IsEmpty())
                {
                    ext = new BoundingRect(0, 0, 100, 100);
                }
                ext = ext * factor;
                projection.SetClientRect(canvas.ClientRectangle); // war nötig wg. Mauell
                projection.SetExtent(model.Extent);
                projection.SetPlacement(canvas.ClientRectangle, ext);
                double f, dx, dy;
                projection.GetPlacement(out f, out dx, out dy);
                canvas?.Invalidate();
                modelNeedsRepaint = true;
                RecalcScrollPosition();
            }
        }
        private BoundingRect GetDisplayExtent()
        {
            BoundingRect ext = model.GetExtentForZoomTotal(projection);
            if (showPaper && paperHeight > 0 && paperWidth > 0)
            {
                GeoPoint2D p1 = projection.ProjectUnscaled(GeoPoint.Origin);
                GeoPoint2D p2 = projection.ProjectUnscaled(new GeoPoint(paperWidth, paperHeight, 0.0));
                ext.MinMax(new GeoPoint2D(p1));
                ext.MinMax(new GeoPoint2D(p2));
            }
            double f, dx, dy;
            if (ext.IsEmpty()) return ext;
            projection.GetPlacement(out f, out dx, out dy);
            return new BoundingRect(ext.Left * f + dx, -ext.Bottom * f + dy, ext.Right * f + dx, -ext.Top * f + dy);
        }
        private void RecalcScrollPosition()
        {
            Rectangle d = (this as IView).DisplayRectangle;
            BoundingRect e = GetDisplayExtent();
            if (e.IsEmpty() || d.IsEmpty) return;
            e = e * 1.1; // etwas Rand drum rum lassen, Konfigurierbar!!!
            double hPart = d.Width / e.Width; // dieser Anteil wird dargestellt: 1.0: Alles, >1.0 mehr als alles, <1.0 der Anteil
            double hPos;
            if (hPart >= 1.0) hPos = 0.0;
            else hPos = (d.Left - e.Left) / (e.Width - d.Width); // 0.0: ganz links, 1.0 ganz rechts
            double vPart = d.Height / e.Height; // dieser Anteil wird dargestellt: 1.0: Alles, >1.0 mehr als alles, <1.0 der Anteil
            double vPos;
            if (vPart >= 1.0) vPos = 0.0;
            else vPos = 1.0 - (d.Top - e.Bottom) / (e.Height - d.Height); // 0.0: ganz unten, , 1.0 ganz oben
            if (ScrollPositionChangedEvent != null) ScrollPositionChangedEvent(hPart, hPos, vPart, vPos);
        }
        public void ZoomTotal(double f) // implements ICadView.ZoomTotal
        {
            Rectangle clientRect = canvas.ClientRectangle;
            if (clientRect.Width == 0 && clientRect.Height == 0) return;
            BoundingRect ext = model.GetExtent(this.projection);
            if (ext.Width + ext.Height == 0.0) ext.Inflate(1, 1);
            ext = ext * f;
            projection.SetPlacement(clientRect, ext);
            canvas.Invalidate();
            RecalcScrollPosition();
        }

        #region IActionInputView Members

        bool IActionInputView.IsLayerVisible(Layer l)
        {
            if (l == null) return true;
            if (visibleLayers == null) return true;
            return visibleLayers.IsLayerChecked(l);
        }

        Layer[] IActionInputView.GetVisibleLayers()
        {
            return visibleLayers.Checked;
        }

        bool IActionInputView.AllowContextMenu
        {
            get { return allowContextMenu; }
        }

        void IActionInputView.SetAdditionalExtent(BoundingCube bc)
        {

        }
        void IActionInputView.MakeEverythingTranparent(bool transparent)
        {

        }

        #endregion

        #region ISerializable Members
        protected GDI2DView(SerializationInfo info, StreamingContext context)
            : this()
        {
            project = info.GetValue("Project", typeof(Project)) as Project;
            model = info.GetValue("Model", typeof(Model)) as Model;
            name = info.GetString("Name");
            projection = info.GetValue("Projection", typeof(Projection)) as Projection;
            try
            {
                paperWidth = info.GetDouble("PaperWidth");
                paperHeight = info.GetDouble("PaperHeight");
                showPaper = info.GetBoolean("ShowPaper");
            }
            catch (SerializationException)
            {
                paperWidth = 0.0;
                paperHeight = 0.0;
                showPaper = false;
            }
            try
            {
                useDisplayOrder = info.GetBoolean("UseDisplayOrder");
            }
            catch (SerializationException)
            {
                useDisplayOrder = false;
            }
            try
            {
                visibleLayers = info.GetValue("VisibleLayers", typeof(CheckedLayerList)) as CheckedLayerList;
            }
            catch (SerializationException)
            {
                visibleLayers = null;
            }
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Project", project);
            info.AddValue("Model", model);
            info.AddValue("Name", name);
            info.AddValue("Projection", projection);
            info.AddValue("PaperWidth", paperWidth);
            info.AddValue("PaperHeight", paperHeight);
            info.AddValue("ShowPaper", showPaper);
            info.AddValue("UseDisplayOrder", useDisplayOrder);
            info.AddValue("VisibleLayers", visibleLayers);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            // kopiert aus Init(), jedoch ohne Überschreibung der eingelesenen properties
            if (Settings.GlobalSettings != null)
            {
                allowDrag = Settings.GlobalSettings.GetBoolValue("AllowDrag", true);
                allowDrop = Settings.GlobalSettings.GetBoolValue("AllowDrop", true);
                allowContextMenu = Settings.GlobalSettings.GetBoolValue("AllowContextMenu", true);
            }
            if (visibleLayers == null) visibleLayers = new CheckedLayerList(project.LayerList, project.LayerList.ToArray(), "AnimatedView.VisibleLayers");
            visibleLayers.CheckStateChangedEvent += new CheckedLayerList.CheckStateChangedDelegate(OnVisibleLayersChanged);
            if (Frame != null)
            {
                selectColor = Frame.GetColorSetting("Select.SelectColor", Color.Yellow); // die Farbe für die selektierten Objekte
                selectWidth = Frame.GetIntSetting("Select.SelectWidth", 2);
                dragWidth = Frame.GetIntSetting("Select.DragWidth", 5);
            }
            int griddisplaymode = Settings.GlobalSettings.GetIntValue("Grid.DisplayMode", 1);
            if (griddisplaymode > 0)
            {
                projection.Grid.DisplayMode = (Grid.Appearance)(griddisplaymode - 1);
                projection.Grid.Show = true;
            }
            else
            {
                projection.Grid.Show = false;
            }
            projection.Grid.XDistance = Settings.GlobalSettings.GetDoubleValue("Grid.XDistance", 10.0);
            projection.Grid.YDistance = Settings.GlobalSettings.GetDoubleValue("Grid.YDistance", 10.0);
            modelNeedsRepaint = false;
        }
        #endregion
    }
}
#endif
