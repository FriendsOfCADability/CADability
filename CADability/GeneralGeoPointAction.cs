using CADability.GeoObject;
using CADability.UserInterface;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;

namespace CADability.Actions
{

    /// <summary>
    /// Thrown by <see cref="GeneralGeoPointAction"/> when there is no way to deliver the point: neither a
    /// <see cref="CADability.UserInterface.GeoPointProperty"/> was given nor a handler is attached to
    /// <see cref="GeneralGeoPointAction.SetGeoPointEvent"/>.
    /// </summary>
    public class GeneralGeoPointActionException : System.ApplicationException
    {
        /// <summary>
        /// Creates the exception with the given message.
        /// </summary>
        /// <param name="message">The error message</param>
        public GeneralGeoPointActionException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Simple Action to modify a GeoPoint by moving the mouse.  The modification will start imediately
    /// after the action is set and will terminate when the mouse button is released or enter
    /// or escape is pressed.
    /// </summary>

    public class GeneralGeoPointAction : Action
    {
        /// <summary>
        /// The point the modification started from (the initial value of the point). It is the base point for the snapping
        /// and the location of the plane returned by <see cref="GetMousePlane"/>.
        /// </summary>
        protected GeoPoint basePoint;
        /// <summary>
        /// The property entry whose point is modified, when the action was created for a
        /// <see cref="CADability.UserInterface.GeoPointProperty"/>; null when it was created with an initial point, in which
        /// case the point is delivered by <see cref="SetGeoPointEvent"/>.
        /// </summary>
        public GeoPointProperty GeoPointProperty;
        private GeoPoint initialGeoPointValue;
        /// <summary>
        /// Free data the creator of the action may attach to identify it in the event handlers, e.g. the index of the point
        /// being modified. Only created by the constructors that take an initial point; null otherwise.
        /// </summary>
        public UserData UserData;
        /// <summary>
        /// Constructs a GeneralGeoPointAction giving the initial Value of the point.
        /// This value will be used in the <see cref="SetGeoPointEvent"/> when the user presses ESC
        /// </summary>
        /// <param name="initialGeoPointValue">initial value of the GeoPoint</param>
        public GeneralGeoPointAction(GeoPoint initialGeoPointValue)
        {
            this.initialGeoPointValue = initialGeoPointValue;
            UserData = new UserData();
            basePoint = initialGeoPointValue;
            // base.Frame.Project.Undo.StartingMouseAction();
        }
        /// <summary>
        /// Constructs a GeneralGeoPointAction giving the initial value of the point (see
        /// <see cref="GeneralGeoPointAction(GeoPoint)"/>) and excludes <paramref name="ignoreSnap"/> from the snapping,
        /// which is usually the object that is being modified.
        /// </summary>
        /// <param name="initialGeoPointValue">initial value of the GeoPoint</param>
        /// <param name="ignoreSnap">object not to snap to, may be null</param>
        public GeneralGeoPointAction(GeoPoint initialGeoPointValue, IGeoObject ignoreSnap)
            : this(initialGeoPointValue)
        {
            if (ignoreSnap != null)
            {
                base.IgnoreForSnap = new GeoObjectList(ignoreSnap);
            }
        }
        /// <summary>
        /// Constructs a GeneralGeoPointAction that modifies the point shown by <paramref name="GeoPointProperty"/>: the
        /// point is read from the property and written back to it on each mouse move, so no <see cref="SetGeoPointEvent"/>
        /// handler is needed. Typically used when the user drags the hotspot of such a property.
        /// </summary>
        /// <param name="GeoPointProperty">the property to modify</param>
        public GeneralGeoPointAction(GeoPointProperty GeoPointProperty)
        {
            this.GeoPointProperty = GeoPointProperty;
            basePoint = GeoPointProperty.GetGeoPoint();
        }
        /// <summary>
        /// Constructs a GeneralGeoPointAction that modifies the point shown by <paramref name="GeoPointProperty"/> (see
        /// <see cref="GeneralGeoPointAction(CADability.UserInterface.GeoPointProperty)"/>) and excludes <paramref name="ignoreSnap"/> from the
        /// snapping, which is usually the object that is being modified.
        /// </summary>
        /// <param name="GeoPointProperty">the property to modify</param>
        /// <param name="ignoreSnap">object not to snap to, may be null</param>
        public GeneralGeoPointAction(GeoPointProperty GeoPointProperty, IGeoObject ignoreSnap)
            : this(GeoPointProperty)
        {
            if (ignoreSnap != null)
            {
                base.IgnoreForSnap = new GeoObjectList(ignoreSnap);
            }
        }
        /// <summary>
        /// Delegate for <see cref="SetGeoPointEvent"/>.
        /// </summary>
        /// <param name="sender">The action raising the event</param>
        /// <param name="NewValue">The new value of the point in model coordinates</param>
        public delegate void SetGeoPointDelegate(GeneralGeoPointAction sender, GeoPoint NewValue);
        /// <summary>
        /// Delegate for <see cref="ActionDoneEvent"/>.
        /// </summary>
        /// <param name="sender">The action that has ended</param>
        public delegate void ActionDoneDelegate(GeneralGeoPointAction sender);
        /// <summary>
        /// Raised with the new point on every mouse move and on the final click, and with the initial point when the user
        /// cancels with Escape or another action takes over. If a handler is attached it is responsible for applying the
        /// point; otherwise the point is written to <see cref="GeoPointProperty"/>. Without either, a
        /// <see cref="GeneralGeoPointActionException"/> is thrown.
        /// </summary>
        public event SetGeoPointDelegate SetGeoPointEvent;
        /// <summary>
        /// Raised when the action is removed from the action stack, i.e. after the point was accepted with a mouse click or
        /// Enter, or the action was cancelled.
        /// </summary>
        public event ActionDoneDelegate ActionDoneEvent;
        private void SetGeoPoint(GeoPoint p)
        {
            if (SetGeoPointEvent != null)
            {
                SetGeoPointEvent(this, p);
            }
            else if (GeoPointProperty != null)
            {
                GeoPointProperty.SetGeoPoint(p);
            }
            else
                throw new GeneralGeoPointActionException("Event OnSetGeoPoint not set");
            if (GeoPointProperty != null)
            {
                GeoPointProperty.GeoPointChanged();
            }
        }
        private GeoPoint GetGeoPoint()
        {
            if (SetGeoPointEvent != null) return initialGeoPointValue;
            if (GeoPointProperty != null)
            {
                return GeoPointProperty.GetGeoPoint();
            }
            throw new GeneralGeoPointActionException("Event OnSetGeoPoint not set");
        }
        /// <summary>
        /// Implements <see cref="Action.OnSetAction"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnSetAction()
        {
            initialGeoPointValue = GetGeoPoint();
        }

        /// <summary>
        /// Returns the plane in which the mouse position is interpreted when it does not snap to anything: by default the
        /// plane through <see cref="basePoint"/> parallel to the drawing plane of the view, so that the point keeps its
        /// height above the drawing plane. Override to move the point in a different plane.
        /// </summary>
        /// <param name="vw">The view the mouse is in</param>
        /// <returns>The plane onto which the mouse position is projected</returns>
        protected virtual Plane GetMousePlane(IView vw)
        {
            return new Plane(basePoint, vw.Projection.DrawingPlane.DirectionX, vw.Projection.DrawingPlane.DirectionY);
        }
        /// <summary>
        /// Implements <see cref="Action.OnMouseMove"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public override void OnMouseMove(MouseEventArgs e, IView vw)
        {
            using (Frame.Project.Undo.ContextFrame(this))
            {
                SnapPointFinder.DidSnapModes DidSnap;
                GeoPoint p = base.SnapPoint(e, basePoint, vw, out DidSnap);
                if (DidSnap == SnapPointFinder.DidSnapModes.DidNotSnap)
                {
                    // der Punkt wurde also nicht gefangen, befindet sich somit in der Zeichenebene
                    // da diese Aktion aber bei Hotspots drankommt, soll aber statt der Zeichenebene die zur Zeichenebene
                    // parallele Ebene durch den basePoint (den Ausgangspunkt) genommen werden
                    Plane parallel = GetMousePlane(vw); //  new Plane(basePoint, vw.Projection.DrawingPlane.DirectionX, vw.Projection.DrawingPlane.DirectionY);
                    p = parallel.ToGlobal(parallel.Project(p));
                    // die Berechnung ist umständlich, man bräuchte nur den Offsetvektor einmal berechnen
                }
                SetGeoPoint(p);
            }
        }
        /// <inheritdoc/>
        public override string GetID()
        {
            return "GeneralGeoPointAction[LeaveSelectProperties]"; // LeaveSelectProperties ist das Zauberwort, 
            // damit die Properties der SelectObjectAction stehen bleiben.
        }
        /// <inheritdoc/>
        public override void OnRemoveAction()
        {
            if (ActionDoneEvent != null) ActionDoneEvent(this);
        }
        /// <summary>
        /// Implements <see cref="Action.OnMouseUp"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public override void OnMouseUp(MouseEventArgs e, IView vw)
        {
            using (Frame.Project.Undo.ContextFrame(this))
            {
                SnapPointFinder.DidSnapModes DidSnap;
                GeoPoint p = base.SnapPoint(e, basePoint, vw, out DidSnap);
                if (DidSnap == SnapPointFinder.DidSnapModes.DidNotSnap)
                {
                    // der Punkt wurde also nicht gefangen, befindet sich somit in der Zeichenebene
                    // da diese Aktion aber bei Hotspots drankommt, soll aber statt der Zeichenebene die zur Zeichenebene
                    // parallele Ebene durch den basePoint (den Ausgangspunkt) genommen werden
                    Plane parallel = GetMousePlane(vw); //  new Plane(basePoint, vw.Projection.DrawingPlane.DirectionX, vw.Projection.DrawingPlane.DirectionY);
                    p = parallel.ToGlobal(parallel.Project(p));
                    // die Berechnung ist umständlich, man bräuchte nur den Offsetvektor einmal berechnen
                }
                SetGeoPoint(p);
            }
            Frame.Project.Undo.ClearContext(); // also die nächsten Änderungen sind ein neuer undo Schritt
            if (GeoPointProperty != null)
            {
                GeoPointProperty.ModifiedByAction(this);
            }
            base.RemoveThisAction();
        }
        /// <summary>
        /// Implements <see cref="Action.OnMouseDown"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public override void OnMouseDown(MouseEventArgs e, IView vw)
        {
            base.OnMouseDown(e, vw);
        }
        /// <summary>
        /// Implements <see cref="Action.OnActivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnActivate(Action OldActiveAction, bool SettingAction)
        {
            // hier dem Undosystem sagen, dass nur die erste Bewegung festgehalten werden soll
            if (GeoPointProperty != null)
            {
                // GeoPointProperty.CheckMouseButton(true);
                GeoPointProperty.FireModifyWithMouse(true);
            }
        }
        /// <summary>
        /// Implements <see cref="Action.OnInactivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="NewActiveAction">the action that will become active</param>
        /// <param name="RemovingAction">true: if called because the action is removed, false: if called when a new action is set on top of this action</param>
        public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
            if (GeoPointProperty != null)
            {
                GeoPointProperty.FireModifyWithMouse(false);
                // GeoPointProperty.CheckMouseButton(false);
            }
            if (!RemovingAction)
            {
                SetGeoPoint(initialGeoPointValue);
                base.RemoveThisAction();
            }
        }
        /// <inheritdoc/>
        public override bool OnEscape()
        {
            SetGeoPoint(initialGeoPointValue);
            base.RemoveThisAction();
            // base.Frame.Project.Undo.IgnoreLastChange();
            return true;
        }
        /// <inheritdoc/>
        public override bool OnEnter()
        {
            if (GeoPointProperty != null)
            {
                GeoPointProperty.ModifiedByAction(this);
            }
            base.RemoveThisAction();
            return true;
        }
    }
}
