using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a line that is tangent to two curves: the user picks the two curves (polylines and paths are
    /// decomposed into their segments), which must lie in a common plane. Of the possible tangent lines the one closest to
    /// the pick positions is shown, the others can be selected with the "next solution" command.
    /// Menu id: "Constr.Line.TwoTangents".
    /// </summary>
    internal class ConstrLine2Tangents : ConstructAction
    {

        private Line line;
        private GeoPoint2D startPoint;
        private GeoPoint2D endPoint;
        private GeoPoint objectPoint;
        private GeoPoint object2Point;
        private ICurve[] tangCurves;
        private ICurve[] tang2Curves;
        private CurveInput curveInput;
        private CurveInput curve2Input;
        private int selected;
        private int selected2;
        private int selSol = 0;  // wg. Unsymmetrie bei 0 wg Abs

        public ConstrLine2Tangents()
        { }

        private bool showLine()
        {
            ArrayList usableCurves = new ArrayList();
            ArrayList usable2Curves = new ArrayList();
            double mindist = double.MaxValue;
            for (int i = 0; i < tangCurves.Length; ++i)
            {
                for (int j = 0; j < tang2Curves.Length; ++j)
                {
                    Plane pl;
                    if (Curves.GetCommonPlane(tangCurves[i], tang2Curves[j], out pl))
                    {
                        ICurve2D l2D1 = tangCurves[i].GetProjectedCurve(pl);
                        if (l2D1 is Path2D) (l2D1 as Path2D).Flatten();
                        ICurve2D l2D2 = tang2Curves[j].GetProjectedCurve(pl);
                        if (l2D2 is Path2D) (l2D2 as Path2D).Flatten();
                        GeoPoint2D[] tangentPoints = Curves2D.TangentLines(l2D1, l2D2);
                        if (tangentPoints.Length > 0)
                        {   // eine gültige Linie ist gefunden
                            usableCurves.Add(tangCurves[i]); // zur lokalen Liste zufügen
                            usable2Curves.Add(tang2Curves[j]); // zur lokalen Liste zufügen
                            for (int k = 0; k < tangentPoints.Length; k += 2)
                            {
                                double dist = Geometry.Dist(tangentPoints[k], pl.Project(objectPoint)) +
                                    Geometry.Dist(tangentPoints[k + 1], pl.Project(object2Point));
                                if (dist < mindist)
                                {
                                    mindist = dist;
                                    selected = usableCurves.Count - 1; // merken, welche Kurve die aktuell benutzte ist
                                    selected2 = usable2Curves.Count - 1; // merken, welche Kurve die aktuell benutzte ist
                                                                         // die Punkte werden als sortiert erwartet!! Hier nur über modulo (%) den Index bestimmen
                                    int l = (k + 2 * (Math.Abs(selSol) % (tangentPoints.Length / 2))) % tangentPoints.Length;
                                    startPoint = tangentPoints[l];
                                    endPoint = tangentPoints[l + 1];
                                }
                            }
                        }
                        if (mindist < double.MaxValue)
                        {
                            //							base.MultiSolution = true;
                            base.MultiSolutionCount = tangentPoints.Length / 2;
                            base.ShowActiveObject = true;
                            line.SetTwoPoints(pl.ToGlobal(startPoint), pl.ToGlobal(endPoint));
                            tangCurves = (ICurve[])usableCurves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                            tang2Curves = (ICurve[])usable2Curves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                            return (true);
                        }
                    }
                }
            }
            base.ShowActiveObject = false;
            base.MultiSolution = false;
            return (false);
        }

        private bool inputTangCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint = base.CurrentMousePosition;
            selected = 0;
            tangCurves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            if (!curve2Input.Fixed)
            {   // hier wird hilfsweise ein kleinkreis erzeugt, um gültige Curves zu bestimmen
                Ellipse circLoc;
                circLoc = Ellipse.Construct();
                circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, objectPoint, base.WorldLength(1));
                tang2Curves = new ICurve[] { circLoc };
            }
            if (tangCurves.Length == 0)
            {
                Ellipse circLoc;
                circLoc = Ellipse.Construct();
                circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, objectPoint, base.WorldLength(1));
                tangCurves = new ICurve[] { circLoc };
                showLine();
            }
            else s = (showLine());
            if (up)
            {
                if (tangCurves.Length == 0) sender.SetCurves(tangCurves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(tangCurves, tangCurves[selected]);
            }
            if (s) return true;
            return false;
        }

        private void inputTangCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            tangCurves = new ICurve[] { SelectedCurve };
            if (curve2Input.Fixed) { showLine(); }

        }
        private bool input2TangCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            object2Point = base.CurrentMousePosition;
            selected2 = 0;
            tang2Curves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            if (!curveInput.Fixed)
            {   // hier wird hilfsweise ein kleinkreis erzeugt, um gültige Curves zu bestimmen
                Ellipse circLoc;
                circLoc = Ellipse.Construct();
                circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, object2Point, base.WorldLength(1));
                tangCurves = new ICurve[] { circLoc };
            }
            if (tang2Curves.Length == 0)
            {
                Ellipse circLoc;
                circLoc = Ellipse.Construct();
                circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, object2Point, base.WorldLength(1));
                tang2Curves = new ICurve[] { circLoc };
                showLine();
            }
            else s = (showLine());
            if (up)
            {
                if (tang2Curves.Length == 0) sender.SetCurves(tang2Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(tang2Curves, tang2Curves[selected2]);
            }
            if (s) return true;
            return false;
        }

        private void input2TangCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            tang2Curves = new ICurve[] { SelectedCurve };
            if (curveInput.Fixed) { showLine(); }

        }

        public override void OnSolution(int sol)
        {
            if (sol == -1) selSol += 1;
            else
                if (sol == -2) selSol -= 1;
            else selSol = sol;
            showLine();
        }

        public override void OnSetAction()
        {
            line = Line.Construct();
            base.ActiveObject = line;
            base.ShowActiveObject = false;
            base.TitleId = "Constr.Line.TwoTangents";
            curveInput = new CurveInput("Constr.Line.Tangent.Object");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(inputTangCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(inputTangCurvesChanged);
            curve2Input = new CurveInput("Constr.Line.Tangent.Object2");
            curve2Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve2Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(input2TangCurves);
            curve2Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(input2TangCurvesChanged);
            base.SetInput(curveInput, curve2Input);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Line.TwoTangents";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}
