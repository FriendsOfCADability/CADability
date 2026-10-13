using CADability.GeoObject;
using CADability.UserInterface;
using System;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;

namespace CADability.Actions
{
    /// <summary>
    /// Action which lets the user set the value of an <see cref="AngleProperty"/> with the mouse. While the action is active the
    /// angle is the direction from a fixed point to the current (snapped) mouse position, measured in the active drawing plane,
    /// or, if the action was constructed with a local plane, the direction from the origin of that plane to the mouse position
    /// projected into it; a <see cref="CalculateAngleEvent"/> handler may compute the angle from the mouse position instead.
    /// A mouse click or Enter accepts the value, Escape restores the initial value. Typically started by the hotspot of an angle property.
    /// </summary>

    public class GeneralAngleAction : Action
    {
        private AngleProperty angleProperty;
        private Angle InitialAngleValue;
        private GeoPoint fixPoint;
        private Plane localPlane;
        Boolean usesLocalPlane;

        /// <summary>
        /// Creates the action for <paramref name="angleProperty"/>. The angle is the direction from <paramref name="fixPoint"/>
        /// to the mouse position, both projected into the active drawing plane.
        /// </summary>
        /// <param name="angleProperty">The property to modify</param>
        /// <param name="fixPoint">The vertex of the angle in model coordinates, e.g. the center of an ellipse</param>
        public GeneralAngleAction(AngleProperty angleProperty, GeoPoint fixPoint)
        {
            this.angleProperty = angleProperty;
            this.fixPoint = fixPoint;
            usesLocalPlane = false;
        }

        /// <summary>
        /// Creates the action for <paramref name="angleProperty"/>. The angle is the direction from the location of
        /// <paramref name="localPlane"/> to the mouse position projected into that plane, measured from the plane's x-axis.
        /// </summary>
        /// <param name="angleProperty">The property to modify</param>
        /// <param name="localPlane">The plane in which the angle is measured, e.g. the plane of a circle (whose location is the center)</param>
        public GeneralAngleAction(AngleProperty angleProperty, Plane localPlane)
        {
            this.angleProperty = angleProperty;
            this.localPlane = localPlane;
            usesLocalPlane = true;
        }

        /// <summary>
        /// Delegate for <see cref="CalculateAngleEvent"/>.
        /// </summary>
        /// <param name="MousePosition">The (snapped) mouse position in model coordinates</param>
        /// <returns>The angle in radians to set</returns>
        public delegate double CalculateAngleDelegate(GeoPoint MousePosition);
        /// <summary>
        /// If a handler is attached, it computes the angle from the mouse position instead of the default calculation,
        /// e.g. the parameter of an ellipse for the start angle of an elliptical arc.
        /// </summary>
        public event CalculateAngleDelegate CalculateAngleEvent;
        private void SetAngle(Angle l)
        {
            angleProperty.SetAngle(l);
        }
        private Angle GetAngle()
        {
            return angleProperty.GetAngle();
        }

        /// <summary>
        /// Implements <see cref="Action.OnSetAction"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnSetAction()
        {
            InitialAngleValue = GetAngle();
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
                GeoPoint p = base.SnapPoint(e, fixPoint, vw, out DidSnap);
                // hier möglicherweise einige Fangmethoden ausschalten
                // der fixPoint und der aktuelle Punkt werden in die aktuelle Zeichenebene projiziert
                if (CalculateAngleEvent != null)
                {
                    SetAngle(new Angle(CalculateAngleEvent(p)));
                }
                else
                {
                    if (usesLocalPlane)
                    {
                        GeoPoint2D pp = localPlane.Project(p);
                        SetAngle(new Angle(pp, GeoPoint2D.Origin));
                    }
                    else
                    {
                        GeoPoint2D pp = ActiveDrawingPlane.Project(p);
                        GeoPoint2D pfixpoint = ActiveDrawingPlane.Project(fixPoint);
                        SetAngle(new Angle(pp, pfixpoint));
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override string GetID()
        {
            return "GeneralAngleAction[LeaveSelectProperties]";
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
            base.RemoveThisAction();
        }

        /// <summary>
        /// Implements <see cref="Action.OnActivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnActivate(Action OldActiveAction, bool SettingAction)
        {
            angleProperty.FireModifyWithMouse(true);
        }
        /// <summary>
        /// Implements <see cref="Action.OnInactivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="NewActiveAction">the action that will become active</param>
        /// <param name="RemovingAction">true: if called because the action is removed, false: if called when a new action is set on top of this action</param>
        public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
            angleProperty.FireModifyWithMouse(false);
            if (!RemovingAction)
            {
                SetAngle(InitialAngleValue);
                base.RemoveThisAction();
            }
        }

        /// <inheritdoc/>
		public override bool OnEscape()
        {
            SetAngle(InitialAngleValue);
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
