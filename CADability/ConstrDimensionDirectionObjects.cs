using CADability.GeoObject;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ConstrDimensionDirectionObjects : ConstructAction
    {

        private Dimension dim;
        private GeoPointInput dimLocationInput;
        private CurveInput curveInput;
        private CurveInput curve2Input;
        private Ellipse elli;
        private Line line1;
        private Line line2;
        private GeoPoint objectPoint1;
        private GeoPoint objectPoint2;


        public ConstrDimensionDirectionObjects()
        {
        }

        public GeoPoint LocationPointOffset()
        {   // die dim.plane hat als x-achse die Massgrundlinie!
            GeoPoint2D p = dim.Plane.Project(dim.DimLineRef);
            if (dim.Plane.Project(dim.GetPoint(0)).y > 0) // der 1. Bem.Punkt geht nach oben
                p.y = p.y - dim.DimensionStyle.LineIncrement; // Parameter: "Masslinien-Abstand"
            else p.y = p.y + dim.DimensionStyle.LineIncrement;
            return (dim.Plane.ToGlobal(p));
        }

        private bool inputDimCurves(CurveInput sender, ICurve[] Curves, bool up)
        {   // voreinstellungen
            bool ok = false;
            base.ShowActiveObject = false;
            curve2Input.ReadOnly = false;
            curve2Input.Fixed = false;
            dimLocationInput.ReadOnly = false;
            dimLocationInput.Fixed = false;
            objectPoint1 = base.CurrentMousePosition;

            if (Curves.Length > 0)
            {   // zunächst: Nur alle Kreise und Linien ausfiltern
                ArrayList usableCurves = new ArrayList();
                for (int i = 0; i < Curves.Length; ++i)
                {
                    if (Curves[i] is Ellipse)
                    {
                        if ((Curves[i] as Ellipse).IsArc & (Curves[i] as Ellipse).IsCircle)
                            usableCurves.Add(Curves[i]); // Kreisbögen zur lokalen Liste zufügen
                    }
                    if (Curves[i] is Line)
                    {
                        usableCurves.Add(Curves[i]); // Linien zur lokalen Liste zufügen
                    }
                }
                Curves = (ICurve[])usableCurves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                if (Curves.Length > 0)
                {
                    dim.DimLineRef = base.CurrentMousePosition;
                    if ((Curves[0] is Ellipse) & !(Curves[0].Owner is Dimension))
                    {
                        elli = Curves[0] as Ellipse;
                        dim.SetPoint(0, elli.Center); // der Mittelpunkt der Winkelbemassung
                        if (elli.CounterClockWise)
                        {   // die Winkelpunkte
                            dim.SetPoint(1, elli.StartPoint);
                            dim.SetPoint(2, elli.EndPoint);
                        }
                        else
                        {
                            dim.SetPoint(2, elli.StartPoint);
                            dim.SetPoint(1, elli.EndPoint);
                        }
                        dim.DimLineRef = base.CurrentMousePosition;
                        base.ShowActiveObject = true;
                        // die anderen Inputs abschalten
                        curve2Input.Fixed = true;
                        curve2Input.ReadOnly = true;
                        dimLocationInput.ReadOnly = true;
                        dimLocationInput.Fixed = true;
                        ok = true;
                    }
                    else
                    {   // merken in line1 und raus
                        if (Curves[0] is Line)
                        {
                            line1 = Curves[0] as Line;
                            ok = true;
                        }
                        else ok = false;
                    }
                }
            }
            else
            {   // es gibt einen Bogen und damit eine Bemassung, ausserhalb des Fangbereichs des CurveInput nur die Lage ändern
                if (elli != null)
                {
                    dim.DimLineRef = base.CurrentMousePosition;
                    base.ShowActiveObject = true;
                    // die anderen Inputs abschalten
                    curve2Input.Fixed = true;
                    curve2Input.ReadOnly = true;
                    dimLocationInput.ReadOnly = true;
                    dimLocationInput.Fixed = true;
                    ok = true;
                }
            }
            if (up)
            {
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            }
            return ok;
        }

        private void inputDimCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            if (SelectedCurve is Ellipse)
            {
                elli = SelectedCurve as Ellipse;
                dim.SetPoint(0, elli.Center); // der Mittelpunkt der Winkelbemassung
                if (elli.CounterClockWise)
                {   // die Winkelpunkte
                    dim.SetPoint(1, elli.StartPoint);
                    dim.SetPoint(2, elli.EndPoint);
                }
                else
                {
                    dim.SetPoint(2, elli.StartPoint);
                    dim.SetPoint(1, elli.EndPoint);
                }
                //				dim.DimLineRef = base.CurrentMousePosition;
                base.ShowActiveObject = true;
                // die anderen Inputs abschalten
                curve2Input.Fixed = true;
                curve2Input.ReadOnly = true;
                dimLocationInput.ReadOnly = true;
            }
            else
            {   // merken in line1 und raus
                line1 = SelectedCurve as Line;
            }
        }

        private bool input2DimCurves(CurveInput sender, ICurve[] dCurves, bool up)
        {   // kommt nur drann, wenn schon eine Linie angeklickt wurde
            bool ok = false;
            objectPoint2 = base.CurrentMousePosition;
            base.ShowActiveObject = false;
            if (dCurves.Length > 0)
            {   // zunächst: Nur alle Linien ausfiltern
                ArrayList usableCurves = new ArrayList();
                for (int i = 0; i < dCurves.Length; ++i)
                {
                    if (dCurves[i] is Line)
                    {
                        usableCurves.Add(dCurves[i]); // zur lokalen Liste zufügen
                    }

                }
                dCurves = (ICurve[])usableCurves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                if (dCurves.Length > 0)
                {   // also: eine linie da!
                    Plane pl;
                    line2 = dCurves[0] as Line;
                    if (Curves.GetCommonPlane(line1, line2, out pl))
                    {   // Schnittpunkt der Linien
                        GeoPoint2D dimC2D;
                        if (Geometry.IntersectLL(pl.Project(line1.StartPoint), pl.Project(line1.EndPoint), pl.Project(line2.StartPoint), pl.Project(line2.EndPoint), out dimC2D))
                        {
                            GeoPoint dimC = pl.ToGlobal(dimC2D);
                            dim.SetPoint(0, dimC);
                            // wegen der Optik: Hilfslinien konstruieren
                            Line lineT = Line.Construct();
                            lineT.SetTwoPoints(dimC, line1.PointAt(line1.PositionOf(objectPoint1)));
                            //							lineT.SetTwoPoints(dimC,Geometry.DropPL(base.CurrentMousePosition,line1.StartPoint,line1.EndPoint));
                            // wegen der Optik: Hilfspunkt nahe Mittelpunkt konstruieren
                            dim.SetPoint(1, lineT.PointAt(0.01));
                            lineT.SetTwoPoints(dimC, line2.PointAt(line2.PositionOf(objectPoint2)));
                            //							lineT.SetTwoPoints(dimC,Geometry.DropPL(base.CurrentMousePosition,line2.StartPoint,line2.EndPoint));
                            dim.SetPoint(2, lineT.PointAt(0.01));
                            dim.DimLineRef = base.CurrentMousePosition;
                            base.ShowActiveObject = true;
                            ok = true;
                        }
                    }
                }
            }
            if (up)
            {
                if (dCurves.Length == 0) sender.SetCurves(dCurves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(dCurves, dCurves[0]);
            }
            return ok;
        }

        private void input2DimCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            Plane pl;
            line2 = SelectedCurve as Line;
            if (Curves.GetCommonPlane(line1, line2, out pl))
            {   // Schnittpunkt der Linien
                GeoPoint2D dimC2D;
                if (Geometry.IntersectLL(pl.Project(line1.StartPoint), pl.Project(line1.EndPoint), pl.Project(line2.StartPoint), pl.Project(line2.EndPoint), out dimC2D))
                {
                    GeoPoint dimC = pl.ToGlobal(dimC2D);
                    dim.SetPoint(0, dimC);
                    // wegen der Optik: Hilfslinien konstruieren
                    Line lineT = Line.Construct();
                    //					lineT.SetTwoPoints(dimC,line1.StartPoint);
                    lineT.SetTwoPoints(dimC, line1.PointAt(line1.PositionOf(objectPoint1)));
                    // wegen der Optik: Hilfspunkt nahe Mittelpunkt konstruieren
                    dim.SetPoint(1, lineT.PointAt(0.01));
                    //					lineT.SetTwoPoints(dimC,line2.StartPoint);
                    lineT.SetTwoPoints(dimC, line2.PointAt(line2.PositionOf(objectPoint2)));
                    dim.SetPoint(2, lineT.PointAt(0.01));
                    dim.DimLineRef = base.CurrentMousePosition;
                    base.ShowActiveObject = true;
                }
            }
        }



        private void SetDimLocation(GeoPoint p)
        {   // der Lagepunkt der Bemassung
            dim.DimLineRef = p;
            // kommt am Anfang wohl dran, deshalb diese Abfrage:
            if (curveInput.Fixed & curve2Input.Fixed)
            {
                Plane pl;
                if (Curves.GetCommonPlane(line1, line2, out pl))
                {
                    GeoVector v1, v2;
                    if (line1.PositionOf(objectPoint1) < line1.PositionOf(dim.GetPoint(0))) // der Pickpunkt entscheidet
                        v1 = new GeoVector(dim.GetPoint(0), line1.StartPoint); // Mittelpunkt-Linie1
                    else v1 = new GeoVector(dim.GetPoint(0), line1.EndPoint); // Mittelpunkt-Linie1
                    if (!v1.IsNullVector()) v1.Norm();
                    if (line2.PositionOf(objectPoint2) < line2.PositionOf(dim.GetPoint(0)))  // der Pickpunkt entscheidet
                        v2 = new GeoVector(dim.GetPoint(0), line2.StartPoint); // Mittelpunkt-Linie2
                    else v2 = new GeoVector(dim.GetPoint(0), line2.EndPoint); // Mittelpunkt-Linie2
                    if (!v2.IsNullVector()) v2.Norm();
                    // umschaltung auf die inverse Bemaßung, abhängig vom Lagepunkt p
                    int ind1 = 1; // Umschaltindices für dimPoint (s.u.)
                    int ind2 = 2;
                    Ellipse arc = Ellipse.Construct(); // Kreisbogen der Bemassung nachbilden
                    arc.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane, pl.Project(dim.GetPoint(0)), pl.Project(dim.GetPoint(0) + Geometry.Dist(dim.GetPoint(0), p) * v1), pl.Project(dim.GetPoint(0) + Geometry.Dist(dim.GetPoint(0), p) * v2), pl, true);
                    if ((arc.PositionOf(p) > 1) || (arc.PositionOf(p) < 0)) // p ausserhalb, also drehen
                    {
                        ind1 = 2;
                        ind2 = 1;
                    }
                    // nun die Berechnung so, dass die Hilfslinien nicht auf der Linie laufen, sondern nur ausserhalb
                    GeoPoint2D[] cutPoints; // Schnittpunkt Linie1 und Kreis um Mittelpunkt durch p
                    cutPoints = Geometry.IntersectLC(pl.Project(dim.GetPoint(0)), pl.Project(dim.GetPoint(0) + v1), pl.Project(dim.GetPoint(0)), Geometry.Dist(dim.GetPoint(0), p));
                    GeoPoint pLoc = GeoPoint.Origin;
                    if (cutPoints.Length == 1)
                        pLoc = pl.ToGlobal(cutPoints[0]);
                    else if (cutPoints.Length > 1)
                    { // den nächsten zum Pickpunkt nehmen
                        if (Geometry.Dist(pl.ToGlobal(cutPoints[0]), objectPoint1) < Geometry.Dist(pl.ToGlobal(cutPoints[1]), objectPoint1))
                            pLoc = pl.ToGlobal(cutPoints[0]);
                        else pLoc = pl.ToGlobal(cutPoints[1]);
                    }
                    double distLoc = line1.PositionOf(pLoc); // die Position auf der Linie
                    if (distLoc < 0)
                        dim.SetPoint(ind1, line1.StartPoint);
                    else
                    {
                        if (distLoc > 1)
                            dim.SetPoint(ind1, line1.EndPoint);
                        else dim.SetPoint(ind1, pLoc); // keine Hilfslinie innerhalb der Linie
                    }
                    // Schnittpunkt Linie2 und Kreis um Mittelpunkt durch p
                    cutPoints = Geometry.IntersectLC(pl.Project(dim.GetPoint(0)), pl.Project(dim.GetPoint(0) + v2), pl.Project(dim.GetPoint(0)), Geometry.Dist(dim.GetPoint(0), p));
                    if (cutPoints.Length == 1)
                        pLoc = pl.ToGlobal(cutPoints[0]);
                    else
                    {// den nächsten zum Pickpunkt nehmen
                        if (Geometry.Dist(pl.ToGlobal(cutPoints[0]), objectPoint2) < Geometry.Dist(pl.ToGlobal(cutPoints[1]), objectPoint2))
                            pLoc = pl.ToGlobal(cutPoints[0]);
                        else pLoc = pl.ToGlobal(cutPoints[1]);
                    }
                    distLoc = line2.PositionOf(pLoc); // die Position auf der Linie
                    if (distLoc < 0)
                        dim.SetPoint(ind2, line2.StartPoint);
                    else
                    {
                        if (distLoc > 1)
                            dim.SetPoint(ind2, line2.EndPoint);
                        else dim.SetPoint(ind2, pLoc); // keine Hilfslinie innerhalb der Linie
                    }

                }
            }
        }


        public override void OnSetAction()
        {
            dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimAngle;
            dim.DimLineRef = ConstrDefaults.DefaultDimPoint;
            dim.Normal = base.ActiveDrawingPlane.Normal;
            dim.AddPoint(new GeoPoint(0.0, 0.0, 0.0));
            dim.AddPoint(new GeoPoint(0.0, 0.0, 0.0));
            dim.AddPoint(new GeoPoint(0.0, 0.0, 0.0));
            base.ActiveObject = dim;
            base.ShowActiveObject = false;


            base.TitleId = "Constr.Dimension.Direction.Objects";

            curveInput = new CurveInput("Constr.Dimension.Direction.Object1");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(inputDimCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(inputDimCurvesChanged);

            curve2Input = new CurveInput("Constr.Dimension.Direction.Object2");
            curve2Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve2Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(input2DimCurves);
            curve2Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(input2DimCurvesChanged);

            dimLocationInput = new GeoPointInput("Constr.Dimension.Location");
            dimLocationInput.DefaultGeoPoint = ConstrDefaults.DefaultDimPoint;
            dimLocationInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimLocation);

            base.SetInput(curveInput, curve2Input, dimLocationInput);
            base.ShowAttributes = true;
            base.OnSetAction();
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Dimension.Direction.Objects";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultDimPoint.Point = LocationPointOffset();
            base.OnDone();
        }
    }
}




