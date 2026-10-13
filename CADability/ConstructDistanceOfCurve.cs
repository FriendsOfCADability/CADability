using CADability.GeoObject;
using CADability.UserInterface;



namespace CADability.Actions
{
    /// <summary>
    /// Action that constructs a length as a length of a curve. It uses a <see cref="LengthProperty"/>
    /// to communicate the constructed length to the outside.
    /// </summary>

    public class ConstructDistanceOfCurve : ConstructAction, IIntermediateConstruction
    {
        private LengthProperty lengthProperty;
        private CurveInput curveInput;
        private double cancelLength;
        private double actualLength;
        private bool measure;
        private bool succeeded;

        public ConstructDistanceOfCurve(LengthProperty lengthProperty)
        {
            this.lengthProperty = lengthProperty;
            if (lengthProperty != null)
                cancelLength = lengthProperty.GetLength();
            measure = (lengthProperty == null);
            succeeded = false;
        }

        private bool DistCurve(CurveInput sender, ICurve[] Curves, bool up)
        {
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                using (Frame.Project.Undo.ContextFrame(this))
                {
                    if (lengthProperty != null)
                        lengthProperty.SetLength(Curves[0].Length);
                }
                actualLength = Curves[0].Length;
                return true;
            }
            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (lengthProperty != null)
                    lengthProperty.SetLength(cancelLength);
            }
            actualLength = 0;
            return false;
        }

        private void DistCurveChanged(CurveInput sender, ICurve SelectedCurve)
        {
            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (lengthProperty != null)
                    lengthProperty.SetLength(SelectedCurve.Length);
            }
            actualLength = SelectedCurve.Length;
        }

        private double GetMeasureText()
        {
            return actualLength;
        }

        private double CalculateMeasureText(GeoPoint MousePosition)
        {
            return actualLength;
        }

        #region IIntermediateConstruction Members
        IPropertyEntry IIntermediateConstruction.GetHandledProperty()
        {
            return lengthProperty;
        }
        bool IIntermediateConstruction.Succeeded
        {
            get { return succeeded; }
        }
        #endregion

        /// <inheritdoc/>
        public override void OnSetAction()
        {
            base.TitleId = "Construct.DistanceOfCurve";

            curveInput = new CurveInput("Construct.DistanceOfCurve.Object");
            curveInput.Decomposed = true; // only decomposed parts of a curve like segments of a polygon or path
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(DistCurve);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(DistCurveChanged);

            LengthInput measureText = new LengthInput("MeasureLength");
            measureText.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(CalculateMeasureText);
            measureText.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetMeasureText);

            if (measure)
                base.SetInput(curveInput, measureText);
            else base.SetInput(curveInput);
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
            Frame.Project.Undo.ClearContext();
			if (lengthProperty != null)
				lengthProperty.Select();
        }

        /// <inheritdoc/>
		public override string GetID()
        {
            return "Construct.DistanceOfCurve";
        }
        /// <inheritdoc/>
        public override void OnDone()
        {
            succeeded = true;
            base.OnDone();
        }
    }
}



