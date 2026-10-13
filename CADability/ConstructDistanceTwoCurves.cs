using CADability.Attribute;
using CADability.Curve2D;
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

    public class ConstructDistanceTwoCurves : ConstructAction, IIntermediateConstruction
    {
        private LengthProperty lengthProperty;
        private CurveInput curve1Input;
        private CurveInput curve2Input;
        private ICurve distCurve1;
        private ICurve distCurve2;
        private double cancelLength;
        private double actualLength;
        private Line feedBackLine;
        private bool feedBackAdd;
        private bool measure;
        private bool succeeded;

        public ConstructDistanceTwoCurves(LengthProperty lengthProperty)
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
            double distmin;
            GeoPoint2D p1, p2;
            if (Curves.GetCommonPlane(distCurve1, distCurve2, out pl))
            {
                ICurve2D curve1_2D = distCurve1.GetProjectedCurve(pl); // die 2D-Kurve
                if (curve1_2D is Path2D) (curve1_2D as Path2D).Flatten();
                ICurve2D curve2_2D = distCurve2.GetProjectedCurve(pl); // die 2D-Kurve
                if (curve2_2D is Path2D) (curve2_2D as Path2D).Flatten();
                distmin = Curves2D.SimpleMinimumDistance(curve1_2D, curve2_2D, out p1, out p2);
                if (distmin > Precision.eps)
                {
                    feedBackLine.SetTwoPoints(pl.ToGlobal(p1), pl.ToGlobal(p2));
                    if (!feedBackAdd)
                    {
                        base.FeedBack.Add(feedBackLine);
                        feedBackAdd = true;
                    }
                    using (Frame.Project.Undo.ContextFrame(this))
                    {
                        if (lengthProperty != null)
                            lengthProperty.SetLength(distmin);
                    }
                    actualLength = distmin;
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

        private bool DistCurve1(CurveInput sender, ICurve[] Curves, bool up)
        {
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                distCurve1 = Curves[0];
                if (curve2Input.Fixed) return Recalc();
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

        private void DistCurve1Changed(CurveInput sender, ICurve SelectedCurve)
        {
            distCurve1 = SelectedCurve;
            if (curve2Input.Fixed) Recalc();
        }

        private bool DistCurve2(CurveInput sender, ICurve[] Curves, bool up)
        {
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                distCurve2 = Curves[0];
                if (curve1Input.Fixed) return Recalc();
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

        private void DistCurve2Changed(CurveInput sender, ICurve SelectedCurve)
        {
            distCurve2 = SelectedCurve;
            if (curve1Input.Fixed) Recalc();
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

        /// <inheritdoc/>
		public override void OnSetAction()
        {
            base.TitleId = "Construct.DistanceTwoCurves";
            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            feedBackAdd = false;

            curve1Input = new CurveInput("Construct.DistanceTwoCurves.Object1");
            curve1Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve1Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(DistCurve1);
            curve1Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(DistCurve1Changed);

            curve2Input = new CurveInput("Construct.DistanceTwoCurves.Object2");
            curve2Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve2Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(DistCurve2);
            curve2Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(DistCurve2Changed);

            LengthInput measureText = new LengthInput("MeasureLength");
            measureText.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(CalculateMeasureText);
            measureText.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetMeasureText);

            if (measure)
                base.SetInput(curve1Input, curve2Input, measureText);
            else base.SetInput(curve1Input, curve2Input);
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
            return "Construct.DistanceTwoCurves";
        }
        /// <inheritdoc/>
        public override void OnDone()
        {
            succeeded = true;
            base.OnDone();
        }
    }
}


