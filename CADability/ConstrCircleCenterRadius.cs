using CADability.Curve2D;
using CADability.GeoObject;
using System;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ConstrCircleCenterRadius : ConstructAction
    {
        private Ellipse circle;
        private CADability.GeoObject.Point gPoint1;
        private GeoPointInput circCenter;
        private LengthInput rad;
        private LengthInput diam;


        public ConstrCircleCenterRadius()
        { }


        private void Center(GeoPoint p)
        {
            circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, p, circle.Radius);
            circle.Center = p;
            gPoint1.Location = p;
        }

        private bool SetCircleRadius(double Length)
        {
            if (Length > Precision.eps)
            {
                //				circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane,circle.Center,Length);
                circle.Radius = Length;
                return true;
            }
            else return false;
        }

        private double GetCircleRadius()
        { return circle.Radius; }


        private bool SetCircleDiameter(double Length)
        {
            if (Length > Precision.eps)
            {
                //				circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane,circle.Center,Length);
                circle.Radius = Length / 2.0;
                return true;
            }
            else return false;
        }
        private double GetCircleDiameter()
        {
            return circle.Radius * 2.0;
        }

        double CalculateDiameter(GeoPoint MousePosition)
        {
            return (Geometry.Dist(MousePosition, circle.Center) * 2.0);
        }

        protected override void InputChanged(object activeInput)
        {
            if (activeInput == diam)
            {
                rad.Optional = true;
                diam.Optional = false;
            };
            if (activeInput == rad)
            {
                rad.Optional = false;
                diam.Optional = true;
            };
        }


        public override void OnSetAction()
        {
            Boolean useRadius = Frame.GetBooleanSetting("Formatting.Radius", true);

            circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, ConstrDefaults.DefaultArcCenter, ConstrDefaults.DefaultArcRadius);
            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);

            base.ActiveObject = circle;
            base.TitleId = "Constr.Circle.CenterRadius";

            circCenter = new GeoPointInput("Constr.Circle.Center");
            circCenter.DefaultGeoPoint = ConstrDefaults.DefaultArcCenter;
            circCenter.DefinesBasePoint = true;
            circCenter.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Center);

            rad = new LengthInput("Constr.Circle.Radius");
            rad.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(SetCircleRadius);
            rad.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetCircleRadius);
            if (!useRadius) { rad.Optional = true; }
            rad.DefaultLength = ConstrDefaults.DefaultArcRadius;
            rad.ForwardMouseInputTo = circCenter;

            diam = new LengthInput("Constr.Circle.Diameter");
            diam.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(SetCircleDiameter);
            diam.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetCircleDiameter);
            diam.CalculateLengthEvent += new LengthInput.CalculateLengthDelegate(CalculateDiameter);
            if (useRadius) { diam.Optional = true; }
            diam.DefaultLength = ConstrDefaults.DefaultArcDiameter;
            diam.ForwardMouseInputTo = circCenter;


            if (useRadius)
            { base.SetInput(circCenter, rad, diam); }
            else
            { base.SetInput(circCenter, diam, rad); }
            base.ShowAttributes = true;

            base.OnSetAction();
        }


        public override string GetID()
        { return "Constr.Circle.Center.Radius"; }

        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            if (CurrentInput == rad && circCenter.Fixed)
            {
                GeoObjectList l = base.GetObjectsUnderCursor(e.Location);
                l.DecomposeBlocks(true);
                l.DecomposeBlockRefs();
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
                            GeoPoint2D c12d = pln.Project(circle.Center);
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

        public override void OnDone()
        { base.OnDone(); }
    }
}
