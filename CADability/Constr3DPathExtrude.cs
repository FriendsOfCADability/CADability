using CADability.GeoObject;
using System;
using System.Collections.Generic;

namespace CADability.Actions
{
    internal class Constr3DPathExtrude : ConstructAction
    {
        private CurveInput curveInput;
        private GeoVectorInput vectorInput;
        private GeoVectorInput vectorOffsetInput;
        private BooleanInput planeInput;
        private LengthInput heightInput;
        private LengthInput heightOffsetInput;
        private CurveInput pipeInput;
        private GeoPoint objectPoint;
        //        private ICurve iCurveSel;
        //        private ICurve iCurveOrg;
        private ICurve iCurvePipe;
        //        private IGeoObjectOwner owner;
        //        private Path pathCreatedFromModel;
        static private Boolean planeMode = true;
        //        static private Boolean VectorOrHeight;
        static private int extrudeMode;
        static private GeoVector vector;
        static private GeoVector vectorOffset;
        static private Double height;
        static private Double heightOffset;
        private Boolean selectedMode;
        private Path pipePath;
        private GeoObjectList selectedObjectsList; // die Liste der selektierten Objekte
        // die folgenden Listen werden dazu synchron gefüllt mit Werten oder "null", bei OnDone zum Einfügen bzw Löschen ausgewertet.
        private List<IGeoObject> geoObjectOrgList; // Liste der beteiligten Originalobjekte, bei OnDone zum Löschen wichtig
        private List<IGeoObject> shapeList; // Die Liste der entstandenen Shapes, bei OnDone zum Einfügen gebraucht
        private List<Path> pathCreatedFromModelList; // Liste der evtl. syntetisch erzeugten Pfade zusammenhängender Objekte
        private List<IGeoObjectOwner> ownerList; // List der Owner der Objekte, wichti bei OnDone
        private Block blk; // da werden die Objekte drinn gesammelt

        public static bool pathTest(GeoObjectList selectedObjects)
        {
            for (int i = 0; i < selectedObjects.Count; i++)
            {   // nur eins muss passen
                if ((selectedObjects[i] is ICurve)) return true;
            }
            return false;
        }


        private Constr3DPathExtrude()
        { // wird über ":this" von den beiden nächsten Constructoren aufgerufen, initialisiert die Listen
            selectedObjectsList = new GeoObjectList();
            geoObjectOrgList = new List<IGeoObject>();
            shapeList = new List<IGeoObject>();
            ownerList = new List<IGeoObjectOwner>();
            pathCreatedFromModelList = new List<Path>();
        }

        public Constr3DPathExtrude(GeoObjectList selectedObjectsList) : this()
        {
            selectedMode = (selectedObjectsList != null);
            if (selectedMode)
            {
                this.selectedObjectsList = selectedObjectsList.Clone();
                ListDefault(selectedObjectsList.Count); // setzt alle Listen auf gleiche Länge, Inhalte "null"
            };
        }
        public Constr3DPathExtrude(Face face) : this()
        {
            selectedMode = false;
            this.selectedObjectsList = new GeoObjectList(face);
            ListDefault(selectedObjectsList.Count); // initializes the lists
        }

        public Constr3DPathExtrude(Constr3DPathExtrude autorepeat) : this()
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
            switch (extrudeMode)
            {
                case 0: // default
                case 1: // höhe:
                    heightInput.Optional = false;
                    if (planeInput!=null) planeInput.Optional = false;
                    if (vectorInput != null) vectorInput.Optional = true;
                    if (pipeInput != null) pipeInput.Optional = true;
                    break;
                case 2: // Vector:
                    heightInput.Optional = true;
                    if (planeInput != null) planeInput.Optional = true;
                    if (vectorInput != null) vectorInput.Optional = false;
                    if (pipeInput != null) pipeInput.Optional = true;
                    break;
                case 3:
                    heightInput.Optional = true;
                    if (planeInput != null) planeInput.Optional = true;
                    if (vectorInput != null) vectorInput.Optional = true;
                    if (pipeInput != null) pipeInput.Optional = false;
                    break;
            }
            //heightInput.Optional = VectorOrHeight;
            //planeInput.Optional = VectorOrHeight;
            //vectorInput.Optional = !VectorOrHeight;
        }

        private bool extrudeOrg()
        {
            if (selectedObjectsList == null) return false;
            IGeoObject iGeoObjectSel;
            blk = Block.Construct(); // zur Darstellung
            if (base.ActiveObject != null) blk.CopyAttributes(base.ActiveObject);
            // der block wird das neue aktive Objekt, er muss die Attribute tragen, weil sie später
            // wieder von ihm verlangt werden
            Boolean success = false; // hat er wenigstens eins gefunden
            int extrudeModeTemp = extrudeMode; // Merker, da extrudeMode evtl. umgeschaltet wird
            for (int i = 0; i < selectedObjectsList.Count; i++) // läuft über alle selektierten Objekte. Evtl nur eins bei Konstruktion
            {
                extrudeMode = extrudeModeTemp;
                iGeoObjectSel = selectedObjectsList[i]; // zur Vereinfachung
                geoObjectOrgList[i] = iGeoObjectSel; // zum Weglöschen des Originals in onDone
                ownerList[i] = iGeoObjectSel.Owner; // owner merken für löschen 
                pathCreatedFromModelList[i] = null;
                shapeList[i] = null;
                Path p = null;
                if (iGeoObjectSel is ICurve)
                {

                    p = Path.CreateFromModel(iGeoObjectSel as ICurve, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
                    if (p == null)
                    { // also nur Einzelelement
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
                }
                if (p != null)
                { // Objekt hat keine Ebene und Objektebene eingestellt: Höheneingabe sperren
                    //                heightInput.ReadOnly = ((p.GetPlanarState() != PlanarState.Planar) && planeMode);
                    if (planeMode)
                    {
                        if (p.GetPlanarState() != PlanarState.Planar)
                        {
                            heightInput.ReadOnly = true;
                            //                        heightOffsetInput.ReadOnly = true;
                            //                        VectorOrHeight = true;
                            extrudeMode = 2;
                            optionalOrg();
                        }
                        else
                        {
                            Plane pl = p.GetPlane();
                            pl.Align(base.ActiveDrawingPlane, false, true);

                            //                        planeVector = pl.Normal;
                            vector = height * pl.Normal;
                            vectorOffset = heightOffset * pl.Normal;
                        }
                    }
                    if (!vectorOffset.IsNullVector())
                    {
                        ModOp m = ModOp.Translate(vectorOffset);
                        p.Modify(m);
                    }
                    IGeoObject shape;
                    if (pipePath == null)
                    {
                        if (p.IsClosed) shape = Make3D.MakePrism(p, vector, Frame.Project, true);
                        else shape = Make3D.Extrude(p, vector, Frame.Project);
                    }
                    else shape = Make3D.MakePipe(p, pipePath, Frame.Project);
                    if (shape != null)
                    {
                        //                        shape.CopyAttributes(iGeoObjectSel as IGeoObject);
                        shape.CopyAttributes(blk);
                        // die Attribute müssen vom Block übernommen werden
                        shapeList[i] = shape; // fertiger Körper in shapeList
                        blk.Add(shape); // zum Darstellen
                        base.FeedBack.AddSelected(p); // zum Markieren des Ursprungsobjekts
                        success = true;
                    }
                }
                //            VectorOrHeight = VectorOrHeightTemp;
            }
            extrudeMode = extrudeModeTemp;
            if (success)
            {
                base.ActiveObject = blk; // darstellen
                base.ShowActiveObject = true;
                return true;
            }
            else
            {
                optionalOrg();
                heightInput.ReadOnly = false;
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
                selectedObjectsList.Add(Curves[0] as IGeoObject); // das eine in die Liste
                ListDefault(1); // die Listen löschen und mit "1" vorbesetzen
                if (extrudeOrg()) return true;
            }
            heightInput.ReadOnly = false;
            base.FeedBack.ClearSelected();
            base.ShowActiveObject = false;
            return false;
        }

        void curveInputPathChanged(ConstructAction.CurveInput sender, ICurve SelectedCurve)
        {
            selectedObjectsList.Clear();
            base.FeedBack.ClearSelected();
            selectedObjectsList.Add(SelectedCurve as IGeoObject); // das eine in die Liste
            ListDefault(1); // die Listen löschen und mit "1" vorbesetzen
                            //            iCurveSel = SelectedCurve;
            extrudeOrg();
        }

        bool SetVector(GeoVector vec)
        {
            if (Precision.IsNullVector(vec)) return false;
            vector = vec;
            //            VectorOrHeight = true; // merken, ob Extrudier-Vektor oder Höhe
            extrudeMode = 2; // merken, ob Extrudier-Vektor oder Höhe
            optionalOrg();
            return extrudeOrg();
        }

        GeoVector GetVector()
        {
            return vector;
        }

        bool SetHeight(double Length)
        {
            if (Precision.IsNull(Length)) return false;
            height = Length;
            if (!planeMode)
            { // Zeichnungsebene als Bezug
                vector = Length * base.ActiveDrawingPlane.Normal;
            }
            //            VectorOrHeight = false; // merken, ob Extrudier-Vektor oder Höhe
            extrudeMode = 1; // merken, ob Extrudier-Vektor oder Höhe
            optionalOrg();
            return extrudeOrg();
        }

        double GetHeight()
        {
            return height;
        }


        void SetPlaneMode(bool val)
        {
            planeMode = val;
            if (!planeMode)
            { // Zeichnungsebene als Bezug
                vector = height * base.ActiveDrawingPlane.Normal;
            }
            //           VectorOrHeight = false; // merken, ob Extrudier-Vektor oder Höhe
            extrudeMode = 1; // merken, ob Extrudier-Vektor oder Höhe
            optionalOrg();
            extrudeOrg();
        }

        bool SetVectorOffset(GeoVector vec)
        {
            vectorOffset = vec;
            //           VectorOrHeight = true; // merken, ob Extrudier-Vektor oder Höhe
            extrudeMode = 2; // merken, ob Extrudier-Vektor oder Höhe
            optionalOrg();
            return extrudeOrg();
        }

        GeoVector GetVectorOffset()
        {
            return vectorOffset;
        }

        bool SetHeightOffset(double Length)
        {
            heightOffset = Length;
            if (!planeMode)
            { // Zeichnungsebene als Bezug
                vectorOffset = Length * base.ActiveDrawingPlane.Normal;
            }
            //           VectorOrHeight = false; // merken, ob Extrudier-Vektor oder Höhe
            extrudeMode = 1; // merken, ob Extrudier-Vektor oder Höhe
            optionalOrg();
            return extrudeOrg();
        }

        double GetHeightOffset()
        {
            return heightOffset;
        }

        bool pipeTest()
        {
            int extrudeModeTemp = extrudeMode;
            Path p = null;
            p = CADability.GeoObject.Path.CreateFromModel(iCurvePipe, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
            if (p == null)
            { // also nur Einzelelement
                if (iCurvePipe is Path)
                { // schon fertig
                    p = iCurvePipe.Clone() as Path;
                }
                else
                {  // Pfad aus Einzelobjekt machen:
                    p = Path.Construct();
                    p.Set(new ICurve[] { iCurvePipe.Clone() });
                }
            }
            else
            { // CreateFromModel hat was zusammengesetzt
                p.Flatten();
            }
            if (p != null)
            {
                extrudeMode = 3; // merken, ob Extrudier-Vektor, Höhe oder Pfad
                optionalOrg();
                pipePath = p;
                if (curveInput.Fixed)
                { if (extrudeOrg()) return true; }
                return true;
            }
            extrudeMode = extrudeModeTemp;
            return false;
        }

        bool pipeInputCurves(ConstructAction.CurveInput sender, ICurve[] Curves, bool up)
        {   // ... nur die sinnvollen Kurven verwenden
            //            objectPoint = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {	// er hat was gewählt
                iCurvePipe = Curves[0];

                //Path p = null;
                //p = CADability.GeoObject.Path.CreateFromModel(iCurvePipe, base.Frame.ActiveView.ProjectedModel, true);
                //if (p == null)
                //{ // also nur Einzelelement
                //    if (iCurvePipe is Path)
                //    { // schon fertig
                //        p = iCurvePipe.Clone() as Path;
                //    }
                //    else
                //    {  // Pfad aus Einzelobjekt machen:
                //        p = Path.Construct();
                //        p.Set(new ICurve[] { iCurvePipe.Clone() });
                //    }
                //}
                //else
                //{ // CreateFromModel hat was zusammengesetzt
                //    p.Flatten();
                //}
                //if (p != null)
                //{
                //    extrudeMode = 3; // merken, ob Extrudier-Vektor, Höhe oder Pfad
                //    optionalOrg();
                //    pipePath = p;
                //    if (curveInput.Fixed)
                //    { if (extrudeOrg()) return true; }
                //    return true;
                //}
                if (pipeTest()) return true;
            }
            //            base.ActiveObject = null;
            pipePath = null;
            base.ShowActiveObject = false;
            return false;
        }

        void pipeInputCurveChanged(ConstructAction.CurveInput sender, ICurve SelectedCurve)
        {
            iCurvePipe = SelectedCurve;
            pipeTest();
        }

        public override void OnSetAction()
        {
            base.ActiveObject = Face.Construct();
            DefaultLength l = new DefaultLength(ConstructAction.DefaultLength.StartValue.ViewWidth8);
            if (vector.IsNullVector()) vector = l * base.ActiveDrawingPlane.Normal;
            if (height == 0.0) { height = l; };
            //           if (!VectorOrHeight)
            if (extrudeMode < 2)
            {
                vector = height * base.ActiveDrawingPlane.Normal;
            }
            base.TitleId = "Constr.Face.PathExtrude";

            curveInput = new CurveInput("Constr.Face.PathExtrude.Path");
            curveInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(curveInputPath);
            curveInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(curveInputPathChanged);
            if (selectedMode) curveInput.Fixed = true;

            heightInput = new LengthInput("Constr.Face.PathExtrude.Height");
            heightInput.SetLengthEvent += new LengthInput.SetLengthDelegate(SetHeight);
            heightInput.GetLengthEvent += new LengthInput.GetLengthDelegate(GetHeight);
            heightInput.ForwardMouseInputTo = curveInput;
            if (selectedMode) extrudeOrg(); //schonmal berechnen und markieren!

            planeInput = new BooleanInput("Constr.Face.PathExtrude.Plane", "Constr.Face.PathExtrude.Plane.Values", planeMode);
            planeInput.SetBooleanEvent += new BooleanInput.SetBooleanDelegate(SetPlaneMode);

            vectorInput = new GeoVectorInput("Constr.Face.PathExtrude.Vector", vector);
            vectorInput.SetGeoVectorEvent += new GeoVectorInput.SetGeoVectorDelegate(SetVector);
            vectorInput.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetVector);
            //            vectorInput.DefaultGeoVector = ConstrDefaults.DefaultExtrudeDirection;
            vectorInput.ForwardMouseInputTo = curveInput;

            heightOffsetInput = new LengthInput("Constr.Face.PathExtrude.HeightOffset");
            heightOffsetInput.SetLengthEvent += new LengthInput.SetLengthDelegate(SetHeightOffset);
            heightOffsetInput.GetLengthEvent += new LengthInput.GetLengthDelegate(GetHeightOffset);
            heightOffsetInput.ForwardMouseInputTo = curveInput;
            heightOffsetInput.Optional = true;

            vectorOffsetInput = new GeoVectorInput("Constr.Face.PathExtrude.VectorOffset", vectorOffset);
            vectorOffsetInput.SetGeoVectorEvent += new GeoVectorInput.SetGeoVectorDelegate(SetVectorOffset);
            vectorOffsetInput.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetVectorOffset);
            vectorOffsetInput.ForwardMouseInputTo = curveInput;
            vectorOffsetInput.Optional = true;

            pipeInput = new CurveInput("Constr.Face.PathExtrude.Pipe");
            pipeInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(pipeInputCurves);
            pipeInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(pipeInputCurveChanged);

            optionalOrg();

            SeparatorInput separatorHeight = new SeparatorInput("Constr.Face.PathExtrude.SeparatorHeight");
            SeparatorInput separatorVector = new SeparatorInput("Constr.Face.PathExtrude.SeparatorVector");
            SeparatorInput separatorPipe = new SeparatorInput("Constr.Face.PathExtrude.SeparatorPipe");

            //            if (selectedMode)
            //                base.SetInput( heightInput, heightOffsetInput, separatorVector, vectorInput, vectorOffsetInput, separatorPipe, pipeInput, planeInput);
            //             else base.SetInput(curveInput,separatorHeight, heightInput, heightOffsetInput,separatorVector,vectorInput, vectorOffsetInput,separatorPipe,pipeInput, planeInput);
            base.SetInput(curveInput, separatorHeight, heightInput, heightOffsetInput, planeInput, separatorVector, vectorInput, vectorOffsetInput, separatorPipe, pipeInput);
            base.ShowAttributes = true;
            base.OnSetAction();
        }



        public override string GetID()
        { return "Constr.Face.PathExtrude"; }

        public override void OnDone()
        {
            //            if (base.ActiveObject != null)
            if (base.ShowActiveObject == true)
            {	// es soll ein shape eingefügt werden
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    for (int i = 0; i < selectedObjectsList.Count; i++) // geht durch die komplette Liste
                    {
                        if (shapeList[i] != null) // nur hier was machen!
                        {
                            if (vectorOffset.IsNullVector() && Frame.GetBooleanSetting("Construct.3D_Delete2DBase", false))
                            {
                                if (geoObjectOrgList[i] != null) // evtl. Einzelobjekt (Object oder Path) als Original rauslöschen
                                    ownerList[i].Remove(geoObjectOrgList[i] as IGeoObject);
                                else
                                { // die Einzelelemente des CreateFromModel identifizieren
                                    for (int j = 0; j < pathCreatedFromModelList[i].Count; ++j)
                                    {
                                        IGeoObject obj = null;
                                        if ((pathCreatedFromModelList[i].Curve(j) as IGeoObject).UserData.ContainsData("CADability.Path.Original"))
                                            obj = (pathCreatedFromModelList[i].Curve(j) as IGeoObject).UserData.GetData("CADability.Path.Original") as IGeoObject;
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
