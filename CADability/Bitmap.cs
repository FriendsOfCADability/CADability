using CADability.Actions;
using CADability.Attribute;
using CADability.Shapes;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;

namespace CADability.GeoObject
{
    /// <summary>
    /// A bitmap as a rectangular or rhombiod face in 3D space. This object is mainly used for illustration or 
    /// as a background in a model. It is a GeoObject and may be part of a model. It is defined by the image and the
    /// location in space. The image is kept as the bytes of a PNG, JPEG, GIF or BMP file (<see cref="ImageData"/>), which
    /// is also what project files contain; the kernel does not decode it (issue #412).
    /// </summary>
    [Serializable()]
    public class Picture : IGeoObjectImpl, ISerializable
    {
        private byte[] imageData; // the bytes of the image file, null for no image
        private Bitmap gdiBitmap; // decoded from imageData with GDI+ for painting (Windows), created on demand
        private bool gdiFailed; // GDI+ cannot decode imageData (no GDI+ on this platform, or a format GDI+ does not know)
        private GeoPoint location;
        private GeoVector directionWidth, directionHeight;
        #region polymorph construction
        /// <summary>
        /// Delegate for the construction of a Picture.
        /// </summary>
        /// <returns>A Picture or Picture derived class</returns>
        public delegate Picture ConstructionDelegate();
        /// <summary>
        /// Provide a delegate here if you want you Picture derived class to be 
        /// created each time CADability creates a Picture.
        /// </summary>
        public static ConstructionDelegate Constructor;
        /// <summary>
        /// The only way to create a Picture. There are no public constructors for the Picture to assure
        /// that this is the only way to construct a Picture.
        /// </summary>
        /// <returns></returns>
        public static Picture Construct()
        {
            if (Constructor != null) return Constructor();
            return new Picture();
        }
        /// <summary>
        /// Definition of the <see cref="Constructed"/> event
        /// </summary>
        /// <param name="justConstructed">The Picture that was just constructed</param>
        public delegate void ConstructedDelegate(Picture justConstructed);
        /// <summary>
        /// Event being raised when a Picture object has been created.
        /// </summary>
        public static event ConstructedDelegate Constructed;
        #endregion
        /// <summary>
        /// Protected constructor: raises <see cref="Constructed"/>. Use <see cref="Construct"/> to create a picture and
        /// <see cref="Set(byte[], GeoPoint, GeoVector, GeoVector)"/> to define it.
        /// </summary>
        protected Picture()
        {
            Constructed?.Invoke(this);
        }
        //~Picture()
        //{
        //    System.Diagnostics.Trace.WriteLine("Destruktor von Picture");
        //}
        /// <summary>
        /// Sets the image and the position of the picture.
        /// </summary>
        /// <param name="imageData">The bytes of a PNG, JPEG, GIF or BMP file, e.g. from <see cref="File.ReadAllBytes(string)"/>;
        /// null for no image. The array is kept, so it must not be changed afterwards.</param>
        /// <param name="location">Position where the lower left point of the image will be displayed</param>
        /// <param name="directionWidth">Vector specifying the baseline of the image</param>
        /// <param name="directionHeight">Vector specifying the left side of the image</param>
        /// <exception cref="ArgumentException">The data is not a PNG, JPEG, GIF or BMP file</exception>
        public void Set(byte[] imageData, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight)
        {
            EncodedImage.Validate(imageData, nameof(imageData));
            using (new Changing(this))
            {
                SetImage(imageData, null);
                this.location = location;
                this.directionWidth = directionWidth;
                this.directionHeight = directionHeight;
            }
        }
        /// <summary>
        /// Sets the Data of the Bitmap. The bitmap is encoded into <see cref="ImageData"/>: PNG, JPEG, GIF and BMP images keep
        /// their format, any other image becomes PNG. Needs GDI+, i.e. Windows.
        /// </summary>
        /// <param name="bitmap">The bitmap to be displayed</param>
        /// <param name="location">Position where the lower left point of the bitmap will be displayed</param>
        /// <param name="directionWidth">Vector specifying the baseline of the bitmap</param>
        /// <param name="directionHeight">Vector specifying the left side of the bitmap</param>
        [Obsolete("Use Set(byte[], GeoPoint, GeoVector, GeoVector) with the bytes of the image file. GDI+ is removed from the kernel in CADability 2.0 (issue #412).")]
        public void Set(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight)
        {
            using (new Changing(this))
            {
                SetImage(EncodedImage.FromGdiImage(bitmap), bitmap);
                this.location = location;
                this.directionWidth = directionWidth;
                this.directionHeight = directionHeight;
            }
        }
        /// <summary>
        /// The image of the picture as the bytes of a PNG, JPEG, GIF or BMP file, null if the picture has no image. This is
        /// what project files contain. The array must not be changed; set a new array instead. Setting it raises the change
        /// events and is undoable.
        /// </summary>
        /// <exception cref="ArgumentException">The data is not a PNG, JPEG, GIF or BMP file</exception>
        public byte[] ImageData
        {
            get
            {
                return imageData;
            }
            set
            {
                EncodedImage.Validate(value, nameof(value));
                using (new Changing(this))
                {
                    SetImage(value, null);
                }
            }
        }
        /// <summary>
        /// The width of the image in pixels, read from the header of <see cref="ImageData"/>; 0 without an image.
        /// </summary>
        public int PixelWidth => EncodedImage.TryGetSize(imageData, out int width, out _) ? width : 0;
        /// <summary>
        /// The height of the image in pixels, read from the header of <see cref="ImageData"/>; 0 without an image.
        /// </summary>
        public int PixelHeight => EncodedImage.TryGetSize(imageData, out _, out int height) ? height : 0;
        /// <summary>
        /// Width divided by height of the image in pixels, 1 without an image.
        /// </summary>
        private double AspectRatio => PixelWidth > 0 && PixelHeight > 0 ? PixelWidth / (double)PixelHeight : 1.0;
        /// <summary>
        /// The bitmap, the contents of the Picture, decoded from <see cref="ImageData"/> with GDI+. Setting it encodes the
        /// bitmap into <see cref="ImageData"/>. Needs GDI+, i.e. Windows.
        /// </summary>
        [Obsolete("Use ImageData, the bytes of the image file. GDI+ is removed from the kernel in CADability 2.0 (issue #412).")]
        public Bitmap Bitmap
        {
            get
            {
                if (imageData != null && gdiBitmap == null) gdiBitmap = EncodedImage.ToGdiBitmap(imageData);
                return gdiBitmap;
            }
            set
            {
                using (new Changing(this))
                {
                    SetImage(EncodedImage.FromGdiImage(value), value);
                }
            }
        }
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
            catch (Exception e) when (e is PlatformNotSupportedException || e is TypeInitializationException || e is DllNotFoundException || e is ArgumentException || e is ExternalException)
            {
                gdiFailed = true; // e.g. on Linux: the picture is painted as its outline
            }
            return gdiBitmap;
        }
        /// <summary>
        /// Position of the lower left point of the bitmap
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
        /// The location of the lower baseline of the bitmap
        /// </summary>
        public GeoVector DirectionWidth
        {
            get
            {
                return directionWidth;
            }
            set
            {
                using (new Changing(this))
                {
                    directionWidth = value;
                }
            }
        }
        /// <summary>
        /// The location of the left side of the bitmap
        /// </summary>
        public GeoVector DirectionHeight
        {
            get
            {
                return directionHeight;
            }
            set
            {
                using (new Changing(this))
                {
                    directionHeight = value;
                }
            }
        }
        /// <summary>
        /// Sets the height of the picture, i.e. the length of <see cref="DirectionHeight"/>, to <paramref name="l"/> (in
        /// world units), keeping its direction. With <paramref name="keepAspectRatio"/> the width is adjusted as well, so
        /// that the picture keeps the aspect ratio of the bitmap's pixels. A value of 0 is ignored. Raises the change events
        /// and is undoable.
        /// </summary>
        /// <param name="l">the new height</param>
        /// <param name="keepAspectRatio">true to also set the width according to the aspect ratio of the bitmap</param>
        public void SetHeight(double l, bool keepAspectRatio)
        {
            if (l == 0.0) return;
            using (new Changing(this))
            {
                double ar = AspectRatio;
                directionHeight = l * directionHeight.Normalized;
                if (keepAspectRatio)
                {
                    directionWidth = l * ar * directionWidth.Normalized;
                }
            }
        }
        /// <summary>
        /// Sets the width of the picture, i.e. the length of <see cref="DirectionWidth"/>, to <paramref name="l"/> (in
        /// world units), keeping its direction. With <paramref name="keepAspectRatio"/> the height is adjusted as well, so
        /// that the picture keeps the aspect ratio of the bitmap's pixels. A value of 0 is ignored. Raises the change events
        /// and is undoable.
        /// </summary>
        /// <param name="l">the new width</param>
        /// <param name="keepAspectRatio">true to also set the height according to the aspect ratio of the bitmap</param>
        public void SetWidth(double l, bool keepAspectRatio)
        {
            if (l == 0.0) return;
            using (new Changing(this))
            {
                double ar = AspectRatio;
                directionWidth = l * directionWidth.Normalized;
                if (keepAspectRatio)
                {
                    directionHeight = (l / ar) * directionHeight.Normalized;
                }
            }
        }
        /// <summary>
        /// The path where the image file is located. This value is not used by the Picture object and may have any content.
        /// Usually it is the path and filename, especially when the object is being generated interactively 
        /// by CADability's user interface. Setting the value does NOT load the specified image. This must be done
        /// separately by setting the <see cref="ImageData"/> property. When an old project file has a picture whose image
        /// cannot be read, the picture is loaded from this path, if the file exists.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Clips the bitmap according to the provided shape and plane. The BitmapBits are replaced by
        /// transparent pixels, the original bits are lost. Setting another clip area doesn't restore already clipped
        /// pixels. (Of course undo restores the original bitmap bits)
        /// </summary>
        /// <param name="plane">The plane as a reference system for the shape</param>
        /// <param name="shape">The shape for the clip operation</param>
        /// <remarks>Needs GDI+, i.e. Windows. The clipped image is stored as PNG.</remarks>
        [Obsolete("Clip needs GDI+, which is removed from the kernel in CADability 2.0 (issue #412). Clip the image file before setting ImageData instead.")]
        public void Clip(Plane plane, CompoundShape shape)
        {
            Bitmap bitmap = EncodedImage.ToGdiBitmap(imageData);
            if (bitmap == null) return;
            Plane pln = new Plane(location, directionWidth, directionHeight);
            CompoundShape prsh = shape.Project(plane, pln);
            ModOp2D m = ModOp2D.Scale(bitmap.Width / directionWidth.Length, -bitmap.Height / directionHeight.Length);
            prsh = prsh.GetModified(m);
            m = ModOp2D.Translate(0, bitmap.Height);
            prsh = prsh.GetModified(m);
            GraphicsPath gp = prsh.CreateGraphicsPath();
            using (new Changing(this))
            {
                Region rg = new Region(gp);
                Bitmap clone = bitmap.Clone() as Bitmap;
                Graphics graphics = Graphics.FromImage(bitmap);
                graphics.Clear(Color.FromArgb(0, 0, 0, 0));
                graphics.SetClip(rg, CombineMode.Replace);
                graphics.DrawImage(clone, new System.Drawing.Point(0, 0));
                graphics.Dispose();
                clone.Dispose();
                rg.Dispose();
                SetImage(EncodedImage.FromGdiImage(bitmap), bitmap); // a bitmap made in memory: PNG
            }
        }

        /// <summary>
        /// Replaces the bitmap without raising the change events and without an undo step, in contrast to setting
        /// <see cref="Bitmap"/>. For callers that record the undo themselves. Not used within CADability.
        /// </summary>
        /// <param name="bmp">the new bitmap</param>
        [Obsolete("Use SetImageDataNoUndo with the bytes of the image file. GDI+ is removed from the kernel in CADability 2.0 (issue #412).")]
        public void SetBitmapNoUndo(Bitmap bmp)
        {
            SetImage(EncodedImage.FromGdiImage(bmp), bmp);
        }
        /// <summary>
        /// Replaces the image without raising the change events and without an undo step, in contrast to setting
        /// <see cref="ImageData"/>. For callers that record the undo themselves.
        /// </summary>
        /// <param name="imageData">the bytes of a PNG, JPEG, GIF or BMP file, or null</param>
        /// <exception cref="ArgumentException">The data is not a PNG, JPEG, GIF or BMP file</exception>
        public void SetImageDataNoUndo(byte[] imageData)
        {
            EncodedImage.Validate(imageData, nameof(imageData));
            SetImage(imageData, null);
        }
        #region IGeoObject override
        /// <inheritdoc/>
        public override void Modify(ModOp m)
        {
            using (new Changing(this, "ModifyInverse", m))
            {
                location = m * location;
                directionHeight = m * directionHeight;
                directionWidth = m * directionWidth;
            }
        }
        /// <inheritdoc/>
        public override IGeoObject Clone()
        {
            Picture res = Picture.Construct();
            res.location = location;
            res.imageData = imageData; // never changed in place, so it can be shared
            res.directionWidth = directionWidth;
            res.directionHeight = directionHeight;
            return res;
        }
        /// <inheritdoc/>
        public override void CopyGeometry(IGeoObject toCopyFrom)
        {
            using (new Changing(this))
            {
                if (!(toCopyFrom is Picture from)) 
                    return;
                this.location = from.location;
                SetImage(from.imageData, from.gdiBitmap);
                this.directionWidth = from.directionWidth;
                this.directionHeight = from.directionHeight;
            }
        }
        /// <inheritdoc/>
        public override BoundingRect GetExtent(Projection projection, ExtentPrecision extentPrecision)
        {
            BoundingRect res = new BoundingRect(projection.ProjectUnscaled(location), projection.ProjectUnscaled(location + directionWidth), projection.ProjectUnscaled(location + directionHeight), projection.ProjectUnscaled(location + directionWidth + directionHeight));
            return res;
        }
        /// <inheritdoc/>
        public override BoundingCube GetBoundingCube()
        {
            return new BoundingCube(location, location + directionWidth, location + directionHeight, location + directionWidth + directionHeight);
        }
        /// <inheritdoc/>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            Bitmap bitmap = BitmapForPainting();
            if (bitmap != null) paintTo3D.RectangularBitmap(bitmap, location, directionWidth, directionHeight);
            else
            {   // no GDI+ (or no image): the outline shows where the picture is
                paintTo3D.Polyline(new GeoPoint[] { location, location + directionWidth, location + directionWidth + directionHeight, location + directionHeight, location });
            }
        }
        /// <inheritdoc/>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            Bitmap bitmap = BitmapForPainting();
            if (bitmap != null) paintTo3D.PrepareBitmap(bitmap);
        }
        /// <inheritdoc/>
        public override void PrepareDisplayList(double precision)
        {
        }
        /// <inheritdoc/>
        public override void PaintTo3DList(IPaintTo3D paintTo3D, ICategorizedDislayLists lists)
        {
            PrePaintTo3D(paintTo3D);
            lists.Add(this.Layer, true, false, this);
        }
        /// <inheritdoc/>
        public override IPropertyEntry GetShowProperties(IFrame frame)
        {
            return new ShowPropertyPicture(this, frame);
        }
        /// <inheritdoc/>
        public override IQuadTreeInsertableZ GetQuadTreeItem(Projection projection, ExtentPrecision extentPrecision)
        {
            return null;
        }
        /// <inheritdoc/>
        public override BoundingCube GetExtent(double precision)
        {
            return new BoundingCube(location, location + directionWidth, location + directionWidth + directionHeight, location + directionHeight);
        }
        /// <inheritdoc/>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Interferes(new GeoPoint[] { location, location + directionWidth, location + directionWidth + directionHeight, location + directionHeight }, new int[] { 0, 1, 2, 0, 2, 3 });
        }
        /// <inheritdoc/>
        public override bool HitTest(Projection projection, BoundingRect rect, bool onlyInside)
        {
            if (onlyInside)
            {
                return projection.ProjectUnscaled(location) <= rect && projection.ProjectUnscaled(location + directionWidth) <= rect &&
                 projection.ProjectUnscaled(location + directionHeight) <= rect && projection.ProjectUnscaled(location + directionWidth + directionHeight) <= rect;
            }
            else
            {
                ClipRect clr = new ClipRect(rect);
                return clr.ParallelogramHitTest(projection.ProjectUnscaled(location), projection.ProjectUnscaled(directionWidth), projection.ProjectUnscaled(directionHeight));
            }
        }
        /// <inheritdoc/>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            return BoundingCube.UnitBoundingCube.Interferes(new GeoPoint[] { area.ToUnitBox * location,
                area.ToUnitBox * (location + directionWidth),
                area.ToUnitBox * (location + directionWidth + directionHeight),
                area.ToUnitBox * (location + directionHeight) },
                new int[] { 0, 1, 2, 0, 2, 3 });
        }
        /// <inheritdoc/>
        public override double Position(GeoPoint fromHere, GeoVector direction, double precision)
        {
            try
            {
                Plane pln = new Plane(location, directionWidth, directionHeight);
                GeoPoint p = pln.Intersect(fromHere, direction);
                return Geometry.LinePar(fromHere, direction, p); // nicht getestet ob Bitmap auch getroffen
            }
            catch (ArithmeticException)
            {
                return double.MaxValue;
            }
        }
        /// <inheritdoc/>
        public override void FindSnapPoint(SnapPointFinder spf)
        {
            if (spf.SnapToFaceSurface)
            {
                Plane pln = new Plane(location, directionWidth, directionHeight);
                if (pln.Intersect(spf.SourceBeam.Location, spf.SourceBeam.Direction, out var ip))
                {
                    GeoPoint2D ip2d = Geometry.GetPosition(pln.Project(ip), GeoPoint2D.Origin, pln.Project(directionWidth), pln.Project(directionHeight));
                    double linepos = Geometry.LinePar(spf.SourceBeam.Location, spf.SourceBeam.Direction, ip);
                    if (linepos < spf.faceDist)
                    {
                        spf.faceDist = linepos;
                        spf.Check(ip, this, SnapPointFinder.DidSnapModes.DidSnapToFaceSurface);
                    }

                }
            }
            if (spf.SnapToObjectCenter)
            {
                GeoPoint center = location + 0.5 * directionWidth + 0.5 * directionHeight;
                spf.Check(center, this, SnapPointFinder.DidSnapModes.DidSnapToObjectCenter);
            }
            if (spf.SnapToObjectSnapPoint)
            {
                spf.Check(location, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
                spf.Check(location + directionWidth, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
                spf.Check(location + directionHeight, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
                spf.Check(location + directionWidth + directionHeight, this, SnapPointFinder.DidSnapModes.DidSnapToObjectSnapPoint);
            }
        }
        /// <inheritdoc/>
        public override Style.EDefaultFor PreferredStyle => Style.EDefaultFor.Text;
        #endregion

        #region ISerializable Members
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected Picture(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            directionWidth = (GeoVector)info.GetValue("DirectionWidth", typeof(GeoVector));
            directionHeight = (GeoVector)info.GetValue("DirectionHeight", typeof(GeoVector));
            Path = info.GetString("Path");
            imageData = ReadImage(info, Path, "Picture");
        }
        /// <summary>
        /// The image of a picture or icon from a project file: "ImageData" (the image file as Base64) since #412 step 7, or
        /// "Bitmap" (a serialized GDI+ bitmap) in files of CADability 1.x up to then. When the image cannot be read, the file
        /// at <paramref name="path"/> is loaded instead, if there is one.
        /// </summary>
        internal static byte[] ReadImage(SerializationInfo info, string path, string objectName)
        {
            byte[] data = null;
            bool found = false;
            foreach (SerializationEntry entry in info)
            {
                if (entry.Name == "ImageData")
                {
                    found = entry.Value != null; // null: saved without an image
                    if (entry.Value is string base64) data = Convert.FromBase64String(base64);
                    else data = entry.Value as byte[];
                }
                else if (entry.Name == "Bitmap")
                {
                    found = true;
                    try
                    {
                        data = EncodedImage.FromSerialized(entry.Value);
                    }
                    catch (Exception e) when (!(e is ThreadAbortException) && !(e is OutOfMemoryException))
                    {
                        data = null;
                    }
                }
            }
            if (data != null && EncodedImage.TryGetSize(data, out _, out _)) return data;
            if (!found) return null; // an object without an image
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    if (File.Exists(path))
                    {
                        System.Diagnostics.Trace.TraceWarning($"{objectName}: the image in the project file cannot be read, it is loaded from \"{path}\".");
                        return ReadImageFile(path);
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
                {
                }
            }
            if (data != null) System.Diagnostics.Trace.TraceWarning($"{objectName}: the image in the project file is not a PNG, JPEG, GIF or BMP file and cannot be shown.");
            else System.Diagnostics.Trace.TraceWarning($"{objectName}: the image in the project file cannot be read and there is no file \"{path}\".");
            return data;
        }
        /// <inheritdoc/>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            // Base64 rather than byte[]: JsonSerialize writes every element of an array as an object of its own
            info.AddValue("ImageData", imageData == null ? null : Convert.ToBase64String(imageData));
            info.AddValue("Location", location);
            info.AddValue("DirectionWidth", directionWidth);
            info.AddValue("DirectionHeight", directionHeight);
            info.AddValue("Path", Path);
        }
        #endregion
        /// <summary>
        /// The bytes of an image file for <see cref="ImageData"/>. A PNG, JPEG, GIF or BMP file is taken as it is. Any other
        /// image format GDI+ knows (e.g. TIFF) is converted to PNG where GDI+ is available. The file is closed when this
        /// method returns.
        /// </summary>
        /// <exception cref="ArgumentException">The file is not an image that can be used</exception>
        internal static byte[] ReadImageFile(string filePath)
        {
            byte[] data = File.ReadAllBytes(filePath);
            if (EncodedImage.TryGetSize(data, out _, out _)) return data;
            try
            {
                using (MemoryStream stream = new MemoryStream(data))
                using (Bitmap bitmap = new Bitmap(stream))
                {
                    return EncodedImage.FromGdiImage(bitmap);
                }
            }
            catch (Exception e) when (e is PlatformNotSupportedException || e is TypeInitializationException || e is DllNotFoundException || e is ArgumentException || e is ExternalException)
            {
                throw new ArgumentException("\"" + filePath + "\" is not a PNG, JPEG, GIF or BMP file.", nameof(filePath), e);
            }
        }

    }


    /// <summary>
    /// Property entry that shows and edits a <see cref="Picture"/> in the property grid: the location, the width and
    /// height (optionally keeping the aspect ratio of the bitmap), the two direction vectors (optionally kept
    /// perpendicular), the file path and the attributes. Location and directions have hotspots in the drawing area.
    /// It is returned by <see cref="Picture.GetShowProperties(IFrame)"/>.
    /// </summary>
    public class ShowPropertyPicture : PropertyEntryImpl, ICommandHandler, IGeoObjectShowProperty, IDisplayHotSpots
    {
        private readonly IPropertyEntry[] attributeProperties; // Display for the attributes (layer, color, etc.)
        private IPropertyEntry[] subEntries;
        private readonly Picture picture;
        private readonly GeoVectorProperty dirWidth;
        private readonly GeoVectorProperty dirHeight;
        private readonly GeoPointProperty location;
        private readonly LengthProperty width;
        private readonly LengthProperty height;
        private readonly StringProperty path;
        private readonly BooleanProperty keepAspectRatio;
        private readonly BooleanProperty rectangular;
        private readonly GeoVectorHotSpot dirWidthHotSpot; // Hotspot for direction
        private readonly GeoVectorHotSpot dirHeightHotSpot; // Hotspot for direction

        /// <summary>
        /// Creates the property entry for the picture <paramref name="picture"/>.
        /// </summary>
        /// <param name="picture">the picture to show</param>
        /// <param name="frame">the frame the property grid belongs to</param>
        public ShowPropertyPicture(Picture picture, IFrame frame) : base(frame)
        {
            this.picture = picture;
            attributeProperties = picture.GetAttributeProperties(frame);
            base.resourceIdInternal = "Picture.Object";

            location = new GeoPointProperty(frame, "Picture.Location");
            location.OnGetValue = OnGetRefPoint;
            location.OnSetValue = OnSetRefPoint;
            location.ModifyWithMouse = OnModifyLocationWithMouse;

            width = new LengthProperty(frame, "Picture.Width");
            width.OnGetValue = OnGetWidth;
            width.OnSetValue = OnSetWidth;

            height = new LengthProperty(frame, "Picture.Height");
            height.OnGetValue = OnGetHeight;
            height.OnSetValue = OnSetHeight;

            dirWidth = new GeoVectorProperty(frame, "Picture.DirWidth");
            dirWidth.OnGetValue = OnGetDirWidth;
            dirWidth.OnSetValue = OnSetDirWidth;
            dirWidth.ModifyWithMouse = OnModifyDirWidthWithMouse;

            dirHeight = new GeoVectorProperty(frame, "Picture.DirHeight");
            dirHeight.OnGetValue = OnGetDirHeight;
            dirHeight.OnSetValue = OnSetDirHeight;
            dirHeight.ModifyWithMouse = OnModifyDirHeightWithMouse;

            path = new StringProperty(picture.Path, "Picture.Path");
            path.OnGetValue = OnGetPath;
            path.OnSetValue = OnSetPath;

            path.SetContextMenu("MenuId.Picture.Path", this);
            keepAspectRatio = new BooleanProperty("Picture.KeepAspectRatio", "YesNo.Values");
            double p = picture.DirectionWidth.Length / Math.Max(1, picture.PixelWidth) * Math.Max(1, picture.PixelHeight) / picture.DirectionHeight.Length;
            keepAspectRatio.BooleanValue = Math.Abs(1.0 - p) < 1e-6;
            keepAspectRatio.BooleanChangedEvent += OnKeepAspectRatioChanged;
            rectangular = new BooleanProperty("Picture.Rectangular", "YesNo.Values");
            rectangular.BooleanValue = Precision.IsPerpendicular(picture.DirectionWidth, picture.DirectionHeight, false);
            rectangular.BooleanChangedEvent += OnRectangularChanged;
            dirWidthHotSpot = new GeoVectorHotSpot(dirWidth);
            dirWidthHotSpot.Position = picture.Location + picture.DirectionWidth;
            dirHeightHotSpot = new GeoVectorHotSpot(dirHeight);
            dirHeightHotSpot.Position = picture.Location + picture.DirectionHeight;
        }

        /// <summary>
        /// Width / height of the picture's image in pixels (<paramref name="widthByHeight"/>), or height / width; 1 without
        /// an image.
        /// </summary>
        internal static double PixelRatio(Picture picture, bool widthByHeight)
        {
            if (picture.PixelWidth <= 0 || picture.PixelHeight <= 0) return 1.0;
            return widthByHeight ? picture.PixelWidth / (double)picture.PixelHeight : picture.PixelHeight / (double)picture.PixelWidth;
        }
        private void OnKeepAspectRatioChanged(object sender, bool newValue)
        {
            if (keepAspectRatio.BooleanValue)
            {
                picture.DirectionHeight = picture.DirectionWidth.Length * PixelRatio(picture, false) * picture.DirectionHeight.Normalized;
            }
        }

        private void OnRectangularChanged(object sender, bool newValue)
        {
            GeoVector normal = picture.DirectionWidth ^ picture.DirectionHeight;
            if (rectangular.BooleanValue)
            {
                picture.DirectionHeight = picture.DirectionHeight.Length * (normal ^ picture.DirectionWidth).Normalized;
            }
        }

        private void OnModifyDirHeightWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoVectorAction gva = new GeneralGeoVectorAction(sender as GeoVectorProperty, picture.Location, picture);
            Frame.SetAction(gva);
        }

        private void OnModifyDirWidthWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoVectorAction gva = new GeneralGeoVectorAction(sender as GeoVectorProperty, picture.Location, picture);
            Frame.SetAction(gva);
        }

        private void OnModifyLocationWithMouse(IPropertyEntry sender, bool startModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(location, picture);
            Frame.SetAction(gpa);
        }

        #region IPropertyEntry overrides
        private void OnGeoObjectDidChange(IGeoObject sender, GeoObjectChange change)
        {	// wird bei Änderungen der Geometrie aufgerufen, Abgleich der Anzeigen
            location.Refresh();
            width.Refresh();
            height.Refresh();
            dirWidthHotSpot.Position = picture.Location + picture.DirectionWidth;
            dirHeightHotSpot.Position = picture.Location + picture.DirectionHeight;
            if (HotspotChangedEvent != null)
            {
                HotspotChangedEvent(location, HotspotChangeMode.Moved);
                HotspotChangedEvent(dirWidthHotSpot, HotspotChangeMode.Moved);
                HotspotChangedEvent(dirHeightHotSpot, HotspotChangeMode.Moved);
            }
        }
        /// <inheritdoc/>
        public override void Added(IPropertyPage propertyTreeView)
        {
            picture.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            base.Added(propertyTreeView);
            OnGeoObjectDidChange(picture, null); // reactivate HotSpots
        }
        /// <inheritdoc/>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            picture.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            base.Removed(propertyTreeView);
        }
        /// <inheritdoc/>
        public override void Opened(bool isOpen)
        {	
            // Used to show or hide the hotspots when the sub-properties
            // are expanded or collapsed. This happens, for example, when multiple objects are selected
            // and this line is expanded.
            if (HotspotChangedEvent != null)
            {
                if (isOpen)
                {
                    HotspotChangedEvent(location, HotspotChangeMode.Visible);
                    HotspotChangedEvent(dirWidthHotSpot, HotspotChangeMode.Visible);
                    HotspotChangedEvent(dirHeightHotSpot, HotspotChangeMode.Visible);
                }
                else
                {
                    HotspotChangedEvent(location, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(dirWidthHotSpot, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(dirHeightHotSpot, HotspotChangeMode.Invisible);
                }
            }
            base.Opened(isOpen);
        }
        /// <inheritdoc/>
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable;

        /// <inheritdoc/>
        public override IPropertyEntry[] SubItems
        {
            get
            {
                if (subEntries != null)
                    return subEntries;

                List<IPropertyEntry> prop = new List<IPropertyEntry>();
                prop.Add(path);
                prop.Add(location);
                prop.Add(width);
                prop.Add(height);
                prop.Add(dirWidth);
                prop.Add(dirHeight);
                prop.Add(keepAspectRatio);
                prop.Add(rectangular);
                IPropertyEntry[] mainProps = prop.ToArray();
                subEntries = PropertyEntryImpl.Concat(mainProps, attributeProperties);
                return subEntries;
            }
        }

        private void OnSetPath(string newValue)
        {
            picture.Path = newValue;
        }

        private string OnGetPath()
        {
            return picture.Path;
        }

        private void OnSetDirHeight(GeoVector v)
        {
            double l = picture.DirectionHeight.Length;
            GeoVector normal = picture.DirectionWidth ^ picture.DirectionHeight;
            using (base.Frame.Project.Undo.UndoFrame)
            {
                picture.DirectionHeight = l * v.Normalized;
                if (rectangular.BooleanValue)
                {
                    picture.DirectionWidth = picture.DirectionWidth.Length * (v ^ normal).Normalized;
                }
                if (keepAspectRatio.BooleanValue)
                {
                    picture.DirectionWidth = picture.DirectionHeight.Length * PixelRatio(picture, true) * picture.DirectionWidth.Normalized;
                }
            }
        }

        private GeoVector OnGetDirHeight()
        {
            return picture.DirectionHeight;
        }

        private void OnSetDirWidth(GeoVector v)
        {
            double l = picture.DirectionWidth.Length;
            GeoVector normal = picture.DirectionWidth ^ picture.DirectionHeight;
            using (base.Frame.Project.Undo.UndoFrame)
            {
                picture.DirectionWidth = l * v.Normalized;
                if (rectangular.BooleanValue)
                {
                    picture.DirectionHeight = picture.DirectionHeight.Length * (normal ^ v).Normalized;
                }
                if (keepAspectRatio.BooleanValue)
                {
                    picture.DirectionHeight = picture.DirectionWidth.Length * PixelRatio(picture, false) * picture.DirectionHeight.Normalized;
                }
            }
        }

        private GeoVector OnGetDirWidth()
        {
            return picture.DirectionWidth;
        }

        private void OnSetHeight(double l)
        {
            if (l != 0.0)
            {
                picture.SetHeight(l, keepAspectRatio.BooleanValue);
            }
        }

        private double OnGetHeight()
        {
            return picture.DirectionHeight.Length;
        }

        private void OnSetWidth(double l)
        {
            if (l != 0.0)
            {
                picture.SetWidth(l, keepAspectRatio.BooleanValue);
            }
        }

        private double OnGetWidth()
        {
            return picture.DirectionWidth.Length;
        }
        private GeoPoint OnGetRefPoint()
        {
            return picture.Location;
        }
        private void OnSetRefPoint(GeoPoint p)
        {
            picture.Location = p;
        }
        #endregion
        #region ICommandHandler Members
        bool ICommandHandler.OnCommand(string menuId)
        {
            switch (menuId)
            {
                case "MenuId.Picture.Path.Reload":
                    try
                    {
                        picture.ImageData = Picture.ReadImageFile(picture.Path);
                    }
                    catch (Exception e)
                    {
                        if (e is ThreadAbortException) 
                            throw;
                    }
                    return true;
                case "MenuId.Picture.Path.Open":
                    int filterIndex = 0;
                    if (Frame.UIService.ShowOpenFileDlg("Bitmap", StringTable.GetString("MenuId.Picture.Path.Open"), StringTable.GetString("Picture.Open.Filter"), ref filterIndex, out string fileName) == Substitutes.DialogResult.OK)
                    {
                        if (!string.IsNullOrWhiteSpace(fileName))
                        {
                            try
                            {
                                picture.ImageData = Picture.ReadImageFile(fileName);
                                picture.Path = fileName;
                            }
                            catch (Exception e)
                            {
                                if (e is ThreadAbortException) 
                                    throw;
                            }
                        }
                    }
                    return true;
            }
            return false;
        }
        bool ICommandHandler.OnUpdateCommand(string menuId, CommandState commandState)
        {
            switch (menuId)
            {
                case "MenuId.Picture.Path.Reload":
                    commandState.Enabled = true;
                    return true;
                case "MenuId.Picture.Path.Open":
                    commandState.Enabled = true;
                    return true;
            }
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
            return picture;
        }

        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return null;
        }

        #endregion
        #region IDisplayHotSpots Members
        /// <summary>
        /// Raised when the hotspots of this entry (location and directions of the picture) are to be shown, hidden,
        /// selected or deselected: when the entry is opened or closed and when one of its sub-entries is selected. The
        /// handler gets the hotspot and the kind of change, see <see cref="IDisplayHotSpots"/>.
        /// </summary>
        public event CADability.HotspotChangedDelegate HotspotChangedEvent;
        /// <inheritdoc/>
        public void ReloadProperties()
        {
            base.propertyPage.Refresh(this);
        }

        #endregion

    }
}
