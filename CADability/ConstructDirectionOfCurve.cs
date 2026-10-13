using CADability.GeoObject;
using CADability.UserInterface;



namespace CADability.Actions
{
    /// <summary>
    /// Action that constructs a length as a distance between two points. It uses a <see cref="LengthProperty"/>
    /// to communicate the constructed length to the outside.
    /// </summary>

    public class ConstructDirectionOfCurve : ConstructAction, IIntermediateConstruction
    {
        private GeoVectorProperty vectorProperty;
        private CurveInput curveInput;
        private GeoVector cancelVector;
        private GeoVector actualVector;
        private ICurve dirCurve;
        private GeoPoint objectPoint;
        private int dirPointSelect;
        private int dirOffsetSelect;
        private bool measure;
        private bool succeeded;

        public ConstructDirectionOfCurve(GeoVectorProperty vectorProperty)
        {
            this.vectorProperty = vectorProperty;
            if (vectorProperty != null)
                cancelVector = vectorProperty.GetGeoVector();
            measure = (vectorProperty == null);
            succeeded = false;
        }

        private void selectDir()
        {
            GeoVector dirCurveMod = new GeoVector(0.0, 1.0, 1.0);
            switch (dirPointSelect)
            { // Startpunkt|Automatik|Endpunkt|Mittelpunkt|freier Punkt
                default:
                    dirCurveMod = dirCurve.StartDirection;
                    break;
                case 0: // Startpunkt
                    dirCurveMod = dirCurve.StartDirection;
                    break;
                case 1: // Automatik
                    if (dirCurve.PositionOf(objectPoint) > 0.66)
                        dirCurveMod = dirCurve.EndDirection;
                    else
                        if (dirCurve.PositionOf(objectPoint) > 0.33)
                        dirCurveMod = dirCurve.DirectionAt(0.5);
                    else
                        dirCurveMod = dirCurve.StartDirection;
                    break;
                case 2: // Endpunkt
                    dirCurveMod = dirCurve.EndDirection;
                    break;
                case 3: // Mittelpunkt
                    dirCurveMod = dirCurve.DirectionAt(0.5);
                    break;
                case 4: // freier Punkt
                    dirCurveMod = dirCurve.DirectionAt(dirCurve.PositionOf(objectPoint));
                    break;
            }
            Plane pl;
            if (dirCurve.GetPlanarState() == PlanarState.Planar)
                pl = dirCurve.GetPlane();
            else pl = base.ActiveDrawingPlane;
            for (int i = 0; i < (dirOffsetSelect); ++i)
                dirCurveMod = dirCurveMod ^ pl.Normal;
            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (vectorProperty != null)
                    vectorProperty.SetGeoVector(dirCurveMod);
            }
            actualVector = dirCurveMod;
        }


        private bool DirCurve(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                dirCurve = Curves[0];
                selectDir();
                return true;
            }
            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (vectorProperty != null)
                    vectorProperty.SetGeoVector(cancelVector);
            }
            actualVector = cancelVector;
            return false;
        }

        private void DirCurveChanged(CurveInput sender, ICurve SelectedCurve)
        {
            dirCurve = SelectedCurve;
            selectDir();
        }

        private void SetDirPoint(int val)
        {
            dirPointSelect = val;
        }

        private void SetDirOffset(int val)
        {
            dirOffsetSelect = val;
        }

        #region IIntermediateConstruction Members
        IPropertyEntry IIntermediateConstruction.GetHandledProperty()
        {
            return vectorProperty;
        }
        bool IIntermediateConstruction.Succeeded
        {
            get { return succeeded; }
        }
        #endregion

        private GeoVector GetMeasureText()
        {
            return actualVector;
        }

        private GeoVector CalculateMeasureText(GeoPoint MousePosition)
        {
            return actualVector;
        }

        /// <inheritdoc/>
		public override void OnSetAction()
        {
            base.TitleId = "Construct.DirectionOfCurve";

            dirPointSelect = ConstrDefaults.DefaultDirectionPoint;
            dirOffsetSelect = ConstrDefaults.DefaultDirectionOffset;

            curveInput = new CurveInput("Construct.DirectionOfCurve.Object");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(DirCurve);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(DirCurveChanged);

            MultipleChoiceInput dirPoint = new MultipleChoiceInput("Construct.DirectionOfCurve.DirPoint", "Construct.DirectionOfCurve.DirPoint.Values");
            dirPoint.DefaultChoice = ConstrDefaults.DefaultDirectionPoint;
            dirPoint.SetChoiceEvent += new CADability.Actions.ConstructAction.MultipleChoiceInput.SetChoiceDelegate(SetDirPoint);

            MultipleChoiceInput dirOffset = new MultipleChoiceInput("Construct.DirectionOfCurve.DirOffset", "Construct.DirectionOfCurve.DirOffset.Values");
            dirOffset.DefaultChoice = ConstrDefaults.DefaultDirectionOffset;
            dirOffset.SetChoiceEvent += new CADability.Actions.ConstructAction.MultipleChoiceInput.SetChoiceDelegate(SetDirOffset);

            GeoVectorInput measureText = new GeoVectorInput("MeasureDirection");
            measureText.CalculateGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.CalculateGeoVectorDelegate(CalculateMeasureText);
            measureText.GetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.GetGeoVectorDelegate(GetMeasureText);
            measureText.IsAngle = true;

            if (measure)
                base.SetInput(curveInput, dirPoint, dirOffset, measureText);
            else base.SetInput(curveInput, dirPoint, dirOffset);
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
            return "Construct.DirectionOfCurve";
        }

        /// <inheritdoc/>
		public override void OnDone()
        {
            succeeded = true;
            if (vectorProperty != null) vectorProperty.ModifiedByAction(this);
            base.OnDone();
        }

    }
}



