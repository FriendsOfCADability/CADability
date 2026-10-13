using CADability.Curve2D;
using CADability.GeoObject;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a line with a given direction that is tangential to a curve. The user picks the
    /// tangent object (circle, arc, ellipse or any other planar curve), enters the length and the direction
    /// (as an angle) of the line and chooses whether the tangent point is the start point or the middle of
    /// the line. Of several tangent points the one closest to the pick position is used. The end point of
    /// the line becomes the default start point of the next construction. Started with the action id
    /// "Constr.Line.TangentAngle" (menu command "MenuId.Constr.Line.TangentAngle").
    /// </summary>
    internal class ConstrLineTangentAngle : ConstructAction
    {
        private Line line;
        private GeoPoint2D startPoint2D;
        private GeoPoint startPoint;
        private GeoPoint endPoint;
        private GeoPoint objectPoint;
        private ICurve[] tangCurves;
        private GeoVector lineDir;
        private double lineLength;
        private CurveInput curveInput;
        private bool linePositionMiddle;
        int selected;



        public ConstrLineTangentAngle()
        { }

        private bool showLine()
        {
            ArrayList usableCurves = new ArrayList(); // lokales Array, das die gültigen Kurven sammelt
            double mindist = double.MaxValue;
            for (int i = 0; i < tangCurves.Length; ++i)
            {
                Plane pl;
                double lineAngle;
                if (Curves.GetCommonPlane(objectPoint, tangCurves[i], out pl))
                {
                    ICurve2D l2D = tangCurves[i].GetProjectedCurve(pl);
                    if (l2D is Path2D) (l2D as Path2D).Flatten();
                    lineAngle = pl.Project(lineDir).Angle; // 2D-Winkel aus der Richtung erzeugen
                    GeoPoint2D[] tangentPoints = l2D.TangentPointsToAngle(lineAngle, pl.Project(objectPoint));
                    if (tangentPoints.Length > 0)
                    {   // eine gültige Kurve ist gefunden
                        usableCurves.Add(tangCurves[i]);
                        for (int j = 0; j < tangentPoints.Length; ++j)
                        {
                            double dist = Geometry.Dist(tangentPoints[j], pl.Project(objectPoint));
                            if (dist < mindist)
                            {
                                mindist = dist;
                                selected = usableCurves.Count - 1; // merken, welche Kurve die aktuell benutzte ist
                                startPoint2D = tangentPoints[j];
                            }

                        }
                    }
                }
                if (mindist < double.MaxValue)
                {
                    if (!linePositionMiddle)
                    {
                        startPoint = pl.ToGlobal(startPoint2D);
                        endPoint = startPoint + lineLength * lineDir;
                    }
                    else
                    {
                        startPoint = pl.ToGlobal(startPoint2D) - (lineLength / 2.0) * lineDir;
                        endPoint = pl.ToGlobal(startPoint2D) + (lineLength / 2.0) * lineDir;
                    }
                    line.SetTwoPoints(startPoint, endPoint);
                    tangCurves = (ICurve[])usableCurves.ToArray(typeof(ICurve)); // tangCurves wird mit den gültigen überschrieben und unten zur Anzeige zurückgegeben an den Sender
                    base.BasePoint = pl.ToGlobal(startPoint2D);
                    //					base.BasePoint = line.StartPoint;
                    return (true);
                }
            }
            // hier Prototyp-Darstellung:
            if (!linePositionMiddle)
            {
                startPoint = base.BasePoint;
                endPoint = startPoint + lineLength * lineDir;
            }
            else
            {
                startPoint = base.BasePoint - (lineLength / 2.0) * lineDir;
                endPoint = base.BasePoint + (lineLength / 2.0) * lineDir;
            }
            //			startPoint = base.BasePoint;
            //			endPoint = startPoint+lineLength*lineDir;
            line.SetTwoPoints(startPoint, endPoint);
            return (false);
        }
        private bool inputTangCurves(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint = base.CurrentMousePosition;
            base.BasePoint = objectPoint;
            selected = 0;
            tangCurves = Curves; // lokale Liste, damit showline immer die Daten hat
            bool s = false;
            s = (showLine());
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
            showLine();

        }

        double CalculateLength(GeoPoint MousePosition)
        {
            if (!linePositionMiddle)
            {
                return Geometry.Dist(base.BasePoint, MousePosition);
            }
            else return (Geometry.Dist(base.BasePoint, MousePosition) * 2.0);
        }

        private bool OnSetLength(double length)
        {
            if (length > Precision.eps)
            {
                if (!linePositionMiddle)
                {
                    lineLength = length;
                    line.Length = length;
                }
                else
                {
                    startPoint = base.BasePoint - (length / 2.0) * lineDir;
                    endPoint = base.BasePoint + (length / 2.0) * lineDir;
                    line.SetTwoPoints(startPoint, endPoint);
                    lineLength = length;
                }
                return true;
            }
            return false;
        }

        private bool SetGeoVector(GeoVector vector)
        {
            if (Precision.IsNullVector(vector)) return false;
            lineDir = vector;
            lineDir.Norm();
            //			if (curveInput.Fixed)
            //			{
            objectPoint = base.BasePoint;
            //			objectPoint = line.StartPoint;
            showLine();
            //			}
            return true;
        }

        void SetPosition(bool val)
        {
            linePositionMiddle = val;
            objectPoint = base.BasePoint;
            showLine();
        }




        public override void OnSetAction()
        {
            line = Line.Construct();
            lineLength = ConstrDefaults.DefaultLineLength;
            lineDir = ConstrDefaults.DefaultLineDirection;
            linePositionMiddle = ConstrDefaults.DefaultLinePosition;
            startPoint = new GeoPoint(0.0, 0.0, 0.0);
            endPoint = startPoint + lineLength * lineDir;
            line.SetTwoPoints(startPoint, endPoint);
            base.ActiveObject = line;
            tangCurves = new ICurve[0];

            base.TitleId = "Constr.Line.TangentAngle";

            curveInput = new CurveInput("Constr.Line.Tangent.Object");
            curveInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            curveInput.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(inputTangCurves);
            curveInput.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(inputTangCurvesChanged);

            LengthInput len = new LengthInput("Line.Length");
            len.DefaultLength = ConstrDefaults.DefaultLineLength;
            len.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(OnSetLength);
            len.CalculateLengthEvent += new LengthInput.CalculateLengthDelegate(CalculateLength);
            len.ForwardMouseInputTo = curveInput;

            GeoVectorInput dir = new GeoVectorInput("Line.Direction");
            dir.DefaultGeoVector = ConstrDefaults.DefaultLineDirection;
            dir.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(SetGeoVector);
            dir.IsAngle = true;
            dir.ForwardMouseInputTo = curveInput;

            BooleanInput position = new BooleanInput("Constr.Line.Tangent.Position", "Constr.Line.Tangent.Position.Values");
            position.DefaultBoolean = ConstrDefaults.DefaultLinePosition;
            position.SetBooleanEvent += new BooleanInput.SetBooleanDelegate(SetPosition);
            //            position.GetBooleanEvent += new BooleanInput.GetBooleanDelegate(GetPosition);

            base.SetInput(curveInput, len, dir, position);
            base.ShowAttributes = true;

            base.OnSetAction();
        }



        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Line.TangentAngle";
        }

        public override void OnDone()
        {
            ConstrDefaults.DefaultStartPoint.Point = line.EndPoint; // wird auf den letzten Endpunkt gesetzt
            base.OnDone();
        }
    }
}
