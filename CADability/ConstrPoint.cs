using CADability.GeoObject;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a single <see cref="CADability.GeoObject.Point"/> object at a position entered by the user.
    /// The point symbol is taken from the setting "KeepState.PointSymbol". Menu id: "Constr.Point".
    /// </summary>
    internal class ConstrPoint : ConstructAction
    {
        private Point gPoint;
        private static PointSymbol pointSymbol = (PointSymbol)Settings.GlobalSettings.GetIntValue("KeepState.PointSymbol", (int)PointSymbol.Plus);

        public ConstrPoint()
        {
        }

        private void GPoint(GeoPoint p)
        {
            gPoint.Location = p;
        }


        public override void OnSetAction()
        {
            gPoint = Point.Construct();
            gPoint.Location = ConstrDefaults.DefaultStartPoint;
            base.ActiveObject = gPoint;
            gPoint.Symbol = pointSymbol;

            base.TitleId = "Constr.Point";

            GeoPointInput pointInput = new GeoPointInput("Point");
            pointInput.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;
            pointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(GPoint);

            base.SetInput(pointInput);
            base.ShowAttributes = true;

            base.OnSetAction();

        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Point";
        }

        public override void OnDone()
        {
            if (base.ActiveObject is Point)
            {
                pointSymbol = (base.ActiveObject as Point).Symbol;
                Settings.GlobalSettings.SetValue("KeepState.PointSymbol", (int)pointSymbol);
            }
            base.OnDone();
        }


    }
}
