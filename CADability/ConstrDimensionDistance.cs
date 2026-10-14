using CADability.Curve2D;
using CADability.GeoObject;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a distance dimension (<see cref="Dimension.EDimType.DimPoints"/>) between two curves.
    /// The user picks two objects that lie in a common plane; the dimension measures the minimum distance
    /// between them near the pick positions. If there is no object under the cursor, the mouse position
    /// itself is used as one end of the distance. The optional third input moves the dimension line.
    /// Started with the action id "Constr.Dimension.Distance" (menu command "MenuId.Constr.Distance.Dimension").
    /// </summary>
    internal class ConstrDimensionDistance : ConstructAction
    {

        private Dimension dim;
        private GeoPoint objectPoint;
        private ICurve dimCurve;
        private ICurve dim2Curve;
        private CurveInput curveInput;
        private CurveInput curve2Input;
        private GeoPointInput dimLocationInput;

        public ConstrDimensionDistance()
        { }

        private bool showDim()
        {
            if ((dimCurve == null) | (dim2Curve == null) | (dimCurve == dim2Curve)) return false;
            Plane pl;
            if (Curves.GetCommonPlane(dimCurve, dim2Curve, out pl))
            {
                ICurve2D l2D1 = dimCurve.GetProjectedCurve(pl);
                if (l2D1 is Path2D) (l2D1 as Path2D).Flatten();
                ICurve2D l2D2 = dim2Curve.GetProjectedCurve(pl);
                if (l2D2 is Path2D) (l2D2 as Path2D).Flatten();
                GeoPoint2D dim2DP1, dim2DP2, dimLoc;
                dimLoc = pl.Project(objectPoint);
                double dist = Curves2D.SimpleMinimumDistance(l2D1, l2D2, dimLoc, out dim2DP1, out dim2DP2);
                if (dist > 0)
                {   // eine gültige Bemassung kann gemacht werden
                    GeoPoint dimP1 = pl.ToGlobal(dim2DP1);
                    GeoPoint dimP2 = pl.ToGlobal(dim2DP2);
                    dim.DimLineDirection = new GeoVector(dimP1, dimP2);
                    if (!dimLocationInput.Fixed) dim.DimLineRef = new GeoPoint(dimP1, dimP2);
                    if (dim.PointCount == 0)
                    {
                        dim.AddPoint(dimP1);
                        dim.AddPoint(dimP2);
                    }
                    else
                    {
                        dim.SetPoint(0, dimP1);
                        dim.SetPoint(1, dimP2);
                    }
                    base.ShowActiveObject = true;
                    return (true);
                }
            }
            base.ShowActiveObject = false;
            return (false);
        }

        private bool inputDimCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint = base.CurrentMousePosition;
            if (up)
            {
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            }
            if (Curves.Length > 0)
            {
                dimCurve = Curves[0];
                if (curve2Input.Fixed) return showDim();
                return true;
            }
            else
            {
                if (curve2Input.Fixed)
                {   // hier wird hilfsweise ein kleinkreis erzeugt, um gültige Curves zu bestimmen
                    Ellipse circLoc;
                    circLoc = Ellipse.Construct();
                    circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, objectPoint, base.WorldLength(1));
                    dimCurve = (ICurve)circLoc;
                    return showDim();
                }
            }
            return false;
        }

        private void inputDimCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            dimCurve = SelectedCurve;
            if (curve2Input.Fixed) showDim();

        }
        private bool input2DimCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint = base.CurrentMousePosition;
            if (up)
            {
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            }
            if (Curves.Length > 0)
            {
                dim2Curve = Curves[0];
                if (curveInput.Fixed) return showDim();
                return true;
            }
            else
            {
                if (curveInput.Fixed)
                {   // hier wird hilfsweise ein kleinkreis erzeugt, um gültige Curves zu bestimmen
                    Ellipse circLoc;
                    circLoc = Ellipse.Construct();
                    circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, objectPoint, base.WorldLength(1));
                    dim2Curve = (ICurve)circLoc;
                    return showDim();
                }
            }
            return false;
        }

        private void input2DimCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            dim2Curve = SelectedCurve;
            if (curveInput.Fixed)
            { showDim(); }

        }

        private void SetDimLocation(GeoPoint p)
        {   // der Lagepunkt der Bemassung
            dim.DimLineRef = p;
        }

        private GeoPoint GetDimLocation()
        {   // der Lagepunkt der Bemassung
            return dim.DimLineRef;
        }


        public override void OnSetAction()
        {
            base.TitleId = "Constr.Dimension.Distance";
            dim = Dimension.Construct();
            dim.DimType = Dimension.EDimType.DimPoints;
            dim.Normal = base.ActiveDrawingPlane.Normal;
            base.ActiveObject = dim;
            base.ShowActiveObject = false;

            curveInput = new CurveInput("Constr.Dimension.Distance.Object1");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(inputDimCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(inputDimCurvesChanged);
            curve2Input = new CurveInput("Constr.Dimension.Distance.Object2");
            curve2Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve2Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(input2DimCurves);
            curve2Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(input2DimCurvesChanged);
            dimLocationInput = new GeoPointInput("Constr.Dimension.Location");
            dimLocationInput.Optional = true;
            dimLocationInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimLocation);
            dimLocationInput.GetGeoPointEvent += new ConstructAction.GeoPointInput.GetGeoPointDelegate(GetDimLocation);

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
            return "Constr.Dimension.Distance";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}

