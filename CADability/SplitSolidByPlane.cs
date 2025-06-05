using CADability.GeoObject;

namespace CADability.Actions
{
    public class SplitSolidByPlane : ConstructAction
    {
        private Plane plane; // die zentrale Ebene
        private Polyline feedBackPolyLine; // Darstellungsrechteck für die Ebene
        private FeedBackPlane feedBackplane;
        private double width, height; // Breite und Höhe der Ebene für Feedback
        private Solid toSplit;

        public SplitSolidByPlane(Solid toSplit)
        {
            this.toSplit = toSplit;
        }

        private void RecalcFeedbackPolyLine()
        {
            GeoPoint2D center = plane.Project(base.CurrentMousePosition); // aktueller Mauspunkt in der Ebene
            center = GeoPoint2D.Origin;
            GeoPoint[] p = new GeoPoint[4];
            p[0] = plane.ToGlobal(new GeoPoint2D(center.x + width / 2.0, center.y + height / 2.0));
            p[1] = plane.ToGlobal(new GeoPoint2D(center.x + width / 2.0, center.y - height / 2.0));
            p[2] = plane.ToGlobal(new GeoPoint2D(center.x - width / 2.0, center.y - height / 2.0));
            p[3] = plane.ToGlobal(new GeoPoint2D(center.x - width / 2.0, center.y + height / 2.0));
            feedBackPolyLine.SetPoints(p, true);// Ebene als Rechteck (4 Punkte) darstellen
            Plane fpln = new Plane(plane.ToGlobal(center), plane.DirectionX, plane.DirectionY);
            feedBackplane.Set(fpln, width, height);
        }
        bool SetPlaneInput(Plane val)
        {
            plane = val;
            RecalcFeedbackPolyLine();
            return true;
        }
        public override void OnSetAction()
        {
            //            base.ActiveObject = Polyline.Construct();
            base.TitleId = "Constr.PlaneIntersection";
            plane = base.ActiveDrawingPlane;
            feedBackPolyLine = Polyline.Construct();
            BoundingCube ext = toSplit.GetBoundingCube();
            width = ext.DiagonalLength;
            height = ext.DiagonalLength;
            base.ActiveObject = feedBackPolyLine;

            PlaneInput planeInput = new PlaneInput("Constr.Plane");
            planeInput.SetPlaneEvent += new PlaneInput.SetPlaneDelegate(SetPlaneInput);

            base.SetInput(planeInput);
            base.ShowAttributes = true;

            feedBackplane = new FeedBackPlane(plane, width, height);
            base.ShowActiveObject = false;
            base.OnSetAction();
            RecalcFeedbackPolyLine();
            base.Frame.SnapMode |= SnapPointFinder.SnapModes.SnapToFaceSurface;
        }
        public override void OnActivate(Action OldActiveAction, bool SettingAction)
        {
            base.FeedBack.Add(feedBackplane);
            base.OnActivate(OldActiveAction, SettingAction);
        }
        public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
            base.FeedBack.Remove(feedBackplane);
            base.OnInactivate(NewActiveAction, RemovingAction);
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
            base.Frame.SnapMode &= ~SnapPointFinder.SnapModes.SnapToFaceSurface;
        }
        public override string GetID()
        {
            return "Constr.SplitSolidByPlane";
        }

        public override void OnDone()
        {
            if (base.ActiveObject != null)
            {
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    CurrentMouseView.Canvas.Cursor = "WaitCursor";
                    GeoObjectList ToAdd = new GeoObjectList();
                    Model m = base.Frame.ActiveView.Model;
                    BRepOperation brepOp = new BRepOperation(toSplit.Shells[0], plane);
                    (Shell[] upper, Shell[] lower) = BRepOperation.SplitByPlane(toSplit.Shells[0], plane);
                    for (int i = 0; i < upper.Length; i++)
                    {
                        ToAdd.Add(Solid.MakeSolid(upper[i]));
                    }
                    for (int i = 0; i < lower.Length; i++)
                    {
                        ToAdd.Add(Solid.MakeSolid(lower[i]));
                    }
                    m.Remove(toSplit);
                    m.Add(ToAdd);
                    base.ActiveObject = null;
                    base.OnDone();
                }
            }
        }
    }
}
