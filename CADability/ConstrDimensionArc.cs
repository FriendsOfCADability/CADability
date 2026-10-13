using CADability.GeoObject;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a radius or a diameter dimension (<see cref="Dimension.EDimType.DimRadius"/> or
    /// <see cref="Dimension.EDimType.DimDiameter"/>) of a circle or circular arc; the constructor argument
    /// selects which one. The user picks the circle (other curves under the cursor are ignored) and then the
    /// position of the dimension line, which is projected into the plane of the circle. <see cref="GetID"/>
    /// returns "Constr.Dimension.Arc.Radius" or "Constr.Dimension.Arc.Diameter" accordingly (menu commands
    /// "MenuId.Constr.Radius.Dimension" and "MenuId.Constr.Diameter.Dimension").
    /// </summary>
    internal class ConstrDimensionArc : ConstructAction
    {

        private Dimension dim;
        private string idString;
        private DimArcType dimArcType;
        private Ellipse elli;

        public enum DimArcType { Radius, Diameter }

        public ConstrDimensionArc(DimArcType dimArcType)
        {
            this.dimArcType = dimArcType; // switch für die zwei Bemaßungsarten Radius, Durchmesser
        }

        public ConstrDimensionArc(ConstrDimensionArc autorepeat)
        {   // diese Konstruktor kommt bei autorepeat dran, wenn es keinen leeren Konstruktor gibt
            this.dimArcType = autorepeat.dimArcType;
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
        {
            bool ok = false;
            if (Curves.Length > 0)
            {   // zunächst: Nur alle Kreise ausfiltern
                ArrayList usableCurves = new ArrayList();
                for (int i = 0; i < Curves.Length; ++i)
                {
                    if (Curves[i] is Ellipse)
                    {
                        if ((Curves[i] as Ellipse).IsCircle)
                            usableCurves.Add(Curves[i]); // zur lokalen Liste zufügen
                    }

                }
                Curves = (ICurve[])usableCurves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                if (Curves.Length > 0)
                {
                    elli = Curves[0] as Ellipse;
                    dim.SetPoint(0, elli.Center);
                    dim.Radius = elli.Radius;
                    dim.DimLineRef = elli.Plane.ToGlobal(elli.Plane.Project(base.CurrentMousePosition));
                    base.ShowActiveObject = true;
                    ok = true;
                }
                else base.ShowActiveObject = false;
            }
            /*
                        else
                        {	// es gibt einen Kreis und damit eine Bemassung, ausserhalb des Fanbereichs des CurveInput nur die Lage ändern
                            if (elli != null)
                            {
                                dim.DimLineRef = base.CurrentMousePosition;
                                ok = true;
                            }

                        }
            */
            if (up)
            {
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            }
            return ok;
        }

        private void inputDimCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {   // kommt nie dran, da nur eine Aktion in base.SetInput angemeldet! Funkioniert aber!
            elli = SelectedCurve as Ellipse;
            dim.SetPoint(0, elli.Center);
            dim.Radius = elli.Radius;
            dim.DimLineRef = elli.Plane.ToGlobal(elli.Plane.Project(base.CurrentMousePosition));
        }

        private void SetDimLocation(GeoPoint p)
        {	// der Lagepunkt der Bemassung
            if (elli != null)
            {
                dim.DimLineRef = elli.Plane.ToGlobal(elli.Plane.Project(base.CurrentMousePosition));
            }
            else
            {
                dim.DimLineRef = base.CurrentMousePosition;
            }
        }

        public override void OnSetAction()
        {
            dim = Dimension.Construct();
            if (dimArcType == DimArcType.Radius)
            {
                idString = "Constr.Dimension.Arc.Radius";
                dim.DimType = Dimension.EDimType.DimRadius;
            }
            else
            {
                idString = "Constr.Dimension.Arc.Diameter";
                dim.DimType = Dimension.EDimType.DimDiameter;
            }
            dim.DimLineRef = ConstrDefaults.DefaultDimPoint;
            dim.Normal = base.ActiveDrawingPlane.Normal;
            dim.AddPoint(new GeoPoint(0.0, 0.0, 0.0));

            base.TitleId = idString;
            base.ActiveObject = dim;

            CurveInput curveInput = new CurveInput("Constr.Dimension.Arc.Object");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(inputDimCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(inputDimCurvesChanged);

            GeoPointInput dimLocationInput = new GeoPointInput("Constr.Dimension.Location");
            dimLocationInput.DefaultGeoPoint = ConstrDefaults.DefaultDimPoint;
            dimLocationInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimLocation);


            base.SetInput(curveInput, dimLocationInput);
            base.ShowAttributes = true;
            base.ShowActiveObject = false;
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
            ConstrDefaults.DefaultDimPoint.Point = LocationPointOffset();
            base.OnDone();
        }

    }
}



