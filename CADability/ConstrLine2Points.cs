using CADability.GeoObject;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a line segment by its start point and its end point. Instead of the end point the user may also
    /// enter the length and the direction of the line. Menu id: "Constr.Line.TwoPoints".
    /// </summary>
    internal class ConstrLine2Points : ConstructAction
    {
        private Line line;
        private GeoPointInput startPointInput;
        private GeoPointInput endPointInput;
        public ConstrLine2Points()
        {
        }
        private double OnGetLength()
        {
            return line.Length;
        }

        private void OnSetStartPoint(GeoPoint p)
        {
            line.StartPoint = p;
            // wenn für den Endpunkt noch keine Eingabe erfolgt ist, wird er hier identisch
            // zum Startpunkt gesetzt
            if (!endPointInput.Fixed) line.EndPoint = p;
            else base.ShowActiveObject = true;
        }

        private void OnSetEndPoint(GeoPoint p)
        {
            line.EndPoint = p;
            if (startPointInput.Fixed) base.ShowActiveObject = true;
            // der Startpunkt bleibt unverändert, auch wenn noch nicht definiert, da
            // er ja ein DefaultGeoPoint hat und immer den Wert des letzten Endpunktes
            bool debug = false;
            if (debug && startPointInput.Fixed)
            {
                ICurve l = base.CurrentMouseView.Model.AllObjects[1] as Line;
                l.Reverse();
            }
        }

        private GeoPoint OnGetEndPoint()
        {
            return line.EndPoint;
        }

        private GeoVector GetGeoVector()
        {
            return (line.StartDirection);
        }

        public override void OnSetAction()
        {
            line = Line.Construct();
            line.StartPoint = ConstrDefaults.DefaultStartPoint;
            line.EndPoint = line.StartPoint;
            base.ActiveObject = line;
            base.ShowActiveObject = false;
            base.TitleId = "Constr.Line.TwoPoints";

            startPointInput = new GeoPointInput("Line.StartPoint");
            startPointInput.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;
            startPointInput.DefinesBasePoint = true;
            startPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetStartPoint);

            endPointInput = new GeoPointInput("Line.EndPoint");
            endPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetEndPoint);
            endPointInput.GetGeoPointEvent += new ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetEndPoint);

            LengthInput len = new LengthInput("Line.Length");
            len.Optional = true;
            len.ReadOnly = true;
            len.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(OnGetLength);

            GeoVectorInput dir = new GeoVectorInput("Line.Direction");
            dir.Optional = true;
            dir.ReadOnly = true;
            dir.GetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.GetGeoVectorDelegate(GetGeoVector);
            dir.IsAngle = true;

            base.SetInput(startPointInput, endPointInput, len, dir);
            base.ShowAttributes = true;

            base.OnSetAction();

        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Line.TwoPoints";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultStartPoint.Point = line.EndPoint; // wird auf den letzten Endpunkt gesetzt
            base.OnDone();
        }


    }
}
