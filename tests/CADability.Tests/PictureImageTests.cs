using CADability.Attribute;
using CADability.GeoObject;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using Icon = CADability.GeoObject.Icon;
using Path = System.IO.Path;

namespace CADability.Tests
{
    /// <summary>
    /// <see cref="Picture"/> and <see cref="Icon"/> keep their image as the bytes of a PNG, JPEG, GIF or BMP file and write
    /// those into project files, so that projects with pictures open on every platform (issue #412, step 7). The kernel
    /// reads only the size from the header; GDI+ decodes the image for painting where it is available.
    /// pictures-1x.cdb and pictures-1x.cdb.json were written by CADability 1.x before this change (on Windows and .NET 8,
    /// with GDI+ bitmaps): a 7×5 picture, a 9×4 picture loaded from a JPEG and a 4×3 icon, whose pixel (x, y) is
    /// RGB (28x, 50y, 128).
    /// </summary>
    [TestClass]
    public class PictureImageTests
    {
        private static string CdbFile(string name, [CallerFilePath] string thisFile = "")
            => Path.Combine(Path.GetDirectoryName(thisFile), "Files", "CDB", name);

        // a PNG of 3×2 pixels
        private static readonly byte[] Png3x2 = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAMAAAACCAIAAAASFvFNAAAAG0lEQVR4nAXBAQEAAACCIDe5qbHOCgCHIeSyPCtwBd1HI03nAAAAAElFTkSuQmCC");

        private static Project RoundTrip(Project project)
        {
            MemoryStream written = new MemoryStream();
            project.WriteToJson(written); // closes the stream
            return Project.ReadFromJson(new MemoryStream(written.ToArray()));
        }

        [TestMethod]
        public void TheSizeIsReadFromTheHeader()
        {
            Assert.IsTrue(EncodedImage.TryGetSize(Png3x2, out int w, out int h));
            Assert.AreEqual((3, 2), (w, h));
            Assert.AreEqual("png", EncodedImage.FormatOf(Png3x2));

            byte[] gif = { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a', 0x2C, 0x01, 0x10, 0x00, 0, 0, 0 };
            Assert.IsTrue(EncodedImage.TryGetSize(gif, out w, out h));
            Assert.AreEqual((300, 16), (w, h));

            byte[] bmp = new byte[54];
            bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
            BitConverter.GetBytes(40).CopyTo(bmp, 14);
            BitConverter.GetBytes(640).CopyTo(bmp, 18);
            BitConverter.GetBytes(-480).CopyTo(bmp, 22); // top-down
            Assert.IsTrue(EncodedImage.TryGetSize(bmp, out w, out h));
            Assert.AreEqual((640, 480), (w, h));
            byte[] os2 = new byte[26];
            os2[0] = (byte)'B'; os2[1] = (byte)'M';
            BitConverter.GetBytes(12).CopyTo(os2, 14);
            BitConverter.GetBytes((short)33).CopyTo(os2, 18);
            BitConverter.GetBytes((short)44).CopyTo(os2, 20);
            Assert.IsTrue(EncodedImage.TryGetSize(os2, out w, out h));
            Assert.AreEqual((33, 44), (w, h));

            // SOI, an APP0 segment, a DHT segment (which must not be taken for a frame header), then SOF2 (progressive)
            byte[] jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x4A, 0x46, 0xFF, 0xC4, 0x00, 0x03, 0x00, 0xFF, 0xC2, 0x00, 0x0B, 0x08, 0x01, 0xE0, 0x02, 0x80, 0x01, 0x01, 0x11, 0x00 };
            Assert.IsTrue(EncodedImage.TryGetSize(jpeg, out w, out h));
            Assert.AreEqual((640, 480), (w, h));

            Assert.IsFalse(EncodedImage.TryGetSize(Png3x2.Take(20).ToArray(), out _, out _), "a truncated header");
            Assert.IsFalse(EncodedImage.TryGetSize(new byte[] { 0x49, 0x49, 0x2A, 0x00, 0, 0, 0, 0 }, out _, out _), "TIFF is not supported");
            Assert.IsFalse(EncodedImage.TryGetSize(null, out _, out _));
            Assert.IsFalse(EncodedImage.TryGetSize(jpeg.Take(14).ToArray(), out _, out _), "a JPEG without a frame header");
        }

        [TestMethod]
        public void APictureKeepsTheBytesOfTheImageFile()
        {
            Picture picture = Picture.Construct();
            picture.Set(Png3x2, new GeoPoint(1, 2, 3), new GeoVector(30, 0, 0), new GeoVector(0, 20, 0));
            Assert.AreSame(Png3x2, picture.ImageData);
            Assert.AreEqual(3, picture.PixelWidth);
            Assert.AreEqual(2, picture.PixelHeight);
            picture.SetWidth(60, true);
            Assert.AreEqual(40, picture.DirectionHeight.Length, 1e-12, "the aspect ratio of the pixels");
            Assert.ThrowsException<ArgumentException>(() => picture.ImageData = new byte[] { 1, 2, 3 });
            Assert.AreSame(Png3x2, picture.ImageData, "invalid data does not replace the image");
            picture.ImageData = null;
            Assert.AreEqual(0, picture.PixelWidth);
            Picture clone = (Picture)picture.Clone();
            Assert.IsNull(clone.ImageData);
        }

        [TestMethod]
        public void PicturesAndIconsSurviveAJsonRoundTrip()
        {
            Project project = Project.CreateSimpleProject();
            Picture picture = Picture.Construct();
            picture.Set(Png3x2, new GeoPoint(1, 2, 3), new GeoVector(30, 0, 0), new GeoVector(0, 20, 0));
            picture.Path = "image.png";
            project.GetActiveModel().Add(picture);
            Icon icon = Icon.Construct();
            icon.Set(Png3x2, new GeoPoint(4, 5, 6), 1, 1);
            project.GetActiveModel().Add(icon);

            MemoryStream written = new MemoryStream();
            project.WriteToJson(written);
            string json = System.Text.Encoding.UTF8.GetString(written.ToArray());
            StringAssert.Contains(json, Convert.ToBase64String(Png3x2), "the image file as Base64");
            Assert.IsFalse(json.Contains("System.Drawing"), "no GDI+ types in the file");

            Project read = Project.ReadFromJson(new MemoryStream(written.ToArray()));
            Picture readPicture = read.GetActiveModel().AllObjects.OfType<Picture>().Single();
            CollectionAssert.AreEqual(Png3x2, readPicture.ImageData);
            Assert.AreEqual(new GeoPoint(1, 2, 3), readPicture.Location);
            Assert.AreEqual("image.png", readPicture.Path);
            Icon readIcon = read.GetActiveModel().AllObjects.OfType<Icon>().Single();
            CollectionAssert.AreEqual(Png3x2, readIcon.ImageData);
            Assert.AreEqual(new GeoPoint(4, 5, 6), readIcon.Location);
        }

        private static void AssertLegacyPictures(Project project, string file)
        {
            Assert.IsNotNull(project, "could not read " + file);
            Picture[] pictures = project.GetActiveModel().AllObjects.OfType<Picture>().ToArray();
            Assert.AreEqual(2, pictures.Length, file);
            Picture png = pictures.Single(p => p.Path.EndsWith("pattern.png"));
            Assert.AreEqual("png", EncodedImage.FormatOf(png.ImageData), file);
            Assert.AreEqual((7, 5), (png.PixelWidth, png.PixelHeight), file);
            Assert.AreEqual(new GeoPoint(1, 2, 3), png.Location, file);
            Assert.AreEqual(new GeoVector(70, 0, 0), png.DirectionWidth, file);
            // the JPEG bitmap was serialized by System.Drawing.Common on .NET 8, which stores every image as PNG
            Picture jpeg = pictures.Single(p => p.Path == "pattern.jpg");
            Assert.IsNotNull(EncodedImage.FormatOf(jpeg.ImageData), file);
            Assert.AreEqual((9, 4), (jpeg.PixelWidth, jpeg.PixelHeight), file);
            Icon icon = project.GetActiveModel().AllObjects.OfType<Icon>().Single();
            Assert.AreEqual((4, 3), (icon.PixelWidth, icon.PixelHeight), file);
            Assert.AreEqual(new GeoPoint(5, 5, 0), icon.Location, file);
#if WINDOWS
            // GDI+ decodes the bytes to the pixels the 1.x bitmap had
#pragma warning disable CS0618 // the GDI+ bitmap is obsolete, but it is what the old files contained
            Bitmap bitmap = png.Bitmap;
#pragma warning restore CS0618
            for (int x = 0; x < 7; ++x)
                for (int y = 0; y < 5; ++y)
                    Assert.AreEqual(Color.FromArgb(255, 28 * x, 50 * y, 128).ToArgb(), bitmap.GetPixel(x, y).ToArgb(), $"{file}: pixel {x}, {y}");
#endif
        }

        [TestMethod]
        public void A1xJsonFileWithPicturesOpens()
        {
            AssertLegacyPictures(Project.ReadFromFile(CdbFile("pictures-1x.cdb.json"), "cdb"), "pictures-1x.cdb.json");
        }

        [TestMethod]
        [DoNotParallelize] // enables the BinaryFormatter for the process
        public void A1xBinaryFileWithPicturesOpens()
        {
            AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
            try
            {
#pragma warning disable SYSLIB0011 // the binary format is only read, for old files
                new BinaryFormatter().Serialize(new MemoryStream(), 1);
#pragma warning restore SYSLIB0011
            }
            catch (Exception e) when (e is NotSupportedException || e is PlatformNotSupportedException)
            {
                Assert.Inconclusive("this runtime has no BinaryFormatter: " + e.Message);
            }
            using (FileStream stream = File.OpenRead(CdbFile("pictures-1x.cdb")))
            {
#pragma warning disable CS0618 // ReadFromStream is kept for old binary files
                AssertLegacyPictures(Project.ReadFromStream(stream), "pictures-1x.cdb");
#pragma warning restore CS0618
            }
        }

        [TestMethod]
        public void AnUnreadableImageIsLoadedFromThePath()
        {
            string file = Path.Combine(Path.GetTempPath(), "PictureImageTests-" + Guid.NewGuid().ToString("N") + ".png");
            File.WriteAllBytes(file, Png3x2);
            try
            {
                Project project = Project.CreateSimpleProject();
                Picture picture = Picture.Construct();
                picture.Set(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0, 1, 0, 0, 0, 1 }, GeoPoint.Origin, GeoVector.XAxis, GeoVector.YAxis);
                picture.Path = file;
                project.GetActiveModel().Add(picture);
                MemoryStream written = new MemoryStream();
                project.WriteToJson(written);
                // damage the image in the file: a header that ends before its size
                string json = System.Text.Encoding.UTF8.GetString(written.ToArray()).Replace(Convert.ToBase64String(picture.ImageData), Convert.ToBase64String(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }));
                Project read = Project.ReadFromJson(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));
                CollectionAssert.AreEqual(Png3x2, read.GetActiveModel().AllObjects.OfType<Picture>().Single().ImageData);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void APictureIsPaintedWithGdiOrAsItsOutline()
        {
            Picture picture = Picture.Construct();
            picture.Set(Png3x2, GeoPoint.Origin, new GeoVector(30, 0, 0), new GeoVector(0, 20, 0));
            RecordingPaintTo3D paint = new RecordingPaintTo3D();
            picture.PrePaintTo3D(paint);
            picture.PaintTo3D(paint);
#if WINDOWS
            Assert.AreEqual(1, paint.Bitmaps.Count, "GDI+ decodes the image");
            Assert.AreEqual((3, 2), (paint.Bitmaps[0].Width, paint.Bitmaps[0].Height));
#else
            // without GDI+ the image is not decoded, and the outline shows where the picture is
            Assert.AreEqual(0, paint.Bitmaps.Count);
            Assert.AreEqual(1, paint.Polylines.Count);
            CollectionAssert.AreEqual(new[] { GeoPoint.Origin, new GeoPoint(30, 0, 0), new GeoPoint(30, 20, 0), new GeoPoint(0, 20, 0), GeoPoint.Origin }, paint.Polylines[0]);
#endif
        }

        private class RecordingPaintTo3D : IPaintTo3D
        {
            public List<GeoPoint[]> Polylines { get; } = new List<GeoPoint[]>();
            public List<Bitmap> Bitmaps { get; } = new List<Bitmap>();

            public void Polyline(GeoPoint[] points) { Polylines.Add(points); }
            public void RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight) { Bitmaps.Add(bitmap); }

            #region unused IPaintTo3D members
            public bool PaintSurfaces => true;
            public bool PaintEdges => true;
            public bool PaintSurfaceEdges { get; set; }
            public bool UseLineWidth { get; set; }
            public double Precision { get; set; } = 0.01;
            public double PixelToWorld => 1.0;
            public bool SelectMode { get; set; }
            public Color SelectColor { get; set; }
            public bool DelayText { get; set; }
            public bool DelayAll { get; set; }
            public bool TriangulateText { get; set; }
            public bool DontRecalcTriangulation { get; set; }
            public PaintCapabilities Capabilities => PaintCapabilities.Standard;
            public IDisposable FacesBehindEdgesOffset => null;
            public bool IsBitmap => false;

            public void MakeCurrent() { }
            public void SetColor(Color color, int lockColor = 0) { }
            public void AvoidColor(Color color) { }
            public void SetLineWidth(LineWidth lineWidth) { }
            public void SetLinePattern(LinePattern pattern) { }
            public void FilledPolyline(GeoPoint[] points) { }
            public void Points(GeoPoint[] points, float size, PointSymbol pointSymbol) { }
            public void Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples) { }
            public void PrepareText(string fontName, string textString, FontStyle fontStyle) { }
            public void PreparePointSymbol(PointSymbol pointSymbol) { }
            public void PrepareIcon(Bitmap icon) { }
            public void PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset) { }
            public void PrepareBitmap(Bitmap bitmap) { }
            public void Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, CADability.GeoObject.Text.AlignMode alignment, CADability.GeoObject.Text.LineAlignMode lineAlignment) { }
            public void List(IPaintTo3DList paintThisList) { }
            public void SelectedList(IPaintTo3DList paintThisList, int wobbleRadius) { }
            public void Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree) { }
            public void Line2D(int sx, int sy, int ex, int ey) { }
            public void Line2D(PointF p1, PointF p2) { }
            public void FillRect2D(PointF p1, PointF p2) { }
            public void Point2D(int x, int y) { }
            public void DisplayIcon(GeoPoint p, Bitmap icon) { }
            public void DisplayBitmap(GeoPoint p, Bitmap bitmap) { }
            public void SetProjection(Projection projection, BoundingCube boundingCube) { }
            public void Clear(Color background) { }
            public void Resize(int width, int height) { }
            public void OpenList(string name = null) { }
            public IPaintTo3DList CloseList() => null;
            public IPaintTo3DList MakeList(List<IPaintTo3DList> sublists) => null;
            public void OpenPath() { }
            public void ClosePath(Color color) { }
            public void CloseFigure() { }
            public void Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter) { }
            public void FreeUnusedLists() { }
            public void UseZBuffer(bool use) { }
            public void Blending(bool on) { }
            public void FinishPaint() { }
            public void PaintFaces(PaintTo3D.PaintMode paintMode) { }
            public void Dispose() { }
            public void PushState() { }
            public void PopState() { }
            public void PushMultModOp(ModOp insertion) { }
            public void PopModOp() { }
            public void SetClip(Rectangle clipRectangle) { }
            #endregion
        }
    }
}
