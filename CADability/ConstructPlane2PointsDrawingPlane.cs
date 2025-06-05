using CADability.UserInterface;
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
    /// 
    /// </summary>

    public class ConstructPlane2PointsDrawingPlane : CADability.Actions.ConstructAction, IIntermediateConstruction, IConstructPlane
    {
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
        public delegate void PlaneChangedDelegate(Plane plane);
        public event PlaneChangedDelegate PlaneChangedEvent;
        public ConstructPlane2PointsDrawingPlane()
        {
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.GetID ()"/>
        /// </summary>
        /// <returns></returns>
		public override string GetID()
        {
            return "Construct.Plane.2PointsDrawingPlane";
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
        /// </summary>
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
        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.AutoRepeat ()"/>
        /// </summary>
        /// <returns></returns>
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

