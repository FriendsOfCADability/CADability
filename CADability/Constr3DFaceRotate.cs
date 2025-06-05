using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CADability.Actions
{
    public class Constr3DFaceRotate : ConstructAction
    {
        private GeoObjectInput geoObjectInput;
        private GeoPointInput innerPointInput;
        private CurveInput rotateLineInput;
        private GeoVectorInput axisVectorInput;
        private GeoPointInput axisPointInput;
        private GeoPoint objectPoint;
        //        private IGeoObject iGeoObjectSel;
        //        private IGeoObject iGeoObjectOrg;
        //        private IGeoObjectOwner owner;
        //        private Path pathCreatedFromModel;
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
        private bool innerPointUp;

        public static bool faceTestRotate(GeoObjectList selectedObjects)
        {
            for (int i = 0; i < selectedObjects.Count; i++)
            {   // nur eins muss passen
                if ((selectedObjects[i] is Face) || (selectedObjects[i] is Shell) || ((selectedObjects[i] is ICurve) && (selectedObjects[i] as ICurve).IsClosed)
                     || ((selectedObjects[i] is Ellipse) && (selectedObjects[i] as Ellipse).IsArc) || (selectedObjects[i] is Path) || (selectedObjects[i] is Polyline)) return true;
            }
            return false;
        }



        private Constr3DFaceRotate()
        { // wird über ":this" von den beiden nächsten Constructoren aufgerufen, initialisiert die Listen
            selectedObjectsList = new GeoObjectList();
            geoObjectOrgList = new List<IGeoObject>();
            shapeList = new List<IGeoObject>();
            ownerList = new List<IGeoObjectOwner>();
            pathCreatedFromModelList = new List<Path>();
        }


        public Constr3DFaceRotate(GeoObjectList selectedObjectsList) : this()
        {
            selectedMode = (selectedObjectsList != null);
            if (selectedMode)
            {
                this.selectedObjectsList = selectedObjectsList.Clone();
                ListDefault(selectedObjectsList.Count); // setzt alle Listen auf gleiche Länge, Inhalte "null"
            };
        }

        public Constr3DFaceRotate(Constr3DFaceRotate autorepeat) : this()
        {
            // if (autorepeat.selectedMode) throw new ApplicationException();
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


        void updateOptional()
        {
            axisPointInput.Optional = !axisOrLine;
            axisVectorInput.Optional = !axisOrLine;
            rotateLineInput.Optional = axisOrLine;
        }


        private bool rotateOrg(bool openDraw)
        {
            if (selectedObjectsList == null || selectedObjectsList.Count == 0) return false;
            IGeoObject selectedObject = null;
            blk = Block.Construct(); // as feedback object
            if (base.ActiveObject != null) blk.CopyAttributes(base.ActiveObject);
            // der block wird das neue aktive Objekt, er muss die Attribute tragen, weil sie später
            // wieder von ihm verlangt werden
            Boolean success = false;
            GeoPoint axisPointSav = axisPoint;
            GeoVector axisVectorSav = axisVector;
            updateOptional();
            if (selectedObjectsList.Count > 1)
            {   // maybe there are several curves in the list, then we try to make a path
                // and use only this path. No mixture of faces and paths possible
                List<ICurve> crvs = new List<ICurve>();
                for (int i = 0; i < selectedObjectsList.Count; i++)
                {
                    if (selectedObjectsList[i] is ICurve crv) crvs.Add(crv);
                }
                if (crvs.Count > 1)
                {
                    Path path = Path.FromSegments(crvs, false);
                    if (path != null)
                    {
                        selectedObjectsList.Clear();
                        selectedObjectsList.Add(path);
                    }
                }
            }
            for (int i = 0; i < selectedObjectsList.Count; i++)
            {
                IGeoObject objectToRotate = null; // lokaler Merker
                geoObjectOrgList[i] = selectedObject; // zum Weglöschen des Originals in onDone
                selectedObject = selectedObjectsList[i];
                ownerList[i] = selectedObject.Owner; // owner merken für löschen 
                pathCreatedFromModelList[i] = null;
                shapeList[i] = null;

                if ((selectedObject is Face) || (selectedObject is Shell))
                { // nur kopieren
                    objectToRotate = selectedObject.Clone();
                }
                else
                {
                    if (selectedObject is ICurve selCurve)
                    {
                        Path p = null;
                        if (!selCurve.IsClosed)
                        {
                            // trying to rotate an open curve: if the axis and the curve have a commonn plane close a path with the perpendicular foot points and axis segment
                            Line axisLine = Line.TwoPoints(axisPoint, axisPoint + axisVector);
                            if (Curves.GetCommonPlane(new ICurve[] { selCurve, axisLine }, out Plane commonPlane))
                            {
                                Line l1 = Line.TwoPoints(selCurve.EndPoint, Geometry.DropPL(selCurve.EndPoint, axisPoint, axisVector));
                                Line l2 = Line.TwoPoints(Geometry.DropPL(selCurve.StartPoint, axisPoint, axisVector), selCurve.StartPoint);
                                Line l3 = Line.TwoPoints(l1.EndPoint, l2.StartPoint);
                                List<ICurve> closedPath = new List<ICurve>();
                                closedPath.Add(selCurve);
                                if (l1.Length > Precision.eps) closedPath.Add(l1);
                                if (l3.Length > Precision.eps) closedPath.Add(l3);
                                if (l2.Length > Precision.eps) closedPath.Add(l2);
                                p = Path.FromSegments(closedPath, true); // should always work
                                p.Flatten();
                            }
                        }
                        else
                        {
                            p = Path.FromSegments(new ICurve[] { selCurve }, true);
                        }
                        if (p == null) continue; // no path constructed
                        if ((p as ICurve).GetSelfIntersections().Length > 0) continue; // not possible
                        if (!p.IsClosed) continue;
                        // make a face from the closed pathe
                        objectToRotate = Make3D.MakeFace(p, Frame.Project, true);
                    }
                }
                if (objectToRotate != null)
                { // also was geeignetes dabei
                  //if (angleOffsetRotation != 0.0)
                  //{
                  //    ModOp m = ModOp.Rotate(axisPoint, axisVector, new SweepAngle(angleOffsetRotation));
                  //    iGeoObjectTemp.Modify(m);
                  //}
                    double sw = angleRotation;
                    if (sw == 0.0) sw = Math.PI * 2.0;
                    // IGeoObject shape = Make3D.MakeRevolution(iGeoObjectTemp, axisPoint, axisVector, sw, Frame.Project);
                    IGeoObject shape = Make3D.Rotate(objectToRotate, new Axis(axisPoint, axisVector), sw, angleOffsetRotation, Frame.Project);
                    if (shape != null)
                    {
                        shape.CopyAttributes(blk);
                        shapeList[i] = shape; // fertiger Körper in shapeList
                        blk.Add(shape); // resulting object
                        base.FeedBack.AddSelected(objectToRotate); // zum Markieren des Ursprungsobjekts
                        success = true;
                    }
                }
            }
            axisPoint = axisPointSav; // im Fall nicht geschlossener Pfad und nichts bestimmt, werden diese Parameter oben verstellt
            axisVector = axisVectorSav;
            if (success)
            {
                base.ActiveObject = blk; // darstellen
                base.ShowActiveObject = true;
                return true;
            }
            else
            {
                updateOptional();
                base.ShowActiveObject = false;
                base.FeedBack.ClearSelected();
                return false;
            }
        }


        bool geoObjectInputFace(ConstructAction.GeoObjectInput sender, IGeoObject[] TheGeoObjects, bool up)
        {   // ... nur die sinnvollen Kurven verwenden
            objectPoint = base.CurrentMousePosition;
            if (up)
                if (TheGeoObjects.Length == 0) sender.SetGeoObject(TheGeoObjects, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetGeoObject(TheGeoObjects, TheGeoObjects[0]);
            if (TheGeoObjects.Length > 0)
            {   // er hat was gewählt
                selectedObjectsList.Clear();
                base.FeedBack.ClearSelected();
                selectedObjectsList.Add(TheGeoObjects[0]); // das eine in die Liste
                ListDefault(1); // die Listen löschen und mit "1" vorbesetzen
                if (rotateOrg(false)) return true;
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
            rotateOrg(false);
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
                        if (rotateOrg(false))
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


        private bool RotateAxis(CurveInput sender, ICurve[] Curves, bool up)
        {   // only use curves, which can define an axis
            List<ICurve> usableCurves = new List<ICurve>();
            Curves = Curves.Where(c => c is Line || c is Ellipse).ToArray();
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // will be displayed in property grid
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {   // einfach die erste Kurve nehmen
                if (Curves[0] is Line line)
                {
                    axisPoint = line.StartPoint;
                    axisVector = line.StartDirection;
                    axisOrLine = false;
                } else if (Curves[0] is Ellipse elli)
                {
                    axisPoint = elli.Center;
                    axisVector = elli.Plane.Normal;
                    axisOrLine = false;
                }
                updateOptional();
                return rotateOrg(true);
            }
            base.FeedBack.ClearSelected();
            base.ShowActiveObject = false;
            return false;
        }

        private void RotateAxisChanged(CurveInput sender, ICurve SelectedCurve)
        {
            if (SelectedCurve is Line line)
            {
                axisPoint = SelectedCurve.StartPoint;
                axisVector = SelectedCurve.StartDirection;
            }
            else if (SelectedCurve is Ellipse elli)
            {
                axisPoint = elli.Center;
                axisVector = elli.Plane.Normal;
            }
            else return;
            rotateOrg(false);
        }


        bool SetAxisVector(GeoVector vec)
        {
            if (Precision.IsNullVector(vec)) return false;
            axisVector = vec;
            axisOrLine = true;
            updateOptional();
            return rotateOrg(true);
        }

        GeoVector GetAxisVector()
        {
            return axisVector;
        }

        void SetAxisPoint(GeoPoint p)
        {
            axisPoint = p;
            axisOrLine = true;
            updateOptional();
            rotateOrg(true);
        }

        GeoPoint GetAxisPoint()
        {
            return axisPoint;
        }


        bool SetAngleInput(Angle angle)
        {
            //            if (angle == 0.0) return false; sonst geht 360° nicht, da 360° als 0.0 ankommt
            angleRotation = angle;
            rotateOrg(false);
            return true;
        }

        Angle GetAngleInput()
        {
            return angleRotation;
        }

        bool SetAngleOffsetInput(Angle angle)
        {
            angleOffsetRotation = angle;
            rotateOrg(false);
            return true;
        }

        Angle GetAngleOffsetInput()
        {
            return angleOffsetRotation;
        }

        private void SetInsertMode(int val)
        {
            Frame.SolidModellingMode = (Make3D.SolidModellingMode)val;
        }

        int GetInsertMode()
        {
            return (int)(Frame.SolidModellingMode);
        }


        public override void OnSetAction()
        {
            base.ActiveObject = Face.Construct();
            if (axisVector.IsNullVector()) axisVector = GeoVector.YAxis;
            if (angleRotation == 0.0) angleRotation = Math.PI;

            base.TitleId = "Constr.Solid.FaceRotate";

            geoObjectInput = new GeoObjectInput("Constr.Solid.FaceRotate.Path");
            geoObjectInput.MouseOverGeoObjectsEvent += new GeoObjectInput.MouseOverGeoObjectsDelegate(geoObjectInputFace);
            geoObjectInput.GeoObjectSelectionChangedEvent += new GeoObjectInput.GeoObjectSelectionChangedDelegate(geoObjectInputFaceChanged);
            if (selectedMode) geoObjectInput.Fixed = true;

            innerPointInput = new GeoPointInput("Constr.Solid.FaceRotate.InnerPoint");
            innerPointInput.SetGeoPointEvent += new GeoPointInput.SetGeoPointDelegate(SetInnerPointInput);
            innerPointInput.MouseClickEvent += new MouseClickDelegate(innerPointInputMouseClickEvent);
            innerPointInput.Optional = true;


            rotateLineInput = new CurveInput("Constr.Face.PathRotate.AxisLine");
            rotateLineInput.Decomposed = true; // nur Einzelelemente, auch bei Polyline und Pfad
            rotateLineInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(RotateAxis);
            rotateLineInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(RotateAxisChanged);

            axisPointInput = new GeoPointInput("Constr.Face.PathRotate.AxisPoint", axisPoint);
            axisPointInput.SetGeoPointEvent += new GeoPointInput.SetGeoPointDelegate(SetAxisPoint);
            axisPointInput.GetGeoPointEvent += new GeoPointInput.GetGeoPointDelegate(GetAxisPoint);
            axisPointInput.ForwardMouseInputTo = geoObjectInput;
            axisPointInput.DefinesBasePoint = true;

            axisVectorInput = new GeoVectorInput("Constr.Face.PathRotate.AxisVector", axisVector);
            axisVectorInput.SetGeoVectorEvent += new GeoVectorInput.SetGeoVectorDelegate(SetAxisVector);
            axisVectorInput.GetGeoVectorEvent += new GeoVectorInput.GetGeoVectorDelegate(GetAxisVector);
            //            vectorInput.DefaultGeoVector = ConstrDefaults.DefaultExtrudeDirection;
            axisVectorInput.ForwardMouseInputTo = geoObjectInput;
            updateOptional();

            AngleInput angleInput = new AngleInput("Constr.Face.PathRotate.Angle", angleRotation);
            angleInput.SetAngleEvent += new AngleInput.SetAngleDelegate(SetAngleInput);
            angleInput.GetAngleEvent += new AngleInput.GetAngleDelegate(GetAngleInput);

            AngleInput angleOffsetInput = new AngleInput("Constr.Face.PathRotate.AngleOffset", angleOffsetRotation);
            angleOffsetInput.SetAngleEvent += new AngleInput.SetAngleDelegate(SetAngleOffsetInput);
            angleOffsetInput.GetAngleEvent += new AngleInput.GetAngleDelegate(GetAngleOffsetInput);
            angleOffsetInput.Optional = true;

            MultipleChoiceInput insertMode = new MultipleChoiceInput("Constr.Solid.SolidInsertMode", "Constr.Solid.SolidInsertMode.Values");
            insertMode.GetChoiceEvent += new MultipleChoiceInput.GetChoiceDelegate(GetInsertMode);
            insertMode.SetChoiceEvent += new CADability.Actions.ConstructAction.MultipleChoiceInput.SetChoiceDelegate(SetInsertMode);

            base.SetInput(geoObjectInput, innerPointInput, rotateLineInput, axisPointInput, axisVectorInput, angleInput, angleOffsetInput, insertMode);
            base.ShowAttributes = true;
            base.ShowActiveObject = false;
            base.OnSetAction();
            if (selectedMode)
                rotateOrg(false); // zum Zeichnen und organisieren

        }



        public override string GetID()
        { return "Constr.Solid.FaceRotate"; }

        public override void OnDone()
        {
            if (base.ShowActiveObject == true)
            {	// es soll mindestens ein shape eingefügt werden
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    for (int i = 0; i < shapeList.Count; i++) // geht durch die komplette Liste
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
                        }
                    }
                    base.DisassembleBlock = true; // the resulting block should be decomposed
                    base.OnDone();
                }
            }
            base.OnDone();
        }

    }
}




