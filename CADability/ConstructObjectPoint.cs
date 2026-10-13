using CADability.GeoObject;
using CADability.UserInterface;

namespace CADability.Actions
{
    /// <summary>
    /// Action that constructs a midpoint between two points. It uses a <see cref="GeoPointProperty"/>
    /// to communicate the constructed point to the outside.
    /// </summary>

    public class ConstructObjectPoint : ConstructAction, IIntermediateConstruction
    {
        private GeoPointProperty geoPointProperty;
        private CurveInput ratioCurveInput;
        private DoubleInput ratioInput;
        private LengthInput ratioLength;
        static private double ratio;
        static private double ratioDist;
        private ICurve ratioCurveSel;
        private GeoPoint objectPoint1;
        private GeoPoint cancelPoint;
        private GeoPoint actualPoint;
        private CADability.GeoObject.Point gPoint;
        private bool measure;
        private bool succeeded;
        static private bool ratioAsLength;

        public ConstructObjectPoint(GeoPointProperty geoPointProperty)
        {
            this.geoPointProperty = geoPointProperty;
            if (geoPointProperty != null)
                cancelPoint = geoPointProperty.GetGeoPoint();
            measure = (geoPointProperty == null);
            succeeded = false;
        }

        private void Recalc()
        {
            if (ratioAsLength)
                ratio = ratioDist / ratioCurveSel.Length;
            else ratioDist = Geometry.Dist(ratioCurveSel.StartPoint, ratioCurveSel.PointAt(ratio));

            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (geoPointProperty != null)
                {
                    if (Geometry.Dist(objectPoint1, ratioCurveSel.StartPoint) < Geometry.Dist(objectPoint1, ratioCurveSel.EndPoint))
                        geoPointProperty.SetGeoPoint(ratioCurveSel.PointAt(ratio));
                    else geoPointProperty.SetGeoPoint(ratioCurveSel.PointAt(1.0 - ratio));
                    gPoint.Location = geoPointProperty.GetGeoPoint();
                }
                else
                {
                    if (Geometry.Dist(objectPoint1, ratioCurveSel.StartPoint) < Geometry.Dist(objectPoint1, ratioCurveSel.EndPoint))
                        actualPoint = ratioCurveSel.PointAt(ratio);
                    else actualPoint = ratioCurveSel.PointAt(1.0 - ratio);
                    gPoint.Location = actualPoint;
                }
            }
        }


        private bool ratioCurve(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint1 = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                ratioCurveSel = Curves[0];
                Recalc();
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

        private void ratioCurveChanged(CurveInput sender, ICurve SelectedCurve)
        {
            ratioCurveSel = SelectedCurve;
            Recalc();
        }

        private bool OnSetRatio(double val)
        {
            ratio = val;
            ratioAsLength = false;
            if (ratioCurveInput.Fixed) Recalc();
            return true;
        }

        private double OnGetRatio()
        {
            return ratio;
        }

        private double OnGetRatioLength()
        {
            return ratioDist;
        }

        private bool OnSetRatioLength(double Length)
        {
            ratioDist = Length;
            ratioAsLength = true;
            if (ratioCurveInput.Fixed) Recalc();
            return true;

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
            base.TitleId = "Construct.ObjectPoint";
            // da oben static private, werden diese Variablen gemerkt. Beim ersten Mal vorbesetzen:
            if (ratio == 0) ratio = 0.5;
            if (ratioDist == 0) ratioDist = 1;

            gPoint = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint);

            ratioCurveInput = new CurveInput("Construct.ObjectPoint.Curve");
            ratioCurveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            ratioCurveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(ratioCurve);
            ratioCurveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(ratioCurveChanged);

            ratioInput = new DoubleInput("Construct.ObjectPoint.Ratio");
            ratioInput.SetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.SetDoubleDelegate(OnSetRatio);
            ratioInput.Fixed = true; // muss nicht eingegeben werden
            ratioInput.GetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.GetDoubleDelegate(OnGetRatio);
            ratioInput.ForwardMouseInputTo = ratioCurveInput;
            //			ratio = 0.5; // immer Standardwert

            ratioLength = new LengthInput("Construct.ObjectPoint.RatioLength");
            ratioLength.SetLengthEvent += new LengthInput.SetLengthDelegate(OnSetRatioLength);
            ratioLength.GetLengthEvent += new LengthInput.GetLengthDelegate(OnGetRatioLength);
            ratioLength.Fixed = true; // muss nicht eingegeben werden
            ratioLength.ForwardMouseInputTo = ratioCurveInput;

            GeoPointInput measureText = new GeoPointInput("MeasurePoint");
            measureText.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetMeasureText);
            // geht nur mit readOnly, da sonst die Mausbewegung den angezeigten Wert überschreibt
            measureText.ReadOnly = true;
            if (measure)
                base.SetInput(ratioCurveInput, ratioInput, ratioLength, measureText);
            else base.SetInput(ratioCurveInput, ratioInput, ratioLength);
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
            return "Construct.ObjectPoint";
        }
        /// <inheritdoc/>
		public override void OnDone()
        {
            succeeded = true;
            if (geoPointProperty != null) geoPointProperty.ModifiedByAction(this);
            base.OnDone();
        }
        public override bool OnEscape()
        {
            if (geoPointProperty != null) geoPointProperty.SetGeoPoint(cancelPoint);
            return base.OnEscape();
        }

    }
}


