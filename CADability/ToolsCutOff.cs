using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;


namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ToolsCutOff : ConstructAction
    {
        private GeoPoint objectPoint; // der (evtl. mittlere) Pickpunkt zum Runden
        private GeoPoint objectPoint1; // der Pickpunkt der ersten Curve
        private ICurve iCurve1; // lokales Element
        private ICurve iCurve2; // lokales Element
        private ICurve iCurveComposedSplit; // lokales Element
        private ICurve iCurveComposedSingle2Objects; // lokales Element
        private CurveInput cutOffObject;
        private CurveInput cutOffObject2;
        private LengthInput cutOffLength;
        private AngleInput cutOffAngle;
        private MultipleChoiceInput cutOffMethod;
        private double cutOffLen; // der globale RundungsRadius
        private Angle cutOffAng;
        private Line line;
        private int methodSelect;
        private IGeoObjectOwner owner;
        private bool composedSplit;
        private IGeoObjectOwner ownerCreated;
        private Path pathCreatedFromModel; //  synthetisch erzeugter Pfad zusammenhängender Objekte
        private ICurve iCurve1Sav; // lokales Element
        private bool composedSingle2Objects;


        public ToolsCutOff()
        { }

        private bool showCutOff()
        {	// nur wenn beide Kurven gültig sind
            base.ActiveObject = null;
            base.FeedBack.ClearSelected();
            if ((iCurve1 != null) && (iCurve2 != null) && (iCurve1 != iCurve2))
            {
                Plane pl; // nur bei gemeisamer Ebene
                if (Curves.GetCommonPlane(iCurve1, iCurve2, out pl))
                {
                    if (composedSplit)
                        if (iCurveComposedSplit != null) owner = iCurveComposedSplit.Owner; // owner merken für löschen und Einfügen
                        else owner = ownerCreated;
                    //                        owner = (iCurveComposedSplit as IGeoObject).Owner; // owner merken für löschen und Einfügen
                    else owner = iCurve1.Owner; // owner merken für löschen und Einfügen
                    pl.Align(base.ActiveDrawingPlane, false); // Winkel anpassen
                    ICurve2D curve1_2D = iCurve1.GetProjectedCurve(pl); // die 2D-Kurven
                    if (curve1_2D is Path2D) (curve1_2D as Path2D).Flatten();
                    ICurve2D curve2_2D = iCurve2.GetProjectedCurve(pl);
                    if (curve2_2D is Path2D) (curve2_2D as Path2D).Flatten();
                    ICurve newCurve = iCurve1.Clone();  // neue Kurve bis zum Schnittpunkt 
                                                        // hier die Schnittpunkte bestimmen und den cutPoint auf den nächsten Schnittpunt setzen
                    GeoPoint2DWithParameter cutPoint;
                    if (Curves2D.NearestPoint(curve1_2D.Intersect(curve2_2D), pl.Project(objectPoint), out cutPoint)) // fasen war möglich
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

                                                        // neue Kurve bis zum Schnittpunkt synthetisieren:
                                                        if (cutPoint.par1 <= 0.5)
                                                        {
                                                            newCurve.Trim(cutPoint.par1,1.0);
                                                            parCut = (sideLength)/newCurve.Length; // der Parameter des Fasenpunktes
                                                        }
                                                        else
                                                        {
                                                            newCurve.Trim(0.0,cutPoint.par1);
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
                                // Berechnung, ob die Kurven realen Schnittpunkt haben
                                //								if ((cutPoint.par1 <= 1.0001) & (cutPoint.par1 >= -0.0001) & (cutPoint.par2 <= 1.0001) & (cutPoint.par2 >= -0.0001))
                                if (curve1_2D.IsParameterOnCurve(cutPoint.par1) & curve2_2D.IsParameterOnCurve(cutPoint.par2))
                                {
                                    onCurve1 = (cutPoint1.par1 > 0.0) & (cutPoint1.par1 < 1.0);
                                    onCurve2 = (cutPoint2.par1 > 0.0) & (cutPoint2.par1 < 1.0);
                                }
                                else
                                {
                                    if (cutPoint.par1 > 0.5)
                                        onCurve1 = (cutPoint1.par1 > 0.0) & (cutPoint1.par1 < 100);
                                    // im Quasi-Parallelfall stehen in par1 riesige Werte
                                    else
                                        onCurve1 = (cutPoint1.par1 < 1.0) & (cutPoint1.par1 > -100);
                                    if (cutPoint.par2 > 0.5)
                                        onCurve2 = (cutPoint2.par1 > 0.0) & (cutPoint2.par1 < 100);
                                    else
                                        onCurve2 = (cutPoint2.par1 < 1.0) & (cutPoint2.par1 > -100);
                                }
                                if (onCurve1 && onCurve2)
                                {
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
                                    line.CopyAttributes(iCurve1);
                                    base.ActiveObject = line; // merken
                                    base.FeedBack.AddSelected(line);// darstellen
                                    return true;
                                }
                            }
                    }
                }
            }
            return false;
        }

        /*
                private bool CutOffObject(CurveInput sender, ICurve [] Curves, bool up)
                {	// ... nur die sinnvolen Kurven verwenden
                    objectPoint1 = base.CurrentMousePosition;
                    objectPoint = objectPoint1;
                    if (up) 
                        if (Curves.Length == 0) sender.SetCurves(Curves,null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                        else sender.SetCurves(Curves,Curves[0]); 
                    composedSplit = false;
                    if (Curves.Length == 1)
                    {	// er hat nur eine Kurve gewählt, also cutOffObject2 freischalten
                        iCurve1 = Curves[0];
                        cutOffObject2.Optional = false;
                        cutOffObject2.ReadOnly = false;
        //				composedSplit = false;
                        if (Curves[0].IsComposed) // also: Pfad oder Polyline
                        {	// jetzt an der Mausposition aufknacken in 2 Pathes
                            ICurve [] tmpCurves = cutOffObject.SplitAtMousePosition(Curves[0]);
                            if (tmpCurves.Length == 2)
                            {
                                iCurve1 = tmpCurves[0];
                                iCurve2 = tmpCurves[1];
                                // merken für OnDone zum löschen und Attributsetzen
                                iCurveComposedSplit = Curves[0];
                                cutOffObject2.Optional = true;
                                cutOffObject2.ReadOnly = true;
                                composedSplit = true;
                            }
                        }
                        cutOffLength.SetDistanceFromLine(iCurve1.StartPoint,iCurve1.EndPoint);
                        if (showCutOff())
                            cutOffObject.HitCursor = CursorTable.GetCursor("CutOffReady.cur");
                        else
                            cutOffObject.HitCursor = CursorTable.GetCursor("CutOff.cur");
                        return true;
                    }
                    if (Curves.Length >= 2)
                    {	// nachsehen, ob ein realer Schnittpunkt da ist
                        double[] cutPlace = CADability.GeoObject.Curves.Intersect(Curves[0], Curves[1], true);
                        if (cutPlace.Length > 0) // nur reale Schnittpunkte sollen gelten
                        {	// er hat zwei Kurven gewählt, also cutOffObject2 ausschalten
                            cutOffObject2.Optional = true;
                            cutOffObject2.ReadOnly = true;
                            iCurve1 = Curves[0];
                            iCurve2 = Curves[1];
                            cutOffLength.SetDistanceFromLine(iCurve1.StartPoint,iCurve1.EndPoint);
                            if (showCutOff())
                            {
                                cutOffObject.HitCursor = CursorTable.GetCursor("CutOffReady.cur");
                                return true;
                            }
                            else
                            {
                                cutOffObject.HitCursor = CursorTable.GetCursor("CutOff.cur");
                                return false;
                            }
                        }
                    }
                    cutOffObject.HitCursor = CursorTable.GetCursor("CutOff.cur");
                    cutOffObject2.Optional = false;
                    cutOffObject2.ReadOnly = false;
                    base.ActiveObject = null;
                    base.FeedBack.ClearSelected();
                    return false;
                }
        */

        private bool CutOffObject(CurveInput sender, ICurve[] Curves, bool up)
        {	// ... nur die sinnvolen Kurven verwenden
            objectPoint1 = base.CurrentMousePosition;
            objectPoint = objectPoint1;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            //           composedSingle = false;
            composedSplit = false;
            iCurveComposedSplit = null;
            pathCreatedFromModel = null;
            if (Curves.Length == 1)
            {
                // er hat nur eine Kurve gewählt, also roundObject2 freischalten
                iCurve1 = Curves[0];
                iCurve1Sav = iCurve1;
                cutOffObject.HitCursor = CursorTable.GetCursor("CutOff.cur");
                cutOffObject2.Optional = false;
                cutOffObject2.ReadOnly = false;
                base.ActiveObject = null;
                base.FeedBack.ClearSelected();
                ICurve pathHit = null;
                if (Curves[0].IsComposed) // also: Pfad oder Polyline
                {
                    pathHit = Curves[0].Clone();
                    iCurveComposedSplit = Curves[0];
                    ToolsRoundOff.pathTestIntersection(pathHit, objectPoint);
                }
                else
                {
                    Path p = CADability.GeoObject.Path.CreateFromModel(Curves[0] as ICurve, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
                    if (p != null) pathHit = p as ICurve;
                    pathCreatedFromModel = p;
                    ownerCreated = Curves[0].Owner;
                }
                if (pathHit != null)
                {
                    // jetzt an der Mausposition aufknacken in 2 Pathes
                    ICurve[] tmpCurves = cutOffObject.SplitAtMousePosition(pathHit);
                    if (tmpCurves.Length == 2)
                    {
                        iCurve1 = tmpCurves[0];
                        iCurve2 = tmpCurves[1];
                        composedSplit = true;
                        //                       cutOffLength.SetDistanceFromLine(iCurve1.StartPoint, iCurve1.EndPoint);
                        if (showCutOff())
                        {
                            cutOffObject2.Optional = true;
                            cutOffObject2.ReadOnly = true;
                            cutOffObject.HitCursor = CursorTable.GetCursor("CutOffReady.cur");
                            //                            base.FeedBack.ClearSelected();
                        }
                        else
                        {
                            iCurve1 = Curves[0];
                            composedSplit = false;
                            cutOffObject2.Optional = false;
                            cutOffObject2.ReadOnly = false;
                            cutOffObject.HitCursor = CursorTable.GetCursor("CutOff.cur");
                        }

                    }
                }
                return true;
                //				base.SetFocus(roundObject2,true);
            }
            if (Curves.Length >= 2)
            {	// nachsehen, ob ein realer Schnittpunkt da ist
                double[] cutPlace = CADability.GeoObject.Curves.Intersect(Curves[0], Curves[1], true);
                if (cutPlace.Length > 0) // nur reale Schnittpunkte sollen gelten
                {	// er hat zwei Kurven gewählt, also roundObject2 ausschalten
                    //roundObject2.Optional = true;
                    //roundObject2.ReadOnly = true;
                    iCurve1 = Curves[0];
                    iCurve2 = Curves[1];
                    //                    cutOffLength.SetDistanceFromLine(iCurve1.StartPoint, iCurve1.EndPoint);
                    if (showCutOff())
                    {
                        cutOffObject2.Optional = true;
                        cutOffObject2.ReadOnly = true;
                        cutOffObject.HitCursor = CursorTable.GetCursor("CutOffReady.cur");
                        return true;
                    }
                    else
                    {
                        cutOffObject.HitCursor = CursorTable.GetCursor("CutOff.cur");
                        cutOffObject2.Optional = false;
                        cutOffObject2.ReadOnly = false;
                        base.FeedBack.ClearSelected();
                        base.ActiveObject = null;
                        return false;
                    }

                    //return(showRound());
                }
            }
            cutOffObject.HitCursor = CursorTable.GetCursor("CutOff.cur");
            cutOffObject2.Optional = false;
            cutOffObject2.ReadOnly = false;
            base.FeedBack.ClearSelected();
            base.ActiveObject = null;
            return false;
        }


        private void CutOffObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve1 = SelectedCurve;
            showCutOff();
        }
        /*
                private bool CutOffObject2(CurveInput sender, ICurve [] Curves, bool up)
                {	// Mittelpunkt der beiden Pickpunkte
                    objectPoint = new GeoPoint(base.CurrentMousePosition,objectPoint1);
                    if (up) 
                        if (Curves.Length == 0) sender.SetCurves(Curves,null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                        else sender.SetCurves(Curves,Curves[0]); 
                    if (Curves.Length > 0)
                    {
                        iCurve2 = Curves[0];
                        if (showCutOff())
                        {
                            cutOffObject2.HitCursor = CursorTable.GetCursor("CutOffReady.cur");
                            return true;
                        }
                        else
                        {
                            cutOffObject2.HitCursor = CursorTable.GetCursor("CutOff.cur");
                            return false;
                        }

            //			return(showCutOff());
                    }
                    base.ActiveObject = null;
                    base.FeedBack.ClearSelected();
                    return false;
                }
        */

        private bool CutOffObject2(CurveInput sender, ICurve[] Curves, bool up)
        {	// Mittelpunkt der beiden Pickpunkte
            objectPoint = new GeoPoint(base.CurrentMousePosition, objectPoint1);
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                if (composedSingle2Objects) iCurve1 = iCurve1Sav;
                composedSingle2Objects = false;
                iCurve2 = Curves[0];
                if ((iCurve1 is Path) & (iCurve1 == iCurve2) & (iCurve1.SubCurves.Length > 1))
                {
                    iCurveComposedSplit = iCurve1;
                    composedSplit = true;
                    iCurve1 = (Curves[0] as Path).Curve(0).Clone(); // das erste 
                    iCurve2 = (Curves[0] as Path).Curve((Curves[0] as Path).Count - 1).Clone(); // das letzte 
                    if (showCutOff())
                    {
                        composedSingle2Objects = true;
                        // kompiziertes Konstrukt, um den Bogen selber zu entfernen, temporäres Array, eins kürzer 
                        ICurve[] iCurveShort = new ICurve[(iCurveComposedSplit as Path).Count - 2];
                        for (int i = 0; i < iCurveShort.Length; ++i)
                        {	// wir brauchen Clones um den Originalpfad nicht zu zerstören
                            iCurveShort[i] = iCurveComposedSplit.SubCurves[i + 1].Clone();
                        }
                        // Kopie auf das kürzere Array
                        // Array.Copy(iCurveComposedSplit.SubCurves,1,iCurveShort,0,(iCurveComposedSplit as Path).Count - 2);
                        //  neu mit dem kleinen Array
                        iCurveComposedSingle2Objects = Path.Construct();
                        (iCurveComposedSingle2Objects as Path).Set(iCurveShort);
                        iCurveComposedSingle2Objects.CopyAttributes(iCurveComposedSplit); // gemerkte Attribute setzen
                        cutOffObject2.HitCursor = CursorTable.GetCursor("CutOffReady.cur");
                        return true;
                    }
                    iCurve1 = iCurveComposedSplit;
                }
                if (showCutOff())
                {
                    cutOffObject2.HitCursor = CursorTable.GetCursor("CutOffReady.cur");
                    return true;
                }
                else
                {
                    //                   roundObject2.HitCursor = CursorTable.GetCursor("RoundOff.cur");
                    return false;
                }

                //				return(showRound());
            }
            base.ActiveObject = null;
            base.FeedBack.ClearSelected();
            return false;
        }


        private void CutOffObjectChanged2(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve2 = SelectedCurve;
            showCutOff();
        }

        private bool CutOffLength(double Length)
        {
            if (Length > Precision.eps)
            {
                cutOffLen = Length;
                showCutOff();
                return true; // sonst wird es bei der Aktion nicht übernommen
            }
            base.ActiveObject = null;
            base.FeedBack.ClearSelected();
            return false;
        }

        private bool cutOffSetAngle(Angle angle)
        {   // nur im (methodSelect = 2)-Fall (Winkelhalbierendenabschnitt) ist Null erlaubt
            if ((methodSelect != 2) && (angle < Precision.eps))
                return false;
            cutOffAng = angle;
            showCutOff();
            return true;
        }

        private void SetMethod(int val)
        {
            methodSelect = val;
        }


        public override void OnSetAction()
        {
            base.ActiveObject = null;
            line = Line.Construct();
            base.TitleId = "ToolsCutOff";
            iCurve1 = null;
            iCurve2 = null;
            composedSingle2Objects = false;
            composedSplit = false;
            cutOffLen = ConstrDefaults.DefaultCutOffLength;
            cutOffAng = ConstrDefaults.DefaultCutOffAngle;
            methodSelect = ConstrDefaults.DefaultCutOffMethod;
            cutOffObject = new CurveInput("ToolsCutOff.Object");
            cutOffObject.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(CutOffObject);
            cutOffObject.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(CutOffObjectChanged);
            cutOffObject.HitCursor = CursorTable.GetCursor("CutOff.cur");
            cutOffObject.ModifiableOnly = true;
            cutOffObject2 = new CurveInput("ToolsCutOff.Object2");
            cutOffObject2.ReadOnly = true;
            cutOffObject2.Optional = true;
            cutOffObject2.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(CutOffObject2);
            cutOffObject2.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(CutOffObjectChanged2);
            cutOffObject2.HitCursor = CursorTable.GetCursor("CutOff.cur");
            cutOffObject2.ModifiableOnly = true;
            cutOffLength = new LengthInput("ToolsCutOff.Length");
            //			cutOffLength.Optional = true;
            cutOffLength.DefaultLength = ConstrDefaults.DefaultCutOffLength;
            cutOffLength.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(CutOffLength);
            cutOffLength.ForwardMouseInputTo = cutOffObject;
            cutOffAngle = new AngleInput("ToolsCutOff.Angle");
            cutOffAngle.DefaultAngle = ConstrDefaults.DefaultCutOffAngle;
            //			cutOffAngle.Optional = true;
            cutOffAngle.ForwardMouseInputTo = cutOffObject;
            cutOffAngle.SetAngleEvent += new CADability.Actions.ConstructAction.AngleInput.SetAngleDelegate(cutOffSetAngle);
            cutOffMethod = new MultipleChoiceInput("ToolsCutOff.Method", "ToolsCutOff.Method.Values");
            cutOffMethod.DefaultChoice = ConstrDefaults.DefaultCutOffMethod;
            cutOffMethod.ForwardMouseInputTo = cutOffObject;
            cutOffMethod.SetChoiceEvent += new CADability.Actions.ConstructAction.MultipleChoiceInput.SetChoiceDelegate(SetMethod);
            base.SetInput(cutOffObject, cutOffObject2, cutOffLength, cutOffAngle, cutOffMethod);
            base.ShowActiveObject = false;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "ToolsCutOff";
        }

        public override void OnDone()
        {
            if (base.ActiveObject != null)
            {
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    /*					
                                        // alten Pfad entfernen
                                        if (composedSplit) (iCurveComposedSplit as IGeoObject).Owner.Remove(iCurveComposedSplit as IGeoObject);

                                        if (iCurve1.PositionOf(objectPoint) > 0.5) // am oberen Ende geklickt
                                            iCurve1.Trim(0.0,iCurve1.PositionOf(line.StartPoint));
                                        else 
                                        {
                                            iCurve1.Trim(iCurve1.PositionOf(line.StartPoint),1.0);
                                            if (Frame.GetBooleanSetting("Construct.MakePath",true)) iCurve1.Reverse();
                                        }
                                        if (iCurve2.PositionOf(objectPoint) > 0.5) // am oberen Ende geklickt
                                        {
                                            iCurve2.Trim(0.0,iCurve2.PositionOf(line.EndPoint));
                                            if (Frame.GetBooleanSetting("Construct.MakePath",true)) iCurve2.Reverse();
                                        }
                                        else iCurve2.Trim(iCurve2.PositionOf(line.EndPoint),1.0);
                                        (line as IGeoObject).CopyAttributes(iCurve1 as IGeoObject);

                                        if (Frame.GetBooleanSetting("Construct.MakePath",true))
                                        {	// das Ergebnis soll in einem Pfad zusammengefasst werden!
                                            Path tmpPath =  Path.Construct();
                                            tmpPath.Add(iCurve1);
                                            tmpPath.Add(line);
                                            tmpPath.Add(iCurve2);
                                            (tmpPath as IGeoObject).CopyAttributes(iCurve1 as IGeoObject);
                                            tmpPath.Flatten(); // bringt alles auf die "geometrische" Ebene, Unterpfade werden aufglöst
                                            owner.Add(tmpPath as IGeoObject);
                                        }
                                        else 
                                        {
                                            owner.Add(line as IGeoObject); 
                                            if (composedSplit)
                                            {
                                                // da neu erzeugt von SplitAtMousePosition, hier explizit einfügen
                                                owner.Add(iCurve1 as IGeoObject); 
                                                owner.Add(iCurve2 as IGeoObject); 
                                            }
                                        }
                    */

                    Boolean doIt = true;
                    // alten Pfad entfernen
                    if (composedSplit)
                    {
                        if (iCurveComposedSplit != null)
                            iCurveComposedSplit.Owner.Remove(iCurveComposedSplit);
                        else
                        { // die Einzelelemente des CreateFromModel identifizieren
                            for (int j = 0; j < pathCreatedFromModel.Count; ++j)
                            {
                                IGeoObject obj = null;
                                if (pathCreatedFromModel.Curve(j).UserData.ContainsData("CADability.Path.Original"))
                                    obj = pathCreatedFromModel.Curve(j).UserData.GetData("CADability.Path.Original") as IGeoObject;
                                if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
                            }
                        }
                    }

                    //if (!Precision.IsEqual(iCurve1.DirectionAt(iCurve1.PositionOf(line.StartPoint)).Normalized, line.DirectionAt(0.0)))
                    //{ iCurve1.Reverse(); } // damit das beim Pfad klappt weiter unten!
                    //if (!Precision.IsEqual(iCurve2.DirectionAt(iCurve2.PositionOf(line.EndPoint)).Normalized, line.DirectionAt(1.0)))
                    //{ iCurve2.Reverse(); }

                    if (iCurve1.PositionOf(objectPoint) < 0.5) // am unteren Ende geklickt
                        iCurve1.Reverse();
                    if (iCurve2.PositionOf(objectPoint) > 0.5) // am oberen Ende geklickt
                        iCurve2.Reverse();

                    if (iCurve1.IsClosed)
                    { // um nur die letzte Linie/Element wegzutrimmen und nicht größere Teile
                        if (iCurve1 is Path)
                        { // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das letzte ist 
                            (iCurve1 as Path).CyclicalPermutation(line.StartPoint, false);
                        }
                        if (iCurve1 is Polyline)
                        { // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das erste ist 
                            (iCurve1 as Polyline).CyclicalPermutation(line.StartPoint, false);
                        }
                    }
                    if (iCurve2.IsClosed)
                    { // um nur die letzte Linie/Element wegzutrimmen und nicht größere Teile
                        if (iCurve2 is Path)
                        { // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das letzte ist 
                            (iCurve2 as Path).CyclicalPermutation(line.EndPoint, true);
                        }
                        if (iCurve2 is Polyline)
                        { // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das erste ist 
                            (iCurve2 as Polyline).CyclicalPermutation(line.EndPoint, true);
                        }
                    }

                    double trimBorderCurve1End = iCurve1.PositionOf(line.StartPoint);
                    double trimBorderCurve2Start = iCurve2.PositionOf(line.EndPoint);
                    if ((trimBorderCurve1End > 0.0) && (trimBorderCurve2Start < 1.0))
                    {
                        iCurve1.Trim(0.0, trimBorderCurve1End);
                        iCurve2.Trim(trimBorderCurve2Start, 1.0);
                    }
                    else doIt = false;
                    //iCurve1.Trim(0.0,iCurve1.PositionOf(arc.StartPoint));
                    //iCurve2.Trim(iCurve2.PositionOf(arc.EndPoint),1.0);
                    //if (iCurve1.PositionOf(objectPointSav) > 0.5)
                    //    iCurve1.Trim(0.0,iCurve1.PositionOf(arc.StartPoint));
                    //else
                    //{
                    //    iCurve1.Trim(iCurve1.PositionOf(arc.StartPoint),1.0);
                    //    if (Frame.GetBooleanSetting("Construct.MakePath",true)) iCurve1.Reverse();
                    //}
                    //if (iCurve2.PositionOf(objectPointSav) > 0.5)
                    //{
                    //    iCurve2.Trim(0.0,iCurve2.PositionOf(arc.EndPoint));
                    //    if (Frame.GetBooleanSetting("Construct.MakePath",true)) iCurve2.Reverse();
                    //}
                    //else iCurve2.Trim(iCurve2.PositionOf(arc.EndPoint),1.0);
                    if (doIt)
                    {
                        (line as IGeoObject).CopyAttributes(iCurve1);

                        if (Frame.GetBooleanSetting("Construct.MakePath", true) || composedSingle2Objects)
                        {	// das Ergebnis soll in einem Pfad zusammengefasst werden!
                            Path tmpPath = Path.Construct();
                            tmpPath.Add(iCurve1);
                            tmpPath.Add(line);
                            //                            if (!composedSingle) 
                            tmpPath.Add(iCurve2);
                            if (composedSingle2Objects)
                            {
                                tmpPath.Add(iCurveComposedSingle2Objects);

                            }
                            (tmpPath as IGeoObject).CopyAttributes(iCurve1);
                            tmpPath.Flatten(); // bringt alles auf die "geometrische" Ebene, Unterpfade werden aufgelöst
                            owner.Add(tmpPath as IGeoObject);
                        }
                        else
                        {
                            owner.Add(line as IGeoObject);
                            if (composedSplit)
                            {
                                // da neu erzeugt von SplitAtMousePosition, hier explizit einfügen
                                owner.Add(iCurve1);
                                owner.Add(iCurve2);
                            }
                            //if (composedSingle)
                            //{
                            //    // da neu erzeugt, hier explizit einfügen
                            //    owner.Add(iCurve1 as IGeoObject);
                            //}
                        }
                    }

                }
                base.ActiveObject = null;
            }
            base.OnDone();
        }

    }
}



