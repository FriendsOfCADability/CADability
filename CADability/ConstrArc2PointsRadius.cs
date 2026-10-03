using CADability.GeoObject;
using System;
using System.Collections;

namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    /// 
    // die verschiedenen Möglichkeiten bei Tastatureingabe!

    internal class ConstrArc2PointsRadius : ConstructAction
    {

        private GeoPoint arcPoint1;
        private GeoPoint arcPoint2;
        private GeoPoint arcPoint3;
        private Ellipse arc;
        private double arcRad;
        private double arcRadSet;
        private GeoPointInput arcPoint1Input;
        private GeoPointInput arcPoint2Input;
        private LengthInput len;
        private LengthInput diamInput;
        private Boolean useRadius;
        private int selSol = 0;  // wg. Unsymmetrie bei 0 wg Abs

        public ConstrArc2PointsRadius()
        { }

        private bool showArc(GeoPoint p1)
        {   // zunächst Punkte sammeln, um unabhängig von der Erstellungsreihenfolge zu sein!
            ArrayList p = new ArrayList();
            if (arcPoint1Input.Fixed) p.Add(arcPoint1);
            if (arcPoint2Input.Fixed) p.Add(arcPoint2);
            switch (p.Count)
            {
                case 0:
                    {   // prototyp darstellen
                        p1.x = p1.x + arcRad;
                        arc.SetArcPlaneCenterRadius(base.ActiveDrawingPlane, p1, arcRad);
                        return true;
                    }
                case 1:
                    {   // Kreis aus 2 Punkten darstellen, damit man was sieht
                        if (Geometry.Dist((GeoPoint)p[0], p1) != 0)
                        {
                            if (len.Fixed) // der Radius ist schon eingetippt
                            {
                                //							base.MultiSolution = true;
                                base.MultiSolutionCount = 4;
                                // für den LocationPunkt, der die Auswahl der 2 möglichen Fälle definiert, brauche ich einen senkrechten punkt in einer Orientierung:
                                GeoVector locV = ((GeoPoint)p[0] - p1) ^ base.ActiveDrawingPlane.Normal;
                                // falls durch den 2. Punkt (p1) der Radius zu gross wird, hier anpassen
                                arcRad = Math.Max(arcRadSet, Geometry.Dist((GeoPoint)p[0], p1));
                                arc.SetArcPlane2PointsRadiusLocation(base.ActiveDrawingPlane, (GeoPoint)p[0], p1, arcRad, p1 + locV, Math.Abs(selSol));
                                return true;
                            }
                            else  // es gibt noch keine definierte Länge
                            {   // hilfsweise wird ein kreis aus 2 gegenüberliegenden Punkten angezeigt
                                try { arc.SetArcPlane2Points(base.ActiveDrawingPlane, (GeoPoint)p[0], p1); }
                                catch (EllipseException) { return false; }
                                return true;
                            }
                        }
                    }
                    break;
                case 2:
                    {   // hier der eigentliche Lösungsfall:
                        //					base.MultiSolution = true;
                        base.MultiSolutionCount = 4;
                        arc.SetArcPlane2PointsRadiusLocation(base.ActiveDrawingPlane, arcPoint1, arcPoint2, arcRad, p1, Math.Abs(selSol));
                        return true;
                    }
            }
            //			base.MultiSolution = false;
            base.MultiSolutionCount = 0;
            return false;
        }

        private void ArcPoint1(GeoPoint p)
        {
            arcPoint1 = p;
            showArc(arcPoint1);
        }

        private void ArcPoint2(GeoPoint p)
        {
            arcPoint2 = p;
            showArc(arcPoint2);
        }



        private double RadiusFromPoint(GeoPoint MousePosition)
        {   // hier wird die Länge (also der Radius) extern berechnet und zurückgeliefert
            // dazu wird hilfsweise ein Kreis aus drei Punkten berechnet und dessen Radius genommen
            // das hat nur Sinn, wenn die beiden Punkte schon bestimmt sind
            if (!(arcPoint1Input.Fixed & arcPoint2Input.Fixed)) return arcRad;
            arcPoint3 = MousePosition;
            double dist12 = Geometry.Dist(arcPoint1, arcPoint2); // sinnvoller Wert für exception
            try
            {
                arc.SetArc3Points(arcPoint1, arcPoint3, arcPoint2, base.ActiveDrawingPlane);
            }
            catch (EllipseException) { return dist12 / 2.0; }
            // jetzt den radius des Kreises aus SetArc3Points zurückliefern
            return arc.MajorRadius;
        }

        private bool ArcRadius(double length)
        {
            arcPoint3 = base.CurrentMousePosition;
            if (length > Precision.eps)
            {
                arcRad = length;
                arcRadSet = length;
                return (showArc(arcPoint3));
            }
            return false;
        }

        private double GetRadius()
        {
            return arcRad;
        }


        private bool SetCircleDiameter(double diam)
        {
            arcPoint3 = base.CurrentMousePosition;
            if (diam > Precision.eps)
            {
                arcRad = diam / 2.0;
                arcRadSet = arcRad;
                return (showArc(arcPoint3));
            }
            return false;
        }

        private double CalculateDiameter(GeoPoint MousePosition)
        {
            // hier wird die Länge (also der Radius) extern berechnet und zurückgeliefert
            // dazu wird hilfsweise ein Kreis aus drei Punkten berechnet und dessen Radius genommen
            // das hat nur Sinn, wenn die beiden Punkte schon bestimmt sind
            if (!(arcPoint1Input.Fixed & arcPoint2Input.Fixed)) return arcRad * 2.0;

            arcPoint3 = MousePosition;
            double dist12 = Geometry.Dist(arcPoint1, arcPoint2); // sinnvoller Wert für exception
            try
            {
                arc.SetArc3Points(arcPoint1, arcPoint3, arcPoint2, base.ActiveDrawingPlane);
            }
            catch (EllipseException) { return dist12; }
            // jetzt den radius des Kreises aus SetArc3Points zurückliefern
            return arc.MajorRadius * 2.0;
        }

        private double GetCircleDiameter()
        {
            return arcRad * 2.0;
        }



        private GeoPoint ArcCenter()
        {
            return arc.Center;
        }

        //public override void OnDifferentSolution(bool next)
        //{
        //    if (next) selSol += 1;
        //    else selSol -= 1;
        //    showArc(arcPoint3);
        //}

        public override void OnSolution(int sol)
        {
            if (sol == -1) selSol += 1;
            else
                if (sol == -2) selSol -= 1;
            else selSol = sol;
            showArc(arcPoint3);
        }

        protected override void InputChanged(object activeInput)
        {
            if (activeInput == diamInput)
            {
                len.Optional = true;
                diamInput.Optional = false;
            };
            if (activeInput == len)
            {
                len.Optional = false;
                diamInput.Optional = true;
            };
        }


        public override void OnSetAction()
        {
            useRadius = Frame.GetBooleanSetting("Formatting.Radius", true);

            arc = Ellipse.Construct();
            arcRad = ConstrDefaults.DefaultArcRadius;
            arc.SetArcPlaneCenterRadiusAngles(base.ActiveDrawingPlane, new GeoPoint(0.0, 0.0, 0.0), arcRad, 0.0, 1.5 * Math.PI);

            base.ActiveObject = arc;
            base.TitleId = "Constr.Arc.2PointsRadius";

            arcPoint1Input = new GeoPointInput("Constr.Arc.Point1");
            arcPoint1Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(ArcPoint1);
            arcPoint2Input = new GeoPointInput("Constr.Arc.Point2");
            arcPoint2Input.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(ArcPoint2);

            len = new LengthInput("Constr.Arc.Radius");
            len.DefaultLength = ConstrDefaults.DefaultArcRadius;
            len.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(ArcRadius);
            len.GetLengthEvent += new CADability.Actions.ConstructAction.LengthInput.GetLengthDelegate(GetRadius);
            if (!useRadius) { len.Optional = true; }
            // hier wird der Radius extern berechnet und zurückgeliefert
            len.CalculateLengthEvent += new CADability.Actions.ConstructAction.LengthInput.CalculateLengthDelegate(RadiusFromPoint);
            len.ForwardMouseInputTo = new object[] { arcPoint1Input, arcPoint2Input };

            diamInput = new LengthInput("Constr.Circle.Diameter");
            diamInput.SetLengthEvent += new ConstructAction.LengthInput.SetLengthDelegate(SetCircleDiameter);
            diamInput.GetLengthEvent += new ConstructAction.LengthInput.GetLengthDelegate(GetCircleDiameter);
            diamInput.CalculateLengthEvent += new LengthInput.CalculateLengthDelegate(CalculateDiameter);
            if (useRadius) { diamInput.Optional = true; }
            diamInput.DefaultLength = ConstrDefaults.DefaultArcDiameter;
            diamInput.ForwardMouseInputTo = new object[] { arcPoint1Input, arcPoint2Input };

            GeoPointInput arcCenter = new GeoPointInput("Constr.Arc.Center");
            arcCenter.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(ArcCenter);
            arcCenter.Optional = true;
            arcCenter.ReadOnly = true;

            if (useRadius)
            { base.SetInput(arcPoint1Input, arcPoint2Input, len, diamInput, arcCenter); }
            else
            { base.SetInput(arcPoint1Input, arcPoint2Input, diamInput, len, arcCenter); }


            base.ShowAttributes = true;

            base.OnSetAction();
        }
        public override string GetID()
        { return "Constr.Arc.2PointsRadius"; }

        public override void OnDone()
        { base.OnDone(); }

    }
}
