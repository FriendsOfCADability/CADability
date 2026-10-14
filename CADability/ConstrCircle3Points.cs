using CADability.Curve2D;
using CADability.GeoObject;
using System.Collections;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a circle through three points picked by the user. Centre and radius are shown as read-only values.
    /// Menu id: "Constr.Circle.3Points".
    /// </summary>
    internal class ConstrCircle3Points : ConstructAction
    {
        private GeoPoint circlePoint1;
        private GeoPoint circlePoint2;
        private GeoPoint circlePoint3;
        private GeoPointInput arcPoint1Input;
        private GeoPointInput arcPoint2Input;
        private GeoPointInput arcPoint3Input;
        private CADability.GeoObject.Point gPoint1;
        private CADability.GeoObject.Point gPoint2;

        private Ellipse circle;

        public ConstrCircle3Points()
        { }

        private void showCircle(GeoPoint p1)
        {
            ArrayList p = new ArrayList();
            if (arcPoint1Input.Fixed) p.Add(circlePoint1);
            if (arcPoint2Input.Fixed) p.Add(circlePoint2);
            if (arcPoint3Input.Fixed) p.Add(circlePoint3);
            if (p.Count == 0)
            {   // prototyp, um was zu sehen
                p1.x = p1.x + base.WorldViewSize / 20;
                circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, p1, base.WorldViewSize / 20);
            }
            if (p.Count == 1)
            {   // prototyp, um was zu sehen
                try
                {
                    circle.SetCirclePlane2Points(base.ActiveDrawingPlane, (GeoPoint)p[0], p1);
                }
                catch (EllipseException) { }
                gPoint1.Location = (GeoPoint)p[0];
            }
            if (p.Count >= 2)
            {
                try
                {
                    circle.SetCircle3Points((GeoPoint)p[0], (GeoPoint)p[1], p1, base.ActiveDrawingPlane);
                }
                catch (EllipseException) { }
                gPoint2.Location = (GeoPoint)p[1];
            }

        }

        private void CirclePoint1(GeoPoint p)
        {
            circlePoint1 = p;
            showCircle(circlePoint1);
        }

        private void CirclePoint2(GeoPoint p)
        {
            circlePoint2 = p;
            showCircle(circlePoint2);
        }

        private void CirclePoint3(GeoPoint p)
        {
            circlePoint3 = p;
            showCircle(circlePoint3);
        }

        private GeoPoint CircleCenter()
        { return circle.Center; }

        private double CircleRadius()
        { return circle.MajorRadius; }


        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            // nur beim dritten Punkt:
            if (CurrentInput == arcPoint3Input && arcPoint1Input.Fixed && arcPoint2Input.Fixed)
            {
                GeoObjectList l = base.GetObjectsUnderCursor(e.Location);
                l.DecomposeAll();
                GeoPoint inpoint = base.WorldPoint(e.Location);
                Plane pln = Plane.XYPlane;
                bool plnOK = false;
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] is ICurve)
                    {
                        ICurve curve = l[i] as ICurve;
                        if (curve is Line)
                        {
                            double eps;
                            bool linear;
                            // alles in einer Ebene? In welcher?
                            pln = Plane.FromPoints(new GeoPoint[] { circlePoint1, circlePoint2, (curve as Line).StartPoint, (curve as Line).EndPoint }, out eps, out linear);
                            plnOK = !(linear || eps > Precision.eps);
                        }
                        if (curve is Ellipse)
                        {
                            pln = (curve as Ellipse).GetPlane();
                            // alles in einer Ebene? 
                            plnOK = !((pln.Distance(circlePoint1) > Precision.eps) || (pln.Distance(circlePoint2) > Precision.eps));
                        }
                        if (plnOK)
                        {   // Hilfskreise machen um die ersten 2 Punkte:
                            Ellipse circLoc1;
                            circLoc1 = Ellipse.Construct();
                            circLoc1.SetCirclePlaneCenterRadius(pln, circlePoint1, 0.0);
                            Ellipse circLoc2;
                            circLoc2 = Ellipse.Construct();
                            circLoc2.SetCirclePlaneCenterRadius(pln, circlePoint2, 0.0);
                            // Achtung! Die Sortierung mit Linie(n) am Anfang ist wichtig für die Auswertung des Abstandes!!
                            ICurve2D l2D1 = (l[i] as ICurve).GetProjectedCurve(pln);
                            ICurve2D l2D2 = circLoc1.GetProjectedCurve(pln);
                            ICurve2D l2D3 = circLoc2.GetProjectedCurve(pln);
                            GeoPoint2D[] tangentPoints = Curves2D.TangentCircle(l2D1, l2D2, l2D3, pln.Project(inpoint), pln.Project(circlePoint1), pln.Project(circlePoint2));
                            if (tangentPoints.Length > 0)
                            {
                                for (int k = 0; k < tangentPoints.Length; k += 4)
                                {
                                    double dist = Geometry.Dist(tangentPoints[k + 1], pln.Project(inpoint));
                                    if (dist < mindist)
                                    {
                                        mindist = dist;
                                        found = pln.ToGlobal(tangentPoints[k + 1]);
                                    }
                                }
                            }

                        }
                    }
                }
            }
            return mindist != double.MaxValue;
        }



        public override void OnSetAction()
        {
            circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, new GeoPoint(0.0, 0.0, 0.0), base.WorldViewSize / 20);
            base.ActiveObject = circle;
            base.TitleId = "Constr.Circle.3Points";

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            gPoint2 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);
            base.FeedBack.Add(gPoint2);

            arcPoint1Input = new GeoPointInput("Constr.Circle.Point1");
            arcPoint1Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint1);
            arcPoint2Input = new GeoPointInput("Constr.Circle.Point2");
            arcPoint2Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint2);
            arcPoint3Input = new GeoPointInput("Constr.Circle.3Points.Point3");
            arcPoint3Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint3);
            GeoPointInput arcCenterInput = new GeoPointInput("Constr.Circle.Center");
            arcCenterInput.GetGeoPointEvent += new ConstructAction.GeoPointInput.GetGeoPointDelegate(CircleCenter);
            arcCenterInput.Optional = true;
            arcCenterInput.ReadOnly = true;
            LengthInput arcRadius = new LengthInput("Constr.Circle.Radius");
            arcRadius.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(CircleRadius);
            arcRadius.Optional = true;
            arcRadius.ReadOnly = true;
            base.SetInput(arcPoint1Input, arcPoint2Input, arcPoint3Input, arcCenterInput, arcRadius);
            base.ShowAttributes = true;

            base.OnSetAction();
        }
        public override string GetID()
        {
            return "Constr.Circle.3Points";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}
