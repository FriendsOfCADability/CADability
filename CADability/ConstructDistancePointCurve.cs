using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Drawing;
using Point = System.Drawing.Point;

namespace CADability.Actions
{
    /// <summary>
    /// Action that constructs a length as a distance between two points. It uses a <see cref="LengthProperty"/>
    /// to communicate the constructed length to the outside.
    /// </summary>

    public class ConstructDistancePointCurve : ConstructAction, IIntermediateConstruction
    {
        private LengthProperty lengthProperty;
        private GeoPointInput pointInput;
        private CurveInput curveInput;
        private ICurve distCurve;
        private GeoPoint distPoint;
        private GeoPoint secondPoint;
        private double cancelLength;
        private double actualLength;
        private Line feedBackLine;
        private bool feedBackAdd;
        private bool measure;
        private bool succeeded;

        public ConstructDistancePointCurve(LengthProperty lengthProperty)
        {
            this.lengthProperty = lengthProperty;
            if (lengthProperty != null)
                cancelLength = lengthProperty.GetLength();
            measure = (lengthProperty == null);
            succeeded = false;
        }

        private bool Recalc()
        {
            Plane pl;
            GeoPoint2D lotPoint = new GeoPoint2D(0.0, 0.0);
            if (Curves.GetCommonPlane(distPoint, distCurve, out pl))
            {
                ICurve2D curve_2D = distCurve.GetProjectedCurve(pl); // die 2D-Kurve
                if (curve_2D is Path2D) (curve_2D as Path2D).Flatten();
                GeoPoint2D distPoint2D = pl.Project(distPoint);
                GeoPoint2D[] perpP = curve_2D.PerpendicularFoot(distPoint2D); // Lotpunkt(e) Kurve 
                double distCP = double.MaxValue;
                for (int j = 0; j < perpP.Length; ++j) // Schleife über alle Lotpunkte Kurve
                {
                    double distLoc = Geometry.Dist(perpP[j], distPoint2D);
                    if (distLoc < distCP)
                    {
                        distCP = distLoc;
                        lotPoint = perpP[j]; // Lotpunkt schonmal merken	
                    }
                }
                if (distCP < double.MaxValue)
                {
                    feedBackLine.SetTwoPoints(distPoint, pl.ToGlobal(lotPoint));
                    if (!feedBackAdd)
                    {
                        base.FeedBack.Add(feedBackLine);
                        feedBackAdd = true;
                    }
                    actualLength = Geometry.Dist(distPoint, pl.ToGlobal(lotPoint));
                    secondPoint = pl.ToGlobal(lotPoint);
                    using (Frame.Project.Undo.ContextFrame(this))
                    {
                        if (lengthProperty != null)
                            lengthProperty.SetLength(actualLength);
                    }
                    return true;
                }
            }
            if (feedBackAdd)
            {
                base.FeedBack.Remove(feedBackLine);
                feedBackAdd = false;
            }

            using (Frame.Project.Undo.ContextFrame(this))
            {
                if (lengthProperty != null)
                    lengthProperty.SetLength(cancelLength);
            }
            actualLength = 0;
            return false;
        }

        private void DistPoint(GeoPoint p)
        {
            distPoint = p;
            if (curveInput.Fixed) Recalc();
        }

        private bool DistCurve(CurveInput sender, ICurve[] Curves, bool up)
        {
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                distCurve = Curves[0];
                if (pointInput.Fixed) return Recalc();
                return true;
            }
            if (feedBackAdd)
            {
                base.FeedBack.Remove(feedBackLine);
                feedBackAdd = false;
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
            distCurve = SelectedCurve;
            if (pointInput.Fixed) Recalc();
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
            return actualLength;
        }

        private double CalculateMeasureText(GeoPoint MousePosition)
        {
            return actualLength;
        }



        private double GetMeasureTextx()
        {
            if (actualLength != 0.0)
                return Math.Abs(distPoint.x - secondPoint.x);
            else return 0;
        }

        private double CalculateMeasureTextx(GeoPoint MousePosition)
        {
            if (actualLength != 0.0)
                return Math.Abs(distPoint.x - secondPoint.x);
            else return 0;
        }

        private double GetMeasureTexty()
        {
            if (actualLength != 0.0)
                return Math.Abs(distPoint.y - secondPoint.y);
            else return 0;
        }

        private double CalculateMeasureTexty(GeoPoint MousePosition)
        {
            if (actualLength != 0.0)
                return Math.Abs(distPoint.y - secondPoint.y);
            else return 0;
        }

        private double GetMeasureTextz()
        {
            if (actualLength != 0.0)
                return Math.Abs(distPoint.z - secondPoint.z);
            else return 0;
        }

        private double CalculateMeasureTextz(GeoPoint MousePosition)
        {
            if (actualLength != 0.0)
                return Math.Abs(distPoint.z - secondPoint.z);
            else return 0;
        }



        /// <inheritdoc/>
		public override void OnSetAction()
        {
            base.TitleId = "Construct.DistancePointCurve";
            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            feedBackAdd = false;

            pointInput = new GeoPointInput("Construct.DistancePointCurve.Point");
            pointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(DistPoint);

            curveInput = new CurveInput("Construct.DistancePointCurve.Object");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(DistCurve);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(DistCurveChanged);

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


            if (measure)
                base.SetInput(pointInput, curveInput, measureText, measureTextx, measureTexty, measureTextz);
            else base.SetInput(pointInput, curveInput);
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
            return "Construct.DistancePointCurve";
        }
        /// <inheritdoc/>
        public override void OnDone()
        {
            succeeded = true;
            base.OnDone();
        }
    }
}

