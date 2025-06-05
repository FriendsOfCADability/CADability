using CADability.Attribute;
using CADability.GeoObject;
using CADability.Substitutes;
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
    public class MoveObjects : ConstructAction
    {
        private Block block;
        private GeoObjectList originals;
        private GeoVector offset;
        private GeoVectorInput vec;
        private GeoPointInput startPointInput;
        private GeoPointInput endPointInput;
        private GeoPoint startPoint;
        private GeoPoint endPoint;
        private Line feedBackLine;
        private bool copyObject;

        public MoveObjects(GeoObjectList list)
        {
            block = Block.Construct();
            foreach (IGeoObject go in list)
            {
                block.Add(go.Clone());
            }
            originals = new GeoObjectList(list);
            offset = new GeoVector(0.0, 0.0, 0.0);
        }

        private bool Vec_OnSetGeoVector(GeoVector vector)
        {
            ModOp m = ModOp.Translate(vector - offset);
            block.Modify(m);
            base.ActiveObject = block;
            offset = vector;
            return true;
        }

        private void VecOnMouseClick(bool up, GeoPoint MousePosition, IView View)
        {
            if (!up) // also beim Drücken, nicht beim Loslassen
            {
                block.RefPoint = MousePosition; // den Runterdrückpunkt merken
                startPoint = MousePosition; // den Runterdrückpunkt merken
                base.BasePoint = startPoint;
                vec.SetVectorFromPoint(MousePosition);
                startPointInput.Fixed = true; // und sagen dass er existiert
                base.FeedBack.Add(feedBackLine);
                base.SetFocus(endPointInput, true); // Focus auf den Endpunkt setzen
            }

        }

        GeoVector vecCalculateGeoVector(GeoPoint MousePosition)
        {
            startPoint = MousePosition;
            return new GeoVector(0.0, 0.0, 0.0);
        }

        GeoVector vecGetGeoVector()
        {
            return offset;
        }



        private void SetStartPoint(GeoPoint p)
        {
            block.RefPoint = p;
            startPoint = p;
            vec.SetVectorFromPoint(p);
            endPointInput.Optional = false;
            base.FeedBack.Add(feedBackLine);
        }

        private GeoPoint GetStartPoint()
        {
            return startPoint;
        }

        private void SetEndPoint(GeoPoint p)
        {
            endPoint = p;
            if (startPointInput.Fixed)
            {
                vec.Fixed = true; // damit die Aktion nach dem Endpunkt aufhört
                GeoVector vect = new GeoVector(startPoint, p);
                ModOp m = ModOp.Translate(vect - offset);
                block.Modify(m);
                base.ActiveObject = block;
                feedBackLine.SetTwoPoints(startPoint, p);
                offset = vect;
            }
        }

        private GeoPoint GetEndPoint()
        {
            return endPoint;
        }

        public void SetCopy(bool val)
        {
            copyObject = val;
        }

        public override void OnSetAction()
        {
            //			base.ActiveObject = block;
            base.TitleId = "MoveObjects";
            copyObject = ConstrDefaults.DefaultCopyObjects;
            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            base.SetCursor(SnapPointFinder.DidSnapModes.DidNotSnap, "Move");


            vec = new GeoVectorInput("MoveObjects.Vector");
            vec.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(Vec_OnSetGeoVector);
            vec.MouseClickEvent += new MouseClickDelegate(VecOnMouseClick);
            vec.CalculateGeoVectorEvent += new GeoVectorInput.CalculateGeoVectorDelegate(vecCalculateGeoVector);
            vec.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(vecGetGeoVector);

            startPointInput = new GeoPointInput("Objects.StartPoint");
            startPointInput.Optional = true;
            startPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetStartPoint);
            startPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetStartPoint);

            endPointInput = new GeoPointInput("Objects.EndPoint");
            endPointInput.Optional = true;
            endPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetEndPoint);
            endPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetEndPoint);

            BooleanInput copy = new BooleanInput("Modify.CopyObjects", "YesNo.Values");
            copy.DefaultBoolean = ConstrDefaults.DefaultCopyObjects;
            copy.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetCopy);

            base.SetInput(vec, startPointInput, endPointInput, copy);

            BoundingCube result = BoundingCube.EmptyBoundingCube;
            foreach (IGeoObject go in originals)
            {
                result.MinMax(go.GetBoundingCube());
            }

            GeoPoint blockCenter = result.GetCenter();
            block.RefPoint = blockCenter;
            vec.SetVectorFromPoint(blockCenter);
            base.OnSetAction();
        }


        public override string GetID()
        {
            return "MoveObjects";
        }

        public override void OnDone()
        {
            // ist die Shift Taste gehalten, so werden Kopien gemacht, d.h. der die Elemente
            // des blocks werden eingefügt. Ansonsten werden die original-Objekte verändert
            // TODO: Feedback über Cursor bei Shift-Taste fehlt noch
            // TODO: die neuen oder veränderten Objekte sollten markiert sein.
            using (Frame.Project.Undo.UndoFrame)
            {
                ModOp m = ModOp.Translate(offset);
                if (((Frame.UIService.ModifierKeys & Keys.Shift) != 0) || copyObject)
                {
                    GeoObjectList cloned = new GeoObjectList();
                    foreach (IGeoObject go in originals)
                    {
                        IGeoObject cl = go.Clone();
                        cl.Modify(m);
                        //					go.Owner.Add(cl);
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
