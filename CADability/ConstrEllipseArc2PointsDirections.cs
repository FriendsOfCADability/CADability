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
    /// Construction of an elliptical arc from two points on the ellipse and the tangent directions at these points.
    /// The user enters the first point and its direction, the second point and its direction and the direction of the
    /// arc (counterclockwise or clockwise); the ellipse lies in the active drawing plane.
    /// Menu id: "Constr.Ellipsearc.2PointsDirections".
    /// </summary>
    internal class ConstrEllipseArc2PointsDirections : ConstructAction
    {
        private Ellipse ellipse;
        private GeoPoint point1;
        private GeoPoint point2;
        private GeoVector vector1;
        private GeoVector vector2;
        private CADability.GeoObject.Point gPoint1;
        private CADability.GeoObject.Point gPoint2;
        private Line feedBackLine;
        private Boolean dir;

        public ConstrEllipseArc2PointsDirections()
        { }

        private Boolean showEllipse()
        {
            if (!Precision.IsEqual(point1, point2) && !vector1.IsNullVector() && !vector2.IsNullVector())
            {
                if (ellipse.SetEllipseArc2PointsDirections(point1, vector1, point2, vector2, dir, base.ActiveDrawingPlane))
                {
                    base.MultiSolutionCount = 2; // zur schnellen Umschaltung der Richtung in OnDifferentSolution, s.u.
                    return true;
                }
            }
            base.MultiSolutionCount = 0;
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

        private void SetDirection(bool val)
        {
            dir = val;
            showEllipse();
        }

        bool GetDirection()
        {
            return dir;
        }


        public override void OnSolution(int sol)
        {
            if (sol == -1) dir = !dir;
            else
                if (sol == -2) dir = !dir;
            else dir = (sol & 1) != 0;
            showEllipse();
        }


        public override void OnSetAction()
        {
            ellipse = Ellipse.Construct();
            dir = ConstrDefaults.DefaultArcDirection;
            point1 = ConstrDefaults.DefaultEllipseCenter;
            point2 = point1;
            point2.x = point1.x + ConstrDefaults.DefaultEllipseMajorRadius;
            point2.y = point1.y + ConstrDefaults.DefaultEllipseMinorRadius;
            vector1 = base.ActiveDrawingPlane.DirectionY;
            vector2 = base.ActiveDrawingPlane.DirectionX;
            showEllipse();


            base.ActiveObject = ellipse;
            base.TitleId = "Constr.Ellipsearc.2PointsDirections";

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);
            gPoint2 = ActionFeedBack.FeedbackPoint(base.Frame);
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

            BooleanInput dirInput = new BooleanInput("Constr.Arc.Direction", "Constr.Arc.Direction.Values");
            dirInput.DefaultBoolean = ConstrDefaults.DefaultArcDirection;
            dirInput.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetDirection);
            dirInput.GetBooleanEvent += new BooleanInput.GetBooleanDelegate(GetDirection);

            base.SetInput(elliPoint1, elliDir1, elliPoint2, elliDir2, dirInput);
            base.ShowAttributes = true;
            base.OnSetAction();
        }

        public override string GetID()
        { return "Constr.Ellipsearc.2PointsDirections"; }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}





