using CADability.GeoObject;
using CADability.UserInterface;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.Actions
{
    class ConstructPlaneOfCurve : ConstructAction, IIntermediateConstruction, IConstructPlane
    {
        private Plane plane;
        private Plane fplane;
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
        public delegate void PlaneChangedDelegate(Plane plane);
        public event PlaneChangedDelegate PlaneChangedEvent;
        public ConstructPlaneOfCurve(string resourceId)
        {
            base.TitleId = resourceId;
        }
        public override string GetID()
        {
            return "Construct.Plane.OfCurve";
        }

        public override void OnSetAction()
        {

            plane = base.ActiveDrawingPlane;
            feedBackPolyLine = Polyline.Construct();
            Rectangle rect = Frame.ActiveView.DisplayRectangle;
            width = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Width / 2.0;
            height = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Height / 2.0;
            feedBackplane = new FeedBackPlane(plane, width, height);
            RecalcFeedbackPolyLine();
            GeoObjectInput input = new GeoObjectInput("Construct.Plane.OfCurve");
            input.EdgesOnly = true;
            input.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(OnMouseOverGeoObjects);
            base.SetInput(input);
            base.FeedBack.Add(feedBackplane);
            base.OnSetAction();
        }

        private void CheckPlane()
        {
            try
            {
                fplane = plane;
                fplane.Location = fplane.ToGlobal(fplane.Project(base.CurrentMousePosition));
                RecalcFeedbackPolyLine();
                if (PlaneChangedEvent != null) PlaneChangedEvent(plane);
            }
            catch (PlaneException)
            { } // nix zu tun
        }
        bool OnMouseOverGeoObjects(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {
            GeoPoint p = base.CurrentMousePosition;
            for (int i = 0; i < TheGeoObjects.Length; ++i)
            {
                if (TheGeoObjects[i] is ICurve cv)
                {
                    if (cv.GetPlanarState()==PlanarState.Planar)
                    {
                        plane = cv.GetPlane();
                        CheckPlane();
                        return true;
                    }
                }
            }
            return false;
        }
        public Plane ConstructedPlane
        {
            get
            {
                return plane;
            }
        }

        #region IIntermediateConstruction Members

        IPropertyEntry IIntermediateConstruction.GetHandledProperty()
        {
            return null;
        }

        bool IIntermediateConstruction.Succeeded
        {
            get { return true; }
        }

        #endregion
    }
}
