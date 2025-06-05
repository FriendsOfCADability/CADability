using CADability.Attribute;
using CADability.GeoObject;
using CADability.Substitutes;
using System;
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
    public class ScaleObjects : ConstructAction
    {
        private Block block; // der Arbeitsblock
        private GeoObjectList originals; // die übergebenen Originale
        private double faktor;
        private double faktorX;
        private double faktorY;
        private double faktorZ;
        private BoundingCube cube; // der umgebende Quader
        private bool dis; // Verzerrung ja
        private DoubleInput fac;
        private DoubleInput fac1;
        private DoubleInput fac2;
        private DoubleInput fac3;
        private GeoPoint startPoint;
        private GeoPoint endPoint;
        private GeoPointInput startPointInput;
        private GeoPointInput endPointInput;
        private Line feedBackLine;
        private bool copyObject;
        private bool clickandPress; // kein Verziehen, nur Klick


        public ScaleObjects(GeoObjectList list)
        {
            block = Block.Construct(); // Kopie in den Block als Darstellungsmedium
            foreach (IGeoObject go in list)
            {
                block.Add(go.Clone());
            }
            originals = new GeoObjectList(list); // merken für OnDone
            faktor = 1.0;
            faktorX = 1.0;
            faktorY = 1.0;
            faktorZ = 1.0;
        }

        private void SetfixPoint(GeoPoint p)
        {
            block.RefPoint = p;
            base.BasePoint = p; // der Fixpunkt
        }

        private GeoPoint GetfixPoint()
        {
            return base.BasePoint;
        }

        private double CalcFactor(GeoPoint MousePosition)
        {
            startPoint = MousePosition;
            return faktor;
        }

        private double CalculateFactor(GeoPoint MousePosition)
        {
            if (!startPointInput.Fixed)
            {
                startPoint = MousePosition;
            }
            double divRBx; // Abstand rect-Rand zu Referenzpunt in x-Richtung
            double divRBy; // Abstand rect-Rand zu Referenzpunt in y-Richtung
            double divRBz; // Abstand rect-Rand zu Referenzpunt in z-Richtung
            double divMBx; // Abstand MousePosition zu Referenzpunt in x-Richtung
            double divMBy; // Abstand MousePosition zu Referenzpunt in y-Richtung
            double divMBz; // Abstand MousePosition zu Referenzpunt in z-Richtung
            BoundingCube cubeLoc = cube; // der umgebende Kubus der Objekte
            cubeLoc.MinMax(base.BasePoint); // der Punkt kommt noch mit dazu, kann ja auch ausserhalb liegen
            // es wird ein Faktor gebildet aus: (Abstand rect-Rand und Referenzpunkt) und (Abstand MousePos. und Referenzpunkt),
            // getrennt für alle vier Quadranten
            if (MousePosition.x > base.BasePoint.x)
            {
                divRBx = cubeLoc.Xmax - base.BasePoint.x;
                divMBx = MousePosition.x - base.BasePoint.x;
            }
            else
            {
                divRBx = base.BasePoint.x - cubeLoc.Xmin;
                divMBx = base.BasePoint.x - MousePosition.x;
            }
            if (MousePosition.y > base.BasePoint.y)
            {
                divRBy = cubeLoc.Ymax - base.BasePoint.y;
                divMBy = MousePosition.y - base.BasePoint.y;
            }
            else
            {
                divRBy = base.BasePoint.y - cubeLoc.Ymin;
                divMBy = base.BasePoint.y - MousePosition.y;
            }
            if (MousePosition.z > base.BasePoint.z)
            {
                divRBz = cubeLoc.Zmax - base.BasePoint.z;
                divMBz = MousePosition.z - base.BasePoint.z;
            }
            else
            {
                divRBz = base.BasePoint.z - cubeLoc.Zmin;
                divMBz = base.BasePoint.z - MousePosition.z;
            }
            // jetzt werden die drei Faktoren bestimmt
            if (Math.Abs(divRBx) <= 1e-6) faktorX = 0;
            else faktorX = divMBx / divRBx;
            if (Math.Abs(divRBy) <= 1e-6) faktorY = 0;
            else faktorY = divMBy / divRBy;
            if (Math.Abs(divRBz) <= 1e-6) faktorZ = 0;
            else faktorZ = divMBz / divRBz;


            // falls einer ausfällt: den größten anderen nehmen, das erhöht den Benutzungskomfort
            if (faktorX == 0) faktorX = Math.Max(faktorY, faktorZ);
            if (faktorY == 0) faktorY = Math.Max(faktorX, faktorZ);
            if (faktorZ == 0) faktorZ = Math.Max(faktorX, faktorY);

            if (dis) return faktorX; // falls Verzerrung: nur x-Wert, also: facWidth, facH ist global und wird in Setfaktor verwurstet

            //			if (divMBx == 0) return Math.Max(faktorY,faktorZ);
            //			if (divRBx == 0) return ;
            // die Auswahl, welcher von beiden benutzt wird, Quadranten als Strahlen vom Referenzpunkt dirch die Ecken des umgebenden Rechtecks rectLoc
            if ((divMBy / divMBx) > (divRBy / divRBx))
            {
                if ((divMBy / divMBz) > (divRBy / divRBz))
                    return faktorZ;
                else return faktorY;
            }
            else
            {
                if ((divMBx / divMBz) > (divRBx / divRBz))
                    return faktorZ;
                else return faktorX;
            }

            //BoundingRect rectLoc = rect;
            //rectLoc.MinMax(base.ActiveDrawingPlane.Project(base.BasePoint)); // der Punkt kann ja auch ausserhalb liegen
            //// es wird ein Faktor gebildet aus: (Abstand rect-Rand und Referenzpunkt) und (Abstand MousePos. und Referenzpunkt),
            //// getrennt für alle vier Quadranten
            //if (MousePosition.x > base.BasePoint.x)
            //{
            //    divRBx = rectLoc.Right - base.BasePoint.x;
            //    divMBx = MousePosition.x - base.BasePoint.x;
            //}
            //else
            //{
            //    divRBx = base.BasePoint.x - rectLoc.Left;
            //    divMBx = base.BasePoint.x - MousePosition.x;
            //}
            //if (MousePosition.y > base.BasePoint.y)
            //{
            //    divRBy = rectLoc.Top - base.BasePoint.y;
            //    divMBy = MousePosition.y - base.BasePoint.y;
            //}
            //else
            //{
            //    divRBy = base.BasePoint.y - rectLoc.Bottom;
            //    divMBy = base.BasePoint.y - MousePosition.y;
            //}
            //// jetzt werden die beiden Faktoren bestimmt
            //if (divRBy == 0) facH = 0;
            //else facH = divMBy / divRBy;
            //if (divRBx == 0) facW = 0;
            //else facW = divMBx / divRBx;

            //if (dis) return facW; // falls Verzerrung: nur x-Wert, also: facWidth, facH ist global und wird in Setfaktor verwurstet

            //// falls einer ausfällt: den anderen nehmen, das erhöht den Benutzungskomfort
            //if (facH == 0) facH = facW;
            //if (facW == 0) facW = facH;

            //if (divMBx == 0) return facH;
            //if (divRBx == 0) return facW;
            //// die Auswahl, welcher von beiden benutzt wird, Quadranten als Strahlen vom Referenzpunkt dirch die Ecken des umgebenden Rechtecks rectLoc
            //if ((divMBy / divMBx) > (divRBy / divRBx))
            //    return facH;
            //else return facW;

        }

        private double CalculateFactor2(GeoPoint MousePosition)
        {
            return faktorY;
            //			return facH;
        }

        private double CalculateFactor3(GeoPoint MousePosition)
        {
            return faktorZ;
            //			return facH;
        }

        private bool SetFactor(double val)
        {	// zunächst: Block zurücksetzen
            for (int i = 0; i < originals.Count; ++i)
            {
                block.Item(i).CopyGeometry(originals[i]);
            }
            ModOp m;
            m = ModOp.Scale(base.BasePoint, val);
            if (Precision.IsNull(Math.Abs(m.Determinant))) return false;
            faktorX = val;
            faktorY = val;
            faktorZ = val;
            block.Modify(m);
            faktor = val;
            return true;
        }

        private double GetFactor()
        {	// wegen der Anzeige
            return faktor;
        }

        private bool SetFactor1(double val)
        {	// zunächst: Block zurücksetzen
            for (int i = 0; i < originals.Count; ++i)
            {
                block.Item(i).CopyGeometry(originals[i]);
            }
            ModOp m;
            //			faktorY = facH;
            m = ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.DirectionX, val) * ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.DirectionY, faktorY) * ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.Normal, faktorZ);
            if (Precision.IsNull(Math.Abs(m.Determinant))) return false;
            block.Modify(m);
            faktorX = val;
            faktor = 1.0;
            return true;
        }

        private double GetFactor1()
        {	// wegen der Anzeige
            return faktorX;
        }

        private bool SetFactor2(double val)
        {	// zunächst: Block zurücksetzen
            for (int i = 0; i < originals.Count; ++i)
            {
                block.Item(i).CopyGeometry(originals[i]);
            }
            ModOp m;
            m = ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.DirectionX, faktorX) * ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.DirectionY, val) * ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.Normal, faktorZ);
            if (Precision.IsNull(Math.Abs(m.Determinant))) return false;
            block.Modify(m);
            faktorY = val;
            return true;
        }

        private double GetFactor2()
        {	// wegen der Anzeige
            return faktorY;
        }

        private bool SetFactor3(double val)
        {	// zunächst: Block zurücksetzen
            for (int i = 0; i < originals.Count; ++i)
            {
                block.Item(i).CopyGeometry(originals[i]);
            }
            ModOp m;
            m = ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.DirectionX, faktorX) * ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.DirectionY, faktorY) * ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.Normal, val);
            if (Precision.IsNull(Math.Abs(m.Determinant))) return false;
            block.Modify(m);
            faktorZ = val;
            return true;
        }

        private double GetFactor3()
        {	// wegen der Anzeige
            return faktorZ;
        }


        private void facOnMouseClick(bool up, GeoPoint MousePosition, IView View)
        {
            if (!up) // also beim Drücken, nicht beim Loslassen
            {
                // statt des umgebenden Kubus der Objekte ist es nun der definierter Punkt p
                cube = BoundingCube.EmptyBoundingCube;
                cube.MinMax(base.BasePoint); // der FixPunkt 
                cube.MinMax(MousePosition); // der Skalierungspunkt kommt dazu
                startPoint = MousePosition; // den Runterdrückpunkt merken
                //                base.BasePoint = startPoint;
                startPointInput.Fixed = true; // und sagen dass er existiert
                if (dis)
                {
                    fac1.Fixed = true;
                    fac2.Fixed = true;
                    fac3.Fixed = true;
                }
                else fac.Fixed = true;
                base.FeedBack.Add(feedBackLine);
                base.SetFocus(endPointInput, true); // Focus auf den Endpunkt setzen
            }
        }

        private void SetStartPoint(GeoPoint p)
        {
            // statt des umgebenden Rechtecks der Objekte ist es nun der definierter Punkt p
            startPoint = p;
            cube = BoundingCube.EmptyBoundingCube;
            cube.MinMax(base.BasePoint); // der FixPunkt 
            cube.MinMax(p); // der Skalierungspunkt kommt hinzu
        }

        private GeoPoint GetStartPoint()
        {
            return startPoint;
        }

        private void SetEndPoint(GeoPoint p)
        {
            endPoint = p;
            if (Frame.ActiveView.Projection.WorldToDeviceFactor * Geometry.Dist(startPoint, p) > 3)
                clickandPress = true; // falls er jemals mehr als 3 Pixel gedrückt hielt: Vektor bestimmen, sonst: raus
            if (clickandPress)
            {
                if (dis) SetFactor1(CalculateFactor(p));
                else SetFactor(CalculateFactor(p));
                feedBackLine.SetTwoPoints(startPoint, p);
                //                base.ActiveObject = block;
            }
        }

        private GeoPoint GetEndPoint()
        {
            return endPoint;
        }

        private void SetDistort(bool val)
        {
            dis = val;
            fac.ReadOnly = dis;
            fac.Optional = dis;
            if (dis) base.SetFocus(fac1, true);
            else base.SetFocus(fac, true);
            fac1.ReadOnly = !dis;
            fac1.Optional = !dis;
            fac2.ReadOnly = !dis;
            fac2.Optional = !dis;
            fac3.ReadOnly = !dis;
            fac3.Optional = !dis;
        }

        private void SetCopy(bool val)
        {
            copyObject = val;
        }

        public override void OnSetAction()
        {
            base.ActiveObject = block;
            base.TitleId = "ScaleObjects";
            dis = ConstrDefaults.DefaultScaleDistort;
            copyObject = ConstrDefaults.DefaultCopyObjects;
            feedBackLine = Line.Construct();
            Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
            feedBackLine.ColorDef = new ColorDef("", backColor);
            base.SetCursor(SnapPointFinder.DidSnapModes.DidNotSnap, "Size");
            clickandPress = false;

            GeoPointInput fixPoint = new GeoPointInput("Objects.FixPoint", base.BasePoint);
            fixPoint.Optional = true;
            fixPoint.DefinesHotSpot = true;
            fixPoint.HotSpotSource = "Hotspots.png:0";
            fixPoint.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetfixPoint);
            fixPoint.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetfixPoint);
            // dieser Punkt mit dem Fixpunkt dient zur Faktor-Bestimmung, "1" läßt ihn unverändert, "2" verdoppelt seine Entfernung vom Fixpunkt
            startPointInput = new GeoPointInput("Objects.StartPoint");
            startPointInput.Optional = true;
            startPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetStartPoint);
            startPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetStartPoint);

            endPointInput = new GeoPointInput("Objects.EndPoint");
            endPointInput.Optional = true;
            endPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(SetEndPoint);
            endPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(GetEndPoint);

            fac = new DoubleInput("ScaleObjects.Factor", faktor);
            fac.ReadOnly = dis;
            fac.Optional = dis;
            fac.SetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.SetDoubleDelegate(SetFactor);
            fac.GetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.GetDoubleDelegate(GetFactor);
            fac.CalculateDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.CalculateDoubleDelegate(CalculateFactor);
            fac.MouseClickEvent += new MouseClickDelegate(facOnMouseClick);
            // Verzerrung
            BooleanInput distort = new BooleanInput("ScaleObjects.Distort", "ScaleObjects.Distort.Values");
            distort.DefaultBoolean = ConstrDefaults.DefaultScaleDistort;
            distort.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetDistort);
            fac1 = new DoubleInput("ScaleObjects.FactorX");
            fac1.ReadOnly = !dis;
            fac1.Optional = !dis;
            fac1.SetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.SetDoubleDelegate(SetFactor1);
            fac1.GetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.GetDoubleDelegate(GetFactor1);
            fac1.CalculateDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.CalculateDoubleDelegate(CalculateFactor);
            fac1.MouseClickEvent += new MouseClickDelegate(facOnMouseClick);
            fac2 = new DoubleInput("ScaleObjects.FactorY");
            fac2.ReadOnly = !dis;
            fac2.Optional = !dis;
            //			fac2.Optional = true;
            fac2.SetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.SetDoubleDelegate(SetFactor2);
            fac2.GetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.GetDoubleDelegate(GetFactor2);
            fac2.CalculateDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.CalculateDoubleDelegate(CalculateFactor2);

            fac3 = new DoubleInput("ScaleObjects.FactorZ");
            fac3.ReadOnly = !dis;
            fac3.Optional = !dis;
            //			fac2.Optional = true;
            fac3.SetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.SetDoubleDelegate(SetFactor3);
            fac3.GetDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.GetDoubleDelegate(GetFactor3);
            fac3.CalculateDoubleEvent += new CADability.Actions.ConstructAction.DoubleInput.CalculateDoubleDelegate(CalculateFactor3);

            BooleanInput copy = new BooleanInput("Modify.CopyObjects", "YesNo.Values");
            copy.DefaultBoolean = ConstrDefaults.DefaultCopyObjects;
            copy.SetBooleanEvent += new CADability.Actions.ConstructAction.BooleanInput.SetBooleanDelegate(SetCopy);

            // erstmal wird das umgebende Rechteck bestimmt
            cube = BoundingCube.EmptyBoundingCube;
            foreach (IGeoObject go in originals)
            {
                cube.MinMax(go.GetBoundingCube());
            }
            GeoPoint blockCenter = cube.GetCenter();
            block.RefPoint = blockCenter;
            // im Basepoint steht der Fixpunkt der Skalierung
            base.BasePoint = blockCenter;
            base.SetInput(fixPoint, fac, startPointInput, endPointInput, distort, fac1, fac2, fac3, copy);
            base.OnSetAction();
        }

        public override string GetID()
        {
            return "ScaleObjects";
        }

        public override void OnDone()
        {
            // ist die Shift Taste gehalten, so werden Kopien gemacht, d.h. der die Elemente
            // des blocks werden eingefügt. Ansonsten werden die original-Objekte verändert
            // TODO: Feedback über Cursor bei Shift-Taste fehlt noch
            // TODO: die neuen oder veränderten Objekte sollten markiert sein.
            using (Frame.Project.Undo.UndoFrame)
            {
                ModOp m;
                if (!dis) m = ModOp.Scale(base.BasePoint, faktor); // ein faktor für alle!
                else
                {	// 3 Faktoren für 3 Achsen
                    m = ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.DirectionX, faktorX) * ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.DirectionY, faktorY) * ModOp.Scale(base.BasePoint, base.ActiveDrawingPlane.Normal, faktorZ);
                }
                if (!Precision.IsNull(Math.Abs(m.Determinant)))
                {

                    if (((Frame.UIService.ModifierKeys & Keys.Shift) != 0) || copyObject)
                    {
                        GeoObjectList cloned = new GeoObjectList();
                        foreach (IGeoObject go in originals)
                        {
                            IGeoObject cl = go.Clone();
                            cl.Modify(m);
                            cloned.Add(cl);
                        }
                        base.Frame.Project.GetActiveModel().Add(cloned);
                    }
                    else
                    {
                        originals.Modify(m);
                    }
                }
            }
            base.ActiveObject = null; // damit es nicht gleich eingefügt wird
            base.OnDone();
        }
    }
}

