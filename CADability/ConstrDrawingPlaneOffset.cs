namespace CADability.Actions
{
    /// <summary>
    /// Sets the drawing plane of a <see cref="Projection"/> to a parallel offset of a given plane: the plane handed to the
    /// constructor (one of the standard planes XY, XZ or YZ) is moved so that it passes through a point the user picks, and is
    /// then set as the drawing plane of the projection. Used by the drawing plane property when the user chooses an offset
    /// standard plane. Its id is "Construct.DrawingPlane.Offset".
    /// </summary>
    internal class ConstrDrawingPlaneOffset : ConstructAction
    {
        private Plane basePlane;
        private Projection projection;
        private GeoPoint throughPoint;
        public ConstrDrawingPlaneOffset(Plane basePlane, Projection projection)
        {
            this.basePlane = basePlane;
            this.projection = projection;
            throughPoint = basePlane.Location;
            base.ChangeTabInControlCenter = true;
        }
        public override void OnSetAction()
        {
            base.TitleId = "DrawingPlane.Constr.Offset";
            GeoPointInput geoPointInput = new GeoPointInput("DrawingPlane.Offset");
            geoPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetThroughPoint);
            geoPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetThroughPoint);
            base.SetInput(geoPointInput);
            base.OnSetAction();
        }

        public override string GetID()
        {
            return "Construct.DrawingPlane.Offset";
        }

        public override void OnDone()
        {
            basePlane.Location = throughPoint;
            projection.DrawingPlane = basePlane;
            base.OnDone();
        }

        private GeoPoint OnGetThroughPoint()
        {
            return throughPoint;
        }

        private void OnSetThroughPoint(GeoPoint p)
        {
            throughPoint = p;
        }
    }
}
