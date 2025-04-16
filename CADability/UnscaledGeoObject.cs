using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CADability.GeoObject
{
    /// <summary>
    /// A <see cref="IGeoObject">GeoObject</see> which always appears in the same size in a view. It is not scaled when the view is zoomed.
    /// These objects can not be kept in displaylists and hence have a poor performance when used in high numbers.
    /// The objects rotate with the view when viewed from different directions. 
    /// The <see cref="Location"/> specifies the position of the origin (GeoPoint(0,0,0)) of the referenced GeoObject.
    /// Usually you would center the referenced object at the origin so this object will be centered at the <see cref="Location"/>.
    /// In contrast to the <see cref="Icon"/> object, which always shows its face to the viewer and is much faster, 
    /// because it can be kept in the display list.
    /// Since this object does not have a well-defined size in world coordinates, it is only pickable or selectable
    /// at its insertion point. The size of the object is assumed in device units (pixels)
    /// The Layer of this object will be taken into account for the objects visibility, not the layer of the 
    /// referenced object.
    /// <remarks>The use of this object is not recommended because it has a poor performance in both display and selection.
    /// Consider using <see cref="Icon"/> instead.</remarks>
    /// </summary>
    [Serializable()]
    public class UnscaledGeoObject : IGeoObjectImpl, ISerializable
    {
        private GeoPoint location; //corresponds to the origin of the object
        private IGeoObject geoObject;

        #region polymorph construction
        /// <summary>
        /// Delegate for the construction of a UnscaledGeoObject.
        /// </summary>
        /// <returns>A UnscaledGeoObject or UnscaledGeoObject derived class</returns>
        public delegate UnscaledGeoObject ConstructionDelegate();
        /// <summary>
        /// Provide a delegate here if you want you UnscaledGeoObject derived class to be 
        /// created each time CADability creates a UnscaledGeoObject.
        /// </summary>
        public static ConstructionDelegate Constructor;
        /// <summary>
        /// The only way to create a UnscaledGeoObject. There are no public constructors for the UnscaledGeoObject to assure
        /// that this is the only way to construct a UnscaledGeoObject.
        /// </summary>
        /// <returns></returns>
        public static UnscaledGeoObject Construct()
        {
            if (Constructor != null) return Constructor();
            return new UnscaledGeoObject();
        }
        public delegate void ConstructedDelegate(UnscaledGeoObject justConstructed);
        public static event ConstructedDelegate Constructed;
        private UnscaledGeoObject()
        {
            location = GeoPoint.Origin;
            geoObject = null;
            Constructed?.Invoke(this);
        }
        #endregion
        /// <summary>
        /// The world coordinates location of this object
        /// </summary>
        public GeoPoint Location
        {
            get
            {
                return location;
            }
            set
            {
                using (new Changing(this))
                {
                    location = value;
                }
            }
        }
        /// <summary>
        /// The GeoObject to be displayed. This can be any kind of GeoObject, Text and Bitmap are often used.
        /// The object must be in a coordinate space where the Origin will be transferred to the <see cref="Location"/>.
        /// </summary>
        public IGeoObject GeoObject
        {
            get
            {
                return geoObject;
            }
            set
            {
                using (new Changing(this))
                {
                    if (geoObject != null)
                    {
                        geoObject.WillChangeEvent -= OnGeoObjectWillChange;
                        geoObject.DidChangeEvent -= OnGeoObjectDidChange;
                    }
                    geoObject = value;
                    geoObject.WillChangeEvent += OnGeoObjectWillChange;
                    geoObject.DidChangeEvent += OnGeoObjectDidChange;
                }
            }
        }

        private void OnGeoObjectDidChange(IGeoObject sender, GeoObjectChange change)
        {
            FireWillChange(change);
        }

        private void OnGeoObjectWillChange(IGeoObject sender, GeoObjectChange change)
        {
            FireDidChange(change);
        }
        /// <summary>
        /// While the normal <see cref="HitTest(Projection.PickArea, bool)">HitTest</see> only checks the <see cref="Location"/> of the object
        /// this method checks the hit test of the object expanded according to the projection in this <paramref name="area"/>
        /// </summary>
        /// <param name="area">Pick area to check</param>
        /// <param name="onlyInside">true, if the whole object must reside inside the pick area, false if overlapping will suffice</param>
        /// <returns>true when a hit is recognized</returns>
        public bool RealHitTest(Projection.PickArea area, bool onlyInside)
        {
            ModOp m = ModOp.Translate(location.x, location.y, location.z) * ModOp.Scale(area.Projection.DeviceToWorldFactor);
            IGeoObject clone = geoObject.Clone();
            clone.Modify(m);
            return clone.HitTest(area, onlyInside);
        }
        #region IGeoObjectImpl overrides
        /// <summary>
        /// Overrides <see cref="IGeoObject.Modify"/>. Must be implemented by each GeoObject.
        /// No default implementation.
        /// </summary>
        /// <param name="m">the modification</param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this))
            {
                location = m * location;
            }
        }
        /// <summary>
        /// Overrides <see cref="IGeoObject.Clone"/>. Must be implemented by each GeoObject. No default implementation.
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            UnscaledGeoObject res = Construct();
            ++res.isChanging;
            res.CopyGeometry(this);
            res.CopyAttributes(this);
            --res.isChanging;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public override void CopyGeometry(IGeoObject toCopyFrom)
        {
            if (toCopyFrom is UnscaledGeoObject res)
            {
                using (new Changing(this))
                {
                    location = res.location;
                    geoObject = res.geoObject;
                }
            }
            else
            {
                throw new ArgumentException("Invalid object type. Expected UnscaledGeoObject.", nameof(toCopyFrom));
            }
        }

        /// <summary>
        /// Implements <see cref="IGeoObject.GetBoundingCube"/> abstract.
        /// Must be overridden.
        /// </summary>
        /// <returns>the bounding cube</returns>
        public override BoundingCube GetBoundingCube()
        {
            return new BoundingCube(location);
        }
        /// <summary>
        /// Should be overridden and return a <see cref="IPropertyEntry"/> derived object
        /// that handles the display and modification of the properties of the IGeoObject derived object.
        /// Default implementation return null.
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame frame)
        {
            return new ShowPropertyUnscaledGeoObject(this, frame);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            ModOp m = ModOp.Translate(location.x, location.y, location.z) * ModOp.Scale(paintTo3D.PixelToWorld);
            paintTo3D.PushMultModOp(m);
            geoObject.PaintTo3D(paintTo3D);
            paintTo3D.PopModOp();
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {
            geoObject.PrepareDisplayList(precision);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrePaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            geoObject.PrePaintTo3D(paintTo3D);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetQuadTreeItem (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            return null;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override BoundingCube GetExtent(double precision)
        {
            return new BoundingCube(location);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetExtent (Projection, ExtentPrecision)"/>
        /// </summary>
        /// <param name="projection"></param>
        /// <param name="extentPrecision"></param>
        /// <returns></returns>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            BoundingRect res = new BoundingRect(projection.Project(location));
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Contains(location);
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
            // regardless of onlyInside, since only a single point is being tested
            return projection.ProjectUnscaled(location) <= rect;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            return BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * location);
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
            return Geometry.LinePar(fromHere, direction, location);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {
            lists.Add(Layer, false, false, this);
            // base.PaintTo3DList(paintTo3D, lists);
        }
        #endregion
        #region ISerializable Members
        protected UnscaledGeoObject(SerializationInfo info, StreamingContext context)
        {
            geoObject = info.GetValue("GeoObject", typeof(IGeoObject)) as IGeoObject;
            location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
        }
        /// <summary>
        /// Implements ISerializable.GetObjectData. Saves <see cref="UserData"/>, <see cref="CADability.Attribute.Layer"/> and <see cref="CADability.Attribute.Style"/>.
        /// All other properties of the GeoObject must be saved by the derived class. don't forget
        /// to call the base implementation
        /// </summary>
        /// <param name="info">info</param>
        /// <param name="context">context</param>
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("GeoObject", geoObject);
            info.AddValue("Location", location);
        }
        #endregion
    }

    internal class ShowPropertyUnscaledGeoObject : PropertyEntryImpl, ICommandHandler, IGeoObjectShowProperty
    {
        private readonly IPropertyEntry[] attributeProperties; // Display for the attributes (layer, color, etc.)
        private IPropertyEntry[] subEntries;
        private readonly UnscaledGeoObject unscaledGeoObject;
        public ShowPropertyUnscaledGeoObject(UnscaledGeoObject unscaledGeoObject, IFrame frame) : base(frame)
        {
            this.unscaledGeoObject = unscaledGeoObject;
            attributeProperties = unscaledGeoObject.GetAttributeProperties(Frame);
            base.resourceIdInternal = "UnscaledGeoObject.Object";
        }
        #region IPropertyEntry overrides
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable;
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries != null) 
                    return subEntries;

                List<IPropertyEntry> prop = new List<IPropertyEntry>();
                GeoPointProperty location = new GeoPointProperty(Frame, "UnscaledGeoObject.Location");
                location.OnGetValue = OnGetRefPoint;
                location.OnSetValue = OnSetRefPoint;
                prop.Add(location);
                IPropertyEntry spgo = unscaledGeoObject.GeoObject.GetShowProperties(Frame);
                prop.Add(spgo);
                IPropertyEntry[] mainProps = prop.ToArray();
                subEntries = PropertyEntryImpl.Concat(mainProps, attributeProperties);
                return subEntries;
            }
        }
        private GeoPoint OnGetRefPoint()
        {
            return unscaledGeoObject.Location;
        }
        private void OnSetRefPoint(GeoPoint p)
        {
            unscaledGeoObject.Location = p;
        }
        #endregion
        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string menuId)
        {
            return false;
        }

        bool ICommandHandler.OnUpdateCommand(string menuId, CommandState commandState)
        {
            return false;
        }
        void ICommandHandler.OnSelected(MenuWithHandler selectedMenuItem, bool selected) { }

        #endregion

        #region IGeoObjectShowProperty Members

        event CreateContextMenueDelegate IGeoObjectShowProperty.CreateContextMenueEvent
        {
            add { }
            remove { }
        }

        IGeoObject IGeoObjectShowProperty.GetGeoObject()
        {
            return unscaledGeoObject;
        }

        string IGeoObjectShowProperty.GetContextMenuId()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }

}
