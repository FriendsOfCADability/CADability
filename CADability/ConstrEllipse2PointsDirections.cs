using CADability.Attribute;
using CADability.GeoObject;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.Actions
{
    /// <summary>
    /// Construction of a full ellipse from two points on the ellipse and the tangent directions at these
    /// points (see <see cref="Ellipse.SetEllipse2PointsDirections"/>). The user enters the first point, the
    /// direction there, the second point and the direction there; the points are shown as feedback while
    /// the ellipse is not yet defined. Started with the action id "Constr.Ellipse.2PointsDirections"
    /// (menu command "MenuId.Constr.Ellipse.2PointsDirections").
    /// </summary>
    internal class ConstrEllipse2PointsDirections : ConstructAction
    {
        //Gerhard: Optional sollte aber über TAB und konkrete Aktivierung erreichbar sein! (siehe Winkel und radius der Ellipse hier)

        private Ellipse ellipse;
        private GeoPoint point1;
        private GeoPoint point2;
        private GeoVector vector1;
        private GeoVector vector2;
        private CADability.GeoObject.Point gPoint1;
        private CADability.GeoObject.Point gPoint2;
        private Line feedBackLine;

        public ConstrEllipse2PointsDirections()
        { }

        private Boolean showEllipse()
        {
            if (!Precision.IsEqual(point1, point2) && !vector1.IsNullVector() && !vector2.IsNullVector())
            {
                return ellipse.SetEllipse2PointsDirections(point1, vector1, point2, vector2);
            }
            return false;
        }

        private void SetPoint1(GeoPoint p)
        {
            point1 = p;
            base.BasePoint = p; // für die Richtung
            gPoint1.Location = p;
            showEllipse();
        }



        private bool SetElliDir1(GeoVector dir)
        {
            if (!dir.IsNullVector())
            {
                vector1 = dir;
                feedBackLine.SetTwoPoints(point1, base.CurrentMousePosition);
                return showEllipse();
            }
            return false;
        }

        private GeoVector GetElliDir1()
        {
            return vector1;
        }

        private void SetPoint2(GeoPoint p)
        {
            point2 = p;
            base.BasePoint = p; // für die Winkelberechnung
            gPoint2.Location = p;
            showEllipse();
        }



        private bool SetElliDir2(GeoVector dir)
        {
            if (!dir.IsNullVector())
            {
                vector2 = dir;
                feedBackLine.SetTwoPoints(point2, base.CurrentMousePosition);
                return showEllipse();
            }
            return false;
        }

        private GeoVector GetElliDir2()
        {
            return vector2;
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
            point1 = ConstrDefaults.DefaultEllipseCenter;
            point2 = point1;
            point2.x = point1.x + ConstrDefaults.DefaultEllipseMajorRadius;
            vector1 = base.ActiveDrawingPlane.DirectionX;
            vector2 = base.ActiveDrawingPlane.DirectionY;
            //           direction2 = direction1;


            base.ActiveObject = ellipse;
            base.TitleId = "Constr.Ellipse.2PointsDirections";

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            gPoint2 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);
            base.FeedBack.Add(gPoint2);
            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            base.FeedBack.Add(feedBackLine);

            GeoPointInput elliPoint1 = new GeoPointInput("Constr.Ellipse.2PointsDirections.Point1");
            elliPoint1.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetPoint1);

            GeoVectorInput elliDir1 = new GeoVectorInput("Constr.Ellipse.2PointsDirections.Direction1");
            elliDir1.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(SetElliDir1);
            elliDir1.GetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.GetGeoVectorDelegate(GetElliDir1);

            GeoPointInput elliPoint2 = new GeoPointInput("Constr.Ellipse.2PointsDirections.Point2");
            elliPoint2.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(SetPoint2);

            GeoVectorInput elliDir2 = new GeoVectorInput("Constr.Ellipse.2PointsDirections.Direction2");
            elliDir2.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(SetElliDir2);
            elliDir2.GetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.GetGeoVectorDelegate(GetElliDir2);

            base.SetInput(elliPoint1, elliDir1, elliPoint2, elliDir2);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override string GetID()
        { return "Constr.Ellipse.2PointsDirections"; }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}




