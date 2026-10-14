using CADability.Curve2D;
using CADability.GeoObject;
using System.Collections;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;


namespace CADability.Actions
{
    /// <summary>
    /// Construction of a circle from two diametrically opposite points: the user picks two points, the circle has their
    /// connecting line as diameter and lies in the active drawing plane. Centre and radius are shown as read-only values.
    /// Menu id: "Constr.Circle.2Points".
    /// </summary>


    internal class ConstrCircle2Points : ConstructAction
    {

        private GeoPoint circlePoint1;
        private GeoPoint circlePoint2;
        private Ellipse circle;
        private GeoPointInput arcPoint1Input;
        private GeoPointInput arcPoint2Input;
        private CADability.GeoObject.Point gPoint1;

        public ConstrCircle2Points()
        { }

        private void showCircle(GeoPoint p1)
        {
            ArrayList p = new ArrayList();
            if (arcPoint1Input.Fixed) p.Add(circlePoint1);
            if (arcPoint2Input.Fixed) p.Add(circlePoint2);
            if (p.Count == 0)
            {
                p1.x = p1.x + base.WorldViewSize / 20;
                circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, p1, base.WorldViewSize / 20);
            }
            if (p.Count >= 1)
            {
                gPoint1.Location = (GeoPoint)p[0];
                try { circle.SetCirclePlane2Points(base.ActiveDrawingPlane, (GeoPoint)p[0], p1); }
                catch (EllipseException) { }
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


        private GeoPoint CircleCenter()
        { return circle.Center; }

        private double CircleRadius()
        { return circle.MajorRadius; }

        public override void OnSetAction()
        {
            circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, new GeoPoint(0.0, 0.0, 0.0), base.WorldViewSize / 20);

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);

            base.ActiveObject = circle;
            base.TitleId = "Constr.Circle.2Points";

            arcPoint1Input = new GeoPointInput("Constr.Circle.Point1");
            arcPoint1Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint1);
            arcPoint2Input = new GeoPointInput("Constr.Circle.Point2");
            arcPoint2Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint2);
            GeoPointInput arcCenterInput = new GeoPointInput("Constr.Circle.Center");
            arcCenterInput.GetGeoPointEvent += new ConstructAction.GeoPointInput.GetGeoPointDelegate(CircleCenter);
            arcCenterInput.Optional = true;
            arcCenterInput.ReadOnly = true;
            LengthInput arcRadius = new LengthInput("Constr.Circle.Radius");
            arcRadius.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(CircleRadius);
            arcRadius.Optional = true;
            arcRadius.ReadOnly = true;
            base.SetInput(arcPoint1Input, arcPoint2Input, arcCenterInput, arcRadius);
            base.ShowAttributes = true;
            base.OnSetAction();
        }
        public override string GetID()
        { return "Constr.Circle.2Points"; }

        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            if (CurrentInput == arcPoint2Input && arcPoint1Input.Fixed)
            {
                GeoObjectList l = base.GetObjectsUnderCursor(e.Location);
                l.DecomposeAll();
                GeoPoint inpoint = base.WorldPoint(e.Location);
                Plane pln = vw.Projection.DrawingPlane;
                for (int i = 0; i < l.Count; i++)
                {
                    if (l[i] is ICurve)
                    {
                        ICurve curve = l[i] as ICurve;
                        if (curve.IsInPlane(pln))
                        {
                            ICurve2D c2d = curve.GetProjectedCurve(pln);
                            GeoPoint2D c12d = pln.Project(circlePoint1);
                            GeoPoint2D[] pp = c2d.PerpendicularFoot(c12d);
                            for (int j = 0; j < pp.Length; j++)
                            {
                                GeoPoint p = pln.ToGlobal(pp[j]);
                                double d = base.WorldPoint(e.Location) | p;
                                if (d < mindist)
                                {
                                    mindist = d;
                                    found = p;
                                }
                            }
                        }
                    }
                }
            }
            return mindist != double.MaxValue;
        }
        public override void OnDone()
        { base.OnDone(); }

    }
}
