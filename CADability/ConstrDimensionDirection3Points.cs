using CADability.GeoObject;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of an angular dimension (<see cref="Dimension.EDimType.DimAngle"/>) defined by three points: the
    /// apex of the angle, a point on the first leg and a point on the second leg, followed by the position of the
    /// dimension line. The dimension lies in the active drawing plane. Menu id: "Constr.Dimension.Direction.3Points".
    /// </summary>
    internal class ConstrDimensionDirection3Points : ConstructAction
    {

        private Dimension dim;
        private GeoPointInput dimPoint1Input;
        private GeoPointInput dimPoint2Input;
        private GeoPointInput dimPoint3Input;
        private GeoPointInput dimLocationInput;
        private GeoPoint dimP1, dimP2, dimP3;
        private CADability.GeoObject.Point gPoint1, gPoint2, gPoint3;

        public ConstrDimensionDirection3Points()
        { }

        public GeoPoint LocationPointOffset()
        {   // die dim.plane hat als x-achse die Massgrundlinie!
            GeoPoint2D p = dim.Plane.Project(dim.DimLineRef);
            if (dim.Plane.Project(dim.GetPoint(0)).y > 0) // der 1. Bem.Punkt geht nach oben
                p.y = p.y - dim.DimensionStyle.LineIncrement; // Parameter: "Masslinien-Abstand"
            else p.y = p.y + dim.DimensionStyle.LineIncrement;
            return (dim.Plane.ToGlobal(p));
        }

        private void SetDimPoint1(GeoPoint p)
        {
            dimP1 = p;
            dim.SetPoint(0, p);
            gPoint1.Location = p;
            // das folgende dient nur der Optik, damit man eine Prototype-Bem. sieht!
            if (!dimPoint2Input.Fixed) dim.SetPoint(1, new GeoPoint(p.x + base.WorldLength(1), p.y + base.WorldLength(1)));
            if (!dimPoint3Input.Fixed) dim.SetPoint(2, new GeoPoint(p.x - base.WorldLength(1), p.y + base.WorldLength(1)));
            if (!dimLocationInput.Fixed) dim.DimLineRef = new GeoPoint(p.x, p.y + base.WorldLength(35));
        }

        private void SetDimPoint2(GeoPoint p)
        {
            dimP2 = p;
            gPoint2.Location = p;
            if (!dimPoint1Input.Fixed)
                dim.SetPoint(0, new GeoPoint(p.x - base.WorldLength(25), p.y - base.WorldLength(25)));
            Line lineT = Line.Construct();
            lineT.SetTwoPoints(dim.GetPoint(0), p);
            // wegen der Optik: Hilfspunkt nahe Mittelpunkt konstruieren
            dim.SetPoint(1, lineT.PointAt(0.01));
            if (!dimPoint3Input.Fixed) dim.SetPoint(2, new GeoPoint(dim.GetPoint(0).x - base.WorldLength(1), dim.GetPoint(0).y + base.WorldLength(1)));
            if (!dimLocationInput.Fixed) dim.DimLineRef = p;
        }

        private void SetDimPoint3(GeoPoint p)
        {
            dimP3 = p;
            gPoint3.Location = p;
            if (!dimPoint1Input.Fixed)
                dim.SetPoint(0, new GeoPoint(p.x + base.WorldLength(25), p.y - base.WorldLength(25)));
            Line lineT = Line.Construct();
            lineT.SetTwoPoints(dim.GetPoint(0), p);
            // wegen der Optik: Hilfspunkt nahe Mittelpunkt konstruieren
            dim.SetPoint(2, lineT.PointAt(0.01));
            if (!dimPoint2Input.Fixed) dim.SetPoint(1, new GeoPoint(dim.GetPoint(0).x + base.WorldLength(1), dim.GetPoint(0).y + base.WorldLength(1)));
            if (!dimLocationInput.Fixed) dim.DimLineRef = p;
        }


        private void SetDimLocation(GeoPoint p)
        {   // der Lagepunkt der Bemassung
            dim.DimLineRef = p;
            if (!dimPoint1Input.Fixed)
                dim.SetPoint(0, new GeoPoint(p.x, p.y - base.WorldLength(25)));
            if (!dimPoint2Input.Fixed)
                dim.SetPoint(1, new GeoPoint(dim.GetPoint(0).x + base.WorldLength(1), dim.GetPoint(0).y + base.WorldLength(1)));
            else dim.SetPoint(1, dimP2); // die richtigen Punkte nehmen
            if (!dimPoint3Input.Fixed)
                dim.SetPoint(2, new GeoPoint(dim.GetPoint(0).x - base.WorldLength(1), dim.GetPoint(0).y + base.WorldLength(1)));
            else dim.SetPoint(2, dimP3); // die richtigen Punkte nehmen
                                         // die richtigen Punkte nehmen
                                         //			dim.SetPoint(1,dimP2);
                                         //			dim.SetPoint(2,dimP3);
        }


        public override void OnSetAction()
        {
            base.TitleId = "Constr.Dimension.Direction.3Points";
            dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimAngle;
            dim.DimLineRef = ConstrDefaults.DefaultDimPoint;
            dim.Normal = base.ActiveDrawingPlane.Normal;
            dim.AddPoint(new GeoPoint(0.0, 0.0, 0.0));
            dim.AddPoint(new GeoPoint(0.0, 0.0, 0.0));
            dim.AddPoint(new GeoPoint(0.0, 0.0, 0.0));

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            gPoint2 = ActionFeedBack.FeedbackPoint(base.Frame);
            gPoint3 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);
            base.FeedBack.Add(gPoint2);
            base.FeedBack.Add(gPoint3);

            base.ActiveObject = dim;

            dimPoint1Input = new GeoPointInput("Constr.Dimension.Direction.3Points.Point1");
            dimPoint1Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimPoint1);
            dimPoint2Input = new GeoPointInput("Constr.Dimension.Direction.3Points.Point2");
            dimPoint2Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimPoint2);
            dimPoint3Input = new GeoPointInput("Constr.Dimension.Direction.3Points.Point3");
            dimPoint3Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimPoint3);

            dimLocationInput = new GeoPointInput("Constr.Dimension.Location");
            dimLocationInput.DefaultGeoPoint = ConstrDefaults.DefaultDimPoint;
            dimLocationInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimLocation);

            base.SetInput(dimPoint1Input, dimPoint2Input, dimPoint3Input, dimLocationInput);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Dimension.Direction.3Points";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultDimPoint.Point = LocationPointOffset();
            dim.SetPoint(0, dimP1);
            dim.SetPoint(1, dimP2);
            dim.SetPoint(2, dimP3);
            base.OnDone();
        }

    }
}


