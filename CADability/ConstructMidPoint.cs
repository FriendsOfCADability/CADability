using CADability.Attribute;
using CADability.GeoObject;
using CADability.UserInterface;
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
    /// Action that constructs a midpoint between two points. It uses a <see cref="GeoPointProperty"/>
    /// to communicate the constructed point to the outside.
    /// </summary>

    public class ConstructMidPoint : ConstructAction, IIntermediateConstruction
    {
        private GeoPointProperty geoPointProperty;
        private GeoPointInput firstPointInput;
        private GeoPointInput secondPointInput;
        private DoubleInput ratioInput;
        private GeoPoint firstPoint;
        private GeoPoint secondPoint;
        private bool firstPointIsValid;
        private bool secondPointIsValid;
        static private double ratio;
        private Line feedBackLine;
        private CADability.GeoObject.Point gPoint;
        private bool measure;
        private GeoPoint actualPoint;
        private GeoPoint originalPoint;
        private bool succeeded;

        public ConstructMidPoint(GeoPointProperty geoPointProperty)
        {
            this.geoPointProperty = geoPointProperty;
            if (geoPointProperty != null)
                originalPoint = firstPoint = geoPointProperty.GetGeoPoint();
            secondPoint = firstPoint;
            firstPointIsValid = false;
            secondPointIsValid = false;
            measure = (geoPointProperty == null);
            succeeded = false;
        }
        private void Recalc()
        {
            if (firstPointIsValid && secondPointIsValid)
            {
                feedBackLine.SetTwoPoints(firstPoint, secondPoint);
                actualPoint = firstPoint + ratio * (secondPoint - firstPoint);
                using (Frame.Project.Undo.ContextFrame(this))
                {
                    if (geoPointProperty != null)
                        geoPointProperty.SetGeoPoint(actualPoint);
                }
                gPoint.Location = actualPoint;
            }
        }
        private void OnSetFirstPoint(GeoPoint p)
        {
            firstPoint = p;
            firstPointIsValid = true;
            Recalc();
        }
        private void OnSetSecondPoint(GeoPoint p)
        {
            secondPoint = p;
            secondPointIsValid = true;
            Recalc();
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

        private bool OnSetRatio(double val)
        {
            ratio = val;
            Recalc();
            return true;
        }

        private double OnGetRatio()
        {
            return ratio;
        }

        private GeoPoint GetMeasureText()
        {
            return actualPoint;
        }

        /// <inheritdoc/>
		public override void OnSetAction()
        {
            base.TitleId = "Construct.MidPoint";

            // da oben static private, werden diese Variablen gemerkt. Beim ersten Mal vorbesetzen:
            if (ratio == 0) ratio = 0.5;

            gPoint = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint);

            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            base.FeedBack.Add(feedBackLine);

            firstPointInput = new GeoPointInput("Construct.MidPoint.FirstPoint");
            firstPointInput.DefinesBasePoint = true;
            firstPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetFirstPoint);

            secondPointInput = new GeoPointInput("Construct.MidPoint.SecondPoint");
            secondPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetSecondPoint);

            ratioInput = new DoubleInput("Construct.MidPoint.Ratio");
            ratioInput.SetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.SetDoubleDelegate(OnSetRatio);
            ratioInput.Fixed = true; // muss nicht eingegeben werden
            ratioInput.GetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.GetDoubleDelegate(OnGetRatio);
            ratioInput.ForwardMouseInputTo = new object[] { firstPointInput, secondPointInput };
            //			ratio = 0.5; // immer Standardwert

            firstPointIsValid = false;
            secondPointIsValid = false;

            GeoPointInput measureText = new GeoPointInput("MeasurePoint");
            measureText.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetMeasureText);
            // geht nur mit readOnly, da sonst die Mausbewegung den angezeigten Wert überschreibt
            measureText.ReadOnly = true;

            if (measure)
                base.SetInput(firstPointInput, secondPointInput, ratioInput, measureText);
            else base.SetInput(firstPointInput, secondPointInput, ratioInput);

            base.OnSetAction();
        }
        /// <inheritdoc/>
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
            Frame.Project.Undo.ClearContext();
        }
        /// <inheritdoc/>
		public override string GetID()
        {
            return "Construct.MidPoint";
        }
        /// <inheritdoc/>
		public override void OnDone()
        {
            succeeded = true;
            if (geoPointProperty != null) geoPointProperty.ModifiedByAction(this);
            base.OnDone();
        }
        /// <inheritdoc/>
        public override bool OnEscape()
        {
            if (geoPointProperty != null)
                geoPointProperty.SetGeoPoint(originalPoint);
            return base.OnEscape();
        }
    }
}
