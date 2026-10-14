using CADability.GeoObject;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a rectangle (a closed <see cref="Polyline"/>) from a corner point, width, height and rotation
    /// angle: the user picks the start point and enters the width, the height and the angle of the width direction, each
    /// of which can also be picked with the mouse. The rectangle lies in the active drawing plane.
    /// Menu id: "Constr.Rect.PointWidthHeightAngle".
    /// </summary>
    internal class ConstrRectPointWidthHeightAngle : ConstructAction
    {
        public ConstrRectPointWidthHeightAngle()
        { }

        private Polyline line;
        private GeoVectorInput ang;
        private LengthInput height;
        private LengthInput width;
        private GeoPointInput startPointInput;

        private void StartPoint(GeoPoint p)
        {
            //			line.RectangleLocation = p;
            line.SetRectangle(p, line.RectangleWidth * base.ActiveDrawingPlane.DirectionX, line.RectangleHeight * base.ActiveDrawingPlane.DirectionY);
        }

        private double WidthCalculate(GeoPoint MousePosition)
        {  // falls die Breite über einen Punkt im Raum über dem jetzigen Rechteck bestimmt wird:
           // der Lotfußpunkt von MousePosition auf die NebenAchse (y-Direction)
            GeoPoint p = Geometry.DropPL(MousePosition, line.RectangleLocation, line.ParallelogramSecondaryDirection);
            if (!Precision.IsEqual(MousePosition, p))
            {   // Neues Rechteck mit neuer Orientierung im Raum, x-Vektor: Lotfußpunkt, Mausposition
                line.SetRectangle(line.RectangleLocation, new GeoVector(p, MousePosition), line.ParallelogramSecondaryDirection);
                // nun die Breite zurückliefern
                return line.RectangleWidth;
            }
            return 0;
        }

        private bool Width(double length)
        {
            if (length > Precision.eps)
            {
                line.RectangleWidth = length;
                return true;
            }
            return false;
        }

        private double HeightCalculate(GeoPoint MousePosition)
        {   // falls die Höhe über einen Punkt im Raum über dem jetzigen Rechteck bestimmt wird:
            // der Lotfußpunkt von MousePosition auf die HauptAchse (x-Direction)
            GeoPoint p = Geometry.DropPL(MousePosition, line.RectangleLocation, line.ParallelogramMainDirection);
            if (!Precision.IsEqual(MousePosition, p))
            {   // Neues Rechteck mit neuer Orientierung im Raum, y-Vektor: Lotfußpunkt, Mausposition
                line.SetRectangle(line.RectangleLocation, line.ParallelogramMainDirection, new GeoVector(p, MousePosition));
                // nun die Höhe zurückliefern
                return line.RectangleHeight;
            }
            return 0;
        }

        private bool Height(double length)
        {
            if (length > Precision.eps)
            {
                line.RectangleHeight = length;
                return true;
            }
            return false;
        }
        private bool RectAngle(GeoVector vector)
        {   // derWinkel als x-Achsen Vektor
            if (!vector.IsNullVector())
            {
                vector.Norm();
                //if (ActiveDrawingPlane.Normal != vector) // Spezialfall, ausschliessen, sonst krachts
                //    line.SetRectangle(line.RectangleLocation,line.RectangleWidth*vector,line.RectangleHeight*(ActiveDrawingPlane.Normal^vector));
                GeoVector2D v1 = base.ActiveDrawingPlane.Project(vector);
                GeoVector v2 = base.ActiveDrawingPlane.ToGlobal(v1);
                if (!v2.IsNullVector())
                {
                    v2.Norm();
                    if (ActiveDrawingPlane.Normal != v2) // Spezialfall, ausschliessen, sonst krachts
                        line.SetRectangle(line.RectangleLocation, line.RectangleWidth * v2, line.RectangleHeight * (ActiveDrawingPlane.Normal ^ v2));
                }
                return true;
            }
            return false;
        }

        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            if (CurrentInput == width && startPointInput.Fixed)
            {
                GeoObjectList l = base.GetObjectsUnderCursor(e.Location);
                l.DecomposeAll();
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] is ICurve)
                    {
                        double[] tanpos = (l[i] as ICurve).TangentPosition(line.ParallelogramSecondaryDirection);
                        if (tanpos != null)
                        {
                            for (int j = 0; j < tanpos.Length; j++)
                            {
                                GeoPoint p = (l[i] as ICurve).PointAt(tanpos[j]);
                                double d = base.WorldPoint(e.Location) | p;
                                if (d < mindist)
                                {
                                    mindist = d;
                                    found = p;
                                }
                            }
                        }
                    }
                }
            }
            if (CurrentInput == height && startPointInput.Fixed)
            {
                GeoObjectList l = base.GetObjectsUnderCursor(e.Location);
                l.DecomposeBlocks(true);
                l.DecomposeBlockRefs();
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] is ICurve)
                    {
                        double[] tanpos = (l[i] as ICurve).TangentPosition(line.StartDirection);
                        if (tanpos != null)
                        {
                            for (int j = 0; j < tanpos.Length; j++)
                            {
                                GeoPoint p = (l[i] as ICurve).PointAt(tanpos[j]);
                                double d = base.WorldPoint(e.Location) | p;
                                if (d < mindist)
                                {
                                    mindist = d;
                                    found = p;
                                }
                            }
                        }
                    }
                }
            }
            return mindist != double.MaxValue;
        }


        public override void OnSetAction()
        {
            line = Polyline.Construct();
            line.SetRectangle(ConstrDefaults.DefaultStartPoint, new GeoVector(ConstrDefaults.DefaultRectWidth, 0.0, 0.0), new GeoVector(0.0, ConstrDefaults.DefaultRectHeight, 0.0));
            base.BasePoint = ConstrDefaults.DefaultStartPoint;
            base.ActiveObject = line;
            base.TitleId = "Constr.Rect.PointWidthHeightAngle";

            startPointInput = new GeoPointInput("Rect.StartPoint");
            startPointInput.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;
            startPointInput.DefinesBasePoint = true;
            startPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(StartPoint);

            width = new LengthInput("Rect.Width");
            width.DefaultLength = ConstrDefaults.DefaultRectWidth;
            width.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(Width);
            width.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(WidthCalculate);
            width.ForwardMouseInputTo = startPointInput;

            height = new LengthInput("Rect.Height");
            height.DefaultLength = ConstrDefaults.DefaultRectHeight;
            height.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(Height);
            height.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(HeightCalculate);
            height.ForwardMouseInputTo = startPointInput;

            ang = new GeoVectorInput("Rect.Angle");
            ang.IsAngle = true;
            ang.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(RectAngle);
            ang.ForwardMouseInputTo = startPointInput;
            base.SetInput(startPointInput, width, height, ang);
            base.ShowAttributes = true;
            base.OnSetAction();
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Rect.PointWidthHeightAngle";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultStartPoint.Point = line.RectangleLocation + line.ParallelogramMainDirection + line.ParallelogramSecondaryDirection;
            // wird auf den Diagonalpunkt gesetzt
            base.OnDone();
        }
    }
}

