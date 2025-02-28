using CADability.Attribute;
using CADability.Curve2D;
using CADability.GeoObject;
using CADability.Shapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Wintellect.PowerCollections;

namespace CADability
{
    /// <summary>
    /// Erzeugt die Mittelline für einen Text
    /// </summary>
    public class ShapeMiddelLine
    {
        class ParallelInfo // class, damit es manipulierbar ist
        {
            public ICurve2D toCurve;
            public Polyline2D middlePolyline;
            public double startpos, endpos;
            public ParallelInfo(ICurve2D toCurve, double startpos, double endpos, Polyline2D middlePolyline)
            {
                this.startpos = startpos;
                this.endpos = endpos;
                this.toCurve = toCurve;
                this.middlePolyline = middlePolyline;
            }
            public bool contains(double pos)
            {
                return pos >= startpos && pos <= endpos;
            }
        }
        struct Position
        {
            public double pos1, pos2, dist;
            public Position(double pos1, double pos2, double dist)
            {
                this.pos1 = pos1;
                this.pos2 = pos2;
                this.dist = dist;
            }
        }

        // Konfigurationswerte:
        double kinktol; // Knicktoleranz, wenn der Winkel größer wird, gilt es als Knick
        double paralleltol; // Winkel, mit dem zwei Kurven noch als parallel angesehen werden.
        double precision; // Genauigkeit, maximaler Abstand zweier Punkte von zurückgelieferten Polygonen
        public double pointSize;

        CompoundShape shape;
        SimpleShape currentSimpleShape;
        QuadTree<ICurve2D> quadtree;
        BoundingRect extent;
        double maxWidth;
        double strokeWidth; // die typische "Strichbreite" der Zeichen, wird von außen gesetzt, kann auch 0.0, also ungesetzt sein.
        double bmin, bmax, mmin, mmax, tmin, tmax; // die verbotenen Y-Bereiche, wenn 0.0, dann nichtverwednen, Sreifen, in denen keine Polygone enden dürfen
        HatchStyleLines[] grid;
        List<Polyline2D> allPolyLines;

        public ShapeMiddelLine(CompoundShape shape)
        {
            this.shape = shape;
            extent = shape.GetExtent();
            maxWidth = Math.Max(extent.Width / 3.0, extent.Height / 3.0);

            // Konstanten für Genauigkeit und Verhalten:
            kinktol = Math.PI / 18.0; // 10°
            paralleltol = Math.PI / 4.0; // 45°
            precision = extent.Size / 100;
            pointSize = precision;
        }

        public GeoObjectList calculate(Plane plane)
        {
            // vier Schraffurarten um die Form aufzurastern
            grid = new HatchStyleLines[4];
            grid[0] = new HatchStyleLines();
            grid[0].LineDistance = extent.Height / 50.0;
            grid[0].LineAngle = Angle.A0;
            grid[1] = new HatchStyleLines();
            grid[1].LineDistance = extent.Width / 50.0;
            grid[1].LineAngle = Angle.A90;
            grid[2] = new HatchStyleLines();
            grid[2].LineDistance = maxWidth / 12;
            grid[2].LineAngle = Angle.A45;
            grid[3] = new HatchStyleLines();
            grid[3].LineDistance = maxWidth / 12;
            grid[3].LineAngle = Angle.Deg(135);

            allPolyLines = new List<Polyline2D>();
            List<Polyline2D> collectPolyLines = new List<Polyline2D>();
            List<GeoPoint2D> allPoints = new List<GeoPoint2D>();
            for (int i = 0; i < shape.SimpleShapes.Length; i++)
            {
                allPolyLines = new List<Polyline2D>(); // neue leere Liste, damit nicht Einzelteile eines Zeichens miteinander in Beziehung gesetzt werden
                bool isPoint = shape.SimpleShapes[i].NumHoles == 0;
                if (isPoint)
                {
                    BoundingRect ssExt = shape.SimpleShapes[i].GetExtent();
                    BoundingRect ssExt45 = shape.SimpleShapes[i].Outline.GetModified(ModOp2D.Rotate(SweepAngle.Deg(45))).Extent;
                    isPoint = (ssExt.Width < pointSize && ssExt.Height < pointSize) || (ssExt45.Width < pointSize && ssExt45.Height < pointSize);
                    // Problem war 45° gedrehte Quadrate, deshalb genügt eine der beiden Bedingungn
                    if (isPoint)
                    {
                        isPoint = ssExt.Width / ssExt.Height > 0.8 && ssExt.Height / ssExt.Width > 0.8 && ssExt45.Width / ssExt45.Height > 0.8 && ssExt45.Height / ssExt45.Width > 0.8;
                    }
                }
                if (isPoint)
                {
                    allPoints.Add(shape.SimpleShapes[i].GetExtent().GetCenter());
                }
                else
                {
                    calculate(shape.SimpleShapes[i]);
                }
                collectPolyLines.AddRange(allPolyLines);
            }
            allPolyLines = collectPolyLines;
            GeoObjectList res = new GeoObjectList();
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                res.Add(allPolyLines[i].MakeGeoObject(plane));
            }
            for (int i = 0; i < allPoints.Count; i++)
            {
                GeoObject.Point pnt = GeoObject.Point.Construct();
                pnt.Location = plane.ToGlobal(allPoints[i]);
                pnt.Symbol = PointSymbol.Dot;
                res.Add(pnt);
            }
            return res;
        }

        private void calculate(SimpleShape ss)
        {
            //- waagrechte, senkrechte, diagonale Schraffuren machen mit genügend kleinem Abstand.
            //- jedes Linienstück betrifft zwei Kurven (oder auch nur eine)
            //- Wenn die Öffnung der beiden Kurven in den Treffpunkten kleiner 45° ist, dann
            //- suche die kürzeste solche Linie (<45°) zwischen den beiden Kurven
            //- versuche durch Drehen der Linie um ihren Mittelpunkt die gleichwinklige Situation hinzukriegen. Die beiden Tangenten sind hilfreich
            //- dieser Mittelpunkt ist Ausgangspunkt
            //- verschiebe die Linie parallel ein kleines Stück und mache das Gleiche
            //- beim Überschreiten des Endes einer Kurve um den Endpunkt drehen
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(ss.DebugList);
#endif
            currentSimpleShape = ss;
            List<Path2D> allParts = new List<Path2D>();
            quadtree = new QuadTree<ICurve2D>(extent * 1.01);
            Path2D ol = ss.Outline.AsPath();
            Path2D[] olpaths = ol.SplitAtKink(kinktol);
            if (olpaths.Length == 1 && olpaths[0].IsClosed)
            {   // ein geschlossener Path2D macht Probleme mit dem Abbruch
                ICurve2D[] parts = olpaths[0].Split(0.5);
                if (parts.Length == 2)
                {
                    olpaths = new Path2D[2];
                    olpaths[0] = parts[0] as Path2D;
                    olpaths[1] = parts[1] as Path2D;
                }
            }
            // Einzelteile müssen aufgeteilt werden, wenn  ein Strichende rund endet, damit keine Mittelkurven gefunden werden, die
            // auf die selbe Umrandungskurve zurückgehen. Bei manchen Fonts enden die Striche rund.
            olpaths = SplitAtTurnOver(olpaths); // nur die äußere Umrandung muss so behandelt werden
            for (int i = 0; i < olpaths.Length; i++)
            {
                quadtree.AddObject(olpaths[i]);
            }
            allParts.AddRange(olpaths);
            for (int i = 0; i < ss.Holes.Length; i++)
            {
                Path2D hl = ss.Holes[i].AsPath();
                hl.Reverse(); // Orientierung wg. innen/außen
                Path2D[] paths = hl.SplitAtKink(kinktol);
                if (paths.Length == 1 && paths[0].IsClosed)
                {   // ein geschlossener Path2D macht Probleme mit dem Abbruch
                    ICurve2D[] parts = paths[0].Split(0.5);
                    if (parts.Length == 2)
                    {
                        paths = new Path2D[2];
                        paths[0] = parts[0] as Path2D;
                        paths[1] = parts[1] as Path2D;
                    }
                }
                for (int j = 0; j < paths.Length; j++)
                {
                    quadtree.AddObject(paths[j]);
                }
                allParts.AddRange(paths);
            }
            // Alle Konturstücke sind an den Knicken aufgeteilt und befinden sich im Quadtree
            // es wird versucht Linienenden zu beseitigen
            // der Algorithmus ist zu einfach. Erstmal dahingehend erweitert, dass nur die kleineren weggemacht werden
            for (int i = allParts.Count - 1; i >= 0; --i)
            {
                double mw = maxWidth;
                if (strokeWidth > 0.0) mw = 2 * strokeWidth;
                if (allParts[i].SubCurvesCount == 1 && allParts[i].SubCurves[0] is Line2D && allParts[i].Length < mw)
                {
                    // eine kurze Linie
                    double length = allParts[i].Length;
                    bool startCaps = false, endCaps = false;
                    ICurve2D[] close = quadtree.GetObjectsCloseTo(allParts[i]);
                    for (int j = 0; j < close.Length; j++)
                    {
                        if (close[j].Length >= length)
                        {
                            if (Precision.IsEqual(close[j].EndPoint, allParts[i].StartPoint))
                            {
                                SweepAngle sw = new SweepAngle(close[j].EndDirection, allParts[i].StartDirection);
                                if (sw > Math.PI / 4.0 && sw < 3 * Math.PI / 4.0) startCaps = true;
                            }
                            if (Precision.IsEqual(close[j].StartPoint, allParts[i].EndPoint))
                            {
                                SweepAngle sw = new SweepAngle(allParts[i].EndDirection, close[j].StartDirection);
                                if (sw > Math.PI / 4.0 && sw < 3 * Math.PI / 4.0) endCaps = true;
                            }
                        }
                    }
                    if (startCaps && endCaps)
                    {
                        quadtree.RemoveObject(allParts[i]);
                        allParts.RemoveAt(i); // es handelt sich hier um einen Abschluss
                    }
                }
            }

            // allParts enthält jetzt alle Pfade
#if DEBUG
            DebuggerContainer dcallparts = new DebuggerContainer();
#endif
            for (int i = 0; i < allParts.Count; i++)
            {
                allParts[i].UserData.Add("Index", i); // damit man rückwärts den Index wieder findet
#if DEBUG
                dcallparts.Add(allParts[i], Color.Red, i);
#endif
            }
            GeoObjectList allHatchLines = new GeoObjectList();
            for (int i = 0; i < 4; i++)
            {
                GeoObjectList hatchLines = grid[i].GenerateContent(new CompoundShape(ss), Plane.XYPlane);
                allHatchLines.AddRange(hatchLines);
            }
            // alle Schraffurlinien wurden erzeugt, jetzt mit Anfangs/Endpunkt checken, welche Kurvenpaare zusammen gehören
            double eps = extent.Size / 10000;
            Dictionary<Pair<int, int>, List<Position>> connections = new Dictionary<Pair<int, int>, List<Position>>();
            for (int i = 0; i < allHatchLines.Count; i++)
            {
                GeoPoint2D sp = (allHatchLines[i] as Line).StartPoint.To2D();
                GeoPoint2D ep = (allHatchLines[i] as Line).EndPoint.To2D();
                int ind1 = -1, ind2 = -1;
                double pos1 = -1.0, pos2 = -1.0;
                ICurve2D[] cvs = quadtree.GetObjectsFromRect(new BoundingRect(sp, eps, eps));
                double mindist = double.MaxValue;
                for (int j = 0; j < cvs.Length; j++)
                {
                    double par = cvs[j].PositionOf(sp);
                    // if (cvs[j].IsParameterOnCurve(par))
                    if (par >= 0.0 && par <= 1.0)
                    {
                        double dist = cvs[j].PointAt(par) | sp;
                        if (dist < mindist)
                        {
                            mindist = dist;
                            ind1 = (int)cvs[j].UserData.GetData("Index");
                            pos1 = par;
                        }
                    }
                }
                cvs = quadtree.GetObjectsFromRect(new BoundingRect(ep, eps, eps));
                mindist = double.MaxValue;
                for (int j = 0; j < cvs.Length; j++)
                {
                    double par = cvs[j].PositionOf(ep);
                    // if (cvs[j].IsParameterOnCurve(par)) wir können später keine Punkte brauchen, die ganz knapp außerhalb liegen, und es sind ja genug da
                    if (par >= 0.0 && par <= 1.0)
                    {
                        double dist = cvs[j].PointAt(par) | ep;
                        if (dist < mindist)
                        {
                            mindist = dist;
                            ind2 = (int)cvs[j].UserData.GetData("Index");
                            pos2 = par;
                        }
                    }
                }
                if (ind1 >= 0 && ind2 >= 0)
                {
                    if (ind1 > ind2)
                    {   // immer kleinerer Index zuerst
                        int ind = ind1;
                        ind1 = ind2;
                        ind2 = ind;
                        double pos = pos1;
                        pos1 = pos2;
                        pos2 = pos;
                    }
                    GeoVector2D dir1 = allParts[ind1].DirectionAt(pos1);
                    GeoVector2D dir2 = allParts[ind2].DirectionAt(pos2);
                    double a = Math.Abs(new SweepAngle(dir1, -dir2)); // Zwischenwinkel an diesen Positionen
                    if (a > Math.PI) a = 2 * Math.PI - a;
                    if (a < Math.PI / 4)
                    {
                        double dist = allParts[ind1].PointAt(pos1) | allParts[ind2].PointAt(pos2); // nicht sp | ep, bei vor oder nach der Kurve gibt es manchmal 0 für PositionOf
                        if (dist < 3 * maxWidth) // von 2 auf 3 erhöht
                        {
                            List<Position> positionlist;
                            if (!connections.TryGetValue(new Pair<int, int>(ind1, ind2), out positionlist))
                            {
                                positionlist = new List<Position>();
                                connections[new Pair<int, int>(ind1, ind2)] = positionlist;
                            }
                            positionlist.Add(new Position(pos1, pos2, dist));
                        }
                    }
                }
            }
            foreach (KeyValuePair<Pair<int, int>, List<Position>> kv in connections)
            {
                kv.Value.Sort(delegate (Position b1, Position b2)
                {
                    double d1 = b1.dist;
                    double d2 = b2.dist;
                    if (d1 < d2) return -1;
                    if (d2 < d1) return 1;
                    return 0;
                }
                );
#if DEBUG
                dc.Add(new Line2D(allParts[kv.Key.First].PointAt(kv.Value[0].pos1), allParts[kv.Key.Second].PointAt(kv.Value[0].pos2)));
#endif
                ICurve2D curve1 = allParts[kv.Key.First];
                ICurve2D curve2 = allParts[kv.Key.Second];
                List<ParallelInfo> pi1 = curve1.UserData.GetData("Parallel") as List<ParallelInfo>;
                if (pi1 == null)
                {
                    pi1 = new List<ParallelInfo>();
                    curve1.UserData.Add("Parallel", pi1);
                }
                List<ParallelInfo> pi2 = curve2.UserData.GetData("Parallel") as List<ParallelInfo>;
                if (pi2 == null)
                {
                    pi2 = new List<ParallelInfo>();
                    curve2.UserData.Add("Parallel", pi2);
                }
                for (int i = 0; i < kv.Value.Count; i++)
                {   // alle Verbindungen zwischen den beiden Kurven abchecken
                    double pos1 = kv.Value[i].pos1, pos2 = kv.Value[i].pos2;
                    // wenn bei einer der Kurven das Intervall bereits belegt ist, dann mit einem anderen Anfangswert weitermachen
                    if (isInInterval(pi1, pos1) || isInInterval(pi2, pos2)) continue;

                    GeoPoint2D p1 = curve1.PointAt(pos1);
                    GeoPoint2D p2 = curve2.PointAt(pos2);
                    if (strokeWidth > 0.0 && (p1 | p2) > 4 * strokeWidth) continue; // eingeführt wg. Z in Calibri
                    double minpc1, maxpc1, minpc2, maxpc2;
                    Polyline2D pl2d = GetMiddlePath(curve1, curve2, p1, p2, out minpc1, out maxpc1, out minpc2, out maxpc2);
                    if (pl2d != null && isNoArtefact(pl2d))
                    {

                        pi1.Add(new ParallelInfo(curve2, minpc1, maxpc1, pl2d));
                        if (pi1 != pi2) pi2.Add(new ParallelInfo(curve1, minpc2, maxpc2, pl2d)); // bei der selben Kurve nur einmal zufügen
#if DEBUG
                        dc.Add(pl2d, Color.Red, 0);
#endif
                        allPolyLines.Add(pl2d);
                    }
                }
            }

#if DEBUG
            DebuggerContainer dc0 = new DebuggerContainer();
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                dc0.Add(allPolyLines[i], Color.Red, i);
            }
#endif
            // jetzt haben wir eine Sammlung von Polyline2D. Überlappende werden zusammengefasst
            // Verbindungen, die innerhalb liegen, werden erzeugt. Schnittpunkte in Verlängerungen werden zugefügt

            for (int i = 0; i < allParts.Count; i++)
            {   // alle Segmente der Listen nach startpos sortieren, dann werden die Abfragen einfacher
                List<ParallelInfo> pis = allParts[i].UserData.GetData("Parallel") as List<ParallelInfo>;
                if (pis != null)
                {
                    pis.Sort(delegate (ParallelInfo b1, ParallelInfo b2)
                    {
                        double d1 = b1.startpos;
                        double d2 = b2.startpos;
                        if (d1 < d2) return -1;
                        if (d2 < d1) return 1;
                        return 0;
                    }
                );
                }
            }

#if DEBUG
            DebuggerContainer dcparts = new DebuggerContainer();
            for (int i = 0; i < allParts.Count; i++)
            {
                List<ParallelInfo> pis = allParts[i].UserData.GetData("Parallel") as List<ParallelInfo>;
                if (pis != null) for (int j = 0; j < pis.Count; j++)
                        dcparts.Add(pis[j].middlePolyline, Color.Green, i);
            }
#endif

            for (int i = 0; i < allParts.Count; i++)
            {
                List<ParallelInfo> pis = allParts[i].UserData.GetData("Parallel") as List<ParallelInfo>;
                if (pis != null)
                {
                    CombineParts(allParts[i], pis);
                }
            }
#if DEBUG
            DebuggerContainer dcdc = new DebuggerContainer();
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                dcdc.Add(allPolyLines[i], Color.Red, i);
            }
#endif
            BridgeSmallGaps();
#if DEBUG
            DebuggerContainer dc3 = new DebuggerContainer();
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                dc3.Add(allPolyLines[i], Color.Red, i);
            }
#endif
            ExtendPolylines();
#if DEBUG
            DebuggerContainer dc4 = new DebuggerContainer();
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                dc4.Add(allPolyLines[i], Color.Red, i);
            }
#endif
            ClipForbiddenArea(); // speziell für Zeichen, damit sie auf der gleichen Grundlinie stehen: es gibt Rechtecke, in denen darf nichts enden.

#if DEBUG
            DebuggerContainer dc5 = new DebuggerContainer();
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                dc5.Add(allPolyLines[i], Color.Red, i);
            }
            System.Diagnostics.Trace.WriteLine("allPolylines: " + allPolyLines[0].StartPoint.ToString() + ", " + allPolyLines[0].EndPoint.ToString());
#endif
            //ClipOpenEnds();
            //RemoveEcentricVertices();
            // nutzt nichts:
            Reduce2D r2d = new Reduce2D();
            r2d.Add(allPolyLines.ToArray());
            r2d.Precision = precision * 0.1;
            r2d.FlattenPolylines = precision * 0.01; // vor allem Linien erzeugen
            r2d.OutputMode = Reduce2D.Mode.Polylines;
            r2d.BreakPolylines = true;
            allPolyLines.Clear();
            ICurve2D[] red = r2d.Reduced;
            // hier wird jetzt eine Orientierung aufgeprägt: Dan Swope hat das Problem, dass die Orientierung mal so und mal anders kommt
            // bei dem selben Zeichen. Deshalb wir die erste Polyline von links nach rechts orientiert, alle folgenden so, dass die Abstände klein werden
            for (int i = 0; i < red.Length; i++)
            {
                if (red[i] is Polyline2D)
                {
                    if (allPolyLines.Count == 0)
                    {
                        bool reverse = false;

                        if (red[i].StartPoint == red[i].EndPoint)
                        {
                            GeoVector2D dir = red[i].StartDirection;
                            reverse = dir.y < 0; // egal, nur reproduzierbar
                        }
                        else if (red[i].StartPoint.x == red[i].EndPoint.x) reverse = red[i].StartPoint.y > red[i].EndPoint.y;
                        else reverse = red[i].StartPoint.x > red[i].EndPoint.x;
                        if (reverse) red[i].Reverse();
                    }
                    else if (allPolyLines.Count > 1)
                    {
                        double d3 = allPolyLines[allPolyLines.Count - 1].EndPoint | red[i].StartPoint;
                        double d4 = allPolyLines[allPolyLines.Count - 1].EndPoint | red[i].EndPoint;
                        if (d3 > d4)
                        {
                            red[i].Reverse();
                        }
                    }
                    allPolyLines.Add(red[i] as Polyline2D);
                }
            }
#if DEBUG
            System.Diagnostics.Trace.WriteLine("reduced: " + allPolyLines[0].StartPoint.ToString() + ", " + allPolyLines[0].EndPoint.ToString());
            DebuggerContainer dc6 = new DebuggerContainer();
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                dc6.Add(allPolyLines[i], Color.Red, i);
            }
#endif

        }

        private bool isNoArtefact(Polyline2D pl2d)
        {
            if (pl2d.Length < 2 * strokeWidth) // vielleicht zu streng, oder
            {
                if (pl2d.UserData.ContainsData("Mindist"))
                {
                    double mindist = (double)pl2d.UserData.GetData("Mindist");
                    double maxdist = (double)pl2d.UserData.GetData("Maxdist");
                    if (mindist > 1.75 * strokeWidth) // Problem bei Courier New und "s"
                    {
                        return false;
                    }
                }
            }
            bool inside = currentSimpleShape.Contains(pl2d.StartPoint, true) && currentSimpleShape.Contains(pl2d.EndPoint, true);
            return inside;
        }

        private void BridgeSmallGaps()
        {
            List<Polyline2D> toAdd = new List<Polyline2D>();
            double gap = maxWidth / 10.0;
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                for (int j = 0; j < allPolyLines.Count; j++)
                {
                    if (i != j)
                    {
                        Polyline2D p2d = null;
                        double minlength = double.MaxValue;
                        double dist = allPolyLines[i].StartPoint | allPolyLines[j].StartPoint;
                        if (dist < gap && dist > Precision.eps && dist < minlength)
                        {
                            p2d = new Polyline2D(new GeoPoint2D[] { allPolyLines[i].StartPoint, allPolyLines[j].StartPoint });
                            minlength = dist;
                        }
                        dist = allPolyLines[i].StartPoint | allPolyLines[j].EndPoint;
                        if (dist < gap && dist > Precision.eps && dist < minlength)
                        {
                            p2d = new Polyline2D(new GeoPoint2D[] { allPolyLines[i].StartPoint, allPolyLines[j].EndPoint });
                            minlength = dist;
                        }
                        dist = allPolyLines[i].EndPoint | allPolyLines[j].StartPoint;
                        if (dist < gap && dist > Precision.eps && dist < minlength)
                        {
                            p2d = new Polyline2D(new GeoPoint2D[] { allPolyLines[i].EndPoint, allPolyLines[j].StartPoint });
                            minlength = dist;
                        }
                        dist = allPolyLines[i].EndPoint | allPolyLines[j].EndPoint;
                        if (dist < gap && dist > Precision.eps && dist < minlength)
                        {
                            p2d = new Polyline2D(new GeoPoint2D[] { allPolyLines[i].EndPoint, allPolyLines[j].EndPoint });
                            minlength = dist;
                        }
                        if (p2d != null && minlength > 0.0)
                        {
                            double[] parts = this.shape.Clip(p2d, true);
                            if (parts.Length == 2 && parts[0] == 0.0 && parts[1] == 1.0)
                            {
                                toAdd.Add(p2d);
                            }
                        }
                    }
                }
            }
            allPolyLines.AddRange(toAdd);
            // neu: jetzt wird kombiniert
            Reduce2D r2d = new Reduce2D();
            r2d.Add(allPolyLines.ToArray());
            r2d.Precision = gap;
            r2d.OutputMode = Reduce2D.Mode.Polylines;
            allPolyLines.Clear();
            ICurve2D[] red = r2d.Reduced;
            for (int i = 0; i < red.Length; i++)
            {
                if (red[i] is Polyline2D) allPolyLines.Add(red[i] as Polyline2D);
            }
        }

        private Path2D[] SplitAtTurnOver(Path2D[] olpaths)
        {
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < olpaths.Length; i++)
            {
                dc.Add(olpaths[i], Color.Red, i);
            }
#endif
            List<Path2D> res = new List<Path2D>();
            for (int i = 0; i < olpaths.Length; i++)
            {
                res.AddRange(SplitAtTurnOver(olpaths[i]));
            }
            return res.ToArray();
        }

        private List<Path2D> SplitAtTurnOver(Path2D toSplit)
        {
            List<Path2D> res = new List<Path2D>();
            ICurve2D[] subcurves = toSplit.SubCurves; // wird immer ein clone gemacht
            for (int i = 1; i < subcurves.Length; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    SweepAngle sw = new SweepAngle(subcurves[j].EndDirection, subcurves[i].StartDirection);
                    if (Math.Abs(sw.Degree) > 170)
                    {
                        double d1 = Geometry.DistPL(subcurves[j].EndPoint, subcurves[i].StartPoint, subcurves[i].StartDirection);
                        double d2 = Geometry.DistPL(subcurves[i].StartPoint, subcurves[j].EndPoint, subcurves[j].EndDirection);
                        // die Werte sind Vorzeichenbehaftet, die Punkte müssen jeweils auf der linken Seite liegen
                        if (d1 > 0 && d1 < maxWidth && d2 > 0 && d2 < maxWidth)
                        {
                            ICurve2D[] splitted = toSplit.Split((i + j) / 2.0 / toSplit.SubCurvesCount);
                            if (splitted.Length == 2 && splitted[0] is Path2D && splitted[1] is Path2D)
                            {
                                res.AddRange(SplitAtTurnOver(splitted[0] as Path2D));
                                res.AddRange(SplitAtTurnOver(splitted[1] as Path2D));
                                return res;
                            }
                        }
                    }
                }
            }
            res.Add(toSplit);
            return res;
        }

        private void RemoveEcentricVertices()
        {
            // wie sind die Abstände nach außen verteilt?
            SortedDictionary<int, int> distanceDistribution = new SortedDictionary<int, int>();
            double ddist = maxWidth / 100;
            int totNumVert = 0;
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                double[] distances = new double[allPolyLines[i].VertexCount];
                for (int j = 0; j < distances.Length; j++)
                {
                    distances[j] = currentSimpleShape.Distance(allPolyLines[i].Vertex[j]);
                    int dind = (int)(distances[j] / ddist);
                    if (!distanceDistribution.ContainsKey(dind)) distanceDistribution[dind] = 0;
                    distanceDistribution[dind] = distanceDistribution[dind] + 1;
                    ++totNumVert;
                }
                allPolyLines[i].UserData.Add("Distances", distances);
            }

            int maxind1, maxind2;
            maxind1 = maxind2 = 0;
            int maxval = 0;
            foreach (KeyValuePair<int, int> item in distanceDistribution)
            {
                if (item.Value > maxval)
                {
                    maxval = item.Value;
                    maxind1 = maxind2;
                    maxind2 = item.Key;
                }
            }
            if (maxind1 > maxind2)
            {
                int tmp = maxind2;
                maxind2 = maxind1;
                maxind1 = tmp;
            }
            int covered = 0;
            foreach (KeyValuePair<int, int> item in distanceDistribution)
            {
                if (item.Key >= maxind1 && item.Key <= maxind2)
                {
                    covered += item.Value;
                }
            }
            while (covered < totNumVert * 0.9)
            {
                int mi1 = maxind1 - 1;
                while (mi1 >= 0)
                {
                    if (distanceDistribution.ContainsKey(mi1)) break;
                    --mi1;
                }
                int mi2 = maxind2 + 1;
                while (mi2 <= distanceDistribution.Last().Key)
                {
                    if (distanceDistribution.ContainsKey(mi2)) break;
                    ++mi2;
                }
                if (distanceDistribution[mi1] > distanceDistribution[mi2])
                {
                    maxind1 = mi1;
                    covered += distanceDistribution[mi1];
                }
                else
                {
                    maxind2 = mi2;
                    covered += distanceDistribution[mi2];
                }
            }
            double mindist = ddist * maxind1;
            double maxdist = ddist * maxind2;
            for (int i = allPolyLines.Count - 1; i >= 0; --i)
            {   // Polylines evtl aufbrechen
                List<List<GeoPoint2D>> sublists = new List<List<GeoPoint2D>>();
                List<GeoPoint2D> actlist = null;
                double[] distances = allPolyLines[i].UserData.GetData("Distances") as double[]; // muss existieren
                for (int j = 0; j < allPolyLines[i].VertexCount; j++)
                {
                    if (distances[j] >= mindist && distances[j] <= maxdist)
                    {
                        if (actlist == null) actlist = new List<GeoPoint2D>();
                        actlist.Add(allPolyLines[i].Vertex[j]);
                    }
                    else
                    {
                        if (actlist != null) sublists.Add(actlist);
                        actlist = null;
                    }
                }
                if (actlist != null) sublists.Add(actlist);
                if (sublists.Count != 1 || sublists[0].Count != allPolyLines[i].VertexCount)
                {
                    // alte Polylinie raus, neue rein
                    allPolyLines.RemoveAt(i);
                    for (int j = 0; j < sublists.Count; j++)
                    {
                        if (sublists[j].Count >= 2)
                        {
                            Polyline2D pl2d = new Polyline2D(sublists[j].ToArray());
                            allPolyLines.Add(pl2d);
                        }
                    }
                }
            }
        }

        private void ClipForbiddenArea()
        {
            if (bmin != 0.0 || bmax != 0.0)
            {
                BoundingRect forbidden = new BoundingRect(0.0, bmin, 1.0, bmax);
                SimpleShape forbiddenShape = new SimpleShape(forbidden);
                for (int i = 0; i < allPolyLines.Count; i++)
                {
                    if (!allPolyLines[i].IsClosed)
                    {
                        if (forbidden.Contains(allPolyLines[i].StartPoint) && allPolyLines[i].StartDirection.y > 0.0 &&
                            Math.Abs(allPolyLines[i].StartDirection.x) < allPolyLines[i].StartDirection.y && !forbidden.Contains(allPolyLines[i].EndPoint))
                        {   // geht nach oben aus dem Rechteck hinaus
                            double[] parts = forbiddenShape.Clip(allPolyLines[i], false);
                            if (parts.Length > 0 && parts[0] > 0.0)
                            {
                                allPolyLines[i] = allPolyLines[i].Trim(parts[0], 1.0) as Polyline2D;
                            }
                        }
                        if (forbidden.Contains(allPolyLines[i].EndPoint) && allPolyLines[i].EndDirection.y < 0.0 &&
                            Math.Abs(allPolyLines[i].EndDirection.x) < -allPolyLines[i].EndDirection.y && !forbidden.Contains(allPolyLines[i].StartPoint))
                        {   // geht von oben in das Rechteck hinein
                            double[] parts = forbiddenShape.Clip(allPolyLines[i], false);
                            if (parts.Length > 0 && parts[parts.Length - 1] < 1.0)
                            {
                                allPolyLines[i] = allPolyLines[i].Trim(0.0, parts[parts.Length - 1]) as Polyline2D;
                            }
                        }
                    }
                }
            }
        }

        private void ClipOpenEnds()
        {   // schneide alle freien Enden weg, wenn dort ein großer Öffnungswinkel ist
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < allPolyLines.Count; i++)
                dc.Add(allPolyLines[i], Color.Green, i);
#endif
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                if (allPolyLines[i].UserData.GetData("Startopening") != null)
                {
                    double so = (double)allPolyLines[i].UserData.GetData("Startopening");
                    double eo = (double)allPolyLines[i].UserData.GetData("Endopening");
                    GeoPoint2DWithParameter[] allips = GetAllIntersectionPoints(allPolyLines[i], i);
                    if (allips.Length == 0) continue;
                    Array.Sort(allips, delegate (GeoPoint2DWithParameter b1, GeoPoint2DWithParameter b2)
                    {
                        return b1.par1.CompareTo(b2.par1);
                    }
                    );
                    bool trimmStart = so > Math.PI / 20 && Math.Abs(allips[0].par1) > 1e-4;
                    bool trimmEnd = eo > Math.PI / 20 && Math.Abs(allips[allips.Length - 1].par1 - 1.0) > 1e-4;
                    if (trimmStart && trimmEnd && allips.Length >= 2)
                    {
                        ICurve2D trimmed = allPolyLines[i].Trim(allips[0].par1, allips[allips.Length - 1].par1);
                        if (trimmed is Polyline2D)
                        {
                            allPolyLines[i].SetVertices((trimmed as Polyline2D).Vertex); // damit stimmt ggf der quadtree nicht mehr!
                        }
                    }
                    else if (trimmStart) // es gibt mindestens einen Punkt
                    {
                        ICurve2D trimmed = allPolyLines[i].Trim(allips[0].par1, 1.0);
                        if (trimmed is Polyline2D)
                        {
                            allPolyLines[i].SetVertices((trimmed as Polyline2D).Vertex); // damit stimmt ggf der quadtree nicht mehr!
                        }
                    }
                    else if (trimmEnd) // es gibt mindestens einen Punkt
                    {
                        ICurve2D trimmed = allPolyLines[i].Trim(0.0, allips[allips.Length - 1].par1);
                        if (trimmed is Polyline2D)
                        {
                            allPolyLines[i].SetVertices((trimmed as Polyline2D).Vertex); // damit stimmt ggf der quadtree nicht mehr!
                        }
                    }
                }
            }
        }

        private GeoPoint2DWithParameter[] GetAllIntersectionPoints(Polyline2D polyline2D, int excludeIndex)
        {
            List<GeoPoint2DWithParameter> res = new List<GeoPoint2DWithParameter>();
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                if (i != excludeIndex)
                {
                    GeoPoint2DWithParameter[] ips = polyline2D.Intersect(allPolyLines[i]);
                    for (int j = 0; j < ips.Length; j++)
                    {
                        if (polyline2D.IsParameterOnCurve(ips[j].par1) && allPolyLines[i].IsParameterOnCurve(ips[j].par2))
                            res.Add(ips[j]); // nur echte Schnittpunkte
                    }
                }
            }
            return res.ToArray();
        }

        private bool IsPointOnPolyline(GeoPoint2D p, int excludeIndex)
        {
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                if (i != excludeIndex)
                {
                    if (allPolyLines[i].Distance(p) < precision / 10) return true;
                }
            }
            return false;
        }

        private void CombineParts(ICurve2D c2d, List<ParallelInfo> pis)
        {
            // sich überlappende Teilstücke zusammenfassen.
            // Manche Teilstücke werden mehrfach gefunden und hiermit zu einem vereinigt
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(c2d, Color.Red, -1);
            for (int i = 0; i < pis.Count; i++)
                dc.Add(pis[i].middlePolyline, Color.Green, i);
#endif
            for (int i = 0; i < pis.Count - 1; i++)
            {
                for (int j = pis.Count - 1; j > i; --j) // von hinten wg. Löschen
                {
                    if (pis[i].endpos >= pis[j].startpos)
                    {   // dei beiden überlappen sich, i kommt ja vor j
                        if (pis[i].toCurve == pis[j].toCurve)
                        {   // die andere Kurve ist die selbe
                            Polyline2D pl1 = pis[i].middlePolyline; // wird erweitert und beibehalten
                            Polyline2D pl2 = pis[j].middlePolyline; // wird entfernt
                            List<ParallelInfo> pi2 = pis[i].toCurve.UserData.GetData("Parallel") as List<ParallelInfo>;
                            // die entsprechende Liste auf der anderen Seite
                            int k1 = -1, k2 = -1;
                            for (int k = 0; k < pi2.Count; k++)
                            {
                                if (pi2[k].middlePolyline == pl2)
                                {
                                    k2 = k;
                                }
                                if (pi2[k].middlePolyline == pl1)
                                {
                                    k1 = k;
                                }
                            }
                            bool reverse1 = false, reverse2 = false;
                            if (k1 >= 0 && k2 >= 0) // das muss der Fall sein
                            {
                                if (pi2[k1].startpos > pi2[k1].endpos) reverse1 = true;
                                if (pi2[k2].startpos > pi2[k2].endpos) reverse2 = true;
                                pi2[k1].startpos = Math.Min(pi2[k1].startpos, pi2[k2].startpos);
                                pi2[k1].endpos = Math.Max(pi2[k1].endpos, pi2[k2].endpos);
                                pi2.RemoveAt(k2); // dieses Polygon wird entfernt
                            }
                            if (reverse1) pl1.Reverse();
                            if (reverse2) pl2.Reverse();
                            CombinePolylines(pl1, pl2);
                            if (pis != pi2) pis.RemoveAt(j); // sonst wurde es gerade schon entfernt, wenn die beiden Kurven die selben sind
                            allPolyLines.Remove(pl2);
                        }
                    }
                }
            }
            // Lücken im Parameterbereich verbinden, wenn die Verbindung ganz innerhalb liegt
            // Szenario: auf der anderen Seite zu c2d ist ein Knick oder eine Lücke, dann hier die parallele Kurve einfach fortsetzen und die Lücke schließen
            for (int i = 0; i < pis.Count - 1; i++)
            {
                if (pis[i].endpos < pis[i + 1].startpos)
                {
                    //SweepAngle sw1 = new SweepAngle(c2d.DirectionAt(pis[i].endpos), pis[i].middlePolyline.StartDirection);
                    //SweepAngle sw2 = new SweepAngle(c2d.DirectionAt(pis[i].endpos), -pis[i].middlePolyline.EndDirection);
                    //bool isForward1 = Math.Abs(sw1) < Math.Abs(sw2);
                    //sw1 = new SweepAngle(c2d.DirectionAt(pis[i + 1].startpos), pis[i + 1].middlePolyline.StartDirection);
                    //sw2 = new SweepAngle(c2d.DirectionAt(pis[i + 1].startpos), -pis[i + 1].middlePolyline.EndDirection);
                    //bool isForward2 = Math.Abs(sw1) < Math.Abs(sw2);
                    // obiges ist schlecht, da manchmal statt 0 180 kommt
                    bool isForward1 = (c2d.PointAt(pis[i].endpos) | pis[i].middlePolyline.EndPoint) < (c2d.PointAt(pis[i].endpos) | pis[i].middlePolyline.StartPoint);
                    bool isForward2 = (c2d.PointAt(pis[i + 1].startpos) | pis[i + 1].middlePolyline.StartPoint) < (c2d.PointAt(pis[i + 1].startpos) | pis[i + 1].middlePolyline.EndPoint);
                    double startParDist, endParDist;
                    if (isForward1) startParDist = (double)pis[i].middlePolyline.UserData.GetData("Enddist");
                    else startParDist = (double)pis[i].middlePolyline.UserData.GetData("Startdist");
                    if (isForward2) endParDist = (double)pis[i].middlePolyline.UserData.GetData("Startdist");
                    else endParDist = (double)pis[i].middlePolyline.UserData.GetData("Enddist");
                    double startPar = -1, endPar = -1;
                    GeoPoint2D startPoint, endPoint;
                    if (isForward1) startPoint = pis[i].middlePolyline.EndPoint;
                    else startPoint = pis[i].middlePolyline.StartPoint;
                    if (isForward2) endPoint = pis[i + 1].middlePolyline.StartPoint;
                    else endPoint = pis[i + 1].middlePolyline.EndPoint;
                    GeoPoint2D[] fps = c2d.PerpendicularFoot(startPoint);
                    double maxerror = double.MaxValue;
                    for (int j = 0; j < fps.Length; j++)
                    {
                        double pos = c2d.PositionOf(fps[j]);
                        if (Math.Abs(pos - pis[i].endpos) < maxerror)
                        {
                            maxerror = Math.Abs(pos - pis[i].endpos);
                            startPar = pos;
                        }
                    }
                    fps = c2d.PerpendicularFoot(endPoint);
                    maxerror = double.MaxValue;
                    for (int j = 0; j < fps.Length; j++)
                    {
                        double pos = c2d.PositionOf(fps[j]);
                        if (Math.Abs(pos - pis[i + 1].startpos) < maxerror)
                        {
                            maxerror = Math.Abs(pos - pis[i + 1].startpos);
                            endPar = pos;
                        }
                    }
                    if (startPar < endPar)
                    {
                        Polyline2D pl2d = MakeParallel(c2d, startPar, endPar, c2d.PointAt(startPar) | startPoint, c2d.PointAt(endPar) | endPoint);
                        if (pl2d != null)
                        {
                            double[] parts = shape.Clip(pl2d, true);
#if DEBUG
                            DebuggerContainer dc1 = new DebuggerContainer();
                            dc1.Add(c2d, Color.Red, 0);
                            dc1.Add(pis[i].middlePolyline, Color.Green, 1);
                            dc1.Add(pis[i + 1].middlePolyline, Color.Green, 2);
                            dc1.Add(pl2d, Color.Blue, 3);
#endif
                            if (parts.Length == 2 && parts[0] == 0.0 && parts[1] == 1.0)
                            {   // das Verbindungsstück ist ganz drinnen
                                allPolyLines.Add(pl2d);
                            }
                        }
                    }
                }
            }
            // zusammenfassen, wenn die Verbindung genz innerhalb liegt
            for (int i = pis.Count - 2; i >= 0; --i)
            {   // die Segmente folgen ja aufeinander
                Polyline2D pl1 = pis[i].middlePolyline;
                Polyline2D pl2 = pis[i + 1].middlePolyline;
                double mindist = pl1.StartPoint | pl2.StartPoint;
                GeoPoint2D p1 = pl1.StartPoint;
                GeoPoint2D p2 = pl2.StartPoint;
                GeoVector2D dir1 = pl1.StartDirection;
                GeoVector2D dir2 = pl2.StartDirection;
                double dist = pl1.StartPoint | pl2.EndPoint;
                if (dist < mindist)
                {
                    p1 = pl1.StartPoint;
                    p2 = pl2.EndPoint;
                    dir1 = pl1.StartDirection;
                    dir2 = pl2.EndDirection;
                    mindist = dist;
                }
                dist = pl1.EndPoint | pl2.StartPoint;
                if (dist < mindist)
                {
                    p1 = pl1.EndPoint;
                    p2 = pl2.StartPoint;
                    dir1 = pl1.EndDirection;
                    dir2 = pl2.StartDirection;
                    mindist = dist;
                }
                dist = pl1.EndPoint | pl2.EndPoint;
                if (dist < mindist)
                {
                    p1 = pl1.EndPoint;
                    p2 = pl2.EndPoint;
                    dir1 = pl1.EndDirection;
                    dir2 = pl2.EndDirection;
                    mindist = dist;
                }
                SweepAngle sw1 = new SweepAngle(dir1, p2 - p1);
                SweepAngle sw2 = new SweepAngle(p2 - p1, dir2);
                if (Math.Abs(sw1) < Math.PI / 10 && Math.Abs(sw2) < Math.PI / 10) // nur wenn die Richtung einigermaßen passt!
                {
                    double[] parts = shape.Clip(new Line2D(p1, p2), true);
                    if (parts.Length == 2 && parts[0] == 0.0 && parts[1] == 1.0)
                    {
                        Polyline2D pl2d = Polyline2D.MakePolyline2D(new GeoPoint2D[] { p1, p2 });
                        if (pl2d != null)
                            allPolyLines.Add(pl2d);
                    }
                }
                else
                {
                    // folgendes zu kritisch und wird nicht benötigt:

                    // zwei Segmente folgen aufeinander, aber sie passen nicht zusammen, z.B. beim B in der Mitte, wenn 3 Linien aufeinanderkommen
                    // welce der beiden ist die "gute"?
                    // der Effekt kann sein, dass eine middlePolyline zweimal durch eine Parallele ersetzt wird.
                    //double mind1 = (double)pis[i].middlePolyline.UserData.GetData("Mindist");
                    //double maxd1 = (double)pis[i].middlePolyline.UserData.GetData("Maxdist");
                    //double mind2 = (double)pis[i + 1].middlePolyline.UserData.GetData("Mindist");
                    //double maxd2 = (double)pis[i + 1].middlePolyline.UserData.GetData("Maxdist");
                    //if (maxd1 - mind1 < maxd2 - mind2)
                    //{   // die 1. Polyline ist besser
                    //    double pardist;
                    //    if (mode == 0 || mode == 1) pardist = (double)pis[i].middlePolyline.UserData.GetData("Startdist");
                    //    else pardist = (double)pis[i].middlePolyline.UserData.GetData("Enddist");
                    //    Polyline2D p2d = MakeParallel(c2D, pis[i + 1].startpos, pis[i + 1].endpos, pardist / 2.0); // mind ist die ganze Breite
                    //    CombinePolylines(pis[i].middlePolyline, p2d);
                    //    allPolyLines.Remove(pis[i + 1].middlePolyline);
                    //    pis.RemoveAt(i + 1);
                    //}
                    //else
                    //{
                    //    double pardist;
                    //    if (mode == 0 || mode == 2) pardist = (double)pis[i + 1].middlePolyline.UserData.GetData("Startdist");
                    //    else pardist = (double)pis[i + 1].middlePolyline.UserData.GetData("Enddist");
                    //    Polyline2D p2d = MakeParallel(c2D, pis[i].startpos, pis[i].endpos, pardist / 2.0);
                    //    CombinePolylines(pis[i + 1].middlePolyline, p2d);
                    //    allPolyLines.Remove(pis[i].middlePolyline);
                    //    pis.RemoveAt(i);
                    //}
                }
            }
        }

        private void ExtendPolylines()
        {
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            for (int i = 0; i < allPolyLines.Count; i++) dc.Add(allPolyLines[i], Color.Red, i);
#endif
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                GeoPoint2D sp = allPolyLines[i].StartPoint;
                GeoPoint2D ep = allPolyLines[i].EndPoint;
                double avglen = allPolyLines[i].Length / allPolyLines[i].VertexCount;
                allPolyLines[i].Reduce(precision); // Geschnörkel am Anfang und Ende wegmachen
            }
#if DEBUG
            for (int i = 0; i < allPolyLines.Count; i++) dc.Add(allPolyLines[i], Color.Blue, i);
#endif

            // für alle Polylines überprüfen, ob sie am Anfang oder Ende verlängert werden kann
            Dictionary<int, Polyline2D> startExtension = new Dictionary<int, Polyline2D>();
            Dictionary<int, Polyline2D> endExtension = new Dictionary<int, Polyline2D>();
            for (int i = 0; i < allPolyLines.Count; i++)
            {
                double closeGap = (allPolyLines[i].StartPoint | allPolyLines[i].EndPoint);
                if (allPolyLines[i].Length < precision || closeGap < precision) continue; // zu kleine oder geschlossene Polylines nicht verlängern
                bool makeStartExtension = true;
                bool makeEndExtension = true;
                double maxGap = Math.Min(closeGap, 2 * maxWidth); // keine Linie zufügen, die länger ist als 2 * maxWidth oder als die fast geschlossene polylinie
                for (int j = 0; j < allPolyLines.Count; j++)
                {
                    if (i != j)
                    {
                        // zuerst einfache Lückenschließer überprüfen
                        if (makeEndExtension)
                        {
                            if ((allPolyLines[i].EndPoint | allPolyLines[j].StartPoint) < precision)
                            {
                                try
                                {
                                    Polyline2D pl2d = Polyline2D.MakePolyline2D(new GeoPoint2D[] { allPolyLines[i].EndPoint, allPolyLines[j].StartPoint });
                                    if (pl2d != null)
                                    {
                                        Polyline2D alreadyFound;
                                        if (!endExtension.TryGetValue(i, out alreadyFound) || alreadyFound.Length > pl2d.Length)
                                        {
                                            endExtension[i] = pl2d;
                                        }
                                    }
                                }
                                catch (Polyline2DException) { }
                            }
                            if ((allPolyLines[i].EndPoint | allPolyLines[j].EndPoint) < precision)
                            {
                                try
                                {
                                    Polyline2D pl2d = Polyline2D.MakePolyline2D(new GeoPoint2D[] { allPolyLines[i].EndPoint, allPolyLines[j].EndPoint });
                                    if (pl2d != null)
                                    {
                                        Polyline2D alreadyFound;
                                        if (!endExtension.TryGetValue(i, out alreadyFound) || alreadyFound.Length > pl2d.Length)
                                        {
                                            endExtension[i] = pl2d;
                                        }
                                    }
                                }
                                catch (Polyline2DException) { }
                            }
                        }
                        if (makeStartExtension)
                        {
                            if ((allPolyLines[i].StartPoint | allPolyLines[j].StartPoint) < precision)
                            {
                                try
                                {
                                    Polyline2D pl2d = Polyline2D.MakePolyline2D(new GeoPoint2D[] { allPolyLines[i].StartPoint, allPolyLines[j].StartPoint });
                                    if (pl2d != null)
                                    {
                                        Polyline2D alreadyFound;
                                        if (!startExtension.TryGetValue(i, out alreadyFound) || alreadyFound.Length > pl2d.Length)
                                        {
                                            startExtension[i] = pl2d;
                                        }
                                    }
                                }
                                catch (Polyline2DException) { }
                            }
                            if ((allPolyLines[i].StartPoint | allPolyLines[j].EndPoint) < precision)
                            {
                                try
                                {
                                    Polyline2D pl2d = Polyline2D.MakePolyline2D(new GeoPoint2D[] { allPolyLines[i].StartPoint, allPolyLines[j].EndPoint });
                                    if (pl2d != null)
                                    {
                                        Polyline2D alreadyFound;
                                        if (!startExtension.TryGetValue(i, out alreadyFound) || alreadyFound.Length > pl2d.Length)
                                        {
                                            startExtension[i] = pl2d;
                                        }
                                    }
                                }
                                catch (Polyline2DException) { }
                            }
                        }
                        GeoPoint2DWithParameter[] ips = allPolyLines[i].Intersect(allPolyLines[j]);
                        for (int k = 0; k < ips.Length; k++)
                        {
                            Line2D l2d;
                            if (makeStartExtension && ips[k].par1 < 0.0 && (ips[k].p | allPolyLines[i].StartPoint) > precision)
                            {   // Verlängerung nach vorne
                                l2d = new Line2D(ips[k].p, allPolyLines[i].StartPoint);
                                if (l2d.Length < maxGap && Math.Abs(allPolyLines[j].Distance(ips[k].p)) < maxGap)
                                {
#if DEBUG
                                    dc.Add(l2d, Color.Blue, i * 100 + j);
#endif
                                    double[] parts = shape.Clip(l2d, true);
                                    if (parts.Length == 2 && parts[0] == 0.0 && parts[1] == 1.0)
                                    {
                                        Polyline2D pl2d = new Polyline2D(new GeoPoint2D[] { ips[k].p, allPolyLines[i].StartPoint });
                                        Polyline2D alreadyFound;
                                        if (!startExtension.TryGetValue(i, out alreadyFound) || alreadyFound.Length > pl2d.Length)
                                        {
                                            startExtension[i] = pl2d;
                                        }
                                    }
                                    else if (parts.Length == 2 && parts[0] < 0.7 && parts[1] == 1.0)
                                    {   // geht über die Umrandung hinaus, aber nicht zu weit
                                        Polyline2D pl2d = new Polyline2D(new GeoPoint2D[] { l2d.PointAt(parts[0]), allPolyLines[i].StartPoint });
                                        Polyline2D alreadyFound;
                                        if (!startExtension.TryGetValue(i, out alreadyFound) || alreadyFound.Length > pl2d.Length)
                                        {
                                            startExtension[i] = pl2d;
                                        }

                                    }
                                }
                            }
                            else if (makeEndExtension && ips[k].par1 > 1.0 && (ips[k].p | allPolyLines[i].EndPoint) > precision)
                            {   // Verlängerung nach hinten
                                l2d = new Line2D(allPolyLines[i].EndPoint, ips[k].p);
                                if (l2d.Length < maxGap && Math.Abs(allPolyLines[j].Distance(ips[k].p)) < maxGap)
                                {
#if DEBUG
                                    dc.Add(l2d, Color.Green, i * 100 + j);
#endif
                                    double[] parts = shape.Clip(l2d, true);
                                    if (parts.Length == 2 && parts[0] == 0.0 && parts[1] == 1.0)
                                    {
#if DEBUG
                                        DebuggerContainer dc2 = new DebuggerContainer();
                                        dc2.Add(allPolyLines[i], Color.Green, 1);
                                        dc2.Add(allPolyLines[j], Color.Blue, 1);
#endif
                                        Polyline2D pl2d = new Polyline2D(new GeoPoint2D[] { ips[k].p, allPolyLines[i].EndPoint });
                                        Polyline2D alreadyFound;
                                        if (!endExtension.TryGetValue(i, out alreadyFound) || alreadyFound.Length > pl2d.Length)
                                        {
                                            endExtension[i] = pl2d;
                                        }
                                    }
                                    else if (parts.Length == 2 && parts[0] == 0.0 && parts[1] > 0.3)
                                    {   // geht über die Umrandung hinaus, aber nicht zu weit
                                        Polyline2D pl2d = new Polyline2D(new GeoPoint2D[] { l2d.PointAt(parts[1]), allPolyLines[i].EndPoint });
                                        Polyline2D alreadyFound;
                                        if (!endExtension.TryGetValue(i, out alreadyFound) || alreadyFound.Length > pl2d.Length)
                                        {
                                            endExtension[i] = pl2d;
                                        }

                                    }
                                }
                            }
                            else if (Precision.IsEqual(ips[k].p, allPolyLines[i].StartPoint) && allPolyLines[j].IsParameterOnCurve(ips[k].par2))
                            {   // die Kurve endet bereits bei einem Schnittpunkt
                                makeStartExtension = false;
                                startExtension[i] = null;
                            }
                            else if (Precision.IsEqual(ips[k].p, allPolyLines[i].EndPoint) && allPolyLines[j].IsParameterOnCurve(ips[k].par2))
                            {
                                makeEndExtension = false;
                                endExtension[i] = null;
                            }
                        }
                    }
                }
            }
            // an jedem Anfang bzw. Ende gibt es jetzt maximal eine Polyline, die der Menge zugefügt wird
            foreach (KeyValuePair<int, Polyline2D> item in startExtension)
            {
                if (item.Value != null) allPolyLines.Add(item.Value);
            }
            foreach (KeyValuePair<int, Polyline2D> item in endExtension)
            {
                if (item.Value != null) allPolyLines.Add(item.Value);
            }
        }

        private Polyline2D MakeParallel(ICurve2D c2D, double pos1, double pos2, double dist)
        {
            int num = (int)(c2D.Length / precision * (pos2 - pos1) + 1);
            double step = (pos2 - pos1) / num;
            GeoPoint2D[] vtx = new GeoPoint2D[num + 1];
            for (int i = 0; i <= num; i++)
            {
                GeoPoint2D p = c2D.PointAt(pos1 + i * step);
                GeoVector2D v = c2D.DirectionAt(pos1 + i * step).Normalized;
                vtx[i] = p + dist * v.ToLeft();
            }
            return Polyline2D.MakePolyline2D(vtx);
        }

        private Polyline2D MakeParallel(ICurve2D c2D, double pos1, double pos2, double dist1, double dist2)
        {   // quasiparallele, mit kontinuierlich sich änderndem Abstand
            int num = (int)(c2D.Length / precision * (pos2 - pos1) + 1);
            double step = (pos2 - pos1) / num;
            double ddist = (dist2 - dist1) / num;
            GeoPoint2D[] vtx = new GeoPoint2D[num + 1];
            for (int i = 0; i <= num; i++)
            {
                GeoPoint2D p = c2D.PointAt(pos1 + i * step);
                GeoVector2D v = c2D.DirectionAt(pos1 + i * step).Normalized;
                vtx[i] = p + (dist1 + i * ddist) * v.ToLeft();
            }
            Polyline2D pl2d = Polyline2D.MakePolyline2D(vtx);
            if (pl2d != null)
            {
                // Selbstüberschneidungen entfernen
                double[] sis = pl2d.GetSelfIntersections();
                bool clipped = true;
                while (sis != null && sis.Length > 0 && clipped)
                {
                    clipped = false;
                    Array.Sort(sis); // kleinste zuerst
                    GeoPoint2D p0 = pl2d.PointAt(sis[0]);
                    for (int i = 1; i < sis.Length; i++)
                    {   // finde die zu dem ersten Punkt passende Selbstüberschneidung
                        GeoPoint2D p = pl2d.PointAt(sis[i]);
                        if (Precision.IsEqual(p0, p))
                        {
                            Polyline2D start = pl2d.Trim(0, sis[0]) as Polyline2D;
                            Polyline2D end = pl2d.Trim(sis[i], 1) as Polyline2D;
                            if (start != null && end != null)
                            {
                                List<GeoPoint2D> combined = new List<GeoPoint2D>();
                                combined.AddRange(start.Vertex);
                                combined.RemoveAt(combined.Count - 1); // den letzten weg, der kmmt ja gleich nochmal
                                combined.AddRange(end.Vertex);
                                pl2d = new Polyline2D(combined.ToArray());
                                sis = pl2d.GetSelfIntersections();
                                clipped = true;
                            }
                        }
                    }
                }
            }
            return pl2d;
        }

        private void CombinePolylines(Polyline2D pl1, Polyline2D pl2)
        {
            ICurve2D fused = pl1.GetFused(pl2, precision);
            if (fused != null)
            {
                pl1.SetVertices((fused as Polyline2D).Vertex);
            }
            else
            {   // das kommt hoffentlich nicht mehr dran
                List<GeoPoint2D> vertices = new List<GeoPoint2D>(pl1.Vertex);
                for (int i = 0; i < pl2.VertexCount; i++)
                {
                    double pos = pl1.PositionOf(pl2.Vertex[i]);
                    if (pos <= 0.0) vertices.Insert(0, pl2.Vertex[i]);
                    else if (pos >= 1.0) vertices.Add(pl2.Vertex[i]);
                }
                pl1.SetVertices(vertices.ToArray());
            }
        }

        private bool isInInterval(List<ParallelInfo> lpi, double pos)
        {
            for (int i = 0; i < lpi.Count; i++)
            {
                if (lpi[i].contains(pos)) return true;
            }
            return false;
        }

        private Polyline2D GetMiddlePath(ICurve2D curve1, ICurve2D curve2, GeoPoint2D p1, GeoPoint2D p2, out double minpc1, out double maxpc1, out double minpc2, out double maxpc2)
        {
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(curve1, Color.Red, 0);
            dc.Add(curve2, Color.Green, 1);
            dc.Add(new Line2D(p1, p2), Color.Blue, 3);
#endif
            minpc1 = minpc2 = double.MaxValue;
            maxpc1 = maxpc2 = double.MinValue;
            double pos1, pos2;
            if (FindBalancedPoint(curve1, curve2, ref p1, ref p2, out pos1, out pos2))
            {
                List<GeoPoint2D> pl = new List<GeoPoint2D>(); // liste aller Mittelpunkte
                List<double> opening = new List<double>(); // zugehörige Liste der Öffnungswinkel zwischen den beiden Kurven
                List<double> distances = new List<double>(); // zugehörige Liste der Breiten zwischen den beiden Kurven
                GeoPoint2D sp1 = p1, sp2 = p2;
                pl.Add(new GeoPoint2D(p1, p2));
                opening.Add(Math.Abs(new SweepAngle(curve1.DirectionAt(pos1), -curve2.DirectionAt(pos2))));
                distances.Add(p1 | p2);
                do
                {   // geschlossene Einzelkonturen wurden vorher halbiert,deshalb läuft es nicht im Kries und kommt immer zum Ende
                    GeoVector2D offset = precision * (p2 - p1).ToLeft().Normalized;
                    GeoPoint2D pp1 = p1, pp2 = p2;
                    p1 = p1 + offset;
                    p2 = p2 + offset;
                    if (FindBalancedPoint(curve1, curve2, ref p1, ref p2, out pos1, out pos2))
                    {
                        pl.Add(new GeoPoint2D(p1, p2));
                        opening.Add(Math.Abs(new SweepAngle(curve1.DirectionAt(pos1), -curve2.DirectionAt(pos2))));
                        distances.Add(p1 | p2);
                        if (pos1 > maxpc1) maxpc1 = pos1;
                        if (pos1 < minpc1) minpc1 = pos1;
                        if (pos2 > maxpc2) maxpc2 = pos2;
                        if (pos2 < minpc2) minpc2 = pos2;
                    }
                    else
                    {
                        // 2. Versuch mit halbem Offset
                        p1 = pp1 + 0.5 * offset;
                        p2 = pp2 + 0.5 * offset;
                        if (FindBalancedPoint(curve1, curve2, ref p1, ref p2, out pos1, out pos2))
                        {
                            pl.Add(new GeoPoint2D(p1, p2));
                            opening.Add(Math.Abs(new SweepAngle(curve1.DirectionAt(pos1), -curve2.DirectionAt(pos2))));
                            distances.Add(p1 | p2);
                            if (pos1 > maxpc1) maxpc1 = pos1;
                            if (pos1 < minpc1) minpc1 = pos1;
                            if (pos2 > maxpc2) maxpc2 = pos2;
                            if (pos2 < minpc2) minpc2 = pos2;
                        }
                        else
                        {
                            break;
                        }
                    }
                } while (true);
                pl.Reverse(); // umdrehen, der Rest kommt jetzt hintendran
                opening.Reverse();
                distances.Reverse();
                p1 = sp1;
                p2 = sp2;
                do
                {
                    GeoVector2D offset = precision * (p2 - p1).ToRight().Normalized;
                    GeoPoint2D pp1 = p1, pp2 = p2;
                    p1 = p1 + offset;
                    p2 = p2 + offset;
                    if (FindBalancedPoint(curve1, curve2, ref p1, ref p2, out pos1, out pos2))
                    {
                        pl.Add(new GeoPoint2D(p1, p2));
                        opening.Add(Math.Abs(new SweepAngle(curve1.DirectionAt(pos1), -curve2.DirectionAt(pos2))));
                        distances.Add(p1 | p2);
                        if (pos1 > maxpc1) maxpc1 = pos1;
                        if (pos1 < minpc1) minpc1 = pos1;
                        if (pos2 > maxpc2) maxpc2 = pos2;
                        if (pos2 < minpc2) minpc2 = pos2;
                    }
                    else
                    {
                        p1 = pp1 + offset;
                        p2 = pp2 + offset;
                        if (FindBalancedPoint(curve1, curve2, ref p1, ref p2, out pos1, out pos2))
                        {
                            pl.Add(new GeoPoint2D(p1, p2));
                            opening.Add(Math.Abs(new SweepAngle(curve1.DirectionAt(pos1), -curve2.DirectionAt(pos2))));
                            distances.Add(p1 | p2);
                            if (pos1 > maxpc1) maxpc1 = pos1;
                            if (pos1 < minpc1) minpc1 = pos1;
                            if (pos2 > maxpc2) maxpc2 = pos2;
                            if (pos2 < minpc2) minpc2 = pos2;
                        }
                        else
                        {
                            break;
                        }
                    }
                } while (true);
                if (pl.Count > 1)
                {
                    bool ok = false;
                    double mindist = double.MaxValue;
                    double maxdist = double.MinValue;
                    for (int i = 0; i < pl.Count; i++)
                    {
                        if (opening[i] < Math.PI / 10) ok = true; // Öffnungswinkel der beiden Kurven muss an einer Stelle kleiner 18° sein (kritisch, z.B. bei ScriptC)
                        if (distances[i] < mindist) mindist = distances[i];
                        if (distances[i] > maxdist) maxdist = distances[i];
                    }
                    if (ok && mindist < maxWidth)
                    {
                        // die Polyline darf keine Knicke enthalten, die um mehr als 90° gehen
                        // das sind meist Artefakte am Anfang oder Ende
                        int splitAt = -1;
                        if (pl.Count > 2)
                        {
                            for (int i = 1; i < pl.Count - 1; ++i)
                            {
                                SweepAngle bend = new SweepAngle(pl[i] - pl[i - 1], pl[i + 1] - pl[i]);
                                if (Math.Abs(bend.Radian) > Math.PI / 2.0)
                                {
                                    splitAt = i;
                                }
                            }
                        }
                        if (splitAt > 0)
                        {
                            if (splitAt > pl.Count / 2)
                            {
                                pl.RemoveRange(splitAt + 1, pl.Count - splitAt - 1);
                                distances.RemoveRange(splitAt + 1, pl.Count - splitAt - 1);
                                opening.RemoveRange(splitAt + 1, pl.Count - splitAt - 1);
                            }
                            else
                            {
                                pl.RemoveRange(0, splitAt);
                                distances.RemoveRange(0, splitAt);
                                opening.RemoveRange(0, splitAt);
                            }
                        }
                        Polyline2D pl2d = new Polyline2D(pl.ToArray());
                        pl2d.UserData.Add("Mindist", mindist);
                        pl2d.UserData.Add("Maxdist", maxdist);
                        pl2d.UserData.Add("Startdist", distances[0]);
                        pl2d.UserData.Add("Enddist", distances[distances.Count - 1]);
                        pl2d.UserData.Add("Startopening", opening[0]);
                        pl2d.UserData.Add("Endopening", opening[opening.Count - 1]);
#if DEBUG
                        dc.Add(pl2d, Color.Black, 4);
#endif
                        return pl2d;
                    }
                }

            }
            return null;
        }

        private bool FindBalancedPoint(ICurve2D curve1, ICurve2D curve2, ref GeoPoint2D p1, ref GeoPoint2D p2, out double pos1, out double pos2)
        {
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(curve1, Color.Red, 1);
            dc.Add(curve2, Color.Green, 2);
#endif
            bool found;
            GeoPoint2D pp1 = p1, pp2 = p2; // Startwrte merken
            GeoPoint2D fix = new GeoPoint2D(p1, p2); // der sollte zwischen den beiden Konturen liegen
            for (int mode = 0; mode < 3; mode++)
            {   // um die Mitte, den Anfang oder das Ende der Verbindung p1-p2 taumeln lassen
                found = true;
                int cnt = 0;
                do
                {   // Suche nach Schnittpunkten der Linie p1,p2 mit den beiden Kurven
                    // korrigiere p1 und p2, so dass die Kurven in den beiden Punkten symmetrisch schneiden
#if DEBUG
                    dc.Add(new Line2D(p1, p2), Color.Blue, mode);
#endif
                    GeoPoint2D mp = new GeoPoint2D(p1, p2); // der sollte zwischen den beiden Konturen liegen
                    GeoVector2D dirl = p2 - p1;
                    // die Schnittlinie hat die doppelte Länge, damit auch ein Schnittpunkt gefunden wird
                    // Intersect findet offensichtlich nur innerhalb der Linie. Die Breite der Linie darf sich also nicht aprupt ändern
                    // Problem: wenn curve1==curve2: mp muss dazwischen sein, sonst werden falsche Schnittpunkte gefunden
                    GeoPoint2DWithParameter[] ips = curve1.Intersect(mp, mp - 2 * dirl);
                    pos1 = -1.0;
                    double minpar = double.MaxValue;
                    for (int i = 0; i < ips.Length; i++)
                    {
                        if (ips[i].par2 > 0.0 && ips[i].par2 < minpar && curve1.IsParameterOnCurve(ips[i].par1))
                        {
                            minpar = ips[i].par2;
                            pos1 = ips[i].par1;
                            p1 = ips[i].p;
                        }
                    }
                    ips = curve2.Intersect(mp, mp + 2 * dirl);
                    pos2 = -1.0;
                    minpar = double.MaxValue;
                    for (int i = 0; i < ips.Length; i++)
                    {
                        if (ips[i].par2 > 0.0 && ips[i].par2 < minpar && curve2.IsParameterOnCurve(ips[i].par1))
                        {
                            minpar = ips[i].par2;
                            pos2 = ips[i].par1;
                            p2 = ips[i].p;
                        }
                    }
                    ++cnt;
                    if (!curve1.IsParameterOnCurve(pos1) || !curve2.IsParameterOnCurve(pos2) || cnt > 30) // kein echter Schnitt oder konvergiert nicht
                    {
                        found = false;
                        p1 = pp1;
                        p2 = pp2;
                        break;
                    }
                    GeoVector2D dir1 = curve1.DirectionAt(pos1).Normalized;
                    GeoVector2D dir2 = -curve2.DirectionAt(pos2).Normalized;
                    SweepAngle sw = new SweepAngle(dir1, dir2);
                    if (Math.Abs(sw.Radian) > Math.PI / 6.0)
                    {
                        found = false;
                        p1 = pp1;
                        p2 = pp2;
                        break; // Öffnungswinkel größer 30°
                    }
                }
                while (GetBetterDirection(curve1, curve2, ref p1, ref p2, pos1, pos2, mode, fix, cnt > 10)); // 0, Mitte, 1: p1, 2: p2
                if (found)
                {
                    return IsInside(p1, p2); // wenn es was andere schneidet, dann wird false geliefert
                }
            }
            // folgendes kommtn nach GetBetterDirection
            pos1 = pos2 = -1;
            return false;
        }

        private bool GetBetterDirection(ICurve2D curve1, ICurve2D curve2, ref GeoPoint2D p1, ref GeoPoint2D p2, double pos1, double pos2, int mode, GeoPoint2D fix, bool halfOffset)
        {   // gesucht: bessere Werte für p1, p2, damit diese Verbindung symmetrisch zu den beiden Kurven ist 
            // fix: der Fixpunkt soll sich nicht ändern, sonst kann die Linie langsam wegwandern (nur bei mode 0 wichtig, sonst wird p1 bzw. p2 sowieso festgehalten
            // halfOffset: es scheint nicht zu konvergieren, versuiche es mit halbem Offset
            try
            {
                GeoVector2D dir1 = curve1.DirectionAt(pos1).Normalized;
                GeoVector2D dir2 = -curve2.DirectionAt(pos2).Normalized;
                GeoVector2D dirm = (dir1 + dir2).Normalized;
                GeoVector2D dirp = (p2 - p1).Normalized;
                double a = Math.Abs(new SweepAngle(dirp.ToLeft(), dirm));
                if (a > Math.PI / 2.0) dirm = -dirm;
                if (halfOffset) dirm = 0.5 * (dirm + dirp.ToLeft());
                if (Math.Abs(dirm * dirp) < Math.PI / 180) return false; // wenn der Winkelunterschied kleiner 1° ist, dann gehts nicht besser (cos um 90° ist linear)
                double l = p1 | p2; // die neue Linie soll genauso lang sein, wie die alte
                switch (mode)
                {
                    case 0:
                        GeoPoint2D pm = new GeoPoint2D(p1, p2);
                        p1 = fix + l / 2 * dirm.ToLeft();
                        p2 = fix + l / 2 * dirm.ToRight();
                        break;
                    case 1:
                        p2 = p1 + l * dirm.ToRight();
                        break;
                    case 2:
                        p1 = p2 + l * dirm.ToLeft();
                        break;
                }
                // p1 und p2 liegen jetzt nicht mehr auf den Kurven, geben nur die Schnittlinie vor
                return true;
            }
            catch (GeoVectorException)
            {
                return false;
            }
        }

        private void calculateAlt(SimpleShape ss)
        {
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(ss.DebugList);
#endif
            List<Path2D> allParts = new List<Path2D>();
            quadtree = new QuadTree<ICurve2D>(extent * 1.01);
            Path2D ol = ss.Outline.AsPath();
            Path2D[] olpaths = ol.SplitAtKink(kinktol);
            for (int i = 0; i < olpaths.Length; i++)
            {
                quadtree.AddObject(olpaths[i]);
            }
            allParts.AddRange(olpaths);
            for (int i = 0; i < ss.Holes.Length; i++)
            {
                Path2D hl = ss.Holes[i].AsPath();
                hl.Reverse(); // Orientierung wg. innen/außen
                Path2D[] paths = hl.SplitAtKink(kinktol);
                for (int j = 0; j < paths.Length; j++)
                {
                    quadtree.AddObject(paths[j]);
                }
                allParts.AddRange(paths);
            }
            // Alle Konturstücke sind an den Knicken aufgeteilt und befinden sich im Quadtree
            // es wird versucht Linienenden zu beseitigen, also solche Linienstücke, die nur einen Strich abschließen, beim Minuszeichen den vorederen und hinterenStrich
            for (int i = allParts.Count - 1; i >= 0; --i)
            {
                double mw = maxWidth;
                if (strokeWidth > 0.0) mw = 2 * strokeWidth;
                if (allParts[i].SubCurvesCount == 1 && allParts[i].SubCurves[0] is Line2D && allParts[i].Length < mw)
                {
                    // eine kurze Linie
                    bool startCaps = false, endCaps = false;
                    ICurve2D[] close = quadtree.GetObjectsCloseTo(allParts[i]);
                    for (int j = 0; j < close.Length; j++)
                    {
                        if (Precision.IsEqual(close[j].EndPoint, allParts[i].StartPoint))
                        {
                            SweepAngle sw = new SweepAngle(close[j].EndDirection, allParts[i].StartDirection);
                            if (sw > Math.PI / 4.0 && sw < 3 * Math.PI / 4.0) startCaps = true;
                        }
                        if (Precision.IsEqual(close[j].StartPoint, allParts[i].EndPoint))
                        {
                            SweepAngle sw = new SweepAngle(allParts[i].EndDirection, close[j].StartDirection);
                            if (sw > Math.PI / 4.0 && sw < 3 * Math.PI / 4.0) endCaps = true;
                        }
                    }
                    if (startCaps && endCaps)
                    {
                        quadtree.RemoveObject(allParts[i]);
                        allParts.RemoveAt(i); // es handelt sich hier um einen Abschluss
                    }
                }
            }
            // An (10) Stellen wird versucht mit einem Normalenvektor nach links
            // von (1/4) der Breite bzw. Höhe eine Kontur zu treffen. Mit den getroffenen Konturen (kann auch das Teilstück selbst sein)
            // wird versucht, eine Mittellinie zu finden.
            int numSamples = 10;
            for (int i = 0; i < allParts.Count; i++)
            {
                Set<ICurve2D> toCheckWith = new Set<ICurve2D>();
                for (int j = 1; j < numSamples - 1; j++) // Start- und Endpunkt ignorieren
                {
                    double pos = (double)j / (double)numSamples;
                    GeoPoint2D ps = allParts[i].PointAt(pos);
                    GeoVector2D dir = allParts[i].DirectionAt(pos);
                    GeoPoint2D pe = ps + maxWidth * dir.ToLeft().Normalized;
                    ICurve2D[] hit = quadtree.GetObjectsCloseTo(new Line2D(ps, pe));
                    toCheckWith.AddMany(hit);
                }
                //FindMiddleLine(allParts[i]);
                //double minpc1, maxpc1, minpc2, maxpc2;
                //GetMiddlePath(allParts[1], allParts[5], out minpc1, out maxpc1, out minpc2, out maxpc2);
#if DEBUG
                DebuggerContainer dc1 = new DebuggerContainer();
                dc1.Add(allParts[i], Color.Red, 0);
                int dbg = 0;
                foreach (ICurve2D cv in toCheckWith)
                    dc1.Add(cv, Color.Blue, dbg++);
#endif
                foreach (ICurve2D cv in toCheckWith)
                {
                    double minpc1, maxpc1, minpc2, maxpc2;
                    Polyline2D pl2d = GetMiddlePathAlt(allParts[i], cv, out minpc1, out maxpc1, out minpc2, out maxpc2);
                    if (pl2d != null)
                    {
                        // UserData Name ist ganz frei,da es ja nur lokale clones sind
                        List<ParallelInfo> pi = allParts[i].UserData.GetData("Parallel") as List<ParallelInfo>;
                        if (pi == null)
                        {
                            pi = new List<ParallelInfo>();
                            allParts[i].UserData.Add("Parallel", pi);
                        }
                        pi.Add(new ParallelInfo(cv, minpc1, maxpc1, pl2d));
                        pi = cv.UserData.GetData("Parallel") as List<ParallelInfo>;
                        if (pi == null)
                        {
                            pi = new List<ParallelInfo>();
                            cv.UserData.Add("Parallel", pi);
                        }
                        pi.Add(new ParallelInfo(allParts[i], minpc2, maxpc2, pl2d));
#if DEBUG
                        dc.Add(pl2d, Color.Red, i);
#endif
                        allPolyLines.Add(pl2d);
                    }
                }
            }
        }

        private Polyline2D GetMiddlePathAlt(ICurve2D curve1, ICurve2D curve2, out double minpc1, out double maxpc1, out double minpc2, out double maxpc2)
        {
            minpc1 = minpc2 = double.MaxValue;
            maxpc1 = maxpc2 = double.MinValue;
            // wenn die Kurven identisch sind, gehen wir mit einem Schchbrettraster drüber und suchen das kürzeste Intervall
            // wenn immer nur ein Schnittpunkt, dann ist es eine Linie
            GeoPoint2D p1, p2;
            double pc1 = 0.0, pc2 = 0.0;
            bool ok = false;
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            //dc.Add(this.shape.DebugList);
            dc.Add(curve1, Color.Red, 1);
            dc.Add(curve2, Color.Red, 2);
#endif

            if (curve1 != curve2)
            {
                double mindist = Curves2D.SimpleMinimumDistance(curve1, curve2, out p1, out p2);
                pc1 = curve1.PositionOf(p1);
                pc2 = curve2.PositionOf(p2);
                bool endOnC1 = (pc1 == 0.0 || pc1 == 1.0);
                bool endOnC2 = (pc2 == 0.0 || pc2 == 1.0);
                if (mindist < extent.Size / 10) // eine größte Breite muss gegeben werden, damit die beiden äußeren Striche beim H kein Ergebnis liefern
                {
                    if (endOnC1 && endOnC2)
                    {
                        double ppc2 = pc2; // pc2 nicht überschreiben
                        if (FindBalancedPointAlt1(curve1, pc1, curve2, out ppc2))
                        {
                            pc2 = ppc2;
                            ok = true;
                        }
                        else if (FindBalancedPointAlt1(curve2, pc2, curve1, out pc1))
                        {
                            ok = true;
                        }
                        if (!ok)
                        {   // ein kleines Stück vom Anfang entfernt probieren. Wenn die beiden Enden sich schon
                            // symmetrisch treffen, dann existiert jeweils der Lotfußpunkt nicht
                            double dpar1 = precision / curve1.Length;
                            double dpar2 = precision / curve2.Length;
                            if (pc1 == 0.0) pc1 += dpar1;
                            else pc1 -= dpar1;
                            if (pc2 == 0.0) pc2 += dpar2;
                            else pc2 -= dpar2;
                            ppc2 = pc2; // pc2 nicht überschreiben
                            if (FindBalancedPointAlt1(curve1, pc1, curve2, out ppc2))
                            {
                                pc2 = ppc2;
                                ok = true;
                            }
                            else if (FindBalancedPointAlt1(curve2, pc2, curve1, out pc1))
                            {
                                ok = true;
                            }
                        }
                    }
                    else if (endOnC1)
                    {
                        if (FindBalancedPointAlt1(curve1, pc1, curve2, out pc2))
                        {
                            ok = true;
                        }
                    }
                    else if (endOnC2)
                    {
                        if (FindBalancedPointAlt1(curve2, pc2, curve1, out pc1))
                        {
                            ok = true;
                        }
                    }
                    else
                    {   // hier steht die Verbindung von pc1 und pc2 senkrecht auf beiden Kurven
                        ok = true;
                    }
                    if (ok)
                    {
                    }
                }
            }
            else
            {
                p2 = p1 = GeoPoint2D.Origin; // damit es kompiliert
                for (int i = 0; i <= 10; i++)
                {
                    double pos = (double)(i) / 10.0;
                    if (FindBalancedPointAlt1(curve1, pos, curve2, out pc2))
                    {
                        ok = true;
                        pc1 = pos;
                        p1 = curve1.PointAt(pos);
                        p2 = curve2.PointAt(pc2);
                        break;
                    }
                }
            }
            if (ok)
            {   // überprüfen, ob die Verbindungslinie auch nichts anderes schneidet, dann darf sie nicht verwendet werden.
                // p1 = curve1.PointAt(pc1); stimmt ja noch
                p2 = curve2.PointAt(pc2);
                Line2D tst = new Line2D(p1, p2);
                ICurve2D[] intsectwith = quadtree.GetObjectsCloseTo(tst);
                for (int i = 0; i < intsectwith.Length; i++)
                {
                    GeoPoint2DWithParameter[] ips = tst.Intersect(intsectwith[i]);
                    for (int j = 0; j < ips.Length; j++)
                    {
                        if (ips[j].par1 > 1e-4 && ips[j].par1 < 1 - 1e-4 && ips[j].par2 >= 0.0 && ips[j].par2 <= 1.0)
                        {
                            ok = false;
#if DEBUG
                            dc.Add(tst);
#endif
                            break; // die Verbindungslinie schneidet andere Objekte
                        }
                    }
                }
            }
            if (ok)
            {
                // zwei Punkte mit gleichwinkliger innerer Verbindung auf den beiden Kurven gefunden. Jetzt in beide Richtungen voranschreiten
                // und weitere solche Verbindungen suchen
                minpc1 = maxpc1 = pc1;
                minpc2 = maxpc2 = pc2;

                double startpc1 = pc1;
                double startpc2 = pc2;
                List<GeoPoint2D> pl = new List<GeoPoint2D>();
                List<double> opening = new List<double>(); // parallele Liste mit dem Öffnungswinkel der beiden Kurven an der gegebenen Stelle
                pl.Add(new GeoPoint2D(p1, p2)); // mit dem Mittelpunkt anfangen
                opening.Add(Math.Abs(new SweepAngle(curve1.DirectionAt(pc1), -curve2.DirectionAt(pc2))));
                double maxpstep = precision / curve1.Length;
                while (pc1 > 0.0)
                {
                    pc1 -= maxpstep;
                    if (pc1 < 0.0) pc1 = 0.0;
                    if (FindBalancedPointAlt1(curve1, pc1, curve2, out pc2))
                    {
                        pl.Insert(0, new GeoPoint2D(curve1.PointAt(pc1), curve2.PointAt(pc2))); // davor einfügen
                        opening.Insert(0, Math.Abs(new SweepAngle(curve1.DirectionAt(pc1), -curve2.DirectionAt(pc2))));
                        if (pc2 > maxpc2) maxpc2 = pc2;
                        if (pc2 < minpc2) minpc2 = pc2;
                    }
                    else break;
                    minpc1 = pc1;
                }
                pc1 = startpc1;
                while (pc1 < 1.0)
                {
                    pc1 += maxpstep;
                    if (pc1 > 1.0) pc1 = 1.0;
                    if (FindBalancedPointAlt1(curve1, pc1, curve2, out pc2))
                    {
                        pl.Add(new GeoPoint2D(curve1.PointAt(pc1), curve2.PointAt(pc2))); // danach einfügen
                        opening.Add(Math.Abs(new SweepAngle(curve1.DirectionAt(pc1), -curve2.DirectionAt(pc2))));
                        if (pc2 > maxpc2) maxpc2 = pc2;
                        if (pc2 < minpc2) minpc2 = pc2;
                    }
                    else break;
                    maxpc1 = pc1; // beinhaltet ggf. auch den Überschuss
                }
                if (pl.Count > 1)
                {
                    ok = false;
                    for (int i = 0; i < pl.Count; i++)
                    {
                        if (opening[i] < Math.PI / 18) ok = true; // Öffnungswinkel der beiden Kurven muss an einer Stelle kleiner 10° sein
                    }
                    if (ok)
                    {
                        Polyline2D pl2d = new Polyline2D(pl.ToArray());
#if DEBUG
                        dc.Add(this.shape.DebugList);
                        dc.Add(pl2d);
#endif
                        return pl2d;
                    }
                }
            }
            return null;
        }

        private bool FindBalancedPointAlt1(ICurve2D curve1, double pos1, ICurve2D curve2, out double pos2)
        {
            pos2 = -1; // damit out gesetzt ist

            // suche mit einem Radarstrahl von pos1 aus  nach einem passenden Punkt auf curve2, so dass die Schnittwinkel gleich sind.

            // dazu gehen wir einmal senkrecht von curve1 aus nach links, zum anderen suchen wir einen Lotfußpunkt auf den anderen Kurve
            double lengthfactor = 1.0 / Math.Cos(paralleltol); // die seitlichen Radarstrahlen müssen so verlängert werden
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(curve1);
            dc.Add(curve2);
#endif
            GeoPoint2D fromHere = curve1.PointAt(pos1);
            // das sind die beiden Ausgangspunkte, die es immer geben muss
            // der Abstand zwischen ihnen ist evtl. etwas kleiner als der Abstand der symmetrischen (gleichschenklichen) Verbindung, aber nicht viel,
            // da ja der Winkel nur wenig von 90° abweichen darf.
            double swperp = 0.0, swfoot = 0.0; // die beiden Schnittwinkel für senkrecht und Lotfußpunkt
            double posperp = -1.0, posfoot = -1.0; // die Positionen auf curve2 für die beiden begrenzenden Radarstrahlen
            GeoPoint2D pperp = GeoPoint2D.Origin, pfoot = GeoPoint2D.Origin;

            GeoVector2D perp = curve1.DirectionAt(pos1).ToLeft().Normalized; // links geht immer nach innen
            GeoPoint2DWithParameter[] ips = curve2.Intersect(fromHere + 1e-4 * perp, fromHere + maxWidth * perp);
            if (ips != null && ips.Length > 0)
            {
                double pos = 2;
                double mindist = double.MaxValue;
                for (int i = 0; i < ips.Length; i++)
                {
                    double dist = fromHere | ips[i].p;
                    if (ips[i].par1 <= 1.0 && ips[i].par1 >= 0.0 && ips[i].par2 <= 1.0 && ips[i].par2 > 0.0 && dist < mindist)
                    {
                        mindist = dist;
                        pos = ips[i].par1;
                        pperp = ips[i].p;
                    }
                }
                if (pos <= 1.0)
                {
                    posperp = pos;
                    SweepAngle sw1 = new SweepAngle(curve1.DirectionAt(pos1), pperp - fromHere);
                    SweepAngle sw2 = new SweepAngle(pperp - fromHere, curve2.DirectionAt(pos));
                    swperp = sw2 - sw1;
                    pos2 = posperp;
                    if (Precision.SameDirection(curve1.DirectionAt(pos1), curve2.DirectionAt(posperp), false)) return true; // senkrechter Schnitt
                }
            }
            if (posperp < 0.0) return false;
            GeoPoint2D[] fps = curve2.PerpendicularFoot(fromHere); // die Fußpunkte
            if (fps.Length == 0)
            {   // curve2 ist ja ein Path und kann kleine Knicke enthalten, die auch als Fußpunkt gelten können
                Path2D p2d = curve2 as Path2D;
                if (p2d != null)
                {
                    List<GeoPoint2D> addfps = new List<GeoPoint2D>();
                    for (int i = 0; i < p2d.SubCurvesCount - 1; i++)
                    {
                        GeoVector2D snorm = p2d.SubCurves[i].EndDirection.ToRight();
                        GeoVector2D enorm = p2d.SubCurves[i + 1].StartDirection.ToRight();
                        bool l1 = Geometry.OnLeftSide(fromHere, p2d.SubCurves[i].EndPoint, snorm);
                        bool l2 = Geometry.OnLeftSide(fromHere, p2d.SubCurves[i + 1].StartPoint, enorm);
                        // die Normalen in den beiden Punkten müssen den Punkt auf verschiedenen Seiten haben, dann kann es einen Lotfußpunkt geben
                        // geometrisch bedeutet das, dass die Normale auf der Kurve den Punkt überstreicht
                        if (l1 != l2)
                        {
                            addfps.Add(p2d.SubCurves[i].EndPoint);
                        }
                    }
                    fps = addfps.ToArray();
                }
            }
            {
                double mindist = double.MaxValue;
                GeoPoint2D pp = GeoPoint2D.Origin;
                for (int i = 0; i < fps.Length; i++)
                {
                    double dist = fps[i] | fromHere;
                    if (dist > 1e-4 && dist < mindist)
                    {
                        // weitere Bedingung: die Kurve muss von innen getroffen werden
                        double pos = curve2.PositionOf(fps[i]);
                        if (pos >= 0.0 && pos <= 1.0)
                        {
                            SweepAngle sw = new SweepAngle(fps[i] - fromHere, curve2.DirectionAt(pos));
                            if (sw > 0)
                            {
                                mindist = dist;
                                pp = fps[i];
                            }
                        }
                    }
                }
                if (mindist < maxWidth)
                {
                    pfoot = pp;
                    posfoot = curve2.PositionOf(pfoot);
                    SweepAngle sw1 = new SweepAngle(curve1.DirectionAt(pos1), pfoot - fromHere);
                    SweepAngle sw2 = new SweepAngle(pfoot - fromHere, curve2.DirectionAt(posfoot));
                    swfoot = sw2 - sw1;
                    pos2 = posfoot;
                    if (Precision.SameDirection(curve1.DirectionAt(pos1), curve2.DirectionAt(posfoot), false)) return true; // senkrechter Schnitt
                }
            }

            if (posfoot >= 0.0 && posperp >= 0.0)
            {
                do
                {
#if DEBUG
                    DebuggerContainer dc1 = new DebuggerContainer();
                    dc1.Add(curve1, Color.Green, 1);
                    dc1.Add(curve2, Color.Blue, 1);
                    dc1.Add(new Line2D(fromHere, pfoot), Color.Red, 1);
                    dc1.Add(new Line2D(fromHere, pperp), Color.Red, 1);
#endif
                    SweepAngle swl1 = new SweepAngle(curve1.DirectionAt(pos1), pperp - fromHere);
                    SweepAngle swl2 = new SweepAngle(pperp - fromHere, curve2.DirectionAt(posperp));
                    SweepAngle swr1 = new SweepAngle(curve1.DirectionAt(pos1), pfoot - fromHere);
                    SweepAngle swr2 = new SweepAngle(pfoot - fromHere, curve2.DirectionAt(posfoot));
                    double lswd = swl2 - swl1;
                    double rswd = swr2 - swr1;
                    if (lswd * rswd > 0)
                    {   // kann ja nur beim 1. Durchlauf passieren
                        return false;
                    }
                    if (lswd == 0.0)
                    {
                        pos2 = posperp;
                        if (IsInside(fromHere, pperp))
                        {
                            return true;
                        }
                    }
                    if (rswd == 0.0)
                    {
                        pos2 = posfoot;
                        if (IsInside(fromHere, pfoot))
                        {
                            return true;
                        }
                    }
                    // Zwischenposition bestimmen
                    double pos = (posperp + posfoot) / 2.0;
                    GeoPoint2D pp = curve2.PointAt(pos);
                    SweepAngle sw1 = new SweepAngle(curve1.DirectionAt(pos1), pp - fromHere);
                    SweepAngle sw2 = new SweepAngle(pp - fromHere, curve2.DirectionAt(pos));
                    double swd = sw2 - sw1;
                    double a1 = Math.Abs(SweepAngle.ToLeft - sw2);
                    double a2 = Math.Abs(SweepAngle.ToLeft - sw1);
                    if (swd == 0.0)
                    {
                        pos2 = pos;
                        return IsInside(fromHere, pp); // wenn es was andere schneidet, dann wird false geliefert
                    }
                    if (swd * lswd > 0.0)
                    {
                        lswd = swd;
                        posperp = pos;
                        pperp = pp;
                    }
                    else
                    {
                        rswd = swd;
                        posfoot = pos;
                        pfoot = pp;
                    }
                    if (Math.Abs(posfoot - posperp) < 1e-4)
                    {
                        pos2 = (posperp + posfoot) / 2.0;
                        return IsInside(fromHere, pp); // wenn es was andere schneidet, dann wird false geliefert
                    }
                } while (true);
            }
            return false;
        }

        private bool IsInside(GeoPoint2D p1, GeoPoint2D p2)
        {   // stellt fest, ob id Verbindung von p1 zu p2 ganz innerhalb liegt.
            // genau genommen nur, ob es keine Schnittpunkt mit den Objekten aus dem QuadTree gibt, außer am Anfang und am Ende.
            Line2D l2d = new Line2D(p1, p2);
            ICurve2D[] close = quadtree.GetObjectsCloseTo(l2d);
            for (int i = 0; i < close.Length; i++)
            {
                GeoPoint2DWithParameter[] ips = l2d.Intersect(close[i]);
                for (int j = 0; j < ips.Length; j++)
                {
                    if (ips[j].par1 > 1e-4 && ips[j].par1 < 1 - 1e-4 && ips[j].par2 > 1e-4 && ips[j].par2 < 1 - 1e-4) return false;
                }
            }
            return true;
        }

        private bool FindBalancedPointAlt(ICurve2D curve1, double pos1, ICurve2D curve2, out double pos2)
        {
            // suche mit einem Radarstrahl von pos1 aus senkrecht nach links mit einer Abweichung von maximal paralleltol
            // nach einem passenden Punkt auf curve2, so dass die Schnittwinkel gleich sind.
            double lengthfactor = 1.0 / Math.Cos(paralleltol); // die seitlichen Radarstrahlen müssen so verlängert werden
#if DEBUG
            DebuggerContainer dc = new DebuggerContainer();
            dc.Add(curve1);
            dc.Add(curve2);
#endif
            GeoPoint2D fromHere = curve1.PointAt(pos1);
            // das sind die beiden Ausgangspunkte, die es immer geben muss
            // der Abstand zwischen ihnen ist evtl. etwas kleiner als der Abstand der symmetrischen (gleichschenklichen) Verbindung, aber nicht viel,
            // da ja der Winkel nur wenig von 90° abweichen darf.
            double swd = 0.0;
            double posm = 0.0;
            GeoVector2D perp = curve1.DirectionAt(pos1).ToLeft().Normalized; // links geht immer nach innen
            GeoVector2D left = ModOp2D.Rotate(new SweepAngle(paralleltol)) * perp; // maximale Abweichung nach links
            GeoVector2D right = ModOp2D.Rotate(new SweepAngle(-paralleltol)) * perp; // maximale Abweichung nach rechts.
            GeoPoint2DWithParameter[] ips = curve2.Intersect(fromHere + 1e-4 * perp, fromHere + maxWidth * perp);
            if (ips != null && ips.Length > 0)
            {
                double pos = 2;
                for (int i = 0; i < ips.Length; i++)
                {
                    if (ips[i].par1 <= 1.0 && ips[i].par1 >= 0.0 && ips[i].par2 <= 1.0 && ips[i].par2 >= 0.0 && ips[i].par1 < pos)
                        pos = ips[i].par1;
                }
                if (pos <= 1.0)
                {
                    posm = pos2 = pos;
                    GeoPoint2D pp = curve2.PointAt(pos2);
                    SweepAngle sw1 = new SweepAngle(curve1.DirectionAt(pos1), pp - fromHere);
                    SweepAngle sw2 = new SweepAngle(pp - fromHere, curve2.DirectionAt(pos));
                    swd = sw2 - sw1;
                    if (Precision.SameDirection(curve1.DirectionAt(pos1), curve2.DirectionAt(pos2), false)) return true; // senkrechter Schnitt
                }
            }

            // falls die Kurven identisch sind, wollen wir den Startpunkt nicht als Schnittpunkt
            GeoPoint2DWithParameter[] lips = curve2.Intersect(fromHere + 1e-4 * left, fromHere + lengthfactor * maxWidth * left);
#if DEBUG
            dc.Add(new Line2D(fromHere + 1e-6 * left, fromHere + lengthfactor * maxWidth * left));
#endif
            if (lips == null || lips.Length == 0)
            {   // der linke Strahl geht ins Leere, suche einen Punkt auf der Kurve, der möglichst weit links im Radarbereich liegt
                GeoPoint2D[] tp = curve2.TangentPoints(fromHere, fromHere);
                if (tp == null) tp = new GeoPoint2D[0];
                double mindist = double.MaxValue;
                for (int i = -2; i < tp.Length; i++)
                {
                    GeoPoint2D pp;
                    switch (i)
                    {
                        case -2:
                            pp = curve2.StartPoint;
                            break;
                        case -1:
                            pp = curve2.EndPoint;
                            break;
                        default:
                            pp = tp[i];
                            break;
                    }
                    double dist = pp | fromHere;
                    if (dist > 1e-6)
                    {
                        SweepAngle sw = new SweepAngle(left, pp - fromHere); // wie weit muss man nach rechts (negativ!) drehen, um zu pp zu kommen
                        // von den in Frage kommenden Punkten nimm den zu fromHere nächstgelegenen
                        if (-sw.Radian < 2 * paralleltol && sw.Radian < 0 && dist < mindist)
                        {
                            mindist = dist;
                            lips = new GeoPoint2DWithParameter[1];
                            lips[0] = new GeoPoint2DWithParameter(pp, curve2.PositionOf(pp), 0.5); // der par2 ist egal
                        }
                    }
                }
            }
            GeoPoint2DWithParameter[] rips = curve2.Intersect(fromHere + 1e-4 * right, fromHere + lengthfactor * maxWidth * right);
#if DEBUG
            dc.Add(new Line2D(fromHere + 1e-6 * right, fromHere + lengthfactor * maxWidth * right));
#endif
            if (rips == null || rips.Length == 0)
            {   // der rechte Strahl geht ins Leere, suche einen Punkt auf der Kurve, der möglichst weit rectes im Radarbereich liegt
                GeoPoint2D[] tp = curve2.TangentPoints(fromHere, fromHere);
                if (tp == null) tp = new GeoPoint2D[0];
                double mindist = double.MaxValue;
                for (int i = -2; i < tp.Length; i++)
                {
                    GeoPoint2D pp;
                    switch (i)
                    {
                        case -2:
                            pp = curve2.StartPoint;
                            break;
                        case -1:
                            pp = curve2.EndPoint;
                            break;
                        default:
                            pp = tp[i];
                            break;
                    }
                    double dist = pp | fromHere;
                    if (dist > 1e-6)
                    {

                        SweepAngle sw = new SweepAngle(right, pp - fromHere); // wie weit muss man nach links (positiv!) drehen, um zu pp zu kommen
                        // von den in Frage kommenden Punkten nimm den zu fromHere nächstgelegenen
                        if (sw.Radian < 2 * paralleltol && sw.Radian > 0 && dist < mindist)
                        {
                            mindist = dist;
                            rips = new GeoPoint2DWithParameter[1];
                            rips[0] = new GeoPoint2DWithParameter(pp, curve2.PositionOf(pp), 0.5); // der par2 ist egal
                        }
                    }
                }
            }
            if (rips != null && rips.Length > 0 && lips != null && lips.Length > 0)
            {   // es gibt Startwerte für den linken und den rechten Strahl
                // den besten zuerst (auf der Linie am nächsten gelegen, also par2)
                GeoPoint2D pleft = GeoPoint2D.Origin;
                GeoPoint2D pright = GeoPoint2D.Origin;
                double lpos = 2;
                double rpos = 2;
                for (int i = 0; i < rips.Length; i++)
                {
                    if (rips[i].par1 <= 1.0 && rips[i].par1 >= 0.0 && rips[i].par1 < rpos)
                    {
                        rpos = rips[i].par1;
                        pright = rips[i].p;
                    }
                }
                for (int i = 0; i < lips.Length; i++)
                {
                    if (lips[i].par1 <= 1.0 && lips[i].par1 >= 0.0 && lips[i].par1 < lpos)
                    {
                        lpos = lips[i].par1;
                        pleft = lips[i].p;
                    }
                }
                if (lpos > 1 || rpos > 1)
                {
                    pos2 = -1.0;
                    return false;
                }
                // jetzt gibt es einen gültigen linken und einen rechten Strahl
                // wir nehmen den Schnittpunkt der beiden Tangenten und schauen, ob er rechts oder links von der Mittelsenkrechten der Verbindung liegt
                if (Precision.IsEqual(pleft, fromHere))
                {
                    pos2 = lpos;
                    return true;
                }
                if (Precision.IsEqual(pright, fromHere))
                {
                    pos2 = rpos;
                    return true;
                }
                left = (pleft - fromHere).Normalized;
                right = (pright - fromHere).Normalized;
#if DEBUG
                dc.Add(new Line2D(fromHere + 1e-6 * left, fromHere + maxWidth * left), Color.Red, 5);
                dc.Add(new Line2D(fromHere + 1e-6 * right, fromHere + maxWidth * right), Color.Red, 5);
#endif
                do
                {
#if DEBUG
                    DebuggerContainer dc1 = new DebuggerContainer();
                    dc1.Add(curve1);
                    dc1.Add(curve2);
#endif
                    SweepAngle swl1 = new SweepAngle(curve1.DirectionAt(pos1), pleft - fromHere);
                    SweepAngle swl2 = new SweepAngle(pleft - fromHere, curve2.DirectionAt(lpos));
                    SweepAngle swr1 = new SweepAngle(curve1.DirectionAt(pos1), pright - fromHere);
                    SweepAngle swr2 = new SweepAngle(pright - fromHere, curve2.DirectionAt(rpos));
                    double lswd = swl2 - swl1;
                    double rswd = swr2 - swr1;
                    if (lswd * rswd > 0)
                    {   // kann ja nur beim 1. Durchlauf passieren, dann auch checken, ob der mittlere Strahl auch schlecht ist
                        if (lswd * swd < 0)
                        {   // der mittlere Strahl hat umgekehrtes Vorzeichen, ersetze den schlechteren (größeren) der beiden
                            if (Math.Abs(rswd) < Math.Abs(lswd))
                            {
                                lswd = swd;
                                lpos = posm;
                            }
                            else
                            {
                                rswd = swd;
                                rpos = posm;
                            }
                        }
                        else
                        {
                            pos2 = -1;
                            return false;
                        }
                    }
                    if (lswd == 0.0)
                    {
                        pos2 = lpos;
                        return true;
                    }
                    if (rswd == 0.0)
                    {
                        pos2 = rpos;
                        return true;
                    }
                    // Zwischenposition bestimmen
                    double pos = (lpos + rpos) / 2.0;
                    GeoPoint2D pp = curve2.PointAt(pos);
                    SweepAngle sw1 = new SweepAngle(curve1.DirectionAt(pos1), pp - fromHere);
                    SweepAngle sw2 = new SweepAngle(pp - fromHere, curve2.DirectionAt(pos));
                    swd = sw2 - sw1;
                    if (swd == 0.0)
                    {
                        pos2 = pos;
                        return true;
                    }
                    if (swd * lswd > 0.0)
                    {
                        lswd = swd;
                        lpos = pos;
                        pleft = pp;
                    }
                    else
                    {
                        rswd = swd;
                        rpos = pos;
                        pright = pp;
                    }
                    if (Math.Abs(rpos - lpos) < 1e-4)
                    {
                        pos2 = (lpos + rpos) / 2.0;
                        return true;
                    }
                } while (true);
            }
            pos2 = -1.0;
            return false;
        }

        private bool GetOtherPoint(Path2D fromHere, double par, out GeoPoint2D otherPoint, out double otherPar)
        {
            otherPoint = GeoPoint2D.Origin;
            otherPar = -1.0; // für leeres Ergebnis

            GeoPoint2D p = fromHere.PointAt(par);
            GeoVector2D dir1 = fromHere.DirectionAt(par);
            GeoVector2D v = dir1.ToLeft();
            Line2D l = new Line2D(p, p + extent.Size * v.Normalized); // linksabbiegend von diesem Punkt aus
            GeoPoint2D bestPoint = GeoPoint2D.Origin;
            double bestDist = double.MaxValue;
            double parOnBetsCurve = -1.0;
            ICurve2D bestCurve = null;
            foreach (ICurve2D c in quadtree.ObjectsCloseTo(l))
            {
                GeoPoint2DWithParameter[] ips = l.Intersect(c);
                for (int i = 0; i < ips.Length; i++)
                {
                    if (ips[i].par1 > 0.001 && ips[i].par1 < bestDist)
                    {
                        bestCurve = c;
                        parOnBetsCurve = ips[i].par2;
                        bestPoint = ips[i].p;
                        bestDist = ips[i].par1;
                    }
                }
            }
            if (bestCurve != null)
            {
                GeoVector2D dir2 = bestCurve.DirectionAt(parOnBetsCurve);
                // wenn der Schitt genau rechtwinklig ist, dann sind dir1 und dir2 genau entgegengesetzt
                // wir suchen jetzt in einer gewissen Umgebung um den Punkt auf der gefundenen Kurve nach einem Punkt,
                // so dass Winkel (dir1,v) == Winkel(v, -dir2)
                //dir2 = bestCurve.Length * dir2.Normalized; // eine Änderung um 1  ergibt die Länge der Kurve
                double dist = bestPoint | p; // Abstand der beiden Punkte
                // cos(paralleltol) = dir2l / dist
                double dir2l = Math.Cos(paralleltol) * dist;
                double parstep = dir2l / bestCurve.Length; // mit diesem Schritt sollten wir nach der besten Stelle für die Querverbindung suchen.
                // wir suchen den Punkt auf der bestCurve, der mit dem AusgangsPunkt p und den beiden TangentenRichtungen ein gleichseitiges Dreieck bildet
                SweepAngle s1 = new SweepAngle(dir1, bestPoint - p);
                SweepAngle s2 = new SweepAngle(bestPoint - p, dir2); // dir2 geht ja in die entgegengesetzte Richtung wie dir1
                while (Math.Abs(s2 - s1) > 0.001)
                {
#if DEBUG
                    DebuggerContainer dc = new DebuggerContainer();
                    dc.Add(bestCurve);
                    dc.Add(fromHere);
                    dc.Add(new Line2D(p, bestPoint));
#endif
                    if (s2 > s1) parOnBetsCurve += parstep;
                    else parOnBetsCurve -= parstep;
                    if (parOnBetsCurve > 1.0) parOnBetsCurve = 1.0;
                    if (parOnBetsCurve < 0.0) parOnBetsCurve = 0.0;
                    parstep /= 2.0;
                    if (parstep < 1e-6)
                    {
                        return false; // es konvertiert nicht, muss besser getestet werden
                    }
                    bestPoint = bestCurve.PointAt(parOnBetsCurve);
                    dir2 = bestCurve.DirectionAt(parOnBetsCurve);
                    s1 = new SweepAngle(dir1, bestPoint - p);
                    s2 = new SweepAngle(bestPoint - p, dir2);
#if DEBUG
                    dc.Add(new Line2D(p, bestPoint));
#endif
                }
                otherPoint = bestPoint;
                otherPar = parOnBetsCurve;
                return true;
            }

            return false;
        }

        internal void setForbiddenBands(double bmin, double bmax, double mmin, double mmax, double tmin, double tmax, double strokeWidth)
        {
            this.bmin = bmin;
            this.bmax = bmax;
            this.mmin = mmin;
            this.mmax = mmax;
            this.tmin = tmin;
            this.tmax = tmax;
            this.strokeWidth = strokeWidth;
        }
    }
}
