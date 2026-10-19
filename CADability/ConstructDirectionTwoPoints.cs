using CADability.Attribute;
using CADability.GeoObject;
using CADability.UserInterface;
using System.Drawing;
using Point = System.Drawing.Point;

namespace CADability.Actions
{
    /// <summary>
    /// Action that constructs a length as a distance between two points. It uses a <see cref="LengthProperty"/>
    /// to communicate the constructed length to the outside.
    /// </summary>

    public class ConstructDirectionTwoPoints : ConstructAction, IIntermediateConstruction
    {

        private GeoVectorProperty vectorProperty;
        private GeoVectorInput vec;
        private GeoPointInput startPointInput;
        private GeoPointInput endPointInput;
        private GeoPoint startPoint;
        private GeoPoint endPoint;
        private GeoVector actualVector;
        private bool measure;
        private bool succeeded;

        private Line feedBackLine;

        public ConstructDirectionTwoPoints(GeoVectorProperty vectorProperty)
        {
            this.vectorProperty = vectorProperty;
            measure = (vectorProperty == null);
            succeeded = false;
        }


        private bool Vec_OnSetGeoVector(GeoVector vector)
        {
            if (!vector.IsNullVector())
            {
                using (Frame.Project.Undo.ContextFrame(this))
                {
                    if (vectorProperty != null)
                        vectorProperty.SetGeoVector(vector);
                }
                actualVector = vector;
                return true;
            }
            else return false;
        }

        private void VecOnMouseClick(bool up, GeoPoint MousePosition, IView View)
        {
            if (!up) // also beim Drücken, nicht beim Loslassen
            {
                startPoint = MousePosition; // den Runterdrückpunkt merken
                base.BasePoint = startPoint;
                vec.SetVectorFromPoint(MousePosition);
                startPointInput.Fixed = true; // und sagen dass er existiert
                base.FeedBack.Add(feedBackLine);
                base.SetFocus(endPointInput, true); // Focus auf den Endpunkt setzen
            }

        }


        private void SetStartPoint(GeoPoint p)
        {
            startPoint = p;
            vec.SetVectorFromPoint(p);
            endPointInput.Optional = false;
            base.FeedBack.Add(feedBackLine);
        }

        private GeoPoint GetStartPoint()
        {
            return startPoint;
        }

        private void SetEndPoint(GeoPoint p)
        {
            endPoint = p;
            if (startPointInput.Fixed)
            {
                vec.Fixed = true; // damit die Aktion nach dem Endpunkt aufhört
                GeoVector vect = new GeoVector(startPoint, p);
                if (!vect.IsNullVector())
                {
                    feedBackLine.SetTwoPoints(startPoint, p);
                    using (Frame.Project.Undo.ContextFrame(this))
                    {
                        if (vectorProperty != null)
                            vectorProperty.SetGeoVector(vect);
                    }
                    actualVector = vect;
                }
            }
        }

        private GeoPoint GetEndPoint()
        {
            return endPoint;
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
            base.TitleId = "Construct.DirectionTwoPoints";
            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            base.FeedBack.Add(feedBackLine);

            vec = new GeoVectorInput("Construct.DirectionTwoPoints.Vector");
            vec.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(Vec_OnSetGeoVector);
            vec.MouseClickEvent += new MouseClickDelegate(VecOnMouseClick);

            startPointInput = new GeoPointInput("Construct.DirectionTwoPoints.StartPoint");
            startPointInput.Optional = true;
            startPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetStartPoint);
            startPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetStartPoint);

            endPointInput = new GeoPointInput("Construct.DirectionTwoPoints.EndPoint");
            endPointInput.Optional = true;
            endPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetEndPoint);
            endPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetEndPoint);

            GeoVectorInput measureText = new GeoVectorInput("MeasureDirection");
            measureText.CalculateGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.CalculateGeoVectorDelegate(CalculateMeasureText);
            measureText.GetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.GetGeoVectorDelegate(GetMeasureText);
            measureText.IsAngle = true;

            if (measure)
                base.SetInput(vec, startPointInput, endPointInput, measureText);
            else base.SetInput(vec, startPointInput, endPointInput);

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
            return "Construct.DirectionTwoPoints";
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




