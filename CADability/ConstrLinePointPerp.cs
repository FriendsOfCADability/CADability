using CADability.Curve2D;
using CADability.GeoObject;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a line from a point perpendicular onto a curve. The user picks the start point and the curve (polylines and
    /// paths are decomposed into their single elements); the line ends at the perpendicular foot point on the curve which is closest
    /// to the pick position. For a circle or arc whose center is the start point, every point is a foot point, so the point closest
    /// to the mouse position is used. Started with the menu id "Constr.Line.PointPerp".
    /// </summary>
    internal class ConstrLinePointPerp : ConstructAction
    {
        private Line line;
        private GeoPoint startPoint;
        private GeoPoint2D endPoint;
        private GeoPoint objectPoint;
        private ICurve[] perpCurves;
        CurveInput curveInput;
        GeoPointInput inputStartPoint;
        int selected;


        public ConstrLinePointPerp()
        {
        }
        private bool showLine()
        {
            ArrayList usableCurves = new ArrayList(); // lokales Array, das die gültigen Kurven sammelt
            double mindist = double.MaxValue;
            for (int i = 0; i < perpCurves.Length; ++i)
            {
                Plane pl;
                if (Curves.GetCommonPlane(startPoint, perpCurves[i], out pl))
                {
                    ICurve2D l2D = perpCurves[i].GetProjectedCurve(pl);
                    if (l2D is Path2D) (l2D as Path2D).Flatten();
                    GeoPoint2D[] perpPoints = l2D.PerpendicularFoot(pl.Project(startPoint));
                    if (perpPoints.Length > 0)
                    {	// eine gültige Kurve ist gefunden
                        usableCurves.Add(perpCurves[i]);
                        for (int j = 0; j < perpPoints.Length; ++j)
                        {
                            //							double dist = Geometry.Dist(perpPoints[j],pl.Project(startPoint));
                            double dist = Geometry.Dist(perpPoints[j], pl.Project(objectPoint));
                            if (dist < mindist)
                            {
                                mindist = dist;
                                selected = usableCurves.Count - 1; // merken, welche Kurve die aktuell benutzte ist
                                endPoint = perpPoints[j];
                            }
                        }
                    }
                    else
                    {   // beim Kreis oder Bogen vom Mittelpunkt aus gilt jeder Punkt
                        if (l2D is Circle2D)
                        {
                            if (Precision.IsEqual((l2D as Circle2D).Center, pl.Project(startPoint)))
                            {
                                GeoPoint2D pp = l2D.PointAt(l2D.PositionOf(pl.Project(objectPoint)));
                                double dist = Geometry.Dist(pp, pl.Project(objectPoint));
                                if (dist < mindist)
                                {
                                    mindist = dist;
                                    selected = usableCurves.Count - 1; // merken, welche Kurve die aktuell benutzte ist
                                    endPoint = pp;
                                }
                            }
                        }
                    }
                }
                if (mindist < double.MaxValue)
                {
                    line.SetTwoPoints(startPoint, pl.ToGlobal(endPoint));
                    perpCurves = (ICurve[])usableCurves.ToArray(typeof(ICurve)); // perpCurves wird mit den gültigen überschrieben und unten zur Anzeige zurückgegeben an den Sender
                    base.ShowActiveObject = true;
                    return (true);
                }
            }
            line.SetTwoPoints(startPoint, objectPoint);
            base.ShowActiveObject = true;
            return (false);
        }
        private bool inputPerpCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint = base.CurrentMousePosition;
            selected = 0;
            perpCurves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            if (inputStartPoint.Fixed)
            { s = (showLine()); }
            else
                if (perpCurves.Length > 0)
                s = true;
            if (up)
            {
                if (perpCurves.Length == 0) sender.SetCurves(perpCurves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(perpCurves, Curves[selected]);
            }
            if (s) return true;
            return false;
        }

        private void inputPerpCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            perpCurves = new ICurve[] { SelectedCurve };
            if (inputStartPoint.Fixed)
            { showLine(); }

        }

        private void onSetStartPoint(GeoPoint p)
        {
            startPoint = p;
            if (curveInput.Fixed) showLine();
        }

        public override void OnSetAction()
        {
            line = Line.Construct();
            startPoint = ConstrDefaults.DefaultStartPoint;
            base.ActiveObject = line;
            base.ShowActiveObject = false;

            base.TitleId = "Constr.Line.PointPerp";

            inputStartPoint = new GeoPointInput("Line.StartPoint");
            inputStartPoint.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(onSetStartPoint);
            inputStartPoint.DefaultGeoPoint = ConstrDefaults.DefaultStartPoint;

            curveInput = new CurveInput("Constr.Line.Perp.Object");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(inputPerpCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(inputPerpCurvesChanged);
            base.SetInput(inputStartPoint, curveInput);
            base.ShowAttributes = true;

            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Line.PointPerp";
        }

        public override void OnDone()
        {
            base.OnDone();
        }
    }

}
