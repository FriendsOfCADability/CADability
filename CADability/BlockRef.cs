using CADability.Actions;
using CADability.Attribute;
using CADability.UserInterface;
using System;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    internal class ShowPropertyBlockRef : IShowPropertyImpl, ICommandHandler, IGeoObjectShowProperty
    {
        BlockRef blockRef;
        GeoPointProperty positionProp;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        IFrame frame;
        public ShowPropertyBlockRef(BlockRef theBlockRef, IFrame theFrame)
        {
            blockRef = theBlockRef;
            frame = theFrame;
            resourceIdInternal = "BlockRef.Object";
            attributeProperties = blockRef.GetAttributeProperties(frame);
        }



        #region IShowPropertyImpl overrides
        public override string LabelText
        {
            get
            {
                return StringTable.GetFormattedString("BlockRef.Object.Label", blockRef.ReferencedBlock.Name);
            }
            set
            {
                base.LabelText = value;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Added"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Added(IPropertyPage propertyTreeView)
        {
            base.Added(propertyTreeView);
            if (positionProp == null)
                positionProp = new GeoPointProperty("BlockRef.Position", frame, true);
            positionProp.GetGeoPointEvent += new GeoPointProperty.GetGeoPointDelegate(OnGetLocation);
            positionProp.SetGeoPointEvent += new GeoPointProperty.SetGeoPointDelegate(OnSetLocation);
            positionProp.ModifyWithMouseEvent += new ModifyWithMouseDelegate(OnPositionModifyWithMouse);
            blockRef.UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            blockRef.UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataAdded);


        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Removed (IPropertyTreeView)"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Removed(IPropertyTreeView propertyTreeView)
        {
            positionProp.GetGeoPointEvent -= new GeoPointProperty.GetGeoPointDelegate(OnGetLocation);
            positionProp.SetGeoPointEvent -= new GeoPointProperty.SetGeoPointDelegate(OnSetLocation);
            positionProp.ModifyWithMouseEvent -= new ModifyWithMouseDelegate(OnPositionModifyWithMouse);
            blockRef.UserData.UserDataAddedEvent -= new UserData.UserDataAddedDelegate(OnUserDataAdded);
            blockRef.UserData.UserDataRemovedEvent -= new UserData.UserDataRemovedDelegate(OnUserDataAdded);

            base.Removed(propertyTreeView);
        }
        void OnUserDataAdded(string name, object value)
        {
            attributeProperties = blockRef.GetAttributeProperties(frame);
            propertyTreeView.Refresh(this);
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (positionProp == null)
                    positionProp = new GeoPointProperty("BlockRef.Position", frame, true);
                return PropertyEntryImpl.Concat(new IPropertyEntry[] { positionProp }, attributeProperties);
            }
        }

        public override ShowPropertyEntryType EntryType
        {
            get
            {
                return ShowPropertyEntryType.GroupTitle;
            }
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable;
            }
        }
        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                return MenuResource.LoadMenuDefinition("MenuId.Object.BlockRef", false, this);
                // some mor to implement, see below
            }
        }
#endregion
        private GeoPoint OnGetLocation(GeoPointProperty sender)
        {
            return blockRef.RefPoint;
        }
        private void OnSetLocation(GeoPointProperty sender, GeoPoint p)
        {
            blockRef.RefPoint = p;
        }

        private void OnPositionModifyWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(positionProp, blockRef);
            frame.SetAction(gpa);
        }


#region ICommandHandler Members

        /// <summary>
        /// Implements <see cref="CADability.UserInterface.ICommandHandler.OnCommand (string)"/>
        /// </summary>
        /// <param name="MenuId"></param>
        /// <returns></returns>
        virtual public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Explode":
                    if (frame.ActiveAction is SelectObjectsAction)
                    {
                        using (frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = blockRef.Owner;
                            if (addTo == null) addTo = frame.ActiveView.Model;
                            addTo.Remove(blockRef);
                            //IGeoObject go = blockRef.ReferencedBlock.Clone();
                            //go.PropagateAttributes(blockRef.Layer, blockRef.ColorDef);
                            GeoObjectList l = blockRef.Decompose();
                            addTo.Add(l[0]);
                            SelectObjectsAction soa = frame.ActiveAction as SelectObjectsAction;
                            soa.SetSelectedObjects(l); // alle Teilobjekte markieren
                        }
                    }
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Implements <see cref="CADability.UserInterface.ICommandHandler.OnUpdateCommand (string, CommandState)"/>
        /// </summary>
        /// <param name="MenuId"></param>
        /// <param name="CommandState"></param>
        /// <returns></returns>
        virtual public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Explode":
                    CommandState.Enabled = true; // naja isses ja immer
                    return true;
            }
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion
        #region IGeoObjectShowProperty Members
        public event CADability.GeoObject.CreateContextMenueDelegate CreateContextMenueEvent;
        IGeoObject IGeoObjectShowProperty.GetGeoObject()
        {
            return blockRef;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.BlockRef";
        }
#endregion
    }
    /// <summary>
    /// 
    /// </summary>
    [Serializable()]
    public class BlockRef : IGeoObjectImpl, IColorDef, IGeoObjectOwner, ISerializable
    {
        private Block idea;
        private ModOp insertion;
        WeakReference flattened, flattenedP;
        BoundingCube? extent;
#region polymorph construction
        public delegate BlockRef ConstructionDelegate(Block referredBlock);
        public static ConstructionDelegate Constructor;
        public static BlockRef Construct(Block referredBlock)
        {
            if (Constructor != null) return Constructor(referredBlock);
            return new BlockRef(referredBlock);
        }
#endregion
        protected BlockRef(Block referredBlock)
        {
            idea = referredBlock;
            insertion = ModOp.Translate(0.0, 0.0, 0.0);
        }

        private void OnWillChange(IGeoObject Sender, GeoObjectChange Change)
        {
            FireWillChange(Change);
        }
        private void OnDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (flattened != null) flattened.Target = null;
            FireDidChange(Change);
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                insertion = m * insertion;
                if (flattened != null) flattened.Target = null;
                extent = null;

                if (flattenedP != null && flattenedP.Target != null)
                {
                    (flattenedP.Target as Block).Modify(m);
                }

            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            BlockRef result = Construct(idea);
            result.Insertion = insertion;
            result.CopyAttributes(this);
            return result;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            if (!extent.HasValue)
            {
                extent = Flattened.GetBoundingCube();
            }
            return extent.Value;
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Decompose ()"/>
        /// </summary>
        /// <returns></returns>
        public override GeoObjectList Decompose()
        {
            return new GeoObjectList(Flattened);
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            using (new Changing(this))
            {
                BlockRef CopyFromBlockRef = (BlockRef)ToCopyFrom;
                idea = CopyFromBlockRef.idea;
                insertion = CopyFromBlockRef.Insertion;
                if (flattened != null) flattened.Target = null;
                extent = null;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyBlockRef(this, Frame);
        }
        public override string Description
        {
            get
            {
                return StringTable.GetString("BlockRef.Description");
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {
            //paintTo3D.PushMultModOp(insertion);
            //// TODO: hier dem paint3D noch Layer und Color mitgeben für LayerByBlock bzw. ColorByBlock
            //CategorizedDislayLists tmp = new CategorizedDislayLists();
            //idea.PaintTo3DList(paintTo3D, tmp);
            //paintTo3D.PopModOp();
            //tmp.AddToDislayLists(paintTo3D, UniqueId, lists);
            // jetzt auf flattened zeichnen, da sind alle ModOps schon drin, das ist hier einfacher
            // eine bessere aber aufwendigere Lösung wäre evtl. in ICategorizedDislayLists die ModOp zu pushen und popen
            Block blk = Flattened;
            blk.PaintTo3DList(paintTo3D, lists);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrePaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            idea.PrePaintTo3D(paintTo3D);
        }
        public delegate bool PaintTo3DDelegate(BlockRef toPaint, IPaintTo3D paintTo3D);
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            //paintTo3D.PushMultModOp(insertion);
            //idea.PaintTo3D(paintTo3D);
            //paintTo3D.PopModOp();
            // hier nicht Flatten aufrufen, denn beim Verschieben über ClipBoard
            // wird sonst immer Clone gemacht, und das geht in die Zeit und außerdem mag
            // Mauell das nicht bei DRPSymbolen
            Block blk = FlattenedP;
            blk.PaintTo3D(paintTo3D);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            Flattened.PrepareDisplayList(precision); // kann halt wieder verloren gehen, wird wohl bis zum Paint halten???
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.FindSnapPoint (SnapPointFinder)"/>
        /// </summary>
        /// <param name="spf"></param>
        public override void FindSnapPoint(SnapPointFinder spf)
        {   // besser wäre es den SnapPointFinder mit einer ModOp auszustatten, die überschrieben werden kann
            Flattened.FindSnapPoint(spf);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            Block blk = Flattened;
            QuadTreeCollection res = blk.GetQuadTreeItem(projection, extentPrecision) as QuadTreeCollection;
            res.SetOwner(this);
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            return Flattened.GetExtent(projection, extentPrecision);
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
            return Flattened.HitTest(ref cube, precision);
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
            return Flattened.HitTest(projection, rect, onlyInside);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            return Flattened.HitTest(area, onlyInside);
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
            return Flattened.Position(fromHere, direction, precision);
        }
#endregion
        public ModOp Insertion
        {
            get
            {
                return insertion;
            }
            set
            {
                using (new Changing(this, "Insertion", insertion))
                {
                    insertion = value;
                    if (flattened != null) flattened.Target = null;
                    extent = null;
                }
            }
        }
        public GeoPoint RefPoint
        {
            get { return insertion * idea.RefPoint; }
            set
            {
                GeoVector move = value - RefPoint;
                Insertion = ModOp.Translate(move) * insertion;
            }
        }
        public Block ReferencedBlock
        {
            get
            {
                return idea;
            }
            set
            {
                using (new Changing(this, "ReferencedBlock", idea))
                {
                    if (idea != null)
                    {
                        ((IGeoObject)idea).WillChangeEvent -= new ChangeDelegate(OnWillChange);
                        ((IGeoObject)idea).DidChangeEvent -= new ChangeDelegate(OnDidChange);
                    }
                    idea = value;
                    ((IGeoObject)idea).WillChangeEvent += new ChangeDelegate(OnWillChange);
                    ((IGeoObject)idea).DidChangeEvent += new ChangeDelegate(OnDidChange);
                    if (flattened != null) flattened.Target = null;
                    extent = null;
                }
            }
        }
        /// <summary>
        /// Returns a new Block with the Insertion applied and also the ByBlock colors and layers applied
        /// Usually the result is only used temporaryly.
        /// </summary>
        public Block Flattened
        {
            get
            {
                if (flattened == null) flattened = new WeakReference(null);
                try
                {
                    if (flattened.Target != null)
                    {
                        return flattened.Target as Block;
                    }
                }
                catch (InvalidOperationException) { }

                IGeoObject clone = idea.Clone();
                clone.Owner = this; // owner muss gesetzt werden, damit der richtige selectionindex gesetzt wird
                if (this.Layer != null || this.colorDef != null)
                {   // bei Drag and Drop sind diese null und PropagateAttributes kostet viel Zeit ohne was zu tun
                    clone.PropagateAttributes(this.Layer, this.colorDef); // ByParent und null für ColorDef und Layer werden hier ersetzt
                }
                clone.Modify(insertion);
                flattened.Target = clone;
                return clone as Block;
            }
        }
        private Block FlattenedP
        {
            get
            {
                if (flattenedP == null) flattenedP = new WeakReference(null);
                try
                {
                    if (flattenedP.Target != null)
                    {
                        return flattenedP.Target as Block;
                    }
                }
                catch (InvalidOperationException) { }
                // System.Diagnostics.Trace.WriteLine("berechne flattnedP. " + this.UniqueId.ToString());
                Block fp = Block.ConstructInternal(); // das ist ja nur ne Liste, vielleicht wärs ohnehin besser nur ne Liste zu machen
                AddPrimitiveToBlock(fp, this.Layer, this.colorDef, insertion, idea);
                flattenedP.Target = fp;
                return fp as Block;
                //if (flattenedP == null)
                //{
                //    flattenedP = Block.ConstructInternal(); // das ist ja nur ne Liste, vielleicht wärs ohnehin besser nur ne Liste zu machen
                //    AddPrimitiveToBlock(flattenedP, this.Layer, this.colorDef, insertion, idea);
                //}
                //return flattenedP;
            }
        }

        private void AddPrimitiveToBlock(Block addTo, Layer layer, ColorDef colorDef, ModOp mod, IGeoObject toAdd)
        {
            if (toAdd is BlockRef)
            {
                BlockRef br = (toAdd as BlockRef);
                AddPrimitiveToBlock(addTo, br.Layer, br.ColorDef, mod * br.insertion, br.idea);
            }
            else if (toAdd is Hatch && toAdd.NumChildren == 0)
            {   // sonst kann man in ERSACAD nicht per drag and drop verschieben
                IGeoObject clone = toAdd.Clone(); // das ist ja jetzt ein Primitives
                if (clone is ILayer)
                {
                    if (clone.Layer == null) clone.Layer = layer;
                }
                if (clone is IColorDef)
                {
                    if ((clone as IColorDef).ColorDef == null) (clone as IColorDef).ColorDef = colorDef;
                }
                clone.Modify(mod);
                addTo.Add(clone);
            }
            else if (toAdd is Block)
            {
                Block blk = (toAdd as Block);
                Layer l = blk.Layer;
                ColorDef c = blk.ColorDef;
                if (l == null) l = layer;
                if (c == null) c = colorDef;
                for (int i = 0; i < blk.Children.Count; i++)
                {
                    AddPrimitiveToBlock(addTo, l, c, mod, blk.Child(i));
                }
            }
            else
            {
                IGeoObject clone = toAdd.Clone(); // das ist ja jetzt ein Primitives
                if (clone is ILayer)
                {
                    if (clone.Layer == null) clone.Layer = layer;
                }
                if (clone is IColorDef)
                {
                    if ((clone as IColorDef).ColorDef == null) (clone as IColorDef).ColorDef = colorDef;
                }
                clone.Modify(mod);
                addTo.Add(clone);
            }
        }

        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.AttributeChanged (INamedAttribute)"/>
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public override bool AttributeChanged(INamedAttribute attribute)
        {
            if (idea.AttributeChanged(attribute))
            {
                using (new Changing(this, true, true, "AttributeChanged", attribute))
                {
                    if (flattened != null) flattened.Target = null;
                    return true;
                }
            }
            return false;
        }
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
        }
#endregion
#region IGeoObjectOwner Members
        public void Remove(IGeoObject toRemove)
        {
            toRemove.Owner = null;
        }
        public void Add(IGeoObject toAdd)
        {
            toAdd.Owner = this;
        }
#endregion
#region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected BlockRef(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            idea = InfoReader.Read(info, "Idea", typeof(Block)) as Block;
            insertion = (ModOp)InfoReader.Read(info, "Insertion", typeof(ModOp));
            colorDef = ColorDef.Read(info, context);
        }
        /// <summary>
        /// Implements <see cref="ISerializable.GetObjectData"/>
        /// </summary>
        /// <param name="info">The <see cref="System.Runtime.Serialization.SerializationInfo"/> to populate with data.</param>
        /// <param name="context">The destination (<see cref="System.Runtime.Serialization.StreamingContext"/>) for this serialization.</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Idea", idea);
            info.AddValue("Insertion", insertion);
            info.AddValue("ColorDef", colorDef);
        }

#endregion
    }
}
