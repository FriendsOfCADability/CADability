using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a circle that is tangent to two curves and passes through a point. The user picks the two curves
    /// (polylines and paths are decomposed into their segments) and the point; the curves and the point must lie in a common
    /// plane. Of the possible solutions the one closest to the pick positions is shown, the others can be selected with the
    /// "next solution" command. The radius is shown as a read-only value. Menu id: "Constr.Circle.PointTwoTangents".
    /// </summary>
    internal class ConstrCirclePoint2Tangents : ConstructAction
    {

        private Ellipse circ;
        private GeoPoint2D center;
        private GeoPoint object1Point;
        private GeoPoint object2Point;
        private GeoPoint circlePoint;
        private ICurve[] tang1Curves;
        private ICurve[] tang2Curves;
        private CurveInput curve1Input;
        private CurveInput curve2Input;
        private GeoPointInput pointInput;
        private double circRad;
        private int selected1;
        private int selected2;
        private int selSol = 100;  // wg. Unsymmetrie bei 0 wg Abs

        public ConstrCirclePoint2Tangents()
        { }


        private bool showLine()
        {
            ArrayList usable1Curves = new ArrayList();
            ArrayList usable2Curves = new ArrayList();
            Plane pl;
            double mindist = double.MaxValue;
            if (tang1Curves == null) return false;
            if (tang2Curves == null) return false;
            //			if (tang3Curves == null) return false;
            for (int i = 0; i < tang1Curves.Length; ++i)
            {
                for (int j = 0; j < tang2Curves.Length; ++j)
                {
                    if (Curves.GetCommonPlane(tang1Curves[i], tang2Curves[j], out pl))
                    {
                        if (Precision.IsPointOnPlane(circlePoint, pl))
                        {
                            ICurve2D l2D1 = tang1Curves[i].GetProjectedCurve(pl);
                            ICurve2D l2D2 = tang2Curves[j].GetProjectedCurve(pl);
                            ICurve2D l2D3 = new Circle2D(pl.Project(circlePoint), 0.0);
                            GeoPoint2D[] tangentPoints = Curves2D.TangentCircle(l2D1, l2D2, l2D3, pl.Project(object1Point), pl.Project(object2Point), pl.Project(circlePoint));
                            if (tangentPoints.Length > 0)
                            {	// eine gültige Linie ist gefunden
                                //if (curve1Input.Fixed & curve2Input.Fixed & (tangentPoints.Length > 4)) 
                                //{
                                //        int debug = 0;
                                //}
                                usable1Curves.Add(tang1Curves[i]); // zur lokalen Liste zufügen
                                usable2Curves.Add(tang2Curves[j]); // zur lokalen Liste zufügen
                                for (int l = 0; l < tangentPoints.Length; l += 4)
                                {
                                    double dist = Geometry.Dist(tangentPoints[l + 1], pl.Project(object1Point)) +
                                        Geometry.Dist(tangentPoints[l + 2], pl.Project(object2Point)) +
                                        Geometry.Dist(tangentPoints[l + 3], pl.Project(circlePoint));
                                    if (dist < mindist)
                                    {
                                        mindist = dist;
                                        selected1 = usable1Curves.Count - 1; // merken, welche Kurven die aktuell benutzten sind
                                        selected2 = usable2Curves.Count - 1;
                                        // selSol / 6 entspricht div 6, um einige Zeilen tiefer weitere 6 Möglichkeiten auswählen zu können
                                        // (/ 4) und (* 4), da pro Lösung vier Punkte geliefert werden
                                        int m = (l + 4 * (Math.Abs(selSol) % (tangentPoints.Length / 4))) % tangentPoints.Length;
                                        center = tangentPoints[m];
                                        circRad = Geometry.Dist(tangentPoints[m + 1], center);
                                    }

                                }
                            }
                            if (mindist < double.MaxValue)
                            {
                                //										base.MultiSolution = true;
                                base.MultiSolutionCount = tangentPoints.Length / 4;
                                circ.SetCirclePlaneCenterRadius(pl, pl.ToGlobal(center), circRad);
                                tang1Curves = (ICurve[])usable1Curves.ToArray(typeof(ICurve)); // überschreibt die eigentliche Liste und wird unten an den Sender zurückgeliefert
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

        private void CirclePoint(GeoPoint p)
        {
            circlePoint = p;
            if (curve1Input.Fixed || curve2Input.Fixed)
            {
                showLine();
            }
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
                          //                if (curve2Input.Fixed && curve3Input.Fixed) 
                if (curve2Input.Fixed || pointInput.Fixed)
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
            //			base.ActiveObject = null;
            return false;
        }

        private void input1TangCurvesChanged(CurveInput sender, ICurve SelectedCurve)
        {
            tang1Curves = new ICurve[] { SelectedCurve };
            if (curve2Input.Fixed || pointInput.Fixed)
            { showLine(); }

        }
        private bool input2TangCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            object2Point = base.CurrentMousePosition;
            selected2 = 0;
            tang2Curves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            //if (!curve3Input.Fixed && curve1Input.Fixed)
            //{
            //    Line line = Line.Construct();
            //    GeoVector v = new GeoVector(object1Point,object2Point);
            //    v = base.ActiveDrawingPlane.Normal ^ v;
            //    line.SetTwoPoints(object1Point,object1Point + v);
            //    tang3Curves = new ICurve []{line};
            //}
            if (tang2Curves.Length > 0)
            {
                s = true;
                //                if (curve1Input.Fixed && curve3Input.Fixed) 
                if (curve1Input.Fixed || pointInput.Fixed)
                {
                    s = showLine();
                }
                //				else showLine();
            }
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
            if (curve1Input.Fixed || pointInput.Fixed)
            { showLine(); }

        }

        private double CircRadius()
        {
            return circRad;
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
            circ = Ellipse.Construct();
            circRad = ConstrDefaults.DefaultArcRadius;
            circ.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, ConstrDefaults.DefaultArcCenter, ConstrDefaults.DefaultArcRadius);
            base.ActiveObject = circ;
            base.ShowActiveObject = false;

            base.TitleId = "Constr.Circle.PointTwoTangents";
            curve1Input = new CurveInput("Constr.Arc.Tangent.Object");
            curve1Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve1Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(input1TangCurves);
            curve1Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(input1TangCurvesChanged);
            curve2Input = new CurveInput("Constr.Arc.Tangent.Object");
            curve2Input.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curve2Input.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(input2TangCurves);
            curve2Input.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(input2TangCurvesChanged);
            pointInput = new GeoPointInput("Constr.Arc.Point");
            pointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint);
            LengthInput radCirc = new LengthInput("Constr.Arc.Radius");
            radCirc.ReadOnly = true;
            radCirc.Optional = true;
            radCirc.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(CircRadius);
            base.SetInput(curve1Input, curve2Input, pointInput, radCirc);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Circle.PointTwoTangents";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}



