#if !WEBASSEMBLY
using CADability.Attribute;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
// PrintDialog etc.
using CADability.Substitutes;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using PaintEventArgs = CADability.Substitutes.PaintEventArgs;

namespace CADability
{
    /*  NEUES DRUCKKONZEPT
     * Linien, Dreiecke und Texte werden auf das Graphics objekt ausgegeben. 
     * Alle Objekte werden nach z sortiert (mittlerer z-Wert). Die Objekte belegen in z allerdings Intervalle. Dort wo die
     * Intervalle sich überschneiden und auch eine Überschneidung im 2D Bereich stattfindet muss im 3-dimensionalen die Ebenen
     * der beiden Objekte geschnitten werden. Geht die Schnittlinie durch beide Dreiecke, so müssen die Dreiecke an dieser Stelle 
     * aufgeteilt werden. Die geschnittenen Dreiecke (eines zerfällt i.A. in drei) können einsortiert werden.
     * Vielleicht arbeitet man mit einem OctTree um sich schneidende Dreiecke zu finden. Allerdings müssen Dreiecke mit gemeinsamen 
     * Kanten ausgenommen werden.
     * Ausgabe der Dreiecke von hinten nach vorne. Mit PathGradientBrush kann man drei Punkte und drei Farben spezifizieren,
     * das sollte für shading reichen.
     * Texte befinden sich in einer Ebene (Parallelogramm). Wenn dieses Parallelogramm sich schneidet mit einem Dreieck, dann Texte 
     * als Dreiecke ausgeben, sonst als Text (in der richtigen Reihenfolge wg. Überdeckung)
     * Linen werden ggf an Dreiecken geschnitten und als Teilstücke ausgegeben. 
     * Kanten von Flächen müssen extra behandelt werden. Evtl. garnicht ausgeben, evtl. Dreieckskanten extra kennzeichnen, wenn diese
     * zugleich Kanten sind. Ein Kantenstück müsste wissen zu welchen Dreiecken es gehört, dann kann es nach den beiden
     * Dreiecken ausgegeben werden.
     * Extramodus für Ausgabe ohne Dreiecke.
     * Wie sieht es mit Transparenz aus?
     */


    /// <summary>
    /// A view that shows a <see cref="Layout"/>, i.e. the arrangement of one or more patches (projections of a model)
    /// on a sheet of paper, as it will be printed. The paper can be zoomed and scrolled; the paper size and the patches
    /// are shown as properties in the property page, and the context menu allows to add patches, to rename or remove
    /// the layout and to print it with the print settings of the project.
    /// </summary>

    public class LayoutView : IShowPropertyImpl, IView, ICommandHandler
    {
        //public event RepaintView RepaintDrawingEvent;
        //public event RepaintView RepaintSelectEvent;
        //public event RepaintView RepaintActiveEvent;
        /// <summary>
        /// Handler for <see cref="RepaintActionEvent"/>.
        /// </summary>
        /// <param name="View">The layout view that is being painted</param>
        /// <param name="paintTo3d">The paint engine of the canvas, set up for painting in screen coordinates</param>
        public delegate void RepaintActionDelegate(IView View, IPaintTo3D paintTo3d);
        /// <summary>
        /// Raised during each paint cycle after the paper has been painted and before the patches are drawn. Actions on the layout view
        /// (e.g. positioning a patch) can draw their feedback here.
        /// </summary>
        public event RepaintActionDelegate RepaintActionEvent;

        private Layout layout;
        //private IPaintTo3D paintTo3D; // die OpenGL Zeichenmaschine
        internal ModOp2D layoutToScreen; // Abbildung layout auf Bildschirm, also Zoom und Scroll
        internal ModOp2D screenToLayout; // inverse Abbildung
        private PrintDocument printDocument; // lokale Printeinstellungen
        private Project project; //Rückverweis wg. Druckereinstellung und Dokument Name
        private DoubleProperty paperWidth;
        private DoubleProperty paperHeight;
        // eigentlich eine Untergruppe für alle Patches

        /// <summary>
        /// Creates a view on the provided layout. The print settings of the view are taken from the page settings stored in the layout, otherwise
        /// from the print document of the project; if there is none, only the orientation (landscape or portrait) is derived from the paper size of
        /// the layout. The frame creates one layout view per layout of the project.
        /// </summary>
        /// <param name="layout">The layout to show</param>
        /// <param name="project">The project that contains the layout, its models and the print settings</param>
        public LayoutView(Layout layout, Project project)
        {
            this.project = project;
            this.layout = layout;
            screenToLayout = layoutToScreen = ModOp2D.Null;
            base.resourceIdInternal = "LayoutView";
            printDocument = new PrintDocument();
            // the page settings of the layout, else those of the project, else the orientation of the layout's paper
            PageSetup pageSetup = layout.PageSetup ?? project.DefaultPageSetup;
            try
            {
                if (pageSetup != null) pageSetup.ApplyTo(printDocument);
                else printDocument.DefaultPageSettings.Landscape = layout.PaperWidth > layout.PaperHeight;
            }
            catch (InvalidPrinterException) { } // no printer installed
        }

        event ScrollPositionChanged IView.ScrollPositionChangedEvent
        {
            add
            {
                throw new NotImplementedException();
            }

            remove
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// The <see cref="CADability.Layout"/> shown by this view: the paper size and the patches.
        /// </summary>
        public Layout Layout
        {
            get
            {
                return layout;
            }
        }

        /// <summary>
        /// True once the mapping from the layout (paper coordinates in mm) to the screen has been set up, i.e. after the view has been zoomed or
        /// painted for the first time. Before that, the first paint calls <see cref="ZoomTotal(double)"/> itself.
        /// </summary>
        public bool IsInitialized
        {
            get
            {
                return !layoutToScreen.IsNull;
            }
        }
        /// <summary>
        /// Zooms so that the whole paper is visible on the canvas.
        /// </summary>
        /// <param name="factor">Factor by which the paper rectangle is enlarged before it is fitted into the canvas; 1.1 leaves a small margin, 1.0 fits exactly</param>
        public void ZoomTotal(double factor)
        {
            BoundingRect tot = new BoundingRect(0.0, 0.0, layout.PaperWidth, layout.PaperHeight);
            tot *= factor;
            (this as IView).ZoomToRect(tot);
        }
        /// <summary>
        /// Prints the layout on a single page with the provided print document. Depending on the global setting "Printing.PrintingMode" the patches
        /// are either output as vector graphics via <see cref="PrintToGDI"/> (mode 1, with hidden line removal and shading) or each patch is rendered
        /// into a bitmap at the printer resolution which is placed on the page. The paper size of the layout is assumed to be in mm. Exceptions of
        /// the printing system are swallowed.
        /// </summary>
        /// <param name="pd">The print document to print to, with the printer and page settings already set</param>
        [Obsolete("Printing with GDI+ moves into CADability.Forms in CADability 2.0 (issue #412).")]
        public void Print(PrintDocument pd)
        {
            bool printToGDI = Settings.GlobalSettings.GetIntValue("Printing.PrintingMode", 0) == 1;
            if (printToGDI)
            {
                PrintToGDI pdg = new PrintToGDI(this);
                pd.PrintPage += pdg.OnPrintPage;
                try
                {
                    pd.Print();
                }
                catch (Exception)
                {
                }
                pd.PrintPage -= pdg.OnPrintPage;
            }
            else
            {
                pd.PrintPage += OnPrintPage;
                try
                {
                    pd.Print();
                }
                catch (Exception)
                {
                }
                pd.PrintPage -= OnPrintPage;
            }
        }

#region IShowProperty
        /// <inheritdoc/>
        public override void Added(IPropertyPage propertyTreeView)
        {
            base.Added(propertyTreeView);
            //propertyTreeView.FocusChangedEvent += new FocusChangedDelegate(OnFocusChanged);
            for (int i = 0; i < layout.Patches.Length; ++i)
            {
                LayoutPatch lp = layout.Patches[i];
                lp.Connect(layout, project, this);
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Removed(IPropertyTreeView)"/>
        /// </summary>
        /// <param name="propertyTreeView">the IPropertyTreeView from which it was removed</param>
        public override void Removed(IPropertyTreeView propertyTreeView)
        {
            for (int i = 0; i < layout.Patches.Length; ++i)
            {
                LayoutPatch lp = layout.Patches[i];
                lp.Disconnect(project);
            }
            //propertyTreeView.FocusChangedEvent -= new FocusChangedDelegate(OnFocusChanged);
            base.Removed(propertyTreeView);
        }
        /// <inheritdoc/>
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
        /// <inheritdoc/>
        public override void LabelChanged(string NewText)
        {
            if (NewText == layout.Name) return;
            if (!Frame.Project.RenameLayout(layout, NewText))
            {
                // Messagebox oder nicht?
            }
        }
        /// <inheritdoc/>
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.LayoutView", false, this);
            }
        }
        /// <inheritdoc/>
        public override string LabelText
        {
            get
            {
                return layout.Name;
            }
            set
            {
                base.LabelText = value; // wozu?
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
        /// <inheritdoc/>
        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }
        /// <inheritdoc/>
        public override IShowProperty[] SubEntries
        {
            get
            {
                if (subEntries == null)
                {   // man könnte auch die Events bei Added setzen und bei Removed wieder entfernen ...
                    subEntries = new IShowProperty[2 + layout.PatchCount];
                    paperWidth = new DoubleProperty(base.Frame, "Layout.PaperWidth");
                    paperWidth.OnGetValue = () => layout.PaperWidth;
					paperWidth.OnSetValue = val => layout.PaperWidth = val;
                    paperHeight = new DoubleProperty(base.Frame, "Layout.PaperHeight");
					paperHeight.OnGetValue = () => layout.PaperHeight;
					paperHeight.OnSetValue = val => layout.PaperHeight = val;
                    paperWidth.Refresh();
                    paperHeight.Refresh();
                    subEntries[0] = paperWidth;
                    subEntries[1] = paperHeight;
                    for (int i = 0; i < layout.Patches.Length; ++i)
                    {
                        LayoutPatch lp = layout.Patches[i];
                        subEntries[2 + i] = lp;
                    }
                }
                return subEntries;
            }
        }
#endregion
#region ICondorViewInternal Members
        private ICanvas canvas;
        void IView.Connect(ICanvas canvas)
        {
            this.canvas = canvas;
        }
        void IView.Disconnect(ICanvas canvas)
        {
        }
        ICanvas IView.Canvas => canvas;
        string IView.PaintType => "3D";
        /// <summary>
        /// Paints the view: the area outside the paper in black, the paper in white, then (after <see cref="RepaintActionEvent"/>) each patch of the
        /// layout with its own projection, clipped to its area. The first call sets up the mapping to the screen with <see cref="ZoomTotal(double)"/>.
        /// </summary>
        /// <param name="e">The paint event with the clip rectangle</param>
        public void OnPaint(PaintEventArgs e)
        {
            if (!IsInitialized) ZoomTotal(1.1);

            //PaintToOpenGl paintTo3D = new PaintToOpenGl(1e-3); // woher nehmen?
            // IntPtr dc = e.Graphics.GetHdc();
            //paintTo3D.Init(dc, condorCtrl.ClientRectangle.Width, condorCtrl.ClientRectangle.Height, false);
            //IPaintTo3D ipaintTo3D = paintTo3D;
            // ipaintTo3D.Init(condorCtrl); // das erzeugt jedesmal einen neuen renderContext, das kann doch nicht richtig sein
            //ipaintTo3D.MakeCurrent();
            //ipaintTo3D.Clear(Color.Black); // damit ist black die Backgroundcolor

            //e.Graphics.FillRectangle(Brushes.Black, e.Graphics.ClipBounds);
            GeoPoint2D ll = layoutToScreen * new GeoPoint2D(0.0, 0.0);
            //GeoPoint2D ur = layoutToScreen * new GeoPoint2D(layout.PaperWidth, layout.PaperHeight);
            //RectangleF paperrect = RectangleF.FromLTRB((float)ll.x, (float)ur.y, (float)ur.x, (float)ll.y);
            //e.Graphics.FillRectangle(Brushes.White, paperrect);

            //BoundingCube bc = new BoundingCube(ll.x, ur.x, ll.y, ur.y, -1.0, 1.0);
            //ipaintTo3D.SetProjection(new Projection(Projection.StandardProjection.FromTop), bc);
            //ipaintTo3D.UseZBuffer(false);
            //ipaintTo3D.SetColor(Color.White);
            //ipaintTo3D.FillRect2D(ll.PointF, ur.PointF);

            //ipaintTo3D.FinishPaint();// DEBUG
            //return; // DEBUG

            //ipaintTo3D.AvoidColor(Color.White);

            //if (RepaintActionEvent != null) RepaintActionEvent(this, ipaintTo3D);

            for (int i = 0; i < layout.Patches.Length; ++i)
            {
                LayoutPatch lp = layout.Patches[i];
                //BoundingRect paperRect = new BoundingRect(0.0, 0.0, layout.PaperWidth, layout.PaperHeight);

                //BoundingRect ext;
                //if (lp.Area != null)
                //{
                //    ext = lp.Area.Extent;
                //}
                //else
                //{
                //    ext = paperRect;
                //}

                //GeoPoint2D clipll = layoutToScreen * ext.GetLowerLeft();
                //GeoPoint2D clipur = layoutToScreen * ext.GetUpperRight();
                
                //Rectangle clipRectangle = Rectangle.FromLTRB((int)clipll.x, (int)clipur.y, (int)clipur.x, (int)clipll.y);
                
                Projection pr = lp.Projection.Clone();
                pr.GetPlacement(out double factor, out double dx, out double dy);
                pr.SetPlacement(layoutToScreen.Factor * factor, ll.x + layoutToScreen.Factor * dx, ll.y - layoutToScreen.Factor * dy);
                pr.Precision = lp.Model.Extent.Size / 1000;

                //ipaintTo3D.Precision = pr.Precision;
                //ipaintTo3D.SetProjection(pr, lp.Model.Extent);
                //ipaintTo3D.UseZBuffer(true);
                //ipaintTo3D.SetClip(clipRectangle);
                //ipaintTo3D.PaintFaces(PaintTo3D.PaintMode.All);
                // lp.Model.ClearDisplayLists();
                //lp.Model.RecalcDisplayLists(ipaintTo3D);
                //if (lp.Projection.ShowFaces)
                //{
                //    ipaintTo3D.PaintFaces(PaintTo3D.PaintMode.FacesOnly);
                //    foreach (KeyValuePair<Layer, IPaintTo3DList> kv in lp.Model.layerFaceDisplayList)
                //    {
                //        if (lp.IsLayerVisible(kv.Key) || lp.Model.nullLayer == kv.Key)
                //        {
                //            ipaintTo3D.List(kv.Value);
                //        }
                //    }
                //}
                //ipaintTo3D.PaintFaces(PaintTo3D.PaintMode.CurvesOnly);
                //foreach (KeyValuePair<Layer, IPaintTo3DList> kv in lp.Model.layerCurveDisplayList)
                //{
                //    if (lp.IsLayerVisible(kv.Key) || lp.Model.nullLayer == kv.Key)
                //    {
                //        ipaintTo3D.List(kv.Value);
                //    }
                //}
                //ipaintTo3D.SetClip(Rectangle.Empty);
            }
            //ipaintTo3D.FinishPaint();
        }
        /// <summary>
        /// Repaints the view after the canvas has been resized; the zoom and scroll position are not adapted.
        /// </summary>
        /// <param name="oldRectangle">The client rectangle of the canvas before the change (not evaluated)</param>
        public void OnSizeChanged(System.Drawing.Rectangle oldRectangle)
        {
            canvas?.Invalidate();
        }
        /// <summary>
        /// Not implemented: the layout view does not react to scroll bars.
        /// </summary>
        /// <param name="Position">Not evaluated</param>
        public void HScroll(double Position)
        {
            // TODO:  Add LayoutView.HScroll implementation
        }
        /// <summary>
        /// Not implemented: the layout view does not react to scroll bars.
        /// </summary>
        /// <param name="Position">Not evaluated</param>
        public void VScroll(double Position)
        {
            // TODO:  Add LayoutView.VScroll implementation
        }
        /// <inheritdoc/>
        public void OnMouseEnter(EventArgs e)
        {

            Frame.ActionStack.OnMouseEnter(e, this);
        }
        /// <inheritdoc/>
        public void OnMouseHover(EventArgs e)
        {
            Frame.ActionStack.OnMouseHover(e, this);
        }
        /// <inheritdoc/>
        public void OnMouseLeave(EventArgs e)
        {
            Frame.ActionStack.OnMouseLeave(e, this);
        }
        /// <inheritdoc/>
        public void OnMouseMove(MouseEventArgs e)
        {
            Frame.ActionStack.OnMouseMove(e, this);
        }
        /// <inheritdoc/>
        public void OnMouseUp(MouseEventArgs e)
        {
            Frame.ActionStack.OnMouseUp(e, this);
        }
        /// <inheritdoc/>
        public void OnMouseDown(MouseEventArgs e)
        {
            Frame.ActionStack.OnMouseDown(e, this);
        }
        /// <summary>
        /// Zooms in or out by the factor 1.1 around the mouse position and repaints.
        /// </summary>
        /// <param name="e">The mouse event with the wheel delta</param>
        public void OnMouseWheel(MouseEventArgs e)
        {
            double Factor;
            if (e.Delta > 0) Factor = 1.0 / 1.1;
            else if (e.Delta < 0) Factor = 1.1;
            else return;
            Rectangle clr = canvas.ClientRectangle;
            BoundingRect rct = new BoundingRect(clr.Left, clr.Bottom, clr.Right, clr.Top);
            rct.Modify(screenToLayout);
            System.Drawing.Point p = new System.Drawing.Point(e.X, e.Y);
            p = canvas.PointToClient(Frame.UIService.CurrentMousePosition);
            GeoPoint2D p2 = screenToLayout * new GeoPoint2D(p.X, p.Y);
            rct.Right = p2.x + (rct.Right - p2.x) * Factor;
            rct.Left = p2.x + (rct.Left - p2.x) * Factor;
            rct.Bottom = p2.y + (rct.Bottom - p2.y) * Factor;
            rct.Top = p2.y + (rct.Top - p2.y) * Factor;
            (this as IView).ZoomToRect(rct);
            (this as IView).InvalidateAll();
        }
        /// <summary>
        /// Not used: a double click has no effect in the layout view.
        /// </summary>
        /// <param name="e">Not evaluated</param>
        public void OnMouseDoubleClick(MouseEventArgs e)
        {
        }
        /// <inheritdoc/>
        public void ZoomDelta(double f)
        {
            double Factor = f;
            System.Drawing.Rectangle clr = canvas.ClientRectangle;
            System.Drawing.Point p = new System.Drawing.Point((clr.Left + clr.Right) / 2, (clr.Bottom + clr.Top) / 2);
            BoundingRect rct = new BoundingRect(clr.Left, clr.Bottom, clr.Right, clr.Top);
            rct.Modify(screenToLayout);
            GeoPoint2D p2 = screenToLayout * new GeoPoint2D(p.X, p.Y);
            rct.Right = p2.x + (rct.Right - p2.x) * Factor;
            rct.Left = p2.x + (rct.Left - p2.x) * Factor;
            rct.Bottom = p2.y + (rct.Bottom - p2.y) * Factor;
            rct.Top = p2.y + (rct.Top - p2.y) * Factor;
            (this as IView).ZoomToRect(rct);
            (this as IView).InvalidateAll();
        }
        /// <summary>
        /// Always false: objects cannot be dropped on a layout view.
        /// </summary>
        public bool AllowDrop
        {
            get
            {
                return false;
            }
        }
        /// <summary>
        /// Always false: objects cannot be dragged out of a layout view.
        /// </summary>
        public bool AllowDrag
        {
            get
            {
                return false;
            }
        }
        /// <summary>
        /// Always true: a right click in the layout view opens the context menu of the current action.
        /// </summary>
        public bool AllowContextMenu
        {
            get
            {
                return true;
            }
        }
        /// <summary>
        /// Not implemented: the layout view does not accept dropped data.
        /// </summary>
        /// <param name="drgevent">Not evaluated</param>
        public void OnDragDrop(DragEventArgs drgevent)
        {
            // TODO:  Add LayoutView.OnDragDrop implementation
        }
        /// <summary>
        /// Not implemented: the layout view does not accept dropped data.
        /// </summary>
        /// <param name="drgevent">Not evaluated</param>
        public void OnDragEnter(DragEventArgs drgevent)
        {
            // TODO:  Add LayoutView.OnDragEnter implementation
        }
        /// <summary>
        /// Not implemented: the layout view does not accept dropped data.
        /// </summary>
        /// <param name="e">Not evaluated</param>
        public void OnDragLeave(EventArgs e)
        {
            // TODO:  Add LayoutView.OnDragLeave implementation
        }
        /// <summary>
        /// Not implemented: the layout view does not accept dropped data.
        /// </summary>
        /// <param name="drgevent">Not evaluated</param>
        public void OnDragOver(DragEventArgs drgevent)
        {
            // TODO:  Add LayoutView.OnDragOver implementation
        }
        /// <summary>
        /// Remnant of a former tooltip interface of the views, no longer called; always returns 0.
        /// </summary>
        /// <param name="ScreenCursorPosition">Not evaluated</param>
        /// <returns>0</returns>
        public int GetInfoProviderIndex(System.Drawing.Point ScreenCursorPosition)
        {
            // TODO:  Add LayoutView.GetInfoProviderIndex implementation
            return 0;
        }
        /// <summary>
        /// Remnant of a former tooltip interface of the views, no longer called; always returns null.
        /// </summary>
        /// <param name="Index">Not evaluated</param>
        /// <param name="Level">Not evaluated</param>
        /// <returns>null</returns>
        public string GetInfoProviderText(int Index, CADability.UserInterface.InfoLevelMode Level)
        {
            // TODO:  Add LayoutView.GetInfoProviderText implementation
            return null;
        }
        /// <summary>
        /// Remnant of a former tooltip interface of the views, no longer called; always returns 0.
        /// </summary>
        /// <param name="Index">Not evaluated</param>
        /// <returns>0</returns>
        public int GetInfoProviderVerticalPosition(int Index)
        {
            // TODO:  Add LayoutView.GetInfoProviderVerticalPosition implementation
            return 0;
        }
#endregion
#region IView Members
        ProjectedModel IView.ProjectedModel
        {
            get
            {
                // TODO:  Add LayoutView.ProjectedModel getter implementation
                // wer ruft das wozu auf, welches ProjectedModel sollen wir liefern?
                return null;
            }
        }
        Model IView.Model
        {
            get
            {
                return this.project.GetActiveModel(); // damit es keinen Crash gibt
                                                      // TODO:  Add LayoutView.Model getter implementation
                                                      // wer ruft das wozu auf, welches Model sollen wir liefern?
                                                      // return null;
            }
        }
        Projection IView.Projection
        {
            get
            {
                return new Projection(Projection.StandardProjection.FromTop); // damit es keinen Crash gibt
            }
            set { }
        }
        /// <summary>
        /// Dragging from a layout view is not supported: returns <see cref="DragDropEffects.None"/> without starting a drag and drop operation.
        /// </summary>
        /// <param name="dragList">Not evaluated</param>
        /// <param name="all">Not evaluated</param>
        /// <returns><see cref="DragDropEffects.None"/></returns>
        public Substitutes.DragDropEffects DoDragDrop(GeoObjectList dragList, Substitutes.DragDropEffects all)
        {
            return DragDropEffects.None;
        }
        void IView.SetCursor(string cursor)
        {
            (this as IView).Canvas.Cursor = cursor;
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
            IPaintTo3D paintTo3D = canvas.PaintTo3D;
            Rectangle clr = e.ClipRectangle;
            if (!IsInitialized) ZoomTotal(1.1);

            //PaintToOpenGl paintTo3D = new PaintToOpenGl(1e-3); // woher nehmen?
            // IntPtr dc = e.Graphics.GetHdc();
            //paintTo3D.Init(dc, condorCtrl.ClientRectangle.Width, condorCtrl.ClientRectangle.Height, false);
            IPaintTo3D ipaintTo3D = paintTo3D;
            // ipaintTo3D.Init(condorCtrl); // das erzeugt jedesmal einen neuen renderContext, das kann doch nicht richtig sein
            ipaintTo3D.MakeCurrent();
            ipaintTo3D.Clear(Color.Black); // damit ist black die Backgroundcolor

            //e.Graphics.FillRectangle(Brushes.Black, e.Graphics.ClipBounds);
            GeoPoint2D ll = layoutToScreen * new GeoPoint2D(0.0, 0.0);
            GeoPoint2D ur = layoutToScreen * new GeoPoint2D(layout.PaperWidth, layout.PaperHeight);
            RectangleF paperrect = RectangleF.FromLTRB((float)ll.x, (float)ur.y, (float)ur.x, (float)ll.y);
            //e.Graphics.FillRectangle(Brushes.White, paperrect);

            //BoundingCube bc = new BoundingCube(ll.x, ur.x, ll.y, ur.y, -1.0, 1.0);
            //ipaintTo3D.SetProjection(new Projection(Projection.StandardProjection.FromTop), bc);
            ipaintTo3D.UseZBuffer(false);
            ipaintTo3D.SetColor(Color.White);
            ipaintTo3D.FillRect2D(ll.PointF, ur.PointF);

            //ipaintTo3D.FinishPaint();// DEBUG
            //return; // DEBUG

            ipaintTo3D.AvoidColor(Color.White);

            if (RepaintActionEvent != null) RepaintActionEvent(this, ipaintTo3D);

            for (int i = 0; i < layout.Patches.Length; ++i)
            {
                LayoutPatch lp = layout.Patches[i];
                BoundingRect paperRect = new BoundingRect(0.0, 0.0, layout.PaperWidth, layout.PaperHeight);

                BoundingRect ext;
                if (lp.Area != null)
                {
                    ext = lp.Area.Extent;
                }
                else
                {
                    ext = paperRect;
                }

                GeoPoint2D clipll = layoutToScreen * ext.GetLowerLeft();
                GeoPoint2D clipur = layoutToScreen * ext.GetUpperRight();
                Rectangle clipRectangle = Rectangle.FromLTRB((int)clipll.x, (int)clipur.y, (int)clipur.x, (int)clipll.y);
                Projection pr = lp.Projection.Clone();
                double factor, dx, dy;
                pr.GetPlacement(out factor, out dx, out dy);
                pr.SetPlacement(layoutToScreen.Factor * factor, ll.x + layoutToScreen.Factor * dx, ll.y - layoutToScreen.Factor * dy);
                pr.Precision = lp.Model.Extent.Size / 1000;

                ipaintTo3D.Precision = pr.Precision;
                ipaintTo3D.SetProjection(pr, lp.Model.Extent);
                ipaintTo3D.UseZBuffer(true);
                ipaintTo3D.SetClip(clipRectangle);
                ipaintTo3D.PaintFaces(PaintTo3D.PaintMode.All);
                // lp.Model.ClearDisplayLists();
                lp.Model.RecalcDisplayLists(ipaintTo3D);
                if (lp.Projection.ShowFaces)
                {
                    ipaintTo3D.PaintFaces(PaintTo3D.PaintMode.FacesOnly);
                    foreach (KeyValuePair<Layer, IPaintTo3DList> kv in lp.Model.layerFaceDisplayList)
                    {
                        if (lp.IsLayerVisible(kv.Key) || lp.Model.nullLayer == kv.Key)
                        {
                            ipaintTo3D.List(kv.Value);
                        }
                    }
                }
                ipaintTo3D.PaintFaces(PaintTo3D.PaintMode.CurvesOnly);
                foreach (KeyValuePair<Layer, IPaintTo3DList> kv in lp.Model.layerCurveDisplayList)
                {
                    if (lp.IsLayerVisible(kv.Key) || lp.Model.nullLayer == kv.Key)
                    {
                        ipaintTo3D.List(kv.Value);
                    }
                }
                ipaintTo3D.SetClip(Rectangle.Empty);
            }
            ipaintTo3D.FinishPaint();
        }
        void IView.Invalidate(CADability.PaintBuffer.DrawingAspect aspect, System.Drawing.Rectangle ToInvalidate)
        {
            canvas?.Invalidate();
        }
        void IView.InvalidateAll()
        {
            canvas?.Invalidate();
        }
        void IView.SetPaintHandler(PaintBuffer.DrawingAspect aspect, PaintView PaintHandler)
        {
            // nicht sicher, ob das gebraucht wird ...
        }
        void IView.RemovePaintHandler(PaintBuffer.DrawingAspect aspect, PaintView PaintHandler)
        {
        }
        System.Drawing.Rectangle IView.DisplayRectangle
        {
            get
            {
                if (canvas == null) return Rectangle.Empty;
                return canvas.ClientRectangle;
            }
        }
        void IView.ZoomToRect(BoundingRect World2D)
        {
            Rectangle screen = (this as IView).DisplayRectangle;
            double factor;
            // Höhe und Breite==0 soll einen Fehler liefern!
            if (World2D.Height == 0.0) factor = screen.Width / World2D.Width;
            else if (World2D.Width == 0.0) factor = screen.Height / World2D.Height;
            else
            {
                factor = Math.Min(screen.Width / World2D.Width, screen.Height / World2D.Height);
            }
            // wie ist das mit der Y-Richtung in screen?
            double dx = (screen.Right + screen.Left) / 2.0 - (World2D.Right + World2D.Left) / 2.0 * factor;
            double dy = (screen.Top + screen.Bottom) / 2.0 + (World2D.Top + World2D.Bottom) / 2.0 * factor;
            layoutToScreen = new ModOp2D(factor, 0.0, dx, 0.0, -factor, dy);
            screenToLayout = layoutToScreen.GetInverse(); // ginge auch einfacher
        }
        SnapPointFinder.DidSnapModes IView.AdjustPoint(System.Drawing.Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore)
        {
            // TODO:  Add LayoutView.AdjustPoint implementation
            WorldPoint = new GeoPoint();
            return SnapPointFinder.DidSnapModes.DidNotSnap;
        }
        SnapPointFinder.DidSnapModes IView.AdjustPoint(GeoPoint BasePoint, System.Drawing.Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore)
        {
            // TODO:  Add LayoutView.Condor.ICondorView.AdjustPoint implementation
            WorldPoint = new GeoPoint();
            return SnapPointFinder.DidSnapModes.DidNotSnap;
        }
        GeoObjectList IView.PickObjects(System.Drawing.Point MousePoint, PickMode pickMode)
        {
            // TODO:  Add LayoutView.PickObjects implementation
            return null;
        }
        IShowProperty IView.GetShowProperties(IFrame Frame)
        {
            // base.Frame = Frame;
            return this;
        }
        IGeoObject IView.LastSnapObject
        {
            get
            {
                return null;
            }
        }
        SnapPointFinder.DidSnapModes IView.LastSnapMode
        {
            get
            {
                return SnapPointFinder.DidSnapModes.DidNotSnap;
            }
        }
        string IView.Name
        {
            get
            {
                return Layout.Name;
            }
        }
        bool IView.AllowDrop => throw new NotImplementedException();

        bool IView.AllowDrag => throw new NotImplementedException();

        bool IView.AllowContextMenu => throw new NotImplementedException();
#endregion
#region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Zoom.Total":
                    {
                        ZoomTotal(1.1);
                        canvas.Invalidate();
                        return true;
                    }
                case "MenuId.Repaint":
                    {
                        if (!IsInitialized) ZoomTotal(1.1);
                        canvas.Invalidate();
                        return true;
                    }
                case "MenuId.LayoutView.Print":
                    OnPrint();
                    return true;
                case "MenuId.LayoutView.Print.Setup":
                    OnPrintSetup();
                    return true;
                case "MenuId.LayoutView.Print.SelectPrinter":
                    OnSelectPrinter();
                    return true;
                case "MenuId.LayoutView.Show":
                    base.Frame.ActiveView = this;
                    return true;
                case "MenuId.LayoutView.AddPatch":
                    layout.AddPatch(project.GetActiveModel(), new Projection(Projection.StandardProjection.FromTop), null, this);
                    subEntries = null;
                    if (propertyTreeView != null)
                    {
                        propertyTreeView.OpenSubEntries(this, false);
                        propertyTreeView.Refresh(this);
                        propertyTreeView.OpenSubEntries(this, true);
                        propertyTreeView.StartEditLabel(layout.Patches[layout.Patches.Length - 1]);
                    }
                    return true;
                case "MenuId.LayoutView.Rename":
                    propertyTreeView.StartEditLabel(this);
                    return true;
                case "MenuId.LayoutView.Remove":
                    project.RemoveLayout(this.layout);
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.LayoutView.Show":
                    CommandState.Checked = (base.Frame.ActiveView == this);
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
#endregion
        // Get real page bounds based on printable area of the page (http://www.ddj.com/dept/windows/184416821)
        static Rectangle GetRealPageBounds(PrintPageEventArgs e, bool preview)
        {
            // Return in units of 1/100th of an inch
            if (preview) return e.PageBounds;

            // Translate to units of 1/100th of an inch
            RectangleF vpb = e.Graphics.VisibleClipBounds;
            PointF[] bottomRight = { new PointF(vpb.Size.Width, vpb.Size.Height) };
            e.Graphics.TransformPoints(CoordinateSpace.Device, CoordinateSpace.Page, bottomRight);
            float dpiX = e.Graphics.DpiX;
            float dpiY = e.Graphics.DpiY;
            return new Rectangle(
              0,
              0,
              (int)(bottomRight[0].X * 100 / dpiX),
              (int)(bottomRight[0].Y * 100 / dpiY));
        }

        private void OnPrintPage(object sender, PrintPageEventArgs e)
        {
            Rectangle r = e.PageBounds; // ist IMMER in 100 dpi
            // die Pixelauflösung des Druckers ist in e.Graphics.DpiX bzw. DpiY
            double fctpap = 100 / 25.4;
            //double fct = 100 / 25.4;
            double fct = e.Graphics.DpiX / 25.4; // das ist die echte Auflösung

            // fct /= 2; war nur ein Test

            e.Graphics.PageScale = (float)(1.0);

            for (int i = 0; i < layout.PatchCount; ++i)
            {
                LayoutPatch lp = layout.Patches[i];
                e.Graphics.Transform = new System.Drawing.Drawing2D.Matrix((float)fctpap, 0.0f, 0.0f, (float)-fctpap, 0.0f, (float)(layout.PaperHeight * fctpap));

                BoundingRect ext;
                if (lp.Area != null)
                {
                    ext = lp.Area.Extent;
                }
                else
                {
                    ext = new BoundingRect(0.0, 0.0, layout.PaperWidth, layout.PaperHeight);
                }

                // der Faktor fct ist willkürlich, macht man ihn zu groß, gibt es ein OutOfMemory in OpenGl
                // zu klein, wird die Auflösung beim Drucken zu grob. Abhilfe würde es schaffen, wenn man nicht ein
                // Bitmap erzeugt, sondern dieses kachelt. Ob man es aber nahtlos zusammensetzen kann?
                // eigentlich gefragt wäre hier die Pixelauflösung des Graphics, aber vielleicht gibt es die garnicht
                // und es ist ein Metafile, der zum Drucker weitergereicht wird.
                GeoPoint2D ll = ext.GetLowerLeft();
                GeoPoint2D ur = ext.GetUpperRight();
                Rectangle clipRectangle = Rectangle.FromLTRB((int)ll.x, (int)ll.y, (int)ur.x, (int)ur.y);
                System.Drawing.Bitmap PaintToBitmap;
                int bitmapwidth = (int)(clipRectangle.Width * fct);
                int bitmapheight = (int)(clipRectangle.Height * fct);
                PaintToBitmap = new System.Drawing.Bitmap(bitmapwidth, bitmapheight);

                IPaintTo3D ipaintTo3D = Frame.UIService.CreatePaintInterface(PaintToBitmap, lp.Model.Extent.Size / 1000);
                ipaintTo3D.MakeCurrent();
                ipaintTo3D.Clear(Color.White);
                ipaintTo3D.AvoidColor(Color.White);

                Projection pr = lp.Projection.Clone();
                pr.SetUnscaledProjection(new ModOp(1, 0, 0, 0, 0, -1, 0, 0, 0, 0, 1, 0) * pr.UnscaledProjection);
                double factor, dx, dy;
                pr.GetPlacement(out factor, out dx, out dy);
                //new System.Drawing.Drawing2D.Matrix((float)factor, 0.0f, 0.0f, (float)-factor, 0.0f, (float)(layout.PaperHeight * factor)); 
                pr.SetPlacement(fct * factor, fct * (dx - ext.Left), fct * (dy - ext.Bottom));
                ipaintTo3D.SetProjection(pr, lp.Model.Extent);
                ipaintTo3D.UseZBuffer(true);
                ipaintTo3D.PaintFaces(PaintTo3D.PaintMode.All);
                ipaintTo3D.UseLineWidth = true;
                // hier darf nicht mit Listen gearbeitet werden, sonst macht OpanGL die Krätsche
                foreach (IGeoObject go in lp.Model)
                {
                    PaintFlatVisibleLayer(ipaintTo3D, go, lp.visibleLayers);
                }

                //CategorizedDislayLists displayLists = new CategorizedDislayLists();
                //// beim Zeichnen auf Bitmaps müssen die Displaylisten lokal zu diesem rendercontext gehören
                //// obwohl sharelists angegeben wurde und das keinen Fehler bringt
                //// leider wird alles hierbei flach. Man könnte das höchstens damit übertricksen,
                //// dass man bei tringle die Farbe an jedem Eckpunkt mit dem Normalenvektor verrechnet
                //// aber ob das hilft?
                //foreach (IGeoObject go in lp.Model)
                //{
                //    go.PaintTo3DList(ipaintTo3D, displayLists);
                //}
                //displayLists.Finish(ipaintTo3D);
                //ipaintTo3D.PaintFaces(PaintTo3D.PaintMode.FacesOnly);
                //foreach (KeyValuePair<Layer, IPaintTo3DList> kv in displayLists.layerFaceDisplayList)
                //{
                //    if (lp.IsLayerVisible(kv.Key) || displayLists.NullLayer == kv.Key)
                //    {
                //        ipaintTo3D.List(kv.Value);
                //    }
                //}
                //ipaintTo3D.PaintFaces(PaintTo3D.PaintMode.CurvesOnly);
                //foreach (KeyValuePair<Layer, IPaintTo3DList> kv in displayLists.layerCurveDisplayList)
                //{
                //    if (lp.IsLayerVisible(kv.Key) || displayLists.NullLayer == kv.Key)
                //    {
                //        ipaintTo3D.List(kv.Value);
                //    }
                //}
                //// wozu war das folgende noch?
                ////foreach (IGeoObject go in lp.Model)
                ////{
                ////    go.PaintTo3D(ipaintTo3D);
                ////}
                //// ipaintTo3D.Dispose(); gibt OpenGL Fehler
                //ipaintTo3D.FinishPaint();

                //gr.ReleaseHdc(dc);
                //displayLists = null;
                //System.GC.Collect();
                //System.GC.WaitForPendingFinalizers();
                ipaintTo3D.Dispose(); 

                PaintToBitmap.MakeTransparent(Color.White);
                //e.Graphics.DrawImageUnscaled(PaintToBitmap, clipRectangle);
                PointF[] dest = new PointF[3];
                dest[0].X = (float)ll.x;
                dest[0].Y = (float)ur.y;
                dest[1].X = (float)ur.x;
                dest[1].Y = (float)ur.y;
                dest[2].X = (float)ll.x;
                dest[2].Y = (float)ll.y;
                RectangleF src = new RectangleF(new PointF(0.0f, 0.0f), PaintToBitmap.Size);
                e.Graphics.DrawImage(PaintToBitmap, dest, src, GraphicsUnit.Pixel);
            }
        }

        private void PaintFlatVisibleLayer(IPaintTo3D iPaintTo3D, IGeoObject go, Hashtable visibleLayers)
        {
            // beim Drucken dürfen keine Displaylisten verwendet werden, denn es geht auf ein Bitmap
            // deshalb hier rekursiv runterbrechen, bis die Ebene klar ist und dann ohne Liste
            // auf iPaintTo3D ausgeben.
            if (go is Block)
            {
                Block blk = go as Block;
                for (int i = 0; i < blk.Count; ++i)
                {
                    PaintFlatVisibleLayer(iPaintTo3D, blk.Child(i), visibleLayers);
                }
            }
            else if (go is BlockRef)
            {
                PaintFlatVisibleLayer(iPaintTo3D, (go as BlockRef).Flattened, visibleLayers);
            }
            else
            {
                if (go.Layer == null || visibleLayers.ContainsKey(go.Layer))
                {
                    go.PrePaintTo3D(iPaintTo3D);
                    go.PaintTo3D(iPaintTo3D); // iPaintTo3D enthält die Information, dass keine Listen verwendet werden dürfen
                }
            }
        }

        private void OnPrint()
        {
            if (printDocument == null) printDocument = project.PrintDocument;
#pragma warning disable CS0618 // printing moves into CADability.Forms in 2.0 (issue #412)
            Print(printDocument);
#pragma warning restore CS0618
        }
        private void OnPrintSetup()
        {
            //psd.AllowPrinter = true;
            //psd.EnableMetric = true;
            if (printDocument == null) printDocument = project.PrintDocument;
            if (layout.PageSetup != null) layout.PageSetup.ApplyTo(printDocument);

#pragma warning disable CS0618 // the dialogs move into CADability.Forms in 2.0 (issue #412)
            if (Frame.UIService.ShowPageSetupDlg(ref printDocument, printDocument.DefaultPageSettings, out int width, out int height, out bool landscape) == DialogResult.OK)
#pragma warning restore CS0618
            {
                layout.PageSetup = PageSetup.FromPageSettings(printDocument.DefaultPageSettings); // saved with the layout
                //psd.Document.OriginAtMargins = false;

                if (landscape)
                {   // zumindest der pdf drucker liefert das nicht richtig
                    layout.PaperHeight = width * 0.254; // in hundertstel inch
                    layout.PaperWidth = height * 0.254; // in hundertstel inch
                }
                else
                {
                    layout.PaperWidth = width * 0.254; // in hundertstel inch
                    layout.PaperHeight = height * 0.254; // in hundertstel inch
                }
                // layout.pageSettings = psd.PageSettings;
                paperWidth.DoubleChanged();
                paperHeight.DoubleChanged();
                canvas?.Invalidate();
            }
        }
        private void OnSelectPrinter()
        {
            if (printDocument == null) printDocument = project.PrintDocument;
            //PrintDialog printDialog = new PrintDialog();
            //printDialog.Document = printDocument;
            //printDialog.AllowSomePages = false;
            //printDialog.AllowCurrentPage = false;
            //printDialog.AllowSelection = false;
            //printDialog.AllowPrintToFile = false;
#pragma warning disable CS0618 // the dialogs move into CADability.Forms in 2.0 (issue #412)
            if (Frame.UIService.ShowPrintDlg(ref printDocument) == DialogResult.OK)
#pragma warning restore CS0618
            {
                // printDocument = printDialog.Document; update in ShowPrintDlg
                layout.PageSetup = PageSetup.FromPageSettings(printDocument.DefaultPageSettings); // the printer is saved with the layout
            }
        }
        
        internal void Repaint()
        {
            if (!IsInitialized) ZoomTotal(1.1);
            canvas?.Invalidate();
        }

        internal new void Refresh()
        {
            subEntries = null;
            if (propertyTreeView != null)
            {
                bool isOpen = propertyTreeView.IsOpen(this);
                propertyTreeView.OpenSubEntries(this, false);
                propertyTreeView.Refresh(this);
                if (isOpen) propertyTreeView.OpenSubEntries(this, true);
                propertyTreeView.SelectEntry(this);
            }
        }
    }
}
#endif