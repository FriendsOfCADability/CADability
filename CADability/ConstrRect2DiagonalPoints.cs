using CADability.GeoObject;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a rectangle (a closed <see cref="Polyline"/>) by two diagonally opposite corners. The user picks the first
    /// corner and the diagonal corner; one side of the rectangle is parallel to the x-direction of the active drawing plane, the
    /// other side goes from the foot point of the second corner on that direction to the second corner. Started with the menu id
    /// "Constr.Rect.Rect2DiagonalPoints".
    /// </summary>
    internal class ConstrRect2DiagonalPoints : ConstructAction
    {
        public ConstrRect2DiagonalPoints()
        { }

        private Polyline line;
        private GeoPointInput secondPointInput;

        private void StartPoint(GeoPoint p)
        {
            if (secondPointInput.Fixed)
            {
                line.RectangleLocation = p;
            }
            else
            {
                line.SetRectangle(p, line.RectangleWidth * base.ActiveDrawingPlane.DirectionX, line.RectangleHeight * base.ActiveDrawingPlane.DirectionY);
            }

        }

        private void SecondPoint(GeoPoint p)
        {   // falls der 2. Punkt über einen Punkt im Raum über dem jetzigen Rechteck bestimmt wird:
            // der Lotfußpunkt von p auf die HauptAchse (x-Direction)
            //			GeoPoint pl = Geometry.DropPL(p,line.RectangleLocation,line.ParallelogramMainDirection);
            GeoPoint pl = Geometry.DropPL(p, line.RectangleLocation, base.ActiveDrawingPlane.DirectionX);
            if ((!Precision.IsEqual(line.RectangleLocation, pl)) & (!Precision.IsEqual(pl, p)))
            {   // Neues Rechteck mit neuer Orientierung im Raum, x-Vektor: Startpunkt-Lotfußpunkt y-Vektor: Lotfußpunkt-p 
                line.SetRectangle(line.RectangleLocation, new GeoVector(line.RectangleLocation, pl), new GeoVector(pl, p));
            }
        }
        private GeoPoint GetSecondPoint()
        {
            return line.GetPoint(2);
        }

        public override void OnSetAction()
        {
            line = Polyline.Construct();
            line.SetRectangle(ConstrDefaults.DefaultStartPoint, ConstrDefaults.DefaultRectWidth * base.ActiveDrawingPlane.DirectionX, ConstrDefaults.DefaultRectHeight * base.ActiveDrawingPlane.DirectionY);

            base.ActiveObject = line;
            base.TitleId = "Constr.Rect.Rect2DiagonalPoints";

            GeoPointInput startPointInput = new GeoPointInput("Rect.StartPoint");
            startPointInput.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;
            startPointInput.DefinesBasePoint = true;
            startPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(StartPoint);

            secondPointInput = new GeoPointInput("Rect.DiagonalPoint");
            secondPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SecondPoint);
            secondPointInput.GetGeoPointEvent += new ConstructAction.GeoPointInput.GetGeoPointDelegate(GetSecondPoint);


            base.SetInput(startPointInput, secondPointInput);
            base.ShowAttributes = true;

            base.OnSetAction();
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Rect.Rect2DiagonalPoints";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultStartPoint.Point = line.RectangleLocation + line.ParallelogramMainDirection + line.ParallelogramSecondaryDirection;
            // wird auf den Diagonalpunkt gesetzt
            base.OnDone();
        }
    }
}
