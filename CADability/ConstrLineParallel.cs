using CADability.Curve2D;
using CADability.GeoObject;
using System.Collections;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a line parallel to an existing line at a given distance. The user selects a line (segments of
    /// polylines and paths are accepted as single lines) and enters the distance; the side on which the parallel is
    /// created is chosen by the mouse position or by switching to the other solution. The new line has the same
    /// length as the original and lies in the plane through the original line perpendicular to the active drawing plane.
    /// Menu id: "Constr.Line.ParallelDist".
    /// </summary>
    internal class ConstrLineParallel : ConstructAction
    {
        private Line line;
        private double parallelDist;
        private ICurve iCurve;
        private LengthInput lengthInput;
        private CurveInput curveInput;
        private bool nextSol = false;

        public ConstrLineParallel()
        {

        }
        private bool showLine()
        {	// Linie ist nicht senkrecht auf der ActiveDrawingPlane:
            if (!Precision.SameDirection(base.ActiveDrawingPlane.Normal, iCurve.StartDirection, false))
            {	// Ebene senkrecht auf ActiveDrawingPlane und Linie
                base.MultiSolutionCount = 2;
                Plane pl = new Plane(iCurve.StartPoint, iCurve.StartDirection, (base.ActiveDrawingPlane.Normal ^ iCurve.StartDirection));
                ICurve2D l2D = iCurve.GetProjectedCurve(pl);
                if (l2D is Path2D) (l2D as Path2D).Flatten();
                ICurve2D l2DP;
                if (nextSol)
                    l2DP = l2D.Parallel(parallelDist, false, 0.0, 0.0);
                else l2DP = l2D.Parallel(-parallelDist, false, 0.0, 0.0);
                line.SetTwoPoints(pl.ToGlobal(l2DP.StartPoint), pl.ToGlobal(l2DP.EndPoint));
                // Ebene senkrecht auf der Parallelen-Ebene
                Plane plDist = new Plane(iCurve.StartPoint, pl.Normal, iCurve.StartDirection);
                lengthInput.SetDistanceFromPlane(plDist);
                base.ShowActiveObject = true;
                return true;
            }
            base.MultiSolutionCount = 0;
            base.ShowActiveObject = false;
            return false;
        }

        private bool OnMouseOverCurves(CurveInput sender, ICurve[] Curves, bool up)
        {	// ... nur die sinnvolen Kurven verwenden
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
            {
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
                lengthInput.ForwardMouseInputTo = null;
            }
            if (Curves.Length > 0)
            {
                iCurve = Curves[0];
                return (showLine());
            }
            base.ShowActiveObject = false;
            return false;
        }

        private void OnCurveSelectionChanged(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve = SelectedCurve;
            showLine();
        }

        private bool OnSetLength(double Length)
        {
            if (Length != 0.0)
            {
                parallelDist = Length;
                if (curveInput.Fixed) return (showLine());
                else lengthInput.ForwardMouseInputTo = curveInput;
                return true;
            }
            return false;
        }

        public override void OnSolution(int sol)
        {
            if (sol == -1) nextSol = !nextSol;
            else
                if (sol == -2) nextSol = !nextSol;
            else nextSol = (sol & 1) != 0;
            showLine();
        }

        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            if (base.CurrentInput == lengthInput && iCurve is Line)
            {
                GeoObjectList l = base.GetObjectsUnderCursor(e.Location);
                l.DecomposeAll();
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] is ICurve)
                    {
                        double[] tanpos = (l[i] as ICurve).TangentPosition(iCurve.StartDirection);
                        if (tanpos != null)
                        {
                            for (int j = 0; j < tanpos.Length; j++)
                            {
                                GeoPoint p = (l[i] as ICurve).PointAt(tanpos[j]);
                                double d = base.WorldPoint(e.Location) | p;
                                if (d < mindist)
                                {
                                    mindist = d;
                                    found = p;
                                }
                            }
                        }
                    }
                }
            }
            return mindist != double.MaxValue;
        }

        public override void OnSetAction()
        {
            line = Line.Construct();
            base.ActiveObject = line;
            base.ShowActiveObject = false;
            parallelDist = ConstrDefaults.DefaultLineDist;

            base.TitleId = "Constr.Line.ParallelDist";
            curveInput = new CurveInput("Constr.Line.Parallel.Object");
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(OnMouseOverCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(OnCurveSelectionChanged);
            curveInput.Decomposed = true; // nur Einzellinien, auch bei Polyline
            lengthInput = new LengthInput("Constr.Line.Parallel.Dist");
            lengthInput.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(OnSetLength);
            lengthInput.DefaultLength = ConstrDefaults.DefaultLineDist;
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
            return "Constr.Line.ParallelDist";
        }

        public override void OnDone()
        {
            base.OnDone();
        }
    }
}




