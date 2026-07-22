using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;

namespace CADability.Actions
{

    internal interface IActionInputView
    {   // für Aktionen, die selektieren und konstruieren zulassen
        // zu implementieren von Views
        bool IsLayerVisible(Layer l);
        Layer[] GetVisibleLayers();
        bool AllowContextMenu { get; }
        void SetAdditionalExtent(BoundingCube bc);
        void MakeEverythingTranparent(bool transparent);
    }

    /// <summary>
    /// Exception thrown by Action or derived classes
    /// with a hopefully meaningful message
    /// </summary>
    public class ActionException : System.Exception
    {
        internal ActionException(string reason)
            : base(reason)
        {
        }
    }

    /// <summary>
    /// Base class for all "Actions". An Action is an object, that receives various MouseInput events
    /// once it has been "Set" by a call to <see cref="IFrame.SetAction"/>. After performing the 
    /// required tasks, the Action is removed from the ActionStack and the previous active action
    /// is resumed. The action on the bottom of the action stack is the <see cref="SelectObjectsAction"/>.
    /// Use the <see cref="ConstructAction"/> for typical drawing purposes, because it provides a convenient
    /// set of methods. If an action object is set by a call to <see cref="IFrame.SetAction"/> the following
    /// sequence of calls to the new and the old action is executed:
    /// <list type="bullet">
    /// <item><description>new action: <see cref="OnSetAction"/> (as a reaction to <see cref="IFrame.SetAction"/>)</description></item>
    /// <item><description>old action: <see cref="OnInactivate"/> (may call <see cref="IFrame.RemoveActiveAction"/> if desired)</description></item>
    /// <item><description>new action: <see cref="OnActivate"/> (from now on the new action may receive mouse events).</description></item>
    /// <item><description>After the new action calls <see cref="RemoveThisAction"/> or someone calls <see cref="IFrame.RemoveActiveAction"/>
    /// the new action will receive <see cref="OnInactivate"/>. </description></item>
    /// <item><description>The old action (if still on the stack)
    /// will receive a call to <see cref="OnActivate"/> and finally </description></item>
    /// <item><description>the new action will receive a call to
    /// <see cref="OnRemoveAction"/> as a last call.</description></item></list>
    /// </summary>
    public abstract class Action : ICommandHandler
    {
        private ActionStack actionStack;
        private bool autoCursor;
        private bool changeTabInControlCenter;
        internal string returnToTabPage;
        private Dictionary<SnapPointFinder.DidSnapModes, string> cursor;
        private string defaultCursor;
        private ActionFeedBack feedBack;
        /// <summary>
        /// Contains the menu id of the command that invoked this action. used in <see cref="OnUpdateCommand"/>.
        /// </summary>
        protected string MenuId; // wenn von einem Menue ausgelöst, damit der Button gecheckt werden kann
        /// <summary>
        /// a list of objects that are not considered when snapping is resolved. Usually the 
        /// object currently under construction (if any) is in this list.
        /// </summary>
        protected GeoObjectList IgnoreForSnap;
        /// <summary>
        /// If ViewType is not null, only those mouseevents are forwarded which come from a view
        /// of that type.
        /// </summary>
        protected Type ViewType;
        /// <summary>
        /// If OnlyThisModel is not null, only those mouseevents are forwarded which come from a view
        /// that presents this model.
        /// </summary>
        protected Model OnlyThisModel;
        /// <summary>
        /// If OnlyThisView is not null, only those mouseevents are forwarded which come from this view.
        /// </summary>
        protected IView OnlyThisView;
        /// <summary>
        /// Checks, whether this action is the currently active action
        /// </summary>
        protected bool IsActive
        {
            get
            {
                return (actionStack.ActiveAction == this);
            }
        }
        private IView currentMouseView;
        /// <summary>
        /// The view from which the last OnMouseMove/Up/Down was evoked
        /// </summary>
        protected IView CurrentMouseView { get => currentMouseView == null ? Frame.ActiveView : currentMouseView; set => currentMouseView = value; }
        internal void SetCurrentMouseView(IView vw)
        {
            CurrentMouseView = vw;
        }
        internal bool AcceptMouseInput(IView vw)
        {
            if (OnlyThisView != null) return vw == OnlyThisView;
            if (ViewType == null) return true;
            else
            {
                if (!ViewType.IsInstanceOfType(vw)) return false;
            }
            if (OnlyThisModel == null) return true;
            return vw.Model == OnlyThisModel;
        }
        /// <summary>
        /// Creates a new Action and sets some default.
        /// </summary>
        protected Action()
        {
            autoCursor = true; // oder?
            changeTabInControlCenter = false; // erstmal...
            cursor = new Dictionary<SnapPointFinder.DidSnapModes, string>();
            cursor[SnapPointFinder.DidSnapModes.DidNotSnap] = "Pen";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToIntersectionPoint] = "DartIntersect";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToDropPoint] = "DartFoot";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToObjectCenter] = "DartMiddle";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToObjectPoint] = "DartObject";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToTangentPoint] = "DartTangent";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint] = "DartSnap";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToGridPoint] = "DartGrid";
            cursor[SnapPointFinder.DidSnapModes.DidAdjustOrtho] = "DartOrtho";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToLocalZero] = "DartOrigin";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToAbsoluteZero] = "DartOrigin";
            cursor[SnapPointFinder.DidSnapModes.DidSnapToFaceSurface] = "Plane";
            defaultCursor = "Dart"; // für alles außer DidNotSnap
            UseFilter = true; // Standard!
        }
        /// <summary>
        /// Must be implemented by derived class. Returns an identification string. All CADability actions
        /// return the unique strings like "Draw.Line.TwoPoints" or "Zoom"
        /// </summary>
        /// <returns>the identification of the action</returns>
        public abstract string GetID();
        /// <summary>
        /// Use the active filter objects of the project for adjusting the mouse position (snap etc.)
        /// </summary>
        public bool UseFilter;
#region Die leeren Implementierungen der virtuellen Methoden für die organisatorischen Aktions Ereignisse
        /// <summary>
        /// Determins, wether this action can work on a <see cref="LayoutView"/>. Default implementation
        /// returns false. Override, if your Action can work on a LayoutView.
        /// </summary>
        public virtual bool WorksOnLayoutView
        {
            get
            {
                return false;
            }
        }
        /// <summary>
        /// First event that is called when the action is set via <see cref="IFrame.SetAction"/>
        /// </summary>
        public virtual void OnSetAction()
        {
            MenuId = actionStack.Frame.CurrentMenuId;
        }
        /// <summary>
        /// Last event that is called when the action is removed from the action stack.
        /// </summary>
        public virtual void OnRemoveAction()
        {
            if (feedBack != null)
            {
                foreach (IView vw in Frame.AllViews)
                {
                    vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Active, new PaintView(OnRepaintActive));
                }
            }
        }
        /// <summary>
        /// The action has been activated. From now on it will receive calls of the mouse event methods
        /// like <see cref="OnMouseMove"/>.
        /// </summary>
        /// <param name="OldActiveAction">the action that was active</param>
        /// <param name="SettingAction">true: if the action has bee set, false: if the action is resumed</param>
        public virtual void OnActivate(Action OldActiveAction, bool SettingAction)
        {
        }
        /// <summary>
        /// The action has been inactivated. No more calls to the mouse events will appear.
        /// </summary>
        /// <param name="NewActiveAction">the action that will become active</param>
        /// <param name="RemovingAction">true: if called because the action is removed, false: if called when a new action is set on top of this action</param>
        public virtual void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
        }
        /// <summary>
        /// Zooming or scrolling changed the visible aspect of the current view.
        /// </summary>
        /// <param name="d">the reason for the call</param>
        public virtual void OnDisplayChanged(DisplayChangeArg d)
        {
        }
        /// <summary>
        /// Will be called if new views (<see cref="LayoutView"/> or <see cref="ModelView"/>) are created
        /// or removed from the project. Default implementation does nothing.
        /// </summary>
        public virtual void OnViewsChanged()
        {
            if (feedBack != null)
            {
                // schon gelöschte Views sind ja kein Problem, da muss man den Painthandler nicht entfernen
                foreach (IView vw in Frame.AllViews)
                {
                    vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Active, new PaintView(OnRepaintActive));
                    vw.SetPaintHandler(PaintBuffer.DrawingAspect.Active, new PaintView(OnRepaintActive));
                }
            }
        }
#endregion
#region Die leerem Implementierungen der virtuellen Methoden für die Mouse Events
        /// <summary>
        /// Override this method to react on the MouseDown event.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public virtual void OnMouseDown(MouseEventArgs e, IView vw)
        {
        }
        /// <summary>
        /// Override this method to react on the MouseEnter event.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public virtual void OnMouseEnter(System.EventArgs e, IView vw)
        {
        }
        /// <summary>
        /// Override this method to react on the MouseHover event.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public virtual void OnMouseHover(System.EventArgs e, IView vw)
        {
        }
        /// <summary>
        /// Override this method to react on the MouseLeave event.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public virtual void OnMouseLeave(System.EventArgs e, IView vw)
        {
        }
        /// <summary>
        /// Override this method to react on the MouseMove event.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public virtual void OnMouseMove(MouseEventArgs e, IView vw)
        {
        }
        /// <summary>
        /// Override this method to react on the MouseUp event.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public virtual void OnMouseUp(MouseEventArgs e, IView vw)
        {
        }
        /// <summary>
        /// Override this method to react on the MouseWheel event.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public virtual void OnMouseWheel(MouseEventArgs e, IView vw)
        {
        }
#endregion
#region Die leeren Implementierungen für die ToolTips
        internal virtual int GetInfoProviderIndex(Point ScreenCursorPosition, IView View)
        {
            return -1;
        }
        internal virtual string GetInfoProviderText(int Index, CADability.UserInterface.InfoLevelMode Level, IView View)
        {
            return null;
        }
        internal virtual int GetInfoProviderVerticalPosition(int Index, IView View)
        {
            return 0;
        }
#endregion
        /// <summary>
        /// This method will be called when the user presses the enter key. The default
        /// implementation does nothing.
        /// </summary>
        public virtual bool OnEnter() { return false; }
        /// <summary>
        /// This method will be called when the user presses the escape key. The default
        /// implementation does nothing.
        /// </summary>
        public virtual bool OnEscape() { return false; }
        /// <summary>
        /// This method will be called when the user presses the delete key. The default
        /// implementation does nothing.
        /// </summary>
        public virtual bool OnDelete() { return false; }
        /// <summary>
        /// This method will be called when the user presses the enter key. The default
        /// implementation does nothing.
        /// </summary>
        public virtual bool OnEnter(object sender)
        {
            return OnEnter();
        }
        /// <summary>
        /// This method will be called when the user presses the escape key. The default
        /// implementation does nothing.
        /// </summary>
        public virtual bool OnEscape(object sender)
        {
            return OnEscape();
        }
        /// <summary>
        /// This method will be called when the user presses the delete key. The default
        /// implementation does nothing.
        /// </summary>
        public virtual bool OnDelete(object sender)
        {
            return OnDelete();
        }
        /// <summary>
        /// This method will be called when the user presses the tab key. The default
        /// implementation does nothing.
        /// </summary>
        public virtual bool OnTab(object sender)
        {
            return false;
        }
        /// <summary>
        /// Defines, whether this Action should be repeated after it was removed. The default 
        /// implementation returns false, override it if you want a different behaviour.
        /// </summary>
        /// <returns>true to repeat, false otherwise</returns>
        public virtual bool AutoRepeat()
        {
            return false;
        }
        /// <summary>
        /// Called before <see cref="OnSetAction"/> is called, if the action is created by the "autorepeat" machanism.
        /// </summary>
        public virtual void AutoRepeated() { }
        internal ActionStack ActionStack
        {
            get
            {
                return actionStack;
            }
            set
            {
                actionStack = value;
            }
        }
#region Services für abgeleitete Aktionen
        /// <summary>
        /// Returns the frame (<see cref="IFrame"/>) of the context of this action. The frame also gives
        /// access to the project.
        /// </summary>
        public IFrame Frame { get { return actionStack.Frame; } }
        /// <summary>
        /// Provides access to the "feedback" object, which is used to define visual feedback of the action.
        /// When objects in a model are modified, you will immediately see the feedback (if this model is visible in a <see cref="ModelView"/>).
        /// But sometimes you need more feedback, like arrows or imtermediate objects that change while the mousinput (or controlcenter input
        /// or some other conditions) change. Add those objects to the feedback and they will be displayed immediately.
        /// </summary>
        public ActionFeedBack FeedBack
        {
            get
            {
                if (feedBack == null && Frame != null)
                {   // hier wird zum ersten mal auf FeedBack zurückgegriffen
                    // d.h. wenn nicht zugegriffen wird, dann auch kein PaintHandler
                    feedBack = new ActionFeedBack();
                    feedBack.Frame = Frame;
                    foreach (IView vw in Frame.AllViews)
                    {
                        vw.SetPaintHandler(PaintBuffer.DrawingAspect.Active, new PaintView(OnRepaintActive));
                    }
                }
                return feedBack;
            }
        }
        private void OnRepaintActive(Rectangle Extent, IView View, IPaintTo3D PaintToActive)
        {
            if (feedBack != null)
            {
                feedBack.Repaint(Extent, View, PaintToActive);
            }
        }
        /// <summary>
        /// true: this class takes responsibility for setting the cursor,
        /// false: the derived class manages the cursor.
        /// </summary>
        public bool AutoCursor
        {
            get { return autoCursor; }
            set { autoCursor = value; }
        }
        /// <summary>
        /// true: this class may change the selection in the control center
        /// false: this class may not change the selection in the control center
        /// </summary>
        public bool ChangeTabInControlCenter
        {
            get
            {
                return changeTabInControlCenter;
            }
            set
            {
                changeTabInControlCenter = value;
            }
        }
        /// <summary>
        /// Sets the cursor int hte provided view according to the provided snap mode. The cursors may be redefined
        /// by calling <see cref="SetCursor(SnapPointFinder.DidSnapModes, string )"/>
        /// </summary>
        /// <param name="DidSnap"></param>
        /// <param name="vw"></param>
        protected void SetCursor(SnapPointFinder.DidSnapModes DidSnap, IView vw)
        {
            var c = cursor[DidSnap];
            if (c == null) c = defaultCursor;
            if (c != null) vw.SetCursor(c);
        }
        /// <summary>
        /// Sets the cursor name for different snap situation. The <see cref="CursorTable"/>
        /// contains the resources of the named cursors.
        /// </summary>
        /// <param name="DidSnap">the snap situation</param>
        /// <param name="CursorName">the name of the cursor</param>
        public void SetCursor(SnapPointFinder.DidSnapModes DidSnap, string CursorName)
        {
            cursor[DidSnap] = CursorName;
        }


        //public GeoPoint WorldPoint(System.Windows.Forms.MouseEventArgs e)
        //{
        //    return Frame.ActiveView.Projection.DrawingPlanePoint(new Point(e.X,e.Y));
        //}
        /// <summary>
        /// Returns a <see cref="GeoPoint"/> in the model coordinate system that corresponds
        /// to the Client point. No snapping is performed. The drawing plane of the projection is used
        /// </summary>
        /// <param name="p">Point in the client coordinate system of the active view</param>
        /// <returns>the model coordinate</returns>
        public GeoPoint WorldPoint(Point p)
        {
            return Frame.ActiveView.Projection.DrawingPlanePoint(p);
        }
        /// <summary>
        /// Returns a <see cref="GeoPoint"/> in the model coordinate system that corresponds
        /// to the MouseEventArgs point. No snapping is performed. The drawing plane of the projection is used
        /// </summary>
        /// <param name="e">a MouseEventArgs object, that contains the window coordinate</param>
        /// <param name="vw">the IView of the MouseEventArgs</param>
        /// <returns>the model coordinate</returns>
        public GeoPoint WorldPoint(MouseEventArgs e, IView vw)
        {
            return vw.Projection.DrawingPlanePoint(new Point(e.X, e.Y));
        }
        internal GeoPoint2D ProjectedPoint(Point p)
        {
            return Frame.ActiveView.Projection.ProjectUnscaled(WorldPoint(p));
        }
        /// <summary>
        /// Returns a 3D point in the world coordinate system corresponding to the given 2D point
        /// in the active drawing plane.
        /// </summary>
        /// <param name="p">2D point in the drawing plane</param>
        /// <returns>3D point in the world coordinate system</returns>
        public GeoPoint WorldPoint(GeoPoint2D p)
        {
            return Frame.ActiveView.Projection.DrawingPlanePoint(p);
        }
        /// <summary>
        /// Returns a snap point according to the current snap settings in the given <see cref="IView"/>.
        /// Sets the cursor if <see cref="AutoCursor"/> is true.
        /// </summary>
        /// <param name="e">point in the view</param>
        /// <param name="vw">the view</param>
        /// <param name="DidSnap">kind of snap operation that was used</param>
        /// <returns>the best snapping point found</returns>
        public GeoPoint SnapPoint(MouseEventArgs e, IView vw, out SnapPointFinder.DidSnapModes DidSnap)
        {
            GeoPoint p;
            DidSnap = vw.AdjustPoint(new Point(e.X, e.Y), out p, IgnoreForSnap);
            if (autoCursor)
            {
                SetCursor(DidSnap, vw);
            }
            return p;
        }
        /// <summary>
        /// Returns a snap point according to the current snap settings in the given <see cref="IView"/> with respect to a basepoint.
        /// Sets the cursor if <see cref="AutoCursor"/> is true.
        /// </summary>
        /// <param name="e">point in the view</param>
        /// <param name="BasePoint">the base point</param>
        /// <param name="vw">the view</param>
        /// <param name="DidSnap">kind of snap operation that was used</param>
        /// <returns>the best snapping point found</returns>
        public GeoPoint SnapPoint(MouseEventArgs e, GeoPoint BasePoint, IView vw, out SnapPointFinder.DidSnapModes DidSnap)
        {
            GeoPoint p;
            DidSnap = vw.AdjustPoint(BasePoint, new Point(e.X, e.Y), out p, IgnoreForSnap);
            if (autoCursor)
            {
                SetCursor(DidSnap, vw);
            }
            return p;
        }
        /// <summary>
        /// Coverts the given length in pixel (screen) coordinates to w length in the model
        /// coordinate system of the active view
        /// </summary>
        /// <param name="ViewLength"></param>
        /// <returns></returns>
        public double WorldLength(double ViewLength)
        {
            return Frame.ActiveView.Projection.DeviceToWorldFactor * ViewLength;
        }
        /// <summary>
        /// Returns a 3D vector in the world coordinate system corresponding to the given angle
        /// in the active drawing plane.
        /// </summary>
        /// <param name="a">Angle of the requested direction</param>
        /// <returns>Direction vector of the angle a</returns>
        public GeoVector WorldDirection(Angle a)
        {
            return ActiveDrawingPlane.ToGlobal(a.Direction);
        }
        /// <summary>
        /// Liefert ein Größe im Koordinatensystem des Modells, die in etwa dem aktuell dargestellten
        /// Ausschnitt entspricht.
        /// </summary>
        internal double WorldViewSize
        {
            get
            {
                Rectangle r = Frame.ActiveView.DisplayRectangle;
                int w = (r.Height + r.Width) / 2;
                return Frame.ActiveView.Projection.DeviceToWorldFactor * w;
            }
        }
        /// <summary>
        /// Returns the active drawing plane, that is the drawing plane of the active view.
        /// </summary>
        public Plane ActiveDrawingPlane
        {
            get
            {
                if (CurrentMouseView != null)
                    return CurrentMouseView.Projection.DrawingPlane;
                else
                    return actionStack.Frame.ActiveView.Projection.DrawingPlane;
            }
        }
        /// <summary>
        /// Detects whether a given curve (<see cref="ICurve"/>) is touched by the
        /// cursor position given in mousePoint in respect to the active view. The setting
        /// "Select.Pick" gives the maximum pixel distance for the test.
        /// </summary>
        /// <param name="curve">the curve to test</param>
        /// <param name="mousePoint">the mouse position</param>
        /// <returns>true if the mouse position is close to the curve</returns>
        public bool CurveHitTest(ICurve curve, Point mousePoint)
        {
            ProjectedModel pm = Frame.ActiveView.ProjectedModel;
            int pickRectSize = Frame.GetIntSetting("Select.Pick", 5); // "Radius" des PickQuadrates
            BoundingRect pickrect = Frame.ActiveView.Projection.BoundingRectWorld2d(mousePoint.X - 5, mousePoint.X + 5, mousePoint.Y + 5, mousePoint.Y - 5);
            ICurve2D c2d = curve.GetProjectedCurve(Frame.ActiveView.Projection.ProjectionPlane);
            if (c2d != null) return c2d.HitTest(ref pickrect, false);
            return false;
        }
        /// <summary>
        /// Returns a list of <see cref="IGeoObject"/>s that are close to the mouse point
        /// with respect to the current view.
        /// </summary>
        /// <param name="mousePoint">the mouse position to test</param>
        /// <returns>list of touched IGeoObjects</returns>
        public GeoObjectList GetObjectsUnderCursor(Point mousePoint)
        {
            GeoObjectList result = new GeoObjectList();
            //ProjectedModel pm = Frame.ActiveView.ProjectedModel;
            //BoundingRect pickrect = Frame.ActiveView.Projection.BoundingRectWorld2d(mousePoint.X-5,mousePoint.X+5,mousePoint.Y+5,mousePoint.Y-5);
            //GeoObjectList fromquadtree = pm.GetObjectsFromRect(pickrect,PickMode.normal,null);

            IView vw = Frame.ActiveView;
            int pickRadius = Frame.GetIntSetting("Select.PickRadius", 5);
            Projection.PickArea pa = vw.Projection.GetPickSpace(new Rectangle(mousePoint.X - pickRadius, mousePoint.Y - pickRadius, pickRadius * 2, pickRadius * 2));
            IActionInputView pm = vw as IActionInputView;
            GeoObjectList fromquadtree = vw.Model.GetObjectsFromRect(pa, new HashSet<Layer>(pm.GetVisibleLayers()), PickMode.normal, null);

            foreach (IGeoObject go in fromquadtree)
            {
                if (go.Owner is IGeoObject)
                {
                    IGeoObject par = go.Owner as IGeoObject;
                    while (par.Owner is IGeoObject) par = par.Owner as IGeoObject;
                    if (!result.Contains(par)) result.Add(par);
                }
                else
                {	// hier braucht man nicht testen
                    result.Add(go);
                }
            }
            return result;
        }
        /// <summary>
        /// Returns the current mouse position.
        /// </summary>
        public Point CurrentMousePosition
        {
            get
            {
                return Frame.UIService.CurrentMousePosition;
            }
        }
        /// <summary>
        /// Removes this action from the action stack if this action is on top of the action stack
        /// </summary>
        public void RemoveThisAction()
        {
            if (actionStack.ActiveAction == this)
            {
                actionStack.RemoveActiveAction();
            }
        }
        public Action PreviousAction
        {
            get
            {
                return actionStack.PrevoiusAction(this);
            }
        }
#endregion
#region ICommandHandler Members
        /// <summary>
        /// Override if you want to process menu commands with your action.
        /// Default implementation always returns false.
        /// </summary>
        /// <param name="MenuId">the menu id to process</param>
        /// <returns>true if processed, false if not</returns>
        public virtual bool OnCommand(string MenuId)
        {
            return false;
        }
        /// <summary>
        /// Override if you also override <see cref="OnCommand"/> to manipulate the appearance
        /// of the corresponding menu item or the state of the toolbar button. The default implementation
        /// checks whether the MenuId from the parameter corresponds to the menuId member variable
        /// and checks the item if appropriate
        /// </summary>
        /// <param name="MenuId">menu id the command state is queried for</param>
        /// <param name="CommandState">the command state to manipulate</param>
        /// <returns></returns>
        public virtual bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            if (this.MenuId == MenuId && IsActive)
            {
                CommandState.Checked = true;
                // nicht return true, sonst hört er auf, wir betrachten hier aber nur checked
                // und nicht enabled, das wird (z.B. beim Verschieben) an anderer Stelle gesetzt
                // return true;
            }
            return false;
        }
        /// <summary>
        /// Override if your action needs to handle menu selection changes (very rare)
        /// </summary>
        /// <param name="MenuId"></param>
        /// <param name="selected"></param>
        public virtual void OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }
        #endregion
    }

    internal class WaitCursor : IDisposable
    {
        ICanvas canvas;
        string oldCursor;
        public WaitCursor(ICanvas canvas)
        {
            this.canvas = canvas;
            oldCursor = canvas.Cursor;
            canvas.Cursor = "WaitCursor";
        }
#region IDisposable Members

        void IDisposable.Dispose()
        {
            canvas.Cursor = oldCursor;
        }

#endregion
    }
}
