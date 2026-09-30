
using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections.Generic;

namespace CADability.Actions
{
    internal class Constr3DMakeFace : ConstructAction
    {
        private GeoObjectInput geoObjectInput;
        //       private Boolean selectedMode;
        private GeoPointInput innerPointInput;
        private GeoObjectList selectedObjectsList; // die Liste der selektierten Objekte
        // die folgenden Listen werden dazu synchron gefüllt mit Werten oder "null", bei OnDone zum Einfügen bzw Löschen ausgewertet.
        private List<IGeoObject> geoObjectOrgList; // Liste der beteiligten Originalobjekte, bei OnDone zum Löschen wichtig
        private List<IGeoObject> faceList; // Die Liste der entstandenen Shapes, bei OnDone zum Einfügen gebraucht
        private List<Path> pathCreatedFromModelList; // Liste der evtl. syntetisch erzeugten Pfade zusammenhängender Objekte
        private List<IGeoObjectOwner> ownerList; // List der Owner der Objekte, wichti bei OnDone
        private Block blk; // da werden die Objekte drinn gesammelt
        private bool innerPointUp;


        //public static bool faceTestExtrude(GeoObjectList selectedObjects)
        //{
        //    for (int i = 0; i < selectedObjects.Count; i++)
        //    {   // nur eins muss passen
        //       if ((selectedObjects[i] is Face) || (selectedObjects[i] is Shell) || ((selectedObjects[i] is ICurve) && (selectedObjects[i] as ICurve).IsClosed)) return true;
        //    }
        //    return false;
        //}


        //private Constr3DMakeFace()
        //{ // wird über ":this" von den beiden nächsten Constructoren aufgerufen, initialisiert die Listen
        //    selectedObjectsList = new GeoObjectList();
        //    geoObjectOrgList = new List<IGeoObject>();
        //    faceList = new List<IGeoObject>();
        //    ownerList = new List<IGeoObjectOwner>();
        //    pathCreatedFromModelList = new List<Path>();
        //}

        public Constr3DMakeFace()
        //           : this()
        {
            selectedObjectsList = new GeoObjectList();
            geoObjectOrgList = new List<IGeoObject>();
            faceList = new List<IGeoObject>();
            ownerList = new List<IGeoObjectOwner>();
            pathCreatedFromModelList = new List<Path>();
        }

        public Constr3DMakeFace(Constr3DMakeFace autorepeat)
        //            : this()
        {
        }

        private void ListDefault(int number)
        { // setzt alle Listen auf gleiche Länge, Inhalte "null"
            geoObjectOrgList.Clear();
            faceList.Clear();
            ownerList.Clear();
            pathCreatedFromModelList.Clear();
            for (int i = 0; i < number; i++)
            {
                geoObjectOrgList.Add(null);
                faceList.Add(null);
                ownerList.Add(null);
                pathCreatedFromModelList.Add(null);
            }
        }

        public GeoObjectList makeFaceDo(GeoObjectList selectedObjectsList, IFrame frame)
        {
            this.selectedObjectsList = selectedObjectsList.Clone();
            ListDefault(selectedObjectsList.Count); // setzt alle Listen auf gleiche Länge, Inhalte "null"
            IGeoObject iGeoObjectSel;
            //         IGeoObject iGeoObjectOrg;
            Boolean success = false; // hat er wenigstens eins gefunden
            GeoObjectList outList = new GeoObjectList(); // sammelt die gelungenen faces
            for (int i = 0; i < selectedObjectsList.Count; i++) // läuft über alle selektierten Objekte. Evtl nur eins bei Konstruktion
            {
                IGeoObject iGeoObjectTemp = null; // lokaler Merker
                iGeoObjectSel = selectedObjectsList[i]; // zur Vereinfachung
                geoObjectOrgList[i] = iGeoObjectSel; // zum Weglöschen des Originals in onDone
                ownerList[i] = iGeoObjectSel.Owner; // owner merken für löschen 
                pathCreatedFromModelList[i] = null;
                faceList[i] = null;
                //            Boolean VectorOrHeightTemp = VectorOrHeight;
                if (!(iGeoObjectSel is Face) && !(iGeoObjectSel is Shell))
                {
                    if ((iGeoObjectSel is ICurve))
                    {
                        Path p = null;
                        if (selectedObjectsList.Count == 1 && !(iGeoObjectSel as ICurve).IsClosed)
                            p = CADability.GeoObject.Path.CreateFromModel(iGeoObjectSel as ICurve, frame.ActiveView.Model, frame.ActiveView.Projection, true);
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
                            if (p.IsClosed)
                            {
                                geoObjectOrgList[i] = null; // zum nicht Weglöschen des Originals in onDone
                                pathCreatedFromModelList[i] = p; // kopie merken für onDone
                                p = p.Clone() as Path;
                                p.Flatten();
                            }
                        }
                        if (p.IsClosed)
                        { // jetzt face machen
                            iGeoObjectTemp = Make3D.MakeFace(p, frame.Project);
                        }
                    }
                }
                if (iGeoObjectTemp != null)
                { // also was geeignetes dabei
                    iGeoObjectTemp.CopyAttributes(iGeoObjectSel as IGeoObject);
                    faceList[i] = iGeoObjectTemp; // fertiger Körper in shapeList
                    success = true;
                }
            } // for-Schleife über die Objekte der Liste
            if (success)
            {
                using (frame.Project.Undo.UndoFrame)
                {
                    for (int i = 0; i < selectedObjectsList.Count; i++) // geht durch die komplette Liste
                    {
                        if (faceList[i] != null) // nur hier was machen!
                        {
                            outList.Add(faceList[i]); // die Guten sammeln
                            frame.Project.GetActiveModel().Add(faceList[i]); // einfügen
                            if (frame.GetBooleanSetting("Construct.3D_Delete2DBase", false))
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
                }
                return outList;
            }
            return null;
        }


        private bool makeFaceOrg()
        {
            if (selectedObjectsList == null) return false;
            IGeoObject iGeoObjectSel;
            //         IGeoObject iGeoObjectOrg;
            blk = Block.Construct(); // zur Darstellung
            if (base.ActiveObject != null) blk.CopyAttributes(base.ActiveObject);
            // der block wird das neue aktive Objekt, er muss die Attribute tragen, weil sie später
            // wieder von ihm verlangt werden
            Boolean success = false; // hat er wenigstens eins gefunden
            for (int i = 0; i < selectedObjectsList.Count; i++) // läuft über alle selektierten Objekte. Evtl nur eins bei Konstruktion
            {
                IGeoObject iGeoObjectTemp = null; // lokaler Merker
                iGeoObjectSel = selectedObjectsList[i]; // zur Vereinfachung
                geoObjectOrgList[i] = iGeoObjectSel; // zum Weglöschen des Originals in onDone
                ownerList[i] = iGeoObjectSel.Owner; // owner merken für löschen 
                pathCreatedFromModelList[i] = null;
                faceList[i] = null;
                //            Boolean VectorOrHeightTemp = VectorOrHeight;
                if (!(iGeoObjectSel is Face) && !(iGeoObjectSel is Shell))
                {
                    if ((iGeoObjectSel is ICurve))
                    {
                        Path p = null;
                        if (selectedObjectsList.Count == 1)
                            p = CADability.GeoObject.Path.CreateFromModel(iGeoObjectSel as ICurve, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
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
                            if (p.IsClosed)
                            {
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
                else if (iGeoObjectSel is Face)  // nur kopieren
                    iGeoObjectTemp = iGeoObjectSel.Clone();
                if (iGeoObjectTemp != null)
                { // also was geeignetes dabei
                  //                    iGeoObjectTemp.CopyAttributes(iGeoObjectSel as IGeoObject);
                    iGeoObjectTemp.CopyAttributes(blk);
                    // die Attribute müssen vom Block übernommen werden
                    faceList[i] = iGeoObjectTemp; // fertiger Körper in shapeList
                    blk.Add(iGeoObjectTemp); // zum Darstellen
                    base.FeedBack.AddSelected(iGeoObjectTemp); // zum Markieren des Ursprungsobjekts
                    success = true;
                }
            } // for-Schleife über die Objekte der Liste
            if (success)
            {
                base.ActiveObject = blk; // darstellen
                base.ShowActiveObject = true;
                return true;
            }
            else
            {
                base.ShowActiveObject = false;
                base.FeedBack.ClearSelected();
                return false;
            }

        }


        bool geoObjectInputFace(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {   // ... nur die sinnvollen Kurven verwenden
            // TODO: evtl Mehrfachanwahl ermöglichen
            if (up)
                if (TheGeoObjects.Length == 0) sender.SetGeoObject(TheGeoObjects, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetGeoObject(TheGeoObjects, TheGeoObjects[0]);
            if (TheGeoObjects.Length > 0)
            {	// er hat was gewählt
                selectedObjectsList.Clear();
                base.FeedBack.ClearSelected();
                selectedObjectsList.Add(TheGeoObjects[0]); // das eine in die Liste
                ListDefault(1); // die Listen löschen und mit "1" vorbesetzen
                if (makeFaceOrg()) return true;
            }
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
            makeFaceOrg();
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
                        if (makeFaceOrg()) OnDone();
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


        //protected override bool MayFinish()
        //{
        //    return false;
        //}

        public override void OnSetAction()
        {
            base.ActiveObject = Face.Construct();
            base.TitleId = "Constr.Face.FromObject";

            geoObjectInput = new GeoObjectInput("Constr.Face.FromObject.Object");
            geoObjectInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(geoObjectInputFace);
            geoObjectInput.GeoObjectSelectionChangedEvent += new GeoObjectInput.GeoObjectSelectionChangedDelegate(geoObjectInputFaceChanged);

            innerPointInput = new GeoPointInput("Constr.Face.FromObject.InnerPoint");
            innerPointInput.SetGeoPointEvent += new GeoPointInput.SetGeoPointDelegate(SetInnerPointInput);
            innerPointInput.MouseClickEvent += new MouseClickDelegate(innerPointInputMouseClickEvent);
            innerPointInput.Optional = true;


            base.SetInput(geoObjectInput, innerPointInput);
            base.ShowAttributes = true;
            base.ShowActiveObject = false;
            base.OnSetAction();
        }

        public override string GetID()
        { return "Constr.Face.FromObject"; }

        public override void OnDone()
        {
            if (base.ShowActiveObject == true)
            {	// es soll mindestens ein shape eingefügt werden
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    for (int i = 0; i < selectedObjectsList.Count; i++) // geht durch die komplette Liste
                    {
                        if (faceList[i] != null) // nur hier was machen!
                        {
                            if (Frame.GetBooleanSetting("Construct.3D_Delete2DBase", false))
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


