using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using System;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ConstructAequidist : ConstructAction
    {
        private CurveInput baseCurve;
        private LengthInput distance;
        private AngleInput minAngle;
        private static ConstructAction.DefaultAngle defaultMinAngle = new DefaultAngle(DefaultAngle.StartValue.ToLeft); // 180°
        private static ConstructAction.DefaultLength defaultDistance = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth40);
        private ICurve theBaseCurve;
        private double theDistance;
        private Angle theMinAngle;
        public ConstructAequidist(ICurve toThisCurve)
        {
            theBaseCurve = toThisCurve; // null oder gegeben
        }
        public override string GetID()
        {
            return "ConstructAequidist";
        }
        public override void OnSetAction()
        {
            base.TitleId = "ConstructAequidist";
            if (theBaseCurve == null)
            {
                baseCurve = new CurveInput("ConstructAequidist.BaseCurve");
                baseCurve.MouseOverCurvesEvent += new CADability.Actions.ConstructAction.CurveInput.MouseOverCurvesDelegate(OnMouseOverCurves);
                baseCurve.CurveSelectionChangedEvent += new CADability.Actions.ConstructAction.CurveInput.CurveSelectionChangedDelegate(OnCurveSelectionChanged);
                baseCurve.HitCursor = CursorTable.GetCursor("RoundIn.cur");
                baseCurve.ModifiableOnly = false;
                baseCurve.PreferPath = true;
            }

            distance = new LengthInput("ConstructAequidist.Distance");
            distance.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(OnGetDistance);
            distance.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(OnSetDistance);
            distance.DefaultLength = defaultDistance;
            distance.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(OnCalculateDist);
            distance.ForwardMouseInputTo = baseCurve; // kann ja auch null sein
            theDistance = defaultDistance;

            minAngle = new AngleInput("ConstructAequidist.SharpCornerMinAngle");
            minAngle.GetAngleEvent += new AngleInput.GetAngleDelegate(OnGetMinAngle);
            minAngle.SetAngleEvent += new AngleInput.SetAngleDelegate(OnSetMinAngle);
            minAngle.DefaultAngle = defaultMinAngle;
            minAngle.Optional = true;
            minAngle.ForwardMouseInputTo = baseCurve;

            if (baseCurve != null)
            {
                SetInput(baseCurve, distance, minAngle);
            }
            else
            {
                SetInput(distance, minAngle);
            }
            base.OnSetAction();
        }

        bool OnSetMinAngle(Angle angle)
        {
            if (angle >= 0.0 && angle <= Math.PI)
            {
                theMinAngle = angle;
                Recalc();
                return true;
            }
            return false;
        }

        Angle OnGetMinAngle()
        {
            return theMinAngle;
        }
        public override void OnDone()
        {
            if (base.ActiveObject is Block)
            {
                Block blk = base.ActiveObject as Block;
                using (Frame.Project.Undo.UndoFrame)
                {
                    for (int i = 0; i < blk.Count; ++i)
                    {
                        IGeoObject go = blk.Child(i).Clone();
                        go.CopyAttributes(theBaseCurve);
                        if (go is Path && (go as Path).CurveCount == 1)
                        {
                            go = (go as Path).Curves[0];
                        }
                        Frame.Project.GetActiveModel().Add(go);
                    }
                }
            }
            base.ActiveObject = null;
            base.OnDone();
        }
        private bool Recalc()
        {
            if (theBaseCurve == null) return false;
            Plane pln;
            if (theBaseCurve.GetPlanarState() == PlanarState.Planar)
                pln = theBaseCurve.GetPlane();
            else
                pln = base.ActiveDrawingPlane;
            ICurve2D c2d = theBaseCurve.GetProjectedCurve(pln);
            ICurve2D app = c2d.Approximate(false, Frame.GetDoubleSetting("Approximate.Precision", 0.01));
            Border bdr = new Border(app);
            Border[] res = bdr.GetParallel(theDistance, true, 0.0, theMinAngle);
            Block blk = Block.Construct();
            for (int i = 0; i < res.Length; ++i)
            {
                IGeoObject go = res[i].AsPath().MakeGeoObject(pln);
                go.CopyAttributes(theBaseCurve);
                (go as IColorDef).ColorDef = (theBaseCurve as IColorDef).ColorDef;
                blk.Add(go);
            }
            base.ActiveObject = blk;
            return true;
        }
        private bool OnMouseOverCurves(CurveInput sender, ICurve[] TheCurves, bool up)
        {
            for (int i = 0; i < TheCurves.Length; ++i)
            {
                if (TheCurves[i] is GeoObject.Path)
                {
                    if (TheCurves[i].GetPlanarState() == PlanarState.Planar)
                    {
                        sender.SetSelectedCurve(TheCurves[i]);
                        theBaseCurve = TheCurves[i];
                        if (Recalc())
                        {
                            if (up) distance.ForwardMouseInputTo = null; // jetzt Mouse Input für Abstand zulassen
                            return true;
                        }
                    }
                }
                else
                {
                    if (TheCurves[i].GetPlanarState() == PlanarState.Planar || TheCurves[i].GetPlanarState() == PlanarState.UnderDetermined)
                    {
                        sender.SetSelectedCurve(TheCurves[i]);
                        theBaseCurve = Path.CreateFromModel(TheCurves[i], base.Frame.ActiveView.Model, true);
                        theBaseCurve.CopyAttributes(TheCurves[i]);
                        if (Recalc())
                        {
                            if (up) distance.ForwardMouseInputTo = null; // jetzt Mouse Input für Abstand zulassen
                            return true;
                        }
                    }
                }
            }
            theBaseCurve = null;
            base.ActiveObject = null;
            return false;
        }
        private double OnGetDistance()
        {
            return theDistance;
        }
        private bool OnSetDistance(double d)
        {
            theDistance = d;
            return Recalc();
        }
        private void OnCurveSelectionChanged(CurveInput sender, ICurve SelectedCurve)
        {
            theBaseCurve = SelectedCurve;
        }
        private double OnCalculateDist(GeoPoint MousePosition)
        {
            if (theBaseCurve != null)
            {
                Plane pln;
                if (theBaseCurve.GetPlanarState() == PlanarState.Planar)
                    pln = theBaseCurve.GetPlane();
                else
                    pln = base.ActiveDrawingPlane;
                ICurve2D c2d = theBaseCurve.GetProjectedCurve(pln);
                GeoPoint2D p2d = pln.Project(MousePosition);
                GeoPoint2D[] fp = c2d.PerpendicularFoot(p2d);
                int ind = -1;
                double mind = double.MaxValue;
                GeoVector2D dir = GeoVector2D.XAxis; // muss halt initialisiert sein
                for (int i = 0; i < fp.Length; ++i)
                {
                    double d = Geometry.Dist(p2d, fp[i]);
                    if (d < mind)
                    {
                        mind = d;
                        ind = i;
                        double pos = c2d.PositionOf(fp[i]);
                        dir = c2d.DirectionAt(pos);
                    }
                }
                if (ind >= 0)
                {
                    if (Geometry.OnLeftSide(p2d, fp[ind], dir))
                        return -mind;
                    else
                        return mind;
                }
            }
            return 0.0;
        }
    }
}
