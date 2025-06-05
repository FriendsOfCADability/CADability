using CADability.Attribute;
using CADability.GeoObject;
using CADability.Substitutes;
using CADability.UserInterface;
using System;
using System.Collections;
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
    public class RotateObjects : ConstructAction
    {
        private Block block;
        private GeoObjectList originals;
        //		private Angle offset; // wegdamit
        //		private GeoVector vecOffset; // wegdamit
        //		private AngleInput ang; // wegdamit

        private Angle rotationAngle;
        private GeoPoint refPoint;
        private GeoPoint startPoint;
        private GeoPoint endPoint;
        private GeoPointInput endPointInput;
        private GeoPointInput startPointInput;
        private AngleInput rotAngleInput;
        private Ellipse feedBackEllipse;
        private bool copyObject;
        private CurveInput rotateLineInput;
        private GeoVector axisVector;
        private PlaneInput srcPlane, trgPlane; // rotate objects from src to trg
        private Plane src, trg;
        private MultipleChoiceInput offset;
        private int offsetVal; // 0: 0°, 1: 90°, 2: 180°, 3: 270°(-90°)

        public RotateObjects(GeoObjectList list)
        {
            block = Block.Construct();
            originals = new GeoObjectList(list);
            rotationAngle = new Angle(0.0);
            for (int i = 0; i < originals.Count; ++i)
            {
                block.Add(originals[i].Clone());
            }
        }

        private void SetRefPoint(GeoPoint p)
        {
            refPoint = p;
            block.RefPoint = p;
            base.BasePoint = p; // für die Winkelberechnung
            if (rotAngleInput.Fixed)
            {
                for (int i = 0; i < block.Count; ++i)
                {
                    block.Child(i).CopyGeometry(originals[i]);
                }
                ModOp m = ModOp.Rotate(base.BasePoint, axisVector, new SweepAngle(rotationAngle) + GetOffset());
                block.Modify(m);
            }

        }

        private GeoPoint GetRefPoint()
        {
            return base.BasePoint;
        }

        private bool OnSetRotationAngle(Angle angle)
        {
            ModOp m = ModOp.Rotate(base.BasePoint, axisVector, new SweepAngle(angle - rotationAngle) + GetOffset());
            block.Modify(m);
            base.ActiveObject = block;
            rotationAngle = angle;
            return true;
        }

        private Angle OnGetRotationAngle()
        {
            return rotationAngle;
        }

        private double OnCalculateRotationAngle(GeoPoint MousePosition)
        {
            startPoint = MousePosition;
            return rotationAngle;
        }

        private void OnMouseClickRotationAngle(bool up, GeoPoint MousePosition, IView View)
        {
            if (!up) // also beim Drücken, nicht beim Loslassen
            {
                startPoint = MousePosition; // den Runterdrückpunkt merken
                startPointInput.Fixed = true; // und sagen dass er existiert
                base.SetFocus(endPointInput, true); // Focus auf den Endpunkt setzen
            }
        }

        private void OnSetStartPoint(GeoPoint p)
        {
            startPoint = p;
        }

        private GeoPoint OnGetStartPoint()
        {
            return startPoint;
        }

        private void OnSetEndPoint(GeoPoint p)
        {
            endPoint = p;
            if (startPointInput.Fixed)
            {
                rotAngleInput.Fixed = true; // damit die Aktion nach dem Endpunkt aufhört
                SweepAngle swAngle = new SweepAngle((GeoVector2D)(base.ActiveDrawingPlane.Project(startPoint) - base.ActiveDrawingPlane.Project(refPoint)), base.ActiveDrawingPlane.Project(endPoint) - base.ActiveDrawingPlane.Project(refPoint));
                ModOp m = ModOp.Rotate(base.BasePoint, axisVector, new SweepAngle(swAngle - rotationAngle) + GetOffset());
                rotationAngle = swAngle.Radian;
                block.Modify(m);
                feedBackEllipse.SetArcPlaneCenterStartEndPoint(ActiveDrawingPlane, ActiveDrawingPlane.Project(BasePoint), ActiveDrawingPlane.Project(startPoint), ActiveDrawingPlane.Project(endPoint), ActiveDrawingPlane, swAngle.Radian > 0.0);
            }
        }

        private GeoPoint OnGetEndPoint()
        {
            return endPoint;
        }

        private bool RotateLine(CurveInput sender, ICurve[] Curves, bool up)
        {   // ... nur die sinnvolen Kurven verwenden
            ArrayList usableCurves = new ArrayList();
            ModOp m;
            for (int i = 0; i < Curves.Length; ++i)
            {
                Line l = Curves[i] as Line;
                if (l != null)
                {
                    usableCurves.Add(Curves[i]);
                }
            }
            // ...hier wird der ursprüngliche Parameter überschrieben. Hat ja keine Auswirkung nach außen.
            Curves = (ICurve[])usableCurves.ToArray(typeof(ICurve));
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else
                { // erst jetzt die Werte merken!
                    base.BasePoint = Curves[0].StartPoint;
                    axisVector = Curves[0].StartDirection;
                    sender.SetCurves(Curves, Curves[0]);
                }
            // erstmal den Urprungszustand herstellen, da sonst Drehungg akkumuliert wird
            for (int i = 0; i < block.Count; ++i)
            {
                block.Child(i).CopyGeometry(originals[i]);
            }
            if (Curves.Length > 0)
            {   // einfach die erste Kurve nehmen
                ICurve iCurve = Curves[0];
                m = ModOp.Rotate(iCurve.StartPoint, iCurve.StartDirection, new SweepAngle(rotationAngle) + GetOffset());
                block.Modify(m);
                return true;
            }
            // rückgängig!
            m = ModOp.Rotate(base.BasePoint, axisVector, new SweepAngle(rotationAngle) + GetOffset());
            block.Modify(m);
            return false;
        }

        private void RotateLineChanged(CurveInput sender, ICurve SelectedCurve)
        {
            base.BasePoint = SelectedCurve.StartPoint;
            axisVector = SelectedCurve.StartDirection;
            // erstmal den Urprungszustand herstellen
            for (int i = 0; i < block.Count; ++i)
            {
                block.Child(i).CopyGeometry(originals[i]);
            }
            ModOp m = ModOp.Rotate(base.BasePoint, axisVector, new SweepAngle(rotationAngle) + GetOffset());
            block.Modify(m);
        }

        public override bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.XAxis":
                    axisVector = GeoVector.XAxis;
                    SetRefPoint(BasePoint); // zum Updaten
                    return true;
                case "MenuId.YAxis":
                    axisVector = GeoVector.YAxis;
                    SetRefPoint(BasePoint); // zum Updaten
                    return true;
                case "MenuId.ZAxis":
                    axisVector = GeoVector.ZAxis;
                    SetRefPoint(BasePoint); // zum Updaten
                    return true;
                case "MenuId.Construct.Abort":
                    this.OnEscape();
                    return true;
                case "MenuId.Construct.Finish":
                    this.OnEnter();
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Override if you also override <see cref="OnCommand"/> to manipulate the appearance
        /// of the corresponding menu item or the state of the toolbar button. The default implementation
        /// checks whether the MenuId from the parameter corresponds to the menuId member variable
        /// and checks the item if appropriate
        /// </summary>
        /// <param name="MenuId">menu id the command state is queried for</param>
        /// <param name="CommandState">the command state to manipulate</param>
        /// <returns></returns>
        public override bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            if (MenuId == "MenuId.Axis")
            {
                CommandState.Checked = true;
                return true;
            }
            else return false;
        }

        private void SetCopy(bool val)
        {
            copyObject = val;
        }

        public override void OnSetAction()
        {
            //			base.ActiveObject = block;
            base.TitleId = "RotateObjects";
            copyObject = ConstrDefaults.DefaultCopyObjects;
            axisVector = base.ActiveDrawingPlane.Normal;

            feedBackEllipse = Ellipse.Construct();
            feedBackEllipse.SetEllipseCenterAxis(GeoPoint.Origin, GeoVector.XAxis, GeoVector.YAxis); // damit nicht alles 0
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackEllipse.ColorDef = new ColorDef("", backColor);
            base.FeedBack.Add(feedBackEllipse);
            base.SetCursor(SnapPointFinder.DidSnapModes.DidNotSnap, "RotateSmall");

            //--> diese Inputs werden gebraucht
            GeoPointInput refPointInput = new GeoPointInput("Objects.RefPoint");
            refPointInput.Optional = true;
            refPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetRefPoint);
            refPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetRefPoint);
            refPointInput.DefinesHotSpot = true;
            refPointInput.HotSpotSource = "Hotspots.png:0";

            rotAngleInput = new AngleInput("RotateObjects.Angle");
            rotAngleInput.SetAngleEvent += new CADability.Actions.ConstructAction.AngleInput.SetAngleDelegate(OnSetRotationAngle);
            rotAngleInput.GetAngleEvent += new CADability.Actions.ConstructAction.AngleInput.GetAngleDelegate(OnGetRotationAngle);
            rotAngleInput.CalculateAngleEvent += new CADability.Actions.ConstructAction.AngleInput.CalculateAngleDelegate(OnCalculateRotationAngle);
            rotAngleInput.MouseClickEvent += new MouseClickDelegate(OnMouseClickRotationAngle);

            startPointInput = new GeoPointInput("Objects.StartPoint");
            startPointInput.Optional = true;
            startPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetStartPoint);
            startPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetStartPoint);

            endPointInput = new GeoPointInput("Objects.EndPoint");
            endPointInput.Optional = true;
            endPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetEndPoint);
            endPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetEndPoint);

            rotateLineInput = new CurveInput("Constr.Rotate.AxisLine");
            rotateLineInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            rotateLineInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(RotateLine);
            rotateLineInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(RotateLineChanged);
            rotateLineInput.Optional = true;

            srcPlane = new PlaneInput("Constr.Rotate.SourcePlane");
            srcPlane.SetPlaneEvent += SrcPlane_OnSetPlane;
            srcPlane.GetPlaneEvent += SrcPlane_OnGetPlane;
            srcPlane.Optional = true;

            trgPlane = new PlaneInput("Constr.Rotate.TargetPlane");
            trgPlane.SetPlaneEvent += TrgPlane_OnSetPlane;
            trgPlane.GetPlaneEvent += TrgPlane_OnGetPlane;
            trgPlane.Optional = true;

            offset = new MultipleChoiceInput("Constr.Rotate.Offset", "Constr.Rotate.Offset.Values");
            offset.Optional = true;
            offsetVal = 0;
            offset.GetChoiceEvent += OnGetOffset;
            offset.SetChoiceEvent += OnSetOffset;

            BooleanInput copy = new BooleanInput("Modify.CopyObjects", "YesNo.Values");
            copy.DefaultBoolean = ConstrDefaults.DefaultCopyObjects;
            copy.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetCopy);

            base.SetInput(refPointInput, rotAngleInput, startPointInput, endPointInput, rotateLineInput, srcPlane, trgPlane, offset, copy);

            BoundingCube result = BoundingCube.EmptyBoundingCube;
            foreach (IGeoObject go in originals)
            {
                result.MinMax(go.GetBoundingCube());
            }
            GeoPoint blockCenter = result.GetCenter();
            block.RefPoint = blockCenter;
            refPoint = blockCenter;
            base.BasePoint = blockCenter; // für die Winkelberechnung
            base.OnSetAction();
            rotateLineInput.SetContextMenu("MenuId.Axis", this); // kann man erst hier machen, zuvor gibts die Property noch nicht
        }

        private int OnGetOffset()
        {
            return offsetVal;
        }

        private void OnSetOffset(int val)
        {
            offsetVal = val;
        }

        private Plane SrcPlane_OnGetPlane()
        {
            return src;
        }

        private bool SrcPlane_OnSetPlane(Plane val)
        {
            src = val;
            if (trgPlane.Fixed) RotateWithPlanes();
            srcPlane.Fixed = true;
            return true;
        }

        private Plane TrgPlane_OnGetPlane()
        {
            return trg;
        }

        private bool TrgPlane_OnSetPlane(Plane val)
        {
            trg = val;
            if (srcPlane.Fixed) RotateWithPlanes();
            trgPlane.Fixed = true;
            return true;
        }

        private void RotateWithPlanes()
        {
            // get rotation axis from the two planes
            if (src.Intersect(trg, out GeoPoint loc, out GeoVector dir))
            {
                // set to original position
                for (int i = 0; i < block.Count; ++i)
                {
                    block.Child(i).CopyGeometry(originals[i]);
                }
                Plane perp = new Plane(loc, dir); // plane perpendicular to rotation axis
                GeoVector2D from = perp.Project(dir ^ src.Normal);
                GeoVector2D to = perp.Project(dir ^ trg.Normal);
                SweepAngle sw = new SweepAngle(from, to);
                rotationAngle = sw;
                rotAngleInput.ForceValue(rotationAngle);
                ModOp m0 = ModOp.Rotate(loc, dir, sw);
                ModOp m1 = ModOp.Rotate(loc, -dir, sw);
                GeoVector n0 = m0 * src.Normal;
                GeoVector n1 = m1 * src.Normal;
                ModOp m; // not sure, which rotation is better
                if (Math.Abs(n0 * trg.Normal) > Math.Abs(n1 * trg.Normal))
                {
                    m = ModOp.Rotate(loc, dir, sw + GetOffset());
                    axisVector = dir;
                }
                else
                {
                    m = ModOp.Rotate(loc, -dir, sw + GetOffset());
                    axisVector = -dir;
                }
                block.Modify(m);
                base.BasePoint = loc;
            }
        }

        private SweepAngle GetOffset()
        {
            switch (offsetVal)
            {
                default:
                case 0: return new SweepAngle(0);
                case 1: return SweepAngle.ToLeft;
                case 2: return SweepAngle.Opposite;
                case 3: return SweepAngle.ToRight;
            }
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "RotateObjects";
        }

        public override void OnDone()
        {
            // ist die Shift Taste gehalten, so werden Kopien gemacht, d.h. der die Elemente
            // des blocks werden eingefügt. Ansonsten werden die original-Objekte verändert
            // TODO: Feedback über Cursor bei Shift-Taste fehlt noch
            // TODO: die neuen oder veränderten Objekte sollten markiert sein.
            using (Frame.Project.Undo.UndoFrame)
            {
                //				ModOp m = ModOp.Rotate(base.BasePoint,base.ActiveDrawingPlane.Normal,new SweepAngle(rotationAngle));                
                ModOp m = ModOp.Rotate(base.BasePoint, axisVector, new SweepAngle(rotationAngle) + GetOffset());
                if (((Frame.UIService.ModifierKeys & Keys.Shift) != 0) || copyObject)
                {
                    GeoObjectList cloned = new GeoObjectList();
                    foreach (IGeoObject go in originals)
                    {
                        IGeoObject cl = go.Clone();
                        cl.Modify(m);
                        cloned.Add(cl);
                    }
                    base.Frame.Project.GetActiveModel().Add(cloned);
                }
                else
                {
                    originals.Modify(m);
                }
            }
            base.ActiveObject = null; // damit es nicht gleich eingefügt wird
            base.OnDone();
        }
    }
}
