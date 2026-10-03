using CADability.Curve2D;
using CADability.GeoObject;
using System;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;


namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ConstrArcCenterRadius : ConstructAction
    {
        private Ellipse arc;
        private CADability.GeoObject.Point gPoint1;
        private GeoPointInput centerInput;
        private LengthInput radInput;
        private LengthInput diamInput;
        private GeoVectorInput startDirInput;

        public ConstrArcCenterRadius()
        { }


        private void Center(GeoPoint p)
        {
            arc.SetArcPlaneCenterRadius(base.ActiveDrawingPlane, p, arc.Radius);
            gPoint1.Location = p;
        }

        private bool SetArcRadius(double rad)
        {
            if (rad > Precision.eps)
            {
                arc.Radius = rad;
                //                arc.SetArcPlaneCenterRadius(base.ActiveDrawingPlane, arc.Center, rad);
                return true;
            }
            return false;
        }

        private double GetArcRadius()
        { return arc.Radius; }

        private bool SetArcDiameter(double Length)
        {
            if (Length > Precision.eps)
            {
                //				circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane,circle.Center,Length);
                //                arc.SetArcPlaneCenterRadius(base.ActiveDrawingPlane, arc.Center, Length / 2.0);
                arc.Radius = Length / 2.0;
                return true;
            }
            else return false;
        }

        private double GetArcDiameter()
        {
            return arc.Radius * 2.0;
        }

        double CalculateDiameter(GeoPoint MousePosition)
        {
            return (Geometry.Dist(MousePosition, arc.Center) * 2.0);
        }




        private bool StartDir(GeoVector dir)
        {
            arc.StartParameter = base.ActiveDrawingPlane.Project(dir).Angle;
            return true;
        }

        private bool EndDir(GeoVector dir)
        {
            arc.SweepParameter = new SweepAngle(arc.StartParameter, base.ActiveDrawingPlane.Project(dir).Angle, ConstrDefaults.DefaultArcDirection);
            return true;
        }

        private GeoVector GetEndDir()
        {
            return (arc.EndPoint - arc.Center);
        }

        private void SetDirection(bool val)
        {
            if ((val && (arc.SweepParameter < 0.0)) || (!val && (arc.SweepParameter > 0.0)))
            {
                arc.SweepParameter = -(2 * Math.PI - arc.SweepParameter);
            }
        }


        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            if (centerInput.Fixed)
            {
                GeoObjectList l = base.GetObjectsUnderCursor(e.Location);
                l.DecomposeAll();
                GeoPoint inpoint = base.WorldPoint(e.Location);
                Plane pln = vw.Projection.DrawingPlane;
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] is ICurve)
                    {
                        ICurve curve = l[i] as ICurve;
                        if (curve.IsInPlane(pln))
                        {
                            ICurve2D c2d = curve.GetProjectedCurve(pln);
                            GeoPoint2D c12d = pln.Project(arc.Center);
                            GeoPoint2D[] pp = c2d.PerpendicularFoot(c12d);
                            for (int j = 0; j < pp.Length; j++)
                            {
                                GeoPoint p = pln.ToGlobal(pp[j]);
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

            arc = Ellipse.Construct();
            double dir;
            if (ConstrDefaults.DefaultArcDirection)
                dir = 1.5 * Math.PI;
            else dir = -1.5 * Math.PI;
            arc.SetArcPlaneCenterRadiusAngles(base.ActiveDrawingPlane, ConstrDefaults.DefaultStartPoint, ConstrDefaults.DefaultArcRadius, 0.0, dir);

            base.ActiveObject = arc;
            base.TitleId = "Constr.Arc.CenterRadius";

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);

            centerInput = new GeoPointInput("Constr.Arc.Center");
            centerInput.DefaultGeoPoint = ConstrDefaults.DefaultArcCenter;
            centerInput.DefinesBasePoint = true;
            centerInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Center);

            radInput = new LengthInput("Constr.Arc.Radius");
            radInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(SetArcRadius);
            radInput.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetArcRadius);
            if (!useRadius) { radInput.Optional = true; }
            radInput.DefaultLength = ConstrDefaults.DefaultArcRadius;
            radInput.ForwardMouseInputTo = centerInput;

            diamInput = new LengthInput("Constr.Arc.Diameter");
            diamInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(SetArcDiameter);
            diamInput.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetArcDiameter);
            diamInput.CalculateLengthEvent += new LengthInput.CalculateLengthDelegate(CalculateDiameter);
            if (useRadius) { diamInput.Optional = true; }
            diamInput.DefaultLength = ConstrDefaults.DefaultArcDiameter;
            diamInput.ForwardMouseInputTo = centerInput;


            startDirInput = new GeoVectorInput("Constr.Arc.CenterRadius.StartAngle");
            startDirInput.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(StartDir);
            startDirInput.IsAngle = true;

            GeoVectorInput endDirInput = new GeoVectorInput("Constr.Arc.CenterRadius.EndAngle");
            endDirInput.GetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.GetGeoVectorDelegate(GetEndDir);
            endDirInput.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(EndDir);
            endDirInput.IsAngle = true;

            BooleanInput dirInput = new BooleanInput("Constr.Arc.Direction", "Constr.Arc.Direction.Values");
            dirInput.DefaultBoolean = ConstrDefaults.DefaultArcDirection;
            dirInput.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetDirection);
            if (useRadius)
            { base.SetInput(centerInput, radInput, diamInput, startDirInput, endDirInput, dirInput); }
            else
            { base.SetInput(centerInput, diamInput, radInput, startDirInput, endDirInput, dirInput); }


            base.ShowAttributes = true;
            base.OnSetAction();

        }
        public override string GetID()
        { return "Constr.Arc.CenterRadius"; }

        public override void OnDone()
        { base.OnDone(); }

    }
}
