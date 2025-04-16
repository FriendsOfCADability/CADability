using CADability.Curve2D;
using CADability.GeoObject;
using System;
using System.Collections;
using System.Collections.Generic;
using Wintellect.PowerCollections;

namespace CADability
{
    /// <summary>
    /// Ein Interface für Objekte, die in einen QuadTree eingefügt werden können.
    /// Der QuadTree kann also nicht nur IGeoObject Objekte aufnehmen, sondern alle
    /// Objekte, die IQuadTreeInsertable unterstützen.
    /// </summary>

    public interface IQuadTreeInsertable
    {
        /// <summary>
        /// Returns the extent of the two dimensional object
        /// </summary>
        /// <returns>extent</returns>
        BoundingRect GetExtent();
        /// <summary>
        /// Determins whether the rectangle <paramref name="rect"/> coincides with the object
        /// </summary>
        /// <param name="rect">testing rectangle</param>
        /// <param name="includeControlPoints">also check control points (e.g. spline control points, circle center)</param>
        /// <returns>true, if coinciding, false otherwise</returns>
        bool HitTest(ref BoundingRect rect, bool includeControlPoints);
        /// <summary>
        /// A backpointer to the object represented by this item
        /// </summary>
        object ReferencedObject { get; }
    }
    /// <summary>
    /// Eine Collection, die sicherstellt, dass jedes Objekt nur einmal eingefügt wird.
    /// Leider gibt es so etwas wie "set" aus STL nicht
    /// </summary>
    internal class QuadTreeInsertableHashtable : Hashtable
    {
        public void Add(IQuadTreeInsertable ObjectToAdd)
        {
            if (!base.ContainsKey(ObjectToAdd))
                base.Add(ObjectToAdd, null);
        }
    }

    internal class QuadTreeItemCollection : IQuadTreeInsertable
    {
        IQuadTreeInsertable[] list;
        IGeoObject refgeo;
        public QuadTreeItemCollection(IGeoObject refgeo, IQuadTreeInsertable[] list)
        {
            this.list = list;
            this.refgeo = refgeo;
        }
        #region IQuadTreeInsertable Members

        BoundingRect IQuadTreeInsertable.GetExtent()
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            for (int i = 0; i < list.Length; ++i)
            {
                res.MinMax(list[i].GetExtent());
            }
            return res;
        }

        bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
        {
            for (int i = 0; i < list.Length; ++i)
            {
                if (list[i].HitTest(ref rect, includeControlPoints)) return true;
            }
            return false;
        }

        object IQuadTreeInsertable.ReferencedObject
        {
            get { return refgeo; }
        }

        #endregion
    }

    /// <summary>
    /// A QuadTree of 2-dimensional objects that implement IQuadTreeInsertable.
    /// This Class might change in future, so the use of this class is deprecated
    /// </summary>

    public class QuadTree : IEnumerable
    {
        /// <summary>
        /// Ein Knoten im QuadTree. Er enthält entweder eine Liste von Objekten oder
        /// vier Unterknoten, aber nie beides gleichzeitig.
        /// </summary>
        internal class QuadNode
        {
            /// <summary>
            /// Einfache typensichere Liste für IQuadTreeInsertable
            /// </summary>
            public class IQuadTreeInsertableCollection : CollectionBase
            {
                public void Add(IQuadTreeInsertable ObjectToAdd)
                {
                    List.Add(ObjectToAdd);
                }
                public void Remove(int index)
                {
                    if (index > Count - 1 || index < 0)
                    {
                        throw new ArgumentOutOfRangeException("index");
                    }
                    else
                    {
                        List.RemoveAt(index);
                    }
                }
                public void Remove(IQuadTreeInsertable ObjectToRemove)
                {
                    int index = List.IndexOf(ObjectToRemove);
                    if (index >= 0)
                    {
                        List.RemoveAt(index);
                    }
                }
                public IQuadTreeInsertable Item(int Index)
                {
                    return (IQuadTreeInsertable)List[Index];
                }
            }

            // die vier Unterknoten:
            public QuadNode TopRight;
            public QuadNode TopLeft;
            public QuadNode BottomLeft;
            public QuadNode BottomRight;
            public List<IQuadTreeInsertable> ObjectList; // die Objekte als Blätter
            public QuadNode Parent; // Parent, ist beim Root null
            public GeoPoint2D Center; // Mittelpunkt des Rechtecks
            public double Size; // halbe Breite und Höhe des Rechtecks
            public BoundingRect Rect; // das Rechteck selbst
            public QuadTree TheTree; // der QuadTree mit seinen Infos zu MaxListlen u.s.w.
            public QuadNode(QuadTree TheTree)
            {
                ObjectList = new List<IQuadTreeInsertable>();
                this.TheTree = TheTree;
            }

            public QuadNode(QuadNode Parent, GeoPoint2D Center, double Size)
            {
                ObjectList = new List<IQuadTreeInsertable>();
                this.Parent = Parent;
                this.Center = Center;
                this.Size = Size;
                this.Rect = new BoundingRect(Center, Size, Size);
                if (Parent != null) TheTree = Parent.TheTree;
            }
            public QuadNode(QuadTree TheTree, GeoPoint2D Center, double Size)
            {
                ObjectList = new List<IQuadTreeInsertable>();
                this.Parent = null;
                this.Center = Center;
                this.Size = Size;
                this.Rect = new BoundingRect(Center, Size, Size);
                this.TheTree = TheTree;
            }

            public QuadTree GetRoot(ref int deepth)
            {
                // Das Root Objekt hat kein Parent Objekt
                if (Parent == null) return TheTree;
                ++deepth;
                return Parent.GetRoot(ref deepth);
            }

            internal void SetNodes(QuadNode qn0, QuadNode qn1, QuadNode qn2, QuadNode qn3)
            {
                TopRight = qn0;
                TopLeft = qn1;
                BottomLeft = qn2;
                BottomRight = qn3;
                TopRight.Parent = this;
                TopLeft.Parent = this;
                BottomLeft.Parent = this;
                BottomRight.Parent = this;
                ObjectList = null;
            }
            public void AddObject(IQuadTreeInsertable ObjectToAdd)
            {
                if (Parent == null && TopRight == null)
                {	// der Baum ist noch unreif, da er nur aus einer Liste besteht
                    // das von der Wurzel überdeckte Quadrat kann also noch beliebig manipuliert
                    // werden. Es soll 10% größer sein als alle Objekte in der Wurzel
                    BoundingRect ext = ObjectToAdd.GetExtent();
                    if (ext.IsEmpty()) return;
                    if (ObjectList.Count == 0)
                    {	// das erste Objekt wird eingefügt
                        if (TheTree.InitialSize > 0.0)
                        {
                            Rect = new BoundingRect(TheTree.InitialCenter, TheTree.InitialSize, TheTree.InitialSize);
                            if (ext <= Rect)
                            {
                                Center = TheTree.InitialCenter;
                                Size = TheTree.InitialSize;
                            }
                            else
                            {	// vorgegebenes Rechteck ist schon zu klein
                                Center = ext.GetCenter();
                                Size = Math.Max(ext.Width, ext.Height) / 2.0 * 1.1; // 10% größer
                                Rect = new BoundingRect(Center, Size, Size);
                            }
                        }
                        else
                        {
                            Center = ext.GetCenter();
                            Size = Math.Max(ext.Width, ext.Height) / 2.0 * 1.1; // 10% größer
                            if (Size == 0.0) Size = 1.0; // erstes Objekt ein Punkt!
                            Rect = new BoundingRect(Center, Size, Size);
                        }
                    }
                    else
                    {	// ein weiteres Objekt wird zugefügt
                        // wenn ein Rechteck vorgegeben ist und es groß genug ist,
                        // wird es hier nicht verändert
                        BoundingRect Total = new BoundingRect(Center, Size, Size);
                        ext = ext * 1.1; // 10% größer, wenn es aber leer ist (Punkt), dann greift folgende Zeile:
                        if (ext.Width == 0.0 && ext.Height == 0.0) ext.Inflate(Size / 1000);
                        Total.MinMax(ext);
                        Center = Total.GetCenter();
                        Size = Math.Max(Total.Width, Total.Height) / 2.0;
                        Rect = new BoundingRect(Center, Size, Size);
                    }
                }
                if (ObjectToAdd.HitTest(ref Rect, true))
                {
                    if (TopRight == null)
                    {	// es ist ein Blatt und kein Knoten
                        int deepth = 0;
                        QuadTree Root;
                        Root = GetRoot(ref deepth);
                        bool split = false;
                        if ((Root.MaxDeepth < 0 && (ObjectList.Count > (1 << deepth)))) split = true;
                        if ((Root.MaxDeepth > 0 && deepth < Root.MaxDeepth && ObjectList.Count >= Root.MaxListLen)) split = true;
                        if (split)
                        {	// das Blatt wird zum Knoten
                            // Untereinträge erzeugen
                            double HalfSize = Size / 2.0;
                            TopRight = new QuadNode(this, new GeoPoint2D(Center, HalfSize, HalfSize), HalfSize);
                            TopLeft = new QuadNode(this, new GeoPoint2D(Center, -HalfSize, HalfSize), HalfSize);
                            BottomLeft = new QuadNode(this, new GeoPoint2D(Center, -HalfSize, -HalfSize), HalfSize);
                            BottomRight = new QuadNode(this, new GeoPoint2D(Center, HalfSize, -HalfSize), HalfSize);
                            // die vorhanden Objekte aufteilen (for ist marginal schneller als foreach)
                            for (int i = 0; i < ObjectList.Count; ++i)
                            {
                                IQuadTreeInsertable qti = ObjectList[i];
                                TopRight.AddObject(qti);
                                TopLeft.AddObject(qti);
                                BottomLeft.AddObject(qti);
                                BottomRight.AddObject(qti);
                            }
                            // das neue Objekt ebenfalls zufügen
                            TopRight.AddObject(ObjectToAdd);
                            TopLeft.AddObject(ObjectToAdd);
                            BottomLeft.AddObject(ObjectToAdd);
                            BottomRight.AddObject(ObjectToAdd);
                            // die Liste wird nicht mehr gebraucht
                            ObjectList = null;
                        }
                        else
                        {	// es bleibt ein Blatt, einfach in die Liste damit
                            ObjectList.Add(ObjectToAdd);
                        }
                    }
                    else
                    {
                        TopRight.AddObject(ObjectToAdd);
                        TopLeft.AddObject(ObjectToAdd);
                        BottomLeft.AddObject(ObjectToAdd);
                        BottomRight.AddObject(ObjectToAdd);
                    }
                }
            }
            public void RemoveObject(IQuadTreeInsertable ObjectToRemove)
            {
                int deepth = 0;
                QuadTree root = GetRoot(ref deepth);
                bool direct = root.DirectMode;
                if (TopRight == null)
                {	// damit ist der Fall des Root Objektes, wenn noch kein Rechteck vorliegt, abgedeckt
                    // nicht sicher, ob es mehrfach drin sein kann
                    for (int i = ObjectList.Count - 1; i >= 0; --i)
                    {
                        if (direct)
                        {
                            if (ObjectList[i] == ObjectToRemove)
                            {
                                ObjectList.RemoveAt(i);
                            }
                        }
                        else
                        {
                            if (ObjectList[i].ReferencedObject == ObjectToRemove.ReferencedObject)
                            {
                                ObjectList.RemoveAt(i);
                            }
                        }
                    }
                }
                else if (ObjectToRemove.HitTest(ref Rect, true))
                {
                    TopRight.RemoveObject(ObjectToRemove);
                    TopLeft.RemoveObject(ObjectToRemove);
                    BottomLeft.RemoveObject(ObjectToRemove);
                    BottomRight.RemoveObject(ObjectToRemove);
                }
            }
            internal void DebugCount(ref int NumList, ref int NumEntries)
            {
                ++NumList;
                if (ObjectList != null) NumEntries += ObjectList.Count;
                if (TopRight != null)
                {
                    TopRight.DebugCount(ref NumList, ref NumEntries);
                    TopLeft.DebugCount(ref NumList, ref NumEntries);
                    BottomLeft.DebugCount(ref NumList, ref NumEntries);
                    BottomRight.DebugCount(ref NumList, ref NumEntries);
                }
            }
            public void GetObjectsFromRect(BoundingRect r, QuadTreeInsertableHashtable List)
            {
                if (!BoundingRect.Disjoint(r, Rect))
                {
                    if (TopRight != null)
                    {
                        TopRight.GetObjectsFromRect(r, List);
                        TopLeft.GetObjectsFromRect(r, List);
                        BottomLeft.GetObjectsFromRect(r, List);
                        BottomRight.GetObjectsFromRect(r, List);
                    }
                    else
                    {
                        foreach (IQuadTreeInsertable go in ObjectList) List.Add(go);
                    }
                }
            }
            public void GetObjectsFromRect(BoundingRect r, Set<IQuadTreeInsertable> List)
            {
                if (!BoundingRect.Disjoint(r, Rect))
                {
                    if (TopRight != null)
                    {
                        TopRight.GetObjectsFromRect(r, List);
                        TopLeft.GetObjectsFromRect(r, List);
                        BottomLeft.GetObjectsFromRect(r, List);
                        BottomRight.GetObjectsFromRect(r, List);
                    }
                    else
                    {
                        List.AddMany(ObjectList);
                    }
                }
            }
            public void GetObjectsCloseTo(IQuadTreeInsertable CloseToThis, QuadTreeInsertableHashtable List)
            {
                if (CloseToThis.HitTest(ref Rect, true))
                {
                    if (TopRight != null)
                    {
                        TopRight.GetObjectsCloseTo(CloseToThis, List);
                        TopLeft.GetObjectsCloseTo(CloseToThis, List);
                        BottomLeft.GetObjectsCloseTo(CloseToThis, List);
                        BottomRight.GetObjectsCloseTo(CloseToThis, List);
                    }
                    else
                    {
                        foreach (IQuadTreeInsertable go in ObjectList) List.Add(go);
                    }
                }
            }
        }

        /// <summary>
        /// Liefert alle Objekte im QuadTree, aber möglicherweise mehrfach. Um wirklich über 
        /// alle Objekte einzeln zu iterieren, müsste man sie erst in einer HashTable (als Keys)
        /// sammeln.
        /// </summary>
        internal class EnumerateNode : IEnumerator
        {
            private QuadNode EnumThisNode;
            private int Index;
            private EnumerateNode SubNode;
            public EnumerateNode(QuadNode EnumThisNode)
            {
                this.EnumThisNode = EnumThisNode;
                Index = -1;
            }
            private bool SetNextSubNode()
            {
                do
                {
                    ++Index;
                    switch (Index)
                    {
                        case 0: SubNode = new EnumerateNode(EnumThisNode.TopRight); break;
                        case 1: SubNode = new EnumerateNode(EnumThisNode.TopLeft); break;
                        case 2: SubNode = new EnumerateNode(EnumThisNode.BottomLeft); break;
                        case 3: SubNode = new EnumerateNode(EnumThisNode.BottomRight); break;
                        default: return false;
                    }
                } while (!SubNode.MoveNext());
                return true;
            }
            #region IEnumerator Members

            public void Reset()
            {
                Index = -1;
            }

            public object Current
            {
                get
                {
                    if (Index == -1) throw new InvalidOperationException();
                    if (SubNode == null)
                    {	// Blatt
                        if (Index < EnumThisNode.ObjectList.Count) return EnumThisNode.ObjectList[Index];
                        else throw new InvalidOperationException();
                    }
                    else
                    {
                        return SubNode.Current;
                    }
                }
            }

            public bool MoveNext()
            {
                if (EnumThisNode.TopRight == null)
                {	// Blatt
                    ++Index;
                    return (Index < EnumThisNode.ObjectList.Count);
                }
                else
                {
                    if (SubNode != null && SubNode.MoveNext()) return true;
                    return SetNextSubNode();
                }
            }

            #endregion
        }
        internal QuadNode mRoot;
        public int MaxListLen;
        public int MaxDeepth;
        public bool DirectMode;
        public GeoPoint2D InitialCenter;
        public double InitialSize;
        public QuadTree()
        {
            MaxListLen = 20;
            MaxDeepth = 9;
            mRoot = new QuadNode(this);
            mRoot.TheTree = this;
            InitialCenter = new GeoPoint2D(0.0, 0.0);
            InitialSize = 0.0; // d.h. nicht gegeben
        }
        /// <summary>
        /// Erzeugt einen neuen QuadTree, dessen (anfängliche) Ausdehnung gegeben ist.
        /// </summary>
        /// <param name="InitialRect"></param>
        public QuadTree(BoundingRect InitialRect)
            : this()
        {
            InitialCenter = InitialRect.GetCenter();
            InitialSize = Math.Max(InitialRect.Width, InitialRect.Height) / 2.0 * 1.1;
            // damit eine senkrechte oder waagrechte Linie nicht genau auf die Mittlere kante des
            // Quadtrees zu liegen kommt:
            InitialCenter.x += InitialSize * 0.001238475635;
            InitialCenter.y += InitialSize * 0.001274536285;
        }
        public void AddObject(IQuadTreeInsertable ObjectToAdd)
        {
            if (mRoot.TopRight != null)
            {
                // abprüfen, ob der Root nach oben erweitert werden muss.
                // wenn ja, dann 4 neue SubNodes erzeugen und die bestehenden
                // in einen (den richtigen) Subnode einfügen
                // ansonsten einfach AddObject von den vieren aufrufen
                BoundingRect ObjectExtent = ObjectToAdd.GetExtent();
                if (ObjectExtent.IsEmpty()) return;
                BoundingRect NodeExtent = new BoundingRect(mRoot.Center, mRoot.Size, mRoot.Size);
                if (ObjectExtent <= NodeExtent)
                {	// das Objekt passt ganz hier rein, also einfach einfügen
                    mRoot.AddObject(ObjectToAdd);
                }
                else
                {
                    if (ObjectExtent.Left < NodeExtent.Left)
                    {	// Überschreitung nach links
                        if (ObjectExtent.Bottom < NodeExtent.Bottom)
                        {	// Überschreitung nach unten
                            GeoPoint2D Center = mRoot.Center;
                            double Size = mRoot.Size;
                            Center.x -= Size;
                            Center.y -= Size;
                            double HalfSize = Size;
                            Size *= 2.0;
                            QuadNode OldRoot = mRoot;
                            mRoot = new QuadNode(this, Center, Size);
                            mRoot.SetNodes(
                                OldRoot,
                                new QuadNode(this, new GeoPoint2D(Center, -HalfSize, HalfSize), HalfSize),
                                new QuadNode(this, new GeoPoint2D(Center, -HalfSize, -HalfSize), HalfSize),
                                new QuadNode(this, new GeoPoint2D(Center, HalfSize, -HalfSize), HalfSize));
                        }
                        else
                        {	// überschreitung nach oben (oder keine vertikale Überschreitung)
                            GeoPoint2D Center = mRoot.Center;
                            double Size = mRoot.Size;
                            Center.x -= Size;
                            Center.y += Size;
                            double HalfSize = Size;
                            Size *= 2.0;
                            QuadNode OldRoot = mRoot;
                            mRoot = new QuadNode(this, Center, Size);
                            mRoot.SetNodes(
                                new QuadNode(this, new GeoPoint2D(Center, HalfSize, HalfSize), HalfSize),
                                new QuadNode(this, new GeoPoint2D(Center, -HalfSize, HalfSize), HalfSize),
                                new QuadNode(this, new GeoPoint2D(Center, -HalfSize, -HalfSize), HalfSize),
                                OldRoot);
                        }
                    }
                    else
                    {	// Überschreitung nach rechts (oder keine horizontale Überschreitung)
                        if (ObjectExtent.Bottom < NodeExtent.Bottom)
                        {	// Überschreitung nach unten
                            GeoPoint2D Center = mRoot.Center;
                            double Size = mRoot.Size;
                            Center.x += Size;
                            Center.y -= Size;
                            double HalfSize = Size;
                            Size *= 2.0;
                            QuadNode OldRoot = mRoot;
                            mRoot = new QuadNode(this, Center, Size);
                            mRoot.SetNodes(
                                new QuadNode(this, new GeoPoint2D(Center, HalfSize, HalfSize), HalfSize),
                                OldRoot,
                                new QuadNode(this, new GeoPoint2D(Center, -HalfSize, -HalfSize), HalfSize),
                                new QuadNode(this, new GeoPoint2D(Center, HalfSize, -HalfSize), HalfSize));
                        }
                        else
                        {	// überschreitung nach oben (oder keine vertikale Überschreitung)
                            GeoPoint2D Center = mRoot.Center;
                            double Size = mRoot.Size;
                            Center.x += Size;
                            Center.y += Size;
                            double HalfSize = Size;
                            Size *= 2.0;
                            QuadNode OldRoot = mRoot;
                            mRoot = new QuadNode(this, Center, Size);
                            mRoot.SetNodes(
                                new QuadNode(this, new GeoPoint2D(Center, HalfSize, HalfSize), HalfSize),
                                new QuadNode(this, new GeoPoint2D(Center, -HalfSize, HalfSize), HalfSize),
                                OldRoot,
                                new QuadNode(this, new GeoPoint2D(Center, HalfSize, -HalfSize), HalfSize));
                        }
                    }
                    // der Root ist jetzt noch oben gewachsen, also hier einen erneuten Versuch, 
                    // das Objekt einzufügen, kann beliebig rekursiv werden
                    AddObject(ObjectToAdd);
                }
            }
            else
            {
                // keine Überschreitung, alles weitere kann das QuadNode Objekt selbst regeln
                mRoot.AddObject(ObjectToAdd);
            }
        }

        public void RemoveObject(IQuadTreeInsertable ObjectToRemove)
        {
            mRoot.RemoveObject(ObjectToRemove);
        }
        public void DebugPrint()
        {
            int NumList, NumEntries;
            NumList = 0;
            NumEntries = 0;
            mRoot.DebugCount(ref NumList, ref NumEntries);
            // System.Diagnostics.Trace.WriteLine("QuadTree: Anzahl der Listen:" + NumList.ToString() + " Anzahl der Einträge:" + NumEntries.ToString());
        }
        public ICollection GetObjectsFromRect(BoundingRect r)
        {
            QuadTreeInsertableHashtable List = new QuadTreeInsertableHashtable();
            // Set<IQuadTreeInsertable> List = new Set<IQuadTreeInsertable>(); // Hashtable schneller als Set
            mRoot.GetObjectsFromRect(r, List);
            return List.Keys;
            // return List;
        }
        public ICollection GetObjectsInsideRect(BoundingRect r)
        {
            QuadTreeInsertableHashtable List = new QuadTreeInsertableHashtable();
            mRoot.GetObjectsFromRect(r, List);
            ArrayList result = new ArrayList(List.Count);
            foreach (IQuadTreeInsertable qi in List.Keys)
            {
                if (qi.GetExtent() <= r) result.Add(qi);
            }
            return result;
        }
        public ICollection GetObjectsCloseTo(IQuadTreeInsertable CloseToThis)
        {
            QuadTreeInsertableHashtable List = new QuadTreeInsertableHashtable();
            mRoot.GetObjectsCloseTo(CloseToThis, List);
            return List.Keys;
        }
        public void Compact()
        {
        }
        /// <summary>
        /// Returns a BoundingRect that contains all objects in this QuadTree and is between the exact extent and the linear double of this extent.
        /// </summary>
        public BoundingRect Size
        {
            get
            {
                return mRoot.Rect;
            }
        }
        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return new EnumerateNode(mRoot);
        }

        #endregion
    }

    public class QuadTree<T> where T : class, IQuadTreeInsertable
    {
        public enum IterateAction { branchDone, goDeeper, goDeeperAndSplit };
        public interface IIterateQuadTreeLists
        {
            IterateAction Iterate(ref BoundingRect rect, HashSet<T> objects, bool hasSubNodes);
        }

		class QuadNode<U> where U : class, IQuadTreeInsertable
		{
			public QuadNode<U> TopRight;
			public QuadNode<U> TopLeft;
			public QuadNode<U> BottomLeft;
			public QuadNode<U> BottomRight;
			// wird das gebraucht? public QuadNode Parent; // Parent, ist beim Root null
			public GeoPoint2D Center; // Mittelpunkt des Rechtecks
			public double halfWidth, halfHeight; // halbe Breite und Höhe des Rechtecks
			public BoundingRect rect; // das Rechteck selbst
			public QuadTree<U> root; // der QuadTree mit seinen Infos zu MaxListlen u.s.w.
			List<U> objectList; // existiert nur, wenn es ein Blatt ist, war vorher ein Set, Set ist aber viel aufwendiger als List
								// (außer bei Remove). Man darf dasselbe Objekt nicht zweimal in den QuadTree hängen, sonst ist es zweimal in der Liste
								// aber das scheint keine große Einschränkung zu sein, oder?
			int deepth; // die Tiefe des Baums
			bool IsRoot
			{
				get
				{
					return object.Equals(root.root, this);
				}
			}
			public void AddObject(U ObjectToAdd)
			{
				if (IsRoot && TopRight == null)
				{   // der Baum ist noch unreif, da er nur aus einer Liste besteht
					// das von der Wurzel überdeckte Rechteck kann also noch beliebig manipuliert
					// werden. Es soll 10% größer sein als alle Objekte in der Wurzel
					BoundingRect ext = ObjectToAdd.GetExtent();
					if (ext.IsEmpty()) return; // welche Fälle sind das, nichts wird eingefügt?
					if (objectList.Count == 0)
					{   // das erste Objekt wird eingefügt
						if (!root.initialRect.IsEmpty())
						{
							rect = root.initialRect;
							if (ext <= rect)
							{
								Center = rect.GetCenter();
								halfWidth = rect.Width / 2.0;
								halfHeight = rect.Height / 2.0;
							}
							else
							{   // vorgegebenes Rechteck ist schon zu klein
								Center = ext.GetCenter();
								halfWidth = ext.Width / 2.0 * 1.1;
								halfHeight = ext.Height / 2.0 * 1.1; // 10% größer
								rect = new BoundingRect(Center, halfWidth, halfHeight);
							}
						}
						else
						{
							Center = ext.GetCenter();
							halfWidth = ext.Width / 2.0 * 1.1;
							halfHeight = ext.Height / 2.0 * 1.1; // 10% größer
							rect = new BoundingRect(Center, halfWidth, halfHeight);
							// Breite und/oder Höhe kann immer noch 0.0 sein
						}
					}
					else
					{   // ein weiteres Objekt wird zugefügt
						// wenn ein Rechteck vorgegeben ist und es groß genug ist,
						// wird es hier nicht verändert
						BoundingRect total = rect;
						ext = ext * 1.1; // 10% größer, wenn es aber leer ist (Punkt), dann greift folgende Zeile:
										 // if (ext.Width == 0.0 && ext.Height == 0.0) ext.Inflate(Size / 1000);
						total.MinMax(ext);
						Center = total.GetCenter();
						halfWidth = total.Width / 2.0 * 1.1;
						halfHeight = total.Height / 2.0 * 1.1; // 10% größer
						rect = new BoundingRect(Center, halfWidth, halfHeight);
					}
				}
				// nach den Vorbereitungen für die Wurzel beginnt hier das normale Zufügen
				if (ObjectToAdd.HitTest(ref rect, true))
				{
					if (TopRight == null)
					{   // es ist ein Blatt und kein Knoten
						// Ein Blatt wird aufgeteilt, wenn es zu viele Objekte hat (MaxListlen), aber nicht, wenn
						// die breite oder Höhe noch nicht bestimmt sind, oder wenn der baum zu tief würde
						bool split = false;
						if (root.MaxDeepth <= 0)
						{   // dynamische Aufteilung, MaxDeepth gibt die Stärke an
							// MaxDeepth ist negativ, der shift Faktor wird so bestimmt
							int shift = Math.Max(0, deepth + root.MaxDeepth);
							if (objectList.Count > (1 << deepth)) split = true;
						}
						// if ((root.MaxDeepth < 0 && (objectList.Count > (1 << deepth)))) split = true;
						if ((root.MaxDeepth > 0 && deepth < root.MaxDeepth && objectList.Count >= root.MaxListLen && halfHeight > 0.0 && halfWidth > 0.0)) split = true;
						if (split)
						// if (deepth < root.MaxDeepth && objectList.Count >= root.MaxListLen && halfHeight > 0.0 && halfWidth > 0.0)
						{   // das Blatt wird zum Knoten
							// Untereinträge erzeugen
							double quarterWidth = halfWidth / 2.0;
							double quarterHeight = halfHeight / 2.0;
							TopRight = new QuadNode<U>(root, new GeoPoint2D(Center, quarterWidth, quarterHeight), quarterWidth, quarterHeight, deepth + 1);
							TopLeft = new QuadNode<U>(root, new GeoPoint2D(Center, -quarterWidth, quarterHeight), quarterWidth, quarterHeight, deepth + 1);
							BottomLeft = new QuadNode<U>(root, new GeoPoint2D(Center, -quarterWidth, -quarterHeight), quarterWidth, quarterHeight, deepth + 1);
							BottomRight = new QuadNode<U>(root, new GeoPoint2D(Center, quarterWidth, -quarterHeight), quarterWidth, quarterHeight, deepth + 1);
							// die vorhanden Objekte aufteilen (for ist marginal schneller als foreach)
							foreach (U obj in objectList)
							{
								TopRight.AddObject(obj);
								TopLeft.AddObject(obj);
								BottomLeft.AddObject(obj);
								BottomRight.AddObject(obj);
							}
							// das neue Objekt ebenfalls zufügen
							TopRight.AddObject(ObjectToAdd);
							TopLeft.AddObject(ObjectToAdd);
							BottomLeft.AddObject(ObjectToAdd);
							BottomRight.AddObject(ObjectToAdd);
							// die Liste wird nicht mehr gebraucht
							objectList = null;
						}
						else
						{   // es bleibt ein Blatt, einfach in die Liste damit
							objectList.Add(ObjectToAdd);
						}
					}
					else
					{
						TopRight.AddObject(ObjectToAdd);
						TopLeft.AddObject(ObjectToAdd);
						BottomLeft.AddObject(ObjectToAdd);
						BottomRight.AddObject(ObjectToAdd);
					}
				}
			}
			public void RemoveObject(U ObjectToRemove)
			{
				if (TopRight == null)
				{   // damit ist der Fall des Root Objektes, wenn noch kein Rechteck vorliegt, abgedeckt
					objectList.Remove(ObjectToRemove);
				}
				else if (ObjectToRemove.HitTest(ref rect, true))
				{
					TopRight.RemoveObject(ObjectToRemove);
					TopLeft.RemoveObject(ObjectToRemove);
					BottomLeft.RemoveObject(ObjectToRemove);
					BottomRight.RemoveObject(ObjectToRemove);
					if (TopRight.FirstObject == null && TopLeft.FirstObject == null && BottomRight.FirstObject == null && BottomLeft.FirstObject == null)
					{   // alle 4 Unterknoten sind leer
						TopRight = TopLeft = BottomRight = BottomLeft = null;
						objectList = new List<U>();
					}
				}
			}
			public IEnumerable<U> AllObjects()
			{
				if (objectList != null)
				{
					foreach (U obj in objectList)
					{
						yield return obj;
					}
				}
				else if (TopRight != null)
				{
					foreach (U obj in TopRight.AllObjects())
					{
						yield return obj;
					}
					foreach (U obj in TopLeft.AllObjects())
					{
						yield return obj;
					}
					foreach (U obj in BottomLeft.AllObjects())
					{
						yield return obj;
					}
					foreach (U obj in BottomRight.AllObjects())
					{
						yield return obj;
					}
				}
			}
			public IEnumerable<U> ObjectsFromRect(BoundingRect r)
			{
				if (!BoundingRect.Disjoint(r, rect))
				{
					if (objectList != null)
					{
						foreach (U obj in objectList)
						{
							yield return obj;
						}
					}
					else if (TopRight != null)
					{
						foreach (U obj in TopRight.ObjectsFromRect(r))
						{
							yield return obj;
						}
						foreach (U obj in TopLeft.ObjectsFromRect(r))
						{
							yield return obj;
						}
						foreach (U obj in BottomLeft.ObjectsFromRect(r))
						{
							yield return obj;
						}
						foreach (U obj in BottomRight.ObjectsFromRect(r))
						{
							yield return obj;
						}
					}
				}
			}
			public IEnumerable<U> ObjectsCloseTo(IQuadTreeInsertable CloseToThis)
			{   // liefert alle also auch mehrfach
				if (CloseToThis.HitTest(ref rect, true))
				{
					if (objectList != null)
					{
						foreach (U obj in objectList)
						{
							yield return obj;
						}
					}
					else if (TopRight != null)
					{
						foreach (U obj in TopRight.ObjectsCloseTo(CloseToThis))
						{
							yield return obj;
						}
						foreach (U obj in TopLeft.ObjectsCloseTo(CloseToThis))
						{
							yield return obj;
						}
						foreach (U obj in BottomLeft.ObjectsCloseTo(CloseToThis))
						{
							yield return obj;
						}
						foreach (U obj in BottomRight.ObjectsCloseTo(CloseToThis))
						{
							yield return obj;
						}
					}
				}
			}
			public IEnumerable<List<U>> AllLists
			{
				get
				{
					if (objectList != null) yield return objectList;
					else if (TopRight != null)
					{
						foreach (List<U> list in TopRight.AllLists)
						{
							yield return list;
						}
						foreach (List<U> list in TopLeft.AllLists)
						{
							yield return list;
						}
						foreach (List<U> list in BottomRight.AllLists)
						{
							yield return list;
						}
						foreach (List<U> list in BottomLeft.AllLists)
						{
							yield return list;
						}
					}
				}
			}
			public U FirstObject
			{
				get
				{
					if (objectList != null)
					{
						if (objectList.Count > 0) return objectList[0];
						//foreach (U res in objectList)
						//{
						//    return res; // läuft halt nur einmal und liefert das erste
						//}
					}
					else if (TopRight != null)
					{
						U res = TopRight.FirstObject;
						if (res != null) return res;
						res = TopLeft.FirstObject;
						if (res != null) return res;
						res = BottomLeft.FirstObject;
						if (res != null) return res;
						res = BottomRight.FirstObject;
						if (res != null) return res;
					}
					return null;
				}
			}
			internal void SetNodes(QuadNode<U> qn0, QuadNode<U> qn1, QuadNode<U> qn2, QuadNode<U> qn3)
			{
				TopRight = qn0;
				TopLeft = qn1;
				BottomLeft = qn2;
				BottomRight = qn3;
				TopRight.root = root;
				TopLeft.root = root;
				BottomLeft.root = root;
				BottomRight.root = root;
				objectList = null;
			}

			public QuadNode(QuadTree<U> root, GeoPoint2D center, double halfWidth, double halfHeight, int deepth)
			{
				this.root = root;
				this.objectList = new List<U>();
				this.deepth = deepth;
				this.Center = center;
				this.halfWidth = halfWidth;
				this.halfHeight = halfHeight;
				this.rect = new BoundingRect(Center, halfWidth, halfHeight);
			}
			public QuadNode(QuadTree<U> root)
			{
				this.halfWidth = 0.0;
				this.halfHeight = 0.0;
				this.root = root;
				this.rect = BoundingRect.EmptyBoundingRect;
				this.deepth = 0;
				this.objectList = new List<U>();
			}
#if DEBUG
			internal void Debug(DebuggerContainer dc)
			{
				dc.Add(new Line2D(Center + new GeoVector2D(-halfWidth, -halfHeight), Center + new GeoVector2D(-halfWidth, halfHeight)));
				dc.Add(new Line2D(Center + new GeoVector2D(-halfWidth, -halfHeight), Center + new GeoVector2D(halfWidth, -halfHeight)));
				dc.Add(new Line2D(Center + new GeoVector2D(halfWidth, halfHeight), Center + new GeoVector2D(-halfWidth, halfHeight)));
				dc.Add(new Line2D(Center + new GeoVector2D(halfWidth, halfHeight), Center + new GeoVector2D(halfWidth, -halfHeight)));
				if (TopRight != null)
				{
					TopRight.Debug(dc);
					TopLeft.Debug(dc);
					BottomRight.Debug(dc);
					BottomLeft.Debug(dc);
				}
			}
#endif

			internal bool Check(IQuadTreeInsertable closeTo, QuadTree<U>.CheckOne check)
			{
				if (closeTo.HitTest(ref rect, true))
				{
					if (objectList != null)
					{
						foreach (U obj in objectList)
						{
							if (check(obj)) return true;
						}
					}
					else if (TopRight != null)
					{
						if (TopRight.Check(closeTo, check)) return true;
						if (TopLeft.Check(closeTo, check)) return true;
						if (BottomRight.Check(closeTo, check)) return true;
						if (BottomLeft.Check(closeTo, check)) return true;
					}
				}
				return false;
			}

			internal void AddList(List<Pair<BoundingRect, U[]>> res)
			{
				if (objectList != null)
				{
					if (objectList.Count > 0)
						res.Add(new Pair<BoundingRect, U[]>(this.rect, objectList.ToArray()));
				}
				else if (TopRight != null)
				{
					TopRight.AddList(res);
					TopLeft.AddList(res);
					BottomRight.AddList(res);
					BottomLeft.AddList(res);
				}
			}

			internal void Iterate(int stepDownLevels, QuadTree<U>.IIterateQuadTreeLists it)
			{
				QuadTree<U>.IterateAction action = QuadTree<U>.IterateAction.branchDone;
				if (stepDownLevels <= 0)
				{
					action = it.Iterate(ref this.rect, new HashSet<U>(AllObjects()), TopRight != null);
				}
				else if (objectList != null)
				{
					action = it.Iterate(ref this.rect, new HashSet<U>(objectList), false);
				}
				else action = QuadTree<U>.IterateAction.goDeeper;
				if (action == QuadTree<U>.IterateAction.goDeeperAndSplit && objectList != null)
				{      // Aufrufer möchte den Knoten aufteilen
					   // das Blatt wird zum Knoten
					   // Untereinträge erzeugen
					double quarterWidth = halfWidth / 2.0;
					double quarterHeight = halfHeight / 2.0;
					TopRight = new QuadNode<U>(root, new GeoPoint2D(Center, quarterWidth, quarterHeight), quarterWidth, quarterHeight, deepth + 1);
					TopLeft = new QuadNode<U>(root, new GeoPoint2D(Center, -quarterWidth, quarterHeight), quarterWidth, quarterHeight, deepth + 1);
					BottomLeft = new QuadNode<U>(root, new GeoPoint2D(Center, -quarterWidth, -quarterHeight), quarterWidth, quarterHeight, deepth + 1);
					BottomRight = new QuadNode<U>(root, new GeoPoint2D(Center, quarterWidth, -quarterHeight), quarterWidth, quarterHeight, deepth + 1);
					// die vorhanden Objekte aufteilen (for ist marginal schneller als foreach)
					foreach (U obj in objectList)
					{
						TopRight.AddObject(obj);
						TopLeft.AddObject(obj);
						BottomLeft.AddObject(obj);
						BottomRight.AddObject(obj);
					}
					// die Liste wird nicht mehr gebraucht
					objectList = null;
				}
				if (action != QuadTree<U>.IterateAction.branchDone && TopRight != null)
				{
					TopRight.Iterate(stepDownLevels - 1, it);
					TopLeft.Iterate(stepDownLevels - 1, it);
					BottomRight.Iterate(stepDownLevels - 1, it);
					BottomLeft.Iterate(stepDownLevels - 1, it);
				}
			}
		}

		QuadNode<T> root; // hier geht der Quadtree los
        /// <summary>
        /// The maximum number of objects in a leaf-node unless MaxDeepth has been reached
        /// </summary>
        public int MaxListLen;
        /// <summary>
        /// The maximum deepth of the tree. If a node has this deepth no more subnodes will be generated
        /// but the list of objects in that node might increase MaxListlen.
        /// </summary>
        public int MaxDeepth;
        private BoundingRect initialRect;
        public QuadTree()
        {
            MaxListLen = 20;
            MaxDeepth = 9;
            root = new QuadNode<T>(this);
            root.root = this;
            initialRect = BoundingRect.EmptyBoundingRect;
        }
        public QuadTree(BoundingRect initialRect)
        {
            MaxListLen = 20;
            MaxDeepth = 9;
            root = new QuadNode<T>(this);
            root.root = this;
            this.initialRect = initialRect;
        }
        /// <summary>
        /// Adds the specified object to the QuadTree
        /// </summary>
        /// <param name="ObjectToAdd">Object to add</param>
        public void AddObject(T ObjectToAdd)
        {
            if (root.TopRight != null)
            {
                // abprüfen, ob der Root nach oben erweitert werden muss.
                // wenn ja, dann 4 neue SubNodes erzeugen und die bestehenden
                // in einen (den richtigen) Subnode einfügen
                // ansonsten einfach AddObject von den vieren aufrufen
                BoundingRect ObjectExtent = ObjectToAdd.GetExtent();
                if (ObjectExtent.IsEmpty()) return;
                BoundingRect NodeExtent = new BoundingRect(root.Center, root.halfWidth, root.halfHeight);
                if (ObjectExtent <= NodeExtent)
                {	// das Objekt passt ganz hier rein, also einfach einfügen
                    root.AddObject(ObjectToAdd);
                }
                else
                {
                    if (ObjectExtent.Left < NodeExtent.Left)
                    {	// Überschreitung nach links
                        if (ObjectExtent.Bottom < NodeExtent.Bottom)
                        {	// Überschreitung nach unten
                            GeoPoint2D Center = root.Center;
                            double width = root.halfWidth * 2.0;
                            double height = root.halfHeight * 2.0;
                            Center.x -= root.halfWidth;
                            Center.y -= root.halfHeight;
                            QuadNode<T> OldRoot = root;
                            root = new QuadNode<T>(this, Center, width, height, 0);
                            root.SetNodes(
                                OldRoot,
                                new QuadNode<T>(this, new GeoPoint2D(Center, -root.halfWidth, root.halfHeight), root.halfWidth, root.halfHeight, 1),
                                new QuadNode<T>(this, new GeoPoint2D(Center, -root.halfWidth, -root.halfHeight), root.halfWidth, root.halfHeight, 1),
                                new QuadNode<T>(this, new GeoPoint2D(Center, root.halfWidth, -root.halfHeight), root.halfWidth, root.halfHeight, 1));
                        }
                        else
                        {	// überschreitung nach oben (oder keine vertikale Überschreitung)
                            GeoPoint2D Center = root.Center;
                            double width = root.halfWidth * 2.0;
                            double height = root.halfHeight * 2.0;
                            Center.x -= root.halfWidth;
                            Center.y += root.halfHeight;
                            QuadNode<T> OldRoot = root;
                            root = new QuadNode<T>(this, Center, width, height, 0);
                            root.SetNodes(
                                new QuadNode<T>(this, new GeoPoint2D(Center, root.halfWidth, root.halfHeight), root.halfWidth, root.halfHeight, 1),
                                new QuadNode<T>(this, new GeoPoint2D(Center, -root.halfWidth, root.halfHeight), root.halfWidth, root.halfHeight, 1),
                                new QuadNode<T>(this, new GeoPoint2D(Center, -root.halfWidth, -root.halfHeight), root.halfWidth, root.halfHeight, 1),
                                OldRoot);
                        }
                    }
                    else
                    {	// Überschreitung nach rechts (oder keine horizontale Überschreitung)
                        if (ObjectExtent.Bottom < NodeExtent.Bottom)
                        {	// Überschreitung nach unten
                            GeoPoint2D Center = root.Center;
                            double width = root.halfWidth * 2.0;
                            double height = root.halfHeight * 2.0;
                            Center.x += root.halfWidth;
                            Center.y -= root.halfHeight;
                            QuadNode<T> OldRoot = root;
                            root = new QuadNode<T>(this, Center, width, height, 0);
                            root.SetNodes(
                                new QuadNode<T>(this, new GeoPoint2D(Center, root.halfWidth, root.halfHeight), root.halfWidth, root.halfHeight, 1),
                                OldRoot,
                                new QuadNode<T>(this, new GeoPoint2D(Center, -root.halfWidth, -root.halfHeight), root.halfWidth, root.halfHeight, 1),
                                new QuadNode<T>(this, new GeoPoint2D(Center, root.halfWidth, -root.halfHeight), root.halfWidth, root.halfHeight, 1)
                                );
                        }
                        else
                        {	// überschreitung nach oben (oder keine vertikale Überschreitung)
                            GeoPoint2D Center = root.Center;
                            double width = root.halfWidth * 2.0;
                            double height = root.halfHeight * 2.0;
                            Center.x += root.halfWidth;
                            Center.y += root.halfHeight;
                            QuadNode<T> OldRoot = root;
                            root = new QuadNode<T>(this, Center, width, height, 0);
                            root.SetNodes(
                                new QuadNode<T>(this, new GeoPoint2D(Center, root.halfWidth, root.halfHeight), root.halfWidth, root.halfHeight, 1),
                                new QuadNode<T>(this, new GeoPoint2D(Center, -root.halfWidth, root.halfHeight), root.halfWidth, root.halfHeight, 1),
                                OldRoot,
                                new QuadNode<T>(this, new GeoPoint2D(Center, root.halfWidth, -root.halfHeight), root.halfWidth, root.halfHeight, 1)
                                );
                        }
                    }
                    // der Root ist jetzt noch oben gewachsen, also hier einen erneuten Versuch, 
                    // das Objekt einzufügen, kann beliebig rekursiv werden
                    AddObject(ObjectToAdd);
                }
            }
            else
            {
                // keine Überschreitung, alles weitere kann das QuadNode Objekt selbst regeln
                root.AddObject(ObjectToAdd);
            }
        }
        public void AddObjects(List<T> ObjectsToAdd)
        {
            for (int i = 0; i < ObjectsToAdd.Count; ++i)
            {
                AddObject(ObjectsToAdd[i]);
            }
        }

        /// <summary>
        /// Removes the specified object from the QuadTree
        /// </summary>
        /// <param name="ObjectToRemove">Object to remove</param>
        public void RemoveObject(T ObjectToRemove)
        {
            root.RemoveObject(ObjectToRemove);
        }
        public void RemoveObjects(List<T> ObjectsToRemove)
        {
            for (int i = 0; i < ObjectsToRemove.Count; ++i)
            {
                root.RemoveObject(ObjectsToRemove[i]);
            }
        }
        /// <summary>
        /// Returns an enumerator for all objects that are close to the specified bounding rectangle.
        /// </summary>
        /// <param name="r">The rectangle to filter objects</param>
        /// <returns>Enumerator for those objects</returns>
        public IEnumerable<T> AllObjects()
        {
            Set<T> elimiinateDouble = new Set<T>();
            foreach (T obj in root.AllObjects())
            {
                if (!elimiinateDouble.Contains(obj))
                {
                    elimiinateDouble.Add(obj);
                    yield return obj;
                }
            }
        }
        /// <summary>
        /// Returns an enumerator for all objects that are close to the specified bounding rectangle.
        /// </summary>
        /// <param name="r">The rectangle to filter objects</param>
        /// <returns>Enumerator for those objects</returns>
        public IEnumerable<T> ObjectsFromRect(BoundingRect r)
        {
            Set<T> elimiinateDouble = new Set<T>();
            foreach (T obj in root.ObjectsFromRect(r))
            {
                if (!elimiinateDouble.Contains(obj))
                {
                    elimiinateDouble.Add(obj);
                    yield return obj;
                }
            }
        }
        /// <summary>
        /// Returns an enumerator for all objects that are completely inside the specified bounding rectangle.
        /// </summary>
        /// <param name="r">The rectangle to filter objects</param>
        /// <returns>Enumerator for those objects</returns>
        public IEnumerable<T> ObjectsInsideRect(BoundingRect r)
        {
            Set<T> elimiinateDouble = new Set<T>();
            foreach (T obj in root.ObjectsFromRect(r))
            {
                if (!elimiinateDouble.Contains(obj))
                {
                    elimiinateDouble.Add(obj);
                    if (obj.GetExtent() < r) yield return obj;
                }
            }
        }
        /// <summary>
        /// Returns an enumerator for all objects that are closed to the specified object. The distance depends
        /// upon the granularity of the QuadTree.
        /// </summary>
        /// <param name="CloseToThis">Filter objects close to this object</param>
        /// <returns>Enumertor for those objects</returns>
        public IEnumerable<T> ObjectsCloseTo(T CloseToThis)
        {
            Set<T> elimiinateDouble = new Set<T>();
            foreach (T obj in root.ObjectsCloseTo(CloseToThis))
            {
                if (!elimiinateDouble.Contains(obj))
                {
                    elimiinateDouble.Add(obj);
                    yield return obj;
                }
            }
        }
        /// <summary>
        /// Returns an enumerator for all objects that are closed to the specified object. The distance depends
        /// upon the granularity of the QuadTree. if <paramref name="eliminateMultiple"/> is false, the same object
        /// may be yielded in the enumerator multiple times, but the execution speed is much faster, if eliminateMultiple
        /// is true, there will be bookkeeping not to yield the same object multiple times.
        /// </summary>
        /// <param name="CloseToThis">Filter objects close to this object</param>
        /// <param name="eliminateMultiple">false: fast mode, multiple yields, true: single yield per object</param>
        /// <returns>Enumertor for those objects</returns>
        public IEnumerable<T> ObjectsCloseTo(IQuadTreeInsertable CloseToThis, bool eliminateMultiple)
        {
            Set<T> elimiinateDouble = new Set<T>();
            foreach (T obj in root.ObjectsCloseTo(CloseToThis))
            {
                if (!eliminateMultiple || !elimiinateDouble.Contains(obj))
                {
                    if (eliminateMultiple) elimiinateDouble.Add(obj);
                    yield return obj;
                }
            }
        }
        public IEnumerable<List<T>> AllLists
        {
            get
            {
                foreach (List<T> list in root.AllLists) yield return list;
            }
        }
        /// <summary>
        /// Returns an array of objects that are close to the specified bounding rectangle. The objects may overlap
        /// the rectangle or even be totaly outside of the rectangle depending on the granularity of the Quadtree.
        /// But all objects inside the rectangle or interfering with the rectangle are guaranteed to be returned.
        /// </summary>
        /// <param name="r">The filter rectangle</param>
        /// <returns>Array of all appropriate objects</returns>
        public T[] GetObjectsFromRect(BoundingRect r)
        {
            Set<T> all = new Set<T>();
            foreach (T obj in root.ObjectsFromRect(r))
            {
                all.Add(obj);
            }
            return all.ToArray();
        }
        /// <summary>
        /// Returns an array of objects that are inside the specified bounding rectangle. 
        /// </summary>
        /// <param name="r">The filter rectangle</param>
        /// <returns>Array of all appropriate objects</returns>
        public T[] GetObjectsInsideRect(BoundingRect r)
        {
            Set<T> all = new Set<T>();
            foreach (T obj in root.ObjectsFromRect(r))
            {
                if (obj.GetExtent() < r) all.Add(obj);
            }
            return all.ToArray();
        }
        /// <summary>
        /// Returns an array of objects that are close to the specified object. The returned objects may or may not
        /// interfere with the specified object, but it is guaranteed that all objects touching or intersecting
        /// the specified object are in the returned array.
        /// </summary>
        /// <param name="CloseToThis">Object to filter result with</param>
        /// <returns>Array of all appropriate objects</returns>
        public T[] GetObjectsCloseTo(T CloseToThis)
        {
            Set<T> all = new Set<T>();
            foreach (T obj in root.ObjectsCloseTo(CloseToThis))
            {
                all.Add(obj);
            }
            return all.ToArray();
        }
        public T[] GetObjectsCloseTo(IQuadTreeInsertable CloseToThis)
        {
            Set<T> all = new Set<T>();
            foreach (T obj in root.ObjectsCloseTo(CloseToThis))
            {
                all.Add(obj);
            }
            return all.ToArray();
        }
        public T[] GetAllObjects()
        {
            List<T> res = new List<T>();
            foreach (T item in AllObjects())
            {
                res.Add(item);
            }
            return res.ToArray();
        }
        public Pair<BoundingRect, T[]>[] GetAllLists()
        {
            List<Pair<BoundingRect, T[]>> res = new List<Pair<BoundingRect, T[]>>();
            root.AddList(res);
            return res.ToArray();
        }
        public T SomeObject
        {
            get
            {
                if (root != null) return root.FirstObject;
                else return null;
            }
        }
        public bool Check(IQuadTreeInsertable closeTo, CheckOne check)
        {
            return root.Check(closeTo, check);
        }
        public delegate bool CheckOne(T toCheck);
        public BoundingRect Size
        {
            get
            {
                return root.rect;
            }
        }
#if DEBUG
        internal DebuggerContainer Debug
        {
            get
            {
                DebuggerContainer dc = new DebuggerContainer();
                root.Debug(dc);
                return dc;
            }
        }
        internal DebuggerContainer Objects
        {
            get
            {
                DebuggerContainer dc = new DebuggerContainer();
                IEnumerable<T> all = root.ObjectsFromRect(root.rect);
                foreach (object obj in all)
                {
                    ICurve2D c2d = obj as ICurve2D;
                    if (c2d != null)
                    {
                        dc.Add(c2d);
                    }
                }
                return dc;
            }
        }
        public int Count
        {
            get
            {
                return GetAllObjects().Length;
            }
        }
#endif

        internal void Iterate(int stepDownLevels, IIterateQuadTreeLists it)
        {
            root.Iterate(stepDownLevels, it);
        }
    }
}
