using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.Attribute
{
    /// <summary>
    /// An implementation of a <see cref="HatchStyle"/>, that defines a style consisting of
    /// contours inside the shape.
    /// </summary>
    [Serializable()]
    public class HatchStyleContour : HatchStyle, ISerializable
    {
        private double lineDistance; // Abstand der Kurven untereinander
        private double firstDistance; // Abstand zum rand
        /// <summary>
        /// How to proceed with holes in the area to be filled.
        /// </summary>
        public enum EHoleMode
        {
            /// <summary>
            /// Create curves that go around the holes.
            /// </summary>
            circumscribe,
            /// <summary>
            /// Simply break the curves that fill the area
            /// </summary>
            skip,
            /// <summary>
            /// circumscribe the first curve and skip all following curves
            /// </summary>
            both
        };
        private EHoleMode holeMode; // Löcher überspringen statt umfahren
        /// <summary>
        /// Parallel or continous filling modes
        /// </summary>
        public enum ESpiralMode
        {
            /// <summary>
            /// Creates parallel contours to the inside of the enclosing contours or the outside of the holes
            /// </summary>
            Parallel,
            /// <summary>
            /// Connects the parallel contours to one ore more paths to build a continuous contour theat goes 
            /// as a kind of spiral from the outside to the inside
            /// </summary>
            ContourSpiral,
            /// <summary>
            /// Ctreates a round spiral that couvers the whole form and is clipped by the definig CompoundShape
            /// </summary>
            RoundSpiral,
            /// <summary>
            /// Same as ContourSpiral, but the outermost contour is always closed
            /// </summary>
            ContourSpiralClosed
        }
        private ESpiralMode spiralMode;
        private bool counterClock; // Richtung der Konturen
        private bool inbound; // von außen nach innen
        // Linieattribute
        private LineWidth lineWidth;
        private LinePattern linePattern;
        private ColorDef colorDef;
        // für IShowProperty:
        private IPropertyEntry[] subItems;
        // für GenerateContent
        class BorderTree
        {   // von außen nach innen gehender Baum der Äquidistanten, oft nur eine einfache Kette
            // machmal in zwei oder mehrere Konturen aufgeteilt
            public BorderTree(Border bdr)
            {
                border = bdr;
                next = new List<BorderTree>();
            }
            public Border border;
            public List<BorderTree> next;
#if DEBUG
            void getAll(List<Border> list)
            {
                list.Add(border);
                for (int i = 0; i < next.Count; ++i)
                {
                    next[i].getAll(list);
                }
            }
            DebuggerContainer Debug
            {
                get
                {
                    DebuggerContainer res = new DebuggerContainer();
                    BorderTree t = this;
                    List<Border> list = new List<Border>();
                    getAll(list);
                    for (int i = 0; i < list.Count; ++i)
                    {
                        res.Add(list[i], System.Drawing.Color.Red, i);
                    }
                    return res;
                }
            }
#endif
        }
        internal override void Init(Project pr)
        {
            lineDistance = 1.0;
            firstDistance = 1.0;
            holeMode = EHoleMode.circumscribe;
            spiralMode = ESpiralMode.Parallel;
            counterClock = true;
            inbound = true;
            lineWidth = pr.LineWidthList.Current;
            linePattern = pr.LinePatternList.Current;
            colorDef = pr.ColorList.Current;
        }
        public HatchStyleContour()
        {
            resourceIdInternal = "HatchStyleNameContour";
        }
        /// <summary>
        /// Gets or stes the line distance. This is the distance between adjacent contours.
        /// </summary>
        public double LineDistance
        {
            get
            {
                return lineDistance;
            }
            set
            {
                lineDistance = value;
            }
        }
        /// <summary>
        /// Gets or sets the distance to the out bounds of the area to be filled.
        /// </summary>
        public double FirstDistance
        {
            get
            {
                return firstDistance;
            }
            set
            {
                firstDistance = value;
            }
        }
        /// <summary>
        /// Gets or sets the mode how to proceed with holes inside the area to be filled
        /// </summary>
        public EHoleMode HoleMode
        {
            get
            {
                return holeMode;
            }
            set
            {
                holeMode = value;
            }
        }
        public ESpiralMode SpiralMode
        {
            get
            {
                return spiralMode;
            }
            set
            {
                spiralMode = value;
            }
        }
        /// <summary>
        /// Gets or sets the direction of the curves
        /// </summary>
        public bool CounterClock
        {
            get
            {
                return counterClock;
            }
            set
            {
                counterClock = value;
            }
        }
        /// <summary>
        /// Gets or sets the order of the curves: true from the outside to the inside, false from inside to outside
        /// </summary>
        public bool Inbound
        {
            get
            {
                return inbound;
            }
            set
            {
                inbound = value;
            }
        }
        /// <summary>
        /// Gets or sets the color of the lines of this hatch style
        /// </summary>
        public ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                colorDef = value;
            }
        }
        private BorderTree GenerateTree(Border bdr)
        {   // erzeugt eine baumartige Struktur, die die einzelnen Borders von außen nach innen darstellt
            // oft ist dies nur eine einfache Liste, bei Einschnürungen spaltet sich eine Border jedoch in 
            // zwei oder mehrere unabhängige Borders auf. Löcher werden hierbei ignoriert.
            BorderTree res = new BorderTree(bdr);
            BorderTree current = res;
            Border[] next = bdr.GetParallel(-lineDistance, false, 0.0, 0.0);
            for (int i = 0; i < next.Length; ++i)
            {
                res.next.Add(GenerateTree(next[i]));
            }
            return res;
        }
        private List<List<Border>> GenerateBorderLists(SimpleShape ss)
        {
            List<List<Border>> res = new List<List<Border>>();
            // 1. das Simple shape schonmal zufügen, die outline und die Löcher sind jeweils der Anfang einer Liste
            res.Add(new List<Border>());
            res[0].Add(ss.Outline);
            for (int i = 0; i < ss.NumHoles; ++i)
            {
                res.Add(new List<Border>());
                res[i + 1].Add(ss.Hole(i));
            }
            CompoundShape shrunk = ss.Shrink(lineDistance);
            if (shrunk.SimpleShapes.Length == 1 && shrunk.SimpleShapes[0].NumHoles == ss.NumHoles)
            {   // gleiche Struktur, d.h. Umrandung und Löcher passen zusammen.
                List<List<Border>> sub = GenerateBorderLists(shrunk.SimpleShapes[0]);
                for (int i = 0; i < res.Count; ++i)
                {   // in der Hoffnung, dass "Shrink" bei unveränderter Topologie (Anzahl der Löcher) die Reihenfolge der Löcher nicht verändert
                    // werden die so entstandenen Listen drangehängt
                    // 0 ist immer die outline, >0 sind die Löcher. Die Löcher werden davor eingefügt, damit am Ende das größte am Anfang ist
                    if (i == 0) res[i].AddRange(sub[i]);
                    else res[i].InsertRange(0, sub[i]);
                }
                for (int i = res.Count; i < sub.Count; ++i)
                {   // was darüberhinaus entstanden ist wird als neue Liste auf oberster Ebene eingetragen
                    res.Add(sub[i]);
                }
            }
            else if (shrunk.SimpleShapes.Length >= 1)
            {   // andere Struktur, wir fangen einfach neue Listen an
                for (int i = 0; i < shrunk.SimpleShapes.Length; ++i)
                {
                    List<List<Border>> sub = GenerateBorderLists(shrunk.SimpleShapes[i]);
                    res.AddRange(sub);
                }
            }
            return res;
        }

        List<List<Border>> TreeToLists(BorderTree borderTree)
        {
            List<List<Border>> res = new List<List<Border>>();
            List<Border> main = new List<Border>();
            res.Add(main);
            main.Add(borderTree.border);
            while (borderTree.next.Count > 0)
            {
                main.Add(borderTree.next[0].border);
                for (int i = 1; i < borderTree.next.Count; ++i)
                {
                    res.AddRange(TreeToLists(borderTree.next[i]));
                }
                borderTree = borderTree.next[0];
            }
            return res;
        }
        private Path2D MakeSpiral(List<Border> borders)
        {
            // die Ausdehnung von Löchern liefert oft Linien, die aus mehreren Teilstücken
            // zusammengesetzt sind. Diese werden zunächst vereinigt
            for (int i = 0; i < borders.Count; ++i)
            {
                borders[i].Reduce();
            }
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < borders.Count; ++i)
            {
                dc.Add(borders[i], System.Drawing.Color.Blue, i);
            }
#endif
            bool reverse = counterClock != inbound;
            if (borders.Count == 1)
            {
                return new Path2D(borders[0].Segments, true);
            }
            Border inner = borders[borders.Count - 1];
            Path2D finalpath = null;
            double minerror = double.MaxValue;
            // for (int i = 0; i < inner.Segments.Length; ++i)
            // for (int i = 0; i < borders[0].Segments.Length; ++i) // über die äußeren segmente iterieren
            for (int i = 0; i < inner.Segments.Length; ++i) // über die äußeren segmente iterieren
            {
#if DEBUG
                DebuggerContainer dcfunk = new DebuggerContainer();
#endif
                // Fange mit irgend einem inneren Segment an. (Probieren mit allen, nur bestes verwenden)
                Path2D last = inner.CloneAsPath(i, reverse); // Start mit einer ausgewählten inneren Kurve
                List<ICurve2D> curves = new List<ICurve2D>();
                bool ok = true;
                double error = 0.0;
                ICurve2D lastConnection = null;
                for (int k = borders.Count - 2; k >= 0; --k)
                {   // nach außen gehen
                    ICurve2D startcurve = last.SubCurves[0]; // das ist der Eintritt in die innere Kurve
                    GeoPoint2D m = startcurve.PointAt(0.5); // Mittelpunkt der inneren für Parallelensuche
                    Path2D next = null; // der nächst äußere mit dem richtigen Anfang
#if DEBUG
                    DebuggerContainer dc1 = new DebuggerContainer();
                    dc1.Add(curves.ToArray());
#endif
                    next = borders[k].CloneAsPath(0, reverse); // gefunden
                    curves.InsertRange(0, last.SubCurves); // innere Kurve schon mal zufügen (vor alles andere)
#if DEBUG
                    dcfunk.Add(last, System.Drawing.Color.Red, i);
#endif
                    ICurve2D connection = last.SubCurves[0].Clone();
                    GeoPoint2DWithParameter[] ips = next.Intersect(connection);
                    double minPar = double.MinValue;
                    double splitAt = 0.0;
                    for (int ii = 0; ii < ips.Length; ii++)
                    {
                        if (ips[ii].par2 < 0.0 && ips[ii].par2 > minPar && ips[ii].par1 >= 0.0 && ips[ii].par1 <= 1.0)
                        {
                            minPar = ips[ii].par2;
                            splitAt = ips[ii].par1;
                        }
                    }
                    if (minPar == double.MinValue && !(connection is Line2D))
                    {   // keinen Rückwärtigen Schnittpunkt gefunden
                        // versuche mit einer Linie
                        connection = new Line2D(last.StartPoint, last.StartPoint + last.StartDirection);
                        // und mache das Gleiche noch einmal
                        ips = next.Intersect(connection);
                        minPar = double.MinValue;
                        splitAt = 0.0;
                        for (int ii = 0; ii < ips.Length; ii++)
                        {
                            if (ips[ii].par2 < 0.0 && ips[ii].par2 > minPar && ips[ii].par1 >= 0.0 && ips[ii].par1 <= 1.0)
                            {
                                minPar = ips[ii].par2;
                                splitAt = ips[ii].par1;
                            }
                        }
                    }
                    if (minPar == double.MinValue)
                    {   // keinen Rückwärtigen Schnittpunkt gefunden
                        // senkrecht auf die nächste Außenkontur gehen
                        splitAt = next.InsidePositionOf(last.StartPoint);
                        connection = new Line2D(next.PointAt(splitAt), last.StartPoint);
                        error += 5 * lineDistance;
                    }
                    else
                    {
                        connection = connection.Trim(minPar, 0.0); // die rückwärtige Verlängerung der Kurve
                        if (connection == null)
                        {   // senkrecht auf die nächste Außenkontur gehen
                            splitAt = next.InsidePositionOf(last.StartPoint);
                            connection = new Line2D(next.PointAt(splitAt), last.StartPoint);
                            error += 5 * lineDistance;
                        }
                    }
                    // jetzt ein Test. ob die neue tangentiale Verlängerung verwendet werden kann:
                    // sie darf nicht nach innen gehen, zumindest nicht die weiter nach innen
                    // liegende border überschneiden. Bei der innersten Border, darf es nicht nach
                    // innen weitergehen, wenn es nur konkave Ecken gibt (Kleeblatt aus drei Halbkreisen)
                    // dann geht halt nix
                    int innerBoredIndex = k + 2;
                    if (innerBoredIndex == borders.Count) innerBoredIndex = k + 1;
                    double[] clipinside = borders[innerBoredIndex].Clip(connection, true);

                    if (clipinside.Length > 1)
                    {   // Die tangentiale Verlängerung geht durch die innere Kontur, das soll sie nicht
                        // denn sie könnte ja durch eine Insel gehen
                        // Lot auf die nächste Außenkontur ersetzt die connection
                        splitAt = next.InsidePositionOf(last.StartPoint);
                        connection = new Line2D(next.PointAt(splitAt), last.StartPoint);
                        error += 5 * lineDistance;// diese Hilfslösung besonders bestrafen
                    }
                    curves.Insert(0, connection);
#if DEBUG
                    dcfunk.Add(connection, System.Drawing.Color.Blue, i);
#endif
                    if (k > 0)
                    {   // die letzte Verbindung nicht zum Fehler zählen, da die letzen entfernten Segemente auch nicht
                        // vom Fehler abgezogen werden.
                        error += connection.Length; // möglichst kurze Verbindungsstücke hinzufügen
                    }
                    // das ist nicht die echte Länge, denn der parameter läuft mit unterschiedlicher Geschwindigkeit
                    if (k == 0)
                    {   // die äußerste Kontur soll praktisch geschlossen sein, d.h. das Stückchen
                        // welches hinten abgeschnitten wird soll wieder vorne drangehängt werden
                        ICurve2D[] splitted = next.Split(splitAt);
                        if (next.SubCurves[0].UserData.ContainsData("CADability.Border.Index"))
                        {
                            //lastIndex = (int)next.SubCurves[0].UserData.GetData("CADability.Border.Index");
                        }
                        if (splitted.Length == 2)
                        {
                            last = new Path2D(new ICurve2D[] { splitted[1], splitted[0] });
                            last.Flatten();
                            if (spiralMode != ESpiralMode.ContourSpiralClosed)
                            {
                                if (last.SubCurves[0].Length < 2 * lineDistance)
                                {
                                    last.RemoveFirstSegment();
                                    //error -= last.SubCurves[0].Length;
                                }
                            }
                        }
                        else
                        {   // kann nicht splitten, sollte eigentlich nicht vorkommen
                            ok = false;
                            break;
                        }
                    }
                    else
                    {
                        if (lastConnection != null)
                        {
                            double md = Math.Min(Math.Abs(last.MinDistance(connection.StartPoint)),
                                Math.Abs(lastConnection.MinDistance(connection.StartPoint)));
                            error += 5 * md;
                        }
                        // einfach an dieser Stelle aufsplitten und ggf. das Ende weglassen, wenn kurz genug
                        // genauer wäre: wenn der vorletzte Punkt in einem Kreis mit Radius lineDistance liegt
                        // noch besser: alle Segmente die in diesem Kreis liegen
                        ICurve2D[] splitted = next.Split(splitAt); // die ganze nächste Umrandung
                        Path2D splpath;
                        if (splitted.Length == 2)
                        {
                            splpath = new Path2D(new ICurve2D[] { splitted[1], splitted[0] });
                            splpath.Flatten();
                        }
                        else
                        {
                            splpath = splitted[0] as Path2D;
                        }
                        // alles was sich in einem "Langloch" un die zusätzlich eingefügte Linie
                        // am Anfang der nächsten Kurve befindet, kann entfernt werden
                        Border lh = Border.MakeLongHole(connection, lineDistance * 1.0001);
                        double[] cc = lh.Clip(splpath.SubCurves[0], false);
                        while (cc.Length == 0)
                        {
                            error -= splpath.SubCurves[0].Length; // die erparten Kurven werden vom Fehler abgezogen
#if DEBUG
                            dcfunk.Add(splpath.SubCurves[0], System.Drawing.Color.Green, i);
#endif
                            splpath.RemoveFirstSegment();
                            cc = lh.Clip(splpath.SubCurves[0], false);
                        }
                        last = splpath;
                        //double dbg3 = Math.Abs(GeoVector2D.Area(connection.StartDirection.Normalized, last.EndDirection.Normalized));
                        //error += dbg3 * lineDistance;
                    }
                    lastConnection = connection;
                }
                if (ok && error < minerror)
                {   // nur die beste verwenden (verschiedene Anfangskurven, also wo setzt die Spirale an)
                    curves.InsertRange(0, last.SubCurves);
#if DEBUG
                    dcfunk.Add(last, System.Drawing.Color.Red, i);
#endif
                    finalpath = new Path2D(curves.ToArray(), true);
                    minerror = error;
                }
#if DEBUG
                else if (ok)
                {
                    curves.InsertRange(0, last.SubCurves);
                    Path2D debugpath = new Path2D(curves.ToArray(), true);
                }
#endif
            }
            if (minerror < borders.Count * lineDistance * 100)
            {
                return finalpath;
            }
            else
            {   // das Ergebnis ist zu schlecht, zu viel ist abgeschnitten
                return null;
            }
        }

        private int FindIndexOfParallelSegment(ICurve2D outerSegment, Border innerBorder)
        {
            int ind = FindSegmentIndex(outerSegment);
            for (int i = 0; i < innerBorder.Count; ++i)
            {
                // suche das erste segment mit größerem index
                int ind1 = FindSegmentIndex(innerBorder[i]);
                if (ind1 >= ind) return i;
            }
            return 0;
        }

        private int FindSegmentIndex(ICurve2D segment)
        {
            if (segment.UserData.ContainsData("CADability.Border.Index"))
            {
                return (int)segment.UserData.GetData("CADability.Border.Index");
            }
            if (segment.UserData.ContainsData("CADability.Border.Successor"))
            {
                return FindSegmentIndex((ICurve2D)segment.UserData.GetData("CADability.Border.Successor"));
            }
            if (segment.UserData.ContainsData("CADability.Border.Predecessor"))
            {
                return FindSegmentIndex((ICurve2D)segment.UserData.GetData("CADability.Border.Predecessor"));
            }
            return -1;
        }
        private ICurve2D[] MakeSpiral(BorderTree borderTree)
        {
            // erzeugt aus dem gegebenen Baum Spiralen
            List<List<Border>> lists = TreeToLists(borderTree);
            List<ICurve2D> res = new List<ICurve2D>();
            for (int i = 0; i < lists.Count; ++i)
            {
                Path2D p2d = MakeSpiral(lists[i]);
                if (p2d != null)
                {
                    if (p2d.Length > lineDistance)
                    {
                        p2d = p2d.Trim(0.0, p2d.PositionAtLength(p2d.Length - lineDistance)) as Path2D;
                    }
                    if (!inbound) p2d.Reverse();
                    res.Add(p2d);
                }
                else
                {   // das ist die Notlösung, wenns keine Spirale gibt
                    // oft ist die innerste Spur extrem und deshalb gibts keine Spirale.
                    // Versuch: innerste Spur rausnehmen und nochmal versuchen
                    List<Border> sublist = new List<Border>(lists[i]);
                    sublist.RemoveAt(sublist.Count - 1);
                    p2d = MakeSpiral(sublist);
                    if (p2d != null)
                    {
                        if (!inbound) res.Add(lists[i][lists[i].Count - 1].CloneAsPath(0, true));
                        if (p2d.Length > lineDistance)
                        {
                            p2d = p2d.Trim(0.0, p2d.PositionAtLength(p2d.Length - lineDistance)) as Path2D;
                        }
                        if (!inbound) p2d.Reverse();
                        res.Add(p2d);
                        if (inbound) res.Add(lists[i][lists[i].Count - 1].CloneAsPath(0, false));
                    }
                    else
                    {   // geht auch nach Entfernen der innersten Kurve nicht
                        // Richtung und so noch nicht überprüft
                        if (inbound)
                        {
                            for (int j = 0; j < lists[i].Count; ++j)
                            {
                                p2d = lists[i][j].CloneAsPath(0, false);
                                res.Add(p2d);
                            }
                        }
                        else
                        {
                            for (int j = lists[i].Count - 1; j >= 0; --j)
                            {
                                p2d = lists[i][j].CloneAsPath(0, true);
                                res.Add(p2d);
                            }
                        }
                    }
                }
            }
            return res.ToArray();
        }
        /// <summary>
        /// This method is used by a <see cref="Hatch"/> object to calculate its contents. It generates a
        /// set of parallel lines according to <see cref="LineDistance"/> and <see cref="LineAngle"/> 
        /// and clips them with the given shape. The lines are projected into the 3D world
        /// by the given plane. <seealso cref="CompoundShape.Clip"/>
        /// </summary>
        /// <param name="shape">shape of the hatch object</param>
        /// <param name="plane">local plane of the hatch object</param>
        /// <returns></returns>
        public override GeoObjectList GenerateContent(CompoundShape shape, Plane plane)
        {
            GeoObjectList res = new GeoObjectList();
            BoundingRect ext = shape.GetExtent();
            if (firstDistance <= 0.0) firstDistance = lineDistance;
            CompoundShape shrunk = shape.Clone();
            shrunk.Approximate(false, Settings.GlobalSettings.GetDoubleValue("Approximate.Precision", 0.01));
            shrunk.IndexSegments();
            if (this.spiralMode == ESpiralMode.ContourSpiral || spiralMode == ESpiralMode.ContourSpiralClosed)
            {
                //bool hasHoles = false;
                //List<BorderTree> trees = new List<BorderTree>();
                //for (int i = 0; i < shrunk.SimpleShapes.Length; ++i)
                //{
                //    Border[] bdrs = shrunk.SimpleShapes[i].Outline.GetParallel(-firstDistance, false, 0.0, 0.0);
                //    if (shrunk.SimpleShapes[i].NumHoles > 0) hasHoles = true;
                //    for (int j = 0; j < bdrs.Length; ++j)
                //    {
                //        trees.Add(GenerateTree(bdrs[j]));
                //    }
                //}

                List<List<Border>> borderlists = new List<List<Border>>();
                if (holeMode != EHoleMode.skip)
                {   // Löcher umfahren
                    for (int i = 0; i < shrunk.SimpleShapes.Length; ++i)
                    {
                        CompoundShape cs = shrunk.SimpleShapes[i].Shrink(firstDistance);
                        for (int j = 0; j < cs.SimpleShapes.Length; ++j)
                        {
                            borderlists.AddRange(GenerateBorderLists(cs.SimpleShapes[j]));
                        }
                    }
                }
                else
                {   // Löcher überspringen
                    for (int i = 0; i < shrunk.SimpleShapes.Length; ++i)
                    {
                        SimpleShape ss = new SimpleShape(shrunk.SimpleShapes[i].Outline); // ohne Loch
                        CompoundShape cs = ss.Shrink(-firstDistance);
                        for (int j = 0; j < cs.SimpleShapes.Length; ++j)
                        {
                            borderlists.AddRange(GenerateBorderLists(cs.SimpleShapes[j]));
                        }
                    }
                }
                List<ICurve2D> finalCurves = new List<ICurve2D>();
                //if (hasHoles && !inbound && holeMode != EHoleMode.skip)
                //{   // von außen nach innen und Löcher umfahren
                //    CompoundShape cs = shrunk.Shrink(firstDistance);
                //    for (int i = 0; i < cs.SimpleShapes.Length; ++i)
                //    {
                //        for (int j = 0; j < cs.SimpleShapes[i].NumHoles; ++j)
                //        {
                //            Path2D p2d = cs.SimpleShapes[i].Hole(j).AsPath();
                //            if (!counterClock) p2d.Reverse();
                //            finalCurves.Add(p2d);
                //        }
                //    }
                //}
                for (int i = 0; i < borderlists.Count; ++i)
                {
                    Path2D spiral = MakeSpiral(borderlists[i]);
                    if (spiral != null)
                    {
                        if (!inbound) spiral.Reverse();
#if DEBUG
                        DebuggerContainer dc = new DebuggerContainer();
                        dc.Add(spiral);
#endif
                        if (holeMode == EHoleMode.skip)
                        {
                            CompoundShape cs = shrunk.Shrink(firstDistance - 2.0 * Precision.eps);
                            double[] clppnts = cs.Clip(spiral, true);
                            for (int k = 0; k < clppnts.Length; k += 2)
                            {
                                if (clppnts[k] < clppnts[k + 1])
                                {
                                    ICurve2D c2d = spiral.Trim(clppnts[k], clppnts[k + 1]);
                                    finalCurves.Add(c2d);
                                }
                            }
                        }
                        else
                        {
                            finalCurves.Add(spiral);
                        }
                    }
                    else
                    {   // das Bilden der Spirale hat nicht geklappt, einfach alle Kurven zufügen
                        for (int j = 0; j < borderlists[i].Count; ++j)
                        {
                            spiral = borderlists[i][j].AsPath();
                            if (holeMode == EHoleMode.skip)
                            {
                                CompoundShape cs = shrunk.Shrink(firstDistance - 2.0 * Precision.eps);
                                double[] clppnts = cs.Clip(spiral, true);
                                for (int k = 0; k < clppnts.Length; k += 2)
                                {
                                    if (clppnts[k] < clppnts[k + 1])
                                    {
                                        ICurve2D c2d = spiral.Trim(clppnts[k], clppnts[k + 1]);
                                        finalCurves.Add(c2d);
                                    }
                                }
                            }
                            else
                            {
                                finalCurves.Add(spiral);
                            }
                        }
                    }
                }
                if (inbound && holeMode == EHoleMode.both)
                {   // von außen nach innen und Löcher überspringen aund umfahren
                    // d.h. eine Parallellkontur der Löcher zusätzlich zufügen
                    CompoundShape cs = shrunk.Shrink(firstDistance);
                    for (int i = 0; i < cs.SimpleShapes.Length; ++i)
                    {
                        for (int j = 0; j < cs.SimpleShapes[i].NumHoles; ++j)
                        {
                            Path2D p2d = cs.SimpleShapes[i].Hole(j).AsPath();
                            if (!counterClock) p2d.Reverse();
                            finalCurves.Add(p2d);
                        }
                    }
                }
                for (int i = 0; i < finalCurves.Count; ++i)
                {
                    IGeoObject go = finalCurves[i].MakeGeoObject(plane);
                    if (go != null)
                    {
                        ILineWidth lw = go as ILineWidth;
                        if (lw != null) lw.LineWidth = lineWidth;
                        ILinePattern lp = go as ILinePattern;
                        if (lp != null) lp.LinePattern = linePattern;
                        IColorDef cd = go as IColorDef;
                        if (cd != null) cd.ColorDef = colorDef;
                    }
                    res.Add(go);
                }
            }
            else if (this.spiralMode == ESpiralMode.Parallel)
            {
                shrunk = shrunk.Shrink(firstDistance);
                int dbg = 0;
                while (!shrunk.Empty)
                {
                    ++dbg;
                    // wenn man hier sortieren will (Nürnberger):
                    // zunächst alle Borders sammeln, der Größe nach sortieren, vom größten beginnend
                    // alle herausnehmen, die im nächstgrößeren enthalten sind. Diesen Vorgang fortsetzen, bis die Menge leer ist
                    // SortedSet: mit Min bekommt man das erste, ExceptWith ist Subtract
                    foreach (SimpleShape ss in shrunk.SimpleShapes)
                    {
                        Path2D p2d = ss.Outline.AsPath().Clone() as Path2D;
                        if (!counterClock) p2d.Reverse();
                        IGeoObject go = p2d.MakeGeoObject(plane);
                        if (go != null)
                        {
                            ILineWidth lw = go as ILineWidth;
                            if (lw != null) lw.LineWidth = lineWidth;
                            ILinePattern lp = go as ILinePattern;
                            if (lp != null) lp.LinePattern = linePattern;
                            IColorDef cd = go as IColorDef;
                            if (cd != null) cd.ColorDef = colorDef;
                        }
                        res.Add(go);
                        foreach (Border b in ss.Holes)
                        {
                            p2d = b.AsPath().Clone() as Path2D; // was ist mit der Richtung der Löcher?
                            go = p2d.MakeGeoObject(plane);
                            if (go != null)
                            {
                                ILineWidth lw = go as ILineWidth;
                                if (lw != null) lw.LineWidth = lineWidth;
                                ILinePattern lp = go as ILinePattern;
                                if (lp != null) lp.LinePattern = linePattern;
                                IColorDef cd = go as IColorDef;
                                if (cd != null) cd.ColorDef = colorDef;
                            }
                            res.Add(go);
                        }
                    }
                    shrunk = shrunk.Shrink(lineDistance);
                    // if (dbg > 100) break;
                }
                if (!inbound) res.Reverse();
            }
            else if (this.spiralMode == ESpiralMode.RoundSpiral)
            {
                double radius = Math.Sqrt(ext.Width * ext.Width / 4 + ext.Height * ext.Height / 4);
                int n = (int)Math.Ceiling(radius / lineDistance) + 1;
                BSpline2D b2d = BSpline2D.MakeSpiral(ext.GetCenter(), lineDistance, n);
                if (counterClock == inbound)
                {
                    ModOp2D m = ModOp2D.Fit(new GeoPoint2D[] { ext.GetLowerLeft(), ext.GetLowerRight(), ext.GetCenter() }, new GeoPoint2D[] { ext.GetLowerRight(), ext.GetLowerLeft(), ext.GetCenter() }, false);
                    b2d = b2d.GetModified(m) as BSpline2D;
                }
                if (inbound) b2d.Reverse();
                CompoundShape cs = shrunk.Shrink(firstDistance);
                List<ICurve2D> finalCurves = new List<ICurve2D>();
                double[] clppnts = cs.Clip(b2d, true);
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
                dc.Add(b2d);
                for (int k = 0; k < clppnts.Length; ++k)
                {
                    Point pnt = Point.Construct();
                    pnt.Location = new GeoPoint(b2d.PointAt(clppnts[k]));
                    pnt.Symbol = PointSymbol.Cross;
                    dc.Add(pnt);
                }
#endif
                for (int k = 0; k < clppnts.Length; k += 2)
                {
                    if (clppnts[k + 1] - clppnts[k] > 1e-8)
                    {
                        ICurve2D c2d = b2d.Trim(clppnts[k], clppnts[k + 1]);
                        finalCurves.Add(c2d);
                    }
                }
                for (int i = 0; i < finalCurves.Count; ++i)
                {
                    IGeoObject go = finalCurves[i].MakeGeoObject(plane);
                    if (go != null)
                    {
                        ILineWidth lw = go as ILineWidth;
                        if (lw != null) lw.LineWidth = lineWidth;
                        ILinePattern lp = go as ILinePattern;
                        if (lp != null) lp.LinePattern = linePattern;
                        IColorDef cd = go as IColorDef;
                        if (cd != null) cd.ColorDef = colorDef;
                    }
                    res.Add(go);
                }
            }
            return res;
        }
        public override IShowProperty GetShowProperty()
        {
            return null;
        }
        public override HatchStyle Clone()
        {
            HatchStyleContour res = new HatchStyleContour();
            res.Name = base.Name;
            res.lineDistance = lineDistance;
            res.firstDistance = firstDistance;
            res.holeMode = holeMode;
            res.lineWidth = lineWidth;
            res.linePattern = linePattern;
            res.colorDef = colorDef;
            res.spiralMode = spiralMode;
            res.counterClock = counterClock;
            res.inbound = inbound;
            return res;
        }
        #region IPropertyEntry Members
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.SubEntries"/>, 
        /// returns the subentries in this property view.
        /// </summary>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subItems == null)
                {
                    subItems = new IPropertyEntry[9];

                    LengthProperty lp = new LengthProperty("HatchStyleContour.LineDistance", Frame, false);
                    lp.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnPropertyGetLineDistance);
                    lp.SetLengthEvent += new CADability.UserInterface.LengthProperty.SetLengthDelegate(OnPropertySetLineDistance);
                    lp.Refresh();
                    lp.ShowMouseButton = false;
                    subItems[0] = lp;

                    LengthProperty fd = new LengthProperty("HatchStyleContour.FirstDistance", Frame, false);
                    fd.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnPropertyGetFirstDistance);
                    fd.SetLengthEvent += new CADability.UserInterface.LengthProperty.SetLengthDelegate(OnPropertySetFirstDistance);
                    fd.Refresh();
                    fd.ShowMouseButton = false;
                    subItems[1] = fd;

                    MultipleChoiceProperty eh = new MultipleChoiceProperty("HatchStyleContour.ExcludeHoles", (int)holeMode);
                    eh.ValueChangedEvent += new ValueChangedDelegate(ExcludeHolesValueChanged);
                    subItems[2] = eh;

                    MultipleChoiceProperty sm = new MultipleChoiceProperty("HatchStyleContour.SpiralMode", (int)spiralMode);
                    sm.ValueChangedEvent += new ValueChangedDelegate(SpiralModeValueChanged);
                    subItems[3] = sm;

                    BooleanProperty cc = new BooleanProperty("HatchStyleContour.CounterClock", "HatchStyleContour.CounterClock.Values");
                    cc.GetBooleanEvent += new CADability.UserInterface.BooleanProperty.GetBooleanDelegate(OnGetCounterClock);
                    cc.SetBooleanEvent += new CADability.UserInterface.BooleanProperty.SetBooleanDelegate(OnSetCounterClock);
                    cc.Refresh();
                    subItems[4] = cc;

                    BooleanProperty ib = new BooleanProperty("HatchStyleContour.Inbound", "HatchStyleContour.Inbound.Values");
                    ib.GetBooleanEvent += new CADability.UserInterface.BooleanProperty.GetBooleanDelegate(OnGetInbound);
                    ib.SetBooleanEvent += new CADability.UserInterface.BooleanProperty.SetBooleanDelegate(OnSetInbound);
                    ib.Refresh();
                    subItems[5] = ib;

                    Project pr = Frame.Project;
                    LineWidthSelectionProperty lws = new LineWidthSelectionProperty("HatchStyleLines.LineWidth", pr.LineWidthList, this.lineWidth);
                    lws.LineWidthChangedEvent += new CADability.UserInterface.LineWidthSelectionProperty.LineWidthChangedDelegate(OnLineWidthChanged);
                    subItems[6] = lws;

                    LinePatternSelectionProperty lps = new LinePatternSelectionProperty("HatchStyleLines.LinePattern", pr.LinePatternList, this.linePattern);
                    lps.LinePatternChangedEvent += new CADability.UserInterface.LinePatternSelectionProperty.LinePatternChangedDelegate(OnLinePatternChanged);
                    subItems[7] = lps;

                    ColorSelectionProperty csp = new ColorSelectionProperty("HatchStyleLines.Color", pr.ColorList, colorDef, ColorList.StaticFlags.allowUndefined);
                    csp.ShowAllowUndefinedGray = false;
                    csp.ColorDefChangedEvent += new ColorSelectionProperty.ColorDefChangedDelegate(OnColorDefChanged);
                    subItems[8] = csp;

                }
                return subItems;
            }
        }

        void ExcludeHolesValueChanged(object sender, object NewValue)
        {
            holeMode = (EHoleMode)(sender as MultipleChoiceProperty).CurrentIndex;
        }
        void SpiralModeValueChanged(object sender, object NewValue)
        {
            spiralMode = (ESpiralMode)(sender as MultipleChoiceProperty).CurrentIndex;
        }
        public override void Removed(IPropertyPage pp)
        {
            subItems = null;
            base.Removed(pp);
        }

        #endregion
        internal override void Update(bool AddMissingToList)
        {
            if (Parent != null && Parent.Owner != null)
            {
                ColorList cl = Parent.Owner.ColorList;
                if (cl != null && colorDef != null)
                {
                    ColorDef cd = cl.Find(colorDef.Name);
                    if (cd != null)
                        colorDef = cd;
                    else if (AddMissingToList)
                        cl.Add(colorDef);
                }
                LineWidthList ll = Parent.Owner.LineWidthList;
                if (ll != null && lineWidth != null)
                {
                    LineWidth lw = ll.Find(lineWidth.Name);
                    if (lw != null)
                        lineWidth = lw;
                    else if (AddMissingToList)
                        ll.Add(lineWidth);
                }
                LinePatternList pl = Parent.Owner.LinePatternList;
                if (pl != null && linePattern != null)
                {
                    LinePattern lw = pl.Find(linePattern.Name);
                    if (lw != null)
                        linePattern = lw;
                    else if (AddMissingToList)
                        pl.Add(linePattern);
                }
            }
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected HatchStyleContour(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            lineDistance = (double)info.GetValue("LineDistance", typeof(double));
            firstDistance = (double)info.GetValue("FirstDistance", typeof(double));
            object o = info.GetValue("ExcludeHoles", typeof(object));
            if (o is bool)
            {
                if ((bool)o) holeMode = EHoleMode.circumscribe;
                else holeMode = EHoleMode.skip;
            }
            else if (o is EHoleMode) holeMode = (EHoleMode)o;
            else holeMode = EHoleMode.circumscribe;
            try
            {
                spiralMode = (ESpiralMode)info.GetValue("SpiralMode", typeof(ESpiralMode));
            }
            catch (SerializationException)
            {
                bool spiralFilling = (bool)info.GetValue("SpiralFilling", typeof(bool));
                if (spiralFilling) spiralMode = ESpiralMode.ContourSpiral;
                else spiralMode = ESpiralMode.Parallel;
            }
            try
            {
                counterClock = (bool)info.GetValue("CounterClock", typeof(bool));
                inbound = (bool)info.GetValue("Inbound", typeof(bool));
            }
            catch (SerializationException)
            {
                counterClock = true;
                inbound = true;
            }
            colorDef = ColorDef.Read("ColorDef", info, context);
            lineWidth = LineWidth.Read("LineWidth", info, context);
            linePattern = LinePattern.Read("LinePattern", info, context);
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("LineDistance", lineDistance);
            info.AddValue("FirstDistance", firstDistance);
            info.AddValue("ExcludeHoles", holeMode);
            info.AddValue("SpiralMode", spiralMode);
            info.AddValue("CounterClock", counterClock);
            info.AddValue("Inbound", inbound);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("LineWidth", lineWidth);
            info.AddValue("LinePattern", linePattern);
        }

        #endregion
        private bool OnGetCounterClock()
        {
            return counterClock;
        }
        private void OnSetCounterClock(bool val)
        {
            counterClock = val;
        }
        private bool OnGetInbound()
        {
            return inbound;
        }
        private void OnSetInbound(bool val)
        {
            inbound = val;
        }
        private void OnLineWidthChanged(LineWidth selected)
        {
            lineWidth = selected;
        }
        private void OnLinePatternChanged(LinePattern selected)
        {
            linePattern = selected;
        }
        private void OnColorDefChanged(ColorDef selected)
        {
            colorDef = selected;
        }
        private double OnPropertyGetLineDistance(LengthProperty sender)
        {
            return lineDistance;
        }
        private void OnPropertySetLineDistance(LengthProperty sender, double l)
        {
            lineDistance = l;
        }
        private double OnPropertyGetFirstDistance(LengthProperty sender)
        {
            return firstDistance;
        }
        private void OnPropertySetFirstDistance(LengthProperty sender, double l)
        {
            firstDistance = l;
        }
    }
}
