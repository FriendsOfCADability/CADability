using CADability.GeoObject;
using System.Collections.Generic;

namespace CADability.Curve2D
{
    /// <summary>
    /// Helper class to reduce duplicated or 2d connected curves. To use this class create an empty instance,
    /// <see cref="Reduce2D.Add">Add</see>/> curves to it and access the <see cref="Reduce2D.Reduced">Reduced</see>/>
    /// property. Some flags add additional control on how the reduction is performed
    /// </summary>

    public class Reduce2D : IEqualityComparer<ICurve2D>
    {
        HashSet<ICurve2D> curves;
        QuadTree<ICurve2D> quadtree;

        private int curveId;
        /// <summary>
        /// Empty constructor. Use <see cref="Add"/> to add curves.
        /// </summary>
        public Reduce2D()
        {
            curves = new HashSet<ICurve2D>(this);
            Precision = CADability.Precision.eps;
            FlattenPolylines = 0.0;
            curveId = 0;
        }
        /// <summary>
        /// Adds a curve to the reduction.
        /// </summary>
        /// <param name="curve"></param>
        public void Add(ICurve2D curve)
        {
            if (BreakPolylines && curve is Polyline2D)
            {
                Add((curve as Polyline2D).GetSubCurves());
            }
            else
            {
                curve.UserData.Add("Reduce2D.ID", ++curveId); // curveId wg. determinierbarkeit des Set Objektes, wird von Dan Swope so gebraucht
                curves.Add(curve);
            }
        }
        /// <summary>
        /// Adds an array of curves to the reduction
        /// </summary>
        /// <param name="curves"></param>
        public void Add(ICurve2D[] curves)
        {
            for (int i = 0; i < curves.Length; i++)
            {
                Add(curves[i]);
            }
        }
        /// <summary>
        /// Add all objects from l, which are or contain curves. The curves are projected onto the provided plane.
        /// If l contains composite objects (e.g. Block, Path) the objects are decomposed.
        /// </summary>
        /// <param name="l"></param>
        /// <param name="pln"></param>
        public void Add(GeoObjectList l, Plane pln)
        {
            for (int i = 0; i < l.Count; i++)
            {
                AddPrimitiv(l[i], pln);
            }
        }

        private void AddPrimitiv(IGeoObject go, Plane pln)
        {
            if (go.NumChildren > 0)
            {
                for (int i = 0; i < go.NumChildren; i++)
                {
                    AddPrimitiv(go.Child(i), pln);
                }
            }
            else
            {
                if (go is ICurve)
                {
                    ICurve2D c2d = (go as ICurve).GetProjectedCurve(pln);
                    c2d.UserData.Add("CADability.OriginalGeoObjects", new GeoObjectList(go));
                    Add(c2d);
                }
            }
        }

        // Flags:
        /// <summary>
        /// Mode definition.
        /// </summary>
        public enum Mode
        {
            /// <summary>
            /// Return only simple objects, do not collect objects to polylines or paths.
            /// </summary>
            Simple,
            /// <summary>
            /// Collect conected lines to polylines
            /// </summary>
            Polylines,
            /// <summary>
            /// Collect connected curves to paths <see cref="Path2D"/>
            /// </summary>
            Paths
        }
        /// <summary>
        /// Desired output mode.
        /// </summary>
        public Mode OutputMode;
        /// <summary>
        /// Precision: two curves are combined if the resulting curve differs less than this value from the orginal curves.
        /// </summary>
        public double Precision;
        /// <summary>
        /// Break up polylines added to this object. Depending on <see cref="OutputMode"/> the result may again be combined
        /// to polylines or paths.
        /// </summary>
        public bool BreakPolylines;
        /// <summary>
        /// Precision to remove inner points of polylines. If set to 0, polylines remain unchanged
        /// </summary>
        public double FlattenPolylines;
        /// <summary>
        /// Access this property for the result of the reduction.
        /// </summary>
        public ICurve2D[] Reduced
        {
            get
            {
                BoundingRect ext = BoundingRect.EmptyBoundingRect;
#if DEBUG
                DebuggerContainer dc = new DebuggerContainer();
#endif
                foreach (ICurve2D c2d in curves)
                {
                    ext.MinMax(c2d.GetExtent());
#if DEBUG
                    dc.Add(c2d, System.Drawing.Color.Red, 0);
#endif
                }
                ext.Move(new GeoVector2D(ext.Width * 1.23456e-6, ext.Width * 1.65432e-6)); // um einen krummen minimalen Wert
                // verschieben, damit keine zentrierten Symmetrieprobleme auftreten
                quadtree = new QuadTree<ICurve2D>(ext * 1.1);
                quadtree.MaxListLen = 3;
                foreach (ICurve2D c2d in curves)
                {
                    if (c2d.Length > Precision) quadtree.AddObject(c2d);
                }
                // Problem: viele kurze Linien und Kreisbögen könnten zu einem Kreisbogen zusammengefasst werden
                // Das geht aber mit der Konzentration auf immer nur 2 Objekte nicht gut
                List<ICurve2D> res = new List<ICurve2D>();
                ICurve2D startWith;
                while ((startWith = quadtree.SomeObject) != null)
                {
                    quadtree.RemoveObject(startWith);
                    bool didFuse;
                    do
                    {
                        didFuse = false;
                        BoundingRect swext = startWith.GetExtent();
                        swext.Inflate(Precision);
                        ICurve2D[] close = quadtree.GetObjectsFromRect(swext);
                        foreach (ICurve2D c2d in close)
                        {
                            ICurve2D fused = startWith.GetFused(c2d, Precision);
                            if (fused != null)
                            {
                                AddOriginals(fused, startWith, c2d);
                                startWith = fused;
                                quadtree.RemoveObject(c2d);
                                didFuse = true;
                                break;
                            }
                        }
                    } while (didFuse);
                    res.Add(startWith);
                }
                if (OutputMode == Mode.Paths)
                {   // nochmal alles in den QuadTree und jetzt Pfade zusammensammeln
                    quadtree.AddObjects(res);
                    res.Clear();
                    while ((startWith = quadtree.SomeObject) != null)
                    {
                        quadtree.RemoveObject(startWith);
                        bool didConnect;
                        do
                        {
                            BoundingRect swext = startWith.GetExtent();
                            swext.Inflate(Precision);
                            didConnect = false;
                            ICurve2D[] close = quadtree.GetObjectsFromRect(swext);
                            foreach (ICurve2D c2d in close)
                            {
                                if (startWith is Path2D)
                                {
                                    if ((startWith as Path2D).ConnectWith(c2d, Precision))
                                    {
                                        AddOriginals(startWith, c2d);
                                        quadtree.RemoveObject(c2d);
                                        didConnect = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    Path2D p2d = new Path2D(new ICurve2D[] { startWith });
                                    AddOriginals(p2d, startWith);
                                    if (p2d.ConnectWith(c2d, Precision))
                                    {
                                        AddOriginals(p2d, c2d);
                                        startWith = p2d;
                                        quadtree.RemoveObject(c2d);
                                        didConnect = true;
                                        break;
                                    }
                                }
                            }
                        } while (didConnect);
                        if (startWith is Path2D)
                        {
                            if ((startWith.EndPoint | startWith.StartPoint) < Precision)
                            {
                                startWith.EndPoint = startWith.StartPoint;
                            }
                        }
                        res.Add(startWith);
                    }
                }
                else if (OutputMode == Mode.Polylines)
                {   // nochmal alles in den QuadTree und jetzt Polylines zusammensammeln
                    quadtree.AddObjects(res);
                    res.Clear();
                    while ((startWith = quadtree.SomeObject) != null)
                    {
                        quadtree.RemoveObject(startWith);
                        if (startWith is Line2D || startWith is Polyline2D)
                        {
                            BoundingRect swext = startWith.GetExtent();
                            swext.Inflate(Precision);
                            bool didConnect;
                            do
                            {
                                didConnect = false;
                                ICurve2D[] close = quadtree.GetObjectsFromRect(swext);
                                foreach (ICurve2D c2d in close)
                                {
                                    if (c2d is Line2D)
                                    {
                                        if (startWith is Polyline2D)
                                        {
                                            if ((startWith as Polyline2D).ConnectWith(c2d as Line2D, Precision))
                                            {
                                                AddOriginals(startWith, c2d);
                                                quadtree.RemoveObject(c2d);
                                                didConnect = true;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            Polyline2D p2d = new Polyline2D(new GeoPoint2D[] { startWith.StartPoint, startWith.EndPoint });
                                            AddOriginals(p2d, startWith);
                                            if (p2d.ConnectWith(c2d as Line2D, Precision))
                                            {
                                                AddOriginals(p2d, c2d);
                                                startWith = p2d;
                                                quadtree.RemoveObject(c2d);
                                                didConnect = true;
                                                break;
                                            }
                                        }
                                    }
                                    else if (c2d is Polyline2D)
                                    {
                                        if (startWith is Polyline2D)
                                        {
                                            if ((startWith as Polyline2D).ConnectWith(c2d as Polyline2D, Precision))
                                            {
                                                AddOriginals(startWith, c2d);
                                                quadtree.RemoveObject(c2d);
                                                didConnect = true;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            Polyline2D p2d = new Polyline2D(new GeoPoint2D[] { startWith.StartPoint, startWith.EndPoint });
                                            AddOriginals(p2d, startWith);
                                            if (p2d.ConnectWith(c2d as Line2D, Precision))
                                            {
                                                AddOriginals(p2d, c2d);
                                                startWith = p2d;
                                                quadtree.RemoveObject(c2d);
                                                didConnect = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            } while (didConnect);
                        }
                        if (startWith is Polyline2D)
                        {
                            if ((startWith.EndPoint | startWith.StartPoint) < Precision && startWith.Length > Precision) // schließen
                            {
                                startWith.EndPoint = startWith.StartPoint;
                            }
                        }
                        res.Add(startWith);
                    }
                    if (FlattenPolylines > 0.0)
                    {
                        for (int i = 0; i < res.Count; i++)
                        {
                            if (res[i] is Polyline2D) (res[i] as Polyline2D).Reduce(FlattenPolylines);
                        }
                    }
                }
                UsedOriginals = new GeoObjectList();
                for (int i = 0; i < res.Count; i++)
                {
                    GeoObjectList l = res[i].UserData.GetData("CADability.OriginalGeoObjects") as GeoObjectList;
                    if (l != null) UsedOriginals.AddRangeUnique(l);
                }
                return res.ToArray();
            }
        }

        /// <summary>
        /// When adding GeoObjects with the method Add(GeoObjectList l, Plane pln), a list of used GeoObjects
        /// is maintained when the result "Reduced" is calculated. "UsedOriginals" provides a list of those objects, that are
        /// represented in the result. The objects might also be children of the provided objects.
        /// </summary>
        public GeoObjectList UsedOriginals;

        private void AddOriginals(ICurve2D addTo, params ICurve2D[] copy)
        {
            GeoObjectList l = new GeoObject.GeoObjectList();
            for (int i = 0; i < copy.Length; i++)
            {
                GeoObjectList toAdd = copy[i].UserData.GetData("CADability.OriginalGeoObjects") as GeoObjectList;
                if (toAdd != null)
                {
                    l.AddRangeUnique(toAdd);
                }
            }
            addTo.UserData.Add("CADability.OriginalGeoObjects", l);
        }
#if DEBUG
        DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer dc = new DebuggerContainer();
                foreach (ICurve2D c2d in curves)
                {
                    dc.Add(c2d, System.Drawing.Color.Red, 0);
                }
                return dc;
            }
        }
#endif

        bool IEqualityComparer<ICurve2D>.Equals(ICurve2D x, ICurve2D y)
        {
            int idx = (int)x.UserData.GetData("Reduce2D.ID");
            int idy = (int)y.UserData.GetData("Reduce2D.ID");
            return idx == idy;
        }

        int IEqualityComparer<ICurve2D>.GetHashCode(ICurve2D obj)
        {
            int id = (int)obj.UserData.GetData("Reduce2D.ID");
            return id.GetHashCode();
        }
    }
}
