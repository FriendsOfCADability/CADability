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

    public class ConstructPolarPoint : ConstructAction, IIntermediateConstruction
    {

        private GeoPointProperty geoPointProperty;
        private GeoPointInput startPointInput;
        private AngleInput ang;
        private LengthInput len;
        private GeoPoint startPoint;
        private Angle angPolar;
        private GeoPoint currentPoint;
        private double lengthPolar;
        private GeoPoint actualPoint;
        private bool measure;
        private bool succeeded;

        private Line feedBackLine;

        public ConstructPolarPoint(GeoPointProperty geoPointProperty)
        {
            this.geoPointProperty = geoPointProperty;
            if (geoPointProperty != null)
                currentPoint = geoPointProperty.GetGeoPoint();
            startPoint = currentPoint;
            measure = (geoPointProperty == null);
            succeeded = false;
        }

        private void showFeedBackLine()
        {
            feedBackLine.SetTwoPoints(startPoint, base.ActiveDrawingPlane.ToGlobal(base.ActiveDrawingPlane.Project(startPoint) + lengthPolar * new GeoVector2D(angPolar)));
        }


        private void SetStartPoint(GeoPoint p)
        {
            startPoint = p;
            showFeedBackLine();
            if (ang.Fixed & len.Fixed)
            {
                //				actualPoint = startPoint + lengthPolar*new GeoVector(angPolar,0.0);
                actualPoint = base.ActiveDrawingPlane.ToGlobal(base.ActiveDrawingPlane.Project(startPoint) + lengthPolar * new GeoVector2D(angPolar));
                using (Frame.Project.Undo.ContextFrame(this))
                {
                    if (geoPointProperty != null)
                        geoPointProperty.SetGeoPoint(actualPoint);
                }
            }
        }

        private GeoPoint GetStartPoint()
        {
            return startPoint;
        }

        private bool angSetAngle(Angle angle)
        {
            if (angle >= 0.0)
            {
                angPolar = angle;
                showFeedBackLine();
                if (startPointInput.Fixed & len.Fixed)
                {
                    //					actualPoint = startPoint + lengthPolar*new GeoVector(angPolar,0.0);
                    actualPoint = base.ActiveDrawingPlane.ToGlobal(base.ActiveDrawingPlane.Project(startPoint) + lengthPolar * new GeoVector2D(angPolar));
                    using (Frame.Project.Undo.ContextFrame(this))
                    {
                        if (geoPointProperty != null)
                            geoPointProperty.SetGeoPoint(actualPoint);
                    }
                }
                return true;
            }
            return false;
        }

        private bool lenSetLength(double Length)
        {
            if (Length > 0.0)
            {
                lengthPolar = Length;
                showFeedBackLine();
                if (startPointInput.Fixed & ang.Fixed)
                {
                    //					actualPoint = startPoint + lengthPolar*new GeoVector(angPolar,0.0);
                    actualPoint = base.ActiveDrawingPlane.ToGlobal(base.ActiveDrawingPlane.Project(startPoint) + lengthPolar * new GeoVector2D(angPolar));
                    using (Frame.Project.Undo.ContextFrame(this))
                    {
                        if (geoPointProperty != null)
                            geoPointProperty.SetGeoPoint(actualPoint);
                    }
                }
                return true;
            }
            return false;
        }

        private double lenOnCalculateLength(GeoPoint MousePosition)
        {
            return Geometry.Dist(startPoint, MousePosition);
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

        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
        /// </summary>
		public override void OnSetAction()
        {
            base.TitleId = "Construct.PolarPoint";
            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            lengthPolar = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth4);

            startPointInput = new GeoPointInput("Construct.PolarPoint.StartPoint");
            startPointInput.DefinesBasePoint = true;
            startPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetStartPoint);
            startPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetStartPoint);

            ang = new AngleInput("Construct.PolarPoint.Angle", 0.0);
            ang.SetAngleEvent += new CADability.Actions.ConstructAction.AngleInput.SetAngleDelegate(angSetAngle);
            ang.ForwardMouseInputTo = startPointInput;

            len = new LengthInput("Construct.PolarPoint.Length", lengthPolar);
            len.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(lenSetLength);
            len.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(lenOnCalculateLength);
            len.ForwardMouseInputTo = startPointInput;

            GeoPointInput measureText = new GeoPointInput("MeasurePoint");
            measureText.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetMeasureText);
            // geht nur mit readOnly, da sonst die Mausbewegung den angezeigten Wert überschreibt
            measureText.ReadOnly = true;

            if (measure)
                base.SetInput(startPointInput, ang, len, measureText);
            else base.SetInput(startPointInput, ang, len);

            base.OnSetAction();
            base.FeedBack.Add(feedBackLine);
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
            Frame.Project.Undo.ClearContext();
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.GetID ()"/>
        /// </summary>
        /// <returns></returns>
		public override string GetID()
        {
            return "Construct.PolarPoint";
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnDone ()"/>
        /// </summary>
		public override void OnDone()
        {
            succeeded = true;
            if (geoPointProperty != null) geoPointProperty.ModifiedByAction(this);
            base.OnDone();
        }

    }
}






