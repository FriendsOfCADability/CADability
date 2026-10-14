using CADability.Actions;
using CADability.Attribute;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    internal class ShowPropertyBlockRef : IShowPropertyImpl, ICommandHandler, IGeoObjectShowProperty
    {
        private readonly BlockRef blockRef;
        private GeoPointProperty positionProp;
        private IPropertyEntry[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)
        private readonly IFrame frame;
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
        /// <inheritdoc/>
        public override void Added(IPropertyPage page)
        {
            base.Added(page);
            if (positionProp == null)
                positionProp = new GeoPointProperty(frame, "BlockRef.Position");
            positionProp.OnGetValue = OnGetLocation;
            positionProp.OnSetValue = OnSetLocation;
            positionProp.ModifyWithMouse += OnPositionModifyWithMouse;
            blockRef.UserData.UserDataAddedEvent += OnUserDataAdded;
            blockRef.UserData.UserDataRemovedEvent += OnUserDataAdded;


        }
        /// <inheritdoc/>
        public override void Removed(IPropertyTreeView page)
        {
            positionProp.OnGetValue = null;
            positionProp.OnSetValue = null;
            positionProp.ModifyWithMouse -= OnPositionModifyWithMouse;
            blockRef.UserData.UserDataAddedEvent -= OnUserDataAdded;
            blockRef.UserData.UserDataRemovedEvent -= OnUserDataAdded;

            base.Removed(page);
        }

        private void OnUserDataAdded(string name, object value)
        {
            attributeProperties = blockRef.GetAttributeProperties(frame);
            propertyTreeView.Refresh(this);
        }
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (positionProp == null)
                    positionProp = new GeoPointProperty(frame, "BlockRef.Position");
                return PropertyEntryImpl.Concat(new IPropertyEntry[] { positionProp }, attributeProperties);
            }
        }

        public override ShowPropertyEntryType EntryType => ShowPropertyEntryType.GroupTitle;

        /// <inheritdoc/>
        public override ShowPropertyLabelFlags LabelType => ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.Selectable;

        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.BlockRef", false, this));
                CreateContextMenueEvent?.Invoke(this, items);
                blockRef.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }
        #endregion
        private GeoPoint OnGetLocation()
        {
            return blockRef.RefPoint;
        }
        private void OnSetLocation(GeoPoint p)
        {
            blockRef.RefPoint = p;
        }

        private void OnPositionModifyWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(positionProp, blockRef);
            frame.SetAction(gpa);
        }


        #region ICommandHandler Members

        /// <inheritdoc/>
        public virtual bool OnCommand(string menuId)
        {
            switch (menuId)
            {
                case "MenuId.Explode":
                    if (frame.ActiveAction is SelectObjectsAction soa)
                    {
                        using (frame.Project.Undo.UndoFrame)
                        {
                            IGeoObjectOwner addTo = blockRef.Owner ?? frame.ActiveView.Model;
                            addTo.Remove(blockRef);
                            GeoObjectList l = blockRef.Decompose();
                            addTo.Add(l[0]);
                            soa.SetSelectedObjects(l);
                        }
                    }
                    return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public virtual bool OnUpdateCommand(string menuId, CommandState commandState)
        {
            switch (menuId)
            {
                case "MenuId.Explode":
                    commandState.Enabled = true;
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
    /// A reference to a <see cref="Block"/> that is placed in the model with a modification (<see cref="Insertion"/>),
    /// comparable to a block insert in DXF. The referenced block is shared and not owned by the reference, so several
    /// references may show the same block at different positions. For display, hit testing, extent and
    /// <see cref="Decompose"/> the reference lazily creates a flattened copy of the block transformed by the insertion,
    /// which is discarded whenever the block or the insertion changes.
    /// </summary>
    [Serializable()]
    public class BlockRef : IGeoObjectImpl, IColorDef, IGeoObjectOwner
    {
        private Block idea;
        private ModOp insertion;
        private WeakReference flattened, flattenedP;
        private BoundingCube? extent;
        #region polymorph construction
        /// <summary>
        /// Signature of the factory method that <see cref="Construct"/> calls when one is registered in <see cref="Constructor"/>.
        /// </summary>
        /// <param name="referredBlock">The block the new reference refers to</param>
        /// <returns>A new <see cref="BlockRef"/> or an object of a class derived from BlockRef</returns>
        public delegate BlockRef ConstructionDelegate(Block referredBlock);
        /// <summary>
        /// Optional factory for block references: when set, <see cref="Construct"/> calls this delegate instead of creating
        /// a plain <see cref="BlockRef"/>, so that every reference CADability creates is an instance of your derived class.
        /// Null by default.
        /// </summary>
        public static ConstructionDelegate Constructor;
        /// <summary>
        /// Creates a reference to the given block with the identity as <see cref="Insertion"/>, i.e. the block appears where
        /// it is defined; move it with <see cref="Modify"/>, <see cref="Insertion"/> or <see cref="RefPoint"/>. This is the
        /// only public way to create a block reference. If a <see cref="Constructor"/> is registered, the reference is created
        /// by that delegate.
        /// </summary>
        /// <param name="referredBlock">The block to refer to; it is shared with other references, not copied</param>
        /// <returns>The new block reference</returns>
        public static BlockRef Construct(Block referredBlock)
        {
            if (Constructor != null) return Constructor(referredBlock);
            return new BlockRef(referredBlock);
        }
        #endregion
        /// <summary>
        /// Creates a reference to the given block with the identity as insertion. Protected so that references are created
        /// via <see cref="Construct"/>; derived classes call it from their own constructors.
        /// </summary>
        /// <param name="referredBlock">The block to refer to</param>
        protected BlockRef(Block referredBlock)
        {
            idea = referredBlock;
            insertion = ModOp.Translate(0.0, 0.0, 0.0);
        }

        private void OnWillChange(IGeoObject sender, GeoObjectChange change)
        {
            FireWillChange(change);
        }
        private void OnDidChange(IGeoObject sender, GeoObjectChange change)
        {
            if (flattened != null) flattened.Target = null;
            FireDidChange(change);
        }

        /// <inheritdoc/>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                insertion = m * insertion;

                if (flattened != null)
                    flattened.Target = null;

                extent = null;

                if (flattenedP?.Target is Block block)
                    block.Modify(m);
            }

        }
        /// <inheritdoc/>
        public override IGeoObject Clone()
        {
            BlockRef result = Construct(idea);
            result.Insertion = insertion;
            result.CopyAttributes(this);
            return result;
        }
        /// <inheritdoc/>
        public override BoundingCube GetBoundingCube()
        {
            if (!extent.HasValue)
            {
                extent = Flattened.GetBoundingCube();
            }
            return extent.Value;
        }

        /// <inheritdoc/>
        public override GeoObjectList Decompose()
        {
            return new GeoObjectList(Flattened);
        }

        /// <inheritdoc/>
        public override void CopyGeometry(IGeoObject toCopyFrom)
        {
            using (new Changing(this))
            {
                BlockRef copyFromBlockRef = (BlockRef)toCopyFrom;
                idea = copyFromBlockRef.idea;
                insertion = copyFromBlockRef.Insertion;
                if (flattened != null) flattened.Target = null;
                extent = null;
            }
        }
        /// <inheritdoc/>
        public override IPropertyEntry GetShowProperties(IFrame frame)
        {
            return new ShowPropertyBlockRef(this, frame);
        }
        /// <inheritdoc/>
        public override string Description
        {
            get
            {
                return StringTable.GetString("BlockRef.Description");
            }
        }
        /// <inheritdoc/>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {
            // paintTo3D.PushMultModOp(insertion);
            //// TODO: Pass layer and color to paint3D for LayerByBlock or ColorByBlock
            // CategorizedDislayLists tmp = new CategorizedDislayLists();
            // idea.PaintTo3DList(paintTo3D, tmp);
            // paintTo3D.PopModOp();
            // tmp.AddToDislayLists(paintTo3D, UniqueId, lists);
            // Now draw on flattened, as all ModOps are already applied, making this easier
            // A better but more complex solution might be to push and pop the ModOp in ICategorizedDislayLists
            Block blk = Flattened;
            blk.PaintTo3DList(paintTo3D, lists);
        }

        /// <inheritdoc/>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            idea.PrePaintTo3D(paintTo3D);
        }
        /// <summary>
        /// Signature of the handler for <see cref="OnPaintTo3D"/>.
        /// </summary>
        /// <param name="toPaint">The block reference that is about to be painted</param>
        /// <param name="paintTo3D">The paint interface to paint to</param>
        /// <returns>true if the handler has painted the reference itself and the default painting must be skipped, false to
        /// let the reference paint the referenced block as usual</returns>
        public delegate bool PaintTo3DDelegate(BlockRef toPaint, IPaintTo3D paintTo3D);
        /// <summary>
        /// Hook to replace the painting of block references: when set, <see cref="PaintTo3D"/> calls this delegate first and
        /// does nothing more if it returns true. Applies to all block references. Null by default.
        /// </summary>
        public static PaintTo3DDelegate OnPaintTo3D;
        /// <inheritdoc/>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            if (OnPaintTo3D != null && OnPaintTo3D(this, paintTo3D)) return;
            // paintTo3D.PushMultiModOp(insertion);
            // idea.PaintTo3D(paintTo3D);
            // paintTo3D.PopModOp();
            // Do not call Flatten here, because when moving via Clipboard,
            // it always creates a clone, which takes time and is not ideal.
            // Additionally, manual handling does not work well with DRPSymbols.
            Block blk = FlattenedP;
            blk.PaintTo3D(paintTo3D);
        }

        /// <inheritdoc/>
        public override void PrepareDisplayList(double precision)
        {
            Flattened.PrepareDisplayList(precision); // Might get lost again, but should last until painting???
        }

        /// <inheritdoc/>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            // It would be better to equip the SnapPointFinder with a ModOp that can be overridden
            Flattened.FindSnapPoint(spf);
        }

        /// <inheritdoc/>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            Block blk = Flattened;
            QuadTreeCollection res = blk.GetQuadTreeItem(projection, extentPrecision) as QuadTreeCollection;
            res.SetOwner(this);
            return res;
        }
        /// <inheritdoc/>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            return Flattened.GetExtent(projection, extentPrecision);
        }
        #region IOctTreeInsertable members
        /// <inheritdoc/>
        public override BoundingCube GetExtent(double precision)
        {
            return GetBoundingCube();
        }
        /// <inheritdoc/>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            return Flattened.HitTest(ref cube, precision);
        }
        /// <inheritdoc/>
        public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            return Flattened.HitTest(projection, rect, onlyInside);
        }
        /// <inheritdoc/>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            return Flattened.HitTest(area, onlyInside);
        }
        /// <inheritdoc/>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            return Flattened.Position(fromHere, direction, precision);
        }
        #endregion
        /// <summary>
        /// The modification (translation, rotation, scaling ...) that places the referenced block in the model: the reference
        /// displays the block as it is defined, transformed by this operation. It is the identity after <see cref="Construct"/>,
        /// and <see cref="Modify"/> prepends its operation. Setting it discards the cached flattened block, raises the change
        /// events and is undoable.
        /// </summary>
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
        /// <summary>
        /// The position of the reference point of the referenced block (<see cref="Block.RefPoint"/>) in the model, i.e. that
        /// point transformed by <see cref="Insertion"/>. Setting it translates the reference so that the reference point comes
        /// to lie at the given point; this changes <see cref="Insertion"/>, raises the change events and is undoable.
        /// </summary>
        public GeoPoint RefPoint
        {
            get { return insertion * idea.RefPoint; }
            set
            {
                GeoVector move = value - RefPoint;
                Insertion = ModOp.Translate(move) * insertion;
            }
        }
        /// <summary>
        /// The block definition this reference displays. The block is shared with other references and is not owned by the
        /// reference, so modifying it changes all references. Setting it subscribes the reference to the change events of the
        /// new block (changes of the block discard the cached flattened copy), raises the change events and is undoable.
        /// </summary>
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
                        ((IGeoObject)idea).WillChangeEvent -= OnWillChange;
                        ((IGeoObject)idea).DidChangeEvent -= OnDidChange;
                    }
                    idea = value;
                    ((IGeoObject)idea).WillChangeEvent += OnWillChange;
                    ((IGeoObject)idea).DidChangeEvent += OnDidChange;
                    if (flattened != null) flattened.Target = null;
                    extent = null;
                }
            }
        }
        /// <summary>
        /// Returns a new Block with the Insertion applied and also the ByBlock colors and layers applied
        /// Usually the result is only used temporarily.
        /// </summary>
        public Block Flattened
        {
            get
            {
                if (flattened == null)
                    flattened = new WeakReference(null);

                try
                {
                    if (flattened.Target is Block block)
                        return block;
                }
                catch (InvalidOperationException)
                {
                    // Handle or log the exception if needed
                }

                IGeoObject clone = idea.Clone();
                clone.Owner = this; // Owner must be set to ensure the correct selection index is assigned

                if (Layer != null || colorDef != null)
                {
                    // During Drag and Drop, these can be null, and PropagateAttributes is expensive without doing anything useful
                    clone.PropagateAttributes(Layer, colorDef); // Replaces ByParent and null for ColorDef and Layer
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
                if (flattenedP == null)
                    flattenedP = new WeakReference(null);

                try
                {
                    if (flattenedP.Target is Block block)
                        return block;
                }
                catch (InvalidOperationException)
                {
                    // Handle or log the exception if needed
                }

                Block fp = Block.ConstructInternal(); // This is just a list, maybe it would be better to use only a list
                AddPrimitiveToBlock(fp, Layer, colorDef, insertion, idea);
                flattenedP.Target = fp;
                return fp;
            }
        }

        private void AddPrimitiveToBlock(Block addTo, Layer layer, ColorDef color, ModOp mod, IGeoObject toAdd)
        {
            switch (toAdd)
            {
                case BlockRef br:
                    // Recursively process BlockRef with its transformation
                    AddPrimitiveToBlock(addTo, br.Layer, br.ColorDef, mod * br.insertion, br.idea);
                    break;
                case Hatch _ when toAdd.NumChildren == 0:
                    // Prevents drag and drop issues in ERSACAD
                    AddCloneToBlock(addTo, toAdd, layer, color, mod);
                    break;
                case Block blk:
                {
                    // Process each child of the block
                    Layer blkLayer = blk.Layer ?? layer;
                    ColorDef blkColor = blk.ColorDef ?? color;

                    foreach (var child in blk.Children)
                        AddPrimitiveToBlock(addTo, blkLayer, blkColor, mod, child);

                    break;
                }
                default:
                    AddCloneToBlock(addTo, toAdd, layer, color, mod);
                    break;
            }
        }

        private void AddCloneToBlock(Block addTo, IGeoObject toAdd, Layer layer, ColorDef color, ModOp mod)
        {
            // Clone the object and apply necessary attributes
            IGeoObject clone = toAdd.Clone();

            if (clone is ILayer layerObj && layerObj.Layer == null)
                layerObj.Layer = layer;

            if (clone is IColorDef colorObj && colorObj.ColorDef == null)
                colorObj.ColorDef = color;

            clone.Modify(mod);
            addTo.Add(clone);
        }


        /// <inheritdoc/>
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
        /// <summary>
        /// The color of the reference. It is given to those objects of the displayed block that have no color of their own or
        /// whose color is "from parent" (see <see cref="Flattened"/>); objects with a color of their own keep it. May be null.
        /// Setting it raises the change events and is undoable.
        /// </summary>
        public ColorDef ColorDef
        {
            get => colorDef;
            set
            {
                using (ChangingAttribute.Create(this, colorDef))
                {
                    colorDef = ColorDefForOwner(value);
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
        /// <summary>
        /// Implements <see cref="IGeoObjectOwner.Remove"/>. The reference owns only the temporary flattened copy of its block
        /// (see <see cref="Flattened"/>), so this merely clears the <see cref="IGeoObject.Owner"/> of the given object. The
        /// referenced block is not changed.
        /// </summary>
        /// <param name="toRemove">The object that is no longer owned by this reference</param>
        public void Remove(IGeoObject toRemove)
        {
            toRemove.Owner = null;
        }
        /// <summary>
        /// Implements <see cref="IGeoObjectOwner.Add"/>. Only sets the <see cref="IGeoObject.Owner"/> of the given object to
        /// this reference, the object is not added to the referenced block. This is how the flattened copy of the block
        /// (see <see cref="Flattened"/>) is owned.
        /// </summary>
        /// <param name="toAdd">The object to be owned by this reference</param>
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
        /// Writes the data of this object for the legacy binary serialization (<see cref="System.Runtime.Serialization.ISerializable"/>).
        /// Files are written in the JSON format (<see cref="IJsonSerialize"/>) now; the binary format is still read.
        /// </summary>
        /// <param name="info">receives the data</param>
        /// <param name="context">the destination of the serialization</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Idea", idea);
            info.AddValue("Insertion", insertion);
            info.AddValue("ColorDef", colorDef);
        }
        #endregion
    }
}
