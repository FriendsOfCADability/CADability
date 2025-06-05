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
    /// 
    /// </summary>

    public class ConstructPlane : ConstructAction, IIntermediateConstruction, IConstructPlane
    {
        public class ConstructPlaneException : ApplicationException
        {
            public ConstructPlaneException(string Message) : base(Message) { }
        }
        private Plane plane;
        private Plane fplane;
        private GeoPointInput input1;
        private GeoPointInput input2;
        private GeoPointInput input3;
        GeoPoint p1, p2, p3;
        private Polyline feedBackPolyLine;
        private FeedBackPlane feedBackplane;
        private double width, height; // Breite und Höhe der Ebene für Feedback

        private void RecalcFeedbackPolyLine()
        {
            GeoPoint2D center = plane.Project(base.CurrentMousePosition);
            GeoPoint[] p = new GeoPoint[4];
            p[0] = plane.ToGlobal(new GeoPoint2D(center.x + width / 2.0, center.y + height / 2.0));
            p[1] = plane.ToGlobal(new GeoPoint2D(center.x + width / 2.0, center.y - height / 2.0));
            p[2] = plane.ToGlobal(new GeoPoint2D(center.x - width / 2.0, center.y - height / 2.0));
            p[3] = plane.ToGlobal(new GeoPoint2D(center.x - width / 2.0, center.y + height / 2.0));
            feedBackPolyLine.SetPoints(p, true);// Ebene als Rechteck (4 Punkte) darstellen
            feedBackplane.Set(fplane, width, height);
        }

        private void CheckPlane()
        {
            try
            {
                plane = new Plane(p1, p2, p3);
                fplane = new Plane(p1, p2, p3);
                GeoPoint2D p21 = plane.Project(p1);
                GeoPoint2D p22 = plane.Project(p2);
                GeoPoint2D p23 = plane.Project(p3);
                GeoPoint2D cnt = new GeoPoint2D((p21.x + p22.x + p23.x) / 3.0, (p21.y + p22.y + p23.y) / 3.0);
                fplane.Location = fplane.ToGlobal(cnt);
                plane.Location = p1;
                // zumindest für das Feedbackobjekt sollte die Ebene zwischen den 3 Punkten liegen
                RecalcFeedbackPolyLine();
                if (PlaneChangedEvent != null) PlaneChangedEvent(plane);
            }
            catch (PlaneException)
            { } // nix zu tun
        }
        public delegate void PlaneChangedDelegate(Plane plane);
        public event PlaneChangedDelegate PlaneChangedEvent;
        public ConstructPlane(string resourceId)
        {
            base.TitleId = resourceId;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.GetID ()"/>
        /// </summary>
        /// <returns></returns>
		public override string GetID()
        {
            return "Construct.Plane.3Points";
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
        /// </summary>
		public override void OnSetAction()
        {

            plane = base.ActiveDrawingPlane;
            feedBackPolyLine = Polyline.Construct();
            Rectangle rect = Frame.ActiveView.DisplayRectangle;
            width = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Width / 2.0;
            height = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Height / 2.0;
            feedBackplane = new FeedBackPlane(plane, width, height);
            RecalcFeedbackPolyLine();
            //base.FeedBack.Add(feedBackPolyLine);

            input1 = new GeoPointInput("Construct.Plane.Origin");
            input1.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetOriginPoint);
            input1.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetOriginPoint);
            input2 = new GeoPointInput("Construct.Plane.DircetionX");
            input2.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetDircetionXPoint);
            input2.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetDircetionXPoint);
            input3 = new GeoPointInput("Construct.Plane.DircetionY");
            input3.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetDircetionYPoint);
            input3.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetDircetionYPoint);
            base.SetInput(input1, input2, input3);
            //base.FeedBack.Add(feedBackPolyLine);
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
            if (input2.Fixed && input3.Fixed) CheckPlane();
            else
            {   // der Normalfall: 1. Punkt wird eingegeben, 2. und 3. nicht besetzt
                plane = base.ActiveDrawingPlane;
                plane.Location = p1;
                RecalcFeedbackPolyLine();
                if (PlaneChangedEvent != null) PlaneChangedEvent(plane);
            }
        }

        private GeoPoint OnGetDircetionXPoint()
        {
            return p2;
        }

        private void OnSetDircetionXPoint(GeoPoint p)
        {
            p2 = p;
            if (input1.Fixed && input3.Fixed) CheckPlane();
            else
            {   // der Normalfall: 1. und 2. Punkt gegeben, 3. noch nicht
                try
                {
                    plane = new Plane(new GeoPoint((p1.x + p2.x) / 2.0, (p1.y + p2.y) / 2.0, (p1.z + p2.z) / 2.0), p2 - p1, (p2 - p1) ^ base.ActiveDrawingPlane.Normal);
                    RecalcFeedbackPolyLine();
                    if (PlaneChangedEvent != null) PlaneChangedEvent(plane);
                }
                catch (PlaneException) { }
            }
        }

        private GeoPoint OnGetDircetionYPoint()
        {
            return p3;
        }

        private void OnSetDircetionYPoint(GeoPoint p)
        {
            p3 = p;
            if (input1.Fixed && input2.Fixed) CheckPlane();
            // wenn die beiden anderen noch nicht gesetzt sind, gibts halt kein Feedback
        }
        public Plane ConstructedPlane
        {
            get
            {
                if (input1.Fixed && input2.Fixed && input3.Fixed)
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
                return (input1.Fixed && input2.Fixed && input3.Fixed && plane.IsValid());
            }
        }

        #endregion
    }
}
