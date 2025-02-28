using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;

// normales runden!!

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ToolsRoundOff : ConstructAction
    {
        private GeoPoint objectPoint; // der  Pickpunkt zum Runden
        private GeoPoint objectPoint1; // der Pickpunkt der zweiten Curve
        private GeoPoint objectPointSav; // der  Pickpunkt zum Runden als Merker für onDone
        private ICurve iCurve1; // lokales Element
        private ICurve iCurve2; // lokales Element
        private ICurve iCurveComposedSplit; // lokales Element
        private ICurve iCurveComposedSingle2Objects; // lokales Element
        private ICurve iCurve1Sav; // lokales Element
        private CurveInput roundObject;
        private CurveInput roundObject2;
        private LengthInput roundRadius;
        private double roundRad; // der globale RundungsRadius
        private Ellipse arc;
        private IGeoObjectOwner owner;
        private IGeoObjectOwner ownerCreated;
        private bool composedSplit;
        //		private bool composedSingle;
        private bool composedSingle2Objects;
        private Path pathCreatedFromModel; //  synthetisch erzeugter Pfad zusammenhängender Objekte



        public ToolsRoundOff()
        { }


        /// <summary>
        /// 
        /// </summary>
        static public bool pathTestIntersection(ICurve iCurveTest, GeoPoint objectPoint)
        {	// jetzt getestet, ob der Pfad sich selbst überschneidet:
            //			owner = (iCurveTest as IGeoObject).Owner; // owner merken für löschen und Einfügen

            if (iCurveTest.GetPlanarState() == PlanarState.Planar) // also in einer Ebene
            {
                Plane pl = iCurveTest.GetPlane();
                ICurve2D curve_2D = iCurveTest.GetProjectedCurve(pl); // die 2D-Kurve
                if (curve_2D is Path2D) (curve_2D as Path2D).Flatten();
                ICurve2D curveTemp;
                if (iCurveTest is Polyline)
                { // GetSelfIntersections geht nur mit Pfaden!
                    Path p = Path.Construct();
                    p.Set(new ICurve[] { iCurveTest.Clone() });
                    p.Flatten();
                    curveTemp = p.GetProjectedCurve(pl);
                }
                else curveTemp = curve_2D;
                double[] pathIntersectionPoints = curveTemp.GetSelfIntersections(); // die Schnittpunkte mit sich selbst
                if (pathIntersectionPoints.Length > 0)
                {
                    double distS = double.MaxValue; // Entfernung des Pickpunkts zu den Schnittpunkten
                    GeoPoint2D objectPoint2D = new GeoPoint2D(0.0, 0.0);
                    int interSectIndex = 0;
                    for (int i = 0; i < pathIntersectionPoints.Length; i += 2) // immer paarweise, jeweils am Anfang und Ende
                    { // den nächsten Schnittpunkt bestimmen, einer pro Paar reicht dazu
                        GeoPoint2D ps = curveTemp.PointAt(pathIntersectionPoints[i]);
                        double distLoc = Geometry.Dist(ps, pl.Project(objectPoint));
                        if (distLoc < distS)
                        {
                            distS = distLoc;
                            objectPoint2D = ps; // Pickpunkt  merken
                            interSectIndex = i;
                        }
                    }
                    // jetzt hat er den nächsten Kreuzungspunkt bestimmt. Nun nachschauen, ob die Mouseposition evtl. noch näher an einem gewöhnlichen Pfadpunkt steht! Wenn ja: nichts machen, der Resr erfolgt dann weiter unten in showRound!
                    for (int i = 0; i < (curveTemp as Path2D).SubCurvesCount; i++)
                    {
                        double distLoc = Geometry.Dist((curveTemp as Path2D).SubCurves[i].StartPoint, pl.Project(objectPoint));
                        if (distLoc < distS)
                        { // ein Pfadpunkt liegt näher: das reicht! Raus hier!
                            return false;
                        }
                    }

                    // Jetzt: die Kreuzungspunkte (einer von vorne, einer von hinten) in die Testkurve einfuegen!
                    if (iCurveTest is Path)
                    {
                        (iCurveTest as Path).InsertPoint(pathIntersectionPoints[interSectIndex]);
                        (iCurveTest as Path).InsertPoint(pathIntersectionPoints[interSectIndex + 1]);
                    }
                    if (iCurveTest is Polyline)
                    {
                        (iCurveTest as Polyline).InsertPoint(pathIntersectionPoints[interSectIndex]);
                        (iCurveTest as Polyline).InsertPoint(pathIntersectionPoints[interSectIndex + 1]);
                    }
                    return true;
                }
            }
            return false;
        }
        /*
             private bool showRoundPath()
             {	// jetzt werden die Rundparameter bestimmt
                 owner = (iCurve1 as IGeoObject).Owner; // owner merken für löschen und Einfügen

                 if (iCurve1.GetPlanarState() == PlanarState.Planar) // also in einer Ebene
                 {
                     Plane pl = iCurve1.GetPlane();
                     ICurve2D curve_2D = iCurve1.GetProjectedCurve(pl); // die 2D-Kurve
                     ICurve2D curveTemp;
                     if (iCurve1 is Polyline)
                     { // GetSelfIntersections geht nur mit Pfaden!
                         Path p = Path.Construct();
                         p.Set(new ICurve[] { iCurve1.Clone() });
                         p.Flatten();
                         curveTemp = p.GetProjectedCurve(pl);
                     }
                     else curveTemp = curve_2D;
                     double[] pathIntersectionPoints = curveTemp.GetSelfIntersections(); // die Schnittpunkte mit sich selbst
                     //				double [] pathIntersectionPoints = curve_2D.GetSelfIntersections(); // die Schnittpunkte mit sich selbst
                     if (pathIntersectionPoints.Length > 0)
                     {
                         double distS = double.MaxValue; // Entfernung des Pickpunkts zu den Schnittpunkten
                         GeoPoint2D objectPoint2D = new GeoPoint2D(0.0, 0.0);
                         int interSectIndex = 0;
                         for (int i = 0; i < pathIntersectionPoints.Length; i += 2) // immer paarweise, jeweils am Anfang und Ende
                         { // den nächsten Schnittpunkt bestimmen, einer pro Paar reicht dazu
                             //						GeoPoint2D ps = curve_2D.PointAt(pathIntersectionPoints[i]);
                             GeoPoint2D ps = curveTemp.PointAt(pathIntersectionPoints[i]);
                             double distLoc = Geometry.Dist(ps, pl.Project(objectPoint));
                             if (distLoc < distS)
                             {
                                 distS = distLoc;
                                 objectPoint2D = ps; // Pickpunkt  merken
                                 interSectIndex = i;
                             }
                         }
                         // jetzt hat er den nächsten Kreuzungspunkt bestimmt. Nun nachschauen, ob die Mouseposition evtl. noch näher an einem gewöhnlichen Pfadpunkt steht! Wenn ja: nichts machen, der Resr erfolgt dann weiter unten in showRound!
                         for (int i = 0; i < (curveTemp as Path2D).SubCurvesCount; i++)
                         {
                             double distLoc = Geometry.Dist((curveTemp as Path2D).SubCurves[i].StartPoint, pl.Project(objectPoint));
                             if (distLoc < distS)
                             { // ein Pfadpunkt liegt näher: das reicht! Raus hier!
                                 return false;
                             }
                         }


                         // Kurve bis zu diesem Schnittpunkt verkürzen
                         curveTemp.Trim(pathIntersectionPoints[interSectIndex], pathIntersectionPoints[interSectIndex + 1]);
                         //					iCurve1.Trim(pathIntersectionPoints[interSectIndex],pathIntersectionPoints[interSectIndex+1]);
                         // die Parallelen links und rechts der 2 Kurven
                         //                    ICurve2D C1 = curve_2D.Parallel(roundRad, false, 0.0, 0.0);
                         //                    ICurve2D C2 = curve_2D.Parallel(-roundRad, false, 0.0, 0.0);
                         ICurve2D C1 = curveTemp.Parallel(roundRad, false, 0.0, 0.0);
                         ICurve2D C2 = curveTemp.Parallel(-roundRad, false, 0.0, 0.0);
                         // nun alle mit allen schneiden und Schnittpunkte (=evtl. Mittelpunkte des Bogens) merken
                         ArrayList centers = new ArrayList();
                         pathIntersectionPoints = C1.GetSelfIntersections();
                         for (int i = 0; i < pathIntersectionPoints.Length; i += 2)
                         {
                             centers.Add(C1.PointAt(pathIntersectionPoints[i]));
                         }
                         pathIntersectionPoints = C2.GetSelfIntersections();
                         for (int i = 0; i < pathIntersectionPoints.Length; i += 2)
                         {
                             centers.Add(C2.PointAt(pathIntersectionPoints[i]));
                         }
                         GeoPoint2D[] centerPoints = (GeoPoint2D[])centers.ToArray(typeof(GeoPoint2D));
                         bool rndPos = false; // Runden möglich
                         GeoPoint2D arcP1 = new GeoPoint2D(0.0, 0.0);
                         GeoPoint2D arcP2 = new GeoPoint2D(0.0, 0.0);
                         GeoPoint2D arcP1Loc = new GeoPoint2D(0.0, 0.0);
                         GeoPoint2D arcP2Loc = new GeoPoint2D(0.0, 0.0);
                         GeoPoint2D arcCenter = new GeoPoint2D(0.0, 0.0);

                         GeoPoint2D[] perpP; // Lotfusspunkte
                         double loc; // location des schnittpunktes der parallelen auf der Kurve
                         double distCS = double.MaxValue; // Entfernung von der Ecke für Schnittpunkte
                         bool ok1, ok2;
                         // der gesuchte Schnittpunkt hat einen realen Lotfusspunkt auf beiden Kurven und den Abstand roundRad von diesen
                         for (int i = 0; i < centerPoints.Length; ++i) // Schleife über alle Schnittpunkte
                         {
                             ok1 = false;
                             ok2 = false;
                             //						perpP = curve_2D.PerpendicularFoot(centerPoints[i]); // Lotpunkt(e) Kurve 
                             perpP = curveTemp.PerpendicularFoot(centerPoints[i]); // Lotpunkt(e) Kurve 
                             //								distCP = double.MaxValue;
                             for (int j = 0; j < perpP.Length; ++j) // Schleife über alle Lotpunkte Kurve 
                             {
                                 //							loc = curve_2D.PositionOf(perpP[j]); // der Parameter des j. Lotpunktes auf Kurve
                                 loc = curveTemp.PositionOf(perpP[j]); // der Parameter des j. Lotpunktes auf Kurve
                                 // der Parameter muss innerhalb der Kurve sein und der Abstand = roundRad
                                 if ((loc > 0.0) & (loc < 1.0) & (Math.Abs(Geometry.Dist(perpP[j], centerPoints[i]) - roundRad) < Precision.eps))
                                 {
                                     if (ok1) // es gibt schon einen
                                     {
                                         arcP2Loc = perpP[j]; // Lotpunkt schonmal merken	
                                         ok2 = true; // einen zweiten gefunden
                                     }
                                     else
                                     {
                                         arcP1Loc = perpP[j]; // Lotpunkt schonmal merken	
                                         ok1 = true;
                                     }
                                 }
                             }
                             if (ok2)
                             {	// falls mehrere Schnittpunkte alle Bedingungen erfüllen: Den nächsten nehmen!
                                 //							double distLoc = Geometry.Dist(centerPoints[i],objectPoint2D);
                                 double distLoc = Geometry.Dist(centerPoints[i], pl.Project(objectPoint));
                                 if (distLoc < distCS)
                                 {
                                     distCS = distLoc;
                                     // jetzt merken
                                     arcCenter = centerPoints[i];
                                     arcP1 = arcP1Loc;
                                     arcP2 = arcP2Loc;
                                     rndPos = true;
                                 }
                             }
                         }
                         if (rndPos) // runden war möglich
                         {	// Mittelwert zwischen dem Kurven
                             //					roundRadCalc = (Math.Abs(curve1_2D.Distance(pl.Project(radiusPoint))) + Math.Abs(curve2_2D.Distance(pl.Project(radiusPoint)))) / 2.0;
                             objectPointSav = pl.ToGlobal(objectPoint2D); // merken für onDone, als Entscheidungskriterium
                             Ellipse arc1 = Ellipse.Construct();
                             arc.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane, arcCenter, arcP1, arcP2, pl, false);
                             arc1.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane, arcCenter, arcP1, arcP2, pl, true);
                             if (Math.Abs(arc.SweepParameter) > Math.Abs(arc1.SweepParameter)) // es ist immer der kleinere Bogen!
                             {
                                 arc = arc1;
                             }
                             arc.CopyAttributes(iCurve1 as IGeoObject);
                             base.ActiveObject = arc; // darstellen
                             return true;
                         }
                     }
                 }
                 base.ActiveObject = null;
                 return false;
             }
     */



        private bool showRound()
        {	// jetzt werden die Rundparameter bestimmt
            base.ActiveObject = null;
            base.FeedBack.ClearSelected();
            if ((iCurve1 != null) && (iCurve2 != null) && (iCurve1 != iCurve2))
            {
                Plane pl;
                if (Curves.GetCommonPlane(iCurve1, iCurve2, out pl))
                {
                    // dient zum Debuggen      pl.Align(Plane.XYPlane, true, true);  
                    if (composedSplit)
                    {
                        if (iCurveComposedSplit != null) owner = (iCurveComposedSplit as IGeoObject).Owner; // owner merken für löschen und Einfügen
                        else owner = ownerCreated;
                    }
                    else owner = (iCurve1 as IGeoObject).Owner; // owner merken für löschen und Einfügen
                    bool rndPos = false; // Runden möglich
                    GeoPoint2D arcP1 = new GeoPoint2D(0.0, 0.0);
                    GeoPoint2D arcP2 = new GeoPoint2D(0.0, 0.0);
                    GeoPoint2D arcP1Loc = new GeoPoint2D(0.0, 0.0);
                    GeoPoint2D arcP2Loc = new GeoPoint2D(0.0, 0.0);
                    GeoPoint2D arcCenter = new GeoPoint2D(0.0, 0.0);
                    ICurve2D curve1_2D = iCurve1.GetProjectedCurve(pl); // die 2D-Kurven
                    ICurve2D curve2_2D = iCurve2.GetProjectedCurve(pl);
                    if (curve1_2D is Path2D) (curve1_2D as Path2D).Flatten();
                    if (curve2_2D is Path2D) (curve2_2D as Path2D).Flatten();
                    // hier die Schnittpunkte bestimmen und den ObjectPoint auf den nächsten Schnittpunt setzen
                    GeoPoint2DWithParameter[] intersectPoints = curve1_2D.Intersect(curve2_2D);
                    GeoPoint2D objectPoint2D = new GeoPoint2D(0.0, 0.0);
                    double distS = double.MaxValue; // Entfernung des Pickpunkts zu den Schnittpunkten
                    for (int i = 0; i < intersectPoints.Length; ++i)
                    {
                        double distLoc = Geometry.Dist(intersectPoints[i].p, pl.Project(objectPoint));
                        if (distLoc < distS)
                        {
                            distS = distLoc;
                            objectPoint2D = intersectPoints[i].p; // nächster Schnittpunkt zum Pickpunkt  merken	
                        }
                    }
                    GeoVector2D v1CutPoint = curve1_2D.DirectionAt(curve1_2D.PositionOf(objectPoint2D));
                    GeoVector2D v2CutPoint = curve2_2D.DirectionAt(curve2_2D.PositionOf(objectPoint2D));
                    if (roundRad == 0.0) // Trick zum Rückgängigmachen des rundens
                    {
                        objectPoint = pl.ToGlobal(objectPoint2D);
                        //						arc.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane,objectPoint2D,objectPoint2D,objectPoint2D,pl,false);
                        arc.SetCirclePlaneCenterRadius(base.ActiveDrawingPlane, objectPoint, base.WorldLength(1));
                        base.ActiveObject = arc; // merken
                                                 //                       base.FeedBack.AddSelected(arc);// darstellen
                        return true;
                    }
                    double locmin1, locmin2; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
                    locmin1 = 0.0; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
                    locmin2 = 0.0; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
                    double locmax1, locmax2; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
                    locmax1 = 1.0;
                    locmax2 = 1.0;
                    double locPar; // für den Fall: virtueller Schnittpunkt die Parametergrenzen anpassen
                    locPar = curve1_2D.PositionOf(objectPoint2D); // position des Schnittpunktes auf der Kurve1
                                                                  //					if ( locPar > 0.5) locmax1 = locPar;
                                                                  //					else locmin1 = locPar;
                    if (locPar > 1.0) locmax1 = locPar;
                    if (locPar < 0.0) locmin1 = locPar;
                    locPar = curve2_2D.PositionOf(objectPoint2D); // position des Schnittpunktes auf der Kurve2
                                                                  //					if ( locPar > 0.5) locmax2 = locPar;
                                                                  //					else locmin2 = locPar;
                    if (locPar > 1.0) locmax2 = locPar;
                    if (locPar < 0.0) locmin2 = locPar;
                    // die Parallelen links und rechts der 2 Kurven
                    ICurve2D P1L1 = curve1_2D.Parallel(roundRad, false, 0.0, 0.0);
                    ICurve2D P1L2 = curve1_2D.Parallel(-roundRad, false, 0.0, 0.0);
                    ICurve2D P2L1 = curve2_2D.Parallel(roundRad, false, 0.0, 0.0);
                    ICurve2D P2L2 = curve2_2D.Parallel(-roundRad, false, 0.0, 0.0);
                    // nun alle mit allen schneiden und Schnittpunkte (=evtl. Mittelpunkte des Bogens) merken
                    ArrayList centers = new ArrayList();
                    if (P1L1 != null && P2L1 != null) centers.AddRange(P1L1.Intersect(P2L1));
                    if (P1L1 != null && P2L2 != null) centers.AddRange(P1L1.Intersect(P2L2));
                    if (P1L2 != null && P2L1 != null) centers.AddRange(P1L2.Intersect(P2L1));
                    if (P1L2 != null && P2L2 != null) centers.AddRange(P1L2.Intersect(P2L2));
                    GeoPoint2DWithParameter[] centerPoints = (GeoPoint2DWithParameter[])centers.ToArray(typeof(GeoPoint2DWithParameter));
                    GeoPoint2D[] perpP; // Lotfusspunkte
                    double loc; // location des schnittpunktes der parallelen auf der Kurve
                    double distCP; // Entfernung von der Ecke für Fusspunkte
                    double distCS = double.MaxValue; // Entfernung von der Ecke für Schnittpunkte
                    bool ok1, ok2;
                    // der gesuchte Schnittpunkt hat einen realen Lotfusspunkt auf beiden Kurven und den Abstand roundRad von diesen
                    for (int i = 0; i < centerPoints.Length; ++i) // Schleife über alle Schnittpunkte
                    {
                        ok1 = false;
                        ok2 = false;
                        perpP = curve1_2D.PerpendicularFoot(centerPoints[i].p); // Lotpunkt(e) Kurve 1
                        distCP = double.MaxValue;
                        for (int j = 0; j < perpP.Length; ++j) // Schleife über alle Lotpunkte Kurve 1
                        {
                            loc = curve1_2D.PositionOf(perpP[j]); // der Parameter des j. Lotpunktes auf Kurve1
                                                                  // der Parameter muss innerhalb der Kurve sein und der Abstand = roundRad
                            if ((loc > locmin1) & (loc < locmax1) & (Math.Abs(Geometry.Dist(perpP[j], centerPoints[i].p) - roundRad) < Precision.eps))
                            {
                                double distLoc = Geometry.Dist(perpP[j], objectPoint2D);
                                if (distLoc < distCP)
                                {
                                    distCP = distLoc;
                                    arcP1Loc = perpP[j]; // Lotpunkt schonmal merken	
                                }
                                ok1 = true;
                            }
                        }
                        if (ok1) // also was gefunden oben, jetzt dasselbe mit Kurve 2
                        {
                            perpP = curve2_2D.PerpendicularFoot(centerPoints[i].p); // Lotpunkt(e) Kurve 2
                            distCP = double.MaxValue;
                            for (int j = 0; j < perpP.Length; ++j) // Schleife über alle Lotpunkte Kurve 2
                            {
                                loc = curve2_2D.PositionOf(perpP[j]); // der Parameter des j. Lotpunktes auf Kurve2
                                                                      // der Parameter muss innerhalb der Kurve sein und der Abstand = roundRad
                                if ((loc > locmin2) & (loc < locmax2) & (Math.Abs(Geometry.Dist(perpP[j], centerPoints[i].p) - roundRad) < Precision.eps))
                                {
                                    double distLoc = Geometry.Dist(perpP[j], objectPoint2D);
                                    if (distLoc < distCP)
                                    {
                                        distCP = distLoc;
                                        arcP2Loc = perpP[j]; // Lotpunkt schonmal merken	
                                    }
                                    ok2 = true;
                                }
                            }

                        }
                        if (ok2)
                        {
                            bool sel = false;
                            if (roundObject.Fixed) // die erste Kurve wurde bestimmt, bei der zweiten soll der OnSameSide-Mechanismus nicht angewandt werden!
                                sel = true;
                            else
                            { // beim Anfahren werden zwei Objekte gleichzeitig selektiert, hier das spezielle Auswahlkriterium:
                                // falls mehrere Schnittpunkte alle Bedingungen erfüllen: Den im selben Quadranten nehmen!
                                if (Geometry.OnSameSide(centerPoints[i].p, pl.Project(objectPoint), objectPoint2D, v1CutPoint) &&
                                    Geometry.OnSameSide(centerPoints[i].p, pl.Project(objectPoint), objectPoint2D, v2CutPoint))
                                    sel = true;
                            }
                            if (sel)
                            {
                                // jetzt merken
                                double distLoc = Geometry.Dist(centerPoints[i].p, pl.Project(objectPoint));
                                // falls mehrere Schnittpunkte alle Bedingungen erfüllen: Den nächsten nehmen!
                                if (distLoc < distCS)
                                {
                                    distCS = distLoc;
                                    // jetzt merken
                                    arcCenter = centerPoints[i].p;
                                    arcP1 = arcP1Loc;
                                    arcP2 = arcP2Loc;
                                    rndPos = true;
                                }
                            }
                        }
                    }
                    if (rndPos && !Precision.IsEqual(arcP1, arcP2)) // runden war möglich
                    {   // Mittelwert zwischen dem Kurven
                        //					roundRadCalc = (Math.Abs(curve1_2D.Distance(pl.Project(radiusPoint))) + Math.Abs(curve2_2D.Distance(pl.Project(radiusPoint)))) / 2.0;
                        objectPointSav = pl.ToGlobal(objectPoint2D); // merken für onDone, als Entscheidungskriterium
                        Ellipse arc1 = Ellipse.Construct();
                        arc.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane, arcCenter, arcP1, arcP2, pl, false);
                        arc1.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane, arcCenter, arcP1, arcP2, pl, true);
                        if (Math.Abs(arc.SweepParameter) > Math.Abs(arc1.SweepParameter)) // es ist immer der kleinere Bogen!
                        {
                            arc = arc1;
                        }
                        //                        if (Math.Abs(curve1_2D.Distance(arcP1)) > Math.Abs(curve1_2D.Distance(arcP2))) (arc as ICurve).Reverse();
                        arc.CopyAttributes(iCurve1 as IGeoObject);


                        //                        base.FeedBack.AddSelected(arc as IGeoObject);// letzte Linie einfügen
                        //Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
                        //if (arc1 is IColorDef)
                        //    (arc1 as IColorDef).ColorDef = new ColorDef("", backColor);
                        //base.ActiveObject = arc1; // darstellen

                        base.FeedBack.AddSelected(arc); // darstellen
                        base.ActiveObject = arc; // merken
                        return true;
                    }
                }
            }
            //            roundObject.HitCursor = CursorTable.GetCursor("RoundOff.cur");
            return false;
        }

        private bool RoundObject(CurveInput sender, ICurve[] Curves, bool up)
        {   // ... nur die sinnvolen Kurven verwenden
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
                roundObject.HitCursor = CursorTable.GetCursor("RoundOff.cur");
                roundObject2.Optional = false;
                roundObject2.ReadOnly = false;
                base.ActiveObject = null;
                base.FeedBack.ClearSelected();
                ICurve pathHit = null;
                if (Curves[0].IsComposed) // also: Pfad oder Polyline
                {
                    pathHit = Curves[0].Clone();
                    iCurveComposedSplit = Curves[0];
                    pathTestIntersection(pathHit, objectPoint);
                }
                else
                {
                    Path p = CADability.GeoObject.Path.CreateFromModel(Curves[0] as ICurve, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
                    if (p != null) pathHit = p as ICurve;
                    pathCreatedFromModel = p;
                    ownerCreated = (Curves[0] as IGeoObject).Owner;
                }
                if (pathHit != null)
                {
                    // jetzt an der Mausposition aufknacken in 2 Pathes
                    ICurve[] tmpCurves = roundObject.SplitAtMousePosition(pathHit);
                    if (tmpCurves.Length == 2)
                    {
                        iCurve1 = tmpCurves[0];
                        iCurve2 = tmpCurves[1];
                        // ab hier Versuch: Rundungsradius ändern!
                        if ((iCurve1 is Path) & (iCurve2 is Path))
                        {
                            ICurve end1 = (iCurve1 as Path).Curve((iCurve1 as Path).Count - 1); // der letzte 1. Path
                            //(end1  as IGeoObject).CopyAttributes(iCurve1 as IGeoObject); // Attribute merken für unten
                            ICurve start2 = (iCurve2 as Path).Curve(0); // der erste 2. Pfad
                            bool iCurve1ok = false;
                            if (end1 is Ellipse)
                            {	// es ist ein Kreisbogen, und er hat einen evtl tangentialen Vorgänger, also Count > 1
                                if ((end1 as Ellipse).IsCircle & (end1 as Ellipse).IsArc & ((iCurve1 as Path).Count > 1))
                                {	// der Vorgänger
                                    ICurve end1vor = (iCurve1 as Path).Curve((iCurve1 as Path).Count - 2);
                                    // Tangentialbedingung mit dem Vorgänger (end1vor) und dem Nachfolger (start2)
                                    if (Precision.SameDirection(end1vor.DirectionAt(1), end1.DirectionAt(0), false)
                                        & Precision.SameDirection(end1.DirectionAt(1), start2.DirectionAt(0), false))
                                    {	// kompiziertes Konstrukt, um den Bogen selber zu entfernen, temporäres Array, eins kürzer 
                                        ICurve[] iCurveShort = new ICurve[(iCurve1 as Path).Count - 1];
                                        // Kopie auf das kürzere Array
                                        // Array.Copy(iCurve1.SubCurves,0,iCurveShort,0,(iCurve1 as Path).Count - 1);
                                        for (int i = 0; i < iCurveShort.Length; ++i)
                                        {
                                            iCurveShort[i] = iCurve1.SubCurves[i].Clone();
                                        }
                                        // iCurve1 neu mit dem kleinen Array
                                        iCurve1 = Path.Construct();
                                        (iCurve1 as Path).Set(iCurveShort);
                                        (iCurve1 as IGeoObject).CopyAttributes(end1 as IGeoObject); // gemerkte Attribute setzen
                                        iCurve1ok = true;
                                    }
                                }
                            }
                            // am Ende von iCurve1 war kein Rundungsbogen, jetzt Anfang iCurve2 untersuchen
                            if (!iCurve1ok & (start2 is Ellipse))
                            { // es ist ein Kreisbogen, und er hat einen evtl tangentialen Nachfolger, also Count > 1
                                if ((start2 as Ellipse).IsCircle & (start2 as Ellipse).IsArc & ((iCurve2 as Path).Count > 1))
                                {	// der Nachfolger
                                    ICurve start2nach = (iCurve2 as Path).Curve(1);
                                    // Tangentialbedingung mit dem Vorgänger (end1) und dem Nachfolger (start2nach)
                                    if (Precision.SameDirection(end1.DirectionAt(1), start2.DirectionAt(0), false)
                                        & Precision.SameDirection(start2.DirectionAt(1), start2nach.DirectionAt(0), false))
                                    {	// kompiziertes Konstrukt, um den Bogen selber zu entfernen, temporäres Array, eins kürzer 
                                        ICurve[] iCurveShort = new ICurve[(iCurve2 as Path).Count - 1];
                                        // Kopie auf das kürzere Array
                                        // Array.Copy(iCurve2.SubCurves,1,iCurveShort,0,(iCurve2 as Path).Count - 1);
                                        for (int i = 0; i < iCurveShort.Length; ++i)
                                        {
                                            iCurveShort[i] = iCurve2.SubCurves[i + 1].Clone();
                                        }
                                        // iCurve2 neu mit dem kleinen Array
                                        iCurve2 = Path.Construct();
                                        (iCurve2 as Path).Set(iCurveShort);
                                        // iCurve2 neu mit dem kleinen Array
                                        (iCurve2 as IGeoObject).CopyAttributes(iCurve1 as IGeoObject); // gemerkte Attribute setzen
                                    }
                                }
                            }
                        }
                        // merken für OnDone zum löschen und Attributsetzen
                        //						iCurveComposedSplit = Curves[0];
                        //roundObject2.Optional = true;
                        //roundObject2.ReadOnly = true;
                        //roundObject.HitCursor = CursorTable.GetCursor("RoundOffReady.cur");
                        composedSplit = true;
                        //if (iCurve1.IsComposed && iCurve1.SubCurves.Length==0)
                        //{
                        //    int dbg = 0;
                        //}
                        //						showRound();
                        if (showRound())
                        {
                            roundObject2.Optional = true;
                            roundObject2.ReadOnly = true;
                            roundObject.HitCursor = CursorTable.GetCursor("RoundOffReady.cur");
                            //                            base.FeedBack.ClearSelected();
                        }
                        else
                        {
                            iCurve1 = Curves[0];
                            composedSplit = false;
                            roundObject2.Optional = false;
                            roundObject2.ReadOnly = false;
                            roundObject.HitCursor = CursorTable.GetCursor("RoundOff.cur");
                        }

                    }
                }
                return true;
                //				base.SetFocus(roundObject2,true);
            }
            if (Curves.Length >= 2)
            {   // nachsehen, ob ein realer Schnittpunkt da ist
                double[] cutPlace = CADability.GeoObject.Curves.Intersect(Curves[0], Curves[1], true);
                if (cutPlace.Length > 0) // nur reale Schnittpunkte sollen gelten
                {   // er hat zwei Kurven gewählt, also roundObject2 ausschalten
                    //roundObject2.Optional = true;
                    //roundObject2.ReadOnly = true;
                    iCurve1 = Curves[0];
                    iCurve2 = Curves[1];
                    if (showRound())
                    {
                        roundObject2.Optional = true;
                        roundObject2.ReadOnly = true;
                        roundObject.HitCursor = CursorTable.GetCursor("RoundOffReady.cur");
                        return true;
                    }
                    else
                    {
                        roundObject.HitCursor = CursorTable.GetCursor("RoundOff.cur");
                        roundObject2.Optional = false;
                        roundObject2.ReadOnly = false;
                        base.FeedBack.ClearSelected();
                        base.ActiveObject = null;
                        return false;
                    }

                    //return(showRound());
                }
            }
            roundObject.HitCursor = CursorTable.GetCursor("RoundOff.cur");
            roundObject2.Optional = false;
            roundObject2.ReadOnly = false;
            base.FeedBack.ClearSelected();
            base.ActiveObject = null;
            return false;
        }

        private void RoundObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            // da machen wir nichts!
        }

        private bool RoundObject2(CurveInput sender, ICurve[] Curves, bool up)
        {   // Mittelpunkt der beiden Pickpunkte
            objectPoint = new GeoPoint(base.CurrentMousePosition, objectPoint1);
            roundRadius.SetDistanceFromPoint(objectPoint); // eingeführt, da basepoint sonst nicht gesetzt (30.9.2013)
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
                    if (showRound())
                    {
                        composedSingle2Objects = true;
                        // kompiziertes Konstrukt, um den Bogen selber zu entfernen, temporäres Array, eins kürzer 
                        ICurve[] iCurveShort = new ICurve[(iCurveComposedSplit as Path).Count - 2];
                        for (int i = 0; i < iCurveShort.Length; ++i)
                        {   // wir brauchen Clones um den Originalpfad nicht zu zerstören
                            iCurveShort[i] = iCurveComposedSplit.SubCurves[i + 1].Clone();
                        }
                        // Kopie auf das kürzere Array
                        // Array.Copy(iCurveComposedSplit.SubCurves,1,iCurveShort,0,(iCurveComposedSplit as Path).Count - 2);
                        //  neu mit dem kleinen Array
                        iCurveComposedSingle2Objects = Path.Construct();
                        (iCurveComposedSingle2Objects as Path).Set(iCurveShort);
                        (iCurveComposedSingle2Objects as IGeoObject).CopyAttributes(iCurveComposedSplit as IGeoObject); // gemerkte Attribute setzen
                        roundObject2.HitCursor = CursorTable.GetCursor("RoundOffReady.cur");
                        return true;
                    }
                    composedSplit = false;
                    iCurve1 = iCurveComposedSplit;
                }
                if (showRound())
                {
                    roundObject2.HitCursor = CursorTable.GetCursor("RoundOffReady.cur");
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

        private void RoundObjectChanged2(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve2 = SelectedCurve;
            showRound();
        }

        private bool RoundRadius(double Length)
        {
            //			if (Length > Precision.eps)
            if (Length >= 0.0)
            {
                roundRad = Length;
                showRound();
                return true; // sonst wird es bei der Aktion nicht übernommen
            }
            base.ActiveObject = null;
            base.FeedBack.ClearSelected();
            return false;
        }

        //private double RoundRadiusCalc(GeoPoint MousePosition)
        //{
        //    radiusPoint = MousePosition;
        //    if (roundObject.Fixed) { if (showRound()) return roundRadCalc; }
        //    return roundRad;
        //}


        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
        /// </summary>
		public override void OnSetAction()
        { // normales runden!!
            base.ActiveObject = null;
            arc = Ellipse.Construct();
            base.TitleId = "ToolsRoundOff";
            composedSingle2Objects = false;
            roundRad = ConstrDefaults.DefaultRoundRadius;
            //			roundRadCalc = roundRad;
            roundObject = new CurveInput("ToolsRound.Object");
            roundObject.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(RoundObject);
            roundObject.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(RoundObjectChanged);
            roundObject.HitCursor = CursorTable.GetCursor("RoundOff.cur");
            roundObject.ModifiableOnly = true;
            roundObject2 = new CurveInput("ToolsRound.Object2");
            roundObject2.ReadOnly = true;
            roundObject2.Optional = true;
            roundObject2.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(RoundObject2);
            roundObject2.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(RoundObjectChanged2);
            roundObject2.HitCursor = CursorTable.GetCursor("RoundOff.cur");
            roundObject2.ModifiableOnly = true;
            roundRadius = new LengthInput("ToolsRound.Radius");
            //			roundRadius.Optional = true;
            roundRadius.ForwardMouseInputTo = roundObject;
            roundRadius.DefaultLength = ConstrDefaults.DefaultRoundRadius;
            // roundRadius.OnCalculateLength +=new Condor.Actions.ConstructAction.LengthInput.CalculateLength(RoundRadiusCalc); // war ausgeklammert, gibt aber Absturz, da kein Basepoint gesetzt
            roundRadius.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(RoundRadius);
            base.SetInput(roundObject, roundObject2, roundRadius);
            base.OnSetAction();
            base.ShowActiveObject = false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnRemoveAction ()"/>
        /// </summary>
		public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.GetID ()"/>
        /// </summary>
        /// <returns></returns>
		public override string GetID()
        {
            return "ToolsRoundOff";
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnDone ()"/>
        /// </summary>
		public override void OnDone()
        {
            if (base.ActiveObject != null)
            {
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    Boolean doIt = true;
                    // alten Pfad entfernen
                    if (composedSplit)
                    {
                        if (iCurveComposedSplit != null)
                            (iCurveComposedSplit as IGeoObject).Owner.Remove(iCurveComposedSplit as IGeoObject);
                        else
                        { // die Einzelelemente des CreateFromModel identifizieren
                            for (int j = 0; j < pathCreatedFromModel.Count; ++j)
                            {
                                IGeoObject obj = null;
                                if ((pathCreatedFromModel.Curve(j) as IGeoObject).UserData.ContainsData("CADability.Path.Original"))
                                    obj = (pathCreatedFromModel.Curve(j) as IGeoObject).UserData.GetData("CADability.Path.Original") as IGeoObject;
                                if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
                            }
                        }
                    }

                    //if (composedSingle) 
                    //{
                    //    if (iCurve1.PositionOf(arc.StartPoint) < iCurve1.PositionOf(arc.EndPoint))
                    //        iCurve1.Trim(iCurve1.PositionOf(arc.StartPoint),iCurve1.PositionOf(arc.EndPoint));
                    //    else iCurve1.Trim(iCurve1.PositionOf(arc.EndPoint),iCurve1.PositionOf(arc.StartPoint));
                    //}
                    //else
                    {
                        if (roundRad == 0.0)
                        {

                            if (iCurve1.PositionOf(objectPointSav) > 0.5)
                                iCurve1.Trim(0.0, iCurve1.PositionOf(objectPointSav));
                            else
                            {
                                iCurve1.Trim(iCurve1.PositionOf(objectPointSav), 1.0);
                                if (Frame.GetBooleanSetting("Construct.MakePath", true)) iCurve1.Reverse();
                            }
                            if (iCurve2.PositionOf(objectPointSav) > 0.5)
                            {
                                iCurve2.Trim(0.0, iCurve2.PositionOf(objectPointSav));
                                if (Frame.GetBooleanSetting("Construct.MakePath", true)) iCurve2.Reverse();
                            }
                            else iCurve2.Trim(iCurve2.PositionOf(objectPointSav), 1.0);
                        }
                        else
                        {
                            if (!Precision.IsEqual(iCurve1.DirectionAt(iCurve1.PositionOf(arc.StartPoint)).Normalized, arc.DirectionAt(0.0).Normalized))
                            { iCurve1.Reverse(); } // damit das beim Pfad klappt weiter unten!
                            if (!Precision.IsEqual(iCurve2.DirectionAt(iCurve2.PositionOf(arc.EndPoint)).Normalized, arc.DirectionAt(1.0).Normalized))
                            { iCurve2.Reverse(); }

                            if (iCurve1.IsClosed)
                            { // um nur die letzte Linie/Element wegzutrimmen und nicht größere Teile
                                if (iCurve1 is Path)
                                { // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das letzte ist 
                                    (iCurve1 as Path).CyclicalPermutation(arc.StartPoint, false);
                                }
                                if (iCurve1 is Polyline)
                                { // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das erste ist 
                                    (iCurve1 as Polyline).CyclicalPermutation(arc.StartPoint, false);
                                }
                            }
                            if (iCurve2.IsClosed)
                            { // um nur die letzte Linie/Element wegzutrimmen und nicht größere Teile
                                if (iCurve2 is Path)
                                { // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das letzte ist 
                                    (iCurve2 as Path).CyclicalPermutation(arc.EndPoint, true);
                                }
                                if (iCurve2 is Polyline)
                                { // setzt den Anfangspunkt so um, dass das Segment am arc.StartPoint das erste ist 
                                    (iCurve2 as Polyline).CyclicalPermutation(arc.EndPoint, true);
                                }
                            }

                            double trimBorderCurve1End = iCurve1.PositionOf(arc.StartPoint);
                            double trimBorderCurve2Start = iCurve2.PositionOf(arc.EndPoint);
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
                        }
                    }
                    if (doIt)
                    {
                        (arc as IGeoObject).CopyAttributes(iCurve1 as IGeoObject);

                        if (Frame.GetBooleanSetting("Construct.MakePath", true) || composedSingle2Objects)
                        {	// das Ergebnis soll in einem Pfad zusammengefasst werden!
                            Path tmpPath = Path.Construct();
                            tmpPath.Add(iCurve1);
                            if (roundRad > 0.0)
                                tmpPath.Add(arc);
                            //                            if (!composedSingle) 
                            tmpPath.Add(iCurve2);
                            if (composedSingle2Objects)
                            {
                                tmpPath.Add(iCurveComposedSingle2Objects);

                            }
                            (tmpPath as IGeoObject).CopyAttributes(iCurve1 as IGeoObject);
                            tmpPath.Flatten(); // bringt alles auf die "geometrische" Ebene, Unterpfade werden aufgelöst
                            owner.Add(tmpPath as IGeoObject);
                        }
                        else
                        {
                            if (roundRad > 0.0)
                                owner.Add(arc as IGeoObject);
                            if (composedSplit)
                            {
                                // da neu erzeugt von SplitAtMousePosition, hier explizit einfügen
                                owner.Add(iCurve1 as IGeoObject);
                                owner.Add(iCurve2 as IGeoObject);
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

