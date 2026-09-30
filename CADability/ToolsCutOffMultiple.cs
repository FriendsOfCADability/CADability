using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ToolsCutOffMultiple : ConstructAction
    {
        private GeoPoint objectPoint; // der  Pickpunkt zum Runden
        private ICurve iCurve1; // lokales Element
        private ICurve iCurve2; // lokales Element
        private CurveInput cutOffObject;
        private LengthInput cutOffLength;
        private AngleInput cutOffAngle;
        private MultipleChoiceInput cutOffMethod;
        private double cutOffLen; // der globale RundungsRadius
        private Angle cutOffAng;
        //		private Line line;
        private int methodSelect;
        private IGeoObjectOwner owner;

        private ICurve iCurveOrg; // OrginalKurve, merken zum evtl. Löschen
        private ICurve iCurveSel; // OrginalKurve 
        private Block blk; // da werden die Objekte drinn gesammelt
        private Path pathCreatedFromModel;



        public ToolsCutOffMultiple()
        { }

        private bool showCutOff()
        {   // nur wenn beide Kurven gültig sind
            if ((iCurve1 != null) && (iCurve2 != null) && (iCurve1 != iCurve2))
            {
                Plane pl; // nur bei gemeisamer Ebene
                if (Curves.GetCommonPlane(iCurve1, iCurve2, out pl))
                {
                    // !!!
                    //					owner = (iCurve1 as IGeoObject).Owner; // owner merken für löschen und Einfügen
                    pl.Align(base.ActiveDrawingPlane, false); // Winkel anpassen
                    ICurve2D curve1_2D = iCurve1.GetProjectedCurve(pl); // die 2D-Kurven
                    if (curve1_2D is Path2D) (curve1_2D as Path2D).Flatten();
                    ICurve2D curve2_2D = iCurve2.GetProjectedCurve(pl);
                    if (curve2_2D is Path2D) (curve2_2D as Path2D).Flatten();
                    ICurve newCurve = iCurve1.Clone();  // neue Kurve bis zum Schnittpunkt 
                                                        // hier die Schnittpunkte bestimmen und den cutPoint auf den nächsten Schnittpunt setzen
                    GeoPoint2DWithParameter cutPoint;
                    if (Curves2D.NearestPoint(curve1_2D.Intersect(curve2_2D), pl.Project(objectPoint), out cutPoint)) // runden war möglich
                    {
                        GeoVector2D v1 = curve1_2D.DirectionAt(cutPoint.par1); // die Richtung im Schnittpunkt
                        if (cutPoint.par1 > 0.5) v1 = v1.Opposite(); // evtl rumdrehen, falls am Ende
                        v1.Norm();
                        GeoVector2D v2 = curve2_2D.DirectionAt(cutPoint.par2); // die Richtung im Schnittpunkt
                        if (cutPoint.par2 > 0.5) v2 = v2.Opposite(); // evtl rumdrehen, falls am Ende
                        v2.Norm();
                        GeoVector2D v = (v1 + v2); // Winkelhalbierende
                        if (Precision.IsNullVector(pl.ToGlobal(v))) return false;
                        v.Norm();
                        v = cutOffLen * v; // Winkelhalbierende mit Fas-Abstand
                        GeoPoint2D dirPoint = cutPoint.p + v; // wird unten auch als Auswahlpunkt für die Richtung genutzt
                        Line2D line2D = new Line2D(dirPoint, cutPoint.p); // Linie vorbesetzen, Punkte eigentlich egal
                        if ((methodSelect == 0) || (methodSelect == 1))
                        {   // 0: Länge cutOffLen = Seitenlänge Kurve1   1: Länge cutOffLen = Fasenlänge 
                            double sideLength;
                            if (methodSelect == 1) // Länge cutOffLen = Fasenlänge 
                                                   // Geometrie, Pythagoras, bekannt Seite=cutOffLen, Winkel 1 = cutOffAng, Winkel 2 = Winkel der Kurven im Schnittpunkt
                                sideLength = cutOffLen * (Math.Cos(cutOffAng.Radian) + (Math.Sin(cutOffAng.Radian) / Math.Tan(Math.Abs(new SweepAngle(v1, v2).Radian))));
                            else sideLength = cutOffLen;
                            // neue Kurve bis zum Schnittpunkt synthetisieren, dazu: Schnittpunkt finden des Abschnitts mit der iCurve1

                            Ellipse arcTmp = Ellipse.Construct();
                            arcTmp.SetCirclePlaneCenterRadius(pl, pl.ToGlobal(cutPoint.p), sideLength);
                            ICurve2D curveArc_2D = (arcTmp as ICurve).GetProjectedCurve(pl); // die 2D-Kurve
                            GeoPoint2DWithParameter cutArc;
                            GeoPoint2D startPoint;
                            if (Curves2D.NearestPoint(curve1_2D.Intersect(curveArc_2D), dirPoint, out cutArc)) // war möglich
                            {
                                startPoint = cutArc.p;
                            }
                            else return false;

                            /*
                                                        double parCut;
                                                        // neue Kurve bis zum Schnittpunkt sythetisieren:
                                                        if (cutPoint.par1 <= 0.5)
                                                        {
                                                            newCurve.StartPoint = iCurve1.PointAt(cutPoint.par1);
                                                            parCut = (sideLength)/newCurve.Length; // der Parameter des Fasenpunktes
                                                        }
                                                        else
                                                        {
                                                            newCurve.EndPoint = iCurve1.PointAt(cutPoint.par1);
                                                            parCut = (newCurve.Length-sideLength)/newCurve.Length; // der Parameter des Fasenpunktes
                                                        }
                                                        GeoPoint2D startPoint = pl.Project(newCurve.PointAt(parCut));
                                                        GeoVector2D vc = curve1_2D.DirectionAt(parCut); // die Richtung im Schnittpunkt
                            */

                            GeoVector2D vc = curve1_2D.DirectionAt(curve1_2D.PositionOf(startPoint)); // die Richtung im Schnittpunkt
                            if (cutPoint.par1 <= 0.5) vc = vc.Opposite(); // evtl rumdrehen, falls am Ende
                            if (Geometry.OnLeftSide(dirPoint, startPoint, vc)) // Richtung festlegen für Winkeloffset
                                vc.Angle = vc.Angle + new SweepAngle(cutOffAng);
                            else
                                vc.Angle = vc.Angle - new SweepAngle(cutOffAng);
                            // Hilfslinie im Fasabstand, im Offsetwinkel
                            line2D = new Line2D(startPoint, startPoint + vc);
                        }
                        if (methodSelect == 2) // Länge cutOffLen = Winkelhalbierendenlänge
                        {
                            v.Angle = v.Angle + new SweepAngle(cutOffAng); // Winkelhalbierendenwinkel + Offset
                                                                           // Hilfslinie im Fasabstand, senkrecht auf der Winkelhalbierenden v
                            line2D = new Line2D(dirPoint, dirPoint + v.ToLeft());
                        }

                        GeoPoint2DWithParameter cutPoint1;
                        GeoPoint2DWithParameter cutPoint2;
                        // schnittpunkte der Hilfslinie mit den beiden Kurven
                        if (Curves2D.NearestPoint(curve1_2D.Intersect(line2D as ICurve2D), cutPoint.p + v, out cutPoint1))
                            if (Curves2D.NearestPoint(curve2_2D.Intersect(line2D as ICurve2D), cutPoint.p + v, out cutPoint2))
                            {   // da isse, die Fas-Linie, nur, wenn die Punkte echt auf der Kurve bis zum Schnittpunkt  liegen:
                                bool onCurve1, onCurve2;
                                if (cutPoint.par1 > 0.5)
                                    onCurve1 = (cutPoint1.par1 > 0.0) & (cutPoint1.par1 < 100);
                                // im Quasi-Parallelfall stehen in par1 riesige Werte
                                else
                                    onCurve1 = (cutPoint1.par1 < 1.0) & (cutPoint1.par1 > -100);
                                if (cutPoint.par2 > 0.5)
                                    onCurve2 = (cutPoint2.par1 > 0.0) & (cutPoint2.par1 < 100);
                                else
                                    onCurve2 = (cutPoint2.par1 < 1.0) & (cutPoint2.par1 > -100);
                                if (onCurve1 && onCurve2)
                                {
                                    Line line = Line.Construct();
                                    line.SetTwoPoints(pl.ToGlobal(cutPoint1.p), pl.ToGlobal(cutPoint2.p));
                                    // Fasenlänge vorgegeben, aber mit obiger Berechnung falsch, da dort Linien vorausgesetzt werden 
                                    if ((methodSelect == 1) && (Math.Abs(line.Length - cutOffLen) > Precision.eps))
                                    {   // jetzt mit Iteration annähern
                                        double parInd = 0.5;
                                        double parIndDelta = 0.25;
                                        for (int i = 0; i < 49; ++i) // 48 Schritte müssen reichen, par kann zwischen 0 und 1 liegen
                                        {
                                            GeoPoint2D startPoint = pl.Project(newCurve.PointAt(parInd));
                                            GeoVector2D vc = curve1_2D.DirectionAt(parInd); // die Richtung im Schnittpunkt
                                            if (cutPoint.par1 <= 0.5) vc = vc.Opposite(); // evtl rumdrehen, falls am Ende
                                            if (Geometry.OnLeftSide(dirPoint, startPoint, vc)) // Richtung festlegen für Winkeloffset
                                                vc.Angle = vc.Angle + new SweepAngle(cutOffAng);
                                            else
                                                vc.Angle = vc.Angle - new SweepAngle(cutOffAng);
                                            // Hilfslinie im Fasabstand, im Offsetwinkel
                                            line2D = new Line2D(startPoint, startPoint + vc);
                                            if (Curves2D.NearestPoint(curve1_2D.Intersect(line2D as ICurve2D), cutPoint.p + v, out cutPoint1))
                                            {
                                                if (Curves2D.NearestPoint(curve2_2D.Intersect(line2D as ICurve2D), cutPoint.p + v, out cutPoint2))
                                                {   // da isse, die Fas-Linie, nur, wenn die Punkte echt auf der Kurvr liegen:
                                                    if (cutPoint.par1 > 0.5)
                                                        onCurve1 = (cutPoint1.par1 > 0.0) & (cutPoint1.par1 < 100);
                                                    // im Quasi-Parallelfall stehen in par1 riesige Werte
                                                    else
                                                        onCurve1 = (cutPoint1.par1 < 1.0) & (cutPoint1.par1 > -100);
                                                    if (cutPoint.par2 > 0.5)
                                                        onCurve2 = (cutPoint2.par1 > 0.0) & (cutPoint2.par1 < 100);
                                                    else
                                                        onCurve2 = (cutPoint2.par1 < 1.0) & (cutPoint2.par1 > -100);
                                                    if (onCurve1 && onCurve2)
                                                    {
                                                        line.SetTwoPoints(pl.ToGlobal(cutPoint1.p), pl.ToGlobal(cutPoint2.p));
                                                        if ((Math.Abs(line.Length - cutOffLen) < Precision.eps)) // gefunden und raus
                                                            break;
                                                        else
                                                        {
                                                            if (line.Length < cutOffLen)
                                                            {   // Fase ist zu klein: Parameter parInd vergrößern
                                                                parInd = parInd + parIndDelta;
                                                                parIndDelta = parIndDelta / 2.0; // delta halbieren (Bisection!!)
                                                                continue; // nächster Schritt in der For-Schleife
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            // alle anderen Fälle: // Fase ist zu gross: Parameter parInd verkleinern
                                            parInd = parInd - parIndDelta;
                                            parIndDelta = parIndDelta / 2.0; // delta halbieren (Bisection!!)
                                        } // for schleife Iteration
                                    } // Ende Iteration
                                    objectPoint = pl.ToGlobal(cutPoint.p);

                                    if (iCurve1.PositionOf(objectPoint) > 0.5) // am oberen Ende geklickt
                                        iCurve1.Trim(0.0, iCurve1.PositionOf(line.StartPoint));
                                    else iCurve1.Trim(iCurve1.PositionOf(line.StartPoint), 1.0);
                                    if (iCurve2.PositionOf(objectPoint) > 0.5) // am oberen Ende geklickt
                                    {
                                        iCurve2.Trim(0.0, iCurve2.PositionOf(line.EndPoint));
                                        objectPoint = iCurve2.StartPoint;
                                    }
                                    else
                                    {
                                        iCurve2.Trim(iCurve2.PositionOf(line.EndPoint), 1.0);
                                        objectPoint = iCurve2.EndPoint;
                                    }
                                    (line as IGeoObject).CopyAttributes(iCurve1);
                                    //									objectPoint = pl.ToGlobal(cutPoint2.p);
                                    blk.Add(iCurve1);
                                    blk.Add(line);
                                    //                                    base.FeedBack.AddSelected(iCurve1 as IGeoObject);// darstellen
                                    //                                    base.FeedBack.AddSelected(line);// darstellen
                                    iCurve1 = iCurve2; // getrimmte Curve2 als Grundlage zur nächsten Berechnung
                                    return true;
                                }
                            }
                    }
                }
                blk.Add(iCurve1); // unveränderte 1. Kurve zufügen, da kein Fasen möglich
                                                //                base.FeedBack.AddSelected(iCurve1 as IGeoObject);// darstellen
                iCurve1 = iCurve2; // unveränderte Curve2 als Grundlage zur nächsten Berechnung
            }
            //			base.ActiveObject = null;
            return false;
        }



        private bool showCutOffOrg()
        {
            base.ActiveObject = null;
            base.FeedBack.ClearSelected();
            if (iCurveSel == null) return false;
            iCurveOrg = iCurveSel; // zum Weglöschen des Originals in onDone
            owner = iCurveSel.Owner; // owner merken für löschen und Einfügen
            pathCreatedFromModel = CADability.GeoObject.Path.CreateFromModel(iCurveSel, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
            if (pathCreatedFromModel != null)
            {
                iCurveOrg = null; // zum nicht Weglöschen des Originals in onDone
                Path pa = (Path)pathCreatedFromModel.Clone(); // Kopie, da Flatten evtl. interne Pfade zerhaut
                // der Pfad wird gleich wieder aufgelöst, damit man die Einzelteile manipulieren kann
                ICurve[] paCurves = pa.Curves;
                bool paIsClosed = pa.IsClosed;
                pa.Clear();
                bool cutOff = false; // zum Merken, ob mindestens 1 mal gefast wurde
                if (paCurves.Length > 1) // nur dann machts Sinn
                {
                    blk = Block.Construct();
                    base.FeedBack.ClearSelected();
                    base.FeedBack.AddSelected(blk as IGeoObject);
                    iCurve1 = paCurves[0];// 1. Linie vorbesetzen
                    iCurve1.CopyAttributes(paCurves[0]);
                    for (int i = 0; i < paCurves.Length - 1; ++i)
                    {
                        iCurve2 = paCurves[i + 1];
                        iCurve2.CopyAttributes(paCurves[i + 1]);
                        if (showCutOff()) cutOff = true; // hat er mindestens 1 mal gerundet?
                    }
                    if (paIsClosed)
                    {
                        iCurve2 = blk.Item(0) as ICurve;
                        iCurve2.CopyAttributes(blk.Item(0) as IGeoObject);
                        if (showCutOff())
                        {
                            cutOff = true; // hat er mindestens 1 mal gerundet?
                                           //                            base.FeedBack.RemoveSelected(0);
                            blk.Remove(0);
                        }
                    }
                    if (cutOff)
                    {
                        blk.Add(iCurve1); // letzte Linie einfügen
                                                        //                        base.FeedBack.AddSelected(iCurve1 as IGeoObject);// darstellen
                        base.ActiveObject = blk; // merken
                        return true;
                    }
                }
            }
            else
            { // also: Einzelelement, jetzt die sinnvollen abarbeiten!
                if (iCurveSel is Polyline)
                {
                    bool cutOff = false; // zum Merken, ob mindestens 1 mal gefast wurde
                    Polyline p = iCurveSel as Polyline;
                    if (p.PointCount > 2) // nur dann machts Sinn
                    {
                        blk = Block.Construct();
                        base.FeedBack.ClearSelected();
                        base.FeedBack.AddSelected(blk as IGeoObject);
                        Line line = Line.Construct();
                        line.StartPoint = p.GetPoint(0);
                        line.EndPoint = p.GetPoint(1);
                        iCurve1 = line; // 1. Linie vorbesetzen
                        iCurve1.CopyAttributes(iCurveSel);
                        for (int i = 0; i < p.PointCount - 2; ++i)
                        {
                            Line line2 = Line.Construct();
                            line2.StartPoint = p.GetPoint(i + 1);
                            line2.EndPoint = p.GetPoint(i + 2);
                            iCurve2 = line2;
                            iCurve2.CopyAttributes(iCurveSel);
                            if (showCutOff()) cutOff = true; // hat er mindestens 1 mal gerundet?
                        }
                        if (p.IsClosed)
                        {
                            Line line2 = Line.Construct();
                            line2.StartPoint = p.GetPoint(p.PointCount - 1);
                            line2.EndPoint = p.GetPoint(0);
                            iCurve2 = line2;
                            iCurve2.CopyAttributes(iCurveSel);
                            if (showCutOff()) cutOff = true; // hat er mindestens 1 mal gerundet?
                            iCurve2 = blk.Item(0) as ICurve;
                            iCurve2.CopyAttributes(iCurveSel);
                            if (showCutOff()) cutOff = true; // hat er mindestens 1 mal gerundet?
                        }
                        if (cutOff)
                        {
                            blk.Add(iCurve1); // letzte Linie einfügen
                                                            //                            base.FeedBack.AddSelected(iCurve1 as IGeoObject);// darstellen
                            base.ActiveObject = blk; // merken
                            return true;
                        }
                    }

                }

                if (iCurveSel is Path)
                {
                    bool cutOff = false; // zum Merken, ob mindestens 1 mal gefast wurde
                    Path p = iCurveSel as Path;
                    if (p.Count > 2) // nur dann machts Sinn
                    {
                        blk = Block.Construct();
                        base.FeedBack.ClearSelected();
                        base.FeedBack.AddSelected(blk as IGeoObject);
                        iCurve1 = p.Curve(0).Clone();// 1. Linie vorbesetzen
                        iCurve1.CopyAttributes(iCurveSel);
                        for (int i = 0; i < p.Count - 1; ++i)
                        {
                            iCurve2 = p.Curve(i + 1).Clone();
                            iCurve2.CopyAttributes(iCurveSel);
                            if (showCutOff()) cutOff = true; // hat er mindestens 1 mal gerundet?
                        }
                        if (p.IsClosed)
                        {
                            iCurve2 = blk.Item(0) as ICurve;
                            iCurve2.CopyAttributes(iCurveSel);
                            if (showCutOff())
                            {
                                cutOff = true; // hat er mindestens 1 mal gerundet?
                                               //                                base.FeedBack.RemoveSelected(0);
                                blk.Remove(0);
                            }
                        }
                        if (cutOff)
                        {
                            blk.Add(iCurve1); // letzte Linie einfügen
                                                            //                            base.FeedBack.AddSelected(iCurve1 as IGeoObject);// darstellen
                            base.ActiveObject = blk; // merken
                            return true;
                        }
                    }
                }
            }
            return false;
        }


        private bool CutOffObject(CurveInput sender, ICurve[] Curves, bool up)
        {   // ... nur die sinnvolen Kurven verwenden
            objectPoint = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                iCurveSel = Curves[0];
                if (showCutOffOrg()) return true;
            }
            base.ActiveObject = null;
            base.FeedBack.ClearSelected();
            return false;
        }

        private void CutOffObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            iCurveSel = SelectedCurve;
            showCutOffOrg();
        }

        private bool CutOffLength(double Length)
        {
            if (Length > Precision.eps)
            {
                cutOffLen = Length;
                showCutOffOrg();
                return true; // sonst wird es bei der Aktion nicht übernommen
            }
            return false;
        }

        private bool cutOffSetAngle(Angle angle)
        {   // nur im (methodSelect = 2)-Fall (Winkelhalbierendenabschnitt) ist Null erlaubt
            if ((methodSelect != 2) && (angle < Precision.eps))
                return false;
            cutOffAng = angle;
            showCutOffOrg();
            return true;
        }

        private void SetMethod(int val)
        {
            methodSelect = val;
        }

        public override void OnSetAction()
        {
            base.ActiveObject = null;
            //			line = new Line();
            base.TitleId = "ToolsCutOffMultiple";
            iCurve1 = null;
            iCurve2 = null;
            cutOffLen = ConstrDefaults.DefaultCutOffLength;
            cutOffAng = ConstrDefaults.DefaultCutOffAngle;
            methodSelect = ConstrDefaults.DefaultCutOffMethod;
            cutOffObject = new CurveInput("ToolsCutOff.Object");
            cutOffObject.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(CutOffObject);
            cutOffObject.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(CutOffObjectChanged);
            cutOffObject.HitCursor = CursorTable.GetCursor("CutOff.cur");
            cutOffObject.ModifiableOnly = true;
            cutOffLength = new LengthInput("ToolsCutOff.Length");
            //			cutOffLength.Optional = true;
            cutOffLength.ForwardMouseInputTo = cutOffObject;
            cutOffLength.DefaultLength = ConstrDefaults.DefaultCutOffLength;
            cutOffLength.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(CutOffLength);
            cutOffAngle = new AngleInput("ToolsCutOff.Angle");
            cutOffAngle.DefaultAngle = ConstrDefaults.DefaultCutOffAngle;
            //			cutOffAngle.Optional = true;
            cutOffAngle.ForwardMouseInputTo = cutOffObject;
            cutOffAngle.SetAngleEvent += new CADability.Actions.ConstructAction.AngleInput.SetAngleDelegate(cutOffSetAngle);
            cutOffMethod = new MultipleChoiceInput("ToolsCutOff.Method", "ToolsCutOff.Method.Values");
            cutOffMethod.DefaultChoice = ConstrDefaults.DefaultCutOffMethod;
            cutOffMethod.ForwardMouseInputTo = cutOffObject;
            cutOffMethod.SetChoiceEvent += new CADability.Actions.ConstructAction.MultipleChoiceInput.SetChoiceDelegate(SetMethod);
            base.SetInput(cutOffObject, cutOffLength, cutOffAngle, cutOffMethod);
            base.ShowActiveObject = false;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "ToolsCutOffMultiple";
        }

        public override void OnDone()
        {
            if (base.ActiveObject != null)
            {
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    if (iCurveOrg != null) // evtl. Einzelobjekt (Polyline oder Path) als Original rauslöschen
                        owner.Remove(iCurveOrg);

                    ICurve[] iCurveList = new ICurve[blk.Count]; // der Pfad braucht eine Kurvenliste
                    for (int i = 0; i < blk.Count; ++i) iCurveList[i] = blk.Item(i) as ICurve; // von Block zu Liste
                    blk.Clear(); // weg mit dem Block, damit die Objekte nur einen owner haben
                    base.ActiveObject = null; // damit der onDone-Mechanismus nichts einfügt

                    if (Frame.GetBooleanSetting("Construct.MakePath", true))
                    {   // das Ergebnis soll in einem Pfad zusammengefasst werden!
                        Path path = Path.Construct();
                        path.Set(iCurveList); // macht den Path mit Header und so
                        path.CopyAttributes(iCurveSel);
                        owner.Add(path); // nur zum owner des angeklickten Ursprungsobjekts
                    }
                    else
                        for (int i = 0; i < iCurveList.Length; ++i)
                        {
                            owner.Add(iCurveList[i]); // nur zum owner des angeklickten Ursprungsobjekts
                        }

                    if (iCurveOrg == null)  // die Einzelelemente des CreateFromModel identifizieren
                        //for (int i=0; i < iCurveList.Length; ++i) 
                        //{
                        //    IGeoObject obj = (iCurveList[i] as IGeoObject).UserData.GetData("CADability.Path.Original") as IGeoObject;
                        //    if (obj!=null && obj.Owner!=null) obj.Owner.Remove(obj); // löschen
                        //}
                        for (int i = 0; i < pathCreatedFromModel.Count; ++i) // über den ursprünglichen Pfad laufen
                        {
                            IGeoObject obj = null;
                            if (pathCreatedFromModel.Curve(i).UserData.ContainsData("CADability.Path.Original"))
                                obj = pathCreatedFromModel.Curve(i).UserData.GetData("CADability.Path.Original") as IGeoObject;
                            if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
                            pathCreatedFromModel.Curve(i).UserData.RemoveUserData("CADability.Path.Original");
                        }
                }
                base.ActiveObject = null;
            }
            base.OnDone();
        }

    }
}




