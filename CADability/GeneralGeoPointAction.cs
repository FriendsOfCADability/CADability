using CADability.GeoObject;
using CADability.UserInterface;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;

namespace CADability.Actions
{

    public class GeneralGeoPointActionException : System.ApplicationException
    {
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
        protected GeoPoint basePoint;
        public GeoPointProperty GeoPointProperty;
        private GeoPoint initialGeoPointValue;
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
        public GeneralGeoPointAction(GeoPoint initialGeoPointValue, IGeoObject ignoreSnap)
            : this(initialGeoPointValue)
        {
            if (ignoreSnap != null)
            {
                base.IgnoreForSnap = new GeoObjectList(ignoreSnap);
            }
        }
        public GeneralGeoPointAction(GeoPointProperty GeoPointProperty)
        {
            this.GeoPointProperty = GeoPointProperty;
            basePoint = GeoPointProperty.GetGeoPoint();
        }
        public GeneralGeoPointAction(GeoPointProperty GeoPointProperty, IGeoObject ignoreSnap)
            : this(GeoPointProperty)
        {
            if (ignoreSnap != null)
            {
                base.IgnoreForSnap = new GeoObjectList(ignoreSnap);
            }
        }
        public delegate void SetGeoPointDelegate(GeneralGeoPointAction sender, GeoPoint NewValue);
        public delegate void ActionDoneDelegate(GeneralGeoPointAction sender);
        public event SetGeoPointDelegate SetGeoPointEvent;
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
