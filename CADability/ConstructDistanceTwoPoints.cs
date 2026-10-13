using CADability.Attribute;
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
    /// Action that constructs a length as a distance between two points. It uses a <see cref="LengthProperty"/>
    /// to communicate the constructed length to the outside.
    /// </summary>

    public class ConstructDistanceTwoPoints : ConstructAction, IIntermediateConstruction
    {
        private LengthProperty lengthProperty;
        private GeoPointInput firstPointInput;
        private GeoPointInput secondPointInput;
        private GeoPoint firstPoint;
        private GeoPoint secondPoint;
        private bool firstPointIsValid;
        private bool secondPointIsValid;
        private Line feedBackLine;
        private bool measure;
        private bool succeeded;

        public ConstructDistanceTwoPoints(LengthProperty lengthProperty)
        {
            this.lengthProperty = lengthProperty;
            measure = (lengthProperty == null);
            succeeded = false;
        }
        private void Recalc()
        {
            if (firstPointIsValid && secondPointIsValid)
            {
                feedBackLine.SetTwoPoints(firstPoint, secondPoint);
                using (Frame.Project.Undo.ContextFrame(this))
                {
                    if (lengthProperty != null)
                        lengthProperty.SetLength(Geometry.Dist(firstPoint, secondPoint));
                }
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
            return lengthProperty;
        }
        bool IIntermediateConstruction.Succeeded
        {
            get { return succeeded; }
        }
        #endregion


        private double GetMeasureText()
        {
            if (firstPointIsValid && secondPointIsValid)
                return Geometry.Dist(firstPoint, secondPoint);
            else return 0;
        }

        private double CalculateMeasureText(GeoPoint MousePosition)
        {
            if (firstPointIsValid && secondPointIsValid)
                return Geometry.Dist(firstPoint, secondPoint);
            else return 0;
        }


        private double GetMeasureTextx()
        {
            if (firstPointIsValid && secondPointIsValid)
                return Math.Abs(firstPoint.x - secondPoint.x);
            else return 0;
        }

        private double CalculateMeasureTextx(GeoPoint MousePosition)
        {
            if (firstPointIsValid && secondPointIsValid)
                return Math.Abs(firstPoint.x - secondPoint.x);
            else return 0;
        }

        private double GetMeasureTexty()
        {
            if (firstPointIsValid && secondPointIsValid)
                return Math.Abs(firstPoint.y - secondPoint.y);
            else return 0;
        }

        private double CalculateMeasureTexty(GeoPoint MousePosition)
        {
            if (firstPointIsValid && secondPointIsValid)
                return Math.Abs(firstPoint.y - secondPoint.y);
            else return 0;
        }

        private double GetMeasureTextz()
        {
            if (firstPointIsValid && secondPointIsValid)
                return Math.Abs(firstPoint.z - secondPoint.z);
            else return 0;
        }

        private double CalculateMeasureTextz(GeoPoint MousePosition)
        {
            if (firstPointIsValid && secondPointIsValid)
                return Math.Abs(firstPoint.z - secondPoint.z);
            else return 0;
        }

        /// <inheritdoc/>
        public override void OnSetAction()
        {
            base.TitleId = "Construct.DistanceTwoPoints";
            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            base.FeedBack.Add(feedBackLine);

            firstPointInput = new GeoPointInput("Construct.DistanceTwoPoints.FirstPoint");
            firstPointInput.DefinesBasePoint = true;
            firstPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetFirstPoint);

            secondPointInput = new GeoPointInput("Construct.DistanceTwoPoints.SecondPoint");
            secondPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetSecondPoint);

            LengthInput measureText = new LengthInput("MeasureLength");
            measureText.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(CalculateMeasureText);
            measureText.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetMeasureText);

            LengthInput measureTextx = new LengthInput("MeasureLengthx");
            measureTextx.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(CalculateMeasureTextx);
            measureTextx.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetMeasureTextx);

            LengthInput measureTexty = new LengthInput("MeasureLengthy");
            measureTexty.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(CalculateMeasureTexty);
            measureTexty.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetMeasureTexty);

            LengthInput measureTextz = new LengthInput("MeasureLengthz");
            measureTextz.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(CalculateMeasureTextz);
            measureTextz.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetMeasureTextz);

            firstPointIsValid = false;
            secondPointIsValid = false;
            if (measure)
                base.SetInput(firstPointInput, secondPointInput, measureText, measureTextx, measureTexty, measureTextz);
            else base.SetInput(firstPointInput, secondPointInput);
            base.OnSetAction();
        }
        /// <inheritdoc/>
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
            return "Construct.DistanceTwoPoints";
        }
        /// <inheritdoc/>
        public override void OnDone()
        {
            succeeded = true;
            base.OnDone();
        }

    }
}
