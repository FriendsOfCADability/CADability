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
    /// by a face with which it coincides. The image is kept as the bytes of a PNG, JPEG, GIF or BMP file
    /// (<see cref="ImageData"/>), which is also what project files contain (issue #412).
    /// </summary>
    [Serializable()]
    public class Icon : IGeoObjectImpl, ISerializable
    {
        private byte[] imageData; // the bytes of the image file, null for no image
        private Bitmap gdiBitmap; // decoded from imageData with GDI+ for painting (Windows), created on demand
        private bool gdiFailed; // GDI+ cannot decode imageData
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
        /// <summary>
        /// Protected constructor: raises <see cref="Constructed"/>. Use <see cref="Construct"/> to create an icon and
        /// <see cref="Set"/> to define it.
        /// </summary>
        protected Icon()
        {
            if (Constructed != null) Constructed(this);
        }
        /// <summary>
        /// Sets the image and the position of the icon.
        /// </summary>
        /// <param name="imageData">The bytes of a PNG, JPEG, GIF or BMP file; null for no image. The array is kept, so it
        /// must not be changed afterwards.</param>
        /// <param name="location">Position where the icon will be displayed</param>
        /// <param name="offsetx">X-Position in the icon where location is applied</param>
        /// <param name="offsety">Y-Position in the icon where location is applied. (0,0) is lower left of the icon</param>
        /// <exception cref="ArgumentException">The data is not a PNG, JPEG, GIF or BMP file</exception>
        public void Set(byte[] imageData, GeoPoint location, int offsetx, int offsety)
        {
            EncodedImage.Validate(imageData, nameof(imageData));
            using (new Changing(this))
            {
                SetImage(imageData, null);
                this.location = location;
                this.offsetx = offsetx;
                this.offsety = offsety;
            }
        }
        /// <summary>
        /// Sets the Data of the UnscaledIcon. The bitmap is encoded into <see cref="ImageData"/>: PNG, JPEG, GIF and BMP
        /// images keep their format, any other image becomes PNG. Needs GDI+, i.e. Windows.
        /// </summary>
        /// <param name="bitmap">The icon to be displayed</param>
        /// <param name="location">Position where the icon will be displayed</param>
        /// <param name="offsetx">X-Position in the icon where location is applied</param>
        /// <param name="offsety">Y-Position in the icon where location is applied. (0,0) is lower left of the icon</param>
        [Obsolete("Use Set(byte[], GeoPoint, int, int) with the bytes of the image file. GDI+ is removed from the kernel in CADability 2.0 (issue #412).")]
        public void Set(Bitmap bitmap, GeoPoint location, int offsetx, int offsety)
        {
            using (new Changing(this))
            {
                SetImage(EncodedImage.FromGdiImage(bitmap), bitmap);
                this.location = location;
                this.offsetx = offsetx;
                this.offsety = offsety;
            }
        }
        /// <summary>
        /// The image of the icon as the bytes of a PNG, JPEG, GIF or BMP file, null if the icon has no image. The array must
        /// not be changed; use <see cref="Set(byte[], GeoPoint, int, int)"/> to replace it.
        /// </summary>
        public byte[] ImageData => imageData;
        /// <summary>
        /// The width of the image in pixels, read from the header of <see cref="ImageData"/>; 0 without an image.
        /// </summary>
        public int PixelWidth => EncodedImage.TryGetSize(imageData, out int width, out _) ? width : 0;
        /// <summary>
        /// The height of the image in pixels, read from the header of <see cref="ImageData"/>; 0 without an image.
        /// </summary>
        public int PixelHeight => EncodedImage.TryGetSize(imageData, out _, out int height) ? height : 0;
        private void SetImage(byte[] data, Bitmap decoded)
        {
            imageData = data;
            gdiBitmap = decoded;
            gdiFailed = false;
        }
        /// <summary>
        /// The GDI+ bitmap to paint, null where GDI+ is not available or cannot decode the image.
        /// </summary>
        private Bitmap BitmapForPainting()
        {
            if (gdiBitmap != null || imageData == null || gdiFailed) return gdiBitmap;
            try
            {
                gdiBitmap = EncodedImage.ToGdiBitmap(imageData);
            }
            catch (Exception e) when (e is PlatformNotSupportedException || e is TypeInitializationException || e is DllNotFoundException || e is ArgumentException || e is System.Runtime.InteropServices.ExternalException)
            {
                gdiFailed = true;
            }
            return gdiBitmap;
        }
        /// <summary>
        /// The point in world coordinates at which the icon is displayed: the pixel of the bitmap given by the offsets in
        /// <see cref="Set"/> is placed at this point. Setting it raises the change events and is undoable.
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
        #region IGeoObject override
        /// <inheritdoc/>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                location = m * location;
            }
        }
        /// <inheritdoc/>
        public override IGeoObject Clone()
        {
            Icon res = new Icon();
            res.location = location;
            res.SetImage(imageData, gdiBitmap); // the bytes are never changed in place, so they can be shared
            res.offsetx = offsetx;
            res.offsety = offsety;
            return res;
        }
        /// <inheritdoc/>
        public override void CopyGeometry(IGeoObject ToCopyFrom)
        {
            Icon from = ToCopyFrom as Icon;
            if (from == null) return;
            this.location = from.location;
            SetImage(from.imageData, from.gdiBitmap);
            this.offsetx = from.offsetx;
            this.offsety = from.offsety;
        }
        /// <inheritdoc/>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            BoundingRect res = new BoundingRect(projection.Project(location));
            return res;
        }
        /// <inheritdoc/>
        public override BoundingCube GetBoundingCube()
        {
            return new BoundingCube(location);
        }
        /// <inheritdoc/>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            Bitmap bitmap = BitmapForPainting();
            if (bitmap != null) paintTo3D.DisplayBitmap(location, bitmap); // without GDI+ the icon is not painted
        }
        /// <inheritdoc/>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            Bitmap bitmap = BitmapForPainting();
            if (bitmap != null) paintTo3D.PrepareBitmap(bitmap, offsetx, offsety);
        }
        /// <inheritdoc/>
        public override void PrepareDisplayList(double precision)
        {

        }
        /// <inheritdoc/>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {   // zählt zu den linienartigen, damit kommt es etwas nach vorne
            // in der Basisklasse schon richtig implementiert
            base.PaintTo3DList(paintTo3D, lists);
        }
        /// <inheritdoc/>
        public override IPropertyEntry GetShowProperties(IFrame Frame)
        {
            return new ShowPropertyUnscaledIcon(this, Frame);
        }
        /// <inheritdoc/>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            return null;
        }
        /// <inheritdoc/>
        public override BoundingCube GetExtent(double precision)
        {
            return new BoundingCube(location);
        }
        /// <inheritdoc/>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Contains(location);
        }
        /// <inheritdoc/>
        public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            return false;
        }
        /// <inheritdoc/>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            return BoundingCube.UnitBoundingCube.Contains(area.ToUnitBox * location);
        }
        /// <inheritdoc/>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            return Geometry.LinePar(fromHere, direction, location);
        }
        #endregion
        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Icon(SerializationInfo info, StreamingContext context)
        {
            imageData = Picture.ReadImage(info, null, "Icon");
            location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            offsetx = info.GetInt32("Offsetx");
            offsety = info.GetInt32("Offsety");
        }
        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // the image file as Base64, see Picture.GetObjectData
            info.AddValue("ImageData", imageData == null ? null : Convert.ToBase64String(imageData));
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
