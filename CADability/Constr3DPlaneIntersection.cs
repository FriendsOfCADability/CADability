using CADability.GeoObject;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.Actions
{
    internal class Constr3DPlaneIntersection : ConstructAction
    {
        private Plane plane; // die zentrale Ebene
        private Polyline feedBackPolyLine; // Darstellungsrechteck für die Ebene
        private FeedBackPlane feedBackplane;
        private double width, height; // Breite und Höhe der Ebene für Feedback

        public Constr3DPlaneIntersection()
        {
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
            Rectangle rect = Frame.ActiveView.DisplayRectangle; // sinnvolles Standardrechteck
            width = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Width / 2.0;
            height = Frame.ActiveView.Projection.DeviceToWorldFactor * rect.Height / 2.0;
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
        { return "Constr.PlaneIntersection"; }
        private ICurve[] Intersect(IGeoObject go, PlaneSurface pls)
        {
            Plane plane = pls.Plane;
            if (go is Solid)
            {
                BoundingBox bc = go.GetBoundingCube();
                if (bc.Interferes(plane))
                {
                    return (go as Solid).GetPlaneIntersection(pls);
                }
            }
            if (go is Shell)
            {
                BoundingBox bc = go.GetBoundingCube();
                if (bc.Interferes(plane))
                {
                    return (go as Shell).GetPlaneIntersection(pls);
                }
            }
            if (go is Face)
            {
                BoundingBox bc = go.GetBoundingCube();
                if (bc.Interferes(plane))
                {
                    return (go as Face).GetPlaneIntersection(pls);
                }
            }
            List<ICurve> res = new List<ICurve>();
            if (go is Block)
            {
                for (int i = 0; i < go.NumChildren; i++)
                {
                    res.AddRange(Intersect(go.Child(i), pls));
                }
            }
            return res.ToArray();
        }
        public override void OnDone()
        {
            if (base.ActiveObject != null)
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    Frame.ActiveView.Canvas.Cursor = "WaitCursor";
                    GeoObjectList ToAdd = new GeoObjectList();
                    Model m = base.Frame.ActiveView.Model;
                    PlaneSurface pls = new PlaneSurface(plane);
                    for (int i = 0; i < m.Count; ++i) // durch das ganze Modell laufen
                    {
                        ICurve[] crvs = Intersect(m[i], pls);
                        if (crvs != null)
                        {
                            for (int k = 0; k < crvs.Length; k++)
                            {
                                IGeoObject go = crvs[k] as IGeoObject;
                                go.CopyAttributes(base.ActiveObject);
                                ToAdd.Add(go);
                            }
                        }
                    }
                    m.Add(ToAdd);
                    base.KeepAsLastStyle(ActiveObject);
                    base.ActiveObject = null;
                    base.OnDone();
                }
        }
    }
}



