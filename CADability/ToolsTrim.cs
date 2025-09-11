using CADability.Attribute;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
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
    internal class ToolsTrim : ConstructAction
    {
        private GeoPoint objectPoint; // der Pickpunkt der Aufteil-Curve
        private ICurve iCurve; // lokales Element
        private ICurve sourceCurve; // hieran wird getrimmt, falls ausgewählt
        private static ICurve sourceCurveSave; // hieran wird getrimmt, falls ausgewählt und hier gemerkt
        private ICurve trimCurve; // hier wird die Trimmkurve gesammelt
        private CurveInput trimObject;
        private double param1, param2; // die globalen Parameter, wo geschlossene Ellipsen getrimmt werden
        private IGeoObjectOwner owner;
        private bool deleteObject;



        public ToolsTrim()
        { }

        public override void AutoRepeated()
        {
            sourceCurve = sourceCurveSave; // sourceCurve auf den Instanzübergreifenden Wert gesetzt
        }

        private bool showObject()
        {	// jetzt werden die Schnittparameter bestimmt
            base.ActiveObject = null;
            base.FeedBack.ClearSelected();
            double[] cutPlace;
            owner = (iCurve as IGeoObject).Owner; // owner merken für löschen und Einfügen
            trimCurve = null;
            param1 = 0.0;
            param2 = 1.0;

            //			if (trimObject.Fixed)
            if (sourceCurve != null)
            {
                cutPlace = Curves.Intersect(iCurve, sourceCurve, true);
                Array.Sort(cutPlace);
            }
            else cutPlace = base.Frame.ActiveView.ProjectedModel.GetIntersectionParameters(iCurve, ProjectedModel.IntersectionMode.InsideAndSelfIntersection);
            // von Gerhard: cutPlace hat alle Schnittpositionen, aufsteigend sortiert
            // ein Problem taucht auf, wenn das Objekt an Anfangspunkt oder Endpunkt
            // genau einen Schnitt hat, dann enthält cutPlace 0.0 oder 1.0. Sind das dann die
            // einzigen Schnittpunkte, dann soll das Objekt gelöscht werden können:
            //   ein Schnittpunkt			sehr nahe bei Null         oder   sehr nahe bei eins 
            deleteObject = true;
            for (int i = 0; i < cutPlace.Length; i++)
            { // also: nicht entweder fast Null oder fast eins
                if (!((Math.Abs(cutPlace[i]) < 1e-8) || (Math.Abs(cutPlace[i] - 1) < 1e-8)))
                {
                    deleteObject = false;
                    break;
                }

            }

            //if ((cutPlace.Length == 1) && ((Math.Abs(cutPlace[0])<1e-10) || (Math.Abs(cutPlace[0]-1) <1e-10))
            //    || 
            //    // oder 2 Schnittpunkte  und  erster sehr nahe bei Null  und zweiter sehr nahe bei eins
            //    ((cutPlace.Length == 2) && ((Math.Abs(cutPlace[0])<1e-10) && (Math.Abs(cutPlace[1]-1) <1e-10)))
            //    ||
            //        ((cutPlace.Length == 0) && (sourceCurve == null))) // also: einzeln stehendes Element ohne Schnittpunkte
            if (deleteObject)
            {
                trimCurve = iCurve.Clone();
                //				deleteObject = true;
            }
            else
            {
                // von Gerhard: cutPlace hat alle Schnittpositionen, aufsteigend sortiert
                // ein Problem taucht auf, wenn das Objekt an Anfangspunkt oder Endpunkt
                // genau einen Schnitt hat, dann enthält cutPlace 0.0 oder 1.0. Das tritt oft
                // auf, z.B. wenn man eine Linie zweimal trimmt. Also alles was <=0.0 und =>1.0 ist
                // muss raus:
                if (iCurve.IsClosed)
                {
                    // hier evtl. Schnittpunkte bei 0.0 auf 1.0 verdoppeln und umgekehrt, da man nicht weiss, welche Seite man wegtrimmen will
                    // es funktioniert aber auch so, warum auch immer
                    List<double> lst = new List<double>(cutPlace);
                    if (cutPlace[cutPlace.Length - 1] >= 1 - 1e-10) lst.Insert(0, 0.0); // wenn 1.0 ein Schnittpunkt ist, dann im geschlossenen Fall auch 0.0
                    if (cutPlace[0] <= 1e-10) lst.Add(1.0);
                    cutPlace = lst.ToArray();
                }
                else
                {
                    ArrayList tmp = new ArrayList(cutPlace.Length);
                    for (int i = 0; i < cutPlace.Length; ++i)
                    {
                        if (cutPlace[i] > 1e-10 && cutPlace[i] < 1 - 1e-10) tmp.Add(cutPlace[i]);
                    }
                    cutPlace = (double[])tmp.ToArray(typeof(double));
                }
                double pos = iCurve.PositionOf(objectPoint);
                if (cutPlace.Length > 0) // es gibt mindestens einen Schnittpunkt, Schnittpunkte sind sortiert!!
                {
                    int k = -1;
                    for (int i = 0; i < cutPlace.Length; ++i)
                    {
                        if (cutPlace[i] > pos && ((cutPlace[i] > 0.0 && cutPlace[i] < 1.0) || iCurve.IsClosed))
                        {   // pos ist oft -0.01 oder 1.01, da am Anfang oder Ende gepickt
                            k = i; // Schnittstelle "oberhalb" gefunden
                            break;
                        }
                    }
                    ICurve[] splitedCurves;
                    if (iCurve.IsClosed)
                    {
                        if (k > 0) // zwei Schnittpunkte oberhalb von 0.0 = 0 Grad des Kreises
                        {   // dieser Split gilt nur für geschlossenes:
                            splitedCurves = iCurve.Split(cutPlace[k - 1], cutPlace[k]);
                            if (splitedCurves.Length >= 2)
                            {
                                param1 = cutPlace[k - 1]; // merken für onDone
                                param2 = cutPlace[k]; // merken für onDone
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
                                    param1 = cutPlace[cutPlace.Length - 1];  // merken für onDone
                                    param2 = cutPlace[0];  // merken für onDone
                                    trimCurve = splitedCurves[1]; // nur den einen nehmen, da eindeutig sortiert
                                }
                            }

                        }
                    }
                    else    // jetzt die offenen Objekte:
                    {
                        if (k <= 0)  // trimmen am Ende 
                        {
                            if (k == -1)
                            {   //  Pickpunkt am oberen Ende
                                splitedCurves = iCurve.Split(cutPlace[cutPlace.Length - 1]);
                                if (splitedCurves.Length >= 2)
                                {
                                    param2 = cutPlace[cutPlace.Length - 1];  // trimm(0.0,param2) merken für onDone
                                    trimCurve = splitedCurves[1];
                                }
                            }
                            if (k == 0)
                            {   //  Pickpunkt am unteren Ende
                                splitedCurves = iCurve.Split(cutPlace[0]);
                                if (splitedCurves.Length >= 2)
                                {
                                    param1 = cutPlace[0];  // trimm(param1,1.0), merken für onDone
                                    trimCurve = splitedCurves[0];
                                }
                            }
                        }
                        else  // trimmen mit mehreren Schnittpunkten, Pickpunkt mittendrinn
                        {
                            trimCurve = iCurve.Clone(); // zunächst Kopie der Kurve
                            splitedCurves = iCurve.Split(cutPlace[k - 1]); // Schnittpunkt unterhalb
                            if (splitedCurves.Length >= 2)
                            {
                                param1 = cutPlace[k - 1];  // merken für onDone
                                trimCurve = splitedCurves[1]; // Kurvenkopie unten abgeschnitten
                            }
                            else param1 = cutPlace[k];  // merken für onDone
                            splitedCurves = trimCurve.Split(trimCurve.PositionOf(iCurve.PointAt(cutPlace[k]))); // Schnittpunkt oberhalb
                            if (splitedCurves.Length >= 2)
                            {
                                param2 = cutPlace[k];  // merken für onDone
                                trimCurve = splitedCurves[0]; // Kurvenkopie auch noch oben abgeschnitten
                            }
                            else param2 = cutPlace[k - 1];  // merken für onDone

                        }
                    }
                }
            }
            if (trimCurve != null)
            {
                (trimCurve as IGeoObject).CopyAttributes(iCurve as IGeoObject);
                Color backColor = base.Frame.GetColorSetting("Colors.Feedback", Color.DarkGray);
                if (trimCurve is IColorDef)
                    (trimCurve as IColorDef).ColorDef = new ColorDef("", backColor);
                base.FeedBack.AddSelected(trimCurve as IGeoObject); // darstellen
                base.ActiveObject = trimCurve as IGeoObject; // merken
                return true;
            }
            return false;
        }

        private bool TrimObject(CurveInput sender, ICurve[] Curves, bool up)
        {
            objectPoint = base.CurrentMousePosition;
            if (up)
                if (Curves.Length == 0) sender.SetCurves(Curves, null); // ...die werden jetzt im ControlCenter dargestellt (nur bei up)
                else sender.SetCurves(Curves, Curves[0]);
            if (Curves.Length > 0)
            {
                iCurve = Curves[0];
                return (showObject());
            }
            base.ActiveObject = null;
            base.FeedBack.ClearSelected();
            return false;
        }

        private void TrimObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            iCurve = SelectedCurve;
            showObject();
        }

        private bool TrimSourceObject(CurveInput sender, ICurve[] Curves, bool up)
        {   // ... nur die sinnvolen Kurven verwenden
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

        private void TrimSourceObjectChanged(CurveInput sender, ICurve SelectedCurve)
        {
            sourceCurve = SelectedCurve;
        }

        public override void OnSetAction()
        {
            base.ActiveObject = null;
            base.TitleId = "ToolsTrim";
            trimObject = new CurveInput("ToolsTrim.SourceObject");
            trimObject.Optional = true;
            trimObject.ModifiableOnly = true;
            trimObject.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(TrimSourceObject);
            trimObject.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(TrimSourceObjectChanged);
            CurveInput curveInput = new CurveInput("ToolsTrim.Object");
            curveInput.ModifiableOnly = true;
            curveInput.HitCursor = CursorTable.GetCursor("Trim.cur");
            curveInput.MouseOverCurvesEvent += new CurveInput.MouseOverCurvesDelegate(TrimObject);
            curveInput.CurveSelectionChangedEvent += new CurveInput.CurveSelectionChangedDelegate(TrimObjectChanged);
            curveInput.PreferPath = true; // Pfade werden bevorzugt, d.h. keine Teilobjekte von Pfaden liefern
            curveInput.ModifiableOnly = true;
            base.SetInput(curveInput, trimObject);
            base.ShowActiveObject = false;
            base.OnSetAction();
            if (sourceCurve != null)
            {
                trimObject.SetCurves(new ICurve[] { sourceCurveSave }, sourceCurveSave);
            }
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
        }

        public override string GetID()
        {
            return "ToolsTrim";
        }

        public override void OnDone()
        {
            base.ActiveObject = null;
            if (trimCurve != null) // nur dann gibts was zu tun!
            {
                using (base.Frame.Project.Undo.UndoFrame)
                {
                    if (deleteObject)
                    {
                        owner.Remove(iCurve as IGeoObject); // original Löschen
                    }
                    else
                    {

                        if (iCurve.IsClosed) // geschlosene Kurve, also z.B. Kreis
                        {
                            iCurve.Trim(param2, param1); // an zwei Parametern geschnitten
                        }
                        else
                        {
                            //                            if (!Precision.IsEqual(trimCurve.StartPoint, iCurve.StartPoint) && !Precision.IsEqual(trimCurve.EndPoint, iCurve.EndPoint))
                            if ((param1 > 1e-8) && (param2 < (1.0 - 1e-8)))
                            { // mittendrinn´da parameter beide innerhalb der Kurve! 
                                ICurve trimCurveTemp;
                                trimCurveTemp = iCurve.Clone();
                                iCurve.Trim(0.0, param1); // Kurve auf das untere Stück verkürzt
                                (trimCurveTemp as IGeoObject).CopyAttributes(iCurve as IGeoObject);
                                trimCurveTemp.Trim(param2, 1.0); // Kopie als oberes Stück
                                owner.Add(trimCurveTemp as IGeoObject);
                            }
                            else
                            {
                                if (Precision.IsEqual(trimCurve.StartPoint, iCurve.StartPoint)) // der Anfang muss weg
                                    iCurve.Trim(param1, 1.0);
                                if (Precision.IsEqual(trimCurve.EndPoint, iCurve.EndPoint))  // das Ende muss weg
                                    iCurve.Trim(0.0, param2);
                            }
                        }
                    }
                }
            }
            sourceCurveSave = sourceCurve;
            base.OnDone();
        }
    }
}
