using CADability.Attribute;
using CADability.GeoObject;
using Point = CADability.GeoObject.Point; // disambiguate from the global using of System.Drawing

namespace CADability.Tests
{
    [TestClass]
    public class PointTests
    {
        [TestMethod]
        public void PaintTo3D_NotSelectMode_PassesPointSize()
        {
            var point = Point.Construct();
            point.Location = new GeoPoint(1, 2, 3);
            point.Symbol = PointSymbol.Cross;
            point.Size = 7.5;

            var paintTo3D = new RecordingPaintTo3D { SelectMode = false };
            point.PaintTo3D(paintTo3D);

            var call = Assert.That.Single(paintTo3D.PointsCalls);

            Assert.AreEqual(7.5f, call.Size);
            Assert.AreEqual(PointSymbol.Cross, call.Symbol);
            Assert.AreEqual(new GeoPoint(1, 2, 3), Assert.That.Single(call.Points));
        }

        private class RecordingPaintTo3D : IPaintTo3D
        {
            public record PointsCall(GeoPoint[] Points, float Size, PointSymbol Symbol);

            public List<PointsCall> PointsCalls { get; } = new List<PointsCall>();

            public void Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
            {
                PointsCalls.Add(new PointsCall(points, size, pointSymbol));
            }

            #region unused IPaintTo3D members
            public bool PaintSurfaces => true;
            public bool PaintEdges => true;
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
            public PaintCapabilities Capabilities => PaintCapabilities.Standard;
            public IDisposable FacesBehindEdgesOffset => null;
            public bool IsBitmap => false;

            public void MakeCurrent() { }
            public void SetColor(Color color, int lockColor = 0) { }
            public void AvoidColor(Color color) { }
            public void SetLineWidth(LineWidth lineWidth) { }
            public void SetLinePattern(LinePattern pattern) { }
            public void Polyline(GeoPoint[] points) { }
            public void FilledPolyline(GeoPoint[] points) { }
            public void Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples) { }
            public void PrepareText(string fontName, string textString, FontStyle fontStyle) { }
            public void PreparePointSymbol(PointSymbol pointSymbol) { }
            public void PrepareIcon(Bitmap icon) { }
            public void PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset) { }
            public void PrepareBitmap(Bitmap bitmap) { }
            public void RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight) { }
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
            public void SetProjection(Projection projection, BoundingBox boundingCube) { }
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
