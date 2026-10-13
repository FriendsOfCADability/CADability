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
    /// Action that constructs a length as a distance between two points. It uses a <see cref="LengthProperty"/>
    /// to communicate the constructed length to the outside.
    /// </summary>

    public class ConstructVectorPoint : ConstructAction, IIntermediateConstruction
    {

        private GeoPointProperty geoPointProperty;
        private GeoVectorInput vec;
        private GeoPointInput startPointInput;
        private GeoPointInput endPointInput;
        private GeoPoint startPoint;
        private GeoPoint endPoint;
        private GeoPoint currentPoint;
        private GeoPoint actualPoint;
        private bool measure;
        private bool succeeded;

        private Line feedBackLine;

        public ConstructVectorPoint(GeoPointProperty geoPointProperty)
        {
            this.geoPointProperty = geoPointProperty;
            if (geoPointProperty != null)
                currentPoint = geoPointProperty.GetGeoPoint();
            measure = (geoPointProperty == null);
            succeeded = false;
        }


        private bool Vec_OnSetGeoVector(GeoVector vector)
        {
            if (!vector.IsNullVector())
            {
                actualPoint = currentPoint + vector;
                using (Frame.Project.Undo.ContextFrame(this))
                {
                    if (geoPointProperty != null)
                        geoPointProperty.SetGeoPoint(actualPoint);
                }
                return true;
            }
            else return false;
        }

        private void VecOnMouseClick(bool up, GeoPoint MousePosition, IView View)
        {
            if (!up) // also beim Drücken, nicht beim Loslassen
            {
                startPoint = MousePosition; // den Runterdrückpunkt merken
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
                    actualPoint = currentPoint + vect;
                    using (Frame.Project.Undo.ContextFrame(this))
                    {
                        if (geoPointProperty != null)
                            geoPointProperty.SetGeoPoint(actualPoint);
                    }
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
            base.TitleId = "Construct.VectorPoint";
            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);

            vec = new GeoVectorInput("Construct.VectorPoint.Vector");
            vec.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(Vec_OnSetGeoVector);
            vec.MouseClickEvent += new MouseClickDelegate(VecOnMouseClick);

            startPointInput = new GeoPointInput("Construct.VectorPoint.StartPoint");
            startPointInput.Optional = true;
            startPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetStartPoint);
            startPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetStartPoint);

            endPointInput = new GeoPointInput("Construct.VectorPoint.EndPoint");
            endPointInput.Optional = true;
            endPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetEndPoint);
            endPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetEndPoint);

            GeoPointInput measureText = new GeoPointInput("MeasurePoint");
            measureText.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetMeasureText);
            // geht nur mit readOnly, da sonst die Mausbewegung den angezeigten Wert überschreibt
            measureText.ReadOnly = true;

            if (measure)
                base.SetInput(vec, startPointInput, endPointInput, measureText);
            else base.SetInput(vec, startPointInput, endPointInput);
            base.OnSetAction();
            base.FeedBack.Add(feedBackLine);
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
            Frame.Project.Undo.ClearContext();
        }

        /// <inheritdoc/>
		public override string GetID()
        {
            return "Construct.VectorPoint";
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





