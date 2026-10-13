using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;
using DragEventArgs = CADability.Substitutes.DragEventArgs;
using MouseButtons = CADability.Substitutes.MouseButtons;
using DragDropEffects = CADability.Substitutes.DragDropEffects;
using Keys = CADability.Substitutes.Keys;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a circular arc through three points: the user picks the start point, a point on the arc and the end
    /// point. Centre and radius are shown as read-only values. Menu id: "Constr.Arc.3Points".
    /// </summary>
    internal class ConstrArc3Points : ConstructAction
    {
        private GeoPoint arcPoint1;
        private GeoPoint arcPoint2;
        private GeoPoint arcPoint3;
        private GeoPointInput arcPoint1Input;
        private GeoPointInput arcPoint2Input;
        private GeoPointInput arcPoint3Input;
        private CADability.GeoObject.Point gPoint1;
        private CADability.GeoObject.Point gPoint2;


        private Ellipse arc;

        public ConstrArc3Points()
        { }

        private void showArc(GeoPoint p1)
        {   // zunächst Punkte sammeln, um unabhängig von der Erstellungsreihenfolge zu sein!
            ArrayList p = new ArrayList();
            if (arcPoint1Input.Fixed) p.Add(arcPoint1);
            if (arcPoint2Input.Fixed) p.Add(arcPoint2);
            if (arcPoint3Input.Fixed) p.Add(arcPoint3);
            if (p.Count == 0)
            {   // prototyp darstellen
                p1.x = p1.x + base.WorldViewSize / 20;
                arc.SetArcPlaneCenterRadius(base.ActiveDrawingPlane, p1, base.WorldViewSize / 20);
            }
            if (p.Count == 1)
            {   // Kreis aus 2 Punkten darstellen, damit man was sieht
                try
                {
                    arc.SetArcPlane2Points(base.ActiveDrawingPlane, (GeoPoint)p[0], p1);
                }
                catch (EllipseException) { }
                gPoint1.Location = (GeoPoint)p[0];
            }
            if (p.Count >= 2)
            {   // hier der eigentliche Lösungsfall:	
                try
                //				{ arc.SetArc3Points((GeoPoint)p[0],(GeoPoint)p[1],p1,base.ActiveDrawingPlane);	}
                { arc.SetArc3Points(arcPoint1, arcPoint2, arcPoint3, base.ActiveDrawingPlane); }
                catch (EllipseException) { }
                gPoint2.Location = (GeoPoint)p[1];
            }
        }

        private void ArcPoint1(GeoPoint p)
        {
            arcPoint1 = p;
            showArc(arcPoint1);
        }
        private void ArcPoint2(GeoPoint p)
        {
            arcPoint2 = p;
            showArc(arcPoint2);
        }
        private void ArcPoint3(GeoPoint p)
        {
            arcPoint3 = p;
            showArc(arcPoint3);
        }

        private GeoPoint ArcCenter()
        { return arc.Center; }

        private double ArcRadius()
        { return arc.MajorRadius; }

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
                            pln = Plane.FromPoints(new GeoPoint[] { arcPoint1, arcPoint2, (curve as Line).StartPoint, (curve as Line).EndPoint }, out eps, out linear);
                            plnOK = !(linear || eps > Precision.eps);
                        }
                        if (curve is Ellipse)
                        {
                            pln = (curve as Ellipse).GetPlane();
                            // alles in einer Ebene? 
                            plnOK = !((pln.Distance(arcPoint1) > Precision.eps) || (pln.Distance(arcPoint2) > Precision.eps));
                        }
                        if (plnOK)
                        {   // Hilfskreise machen um die ersten 2 Punkte:
                            Ellipse circLoc1;
                            circLoc1 = Ellipse.Construct();
                            circLoc1.SetCirclePlaneCenterRadius(pln, arcPoint1, 0.0);
                            Ellipse circLoc2;
                            circLoc2 = Ellipse.Construct();
                            circLoc2.SetCirclePlaneCenterRadius(pln, arcPoint2, 0.0);
                            // Achtung! Die Sortierung mit evtl. Linie(n) am Anfang ist wichtig für die Auswertung des Abstandes!!
                            ICurve2D l2D1 = (l[i] as ICurve).GetProjectedCurve(pln);
                            ICurve2D l2D2 = circLoc1.GetProjectedCurve(pln);
                            ICurve2D l2D3 = circLoc2.GetProjectedCurve(pln);
                            GeoPoint2D[] tangentPoints = Curves2D.TangentCircle(l2D1, l2D2, l2D3, pln.Project(inpoint), pln.Project(arcPoint1), pln.Project(arcPoint2));
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
            arc = Ellipse.Construct();
            arc.SetArcPlaneCenterRadiusAngles(base.ActiveDrawingPlane, new GeoPoint(0.0, 0.0, 0.0), base.WorldViewSize / 20, 0.0, 1.5 * Math.PI);
            base.ActiveObject = arc;
            base.TitleId = "Constr.Arc.3Points";

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            gPoint2 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);
            base.FeedBack.Add(gPoint2);

            arcPoint1Input = new GeoPointInput("Constr.Arc.Point1");
            arcPoint1Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(ArcPoint1);
            arcPoint2Input = new GeoPointInput("Constr.Arc.Point2");
            arcPoint2Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(ArcPoint2);
            arcPoint3Input = new GeoPointInput("Constr.Arc.3Points.Point3");
            arcPoint3Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(ArcPoint3);

            GeoPointInput arcCenter = new GeoPointInput("Constr.Arc.Center");
            arcCenter.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(ArcCenter);
            arcCenter.Optional = true;
            arcCenter.ReadOnly = true;

            LengthInput len = new LengthInput("Constr.Arc.Radius");
            len.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(ArcRadius);
            len.Optional = true;
            len.ReadOnly = true;

            base.SetInput(arcPoint1Input, arcPoint2Input, arcPoint3Input, arcCenter, len);
            base.ShowAttributes = true;

            base.OnSetAction();
        }
        public override string GetID()
        {
            return "Constr.Arc.3Points";
        }

        public override void OnDone()
        {
            base.OnDone();
        }

    }
}
