using CADability.GeoObject;
using System;

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a full ellipse by its center and two axis points. The user picks the center, a point which defines the
    /// direction and length of the major axis (the ellipse lies in the active drawing plane) and a point whose distance from the
    /// major axis defines the length of the minor axis; the major and minor radius can optionally be entered numerically instead.
    /// Started with the menu id "Constr.Ellipse.CenterRadius".
    /// </summary>
    internal class ConstrEllipseCenterRadius : ConstructAction
    {
        //Gerhard: Optional sollte aber über TAB und konkrete Aktivierung erreichbar sein! (siehe Winkel und radius der Ellipse hier)

        private Ellipse ellipse;
        private GeoPoint point1;
        private GeoPointInput elliPointInput;
        private LengthInput elliMinRad;
        private CADability.GeoObject.Point gPoint1;

        public ConstrEllipseCenterRadius()
        { }


        private void Center(GeoPoint p)
        {
            //			ellipse.Center = p;
            ellipse.SetEllipseCenterPlane(p, base.ActiveDrawingPlane);
            base.BasePoint = p; // für die Winkelberechnung
            gPoint1.Location = p;
        }

        private void Point1(GeoPoint p)
        {
            point1 = p;
            GeoVector dir = p - ellipse.Center;
            if (!dir.IsNullVector())
            {
                // dir1 als senkrechte zu dir in der ActiveDrawingPlane erzeugen:
                GeoVector dir1 = ellipse.Plane.Normal ^ dir;
                dir1.Norm();
                dir1 = ellipse.MinorRadius * dir1;
                // nun daraus neue ellipse, in der Orientierung dir ist der Winkel enthalten
                ellipse.SetEllipseCenterAxis(ellipse.Center, dir, dir1);
                //				ellipse.SetEllipseCenterAxis(ellipse.Center,dir,dir1,base.ActiveDrawingPlane);
                // für die Mauseingabe des 2.Radius wird hier die Referenzlinie ellipse.Center,p angegeben
                elliMinRad.SetDistanceFromLine(ellipse.Center, p);
            }
        }

        private void Point2(GeoPoint p)
        {
            //            elliPoint2 = p;
            GeoVector dir1 = p - Geometry.DropPL(p, ellipse.Center, ellipse.Plane.DirectionX);
            if (!dir1.IsNullVector())
            {
                // nun daraus neue ellipse, 
                ellipse.SetEllipseCenterAxis(ellipse.Center, ellipse.MajorRadius * ellipse.Plane.DirectionX, dir1);
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
                GeoVector dir1 = base.ActiveDrawingPlane.Normal ^ dir;
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

        private bool Angle(Angle angle)
        {
            // derWinkel bezieht sich auf die DrawingPlane der aktuellen Ansicht
            // und wird hier umgerechnet
            GeoVector dir = base.WorldDirection(angle);
            if (!dir.IsNullVector())
            {   // dir1 als senkrechte zu dir in der ActiveDrawingPlane erzeugen:
                GeoVector dir1 = base.ActiveDrawingPlane.Normal ^ dir;
                // nun daraus neue ellipse.plane, in der Orientierung dir ist der Winkel enthalten
                ellipse.Plane = new Plane(ellipse.Center, dir, dir1);
                return true;
            }
            return false;
        }

        private Angle OnGetAngle()
        {
            return base.Frame.ActiveView.Projection.DrawingPlane.Project(ellipse.Plane.DirectionX).Angle;
        }

        public override void OnSetAction()
        {
            ellipse = Ellipse.Construct();
            ellipse.Plane = base.ActiveDrawingPlane;
            ellipse.Center = ConstrDefaults.DefaultEllipseCenter;
            ellipse.MajorRadius = ConstrDefaults.DefaultEllipseMajorRadius;
            ellipse.MinorRadius = ConstrDefaults.DefaultEllipseMinorRadius;
            ellipse.StartParameter = 0;
            ellipse.SweepParameter = 2.0 * Math.PI;
            base.ActiveObject = ellipse;
            base.TitleId = "Constr.Ellipse.CenterRadius";

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);

            GeoPointInput elliCenter = new GeoPointInput("Constr.Ellipse.CenterRadius.Center");
            elliCenter.DefaultGeoPoint = ConstrDefaults.DefaultEllipseCenter;
            elliCenter.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Center);

            elliPointInput = new GeoPointInput("Constr.Ellipse.CenterRadius.Point");
            elliPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Point1);

            GeoPointInput elliPoint2 = new GeoPointInput("Constr.Ellipse.CenterRadius.Point2");
            elliPoint2.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Point2);
            elliPoint2.ForwardMouseInputTo = elliCenter;

            LengthInput elliMaxRad = new LengthInput("Constr.Ellipse.CenterRadius.MajorRadius");
            elliMaxRad.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(SetMajorRadius);
            elliMaxRad.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetMajorRadius);
            elliMaxRad.DefaultLength = ConstrDefaults.DefaultEllipseMajorRadius;
            elliMaxRad.Optional = true;
            elliMaxRad.ForwardMouseInputTo = elliCenter;

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


            base.SetInput(elliCenter, elliPointInput, elliPoint2, elliMaxRad, elliMinRad);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override string GetID()
        { return "Constr.Ellipse.CenterRadius"; }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}



