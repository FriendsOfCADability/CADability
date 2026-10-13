using CADability.Actions;

namespace CADability.UserInterface
{
    /// <summary>
    /// 
    /// </summary>

    public class ShowPropertyHotSpot : IHotSpot
    {
        private IPropertyEntry showProperty;
        private GeoPoint position;
        private IFrame frame;
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
        /// <summary>
        /// Implements <see cref="CADability.IHotSpot.GetHotspotPosition ()"/>
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// Implements <see cref="CADability.IHotSpot.StartDrag(IFrame)"/>
        /// </summary>
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
