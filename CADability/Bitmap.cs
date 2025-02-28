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
using System.Runtime.Serialization;
using System.Threading;

namespace CADability.GeoObject
{
    /// <summary>
    /// A bitmap as a rectangular or rhombiod face in 3D space. This object is mainly used for illustration or 
    /// as a background in a model. It is a GeoObject and may be part of a model. It is defined by the bitmap 
    /// bits and the location in space.
    /// </summary>
    [Serializable()]
    public class Picture : IGeoObjectImpl, ISerializable
    {
        private Bitmap bitmap;
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
        protected Picture()
        {
            Constructed?.Invoke(this);
        }
        //~Picture()
        //{
        //    System.Diagnostics.Trace.WriteLine("Destruktor von Picture");
        //}
        /// <summary>
        /// Sets the Data of the Bitmap.
        /// </summary>
        /// <param name="bitmap">The bitmap to be displayed</param>
        /// <param name="location">Position where the lower left point of the bitmap will be displayed</param>
        /// <param name="directionWidth">Vector specifying the baseline of the bitmap</param>
        /// <param name="directionHeight">Vector specifying the left side of the bitmap</param>
        public void Set(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight)
        {
            using (new Changing(this))
            {
                this.bitmap = bitmap;
                this.location = location;
                this.directionWidth = directionWidth;
                this.directionHeight = directionHeight;
            }
        }
        /// <summary>
        /// The bitmap, the contents of the Picture
        /// </summary>
        public Bitmap Bitmap
        {
            get
            {
                return bitmap;
            }
            set
            {
                using (new Changing(this))
                {
                    bitmap = value;
                }
            }
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
        public void SetHeight(double l, bool keepAspectRatio)
        {
            if (l == 0.0) return;
            using (new Changing(this))
            {
                double ar = Bitmap.Width / (double)Bitmap.Height;
                directionHeight = l * directionHeight.Normalized;
                if (keepAspectRatio)
                {
                    directionWidth = l * ar * directionWidth.Normalized;
                }
            }
        }
        public void SetWidth(double l, bool keepAspectRatio)
        {
            if (l == 0.0) return;
            using (new Changing(this))
            {
                double ar = Bitmap.Width / (double)Bitmap.Height;
                directionWidth = l * directionWidth.Normalized;
                if (keepAspectRatio)
                {
                    directionHeight = (l / ar) * directionHeight.Normalized;
                }
            }
        }
        /// <summary>
        /// The path where the Bitmap is located. This value is not used by the Picture object and may have any content.
        /// Usually it is the path and filename, especially when the object is being generated interactively 
        /// by CADability's user interface. Setting the value does NOT load the specified bitmap. This must be done
        /// separately by setting the <see cref="Bitmap"/> property
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Clips the bitmap according to the provided shape and plane. The BitmapBits are replaced by
        /// transparent pixels, the original bits are lost. Setting another clip area doesn't restore already clipped
        /// pixels. (Of course undo restores the original bitmap bits)
        /// </summary>
        /// <param name="plane">The plane as a reference system for the shape</param>
        /// <param name="shape">The shape for the clip operation</param>
        public void Clip(Plane plane, CompoundShape shape)
        {
#if !WEBASSEMBLY
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
            }
#endif
        }

        public void SetBitmapNoUndo(Bitmap bmp)
        {
            bitmap = bmp;
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
                directionHeight = m * directionHeight;
                directionWidth = m * directionWidth;
            }
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.Clone ()"/>
        /// </summary>
        /// <returns></returns>
        public override IGeoObject Clone()
        {
            Picture res = Picture.Construct();
            res.location = location;
            if (bitmap != null) res.bitmap = bitmap.Clone() as Bitmap;
            else res.bitmap = null;
            res.directionWidth = directionWidth;
            res.directionHeight = directionHeight;
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.CopyGeometry (IGeoObject)"/>
        /// </summary>
        /// <param name="toCopyFrom"></param>
        public override void CopyGeometry(IGeoObject toCopyFrom)
        {
            using (new Changing(this))
            {
                if (!(toCopyFrom is Picture from)) 
                    return;
                this.location = from.location;
                this.bitmap = from.bitmap;
                this.directionWidth = from.directionWidth;
                this.directionHeight = from.directionHeight;
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
            BoundingRect res = new BoundingRect(projection.ProjectUnscaled(location), projection.ProjectUnscaled(location + directionWidth), projection.ProjectUnscaled(location + directionHeight), projection.ProjectUnscaled(location + directionWidth + directionHeight));
            return res;
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetBoundingCube ()"/>
        /// </summary>
        /// <returns></returns>
        public override BoundingCube GetBoundingCube()
        {
            return new BoundingCube(location, location + directionWidth, location + directionHeight, location + directionWidth + directionHeight);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PaintTo3D(IPaintTo3D paintTo3D)
        {
            paintTo3D.RectangularBitmap(bitmap, location, directionWidth, directionHeight);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.PrePaintTo3D (IPaintTo3D)"/>
        /// </summary>
        /// <param name="paintTo3D"></param>
        public override void PrePaintTo3D(IPaintTo3D paintTo3D)
        {
            paintTo3D.PrepareBitmap(bitmap);
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
        {
            PrePaintTo3D(paintTo3D);
            lists.Add(this.Layer, true, false, this);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.GetShowProperties (IFrame)"/>
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public override IPropertyEntry GetShowProperties(IFrame frame)
        {
            return new ShowPropertyPicture(this, frame);
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
            return new BoundingCube(location, location + directionWidth, location + directionWidth + directionHeight, location + directionHeight);
        }
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (ref BoundingCube, double)"/>
        /// </summary>
        /// <param name="cube"></param>
        /// <param name="precision"></param>
        /// <returns></returns>
        public override bool HitTest(ref BoundingCube cube, double precision)
        {
            return cube.Interferes(new GeoPoint[] { location, location + directionWidth, location + directionWidth + directionHeight, location + directionHeight }, new int[] { 0, 1, 2, 0, 2, 3 });
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
        /// <summary>
        /// Overrides <see cref="CADability.GeoObject.IGeoObjectImpl.HitTest (Projection.PickArea, bool)"/>
        /// </summary>
        /// <param name="area"></param>
        /// <param name="onlyInside"></param>
        /// <returns></returns>
        public override bool HitTest(Projection.PickArea area, bool onlyInside)
        {
            return BoundingCube.UnitBoundingCube.Interferes(new GeoPoint[] { area.ToUnitBox * location,
                area.ToUnitBox * (location + directionWidth),
                area.ToUnitBox * (location + directionWidth + directionHeight),
                area.ToUnitBox * (location + directionHeight) },
                new int[] { 0, 1, 2, 0, 2, 3 });
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
        public override Style.EDefaultFor PreferredStyle => Style.EDefaultFor.Text;
        #endregion

        #region ISerializable Members
        protected Picture(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            bitmap = info.GetValue("Bitmap", typeof(Bitmap)) as Bitmap;
            location = (GeoPoint)info.GetValue("Location", typeof(GeoPoint));
            directionWidth = (GeoVector)info.GetValue("DirectionWidth", typeof(GeoVector));
            directionHeight = (GeoVector)info.GetValue("DirectionHeight", typeof(GeoVector));
            Path = info.GetString("Path");
        }
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Bitmap", bitmap);
            info.AddValue("Location", location);
            info.AddValue("DirectionWidth", directionWidth);
            info.AddValue("DirectionHeight", directionHeight);
            info.AddValue("Path", Path);
        }
        #endregion
        internal static Bitmap CopyFrom(string filePath)
        {
            //open file from the disk (file path is the path to the file to be opened)
            using (FileStream fileStream = File.OpenRead(filePath))
            {
                //create new MemoryStream object
                MemoryStream memStream = new MemoryStream();
                memStream.SetLength(fileStream.Length);
                //read file to MemoryStream
                fileStream.Read(memStream.GetBuffer(), 0, (int)fileStream.Length);
                return new Bitmap(memStream);
            }
        }

    }


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
            double p = picture.DirectionWidth.Length / picture.Bitmap.Width * picture.Bitmap.Height / picture.DirectionHeight.Length;
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

        private void OnKeepAspectRatioChanged(object sender, bool newValue)
        {
            if (keepAspectRatio.BooleanValue)
            {
                picture.DirectionHeight = picture.DirectionWidth.Length * picture.Bitmap.Height / picture.Bitmap.Width * picture.DirectionHeight.Normalized;
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
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Added (IPropertyPage)"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Added(IPropertyPage propertyTreeView)
        {
            picture.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            base.Added(propertyTreeView);
            OnGeoObjectDidChange(picture, null); // reactivate HotSpots
        }
        /// <summary>
        /// Overrides <see cref="PropertyEntryImpl.Removed (IPropertyPage)"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            picture.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            base.Removed(propertyTreeView);
        }
        /// <summary>
        /// Overrides <see cref="CADability.UserInterface.IShowPropertyImpl.Opened (bool)"/>
        /// </summary>
        /// <param name="isOpen"></param>
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
        public override PropertyEntryType Flags => PropertyEntryType.GroupTitle | PropertyEntryType.HasSubEntries | PropertyEntryType.ContextMenu | PropertyEntryType.Selectable;

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
                    picture.DirectionWidth = picture.DirectionHeight.Length / picture.Bitmap.Height * picture.Bitmap.Width * picture.DirectionWidth.Normalized;
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
                    picture.DirectionHeight = picture.DirectionWidth.Length * picture.Bitmap.Height / picture.Bitmap.Width * picture.DirectionHeight.Normalized;
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
                        Bitmap pix = new Bitmap(picture.Path);
                        picture.Bitmap = pix;
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
                                Bitmap pix = new Bitmap(fileName);
                                picture.Bitmap = pix;
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
        public event CADability.HotspotChangedDelegate HotspotChangedEvent;
        /// <summary>
        /// Implements <see cref="CADability.IDisplayHotSpots.ReloadProperties ()"/>
        /// </summary>
        public void ReloadProperties()
        {
            base.propertyPage.Refresh(this);
        }

        #endregion

    }
}
