using CADability.Attribute;
using CADability.UserInterface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;


namespace CADability.GeoObject
{

    /// <summary>
    /// Ein Block als geometrisches Objekt. Der Block besitzt seine Kinder. Beim Clonen
    /// werden Kopien der Kinder erzeugt.
    /// A collection of several <see cref="IGeoObject"/>s 
    /// </summary>
    [Serializable()]
    public class Block : IGeoObjectImpl, IColorDef, ISerializable, IGeoObjectOwner, IDeserializationCallback, IExportStep, IJsonSerialize, IJsonSerializeDone
    {
        private GeoPoint refPoint;
        protected GeoObjectList containedObjects;
        private string name;
        private ColorDef CDfromParent;
        private ColorDef colorDef;
        #region polymorph construction
        public delegate Block ConstructionDelegate();
        public static ConstructionDelegate Constructor;
        public static Block Construct()
        {
            if (Constructor != null) return Constructor();
            return new Block();
        }
        internal static Block ConstructInternal()
        {
            return new Block();
        }
        #endregion
        protected Block() // wird gebraucht wg. BlockRef Paint
        {
            containedObjects = new GeoObjectList();
            CDfromParent = ColorDef.CDfromParent.Clone();
        }
        private void OnWillChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (isChanging == 0)
            {
                FireWillChange(Change);
            }
        }
        private void OnDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (isChanging == 0)
            {
                FireDidChange(Change);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                containedObjects.Modify(m);
                refPoint = m * refPoint;
            }
        }
        /// <summary>
        /// Overrides <see cref="IGeoObjectImpl.Clone"/>. Returns a new Block, that contains clones of the
        /// children of this Block. An <see cref="IGeoObject"/> cannot be contained by two different block 
        /// objects, because there may be only one owner (see also <see cref="IGeoObject.Owner"/>).
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            lock (this)
            {
                Block result = Construct();
                result.refPoint = refPoint; // nicht auf die Property, denn die verwendet den Changing Mechanismus, der auf ein Clone() führt
                result.name = name;
                foreach (IGeoObject go in containedObjects)
                    result.AddNoNotification(go.Clone());
                result.CopyAttributes(this);
                return result;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Child (int)"/>
        /// </summary>
        /// <param name="Index"></param>
        /// <returns></returns>
        public override IGeoObject Child(int Index)
        {
            return Item(Index);
        }
        /// <summary>
        /// Yields a copy of the list of contained geoobjects. Removing objects from that list
        /// doesn't remove the objects from the Block.
        /// </summary>
        public GeoObjectList Children
        {
            get
            {
                return new GeoObjectList(containedObjects);
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            using (new Changing(this, "CopyGeometry", this)) // TODO: stimmt so nicht, wg. Undo noch überlegen!
            {
                lock (this)
                {
                    Block CopyFromBlock = (Block)ToCopyFrom;
                    refPoint = CopyFromBlock.RefPoint;
                    for (int i = 0; i < CopyFromBlock.Count; ++i)
                    {
                        containedObjects[i].CopyGeometry(CopyFromBlock.Item(i));
                    }
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            BoundingCube res = BoundingCube.EmptyBoundingCube;
            // lock (this)
            {
                for (int i = 0; i < containedObjects.Count; ++i)
                {
                    res.MinMax(containedObjects[i].GetBoundingCube());
                }
            }
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Decompose ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoObjectList Decompose()
        {
            GeoObjectList res = this.Children.CloneObjects();
            for (int i = 0; i < res.Count; i++)
            {
                res[i].PropagateAttributes(this.Layer, this.colorDef);
            }
            return res;
        }
        public override string Description
        {
            get
            {
                return StringTable.GetString("Block.Description");
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
        /// </summary>
        /// <param name="spf"></param>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            lock (this)
            {
                foreach (IGeoObject go in containedObjects)
                {
                    go.FindSnapPoint(spf);
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HasChildren ()"/>
        /// </summary>
        /// <returns></returns>
        public override bool HasChildren()
        {
            return containedObjects.Count > 0;
        }
        public GeoPoint RefPoint
        {
            get
            {
                return refPoint;
            }
            set
            {
                using (new Changing(this, "RefPoint"))
                {
                    refPoint = value;

                }
            }
        }
        /// <summary>
        /// Gets the number of GeoObjects in the Block
        /// </summary>
        public int Count
        {
            get
            {
                return containedObjects.Count;
            }
        }
        public IGeoObject Item(int Index)
        {
            return containedObjects[Index];
        }
        private void AddNoNotification(IGeoObject ToAdd)
        {
            if (ToAdd.Owner != null) ToAdd.Owner.Remove(ToAdd);
            lock (this)
            {
                containedObjects.Add(ToAdd);
            }
            ToAdd.WillChangeEvent += new ChangeDelegate(OnWillChange);
            ToAdd.DidChangeEvent += new ChangeDelegate(OnDidChange);
            ToAdd.Owner = this;
            IColorDef cd = ToAdd as IColorDef;
            if (cd != null && cd.ColorDef != null)
                if (cd.ColorDef.Source == ColorDef.ColorSource.fromParent)
                    cd.ColorDef = CDfromParent;
        }
        public virtual void Add(IGeoObject ToAdd)
        {
            using (new Changing(this, "Remove", ToAdd))
            {
                if (ToAdd.Owner != null) ToAdd.Owner.Remove(ToAdd);
                lock (this)
                {
                    containedObjects.Add(ToAdd);
                }
                ToAdd.WillChangeEvent += new ChangeDelegate(OnWillChange);
                ToAdd.DidChangeEvent += new ChangeDelegate(OnDidChange);
                ToAdd.Owner = this;
                IColorDef cd = ToAdd as IColorDef;
                if (cd != null && cd.ColorDef != null)
                    if (cd.ColorDef.Source == ColorDef.ColorSource.fromParent)
                        cd.ColorDef = CDfromParent;
            }
        }
        public virtual void Remove(int Index)
        {
            using (new Changing(this, "Add", containedObjects[Index]))
            {
                IGeoObject go = containedObjects[Index];
                go.WillChangeEvent -= new ChangeDelegate(OnWillChange);
                go.DidChangeEvent -= new ChangeDelegate(OnDidChange);
                go.Owner = null;
                IColorDef cd = go as IColorDef;
                if (cd != null && cd.ColorDef != null)
                    if (cd.ColorDef.Source == ColorDef.ColorSource.fromParent)
                        cd.ColorDef = ColorDef.CDfromParent;
                lock (this)
                {
                    containedObjects.Remove(Index);
                }
            }
        }
        internal void Remove(IGeoObject go)
        {
            for (int i = 0; i < containedObjects.Count; i++)
            {
                if (containedObjects[i] == go)
                {
                    Remove(i);
                    break;
                }
            }
        }

        public virtual void Remove(GeoObjectList ToRemove)
        {
            using (new Changing(this, "Add", new object[] { ToRemove }))
            {
                foreach (IGeoObject go in ToRemove)
                {
                    (this as IGeoObjectOwner).Remove(go);
                }
            }
        }
        public virtual void Set(GeoObjectList toSet)
        {
            using (new Changing(this, "Set", new object[] { containedObjects.Clone() }))
            {
                ReplaceContent(toSet);
            }
        }
        public virtual void Add(GeoObjectList ToAdd)
        {
            using (new Changing(this, "Remove", ToAdd))
            {
                foreach (IGeoObject go in ToAdd)
                {
                    lock (this)
                    {
                        containedObjects.Add(go);
                    }
                    go.WillChangeEvent += new ChangeDelegate(OnWillChange);
                    go.DidChangeEvent += new ChangeDelegate(OnDidChange);
                    go.Owner = this;
                    IColorDef cd = go as IColorDef;
                    if (cd != null && cd.ColorDef != null)
                        if (cd.ColorDef.Source == ColorDef.ColorSource.fromParent)
                            cd.ColorDef = CDfromParent;
                }
            }
        }
        public virtual void Insert(int index, IGeoObject ToAdd)
        {
            using (new Changing(this, "Remove", ToAdd))
            {
                if (ToAdd.Owner != null) ToAdd.Owner.Remove(ToAdd);
                lock (this)
                {
                    containedObjects.Insert(index, ToAdd);
                }
                ToAdd.WillChangeEvent += new ChangeDelegate(OnWillChange);
                ToAdd.DidChangeEvent += new ChangeDelegate(OnDidChange);
                ToAdd.Owner = this;
                IColorDef cd = ToAdd as IColorDef;
                if (cd != null && cd.ColorDef != null)
                    if (cd.ColorDef.Source == ColorDef.ColorSource.fromParent)
                        cd.ColorDef = CDfromParent;
            }
        }
        private bool isTmpContainer;
        public bool IsTmpContainer
        {
            get
            {
                return isTmpContainer;
            }
            set
            {
                isTmpContainer = value;
                if (isTmpContainer) PropagateAttributes();
            }
        }
        public override Style Style
        {
            get
            {
                return base.Style;
            }
            set
            {
                base.Style = value;
                if (isTmpContainer) PropagateAttributes();
            }
        }
        public override Layer Layer
        {
            get
            {
                return base.Layer;
            }
            set
            {
                base.Layer = value;
                if (isTmpContainer) PropagateAttributes();
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {
            lock (this)
            {
                for (int i = 0; i < containedObjects.Count; ++i)
                {   // die Überprüfung der Sichtbarkeit erfolgt vor dem Aufruf dieser Methode. Deshalb
                    // muss sie auch vor dem Aufruf der enthaltenen Objekte erfolgen. Sonst müsste man bei jedem geoObject
                    // diesen Test machen
                    if (containedObjects[i].IsVisible)
                        containedObjects[i].PaintTo3DList(paintTo3D, lists);
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrePaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            // lock (this)
            {
                foreach (IGeoObjectImpl go in containedObjects)
                {
                    go.PrePaintTo3D(paintTo3D);
                }
            }
        }
        public delegate bool PaintTo3DDelegate(Block toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            // kommt im Normalfall nicht dran, da der Block beim Darstellen schon aufgelöst wird
            // (wg. der verschiedenen Layer)
            // wird allerdings beim aktiven Objekt aufgerufen
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            // lock (this) das lock ausgeschaltet, sollte das nicht bei den einzelnen Objekten, vor allem Faces, genügen?
            // 
            {
                foreach (IGeoObjectImpl go in containedObjects)
                {
                    go.PaintTo3D(paintTo3D);
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            // lock (this)
            {
                foreach (IGeoObjectImpl go in containedObjects)
                {
                    go.PrepareDisplayList(precision);
                }
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            BoundingRect res = BoundingRect.EmptyBoundingRect;
            // lock (this) kann sich containedObjects währenddessen ändern???
            {
                foreach (IGeoObject go in containedObjects)
                {
                    res.MinMax(go.GetExtent(projection, extentPrecision));
                }
            }
            return res;
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
            lock (this)
            {
                for (int i = 0; i < containedObjects.Count; ++i)
                {
                    res.Add(containedObjects[i].GetQuadTreeItem(projection, extentPrecision));
                }
            }
            return res;
        }
        private void PropagateAttributes()
        {
            lock (this)
            {
                for (int i = 0; i < containedObjects.Count; ++i)
                {
                    containedObjects[i].CopyAttributes(this);
                }
            }
        }
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                using (new Changing(this, false, true, "Name", name))
                {
                    name = value;
                }
            }
        }
        /// <summary>
        /// Removes all objects from that block and returns those objects in a new list. The returned objects
        /// have no <see cref="IGeoObject.Owner"/>.
        /// </summary>
        /// <returns>a list with all the objects of this block</returns>
        public GeoObjectList Clear()
        {
            GeoObjectList res = new GeoObjectList(containedObjects);
            using (new Changing(this, "Add", containedObjects.Clone()))
            {
                lock (this)
                {
                    foreach (IGeoObject go in containedObjects)
                    {
                        go.WillChangeEvent -= new ChangeDelegate(OnWillChange);
                        go.DidChangeEvent -= new ChangeDelegate(OnDidChange);
                        go.Owner = null;
                        IColorDef cd = go as IColorDef;
                        if ((cd != null) && (cd.ColorDef != null))
                        {
                            if (cd.ColorDef.Source == ColorDef.ColorSource.fromParent)
                                cd.ColorDef = ColorDef.CDfromParent;
                        }
                    }
                    containedObjects.Clear();
                }
            }
            return res;
        }
        /// <summary>
        /// Replace the list of child objects without fireing a chang event. This is used
        /// by derived classes (e.g. <see cref="Hatch"/> or <see cref="Dimension"/> or user derived classes
        /// to recalculate their contents. The derived classes already have fired the change
        /// event.
        /// </summary>
        /// <param name="NewContent">List of child objects to replace the existing list</param>
        protected void ReplaceContent(GeoObjectList NewContent)
        {
            foreach (IGeoObject go in containedObjects)
            {
                go.WillChangeEvent -= new ChangeDelegate(OnWillChange); // if some software cannot deal with this (Trumpf...) it must be handeled elsewhere
                go.DidChangeEvent -= new ChangeDelegate(OnDidChange);
                go.Owner = null;
                IColorDef cd = go as IColorDef;
                if (cd != null && cd.ColorDef != null)
                    if (cd.ColorDef.Source == ColorDef.ColorSource.fromParent)
                        cd.ColorDef = ColorDef.CDfromParent;
            }
            lock (this)
            {
                containedObjects.Clear();
            }
            foreach (IGeoObject go in NewContent)
            {
                lock (this)
                {
                    containedObjects.Add(go);
                }
                go.WillChangeEvent += new ChangeDelegate(OnWillChange);
                go.DidChangeEvent += new ChangeDelegate(OnDidChange);
                go.Owner = this;
                IColorDef cd = go as IColorDef;
                if (cd != null && cd.ColorDef != null)
                    if (cd.ColorDef.Source == ColorDef.ColorSource.fromParent)
                        cd.ColorDef = CDfromParent;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyBlock(this, Frame);
        }
        public override int NumChildren
        {
            get
            {
                // TODO:  Add Block.NumChildren getter implementation
                return containedObjects.Count;
            }
        }
        internal ColorDef GetCDfromParent() { return CDfromParent; }
        public override IGeoObject[] OwnedItems
        {
            get
            {
                return containedObjects;
            }
        }
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
            // lock (this)
            {
                for (int i = 0; i < containedObjects.Count; ++i)
                {
                    if (containedObjects[i].HitTest(ref cube, precision)) return true;
                }
                return false;
            }
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
            // lock (this)
            {
                if (onlyInside)
                {   // alle müssen ganz drin sein
                    for (int i = 0; i < containedObjects.Count; ++i)
                    {
                        if (!containedObjects[i].HitTest(projection, rect, onlyInside)) return false;
                    }
                    return true;
                }
                else
                {   // wenigsten eines muss teilweise drin sein
                    for (int i = 0; i < containedObjects.Count; ++i)
                    {
                        if (containedObjects[i].HitTest(projection, rect, onlyInside)) return true;
                    }
                    return false;
                }
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
            // lock (this)
            {
                if (onlyInside)
                {   // alle müssen ganz drin sein
                    for (int i = 0; i < containedObjects.Count; ++i)
                    {
                        if (!containedObjects[i].HitTest(area, onlyInside)) return false;
                    }
                    return true;
                }
                else
                {   // wenigsten eines muss teilweise drin sein
                    for (int i = 0; i < containedObjects.Count; ++i)
                    {
                        if (containedObjects[i].HitTest(area, onlyInside)) return true;
                    }
                    return false;
                }
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
            // lock (this)
            {
                double res = double.MaxValue;
                for (int i = 0; i < containedObjects.Count; ++i)
                {
                    double d = containedObjects[i].Position(fromHere, direction, precision);
                    if (d < res) res = d;
                }
                return res;
            }
        }
        #endregion
        #region IColorDef Members
        public virtual ColorDef ColorDef
        {
            get
            {
                return colorDef;
            }
            set
            {
                if (colorDef != value)
                {
                    if (colorDef != null) colorDef.ColorDidChangeEvent -= new AttributeChangeDelegate(colorDef_ColorDidChange);
                    SetColorDef(ref colorDef, value);
                    if (colorDef != null)
                    {
                        if (CDfromParent != null) CDfromParent.Color = colorDef.Color;
                        colorDef.ColorDidChangeEvent += new AttributeChangeDelegate(colorDef_ColorDidChange);
                    }
                    else
                        if (CDfromParent != null) CDfromParent.Color = ColorDef.CDfromParent.Color;
                    if (isTmpContainer) PropagateAttributes();
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
                if (containedObjects != null)
                {
                    for (int i = 0; i < containedObjects.Count; ++i)
                    {
                        if ((containedObjects[i] is IColorDef) && (containedObjects[i] as IColorDef).ColorDef == null) (containedObjects[i] as IColorDef).SetTopLevel(newValue, true);
                    }
                }
            }
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Block(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            try
            {
                colorDef = (ColorDef)info.GetValue("ColorDef", typeof(ColorDef));

                refPoint = (GeoPoint)info.GetValue("RefPoint", typeof(GeoPoint));
                containedObjects = (GeoObjectList)info.GetValue("ContainedObjects", typeof(GeoObjectList));
                name = (string)info.GetValue("Name", typeof(string));
                // FinishDeserialization.AddToContext(context,this);
            }
            catch (SerializationException)
            {
                SerializationInfoEnumerator e = info.GetEnumerator();
                while (e.MoveNext())
                {
                    System.Diagnostics.Trace.WriteLine("im Block fehlt: " + e.Name);
                }
            }
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ColorDef", colorDef);
            info.AddValue("RefPoint", refPoint);
            info.AddValue("ContainedObjects", containedObjects);
            info.AddValue("Name", name);
        }
        #endregion
        #region IDeserializationCallback Members
        //void IDeserializationCallback.OnDeserialization(object sender)
        public virtual void OnDeserialization(object sender)
        {
            lock (this)
            {
                (containedObjects as IDeserializationCallback).OnDeserialization(sender); // must be called before this here
                for (int i = 0; i < containedObjects.Count; ++i)
                {
                    containedObjects[i].WillChangeEvent += new ChangeDelegate(OnWillChange);
                    containedObjects[i].DidChangeEvent += new ChangeDelegate(OnDidChange);
                    containedObjects[i].Owner = this;
                }
            }
        }
        #endregion
        /// <summary>
        /// Need to make it public in order to allow inheriting classes to acces this method via base.GetObjectData
        /// </summary>
        /// <param name="data"></param>
        public override void GetObjectData(IJsonWriteData data)
        {
            data.RegisterForSerializationDoneCallback(this);
            base.GetObjectData(data);
            data.AddProperty("ColorDef", colorDef);
            data.AddProperty("RefPoint", refPoint);
            data.AddProperty("ContainedObjects", containedObjects);
            data.AddProperty("Name", name);
        }

        public override void SetObjectData(IJsonReadData data)
        {
            base.SetObjectData(data);
            refPoint = data.GetProperty<GeoPoint>("RefPoint");
            containedObjects = data.GetProperty<GeoObjectList>("ContainedObjects");
            colorDef = data.GetPropertyOrDefault<ColorDef>("ColorDef");
            name = data.GetPropertyOrDefault<string>("Name");
            data.RegisterForSerializationDoneCallback(this);
        }
        public virtual void SerializationDone(JsonSerialize jsonSerialize)
        {
            if (jsonSerialize.IsWriting)
            {

            }
            else
            {
                for (int i = 0; i < containedObjects.Count; i++)
                {
                    jsonSerialize.InvokeSerializationDoneCallback(containedObjects[i]);
                }
                lock (this)
                {
                    for (int i = 0; i < containedObjects.Count; ++i)
                    {
                        containedObjects[i].WillChangeEvent += new ChangeDelegate(OnWillChange);
                        containedObjects[i].DidChangeEvent += new ChangeDelegate(OnDidChange);
                        containedObjects[i].Owner = this;
                    }
                }
            }
        }
        private void colorDef_ColorDidChange(object sender, ChangeEventArgs eventArguments)
        {
            if (CDfromParent != null && colorDef != null)
            {
                CDfromParent.Color = colorDef.Color;
            }
        }
        #region IGeoObjectOwner Members

        void CADability.GeoObject.IGeoObjectOwner.Remove(IGeoObject toRemove)
        {
            int ind = containedObjects.IndexOf(toRemove);
            if (ind >= 0) Remove(ind);
        }

        #endregion

        internal void MoveToBack(IGeoObject toMove)
        {
            containedObjects.MoveToBack(toMove);
        }
        internal void MoveToFront(IGeoObject toMove)
        {
            containedObjects.MoveToFront(toMove);
        }

        int IExportStep.Export(ExportStep export, bool topLevel)
        {
            /*
#248 = ADVANCED_BREP_SHAPE_REPRESENTATION( 'Assem1', ( #717, #718, #719, #720, #721, #722, #723, #724, #725, #726, #727 ), #26 );
#727 = AXIS2_PLACEMENT_3D( '', #1437, #1438, #1439 );
#717 = MAPPED_ITEM( '', #1417, #1418 );
#1417 = REPRESENTATION_MAP( #727, #251 );
#1418 = AXIS2_PLACEMENT_3D( '', #2213, #2214, #2215 );
#251 = ADVANCED_BREP_SHAPE_REPRESENTATION( 'A0501_SASIL_plus_00_50_185_3_polig', ( #730 ), #26 );
             */
            List<int> representationItems = new List<int>();
            int mainAxis = export.WriteAxis2Placement3d(GeoPoint.Origin, GeoVector.ZAxis, GeoVector.XAxis);
            for (int i = 0; i < containedObjects.Count; i++)
            {
                if (containedObjects[i] is IExportStep)
                {
                    int toMap = (containedObjects[i] as IExportStep).Export(export, true); // true is correct here, because it is part of a map
                    int axis = export.WriteAxis2Placement3d(GeoPoint.Origin, GeoVector.ZAxis, GeoVector.XAxis);
                    int repMap = export.WriteDefinition("REPRESENTATION_MAP(#" + mainAxis.ToString() + ",#" + toMap.ToString() + ")");
                    int mappedItem = export.WriteDefinition("MAPPED_ITEM( '', #" + repMap.ToString() + ",#" + axis.ToString() + ")");
                    representationItems.Add(mappedItem);
                }
            }
            representationItems.Add(mainAxis);
            int sr = export.WriteDefinition("SHAPE_REPRESENTATION('" + Name + "',(" + export.ToString(representationItems.ToArray(), true) + "),#4)");
            int product = export.WriteDefinition("PRODUCT( '" + Name + "','" + Name + "','',(#2))");
            int pdf = export.WriteDefinition("PRODUCT_DEFINITION_FORMATION_WITH_SPECIFIED_SOURCE( ' ', 'NONE', #" + product.ToString() + ", .NOT_KNOWN. )");
            int pd = export.WriteDefinition("PRODUCT_DEFINITION( 'NONE', 'NONE', #" + pdf.ToString() + ", #3 )");
            int pds = export.WriteDefinition("PRODUCT_DEFINITION_SHAPE( 'NONE', 'NONE', #" + pd.ToString() + " )");
            export.WriteDefinition("SHAPE_DEFINITION_REPRESENTATION( #" + pds.ToString() + ", #" + sr.ToString() + ")");

            return sr;
        }

    }

    public class QuadTreeCollection : IQuadTreeInsertableZ
    {
        QuadTree allObjects;
        IGeoObject go;
        BoundingRect extent;
        public QuadTreeCollection(IGeoObject go, Projection projection)
        {
            this.go = go;
            allObjects = new QuadTree();
            extent = BoundingRect.EmptyBoundingRect;
        }
        public void Add(IQuadTreeInsertableZ toAdd)
        {
            allObjects.AddObject(toAdd);
            extent.MinMax(toAdd.GetExtent());
        }
        public void SetOwner(IGeoObject go)
        {
            this.go = go;
        }
        public IQuadTreeInsertableZ[] GetObjectsFromRect(ref BoundingRect rect)
        {
            List<IQuadTreeInsertableZ> res = new List<IQuadTreeInsertableZ>();
            ICollection co = allObjects.GetObjectsFromRect(rect);
            foreach (IQuadTreeInsertableZ qi in co)
            {
                if (qi is QuadTreeCollection)
                {
                    res.AddRange((qi as QuadTreeCollection).GetObjectsFromRect(ref rect));
                }
                else
                {
                    res.Add(qi);
                }
            }
            return res.ToArray();
        }
        #region IQuadTreeInsertableZ Members

        double IQuadTreeInsertableZ.GetZPosition(GeoPoint2D p)
        {
            double z = double.MinValue;
            ICollection co = allObjects.GetObjectsFromRect(new BoundingRect(p));
            foreach (IQuadTreeInsertableZ qi in co)
            {
                z = Math.Max(z, qi.GetZPosition(p));
            }
            return z;
        }

        #endregion

        #region IQuadTreeInsertable Members

        BoundingRect IQuadTreeInsertable.GetExtent()
        {
            return extent;
        }

        bool IQuadTreeInsertable.HitTest(ref BoundingRect rect, bool includeControlPoints)
        {   // hier mit QuadTree schneller
            ICollection co = allObjects.GetObjectsFromRect(rect);
            foreach (IQuadTreeInsertableZ qi in co)
            {
                if (qi.HitTest(ref rect, includeControlPoints)) return true;
            }
            return false;
        }

        object IQuadTreeInsertable.ReferencedObject
        {
            get { return go; }
        }

        #endregion
    }
}
