using CADability.Curve2D;
using CADability.GeoObject;
using MouseEventArgs = CADability.Substitutes.MouseEventArgs;

namespace CADability.Actions

{
    //Gerhard: Defaultwerte auch bei Readonly setzen
    // Eckhard: Dann Zeile bei OnDone rausnehmen
    /// <summary>
    /// Construction of a full circle in the active drawing plane from its center and a point on the circle.
    /// The user enters the center and then a point through which the circle passes; while the point is
    /// entered, perpendicular feet on curves under the cursor are offered as tangential snap points. The
    /// resulting radius is shown as a read-only input. Started with the action id "Constr.Circle.CenterPoint"
    /// (menu command "MenuId.Constr.Circle.CenterPoint").
    /// </summary>
    internal class ConstrCircleCenterPoint : ConstructAction
    {
        private Ellipse circle;
        private GeoPoint pCirc;
        private GeoPointInput circPointInput;
        private GeoPointInput circCenter;
        private CADability.GeoObject.Point gPoint1;

        public ConstrCircleCenterPoint()
        { }

        private void Center(GeoPoint p)
        {
            double rad;
            if (circPointInput.Fixed)
                rad = Geometry.Dist(pCirc, p);
            else rad = circle.Radius;
            circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, p, rad);
            gPoint1.Location = p;
        }

        private void CirclePoint(GeoPoint p)
        {
            pCirc = p;
            circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, circle.Center, Geometry.Dist(circle.Center, p));
        }

        public double CircleRadius()
        { return circle.MajorRadius; }

        public override void OnSetAction()
        {
            circle = Ellipse.Construct();
            circle.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, ConstrDefaults.DefaultArcCenter, ConstrDefaults.DefaultArcRadius);

            gPoint1 = ActionFeedBack.FeedbackPoint(base.Frame);
            base.FeedBack.Add(gPoint1);

            base.ActiveObject = circle;
            base.TitleId = "Constr.Circle.CenterPoint";

            circCenter = new GeoPointInput("Constr.Circle.Center");
            circCenter.DefaultGeoPoint = ConstrDefaults.DefaultArcCenter;
            circCenter.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(Center);
            circPointInput = new GeoPointInput("Constr.Circle.CenterPoint.Point");
            circPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(CirclePoint);

            LengthInput len = new LengthInput("Constr.Circle.Radius");
            len.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(CircleRadius);
            len.DefaultLength = ConstrDefaults.DefaultArcRadius;
            len.Optional = true;
            len.ReadOnly = true;

            base.SetInput(circCenter, circPointInput, len);
            base.ShowAttributes = true;

            base.OnSetAction();
        }
        protected override bool FindTangentialPoint(MouseEventArgs e, IView vw, out GeoPoint found)
        {
            double mindist = double.MaxValue;
            found = GeoPoint.Origin;
            if (CurrentInput == circPointInput && circCenter.Fixed)
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
                            GeoPoint2D c12d = pln.Project(circle.Center);
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
        public override string GetID()
        { return "Constr.Circle.CenterPoint"; }

        public override void OnDone()
        {
            //			ConstrDefaults.DefaultArcRadius.Length = circle.MajorRadius;
            base.OnDone();
        }
    }
}
