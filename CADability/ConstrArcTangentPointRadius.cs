
using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ConstrArcTangentPointRadius : ConstructAction
    {

        private Ellipse arc;
        private GeoPoint2D center;
        private Angle startAngle;
        private Angle endAngle;
        private GeoPoint objectPoint;
        private GeoPoint radiusPoint;
        private GeoPoint circlePoint;
        private ICurve[] tangCurves;
        private CurveInput curveInput;
        private GeoPointInput pointInput;
        private LengthInput radInput;
        private LengthInput diamInput;
        private double arcRad;
        private bool arcDir;

        private double arcRadCalc;
        private int selected;
        private int selSol = 0;  // wg. Unsymmetrie bei 0 wg Abs

        public ConstrArcTangentPointRadius()
        { }


        private bool showLine()
        {
            ArrayList usableCurves = new ArrayList();
            double mindist = double.MaxValue;
            double arcRadLoc;
            if (tangCurves == null) return false;
            arcRadLoc = arcRad;
            for (int i = 0; i < tangCurves.Length; ++i)
            {
                Plane pl;
                if (tangCurves[i].GetPlanarState() == PlanarState.Planar)
                    pl = tangCurves[i].GetPlane();
                else
                {
                    try { pl = new Plane(tangCurves[i].StartPoint, tangCurves[i].EndPoint, circlePoint); }
                    catch (PlaneException) { break; }
                }
                if (Precision.IsPointOnPlane(circlePoint, pl))
                {
                    ICurve2D l2D1 = tangCurves[i].GetProjectedCurve(pl);
                    if (l2D1 is Path2D) (l2D1 as Path2D).Flatten();
                    GeoPoint2D[] tangentPoints = Curves2D.TangentCircle(l2D1, pl.Project(circlePoint), arcRad);
                    if (tangentPoints.Length > 0)
                    {	// eine gültige Linie ist gefunden
                        usableCurves.Add(tangCurves[i]); // zur lokalen Liste zufügen
                        for (int k = 0; k < tangentPoints.Length; k += 2)
                        {
                            double dist = Geometry.Dist(tangentPoints[k + 1], pl.Project(objectPoint));
                            if (dist < mindist)
                            {
                                mindist = dist;
                                selected = usableCurves.Count - 1; // merken, welche Kurve die aktuell benutzte ist
                                // die Punkte werden als sortiert erwartet!! Hier nur über modulo (%) den Index bestimmen
                                // selSol / 2 shifted nach rechts, um 3 Zeilen tiefer weitere Möglichkeiten auswählen zu können
                                // (/ 3) und (* 3), da pro Lösung drei Punkte geliefert werden
                                int l = (k + 2 * ((Math.Abs(selSol) / 2) % (tangentPoints.Length / 2))) % tangentPoints.Length;
                                center = tangentPoints[l];
                                if ((Math.Abs(selSol) & 0x1) == 0) // 0 und 1 dienen zur Unterscheidung der Kreisbogenausrichtung
                                {
                                    startAngle = new Angle(tangentPoints[l + 1], center);
                                    endAngle = new Angle(pl.Project(circlePoint), center);
                                }
                                else
                                {
                                    startAngle = new Angle(pl.Project(circlePoint), center);
                                    endAngle = new Angle(tangentPoints[l + 1], center);
                                }
                            }

                        }
                    }
                    if (mindist < double.MaxValue)
                    {
                        arcRadCalc = (Math.Abs(l2D1.Distance(pl.Project(radiusPoint))) + Math.Abs(Geometry.Dist(circlePoint, radiusPoint))) / 2.0;
                        //							base.MultiSolution = true;
                        base.MultiSolutionCount = tangentPoints.Length; // /2 *2
                        arc.SetArcPlaneCenterRadiusAngles(pl, pl.ToGlobal(center), arcRadLoc, startAngle, new SweepAngle(startAngle, endAngle, arcDir));
                        tangCurves = (ICurve[])usableCurves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                        base.ShowActiveObject = true;
                        return (true);


                    }
                }
            }

            base.MultiSolutionCount = 0;
            //            base.MultiSolution = false;
            base.ShowActiveObject = false;
            return (false);
        }

        private bool showLineDiam()
        {
            ArrayList usable1Curves = new ArrayList();
            Plane pl;
            double mindist = double.MaxValue;
            if (tangCurves == null) return false;
            for (int i = 0; i < tangCurves.Length; ++i)
            {
                if (tangCurves[i].GetPlanarState() == PlanarState.Planar)
                    pl = tangCurves[i].GetPlane();
                else
                { // Element hat keine Ebene: Eine machen, falls möglich!
                    try { pl = new Plane(tangCurves[i].StartPoint, tangCurves[i].EndPoint, circlePoint); }
                    catch (PlaneException) { break; }
                }
                if (Precision.IsPointOnPlane(radiusPoint, pl))
                {
                    ICurve2D l2D1 = tangCurves[i].GetProjectedCurve(pl);
                    ICurve2D l2D2 = new Circle2D(pl.Project(circlePoint), 0.0);
                    ICurve2D l2D3 = new Circle2D(pl.Project(radiusPoint), 0.0);
                    GeoPoint2D[] tangentPoints = Curves2D.TangentCircle(l2D1, l2D2, l2D3, pl.Project(objectPoint), pl.Project(circlePoint), pl.Project(radiusPoint));
                    if (tangentPoints.Length > 0)
                    {	// eine gültige Linie ist gefunden
                        usable1Curves.Add(tangCurves[i]); // zur lokalen Liste zufügen
                        for (int l = 0; l < tangentPoints.Length; l += 4)
                        {
                            double dist = Geometry.Dist(tangentPoints[l + 1], pl.Project(objectPoint)) +
                                    Geometry.Dist(tangentPoints[l + 2], pl.Project(circlePoint)) +
                                    Geometry.Dist(tangentPoints[l + 3], pl.Project(radiusPoint));
                            if (dist < mindist)
                            {
                                mindist = dist;
                                selected = usable1Curves.Count - 1; // merken, welche Kurven die aktuell benutzten sind
                                // selSol / 6 entspricht div 6, um einige Zeilen tiefer weitere 6 Möglichkeiten auswählen zu können
                                // (/ 4) und (* 4), da pro Lösung vier Punkte geliefert werden
                                int m = (l + 4 * (Math.Abs(selSol) % (tangentPoints.Length / 4))) % tangentPoints.Length;
                                center = tangentPoints[m];
                                arcRadCalc = Geometry.Dist(tangentPoints[m + 1], center) * 2.0;
                            }

                        }
                        if (mindist < double.MaxValue)
                        {
                            //										base.MultiSolution = true;
                            base.MultiSolutionCount = tangentPoints.Length / 4;
                            arc.SetCirclePlaneCenterRadius(pl, pl.ToGlobal(center), arcRad);
                            tangCurves = (ICurve[])usable1Curves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                            base.ShowActiveObject = true;
                            return (true);
                        }
                    }
                }
            }
            base.ShowActiveObject = false;
            base.MultiSolutionCount = 0;
            //            base.MultiSolution = false;
            return (false);
        }



        private bool inputTangCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint = base.CurrentMousePosition;
            selected = 0;
            tangCurves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            if (!pointInput.Fixed)
                s = (tangCurves.Length > 0);
            else s = (showLine());
            if (up)
            {
                if (tangCurves.Length == 0) sender.SetCurves(tangCurves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(tangCurves, tangCurves[selected]);
            }
            if (s) return true;
            //            base.MultiSolution = false;
            base.MultiSolutionCount = 0;
            base.ShowActiveObject = false;
            return false;
        }

        private void inputTangCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            tangCurves = new ICurve[] { SelectedCurve };
            if (pointInput.Fixed)
            { showLine(); }

        }

        private void CirclePoint(GeoPoint p)
        {
            circlePoint = p;
            if (curveInput.Fixed)
            {
                if (!radInput.Fixed && !diamInput.Fixed)
                { // Radius sinnvoll vorbesetzen
                    arcRad = Math.Abs(Geometry.Dist(p, objectPoint));
                }
                showLine();
            }
        }

        private bool CircRadius(double rad)
        {
            if (rad > Precision.eps)
            {
                arcRad = rad;
                if (curveInput.Fixed && pointInput.Fixed) return showLine();
                return true;
            }
            return false;
        }
        private double radInput_OnCalculateLength(GeoPoint MousePosition)
        {
            radiusPoint = MousePosition;
            if (showLine()) return arcRadCalc;
            return arcRad;
        }

        private double GetRadius()
        {
            return arcRad;
        }

        private bool SetArcDiameter(double rad)
        {
            if (rad > Precision.eps)
            {
                arcRad = rad / 2.0;
                if (curveInput.Fixed && pointInput.Fixed) return showLine();
                return true;
            }
            return false;
        }

        private double diamInput_OnCalculateLength(GeoPoint MousePosition)
        {
            radiusPoint = MousePosition;
            if (showLineDiam()) return arcRadCalc;
            return arcRad;
        }

        private double GetArcDiameter()
        {
            return arcRad * 2.0;
        }

        protected override void InputChanged(object activeInput)
        {
            if (activeInput == diamInput)
            {
                radInput.Optional = true;
                diamInput.Optional = false;
            };
            if (activeInput == radInput)
            {
                radInput.Optional = false;
                diamInput.Optional = true;
            };
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
            Boolean useRadius = Frame.GetBooleanSetting("Formatting.Radius", true);
            radiusPoint = new GeoPoint(0, 0);
            arc = Ellipse.Construct();
            arcDir = ConstrDefaults.DefaultArcDirection;
            double dir;
            if (arcDir)
                dir = 1.5 * Math.PI;
            else dir = -1.5 * Math.PI;
            arcRad = ConstrDefaults.DefaultArcRadius;
            arc.SetArcPlaneCenterRadiusAngles(base.ActiveDrawingPlane, ConstrDefaults.DefaultStartPoint, ConstrDefaults.DefaultArcRadius, 0.0, dir);
            base.ActiveObject = arc;
            base.ShowActiveObject = false;
            base.TitleId = "Constr.Arc.TangentPointRadius";

            curveInput = new CurveInput("Constr.Arc.Tangent.Object");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(inputTangCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(inputTangCurvesChanged);

            pointInput = new GeoPointInput("Constr.Arc.Point");
            pointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint);

            radInput = new LengthInput("Constr.Arc.Radius");
            radInput.DefaultLength = ConstrDefaults.DefaultArcRadius;
            radInput.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(radInput_OnCalculateLength);
            radInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(CircRadius);
            radInput.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetRadius);
            radInput.ForwardMouseInputTo = new object[] { curveInput, pointInput };
            if (!useRadius) { radInput.Optional = true; }

            diamInput = new LengthInput("Constr.Circle.Diameter");
            diamInput.DefaultLength = ConstrDefaults.DefaultArcRadius;
            diamInput.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(diamInput_OnCalculateLength);
            diamInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(SetArcDiameter);
            diamInput.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetArcDiameter);
            diamInput.ForwardMouseInputTo = new object[] { curveInput, pointInput };
            if (useRadius) { diamInput.Optional = true; }

            if (useRadius)
            { base.SetInput(curveInput, pointInput, radInput, diamInput); }
            else
            { base.SetInput(curveInput, pointInput, diamInput, radInput); }

            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Arc.TangentPointRadius";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}


