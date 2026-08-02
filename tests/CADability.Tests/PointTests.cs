using System.Drawing;
using CADability.Attribute;
using CADability.GeoObject;

namespace CADability.Tests
{
    [TestClass]
    public class PointTests
    {
        [TestMethod]
        public void PaintTo3D_ShouldUsePointSize_WhenNotInSelectMode()
        {
            var point = Point.Construct();
            point.Location = new GeoPoint(1, 2, 3);
            point.Size = 7.5;
            point.Symbol = PointSymbol.Cross | PointSymbol.Circle;

            var paintTo3D = new CapturingPaintTo3D
            {
                SelectMode = false
            };

            point.PaintTo3D(paintTo3D);

            CollectionAssert.AreEqual(new[] { point.Location }, paintTo3D.LastPoints);
            Assert.AreEqual((float)point.Size, paintTo3D.LastPointSize);
            Assert.AreEqual(point.Symbol, paintTo3D.LastPointSymbol);
        }

        private sealed class CapturingPaintTo3D : IPaintTo3D
        {
            internal GeoPoint[] LastPoints { get; private set; } = Array.Empty<GeoPoint>();
            internal float LastPointSize { get; private set; }
            internal PointSymbol LastPointSymbol { get; private set; }

            public bool PaintSurfaces => false;
            public bool PaintEdges => false;
            public bool PaintSurfaceEdges { get; set; }
            public bool UseLineWidth { get; set; }
            public double Precision { get; set; }
            public double PixelToWorld => 1.0;
            public bool SelectMode { get; set; }
            public Color SelectColor { get; set; }
            public bool DelayText { get; set; }
            public bool DelayAll { get; set; }
            public bool TriangulateText { get; set; }
            public bool DontRecalcTriangulation { get; set; }
            public PaintCapabilities Capabilities => PaintCapabilities.None;
            public IDisposable FacesBehindEdgesOffset => NoOpDisposable.Instance;
            public bool IsBitmap => false;

            public void MakeCurrent() { }
            public void SetColor(Color color, int lockColor = 0) { }
            public void AvoidColor(Color color) { }
            public void SetLineWidth(LineWidth lineWidth) { }
            public void SetLinePattern(LinePattern pattern) { }
            public void Polyline(GeoPoint[] points) { }
            public void FilledPolyline(GeoPoint[] points) { }
            public void Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
            {
                LastPoints = points;
                LastPointSize = size;
                LastPointSymbol = pointSymbol;
            }
            public void Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples) { }
            public void PrepareText(string fontName, string textString, FontStyle fontStyle) { }
            public void PreparePointSymbol(PointSymbol pointSymbol) { }
            public void PrepareIcon(Bitmap icon) { }
            public void PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset) { }
            public void PrepareBitmap(Bitmap bitmap) { }
            public void RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight) { }
            public void Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, GeoObject.Text.AlignMode alignment, GeoObject.Text.LineAlignMode lineAlignment) { }
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

            private sealed class NoOpDisposable : IDisposable
            {
                internal static readonly NoOpDisposable Instance = new();
                public void Dispose() { }
            }
        }
    }
}
