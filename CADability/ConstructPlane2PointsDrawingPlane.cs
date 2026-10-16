using CADability.UserInterface;
using System;
using System.Drawing;
using Point = System.Drawing.Point;

namespace CADability.Actions
{
    /// <summary>
    /// Intermediate construction of a plane by two points: the plane contains the line through the two points the user picks and
    /// is perpendicular to the active drawing plane (its x-axis is the direction from the first to the second point, its y-axis
    /// the normal of the drawing plane, its origin the midpoint of the two points). It is started by a <see cref="ConstructAction.PlaneInput"/>
    /// (menu id "MenuId.Plane.2PointsDrawingPlane") to define a plane while another construction is running; the resulting plane
    /// is reported via <see cref="PlaneChangedEvent"/>, and the plane is shown as a feedback rectangle while the points are entered.
    /// Its id is "Construct.Plane.2PointsDrawingPlane".
    /// </summary>

    public class ConstructPlane2PointsDrawingPlane : CADability.Actions.ConstructAction, IIntermediateConstruction, IConstructPlane
    {
        /// <summary>
        /// Thrown by <see cref="ConstructedPlane"/> when the plane is requested before both points are fixed or when the points did not
        /// define a valid plane.
        /// </summary>
        public class ConstructPlaneException : ApplicationException
        {
            public ConstructPlaneException(string Message) : base(Message) { }
        }
        private Plane plane;
        private GeoPointInput input1;
        private GeoPointInput input2;
        GeoPoint p1, p2;
        private FeedBackPlane feedBackplane;
        private double width, height; // Breite und Höhe der Ebene für Feedback

        private void RecalcFeedbackPlane()
        {
            feedBackplane.Set(plane, width, height);
        }

        private void CheckPlane()
        {
            try
            {
                plane = new Plane(new GeoPoint(p1, p2), p2 - p1, base.ActiveDrawingPlane.Normal);
                RecalcFeedbackPlane();
                if (PlaneChangedEvent != null) PlaneChangedEvent(plane);
            }
            catch (PlaneException)
            { } // nix zu tun
        }
        /// <summary>
        /// Signature of the handlers of <see cref="PlaneChangedEvent"/>: called with the current plane each time one of the two points
        /// changes.
        /// </summary>
        /// <param name="plane">The plane as currently defined by the two points</param>
        public delegate void PlaneChangedDelegate(Plane plane);
        public event PlaneChangedDelegate PlaneChangedEvent;
        public ConstructPlane2PointsDrawingPlane()
        {
        }
        /// <inheritdoc/>
		public override string GetID()
        {
            return "Construct.Plane.2PointsDrawingPlane";
        }

        /// <inheritdoc/>
		public override void OnSetAction()
        {

            plane = base.ActiveDrawingPlane;
            Rectangle rect = Frame.ActiveView.DisplayRectangle;
            width = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Width / 2.0;
            height = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Height / 2.0;
            feedBackplane = new FeedBackPlane(plane, width, height);
            RecalcFeedbackPlane();

            base.TitleId = "Construct.Plane.2PointsDrawingPlane";

            input1 = new GeoPointInput("Construct.Plane.Origin");
            input1.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetOriginPoint);
            input1.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetOriginPoint);
            input2 = new GeoPointInput("Construct.Plane.2PointsDrawingPlane.Point2");
            input2.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetPoint2);
            input2.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetPoint2);
            base.SetInput(input1, input2);
            base.FeedBack.Add(feedBackplane);
            base.OnSetAction();
        }
        /// <inheritdoc/>
        public override bool AutoRepeat()
        {
            return false;
        }

        private GeoPoint OnGetOriginPoint()
        {
            return p1;
        }
        private void OnSetOriginPoint(GeoPoint p)
        {
            p1 = p;
            if (input2.Fixed) CheckPlane();
            else
            {
                plane = new Plane(p1, base.ActiveDrawingPlane.DirectionX, base.ActiveDrawingPlane.Normal);
                RecalcFeedbackPlane();
                if (PlaneChangedEvent != null) PlaneChangedEvent(plane);
            }
        }

        private GeoPoint OnGetPoint2()
        {
            return p2;
        }

        private void OnSetPoint2(GeoPoint p)
        {
            p2 = p;
            if (input1.Fixed) CheckPlane();
        }

        public Plane ConstructedPlane
        {
            get
            {
                if (input1.Fixed && input2.Fixed)
                {
                    if (plane.IsValid()) return plane;
                }
                throw new ConstructPlaneException("Plane was not successfully build");
            }
        }

        #region IIntermediateConstruction Members

        IPropertyEntry IIntermediateConstruction.GetHandledProperty()
        {
            return null;
        }

        bool IIntermediateConstruction.Succeeded
        {
            get
            {
                return (input1.Fixed && input2.Fixed && plane.IsValid());
            }
        }

        #endregion
    }
}

