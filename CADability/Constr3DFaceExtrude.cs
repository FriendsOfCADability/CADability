using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections.Generic;

namespace CADability.Actions
{
    public class Constr3DFaceExtrude : ConstructAction
    {
        private GeoObjectInput geoObjectInput;
        private GeoPointInput innerPointInput;
        private GeoVectorInput vectorInput;
        private GeoVectorInput vectorOffsetInput;
        private BooleanInput planeInput;
        private LengthInput heightInput;
        private LengthInput heightOffsetInput;
        private CurveInput pipeInput;
        private GeoPoint objectPoint;
        //        private IGeoObject  iGeoObjectSel;
        //        private IGeoObject iGeoObjectOrg;
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
        private bool innerPointUp;


        public static bool faceTestExtrude(GeoObjectList selectedObjects)
        {
            for (int i = 0; i < selectedObjects.Count; i++)
            {   // nur eins muss passen
                if ((selectedObjects[i] is Face) || (selectedObjects[i] is Shell) || (selectedObjects[i] is Text) || ((selectedObjects[i] is ICurve) && (selectedObjects[i] as ICurve).IsClosed)) return true;
            }
            return false;
        }


        //public Constr3DFaceExtrude(IGeoObject iGeoObject)
        //{
        //    selectedMode = (iGeoObject != null);
        //    if (selectedMode) { iGeoObjectSel = iGeoObject; };
        //}

        private Constr3DFaceExtrude()
        { // wird über ":this" von den beiden nächsten Constructoren aufgerufen, initialisiert die Listen
            selectedObjectsList = new GeoObjectList();
            geoObjectOrgList = new List<IGeoObject>();
            shapeList = new List<IGeoObject>();
            ownerList = new List<IGeoObjectOwner>();
            pathCreatedFromModelList = new List<Path>();
        }

        public Constr3DFaceExtrude(GeoObjectList selectedObjectsList)
            : this()
        {
            selectedMode = (selectedObjectsList != null);
            if (selectedMode)
            {
                GeoObjectList clonedList = selectedObjectsList.Clone();

                GeoObjectList curves = new GeoObjectList();
                for (int i = clonedList.Count - 1; i >= 0; --i)
                {
                    if (clonedList[i] is ICurve)
                    {
                        curves.Add(clonedList[i]);
                        clonedList.Remove(i);
                    }
                    else if (clonedList[i] is Text text)
                    {
                        clonedList.Remove(i);
                        CompoundShape[] shapes = text.GetShapes();
                        for (int j = 0; j < shapes.Length; j++)
                        {
                            for (int k = 0; k < shapes[j].SimpleShapes.Length; k++)
                            {
                                Face fc = Face.MakeFace(new PlaneSurface(Plane.XYPlane), shapes[j].SimpleShapes[k]);
                                fc.Modify(text.GlyphToWorld);
                                clonedList.Add(fc);
                            }
                        }
                    }
                }
                Plane pln;
                CompoundShape cs = CompoundShape.CreateFromList(curves, Precision.eps, out pln);
                if (cs != null && !cs.Empty)
                {   // man konnte ein CompoundShape erzeugen, dann dieses zu Faces machen und verwenden
                    for (int i = 0; i < cs.SimpleShapes.Length; i++)
                    {
                        PlaneSurface ps = new PlaneSurface(pln);
                        Face toAdd = Face.MakeFace(ps, cs.SimpleShapes[i]);
                        toAdd.CopyAttributes(curves[0]);
                        clonedList.Add(toAdd);
                    }
                }
                this.selectedObjectsList = clonedList;
                ListDefault(this.selectedObjectsList.Count); // setzt alle Listen auf gleiche Länge, Inhalte "null"
            };
        }

        public Constr3DFaceExtrude(Face face)
            : this()
        {
            selectedMode = true;
            this.selectedObjectsList = new GeoObjectList(face);
            ListDefault(this.selectedObjectsList.Count); // setzt alle Listen auf gleiche Länge, Inhalte "null"
        }
        public Constr3DFaceExtrude(Constr3DFaceExtrude autorepeat)
            : this()
        {
            //if (autorepeat.selectedMode) throw new ApplicationException();
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
                case 0: // default:
                case 1: // höhe:
                    heightInput.Optional = false;
                    planeInput.Optional = false;
                    vectorInput.Optional = true;
                    pipeInput.Optional = true;
                    break;
                case 2: // Vector:
                    heightInput.Optional = true;
                    planeInput.Optional = true;
                    vectorInput.Optional = false;
                    pipeInput.Optional = true;
                    break;
                case 3:
                    heightInput.Optional = true;
                    planeInput.Optional = true;
                    vectorInput.Optional = true;
                    pipeInput.Optional = false;
                    break;
            }
            //            heightInput.Optional = VectorOrHeight;
            //            planeInput.Optional = VectorOrHeight;
            //            vectorInput.Optional = !VectorOrHeight;
        }



        private bool extrudeOrg()
        {
            if (selectedObjectsList == null) return false;
            IGeoObject iGeoObjectSel;
            //         IGeoObject iGeoObjectOrg;
            blk = Block.Construct(); // zur Darstellung
            if (base.ActiveObject != null) blk.CopyAttributes(base.ActiveObject);
            // der block wird das neue aktive Objekt, er muss die Attribute tragen, weil sie später
            // wieder von ihm verlangt werden
            blk.IsTmpContainer = true;
            Boolean success = false; // hat er wenigstens eins gefunden
            int extrudeModeTemp = extrudeMode; // Merker, da extrudeMode evtl. umgeschaltet wird
            Plane pln;
            CompoundShape dbg = CompoundShape.CreateFromList(selectedObjectsList, Precision.eps, out pln);
            for (int i = 0; i < selectedObjectsList.Count; i++) // läuft über alle selektierten Objekte. Evtl nur eins bei Konstruktion
            {
                IGeoObject iGeoObjectTemp = null; // lokaler Merker
                extrudeMode = extrudeModeTemp;
                iGeoObjectSel = selectedObjectsList[i]; // zur Vereinfachung
                geoObjectOrgList[i] = iGeoObjectSel; // zum Weglöschen des Originals in onDone
                ownerList[i] = iGeoObjectSel.Owner; // owner merken für löschen 
                pathCreatedFromModelList[i] = null;
                shapeList[i] = null;
                //            Boolean VectorOrHeightTemp = VectorOrHeight;
                if ((iGeoObjectSel is Face) || (iGeoObjectSel is Shell))
                { // nur kopieren
                    iGeoObjectTemp = iGeoObjectSel.Clone();
                }
                else
                {
                    if (iGeoObjectSel is ICurve)
                    {
                        Path p = null;
                        p = CADability.GeoObject.Path.CreateFromModel(iGeoObjectSel as ICurve, base.Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
                        if (p == null)
                        { // also nur Einzelelement
                            if (iGeoObjectSel is Path)
                            { // schon fertig
                                p = iGeoObjectSel.Clone() as Path;
                            }
                            else
                            {  // Pfad aus Einzelobjekt machen:
                                p = Path.Construct();
                                if (iGeoObjectSel is Polyline)
                                {
                                    p.Set(new ICurve[] { iGeoObjectSel.Clone() as ICurve });
                                    p.Flatten();
                                }
                                else if (iGeoObjectSel is Ellipse && !(iGeoObjectSel as Ellipse).IsArc)
                                {
                                    Ellipse elli = iGeoObjectSel as Ellipse;
                                    p.Set(elli.Split(0.5)); // zwei Halbkreise
                                }
                                else
                                {
                                    p.Set(new ICurve[] { iGeoObjectSel.Clone() as ICurve });
                                }
                            }
                        }
                        else
                        { // CreateFromModel hat was zusammengesetzt
                            if (p.IsClosed)
                            {
                                //                                iGeoObjectOrg = null; // zum nicht Weglöschen des Originals in onDone
                                geoObjectOrgList[i] = null; // zum nicht Weglöschen des Originals in onDone
                                pathCreatedFromModelList[i] = p; // kopie merken für onDone
                                p = p.Clone() as Path;
                                p.Flatten();
                            }
                        }
                        if (p.IsClosed)
                        { // jetzt face machen
                            iGeoObjectTemp = Make3D.MakeFace(p, Frame.Project);
                        }
                    }
                }
                if (iGeoObjectTemp != null)
                { // also was geeignetes dabei
                    if (planeMode) // Objektebene soll Grundlage sein
                    {
                        Face faceTemp = null;
                        if (iGeoObjectTemp is Face)
                        {
                            faceTemp = (iGeoObjectTemp as Face);
                        }
                        if ((iGeoObjectTemp is Shell) && ((iGeoObjectTemp as Shell).Faces.Length == 1))
                        {
                            faceTemp = (iGeoObjectTemp as Shell).Faces[0];
                        }
                        if (faceTemp != null)
                        {
                            if (faceTemp.Surface is PlaneSurface)
                            {
                                GeoVector faceNormal = faceTemp.Surface.GetNormal(GeoPoint2D.Origin);
                                // GeoVector temp = base.ActiveDrawingPlane.ToLocal(faceNormal);
                                // die ActiveDrawingPlane ändert sich wenn immer der Cursor durch ein Fenster bewegt wird
                                // das hat manchmal komische Effekte. deshalb hier die etwas statischere
                                // ActiveView.Projection.DrawingPlane verwenden
                                GeoVector temp = base.Frame.ActiveView.Projection.DrawingPlane.ToLocal(faceNormal);
                                if (temp.z < 0.0)
                                {
                                    faceNormal.Reverse();
                                }
                                vector = height * faceNormal;
                                vectorOffset = heightOffset * faceNormal;
                            }
                            else
                            {
                                heightInput.ReadOnly = true;
                                extrudeMode = 2;
                                //                            VectorOrHeight = true;
                                optionalOrg();
                            }
                        }
                    }
                    if (!vectorOffset.IsNullVector())
                    {
                        ModOp m = ModOp.Translate(vectorOffset);
                        iGeoObjectTemp.Modify(m);
                    }
                    //                IGeoObject shape = Make3D.MakePrism(iGeoObjectTemp, vector, Frame.Project);
                    IGeoObject shape;
                    if (pipePath == null)
                        shape = Make3D.MakePrism(iGeoObjectTemp, vector, Frame.Project, false);
                    else shape = Make3D.MakePipe(iGeoObjectTemp, pipePath, Frame.Project);
                    if (shape != null)
                    {
                        //                        shape.CopyAttributes(iGeoObjectSel as IGeoObject);
                        shape.CopyAttributes(blk);
                        // die Attribute müssen vom Block übernommen werden
                        shapeList[i] = shape; // fertiger Körper in shapeList
                        blk.Add(shape); // zum Darstellen
                        base.FeedBack.AddSelected(iGeoObjectTemp); // zum Markieren des Ursprungsobjekts
                        success = true;
                    }
                }
                //            VectorOrHeight = VectorOrHeightTemp;
            } // for-Schleife über die Objekte der Liste
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

        bool geoObjectInputFace(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {   // ... nur die sinnvollen Kurven verwenden
            // TODO: evtl Mehrfachanwahl ermöglichen
            objectPoint = base.CurrentMousePosition;
            if (up)
                if (TheGeoObjects.Length == 0) sender.SetGeoObject(TheGeoObjects, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetGeoObject(TheGeoObjects, TheGeoObjects[0]);
            if (TheGeoObjects.Length > 0)
            {	// er hat was gewählt
                //selectedObjectsList.Add(TheGeoObjects[0]); // das eine in die Liste
                //ListDefault(selectedObjectsList.Count); // die Listen löschen und mit "1" vorbesetzen
                selectedObjectsList.Clear();
                base.FeedBack.ClearSelected();
                selectedObjectsList.Add(TheGeoObjects[0]); // das eine in die Liste
                ListDefault(1); // die Listen löschen und mit "1" vorbesetzen
                if (extrudeOrg()) return true;
            }
            heightInput.ReadOnly = false;
            base.ShowActiveObject = false;
            base.FeedBack.ClearSelected();
            return false;
        }

        void geoObjectInputFaceChanged(ConstructAction.GeoObjectInput sender, IGeoObject SelectedGeoObject)
        {
            selectedObjectsList.Clear();
            base.FeedBack.ClearSelected();
            selectedObjectsList.Add(SelectedGeoObject); // das eine in die Liste
            ListDefault(1); // die Listen löschen und mit "1" vorbesetzen
            extrudeOrg();
        }


        void SetInnerPointInput(GeoPoint p)
        {
            if (innerPointUp)
            {
                CompoundShape shape = FindShape(p, base.ActiveDrawingPlane);
                if (!(shape == null))
                {
                    Face[] faces = shape.MakeFaces(base.ActiveDrawingPlane);
                    if (faces.Length > 0)
                    {
                        selectedObjectsList.Clear();
                        base.FeedBack.ClearSelected();
                        base.Frame.Project.SetDefaults(faces[0]);
                        selectedObjectsList.Add(faces[0]); // das eine in die Liste
                        ListDefault(1); // die Listen löschen und mit "1" vorbesetzen
                        if (extrudeOrg())
                        {
                            geoObjectInput.Fixed = true;
                            geoObjectInput.Optional = true;
                        }
                        else
                        {
                            geoObjectInput.Fixed = false;
                            geoObjectInput.Optional = false;
                        }
                    }
                }
            }
            innerPointUp = false;
        }

        void innerPointInputMouseClickEvent(bool up, GeoPoint MousePosition, IView View)
        {
            if (up)
            {
                innerPointUp = true;
            }
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

        double CalculateHeight(GeoPoint MousePosition)
        {
            if (!planeMode)
            { // Zeichnungsebene als Bezug
                double h = base.ActiveDrawingPlane.Distance(MousePosition);
                if (!Precision.IsNull(h)) return h;
            }
            else
            {
                if (geoObjectOrgList.Count>0 && geoObjectOrgList[0] is Face fc && fc.Surface is PlaneSurface ps)
                {
                    return ps.Plane.Distance(MousePosition);
                }
            }
            return height;
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
            extrudeMode = 1; // merken, ob Extrudier-Vektor oder Höhe
                             //            VectorOrHeight = false; // merken, ob Extrudier-Vektor oder Höhe
            optionalOrg();
            extrudeOrg();
        }

        bool SetVectorOffset(GeoVector vec)
        {
            vectorOffset = vec;
            //            VectorOrHeight = true; // merken, ob Extrudier-Vektor oder Höhe
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
            //            VectorOrHeight = false; // merken, ob Extrudier-Vektor oder Höhe
            extrudeMode = 1; // merken, ob Extrudier-Vektor oder Höhe
            optionalOrg();
            return extrudeOrg();
        }

        double GetHeightOffset()
        {
            return heightOffset;
        }

        private void SetInsertMode(int val)
        {
            Frame.SolidModellingMode = (Make3D.SolidModellingMode)val;
        }

        int GetInsertMode()
        {
            return (int)(Frame.SolidModellingMode);
        }

        bool pipeTest()
        {
            int extrudeModeTemp = extrudeMode;
            Path p = null;
            if (iCurvePipe is Path) p = (iCurvePipe.Clone() as Path);
            else
            {
                p = Path.Construct();
                p.Set(new ICurve[] { iCurvePipe.Clone() });
            }
            // p = CADability.GeoObject.Path.CreateFromModel(iCurvePipe, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
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
            p.Flatten();
            //}
            if (p != null)
            {
                extrudeMode = 3; // merken, ob Extrudier-Vektor, Höhe oder Pfad
                optionalOrg();
                pipePath = p;
                if (geoObjectInput.Fixed)
                {
                    if (extrudeOrg()) return true;
                }
                return true;
            }
            extrudeMode = extrudeModeTemp;
            return false;
        }

        bool pipeInputCurves(ConstructAction.CurveInput sender, ICurve[] Curves, bool up)
        {	// ... nur die sinnvollen Kurven verwenden
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


        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
        /// </summary>
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
            base.TitleId = "Constr.Solid.FaceExtrude";

            geoObjectInput = new GeoObjectInput("Constr.Solid.FaceExtrude.Path");
            geoObjectInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(geoObjectInputFace);
            geoObjectInput.GeoObjectSelectionChangedEvent += new GeoObjectInput.GeoObjectSelectionChangedDelegate(geoObjectInputFaceChanged);
            if (selectedMode) geoObjectInput.Fixed = true; // schon fertig, da Werte reingekommen sind
            //geoObjectInput.MultipleInput = true;
            //geoObjectInput.Optional = true;

            innerPointInput = new GeoPointInput("Constr.Solid.FaceExtrude.InnerPoint");
            innerPointInput.SetGeoPointEvent += new GeoPointInput.SetGeoPointDelegate(SetInnerPointInput);
            innerPointInput.MouseClickEvent += new MouseClickDelegate(innerPointInputMouseClickEvent);
            innerPointInput.Optional = true;

            heightInput = new LengthInput("Constr.Face.PathExtrude.Height");
            heightInput.SetLengthEvent += new LengthInput.SetLengthDelegate(SetHeight);
            heightInput.GetLengthEvent += new LengthInput.GetLengthDelegate(GetHeight);
            heightInput.CalculateLengthEvent += new LengthInput.CalculateLengthDelegate(CalculateHeight);
            heightInput.ForwardMouseInputTo = geoObjectInput;

            planeInput = new BooleanInput("Constr.Face.PathExtrude.Plane", "Constr.Face.PathExtrude.Plane.Values", planeMode);
            planeInput.SetBooleanEvent += new BooleanInput.SetBooleanDelegate(SetPlaneMode);

            vectorInput = new GeoVectorInput("Constr.Face.PathExtrude.Vector", vector);
            vectorInput.SetGeoVectorEvent += new GeoVectorInput.SetGeoVectorDelegate(SetVector);
            vectorInput.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetVector);
            //            vectorInput.DefaultGeoVector = ConstrDefaults.DefaultExtrudeDirection;
            vectorInput.ForwardMouseInputTo = geoObjectInput;

            heightOffsetInput = new LengthInput("Constr.Face.PathExtrude.HeightOffset");
            heightOffsetInput.SetLengthEvent += new LengthInput.SetLengthDelegate(SetHeightOffset);
            heightOffsetInput.GetLengthEvent += new LengthInput.GetLengthDelegate(GetHeightOffset);
            heightOffsetInput.ForwardMouseInputTo = geoObjectInput;
            heightOffsetInput.Optional = true;

            vectorOffsetInput = new GeoVectorInput("Constr.Face.PathExtrude.VectorOffset", vectorOffset);
            vectorOffsetInput.SetGeoVectorEvent += new GeoVectorInput.SetGeoVectorDelegate(SetVectorOffset);
            vectorOffsetInput.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetVectorOffset);
            vectorOffsetInput.ForwardMouseInputTo = geoObjectInput;
            vectorOffsetInput.Optional = true;

            pipeInput = new CurveInput("Constr.Face.PathExtrude.Pipe");
            pipeInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(pipeInputCurves);
            pipeInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(pipeInputCurveChanged);

            optionalOrg();

            MultipleChoiceInput insertMode = new MultipleChoiceInput("Constr.Solid.SolidInsertMode", "Constr.Solid.SolidInsertMode.Values");
            insertMode.GetChoiceEvent += new MultipleChoiceInput.GetChoiceDelegate(GetInsertMode);
            insertMode.SetChoiceEvent += new CADability.Actions.ConstructAction.MultipleChoiceInput.SetChoiceDelegate(SetInsertMode);

            SeparatorInput separatorHeight = new SeparatorInput("Constr.Face.PathExtrude.SeparatorHeight");
            SeparatorInput separatorVector = new SeparatorInput("Constr.Face.PathExtrude.SeparatorVector");
            SeparatorInput separatorPipe = new SeparatorInput("Constr.Face.PathExtrude.SeparatorPipe");
            SeparatorInput separatorInsert = new SeparatorInput("Constr.Face.PathExtrude.SeparatorInsert");


            base.SetInput(geoObjectInput, innerPointInput, separatorHeight, heightInput, heightOffsetInput, planeInput, separatorVector, vectorInput, vectorOffsetInput, separatorPipe, pipeInput, separatorInsert, insertMode);
            base.ShowAttributes = true;
            base.ShowActiveObject = false;
            base.OnSetAction();
            if (selectedMode) extrudeOrg(); //schonmal berechnen und markieren!
        }


        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.GetID ()"/>
        /// </summary>
        /// <returns></returns>
        public override string GetID()
        { return "Constr.Solid.FaceExtrude"; }

        public override bool AutoRepeat()
        {
            return false; // autorepeating this action seems not pracitcable
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnDone ()"/>
        /// </summary>
        public override void OnDone()
        {
            if (base.ShowActiveObject == true)
            {	// es soll mindestens ein shape eingefügt werden
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    for (int i = 0; i < selectedObjectsList.Count; i++) // geht durch die komplette Liste
                    {
                        bool insert = true;
                        if (shapeList[i] != null) // nur hier was machen!
                        {
                            KeepAsLastStyle(ActiveObject);
                            if (Frame.SolidModellingMode == Make3D.SolidModellingMode.unite)
                            { // vereinigen, wenn möglich
                                base.Frame.Project.GetActiveModel().UniteSolid(shapeList[i] as Solid, false);
                                base.ActiveObject = null;
                            }
                            if (Frame.SolidModellingMode == Make3D.SolidModellingMode.subtract)
                            { // abziehen, wenn möglich, sonst: nichts tun
                                insert = base.Frame.Project.GetActiveModel().RemoveSolid(shapeList[i] as Solid, false);
                                base.ActiveObject = null;
                            }
                            if (vectorOffset.IsNullVector() && insert && Frame.GetBooleanSetting("Construct.3D_Delete2DBase", false))
                            {
                                if (geoObjectOrgList[i] != null) // evtl. Einzelobjekt (Object oder Path) als Original rauslöschen
                                    ownerList[i]?.Remove(geoObjectOrgList[i]);
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

