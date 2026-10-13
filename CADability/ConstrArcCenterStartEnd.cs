using CADability.Curve2D;
using CADability.GeoObject;
using System;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;


namespace CADability.Actions

{
    /// <summary>
    /// Construction of a circular arc from its centre, its start point and its end point. The radius is the distance from the
    /// centre to the start point, the end point only determines the end angle; a boolean input selects the direction
    /// (counterclockwise or clockwise). The radius is shown as a read-only value. The arc lies in the active drawing plane.
    /// Menu id: "Constr.Arc.CenterCenterStartEnd".
    /// </summary>
    internal class ConstrArcCenterStartEnd : ConstructAction
    {
        private GeoPoint arcCenter;
        private GeoPoint arcStartPoint;
        private GeoPoint arcEndPoint;
        private double arcRad;
        private double startAng;
        private double endAng;
        private bool arcDir;
        private Plane arcPlane;
        private GeoPointInput arcCenterInput;
        private GeoPointInput arcStartPointInput;
        private GeoPointInput arcEndPointInput;
        private CADability.GeoObject.Point gPoint1;

        private Ellipse arc;


        public ConstrArcCenterStartEnd()
        { }

        private void Center(GeoPoint p)
        {
            arcCenter = p;
            gPoint1.Location = p;
            arcPlane = base.ActiveDrawingPlane;

            if (arcStartPointInput.Fixed)
            {
                arcRad = Geometry.Dist(arcCenter, arcStartPoint);
                startAng = new Angle(arc.Plane.Project(arcStartPoint), arc.Plane.Project(arcCenter));
            }
            if (arcEndPointInput.Fixed)
            {
                endAng = new SweepAngle(startAng, new Angle(arc.Plane.Project(arcEndPoint), arc.Plane.Project(arcCenter)), ConstrDefaults.DefaultArcDirection);
            }
            if (arcStartPointInput.Fixed & arcEndPointInput.Fixed)
            {
                try
                {
                    arcPlane = new Plane(arcCenter, arcStartPoint, arcEndPoint);
                    arcPlane.Align(base.ActiveDrawingPlane, false, true);
                }
                catch (PlaneException) { arcPlane = base.ActiveDrawingPlane; }
            }
            arc.SetArcPlaneCenterRadiusAngles(arcPlane, arcCenter, arcRad, startAng, endAng);
        }

        private void StartPoint(GeoPoint p)
        {
            if (!Precision.IsEqual(p, arcCenter))
            {

                arcStartPoint = p;

                if (arcCenterInput.Fixed)
                {
                    try
                    {
                        arcPlane = new Plane(arcCenter, arcCenter - arcStartPoint, base.ActiveDrawingPlane.Normal ^ ((GeoVector)(arcCenter - arcStartPoint)));
                        arcPlane.Align(base.ActiveDrawingPlane, false);
                    }
                    catch (PlaneException)
                    {
                        arcPlane = new Plane(arcCenter, arcCenter - arcStartPoint, ((GeoVector)(arcCenter - arcStartPoint)) ^ base.ActiveDrawingPlane.DirectionX);
                        arcPlane.Align(base.ActiveDrawingPlane, false, true);
                    };
                    arcRad = Geometry.Dist(arcCenter, arcStartPoint);
                    if (arcCenterInput.Fixed & arcEndPointInput.Fixed)
                    {
                        try
                        {
                            arcPlane = new Plane(arcCenter, arcStartPoint, arcEndPoint);
                            arcPlane.Align(base.ActiveDrawingPlane, false, true);
                        }
                        catch (PlaneException) { arcPlane = base.ActiveDrawingPlane; };
                    }
                    double endAbs = arc.SweepParameter + arc.StartParameter;
                    startAng = new Angle(arc.Plane.Project(arcStartPoint), arc.Plane.Project(arcCenter));
                    //                    endAng = new SweepAngle(endAbs - startAng);
                    arc.SetArcPlaneCenterRadiusAngles(arcPlane, arcCenter, arcRad, startAng, endAng);
                }
            }
        }

        private void EndPoint(GeoPoint p)
        {
            arcEndPoint = p;
            //			arc.SweepParameter = new SweepAngle(arc.StartParameter,new Angle(arc.Plane.Project(arcEndPoint),arc.Plane.Project(arcCenter)),ConstrDefaults.DefaultArcDirection); 
            arcPlane = base.ActiveDrawingPlane;
            if (arcCenterInput.Fixed & arcStartPointInput.Fixed)
            {
                if (!Precision.IsEqual(arcEndPoint, arcCenter) & !Precision.IsEqual(arcEndPoint, arcStartPoint) & !Precision.IsEqual(arcCenter, arcStartPoint))
                {
                    try
                    {
                        arcPlane = new Plane(arcCenter, arcStartPoint, arcEndPoint);
                        arcPlane.Align(base.ActiveDrawingPlane, false, true);
                    }
                    catch (PlaneException) { arcPlane = base.ActiveDrawingPlane; };
                }
            }
            endAng = new SweepAngle(arc.StartParameter, new Angle(arc.Plane.Project(arcEndPoint), arc.Plane.Project(arcCenter)), arcDir);
            arc.SetArcPlaneCenterRadiusAngles(arcPlane, arcCenter, arcRad, startAng, endAng);
        }

        private void SetDirection(bool val)
        {
            if ((val && (arc.SweepParameter < 0.0)) || (!val && (arc.SweepParameter > 0.0)))
            {
                //				arc.SweepParameter = -(2 * Math.PI - arc.SweepParameter);
                arc.SweepParameter = -arc.SweepParameter;
                endAng = arc.SweepParameter;
            }
            arcDir = val;
        }


        private double ArcRadius()
        {
            return arc.MajorRadius;
        }
        /*
                private GeoVector StartDir()
                {
                    return (arc.StartPoint-arc.Center);
                }

                private GeoVector EndDir()
                {
                    return (arc.EndPoint-arc.Center);
                }
        */


        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            if (arcCenterInput.Fixed)
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

        public override void OnSetAction()
        {
            arc = Ellipse.Construct();
            arcCenter = new GeoPoint(0.0, 0.0, 0.0);
            arcRad = base.WorldViewSize / 20;
            startAng = 0.0;
            arcDir = ConstrDefaults.DefaultArcDirection;
            if (arcDir)
                endAng = 1.5 * Math.PI;
            else endAng = -1.5 * Math.PI;


            arcPlane = base.ActiveDrawingPlane;

            arc.SetArcPlaneCenterRadiusAngles(arcPlane, arcCenter, arcRad, startAng, endAng);

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);

            base.ActiveObject = arc;
            base.TitleId = "Constr.Arc.CenterStartEnd";

            arcCenterInput = new GeoPointInput("Constr.Arc.Center");
            arcCenterInput.DefaultGeoPoint = ConstrDefaults.DefaultArcCenter;
            arcCenterInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Center);
            arcStartPointInput = new GeoPointInput("Constr.Arc.StartPoint");
            arcStartPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(StartPoint);
            arcEndPointInput = new GeoPointInput("Constr.Arc.EndPoint");
            arcEndPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(EndPoint);
            BooleanInput dirInput = new BooleanInput("Constr.Arc.Direction", "Constr.Arc.Direction.Values");
            dirInput.DefaultBoolean = ConstrDefaults.DefaultArcDirection;
            dirInput.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetDirection);

            LengthInput arcRadius = new LengthInput("Constr.Arc.Radius");
            arcRadius.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(ArcRadius);
            arcRadius.Optional = true;
            arcRadius.ReadOnly = true;
            /*
                        GeoVectorInput startDir = new GeoVectorInput("Constr.Arc.CenterRadius.StartAngle");
                        startDir.Optional = true;
                        startDir.ReadOnly = true;
                        startDir.OnGetGeoVector +=new Condor.Actions.ConstructAction.GeoVectorInput.GetGeoVector(StartDir);

                        GeoVectorInput endDir = new GeoVectorInput("Constr.Arc.CenterRadius.EndAngle");
                        endDir.Optional = true;
                        endDir.ReadOnly = true;
                        endDir.OnGetGeoVector +=new Condor.Actions.ConstructAction.GeoVectorInput.GetGeoVector(EndDir);
                        base.SetInput(arcCenterInput,arcStartPointInput,arcEndPointInput,dirInput,arcRadius,startDir,endDir);			
            */
            base.SetInput(arcCenterInput, arcStartPointInput, arcEndPointInput, dirInput, arcRadius);
            base.ShowAttributes = true;


            base.OnSetAction();
        }
        public override string GetID()
        { return "Constr.Arc.CenterCenterStartEnd"; }

        public override void OnDone()
        { base.OnDone(); }

    }
}

