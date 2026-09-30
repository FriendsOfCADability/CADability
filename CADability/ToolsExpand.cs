using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;



namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ToolsExpand : ConstructAction
    {
        private GeoPoint objectPoint;
        private ICurve iCurve;
        private ICurve sourceCurve; // hierhin wird verlängert, falls ausgewählt
        //		private static ICurve sourceCurve;
        private ICurve newCurve;
        private CurveInput expandSourceObject;
        private LengthInput distToCurve;
        private double param = 0.0;
        private bool lowerEnd;
        private static ICurve sourceCurveSave; // hierhin wird verlängert, falls ausgewählt und hier gemerkt


        public ToolsExpand()
        {
            //			if (sourceCurveSave != null)
            //				sourceCurve = sourceCurveSave;
            //			sourceCurve = null;

        }

        public override void AutoRepeated()
        {
            sourceCurve = sourceCurveSave; // sourceCurve auf den Instanzübergreifenden Wert gesetzt
        }




        private bool showObject()
        {
            base.FeedBack.ClearSelected();
            base.ActiveObject = null;
            double[] cutPlace;
            ICurve2D c2d = iCurve.GetProjectedCurve(CurrentMouseView.Projection.ProjectionPlane);
            GeoPoint2D op2d = CurrentMouseView.Projection.ProjectUnscaled(objectPoint);
            lowerEnd = (c2d.PositionOf(op2d) < 0.5); // im 2D überprüfen, in 3D ist objectPoint möglicherweise weit von iCurve entfernt
            // bool lowerEnd = (iCurve.PositionOf(objectPoint)< 0.5);
            ProjectedModel.IntersectionMode cutMode;
            bool realCut;
            if (lowerEnd) cutMode = ProjectedModel.IntersectionMode.StartExtension;
            else cutMode = ProjectedModel.IntersectionMode.EndExtension;
            //			if (expandSourceObject.Fixed)
            ICurve[] targetCurves = null;
            if (sourceCurve != null)
                cutPlace = Curves.Intersect(iCurve, sourceCurve, false);
            else
                cutPlace = base.Frame.ActiveView.ProjectedModel.GetIntersectionParameters(iCurve, cutMode, out targetCurves);
            if (cutPlace.Length > 0)
            {
                realCut = false;
                for (int i = 0; i < cutPlace.Length; ++i)
                {
                    if (distToCurve.Length == 0)
                    {
                        if (cutPlace[i] < -1e-8 || cutPlace[i] > 1 + 1e-8) // Endpunkte nicht mit berücksichtigen
                        //if (!((Math.Abs(cutPlace[i]) < 1e-8) || (Math.Abs(cutPlace[i] - 1) < 1e-8)))
                        {
                            realCut = true;
                            break;
                        }
                    }
                    else
                    {
                        //if (cutPlace[i] < 1e-8 || cutPlace[i] > 1 - 1e-8) // Endpunkte mit berücksichtigen
                        {
                            realCut = true;
                            break;
                        }
                    }
                }
                if (realCut)
                {

                    int k = -1;
                    if (lowerEnd) param = double.MinValue;
                    else param = double.MaxValue;
                    double eps; // wenn distToCurve gesetzt ist, dann bleibt ein exakt getrimmtes am Ende gültig, ansonsten wird die nächste Querkurve verwendet
                    if (distToCurve.Length == 0) eps = 1e-8;
                    else
                    {
                        if (sourceCurve != null && sourceCurve.Length > 0) // eine Zielkurve wurde angewählt. Schnitte dürfen jetzt vor dem Ende von iCurve sein
                            //eps = -Math.Abs(distToCurve.Length) / sourceCurve.Length;
                            eps = -1.0; // egal, Hauptsache auf der Kurve
                        else
                            eps = -1e-8;
                    }
                    for (int i = 0; i < cutPlace.Length; ++i)
                    {
                        if (lowerEnd)
                        { // verlängern nach hinten, der größte Wert, der kleiner Null und ungleich quasi Null

                            if ((cutPlace[i] > param) && (cutPlace[i] < 0.0 - eps))
                            {
                                param = cutPlace[i];
                                k = i;
                            }
                        }
                        else
                        { // verlängern nach vorne, der kleinste Wert, der größer 1.0 und ungleich quasi 1.0
                            if ((cutPlace[i] < param) && (cutPlace[i] > 1.0 + eps))
                            {
                                param = cutPlace[i];
                                k = i;
                            }
                        }

                    }
                    if (k >= 0)  // was gefunden
                    {
                        newCurve = iCurve.Clone();
                        if (distToCurve.Length != 0.0)
                        {   // zusätzlich länger oder kürzer machen
                            ICurve targetCurve = sourceCurve;
                            if (targetCurves != null) targetCurve = targetCurves[k];
                            Plane pl;
                            if (Curves.GetCommonPlane(iCurve, targetCurve, out pl))
                            {   // muss der Fall sein
                                ICurve2D targetCurve2d = targetCurve.GetProjectedCurve(pl);
                                ICurve2D parl1 = targetCurve2d.Parallel(-distToCurve.Length, true, Precision.eps, 0.0);
                                ICurve2D parl2 = targetCurve2d.Parallel(distToCurve.Length, true, Precision.eps, 0.0);
                                ICurve2D newCurve2d = newCurve.GetProjectedCurve(pl);
                                // im 2d verlängern
                                if (lowerEnd)
                                    newCurve2d.StartPoint = newCurve2d.PointAt(param);
                                else
                                    newCurve2d.EndPoint = newCurve2d.PointAt(param);
                                if (lowerEnd)
                                {   // jetzt nur den Vorwärtsfall betrachten
                                    newCurve2d.Reverse();
                                }
                                // betrachte beide Parallelen, wir kennen die Richtungen nicht
                                GeoPoint2DWithParameter[] gp1 = newCurve2d.Intersect(parl1);
                                GeoPoint2DWithParameter[] gp2 = newCurve2d.Intersect(parl2);
                                double bestPar;
                                if (distToCurve.Length > 0)
                                {   // Kurve muss kürzer werden
                                    bestPar = 0.0;
                                    for (int i = 0; i < gp1.Length; i++)
                                    {
                                        if (gp1[i].par2 >= 0 && gp1[i].par2 <= 1 && gp1[i].par1 < 1 && gp1[i].par1 > bestPar)
                                        {
                                            bestPar = gp1[i].par1;
                                        }
                                    }
                                    for (int i = 0; i < gp2.Length; i++)
                                    {
                                        if (gp2[i].par2 >= 0 && gp2[i].par2 <= 1 && gp2[i].par1 < 1 && gp2[i].par1 > bestPar)
                                        {
                                            bestPar = gp2[i].par1;
                                        }
                                    }
                                }
                                else
                                {
                                    bestPar = double.MaxValue;
                                    for (int i = 0; i < gp1.Length; i++)
                                    {
                                        if (gp1[i].par2 >= 0 && gp1[i].par2 <= 1 && gp1[i].par1 > 1 && gp1[i].par1 < bestPar)
                                        {
                                            bestPar = gp1[i].par1;
                                        }
                                    }
                                    for (int i = 0; i < gp2.Length; i++)
                                    {
                                        if (gp2[i].par2 >= 0 && gp2[i].par2 <= 1 && gp2[i].par1 > 1 && gp2[i].par1 < bestPar)
                                        {
                                            bestPar = gp2[i].par1;
                                        }
                                    }
                                }
                                // den gefundenen Punkt auf die Kurve setzen
                                if (bestPar > 0 && bestPar < double.MaxValue)
                                {
                                    GeoPoint pp = pl.ToGlobal(newCurve2d.PointAt(bestPar));
                                    param = iCurve.PositionOf(pp); // param ist das eigentliche Ergebnis, welches bei onDone verwendet wird
                                    if (lowerEnd)
                                        newCurve.StartPoint = pp;
                                    else
                                        newCurve.EndPoint = pp;
                                }
                            }
                        }
                        else
                        {
                            if (lowerEnd)
                                newCurve.StartPoint = iCurve.PointAt(param);
                            else
                                newCurve.EndPoint = iCurve.PointAt(param);
                        }
                        newCurve.CopyAttributes(iCurve);
                        //Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
                        //if (newCurve is IColorDef)
                        //    (newCurve as IColorDef).ColorDef = new ColorDef("", backColor);
                        base.ActiveObject = newCurve;
                        base.FeedBack.AddSelected(newCurve);// letzte Linie einfügen
                        return true;
                    }
                }
            }
            param = 0.0;
            return false;
        }

        private bool ExpandObject(CurveInput sender, ICurve[] Curves, bool up)
        {	// ... nur die sinnvolen Kurven verwenden
            objectPoint = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                iCurve = Curves[0];
                return (showObject());
            }
            param = 0.0;
            base.FeedBack.ClearSelected();
            base.ActiveObject = null;
            return false;
        }

        private void ExpandObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve = SelectedCurve;
            showObject();
        }

        private bool ExpandSourceObject(CurveInput sender, ICurve[] Curves, bool up)
        {	// ... nur die sinnvolen Kurven verwenden
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                sourceCurve = Curves[0];
                return true;
            }
            else sourceCurve = null;
            return false;
        }

        private void ExpandSourceObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            sourceCurve = SelectedCurve;
        }

        public override void OnSetAction()
        {
            base.ActiveObject = null;
            base.TitleId = "ToolsExpand";
            CurveInput curveInput = new CurveInput("ToolsExpand.Object");
            curveInput.HitCursor = CursorTable.GetCursor("Expand.cur");
            curveInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(ExpandObject);
            curveInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(ExpandObjectChanged);
            curveInput.ModifiableOnly = true;
            expandSourceObject = new CurveInput("ToolsExpand.SourceObject");
            expandSourceObject.Optional = true;
            expandSourceObject.Decomposed = true;
            expandSourceObject.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(ExpandSourceObject);
            expandSourceObject.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(ExpandSourceObjectChanged);
            distToCurve = new LengthInput("ToolsExpand.Distance");
            distToCurve.Optional = true;
            distToCurve.SetLengthEvent += new LengthInput.SetLengthDelegate(setDistToCurve);
            distToCurve.DefaultLength = ConstrDefaults.DefaultExpandDist;

            base.SetInput(curveInput, expandSourceObject, distToCurve);
            base.OnSetAction();
            if (sourceCurve != null)
            {
                expandSourceObject.SetCurves(new ICurve[] { sourceCurveSave }, sourceCurveSave);
            }
            base.ShowActiveObject = false;
        }

        bool setDistToCurve(double Length)
        {
            return true;
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "ToolsExpand";
        }

        public override void OnDone()
        {
            if (base.ActiveObject != null)
            {
                base.ActiveObject = null;
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    if (lowerEnd) iCurve.StartPoint = iCurve.PointAt(param);
                    else iCurve.EndPoint = iCurve.PointAt(param);
                }
                sourceCurveSave = sourceCurve;
            }
            base.OnDone();
        }

    }
}
