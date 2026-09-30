using CADability.Attribute;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
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
    internal class ToolsTrimSplit : ConstructAction // aufteilen
    {
        private GeoPoint objectPoint; // der Pickpunkt der Aufteil-Curve
        private ICurve iCurve; // lokales Element
        private ICurve[] trimCurves; // die aufgeteilten Kurven
        private ICurve trimCurve; // hier wird die Trimmkurve gesammelt
        private IGeoObjectOwner owner;


        public ToolsTrimSplit()
        { }


        private bool showObject()
        {	// jetzt werden die Schnittparameter bestimmt
            base.FeedBack.ClearSelected();
            base.ActiveObject = null;
            double[] cutPlace = base.Frame.ActiveView.ProjectedModel.GetIntersectionParameters(iCurve, ProjectedModel.IntersectionMode.InsideAndSelfIntersection);
            owner = iCurve.Owner; // owner merken für löschen und Einfügen

            double pos = iCurve.PositionOf(objectPoint); // der Pickpunkt
            trimCurves = null;
            bool realCut;
            if (cutPlace.Length > 0) // es gibt mindestens einen Schnitt
            {
                realCut = false;
                for (int i = 0; i < cutPlace.Length; ++i)
                {
                    if (!((Math.Abs(cutPlace[i]) < 1e-8) || (Math.Abs(cutPlace[i] - 1) < 1e-8)))
                    {
                        realCut = true;
                        break;
                    }
                }
                if (realCut)
                {

                    int k = -1;
                    for (int i = 0; i < cutPlace.Length; ++i)
                    {
                        if (cutPlace[i] > pos && cutPlace[i] > 0.0 && cutPlace[i] < 1.0)
                        {	// pos ist oft -0.01 oder 1.01, da am Anfang oder Ende gepickt
                            k = i; // Schnittstelle "oberhalb" gefunden
                            break;
                        }
                    }
                    ICurve[] splitedCurves;
                    if (iCurve.IsClosed)
                    {
                        if (k > 0) // zwei Schnittpunkte oberhalb von 0.0 = 0 Grad des Kreises/Rechtecks
                        {
                            splitedCurves = iCurve.Split(cutPlace[k - 1], cutPlace[k]);
                            if (splitedCurves.Length >= 2)
                            {
                                trimCurves = splitedCurves; // nur den einen nehmen, da eindeutig sortiert
                                trimCurve = splitedCurves[0]; // nur den einen nehmen, da eindeutig sortiert
                            }
                        }
                        else
                        {
                            if (cutPlace.Length > 1) // erster und letzter Schnittpunkt, 0 Grad ist eingeschlossen
                            {
                                splitedCurves = iCurve.Split(cutPlace[0], cutPlace[cutPlace.Length - 1]);
                                if (splitedCurves.Length >= 2)
                                {
                                    trimCurves = splitedCurves; // nur den einen nehmen, da eindeutig sortiert
                                    trimCurve = splitedCurves[1]; // nur den einen nehmen, da eindeutig sortiert
                                }
                            }

                        }
                    }
                    else
                    {
                        // jetzt die offenen Objekte:
                        if (k <= 0)  // trimmen am Ende 
                        {
                            if (k == -1)
                            {	//  Pickpunkt am oberen Ende
                                splitedCurves = iCurve.Split(cutPlace[cutPlace.Length - 1]);
                                if (splitedCurves.Length >= 2)
                                {
                                    trimCurves = splitedCurves;
                                    // nur den einen nehmen, da eindeutig sortiert
                                    trimCurve = splitedCurves[1];
                                }
                            }
                            if (k == 0)
                            {	//  Pickpunkt am unteren Ende
                                splitedCurves = iCurve.Split(cutPlace[0]);
                                if (splitedCurves.Length >= 2)
                                {
                                    trimCurves = splitedCurves;
                                    // nur den einen nehmen, da eindeutig sortiert
                                    trimCurve = splitedCurves[0];
                                }
                            }
                        }
                        else  // trimmen mit mehreren Schnittpunkten, Pickpunkt mittendrinn
                        {
                            /*					trimCurves = new ICurve[3];
                                                splitedCurves = iCurve.Split(cutPlace[k-1]);
                                                trimCurves[0] = splitedCurves[0]; // untere Linie
                                                splitedCurves = iCurve.Split(cutPlace[k]);
                                                trimCurves[1] = splitedCurves[1]; // obere Linie
                                                ICurve curveTmp = iCurve.Clone();
                                                curveTmp.Trim(cutPlace[k-1],cutPlace[k]); // mittlere Linie
                                                trimCurves[2] = curveTmp;
                            */
                            trimCurve = iCurve.Clone(); // zunächst Kopie der Kurve
                            // tempräres Array zum Sammeln der Kurven
                            ArrayList tmpArray = new ArrayList();
                            splitedCurves = iCurve.Split(cutPlace[k - 1]); // Schnittpunkt unterhalb
                            if (splitedCurves.Length >= 2)
                            {	// den unteren Abschnitt sammeln
                                tmpArray.Add(splitedCurves[0]);
                                trimCurve = splitedCurves[1]; // Kurvenkopie unten abgeschnitten
                            }
                            // auf der (evtl) unten abgeschnittenen Kopie weitermachen
                            splitedCurves = trimCurve.Split(trimCurve.PositionOf(iCurve.PointAt(cutPlace[k]))); // Schnittpunkt oberhalb
                            if (splitedCurves.Length >= 2)
                            {
                                tmpArray.Add(splitedCurves[0]);
                                tmpArray.Add(splitedCurves[1]);
                                trimCurve = splitedCurves[0]; // Kurvenkopie auch noch oben abgeschnitten
                            }
                            else tmpArray.Add(trimCurve); // kein echter Schnittpunkt, dann das Reststück zufügen
                            trimCurves = (ICurve[])tmpArray.ToArray(typeof(ICurve)); // reTyping nach Sammeln  
                        }
                    }
                }
            }
            if (trimCurves != null)
            {
                trimCurve.CopyAttributes(iCurve);
                Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
                if (trimCurve is IColorDef)
                    (trimCurve as IColorDef).ColorDef = new ColorDef("", backColor);
                base.FeedBack.AddSelected(trimCurve); // darstellen
                base.ActiveObject = trimCurve; // merken
                return true;
            }
            return false;
        }

        private bool TrimObject(CurveInput sender, ICurve[] Curves, bool up)
        {   // ... nur die sinnvolen Kurven verwenden
            objectPoint = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                iCurve = Curves[0];
                return (showObject());
            }
            base.FeedBack.ClearSelected();
            base.ActiveObject = null;
            return false;
        }

        private void TrimObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve = SelectedCurve;
            showObject();
        }


        public override void OnSetAction()
        {
            base.ActiveObject = null;
            base.TitleId = "ToolsTrimSplit";
            CurveInput curveInput = new CurveInput("ToolsTrimSplit.Object");
            curveInput.HitCursor = CursorTable.GetCursor("TrimSplit.cur");
            curveInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(TrimObject);
            curveInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(TrimObjectChanged);
            curveInput.ModifiableOnly = true;
            base.SetInput(curveInput);
            base.ShowActiveObject = false;
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "ToolsTrimSplit";
        }

        public override void OnDone()
        {
            if (base.ActiveObject != null)
            {
                base.ActiveObject = null;
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    owner.Remove(iCurve); // original Löschen
                    for (int i = 0; i < trimCurves.Length; ++i)// alle einfügen
                    {
                        trimCurves[i].CopyAttributes(iCurve);
                        owner.Add(trimCurves[i]);
                    }
                }
            }
            base.OnDone();
        }

    }
}

