using CADability.GeoObject;
using CADability.UserInterface;



namespace CADability.Actions
{
    /// <summary>
    /// Action that constructs a length as a distance between two points. It uses a <see cref="LengthProperty"/>
    /// to communicate the constructed length to the outside.
    /// </summary>

    public class ConstructDirectionOfSurface : ConstructAction, IIntermediateConstruction
    {
        private GeoVectorProperty vectorProperty;
        private GeoVector cancelVector;
        private GeoVector actualVector;
        private GeoVector faceVector;
        private int dirOffsetSelect;
        private bool measure;
        private bool succeeded;
        private SnapPointFinder.SnapModes snapModeSav;

        public ConstructDirectionOfSurface(GeoVectorProperty vectorProperty)
        {
            this.vectorProperty = vectorProperty;
            if (vectorProperty != null)
                cancelVector = vectorProperty.GetGeoVector();
            measure = (vectorProperty == null);
            succeeded = false;
        }

        private void selectDir()
        {
            actualVector = faceVector;
            if (dirOffsetSelect == 1) actualVector = -actualVector;
            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (vectorProperty != null)
                    vectorProperty.SetGeoVector(actualVector);
            }
        }



        private bool SetFacePoint(GeoPoint p, SnapPointFinder.DidSnapModes didSnap)
        {
            if (didSnap == SnapPointFinder.DidSnapModes.DidSnapToFaceSurface)
            {
                Face f = base.LastSnapObject as Face;
                if (f != null)
                {
                    GeoPoint2D p2d = f.Surface.PositionOf(p);
                    faceVector = -f.Surface.GetNormal(p2d);
                    selectDir();
                    return true;
                }
            }
            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (vectorProperty != null)
                    vectorProperty.SetGeoVector(cancelVector);
            }
            actualVector = cancelVector;
            return false;
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

        public override void OnSetAction()
        {
            base.TitleId = "Construct.DirectionOfSurface";

            dirOffsetSelect = ConstrDefaults.DefaultDirectionOffset;

            GeoPointInput facePoint = new GeoPointInput("Construct.DirectionOfSurface.Object");
            facePoint.SetGeoPointExEvent += new GeoPointInput.SetGeoPointExDelegate(SetFacePoint);

            MultipleChoiceInput dirOffset = new MultipleChoiceInput("Construct.DirectionOfSurface.DirOffset", "Construct.DirectionOfSurface.DirOffset.Values");
            dirOffset.DefaultChoice = ConstrDefaults.DefaultDirectionOffset;
            dirOffset.SetChoiceEvent += new CADability.Actions.ConstructAction.MultipleChoiceInput.SetChoiceDelegate(SetDirOffset);

            GeoVectorInput measureText = new GeoVectorInput("MeasureDirection");
            measureText.CalculateGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.CalculateGeoVectorDelegate(CalculateMeasureText);
            measureText.GetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.GetGeoVectorDelegate(GetMeasureText);
            measureText.IsAngle = true;

            if (measure)
                base.SetInput(facePoint, dirOffset, measureText);
            else base.SetInput(facePoint, dirOffset);
            base.OnSetAction();
            snapModeSav = base.Frame.SnapMode;
            base.Frame.SnapMode = SnapPointFinder.SnapModes.SnapToFaceSurface;
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
            Frame.Project.Undo.ClearContext();
        }


        public override string GetID()
        {
            return "Construct.DirectionOfSurface";
        }
        /// <summary>
        /// Implements <see cref="Action.OnInactivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="NewActiveAction"><paramref name="Action.OnInactivate.NewActiveAction"/></param>
        /// <param name="RemovingAction"><paramref name="Action.OnInactivate.RemovingAction"/></param>
        public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
            base.Frame.SnapMode = snapModeSav;
            base.OnInactivate(NewActiveAction, RemovingAction);
        }

        public override void OnDone()
        {
            succeeded = true;
            if (vectorProperty != null) vectorProperty.ModifiedByAction(this);
            base.OnDone();
        }

    }
}



