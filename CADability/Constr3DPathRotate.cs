using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;



namespace CADability.Actions
{
    internal class Constr3DPathRotate : ConstructAction
    {
        private CurveInput curveInput;
        private CurveInput rotateLineInput;
        private GeoVectorInput axisVectorInput;
        private GeoPointInput axisPointInput;
        private GeoPoint objectPoint;
        static private GeoVector axisVector;
        static private GeoPoint axisPoint;
        static private Boolean axisOrLine;
        private Boolean selectedMode;
        static private Angle angleRotation;
        static private Angle angleOffsetRotation;
        private GeoObjectList selectedObjectsList; // die Liste der selektierten Objekte
        // die folgenden Listen werden dazu synchron gefüllt mit Werten oder "null", bei OnDone zum Einfügen bzw Löschen ausgewertet.
        private List<IGeoObject> geoObjectOrgList; // Liste der beteiligten Originalobjekte, bei OnDone zum Löschen wichtig
        private List<IGeoObject> shapeList; // Die Liste der entstandenen Shapes, bei OnDone zum Einfügen gebraucht
        private List<Path> pathCreatedFromModelList; // Liste der evtl. syntetisch erzeugten Pfade zusammenhängender Objekte
        private List<IGeoObjectOwner> ownerList; // List der Owner der Objekte, wichti bei OnDone
        private Block blk; // da werden die Objekte drinn gesammelt


        private Constr3DPathRotate()
        { // wird über ":this" von den beiden nächsten Constructoren aufgerufen, initialisiert die Listen
            selectedObjectsList = new GeoObjectList();
            geoObjectOrgList = new List<IGeoObject>();
            shapeList = new List<IGeoObject>();
            ownerList = new List<IGeoObjectOwner>();
            pathCreatedFromModelList = new List<Path>();
        }


        public Constr3DPathRotate(GeoObjectList selectedObjectsList)
            : this()
        {
            selectedMode = (selectedObjectsList != null);
            if (selectedMode)
            {
                this.selectedObjectsList = selectedObjectsList.Clone();
                ListDefault(selectedObjectsList.Count); // setzt alle Listen auf gleiche Länge, Inhalte "null"
            };
        }

        public Constr3DPathRotate(Constr3DPathRotate autorepeat) : this()
        {
            if (autorepeat.selectedMode) throw new ApplicationException();
            selectedMode = false;
        }

        private void ListDefault(int number)
        { // setzt alle Listen auf gleiche Länge, Inhalte "null"
            geoObjectOrgList.Clear();
            shapeList.Clear();
            ownerList.Clear();
            pathCreatedFromModelList.Clear();
            for (int i = 0; i < number; i++)
            {
                geoObjectOrgList.Add(null);
                shapeList.Add(null);
                ownerList.Add(null);
                pathCreatedFromModelList.Add(null);
            }
        }


        void optionalOrg()
        {
            axisPointInput.Optional = !axisOrLine;
            axisVectorInput.Optional = !axisOrLine;
            rotateLineInput.Optional = axisOrLine;
        }


        private bool rotateOrg()
        {
            if (selectedObjectsList == null) return false;
            IGeoObject iGeoObjectSel;
            blk = Block.Construct(); // zur Darstellung
            if (base.ActiveObject != null) blk.CopyAttributes(base.ActiveObject);
            // der block wird das neue aktive Objekt, er muss die Attribute tragen, weil sie später
            // wieder von ihm verlangt werden
            Boolean success = false; // hat er wenigstens eins gefunden
            for (int i = 0; i < selectedObjectsList.Count; i++) // läuft über alle selektierten Objekte. Evtl nur eins bei Konstruktion
            {
                iGeoObjectSel = selectedObjectsList[i]; // zur Vereinfachung
                geoObjectOrgList[i] = iGeoObjectSel; // zum Weglöschen des Originals in onDone
                ownerList[i] = iGeoObjectSel.Owner; // owner merken für löschen 
                pathCreatedFromModelList[i] = null;
                shapeList[i] = null;
                Path p = null;
                if (iGeoObjectSel is ICurve)
                {
                    p = CADability.GeoObject.Path.CreateFromModel(iGeoObjectSel as ICurve, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
                    if (p == null)
                    {  // also nur Einzelelement
                        if (iGeoObjectSel is Path)
                        { // schon fertig
                            p = iGeoObjectSel.Clone() as Path;
                        }
                        else
                        {  // Pfad aus Einzelobjekt machen:
                            p = Path.Construct();
                            p.Set(new ICurve[] { iGeoObjectSel.Clone() as ICurve });
                        }
                    }
                    else
                    { // CreateFromModel hat was zusammengesetzt
                        geoObjectOrgList[i] = null; // zum nicht Weglöschen des Originals in onDone
                        pathCreatedFromModelList[i] = p; // kopie merken für onDone
                        p = p.Clone() as Path;
                        p.Flatten();
                    }
                    if (p != null)
                    { // 
                        if (angleOffsetRotation != 0.0)
                        {
                            ModOp m = ModOp.Rotate(axisPoint, axisVector, new SweepAngle(angleOffsetRotation));
                            p.Modify(m);
                        }
                        double sw = angleRotation;
                        if (sw == 0.0) sw = Math.PI * 2.0;
                        IGeoObject shape = Make3D.MakeRevolution(p, axisPoint, axisVector, sw, Frame.Project);
                        if (shape != null)
                        {
                            shape.CopyAttributes(blk);
                            // die Attribute müssen vom Block übernommen werden
                            shapeList[i] = shape; // fertiger Körper in shapeList
                            blk.Add(shape); // zum Darstellen
                            base.FeedBack.AddSelected(p); // zum Markieren des Ursprungsobjekts
                            success = true;

                        }
                    }
                }
            }
            if (success)
            {
                base.ActiveObject = blk; // darstellen
                base.ShowActiveObject = true;
                return true;
            }
            else
            {
                optionalOrg();
                base.ShowActiveObject = false;
                base.FeedBack.ClearSelected();
                return false;
            }
        }

        bool curveInputPath(ConstructAction.CurveInput sender, ICurve[] Curves, bool up)
        {	// ... nur die sinnvollen Kurven verwenden
            objectPoint = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {	// er hat was gewählt
                selectedObjectsList.Clear();
                base.FeedBack.ClearSelected();
                selectedObjectsList.Add(Curves[0]); // das eine in die Liste
                ListDefault(1); // die Listen löschen und mit "1" vorbesetzen
                if (rotateOrg()) return true;
            }
            base.ShowActiveObject = false;
            base.FeedBack.ClearSelected();
            return false;
        }

        void curveInputPathChanged(ConstructAction.CurveInput sender, ICurve SelectedCurve)
        {
            selectedObjectsList.Clear();
            base.FeedBack.ClearSelected();
            selectedObjectsList.Add(SelectedCurve); // das eine in die Liste
            ListDefault(1); // die Listen löschen und mit "1" vorbesetzen
            rotateOrg();
        }


        private bool RotateLine(CurveInput sender, ICurve[] Curves, bool up)
        {	// ... nur die sinnvolen Kurven verwenden
            ArrayList usableCurves = new ArrayList();
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
                else sender.SetCurves(Curves, Curves[0]);
            // erstmal den Urprungszustand herstellen, "block" ist ja schon gespiegelt 
            if (Curves.Length > 0)
            {	// einfach die erste Kurve nehmen
                ICurve iCurve = Curves[0];
                axisPoint = iCurve.StartPoint;
                axisVector = iCurve.StartDirection;
                axisOrLine = false;
                optionalOrg();
                return rotateOrg();
            }
            base.ShowActiveObject = false;
            return false;
        }

        private void RotateLineChanged(CurveInput sender, ICurve SelectedCurve)
        {
            axisPoint = SelectedCurve.StartPoint;
            axisVector = SelectedCurve.StartDirection;
            rotateOrg();
        }


        bool SetAxisVector(GeoVector vec)
        {
            if (Precision.IsNullVector(vec)) return false;
            axisVector = vec;
            axisOrLine = true;
            optionalOrg();
            return rotateOrg();
        }

        GeoVector GetAxisVector()
        {
            return axisVector;
        }

        void SetAxisPoint(GeoPoint p)
        {
            axisPoint = p;
            axisOrLine = true;
            optionalOrg();
            rotateOrg();
        }

        GeoPoint GetAxisPoint()
        {
            return axisPoint;
        }


        bool SetAngleInput(Angle angle)
        {
            // if (angle == 0.0) return false; sonst geht 360° nicht, da 360° als 0.0 ankommt
            angleRotation = angle;
            rotateOrg();
            return true;
        }

        Angle GetAngleInput()
        {
            return angleRotation;
        }

        bool SetAngleOffsetInput(Angle angle)
        {
            angleOffsetRotation = angle;
            rotateOrg();
            return true;
        }

        Angle GetAngleOffsetInput()
        {
            return angleOffsetRotation;
        }

        public override void OnSetAction()
        {
            base.ActiveObject = Face.Construct();

            if (axisVector.IsNullVector()) axisVector = GeoVector.XAxis;
            if (angleRotation == 0.0) angleRotation = Math.PI;

            base.TitleId = "Constr.Face.PathRotate";

            curveInput = new CurveInput("Constr.Face.PathRotate.Path");
            curveInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(curveInputPath);
            curveInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(curveInputPathChanged);
            if (selectedMode) curveInput.Fixed = true;

            rotateLineInput = new CurveInput("Constr.Face.PathRotate.AxisLine");
            rotateLineInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            rotateLineInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(RotateLine);
            rotateLineInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(RotateLineChanged);


            axisPointInput = new GeoPointInput("Constr.Face.PathRotate.AxisPoint", axisPoint);
            axisPointInput.SetGeoPointEvent += new GeoPointInput.SetGeoPointDelegate(SetAxisPoint);
            axisPointInput.GetGeoPointEvent += new GeoPointInput.GetGeoPointDelegate(GetAxisPoint);
            axisPointInput.ForwardMouseInputTo = curveInput;
            axisPointInput.DefinesBasePoint = true;

            axisVectorInput = new GeoVectorInput("Constr.Face.PathRotate.AxisVector", axisVector);
            axisVectorInput.SetGeoVectorEvent += new GeoVectorInput.SetGeoVectorDelegate(SetAxisVector);
            axisVectorInput.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetAxisVector);
            //            vectorInput.DefaultGeoVector = ConstrDefaults.DefaultExtrudeDirection;
            axisVectorInput.ForwardMouseInputTo = curveInput;
            optionalOrg();

            AngleInput angleInput = new AngleInput("Constr.Face.PathRotate.Angle", angleRotation);
            angleInput.SetAngleEvent += new AngleInput.SetAngleDelegate(SetAngleInput);
            angleInput.GetAngleEvent += new AngleInput.GetAngleDelegate(GetAngleInput);

            AngleInput angleOffsetInput = new AngleInput("Constr.Face.PathRotate.AngleOffset", angleOffsetRotation);
            angleOffsetInput.SetAngleEvent += new AngleInput.SetAngleDelegate(SetAngleOffsetInput);
            angleOffsetInput.GetAngleEvent += new AngleInput.GetAngleDelegate(GetAngleOffsetInput);
            angleOffsetInput.Optional = true;

            base.SetInput(curveInput, rotateLineInput, axisPointInput, axisVectorInput, angleInput, angleOffsetInput);
            base.ShowAttributes = true;
            base.OnSetAction();
        }



        public override string GetID()
        { return "Constr.Face.PathRotate"; }

        public override void OnDone()
        {
            if (base.ShowActiveObject == true)
            {	// es soll ein shape eingefügt werden
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    for (int i = 0; i < selectedObjectsList.Count; i++) // geht durch die komplette Liste
                    {
                        if (shapeList[i] != null) // nur hier was machen!
                        {
                            if (Precision.IsNull(angleOffsetRotation) && Frame.GetBooleanSetting("Construct.3D_Delete2DBase", false))
                            {
                                if (geoObjectOrgList[i] != null) // evtl. Einzelobjekt (Object oder Path) als Original rauslöschen
                                    ownerList[i].Remove(geoObjectOrgList[i] as IGeoObject);
                                else
                                { // die Einzelelemente des CreateFromModel identifizieren
                                    for (int j = 0; j < pathCreatedFromModelList[i].Count; ++j)
                                    {
                                        IGeoObject obj = null;
                                        if (pathCreatedFromModelList[i].Curve(j).UserData.ContainsData("CADability.Path.Original"))
                                            obj = pathCreatedFromModelList[i].Curve(j).UserData.GetData("CADability.Path.Original") as IGeoObject;
                                        if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
                                    }
                                }
                            }
                        }
                    }
                    base.DisassembleBlock = true; // da das ActiveObject aus einem Block besteht: Nur die Einzelteile einfügen!
                    base.OnDone();
                }
            }
            base.OnDone();
        }
    }
}




