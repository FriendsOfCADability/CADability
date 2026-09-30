using CADability.GeoObject;
using CADability.UserInterface;


namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ToolsConnect : ConstructAction
    {
        private GeoPoint objectPoint1;
        private GeoPoint objectPoint2;
        private ICurve iCurve1;
        private ICurve iCurve2; // hierhin wird verlängert, falls ausgewählt
                                //		private static ICurve sourceCurve;
        private ICurve newCurve;
        private CurveInput curve1Input;
        private CurveInput curve2Input;
        private double param1 = 0.0;
        private double param2 = 0.0;
        private double pos1;
        private double pos2;
        //		private static ICurve sourceCurveSave; // hierhin wird verlängert, falls ausgewählt und hier gemerkt


        public ToolsConnect()
        {

        }

        //public override void AutoRepeated()
        //{
        //    sourceCurve = sourceCurveSave; // sourceCurve auf den Instanzübergreifenden Wert gesetzt
        //}


        private bool showObject()
        {
            double[] cutPlace;
            base.FeedBack.ClearSelected();
            if ((iCurve2 != null) && !iCurve2.IsClosed && (iCurve1 != null) && !iCurve1.IsClosed && (iCurve1 != iCurve2))
            {
                //                Plane pl;
                //                if (Curves.GetCommonPlane(iCurve1, iCurve2, out pl))
                //                {
                //                    ICurve2D curve1_2D = iCurve1.GetProjectedCurve(pl); // die 2D-Kurven
                //                    ICurve2D curve2_2D = iCurve2.GetProjectedCurve(pl);
                //                    if (curve1_2D is Path2D) (curve1_2D as Path2D).Flatten();
                //                    if (curve2_2D is Path2D) (curve2_2D as Path2D).Flatten();
                //                    // hier die Schnittpunkte bestimmen und den ObjectPoint auf den nächsten Schnittpunt setzen
                //                    GeoPoint2DWithParameter[] intersectPoints = curve1_2D.Intersect(curve2_2D);
                //                    GeoPoint position = new GeoPoint(objectPoint1, objectPoint2);
                //                    double distS = double.MaxValue; // Entfernung des Pickpunkts zu den Schnittpunkten
                //                    for (int i = 0; i < intersectPoints.Length; ++i)
                //                    {
                ////                        double distLoc = Geometry.Dist(intersectPoints[i].p, pl.Project(position));
                //                        double distLoc = Geometry.Dist(intersectPoints[i].p, pl.Project(objectPoint2));
                //                        if (distLoc < distS)
                //                        {
                //                            distS = distLoc;
                //                            param1 = intersectPoints[i].par1;
                //                            param2 = intersectPoints[i].par2;
                //                        }
                //                    }
                //                }

                cutPlace = Curves.Intersect(iCurve1, iCurve2, false);
                GeoPoint position = new GeoPoint(objectPoint1, objectPoint2);
                if (cutPlace.Length > 0)
                {
                    pos1 = iCurve1.PositionOf(position);
                    //                    pos1 = iCurve1.PositionOf(objectPoint1);
                    double dist = double.MaxValue;
                    for (int i = 0; i < cutPlace.Length; ++i)
                    {
                        double distLoc = Geometry.Dist(position, iCurve1.PointAt(cutPlace[i]));
                        //                        double distLoc = Geometry.Dist(objectPoint1, iCurve1.PointAt(cutPlace[i]));
                        if (distLoc < dist)
                        {
                            //                            dist = Math.Abs(pos1 - cutPlace[i]);
                            dist = distLoc;
                            param1 = cutPlace[i];
                            //                            k1 = 2;
                        }

                    }

                }
                cutPlace = Curves.Intersect(iCurve2, iCurve1, false);
                if (cutPlace.Length > 0)
                {
                    pos2 = iCurve2.PositionOf(position);
                    //                    pos2 = iCurve2.PositionOf(objectPoint2);
                    double dist = double.MaxValue;
                    for (int i = 0; i < cutPlace.Length; ++i)
                    {
                        double distLoc = Geometry.Dist(position, iCurve2.PointAt(cutPlace[i]));
                        //                        double distLoc = Geometry.Dist(objectPoint2, iCurve2.PointAt(cutPlace[i]));
                        if (distLoc < dist)
                        {
                            dist = distLoc;
                            param2 = cutPlace[i];
                        }

                    }

                }
                if (!Precision.IsNull(param1) || !Precision.IsNull(param2))// was gefunden
                {
                    base.FeedBack.ClearSelected();
                    bool connect = false;
                    if ((param1 > (1 + 1e-8) || param1 < -1e-8))
                    {  //  also nur bei realer Verlängerung nach oben oder unten und offen
                        newCurve = iCurve1.Clone();
                        if (param1 < 0.5)
                            newCurve.StartPoint = iCurve1.PointAt(param1);
                        else
                            newCurve.EndPoint = iCurve1.PointAt(param1);
                        newCurve.CopyAttributes(iCurve1);
                        base.FeedBack.AddSelected(newCurve);// erste Linie einfügen
                        connect = true;
                    }
                    if ((param2 > (1 + 1e-8) || param2 < -1e-8))
                    { //  also nur bei realer Verlängerung nach oben oder unten und offen
                        newCurve = iCurve2.Clone();
                        if (param2 < 0.5)
                            newCurve.StartPoint = iCurve2.PointAt(param2);
                        else
                            newCurve.EndPoint = iCurve2.PointAt(param2);
                        newCurve.CopyAttributes(iCurve2);
                        base.FeedBack.AddSelected(newCurve);// letzte Linie einfügen
                        connect = true;
                    }
                    if (connect) return true;
                }
            }
            param1 = 0.0;
            param2 = 0.0;
            return false;
        }

        private bool ExpandObject1(CurveInput sender, ICurve[] Curves, bool up)
        {   // ... nur die sinnvolen Kurven verwenden
            objectPoint1 = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                iCurve1 = Curves[0];
                if (curve2Input.Fixed)
                    return (showObject());
                else return true;
            }
            else iCurve1 = null;
            base.FeedBack.ClearSelected();
            param1 = 0.0;
            param2 = 0.0;
            return false;
        }

        private void ExpandObject1Changed(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve1 = SelectedCurve;
            showObject();
        }

        private bool ExpandObject2(CurveInput sender, ICurve[] Curves, bool up)
        {	// ... nur die sinnvolen Kurven verwenden
            objectPoint2 = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                iCurve2 = Curves[0];
                if (curve1Input.Fixed)
                    return (showObject());
                else return true;
            }
            else iCurve2 = null;
            base.FeedBack.ClearSelected();
            param1 = 0.0;
            param2 = 0.0;
            return false;
        }

        private void ExpandObject2Changed(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve2 = SelectedCurve;
            showObject();
        }

        public override void OnSetAction()
        {
            base.ActiveObject = null;
            base.TitleId = "ToolsConnect";
            curve1Input = new CurveInput("ToolsConnect.Object1");
            curve1Input.HitCursor = CursorTable.GetCursor("Connect.cur");
            curve1Input.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(ExpandObject1);
            curve1Input.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(ExpandObject1Changed);
            curve1Input.ModifiableOnly = true;
            curve2Input = new CurveInput("ToolsConnect.Object2");
            curve2Input.HitCursor = CursorTable.GetCursor("Connect.cur");
            curve2Input.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(ExpandObject2);
            curve2Input.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(ExpandObject2Changed);
            curve2Input.ModifiableOnly = true;
            base.SetInput(curve1Input, curve2Input);
            base.OnSetAction();
        }

        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "ToolsConnect";
        }

        public override void OnDone()
        {
            if (!Precision.IsNull(param1) || !Precision.IsNull(param2))
            {
                using (base.Frame.Project.Undo.UndoFrame)
                { //  also nur bei realer Verlängerung nach oben oder unten und offen
                    if ((param1 > (1 + 1e-8) || param1 < -1e-8) && !iCurve1.IsClosed)
                    {
                        if (param1 < 0.5) iCurve1.StartPoint = iCurve1.PointAt(param1);
                        else iCurve1.EndPoint = iCurve1.PointAt(param1);
                    }

                    if ((param2 > (1 + 1e-8) || param2 < -1e-8) && !iCurve2.IsClosed)
                    {
                        if (param2 < 0.5) iCurve2.StartPoint = iCurve2.PointAt(param2);
                        else iCurve2.EndPoint = iCurve2.PointAt(param2);
                    }
                }
            }
            base.OnDone();
        }
    }
}
