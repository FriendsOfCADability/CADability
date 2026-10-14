using CADability.GeoObject;
using CADability.UserInterface;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a linear dimension between several points. The user picks any number of points (at least two) and
    /// the location of the dimension line; the dimension line is horizontal, vertical or, for the sloping variant, in a
    /// direction given by an additional input, according to the <see cref="DimDirection"/> passed to the constructor.
    /// The optional "mode" input selects simple, continued (chain) or coordinate dimensioning. Menu ids:
    /// "Constr.Dimension.Horizontal", "Constr.Dimension.Vertical" and "Constr.Dimension.Sloping".
    /// </summary>
    internal class ConstrDimensionPoints : ConstructAction, IIndexedGeoPoint
    {
        private Dimension dim;
        private MultiPointInput mi;
        private GeoPointInput dimLocationInput;
        private GeoVectorInput dimDirInput;
        private MultipleChoiceInput dimMethod;
        private int methodSelect;
        private DimDirection dimDir;
        private string idString;
        public enum DimDirection { Horizontal, Vertical, Sloping }

        public ConstrDimensionPoints(DimDirection dimDir)
        {
            this.dimDir = dimDir; // switch für die drei Bemaßungslagen Horizontal, Vertikal, schräg
        }
        public ConstrDimensionPoints(ConstrDimensionPoints autorepeat)
        {	// diese Konstruktor kommt bei autorepeat dran, wenn es keinen leeren Konstruktor gibt
            this.dimDir = autorepeat.dimDir;
        }

        public GeoPoint LocationPointOffset()
        {	// die dim.plane hat als x-achse die Massgrundlinie!
            GeoPoint2D p = dim.Plane.Project(dim.DimLineRef);
            if (dim.Plane.Project(dim.GetPoint(0)).y > 0) // der 1. Bem.Punkt geht nach oben
                p.y = p.y - dim.DimensionStyle.LineIncrement; // Parameter: "Masslinien-Abstand"
            else p.y = p.y + dim.DimensionStyle.LineIncrement;
            return (dim.Plane.ToGlobal(p));
        }


        #region IIndexedGeoPoint Members
        // Interface für MultiGeoPointInput mi 

        public void SetGeoPoint(int Index, GeoPoint ThePoint)
        {
            if (dimDir == DimDirection.Sloping) //Schrägbemassung 
            {
                if (Index == 0) base.BasePoint = ThePoint; // Referenzpunkt für die Richtungsbestimmung
                if ((Index == 1) & (!dimDirInput.Fixed)) // nur, falls nicht explizit die Richtung bestimmt wurde
                {	// Schrägbemassung: der zweite Punkt definiert mit dem 1. die Lage
                    dim.DimLineDirection = new GeoVector(dim.GetPoint(0), ThePoint);
                }
            }
            // das folgende dient nur dazu, den Cursor umzuschalten, falls Bemassung getroffen ist
            if (dim.PointCount == 2)
            {
                GeoObjectList li = base.GetObjectsUnderCursor(base.CurrentMousePoint);
                for (int i = 0; i < li.Count; ++i)
                {
                    if (li[i] is Dimension)
                    {
                        Dimension dimTemp = (li[i] as Dimension);
                        int ind;
                        Dimension.HitPosition hi = dimTemp.GetHitPosition(base.Frame.ActiveView.Projection, base.ProjectedPoint(CurrentMousePoint), out ind);
                        if ((hi & Dimension.HitPosition.DimLine) != 0)
                            base.Frame.ActiveView.SetCursor(CursorTable.GetCursor("Trim.cur"));
                        //TODO: Bemassungscursor!
                    }
                }
            }
            dim.SetPoint(Index, ThePoint);

        }

        public GeoPoint GetGeoPoint(int Index)
        {
            return dim.GetPoint(Index);
        }

        public void InsertGeoPoint(int Index, GeoPoint ThePoint)
        {
            if (dim.PointCount >= 2)
            {	// nach dem 2.Punkt ist keine Änderung der Methode mehr möglich!
                dimMethod.ReadOnly = true;
                // Testen, ob eine Bemassung getroffen wurde
                GeoObjectList li = base.GetObjectsUnderCursor(base.CurrentMousePoint);
                if (Settings.GlobalSettings.GetBoolValue("Dimension.AutoExtend", true)) // eingeführt am 30.08.2016
                {
                    for (int i = 0; i < li.Count; ++i)
                    {
                        if (li[i] is Dimension)
                        { // die getroffene Bem. nutzen
                            Dimension dimTemp = (li[i] as Dimension);
                            // nur wenn die Typen stimmen!
                            if ((dimTemp.DimType == Dimension.EDimType.DimPoints) | (dimTemp.DimType == Dimension.EDimType.DimCoord))
                            {
                                int ind; // wo getroffen?
                                Dimension.HitPosition hi = dimTemp.GetHitPosition(base.Frame.ActiveView.Projection, base.ProjectedPoint(CurrentMousePoint), out ind);
                                if ((hi & Dimension.HitPosition.DimLine) != 0)
                                { // jetzt also: neuen Punkt einfügen, der kommt aus dim.
                                    dimTemp.AddPoint(dim.GetPoint(0));
                                    base.ActiveObject = null;
                                    base.OnDone();
                                    dimTemp.SortPoints();
                                    dimTemp.Recalc();
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            if ((dim.PointCount == 2) & (methodSelect == 0))
            {	// ZweiPunktBemassung: Jetzt ist Schluss!
                mi.Fixed = true;
                if (!dimLocationInput.Fixed)
                {
                    CADability.GeoObject.Point gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame, PointSymbol.Cross);
                    gPoint1.Location = ThePoint;
                    base.FeedBack.Add(gPoint1);
                    base.SetFocus(dimLocationInput, true);
                }
                else base.OnDone();
                return;
            }
            dim.AddPoint(ThePoint);

            CADability.GeoObject.Point gPoint = ActionFeedBack.FeedbackPoint(base.Frame, PointSymbol.Cross);
            gPoint.Location = ThePoint;
            base.FeedBack.Add(gPoint);
        }

        public void RemoveGeoPoint(int Index)
        { // kommt auch drann, falls der Nutzer ins ControlCenter geht!
            if (Index == -1) Index = dim.PointCount - 1; // Punkt am Ende: weg damit
            dim.RemovePoint(Index);
        }

        public int GetGeoPointCount()
        {
            return dim.PointCount;
        }

        bool IIndexedGeoPoint.MayInsert(int Index)
        {
            return true; // neue Punkte entstehen ja automatisch, wozu einfügen?
            //return dim.PointCount>0 && (Index==-1 || Index<dim.PointCount);
        }
        bool IIndexedGeoPoint.MayDelete(int Index)
        {
            return false;
            // return Index<dim.PointCount && Index>=0;
        }
        #endregion

        private void SetDimLocation(GeoPoint p)
        {	// der Lagepunkt der Bemassung
            if (((methodSelect == 1) || (methodSelect == 2)) & (dim.PointCount >= 2)) mi.Fixed = true; // Abbruchkriterium für MultiPoint
            if (dim.PointCount == 1)
            {
                if (!Precision.IsEqual(p, dim.GetPoint(0))) dim.AddPoint(p); // falls erst ein punkt da ist: einen machen zur Darstellung!
            }
            dim.DimLineRef = p;
        }

        private GeoPoint GetDimLocation()
        {	// der Lagepunkt der Bemassung
            return dim.DimLineRef;
        }

        private bool DimDirectionInput(GeoVector vector)
        { // die Richtung der Schrägbemassung
            // falls erst ein punkt da ist: einen machen zur Darstellung!
            if (dim.PointCount == 1) dim.AddPoint(dim.GetPoint(0) + vector);
            dim.DimLineDirection = vector;
            return false;
        }

        private void SetMethod(int val)
        {
            methodSelect = val;
            if (methodSelect == 2) // Kordinatenbem.= Kettenbem. bezgl eines Punktes
                dim.DimType = Dimension.EDimType.DimCoord;
            else dim.DimType = Dimension.EDimType.DimPoints;
        }

        public override void OnSetAction()
        {

            dim = Dimension.Construct();
            switch (dimDir)
            {
                case DimDirection.Horizontal:
                    idString = "Constr.Dimension.Horizontal";
                    dim.DimLineDirection = base.ActiveDrawingPlane.DirectionX;
                    break;
                case DimDirection.Vertical:
                    idString = "Constr.Dimension.Vertical";
                    dim.DimLineDirection = base.ActiveDrawingPlane.DirectionY;
                    break;
                case DimDirection.Sloping:
                    idString = "Constr.Dimension.Sloping";
                    dim.DimLineDirection = base.ActiveDrawingPlane.DirectionX;
                    break;
            }
            dim.DimLineRef = ConstrDefaults.DefaultDimPoint;
            dim.Normal = base.ActiveDrawingPlane.Normal;
            methodSelect = ConstrDefaults.DefaultDimensionMethod;
            if (methodSelect == 2) // Kordinatenbem. bezgl eines Punktes
                dim.DimType = Dimension.EDimType.DimCoord;
            else dim.DimType = Dimension.EDimType.DimPoints;


            base.ActiveObject = dim;

            base.TitleId = idString;

            mi = new MultiPointInput(this);
            mi.ResourceId = "Constr.Dimension.Point";

            dimLocationInput = new GeoPointInput("Constr.Dimension.Location");
            dimLocationInput.DefaultGeoPoint = ConstrDefaults.DefaultDimPoint;
            dimLocationInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimLocation);
            dimLocationInput.GetGeoPointEvent += new ConstructAction.GeoPointInput.GetGeoPointDelegate(GetDimLocation);

            dimDirInput = new GeoVectorInput("Constr.Dimension.Direction");
            dimDirInput.Optional = true;
            dimDirInput.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(DimDirectionInput);
            dimDirInput.IsAngle = true;

            dimMethod = new MultipleChoiceInput("Constr.Dimension.Points.Method", "Constr.Dimension.Points.Method.Values");
            dimMethod.Optional = true;
            dimMethod.DefaultChoice = ConstrDefaults.DefaultDimensionMethod;
            dimMethod.SetChoiceEvent += new CADability.Actions.ConstructAction.MultipleChoiceInput.SetChoiceDelegate(SetMethod);
            if (dimDir == DimDirection.Sloping)
                base.SetInput(mi, dimLocationInput, dimDirInput, dimMethod);
            else base.SetInput(mi, dimLocationInput, dimMethod);
            base.ShowAttributes = true;
            base.OnSetAction();

        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return idString;
        }

        public override void OnDone()
        {
            if (dim.PointCount > 0) ConstrDefaults.DefaultDimPoint.Point = LocationPointOffset();
            if (dim.PointCount < 2) base.ActiveObject = null;
            base.OnDone();
            if (dim.PointCount > 1)
            {
                dim.SortPoints();
                dim.Recalc();
            }
        }

    }
}

