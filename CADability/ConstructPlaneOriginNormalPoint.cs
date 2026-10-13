using CADability.GeoObject;
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
    /// Intermediate construction of a <see cref="Plane"/> from its origin and a second point: the vector
    /// from the origin to that point is the normal of the plane. It is started by the plane input of another
    /// construction action (see <see cref="IIntermediateConstruction"/>), e.g. when a new drawing plane is
    /// defined, and shows the plane as a feedback rectangle around the mouse position while the points are
    /// entered. The result is available in <see cref="ConstructedPlane"/> once both points are fixed, and
    /// <see cref="PlaneChangedEvent"/> reports every change. The action does not auto-repeat. Started with
    /// the action id "Construct.Plane.OriginNormalPoint" (menu command "MenuId.Plane.OriginNormalPoint").
    /// </summary>

    public class ConstructPlaneOriginNormalPoint : CADability.Actions.ConstructAction, IIntermediateConstruction, IConstructPlane
    {
        public class ConstructPlaneException : ApplicationException
        {
            public ConstructPlaneException(string Message) : base(Message) { }
        }
        private Plane plane;
        private GeoPointInput input1;
        private GeoPointInput input2;
        GeoPoint p1, p2;
        private Polyline feedBackPolyLine;
        private FeedBackPlane feedBackplane;
        private double width, height; // Breite und Höhe der Ebene für Feedback

        private void RecalcFeedbackPlane()
        {
            GeoPoint2D center = plane.Project(base.CurrentMousePosition);
            GeoPoint[] p = new GeoPoint[4];
            p[0] = plane.ToGlobal(new GeoPoint2D(center.x + width / 2.0, center.y + height / 2.0));
            p[1] = plane.ToGlobal(new GeoPoint2D(center.x + width / 2.0, center.y - height / 2.0));
            p[2] = plane.ToGlobal(new GeoPoint2D(center.x - width / 2.0, center.y - height / 2.0));
            p[3] = plane.ToGlobal(new GeoPoint2D(center.x - width / 2.0, center.y + height / 2.0));
            feedBackPolyLine.SetPoints(p, true);// Ebene als Rechteck (4 Punkte) darstellen
            feedBackplane.Set(plane, width, height);
        }

        private void CheckPlane()
        {
            try
            {
                plane = new Plane(p1, p2 - p1);
                RecalcFeedbackPlane();
                if (PlaneChangedEvent != null) PlaneChangedEvent(plane);
            }
            catch (PlaneException)
            { } // nix zu tun
        }
        public delegate void PlaneChangedDelegate(Plane plane);
        public event PlaneChangedDelegate PlaneChangedEvent;
        public ConstructPlaneOriginNormalPoint()
        {
        }
        /// <inheritdoc/>
		public override string GetID()
        {
            return "Construct.Plane.OriginNormalPoint";
        }
        /// <inheritdoc/>
        public override bool AutoRepeat()
        {
            return false;
        }
        /// <inheritdoc/>
		public override void OnSetAction()
        {

            plane = base.ActiveDrawingPlane;
            feedBackPolyLine = Polyline.Construct();
            Rectangle rect = Frame.ActiveView.DisplayRectangle;
            width = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Width / 2.0;
            height = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Height / 2.0;
            feedBackplane = new FeedBackPlane(plane, width, height);
            RecalcFeedbackPlane();

            base.TitleId = "Construct.Plane.OriginNormalPoint";

            input1 = new GeoPointInput("Construct.Plane.Origin");
            input1.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetOriginPoint);
            input1.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetOriginPoint);
            input2 = new GeoPointInput("Construct.Plane.OriginNormalPoint.NormalPoint");
            input2.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetPoint2);
            input2.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetPoint2);
            base.SetInput(input1, input2);
            base.FeedBack.Add(feedBackplane);
            base.OnSetAction();
        }

        private GeoPoint OnGetOriginPoint()
        {
            return p1;
        }
        private void OnSetOriginPoint(GeoPoint p)
        {
            p1 = p;
            if (input2.Fixed) CheckPlane();
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


