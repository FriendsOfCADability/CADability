using System;

namespace CADability.Actions
{
    /// <summary>
    /// Obsolete placeholder for a "Measure" action (menu id "Measure") that was meant to measure positions, lengths, angles
    /// and areas without creating objects. It defines no input fields, so starting it throws a
    /// <see cref="ConstructActionException"/>; the frame does not start it any more. The measuring functions are the
    /// entries of the "MenuId.Measure" sub menu, which start the corresponding construction actions (e.g. intersection
    /// point of two curves, point on an object, direction from two points). The class will be removed in the next minor
    /// version.
    /// </summary>
    [Obsolete("Placeholder without input fields, starting it throws; use the construction actions of the \"MenuId.Measure\" sub menu. Will be removed in the next minor version.")]
    public class Measure : ConstructAction
    {
        /// <summary>
        /// Creates the placeholder action.
        /// </summary>
        public Measure()
        {
        }
        /// <summary>
        /// Implements <see cref="Action.OnSetAction"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnSetAction()
        {
            base.TitleId = "Measure";
            base.SetInput();

            base.OnSetAction();
        }
        /// <inheritdoc/>
        public override string GetID()
        { return "Measure"; }

        /// <inheritdoc/>
        public override void OnDone()
        { base.OnDone(); }
    }
}
