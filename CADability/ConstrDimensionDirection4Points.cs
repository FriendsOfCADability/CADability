using CADability.GeoObject;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of an angular dimension between two directions, each given by two points: the user picks the two points
    /// of the first direction, the two points of the second direction and finally the location of the dimension line.
    /// The dimension lies in the active drawing plane. Menu id: "Constr.Dimension.Direction.4Points".
    /// </summary>
    internal class ConstrDimensionDirection4Points : ConstructAction
    {

        private Dimension dim;
        private GeoPointInput dimPoint1Input;
        private GeoPointInput dimPoint2Input;
        private GeoPointInput dimPoint3Input;
        private GeoPointInput dimPoint4Input;
        private GeoPointInput dimLocationInput;
        private GeoPoint dimP1, dimP2, dimP3, dimP4;
        private CADability.GeoObject.Point gPoint1, gPoint2, gPoint3, gPoint4;

        public ConstrDimensionDirection4Points()
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
            if (!dimPoint2Input.Fixed) dim.SetPoint(1, new GeoPoint(p.x + base.WorldLength(1), p.y + base.WorldLength(1)));
            if (!dimPoint3Input.Fixed) dim.SetPoint(2, new GeoPoint(p.x - base.WorldLength(1), p.y + base.WorldLength(1)));
            if (!dimLocationInput.Fixed) dim.DimLineRef = new GeoPoint(p.x, p.y + base.WorldLength(25));
        }

        private void SetDimPoint2(GeoPoint p)
        {
            dimP2 = p;
            gPoint2.Location = p;
            if (!dimPoint1Input.Fixed) dim.SetPoint(0, new GeoPoint(p.x - base.WorldLength(25), p.y - base.WorldLength(25)));
            if (!dimPoint3Input.Fixed) dim.SetPoint(2, new GeoPoint(dim.GetPoint(0).x - base.WorldLength(1), dim.GetPoint(0).y + base.WorldLength(1)));
            Line line1 = Line.Construct();
            line1.SetTwoPoints(dim.GetPoint(0), dimP2);
            dim.SetPoint(1, line1.PointAt(0.01));
            if (!dimLocationInput.Fixed) dim.DimLineRef = p;
        }

        private void SetDimPoint3(GeoPoint p)
        {
            dimP3 = p;
            gPoint3.Location = p;
            if (!dimPoint1Input.Fixed) dim.SetPoint(0, new GeoPoint(p.x + base.WorldLength(25), p.y - base.WorldLength(25)));
            if (!dimPoint2Input.Fixed) dim.SetPoint(1, new GeoPoint(dim.GetPoint(0).x + base.WorldLength(1), dim.GetPoint(0).y + base.WorldLength(1)));
            Line line1 = Line.Construct();
            line1.SetTwoPoints(dim.GetPoint(0), dimP3);
            dim.SetPoint(2, line1.PointAt(0.01));
            if (!dimLocationInput.Fixed) dim.DimLineRef = p;
        }

        private bool GetCenter(out GeoPoint c)
        {
            Plane pl;
            c = dimP4;
            Line line1 = Line.Construct();
            line1.SetTwoPoints(dimP1, dimP2);
            Line line2 = Line.Construct();
            line2.SetTwoPoints(dimP3, dimP4);
            if (Curves.GetCommonPlane(line1, line2, out pl))
            {
                GeoPoint2D dimC2D;
                if (Geometry.IntersectLL(pl.Project(dimP1), pl.Project(dimP2), pl.Project(dimP3), pl.Project(dimP4), out dimC2D))
                {
                    c = pl.ToGlobal(dimC2D);
                    return true;
                }
            }
            return false;
        }

        private void SetDimPoint4(GeoPoint p)
        {
            dimP4 = p;
            gPoint4.Location = p;
            GeoPoint dimC;
            if (dimPoint1Input.Fixed & dimPoint2Input.Fixed & dimPoint3Input.Fixed)
                if (GetCenter(out dimC))
                {
                    dim.SetPoint(0, dimC);
                    Line line1 = Line.Construct();
                    line1.SetTwoPoints(dimC, dimP1);
                    dim.SetPoint(1, line1.PointAt(0.1));
                    line1.SetTwoPoints(dimC, dimP3);
                    dim.SetPoint(2, line1.PointAt(0.1));
                    dim.DimLineRef = p;

                }
        }

        private void SetDimLocation(GeoPoint p)
        {   // der Lagepunkt der Bemassung

            dim.DimLineRef = p;
            if (dimPoint1Input.Fixed & dimPoint2Input.Fixed & dimPoint3Input.Fixed & dimPoint4Input.Fixed)
            {
                GeoVector v = new GeoVector(dim.GetPoint(0), dimP1);
                if (!v.IsNullVector()) v.Norm();
                v = Geometry.Dist(dim.GetPoint(0), p) * v;
                GeoPoint pLoc = dim.GetPoint(0) + v;
                if (Geometry.Dist(dimP1, pLoc) < Geometry.Dist(dimP2, pLoc))
                    dim.SetPoint(1, dimP1);
                else dim.SetPoint(1, dimP2);
                v = new GeoVector(dim.GetPoint(0), dimP3);
                if (!v.IsNullVector()) v.Norm();
                v = Geometry.Dist(dim.GetPoint(0), p) * v;
                pLoc = dim.GetPoint(0) + v;
                if (Geometry.Dist(dimP3, pLoc) < Geometry.Dist(dimP4, pLoc))
                    dim.SetPoint(2, dimP3);
                else dim.SetPoint(2, dimP4);
            }
        }


        public override void OnSetAction()
        {
            base.TitleId = "Constr.Dimension.Direction.4Points";
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
            gPoint4 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);
            base.FeedBack.Add(gPoint2);
            base.FeedBack.Add(gPoint3);
            base.FeedBack.Add(gPoint4);

            base.ActiveObject = dim;

            dimPoint1Input = new GeoPointInput("Constr.Dimension.Direction.4Points.Point1");
            dimPoint1Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimPoint1);
            dimPoint2Input = new GeoPointInput("Constr.Dimension.Direction.4Points.Point2");
            dimPoint2Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimPoint2);
            dimPoint3Input = new GeoPointInput("Constr.Dimension.Direction.4Points.Point3");
            dimPoint3Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimPoint3);
            dimPoint4Input = new GeoPointInput("Constr.Dimension.Direction.4Points.Point4");
            dimPoint4Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimPoint4);

            dimLocationInput = new GeoPointInput("Constr.Dimension.Location");
            dimLocationInput.DefaultGeoPoint = ConstrDefaults.DefaultDimPoint;
            dimLocationInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimLocation);

            base.SetInput(dimPoint1Input, dimPoint2Input, dimPoint3Input, dimPoint4Input, dimLocationInput);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Dimension.Direction.4Points";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultDimPoint.Point = LocationPointOffset();
            base.OnDone();
        }

    }
}



