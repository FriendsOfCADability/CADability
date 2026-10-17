using CADability;
using CADability.Attribute;
using CADability.GeoObject;
using System.Drawing;

namespace CADability.WasmSmoke
{
    /// <summary>
    /// A renderer of its own, as a browser front end would write one: it implements <see cref="IPaintTo3D"/> and records
    /// what the kernel paints. The interface still names GDI+ types (<see cref="Bitmap"/>, <see cref="FontStyle"/>); a
    /// renderer outside Windows implements those members without ever creating a GDI+ object.
    /// </summary>
    internal sealed class RecordingPaintTo3D : IPaintTo3D
    {
        // the kernel paints glyphs and other objects into display lists once and then paints the lists: a list keeps the
        // calls and replays them with the current transformation
        private sealed class DisplayList : IPaintTo3DList, IDisposable
        {
            public readonly List<Action> Calls = new List<Action>();
            public string Name { get; set; }
            public List<IPaintTo3DList> containedSubLists { set { } }
            public void Dispose() { }
        }
        private readonly Stack<DisplayList> openLists = new Stack<DisplayList>();

        /// <summary>
        /// Keeps the call in the open display list, if there is one, and returns true; false if it is to be recorded now.
        /// </summary>
        private bool IntoList(Action call)
        {
            if (openLists.Count == 0) return false;
            openLists.Peek().Calls.Add(call);
            return true;
        }

        public int Polylines, PolylinePoints, Triangles, TextCalls, Bitmaps, NurbsCalls, Arcs, PointCalls;
        public BoundingCube Extent = BoundingCube.EmptyBoundingCube;
        private readonly Stack<ModOp> modOps = new Stack<ModOp>();
        private ModOp current = ModOp.Identity;

        private void Record(GeoPoint p) => Extent.MinMax(current * p);

        public bool PaintSurfaces => true;
        public bool PaintEdges => true;
        public bool PaintSurfaceEdges { get; set; } = true;
        public bool UseLineWidth { get; set; }
        public double Precision { get; set; } = 0.01;
        public double PixelToWorld => 0.1;
        public bool SelectMode { get; set; }
        public Color SelectColor { get; set; }
        public bool DelayText { get; set; }
        public bool DelayAll { get; set; }
        public bool TriangulateText { get; set; } = true;
        public bool DontRecalcTriangulation { get; set; }
        public PaintCapabilities Capabilities => PaintCapabilities.Standard;
        public IDisposable FacesBehindEdgesOffset => new DisplayList();
        public bool IsBitmap => false;

        public void MakeCurrent() { }
        public void SetColor(Color color, int lockColor = 0) { }
        public void AvoidColor(Color color) { }
        public void SetLineWidth(LineWidth lineWidth) { }
        public void SetLinePattern(LinePattern pattern) { }
        public void Polyline(GeoPoint[] points)
        {
            if (IntoList(() => Polyline(points))) return;
            ++Polylines;
            PolylinePoints += points.Length;
            foreach (GeoPoint p in points) Record(p);
        }
        public void FilledPolyline(GeoPoint[] points) => Polyline(points);
        public void Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
        {
            if (IntoList(() => Points(points, size, pointSymbol))) return;
            ++PointCalls;
            foreach (GeoPoint p in points) Record(p);
        }
        public void Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples)
        {
            if (IntoList(() => Triangle(vertex, normals, indextriples))) return;
            Triangles += indextriples.Length / 3;
            foreach (GeoPoint p in vertex) Record(p);
        }
        public void PrepareText(string fontName, string textString, FontStyle fontStyle) { }
        public void PreparePointSymbol(PointSymbol pointSymbol) { }
        public void PrepareIcon(Bitmap icon) { }
        public void PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset) { }
        public void PrepareBitmap(Bitmap bitmap) { }
        public void RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight)
        {
            if (!IntoList(() => RectangularBitmap(bitmap, location, directionWidth, directionHeight))) ++Bitmaps;
        }
        public void Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, Text.AlignMode alignment, Text.LineAlignMode lineAlignment)
        {
            if (IntoList(() => Text(lineDirection, glyphDirection, location, fontName, textString, fontStyle, alignment, lineAlignment))) return;
            ++TextCalls;
            Record(location);
        }
        public void List(IPaintTo3DList paintThisList)
        {
            if (IntoList(() => List(paintThisList))) return;
            if (paintThisList is DisplayList list) foreach (Action call in list.Calls) call();
        }
        public void SelectedList(IPaintTo3DList paintThisList, int wobbleRadius) { }
        public void Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree)
        {
            if (IntoList(() => Nurbs(poles, weights, knots, degree))) return;
            ++NurbsCalls;
            foreach (GeoPoint p in poles) Record(p);
        }
        public void Line2D(int sx, int sy, int ex, int ey) { }
        public void Line2D(PointF p1, PointF p2) { }
        public void FillRect2D(PointF p1, PointF p2) { }
        public void Point2D(int x, int y) { }
        public void DisplayIcon(GeoPoint p, Bitmap icon) { if (!IntoList(() => DisplayIcon(p, icon))) ++Bitmaps; }
        public void DisplayBitmap(GeoPoint p, Bitmap bitmap) { if (!IntoList(() => DisplayBitmap(p, bitmap))) ++Bitmaps; }
        public void SetProjection(Projection projection, BoundingCube boundingCube) { }
        public void Clear(Color background) { }
        public void Resize(int width, int height) { }
        public void OpenList(string name = null) => openLists.Push(new DisplayList { Name = name });
        public IPaintTo3DList CloseList() => openLists.Pop();
        public IPaintTo3DList MakeList(List<IPaintTo3DList> sublists)
        {
            DisplayList res = new DisplayList();
            foreach (IPaintTo3DList sublist in sublists) res.Calls.Add(() => List(sublist));
            return res;
        }
        public void OpenPath() { }
        public void ClosePath(Color color) { }
        public void CloseFigure() { }
        public void Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter)
        {
            if (IntoList(() => Arc(center, majorAxis, minorAxis, startParameter, sweepParameter))) return;
            ++Arcs;
            Record(center);
        }
        public void FreeUnusedLists() { }
        public void UseZBuffer(bool use) { }
        public void Blending(bool on) { }
        public void FinishPaint() { }
        public void PaintFaces(PaintTo3D.PaintMode paintMode) { }
        public void Dispose() { }
        public void PushState() { }
        public void PopState() { }
        public void PushMultModOp(ModOp insertion)
        {
            if (IntoList(() => PushMultModOp(insertion))) return;
            modOps.Push(current);
            current = current * insertion;
        }
        public void PopModOp()
        {
            if (IntoList(PopModOp)) return;
            current = modOps.Pop();
        }
        public void SetClip(Rectangle clipRectangle) { }
    }
}
