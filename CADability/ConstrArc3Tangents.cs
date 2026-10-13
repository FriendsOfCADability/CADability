using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a circular arc which is tangent to three curves. The user picks the three tangent objects (polylines and
    /// paths are decomposed into their single elements); the radius is computed and only displayed. The arc spans between two of
    /// the three tangent points. Which part of the circle is used and which of the possible circles is taken is chosen with the
    /// multi-solution mechanism, starting with the solution closest to the pick positions. Started with the menu id
    /// "Constr.Arc.ThreeTangents".
    /// </summary>
    internal class ConstrArc3Tangents : ConstructAction
    {

        private Ellipse arc;
        private GeoPoint2D center;
        private Angle startAngle;
        private Angle endAngle;
        private GeoPoint object1Point;
        private GeoPoint object2Point;
        private GeoPoint object3Point;
        private ICurve[] tang1Curves;
        private ICurve[] tang2Curves;
        private ICurve[] tang3Curves;
        private CurveInput curve1Input;
        private CurveInput curve2Input;
        private CurveInput curve3Input;
        private double arcRad;
        private int selected1;
        private int selected2;
        private int selected3;
        private int selSol = 0;  // wg. Unsymmetrie bei 0 wg Abs
        private bool arcDir;

        public ConstrArc3Tangents()
        { }


        private bool showLine()
        {
            ArrayList usable1Curves = new ArrayList();
            ArrayList usable2Curves = new ArrayList();
            ArrayList usable3Curves = new ArrayList();
            Plane pl;
            Plane pl1;
            double mindist = double.MaxValue;
            int selSolLoc;
            if (tang1Curves == null) return false;
            if (tang2Curves == null) return false;
            //			if (tang3Curves == null) return false;
            for (int i = 0; i < tang1Curves.Length; ++i)
            {
                for (int j = 0; j < tang2Curves.Length; ++j)
                {
                    if (Curves.GetCommonPlane(tang1Curves[i], tang2Curves[j], out pl))
                    {
                        if (tang3Curves == null) return true; // bei zwei bestimmten: sind in der gleichen Ebene
                        for (int k = 0; k < tang3Curves.Length; ++k)
                        {
                            if (Curves.GetCommonPlane(tang1Curves[i], tang3Curves[k], out pl1))
                            {
                                if (Precision.IsEqual(pl, pl1))
                                {
                                    ICurve2D l2D1 = tang1Curves[i].GetProjectedCurve(pl);
                                    if (l2D1 is Path2D) (l2D1 as Path2D).Flatten();
                                    ICurve2D l2D2 = tang2Curves[j].GetProjectedCurve(pl);
                                    if (l2D2 is Path2D) (l2D2 as Path2D).Flatten();
                                    ICurve2D l2D3 = tang3Curves[k].GetProjectedCurve(pl);
                                    if (l2D3 is Path2D) (l2D3 as Path2D).Flatten();
                                    GeoPoint2D[] tangentPoints = Curves2D.TangentCircle(l2D1, l2D2, l2D3, pl.Project(object1Point), pl.Project(object2Point), pl.Project(object3Point));
                                    if (tangentPoints.Length > 0)
                                    {   // eine gültige Linie ist gefunden
                                        usable1Curves.Add(tang1Curves[i]); // zur lokalen Liste zufügen
                                        usable2Curves.Add(tang2Curves[j]); // zur lokalen Liste zufügen
                                        usable3Curves.Add(tang3Curves[k]); // zur lokalen Liste zufügen
                                        for (int l = 0; l < tangentPoints.Length; l += 4)
                                        {
                                            double dist = Geometry.Dist(tangentPoints[l + 1], pl.Project(object1Point)) +
                                                Geometry.Dist(tangentPoints[l + 2], pl.Project(object2Point)) +
                                                Geometry.Dist(tangentPoints[l + 3], pl.Project(object3Point));
                                            if (dist < mindist)
                                            {
                                                mindist = dist;
                                                selected1 = usable1Curves.Count - 1; // merken, welche Kurven die aktuell benutzten sind
                                                selected2 = usable2Curves.Count - 1;
                                                selected3 = usable3Curves.Count - 1;
                                                // selSol / 6 entspricht div 6, um einige Zeilen tiefer weitere 6 Möglichkeiten auswählen zu können
                                                // (/ 4) und (* 4), da pro Lösung vier Punkte geliefert werden
                                                int m = (l + 4 * ((Math.Abs(selSol) / 6) % (tangentPoints.Length / 4))) % tangentPoints.Length;
                                                selSolLoc = (Math.Abs(selSol) % 6) / 2; // liefert 0, 1, oder 2, "/2" um unten die Orientierung umschalten zu können
                                                center = tangentPoints[m];
                                                arcRad = Geometry.Dist(tangentPoints[m + selSolLoc + 1], center);
                                                if ((Math.Abs(selSol) & 0x1) == 0) // 0 und 1 dienen zur Unterscheidung der Kreisbogenausrichtung
                                                {
                                                    startAngle = new Angle(tangentPoints[m + selSolLoc + 1], center);
                                                    endAngle = new Angle(tangentPoints[m + ((selSolLoc + 1) % 3) + 1], center); // "%3", also modulo 3, da der Index läuft: 2,3,1
                                                }
                                                else
                                                {
                                                    endAngle = new Angle(tangentPoints[m + selSolLoc + 1], center);
                                                    startAngle = new Angle(tangentPoints[m + ((selSolLoc + 1) % 3) + 1], center); // "%3", also modulo 3, da der Index läuft: 2,3,1
                                                }
                                            }

                                        }
                                    }
                                    if (mindist < double.MaxValue)
                                    {
                                        //										base.MultiSolution = true;
                                        base.MultiSolutionCount = (tangentPoints.Length / 4) * 6;
                                        arc.SetArcPlaneCenterRadiusAngles(pl, pl.ToGlobal(center), arcRad, startAngle, new SweepAngle(startAngle, endAngle, arcDir));
                                        base.ShowActiveObject = true;
                                        tang1Curves = (ICurve[])usable1Curves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                                        tang2Curves = (ICurve[])usable2Curves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                                        tang3Curves = (ICurve[])usable3Curves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
                                        return (true);
                                    }
                                }
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

        private bool input1TangCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            object1Point = base.CurrentMousePosition;
            selected1 = 0;
            tang1Curves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            if (tang1Curves.Length > 0)
            {
                s = true; // es ist mind. ein Objekt gefunden
                if (curve2Input.Fixed || curve3Input.Fixed)
                {
                    s = showLine();
                }
            }
            if (up)
            {
                if (tang1Curves.Length == 0) sender.SetCurves(tang1Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(tang1Curves, tang1Curves[selected1]);
            }
            if (s) return true;
            base.ShowActiveObject = false;
            base.MultiSolutionCount = 0;
            //            base.MultiSolution = false;
            return false;
        }

        private void input1TangCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            tang1Curves = new ICurve[] { SelectedCurve };
            if (curve2Input.Fixed || curve3Input.Fixed)
            { showLine(); }

        }
        private bool input2TangCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            object2Point = base.CurrentMousePosition;
            selected2 = 0;
            tang2Curves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            if (tang2Curves.Length > 0)
            {
                s = true;
                if (curve1Input.Fixed || curve3Input.Fixed)
                {
                    s = showLine();
                }
            }

            //if (!curve3Input.Fixed & curve1Input.Fixed)
            //{
            //    Line line = Line.Construct();
            //    GeoVector v = new GeoVector(object1Point,object2Point);
            //    v = base.ActiveDrawingPlane.Normal ^ v;
            //    line.SetTwoPoints(object1Point,object1Point + v);
            //    tang3Curves = new ICurve []{line};
            //}
            //if (tang2Curves.Length > 0)
            //{
            //    s = true;
            //    if (curve1Input.Fixed & curve3Input.Fixed) 
            //    {	
            //        s = showLine();
            //    }
            //    else showLine();
            //}
            //else
            //{
            //    Ellipse circLoc;
            //    circLoc =  Ellipse.Construct();
            //    circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane,object2Point,base.WorldLength(1));
            //    tang2Curves = new ICurve []{circLoc};
            //    showLine();
            //}
            if (up)
            {
                if (tang2Curves.Length == 0) sender.SetCurves(tang2Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(tang2Curves, tang2Curves[selected2]);
            }
            if (s) return true;
            base.ShowActiveObject = false;
            base.MultiSolutionCount = 0;
            //            base.MultiSolution = false;
            return false;
        }

        private void input2TangCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            tang2Curves = new ICurve[] { SelectedCurve };
            if (curve1Input.Fixed || curve3Input.Fixed)
            { showLine(); }

        }

        private bool input3TangCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            object3Point = base.CurrentMousePosition;
            selected3 = 0;
            tang3Curves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            if (tang3Curves.Length > 0)
            {
                s = true;
                if (curve1Input.Fixed || curve2Input.Fixed)
                {
                    s = showLine();
                }
            }
            //if (tang3Curves.Length > 0)
            //{
            //    s = true;
            //    if (curve1Input.Fixed & curve2Input.Fixed) 
            //    {	
            //        s = showLine();
            //    }
            //}
            //else
            //{
            //    Ellipse circLoc;
            //    circLoc =  Ellipse.Construct();
            //    circLoc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane,object3Point,base.WorldLength(1));
            //    tang3Curves = new ICurve []{circLoc};
            //    showLine();
            //}
            if (up)
            {
                if (tang3Curves.Length == 0) sender.SetCurves(tang3Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(tang3Curves, tang3Curves[selected3]);
            }
            if (s) return true;
            base.ShowActiveObject = false;
            base.MultiSolutionCount = 0;
            //            base.MultiSolution = false;
            return false;
        }

        private void input3TangCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            tang3Curves = new ICurve[] { SelectedCurve };
            if (curve1Input.Fixed || curve2Input.Fixed)
            { showLine(); }

        }


        private double ArcRadius()
        {
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

        //public override void OnDifferentSolution(bool next)
        //{
        //    if (next) selSol += 1;
        //    else selSol -= 1;
        //    showLine();
        //}

        public override void OnSetAction()
        {
            arc = Ellipse.Construct();
            arcDir = ConstrDefaults.DefaultArcDirection;
            //			double dir;
            //			if (arcDir)
            //				dir = 1.5 * Math.PI;
            //			else dir = - 1.5 * Math.PI;
            //			arcRad = ConstrDefaults.DefaultArcRadius;
            //			arc.SetArcPlaneCenterRadiusAngles(base.ActiveDrawingPlane,ConstrDefaults.DefaultStartPoint,ConstrDefaults.DefaultArcRadius,0.0,dir);
            base.ActiveObject = arc;
            base.ShowActiveObject = false;

            base.TitleId = "Constr.Arc.ThreeTangents";

            curve1Input = new CurveInput("Constr.Arc.Tangent.Object");
            curve1Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve1Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(input1TangCurves);
            curve1Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(input1TangCurvesChanged);
            curve2Input = new CurveInput("Constr.Arc.Tangent.Object2");
            curve2Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve2Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(input2TangCurves);
            curve2Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(input2TangCurvesChanged);
            curve3Input = new CurveInput("Constr.Arc.Tangent.Object3");
            curve3Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve3Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(input3TangCurves);
            curve3Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(input3TangCurvesChanged);
            LengthInput radArc = new LengthInput("Constr.Arc.Radius");
            radArc.ReadOnly = true;
            radArc.Optional = true;
            radArc.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(ArcRadius);
            base.SetInput(curve1Input, curve2Input, curve3Input, radArc);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Arc.ThreeTangents";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}

