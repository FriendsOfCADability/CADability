using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ToolsRoundMultiple : ConstructAction
    {
        private GeoPoint objectPoint; // der (evtl. mittlere) Pickpunkt zum Runden
        private ICurve iCurve1; // zwei Kurven, zum Rundungsbogenberechnen
        private ICurve iCurve2; // 
        private ICurve iCurveOrg; // OrginalKurve, merken zum evtl. Löschen
        private ICurve iCurveSel; // OrginalKurve 
        private CurveInput roundObject;
        private LengthInput roundRadius;
        private double roundRad; // der globale RundungsRadius
                                 //		private double roundRadCalc; // der globale RundungsRadius berechnet
        private Block blk; // da werden die Objekte drinn gesammelt
        private IGeoObjectOwner owner;
        private Path pathCreatedFromModel;



        public ToolsRoundMultiple()
        { }


        private bool showRound()
        {   // jetzt werden die Rundparameter bestimmt
            Plane pl;
            try
            {
                if (Curves.GetCommonPlane(iCurve1, iCurve2, out pl))
                {
                    bool rndPos = false; // Runden möglich
                    GeoPoint2D arcP1 = new GeoPoint2D(0.0, 0.0);
                    GeoPoint2D arcP2 = new GeoPoint2D(0.0, 0.0);
                    GeoPoint2D arcP1Loc = new GeoPoint2D(0.0, 0.0);
                    GeoPoint2D arcP2Loc = new GeoPoint2D(0.0, 0.0);
                    GeoPoint2D arcCenter = new GeoPoint2D(0.0, 0.0);
                    ICurve2D curve1_2D = iCurve1.GetProjectedCurve(pl); // die 2D-Kurven
                    if (curve1_2D is Path2D) (curve1_2D as Path2D).Flatten();
                    ICurve2D curve2_2D = iCurve2.GetProjectedCurve(pl);
                    if (curve2_2D is Path2D) (curve2_2D as Path2D).Flatten();

                    // ist beim menrfachrunden nicht nötig!!
                    // hier die Schnittpunkte bestimmen und den ObjectPoint auf den nächsten Schnittpunt setzen
                    //				GeoPoint2DWithParameter[] intersectPoints = curve1_2D.Intersect(curve2_2D);
                    //				GeoPoint2D objectPoint2D = new GeoPoint2D(0.0,0.0);
                    //				double distS = double.MaxValue; // Entfernung des Pickpunkts zu den Schnittpunkten
                    //				for (int i=0; i< intersectPoints.Length; ++i) // macht hier wenig Sinn, schadet aber auch nicht
                    //				{ // macht hier wenig Sinn, schadet aber auch nicht, Kompatibilität zum einfachrunden
                    //					double distLoc = Geometry.dist(intersectPoints[i].p,pl.Project(objectPoint));
                    //					if (distLoc < distS)
                    //					{
                    //						distS = distLoc;
                    //						objectPoint2D = intersectPoints[i].p; // Pickpunkt  merken	
                    //					}
                    //				}

                    // statt der Berechnung oben: Auswahlpunkt ist der gemeinsame Punkt der beiden Kurven!!
                    GeoPoint2D objectPoint2D = curve1_2D.EndPoint;



                    double locmin1, locmin2; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
                    locmin1 = 0.0; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
                    locmin2 = 0.0; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
                    double locmax1, locmax2; // Parametergrenzen vorbesetzen für den Fall: Realer Schnittpunkt
                    locmax1 = 1.0;
                    locmax2 = 1.0;

                    // bei mehfachrunden nicht nötig, da alle aneinanderhängen, also nur reale Schnittpunkte!
                    //				double locPar; // für den Fall: virtueller Schnittpunkt die Parametergrenzen anpassen
                    //				locPar = curve1_2D.PositionOf(objectPoint2D); // position des Schnittpunktes auf der Kurve1
                    //				if ( locPar > 1.0) locmax1 = locPar;
                    //				if ( locPar < 0.0) locmin1 = locPar;
                    //				locPar = curve2_2D.PositionOf(objectPoint2D); // position des Schnittpunktes auf der Kurve2
                    //				if ( locPar > 1.0) locmax2 = locPar;
                    //				if ( locPar < 0.0) locmin2 = locPar;

                    // die Parallelen links und rechts der 2 Kurven
                    ICurve2D P1L1 = curve1_2D.Parallel(roundRad, false, 0.0, 0.0);
                    ICurve2D P1L2 = curve1_2D.Parallel(-roundRad, false, 0.0, 0.0);
                    ICurve2D P2L1 = curve2_2D.Parallel(roundRad, false, 0.0, 0.0);
                    ICurve2D P2L2 = curve2_2D.Parallel(-roundRad, false, 0.0, 0.0);
                    // nun alle mit allen schneiden und Schnittpunkte (=evtl. Mittelpunkte des Bogens) merken
                    ArrayList centers = new ArrayList();
                    centers.AddRange(P1L1.Intersect(P2L1));
                    centers.AddRange(P1L1.Intersect(P2L2));
                    centers.AddRange(P1L2.Intersect(P2L1));
                    centers.AddRange(P1L2.Intersect(P2L2));
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
                            if ((loc >= locmin1) & (loc <= locmax1) & (Math.Abs(Geometry.Dist(perpP[j], centerPoints[i].p) - roundRad) < Precision.eps))
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
                                if ((loc >= locmin2) & (loc <= locmax2) & (Math.Abs(Geometry.Dist(perpP[j], centerPoints[i].p) - roundRad) < Precision.eps))
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
                        {	// falls mehrere Schnittpunkte alle Bedingungen erfüllen: Den nächsten nehmen!
                            double distLoc = Geometry.Dist(centerPoints[i].p, objectPoint2D);
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
                    if (rndPos && !Precision.IsEqual(arcP1, arcP2)) // runden war möglich
                    {	// Mittelwert zwischen dem Kurven
                        //					roundRadCalc = (Math.Abs(curve1_2D.Distance(pl.Project(radiusPoint))) + Math.Abs(curve2_2D.Distance(pl.Project(radiusPoint)))) / 2.0;
                        objectPoint = pl.ToGlobal(objectPoint2D); // merken als Entscheidungskriterium, ist hier immer der Schnittpunkt
                        Ellipse arc = Ellipse.Construct();
                        Ellipse arc1 = Ellipse.Construct();
                        arc.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane, arcCenter, arcP1, arcP2, pl, false);
                        arc1.SetArcPlaneCenterStartEndPoint(base.ActiveDrawingPlane, arcCenter, arcP1, arcP2, pl, true);
                        if (Math.Abs(arc.SweepParameter) > Math.Abs(arc1.SweepParameter)) // es ist immer der kleinere Bogen!
                        { arc = arc1; }
                        arc.CopyAttributes(iCurve1 as IGeoObject);
                        if (iCurve1.PositionOf(objectPoint) > 0.5)
                            iCurve1.Trim(0.0, iCurve1.PositionOf(arc.StartPoint));
                        else iCurve1.Trim(iCurve1.PositionOf(arc.StartPoint), 1.0);
                        if (iCurve2.PositionOf(objectPoint) > 0.5)
                            iCurve2.Trim(0.0, iCurve2.PositionOf(arc.EndPoint));
                        else iCurve2.Trim(iCurve2.PositionOf(arc.EndPoint), 1.0);
                        if (iCurve1.Length > Precision.eps)
                        {
                            blk.Add(iCurve1 as IGeoObject);
                            //                        base.FeedBack.AddSelected(iCurve1 as IGeoObject); // darstellen
                        }
                        blk.Add(arc);
                        //                    base.FeedBack.AddSelected(arc); // darstellen
                        iCurve1 = iCurve2; // getrimmte Curve2 als Grundlage zur nächsten Berechnung
                        return true;
                    }
                }
            }
            catch (ApplicationException)
            {
                return false;
            }
            blk.Add(iCurve1 as IGeoObject); // unveränderte 1. Kurve zufügen, da kein Runden möglich
                                            //            base.FeedBack.AddSelected(iCurve1 as IGeoObject); // darstellen
            iCurve1 = iCurve2; // unveränderte Curve2 als Grundlage zur nächsten Berechnung
            return false;
        }

        private bool showRoundOrg()
        {
            base.FeedBack.ClearSelected();
            base.ActiveObject = null;
            if (iCurveSel == null) return false;
            iCurveOrg = iCurveSel; // zum Weglöschen des Originals in onDone
            owner = (iCurveSel as IGeoObject).Owner; // owner merken für löschen und Einfügen
            // erstmal checken, ob da was dranhängt!
            pathCreatedFromModel = CADability.GeoObject.Path.CreateFromModel(iCurveSel, Frame.ActiveView.Model, Frame.ActiveView.Projection, true);
            if (pathCreatedFromModel != null)
            {
                iCurveOrg = null; // Merker zum nicht Weglöschen des Originals in onDone

                Path pa = (Path)pathCreatedFromModel.Clone(); // Kopie, da Flatten evtl. interne Pfade zerhaut
                // der Pfad wird gleich wieder aufgelöst, damit man die Einzelteile manipulieren kann
                ICurve[] paCurves = pa.Curves;
                bool paIsClosed = pa.IsClosed;
                pa.Clear();
                bool rund = false; // zum Merken, ob mindestens 1 mal gerundet wurde
                if (paCurves.Length > 1) // nur dann machts Sinn
                {
                    blk = Block.Construct();
                    base.FeedBack.ClearSelected();
                    base.FeedBack.AddSelected(blk as IGeoObject);
                    iCurve1 = paCurves[0];// 1. Linie vorbesetzen
                    //					(iCurve1 as IGeoObject).CopyAttributes(pa.Curve(0) as IGeoObject);
                    for (int i = 0; i < paCurves.Length - 1; ++i)
                    {
                        iCurve2 = paCurves[i + 1];
                        //						(iCurve2 as IGeoObject).CopyAttributes(pa.Curve(i+1) as IGeoObject);
                        if (showRound()) rund = true; // hat er mindestens 1 mal gerundet?
                    }
                    if (paIsClosed)
                    {
                        iCurve2 = blk.Item(0) as ICurve;
                        //						(iCurve2 as IGeoObject).CopyAttributes(blk.Item(0) as IGeoObject);
                        if (showRound())
                        {
                            rund = true; // hat er mindestens 1 mal gerundet?
                            blk.Remove(0);
                            //                            base.FeedBack.RemoveSelected(0);
                        }
                    }
                    if (rund)
                    {
                        blk.Add(iCurve1 as IGeoObject); // letzte Linie einfügen
                                                        //                        base.FeedBack.AddSelected(iCurve1 as IGeoObject); // darstellen
                        base.ActiveObject = blk; // merken
                        return true;
                    }
                }
            }
            else
            { // also: Einzelelement, jetzt die sinnvollen abarbeiten!
                if (iCurveSel is Polyline)
                {
                    bool rund = false; // zum Merken, ob mindestens 1 mal gerundet wurde
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
                        (iCurve1 as IGeoObject).CopyAttributes(iCurveSel as IGeoObject);
                        for (int i = 0; i < p.PointCount - 2; ++i)
                        {
                            Line line2 = Line.Construct();
                            line2.StartPoint = p.GetPoint(i + 1);
                            line2.EndPoint = p.GetPoint(i + 2);
                            iCurve2 = line2;
                            (iCurve2 as IGeoObject).CopyAttributes(iCurveSel as IGeoObject);
                            if (showRound()) rund = true; // hat er mindestens 1 mal gerundet?
                        }
                        if (p.IsClosed)
                        {
                            Line line2 = Line.Construct();
                            line2.StartPoint = p.GetPoint(p.PointCount - 1);
                            line2.EndPoint = p.GetPoint(0);
                            iCurve2 = line2;
                            (iCurve2 as IGeoObject).CopyAttributes(iCurveSel as IGeoObject);
                            if (showRound()) rund = true; // hat er mindestens 1 mal gerundet?
                            iCurve2 = blk.Item(0) as ICurve;
                            (iCurve2 as IGeoObject).CopyAttributes(iCurveSel as IGeoObject);
                            if (showRound()) rund = true; // hat er mindestens 1 mal gerundet?
                        }
                        if (rund)
                        {
                            blk.Add(iCurve1 as IGeoObject); // letzte Linie einfügen
                                                            //                            base.FeedBack.AddSelected(iCurve1 as IGeoObject); // darstellen
                            base.ActiveObject = blk; // merken
                            return true;
                        }
                    }

                }

                if (iCurveSel is Path)
                {
                    bool rund = false; // zum Merken, ob mindestens 1 mal gerundet wurde
                    Path p = iCurveSel as Path;
                    if (p.Count > 2) // nur dann machts Sinn
                    {
                        blk = Block.Construct();
                        base.FeedBack.ClearSelected();
                        base.FeedBack.AddSelected(blk as IGeoObject);
                        iCurve1 = p.Curve(0).Clone(); // 1. Linie vorbesetzen
                        (iCurve1 as IGeoObject).CopyAttributes(iCurveSel as IGeoObject);
                        for (int i = 0; i < p.Count - 1; ++i)
                        {
                            iCurve2 = p.Curve(i + 1).Clone();
                            (iCurve2 as IGeoObject).CopyAttributes(iCurveSel as IGeoObject);
                            if (showRound()) rund = true; // hat er mindestens 1 mal gerundet?
                        }
                        if (p.IsClosed)
                        {
                            iCurve2 = blk.Item(0) as ICurve;
                            (iCurve2 as IGeoObject).CopyAttributes(iCurveSel as IGeoObject);
                            if (showRound())
                            {
                                rund = true; // hat er mindestens 1 mal gerundet?
                                blk.Remove(0);
                                //                                base.FeedBack.RemoveSelected(0);
                            }
                        }
                        if (rund)
                        {
                            blk.Add(iCurve1 as IGeoObject); // letzte Linie einfügen
                                                            //                            base.FeedBack.AddSelected(iCurve1 as IGeoObject); // darstellen
                            base.ActiveObject = blk; // merken
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool RoundObject(CurveInput sender, ICurve[] Curves, bool up)
        {   // ... nur die sinnvollen Kurven verwenden
            objectPoint = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {   // er hat was gewählt
                iCurveSel = Curves[0];
                if (showRoundOrg()) return true;
            }
            base.FeedBack.ClearSelected();
            base.ActiveObject = null;
            return false;
        }

        private void RoundObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            iCurveSel = SelectedCurve;
            showRoundOrg();
        }

        private bool RoundRadius(double Length)
        {
            if (Length > Precision.eps)
            {
                roundRad = Length;
                showRoundOrg();
                return true; // sonst wird es bei der Aktion nicht übernommen
            }
            return false;
        }

        //		private double RoundRadiusCalc(GeoPoint MousePosition)
        //		{
        //			radiusPoint = MousePosition;
        //			if (roundObject.Fixed)	{ if (showRound()) return roundRadCalc; }
        //			return roundRad;
        //		}

        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
        /// </summary>
        public override void OnSetAction()
        {
            base.ActiveObject = null;
            blk = Block.Construct();
            iCurveOrg = null; // zum nicht Weglöschen des Originals in onDone
            base.TitleId = "ToolsRoundMultiple";
            roundRad = ConstrDefaults.DefaultRoundRadius;
            //			roundRadCalc = roundRad;
            roundObject = new CurveInput("ToolsRound.Object");
            roundObject.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(RoundObject);
            roundObject.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(RoundObjectChanged);
            roundObject.HitCursor = CursorTable.GetCursor("RoundOff.cur");
            roundObject.ModifiableOnly = true;
            roundRadius = new LengthInput("ToolsRound.Radius");
            //			roundRadius.Optional = true;
            roundRadius.ForwardMouseInputTo = roundObject;
            roundRadius.DefaultLength = ConstrDefaults.DefaultRoundRadius;
            //			roundRadius.OnCalculateLength +=new Condor.Actions.ConstructAction.LengthInput.CalculateLength(RoundRadiusCalc);
            roundRadius.SetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.SetLengthDelegate(RoundRadius);
            base.SetInput(roundObject, roundRadius);
            base.ShowActiveObject = false;
            base.OnSetAction();
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
            return "ToolsRoundMultiple";
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnDone ()"/>
        /// </summary>
		public override void OnDone()
        {
            if (base.ActiveObject != null)
            {   // es soll ein Pfad eingefügt werden
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    if (iCurveOrg != null) // evtl. Einzelobjekt (Polyline oder Path) als Original rauslöschen
                        owner.Remove(iCurveOrg as IGeoObject);

                    ICurve[] iCurveList = new ICurve[blk.Count]; // der Pfad braucht eine Kurvenliste
                    for (int i = 0; i < blk.Count; ++i) iCurveList[i] = blk.Item(i) as ICurve; // von Block zu Liste
                    blk.Clear(); // weg mit dem Block, damit die Objekte nur einen owner haben
                    base.ActiveObject = null; // damit der onDone-Mechanismus nichts einfügt

                    if (Frame.GetBooleanSetting("Construct.MakePath", true))
                    {   // das Ergebnis soll in einem Pfad zusammengefasst werden!
                        Path path = Path.Construct();
                        path.Set(iCurveList); // macht den Path mit Header und so
                        (path as IGeoObject).CopyAttributes(iCurveSel as IGeoObject);
                        owner.Add(path); // nur zum owner des angeklickten Ursprungsobjekts
                    }
                    else
                    {
                        for (int i = 0; i < iCurveList.Length; ++i)
                        {
                            owner.Add(iCurveList[i] as IGeoObject); // nur zum owner des angeklickten Ursprungsobjekts
                        }
                    }
                    if (iCurveOrg == null)  // die Einzelelemente des CreateFromModel identifizieren
                                            //						for (int i=0; i < iCurveList.Length; ++i) 
                                            //{
                                            //    IGeoObject obj = (iCurveList[i] as IGeoObject).UserData.GetData("CADability.Path.Original") as IGeoObject;
                                            //    if (obj!=null && obj.Owner!=null) obj.Owner.Remove(obj); // löschen
                                            //    (iCurveList[i] as IGeoObject).UserData.RemoveUserData("CADability.Path.Original");
                                            //}
                        for (int i = 0; i < pathCreatedFromModel.Count; ++i) // über den ursprünglichen Pfad laufen
                        {
                            IGeoObject obj = null;
                            if ((pathCreatedFromModel.Curve(i) as IGeoObject).UserData.ContainsData("CADability.Path.Original"))
                                obj = (pathCreatedFromModel.Curve(i) as IGeoObject).UserData.GetData("CADability.Path.Original") as IGeoObject;
                            if (obj != null && obj.Owner != null) obj.Owner.Remove(obj); // löschen
                            (pathCreatedFromModel.Curve(i) as IGeoObject).UserData.RemoveUserData("CADability.Path.Original");
                        }
                }
            }
            base.OnDone();
        }
    }
}


