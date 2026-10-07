namespace CADability.Actions
{
    /// <summary>
    /// Delegate for the event indicating a change of an <see cref="IFeedBack"/> object.
    /// </summary>
    /// <param name="sender"></param>
    public delegate void FeedBackChangedDelegate(IFeedBack sender);
    /// <summary>
    /// Interface, which must be implemented by objects that act as eedback objects in Actions.
    /// See <see cref="ActionFeedBack"/> and <see cref="Action.FeedBack"/>
    /// </summary>

    public interface IFeedBack
    {
        /// <summary>
        /// Implements the painting of this object
        /// </summary>
        /// <param name="paintTo3D">Where to paint to</param>
        void PaintTo3D(IPaintTo3D paintTo3D);
        BoundingBox GetExtent();
        /// <summary>
        /// Event raised by this object to indicate a change.
        /// </summary>
        event FeedBackChangedDelegate FeedBackChangedEvent;
    }
}
