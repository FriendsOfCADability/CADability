using CADability.Attribute;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Runtime.Serialization;
using Wintellect.PowerCollections;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;
using PaintEventArgs = CADability.Substitutes.PaintEventArgs;
using CADability.Substitutes;

namespace CADability
{

    /* KONZEPT
     * GeoObjekte können einen Actuator (IDrive) haben. Das Modell hat eine Liste aller Drives.
     * Die Drives sind u.U. voneinander abhängig. Daraus ergibt sich ein Baum.
     * Vor der Simulation wird zu jedem Drive eine Displaylist erzeugt. Während des
     * Ablaufs wird für jeden Drive die ModOp bestimmt, die sich aus den Abhängigkeiten 
     * und den einzelnen ModOps ergibt.
     * Die DisplayListen werden mit den ModOps versehen dargestellt. (Kanten/Flächen unterscheidung?)
     * Einige Objekte werden jeweils gesondert ausgegeben (wg. Farbänderung)
     * 
     * Das Modell enthält eine Liste aller Drives.
     * Die Ansicht enthält einen Ablaufplan Schedule
    */


    /// <summary>
    /// A view in which mechanical dependencies of objects can be defined and animated.
    /// </summary>
    [Serializable]
    public class AnimatedView : IShowPropertyImpl, IView, ICommandHandler, ISerializable
    {
        private Project project;
        private Model model;
        private Projection projection; // die gerade gülitge Projektion
        private IPaintTo3D paintToOpenGl; // die OpenGL Maschine
        private Dictionary<IDrive, CategorizedDislayLists> displayLists;
        private CheckedLayerList visibleLayers;
        private GeoObjectList highlightedObjects;
        private Color highlightColor;
        private IPaintTo3DList highlightList;
        private string name;
        private Color? backgroundColor;
        // für andere Actionen
        public event PaintView PaintDrawingEvent;
        public event PaintView PaintSelectEvent;
        public event PaintView PaintActiveEvent;
        public event PaintView PaintBackgroundEvent;
        // Zoom und Scroll
        private Point lastPanPosition;
        private GeoPoint fixPoint;
        private bool fixPointValid;
        private static double mouseWheelZoomFactor = Settings.GlobalSettings.GetDoubleValue("MouseWheelZoomFactor", 1.1);
        // Select
        private bool selectionEnabled = false;
        private GeoObjectList selectedObjects;
        private GeoObjectList objectsOnLButtonDown;
        private IPaintTo3DList displayList;
        private Color selectColor;
        private int selectWidth;
        private int highlightWidth;
        private int dragWidth;
        private Point downPoint; // Position des letzten MouseDown
        // Animation läuft
        private bool isRunning;
        private bool isPaused;
        ISchedule schedule;
        double timeBase; // basis für die Zeitbestimmung, so dass time = (tc - timeBase) / 1000.0 * speed;
        double startTime;
        double endTime;
        double speed;
        double currentTime;
        ShowPropertySchedules schedulesProperty;
        class NullDrive : IDrive
        {   // alle statischen Objekte gehören zu diesem Drive
            string name;
            public NullDrive()
            {
                name = System.Guid.NewGuid().ToString();
            }
            #region IDrive Members
            string IDrive.Name
            {
                get
                {
                    return name;
                }
                set
                {
                    throw new Exception("The method or operation is not implemented.");
                }
            }
            IDrive IDrive.DependsOn
            {
                get
                {
                    return null;
                }
                set
                {
                    throw new Exception("The method or operation is not implemented.");
                }
            }
            double IDrive.Position
            {
                get
                {
                    throw new Exception("The method or operation is not implemented.");
                }
                set
                {
                    throw new Exception("The method or operation is not implemented.");
                }
            }
            ModOp IDrive.Movement
            {
                get
                {
                    return ModOp.Identity;
                }
            }
            List<IGeoObject> IDrive.MovedObjects
            {
                get
                {
                    return null;
                }
            }
            #endregion
        }
        private NullDrive staticObjects;
        protected AnimatedView()
        {
            staticObjects = new NullDrive();
            highlightedObjects = new GeoObjectList();
            selectedObjects = new GeoObjectList();

            selectColor = Frame.GetColorSetting("Select.SelectColor", Color.Yellow); // die Farbe für die selektierten Objekte
            selectWidth = Frame.GetIntSetting("Select.SelectWidth", 2);
            dragWidth = Frame.GetIntSetting("Select.DragWidth", 5);
            projection = new Projection(Projection.StandardProjection.Isometric);
            highlightWidth = 0;
            base.resourceIdInternal = "AnimatedView";
        }
        /// <summary>
        /// Creates a new AnimatedView object. In oder to display this view on the screen you need to add this view to a
        /// <see cref="IFrame"/> and set it as the <see cref="IFrame.ActiveView"/>.
        /// </summary>
        /// <param name="project">The project that contains the lists of all schedules (if needed)</param>
        /// <param name="model">The model that is displayed and contains the list of all drives</param>
        /// <param name="frame">The frame which is the context of this view</param>
        public AnimatedView(Project project, Model model, IFrame frame)
            : this()
        {
            this.project = project;
            this.model = model;
            visibleLayers = new CheckedLayerList(project.LayerList, project.LayerList.ToArray(), "AnimatedView.VisibleLayers");
            visibleLayers.CheckStateChangedEvent += new CheckedLayerList.CheckStateChangedDelegate(OnVisibleLayersChanged);
            projection.Precision = model.Extent.Size / 10000; // damit es nicht 0 ist
        }
        /// <summary>
        /// Name of this AnimatedView as shown in the controlcenter
        /// </summary>
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
        /// <summary>
        /// Backgroundcolor to override the default background color as defined in the global settings
        /// </summary>
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
        /// <summary>
        /// Adds the provided object to the highlighted objects of this view. This is typically called during
        /// simulation/animation to draw the user attention to this object
        /// </summary>
        /// <param name="toHighlight">highlighted object to add</param>
        public void AddHighlightedObject(IGeoObject toHighlight)
        {
            highlightedObjects.Add(toHighlight);
            highlightList = null; // neu machen
        }
        /// <summary>
        /// Remove the highlighted object that was previously added
        /// </summary>
        /// <param name="toRemove">highlighted object to remove</param>
        public void RemoveHighlightedObject(IGeoObject toRemove)
        {
            highlightedObjects.Remove(toRemove);
            highlightList = null; // neu machen
        }
        public void ClearHighlightedObjects()
        {
            highlightedObjects.Clear();
            highlightList = null; // neu machen
        }
        /// <summary>
        /// Color of the highlighted objects
        /// </summary>
        public Color HighlightColor
        {
            get
            {
                return highlightColor;
            }
            set
            {
                highlightColor = value;
            }
        }
        public int HighlightWidth
        {
            get
            {
                return highlightWidth;
            }
            set
            {
                highlightWidth = value;
            }

        }
        /// <summary>
        /// List of visible layers. Modify visible layers using <see cref="CheckedLayerList.Set"/>
        /// </summary>
        public CheckedLayerList VisibleLayers
        {
            get
            {
                return visibleLayers;
            }
        }
        /// <summary>
        /// True while the animation/simulation is running.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return isRunning;
            }
        }
        /// <summary>
        /// True while the anumation is paused
        /// </summary>
        public bool IsPaused
        {
            get
            {
                return isPaused;
            }
        }
        /// <summary>
        /// Delegate for the <see cref="NextStepEvent"/>
        /// </summary>
        /// <param name="sender">The calling AnimtedView</param>
        /// <param name="time">The current time in the sense of the simulation</param>
        public delegate void NextStepDelegate(AnimatedView sender, double time);
        /// <summary>
        /// Event beeing raised on each frame update of the animation
        /// </summary>
        public event NextStepDelegate NextStepEvent;
        /// <summary>
        /// Delegate for the <see cref="GetTimeEvent"/>. If not handeled the time is 
        /// determined by the internal clock.
        /// </summary>
        /// <param name="sender">The calling AnimtedView</param>
        /// <returns>The current time in seconds</returns>
        public delegate double GetTimeDelegate(AnimatedView sender);
        /// <summary>
        /// Event beeing raised on each frame update of the animation
        /// </summary>
        public event GetTimeDelegate GetTimeEvent;
        private void OnVisibleLayersChanged(Layer layer, bool isChecked)
        {
            canvas?.Invalidate();
        }
        private void PrepareDisplayLists()
        {
            displayLists = new Dictionary<IDrive, CategorizedDislayLists>();
            Dictionary<IDrive, List<IGeoObject>> clsssified = new Dictionary<IDrive, List<IGeoObject>>();
            List<IGeoObject> withNoDrive = new List<IGeoObject>();
            foreach (IGeoObject go in model)
            {
                IDrive dv = go.Actuator;
                if (dv == null) dv = staticObjects;
                List<IGeoObject> toAdd;
                if (!clsssified.TryGetValue(dv, out toAdd))
                {
                    toAdd = new List<IGeoObject>();
                    clsssified[dv] = toAdd; ;
                }
                toAdd.Add(go);
            }
            foreach (KeyValuePair<IDrive, List<IGeoObject>> kv in clsssified)
            {
                CategorizedDislayLists categorizedDislayLists = new CategorizedDislayLists();
                foreach (IGeoObject go in kv.Value)
                {
                    if (go.IsVisible) go.PaintTo3DList(paintToOpenGl, categorizedDislayLists);
                }
                categorizedDislayLists.Finish(paintToOpenGl);
                displayLists[kv.Key] = categorizedDislayLists;
            }
        }
        /// <summary>
        /// Starts the realtime animation. <paramref name="speed"/> provides a time factor, 
        /// 1.0 is real time. The method returns immediately. There may bee zooming and scrolling 
        /// during the animation. The animation may be stopped at any time and stops automatically 
        /// when endTime is reached. Each discrete frame that is displayed fires the 
        /// <see cref="NextStepEvent"/> to enable the user of this class to provide some
        /// additional display changes or other tasks.
        /// </summary>
        /// <param name="startTime">Start time for the animation</param>
        /// <param name="endTime">Stop time for the animation</param>
        /// <param name="speed">Speed factor</param>
        public void StartAnimation(ISchedule schedule, double startTime, double endTime, double speed)
        {
            this.schedule = schedule;
            this.startTime = startTime;
            this.endTime = endTime;
            this.speed = speed;
            isRunning = true;
            int tc = System.Environment.TickCount;
            timeBase = tc;
            currentTime = startTime;
            Frame.UIService.ApplicationIdle += new EventHandler(OnApplicationIdle);
        }
        /// <summary>
        /// Pauses the animation. All drive positions remain unchanged, the time stops.
        /// </summary>
        public void PauseAnimation()
        {
            Frame.UIService.ApplicationIdle -= new EventHandler(OnApplicationIdle);
            isPaused = true;
        }
        /// <summary>
        /// Resume a previously paused animation
        /// </summary>
        public void ResumeAnimation()
        {
            int tc = System.Environment.TickCount;
            timeBase = tc - (currentTime - startTime) / speed * 1000.0;
            // das war geändert in die folgende Zeile, hat vermutlich was mit Irmler zu tun. Jetzt 
            // wieder rückgängig gemacht auf die obere Zeile wg. Bazzi, macht hoffentlich keine Probleme.
            //timeBase = tc;
            Frame.UIService.ApplicationIdle += new EventHandler(OnApplicationIdle);
            isPaused = false;
        }
        /// <summary>
        /// Stop the animation. All objects return to the starting position
        /// </summary>
        public void StopAnimation()
        {
            isRunning = false;
            Frame.UIService.ApplicationIdle -= new EventHandler(OnApplicationIdle);
            canvas?.Invalidate();
        }
        public void ReassembleDisplayList()
        {
            displayLists = null;
        }
        /// <summary>
        /// Tests the collision of two solids. The current position of the drives is applied to both objects 
        /// (typically one of the objects is static). If there is a collision, true is returned 
        /// and the <paramref name="collisionPoint"/> is filled with an arbitrary point where collision
        /// takes place.
        /// </summary>
        /// <param name="firstObject">First Solid of the test</param>
        /// <param name="secondObject">Sirst Solid of the test</param>
        /// <param name="collisionPoint">Collision point</param>
        /// <returns>True if there is a collision, false otherwise</returns>
        public bool Collision(Solid firstObject, Solid secondObject, double precision, out GeoPoint collisionPoint)
        {
            // wir nehmen an, dass alle DrivePositions gesetzt sind, das ist Aufgabe des Aufrufers
            Solid clone1 = firstObject.Clone() as Solid;
            Solid clone2 = secondObject.Clone() as Solid;
            if (firstObject.Actuator != null)
            {
                ModOp m = firstObject.Actuator.Movement;
                IDrive drv = firstObject.Actuator.DependsOn;
                while (drv != null)
                {
                    m = m * drv.Movement;
                    drv = drv.DependsOn;
                }
                clone1.Modify(m);
            }
            if (secondObject.Actuator != null)
            {
                ModOp m = secondObject.Actuator.Movement;
                IDrive drv = secondObject.Actuator.DependsOn;
                while (drv != null)
                {
                    m = m * drv.Movement;
                    drv = drv.DependsOn;
                }
                clone2.Modify(m);
            }
            // erstmal auf OCas zurückgeführt...
            //Solid[] ints = Make3D.Intersection(clone1, clone2);
            //if (ints != null && ints.Length > 0)
            //{
            //    Vertex[] v = ints[0].Shells[0].Vertices;
            //    if (v.Length > 0)
            //    {
            //        collisionPoint = v[0].Position;
            //        return true;
            //    }
            //}
            //collisionPoint = GeoPoint.Origin;
            //return false;
            // mit BRepOperation gibts z.Z. ein Problem: 23.4. mal wieder freigegeben
            //BRepOperation bro = new BRepOperation(clone1.Shells[0], clone2.Shells[0], BRepOperation.Operation.testonly);
            //return bro.Intersect(out collisionPoint);
            CollisionDetection cd = new CollisionDetection(clone1.Shells[0], clone2.Shells[0]);
            return cd.GetResult(precision, out collisionPoint);
        }
        /// <summary>
        /// List of all drives defined in this context.
        /// </summary>
        public DriveList DriveList
        {
            get
            {
                return model.AllDrives;
            }
        }
        /// <summary>
        /// Set or get the current speed factor (1.0 is normal)
        /// </summary>
        public double Speed
        {
            get
            {
                return speed;
            }
            set
            {
                lock (this)
                {
                    speed = value;
                    if (IsRunning)
                    {
                        int tc = System.Environment.TickCount;
                        timeBase = tc - currentTime / speed * 1000.0;
                        // geändert wg. Bazzi
                        // timeBase = tc;
                    }
                }
            }
        }
        private GeoObjectList ObjectsUnderCursor(MouseEventArgs e)
        {
            GeoObjectList res = new GeoObjectList();

            if (selectionEnabled)
            {
                BoundingRect pickrect = projection.BoundingRectWorld2d(e.X - 5, e.X + 5, e.Y + 5, e.Y - 5);
                Projection.PickArea area = projection.GetPickSpace(pickrect);

                res = model.GetObjectsFromRect(area, new Set<Layer>(visibleLayers.Checked), PickMode.single, null);
            }

            return res;
        }
        public bool SelectionEnabled
        {
            get { return selectionEnabled; }
            set { selectionEnabled = value; }
        }
        public GeoObjectList DraggingObjects
        {
            get
            {
                return objectsOnLButtonDown;
            }
        }
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
                ext = ext * factor;
                projection.SetPlacement(canvas.ClientRectangle, ext);
                canvas.Invalidate();
            }
        }
        /// <summary>
        /// Sets or gets the fixpoint for interactive view direction changes
        /// </summary>
        public GeoPoint FixPoint
        {
            get
            {
                return fixPoint;
            }
            set
            {
                fixPoint = value;
                fixPointValid = true;
            }
        }
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
        #region IView Members
        ProjectedModel IView.ProjectedModel
        {   // sollte nicht aufgerufen werden
            get { throw new Exception("The method or operation is not implemented."); }
        }
        Model IView.Model
        {
            get
            {
                return model;
            }
        }
        Projection IView.Projection
        {
            get
            {
                return projection;
            }
            set
            {
                projection = value.Clone();
            }
        }
        void IView.SetCursor(string cursor)
        {
            canvas.Cursor = cursor;
        }
        void IView.Invalidate(PaintBuffer.DrawingAspect aspect, Rectangle ToInvalidate)
        {
            displayLists = null; // force recalculation
            canvas?.Invalidate();
        }
        void IView.InvalidateAll()
        {
            displayLists = null; // force recalculation
            canvas?.Invalidate();
        }
        //void IView.SetPaintHandler(PaintBuffer.DrawingAspect aspect, RepaintView PaintHandler)
        //{
        //    throw new Exception("The method or operation is not implemented.");
        //}
        //void IView.RemovePaintHandler(PaintBuffer.DrawingAspect aspect, RepaintView PaintHandler)
        //{

        //}
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
        Rectangle IView.DisplayRectangle
        {
            get
            {
                return canvas.ClientRectangle;
            }
        }
        void IView.ZoomToRect(BoundingRect World2D)
        {
            if (World2D.Width + World2D.Height < 1e-6) return;
            projection.SetPlacement((this as IView).DisplayRectangle, World2D);
            (this as IView).Invalidate(PaintBuffer.DrawingAspect.All, (this as IView).DisplayRectangle);
            // RecalcScrollPosition();
        }
        SnapPointFinder.DidSnapModes IView.AdjustPoint(Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore)
        {
            SnapPointFinder spf = new SnapPointFinder();
            spf.FilterList = Frame.Project.FilterList;
            spf.Init(MousePoint, projection, Frame.SnapMode, 5);
            spf.SnapMode = Frame.SnapMode;
            spf.Snap30 = Frame.GetBooleanSetting("Snap.Circle30", false);
            spf.Snap45 = Frame.GetBooleanSetting("Snap.Circle45", false);
            spf.SnapLocalOrigin = Frame.GetBooleanSetting("Snap.SnapLocalOrigin", false);
            spf.SnapGlobalOrigin = Frame.GetBooleanSetting("Snap.SnapGlobalOrigin", false);
            spf.IgnoreList = ToIgnore;
            model.AdjustPoint(spf, new Set<Layer>(visibleLayers.Checked));
            WorldPoint = spf.SnapPoint; // ist auch gesetzt, wenn nicht gefangen (gemäß DrawingPlane)
            //lastSnapObject = spf.BestObject;
            //lastSnapMode = spf.DidSnap;
            return spf.DidSnap;
        }
        SnapPointFinder.DidSnapModes IView.AdjustPoint(GeoPoint BasePoint, Point MousePoint, out GeoPoint WorldPoint, GeoObjectList ToIgnore)
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
            model.AdjustPoint(spf, new Set<Layer>(visibleLayers.Checked));
            WorldPoint = spf.SnapPoint;
            //lastSnapObject = spf.BestObject;
            //lastSnapMode = spf.DidSnap;
            return spf.DidSnap;
        }
        GeoObjectList IView.PickObjects(Point MousePoint, PickMode pickMode)
        {
            if (isRunning)
            {   // hier müsste man eine Kopie des Models mit den Positionen erzeugen und dort picken
                // und man müsste netürliche die Originalobjekte zurückliefern, was gerade bei verschachtelten
                // Objekten (Solid, Block) recht schwierig ist. 
                return null;
            }
            else
            {
                if (selectionEnabled)
                {
                    GeoPoint2D p0 = projection.PointWorld2D(MousePoint);
                    double d = 5.0 * projection.DeviceToWorldFactor;
                    BoundingRect pickrect = new BoundingRect(p0, d, d);
                    return model.GetObjectsFromRect(pickrect, projection, null, pickMode, null);
                }
                else
                {
                    return null;
                }
            }
        }
        CADability.UserInterface.IShowProperty IView.GetShowProperties(IFrame Frame)
        {
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
        public event CADability.ScrollPositionChanged ScrollPositionChangedEvent;
        private void Scroll(double dx, double dy)
        {
            projection.MovePlacement(dx, dy);
        }
        #endregion
        private ICanvas canvas;
        void IView.Connect(ICanvas canvas)
        {
            this.paintToOpenGl = canvas.PaintTo3D;
            this.canvas = canvas;
        }
        void IView.Disconnect(ICanvas canvas)
        {
        }
        ICanvas IView.Canvas => canvas;
        string IView.PaintType => "3D";
        #region ICondorViewInternal Members
        bool IView.AllowDrop => false;

        bool IView.AllowDrag => false;

        bool IView.AllowContextMenu => false;
        public Substitutes.DragDropEffects DoDragDrop(GeoObjectList dragList, Substitutes.DragDropEffects all)
        {
            return DragDropEffects.None;
        }
        void IView.OnPaint(PaintEventArgs e)
        {
            if (paintToOpenGl == null) return;
            IPaintTo3D ipaintTo3D = paintToOpenGl;
            ipaintTo3D.DontRecalcTriangulation = true;

            ipaintTo3D.Clear(BackgroundColor);

            ipaintTo3D.UseZBuffer(true);
            BoundingCube bc = model.Extent;
            bc.MinMax(model.MinExtend);
            // sicherstellen, dass die komplette Rasterebene auch mit angezeigt wird
            BoundingRect ext = BoundingRect.EmptyBoundingRect;
            ext.MinMax(projection.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymin, bc.Zmin)));
            ext.MinMax(projection.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymin, bc.Zmax)));
            ext.MinMax(projection.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymax, bc.Zmin)));
            ext.MinMax(projection.DrawingPlane.Project(new GeoPoint(bc.Xmin, bc.Ymax, bc.Zmax)));
            ext.MinMax(projection.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymin, bc.Zmin)));
            ext.MinMax(projection.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymin, bc.Zmax)));
            ext.MinMax(projection.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymax, bc.Zmin)));
            ext.MinMax(projection.DrawingPlane.Project(new GeoPoint(bc.Xmax, bc.Ymax, bc.Zmax)));
            ext.Inflate(ext.Width, ext.Height);
            bc.MinMax(projection.DrawingPlane.ToGlobal(new GeoPoint2D(ext.Left, ext.Bottom)));
            bc.MinMax(projection.DrawingPlane.ToGlobal(new GeoPoint2D(ext.Left, ext.Top)));
            bc.MinMax(projection.DrawingPlane.ToGlobal(new GeoPoint2D(ext.Right, ext.Bottom)));
            bc.MinMax(projection.DrawingPlane.ToGlobal(new GeoPoint2D(ext.Right, ext.Top)));

            ipaintTo3D.SetProjection(projection, bc);
            double precisionFactor = Settings.GlobalSettings.GetDoubleValue("HighestDisplayPrecision", 1e5);
            ipaintTo3D.Precision = Math.Max(1.0 / projection.WorldToDeviceFactor, model.Extent.MaxSide / precisionFactor);


            if (displayLists == null) PrepareDisplayLists();

            foreach (KeyValuePair<IDrive, CategorizedDislayLists> kv in displayLists)
            {   // alle drives der Reihe nach
                ModOp m = ModOp.Identity;
                if (isRunning)
                {
                    m = kv.Key.Movement;
                    IDrive dv = kv.Key.DependsOn;
                    while (dv != null)
                    {
                        m = dv.Movement * m; // richtigrum??
                        dv = dv.DependsOn;
                    }
                }
                ipaintTo3D.PushMultModOp(m);
                foreach (KeyValuePair<Layer, IPaintTo3DList> de in kv.Value.layerCurveDisplayList)
                {
                    if (visibleLayers.IsLayerChecked(de.Key) || de.Key == kv.Value.NullLayer)
                    {
                        ipaintTo3D.List(de.Value);
                    }
                }
                ipaintTo3D.PopModOp();
                using (ipaintTo3D.FacesBehindEdgesOffset)
                {   // das ist die richtige Reihenfoöge der Matritzen, mit "ipaintTo3D.Precision = 1.0;" ausprobiert
                    ipaintTo3D.PushMultModOp(m);
                    foreach (KeyValuePair<Layer, IPaintTo3DList> de in kv.Value.layerFaceDisplayList)
                    {
                        if (visibleLayers.IsLayerChecked(de.Key) || de.Key == kv.Value.NullLayer)
                        {
                            ipaintTo3D.List(de.Value);
                        }
                    }
                    ipaintTo3D.Blending(true);
                    foreach (KeyValuePair<Layer, IPaintTo3DList> de in kv.Value.layerTransparentDisplayList)
                    {
                        if (visibleLayers.IsLayerChecked(de.Key) || de.Key == kv.Value.NullLayer)
                        {
                            ipaintTo3D.List(de.Value);
                        }
                    }
                    ipaintTo3D.Blending(false);
                    ipaintTo3D.PopModOp();
                }
                if (isRunning)
                {
                    // ipaintTo3D.PopModOp();
                }
            }
            if (!isRunning)
            {
                ipaintTo3D.SelectMode = true;
                ipaintTo3D.SelectColor = selectColor;
                if (displayList == null)
                {
                    ipaintTo3D.OpenList();
                    foreach (IGeoObject go in selectedObjects)
                    {
                        if (go != null) go.PaintTo3D(ipaintTo3D);
                    }
                    displayList = ipaintTo3D.CloseList();
                }
                ipaintTo3D.SelectedList(displayList, selectWidth);
                ipaintTo3D.SelectMode = false;
            }
            if (highlightedObjects.Count > 0)
            {
                ipaintTo3D.SelectMode = true;
                ipaintTo3D.SelectColor = highlightColor;
                if (highlightList == null)
                {
                    ipaintTo3D.OpenList();
                    for (int i = 0; i < highlightedObjects.Count; ++i)
                    {
                        highlightedObjects[i].PaintTo3D(ipaintTo3D);
                    }
                    highlightList = ipaintTo3D.CloseList();
                }
                ipaintTo3D.SelectedList(highlightList, highlightWidth);
                ipaintTo3D.SelectMode = false;
            }
            if (PaintActiveEvent != null) PaintActiveEvent(e.ClipRectangle, this, ipaintTo3D);
            if (PaintSelectEvent != null) PaintSelectEvent(e.ClipRectangle, this, ipaintTo3D);
            ipaintTo3D.FinishPaint();
        }
        void IView.OnSizeChanged(Rectangle oldRectangle)
        {
            Size newSize = Size.Empty;
            if (canvas != null) newSize = canvas.ClientRectangle.Size;
            if (paintToOpenGl != null) (paintToOpenGl as IPaintTo3D).Resize(newSize.Width, newSize.Height);
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
        }
        void IView.ZoomDelta(double f)
        {
            //double Factor = f;
            //System.Drawing.Rectangle clr = condorCtrl.ClientRectangle;
            //System.Drawing.Point p = new System.Drawing.Point((clr.Left + clr.Right) / 2, (clr.Bottom + clr.Top) / 2);
            //BoundingRect rct = new BoundingRect(clr.Left, clr.Bottom, clr.Right, clr.Top);
            //rct.Modify(screenToLayout);
            //GeoPoint2D p2 = screenToLayout * new GeoPoint2D(p.X, p.Y);
            //rct.Right = p2.x + (rct.Right - p2.x) * Factor;
            //rct.Left = p2.x + (rct.Left - p2.x) * Factor;
            //rct.Bottom = p2.y + (rct.Bottom - p2.y) * Factor;
            //rct.Top = p2.y + (rct.Top - p2.y) * Factor;
            //(this as IView).ZoomToRect(rct);
            //(this as IView).InvalidateAll();
            double Factor = f;
            BoundingRect rct = GetVisibleBoundingRect();
            Rectangle clr = canvas.ClientRectangle;
            Point p = new Point((clr.Left + clr.Right) / 2, (clr.Bottom + clr.Top) / 2);
            GeoPoint2D p2 = this.projection.PointWorld2D(p);
            rct.Right = p2.x + (rct.Right - p2.x) * Factor;
            rct.Left = p2.x + (rct.Left - p2.x) * Factor;
            rct.Bottom = p2.y + (rct.Bottom - p2.y) * Factor;
            rct.Top = p2.y + (rct.Top - p2.y) * Factor;
            this.ZoomToRect(rct);
        }
        void IView.OnMouseDown(MouseEventArgs eIn)
        {
            MouseEventArgs e = eIn;
            if (MouseDown != null) if (MouseDown(this, ref e)) return;
            bool doScroll = e.Button == MouseButtons.Middle;
            if (e.Button == MouseButtons.Left && (Frame.UIService.ModifierKeys & Keys.Control) != 0)
                doScroll = true;
            if (doScroll)
            {
                lastPanPosition = new Point(e.X, e.Y);
            }
            if (Frame != null)
            {
                Frame.ActiveView = this;
                Frame.ActionStack.OnMouseDown(e, this);
                // keine Aktion hier, nur markieren und schieben
                objectsOnLButtonDown = ObjectsUnderCursor(e);
                if (selectionEnabled)
                {
                    if (objectsOnLButtonDown.Count > 0) (this as IView).SetCursor("Hand");
                    else (this as IView).SetCursor("Arrow");
                }
                downPoint = new Point(e.X, e.Y);
            }
        }
        void IView.OnMouseEnter(System.EventArgs e)
        {
            Frame.ActionStack.OnMouseEnter(e, this);
        }
        void IView.OnMouseHover(System.EventArgs e)
        {
            Frame.ActionStack.OnMouseHover(e, this);
        }
        void IView.OnMouseLeave(System.EventArgs e)
        {
            Frame.ActionStack.OnMouseLeave(e, this);
        }
        private BoundingRect GetVisibleBoundingRect()
        {
            Rectangle clr = canvas.ClientRectangle;
            return projection.BoundingRectWorld2d(clr.Left, clr.Right, clr.Bottom, clr.Top);
        }
        public void ZoomToRect(BoundingRect visibleRect)
        {
            if (canvas != null)
            {
                if (canvas.ClientRectangle.Width == 0 && canvas.ClientRectangle.Height == 0) return;
                projection.SetPlacement(canvas.ClientRectangle, visibleRect);
                canvas.Invalidate();
            }
        }
        private void SetViewDirection(ModOp project, GeoPoint fixPoint, bool mouseIsDown)
        {
            PointF before = projection.ProjectF(fixPoint);
            projection.SetUnscaledProjection(project);
            PointF after = projection.ProjectF(fixPoint);
            projection.MovePlacement(before.X - after.X, before.Y - after.Y);
            canvas?.Invalidate();
            //if (!mouseIsDown)
            //{
            //    RecalcScrollPosition();
            //    projectedModel.RecalcAll(false);
            //}
            // condorCtrl.Update();
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
            if (doScroll && paintToOpenGl != null)
            {
                (this as IView).SetCursor("SizeAll");
                int HScrollOffset = e.X - lastPanPosition.X;
                int VScrollOffset = e.Y - lastPanPosition.Y;
                canvas?.Invalidate();
                projection.MovePlacement(HScrollOffset, VScrollOffset);
                lastPanPosition = new Point(e.X, e.Y);
            }
            if (doDirection && paintToOpenGl != null)
            {
                int HOffset = e.X - lastPanPosition.X;
                int VOffset = e.Y - lastPanPosition.Y;
                if (VOffset != 0 || HOffset != 0)
                {
                    //if (Math.Abs(VOffset) > Math.Abs(HOffset)) HOffset = 0;
                    //else VOffset = 0;
                    // bringt keine Vorteile
                    lastPanPosition = new Point(e.X, e.Y);
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
                    Point clcenter = canvas.ClientRectangle.Location;
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
                canvas.Frame.ActiveView = this; // activeview soll sich nur bei Klick ändern
                Frame.ActionStack.OnMouseMove(e, this);
                GeoObjectList l = ObjectsUnderCursor(e);
                if (selectionEnabled)
                {
                    if (l.Count > 0) (this as IView).SetCursor("Hand");
                    else (this as IView).SetCursor("Arrow");
                }
                if (e.Button == MouseButtons.Left && (Math.Abs(e.X - downPoint.X) > dragWidth || Math.Abs(e.Y - downPoint.Y) > dragWidth) && objectsOnLButtonDown != null)
                {
                    GeoObjectList dragList = new GeoObjectList(objectsOnLButtonDown.Count);
                    for (int i = 0; i < objectsOnLButtonDown.Count; i++)
                        dragList.Add(objectsOnLButtonDown[i].Clone());
                    DoDragDrop(dragList, Substitutes.DragDropEffects.Link);
                }
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
                Frame.SnapMode = SnapPointFinder.SnapModes.SnapToObjectPoint | SnapPointFinder.SnapModes.SnapToFaceSurface;
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
                return;
            }
            if (Frame != null)
            {
                Frame.ActiveView = this;
                Frame.ActionStack.OnMouseUp(e, this);
                GeoObjectList l = ObjectsUnderCursor(e);
                if (selectionEnabled)
                {
                    if (l.Count > 0) (this as IView).SetCursor("Hand");
                    else (this as IView).SetCursor("Arrow");
                    if ((Frame.UIService.ModifierKeys & Keys.Control) != 0 && (e.Button == MouseButtons.Left)) selectedObjects.AddRange(l);
                    else if (e.Button == MouseButtons.Left) selectedObjects = l;
                    displayList = null;
                    canvas?.Invalidate();
                }
            }
            // folgenden Block habe ich hinten angestellt wg. ViewPointAction
            //if (projectedModelNeedsRecalc)
            //{
            //    projectedModelNeedsRecalc = false;
            //    projectedModel.RecalcAll(false);
            //    RecalcScrollPosition();
            //}
        }
        void IView.OnMouseWheel(MouseEventArgs eIn)
        {
            MouseEventArgs e = eIn;
            if (MouseWheel != null) if (MouseWheel(this, ref e)) return;
            double Factor;
            if (e.Delta > 0) Factor = 1.0 / mouseWheelZoomFactor;
            else if (e.Delta < 0) Factor = mouseWheelZoomFactor;
            else return;
            BoundingRect rct = GetVisibleBoundingRect();
            Point p = new Point(e.X, e.Y);
            p = canvas.PointToClient(Frame.UIService.CurrentMousePosition);
            GeoPoint2D p2 = projection.PointWorld2D(p);
            rct.Right = p2.x + (rct.Right - p2.x) * Factor;
            rct.Left = p2.x + (rct.Left - p2.x) * Factor;
            rct.Bottom = p2.y + (rct.Bottom - p2.y) * Factor;
            rct.Top = p2.y + (rct.Top - p2.y) * Factor;
            this.ZoomToRect(rct);
            Frame.ActionStack.OnMouseWheel(e,this);
        }
        void IView.OnMouseDoubleClick(MouseEventArgs eIn)
        {
            MouseEventArgs e = eIn;
            if (MouseDoubleClick != null) if (MouseDoubleClick(this, ref e)) return;
        }
        void IView.OnDragDrop(DragEventArgs drgevent)
        {
        }
        void IView.OnDragEnter(DragEventArgs drgevent)
        {
        }
        void IView.OnDragLeave(EventArgs e)
        {
        }
        void IView.OnDragOver(DragEventArgs drgevent)
        {
        }
        #endregion
        #region IShowProperty implementation
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                ShowPropertyLabelFlags res = ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu;
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
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.AnimatedView", false, this);
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
                    schedulesProperty = new ShowPropertySchedules(model);
                    subEntries = new IShowProperty[] { visibleLayers, new ShowPropertyDrives(model.AllDrives), schedulesProperty };
                }
                return subEntries;
            }
        }

        string IView.Name => Name;


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
        }
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.AnimatedView.Show":
                    base.Frame.ActiveView = this;
                    return true;
                case "MenuId.AnimatedView.Start":
                    if (schedulesProperty.ActiveSchedule == null)
                    {
                        Frame.UIService.ShowMessageBox(StringTable.GetString("Error.Schedules.NoActiveSchedule"), StringTable.GetString("Errormessage.Title.Schedules"), MessageBoxButtons.OK);
                        return true;
                    }
                    StartAnimation(schedulesProperty.ActiveSchedule, 0.0, double.MaxValue, 1.0);
                    return true;
                case "MenuId.AnimatedView.Stop":
                    StopAnimation();
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.AnimatedView.Show":
                    CommandState.Checked = base.Frame.ActiveView == this;
                    return true;
                case "MenuId.AnimatedView.Start":
                    CommandState.Enabled = !isRunning;
                    return true;
                case "MenuId.AnimatedView.Stop":
                    CommandState.Enabled = isRunning;
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        void OnApplicationIdle(object sender, EventArgs e)
        {
            //displayLists = null;
            int tc = System.Environment.TickCount;
            double time;
            lock (this)
            {
                if (GetTimeEvent != null) time = GetTimeEvent(this);
                else time = (tc - timeBase) / 1000.0 * speed + startTime;
                schedule.SetDrivePositions(time, this);
                currentTime = time;
            }
            if (NextStepEvent != null) NextStepEvent(this, time);
            if (canvas != null) canvas.Invalidate();
            else Frame.UIService.ApplicationIdle -= new EventHandler(OnApplicationIdle); // canvas has somehow been deleted
            if (time > endTime)
            {
                StopAnimation();
                if (NextStepEvent != null) NextStepEvent(this, time);
            }
            //Application.DoEvents();
        }
        #endregion
        public void SetSelectedObject(IGeoObject go)
        {
            selectedObjects.Clear();
            selectedObjects.Add(go);
            displayList = null;
            (this as IView).InvalidateAll();
        }
        public void SetSelectedObjects(GeoObjectList l)
        {
            selectedObjects.Clear();
            selectedObjects.AddRange(l);
            displayList = null;
            (this as IView).InvalidateAll();
        }
        public GeoObjectList GetSelectedObjects()
        {
            return selectedObjects;
        }
        private BoundingRect GetDisplayExtent()
        {
            BoundingRect ext = model.GetExtentForZoomTotal(projection);
            double f, dx, dy;
            if (ext.IsEmpty()) return ext;
            projection.GetPlacement(out f, out dx, out dy);
            return new BoundingRect(ext.Left * f + dx, -ext.Bottom * f + dy, ext.Right * f + dx, -ext.Top * f + dy);
        }

        #region ISerializable Members
        protected AnimatedView(SerializationInfo info, StreamingContext context)
            : this()
        {
            project = info.GetValue("project", typeof(Project)) as Project;
            model = info.GetValue("Model", typeof(Model)) as Model;
            projection = info.GetValue("Projection", typeof(Projection)) as Projection;
            visibleLayers = info.GetValue("VisibleLayers", typeof(CheckedLayerList)) as CheckedLayerList;
            name = info.GetString("Name");
        }


        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("project", project, typeof(Project));
            info.AddValue("Model", model, typeof(Model));
            info.AddValue("Projection", projection, typeof(Projection));
            info.AddValue("VisibleLayers", visibleLayers, typeof(CheckedLayerList));
            info.AddValue("Name", name, typeof(string));
        }
        GeoObjectList IView.GetDataPresent(object data)
        {
            return Frame.UIService.GetDataPresent(data);
        }

        DragDropEffects IView.DoDragDrop(GeoObjectList dragList, DragDropEffects all)
        {
            return canvas.DoDragDrop(dragList, all);
        }

        void IView.ZoomTotal(double f)
        {
            Rectangle clientRect = canvas.ClientRectangle;
            if (clientRect.Width == 0 && clientRect.Height == 0) return;
            BoundingRect ext = model.GetExtent(this.projection);
            if (ext.Width + ext.Height == 0.0) ext.Inflate(1, 1);
            ext = ext * f;
            projection.SetPlacement(clientRect, ext);
            canvas.Invalidate();
            // RecalcScrollPosition();
        }
        #endregion
    }

}
