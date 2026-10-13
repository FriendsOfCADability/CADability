using CADability.GeoObject;
using CADability.UserInterface;
using System;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;

namespace CADability.Actions
{
    /// <summary>
    /// Simple <see cref="Action"/> to modify a GeoVector. This action doesn't set up an own
    /// ControlCenter entry but assumes there is a <see cref="GeoVectorProperty"/> entry active in the ControlCenter.
    /// </summary>

    public class GeneralGeoVectorAction : Action
    {
        private GeoVectorProperty geoVectorProperty;
        private GeoVector initialGeoVectorValue;
        private GeoPoint basePoint;
        /// <summary>
        /// UserData to differentiate between multiple GeneralGeoVectorActions.
        /// </summary>
		public UserData UserData;
        /// <summary>
        /// Constructs a GeneralGeoVectorAction to modify the provided <see cref="GeoVectorProperty"/>
        /// </summary>
        /// <param name="ToModify">Property to modify</param>
        /// <param name="basePoint">A basePoint for the modification</param>
        public GeneralGeoVectorAction(GeoVectorProperty ToModify, GeoPoint basePoint)
        {
            this.geoVectorProperty = ToModify;
            initialGeoVectorValue = ToModify.GetGeoVector();
            this.basePoint = basePoint;
        }
        public GeneralGeoVectorAction(GeoVectorProperty ToModify, GeoPoint basePoint, IGeoObject ignoreSnap) : this(ToModify, basePoint)
        {
            if (ignoreSnap != null)
            {
                base.IgnoreForSnap = new GeoObjectList(ignoreSnap);
            }
        }
        /// <summary>
        /// Type definition for <see cref="SetGeoVectorEvent"/>.
        /// </summary>
        /// <param name="sender">Source of notification (this object)</param>
        /// <param name="NewValue">New value for the GeoPoint</param>
        public delegate void SetGeoVectorDelegate(GeneralGeoVectorAction sender, GeoVector NewValue);
        /// <summary>
        /// Event beeing called when the property changes. You don't need to specify a handler here because
        /// GeoVectorProperty.SetGeoVector is beeing called.
        /// </summary>
        public event SetGeoVectorDelegate SetGeoVectorEvent;
        private void SetGeoVector(GeoVector v)
        {
            if (SetGeoVectorEvent != null)
            {
                SetGeoVectorEvent(this, v);
            }
            else if (geoVectorProperty != null)
            {
                geoVectorProperty.SetGeoVector(v);
            }
            else
            {
                throw new NotImplementedException("SetGeoVector");
            }
            if (geoVectorProperty != null)
            {
                geoVectorProperty.GeoVectorChanged();
            }
        }
        private GeoVector GetGeoVector()
        {
            if (SetGeoVectorEvent != null) return initialGeoVectorValue;
            if (geoVectorProperty != null)
            {
                return geoVectorProperty.GetGeoVector();
            }
            throw new NotImplementedException("GetGeoVector");
        }
        /// <summary>
        /// Implements <see cref="Action.OnSetAction"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnSetAction()
        {
            initialGeoVectorValue = GetGeoVector();
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
                SetGeoVector(base.SnapPoint(e, vw, out DidSnap) - basePoint);
            }
        }
        /// <inheritdoc/>
        public override string GetID()
        {
            return "GeneralGeoVectorAction[LeaveSelectProperties]"; // LeaveSelectProperties ist das Zauberwort, 
                                                                    // damit die Properties der SelectObjectAction stehen bleiben.
        }
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
            using (Frame.Project.Undo.ContextFrame(this))
            {
                SnapPointFinder.DidSnapModes DidSnap;
                SetGeoVector(base.SnapPoint(e, vw, out DidSnap) - basePoint);
            }
            Frame.Project.Undo.ClearContext(); // also die nächsten Änderungen sind ein neuer undo Schritt
            base.RemoveThisAction();
        }
        /// <summary>
        /// Implements <see cref="Action.OnActivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnActivate(Action OldActiveAction, bool SettingAction)
        {
            if (geoVectorProperty != null)
            {
                geoVectorProperty.FireModifyWithMouse(true);
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
            if (geoVectorProperty != null)
            {
                geoVectorProperty.FireModifyWithMouse(false);
            }
            if (!RemovingAction)
            {
                SetGeoVector(initialGeoVectorValue);
                base.RemoveThisAction();
            }
        }
        public override bool OnEscape()
        {
            SetGeoVector(initialGeoVectorValue);
            base.RemoveThisAction();
            return true;
        }
        public override bool OnEnter()
        {
            base.RemoveThisAction();
            return true;
        }
    }
}
