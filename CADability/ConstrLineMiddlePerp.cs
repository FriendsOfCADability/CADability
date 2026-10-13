using CADability.GeoObject;
using System.Collections;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of the perpendicular bisector of a line: the new line starts at the midpoint of the picked
    /// line and runs perpendicular to it in the active drawing plane. The user picks the line (only
    /// <see cref="Line"/> objects are accepted) and enters the length; the side on which the new line is drawn
    /// follows the mouse position and can be switched with the next/previous solution commands. Started
    /// with the action id "Constr.Line.MiddlePerp" (menu command "MenuId.Constr.Line.MiddlePerp").
    /// </summary>
    internal class ConstrLineMiddlePerp : ConstructAction
    {
        private Line line;
        private GeoPoint objectPoint;
        private double lineLength;
        private ICurve iCurve;
        private CurveInput curveInput;
        private LengthInput lengthInput;
        private bool nextSol = false;


        public ConstrLineMiddlePerp()
        { }

        private bool showLine()
        {
            GeoPoint startPoint = new GeoPoint(iCurve.StartPoint, iCurve.EndPoint); // Mittelpunkt
            GeoVector vPerp = (iCurve.StartDirection ^ base.ActiveDrawingPlane.Normal); // Senkrechte
            if (!vPerp.IsNullVector())
            {
                base.MultiSolutionCount = 2;
                vPerp.Norm();
                GeoPoint endPoint;
                if (nextSol)
                    endPoint = startPoint - lineLength * vPerp;
                else endPoint = startPoint + lineLength * vPerp;
                line.SetTwoPoints(startPoint, endPoint);
                // die Basis für die Längenangabe
                lengthInput.SetDistanceFromLine(iCurve.StartPoint, iCurve.EndPoint);
                base.ShowActiveObject = true;
                return true;
            }
            base.ShowActiveObject = false;
            base.MultiSolutionCount = 0;
            return false;
        }

        private bool PerpObject(CurveInput sender, ICurve[] Curves, bool up)
        {   // ... nur die sinnvolen Kurven verwenden
            ArrayList usableCurves = new ArrayList();
            for (int i = 0; i < Curves.Length; ++i)
            {
                Line l = Curves[i] as Line;
                if (l != null)
                {
                    usableCurves.Add(Curves[i]);
                }
            }
            // ...hier wird der ursprüngliche Parameter überschrieben. Hat ja keine Auswirkung nach außen.
            Curves = (ICurve[])usableCurves.ToArray(typeof(ICurve));
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                iCurve = Curves[0];
                return (showLine());
            }
            base.ShowActiveObject = false;
            return false;
        }

        private void PerpObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve = SelectedCurve;
            showLine();
        }


        public override void OnSolution(int sol)
        {
            if (sol == -1) nextSol = !nextSol;
            else
                if (sol == -2) nextSol = !nextSol;
            else nextSol = (sol & 1) != 0;
            showLine();
        }

        private bool OnSetLength(double length)
        {
            if (length > Precision.eps)
                lineLength = length;
            else return false;
            if (curveInput.Fixed)
            {
                base.MultiSolutionCount = 2;
                objectPoint = base.CurrentMousePosition;
                GeoVector v = line.EndPoint - line.StartPoint;
                if (!v.IsNullVector()) v.Norm();
                else v = new GeoVector(1.0, 0.0, 0.0);
                GeoPoint endPoint = line.StartPoint + length * v;
                GeoPoint endPointOpp = line.StartPoint - length * v;
                if (!nextSol)
                {
                    if (objectPoint.Distance(endPoint) > objectPoint.Distance(endPointOpp))
                        endPoint = endPointOpp;
                }
                else
                {
                    if (objectPoint.Distance(endPoint) < objectPoint.Distance(endPointOpp))
                        endPoint = endPointOpp;
                }
                line.EndPoint = endPoint;
                return true;
            }
            base.MultiSolutionCount = 0;
            return false;
        }
        private double OnGetLength()
        {
            return lineLength;
        }

        public override void OnSetAction()
        {
            line = Line.Construct();
            lineLength = ConstrDefaults.DefaultLineLength;
            base.ActiveObject = line;
            base.ShowActiveObject = false;
            base.TitleId = "Constr.Line.MiddlePerp";
            curveInput = new CurveInput("Constr.Line.MiddlePerp.Object");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(PerpObject);
            curveInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(PerpObjectChanged);
            lengthInput = new LengthInput("Line.Length");
            lengthInput.defaultLength = ConstrDefaults.DefaultLineLength;
            lengthInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(OnSetLength);
            lengthInput.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(OnGetLength);
            lengthInput.ForwardMouseInputTo = curveInput;
            base.SetInput(curveInput, lengthInput);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Line.MiddlePerp";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}
