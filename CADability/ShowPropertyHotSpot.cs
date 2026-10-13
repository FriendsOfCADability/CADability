using CADability.Actions;

namespace CADability.UserInterface
{
    /// <summary>
    /// A hotspot (see <see cref="IHotSpot"/>) which stands for a property entry that has no position of its own, e.g. a length:
    /// it is displayed at the position set by its owner and, when the user drags it, starts a <see cref="GeneralGeoPointAction"/>
    /// and reports the new positions via <see cref="PositionChangedEvent"/>, so that the owner can update its property. Used e.g.
    /// for the width and height hotspots of a rectangular polyline.
    /// </summary>

    public class ShowPropertyHotSpot : IHotSpot
    {
        private IPropertyEntry showProperty;
        private GeoPoint position;
        private IFrame frame;
        /// <summary>
        /// Signature of the handlers of <see cref="PositionChangedEvent"/>: called with each new position while the user drags the
        /// hotspot.
        /// </summary>
        /// <param name="newPosition">The new position of the hotspot in world coordinates</param>
        public delegate void PositionChangedDelegate(GeoPoint newPosition);
        public event PositionChangedDelegate PositionChangedEvent;
        public ShowPropertyHotSpot(IPropertyEntry showProperty, IFrame frame)
        {
            this.frame = frame;
            this.showProperty = showProperty;
        }
        public GeoPoint Position
        {
            get { return position; }
            set { position = value; }
        }
#region IHotSpot Members
        /// <inheritdoc/>
        public GeoPoint GetHotspotPosition()
        {
            return position;
        }
        public bool IsSelected
        {
            get
            {
                return false;
            }
            set
            {
                // TODO:  Add ShowPropertyHotSpot.IsSelected setter implementation
            }
        }
        /// <inheritdoc/>
        public void StartDrag(IFrame frame)
        {
            if (PositionChangedEvent != null)
            {
                GeneralGeoPointAction gga = new GeneralGeoPointAction(position);
                gga.SetGeoPointEvent += new CADability.Actions.GeneralGeoPointAction.SetGeoPointDelegate(OnActionSetGeoPoint);
                frame.SetAction(gga);
            }
        }
        public string GetInfoText(CADability.UserInterface.InfoLevelMode Level)
        {
            return showProperty.Label;
        }
        public string ResourceId
        {
            get { return showProperty.ResourceId; }
        }
        MenuWithHandler[] IHotSpot.ContextMenu
        {
            get
            {
                return showProperty.ContextMenu;
            }
        }
        public bool Hidden { get { return showProperty.ReadOnly; } }
#endregion
        private void OnActionSetGeoPoint(GeneralGeoPointAction sender, GeoPoint NewValue)
        {
            if (PositionChangedEvent != null) PositionChangedEvent(NewValue);
        }
    }
}
