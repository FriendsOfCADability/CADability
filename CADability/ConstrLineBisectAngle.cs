using CADability.Curve2D;
using CADability.GeoObject;
using System;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a line which bisects the angle between two curves. The user picks the two objects (polylines and paths are
    /// decomposed into their single elements) and enters the length of the line. The line starts at the intersection point of the
    /// two curves closest to the pick positions and bisects the angle between their directions at that point; of the four possible
    /// directions (the bisector, its opposite and the two perpendicular ones) the one closest to the pick positions is used, the
    /// others can be selected with the multi-solution mechanism. Started with the menu id "Constr.Line.BisectAngle".
    /// </summary>
    internal class ConstrLineBisectAngle : ConstructAction
    {
        private Line line;
        private GeoPoint startPoint;
        private GeoPoint endPoint;
        private GeoPoint objectPoint;
        private GeoPoint objectPoint1;
        private GeoPoint objectPoint2;
        private ICurve tangCurve1;
        private ICurve tangCurve2;
        private double lineLength;
        private CurveInput curveInput1;
        private CurveInput curveInput2;
        private LengthInput lengthInput;
        private int selSol = 0;  // wg. Unsymmetrie bei 0 wg Abs


        public ConstrLineBisectAngle()
        { }

        private bool selectLine()
        {
            try
            {
                GeoPoint[] endPointA = new GeoPoint[4];
                double mindist = double.MaxValue;
                GeoVector v = endPoint - startPoint;
                v.Norm();
                endPointA[0] = startPoint + lineLength * v;
                v = v ^ base.ActiveDrawingPlane.Normal; // jeweils senkrecht auf der vorherigen
                endPointA[1] = startPoint + lineLength * v;
                v = v ^ base.ActiveDrawingPlane.Normal;
                endPointA[2] = startPoint + lineLength * v;
                v = v ^ base.ActiveDrawingPlane.Normal;
                endPointA[3] = startPoint + lineLength * v;
                for (int i = 0; i < endPointA.Length; ++i)
                {   // der zur Mausposition am nächsten gelegene Punkt wird gesucht
                    double dist = Geometry.Dist(endPointA[i], objectPoint);
                    if (dist < mindist)
                    {
                        mindist = dist;
                        endPoint = endPointA[i];
                    }
                }
                if ((Math.Abs(selSol) % 4) != 0)
                {
                    // nochmal von vorne mit dem nächsten Punkt
                    v = endPoint - startPoint;
                    v.Norm();
                    // abhängig von der MultiSolution-Wahl umschalten
                    for (int i = 0; i < ((Math.Abs(selSol) % 4)); ++i)
                        v = v ^ base.ActiveDrawingPlane.Normal;
                    endPoint = startPoint + lineLength * v;
                }
                line.SetTwoPoints(startPoint, endPoint);
                return true;
            }
            catch (GeoVectorException)
            {
                return false;
            }
        }

        private bool showLine()
        {
            ICurve2D c1_2D;
            ICurve2D c2_2D;
            objectPoint = new GeoPoint(objectPoint1, objectPoint2); // die Mitte der Pickpunkte der zwei Objekte
            Plane pl;
            if (Curves.GetCommonPlane(tangCurve1, tangCurve2, out pl)) // die Kurven liegen in einer Ebene
            {
                c1_2D = tangCurve1.GetProjectedCurve(pl);
                if (c1_2D is Path2D) (c1_2D as Path2D).Flatten();
                c2_2D = tangCurve2.GetProjectedCurve(pl);
                if (c2_2D is Path2D) (c2_2D as Path2D).Flatten();
                GeoPoint2DWithParameter cutPoint;
                // der nächste der Schnittpunkte ist in cutPoint
                if (Curves2D.NearestPoint(c1_2D.Intersect(c2_2D), pl.Project(objectPoint), out cutPoint))
                {
                    base.MultiSolutionCount = 4;
                    try
                    {
                        GeoVector2D v1 = c1_2D.DirectionAt(cutPoint.par1); // die Richtung im Schnittpunkt
                        v1.Norm();
                        GeoVector2D v2 = c2_2D.DirectionAt(cutPoint.par2); // die Richtung im Schnittpunkt
                        v2.Norm();
                        startPoint = pl.ToGlobal(cutPoint.p);
                        endPoint = pl.ToGlobal(new GeoPoint2D(cutPoint.p, lineLength, (v1 + v2).Angle));
                        if (selectLine()) // die wahrscheinlichste der vier möglichen wählen
                        {
                            lengthInput.SetDistanceFromPoint(line.StartPoint); // die Basis für die Längenbestimmung
                            base.ShowActiveObject = true;
                            return (true);
                        }
                    }
                    catch (GeoVectorException)
                    {
                        base.ShowActiveObject = false;
                        base.MultiSolutionCount = 0;
                        return false;
                    }
                }
            }
            base.ShowActiveObject = false;
            base.MultiSolutionCount = 0;
            return (false);
        }

        private bool BisectObject1(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint1 = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                tangCurve1 = Curves[0];
                if (curveInput2.Fixed)
                {
                    if (tangCurve1 != tangCurve2) return (showLine());
                }
                else return true;
            }
            return false;
        }

        private void BisectObject1Changed(CurveInput sender, ICurve SelectedCurve)
        {
            tangCurve1 = SelectedCurve;
            if (curveInput2.Fixed)
                if (tangCurve1 != tangCurve2) showLine();
        }

        private bool BisectObject2(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint2 = base.CurrentMousePosition;
            if (up) // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);

            if (Curves.Length > 0)
            {
                tangCurve2 = Curves[0];
                if (curveInput1.Fixed)
                { if (tangCurve1 != tangCurve2) return (showLine()); }
                else return true;
            }
            return false;
        }

        private void BisectObject2Changed(CurveInput sender, ICurve SelectedCurve)
        {
            tangCurve2 = SelectedCurve;
            if (curveInput1.Fixed)
                if (tangCurve1 != tangCurve2) showLine();
        }

        private bool OnSetLength(double length)
        {
            if (length < Precision.eps) return false;
            lineLength = length;
            if (curveInput1.Fixed & curveInput2.Fixed) return (selectLine());
            return true;
        }

        private double OnGetLength()
        {
            return line.Length;
        }

        public override void OnSolution(int sol)
        {
            if (sol == -1) selSol += 1;
            else
                if (sol == -2) selSol -= 1;
            else selSol = sol;
            selectLine();
        }

        public override void OnSetAction()
        {
            lineLength = ConstrDefaults.DefaultLineLength;
            line = Line.Construct();
            base.ActiveObject = line;
            base.ShowActiveObject = false;
            base.TitleId = "Constr.Line.BisectAngle";
            line.Length = ConstrDefaults.DefaultLineLength;

            curveInput1 = new CurveInput("Constr.Line.Bisect.Object");
            curveInput1.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput1.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(BisectObject1);
            curveInput1.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(BisectObject1Changed);
            curveInput2 = new CurveInput("Constr.Line.Bisect.Object");
            curveInput2.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput2.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(BisectObject2);
            curveInput2.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(BisectObject2Changed);
            lengthInput = new LengthInput("Line.Length");
            lengthInput.defaultLength = ConstrDefaults.DefaultLineLength;
            lengthInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(OnSetLength);
            lengthInput.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(OnGetLength);
            lengthInput.ForwardMouseInputTo = new object[] { curveInput1, curveInput2 };
            base.SetInput(curveInput1, curveInput2, lengthInput);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Line.BisectAngle";
        }

        public override void OnDone()
        {
            base.OnDone();
        }
    }
}
