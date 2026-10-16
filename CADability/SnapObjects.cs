using CADability.Attribute;
using CADability.GeoObject;
using CADability.Substitutes;
using System;
using System.Drawing;
using Point = System.Drawing.Point;


namespace CADability.Actions
{
    /// <summary>
    /// Transforms the selected objects by snapping up to three source points onto three destination points.
    /// The user enters pairs of a source point on the objects and the destination it should be moved to:
    /// with one pair the objects are translated, with two pairs they are also rotated and scaled, with three
    /// pairs the transformation is fully determined (see <see cref="ModOp.Fit(GeoPoint[], GeoPoint[], bool)"/>).
    /// Options allow distortion (non-uniform scaling) and making copies instead of modifying the originals
    /// (also with the shift key). Started from the "MenuId.Object.Snap" command of the selection; the action
    /// id is "SnapObjects".
    /// </summary>
    internal class SnapObjects : ConstructAction
    {
        private Block block; // Arbeits- und Darstellungsblock
        private GeoObjectList originals; // Speicherung der OriginalObjekte
        private GeoPoint src1, src2, src3; // die Objekt=Quellpunkte
        private GeoPoint dst1, dst2, dst3; // die Zielpunkte
        private GeoPointInput srcP1, srcP2, srcP3; // Quellpunktmethoden
        private GeoPointInput dstP1, dstP2, dstP3; // Zielpunktmethoden
        private bool pair1, pair2; // welche Punktepaare sind schon bestimmt
        private bool distSw; // Verzerrung beim 2-Punkte-fangen
        private ModOp m; // die entscheidende Matrix
        private Line feedBackLine;
        private bool copyObject;

        public SnapObjects(GeoObjectList list)
        {
            // macht eine Kopie der zu manipulierenden Objekte, um diese als "aktives Objekt" 
            // während der Aktion darzustellen
            block = Block.Construct();
            foreach (IGeoObject go in list)
            {
                block.Add(go.Clone());
            }
            originals = new GeoObjectList(list);
            m = ModOp.Identity;
            pair1 = false;
            pair2 = false;
        }

        private void SetSourcePoint1(GeoPoint p)
        {
            src1 = p;
        }

        private void SourcePoint1OnMouseClick(bool up, GeoPoint MousePosition, IView View)
        {
            if (!up) // also beim Drücken, nicht beim Loslassen
            {
                src1 = MousePosition; // den Runterdrückpunkt merken
                base.BasePoint = MousePosition;
                srcP1.Fixed = true; // und sagen dass er existiert
                base.SetFocus(dstP1, true); // Focus auf den Endpunkt setzen
            }
        }

        private void SetDestPoint1(GeoPoint p)
        {
            if (srcP1.Fixed) // macht nur Sinn bei bestimmten 1. Quellpunkt
            {  // zunächst: Block zurücksetzen
                for (int i = 0; i < originals.Count; ++i)
                {
                    block.Item(i).CopyGeometry(originals[i]);
                }
                dst1 = p;
                pair1 = true; // dieses Punktepaar ist bestimmt
                GeoPoint[] src = new GeoPoint[1]; // PunkteArrays für ModOp.Fit erstellen
                GeoPoint[] dst = new GeoPoint[1];
                src[0] = src1;
                dst[0] = dst1;
                try
                {
                    m = ModOp.Fit(src, dst, distSw);
                }
                catch (ModOpException)
                {
                    m = ModOp.Identity;
                }
                feedBackLine.SetTwoPoints(src1, p);
                block.Modify(m);
            }
        }

        private void SetSourcePoint2(GeoPoint p)
        {
            src2 = p;
        }

        private void SourcePoint2OnMouseClick(bool up, GeoPoint MousePosition, IView View)
        {
            if (!up) // also beim Drücken, nicht beim Loslassen
            {
                src2 = MousePosition; // den Runterdrückpunkt merken
                base.BasePoint = MousePosition;
                srcP2.Fixed = true; // und sagen dass er existiert
                base.SetFocus(dstP2, true); // Focus auf den Endpunkt setzen
            }

        }

        private void SetDestPoint2(GeoPoint p)
        {
            if (srcP2.Fixed || pair1)  // macht nur sinn bei bestimmten 2. Quellpunkt und Vorgängerpaar
            {   // zunächst: Block zurücksetzen
                for (int i = 0; i < originals.Count; ++i)
                {
                    block.Item(i).CopyGeometry(originals[i]);
                }
                dst2 = p;
                pair2 = true; // dieses Punktepaar ist bestimmt
                GeoPoint[] src = new GeoPoint[2]; // PunkteArrays für ModOp.Fit erstellen
                GeoPoint[] dst = new GeoPoint[2];
                src[0] = src1;
                dst[0] = dst1;
                src[1] = src2;
                dst[1] = dst2;
                try
                {
                    m = ModOp.Fit(src, dst, distSw);
                }
                catch (ModOpException)
                {
                    m = ModOp.Identity;
                }
                feedBackLine.SetTwoPoints(src2, p);
                block.Modify(m);
            }
        }
        private void SetSourcePoint3(GeoPoint p)
        {
            src3 = p;
        }

        private void SourcePoint3OnMouseClick(bool up, GeoPoint MousePosition, IView View)
        {
            if (!up) // also beim Drücken, nicht beim Loslassen
            {
                src3 = MousePosition; // den Runterdrückpunkt merken
                base.BasePoint = MousePosition;
                srcP3.Fixed = true; // und sagen dass er existiert
                base.SetFocus(dstP3, true); // Focus auf den Endpunkt setzen
            }

        }


        private void SetDestPoint3(GeoPoint p)
        {
            if (srcP3.Fixed || pair1 || pair2) // macht nur sinn bei bestimmten 3. Quellpunkt und den beiden Vorgängerpaaren
            {   // zunächst: Block zurücksetzen
                for (int i = 0; i < originals.Count; ++i)
                {
                    block.Item(i).CopyGeometry(originals[i]);
                }
                dst3 = p;
                GeoPoint[] src = new GeoPoint[3]; // PunkteArrays für ModOp.Fit erstellen
                GeoPoint[] dst = new GeoPoint[3];
                src[0] = src1;
                dst[0] = dst1;
                src[1] = src2;
                dst[1] = dst2;
                src[2] = src3;
                dst[2] = dst3;
                try
                {
                    m = ModOp.Fit(src, dst, distSw);
                }
                catch (ApplicationException)
                {
                    m = ModOp.Identity;
                }
                feedBackLine.SetTwoPoints(src3, p);
                block.Modify(m);
            }
        }


        private void SetDistort(Boolean val)
        {   // soll Verzerren erlaubt sein 
            distSw = val;
        }

        private void SetCopy(bool val)
        {   // Kopie erzeugen, Original unbehelligt lassen
            copyObject = val;
        }

        public override void OnSetAction()
        {   // wird beim Start der Aktion aufgerufen
            // hier wird der Inhalt des ControlCenters zusammengesetzt und bestimmt, welche Eingaben für diese Aktion
            // notwendig sind.
            base.ActiveObject = block;
            base.TitleId = "SnapObjects";
            copyObject = ConstrDefaults.DefaultCopyObjects;
            feedBackLine = Line.Construct(); // Feedback während eine Verbindung zwischen Quellpunkt und Zielpunkt hergestellt wird
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            base.FeedBack.Add(feedBackLine);
            base.SetCursor(SnapPointFinder.DidSnapModes.DidNotSnap, "ScaleSnap");
            // Punktepaare
            srcP1 = new GeoPointInput("SnapObjects.Source1");
            srcP1.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetSourcePoint1);
            srcP1.MouseClickEvent += new MouseClickDelegate(SourcePoint1OnMouseClick);
            dstP1 = new GeoPointInput("SnapObjects.Destination1");
            dstP1.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDestPoint1);
            srcP2 = new GeoPointInput("SnapObjects.Source2");
            srcP2.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetSourcePoint2);
            srcP2.MouseClickEvent += new MouseClickDelegate(SourcePoint2OnMouseClick);
            dstP2 = new GeoPointInput("SnapObjects.Destination2");
            dstP2.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDestPoint2);
            srcP3 = new GeoPointInput("SnapObjects.Source3");
            srcP3.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetSourcePoint3);
            srcP3.MouseClickEvent += new MouseClickDelegate(SourcePoint3OnMouseClick);
            dstP3 = new GeoPointInput("SnapObjects.Destination3");
            dstP3.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetDestPoint3);
            // Verzerrung
            BooleanInput distort = new BooleanInput("ScaleObjects.Distort", "ScaleObjects.Distort.Values");
            distort.DefaultBoolean = ConstrDefaults.DefaultScaleDistort;
            distort.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetDistort);
            // Kopie
            BooleanInput copy = new BooleanInput("Modify.CopyObjects", "YesNo.Values");
            copy.DefaultBoolean = ConstrDefaults.DefaultCopyObjects;
            copy.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetCopy);

            // die Eingabeobjekte festlegen
            base.SetInput(srcP1, dstP1, srcP2, dstP2, distort, srcP3, dstP3, copy);
            // wichtig: Basisimplementierung aufrufen
            base.OnSetAction();
        }

        public override string GetID()
        {
            return "SnapObjects";
        }

        public override void OnDone()
        {   // wird aufgerufen, wenn alle Eingaben vollständig gegeben wurden

            // ist die Shift Taste gehalten, so werden Kopien gemacht, d.h. modifizierte Kopien
            // der Originalobjekte werden eingefügt. Ansonsten werden die original-Objekte verändert
            using (Frame.Project.Undo.UndoFrame) // damit es rückgängig gemacht werden kann
            {
                if (((Frame.UIService.ModifierKeys & Keys.Shift) != 0) || copyObject)
                {   // es soll eine Kopie erstellt werden
                    foreach (IGeoObject go in originals)
                    {
                        IGeoObject cl = go.Clone();
                        cl.Modify(m);
                        base.Frame.Project.GetActiveModel().Add(cl); // Kopie in das Modell einfügen
                    }
                }
                else
                {
                    originals.Modify(m); // alle Originalobjekte modifizeiern
                }
            }
            base.ActiveObject = null; // damit das aktive Objekt nicht eingefügt wird. Es diente nur zur Darstellung währen der Aktion
            base.OnDone(); // wichtig: Basisimplementierung aufrufen
        }
    }
}
