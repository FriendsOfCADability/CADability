using CADability.GeoObject;
using System;



namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>

    public class CopyMatrixObjects : ConstructAction
    {
        private Block block; // das aktive Objekt
        private Block backgroundBlock; // im Hintergrund (Task) in Arbeit
        private GeoObjectList originals;
        static private int horRight, horLeft, verUp, verDown;
        static private GeoVector dirV;
        private double distX;
        private double distY;
        private GeoPoint centerPoint;
        private LengthInput horDist;
        private LengthInput verDist;



        public CopyMatrixObjects(GeoObjectList list)
        {
            originals = new GeoObjectList(list);// liste merken
        }

        private delegate void MakeBlockDelegate(int horLeft, int horRight, int verDown, int verUp, double distX, double distY);
        private void MakeBlock(int horLeft, int horRight, int verDown, int verUp, double distX, double distY)
        {   // läuft asynchron
            backgroundBlock = Block.Construct();
            for (int i = -horLeft; i <= horRight; ++i)
            {
                if (i != 0) // i=0 ist das Original, soll nicht verdoppelt werden!
                {
                    GeoObjectList li = new GeoObjectList();
                    // für jede Kopie ein Clone machen
                    li = originals.CloneObjects();
                    // AbstandX * Kopienwinkel:
                    GeoVector vect = (i * distX) * dirV;
                    ModOp m = ModOp.Translate(vect);
                    li.Modify(m);
                    backgroundBlock.Add(li);
                }
                for (int j = -verDown; j <= verUp; ++j)
                {
                    if (j != 0) // J=0 ist das Original, soll nicht verdoppelt werden!
                    {
                        GeoObjectList liV = new GeoObjectList();
                        // für jede Kopie ein Clone machen
                        liV = originals.CloneObjects();
                        // der senkrechte Winkel auf Kopienwinkel dirV
                        GeoVector dirV_Vert = base.ActiveDrawingPlane.Normal ^ dirV;
                        GeoVector vectV = (i * distX) * dirV + (j * distY) * dirV_Vert;
                        ModOp m = ModOp.Translate(vectV);
                        liV.Modify(m);
                        backgroundBlock.Add(liV);
                    }
                }
            }
        }
        private delegate void BlockDoneDelegate();
        private void BlockDone()
        {   // läuft im Hauptthread der Anwendung
            horDist.ReadOnly = horDist.Optional = ((horLeft == 0) & (horRight == 0));
            verDist.ReadOnly = verDist.Optional = ((verDown == 0) & (verUp == 0));
            block = backgroundBlock;
            if (block.Count > 0)
            {
                base.ActiveObject = block;
            }
            else
            {
                base.ActiveObject = null;
            }
        }

        private bool showMatrix()
        {
            if (IsBackgroundTaskActive) CancelBackgroundTask();
            StartBackgroundTask(new MakeBlockDelegate(MakeBlock), new BlockDoneDelegate(BlockDone), horLeft, horRight, verDown, verUp, distX, distY);
            return true;
        }

        private void SetHorCountRight(int val)
        {
            horRight = val;
            showMatrix();
        }

        private int CalcHorCountRight(GeoPoint MousePosition)
        {
            if (MousePosition.x > centerPoint.x)
            {
                return Math.Max(0, (int)((MousePosition.x - centerPoint.x + distX / 2) / distX));
            }
            else return (0);
        }

        private void SetHorCountLeft(int val)
        {
            horLeft = val;
            showMatrix();
        }

        private int CalcHorCountLeft(GeoPoint MousePosition)
        {
            if (centerPoint.x > MousePosition.x)
            {
                return Math.Max(0, (int)((centerPoint.x - distX / 2 - MousePosition.x) / distX));
            }
            else return (0);
        }


        private void SetVerCountUp(int val)
        {
            verUp = val;
            showMatrix();
        }

        private int CalcVerCountUp(GeoPoint MousePosition)
        {
            if (MousePosition.y > centerPoint.y)
            {
                return Math.Max(0, (int)((MousePosition.y - centerPoint.y + distY / 2) / distY));
            }
            else return (0);
        }

        private void SetVerCountDown(int val)
        {
            verDown = val;
            showMatrix();
        }

        private int CalcVerCountDown(GeoPoint MousePosition)
        {
            if (centerPoint.y > MousePosition.y)
            {
                return Math.Max(0, (int)((centerPoint.y - distY / 2 - MousePosition.y) / distY));
            }
            else return (0);
        }

        private bool SetHorDist(double Length)
        {
            distX = Length;
            return (showMatrix());
        }

        private bool SetVerDist(double Length)
        {
            distY = Length;
            return (showMatrix());
        }

        private bool SetDir(GeoVector vector)
        {
            if (!Precision.IsNullVector(vector))
            {
                dirV = vector;
                dirV.Norm();
                // Referenzlinien für die Abstände, abhängig vom Winkel!
                horDist.SetDistanceFromLine(centerPoint, centerPoint + (dirV ^ base.ActiveDrawingPlane.Normal));
                verDist.SetDistanceFromLine(centerPoint, centerPoint + dirV);
                return (showMatrix());
            }
            return false;
        }


        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
        /// </summary>
		public override void OnSetAction()
        {
            base.TitleId = "CopyMatrixObjects";

            BoundingRect result = originals.GetExtent(Frame.ActiveView.ProjectedModel.Projection, true, false);
            centerPoint = base.ActiveDrawingPlane.ToGlobal(result.GetCenter());
            distX = result.Width;
            distY = result.Height;
            // da oben static private, werden diese Variablen gemerkt. Beim ersten Mal vorbesetzen:
            if ((horRight == 0) & (horLeft == 0) & (verUp == 0) & (verDown == 0))
            {
                horRight = 3;
                verUp = 2;
            }
            // da oben static private, wird diese Variable gemerkt. Beim ersten Mal vorbesetzen:
            if (dirV.IsNullVector())
            {
                dirV = new GeoVector(1.0, 0.0, 0.0);
            }

            IntInput horCountRight = new IntInput("CopyMatrixObjects.HorCountRight", horRight);
            horCountRight.SetMinMax(0, int.MaxValue, true);
            horCountRight.SetIntEvent += new CADability.Actions.ConstructAction.IntInput.SetIntDelegate(SetHorCountRight);
            //			horCountRight.CalculateIntEvent +=new CADability.Actions.ConstructAction.IntInput.CalculateIntDelegate(CalcHorCountRight);

            IntInput verCountUp = new IntInput("CopyMatrixObjects.VerCountUp", verUp);
            verCountUp.SetMinMax(0, int.MaxValue, true);
            verCountUp.SetIntEvent += new CADability.Actions.ConstructAction.IntInput.SetIntDelegate(SetVerCountUp);
            //			verCountUp.CalculateIntEvent +=new CADability.Actions.ConstructAction.IntInput.CalculateIntDelegate(CalcVerCountUp);

            horDist = new LengthInput("CopyMatrixObjects.HorDist", distX);
            horDist.SetDistanceFromLine(centerPoint, centerPoint + (dirV ^ base.ActiveDrawingPlane.Normal));
            horDist.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(SetHorDist);

            verDist = new LengthInput("CopyMatrixObjects.VerDist", distY);
            verDist.SetDistanceFromLine(centerPoint, centerPoint + dirV);
            verDist.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(SetVerDist);

            GeoVectorInput dir = new GeoVectorInput("CopyMatrixObjects.Direction", dirV);
            dir.Optional = true;
            dir.SetVectorFromPoint(centerPoint);
            dir.SetGeoVectorEvent += new CADability.Actions.ConstructAction.GeoVectorInput.SetGeoVectorDelegate(SetDir);
            dir.IsAngle = true;

            IntInput horCountLeft = new IntInput("CopyMatrixObjects.HorCountLeft", horLeft);
            horCountLeft.SetMinMax(0, int.MaxValue, true);
            horCountLeft.Optional = true;
            horCountLeft.SetIntEvent += new CADability.Actions.ConstructAction.IntInput.SetIntDelegate(SetHorCountLeft);
            //			horCountLeft.CalculateIntEvent +=new CADability.Actions.ConstructAction.IntInput.CalculateIntDelegate(CalcHorCountLeft);

            IntInput verCountDown = new IntInput("CopyMatrixObjects.VerCountDown", verDown);
            verCountDown.SetMinMax(0, int.MaxValue, true);
            verCountDown.Optional = true;
            verCountDown.SetIntEvent += new CADability.Actions.ConstructAction.IntInput.SetIntDelegate(SetVerCountDown);
            //			verCountDown.CalculateIntEvent +=new CADability.Actions.ConstructAction.IntInput.CalculateIntDelegate(CalcVerCountDown);


            base.SetInput(horCountRight, verCountUp, horDist, verDist, dir, horCountLeft, verCountDown);

            base.OnSetAction();
            showMatrix();
        }

        /// <summary>
        /// Implements <see cref="Action.GetID"/>.
        /// </summary>
        public override string GetID()
        {
            return "CopyMatrixObjects";
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnDone ()"/>
        /// </summary>
		public override void OnDone()
        {
            using (base.Frame.Project.Undo.UndoFrame)
            {
                if (IsBackgroundTaskActive) WaitForBackgroundTask(); // damit block gemacht wird
                base.Frame.Project.GetActiveModel().Add(block.Clear());
                // folgendes ist zu langsam:
                // hier rückwärts zählen, da sonst Blödsinn rauskommt
                //for (int i = block.Count - 1; i >= 0; --i)
                //{
                //    base.Frame.Project.GetActiveModel().Add(block.Item(i));
                //}
            }
            base.ActiveObject = null; // damit es nicht gleich eingefügt wird

            base.OnDone();
        }

    }
}
