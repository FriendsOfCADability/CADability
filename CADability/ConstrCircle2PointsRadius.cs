
using CADability.GeoObject;
using System;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ConstrCircleTwoPointsRadius : ConstructAction
    {

        private Ellipse circ;
        private GeoPoint2D center;
        private GeoPoint radiusPoint;
        private GeoPoint circlePoint1;
        private GeoPoint circlePoint2;
        private GeoPointInput point1Input;
        private GeoPointInput point2Input;
        private LengthInput radInput;
        private LengthInput diamInput;
        private Boolean useRadius;
        private double circRad;
        private double circRadCalc;
        private int selSol = 0;  // wg. Unsymmetrie bei 0 wg Abs

        public ConstrCircleTwoPointsRadius()
        { }


        private bool showLine()
        {
            Plane pl = base.ActiveDrawingPlane;
            double mindist = double.MaxValue;
            GeoPoint2D[] centerPoints = Geometry.IntersectCC(pl.Project(circlePoint1), circRad, pl.Project(circlePoint2), circRad);
            for (int i = 0; i < centerPoints.Length; i++)
            {
                double dist = Geometry.Dist(centerPoints[i], pl.Project(radiusPoint));
                if (dist < mindist)
                {
                    mindist = dist;
                    int m = (i + selSol) % centerPoints.Length;
                    center = centerPoints[m];
                }

            }
            if (mindist < double.MaxValue)
            {
                if (CurrentInput != diamInput)
                {
                    //	Hilfsradius: Mittlerer Abstand momentaner Mauspunkt zu den Kreispunkten									
                    circRadCalc = (Geometry.Dist(circlePoint1, radiusPoint) + Geometry.Dist(circlePoint2, radiusPoint)) / 2.0;
                }
                else
                { // Durchmesser
                    try
                    {
                        Ellipse circTemp;
                        circTemp = Ellipse.Construct();
                        circTemp.SetCircle3Points(circlePoint1, circlePoint2, radiusPoint, base.ActiveDrawingPlane);
                        if ((Geometry.DistPL(radiusPoint, circlePoint1, circlePoint2) * 2.0) > Geometry.Dist(circlePoint1, circlePoint2))
                        { circRadCalc = circTemp.Radius * 2.0; }
                        else
                        { circRadCalc = Geometry.Dist(circlePoint1, circlePoint2); }
                    }
                    catch (EllipseException) { }
                }
                base.MultiSolutionCount = centerPoints.Length;
                circ.SetCirclePlaneCenterRadius(pl, pl.ToGlobal(center), circRad);
                base.ShowActiveObject = true;
                return (true);
            }



            base.MultiSolutionCount = 0;
            base.ShowActiveObject = false;
            return (false);
        }

        private void CirclePoint1(GeoPoint p)
        {
            circlePoint1 = p;
            if (point2Input.Fixed)
            {
                showLine();
            }
        }

        private void CirclePoint2(GeoPoint p)
        {
            circlePoint2 = p;
            if (point1Input.Fixed)
            {
                showLine();
            }
        }


        private bool CircRadius(double rad)
        {
            if (rad > Precision.eps)
            {
                circRad = rad;
                if (point1Input.Fixed && point2Input.Fixed)
                {
                    Double radMin = Geometry.Dist(circlePoint1, circlePoint2) / 2.0;
                    circRad = Math.Max(rad, radMin); // Radius nicht kleiner als Punktabstand zuläßt
                    if (radMin > rad)
                    {
                        base.MultiSolutionCount = 0;
                        base.ShowActiveObject = false;
                        return false;
                    }
                    return showLine();
                }
                return true;
            }
            return false;
        }

        private double radInput_OnCalculateLength(GeoPoint MousePosition)
        {
            radiusPoint = MousePosition;
            if (showLine()) return circRadCalc;
            return circRad;
        }

        private double GetRadius()
        {
            return circRad;
        }

        private bool SetCircleDiameter(double diam)
        {
            if (diam > Precision.eps)
            {
                circRad = diam / 2.0;
                if (point1Input.Fixed && point2Input.Fixed)
                {
                    Double radMin = Geometry.Dist(circlePoint1, circlePoint2) / 2.0;
                    circRad = Math.Max(circRad, radMin); // Radius nicht kleiner als Punktabstand zuläßt
                    if (radMin > circRad)
                    {
                        base.MultiSolutionCount = 0;
                        base.ShowActiveObject = false;
                        return false;
                    }
                    return showLine();
                }
                return true;
            }
            return false;
        }

        private double CalculateDiameter(GeoPoint MousePosition)
        {
            radiusPoint = MousePosition;
            if (showLine()) return circRadCalc;
            return circRad;
        }

        private double GetCircleDiameter()
        {
            return circRad * 2.0;
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
            useRadius = Frame.GetBooleanSetting("Formatting.Radius", true);

            radiusPoint = new GeoPoint(0, 0);
            circ = Ellipse.Construct();
            circRad = ConstrDefaults.DefaultArcRadius;
            circ.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, ConstrDefaults.DefaultArcCenter, ConstrDefaults.DefaultArcRadius);
            base.ActiveObject = circ;
            base.ShowActiveObject = false;
            base.TitleId = "Constr.Circle.TwoPointsRadius";
            point1Input = new GeoPointInput("Constr.Circle.Point1");
            point1Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint1);
            point2Input = new GeoPointInput("Constr.Circle.Point2");
            point2Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint2);

            radInput = new LengthInput("Constr.Arc.Radius");
            radInput.DefaultLength = ConstrDefaults.DefaultArcRadius;
            radInput.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(radInput_OnCalculateLength);
            radInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(CircRadius);
            radInput.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetRadius);
            if (!useRadius) { radInput.Optional = true; }
            radInput.ForwardMouseInputTo = new object[] { point1Input, point2Input };

            diamInput = new LengthInput("Constr.Circle.Diameter");
            diamInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(SetCircleDiameter);
            diamInput.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetCircleDiameter);
            diamInput.CalculateLengthEvent += new LengthInput.CalculateLengthDelegate(CalculateDiameter);
            if (useRadius) { diamInput.Optional = true; }
            diamInput.DefaultLength = ConstrDefaults.DefaultArcDiameter;
            diamInput.ForwardMouseInputTo = new object[] { point1Input, point2Input };


            if (useRadius)
            { base.SetInput(point1Input, point2Input, radInput, diamInput); }
            else
            { base.SetInput(point1Input, point2Input, diamInput, radInput); }
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "Constr.Circle.TwoPointsRadius";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}

