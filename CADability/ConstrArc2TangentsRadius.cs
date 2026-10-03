using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ConstrArc2TangentsRadius : ConstructAction
    {

        private Ellipse arc;
        private GeoPoint2D center;
        private Angle startAngle;
        private Angle endAngle;
        private GeoPoint objectPoint;
        private GeoPoint object2Point;
        private GeoPoint radiusPoint;
        private ICurve[] tangCurves;
        private ICurve[] tang2Curves;
        private CurveInput curveInput;
        private CurveInput curve2Input;
        private LengthInput radInput;
        private LengthInput diamInput;
        private double arcRad;
        private double arcRadCalc;
        private int selected;
        private int selected2;
        private int selSol = 0;  // wg. Unsymmetrie bei 0 wg Abs
        private bool arcDir;

        public ConstrArc2TangentsRadius()
        { }

        private bool showLine()
        {
            ArrayList usableCurves = new ArrayList();
            ArrayList usable2Curves = new ArrayList();
            double mindist = double.MaxValue;
            double arcRadLoc;
            if (tangCurves == null) return false;
            if (tang2Curves == null) return false;
            //			if (!radInput.Fixed) arcRadLoc = Math.Max(arcRad,Geometry.dist(objectPoint,object2Point)/2);
            //			else arcRadLoc = arcRad;
            arcRadLoc = arcRad;

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
                        GeoPoint2D[] tangentPoints = Curves2D.TangentCircle(l2D1, l2D2, arcRadLoc);
                        if (tangentPoints.Length > 0)
                        {   // eine gültige Linie ist gefunden
                            usableCurves.Add(tangCurves[i]); // zur lokalen Liste zufügen
                            usable2Curves.Add(tang2Curves[j]); // zur lokalen Liste zufügen
                            for (int k = 0; k < tangentPoints.Length; k += 3)
                            {
                                double dist = Geometry.Dist(tangentPoints[k + 1], pl.Project(objectPoint)) +
                                    Geometry.Dist(tangentPoints[k + 2], pl.Project(object2Point));
                                if (dist < mindist)
                                {
                                    mindist = dist;
                                    selected = usableCurves.Count - 1; // merken, welche Kurve die aktuell benutzte ist
                                    selected2 = usable2Curves.Count - 1; // merken, welche Kurve die aktuell benutzte ist
                                                                         // die Punkte werden als sortiert erwartet!! Hier nur über modulo (%) den Index bestimmen
                                                                         // selSol / 2 shifted nach rechts, um 3 Zeilen tiefer weitere Möglichkeiten auswählen zu können
                                                                         // (/ 3) und (* 3), da pro Lösung drei Punkte geliefert werden
                                    int l = (k + 3 * ((Math.Abs(selSol) / 2) % (tangentPoints.Length / 3))) % tangentPoints.Length;
                                    center = tangentPoints[l];
                                    if ((Math.Abs(selSol) & 0x1) == 0) // 0 und 1 dienen zur Unterscheidung der Kreisbogenausrichtung
                                    {
                                        startAngle = new Angle(tangentPoints[l + 1], center);
                                        endAngle = new Angle(tangentPoints[l + 2], center);
                                    }
                                    else
                                    {
                                        startAngle = new Angle(tangentPoints[l + 2], center);
                                        endAngle = new Angle(tangentPoints[l + 1], center);
                                    }
                                }

                            }
                        }
                        if (mindist < double.MaxValue)
                        {
                            arcRadCalc = (Math.Abs(l2D1.Distance(pl.Project(radiusPoint))) + Math.Abs(l2D2.Distance(pl.Project(radiusPoint)))) / 2.0;
                            //							base.MultiSolution = true;
                            base.MultiSolutionCount = (tangentPoints.Length / 3) * 2;
                            if (base.ActiveObject == null) base.ActiveObject = arc;
                            //							if (Math.Abs(new SweepAngle(startAngle,endAngle,arcDir)) > (Math.Abs(new SweepAngle(endAngle,startAngle,!arcDir))))
                            //							arc.SetArcPlaneCenterRadiusAngles(base.ActiveDrawingPlane,pl.ToGlobal(center),arcRadLoc,startAngle,new SweepAngle(startAngle,endAngle,arcDir));
                            arc.SetArcPlaneCenterRadiusAngles(pl, pl.ToGlobal(center), arcRadLoc, startAngle, new SweepAngle(startAngle, endAngle, arcDir));
                            tangCurves = (ICurve[])usableCurves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                            tang2Curves = (ICurve[])usable2Curves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                            base.ShowActiveObject = true;
                            return (true);
                        }
                    }
                }
            }
            base.ShowActiveObject = false;
            //			base.MultiSolution = false;
            base.MultiSolutionCount = 0;
            return (false);
        }


        private bool showLineDiam()
        {
            ArrayList usable1Curves = new ArrayList();
            ArrayList usable2Curves = new ArrayList();
            Plane pl;
            double mindist = double.MaxValue;
            if (tangCurves == null) return false;
            if (tang2Curves == null) return false;
            //			if (tang3Curves == null) return false;
            for (int i = 0; i < tangCurves.Length; ++i)
            {
                for (int j = 0; j < tang2Curves.Length; ++j)
                {
                    if (Curves.GetCommonPlane(tangCurves[i], tang2Curves[j], out pl))
                    {
                        if (Precision.IsPointOnPlane(radiusPoint, pl))
                        {
                            ICurve2D l2D1 = tangCurves[i].GetProjectedCurve(pl);
                            ICurve2D l2D2 = tang2Curves[j].GetProjectedCurve(pl);
                            ICurve2D l2D3 = new Circle2D(pl.Project(radiusPoint), 0.0);
                            GeoPoint2D[] tangentPoints = Curves2D.TangentCircle(l2D1, l2D2, l2D3, pl.Project(objectPoint), pl.Project(object2Point), pl.Project(radiusPoint));
                            if (tangentPoints.Length > 0)
                            {	// eine gültige Linie ist gefunden
                                //if (curve1Input.Fixed & curve2Input.Fixed & (tangentPoints.Length > 4)) 
                                //{
                                //        int debug = 0;
                                //}
                                usable1Curves.Add(tangCurves[i]); // zur lokalen Liste zufügen
                                usable2Curves.Add(tang2Curves[j]); // zur lokalen Liste zufügen
                                for (int l = 0; l < tangentPoints.Length; l += 4)
                                {
                                    double dist = Geometry.Dist(tangentPoints[l + 1], pl.Project(objectPoint)) +
                                        Geometry.Dist(tangentPoints[l + 2], pl.Project(object2Point)) +
                                        Geometry.Dist(tangentPoints[l + 3], pl.Project(radiusPoint));
                                    if (dist < mindist)
                                    {
                                        mindist = dist;
                                        selected = usable1Curves.Count - 1; // merken, welche Kurven die aktuell benutzten sind
                                        selected2 = usable2Curves.Count - 1;
                                        // selSol / 6 entspricht div 6, um einige Zeilen tiefer weitere 6 Möglichkeiten auswählen zu können
                                        // (/ 4) und (* 4), da pro Lösung vier Punkte geliefert werden
                                        int m = (l + 4 * (Math.Abs(selSol) % (tangentPoints.Length / 4))) % tangentPoints.Length;
                                        center = tangentPoints[m];
                                        arcRadCalc = Geometry.Dist(tangentPoints[m + 1], center) * 2.0;
                                    }

                                }
                            }
                            if (mindist < double.MaxValue)
                            {
                                //										base.MultiSolution = true;
                                base.MultiSolutionCount = tangentPoints.Length / 4;
                                arc.SetCirclePlaneCenterRadius(pl, pl.ToGlobal(center), arcRad);
                                tangCurves = (ICurve[])usable1Curves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                                tang2Curves = (ICurve[])usable2Curves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                                base.ShowActiveObject = true;
                                return (true);
                            }
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
            if (!curve2Input.Fixed)
                s = (tangCurves.Length > 0);
            else s = (showLine());
            //if (!curve2Input.Fixed) 
            //{	// hier wird hilfsweise ein kleiner kreis erzeugt, um gültige Curves zu bestimmen
            //    Ellipse circLoc;
            //    circLoc =  Ellipse.Construct();
            //    circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane,objectPoint,base.WorldLength(1));
            //    tang2Curves = new ICurve []{circLoc};
            //}
            //s = (showLine());
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
            if (curve2Input.Fixed)
            { showLine(); }

        }
        private bool input2TangCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            object2Point = base.CurrentMousePosition;
            selected2 = 0;
            tang2Curves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            if (!curveInput.Fixed)
                s = (tang2Curves.Length > 0);
            else s = (showLine());
            //if (curveInput.Fixed) 
            //{

            //    if (tang2Curves.Length == 0)
            //    {	// hier wird hilfsweise ein kleinkreis erzeugt, um einfach eine Liniendarstellung zu erhalten
            //        Ellipse circLoc;
            //        circLoc =  Ellipse.Construct();
            //        circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane,object2Point,base.WorldLength(1));
            //        tang2Curves = new ICurve []{circLoc};
            //        showLine();
            //    }
            //    else s = (showLine());
            //}
            if (up)
            {
                if (tang2Curves.Length == 0) sender.SetCurves(tang2Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(tang2Curves, tang2Curves[selected2]);
            }
            if (s) return true;
            //            base.MultiSolution = false;
            base.MultiSolutionCount = 0;
            base.ShowActiveObject = false;
            return false;
        }

        private void input2TangCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            tang2Curves = new ICurve[] { SelectedCurve };
            if (curveInput.Fixed)
            { showLine(); }

        }

        private bool ArcRadius(double rad)
        {
            if (rad > Precision.eps)
            {
                arcRad = rad;
                if (curveInput.Fixed & curve2Input.Fixed) return showLine();
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

        private bool SetArcDiameter(double Length)
        {
            if (Length > Precision.eps)
            {
                arcRad = Length / 2.0;
                if (curveInput.Fixed & curve2Input.Fixed) return showLine();
                return true;
            }
            else return false;
        }
        private double GetArcDiameter()
        {
            return arcRad * 2.0;
        }

        double CalculateDiameter(GeoPoint MousePosition)
        {
            radiusPoint = MousePosition;
            if (showLineDiam()) return arcRadCalc;
            return arcRad;
        }



        public override void OnSolution(int sol)
        {
            if (sol == -1) selSol += 1;
            else
                if (sol == -2) selSol -= 1;
            else selSol = sol;
            showLine();
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
            base.TitleId = "Constr.Arc.TwoTangentsRadius";

            curveInput = new CurveInput("Constr.Arc.Tangent.Object");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(inputTangCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(inputTangCurvesChanged);
            curve2Input = new CurveInput("Constr.Arc.Tangent.Object2");
            curve2Input.Decomposed = true; //  nur Einzelelemente, auch bei Polyline und Pfad
            curve2Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(input2TangCurves);
            curve2Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(input2TangCurvesChanged);
            radInput = new LengthInput("Constr.Arc.Radius");
            radInput.DefaultLength = ConstrDefaults.DefaultArcRadius;
            radInput.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(radInput_OnCalculateLength);
            radInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(ArcRadius);
            radInput.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetRadius);
            radInput.ForwardMouseInputTo = new object[] { curveInput, curve2Input };
            if (!useRadius) { radInput.Optional = true; }

            diamInput = new LengthInput("Constr.Circle.Diameter");
            diamInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(SetArcDiameter);
            diamInput.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetArcDiameter);
            diamInput.CalculateLengthEvent += new LengthInput.CalculateLengthDelegate(CalculateDiameter);
            if (useRadius) { diamInput.Optional = true; }
            diamInput.DefaultLength = ConstrDefaults.DefaultArcDiameter;
            diamInput.ForwardMouseInputTo = new object[] { curveInput, curve2Input };


            if (useRadius)
            { base.SetInput(curveInput, curve2Input, radInput, diamInput); }
            else
            { base.SetInput(curveInput, curve2Input, diamInput, radInput); }

            base.ShowActiveObject = false;
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Arc.TwoTangentsRadius";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}
