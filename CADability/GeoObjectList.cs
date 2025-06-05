using CADability.Attribute;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Wintellect.PowerCollections;

namespace CADability.GeoObject
{
    /// <summary>
    /// Simple list of GeoObjects (IGeoObject). Implemented as an ArrayList.
    /// </summary>
    [Serializable]
    public class GeoObjectList : IEnumerable, ISerializable, IDeserializationCallback, IEnumerable<IGeoObject>, IJsonSerialize
    {
        private ArrayList alist; // used for ISerializabe and IDeserializationCallback since list has been changed to List<IGeoObject>, which cannot be serialized/deserialized
        protected List<IGeoObject> list;
        private UserData userData;
        public delegate void ObjectAddedDelegate(GeoObjectList sender, IGeoObject addedObject, bool lastRangeElement);
        public event ObjectAddedDelegate ObjectAddedEvent;
        public delegate void ObjectRemovedDelegate(GeoObjectList sender, IGeoObject removedObject, bool lastRangeElement);
        public event ObjectRemovedDelegate ObjectRemovedEvent;

        public GeoObjectList()
        {
            list = new List<IGeoObject>();
            userData = new UserData();
        }
        public GeoObjectList(int Capacity)
        {
            list = new List<IGeoObject>(Capacity);
            userData = new UserData();
        }
        public GeoObjectList(IGeoObject singleObject)
            : this(1)
        {
            list.Add(singleObject);
        }
        public GeoObjectList(params IGeoObject[] l)
            : this(l.Length)
        {
            list.AddRange(l);
        }
        public GeoObjectList(List<IGeoObject> list)
            : this(list.Count)
        {
            AddRange(list.ToArray());
        }
        public GeoObjectList(ICollection<IGeoObject> list)
            : this(list.Count)
        {
            this.list.AddRange(list);
        }
        public GeoObjectList(IEnumerable<IGeoObject> list)
            : this(list.ToArray().Length)
        {
            this.list.AddRange(list);
        }
        public GeoObjectList(IEnumerable<ICurve> list)
            : this(list.ToArray().Length)
        {
            foreach (ICurve crv in list)
            {
                this.list.AddIfNotNull(crv as IGeoObject);
            }
        }
        public void Add(IGeoObject ObjectToAdd)
        {
            list.Add(ObjectToAdd);
            ObjectAddedEvent?.Invoke(this, ObjectToAdd, true);
        }
        public void Insert(int index, IGeoObject ObjectToAdd)
        {
            list.Insert(index, ObjectToAdd);
            ObjectAddedEvent?.Invoke(this, ObjectToAdd, true);
        }
        public void AddUnique(IGeoObject ObjectToAdd)
        {	// da list eine ArrayList ist, ist AddUnique u.U. aufwendig
            // Überlegung: sollte man GeoObjectList per Eigenschaft auch parallel als
            // HashTable implementieren? Das würde hier Vorteile bringen.
            if (!list.Contains(ObjectToAdd))
                Add(ObjectToAdd);
        }
        public void AddDecomposed(IGeoObject ObjectToAdd)
        {
            if (ObjectToAdd is Hatch)
            {
                Hatch hatch = (ObjectToAdd as Hatch);
                hatch.ConditionalRecalc();
            }
            if (ObjectToAdd is Block)
            {
                Block blk = ObjectToAdd as Block;
                for (int j = 0; j < blk.Count; ++j)
                {
                    AddDecomposed(blk.Child(j));
                }
            }
            else
            {
                Add(ObjectToAdd);
            }
        }
        private void AddDecomposed(IGeoObject ObjectToAdd, bool decomposeHatch, bool decomposePath, bool decomposePolyLine)
        {
            if (ObjectToAdd is Hatch)
            {
                Hatch hatch = (ObjectToAdd as Hatch);
                hatch.ConditionalRecalc();
            }
            if (ObjectToAdd is Block && !(!decomposeHatch && ObjectToAdd is Hatch))
            {
                Block blk = ObjectToAdd as Block;
                for (int j = 0; j < blk.Count; ++j)
                {
                    blk.Child(j).PropagateAttributes(blk.Layer, blk.ColorDef);
                    AddDecomposed(blk.Child(j), decomposeHatch, decomposePath, decomposePolyLine);
                }
            }
            else if (ObjectToAdd is Path)
            {
                Path path = (ObjectToAdd as Path);
                for (int i = 0; i < path.CurveCount; i++)
                {
                    Add(path.Curve(i) as IGeoObject);
                }
            }
            else if (ObjectToAdd is Polyline && decomposePolyLine)
            {
                Polyline polyline = (ObjectToAdd as Polyline);
                for (int i = 0; i < polyline.Vertices.Length - 1; i++)
                {
                    Line line = Line.Construct();
                    line.SetTwoPoints(polyline.Vertices[i], polyline.Vertices[i + 1]);
                    line.CopyAttributes(polyline);
                    Add(line);
                }
                if (polyline.IsClosed)
                {
                    Line line = Line.Construct();
                    line.SetTwoPoints(polyline.Vertices[polyline.Vertices.Length - 1], polyline.Vertices[0]);
                    line.CopyAttributes(polyline);
                    Add(line);
                }
            }
            else
            {
                Add(ObjectToAdd);
            }
        }

        public void AddRange(IEnumerable<IGeoObject> ObjectsToAdd)
        {
            list.AddRange(ObjectsToAdd);
            if (ObjectAddedEvent != null)
            {
                List<IGeoObject> objectsAdded = new List<IGeoObject>(ObjectsToAdd);
                for (int i = 0; i < objectsAdded.Count; i++) ObjectAddedEvent(this, objectsAdded[i], i == objectsAdded.Count - 1);
            }
        }

        public void AddRangeUnique(GeoObjectList ObjectsToAdd)
        {   // erst die überflüssigen entfernen, dann alle verbleibenden zufügen wg. ObjectAddedEvent
            for (int i = ObjectsToAdd.Count - 1; i >= 0; --i)
            {
                int index = list.IndexOf(ObjectsToAdd[i]);
                if (index >= 0) ObjectsToAdd.Remove(i);
            }
            AddRange(ObjectsToAdd);
        }
        public void Remove(int index)
        {
            // man braucht nicht auf die Gültigkeit des Index überprüfen, denn der Zugriff
            // List[index] tut es ja auch und wirft genau die ArgumentOutOfRangeException, die
            // man selbst hier auch werfen würde, also wozu dann überprüfen
            IGeoObject go = list[index] as IGeoObject;
            if (go != null)
            {
                list.RemoveAt(index);
                if (ObjectRemovedEvent != null) ObjectRemovedEvent(this, go, true);
            }
        }
        /// <summary>
        /// Removes all GeoObjects that are owned by Parent
        /// </summary>
        /// <param name="Parent">The owner</param>
        public void RemoveChildrenOf(IGeoObject Parent)
        {
            for (int i = list.Count - 1; i >= 0; --i)
            {
                IGeoObject go = list[i] as IGeoObject;
                if (go.Owner == Parent) list.RemoveAt(i);
            }
            if (ObjectRemovedEvent != null)
            {
                int j;
                for (j = 0; j < Parent.NumChildren - 1; j++)
                    ObjectRemovedEvent(this, Parent.Child(j), false);
                ObjectRemovedEvent(this, Parent.Child(j), true);
            }
        }
        public void Remove(IGeoObject ObjectToRemove)
        {
            int index = list.IndexOf(ObjectToRemove);
            if (index >= 0) Remove(index);
        }
        public void DecomposeBlocks()
        {
            for (int i = list.Count - 1; i >= 0; --i)
            {
                if (this[i] is Hatch hatch)
                {
                    hatch.ConditionalRecalc();
                }
                else if (this[i] is Block blk)
                {
                    Remove(i);
                    for (int j = 0; j < blk.Count; ++j)
                    {
                        AddDecomposed(blk.Child(j));
                    }
                }
            }
        }
        public void DecomposeBlocks(bool decomposeHatch)
        {
            for (int i = list.Count - 1; i >= 0; --i)
            {
                if (this[i] is Block && !(!decomposeHatch && this[i] is Hatch))
                {
                    if (this[i] is Hatch)
                    {
                        Hatch hatch = (this[i] as Hatch);
                        hatch.ConditionalRecalc();
                    }
                    Block blk = this[i] as Block;
                    Remove(i);
                    for (int j = 0; j < blk.Count; ++j)
                    {
                        AddDecomposed(blk.Child(j), decomposeHatch, false, false);
                    }
                }
                if (this[i] is BlockRef)
                {
                    BlockRef blk = this[i] as BlockRef;
                    Remove(i);
                    AddDecomposed(blk.Flattened, decomposeHatch, false, false);
                }
            }
        }
        public void DecomposeBlockRefs()
        {
            bool decomposed;
            do
            {
                decomposed = false;
                for (int i = list.Count - 1; i >= 0; --i)
                {
                    if (this[i] is BlockRef)
                    {
                        GeoObjectList l = (this[i] as BlockRef).Decompose();
                        Remove(i);
                        AddRange(l);
                        decomposed = true;
                    }
                }
            } while (decomposed);
        }
        public void DecomposeAll()
        {
            for (int i = list.Count - 1; i >= 0; --i)
            {
                if (this[i] is Block)
                {
                    Block blk = this[i] as Block;
                    Remove(i);
                    for (int j = 0; j < blk.Count; ++j)
                    {
                        blk.Child(j).PropagateAttributes(blk.Layer, blk.ColorDef);
                        AddDecomposed(blk.Child(j), true, true, true);
                    }
                }
                if (this[i] is BlockRef)
                {
                    BlockRef blk = this[i] as BlockRef;
                    Remove(i);
                    AddDecomposed(blk.Flattened, true, true, true);
                }
                if (this[i] is Path || this[i] is Polyline)
                {
                    IGeoObject go = this[i];
                    Remove(i);
                    AddDecomposed(go, true, true, true);
                }
            }
        }
        /// <summary>
        /// Remove all objects that are not accepted by the given filter.
        /// </summary>
        /// <param name="filterList">List of filters to check with</param>
        public void Reduce(FilterList filterList)
        {
            for (int i = list.Count - 1; i >= 0; --i)
            {
                if (!filterList.Accept(this[i]))
                {
                    Remove(i);
                }
            }
        }
        public void Reverse()
        {
            list.Reverse();
        }
        public void Clear()
        {
            list.Clear();
        }
        public bool Contains(IGeoObject ToTest)
        {
            int index = list.IndexOf(ToTest);
            return (index >= 0);
        }
        /// <summary>
        /// Tests, weather this list and the list l have the same content
        /// (maybe in a different order)
        /// </summary>
        /// <param name="l">The GeoObjectList to compare with this list</param>
        /// <returns>true if the lists have the same content</returns>
        public bool HasSameContent(GeoObjectList l)
        {
            if (l.Count != list.Count) return false;
            for (int i = 0; i < l.Count; ++i)
            {
                int index = list.IndexOf(l[i]);
                if (index < 0) return false;
            }
            return true;
        }
        public int IndexOf(IGeoObject ToTest)
        {
            return list.IndexOf(ToTest);
        }
        /// <summary>
        /// Returns a deep clone of the list.
        /// </summary>
        /// <returns></returns>
        public GeoObjectList CloneObjects()
        {
            GeoObjectList res = new GeoObjectList(this);
            for (int i = 0; i < res.Count; ++i)
            {
                IGeoObject go = res[i];
                res[i] = go.Clone();
                res[i].CopyAttributes(go);
            }
            return res;
        }
        /// <summary>
        /// Returns a new independent GeoObjectList that contains the same objects as
        /// this list (shallow copy)
        /// </summary>
        /// <returns>the new list</returns>
        public GeoObjectList Clone()
        {
            return new GeoObjectList(this);
        }
        public BoundingRect GetExtent(Projection projection, bool Use2DWorld, bool RegardLineWidth)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            foreach (IGeoObject go in list)
            {
                res.MinMax(go.GetExtent(projection, ExtentPrecision.Raw));
            }
            return res;
        }
        public BoundingCube GetExtent()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            foreach (IGeoObject go in list)
            {
                res.MinMax(go.GetBoundingCube());
            }
            return res;
        }
        public void Modify(ModOp m)
        {
            foreach (IGeoObject go in list)
            {
                go.Modify(m);
            }
        }
        /// <summary>
        /// Returns the number of GeoObjects in this list.
        /// </summary>
        public int Count
        {
            get
            {
                return list.Count;
            }
        }
        public UserData UserData { get { return userData; } }
        public IGeoObject this[int Index]
        {
            get { return list[Index]; }
            set { list[Index] = value; }
        }
        public static implicit operator IGeoObject[](GeoObjectList l)
        {
            return (IGeoObject[])l.list.ToArray();
        }
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected GeoObjectList(SerializationInfo info, StreamingContext context)
        {
            alist = (ArrayList)info.GetValue("List", typeof(ArrayList));
            list = new List<IGeoObject>(alist.Count);
            for (int i = 0; i < alist.Count; i++) list.Add(alist[i] as IGeoObject);
            userData = InfoReader.ReadOrCreate(info, "UserData", typeof(UserData), new object[] { }) as UserData;
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ArrayList alist = new ArrayList(list);
            info.AddValue("List", alist, typeof(ArrayList));
            info.AddValue("UserData", userData);
        }
        void IDeserializationCallback.OnDeserialization(object sender)
        {
            if (alist != null)
            {
                list = new List<IGeoObject>(alist.Count);
                for (int i = 0; i < alist.Count; i++) list.Add(alist[i] as IGeoObject);
                alist = null;
            }
        }

        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("List", list);
            data.AddProperty("UserData", userData);
        }

        public void SetObjectData(IJsonReadData data)
        {
            list = data.GetProperty<List<IGeoObject>>("List");
            userData = data.GetProperty<UserData>("UserData");
        }

        #endregion
        /// <summary>
        /// A GeoObjectList is reduced to the owners of the contained objects. I.e. when an object has an owner
        /// of type IGeoObject (like a child of a <see cref="Block"/>), it is removed from the list and the owner is
        /// added instead. As a result, the list contains only objects that are owned by a <see cref="Model"/> or
        /// don't have an owner at all.
        /// </summary>
        public void ReduceToOwner()
        {
            Set<IGeoObject> set = new Set<IGeoObject>();
            for (int i = 0; i < this.list.Count; ++i)
            {
                IGeoObject owner = list[i] as IGeoObject;
                while (owner.Owner is IGeoObject) owner = owner.Owner as IGeoObject;
                if (owner.Owner != null) set.Add(owner);
            }
            list.Clear();
            list.AddRange(set.ToArray());
        }

        internal bool CheckConsistency()
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is Solid sld) if (!sld.Shells[0].CheckConsistency()) return false;
                    else if (list[i] is Shell shl) if (!shl.CheckConsistency()) return false;
                        else if (list[i] is Face fc) if (!fc.CheckConsistency()) return false;
                            else if (list[i] is Block blk) if (!blk.Children.CheckConsistency()) return false;
            }
            return true;
        }
        internal void MoveToFront(IGeoObject iGeoObject)
        {
            list.Remove(iGeoObject);
            list.Add(iGeoObject);
        }
        internal void MoveToBack(IGeoObject iGeoObject)
        {
            list.Remove(iGeoObject);
            list.Insert(0, iGeoObject);
        }

        IEnumerator<IGeoObject> IEnumerable<IGeoObject>.GetEnumerator()
        {
            List<IGeoObject> ll = new List<IGeoObject>();
            for (int i = 0; i < list.Count; ++i) ll.Add(list[i] as IGeoObject);
            return ll.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

    }
}
