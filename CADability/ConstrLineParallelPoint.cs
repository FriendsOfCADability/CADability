using CADability.Curve2D;
using CADability.GeoObject;
using System.Collections;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a line parallel to an existing line through a given point: the user picks the line (polylines are
    /// decomposed into their segments, only lines are accepted) and the point the parallel has to pass through. The result
    /// has the same length as the original line. Menu id: "Constr.Line.ParallelPoint".
    /// </summary>
    internal class ConstrLineParallelPoint : ConstructAction
    {
        private Line line;
        private GeoPoint throughPoint;
        private ICurve iCurve;
        CurveInput curveInput;
        GeoPointInput inputThroughPoint;


        public ConstrLineParallelPoint()
        { }

        private bool showLine()
        {
            Plane pl;
            if (Curves.GetCommonPlane(throughPoint, iCurve, out pl))
            {
                ICurve2D l2D = iCurve.GetProjectedCurve(pl);
                if (l2D is Path2D) (l2D as Path2D).Flatten();
                double dist = l2D.Distance(pl.Project(throughPoint));
                //			System.Diagnostics.Trace.WriteLine(dist.ToString());
                ICurve2D l2DP = l2D.Parallel(-dist, false, 0.0, 0.0);
                line.SetTwoPoints(pl.ToGlobal(l2DP.StartPoint), pl.ToGlobal(l2DP.EndPoint));
                base.ShowActiveObject = true;
                return true;
            }
            base.ShowActiveObject = false;
            return false;
        }
        private bool OnMouseOverCurves(CurveInput sender, ICurve[] Curves, bool up)
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
                if (inputThroughPoint.Fixed) return (showLine());
                return true;
            }
            base.ShowActiveObject = false;
            return false;
        }

        private void OnCurveSelectionChanged(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve = SelectedCurve;
            if (inputThroughPoint.Fixed) showLine();
        }

        private void onSetThroughPoint(GeoPoint p)
        {
            throughPoint = p;
            if (curveInput.Fixed) showLine();
        }

        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            if (base.CurrentInput == inputThroughPoint && iCurve is Line)
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
            base.TitleId = "Constr.Line.ParallelPoint";

            curveInput = new CurveInput("Constr.Line.Parallel.Object");
            curveInput.Decomposed = true; // nur Einzellinien, auch bei Polyline
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(OnMouseOverCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(OnCurveSelectionChanged);

            inputThroughPoint = new GeoPointInput("Constr.Line.Parallel.Point");
            inputThroughPoint.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(onSetThroughPoint);

            base.SetInput(curveInput, inputThroughPoint);
            base.ShowAttributes = true;

            base.OnSetAction();
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Line.ParallelPoint";
        }

        public override void OnDone()
        {
            base.OnDone();
        }
    }
}
