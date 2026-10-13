namespace CADability.Actions
{
    /// <summary>
    /// Placeholder for the "Measure" action (menu id "Measure"), which is meant to measure positions, lengths, angles
    /// and areas without creating objects. The action defines no input fields; the actual measuring functions are the
    /// entries of the "MenuId.Measure" sub menu, which start the corresponding construction actions
    /// (e.g. intersection point of two curves, point on an object, direction from two points).
    /// </summary>

    public class Measure : ConstructAction
    {
        public Measure()
        {
            // 
            // TODO: Add constructor logic here
            //
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

        public override void OnDone()
        { base.OnDone(); }
    }
}
