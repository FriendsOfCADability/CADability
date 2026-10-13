
using CADability.GeoObject;
using System;



namespace CADability.Actions
{
    /// <summary>
    /// Creates multiple copies of the selected objects arranged on a circle (a polar array). The user enters the
    /// rotation point (default: the center of the objects), the number of copies, whether the copies are distributed
    /// over a full circle or over a given angle, and whether the copies are rotated together with their position or
    /// keep their orientation. Menu id: "CopyCircularObjects".
    /// </summary>

    public class CopyCircularObjects : ConstructAction
    {
        private Block block;
        private GeoObjectList originals;
        static private int copCount;
        static private double copAngle;
        static private bool partOfCircle;
        static private bool objRot;
        private double distX;
        private GeoPoint centerPoint;
        private GeoPoint rotationPoint;
        //		private DoubleInput copiesAngle;
        private AngleInput copiesAngle;



        public CopyCircularObjects(GeoObjectList list)
        {
            originals = new GeoObjectList(list);// originale merken
        }

        private bool showCirc()
        {
            block = Block.Construct();
            for (int i = 1; i < copCount; ++i)
            {
                GeoObjectList li = new GeoObjectList();
                // für jede Kopie ein Clone machen
                li = originals.CloneObjects();
                ModOp m = ModOp.Rotate(rotationPoint, base.ActiveDrawingPlane.Normal, i * copAngle);
                if (!objRot) // Objekte drehen mit: einfach modifizieren
                    li.Modify(m);
                else
                {   // Objekte drehen nicht mit: Translation des gedrehten centerpoints
                    ModOp mt = ModOp.Translate(new GeoVector(centerPoint, m * centerPoint));
                    li.Modify(mt);
                }
                block.Add(li);
            }
            if (block.Count > 0)
            {
                base.ActiveObject = block;
                return true;
            }
            else
            {
                base.ActiveObject = null;
                return false;
            }
        }


        private void SetRotPoint(GeoPoint p)
        {
            rotationPoint = p;
            showCirc();

        }
        private GeoPoint GetRotPoint()
        {
            return rotationPoint;
        }

        private void SetCopiesCount(int val)
        {
            copCount = val;
            if (!partOfCircle) copAngle = 2.0 * Math.PI / val;
            showCirc();
        }


        private int CalcCopiesCount(GeoPoint MousePosition)
        {
            //			if (MousePosition.x > centerPoint.x)
            //			{
            //				return Math.Max(0,(int) Math.Abs((MousePosition.x - centerPoint.x)/distX));
            //			}
            //			else return (copCount);
            return (copCount);
        }

        private void SetFullCircle(bool val)
        {
            partOfCircle = !val;
            copiesAngle.ReadOnly = !partOfCircle;
            copiesAngle.Optional = !partOfCircle;
            if (!partOfCircle)
            {
                copAngle = 2.0 * Math.PI / copCount;
                showCirc();
            }

        }

        private bool GetFullCircle()
        {
            return !partOfCircle;
        }

        private void SetObjectsRotation(bool val)
        {
            objRot = val;
            showCirc();
        }

        private bool GetObjectsRotation()
        {
            return objRot;
        }


        private bool SetCopAngle(Angle val)
        {
            if (val != 0)
            {
                //				copAngle = val/180*Math.PI;
                copAngle = val;
                return showCirc();
            }
            return false;
        }

        private Angle GetCopAngle()
        {   // wegen der Anzeige
            //			return copAngle/Math.PI*180;
            return copAngle;
        }

        private double CalculateCopAngle(GeoPoint MousePosition)
        {
            //			if (MousePosition.x > centerPoint.x)
            //			{
            //				return (int) Math.Abs((MousePosition.x - centerPoint.x)/distX)*Math.PI/12;
            //			}
            //			else return (Math.PI/12*180);
            return (copAngle);
        }

        /// <inheritdoc/>
		public override void OnSetAction()
        {
            base.TitleId = "CopyCircularObjects";

            BoundingRect result = originals.GetExtent(Frame.ActiveView.ProjectedModel.Projection, true, false);
            centerPoint = base.ActiveDrawingPlane.ToGlobal(result.GetCenter());
            //			rotationPoint =  base.ActiveDrawingPlane.ToGlobal(result.GetLowerLeft());
            rotationPoint = centerPoint;
            distX = result.Width;
            if (copCount == 0)
            {
                copCount = 4;
                copAngle = Math.PI / 2.0;
            }

            GeoPointInput rotPoint = new GeoPointInput("CopyCircularObjects.RotationPoint");
            rotPoint.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetRotPoint);
            rotPoint.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetRotPoint);
            rotPoint.Optional = true;
            rotPoint.DefinesHotSpot = true;
            rotPoint.HotSpotSource = "Hotspots.png:0";

            IntInput copiesCount = new IntInput("CopyCircularObjects.CopiesCount", copCount);
            copiesCount.SetMinMax(0, int.MaxValue, true);
            copiesCount.SetIntEvent += new CADability.Actions.ConstructAction.IntInput.SetIntDelegate(SetCopiesCount);
            //			copiesCount.CalculateIntEvent +=new CADability.Actions.ConstructAction.IntInput.CalculateIntDelegate(CalcCopiesCount);

            BooleanInput fullCircle = new BooleanInput("CopyCircularObjects.FullCircle", "CopyCircularObjects.FullCircle.Values");
            fullCircle.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetFullCircle);
            fullCircle.GetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.GetBooleanDelegate(GetFullCircle);

            copiesAngle = new AngleInput("CopyCircularObjects.CopiesAngle", copAngle);
            copiesAngle.ReadOnly = !partOfCircle;
            copiesAngle.Optional = !partOfCircle;
            copiesAngle.SetAngleEvent += new CADability.Actions.ConstructAction.AngleInput.SetAngleDelegate(SetCopAngle);
            copiesAngle.GetAngleEvent += new CADability.Actions.ConstructAction.AngleInput.GetAngleDelegate(GetCopAngle);
            copiesAngle.CalculateAngleEvent += new CADability.Actions.ConstructAction.AngleInput.CalculateAngleDelegate(CalculateCopAngle);

            BooleanInput rotObject = new BooleanInput("CopyCircularObjects.ObjectsRotation", "CopyCircularObjects.ObjectsRotation.Values");
            rotObject.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetObjectsRotation);
            rotObject.GetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.GetBooleanDelegate(GetObjectsRotation);

            base.SetInput(rotPoint, copiesCount, fullCircle, copiesAngle, rotObject);

            base.OnSetAction();
            showCirc();
        }

        /// <inheritdoc/>
        public override string GetID()
        {
            return "CopyCircularObjects";
        }

        /// <inheritdoc/>
		public override void OnDone()
        {
            using (base.Frame.Project.Undo.UndoFrame)
            {
                if (base.ActiveObject != null)
                {
                    base.Frame.Project.GetActiveModel().Add(block.Clear());
                    // folgendes ist zu langsam:
                    // hier rückwärts zählen, da sonst Blödsinn rauskommt
                    //for (int i= block.Count-1; i>=0; --i)
                    //{
                    //    base.Frame.Project.GetActiveModel().Add(block.Item(i));
                    //}
                }
            }
            base.ActiveObject = null; // damit es nicht gleich eingefügt wird

            base.OnDone();
        }

    }
}

