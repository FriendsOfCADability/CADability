using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;

namespace CADability.Actions
{
    /// <summary>
    /// Action that constructs a midpoint between two points. It uses a <see cref="GeoPointProperty"/>
    /// to communicate the constructed point to the outside.
    /// </summary>

    public class ConstructIntersectPoint : ConstructAction, IIntermediateConstruction
    {
        private GeoPointProperty geoPointProperty;
        private CurveInput firstCurveInput;
        private CurveInput secondCurveInput;
        private GeoPoint objectPoint1;
        private GeoPoint objectPoint2;
        private GeoPoint cancelPoint;
        private GeoPoint actualPoint;
        private ICurve intersectCurve1;
        private ICurve intersectCurve2;
        private CADability.GeoObject.Point gPoint;
        private bool measure;
        private bool succeeded;

        public ConstructIntersectPoint(GeoPointProperty geoPointProperty)
        {
            this.geoPointProperty = geoPointProperty;
            if (geoPointProperty != null)
                cancelPoint = geoPointProperty.GetGeoPoint();
            measure = (geoPointProperty == null);
            succeeded = false;
        }

        private bool Recalc()
        {
            double mindist = double.MaxValue;
            ICurve2D c1_2D;
            ICurve2D c2_2D;
            GeoPoint objectPoint = new GeoPoint(objectPoint1, objectPoint2); // die Mitte der Pickpunkte der zwei Objekte
            GeoPoint2D startPoint2D = new GeoPoint2D(0.0, 0.0);
            Plane pl;
            if (Curves.GetCommonPlane(intersectCurve1, intersectCurve2, out pl)) // die Kurven liegen in einer Ebene
            {
                c1_2D = intersectCurve1.GetProjectedCurve(pl);
                if (c1_2D is Path2D) (c1_2D as Path2D).Flatten();
                c2_2D = intersectCurve2.GetProjectedCurve(pl);
                if (c2_2D is Path2D) (c2_2D as Path2D).Flatten();
                GeoPoint2DWithParameter[] intersectPoints = c1_2D.Intersect(c2_2D); // die Schnittpunkte 
                if (intersectPoints.Length > 0)
                {   // nun den nächsten auswählen
                    for (int j = 0; j < intersectPoints.Length; ++j)
                    {
                        double dist = Geometry.Dist(intersectPoints[j].p, pl.Project(objectPoint));
                        if (dist < mindist)
                        {
                            mindist = dist;
                            startPoint2D = intersectPoints[j].p;
                        }
                    }
                }
                if (mindist < double.MaxValue)
                {
                    actualPoint = pl.ToGlobal(startPoint2D);
                    using (Frame.Project.Undo.ContextFrame(this))
                    {
                        if (geoPointProperty != null)
                            geoPointProperty.SetGeoPoint(actualPoint);
                    }
                    gPoint.Location = actualPoint;
                    return true;
                }
            }
            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (geoPointProperty != null)
                    geoPointProperty.SetGeoPoint(cancelPoint);
            }
            actualPoint = cancelPoint;
            return false;
        }

        private bool firstCurve(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint1 = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                intersectCurve1 = Curves[0];
                if (secondCurveInput.Fixed)
                    if (intersectCurve1 != intersectCurve2) return Recalc();
                return true;
            }
            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (geoPointProperty != null)
                    geoPointProperty.SetGeoPoint(cancelPoint);
            }
            actualPoint = cancelPoint;
            gPoint.Location = objectPoint1;
            return false;
        }

        private void firstCurveChanged(CurveInput sender, ICurve SelectedCurve)
        {
            intersectCurve1 = SelectedCurve;
            if (secondCurveInput.Fixed)
                if (intersectCurve1 != intersectCurve2) Recalc();

        }
        private bool secondCurve(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint2 = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                intersectCurve2 = Curves[0];
                if (firstCurveInput.Fixed)
                    if (intersectCurve1 != intersectCurve2) return Recalc();
                return true;
            }
            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (geoPointProperty != null)
                    geoPointProperty.SetGeoPoint(cancelPoint);
            }
            actualPoint = cancelPoint;
            gPoint.Location = objectPoint2;
            return false;
        }

        private void secondCurveChanged(CurveInput sender, ICurve SelectedCurve)
        {
            intersectCurve2 = SelectedCurve;
            if (firstCurveInput.Fixed)
                if (intersectCurve1 != intersectCurve2) Recalc();

        }

        #region IIntermediateConstruction Members
        IPropertyEntry IIntermediateConstruction.GetHandledProperty()
        {
            return geoPointProperty;
        }
        bool IIntermediateConstruction.Succeeded
        {
            get { return succeeded; }
        }
        #endregion

        private GeoPoint GetMeasureText()
        {
            return actualPoint;
        }

        /// <inheritdoc/>
		public override void OnSetAction()
        {
            base.TitleId = "Construct.IntersectPoint";

            gPoint = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint);

            firstCurveInput = new CurveInput("Construct.IntersectPoint.FirstCurve");
            firstCurveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            firstCurveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(firstCurve);
            firstCurveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(firstCurveChanged);

            secondCurveInput = new CurveInput("Construct.IntersectPoint.SecondCurve");
            secondCurveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            secondCurveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(secondCurve);
            secondCurveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(secondCurveChanged);

            GeoPointInput measureText = new GeoPointInput("MeasurePoint");
            measureText.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetMeasureText);
            // geht nur mit readOnly, da sonst die Mausbewegung den angezeigten Wert überschreibt
            measureText.ReadOnly = true;
            if (measure)
                base.SetInput(firstCurveInput, secondCurveInput, measureText);
            else base.SetInput(firstCurveInput, secondCurveInput);
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
            Frame.Project.Undo.ClearContext();
        }
        /// <inheritdoc/>
		public override string GetID()
        {
            return "Construct.IntersectPoint";
        }
        /// <inheritdoc/>
		public override void OnDone()
        {
            succeeded = true;
            if (geoPointProperty != null) geoPointProperty.ModifiedByAction(this);
            base.OnDone();
        }


    }
}

