using CADability.Attribute;
using CADability.Curve2D;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Globalization;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability.GeoObject
{

    public interface IDimensionStyle
    {
        // für DimensionStyleSelectionProperty, damit die weiß, wie man den DimensionStyle verändert
        DimensionStyle DimensionStyle
        {
            get;
            set;
        }
    }

    /// <summary>
    /// The dimension object represents a dimensioning in a model and is a <see href="a2f7e3d8-5886-48a6-b0a2-a896dcce8c4a.htm">GeoObject</see>.
    /// To use a dimension object you must at least set the properties <see cref="DimensionStyle"/>, <see cref="DimLineRef"/>,
    /// <see cref="DimLineDirection"/>, and add two points with the <see cref="AddPoint"/> method. Example:
    /// <code>
    /// // assuming "project" is a reference to a Project
    /// Dimension d = Dimension.Construct();
    /// d.DimensionStyle = project.DimensionStyleList.Current;
    /// d.DimLineRef = new GeoPoint(200, 200, 0);
    /// d.DimLineDirection = GeoVector.XAxis;
    /// d.AddPoint(new GeoPoint(100, 100, 0));
    /// d.AddPoint(new GeoPoint(200, 100, 0));
    /// d.AddPoint(new GeoPoint(300, 100, 0));
    /// project.GetActiveModel().Add(d);
    /// </code>
    /// </summary>
    [Serializable()]
    public class Dimension : IGeoObjectImpl, ISerializable, IDimensionStyle, IGeoObjectOwner
    {
        new private class Changing : IGeoObjectImpl.Changing
        {
            public Changing(Dimension dimension)
                : base(dimension)
            {
                // dimension.projectionData.Clear();
            }
            public Changing(Dimension dimension, bool UndoNecessary)
                : base(dimension, UndoNecessary)
            {
                // dimension.projectionData.Clear();
            }
            public Changing(Dimension dimension, string PropertyName)
                : base(dimension, PropertyName)
            {
                // dimension.projectionData.Clear();
            }
            public Changing(Dimension dimension, string MethodOrPropertyName, params object[] Parameters)
                : base(dimension, MethodOrPropertyName, Parameters)
            {
                // dimension.projectionData.Clear();
            }
        }
        // private Hashtable projectionData;
        /// <summary>
        /// Different types of dimensioning
        /// </summary>
        public enum EDimType
        {
            /// <summary>
            /// Dimensioning the distance of two or more points in any angle
            /// </summary>
            DimPoints,
            /// <summary>
            /// Dimensioning a coordinate position
            /// </summary>
            DimCoord,
            /// <summary>
            /// Dimensioning an angle
            /// </summary>
            DimAngle,
            /// <summary>
            /// Dimensioning a radius (of a circle or arc)
            /// </summary>
            DimRadius,
            /// <summary>
            /// Dimensioning a diameter (of a circle or arc)
            /// </summary>
            DimDiameter,
            /// <summary>
            /// Tagging a location
            /// </summary>
            DimLocation,
            DimAll
        };
        // zunächst aus CONDOR 4 übernommen, die essentiellen Daten:
        private GeoPoint[] points; // die vermaßten Punkte, bei Winkel/Durchmesser/Radiusbemaßung: Mittelpunkt
        private DimensionStyle dimStyle; // der Bemaßungstyp
        private EDimType dimType; // welcher Art von Bemaßung (Punkt, Radius, Winkel...)
        private GeoPoint dimLineRef; // Bezugspunkt durch den die Maßlinie geht
        /// <summary>
        /// Richtung der Maßlinie
        /// </summary>
        private GeoVector dimLineDirection; // Richtung der Maßlinie
        private Angle extLineAngle; // Winkel der Hilfslinien, gewöhnlich pi/2
        private Angle startAngle, endAngle; // für Winkelbemaßung (...Angle zugefügt)
        private double radius; // für Winkel/Durchmesser/Radiusbemaßung
        private double distToStart, distToEnd; // für Winkelbemaßung
        // die folgenden sind pro Abschnitt in points, von links nach rechts sortiert
        // bei Punkt- und Koordinatenbemaßung können es mehrere sein
        private double[] textPos; // 0.0 ist links, 0.5 ist mitte 1.0 ist rechts
        private string[] dimText; // überschreibt den Text der Bemaßung
        private string[] tolPlusText; // überschreibt den hochgestellten Text
        private string[] tolMinusText; // überschreibt den tiefgestellten Text
        private string[] prefix; // überschreibt den gegeben Prefix
        private string[] postfix; // überschreibt den gegeben Postfix
        private string[] postfixAlt; // überschreibt den gegeben PostfixAlt
        private GeoVector normal; // Normalenrichtung der Ebene, in der sich die Bemaßung befindet

        private Plane plane; // die Ebene, in der alle stattfindet, rekonstruierbar
        private BoundingCube extent = BoundingCube.EmptyBoundingCube;

        /// <summary>
        /// Die folgenden Daten für den Fall, dass der Text editiert wird
        /// </summary>
        private Text editingText; // einer der Bemaßungstexte festgehalten für den Editor, null, wenn nicht editierend
        private int editingTextIndex; // für welchen Text Index wird gerade editiert
        public enum EditingMode { dontEdit, editDimText, editTolPlusText, editTolMinusText, editPrefix, editPostfix, editPosfixAlt }
        private EditingMode editingMode;
        #region polymorph construction
        public delegate Dimension ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Dimension Construct()
        {
            if (Constructor != null) return Constructor();
            return new Dimension();
        }
        #endregion
        protected Dimension()
        {
            dimType = EDimType.DimPoints;
            points = new GeoPoint[0];
            // projectionData = new Hashtable();
            plane = Plane.XYPlane;
            normal = GeoVector.ZAxis; // Vorbesetung für die NormalenRichtung
        }
        #region Ausführung der Bemaßung
        private void AddDimensionArc(GeoObjectList List, GeoPoint2D center, double radius, Angle start, SweepAngle sweep)
        {	// Maßlinie als Bogen (Winkelbemaßung)
            if (sweep.Radian < 0) sweep += SweepAngle.Full;
            Arc2D a2d = new Arc2D(center, radius, start, sweep);
            IGeoObject go = a2d.MakeGeoObject(plane);
            (go as IColorDef).ColorDef = dimStyle.DimLineColor;
            (go as ILineWidth).LineWidth = dimStyle.DimLineWidth;
            // go.UserData.Add("Condor.Dimension.DimensionLine",index); // nicht für Bögen, oder?
            List.Add(go);
        }
        private void AddDimensionLine(GeoObjectList List, GeoPoint2D p1, GeoPoint2D p2, int index)
        {	// Maßlinie
            if (!dimStyle.DimNoDimLine)
            {
                Line l = Line.Construct();
                l.StartPoint = plane.ToGlobal(p1);
                l.EndPoint = plane.ToGlobal(p2);
                l.UserData.Add("CADability.Dimension.DimensionLine", index);
                l.ColorDef = dimStyle.DimLineColor;
                l.LineWidth = dimStyle.DimLineWidth;
                List.Add(l);
            }
        }
        private void AddTextRect(GeoObjectList List, Polyline2D pl, int index)
        {	// eine offene 2D PolyLine kommt rein
            Polyline go = (Polyline)pl.MakeGeoObject(plane);
            go.IsClosed = true; // hier schließen
            go.UserData.Add("CADability.Dimension.TextRect", index);
            go.ColorDef = dimStyle.DimLineColor;
            go.LineWidth = dimStyle.DimLineWidth;
            List.Add(go);
        }
        private void AddGuideLine(GeoObjectList List, GeoPoint2D p1, GeoPoint2D p2)
        {	// Maß-Hilfslinie, vom Bemaßungspunkt auf die Maßlinie
            // davon ausgehen, dass p1 der Objektpunkt ist und p2 der Punkt an der Maßlinie
            if (Precision.IsEqual(p1, p2)) return;
            GeoVector2D dir = p2 - p1; // ist nicht Nullvektor
            dir.Norm();
            Line l = Line.Construct();
            if (dimStyle.DimNoExtLine)
            {
                if (dimStyle.ExtLineExtension != 0.0)
                {	// nur Stückchen mit dimStyle.ExtLineExtension
                    l.StartPoint = plane.ToGlobal(p2 - dimStyle.ExtLineExtension * dir);
                    l.EndPoint = plane.ToGlobal(p2 + dimStyle.ExtLineExtension * dir);
                }
                else
                {
                    return; // keine Hilfslinien
                }
            }
            else
            {
                l.StartPoint = plane.ToGlobal(p1 + dimStyle.ExtLineOffset * dir);
                l.EndPoint = plane.ToGlobal(p2 + dimStyle.ExtLineExtension * dir);
            }
            l.ColorDef = dimStyle.ExtLineColor;
            l.LineWidth = dimStyle.ExtLineWidth;
            List.Add(l);
        }
        static double ArrowAngle = 9.462 * 2.0 / 180.0 * Math.PI; // gemessen in einer DXF Zeichnung
        private void AddOpenArrow(GeoObjectList List, GeoPoint2D pos, Angle dir, double layoutFactor, bool outside)
        {
            double w = dimStyle.SymbolSize * layoutFactor; // echte Breite des Pfeiles
            GeoVector2D vdir = new GeoVector2D(dir);
            double d = w / Math.Tan(ArrowAngle / 2) / 2; // Länge des Pfeils
            GeoPoint2D a1 = pos + d * vdir + (w / 2.0) * (vdir.ToLeft());
            GeoPoint2D a2 = pos + d * vdir + (w / 2.0) * (vdir.ToRight());
            Polyline2D p2d = new Polyline2D(new GeoPoint2D[] { a1, pos, a2 });
            IGeoObject go = p2d.MakeGeoObject(this.plane);
            (go as IColorDef).ColorDef = dimStyle.FillColor;
            (go as ILineWidth).LineWidth = dimStyle.DimLineWidth;
            IColorDef cd = go as IColorDef;
            if (cd != null) cd.ColorDef = dimStyle.DimLineColor;
            List.Add(go);
            if (outside)
            {	// Pfeil außerhalb, noch ein Stückchen Maßlinie hinzufügen
                Line2D l2d = new Line2D(pos, pos + 1.5 * w * vdir);
                go = l2d.MakeGeoObject(this.plane);
                (go as IColorDef).ColorDef = dimStyle.DimLineColor;
                (go as ILineWidth).LineWidth = dimStyle.DimLineWidth;
                List.Add(go);
            }
        }
        private void AddClosedArrow(GeoObjectList List, GeoPoint2D pos, Angle dir, double layoutFactor, bool outside)
        {
            double w = dimStyle.SymbolSize * layoutFactor; // echte Breite des Pfeiles
            GeoVector2D vdir = new GeoVector2D(dir);
            double d = w / Math.Tan(ArrowAngle / 2) / 2; // Länge des Pfeils
            GeoPoint2D a1 = pos + d * vdir + (w / 2.0) * (vdir.ToLeft());
            GeoPoint2D a2 = pos + d * vdir + (w / 2.0) * (vdir.ToRight());
            Polyline2D p2d = new Polyline2D(new GeoPoint2D[] { a1, pos, a2, a1 });
            IGeoObject go = p2d.MakeGeoObject(this.plane);
            (go as IColorDef).ColorDef = dimStyle.FillColor;
            (go as ILineWidth).LineWidth = dimStyle.DimLineWidth;
            IColorDef cd = go as IColorDef;
            if (cd != null) cd.ColorDef = dimStyle.DimLineColor;
            List.Add(go);
            if (outside)
            {	// Pfeil außerhalb, noch ein Stückchen Maßlinie hinzufügen
                Line2D l2d = new Line2D(pos, pos + 1.5 * w * vdir);
                go = l2d.MakeGeoObject(this.plane);
                (go as IColorDef).ColorDef = dimStyle.DimLineColor;
                (go as ILineWidth).LineWidth = dimStyle.DimLineWidth;
                List.Add(go);
            }
        }
        private void AddFilledArrow(GeoObjectList List, GeoPoint2D pos, Angle dir, double layoutFactor, bool outside)
        {
            double w = dimStyle.SymbolSize * layoutFactor; // echte Breite des Pfeiles
            GeoVector2D vdir = new GeoVector2D(dir);
            double d = w / Math.Tan(ArrowAngle / 2) / 2; // Länge des Pfeils
            GeoPoint2D a1 = pos + d * vdir + (w / 2.0) * (vdir.ToLeft());
            GeoPoint2D a2 = pos + d * vdir + (w / 2.0) * (vdir.ToRight());
            try
            {
                Polyline2D p2d = new Polyline2D(new GeoPoint2D[] { a1, pos, a2, a1 });
                Border bdr = new Border(new ICurve2D[] { p2d });
                SimpleShape ss = new SimpleShape(bdr);
                CompoundShape cs = new CompoundShape(ss);
                Hatch h = Hatch.Construct();
                h.Plane = plane;
                h.CompoundShape = cs;
                h.HatchStyle = dimStyle.HatchStyleSolid;
                List.Add(h);
                if (outside)
                {	// Pfeil außerhalb, noch ein Stückchen Maßlinie hinzufügen
                    Line2D l2d = new Line2D(pos, pos + 1.5 * d * vdir);
                    IGeoObject go = l2d.MakeGeoObject(this.plane);
                    (go as IColorDef).ColorDef = dimStyle.DimLineColor;
                    (go as ILineWidth).LineWidth = dimStyle.DimLineWidth;
                    List.Add(go);
                }
            }
            catch (Polyline2DException)
            {
                // das hat nicht geklappt, vielleich ist die Symbolgröße 0.0
            }
        }
        private void AddCircle(GeoObjectList List, GeoPoint2D pos, double layoutFactor)
        {
            Circle2D c2d = new Circle2D(pos, dimStyle.SymbolSize * layoutFactor / 2.0);
            IGeoObject go = c2d.MakeGeoObject(this.plane);
            (go as IColorDef).ColorDef = dimStyle.FillColor;
            (go as ILineWidth).LineWidth = dimStyle.DimLineWidth;
            List.Add(go);
        }
        private void AddFilledCircle(GeoObjectList List, GeoPoint2D pos, double layoutFactor)
        {
            Circle2D c2d = new Circle2D(pos, dimStyle.SymbolSize * layoutFactor / 2.0);
            Border bdr = new Border(new ICurve2D[] { c2d });
            SimpleShape ss = new SimpleShape(bdr);
            CompoundShape cs = new CompoundShape(ss);
            Hatch h = Hatch.Construct();
            h.Plane = plane;
            h.CompoundShape = cs;
            h.HatchStyle = dimStyle.HatchStyleSolid;
            List.Add(h);
        }
        private void AddSlash(GeoObjectList List, GeoPoint2D pos, Angle dir, double layoutFactor)
        {
            double w = dimStyle.SymbolSize * layoutFactor / 2.0; // halbe Breite des Striches
            GeoVector2D vdir = new GeoVector2D(dir);
            GeoPoint2D a1 = pos + w * vdir + w * (vdir.ToLeft());
            GeoPoint2D a2 = pos - w * vdir + w * (vdir.ToRight());
            Line2D l2d = new Line2D(a1, a2);
            IGeoObject go = l2d.MakeGeoObject(this.plane);
            (go as IColorDef).ColorDef = dimStyle.FillColor;
            (go as ILineWidth).LineWidth = dimStyle.DimLineWidth;
            List.Add(go);
        }
        private void AddCoordRefSymbol(GeoObjectList List, GeoPoint2D pos, double layoutFactor)
        {	// die Koordinatenbemaßung hat zwei verschiedene Symbolarten: die eingestellte und
            // wenn ein Pfeil eingestellt ist einen Kreis
            switch (dimStyle.Symbol)
            {
                case DimensionStyle.ESymbol.DimOpenArrow: AddCircle(List, pos, layoutFactor); break;
                case DimensionStyle.ESymbol.DimClosedArrow: AddCircle(List, pos, layoutFactor); break;
                case DimensionStyle.ESymbol.DimFilledArrow: AddFilledCircle(List, pos, layoutFactor); break;
                case DimensionStyle.ESymbol.DimCircle: AddCircle(List, pos, layoutFactor); break;
                case DimensionStyle.ESymbol.DimFilledCircle: AddFilledCircle(List, pos, layoutFactor); break;
                case DimensionStyle.ESymbol.DimSlash: AddCircle(List, pos, layoutFactor); break;
                case DimensionStyle.ESymbol.DimSymbol: break;
            }
        }
        private void AddSymbol(GeoObjectList List, GeoPoint2D pos, Angle dir, double layoutFactor, bool outside)
        {
            if (!dimStyle.DimNoDimLine)
            {
                switch (dimStyle.Symbol)
                {
                    case DimensionStyle.ESymbol.DimOpenArrow: AddOpenArrow(List, pos, dir, layoutFactor, outside); break;
                    case DimensionStyle.ESymbol.DimClosedArrow: AddClosedArrow(List, pos, dir, layoutFactor, outside); break;
                    case DimensionStyle.ESymbol.DimFilledArrow: AddFilledArrow(List, pos, dir, layoutFactor, outside); break;
                    case DimensionStyle.ESymbol.DimCircle: AddCircle(List, pos, layoutFactor); break;
                    case DimensionStyle.ESymbol.DimFilledCircle: AddFilledCircle(List, pos, layoutFactor); break;
                    case DimensionStyle.ESymbol.DimSlash: AddSlash(List, pos, dir, layoutFactor); break;
                    case DimensionStyle.ESymbol.DimSymbol: break;
                }
            }
        }
        /// <summary>
        /// Ein Symbol zufügen. Die Maßlinie ist immer die X-Achse.
        /// </summary>
        /// <param name="List">hier zufügen</param>
        /// <param name="xPos">x-Position des Punktes</param>
        /// <param name="layoutFactor">Faktor Papier->Welt</param>
        /// <param name="leftSide">Symbol auf der linken Seite</param>
        /// <param name="outside">Symbol ist außerhalb</param>
        private void AddSymbol(GeoObjectList list, double xPos, double layoutFactor, bool leftSide, bool outside)
        {
            Angle dir;
            if (leftSide != outside) dir = Angle.A0;
            else dir = Angle.A180;
            GeoPoint2D pos = new GeoPoint2D(xPos, 0.0);
            AddSymbol(list, pos, dir, layoutFactor, outside);
        }
        private double GetSymbolLength(double layoutFactor)
        {
            switch (dimStyle.Symbol)
            {
                case DimensionStyle.ESymbol.DimOpenArrow:
                case DimensionStyle.ESymbol.DimClosedArrow:
                case DimensionStyle.ESymbol.DimFilledArrow:
                    double w = dimStyle.SymbolSize * layoutFactor; // echte Breite des Pfeiles
                    return w / Math.Tan(ArrowAngle / 2) / 2; // Länge des Pfeils
                case DimensionStyle.ESymbol.DimCircle:
                case DimensionStyle.ESymbol.DimFilledCircle:
                case DimensionStyle.ESymbol.DimSlash:
                    return dimStyle.SymbolSize * layoutFactor / 2.0;
                case DimensionStyle.ESymbol.DimSymbol: return 0.0;
            }
            return 0.0;
        }
        private string FormatAngleValue(double dimValue)
        {	// dimValue ist Bogenmaß bzw Bogenlänge
            switch (dimStyle.AngleText)
            {
                case DimensionStyle.EAngleText.Radian:
                case DimensionStyle.EAngleText.ArcLength: // hier wird Bogenlänge bzw Bogenmaß bereits richtig als Input erwartet
                    return FormatValue(dimValue);
                case DimensionStyle.EAngleText.DegreeDecimal:
                    return FormatValue(dimValue * 180 / Math.PI);
                case DimensionStyle.EAngleText.Grade:
                    return FormatValue(dimValue * 200 / Math.PI);
                case DimensionStyle.EAngleText.DegreeMinuteSecond:
                    {
                        double d = dimValue * 180 / Math.PI;
                        double m = (d - Math.Floor(d)) * 60;
                        double s = (d / 60 - Math.Floor(d / 60)) * 60;
                        string txtd = Math.Floor(d).ToString("#"); // Grad ganzzahlig
                        string txtm = Math.Floor(m).ToString("#"); // Grad ganzzahlig
                        string txts = Math.Floor(s).ToString("#"); // Grad ganzzahlig
                        return txtd + "°" + txtm + "'" + txts + "\""; // noch nicht rnd ausgewertet
                    }
            }
            return "";
        }
        private string FormatValue(double dimValue)
        {
            if (dimStyle.Round > 1.0)
            {
                int n = (int)(dimStyle.Round);
                if (n <= 0) n = 1;
                if (n > 64) n = 64;
                int z = (int)Math.Round(dimValue * n);
                while ((z % 2) == 0 && (n > 1))
                {
                    n /= 2;
                    z /= 2;
                }
                if (n == 1) return z.ToString();
                else
                {
                    if (z > n)
                    {
                        int w = (z / n);
                        z = z % n;
                        return w.ToString() + " " + z.ToString() + "/" + n.ToString();
                    }
                    else
                    {
                        return z.ToString() + "/" + n.ToString();
                    }
                }
            }
            else
            {
                if (dimStyle.Round == 0) return dimValue.ToString(); // höchste Genauigkeit
                try
                {
                    double rr = Math.Round(dimValue / dimStyle.Round) * dimStyle.Round;
                    string format = "";
                    // #.## entfernt überflüssige Nullen nach dem Komma, 0.00 erzwingt die Stellenanzahl, also auch mit Nullen
                    if (dimStyle.Round < 1.0) format = "0.#";
                    if (dimStyle.Round < 0.1) format = "0.##";
                    if (dimStyle.Round < 0.01) format = "0.###";
                    if (dimStyle.Round < 0.001) format = "0.####";
                    if (dimStyle.Round < 0.0001) format = "0.#####";
                    if (dimStyle.Round < 0.00001) format = "0.######";
                    if (dimStyle.Round == 0.25) format = "0.##";
                    if (dimStyle.Round == 0.025) format = "0.###";
                    if (dimStyle.Round == 0.0025) format = "0.####";
                    if (dimStyle.DimForceTrailingZero) format = format.Replace("#", "0");
                    string res = rr.ToString(format, NumberFormatInfo.InvariantInfo);
                    if (res.Length == 0) res = "0";
                    return res;
                }
                catch (OverflowException)
                {
                    return dimValue.ToString();
                }
            }
        }
        private Text MakeText(GeoPoint2D location, Angle direction, double textSize, Text.AlignMode alignement, string text)
        {
            Text res = Text.Construct();
            res.Location = plane.ToGlobal(location);
            res.SetDirections(plane.ToGlobal(direction.Direction), plane.ToGlobal(direction.Direction.ToLeft()));
            res.Font = dimStyle.TextFont;
            res.ColorDef = dimStyle.FontColor;
            if (res.ColorDef == null) res.ColorDef = dimStyle.ExtLineColor;
            res.TextString = text;
            res.Alignment = alignement; // 0: Grundlinie, 1: unten, 2: zentriert, 3: oben
            res.LineAlignment = 0; // links
            res.TextSize = textSize;
            return res;
        }
        private void MakeText(Text txt, GeoPoint2D location, Angle direction, double textSize, Text.AlignMode alignement)
        {	// bestehendes Textobjekt (es wird gerade editiert) verändern
            txt.Location = plane.ToGlobal(location);
            txt.SetDirections(plane.ToGlobal(direction.Direction), plane.ToGlobal(direction.Direction.ToLeft()));
            txt.Font = dimStyle.TextFont;
            // res.TextString = text;
            txt.Alignment = alignement; // 0: Grundlinie, 1: unten, 2: zentriert, 3: oben
            txt.LineAlignment = 0; // links
            txt.TextSize = textSize;
        }
        private GeoObjectList MakeText(double layoutFactor, int index)
        {	// liefert eine Liste von Texten um den 0-Punkt, die später noch
            // platziert werden muss
            GeoObjectList res = new GeoObjectList();
            // diverse Texte erzeugen: Prefix, Wert, Postfix, Hochgestellt, Tiefgestellt, Alternativ
            // die Texte werden alle bei (0,0) (in der Ebene) und horizontal erzeugt. 
            // Von links nach rechts und ggf. Hochgestellt. Die ganze Liste wird dann als
            // ein Ding betrachtet und dessen Extend wird zum Platzieren verwendet.

            double left = 0.0; // linker Rand, schreitet fort

            Text txt = null;
            string str = GetPrefix(index);
            if (str != null && str.Length > 0)
            {
                txt = MakeText(new GeoPoint2D(left, 0.0), 0.0, dimStyle.TextSize * layoutFactor, Text.AlignMode.Center, str);
            }
            if (txt != null)
            {
                txt.UserData.Add("CADability.Dimension.Prefix", index);
                res.Add(txt);
                BoundingRect rct = txt.GetExtent(plane.CoordSys.GetProjection(), ExtentPrecision.Raw);
                // BoundingRect rct = IGeoObjectImpl.GetExtent(txt, plane.CoordSys.GetProjection(), false);
                left = rct.Right;
            }
            txt = null;
            str = GetDimText(index);
            if (str != null && str.Length > 0)
            {
                if (editingTextIndex != index || editingMode != EditingMode.editDimText || editingText == null)
                {
                    txt = MakeText(new GeoPoint2D(left, 0.0), 0.0, dimStyle.TextSize * layoutFactor, Text.AlignMode.Center, str);
                    if (editingTextIndex == index && editingMode == EditingMode.editDimText)
                    {
                        editingText = txt; // erstmaliges bestimmen des zu editierenden Textes
                    }
                }
                else
                {
                    txt = editingText; // verändern des gerade editierten Textes
                    MakeText(txt, new GeoPoint2D(left, 0.0), 0.0, dimStyle.TextSize * layoutFactor, Text.AlignMode.Center);
                }
            }
            if (txt != null)
            {
                txt.UserData.Add("CADability.Dimension.Text", index);
                res.Add(txt);
                BoundingRect rct = txt.GetExtent(plane.CoordSys.GetProjection(), ExtentPrecision.Raw);
                // BoundingRect rct = IGeoObjectImpl.GetExtent(txt, plane.CoordSys.GetProjection(), false);
                left = rct.Right;
            }
            txt = null;
            str = GetPostfix(index);
            if (str != null && str.Length > 0)
            {	// prefix ist überschrieben
                txt = MakeText(new GeoPoint2D(left, 0.0), 0.0, dimStyle.TextSize * layoutFactor, Text.AlignMode.Center, str);
            }
            if (txt != null)
            {
                txt.UserData.Add("CADability.Dimension.PostFix", index);
                res.Add(txt);
                BoundingRect rct = txt.GetExtent(plane.CoordSys.GetProjection(), ExtentPrecision.Raw);
                // BoundingRect rct = IGeoObjectImpl.GetExtent(txt, plane.CoordSys.GetProjection(), false);
                left = rct.Right;
            }
            // Toleranzen kommen automatisch, wenn eingeschaltet oder wenn explizit gesetzt
            if (dimStyle.DimTxtTolerances || tolPlusText[index] != null || tolMinusText[index] != null)
            {
                double left1 = left;
                double left2 = left;
                txt = null;
                if (dimStyle.DimTxtTolerances) str = GetTolPlusText(index);
                else str = tolPlusText[index];
                if (str != null && str.Length > 0)
                {
                    txt = MakeText(new GeoPoint2D(left, 0.0), 0.0, dimStyle.TextSizeTol * layoutFactor, Text.AlignMode.Bottom, str);
                }
                if (txt != null)
                {
                    txt.UserData.Add("CADability.Dimension.TolPlus", index);
                    res.Add(txt);
                    BoundingRect rct = txt.GetExtent(plane.CoordSys.GetProjection(), ExtentPrecision.Raw);
                    // BoundingRect rct = IGeoObjectImpl.GetExtent(txt, plane.CoordSys.GetProjection(), false);
                    left1 = rct.Right;
                }
                txt = null;
                if (dimStyle.DimTxtTolerances) str = GetTolMinusText(index);
                else str = tolMinusText[index];
                if (str != null && str.Length > 0)
                {
                    txt = MakeText(new GeoPoint2D(left, 0.0), 0.0, dimStyle.TextSizeTol * layoutFactor, Text.AlignMode.Top, str);
                }
                if (txt != null)
                {
                    txt.UserData.Add("CADability.Dimension.TolMinus", index);
                    res.Add(txt);
                    BoundingRect rct = txt.GetExtent(plane.CoordSys.GetProjection(), ExtentPrecision.Raw);
                    // BoundingRect rct = IGeoObjectImpl.GetExtent(txt, plane.CoordSys.GetProjection(), false);
                    left1 = rct.Right;
                }
                left = Math.Max(left1, left2);
            }

            return res;
        }
        private double GetSymbolWidth(Projection p)
        {	// die Breite des Symbols 
            double l = dimStyle.SymbolSize * p.LayoutFactor;
            switch (dimStyle.Symbol)
            {
                case DimensionStyle.ESymbol.DimOpenArrow: return l;
                case DimensionStyle.ESymbol.DimClosedArrow: return l;
                case DimensionStyle.ESymbol.DimFilledArrow: return l;
                case DimensionStyle.ESymbol.DimCircle: return l / 2.0;
                case DimensionStyle.ESymbol.DimFilledCircle: return l / 2.0;
                case DimensionStyle.ESymbol.DimSlash: return l / 2.0;
                case DimensionStyle.ESymbol.DimSymbol: break;
            }
            return 0.0;
        }
        private enum EGuideLines { both, onlyLeft, onlyRight }
        private void MakeTwoPointsDim(GeoObjectList list, Projection p, GeoPoint2D p1, GeoPoint2D p2, double dimLineHeight, bool rotateText, EGuideLines guideLines, bool mustFit, int index)
        {	// die Ebene ist berechnet, zwei Punkte (p1 und p2) sollen vermaßt werden, Maßlinie ist
            // in dieser Ebene horizontal, dimLineHeight gibt die Höhe der Maßlinie an

            // 1. Hilfslinien
            if (guideLines != EGuideLines.onlyRight)
            {	// linke Hilfslinie
                GeoPoint2D p1Foot = p1;
                GeoPoint2D p1Top = p1;
                p1Top.y += dimStyle.ExtLineOffset;
                p1Foot.y = dimLineHeight + dimStyle.ExtLineExtension;
                AddGuideLine(list, p1Top, p1Foot);
            }
            if (guideLines != EGuideLines.onlyLeft)
            {	// rechte Hilfslinie
                GeoPoint2D p2Foot = p2;
                p2Foot.y = dimLineHeight + dimStyle.ExtLineExtension; //???
                GeoPoint2D p2Top = p2;
                p2Top.y += dimStyle.ExtLineOffset;
                AddGuideLine(list, p2Top, p2Foot);
            }

            // 2. die Maßlinie, Maßsymbole und Texte einfügen
            double symbolWidth = GetSymbolWidth(p);
            p1.y = dimLineHeight;
            p2.y = dimLineHeight; // das ist die Projektion auf Maßlinie
            bool SymbolInside = false;
            switch (dimStyle.SymbolPlacement)
            {
                case DimensionStyle.ESymbolPlacement.DimInside: SymbolInside = true; break;
                case DimensionStyle.ESymbolPlacement.DimOutside: SymbolInside = false; break;
                case DimensionStyle.ESymbolPlacement.DimAutomatic: SymbolInside = (p2.x - p1.x > 2.0 * symbolWidth); break;
            }
            if (SymbolInside)
            {
                AddSymbol(list, p1, Angle.A0, p.LayoutFactor, false);
                AddSymbol(list, p2, Angle.A180, p.LayoutFactor, false);
            }
            else
            {
                AddSymbol(list, p1, Angle.A180, p.LayoutFactor, true);
                AddSymbol(list, p2, Angle.A0, p.LayoutFactor, true);
            }

            // der Text
            GeoObjectList l = MakeText(p.LayoutFactor, index);
            BoundingRect rct = l.GetExtent(plane.CoordSys.GetProjection(), true, false);
            if (rotateText)
            {	// den ganzen Beschriftungsklumpatsch einfach umdrehen
                GeoPoint c = plane.ToGlobal(rct.GetCenter());
                l.Modify(ModOp.Rotate(c, plane.Normal, SweepAngle.Opposite));
            }
            // dimStyle.TextDist Abstand von der Maßlinie, 0.0 auf der Maßlinie, 1.0 eine Textgröße über der Maßlinie
            // this.textPos 0.0 links, 1.0 rechts 0.5 Mitte
            double textdist = dimStyle.TextDist;
            if (rotateText) textdist = -textdist; // es steht auf dem Kopf, also in die andere Richtung
            GeoPoint2D txtLocation = p1 + textPos[index] * (p2 - p1) + dimStyle.TextSize * textdist * GeoVector2D.YAxis;
            SweepAngle sa = 0.0;
            if (dimStyle.DimTxtRotate60) sa = SweepAngle.Deg(60);
            if (dimStyle.DimTxtRotate90) sa = SweepAngle.Deg(90);
            if (dimStyle.DimTxtRotateAutomatic)
            {
                if (rct.Width > p2.x - p1.x) sa = SweepAngle.Deg(90);
            }
            if ((sa == 0.0 && dimStyle.DimTxtAutomatic && (rct.Width > p2.x - p1.x) && mustFit) || dimStyle.DimTxtOutside)
            {	// genau zwei Punkte zu vermaßen und der Text passt nicht rein:
                // nach rechts verschieben
                GeoVector2D dir = (p2 - p1);
                dir.Norm();
                txtLocation = p2 + (rct.Width / 2.0) * dir + dimStyle.TextSize * dimStyle.TextDist * GeoVector2D.YAxis;
            }

            GeoVector2D txtMove = txtLocation - rct.GetCenter();
            if (sa != 0)
            {
                GeoPoint2D rotateCenter = new GeoPoint2D(rct.GetLowerLeft(), rct.GetUpperLeft());
                txtMove = txtLocation - rotateCenter;
                l.Modify(ModOp.Rotate(plane.ToGlobal(rotateCenter), plane.Normal, sa));
            }
            l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
            rct.Move(txtMove);
            list.AddRange(l);

            // die Bemaßungslinie
            if (sa == 0.0 && dimStyle.DimBreakDimLine)
            {
                if (dimStyle.DimTextOutsideForceDimensionLine)
                {
                    double pos1 = Geometry.LinePar(p1, p2, rct.GetLowerLeft());
                    double pos2 = Geometry.LinePar(p1, p2, rct.GetUpperRight());
                    if (Math.Min(pos1, pos2) < 0.0) p1 = Geometry.LinePos(p1, p2, Math.Min(pos1, pos2));
                    else if (Math.Max(pos1, pos2) > 1.0) p2 = Geometry.LinePos(p1, p2, Math.Max(pos1, pos2));
                }
                ClipRect clr = new ClipRect(rct);
                GeoPoint2D p3 = p1;
                GeoPoint2D p4 = p2;
                if (!clr.ClipLine(ref p3, ref p4))
                {
                    AddDimensionLine(list, p1, p2, index);
                }
                else
                {
                    AddDimensionLine(list, p1, p3, index);
                    AddDimensionLine(list, p4, p2, index);
                }
            }
            else
            {
                AddDimensionLine(list, p1, p2, index);
            }
        }

        private void RecalcPoints(Projection p, GeoObjectList List)
        {
            if (dimStyle == null) return;
            if (points.Length < 2) return;
            try
            {
                plane = new Plane(dimLineRef, dimLineDirection, normal ^ dimLineDirection);
            }
            catch (PlaneException)
            {   // Ebene nicht bestimmbar, was also tun?
                // nimm die Ebene aus den Punkten und dimlineref
                try
                {
                    Plane pln = new Plane(dimLineRef, points[0], points[1]);
                    normal = pln.Normal;
                    plane = new Plane(dimLineRef, dimLineDirection, normal ^ dimLineDirection);
                }
                catch (PlaneException)
                {
                    return;
                }
            }
            GeoVector2D hor = plane.Project(p.ProjectionPlane.DirectionX);
            bool rotateText = hor.x < 0;
            rotateText = false; // DEBUG!!!
            // die Ebene steht fest, alles weitere findet in dieser Ebene statt
            // dimLineRef ist der Ursprung, dimLineDirection die X-Achse
            // Sortierung sollte gewöhnlich nichts ändern (nur während der Konstruktion,
            // wo ohnehin die Textfelder alle leer, also default sind), in den anderen
            // Fällen wird immer SortPoints vorher aufgerufen.
            GeoPoint2D[] SortedPoints = new GeoPoint2D[points.Length];
            for (int i = 0; i < points.Length; ++i)
            {
                SortedPoints[i] = plane.Project(points[i]);
            }
            Array.Sort(SortedPoints, 0, SortedPoints.Length, GeoPoint2D.CompareX);

            // Hilfslinien
            for (int i = 0; i < SortedPoints.Length; ++i)
            {
                GeoPoint2D p1 = SortedPoints[i];
                GeoPoint2D p2 = p1;
                p2.y = 0.0;
                AddGuideLine(List, p1, p2);
            }
            // die Maßlinie, Maßsymbole und Texte einfügen
            double symbolWidth = GetSymbolWidth(p);
            for (int i = 0; i < SortedPoints.Length - 1; ++i)
            {
                GeoPoint2D p1 = SortedPoints[i];
                GeoPoint2D p2 = SortedPoints[i + 1];
                p1.y = 0.0;
                p2.y = 0.0; // das ist die Projektion auf die X-Achse
                bool SymbolInside = false;
                switch (dimStyle.SymbolPlacement)
                {
                    case DimensionStyle.ESymbolPlacement.DimInside: SymbolInside = true; break;
                    case DimensionStyle.ESymbolPlacement.DimOutside: SymbolInside = false; break;
                    case DimensionStyle.ESymbolPlacement.DimAutomatic:
                        {
                            double w = dimStyle.SymbolSize * p.LayoutFactor; // echte Breite des Pfeiles
                            double d = w / Math.Tan(ArrowAngle / 2) / 2; // Länge des Pfeils
                            SymbolInside = (p2.x - p1.x > 2.0 * d); // das wirkt nur wenn Pfeile eingestllt sind, sonst isses egal
                        }
                        break;
                }
                if (SymbolInside)
                {
                    AddSymbol(List, p1.x, p.LayoutFactor, true, false);
                    AddSymbol(List, p2.x, p.LayoutFactor, false, false);
                }
                else
                {
                    AddSymbol(List, p1.x, p.LayoutFactor, true, true);
                    AddSymbol(List, p2.x, p.LayoutFactor, false, true);
                }
                // der Text
                GeoObjectList l = MakeText(p.LayoutFactor, i);
                BoundingRect rct = l.GetExtent(plane.CoordSys.GetProjection(), true, false);
                // BoundingRect rcttxt = rct; // struct Zuweisung!
                rct.Inflate(dimStyle.DimensionLineGap);
                // rcttxt.Inflate(dimStyle.DimensionLineGap);
                Polyline2D textRect = new Polyline2D(new GeoPoint2D[] { new GeoPoint2D(rct.Left, rct.Bottom), new GeoPoint2D(rct.Right, rct.Bottom), new GeoPoint2D(rct.Right, rct.Top), new GeoPoint2D(rct.Left, rct.Top) });
                if (rotateText)
                {	// den ganzen Beschriftungsklumpatsch einfach umdrehen
                    GeoPoint c = plane.ToGlobal(rct.GetCenter());
                    l.Modify(ModOp.Rotate(c, plane.Normal, SweepAngle.Opposite));
                    textRect.Modify(ModOp2D.Rotate(rct.GetCenter(), SweepAngle.Opposite));
                }
                // dimStyle.TextDist Abstand von der Maßlinie, 0.0 auf der Maßlinie, 1.0 eine Textgröße über der Maßlinie
                // this.textPos 0.0 links, 1.0 rechts 0.5 Mitte
                double textdist = dimStyle.TextDist;
                if (rotateText) textdist = -textdist; // es steht auf dem Kopf, also in die andere Richtung
                GeoPoint2D txtLocation = p1 + textPos[i] * (p2 - p1) + dimStyle.TextSize * textdist * GeoVector2D.YAxis;
                SweepAngle sa = 0.0;
                if (dimStyle.DimTxtRotate60) sa = SweepAngle.Deg(60);
                if (dimStyle.DimTxtRotate90) sa = SweepAngle.Deg(90);
                if (dimStyle.DimTxtRotateAutomatic)
                {
                    if (rct.Width > p2.x - p1.x) sa = SweepAngle.Deg(90);
                }
                if ((sa == 0.0 && dimStyle.DimTxtAutomatic && (rct.Width > p2.x - p1.x) && SortedPoints.Length == 2) || dimStyle.DimTxtOutside)
                {	// genau zwei Punkte zu vermaßen und der Text passt nicht rein:
                    // nach rechts verschieben
                    GeoVector2D dir = (p2 - p1);
                    dir.Norm();
                    txtLocation = p2 + (rct.Width / 2.0) * dir + dimStyle.TextSize * dimStyle.TextDist * GeoVector2D.YAxis;
                }

                GeoVector2D txtMove = txtLocation - rct.GetCenter();
                if (sa != 0)
                {
                    GeoPoint2D rotateCenter = new GeoPoint2D(rct.GetLowerLeft(), rct.GetUpperLeft());
                    txtMove = txtLocation - rotateCenter;
                    l.Modify(ModOp.Rotate(plane.ToGlobal(rotateCenter), plane.Normal, sa));
                    textRect.Modify(ModOp2D.Rotate(rotateCenter, sa));
                }
                l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
                textRect.Modify(ModOp2D.Translate(txtMove));
                rct.Move(txtMove);
                List.AddRange(l);
                if (dimStyle.DimTxtRect)
                {
                    AddTextRect(List, textRect, i);
                }
                // die Bemaßungslinie
                // dimStyle.DimLineExtension am Rand zufügen
                if (i == 0) p1.x -= dimStyle.DimLineExtension;
                if (i == SortedPoints.Length - 2) p2.x += dimStyle.DimLineExtension;
                if (dimStyle.DimTextOutsideForceDimensionLine)
                {
                    double pos1 = Geometry.LinePar(p1, p2, rct.GetLowerLeft());
                    double pos2 = Geometry.LinePar(p1, p2, rct.GetUpperRight());
                    if (Math.Min(pos1, pos2) < 0.0) p1 = Geometry.LinePos(p1, p2, Math.Min(pos1, pos2));
                    else if (Math.Max(pos1, pos2) > 1.0) p2 = Geometry.LinePos(p1, p2, Math.Max(pos1, pos2));
                }
                if (sa == 0.0 && dimStyle.DimBreakDimLine)
                {
                    ClipRect clr = new ClipRect(rct);
                    GeoPoint2D p3 = p1;
                    GeoPoint2D p4 = p2;
                    if (!clr.ClipLine(ref p3, ref p4))
                    {
                        AddDimensionLine(List, p1, p2, i);
                    }
                    else
                    {
                        AddDimensionLine(List, p1, p3, i);
                        AddDimensionLine(List, p4, p2, i);
                    }
                }
                else
                {
                    AddDimensionLine(List, p1, p2, i);
                }
            }
        }
        private void RecalcCoord(Projection p, GeoObjectList List)
        {
            if (dimStyle == null) return;
            if (points.Length < 2) return;
            try
            {
                plane = new Plane(dimLineRef, dimLineDirection, normal ^ dimLineDirection);
            }
            catch (PlaneException) { return; } // Ebene nicht bestimmbar, was also tun?
            GeoVector2D hor = plane.Project(p.ProjectionPlane.DirectionX);
            bool rotateText = hor.Angle >= Angle.A180 && hor.Angle < Math.PI * 2.0;
            // die Ebene steht fest, alles weitere findet in dieser Ebene statt
            // dimLineRef ist der Ursprung, dimLineDirection die X-Achse
            // Sortierung sollte gewöhnlich nichts ändern (nur während der Konstruktion,
            // wo ohnehin die Textfelder alle leer, also default sind), in den anderen
            // Fällen wird immer SortPoints vorher aufgerufen.
            GeoPoint2D[] SortedPoints = new GeoPoint2D[points.Length];
            for (int i = 0; i < points.Length; ++i)
            {
                SortedPoints[i] = plane.Project(points[i]);
            }
            Array.Sort(SortedPoints, 1, SortedPoints.Length - 1, GeoPoint2D.CompareX);

            if (dimStyle.DimMultiLine && dimStyle.LineIncrement > 0.0)
            {
                GeoPoint2D pref = SortedPoints[0];
                double leftPos = 0.0;
                double rightPos = 0.0;
                bool down = 0.0 < pref.y;
                double h = 0.0;
                EGuideLines guideLines = EGuideLines.both;
                for (int i = 1; i < points.Length; ++i)
                {
                    GeoPoint2D pi = SortedPoints[i];
                    bool left = pi.x < pref.x;
                    if (left)
                    {
                        h = leftPos;
                        if (down) leftPos -= dimStyle.LineIncrement;
                        else leftPos += dimStyle.LineIncrement;
                        guideLines = EGuideLines.onlyLeft;
                    }
                    else
                    {
                        h = rightPos;
                        if (down) rightPos -= dimStyle.LineIncrement;
                        else rightPos += dimStyle.LineIncrement;
                        guideLines = EGuideLines.onlyRight;
                    }
                    if (left)
                    {
                        MakeTwoPointsDim(List, p, pi, pref, h, rotateText, guideLines, true, i - 1);
                    }
                    else
                    {
                        MakeTwoPointsDim(List, p, pref, pi, h, rotateText, guideLines, true, i - 1);
                    }
                }
                double y0;
                if (down) y0 = Math.Min(leftPos, rightPos) + dimStyle.LineIncrement;
                else y0 = Math.Max(leftPos, rightPos) - dimStyle.LineIncrement;
                AddGuideLine(List, pref, new GeoPoint2D(pref.x, y0));

                return;
            }

            // Hilfslinien
            for (int i = 0; i < SortedPoints.Length; ++i)
            {
                GeoPoint2D p1 = SortedPoints[i];
                GeoPoint2D p2 = p1;
                p2.y = 0.0;
                AddGuideLine(List, p1, p2);
            }
            // die Maßlinie, Maßsymbole und Texte einfügen
            GeoPoint2D p0 = SortedPoints[0]; // der Bezugspunkt
            GeoPoint2D p0x = p0; // der Bezugspunkt
            p0x.y = 0.0;
            AddCoordRefSymbol(List, p0x, p.LayoutFactor);
            double pxmin = p0.x;
            double pxmax = p0.x;
            double symbolWidth = GetSymbolWidth(p);
            for (int i = 1; i < SortedPoints.Length; ++i)
            {
                GeoPoint2D p1 = SortedPoints[i];
                bool moveText = p1.y < 0.0;
                p1.y = 0.0;
                if (p1.x < pxmin) pxmin = p1.x;
                if (p1.x > pxmax) pxmax = p1.x;
                // Symbole immer von innen
                Angle a;
                if (p1.x > p0.x) a = Angle.A180;
                else a = Angle.A0;
                AddSymbol(List, p1, a, p.LayoutFactor, false);

                // der Text
                GeoObjectList l = MakeText(p.LayoutFactor, i - 1);
                BoundingRect rct = l.GetExtent(plane.CoordSys.GetProjection(), true, false);
                rct += dimStyle.TextSize / 2.0;
                GeoPoint c = plane.ToGlobal(rct.GetCenter());
                if (rotateText)
                {	// den ganzen Beschriftungsklumpatsch einfach umdrehen
                    l.Modify(ModOp.Rotate(c, plane.Normal, SweepAngle.Opposite));
                }
                GeoPoint2D rightCenter = new GeoPoint2D(rct.Right, (rct.Bottom + rct.Top) / 2.0);
                c = plane.ToGlobal(rightCenter);
                // rechter Rand
                l.Modify(ModOp.Rotate(c, plane.Normal, SweepAngle.ToLeft));
                if (moveText)
                {
                    l.Modify(ModOp.Translate(plane.ToGlobal(new GeoVector2D(0, rct.Width))));
                }
                // dimStyle.TextDist Abstand von der Maßlinie, 0.0 auf der Maßlinie, 1.0 eine Textgröße über der Maßlinie
                // this.textPos 0.0 links, 1.0 rechts 0.5 Mitte
                double textdist = dimStyle.TextDist;
                if (rotateText) textdist = -textdist; // es steht auf dem Kopf, also in die andere Richtung
                textdist = 0.0; // textdist dient der Angabe über oder unter der Maßlinie
                // das können wir hier nicht gebrauchen!
                double d = -10;
                if (moveText) d = -d;
                GeoPoint2D txtLocation = p1 + dimStyle.TextSize * textdist * GeoVector2D.YAxis + (textPos[i - 1] - 0.5) * dimStyle.TextSize * d * GeoVector2D.YAxis;
                SweepAngle sa = 0.0;

                GeoVector2D txtMove = txtLocation - rightCenter;
                l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
                rct.Move(txtMove);
                List.AddRange(l);

            }
            // die Bemaßungslinie (es ist nur eine durchgehende)
            AddDimensionLine(List, new GeoPoint2D(pxmin, 0.0), new GeoPoint2D(pxmax, 0.0), 0);
        }
        private void RecalcAngle(Projection p, GeoObjectList list)
        {
            // relevante Daten: points[0]: der Mittelpunkt
            // points[1]: Startpunkt für die 1. Hilfslinie
            // points[2]: Startpunkt für die 2. Hilfslinie
            // dimLineRef: Bezugspunkt

            // die Winkelbemaßung geht immer im Uhrzeigersinn, d.h. von der Startrichtung
            // zur Endrichtung linksrum. Damit ist es auch möglich, Winkel größer als 180°
            // zu bemaßen.
            if (points.Length < 3) return;
            try
            {
                plane = new Plane(points[0], points[1] - points[0], points[2] - points[0]);
                // die Normale dieser Ebene und this.normal sollten in die selbe Richtung zeigen
                GeoVector ntest = plane.ToLocal(normal);
                if (ntest.z < 0.0)
                {	// Normalenvektor ist auf der "Unterseite", Ebene umdrehen
                    plane = new Plane(points[0], points[2] - points[0], points[1] - points[0]);
                }
                plane.Align(p.ProjectionPlane, false);
            }
            catch (PlaneException) { return; } // Ebene nicht bestimmbar, was also tun?
            // die lokale Ebene ist nun so bestimmt: der Mittelpunkt ist der Ursprung,
            // points[1] und points[2] liegen in der Ebene, Ebene ist horizontal zur Projektion ausgerichtet

            GeoPoint2D center = plane.Project(points[0]); // muss (0,0) sein
            GeoPoint2D ps1 = plane.Project(points[1]);
            GeoPoint2D ps2 = plane.Project(points[2]);
            GeoPoint2D refPoint = plane.Project(dimLineRef);
            GeoVector2D dir1 = ps1.ToVector(); dir1.Norm();
            GeoVector2D dir2 = ps2.ToVector(); dir2.Norm();
            SweepAngle sa = new SweepAngle(dir1, dir2);
            Angle txtposangle = dir1.Angle + textPos[0] * (new SweepAngle(dir1, dir2));
            GeoVector2D dirc = txtposangle.Direction;
            if (sa.Radian < 0.0) dirc = dirc.Opposite();
            double radius = Geometry.Dist(center, refPoint);
            GeoPoint2D txtPos = center + radius * dirc + dimStyle.TextSize * dimStyle.TextDist * dirc;
            GeoObjectList txt = MakeText(p.LayoutFactor, 0);
            BoundingRect txtExt = txt.GetExtent(plane.CoordSys.GetProjection(), true, false);
            GeoPoint2D txtCenter = txtExt.GetCenter();

            ModOp m = ModOp.Identity;

            if (!dimStyle.DimTxtInsideHor)
            {	// also nicht zwangsweise horizontal:
                Angle rotate = 0.0;
                if (dirc.Angle.Radian < Math.PI) rotate = dirc.ToRight().Angle;
                else rotate = dirc.ToLeft().Angle;
                m = ModOp.Rotate(plane.ToGlobal(txtCenter), plane.Normal, new SweepAngle(rotate.Radian));
            }
            m = ModOp.Translate(plane.ToGlobal(txtPos - txtCenter)) * m;
            txt.Modify(m);
            // jetzt stimmt der Extent leider nicht mehr und man kann nicht mehr clippen
            // um den MaßBogen zu unterbrechen
            list.AddRange(txt);
            GeoPoint2D arc1 = center + radius * dir1;
            GeoPoint2D arc2 = center + radius * dir2;
            AddGuideLine(list, ps1, arc1);
            AddGuideLine(list, ps2, arc2);

            Angle sym1 = dir1.ToLeft().Angle;
            Angle sym2 = dir2.ToRight().Angle;
            if (dimStyle.Symbol == DimensionStyle.ESymbol.DimOpenArrow ||
                dimStyle.Symbol == DimensionStyle.ESymbol.DimClosedArrow ||
                dimStyle.Symbol == DimensionStyle.ESymbol.DimFilledArrow)
            {
                double w = dimStyle.SymbolSize * p.LayoutFactor; // echte Breite des Pfeiles
                double d = w / Math.Tan(ArrowAngle / 2) / 2; // Länge des Pfeils
                // die beiden Richtungen so nach innen biegen, dass die Pfeile schön gerade aussehen
                SweepAngle da = new SweepAngle(radius, d / 2.0);
                sym1 += da;
                sym2 -= da;
            }
            AddSymbol(list, arc1, sym1, p.LayoutFactor, false);
            AddSymbol(list, arc2, sym2, p.LayoutFactor, false);
            AddDimensionArc(list, center, radius, dir1.Angle, sa);
        }
        private void RecalcRadius(Projection p, GeoObjectList list)
        {	// Bemaßung gegeben durch points[0]: Mittelpunkt, normal: Normale der Kreisebene,
            // radius: der Radius, dimLineRef: hierdurch geht die Maßlinie, unterscheiden innerhalb/außerhalb
            // dimLineRef ist auch ggf der Abknickpunkt
            if (dimStyle == null) return;
            if (points.Length < 1) return; // wir brauchen nur einen Punkt (MIttelpunkt)
            try
            {
                dimLineDirection = dimLineRef - points[0];
                plane = new Plane(points[0], dimLineDirection, normal ^ dimLineDirection);
            }
            catch (PlaneException) { return; } // Ebene nicht bestimmbar, was also tun?
            GeoVector2D hor = plane.Project(p.ProjectionPlane.DirectionX);
            hor.Norm();
            bool rotateText = hor.x < 0;

            GeoPoint2D center = plane.Project(points[0]); // müsste (0,0) sein
            GeoVector2D dir = plane.Project(dimLineDirection);
            dir.Norm();
            GeoPoint2D radiuspoint = center + radius * dir;
            GeoPoint2D p1 = plane.Project(dimLineRef); // dimLineRef in der Ebene, dort knick es ggf ab
            bool inside = Geometry.Dist(center, p1) < radius; // Bemaßung von innen
            if (inside)
            {
                AddDimensionLine(list, center, radiuspoint, 0);
                AddSymbol(list, radiuspoint, dir.Opposite().Angle, p.LayoutFactor, false);
                GeoObjectList l = MakeText(p.LayoutFactor, 0);
                BoundingRect rct = l.GetExtent(plane.CoordSys.GetProjection(), true, false);
                GeoPoint c = plane.ToGlobal(rct.GetCenter());
                if (rotateText)
                {	// den ganzen Beschriftungsklumpatsch einfach umdrehen
                    l.Modify(ModOp.Rotate(c, plane.Normal, SweepAngle.Opposite));
                }
                double textdist = dimStyle.TextDist;
                if (rotateText) textdist = -textdist; // es steht auf dem Kopf, also in die andere Richtung
                GeoPoint2D txtLocation = center + textPos[0] * (radiuspoint - center) + dimStyle.TextSize * textdist * GeoVector2D.YAxis;

                GeoVector2D txtMove = txtLocation - rct.GetCenter();
                l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
                rct.Move(txtMove);
                list.AddRange(l);
            }
            else
            {
                GeoObjectList l = MakeText(p.LayoutFactor, 0);
                BoundingRect rct = l.GetExtent(plane.CoordSys.GetProjection(), true, false);
                GeoPoint c = plane.ToGlobal(rct.GetCenter());
                if (dimStyle.DimRadiusBend)
                {	// abknickend, d.h. Linie bis zu dimLineRef (p1) und dann horizontal weiter
                    // für den Text
                    AddDimensionLine(list, radiuspoint, p1, 0);
                    double w = GetSymbolLength(p.LayoutFactor) * 1.1; // + 10%

                    // den Text gemäß hor drehen
                    l.Modify(ModOp.Rotate(c, plane.Normal, new SweepAngle(hor.Angle.Radian)));
                    if (hor.x < 0.0) hor = -hor; // hor umkeheren wenn der Strich nach links geht
                    double textdist = dimStyle.TextDist;
                    if (rotateText) textdist = -textdist; // es steht auf dem Kopf, also in die andere Richtung
                    GeoPoint2D txtLocation = p1 + (rct.Width / 2) * hor + ((textPos[0] - 0.5) * dimStyle.TextSize * 10) * hor + dimStyle.TextSize * textdist * hor.ToLeft();
                    GeoVector2D txtMove = txtLocation - rct.GetCenter();
                    l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
                    list.AddRange(l);

                    AddDimensionLine(list, p1, p1 + (rct.Width * 1.1) * hor, 1);
                    AddSymbol(list, radiuspoint, dir.Angle, p.LayoutFactor, false);
                }
                else
                {	// gerade außerhalb, Maßlinie genausolang wie der Text (+10%)
                    if (rotateText)
                    {	// den ganzen Beschriftungsklumpatsch einfach umdrehen
                        l.Modify(ModOp.Rotate(c, plane.Normal, SweepAngle.Opposite));
                    }
                    double w = GetSymbolLength(p.LayoutFactor) * 1.1; // + 10%
                    double textdist = dimStyle.TextDist;
                    if (rotateText) textdist = -textdist; // es steht auf dem Kopf, also in die andere Richtung
                    GeoPoint2D txtLocation = radiuspoint + w * dir + ((textPos[0] - 0.5) * dimStyle.TextSize * 10) * dir + dimStyle.TextSize * textdist * GeoVector2D.YAxis;
                    GeoPoint2D txtLeft = new GeoPoint2D(rct.Left, (rct.Bottom + rct.Top) / 2.0);
                    GeoVector2D txtMove = txtLocation - txtLeft;
                    l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
                    rct.Move(txtMove);
                    list.AddRange(l);
                    AddDimensionLine(list, radiuspoint, radiuspoint + (w + rct.Width) * dir, 0);
                    AddSymbol(list, radiuspoint, dir.Angle, p.LayoutFactor, false);
                }
            }
        }
        private void RecalcDiameter(Projection p, GeoObjectList list)
        {	// Bemaßung gegeben durch points[0]: Mittelpunkt, normal: Normale der Kreisebene,
            // radius: der Radius, dimLineRef: hierdurch geht die Maßlinie, unterscheiden innerhalb/außerhalb
            // dimLineRef ist auch ggf der Abknickpunkt
            if (dimStyle == null) return;
            if (points.Length < 1) return; // wir brauchen nur einen Punkt (MIttelpunkt)
            try
            {
                dimLineDirection = dimLineRef - points[0];
                plane = new Plane(points[0], dimLineDirection, normal ^ dimLineDirection);
            }
            catch (PlaneException) { return; } // Ebene nicht bestimmbar, was also tun?
            GeoVector2D hor = plane.Project(p.ProjectionPlane.DirectionX);
            hor.Norm();
            bool rotateText = hor.x < 0;

            GeoPoint2D center = plane.Project(points[0]); // müsste (0,0) sein
            GeoVector2D dir = plane.Project(dimLineDirection);
            dir.Norm();
            GeoPoint2D radiuspoint = center + radius * dir;
            GeoPoint2D p1 = plane.Project(dimLineRef); // dimLineRef in der Ebene, dort knick es ggf ab
            bool inside = Geometry.Dist(center, p1) < radius; // Bemaßung von innen
            if (inside)
            {
                GeoPoint2D oppositePoint = center - radius * dir;
                AddDimensionLine(list, oppositePoint, radiuspoint, 0);
                AddSymbol(list, radiuspoint, dir.Opposite().Angle, p.LayoutFactor, false);
                AddSymbol(list, oppositePoint, dir.Angle, p.LayoutFactor, false);
                GeoObjectList l = MakeText(p.LayoutFactor, 0);
                BoundingRect rct = l.GetExtent(plane.CoordSys.GetProjection(), true, false);
                GeoPoint c = plane.ToGlobal(rct.GetCenter());
                if (rotateText)
                {	// den ganzen Beschriftungsklumpatsch einfach umdrehen
                    l.Modify(ModOp.Rotate(c, plane.Normal, SweepAngle.Opposite));
                }
                double textdist = dimStyle.TextDist;
                if (rotateText) textdist = -textdist; // es steht auf dem Kopf, also in die andere Richtung
                GeoPoint2D txtLocation = oppositePoint + textPos[0] * (radiuspoint - oppositePoint) + dimStyle.TextSize * textdist * GeoVector2D.YAxis;

                GeoVector2D txtMove = txtLocation - rct.GetCenter();
                l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
                rct.Move(txtMove);
                list.AddRange(l);
            }
            else
            {	// von außen identisch wie bei RadiusBemaßung
                GeoObjectList l = MakeText(p.LayoutFactor, 0);
                BoundingRect rct = l.GetExtent(plane.CoordSys.GetProjection(), true, false);
                GeoPoint c = plane.ToGlobal(rct.GetCenter());
                if (dimStyle.DimRadiusBend)
                {	// abknickend, d.h. Linie bis zu dimLineRef (p1) und dann horizontal weiter
                    // für den Text
                    AddDimensionLine(list, radiuspoint, p1, 0);
                    double w = GetSymbolLength(p.LayoutFactor) * 1.1; // + 10%

                    // den Text gemäß hor drehen
                    l.Modify(ModOp.Rotate(c, plane.Normal, new SweepAngle(hor.Angle.Radian)));
                    if (hor.x < 0.0) hor = -hor; // hor umkeheren wenn der Strich nach links geht
                    double textdist = dimStyle.TextDist;
                    if (rotateText) textdist = -textdist; // es steht auf dem Kopf, also in die andere Richtung
                    GeoPoint2D txtLocation = p1 + (rct.Width / 2) * hor + ((textPos[0] - 0.5) * dimStyle.TextSize * 10) * hor + dimStyle.TextSize * textdist * hor.ToLeft();
                    GeoVector2D txtMove = txtLocation - rct.GetCenter();
                    l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
                    list.AddRange(l);

                    AddDimensionLine(list, p1, p1 + (rct.Width * 1.1) * hor, 0);
                    AddSymbol(list, radiuspoint, dir.Angle, p.LayoutFactor, false);
                }
                else
                {	// gerade außerhalb, Maßlinie genausolang wie der Text (+10%)
                    if (rotateText)
                    {	// den ganzen Beschriftungsklumpatsch einfach umdrehen
                        l.Modify(ModOp.Rotate(c, plane.Normal, SweepAngle.Opposite));
                    }
                    double w = GetSymbolLength(p.LayoutFactor) * 1.1; // + 10%
                    double textdist = dimStyle.TextDist;
                    if (rotateText) textdist = -textdist; // es steht auf dem Kopf, also in die andere Richtung
                    GeoPoint2D txtLocation = radiuspoint + w * dir + dimStyle.TextSize * textdist * GeoVector2D.YAxis;
                    GeoPoint2D txtLeft = new GeoPoint2D(rct.Left, (rct.Bottom + rct.Top) / 2.0);
                    GeoVector2D txtMove = txtLocation - txtLeft;
                    l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
                    rct.Move(txtMove);
                    list.AddRange(l);
                    AddDimensionLine(list, radiuspoint, radiuspoint + (w + rct.Width) * dir, 0);
                    AddSymbol(list, radiuspoint, dir.Angle, p.LayoutFactor, false);
                }
            }
        }
        private void RecalcLocation(Projection p, GeoObjectList list)
        {	// Bemaßung gegeben durch points[i]: die Ausgangspunkte
            // dimLineRef ist der Abknickpunkt
            if (dimStyle == null) return;
            if (points.Length < 1) return; // wir brauchen mindesten einen Punkt
            try
            {
                dimLineDirection = dimLineRef - points[0];
                plane = new Plane(dimLineRef, normal);
                //plane.Align(p.ProjectionPlane, false, true);
            }
            catch (PlaneException) { return; } // Ebene nicht bestimmbar, was also tun?
            GeoVector2D hor = plane.Project(p.ProjectionPlane.DirectionX);
            hor.Norm(); // das muss (1,0) sein, oder?

            GeoPoint2D bend = plane.Project(dimLineRef); // müsste (0,0) sein
            bool toRight = true; // nach welcher Seite geht der waagrechte Strich
            for (int i = 0; i < points.Length; ++i)
            {
                GeoPoint2D center = plane.Project(points[i]);
                if (i == 0) toRight = center.x < 0.0;
                AddDimensionLine(list, center, bend, i);
                AddSymbol(list, center, (bend - center).Angle, p.LayoutFactor, false);
            }
            GeoObjectList l = MakeText(p.LayoutFactor, 0);
            BoundingRect rct = l.GetExtent(plane.CoordSys.GetProjection(), true, false);
            GeoPoint2D p2 = bend;
            if (toRight) p2.x += rct.Width;
            else p2.x -= rct.Width;
            AddDimensionLine(list, bend, p2, 0);
            GeoPoint c = plane.ToGlobal(rct.GetCenter());
            double textdist = dimStyle.TextDist;
            GeoPoint2D txtLocation = bend + textPos[0] * (p2 - bend) + new GeoVector2D(0.0, textdist * p.LayoutFactor);
            GeoVector2D txtMove = txtLocation - rct.GetCenter();
            l.Modify(ModOp.Translate(plane.ToGlobal(txtMove)));
            list.AddRange(l);
        }
        public void Recalc(Projection p, GeoObjectList list)
        {
            ++isChanging; // während Recalc kein Changing senden (wg. Texteditor)
            try
            {
                switch (dimType)
                {
                    case EDimType.DimPoints: RecalcPoints(p, list); break;
                    case EDimType.DimCoord: RecalcCoord(p, list); break;
                    case EDimType.DimAngle: RecalcAngle(p, list); break;
                    case EDimType.DimRadius: RecalcRadius(p, list); break;
                    case EDimType.DimDiameter: RecalcDiameter(p, list); break;
                    case EDimType.DimLocation: RecalcLocation(p, list); break;
                }
            }
            finally
            {
                --isChanging;
            }
            extent = list.GetExtent(); // cache the extent. When calling GetBoundingCube the projection is unknown
        }
        #endregion
        public void AddPoint(GeoPoint p)
        {
            using (new Changing(this, "RemovePoint", points.Length))
            {
                ArrayList pp;
                pp = new ArrayList(points);
                pp.Add(p);
                points = (GeoPoint[])pp.ToArray(typeof(GeoPoint));
                if ((dimType == EDimType.DimPoints || dimType == EDimType.DimCoord))
                {	// in die Textfelder hinten ein leeres anhängen
                    // diese Felder werden später mit den Punkten synchron sortiert
                    if (points.Length > 1)
                    {
                        double[] newtextPos = new double[points.Length - 1];
                        string[] newdimText = new string[points.Length - 1];
                        string[] newtolPlusText = new string[points.Length - 1];
                        string[] newtolMinusText = new string[points.Length - 1];
                        string[] newprefix = new string[points.Length - 1];
                        string[] newpostfix = new string[points.Length - 1];
                        string[] newpostfixAlt = new string[points.Length - 1];
                        if (points.Length >= 2)
                        {
                            newtextPos[points.Length - 2] = 0.5;
                            if (points.Length > 2)
                            {	// vorher gabs noch keine arrays
                                Array.Copy(textPos, 0, newtextPos, 0, points.Length - 2);
                                Array.Copy(dimText, 0, newdimText, 0, points.Length - 2);
                                Array.Copy(tolPlusText, 0, newtolPlusText, 0, points.Length - 2);
                                Array.Copy(tolMinusText, 0, newtolMinusText, 0, points.Length - 2);
                                Array.Copy(prefix, 0, newprefix, 0, points.Length - 2);
                                Array.Copy(postfix, 0, newpostfix, 0, points.Length - 2);
                                Array.Copy(postfixAlt, 0, newpostfixAlt, 0, points.Length - 2);
                            }
                        }
                        textPos = newtextPos;
                        dimText = newdimText;
                        tolPlusText = newtolPlusText;
                        tolMinusText = newtolMinusText;
                        prefix = newprefix;
                        postfix = newpostfix;
                        postfixAlt = newpostfixAlt;
                    }
                }
                else
                {
                    // Winkel, Radien oder Durchmesserbemaßung:
                    // nur einmal leere Arrays erzeugen
                    if (textPos == null)
                    {
                        textPos = new double[1];
                        textPos[0] = 0.5;
                        dimText = new string[1];
                        tolPlusText = new string[1];
                        tolMinusText = new string[1];
                        prefix = new string[1];
                        postfix = new string[1];
                        postfixAlt = new string[1];
                    }
                }
            }
        }
        public void RemovePoint(int Index)
        {
            using (new Changing(this, "AddPoint", points[Index]))
            {
                ArrayList pp = new ArrayList(points);
                pp.RemoveAt(Index);
                points = (GeoPoint[])pp.ToArray(typeof(GeoPoint));
            }
        }
        public int PointCount
        {
            get { return points.Length; }
        }
        public GeoPoint GetPoint(int Index)
        {
            return points[Index];
        }
        public void SetPoint(int Index, GeoPoint p)
        {
            using (new Changing(this, "SetPoint", Index, points[Index]))
            {
                points[Index] = p;
            }
        }
        public EDimType DimType
        {
            get { return dimType; }
            set
            {
                using (new Changing(this, "DimType"))
                {
                    dimType = value;
                    if ((dimType != EDimType.DimPoints) && (dimType != EDimType.DimCoord))
                    {
                    }
                }
            }
        }
        public GeoPoint DimLineRef
        {
            get { return dimLineRef; }
            set
            {
                using (new Changing(this, "DimLineRef"))
                {
                    dimLineRef = value;
                }
            }
        }
        public GeoVector DimLineDirection
        {
            get { return dimLineDirection; }
            set
            {
                using (new Changing(this, "DimLineDirection"))
                {
                    dimLineDirection = value;
                }
            }
        }
        public Angle ExtLineAngle
        {
            get { return extLineAngle; }
            set
            {
                using (new Changing(this, "ExtLineAngle"))
                {
                    extLineAngle = value;
                }
            }
        }
        public Angle StartAngle
        {
            get { return startAngle; }
            set
            {
                using (new Changing(this, "StartAngle"))
                {
                    startAngle = value;
                }
            }
        }
        public Angle EndAngle
        {
            get { return endAngle; }
            set
            {
                using (new Changing(this, "EndAngle"))
                {
                    endAngle = value;
                }
            }
        }
        public double Radius
        {
            get { return radius; }
            set
            {
                using (new Changing(this, "Radius"))
                {
                    radius = value;
                }
            }
        }
        public double DistToStart
        {
            get { return distToStart; }
            set
            {
                using (new Changing(this, "DistToStart"))
                {
                    distToStart = value;
                }
            }
        }
        public Angle DistToEnd
        {
            get { return distToEnd; }
            set
            {
                using (new Changing(this, "DistToEnd"))
                {
                    distToEnd = value;
                }
            }
        }
        public double GetTextPos(int index)
        {
            return textPos[index];
        }
        internal void SetTextPosCoordinate(int index, Projection pr, GeoPoint pos)
        {
            GeoPoint2D p1, p2, p3, p4; // ein case klammert nicht
            switch (dimType)
            {
                case EDimType.DimAngle:
                    p1 = plane.Project(points[0]); // Mittelpunkt
                    p2 = plane.Project(points[1]); // StartWinkel
                    p3 = plane.Project(points[2]); // EndWinkel
                    p4 = plane.Project(dimLineRef); // Radiuspunkt
                    Angle start = (p2 - p1).Angle;
                    Angle end = (p3 - p1).Angle;
                    Arc2D a2d = new Arc2D(p1, Geometry.Dist(p4, p1), start, new SweepAngle(start, end, true));
                    SetTextPos(0, a2d.PositionOf(plane.Project(pos)));
                    break;
                case EDimType.DimRadius:
                    {
                        p1 = plane.Project(points[0]); // Mittelpunkt
                        p2 = plane.Project(dimLineRef); // ggf. Knickpunkt
                        // p3,p4 werden die beiden Linienpunkte für die Grundlinie
                        if (Geometry.Dist(p1, p2) < radius)
                        {	// innerhalb
                            GeoVector2D dir = p2 - p1; dir.Norm();
                            p3 = p1;
                            p4 = p1 + radius * dir;
                        }
                        else
                        {
                            if (dimStyle.DimRadiusBend)
                            {
                                GeoVector2D hor = plane.Project(pr.ProjectionPlane.DirectionX);
                                hor.Norm();
                                if (hor.x < 0.0) hor = -hor; // hor umkeheren wenn der Strich nach links geht
                                p3 = p2;
                                p4 = p2 + (dimStyle.TextSize * 10) * hor; // damit was rauskommt
                            }
                            else
                            {
                                GeoVector2D dir = p2 - p1; dir.Norm();
                                p3 = p1 + radius * dir;
                                // die Textlänge ist leider unbakannt, deshalb einfach 10*Textgröße
                                p4 = p3 + (dimStyle.TextSize * 10.0) * dir;
                            }
                        }
                        Line2D l2d = new Line2D(p3, p4);
                        SetTextPos(0, l2d.PositionOf(plane.Project(pos)));
                    }
                    break;
                case EDimType.DimDiameter:
                    {
                        p1 = plane.Project(points[0]); // Mittelpunkt
                        p2 = plane.Project(dimLineRef); // ggf. Knickpunkt
                        // p3,p4 werden die beiden Linienpunkte für die Grundlinie
                        if (Geometry.Dist(p1, p2) < radius)
                        {	// innerhalb
                            GeoVector2D dir = p2 - p1; dir.Norm();
                            p3 = p1 - radius * dir;
                            p4 = p1 + radius * dir;
                        }
                        else
                        {
                            if (dimStyle.DimRadiusBend)
                            {
                                GeoVector2D hor = plane.Project(pr.ProjectionPlane.DirectionX);
                                hor.Norm();
                                if (hor.x < 0.0) hor = -hor; // hor umkeheren wenn der Strich nach links geht
                                p3 = p2;
                                p4 = p2 + (dimStyle.TextSize * 10) * hor; // damit was rauskommt
                            }
                            else
                            {
                                GeoVector2D dir = p2 - p1; dir.Norm();
                                p3 = p1 + radius * dir;
                                // die Textlänge ist leider unbakannt, deshalb einfach 10*Textgröße
                                p4 = p3 + (dimStyle.TextSize * 10.0) * dir;
                            }
                        }
                        Line2D l2d = new Line2D(p3, p4);
                        SetTextPos(0, l2d.PositionOf(plane.Project(pos)));
                    }
                    break;
                case EDimType.DimCoord:
                    {
                        if (dimStyle.DimMultiLine)
                        {
                            p1 = plane.Project(points[index]);
                            p2 = plane.Project(points[index + 1]);
                            p1.y = 0.0;
                            p2.y = 0.0;
                            Line2D l2d = new Line2D(p1, p2);
                            SetTextPos(index, l2d.PositionOf(plane.Project(pos)));
                        }
                        else
                        {	// senkrechter Text
                            p1 = plane.Project(points[index + 1]);
                            double d = -10.0;
                            if (p1.y < 0) d = -d;
                            p1.y = 0;
                            p2 = p1;
                            p2.y = dimStyle.TextSize * d;
                            Line2D l2d = new Line2D(p1, p2);
                            SetTextPos(index, l2d.PositionOf(plane.Project(pos)));
                        }
                    }
                    break;
                case EDimType.DimLocation:
                    {
                        GeoVector2D hor = plane.Project(pr.ProjectionPlane.DirectionX);
                        hor.Norm(); // das muss (1,0) sein, oder?
                        p3 = plane.Project(dimLineRef); // Knickpunkt
                        p4 = p3 + (dimStyle.TextSize * 10) * hor;
                        Line2D l2d = new Line2D(p3, p4);
                        SetTextPos(index, l2d.PositionOf(plane.Project(pos)));
                    }
                    break;
                case EDimType.DimPoints:
                    {
                        p1 = plane.Project(points[index]);
                        p2 = plane.Project(points[index + 1]);
                        p1.y = 0.0;
                        p2.y = 0.0;
                        Line2D l2d = new Line2D(p1, p2);
                        SetTextPos(index, l2d.PositionOf(plane.Project(pos)));
                    }
                    break;
            }
        }
        internal GeoPoint GetTextPosCoordinate(int index, Projection pr)
        {
            GeoPoint2D p1, p2, p3, p4; // ein case klammert nicht
            switch (dimType)
            {
                case EDimType.DimAngle:
                    p1 = plane.Project(points[0]); // Mittelpunkt
                    p2 = plane.Project(points[1]); // StartWinkel
                    p3 = plane.Project(points[2]); // EndWinkel
                    p4 = plane.Project(dimLineRef); // Radiuspunkt
                    Angle start = (p2 - p1).Angle;
                    Angle end = (p3 - p1).Angle;
                    Arc2D a2d = new Arc2D(p1, Geometry.Dist(p4, p1), start, new SweepAngle(start, end, true));
                    return plane.ToGlobal(a2d.PointAt(textPos[0]));
                case EDimType.DimRadius:
                    {
                        p1 = plane.Project(points[0]); // Mittelpunkt
                        p2 = plane.Project(dimLineRef); // ggf. Knickpunkt
                        // p3,p4 werden die beiden Linienpunkte für die Grundlinie
                        if (Geometry.Dist(p1, p2) < radius)
                        {	// innerhalb
                            GeoVector2D dir = p2 - p1; dir.Norm();
                            p3 = p1;
                            p4 = p1 + radius * dir;
                        }
                        else
                        {
                            if (dimStyle.DimRadiusBend)
                            {
                                GeoVector2D hor = plane.Project(pr.ProjectionPlane.DirectionX);
                                hor.Norm();
                                if (hor.x < 0.0) hor = -hor; // hor umkeheren wenn der Strich nach links geht
                                p3 = p2;
                                p4 = p2 + (dimStyle.TextSize * 10) * hor; // damit was rauskommt
                            }
                            else
                            {
                                GeoVector2D dir = p2 - p1; dir.Norm();
                                p3 = p1 + radius * dir;
                                // die Textlänge ist leider unbakannt, deshalb einfach 10*Textgröße
                                p4 = p3 + (dimStyle.TextSize * 10.0) * dir;
                            }
                        }
                        Line2D l2d = new Line2D(p3, p4);
                        return plane.ToGlobal(l2d.PointAt(textPos[0]));
                    }
                case EDimType.DimDiameter:
                    {
                        p1 = plane.Project(points[0]); // Mittelpunkt
                        p2 = plane.Project(dimLineRef); // ggf. Knickpunkt
                        // p3,p4 werden die beiden Linienpunkte für die Grundlinie
                        if (Geometry.Dist(p1, p2) < radius)
                        {	// innerhalb
                            GeoVector2D dir = p2 - p1;
                            if (!Precision.IsNullVector(dir)) dir.Norm();
                            p3 = p1 - radius * dir;
                            p4 = p1 + radius * dir;
                        }
                        else
                        {
                            if (dimStyle.DimRadiusBend)
                            {
                                GeoVector2D hor = plane.Project(pr.ProjectionPlane.DirectionX);
                                if (!Precision.IsNullVector(hor)) hor.Norm();
                                if (hor.x < 0.0) hor = -hor; // hor umkeheren wenn der Strich nach links geht
                                p3 = p2;
                                p4 = p2 + (dimStyle.TextSize * 10) * hor; // damit was rauskommt
                            }
                            else
                            {
                                GeoVector2D dir = p2 - p1;
                                if (!Precision.IsNullVector(dir)) dir.Norm();
                                p3 = p1 + radius * dir;
                                // die Textlänge ist leider unbakannt, deshalb einfach 10*Textgröße
                                p4 = p3 + (dimStyle.TextSize * 10.0) * dir;
                            }
                        }
                        Line2D l2d = new Line2D(p3, p4);
                        return plane.ToGlobal(l2d.PointAt(textPos[0]));
                    }
                case EDimType.DimCoord:
                    if (dimStyle.DimMultiLine)
                    {
                        p1 = plane.Project(points[index]);
                        p2 = plane.Project(points[index + 1]);
                        return plane.ToGlobal(new GeoPoint2D(p1.x + textPos[index] * (p2.x - p1.x), 0.0));
                    }
                    else
                    {
                        p1 = plane.Project(points[index + 1]);
                        double d = -10.0;
                        if (p1.y < 0) d = -d;
                        return plane.ToGlobal(new GeoPoint2D(p1.x, textPos[index] * dimStyle.TextSize * d));
                    }
                case EDimType.DimLocation:
                    {
                        GeoVector2D hor = plane.Project(pr.ProjectionPlane.DirectionX);
                        hor.Norm(); // das muss (1,0) sein, oder?
                        p3 = plane.Project(dimLineRef); // Knickpunkt
                        p4 = p3 + (dimStyle.TextSize * 10) * hor;
                        Line2D l2d = new Line2D(p3, p4);
                        return plane.ToGlobal(l2d.PointAt(textPos[0]));
                    }
                default: // damits sonst kein return braucht
                case EDimType.DimPoints:
                    p1 = plane.Project(points[index]);
                    p2 = plane.Project(points[index + 1]);
                    return plane.ToGlobal(new GeoPoint2D(p1.x + textPos[index] * (p2.x - p1.x), 0.0));
            }
        }
        public void SetTextPos(int index, double val)
        {
            using (new Changing(this, "SetTextPos", index, textPos[index]))
            {
                textPos[index] = val;
            }
        }
        public string GetDimText(int index)
        {
            if (dimText[index] != null) return dimText[index];
            switch (dimType)
            {
                case EDimType.DimAngle:
                    SweepAngle sa = new SweepAngle(plane.Project(points[1] - points[0]), plane.Project(points[2] - points[0]));
                    if (sa.Radian < 0.0) sa += SweepAngle.Full;
                    if (dimStyle.AngleText == DimensionStyle.EAngleText.ArcLength)
                    {
                        double radius = Geometry.Dist(points[1], points[0]);
                        return FormatAngleValue(radius * sa.Radian);
                    }
                    else
                    {
                        return FormatAngleValue(sa.Radian);
                    }
                case EDimType.DimPoints:
                    {
                        GeoPoint2D p1 = plane.Project(points[index]);
                        GeoPoint2D p2 = plane.Project(points[index + 1]);
                        return FormatValue(Math.Abs(p2.x - p1.x) * dimStyle.Scale);
                    }
                case EDimType.DimCoord:
                    {
                        GeoPoint2D p1 = plane.Project(points[0]);
                        GeoPoint2D p2 = plane.Project(points[index + 1]);
                        return FormatValue(Math.Abs(p2.x - p1.x));
                    }
                case EDimType.DimRadius:
                    {
                        return FormatValue(radius * dimStyle.Scale);
                    }
                case EDimType.DimDiameter:
                    {
                        return FormatValue(2.0 * radius * dimStyle.Scale);
                    }
                case EDimType.DimLocation:
                    {	// Beschriftung oder Punkt-Koordinatenbemaßung
                        return "(" + FormatValue(points[0].x) + ";" + FormatValue(points[0].y) + ";" + FormatValue(points[0].z) + ")";
                    }
                default: return "";
            }
        }
        public void SetDimText(int index, string text)
        {
            string newValue = text;

            if (string.IsNullOrEmpty(text))
                newValue = null;

            if (dimText[index] == newValue)
                return;

            using (new Changing(this, "SetDimText", index, dimText))
            {
                dimText[index] = newValue;
            }
        }
        public string GetTolPlusText(int index)
        {
            if (tolPlusText[index] != null) return tolPlusText[index];
            string res = FormatValue(dimStyle.PlusTolerance);
            if (res.Length > 0 && res[0] != '-' && dimStyle.PlusTolerance != 0.0) res = "+" + res; // Vorzeichen erzwingen
            return res;
        }
        public void SetTolPlusText(int index, string txt)
        {
            string newValue = txt;

            if (string.IsNullOrEmpty(txt))
                newValue = null;

            if (tolPlusText[index] == newValue)
                return;

            using (new Changing(this, "SetTolPlusText", index, tolPlusText[index]))
            {
                tolPlusText[index] = newValue;
            }
        }
        public string GetTolMinusText(int index)
        {
            if (tolMinusText[index] != null) return tolMinusText[index];
            string res = FormatValue(dimStyle.MinusTolerance);
            if (res.Length > 0 && res[0] != '-' && dimStyle.MinusTolerance != 0.0) res = "+" + res; // Vorzeichen erzwingen
            return res;
        }
        public void SetTolMinusText(int index, string txt)
        {
            string newValue = txt;

            if (string.IsNullOrEmpty(txt))
                newValue = null;

            if (tolMinusText[index] == newValue)
                return;

            using (new Changing(this, "SetTolMinusText", index, tolMinusText[index]))
            {
                tolMinusText[index] = newValue;
            }
        }
        public string GetPrefix(int index)
        {
            if (prefix[index] != null) return prefix[index];
            return dimStyle.TextPrefix;
        }
        public void SetPrefix(int index, string txt)
        {
            string newValue = txt;

            if (string.IsNullOrEmpty(txt))
                newValue = null;

            if (prefix[index] == newValue)
                return;

            using (new Changing(this, "SetPrefix", index, prefix[index]))
            {
                prefix[index] = newValue;
            }
        }
        public string GetPostfix(int index)
        {
            if (postfix[index] != null) return postfix[index];
            return dimStyle.TextPostfix;
        }
        public void SetPostfix(int index, string txt)
        {
            string newValue = txt;

            if (string.IsNullOrEmpty(txt))
                newValue = null;

            if (postfix[index] == newValue)
                return;

            using (new Changing(this, "SetPostfix", index, postfix[index]))
            {
                postfix[index] = newValue;
            }
        }
        public string GetPostfixAlt(int index)
        {
            if (postfixAlt[index] != null) return postfixAlt[index];
            return dimStyle.TextPostfixAlt;
        }
        public void SetPostfixAlt(int index, string txt)
        {
            string newValue = txt;

            if (string.IsNullOrEmpty(txt))
                newValue = null;

            if (postfixAlt[index] == newValue)
                return;

            using (new Changing(this, "SetPostfixAlt", index, postfixAlt[index]))
            {
                postfixAlt[index] = newValue;
            }
        }
        public GeoVector Normal
        {
            get { return normal; }
            set { normal = value; }
        }
        internal GeoObjectList GetList()
        {
            GeoObjectList list = new GeoObjectList();
            try
            {
                // Recalc(new Projection(-Plane.Normal, plane.DirectionX), list);
                Recalc(new Projection(-Plane.Normal, Precision.SameDirection(plane.Normal, GeoVector.ZAxis, false) ? plane.DirectionY : GeoVector.ZAxis), list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
            }
            return list;
        }
        public Plane Plane
        {
            get
            {
                return plane;
            }
        }
        internal GeoPoint FindTextPosition(int index)
        {
            // AutoCad erwartet die Mitte des gesamten Textes, inclusive Prefix und Postfix
            // und Toleranzen hier. Es wird die entsprechende Liste gesammelt und dann der Mittlepunkt bestimmt
            GeoObjectList list = GetList();
            GeoObjectList textlist = new GeoObjectList();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].UserData.ContainsData("CADability.Dimension.Text"))
                {
                    if (((int)list[i].UserData["CADability.Dimension.Text"]) == index)
                    {
                        textlist.Add(list[i]);
                    }
                }
                if (list[i].UserData.ContainsData("CADability.Dimension.Prefix"))
                {
                    if (((int)list[i].UserData["CADability.Dimension.Prefix"]) == index)
                    {
                        textlist.Add(list[i]);
                    }
                }
                if (list[i].UserData.ContainsData("CADability.Dimension.PostFix"))
                {
                    if (((int)list[i].UserData["CADability.Dimension.PostFix"]) == index)
                    {
                        textlist.Add(list[i]);
                    }
                }
                if (list[i].UserData.ContainsData("CADability.Dimension.TolPlus"))
                {
                    if (((int)list[i].UserData["CADability.Dimension.TolPlus"]) == index)
                    {
                        textlist.Add(list[i]);
                    }
                }
                if (list[i].UserData.ContainsData("CADability.Dimension.TolMinus"))
                {
                    if (((int)list[i].UserData["CADability.Dimension.TolMinus"]) == index)
                    {
                        textlist.Add(list[i]);
                    }
                }
            }
            if (textlist.Count > 0)
            {
                return textlist.GetExtent().GetCenter();
            }
            return GetTextPosCoordinate(index, Projection.FromTop); // Notlösung, wenns nicht klappt
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Decompose ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoObjectList Decompose()
        {
            return GetList();
        }
        #region SortingPoints
        private class SortingPoints : IComparer
        {
            Dimension dimension;
            public SortingPoints(Dimension dimension)
            {
                this.dimension = dimension;
            }
            #region IComparer Members

            public int Compare(object x, object y)
            {	// die beiden Bemaßungspunkte checken, die x-Komponente 
                // in der zugehörigen Ebene gibt die Sortierung an
                int i1 = (int)x;
                int i2 = (int)y;
                GeoPoint2D p1 = dimension.plane.Project(dimension.points[i1]);
                GeoPoint2D p2 = dimension.plane.Project(dimension.points[i2]);
                if (p1.x < p2.x) return -1;
                if (p1.x > p2.x) return 1;
                return 0;
            }

            #endregion
        }
        #endregion
        /// <summary>
        /// Call this after a point to a dimension of type EDimType.DimCoord or EDimType.DimPoints
        /// has been added or changed, to rorder the list of points
        /// </summary>
        public void SortPoints()
        {
            if (dimType == EDimType.DimPoints || dimType == EDimType.DimCoord)
            {
                int[] toSort = new int[points.Length];
                for (int i = 0; i < toSort.Length; ++i)
                {
                    toSort[i] = i;
                }
                if (!plane.IsValid())
                {
                    try
                    {
                        plane = new Plane(dimLineRef, dimLineDirection, normal ^ dimLineDirection);
                    }
                    catch (PlaneException) { return; } // Ebene nicht bestimmbar, was also tun?
                }
                if (dimType == EDimType.DimCoord)
                {	// der 1. Index soll nicht mit sortiert werden
                    Array.Sort(toSort, 1, toSort.Length - 1, new SortingPoints(this));
                }
                else
                {
                    Array.Sort(toSort, new SortingPoints(this));
                }
                // toSort ist jetzt eine Indexliste der neuen Sortierung
                GeoPoint[] newpoints = new GeoPoint[points.Length];
                for (int i = 0; i < points.Length; ++i)
                {
                    newpoints[i] = points[toSort[i]]; // oder umgekehrt?
                }
                points = newpoints;

                double[] newtextPos = new double[points.Length - 1];
                string[] newdimText = new string[points.Length - 1];
                string[] newtolPlusText = new string[points.Length - 1];
                string[] newtolMinusText = new string[points.Length - 1];
                string[] newprefix = new string[points.Length - 1];
                string[] newpostfix = new string[points.Length - 1];
                for (int i = 0; i < points.Length - 1; ++i)
                {
                    int ind = toSort[i];
                    if (ind == points.Length - 1) ind = points.Length - 2;
                    newtextPos[i] = textPos[ind];
                    newdimText[i] = dimText[ind];
                    newtolPlusText[i] = tolPlusText[ind];
                    newtolMinusText[i] = tolMinusText[ind];
                    newprefix[i] = prefix[ind];
                    newpostfix[i] = postfix[ind];
                }
                textPos = newtextPos;
                dimText = newdimText;
                tolPlusText = newtolPlusText;
                tolMinusText = newtolMinusText;
                prefix = newprefix;
                postfix = newpostfix;
            }
        }
        [Flags]
        public enum HitPosition { DimLine = 0x01, Prefix = 0x02, Text = 0x04, PostFix = 0x08, UpperText = 0x10, LowerText = 0x20, AltText = 0x40, PostFixAlt = 0x80 }
        public HitPosition GetHitPosition(Projection p, GeoPoint2D projectedPoint, out int Index)
        {
            HitPosition res = 0;
            Index = -1;
            //double w = 5.0 * p.DeviceToWorldFactor; // TODO: auf settings zurückgreifen
            //BoundingRect rect = new BoundingRect(projectedPoint, w, w);
            //I2DRepresentation[] rep2d = Get2DRepresentation(p, null);
            //for (int i = 0; i < rep2d.Length; ++i)
            //{
            //    if (rep2d[i].HitTest(ref rect, false))
            //    {
            //        if (rep2d[i].UserData.ContainsData("CADability.Dimension.DimensionLine"))
            //        {
            //            res |= HitPosition.DimLine;
            //            if (Index == -1) Index = (int)rep2d[i].UserData["CADability.Dimension.DimensionLine"];
            //        }
            //        if (rep2d[i].UserData.ContainsData("CADability.Dimension.Prefix"))
            //        {
            //            res |= HitPosition.Prefix;
            //            if (Index == -1) Index = (int)rep2d[i].UserData["CADability.Dimension.Prefix"];
            //        }
            //        if (rep2d[i].UserData.ContainsData("CADability.Dimension.Text"))
            //        {
            //            res |= HitPosition.Text;
            //            if (Index == -1) Index = (int)rep2d[i].UserData["CADability.Dimension.Text"];
            //        }
            //        if (rep2d[i].UserData.ContainsData("CADability.Dimension.PostFix"))
            //        {
            //            res |= HitPosition.PostFix;
            //            if (Index == -1) Index = (int)rep2d[i].UserData["CADability.Dimension.PostFix"];
            //        }
            //        if (rep2d[i].UserData.ContainsData("CADability.Dimension.TolPlus"))
            //        {
            //            res |= HitPosition.UpperText;
            //            if (Index == -1) Index = (int)rep2d[i].UserData["CADability.Dimension.TolPlus"];
            //        }
            //        if (rep2d[i].UserData.ContainsData("CADability.Dimension.TolMinus"))
            //        {
            //            res |= HitPosition.LowerText;
            //            if (Index == -1) Index = (int)rep2d[i].UserData["CADability.Dimension.TolMinus"];
            //        }
            //    }
            //}
            return res;
        }
        internal Text EditText(Projection pr, int index, EditingMode mode)
        {
            editingText = null; // jedenfalls den alten wegmachen
            editingTextIndex = index;
            editingMode = mode;
            GeoObjectList list = new GeoObjectList();
            Recalc(pr, list);
            return editingText; // der müsste ja jetzt neu entstanden sein
        }
        #region IGeoObject overrides
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                for (int i = 0; i < points.Length; ++i)
                {
                    points[i] = m * points[i];
                }
                dimLineRef = m * dimLineRef;
                plane.Modify(m);
                normal = m * normal;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            Dimension res = Construct();
            res.CopyAttributes(this);
            res.points = (GeoPoint[])points.Clone();
            res.dimStyle = dimStyle;
            res.dimType = dimType;
            res.dimLineRef = dimLineRef;
            res.dimLineDirection = dimLineDirection;
            res.extLineAngle = extLineAngle;
            res.startAngle = startAngle;
            res.endAngle = endAngle;
            res.radius = radius;
            res.distToStart = distToStart;
            res.distToEnd = distToEnd;
            res.textPos = (double[])textPos.Clone();
            res.dimText = (string[])dimText.Clone();
            res.tolPlusText = (string[])tolPlusText.Clone();
            res.tolMinusText = (string[])tolMinusText.Clone();
            res.prefix = (string[])prefix.Clone();
            res.postfix = (string[])postfix.Clone();
            res.postfixAlt = (string[])postfixAlt.Clone();
            res.normal = normal;

            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            using (new Changing(this))
            {
                Dimension dim = ToCopyFrom as Dimension;
                this.points = (GeoPoint[])dim.points.Clone();
                this.dimStyle = dim.dimStyle;
                this.dimType = dim.dimType;
                this.dimLineRef = dim.dimLineRef;
                this.dimLineDirection = dim.dimLineDirection;
                this.extLineAngle = dim.extLineAngle;
                this.startAngle = dim.startAngle;
                this.endAngle = dim.endAngle;
                this.radius = dim.radius;
                this.distToStart = dim.distToStart;
                this.distToEnd = dim.distToEnd;
                this.textPos = dim.textPos;
                this.dimText = dim.dimText;
                this.tolPlusText = dim.tolPlusText;
                this.tolMinusText = dim.tolMinusText;
                this.prefix = dim.prefix;
                this.postfix = dim.postfix;
                this.postfixAlt = dim.postfixAlt;
                this.normal = dim.normal;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyDimension(this, Frame);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.IsAttributeUsed (object)"/>
        /// </summary>
        /// <param name="Attribute"></param>
        /// <returns></returns>
        public override bool IsAttributeUsed(object Attribute)
        {
            if (Attribute == dimStyle) return true;
            return base.IsAttributeUsed(Attribute);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetAttributeProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry[] GetAttributeProperties(IFrame Frame)
        {
            IPropertyEntry[] b = base.GetAttributeProperties(Frame);
            IPropertyEntry[] res = new IPropertyEntry[b.Length + 1];
            Array.Copy(b, 0, res, 0, b.Length);
            res[b.Length] = new DimensionStyleSelectionProperty("DimensionStyle.Selection", Frame.Project.DimensionStyleList, this, dimType, false);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            if (!extent.IsEmpty) return extent;
            GeoObjectList list = new GeoObjectList();
            try
            {
                // Recalc(new Projection(-Plane.Normal, plane.DirectionY), list);
                Recalc(new Projection(-Plane.Normal, Precision.SameDirection(plane.Normal, GeoVector.ZAxis, false) ? plane.DirectionY : GeoVector.ZAxis), list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
            }
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < list.Count; ++i)
            {
                res.MinMax(list[i].GetBoundingCube());
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrePaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            GeoObjectList list = new GeoObjectList();
            try
            {
                Recalc(new Projection(-Plane.Normal, Precision.SameDirection(plane.Normal, GeoVector.ZAxis, false) ? plane.DirectionY : GeoVector.ZAxis), list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
            }
            for (int i = 0; i < list.Count; ++i)
            {
                (list[i] as IGeoObjectImpl).PrePaintTo3D(paintTo3D);
            }
        }
        public delegate bool PaintTo3DDelegate(Dimension toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            GeoObjectList list = new GeoObjectList();
            try
            {
                Recalc(new Projection(-Plane.Normal, Precision.SameDirection(plane.Normal, GeoVector.ZAxis, false) ? plane.DirectionY : GeoVector.ZAxis), list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
            }
            for (int i = 0; i < list.Count; ++i)
            {
                (list[i] as IGeoObjectImpl).PaintTo3D(paintTo3D);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            // nichts zu tun
        }
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Dimension;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            GeoObjectList list = new GeoObjectList();
            try
            {
                Recalc(projection, list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException)
                    throw;
            }

            QuadTreeCollection res = new QuadTreeCollection(this, projection);

            foreach (IGeoObject geo in list)
                res.Add(geo.GetQuadTreeItem(projection, extentPrecision));

            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            GeoObjectList list = new GeoObjectList();
            try
            {
                Recalc(new Projection(-Plane.Normal, Precision.SameDirection(plane.Normal, GeoVector.ZAxis, false) ? plane.DirectionY : GeoVector.ZAxis), list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
            }
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < list.Count; ++i)
            {
                res.MinMax(list[i].GetExtent(projection, extentPrecision));
            }
            return res;
        }
        #endregion
        #region IOctTreeInsertable members
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override BoundingCube GetExtent(double precision)
        {
            return GetBoundingCube();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            GeoObjectList list = new GeoObjectList();
            try
            {
                Recalc(new Projection(-Plane.Normal, Precision.SameDirection(plane.Normal, GeoVector.ZAxis, false) ? plane.DirectionY : GeoVector.ZAxis), list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
            }
            for (int i = 0; i < list.Count; ++i)
            {
                if ((list[i] as IOctTreeInsertable).HitTest(ref cube, precision)) return true;
            }
            return false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection, BoundingRect, bool)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="rect"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            GeoObjectList list = new GeoObjectList();
            try
            {
                Recalc(projection, list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
            }
            if (onlyInside)
            {   // alle müssen ganz drin sein
                for (int i = 0; i < list.Count; ++i)
                {
                    if (!list[i].HitTest(projection, rect, onlyInside)) return false;
                }
                return true;
            }
            else
            {   // wenigsten eines muss teilweise drin sein
                for (int i = 0; i < list.Count; ++i)
                {
                    if (list[i].HitTest(projection, rect, onlyInside)) return true;
                }
                return false;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            GeoObjectList list = new GeoObjectList();
            try
            {
                Recalc(area.Projection, list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
            }
            if (onlyInside)
            {   // alle müssen ganz drin sein
                for (int i = 0; i < list.Count; ++i)
                {
                    if (!list[i].HitTest(area, onlyInside)) return false;
                }
                return true;
            }
            else
            {   // wenigsten eines muss teilweise drin sein
                for (int i = 0; i < list.Count; ++i)
                {
                    if (list[i].HitTest(area, onlyInside)) return true;
                }
                return false;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Position (GeoPoint, GeoVector, double)"/>
        /// </summary>
        /// <param name="fromHere"></param>
        /// <param name="direction"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            GeoObjectList list = new GeoObjectList();
            try
            {
                Recalc(new Projection(-Plane.Normal, Precision.SameDirection(plane.Normal, GeoVector.ZAxis, false) ? plane.DirectionY : GeoVector.ZAxis), list);
            }
            catch (Exception e)
            {
                if (e is ThreadAbortException) throw (e);
            }
            double res = double.MaxValue;
            for (int i = 0; i < list.Count; ++i)
            {
                double d = list[i].Position(fromHere, direction, precision);
                if (d < res) res = d;
            }
            return res;
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Dimension(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            // projectionData = new Hashtable();

            points = (GeoPoint[])info.GetValue("Points", typeof(GeoPoint[]));
            dimStyle = (DimensionStyle)info.GetValue("DimStyle", typeof(DimensionStyle));
            dimType = (EDimType)info.GetValue("DimType", typeof(EDimType));
            dimLineRef = (GeoPoint)info.GetValue("DimLineRef", typeof(GeoPoint));
            dimLineDirection = (GeoVector)info.GetValue("DimLineDirection", typeof(GeoVector));
            extLineAngle = (Angle)info.GetValue("ExtLineAngle", typeof(Angle));
            startAngle = (Angle)info.GetValue("StartAngle", typeof(Angle));
            endAngle = (Angle)info.GetValue("EndAngle", typeof(Angle));
            radius = (double)info.GetValue("Radius", typeof(double));
            distToStart = (double)info.GetValue("DistToStart", typeof(double));
            distToEnd = (double)info.GetValue("DistToEnd", typeof(double));
            textPos = (double[])info.GetValue("TextPos", typeof(double[]));
            dimText = (string[])info.GetValue("DimText", typeof(string[]));
            tolPlusText = (string[])info.GetValue("TolPlusText", typeof(string[]));
            tolMinusText = (string[])info.GetValue("TolMinusText", typeof(string[]));
            prefix = (string[])info.GetValue("Prefix", typeof(string[]));
            postfix = (string[])info.GetValue("Postfix", typeof(string[]));
            postfixAlt = (string[])info.GetValue("PostfixAlt", typeof(string[]));
            normal = (GeoVector)info.GetValue("Normal", typeof(GeoVector));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public new void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("Points", points);
            info.AddValue("DimStyle", dimStyle);
            info.AddValue("DimType", dimType);
            info.AddValue("DimLineRef", dimLineRef);
            info.AddValue("DimLineDirection", dimLineDirection);
            info.AddValue("ExtLineAngle", extLineAngle, typeof(Angle));
            info.AddValue("StartAngle", startAngle, typeof(Angle));
            info.AddValue("EndAngle", endAngle, typeof(Angle));
            info.AddValue("Radius", radius);
            info.AddValue("DistToStart", distToStart);
            info.AddValue("DistToEnd", distToEnd);
            info.AddValue("TextPos", textPos);
            info.AddValue("DimText", dimText);
            info.AddValue("TolPlusText", tolPlusText);
            info.AddValue("TolMinusText", tolMinusText);
            info.AddValue("Prefix", prefix);
            info.AddValue("Postfix", postfix);
            info.AddValue("PostfixAlt", postfixAlt);
            info.AddValue("Normal", normal);
        }

        #endregion
        #region IDimensionStyle Members

        public DimensionStyle DimensionStyle
        {
            get
            {
                return dimStyle;
            }
            set
            {
                using (new Changing(this, "DimensionStyle"))
                {
                    dimStyle = value;
                }
            }
        }
        /// <summary>
        /// Call this, if the DimensionStyle used by this Dimension was modified.
        /// </summary>
        public void Recalc()
        {
            using (new Changing(this, false)) { }
        }

        #endregion

        #region IGeoObjectOwner Members

        public void Remove(IGeoObject toRemove)
        {
            toRemove.Owner = null;
        }

        public void Add(IGeoObject toAdd)
        {
            toAdd.Owner = this;
        }

        #endregion
    }
}
