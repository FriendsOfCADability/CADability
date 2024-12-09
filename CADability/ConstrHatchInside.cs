using CADability.Attribute;
using CADability.GeoObject;
using CADability.Shapes;
using System.Threading;
using Wintellect.PowerCollections;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>

    public class ConstrHatchInside : CADability.Actions.ConstructAction
    {
        // wenn debug==true, dann wird nur beim Mausklick
        // FindShape aufgerufen und kein thread benutzt
        private static bool debug = true;
        // also, wenn er hängt und man "Debug->Detach" macht, dann läuft er weiter
        // scheint also so, als ob der Debugger das Problem verursacht
        /// <summary>
        /// A mode used when creating shapes from curves.
        /// </summary>
        public enum HatchMode
        {
            /// <summary>
            /// A simple shape without holes and in case of an inner point the smallest shape that contains this point
            /// </summary>
            simple,
            /// <summary>
            /// All holes which are contained in the shape are also included in the result
            /// </summary>
            excludeHoles,
            /// <summary>
            /// The simple biggest shape without holes defined by the curves and which in case of a provided inner point contains that point
            /// </summary>
            hull,
            /// <summary>
            /// All shapes that cane be created from the provided curves.
            /// </summary>
            allShapes
        }
        private HatchMode mode;
        private Plane foundOnPlane;
        private ConstrHatchInside() // damit keine automatische Wiederholung möglich, Copykonstruktor!
        {
            shapeFound = new ShapeFoundDelegate(OnShapeFound);
        }
        public ConstrHatchInside(HatchMode mode)
            : this()
        {
            this.mode = mode;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.GetID ()"/>
        /// </summary>
        /// <returns></returns>
        public override string GetID()
        {
            return "Constr.Hatch.InnerPoint";
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
        /// </summary>
        public override void OnSetAction()
        {
            // die Titel Ids müssen sich unterscheiden, auch wg. der Hilfe
            string resSubId = "";
            switch (mode)
            {
                case HatchMode.simple:
                    resSubId = "InnerPoint";
                    break;
                case HatchMode.hull:
                    resSubId = "Hull";
                    break;
                case HatchMode.excludeHoles:
                    resSubId = "ExcludeHoles";
                    break;
            }
            base.TitleId = "Constr.Hatch." + resSubId;
            ConstructAction.GeoPointInput gpi = new GeoPointInput("Constr.Hatch.InnerPoint.Point");
            gpi.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnPoint);
            gpi.MouseClickEvent += new MouseClickDelegate(OnMouseClick);
            base.UseFilter = true;
            base.ShowAttributes = true;
            base.ShowActiveObject = false;
            base.ActiveObject = Hatch.Construct();
            base.SetInput(gpi);
            base.OnSetAction();
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.AutoRepeat ()"/>
        /// </summary>
        /// <returns></returns>
        public override bool AutoRepeat()
        {
            return false;
        }
        private delegate void ShapeFoundDelegate(CompoundShape cs, Plane plane, GeoPoint fromThisPoint);
        private ShapeFoundDelegate shapeFound;
        private bool findShapeIsRunning;
        private Thread findShapeThread;
        private void OnShapeFound(CompoundShape cs, Plane plane, GeoPoint fromThisPoint)
        {
            Hatch h = base.ActiveObject as Hatch;
            if (h == null) return; // woher kommt das (direkt beim Klicken nach dem Einfügen)
            h.CompoundShape = cs;
            h.Plane = foundOnPlane; // immer auf der ProjectionPlane arbeiten, dort wurde die Schraffur ja gefunden
            GeoPoint[] src = new GeoPoint[3];
            GeoPoint[] dst = new GeoPoint[3];
            src[0] = foundOnPlane.ToGlobal(GeoPoint2D.Origin);
            src[1] = foundOnPlane.ToGlobal(GeoPoint2D.Origin + GeoVector2D.XAxis);
            src[2] = foundOnPlane.ToGlobal(GeoPoint2D.Origin + GeoVector2D.YAxis);
            for (int i = 0; i < 3; ++i)
            {
                dst[i] = plane.Intersect(src[i], foundOnPlane.Normal);
            }
            ModOp toPlane = ModOp.Fit(src, dst, true);
            h.Modify(toPlane);
            h.Recalc();
            base.ShowActiveObject = true;
            // System.Diagnostics.Debug.WriteLine("Shape found");
        }
        private void FindShape()
        {	// wird in einem zweiten Thread aufgerufen. Ob es Probleme macht, dass von dieser
            // Methode ausgehend indirekt OpenCascade verwendet wird? Um zu verhindern, dass
            // gleichzeitig mehrere Methoden von OpenCascade laufen müsste man ein "lock" mit einem
            // globalen Objekt der OpenCascade Bibliothek machen...
            try
            {
                Frame.ActiveView.Canvas.Cursor = "WaitCursor";
                GeoPoint p = base.CurrentMousePosition;
                foundOnPlane = CurrentMouseView.Projection.ProjectionPlane; // bei mehreren Fenstern so nicht gut!!!
                GeoPoint2D onPlane = foundOnPlane.Project(p);
                // hier müsste man irgendwie erst wenig picken und wenn nix geht dann immer mehr
                BoundingRect pickrect = new BoundingRect(onPlane, base.WorldViewSize, base.WorldViewSize);
                Set<Layer> visibleLayers = null;
                if (Frame.ActiveView is ModelView)
                {
                    visibleLayers = new Set<Layer>((Frame.ActiveView as ModelView).GetVisibleLayers());
                }
#if !WEBASSEMBLY
                else if (Frame.ActiveView is GDI2DView)
                {
                    visibleLayers = new Set<Layer>((Frame.ActiveView as GDI2DView).VisibleLayers.Checked);
                }
#endif
                GeoObjectList l = Frame.ActiveView.Model.GetObjectsFromRect(pickrect, CurrentMouseView.Projection, visibleLayers, PickMode.normal, Frame.Project.FilterList);
                // Problem: ein großese Rechteck, welches weit über pickrect hinausgeht hat Inseln, die nicht im pickrect liegen
                // Diese werden nicht gefunden. Deshalb hier nochmal das pickrect ggf. erweitern
                pickrect = l.GetExtent(CurrentMouseView.Projection, true, false);
                // l = Frame.ActiveView.ProjectedModel.GetObjectsFromRect(pickrect, null);
                l = Frame.ActiveView.Model.GetObjectsFromRect(pickrect, CurrentMouseView.Projection, visibleLayers, PickMode.normal, Frame.Project.FilterList);
                l.DecomposeBlocks(false);
                // l.Reduce(Frame.Project.FilterList); // FilterList jetzt schon beim Picken
                // CompoundShape cs = CompoundShape.CreateFromConnectedList(l, foundOnPlane, onPlane, l.GetExtent().Size * 1e-6, mode);
                CompoundShape cs = null;
                if (cs == null)
                {
                    CurveGraph cg = CurveGraph.CrackCurves(l, foundOnPlane, l.GetExtent().Size * 1e-6); // gap eine Ordnung größer als Precision
                    if (cg != null)
                    {
                        cs = cg.CreateCompoundShape(true, onPlane, mode, false);
#if DEBUG
                        if (cs == null)
                        {
                            cg = CurveGraph.CrackCurves(l, foundOnPlane, l.GetExtent().Size * 1e-6); // gap eine Ordnung größer als Precision
                            cs = cg.CreateCompoundShape(true, onPlane, mode, false);
                        }
#endif
                    }
                    else
                    {
                        cg = CurveGraph.CrackCurves(l, foundOnPlane, l.GetExtent().Size * 1e-6); // gap eine Ordnung größer als Precision
                    }
                }
                if (cs != null)
                {
                    Plane shapesPlane;
                    Plane pln;
                    if (cs != null)
                    {
                        // das BeginInvoke synchronisiert mit den MouseMessages, d.h.
                        // OnShapeFound wird nur aufgerufen, wenn alle Messages
                        // abgearbeitet sind, er also nicht in irgendeiner anderen
                        // Methode dieser Klasse steckt. Denn in OnShapeFound wird
                        // ActiveObject gesetzt und das geht nur synchron mit dem Control.
                        if (Curves.GetCommonPlane(cs.Curves3d(), out shapesPlane))
                        {
                            //cs = cs.Project(foundOnPlane, shapesPlane);
                            pln = shapesPlane;
                        }
                        else
                        {
                            pln = CurrentMouseView.Projection.DrawingPlane;
                        }
                        if (debug)
                        {
                            OnShapeFound(cs, pln, p);
                        }
                        else
                        {
                            OnShapeFound(cs, pln, p);
                        }
                    }
                }
                findShapeIsRunning = false;
            }
            catch (ThreadAbortException)
            {
                findShapeIsRunning = false;
            }
            catch (System.Exception e)
            {
                string dbg = e.Message;
                findShapeIsRunning = false;
            }
            finally
            {	// aus welchen Gründen könnte er hier rausfliegen???
                findShapeIsRunning = false;
            }
        }
        private void OnPoint(GeoPoint p)
        {
            if (debug)
            {
                // FindShape();
                return; // keinen thread starten, nur bei Mausklick rechnen
            }
            if (base.ShowActiveObject)
            {
                Plane pln = Frame.ActiveView.Projection.DrawingPlane;
                GeoPoint2D onPlane = pln.Project(p);
                Hatch h = ActiveObject as Hatch;
                if (h.CompoundShape != null)
                {
                    lock (this)
                    {
                        if (!h.CompoundShape.Contains(onPlane, true))
                        {
                            base.ShowActiveObject = false;
                        }
                    }
                }
            }

            if (findShapeIsRunning)
            {
                lock (this)
                {
                    if (findShapeThread.ThreadState == ThreadState.Stopped)
                    {
                        findShapeIsRunning = false;
                        return; // eingeführt am 7.6.06, da keine Reaktion bei der Mausbewegung
                        // ThreadState ist ThreadState.Stopped aber findShapeIsRunning ist true und blieb auch so
                    }
                    if (findShapeThread.ThreadState != ThreadState.Running) return;
                    try
                    {
                        findShapeThread.Abort();
                    }
                    catch (System.Threading.ThreadStateException)
                    {
                        findShapeIsRunning = false;
                        return; // keinen weiteren thread starten
                    }
                }
                try
                {
                    findShapeThread.Join();
                }
                catch (System.Threading.ThreadStateException)
                {
                    findShapeIsRunning = false;
                    return; // keinen weiteren thread starten
                }
            }
            try
            {
                lock (this)
                {
                    findShapeThread = new Thread(new ThreadStart(FindShape));
                    findShapeThread.Start();
                    findShapeIsRunning = true;
                }
            }
            catch (System.Exception e)
            {	// kommt nicht hierhin
                string msg = e.Message;
                findShapeIsRunning = false;
            }
        }
        private void OnMouseClick(bool up, GeoPoint MousePosition, IView View)
        {
            if (up)
            {
                if (base.ShowActiveObject)
                {
                    Plane pln = Frame.ActiveView.Projection.DrawingPlane;
                    GeoPoint2D onPlane = pln.Project(MousePosition);
                    Hatch h = ActiveObject as Hatch;
                    // GeoObjectVisualizer.TestShowVisualizer(h);
                    if (!h.CompoundShape.Contains(onPlane, true))
                    {
                        base.ShowActiveObject = false;
                    }
                    else
                    {	// das ActiveObject wird ja eingefügt, wenn die Aktion zu Ende ist
                    }
                }
                else
                {
                    if (debug)
                    {
                        FindShape();
                        if (base.ShowActiveObject)
                        {
                            // System.Diagnostics.Trace.WriteLine("Schraffur wird zugefügt: " + (base.ActiveObject as Hatch).CompoundShape.Area.ToString());
                            base.OnDone(); // ActiveObject ist OK
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnRemoveAction ()"/>
        /// </summary>
        public override void OnRemoveAction()
        {
            if (findShapeIsRunning)
            {
                try
                {
                    findShapeThread.Abort();
                    // findShapeThread.Join(); // wäre hier vielleicht nicht mehr nötig
                }
                catch (ThreadStateException) { }
            }
            base.OnRemoveAction();
        }

    }
}
