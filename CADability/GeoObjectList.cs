using CADability.Attribute;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// Simple list of GeoObjects (IGeoObject). Implemented as an ArrayList.
    /// </summary>
    [Serializable]
    public class GeoObjectList : IEnumerable, ISerializable, IDeserializationCallback, IEnumerable<IGeoObject>, IJsonSerialize
    {
        private ArrayList alist; // used for ISerializabe and IDeserializationCallback since list has been changed to List<IGeoObject>, which cannot be serialized/deserialized
        /// <summary>
        /// The objects of this list in their order. This is internal state exposed to derived classes; modify it through the
        /// methods of this class so that <see cref="ObjectAddedEvent"/> and <see cref="ObjectRemovedEvent"/> are raised.
        /// </summary>
        protected List<IGeoObject> list;
        private UserData userData;
        /// <summary>
        /// Handler for <see cref="ObjectAddedEvent"/>.
        /// </summary>
        /// <param name="sender">the list that changed</param>
        /// <param name="addedObject">the object that was added</param>
        /// <param name="lastRangeElement">true for a single added object and for the last object of a range added with
        /// <see cref="AddRange"/>, false for the other objects of a range; lets a handler postpone its work until the whole
        /// range has arrived</param>
        public delegate void ObjectAddedDelegate(GeoObjectList sender, IGeoObject addedObject, bool lastRangeElement);
        /// <summary>
        /// Raised after an object was added to the list, once for each object.
        /// </summary>
        public event ObjectAddedDelegate ObjectAddedEvent;
        /// <summary>
        /// Handler for <see cref="ObjectRemovedEvent"/>.
        /// </summary>
        /// <param name="sender">the list that changed</param>
        /// <param name="removedObject">the object that was removed</param>
        /// <param name="lastRangeElement">true for a single removed object and for the last object of a range removed with
        /// <see cref="RemoveChildrenOf"/>, false for the other objects of a range</param>
        public delegate void ObjectRemovedDelegate(GeoObjectList sender, IGeoObject removedObject, bool lastRangeElement);
        /// <summary>
        /// Raised after an object was removed from the list, once for each object. Not raised by <see cref="Clear"/>.
        /// </summary>
        public event ObjectRemovedDelegate ObjectRemovedEvent;

        /// <summary>
        /// Creates an empty list.
        /// </summary>
        public GeoObjectList()
        {
            list = new List<IGeoObject>();
            userData = new UserData();
        }
        /// <summary>
        /// Creates an empty list with room for <paramref name="Capacity"/> objects.
        /// </summary>
        /// <param name="Capacity">the initial capacity</param>
        public GeoObjectList(int Capacity)
        {
            list = new List<IGeoObject>(Capacity);
            userData = new UserData();
        }
        /// <summary>
        /// Creates a list that contains only <paramref name="singleObject"/>.
        /// </summary>
        /// <param name="singleObject">the object to put into the list</param>
        public GeoObjectList(IGeoObject singleObject)
            : this(1)
        {
            list.Add(singleObject);
        }
        /// <summary>
        /// Creates a list that contains the given objects in the given order.
        /// </summary>
        /// <param name="l">the objects to put into the list</param>
        public GeoObjectList(params IGeoObject[] l)
            : this(l.Length)
        {
            list.AddRange(l);
        }
        /// <summary>
        /// Creates a list that contains the objects of <paramref name="list"/> in their order. The objects are copied; the
        /// new list is independent of <paramref name="list"/>.
        /// </summary>
        /// <param name="list">the objects to put into the list</param>
        public GeoObjectList(List<IGeoObject> list)
            : this(list.Count)
        {
            AddRange(list.ToArray());
        }
        /// <summary>
        /// Creates a list that contains the objects of <paramref name="list"/> in their order. The objects are copied; the
        /// new list is independent of <paramref name="list"/>.
        /// </summary>
        /// <param name="list">the objects to put into the list</param>
        public GeoObjectList(ICollection<IGeoObject> list)
            : this(list.Count)
        {
            this.list.AddRange(list);
        }
        /// <summary>
        /// Creates a list that contains the objects of <paramref name="list"/> in their order. The sequence is enumerated
        /// twice, once to determine the capacity and once to copy the objects.
        /// </summary>
        /// <param name="list">the objects to put into the list</param>
        public GeoObjectList(IEnumerable<IGeoObject> list)
            : this(list.ToArray().Length)
        {
            this.list.AddRange(list);
        }
        /// <summary>
        /// Creates a list that contains the given curves (every <see cref="ICurve"/> is an <see cref="IGeoObject"/>) in
        /// their order. Null entries are skipped. The sequence is enumerated twice.
        /// </summary>
        /// <param name="list">the curves to put into the list</param>
        public GeoObjectList(IEnumerable<ICurve> list)
            : this(list.ToArray().Length)
        {
            foreach (ICurve crv in list)
            {
                this.list.AddIfNotNull(crv);
            }
        }
        /// <summary>
        /// Appends <paramref name="ObjectToAdd"/> at the end of the list and raises <see cref="ObjectAddedEvent"/>. The
        /// object may already be contained; use <see cref="AddUnique"/> to avoid duplicates.
        /// </summary>
        /// <param name="ObjectToAdd">the object to add</param>
        public void Add(IGeoObject ObjectToAdd)
        {
            list.Add(ObjectToAdd);
            ObjectAddedEvent?.Invoke(this, ObjectToAdd, true);
        }
        /// <summary>
        /// Inserts <paramref name="ObjectToAdd"/> at the position <paramref name="index"/>, moving the following objects
        /// back by one, and raises <see cref="ObjectAddedEvent"/>.
        /// </summary>
        /// <param name="index">the position of the new object, 0 to <see cref="Count"/>; otherwise an
        /// <see cref="ArgumentOutOfRangeException"/> is thrown</param>
        /// <param name="ObjectToAdd">the object to insert</param>
        public void Insert(int index, IGeoObject ObjectToAdd)
        {
            list.Insert(index, ObjectToAdd);
            ObjectAddedEvent?.Invoke(this, ObjectToAdd, true);
        }
        /// <summary>
        /// Appends <paramref name="ObjectToAdd"/> unless it is already contained in the list (linear search). Raises
        /// <see cref="ObjectAddedEvent"/> only when the object is actually added.
        /// </summary>
        /// <param name="ObjectToAdd">the object to add</param>
        public void AddUnique(IGeoObject ObjectToAdd)
        {	// da list eine ArrayList ist, ist AddUnique u.U. aufwendig
            // Überlegung: sollte man GeoObjectList per Eigenschaft auch parallel als
            // HashTable implementieren? Das würde hier Vorteile bringen.
            if (!list.Contains(ObjectToAdd))
                Add(ObjectToAdd);
        }
        /// <summary>
        /// Adds <paramref name="ObjectToAdd"/>, but a <see cref="Block"/> is replaced by its children, recursively; a
        /// <see cref="Hatch"/> (which is a block) is recalculated when necessary and then replaced by its hatch lines in the
        /// same way. All other objects are added as they are. <see cref="ObjectAddedEvent"/> is raised for each object that
        /// is actually added.
        /// </summary>
        /// <param name="ObjectToAdd">the object to add</param>
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
                    Add(path.Curve(i));
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

        /// <summary>
        /// Appends all objects of <paramref name="ObjectsToAdd"/> in their order and raises <see cref="ObjectAddedEvent"/>
        /// for each of them, with <c>lastRangeElement</c> set for the last one.
        /// </summary>
        /// <param name="ObjectsToAdd">the objects to add</param>
        public void AddRange(IEnumerable<IGeoObject> ObjectsToAdd)
        {
            list.AddRange(ObjectsToAdd);
            if (ObjectAddedEvent != null)
            {
                List<IGeoObject> objectsAdded = new List<IGeoObject>(ObjectsToAdd);
                for (int i = 0; i < objectsAdded.Count; i++) ObjectAddedEvent(this, objectsAdded[i], i == objectsAdded.Count - 1);
            }
        }

        /// <summary>
        /// Appends those objects of <paramref name="ObjectsToAdd"/> that are not yet contained in this list, raising
        /// <see cref="ObjectAddedEvent"/> for each of them. Note that the objects which are already contained are removed
        /// from <paramref name="ObjectsToAdd"/>, so that list is modified as well.
        /// </summary>
        /// <param name="ObjectsToAdd">the objects to add; loses the objects that were already contained</param>
        public void AddRangeUnique(GeoObjectList ObjectsToAdd)
        {   // erst die überflüssigen entfernen, dann alle verbleibenden zufügen wg. ObjectAddedEvent
            for (int i = ObjectsToAdd.Count - 1; i >= 0; --i)
            {
                int index = list.IndexOf(ObjectsToAdd[i]);
                if (index >= 0) ObjectsToAdd.Remove(i);
            }
            AddRange(ObjectsToAdd);
        }
        /// <summary>
        /// Removes the object at the position <paramref name="index"/> and raises <see cref="ObjectRemovedEvent"/>.
        /// </summary>
        /// <param name="index">the position of the object to remove; an invalid index throws an
        /// <see cref="ArgumentOutOfRangeException"/></param>
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
        /// <summary>
        /// Removes the first occurrence of <paramref name="ObjectToRemove"/> from the list and raises
        /// <see cref="ObjectRemovedEvent"/>. Does nothing when the object is not contained.
        /// </summary>
        /// <param name="ObjectToRemove">the object to remove</param>
        public void Remove(IGeoObject ObjectToRemove)
        {
            int index = list.IndexOf(ObjectToRemove);
            if (index >= 0) Remove(index);
        }
        /// <summary>
        /// Replaces every <see cref="Block"/> of the list by its children, recursively, so that afterwards the list contains
        /// no blocks any more. A <see cref="Hatch"/> at the top level of the list is kept (it is only recalculated when
        /// necessary), while hatches inside blocks are replaced by their hatch lines; <see cref="BlockRef"/> objects are
        /// not touched, see <see cref="DecomposeBlockRefs"/>. The children are appended at the end, so the order of the
        /// list changes. <see cref="ObjectRemovedEvent"/> and <see cref="ObjectAddedEvent"/> are raised accordingly.
        /// </summary>
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
        /// <summary>
        /// Replaces every <see cref="Block"/> and every <see cref="BlockRef"/> (through its <see cref="BlockRef.Flattened"/>
        /// block) by its contents, recursively; a <see cref="Hatch"/> is only decomposed into its hatch lines when
        /// <paramref name="decomposeHatch"/> is true. A <see cref="Path"/> taken out of a block is replaced by its segments,
        /// whereas paths at the top level of the list are kept. Objects taken out of a nested block inherit the layer and
        /// color of that block when they have none of their own (<see cref="IGeoObject.PropagateAttributes"/>). The contents
        /// are appended at the end, so the order of the list changes.
        /// </summary>
        /// <param name="decomposeHatch">true to replace hatches by their hatch lines, false to keep them</param>
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
        /// <summary>
        /// Replaces every <see cref="BlockRef"/> by the objects of <see cref="BlockRef.Decompose"/>, i.e. the objects of the
        /// referenced block with the insertion applied, repeating this until no block reference is left (the decomposed
        /// objects may contain block references again). <see cref="Block"/> objects are not touched, see
        /// <see cref="DecomposeBlocks()"/>.
        /// </summary>
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
        /// <summary>
        /// Decomposes the list down to simple objects: every <see cref="Block"/> (hatches included) and every
        /// <see cref="BlockRef"/> is replaced by its contents, recursively, every <see cref="Path"/> by its segments and
        /// every <see cref="Polyline"/> by lines. Objects taken out of a block inherit the layer and color of the block when
        /// they have none of their own (<see cref="IGeoObject.PropagateAttributes"/>). The parts are appended at the end,
        /// so the order of the list changes.
        /// </summary>
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
        /// <summary>
        /// Reverses the order of the objects in the list. No events are raised.
        /// </summary>
        public void Reverse()
        {
            list.Reverse();
        }
        /// <summary>
        /// Removes all objects from the list without raising <see cref="ObjectRemovedEvent"/>.
        /// </summary>
        public void Clear()
        {
            list.Clear();
        }
        /// <summary>
        /// Checks whether <paramref name="ToTest"/> is contained in the list (same object, linear search).
        /// </summary>
        /// <param name="ToTest">the object to look for</param>
        /// <returns>true if the object is in the list</returns>
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
        /// <summary>
        /// Returns the position of <paramref name="ToTest"/> in the list.
        /// </summary>
        /// <param name="ToTest">the object to look for</param>
        /// <returns>the index of the first occurrence, or -1 if the object is not in the list</returns>
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
        /// <summary>
        /// Returns the 2d extent of all objects of the list as they appear in <paramref name="projection"/>, in the
        /// coordinates of the projection's plane: the union of <see cref="IGeoObject.GetExtent(Projection, ExtentPrecision)"/>
        /// with <see cref="ExtentPrecision.Raw"/>. The parameters <paramref name="Use2DWorld"/> and
        /// <paramref name="RegardLineWidth"/> are ignored.
        /// </summary>
        /// <param name="projection">the projection</param>
        /// <param name="Use2DWorld">ignored</param>
        /// <param name="RegardLineWidth">ignored</param>
        /// <returns>the extent, <see cref="BoundingRect.EmptyBoundingRect"/> for an empty list</returns>
        public BoundingRect GetExtent(Projection projection, bool Use2DWorld, bool RegardLineWidth)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            foreach (IGeoObject go in list)
            {
                res.MinMax(go.GetExtent(projection, ExtentPrecision.Raw));
            }
            return res;
        }
        /// <summary>
        /// Returns the 3d extent of all objects of the list, the union of their <see cref="IGeoObject.GetBoundingCube"/>.
        /// </summary>
        /// <returns>the extent, <see cref="BoundingCube.EmptyBoundingCube"/> for an empty list</returns>
        public BoundingCube GetExtent()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            foreach (IGeoObject go in list)
            {
                res.MinMax(go.GetBoundingCube());
            }
            return res;
        }
        /// <summary>
        /// Applies the modification <paramref name="m"/> to every object of the list, see <see cref="IGeoObject.Modify"/>.
        /// Each object raises its change events, so the modification is recorded for undo when the objects belong to a model.
        /// </summary>
        /// <param name="m">the modification to apply</param>
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
        /// <summary>
        /// Arbitrary data attached to this list, see <see cref="CADability.UserData"/>. Empty by default; saved together
        /// with the list.
        /// </summary>
        public UserData UserData { get { return userData; } }
        /// <summary>
        /// Gets or sets the object at the position <paramref name="Index"/>. Setting replaces the object without raising
        /// <see cref="ObjectRemovedEvent"/> or <see cref="ObjectAddedEvent"/>. An invalid index throws an
        /// <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        /// <param name="Index">the position, 0 to <see cref="Count"/> - 1</param>
        public IGeoObject this[int Index]
        {
            get { return list[Index]; }
            set { list[Index] = value; }
        }
        /// <summary>
        /// Converts the list to an array of its objects. The array is a copy: changing it does not change the list.
        /// </summary>
        /// <param name="l">the list to convert</param>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
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

        /// <summary>
        /// Writes the objects and the user data of this list for the JSON serialization (<see cref="IJsonSerialize"/>).
        /// </summary>
        /// <param name="data">receives the data</param>
        public void GetObjectData(IJsonWriteData data)
        {
            data.AddProperty("List", list);
            data.AddProperty("UserData", userData);
        }

        /// <summary>
        /// Reads the objects and the user data written by <see cref="GetObjectData(IJsonWriteData)"/> back from the JSON
        /// serialization.
        /// </summary>
        /// <param name="data">provides the data</param>
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
            HashSet<IGeoObject> set = new HashSet<IGeoObject>();
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
