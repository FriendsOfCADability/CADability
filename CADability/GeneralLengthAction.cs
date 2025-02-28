using CADability.GeoObject;
using CADability.UserInterface;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;

namespace CADability.Actions
{
    /// <summary>
    /// 
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
        public GeneralLengthAction(LengthProperty lengthProperty, GeoPoint fixPoint)
        {
            this.lengthProperty = lengthProperty;
            this.fixPoint = fixPoint;
            mode = Mode.FromPoint;
        }
        public GeneralLengthAction(LengthProperty lengthProperty, GeoPoint fixPoint, IGeoObject ignoreSnap) : this(lengthProperty, fixPoint)
        {
            if (ignoreSnap != null)
            {
                base.IgnoreForSnap = new GeoObjectList(ignoreSnap);
            }
        }
        public GeneralLengthAction(LengthProperty lengthProperty, GeoPoint linePoint, GeoVector lineDirection, IGeoObject ignoreSnap) : this(lengthProperty, linePoint, lineDirection)
        {
            if (ignoreSnap != null)
            {
                base.IgnoreForSnap = new GeoObjectList(ignoreSnap);
            }
        }

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
        /// <param name="e"><paramref name="Action.OnMouseMove.e"/></param>
        /// <param name="vw"><paramref name="Action.OnMouseMove.vw"/></param>
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
        /// <summary>
        /// Implements <see cref="Action.GetID"/>.
        /// </summary>
        public override string GetID()
        {
            return "GeneralLengthAction[LeaveSelectProperties]";
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.OnRemoveAction ()"/>
        /// </summary>
		public override void OnRemoveAction()
        {
        }

        /// <summary>
        /// Implements <see cref="Action.OnMouseUp"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="e"><paramref name="Action.OnMouseUp.e"/></param>
        /// <param name="vw"><paramref name="Action.OnMouseUp.vw"/></param>
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
        /// <param name="newActiveAction"><paramref name="Action.OnInactivate.NewActiveAction"/></param>
        /// <param name="removingAction"><paramref name="Action.OnInactivate.RemovingAction"/></param>
        public override void OnInactivate(Action newActiveAction, bool removingAction)
        {
            lengthProperty.FireModifyWithMouse(false);
            if (!removingAction)
            {
                SetLength(initialLengthValue);
                base.RemoveThisAction();
            }
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.OnEscape ()"/>
        /// </summary>
		public override bool OnEscape()
        {
            SetLength(initialLengthValue);
            base.RemoveThisAction();
            return true;
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.OnEnter ()"/>
        /// </summary>
		public override bool OnEnter()
        {
            base.RemoveThisAction();
            return true;
        }
    }
}
