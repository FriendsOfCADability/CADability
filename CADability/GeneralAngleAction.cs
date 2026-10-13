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
    /// 
    /// </summary>

    public class GeneralAngleAction : Action
    {
        private AngleProperty angleProperty;
        private Angle InitialAngleValue;
        private GeoPoint fixPoint;
        private Plane localPlane;
        Boolean usesLocalPlane;

        public GeneralAngleAction(AngleProperty angleProperty, GeoPoint fixPoint)
        {
            this.angleProperty = angleProperty;
            this.fixPoint = fixPoint;
            usesLocalPlane = false;
        }

        public GeneralAngleAction(AngleProperty angleProperty, Plane localPlane)
        {
            this.angleProperty = angleProperty;
            this.localPlane = localPlane;
            usesLocalPlane = true;
        }

        public delegate double CalculateAngleDelegate(GeoPoint MousePosition);
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

        /// <summary>
        /// Implements <see cref="Action.GetID"/>.
        /// </summary>
        public override string GetID()
        {
            return "GeneralAngleAction[LeaveSelectProperties]";
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

        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.OnEscape ()"/>
        /// </summary>
		public override bool OnEscape()
        {
            SetAngle(InitialAngleValue);
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
