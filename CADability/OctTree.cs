using CADability.GeoObject;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wintellect.PowerCollections;

namespace CADability
{
#if DEBUG
    internal class PerfTimer : IDisposable
    {
        static Dictionary<string, int> Timers = new Dictionary<string, int>();
        string name;
        int starttime;
        public PerfTimer(string name)
        {
            this.name = name;
            starttime = Environment.TickCount;
        }
    #region IDisposable Members

        void IDisposable.Dispose()
        {
            int dt = Environment.TickCount - starttime;
            int current;
            if (Timers.ContainsKey(name))
            {
                current = Timers[name];
            }
            else
            {
                current = 0;
            }
            current += dt;
            Timers[name] = current;
        }

        public static void doubleTest()
        {
            double d = 1.0;
            for (int i = 0; i < 100; i++)
            {
                d = d / 2;
            }
            for (int i = -32; i < 32; i++)
            {
                if (i < 0) d = 1.0 / (((Int64)1) << (-i));
                else d = ((Int64)1) << (i);

                System.Diagnostics.Trace.WriteLine(d.ToString(), BitConverter.DoubleToInt64Bits(d).ToString("X"));
            }
        }
    #endregion
    }
#endif

    public interface IOctTreeInsertable
    {
        /// <summary>
        /// Gets the 3-dimensional extent of an object
        /// </summary>
        /// <param name="precision">Precision of the test</param>
        /// <returns>The minmax cube</returns>
        BoundingCube GetExtent(double precision);
        /// <summary>
        /// Tests whether an object is touched by the provided cube
        /// </summary>
        /// <param name="cube">To test with</param>
        /// <param name="precision">Required precision</param>
        /// <returns>true if the object strikes the cube</returns>
        bool HitTest(ref BoundingCube cube, double precision);
        /// <summary>
        /// Tests whether an object is inside or touched by the rectangle when projected with the 
        /// provided projection
        /// </summary>
        /// <param name="projection">The projection for the test</param>
        /// <param name="rect">The rectangle to test the position</param>
        /// <param name="onlyInside">true: the object must be totaly inside the rectangle, false: 
        /// the object must be totaly or partially inside the rectangle</param>
        /// <returns>true when the test succeedes, false otherwise</returns>
        bool HitTest(Projection projection, BoundingRect rect, bool onlyInside);
        /// <summary>
        /// Tests whether an object is inside or touched by the provided area
        /// </summary>
        /// <param name="projection">The projection for the test</param>
        /// <param name="area">The area to check for</param>
        /// <param name="onlyInside">true: the object must be totaly inside the area, false: 
        /// the object must be totaly or partially inside the area</param>
        /// <returns>true when the test succeedes, false otherwise</returns>
        bool HitTest(Projection.PickArea area, bool onlyInside);
        /// <summary>
        /// Returns the smallest parameter value (l) where the provided line hits the object so that the point
        /// <paramref name="fromHere"/> + l * <paramref name="direction"/> is on or close to the object. The result may be negative.
        /// Return double.MaxValue if there is no such point.
        /// </summary>
        /// <param name="fromHere">Startpoint of the line</param>
        /// <param name="direction">Direction of the line</param>
        /// <param name="precision">Precision for the test</param>
        /// <returns>Position on the line</returns>
        double Position(GeoPoint fromHere, GeoVector direction, double precision);
    }
    /// <summary>
    /// Generic class to provide fast access to <see cref="IOctTreeInsertable"/> implementing objects.
    /// </summary>
    /// <typeparam name="T">The generic type, must implement <see cref="IOctTreeInsertable"/></typeparam>
    public class OctTree<T> where T : IOctTreeInsertable
    {
        /// <summary>
        /// The precision of this octtree
        /// </summary>
        public double precision;
        /// <summary>
        /// The root <see cref="Node&lt;T&gt;"/> of this octtree
        /// </summary>
        private Node<T> node;
        private readonly SplitTestFunction splitTest;

        /// <summary>
        /// Definition of a node of this octtree.
        /// </summary>
        /// <typeparam name="TT">The generic type, is the same type as the enclosing OctTree type</typeparam>
        public class Node<TT> : IComparable<Node<TT>> where TT : T
        {
            /// <summary>
            /// the subtrees, may be null
            /// </summary>
            public Node<TT> ppp, mpp, pmp, mmp, ppm, mpm, pmm, mmm; // x,y,z P: positive, N: negative (relative to the center of the cube)
            /// <summary>
            /// List of <see cref="IOctTreeInsertable"/> objects in this octTree
            /// </summary>
            public HashSet<TT> list;
            /// <summary>
            /// Back reference to the parent node.
            /// </summary>
            public Node<TT> parent;
            /// <summary>
            /// Back reference to the root.
            /// </summary>
            public OctTree<TT> root;
            /// <summary>
            /// Depth of this node in the tree
            /// </summary>
            public int deepth; // Tiefe dieses Knotens
            /// <summary>
            /// Center of the cube definig this node.
            /// </summary>
            public GeoPoint center; // Mittelpunkt des Würfels
            /// <summary>
            /// "Radius" (half width) of the cube defining this node
            /// </summary>
            public double size; // halbe Seitenlänge des Würfels, wenn 0.0, dann noch kein Wert
            /// <summary>
            /// Cube defining this node
            /// </summary>
            public BoundingCube cube;
            /// <summary>
            /// Derived OctTrees may use this for whatever they need it
            /// </summary>
            public object extension;
            public Node(OctTree<TT> root, GeoPoint center, double size)
            {   // nur der root Knoten wird so erzeugt
                this.root = root;
                this.parent = null;
                this.center = center;
                this.size = size;
                this.list = null; // keine Liste im Gegensatz zu siehe unten
                this.deepth = 0;
                this.cube = new BoundingCube(center, size);
            }
            public Node(OctTree<TT> root, Node<TT> parent, GeoPoint center, double halfSize)
            {
                this.root = root;
                this.parent = parent;
                this.center = center;
                this.size = halfSize;
                this.list = new HashSet<TT>(); // beginnt als blatt mit leerer liste, im gegensatz zum root, der ganz leer beginnt
                this.deepth = parent.deepth + 1;
                // this.cube = new BoundingCube(center, size);
                // the cubes of the octtree may not have gaps inbetween. Because of precisiion loss when adding this might occur
                // so we "round up" the maximum values
                this.cube = new BoundingCube(center.x - halfSize, Geometry.NextDouble(center.x + halfSize), center.y - halfSize, Geometry.NextDouble(center.y + halfSize), center.z - halfSize, Geometry.NextDouble(center.z + halfSize));

            }
            internal Node<TT> extend(bool toleft, bool tofront, bool tobottom)
            {
                GeoPoint newCenter = new GeoPoint();
                if (toleft) newCenter.x = center.x - size;
                else newCenter.x = center.x + size;
                if (tofront) newCenter.y = center.y - size;
                else newCenter.y = center.y + size;
                if (tobottom) newCenter.z = center.z - size;
                else newCenter.z = center.z + size;
                Node<TT> res = new Node<TT>(root, newCenter, size * 2.0);
                res.ppp = new Node<TT>(root, this, new GeoPoint(newCenter.x + size, newCenter.y + size, newCenter.z + size), size);
                res.mpp = new Node<TT>(root, this, new GeoPoint(newCenter.x - size, newCenter.y + size, newCenter.z + size), size);
                res.pmp = new Node<TT>(root, this, new GeoPoint(newCenter.x + size, newCenter.y - size, newCenter.z + size), size);
                res.mmp = new Node<TT>(root, this, new GeoPoint(newCenter.x - size, newCenter.y - size, newCenter.z + size), size);
                res.ppm = new Node<TT>(root, this, new GeoPoint(newCenter.x + size, newCenter.y + size, newCenter.z - size), size);
                res.mpm = new Node<TT>(root, this, new GeoPoint(newCenter.x - size, newCenter.y + size, newCenter.z - size), size);
                res.pmm = new Node<TT>(root, this, new GeoPoint(newCenter.x + size, newCenter.y - size, newCenter.z - size), size);
                res.mmm = new Node<TT>(root, this, new GeoPoint(newCenter.x - size, newCenter.y - size, newCenter.z - size), size);
                this.parent = res;
                int sw = 0;
                if (toleft) sw += 4;
                if (tofront) sw += 2;
                if (tobottom) sw += 1;
                switch (sw)
                {
                    case 0: res.mmm = this; break;
                    case 1: res.mmp = this; break;
                    case 2: res.mpm = this; break;
                    case 3: res.mpp = this; break;
                    case 4: res.pmm = this; break;
                    case 5: res.pmp = this; break;
                    case 6: res.ppm = this; break;
                    case 7: res.ppp = this; break;
                }
                RecalcDeepth();
                if (ppp == null && list == null)
                {   // dieser Knoten ist ein noch unfertiger root Knoten, der sich durch keine Liste und keine Unterknoten
                    // auszeichnet. Er bekommt jetzt eine leere Liste
                    list = new HashSet<TT>();
                }
                return res;
            }
            private void RecalcDeepth()
            {
                this.deepth = parent.deepth + 1;
                if (ppp != null)
                {
                    ppp.RecalcDeepth();
                    mpp.RecalcDeepth();
                    pmp.RecalcDeepth();
                    mmp.RecalcDeepth();
                    ppm.RecalcDeepth();
                    mpm.RecalcDeepth();
                    pmm.RecalcDeepth();
                    mmm.RecalcDeepth();
                }
            }
            internal void AddObjectAsync(TT objectToAdd)
            {
                if (parent == null && ppp == null)
                {	// this is the root and it must be initialized
                    BoundingCube ext = objectToAdd.GetExtent(root.precision);
                    if (ext.IsEmpty) return;
                    lock (this)
                    {   // there may be only a single thread, which creates the empty list
                        // so for the root all objects are forced to be inserted synchronously
                        if (ppp == null) // maybe it has been created while waiting for the lock
                        {
                            if (list == null)
                            {   // inserting the first object into the root
                                list = new HashSet<TT>();
                                if (size > 0.0 && cube.Contains(ext))    // there is already a size (the octtree has been created with a size)
                                {
                                }
                                else
                                {
                                    center = ext.GetCenter();
                                    size = ext.MaxSide / 2.0 * 1.1;
                                    // manipulate the center a little bit to avoid unnecessary list splitting when all objects are in one of the standard planes
                                    // which is often the case with only 2 dimensional objects
                                    center = new GeoPoint(center.x + size * 0.0001, center.y + size * 0.0001, center.z + size * 0.0001);
                                    if (size == 0.0) size = 1.0; // there was only a point, size may newer be 0
                                }
                            }
                            else
                            {   // adding an additional object to the root
                                // we can still manipulate the root extent, because there are no subtrees yet
                                if (!cube.Contains(ext))
                                {
                                    ext.MinMax(cube);
                                    center = ext.GetCenter();
                                    size = ext.MaxSide / 2.0 * 1.1;
                                    center = new GeoPoint(center.x + size * 0.0001, center.y + size * 0.0001, center.z + size * 0.0001);
                                }
                            }
                            cube = new BoundingCube(center, size);
                        }
                    }
                }
                bool insert;
                lock (objectToAdd) // HitTest and GetExtent are maybe not reentrant, because they may modify the approximation of the object
                    insert = !BoundingCube.Disjoint(objectToAdd.GetExtent(root.precision), cube) && objectToAdd.HitTest(ref cube, root.precision);
                if (insert)
                {
                    HashSet<TT> toInsert = null;
                    if (ppp == null)
                    {	// no subtrees, insert into the list or split into subtrees
                        lock (this)
                        {
                            if (ppp == null) // maybe another thread has created the subtrees while we were waiting for the lock, so we are already done
                            {
                                if (root.SplitNode(this as CADability.OctTree<TT>.Node<TT>, objectToAdd))
                                {   // create the subtrees
                                    double halfSize = Geometry.NextDouble(size / 2.0); // it is important to "round up", because otherwise we have gaps between the cubes
                                    mpp = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y + halfSize, center.z + halfSize), halfSize);
                                    pmp = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y - halfSize, center.z + halfSize), halfSize);
                                    mmp = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y - halfSize, center.z + halfSize), halfSize);
                                    ppm = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y + halfSize, center.z - halfSize), halfSize);
                                    mpm = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y + halfSize, center.z - halfSize), halfSize);
                                    pmm = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y - halfSize, center.z - halfSize), halfSize);
                                    mmm = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y - halfSize, center.z - halfSize), halfSize);
                                    toInsert = list;
                                    list = null;
                                    ppp = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y + halfSize, center.z + halfSize), halfSize);
                                    // this "ppp = " must be the last (atomic) line, for concurrent threads which find the ppp!=null must be able to use all subtrees
                                    // the insertion will happen after the lock is left
                                }
                                else
                                {
                                    list.Add(objectToAdd);
                                    return; // done, leave the lock fast!
                                }
                            }
                        }
                    }
                    // here ppp!=null and the object has not been added yet
                    // we could do the following parallel, but we get here when multiple objects are inserted parallel and 
                    // the overhead of doing this parallel may be greater than what we would gain (still to test)
                    if (toInsert != null)
                    {
                        //Parallel.For(0, toInsert.Count, i =>
                        foreach (TT item in toInsert)
                        {
                            ppp.AddObjectAsync(item);
                            mpp.AddObjectAsync(item);
                            pmp.AddObjectAsync(item);
                            mmp.AddObjectAsync(item);
                            ppm.AddObjectAsync(item);
                            mpm.AddObjectAsync(item);
                            pmm.AddObjectAsync(item);
                            mmm.AddObjectAsync(item);
                        }
                        // );
                    }
                    ppp.AddObjectAsync(objectToAdd);
                    mpp.AddObjectAsync(objectToAdd);
                    pmp.AddObjectAsync(objectToAdd);
                    mmp.AddObjectAsync(objectToAdd);
                    ppm.AddObjectAsync(objectToAdd);
                    mpm.AddObjectAsync(objectToAdd);
                    pmm.AddObjectAsync(objectToAdd);
                    mmm.AddObjectAsync(objectToAdd);

                }
            }
            internal void AddObject(TT objectToAdd)
            {
                if (parent == null && ppp == null)
                {	// der Baum ist noch unreif, da er nur aus einer Liste besteht
                    // das von der Wurzel überdeckte Quadrat kann also noch beliebig manipuliert
                    // werden. Es soll 10% größer sein als alle Objekte in der Wurzel
                    BoundingCube ext = objectToAdd.GetExtent(root.precision);
                    if (ext.IsEmpty) return;
                    if (list == null)
                    {	// das allererste Objekt wird im root eingefügt
                        list = new HashSet<TT>();
                        if (size > 0.0 && cube.Contains(ext))    // es gibt schone eine Größenvorgabe und es passt
                        {
                        }
                        else
                        {
                            center = ext.GetCenter();
                            size = ext.MaxSide / 2.0 * 1.1;
                            // Mittelpunkt etwas verwackeln, sonst liegt eine achsenparallele Fläche
                            // genau auf der Grenze, und das ist nicht gut!
                            center = new GeoPoint(center.x + size * 0.0001, center.y + size * 0.0001, center.z + size * 0.0001);
                            if (size == 0.0) size = 1.0; // Notbremse: nur ein Punkt wurde zugefügt, das kann sonst schiefgehen
                        }
                    }
                    else
                    {	// ein weiteres Objekt wird zugefügt
                        // wir sind hier in der komfortablen Lage den cube noch frei variieren zu können, 
                        // da es nur eine Liste gibt und wir uns im root befinden, cube muss schon existieren
                        if (!cube.Contains(ext))
                        {
                            ext.MinMax(cube);
                            center = ext.GetCenter();
                            size = ext.MaxSide / 2.0 * 1.1;
                            center = new GeoPoint(center.x + size * 0.0001, center.y + size * 0.0001, center.z + size * 0.0001);
                        }
                    }
                    cube = new BoundingCube(center, size);
                }
                // der Test auf Disjoint ist wahrscheinlich schneller und greift meistens
                //if (root.FilterHitTest(objectToAdd, this as CADability.OctTree<TT>.Node<TT>) || // evtl schneller mit diesem Filter
                if ((!BoundingCube.Disjoint(objectToAdd.GetExtent(root.precision), cube) && objectToAdd.HitTest(ref cube, root.precision)))
                {
                    if (ppp == null)
                    {	// es ist ein Blatt und kein Knoten
                        if (root.SplitNode(this as CADability.OctTree<TT>.Node<TT>, objectToAdd))
                        {	// das Blatt wird zum Knoten
                            // Untereinträge erzeugen
                            double halfSize = Geometry.NextDouble(size / 2.0);
                            ppp = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y + halfSize, center.z + halfSize), halfSize);
                            mpp = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y + halfSize, center.z + halfSize), halfSize);
                            pmp = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y - halfSize, center.z + halfSize), halfSize);
                            mmp = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y - halfSize, center.z + halfSize), halfSize);
                            ppm = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y + halfSize, center.z - halfSize), halfSize);
                            mpm = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y + halfSize, center.z - halfSize), halfSize);
                            pmm = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y - halfSize, center.z - halfSize), halfSize);
                            mmm = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y - halfSize, center.z - halfSize), halfSize);
                            // die vorhanden Objekte aufteilen (for ist marginal schneller als foreach)
                            foreach (TT item in list)
                            {
                                ppp.AddObject(item);
                                mpp.AddObject(item);
                                pmp.AddObject(item);
                                mmp.AddObject(item);
                                ppm.AddObject(item);
                                mpm.AddObject(item);
                                pmm.AddObject(item);
                                mmm.AddObject(item);
                            }
                            // das neue Objekt ebenfalls zufügen
#if DEBUG
                            // TODO: das kommt manchmal und sollte nicht: überprüfen!
                            // if (list.Contains(objectToAdd)) throw new ApplicationException("Fatal error in OctTree");
#endif
                            ppp.AddObject(objectToAdd);
                            mpp.AddObject(objectToAdd);
                            pmp.AddObject(objectToAdd);
                            mmp.AddObject(objectToAdd);
                            ppm.AddObject(objectToAdd);
                            mpm.AddObject(objectToAdd);
                            pmm.AddObject(objectToAdd);
                            mmm.AddObject(objectToAdd);
                            // die Liste wird nicht mehr gebraucht
                            list = null;
                        }
                        else
                        {	// es bleibt ein Blatt, einfach in die Liste damit
#if DEBUG
                            // TODO: das kommt manchmal und sollte nicht: überprüfen!
                            // if (list.Contains(objectToAdd)) throw new ApplicationException("Fatal error in OctTree");
#endif
                            list.Add(objectToAdd);
                        }
                    }
                    else
                    {
                        ppp.AddObject(objectToAdd);
                        mpp.AddObject(objectToAdd);
                        pmp.AddObject(objectToAdd);
                        mmp.AddObject(objectToAdd);
                        ppm.AddObject(objectToAdd);
                        mpm.AddObject(objectToAdd);
                        pmm.AddObject(objectToAdd);
                        mmm.AddObject(objectToAdd);
                    }
                }
            }
            internal void RemoveObject(TT objectToRemove)
            {
                if (ppp == null)
                {
                    list.Remove(objectToRemove);
                }
                else
                {
                    // if (objectToRemove.HitTest(ref cube, root.precision))
                    {
                        ppp.RemoveObject(objectToRemove);
                        mpp.RemoveObject(objectToRemove);
                        pmp.RemoveObject(objectToRemove);
                        mmp.RemoveObject(objectToRemove);
                        ppm.RemoveObject(objectToRemove);
                        mpm.RemoveObject(objectToRemove);
                        pmm.RemoveObject(objectToRemove);
                        mmm.RemoveObject(objectToRemove);
                    }
                }
            }
            internal void GetObjectsFromLine(GeoPoint start, GeoVector dir, double maxdist, Set<TT> addToList)
            {
                if (cube.Interferes(start, dir, maxdist, false))
                {
                    if (list != null)
                    {
                        addToList.AddMany(list);
                    }
                    else if (ppp != null)
                    {
                        ppp.GetObjectsFromLine(start, dir, maxdist, addToList);
                        mpp.GetObjectsFromLine(start, dir, maxdist, addToList);
                        pmp.GetObjectsFromLine(start, dir, maxdist, addToList);
                        mmp.GetObjectsFromLine(start, dir, maxdist, addToList);
                        ppm.GetObjectsFromLine(start, dir, maxdist, addToList);
                        mpm.GetObjectsFromLine(start, dir, maxdist, addToList);
                        pmm.GetObjectsFromLine(start, dir, maxdist, addToList);
                        mmm.GetObjectsFromLine(start, dir, maxdist, addToList);
                    }
                }
            }
            internal void GetObjectsFromRect(Projection projection, BoundingRect rect, bool onlyInside, Set<TT> addToList)
            {
                if (cube.Interferes(projection, rect))
                {
                    if (list != null)
                    {
                        foreach (TT item in list)
                        {
                            if (!addToList.Contains(item) && item.HitTest(projection, rect, onlyInside))
                            {
                                addToList.Add(item);
                            }
                        }
                    }
                    else if (ppp != null)
                    {
                        ppp.GetObjectsFromRect(projection, rect, onlyInside, addToList);
                        mpp.GetObjectsFromRect(projection, rect, onlyInside, addToList);
                        pmp.GetObjectsFromRect(projection, rect, onlyInside, addToList);
                        mmp.GetObjectsFromRect(projection, rect, onlyInside, addToList);
                        ppm.GetObjectsFromRect(projection, rect, onlyInside, addToList);
                        mpm.GetObjectsFromRect(projection, rect, onlyInside, addToList);
                        pmm.GetObjectsFromRect(projection, rect, onlyInside, addToList);
                        mmm.GetObjectsFromRect(projection, rect, onlyInside, addToList);
                    }
                }
            }
            internal void GetObjectsFromRect(Projection.PickArea area, bool onlyInside, Set<TT> addToList)
            {
                if (area == null) return;
                if (cube.Interferes(area))
                {
                    if (list != null)
                    {
                        foreach (TT item in list)
                        {
                            if (!addToList.Contains(item) && item.HitTest(area, onlyInside))
                            {
                                addToList.Add(item);
                            }
                        }
                    }
                    else if (ppp != null)
                    {
                        ppp.GetObjectsFromRect(area, onlyInside, addToList);
                        mpp.GetObjectsFromRect(area, onlyInside, addToList);
                        pmp.GetObjectsFromRect(area, onlyInside, addToList);
                        mmp.GetObjectsFromRect(area, onlyInside, addToList);
                        ppm.GetObjectsFromRect(area, onlyInside, addToList);
                        mpm.GetObjectsFromRect(area, onlyInside, addToList);
                        pmm.GetObjectsFromRect(area, onlyInside, addToList);
                        mmm.GetObjectsFromRect(area, onlyInside, addToList);
                    }
                }
            }
            internal void GetObjectsCloseTo(IOctTreeInsertable closeToThis, Set<TT> addToList)
            {
                BoundingCube clone = cube;
                if (closeToThis.HitTest(ref clone, root.precision))
                {
                    if (list != null)
                    {
                        lock (addToList)
                        {
                            addToList.AddMany(list);
                        }
                    }
                    else
                    {
#if PARALLEL
                        Parallel.Invoke(
                        () => ppp.GetObjectsCloseTo(closeToThis, addToList),
                        () => mpp.GetObjectsCloseTo(closeToThis, addToList),
                        () => pmp.GetObjectsCloseTo(closeToThis, addToList),
                        () => mmp.GetObjectsCloseTo(closeToThis, addToList),
                        () => ppm.GetObjectsCloseTo(closeToThis, addToList),
                        () => mpm.GetObjectsCloseTo(closeToThis, addToList),
                        () => pmm.GetObjectsCloseTo(closeToThis, addToList),
                        () => mmm.GetObjectsCloseTo(closeToThis, addToList));
#else
                        ppp.GetObjectsCloseTo(closeToThis, addToList);
                        mpp.GetObjectsCloseTo(closeToThis, addToList);
                        pmp.GetObjectsCloseTo(closeToThis, addToList);
                        mmp.GetObjectsCloseTo(closeToThis, addToList);
                        ppm.GetObjectsCloseTo(closeToThis, addToList);
                        mpm.GetObjectsCloseTo(closeToThis, addToList);
                        pmm.GetObjectsCloseTo(closeToThis, addToList);
                        mmm.GetObjectsCloseTo(closeToThis, addToList);
#endif
                    }
                }
            }
            internal IEnumerable<Node<T>> GetNodesCloseTo(IOctTreeInsertable closeToThis)
            {
                List<Node<T>> res = new List<Node<T>>();
                BoundingCube clone = cube;
                if (closeToThis.HitTest(ref clone, root.precision))
                {
                    if (list != null)
                    {
                        res.Add(this as OctTree<T>.Node<T>);
                    }
                    else
                    {
                        res.AddRange(ppp.GetNodesCloseTo(closeToThis));
                        res.AddRange(mpp.GetNodesCloseTo(closeToThis));
                        res.AddRange(pmp.GetNodesCloseTo(closeToThis));
                        res.AddRange(mmp.GetNodesCloseTo(closeToThis));
                        res.AddRange(ppm.GetNodesCloseTo(closeToThis));
                        res.AddRange(mpm.GetNodesCloseTo(closeToThis));
                        res.AddRange(pmm.GetNodesCloseTo(closeToThis));
                        res.AddRange(mmm.GetNodesCloseTo(closeToThis));
                    }
                }
                return res;
            }
            internal IEnumerable<Node<T>> Leaves
            {
                get
                {
                    if (list != null) yield return this as Node<T>;
                    else if (ppp != null)
                    {
                        foreach (Node<T> node in ppp.Leaves) yield return node;
                        foreach (Node<T> node in mpp.Leaves) yield return node;
                        foreach (Node<T> node in pmp.Leaves) yield return node;
                        foreach (Node<T> node in mmp.Leaves) yield return node;
                        foreach (Node<T> node in ppm.Leaves) yield return node;
                        foreach (Node<T> node in mpm.Leaves) yield return node;
                        foreach (Node<T> node in pmm.Leaves) yield return node;
                        foreach (Node<T> node in mmm.Leaves) yield return node;
                    }
                }
            }
            internal IEnumerable<HashSet<TT>> Lists
            {
                get
                {
                    if (list != null) yield return list;
                    else if (ppp != null)
                    {
                        foreach (HashSet<TT> list in ppp.Lists) yield return list;
                        foreach (HashSet<TT> list in mpp.Lists) yield return list;
                        foreach (HashSet<TT> list in pmp.Lists) yield return list;
                        foreach (HashSet<TT> list in mmp.Lists) yield return list;
                        foreach (HashSet<TT> list in ppm.Lists) yield return list;
                        foreach (HashSet<TT> list in mpm.Lists) yield return list;
                        foreach (HashSet<TT> list in pmm.Lists) yield return list;
                        foreach (HashSet<TT> list in mmm.Lists) yield return list;
                    }
                }
            }
            internal void GetObjectsFromBox(BoundingCube box, Set<TT> addToList, Filter filter)
            {
                if (!BoundingCube.Disjoint(cube, box))
                {
                    if (list != null)
                    {
                        foreach (TT item in list)
                        {
                            if (filter == null || filter(item))
                                addToList.Add(item);
                        }
                    }
                    else if (ppp != null)
                    {
                        ppp.GetObjectsFromBox(box, addToList, filter);
                        mpp.GetObjectsFromBox(box, addToList, filter);
                        pmp.GetObjectsFromBox(box, addToList, filter);
                        mmp.GetObjectsFromBox(box, addToList, filter);
                        ppm.GetObjectsFromBox(box, addToList, filter);
                        mpm.GetObjectsFromBox(box, addToList, filter);
                        pmm.GetObjectsFromBox(box, addToList, filter);
                        mmm.GetObjectsFromBox(box, addToList, filter);
                    }
                }
            }
            internal void GetNodesFromBox(BoundingCube box, List<Node<TT>> addToList, FilterNode filter)
            {
                if (!BoundingCube.Disjoint(cube, box))
                {
                    if (list != null)
                    {
                        if (filter == null || filter(this as OctTree<T>.Node<T>)) addToList.Add(this);
                    }
                    else if (ppp != null)
                    {
                        ppp.GetNodesFromBox(box, addToList, filter);
                        mpp.GetNodesFromBox(box, addToList, filter);
                        pmp.GetNodesFromBox(box, addToList, filter);
                        mmp.GetNodesFromBox(box, addToList, filter);
                        ppm.GetNodesFromBox(box, addToList, filter);
                        mpm.GetNodesFromBox(box, addToList, filter);
                        pmm.GetNodesFromBox(box, addToList, filter);
                        mmm.GetNodesFromBox(box, addToList, filter);
                    }
                }
            }
            internal void GetObjectsFromPoint(GeoPoint p, Set<TT> addToList)
            {
                if (cube.Contains(p))
                {
                    if (list != null)
                    {
                        addToList.AddMany(list);
                    }
                    else if (ppp != null)
                    {
                        ppp.GetObjectsFromPoint(p, addToList);
                        mpp.GetObjectsFromPoint(p, addToList);
                        pmp.GetObjectsFromPoint(p, addToList);
                        mmp.GetObjectsFromPoint(p, addToList);
                        ppm.GetObjectsFromPoint(p, addToList);
                        mpm.GetObjectsFromPoint(p, addToList);
                        pmm.GetObjectsFromPoint(p, addToList);
                        mmm.GetObjectsFromPoint(p, addToList);
                    }
                }
            }
            internal void GetObjectsFromPlane(Plane plane, Set<T> addToList)
            {
                if (cube.Interferes(plane))
                {
                    if (list != null)
                    {
                        foreach (T item in list)
                        {
                            addToList.Add(item);
                        }
                    }
                    else if (ppp != null)
                    {
                        ppp.GetObjectsFromPlane(plane, addToList);
                        mpp.GetObjectsFromPlane(plane, addToList);
                        pmp.GetObjectsFromPlane(plane, addToList);
                        mmp.GetObjectsFromPlane(plane, addToList);
                        ppm.GetObjectsFromPlane(plane, addToList);
                        mpm.GetObjectsFromPlane(plane, addToList);
                        pmm.GetObjectsFromPlane(plane, addToList);
                        mmm.GetObjectsFromPlane(plane, addToList);
                    }
                }
            }
#if DEBUG
            internal void Debug(GeoObjectList res)
            {
                if (list != null)
                {
                    Line l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(-size, -size, -size), center + new GeoVector(size, -size, -size));
                    res.Add(l);
                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(size, -size, -size), center + new GeoVector(size, size, -size));
                    res.Add(l);
                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(size, size, -size), center + new GeoVector(-size, size, -size));
                    res.Add(l);
                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(-size, size, -size), center + new GeoVector(-size, -size, -size));
                    res.Add(l);

                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(-size, -size, size), center + new GeoVector(size, -size, size));
                    res.Add(l);
                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(size, -size, size), center + new GeoVector(size, size, size));
                    res.Add(l);
                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(size, size, size), center + new GeoVector(-size, size, size));
                    res.Add(l);
                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(-size, size, size), center + new GeoVector(-size, -size, size));
                    res.Add(l);

                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(-size, -size, -size), center + new GeoVector(-size, -size, size));
                    res.Add(l);
                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(size, -size, -size), center + new GeoVector(size, -size, size));
                    res.Add(l);
                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(size, size, -size), center + new GeoVector(size, size, size));
                    res.Add(l);
                    l = Line.Construct();
                    l.SetTwoPoints(center + new GeoVector(-size, size, -size), center + new GeoVector(-size, size, size));
                    res.Add(l);
                }
                else
                {
                    ppp.Debug(res);
                    mpp.Debug(res);
                    pmp.Debug(res);
                    mmp.Debug(res);
                    ppm.Debug(res);
                    mpm.Debug(res);
                    pmm.Debug(res);
                    mmm.Debug(res);
                }
            }
            internal void Debug(GeoObjectList res, CheckThis checkThis)
            {
                if (list != null)
                {
                    if (checkThis(list.ToArray() as IOctTreeInsertable[], cube))
                    {
                        Line l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(-size, -size, -size), center + new GeoVector(size, -size, -size));
                        res.Add(l);
                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(size, -size, -size), center + new GeoVector(size, size, -size));
                        res.Add(l);
                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(size, size, -size), center + new GeoVector(-size, size, -size));
                        res.Add(l);
                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(-size, size, -size), center + new GeoVector(-size, -size, -size));
                        res.Add(l);

                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(-size, -size, size), center + new GeoVector(size, -size, size));
                        res.Add(l);
                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(size, -size, size), center + new GeoVector(size, size, size));
                        res.Add(l);
                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(size, size, size), center + new GeoVector(-size, size, size));
                        res.Add(l);
                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(-size, size, size), center + new GeoVector(-size, -size, size));
                        res.Add(l);

                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(-size, -size, -size), center + new GeoVector(-size, -size, size));
                        res.Add(l);
                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(size, -size, -size), center + new GeoVector(size, -size, size));
                        res.Add(l);
                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(size, size, -size), center + new GeoVector(size, size, size));
                        res.Add(l);
                        l = Line.Construct();
                        l.SetTwoPoints(center + new GeoVector(-size, size, -size), center + new GeoVector(-size, size, size));
                        res.Add(l);
                    }
                }
                else if (ppp != null)
                {
                    ppp.Debug(res, checkThis);
                    mpp.Debug(res, checkThis);
                    pmp.Debug(res, checkThis);
                    mmp.Debug(res, checkThis);
                    ppm.Debug(res, checkThis);
                    mpm.Debug(res, checkThis);
                    pmm.Debug(res, checkThis);
                    mmm.Debug(res, checkThis);
                }
            }
            internal GeoObjectList DebugList
            {
                get
                {
                    GeoObjectList res = new GeoObjectList();
                    if (list != null)
                    {
                        foreach (TT item in list)
                        {
                            if (item is IDebuggerVisualizer dv)
                                res.AddRange(dv.GetList());
                        }
                    }
                    return res;
                }
            }
#endif
            internal Node<T> FindExactNode(BoundingCube bc)
            {
                if (bc.Equals(cube)) return this as Node<T>;
                if (cube.Contains(bc))
                {
                    if (list != null) return this as Node<T>;
                    else if (ppp != null)
                    {
                        Node<T> nd;
                        nd = ppp.FindExactNode(bc);
                        if (nd != null) return nd;
                        nd = mpp.FindExactNode(bc);
                        if (nd != null) return nd;
                        nd = pmp.FindExactNode(bc);
                        if (nd != null) return nd;
                        nd = mmp.FindExactNode(bc);
                        if (nd != null) return nd;
                        nd = ppm.FindExactNode(bc);
                        if (nd != null) return nd;
                        nd = mpm.FindExactNode(bc);
                        if (nd != null) return nd;
                        nd = pmm.FindExactNode(bc);
                        if (nd != null) return nd;
                        nd = mmm.FindExactNode(bc);
                        if (nd != null) return nd;
                    }
                }
                return null;
            }

            internal Node<T> FindNode(GeoPoint center)
            {
                if (cube.Contains(center))
                {
                    if (list != null) return this as Node<T>;
                    else if (ppp != null)
                    {
                        Node<T> nd;
                        nd = ppp.FindNode(center);
                        if (nd != null) return nd;
                        nd = mpp.FindNode(center);
                        if (nd != null) return nd;
                        nd = pmp.FindNode(center);
                        if (nd != null) return nd;
                        nd = mmp.FindNode(center);
                        if (nd != null) return nd;
                        nd = ppm.FindNode(center);
                        if (nd != null) return nd;
                        nd = mpm.FindNode(center);
                        if (nd != null) return nd;
                        nd = pmm.FindNode(center);
                        if (nd != null) return nd;
                        nd = mmm.FindNode(center);
                        if (nd != null) return nd;
                    }
                }
                return null;
            }
#region IComparer<Node<TT>> Members

            public int Compare(Node<TT> x, Node<TT> y)
            {
                int res = x.center.x.CompareTo(y.center.x);
                if (res == 0) res = x.center.y.CompareTo(y.center.y);
                if (res == 0) res = x.center.z.CompareTo(y.center.z);
                return res;
            }

#endregion
            internal void Split()
            {
                double halfSize = Geometry.NextDouble(size / 2.0);
                ppp = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y + halfSize, center.z + halfSize), halfSize);
                mpp = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y + halfSize, center.z + halfSize), halfSize);
                pmp = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y - halfSize, center.z + halfSize), halfSize);
                mmp = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y - halfSize, center.z + halfSize), halfSize);
                ppm = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y + halfSize, center.z - halfSize), halfSize);
                mpm = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y + halfSize, center.z - halfSize), halfSize);
                pmm = new Node<TT>(root, this, new GeoPoint(center.x + halfSize, center.y - halfSize, center.z - halfSize), halfSize);
                mmm = new Node<TT>(root, this, new GeoPoint(center.x - halfSize, center.y - halfSize, center.z - halfSize), halfSize);
                // die vorhanden Objekte aufteilen (for ist marginal schneller als foreach)
                foreach (TT item in list)
                {
                    ppp.AddObject(item);
                    mpp.AddObject(item);
                    pmp.AddObject(item);
                    mmp.AddObject(item);
                    ppm.AddObject(item);
                    mpm.AddObject(item);
                    pmm.AddObject(item);
                    mmm.AddObject(item);
                }
                // die Liste wird nicht mehr gebraucht
                list = null;
            }
            internal bool IsNeighbour(Node<T> other)
            {
                Node<T> smaller, bigger;
                if (deepth > other.deepth)
                {
                    smaller = this as Node<T>;
                    bigger = other;
                }
                else
                {   // wenn gleich dann auch egal
                    bigger = this as Node<T>;
                    smaller = other;
                }
                GeoPoint p = smaller.center;
                double w = 2 * smaller.size;
                if (smaller.center.x < bigger.center.x - bigger.size) p.x += w;
                else if (smaller.center.x > bigger.center.x + bigger.size) p.x -= w;
                else if (smaller.center.y < bigger.center.y - bigger.size) p.y += w;
                else if (smaller.center.y > bigger.center.y + bigger.size) p.y -= w;
                else if (smaller.center.z < bigger.center.z - bigger.size) p.z += w;
                else if (smaller.center.z > bigger.center.z + bigger.size) p.z -= w;
                return bigger.cube.Contains(p);
            }
#region IComparable<Node<TT>> Members

            int IComparable<Node<TT>>.CompareTo(Node<TT> other)
            {
                int res = center.x.CompareTo(other.center.x);
                if (res == 0) res = center.y.CompareTo(other.center.y);
                if (res == 0) res = center.z.CompareTo(other.center.z);
                return res;
            }

#endregion

            internal void GetAllObjects(Set<T> addToList)
            {
                if (list != null)
                {
                    foreach (T item in list)
                    {
                        addToList.Add(item);
                    }
                }
                else if (ppp != null)
                {
                    ppp.GetAllObjects(addToList);
                    mpp.GetAllObjects(addToList);
                    pmp.GetAllObjects(addToList);
                    mmp.GetAllObjects(addToList);
                    ppm.GetAllObjects(addToList);
                    mpm.GetAllObjects(addToList);
                    pmm.GetAllObjects(addToList);
                    mmm.GetAllObjects(addToList);
                }
            }

            internal void GetDeepest(ref Node<TT> bestCandidate)
            {
                if (list != null)
                {
                    if (deepth >= bestCandidate.deepth && list.Count > 0)
                    {
                        if (deepth == bestCandidate.deepth)
                        {
                            if (bestCandidate.list == null || bestCandidate.list.Count < list.Count) bestCandidate = this;
                        }
                        else
                        {
                            bestCandidate = this;
                        }
                    }
                }
                else if (ppp != null)
                {
                    ppp.GetDeepest(ref bestCandidate);
                    mpp.GetDeepest(ref bestCandidate);
                    pmp.GetDeepest(ref bestCandidate);
                    mmp.GetDeepest(ref bestCandidate);
                    ppm.GetDeepest(ref bestCandidate);
                    mpm.GetDeepest(ref bestCandidate);
                    pmm.GetDeepest(ref bestCandidate);
                    mmm.GetDeepest(ref bestCandidate);
                }
            }

            internal void Shrink()
            {
                if (list != null) return;
            }
        }

        //internal void RemoveAndShrink(IEnumerable<T> toRemove)
        //{
        //    foreach (T item in toRemove)
        //    {
        //        RemoveObject(item);
        //    }
        //    Shrink();
        //}
        //internal void Shrink()
        //{
        //    node.Shrink();
        //}
        internal HashSet<T> GetDeepest()
        {
            Node<T> bestCandidate = node;
            node.GetDeepest(ref bestCandidate);
            return bestCandidate.list;
        }

        public OctTree()
        {
        }
        public void Initialize(BoundingCube ext, double precision)
        {
            this.precision = precision;
            double size = ext.MaxSide / 2.0;
            double minsize = size / 1000;
            size += minsize;
            GeoPoint center = ext.GetCenter();
            if ((ext.Zmax - ext.Zmin) < (size / 1000))
            {   // im Wesentlichen 2-dimensional
                center.z -= minsize / 2; // ein bisschen verrücken, damit bei einer 2dimensionalen
                // Zeichnung die Objekte nicht alle genau auf der Fläche zwischen oberer und unterer Hälfte liegen
            }
            node = new Node<T>(this, center, size * 1.01); // 1% größer
        }
        public void InitializePrecise(BoundingCube ext, double precision)
        {
            this.precision = precision;
            double size = ext.MaxSide / 2.0;
            GeoPoint center = ext.GetCenter();
            node = new Node<T>(this, center, size);
        }
        /// <summary>
        /// Constructs an octtree providing an initial size and a precision
        /// </summary>
        /// <param name="ext">Initial size of the tree. Objects beeng added later may exceed this cube</param>
        /// <param name="precision">Precision, used internally</param>
        public delegate bool SplitTestFunction(Node<T> node, T objectToAdd);
        public OctTree(BoundingCube ext, double precision, SplitTestFunction splitTest = null)
        {
            this.precision = precision;
            double size = ext.MaxSide / 2.0;
            double minsize = size / 1000;
            size += minsize;
            GeoPoint center = ext.GetCenter();
            if ((ext.Zmax - ext.Zmin) < (size / 1000))
            {   // im Wesentlichen 2-dimensional
                center.z -= minsize / 2; // ein bisschen verrücken, damit bei einer 2dimensionalen
                // Zeichnung die Objekte nicht alle genau auf der Fläche zwischen oberer und unterer Hälfte liegen
            }
            node = new Node<T>(this, center, size * 1.01); // 1% größer
            this.splitTest = splitTest;
        }
        /// <summary>
        /// Add the provided object to the tree. This may split some nodes and cause calls to the method of other objects already in the tree.
        /// </summary>
        /// <param name="objectToAdd">Object beeing added</param>
        public void AddObject(T objectToAdd)
        {   // an dieser Stelle könnte man ein neues Objekt machen, welches objectToAdd enthält und einen Stempel
            // fürs Iterieren. Node müsste dann entsprechend geändert werden
            BoundingCube ext = objectToAdd.GetExtent(precision);
#if DEBUG
            if (!ext.IsValid || !node.cube.GetCenter().IsValid)
            {

            }
#endif
            if (node.cube.Contains(ext))
            {
                node.AddObject(objectToAdd);
            }
            else
            {   // der Baum muss erweitert werden
                // nach welcher Richtung? es gibt 8 Möglichkeiten
                GeoPoint objectCenter = ext.GetCenter();
                GeoPoint nodeCenter = node.cube.GetCenter();
                node = node.extend((objectCenter.x < nodeCenter.x), (objectCenter.y < nodeCenter.y), (objectCenter.z < nodeCenter.z));
                AddObject(objectToAdd);
            }
        }

        public void AddObjectAsync(T objectToAdd)
        {   // an dieser Stelle könnte man ein neues Objekt machen, welches objectToAdd enthält und einen Stempel
            // fürs Iterieren. Node müsste dann entsprechend geändert werden
            BoundingCube ext = objectToAdd.GetExtent(precision);
            if (node.cube.Contains(ext))
            {
                node.AddObjectAsync(objectToAdd);
            }
            else
            {   // der Baum muss erweitert werden
                // nach welcher Richtung? es gibt 8 Möglichkeiten
                GeoPoint objectCenter = ext.GetCenter();
                GeoPoint nodeCenter = node.cube.GetCenter();
                node = node.extend((objectCenter.x < nodeCenter.x), (objectCenter.y < nodeCenter.y), (objectCenter.z < nodeCenter.z));
                AddObjectAsync(objectToAdd);
            }
        }
        public void AddMany(IEnumerable<T> objectsToAdd)
        {
            foreach (T item in objectsToAdd)
            {
                AddObject(item);
            }
        }
        /// <summary>
        /// Remove object from the tree
        /// </summary>
        /// <param name="objectToRemove">object to bee removed</param>
        public void RemoveObject(T objectToRemove)
        {
            node.RemoveObject(objectToRemove);
        }
        /// <summary>
        /// Returns an array of objects which contains all objects that are close to the provided line. It may also contain some objects
        /// that have a greater distance than <paramref name="maxdist"/> to the line.
        /// </summary>
        /// <param name="start">Starting point of the line</param>
        /// <param name="dir">Direction of the line</param>
        /// <param name="maxdist">Maximum distance to the line</param>
        /// <returns>Array of all objects which match the criterion</returns>
        public T[] GetObjectsFromLine(GeoPoint start, GeoVector dir, double maxdist)
        {
            Set<T> addToList = new Set<T>();
            node.GetObjectsFromLine(start, dir, maxdist, addToList);
            return addToList.ToArray();
        }
        /// <summary>
        /// Returns an array of all objects that interfere with the provided rectangle in repect to the provided <see cref="Projection"/>. 
        /// It may also contain some objects which don't interfere but are close to the rectangle, if <paramref name="onlyInside"/> is false.
        /// </summary>
        /// <param name="projection">The projection</param>
        /// <param name="rect">The rectangle, usually from a selection</param>
        /// <param name="onlyInside">If true, only objects completely inside the rectangle are returned</param>
        /// <returns>Array of all objects which match the criterion</returns>
        public T[] GetObjectsFromRect(Projection projection, BoundingRect rect, bool onlyInside)
        {
            //GeoObjectList dbg = DebugCheck(delegate(IOctTreeInsertable[] theList, BoundingCube bc) { return bc.Interferes(projection, rect); });
            Set<T> addToList = new Set<T>();
            node.GetObjectsFromRect(projection, rect, onlyInside, addToList);
            return addToList.ToArray();
        }
        /// <summary>
        /// Returns an array of all objects that interfere with the provided <see cref="Projection.PickArea"/> area. If <paramref name="onlyInside"/> is true
        /// only objects inside the frustum or box of the <paramref name="area"/> are returned, otherwise there may also be objects
        /// that are aoutside this area.
        /// </summary>
        /// <param name="area">the frustum or box defining the selection</param>
        /// <param name="onlyInside">True to only return objects completely inside the area</param>
        /// <returns>Array of all objects which match the criterion</returns>
        public T[] GetObjectsFromRect(Projection.PickArea area, bool onlyInside)
        {
            //GeoObjectList dbg = DebugCheck(delegate(IOctTreeInsertable[] theList, BoundingCube bc) { return bc.Interferes(projection, rect); });
            Set<T> addToList = new Set<T>();
            node.GetObjectsFromRect(area, onlyInside, addToList);
            return addToList.ToArray();
        }
        /// <summary>
        /// Returns all objects that interfere or are close to the provided box.
        /// </summary>
        /// <param name="box">Box for the selection</param>
        /// <returns>Array of all objects which match the criterion</returns>
        public T[] GetObjectsFromBox(BoundingCube box)
        {
            //GeoObjectList dbg = DebugCheck(delegate(IOctTreeInsertable[] theList, BoundingCube bc) { return bc.Interferes(projection, rect); });
            Set<T> addToList = new Set<T>();
            node.GetObjectsFromBox(box, addToList, null);
            return addToList.ToArray();
        }
        /// <summary>
        /// Returns all objects that interfere or are close to the provided box and accepted by the <paramref name="filter"/>.
        /// </summary>
        /// <param name="box">Box specifying the selection</param>
        /// <param name="filter">Filter restriction the result</param>
        /// <returns>Array of all objects which match the criterion</returns>
        public T[] GetObjectsFromBox(BoundingCube box, Filter filter)
        {
            //GeoObjectList dbg = DebugCheck(delegate(IOctTreeInsertable[] theList, BoundingCube bc) { return bc.Interferes(projection, rect); });
            Set<T> addToList = new Set<T>();
            node.GetObjectsFromBox(box, addToList, filter);
            return addToList.ToArray();
        }
        /// <summary>
        /// Returns all nodes of this tree that interfere with the provided box and pass the filter test.
        /// </summary>
        /// <param name="box">Restricting box</param>
        /// <param name="filter">Additional filter (may be null)</param>
        /// <returns>All nodes which match the criterion</returns>
        public Node<T>[] GetNodesFromBox(BoundingCube box, FilterNode filter)
        {
            //GeoObjectList dbg = DebugCheck(delegate(IOctTreeInsertable[] theList, BoundingCube bc) { return bc.Interferes(projection, rect); });
            List<Node<T>> addToList = new List<Node<T>>();
            node.GetNodesFromBox(box, addToList, filter);
            return addToList.ToArray();
        }
        /// <summary>
        /// Returns all objects that are close to the provided point.
        /// </summary>
        /// <param name="p">Point for selection</param>
        /// <returns>Array of all objects which match the criterion</returns>
        public T[] GetObjectsFromPoint(GeoPoint p)
        {
            //GeoObjectList dbg = DebugCheck(delegate(IOctTreeInsertable[] theList, BoundingCube bc) { return bc.Interferes(projection, rect); });
            Set<T> addToList = new Set<T>();
            if (node != null) node.GetObjectsFromPoint(p, addToList);
            return addToList.ToArray();
        }
        /// <summary>
        /// Returns all objects that are close to the provided plane
        /// </summary>
        /// <param name="plane">Plane for selection</param>
        /// <returns>All objects close to the plane</returns>
        public T[] GetObjectsFromPlane(Plane plane)
        {
            Set<T> addToList = new Set<T>();
            node.GetObjectsFromPlane(plane, addToList);
            return addToList.ToArray();
        }
        /// <summary>
        /// Returns all objects that are close to the provided object.
        /// </summary>
        /// <param name="closeToThis">Object which neighbours are searched</param>
        /// <returns>All objects close to <paramref name="closeToThis"/></returns>
        public T[] GetObjectsCloseTo(IOctTreeInsertable closeToThis)
        {
            Set<T> addToList = new Set<T>();
            if (node != null) node.GetObjectsCloseTo(closeToThis, addToList);
            return addToList.ToArray();
        }
        /// <summary>
        /// returns the extend of the root node.
        /// </summary>
        public BoundingCube Extend
        {
            get
            {
                return node.cube;
            }
        }
        /// <summary>
        /// Returns true if the tree is empty
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return node.list == null && node.mmm == null;
            }
        }
        /// <summary>
        /// Returns all nodes close to the provided object.
        /// </summary>
        /// <param name="closeToThis">Object for selection</param>
        /// <returns>All nodes interfering with the provided object</returns>
        protected Node<T>[] GetNodesCloseTo(IOctTreeInsertable closeToThis)
        {
            List<Node<T>> res = new List<Node<T>>();
            res.AddRange(node.GetNodesCloseTo(closeToThis));
            return res.ToArray();
        }
        /// <summary>
        /// Find the node containing the provided point.
        /// </summary>
        /// <param name="center">the point</param>
        /// <returns>the node or null if outside</returns>
        public Node<T> FindNode(GeoPoint center)
        {
            return node.FindNode(center);
        }
        /// <summary>
        /// Find the node which corresponds exactely to the provided boundingcube
        /// </summary>
        /// <param name="bc"></param>
        /// <returns></returns>
        public Node<T> FindExactNode(BoundingCube bc)
        {
            return node.FindExactNode(bc);
        }
        /// <summary>
        /// Enumeration of the 6 sides of a cube.
        /// </summary>
        protected enum Side { left, right, bottom, top, front, back };
        /// <summary>
        /// Delegate definition of a filtering method restricting <see cref="IOctTreeInsertable"/> objects.
        /// </summary>
        /// <param name="toCheck">The object beeing checked</param>
        /// <returns>true if accepted, false if rejected</returns>
        public delegate bool Filter(T toCheck);
        /// <summary>
        /// Delegate definition of a filtering method restricting <see cref="Node<typeparamref name="T"/>"/>s.
        /// </summary>
        /// <param name="toCheck">The node beeing checked</param>
        /// <returns>true if accepted, false if rejected</returns>
        public delegate bool FilterNode(Node<T> toCheck);
        /// <summary>
        /// Returns all neighbours to the provided node. The result is sortet in a 2-dimensional array where the first
        /// index defines the side according to <see cref="Side"/>.
        /// </summary>
        /// <param name="node">The node which neighbours are requested</param>
        /// <param name="filter">A optinal filter or null if unfiltered</param>
        /// <returns>The neighbours</returns>
        protected T[][] GetNeighbours(Node<T> node, Filter filter)
        {
            BoundingCube bc = node.cube;
            T[][] res = new T[6][]; // alle Seiten
            res[(int)Side.left] = GetObjectsFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin - precision, bc.Ymin + precision, bc.Zmin + precision),
                    new GeoPoint(bc.Xmin - precision, bc.Ymax - precision, bc.Zmax - precision)), filter);
            res[(int)Side.right] = GetObjectsFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmax + precision, bc.Ymin + precision, bc.Zmin + precision),
                    new GeoPoint(bc.Xmax + precision, bc.Ymax - precision, bc.Zmax - precision)));
            res[(int)Side.front] = GetObjectsFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin + precision, bc.Ymin - precision, bc.Zmin + precision),
                    new GeoPoint(bc.Xmax - precision, bc.Ymin - precision, bc.Zmax - precision)));
            res[(int)Side.back] = GetObjectsFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin + precision, bc.Ymax + precision, bc.Zmin + precision),
                    new GeoPoint(bc.Xmax - precision, bc.Ymax + precision, bc.Zmax - precision)));
            res[(int)Side.bottom] = GetObjectsFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin + precision, bc.Ymin + precision, bc.Zmin - precision),
                    new GeoPoint(bc.Xmax - precision, bc.Ymax - precision, bc.Zmin - precision)));
            res[(int)Side.top] = GetObjectsFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin + precision, bc.Ymin + precision, bc.Zmax + precision),
                    new GeoPoint(bc.Xmax - precision, bc.Ymax - precision, bc.Zmax + precision)));
            return res;
        }
        /// <summary>
        /// Returns all neighbour nodes to the provided node. The result is sortet in a 2-dimensional array where the first
        /// index defines the side according to <see cref="Side"/>.
        /// </summary>
        /// <param name="node">The node which neighbours are requested</param>
        /// <param name="filter">A optinal filter or null if unfiltered</param>
        /// <returns>The neighbour nodes</returns>
        protected Node<T>[][] GetNeighbourNodes(Node<T> node, FilterNode filter)
        {
            BoundingCube bc = node.cube;
            Node<T>[][] res = new Node<T>[6][]; // alle Seiten
            res[(int)Side.left] = GetNodesFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin - precision, bc.Ymin + precision, bc.Zmin + precision),
                    new GeoPoint(bc.Xmin - precision, bc.Ymax - precision, bc.Zmax - precision)), filter);
            res[(int)Side.right] = GetNodesFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmax + precision, bc.Ymin + precision, bc.Zmin + precision),
                    new GeoPoint(bc.Xmax + precision, bc.Ymax - precision, bc.Zmax - precision)), filter);
            res[(int)Side.front] = GetNodesFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin + precision, bc.Ymin - precision, bc.Zmin + precision),
                    new GeoPoint(bc.Xmax - precision, bc.Ymin - precision, bc.Zmax - precision)), filter);
            res[(int)Side.back] = GetNodesFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin + precision, bc.Ymax + precision, bc.Zmin + precision),
                    new GeoPoint(bc.Xmax - precision, bc.Ymax + precision, bc.Zmax - precision)), filter);
            res[(int)Side.bottom] = GetNodesFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin + precision, bc.Ymin + precision, bc.Zmin - precision),
                    new GeoPoint(bc.Xmax - precision, bc.Ymax - precision, bc.Zmin - precision)), filter);
            res[(int)Side.top] = GetNodesFromBox(
                new BoundingCube(
                    new GeoPoint(bc.Xmin + precision, bc.Ymin + precision, bc.Zmax + precision),
                    new GeoPoint(bc.Xmax - precision, bc.Ymax - precision, bc.Zmax + precision)), filter);
            return res;
        }
        /// <summary>
        /// Iterator that iterates over all leaves of the tree
        /// </summary>
        internal IEnumerable<Node<T>> Leaves
        {
            get
            {
                if (node.list != null) yield return node;
                else
                {
                    foreach (Node<T> nd in node.Leaves) yield return nd;
                }
            }
        }
        internal IEnumerable<HashSet<T>> Lists
        {
            get
            {
                if (node.list != null) yield return node.list;
                else
                {
                    foreach (HashSet<T> list in node.Lists) yield return list;
                }
            }
        }
#if DEBUG
        internal GeoObjectList Debug
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                node.Debug(res);
                T[] all = this.GetAllObjects();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is IGeoObject) res.Add(all[i] as IGeoObject);
                }
                return res;
            }
        }
        internal delegate bool CheckThis(IOctTreeInsertable[] theList, BoundingCube bc);
        internal GeoObjectList DebugCheck(CheckThis checkThis)
        {
            GeoObjectList res = new GeoObjectList();
            node.Debug(res, checkThis);
            return res;
        }
        internal GeoObjectList DebugNonEmptyCubes
        {
            get
            {
                GeoObjectList res = new GeoObjectList();
                foreach (Node<T> node in Leaves)
                {
                    if (node.list != null && node.list.Count > 0)
                    {
                        res.Add(node.cube.AsBox);
                    }
                }
                return res;
            }
        }
        public int NonEmptyCubesCount
        {
            get
            {
                int res = 0;
                foreach (Node<T> node in Leaves)
                {
                    if (node.list != null && node.list.Count > 0)
                    {
                        ++res;
                    }
                }
                return res;
            }
        }
        internal GeoObjectList DebugCubesByObject(T obj)
        {
            GeoObjectList res = new GeoObjectList();
            foreach (Node<T> node in Leaves)
            {
                if (node.list != null)
                {
                    if (node.list.Contains(obj))
                    {
                        res.Add(node.cube.AsBox);
                    }
                }
            }
            return res;
        }
        public GeoObjectList DebugCubesByObject(Set<T> objs)
        {
            GeoObjectList res = new GeoObjectList();
            foreach (Node<T> node in Leaves)
            {
                if (node.list != null)
                {
                    if (objs.Intersection(new Set<T>(node.list)).Count > 0)
                    {
                        res.Add(node.cube.AsBox);
                    }
                }
            }
            return res;
        }
        internal GeoObjectList DebugCubesByObject(IOctTreeInsertable obj)
        {
            GeoObjectList res = new GeoObjectList();
            Node<T>[] nodes = GetNodesCloseTo(obj);
            for (int i = 0; i < nodes.Length; i++)
            {
                res.Add(nodes[i].cube.AsBox);
            }
            return res;
        }
#endif
        /// <summary>
        /// Criterion whether to split a node when it contains too many leaves. The default implemenation yield a dynamically balanced
        /// tree which allows more leaves in deeper nodes.
        /// </summary>
        /// <param name="node">The node beeing checked</param>
        /// <param name="objectToAdd">The object beeing added</param>
        /// <returns>true, if node should be splitted, false otherwise</returns>
        protected virtual bool SplitNode(Node<T> node, T objectToAdd)
        {
            if (splitTest != null) return splitTest(node, objectToAdd);
            return node.list.Count > (1 << (node.deepth)); // dynamische Anpassung der Listenlänge
            // evtl. kann hier ein Faktor mit eingehen, jetzt sind im Root nur ein Objekt
            // und in der n-ten Tiefe 2 hoch n Objekte erlaubt
        }

        //protected virtual bool FilterHitTest(object objectToAdd, OctTree<T>.Node<T> node)
        //{
        //    return false; 
        //}

        public T[] GetAllObjects()
        {
            Set<T> res = new Set<T>();
            if (!IsEmpty) node.GetAllObjects(res);
            return res.ToArray();
        }
    }
}
