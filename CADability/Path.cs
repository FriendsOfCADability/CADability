using CADability.Attribute;
using CADability.Curve2D;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability.GeoObject
{

    public class PathException : ApplicationException
    {
        public enum ExceptionType { PathIsEmtpy, PathIsInvalid, NoModification, NotPlanar };
        public ExceptionType et;
        public PathException(ExceptionType ExceptionType, string msg)
            : base(msg)
        {
            this.et = ExceptionType;
        }
    }

    /// <summary>
    /// A geoobject, that derives from Block and represents a ordered collection of
    /// geoobjects which all support the ICurve intrface. The contained curves are connected, 
    /// i.e. Child(i).EndPoint is equal or close to Child(i+1).StartPoint.
    /// The Path is not necessary planar, closed and may be self intersecting.
    /// </summary>
    [Serializable()]
    public class Path : IGeoObjectImpl, IColorDef, ILinePattern, ILineWidth, ICurve,
        IGeoObjectOwner, ISerializable, IDeserializationCallback, IExtentedableCurve
    {
        public enum ModificationMode
        {
            /// <summary>
            /// when modifying a point keeps the angle between the two connected curves
            /// </summary>
            keepAngle,
            /// <summary>
            /// keeps the ratio of the two radii of an ellipse, i.e. a circular ellipse will stay circular
            /// </summary>
            keepArcRatio
        }
        // sollte mit einem OpenCascade buddy (wire) versehen werden
        private ICurve[] subCurves; // die enthaltenen Kurven
        private double[] length; // die Längen der einzelnen Teilobjekte
        private PlaneRef inPlane; // gibt die Ebene an, in der sich der Pfad befindet, wenn es eine gibt
        private PlanarState planarState; // Unknown, wenn noch nicht berechnet
        private bool isClosed; // letzte Kurve endet am Anfangspunkt der ersten
        private void Recalc()
        {	// neu berechnen der Längen und Parameter Spannen
            length = new double[subCurves.Length];
            for (int i = 0; i < subCurves.Length; ++i)
            {
                length[i] = subCurves[i].Length;
            }
            if (subCurves.Length > 0) isClosed = Precision.IsEqual(StartPoint, EndPoint);
            else isClosed = false;
        }
        private void RecalcPlanarState()
        {	// berechnet den PlanarState und wenn planar, dann auch die Ebene
            if (planarState == PlanarState.Unknown || inPlane==null)
            {
                if (subCurves == null) return;
                if (subCurves.Length == 0) return; // bleibt unbekannt
                if (subCurves.Length == 1)
                {
                    planarState = subCurves[0].GetPlanarState();
                    if (planarState == PlanarState.Planar)
                    {
                        inPlane = subCurves[0].GetPlane();
                    }
                }
                else
                {
                    Plane pln;
                    if (CADability.GeoObject.Curves.GetCommonPlane(subCurves, out pln))
                    {
                        inPlane = new PlaneRef(pln);
                        planarState = PlanarState.Planar;
                    }
                    else
                    {	// hier gibt es noch zwei Möglichkeiten: NonPlanar und Underdetermined
                        // letzteres gilt es herauszufinden:
                        bool nonplanar = false;
                        for (int i = 0; i < subCurves.Length; ++i)
                        {
                            if (subCurves[i].GetPlanarState() != PlanarState.UnderDetermined)
                            {
                                nonplanar = true;
                                break;
                            }
                        }
                        if (nonplanar)
                        {
                            planarState = PlanarState.NonPlanar;
                            return;
                        }
                        // also alle sind UnderDetermined
                        GeoVector dir = subCurves[0].StartDirection;
                        for (int i = 0; i < subCurves.Length; ++i)
                        {
                            if (!Precision.SameDirection(dir, subCurves[i].EndDirection, false))
                            {	// verschiedene Richtungen
                                nonplanar = true;
                                break;
                            }
                        }
                        if (nonplanar)
                        {
                            planarState = PlanarState.NonPlanar;
                        }
                        else
                        {	// alles dieselbe Richtung, Pfad aus mehreren Linien, die eine Linie bilden
                            planarState = PlanarState.UnderDetermined;
                        }
                    }
                }
            }
        }
        #region polymorph construction
        public delegate Path ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Path Construct()
        {
            if (Constructor != null) return Constructor();
            return new Path();
        }
        public static Path FromSegments(IEnumerable<ICurve> curves, bool connectedAndOriented)
        {
            Path res = Path.Construct();
            if (connectedAndOriented)
            {
                res.Set(curves.ToArray());
                return res;
            }
            else
            {
                GeoObjectList geoObjects = new GeoObjectList();
                foreach (ICurve crv in curves)
                {
                    geoObjects.Add(crv as IGeoObject);
                }
                if (res.Set(geoObjects, false, Precision.eps)) return res;
                else return null;
            }
        }
        public delegate void ConstructedDelegate(Path justConstructed);
        public static event ConstructedDelegate Constructed;
        #endregion
        //		private void RecalcPlane()
        //		{
        //			if (inPlane!=null) return; // schon berechnet
        //			GeoPoint [] pts = new GeoPoint[subCurves.Length];
        //			for (int i=0; i<subCurves.Length; ++i) 
        //			{
        //				pts[i] = subCurves[i].StartPoint;
        //			}
        //			double maxDist;
        //			Plane pln = Plane.FromPoints(pts,out maxDist);
        //			if (maxDist>Precision.eps) return;
        //			for (int i=0; i<subCurves.Length; ++i) 
        //			{
        //				if (subCurves[i].GetPlanarState()==Condor.PlanarState.NonPlanar) return;
        //				if (!subCurves[i].IsInPlane(pln)) return;
        //			}
        //			inPlane = new PlaneRef(pln);
        //		}
        protected Path()
        {
            planarState = PlanarState.Unknown;
            if (Constructed != null) Constructed(this);
        }
        private static Path Construct(ICurve[] connectedCurves)
        {
            Path res = Path.Construct();
            if (connectedCurves.Length > 0) res.CopyAttributes(connectedCurves[0] as IGeoObject); // Farbe etc vom der ersten Kurve nehmen
            if (connectedCurves.Length > 1)
            {   // die Liste darf keine geschlossenen Kurven enthalten, das interaktive vertex-verschieben geht sonst nicht
                List<ICurve> openCurves = new List<ICurve>(connectedCurves);
                for (int i = openCurves.Count - 1; i >= 0; --i)
                {
                    if (openCurves[i].IsClosed) openCurves.RemoveAt(i);
                }
                connectedCurves = openCurves.ToArray();
            }
            res.subCurves = (ICurve[])connectedCurves.Clone();
            for (int i = 0; i < res.subCurves.Length; ++i)
            {
                IGeoObject go = res.subCurves[i] as IGeoObject;
                if (go != null)
                {
                    if (go.Owner != null) go.Owner.Remove(go);
                    go.Owner = res;
                    go.DidChangeEvent += new ChangeDelegate(res.SubCurveDidChange);
                    go.WillChangeEvent += new ChangeDelegate(res.SubCurveWillChange);
                }
            }
            res.planarState = PlanarState.Unknown;
            res.Recalc();
            return res;
        }
        /// <summary>
        /// Creates a path from a list of properly connected curve objects.
        /// The curve objects will be removed from their owner (if there is a owner)
        /// </summary>
        /// <param name="connectedCurves">array of connected ICurve objects</param>
        //protected Path(ICurve[] connectedCurves)
        //    : this()
        //{
        //    if (connectedCurves.Length>0) CopyAttributes(connectedCurves[0] as IGeoObject); // Farbe etc vom der ersten Kurve nehmen
        //    subCurves = (ICurve[])connectedCurves.Clone();
        //    for (int i = 0; i < subCurves.Length; ++i)
        //    {
        //        IGeoObject go = subCurves[i] as IGeoObject;
        //        if (go != null)
        //        {
        //            if (go.Owner != null) go.Owner.Remove(go);
        //            go.Owner = this;
        //            go.DidChangeEvent += new ChangeDelegate(SubCurveDidChange);
        //            go.WillChangeEvent += new ChangeDelegate(SubCurveWillChange);
        //        }
        //    }
        //    planarState = PlanarState.Unknown;
        //    Recalc();
        //}
        /// <summary>
        /// Returns a Path containing the curve BeginWith and other curves from the projectedModel.
        /// The curve BeginWith is checked at both ends to find connected objects. The search is stopped
        /// if there are nore more connected objects. The curves are clones and are tagged with <see cref="UserData"/> objects
        /// with the name key "CADability.Path.Original" and the original object as the value. 
        /// If flatten is true, UserData will be lost.
        /// </summary>
        /// <param name="BeginWith">Curve to begin with</param>
        /// <param name="projectedModel">Projected model to search</param>
        /// <param name="flatten">true: flatten the result, false: result not flattened</param>
        /// <returns>The path or null if no cennecting objects found</returns>
        public static Path CreateFromModel(ICurve BeginWith, Model model, Projection projection, bool flatten)
        {
            UntypedSet usedObjects = new UntypedSet();
            ArrayList connectedObjects = new ArrayList();
            usedObjects.Add(BeginWith);
            IGeoObject go = BeginWith.Clone() as IGeoObject; ;
            go.UserData.Add("CADability.Path.Original", BeginWith);
            connectedObjects.Add(go);
            GeoPoint StartPoint = BeginWith.StartPoint;
            if (BeginWith.IsClosed)
            {
                // nix machen
            }
            else
            {
                GeoPoint lastEndPoint = BeginWith.EndPoint;
                do
                {
                    BoundingRect ext = new BoundingRect(projection.ProjectUnscaled(lastEndPoint), Precision.eps, Precision.eps);
                    GeoObjectList list = model.GetObjectsFromRect(ext, projection, null, PickMode.normal, null);
                    ICurve found = null;
                    for (int i = 0; i < list.Count; ++i)
                    {
                        ICurve crv = list[i] as ICurve;
                        if (crv != null && !usedObjects.Contains(crv))
                        {
                            if (Precision.IsEqual(crv.StartPoint, lastEndPoint))
                            {
                                found = crv.Clone();
                                go = found as IGeoObject;
                                go.UserData.Add("CADability.Path.Original", crv);
                                usedObjects.Add(crv);
                                connectedObjects.Add(found);
                                break;
                            }
                            else if (Precision.IsEqual(crv.EndPoint, lastEndPoint))
                            {
                                found = crv.Clone();
                                found.Reverse();
                                go = found as IGeoObject;
                                go.UserData.Add("CADability.Path.Original", crv);
                                usedObjects.Add(crv);
                                connectedObjects.Add(found);
                                break;
                            }
                        }
                    }
                    if (found != null)
                    {
                        lastEndPoint = found.EndPoint;
                    }
                    else
                    {
                        break;
                    }
                } while (true);
                // Rückwärtssuche
                lastEndPoint = BeginWith.StartPoint;
                do
                {
                    BoundingRect ext = new BoundingRect(projection.ProjectUnscaled(lastEndPoint), Precision.eps, Precision.eps);
                    GeoObjectList list = model.GetObjectsFromRect(ext, projection, null, PickMode.normal, null);
                    ICurve found = null;
                    for (int i = 0; i < list.Count; ++i)
                    {
                        ICurve crv = list[i] as ICurve;
                        if (crv != null && !usedObjects.Contains(crv))
                        {
                            if (Precision.IsEqual(crv.StartPoint, lastEndPoint))
                            {
                                found = crv.Clone();
                                found.Reverse();
                                go = found as IGeoObject;
                                go.UserData.Add("CADability.Path.Original", crv);
                                usedObjects.Add(crv);
                                connectedObjects.Insert(0, found);
                                break;
                            }
                            else if (Precision.IsEqual(crv.EndPoint, lastEndPoint))
                            {
                                found = crv.Clone();
                                go = found as IGeoObject;
                                go.UserData.Add("CADability.Path.Original", crv);
                                usedObjects.Add(crv);
                                connectedObjects.Insert(0, found);
                                break;
                            }
                        }
                    }
                    if (found != null)
                    {
                        lastEndPoint = found.StartPoint;
                    }
                    else
                    {
                        break;
                    }
                } while (true);
            }
            if (connectedObjects.Count > 1)
            {
                Path res = Path.Construct((ICurve[])connectedObjects.ToArray(typeof(ICurve)));
                if (flatten) res.Flatten(); // wichtig wg. Nulllinien entfernen, leider geht dabei UserData "CADability.Path.Original" verloren
                return res;
            }
            else
            {
                return null;
            }
        }
        /// <summary>
        /// Returns a Path containing the curve BeginWith and other curves from the Model.
        /// The curve BeginWith is checked at both ends to find connected objects. The search is stopped
        /// when there are nore more connected objects. The curves are clones and are tagged with <see cref="UserData"/> objects
        /// with the name key "CADability.Path.Original" and the original object as the value. 
        /// If flatten is true, UserData will be lost.
        /// </summary>
        /// <param name="BeginWith">Curve to begin with</param>
        /// <param name="model">the model to search for connecting curves</param>
        /// <param name="flatten">true: flatten the result, false: result not flattened</param>
        /// <returns>The path or null if no cennecting objects found</returns>
        public static Path CreateFromModel(ICurve BeginWith, Model model, bool flatten)
        {
            UntypedSet usedObjects = new UntypedSet();
            ArrayList connectedObjects = new ArrayList();
            usedObjects.Add(BeginWith);
            IGeoObject go = BeginWith.Clone() as IGeoObject; ;
            go.UserData.Add("CADability.Path.Original", BeginWith);
            connectedObjects.Add(go);
            GeoPoint StartPoint = BeginWith.StartPoint;
            if (BeginWith.IsClosed)
            {
                // nix machen
            }
            else
            {
                GeoPoint lastEndPoint = BeginWith.EndPoint;
                do
                {
                    BoundingCube ext = new BoundingCube(lastEndPoint);
                    IGeoObject[] list = model.octTree.GetObjectsFromBox(ext);
                    ICurve found = null;
                    for (int i = 0; i < list.Length; ++i)
                    {
                        ICurve crv = list[i] as ICurve;
                        if (crv != null && !usedObjects.Contains(crv))
                        {
                            if (Precision.IsEqual(crv.StartPoint, lastEndPoint))
                            {
                                found = crv.Clone();
                                go = found as IGeoObject;
                                go.UserData.Add("CADability.Path.Original", crv);
                                usedObjects.Add(crv);
                                connectedObjects.Add(found);
                                break;
                            }
                            else if (Precision.IsEqual(crv.EndPoint, lastEndPoint))
                            {
                                found = crv.Clone();
                                found.Reverse();
                                go = found as IGeoObject;
                                go.UserData.Add("CADability.Path.Original", crv);
                                usedObjects.Add(crv);
                                connectedObjects.Add(found);
                                break;
                            }
                        }
                    }
                    if (found != null)
                    {
                        lastEndPoint = found.EndPoint;
                    }
                    else
                    {
                        break;
                    }
                } while (true);
                // Rückwärtssuche
                lastEndPoint = BeginWith.StartPoint;
                do
                {
                    BoundingCube ext = new BoundingCube(lastEndPoint);
                    IGeoObject[] list = model.octTree.GetObjectsFromBox(ext);
                    ICurve found = null;
                    for (int i = 0; i < list.Length; ++i)
                    {
                        ICurve crv = list[i] as ICurve;
                        if (crv != null && !usedObjects.Contains(crv))
                        {
                            if (Precision.IsEqual(crv.StartPoint, lastEndPoint))
                            {
                                found = crv.Clone();
                                found.Reverse();
                                go = found as IGeoObject;
                                go.UserData.Add("CADability.Path.Original", crv);
                                usedObjects.Add(crv);
                                connectedObjects.Insert(0, found);
                                break;
                            }
                            else if (Precision.IsEqual(crv.EndPoint, lastEndPoint))
                            {
                                found = crv.Clone();
                                go = found as IGeoObject;
                                go.UserData.Add("CADability.Path.Original", crv);
                                usedObjects.Add(crv);
                                connectedObjects.Insert(0, found);
                                break;
                            }
                        }
                    }
                    if (found != null)
                    {
                        lastEndPoint = found.StartPoint;
                    }
                    else
                    {
                        break;
                    }
                } while (true);
            }
            if (connectedObjects.Count > 0)
            {
                Path res = Path.Construct((ICurve[])connectedObjects.ToArray(typeof(ICurve)));
                if (flatten) res.Flatten(); // wichtig wg. Nulllinien entfernen, leider geht dabei UserData "CADability.Path.Original" verloren
                return res;
            }
            else
            {
                return null;
            }
        }
        //public static Path[] CreateFromCurves(ICurve[] curves)
        //{
        //    BoundingCube bc = BoundingCube.EmptyBoundingCube;
        //    for (int i = 0; i < curves.Length; i++)
        //    {
        //        bc.MinMax(curves[i].GetExtent());
        //    }
        //    OctTree<ICurve>
        //}
        /// <summary>
        /// Takes a list of unsorted and unoriented geoobjects and tries to put them
        /// together to a connected path. All previously contained segments are removed.
        /// All objects of the list, that are used in this path will be removed
        /// from their owner (see <see cref="IGeoObject.Owner"/>). To avoid objects
        /// bee removed from their owner (e.g. <see cref="Model"/> or <see cref="Block"/>)
        /// use a list of cloned GeoObjects (<see cref="GeoObjectList.CloneObjects"/>).
        /// </summary>
        /// <param name="l">List of unsorted GeoObjects</param>
        /// <param name="moreThanOne">if true, the path must consist of more than one curve</param>
        /// <returns>success</returns>
        public bool Set(GeoObjectList l, bool moreThanOne, double maxgap)
        {
            ICurve[] curves = new ICurve[l.Count];
            int BestCurve = -1;
            for (int i = 0; i < l.Count; ++i)
            {
                curves[i] = l[i] as ICurve;
                if (BestCurve < 0 && curves[i] != null) BestCurve = i;
            }
            if (BestCurve < 0) return false;
            // curves kann null Einträge enthalten, uns interessieren nur die echten Kurven
            // die beiden Arrays fassen das Zwischenergebnis zusammen
            ArrayList OrderedCurves = new ArrayList(l.Count); // ICurve
            ArrayList Directions = new ArrayList(l.Count); // bool reverse
            OrderedCurves.Add(curves[BestCurve]);
            Directions.Add(false);
            GeoPoint EndPoint = curves[BestCurve].EndPoint;
            curves[BestCurve] = null; // entfernen
            do // vorwärts suchen
            {
                BestCurve = -1;
                double minDist = double.MaxValue;
                bool reverse = false;
                for (int i = 0; i < l.Count; ++i)
                {
                    ICurve c = curves[i];
                    if (c != null)
                    {
                        double d = Geometry.Dist(c.StartPoint, EndPoint);
                        if (d < minDist)
                        {
                            minDist = d;
                            BestCurve = i;
                            reverse = false;
                        }
                        d = Geometry.Dist(c.EndPoint, EndPoint);
                        if (d < minDist)
                        {
                            minDist = d;
                            BestCurve = i;
                            reverse = true;
                        }
                    }
                }
                if (BestCurve >= 0 && minDist < maxgap)
                {
                    OrderedCurves.Add(curves[BestCurve]);
                    Directions.Add(reverse);
                    if (reverse) EndPoint = curves[BestCurve].StartPoint;
                    else EndPoint = curves[BestCurve].EndPoint;
                    curves[BestCurve] = null; // entfernen
                }
                else
                {
                    BestCurve = -1; // Abbruch
                }
            }
            while (BestCurve >= 0);
            EndPoint = (OrderedCurves[0] as ICurve).StartPoint; // Anfang immer richtig rum
            do // rückwärts suchen
            {
                BestCurve = -1;
                double minDist = double.MaxValue;
                bool reverse = false;
                for (int i = 0; i < l.Count; ++i)
                {
                    ICurve c = curves[i];
                    if (c != null)
                    {
                        double d = Geometry.Dist(c.StartPoint, EndPoint);
                        if (d < minDist)
                        {
                            minDist = d;
                            BestCurve = i;
                            reverse = true;
                        }
                        d = Geometry.Dist(c.EndPoint, EndPoint);
                        if (d < minDist)
                        {
                            minDist = d;
                            BestCurve = i;
                            reverse = false;
                        }
                    }
                }
                if (BestCurve >= 0 && minDist < maxgap)
                {
                    OrderedCurves.Insert(0, curves[BestCurve]);
                    Directions.Insert(0, reverse);
                    if (reverse) EndPoint = curves[BestCurve].EndPoint;
                    else EndPoint = curves[BestCurve].StartPoint;
                    curves[BestCurve] = null; // entfernen
                }
                else
                {
                    BestCurve = -1; // Abbruch
                }
            }
            while (BestCurve >= 0);
            if (OrderedCurves.Count == 0) return false;
            if (OrderedCurves.Count > 1)
            {
                for (int i = OrderedCurves.Count - 1; i >= 0; --i)
                {   // bei mehreren Kurven darf keine geschlossene dabei sein, also keine innere Schleife
                    // sonst gibt es ein Problem, wenn man gemeinsamen Punkt ändern will
                    if ((OrderedCurves[i] as ICurve).IsClosed) OrderedCurves.RemoveAt(i);
                }
            }
            if (OrderedCurves.Count == 0 || (OrderedCurves.Count == 1 && moreThanOne)) return false;
            for (int i = 0; i < OrderedCurves.Count; ++i)
            {
                ICurve c = OrderedCurves[i] as ICurve;
                if ((bool)Directions[i]) c.Reverse();
                if (i > 0) c.StartPoint = this.subCurves[i - 1].EndPoint;
                this.Add(c);
                // TODO: wo ist das Remove, wo ist Changing???
                // Add macht das mit dem Owner
            }
            this.CopyAttributes(OrderedCurves[0] as IGeoObject);
            Recalc();
            return true;
        }
        /// <summary>
        /// Makes this path represent the given list if all objects in that list are properly
        /// oriented and connected. If the objects do not connect, false will be returned and this
        /// path remains unchanged.
        /// </summary>
        /// <param name="connectedCurves">new contents of this path</param>
        /// <returns>true on success, false on failure</returns>
        public bool Set(ICurve[] connectedCurves)
        {
            for (int i = 0; i < connectedCurves.Length - 1; ++i)
            {
                if (!Precision.IsEqual(connectedCurves[i].EndPoint, connectedCurves[i + 1].StartPoint))
                {
                    // DEBUG!!!
                    // double d = Geometry.Dist(connectedCurves[i].EndPoint, connectedCurves[i + 1].StartPoint);
                    // return false;
                    try
                    {
                        connectedCurves[i].EndPoint = connectedCurves[i + 1].StartPoint;
                    }
                    catch (Exception e)
                    {   // andersrum probieren
                        if (e is ThreadAbortException) throw (e);
                        try
                        {
                            connectedCurves[i + 1].StartPoint = connectedCurves[i].EndPoint;
                        }
                        catch (Exception) { } // geht halt nicht, egal, eigentlich Linie einfügen
                    }
                }
            }
            using (new Changing(this))
            {
                if (subCurves != null)
                {
                    for (int i = 0; i < subCurves.Length; ++i)
                    {
                        IGeoObject go = subCurves[i] as IGeoObject;
                        if (go != null)
                        {
                            go.Owner = null;
                            go.DidChangeEvent -= new ChangeDelegate(SubCurveDidChange);
                            go.WillChangeEvent -= new ChangeDelegate(SubCurveWillChange);
                        }
                    }
                }
                subCurves = (ICurve[])connectedCurves.Clone();
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    IGeoObject go = subCurves[i] as IGeoObject;
                    if (go != null)
                    {
                        if (go.Owner != null) go.Owner.Remove(go);
                        go.Owner = this;
                        go.DidChangeEvent += new ChangeDelegate(SubCurveDidChange);
                        go.WillChangeEvent += new ChangeDelegate(SubCurveWillChange);
                    }
                }
                Recalc();
            }
            return true;
        }
        private void AddFlattend(ArrayList addTo, ICurve toAdd)
        {
            if (toAdd.IsComposed)
            {
                ICurve[] tmp = toAdd.SubCurves;
                if (tmp.Length > 0)
                {
                    (tmp[0] as IGeoObject).UserData.CloneFrom((toAdd as IGeoObject).UserData);
                }
                AddFlattend(addTo, tmp);
            }
            else
            {
                // möglicherweise ein Problem wenn mehrere Kleinstlinien aufeinanderfolgen, so dass die Summe der
                // Abstaände wieder größer als Precision.eps werden.
                if (toAdd.Length > Precision.eps) addTo.Add(toAdd);
            }
        }
        private void AddFlattend(ArrayList addTo, ICurve[] toAdd)
        {
            for (int i = 0; i < toAdd.Length; ++i)
            {
                AddFlattend(addTo, toAdd[i]);
            }
        }
        internal void Adjust()
        {
            for (int i = 1; i < subCurves.Length; ++i)
            {
                subCurves[i].StartPoint = subCurves[i - 1].EndPoint;
            }
            if (IsClosed && subCurves.Length > 1)
            {
                subCurves[0].StartPoint = subCurves[subCurves.Length - 1].EndPoint;
            }
        }
        /// <summary>
        /// Flattens this path. All subcurves that are composed of simple curves
        /// are decomposed into simpler curves (e.g. a polyline is decomposed into lines)
        /// </summary>
        public void Flatten()
        {
            ArrayList newSubCurves = new ArrayList();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                AddFlattend(newSubCurves, subCurves[i]);
            }
            Set((ICurve[])newSubCurves.ToArray(typeof(ICurve)));
        }
        /// <summary>
        /// Adds a curve to this path. It is checked whether the curve to add can be connected
        /// to the start or endpoint of this path. If necessary the curve will be reversed. If
        /// it cannot be connected false will be returned.
        /// </summary>
        /// <param name="ToAdd">curve to add</param>
        /// <returns>true on success, false on failure</returns>
        public bool Add(ICurve ToAdd)
        {
            bool insertAtBeginning = false;
            if (subCurves != null && subCurves.Length > 0)
            {	// Zusammenhang testen
                if (!Precision.IsEqual(EndPoint, ToAdd.StartPoint)) // passt schon
                {
                    if (Precision.IsEqual(EndPoint, ToAdd.EndPoint))
                    {	// am Ende umgedreht
                        ToAdd.Reverse();
                    }
                    else if (Precision.IsEqual(StartPoint, ToAdd.EndPoint))
                    {	// am Anfang
                        insertAtBeginning = true;
                    }
                    else if (Precision.IsEqual(StartPoint, ToAdd.StartPoint))
                    {	// am Anfang, umgedreht
                        ToAdd.Reverse();
                        insertAtBeginning = true;
                    }
                    else
                    {	// passt garnicht dran
                        return false;
                    }
                }
            }
            using (new Changing(this))
            {
                length = null; // ungültig machen
                inPlane = null;
                if (subCurves != null)
                {
                    ArrayList al = new ArrayList(subCurves);
                    if (insertAtBeginning)
                    {
                        al.Insert(0, ToAdd);
                    }
                    else
                    {
                        al.Add(ToAdd);
                    }
                    subCurves = (ICurve[])al.ToArray(typeof(ICurve));
                }
                else
                {
                    subCurves = new ICurve[] { ToAdd };
                }
                IGeoObject go = ToAdd as IGeoObject;
                if (go != null)
                {
                    if (go.Owner != null) go.Owner.Remove(go);
                    go.Owner = this;
                    go.DidChangeEvent += new ChangeDelegate(SubCurveDidChange);
                    go.WillChangeEvent += new ChangeDelegate(SubCurveWillChange);
                }
            }
            return true;
        }
        /// <summary>
        /// Removes all subcurves, the Path will be empty. Use this before modifying
        /// contained objects of this path because it is not allowed to mdify an object
        /// in a path in a way that would compromise the consitence of the path
        /// </summary>
        public void Clear()
        {
            Set(new ICurve[] { });
        }
        public bool Remove(ICurve toRemove)
        {
            int ind = -1;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                if (subCurves[i] == toRemove)
                {
                    ind = i;
                    break;
                }
            }
            if (ind >= 0)
            {
                IGeoObject go = toRemove as IGeoObject;
                if (go != null)
                {
                    go.DidChangeEvent -= new ChangeDelegate(SubCurveDidChange);
                    go.WillChangeEvent -= new ChangeDelegate(SubCurveWillChange);
                }

                ArrayList al = new ArrayList(subCurves.Length - 1);
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    if (i != ind) al.Add(subCurves[i]);
                }
                return Set((ICurve[])al.ToArray(typeof(ICurve)));
            }
            return false;
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Decompose ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoObjectList Decompose()
        {
            ICurve[] tmpCurve = this.Curves;
            GeoObjectList tmpGeoObject = new GeoObjectList();
            for (int i = 0; i < tmpCurve.Length; i++)
            {
                tmpGeoObject.Add((tmpCurve[i] as IGeoObject).Clone());
                tmpGeoObject[i].CopyAttributes(this);
            }
            return tmpGeoObject;
        }
        /// <summary>
        /// Removes all segments that are shorter than maxLengthToRemove. Connects the ramaining segments.
        /// </summary>
        /// <param name="maxLengthToRemove">Max. length of subcurves to remove</param>
        public void RemoveShortSegments(double maxLengthToRemove)
        {
            List<ICurve> newSubCurves = new List<ICurve>();
            for (int i = 0; i < subCurves.Length; i++)
            {
                if (subCurves[i].Length > maxLengthToRemove) newSubCurves.Add(subCurves[i]);
            }
            Set(newSubCurves.ToArray());
        }

        /// <summary>
        /// Assumes both this and toThis are closed Paths with the same number of segments. Changes this Path
        /// to start with the segment where the distance of corresponding vertices is minimal. The geometry of 
        /// this curve will not be changed
        /// </summary>
        /// <param name="toThis">adapt to this path</param>
        /// <returns>true, if condition is satisfied</returns>
        public bool BestCyclicalPosition(Path toThis)
        {
            if (!IsClosed) return false;
            if (!toThis.IsClosed) return false;
            if (this.subCurves.Length != toThis.subCurves.Length) return false;
            int bestStartIndex = -1;
            double minDist = double.MaxValue;
            for (int i = 0; i < subCurves.Length; i++)
            {
                double sum = 0.0;
                for (int j = 0; j < subCurves.Length; j++)
                {
                    sum += Geometry.Dist(subCurves[(j + i) % subCurves.Length].StartPoint, toThis.subCurves[j].StartPoint);
                }
                if (sum < minDist)
                {
                    minDist = sum;
                    bestStartIndex = i;
                }
            }
            if (bestStartIndex > 0)
            {   // bei 0 bleibt ja alles unverändert
                ICurve[] newSubCurves = new ICurve[subCurves.Length];
                Array.Copy(subCurves, bestStartIndex, newSubCurves, 0, subCurves.Length - bestStartIndex);
                Array.Copy(subCurves, 0, newSubCurves, subCurves.Length - bestStartIndex, bestStartIndex);
                using (new Changing(this))
                {
                    subCurves = newSubCurves;
                    Recalc();
                }
            }
            return true;
        }
        public void ChangeCyclicalStart(int newStartCurve)
        {
            ICurve[] newSubCurves = new ICurve[subCurves.Length];
            Array.Copy(subCurves, newStartCurve, newSubCurves, 0, subCurves.Length - newStartCurve);
            Array.Copy(subCurves, 0, newSubCurves, subCurves.Length - newStartCurve, newStartCurve);
            using (new Changing(this))
            {
                subCurves = newSubCurves;
                Recalc();
            }
        }
        public void SetPoint(int index, GeoPoint newValue, ModificationMode mode)
        {
            using (new Changing(this))
            {   // der index des Punktes ist gleichzeitig der index der Kurve, deren Startpunkt verändert wird
                if (index == -1) index = subCurves.Length;
                if (index < 0 || index > subCurves.Length) return; // subCurves.Length ist Endpunkt der letzten
                while (index < 0) index += subCurves.Length;
                while (index > subCurves.Length) index -= subCurves.Length;

                if (index == 0 && !IsClosed)
                {
                    subCurves[0].StartPoint = newValue;
                    return;
                }
                if (index == subCurves.Length && !IsClosed)
                {
                    subCurves[subCurves.Length - 1].EndPoint = newValue;
                    return;
                }
                int last = index - 1;
                if (last < 0) last = subCurves.Length - 1;
                if (index == subCurves.Length) index = 0;
                ICurve scl = subCurves[last].Clone();
                ICurve sci = subCurves[index].Clone();

                GeoPoint p0 = subCurves[last].StartPoint;
                GeoPoint p1 = subCurves[index].StartPoint;
                GeoPoint p2 = subCurves[index].EndPoint;
                ModOp fit = ModOp.Identity;
                ModOp fit1 = ModOp.Identity;
                ModOp fit2 = ModOp.Identity;
                bool defined = false;
                if (subCurves[last] is Ellipse || subCurves[index] is Ellipse)
                {   // eine der ggf. beiden Ellipsen soll auch die andere Tangente beibehalten
                    //Ellipse e;
                    //if (subCurves[last] is Ellipse) e = subCurves[last] as Ellipse;
                    //else e = subCurves[index] as Ellipse;
                    //GeoVector2D dir1 = e.Plane.Project(e.StartDirection);
                    //GeoVector2D dir2 = e.Plane.Project(e.EndDirection);
                    //GeoPoint2D sp = e.Plane.Project(e.StartPoint);
                    //GeoPoint2D ep = e.Plane.Project(e.EndPoint);
                    //GeoPoint2D pp4;
                    //if (Geometry.IntersectLL(sp, dir1, ep, dir2, out pp4))
                    //{
                    //    GeoPoint p4 = e.Plane.ToGlobal(pp4);
                    //    try
                    //    {
                    //        fit = ModOp.Fit(new GeoPoint[] { p0, p1, p2, p4 }, new GeoPoint[] { p0, newValue, p2, p4 }, true);
                    //        defined = true;
                    //    }
                    //    catch (ModOpException)
                    //    {   // nicht defined
                    //    }
                    //}
                }
                if (!defined)
                {
                    try
                    {
                        fit1 = ModOp.Fit(new GeoPoint[] { p0, p1 }, new GeoPoint[] { p0, newValue }, true);
                        fit2 = ModOp.Fit(new GeoPoint[] { p1, p2 }, new GeoPoint[] { newValue, p2 }, true);
                        defined = true;
                    }
                    catch (ModOpException)
                    {
                    }
                }
                // so werden aus Kreisen Ellipsen, was von Aerotec nicht gewünscht
                // sieht aber besser aus, beide Kurven werden mit der selben "fit" modifiziert, ggf. per Settings steuern
                //if (!defined)
                //{
                //    try
                //    {
                //        fit = ModOp.Fit(new GeoPoint[] { p0, p1, p2 }, new GeoPoint[] { p0, newValue, p2 }, true);
                //        defined = true;
                //    }
                //    catch (ModOpException)
                //    {
                //    }
                //}
                if (defined)
                {
                    (subCurves[last] as IGeoObject).Modify(fit1);
                    (subCurves[index] as IGeoObject).Modify(fit2);
                }
                else
                {
                    subCurves[last].EndPoint = newValue;
                    subCurves[index].StartPoint = newValue;
                }
                if (!consistent)
                {
                    (subCurves[last] as IGeoObject).CopyGeometry(scl as IGeoObject);
                    (subCurves[index] as IGeoObject).CopyGeometry(sci as IGeoObject);
                    //subCurves[last].StartPoint = p0;
                    //subCurves[last].EndPoint = p1;
                    //subCurves[index].StartPoint = p1;
                    //subCurves[index].EndPoint = p2;
                }
            }
        }
        public void SetPoint(int indexEndPoint, int indexStartPoint, GeoPoint newValue)
        {
            // indexStartPoint muss um 1 größer sein als indexEndPoint
            // die können auch jeweils -1 sein, wenn am Anfang oder am Ende geändert wird
            // diese Logik könnte auch hierher verfrachtet werden...
            // um es konsistent zu halten werden hier beide Änderungen in einem Aufwasch gemacht
            using (new Changing(this))
            {
                if (indexEndPoint >= 0) subCurves[indexEndPoint].EndPoint = newValue;
                if (indexStartPoint >= 0) subCurves[indexStartPoint].StartPoint = newValue;
            }
        }
        /// <summary>
        /// Splits the curve of this path that contains the provided position. If position is exactely 
        /// on the connection of two subcurves, the path remains unchanged. Otherwise the curve containing the
        /// position is split and both curves are added to the path
        /// </summary>
        /// <param name="position">Where to split, must be inbetween 0 and 1</param>
        public void InsertPoint(double position)
        {
            double pos = position * subCurves.Length;
            int ind = (int)Math.Floor(pos);
            if (ind < 0) ind = 0;
            if (ind >= subCurves.Length) ind = subCurves.Length - 1;
            double par = pos - ind;
            if (par == 0.0 || par == 1.0) return;
            ICurve[] splitted = subCurves[ind].Split(par);
            if (splitted.Length == 2)
            {
                using (new Changing(this))
                {
                    List<ICurve> tmp = new List<ICurve>(subCurves);
                    tmp.RemoveAt(ind);
                    tmp.Insert(ind, splitted[1]);
                    tmp.Insert(ind, splitted[0]);
                    IGeoObject go = splitted[0] as IGeoObject;
                    if (go != null)
                    {
                        if (go.Owner != null) go.Owner.Remove(go);
                        go.Owner = this;
                        go.DidChangeEvent += new ChangeDelegate(SubCurveDidChange);
                        go.WillChangeEvent += new ChangeDelegate(SubCurveWillChange);
                    }
                    go = splitted[1] as IGeoObject;
                    if (go != null)
                    {
                        if (go.Owner != null) go.Owner.Remove(go);
                        go.Owner = this;
                        go.DidChangeEvent += new ChangeDelegate(SubCurveDidChange);
                        go.WillChangeEvent += new ChangeDelegate(SubCurveWillChange);
                    }
                    subCurves = tmp.ToArray();
                }
            }
        }
        internal void CyclicalPermutation(GeoPoint pointOnSegment, bool setStartSegment)
        {
            if (!IsClosed) return;
            int n = (int)Math.Floor(PositionOf(pointOnSegment) * subCurves.Length);
            if (!setStartSegment)
            {   // wenn dieses Segment das Endsegment sein soll, dann ist das nächste das Startsegment
                ++n;
                if (n >= subCurves.Length) n = 0;
            }
            ICurve[] newSubCurves = new ICurve[subCurves.Length];
            Array.Copy(subCurves, n, newSubCurves, 0, subCurves.Length - n);
            Array.Copy(subCurves, 0, newSubCurves, subCurves.Length - n, n);
            using (new Changing(this))
            {
                subCurves = newSubCurves;
                Recalc();
            }
        }
        internal void CyclicalPermutation(int startSegment)
        {
            if (!IsClosed) return;
            ICurve[] newSubCurves = new ICurve[subCurves.Length];
            Array.Copy(subCurves, startSegment, newSubCurves, 0, subCurves.Length - startSegment);
            Array.Copy(subCurves, 0, newSubCurves, subCurves.Length - startSegment, startSegment);
            using (new Changing(this))
            {
                subCurves = newSubCurves;
                Recalc();
            }
        }
        /// <value>
        /// Gets the number uf subcurves in this path.
        /// </value>
        public int Count
        {
            get
            {
                if (subCurves == null) return 0;
                return subCurves.Length;
            }
        }
        /// <summary>
        /// Returns the i-th curve of the path. Do not modify this curve or the path
        /// will be in an invalid state. For the number of curves see <see cref="CurveCount"/>.
        /// </summary>
        /// <param name="Index">Index of the desired curve</param>
        /// <returns>The curve with the given index</returns>
        public ICurve Curve(int Index)
        {
            return subCurves[Index];
        }
        /// <summary>
        /// Returns a cloned array of the curves of this path. Do not modify the
        /// curves or the path will be in an invalid state.
        /// </summary>
        public ICurve[] Curves
        {
            get
            {
                return (ICurve[])subCurves.Clone();
            }
        }
        /// <summary>
        /// Returns the number of curves in the Path
        /// </summary>
        public int CurveCount
        {
            get
            {
                if (subCurves != null)
                    return subCurves.Length;
                return 0;
            }
        }
        public GeoPoint[] Vertices
        {
            get
            {
                List<GeoPoint> res = new List<GeoPoint>();
                for (int i = 0; i < subCurves.Length; i++)
                {
                    res.Add(subCurves[i].StartPoint);
                }
                if (!IsClosed) res.Add(subCurves[subCurves.Length - 1].EndPoint);
                return res.ToArray();
            }
        }
        #region IGeoObjectImpl
        public override Style.EDefaultFor PreferredStyle
        {
            get
            {
                return Style.EDefaultFor.Curves;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyPath(this, Frame);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            if (subCurves != null)
            {
                ICurve[] clonedCurves = new ICurve[subCurves.Length];
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    clonedCurves[i] = subCurves[i].Clone();
                }
                Path res = Construct();
                res.Set(clonedCurves);
                res.CopyAttributes(this);
                return res;
            }
            else
            {
                Path res = Construct(); // leerer Pfad
                res.CopyAttributes(this);
                return res;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            using (new Changing(this))
            {
                Path source = ToCopyFrom as Path;
                if (subCurves != null)
                {	// alten Inhalt abmelden
                    for (int i = 0; i < subCurves.Length; ++i)
                    {
                        IGeoObject go = subCurves[i] as IGeoObject;
                        if (go != null)
                        {
                            go.Owner = null;
                            go.DidChangeEvent -= new ChangeDelegate(SubCurveDidChange);
                            go.WillChangeEvent -= new ChangeDelegate(SubCurveWillChange);
                        }
                    }
                }
                if (source.subCurves != null)
                {
                    subCurves = new ICurve[source.subCurves.Length];
                    for (int i = 0; i < source.subCurves.Length; ++i)
                    {
                        subCurves[i] = source.subCurves[i].Clone();
                        IGeoObject go = subCurves[i] as IGeoObject;
                        if (go != null)
                        {
                            if (go.Owner != null) go.Owner.Remove(go);
                            go.Owner = this;
                            go.DidChangeEvent += new ChangeDelegate(SubCurveDidChange);
                            go.WillChangeEvent += new ChangeDelegate(SubCurveWillChange);
                        }
                    }
                    isClosed = IsClosed;
                }
                else
                {
                    subCurves = null; // besser immer mit einer leeren subCurve arbeiten, dann muss man nicht überall fragen...
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this))
            {
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    (subCurves[i] as IGeoObject).Modify(m);
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
        /// </summary>
        /// <param name="spf"></param>
        public override void FindSnapPoint(SnapPointFinder spf)
        {	// einfach an die enthaltenen Objekte weitergeben, somit keine Probleme
            // mit Mittelpunkt, Tangenten u.s.w.
            if (!spf.Accept(this)) return;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                IGeoObject go = subCurves[i] as IGeoObject;
                if (go != null) go.FindSnapPoint(spf);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                res.MinMax((subCurves[i] as IGeoObject).GetBoundingCube());
            }
            return res;
        }
        public delegate bool PaintTo3DDelegate(Path toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            if (paintTo3D.SelectMode)
            {
                // paintTo3D.SetColor(paintTo3D.SelectColor);
            }
            else
            {
                if (colorDef != null) paintTo3D.SetColor(colorDef.Color);
            }
            for (int i = 0; i < subCurves.Length; ++i)
            {
                (subCurves[i] as IGeoObjectImpl).PaintTo3D(paintTo3D);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            for (int i = 0; i < subCurves.Length; ++i)
            {
                (subCurves[i] as IGeoObject).PrepareDisplayList(precision);
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
            QuadTreeCollection res = new QuadTreeCollection(this, projection);
            // warum stand hier i=1 statt i=0 ? (3.4.07)
            for (int i = 0; i < subCurves.Length; ++i)
            {
                res.Add((subCurves[i] as IGeoObject).GetQuadTreeItem(projection, extentPrecision));
            }
            return res;
        }
        public override Layer Layer
        {
            get
            {
                return base.Layer;
            }
            set
            {
                using (new ChangingAttribute(this, "Layer", base.Layer))
                {
                    base.Layer = value;
                    if (subCurves != null)
                    {
                        for (int i = 0; i < subCurves.Length; ++i)
                        {
                            (subCurves[i] as IGeoObject).Layer = value;
                        }
                    }
                }
            }
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
            for (int i = 0; i < subCurves.Length; ++i)
            {
                if ((subCurves[i] as IOctTreeInsertable).HitTest(ref cube, precision)) return true;
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
            ICurve2D c2d = (this as ICurve).GetProjectedCurve(projection.ProjectionPlane);
            if (onlyInside) return c2d.GetExtent() <= rect;
            else return c2d.HitTest(ref rect, false);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            if (onlyInside)
            {
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    if (!(subCurves[i] as IGeoObject).HitTest(area, true)) return false;
                }
                return true;
            }
            else
            {
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    if ((subCurves[i] as IGeoObject).HitTest(area, false)) return true;
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
        {   // noch nicht getestet
            if (planarState == PlanarState.Planar)
            {
                GeoPoint p = ((Plane)inPlane).Intersect(fromHere, direction);
                return Geometry.LinePar(fromHere, direction, p); // nicht getestet ob Polyline auch getroffen
            }
            else
            {
                double res = double.MaxValue;
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    double d = (subCurves[i] as IOctTreeInsertable).Position(fromHere, direction, precision);
                    if (d < res) res = d;
                }
                return res;
            }
        }
        #endregion
        #region ICurve Members
        public GeoPoint StartPoint
        {
            get
            {
                if (subCurves.Length == 0) throw new PathException(PathException.ExceptionType.PathIsEmtpy, "StartPoint: Path is empty");
                return subCurves[0].StartPoint;
            }
            set
            {
                if (subCurves.Length == 0) throw new PathException(PathException.ExceptionType.PathIsEmtpy, "StartPoint: Path is empty");
                subCurves[0].StartPoint = value;
            }
        }
        public GeoPoint EndPoint
        {
            get
            {
                if (subCurves.Length == 0) throw new PathException(PathException.ExceptionType.PathIsEmtpy, "EndPoint: Path is empty");
                return subCurves[subCurves.Length - 1].EndPoint;
            }
            set
            {
                if (subCurves.Length == 0) throw new PathException(PathException.ExceptionType.PathIsEmtpy, "EndPoint: Path is empty");
                subCurves[subCurves.Length - 1].EndPoint = value;
            }
        }
        public GeoVector StartDirection
        {
            get
            {
                if (subCurves.Length == 0) throw new PathException(PathException.ExceptionType.PathIsEmtpy, "StartDirection: Path is empty");
                return subCurves[0].StartDirection;
            }
        }
        public GeoVector EndDirection
        {
            get
            {
                if (subCurves.Length == 0) throw new PathException(PathException.ExceptionType.PathIsEmtpy, "EndDirection: Path is empty");
                return subCurves[subCurves.Length - 1].EndDirection;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.DirectionAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public GeoVector DirectionAt(double Position)
        {
            double pos = Position * subCurves.Length;
            int ind = (int)Math.Floor(pos);
            if (ind < 0) ind = 0;
            if (ind >= subCurves.Length) ind = subCurves.Length - 1;
            return Curve(ind).DirectionAt(pos - ind);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PointAt (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public GeoPoint PointAt(double Position)
        {
            // der Lauf der Parameter ist so zu interpretieren: Der Wert liegt zwischen 0 und 1
            // bei n Segmenten deckt jedes Segment einen Bereich von 1/n ab
            double pos = Position * subCurves.Length;
            int ind = (int)Math.Floor(pos);
            if (ind < 0) ind = 0;
            if (ind >= subCurves.Length) ind = subCurves.Length - 1;
            //			if (ind<0) return StartPoint;
            //			else if (ind>=subCurves.Length) return EndPoint;
            //			else return Curve(ind).PointAt(pos-ind);
            // gefordert ist, dass auch Punkte vor oder nach der Kurve geliefert werden 
            // (z.B. wg. Verlängern). Deshalb nicht einfach Start- bzw. Endpunkt liefern
            return Curve(ind).PointAt(pos - ind);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint, Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="pl"></param>
        /// <returns></returns>
        public double PositionOf(GeoPoint p, Plane pl)
        {
            throw new NotImplementedException("Path. PositionOf");
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint, double)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <param name="prefer"></param>
        /// <returns></returns>
        public double PositionOf(GeoPoint p, double prefer)
        {
            if (subCurves.Length == 1) return subCurves[0].PositionOf(p, prefer);
            else return PositionOf(p);
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.PositionOf (GeoPoint)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double PositionOf(GeoPoint p)
        {
            double res = -1.0;
            double mindist = double.MaxValue;
            // solange der Punkt auf einem Segment liegt, gibt es kein Problem. Ist der
            // Punkt allerdings außerhalb, so kann er u.U. sowohl vor dem Anfang als auch nach
            // dem Ende des Pfades liegen (z.B. beim Halbkreis). Wir lassen nur die Seite zu,
            // die dem gesuchten Punkt näher liegt, sonst gibt es bei solchen Halbkreisförmigen
            // Pfaden Probleme.
            bool closerToStartPoint = Geometry.Dist(StartPoint, p) < Geometry.Dist(EndPoint, p);
            for (int i = 0; i < subCurves.Length; ++i)
            {
                double pos = Curve(i).PositionOf(p);
                if ((pos >= -1e-10 && pos <= 1 + 1e-10) || // innerhalb mit festem epsilon, da auf 0..1 normiert
                    (closerToStartPoint && i == 0 && pos < 0.0) || // vor dem Anfang
                    (!closerToStartPoint && i == subCurves.Length - 1 && pos > 1.0)) // nach dem Ende
                {
                    GeoPoint pp = Curve(i).PointAt(pos);
                    double d = Geometry.Dist(p, pp);
                    if (d < mindist)
                    {
                        res = (i + pos) / subCurves.Length;
                        mindist = d;
                    }
                }
            }
            // es kann sein, dass der Punkt nahe einem Eck liegt, aber auf keiner der beiden
            // angrenzenden Kurven
            for (int i = 0; i < subCurves.Length; ++i)
            {
                if (i == 0 && Geometry.Dist(Curve(i).StartPoint, p) < mindist)
                {
                    mindist = Geometry.Dist(Curve(i).StartPoint, p);
                    res = 0.0;
                }
                if (Geometry.Dist(Curve(i).EndPoint, p) < mindist)
                {
                    mindist = Geometry.Dist(Curve(i).EndPoint, p);
                    res = (double)(i + 1) / (double)subCurves.Length;
                }
            }
            return res;
        }
        void ICurve.Reverse()
        {
            using (new Changing(this, typeof(ICurve), "Reverse", new object[0]))
            {
#if DEBUG
                isReversing = true;
#endif
                Array.Reverse(subCurves);
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    subCurves[i].Reverse();
                }
#if DEBUG
                isReversing = false;
#endif
            }
        }
        public double Length
        {
            get
            {
                if (subCurves == null) return 0.0;
                double l = 0.0;
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    l += subCurves[i].Length;
                }
                return l;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Split (double)"/>
        /// </summary>
        /// <param name="Position"></param>
        /// <returns></returns>
        public ICurve[] Split(double Position)
        {
            if (Position <= 0.0 || Position >= 1.0)
                return new ICurve[1] { this.Clone() as ICurve };
            double p = Position * subCurves.Length; // auf 0..n normiert
            int splitindex = (int)Math.Floor(p);
            if (splitindex >= subCurves.Length || splitindex < 0)
            {
                return new ICurve[] { this.Clone() as ICurve }; // am Anfang oder Ende splitten
            }
            ICurve[] splitted = Curve(splitindex).Split(p - splitindex);
            if (splitted.Length < 2 || (p - splitindex) < 1e-10)
            {	// die Bedingung reicht nicht, wenn Schnitt genau am
                // Ende des Segments splitindex
                ICurve[] cv1 = new ICurve[splitindex];
                ICurve[] cv2 = new ICurve[subCurves.Length - splitindex];
                for (int i = 0; i < splitindex; ++i)
                {
                    cv1[i] = Curve(i).Clone();
                }
                for (int i = splitindex; i < subCurves.Length; ++i)
                {
                    cv2[i - splitindex] = Curve(i).Clone();
                }
                if (cv1.Length > 0 && cv2.Length > 0)
                {
                    return new ICurve[] { Path.Construct(cv1), Path.Construct(cv2) };
                }
                else if (cv1.Length > 0)
                {
                    return new ICurve[] { Path.Construct(cv1) };
                }
                else if (cv2.Length > 0)
                {
                    return new ICurve[] { Path.Construct(cv2) };
                }
            }

            GeoPoint splitPoint = PointAt(Position);
            ICurve[] c1 = new ICurve[splitindex + 1];
            for (int i = 0; i < splitindex; ++i)
            {
                c1[i] = Curve(i).Clone();
            }
            c1[splitindex] = splitted[0];
            Path p1 = Path.Construct(c1);
            ICurve[] c2 = new ICurve[subCurves.Length - splitindex];
            for (int i = splitindex + 1; i < subCurves.Length; ++i)
            {
                c2[i - splitindex] = Curve(i).Clone();
            }
            c2[0] = splitted[1];
            Path p2 = Path.Construct(c2);
            return new ICurve[] { p1, p2 };
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Split (double, double)"/>
        /// </summary>
        /// <param name="Position1"></param>
        /// <param name="Position2"></param>
        /// <returns></returns>
        public ICurve[] Split(double Position1, double Position2)
        {
            if (!IsClosed) return new ICurve[] { };
            GeoPoint p2 = PointAt(Position2);
            if (Position1 > Position2)
            {
                double t = Position2;
                Position2 = Position1;
                Position1 = t;
            }
            ICurve[] tmp = Split(Position1);
            // es gibt zwei Path Objekte, der 2. Punkt ist auf dem 2. Objekt
            if (tmp.Length == 2)
            {
                Path pp = tmp[1] as Path;
                if (pp != null)
                {
                    ICurve[] tmp1 = pp.Split(pp.PositionOf(p2));
                    if (tmp1.Length == 2)
                    {
                        Path pp1 = tmp1[0] as Path; // des innere Stück
                        Path pp2 = tmp1[1] as Path; // hinteres Stück, dazu noch tmp[0]
                        Path pp3 = tmp[0] as Path;
                        pp2.Add(pp3);
                        return new ICurve[] { pp1, pp2 };
                    }
                }
            }
            return new ICurve[] { }; // hat nicht geklappt
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.Trim (double, double)"/>
        /// </summary>
        /// <param name="StartPos"></param>
        /// <param name="EndPos"></param>
        public void Trim(double StartPos, double EndPos)
        {
            bool reverse = false;
            if (StartPos > EndPos)
            {
                reverse = true;
                double t = EndPos;
                EndPos = StartPos;
                StartPos = t;
            }
            if (StartPos < 0.0)
            {
                // nach vorne verlängern
                double pos = StartPos * subCurves.Length;
                Curve(0).Trim(pos, 1.0);
                return; // und was ist mit reverse?
            }
            if (EndPos > 1.0)
            {
                // nach hinten verlängern
                double pos = EndPos * subCurves.Length;
                int ind = subCurves.Length - 1;
                Curve(ind).Trim(0.0, pos - ind);
                return; // und was ist mit reverse?
            }
            if (StartPos == 0.0)
            {
                ICurve[] res = Split(EndPos);
                if (res.Length == 2)
                {
                    ICurve[] tmp = (res[0] as Path).Curves;
                    // Curves liefert zwar ein geklontes Array, aber mit denselben Kurven
                    // deshalb noch clonen
                    for (int i = 0; i < tmp.Length; ++i)
                    {
                        tmp[i] = tmp[i].Clone();
                    }
                    Set(tmp);
                }
            }
            else if (EndPos == 1.0)
            {
                ICurve[] res = Split(StartPos);
                if (res.Length == 2)
                {
                    ICurve[] tmp = (res[1] as Path).Curves;
                    // Curves liefert zwar ein geklontes Array, aber mit denselben Kurven
                    // deshalb noch clonen
                    for (int i = 0; i < tmp.Length; ++i)
                    {
                        tmp[i] = tmp[i].Clone();
                    }
                    Set(tmp);
                }
            }
            else
            {	// vorne und hinten trimmen
                GeoPoint p = PointAt(EndPos);
                ICurve[] res = Split(StartPos);
                if (isClosed && reverse)
                {	// das Ergebnis setzt sich aus zwei Teilpfaden zusammen
                    if (res.Length == 2)
                    {
                        double endpos = res[1].PositionOf(p);
                        ICurve[] part2 = res[1].Split(endpos);
                        if (part2.Length == 2)
                        {
                            ArrayList ar = new ArrayList();
                            ICurve[] tmp = part2[1].SubCurves;
                            // Curves liefert zwar ein geklontes Array, aber mit denselben Kurven
                            // deshalb noch clonen
                            for (int i = 0; i < tmp.Length; ++i)
                            {
                                tmp[i] = tmp[i].Clone();
                            }
                            ar.AddRange(tmp);
                            tmp = res[0].SubCurves;
                            for (int i = 0; i < tmp.Length; ++i)
                            {
                                tmp[i] = tmp[i].Clone();
                            }
                            ar.AddRange(tmp);
                            Set((ICurve[])ar.ToArray(typeof(ICurve)));
                        }
                    }
                }
                else
                {
                    if (res.Length == 2)
                    {
                        double endpos = res[1].PositionOf(p);
                        res = res[1].Split(endpos);
                        if (res.Length == 2)
                        {
                            ICurve[] tmp = (res[0] as Path).Curves;
                            for (int i = 0; i < tmp.Length; ++i)
                            {
                                tmp[i] = tmp[i].Clone();
                            }
                            Set((res[0] as Path).Curves);
                        }
                    }
                }
            }
        }
        ICurve ICurve.Clone() { return (ICurve)this.Clone(); }
        ICurve ICurve.CloneModified(ModOp m)
        {
            IGeoObject clone = Clone();
            clone.Modify(m);
            return (ICurve)clone;
        }
        public bool IsClosed
        {
            get
            {
                return Precision.IsEqual(StartPoint, EndPoint);
            }
        }
        public bool IsSingular
        {
            get
            {
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    if (!subCurves[i].IsSingular) return false;
                }
                return true;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetPlanarState ()"/>
        /// </summary>
        /// <returns></returns>
        public PlanarState GetPlanarState()
        {
            if (planarState == PlanarState.Unknown) RecalcPlanarState();
            return planarState;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetPlane ()"/>
        /// </summary>
        /// <returns></returns>
        public Plane GetPlane()
        {
            if (planarState == PlanarState.Unknown || inPlane == null) RecalcPlanarState();
            if (inPlane != null) return inPlane;
            else throw new PathException(PathException.ExceptionType.NotPlanar, "GetPlane: Path is not planar");
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.IsInPlane (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool IsInPlane(Plane p)
        {
            if (planarState == PlanarState.Unknown) RecalcPlanarState();
            if (planarState == PlanarState.Planar)
            {
                if (inPlane != null) return Precision.IsEqual(p, inPlane);
                else return false;
            }
            else if (planarState == PlanarState.UnderDetermined)
            {
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    if (!subCurves[i].IsInPlane(p)) return false;
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.GetProjectedCurve (Plane)"/>
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public CADability.Curve2D.ICurve2D GetProjectedCurve(Plane p)
        {
            List<ICurve2D> cvs = new List<ICurve2D>();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                ICurve c = subCurves[i];
                ICurve2D c2d = c.GetProjectedCurve(p); // in der Hoffnung, dass das immer klappt
                if (c2d != null) cvs.Add(c2d);
            }
            Path2D res = new Path2D(cvs.ToArray(), true);
            if (length == null) Recalc();
            res.DisplayClosed = isClosed;
            return res;
        }
        public override string Description
        {
            get
            {
                return StringTable.GetString("Path.Description");
            }
        }
        bool ICurve.IsComposed
        {
            get { return true; }
        }
        ICurve[] ICurve.SubCurves
        {
            // warum Clone zurückliefern? Damit der Aufrufer nicht ungefragt Objekte hinzufügt oder entfernt
            get { return (ICurve[])subCurves.Clone(); }
        }
        ICurve ICurve.Approximate(bool linesOnly, double maxError)
        {
            if (GetPlanarState() == PlanarState.Planar)
            {
                Plane pln = GetPlane();
                ICurve2D c2d = GetProjectedCurve(pln);
                ICurve2D app = c2d.Approximate(linesOnly, maxError);
                return app.MakeGeoObject(pln) as ICurve;
            }
            List<ICurve> al = new List<ICurve>();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                al.Add(subCurves[i].Approximate(linesOnly, maxError));
            }
            Path res = Path.Construct();
            res.Set(al.ToArray());
            res.Flatten();
            return res;
        }
        double[] ICurve.TangentPosition(GeoVector direction)
        {
            List<double> res = new List<double>();
            for (int i = 0; i < subCurves.Length; i++)
            {
                double[] tmp = subCurves[i].TangentPosition(direction);
                if (tmp != null)
                {
                    for (int j = 0; j < tmp.Length; ++j)
                    {
                        tmp[j] = (i + tmp[j]) / (double)subCurves.Length;
                    }
                    res.AddRange(tmp);
                }
            }
            return res.ToArray();
        }
        double[] ICurve.GetSelfIntersections()
        {
            List<double> res = new List<double>();
            for (int i = 0; i < subCurves.Length - 1; ++i)
            {
                for (int j = i + 1; j < subCurves.Length; ++j)
                {
                    double[] intp = CADability.GeoObject.Curves.Intersect(subCurves[i], subCurves[j], true);
                    for (int k = 0; k < intp.Length; ++k)
                    {
                        double p = subCurves[j].PositionOf(subCurves[i].PointAt(intp[k]));
                        if (j == i + 1)
                        {   // Ecken selbst nicht als Schnittpunkte interpretieren
                            if (Math.Abs(1.0 - intp[k]) < 1e-8 && Math.Abs(p) < 1e-8) continue;
                        }
                        if (i == 0 && j == subCurves.Length - 1 && isClosed)
                        {   // bei geschlossenen nicht den Schließpunkt beachten
                            if (Math.Abs(intp[k]) < 1e-8 && Math.Abs(1.0 - p) < 1e-8) continue;
                        }
                        if (p >= 0.0 && p <= 1.0)
                        {
                            res.Add((i + intp[k]) / subCurves.Length);
                            res.Add((j + p) / subCurves.Length);
                        }
                    }
                }
            }
            for (int i = 0; i < subCurves.Length; ++i)
            {   // sollte eine Teilkurve selbst innere Schnittpunkte haben, müssen die natürlich auch noch dazu:
                double[] intp = subCurves[i].GetSelfIntersections();
                for (int j = 0; j < intp.Length; ++j)
                {
                    intp[j] = (i + intp[j]) / subCurves.Length;
                }
                res.AddRange(intp);
            }
            return res.ToArray();
        }
        bool ICurve.SameGeometry(ICurve other, double precision)
        {
            if (other is Path)
            {
                Path pother = other as Path;
                if (pother.CurveCount == CurveCount)
                {
                    bool forward = (StartDirection * other.StartDirection) > 0;
                    for (int i = 0; i < CurveCount; i++)
                    {
                        if (forward)
                        {
                            if (!Curve(i).SameGeometry(pother.Curve(i), precision)) return false;
                        }
                        else
                        {
                            if (!Curve(i).SameGeometry(pother.Curve(CurveCount - 1 - i), precision)) return false;

                        }
                    }
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// Implements <see cref="CADability.GeoObject.ICurve.TryPointDeriv2At (double, out GeoPoint, out GeoVector, out GeoVector)"/>
        /// </summary>
        /// <param name="position"></param>
        /// <param name="point"></param>
        /// <param name="deriv"></param>
        /// <param name="deriv2"></param>
        /// <returns></returns>
        public virtual bool TryPointDeriv2At(double position, out GeoPoint point, out GeoVector deriv, out GeoVector deriv2)
        {
            point = GeoPoint.Origin;
            deriv = deriv2 = GeoVector.NullVector;
            return false;
        }
        /// <summary>
        /// Returns the index of the subcurve at the given position. Position must be between 0.0 and this.Length
        /// </summary>
        /// <param name="position">Position for the query</param>
        /// <returns>Index of the curve</returns>
        public int IndexAtLength(double position)
        {
            double length = Length;
            int maxind = subCurves.Length;
            for (int i = 0; i < maxind; ++i)
            {
                double l = subCurves[i].Length;
                if (l < position)
                {
                    position -= l;
                }
                else
                {
                    return i;
                }
            }
            return maxind - 1;
        }
        double ICurve.PositionAtLength(double position)
        {
            double length = Length;
            int maxind = subCurves.Length;
            for (int i = 0; i < maxind; ++i)
            {
                double l = subCurves[i].Length;
                if (l < position)
                {
                    position -= l;
                }
                else
                {
                    return (i + subCurves[i].PositionAtLength(position)) / maxind;
                }
            }
            return 1.0;
        }
        double ICurve.ParameterToPosition(double parameter)
        {
            return (this as ICurve).PositionAtLength(parameter);
        }
        double ICurve.PositionToParameter(double position)
        {
            return position * Length;
        }

        BoundingCube ICurve.GetExtent()
        {
            return GetExtent(0.0);
        }
        bool ICurve.Extend(double atStart, double atEnd)
        {
            return false;
        }
        bool ICurve.HitTest(BoundingCube cube)
        {
            return HitTest(ref cube, 0.0);
        }
        double[] ICurve.GetSavePositions()
        {
            return new double[] { 0.0, 1.0 };
        }
        double[] ICurve.GetExtrema(GeoVector direction)
        {
            List<double> res = new List<double>();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                res.AddRange(subCurves[i].GetExtrema(direction));
            }
            return res.ToArray();
        }
        double[] ICurve.GetPlaneIntersection(Plane plane)
        {
            List<double> res = new List<double>();
            for (int i = 0; i < subCurves.Length; ++i)
            {
                double[] ipar = subCurves[i].GetPlaneIntersection(plane);
                for (int j = 0; j < ipar.Length; ++j)
                {
                    bool ok = (ipar[j] >= 0.0 && ipar[j] <= 1.0);
                    if (i == 0) ok = (ipar[j] <= 1.0);
                    if (i == subCurves.Length - 1) ok = (ipar[j] >= 0.0);
                    if (ok)
                    {
                        res.Add((i + ipar[j]) / subCurves.Length);
                    }
                }
            }
            return res.ToArray();
        }
        double ICurve.DistanceTo(GeoPoint p)
        {
            double pos = (this as ICurve).PositionOf(p);
            if (pos >= 0.0 && pos <= 1.0)
            {
                GeoPoint pCurve = (this as ICurve).PointAt(pos);
                return pCurve | p;
            }
            else
            {
                return Math.Min(p | StartPoint, p | EndPoint);
            }
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Path(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            try
            {
                subCurves = info.GetValue("SubCurves", typeof(ICurve[])) as ICurve[];
                colorDef = ColorDef.Read("ColorDef", info, context);
                lineWidth = LineWidth.Read("LineWidth", info, context);
                linePattern = LinePattern.Read("LinePattern", info, context);
            }
            catch (SerializationException)
            {	// vielleicht noch nach altem Muster gespeichert?
                // das blöde: containedObjects kann man noch nicht verwenden...
                GeoObjectList containedObjects = (GeoObjectList)info.GetValue("ContainedObjects", typeof(GeoObjectList));
                UserData.Add("CADability.SerializedAsBlock.ContainedObjects", containedObjects);
            }
            planarState = PlanarState.Unknown;
        }

        /// <summary>
        /// Implements ISerializable:GetObjectData
        /// </summary>
        /// <param name="info"></param>
        /// <param name="context"></param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("SubCurves", subCurves);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("LineWidth", lineWidth);
            info.AddValue("LinePattern", linePattern);
        }
        #endregion
        #region IDeserializationCallback Members
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (UserData.ContainsData("CADability.SerializedAsBlock.ContainedObjects"))
            {	// kommt nur dran, wenn noch als Block abgespeichert, die Info steckt dann im UserData
                GeoObjectList containedObjects = UserData.GetData("CADability.SerializedAsBlock.ContainedObjects") as GeoObjectList;
                this.Set(containedObjects, false, Precision.eps);
                UserData.RemoveUserData("CADability.SerializedAsBlock.ContainedObjects");
            }
            else
            {
                for (int i = 0; i < subCurves.Length; ++i)
                {
                    IGeoObject go = subCurves[i] as IGeoObject;
                    if (go != null)
                    {
                        if (go.Owner != null) go.Owner.Remove(go);
                        go.Owner = this;
                        // nicht nachvollziehbar: die events sind schon gesetzt
                        // deshalb erst entfernen, dann neu setzen:
                        go.DidChangeEvent -= new ChangeDelegate(SubCurveDidChange);
                        go.WillChangeEvent -= new ChangeDelegate(SubCurveWillChange);
                        go.DidChangeEvent += new ChangeDelegate(SubCurveDidChange);
                        go.WillChangeEvent += new ChangeDelegate(SubCurveWillChange);
                    }
                }
            }
            Recalc(); // die Längen und isClosed wenigstens berechnen
            if (Constructed != null) Constructed(this);
        }
        #endregion
        #region IOcasWire Members

        #endregion
        #region IColorDef Members
        private ColorDef colorDef;
        public ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                using (new ChangingAttribute(this, "ColorDef", colorDef))
                {
                    colorDef = value;
                    if (subCurves != null) for (int i = 0; i < subCurves.Length; ++i)
                        {
                            (subCurves[i] as IColorDef).ColorDef = value;
                        }
                }
            }
        }
        void IColorDef.SetTopLevel(ColorDef newValue)
        {
            colorDef = newValue;
        }
        void IColorDef.SetTopLevel(ColorDef newValue, bool overwriteChildNullColor)
        {
            (this as IColorDef).SetTopLevel(newValue);
            if (overwriteChildNullColor)
            {
                if (subCurves != null)
                {
                    for (int i = 0; i < subCurves.Length; ++i)
                    {
                        if ((subCurves[i] as IColorDef).ColorDef == null)
                        {
                            (subCurves[i] as IColorDef).ColorDef = newValue;
                        }
                    }
                }
            }
        }
        #endregion
        #region ILinePattern Members
        private LinePattern linePattern;
        public LinePattern LinePattern
        {
            get
            {
                return linePattern;
            }
            set
            {
                using (new ChangingAttribute(this, "LinePattern", linePattern))
                {
                    linePattern = value;
                    if (subCurves != null) for (int i = 0; i < subCurves.Length; ++i)
                        {
                            (subCurves[i] as ILinePattern).LinePattern = value;
                        }
                }
            }
        }
        #endregion
        #region ILineWidth Members
        private LineWidth lineWidth;
        public LineWidth LineWidth
        {
            get
            {
                return lineWidth;
            }
            set
            {
                using (new ChangingAttribute(this, "LineWidth", lineWidth))
                {
                    lineWidth = value;
                    if (subCurves != null) for (int i = 0; i < subCurves.Length; ++i)
                        {
                            (subCurves[i] as ILineWidth).LineWidth = value;
                        }
                }
            }
        }
        #endregion
        #region IGeoObjectOwner
        void IGeoObjectOwner.Remove(IGeoObject toRemove)
        {	// ein Teilobjekt eines Pfades wird anderweitig verwendet
            // was kann man da tun? Es einfach zu entfernen geht nicht, da dieser
            // Pfad durch ein späteres Undo wieder verwendet werden könnte.
            // also wir bahalten einen Clone von dem zu entfernenden Teilstück
            // Typischer Fall: man entfernt einen Pfad aus dem Modell und fasst ihn
            // zu mit einem anderen pfad zusammen. Oder: bei "Flatten" werden die Objekte
            // eines Pfades zu einem anderen dazugenommen.
            // andere LÖSUNG: ein Pfad ist unveränderlich (bis auf Modifikationen)
            // d.h. beim Zusammenfassen oder bei "Flatten" werden immer Kopien verwendet.
            // das wäre auch eine sehr suabere Lösung!
            int ind = -1;
            for (int i = 0; i < subCurves.Length; ++i)
            {
                if (subCurves[i] == toRemove)
                {
                    ind = i;
                    break;
                }
            }
            if (ind >= 0)
            {
                toRemove.DidChangeEvent -= new ChangeDelegate(SubCurveDidChange);
                toRemove.WillChangeEvent -= new ChangeDelegate(SubCurveWillChange);
                IGeoObject cl = toRemove.Clone();
                cl.DidChangeEvent += new ChangeDelegate(SubCurveDidChange);
                cl.WillChangeEvent += new ChangeDelegate(SubCurveWillChange);
                subCurves[ind] = cl as ICurve;
            }
        }
        void IGeoObjectOwner.Add(IGeoObject toAdd)
        {
            // da machen wir nur selbst
        }
        #endregion
        private bool consistent
        {
            get
            {
                if (isReversing) return true;
                if (subCurves == null) return true;
                for (int i = 0; i < subCurves.Length - 1; ++i)
                {
                    if (!Precision.IsEqual(subCurves[i].EndPoint, subCurves[i + 1].StartPoint))
                        return false;
                }
                return true;
            }
        }
        private bool isReversing;
        private void SubCurveDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (this.isChanging < 1)
            {	// wenn der Pfad selbst die Ursache für das Ändern ist, dann müssen wir das
                // nicht weiterleiten
                base.FireDidChange(Change);
            }
        }
        private void SubCurveWillChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (this.isChanging < 1)
            {
                base.FireWillChange(Change);
            }
        }

        #region IExtentedableCurve Members

        IOctTreeInsertable IExtentedableCurve.GetExtendedCurve(ExtentedableCurveDirection direction)
        {
            return new InfinitePath(this);
        }

        #endregion

        internal Path[] SplitNonTangential()
        {
            List<Path> res = new List<Path>();
            List<ICurve> current = new List<ICurve>();
            current.Add(subCurves[0].Clone());
            for (int i = 1; i < subCurves.Length; i++)
            {
                if (Precision.SameDirection(current[current.Count - 1].EndDirection, subCurves[i].StartDirection, false))
                {
                    current.Add(subCurves[i].Clone());
                }
                else
                {
                    res.Add(Path.Construct(current.ToArray()));
                    current.Clear();
                    current.Add(subCurves[i].Clone());
                }
            }
            res.Add(Path.Construct(current.ToArray()));
            // der letzte könnte tangential zum ersten sein
            if (res.Count > 1)
            {
                if (Precision.SameDirection(res[res.Count - 1].EndDirection, res[0].StartDirection, false))
                {
                    List<ICurve> connected = new List<ICurve>(res[res.Count - 1].subCurves);
                    connected.AddRange(res[0].subCurves);
                    res[0] = Path.Construct(connected.ToArray());
                    res.RemoveAt(res.Count - 1);
                }
            }
            return res.ToArray();
        }

        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            return (this as ICurve).GetProjectedCurve(projection.ProjectionPlane).GetExtent();
        }
    }
    /// <summary>
    /// Dieses Objekt dient nur dazu, Kurven in der Nähe des verlängerten Pfades aus dem OctTree zu filtern
    /// Wenn das erste und das letzte Segment unendlich verlängert werden und zwar in beide Richtungen,
    /// so liefert das mehr Objekte als gewünscht, schaded aber nur der Performance
    /// </summary>
    internal class InfinitePath : IOctTreeInsertable
    {
        IOctTreeInsertable[] subCurves;
        bool infinite;
        Path thePath;
        public InfinitePath(Path thePath)
        {
            this.thePath = thePath.Clone() as Path;
            this.thePath.Flatten();
            subCurves = new IOctTreeInsertable[this.thePath.CurveCount];
            infinite = false;
            for (int i = 0; i < this.thePath.CurveCount; ++i)
            {
                subCurves[i] = this.thePath.Curve(i) as IOctTreeInsertable;
            }
            if (this.thePath.Curve(0) is IExtentedableCurve && !this.thePath.IsClosed)
            {
                subCurves[0] = (this.thePath.Curve(0) as IExtentedableCurve).GetExtendedCurve(ExtentedableCurveDirection.forward);
                infinite = true;
            }
            int l = this.thePath.CurveCount - 1;
            if (this.thePath.Curve(l) is IExtentedableCurve && !this.thePath.IsClosed)
            {
                subCurves[l] = (this.thePath.Curve(l) as IExtentedableCurve).GetExtendedCurve(ExtentedableCurveDirection.backward);
                infinite = true;
            }
        }
        #region IOctTreeInsertable Members

        BoundingCube IOctTreeInsertable.GetExtent(double precision)
        {
            if (infinite)
            {
                return BoundingCube.InfiniteBoundingCube;
            }
            else
            {
                return thePath.GetExtent(precision);
            }
        }

        bool IOctTreeInsertable.HitTest(ref BoundingCube cube, double precision)
        {
            for (int i = 0; i < subCurves.Length; ++i)
            {
                if (subCurves[i].HitTest(ref cube, precision)) return true;
            }
            return false;
        }

        bool IOctTreeInsertable.HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            for (int i = 0; i < subCurves.Length; ++i)
            {
                if (subCurves[i].HitTest(projection, rect, onlyInside)) return true;
            }
            return false;
        }

        double IOctTreeInsertable.Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        bool IOctTreeInsertable.HitTest(Projection.PickArea area, bool onlyInside)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
