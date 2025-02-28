using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.GeoObject
{
    /// <summary>
    /// A GeoObject that displays an icon at a given position. The icon will always face the viewer
    /// and will not be scaled. The icon appears with a small offset in direction to the viewer, so it will not be covered
    /// by a face with which it coincides.
    /// </summary>
    [Serializable()]
    public class Icon : IGeoObjectImpl, ISerializable
    {
        private Bitmap bitmap;
        private GeoPoint location;
        private int offsetx, offsety;
        #region polymorph construction
        /// <summary>
        /// Delegate for the construction of a Icon.
        /// </summary>
        /// <returns>A Icon or Icon derived class</returns>
        public delegate Icon ConstructionDelegate();
        /// <summary>
        /// Provide a delegate here if you want your Icon derived class to be 
        /// created each time CADability creates an Icon.
        /// </summary>
        public static ConstructionDelegate Constructor;
        /// <summary>
        /// The only way to create an Icon. There are no public constructors for the Icon to assure
        /// that this is the only way to construct an Icon.
        /// </summary>
        /// <returns></returns>
        public static Icon Construct()
        {
            if (Constructor != null) return Constructor();
            return new Icon();
        }
        /// <summary>
        /// Definition of the <see cref="Constructed"/> event
        /// </summary>
        /// <param name="justConstructed">The Icon that was just constructed</param>
        public delegate void ConstructedDelegate(Icon justConstructed);
        /// <summary>
        /// Event being raised when an Icon object has been created.
        /// </summary>
        public static event ConstructedDelegate Constructed;
        #endregion
        protected Icon()
        {
            if (Constructed != null) Constructed(this);
        }
        /// <summary>
        /// Sets the Data of the UnscaledIcon.
        /// </summary>
        /// <param name="bitmap">The icon to be displayed</param>
        /// <param name="location">Position where the icon will be displayed</param>
        /// <param name="offsetx">X-Position in the icon where location is applied</param>
        /// <param name="offsety">Y-Position in the icon where location is applied. (0,0) is lower left of the icon</param>
        public void Set(Bitmap bitmap, GeoPoint location, int offsetx, int offsety)
        {
            using (new Changing(this))
            {
                this.bitmap = bitmap;
                this.location = location;
                this.offsetx = offsetx;
                this.offsety = offsety;
            }
        }
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
        #region IGeoObject override
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Modify (ModOp)"/>
        /// </summary>
        /// <param name="m"></param>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                location = m * location;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            Icon res = new Icon();
            res.location = location;
            res.bitmap = bitmap;
            res.offsetx = offsetx;
            res.offsety = offsety;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="ToCopyFrom"></param>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            Icon from = ToCopyFrom as Icon;
            if (from == null) return;
            this.location = from.location;
            this.bitmap = from.bitmap;
            this.offsetx = from.offsetx;
            this.offsety = from.offsety;
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
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            return new BoundingCube(location);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            paintTo3D.DisplayBitmap(location, bitmap);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrePaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            paintTo3D.PrepareBitmap(bitmap, offsetx, offsety);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrepareDisplayList (double)"/>
        /// </summary>
        /// <param name="precision"></param>
        public override void PrepareDisplayList(double precision)
        {

        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3DList (IPaintTo3D, ICategorizedDislayLists)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        /// <param name="lists"></param>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {   // zählt zu den linienartigen, damit kommt es etwas nach vorne
            // in der Basisklasse schon richtig implementiert
            base.PaintTo3DList(paintTo3D, lists);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="Frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyUnscaledIcon(this, Frame);
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
            return false;
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
        #endregion
        #region ISerializable Members
        protected Icon(SerializationInfo info, StreamingContext context)
        {
            bitmap = info.GetValue("Bitmap", typeof(Bitmap)) as Bitmap;
            location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            offsetx = info.GetInt32("Offsetx");
            offsety = info.GetInt32("Offsety");
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Bitmap", bitmap);
            info.AddValue("Location", location);
            info.AddValue("Offsetx", offsetx);
            info.AddValue("Offsety", offsety);
        }

        #endregion
    }

    internal class ShowPropertyUnscaledIcon : PropertyEntryImpl, ICommandHandler, IGeoObjectShowProperty
    {
        private readonly IPropertyEntry[] attributeProperties; //Show properties (Layer, color, etc.)
        private IPropertyEntry[] subEntries;
        private readonly Icon icon;
        public ShowPropertyUnscaledIcon(Icon icon, IFrame frame) : base(frame)
        {
            this.icon = icon;
            attributeProperties = icon.GetAttributeProperties(Frame);
            base.resourceIdInternal = "Icon.Object";
        }
        #region IPropertyEntry overrides
        public override PropertyEntryType Flags => PropertyEntryType.ContextMenu | PropertyEntryType.Selectable | PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries;
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries == null)
                {
                    List<IPropertyEntry> prop = new List<IPropertyEntry>();
                    GeoPointProperty location = new GeoPointProperty(Frame, "Icon.Location");
                    location.OnGetValue = () => icon.Location;
                    location.OnSetValue = (p) => icon.Location = p;

                    prop.Add(location);
                    IPropertyEntry[] mainProps = prop.ToArray();
                    subEntries = PropertyEntryImpl.Concat(mainProps, attributeProperties);
                }
                return subEntries;
            }
        }
        #endregion
        #region ICommandHandler Members

        bool ICommandHandler.OnCommand(string MenuId)
        {
            return false;
        }

        bool ICommandHandler.OnUpdateCommand(string MenuId, CommandState CommandState)
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
            return icon;
        }

        string IGeoObjectShowProperty.GetContextMenuId()
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
