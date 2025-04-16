// #define DEBUG_OPENGL
using CADability.Attribute;
using CADability.GeoObject;
using System;
using System.Collections.Generic;
#if WEBASSEMBLY
using CADability.WebDrawing;
#else
using System.Drawing;
#endif
using System.Runtime.InteropServices;
using System.Threading;

using Wintellect.PowerCollections;

namespace CADability
{

    /// <summary>
    /// Exception which is thrown when the is not egnough memory for the 3D display driver
    /// </summary>

    public class PaintTo3DOutOfMemory : ApplicationException
    {
        internal PaintTo3DOutOfMemory()
        {
        }
    }

    /// <summary>
    /// Capabilities of the paint interface
    /// </summary>
    [Flags()]
    public enum PaintCapabilities
    {
        /// <summary>
        /// No special capabilities
        /// </summary>
        Standard = 0,
        /// <summary>
        /// Can paint circular arcs
        /// </summary>
        CanDoArcs = 1,
        /// <summary>
        /// Can fill a path (use the methods <see cref="IPaintTo3D.OpenPath"/>, <see cref="IPaintTo3D.CloseFigure"/> and <see cref="IPaintTo3D.ClosePath"/>.
        /// </summary>
        CanFillPaths = 2,
        /// <summary>
        /// Can handle a displaylist which is independant of the zoom level.
        /// </summary>
        ZoomIndependentDisplayList = 4
    }

    /// <summary>
    /// Interface to paint on a OpenGL, DirectX, GDI or some other output device. This interface may still change in future
    /// it is provided for informational purposes only.
    /// </summary>

    public interface IPaintTo3D
    {
        /// <summary>
        /// Determins whether surfaces (triangles) are included in painting
        /// </summary>
        bool PaintSurfaces { get; }
        /// <summary>
        /// Determins whether curves are included in painting
        /// </summary>
        bool PaintEdges { get; }
        /// <summary>
        /// Determins whether edges of faces should also be painted
        /// </summary>
        bool PaintSurfaceEdges { get; set; }
        /// <summary>
        /// Should the line width be applied to painting curves
        /// </summary>
        bool UseLineWidth { get; set; }
        /// <summary>
        /// Gets or sets the precision of the display. (used for tesselation or curve approximation)
        /// </summary>
        double Precision { get; set; }
        /// <summary>
        /// Returns a factor that translates a one pixel distance into world coordinates
        /// </summary>
        double PixelToWorld { get; }
        /// <summary>
        /// Gets or sets the flag whether the next objects should be painted in the "select mode"
        /// </summary>
        bool SelectMode { get; set; }
        /// <summary>
        /// Gets or sets the select color
        /// </summary>
        Color SelectColor { get; set; }
        /// <summary>
        /// Depricated, not implemented in any current paint interface
        /// </summary>
        bool DelayText { get; set; } // Text nicht in die DisplayListen, da out of Memory
        /// <summary>
        /// Depricated, not implemented in any current paint interface
        /// </summary>
        bool DelayAll { get; set; } // kein Objekt in die DisplayListen, da out of Memory
        /// <summary>
        /// Will text objects be tesselated
        /// </summary>
        bool TriangulateText { get; }
        /// <summary>
        /// Deprecated, currently not used
        /// </summary>
        bool DontRecalcTriangulation { get; set; }
        /// <summary>
        /// Returns the capabilities of this implementation of the paint interface
        /// </summary>
        PaintCapabilities Capabilities { get; }

        /// <summary>
        /// Will be called before any other paint methods are called. May be called multiple times after <see cref="Init"/> has been called
        /// </summary>
        void MakeCurrent();
        /// <summary>
        /// Sets the color for the next paint methods
        /// </summary>
        /// <param name="color">The color to use for drawing</param>
        void SetColor(Color color);
        /// <summary>
        /// Never use this color for drawing (because it is the background color)
        /// </summary>
        /// <param name="color">Color to avoid</param>
        void AvoidColor(Color color);
        /// <summary>
        /// Sets the line width for subsequent curve drawing
        /// </summary>
        /// <param name="lineWidth">Line width in world coordinates</param>
        void SetLineWidth(LineWidth lineWidth);
        /// <summary>
        /// Sets the line pattern for subsequent curve drawing. A pattern consists of
        /// pairs of double values: stroke length followed by gap length. If the parameter is null
        /// or an empty array, solidlines or curves will be drawn.
        /// </summary>
        /// <param name="pattern"></param>
        void SetLinePattern(LinePattern pattern);
        /// <summary>
        /// Draws a sequence of lines.
        /// </summary>
        /// <param name="points">The points to connect</param>
        void Polyline(GeoPoint[] points);
        /// <summary>
        /// Deprecated, will not be used from within CADability.
        /// </summary>
        /// <param name="points"></param>
        void FilledPolyline(GeoPoint[] points);
        /// <summary>
        /// Draws simple pixel based points e.g. for background grid display.
        /// </summary>
        /// <param name="points"></param>
        /// <param name="size"></param>
        void Points(GeoPoint[] points, float size, PointSymbol pointSymbol);
        /// <summary>
        /// Draw a set of solid-filled triangles with the current color.
        /// </summary>
        /// <param name="vertex">The coordinates of the vertices</param>
        /// <param name="normals">The coordinates of the normal vectors in the vertices</param>
        /// <param name="indextriples">Triples of indices which define traingles. Length must be a multiple of 3</param>
        void Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples);
        /// <summary>
        /// Assure that the charactres in <paramref name="textString"/> will be available when
        /// <see cref="Text"/> is called.
        /// </summary>
        /// <param name="fontName">Name of the font</param>
        /// <param name="textString">String to be painted</param>
        /// <param name="fontStyle">Additional font style (bold, italic, etc.)</param>
        void PrepareText(string fontName, string textString, FontStyle fontStyle);
        /// <summary>
        /// Assure that the point symbol is loaded
        /// </summary>
        /// <param name="pointSymbol"></param>
        void PreparePointSymbol(PointSymbol pointSymbol);
        /// <summary>
        /// Assure that the <paramref name="icon"/> will be available when <see cref="DisplayIcon"/> will subsequently be called.
        /// (Some implementations cache the icon or transform it into an convenient format)
        /// </summary>
        /// <param name="icon">The icon</param>
        void PrepareIcon(Bitmap icon);
        /// <summary>
        /// Assure that the <paramref name="bitmap"/> will be available when <see cref="DisplayIcon"/> will subsequently be called.
        /// (Some implementations cache the bitmap or transform it into an convenient format)
        /// </summary>
        /// <param name="bitmap">The bitmap</param>
        /// <param name="xoffset">x-component of the origin that defines the insertion point (e.g. to center the bitmap)</param>
        /// <param name="yoffset">y-component of the origin</param>
        void PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset);
        /// <summary>
        /// Similar to <see cref="PrepareBitmap(Bitmap , int , int )"/> with origin set to (0,0)
        /// </summary>
        /// <param name="bitmap">The bitmap.</param>
        void PrepareBitmap(Bitmap bitmap);
        /// <summary>
        /// Draws a rectangular bitmap at the provided <paramref name="location"/> with <paramref name="directionWidth"/>
        /// specifying the direction of the lower edge of the bitmap and <paramref name="directionHeight"/>
        /// specifying the direction of the left edge of the bitmap. <see cref="PrepareBitmap"/> must be called 
        /// before this method is called.
        /// </summary>
        /// <param name="bitmap">The bitmap to draw</param>
        /// <param name="location">Location of the lower left corner of the bitmap</param>
        /// <param name="directionWidth">Direction of the lower edge of the bitmap</param>
        /// <param name="directionHeight">Direction of the left edge of the bitmap</param>
        void RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight);
        /// <summary>
        /// Draw a text with the provided parameters and the current color.
        /// </summary>
        /// <param name="lineDirection">Direction of the base line of the text</param>
        /// <param name="glyphDirection">Direction of the glyph of the characters (for horizontal text this is the y-axis)</param>
        /// <param name="location">Location where to draw the text (using alignement)</param>
        /// <param name="fontName">Name of the font</param>
        /// <param name="textString">String to draw</param>
        /// <param name="fontStyle">Style of the font (e.g. bold)</param>
        /// <param name="alignment">Left, right or center (horizontal) alignement</param>
        /// <param name="lineAlignment">Vertical alignement</param>
        void Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, CADability.GeoObject.Text.AlignMode alignment, CADability.GeoObject.Text.LineAlignMode lineAlignment);
        /// <summary>
        /// Paint the provided display list.
        /// </summary>
        /// <param name="paintThisList">Display list to paint</param>
        void List(IPaintTo3DList paintThisList);
        /// <summary>
        /// Paint the provided display list using the display mode.
        /// </summary>
        /// <param name="paintThisList">List to paint</param>
        /// <param name="wobbleRadius">Wobble radius to paint the same list multiple times with small offsets</param>
        void SelectedList(IPaintTo3DList paintThisList, int wobbleRadius);
        /// <summary>
        /// Deprecated, not used anymore and not implemented by the CADability display drivers.
        /// </summary>
        /// <param name="poles"></param>
        /// <param name="weights"></param>
        /// <param name="knots"></param>
        /// <param name="degree"></param>
        void Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree);
        /// <summary>
        /// Paint a 2D line in the pixel coordinates system of the display. Usually used for background painting.
        /// </summary>
        /// <param name="sx">Start x-coordinate</param>
        /// <param name="sy">Start y-coordinate</param>
        /// <param name="ex">End x-coordinate</param>
        /// <param name="ey">End y-coordinate</param>
        void Line2D(int sx, int sy, int ex, int ey);
        /// <summary>
        /// Paint a 2D line in the pixel coordinates system of the display. Usually used for background painting.
        /// Currently not used.
        /// </summary>
        /// <param name="p1">Start point</param>
        /// <param name="p2">End point</param>
        void Line2D(PointF p1, PointF p2);
        /// <summary>
        /// Fill the axis oriented rectangle with the current color. Usually used for background painting.
        /// </summary>
        /// <param name="p1">Lower left point of the rectangle</param>
        /// <param name="p2">Upper right point of the rectangle</param>
        void FillRect2D(PointF p1, PointF p2);
        /// <summary>
        /// Deprecated, currently not used and not implemented
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        void Point2D(int x, int y);
        /// <summary>
        /// Displays the provided icon at the provided position. <see cref="PrepareIcon"/> has been called or must be called
        /// before this method is called. The icon aleasy faces the viewer, no perspective of the projection is applied.
        /// </summary>
        /// <param name="p">Where to draw the icon (world coordinates)</param>
        /// <param name="icon">The icon to draw</param>
        void DisplayIcon(GeoPoint p, Bitmap icon);
        /// <summary>
        /// Displays the provided bitmap at the provided location. The bitmap always faces the viewer.
        /// <see cref="PrepareBitmap"/> has been called or must be called prior to this method.
        /// </summary>
        /// <param name="p">Where to Paint</param>
        /// <param name="bitmap">Th ebitmap to paint</param>
        void DisplayBitmap(GeoPoint p, Bitmap bitmap);
        /// <summary>
        /// Sets the projection to use in subsequent calls to paint methods.
        /// </summary>
        /// <param name="projection">The projection</param>
        /// <param name="boundingCube">The bounding cube, may be used for clipping</param>
        void SetProjection(Projection projection, BoundingCube boundingCube);
        /// <summary>
        /// Clear the whole area with this color. Used before painting objects.
        /// </summary>
        /// <param name="background">Color to clear display with</param>
        void Clear(Color background);
        /// <summary>
        /// Called when the size of the container associated with this interface changes.
        /// </summary>
        /// <param name="width">New width in pixels</param>
        /// <param name="height">New height in pixels</param>
        void Resize(int width, int height);
        /// <summary>
        /// Opens a new display list. All subsequent calls to paint methods will be stred in the list. When
        /// <see cref="CloseList"/> will be called the object resembling the list will be returned. Only the following 
        /// method calls are allowed while a displaylist is open: <see cref="Polyline"/>, <see cref="FilledPolyline"/>,
        /// <see cref="Points"/>, <see cref="Triangle"/>, <see cref="RectangularBitmap"/>, <see cref="Text"/>,
        /// <see cref="DisplayIcon"/>, <see cref="DisplayBitmap"/>, <see cref="List"/>
        /// </summary>
        void OpenList(string name = null);
        /// <summary>
        /// Close the display list <see cref="OpenList"/>.
        /// </summary>
        /// <returns>An object resembling the display list</returns>
        IPaintTo3DList CloseList();
        /// <summary>
        /// Makes a new display list as an assembly of the provided lists.
        /// </summary>
        /// <param name="sublists">List to assemble</param>
        /// <returns>Created display list</returns>
        IPaintTo3DList MakeList(List<IPaintTo3DList> sublists);
        /// <summary>
        /// When the implementation supports Paths (see <see cref="PaintCapabilities.CanFillPaths"/>), this call
        /// will start recording calls to <see cref="Polyline"/> and <see cref="Arc"/> until <see cref="ClosePath"/> is called.
        /// </summary>
        void OpenPath();
        //void BeginOutline();
        //void BeginHole();
        //void PaintRegion(Color color);
        /// <summary>
        /// Closes the path which was started with <see cref="OpenPath"/> and fills the interior with the provided color.
        /// </summary>
        /// <param name="color">Color to fill the path with.</param>
        void ClosePath(Color color);
        /// <summary>
        /// Closes a subfigure while defining a path. Subfigures are the enclosing path and the holes to be spared.
        /// Only valid after <see cref="OpenPath"/> and before <see cref="ClosePath"/> have been called.
        /// </summary>
        void CloseFigure();
        /// <summary>
        /// Draws an (elliptical) arc according to the provided parameters. May only be used when the implementation supports arcs (see <see cref="PaintCapabilities.CanDoArcs"/>.
        /// </summary>
        /// <param name="center">The center of the arc</param>
        /// <param name="majorAxis">Major axis, also defines the radius</param>
        /// <param name="minorAxis">Minor axis, also defines the radius. When painting a circular arc, minorAxis and majorAxis must have the same length</param>
        /// <param name="startParameter">Radian value of the starting position (0 is direction of majorAxis)</param>
        /// <param name="sweepParameter">Radian for the sweep angle</param>
        void Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter);
        /// <summary>
        /// Only used internally, no need to call.
        /// </summary>
        void FreeUnusedLists();
        /// <summary>
        /// Usually set to true, switch off to paint coordinate axis etc.
        /// </summary>
        /// <param name="use"></param>
        void UseZBuffer(bool use);
        /// <summary>
        /// OpenGL specific, set blending mode
        /// </summary>
        /// <param name="on"></param>
        void Blending(bool on);
        /// <summary>
        /// Call when a frame is finished and the display buffers should now be switched (if using two display buffers)
        /// </summary>
        void FinishPaint();
        /// <summary>
        /// Faces are painted with a small offset to the backgroung, wheras curves are painted with a small offset to the front.
        /// This ensures proper visibility of edges that lie on faces.
        /// </summary>
        /// <param name="paintMode">Paint faces, curves or both (<see cref="PaintTo3D.PaintMode"/>)</param>
        void PaintFaces(PaintTo3D.PaintMode paintMode);
        /// <summary>
        /// Internal use only.
        /// </summary>
        IDisposable FacesBehindEdgesOffset { get; }
        /// <summary>
        /// Will be called when the assoziated control is closed.
        /// </summary>
        void Dispose();
        /// <summary>
        /// Saves the current state.
        /// </summary>
        void PushState();
        /// <summary>
        /// Restores the previously saved state
        /// </summary>
        void PopState();
        /// <summary>
        /// Sets a matrix to multiply all objects beeing displayed with. This matrix will be applied additionally
        /// to the current matrix
        /// </summary>
        /// <param name="insertion">New matrix</param>
        void PushMultModOp(ModOp insertion);
        /// <summary>
        /// Undoes the previously called <see cref="PushMultModOp"/>
        /// </summary>
        void PopModOp();
        /// <summary>
        /// Sets a clip rectangle for subsequent paint commands. If <paramref name="clipRectangle"/> is empty,
        /// no clipping will occur.
        /// </summary>
        /// <param name="clipRectangle">Rectangle to use for clipping</param>
        void SetClip(Rectangle clipRectangle);
        bool IsBitmap { get; }
    }

    /// <summary>
    /// Some static helper methods
    /// </summary>

    public class PaintTo3D
    {
        public enum PaintMode { FacesOnly, CurvesOnly, All }
        public static void PaintHandle(IPaintTo3D paintTo3D, int x, int y, int width, Color color)
        {
            paintTo3D.SetLineWidth(null); // dünnstmöglich
            paintTo3D.SetLinePattern(null);
            paintTo3D.SetColor(color);
            paintTo3D.Line2D(x - width, y - width, x - width, y + width);
            paintTo3D.Line2D(x - width, y + width, x + width, y + width);
            paintTo3D.Line2D(x + width, y + width, x + width, y - width);
            paintTo3D.Line2D(x + width, y - width, x - width, y - width);
            // ohne die folgenden Aufrufe fehlt meist ein Punkt im Quadrat
            // hängt vom OpenGl Treiber ab: manchmal sind diese Punkte versetzt
            //paintTo3D.Point2D(x - width, y - width);
            //paintTo3D.Point2D(x - width, y + width);
            //paintTo3D.Point2D(x + width, y + width);
            //paintTo3D.Point2D(x + width, y - width);
        }
        public static void PaintHandle(IPaintTo3D paintTo3D, PointF pf, int width, Color color)
        {
            paintTo3D.SetColor(color);
            paintTo3D.SetLineWidth(null);
            paintTo3D.SetLinePattern(null);
            paintTo3D.Line2D((int)pf.X - width, (int)pf.Y - width, (int)pf.X - width, (int)pf.Y + width);
            paintTo3D.Line2D((int)pf.X - width, (int)pf.Y + width, (int)pf.X + width, (int)pf.Y + width);
            paintTo3D.Line2D((int)pf.X + width, (int)pf.Y + width, (int)pf.X + width, (int)pf.Y - width);
            paintTo3D.Line2D((int)pf.X + width, (int)pf.Y - width, (int)pf.X - width, (int)pf.Y - width);
            //paintTo3D.Point2D((int)pf.X + width, (int)pf.Y + width);
            //paintTo3D.Point2D((int)pf.X + width, (int)pf.Y - width);
            //paintTo3D.Point2D((int)pf.X - width, (int)pf.Y + width);
            //paintTo3D.Point2D((int)pf.X - width, (int)pf.Y - width);
        }

		//Not implemented?
		internal static List<Bitmap> BitmapList => null;

        public static void PointSymbol(IPaintTo3D paintTo3D, GeoPoint location, double size, GeoObject.PointSymbol symbol)
        {
            Bitmap bmp = null;
            if ((symbol & CADability.GeoObject.PointSymbol.Select) != 0)
            {
                bmp = BitmapList[12];
                if (bmp != null)
                {
                    paintTo3D.DisplayIcon(location, bmp);
                }
                return; // nur das volle Quadrat anzeigen, sonst nichts
            }

            int offset = 0;
            if (paintTo3D.UseLineWidth) offset = 6; // so wird gesteuert dass bei nur dünn die dünnen Punkte und bei
            // mit Linienstärke ggf. die dicken Punkte angezeigt werden (Forderung PFOCAD)
            switch ((GeoObject.PointSymbol)((int)symbol & 0x07))
            {
                case CADability.GeoObject.PointSymbol.Empty:
                    bmp = null;
                    break;
                case CADability.GeoObject.PointSymbol.Dot:
                    {
                        bmp = BitmapList[0 + offset];
                    }
                    break;
                case CADability.GeoObject.PointSymbol.Plus:
                    {
                        bmp = BitmapList[1 + offset];
                    }
                    break;
                case CADability.GeoObject.PointSymbol.Cross:
                    {
                        bmp = BitmapList[2 + offset];
                    }
                    break;
                case CADability.GeoObject.PointSymbol.Line:
                    {
                        bmp = BitmapList[3 + offset];
                    }
                    break;
            }
            if (bmp != null)
            {
                paintTo3D.DisplayIcon(location, bmp);
            }
            bmp = null;
            if ((symbol & CADability.GeoObject.PointSymbol.Circle) != 0)
            {
                bmp = BitmapList[5 + offset];
            }
            if ((symbol & CADability.GeoObject.PointSymbol.Square) != 0)
            {
                bmp = BitmapList[4 + offset];
            }
            if (bmp != null)
            {
                paintTo3D.DisplayIcon(location, bmp);
            }
        }
        public static void Arrow(IPaintTo3D paintTo3D, GeoPoint base1, GeoPoint base2, GeoPoint apex)
        {
            GeoVector nor1 = (apex - base1) ^ (apex - base2);
            nor1.Norm();
            double l = base1 | base2;
            GeoPoint c = new GeoPoint(base1, base2);
            GeoPoint base3 = c + l / 2.0 * nor1;
            GeoPoint base4 = c - l / 2.0 * nor1;
            GeoVector nor2 = (apex - base3) ^ (apex - base4);
            paintTo3D.Triangle(new GeoPoint[] { apex, base1, base2 }, new GeoVector[] { nor1, nor1, nor1 }, new int[] { 0, 1, 2 });
            paintTo3D.Triangle(new GeoPoint[] { apex, base3, base4 }, new GeoVector[] { nor2, nor2, nor2 }, new int[] { 0, 1, 2 });
        }
        public static void Arrow1(IPaintTo3D paintTo3D, GeoPoint base1, GeoPoint base2, GeoPoint apex)
        {
            GeoVector nor1 = (apex - base1) ^ (apex - base2);
            nor1.Norm();
            //double l = base1 | base2;
            //GeoPoint c = new GeoPoint(base1, base2);
            //GeoPoint base3 = c + l / 2.0 * nor1;
            //GeoPoint base4 = c - l / 2.0 * nor1;
            //GeoVector nor2 = (apex - base3) ^ (apex - base4);
            paintTo3D.Triangle(new GeoPoint[] { apex, base1, base2 }, new GeoVector[] { nor1, nor1, nor1 }, new int[] { 0, 1, 2 });
            //paintTo3D.Triangle(new GeoPoint[] { apex, base3, base4 }, new GeoVector[] { nor2, nor2, nor2 }, new int[] { 0, 1, 2 });
        }

        public static void PreparePointSymbol(IPaintTo3D paintTo3D, PointSymbol symbol)
        {
            int offset = 0;
            if (paintTo3D.UseLineWidth) offset = 6; // so wird gesteuert dass bei nur dünn die dünnen Punkte und bei
            // mit Linienstärke ggf. die dicken Punkte angezeigt werden (Forderung PFOCAD)
            Bitmap bmp = null;
            switch ((GeoObject.PointSymbol)((int)symbol & 0x07))
            {
                case CADability.GeoObject.PointSymbol.Empty:
                    bmp = null;
                    break;
                case CADability.GeoObject.PointSymbol.Dot:
                    {
                        bmp = BitmapList[0 + offset];
                    }
                    break;
                case CADability.GeoObject.PointSymbol.Plus:
                    {
                        bmp = BitmapList[1 + offset];
                    }
                    break;
                case CADability.GeoObject.PointSymbol.Cross:
                    {
                        bmp = BitmapList[2 + offset];
                    }
                    break;
                case CADability.GeoObject.PointSymbol.Line:
                    {
                        bmp = BitmapList[3 + offset];
                    }
                    break;
            }
            if (bmp != null)
            {
                paintTo3D.PrepareIcon(bmp);
            }
            bmp = null;
            if ((symbol & CADability.GeoObject.PointSymbol.Circle) != 0)
            {
                bmp = BitmapList[5 + offset];
            }
            if ((symbol & CADability.GeoObject.PointSymbol.Square) != 0)
            {
                bmp = BitmapList[4 + offset];
            }
            if ((symbol & CADability.GeoObject.PointSymbol.Select) != 0)
            {
                bmp = BitmapList[12];
            }
            if (bmp != null)
            {
                paintTo3D.PrepareIcon(bmp);
            }
        }
    }


    public interface IPaintTo3DList
    {
        string Name { get; set; }
        List<IPaintTo3DList> containedSubLists { set; }
        void Dispose();
    }

    /// <summary>
    /// This class defines static events that can be used to customize the OpenGL implementation
    /// </summary>

    public class OpenGlCustomize
    {
        /// <summary>
        /// Delegate definition of <see cref="SetProjectionEvent"/>.
        /// 
        /// </summary>
        /// <param name="renderContext">The OpenGL render context</param>
        /// <param name="paintTo3D">The IPaintTo3D interface of the instance beeing involved</param>
        /// <param name="projection">The projetion that has been set</param>
        /// <param name="boundingCube">The bounding cube for the display</param>
        public delegate void SetProjectionDelegate(IntPtr renderContext, IPaintTo3D paintTo3D, Projection projection, BoundingCube boundingCube);
        /// <summary>
        /// Event that is raised when the projection of the OpenGL view was changed. You can modify the light sources and direction.
        /// The original code for setting the light model is:
        /// <code>
        /// Gl.glLightModeli(Gl.GL_LIGHT_MODEL_TWO_SIDE, Gl.GL_TRUE);
        /// Gl.glEnable(Gl.GL_LIGHTING);
        /// Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, new float[] { (float)v.x, (float)v.y, (float)v.z, 0.0f });
        /// Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_AMBIENT, new float[] { 0.2f, 0.2f, 0.2f, 1.0f });
        /// Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_DIFFUSE, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
        /// Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_SPECULAR, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
        /// Gl.glEnable(Gl.GL_LIGHT0);
        /// </code>
        /// </summary>
        public static event SetProjectionDelegate SetProjectionEvent;
        internal static void InvokeSetProjection(IntPtr renderContext, IPaintTo3D paintTo3D, Projection projection, BoundingCube boundingCube)
        {
            if (SetProjectionEvent != null)
            {
                SetProjectionEvent(renderContext, paintTo3D, projection, boundingCube);
            }
        }
        /// <summary>
        /// Paint the provided IGeoObjects onto a bitmap with the help form OpenGL. The viewDirection specifies in which direction the objects are projected (e.g. (0,0,-1) is from top)
        /// the objects fill the bitmap and leave a 10% empty frame.
        /// </summary>
        /// <param name="list">objects to paint</param>
        /// <param name="viewDirection">direction of view</param>
        /// <param name="width">of the bitmap</param>
        /// <param name="height">of the bitmap</param>
        /// <returns></returns>
        public static Bitmap PaintToBitmap(GeoObjectList list, GeoVector viewDirection, int width, int height, BoundingCube? extent = null)
        {
            throw new NotImplementedException("PaintToBitmap not implemented, maybe implement in CADability.Forms");
            //Bitmap bmp = new Bitmap(width, height);
            //Graphics gr = Graphics.FromImage(bmp);
            //IntPtr dc = gr.GetHdc();
            //BoundingCube bc;
            //if (extent.HasValue) bc = extent.Value;
            //else bc = list.GetExtent();
            //PaintToOpenGl paintTo3D = new PaintToOpenGl(bc.Size / Math.Max(width, height));
            //paintTo3D.Init(dc, width, height, true);
            //IPaintTo3D ipaintTo3D = paintTo3D;
            //ipaintTo3D.MakeCurrent();
            //ipaintTo3D.Clear(Color.White);
            //ipaintTo3D.AvoidColor(Color.White);

            //Projection projection = new Projection(Projection.StandardProjection.FromTop);
            //if (Precision.SameDirection(viewDirection, GeoVector.ZAxis, false)) projection.SetDirection(viewDirection, GeoVector.YAxis, bc);
            //else projection.SetDirection(viewDirection, GeoVector.ZAxis, bc);
            //projection.Precision = bc.Size * 1e-3;

            //BoundingRect ext = bc.GetExtent(projection); //  list.GetExtent(projection, true, false);
            //ext = ext * 1.1; // inflate by 10 percent 
            //projection.SetPlacement(new Rectangle(0, 0, bmp.Width, bmp.Height), ext);

            //ipaintTo3D.SetProjection(projection, bc);
            //foreach (IGeoObject go in list)
            //{
            //    go.PrePaintTo3D(ipaintTo3D);
            //}
            //foreach (IGeoObject go in list)
            //{
            //    go.PaintTo3D(ipaintTo3D);
            //}

            //gr.ReleaseHdc(dc);
            //bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
            //ipaintTo3D.Dispose();
            //return bmp;
        }
    }

}
