using CADability.Actions;
using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability.Shapes
{

    /// <summary>
    /// A simply connected 2d shape. It consists of a <see cref="Border"/> outline and 0 or more holes.
    /// The holes don't overlap (disjunct) and reside totally inside the outline.
    /// </summary>
    [Serializable()]
    public class SimpleShape : ISerializable, IQuadTreeInsertable, IComparable<SimpleShape>, IJsonSerialize
    {
        // Ist nicht wie Border unveränderlich und über ein Builder Objekt herzustellen?
        private Border outline;
        private Border[] holes;
        /// <summary>
        /// Constructs a simple shape by specifying the <paramref name="outline"/> and any number of <paramref name="holes"/>.
        /// </summary>
        /// <param name="outline">The outline</param>
        /// <param name="holes">The holes</param>
        public SimpleShape(Border outline, params Border[] holes)
        {
            this.outline = outline;
            this.outline.SplitSingleCurve();
            this.holes = (Border[])holes.Clone();
            this.outline.forceConnect(true);
            for (int i = 0; i < holes.Length; i++)
            {
                this.holes[i].forceConnect(true);
            }
        }
        /// <summary>
        /// Constructs a simple shape by specifying the <paramref name="outline"/> 
        /// </summary>
        /// <param name="outline">The outline</param>
        public SimpleShape(Border outline)
        {
            this.outline = outline;
            if (!this.outline.IsClosed) this.outline.forceConnect(true);
            this.outline.SplitSingleCurve();
            this.holes = new Border[0];
        }
        internal SimpleShape(BoundingRect rect)
        {
            Border bdr = rect.ToBorder();
            this.outline = bdr;
            this.holes = new Border[0];
        }
        public static SimpleShape ThickPolyLine(Polyline2D p2d, double radius)
        {
            CompoundShape cs = null;
            for (int i = 0; i < p2d.VertexCount - 1; i++)
            {
                if (i == 0)
                {
                    cs = new CompoundShape(new SimpleShape(Border.MakeRectangle(p2d.Vertex[i], p2d.Vertex[i + 1], radius)));
                }
                else
                {
                    CompoundShape cs1 = new CompoundShape(new SimpleShape(Border.MakeRectangle(p2d.Vertex[i], p2d.Vertex[i + 1], radius)));
                    CompoundShape cs2 = new CompoundShape(new SimpleShape(Border.MakeCircle(p2d.Vertex[i], radius)));
                    CompoundShape cs3 = cs1 + cs2;
                    cs = cs + cs3;
                }
            }
            CompoundShape cs4 = new CompoundShape(new SimpleShape(Border.MakeCircle(p2d.StartPoint, radius)));
            cs = cs + cs4;
            cs4 = new CompoundShape(new SimpleShape(Border.MakeCircle(p2d.EndPoint, radius)));
            cs = cs + cs4;
            return cs.SimpleShapes[0];
        }
        /// <summary>
        /// Returns a clone (deep copy) of this simple shape
        /// </summary>
        /// <returns>The clone</returns>
        public SimpleShape Clone()
        {
            if (holes != null && holes.Length > 0)
            {
                Border[] clonedholes = new Border[holes.Length];
                for (int i = 0; i < holes.Length; ++i)
                {
                    clonedholes[i] = holes[i].Clone();
                }
                return new SimpleShape(outline.Clone(), clonedholes);
            }
            else
            {
                return new SimpleShape(outline.Clone());
            }
        }
        //internal SimpleShape(CndOCas.FaceBounds fb)
        //{	// 
        //    outline = Border.FromWire2D(fb.Outline);
        //    holes = new Border[fb.HolesCount];
        //    for (int i=0; i<fb.HolesCount; ++i)
        //    {
        //        holes[i] = Border.FromWire2D(fb.GetHole(i));
        //    }
        //}
        /// <summary>
        /// Gets the outline of this simple shape. Do not modify the border because this shape might become invalid.
        /// To obtain a good performance the outline of this simple shape is returned, not a clone.
        /// </summary>
        public Border Outline
        {
            get
            {
                return outline;
            }
        }
        /// <summary>
        /// Gets the number of holes in this simple shape.
        /// </summary>
        public int NumHoles
        {
            get
            {
                return holes.Length;
            }
        }
        /// <summary>
        /// Gets the hole with the specified index. The original border is returned. Do not modify this or the simple shape might become invalid.
        /// If you need to modify it, make a clone first.
        /// </summary>
        /// <param name="Index"></param>
        /// <returns></returns>
        public Border Hole(int Index)
        {
            return holes[Index];
        }
        /// <summary>
        /// Returns all holes of this simple shape in an array. You may modify the array since it is a clone of the holes array in this object.
        /// But you may not modify the individual holes.
        /// </summary>
        public Border[] Holes
        {
            get
            {
                return (Border[])holes.Clone(); // kopie des Arrays, somit unveränderlich
            }
        }
        public IEnumerable<ICurve2D> Segments
        {
            get
            {
                return outline.Segments.Concat(holes.SelectMany(h => h.Segments));
            }
        }
        /// <summary>
        /// Returns the area of this simple shape.
        /// </summary>
        public double Area
        {
            get
            {   // die Border cachen die Area!
                double res = outline.Area;
                for (int i = 0; i < holes.Length; ++i)
                {
                    res -= holes[i].Area;
                }
                return res;
            }
        }
        /// <summary>
        /// Returns true, if the Area of this shape is 0.0
        /// </summary>
        public bool Empty => outline.IsEmpty;

        /// <summary>
        /// Tests whether the provided point is contained in this simple shape.
        /// </summary>
        /// <param name="p">The point to be examined</param>
        /// <param name="acceptOnCurve">true: accept points on the border as inside points, false: return true only for points totally inside this shape</param>
        /// <returns>true if the point is contained, false otherwise</returns>
        public bool Contains(GeoPoint2D p, bool acceptOnCurve)
        {
            Border.Position pos = outline.GetPosition(p);
            if (pos == Border.Position.Outside) return false;
            if ((!acceptOnCurve) && pos == Border.Position.OnCurve) return false;
            if (pos == Border.Position.OpenBorder) return false;

            for (int i = 0; i < holes.Length; ++i)
            {
                Border.Position hpos = holes[i].GetPosition(p);
                if (hpos == Border.Position.Inside) return false;
                if ((!acceptOnCurve) && hpos == Border.Position.OnCurve) return false;
            }
            return true;
        }
        public Border.Position GetPosition(GeoPoint2D p, double prec = 0.0)
        {
            Border.Position pos = outline.GetPosition(p, prec);
            if (pos == Border.Position.Outside) return Border.Position.Outside;
            if (pos == Border.Position.OnCurve) return Border.Position.OnCurve;
            for (int i = 0; i < holes.Length; ++i)
            {
                Border.Position hpos = holes[i].GetPosition(p, prec);
                if (hpos == Border.Position.Inside) return Border.Position.Outside;
                if (hpos == Border.Position.OnCurve) return Border.Position.OnCurve;
            }
            return Border.Position.Inside;
        }
        /// <summary>
        /// Returns a CompoundShape consisting of several SimpleShapes that touch each other.
        /// This SimpleShape is cut along the open Border <paramref name="ToSplitWith"/> into
        /// several subshapes.
        /// </summary>
        /// <param name="ToSplitWith">The curve to cut with (open Border)</param>
        /// <returns>Splitted shape</returns>
        public CompoundShape Split(Border ToSplitWith)
        {
            BorderOperation bo = new BorderOperation(outline, ToSplitWith);
            if (bo.IsValid())
            {
                try
                {
                    CompoundShape splittedOutline = bo.Split();
                    // i.A. mehrere SimpleShapes
                    foreach (Border hole in holes)
                    {
                        splittedOutline = splittedOutline - new CompoundShape(new SimpleShape(hole));
                    }
                    return splittedOutline;
                }
                catch (BorderException)
                {
                    return null;
                }
            }
            else
            {
                return new CompoundShape(this);
            }
        }
        /// <summary>
        /// Shrinks this simple shape by the given amount. note that the result is a <see cref="CompoundShape"/>
        /// which is composed of multiple simple shapes.
        /// </summary>
        /// <param name="d">Amount to shrink, if negative <see cref="Expand"/> will be called</param>
        /// <returns>The resulting shape (may be empty)</returns>
        public CompoundShape Shrink(double d)
        {
            if (d < 0.0) return Expand(-d);
            Border[] oo = outline.GetParallel(-d, false, 0.0, 0.0); // Schrumpfen
            if (oo.Length == 0)
            {   // manchmal gibt es kein Ergebnis wenn der Zusammenhang fast tangential ist. Dann hilft
                // es aber zweimal mit dem halben Abstand die Äquidistante zu berechnen
                // vermutlich, weil das Ergebnis jedesmal wieder eingerenkt wird
                // Elegant ist aber was anderes!
                Border[] ooo = outline.GetParallel(-d / 2.0, false, 0.0, 0.0); // Schrumpfen
                List<Border> loo = new List<Border>();
                for (int i = 0; i < ooo.Length; i++)
                {
                    loo.AddRange(ooo[i].GetParallel(-d / 2.0, false, 0.0, 0.0));
                }
                oo = loo.ToArray();
            }
            // versuchsweise mit der neuen Methode
            // Border[] oo = outline.GetParallel(-d); // Schrumpfen
            // bei mehreren Borders, die durch Verkleinern einer einfachen Umrandung entstehen,
            // können nur mehrere disjunkte Umrandungen entstehen. Löcher können nicht entstehen
            CompoundShape res = new CompoundShape();
            res.UniteDisjunct(oo); // Ausgang: die geschrumpfte Außenkontur
            // davon abziehen: die aufgeblasenen Löcher
            for (int i = 0; i < holes.Length; ++i)
            {
                oo = holes[i].GetParallel(d, false, 0.0, 0.0); // Aufblasen
                // versuchsweise mit der neuen Methode
                // oo = holes[i].GetParallel(d); // Aufblasen
                // die größte ist Außenkontur, der Rest sind Löcher
                if (oo.Length > 1)
                {
                    int outind = 0;
                    double maxarea = oo[0].Area;
                    for (int j = 1; j < oo.Length; ++j)
                    {
                        if (oo[j].Area > maxarea)
                        {
                            maxarea = oo[j].Area;
                            outind = j;
                        }
                    }
                    Border[] hh = new Border[oo.Length - 1];
                    int jj = 0;
                    for (int j = 0; j < oo.Length; ++j)
                    {
                        if (j != outind)
                        {
                            hh[jj] = oo[j];
                            ++jj;
                        }
                    }
                    SimpleShape ss = new SimpleShape(oo[outind], hh);
                    res = res - new CompoundShape(ss);
                }
                else if (oo.Length > 0)
                {
                    res = res - new CompoundShape(new SimpleShape(oo[0]));
                }
            }
            return res;
        }
        /// <summary>
        /// Expands this simple shape by the given amount. note that the result is a <see cref="CompoundShape"/>
        /// which is composed of multiple simple shapes.
        /// </summary>
        /// <param name="d">Amount to expand, if negative <see cref="Shrink"/> will be called</param>
        /// <returns>The resulting shape</returns>
        public CompoundShape Expand(double d)
        {
            if (d < 0.0) return Shrink(-d);
            // versuchsweise mit der neuen Methode
            Border[] oo = outline.GetParallel(d, false, 0.0, 0.0); // Erweitern
            // Border[] oo = outline.GetParallel(d); // Erweitern
            CompoundShape res = new CompoundShape();
            if (oo.Length > 1)
            {   // suche das größte Border Objekt
                int ind = -1;
                double max = double.MinValue;
                for (int i = 0; i < oo.Length; ++i)
                {
                    double a = oo[i].Area;
                    if (a > max)
                    {
                        max = a;
                        ind = i;
                    }
                }
                res.UniteDisjunct(oo[ind]);
                for (int i = 0; i < oo.Length; ++i)
                {
                    if (i != ind)
                    {
                        res.Subtract(new SimpleShape(oo[i]));
                    }
                }
            }
            else if (oo.Length == 1)
            {
                res.UniteDisjunct(oo[0]);
            }
            for (int i = 0; i < holes.Length; ++i)
            {
                // versuchsweise mit der neuen Methode
                oo = holes[i].GetParallel(-d, false, 0.0, 0.0);
                // oo = holes[i].GetParallel(-d);
                for (int j = 0; j < oo.Length; ++j)
                {
                    res.Subtract(new SimpleShape(oo[j]));
                }
            }

            return res;
        }
        /// <summary>
        /// Returns the intersection (common parts, overlapping area) of two simple shapes. 
        /// </summary>
        /// <param name="Part1">First shape</param>
        /// <param name="Part2">Second shape</param>
        /// <returns>The resulting shape (may be empty)</returns>
        public static CompoundShape Intersect(SimpleShape Part1, SimpleShape Part2)
        {
            return Intersect(Part1, Part2, Precision.eps);
        }
        public static CompoundShape Intersect(SimpleShape Part1, SimpleShape Part2, double precision)
        {
            // SimpleShapes sollte nie leer sein. Falls doch, sollten sie sich
            // erst garnicht in einem CompoundShape befinden
            CompoundShape res;
            BorderOperation bo = new BorderOperation(Part1.outline, Part2.outline, precision);
            switch (bo.Position)
            {
                case BorderOperation.BorderPosition.disjunct:
                    return new CompoundShape(); // leer
                case BorderOperation.BorderPosition.b1coversb2:
                    // die Part2.Outline liegt komplett innerhalb von Part1.Outline
                    // das Ergebnis ist also Part2 minus die Löcher von Part1
                    res = new CompoundShape(Part2);
                    for (int i = 0; i < Part1.holes.Length; ++i)
                    {
                        res = CompoundShape.Difference(res, new CompoundShape(new SimpleShape(Part1.holes[i])), precision);
                    }
                    return res;
                case BorderOperation.BorderPosition.identical:
                case BorderOperation.BorderPosition.b2coversb1:
                    // Part2 umschließt Part1:
                    // das Ergebnis ist also Part1 minus die Löcher von Part2
                    res = new CompoundShape(Part1);
                    for (int i = 0; i < Part2.holes.Length; ++i)
                    {
                        res = CompoundShape.Difference(res, new CompoundShape(new SimpleShape(Part2.holes[i])), precision);
                    }
                    return res;
                case BorderOperation.BorderPosition.intersecting:
                    res = bo.Intersection(); // Vorsicht, noch nicht implementiert!
                    for (int i = 0; i < Part1.holes.Length; ++i)
                    {
                        res = CompoundShape.Difference(res, new CompoundShape(new SimpleShape(Part1.holes[i])), precision);
                    }
                    for (int i = 0; i < Part2.holes.Length; ++i)
                    {
                        res = CompoundShape.Difference(res, new CompoundShape(new SimpleShape(Part2.holes[i])), precision);
                    }
                    return res;
            }
            return new CompoundShape(); // leer
        }
#if DEBUG
        public static CompoundShape IntersectX(SimpleShape Part1, SimpleShape Part2, double precision)
        {
            // SimpleShapes sollte nie leer sein. Falls doch, sollten sie sich
            // erst garnicht in einem CompoundShape befinden
            CompoundShape res;
            BorderQuadTree bo = new BorderQuadTree(Part1.outline, Part2.outline, precision);
            switch (bo.Position)
            {
                case BorderQuadTree.BorderPosition.disjunct:
                    return new CompoundShape(); // leer
                case BorderQuadTree.BorderPosition.b1coversb2:
                    // die Part2.Outline liegt komplett innerhalb von Part1.Outline
                    // das Ergebnis ist also Part2 minus die Löcher von Part1
                    res = new CompoundShape(Part2);
                    for (int i = 0; i < Part1.holes.Length; ++i)
                    {
                        res = res - new CompoundShape(new SimpleShape(Part1.holes[i]));
                    }
                    return res;
                case BorderQuadTree.BorderPosition.identical:
                case BorderQuadTree.BorderPosition.b2coversb1:
                    // Part2 umschließt Part1:
                    // das Ergebnis ist also Part1 minus die Löcher von Part2
                    res = new CompoundShape(Part1);
                    for (int i = 0; i < Part2.holes.Length; ++i)
                    {
                        res = res - new CompoundShape(new SimpleShape(Part2.holes[i]));
                    }
                    return res;
                case BorderQuadTree.BorderPosition.intersecting:
                    res = bo.Intersection();
                    for (int i = 0; i < Part1.holes.Length; ++i)
                    {
                        // res = res - new CompoundShape(new SimpleShape(Part1.holes[i]));
                        res = CompoundShape.DifferenceX(res, new CompoundShape(new SimpleShape(Part1.holes[i])), precision);
                    }
                    for (int i = 0; i < Part2.holes.Length; ++i)
                    {
                        // res = res - new CompoundShape(new SimpleShape(Part2.holes[i]));
                        res = CompoundShape.DifferenceX(res, new CompoundShape(new SimpleShape(Part2.holes[i])), precision);
                    }
                    return res;
            }
            return new CompoundShape(); // leer
        }
#endif
        /// <summary>
        /// Returns the union of two simple shapes. 
        /// </summary>
        /// <param name="Part1">First shape</param>
        /// <param name="Part2">Second shape</param>
        /// <returns>The resulting shape (may be empty)</returns>
        public static CompoundShape Unite(SimpleShape Part1, SimpleShape Part2)
        {
            return Unite(Part1, Part2, Precision.eps);
        }
        internal static CompoundShape Unite(SimpleShape Part1, SimpleShape Part2, double precision)
        {
            return Unite(Part1, Part2, precision, null);
        }
        internal static CompoundShape Unite(SimpleShape Part1, SimpleShape Part2, double precision, Dictionary<BorderPair, BorderOperation> borderOperationCache)
        {
            CompoundShape res = null;
            BorderOperation bo = BorderPair.GetBorderOperation(borderOperationCache, Part1.outline, Part2.outline, precision);
            switch (bo.Position)
            {
                case BorderOperation.BorderPosition.disjunct:
                    // disjunkt: einfach beide SimpleShapes nehmen
                    return new CompoundShape(Part1, Part2);
                case BorderOperation.BorderPosition.b1coversb2:
                    res = new CompoundShape(new SimpleShape(Part1.outline));
                    // die Löcher kommen weiter unten noch
                    break;
                case BorderOperation.BorderPosition.b2coversb1:
                    res = new CompoundShape(new SimpleShape(Part2.outline));
                    // die Löcher kommen weiter unten noch
                    break;
                case BorderOperation.BorderPosition.intersecting:
                    res = bo.Union();
                    break;
                case BorderOperation.BorderPosition.identical:
                    res = new CompoundShape(new SimpleShape(Part1.outline));
                    // die Löcher kommen weiter unten noch
                    break;
                case BorderOperation.BorderPosition.unknown:
                    throw new BorderException("unexpected error in CompoundShape.Unite!", BorderException.BorderExceptionType.InternalError);
            }
            // die einfache Verrechnung der Ränder ist erledigt, die Löcher
            // die nur ein der einen Fläche, nicht aber in der anderen liegen
            // werden jetzt abgezogen
            for (int i = 0; i < Part1.holes.Length; ++i)
            {
                SimpleShape Hole = new SimpleShape(Part1.holes[i]);
                CompoundShape RealHole = SimpleShape.Subtract(Hole, Part2, borderOperationCache, precision);
                res = CompoundShape.Difference(res, RealHole, precision);
            }
            for (int i = 0; i < Part2.holes.Length; ++i)
            {
                SimpleShape Hole = new SimpleShape(Part2.holes[i]);
                CompoundShape RealHole = SimpleShape.Subtract(Hole, Part1, borderOperationCache, precision);
                res = CompoundShape.Difference(res, RealHole, precision);
            }
            return res;
        }
#if DEBUG
        internal static CompoundShape UniteX(SimpleShape Part1, SimpleShape Part2, double precision)
        {
            CompoundShape res = null;
            BorderQuadTree bq = new BorderQuadTree(Part1.outline, Part2.outline, precision);
            switch (bq.Position)
            {
                case BorderQuadTree.BorderPosition.disjunct:
                    // disjunkt: einfach beide SimpleShapes nehmen
                    return new CompoundShape(Part1, Part2);
                case BorderQuadTree.BorderPosition.b1coversb2:
                    res = new CompoundShape(new SimpleShape(Part1.outline));
                    // die Löcher kommen weiter unten noch
                    break;
                case BorderQuadTree.BorderPosition.b2coversb1:
                    res = new CompoundShape(new SimpleShape(Part2.outline));
                    // die Löcher kommen weiter unten noch
                    break;
                case BorderQuadTree.BorderPosition.intersecting:
                    res = bq.Union();
                    break;
                case BorderQuadTree.BorderPosition.identical:
                    res = new CompoundShape(new SimpleShape(Part1.outline));
                    // die Löcher kommen weiter unten noch
                    break;
                case BorderQuadTree.BorderPosition.unknown:
                    throw new BorderException("unexpected error in CompoundShape.Unite!", BorderException.BorderExceptionType.InternalError);
            }
            // die einfache Verrechnung der Ränder ist erledigt, die Löcher
            // die nur ein der einen Fläche, nicht aber in der anderen liegen
            // werden jetzt abgezogen
            for (int i = 0; i < Part1.holes.Length; ++i)
            {
                SimpleShape Hole = new SimpleShape(Part1.holes[i]);
                CompoundShape RealHole = SimpleShape.SubtractX(Hole, Part2, precision);
                res = CompoundShape.DifferenceX(res, RealHole, precision);
            }
            for (int i = 0; i < Part2.holes.Length; ++i)
            {
                SimpleShape Hole = new SimpleShape(Part2.holes[i]);
                CompoundShape RealHole = SimpleShape.SubtractX(Hole, Part1, precision);
                res = CompoundShape.DifferenceX(res, RealHole, precision);
            }
            return res;
        }
#endif
        /// <summary>
        /// Returns the subtraction of <paramref name="Part1"/> minus <paramref name="Part2"/>, i.e. all parts that belont to Part1
        /// but not to Part2.
        /// </summary>
        /// <param name="Part1">Shape to be subtracted from</param>
        /// <param name="Part2">Shape that is subtracted</param>
        /// <returns>The resulting shape (may be empty)</returns>
        public static CompoundShape Subtract(SimpleShape Part1, SimpleShape Part2)
        {
            return Subtract(Part1, Part2, null, 0.0);
        }
        internal static CompoundShape Subtract(SimpleShape Part1, SimpleShape Part2, double precision)
        {
            return Subtract(Part1, Part2, null, precision);
        }
        internal static CompoundShape Subtract(SimpleShape Part1, SimpleShape Part2, Dictionary<BorderPair, BorderOperation> borderOperationCache, double precision)
        {
            // SimpleShapes sollte nie leer sein. Falls doch, sollten sie sich
            // erst garnicht in einem CompoundShape befinden
            CompoundShape res;
            BorderOperation bo = BorderPair.GetBorderOperation(borderOperationCache, Part1.outline, Part2.outline, precision);
            switch (bo.Position)
            {
                case BorderOperation.BorderPosition.disjunct:
                    return new CompoundShape(Part1.Clone());
                case BorderOperation.BorderPosition.b1coversb2:
                    // die Part2.Outline liegt komplett innerhalb von Part1.Outline
                    // also ss mit diesen Löchern vereinigen. Ergibt die neuen Löcher und ggf.
                    // neue disjunkte Outlines. Von den Löchern von ss bleibten nun noch
                    // die eigenen Löcher abzuziehen und das Ergebnis als neue disjunkte SimpleShapes
                    // zuzufügen
                    CompoundShape holes = new CompoundShape();
                    holes.UniteDisjunct(Part1.holes);
                    holes = CompoundShape.Union(holes, new CompoundShape(new SimpleShape(Part2.outline)), precision);
                    // first wird das 1. SimpleShape, bestehend aus Part1 outline und
                    // den outlines aller SimpleShapes in holes
                    Border[] firstholes = new Border[holes.SimpleShapes.Length];
                    for (int i = 0; i < firstholes.Length; ++i)
                    {
                        firstholes[i] = holes.SimpleShapes[i].outline;
                    }
                    SimpleShape first = new SimpleShape(Part1.outline, firstholes);

                    // noch nicht berücksichtigt sind die beim Vereinigen der Löcher entanden "Lochlöcher"
                    // und die Löcher von Part2.
                    // Die Lochlöcher liegen mit Sicherheit außerhalb von Part2 und der Löcher von Part1, können
                    // also bedenkenlos als disjunkte Shapes zugefügt werden
                    res = new CompoundShape(first);
                    for (int i = 0; i < holes.SimpleShapes.Length; ++i)
                    {
                        for (int j = 0; j < holes.SimpleShapes[i].holes.Length; ++j)
                        {
                            CompoundShape next = SimpleShape.Intersect(Part1, new SimpleShape(holes.SimpleShapes[i].holes[j]), precision); // hier noch borderOperationCache
                            res.UniteDisjunct(next);
                        }
                    }
                    // dort wo die Löcher von Part2 auf Substanz von Part 1 treffen, werden diese Teile noch hinzugefügt
                    for (int i = 0; i < Part2.holes.Length; ++i)
                    {
                        CompoundShape next = SimpleShape.Intersect(Part1, new SimpleShape(Part2.holes[i]), precision);
                        res.UniteDisjunct(next);
                    }
                    return res;
                case BorderOperation.BorderPosition.identical:
                case BorderOperation.BorderPosition.b2coversb1:
                    // Part2 umschließt Part1:
                    // Das Ergebnis sind die Löcher von Part2 geschnitten mit Part1
                    res = new CompoundShape();
                    if (Part2.NumHoles > 0)
                    {
                        res.UniteDisjunct(Part2.holes);
                        res = CompoundShape.Intersection(res, new CompoundShape(Part1), precision);
                    }
                    return res;
                case BorderOperation.BorderPosition.intersecting:
                    res = bo.Difference();
                    for (int i = 0; i < Part2.NumHoles; ++i)
                    {   // die Löcher von Part2 mit Part1 schneiden und zufügen
                        CompoundShape toAdd = SimpleShape.Intersect(Part1, new SimpleShape(Part2.holes[i]), precision);
                        res.UniteDisjunct(toAdd); // wenn toAdd nicht leer ist, ist es garantiert disjunkt
                    }
                    for (int i = 0; i < Part1.NumHoles; ++i)
                    {   // die Löcher von Part1 abziehen
                        res = res - new CompoundShape(new SimpleShape(Part1.holes[i]));
                        res = CompoundShape.Difference(res, new CompoundShape(new SimpleShape(Part1.holes[i])), precision);
                    }
                    return res;
            }
            return new CompoundShape(); // leer
        }
#if DEBUG
        internal static CompoundShape SubtractX(SimpleShape Part1, SimpleShape Part2, double precision)
        {
            // SimpleShapes sollte nie leer sein. Falls doch, sollten sie sich
            // erst garnicht in einem CompoundShape befinden
            CompoundShape res;
            BorderQuadTree bo = new BorderQuadTree(Part1.outline, Part2.outline, precision);
            switch (bo.Position)
            {
                case BorderQuadTree.BorderPosition.disjunct:
                    return new CompoundShape(Part1.Clone());
                case BorderQuadTree.BorderPosition.b1coversb2:
                    // die Part2.Outline liegt komplett innerhalb von Part1.Outline
                    // also ss mit diesen Löchern vereinigen. Ergibt die neuen Löcher und ggf.
                    // neue disjunkte Outlines. Von den Löchern von ss bleibten nun noch
                    // die eigenen Löcher abzuziehen und das Ergebnis als neue disjunkte SimpleShapes
                    // zuzufügen
                    CompoundShape holes = new CompoundShape();
                    holes.UniteDisjunct(Part1.holes);
                    // holes = holes + new CompoundShape(new SimpleShape(Part2.outline));
                    holes = CompoundShape.UnionX(holes, new CompoundShape(new SimpleShape(Part2.outline)), precision);
                    // first wird das 1. SimpleShape, bestehend aus Part1 outline und
                    // den outlines aller SimpleShapes in holes
                    Border[] firstholes = new Border[holes.SimpleShapes.Length];
                    for (int i = 0; i < firstholes.Length; ++i)
                    {
                        firstholes[i] = holes.SimpleShapes[i].outline;
                    }
                    SimpleShape first = new SimpleShape(Part1.outline, firstholes);

                    // noch nicht berücksichtigt sind die beim Vereinigen der Löcher entanden "Lochlöcher"
                    // und die Löcher von Part2.
                    // Die Lochlöcher liegen mit Sicherheit außerhalb von Part2 und der Löcher von Part1, können
                    // also bedenkenlos als disjunkte Shapes zugefügt werden
                    res = new CompoundShape(first);
                    for (int i = 0; i < holes.SimpleShapes.Length; ++i)
                    {
                        for (int j = 0; j < holes.SimpleShapes[i].holes.Length; ++j)
                        {
                            CompoundShape next = SimpleShape.Intersect(Part1, new SimpleShape(holes.SimpleShapes[i].holes[j]), precision); // hier noch borderOperationCache
                            res.UniteDisjunct(next);
                        }
                    }
                    // dort wo die Löcher von Part2 auf Substanz von Part 1 treffen, werden diese Teile noch hinzugefügt
                    for (int i = 0; i < Part2.holes.Length; ++i)
                    {
                        CompoundShape next = SimpleShape.Intersect(Part1, new SimpleShape(Part2.holes[i]), precision);
                        res.UniteDisjunct(next);
                    }
                    return res;
                case BorderQuadTree.BorderPosition.identical:
                case BorderQuadTree.BorderPosition.b2coversb1:
                    // Part2 umschließt Part1:
                    // Das Ergebnis sind die Löcher von Part2 geschnitten mit Part1
                    res = new CompoundShape();
                    if (Part2.NumHoles > 0)
                    {
                        res.UniteDisjunct(Part2.holes);
                        res = CompoundShape.Intersection(res, new CompoundShape(Part1), precision);
                    }
                    return res;
                case BorderQuadTree.BorderPosition.intersecting:
                    res = bo.Difference();
                    for (int i = 0; i < Part2.NumHoles; ++i)
                    {   // die Löcher von Part2 mit Part1 schneiden und zufügen
                        CompoundShape toAdd = SimpleShape.Intersect(Part1, new SimpleShape(Part2.holes[i]), precision);
                        res.UniteDisjunct(toAdd); // wenn toAdd nicht leer ist, ist es garantiert disjunkt
                    }
                    for (int i = 0; i < Part1.NumHoles; ++i)
                    {   // die Löcher von Part1 abziehen
                        res = res - new CompoundShape(new SimpleShape(Part1.holes[i]));
                    }
                    return res;
            }
            return new CompoundShape(); // leer
        }
#endif
        /// <summary>
        /// Relative position of two shapes
        /// </summary>
        public enum Position
        {
            /// <summary>
            /// Disjunct (non overlapping)
            /// </summary>
            disjunct,
            /// <summary>
            /// Intersecting, overlapping, but not including
            /// </summary>
            intersecting,
            /// <summary>
            /// First shape totally covers second shape
            /// </summary>
            firstcontainscecond,
            /// <summary>
            /// Second shape totally covers first shape
            /// </summary>
            secondcontainsfirst,
            /// <summary>
            /// The two shapes are identical
            /// </summary>
            identical
        }
        /// <summary>
        /// Checks the relative position of two shapes to each other. The order of the parameters is important for the result.
        /// </summary>
        /// <param name="cs1">First shape</param>
        /// <param name="cs2">Second shape</param>
        /// <returns>The position</returns>
        public static Position GetPosition(CompoundShape cs1, CompoundShape cs2)
        {   // this is not correct, but we don't need it to be correct
            Position res = Position.disjunct;
            for (int i = 0; i < cs1.SimpleShapes.Length; i++)
            {
                for (int j = 0; j < cs2.SimpleShapes.Length; j++)
                {
                    Position pos = GetPosition(cs1.SimpleShapes[i], cs2.SimpleShapes[j]);
                    if (pos > res) res = pos;
                }
            }
            return res;
        }
        /// <summary>
        /// Checks the relative position of two shapes to each other. The order of the parameters is important for the result.
        /// </summary>
        /// <param name="s1">First shape</param>
        /// <param name="s2">Second shape</param>
        /// <returns>The position</returns>
        public static Position GetPosition(SimpleShape s1, SimpleShape s2)
        {
            return GetPosition(s1, s2, null, 0.0);
        }
        internal static Position GetPosition(SimpleShape s1, SimpleShape s2, Dictionary<BorderPair, BorderOperation> borderOperationCache, double precision)
        {
            BorderOperation bo = BorderPair.GetBorderOperation(borderOperationCache, s1.outline, s2.outline, precision);

            switch (bo.Position)
            {
                case BorderOperation.BorderPosition.disjunct:
                    return Position.disjunct;
                case BorderOperation.BorderPosition.intersecting:
                    return Position.intersecting;
                case BorderOperation.BorderPosition.identical:
                    if (s1.NumHoles == 0 && s2.NumHoles == 0) return Position.identical;
                    if (s1.NumHoles == 0) return Position.firstcontainscecond;
                    if (s2.NumHoles == 0) return Position.secondcontainsfirst;
                    // beide haben Löcher
                    bool b1holesoutside = true;
                    for (int i = 0; i < s1.NumHoles; i++)
                    {
                        Position ppp = GetPosition(new SimpleShape(s1.Holes[i]), s2);
                        if (ppp != Position.disjunct)
                        {
                            b1holesoutside = false;
                            break;
                        }
                    }
                    bool b2holesoutside = true;
                    for (int i = 0; i < s2.NumHoles; i++)
                    {
                        Position ppp = GetPosition(new SimpleShape(s2.Holes[i]), s1);
                        if (ppp != Position.disjunct)
                        {
                            b2holesoutside = false;
                            break;
                        }
                    }
                    if (b1holesoutside && b2holesoutside) return Position.identical; // alle Löcher des einen sind außerhalb der Form des anderen (u. umgekehrt)
                    if (!b1holesoutside && b2holesoutside) return Position.secondcontainsfirst;
                    if (b1holesoutside && !b2holesoutside) return Position.firstcontainscecond;
                    bool holesIdentical = true;
                    for (int i = 0; i < s1.NumHoles; i++)
                    {
                        bool identicalHoleFound = false;
                        for (int j = 0; j < s2.NumHoles; j++)
                        {
                            BorderOperation boh = BorderPair.GetBorderOperation(borderOperationCache, s1.holes[i], s2.holes[j], precision);
                            if (boh.Position == BorderOperation.BorderPosition.identical)
                            {
                                identicalHoleFound = true;
                                break;
                            }
                        }
                        if (!identicalHoleFound)
                        {
                            holesIdentical = false;
                            break;
                        }
                    }
                    if (holesIdentical) return Position.identical;
                    return Position.intersecting; // beide haben Löcher im "Fleisch" des anderen

                case BorderOperation.BorderPosition.b1coversb2:
                    // zwei Möglichkeiten: firstcontainscecond oder intersecting
                    // wenn all Löcher von s1 außerhalb von s2 sind dann firstcontainscecond 
                    // sonst intersecting
                    for (int i = 0; i < s1.holes.Length; ++i)
                    {

                        BorderOperation bo1 = BorderPair.GetBorderOperation(borderOperationCache, s1.holes[i], s2.outline, 0.0);
                        switch (bo1.Position)
                        {
                            case BorderOperation.BorderPosition.identical: // das Loch ist identisch mit s2
                            case BorderOperation.BorderPosition.b1coversb2:
                                return Position.disjunct; // s2 liegt ganz in einem Loch
                            case BorderOperation.BorderPosition.intersecting:
                                return Position.intersecting;
                            case BorderOperation.BorderPosition.disjunct:
                                continue; // kein Treffer
                            case BorderOperation.BorderPosition.b2coversb1:
                                // das Loch liegt ganz in s2:
                                // vermutlich intersecting, es sei denn, das Loch liegt
                                // wiederum ganz in einem Loch von s2
                                bool inside = false;
                                for (int j = 0; j < s2.holes.Length; ++j)
                                {
                                    BorderOperation bo2 = BorderPair.GetBorderOperation(borderOperationCache, s1.holes[i], s2.holes[j], 0.0);
                                    if (bo2.Position == BorderOperation.BorderPosition.b2coversb1)
                                    {
                                        inside = true;
                                        break;
                                    }
                                }
                                if (!inside) return Position.intersecting;
                                break;
                            case BorderOperation.BorderPosition.unknown:
                                throw new BorderException("internal error in SimpleShape.GetPosition", BorderException.BorderExceptionType.InternalError);
                        }
                    }
                    return Position.firstcontainscecond;
                case BorderOperation.BorderPosition.b2coversb1:
                    Position pp = GetPosition(s2, s1); // um nicht obigen Fall mit umgekehrten Rollen nochmal zu schreiben
                    if (pp == Position.firstcontainscecond) return Position.secondcontainsfirst;
                    else if (pp == Position.secondcontainsfirst) return Position.firstcontainscecond;
                    else return pp;
                case BorderOperation.BorderPosition.unknown:
                    return Position.disjunct;
                    throw new BorderException("internal error in SimpleShape.GetPosition", BorderException.BorderExceptionType.InternalError);
            }
            // Alle Fälle abgedeckt, hierhin gehts nicht
            return Position.disjunct;
        }
        public static SimpleShape MakeCircle(GeoPoint2D center, double radius)
        {
            return new SimpleShape(Border.MakeCircle(center, radius));
        }
        public static SimpleShape MakePolygon(GeoPoint2D center, double radius, int numberOfSides)
        {
            return new SimpleShape(Border.MakePolygon(center, radius, numberOfSides));
        }
        public static SimpleShape MakeLongHole(ICurve2D centerLine, double halfWidth, double precision)
        {
            if (!(centerLine is Path2D)) centerLine = new Path2D(new ICurve2D[] { centerLine });
            Path2D path = centerLine.Approximate(false, precision) as Path2D;
            // now path is a Path2D and only contains lines and arcs (2d, connected)
            // for each segment in this path we create a "longhole", which is in case of a line two parallel lines and two half circles at both ends,
            // in case of an arc there are more alternatives (see code below)
            // All these longholes are united, but since the segments are connected it always remains a SimpleShape (no CompundShape)
            SimpleShape result = null;
            for (int i = 0; i < path.SubCurves.Length; i++)
            {
                SimpleShape subShape = null; // subCurve[i] makes this long hole
                if (path.SubCurves[i] is Line2D)
                {
                    Border bdr = Border.MakeLongHole(path.SubCurves[i], halfWidth);
                    subShape = new SimpleShape(bdr);
                }
                if (path.SubCurves[i] is Arc2D)
                {
                    Arc2D a2d = (path.SubCurves[i] as Arc2D);
                    if (a2d.Radius <= halfWidth)
                    {   // Border.MakeLongHole cannot handle this
                        if (a2d.Sweep < 0) a2d = a2d.CloneReverse(true) as Arc2D;
                        Arc2D a1 = new Arc2D(a2d.EndPoint, halfWidth, new Angle(a2d.EndDirection) - Angle.A90, SweepAngle.Opposite);
                        Arc2D a2 = new Arc2D(a2d.StartPoint, halfWidth, new Angle(a2d.StartDirection) + Angle.A90, SweepAngle.Opposite);
                        Arc2D am = a2d.Clone() as Arc2D;
                        am.Radius = a2d.Radius + halfWidth;
                        GeoPoint2DWithParameter[] ips = a1.Intersect(a2); // probably 2 intersection points
                        int ind = -1;
                        for (int j = 0; j < ips.Length; j++)
                        {
                            if (ips[j].par1 >= 0 && ips[j].par1 <= 1 && ips[j].par2 >= 0 && ips[j].par2 <= 1) ind = j;
                        }
                        if (ind >= 0)
                        {
                            a1 = a1.Trim(0, ips[ind].par1) as Arc2D;
                            a2 = a2.Trim(ips[ind].par2, 1) as Arc2D;
                            Border bdr = new Border(new ICurve2D[] { a2, am, a1 });
                            subShape = new SimpleShape(bdr);
                        }
                    }
                    else
                    {
                        if (Math.Abs(a2d.SweepAngle) > Math.PI)
                        {   // it is possible that the resulting subShape has a hole
                            ICurve2D[] parts = a2d.Split(0.5); // there must be two parts
                            Border bdr1 = Border.MakeLongHole(parts[0], halfWidth);
                            Border bdr2 = Border.MakeLongHole(parts[1], halfWidth);
                            subShape = Unite(new SimpleShape(bdr1), new SimpleShape(bdr2)).SimpleShapes[0];
                        }
                        else
                        {
                            subShape = new SimpleShape(Border.MakeLongHole(a2d, halfWidth));
                        }
                    }
                }
                if (result == null) result = subShape;
                else if (subShape != null) result = Unite(result, subShape).SimpleShapes[0]; // the resulting CompoundShape must be connected, i.e. only one SimpleShape
            }
            return result;
        }
        /// <summary>
        /// The 2d-simple shape is assumed to reside in plane "fromPlane". It will be projected
        /// perpendicular onto the plane "toPlane". If the planes are perpendicular, the result
        /// will be am empty shape.
        /// </summary>
        /// <param name="fromPlane">the containing plane</param>
        /// <param name="toPlane">the projection plane</param>
        /// <returns>the projected shape</returns>
        public SimpleShape Project(Plane fromPlane, Plane toPlane)
        {	// die Topologie bleibt erhalten, fromPlane und toPlane dürfen nicht senkrecht sein
            Border o = outline.Project(fromPlane, toPlane);
            Border[] h = new Border[holes.Length];
            for (int i = 0; i < holes.Length; ++i)
            {
                h[i] = holes[i].Project(fromPlane, toPlane);
            }
            return new SimpleShape(o, h);
        }
        /// <summary>
        /// Returns a modified shape of this shape. This shape remains unchanged.
        /// </summary>
        /// <param name="m">Modification by which this shape is modified</param>
        /// <returns></returns>
        public SimpleShape GetModified(ModOp2D m)
        {
            Border o = outline.GetModified(m);
            Border[] h = new Border[holes.Length];
            for (int i = 0; i < holes.Length; ++i)
            {
                h[i] = holes[i].GetModified(m);
            }
            return new SimpleShape(o, h);
        }
        internal void AddToGraphicsPath(System.Drawing.Drawing2D.GraphicsPath path)
        {
            outline.AddToGraphicsPath(path, true);
            for (int i = 0; i < holes.Length; ++i)
            {
                holes[i].AddToGraphicsPath(path, false);
            }
        }
        internal void Flatten()
        {
            outline.flatten();
            for (int i = 0; i < holes.Length; ++i)
            {
                holes[i].flatten();
            }
        }
        /// <summary>
        /// Returns the extent of this shape, i.e. the size of the horizontally adjusted rectangle that encloses it.
        /// </summary>
        /// <returns></returns>
        public BoundingRect GetExtent()
        {
            return outline.Extent; // holes muss ja nicht berücksichtigt werden
        }
        /// <summary>
        /// Checks whether this shape and the provided rectangle overlap
        /// </summary>
        /// <param name="Rect"></param>
        /// <returns></returns>
        public bool HitTest(ref BoundingRect Rect)
        {
            BoundingRect ext = outline.Extent;
            if (BoundingRect.Disjoint(ext, Rect)) return false; //  grober Vorabcheck
            Border.Position pos = outline.GetPosition(Rect);
            switch (pos)
            {
                case Border.Position.Inside: // die Löcher untersuchen
                    for (int i = 0; i < holes.Length; ++i)
                    {
                        if (holes[i].GetPosition(Rect) == Border.Position.Inside) return false;
                    }
                    return true;
                case Border.Position.Outside: return false;
                case Border.Position.OnCurve: return true; // egal was die Löcher sagen
            }
            return false; // damit der Compiler zufrieden ist
        }
        internal double Distance(GeoPoint2D p)
        {
            double res = outline.GetMinDistance(p);
            for (int i = 0; i < holes.Length; i++)
            {
                double d = holes[i].GetMinDistance(p);
                if (d < res) res = d;
            }
            return res;
        }
        public GeoObjectList DebugList
        {
            get
            {
                GeoObjectList res = new GeoObjectList(outline.DebugList);
                for (int i = 0; i < holes.Length; ++i)
                {
                    res.AddRange(holes[i].DebugList);
                }
                return res;
            }
        }
        /// <summary>
        /// Clips the provided curve by this shape. Either the inner parts or the parts outside of this shape are returned.
        /// </summary>
        /// <param name="toClip">Curve to be clipped</param>
        /// <param name="returnInsideParts">true: return inside parts, false: return outside parts</param>
        /// <returns>the clipped curves</returns>
        public double[] Clip(ICurve2D toClip, bool returnInsideParts)
        {
            // Hier ist noch etwas unklar: "outline.Clip" muss [0.0,1.0] liefern, wenn die Kurve ganz
            // innerhalb liegt, wenn es nichts liefert, dann ist es außerhalb
            List<double> res = new List<double>(outline.Clip(toClip, returnInsideParts));
            // hier gehen wir davon aus, dass die Löcher sich mit dem Rand nicht überschneiden
            // und auch die Löcher selbst sich nicht überschneiden
            // die inneren Teile der Löcher sind also die Lücken und sortieren sich genauso ein
            for (int j = 0; j < holes.Length; ++j)
            {
                double[] cc = holes[j].Clip(toClip, returnInsideParts);
                if (res.Count == 0 && cc.Length > 0)
                {   // das ist unlogisch, und muss noch überprüft werden
                    res.Add(0.0);
                    res.Add(1.0); // damit es sich bei den Löchern richtig verhält
                }
                res.AddRange(cc);
            }
            res.Sort();
            for (int i = res.Count - 1; i > 0; --i)
            {
                if (res[i] == res[i - 1])
                {
                    res.RemoveAt(i);
                    res.RemoveAt(i - 1);
                    --i;
                }
            }
            return res.ToArray();
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected SimpleShape(SerializationInfo info, StreamingContext context)
        {
            outline = (Border)info.GetValue("Outline", typeof(Border));
            holes = (Border[])info.GetValue("Holes", typeof(Border[]));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Outline", outline);
            info.AddValue("Holes", holes);
        }

        #endregion
        internal void Reduce()
        {
            outline.Reduce();
            for (int j = 0; j < holes.Length; ++j)
            {
                holes[j].Reduce();
            }
        }
        public void Reduce(double precision)
        {
            if (precision < 0)
            {
                Reduce2D r2d = new Reduce2D();
                r2d.Add(outline.Segments);
                r2d.Precision = -precision;
                r2d.OutputMode = Reduce2D.Mode.Simple;
                ICurve2D[] cvs = r2d.Reduced;
            }
            else
            {
                outline.Reduce(precision);
                outline.ReduceDeadEnd(precision); // kann leer werden!

                bool shrink = false;
                for (int j = 0; j < holes.Length; ++j)
                {
                    if (holes[j].Area < precision)
                    {
                        holes[j] = null;
                        shrink = true;
                    }
                    else
                    {
                        holes[j].Reduce(precision);
                        holes[j].ReduceDeadEnd(precision);
                    }
                }
                if (shrink)
                {
                    List<Border> lholes = new List<Border>();
                    for (int j = 0; j < holes.Length; ++j)
                    {
                        if (holes[j] != null && holes[j].Area > precision) lholes.Add(holes[j]);
                    }
                    holes = lholes.ToArray();
                }
            }
        }
        internal void Approximate(bool linesOnly, double precision)
        {
            outline.Approximate(linesOnly, precision);
            for (int j = 0; j < holes.Length; ++j)
            {
                holes[j].Approximate(linesOnly, precision);
            }
        }
        internal void RemoveSingleSegmentBorders()
        {
            // Borders, sei es outline oder holes, die nur aus einer geschlossenen Kurve bestehen
            // werden in zwei Kurven aufgeteilt
            if (outline.Count == 1)
            {
                outline = new Border(outline[0].Split(0.5));
            }
            for (int i = 0; i < holes.Length; ++i)
            {
                if (holes[i].Count == 1)
                {
                    holes[i] = new Border(holes[i][0].Split(0.5));
                }
            }
        }
        internal bool HasSingleSegmentBorder()
        {
            if (outline.Count == 1)
            {
                return true;
            }
            for (int i = 0; i < holes.Length; ++i)
            {
                if (holes[i].Count == 1)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Converts the outline and the holes of this shape into <see cref="Path"/> objects.
        /// </summary>
        /// <param name="plane">Plane in 3D space where the shape is located</param>
        /// <returns>The resulting path(s)</returns>
        public Path[] MakePaths(Plane plane)
        {
            List<Path> res = new List<Path>();
            res.Add(Outline.MakePath(plane));
            for (int i = 0; i < holes.Length; ++i)
            {
                res.Add(holes[i].MakePath(plane));
            }
            return res.ToArray();
        }
        public Path2D GetSingleOutline()
        {   // berücksichtigt noch nicht, dass man zuerst das Loch nehmen sollte, welches am nächsten zum Rand liegt
            Path2D res = Outline.AsPath();
            for (int i = 0; i < holes.Length; i++)
            {
                Path2D h = holes[i].AsPath();
                h.Reverse();
                res = ConnectPaths(res, h);
            }
            return res;
        }

        public static Path2D ConnectPaths(Path2D outline, Path2D hole)
        {
            GeoPoint2D p1, p2;
            Path2D oflat = outline.Clone() as Path2D;
            oflat.Flatten();
            Path2D hflat = hole.Clone() as Path2D;
            hflat.Flatten();
            Curves2D.SimpleMinimumDistance(oflat, hflat, out p1, out p2);
            ICurve2D[] oparts = oflat.Split(oflat.PositionOf(p1));
            ICurve2D[] hparts = hflat.Split(hflat.PositionOf(p2));
            Path2D res = new Path2D(new ICurve2D[] { new Line2D(p2, p1) });
            if (oparts.Length == 2)
            {
                res.Append(oparts[1]);
                res.Append(oparts[0]);
            }
            else
            {
                res.Append(oparts[0]);
            }
            res.Append(new Line2D(p1, p2));
            if (hparts.Length == 2)
            {
                res.Append(hparts[1]);
                res.Append(hparts[0]);
            }
            else
            {
                res.Append(hparts[0]);
            }
            res.Flatten();
            return res;
        }

        #region IQuadTreeInsertable Members

        BoundingRect IQuadTreeInsertable.GetExtent()
        {
            return this.GetExtent();
        }

        bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            return this.HitTest(ref rect);
        }

        object IQuadTreeInsertable.ReferencedObject
        {
            get { return null; }
        }

        #endregion

        internal void Move(double dx, double dy)
        {
            outline.Move(dx, dy);
            for (int i = 0; i < holes.Length; ++i)
            {
                holes[i].Move(dx, dy);
            }
        }

        #region IComparable<SimpleShape> Members
        // wird halt gebraucht für OrderedMultiDictionary, obwohl es dort egal ist
        int IComparable<SimpleShape>.CompareTo(SimpleShape other)
        {
            return (GetExtent() as IComparable<BoundingRect>).CompareTo(other.GetExtent());
        }
        #endregion

        internal bool IsPointOnBorder(GeoPoint2D p, double precision)
        {
            Border.Position pos = outline.GetPosition(p, precision);
            if (pos == Border.Position.OnCurve) return true;
            for (int i = 0; i < holes.Length; ++i)
            {
                pos = holes[i].GetPosition(p, precision);
                if (pos == Border.Position.OnCurve) return true;
            }
            return false;
        }
        /// <summary>
        /// Checks whether this shape is correct. I.e. no (self-) intersections of the outline or holes, all holes inside the outline
        /// </summary>
        /// <returns></returns>
        public bool CheckTopology()
        {
            if (Outline.Area < 0) return false;
            if (Outline.GetSelfIntersection(0.0).Length > 0) return false;
            if (holes != null)
            {
                for (int i = 0; i < holes.Length; i++)
                {
                    if (holes[i].GetSelfIntersection(0.0).Length > 0) return false;
                    if (Outline.GetPosition(holes[i].StartPoint) == Border.Position.Outside) return false;
                    if (BorderPair.GetBorderOperation(null, Outline, holes[i], 0.0).Position != BorderOperation.BorderPosition.b1coversb2) return false;
                    for (int j = i + 1; j < holes.Length; j++)
                    {
                        if (BorderPair.GetBorderOperation(null, holes[j], holes[i], 0.0).Position != BorderOperation.BorderPosition.disjunct) return false;
                    }
                }
            }
            return true;
        }
        internal void AddHole(Border bdr)
        {
            if (holes == null) holes = new Border[0];
            Border[] newholes = new Border[holes.Length + 1];
            Array.Copy(holes, newholes, holes.Length);
            newholes[holes.Length] = bdr;
            holes = newholes;
        }

        public GeoPoint2D GetSomeInnerPoint()
        {
            BoundingRect ext = this.GetExtent() * 1.1;
            // Problem: zwei sich berührende Löcher, die Cliplinie geht genau durch die Berührstelle
            // um das zu vermeiden, wird ext ein wenig unsymmetrisch gemacht (denn Symmetrien sind oft das Problem
            ext.Right += ext.Width * 0.01;
            ext.Top += ext.Height * 0.02;
            double minDist = Math.Min(ext.Width, ext.Height) / 8; // soweit sollten wir vom rand entfernt sein, der Wert wird immer kleiner
            int n = 4;
            while (true)
            {
                double maxWidth = 0.0;
                GeoPoint2D bestPoint = GeoPoint2D.Origin;
                double d = ext.GetHeight() / n;
                for (int i = 1; i < n; i++)
                {
                    Line2D l2d = new Line2D(new GeoPoint2D(ext.Left, ext.Bottom + i * d), new GeoPoint2D(ext.Right, ext.Bottom + i * d));
                    double[] cl = this.Clip(l2d, true);
                    if (cl.Length > 0)
                    {
                        for (int j = 0; j < cl.Length; j += 2)
                        {
                            double dst = cl[j + 1] - cl[j];
                            if (dst > maxWidth)
                            {
                                maxWidth = dst;
                                bestPoint = l2d.PointAt((cl[j + 1] + cl[j]) / 2);
                            }
                        }
                    }
                }
                d = ext.GetWidth() / n;
                for (int i = 1; i < n; i++)
                {
                    Line2D l2d = new Line2D(new GeoPoint2D(ext.Left + i * d, ext.Bottom), new GeoPoint2D(ext.Left + i * d, ext.Top));
                    double[] cl = this.Clip(l2d, true);
                    if (cl.Length > 0)
                    {
                        for (int j = 0; j < cl.Length; j += 2)
                        {
                            double dst = cl[j + 1] - cl[j];
                            if (dst > maxWidth)
                            {
                                maxWidth = dst;
                                bestPoint = l2d.PointAt((cl[j + 1] + cl[j]) / 2);
                            }
                        }
                    }
                }
                // wenn der Punkt weit weg genug vom Rand ist, dann verwenden. Die Abfrage wird jedesmal lascher, so dass ein  Ende kommen muss
                if (Distance(bestPoint) > minDist) return bestPoint;
                minDist /= 4;
                n = 2 * n + 1; // damit es neue Linien gibt
                if (n > 10000) return GeoPoint2D.Origin;
            }
        }
        private CompoundShape.SignatureOld CalculateSignatureOld()
        {
            double lprec = outline.Length * 1e-4; // wir nehemen die Gesamtlänge, da die rotationsinvariant ist
            double aprec = Math.PI * 1e-3; // ungefähr 1/10 Grad
            while (true)
            {
                RangeCounter rcl = new RangeCounter(lprec, 0.0);
                RangeCounter rca = new RangeCounter(aprec, 0.0);
                outline.CalcRanges(rcl, rca);
                for (int i = 0; i < holes.Length; i++)
                {
                    holes[i].CalcRanges(rcl, rca);
                }
                bool aok = rca.isOk();
                bool lok = rcl.isOk();
                if (aok && lok)
                {
                    List<int> acnt = new List<int>();
                    List<double> aval = new List<double>();
                    foreach (KeyValuePair<double, int> item in rca)
                    {
                        acnt.Add(item.Value);
                        aval.Add(item.Key);
                    }
                    List<int> lcnt = new List<int>();
                    List<double> lval = new List<double>();
                    foreach (KeyValuePair<double, int> item in rcl)
                    {
                        lcnt.Add(item.Value);
                        lval.Add(item.Key);
                    }
                    return new CompoundShape.SignatureOld(aval.ToArray(), acnt.ToArray(), lval.ToArray(), lcnt.ToArray(), aprec, lprec);
                }
                if (!aok) aprec *= 1.7; // größere Intervalle, dann gibt es irgendwann nur noch eine Kategorie, in die alles fällt, damit endet diese Schleife sicher
                if (!lok) lprec *= 1.7;
            }
        }
        public bool isCongruent(SimpleShape other, CompoundShape.Signature otherSig, out ModOp2D thisToOther, double precision)
        {
            List<int> longestSides = new List<int>(); // es kann ja mehrere von der gleichen Länge geben
            double maxSide = 0.0;
            for (int i = 0; i < outline.Count; i++)
            {
                double l = outline[i].Length;
                if (l > maxSide)
                {
                    maxSide = l;
                }
            }
            for (int i = 0; i < outline.Count; i++)
            {
                double l = outline[i].Length;
                if (maxSide - l < maxSide * 1e-6)
                {
                    longestSides.Add(i);
                }
            }
            for (int i = 0; i < longestSides.Count; i++)
            {
                CompoundShape.Signature sig = CalculateSignature(longestSides[i]);
                bool reflect;
                BoundingRect ext = GetExtent();
                double prec = Math.Max(precision / ext.Width, precision / ext.Height) * 10;
                if (sig.isEqual(otherSig, out reflect, prec))
                {
                    SweepAngle sw = new SweepAngle(outline[longestSides[i]].MiddleDirection, GeoVector2D.XAxis);
                    ModOp2D thisToHor = ModOp2D.Rotate(sw);
                    ModOp2D otherToHor = ModOp2D.Rotate(otherSig.toHor);
                    SimpleShape thisHor = this.GetModified(thisToHor);
                    SimpleShape otherHor = other.GetModified(otherToHor);
                    BoundingRect thisHorExt = thisHor.GetExtent();
                    BoundingRect otherHorExt = otherHor.GetExtent();
                    if (Math.Abs(thisHorExt.Width - otherHorExt.Width) > precision || Math.Abs(thisHorExt.Height - otherHorExt.Height) > precision)
                    {
                        continue;
                    }
                    ModOp2D move;
                    if (reflect)
                    {
                        //move = ModOp2D.Fit(new GeoPoint2D[] { thisHorExt.GetLowerLeft(), thisHorExt.GetLowerRight() }, new GeoPoint2D[] { otherHorExt.GetLowerRight(), otherHorExt.GetLowerLeft() }, false);
                        ModOp2D rfl = ModOp2D.Scale(-1, 1);
                        move = rfl * ModOp2D.Translate((rfl * otherHorExt).GetLowerLeft() - thisHorExt.GetLowerLeft());
                    }
                    else
                    {
                        move = ModOp2D.Translate(otherHorExt.GetLowerLeft() - thisHorExt.GetLowerLeft());
                    }
                    thisToOther = otherToHor.GetInverse() * move * thisToHor;
                    SimpleShape res = this.GetModified(thisToOther);
                    if (SimpleShape.GetPosition(res, other, null, precision) == Position.identical) return true;

                }
            }
            thisToOther = ModOp2D.Null;
            return false;
        }
        public CompoundShape.Signature CalculateSignature()
        {
            // bestimme die längste Seite der Umrandung
            // es können natürlich mehere Seiten gleichlang sein, hier wird einfach eine davon genommen. Es gibt immer eine.
            double maxSide = 0.0;
            int ind = -1;
            for (int i = 0; i < outline.Count; i++)
            {
                double l = outline[i].Length;
                if (l > maxSide)
                {
                    maxSide = l;
                    ind = i;
                }
            }
            return CalculateSignature(ind);
        }

        private CompoundShape.Signature CalculateSignature(int ind)
        {
            // drehe die Form so, dass die gegebene Seite horizontal ist
            SweepAngle toHor = new SweepAngle(outline[ind].MiddleDirection, GeoVector2D.XAxis);
            ModOp2D makeHorizontal = ModOp2D.Rotate(toHor);
            SimpleShape horClone = GetModified(makeHorizontal);
            // mache 2 horizontale und 2 vertikale Schnitte
            BoundingRect ext = horClone.GetExtent() * 1.01; // etwas größer
            double d1 = 0.3854629847289;
            double d2 = 0.6493954732093; // zwei zufällige Zahlen, die möglichst keine Symmetrien oder sowas treffen
            double[][] res = new double[6][];
            res[0] = horClone.Clip(new Line2D(new GeoPoint2D(ext.Left, ext.Bottom + d1 * ext.Height), new GeoPoint2D(ext.Right, ext.Bottom + d1 * ext.Height)), true);
            res[1] = horClone.Clip(new Line2D(new GeoPoint2D(ext.Left, ext.Bottom + d2 * ext.Height), new GeoPoint2D(ext.Right, ext.Bottom + d2 * ext.Height)), true);
            // bei den senkrechten muss man symmetrisch sein, wegen der möglichen Spiegelung
            res[2] = horClone.Clip(new Line2D(new GeoPoint2D(ext.Left + (1.0 - d2) * ext.Width, ext.Bottom), new GeoPoint2D(ext.Left + (1.0 - d2) * ext.Width, ext.Top)), true);
            res[3] = horClone.Clip(new Line2D(new GeoPoint2D(ext.Left + d1 * ext.Width, ext.Bottom), new GeoPoint2D(ext.Left + d1 * ext.Width, ext.Top)), true);
            res[4] = horClone.Clip(new Line2D(new GeoPoint2D(ext.Left + (1.0 - d1) * ext.Width, ext.Bottom), new GeoPoint2D(ext.Left + (1.0 - d1) * ext.Width, ext.Top)), true);
            res[5] = horClone.Clip(new Line2D(new GeoPoint2D(ext.Left + d2 * ext.Width, ext.Bottom), new GeoPoint2D(ext.Left + d2 * ext.Width, ext.Top)), true);
            double length = outline.Length;
            for (int i = 0; i < holes.Length; i++)
            {
                length += holes[i].Length;
            }
            return new CompoundShape.Signature(toHor.Radian, res, length);
            //double[][] res = new double[holes.Length + 1][];
            //res[0] = outline.Code();
            //for (int i = 0; i < holes.Length; i++)
            //{
            //    res[i + 1] = holes[i].Code();
            //}
            //return new CompoundShape.Signature(res);
        }

#if DEBUG
        internal static Position GetPositionX(SimpleShape s1, SimpleShape s2, double precision)
        {
            Border[] bp = s1.outline.RemoveConstrictions(precision);
            BorderQuadTree bq = new BorderQuadTree(s1.outline, s2.outline, precision);

            switch (bq.Position)
            {
                case BorderQuadTree.BorderPosition.disjunct:
                    return Position.disjunct;
                case BorderQuadTree.BorderPosition.intersecting:
                    return Position.intersecting;
                case BorderQuadTree.BorderPosition.identical:
                    if (s1.NumHoles == 0 && s2.NumHoles == 0) return Position.identical;
                    if (s1.NumHoles == 0) return Position.firstcontainscecond;
                    if (s2.NumHoles == 0) return Position.secondcontainsfirst;
                    // beide haben Löcher
                    bool b1holesoutside = true;
                    for (int i = 0; i < s1.NumHoles; i++)
                    {
                        Position ppp = GetPositionX(new SimpleShape(s1.Holes[i]), s2, precision);
                        if (ppp != Position.disjunct)
                        {
                            b1holesoutside = false;
                            break;
                        }
                    }
                    bool b2holesoutside = true;
                    for (int i = 0; i < s2.NumHoles; i++)
                    {
                        Position ppp = GetPositionX(new SimpleShape(s2.Holes[i]), s1, precision);
                        if (ppp != Position.disjunct)
                        {
                            b2holesoutside = false;
                            break;
                        }
                    }
                    if (b1holesoutside && b2holesoutside) return Position.identical; // alle Löcher des einen sind außerhalb der Form des anderen (u. umgekehrt)
                    if (!b1holesoutside && b2holesoutside) return Position.secondcontainsfirst;
                    if (b1holesoutside && !b2holesoutside) return Position.firstcontainscecond;
                    return Position.intersecting; // beide haben Löcher im "Fleisch" des anderen

                case BorderQuadTree.BorderPosition.b1coversb2:
                    // zwei Möglichkeiten: firstcontainscecond oder intersecting
                    // wenn all Löcher von s1 außerhalb von s2 sind dann firstcontainscecond 
                    // sonst intersecting
                    for (int i = 0; i < s1.holes.Length; ++i)
                    {

                        BorderQuadTree bo1 = new BorderQuadTree(s1.holes[i], s2.outline, precision);
                        switch (bo1.Position)
                        {
                            case BorderQuadTree.BorderPosition.identical: // das Loch ist identisch mit s2
                            case BorderQuadTree.BorderPosition.b1coversb2:
                                return Position.disjunct; // s2 liegt ganz in einem Loch
                            case BorderQuadTree.BorderPosition.intersecting:
                                return Position.intersecting;
                            case BorderQuadTree.BorderPosition.disjunct:
                                continue; // kein Treffer
                            case BorderQuadTree.BorderPosition.b2coversb1:
                                // das Loch liegt ganz in s2:
                                // vermutlich intersecting, es sei denn, das Loch liegt
                                // wiederum ganz in einem Loch von s2
                                bool inside = false;
                                for (int j = 0; j < s2.holes.Length; ++j)
                                {
                                    BorderQuadTree bo2 = new BorderQuadTree(s1.holes[i], s2.holes[j], precision);
                                    if (bo2.Position == BorderQuadTree.BorderPosition.b2coversb1)
                                    {
                                        inside = true;
                                        break;
                                    }
                                }
                                if (!inside) return Position.intersecting;
                                break;
                            case BorderQuadTree.BorderPosition.unknown:
                                throw new BorderException("internal error in SimpleShape.GetPosition", BorderException.BorderExceptionType.InternalError);
                        }
                    }
                    return Position.firstcontainscecond;
                case BorderQuadTree.BorderPosition.b2coversb1:
                    Position pp = GetPositionX(s2, s1, precision); // um nicht obigen Fall mit umgekehrten Rollen nochmal zu schreiben
                    if (pp == Position.firstcontainscecond) return Position.secondcontainsfirst;
                    else if (pp == Position.secondcontainsfirst) return Position.firstcontainscecond;
                    else return pp;
                case BorderQuadTree.BorderPosition.unknown:
                    return Position.disjunct;
                    throw new BorderException("internal error in SimpleShape.GetPosition", BorderException.BorderExceptionType.InternalError);
            }
            // Alle Fälle abgedeckt, hierhin gehts nicht
            return Position.disjunct;
        }
#endif

        internal void AdjustVerticalLines(double x)
        {
            double prec = GetExtent().Size * 1e-8;
            for (int i = 0; i < outline.Segments.Length; i++)
            {
                if (outline.Segments[i] is Line2D)
                {
                    if (Math.Abs(outline.Segments[i].StartPoint.x - x) < prec && Math.Abs(outline.Segments[i].EndPoint.x - x) < prec)
                    {
                        outline.Segments[i].StartPoint = new GeoPoint2D(x, outline.Segments[i].StartPoint.y); // exakt setzen, es geht darum, gesplittete Shapes mit exakten Linien zu versehen
                        outline.Segments[i].EndPoint = new GeoPoint2D(x, outline.Segments[i].EndPoint.y);
                    }
                }
            }
            for (int j = 0; j < holes.Length; j++)
            {
                for (int i = 0; i < holes[j].Segments.Length; i++)
                {
                    if (holes[j].Segments[i] is Line2D)
                    {
                        if (Math.Abs(holes[j].Segments[i].StartPoint.x - x) < prec && Math.Abs(holes[j].Segments[i].EndPoint.x - x) < prec)
                        {
                            holes[j].Segments[i].StartPoint = new GeoPoint2D(x, holes[j].Segments[i].StartPoint.y); // exakt setzen, es geht darum, gesplittete Shapes mit exakten Linien zu versehen
                            holes[j].Segments[i].EndPoint = new GeoPoint2D(x, holes[j].Segments[i].EndPoint.y);
                        }
                    }
                }
            }
        }

        internal void AdjustHorizontalLines(double y)
        {
            double prec = GetExtent().Size * 1e-8;
            for (int i = 0; i < outline.Segments.Length; i++)
            {
                if (outline.Segments[i] is Line2D)
                {
                    if (Math.Abs(outline.Segments[i].StartPoint.y - y) < prec && Math.Abs(outline.Segments[i].EndPoint.y - y) < prec)
                    {
                        outline.Segments[i].StartPoint = new GeoPoint2D(outline.Segments[i].StartPoint.x, y); // exakt setzen, es geht darum, gesplittete Shapes mit exakten Linien zu versehen
                        outline.Segments[i].EndPoint = new GeoPoint2D(outline.Segments[i].EndPoint.x, y);
                    }
                }
            }
            for (int j = 0; j < holes.Length; j++)
            {
                for (int i = 0; i < holes[j].Segments.Length; i++)
                {
                    if (holes[j].Segments[i] is Line2D)
                    {
                        if (Math.Abs(holes[j].Segments[i].StartPoint.y - y) < prec && Math.Abs(holes[j].Segments[i].EndPoint.y - y) < prec)
                        {
                            holes[j].Segments[i].StartPoint = new GeoPoint2D(holes[j].Segments[i].StartPoint.x, y); // exakt setzen, es geht darum, gesplittete Shapes mit exakten Linien zu versehen
                            holes[j].Segments[i].EndPoint = new GeoPoint2D(holes[j].Segments[i].EndPoint.x, y);
                        }
                    }
                }
            }
        }
        #region IJsonSerialize
        protected SimpleShape()
        { }
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("Outline", outline);
            data.AddProperty("Holes", holes);
        }

        public void SetObjectData(IJsonReadData data)
        {
            outline = data.GetProperty<Border>("Outline");
            holes = data.GetProperty<Border[]>("Holes");
        }
        #endregion
    }

    /// <summary>
    /// A 2d shape composed by multiple <see cref="SimpleShape"/> objects.
    /// All simple shapes are disjoint. 
    /// </summary>
    [Serializable()]
    public class CompoundShape : ISerializable, IJsonSerialize, IQuadTreeInsertable
    {
        [Serializable()]
        internal class SignatureOld : ISerializable
        {
            double[] angleValues; // synchrone Arrays über die Winkel und wie oft sie vorkommen
            int[] angleCount;
            double[] lengthValues; // analog mit Längen
            int[] lengthCount;
            double anglePrecision; // mit der Winkelgenauigkeit wurde gerechnet
            double lengthPrecision; // mit der Längengenauigkeit wurde gerechnet

            public SignatureOld(double[] angleValues, int[] angleCount, double[] lengthValues, int[] lengthCount, double anglePrecision, double lengthPrecision)
            {
                this.angleValues = angleValues;
                this.angleCount = angleCount;
                this.lengthValues = lengthValues;
                this.lengthCount = lengthCount;
                this.anglePrecision = anglePrecision;
                this.lengthPrecision = lengthPrecision;
            }
            public bool isEqual(SignatureOld other)
            {   // simpler Gleichheitstest: Werte dürfen sich nur um Genauigkeit unterscheiden, Anzahlen müssen genau stimmen
                if (angleCount.Length != other.angleCount.Length) return false;
                if (lengthCount.Length != other.lengthCount.Length) return false;

                for (int i = 0; i < angleCount.Length; i++)
                {
                    if (Math.Abs(angleValues[i] - other.angleValues[i]) > anglePrecision + other.anglePrecision || angleCount[i] != other.angleCount[i])
                    {
                        return false;
                    }
                }
                for (int i = 0; i < lengthCount.Length; i++)
                {
                    if (Math.Abs(lengthValues[i] - other.lengthValues[i]) > lengthPrecision + other.lengthPrecision || lengthCount[i] != other.lengthCount[i])
                    {
                        return false;
                    }
                }
                return true;
            }

            protected SignatureOld(SerializationInfo info, StreamingContext context)
            {
                angleValues = (double[])info.GetValue("AngleValues", typeof(double[]));
                angleCount = (int[])info.GetValue("AngleCount", typeof(int[]));
                lengthValues = (double[])info.GetValue("LengthValues", typeof(double[]));
                lengthCount = (int[])info.GetValue("LengthCount", typeof(int[]));
                anglePrecision = (double)info.GetValue("AnglePrecision", typeof(double));
                lengthPrecision = (double)info.GetValue("LengthPrecision", typeof(double));
            }
            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("AngleValues", angleValues);
                info.AddValue("AngleCount", angleCount);
                info.AddValue("LengthValues", lengthValues);
                info.AddValue("LengthCount", lengthCount);
                info.AddValue("AnglePrecision", anglePrecision);
                info.AddValue("LengthPrecision", lengthPrecision);
            }
        }

        [Serializable()]
        public class Signature : ISerializable
        {
            double[][] values; // die Schnitte
            internal double toHor; // Richtung der Schnitte
            internal double length; // Länge der Kontour

            public Signature(double toHor, double[][] values, double length)
            {
                this.toHor = toHor;
                this.values = values;
                this.length = length;
            }
            private static int[] sameArrays(double[] a1, double[] a2, double lprec, double aprec, ref int reflectMode)
            {
                int[] res = new int[2];
                if (a1.Length != a2.Length) return null;
                int jmax = -1;
                double maxLength = 0.0;
                for (int j = 0; j < a1.Length; j += 2)
                {
                    if (a1[j] > maxLength)
                    {
                        maxLength = a1[j];
                        jmax = j;
                    }
                }
                // für das andere Array gibt es ggf. mehrere Kandidaten
                List<int> otherMax = new List<int>();
                for (int j = 0; j < a1.Length; j += 2)
                {
                    if (a2[j] > maxLength - lprec)
                    {
                        otherMax.Add(j); // gleiche Länge wie die max. Länge
                    }
                }
                bool ok = false;
                if (reflectMode != 2)
                {   // Test auf gleiche Orientierung
                    for (int k = 0; k < otherMax.Count; k++)
                    {
                        ok = true;
                        int cnt = a1.Length;
                        int omax = otherMax[k];
                        for (int j = 0; j < a1.Length; j += 2)
                        {
                            if (Math.Abs(a1[(j + jmax) % cnt] - a2[(j + omax) % cnt]) > lprec) ok = false;
                            if (Math.Abs(a1[(j + 1 + jmax) % cnt] - a2[(j + 1 + omax) % cnt]) > aprec) ok = false;
                            if (!ok) break;
                        }
                        if (ok)
                        {
                            // die Konstellation als Ergebnis leiefern
                            res[0] = jmax;
                            res[1] = omax;
                            reflectMode = 1; // richtig rum
                            break; // eine Konstellation gefunden
                        }
                    }
                }
                if (!ok && reflectMode != 1)
                {   // Test auf rückwärts Orientierung
                    for (int k = 0; k < otherMax.Count; k++)
                    {
                        ok = true;
                        int cnt = a1.Length;
                        int omax = otherMax[k];
                        for (int j = 0; j < a1.Length; j += 2)
                        {
                            if (Math.Abs(a1[(j + jmax) % cnt] - a2[(cnt - j + omax) % cnt]) > lprec) ok = false;
                            if (Math.Abs(a1[(j + 1 + jmax) % cnt] + a2[(cnt - j - 1 + omax) % cnt]) > aprec) ok = false; // Winkel jetzt andersrum
                            if (!ok) break;
                        }
                        if (ok)
                        {
                            // die Konstellation als Ergebnis leiefern
                            res[0] = jmax;
                            res[1] = omax;
                            reflectMode = 2; // richtig rum
                            break; // eine Konstellation gefunden
                        }
                    }
                }
                if (!ok) return null;
                return res;
            }
            //public int[] isEqual(Signature other, out bool reflect)
            //{
            //    reflect = false;
            //    int[] res = new int[2];
            //    double aprec = Math.PI * 1e-3; // 1/10°
            //    double totlen = 0.0;
            //    for (int i = 0; i < values[0].Length; i += 2)
            //    {
            //        totlen += values[0][i];
            //    }
            //    double lprec = totlen * 1e-4;
            //    if (values.Length != other.values.Length) return null;
            //    int reflectMode = 0;
            //    res = sameArrays(values[0], other.values[0], lprec, aprec, ref reflectMode);
            //    reflect = reflectMode == 2;
            //    if (res == null) return null;
            //    // die Löcher können in verschiedener Reihenfolge sein
            //    for (int i = 1; i < values.Length; i++)
            //    {
            //        bool ok = false;
            //        for (int j = 1; j < values.Length; j++)
            //        {
            //            if (other.values[j].Length == values[i].Length)
            //            {
            //                int[] tst = sameArrays(values[i], other.values[j], lprec, aprec, ref reflectMode);
            //                if (tst != null)
            //                {
            //                    ok = true;
            //                    break;
            //                }
            //            }
            //        }
            //        if (!ok) return null;
            //    }
            //    return res;
            //}

            public bool isEqual(Signature other, out bool reflect, double precision)
            {   // enthält horizontale und vertikale Schnitte im Sinne von Clip, also Werte zwischen 0 und 1
                reflect = false;
                int reflectMode = 0; // 0: unbestimmte, 1: nicht spiegeln, 2: spiegeln
                if (values.Length != other.values.Length) return false;
                for (int i = 0; i < 2; i++)
                {
                    if (values[i].Length != other.values[i].Length) return false;
                    bool ok = true;
                    if (reflectMode != 2)
                    {
                        ok = true;
                        for (int j = 0; j < values[i].Length; j++)
                        {
                            if (Math.Abs(values[i][j] - other.values[i][j]) > precision)
                            {
                                ok = false;
                                break;
                            }
                        }
                        if (ok) reflectMode = 1;
                    }
                    if (reflectMode != 1)
                    {
                        ok = true;
                        for (int j = 0; j < values[i].Length; j++)
                        {
                            if (Math.Abs((1.0 - values[i][values[i].Length - j - 1]) - other.values[i][j]) > precision)
                            {
                                ok = false;
                                break;
                            }
                        }
                        if (ok) reflectMode = 2;
                    }
                    if (!ok) return false;
                }
                for (int i = 2; i < 6; i++)
                {
                    int ii = i;
                    if (reflectMode == 2)
                    {   // hier die senkrechten von rechts nach links statt links nach rechts
                        ii = 7 - ii; // 5, 4, 3, 2
                    }
                    if (values[ii].Length != other.values[i].Length) return false;
                    bool ok = true;
                    ok = true;
                    for (int j = 0; j < values[ii].Length; j++)
                    {
                        if (Math.Abs(values[ii][j] - other.values[i][j]) > precision)
                        {
                            ok = false;
                            break;
                        }
                    }
                    if (!ok) return false;
                }
                reflect = reflectMode == 2;
                if (Math.Abs(length - other.length) > 2 * precision) return false; // 2*, da Länge mehr Fehler haben kann
                return true; // alle Prüfungen bestenden
            }

            protected Signature(SerializationInfo info, StreamingContext context)
            {
                values = (double[][])info.GetValue("Values", typeof(double[][]));
                toHor = (double)info.GetValue("ToHor", typeof(double));
                length = (double)info.GetValue("Length", typeof(double));
            }
            void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue("Values", values);
                info.AddValue("ToHor", toHor);
                info.AddValue("Length", length);
            }
        }

        private SimpleShape[] simpleShapes;
        /// <summary>
        /// Constructs an empty shape
        /// </summary>
        public CompoundShape()
        {
            simpleShapes = new SimpleShape[0];
        }
        /// <summary>
        /// Constructs a compund shape from one or many simple shapes. The simple shapes must be non overlapping
        /// <note>If you are not sure, whether</note>
        /// </summary>
        /// <param name="simpleShapes">the simple shapes</param>
        public CompoundShape(params SimpleShape[] simpleShapes)
        {
            this.simpleShapes = (SimpleShape[])simpleShapes.Clone();
        }
        /// <summary>
        /// Das im Parameter gegebene Objekt muss eine offene Kurve sein, die dieses CompoundShape
        /// Objekt durchschneidet. Das Ergebniss ist ein CompoundShape, was aus mehreren SimpleShapes
        /// besteht, die durch das Durchschneiden entstanden sind.
        /// </summary>
        /// <param name="ToSplitWith"></param>
        /// <returns></returns>
        public CompoundShape Split(Border ToSplitWith)
        {
            try
            {
                List<SimpleShape> sslist = new List<SimpleShape>();
                foreach (SimpleShape ss in simpleShapes)
                {
                    CompoundShape cs = ss.Split(ToSplitWith);
                    foreach (SimpleShape part in cs.SimpleShapes)
                    {
                        sslist.Add(part);
                    }
                }
                return new CompoundShape(sslist.ToArray());
            }
            catch (Exception e) // wenn hier irgendwas schief geht, dann nicht mehr asynchron laufen lassen
            {
                if (e is ThreadAbortException) throw (e);
                return this; // ungesplittet
            }
        }
        public CompoundShape Clone()
        {
            CompoundShape res = new CompoundShape();
            res.simpleShapes = new SimpleShape[simpleShapes.Length];
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                res.simpleShapes[i] = simpleShapes[i].Clone();
            }
            return res;
        }
        /// <summary>
        /// Creates a CompundShape from the curves in this list. Assumes the curves are connected. The outer hull is returned.
        /// </summary>
        /// <param name="curves2d">The curves</param>
        /// <param name="maxGap">Maximum gap between curves</param>
        /// <param name="partInPart">Return Part in Part (true) or remove them (false)</param>
        /// <param name="deadObjectList">List of objects that could not be used to build a CompoundShape</param>
        /// <returns>The shape</returns>
        public static CompoundShape CreateFromList(ICurve2D[] curves2d, double maxGap, bool partInPart, out GeoObjectList deadObjectList)
        {
            CurveGraph pg = new CurveGraph(curves2d, maxGap);
            CompoundShape res = pg.CreateCompoundShape(false, new GeoPoint2D(0.0, 0.0), ConstrHatchInside.HatchMode.hull, partInPart);
            deadObjectList = new GeoObjectList(pg.DeadObjects);
            return res;
        }
        /// <summary>
        /// Creates a CompundShape from the curves in this list. Assumes the curves are connected. The outer hull is returned.
        /// </summary>
        /// <param name="curves2d">The curves</param>
        /// <param name="maxGap">Maximum gap between curves</param>
        /// <returns>The shape</returns>
        public static CompoundShape CreateFromList(ICurve2D[] curves2d, double maxGap)
        {
            CurveGraph pg = new CurveGraph(curves2d, maxGap);
            CompoundShape res = pg.CreateCompoundShape(false, new GeoPoint2D(0.0, 0.0), ConstrHatchInside.HatchMode.hull, false);
            return res;
        }
        /// <summary>
        /// Creates a CompundShape from the objects in the list. The objects must reside in a common plane.
        /// </summary>
        /// <param name="TheObjects">The objects to be connected to a shape</param>
        /// <param name="maxGap">Maximum gap between curves</param>
        /// <param name="plane">The common plane and the location of the shape in 3d space</param>
        /// <returns>The shape, may be null if no shape could be created</returns>
        public static CompoundShape CreateFromList(GeoObjectList TheObjects, double maxGap, out Plane plane, bool partInPart = false)
        {
            ArrayList curvesal = new ArrayList();

            foreach (IGeoObject go in TheObjects)
            {	// TODO: Blöcke auflösen!!!
                ICurve cv = go as ICurve;
                if (cv != null)
                {
                    curvesal.Add(cv);
                }
            }
            ICurve[] curves = (ICurve[])(curvesal.ToArray(typeof(ICurve)));
            if (Curves.GetCommonPlane(curves, out plane))
            {
                ArrayList curves2D = new ArrayList(curves.Length);
                for (int i = 0; i < curves.Length; ++i)
                {
                    ICurve2D cv2d = curves[i].GetProjectedCurve(plane);
                    if (cv2d != null) curves2D.Add(cv2d);
                }
                Reduce2D r2d = new Reduce2D();
                r2d.Add((ICurve2D[])curves2D.ToArray(typeof(ICurve2D)));
                r2d.Precision = maxGap;
                r2d.BreakPolylines = true;
                r2d.OutputMode = Reduce2D.Mode.Simple;
                CurveGraph pg = new CurveGraph(r2d.Reduced, maxGap);
                CompoundShape res = pg.CreateCompoundShape(false, new GeoPoint2D(0.0, 0.0), ConstrHatchInside.HatchMode.hull, partInPart);
                if (res != null)
                {
                    for (int i = 0; i < res.simpleShapes.Length; i++)
                    {
                        res.simpleShapes[i].Reduce(maxGap);
                    }
                }
                return res;
            }
            return null;
        }
        /// <summary>
        /// Creates a CompundShape from the objects in the list. The objects must reside in a common plane.
        /// Only the shape that encloses the inner point is created. If <paramref name="excludeHoles"/> is true,
        /// The resulting shape may contain holes.
        /// </summary>
        /// <param name="TheObjects">The curves to make the shape from</param>
        /// <param name="innerPoint">The inner point to define which shape is to be created</param>
        /// <param name="connected">True: the curves are already connected (faster performance), false: the curves may overlap or intersect</param>
        /// <param name="excludeHoles">True: exclude all holes that are inside the outer shape, false: return only the outer shape</param>
        /// <param name="maxGap">Maximum gap between curves</param>
        /// <param name="plane">The common plane and the location of the shape in 3d space</param>
        /// <returns>The shape or null, if no appropriate shape found</returns>
        public static CompoundShape CreateFromList(GeoObjectList TheObjects, GeoPoint innerPoint, bool connected, bool excludeHoles, double maxGap, out Plane plane)
        {
            List<ICurve> curvesal = new List<ICurve>();
            TheObjects.DecomposeAll();

            foreach (IGeoObject go in TheObjects)
            {
                ICurve cv = go as ICurve;
                if (cv != null)
                {
                    curvesal.Add(cv);
                }
            }
            ICurve[] curves = curvesal.ToArray();
            if (Curves.GetCommonPlane(curves, out plane))
            {
                CurveGraph pg;
                if (connected)
                {
                    ArrayList curves2D = new ArrayList(curves.Length);
                    for (int i = 0; i < curves.Length; ++i)
                    {
                        ICurve2D cv2d = curves[i].GetProjectedCurve(plane);
                        if (cv2d != null) curves2D.Add(cv2d);
                    }
                    pg = new CurveGraph((ICurve2D[])curves2D.ToArray(typeof(ICurve2D)), maxGap);
                }
                else
                {
                    pg = CurveGraph.CrackCurves(TheObjects, plane, maxGap); // gap eine Ordnung größer als Precision
                }
                CompoundShape res;
                if (excludeHoles)
                    res = pg.CreateCompoundShape(true, plane.Project(innerPoint), ConstrHatchInside.HatchMode.excludeHoles, false);
                else
                    res = pg.CreateCompoundShape(true, plane.Project(innerPoint), ConstrHatchInside.HatchMode.simple, false); // richtiger Mode?
                return res;
            }
            return null;
        }
        /// <summary>
        /// Creates a CompundShape from the objects in the list. The objects must reside in a common plane.
        /// The objects must reside in the provided <paramref name="plane"/>. The <paramref name="innerPoint"/> defines
        /// a point which must be inside the shape. The <paramref name="mode"/> gives more control about the desired result.
        /// </summary>
        /// <param name="TheObjects">The objects (curves) that build the shape</param>
        /// <param name="plane">The plane, in which the objects reside</param>
        /// <param name="innerPoint">the inner point which acts as a seed to define the shape</param>
        /// <param name="maxGap">The maiximum allowable gap between curves to still consider them as connected.</param>
        /// <param name="mode">The mode, <see cref="ConstrHatchInside.HatchMode"/></param>
        /// <returns>The shape or null, if no appropriate shape found</returns>
        public static CompoundShape CreateFromConnectedList(GeoObjectList TheObjects, Plane plane, GeoPoint2D innerPoint, double maxGap, ConstrHatchInside.HatchMode mode)
        {
            Border[] bdrs = Border.ClosedBordersFromList(TheObjects, plane, maxGap);
            Array.Sort(bdrs, new BorderAreaComparer());
            // der größe nach sortieren, zuerst kommt das kleinste
            if (mode != ConstrHatchInside.HatchMode.allShapes)
            {
                int bestBorder = -1;
                if (mode != ConstrHatchInside.HatchMode.hull)
                {	// suche die kleinste umgebende Umrandung:
                    for (int i = 0; i < bdrs.Length; ++i)
                    {
                        if (bdrs[i].GetPosition(innerPoint) == Border.Position.Inside)
                        {
                            bestBorder = i;
                            break;
                        }
                    }
                }
                else
                {	// suche die größte umgebende Umrandung:
                    // also rückwärts durch das array
                    for (int i = bdrs.Length - 1; i >= 0; --i)
                    {
                        if (bdrs[i].GetPosition(innerPoint) == Border.Position.Inside)
                        {
                            bestBorder = i;
                            break;
                        }
                    }
                }
                if (bestBorder >= 0)
                {
                    if (mode == ConstrHatchInside.HatchMode.excludeHoles)
                    {
                        // nur die kleineren Borders betrachten, die größeren können ja keine Löcher sein
                        SimpleShape ss = new SimpleShape(bdrs[bestBorder]);
                        CompoundShape cs = new CompoundShape(ss);
                        for (int j = 0; j < bestBorder; ++j)
                        {   // das sind nur die Löcher
                            cs.Subtract(new SimpleShape(bdrs[j]));
                        }
                        for (int j = bestBorder + 1; j < bdrs.Length; ++j)
                        {   // hier sind keine echten Löcher mehr
                            SimpleShape ss1 = new SimpleShape(bdrs[j]);
                            if (SimpleShape.GetPosition(ss, ss1) == SimpleShape.Position.intersecting)
                            {
                                if (bdrs[j].GetPosition(innerPoint) == Border.Position.Inside)
                                {
                                    cs = CompoundShape.Intersection(cs, new CompoundShape(ss1));
                                }
                                else
                                {
                                    cs.Subtract(ss1);
                                }
                            }
                        }
                        return cs;
                    }
                    else
                    {
                        SimpleShape ss = new SimpleShape(bdrs[bestBorder]);
                        CompoundShape cs = new CompoundShape(ss);
                        for (int j = 0; j < bdrs.Length; ++j)
                        {
                            if (j != bestBorder)
                            {   // echte Löcher ignorieren
                                SimpleShape ss1 = new SimpleShape(bdrs[j]);
                                if (SimpleShape.GetPosition(ss, ss1) == SimpleShape.Position.intersecting)
                                {
                                    if (bdrs[j].GetPosition(innerPoint) == Border.Position.Inside)
                                    {
                                        cs = CompoundShape.Intersection(cs, new CompoundShape(ss1));
                                    }
                                    else
                                    {
                                        cs.Subtract(ss1);
                                    }
                                }
                            }
                        }
                        return cs;
                    }
                }
            }
            else
            {   // wenn nicht "useInnerPoint", dann die erste (größte) Border liefern
                // bei überlappenden Konturen kein definiertes Vorgehen
                if (bdrs.Length > 0)
                {
                    Array.Reverse(bdrs); // das größte zuerst
                    List<Border> toIterate = new List<Border>(bdrs);
                    CompoundShape res = new CompoundShape();
                    while (toIterate.Count > 0)
                    {
                        SimpleShape ss = new SimpleShape(toIterate[0]);
                        CompoundShape cs = new CompoundShape(ss);
                        // das erste ist der Rand, die folgenden die Löcher
                        for (int i = toIterate.Count - 1; i > 0; --i)
                        {
                            SimpleShape ss1 = new SimpleShape(toIterate[i]);
                            if (SimpleShape.GetPosition(ss, ss1) == SimpleShape.Position.firstcontainscecond)
                            {
                                cs.Subtract(ss1);
                                toIterate.RemoveAt(i);
                            }
                        }
                        toIterate.RemoveAt(0);
                        res = CompoundShape.Union(res, cs);
                    }
                    return res;
                }
            }
            return null;
        }
        /* Democode für Münnich/Metzler
        internal static void CheckCompoundShapeVersusList(Plane pln, CompoundShape cs, GeoObjectList list, double maxGap)
        {
            for (int i = 0; i < cs.simpleShapes.Length; i++)
            {
                CheckBorderVersusList(pln, cs.simpleShapes[i].Outline, list, maxGap);
                for (int j = 0; j < cs.simpleShapes[i].Holes.Length; j++)
                {
                    CheckBorderVersusList(pln, cs.simpleShapes[i].Holes[j], list, maxGap);
                }
            }
        }

        private static void CheckBorderVersusList(Plane pln, Border border, GeoObjectList list, double maxGap)
        {
            for (int i = 0; i < border.Segments.Length; i++)
            {
                GeoPoint sp = pln.ToGlobal(border.Segments[i].StartPoint);
                GeoPoint ep = pln.ToGlobal(border.Segments[i].EndPoint);
                for (int j = 0; j < list.Count; j++)
                {
                    ICurve curve = list[i] as ICurve;
                    if (curve != null)
                    {
                        GeoPoint sp1 = curve.StartPoint;
                        GeoPoint ep1 = curve.EndPoint;
                        if (Geometry.Dist(sp1, sp) < maxGap && Geometry.Dist(ep1 , ep) < maxGap)
                        {
                            list.Remove(j);
                            break;
                        }
                        if (Geometry.Dist(ep1, sp) < maxGap && Geometry.Dist(sp1 , ep) < maxGap)
                        {
                            list.Remove(j);
                            break;
                        }
                    }
                }
            }
        }*/
        /// <summary>
        /// Returns the array of SimpleShape, which define this CompoundShape. In many cases, there is only one SimpleShape.
        /// </summary>
        public SimpleShape[] SimpleShapes
        {
            get
            {
                return simpleShapes;
            }
        }
        /// <summary>
        /// The 2d-compund shape is assumed to reside in plane "fromPlane". It will be projected
        /// perpendicular onto the plane "toPlane". If the planes are perpendicular, the result
        /// will be am empty shape
        /// </summary>
        /// <param name="fromPlane">the containing plane</param>
        /// <param name="toPlane">the projection plane</param>
        /// <returns>the projected shape</returns>
        public CompoundShape Project(Plane fromPlane, Plane toPlane)
        {
            if (Precision.IsPerpendicular(fromPlane.Normal, toPlane.Normal, true))
            {
                return new CompoundShape();
            }
            else
            {
                SimpleShape[] ss = new SimpleShape[simpleShapes.Length];
                for (int i = 0; i < simpleShapes.Length; ++i)
                {
                    ss[i] = simpleShapes[i].Project(fromPlane, toPlane);
                }
                return new CompoundShape(ss);
            }
        }
        public CompoundShape GetModified(ModOp2D m)
        {
            SimpleShape[] ss = new SimpleShape[simpleShapes.Length];
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                ss[i] = simpleShapes[i].GetModified(m);
            }
            return new CompoundShape(ss);
        }
        public CompoundShape Shrink(double d)
        {
            if (d < 0.0) return Expand(-d); // nur echtes Zusammenziehen, Ausdehnen dort
            CompoundShape res = new CompoundShape();
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                CompoundShape cs = simpleShapes[i].Shrink(d);
                // sind sicher alle disjunkt!
                if (!cs.Empty) res.UniteDisjunct(cs);
            }
            return res;
        }
        public CompoundShape Expand(double d)
        {
            if (d < 0.0) return Shrink(-d); // nur echtes Ausdehnen, Zusammenziehen dort
            CompoundShape res = new CompoundShape();
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                CompoundShape cs = simpleShapes[i].Expand(d);
                if (cs != null && !cs.Empty) res = res + cs; // vereinigen, können sich ja berürhern!
            }
            return res;
        }
        public static bool Interferes(CompoundShape cs1, CompoundShape cs2)
        {   
            for (int i = 0; i < cs1.SimpleShapes.Length; i++)
            {
                for (int j = 0; j < cs2.SimpleShapes.Length; j++)
                {
                    if (SimpleShape.GetPosition(cs1.SimpleShapes[i], cs2.SimpleShapes[j]) != SimpleShape.Position.disjunct) return true;
                }
            }
            return false;
        }
        static public CompoundShape operator +(CompoundShape s1, CompoundShape s2)
        {
            return PlusOperator(s1, s2, Precision.eps);
        }
        static internal CompoundShape PlusOperator(CompoundShape s1, CompoundShape s2, double precision)
        {
            // simpler Gedanke: betrachte alle Kombinationen von simpleshapes miteinander:
            // überschneiden sich zwei, dann werden beide aus den Listen entfernt und die Vereinigung 
            // wird in eine dritte Liste zugefügt. Ist das eine im anderen enthalten, so wird das enthaltene
            // aus seiner Liste entfernt. Zum Schluss werden beide Listen zusammengeschmissen, denn die SimpleShapes
            // sind ja alle disjunkt
            List<SimpleShape> first = new List<SimpleShape>(s1.simpleShapes);
            List<SimpleShape> second = new List<SimpleShape>(s2.simpleShapes);
            List<SimpleShape> third = new List<SimpleShape>();
            for (int i = 0; i < s1.simpleShapes.Length; ++i)
            {
                for (int j = 0; j < s2.simpleShapes.Length; ++j)
                {
                    try
                    {
                        Dictionary<BorderPair, BorderOperation> borderOperationCache = new Dictionary<BorderPair, BorderOperation>();
                        switch (SimpleShape.GetPosition(s1.simpleShapes[i], s2.simpleShapes[j], borderOperationCache, precision))
                        {
                            case SimpleShape.Position.disjunct:
                                // disjunkt, keine weitere Aktion
                                break;
                            case SimpleShape.Position.firstcontainscecond:
                                second.Remove(s2.simpleShapes[j]);
                                break;
                            case SimpleShape.Position.secondcontainsfirst:
                                first.Remove(s1.simpleShapes[i]);
                                break;
                            case SimpleShape.Position.identical:
                                second.Remove(s2.simpleShapes[j]);
                                break;
                            case SimpleShape.Position.intersecting:
                                // kommt auch, wenn sie nur Punkte gemeinsam haben, Unite gibt dann
                                // allerdings eine exception
                                // weiteres Problem: Die objecte in third sind nicht unbedingt
                                // disjunkt
                                third.AddRange(SimpleShape.Unite(s1.simpleShapes[i], s2.simpleShapes[j], precision, borderOperationCache).simpleShapes);
                                first.Remove(s1.simpleShapes[i]);
                                second.Remove(s2.simpleShapes[j]);
                                break;
                        }
                    }
                    catch (BorderException)
                    {   // Position ist nicht bestimmbar, gewöhnlich sind die irgendwie identisch
                        // oder sie berühren sich über längere Strecken mit vielen Schnittpunkten
                        // dann wir heuristisch verfahren
                        double a1 = s1.simpleShapes[i].Area;
                        double a2 = s2.simpleShapes[j].Area;
                        if (a2 >= a1)
                        {
                            GeoPoint2D ip = s1.simpleShapes[i].GetSomeInnerPoint();
                            if (s2.Contains(ip, false))
                            {   // s1 ganz innerhalb von s2
                                first.Remove(s1.simpleShapes[i]);
                            }
                        }
                        else
                        {
                            GeoPoint2D ip = s2.simpleShapes[j].GetSomeInnerPoint();
                            if (s1.Contains(ip, false))
                            {   // s1 ganz innerhalb von s2
                                second.Remove(s2.simpleShapes[j]);
                            }
                        }
                    }
                }
            }
            bool intersecting = true;
            while (intersecting)
            {   // die Einzelteile in third können sich immer noch überschneiden
                intersecting = false;
                for (int i = 0; i < third.Count - 1; ++i)
                {
                    for (int j = i + 1; j < third.Count; ++j)
                    {
                        try
                        {
                            switch (SimpleShape.GetPosition(third[i], third[j]))
                            {
                                case SimpleShape.Position.disjunct:
                                    // disjunkt, keine weitere Aktion
                                    break;
                                case SimpleShape.Position.firstcontainscecond:
                                    third.RemoveAt(j);
                                    intersecting = true;
                                    break;
                                case SimpleShape.Position.secondcontainsfirst:
                                    third.RemoveAt(i);
                                    intersecting = true;
                                    break;
                                case SimpleShape.Position.intersecting:
                                    // kommt auch, wenn sie nur Punkte gemeinsam haben, Unite gibt dann
                                    // allerdings eine exception
                                    // weiteres Problem: Die objecte in third sind nicht unbedingt
                                    // disjunkt
                                    CompoundShape toAdd = SimpleShape.Unite(third[i], third[j], precision);
                                    // zwei SimpleShapes wurden vereinigt. Wenn das Ergebnis wieder zwei SimpleShapes sind, dann ist was schief gegangen
                                    // denn die beiden überschneiden sich ja.
                                    if (toAdd.simpleShapes.Length == 1)
                                    {
                                        third.AddRange(SimpleShape.Unite(third[i], third[j], precision).simpleShapes);
                                        third.RemoveAt(j); // zuerst mit dem größeren Index löschen
                                        third.RemoveAt(i);
                                        intersecting = true;
                                    }
                                    break;
                            }
                        }
                        catch (BorderException)
                        {   // Position ist nicht bestimmbar, gewöhnlich sind die irgendwie identisch
                            // oder sonstwie pathologisch
                            break;
                        }
                        if (intersecting) break;
                    }
                    if (intersecting) break;
                }
            }
            List<SimpleShape> all = new List<SimpleShape>(first);
            all.AddRange(second);
            all.AddRange(third);

            CompoundShape res = new CompoundShape();
            res.UniteDisjunct(all.ToArray());
            return res;
        }
#if DEBUG
        static internal CompoundShape PlusOperatorX(CompoundShape s1, CompoundShape s2, double precision)
        {
            // simpler Gedanke: betrachte alle Kombinationen von simpleshapes miteinander:
            // überschneiden sich zwei, dann werden beide aus den Listen entfernt und die Vereinigung 
            // wird in eine dritte Liste zugefügt. Ist das eine im anderen enthalten, so wird das enthaltene
            // aus seiner Liste entfernt. Zum Schluss werden beide Listen zusammengeschmissen, denn die SimpleShapes
            // sind ja alle disjunkt
            List<SimpleShape> first = new List<SimpleShape>(s1.simpleShapes);
            List<SimpleShape> second = new List<SimpleShape>(s2.simpleShapes);
            List<SimpleShape> third = new List<SimpleShape>();
            for (int i = 0; i < s1.simpleShapes.Length; ++i)
            {
                for (int j = 0; j < s2.simpleShapes.Length; ++j)
                {
                    switch (SimpleShape.GetPositionX(s1.simpleShapes[i], s2.simpleShapes[j], precision))
                    {
                        case SimpleShape.Position.disjunct:
                            // disjunkt, keine weitere Aktion
                            break;
                        case SimpleShape.Position.firstcontainscecond:
                            second.Remove(s2.simpleShapes[j]);
                            break;
                        case SimpleShape.Position.secondcontainsfirst:
                            first.Remove(s1.simpleShapes[i]);
                            break;
                        case SimpleShape.Position.identical:
                            second.Remove(s2.simpleShapes[j]);
                            break;
                        case SimpleShape.Position.intersecting:
                            // kommt auch, wenn sie nur Punkte gemeinsam haben, Unite gibt dann
                            // allerdings eine exception
                            // weiteres Problem: Die objecte in third sind nicht unbedingt
                            // disjunkt
                            third.AddRange(SimpleShape.Unite(s1.simpleShapes[i], s2.simpleShapes[j], precision).simpleShapes);
                            first.Remove(s1.simpleShapes[i]);
                            second.Remove(s2.simpleShapes[j]);
                            break;
                    }
                }
            }
            bool intersecting = true;
            while (intersecting)
            {   // die Einzelteile in third können sich immer noch überschneiden
                intersecting = false;
                for (int i = 0; i < third.Count - 1; ++i)
                {
                    for (int j = i + 1; j < third.Count; ++j)
                    {
                        try
                        {
                            switch (SimpleShape.GetPositionX(third[i], third[j], precision))
                            {
                                case SimpleShape.Position.disjunct:
                                    // disjunkt, keine weitere Aktion
                                    break;
                                case SimpleShape.Position.firstcontainscecond:
                                    third.RemoveAt(j);
                                    intersecting = true;
                                    break;
                                case SimpleShape.Position.secondcontainsfirst:
                                    third.RemoveAt(i);
                                    intersecting = true;
                                    break;
                                case SimpleShape.Position.intersecting:
                                    // kommt auch, wenn sie nur Punkte gemeinsam haben, Unite gibt dann
                                    // allerdings eine exception
                                    // weiteres Problem: Die objecte in third sind nicht unbedingt
                                    // disjunkt
                                    CompoundShape toAdd = SimpleShape.Unite(third[i], third[j]);
                                    // zwei SimpleShapes wurden vereinigt. Wenn das Ergebnis wieder zwei SimpleShapes sind, dann ist was schief gegangen
                                    // denn die beiden überschneiden sich ja.
                                    if (toAdd.simpleShapes.Length == 1) third.AddRange(SimpleShape.Unite(third[i], third[j]).simpleShapes);
                                    third.RemoveAt(j); // zuerst mit dem größeren Index löschen
                                    third.RemoveAt(i);
                                    intersecting = true;
                                    break;
                            }
                        }
                        catch (BorderException)
                        {   // Position ist nicht bestimmbar, gewöhnlich sind die irgendwie identisch
                            // oder sonstwie pathologisch
                            break;
                        }
                        if (intersecting) break;
                    }
                    if (intersecting) break;
                }
            }
            List<SimpleShape> all = new List<SimpleShape>(first);
            all.AddRange(second);
            all.AddRange(third);

            CompoundShape res = new CompoundShape();
            res.UniteDisjunct(all.ToArray());
            return res;
        }
#endif
        static public CompoundShape Union(CompoundShape s1, CompoundShape s2)
        {
            return s1 + s2;
        }
        static public CompoundShape Union(CompoundShape s1, CompoundShape s2, double precision)
        {
            return PlusOperator(s1, s2, precision);
        }
#if DEBUG
        static public CompoundShape UnionX(CompoundShape s1, CompoundShape s2, double precision)
        {
            if (precision == 0.0) precision = (s1.GetExtent().Size + s2.GetExtent().Size) * 1e-6;
            return PlusOperatorX(s1, s2, precision);
        }
#endif
        internal void UniteDisjunct(params CompoundShape[] shapes)
        {
            ArrayList al = new ArrayList(simpleShapes);
            for (int i = 0; i < shapes.Length; ++i)
            {
                for (int j = 0; j < shapes[i].simpleShapes.Length; ++j)
                {
                    if (!shapes[i].simpleShapes[j].Empty) al.Add(shapes[i].simpleShapes[j]);
                }
            }
            simpleShapes = (SimpleShape[])al.ToArray(typeof(SimpleShape));
        }
        internal void UniteDisjunct(params SimpleShape[] shapes)
        {
            ArrayList al = new ArrayList(simpleShapes);
            for (int i = 0; i < shapes.Length; ++i)
            {
                if (!shapes[i].Empty) al.Add(shapes[i]);
            }
            simpleShapes = (SimpleShape[])al.ToArray(typeof(SimpleShape));
        }
        internal void UniteDisjunct(params Border[] borders)
        {
            ArrayList al = new ArrayList(simpleShapes);
            for (int i = 0; i < borders.Length; ++i)
            {
                if (borders[i].Area != 0.0) al.Add(new SimpleShape(borders[i]));
            }
            simpleShapes = (SimpleShape[])al.ToArray(typeof(SimpleShape));
        }
        //		static internal CompoundShape UniteDisjunct(params CompoundShape [] shapes)
        //		{
        //			ArrayList simpleshapes = new ArrayList();
        //			for (int i=0; i<shapes.Length; ++i)
        //			{
        //				simpleshapes.AddRange(shapes[i].simpleShapes);
        //			}
        //			CompoundShape res = new CompoundShape((SimpleShape[])simpleshapes.ToArray(typeof(SimpleShape)));
        //			return res;
        //		}
        //		static internal CompoundShape UniteDisjunct(Border [] borders)
        //		{
        //			SimpleShape [] ss = new SimpleShape[borders.Length];
        //			for (int i=0; i<ss.Length; ++i)
        //			{
        //				ss[i] = new SimpleShape(borders[i]);
        //			}
        //			return new CompoundShape(ss);
        //		}
        /// <summary>
        /// Returns the difference of the two CompoundShapes s1 and s2 (s1-s2). 
        /// The result may be empty. 
        /// </summary>
        /// <param name="s1">first operand</param>
        /// <param name="s2">second operand</param>
        /// <returns>difference</returns>
        static public CompoundShape operator -(CompoundShape s1, CompoundShape s2)
        {
            return MinusOperator(s1, s2, Precision.eps);
        }
        static internal CompoundShape MinusOperator(CompoundShape s1, CompoundShape s2, double precision)
        {
            if (s1.Empty) return new CompoundShape();
            if (s2.Empty) return s1.Clone();
            CompoundShape res = new CompoundShape();
            for (int i = 0; i < s1.simpleShapes.Length; ++i)
            {
                CompoundShape tmp = new CompoundShape();
                tmp = SimpleShape.Subtract(s1.simpleShapes[i], s2.simpleShapes[0], precision);
                for (int j = 1; j < s2.simpleShapes.Length; ++j)
                {
                    tmp.Subtract(s2.simpleShapes[j]);
                }
                res.UniteDisjunct(tmp);
            }
            return res;
        }
#if DEBUG
        static public CompoundShape DifferenceX(CompoundShape s1, CompoundShape s2, double precision)
        {
            if (s1.Empty) return new CompoundShape();
            if (s2.Empty) return s1.Clone();
            CompoundShape res = new CompoundShape();
            for (int i = 0; i < s1.simpleShapes.Length; ++i)
            {
                CompoundShape tmp = new CompoundShape();
                tmp = SimpleShape.SubtractX(s1.simpleShapes[i], s2.simpleShapes[0], precision);
                for (int j = 1; j < s2.simpleShapes.Length; ++j)
                {
                    tmp = CompoundShape.DifferenceX(tmp, new CompoundShape(s2.simpleShapes[j]), precision); // wird nicht rekursiv, da nur ein einziges SimpleShape in 2. Parameter
                }
                res.UniteDisjunct(tmp);
            }
            return res;
        }
#endif
        static public CompoundShape Difference(CompoundShape s1, CompoundShape s2)
        {
            return s1 - s2;
        }
        static public CompoundShape Difference(CompoundShape s1, CompoundShape s2, double precision)
        {
            return MinusOperator(s1, s2, precision);
        }
        internal void Subtract(SimpleShape toSubtract)
        {
            ArrayList css = new ArrayList();
            ArrayList sss = new ArrayList();
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                css.Add(SimpleShape.Subtract(simpleShapes[i], toSubtract));
            }
            for (int i = 0; i < css.Count; ++i)
            {
                CompoundShape cs = (CompoundShape)css[i];
                if (cs != null)
                {
                    for (int j = 0; j < cs.simpleShapes.Length; ++j)
                    {
                        sss.Add(cs.simpleShapes[j]);
                    }
                }
            }
            simpleShapes = (SimpleShape[])sss.ToArray(typeof(SimpleShape));
        }
        static public CompoundShape operator *(CompoundShape s1, CompoundShape s2)
        {   // Alle Schnitte zwischen simpleshapes von s1 und simpleshapes von s2 disjunkt vereinigen
            CompoundShape res = new CompoundShape();
            for (int i = 0; i < s1.simpleShapes.Length; ++i)
            {
                for (int j = 0; j < s2.simpleShapes.Length; ++j)
                {
                    res.UniteDisjunct(SimpleShape.Intersect(s1.simpleShapes[i], s2.simpleShapes[j]));
                }
            }
            return res;
        }
        static public CompoundShape Intersection(CompoundShape s1, CompoundShape s2)
        {
            return s1 * s2;
        }
        static public CompoundShape Intersection(CompoundShape s1, CompoundShape s2, double precision)
        {
            CompoundShape res = new CompoundShape();
            for (int i = 0; i < s1.simpleShapes.Length; ++i)
            {
                for (int j = 0; j < s2.simpleShapes.Length; ++j)
                {
                    res.UniteDisjunct(SimpleShape.Intersect(s1.simpleShapes[i], s2.simpleShapes[j], precision));
                }
            }
            return res;
        }
        internal void IndexSegments()
        {
            int index = 0;
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                for (int j = 0; j < simpleShapes[i].Outline.Count; ++j)
                {
                    simpleShapes[i].Outline.Segments[j].UserData.Add("CADability.Border.Index", index++);
                }
                for (int j = 0; j < simpleShapes[i].NumHoles; ++j)
                {
                    for (int k = 0; k < simpleShapes[i].Hole(j).Count; ++k)
                    {
                        simpleShapes[i].Hole(j).Segments[k].UserData.Add("CADability.Border.Index", index++);
                    }
                }
            }
        }
        public bool Empty
        {
            get
            {
                if (simpleShapes == null) return true;
                if (simpleShapes.Length == 0) return true;
                if (Area == 0.0) return true;
                return false;
            }
        }
        public double Area
        {
            get
            {
                double res = 0.0;
                for (int i = 0; i < simpleShapes.Length; ++i)
                {
                    res += simpleShapes[i].Area;
                }
                return res;
            }
        }
        public bool HitTest(ref BoundingRect Rect)
        {
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                if (simpleShapes[i].HitTest(ref Rect)) return true;
            }
            return false;
        }
        public bool Contains(GeoPoint2D p, bool acceptOnCurve)
        {
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                if (simpleShapes[i].Contains(p, acceptOnCurve)) return true;
            }
            return false;
        }
        public System.Drawing.Drawing2D.GraphicsPath CreateGraphicsPath()
        {
            System.Drawing.Drawing2D.GraphicsPath res = new System.Drawing.Drawing2D.GraphicsPath();
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                simpleShapes[i].AddToGraphicsPath(res);
            }
            return res;
        }
        public double[] Clip(ICurve2D toClip, bool returnInsideParts)
        {
            List<double> res = new List<double>();
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                res.AddRange(simpleShapes[i].Clip(toClip, returnInsideParts));
            }
            res.Sort(); // es gibt ja keine Überschneidungen, Sortierung allerdings wäre auch nicht wichtig
            // da immer gerade Anzahl und zwei Positionen sind ein Intervall
            return res.ToArray();
        }
        /// <summary>
        /// Returns planar faces, one for each contained <see cref="SimpleShape"/>.
        /// </summary>
        /// <param name="plane">Plane where to construct the faces</param>
        /// <returns>Array of faces</returns>
        public Face[] MakeFaces(Plane plane)
        {
            List<Face> res = new List<Face>();
            for (int i = 0; i < SimpleShapes.Length; ++i)
            {
                Face fc = Face.MakeFace(new PlaneSurface(plane), SimpleShapes[i]);
                if (fc != null) res.Add(fc);
            }
            return res.ToArray();
        }
        /// <summary>
        /// Converts the shape to one or more <see cref="Path"/> objects according to the provided plane.
        /// </summary>
        /// <param name="plane">Plane in 3D space where the shape should be located</param>
        /// <returns>The resulting path(s)</returns>
        public Path[] MakePaths(Plane plane)
        {
            List<Path> res = new List<Path>();
            for (int i = 0; i < SimpleShapes.Length; ++i)
            {
                res.AddRange(SimpleShapes[i].MakePaths(plane));
            }
            return res.ToArray();
        }
        public GeoObjectList DebugList
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                for (int i = 0; i < simpleShapes.Length; ++i)
                {
                    res.AddRange(simpleShapes[i].DebugList);
                }
                return res;
            }
        }
        internal ICurve[] Curves3d()
        {
            List<ICurve> res = new List<ICurve>();
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                SimpleShape ss = simpleShapes[i];
                Border bdr = ss.Outline;
                for (int j = 0; j < bdr.Count; ++j)
                {
                    if (bdr[j].UserData.ContainsData("3d"))
                    {
                        res.Add(bdr[j].UserData.GetData("3d") as ICurve);
                        bdr[j].UserData.RemoveUserData("3d");
                    }
                }
                for (int j = 0; j < ss.NumHoles; ++j)
                {
                    bdr = ss.Hole(j);
                    for (int k = 0; k < bdr.Count; ++k)
                    {
                        if (bdr[k].UserData.ContainsData("3d"))
                        {
                            res.Add(bdr[k].UserData.GetData("3d") as ICurve);
                            bdr[k].UserData.RemoveUserData("3d");
                        }
                    }
                }
            }
            return res.ToArray();
        }
        public Path2D GetSingleOutline()
        {   // berücksichtigt noch nicht, dass eine Verbindungslinie ein weiteres simpleShape schneidet
            Path2D res = simpleShapes[0].GetSingleOutline();
            for (int i = 1; i < simpleShapes.Length; i++)
            {
                res = SimpleShape.ConnectPaths(res, simpleShapes[i].GetSingleOutline());
            }
            return res;
        }

        public static CompoundShape FromSingleOutline(ICurve2D[] curves, double maxGap)
        {
            int ind = 0;
            List<SimpleShape> simpleShapes = new List<SimpleShape>();
            SimpleShape accumulating = null;
            while (ind < curves.Length)
            {
                GeoPoint2D sp = curves[ind].StartPoint;
                Border bdr = null;
                for (int i = ind; i < curves.Length; i++)
                {
                    if ((curves[i].EndPoint | sp) < maxGap)
                    {
                        // curves[i].EndPoint = sp; // sicher schließen, nicht nötig wg. Border Konstruktor
                        ICurve2D[] parts;
                        if (curves[i] is Line2D && curves[ind] is Line2D && (curves[ind].EndPoint | curves[i].StartPoint) < maxGap / 2 && i - ind > 1)
                        {   // beginnt und endet mit der selben Linie, nur rückwärts
                            parts = new ICurve2D[i - ind - 1];
                            Array.Copy(curves, ind + 1, parts, 0, i - ind - 1);
                        }
                        else
                        {
                            parts = new ICurve2D[i - ind + 1];
                            Array.Copy(curves, ind, parts, 0, i - ind + 1);
                        }
                        bdr = new Border(parts, true);
                        ind = i + 1;
                        break;
                    }
                }
                if (bdr != null)
                {
                    if (accumulating == null)
                    {
                        accumulating = new SimpleShape(bdr);
                    }
                    else
                    {   // es kommt immer zuerst die Outline, dann die holes
                        SimpleShape bdrshape = new SimpleShape(bdr);
                        if (SimpleShape.GetPosition(accumulating, bdrshape) == SimpleShape.Position.firstcontainscecond)
                        {
                            accumulating.AddHole(bdr);
                        }
                        else
                        {
                            simpleShapes.Add(accumulating);
                            accumulating = bdrshape;
                        }
                    }
                }
                else break; // es muss immer ein Border entstehen
            }
            if (accumulating != null) simpleShapes.Add(accumulating); // das letzte noch dazu
            return new CompoundShape(simpleShapes.ToArray());
        }
        private UserData userData;
        public UserData UserData
        {
            get
            {
                if (userData == null) userData = new UserData();
                return userData;
            }
        }
        public BoundingRect GetDisplayExtent(double WorldToPixel) { return GetExtent(); }
        #region IQuadTreeInsertable Members
        public BoundingRect GetExtent()
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                res.MinMax(simpleShapes[i].GetExtent());
            }
            return res;
        }
        public bool HitTest(ref BoundingRect Rect, bool IncludeControlPoints)
        {
            return HitTest(ref Rect);
        }
        object IQuadTreeInsertable.ReferencedObject
        {
            get { return null; }
        }
#endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected CompoundShape(SerializationInfo info, StreamingContext context)
        {
            simpleShapes = (SimpleShape[])info.GetValue("SimpleShapes", typeof(SimpleShape[]));
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("SimpleShapes", simpleShapes);
        }

        #endregion
        #region IJsonSerialize
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("SimpleShapes", simpleShapes);
        }

        public void SetObjectData(IJsonReadData data)
        {
            simpleShapes = data.GetProperty<SimpleShape[]>("SimpleShapes");
        }
        #endregion
        public void Approximate(bool linesOnly, double precision)
        {
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                SimpleShape ss = simpleShapes[i];
                ss.Approximate(linesOnly, precision);
            }
        }

        internal void Move(double dx, double dy)
        {
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                simpleShapes[i].Move(dx, dy);
            }
        }

        internal void AdjustVerticalLines(double x)
        {
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                simpleShapes[i].AdjustVerticalLines(x);
            }
        }
        internal void AdjustHorizontalLines(double y)
        {
            for (int i = 0; i < simpleShapes.Length; ++i)
            {
                simpleShapes[i].AdjustHorizontalLines(y);
            }
        }

    }

}
