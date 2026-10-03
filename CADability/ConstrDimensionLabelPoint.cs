using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif


namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ConstrDimensionLabelPoint : ConstructAction
    {

        private Dimension dim;
        private Dimension dimAddTo;
        private CurveInput curveInput;
        private GeoPointInput dimPointInput;
        private GeoPointInput dimLocationInput;
        private BooleanInput dimMethod;
        private StringInput dimStringInput;
        private string idString;
        private string pointString;
        private string objectString;
        private string locationString;
        private DimLabelType dimLabelType;
        private ICurve dimCurve;
        private GeoPoint dimPoint;
        private bool dimMethodPoint;


        public enum DimLabelType { Point, Labeling }

        public ConstrDimensionLabelPoint(DimLabelType dimLabelType)
        {
            this.dimLabelType = dimLabelType; // switch für die zwei Bemaßungsarten Radius, Durchmesser
        }

        public ConstrDimensionLabelPoint(ConstrDimensionLabelPoint autorepeat)
        {   // diese Konstruktor kommt bei autorepeat dran, wenn es keinen leeren Konstruktor gibt
            this.dimLabelType = autorepeat.dimLabelType;
        }


        private void PerpFoot()
        {
            if (dimCurve != null) //falls in CurveInput (s.u.) ein Objekt getroffen war
            {
                Plane pl;
                double mindist = double.MaxValue;
                GeoPoint2D footPoint = new GeoPoint2D(0.0, 0.0);
                if (Curves.GetCommonPlane(dim.DimLineRef, dimCurve, out pl))
                {
                    ICurve2D c2D = dimCurve.GetProjectedCurve(pl);
                    if (c2D is Path2D) (c2D as Path2D).Flatten();
                    GeoPoint2D[] perpPoints = c2D.PerpendicularFoot(pl.Project(dim.DimLineRef));
                    if (perpPoints.Length > 0)
                    {   // eine gültige Kurve ist gefunden
                        for (int j = 0; j < perpPoints.Length; ++j)
                        {
                            double dist = Geometry.Dist(perpPoints[j], pl.Project(dim.DimLineRef));
                            if (dist < mindist)
                            {
                                mindist = dist;
                                footPoint = perpPoints[j];
                            }
                        }
                    }
                }
                if (mindist < double.MaxValue)
                {   // also: Fußpunkt gefunden
                    dim.SetPoint(0, pl.ToGlobal(footPoint));
                }
            }
        }


        private bool inputDimCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            dimPoint = base.CurrentMousePosition;
            if (Curves.Length > 0)
            {
                dimCurve = Curves[0];
                base.Frame.ActiveView.SetCursor("Hand");
            }
            else
            {
                dimCurve = null;
                base.Frame.ActiveView.SetCursor("Arrow");
            }
            if (up)
            {
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            }
            dim.SetPoint(0, dimPoint);
            base.ShowActiveObject = true;
            if (!dimLocationInput.Fixed)
                dim.DimLineRef = new GeoPoint(dimPoint.x + base.WorldLength(15), dimPoint.y + base.WorldLength(15));
            else // Lagepunkt schon bestimmt, also Berechnung!
                PerpFoot();
            return true;
        }

        private void inputDimCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            dimCurve = SelectedCurve;
        }
        private void SetDimPoint(GeoPoint p)
        {   // der Punkt der Bemassung
            dimPoint = p;
            dim.SetPoint(0, dimPoint);
            base.ShowActiveObject = true;
            if (!dimLocationInput.Fixed)
                dim.DimLineRef = new GeoPoint(dimPoint.x + base.WorldLength(15), dimPoint.y + base.WorldLength(15));
        }

        private void SetDimLocation(GeoPoint p)
        {   // der Lagepunkt der Bemassung
            dim.DimLineRef = p;
            if ((dimMethodPoint & !curveInput.Fixed) | (!dimMethodPoint & !dimPointInput.Fixed))
                dim.SetPoint(0, new GeoPoint(p.x - base.WorldLength(15), p.y - base.WorldLength(15)));
            base.ShowActiveObject = true;
            // jetzt erfogt der Test, ob eine andere Bemassung vom Typ DimLocation getroffen wurde
            dimAddTo = null; // dient als merker für DimLocationOnMouseClick, Methode s.u.
                             // Testen, ob eine Bemassung getroffen wurde
            GeoObjectList li = base.GetObjectsUnderCursor(base.CurrentMousePoint);
            for (int i = 0; i < li.Count; ++i)
            {
                if (li[i] is Dimension)
                { // die getroffene Bem. nutzen
                    Dimension dimTemp = (li[i] as Dimension);
                    if (dimTemp.DimType == Dimension.EDimType.DimLocation)
                    {   // nur Bemassung gleichen Typs
                        int ind; // wo getroffen? nur an Hilfslinie, nicht am Text
                        Dimension.HitPosition hi = dimTemp.GetHitPosition(base.Frame.ActiveView.Projection, base.ProjectedPoint(CurrentMousePoint), out ind);
                        if ((hi & Dimension.HitPosition.DimLine) != 0)
                        { // jetzt also: Bemassung merken 
                            dimAddTo = dimTemp;
                            // und zur Kennung optisch den Refpunkt setzen
                            dim.DimLineRef = dimTemp.DimLineRef;
                        }
                    }
                }
            }
            PerpFoot();
        }

        private void DimLocationOnMouseClick(bool up, GeoPoint MousePosition, IView View)
        {
            if ((up & (dimAddTo != null)))
            {	// also: er will einen Punkt (dim.GetPoint(0)) zu einer bestehenden Label-Bem. (dimAddTo) zufügen
                if ((dimMethodPoint & curveInput.Fixed) | (!dimMethodPoint & dimPointInput.Fixed))
                { // nur, wenn der Punkt auf die ein oder andere Weise bestimmt ist
                    dimAddTo.AddPoint(dim.GetPoint(0));
                    // jetzt: schnell raus aus allem!
                    base.ActiveObject = null;
                    base.OnDone();
                    return;
                }
            }
        }

        private void SetDimString(string val)
        {
            dim.SetDimText(0, val);
        }

        private string GetDimString()
        {
            return dim.GetDimText(0);
        }

        private void SetMethod(Boolean val)
        {
            dimMethodPoint = val;
            curveInput.ReadOnly = !dimMethodPoint;
            curveInput.Optional = !dimMethodPoint;
            dimPointInput.ReadOnly = dimMethodPoint;
            dimPointInput.Optional = dimMethodPoint;
            if (dimMethodPoint) base.SetFocus(curveInput, true);
            else base.SetFocus(dimPointInput, true);
        }

        public override void OnSetAction()
        {
            dim = Dimension.Construct();
            if (dimLabelType == DimLabelType.Point)
            {
                idString = "Constr.Dimension.Point";
                pointString = "Constr.Dimension.Point.Point";
                objectString = "Constr.Dimension.Point.Object";
                locationString = "Constr.Dimension.Location";
                dim.DimType = Dimension.EDimType.DimLocation;
            }
            else
            {
                idString = "Constr.Dimension.Labeling";
                pointString = "Constr.Dimension.Labeling.Point";
                objectString = "Constr.Dimension.Labeling.Object";
                locationString = "Constr.Dimension.Labeling.Location";
                dim.DimType = Dimension.EDimType.DimLocation;
            }
            dim.DimLineRef = ConstrDefaults.DefaultDimPoint;
            dim.Normal = base.ActiveDrawingPlane.Normal;
            dim.AddPoint(new GeoPoint(0.0, 0.0, 0.0));

            dimMethodPoint = ConstrDefaults.DefaultDimensionPointMethod;

            base.TitleId = idString;
            base.ActiveObject = dim;
            base.ShowActiveObject = false;
            curveInput = new CurveInput(objectString);
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.ReadOnly = !dimMethodPoint;
            curveInput.Optional = !dimMethodPoint;
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(inputDimCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(inputDimCurvesChanged);

            dimPointInput = new GeoPointInput(pointString);
            dimPointInput.ReadOnly = dimMethodPoint;
            dimPointInput.Optional = dimMethodPoint;
            dimPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimPoint);

            dimLocationInput = new GeoPointInput(locationString);
            dimLocationInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDimLocation);
            dimLocationInput.MouseClickEvent += new MouseClickDelegate(DimLocationOnMouseClick);

            dimStringInput = new StringInput("Constr.Dimension.Labeling.Point.Text");
            dimStringInput.SetStringEvent += new CADability.Actions.ConstructAction.StringInput.SetStringDelegate(SetDimString);
            dimStringInput.GetStringEvent += new CADability.Actions.ConstructAction.StringInput.GetStringDelegate(GetDimString);

            dimMethod = new BooleanInput("Constr.Dimension.Point.Method", "Constr.Dimension.Point.Method.Values");
            dimMethod.DefaultBoolean = ConstrDefaults.DefaultDimensionPointMethod;
            dimMethod.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetMethod);


            if (dimLabelType == DimLabelType.Point)
                base.SetInput(curveInput, dimPointInput, dimLocationInput, dimMethod);
            else base.SetInput(curveInput, dimPointInput, dimLocationInput, dimStringInput, dimMethod);
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
            base.OnDone();
        }
        protected override void InputChanged(object activeInput)
        {
            if (activeInput == curveInput)
            {
                base.AutoCursor = false;
            }
            else base.AutoCursor = true;

        }

    }
}




