using CADability.GeoObject;
using CADability.UserInterface;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;

namespace CADability.Actions
{
    /// <summary>
    /// An action that lets the user modify a <see cref="LengthProperty"/> with the mouse. Depending on the constructor
    /// the length is either the distance of the (snapped) mouse position from a fixed point or its distance from a line
    /// given by a point and a direction. The length is updated on every mouse move; a mouse click accepts it, ESC (or
    /// the activation of another action) restores the initial value and ends the action. Used e.g. by the radius
    /// property of a circle or the size property of a text.
    /// </summary>

    public class GeneralLengthAction : Action
    {
        private readonly LengthProperty lengthProperty;
        private double initialLengthValue;
        private readonly GeoPoint fixPoint;
        private readonly GeoPoint linePoint;
        private readonly GeoVector lineDirection;
        private enum Mode { FromPoint, FromLine }
        private readonly Mode mode;
        /// <summary>
        /// Creates the action for <paramref name="lengthProperty"/>. The length is the distance of the (snapped) mouse
        /// position from <paramref name="fixPoint"/>.
        /// </summary>
        /// <param name="lengthProperty">The property to modify</param>
        /// <param name="fixPoint">The point the distance is measured from, e.g. the center of a circle for its radius</param>
        public GeneralLengthAction(LengthProperty lengthProperty, GeoPoint fixPoint)
        {
            this.lengthProperty = lengthProperty;
            this.fixPoint = fixPoint;
            mode = Mode.FromPoint;
        }
        /// <summary>
        /// Creates the action for <paramref name="lengthProperty"/> measuring from <paramref name="fixPoint"/> (see
        /// <see cref="GeneralLengthAction(LengthProperty, GeoPoint)"/>) and excludes <paramref name="ignoreSnap"/> from the
        /// snapping, which is usually the object that is being modified.
        /// </summary>
        /// <param name="lengthProperty">The property to modify</param>
        /// <param name="fixPoint">The point the distance is measured from</param>
        /// <param name="ignoreSnap">Object not to snap to, may be null</param>
        public GeneralLengthAction(LengthProperty lengthProperty, GeoPoint fixPoint, IGeoObject ignoreSnap) : this(lengthProperty, fixPoint)
        {
            if (ignoreSnap != null)
            {
                base.IgnoreForSnap = new GeoObjectList(ignoreSnap);
            }
        }
        /// <summary>
        /// Creates the action for <paramref name="lengthProperty"/> measuring from a line (see
        /// <see cref="GeneralLengthAction(LengthProperty, GeoPoint, GeoVector)"/>) and excludes <paramref name="ignoreSnap"/>
        /// from the snapping, which is usually the object that is being modified.
        /// </summary>
        /// <param name="lengthProperty">The property to modify</param>
        /// <param name="linePoint">A point on the line the distance is measured from</param>
        /// <param name="lineDirection">The direction of that line</param>
        /// <param name="ignoreSnap">Object not to snap to, may be null</param>
        public GeneralLengthAction(LengthProperty lengthProperty, GeoPoint linePoint, GeoVector lineDirection, IGeoObject ignoreSnap) : this(lengthProperty, linePoint, lineDirection)
        {
            if (ignoreSnap != null)
            {
                base.IgnoreForSnap = new GeoObjectList(ignoreSnap);
            }
        }

        /// <summary>
        /// Creates the action for <paramref name="lengthProperty"/>. The length is the distance of the (snapped) mouse
        /// position from the line through <paramref name="linePoint"/> with direction <paramref name="lineDirection"/>,
        /// e.g. the height of a rectangle measured from its base line.
        /// </summary>
        /// <param name="lengthProperty">The property to modify</param>
        /// <param name="linePoint">A point on the line the distance is measured from</param>
        /// <param name="lineDirection">The direction of that line</param>
        public GeneralLengthAction(LengthProperty lengthProperty, GeoPoint linePoint, GeoVector lineDirection)
        {
            this.lengthProperty = lengthProperty;
            this.linePoint = linePoint;
            this.lineDirection = lineDirection;
            mode = Mode.FromLine;
        }

        private void SetLength(double l)
        {
            lengthProperty.SetLength(l);
            lengthProperty.LengthChanged();
        }
        private double GetLength()
        {
            return lengthProperty.GetLength();
        }

        /// <summary>
        /// Implements <see cref="Action.OnSetAction"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnSetAction()
        {
            initialLengthValue = GetLength();
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
                SnapPointFinder.DidSnapModes didSnap;
                switch (mode)
                {
                    case Mode.FromPoint:
                        SetLength(Geometry.Dist(base.SnapPoint(e, fixPoint, vw, out didSnap), fixPoint));
                        break;
                    case Mode.FromLine:
                        SetLength(Geometry.DistPL(base.SnapPoint(e, fixPoint, vw, out didSnap), linePoint, lineDirection));
                        break;
                }
            }
        }
        /// <inheritdoc/>
        public override string GetID()
        {
            return "GeneralLengthAction[LeaveSelectProperties]";
        }

        /// <inheritdoc/>
		public override void OnRemoveAction()
        {
        }

        /// <summary>
        /// Implements <see cref="Action.OnMouseUp"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="e">MouseEventArgs of the call</param>
        /// <param name="vw">the IView which contains the mouse</param>
        public override void OnMouseUp(MouseEventArgs e, IView vw)
        {
            OnMouseMove(e, vw);
            Frame.Project.Undo.ClearContext(); //the next changes are a new undo step
            base.RemoveThisAction();
        }
        /// <summary>
        /// Implements <see cref="Action.OnActivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnActivate(Action oldActiveAction, bool settingAction)
        {
            lengthProperty.FireModifyWithMouse(true);
        }
        /// <summary>
        /// Implements <see cref="Action.OnInactivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="newActiveAction">the action that will become active</param>
        /// <param name="removingAction">true: if called because the action is removed, false: if called when a new action is set on top of this action</param>
        public override void OnInactivate(Action newActiveAction, bool removingAction)
        {
            lengthProperty.FireModifyWithMouse(false);
            if (!removingAction)
            {
                SetLength(initialLengthValue);
                base.RemoveThisAction();
            }
        }

        /// <inheritdoc/>
		public override bool OnEscape()
        {
            SetLength(initialLengthValue);
            base.RemoveThisAction();
            return true;
        }

        /// <inheritdoc/>
		public override bool OnEnter()
        {
            base.RemoveThisAction();
            return true;
        }
    }
}
