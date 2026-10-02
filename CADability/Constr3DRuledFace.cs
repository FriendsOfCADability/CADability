using CADability.GeoObject;
using System;
using System.Collections;


namespace CADability.Actions
{
    internal class Constr3DRuledFace : ConstructAction
    {
        private GeoObjectInput geoObjectInput1;
        private GeoObjectInput geoObjectInput2;
        //        private GeoPoint objectPoint;
        private IGeoObject iGeoObjectOrg1;
        private IGeoObject iGeoObjectOrg2;
        private IGeoObject attrObject;
        private IGeoObjectOwner owner1;
        private IGeoObjectOwner owner2;
        private Path pathCreatedFromModel1;
        private Path pathCreatedFromModel2;
        private Path p1;
        private Path p2;
        private int selectedMode;


        static public void ruledFaceDo(GeoObjectList geoObjectList, IFrame frame)
        { // im Falle zweier selektierter Pfade: alles machen ohne Aktion!
            bool pathCreatedFromModel1 = false;
            bool pathCreatedFromModel2 = false;
            // erstmal versuchen, Pfade draus zu machen
            Path p1;
            if (geoObjectList[0] is Path)
            { // schon fertig
                p1 = geoObjectList[0].Clone() as Path;
            }
            else
            {  // Pfad aus Einzelobjekt machen:
                p1 = findPath(geoObjectList[0], frame.ActiveView.Model, out pathCreatedFromModel1);
            }
            Path p2;
            if (geoObjectList[1] is Path)
            { // schon fertig
                p2 = geoObjectList[1].Clone() as Path;
            }
            else
            {  // Pfad aus Einzelobjekt machen:
                p2 = findPath(geoObjectList[1], frame.ActiveView.Model, out pathCreatedFromModel2);
            }
            IGeoObject ss = null;
            if ((p1 != null) && (p2 != null))
                ss = Make3D.MakeRuledShell(p1, p2, frame.Project);
            if (ss != null)
            {
                using (frame.Project.Undo.UndoFrame)
                {
                    ss.CopyAttributes(geoObjectList[0]);
                    frame.Project.GetActiveModel().Add(ss); // einfügen
                    if (!pathCreatedFromModel2 && frame.GetBooleanSetting("Construct.3D_Delete2DBase", false)) geoObjectList[1].Owner.Remove(geoObjectList[1] as IGeoObject); // Original weg
                    if (pathCreatedFromModel2)
                    { // die Einzelelemente des CreateFromModel identifizieren
                        for (int i = 0; i < p2.Count; ++i)
                        {
                            IGeoObject obj = null;
                            if (p2.Curve(i).UserData.ContainsData("CADability.Path.Original"))
                                obj = p2.Curve(i).UserData.GetData("CADability.Path.Original") as IGeoObject;
                            if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
                        }
                    }
                    if (!pathCreatedFromModel1 && frame.GetBooleanSetting("Construct.3D_Delete2DBase", false)) geoObjectList[0].Owner.Remove(geoObjectList[0] as IGeoObject);
                    if (pathCreatedFromModel1)
                    { // die Einzelelemente des CreateFromModel identifizieren
                        for (int i = 0; i < p1.Count; ++i)
                        {
                            IGeoObject obj = null;
                            if (p1.Curve(i).UserData.ContainsData("CADability.Path.Original"))
                                obj = p1.Curve(i).UserData.GetData("CADability.Path.Original") as IGeoObject;
                            if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
                        }
                    }

                }
            }
        }

        static public bool ruledFaceTest(GeoObjectList geoObjectList, Model model)
        { // Tested für SelectedObjectsAction, ob Eintrag enabled oder nicht
            bool temp;
            if ((geoObjectList.Count == 1) && (geoObjectList[0] is ICurve) && (findPath(geoObjectList[0], model, out temp) != null))
            { // ein Element, Pfad  
                return true;
            }
            if ((geoObjectList.Count == 2) && (geoObjectList[0] is ICurve) && (geoObjectList[1] is ICurve))
            { // zwei Elemente, auf Pfade testen
                Path p1 = (findPath(geoObjectList[0], model, out temp));
                Path p2 = (findPath(geoObjectList[1], model, out temp));
                if ((p1 != null) && (p2 != null))
                {
                    if ((p1.IsClosed && p2.IsClosed) || (!p1.IsClosed && !p2.IsClosed)) // OPenCascade will nur geschlossene oder nur offene
                    {
                        if (((p1 as ICurve).GetPlanarState() == PlanarState.Planar) && ((p2 as ICurve).GetPlanarState() == PlanarState.Planar)) // jeder Pfad in sich in einer Ebene
                        { // beide eben, jetzt nur noch: unterschiedliche Ebenen:
                            if (!Precision.IsEqual(p1.GetPlane(), p2.GetPlane()))
                                return true;
                        }
                        if ((((p1 as ICurve).GetPlanarState() == PlanarState.Planar) || (p1 as ICurve).GetPlanarState() == PlanarState.UnderDetermined) && (((p2 as ICurve).GetPlanarState() == PlanarState.Planar) || (p2 as ICurve).GetPlanarState() == PlanarState.UnderDetermined)) // jeder Pfad in sich in einer Ebene
                        { // beide eben, jetzt nur noch: unterschiedliche Ebenen:
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        public Constr3DRuledFace(GeoObjectList geoObjectList, IFrame frame)
        { // constructor für SelectedObjectsAction, ein selektierter Pfad
            bool pathCreatedFromModel;
            if (geoObjectList == null)
            { selectedMode = 0; }
            else
            {
                //if (geoObjectList.Count == 2)
                //    {  ruledSolidDo(geoObjectList, frame); }
                //else
                selectedMode = 1;
                p1 = findPath(geoObjectList[0], frame.ActiveView.Model, out pathCreatedFromModel);
                if (p1 != null)
                {
                    attrObject = geoObjectList[0]; // merker, um das passende Attribut zu bekommen
                    iGeoObjectOrg1 = geoObjectList[0]; // zum Weglöschen des Originals in onDone
                    owner1 = geoObjectList[0].Owner; // owner merken für löschen  
                    if (pathCreatedFromModel)
                    {
                        iGeoObjectOrg1 = null; // zum nicht Weglöschen des Originals in onDone
                        pathCreatedFromModel1 = p1; // kopie merken für onDone
                    }
                }
            }
        }

        public Constr3DRuledFace(Constr3DRuledFace autorepeat)
        {
            if (autorepeat.selectedMode > 0) throw new ApplicationException();
            selectedMode = 0;
        }


        private static Path findPath(IGeoObject iGeoObjectSel, Model model, out bool createdFromModel)
        {  // macht geeigneten Pfad:
            createdFromModel = false;
            Path p = null;
            if ((iGeoObjectSel is Face) || (iGeoObjectSel is Shell))
            { // TODO: Path vom Face oder Shell
                //                iGeoObjectTemp = iGeoObjectSel.Clone();
            }
            else
            {
                if (iGeoObjectSel is ICurve)
                {   // erstmal verbundene Objecte suchen
                    p = CADability.GeoObject.Path.CreateFromModel(iGeoObjectSel as ICurve, model, true);
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
                        p.Flatten();
                        createdFromModel = true;
                    }
                    if (p != null)
                    {
                        return p;
                    }
                }
            }
            return null;
        }


        private bool ruledFaceOrg()
        {
            if (!(Precision.IsEqual(p1.StartPoint, p2.StartPoint) && (Precision.IsEqual(p1.EndPoint, p2.EndPoint)))) // 1. Näherung, dass sie nicht gleich sind!
            {
                if ((p1.IsClosed && p2.IsClosed) || (!p1.IsClosed && !p2.IsClosed)) // OPenCascade will nur geschlossene oder nur offene
                {
                    if ((((p1 as ICurve).GetPlanarState() == PlanarState.Planar) || (p1 as ICurve).GetPlanarState() == PlanarState.UnderDetermined) && (((p2 as ICurve).GetPlanarState() == PlanarState.Planar) || (p2 as ICurve).GetPlanarState() == PlanarState.UnderDetermined))
                    { // also nur Ebene Pfade mit unterschiedlichen Ebenen oder unbestimmt, z.B. Linien:
                        if (((p1 as ICurve).GetPlanarState() == PlanarState.Planar) && ((p2 as ICurve).GetPlanarState() == PlanarState.Planar) && Precision.IsEqual((p1 as ICurve).GetPlane(), (p2 as ICurve).GetPlane())) return false;
                        // also was geeignetes dabei
                        IGeoObject ss = Make3D.MakeRuledShell(p1, p2, Frame.Project);
                        if (ss != null)
                        {
                            if (base.ActiveObject != null) ss.CopyAttributes(base.ActiveObject);
                            // der block wird das neue aktive Objekt, er muss die Attribute tragen, weil sie später
                            // wieder von ihm verlangt werden
                            //                        ss.CopyAttributes(attrObject);
                            base.ActiveObject = ss; // darstellen
                            base.ShowActiveObject = true;
                            return true;
                        }
                    }
                }
            }
            base.ShowActiveObject = false;
            return false;
        }



        bool geoObjectInputPath1(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {   // ... nur die sinnvollen Kurven verwenden
            //            objectPoint = base.CurrentMousePosition;
            bool createdFromModel;
            if (base.Frame.ActiveView.Model == null) return false;
            ArrayList usableObjects = new ArrayList();
            for (int i = 0; i < TheGeoObjects.Length; ++i)
            {
                p1 = findPath(TheGeoObjects[i], base.Frame.ActiveView.Model, out createdFromModel);
                if (p1 != null)
                {
                    if (p2 != null) // anderer Pfad schon bestimmt
                    {
                        if (ruledFaceOrg()) usableObjects.Add(TheGeoObjects[i]);
                    }
                    else usableObjects.Add(TheGeoObjects[i]);
                }
            }
            // ...hier wird der ursprüngliche Parameter überschrieben. Hat ja keine Auswirkung nach außen.
            TheGeoObjects = (IGeoObject[])usableObjects.ToArray(typeof(IGeoObject));
            if (up)
                if (TheGeoObjects.Length == 0) sender.SetGeoObject(TheGeoObjects, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetGeoObject(TheGeoObjects, TheGeoObjects[0]);
            if (TheGeoObjects.Length > 0)
            {	// er hat was gewählt
                p1 = findPath(TheGeoObjects[0], base.Frame.ActiveView.Model, out createdFromModel);
                if (p1 != null)
                {
                    attrObject = TheGeoObjects[0]; // merker, um das passende Attribut zu bekommen
                    iGeoObjectOrg1 = TheGeoObjects[0]; // zum Weglöschen des Originals in onDone
                    owner1 = TheGeoObjects[0].Owner; // owner merken für löschen  
                    if (createdFromModel)
                    {
                        iGeoObjectOrg1 = null; // zum nicht Weglöschen des Originals in onDone
                        pathCreatedFromModel1 = p1; // kopie merken für onDone
                    }
                    if (p2 != null) // anderer Pfad schon bestimmt
                    {
                        return ruledFaceOrg();
                    }
                    else return true;
                }
            }
            p1 = null;
            base.ShowActiveObject = false;
            return false;
        }

        void geoObjectInputPath1Changed(ConstructAction.GeoObjectInput sender, IGeoObject SelectedGeoObject)
        {
            bool createdFromModel;
            p1 = findPath(SelectedGeoObject, base.Frame.ActiveView.Model, out createdFromModel);
            if (p1 != null)
            {
                attrObject = SelectedGeoObject; // merker, um das passende Attribut zu bekommen
                iGeoObjectOrg1 = SelectedGeoObject; // zum Weglöschen des Originals in onDone
                owner1 = SelectedGeoObject.Owner; // owner merken für löschen  
                if (createdFromModel)
                {
                    iGeoObjectOrg1 = null; // zum nicht Weglöschen des Originals in onDone
                    pathCreatedFromModel1 = p1; // kopie merken für onDone
                }
                if (p2 != null)  // anderer Pfad schon bestimmt
                {
                    ruledFaceOrg();
                }
            }
        }

        bool geoObjectInputPath2(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {   // ... nur die sinnvollen Kurven verwenden
            //            objectPoint = base.CurrentMousePosition;
            bool createdFromModel;
            ArrayList usableObjects = new ArrayList();
            for (int i = 0; i < TheGeoObjects.Length; ++i)
            {
                p2 = findPath(TheGeoObjects[i], base.Frame.ActiveView.Model, out createdFromModel);
                if (p2 != null)
                {
                    //                    if (geoObjectInput1.Fixed)
                    if (p1 != null) // anderer Pfad schon bestimmt
                    {
                        if (ruledFaceOrg()) usableObjects.Add(TheGeoObjects[i]);
                    }
                    else usableObjects.Add(TheGeoObjects[i]);
                }
            }
            // ...hier wird der ursprüngliche Parameter überschrieben. Hat ja keine Auswirkung nach außen.
            TheGeoObjects = (IGeoObject[])usableObjects.ToArray(typeof(IGeoObject));
            if (up)
                if (TheGeoObjects.Length == 0) sender.SetGeoObject(TheGeoObjects, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetGeoObject(TheGeoObjects, TheGeoObjects[0]);
            if (TheGeoObjects.Length > 0)
            {	// er hat was gewählt
                p2 = findPath(TheGeoObjects[0], base.Frame.ActiveView.Model, out createdFromModel);
                if (p2 != null)
                {
                    iGeoObjectOrg2 = TheGeoObjects[0]; // zum Weglöschen des Originals in onDone
                    owner2 = TheGeoObjects[0].Owner; // owner merken für löschen  
                    if (createdFromModel)
                    {
                        iGeoObjectOrg2 = null; // zum nicht Weglöschen des Originals in onDone
                        pathCreatedFromModel2 = p2; // kopie merken für onDone
                    }
                    if (p1 != null) // anderer Pfad schon bestimmt
                    {
                        return ruledFaceOrg();
                    }
                    else return true;
                }
            }
            p2 = null;
            base.ShowActiveObject = false;
            return false;
        }

        void geoObjectInputPath2Changed(ConstructAction.GeoObjectInput sender, IGeoObject SelectedGeoObject)
        {
            //            iGeoObjectSel2 = SelectedGeoObject;
            bool createdFromModel;
            p2 = findPath(SelectedGeoObject, base.Frame.ActiveView.Model, out createdFromModel);
            if (p2 != null)
            {
                iGeoObjectOrg2 = SelectedGeoObject; // zum Weglöschen des Originals in onDone
                owner2 = SelectedGeoObject.Owner; // owner merken für löschen  
                if (createdFromModel)
                {
                    iGeoObjectOrg2 = null; // zum nicht Weglöschen des Originals in onDone
                    pathCreatedFromModel2 = p2; // kopie merken für onDone
                }
                if (p1 != null) // anderer Pfad schon bestimmt
                {
                    ruledFaceOrg();
                }
            }
        }

        public override void OnSetAction()
        {
            base.ActiveObject = Face.Construct();
            base.TitleId = "Constr.Face.RuledFace";

            geoObjectInput1 = null;
            geoObjectInput1 = new GeoObjectInput("Constr.Face.RuledFace.Path1");
            geoObjectInput1.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(geoObjectInputPath1);
            geoObjectInput1.GeoObjectSelectionChangedEvent += new GeoObjectInput.GeoObjectSelectionChangedDelegate(geoObjectInputPath1Changed);
            if (selectedMode == 1) geoObjectInput1.Fixed = true;

            geoObjectInput2 = new GeoObjectInput("Constr.Face.RuledFace.Path2");
            geoObjectInput2.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(geoObjectInputPath2);
            geoObjectInput2.GeoObjectSelectionChangedEvent += new GeoObjectInput.GeoObjectSelectionChangedDelegate(geoObjectInputPath2Changed);

            base.SetInput(geoObjectInput1, geoObjectInput2);
            base.ShowAttributes = true;
            base.OnSetAction();
        }


        public override string GetID()
        { return "Constr.Face.RuledFace"; }

        public override void OnDone()
        {
            if (base.ShowActiveObject == true)
            {	// es soll ein shape eingefügt werden
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    if (Frame.GetBooleanSetting("Construct.3D_Delete2DBase", false)) // beim Abziehen kann sein, dass nichts passieren soll
                    {
                        if (iGeoObjectOrg1 != null) // evtl. Einzelobjekt (Object oder Path) als Original rauslöschen
                            owner1.Remove(iGeoObjectOrg1 as IGeoObject);
                        else
                        { // die Einzelelemente des CreateFromModel identifizieren
                            for (int i = 0; i < pathCreatedFromModel1.Count; ++i)
                            {
                                IGeoObject obj = null;
                                if (pathCreatedFromModel1.Curve(i).UserData.ContainsData("CADability.Path.Original"))
                                    obj = pathCreatedFromModel1.Curve(i).UserData.GetData("CADability.Path.Original") as IGeoObject;
                                if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
                            }
                        }
                        if (iGeoObjectOrg2 != null) // evtl. Einzelobjekt (Object oder Path) als Original rauslöschen
                            owner2.Remove(iGeoObjectOrg2 as IGeoObject);
                        else
                        { // die Einzelelemente des CreateFromModel identifizieren
                            for (int i = 0; i < pathCreatedFromModel2.Count; ++i)
                            {
                                IGeoObject obj = null;
                                if (pathCreatedFromModel2.Curve(i).UserData.ContainsData("CADability.Path.Original"))
                                    obj = pathCreatedFromModel2.Curve(i).UserData.GetData("CADability.Path.Original") as IGeoObject;
                                if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
                            }
                        }
                    }
                    base.OnDone();
                }
            }
            base.OnDone();
        }

    }
}



