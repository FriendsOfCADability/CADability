using CADability.GeoObject;
using System;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of an elliptical arc from its centre and its axes: the user picks the centre, a point that defines the
    /// major axis (its direction and the major radius), a point that defines the minor radius, and the start and the end
    /// angle of the arc; a boolean input selects the direction (counterclockwise or clockwise). Major radius, minor radius
    /// and the angle of the major axis may alternatively be entered as values. The ellipse lies in the active drawing plane.
    /// Menu id: "Constr.EllipseArc.CenterRadius".
    /// </summary>
    internal class ConstrEllipseArcCenterRadius : ConstructAction
    {
        //Gerhard: Optional sollte aber über TAB und konkrete Aktivierung erreichbar sein! (siehe Winkel und radius der Ellipse hier)

        private Ellipse ellipse;
        //		private GeoPoint elliPoint1;
        //		private GeoPoint elliPoint2;
        private LengthInput elliMinRad;
        private CADability.GeoObject.Point gPoint1;
        //        private GeoPoint elliCenter;

        public ConstrEllipseArcCenterRadius()
        { }


        private void Center(GeoPoint p)
        {
            //            elliCenter = p;
            ellipse.SetEllipseCenterPlane(p, base.ActiveDrawingPlane);
            base.BasePoint = p; // für die Winkelberechnung
            gPoint1.Location = p;
        }

        private void Point1(GeoPoint p)
        {
            //			elliPoint1 = p;
            GeoVector dir = p - ellipse.Center;
            if (!dir.IsNullVector())
            {
                // dir1 als senkrechte zu dir in der ActiveDrawingPlane erzeugen:
                GeoVector dir1 = ellipse.Plane.Normal ^ dir;
                dir1.Norm();
                dir1 = ellipse.MinorRadius * dir1;
                // nun daraus neue ellipse, in der Orientierung dir ist der Winkel enthalten
                ellipse.SetEllipseArcCenterAxis(ellipse.Center, dir, dir1, ellipse.StartParameter, ellipse.SweepParameter);
                // für die Mauseingabe des 2.Radius wird hier die Referenzlinie ellipse.Center,p angegeben
                elliMinRad.SetDistanceFromLine(ellipse.Center, p);
            }
            /*
                        if (!dir.IsNullVector())
                        {
                            ellipse.MajorRadius = Geometry.dist(ellipse.Center,p); 
                            // dir1 als senkrechte zu dir in der ActiveDrawingPlane erzeugen:
                            GeoVector dir1 = base.ActiveDrawingPlane.Normal ^ dir;
                            // nun daraus neue ellipse.plane, in der Orientierung dir ist der Winkel enthalten
                            ellipse.Plane = new Plane(ellipse.Center,dir,dir1);
                            // für die Mauseingabe des 2.Radius wird hier die Referenzlinie ellipse.Center,p angegeben
                            elliMinRad.SetDistanceFromLine(ellipse.Center,p);
                            elliAngle = new Angle(ActiveDrawingPlane.DirectionX,dir);
                        }
            */
        }

        private void Point2(GeoPoint p)
        {
            //            elliPoint2 = p;
            GeoVector dir1 = p - Geometry.DropPL(p, ellipse.Center, ellipse.Plane.DirectionX);
            if (!dir1.IsNullVector())
            {
                // nun daraus neue ellipse, 
                ellipse.SetEllipseArcCenterAxis(ellipse.Center, ellipse.MajorRadius * ellipse.Plane.DirectionX, dir1, ellipse.StartParameter, ellipse.SweepParameter);
            }
        }


        private bool SetMajorRadius(double length)
        {
            if (length > Precision.eps)
            {
                ellipse.MajorRadius = length;
                return true;
            }
            return false;
        }

        private double GetMajorRadius()
        {
            return (ellipse.MajorRadius);
        }

        private bool MinorRadius(double length)
        {
            if (length > Precision.eps)
            {
                ellipse.MinorRadius = length;
                return true;
            }
            return false;
        }
        private double GetMinorRadius()
        {
            return (ellipse.MinorRadius);
        }

        private bool ElliDir(GeoVector dir)
        {
            if (!dir.IsNullVector())
            {   // dir1 als senkrechte zu dir in der ActiveDrawingPlane erzeugen:
                GeoVector dir1 = ellipse.Plane.Normal ^ dir;
                // nun daraus neue ellipse.plane, in der Orientierung dir ist der Winkel enthalten
                ellipse.Plane = new Plane(ellipse.Center, dir, dir1);
                return true;
            }
            return false;
        }
        private GeoVector GetElliDir()
        {
            return ellipse.Plane.DirectionX;
        }


        private bool StartDir(GeoVector dir)
        {
            double endAbs = ellipse.SweepParameter + ellipse.StartParameter;
            ellipse.StartParameter = ellipse.ParameterOf(ellipse.Center + dir);
            ellipse.SweepParameter = new SweepAngle(ellipse.StartParameter, endAbs, ConstrDefaults.DefaultArcDirection);
            return true;
        }

        private bool EndDir(GeoVector dir)
        {
            ellipse.SweepParameter = new SweepAngle(ellipse.StartParameter, ellipse.ParameterOf(ellipse.Center + dir), ConstrDefaults.DefaultArcDirection);
            return true;
        }

        private GeoVector GetEndDir()
        {
            return ellipse.EndPoint - ellipse.Center;
        }

        private void SetDirection(bool val)
        {
            if ((val && (ellipse.SweepParameter < 0.0)) || (!val && (ellipse.SweepParameter > 0.0)))
            {
                ellipse.SweepParameter = -(2 * Math.PI - ellipse.SweepParameter);
            }
        }

        public override void OnSetAction()
        {
            ellipse = Ellipse.Construct();
            ellipse.Plane = base.ActiveDrawingPlane;
            ellipse.Center = ConstrDefaults.DefaultEllipseCenter;
            //            elliCenter = ellipse.Center;
            ellipse.MajorRadius = ConstrDefaults.DefaultEllipseMajorRadius;
            //            elliPoint1 = elliCenter + ellipse.MajorRadius * base.ActiveDrawingPlane.DirectionX;
            ellipse.MinorRadius = ConstrDefaults.DefaultEllipseMinorRadius;
            //            elliPoint2 = elliCenter + ellipse.MinorRadius * base.ActiveDrawingPlane.DirectionY;
            ellipse.StartParameter = 0.0;
            if (ConstrDefaults.DefaultArcDirection)
                ellipse.SweepParameter = 1.5 * Math.PI;
            else ellipse.SweepParameter = -1.5 * Math.PI;
            base.ActiveObject = ellipse;
            base.TitleId = "Constr.EllipseArc.CenterRadius";

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);

            GeoPointInput elliCenter = new GeoPointInput("Constr.Ellipse.CenterRadius.Center");
            elliCenter.DefaultGeoPoint = ConstrDefaults.DefaultEllipseCenter;
            elliCenter.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Center);

            GeoPointInput elliPointInput = new GeoPointInput("Constr.Ellipse.CenterRadius.Point");
            elliPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Point1);

            GeoPointInput elliPoint2 = new GeoPointInput("Constr.Ellipse.CenterRadius.Point2");
            //			elliMinRad.DefaultLength = ConstrDefaults.DefaultEllipseMinorRadius;
            elliPoint2.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Point2);
            elliPoint2.ForwardMouseInputTo = elliCenter;

            LengthInput elliMaxRad = new LengthInput("Constr.Ellipse.CenterRadius.MajorRadius");
            elliMaxRad.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(SetMajorRadius);
            elliMaxRad.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetMajorRadius);
            elliMaxRad.DefaultLength = ConstrDefaults.DefaultEllipseMajorRadius;
            elliMaxRad.ForwardMouseInputTo = elliCenter;
            elliMaxRad.Optional = true;

            elliMinRad = new LengthInput("Constr.Ellipse.CenterRadius.MinorRadius");
            elliMinRad.DefaultLength = ConstrDefaults.DefaultEllipseMinorRadius;
            elliMinRad.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(MinorRadius);
            elliMinRad.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetMinorRadius);
            elliMinRad.ForwardMouseInputTo = elliCenter;
            elliMinRad.Optional = true;

            GeoVectorInput elliDir = new GeoVectorInput("Constr.Ellipse.CenterRadius.Angle");
            elliDir.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(ElliDir);
            elliDir.GetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.GetGeoVectorDelegate(GetElliDir);
            elliDir.Optional = true;
            elliDir.IsAngle = true;


            GeoVectorInput startDir = new GeoVectorInput("Constr.EllipseArc.StartAngle");
            startDir.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(StartDir);
            startDir.IsAngle = true;

            GeoVectorInput endDir = new GeoVectorInput("Constr.EllipseArc.EndAngle");
            endDir.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(EndDir);
            endDir.GetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.GetGeoVectorDelegate(GetEndDir);
            endDir.IsAngle = true;

            BooleanInput dirInput = new BooleanInput("Constr.Arc.Direction", "Constr.Arc.Direction.Values");
            dirInput.DefaultBoolean = ConstrDefaults.DefaultArcDirection;
            dirInput.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetDirection);

            base.SetInput(elliCenter, elliPointInput, elliPoint2, startDir, endDir, elliMaxRad, elliMinRad, dirInput);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override string GetID()
        { return "Constr.EllipseArc.CenterRadius"; }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}



