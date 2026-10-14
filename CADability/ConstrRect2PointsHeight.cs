using CADability.Curve2D;
using CADability.GeoObject;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a rectangle (a closed <see cref="Polyline"/>) from the two end points of its base line
    /// and its height. The user enters the start point and the second point, which give the direction and the
    /// width of the base line, and then the height, measured perpendicular to the base line; a height entered
    /// by a mouse position also tilts the rectangle towards that position in space. The diagonal corner
    /// becomes the default start point of the next construction. Started with the action id
    /// "Constr.Rect.Rect2PointsHeight" (menu command "MenuId.Constr.Rect.Rect2PointsHeight").
    /// </summary>
    internal class ConstrRect2PointsHeight : ConstructAction
    {
        public ConstrRect2PointsHeight()
        { }

        private Polyline line;
        private GeoPointInput secondPointInput;
        private GeoPointInput startPointInput;
        private LengthInput height;

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
        {
            if (!Precision.IsEqual(line.RectangleLocation, p))
            {
                GeoVector v = line.ParallelogramSecondaryDirection;
                if (!Precision.SameDirection(new GeoVector(line.RectangleLocation, p), v, false))
                    line.SetRectangle(line.RectangleLocation, new GeoVector(line.RectangleLocation, p), v);
            }
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


        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            if (CurrentInput == secondPointInput && startPointInput.Fixed)
            {
                GeoObjectList l = base.GetObjectsUnderCursor(e.Location);
                l.DecomposeAll();
                GeoPoint inpoint = base.WorldPoint(e.Location);
                Plane pln = vw.Projection.DrawingPlane;
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] is ICurve)
                    {
                        ICurve curve = l[i] as ICurve;
                        if (curve.IsInPlane(pln))
                        {
                            ICurve2D c2d = curve.GetProjectedCurve(pln);
                            GeoPoint2D c12d = pln.Project(line.RectangleLocation);
                            GeoPoint2D[] pp = c2d.PerpendicularFoot(c12d);
                            for (int j = 0; j < pp.Length; j++)
                            {
                                GeoPoint p = pln.ToGlobal(pp[j]);
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
            if (CurrentInput == height && startPointInput.Fixed && secondPointInput.Fixed)
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

            base.ActiveObject = line;
            base.TitleId = "Constr.Rect.Rect2PointsHeight";

            startPointInput = new GeoPointInput("Rect.StartPoint");
            startPointInput.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;
            startPointInput.DefinesBasePoint = true;
            startPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(StartPoint);

            secondPointInput = new GeoPointInput("Rect.SecondPoint");
            secondPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SecondPoint);

            height = new LengthInput("Rect.Height");
            height.DefaultLength = ConstrDefaults.DefaultRectHeight;
            height.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(Height);
            height.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(HeightCalculate);
            height.ForwardMouseInputTo = new object[] { startPointInput, secondPointInput };

            base.SetInput(startPointInput, secondPointInput, height);
            base.ShowAttributes = true;

            base.OnSetAction();
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Rect.Rect2PointsHeight";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultStartPoint.Point = line.RectangleLocation + line.ParallelogramMainDirection + line.ParallelogramSecondaryDirection;
            // wird auf den Diagonalpunkt gesetzt
            base.OnDone();
        }
    }
}


